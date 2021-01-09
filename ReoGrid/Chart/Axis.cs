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
 * Copyright (c) 2012-2016 Jing <lujing at unvell.com>
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
#else
using RGFloat = System.Double;
#endif // WINFORM

using unvell.ReoGrid.Data;
using unvell.ReoGrid.Drawing;
using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Rendering;

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Axis data information for axis-based chart.
	/// </summary>
	public class AxisDataInfo
	{
		/// <summary>
		/// Get or set the plot vertial levels.
		/// </summary>
		public int Levels { get; set; }

		/// <summary>
		/// Get or set axis scaler.
		/// </summary>
		public int Scaler { get; set; }

		/// <summary>
		/// Get or set axis minimum value.
		/// </summary>
		public double Minimum { get; set; }

		/// <summary>
		/// Get or set axis maximum value.
		/// </summary>
		public double Maximum { get; set; }

		/// <summary>
		/// Specifies that whether or not to decide the axis minimum value automatically by scanning data.
		/// </summary>
		public bool AutoMinimum { get; set; }

		/// <summary>
		/// Specifies that whether or not to decide the axis maximum value automatically by scanning data.
		/// </summary>
		public bool AutoMaximum { get; set; }

		/// <summary>
		/// Get or set the axis large stride value.
		/// </summary>
		public double LargeStride { get; set; }

		/// <summary>
		/// Get or set the axis small stride value.
		/// </summary>
		public double SmallStride { get; set; }

	}

	/// <summary>
	/// Axis Types
	/// </summary>
	public enum AxisTypes
	{
		/// <summary>
		/// Primary axis
		/// </summary>
		Primary,
		
		/// <summary>
		/// Secondary axis
		/// </summary>
		Secondary,
	}

	/// <summary>
	/// Axis Orientations
	/// </summary>
	public enum AxisOrientation
	{
		/// <summary>
		/// Horizontal
		/// </summary>
		Horizontal,

		/// <summary>
		/// Vertical
		/// </summary>
		Vertical,
	}

	internal struct PlotPointRow : IEnumerable<PlotPointColumn>
	{
		public PlotPointColumn[] columns;

		public PlotPointColumn this[int index]
		{
			get { return columns[index]; }
			set { columns[index] = value; }
		}

		public int Length
		{
			get { return this.columns.Length; }
		}

		public IEnumerator<PlotPointColumn> GetEnumerator()
		{
			foreach (var col in this.columns)
			{
				yield return col;
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.columns.GetEnumerator();
		}
	}

	internal struct PlotPointColumn
	{
		public bool hasValue;
		public RGFloat value;

		public static readonly PlotPointColumn Nil = new PlotPointColumn { hasValue = false, value = 0 };

		public static implicit operator PlotPointColumn(RGFloat value)
		{
			return new PlotPointColumn { hasValue = true, value = value };
		}
	}

	#region On-Axis Views
	public abstract class AxisInfoView : DrawingObject
	{
		public readonly AxisChart Chart;
		public readonly AxisTypes AxisType;

		public AxisInfoView(AxisChart chart, AxisTypes axisType = AxisTypes.Primary)
		{
			Chart = chart;
			AxisType = axisType;

			FillColor = SolidColor.Transparent;
			LineColor = SolidColor.Transparent;
			FontSize *= 0.9f;
		}

		protected AxisDataInfo AxisInfo => AxisType == AxisTypes.Primary ? Chart.PrimaryAxisInfo : Chart.SecondaryAxisInfo;
	}

	public class AxisCategoryLabelView : AxisInfoView
	{
		private AxisOrientation orientation;

		public AxisCategoryLabelView(AxisChart chart, AxisTypes axisType = AxisTypes.Primary, AxisOrientation orientation = AxisOrientation.Vertical)
			: base(chart, axisType)
		{
			this.orientation = orientation;
		}

		/// <summary>
		/// Render axis information view.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context instance.</param>
		protected override void OnPaint(DrawingContext dc)
		{
			base.OnPaint(dc);

			var ai = AxisInfo;

			if (ai == null) return;

			var g = dc.Graphics;

			var ds = Chart.DataSource;
			var clientRect = ClientBounds;

			RGFloat fontHeight = (RGFloat)(this.FontSize * PlatformUtility.GetDPI() / 72.0) + 4;

			double rowValue = ai.Minimum;

			if (orientation == AxisOrientation.Vertical)
			{
				RGFloat stepY = (clientRect.Height - fontHeight) / ai.Levels;
				var textRect = new Rectangle(0, clientRect.Bottom - fontHeight, clientRect.Width, fontHeight);

				for (int level = 0; level <= ai.Levels; level++)
				{
					g.DrawText(Math.Round(rowValue, Math.Abs(ai.Scaler)).ToString(), FontName, FontSize, ForeColor, textRect, ReoGridHorAlign.Right, ReoGridVerAlign.Middle);

					textRect.Y -= stepY;
					rowValue += Math.Round(ai.LargeStride, Math.Abs(ai.Scaler));
				}
			}
			else if (orientation == AxisOrientation.Horizontal)
			{
				var maxWidth = Math.Max(
					PlatformUtility.MeasureText(dc.Renderer,
						Math.Round(ai.Minimum, Math.Abs(ai.Scaler)).ToString(),
						FontName, FontSize, Drawing.Text.FontStyles.Regular
					).Width,
					PlatformUtility.MeasureText(dc.Renderer,
						Math.Round(ai.Maximum, Math.Abs(ai.Scaler)).ToString(),
						FontName, FontSize, Drawing.Text.FontStyles.Regular
					).Width);

				int showTitleStride = Math.Max((int)Math.Ceiling(ai.Levels * maxWidth / clientRect.Width), 1);

				RGFloat columnWidth = clientRect.Width / ai.Levels * showTitleStride;
				var textRect = new Rectangle(clientRect.Left - (columnWidth / 2), clientRect.Top, columnWidth, clientRect.Height);

				for (int level = 0; level <= ai.Levels; level += showTitleStride)
				{
					g.DrawText(Math.Round(rowValue, Math.Abs(ai.Scaler)).ToString(), FontName, FontSize, ForeColor, textRect, ReoGridHorAlign.Center, ReoGridVerAlign.Top);

					textRect.X += columnWidth;
					rowValue += Math.Round(ai.LargeStride, Math.Abs(ai.Scaler)) * showTitleStride;
				}
			}
		}
	}

	public class AxisSerialLabelView : AxisInfoView
	{
		private AxisOrientation orientation;

		public AxisSerialLabelView(AxisChart chart, AxisTypes axisType = AxisTypes.Primary, AxisOrientation orientation = AxisOrientation.Horizontal)
			: base(chart, axisType)
		{
			this.orientation = orientation;
		}

		/// <summary>
		/// Render data label view.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context instance.</param>
		protected override void OnPaint(DrawingContext dc)
		{
			base.OnPaint(dc);

			var ai = AxisInfo;

			if (ai == null) return;

			var g = dc.Graphics;

			var ds = this.Chart.DataSource;
			var clientRect = this.ClientBounds;

			RGFloat maxWidth = 0;
			RGFloat maxHeight = 0;

			int dataCount = ds.CategoryCount;

			for (int i = 0; i < dataCount; i++)
			{
				var title = ds.GetCategoryName(i);

				if (!string.IsNullOrEmpty(title))
				{
					var size = PlatformUtility.MeasureText(dc.Renderer, title, this.FontName, this.FontSize, Drawing.Text.FontStyles.Regular);

					if (maxWidth < size.Width)
						maxWidth = size.Width;
					if (maxHeight < size.Height)
						maxHeight = size.Height;
				}
			}

			if (orientation == AxisOrientation.Horizontal && maxWidth != 0)
			{
				RGFloat columnWidth = (clientRect.Width) / dataCount;

				var showableColumns = clientRect.Width / maxWidth;

				int showTitleStride = (int)Math.Ceiling(dataCount / showableColumns);
				if (showTitleStride < 1) showTitleStride = 1;

				ReoGridHorAlign halign = showTitleStride == 1 ? ReoGridHorAlign.Center : ReoGridHorAlign.Left;

				RGFloat stepX = clientRect.Width / dataCount;

				for (int i = 0; i < dataCount; i += showTitleStride)
				{
					string text = ds.GetCategoryName(i);

					if (!string.IsNullOrEmpty(text))
					{
						var textRect = new Rectangle(columnWidth * i, 0, columnWidth * showTitleStride, clientRect.Height);

						g.DrawText(text, FontName, FontSize, ForeColor, textRect, halign, ReoGridVerAlign.Middle);
					}
				}
			}
			else if (orientation == AxisOrientation.Vertical && maxHeight != 0)
			{
				RGFloat rowHeight = (clientRect.Height - 10) / dataCount;

				var showableRows = clientRect.Height / maxHeight;

				int showTitleStride = (int)Math.Ceiling(dataCount / showableRows);
				if (showTitleStride < 1) showTitleStride = 1;

				for (int i = 0; i < dataCount; i += showTitleStride)
				{
					string text = ds.GetCategoryName(i);

					if (!string.IsNullOrEmpty(text))
					{
						var textRect = new Rectangle(0, rowHeight * i + 5, clientRect.Width, rowHeight);

						g.DrawText(text, FontName, FontSize, ForeColor, textRect, ReoGridHorAlign.Right, ReoGridVerAlign.Middle);
					}
				}
			}
		}
	}
	#endregion // On-Axis Views

	#region Axis Plot Background View
	public class AxisGuideLinePlotView : ChartPlotView
	{
		public AxisGuideLinePlotView(AxisChart chart)
			: base(chart)
		{
			LineColor = SolidColor.Silver;
		}

		/// <summary>
		/// Render axis plot view.
		/// </summary>
		/// <param name="dc">Platform unassociated drawing context instance.</param>
		protected override void OnPaint(DrawingContext dc)
		{
			var axisChart = Chart as AxisChart;

			var g = dc.Graphics;
			var clientRect = this.ClientBounds;

			if (axisChart.ShowHorizontalGuideLines)
			{
				var ai = axisChart.PrimaryAxisInfo;

				RGFloat stepY = clientRect.Height / ai.Levels;
				RGFloat y = clientRect.Bottom;

				for (int level = 0; level <= ai.Levels; level++)
				{
					g.DrawLine(clientRect.X, y, clientRect.Right, y, this.LineColor);
					y -= stepY;
				}
			}

			if (axisChart.ShowVerticalGuideLines)
			{
				var ai = axisChart.PrimaryAxisInfo;

				RGFloat stepX = clientRect.Width / ai.Levels;
				RGFloat x = clientRect.Left;

				for (int level = 0; level <= ai.Levels; level++)
				{
					g.DrawLine(x, clientRect.Top, x, clientRect.Bottom, this.LineColor);
					x += stepX;
				}
			}
		}
	}
	#endregion // Axis Plot Background View
}

#endif // DRAWING