using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpyX;

namespace StackTraceExplorer
{
	/// <summary>
	/// Resolves stack frame methods against loaded assemblies in ILSpy.
	/// </summary>
	public class MethodResolver
	{
		private readonly AssemblyTreeModel assemblyTreeModel;

		// Pattern to extract original method from async state machine type
		private static readonly Regex AsyncStateTypePattern = new Regex(
			@"^(?<parentType>.+)\.<(?<method>\w+)>d__\d+$",
			RegexOptions.Compiled);

		// Pattern to extract original method from lambda closure type
		// Handles both non-generic and generic closure types like <>c__DisplayClass3_0'1<object>
		private static readonly Regex LambdaTypePattern = new Regex(
			@"^(?<parentType>.+)\.<>c(__DisplayClass\d+_\d+)?([`']\d+)?(<[^>]+>)?$",
			RegexOptions.Compiled);

		// Pattern to detect generic arity (e.g., List`1 or List'1)
		// Note: Some stack traces use apostrophe (') instead of backtick (`)
		private static readonly Regex GenericArityPattern = new Regex(
			@"[`'](\d+)$",
			RegexOptions.Compiled);

		// Pattern to detect property accessor (get_PropertyName or set_PropertyName)
		// and event accessor (add_EventName or remove_EventName)
		private static readonly Regex AccessorPattern = new Regex(
			@"^(?<accessor>get|set|add|remove)_(?<member>.+)$",
			RegexOptions.Compiled);

		public MethodResolver(AssemblyTreeModel assemblyTreeModel)
		{
			this.assemblyTreeModel = assemblyTreeModel;
		}

		/// <summary>
		/// Strips all generic type arguments from a type or method name and appends generic arity.
		/// Properly handles nested angle brackets (e.g., &lt;List&lt;int&gt;, Dictionary&lt;string, int&gt;&gt;).
		/// Example: AsyncEventExecutionManager&lt;A, B&gt; → AsyncEventExecutionManager`2
		/// </summary>
		private static string StripGenericTypeArgs(string name)
		{
			if (string.IsNullOrEmpty(name) || !name.Contains('<'))
				return name;

			var result = new System.Text.StringBuilder(name.Length);
			int depth = 0;
			int topLevelCommaCount = 0;
			bool hasGenericArgs = false;

			foreach (char c in name)
			{
				if (c == '<')
				{
					if (depth == 0)
					{
						hasGenericArgs = true;
						topLevelCommaCount = 0; // Reset for each generic section
					}
					depth++;
				}
				else if (c == '>')
				{
					depth--;
					if (depth == 0 && hasGenericArgs)
					{
						// Append the generic arity (number of type args = commas + 1)
						result.Append('`');
						result.Append(topLevelCommaCount + 1);
						hasGenericArgs = false;
					}
				}
				else if (depth == 0)
				{
					result.Append(c);
				}
				else if (depth == 1 && c == ',')
				{
					// Count commas at the top level of generic args
					topLevelCommaCount++;
				}
			}

			return result.ToString();
		}

		/// <summary>
		/// Resolves a stack frame to an IMethod in the loaded assemblies.
		/// </summary>
		public async Task<IMethod?> ResolveAsync(StackFrame frame)
		{
			if (!frame.IsMethodFrame || string.IsNullOrEmpty(frame.FullTypeName))
				return null;

			return await Task.Run(() => Resolve(frame));
		}

		/// <summary>
		/// Resolves a stack frame to an IMethod synchronously.
		/// </summary>
		public IMethod? Resolve(StackFrame frame)
		{
			if (!frame.IsMethodFrame || string.IsNullOrEmpty(frame.FullTypeName))
				return null;

			var assemblies = assemblyTreeModel.AssemblyList.GetAssemblies();

			// If we have an assembly name hint, try that first
			if (!string.IsNullOrEmpty(frame.AssemblyName))
			{
				var assemblyHint = GetAssemblyNameWithoutExtension(frame.AssemblyName);
				var prioritizedAssemblies = assemblies
					.OrderByDescending(a => GetAssemblyNameWithoutExtension(a.ShortName)
						.Equals(assemblyHint, StringComparison.OrdinalIgnoreCase))
					.ToList();

				foreach (var assembly in prioritizedAssemblies)
				{
					var method = TryResolveInAssembly(frame, assembly);
					if (method != null)
						return method;
				}
			}
			else
			{
				// Search all assemblies
				foreach (var assembly in assemblies)
				{
					var method = TryResolveInAssembly(frame, assembly);
					if (method != null)
						return method;
				}
			}

			return null;
		}

