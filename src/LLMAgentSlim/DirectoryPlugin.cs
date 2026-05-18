using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

internal class DirectoryPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
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
}