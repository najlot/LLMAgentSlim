using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace LLMAgentSlimGUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	private ViewModelBase _currentContent;
	private PreRunConversationViewModel _preRunViewModel;

	public MainWindowViewModel()
	{
		_preRunViewModel = CreatePreRunViewModel();
		_currentContent = _preRunViewModel;
	}

	public ViewModelBase CurrentContent
	{
		get => _currentContent;
		private set => SetProperty(ref _currentContent, value);
	}

	[ObservableProperty]
	private string statusMessage = "Select a workspace to begin.";

	public async Task InitializeAsync(Window? window)
	{
		await _preRunViewModel.InitializeAsync(window);
	}

	private PreRunConversationViewModel CreatePreRunViewModel()
	{
		return new PreRunConversationViewModel(
			onStatusChanged: status =>
			{
				StatusMessage = status;
				return Task.CompletedTask;
			},
			onRunRequested: async (workspacePath, configurationText, selectedPluginKeys) =>
			{
				var requestText = _preRunViewModel.RequestText;
				var activeVm = new ActiveConversationViewModel(
					workspacePath,
					configurationText,
					selectedPluginKeys,
					onStatusChanged: status =>
					{
						StatusMessage = status;
						return Task.CompletedTask;
					},
					onNewConversationRequested: async () =>
					{
						if (CurrentContent is ActiveConversationViewModel old)
						{
							await old.DisposeAsync();
						}

						_preRunViewModel = CreatePreRunViewModel();
						await _preRunViewModel.LoadWorkspaceAsync(workspacePath, saveState: false);
						CurrentContent = _preRunViewModel;
						StatusMessage = "Ready for a new conversation.";
					});

				CurrentContent = activeVm;
				await activeVm.StartInitialTurnAsync(requestText);
			});
	}
}
