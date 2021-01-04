/*****************************************************************************
 * 
 * ReoGrid - .NET Spreadsheet Control
 * 
 * http://reogrid.net/
 *
 * THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
 * PURPOSE.
 *
 * Author: Jing <lujing at unvell.com>
 *
 * Copyright (c) 2012-2016 Jing <lujing at unvell.com>
 * Copyright (c) 2012-2016 unvell.com, all rights reserved.
 * 
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using unvell.ReoGrid.Core;
using unvell.ReoGrid.DataFormat;
using unvell.ReoGrid.Utility;

namespace unvell.ReoGrid.DataFormat
{
	/// <summary>
	/// Datetime data formatter
	/// </summary>
	public class DateTimeDataFormatter : IDataFormatter
	{
		/// <summary>
		/// Format cell
		/// </summary>
		/// <param name="cell">cell to be formatted</param>
		/// <returns>Formatted text used to display as cell content</returns>
		public string FormatCell(Cell cell)
		{
			object data = cell.InnerData;

			bool isFormat = false;
			DateTime value = DateTime.FromOADate(0);
			string formattedText = null;

			if (data is DateTime dt)
			{
				value = dt;
				isFormat = true;
			}
			else
			{
				string strdata = (data is string ? (string)data : Convert.ToString(data));
				isFormat = DateTime.TryParse(strdata, out value);
				if (isFormat)
				{
					cell.InnerData = value;
					var culture = Thread.CurrentThread.CurrentCulture;
					cell.DataFormatArgs = new DateTimeFormatArgs
					{
						Format = culture.DateTimeFormat.ShortDatePattern,
						CultureName = culture.Name
					};
				}
				else
				{
					double number;
					if (double.TryParse(strdata, NumberStyles.Any, CultureInfo.InvariantCulture, out number))
					{
						try
						{
							value = DateTime.FromOADate(number);
							isFormat = true;
						}
						catch { }
					}
				}
			}

			if (isFormat)
			{
				if (cell.InnerStyle.HAlign == ReoGridHorAlign.General)
				{
					cell.RenderHorAlign = ReoGridRenderHorAlign.Right;
				}

				CultureInfo culture = Thread.CurrentThread.CurrentCulture;
				string pattern = culture.DateTimeFormat.ShortDatePattern;

				if (cell.DataFormatArgs is DateTimeFormatArgs args)
				{
					// fixes issue #203: pattern is ignored incorrectly
					if (!string.IsNullOrEmpty(args.Format))
					{
						pattern = args.Format;
					}

					if (args.CultureName != null && args.CultureName != culture.Name)
					{
						culture = new CultureInfo(args.CultureName);
					}
				}

				if (culture.Name.StartsWith("ja") && pattern.Contains("g"))
				{
					culture = new CultureInfo("ja-JP", true);
					culture.DateTimeFormat.Calendar = new JapaneseCalendar();
				}

				try
				{
					switch (pattern)
					{
						case "d":
							formattedText = value.Day.ToString();
							break;

						default:
							formattedText = value.ToString(pattern, culture);
							break;
					}
				}
				catch
				{
					formattedText = Convert.ToString(value);
				}
			}

			return formattedText;
		}

		/// <summary>
		/// Represents the argument that is used during format a cell as data time.
		/// </summary>
		[Serializable]
		public struct DateTimeFormatArgs
		{
			private string format;
			/// <summary>
			/// Get or set the date time pattern. (Standard .NET datetime pattern is supported, e.g.: yyyy/MM/dd)
			/// </summary>
			public string Format { get { return format; } set { format = value; } }

			private string cultureName;
			/// <summary>
			/// Get or set the culture name that is used to format datetime according to localization settings.
			/// </summary>
			public string CultureName { get { return cultureName; } set { cultureName = value; } }

			/// <summary>
			/// Compare to another object, check whether or not two objects are same.
			/// </summary>
			/// <param name="obj">Another object to be compared.</param>
			/// <returns>True if two objects are same; Otherwise return false.</returns>
			public override bool Equals(object obj)
			{
				if (!(obj is DateTimeFormatArgs)) return false;
				DateTimeFormatArgs o = (DateTimeFormatArgs)obj;
				return format.Equals(o.format)
					&& cultureName.Equals(o.cultureName);
			}

			/// <summary>
			/// Get the hash code of this argument object.
			/// </summary>
			/// <returns>Hash code of argument object.</returns>
			public override int GetHashCode()
			{
				return format.GetHashCode() ^ cultureName.GetHashCode();
			}
		}

		/// <summary>
		/// Determines whether or not to perform a test when target cell is not set as datetime format.
		/// </summary>
		/// <returns></returns>
		public bool PerformTestFormat()
		{
			return true;
		}
	}
}
