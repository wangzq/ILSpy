namespace StackTraceExplorer.Parsers
{
	/// <summary>
	/// Interface for stack trace parsers.
	/// </summary>
	public interface IStackTraceParser
	{
		/// <summary>
		/// Gets the priority of this parser. Higher values are tried first.
		/// </summary>
		int Priority { get; }

		/// <summary>
		/// Determines if this parser can parse the given line.
		/// </summary>
		bool CanParse(string line);

		/// <summary>
		/// Parses a stack trace line into a StackFrame.
		/// </summary>
		StackFrame? Parse(string line);
	}
}
