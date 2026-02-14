using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace StackTraceExplorer.Parsers
{
	/// <summary>
	/// Parses standard .NET stack trace format.
	/// Examples:
	///   at Namespace.Class.Method(Type param)
	///   at Namespace.Class.Method(Type param)  ilOffset = 0x8E
	///   at ClassName.Method(params) +0x0
	/// </summary>
	public class StandardNetParser : IStackTraceParser
	{
		// Pattern: "   at Namespace.Class.Method(params)  ilOffset = 0xHEX" or "+0xHEX"
		// Made more relaxed - params and closing paren are optional for truncated lines
		private static readonly Regex StandardPattern = new Regex(
			@"^\s*at\s+(?<fullMethod>[^\(]+)(?:\((?<params>[^\)]*)\)?)?(?:\s+(?:ilOffset\s*=\s*)?(?:\+)?0x(?<ilOffset>[0-9A-Fa-f]+))?",
			RegexOptions.Compiled);

		// Fallback pattern for lines without "at " prefix but look like method calls
		private static readonly Regex FallbackMethodPattern = new Regex(
			@"^[\s>]*(?<fullMethod>[\w\.]+\.\w+)(?:\[(?<genericArgs>[^\]]+)\]|<(?<genericArgs>[^>]+)>)?(?:\((?<params>[^\)]*)\)?)?",
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

		public int Priority => 10;

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
			// More relaxed - accept lines with "at " or lines that look like method calls
			if (line.Contains(" at ") || line.TrimStart().StartsWith("at "))
				return true;

			// Check if it looks like a method call (has dots and possibly parentheses)
			var trimmed = line.TrimStart(' ', '\t', '>');
			return trimmed.Contains('.') && !trimmed.StartsWith("[") && !trimmed.StartsWith("---");
		}

		public StackFrame? Parse(string line)
		{
			Match match;
			string fullMethod;
			string parameters;
			string ilOffsetStr;

			// Try standard pattern first
			match = StandardPattern.Match(line);
			if (match.Success && match.Groups["fullMethod"].Success)
			{
				fullMethod = match.Groups["fullMethod"].Value.Trim();
				parameters = match.Groups["params"].Success ? match.Groups["params"].Value.Trim() : "";
				ilOffsetStr = match.Groups["ilOffset"].Value;
			}
			else
			{
				// Try fallback pattern for lines without "at "
				match = FallbackMethodPattern.Match(line);
				if (!match.Success || !match.Groups["fullMethod"].Success)
					return null;

				fullMethod = match.Groups["fullMethod"].Value.Trim();
				parameters = match.Groups["params"].Success ? match.Groups["params"].Value.Trim() : "";
				ilOffsetStr = "";

				// Handle generic args in fallback
				if (match.Groups["genericArgs"].Success)
				{
					var args = match.Groups["genericArgs"].Value;
					// Append generic marker to method name for later processing
					fullMethod = fullMethod + "[" + args + "]";
				}
			}

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
				FullTypeName = fullTypeName,
				MethodName = methodName,
				Parameters = parameters,
				GenericMethodArity = genericMethodArity
			};

			if (!string.IsNullOrEmpty(ilOffsetStr))
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