		private string GetAssemblyNameWithoutExtension(string name)
		{
			if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
				name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
			{
				return name.Substring(0, name.Length - 4);
			}
			return name;
		}

		private IMethod? TryResolveInAssembly(StackFrame frame, LoadedAssembly assembly)
		{
			// Use the type system that LoadedAssembly already caches internally
			// This avoids creating expensive DecompilerTypeSystem instances
			var typeSystem = assembly.GetTypeSystemOrNull();
			if (typeSystem == null)
				return null;

			try
			{
				var type = FindType(typeSystem, frame.FullTypeName!);
				if (type == null)
				{
					// For compiler-generated types, try to find the parent type
					type = FindParentTypeForCompilerGenerated(typeSystem, frame.FullTypeName!);
				}

				if (type == null)
					return null;

				return FindMethod(type, frame);
			}
			catch
			{
				return null;
			}
		}

		private ITypeDefinition? FindType(ICompilation typeSystem, string fullTypeName)
		{
			// Strip generic type arguments like <System.Guid> or <A, B> from the type name
			// These appear in stack traces but aren't part of the actual type name
			// Use balanced bracket matching to handle nested generics properly
			fullTypeName = StripGenericTypeArgs(fullTypeName);

			// If no dots in the name, this is a simple type name - search by name only
			// This handles stack traces like "at CrmTrace.Write(...)" where only the class name is present
			if (!fullTypeName.Contains('.') && !fullTypeName.Contains('+'))
			{
				return FindTypeBySimpleName(typeSystem, fullTypeName);
			}

			// Handle nested types (indicated by +)
			var parts = fullTypeName.Replace('+', '.').Split('.');
			if (parts.Length == 0)
				return null;

			// Try to find the type by progressively building the namespace
			foreach (var module in typeSystem.Modules)
			{
				// Try exact match first
				var type = module.GetTypeDefinition(new TopLevelTypeName(fullTypeName));
				if (type != null)
					return type;

				// Try building namespace.typename combinations
				for (int i = parts.Length - 1; i >= 1; i--)
				{
					var ns = string.Join(".", parts.Take(i));
					var typeName = string.Join(".", parts.Skip(i));

					// Handle nested types
					var typeNameParts = typeName.Split('.');
					var topLevelName = typeNameParts[0];

					// Handle generic types (e.g., List`1)
					var genericMatch = GenericArityPattern.Match(topLevelName);
					int typeParameterCount = 0;
					if (genericMatch.Success)
					{
						typeParameterCount = int.Parse(genericMatch.Groups[1].Value);
						topLevelName = topLevelName.Substring(0, genericMatch.Index);
					}

					type = module.GetTypeDefinition(new TopLevelTypeName(ns, topLevelName, typeParameterCount));

					// Navigate nested types
					for (int j = 1; j < typeNameParts.Length && type != null; j++)
					{
						var nestedName = typeNameParts[j];
						genericMatch = GenericArityPattern.Match(nestedName);
						int nestedTypeParams = 0;
						if (genericMatch.Success)
						{
							nestedTypeParams = int.Parse(genericMatch.Groups[1].Value);
							nestedName = nestedName.Substring(0, genericMatch.Index);
						}

						type = type.NestedTypes.FirstOrDefault(t =>
							t.Name == nestedName &&
							t.TypeParameterCount == type.TypeParameterCount + nestedTypeParams);
					}

					if (type != null)
						return type;
				}
			}

			return null;
		}

