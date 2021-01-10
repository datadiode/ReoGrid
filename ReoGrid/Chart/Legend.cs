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

#if WINFORM || ANDROID
using RGFloat = System.Single;
#elif WPF
using RGFloat = System.Double;
#endif // WPF

using unvell.ReoGrid.Drawing;
using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Rendering;

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Represents chart legend view.
	/// </summary>
	public class ChartLegend : DrawingComponent
	{
		/// <summary>
		/// Get the instance of owner chart.
		/// </summary>
		public virtual IChart Chart { get; protected set; }

		/// <summary>
		/// Create chart legend view.
		/// </summary>
		/// <param name="chart">Instance of owner chart.</param>
		public ChartLegend(IChart chart)
		{
			this.Chart = chart;

			this.LineColor = SolidColor.Transparent;
			this.FillColor = SolidColor.Transparent;
			this.FontSize *= 0.8f;
		}

		/// <summary>
		/// Get or set type of legend.
		/// </summary>
		public LegendType LegendType { get; set; }

		/// <summary>
		/// Get or set the display position of legend.
		/// </summary>
		public LegendPosition LegendPosition { get; set; }

		/*
		/// <summary>
		/// Render chart legend view.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context instance.</param>
		protected override void OnPaint(DrawingContext dc)
		{
			base.OnPaint(dc);
		
			var g = dc.Graphics;
			//var ds = this.Chart.DataSource;
			var clientRect = this.ClientBounds;

			int dataCount = this.Chart.GetSerialCount();

			Rectangle itemRect = new Rectangle(0, 0, this.ItemSize.Width, ItemSize.Height);
			Size smybolSize = this.GetSymbolSize();

			for (int index = 0; index < dataCount; index++)
			{
				string itemTitle = this.Chart.GetSerialName(index);

				Rectangle symbolRect = new Rectangle(itemRect.Left + 3, itemRect.Top + (itemRect.Height - smybolSize.Height) / 2,
					smybolSize.Width, smybolSize.Height);

				this.DrawSymbol(dc, index, symbolRect);

				if (itemTitle != null)
				{
					Rectangle textRect = new Rectangle(symbolRect.Right + 3, itemRect.Top,
						itemRect.Width - symbolRect.Width - 3, itemRect.Height);

					g.DrawText(itemTitle, this.FontName, this.FontSize, this.ForeColor, textRect, ReoGridHorAlign.Left, ReoGridVerAlign.Middle);
				}

				itemRect.X += itemRect.Width;

				if (itemRect.X + itemRect.Width > clientRect.Right)
				{
					itemRect.X = 0;
					itemRect.Y += ItemSize.Height;
				}
			}
		}
		*/

		/// <summary>
		/// Get symbol count of chart legend.
		/// </summary>
		protected virtual int SymbolCount => Chart.DataSource.SerialCount;

		/// <summary>
		/// Get symbol text of chart legend.
		/// </summary>
		/// <param name="index">Index of serial in data source.</param>
		/// <returns>Symbol text of chart legend.</returns>
		protected virtual string GetSymbolText(int index) => Chart.DataSource[index].Label;

		/// <summary>
		/// Get default symbol size of chart legend.
		/// </summary>
		/// <param name="index">Index of serial in data source.</param>
		/// <returns>Symbol size of chart legend.</returns>
		protected virtual Size GetSymbolSize(int index)
		{
			return new Size(14, 14);
		}

		private Size layoutedSize = Size.Zero;

		/// <summary>
		/// Get measured legend view size.
		/// </summary>
		/// <returns>Measured size of legend view.</returns>
		public override Size GetPreferredSize()
		{
			return this.layoutedSize;
		}

		/// <summary>
		/// MeasureSize() produces a layout scaled to the resulting bounds.
		/// Therefore, nop out OnBoundsChanged() to prevent excess scaling.
		/// </summary>
		public override void OnBoundsChanged(Rectangle oldRect)
		{
		}

		/// <summary>
		/// Layout all legned items.
		/// </summary>
		public virtual void MeasureSize(DrawingContext dc, Rectangle parentClientRect)
		{
			var ds = Chart.DataSource;
			if (ds == null) return;

			int count = SymbolCount;

			Children.Clear();

			RGFloat maxSymbolWidth = 0, maxSymbolHeight = 0, maxLabelWidth = 0, maxLabelHeight = 0;

			// Suppress the legend when it has no labels or just repeats the chart title
			if (count == 1 && string.IsNullOrEmpty(Chart.Title) && GetSymbolText(0) == Chart.GetDisplayTitle())
				return;

			#region Measure Sizes
			for (int index = 0; index < count; index++)
			{
				string label = GetSymbolText(index);

				if (string.IsNullOrEmpty(label)) continue;

				var symbolSize = GetSymbolSize(index);

				if (maxSymbolWidth < symbolSize.Width) maxSymbolWidth = symbolSize.Width;
				if (maxSymbolHeight < symbolSize.Height) maxSymbolHeight = symbolSize.Height;

				var legendItem = new ChartLegendItem(Chart.DataSerialStyles[index], label)
				{
					FontName = FontName,
					FontSize = FontSize,
					FontStyles = FontStyles,
					SymbolBounds = new Rectangle(new Point(0, 0), symbolSize),
				};

				var labelSize = PlatformUtility.MeasureText(dc, label, legendItem.FontName, legendItem.FontSize, legendItem.FontStyles);

				if (maxLabelWidth < labelSize.Width) maxLabelWidth = labelSize.Width;
				if (maxLabelHeight < labelSize.Height) maxLabelHeight = labelSize.Height;

				legendItem.LabelBounds = new Rectangle(new Point(0, 0), labelSize);

				Children.Add(legendItem);
			}
			#endregion // Measure Sizes

			#region Layout
			const RGFloat symbolLabelSpacing = 4;

			var itemWidth = maxSymbolWidth + symbolLabelSpacing + maxLabelWidth;
			var itemHeight = Math.Max(maxSymbolHeight, maxLabelHeight);

			var clientRect = parentClientRect;
			RGFloat x = 0, y = 0, right = 0, bottom = 0, overflow = 0;

			if (LegendPosition == LegendPosition.Top || LegendPosition == LegendPosition.Bottom)
			{
				overflow = clientRect.Width - itemWidth;
			}

			foreach (var item in Children)
			{
				if (item is ChartLegendItem legendItem)
				{
					legendItem.SetSymbolLocation(0, (itemHeight - legendItem.SymbolBounds.Height) / 2);
					legendItem.SetLabelLocation(maxSymbolWidth + symbolLabelSpacing, (itemHeight - legendItem.LabelBounds.Height) / 2);

					legendItem.Bounds = new Rectangle(x, y, itemWidth, itemHeight);

					if (right < legendItem.Right) right = legendItem.Right;
					if (bottom < legendItem.Bottom) bottom = legendItem.Bottom;
				}

				const RGFloat itemSpacing = 10;

				x += itemWidth + itemSpacing;

				if (x > overflow)
				{
					x = 0;
					y += itemHeight + itemSpacing;
				}
			}
			#endregion // Layout

			layoutedSize = new Size(right + 10, bottom);
		}

	}

	public class ChartLegendByCategory : ChartLegend
	{
		public ChartLegendByCategory(IChart chart) : base(chart) { }

		/// <summary>
		/// Get symbol count of chart legend.
		/// </summary>
		protected override int SymbolCount => Chart.DataSource.CategoryCount;

		/// <summary>
		/// Get symbol text of chart legend.
		/// </summary>
		/// <param name="index">Index of serial in data source.</param>
		/// <returns>Symbol text of chart legend.</returns>
		protected override string GetSymbolText(int index) => Chart.DataSource.GetCategoryName(index);
	}

	/// <summary>
	/// Represents chart legend item.
	/// </summary>
	public class ChartLegendItem : DrawingObject
	{
		private Rectangle symbolBounds;
		public virtual Rectangle SymbolBounds { get { return this.symbolBounds; } set { this.symbolBounds = value; } }

		private Rectangle labelBounds;
		public virtual Rectangle LabelBounds { get { return this.labelBounds; } set { this.labelBounds = value; } }

		public virtual void SetSymbolLocation(RGFloat x, RGFloat y)
		{
			this.symbolBounds.X = x;
			this.symbolBounds.Y = y;
		}

		public virtual void SetLabelLocation(RGFloat x, RGFloat y)
		{
			this.labelBounds.X = x;
			this.labelBounds.Y = y;
		}

		public virtual IDrawingObjectStyle SymbolStyle  { get; protected set; }

		public ChartLegendItem(IDrawingObjectStyle symbolStyle, string legendLabel)
		{
			this.SymbolStyle = symbolStyle;
			this.LegendLabel = legendLabel;
		}

		public virtual string LegendLabel { get; set; }

		protected override void OnPaint(DrawingContext dc)
		{
#if DEBUG
			//dc.Graphics.FillRectangle(this.ClientBounds, SolidColor.LightSteelBlue);
#endif // DEBUG

			if (this.symbolBounds.Width > 0 && this.symbolBounds.Height > 0)
			{
				this.OnPaintSymbol(dc);
			}

			if (this.labelBounds.Width > 0 && this.labelBounds.Height > 0)
			{
				this.OnPaintLabel(dc);
			}
		}

		/// <summary>
		/// Draw chart legend symbol.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context instance.</param>
		public virtual void OnPaintSymbol(DrawingContext dc)
		{
			dc.Graphics.DrawAndFillRectangle(symbolBounds, SymbolStyle.LineColor, SymbolStyle.FillColor);
		}

		/// <summary>
		/// Draw chart legend label.
		/// </summary>
		/// <param name="dc">Platform no-associated drawing context instance.</param>
		public virtual void OnPaintLabel(DrawingContext dc)
		{
			dc.Graphics.DrawText(LegendLabel, this.FontName, this.FontSize, this.ForeColor, this.labelBounds,
				ReoGridHorAlign.Left, ReoGridVerAlign.Middle);
		}
	}

	/// <summary>
	/// Legend type.
	/// </summary>
	public enum LegendType
	{
		/// <summary>
		/// Primary legend.
		/// </summary>
		PrimaryLegend,

		/// <summary>
		/// Secondary legend.
		/// </summary>
		SecondaryLegend,
	}

	/// <summary>
	/// Legend position.
	/// </summary>
	public enum LegendPosition
	{
		/// <summary>
		/// Right
		/// </summary>
		Right,

		/// <summary>
		/// Bottom
		/// </summary>
		Bottom,

		/// <summary>
		/// Left
		/// </summary>
		Left,

		/// <summary>
		/// Top
		/// </summary>
		Top,
	}
}

#endif // DRAWING