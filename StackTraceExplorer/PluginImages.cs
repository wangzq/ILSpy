using System;
using System.Windows;
using System.Windows.Media;

namespace StackTraceExplorer
{
	/// <summary>
	/// Provides access to ILSpy's standard icons for use in the plugin.
	/// </summary>
	public static class PluginImages
	{
		/// <summary>
		/// Copy/Clipboard icon (used for Paste button).
		/// </summary>
		public static ImageSource Copy { get; } = LoadFromILSpy("Images/Copy");

		/// <summary>
		/// Open/Folder icon (used for Open button).
		/// </summary>
		public static ImageSource Open { get; } = LoadFromILSpy("Images/Open");

		private static ImageSource LoadFromILSpy(string name)
		{
			var uri = new Uri("/" + name + ".xaml", UriKind.Relative);
			var drawing = (Drawing)Application.LoadComponent(uri);
			var image = new DrawingImage(drawing);
			if (image.CanFreeze)
			{
				image.Freeze();
			}
			return image;
		}
	}
}
