# LLMAgentSlim

LLMAgentSlim is a minimal Semantic Kernel coding agent that runs against an Ollama model and exposes a workspace-scoped toolset for common development tasks.

## Plugins

- NotesPlugin for lightweight task memory.
- CSharpPlugin for ad hoc C# execution.
- FileSystemPlugin for reading files, reading line ranges, patching exact snippets, writing, appending, and deleting files.
- DirectoryPlugin for listing and creating directories.
- SearchPlugin for file discovery and text search.
- ShellPlugin for non-interactive shell commands.
- GitPlugin for status, diff summary, and recent commits.
- DotNetPlugin for build and test commands.

## Run

```bash
dotnet run --project src/LLMAgentSlim/LLMAgentSlim.csproj
```

Avalonia GUI:

```bash
dotnet run --project src/LLMAgentSlimGUI/LLMAgentSlimGUI.csproj
```

Optional startup arguments:

```bash
dotnet run --project src/LLMAgentSlim/LLMAgentSlim.csproj -- --path /path/to/workspace
dotnet run --project src/LLMAgentSlim/LLMAgentSlim.csproj -- --config /path/to/llmagentslim.json
```

- `-p` or `--path` selects the working directory LLMAgentSlim uses as its current directory and workspace root.
- `-c` or `--config` selects an explicit configuration file. Relative config paths are resolved from the selected working directory.
- While the console agent is running, press `Ctrl+C` to stop the current run immediately and return to the prompt without exiting the app.

LLMAgentSlim reads its provider selection and provider-specific settings from `llmagentslim.json` in the current working directory. The repository includes an Ollama example:

Configuration lookup order:

- Explicit config file from `-c` or `--config`.
- `llmagentslim.json` in the effective working directory.
- `llmagentslim.json` in the appdata directory under `.llmagentslim/`.

## GUI workflow

- On startup, the Avalonia app reopens the last workspace when it still exists.
- If there is no remembered workspace, the app prompts for a folder selection.
- The GUI shows the effective configuration from the workspace file, the appdata fallback, or built-in defaults when no file exists yet.
- You can edit configuration JSON in the UI without saving, test a run with those changes, and save the current JSON to `llmagentslim.json` in the selected workspace when ready.
- Before starting a conversation, the GUI lets you enable or disable the optional workspace plugins that will be available for that run.
- The right side of the window shows the conversation history and a live activity list of invoked agent tools instead of console command output.
- While a GUI run is active, use the `Stop` button to cancel the current agent turn immediately.
- After a task completes, you can send follow-up suggestions in the same conversation or start a new conversation.

```json
{
	"LLMAgentSlim": {
		"Provider": "ollama",
		"Providers": {
			"Ollama": {
				"Endpoint": "http://localhost:11434/",
				"Model": "qwen3-coder:30b",
				"TimeoutMinutes": 10,
				"TopK": 10,
				"TopP": 0.5,
				"Temperature": 0.1
			}
		}
	}
}
```