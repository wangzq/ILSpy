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

		// Matches WinDbg runtime frames: [GCFrame], [HelperMethodFrame], [PrestubMethodFrame], etc.
		// Also matches frames with addresses like [GCFrame: 0x00000027a658da48]
		private static readonly Regex GCFramePattern = new Regex(
			@"^\s*(?:[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+)?\[(GCFrame|HelperMethodFrame|PrestubMethodFrame|DebuggerU2MCatchHandlerFrame).*\]",
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
			// Check for bracket markers like [GCFrame], [HelperMethodFrame], etc.
			if (trimmed.StartsWith("[") || trimmed.StartsWith("---"))
				return true;
			// Check for WinDbg !clrstack runtime frames with hex address prefix
			// Format: HexAddr HexAddr [FrameType: HexAddr]
			if (GCFramePattern.IsMatch(trimmed))
				return true;
			if (ExceptionHeaderPattern.IsMatch(trimmed))
				return true;
			return false;
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
