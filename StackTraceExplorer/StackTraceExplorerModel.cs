using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using ICSharpCode.ILSpy;
using Microsoft.Win32;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.Search;
using ICSharpCode.ILSpy.Util;
using ICSharpCode.ILSpy.ViewModels;

using TomsToolbox.Wpf;

using DelegateCommand = ICSharpCode.ILSpy.Commands.DelegateCommand;

namespace StackTraceExplorer
{
	/// <summary>
	/// ViewModel for the Stack Trace Explorer tool pane.
	/// </summary>
	[ExportToolPane]
	[Shared]
	public class StackTraceExplorerModel : ToolPaneModel
	{
		public const string PaneContentId = "stackTraceExplorerPane";

		private static readonly string CacheFilePath = Path.Combine(
			Path.GetTempPath(), "ILSpy_StackTraceExplorer_LastInput.txt");

		private readonly AssemblyTreeModel assemblyTreeModel;
		private readonly MethodResolver methodResolver;

		private StackTraceTab? selectedTab;
		private bool isInitialized;

		public StackTraceExplorerModel(AssemblyTreeModel assemblyTreeModel)
		{
			this.assemblyTreeModel = assemblyTreeModel;
			this.methodResolver = new MethodResolver(assemblyTreeModel);

			ContentId = PaneContentId;
			Title = "Stack Trace Explorer";
			ShortcutKey = new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift);
			IsCloseable = true;

			PasteCommand = new DelegateCommand(ExecutePaste);
			OpenCommand = new DelegateCommand(ExecuteOpen);

			// Re-resolve when assemblies change
			MessageBus<CurrentAssemblyListChangedEventArgs>.Subscribers += OnAssemblyListChanged_Handler;
		}

		#region Properties

		public ObservableCollection<StackTraceTab> Tabs { get; } = new();

		public StackTraceTab? SelectedTab {
			get => selectedTab;
			set => SetProperty(ref selectedTab, value);
		}

		#endregion

		#region Commands

		public ICommand PasteCommand { get; }
		public ICommand OpenCommand { get; }

		private void ExecutePaste()
		{
			try
			{
				if (Clipboard.ContainsText())
				{
					var text = Clipboard.GetText();
					if (!string.IsNullOrWhiteSpace(text))
					{
						CreateNewTab(text);
					}
				}
			}
			catch
			{
				// Clipboard operations can fail
			}
		}

		private void ExecuteOpen()
		{
			var dialog = new OpenFileDialog {
				Title = "Open Stack Trace Files",
				Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log|All Files (*.*)|*.*",
				Multiselect = true
			};

			if (dialog.ShowDialog() == true)
			{
				foreach (var fileName in dialog.FileNames)
				{
					try
					{
						var text = File.ReadAllText(fileName);
						if (!string.IsNullOrWhiteSpace(text))
						{
							var tab = CreateNewTab(text);
							// Use file name as tab title
							tab.Title = Path.GetFileName(fileName);
						}
					}
					catch
					{
						// Ignore errors reading individual files
					}
				}
			}
		}

		#endregion

		#region Tab Management

		private StackTraceTab CreateNewTab(string? inputText = null)
		{
			var tab = new StackTraceTab(methodResolver, CloseTab);
			Tabs.Add(tab);
			SelectedTab = tab;

			if (!string.IsNullOrWhiteSpace(inputText))
			{
				tab.InputText = inputText;
			}

			return tab;
		}

		private void CloseTab(StackTraceTab tab)
		{
			var index = Tabs.IndexOf(tab);
			Tabs.Remove(tab);

			// Select an adjacent tab if available
			if (Tabs.Count > 0)
			{
				if (index >= Tabs.Count)
					index = Tabs.Count - 1;
				SelectedTab = Tabs[index];
			}
			else
			{
				SelectedTab = null;
			}
		}

		#endregion

		#region Persistence

		/// <summary>
		/// Loads the last used stack trace from the cache file.
		/// Called when the view becomes visible for the first time.
		/// </summary>
		public void LoadCachedInput()
		{
			if (isInitialized)
				return;

			isInitialized = true;

			try
			{
				if (File.Exists(CacheFilePath))
				{
					var cached = File.ReadAllText(CacheFilePath);
					if (!string.IsNullOrWhiteSpace(cached))
					{
						CreateNewTab(cached);
						return;
					}
				}
			}
			catch
			{
				// Ignore errors loading cache
			}

			// Create an empty tab if no cached content
			if (Tabs.Count == 0)
			{
				CreateNewTab();
			}
		}

