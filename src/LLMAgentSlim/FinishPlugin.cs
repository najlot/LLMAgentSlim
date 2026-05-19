using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

internal class FinishPlugin(Func<CancellationTokenSource> cancellationTokenSourceSelector)
{
	[KernelFunction("task_finished")]
	[Description("Marks the task as finished.")]
	public void TaskFinished(string userMessage)
	{
		Console.WriteLine(userMessage);
		cancellationTokenSourceSelector().Cancel();
	}
}
