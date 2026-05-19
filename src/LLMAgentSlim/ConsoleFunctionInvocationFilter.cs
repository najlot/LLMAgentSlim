using Microsoft.SemanticKernel;
using System.Globalization;

namespace LLMAgentSlim;

internal sealed class ConsoleFunctionInvocationFilter : IFunctionInvocationFilter
{
    private const int MaxValueLength = 120;

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var functionName = GetFunctionDisplayName(context);
        Console.WriteLine($"> {functionName}({FormatArguments(context.Arguments)})");

        try
        {
            await next(context).ConfigureAwait(false);
            Console.WriteLine($"< {functionName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! {functionName}: {ex.Message}");
            throw;
        }
    }

    private static string GetFunctionDisplayName(FunctionInvocationContext context)
    {
        var pluginName = context.Function.PluginName;
        return string.IsNullOrWhiteSpace(pluginName)
            ? context.Function.Name
            : $"{pluginName}.{context.Function.Name}";
    }

    private static string FormatArguments(KernelArguments arguments)
    {
        if (arguments.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", arguments.Select(argument => $"{argument.Key}: {FormatValue(argument.Value)}"));
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return $"\"{Truncate(text.Replace(Environment.NewLine, "\\n"))}\"";
        }

        return Truncate(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxValueLength ? value : $"{value[..MaxValueLength]}...";
}
