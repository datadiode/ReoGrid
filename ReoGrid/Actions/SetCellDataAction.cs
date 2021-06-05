/*****************************************************************************
 * 
 * ReoGrid - Opensource .NET Spreadsheet Control
 * 
 * https://reogrid.net/
 *
 * THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
 * PURPOSE.
 *
 * Thank you to all contributors!
 * 
 * (c) 2012-2020 Jingwood, unvell.com <jingwood at unvell.com>
 * 
 ****************************************************************************/

using System;
using unvell.ReoGrid.DataFormat;

namespace unvell.ReoGrid.Actions
{
	/// <summary>
	/// Set data of cell action.
	/// </summary>
	public class SetCellDataAction : BaseWorksheetAction
	{
		/// <summary>
		/// Index of row to set data.
		/// </summary>
		public readonly int Row;

		/// <summary>
		/// Index of column to set data.
		/// </summary>
		public readonly int Col;

		//private bool isCellNull;

		/// <summary>
		/// Data of cell.
		/// </summary>
		public readonly object Data;

		private object backupData;
		//private string backupFormula;
		//private string displayBackup;
		private CellDataFormatFlag backupDataFormat;
		private object backupDataFormatArgs;
		//private Core.ReoGridRenderHorAlign backupRenderAlign;
		//private bool autoUpdateReferenceCells = false;
		private ushort? backupRowHeight = 0;

		/// <summary>
		/// Create SetCellValueAction with specified index of row and column.
		/// </summary>
		/// <param name="pos">position to locate the cell to be set.</param>
		/// <param name="data">data to be set.</param>
		public SetCellDataAction(CellPosition pos, object data)
		{
			this.Row = pos.Row;
			this.Col = pos.Col;
			this.Data = data;
		}

		/// <summary>
		/// Create action to set cell data.
		/// </summary>
		/// <param name="address">address to locate specified cell.</param>
		/// <param name="data">data to be set.</param>
		public SetCellDataAction(string address, object data)
			: this(new CellPosition(address), data)
		{
		}

		/// <summary>
		/// Do this operation.
		/// </summary>
		public override void Do()
		{
			Cell cell = Worksheet.CreateAndGetCell(Row, Col);

			this.backupData = cell.HasFormula ? ("=" + cell.InnerFormula) : cell.InnerData;

			this.backupDataFormat = cell.DataFormat;
			this.backupDataFormatArgs = cell.DataFormatArgs;

			try
			{
				Worksheet.SetSingleCellData(cell, Data);

				var rowHeightSettings = WorksheetSettings.Edit_AutoExpandRowHeight
					| WorksheetSettings.Edit_AllowAdjustRowHeight;

				RowHeader rowHeader = this.Worksheet.GetRowHeader(cell.InternalRow);

				this.backupRowHeight = rowHeader.InnerHeight;

				if ((this.Worksheet.settings & rowHeightSettings) == rowHeightSettings
					&& rowHeader.IsAutoHeight)
				{
					cell.ExpandRowHeight();
				}
			}
			catch (Exception ex)
			{
				this.Worksheet.NotifyExceptionHappen(ex);
			}

		}

		public override void Redo()
		{
			this.Do();

			Cell cell = Worksheet.GetCell(Row, Col);

			if (cell != null)
			{
				Worksheet.SelectRange(new RangePosition(cell.InternalRow, cell.InternalCol, cell.Rowspan, cell.Colspan));
			}
		}

		/// <summary>
		/// Undo this operation.
		/// </summary>
		public override void Undo()
		{
			if (this.backupRowHeight != null)
			{
				var rowHeader = this.Worksheet.GetRowHeader(this.Row);

				if (rowHeader.InnerHeight != this.backupRowHeight)
				{
					this.Worksheet.SetRowsHeight(this.Row, 1, (ushort)this.backupRowHeight);
				}
			}

			Cell cell = Worksheet.GetCell(Row, Col);
			if (cell != null)
			{
				cell.DataFormat = this.backupDataFormat;
				cell.DataFormatArgs = this.backupDataFormatArgs;

				this.Worksheet.SetSingleCellData(cell, this.backupData);

				Worksheet.SelectRange(new RangePosition(cell.InternalRow, cell.InternalCol, cell.Rowspan, cell.Colspan));
			}
		}

		/// <summary>
		/// Get friendly name of this action
		/// </summary>
		/// <returns></returns>
		public override string GetName()
		{
			string str = Data == null ? "null" : Data.ToString();
			return "Set Cell Value: " + (str.Length > 10 ? (str.Substring(0, 7) + "...") : str);
		}
	}
}
