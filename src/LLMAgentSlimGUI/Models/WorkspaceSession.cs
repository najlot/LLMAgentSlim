namespace LLMAgentSlimGUI.Models;

internal sealed class WorkspaceSession
{
	public required string WorkspacePath { get; init; }

	public required string EffectiveConfigurationPath { get; init; }

	public required string ConfigurationText { get; init; }

	public bool ConfigurationExistsInWorkspace { get; init; }
}
