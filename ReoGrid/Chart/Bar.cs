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

using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Rendering;

#if WINFORM || ANDROID
using RGFloat = System.Single;
#else
using RGFloat = System.Double;
#endif // WINFORM

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Represents a horizontal bar chart.
	/// </summary>
	public class BarChart : ColumnChart
	{
		/// <summary>
		/// Create the instance of bar chart.
		/// </summary>
		public BarChart()
		{
			this.ShowHorizontalGuideLines = false;
			this.ShowVerticalGuideLines = true;
		}

		/// <summary>
		/// Create and return the chart plot view object.
		/// </summary>
		/// <returns></returns>
		protected override ColumnChartPlotView CreateColumnChartPlotView()
		{
			return new BarChartPlotView(this);
		}

		///// <summary>
		///// Return the default bounds for chart plot view body.
		///// </summary>
		///// <returns></returns>
		//protected override Rectangle GetPlotViewBounds(Rectangle)
		//{
		//	RGFloat width = this.Width * 0.60f;
		//	RGFloat height = this.Height - 80;

		//	return new Rectangle((this.Width - width) / 2, 50, width, height);
		//}

		/// <summary>
		/// Create and return the serial label axis info view.
		/// </summary>
		/// <param name="bodyBounds">Bounds for chart body.</param>
		/// <returns>Instance of serial label axis info view.</returns>
		protected override AxisInfoView CreatePrimaryAxisSerialLabelView(Rectangle bodyBounds)
		{
			return new AxisSerialLabelView(this, AxisTypes.Primary, AxisOrientation.Vertical)
			{
				Bounds = GetDefaultVerticalAxisInfoViewBounds(bodyBounds),
			};
		}

		/// <summary>
		/// Create and return the category label axis info view.
		/// </summary>
		/// <param name="bodyBounds">Bounds for chart body.</param>
		/// <returns>Instance of category label axis info view.</returns>
		protected override AxisInfoView CreatePrimaryAxisCategoryLabelView(Rectangle bodyBounds)
		{
			return new AxisCategoryLabelView(this, AxisTypes.Primary, AxisOrientation.Horizontal)
			{
				Bounds = GetDefaultHorizontalAxisInfoViewBounds(bodyBounds),
			};
		}

		protected override Rectangle GetPlotViewBounds(DrawingContext dc, Rectangle bodyBounds)
		{
			var rect = base.GetPlotViewBounds(dc, bodyBounds);

			RGFloat extraWidth = 0;

			for (int i = 0; i < DataSource.CategoryCount; i++)
			{
				var title = DataSource.GetCategoryName(i);

				if (!string.IsNullOrEmpty(title))
				{
					var size = PlatformUtility.MeasureText(dc, title, FontName, FontSize, Drawing.Text.FontStyles.Regular);
					if (extraWidth < size.Width)
						extraWidth = size.Width;
				}
			}

			extraWidth -= 30; // to compensate for the 30 units set aside by base.GetPlotViewBounds()

			return new Rectangle(rect.X + extraWidth, rect.Y, rect.Width - extraWidth, rect.Height);
		}
	}

	/// <summary>
	/// Represents a bar chart plot view.
	/// </summary>
	public class BarChartPlotView : ColumnChartPlotView
	{
		/// <summary>
		/// Create instance of bar chart plot view.
		/// </summary>
		/// <param name="chart">Bar chart which holds this plot view.</param>
		public BarChartPlotView(BarChart chart)
			: base(chart)
		{
		}

		/// <summary>
		/// Render the bar chart plot view.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context instance.</param>
		protected override void OnPaint(DrawingContext dc)
		{
			var clientRect = ClientBounds;
			var availableHeight = clientRect.Height * 0.7;

			if (availableHeight < 20)
			{
				return;
			}

			var axisChart = Chart as AxisChart;

			var ds = Chart.DataSource;

			var rows = ds.SerialCount;
			var columns = ds.CategoryCount;

			var groupColumnWidth = availableHeight / columns;
			var groupColumnSpace = (clientRect.Height - availableHeight) / columns;
			var singleColumnHeight = groupColumnWidth / rows;

			var ai = axisChart.PrimaryAxisInfo;
			double scaleX = clientRect.Width / (ai.Maximum - ai.Minimum);
			var zeroWidth = (RGFloat)(-ai.Minimum * scaleX);

			double y = groupColumnSpace / 2;

			var g = dc.Graphics;

			for (int c = 0; c < columns; c++)
			{
				for (int r = 0; r < rows; r++)
				{
					if (ds[r][c] is double value)
					{
						var style = axisChart.DataSerialStyles[r];
						var rect = value > 0 ?
							new Rectangle(
								zeroWidth, (RGFloat)y,
								(RGFloat)(value * scaleX), (RGFloat)(singleColumnHeight - 1)) :
							new Rectangle(
								zeroWidth - (RGFloat)value, (RGFloat)y,
								(RGFloat)(value * scaleX), (RGFloat)(singleColumnHeight - 1));
						rect.Intersect(clientRect);
						g.DrawAndFillRectangle(rect, style.LineColor, style.FillColor);
					}

					y += singleColumnHeight;
				}

				y += groupColumnSpace;
			}
		}
	}
}

#endif // DRAWING