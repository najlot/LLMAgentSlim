using Microsoft.SemanticKernel;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace LLMAgentSlimGUI.Services;

internal sealed class GuiFunctionInvocationFilter(Action<string, bool> logAction) : IFunctionInvocationFilter
{
	private const int MaxValueLength = 120;

	public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
	{
		var functionName = GetFunctionDisplayName(context);
		logAction($"> {functionName}({FormatArguments(context.Arguments)})", false);

		try
		{
			await next(context).ConfigureAwait(false);
			logAction($"< {functionName}", false);
		}
		catch (Exception ex)
		{
			logAction($"! {functionName}: {ex.Message}", true);
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
