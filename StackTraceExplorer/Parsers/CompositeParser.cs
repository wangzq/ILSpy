using System;
using System.Collections.Generic;
using System.Linq;

namespace StackTraceExplorer.Parsers
{
	/// <summary>
	/// Combines multiple parsers to handle various stack trace formats.
	/// </summary>
	public class CompositeParser
	{
		private readonly IStackTraceParser[] parsers;

		public CompositeParser()
		{
			// Initialize parsers, sorted by priority (highest first)
			parsers = new IStackTraceParser[]
			{
				new SpecialMarkersParser(),
				new WinDbgParser(),
				new StandardNetParser()
			}.OrderByDescending(p => p.Priority).ToArray();
		}

		/// <summary>
		/// Parses a complete stack trace text into individual frames.
		/// </summary>
		public IReadOnlyList<StackFrame> Parse(string? stackTraceText)
		{
			var frames = new List<StackFrame>();

			if (string.IsNullOrWhiteSpace(stackTraceText))
				return frames;

			var lines = stackTraceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var line in lines)
			{
				var frame = ParseLine(line);
				if (frame != null)
				{
					frames.Add(frame);
				}
			}

			return frames;
		}

		/// <summary>
		/// Parses a single line using the appropriate parser.
		/// </summary>
		public StackFrame? ParseLine(string line)
		{
			if (string.IsNullOrWhiteSpace(line))
				return null;

			// Skip lines that are just numbers (line numbers from pasted text)
			if (int.TryParse(line.Trim(), out _))
				return null;

			// Skip lines that look like WinDbg prompts
			if (line.TrimStart().StartsWith("0:") && line.Contains(">"))
			{
				// This might be "0:366> !clrstack" - skip it
				if (!line.Contains("!") || !line.Contains("("))
					return null;
			}

			// Try each parser in priority order
			foreach (var parser in parsers)
			{
				if (parser.CanParse(line))
				{
					var frame = parser.Parse(line);
					if (frame != null)
						return frame;
				}
			}

			// If no parser matched but the line looks like it might be relevant,
			// return an unknown frame
			var trimmed = line.Trim();
			if (trimmed.Length > 0 && !IsIgnorableLine(trimmed))
			{
				return new StackFrame {
					FrameType = StackFrameType.Unknown,
					RawText = line
				};
			}

			return null;
		}

		private bool IsIgnorableLine(string line)
		{
			// Lines to ignore
			return line.StartsWith("OS Thread Id:", StringComparison.OrdinalIgnoreCase)
				|| line.StartsWith("Child SP", StringComparison.OrdinalIgnoreCase)
				|| line.StartsWith("IP Call Site", StringComparison.OrdinalIgnoreCase)
				|| line.StartsWith("GetLastError", StringComparison.OrdinalIgnoreCase)
				|| line.All(c => c == '-' || c == '=' || char.IsWhiteSpace(c))
				|| string.IsNullOrWhiteSpace(line);
		}
	}
}
