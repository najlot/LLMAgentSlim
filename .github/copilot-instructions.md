# Copilot instructions for `LLMAgentSlim`

`LLMAgentSlim` is a minimal `.NET 10` console application that hosts a Semantic Kernel-based coding agent and runs against an Ollama model.
The app exposes a workspace-scoped toolset for common development tasks.

## Repository purpose

This repository provides:
- agent startup and configuration loading
- provider configuration for Ollama-based chat completion
- workspace-scoped plugins for notes, C# execution, file system access, directory operations, search, shell commands, git, and `dotnet`

## Tech stack

- C#
- .NET 10
- Semantic Kernel
- Ollama / OllamaSharp
- LiteDB

## Repository structure

- `src/LLMAgentSlim/Program.cs` - application entry point, configuration loading, kernel setup, and plugin registration
- `src/LLMAgentSlim/*.cs` - plugins and supporting types
- `README.md` - usage and configuration documentation

## Build and validation

Before finishing a change, use the commands that match the scope of the task:

- Build:
  - `dotnet build src/LLMAgentSlim/LLMAgentSlim.csproj`
- Run:
  - `dotnet run --project src/LLMAgentSlim/LLMAgentSlim.csproj`
- If tests are added in the future:
  - `dotnet test`

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