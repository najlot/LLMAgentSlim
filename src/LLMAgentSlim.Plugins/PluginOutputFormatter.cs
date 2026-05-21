using System.Text;

namespace LLMAgentSlim;

internal static class PluginOutputFormatter
{
	private const int DefaultMaxCharacters = 12000;
	private const int DefaultMaxLines = 200;

	public static string Limit(string value, int maxCharacters = DefaultMaxCharacters, int maxLines = DefaultMaxLines)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		using var reader = new StringReader(value);
		var builder = new StringBuilder();
		var lineCount = 0;
		var wasTruncated = false;

		while (lineCount < maxLines)
		{
			var line = reader.ReadLine();
			if (line is null)
			{
				break;
			}

			var nextLength = builder.Length + line.Length + Environment.NewLine.Length;
			if (nextLength > maxCharacters)
			{
				wasTruncated = true;
				break;
			}

			builder.AppendLine(line);
			lineCount++;
		}

		if (!wasTruncated && reader.ReadLine() is not null)
		{
			wasTruncated = true;
		}

		if (wasTruncated)
		{
			builder.AppendLine("... output truncated.");
		}

		return builder.ToString().TrimEnd();
	}
}