namespace LLMAgentSlim;

internal static class LLMAgentSlimPaths
{
	public const string ConfigurationFileName = "llmagentslim.json";
	public const string AppDataDirectoryName = ".llmagentslim";

	public static string GetAppDataDirectory()
	{
		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		if (string.IsNullOrWhiteSpace(appDataPath))
		{
			appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		}

		if (string.IsNullOrWhiteSpace(appDataPath))
		{
			throw new InvalidOperationException("Could not determine the application data directory.");
		}

		return Path.Combine(appDataPath, AppDataDirectoryName);
	}
}