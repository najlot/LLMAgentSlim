# Gemini instructions for `LLMAgentSlim`

`LLMAgentSlim` is a minimal `.NET 10` application that hosts a Semantic Kernel-based coding agent and runs against an Ollama model.
The app exposes a workspace-scoped toolset for common development tasks, available via both a console interface and an Avalonia UI frontend.

## Repository purpose

This repository provides:
- agent startup and configuration loading
- provider configuration for Ollama-based chat completion
- workspace-scoped plugins for notes, C# execution, file system access, directory operations, search, shell commands, git, and `dotnet`
- an Avalonia UI frontend for interacting with the agent

## Tech stack

- C#
- .NET 10
- Semantic Kernel
- Ollama / OllamaSharp
- LiteDB
- Avalonia UI

## Repository structure

- `src/LLMAgentSlim.sln` - the primary solution file
- `src/LLMAgentSlim/` - console application entry point, configuration loading, and kernel setup
- `src/LLMAgentSlim.Plugins/` - dedicated project for plugins and supporting types
- `src/LLMAgentSlimGUI/` - Avalonia UI frontend application
- `README.md` - usage and configuration documentation

## Build and validation

Before finishing a change, use the commands that match the scope of the task:

- Build:
  - `dotnet build src/LLMAgentSlim.sln`
- Run Console App:
  - `dotnet run --project src/LLMAgentSlim/LLMAgentSlim.csproj`
- Run GUI App:
  - `dotnet run --project src/LLMAgentSlimGUI/LLMAgentSlimGUI.csproj`
- If tests are added in the future:
  - `dotnet test src/LLMAgentSlim.sln`

There does not appear to be a dedicated test project in the repository today. Do not invent test results or assume tests exist.

## Configuration guidance

The application loads `llmagentslim.json` from:
1. an explicit `--config` path
2. the selected working directory
3. the app data directory

When changing configuration behavior:
- keep `Program.cs`, configuration models, and `README.md` in sync
- preserve the documented lookup order unless the task explicitly requires changing it
- keep error messages clear and actionable

## Coding guidelines

- Target `net10.0`
- Preserve nullable reference types and implicit usings
- Follow the existing repository style and naming patterns
- Prefer minimal, focused changes over broad refactoring
- Keep behavior changes scoped to the requested task
- Do not rename plugins, commands, or configuration keys unless the task requires it
- Prefer clear exceptions and validation for invalid paths, missing files, and unsupported providers
- Keep console interaction predictable and easy to follow

## Plugin and agent-specific guidance

When changing or adding plugin behavior:
- keep plugins workspace-scoped
- do not assume files or directories exist without checking
- prefer direct, deterministic tool behavior
- avoid adding hidden side effects
- keep public method names descriptive because they shape tool usage

When changing the agent flow:
- preserve the step-by-step tool-driven interaction model
- keep prompts and completion behavior concise
- do not remove safeguards against invented file or tool results unless explicitly requested

## Documentation expectations

Update `README.md` when changes affect:
- startup arguments
- configuration schema
- provider behavior
- plugin capabilities
- build or run instructions