		private ITypeDefinition? FindParentTypeForCompilerGenerated(ICompilation typeSystem, string fullTypeName)
		{
			// For async state machines: Namespace.Class.<MethodName>d__1 -> Namespace.Class
			var asyncMatch = AsyncStateTypePattern.Match(fullTypeName);
			if (asyncMatch.Success)
			{
				return FindType(typeSystem, asyncMatch.Groups["parentType"].Value);
			}

			// For lambdas: Namespace.Class.<>c__DisplayClass1_0 -> Namespace.Class
			var lambdaMatch = LambdaTypePattern.Match(fullTypeName);
			if (lambdaMatch.Success)
			{
				return FindType(typeSystem, lambdaMatch.Groups["parentType"].Value);
			}

			return null;
		}

		private IMethod? FindMethod(ITypeDefinition type, StackFrame frame)
		{
			if (string.IsNullOrEmpty(frame.MethodName))
				return null;

			var methodName = frame.MethodName!;

			// Strip generic type arguments from method name (e.g., Execute<bool> -> Execute)
			methodName = StripGenericTypeArgs(methodName);

			// Check if this is a static constructor (.cctor)
			bool isStaticConstructor = methodName == ".cctor";

			// Check if this is a constructor call (method name matches type name)
			bool isConstructor = false;
			var simpleTypeName = type.Name;
			// Handle generic types - strip `N suffix
			var backtickIndex = simpleTypeName.IndexOf('`');
			if (backtickIndex > 0)
				simpleTypeName = simpleTypeName.Substring(0, backtickIndex);

			if (methodName.Equals(simpleTypeName, StringComparison.Ordinal))
			{
				isConstructor = true;
			}

			// Check if this is a property accessor (get_PropertyName or set_PropertyName)
			// or event accessor (add_EventName or remove_EventName)
			string? memberName = null;
			string? accessorType = null;
			var accessorMatch = AccessorPattern.Match(methodName);
			if (accessorMatch.Success)
			{
				memberName = accessorMatch.Groups["member"].Value;
				accessorType = accessorMatch.Groups["accessor"].Value;
			}

			// For compiler-generated methods, extract the original method name
			string? originalMethodName = null;
			if (frame.FrameType == StackFrameType.AsyncStateMachine)
			{
				// <MethodName>d__1.MoveNext -> look for MethodName
				var match = Regex.Match(frame.FullTypeName ?? "", @"<(?<method>\w+)>d__\d+$");
				if (match.Success)
					originalMethodName = match.Groups["method"].Value;
			}
			else if (frame.FrameType == StackFrameType.Lambda)
			{
				// <>c__DisplayClass.<Execute>b__0 -> look for Execute in parent
				var match = Regex.Match(methodName, @"<(?<method>\w+)>b__\d+");
				if (match.Success)
					originalMethodName = match.Groups["method"].Value;
			}

			// Build list of method name candidates to search
			var candidates = new List<string> { methodName };
			if (isStaticConstructor)
			{
				candidates.Add(".cctor"); // IL name for static constructors
			}
			if (isConstructor)
			{
				candidates.Add(".ctor"); // IL name for instance constructors
				candidates.Add(".cctor"); // IL name for static constructors
			}
			if (!string.IsNullOrEmpty(originalMethodName))
				candidates.Add(originalMethodName);

			// Handle generic method arity in name (e.g., Execute`1)
			foreach (var candidate in candidates.ToList())
			{
				var genericMatch = GenericArityPattern.Match(candidate);
				if (genericMatch.Success)
				{
					candidates.Add(candidate.Substring(0, genericMatch.Index));
				}
			}

			// Search for matching method
			var methods = type.Methods.Where(m => candidates.Contains(m.Name)).ToList();

			if (methods.Count == 0)
			{
				// Try nested types for state machines
				foreach (var nestedType in type.NestedTypes)
				{
					var nestedMethods = nestedType.Methods.Where(m => candidates.Contains(m.Name)).ToList();
					if (nestedMethods.Count > 0)
						methods = nestedMethods;
				}
			}

			// If still no methods found, try property/event accessors
			if (methods.Count == 0 && !string.IsNullOrEmpty(memberName) && !string.IsNullOrEmpty(accessorType))
			{
				// Try properties
				var property = type.Properties.FirstOrDefault(p =>
					p.Name.Equals(memberName, StringComparison.Ordinal));
				if (property != null)
				{
					if (accessorType == "get" && property.Getter != null)
						return property.Getter;
					if (accessorType == "set" && property.Setter != null)
						return property.Setter;
				}

				// Try events
				var evt = type.Events.FirstOrDefault(e =>
					e.Name.Equals(memberName, StringComparison.Ordinal));
				if (evt != null)
				{
					if (accessorType == "add" && evt.AddAccessor != null)
						return evt.AddAccessor;
					if (accessorType == "remove" && evt.RemoveAccessor != null)
						return evt.RemoveAccessor;
				}
			}

			if (methods.Count == 0)
				return null;

			// Filter by generic arity FIRST (before checking if only one method)
			// This ensures we pick the right overload when there's both generic and non-generic versions
			if (frame.GenericMethodArity > 0)
			{
				var byGenericArity = methods.Where(m => m.TypeParameters.Count == frame.GenericMethodArity).ToList();
				if (byGenericArity.Count == 1)
					return byGenericArity[0];
				if (byGenericArity.Count > 0)
					methods = byGenericArity;
			}
			else
			{
				// Prefer non-generic methods if no arity specified
				var nonGeneric = methods.Where(m => m.TypeParameters.Count == 0).ToList();
				if (nonGeneric.Count > 0)
					methods = nonGeneric;
			}

			if (methods.Count == 1)
				return methods[0];

			// Multiple overloads - try to match by parameter count
			if (!string.IsNullOrEmpty(frame.Parameters))
			{
				var paramCount = CountParameters(frame.Parameters!);
				var byParamCount = methods.Where(m => m.Parameters.Count == paramCount).ToList();
				if (byParamCount.Count == 1)
					return byParamCount[0];
				if (byParamCount.Count > 0)
					methods = byParamCount;

				// Try to match by parameter types
				var paramTypes = ParseParameterTypes(frame.Parameters!);
				var byParamTypes = methods
					.Where(m => MatchesParameterTypes(m, paramTypes))
					.ToList();
				if (byParamTypes.Count == 1)
					return byParamTypes[0];
				if (byParamTypes.Count > 0)
					return byParamTypes[0];
			}

			// Return first match
			return methods[0];
		}

