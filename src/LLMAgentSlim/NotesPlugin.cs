using LiteDB;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLMAgentSlim;

internal class NotesPlugin
{
	private const string CollectionName = "notes";
	private readonly string _databasePath;
	private readonly string _projectPath;

	public NotesPlugin(string projectPath)
	{
		_projectPath = Path.GetFullPath(projectPath);

		var databaseDirectory = LLMAgentSlimPaths.GetAppDataDirectory();
		Directory.CreateDirectory(databaseDirectory);

		_databasePath = Path.Combine(databaseDirectory, "notes.db");
		InitializeDatabase();
	}

	private sealed class Note
	{
		public int Id { get; set; }
		public string ProjectPath { get; set; } = string.Empty;
		public string NormalizedName { get; set; } = string.Empty;
		public string ProjectScopedNameKey { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Content { get; set; } = string.Empty;
	}

	[KernelFunction("add_note")]
	[Description("Adds a new note with the specified name and content.")]
	public string AddNote(string name, string content)
	{
		using var database = OpenDatabase();
		var notes = GetCollection(database);
		var normalizedName = NormalizeName(name);
		var note = FindNote(notes, normalizedName);

		if (note != null)
		{
			note.Name = name;
			note.Content = content;
			notes.Update(note);

			return $"Note '{name}' updated.";
		}

		notes.Insert(new Note
		{
			ProjectPath = _projectPath,
			NormalizedName = normalizedName,
			ProjectScopedNameKey = CreateProjectScopedNameKey(normalizedName),
			Name = name,
			Content = content
		});

		return $"Note '{name}' added.";
	}

	[KernelFunction("get_note_names")]
	[Description("Returns a comma-separated list of all note names.")]
	public string GetNoteNames()
	{
		using var database = OpenDatabase();
		var notes = GetCollection(database)
			.Find(note => note.ProjectPath == _projectPath)
			.OrderBy(note => note.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (notes.Count == 0)
		{
			return "No notes available.";
		}

		return string.Join(", ", notes.Select(note => note.Name));
	}

	[KernelFunction("get_note_content")]
	[Description("Returns the content of the note with the specified name.")]
	public string GetNoteContent(string name)
	{
		using var database = OpenDatabase();
		var note = FindNote(GetCollection(database), NormalizeName(name));
		if (note == null)
		{
			return $"Note with name '{name}' not found.";
		}

		return note.Content;
	}

	[KernelFunction("delete_note")]
	[Description("Deletes the note with the specified name.")]
	public string DeleteNote(string name)
	{
		using var database = OpenDatabase();
		var notes = GetCollection(database);
		var note = FindNote(notes, NormalizeName(name));
		if (note == null)
		{
			return $"Note with name '{name}' not found.";
		}

		notes.Delete(note.Id);

		return $"Note '{name}' deleted.";
	}

	[KernelFunction("update_note")]
	[Description("Updates the content of the note with the specified name.")]
	public string UpdateNote(string name, string newContent)
	{
		using var database = OpenDatabase();
		var notes = GetCollection(database);
		var note = FindNote(notes, NormalizeName(name));
		if (note == null)
		{
			return $"Note with name '{name}' not found.";
		}

		note.Content = newContent;
		notes.Update(note);
		return $"Note '{name}' updated.";
	}

	private void InitializeDatabase()
	{
		using var database = OpenDatabase();
		var notes = GetCollection(database);
		notes.EnsureIndex(note => note.ProjectPath);
		notes.EnsureIndex(note => note.ProjectScopedNameKey, unique: true);
	}

	private LiteDatabase OpenDatabase() => new(_databasePath);

	private ILiteCollection<Note> GetCollection(LiteDatabase database) => database.GetCollection<Note>(CollectionName);

	private Note? FindNote(ILiteCollection<Note> notes, string normalizedName) =>
		notes.FindOne(note => note.ProjectScopedNameKey == CreateProjectScopedNameKey(normalizedName));

	private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

	private string CreateProjectScopedNameKey(string normalizedName) => $"{_projectPath}|{normalizedName}";
}