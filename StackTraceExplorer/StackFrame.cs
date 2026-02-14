using System.Text.RegularExpressions;

using ICSharpCode.Decompiler.TypeSystem;

using TomsToolbox.Wpf;

namespace StackTraceExplorer
{
	/// <summary>
	/// Represents a parsed stack frame entry.
	/// </summary>
	public class StackFrame : ObservableObject
	{
		private static readonly Regex AsyncStateMachinePattern = new Regex(
			@"<(?<method>\w+)>d__\d+\.MoveNext",
			RegexOptions.Compiled);

		private static readonly Regex LambdaPattern = new Regex(
			@"<>c(__DisplayClass\d+_\d+)?\.(<(?<method>\w+)>b__\d+|<\w+>)",
			RegexOptions.Compiled);

		private StackFrameType frameType;
		private string rawText = string.Empty;
		private string? assemblyName;
		private string? fullTypeName;
		private string? methodName;
		private string? parameters;
		private int? ilOffset;
		private int genericMethodArity;
		private bool isResolved;
		private IMethod? resolvedMethod;
		private string? resolutionError;

		/// <summary>
		/// Gets or sets the type of this stack frame.
		/// </summary>
		public StackFrameType FrameType {
			get => frameType;
			set => SetProperty(ref frameType, value);
		}

		/// <summary>
		/// Gets or sets the raw text of the stack frame line.
		/// </summary>
		public string RawText {
			get => rawText;
			set => SetProperty(ref rawText, value ?? string.Empty);
		}

		/// <summary>
		/// Gets or sets the assembly name (e.g., "Microsoft.Crm.Core.dll").
		/// </summary>
		public string? AssemblyName {
			get => assemblyName;
			set => SetProperty(ref assemblyName, value);
		}

		/// <summary>
		/// Gets or sets the full type name (e.g., "Microsoft.Crm.CrmDataReader").
		/// </summary>
		public string? FullTypeName {
			get => fullTypeName;
			set => SetProperty(ref fullTypeName, value);
		}

		/// <summary>
		/// Gets or sets the method name (e.g., "GetString").
		/// </summary>
		public string? MethodName {
			get => methodName;
			set => SetProperty(ref methodName, value);
		}

		/// <summary>
		/// Gets or sets the method parameters (e.g., "int i").
		/// </summary>
		public string? Parameters {
			get => parameters;
			set => SetProperty(ref parameters, value);
		}

		/// <summary>
		/// Gets or sets the IL offset if available.
		/// </summary>
		public int? ILOffset {
			get => ilOffset;
			set => SetProperty(ref ilOffset, value);
		}

		/// <summary>
		/// Gets or sets the generic method arity (number of type parameters).
		/// </summary>
		public int GenericMethodArity {
			get => genericMethodArity;
			set => SetProperty(ref genericMethodArity, value);
		}

		/// <summary>
		/// Gets or sets whether this frame has been resolved to a method.
		/// </summary>
		public bool IsResolved {
			get => isResolved;
			set {
				if (SetProperty(ref isResolved, value))
				{
					OnPropertyChanged(nameof(CanNavigate));
				}
			}
		}

		/// <summary>
		/// Gets or sets the resolved method.
		/// </summary>
		public IMethod? ResolvedMethod {
			get => resolvedMethod;
			set {
				if (SetProperty(ref resolvedMethod, value))
				{
					OnPropertyChanged(nameof(CanNavigate));
				}
			}
		}

		/// <summary>
		/// Gets or sets any error that occurred during resolution.
		/// </summary>
		public string? ResolutionError {
			get => resolutionError;
			set => SetProperty(ref resolutionError, value);
		}

		/// <summary>
		/// Gets whether this frame can be navigated to.
		/// </summary>
		public bool CanNavigate => IsResolved && ResolvedMethod != null;

		/// <summary>
		/// Gets whether this frame represents a method (navigable type).
		/// </summary>
		public bool IsMethodFrame => FrameType == StackFrameType.Method
			|| FrameType == StackFrameType.AsyncStateMachine
			|| FrameType == StackFrameType.Lambda;

		/// <summary>
		/// Gets the display text for this frame.
		/// </summary>
		public string DisplayText {
			get {
				return FrameType switch {
					StackFrameType.NativeTransition => "[Native to Managed Transition]",
					StackFrameType.ManagedTransition => "[Managed to Native Transition]",
					StackFrameType.InnerExceptionBoundary => "--- End of inner exception stack trace ---",
					StackFrameType.ExceptionHeader => RawText,
					StackFrameType.Unknown => RawText,
					_ => FormatMethodDisplay()
				};
			}
		}

		/// <summary>
		/// Gets the original method name for compiler-generated methods.
		/// </summary>
		public string? OriginalMethodName {
			get {
				if (MethodName == null)
					return null;

				// Check for async state machine pattern: <MethodName>d__1.MoveNext
				var asyncMatch = AsyncStateMachinePattern.Match(MethodName);
				if (asyncMatch.Success)
					return asyncMatch.Groups["method"].Value;

				// Check for lambda pattern: <>c__DisplayClass5_0.<Execute>b__0
				var lambdaMatch = LambdaPattern.Match(MethodName);
				if (lambdaMatch.Success && lambdaMatch.Groups["method"].Success)
					return lambdaMatch.Groups["method"].Value;

				return MethodName;
			}
		}

		private string FormatMethodDisplay()
		{
			// Return the original raw text to preserve generic type arguments and other formatting
			// The raw text shows exactly what the user pasted (e.g., "Execute<bool>(...)")
			// rather than the parsed/stripped version (e.g., "Execute(...)")
			if (!string.IsNullOrEmpty(RawText))
				return RawText.TrimStart();

			// Fallback to constructed display if no raw text
			var display = $"{FullTypeName}.{MethodName}({Parameters ?? ""})";

			if (FrameType == StackFrameType.AsyncStateMachine)
				display += " [async]";
			else if (FrameType == StackFrameType.Lambda)
				display += " [lambda]";

			return display;
		}
	}
}
