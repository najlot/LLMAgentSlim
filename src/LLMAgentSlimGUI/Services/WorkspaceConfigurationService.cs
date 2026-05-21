using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LLMAgentSlimGUI.Models;

namespace LLMAgentSlimGUI.Services;

internal sealed class WorkspaceConfigurationService
{
	private static readonly JsonSerializerOptions DefaultJsonOptions = new()
	{
		WriteIndented = true
	};

	public async Task<WorkspaceSession> LoadWorkspaceAsync(string workspacePath, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(workspacePath))
		{
			throw new ArgumentException("Workspace path cannot be empty.", nameof(workspacePath));
		}

		var fullWorkspacePath = Path.GetFullPath(workspacePath);
		if (!Directory.Exists(fullWorkspacePath))
		{
			throw new DirectoryNotFoundException($"The workspace '{fullWorkspacePath}' does not exist.");
		}

		var workspaceConfigurationPath = Path.Combine(fullWorkspacePath, GuiPaths.ConfigurationFileName);
		if (File.Exists(workspaceConfigurationPath))
		{
			return new WorkspaceSession
			{
				WorkspacePath = fullWorkspacePath,
				EffectiveConfigurationPath = workspaceConfigurationPath,
				ConfigurationText = await File.ReadAllTextAsync(workspaceConfigurationPath, cancellationToken).ConfigureAwait(false),
				ConfigurationExistsInWorkspace = true
			};
		}

		var appDataConfigurationPath = Path.Combine(GuiPaths.GetAppDataDirectory(), GuiPaths.ConfigurationFileName);
		if (File.Exists(appDataConfigurationPath))
		{
			return new WorkspaceSession
			{
				WorkspacePath = fullWorkspacePath,
				EffectiveConfigurationPath = appDataConfigurationPath,
				ConfigurationText = await File.ReadAllTextAsync(appDataConfigurationPath, cancellationToken).ConfigureAwait(false),
				ConfigurationExistsInWorkspace = false
			};
		}

		return new WorkspaceSession
		{
			WorkspacePath = fullWorkspacePath,
			EffectiveConfigurationPath = workspaceConfigurationPath,
			ConfigurationText = CreateDefaultConfigurationText(),
			ConfigurationExistsInWorkspace = false
		};
	}

	public async Task SaveWorkspaceConfigurationAsync(string workspacePath, string configurationText, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(workspacePath))
		{
			throw new ArgumentException("Workspace path cannot be empty.", nameof(workspacePath));
		}

		if (string.IsNullOrWhiteSpace(configurationText))
		{
			throw new ArgumentException("Configuration text cannot be empty.", nameof(configurationText));
		}

		JsonDocument.Parse(configurationText);

		var fullWorkspacePath = Path.GetFullPath(workspacePath);
		if (!Directory.Exists(fullWorkspacePath))
		{
			throw new DirectoryNotFoundException($"The workspace '{fullWorkspacePath}' does not exist.");
		}

		var configurationPath = Path.Combine(fullWorkspacePath, GuiPaths.ConfigurationFileName);
		await File.WriteAllTextAsync(configurationPath, configurationText, cancellationToken).ConfigureAwait(false);
	}

	public string CreateDefaultConfigurationText()
	{
		var configuration = new
		{
			LLMAgentSlim = new
			{
				Provider = "ollama",
				Providers = new
				{
					Ollama = new
					{
						Endpoint = "http://localhost:11434/",
						Model = "qwen3-coder:30b",
						TimeoutMinutes = 10,
						TopK = 10,
						TopP = 0.5,
						Temperature = 0.1
					}
				}
			}
		};

		return JsonSerializer.Serialize(configuration, DefaultJsonOptions);
	}
}
