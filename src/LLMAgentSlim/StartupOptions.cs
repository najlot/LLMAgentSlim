namespace LLMAgentSlim;

internal sealed record StartupOptions(string? ConfigurationPath, string? WorkingDirectory)
{
	public static StartupOptions Parse(string[] arguments)
	{
		string? configurationPath = null;
		string? workingDirectory = null;

		for (var index = 0; index < arguments.Length; index++)
		{
			var argument = arguments[index];

			if (TryMatchOption(argument, "-c", "--config", out var inlineValue))
			{
				configurationPath = AssignOptionValue(
					configurationPath,
					inlineValue ?? ReadNextValue(arguments, ref index, argument),
					"-c/--config");
				continue;
			}

			if (TryMatchOption(argument, "-p", "--path", out inlineValue))
			{
				workingDirectory = AssignOptionValue(
					workingDirectory,
					inlineValue ?? ReadNextValue(arguments, ref index, argument),
					"-p/--path");
				continue;
			}

			throw new ArgumentException(
				$"Unknown argument '{argument}'. Supported arguments: -c|--config <file> and -p|--path <directory>.");
		}

		return new StartupOptions(configurationPath, workingDirectory);
	}

	private static string AssignOptionValue(string? existingValue, string value, string optionName)
	{
		if (existingValue is not null)
		{
			throw new ArgumentException($"The {optionName} option can only be specified once.");
		}

		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException($"The {optionName} option requires a non-empty value.");
		}

		return value;
	}

	private static string ReadNextValue(string[] arguments, ref int index, string optionName)
	{
		if (index + 1 >= arguments.Length || IsSupportedOptionToken(arguments[index + 1]))
		{
			throw new ArgumentException($"Missing value for '{optionName}'.");
		}

		index++;
		return arguments[index];
	}

	private static bool TryMatchOption(string argument, string shortName, string longName, out string? inlineValue)
	{
		if (string.Equals(argument, shortName, StringComparison.Ordinal)
			|| string.Equals(argument, longName, StringComparison.Ordinal))
		{
			inlineValue = null;
			return true;
		}

		if (argument.StartsWith(shortName + "=", StringComparison.Ordinal))
		{
			inlineValue = argument[(shortName.Length + 1)..];
			return true;
		}

		if (argument.StartsWith(longName + "=", StringComparison.Ordinal))
		{
			inlineValue = argument[(longName.Length + 1)..];
			return true;
		}

		inlineValue = null;
		return false;
	}

	private static bool IsSupportedOptionToken(string argument) =>
		string.Equals(argument, "-c", StringComparison.Ordinal)
		|| string.Equals(argument, "--config", StringComparison.Ordinal)
		|| string.Equals(argument, "-p", StringComparison.Ordinal)
		|| string.Equals(argument, "--path", StringComparison.Ordinal)
		|| argument.StartsWith("-c=", StringComparison.Ordinal)
		|| argument.StartsWith("--config=", StringComparison.Ordinal)
		|| argument.StartsWith("-p=", StringComparison.Ordinal)
		|| argument.StartsWith("--path=", StringComparison.Ordinal);
}