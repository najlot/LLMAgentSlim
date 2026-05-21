using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMAgentSlimGUI.Models;
using LLMAgentSlimGUI.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace LLMAgentSlimGUI.ViewModels;

public partial class ActiveConversationViewModel : ViewModelBase, IAsyncDisposable
{
	private readonly string _workspacePath;
	private readonly string _configurationText;
	private readonly string[] _selectedPluginKeys;
	private readonly Func<string, Task> _onStatusChanged;
	private readonly Func<Task> _onNewConversationRequested;
	private readonly IRelayCommand _stopAgentCommand;
	private AgentRunner? _agentRunner;
	private TaskCompletionSource<string>? _pendingUserInputSource;

	public ActiveConversationViewModel(
		string workspacePath,
		string configurationText,
		string[] selectedPluginKeys,
		Func<string, Task> onStatusChanged,
		Func<Task> onNewConversationRequested)
	{
		_workspacePath = workspacePath;
		_configurationText = configurationText;
		_selectedPluginKeys = selectedPluginKeys;
		_onStatusChanged = onStatusChanged;
		_onNewConversationRequested = onNewConversationRequested;
		Conversation = [];
		_stopAgentCommand = new RelayCommand(StopAgent, () => CanStopAgent);
	}

	public ObservableCollection<ConversationEntry> Conversation { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasPendingQuestion))]
	public partial string PendingQuestion { get; set; } = string.Empty;

	public bool HasPendingQuestion => !string.IsNullOrWhiteSpace(PendingQuestion);

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SubmitPendingAnswerCommand))]
	private string pendingAnswer = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SubmitPendingAnswerCommand))]
	private bool isAwaitingUserInput;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SendFollowUpCommand))]
	private string followUpText = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanStopAgent))]
	[NotifyCanExecuteChangedFor(nameof(SendFollowUpCommand))]
	[NotifyCanExecuteChangedFor(nameof(StartNewConversationCommand))]
	private bool isBusy;

	private bool _hasConversation;

	public bool CanStopAgent => IsBusy && _agentRunner is not null;
	public bool CanSendFollowUp => _hasConversation && !IsBusy && !string.IsNullOrWhiteSpace(FollowUpText);
	public bool CanStartNewConversation => _hasConversation && !IsBusy;
	public bool CanSubmitPendingAnswer => IsAwaitingUserInput && !string.IsNullOrWhiteSpace(PendingAnswer);

	public IRelayCommand StopAgentCommand => _stopAgentCommand;

	public async Task StartInitialTurnAsync(string requestText)
	{
		Conversation.Clear();
		_hasConversation = true;
		await DisposeRunnerAsync();
		_agentRunner = new AgentRunner(_workspacePath, _configurationText, _selectedPluginKeys, LogCommand, PromptUserAsync);
		await _agentRunner.InitializeAsync(CancellationToken.None);
		await ExecuteTurnAsync(requestText, isInitialTurn: true);
	}

	[RelayCommand(CanExecute = nameof(CanSendFollowUp))]
	private Task SendFollowUpAsync() => ExecuteTurnAsync(FollowUpText, isInitialTurn: false);

	[RelayCommand(CanExecute = nameof(CanStartNewConversation))]
	private async Task StartNewConversationAsync()
	{
		_pendingUserInputSource?.TrySetCanceled();
		_pendingUserInputSource = null;
		await DisposeRunnerAsync();
		await _onNewConversationRequested();
	}

	private void StopAgent()
	{
		if (_agentRunner?.StopCurrentRun() != true)
		{
			return;
		}

		_ = _onStatusChanged("Stopping the current agent run...");
		LogCommand("Stop requested by user.", false);
		NotifyCommandStates();
	}

	[RelayCommand]
	private void SubmitPendingAnswer()
	{
		if (!IsAwaitingUserInput || string.IsNullOrWhiteSpace(PendingAnswer))
		{
			return;
		}

		var answer = PendingAnswer.Trim();
		Conversation.Add(new ConversationEntry(DateTimeOffset.Now, "User", answer));
		_pendingUserInputSource?.TrySetResult(answer);
		_pendingUserInputSource = null;
		PendingAnswer = string.Empty;
		PendingQuestion = string.Empty;
		IsAwaitingUserInput = false;
		_ = _onStatusChanged("Answer submitted. Waiting for the agent...");
		SubmitPendingAnswerCommand.NotifyCanExecuteChanged();
	}

	private async Task ExecuteTurnAsync(string text, bool isInitialTurn)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		IsBusy = true;
		await _onStatusChanged("Running agent...");
		try
		{
			OnPropertyChanged(nameof(CanStopAgent));
			StopAgentCommand.NotifyCanExecuteChanged();

			Conversation.Add(new ConversationEntry(DateTimeOffset.Now, "User", text.Trim()));
			var result = await _agentRunner!.RunTurnAsync(text.Trim(), CancellationToken.None);
			if (!string.IsNullOrWhiteSpace(result.Message))
			{
				Conversation.Add(new ConversationEntry(DateTimeOffset.Now, "Agent", result.Message));
			}

			_hasConversation = true;
			if (isInitialTurn)
			{
				// initial request text is owned by PreRunConversationViewModel; nothing to clear here
			}
			else
			{
				FollowUpText = string.Empty;
			}

			await _onStatusChanged(result.WasStopped
				? "Agent run stopped. You can send a follow-up or start a new conversation."
				: result.IsCompleted
					? "Task completed. You can add suggestions or start a new conversation."
					: "Waiting for your next message.");
		}
		catch (Exception ex)
		{
			await _onStatusChanged(ex.Message);
			LogCommand(ex.Message, true);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void LogCommand(string message, bool isError)
	{
		var role = isError ? "Agent error" : "Agent activity";
		_ = MainThreadDispatcher.InvokeAsync(() => Conversation.Add(new ConversationEntry(DateTimeOffset.Now, role, message)));
	}

	private async Task<string> PromptUserAsync(string question, CancellationToken cancellationToken)
	{
		var completionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		_pendingUserInputSource = completionSource;
		var registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
		_ = completionSource.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);

		await MainThreadDispatcher.InvokeAsync(() =>
		{
			Conversation.Add(new ConversationEntry(DateTimeOffset.Now, "Agent question", question));
			PendingQuestion = question;
			PendingAnswer = string.Empty;
			IsAwaitingUserInput = true;
			_ = _onStatusChanged("Agent is waiting for your answer.");
			SubmitPendingAnswerCommand.NotifyCanExecuteChanged();
		});

		return await completionSource.Task;
	}

	private async Task DisposeRunnerAsync()
	{
		if (_agentRunner is not null)
		{
			OnPropertyChanged(nameof(CanStopAgent));
			await _agentRunner.DisposeAsync();
			_agentRunner = null;
			OnPropertyChanged(nameof(CanStopAgent));
		}
	}

	private void NotifyCommandStates()
	{
		SendFollowUpCommand.NotifyCanExecuteChanged();
		StopAgentCommand.NotifyCanExecuteChanged();
		StartNewConversationCommand.NotifyCanExecuteChanged();
		SubmitPendingAnswerCommand.NotifyCanExecuteChanged();
	}

	public async ValueTask DisposeAsync()
	{
		_pendingUserInputSource?.TrySetCanceled();
		await DisposeRunnerAsync();
	}
}
