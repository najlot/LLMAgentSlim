using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

internal class FileSystemPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
{
	[KernelFunction("read_file")]
	[Description("Reads a text file from the workspace and returns its content.")]
	public string ReadFile(string path) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath))
			{
				return $"File '{path}' not found.";
			}

			return PluginOutputFormatter.Limit(File.ReadAllText(fullPath));
		});

	[KernelFunction("read_file_range")]
	[Description("Reads an inclusive range of lines from a text file in the workspace and returns the lines with line numbers.")]
	public string ReadFileRange(string path, int startLine, int endLine) =>
		Execute(() =>
		{
			if (startLine <= 0)
			{
				return "startLine must be greater than 0.";
			}

			if (endLine < startLine)
			{
				return "endLine must be greater than or equal to startLine.";
			}

			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath))
			{
				return $"File '{path}' not found.";
			}

			var lines = File.ReadAllLines(fullPath);
			if (lines.Length == 0)
			{
				return $"File '{path}' is empty.";
			}

			if (startLine > lines.Length)
			{
				return $"startLine {startLine} is outside the file. The file has {lines.Length} lines.";
			}

			var actualEndLine = Math.Min(endLine, lines.Length);
			var selectedLines = lines
				.Skip(startLine - 1)
				.Take(actualEndLine - startLine + 1)
				.Select((line, index) => $"{startLine + index}: {line}");

			return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, selectedLines));
		});

	[KernelFunction("write_file")]
	[Description("Creates or overwrites a text file in the workspace with the provided content.")]
	public string WriteFile(string path, string content) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			var directoryPath = Path.GetDirectoryName(fullPath);

			if (!string.IsNullOrWhiteSpace(directoryPath))
			{
				Directory.CreateDirectory(directoryPath);
			}

			File.WriteAllText(fullPath, content);
			return $"Wrote {content.Length} characters to '{PathResolver.ToRelativePath(fullPath)}'.";
		});

	[KernelFunction("append_file")]
	[Description("Appends text to an existing file or creates it if it does not exist.")]
	public string AppendFile(string path, string content) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			var directoryPath = Path.GetDirectoryName(fullPath);

			if (!string.IsNullOrWhiteSpace(directoryPath))
			{
				Directory.CreateDirectory(directoryPath);
			}

			File.AppendAllText(fullPath, content);
			return $"Appended {content.Length} characters to '{PathResolver.ToRelativePath(fullPath)}'.";
		});

	[KernelFunction("patch_file")]
	[Description("Patches a text file by replacing an exact snippet. The file is only modified when the number of matches equals expectedOccurrences.")]
	public string PatchFile(string path, string oldText, string newText, int expectedOccurrences = 1) =>
		Execute(() =>
		{
			if (expectedOccurrences <= 0)
			{
				return "expectedOccurrences must be greater than 0.";
			}

			if (string.IsNullOrEmpty(oldText))
			{
				return "oldText cannot be empty.";
			}

			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath))
			{
				return $"File '{path}' not found.";
			}

			var currentContent = File.ReadAllText(fullPath);
			var occurrences = CountOccurrences(currentContent, oldText);
			if (occurrences == 0)
			{
				return $"Snippet not found in '{PathResolver.ToRelativePath(fullPath)}'.";
			}

			if (occurrences != expectedOccurrences)
			{
				return $"Expected {expectedOccurrences} occurrence(s) of the snippet in '{PathResolver.ToRelativePath(fullPath)}', but found {occurrences}. File not modified.";
			}

			var updatedContent = currentContent.Replace(oldText, newText, StringComparison.Ordinal);
			if (updatedContent == currentContent)
			{
				return $"No changes applied to '{PathResolver.ToRelativePath(fullPath)}'.";
			}

			File.WriteAllText(fullPath, updatedContent);
			return $"Patched '{PathResolver.ToRelativePath(fullPath)}' by replacing {occurrences} occurrence(s).";
		});

	[KernelFunction("delete_file")]
	[Description("Deletes a file from the workspace.")]
	public string DeleteFile(string path) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath))
			{
				return $"File '{path}' not found.";
			}

			File.Delete(fullPath);
			return $"Deleted '{PathResolver.ToRelativePath(fullPath)}'.";
		});

	private static int CountOccurrences(string content, string value)
	{
		var count = 0;
		var startIndex = 0;

		while (true)
		{
			var index = content.IndexOf(value, startIndex, StringComparison.Ordinal);
			if (index < 0)
			{
				return count;
			}

			count++;
			startIndex = index + value.Length;
		}
	}
}