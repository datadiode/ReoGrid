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

#if WINFORM || ANDROID
using RGFloat = System.Single;
#else
using RGFloat = System.Double;
#endif // WINFORM

using unvell.ReoGrid.Rendering;

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Represents line chart component.
	/// </summary>
	public class AreaChart : AxisChart
	{
		private AreaLineChartPlotView areaLineChartPlotView;

		/// <summary>
		/// Get plot view object of line chart component.
		/// </summary>
		public AreaLineChartPlotView AreaLineChartPlotView
		{
			get { return this.areaLineChartPlotView; }
			protected set { this.areaLineChartPlotView = value; }
		}

		/// <summary>
		/// Create line chart component instance.
		/// </summary>
		public AreaChart()
		{
			base.AddPlotViewLayer(this.areaLineChartPlotView = new AreaLineChartPlotView(this));
		}

		/// <summary>
		/// Creates and returns line chart legend instance.
		/// </summary>
		/// <returns>Instance of line chart legend.</returns>
		protected override ChartLegend CreateChartLegend(LegendType type)
		{
			return new LineChartLegend(this);
		}
	}

	public class AreaLineChartPlotView : LineChartPlotView
	{
		/// <summary>
		/// Create line chart plot view object.
		/// </summary>
		/// <param name="chart">Parent chart component instance.</param>
		public AreaLineChartPlotView(AxisChart chart)
			: base(chart)
		{
		}

		/// <summary>
		/// Render plot view region of line chart component.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context.</param>
		protected override void OnPaint(DrawingContext dc)
		{
			var axisChart = Chart as AxisChart;

			var ai = axisChart.PrimaryAxisInfo;
			if (double.IsNaN(ai.Levels) || ai.Levels <= 0)
			{
				return;
			}

			var ds = Chart.DataSource;

			var g = dc.Graphics;
			var clientRect = ClientBounds;

			double scaleX = clientRect.Width / ds.CategoryCount;
			double scaleY = clientRect.Height / (ai.Maximum - ai.Minimum);
			var zeroHeight = (RGFloat)(ai.Minimum * scaleY + clientRect.Height);

#if WINFORM
			var path = new System.Drawing.Drawing2D.GraphicsPath();

			for (int r = 0; r < ds.SerialCount; r++)
			{
				var style = axisChart.DataSerialStyles[r];
				var lastPoint = new System.Drawing.PointF((RGFloat)(0.5 * scaleX), zeroHeight);

				var point = lastPoint;
				for (int c = 0; c < ds.CategoryCount; c++)
				{
					if (ds[r][c] is double value)
					{
						point.Y = zeroHeight - (RGFloat)(value * scaleY);
					}
					else
					{
						point.Y = zeroHeight;
					}
					path.AddLine(lastPoint, point);
					lastPoint = point;
					point.X += (RGFloat)scaleX;
				}

				if (lastPoint.Y != zeroHeight)
				{
					point.X = lastPoint.X;
					point.Y = zeroHeight;
					path.AddLine(lastPoint, point);
				}

				path.CloseFigure();

				g.FillPath(style.FillColor, path);

				path.Reset();
			}

			path.Dispose();
#elif WPF

			for (int r = 0; r < ds.SerialCount; r++)
			{
				var style = axisChart.DataSerialStyles[r];

				var seg = new System.Windows.Media.PathFigure();

				var lastPoint = new System.Windows.Point(0.5 * scaleX, zeroHeight);
				seg.StartPoint = lastPoint;

				var point = seg.StartPoint;
				for (int c = 0; c < ds.CategoryCount; c++)
				{
					if (ds[r][c] is double value)
					{
						point.Y = zeroHeight - value * scaleY;
					}
					else
					{
						point.Y = zeroHeight;
					}
					seg.Segments.Add(new System.Windows.Media.LineSegment(point, true));
					lastPoint = point;
					point.X += scaleX;
				}

				point.X = lastPoint.X;
				point.Y = zeroHeight;
				seg.Segments.Add(new System.Windows.Media.LineSegment(point, true));

				seg.IsClosed = true;

				var path = new System.Windows.Media.PathGeometry();
				path.Figures.Add(seg);
				g.FillPath(style.LineColor, path);
			}

#endif // WPF
		}

	}
}

#endif // DRAWING
