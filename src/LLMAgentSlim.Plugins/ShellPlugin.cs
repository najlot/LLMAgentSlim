using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

public class ShellPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
{
	[KernelFunction("run_command")]
	[Description("Runs a non-interactive shell command inside the workspace and returns the exit code, stdout, and stderr.")]
	public Task<string> RunCommand(string command, string path = ".", int timeoutSeconds = 60) =>
		ExecuteAsync(async () =>
		{
			var workingDirectory = ResolveWorkingDirectory(path);
			var result = await CommandRunner.RunShellAsync(
				command,
				workingDirectory,
				Math.Clamp(timeoutSeconds, 1, 600)).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	private string ResolveWorkingDirectory(string path)
	{
		var fullPath = PathResolver.ResolvePath(path);
		if (File.Exists(fullPath))
		{
			return Path.GetDirectoryName(fullPath) ?? PathResolver.WorkspaceRoot;
		}

		if (!Directory.Exists(fullPath))
		{
			throw new DirectoryNotFoundException($"Directory '{path}' not found.");
		}

		return fullPath;
	}
}