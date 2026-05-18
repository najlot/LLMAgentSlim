namespace LLMAgentSlim;

internal sealed class WorkspacePathResolver
{
	private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	public WorkspacePathResolver(string workspaceRoot)
	{
		WorkspaceRoot = Path.GetFullPath(workspaceRoot);
	}

	public string WorkspaceRoot { get; }

	public string ResolvePath(string path)
	{
		var candidatePath = string.IsNullOrWhiteSpace(path)
			? WorkspaceRoot
			: Path.IsPathRooted(path)
				? path
				: Path.Combine(WorkspaceRoot, path);

		var fullPath = Path.GetFullPath(candidatePath);
		EnsureWithinWorkspace(path, fullPath);

		return fullPath;
	}

	public string ToRelativePath(string fullPath)
	{
		EnsureWithinWorkspace(fullPath, fullPath);

		var relativePath = Path.GetRelativePath(WorkspaceRoot, fullPath);
		if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
		{
			return ".";
		}

		return relativePath.Replace(Path.DirectorySeparatorChar, '/');
	}

	private void EnsureWithinWorkspace(string requestedPath, string fullPath)
	{
		if (string.Equals(fullPath, WorkspaceRoot, PathComparison))
		{
			return;
		}

		var rootWithSeparator = Path.EndsInDirectorySeparator(WorkspaceRoot)
			? WorkspaceRoot
			: WorkspaceRoot + Path.DirectorySeparatorChar;

		if (!fullPath.StartsWith(rootWithSeparator, PathComparison))
		{
			throw new InvalidOperationException($"Path '{requestedPath}' is outside the workspace.");
		}
	}
}