namespace LLMAgentSlim;

internal sealed class LLMAgentSlimConfiguration
{
	public string Provider { get; init; } = "ollama";

	public ProviderConfiguration Providers { get; init; } = new();
}

internal sealed class ProviderConfiguration
{
	public OllamaProviderConfiguration Ollama { get; init; } = new();
}

internal sealed class OllamaProviderConfiguration
{
	public string Endpoint { get; init; } = "http://localhost:11434/";

	public string Model { get; init; } = "qwen3-coder:30b";

	public int TimeoutMinutes { get; init; } = 10;

	public int TopK { get; init; } = 10;

	public float TopP { get; init; } = 0.5f;

	public float Temperature { get; init; } = 0.1f;
}