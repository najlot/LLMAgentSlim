using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

public class DotNetPlugin(string workspaceRoot) : WorkspacePluginBase(workspaceRoot)
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

	[KernelFunction("dotnet_restore")]
	[Description("Runs dotnet restore for a .NET project, solution, or directory inside the workspace.")]
	public Task<string> RestoreProject(string path = ".") =>
		ExecuteAsync(async () =>
		{
			var (targetPath, workingDirectory) = ResolveTarget(path);
			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				["restore", targetPath, "--nologo", "-v", "minimal"],
				workingDirectory,
				180).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("dotnet_clean")]
	[Description("Runs dotnet clean for a .NET project, solution, or directory inside the workspace.")]
	public Task<string> CleanProject(string path = ".", string configuration = "Debug") =>
		ExecuteAsync(async () =>
		{
			var (targetPath, workingDirectory) = ResolveTarget(path);
			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				["clean", targetPath, "--nologo", "-v", "minimal", "-c", configuration],
				workingDirectory,
				180).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("dotnet_list_packages")]
	[Description("Lists installed NuGet packages for a project or solution.")]
	public Task<string> ListPackages(string path = ".") =>
		ExecuteAsync(async () =>
		{
			var (targetPath, workingDirectory) = ResolveTarget(path);
			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				["list", targetPath, "package"],
				workingDirectory,
				180).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("dotnet_add_package")]
	[Description("Installs a NuGet package to a .NET project. Optionally specify a version; omit to get the latest.")]
	public Task<string> AddPackage(string path, string packageName, string version = "") =>
		ExecuteAsync(async () =>
		{
			if (string.IsNullOrWhiteSpace(packageName))
			{
				return "Package name cannot be empty.";
			}

			var (targetPath, workingDirectory) = ResolveTarget(path);
			var arguments = new List<string> { "add", targetPath, "package", packageName };

			if (!string.IsNullOrWhiteSpace(version))
			{
				arguments.Add("--version");
				arguments.Add(version.Trim());
			}

			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				arguments,
				workingDirectory,
				120).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("dotnet_update_package")]
	[Description("Updates a NuGet package in a .NET project to the latest version or to a specific version.")]
	public Task<string> UpdatePackage(string path, string packageName, string version = "") =>
		ExecuteAsync(async () =>
		{
			if (string.IsNullOrWhiteSpace(packageName))
			{
				return "Package name cannot be empty.";
			}

			var (targetPath, workingDirectory) = ResolveTarget(path);
			var arguments = new List<string> { "add", targetPath, "package", packageName };

			if (!string.IsNullOrWhiteSpace(version))
			{
				arguments.Add("--version");
				arguments.Add(version.Trim());
			}

			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				arguments,
				workingDirectory,
				120).ConfigureAwait(false);

			return CommandRunner.FormatResult(PathResolver.ToRelativePath(workingDirectory), result);
		});

	[KernelFunction("dotnet_remove_package")]
	[Description("Removes a NuGet package from a .NET project.")]
	public Task<string> RemovePackage(string path, string packageName) =>
		ExecuteAsync(async () =>
		{
			if (string.IsNullOrWhiteSpace(packageName))
			{
				return "Package name cannot be empty.";
			}

			var (targetPath, workingDirectory) = ResolveTarget(path);
			var result = await CommandRunner.RunProcessAsync(
				"dotnet",
				["remove", targetPath, "package", packageName],
				workingDirectory,
				120).ConfigureAwait(false);

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