using System.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using TomsToolbox.Wpf.Composition.AttributedModel;

namespace StackTraceExplorer
{
	/// <summary>
	/// Interaction logic for StackTraceExplorerView.xaml
	/// </summary>
	[DataTemplate(typeof(StackTraceExplorerModel))]
	[NonShared]
	public partial class StackTraceExplorerView : UserControl
	{
		public StackTraceExplorerView()
		{
			InitializeComponent();
			Loaded += OnLoaded;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			if (DataContext is StackTraceExplorerModel model)
			{
				model.LoadCachedInput();
			}
		}

		private void OnTabHeaderClick(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement element && element.DataContext is StackTraceTab tab)
			{
				if (DataContext is StackTraceExplorerModel model)
				{
					model.SelectedTab = tab;
				}
			}
		}

		private void OnTabHeaderMouseDown(object sender, MouseButtonEventArgs e)
		{
			// Middle-click to close tab
			if (e.MiddleButton == MouseButtonState.Pressed)
			{
				if (sender is FrameworkElement element && element.DataContext is StackTraceTab tab)
				{
					tab.CloseCommand.Execute(null);
					e.Handled = true;
				}
			}
		}

		private void OnTabCloseClick(object sender, RoutedEventArgs e)
		{
			if (sender is FrameworkElement element && element.DataContext is StackTraceTab tab)
			{
				tab.CloseCommand.Execute(null);
			}
		}

		private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Single-click only navigates for resolved frames
			if (DataContext is StackTraceExplorerModel model && model.SelectedTab?.SelectedFrame?.CanNavigate == true)
			{
				model.NavigateToFrame(model.SelectedTab.SelectedFrame);
			}
		}

		private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
		{
			// Double-click triggers search for unresolved frames
			if (DataContext is StackTraceExplorerModel model && model.SelectedTab?.SelectedFrame != null)
			{
				var frame = model.SelectedTab.SelectedFrame;
				if (frame.IsMethodFrame && !frame.CanNavigate)
				{
					model.NavigateToFrame(frame); // This will trigger SearchForFrame
					e.Handled = true;
				}
			}
		}

		private void OnListKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && DataContext is StackTraceExplorerModel model)
			{
				var frame = model.SelectedTab?.SelectedFrame;
				if (frame != null && frame.IsMethodFrame)
				{
					model.NavigateToFrame(frame);
					e.Handled = true;
				}
			}
		}
	}
}
