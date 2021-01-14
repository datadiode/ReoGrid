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

#if DRAWING

using System;
using System.Collections.Generic;

#if WINFORM || ANDROID
using RGFloat = System.Single;
#else
using RGFloat = System.Double;
#endif // WINFORM

using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Drawing.Shapes;
using unvell.ReoGrid.Drawing;
using unvell.ReoGrid.Rendering;

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Repersents pie chart component.
	/// </summary>
	public class PieChart : Chart
	{
		internal virtual PieChartDataInfo DataInfo { get; private set; }
		internal virtual PiePlotView PiePlotView { get; private set; }

		/// <summary>
		/// Creates pie chart instance.
		/// </summary>
		public PieChart()
		{
			this.DataInfo = new PieChartDataInfo();
			this.AddPlotViewLayer(this.PiePlotView = CreatePlotViewInstance());
		}

		#region Legend
		protected override ChartLegend CreateChartLegend(LegendType type)
		{
			var chartLegend = new ChartLegendByCategory(this);

			if (type == LegendType.PrimaryLegend)
			{
				chartLegend.LegendPosition = LegendPosition.Bottom;
			}

			return chartLegend;
		}
		#endregion // Legend

		#region Plot view instance
		/// <summary>
		/// Creates and returns pie plot view.
		/// </summary>
		/// <returns></returns>
		protected virtual PiePlotView CreatePlotViewInstance()
		{
			return new PiePlotView(this);
		}
		#endregion // Plot view instance

		#region Layout
		protected override Rectangle GetPlotViewBounds(DrawingContext dc, Rectangle bodyBounds)
		{
			RGFloat minSize = Math.Min(bodyBounds.Width, bodyBounds.Height);

			return new Rectangle(bodyBounds.X + (bodyBounds.Width - minSize) / 2, 
				bodyBounds.Y + (bodyBounds.Height - minSize) / 2,
				minSize, minSize);
		}
		#endregion // Layout
	}
	
	/// <summary>
	/// Represents pie chart data information.
	/// </summary>
	public class PieChartDataInfo
	{
		public double Total { get; set; }
	}

	/// <summary>
	/// Represents pie plot view.
	/// </summary>
	public class PiePlotView : ChartPlotView
	{
		/// <summary>
		/// Create plot view object of pie 2d chart.
		/// </summary>
		/// <param name="chart">Pie chart instance.</param>
		public PiePlotView(Chart chart)
			: base(chart)
		{
		}

		protected virtual PieShape CreatePieShape(Rectangle bounds)
		{
			return new PieShape()
			{
				Bounds = bounds,
				LineColor = SolidColor.Transparent,
			};
		}

		/// <summary>
		/// Render pie 2d plot view.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context instance.</param>
		protected override void OnPaint(Rendering.DrawingContext dc)
		{
			base.OnPaint(dc);

			var ds = Chart.DataSource;
			if (ds != null && ds.SerialCount != 0)
			{
				var serial = ds[0];
				var dataCount = serial.Count;

				// Sum up the data values
				double sum = 0;

				for (int i = 0; i < dataCount; i++)
				{
					var data = serial[i];

					if (data != null)
					{
						sum += (double)data;
					}
				}

				// Draw the pie shapes
				RGFloat currentAngle = 0;
				PieShape pie = null;

				for (int i = 0; i < dataCount; i++)
				{
					RGFloat scale = (RGFloat)(360.0 / sum);
					var data = serial[i];

					if (data != null)
					{
						RGFloat angle = (RGFloat)(data * scale);

						pie = CreatePieShape(ClientBounds);
						pie.StartAngle = currentAngle;
						pie.SweepAngle = angle;
						pie.FillColor = Chart.DataSerialStyles[i].FillColor;
						pie.Draw(dc);

						currentAngle += angle;
					}
				}

				// Draw the category names over the pie shapes (experimental)
				/*currentAngle = 0;

				for (int i = 0; i < dataCount; i++)
				{
					RGFloat scale = (RGFloat)(360.0 / sum);
					var data = serial[i];

					if (data != null)
					{
						RGFloat angle = (RGFloat)(data * scale);
						currentAngle += angle;
						var itemTitle = ds.GetCategoryName(i);
						if (itemTitle != null)
						{
							angle = currentAngle - angle / 2;
							var bounds = pie.ClientBounds;
							var x = Math.Sin(Math.PI * angle / 180) * bounds.Width / 3;
							var y = Math.Cos(Math.PI * angle / 180) * bounds.Height / -3;
							bounds.Offset((RGFloat)x, (RGFloat)y);
							dc.Graphics.DrawText(itemTitle, FontName, FontSize, ForeColor, bounds,
								ReoGridHorAlign.Center, ReoGridVerAlign.Middle);
						}
					}
				}*/
			}
		}
	}

	/// <summary>
	/// Repersents pie 2D chart component.
	/// </summary>
	public class Pie2DChart : PieChart
	{
	}

	/// <summary>
	/// Represents pie 2D plot view.
	/// </summary>
	public class Pie2DPlotView : PiePlotView
	{
		public Pie2DPlotView(Pie2DChart pieChart)
			: base(pieChart)
		{
		}
	}

	/// <summary>
	/// Repersents doughnut chart component.
	/// </summary>
	public class DoughnutChart : PieChart
	{
		/// <summary>
		/// Creates and returns doughnut plot view.
		/// </summary>
		protected override PiePlotView CreatePlotViewInstance()
		{
			return new DoughnutPlotView(this);
		}
	}

	/// <summary>
	/// Represents doughnut plot view.
	/// </summary>
	public class DoughnutPlotView : PiePlotView
	{
		public DoughnutPlotView(DoughnutChart chart)
			: base(chart)
		{
		}

		protected override PieShape CreatePieShape(Rectangle bounds)
		{
			return new Drawing.Shapes.SmartShapes.BlockArcShape
			{
				Bounds = bounds,
				LineColor = SolidColor.White,
			};
		}
	}
}

#endif // DRAWING