using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

public class DirectoryPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
{
	[KernelFunction("list_directory")]
	[Description("Lists files and folders inside a workspace directory.")]
	public string ListDirectory(string path = ".") =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			if (!Directory.Exists(fullPath))
			{
				return $"Directory '{path}' not found.";
			}

			var directories = Directory.EnumerateDirectories(fullPath)
				.Select(directoryPath => Path.GetFileName(directoryPath) + "/")
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

			var files = Directory.EnumerateFiles(fullPath)
				.Select(Path.GetFileName)
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

			var entries = directories.Concat(files).ToList();
			if (entries.Count == 0)
			{
				return $"Directory '{PathResolver.ToRelativePath(fullPath)}' is empty.";
			}

			return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, entries));
		});

	[KernelFunction("create_directory")]
	[Description("Creates a directory inside the workspace.")]
	public string CreateDirectory(string path) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			Directory.CreateDirectory(fullPath);
			return $"Created directory '{PathResolver.ToRelativePath(fullPath)}'.";
		});

	[KernelFunction("delete_directory")]
	[Description("Deletes a directory from the workspace. Can optionally delete recursively.")]
	public string DeleteDirectory(string path, bool recursive = false) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			if (!Directory.Exists(fullPath))
			{
				return $"Directory '{path}' not found.";
			}

			Directory.Delete(fullPath, recursive);
			return $"Deleted directory '{PathResolver.ToRelativePath(fullPath)}'.";
		});

	[KernelFunction("move_directory")]
	[Description("Moves or renames a directory in the workspace.")]
	public string MoveDirectory(string sourcePath, string destinationPath) =>
		Execute(() =>
		{
			var fullSourcePath = PathResolver.ResolvePath(sourcePath);
			if (!Directory.Exists(fullSourcePath))
			{
				return $"Directory '{sourcePath}' not found.";
			}

			var fullDestinationPath = PathResolver.ResolvePath(destinationPath);
			Directory.Move(fullSourcePath, fullDestinationPath);
			return $"Moved directory '{PathResolver.ToRelativePath(fullSourcePath)}' to '{PathResolver.ToRelativePath(fullDestinationPath)}'.";
		});

	[KernelFunction("tree_directory")]
	[Description("Lists a directory tree up to a limited depth for quick workspace exploration.")]
	public string TreeDirectory(string path = ".", int maxDepth = 2, int maxEntries = 200) =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			if (!Directory.Exists(fullPath))
			{
				return $"Directory '{path}' not found.";
			}

			var effectiveMaxDepth = Math.Clamp(maxDepth, 0, 6);
			var effectiveMaxEntries = Math.Clamp(maxEntries, 1, 500);
			var lines = new List<string> { PathResolver.ToRelativePath(fullPath) + "/" };
			var emittedEntries = 0;

			void Visit(string currentDirectory, int depth)
			{
				if (depth >= effectiveMaxDepth || emittedEntries >= effectiveMaxEntries)
				{
					return;
				}

				var directories = Directory.EnumerateDirectories(currentDirectory)
					.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase);
				var files = Directory.EnumerateFiles(currentDirectory)
					.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase);

				foreach (var directory in directories)
				{
					if (emittedEntries >= effectiveMaxEntries)
					{
						return;
					}

					lines.Add($"{new string(' ', (depth + 1) * 2)}{Path.GetFileName(directory)}/");
					emittedEntries++;
					Visit(directory, depth + 1);
				}

				foreach (var file in files)
				{
					if (emittedEntries >= effectiveMaxEntries)
					{
						return;
					}

					lines.Add($"{new string(' ', (depth + 1) * 2)}{Path.GetFileName(file)}");
					emittedEntries++;
				}
			}

			Visit(fullPath, 0);

			if (emittedEntries >= effectiveMaxEntries)
			{
				lines.Add("... output truncated.");
			}

			return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, lines), 12000, 400);
		});

	[KernelFunction("get_directory_size")]
	[Description("Calculates the total size in bytes of all files in a directory recursively.")]
	public string GetDirectorySize(string path = ".") =>
		Execute(() =>
		{
			var fullPath = PathResolver.ResolvePath(path);
			if (!Directory.Exists(fullPath))
			{
				return $"Directory '{path}' not found.";
			}

			long totalSize = 0;
			try
			{
				var files = Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
				foreach (var file in files)
				{
					totalSize += new FileInfo(file).Length;
				}
				return $"Total size of '{PathResolver.ToRelativePath(fullPath)}' (recursive): {totalSize} bytes.";
			}
			catch (Exception ex)
			{
				return $"Error calculating directory size: {ex.Message}";
			}
		});
}