using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace LLMAgentSlim;

internal class SearchPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
{
	private static readonly HashSet<string> IgnoredDirectories =
	[
		".git",
		".vs",
		"bin",
		"obj"
	];

	private static readonly HashSet<string> IgnoredExtensions =
	[
		".dll",
		".exe",
		".pdb",
		".png",
		".jpg",
		".jpeg",
		".gif",
		".webp",
		".zip",
		".gz",
		".tar",
		".7z"
	];

	[KernelFunction("find_files")]
	[Description("Finds files under the workspace using a glob pattern such as '*.cs' or 'src/**/*.cs'.")]
	public string FindFiles(string pattern, string path = ".") =>
		Execute(() =>
		{
			if (string.IsNullOrWhiteSpace(pattern))
			{
				return "Pattern cannot be empty.";
			}

			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
			{
				return $"Path '{path}' not found.";
			}

			var normalizedPattern = pattern.Replace('\\', '/');
			var matchFileNameOnly = !normalizedPattern.Contains('/');
			var regex = CreateGlobRegex(normalizedPattern);

			var matches = EnumerateFiles(fullPath)
				.Select(filePath => PathResolver.ToRelativePath(filePath))
				.Where(relativePath => regex.IsMatch(matchFileNameOnly ? Path.GetFileName(relativePath) : relativePath))
				.Take(200)
				.ToList();

			if (matches.Count == 0)
			{
				return $"No files found for pattern '{pattern}'.";
			}

			return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, matches), 12000, 220);
		});

	[KernelFunction("search_text")]
	[Description("Searches plain text in workspace files and returns matching paths, line numbers, and line snippets.")]
	public string SearchText(string text, string path = ".", int maxResults = 20) =>
		Execute(() =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return "Search text cannot be empty.";
			}

			var fullPath = PathResolver.ResolvePath(path);
			if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
			{
				return $"Path '{path}' not found.";
			}

			var results = new List<string>();
			foreach (var filePath in EnumerateFiles(fullPath))
			{
				if (ShouldSkipFile(filePath))
				{
					continue;
				}

				var lineNumber = 0;

				try
				{
					foreach (var line in File.ReadLines(filePath))
					{
						lineNumber++;
						if (!line.Contains(text, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						results.Add($"{PathResolver.ToRelativePath(filePath)}:{lineNumber}: {line.Trim()}");
						if (results.Count >= Math.Clamp(maxResults, 1, 100))
						{
							return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, results), 12000, 120);
						}
					}
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
			}

			if (results.Count == 0)
			{
				return $"No matches found for '{text}'.";
			}

			return PluginOutputFormatter.Limit(string.Join(Environment.NewLine, results), 12000, 120);
		});

	private IEnumerable<string> EnumerateFiles(string path)
	{
		if (File.Exists(path))
		{
			yield return path;
			yield break;
		}

		var pendingDirectories = new Stack<string>();
		pendingDirectories.Push(path);

		while (pendingDirectories.Count > 0)
		{
			var currentDirectory = pendingDirectories.Pop();

			IEnumerable<string> subDirectories;
			try
			{
				subDirectories = Directory.EnumerateDirectories(currentDirectory);
			}
			catch (IOException)
			{
				continue;
			}
			catch (UnauthorizedAccessException)
			{
				continue;
			}

			foreach (var subDirectory in subDirectories)
			{
				if (IgnoredDirectories.Contains(Path.GetFileName(subDirectory)))
				{
					continue;
				}

				pendingDirectories.Push(subDirectory);
			}

			IEnumerable<string> files;
			try
			{
				files = Directory.EnumerateFiles(currentDirectory);
			}
			catch (IOException)
			{
				continue;
			}
			catch (UnauthorizedAccessException)
			{
				continue;
			}

			foreach (var filePath in files)
			{
				yield return filePath;
			}
		}
	}

	private static Regex CreateGlobRegex(string pattern)
	{
		var builder = new StringBuilder("^");

		for (var index = 0; index < pattern.Length; index++)
		{
			var current = pattern[index];
			if (current == '*')
			{
				var isDoubleStar = index + 1 < pattern.Length && pattern[index + 1] == '*';
				if (isDoubleStar)
				{
					builder.Append(".*");
					index++;
				}
				else
				{
					builder.Append("[^/]*");
				}

				continue;
			}

			if (current == '?')
			{
				builder.Append("[^/]");
				continue;
			}

			builder.Append(Regex.Escape(current.ToString()));
		}

		builder.Append('$');

		return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static bool ShouldSkipFile(string filePath)
	{
		var fileInfo = new FileInfo(filePath);
		return fileInfo.Length > 1_000_000 || IgnoredExtensions.Contains(fileInfo.Extension);
	}
}