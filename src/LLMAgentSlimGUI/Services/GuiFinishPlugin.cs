using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Threading;

namespace LLMAgentSlimGUI.Services;

internal sealed class GuiFinishPlugin(Func<CancellationTokenSource> cancellationTokenSourceSelector)
{
	[KernelFunction("task_finished")]
	[Description("Marks the task as finished.")]
	public void TaskFinished(string userMessage)
	{
		cancellationTokenSourceSelector().Cancel();
	}
}
