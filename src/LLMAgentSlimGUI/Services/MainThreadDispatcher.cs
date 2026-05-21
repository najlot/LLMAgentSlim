using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace LLMAgentSlimGUI.Services;

internal static class MainThreadDispatcher
{
	public static async Task InvokeAsync(Action action)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			action();
			return;
		}

		await Dispatcher.UIThread.InvokeAsync(action);
	}
}
