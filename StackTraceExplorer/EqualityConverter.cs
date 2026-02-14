using System;
using System.Globalization;
using System.Windows.Data;

namespace StackTraceExplorer
{
	/// <summary>
	/// Converter that returns true if two values are equal (by reference).
	/// Used for determining if a tab is the selected tab.
	/// </summary>
	public class EqualityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values == null || values.Length < 2)
				return false;

			return ReferenceEquals(values[0], values[1]);
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
