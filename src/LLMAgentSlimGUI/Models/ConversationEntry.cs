namespace LLMAgentSlimGUI.Models;

public sealed record ConversationEntry(System.DateTimeOffset Timestamp, string Role, string Message)
{
	public bool CanCopy => string.Equals(Role, "Agent", System.StringComparison.Ordinal);
}