		private int CountParameters(string parameters)
		{
			if (string.IsNullOrWhiteSpace(parameters))
				return 0;

			// Count commas, handling nested generics
			int count = 1;
			int depth = 0;
			foreach (char c in parameters)
			{
				switch (c)
				{
					case '<':
					case '(':
						depth++;
						break;
					case '>':
					case ')':
						depth--;
						break;
					case ',':
						if (depth == 0)
							count++;
						break;
				}
			}
			return count;
		}

		private List<string> ParseParameterTypes(string parameters)
		{
			var types = new List<string>();
			if (string.IsNullOrWhiteSpace(parameters))
				return types;

			var current = new System.Text.StringBuilder();
			int depth = 0;

			foreach (char c in parameters)
			{
				switch (c)
				{
					case '<':
					case '(':
						depth++;
						current.Append(c);
						break;
					case '>':
					case ')':
						depth--;
						current.Append(c);
						break;
					case ',':
						if (depth == 0)
						{
							types.Add(ExtractTypeName(current.ToString()));
							current.Clear();
						}
						else
						{
							current.Append(c);
						}
						break;
					default:
						current.Append(c);
						break;
				}
			}

			if (current.Length > 0)
			{
				types.Add(ExtractTypeName(current.ToString()));
			}

			return types;
		}

		private string ExtractTypeName(string paramDecl)
		{
			// Parameter declaration can be "Type name" or just "Type"
			var trimmed = paramDecl.Trim();

			// Handle ref/out/in modifiers
			trimmed = Regex.Replace(trimmed, @"^(ref|out|in)\s+", "");

			// Split on last space to separate type from name
			var lastSpace = trimmed.LastIndexOf(' ');
			if (lastSpace > 0)
			{
				var potentialType = trimmed.Substring(0, lastSpace);
				// Check if what follows looks like a variable name
				var potentialName = trimmed.Substring(lastSpace + 1);
				if (Regex.IsMatch(potentialName, @"^[a-zA-Z_]\w*$"))
					return potentialType;
			}

			return trimmed;
		}

