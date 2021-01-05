using System;
using System.Globalization;

namespace unvell.ReoGrid.DataFormat
{
	#region Percent
	/// <summary>
	/// Percent data formatter
	/// </summary>
	public class PercentDataFormatter : IDataFormatter
	{
		/// <summary>
		/// Format specified cell
		/// </summary>
		/// <param name="cell">cell to be formatted</param>
		/// <param name="culture">culture for parsing</param>
		/// <returns>Formatted text used to display as cell content</returns>
		public string FormatCell(Cell cell, CultureInfo culture)
		{
			object data = cell.InnerData;

			double percent = 0;
			bool isFormat = false;

			if (data is double)
			{
				percent = (double)data;
				isFormat = true;
			}
			else if (data is DateTime dt)
			{
				percent = dt.ToOADate();
				isFormat = true;
			}
			else
			{
				string str = Convert.ToString(data);
				if (str.Length > 1 && str.EndsWith("%"))
				{
					// string ends with "%"
					str = str.Substring(0, str.Length - 1);
					isFormat = double.TryParse(str, NumberStyles.Any, culture, out percent);
					percent /= 100d;
				}
				else
				{
					// string ends without "%"
					isFormat = double.TryParse(str, NumberStyles.Any, culture, out percent);
				}

				if (isFormat) cell.InnerData = percent;
			}

			if (isFormat)
			{
				var arg = cell.DataFormatArgs as NumberDataFormatter.INumberFormatArgs;
				var format = NumberDataFormatter.FormatNumberCellAndGetPattern(cell, ref percent, arg);
				return percent.ToString(format + "%");
			}

			return null;
		}

		/// <summary>
		/// Perform a format check
		/// </summary>
		/// <returns>true if the data is in this format</returns>
		public bool PerformTestFormat()
		{
			return true;
		}
	}
	#endregion // Percent

}
