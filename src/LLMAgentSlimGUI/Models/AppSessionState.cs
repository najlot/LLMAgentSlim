using System.Collections.Generic;

namespace LLMAgentSlimGUI.Models;

internal sealed class AppSessionState
{
	public string? LastWorkspacePath { get; set; }

	public List<string> SelectedPluginKeys { get; set; } = [];
}
