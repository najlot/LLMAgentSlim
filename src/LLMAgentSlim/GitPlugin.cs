using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

internal class GitPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
{
	[KernelFunction("git_status")]
	[Description("Runs 'git status --short --branch' in the workspace or a subdirectory.")]
	public Task<string> GetStatus(string path = ".") =>
		ExecuteAsync(async () =>
		{
			var workingDirectory = ResolveWorkingDirectory(path);
			var result = await CommandRunner.RunProcessAsync(
				"git",
				["status", "--short", "--branch"],
				workingDirectory,
				30).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("git_diff")]
	[Description("Runs 'git diff --stat' in the workspace or a subdirectory and returns a summary.")]
	public Task<string> GetDiff(string path = ".") =>
		ExecuteAsync(async () =>
		{
			var workingDirectory = ResolveWorkingDirectory(path);
			var result = await CommandRunner.RunProcessAsync(
				"git",
				["--no-pager", "diff", "--stat"],
				workingDirectory,
				30).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("git_log")]
	[Description("Returns recent commits using 'git log --oneline'.")]
	public Task<string> GetLog(string path = ".", int count = 5) =>
		ExecuteAsync(async () =>
		{
			var workingDirectory = ResolveWorkingDirectory(path);
			var result = await CommandRunner.RunProcessAsync(
				"git",
				["log", "--oneline", "-n", Math.Clamp(count, 1, 20).ToString()],
				workingDirectory,
				30).ConfigureAwait(false);

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