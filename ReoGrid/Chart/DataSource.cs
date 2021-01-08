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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using unvell.ReoGrid.Data;
using unvell.ReoGrid.Graphics;

namespace unvell.ReoGrid.Chart
{
	/// <summary>
	/// Represents the interface of data source used for chart.
	/// </summary>
	/// <typeparam name="T">Standard data serial classes.</typeparam>
	public interface IChartDataSource<T> : IDataSource<T> where T : IChartDataSerial
	{
		/// <summary>
		/// Get number of categories.
		/// </summary>
		int CategoryCount { get; }

		/// <summary>
		/// Get category name by specified index position.
		/// </summary>
		/// <param name="index">Zero-based number of category to get its name.</param>
		/// <returns>Specified category's name by index position.</returns>
		string GetCategoryName(int index);
	}

	/// <summary>
	/// Represents the interface of data serial used for chart.
	/// </summary>
	public interface IChartDataSerial : IDataSerial
	{
		/// <summary>
		/// Get the serial name.
		/// </summary>
		string Label { get; }
	}

	/// <summary>
	/// Data source from given worksheet
	/// </summary>
	public class WorksheetChartDataSource : IChartDataSource<WorksheetChartDataSerial>
	{
		private Worksheet worksheet;

		#region Ranges

		private int categoryCount;

		#endregion // Ranges

		#region Constructor

		/// <summary>
		/// Create data source instance with specified worksheet instance
		/// </summary>
		/// <param name="worksheet">Instance of worksheet to read titles and data of plot serial.</param>
		public WorksheetChartDataSource(Worksheet worksheet)
		{
			this.worksheet = worksheet;
		}

		/// <summary>
		/// Create data source instance with specified worksheet instance
		/// </summary>
		/// <param name="worksheet">Instance of worksheet to read titles and data of plot serial.</param>
		/// <param name="serialNamesRange">Names for serial data from this range.</param>
		/// <param name="serialsRange">Serial data from this range.</param>
		/// <param name="serialPerRowOrColumn">Add serials by this specified direction. Default is Row.</param>
		public WorksheetChartDataSource(Worksheet worksheet, string serialNamesRange, string serialsRange,
			RowOrColumn serialPerRowOrColumn = RowOrColumn.Row)
			: this(worksheet)
		{
			if (worksheet == null)
			{
				throw new ArgumentNullException("worksheet");
			}

			var snRange = worksheet.TryGetRangeByAddressOrName(serialNamesRange);
			if (snRange == null)
			{
				throw new InvalidAddressException("cannot determine the serial names range by specified range address or name.");
			}

			var sRange = worksheet.TryGetRangeByAddressOrName(serialsRange);
			if (sRange == null)
			{
				throw new InvalidAddressException("cannot determine the serials range by specified range address or name.");
			}

			AddSerialsFromRange(snRange, sRange, serialPerRowOrColumn);
		}

		/// <summary>
		/// Create data source instance with specified worksheet instance and serial data range.
		/// </summary>
		/// <param name="worksheet">Instance of worksheet to read titles and data of plot serial.</param>
		/// <param name="serialNamesRange">Range to read labels of data serial.</param>
		/// <param name="serialsRange">Range to read serial data.</param>
		/// <param name="serialPerRowOrColumn">Add serials by this specified direction. Default is Row.</param>
		public WorksheetChartDataSource(Worksheet worksheet, ReferenceRange serialNamesRange, ReferenceRange serialsRange,
			RowOrColumn serialPerRowOrColumn = RowOrColumn.Row)
			: this(worksheet ?? throw new ArgumentNullException("worksheet"))
		{
			this.AddSerialsFromRange(serialNamesRange, serialsRange, serialPerRowOrColumn);
		}

		private void AddSerialsFromRange(ReferenceRange serialNamesRange, ReferenceRange serialsRange,
			RowOrColumn serialPerRowOrColumn = RowOrColumn.Row)
		{
			if (serialPerRowOrColumn == RowOrColumn.Row)
			{
				for (int r = serialsRange.Row; r <= serialsRange.EndRow; r++)
				{
					var label = new RangePosition(r, serialNamesRange.Col, 1, serialNamesRange.Cols);
					var pos = new RangePosition(r, serialsRange.Col, 1, serialsRange.Cols);
					this.AddSerial(worksheet,
						new ReferenceRange(serialNamesRange.Worksheet, label),
						new ReferenceRange(serialsRange.Worksheet, pos));
				}
			}
			else if (serialPerRowOrColumn == RowOrColumn.Column)
			{
				for (int c = serialsRange.Col; c <= serialsRange.EndCol; c++)
				{
					var label = new RangePosition(serialNamesRange.Row, c, serialNamesRange.Rows, 1);
					var pos = new RangePosition(serialsRange.Row, c, serialsRange.Rows, 1);
					this.AddSerial(worksheet,
						new ReferenceRange(serialNamesRange.Worksheet, label),
						new ReferenceRange(serialsRange.Worksheet, pos));
				}
			}
			else
			{
				this.AddSerial(worksheet, serialNamesRange, serialsRange);
			}
		}

		#endregion // Constructor

		#region Category

		/// <summary>
		/// Get or set the range that contains the category names.
		/// </summary>
		public ReferenceRange CategoryNameRange { get; set; }

