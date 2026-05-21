using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using LLMAgentSlim;
using LLMAgentSlimGUI.Models;
using System.Text.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace LLMAgentSlimGUI.Services;

internal sealed class AgentRunner : IAsyncDisposable
{
	private const string TaskCompletedToken = "TASK_COMPLETED";
	private const string SystemPromptTemplate = """
Rules:
- Solve the user task step by step.
- Use tools whenever needed.
- Think before acting.
- After every tool result, evaluate the next step.
- If the task is completed, respond ONLY with:
TASK_COMPLETED

- Never invent tool results.
- Never assume files exist.
- Keep responses short.
 - Prefer the FileSystem, Directory, Search, Shell, Git, DotNet, and Http plugins for workspace actions.
- Use the Memory plugin to save and recall information across steps (findings, decisions, intermediate results).
- Use the UserInput plugin to ask the user for decisions, clarifications, or confirmations whenever you are uncertain how to proceed.
- Use the CSharp plugin only when a more direct plugin is not available.

You are running on "PROJECT_OS" in `PROJECT_DIR`.
ALWAYS explore the environment and read the files needed to understand the current context before doing any action.
""";

	private readonly string _workspacePath;
	private readonly string _configurationText;
	private readonly HashSet<string> _selectedPluginKeys;
	private readonly Action<string, bool> _logAction;
	private readonly Func<string, CancellationToken, Task<string>> _promptHandler;
	private ChatHistory? _history;
	private Kernel? _kernel;
	private IChatCompletionService? _chatCompletionService;
	private PromptExecutionSettings? _settings;
	private HttpClient? _configuredHttpClient;
	private HttpClient? _pluginHttpClient;
	private CancellationTokenSource _cancellationTokenSource = new();
	private int _isRunInProgress;

	public AgentRunner(
		string workspacePath,
		string configurationText,
		IEnumerable<string> selectedPluginKeys,
		Action<string, bool> logAction,
		Func<string, CancellationToken, Task<string>> promptHandler)
	{
		_workspacePath = Path.GetFullPath(workspacePath);
		_configurationText = configurationText;
		_selectedPluginKeys = selectedPluginKeys
			.Where(static key => !string.IsNullOrWhiteSpace(key))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		_logAction = logAction;
		_promptHandler = promptHandler;
	}

