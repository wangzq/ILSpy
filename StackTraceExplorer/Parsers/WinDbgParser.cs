using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace StackTraceExplorer.Parsers
{
	/// <summary>
	/// Parses WinDbg/Visual Studio debugger stack trace format.
	/// Examples:
	///   Assembly.dll!Namespace.Class.Method(params) (IL≈0xHEX, Native=0xAddr+0xOff)
	///   Microsoft.Crm.Core.dll!Microsoft.Crm.CrmDataReader.GetString(int i) (IL=0x0000, Native=0x00007FFA6EA9DDF0+0x31)
	///   >	Assembly.dll!Method() (IL=epilog, Native=0x...)
	/// </summary>
	public class WinDbgParser : IStackTraceParser
	{
		// Pattern: "Assembly.dll!Namespace.Class.Method(params) (IL≈0xHEX, Native=0xAddr+0xOff)"
		// The > at the beginning indicates current frame, tabs/spaces are leading whitespace
		// Uses non-greedy match for fullMethod, then greedily match params to handle nested parens
		private static readonly Regex WinDbgPattern = new Regex(
			@"^[>\s\t]*(?<assembly>[^\!\s]+)!(?<fullMethod>.+?)\((?<params>.*)\)\s*\(IL[≈=](?:0x)?(?<ilOffset>[0-9A-Fa-f]+|epilog)(?:,\s*Native=0x(?<nativeAddr>[0-9A-Fa-f]+)(?:\+0x(?<nativeOffset>[0-9A-Fa-f]+))?)?\)",
			RegexOptions.Compiled);

		// Pattern to detect async state machine: <MethodName>d__N.MoveNext
		private static readonly Regex AsyncPattern = new Regex(
			@"<\w+>d__\d+\.MoveNext$",
			RegexOptions.Compiled);

		// Pattern to detect lambda/closure: <>c__DisplayClass or <>c.<Method>b__N
		private static readonly Regex LambdaPattern = new Regex(
			@"<>c(__DisplayClass\d+_\d+)?\.(<\w+>b__\d+|<\w+>)",
			RegexOptions.Compiled);

		// Pattern to extract generic method arguments: Method[T] or Method<T>
		private static readonly Regex GenericMethodPattern = new Regex(
			@"^(?<name>\w+)(?:\[(?<args>[^\]]+)\]|<(?<args>[^>]+)>)$",
			RegexOptions.Compiled);

		public int Priority => 20; // Higher priority than StandardNetParser

		/// <summary>
		/// Finds the last dot that is not inside angle brackets.
		/// This handles cases like "Class.Execute&lt;System.Guid&gt;" where we want
		/// the dot before Execute, not the one inside the generic args.
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

		public bool CanParse(string line)
		{
			return line.Contains("!") && (line.Contains("(IL=") || line.Contains("(IL≈"));
		}

		public StackFrame? Parse(string line)
		{
			var match = WinDbgPattern.Match(line);
			if (!match.Success)
				return null;

			var assemblyName = match.Groups["assembly"].Value.Trim();
			var fullMethod = match.Groups["fullMethod"].Value.Trim();
			var parameters = match.Groups["params"].Value.Trim();
			var ilOffsetStr = match.Groups["ilOffset"].Value;

			// Split fullMethod into type and method
			// Need to find last dot NOT inside angle brackets (for generic args like <System.Guid>)
			var lastDot = FindLastDotOutsideAngleBrackets(fullMethod);
			if (lastDot <= 0)
				return null;

			var fullTypeName = fullMethod.Substring(0, lastDot);
			var methodName = fullMethod.Substring(lastDot + 1);

			// Handle generic method syntax: Method[T] or Method<T> -> Method
			int genericMethodArity = 0;
			var genericMatch = GenericMethodPattern.Match(methodName);
			if (genericMatch.Success)
			{
				methodName = genericMatch.Groups["name"].Value;
				var args = genericMatch.Groups["args"].Value;
				// Count type arguments
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
				Parameters = parameters,
				GenericMethodArity = genericMethodArity
			};

			// Parse IL offset (could be "epilog" or hex)
			if (!string.IsNullOrEmpty(ilOffsetStr) && ilOffsetStr != "epilog")
			{
				if (int.TryParse(ilOffsetStr, System.Globalization.NumberStyles.HexNumber, null, out int offset))
				{
					frame.ILOffset = offset;
				}
			}

			return frame;
		}
	}
}
