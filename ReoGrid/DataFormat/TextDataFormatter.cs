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
using System.Globalization;
using unvell.ReoGrid.Core;

namespace unvell.ReoGrid.DataFormat
{
	internal class TextDataFormatter : IDataFormatter
	{
		/// <summary>
		/// Format specified cell
		/// </summary>
		/// <param name="cell">cell to be formatted</param>
		/// <param name="culture">culture for parsing</param>
		/// <returns>Formatted text used to display as cell content</returns>
		public string FormatCell(Cell cell, CultureInfo culture)
		{
			if (cell.InnerStyle.HAlign == ReoGridHorAlign.General)
			{
				cell.RenderHorAlign = ReoGridRenderHorAlign.Left;
			}

			return Convert.ToString(cell.InnerData);
		}

		public bool PerformTestFormat()
		{
			return false;
		}
	}
}
