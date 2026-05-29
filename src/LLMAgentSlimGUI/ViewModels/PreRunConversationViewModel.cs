using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMAgentSlimGUI.Models;
using LLMAgentSlimGUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LLMAgentSlimGUI.ViewModels;

public partial class PreRunConversationViewModel : ObservableObject
{
	private readonly WorkspaceConfigurationService _workspaceConfigurationService = new();
	private readonly SessionStateStore _sessionStateStore = new();
	private AppSessionState _sessionState = new();
	private readonly Func<string, Task> _onStatusChanged;
	private readonly Func<string, string, string[], Task> _onRunRequested;

	public PreRunConversationViewModel(
		Func<string, Task> onStatusChanged,
		Func<string, string, string[], Task> onRunRequested)
	{
		_onStatusChanged = onStatusChanged;
		_onRunRequested = onRunRequested;
		AvailablePlugins = CreateDefaultPluginSelections();
	}

	public ObservableCollection<PluginSelectionItem> AvailablePlugins { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanRun))]
	[NotifyCanExecuteChangedFor(nameof(RunCommand))]
	public partial string RequestText { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveConfigurationCommand))]
	public partial string ConfigurationText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string WorkspacePath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ConfigurationSourceLabel { get; set; } = "Using defaults";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanRun))]
	[NotifyCanExecuteChangedFor(nameof(RunCommand))]
	[NotifyCanExecuteChangedFor(nameof(SaveConfigurationCommand))]
	[NotifyCanExecuteChangedFor(nameof(SavePluginSelectionCommand))]
	public partial bool IsWorkspaceLoaded { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanRun))]
	[NotifyCanExecuteChangedFor(nameof(RunCommand))]
	[NotifyCanExecuteChangedFor(nameof(SaveConfigurationCommand))]
	[NotifyCanExecuteChangedFor(nameof(SavePluginSelectionCommand))]
	public partial bool IsBusy { get; set; }

	public bool CanRun => IsWorkspaceLoaded && !IsBusy && !string.IsNullOrWhiteSpace(RequestText);

	public bool CanSaveConfiguration => IsWorkspaceLoaded && !IsBusy && !string.IsNullOrWhiteSpace(ConfigurationText);

	public bool CanSavePluginSelection => IsWorkspaceLoaded && !IsBusy && AvailablePlugins.Count > 0;

	public async Task InitializeAsync(Window? window)
	{
		try
		{
			_sessionState = await _sessionStateStore.LoadAsync(CancellationToken.None);
			ApplySelectedPluginKeys(_sessionState.SelectedPluginKeys);

			if (!string.IsNullOrWhiteSpace(_sessionState.LastWorkspacePath) && Directory.Exists(_sessionState.LastWorkspacePath))
			{
				await LoadWorkspaceAsync(_sessionState.LastWorkspacePath, saveState: false);
				return;
			}

			await _onStatusChanged("Select a workspace to begin.");
			if (window is not null)
			{
				await PickWorkspaceAsync(window);
			}
		}
		catch (Exception ex)
		{
			await _onStatusChanged(ex.Message);
		}
	}

	public async Task LoadWorkspaceAsync(string workspacePath, bool saveState)
	{
		var session = await _workspaceConfigurationService.LoadWorkspaceAsync(workspacePath, CancellationToken.None);
		WorkspacePath = session.WorkspacePath;
		ConfigurationText = session.ConfigurationText;
		ConfigurationSourceLabel = session.ConfigurationExistsInWorkspace
			? $"Loaded from workspace: {session.EffectiveConfigurationPath}"
			: File.Exists(session.EffectiveConfigurationPath)
				? $"Loaded from app data: {session.EffectiveConfigurationPath}"
				: "Using default configuration values";
		IsWorkspaceLoaded = true;
		await _onStatusChanged($"Workspace loaded: {session.WorkspacePath}");

		if (saveState)
		{
			_sessionState.LastWorkspacePath = session.WorkspacePath;
			await _sessionStateStore.SaveAsync(_sessionState, CancellationToken.None);
		}
	}

	public string[] GetSelectedPluginKeys() => AvailablePlugins
		.Where(plugin => plugin.IsEnabled)
		.Select(plugin => plugin.Key)
		.ToArray();

	public void ApplySelectedPluginKeys(IReadOnlyCollection<string>? selectedPluginKeys)
	{
		if (selectedPluginKeys is null || selectedPluginKeys.Count == 0)
		{
			return;
		}

		var selected = selectedPluginKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var plugin in AvailablePlugins)
		{
			plugin.IsEnabled = selected.Contains(plugin.Key);
		}
	}

	[RelayCommand(CanExecute = nameof(CanRun))]
	private async Task RunAsync()
	{
		if (string.IsNullOrWhiteSpace(RequestText))
		{
			return;
		}

		IsBusy = true;
		try
		{
			await _onRunRequested(WorkspacePath, ConfigurationText, GetSelectedPluginKeys());
		}
		catch (Exception ex)
		{
			await _onStatusChanged(ex.Message);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task PickWorkspaceAsync(Window? window)
	{
		if (window?.StorageProvider is null)
		{
			await _onStatusChanged("Workspace selection is not available.");
			return;
		}

		var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			AllowMultiple = false,
			Title = "Select workspace folder"
		});

		var folder = folders.FirstOrDefault();
		if (folder is null)
		{
			return;
		}

		await LoadWorkspaceAsync(folder.Path.LocalPath, saveState: true);
	}

	[RelayCommand(CanExecute = nameof(CanSaveConfiguration))]
	private async Task SaveConfigurationAsync()
	{
		try
		{
			await _workspaceConfigurationService.SaveWorkspaceConfigurationAsync(WorkspacePath, ConfigurationText, CancellationToken.None);
			ConfigurationSourceLabel = $"Saved to {Path.Combine(WorkspacePath, GuiPaths.ConfigurationFileName)}";
			await _onStatusChanged("Configuration saved.");
		}
		catch (Exception ex)
		{
			await _onStatusChanged(ex.Message);
		}
	}

	[RelayCommand(CanExecute = nameof(CanSavePluginSelection))]
	private async Task SavePluginSelectionAsync()
	{
		try
		{
			_sessionState.SelectedPluginKeys = [.. GetSelectedPluginKeys()];
			if (IsWorkspaceLoaded)
			{
				_sessionState.LastWorkspacePath = WorkspacePath;
			}

			await _sessionStateStore.SaveAsync(_sessionState, CancellationToken.None);
			await _onStatusChanged("Plugin selection saved.");
		}
		catch (Exception ex)
		{
			await _onStatusChanged(ex.Message);
		}
	}

	private static ObservableCollection<PluginSelectionItem> CreateDefaultPluginSelections()
	{
		return
		[
			new PluginSelectionItem("Memory", "Memory", "Save and recall findings across steps."),
			new PluginSelectionItem("FileSystem", "File System", "Read, write, patch, append, and delete files in the workspace."),
			new PluginSelectionItem("Directory", "Directory", "List and create directories in the workspace."),
			new PluginSelectionItem("Search", "Search", "Find files and search text in the workspace."),
			new PluginSelectionItem("Shell", "Shell", "Run non-interactive shell commands in the workspace."),
			new PluginSelectionItem("Git", "Git", "Inspect repository status, diffs, and recent commits."),
			new PluginSelectionItem("DotNet", ".NET", "Build and test .NET projects in the workspace."),
			new PluginSelectionItem("Http", "HTTP", "Fetch web content and call HTTP endpoints from the workspace context."),
			new PluginSelectionItem("UserInput", "User input", "Ask for decisions, clarifications, and confirmations during a run."),
			new PluginSelectionItem("CSharp", "C#", "Run ad hoc C# code when a direct plugin is not available.")
		];
	}
}
