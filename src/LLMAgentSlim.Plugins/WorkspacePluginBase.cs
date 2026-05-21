namespace LLMAgentSlim;

public abstract class WorkspacePluginBase
{
	protected WorkspacePluginBase(string workspaceRoot)
	{
		PathResolver = new WorkspacePathResolver(workspaceRoot);
	}

	protected WorkspacePathResolver PathResolver { get; }

	protected string Execute(Func<string> action)
	{
		try
		{
			return action();
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
	}

	protected async Task<string> ExecuteAsync(Func<Task<string>> action)
	{
		try
		{
			return await action().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
	}
}