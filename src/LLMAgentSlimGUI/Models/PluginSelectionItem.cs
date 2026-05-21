using CommunityToolkit.Mvvm.ComponentModel;

namespace LLMAgentSlimGUI.Models;

public sealed partial class PluginSelectionItem : ObservableObject
{
	public PluginSelectionItem(string key, string displayName, string description, bool isEnabled = true)
	{
		Key = key;
		DisplayName = displayName;
		Description = description;
		this.isEnabled = isEnabled;
	}

	public string Key { get; }

	public string DisplayName { get; }

	public string Description { get; }

	[ObservableProperty]
	private bool isEnabled;
}