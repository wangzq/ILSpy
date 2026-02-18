using System.Composition;
using System.Reflection;

using ICSharpCode.ILSpy;

namespace StackTraceExplorer
{
	/// <summary>
	/// Menu command to show the Stack Trace Explorer pane.
	/// </summary>
	[ExportMainMenuCommand(
		ParentMenuID = "_View",
		Header = "Stack Trace E_xplorer (v1.0.20)",
		MenuCategory = "View",
		MenuOrder = 3000)]
	[Shared]
	public class ShowStackTraceExplorerCommand : SimpleCommand
	{
		public override void Execute(object? parameter)
		{
			var model = App.ExportProvider.GetExportedValue<StackTraceExplorerModel>();
			model.Show();
		}
	}
}
