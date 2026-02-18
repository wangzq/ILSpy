using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace StackTraceExplorer.Parsers
{
	/// <summary>
	/// Parses Watson crash dump stack trace format.
	/// Examples:
	///   clr!AppDomain::BindAssemblySpec+0x0 [f:\dd\ndp\clr\src\vm\appdomain.cpp @ 9084]
	///   mscorlib_ni!System.Reflection.Assembly.LoadFile+0x0 [f:\dd\ndp\clr\src\BCL\system\reflection\assembly.cs @ 588]
	///   Microsoft_Crm_Core!Microsoft.Crm.Core.Helpers.DynamicBindingRedirector.CurrentDomain_AssemblyResolve+0x0
	///   unknown!Microsoft.Identity.Client.OAuth2.OAuth2Client+_ExecuteRequestAsync_d__13
	///   mscorlib_ni!System.Threading.Tasks.Task`1[[System.__Canon, mscorlib]].TrySetResult+0x0 [[System.__Canon, mscorlib @ 490]
	/// </summary>
	public class WatsonCrashParser : IStackTraceParser
	{
		// Pattern: "Module!FullMethod+0xOffset [source @ line]"
		// Module names can have underscores (e.g., mscorlib_ni, Microsoft_Crm_Core)
		// Method can include generic parameters in [[...]] notation
		// Source info is optional and in brackets: [filepath @ linenumber]
		private static readonly Regex WatsonPattern = new Regex(
			@"^(?<module>[^\!\s]+)!(?<fullMethod>.+?)(?:\+0x(?<offset>[0-9A-Fa-f]+))?\s*(?:\[(?<source>[^\]]+)\])?$",
			RegexOptions.Compiled);

		// Pattern to detect async state machine: ._MethodName_d__N or <MethodName>d__N or just _d__N at end
		// After + is replaced with ., the pattern becomes ._MethodName_d__N
		private static readonly Regex AsyncPattern = new Regex(
			@"(?:\._\w+_d__\d+$|<\w+>d__\d+|_d__\d+$)",
			RegexOptions.Compiled);

		// Pattern to detect lambda/closure: <>c__DisplayClass or <>c.<Method>b__N
		private static readonly Regex LambdaPattern = new Regex(
			@"<>c(__DisplayClass\d+_\d+)?\.?(<\w+>b__\d+|<\w+>)?",
			RegexOptions.Compiled);

		// Pattern to extract generic method arguments: Method[T] or Method<T>
		private static readonly Regex GenericMethodPattern = new Regex(
			@"^(?<name>\w+)(?:\[(?<args>[^\]]+)\]|<(?<args>[^>]+)>)$",
			RegexOptions.Compiled);

		// Pattern to strip generic type info from method: Task`1[[...]]
		private static readonly Regex GenericTypeInMethodPattern = new Regex(
			@"`\d+\[\[.*?\]\]",
			RegexOptions.Compiled);

		public int Priority => 25; // Higher priority than WinDbgParser (20)

		public bool CanParse(string line)
		{
			// Must contain ! and either +0x offset or look like Watson format
			// Exclude WinDbg format which has (IL= or (IL≈
			if (line.Contains("(IL=") || line.Contains("(IL≈"))
				return false;

			if (!line.Contains("!"))
				return false;

			// Watson format typically has Module!Method+0x0 or Module!Method [source]
			// or just Module!Method
			var trimmed = line.Trim();
			var bangIndex = trimmed.IndexOf('!');
			if (bangIndex <= 0 || bangIndex >= trimmed.Length - 1)
				return false;

			// Check if it's a valid module name (no spaces before !)
			var module = trimmed.Substring(0, bangIndex);
			if (module.Contains(' ') || module.Contains('\t'))
				return false;

			return true;
		}

		public StackFrame? Parse(string line)
		{
			var match = WatsonPattern.Match(line.Trim());
			if (!match.Success)
				return null;

			var moduleName = match.Groups["module"].Value.Trim();
			var fullMethod = match.Groups["fullMethod"].Value.Trim();
			var sourceInfo = match.Groups["source"].Success ? match.Groups["source"].Value : null;

			// Skip native frames (clr, ntdll, kernel32, KERNELBASE, VCRUNTIME, etc.)
			// These don't have .NET type information
			if (IsNativeModule(moduleName))
			{
				return new StackFrame {
					FrameType = StackFrameType.Unknown,
					RawText = line,
					AssemblyName = moduleName
				};
			}

			// Convert module name: Microsoft_Crm_Core -> Microsoft.Crm.Core
			// But keep _ni suffix handling (mscorlib_ni -> mscorlib)
			var assemblyName = ConvertModuleToAssemblyName(moduleName);

			// Clean up generic type notation: Task`1[[System.__Canon, mscorlib]] -> Task
			fullMethod = GenericTypeInMethodPattern.Replace(fullMethod, "");

			// Handle nested class notation with + (e.g., OuterClass+NestedClass.Method)
			// Convert + to . for consistency, but be careful with async state machines
			// Async: OAuth2Client+_ExecuteRequestAsync_d__13 -> OAuth2Client._ExecuteRequestAsync_d__13
			fullMethod = fullMethod.Replace("+", ".");

			// Split fullMethod into type and method
			var lastDot = FindLastDotOutsideAngleBrackets(fullMethod);
			if (lastDot <= 0)
			{
				// Could be just a function name (native-style like C++ methods)
				return new StackFrame {
					FrameType = StackFrameType.Unknown,
					RawText = line,
					AssemblyName = assemblyName
				};
			}

			var fullTypeName = fullMethod.Substring(0, lastDot);
			var methodName = fullMethod.Substring(lastDot + 1);

			// Handle generic method syntax: Method[T] or Method<T> -> Method
			int genericMethodArity = 0;
			var genericMatch = GenericMethodPattern.Match(methodName);
			if (genericMatch.Success)
			{
				methodName = genericMatch.Groups["name"].Value;
				var args = genericMatch.Groups["args"].Value;
				genericMethodArity = 1 + args.Count(c => c == ',');
			}

			// Determine frame type
			var frameType = StackFrameType.Method;
			if (AsyncPattern.IsMatch(fullMethod))
			{
				frameType = StackFrameType.AsyncStateMachine;
			}
			else if (LambdaPattern.IsMatch(fullMethod))
			{
				frameType = StackFrameType.Lambda;
			}

			var frame = new StackFrame {
				FrameType = frameType,
				RawText = line,
				AssemblyName = assemblyName,
				FullTypeName = fullTypeName,
				MethodName = methodName,
				Parameters = "", // Watson format doesn't include parameters
				GenericMethodArity = genericMethodArity
			};

			return frame;
		}

		/// <summary>
		/// Determines if a module is a native (non-.NET) module.
		/// </summary>
		private static bool IsNativeModule(string moduleName)
		{
			var lower = moduleName.ToLowerInvariant();
			return lower == "clr"
				|| lower == "ntdll"
				|| lower == "kernel32"
				|| lower == "kernelbase"
				|| lower.StartsWith("vcruntime")
				|| lower == "coreclr"
				|| lower == "hostpolicy"
				|| lower == "hostfxr";
		}

		/// <summary>
		/// Converts Watson module name to assembly name.
		/// E.g., Microsoft_Crm_Core -> Microsoft.Crm.Core
		///       mscorlib_ni -> mscorlib
		///       System_Net_Http -> System.Net.Http
		/// </summary>
		private static string ConvertModuleToAssemblyName(string moduleName)
		{
			// Remove _ni suffix (native image)
			if (moduleName.EndsWith("_ni", StringComparison.OrdinalIgnoreCase))
			{
				moduleName = moduleName.Substring(0, moduleName.Length - 3);
			}

			// Convert underscores to dots
			return moduleName.Replace('_', '.');
		}

		/// <summary>
		/// Finds the last dot that is not inside angle brackets or square brackets.
		/// </summary>
		private static int FindLastDotOutsideAngleBrackets(string text)
		{
			int depth = 0;
			int lastDotOutside = -1;

			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c == '<' || c == '[')
				{
					depth++;
				}
				else if (c == '>' || c == ']')
				{
					depth--;
				}
				else if (c == '.' && depth == 0)
				{
					lastDotOutside = i;
				}
			}

			return lastDotOutside;
		}
	}
}
