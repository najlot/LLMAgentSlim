namespace LLMAgentSlimGUI.Models;

public sealed record AgentCommandEntry(System.DateTimeOffset Timestamp, string Message, bool IsError);