		private bool MatchesParameterTypes(IMethod method, List<string> expectedTypes)
		{
			if (method.Parameters.Count != expectedTypes.Count)
				return false;

			for (int i = 0; i < method.Parameters.Count; i++)
			{
				var paramType = method.Parameters[i].Type;
				var expected = expectedTypes[i];

				if (!TypeMatches(paramType, expected))
					return false;
			}

			return true;
		}

		private bool TypeMatches(IType type, string expected)
		{
			// Get simple name
			var typeName = type.Name;
			var typeFullName = type.FullName;

			// Normalize expected type
			expected = expected.Trim();

			// Check direct match
			if (typeName.Equals(expected, StringComparison.Ordinal) ||
				typeFullName.Equals(expected, StringComparison.Ordinal))
				return true;

			// Handle common type aliases
			var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
			{
				{ "int", new[] { "Int32", "System.Int32" } },
				{ "string", new[] { "String", "System.String" } },
				{ "bool", new[] { "Boolean", "System.Boolean" } },
				{ "long", new[] { "Int64", "System.Int64" } },
				{ "double", new[] { "Double", "System.Double" } },
				{ "float", new[] { "Single", "System.Single" } },
				{ "object", new[] { "Object", "System.Object" } },
				{ "byte", new[] { "Byte", "System.Byte" } },
				{ "char", new[] { "Char", "System.Char" } },
				{ "short", new[] { "Int16", "System.Int16" } },
				{ "uint", new[] { "UInt32", "System.UInt32" } },
				{ "ulong", new[] { "UInt64", "System.UInt64" } },
				{ "ushort", new[] { "UInt16", "System.UInt16" } },
				{ "decimal", new[] { "Decimal", "System.Decimal" } },
			};

			foreach (var kvp in aliases)
			{
				if (expected.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
				{
					if (kvp.Value.Any(v => typeFullName.Equals(v, StringComparison.Ordinal)))
						return true;
				}
				if (kvp.Value.Any(v => expected.Equals(v, StringComparison.OrdinalIgnoreCase)))
				{
					if (typeFullName.EndsWith(kvp.Value[1], StringComparison.Ordinal))
						return true;
				}
			}

			// Check if expected ends with the type name (handles namespace differences)
			if (expected.EndsWith("." + typeName, StringComparison.Ordinal) ||
				expected.Equals(typeName, StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}

		/// <summary>
		/// Searches for a type by its simple name only (no namespace).
		/// This handles stack traces where only the class name is available.
		/// </summary>
		private ITypeDefinition? FindTypeBySimpleName(ICompilation typeSystem, string simpleName)
		{
			// Strip generic arity from simple name (e.g., List`1 -> List with arity 1)
			var genericMatch = GenericArityPattern.Match(simpleName);
			int expectedArity = 0;
			if (genericMatch.Success)
			{
				expectedArity = int.Parse(genericMatch.Groups[1].Value);
				simpleName = simpleName.Substring(0, genericMatch.Index);
			}

			// Only search the main module (the assembly we're currently checking)
			// This is much faster than searching all referenced modules
			var mainModule = typeSystem.MainModule;
			if (mainModule == null)
				return null;

			// TopLevelTypeDefinitions includes types from all namespaces in this module
			foreach (var type in mainModule.TopLevelTypeDefinitions)
			{
				// Compare just the simple type name (type.Name), not the full name
				if (type.Name.Equals(simpleName, StringComparison.Ordinal) &&
					type.TypeParameterCount == expectedArity)
				{
					return type;
				}

				// Also check nested types (for cases like Outer.Inner where we only have "Inner")
				var nestedMatch = FindNestedTypeByName(type, simpleName, expectedArity);
				if (nestedMatch != null)
					return nestedMatch;
			}

			return null;
		}

		/// <summary>
		/// Recursively searches nested types for a type with the given simple name.
		/// </summary>
		private ITypeDefinition? FindNestedTypeByName(ITypeDefinition parent, string name, int arity)
		{
			foreach (var nested in parent.NestedTypes)
			{
				if (nested.Name.Equals(name, StringComparison.Ordinal) &&
					nested.TypeParameterCount == arity)
				{
					return nested;
				}

				var deeperMatch = FindNestedTypeByName(nested, name, arity);
				if (deeperMatch != null)
					return deeperMatch;
			}
			return null;
		}
	}
}
