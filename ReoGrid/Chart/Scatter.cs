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

#if WINFORM || ANDROID
using RGFloat = System.Single;
#else
using RGFloat = System.Double;
#endif // WINFORM

using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Rendering;


namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Represents scatter chart component.
	/// </summary>
	public class ScatterChart : AxisChart
	{
		/// <summary>
		/// Plot view object of scatter chart component.
		/// </summary>
		public readonly ScatterChartPlotView ScatterChartPlotView;

		/// <summary>
		/// Create scatter chart component instance.
		/// </summary>
		public ScatterChart()
		{
			SecondaryAxisInfo = new AxisDataInfo();
			AddPlotViewLayer(ScatterChartPlotView = new ScatterChartPlotView(this));
		}

		//protected override bool IsTransposed => true;

		/// <summary>
		/// Update chart data information.
		/// </summary>
		protected override void UpdatePlotData()
		{
			var ds = DataSource;
			if (ds == null) return;

			double xmin = double.PositiveInfinity;
			double xmax = double.NegativeInfinity;
			double ymin = double.PositiveInfinity;
			double ymax = double.NegativeInfinity;

			for (int i = 0, ii = ds.SerialCount / 2; ii > 0; --ii)
			{
				var xser = ds[i++];
				var yser = ds[i++];

				for (int j = 0, jj = Math.Min(xser.Count, yser.Count); jj > 0; --jj, ++j)
				{
					if (xser[j] is double xval && yser[j] is double yval)
					{
						if (xmin > xval) xmin = xval;
						if (xmax < xval) xmax = xval;
						if (ymin > yval) ymin = yval;
						if (ymax < yval) ymax = yval;
					}
				}
			}

			UpdateAxisInfo(PrimaryAxisInfo, ymin, ymax);
			UpdateAxisInfo(SecondaryAxisInfo, xmin, xmax);
		}

		/// <summary>
		/// Create and return the serial label axis info view.
		/// </summary>
		/// <param name="bodyBounds">Bounds for chart body.</param>
		/// <returns>Instance of serial label axis info view.</returns>
		protected override AxisInfoView CreatePrimaryAxisSerialLabelView(Rectangle bodyBounds)
		{
			return new AxisCategoryLabelView(this, AxisTypes.Secondary, AxisOrientation.Horizontal)
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
			return new AxisCategoryLabelView(this, AxisTypes.Primary, AxisOrientation.Vertical)
			{
				Bounds = GetDefaultHorizontalAxisInfoViewBounds(bodyBounds),
			};
		}
		/// <summary>
		/// Creates and returns scatter chart legend instance.
		/// </summary>
		/// <returns>Instance of scatter chart legend.</returns>
		protected override ChartLegend CreateChartLegend(LegendType type)
		{
			return new ScatterChartLegend(this);
		}
	}

	/// <summary>
	/// Represents scatter chart legend.
	/// </summary>
	public class ScatterChartLegend : ChartLegend
	{
		/// <summary>
		/// Create scatter chart legend.
		/// </summary>
		/// <param name="chart">Parent chart component.</param>
		public ScatterChartLegend(IChart chart)
			: base(chart)
		{
		}

		/// <summary>
		/// Get symbol size of chart legend.
		/// </summary>
		/// <param name="index">Index of serial in data source.</param>
		/// <returns>Symbol size of chart legend.</returns>
		protected override Size GetSymbolSize(int index)
		{
			return new Size(25, 3);
		}
	}

	/// <summary>
	/// Represents plot view object of scatter chart component.
	/// </summary>
	public class ScatterChartPlotView : ChartPlotView
	{
		/// <summary>
		/// Create scatter chart plot view object.
		/// </summary>
		/// <param name="chart">Parent chart component instance.</param>
		public ScatterChartPlotView(AxisChart chart)
			: base(chart)
		{
		}

		/// <summary>
		/// Render plot view of scatter chart component.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context.</param>
		protected override void OnPaint(DrawingContext dc)
		{
			base.OnPaint(dc);

			var axisChart = Chart as AxisChart;

			var xai = axisChart.SecondaryAxisInfo;
			if (double.IsNaN(xai.Levels) || xai.Levels <= 0)
			{
				return;
			}

			var yai = axisChart.PrimaryAxisInfo;
			if (double.IsNaN(yai.Levels) || yai.Levels <= 0)
			{
				return;
			}

			var ds = Chart.DataSource;

			var g = dc.Graphics;

			var clientRect = ClientBounds;
			double scaleX = clientRect.Width / (xai.Maximum - xai.Minimum);
			double scaleY = clientRect.Height / (yai.Maximum - yai.Minimum);
			var zeroWidth = (RGFloat)(xai.Minimum * scaleX);
			var zeroHeight = (RGFloat)(yai.Minimum * scaleY + clientRect.Height);

			int c = 0;

			for (int i = 0, ii = ds.SerialCount / 2; ii > 0; --ii)
			{
				var xser = ds[i++];
				var yser = ds[i++];

				var style = axisChart.DataSerialStyles[c++];

				var shift = style.LineWidth + 1;
				var width = style.LineWidth + shift;

				for (int j = 0, jj = Math.Min(xser.Count, yser.Count); jj > 0; --jj, ++j)
				{
					if (xser[j] is double xval && yser[j] is double yval)
					{
						g.DrawEllipse(style.LineColor,
							(RGFloat)(zeroWidth + xval * scaleX - shift),
							(RGFloat)(zeroHeight - yval * scaleY - shift),
							width, width);
					}
				}
			}
		}
	}
}

#endif // DRAWING