using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using unvell.ReoGrid.DataFormat;

namespace unvell.ReoGrid.DataFormat
{
	#region Percent
	/// <summary>
	/// Percent data formatter
	/// </summary>
	public class PercentDataFormatter : IDataFormatter
	{
		public string FormatCell(Cell cell)
		{
			object data = cell.InnerData;

			double percent = 0;
			bool isFormat = false;
			short digits = 0;

			if (data is double)
			{
				percent = (double)data;
				isFormat = true;
				digits = 9;
			}
			else if (data is DateTime dt)
			{
				percent = dt.ToOADate();
				isFormat = true;
				digits = 0;
			}
			else
			{
				string str = Convert.ToString(data);
				if (str.Length > 1 && str.EndsWith("%"))
				{
					// string ends with "%"
					str = str.Substring(0, str.Length - 1);

					isFormat = double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out percent);

					if (isFormat)
					{
						percent /= 100d;

						int decimalDigits = str.LastIndexOf(Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
						if (decimalDigits >= 0)
						{
							digits = (short)(str.Length - 1 - decimalDigits);
						}
					}
				}
				else
				{
					// string ends without "%"
					isFormat = double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out percent);

					if (isFormat)
					{
						int decimalDigits = str.LastIndexOf(Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
						if (decimalDigits >= 0)
						{
							digits = (short)(str.Length - 1 - decimalDigits);
						}
					}
					else
					{
						// try convert from datetime
						DateTime date;
						if (DateTime.TryParse(str, out date))
						{
							percent = date.ToOADate();
							isFormat = true;
						}
					}
				}

				if (isFormat) cell.InnerData = percent;
			}

			if (isFormat)
			{
				var format = NumberDataFormatter.FormatNumberCellAndGetPattern(cell, ref percent,
					cell.DataFormatArgs as NumberDataFormatter.INumberFormatArgs);

				return percent.ToString(format + "%");
			}

			return null;
		}


		//[Serializable]
		[Obsolete("use NumberDataFormatter.NumberFormatArgs instead")]
		public struct PercentFormatArgs
		{
			///// <summary>
			///// Get or set the decimal places
			///// </summary>
			//public short DecimalPlaces { get; set; }

			///// <summary>
			///// Determine whether or not to display the number using decimal mark
			///// </summary>
			//public bool UseSeparator { get; set; }

			///// <summary>
			///// Compare two objects, check whether or not they are same
			///// </summary>
			///// <param name="obj">Another object to be checked with this</param>
			///// <returns>True if two objects are same</returns>
			//public override bool Equals(object obj)
			//{
			//	if (!(obj is PercentFormatArgs)) return false;
			//	PercentFormatArgs o = (PercentFormatArgs)obj;
			//	return this.DecimalPlaces.Equals(o.DecimalPlaces)
			//		&& this.UseSeparator == o.UseSeparator;
			//}

			///// <summary>
			///// Get the hash code of this object
			///// </summary>
			///// <returns></returns>
			//public override int GetHashCode()
			//{
			//	return base.GetHashCode();
			//}
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
