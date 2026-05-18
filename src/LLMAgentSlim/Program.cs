using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
- Prefer the FileSystem, Directory, Search, Shell, Git, and DotNet plugins for workspace actions.
- Use the Notes plugin for keeping track of information.
- Use the CSharp plugin only when a more direct plugin is not available.

You are running on "PROJECT_OS" in `PROJECT_DIR`.
ALWAYS explore the environment and read the files needed to understand the current context before doing any action.
""";

systemPrompt = systemPrompt
	.Replace("PROJECT_DIR", currentDir)
	.Replace("PROJECT_OS", Environment.OSVersion.ToString());

var builder = Kernel.CreateBuilder();
PromptExecutionSettings settings;
HttpClient? configuredHttpClient = null;

switch (agentConfiguration.Provider.Trim().ToLowerInvariant())
{
	case "ollama":
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
	default:
		throw new InvalidOperationException($"Unsupported provider '{agentConfiguration.Provider}'.");
}

using var httpClient = configuredHttpClient;

builder.Plugins.AddFromObject(new NotesPlugin(currentDir));
builder.Plugins.AddFromObject(new CSharpPlugin());
builder.Plugins.AddFromObject(new FileSystemPlugin(currentDir));
builder.Plugins.AddFromObject(new DirectoryPlugin(currentDir));
builder.Plugins.AddFromObject(new SearchPlugin(currentDir));
builder.Plugins.AddFromObject(new ShellPlugin(currentDir));
builder.Plugins.AddFromObject(new GitPlugin(currentDir));
builder.Plugins.AddFromObject(new DotNetPlugin(currentDir));

ChatHistory history = [];
history.AddSystemMessage(systemPrompt);

Console.WriteLine("Instruction:");
Console.WriteLine(">>> ");
var instruction = Console.ReadLine();
while (string.IsNullOrWhiteSpace(instruction))
{
	Console.WriteLine("Instruction cannot be empty. Please enter a valid instruction:");
	instruction = Console.ReadLine();
}

history.AddUserMessage(instruction);

var kernel = builder.Build();
var chat = kernel.GetRequiredService<IChatCompletionService>();

for (int step = 1; ; step++)
{
	Console.WriteLine($"--- STEP {step} ---");

	var response = await chat.GetChatMessageContentAsync(
		history,
		settings,
		kernel
	).ConfigureAwait(false);

	history.Add(response);

	Console.WriteLine(response.Content);

	if (response.Content?.Contains("TASK_COMPLETED") == true
		|| response.Content?.Contains("I cannot continue") == true
		|| string.IsNullOrWhiteSpace(response.Content))
	{
		Console.WriteLine(">>> ");
		var feedback = Console.ReadLine();

		while (string.IsNullOrWhiteSpace(feedback))
		{
			Console.WriteLine("Instruction cannot be empty. Please enter a valid instruction:");
			Console.WriteLine(">>> ");
			feedback = Console.ReadLine();
		}

		history.AddUserMessage(feedback);
	}
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