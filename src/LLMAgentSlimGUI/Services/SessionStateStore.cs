using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LLMAgentSlimGUI.Models;

namespace LLMAgentSlimGUI.Services;

internal sealed class SessionStateStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		WriteIndented = true
	};

	public async Task<AppSessionState> LoadAsync(CancellationToken cancellationToken)
	{
		var sessionStatePath = GuiPaths.GetSessionStatePath();
		if (!File.Exists(sessionStatePath))
		{
			return new AppSessionState();
		}

		await using var stream = File.OpenRead(sessionStatePath);
		return await JsonSerializer.DeserializeAsync<AppSessionState>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
			?? new AppSessionState();
	}

	public async Task SaveAsync(AppSessionState state, CancellationToken cancellationToken)
	{
		var sessionStatePath = GuiPaths.GetSessionStatePath();
		Directory.CreateDirectory(Path.GetDirectoryName(sessionStatePath)!);

		await using var stream = File.Create(sessionStatePath);
		await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken).ConfigureAwait(false);
	}
}
