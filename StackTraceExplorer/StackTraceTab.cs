using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ICSharpCode.ILSpy;

using StackTraceExplorer.Parsers;

using TomsToolbox.Wpf;

namespace StackTraceExplorer
{
	/// <summary>
	/// Represents a single tab in the Stack Trace Explorer, containing one parsed stack trace.
	/// </summary>
	public class StackTraceTab : ObservableObjectBase
	{
		private readonly MethodResolver methodResolver;
		private readonly CompositeParser parser;
		private readonly Action<StackTraceTab> closeTabAction;

		private string title = "Stack Trace";
		private string inputText = string.Empty;
		private StackFrame? selectedFrame;
		private string statusText = "Ready";
		private bool isProcessing;

		public StackTraceTab(MethodResolver methodResolver, Action<StackTraceTab> closeTabAction)
		{
			this.methodResolver = methodResolver;
			this.parser = new CompositeParser();
			this.closeTabAction = closeTabAction;

			CloseCommand = new DelegateCommand(() => closeTabAction(this));
		}

		#region Properties

		public ObservableCollection<StackFrame> Frames { get; } = new();

		public string Title {
			get => title;
			set => SetProperty(ref title, value);
		}

		public string InputText {
			get => inputText;
			set {
				if (SetProperty(ref inputText, value ?? string.Empty))
				{
					// Auto-parse on text change
					ParseInputAsync().HandleExceptions();
				}
			}
		}

		public StackFrame? SelectedFrame {
			get => selectedFrame;
			set => SetProperty(ref selectedFrame, value);
		}

		public string StatusText {
			get => statusText;
			set => SetProperty(ref statusText, value ?? string.Empty);
		}

		public ICommand CloseCommand { get; }

		#endregion

		#region Parsing and Resolution

		private async Task ParseInputAsync()
		{
			if (isProcessing)
				return;

			if (string.IsNullOrWhiteSpace(inputText))
			{
				Frames.Clear();
				StatusText = "Ready";
				Title = "Stack Trace";
				return;
			}

			isProcessing = true;
			StatusText = "Parsing...";

			try
			{
				var parsedFrames = parser.Parse(inputText);

				// Clear and re-add frames
				Frames.Clear();
				foreach (var frame in parsedFrames)
				{
					Frames.Add(frame);
				}

				// Generate title from first method frame
				UpdateTitle();

				// Resolve methods in background
				await ResolveFramesAsync();

				UpdateStatus();
			}
			finally
			{
				isProcessing = false;
			}
		}

		private void UpdateTitle()
		{
			var firstMethod = Frames.FirstOrDefault(f => f.IsMethodFrame);
			if (firstMethod != null)
			{
				// Use method name or shortened type.method
				var methodName = firstMethod.MethodName ?? "";
				if (!string.IsNullOrEmpty(firstMethod.FullTypeName))
				{
					var typeName = firstMethod.FullTypeName;
					var lastDot = typeName.LastIndexOf('.');
					if (lastDot > 0)
					{
						var className = typeName.Substring(lastDot + 1);
						// Handle nested/compiler-generated types
						var plusIndex = className.IndexOf('+');
						if (plusIndex > 0)
							className = className.Substring(0, plusIndex);
						if (!className.StartsWith("<"))
							methodName = className + "." + methodName;
					}
				}

				// Truncate if too long
				if (methodName.Length > 30)
					methodName = methodName.Substring(0, 27) + "...";

				Title = methodName;
			}
			else
			{
				Title = "Stack Trace";
			}
		}

		public async Task ResolveFramesAsync()
		{
			StatusText = "Resolving methods...";

			foreach (var frame in Frames.Where(f => f.IsMethodFrame))
			{
				try
				{
					frame.ResolvedMethod = await methodResolver.ResolveAsync(frame);
					frame.IsResolved = true;
				}
				catch (Exception ex)
				{
					frame.ResolutionError = ex.Message;
					frame.IsResolved = true; // Mark as resolved even on error
				}
			}

			UpdateStatus();
		}

		private void UpdateStatus()
		{
			var methodFrames = Frames.Where(f => f.IsMethodFrame).ToList();
			var resolvedCount = methodFrames.Count(f => f.CanNavigate);
			var totalFrames = Frames.Count;
			var methodCount = methodFrames.Count;

			if (totalFrames == 0)
			{
				StatusText = "Ready";
			}
			else
			{
				StatusText = $"{resolvedCount}/{methodCount} methods resolved, {totalFrames} total frames";
			}
		}

		#endregion

		/// <summary>
		/// Simple delegate command implementation.
		/// </summary>
		private class DelegateCommand : ICommand
		{
			private readonly Action execute;

			public DelegateCommand(Action execute)
			{
				this.execute = execute;
			}

			public event EventHandler? CanExecuteChanged;

			public bool CanExecute(object? parameter) => true;

			public void Execute(object? parameter) => execute();
		}
	}
}
