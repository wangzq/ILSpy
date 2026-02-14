using System.Text.RegularExpressions;

namespace StackTraceExplorer.Parsers
{
	/// <summary>
	/// Parses special stack trace markers like transitions and exception boundaries.
	/// </summary>
	public class SpecialMarkersParser : IStackTraceParser
	{
		private static readonly Regex NativeTransitionPattern = new Regex(
			@"^\s*\[Native to Managed Transition\]",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex ManagedTransitionPattern = new Regex(
			@"^\s*\[Managed to Native Transition\]",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex InnerExceptionPattern = new Regex(
			@"^\s*---\s*End of inner exception stack trace\s*---",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex GCFramePattern = new Regex(
			@"^\s*\[(GCFrame|HelperMethodFrame).*\]",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex ExceptionHeaderPattern = new Regex(
			@"^\s*(?<exType>[A-Za-z0-9_.]+Exception):\s*(?<message>.*)$",
			RegexOptions.Compiled);

		private static readonly Regex AppDomainTransitionPattern = new Regex(
			@"^\s*\[AppDomain Transition\]",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public int Priority => 100; // Highest priority to catch markers first

		public bool CanParse(string line)
		{
			var trimmed = line.Trim();
			return trimmed.StartsWith("[")
				|| trimmed.StartsWith("---")
				|| ExceptionHeaderPattern.IsMatch(trimmed);
		}

		public StackFrame? Parse(string line)
		{
			if (NativeTransitionPattern.IsMatch(line))
			{
				return new StackFrame {
					FrameType = StackFrameType.NativeTransition,
					RawText = line
				};
			}

			if (ManagedTransitionPattern.IsMatch(line))
			{
				return new StackFrame {
					FrameType = StackFrameType.ManagedTransition,
					RawText = line
				};
			}

			if (AppDomainTransitionPattern.IsMatch(line))
			{
				return new StackFrame {
					FrameType = StackFrameType.NativeTransition, // Treat as transition
					RawText = line
				};
			}

			if (GCFramePattern.IsMatch(line))
			{
				return new StackFrame {
					FrameType = StackFrameType.NativeTransition, // Treat GCFrame as transition
					RawText = line
				};
			}

			if (InnerExceptionPattern.IsMatch(line))
			{
				return new StackFrame {
					FrameType = StackFrameType.InnerExceptionBoundary,
					RawText = line
				};
			}

			var exMatch = ExceptionHeaderPattern.Match(line);
			if (exMatch.Success)
			{
				return new StackFrame {
					FrameType = StackFrameType.ExceptionHeader,
					RawText = line,
					FullTypeName = exMatch.Groups["exType"].Value
				};
			}

			return null;
		}
	}
}
