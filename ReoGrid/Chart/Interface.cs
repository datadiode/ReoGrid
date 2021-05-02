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

#if DRAWING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if WINFORM || ANDROID
using RGFloat = System.Single;

#elif WPF
using RGFloat = System.Double;

#endif // WPF

using unvell.ReoGrid.Data;
using unvell.ReoGrid.Drawing;
using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Events;
using unvell.ReoGrid.Rendering;

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Interface of chart component
	/// </summary>
	public interface IChart : IDrawingContainer
	{
		/// <summary>
		/// Get or set the title of chart
		/// </summary>
		string Title { get; set; }

		/// <summary>
		/// Get or set the data source of chart
		/// </summary>
		WorksheetChartDataSource DataSource { get; set; }

		/// <summary>
		/// Determine the style of data serial
		/// </summary>
		DataSerialStyleCollection DataSerialStyles { get; }

		/// <summary>
		/// Get the chart title shown to the user, either as explicitly assigned,
		/// or as derived from the exact one serial or category name if such exists.
		/// </summary>
		string GetDisplayTitle();
	}

	/// <summary>
	/// Event arguments for drawing context in Chart
	/// </summary>
	public class ChartDrawingEventArgs : DrawingEventArgs
	{
		/// <summary>
		/// Get the instance of current chart
		/// </summary>
	public IChart Chart { get; private set; }

		internal ChartDrawingEventArgs(IChart chart, DrawingContext dc, Rectangle bounds)
			: base(dc, bounds)
		{
			this.Chart = chart;
		}
	}

}

#endif // DRAWING
