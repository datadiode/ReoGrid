/*****************************************************************************
 * 
 * ReoGrid - .NET Spreadsheet Control
 * 
 * https://reogrid.net/
 *
 * THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
 * PURPOSE.
 *
 * Author: Jing Lu <jingwood at unvell.com>
 *
 * Copyright (c) 2012-2021 Jing Lu <jingwood at unvell.com>
 * Copyright (c) 2012-2016 unvell.com, all rights reserved.
 * 
 ****************************************************************************/

using System;
using System.Globalization;
using unvell.ReoGrid.Core;
using unvell.ReoGrid.Graphics;

namespace unvell.ReoGrid.DataFormat
{
	/// <summary>
	/// Currency data formatter
	/// </summary>
	public class CurrencyDataFormatter : IDataFormatter
	{
		/// <summary>
		/// Format specified cell
		/// </summary>
		/// <param name="cell">cell to be formatted</param>
		/// <param name="culture">culture for parsing</param>
		/// <returns>Formatted text used to display as cell content</returns>
		public string FormatCell(Cell cell, CultureInfo culture)
		{
			bool isFormat = false;

			object data = cell.InnerData;
			double currency = double.NaN;

			if (data is double)
			{
				isFormat = true;
				currency = (double)data;
			}
			else if (data is DateTime dt)
			{
				currency = dt.ToOADate();
				isFormat = true;
			}
			else
			{
				string str = Convert.ToString(data).Trim();

				if (str.StartsWith("$"))
				{
					string number = str.Substring(1);
					if (double.TryParse(number, NumberStyles.Any, culture, out currency))
					{
						isFormat = true;
						cell.InnerData = currency;
						var args = new CurrencyFormatArgs()
						{
							DecimalPlaces = 2
						};
						switch (culture.NumberFormat.CurrencyPositivePattern)
						{
							case 0:
								args.PrefixSymbol = culture.NumberFormat.CurrencySymbol;
								break;
							case 1:
								args.PostfixSymbol = culture.NumberFormat.CurrencySymbol;
								break;
							case 2:
								args.PrefixSymbol = " " + culture.NumberFormat.CurrencySymbol;
								break;
							case 3:
								args.PostfixSymbol = " " + culture.NumberFormat.CurrencySymbol;
								break;
						}
						cell.DataFormatArgs = args;
					}
				}
				else
				{
					// Stop trying to convert datetime value to currency, #170
					isFormat = double.TryParse(str, NumberStyles.Any, culture, out currency);
				}
			}

			if (isFormat)
			{
				if (cell.InnerStyle.HAlign == ReoGridHorAlign.General)
				{
					cell.RenderHorAlign = ReoGridRenderHorAlign.Right;
				}

				string prefixSymbol = null;
				string postfixSymbol = null;
				short decimals = 2;
				NumberDataFormatter.NumberNegativeStyle negativeStyle = NumberDataFormatter.NumberNegativeStyle.Default;
				string prefix = null;
				string postfix = null;

				if (cell.DataFormatArgs is CurrencyFormatArgs args)
				{
					prefixSymbol = args.PrefixSymbol;
					postfixSymbol = args.PostfixSymbol;
					decimals = args.DecimalPlaces;
					negativeStyle = args.NegativeStyle;
					prefix = args.CustomNegativePrefix;
					postfix = args.CustomNegativePostfix;
				}

				if (currency < 0)
				{
					if ((negativeStyle & NumberDataFormatter.NumberNegativeStyle.Red) == NumberDataFormatter.NumberNegativeStyle.Red)
						cell.RenderColor = SolidColor.Red;
					else
						cell.RenderColor = SolidColor.Transparent;
				}

				// decimal places
				string decimalPlacePart = new string('0', decimals);

				// number
				string numberPartFormat = prefixSymbol + "#,##0." + decimalPlacePart + postfixSymbol;

				if ((negativeStyle & NumberDataFormatter.NumberNegativeStyle.Brackets) == NumberDataFormatter.NumberNegativeStyle.Brackets)
				{
					numberPartFormat = (currency < 0) ? ("(" + numberPartFormat + ")") : numberPartFormat;
				}
				else if ((negativeStyle & NumberDataFormatter.NumberNegativeStyle.Prefix_Sankaku) == NumberDataFormatter.NumberNegativeStyle.Prefix_Sankaku)
				{
					numberPartFormat = (currency < 0) ? ("▲ " + numberPartFormat) : numberPartFormat;
				}
				else if ((negativeStyle & NumberDataFormatter.NumberNegativeStyle.CustomSymbol) == NumberDataFormatter.NumberNegativeStyle.CustomSymbol)
				{
					numberPartFormat = (currency < 0) ? (prefix + numberPartFormat + postfix) : numberPartFormat;
				}

				// negative
				if ((negativeStyle & NumberDataFormatter.NumberNegativeStyle.Minus) == 0)
				{
					currency = Math.Abs(currency);
				}

				return currency.ToString(numberPartFormat);
			}
			else
				return null;
		}

		/// <summary>
		/// Represents arguments of currency data format.
		/// </summary>
		[Serializable]
		public class CurrencyFormatArgs : NumberDataFormatter.NumberFormatArgs
		{
			/// <summary>
			/// Currency symbol that displayed before currency number.
			/// </summary>
			public string PrefixSymbol { get; set; }

			/// <summary>
			/// Currency symbol that displayed after currency number.
			/// </summary>
			public string PostfixSymbol { get; set; }

			/// <summary>
			/// Culture name in English. (e.g. en-US)
			/// </summary>
			public string CultureEnglishName { get; set; }

			/// <summary>
			/// Locale ID
			/// </summary>
			public int LCID => CultureInfo.GetCultureInfo(CultureEnglishName).LCID;

			/// <summary>
			/// Check whether or not two objects are same.
			/// </summary>
			/// <param name="obj">Another object to be compared.</param>
			/// <returns>True if two objects are same; Otherwise return false.</returns>
			public override bool Equals(object obj)
			{
				if (!(obj is CurrencyFormatArgs)) return false;

				CurrencyFormatArgs o = (CurrencyFormatArgs)obj;

				return PrefixSymbol == o.PrefixSymbol
					&& PostfixSymbol == o.PostfixSymbol
					&& string.Compare(CultureEnglishName, o.CultureEnglishName, true) == 0
					&& base.Equals(obj);
			}

			/// <summary>
			/// Get hash code
			/// </summary>
			/// <returns></returns>
			public override int GetHashCode()
			{
				return base.GetHashCode();
			}
		}

		/// <summary>
		/// Determine whether or not to perform format test
		/// </summary>
		/// <returns>True to perform test; False to abort</returns>
		public bool PerformTestFormat()
		{
			return true;
		}
	}

}
