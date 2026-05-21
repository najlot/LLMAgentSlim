using System.Diagnostics;
using System.Text;

namespace LLMAgentSlim;

internal sealed record CommandExecutionResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

internal static class CommandRunner
{
	public static Task<CommandExecutionResult> RunProcessAsync(
		string fileName,
		IEnumerable<string> arguments,
		string workingDirectory,
		int timeoutSeconds,
		CancellationToken cancellationToken = default)
	{
		var startInfo = CreateBaseStartInfo(fileName, workingDirectory);

		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		return RunAsync(startInfo, timeoutSeconds, cancellationToken);
	}

	public static Task<CommandExecutionResult> RunShellAsync(
		string command,
		string workingDirectory,
		int timeoutSeconds,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command))
		{
			throw new ArgumentException("Command cannot be empty.", nameof(command));
		}

		var startInfo = OperatingSystem.IsWindows()
			? CreateBaseStartInfo("cmd.exe", workingDirectory)
			: CreateBaseStartInfo("/bin/bash", workingDirectory);

		if (OperatingSystem.IsWindows())
		{
			startInfo.ArgumentList.Add("/c");
		}
		else
		{
			startInfo.ArgumentList.Add("-lc");
		}

		startInfo.ArgumentList.Add(command);

		return RunAsync(startInfo, timeoutSeconds, cancellationToken);
	}

	public static string FormatResult(string workingDirectory, CommandExecutionResult result)
	{
		var builder = new StringBuilder();

		builder.AppendLine($"Working directory: {workingDirectory}");
		if (result.TimedOut)
		{
			builder.AppendLine("Command timed out.");
		}

		builder.AppendLine($"Exit code: {result.ExitCode}");

		if (!string.IsNullOrWhiteSpace(result.StandardOutput))
		{
			builder.AppendLine("STDOUT:");
			builder.AppendLine(result.StandardOutput.TrimEnd());
		}

		if (!string.IsNullOrWhiteSpace(result.StandardError))
		{
			builder.AppendLine("STDERR:");
			builder.AppendLine(result.StandardError.TrimEnd());
		}

		return PluginOutputFormatter.Limit(builder.ToString().TrimEnd());
	}

	private static async Task<CommandExecutionResult> RunAsync(
		ProcessStartInfo startInfo,
		int timeoutSeconds,
		CancellationToken cancellationToken)
	{
		if (timeoutSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout must be greater than zero.");
		}

		using var process = new Process { StartInfo = startInfo };
		process.Start();

		var standardOutputTask = process.StandardOutput.ReadToEndAsync();
		var standardErrorTask = process.StandardError.ReadToEndAsync();

		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
		{
			TryKill(process);
			await process.WaitForExitAsync().ConfigureAwait(false);

			return new CommandExecutionResult(
				-1,
				await standardOutputTask.ConfigureAwait(false),
				await standardErrorTask.ConfigureAwait(false),
				true);
		}

		return new CommandExecutionResult(
			process.ExitCode,
			await standardOutputTask.ConfigureAwait(false),
			await standardErrorTask.ConfigureAwait(false),
			false);
	}

	private static ProcessStartInfo CreateBaseStartInfo(string fileName, string workingDirectory) =>
		new()
		{
			FileName = fileName,
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

	private static void TryKill(Process process)
	{
		if (!process.HasExited)
		{
			process.Kill(entireProcessTree: true);
		}
	}
}