		/// <summary>
		/// Return the title of specified column.
		/// </summary>
		/// <param name="index">Zero-based number of column.</param>
		/// <returns>Return the title that will be displayed on chart.</returns>
		public string GetCategoryName(int index)
		{
			if (CategoryNameRange != null)
			{
				var row = CategoryNameRange.Row;
				var col = CategoryNameRange.Col;
				if (CategoryNameRange.Row == CategoryNameRange.EndRow ?
					(col += index) <= CategoryNameRange.EndCol :
					(row += index) <= CategoryNameRange.EndRow)
				{
					return (CategoryNameRange.Worksheet ?? worksheet).GetCellData<string>(row, col);
				}
			}
			return null;
		}

		#endregion // Category

		#region Serials
		/// <summary>
		/// Get number of data serials.
		/// </summary>
		public virtual int SerialCount { get { return this.serials.Count; } }

		/// <summary>
		/// Get number of categories.
		/// </summary>
		public virtual int CategoryCount { get { return this.categoryCount; } }

		internal List<WorksheetChartDataSerial> serials = new List<WorksheetChartDataSerial>();

		public WorksheetChartDataSerial this[int index]
		{
			get
			{
				if (index < 0 || index >= this.serials.Count)
				{
					throw new ArgumentOutOfRangeException("index");
				}

				return this.serials[index];
			}
		}

		/// <summary>
		/// Add serial data into data source.
		/// </summary>
		/// <param name="serial">Serial data source.</param>
		public void Add(WorksheetChartDataSerial serial)
		{
			this.serials.Add(serial);

			this.UpdateCategoryCount(serial);
		}

		internal void UpdateCategoryCount(WorksheetChartDataSerial serial)
		{
			this.categoryCount = Math.Max(this.categoryCount, Math.Max(serial.DataRange.Cols, serial.DataRange.Rows));
		}

		/// <summary>
		/// Add serial data into data source from a range, set the name as the label of serial.
		/// </summary>
		/// <param name="worksheet">Worksheet instance to read serial data.</param>
		/// <param name="name">Name for serial to be added.</param>
		/// <param name="serialRange">Range to read serial data from worksheet.</param>
		/// <returns>Instance of chart serial has been added.</returns>
		public WorksheetChartDataSerial AddSerial(Worksheet worksheet, ReferenceRange labelAddress, ReferenceRange serialRange)
		{
			var serial = new WorksheetChartDataSerial(this, worksheet, labelAddress, serialRange);
			this.Add(serial);
			return serial;
		}

		public WorksheetChartDataSerial GetSerial(int index)
		{
			if (index < 0 || index >= this.serials.Count)
			{
				throw new ArgumentOutOfRangeException("index");
			}

			return this.serials[index];
		}
		#endregion // Serials
	}

	#region WorksheetChartDataSerial
	/// <summary>
	/// Represents implementation of chart data serial.
	/// </summary>
	public class WorksheetChartDataSerial : IChartDataSerial
	{
		private WorksheetChartDataSource dataSource;

		private Worksheet worksheet;

		/// <summary>
		/// Determine the range to read data from worksheet
		/// </summary>
		public ReferenceRange DataRange { get; set; }

		/// <summary>
		/// Determine the range to read serial labels from worksheet
		/// </summary>
		public ReferenceRange LabelAddress { get; set; }

		/// <summary>
		/// Create chart data serial instance with specified worksheet and reference ranges
		/// </summary>
		/// <param name="dataSource">Data source to read chart data from worksheet.</param>
		/// <param name="worksheet">Instance of worksheet that contains the data to be read.</param>
		/// <param name="labelAddress">The address to locate label of serial on worksheet.</param>
		/// <param name="dataRange">Serial data range to read serial data for chart from worksheet.</param>
		public WorksheetChartDataSerial(WorksheetChartDataSource dataSource, Worksheet worksheet, ReferenceRange labelAddress, ReferenceRange dataRange)
		{
			this.dataSource = dataSource ?? throw new ArgumentNullException("dataSource");
			this.worksheet = worksheet ?? throw new ArgumentNullException("worksheet");
			this.LabelAddress = labelAddress;
			this.DataRange = dataRange;
		}

		/// <summary>
		/// Get label text of serial.
		/// </summary>
		public string Label
		{
			get
			{
				if (LabelAddress == null)
					return string.Empty;

				var worksheet = LabelAddress.Worksheet ?? this.worksheet;
				if (worksheet == null)
					return string.Empty;

				return worksheet.GetCellText(LabelAddress.StartPos);
			}
		}

		/// <summary>
		/// Get number of data items of current serial.
		/// </summary>
		public int Count => Math.Max(DataRange.Rows, DataRange.Cols);

		/// <summary>
		/// Get data from serial by specified index position.
		/// </summary>
		/// <param name="index">Zero-based index position in serial to get data.</param>
		/// <returns>Data in double type to be get from specified index of serial.
		/// If index is out of range, or data in worksheet is null, then return null.
		/// </returns>
		public double? this[int index]
		{
			get
			{
				object data;

				var worksheet = DataRange.Worksheet ?? this.worksheet;
				if (DataRange.Rows > DataRange.Cols)
				{
					data = worksheet.GetCellData(DataRange.Row + index, DataRange.Col);
				}
				else
				{
					data = worksheet.GetCellData(DataRange.Row, DataRange.Col + index);
				}

				double val;

				if (unvell.ReoGrid.Utility.CellUtility.TryGetNumberData(data, out val))
				{
					return val;
				}
				else
				{
					return null;
				}
			}
		}
	}
	#endregion // WorksheetChartDataSerial

}

#endif // DRAWING