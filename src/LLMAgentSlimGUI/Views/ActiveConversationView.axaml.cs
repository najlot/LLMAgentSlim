using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LLMAgentSlimGUI.Models;
using LLMAgentSlimGUI.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace LLMAgentSlimGUI.Views;

public partial class ActiveConversationView : UserControl
{
	private ListBox? _conversationList;
	private ScrollViewer? _conversationScrollViewer;
	private ActiveConversationViewModel? _viewModel;
	private bool _shouldAutoScroll = true;

	public ActiveConversationView()
	{
		InitializeComponent();
		AttachedToVisualTree += (_, _) => HandleAttachedToVisualTree();
		DetachedFromVisualTree += (_, _) => HandleDetachedFromVisualTree();
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		AttachViewModel(DataContext as ActiveConversationViewModel);
	}

	private void HandleAttachedToVisualTree()
	{
		_conversationList = this.FindControl<ListBox>("ConversationList");
		_conversationScrollViewer = _conversationList?
			.GetVisualDescendants()
			.OfType<ScrollViewer>()
			.FirstOrDefault();

		if (_conversationScrollViewer is not null)
		{
			_conversationScrollViewer.ScrollChanged += OnConversationScrollChanged;
			_shouldAutoScroll = IsScrolledToBottom(_conversationScrollViewer);
		}

		AttachViewModel(DataContext as ActiveConversationViewModel);
		QueueScrollToLatest(force: true);
	}

	private void HandleDetachedFromVisualTree()
	{
		if (_conversationScrollViewer is not null)
		{
			_conversationScrollViewer.ScrollChanged -= OnConversationScrollChanged;
			_conversationScrollViewer = null;
		}

		_conversationList = null;
		AttachViewModel(null);
	}

	private void AttachViewModel(ActiveConversationViewModel? viewModel)
	{
		if (ReferenceEquals(_viewModel, viewModel))
		{
			return;
		}

		if (_viewModel is not null)
		{
			_viewModel.Conversation.CollectionChanged -= OnConversationCollectionChanged;
			_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		}

		_viewModel = viewModel;

		if (_viewModel is not null)
		{
			_viewModel.Conversation.CollectionChanged += OnConversationCollectionChanged;
			_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		}
	}

	private void OnConversationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset)
		{
			QueueScrollToLatest(force: e.Action == NotifyCollectionChangedAction.Reset);
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ActiveConversationViewModel.HasPendingQuestion) && _shouldAutoScroll)
		{
			QueueScrollToLatest(force: true);
		}
	}

	private void OnConversationScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (_conversationScrollViewer is not null)
		{
			_shouldAutoScroll = IsScrolledToBottom(_conversationScrollViewer);
		}
	}

	private void QueueScrollToLatest(bool force)
	{
		if (!force && !_shouldAutoScroll)
		{
			return;
		}

		Dispatcher.UIThread.Post(ScrollToLatest, DispatcherPriority.Background);
	}

	private void ScrollToLatest()
	{
		if (_conversationList?.ItemsSource is not System.Collections.Generic.IEnumerable<ConversationEntry> entries)
		{
			return;
		}

		var lastEntry = entries.LastOrDefault();
		if (lastEntry is null)
		{
			return;
		}

		_conversationList.ScrollIntoView(lastEntry);
	}

	private async void CopyConversationEntry_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (sender is not Control { DataContext: ConversationEntry { CanCopy: true } entry })
		{
			return;
		}

		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel?.Clipboard is null)
		{
			return;
		}

		await topLevel.Clipboard.SetTextAsync(entry.Message);
	}

	private static bool IsScrolledToBottom(ScrollViewer scrollViewer)
	{
		const double tolerance = 1d;
		return scrollViewer.Extent.Height - (scrollViewer.Offset.Y + scrollViewer.Viewport.Height) <= tolerance;
	}
}
