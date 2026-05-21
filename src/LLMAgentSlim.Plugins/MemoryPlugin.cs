using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace LLMAgentSlim;

public class MemoryPlugin
{
   private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true
	};

	private readonly string _memoryDirectory;
	private readonly string _workspaceMemoryDirectory;
	private readonly string _workspacesFilePath;

	public MemoryPlugin(string workspaceRoot)
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

		_memoryDirectory = Path.Combine(appDataPath, ".llmagentslim", "memory");
		Directory.CreateDirectory(_memoryDirectory);

		_workspacesFilePath = Path.Combine(_memoryDirectory, "workspaces.json");
		var workspaceKey = GetOrCreateWorkspaceKey(Path.GetFullPath(workspaceRoot));
		_workspaceMemoryDirectory = Path.Combine(_memoryDirectory, workspaceKey);
		Directory.CreateDirectory(_workspaceMemoryDirectory);
	}

	[KernelFunction("memory_save")]
	[Description("Saves a workspace memory entry with the specified key and value. Use this to remember findings decisions, or any information relevant to the current task.")]
	public string SaveMemory(string key, string value)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return "Key cannot be empty.";
		}

        var entry = FindMemoryEntry(key);
		var existed = entry is not null;
		var filePath = entry?.FilePath ?? Path.Combine(_workspaceMemoryDirectory, $"{Guid.NewGuid()}.json");

		SaveEntry(filePath, new MemoryEntry
		{
			Key = key,
			Value = value
		});

		return existed ? $"Memory '{key}' updated." : $"Memory '{key}' saved.";
	}

	[KernelFunction("memory_list_keys")]
	[Description("Returns a comma-separated list of all workspace memory keys.")]
	public string ListMemoryKeys()
	{
        var keys = GetMemoryEntries()
			.Select(entry => entry.Entry.Key)
			.Where(static key => !string.IsNullOrWhiteSpace(key))
			.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (keys.Count == 0)
		{
			return "No memory entries available.";
		}

		return string.Join(", ", keys);
	}

	[KernelFunction("memory_recall")]
	[Description("Returns the value stored in workspace memory for the specified key.")]
	public string RecallMemory(string key)
	{
        var entry = FindMemoryEntry(key);
		return entry is not null
			? entry.Entry.Value
			: $"Memory key '{key}' not found.";
	}

	[KernelFunction("memory_forget")]
	[Description("Deletes the workspace memory entry with the specified key.")]
	public string ForgetMemory(string key)
	{
        var entry = FindMemoryEntry(key);
		if (entry is null)
		{
			return $"Memory key '{key}' not found.";
		}

      File.Delete(entry.FilePath);
		return $"Memory '{key}' forgotten.";
	}

	[KernelFunction("memory_update")]
	[Description("Updates the value of an existing workspace memory entry.")]
	public string UpdateMemory(string key, string newValue)
	{
        var entry = FindMemoryEntry(key);
		if (entry is null)
		{
			return $"Memory key '{key}' not found.";
		}

      SaveEntry(entry.FilePath, new MemoryEntry
		{
			Key = key,
			Value = newValue
		});

		return $"Memory '{key}' updated.";
	}

	private string GetOrCreateWorkspaceKey(string workspaceRoot)
	{
		var workspaces = LoadWorkspaceMappings();
		if (workspaces.TryGetValue(workspaceRoot, out var workspaceKey) && !string.IsNullOrWhiteSpace(workspaceKey))
		{
			return workspaceKey;
		}

		workspaceKey = Guid.NewGuid().ToString();
		workspaces[workspaceRoot] = workspaceKey;
		SaveWorkspaceMappings(workspaces);
		return workspaceKey;
	}

	private Dictionary<string, string> LoadWorkspaceMappings()
	{
		if (!File.Exists(_workspacesFilePath))
		{
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		var json = File.ReadAllText(_workspacesFilePath);
		var workspaces = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
		return workspaces is null
			? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, string>(workspaces, StringComparer.OrdinalIgnoreCase);
	}

	private void SaveWorkspaceMappings(Dictionary<string, string> workspaces)
	{
		File.WriteAllText(_workspacesFilePath, JsonSerializer.Serialize(workspaces, JsonOptions));
	}

	private IReadOnlyList<StoredMemoryEntry> GetMemoryEntries()
	{
		if (!Directory.Exists(_workspaceMemoryDirectory))
		{
			return [];
		}

		var entries = new List<StoredMemoryEntry>();
		foreach (var filePath in Directory.GetFiles(_workspaceMemoryDirectory, "*.json"))
		{
			var entry = LoadEntry(filePath);
			if (entry is null || string.IsNullOrWhiteSpace(entry.Key))
			{
				continue;
			}

			entries.Add(new StoredMemoryEntry(filePath, entry));
		}

		return entries;
	}

	private StoredMemoryEntry? FindMemoryEntry(string key)
	{
		return GetMemoryEntries().FirstOrDefault(entry => string.Equals(entry.Entry.Key, key, StringComparison.OrdinalIgnoreCase));
	}

	private MemoryEntry? LoadEntry(string filePath)
	{
		var json = File.ReadAllText(filePath);
		return JsonSerializer.Deserialize<MemoryEntry>(json);
	}

	private void SaveEntry(string filePath, MemoryEntry entry)
	{
		File.WriteAllText(filePath, JsonSerializer.Serialize(entry, JsonOptions));
	}

	private sealed class MemoryEntry
	{
		public string Key { get; init; } = string.Empty;

		public string Value { get; init; } = string.Empty;
	}

	private sealed class StoredMemoryEntry(string filePath, MemoryEntry entry)
	{
		public string FilePath { get; } = filePath;

		public MemoryEntry Entry { get; } = entry;
	}
}