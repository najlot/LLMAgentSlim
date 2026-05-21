using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LLMAgentSlimGUI.Services;

internal sealed class GuiUserInputPlugin(Func<string, CancellationToken, Task<string>> promptHandler)
{
	[KernelFunction("ask_user")]
	[Description("Asks the user a question and returns their answer. Use this whenever a decision or clarification is needed before proceeding.")]
	public Task<string> AskUser(
		[Description("The question or prompt to present to the user.")] string question,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			return Task.FromResult("Question cannot be empty.");
		}

		return promptHandler(question, cancellationToken);
	}

	[KernelFunction("ask_user_choice")]
	[Description("Presents the user with a numbered list of options and returns the chosen option text. Use this when the agent needs the user to pick one of several alternatives.")]
	public async Task<string> AskUserChoice(
		[Description("The question or prompt to present to the user.")] string question,
		[Description("Comma-separated list of options to choose from.")] string options,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			return "Question cannot be empty.";
		}

		if (string.IsNullOrWhiteSpace(options))
		{
			return "Options cannot be empty.";
		}

		var optionList = options
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();

		if (optionList.Count == 0)
		{
			return "Options cannot be empty.";
		}

		var prompt = $"{question}{Environment.NewLine}{string.Join(Environment.NewLine, optionList.Select((option, index) => $"{index + 1}. {option}"))}{Environment.NewLine}Reply with the option text.";
		var answer = (await promptHandler(prompt, cancellationToken).ConfigureAwait(false)).Trim();
		return optionList.FirstOrDefault(option => string.Equals(option, answer, StringComparison.OrdinalIgnoreCase))
			?? answer;
	}

	[KernelFunction("ask_user_confirmation")]
	[Description("Asks the user a yes/no confirmation question. Returns 'yes' or 'no'.")]
	public async Task<string> AskUserConfirmation(
		[Description("The yes/no question to ask the user.")] string question,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			return "Question cannot be empty.";
		}

		var answer = (await promptHandler($"{question} (yes/no)", cancellationToken).ConfigureAwait(false)).Trim();
		return answer.Equals("y", StringComparison.OrdinalIgnoreCase) ? "yes"
			: answer.Equals("n", StringComparison.OrdinalIgnoreCase) ? "no"
			: answer;
	}
}
