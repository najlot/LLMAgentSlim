using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

internal class UserInputPlugin
{
    [KernelFunction("ask_user")]
    [Description("Asks the user a question and returns their answer. Use this whenever a decision or clarification is needed before proceeding.")]
    public string AskUser(
        [Description("The question or prompt to present to the user.")] string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Question cannot be empty.";
        }

        Console.WriteLine();
        Console.WriteLine($"[Agent asks]: {question}");
        Console.Write(">>> ");

        var answer = Console.ReadLine();
        while (string.IsNullOrWhiteSpace(answer))
        {
            Console.WriteLine("Answer cannot be empty. Please provide a response:");
            Console.Write(">>> ");
            answer = Console.ReadLine();
        }

        return answer.Trim();
    }

    [KernelFunction("ask_user_choice")]
    [Description("Presents the user with a numbered list of options and returns the chosen option text. Use this when the agent needs the user to pick one of several alternatives.")]
    public string AskUserChoice(
        [Description("The question or prompt to present to the user.")] string question,
        [Description("Comma-separated list of options to choose from.")] string options)
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

        Console.WriteLine();
        Console.WriteLine($"[Agent asks]: {question}");
        for (var i = 0; i < optionList.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {optionList[i]}");
        }

        while (true)
        {
            Console.Write($">>> Enter choice (1-{optionList.Count}): ");
            var input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= optionList.Count)
            {
                return optionList[choice - 1];
            }

            Console.WriteLine($"Invalid choice. Please enter a number between 1 and {optionList.Count}.");
        }
    }

    [KernelFunction("ask_user_confirmation")]
    [Description("Asks the user a yes/no confirmation question. Returns 'yes' or 'no'.")]
    public string AskUserConfirmation(
        [Description("The yes/no question to ask the user.")] string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Question cannot be empty.";
        }

        Console.WriteLine();
        Console.WriteLine($"[Agent asks]: {question} (yes/no)");

        while (true)
        {
            Console.Write(">>> ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (input is "yes" or "y")
            {
                return "yes";
            }

            if (input is "no" or "n")
            {
                return "no";
            }

            Console.WriteLine("Please answer 'yes' or 'no'.");
        }
    }
}
