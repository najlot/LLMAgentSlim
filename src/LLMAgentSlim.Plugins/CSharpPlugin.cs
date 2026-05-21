using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace LLMAgentSlim;

public class Globals { }
public class CSharpPlugin
{
	#region Static Initialization

	private static readonly InteractiveAssemblyLoader _loader;
	private static readonly ScriptOptions _options;
	private static readonly Script<object> _emptyScript;

	static CSharpPlugin()
	{
		_loader = GetLoader();
		_options = GetOptions();
		_emptyScript = CSharpScript.Create(string.Empty, _options, typeof(Globals), _loader);
		_emptyScript.Compile();
	}

	private static System.Reflection.Assembly[] GetReferences() =>
	[
		typeof(object).Assembly,
		typeof(System.Console).Assembly,
		typeof(System.IO.FileInfo).Assembly,
		typeof(System.Linq.IQueryable).Assembly,
		typeof(System.Dynamic.DynamicObject).Assembly,
		typeof(System.Text.RegularExpressions.Regex).Assembly,
		typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly
	];

	private static InteractiveAssemblyLoader GetLoader()
	{
		var loader = new InteractiveAssemblyLoader();

		foreach (var reference in GetReferences())
		{
			loader.RegisterDependency(reference);
		}

		return loader;
	}

	private static ScriptOptions GetOptions() =>
		ScriptOptions.Default
			.WithReferences(GetReferences())
			.AddImports(
				"System",
				"System.IO",
				"System.Linq",
				"System.Text",
				"System.Dynamic",
				"System.Collections.Generic",
				"System.Text.RegularExpressions"
			);

	#endregion Static Initialization

	private static readonly ConcurrentDictionary<string, ScriptRunner<object>> _cache = new();

	[KernelFunction("run_csharp")]
	[Description("Executes the provided C# code and returns the console output or any exceptions as a string.")]
	public async Task<string> RunCSharp(string code)
	{
		var output = new StringWriter();
		var original = Console.Out;
		var result = string.Empty;

		try
		{
			Console.SetOut(output);

			var scriptRunner = _cache.GetOrAdd(
				code,
				static key => _emptyScript.ContinueWith(key, _options).CreateDelegate());

			await scriptRunner(new Globals()).ConfigureAwait(false);

			result = output.ToString();
		}
		catch (Exception ex)
		{
			result = ex.ToString();
		}
		finally
		{
			Console.SetOut(original);
		}

		return result;
	}
}