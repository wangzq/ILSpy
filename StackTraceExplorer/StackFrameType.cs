namespace StackTraceExplorer
{
	/// <summary>
	/// Represents the type of a stack frame entry.
	/// </summary>
	public enum StackFrameType
	{
		/// <summary>
		/// A regular method frame.
		/// </summary>
		Method,

		/// <summary>
		/// An async state machine method (MoveNext).
		/// </summary>
		AsyncStateMachine,

		/// <summary>
		/// A lambda or closure method.
		/// </summary>
		Lambda,

		/// <summary>
		/// A [Native to Managed Transition] marker.
		/// </summary>
		NativeTransition,

		/// <summary>
		/// A [Managed to Native Transition] marker.
		/// </summary>
		ManagedTransition,

		/// <summary>
		/// An inner exception boundary marker.
		/// </summary>
		InnerExceptionBoundary,

		/// <summary>
		/// An exception type and message header.
		/// </summary>
		ExceptionHeader,

		/// <summary>
		/// A line that could not be parsed.
		/// </summary>
		Unknown
	}
}
