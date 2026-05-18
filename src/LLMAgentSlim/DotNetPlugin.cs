using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

internal class DotNetPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
{
	[KernelFunction("dotnet_build")]
	[Description("Builds a .NET project, solution, or directory inside the workspace.")]
	public Task<string> BuildProject(string path = ".", string configuration = "Debug") =>
		ExecuteAsync(async () =>
		{
			var (targetPath, workingDirectory) = ResolveTarget(path);
			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				["build", targetPath, "--nologo", "-v", "minimal", "-c", configuration],
				workingDirectory,
				180).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("dotnet_test")]
	[Description("Runs dotnet test for a .NET project, solution, or directory inside the workspace.")]
	public Task<string> TestProject(string path = ".", string configuration = "Debug", string filter = "") =>
		ExecuteAsync(async () =>
		{
			var (targetPath, workingDirectory) = ResolveTarget(path);
			var arguments = new List<string>
			{
				"test",
				targetPath,
				"--nologo",
				"-v",
				"minimal",
				"-c",
				configuration
			};

			if (!string.IsNullOrWhiteSpace(filter))
			{
				arguments.Add("--filter");
				arguments.Add(filter);
			}

			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				arguments,
				workingDirectory,
				300).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	private (string TargetPath, string WorkingDirectory) ResolveTarget(string path)
	{
		var fullPath = PathResolver.ResolvePath(path);
		if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
		{
			throw new FileNotFoundException($"Path '{path}' not found.");
		}

		if (Directory.Exists(fullPath))
		{
			return (fullPath, fullPath);
		}

		return (fullPath, Path.GetDirectoryName(fullPath) ?? PathResolver.WorkspaceRoot);
	}
}