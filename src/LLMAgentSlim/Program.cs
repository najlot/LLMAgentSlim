using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using LLMAgentSlim;

var startupOptions = StartupOptions.Parse(args);
var currentDir = ResolveWorkingDirectory(startupOptions.WorkingDirectory);
var configurationPath = ResolveConfigurationPath(startupOptions.ConfigurationPath, currentDir);
Directory.SetCurrentDirectory(currentDir);

var configurationDirectory = Path.GetDirectoryName(configurationPath)
	?? throw new InvalidOperationException($"Could not determine the configuration directory for '{configurationPath}'.");

var configuration = new ConfigurationBuilder()
	.SetBasePath(configurationDirectory)
	.AddJsonFile(Path.GetFileName(configurationPath), optional: false, reloadOnChange: false)
	.Build();

var agentConfiguration = configuration
	.GetRequiredSection("LLMAgentSlim")
	.Get<LLMAgentSlimConfiguration>()
	?? throw new InvalidOperationException("The LLMAgentSlim configuration section is missing or invalid.");

var systemPrompt = """
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

systemPrompt = systemPrompt
	.Replace("PROJECT_DIR", currentDir)
	.Replace("PROJECT_OS", Environment.OSVersion.ToString());

var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IFunctionInvocationFilter, ConsoleFunctionInvocationFilter>();
PromptExecutionSettings settings;
HttpClient? configuredHttpClient = null;

switch (agentConfiguration.Provider.Trim().ToLowerInvariant())
{
	case "ollama":
		Console.WriteLine("Using Ollama as the provider.");
		Console.WriteLine($"Endpoint: {agentConfiguration.Providers.Ollama.Endpoint}");
		Console.WriteLine($"Model: {agentConfiguration.Providers.Ollama.Model}");

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
			Stop = ["TASK_COMPLETED"]
		};

		break;
	case "openai":
		Console.WriteLine("Using OpenAI-compatible provider.");
		Console.WriteLine($"Endpoint: {agentConfiguration.Providers.OpenAI.Endpoint}");
		Console.WriteLine($"Model: {agentConfiguration.Providers.OpenAI.Model}");

		configuredHttpClient = new HttpClient
		{
			BaseAddress = new Uri(agentConfiguration.Providers.OpenAI.Endpoint),
			Timeout = TimeSpan.FromMinutes(agentConfiguration.Providers.OpenAI.TimeoutMinutes)
		};

		builder.AddOpenAIChatCompletion(
			modelId: agentConfiguration.Providers.OpenAI.Model,
			endpoint: new Uri(agentConfiguration.Providers.OpenAI.Endpoint),
			apiKey: NullIfWhiteSpace(agentConfiguration.Providers.OpenAI.ApiKey),
			orgId: NullIfWhiteSpace(agentConfiguration.Providers.OpenAI.OrganizationId),
			httpClient: configuredHttpClient);

		settings = new OpenAIPromptExecutionSettings
		{
			TopP = agentConfiguration.Providers.OpenAI.TopP,
			Temperature = agentConfiguration.Providers.OpenAI.Temperature,
			FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
			StopSequences = ["TASK_COMPLETED"]
		};

		break;
	default:
		throw new InvalidOperationException($"Unsupported provider '{agentConfiguration.Provider}'.");
}

using var httpClient = configuredHttpClient;
using var pluginHttpClient = new HttpClient();
var cancellationTokenSource = new CancellationTokenSource();
var isAgentRunning = false;

Console.CancelKeyPress += (_, eventArgs) =>
{
	if (!isAgentRunning)
	{
		return;
	}

	eventArgs.Cancel = true;
	cancellationTokenSource.Cancel();
	Console.WriteLine();
	Console.WriteLine("Stop requested. Cancelling the current agent run...");
};

builder.Plugins.AddFromObject(new FinishPlugin(() => cancellationTokenSource));
builder.Plugins.AddFromObject(new MemoryPlugin(currentDir));
builder.Plugins.AddFromObject(new FileSystemPlugin(currentDir));
builder.Plugins.AddFromObject(new DirectoryPlugin(currentDir));
builder.Plugins.AddFromObject(new SearchPlugin(currentDir));
builder.Plugins.AddFromObject(new ShellPlugin(currentDir));
builder.Plugins.AddFromObject(new GitPlugin(currentDir));
builder.Plugins.AddFromObject(new DotNetPlugin(currentDir));
builder.Plugins.AddFromObject(new HttpPlugin(currentDir, pluginHttpClient));
builder.Plugins.AddFromObject(new UserInputPlugin());
builder.Plugins.AddFromObject(new CSharpPlugin());

ChatHistory history = [];
history.AddSystemMessage(systemPrompt);

Console.WriteLine("Directory: " + currentDir);
Console.WriteLine();

Console.WriteLine("Instruction or 'exit' to quit:");
Console.Write(">>> ");
var instruction = Console.ReadLine();
while (string.IsNullOrWhiteSpace(instruction))
{
	Console.WriteLine("Instruction cannot be empty. Please enter a valid instruction:");
	Console.Write(">>> ");
	instruction = Console.ReadLine();
}

if (instruction.Trim().ToLowerInvariant() == "exit")
{
	Console.WriteLine("Exiting...");
	return;
}

history.AddUserMessage(instruction);

var kernel = builder.Build();
var chat = kernel.GetRequiredService<IChatCompletionService>();

while (true)
{
	try
	{
		isAgentRunning = true;
		var response = await chat.GetChatMessageContentAsync(
			history,
			settings,
			kernel,
			cancellationTokenSource.Token
		).ConfigureAwait(false);

		history.Add(response);

		Console.WriteLine(response.Content);
	}
	catch (TaskCanceledException)
	{
		Console.WriteLine("Agent run stopped.");
		cancellationTokenSource = new CancellationTokenSource();
	}
	finally
	{
		isAgentRunning = false;
	}

	Console.WriteLine("Instruction or 'exit' to quit:");
	Console.Write(">>> ");
	var feedback = Console.ReadLine();

	while (string.IsNullOrWhiteSpace(feedback))
	{
		Console.WriteLine("Instruction cannot be empty. Please enter a valid instruction:");
		Console.Write(">>> ");
		feedback = Console.ReadLine();
	}

	if (feedback.Trim().ToLowerInvariant() == "exit")
	{
		Console.WriteLine("Exiting...");
		return;
	}

	history.AddUserMessage(feedback);
}

static string ResolveWorkingDirectory(string? requestedWorkingDirectory)
{
	var candidatePath = string.IsNullOrWhiteSpace(requestedWorkingDirectory)
		? Directory.GetCurrentDirectory()
		: requestedWorkingDirectory;

	var fullPath = Path.GetFullPath(candidatePath);
	if (!Directory.Exists(fullPath))
	{
		throw new DirectoryNotFoundException($"The working directory '{fullPath}' does not exist.");
	}

	return fullPath;
}

static string ResolveConfigurationPath(string? requestedConfigurationPath, string workingDirectory)
{
	if (!string.IsNullOrWhiteSpace(requestedConfigurationPath))
	{
		var explicitConfigurationPath = ResolveAgainstWorkingDirectory(requestedConfigurationPath, workingDirectory);
		if (!File.Exists(explicitConfigurationPath))
		{
			throw new FileNotFoundException(
				$"The configuration file '{explicitConfigurationPath}' does not exist.",
				explicitConfigurationPath);
		}

		return explicitConfigurationPath;
	}

	var workspaceConfigurationPath = Path.Combine(workingDirectory, LLMAgentSlimPaths.ConfigurationFileName);
	if (File.Exists(workspaceConfigurationPath))
	{
		return workspaceConfigurationPath;
	}

	var appDataDirectory = LLMAgentSlimPaths.GetAppDataDirectory();
	var appDataConfigurationPath = Path.Combine(appDataDirectory, LLMAgentSlimPaths.ConfigurationFileName);
	if (File.Exists(appDataConfigurationPath))
	{
		return appDataConfigurationPath;
	}

	throw new FileNotFoundException(
		$"Could not find '{LLMAgentSlimPaths.ConfigurationFileName}' in '{workingDirectory}' or '{appDataDirectory}'.");
}

static string ResolveAgainstWorkingDirectory(string path, string workingDirectory)
{
	if (Path.IsPathRooted(path))
	{
		return Path.GetFullPath(path);
	}

	return Path.GetFullPath(Path.Combine(workingDirectory, path));
}

static string? NullIfWhiteSpace(string? value)
{
	return string.IsNullOrWhiteSpace(value) ? null : value;
}