using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace LLMAgentSlimGUI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	private readonly PreRunConversationViewModel _setupViewModel;
	private ActiveConversationViewModel? _conversationViewModel;
	private int _selectedTabIndex;

	public MainWindowViewModel()
	{
		_setupViewModel = CreateSetupViewModel();
		_setupViewModel.PropertyChanged += OnSetupViewModelPropertyChanged;
		_selectedTabIndex = 0;
	}

	public PreRunConversationViewModel SetupViewModel => _setupViewModel;

	public ActiveConversationViewModel? ConversationViewModel
	{
		get => _conversationViewModel;
		private set
		{
			if (SetProperty(ref _conversationViewModel, value))
			{
				OnPropertyChanged(nameof(IsConversationTabEnabled));
			}
		}
	}

	public bool IsConversationTabEnabled => ConversationViewModel is not null;

	public int SelectedTabIndex
	{
		get => _selectedTabIndex;
		set => SetProperty(ref _selectedTabIndex, value);
	}

	public string WorkspaceSummary => string.IsNullOrWhiteSpace(SetupViewModel.WorkspacePath)
		? "No workspace selected"
		: SetupViewModel.WorkspacePath;

	[ObservableProperty]
	public partial string StatusMessage { get; set; } = "Select a workspace to begin.";

	public async Task InitializeAsync(Window? window)
	{
		await SetupViewModel.InitializeAsync(window);
	}

	private PreRunConversationViewModel CreateSetupViewModel()
	{
		return new PreRunConversationViewModel(
			onStatusChanged: status =>
			{
				StatusMessage = status;
				return Task.CompletedTask;
			},
			onRunRequested: async (workspacePath, configurationText, selectedPluginKeys) =>
			{
				await DisposeConversationAsync();

				var requestText = SetupViewModel.RequestText;
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
							await DisposeConversationAsync();
							SelectedTabIndex = 0;
						StatusMessage = "Ready for a new conversation.";
					});

				ConversationViewModel = activeVm;
				SelectedTabIndex = 1;
				await activeVm.StartInitialTurnAsync(requestText);
			});
	}

	private async Task DisposeConversationAsync()
	{
		if (ConversationViewModel is null)
		{
			return;
		}

		var previousConversation = ConversationViewModel;
		ConversationViewModel = null;
		await previousConversation.DisposeAsync();
	}

	private void OnSetupViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(PreRunConversationViewModel.WorkspacePath) or nameof(PreRunConversationViewModel.IsWorkspaceLoaded))
		{
			OnPropertyChanged(nameof(WorkspaceSummary));
		}
	}
}