	public Task InitializeAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!Directory.Exists(_workspacePath))
		{
			throw new DirectoryNotFoundException($"The workspace '{_workspacePath}' does not exist.");
		}

		using var _ = JsonDocument.Parse(_configurationText);

		var configuration = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_configurationText)))
			.Build();

		var agentConfiguration = configuration
			.GetRequiredSection("LLMAgentSlim")
			.Get<AgentConfiguration>()
			?? throw new InvalidOperationException("The LLMAgentSlim configuration section is missing or invalid.");

		var builder = Kernel.CreateBuilder();
		builder.Services.AddSingleton<IFunctionInvocationFilter>(_ => new GuiFunctionInvocationFilter(_logAction));

		HttpClient? configuredHttpClient = null;
		PromptExecutionSettings settings;

		switch (agentConfiguration.Provider.Trim().ToLowerInvariant())
		{
			case "ollama":
				_logAction($"Using Ollama as the provider. Endpoint: {agentConfiguration.Providers.Ollama.Endpoint} Model: {agentConfiguration.Providers.Ollama.Model}", false);

				configuredHttpClient = new HttpClient
				{
					BaseAddress = new Uri(agentConfiguration.Providers.Ollama.Endpoint),
					Timeout = TimeSpan.FromMinutes(agentConfiguration.Providers.Ollama.TimeoutMinutes)
				};

				var chatClient = new OllamaApiClient(configuredHttpClient, agentConfiguration.Providers.Ollama.Model);
				builder.AddOllamaChatCompletion(chatClient);

				settings = new OllamaPromptExecutionSettings
				{
					TopK = agentConfiguration.Providers.Ollama.TopK,
					TopP = agentConfiguration.Providers.Ollama.TopP,
					Temperature = agentConfiguration.Providers.Ollama.Temperature,
					FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
					Stop = [TaskCompletedToken]
				};
				break;
			default:
				throw new InvalidOperationException($"Unsupported provider '{agentConfiguration.Provider}'.");
		}

		_configuredHttpClient = configuredHttpClient;
		_pluginHttpClient = new HttpClient();

		builder.Plugins.AddFromObject(new GuiFinishPlugin(() => _cancellationTokenSource));
		if (IsPluginEnabled("Memory"))
		{
			builder.Plugins.AddFromObject(new MemoryPlugin(_workspacePath));
		}

		if (IsPluginEnabled("FileSystem"))
		{
			builder.Plugins.AddFromObject(new FileSystemPlugin(_workspacePath));
		}

		if (IsPluginEnabled("Directory"))
		{
			builder.Plugins.AddFromObject(new DirectoryPlugin(_workspacePath));
		}

		if (IsPluginEnabled("Search"))
		{
			builder.Plugins.AddFromObject(new SearchPlugin(_workspacePath));
		}

		if (IsPluginEnabled("Shell"))
		{
			builder.Plugins.AddFromObject(new ShellPlugin(_workspacePath));
		}

		if (IsPluginEnabled("Git"))
		{
			builder.Plugins.AddFromObject(new GitPlugin(_workspacePath));
		}

		if (IsPluginEnabled("DotNet"))
		{
			builder.Plugins.AddFromObject(new DotNetPlugin(_workspacePath));
		}

		if (IsPluginEnabled("Http"))
		{
			builder.Plugins.AddFromObject(new HttpPlugin(_workspacePath, _pluginHttpClient));
		}

		if (IsPluginEnabled("UserInput"))
		{
			builder.Plugins.AddFromObject(new GuiUserInputPlugin(_promptHandler));
		}

		if (IsPluginEnabled("CSharp"))
		{
			builder.Plugins.AddFromObject(new CSharpPlugin());
		}

		var kernel = builder.Build();
		var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
		var history = new ChatHistory();
		history.AddSystemMessage(BuildSystemPrompt());

		_kernel = kernel;
		_chatCompletionService = chatCompletionService;
		_history = history;
		_settings = settings;

		return Task.CompletedTask;
	}

	public async Task<AgentTurnResult> RunTurnAsync(string userMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

		if (_history is null || _kernel is null || _chatCompletionService is null || _settings is null)
		{
			throw new InvalidOperationException("The agent is not initialized.");
		}

		_history.AddUserMessage(userMessage);

		using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

		try
		{
			Interlocked.Exchange(ref _isRunInProgress, 1);
			var response = await _chatCompletionService.GetChatMessageContentAsync(
				_history,
				_settings,
				_kernel,
				linkedCancellation.Token).ConfigureAwait(false);

			_history.Add(response);
			var message = response.Content?.Trim() ?? string.Empty;
			var isCompleted = string.Equals(message, TaskCompletedToken, StringComparison.Ordinal);
			return new AgentTurnResult(message, isCompleted, false);
		}
		catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
		{
			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = new CancellationTokenSource();
			return new AgentTurnResult(string.Empty, false, true);
		}
		finally
		{
			Interlocked.Exchange(ref _isRunInProgress, 0);
		}
	}

	public bool IsRunInProgress => Volatile.Read(ref _isRunInProgress) == 1;

	public bool StopCurrentRun()
	{
		if (_cancellationTokenSource.IsCancellationRequested)
		{
			return false;
		}

		_cancellationTokenSource.Cancel();
		return true;
	}

	public ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Dispose();
		_configuredHttpClient?.Dispose();
		_pluginHttpClient?.Dispose();
		return ValueTask.CompletedTask;
	}

	private string BuildSystemPrompt()
	{
		var prompt = SystemPromptTemplate
			.Replace("PROJECT_DIR", _workspacePath)
			.Replace("PROJECT_OS", Environment.OSVersion.ToString());

		var enabledPlugins = GetEnabledPluginDisplayNames();
		return enabledPlugins.Length == 0
			? prompt + Environment.NewLine + Environment.NewLine + "No optional workspace plugins are enabled for this run."
			: prompt + Environment.NewLine + Environment.NewLine + "Enabled optional workspace plugins: " + string.Join(", ", enabledPlugins) + ".";
	}

	private string[] GetEnabledPluginDisplayNames()
	{
		var orderedPlugins = new[]
		{
			("Memory", "Memory"),
			("FileSystem", "FileSystem"),
			("Directory", "Directory"),
			("Search", "Search"),
			("Shell", "Shell"),
			("Git", "Git"),
			("DotNet", "DotNet"),
			("Http", "Http"),
			("UserInput", "UserInput"),
			("CSharp", "CSharp")
		};

		return orderedPlugins
			.Where(plugin => IsPluginEnabled(plugin.Item1))
			.Select(plugin => plugin.Item2)
			.ToArray();
	}

	private bool IsPluginEnabled(string pluginKey) => _selectedPluginKeys.Contains(pluginKey);
}
