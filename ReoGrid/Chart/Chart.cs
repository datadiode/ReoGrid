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

#if WINFORM || ANDROID
using RGFloat = System.Single;
#elif WPF
using RGFloat = System.Double;
#endif // WPF

using unvell.ReoGrid.Drawing;
using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Rendering;
using unvell.ReoGrid.Interaction;

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Represents chart drawing component.
	/// </summary>
	public abstract class Chart : DrawingComponent, IChart
	{
		/// <summary>
		/// Get or set the title string object.
		/// </summary>
		public virtual IDrawingObject TitleView { get; set; }

		/// <summary>
		/// Get or set the title of chart.
		/// </summary>
		public virtual string Title { get; set; }

		#region Constructor
		/// <summary>
		/// Create chart instance.
		/// </summary>
		protected Chart()
		{
			// border line color
			this.LineColor = SolidColor.Silver;
			this.Padding = new PaddingValue(10);

			// body
			this.Children.Add(this.PlotViewContainer = new DrawingComponent()
			{
				FillColor = SolidColor.Transparent,
				LineColor = SolidColor.Transparent,
			});

			// title
			this.Children.Add(this.TitleView = new ChartTitle(this));

			// legend
			this.Children.Add(this.PrimaryLegend = CreateChartLegend(LegendType.PrimaryLegend));
		}
		#endregion // Constructor

		#region Layout

		public override Size GetPreferredSize()
		{
			return new Size(400, 260);
		}

		/// <summary>
		/// Update children view bounds.
		/// </summary>
		protected virtual void UpdateLayout(DrawingContext dc)
		{
			var clientRect = ClientBounds;

			const RGFloat titlePlotSpacing = 20;
			const RGFloat plotLegendSpacing = 10;

			var titleBounds = GetTitleBounds();

			var bodyBounds = new Rectangle(clientRect.X, titleBounds.Bottom + titlePlotSpacing,
				clientRect.Width, clientRect.Height - titleBounds.Bottom - titlePlotSpacing);

			if (ShowLegend)
			{
				var legendSize = Size.Zero;

				if (PrimaryLegend != null)
				{
					PrimaryLegend.MeasureSize(dc, clientRect);
					PrimaryLegend.Visible = PrimaryLegend.Children.Count != 0;

					if (PrimaryLegend.Visible)
					{
						PrimaryLegend.Bounds = GetLegendBounds(bodyBounds, LegendType.PrimaryLegend, PrimaryLegend.LegendPosition);

						switch (PrimaryLegend.LegendPosition)
						{
							case LegendPosition.Left:
								bodyBounds.X += PrimaryLegend.Width + plotLegendSpacing;
								break;

							default:
							case LegendPosition.Right:
								bodyBounds.Width -= PrimaryLegend.Width + plotLegendSpacing;
								break;

							case LegendPosition.Top:
								bodyBounds.Y += PrimaryLegend.Height + plotLegendSpacing;
								break;

							case LegendPosition.Bottom:
								bodyBounds.Height -= PrimaryLegend.Height + plotLegendSpacing;
								break;
						}
					}
				}
			}

			if (PlotViewContainer != null)
			{
				PlotViewContainer.Bounds = GetPlotViewBounds(dc, bodyBounds);
			}

			if (TitleView != null) TitleView.Bounds = titleBounds;
		}

		/// <summary>
		/// Get the chart title shown to the user, either as explicitly assigned,
		/// or as derived from the exact one serial or category name if such exists.
		/// </summary>
		public string GetDisplayTitle()
		{
			string text = Title;
			if (string.IsNullOrEmpty(text) && DataSource != null)
			{
				if (DataSource.SerialCount == 1)
				{
					text = DataSource[0].Label;
				}
				else if (DataSource.CategoryCount == 1)
				{
					text = DataSource.GetCategoryName(0);
				}
				else
				{
					text = "Chart";
				}
			}
			return text;
		}

		/// <summary>
		/// Get default title bounds.
		/// </summary>
		/// <returns>Rectangle of title bounds.</returns>
		protected virtual Rectangle GetTitleBounds()
		{
			var titleRect = this.ClientBounds;
			titleRect.Height = 30;
			return titleRect;
		}

		/// <summary>
		/// Get default body bounds.
		/// </summary>
		/// <returns>Rectangle of body bounds.</returns>
		protected virtual Rectangle GetPlotViewBounds(DrawingContext dc, Rectangle bodyBounds)
		{
			return bodyBounds;
		}

		/// <summary>
		/// Get legend view bounds.
		/// </summary>
		/// <returns></returns>
		protected virtual Rectangle GetLegendBounds(Rectangle plotViewBounds, LegendType type, LegendPosition position)
		{
			var clientRect = this.ClientBounds;

			Size legendSize = Size.Zero;

			switch (type)
			{
				default:
				case LegendType.PrimaryLegend:
					legendSize = this.PrimaryLegend.GetPreferredSize();
					break;
			}

			switch (position)
			{
				case LegendPosition.Left:
					return new Rectangle(0,
						plotViewBounds.Y + (plotViewBounds.Height - legendSize.Height) / 2,
							legendSize.Width, legendSize.Height);

				default:
				case LegendPosition.Right:
					return new Rectangle(clientRect.Right - legendSize.Width,
						plotViewBounds.Y + (plotViewBounds.Height - legendSize.Height) / 2,
							legendSize.Width, legendSize.Height);

				case LegendPosition.Top:
					return new Rectangle(plotViewBounds.X + (plotViewBounds.Width - legendSize.Width) / 2,
						0, legendSize.Width, legendSize.Height);

				case LegendPosition.Bottom:
					return new Rectangle(plotViewBounds.X + (plotViewBounds.Width - legendSize.Width) / 2, 
						plotViewBounds.Bottom - legendSize.Height, legendSize.Width, legendSize.Height);
			}
		}
		#endregion // Layout

		#region Paint
		protected override void OnPaint(DrawingContext dc)
		{
			UpdateLayout(dc);
			base.OnPaint(dc);
		}
		#endregion // Paint

		#region Data Source
		///// <summary>
		///// Specifies that whether or not allow to swap the row and column from specified data range.
		///// </summary>
		//public virtual bool SwapDataRowColumn { get; set; }

		private WorksheetChartDataSource dataSource;

		/// <summary>
		/// Get or set chart data source.
		/// </summary>
		public virtual WorksheetChartDataSource DataSource
		{
			get { return this.dataSource; }
			set
			{
				this.dataSource = value;
				this.ResetDataSerialStyles();
			}
		}

		/// <summary>
		/// Update chart when data source or data range has been changed.
		/// </summary>
		protected virtual void UpdatePlotData()
		{
			// empty
		}

		#endregion // Data Source

		#region Plot View Children
		/// <summary>
		/// Get the chart plot view component.
		/// </summary>
		public virtual IDrawingContainer PlotViewContainer
		{ get; private set; }

		/// <summary>
		/// Add chart plot view object.
		/// </summary>
		/// <param name="view">Chart plot view object.</param>
		protected virtual void AddPlotViewLayer(IPlotView view)
		{
			if (this.PlotViewContainer != null)
			{
				view.Bounds = this.PlotViewContainer.ClientBounds;
				this.PlotViewContainer.Children.Add(view);
			}

			this.Invalidate();
		}
		#endregion // Children Body

		#region Legend

		/// <summary>
		/// Get or set whether or not to show legend view.
		/// </summary>
		public bool ShowLegend { get; set; } = true;

		/// <summary>
		/// Get or set the primary legend object.
		/// </summary>
		public virtual ChartLegend PrimaryLegend { get; set; }

		//private LegendPosition primaryLegendPosition;

		//public LegendPosition PrimaryLegendPosition
		//{
		//	get
		//	{
		//		return this.primaryLegendPosition;
		//	}
		//	set
		//	{
		//		if (this.primaryLegendPosition != value)
		//		{
		//			this.primaryLegendPosition = value;

		//			this.UpdateChart();
		//			this.Invalidate();
		//		}
		//	}
		//}

		/// <summary>
		/// Create chart legend.
		/// </summary>
		/// <returns>Instance of chart legend.</returns>
		protected virtual ChartLegend CreateChartLegend(LegendType type)
		{
			return new ChartLegend(this);
		}

		#endregion // Legend Symbol

		#region Data Serial
		internal List<DataSerialStyle> serialStyles = new List<DataSerialStyle>();

		/// <summary>
		/// Reset data serial to row default styles.
		/// </summary>
		protected virtual void ResetDataSerialStyles()
		{
			if (DataSource == null) return;

			// Allocate styles in such way that derived classes can opt to use them for categories instead
			int dataSerialCount = Math.Max(DataSource.SerialCount, DataSource.CategoryCount);

			if (dataSerialCount <= 0)
			{
				serialStyles.Clear();
			}

			while (serialStyles.Count < dataSerialCount)
			{
				serialStyles.Add(new DataSerialStyle(this)
				{
					FillColor = ChartUtility.GetDefaultDataSerialFillColor(serialStyles.Count),
					LineColor = ChartUtility.GetDefaultDataSerialFillColor(serialStyles.Count),
					LineWidth = 2f,
				});
			}
		}

		///// <summary>
		///// Get the number of serial styles that is used for this chart.
		///// </summary>
		///// <returns>Number of serial styles.</returns>
		//protected virtual int GetSerialStyleCount()
		//{
		//	return this.dataSource.SerialCount;
		//}

		private DataSerialStyleCollection dataSerialStyleCollection = null;

		/// <summary>
		/// Get data serial styles.
		/// </summary>
		public virtual DataSerialStyleCollection DataSerialStyles
		{
			get
			{
				if (this.dataSerialStyleCollection == null)
				{
					this.dataSerialStyleCollection = new DataSerialStyleCollection(this);
				}

				return this.dataSerialStyleCollection;
			}
		}

		///// <summary>
		///// Get number of data serials from data source.
		///// </summary>
		///// <returns>Number of data serials.</returns>
		//public virtual int GetSerialCount()
		//{
		//	return this.DataSource == null ? 0 : this.DataSource.SerialCount;
		//}

		///// <summary>
		///// Get name of specified data serial.
		///// </summary>
		///// <param name="index">Zero-based number of data serial to get name.</param>
		///// <returns>Name in string of specified data serial.</returns>
		//public virtual string GetSerialName(int index)
		//{
		//	return this.DataSource == null ? string.Empty : this.DataSource[index].Label;
		//}

		#endregion // Data Serial

		#region Mouse
		/// <summary>
		/// Handles the mouse down event.
		/// </summary>
		/// <param name="location">Relative location of mouse button press-down.</param>
		/// <param name="button">Determines that which mouse button is pressed down.</param>
		/// <returns>True if event has been handled; Otherwise false.</returns>
		public override bool OnMouseDown(Point location, MouseButtons button)
		{
			return base.OnMouseDown(location, button);
		}
		#endregion // Mouse
	}

	/// <summary>
	/// Represents axis-based chart component. 
	/// This is an abstract class that should be implemented by other axis-based chart classes.
	/// </summary>
	public abstract class AxisChart : Chart
	{
		#region Attributes
		/// <summary>
		/// Get or set the primary axis information set.
		/// </summary>
		public virtual AxisDataInfo PrimaryAxisInfo
		{ get; set; }

		/// <summary>
		/// Get or set the secondary axis information set.
		/// </summary>
		public virtual AxisDataInfo SecondaryAxisInfo
		{ get; set; }

		/// <summary>
		/// Get or set the primary axis view object.
		/// </summary>
		public virtual AxisInfoView HorizontalAxisInfoView
		{ get; set; }

		/// <summary>
		/// Get or set the data label view object.
		/// </summary>
		public virtual AxisInfoView VerticalAxisInfoView
		{ get; set; }

		/// <summary>
		/// Get or set the grid line background view object.
		/// </summary>
		public virtual AxisGuideLinePlotView GuideLineBackgroundView
		{ get; set; }

		/// <summary>
		/// Specifies that whether or not allow to display the horizontal guide lines.
		/// </summary>
		public virtual bool ShowHorizontalGuideLines { get; set; }

		/// <summary>
		/// Specifies that whether or not allow to display the vertical guide lines.
		/// </summary>
		public virtual bool ShowVerticalGuideLines { get; set; }
		#endregion // Attributes

		#region Constructor

		/// <summary>
		/// Create axis-based chart instance.
		/// </summary>
		public AxisChart()
		{
			this.ShowHorizontalGuideLines = true;
			this.ShowVerticalGuideLines = false;

			this.PrimaryAxisInfo = new AxisDataInfo();
			this.SecondaryAxisInfo = null;// = new AxisRulerInfo();

			var bodyBounds = this.PlotViewContainer.Bounds;
			var clientRect = this.ClientBounds;

			base.AddPlotViewLayer(this.GuideLineBackgroundView = this.CreateGuideLineBackgroundView(bodyBounds));
			this.Children.Add(this.HorizontalAxisInfoView = this.CreatePrimaryAxisSerialLabelView(bodyBounds));
			this.Children.Add(this.VerticalAxisInfoView = this.CreatePrimaryAxisCategoryLabelView(bodyBounds));
		}

		protected virtual AxisGuideLinePlotView CreateGuideLineBackgroundView(Rectangle bodyBounds)
		{
			return new AxisGuideLinePlotView(this)
			{
				Size = new Size(bodyBounds.Width, bodyBounds.Height),
			};
		}

		protected virtual AxisInfoView CreatePrimaryAxisSerialLabelView(Rectangle bodyBounds)
		{
			return new AxisSerialLabelView(this, AxisTypes.Primary)
			{
				Bounds = GetDefaultHorizontalAxisInfoViewBounds(bodyBounds),
			};
		}

		protected virtual Rectangle GetDefaultHorizontalAxisInfoViewBounds(Rectangle bodyBounds)
		{
			return new Rectangle(bodyBounds.X, bodyBounds.Bottom + 6, bodyBounds.Width, 24);
		}

		/// <summary>
		/// Create default primary axis category label view.
		/// </summary>
		/// <param name="bodyBounds"></param>
		/// <returns></returns>
		protected virtual AxisInfoView CreatePrimaryAxisCategoryLabelView(Rectangle bodyBounds)
		{
			return new AxisCategoryLabelView(this, AxisTypes.Primary)
			{
				Bounds = GetDefaultVerticalAxisInfoViewBounds(bodyBounds),
			};
		}

		/// <summary>
		/// Return default vertical axis bounds.
		/// </summary>
		/// <param name="bodyBounds">Bounds of chart body.</param>
		/// <returns>Vertical axis bounds rectangle.</returns>
		protected virtual Rectangle GetDefaultVerticalAxisInfoViewBounds(Rectangle bodyBounds)
		{
			return new Rectangle(bodyBounds.X - 35, bodyBounds.Y - 5, 30, bodyBounds.Height + 10);
		}
		
		#endregion // Constructor

		#region Update Draw Points

		/// <summary>
		/// Update chart data information.
		/// </summary>
		protected override void UpdatePlotData()
		{
			var ds = DataSource;
			if (ds == null) return;

			double minData = double.PositiveInfinity;
			double maxData = double.NegativeInfinity;

			for (int r = 0; r < ds.SerialCount; r++)
			{
				for (int c = 0; c < ds.CategoryCount; c++)
				{
					if (ds[r][c] is double data)
					{
						if (minData > data) minData = data;
						if (maxData < data) maxData = data;
					}
				}
			}

			//var ai = !SwapDataRowColumn ? PrimaryAxisInfo : SecondaryAxisInfo;
			UpdateAxisInfo(PrimaryAxisInfo, minData, maxData);
		}

		/// <summary>
		/// Update specified axis information.
		/// </summary>
		/// <param name="ai">Axis information set.</param>
		/// <param name="minData">Minimum value scanned from data range.</param>
		/// <param name="maxData">Maximum value scanned from data range.</param>
		protected virtual void UpdateAxisInfo(AxisDataInfo ai, double minData, double maxData)
		{
			var clientRect = this.PlotViewContainer;

			double range = maxData - minData;

			var isTransposed = HorizontalAxisInfoView.Orientation == AxisOrientation.Vertical;

			ai.Levels = (int)Math.Ceiling((isTransposed ? clientRect.Width : clientRect.Height) / 30f);

			// when clientRect is zero, nothing to do
			if (double.IsNaN(ai.Levels))
			{
				return;
			}

			if (minData == maxData)
			{
				if (maxData == 0)
					maxData = ai.Levels;
				else
					minData = 0;
			}

			int scaler;
			double stride = ChartUtility.CalcLevelStride(minData, maxData, ai.Levels, out scaler);
			double nearzero = stride / 1E9;
			ai.Scaler = scaler;

			double m;

			if (!ai.AutoMinimum)
			{
				if (this.AxisOriginToZero(minData, maxData, range))
				{
					ai.Minimum = 0;
				}
				else
				{
					m = minData % stride;
					if (Math.Abs(m) < nearzero)
					{
						ai.Minimum = minData;
					}
					else
					{
						if (minData < 0)
						{
							ai.Minimum = minData - stride - m;
						}
						else
						{
							ai.Minimum = minData - m;
						}
					}
				}
			}

			if (!ai.AutoMaximum)
			{
				m = maxData % stride;
				if (Math.Abs(m) < nearzero)
				{
					ai.Maximum = maxData;
				}
				else
				{
					ai.Maximum = maxData - m + stride;
				}
			}

			ai.Levels = (int)Math.Round((ai.Maximum - ai.Minimum) / stride);

			ai.LargeStride = stride;
		}

		/// <summary>
		/// Measure axis ruler information.
		/// </summary>
		/// <param name="info">Specified axis data information set.</param>
		/// <param name="data">Data to be measured.</param>
		protected virtual void MeasureAxisRuler(AxisDataInfo info, double data)
		{
			if (info.Minimum > data) info.Minimum = data;
			if (info.Maximum < data) info.Maximum = data;

			info.LargeStride = (info.Maximum - info.Minimum) / 5;
		}

		/// <summary>
		/// Determines that whether or not allow to set axis minimum value to a non-zero position automatically.
		/// </summary>
		/// <param name="minData">Minimum data scanned from data source.</param>
		/// <param name="maxData">Maximum data scanned from data source.</param>
		/// <param name="range">Data range.</param>
		/// <returns>True to set axis minimum value; Otherwise false.</returns>
		protected virtual bool AxisOriginToZero(double minData, double maxData, double range)
		{
			return (maxData > 0 && minData > 0
					|| maxData < 0 && minData < 0)
					&& Math.Abs(minData) < range;
		}

		#endregion // Update Draw Points

		#region Layout

		/// <summary>
		/// Update all children bounds.
		/// </summary>
		protected override void UpdateLayout(DrawingContext dc)
		{
			base.UpdateLayout(dc);

			UpdatePlotData();

			GuideLineBackgroundView.Bounds = PlotViewContainer.ClientBounds;

			UpdateAxisLabelViewLayout(this.PlotViewContainer.Bounds);
		}

		protected virtual void UpdateAxisLabelViewLayout(Rectangle plotRect)
		{
			const RGFloat spacing = 10;

			var isTransposed = HorizontalAxisInfoView.Orientation == AxisOrientation.Vertical;
			var vbounds = new Rectangle(ClientBounds.X, plotRect.Y - 5, plotRect.X - ClientBounds.X - spacing, plotRect.Height + 10);
			var hbounds = new Rectangle(plotRect.X, plotRect.Bottom + 5, plotRect.Width, 15);
			VerticalAxisInfoView.Bounds = isTransposed ? hbounds : vbounds;
			HorizontalAxisInfoView.Bounds = isTransposed ? vbounds : hbounds;
		}

		protected override Rectangle GetPlotViewBounds(DrawingContext dc, Rectangle bodyBounds)
		{
			var rect = base.GetPlotViewBounds(dc, bodyBounds);

			const RGFloat spacing = 10;

			return new Rectangle(rect.X + 30 + spacing, rect.Y, rect.Width - 30 - spacing, rect.Height - 10);
		}

		#endregion // Layout
	}

}

#endif // DRAWING