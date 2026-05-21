using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

public class FileSystemPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
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

	[KernelFunction("get_path_info")]
	[Description("Returns basic information about a file or directory in the workspace, including size and timestamps when available.")]
	public string GetPathInfo(string path) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			if (File.Exists(fullPath))
			{
				var fileInfo = new FileInfo(fullPath);
				return PluginOutputFormatter.Limit(string.Join(Environment.NewLine,
				[
					$"Path: {PathResolver.ToRelativePath(fullPath)}",
					"Type: File",
					$"Size: {fileInfo.Length} bytes",
					$"Created: {fileInfo.CreationTimeUtc:O}",
					$"Modified: {fileInfo.LastWriteTimeUtc:O}",
					$"ReadOnly: {fileInfo.IsReadOnly}"
				]));
			}

			if (Directory.Exists(fullPath))
			{
				var directoryInfo = new DirectoryInfo(fullPath);
				var fileCount = Directory.EnumerateFiles(fullPath).Count();
				var directoryCount = Directory.EnumerateDirectories(fullPath).Count();
				return PluginOutputFormatter.Limit(string.Join(Environment.NewLine,
				[
					$"Path: {PathResolver.ToRelativePath(fullPath)}",
					"Type: Directory",
					$"Directories: {directoryCount}",
					$"Files: {fileCount}",
					$"Created: {directoryInfo.CreationTimeUtc:O}",
					$"Modified: {directoryInfo.LastWriteTimeUtc:O}"
				]));
			}

			return $"Path '{path}' not found.";
		});

	[KernelFunction("copy_file")]
	[Description("Copies a file to another location inside the workspace.")]
	public string CopyFile(string sourcePath, string destinationPath, bool overwrite = false) =>
		Execute(() =>
		{
			var fullSourcePath = PathResolver.ResolvePath(sourcePath);
			if (!File.Exists(fullSourcePath))
			{
				return $"File '{sourcePath}' not found.";
			}

			var fullDestinationPath = PathResolver.ResolvePath(destinationPath);
			var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);

			if (!string.IsNullOrWhiteSpace(destinationDirectory))
			{
				Directory.CreateDirectory(destinationDirectory);
			}

			File.Copy(fullSourcePath, fullDestinationPath, overwrite);
			return $"Copied '{PathResolver.ToRelativePath(fullSourcePath)}' to '{PathResolver.ToRelativePath(fullDestinationPath)}'.";
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

	[KernelFunction("move_file")]
	[Description("Moves or renames a file in the workspace.")]
	public string MoveFile(string sourcePath, string destinationPath, bool overwrite = false) =>
		Execute(() =>
		{
			var fullSourcePath = PathResolver.ResolvePath(sourcePath);
			if (!File.Exists(fullSourcePath))
			{
				return $"File '{sourcePath}' not found.";
			}

			var fullDestinationPath = PathResolver.ResolvePath(destinationPath);
			var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);

			if (!string.IsNullOrWhiteSpace(destinationDirectory))
			{
				Directory.CreateDirectory(destinationDirectory);
			}

			File.Move(fullSourcePath, fullDestinationPath, overwrite);
			return $"Moved '{PathResolver.ToRelativePath(fullSourcePath)}' to '{PathResolver.ToRelativePath(fullDestinationPath)}'.";
		});

	[KernelFunction("read_file_head")]
	[Description("Reads the first few lines of a text file in the workspace.")]
	public string ReadFileHead(string path, int linesToRead = 10) =>
		Execute(() =>
		{
			if (linesToRead <= 0) return "linesToRead must be greater than 0.";

			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath)) return $"File '{path}' not found.";

			var lines = File.ReadLines(fullPath).Take(linesToRead);
			return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, lines));
		});

	[KernelFunction("read_file_tail")]
	[Description("Reads the last few lines of a text file in the workspace.")]
	public string ReadFileTail(string path, int linesToRead = 10) =>
		Execute(() =>
		{
			if (linesToRead <= 0) return "linesToRead must be greater than 0.";

			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath)) return $"File '{path}' not found.";

			var lines = File.ReadAllLines(fullPath);
			var tailLines = lines.Skip(Math.Max(0, lines.Length - linesToRead));
			return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, tailLines));
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