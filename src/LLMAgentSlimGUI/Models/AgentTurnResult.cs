namespace LLMAgentSlimGUI.Models;

internal sealed record AgentTurnResult(string Message, bool IsCompleted, bool WasStopped);
