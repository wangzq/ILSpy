using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace StackTraceExplorer.Parsers
{
	/// <summary>
	/// Parses WinDbg !clrstack output format.
	/// Examples:
	///   00000027A658D6E0 00007ff8c35869ad System.Reflection.Assembly.LoadFile(System.String)
	///   00000027A667B2A0 00007ff865ea06de Microsoft.Identity.Client.Internal.Requests.RequestBase+d__11.MoveNext()
	///   00000027A667B150 00007ff8c2d82c43 System.Lazy`1[[System.__Canon, mscorlib]].CreateValue()
	/// </summary>
	public class ClrStackParser : IStackTraceParser
	{
		// Pattern: "HexAddr HexAddr FullMethod(params)"
		// Captures the method signature after two hex addresses
		private static readonly Regex ClrStackPattern = new Regex(
			@"^(?<sp>[0-9A-Fa-f]{8,16})\s+(?<ip>[0-9A-Fa-f]{8,16})\s+(?<fullMethod>.+?)\((?<params>.*)\)\s*$",
			RegexOptions.Compiled);

		// Pattern to detect async state machine: +d__N.MoveNext or <MethodName>d__N.MoveNext
		private static readonly Regex AsyncPattern = new Regex(
			@"(?:\+d__\d+\.MoveNext$|<\w+>d__\d+\.MoveNext$)",
			RegexOptions.Compiled);

		// Pattern to detect lambda/closure: <>c__DisplayClass or <>c.<Method>b__N or +c__DisplayClass
		private static readonly Regex LambdaPattern = new Regex(
			@"(?:<>c|[+.]c)(__DisplayClass\d+_\d+)?\.?(?:<\w+>b__\d+)?",
			RegexOptions.Compiled);

		// Pattern to extract generic method arguments: Method[T] or Method<T>
		private static readonly Regex GenericMethodPattern = new Regex(
			@"^(?<name>\w+)(?:\[(?<args>[^\]]+)\]|<(?<args>[^>]+)>)$",
			RegexOptions.Compiled);

		// Pattern to strip generic type info: Type`N[[...]] or Type`N
		private static readonly Regex GenericTypePattern = new Regex(
			@"`\d+(?:\[\[.*?\]\])?",
			RegexOptions.Compiled);

		public int Priority => 30; // Higher priority than WatsonCrashParser (25)

		public bool CanParse(string line)
		{
			// Must start with a hex address (8-16 hex digits) followed by another hex address
			var trimmed = line.Trim();
			if (trimmed.Length < 20)
				return false;

			// Quick check: first 8+ chars should be hex, then space, then another hex
			int firstSpace = trimmed.IndexOf(' ');
			if (firstSpace < 8 || firstSpace > 16)
				return false;

			// Check if first part is hex
			var firstPart = trimmed.Substring(0, firstSpace);
			if (!IsHexString(firstPart))
				return false;

			// Check if second part starts with hex
			var rest = trimmed.Substring(firstSpace).TrimStart();
			int secondSpace = rest.IndexOf(' ');
			if (secondSpace < 8)
				return false;

			var secondPart = rest.Substring(0, secondSpace);
			if (!IsHexString(secondPart))
				return false;

			// Don't match runtime frames like [GCFrame: ...]
			var methodPart = rest.Substring(secondSpace).TrimStart();
			if (methodPart.StartsWith("["))
				return false;

			return true;
		}

		private static bool IsHexString(string s)
		{
			foreach (char c in s)
			{
				if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
					return false;
			}
			return true;
		}

		public StackFrame? Parse(string line)
		{
			var match = ClrStackPattern.Match(line.Trim());
			if (!match.Success)
				return null;

			var fullMethod = match.Groups["fullMethod"].Value.Trim();
			var parameters = match.Groups["params"].Value.Trim();

			// Clean up generic type notation: Type`1[[System.__Canon, mscorlib]] -> Type
			fullMethod = GenericTypePattern.Replace(fullMethod, "");

			// Handle nested class notation with + (e.g., OuterClass+NestedClass.Method)
			// Convert + to . for consistency
			fullMethod = fullMethod.Replace("+", ".");

			// Split fullMethod into type and method
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
				genericMethodArity = 1 + args.Count(c => c == ',');
			}

			// Determine frame type
			var frameType = StackFrameType.Method;
			if (AsyncPattern.IsMatch(match.Groups["fullMethod"].Value))
			{
				frameType = StackFrameType.AsyncStateMachine;
			}
			else if (LambdaPattern.IsMatch(match.Groups["fullMethod"].Value))
			{
				frameType = StackFrameType.Lambda;
			}

			return new StackFrame {
				FrameType = frameType,
				RawText = line,
				FullTypeName = fullTypeName,
				MethodName = methodName,
				Parameters = parameters,
				GenericMethodArity = genericMethodArity
			};
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
