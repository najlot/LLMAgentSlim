using Avalonia.Controls;
using LLMAgentSlimGUI.ViewModels;
using System;

namespace LLMAgentSlimGUI.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		Opened += OnOpened;
	}

	private async void OnOpened(object? sender, EventArgs e)
	{
		Opened -= OnOpened;
		if (DataContext is MainWindowViewModel viewModel)
		{
			await viewModel.InitializeAsync(this).ConfigureAwait(false);
		}
	}
}