		/// <summary>
		/// Saves the current tab's input to cache for persistence.
		/// </summary>
		public void SaveCurrentTabToCache()
		{
			try
			{
				var text = SelectedTab?.InputText;
				if (string.IsNullOrWhiteSpace(text))
				{
					if (File.Exists(CacheFilePath))
						File.Delete(CacheFilePath);
				}
				else
				{
					File.WriteAllText(CacheFilePath, text);
				}
			}
			catch
			{
				// Ignore errors saving cache
			}
		}

		#endregion

		#region Assembly Changes

		private void OnAssemblyListChanged_Handler(object? sender, CurrentAssemblyListChangedEventArgs e)
		{
			OnAssemblyListChanged();
		}

		private async void OnAssemblyListChanged()
		{
			// Re-resolve frames in all tabs when assemblies change
			foreach (var tab in Tabs)
			{
				if (tab.Frames.Count > 0)
				{
					await tab.ResolveFramesAsync();
				}
			}
		}

		#endregion

		#region Navigation

		/// <summary>
		/// Navigates to the selected frame. If the method is resolved, navigates directly.
		/// If not resolved, opens the search pane with the method name.
		/// </summary>
		public void NavigateToFrame(StackFrame? frame)
		{
			if (frame == null || !frame.IsMethodFrame)
				return;

			if (frame.CanNavigate && frame.ResolvedMethod != null)
			{
				// Method is resolved - navigate directly
				var node = assemblyTreeModel.FindTreeNode(frame.ResolvedMethod);
				if (node != null)
				{
					assemblyTreeModel.SelectNode(node);
				}
				else
				{
					// Fallback: try MessageBus navigation
					MessageBus.Send(this, new NavigateToReferenceEventArgs(frame.ResolvedMethod));
				}
			}
			else
			{
				// Method not resolved - send to search
				SearchForFrame(frame);
			}
		}

		// Pattern to strip generic type arguments like <bool>, <System.Guid>, etc.
		private static readonly System.Text.RegularExpressions.Regex GenericTypeArgsPattern =
			new System.Text.RegularExpressions.Regex(@"<[^<>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

		private void SearchForFrame(StackFrame frame)
		{
			// Build a search term from the frame
			var searchTerm = frame.MethodName ?? "";

			// Strip generic type arguments from method name (e.g., Execute<bool> -> Execute)
			searchTerm = GenericTypeArgsPattern.Replace(searchTerm, "");

			// If we have type name, use just the class name (last part before method)
			if (!string.IsNullOrEmpty(frame.FullTypeName))
			{
				var typeName = frame.FullTypeName;
				// Strip generic type arguments from type name too
				typeName = GenericTypeArgsPattern.Replace(typeName, "");

				var lastDot = typeName.LastIndexOf('.');
				if (lastDot > 0)
				{
					// Get just the class name
					var className = typeName.Substring(lastDot + 1);
					// Handle nested types
					var plusIndex = className.IndexOf('+');
					if (plusIndex > 0)
						className = className.Substring(plusIndex + 1);
					// Handle compiler-generated types - but preserve method names like <Execute>
					if (!className.StartsWith("<"))
						searchTerm = className + "." + searchTerm;
				}
				else
				{
					searchTerm = typeName + "." + searchTerm;
				}
			}

			// Remove compiler-generated markers for search
			if (searchTerm.Contains("<>"))
			{
				// For lambdas like <>c__DisplayClass5_0.<Execute>b__0, search for Execute
				var match = System.Text.RegularExpressions.Regex.Match(searchTerm, @"<(\w+)>");
				if (match.Success)
					searchTerm = match.Groups[1].Value;
			}

			// Show search pane and set search term
			try
			{
				var dockWorkspace = App.ExportProvider.GetExportedValue<DockWorkspace>();
				dockWorkspace.ShowToolPane(SearchPaneModel.PaneContentId);

				var searchModel = App.ExportProvider.GetExportedValue<SearchPaneModel>();
				searchModel.SearchTerm = searchTerm;
			}
			catch
			{
				// Ignore errors opening search
			}
		}

		#endregion
	}
}
