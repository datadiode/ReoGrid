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
 * This software released under LGPLv3 license.
 *
 * Copyright (c) 2012-2016 Jing <lujing at unvell.com>
 * Copyright (c) 2012-2016 unvell.com, all rights reserved.
 * 
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Provider;

using unvell.Common;

using unvell.ReoGrid.Editor.Properties;
using unvell.ReoGrid.PropertyPages;
using unvell.ReoGrid.Actions;
using unvell.ReoGrid.CellTypes;
using unvell.ReoGrid.Events;
using unvell.ReoGrid.Data;
using unvell.ReoGrid.WinForm;
using unvell.ReoGrid.IO;
using unvell.ReoGrid.DataFormat;
using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Rendering;
using unvell.ReoGrid.Editor.LangRes;

using unvell.ReoGrid.Print;
using unvell.ReoGrid.Drawing;
using unvell.ReoGrid.Drawing.Text;
using unvell.UIControls;

using Point = System.Drawing.Point;
using System.Collections;
using unvell.Common.Win32Lib;
using System.Threading;

namespace unvell.ReoGrid.Editor
{
	/// <summary>
	/// Represents Editor of ReoGrid component.
	/// </summary>
	public partial class ReoGridCompare : Form
	{
		#region Constructor

		private NamedRangeManageForm nameManagerForm = null;
		private readonly CSVFormatArgument csvTabDelimited = new CSVFormatArgument
		{
			LineRegex = CSVFormatArgument.RegexTab
		};
		private readonly CSVFormatArgument csvCommaDelimited = new CSVFormatArgument
		{
			LineRegex = CSVFormatArgument.RegexComma
		};
		private readonly CSVFormatArgument csvSemicolonDelimited = new CSVFormatArgument
		{
			LineRegex = CSVFormatArgument.RegexSemicolon
		};
		private readonly CSVFormatArgument csvPipeDelimited = new CSVFormatArgument
		{
			LineRegex = CSVFormatArgument.RegexPipe
		};

		private enum Side : int
		{
			Left = 1,
			Right = 2,
		};

		/// <summary>
		/// Flag to avoid scroll two controls recursively
		/// </summary>
		private bool inScrolling = false;
		private float ViewLeft = 0;
		private float ViewTop = 0;
		private object arg1;
		private object arg2;
		private LinkedList<IAction> undoStack = new LinkedList<IAction>();
		private LinkedList<IAction> redoStack = new LinkedList<IAction>();
		private int rowDiffCount = 0;
		private bool KeepSheetsInSync =>
			grid1.Visible && grid2.Visible && // need to avoid synchronization during Load()
			grid1.GetWorksheetIndex(grid1.CurrentWorksheet) == grid2.GetWorksheetIndex(grid2.CurrentWorksheet);

		public void ParseArguments(IList arguments)
		{
			int i;
			if ((i = arguments.IndexOf("/ParentWindow")) != -1)
			{
				arguments.RemoveAt(i);
				if (i < arguments.Count)
				{
					IntPtr hwndParent = (IntPtr)Convert.ToInt64((string)arguments[i], 16);
					arguments.RemoveAt(i);
					FormBorderStyle = FormBorderStyle.None;
					CreateControl();
					Win32.SetWindowLong(Handle, Win32.GWL_STYLE, Win32.GetWindowLong(Handle, Win32.GWL_STYLE) | Win32.WS_CHILD);
					Win32.SetParent(Handle, hwndParent);
				}
			}
			if (arguments.Count > 0)
			{
				header1.Text = (string)arguments[0];
				header1.Modified = true;
			}
			if (arguments.Count > 1)
			{
				header2.Text = (string)arguments[1];
				header2.Modified = true;
			}
		}
		/// <summary>
		/// Create instance of ReoGrid Editor.
		/// </summary>
		public ReoGridCompare()
		{
			InitializeComponent();
			toolStrip1.Renderer = new ToolStripRenderer();

			nextDiffToolStripButton.Click += nextDiffToolStripButton_Click;
			prevDiffToolStripButton.Click += prevDiffToolStripButton_Click;
			firstDiffToolStripButton.Click += firstDiffToolStripButton_Click;
			lastDiffToolStripButton.Click += lastDiffToolStripButton_Click;
			left2rightToolStripButton.Click += left2rightToolStripButton_Click;
			right2leftToolStripButton.Click += right2leftToolStripButton_Click;

			header1.GotFocus += Header_GotFocus;
			header2.GotFocus += Header_GotFocus;
			header1.SelectionChanged += (s, e) => arg1 = LoadOneFile(grid1, header1);
			header2.SelectionChanged += (s, e) => arg2 = LoadOneFile(grid2, header2);

			grid1.GotFocus += Grid_GotFocus;
			grid2.GotFocus += Grid_GotFocus;
			grid1.LostFocus += Grid_LostFocus;
			grid2.LostFocus += Grid_LostFocus;
			grid1.WorksheetInserted += Grid_WorksheetInserted;
			grid2.WorksheetInserted += Grid_WorksheetInserted;
			grid1.WorksheetRemoved += Grid_WorksheetRemoved;
			grid2.WorksheetRemoved += Grid_WorksheetRemoved;
			grid1.CurrentWorksheetChanged += Grid_CurrentWorksheetChanged;
			grid2.CurrentWorksheetChanged += Grid_CurrentWorksheetChanged;
			grid1.ActionPerformed += Grid_ActionPerformed;
			grid2.ActionPerformed += Grid_ActionPerformed;
			grid1.Visible = false;
			grid2.Visible = false;

			// Simulate some events which went undetected
			Grid_WorksheetInserted(grid1, new WorksheetInsertedEventArgs(grid1.CurrentWorksheet));
			Grid_WorksheetInserted(grid2, new WorksheetInsertedEventArgs(grid2.CurrentWorksheet));

			// Sync scroll from control 1 to control 2
			grid1.WorksheetScrolled += (s, e) =>
			{
				if (!inScrolling)
				{
					inScrolling = true;
					var grid = s as ReoGridControl;
					ViewLeft = grid.CurrentWorksheet.ViewLeft;
					ViewTop = grid.CurrentWorksheet.ViewTop;
					if (KeepSheetsInSync)
						grid2.ScrollCurrentWorksheet(e.OffsetX, e.OffsetY);
					inScrolling = false;
				}
			};

			// Sync scroll from control 2 to control 1
			grid2.WorksheetScrolled += (s, e) =>
			{
				if (!inScrolling)
				{
					inScrolling = true;
					var grid = s as ReoGridControl;
					ViewLeft = grid.CurrentWorksheet.ViewLeft;
					ViewTop = grid.CurrentWorksheet.ViewTop;
					if (KeepSheetsInSync)
						grid1.ScrollCurrentWorksheet(e.OffsetX, e.OffsetY);
					inScrolling = false;
				}
			};

			SuspendLayout();
			isUIUpdating = true;

			SetupUILanguage();

			fontToolStripComboBox.Text = Worksheet.DefaultStyle.FontName;

			fontSizeToolStripComboBox.Text = Worksheet.DefaultStyle.FontSize.ToString();
			fontSizeToolStripComboBox.Items.AddRange(FontUIToolkit.FontSizeList.Select(f => (object)f).ToArray());

			backColorPickerToolStripButton.CloseOnClick = true;
			borderColorPickToolStripItem.CloseOnClick = true;
			textColorPickToolStripItem.CloseOnClick = true;

			undoToolStripButton.Enabled = false;
			undoToolStripMenuItem.Enabled = false;
			redoToolStripButton.Enabled = false;
			redoToolStripMenuItem.Enabled = false;
			repeatLastActionToolStripMenuItem.Enabled = false;

			zoomToolStripDropDownButton.Text = "100%";

			isUIUpdating = false;

			toolbarToolStripMenuItem.Click += (s, e) => fontToolStrip.Visible = toolStrip1.Visible = toolbarToolStripMenuItem.Checked;
			formulaBarToolStripMenuItem.CheckedChanged += (s, e) => formulaBar.Visible = formulaBarToolStripMenuItem.Checked;
			statusBarToolStripMenuItem.CheckedChanged += (s, e) => statusStrip1.Visible = statusBarToolStripMenuItem.Checked;
			sheetSwitcherToolStripMenuItem.CheckedChanged += (s, e) =>
				GridControl.SetSettings(WorkbookSettings.View_ShowSheetTabControl, sheetSwitcherToolStripMenuItem.Checked);

			showHorizontaScrolllToolStripMenuItem.CheckedChanged += (s, e) =>
				GridControl.SetSettings(WorkbookSettings.View_ShowHorScroll, showHorizontaScrolllToolStripMenuItem.Checked);
			showVerticalScrollbarToolStripMenuItem.CheckedChanged += (s, e) =>
				GridControl.SetSettings(WorkbookSettings.View_ShowVerScroll, showVerticalScrollbarToolStripMenuItem.Checked);

			showGridLinesToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.View_ShowGridLine, showGridLinesToolStripMenuItem.Checked);
			showPageBreakToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.View_ShowPageBreaks, showPageBreakToolStripMenuItem.Checked);
			showFrozenLineToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.View_ShowFrozenLine, showFrozenLineToolStripMenuItem.Checked);
			showRowHeaderToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.View_ShowRowHeader, showRowHeaderToolStripMenuItem.Checked);
			showColumnHeaderToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.View_ShowColumnHeader, showColumnHeaderToolStripMenuItem.Checked);
			showRowOutlineToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.View_AllowShowRowOutlines, showRowOutlineToolStripMenuItem.Checked);
			showColumnOutlineToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.View_AllowShowColumnOutlines, showColumnOutlineToolStripMenuItem.Checked);

			sheetReadonlyToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.Edit_Readonly, sheetReadonlyToolStripMenuItem.Checked);

			resetAllPageBreaksToolStripMenuItem.Click += (s, e) => CurrentWorksheet.ResetAllPageBreaks();
			resetAllPageBreaksToolStripMenuItem1.Click += (s, e) => CurrentWorksheet.ResetAllPageBreaks();

			selModeNoneToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionMode = WorksheetSelectionMode.None;
			selModeCellToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionMode = WorksheetSelectionMode.Cell;
			selModeRangeToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionMode = WorksheetSelectionMode.Range;
			selModeRowToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionMode = WorksheetSelectionMode.Row;
			selModeColumnToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionMode = WorksheetSelectionMode.Column;

			selStyleNoneToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionStyle = WorksheetSelectionStyle.None;
			selStyleDefaultToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionStyle = WorksheetSelectionStyle.Default;
			selStyleFocusRectToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionStyle = WorksheetSelectionStyle.FocusRect;

			selDirRightToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionForwardDirection = SelectionForwardDirection.Right;
			selDirDownToolStripMenuItem.Click += (s, e) => GridControl.CurrentWorksheet.SelectionForwardDirection = SelectionForwardDirection.Down;

			zoomToolStripDropDownButton.TextChanged += zoomToolStripDropDownButton_TextChanged;

			undoToolStripButton.Click += Undo;
			redoToolStripButton.Click += Redo;
			undoToolStripMenuItem.Click += Undo;
			redoToolStripMenuItem.Click += Redo;

			mergeRangeToolStripMenuItem.Click += MergeSelectionRange;
			cellMergeToolStripButton.Click += MergeSelectionRange;
			unmergeRangeToolStripMenuItem.Click += UnmergeSelectionRange;
			unmergeRangeToolStripButton.Click += UnmergeSelectionRange;
			mergeCellsToolStripMenuItem.Click += MergeSelectionRange;
			unmergeCellsToolStripMenuItem.Click += UnmergeSelectionRange;
			formatCellsToolStripMenuItem.Click += formatCellToolStripMenuItem_Click;
			resizeToolStripMenuItem.Click += resizeToolStripMenuItem_Click;
			textWrapToolStripButton.Click += textWrapToolStripButton_Click;

			rowHeightToolStripMenuItem.Click += (s, e) =>
			{
				var worksheet = CurrentWorksheet;

				using (SetWidthOrHeightDialog rowHeightForm = new SetWidthOrHeightDialog(RowOrColumn.Row))
				{
					rowHeightForm.Value = worksheet.GetRowHeight(worksheet.SelectionRange.Row);

					if (rowHeightForm.ShowDialog() == DialogResult.OK)
					{
						GridControl.DoAction(new SetRowsHeightAction(worksheet.SelectionRange.Row,
							worksheet.SelectionRange.Rows, (ushort)rowHeightForm.Value));
					}
				}
			};

			columnWidthToolStripMenuItem.Click += (s, e) =>
			{
				var worksheet = CurrentWorksheet;

				using (SetWidthOrHeightDialog colWidthForm = new SetWidthOrHeightDialog(RowOrColumn.Column))
				{
					colWidthForm.Value = worksheet.GetColumnWidth(worksheet.SelectionRange.Col);

					if (colWidthForm.ShowDialog() == DialogResult.OK)
					{
						GridControl.DoAction(new SetColumnsWidthAction(worksheet.SelectionRange.Col,
							worksheet.SelectionRange.Cols, (ushort)colWidthForm.Value));
					}
				}
			};

			exportAsHtmlToolStripMenuItem.Click += (s, e) =>
			{
				using (SaveFileDialog sfd = new SaveFileDialog())
				{
					sfd.Filter = "HTML File(*.html;*.htm)|*.html;*.htm";
					sfd.FileName = "Exported ReoGrid Worksheet";

					if (sfd.ShowDialog() == DialogResult.OK)
					{
						using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create))
						{
							CurrentWorksheet.ExportAsHTML(fs);
						}

						Process.Start(sfd.FileName);
					}
				}
			};

			saveToolStripButton.Click += (s, e) => Save(Side.Left | Side.Right);
			saveToolStripMenuItem.Click += (s, e) => Save(Side.Left | Side.Right);
			saveLeftToolStripMenuItem.Click += (s, e) => Save(Side.Left);
			saveRightToolStripMenuItem.Click += (s, e) => Save(Side.Right);
			saveAsLeftToolStripMenuItem.Click += (s, e) => SaveAs(Side.Left);
			saveAsRightToolStripMenuItem.Click += (s, e) => SaveAs(Side.Right);

			groupRowsToolStripMenuItem.Click += groupRowsToolStripMenuItem_Click;
			groupRowsToolStripMenuItem1.Click += groupRowsToolStripMenuItem_Click;
			ungroupRowsToolStripMenuItem.Click += ungroupRowsToolStripMenuItem_Click;
			ungroupRowsToolStripMenuItem1.Click += ungroupRowsToolStripMenuItem_Click;
			ungroupAllRowsToolStripMenuItem.Click += ungroupAllRowsToolStripMenuItem_Click;
			ungroupAllRowsToolStripMenuItem1.Click += ungroupAllRowsToolStripMenuItem_Click;

			groupColumnsToolStripMenuItem.Click += groupColumnsToolStripMenuItem_Click;
			groupColumnsToolStripMenuItem1.Click += groupColumnsToolStripMenuItem_Click;
			ungroupColumnsToolStripMenuItem.Click += ungroupColumnsToolStripMenuItem_Click;
			ungroupColumnsToolStripMenuItem1.Click += ungroupColumnsToolStripMenuItem_Click;
			ungroupAllColumnsToolStripMenuItem.Click += ungroupAllColumnsToolStripMenuItem_Click;
			ungroupAllColumnsToolStripMenuItem1.Click += ungroupAllColumnsToolStripMenuItem_Click;

			hideRowsToolStripMenuItem.Click += (s, e) => GridControl.DoAction(new HideRowsAction(
				CurrentWorksheet.SelectionRange.Row, CurrentWorksheet.SelectionRange.Rows));
			unhideRowsToolStripMenuItem.Click += (s, e) => GridControl.DoAction(new UnhideRowsAction(
				CurrentWorksheet.SelectionRange.Row, CurrentWorksheet.SelectionRange.Rows));

			hideColumnsToolStripMenuItem.Click += (s, e) => GridControl.DoAction(new HideColumnsAction(
				CurrentWorksheet.SelectionRange.Col, CurrentWorksheet.SelectionRange.Cols));
			unhideColumnsToolStripMenuItem.Click += (s, e) => GridControl.DoAction(new UnhideColumnsAction(
				CurrentWorksheet.SelectionRange.Col, CurrentWorksheet.SelectionRange.Cols));

			// freeze to cell / edges
			freezeToCellToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.LeftTop);
			freezeToLeftToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.Left);
			freezeToTopToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.Top);
			freezeToRightToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.Right);
			freezeToBottomToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.Bottom);
			freezeToLeftTopToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.LeftTop);
			freezeToLeftBottomToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.LeftBottom);
			freezeToRightTopToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.RightTop);
			freezeToRightBottomToolStripMenuItem.Click += (s, e) => FreezeToEdge(FreezeArea.RightBottom);

			defineNamedRangeToolStripMenuItem.Click += (s, e) =>
			{
				var sheet = CurrentWorksheet;

				var name = sheet.GetNameByRange(sheet.SelectionRange);
				NamedRange namedRange = null;

				if (!string.IsNullOrEmpty(name))
				{
					namedRange = sheet.GetNamedRange(name);
				}

				using (DefineNamedRangeDialog dnrf = new DefineNamedRangeDialog())
				{
					dnrf.Range = sheet.SelectionRange;
					if (namedRange != null)
					{
						dnrf.RangeName = name;
						dnrf.Comment = namedRange.Comment;
					}

					if (dnrf.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					{
						var newName = dnrf.RangeName;

						var existedRange = sheet.GetNamedRange(newName);
						if (existedRange != null)
						{
							if (MessageBox.Show(this, LangRes.LangResource.Msg_Named_Range_Overwrite,
								Application.ProductName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
								== System.Windows.Forms.DialogResult.Cancel)
							{
								return;
							}

							sheet.UndefineNamedRange(newName);
						}

						var range = NamedRangeManageForm.DefineNamedRange(this, sheet, newName, dnrf.Comment, dnrf.Range);

						if (formulaBar != null && formulaBar.Visible)
						{
							formulaBar.RefreshCurrentAddress();
						}
					}
				}
			};

			nameManagerToolStripMenuItem.Click += (s, e) =>
			{
				if (nameManagerForm == null || nameManagerForm.IsDisposed)
				{
					nameManagerForm = new NamedRangeManageForm(GridControl);
				}

				nameManagerForm.Show(this);
			};

			tracePrecedentsToolStripMenuItem.Click += (s, e) => CurrentWorksheet.TraceCellPrecedents(CurrentWorksheet.FocusPos);
			traceDependentsToolStripMenuItem.Click += (s, e) => CurrentWorksheet.TraceCellDependents(CurrentWorksheet.FocusPos);

			removeAllArrowsToolStripMenuItem.Click += (s, e) => CurrentWorksheet.RemoveRangeAllTraceArrows(CurrentWorksheet.SelectionRange);
			removePrecedentArrowsToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.IterateCells(CurrentWorksheet.SelectionRange, (r, c, cell) =>
					CurrentWorksheet.RemoveCellTracePrecedents(cell));
			removeDependentArrowsToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.IterateCells(CurrentWorksheet.SelectionRange, (r, c, cell) =>
					CurrentWorksheet.RemoveCellTraceDependents(cell));

			columnPropertiesToolStripMenuItem.Click += (s, e) =>
			{
				var worksheet = CurrentWorksheet;

				int index = worksheet.SelectionRange.Col;
				int count = worksheet.SelectionRange.Cols;

				using (var hf = new HeaderPropertyDialog(RowOrColumn.Column))
				{
					var sampleHeader = worksheet.ColumnHeaders[index];

					hf.HeaderText = sampleHeader.Text;
					hf.HeaderTextColor = sampleHeader.TextColor ?? Color.Empty;
					hf.DefaultCellBody = sampleHeader.DefaultCellBody;
					hf.AutoFitToCell = sampleHeader.IsAutoWidth;

					if (hf.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					{
						var newText = string.IsNullOrEmpty(hf.HeaderText) ? null : hf.HeaderText;

						for (int i = index; i < index + count; i++)
						{
							var header = worksheet.ColumnHeaders[i];

							if (string.IsNullOrEmpty(header.Text) || newText == null)
							{
								header.Text = newText;
							}

							header.TextColor = hf.HeaderTextColor;
							header.DefaultCellBody = hf.DefaultCellBody;
							header.IsAutoWidth = hf.AutoFitToCell;
						}
					}
				}
			};

			rowPropertiesToolStripMenuItem.Click += (s, e) =>
			{
				var sheet = formulaBar.GridControl.CurrentWorksheet;

				int index = sheet.SelectionRange.Row;
				int count = sheet.SelectionRange.Rows;

				using (var hpf = new HeaderPropertyDialog(RowOrColumn.Row))
				{
					var sampleHeader = sheet.RowHeaders[index];

					hpf.HeaderText = sampleHeader.Text;
					hpf.HeaderTextColor = sampleHeader.TextColor ?? Color.Empty;
					hpf.DefaultCellBody = sampleHeader.DefaultCellBody;
					hpf.RowHeaderWidth = sheet.RowHeaderWidth;
					hpf.AutoFitToCell = sampleHeader.IsAutoHeight;

					if (hpf.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					{
						var newText = string.IsNullOrEmpty(hpf.HeaderText) ? null : hpf.HeaderText;

						for (int i = index; i < index + count; i++)
						{
							var header = sheet.RowHeaders[i];

							if (string.IsNullOrEmpty(header.Text) || newText == null)
							{
								header.Text = newText;
							}

							header.TextColor = hpf.HeaderTextColor;
							header.DefaultCellBody = hpf.DefaultCellBody;
							header.IsAutoHeight = hpf.AutoFitToCell;
						}

						if (hpf.RowHeaderWidth != sheet.RowHeaderWidth)
						{
							sheet.RowHeaderWidth = hpf.RowHeaderWidth;
						}
					}
				}
			};

			rowCutToolStripMenuItem.Click += cutRangeToolStripMenuItem_Click;
			rowCopyToolStripMenuItem.Click += copyRangeToolStripMenuItem_Click;
			rowPasteToolStripMenuItem.Click += pasteRangeToolStripMenuItem_Click;

			colCutToolStripMenuItem.Click += cutRangeToolStripMenuItem_Click;
			colCopyToolStripMenuItem.Click += copyRangeToolStripMenuItem_Click;
			colPasteToolStripMenuItem.Click += pasteRangeToolStripMenuItem_Click;

			rowFormatCellsToolStripMenuItem.Click += formatCellToolStripMenuItem_Click;
			colFormatCellsToolStripMenuItem.Click += formatCellToolStripMenuItem_Click;

			printSettingsToolStripMenuItem.Click += printSettingsToolStripMenuItem_Click;

			printToolStripMenuItem.Click += PrintToolStripMenuItem_Click;

			var noneTypeMenuItem = new ToolStripMenuItem(LangResource.None);
			noneTypeMenuItem.Click += cellTypeNoneMenuItem_Click;
			changeCellsTypeToolStripMenuItem.DropDownItems.Add(noneTypeMenuItem);
			changeCellsTypeToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());

			var noneTypeMenuItem2 = new ToolStripMenuItem(LangResource.None);
			noneTypeMenuItem2.Click += cellTypeNoneMenuItem_Click;
			changeCellsTypeToolStripMenuItem2.DropDownItems.Add(noneTypeMenuItem2);
			changeCellsTypeToolStripMenuItem2.DropDownItems.Add(new ToolStripSeparator());

			foreach (var cellType in CellTypesManager.CellTypes)
			{
				var name = cellType.Key;
				if (name.EndsWith("Cell"))
					name = name.Substring(0, name.Length - 4);

				var menuItem = new ToolStripMenuItem(name)
				{
					Tag = cellType.Value,
				};

				menuItem.Click += cellTypeMenuItem_Click;
				changeCellsTypeToolStripMenuItem.DropDownItems.Add(menuItem);

				var menuItem2 = new ToolStripMenuItem(name)
				{
					Tag = cellType.Value,
				};

				menuItem2.Click += cellTypeMenuItem_Click;
				changeCellsTypeToolStripMenuItem2.DropDownItems.Add(menuItem2);
			}

			rowContextMenuStrip.Opening += (s, e) =>
			{
				insertRowPageBreakToolStripMenuItem.Enabled = !GridControl.CurrentWorksheet.PrintableRange.IsEmpty;
				removeRowPageBreakToolStripMenuItem.Enabled = GridControl.CurrentWorksheet.RowPageBreaks.Contains(GridControl.CurrentWorksheet.FocusPos.Row);
			};

			columnContextMenuStrip.Opening += (s, e) =>
			{
				insertColPageBreakToolStripMenuItem.Enabled = !GridControl.CurrentWorksheet.PrintableRange.IsEmpty;
				removeColPageBreakToolStripMenuItem.Enabled = GridControl.CurrentWorksheet.ColumnPageBreaks.Contains(GridControl.CurrentWorksheet.FocusPos.Col);
			};

			AutoFunctionSumToolStripMenuItem.Click += (s, e) => ApplyFunctionToSelectedRange("SUM");
			AutoFunctionAverageToolStripMenuItem.Click += (s, e) => ApplyFunctionToSelectedRange("AVERAGE");
			AutoFunctionCountToolStripMenuItem.Click += (s, e) => ApplyFunctionToSelectedRange("COUNT");
			AutoFunctionMaxToolStripMenuItem.Click += (s, e) => ApplyFunctionToSelectedRange("MAX");
			AutoFunctionMinToolStripMenuItem.Click += (s, e) => ApplyFunctionToSelectedRange("MIN");

			focusStyleDefaultToolStripMenuItem.CheckedChanged += (s, e) =>
			{
				if (focusStyleDefaultToolStripMenuItem.Checked) CurrentWorksheet.FocusPosStyle = FocusPosStyle.Default;
			};
			focusStyleNoneToolStripMenuItem.CheckedChanged += (s, e) =>
			{
				if (focusStyleNoneToolStripMenuItem.Checked) CurrentWorksheet.FocusPosStyle = FocusPosStyle.None;
			};

			homepageToolStripMenuItem.Click += (s, e) =>
			{
				try
				{
					Process.Start(LangResource.HP_Homepage);
				}
				catch { }
			};

			documentationToolStripMenuItem.Click += (s, e) =>
			{
				try
				{
					Process.Start(LangResource.HP_Homepage_Document);
				}
				catch { }
			};

			insertColPageBreakToolStripMenuItem.Click += insertColPageBreakToolStripMenuItem_Click;
			insertRowPageBreakToolStripMenuItem.Click += insertRowPageBreakToolStripMenuItem_Click;
			removeColPageBreakToolStripMenuItem.Click += removeColPageBreakToolStripMenuItem_Click;
			removeRowPageBreakToolStripMenuItem.Click += removeRowPageBreakToolStripMenuItem_Click;

			filterToolStripMenuItem.Click += filterToolStripMenuItem_Click;
			clearFilterToolStripMenuItem.Click += clearFilterToolStripMenuItem_Click;
			columnFilterToolStripMenuItem.Click += filterToolStripMenuItem_Click;
			clearColumnFilterToolStripMenuItem.Click += clearFilterToolStripMenuItem_Click;

			grid1.ExceptionHappened += (s, e) =>
			{
				if (e.Exception is RangeIntersectionException)
				{
					MessageBox.Show(this, LangResource.Msg_Range_Intersection_Exception,
						"ReoGrid Editor", MessageBoxButtons.OK, MessageBoxIcon.Stop);
				}
				else if (e.Exception is OperationOnReadonlyCellException)
				{
					MessageBox.Show(this, LangResource.Msg_Operation_Aborted,
						"ReoGrid Editor", MessageBoxButtons.OK, MessageBoxIcon.Stop);
				}
			};

			grid1.SettingsChanged += (s, e) =>
				{
					sheetSwitcherToolStripMenuItem.Checked = GridControl.HasSettings(WorkbookSettings.View_ShowSheetTabControl);
					showHorizontaScrolllToolStripMenuItem.Checked = GridControl.HasSettings(WorkbookSettings.View_ShowHorScroll);
					showVerticalScrollbarToolStripMenuItem.Checked = GridControl.HasSettings(WorkbookSettings.View_ShowVerScroll);
				};

			clearAllToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.ClearRangeContent(CurrentSelectionRange, CellElementFlag.All);
			clearDataToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.ClearRangeContent(CurrentSelectionRange, CellElementFlag.Data);
			clearDataFormatToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.ClearRangeContent(CurrentSelectionRange, CellElementFlag.DataFormat);
			clearFormulaToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.ClearRangeContent(CurrentSelectionRange, CellElementFlag.Formula);
			clearCellBodyToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.ClearRangeContent(CurrentSelectionRange, CellElementFlag.Body);
			clearStylesToolStripMenuItem.Click += (s, e) =>
				CurrentWorksheet.ClearRangeContent(CurrentSelectionRange, CellElementFlag.Style);
			clearBordersToolStripButton.Click += (s, e) =>
				CurrentWorksheet.ClearRangeContent(CurrentSelectionRange, CellElementFlag.Border);

			exportCurrentWorksheetToolStripMenuItem.Click += (s, e) => ExportAsCsv(RangePosition.EntireRange);
			exportSelectedRangeToolStripMenuItem.Click += (s, e) => ExportAsCsv(CurrentSelectionRange);

			dragToMoveRangeToolStripMenuItem.CheckedChanged += (s, e) => CurrentWorksheet.SetSettings(
				WorksheetSettings.Edit_DragSelectionToMoveCells, dragToMoveRangeToolStripMenuItem.Checked);
			dragToFillSerialToolStripMenuItem.CheckedChanged += (s, e) => CurrentWorksheet.SetSettings(
				WorksheetSettings.Edit_DragSelectionToFillSerial, dragToFillSerialToolStripMenuItem.Checked);

			suspendReferenceUpdatingToolStripMenuItem.CheckedChanged += (s, e) =>
				CurrentWorksheet.SetSettings(WorksheetSettings.Formula_AutoUpdateReferenceCell,
				!suspendReferenceUpdatingToolStripMenuItem.Checked);

			recalculateWorksheetToolStripMenuItem.Click += (s, e) => CurrentWorksheet.Recalculate();

#if RG_DEBUG

			showDebugFormToolStripButton.Click += new System.EventHandler(showDebugFormToolStripButton_Click);

			#region Debug Validation Events
			grid1.Undid += (s, e) => _Debug_Auto_Validate_All(((BaseWorksheetAction)e.Action).Worksheet);
			grid1.Redid += (s, e) => _Debug_Auto_Validate_All(((BaseWorksheetAction)e.Action).Worksheet);
			grid2.Undid += (s, e) => _Debug_Auto_Validate_All(((BaseWorksheetAction)e.Action).Worksheet);
			grid2.Redid += (s, e) => _Debug_Auto_Validate_All(((BaseWorksheetAction)e.Action).Worksheet);

			showDebugInfoToolStripMenuItem.Click += (s, e) =>
			{
				showDebugFormToolStripButton.PerformClick();
				showDebugInfoToolStripMenuItem.Checked = showDebugFormToolStripButton.Checked;
			};

			validateBorderSpanToolStripMenuItem.Click += (s, e) => _Debug_Validate_BorderSpan(CurrentWorksheet, true);
			validateMergedRangeToolStripMenuItem.Click += (s, e) => _Debug_Validate_Merged_Cell(CurrentWorksheet, true);
			validateAllToolStripMenuItem.Click += (s, e) => _Debug_Validate_All(CurrentWorksheet, true);

			#endregion // Debug Validation Events
#endif // RG_DEBUG

			ResumeLayout();
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			switch (keyData)
			{
				case Keys.F6:
					// Switch between panes
					(header1.Active ? grid2 : grid1).Focus();
					return true;
				case Keys.Alt | Keys.Down:
					nextDiffToolStripButton.PerformClick();
					return true;
				case Keys.Alt | Keys.Up:
					prevDiffToolStripButton.PerformClick();
					return true;
				case Keys.Alt | Keys.Home:
					firstDiffToolStripButton.PerformClick();
					return true;
				case Keys.Alt | Keys.End:
					lastDiffToolStripButton.PerformClick();
					return true;
				case Keys.Alt | Keys.Right:
					left2rightToolStripButton.PerformClick();
					return true;
				case Keys.Alt | Keys.Left:
					right2leftToolStripButton.PerformClick();
					return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void toolStripButton_EnabledChanged(object sender, EventArgs e)
		{
			AutomationEventArgs args = new AutomationEventArgs(InvokePatternIdentifiers.InvokedEvent);
			var provider = AutomationInteropProvider.HostProviderFromHandle(toolStrip1.Handle);
			AutomationInteropProvider.RaiseAutomationEvent(InvokePatternIdentifiers.InvokedEvent, provider, args);
		}

		private void ExportAsCsv(RangePosition range)
		{
			using (SaveFileDialog dlg = new SaveFileDialog())
			{
				dlg.Filter = LangResource.Filter_Export_As_CSV;
				dlg.FileName = Path.GetFileNameWithoutExtension(CurrentFilePath);

				if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					using (FileStream fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write, FileShare.Read))
					{
						CurrentWorksheet.ExportAsCSV(fs, range);
					}

#if DEBUG
					Process.Start(dlg.FileName);
#endif
				}
			};
		}

		void worksheet_SelectionModeChanged(object sender, EventArgs e)
		{
			UpdateSelectionModeAndStyle();
		}

		void worksheet_SelectionForwardDirectionChanged(object sender, EventArgs e)
		{
			UpdateSelectionForwardDirection();
		}

		void worksheet_Resetted(object sender, EventArgs e)
		{
			statusToolStripStatusLabel.Text = string.Empty;
		}

		void worksheet_SettingsChanged(object sender, SettingsChangedEventArgs e)
		{
			var worksheet = sender as Worksheet;
			if (worksheet != null) UpdateWorksheetSettings(worksheet);
		}

		void UpdateWorksheetSettings(Worksheet sheet)
		{
			bool visible = false;

			visible = sheet.HasSettings(WorksheetSettings.View_ShowGridLine);
			if (showGridLinesToolStripMenuItem.Checked != visible) showGridLinesToolStripMenuItem.Checked = visible;

			visible = sheet.HasSettings(WorksheetSettings.View_ShowPageBreaks);
			if (showPageBreakToolStripMenuItem.Checked != visible) showPageBreakToolStripMenuItem.Checked = visible;

			visible = sheet.HasSettings(WorksheetSettings.View_ShowFrozenLine);
			if (showFrozenLineToolStripMenuItem.Checked != visible) showFrozenLineToolStripMenuItem.Checked = visible;

			visible = sheet.HasSettings(WorksheetSettings.View_ShowRowHeader);
			if (showRowHeaderToolStripMenuItem.Checked != visible) showRowHeaderToolStripMenuItem.Checked = visible;

			visible = sheet.HasSettings(WorksheetSettings.View_ShowColumnHeader);
			if (showColumnHeaderToolStripMenuItem.Checked != visible) showColumnHeaderToolStripMenuItem.Checked = visible;

			visible = sheet.HasSettings(WorksheetSettings.View_AllowShowRowOutlines);
			if (showRowOutlineToolStripMenuItem.Checked != visible) showRowOutlineToolStripMenuItem.Checked = visible;

			visible = sheet.HasSettings(WorksheetSettings.View_AllowShowColumnOutlines);
			if (showColumnOutlineToolStripMenuItem.Checked != visible) showColumnOutlineToolStripMenuItem.Checked = visible;

			var check = sheet.HasSettings(WorksheetSettings.Edit_DragSelectionToMoveCells);
			if (dragToMoveRangeToolStripMenuItem.Checked != check) dragToMoveRangeToolStripMenuItem.Checked = check;

			check = sheet.HasSettings(WorksheetSettings.Edit_DragSelectionToFillSerial);
			if (dragToFillSerialToolStripMenuItem.Checked != check) dragToFillSerialToolStripMenuItem.Checked = check;

			check = !sheet.HasSettings(WorksheetSettings.Formula_AutoUpdateReferenceCell);
			if (suspendReferenceUpdatingToolStripMenuItem.Checked != check) suspendReferenceUpdatingToolStripMenuItem.Checked = check;

			sheetReadonlyToolStripMenuItem.Checked = sheet.HasSettings(WorksheetSettings.Edit_Readonly);

		}

		void cellTypeNoneMenuItem_Click(object sender, EventArgs e)
		{
			var worksheet = CurrentWorksheet;

			if (worksheet != null)
			{
				worksheet.IterateCells(worksheet.SelectionRange, false, (r, c, cell) =>
				{
					if (cell != null)
						cell.Body = null;
					return true;
				});
			}
		}

		void cellTypeMenuItem_Click(object sender, EventArgs e)
		{
			var worksheet = CurrentWorksheet;

			var menuItem = sender as ToolStripMenuItem;

			if (menuItem != null && menuItem.Tag is Type && worksheet != null)
			{
				foreach (var cell in worksheet.Ranges[worksheet.SelectionRange].Cells)
				{
					cell.Body = System.Activator.CreateInstance((Type)menuItem.Tag) as ICellBody;
				}
			}
		}

		void textWrapToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentSelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.TextWrap,
				TextWrapMode = textWrapToolStripButton.Checked ? TextWrapMode.WordBreak : TextWrapMode.NoWrap,
			}));
		}

		void worksheet_GridScaled(object sender, EventArgs e)
		{
			var worksheet = sender as Worksheet;
			// Don't synchronize with a hidden grid
			if (grid1.GetWorksheetIndex(worksheet) != -1 && grid1.Visible ||
				grid2.GetWorksheetIndex(worksheet) != -1 && grid2.Visible)
			{
				zoomToolStripDropDownButton.Text = worksheet.ScaleFactor * 100 + "%";
			}
		}

		void worksheet_ColumnsWidthChanged(object sender, ColumnsWidthChangedEventArgs e)
		{
			if (KeepSheetsInSync)
			{
				var worksheet = sender != grid1.CurrentWorksheet ? grid1.CurrentWorksheet : grid2.CurrentWorksheet;
				worksheet.ColumnsWidthChanged -= worksheet_ColumnsWidthChanged;
				worksheet.SetColumnsWidth(e.Index, e.Count, (ushort)e.Width);
				worksheet.ColumnsWidthChanged += worksheet_ColumnsWidthChanged;
			}
		}

		void worksheet_RowsHeightChanged(object sender, RowsHeightChangedEventArgs e)
		{
			if (KeepSheetsInSync)
			{
				var worksheet = sender != grid1.CurrentWorksheet ? grid1.CurrentWorksheet : grid2.CurrentWorksheet;
				worksheet.RowsHeightChanged -= worksheet_RowsHeightChanged;
				worksheet.SetRowsHeight(e.Row, e.Count, (ushort)e.Height, e.HeightGetter);
				worksheet.RowsHeightChanged += worksheet_RowsHeightChanged;
			}
		}

		#endregion // Constructor

		#region Utility

#if RG_DEBUG
		#region Debug Validations
		/// <summary>
		/// Use for Debug mode. Check for border span is valid.
		/// </summary>
		/// <param name="showSuccessMsg"></param>
		/// <returns></returns>
		bool _Debug_Validate_BorderSpan(Worksheet sheet, bool showSuccessMsg)
		{
			bool rs = sheet._Debug_Validate_BorderSpan();

			if (rs)
			{
				if (showSuccessMsg) ShowStatus("Border span validation ok.");
			}
			else
			{
				ShowError("Border span test failed.");

				if (!showDebugInfoToolStripMenuItem.Checked)
				{
					showDebugInfoToolStripMenuItem.PerformClick();
				}
			}

			return rs;
		}
		bool _Debug_Validate_Merged_Cell(Worksheet sheet, bool showSuccessMsg)
		{
			bool rs = sheet._Debug_Validate_MergedCells();

			if (rs)
			{
				if (showSuccessMsg) ShowStatus("Merged range validation ok.");
			}
			else
			{
				ShowError("Merged range validation failed.");

				if (!showDebugInfoToolStripMenuItem.Checked)
				{
					showDebugInfoToolStripMenuItem.PerformClick();
				}
			}

			return rs;
		}
		bool _Debug_Validate_Unmerged_Range(Worksheet sheet, bool showSuccessMsg, RangePosition range)
		{
			bool rs = sheet._Debug_Validate_Unmerged_Range(range);

			if (rs)
			{
				if (showSuccessMsg) ShowStatus("Unmerged range validation ok.");
			}
			else
			{
				ShowError("Unmerged range validation failed.");

				if (!showDebugInfoToolStripMenuItem.Checked)
				{
					showDebugInfoToolStripMenuItem.PerformClick();
				}
			}

			return rs;
		}
		bool _Debug_Validate_All(Worksheet sheet, bool showSuccessMsg)
		{
			return _Debug_Validate_All(sheet, showSuccessMsg, RangePosition.EntireRange);
		}
		bool _Debug_Validate_All(Worksheet sheet, RangePosition range)
		{
			return _Debug_Validate_All(sheet, false, range);
		}
		bool _Debug_Validate_All(Worksheet sheet, bool showSuccessMsg, RangePosition range)
		{
			bool rs = _Debug_Validate_BorderSpan(sheet, showSuccessMsg);
			if (rs) rs = _Debug_Validate_Merged_Cell(sheet, showSuccessMsg);
			if (rs) rs = _Debug_Validate_Unmerged_Range(sheet, showSuccessMsg, range);

			return rs;
		}
		bool _Debug_Auto_Validate_All(Worksheet sheet) { return _Debug_Validate_All(sheet, false); }
		bool _Debug_Auto_Validate_All(Worksheet sheet, RangePosition range) { return _Debug_Validate_All(sheet, range); }
		#endregion // Debug Validations
#endif // RG_DEBUG

		public ReoGridControl GridControl { get { return formulaBar.GridControl; } }

		public Worksheet CurrentWorksheet { get { return GridControl.CurrentWorksheet; } }

		public RangePosition CurrentSelectionRange
		{
			get { return CurrentWorksheet.SelectionRange; }
			set { CurrentWorksheet.SelectionRange = value; }
		}

		internal void ShowStatus(string msg)
		{
			ShowStatus(msg, false);
		}
		internal void ShowStatus(string msg, bool error)
		{
			statusToolStripStatusLabel.Text = msg;
			statusToolStripStatusLabel.ForeColor = error ? Color.Red : SystemColors.WindowText;
		}
		public void ShowError(string msg)
		{
			ShowStatus(msg, true);
		}

		private void UpdateMenuAndToolStripsWhenAction(object sender, EventArgs e)
		{
			if (sender == GridControl)
				UpdateMenuAndToolStrips();
		}

		private void Undo(object sender, EventArgs e)
		{
			var action = undoStack.Last();
			undoStack.RemoveLast();
			redoStack.AddLast(action);
			if (grid1.CanUndo(action))
			{
				grid1.Undo();
			}
			else if (grid2.CanUndo(action))
			{
				grid2.Undo();
			}
		}

		private void Redo(object sender, EventArgs e)
		{
			var action = redoStack.Last();
			redoStack.RemoveLast();
			undoStack.AddLast(action);
			if (grid1.CanRedo(action))
			{
				grid1.Redo();
			}
			else if (grid2.CanRedo(action))
			{
				grid2.Redo();
			}
		}

		void zoomToolStripDropDownButton_TextChanged(object sender, EventArgs e)
		{
			if (isUIUpdating) return;

			if (zoomToolStripDropDownButton.Text.Length > 0)
			{
				int value = 0;
				if (int.TryParse(zoomToolStripDropDownButton.Text.Substring(0, zoomToolStripDropDownButton.Text.Length - 1), out value))
				{
					float scale = value / 100f;
					scale = (float)Math.Round(scale, 1);

					grid1.CurrentWorksheet.SetScale(scale);
					grid2.CurrentWorksheet.SetScale(scale);
				}
			}
		}

		void grid_SelectionRangeChanged(object sender, RangeEventArgs e)
		{
			// get event source worksheet
			var worksheet = sender as Worksheet;

			// if source worksheet is current worksheet, update menus and tool strips
			if (worksheet == CurrentWorksheet)
			{
				if (worksheet.SelectionRange == RangePosition.Empty)
				{
					rangeInfoToolStripStatusLabel.Text = "Selection None";
				}
				else
				{
					rangeInfoToolStripStatusLabel.Text =
						string.Format("{0} {1} x {2}", worksheet.SelectionRange.ToString(),
						worksheet.SelectionRange.Rows, worksheet.SelectionRange.Cols);
				}

				UpdateMenuAndToolStrips();
			}
		}

		#endregion // Utility

		#region Language

		void SetupUILanguage()
		{
			#region Menu
			// File
			fileToolStripMenuItem.Text = LangResource.Menu_File;
			saveToolStripMenuItem.Text = LangResource.Menu_File_Save;
			exportAsHtmlToolStripMenuItem.Text = LangResource.Menu_File_Export_As_HTML;
			exportAsCSVToolStripMenuItem.Text = LangResource.Menu_File_Export_As_CSV;
			exportSelectedRangeToolStripMenuItem.Text = LangResource.Menu_File_Export_As_CSV_Selected_Range;
			exportCurrentWorksheetToolStripMenuItem.Text = LangResource.Menu_File_Export_As_CSV_Current_Worksheet;
			printPreviewToolStripMenuItem.Text = LangResource.Menu_File_Print_Preview;
			printSettingsToolStripMenuItem.Text = LangResource.Menu_File_Print_Settings;
			printToolStripMenuItem.Text = LangResource.Menu_File_Print;
			exitToolStripMenuItem.Text = LangResource.Menu_File_Exit;

			// Edit
			editToolStripMenuItem.Text = LangResource.Menu_Edit;
			undoToolStripMenuItem.Text = LangResource.Menu_Undo;
			redoToolStripMenuItem.Text = LangResource.Menu_Redo;
			repeatLastActionToolStripMenuItem.Text = LangResource.Menu_Edit_Repeat_Last_Action;
			cutToolStripMenuItem.Text = LangResource.Menu_Cut;
			copyToolStripMenuItem.Text = LangResource.Menu_Copy;
			pasteToolStripMenuItem.Text = LangResource.Menu_Paste;
			clearToolStripMenuItem.Text = LangResource.Menu_Edit_Clear;
			clearAllToolStripMenuItem.Text = LangResource.All;
			clearDataToolStripMenuItem.Text = LangResource.Data;
			clearDataFormatToolStripMenuItem.Text = LangResource.Data_Format;
			clearFormulaToolStripMenuItem.Text = LangResource.Formula;
			clearCellBodyToolStripMenuItem.Text = LangResource.CellBody;
			clearStylesToolStripMenuItem.Text = LangResource.Style;
			clearBordersToolStripMenuItem.Text = LangResource.Border;
			focusCellStyleToolStripMenuItem.Text = LangResource.Menu_Edit_Focus_Cell_Style;
			focusStyleDefaultToolStripMenuItem.Text = LangResource.Default;
			focusStyleNoneToolStripMenuItem.Text = LangResource.None;
			selectionToolStripMenuItem.Text = LangResource.Menu_Edit_Selection;
			dragToMoveRangeToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Drag_To_Move_Content;
			dragToFillSerialToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Drag_To_Fill_Serial;
			selectionStyleToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Style;
			selStyleDefaultToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Style_Default;
			selStyleFocusRectToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Style_Focus_Rect;
			selStyleNoneToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Style_None;
			selectionModeToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Mode;
			selModeNoneToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Mode_None;
			selModeCellToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Mode_Cell;
			selModeRangeToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Mode_Range;
			selModeRowToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Mode_Row;
			selModeColumnToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Mode_Column;
			selectionMoveDirectionToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Move_Direction;
			selDirRightToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Move_Direction_Right;
			selDirDownToolStripMenuItem.Text = LangResource.Menu_Edit_Selection_Move_Direction_Down;
			selectAllToolStripMenuItem.Text = LangResource.Menu_Edit_Select_All;

			// View
			viewToolStripMenuItem.Text = LangResource.Menu_View;
			componentsToolStripMenuItem.Text = LangResource.Menu_View_Components;
			toolbarToolStripMenuItem.Text = LangResource.Menu_View_Components_Toolbar;
			formulaBarToolStripMenuItem.Text = LangResource.Menu_View_Components_FormulaBar;
			statusBarToolStripMenuItem.Text = LangResource.Menu_View_Components_StatusBar;
			visibleToolStripMenuItem.Text = LangResource.Menu_View_Visible;
			showGridLinesToolStripMenuItem.Text = LangResource.Menu_View_Visible_Grid_Lines;
			showPageBreakToolStripMenuItem.Text = LangResource.Menu_View_Visible_Page_Breaks;
			showFrozenLineToolStripMenuItem.Text = LangResource.Menu_View_Visible_Forzen_Line;
			sheetSwitcherToolStripMenuItem.Text = LangResource.Menu_View_Visible_Sheet_Tab;
			showHorizontaScrolllToolStripMenuItem.Text = LangResource.Menu_View_Visible_Horizontal_ScrollBar;
			showVerticalScrollbarToolStripMenuItem.Text = LangResource.Menu_View_Visible_Vertical_ScrollBar;
			showRowHeaderToolStripMenuItem.Text = LangResource.Menu_View_Visible_Row_Header;
			showColumnHeaderToolStripMenuItem.Text = LangResource.Menu_View_Visible_Column_Header;
			showRowOutlineToolStripMenuItem.Text = LangResource.Menu_View_Visible_Row_Outline_Panel;
			showColumnOutlineToolStripMenuItem.Text = LangResource.Menu_View_Visible_Column_Outline_Panel;
			resetAllPageBreaksToolStripMenuItem.Text = LangResource.Menu_Reset_All_Page_Breaks;
			freezeToCellToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Cell;
			freezeToSpecifiedEdgeToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges;
			freezeToLeftToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Left;
			freezeToRightToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Right;
			freezeToTopToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Top;
			freezeToBottomToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Bottom;
			freezeToLeftTopToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Top_Left;
			freezeToLeftBottomToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Bottom_Left;
			freezeToRightTopToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Top_Right;
			freezeToRightBottomToolStripMenuItem.Text = LangResource.Menu_View_Freeze_To_Edges_Bottom_Right;
			unfreezeToolStripMenuItem.Text = LangResource.Menu_View_Unfreeze;

			// Cells
			cellsToolStripMenuItem.Text = LangResource.Menu_Cells;
			mergeCellsToolStripMenuItem.Text = LangResource.Menu_Cells_Merge_Cells;
			unmergeCellsToolStripMenuItem.Text = LangResource.Menu_Cells_Unmerge_Cells;
			changeCellsTypeToolStripMenuItem.Text = LangResource.Menu_Change_Cells_Type;
			formatCellsToolStripMenuItem.Text = LangResource.Menu_Format_Cells;

			// Sheet
			sheetToolStripMenuItem.Text = LangResource.Menu_Sheet;
			filterToolStripMenuItem.Text = LangResource.Menu_Sheet_Filter;
			clearFilterToolStripMenuItem.Text = LangResource.Menu_Sheet_Clear_Filter;
			groupToolStripMenuItem.Text = LangResource.Menu_Sheet_Group;
			groupRowsToolStripMenuItem.Text = LangResource.Menu_Sheet_Group_Rows;
			groupColumnsToolStripMenuItem.Text = LangResource.Menu_Sheet_Group_Columns;
			ungroupToolStripMenuItem.Text = LangResource.Menu_Sheet_Ungroup;
			ungroupRowsToolStripMenuItem.Text = LangResource.Menu_Sheet_Ungroup_Selection_Rows;
			ungroupAllRowsToolStripMenuItem.Text = LangResource.Menu_Sheet_Ungroup_All_Rows;
			ungroupColumnsToolStripMenuItem.Text = LangResource.Menu_Sheet_Ungroup_Selection_Columns;
			ungroupAllColumnsToolStripMenuItem.Text = LangResource.Menu_Sheet_Ungroup_All_Columns;
			insertToolStripMenuItem.Text = LangResource.Menu_Sheet_Insert;
			resizeToolStripMenuItem.Text = LangResource.Menu_Sheet_Resize;
			sheetReadonlyToolStripMenuItem.Text = LangResource.Menu_Edit_Readonly;

			// Formula
			formulaToolStripMenuItem.Text = LangResource.Menu_Formula;
			autoFunctionToolStripMenuItem.Text = LangResource.Menu_Formula_Auto_Function;
			defineNamedRangeToolStripMenuItem.Text = LangResource.Menu_Formula_Define_Name;
			nameManagerToolStripMenuItem.Text = LangResource.Menu_Formula_Name_Manager;
			tracePrecedentsToolStripMenuItem.Text = LangResource.Menu_Formula_Trace_Precedents;
			traceDependentsToolStripMenuItem.Text = LangResource.Menu_Formula_Trace_Dependents;
			removeArrowsToolStripMenuItem.Text = LangResource.Menu_Formula_Remove_Trace_Arrows;
			removeAllArrowsToolStripMenuItem.Text = LangResource.Menu_Formula_Remove_Trace_Arrows_Remove_All_Arrows;
			removePrecedentArrowsToolStripMenuItem.Text = LangResource.Menu_Formula_Remove_Trace_Arrows_Remove_Precedent_Arrows;
			removeDependentArrowsToolStripMenuItem.Text = LangResource.Menu_Formula_Remove_Trace_Arrows_Remove_Dependent_Arrows;
			suspendReferenceUpdatingToolStripMenuItem.Text = LangResource.Menu_Formula_Suspend_Reference_Updates;
			recalculateWorksheetToolStripMenuItem.Text = LangResource.Menu_Formula_Recalculate_Worksheet;

			// Tools
			toolsToolStripMenuItem.Text = LangResource.Menu_Tools;
			controlStyleToolStripMenuItem.Text = LangResource.Menu_Tools_Control_Appearance;
			helpToolStripMenuItem.Text = LangResource.Menu_Help;
			homepageToolStripMenuItem.Text = LangResource.Menu_Help_Homepage;
			documentationToolStripMenuItem.Text = LangResource.Menu_Help_Documents;
			aboutToolStripMenuItem.Text = LangResource.Menu_Help_About;

			// Column Context Menu
			colCutToolStripMenuItem.Text = LangResource.Menu_Cut;
			colCopyToolStripMenuItem.Text = LangResource.Menu_Copy;
			colPasteToolStripMenuItem.Text = LangResource.Menu_Paste;
			insertColToolStripMenuItem.Text = LangResource.CtxMenu_Col_Insert_Columns;
			deleteColumnToolStripMenuItem.Text = LangResource.CtxMenu_Col_Delete_Columns;
			resetToDefaultWidthToolStripMenuItem.Text = LangResource.CtxMenu_Col_Reset_To_Default_Width;
			columnWidthToolStripMenuItem.Text = LangResource.CtxMenu_Col_Column_Width;
			hideColumnsToolStripMenuItem.Text = LangResource.Menu_Hide;
			unhideColumnsToolStripMenuItem.Text = LangResource.Menu_Unhide;
			columnFilterToolStripMenuItem.Text = LangResource.CtxMenu_Col_Filter;
			clearColumnFilterToolStripMenuItem.Text = LangResource.CtxMenu_Col_Clear_Filter;
			groupColumnsToolStripMenuItem1.Text = LangResource.Menu_Group;
			ungroupColumnsToolStripMenuItem1.Text = LangResource.Menu_Ungroup;
			ungroupAllColumnsToolStripMenuItem1.Text = LangResource.Menu_Ungroup_All;
			insertColPageBreakToolStripMenuItem.Text = LangResource.Menu_Insert_Page_Break;
			removeColPageBreakToolStripMenuItem.Text = LangResource.Menu_Remove_Page_Break;
			columnPropertiesToolStripMenuItem.Text = LangResource.Menu_Property;
			colFormatCellsToolStripMenuItem.Text = LangResource.Menu_Format_Cells;

			// Row Context Menu
			rowCutToolStripMenuItem.Text = LangResource.Menu_Cut;
			rowCopyToolStripMenuItem.Text = LangResource.Menu_Copy;
			rowPasteToolStripMenuItem.Text = LangResource.Menu_Paste;
			insertRowToolStripMenuItem.Text = LangResource.CtxMenu_Row_Insert_Rows;
			deleteRowsToolStripMenuItem.Text = LangResource.CtxMenu_Row_Delete_Rows;
			resetToDefaultHeightToolStripMenuItem.Text = LangResource.CtxMenu_Row_Reset_to_Default_Height;
			rowHeightToolStripMenuItem.Text = LangResource.CtxMenu_Row_Row_Height;
			hideRowsToolStripMenuItem.Text = LangResource.Menu_Hide;
			unhideRowsToolStripMenuItem.Text = LangResource.Menu_Unhide;
			groupRowsToolStripMenuItem1.Text = LangResource.Menu_Group;
			ungroupRowsToolStripMenuItem1.Text = LangResource.Menu_Ungroup;
			ungroupAllRowsToolStripMenuItem1.Text = LangResource.Menu_Ungroup_All;
			insertRowPageBreakToolStripMenuItem.Text = LangResource.Menu_Insert_Page_Break;
			removeRowPageBreakToolStripMenuItem.Text = LangResource.Menu_Remove_Page_Break;
			rowPropertiesToolStripMenuItem.Text = LangResource.Menu_Property;
			rowFormatCellsToolStripMenuItem.Text = LangResource.Menu_Format_Cells;

			// Cell Context Menu
			cutRangeToolStripMenuItem.Text = LangResource.Menu_Cut;
			copyRangeToolStripMenuItem.Text = LangResource.Menu_Copy;
			pasteRangeToolStripMenuItem.Text = LangResource.Menu_Paste;
			mergeRangeToolStripMenuItem.Text = LangResource.CtxMenu_Cell_Merge;
			unmergeRangeToolStripMenuItem.Text = LangResource.CtxMenu_Cell_Unmerge;
			changeCellsTypeToolStripMenuItem2.Text = LangResource.Menu_Change_Cells_Type;
			formatCellToolStripMenuItem.Text = LangResource.Menu_Format_Cells;

			// Lead Header Context Menu
			resetAllPageBreaksToolStripMenuItem1.Text = LangResource.Menu_Reset_All_Page_Breaks;

			#endregion // Menu

			header1.SetupUILanguage();
			header2.SetupUILanguage();
		}

		void ChangeLanguage(string culture)
		{
			Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(culture);
			SetupUILanguage();
		}

		void englishenUSToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ChangeLanguage("en-US");
		}

		void japanesejpJPToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ChangeLanguage("ja-JP");
		}

		void simplifiedChinesezhCNToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ChangeLanguage("zh-CN");
		}

		void germandeDEToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ChangeLanguage("de-DE");
		}
		#endregion // Langauge

		#region Update Menus & Toolbars
		private bool isUIUpdating = false;
		private void UpdateMenuAndToolStrips()
		{
			if (isUIUpdating)
				return;

			isUIUpdating = true;

			var worksheet = CurrentWorksheet;

			WorksheetRangeStyle style = worksheet.GetCellStyles(worksheet.SelectionRange.StartPos);
			if (style != null)
			{
				// cross-thread exception
				Action set = () =>
				{
					fontToolStripComboBox.Text = style.FontName;
					fontSizeToolStripComboBox.Text = style.FontSize.ToString();
					boldToolStripButton.Checked = style.Bold;
					italicToolStripButton.Checked = style.Italic;
					strikethroughToolStripButton.Checked = style.Strikethrough;
					underlineToolStripButton.Checked = style.Underline;
					textColorPickToolStripItem.SolidColor = style.TextColor;
					backColorPickerToolStripButton.SolidColor = style.BackColor;
					textAlignLeftToolStripButton.Checked = style.HAlign == ReoGridHorAlign.Left;
					textAlignCenterToolStripButton.Checked = style.HAlign == ReoGridHorAlign.Center;
					textAlignRightToolStripButton.Checked = style.HAlign == ReoGridHorAlign.Right;
					distributedIndentToolStripButton.Checked = style.HAlign == ReoGridHorAlign.DistributedIndent;
					textAlignTopToolStripButton.Checked = style.VAlign == ReoGridVerAlign.Top;
					textAlignMiddleToolStripButton.Checked = style.VAlign == ReoGridVerAlign.Middle;
					textAlignBottomToolStripButton.Checked = style.VAlign == ReoGridVerAlign.Bottom;
					textWrapToolStripButton.Checked = style.TextWrapMode != TextWrapMode.NoWrap;

					RangeBorderInfoSet borderInfo = worksheet.GetRangeBorders(worksheet.SelectionRange);
					if (borderInfo.Left != null)
					{
						borderColorPickToolStripItem.SolidColor = borderInfo.Left.Color;
					}
					else if (borderInfo.Right != null)
					{
						borderColorPickToolStripItem.SolidColor = borderInfo.Right.Color;
					}
					else if (borderInfo.Top != null)
					{
						borderColorPickToolStripItem.SolidColor = borderInfo.Top.Color;
					}
					else if (borderInfo.Bottom != null)
					{
						borderColorPickToolStripItem.SolidColor = borderInfo.Bottom.Color;
					}
					else if (borderInfo.InsideHorizontal != null)
					{
						borderColorPickToolStripItem.SolidColor = borderInfo.InsideHorizontal.Color;
					}
					else if (borderInfo.InsideVertical != null)
					{
						borderColorPickToolStripItem.SolidColor = borderInfo.InsideVertical.Color;
					}
					else
					{
						borderColorPickToolStripItem.SolidColor = Color.Black;
					}

					var canUndo = undoStack.Count() != 0;
					var canRedo = redoStack.Count() != 0;

					undoToolStripButton.Enabled = canUndo;
					undoToolStripMenuItem.Enabled = canUndo;

					redoToolStripButton.Enabled = canRedo;
					redoToolStripMenuItem.Enabled = canRedo;

					repeatLastActionToolStripMenuItem.Enabled = canUndo || canRedo;

					cutToolStripButton.Enabled =
						cutToolStripMenuItem.Enabled =
						rowCutToolStripMenuItem.Enabled =
						colCutToolStripMenuItem.Enabled =
						worksheet.CanCut();

					copyToolStripButton.Enabled =
						copyToolStripMenuItem.Enabled =
						rowCopyToolStripMenuItem.Enabled =
						colCopyToolStripMenuItem.Enabled =
						worksheet.CanCopy();

					pasteToolStripButton.Enabled =
						pasteToolStripMenuItem.Enabled =
						rowPasteToolStripMenuItem.Enabled =
						colPasteToolStripMenuItem.Enabled =
						worksheet.CanPaste();

					unfreezeToolStripMenuItem.Enabled = worksheet.IsFrozen;

					isUIUpdating = false;
					toolStripButton_EnabledChanged(this, EventArgs.Empty);
				};

				if (InvokeRequired)
					Invoke(set);
				else
					set();
			}

	#if !DEBUG
			debugToolStripMenuItem.Enabled = false;
	#endif // DEBUG

		}

		private bool settingSelectionMode = false;

		private void UpdateSelectionModeAndStyle()
		{
			if (settingSelectionMode) return;

			settingSelectionMode = true;

			selModeNoneToolStripMenuItem.Checked = false;
			selModeCellToolStripMenuItem.Checked = false;
			selModeRangeToolStripMenuItem.Checked = false;
			selModeRowToolStripMenuItem.Checked = false;
			selModeColumnToolStripMenuItem.Checked = false;

			switch (CurrentWorksheet.SelectionMode)
			{
				case WorksheetSelectionMode.None:
					selModeNoneToolStripMenuItem.Checked = true;
					break;
				case WorksheetSelectionMode.Cell:
					selModeCellToolStripMenuItem.Checked = true;
					break;
				default:
				case WorksheetSelectionMode.Range:
					selModeRangeToolStripMenuItem.Checked = true;
					break;
				case WorksheetSelectionMode.Row:
					selModeRowToolStripMenuItem.Checked = true;
					break;
				case WorksheetSelectionMode.Column:
					selModeColumnToolStripMenuItem.Checked = true;
					break;
			}

			selStyleNoneToolStripMenuItem.Checked = false;
			selStyleDefaultToolStripMenuItem.Checked = false;
			selStyleFocusRectToolStripMenuItem.Checked = false;

			switch (CurrentWorksheet.SelectionStyle)
			{
				case WorksheetSelectionStyle.None:
					selStyleNoneToolStripMenuItem.Checked = true;
					break;
				default:
				case WorksheetSelectionStyle.Default:
					selStyleDefaultToolStripMenuItem.Checked = true;
					break;
				case WorksheetSelectionStyle.FocusRect:
					selStyleFocusRectToolStripMenuItem.Checked = true;
					break;
			}

			focusStyleDefaultToolStripMenuItem.Checked = false;
			focusStyleNoneToolStripMenuItem.Checked = false;

			switch (CurrentWorksheet.FocusPosStyle)
			{
				default:
				case FocusPosStyle.Default:
					focusStyleDefaultToolStripMenuItem.Checked = true;
					break;
				case FocusPosStyle.None:
					focusStyleNoneToolStripMenuItem.Checked = true;
					break;
			}

			settingSelectionMode = false;
		}

		private void UpdateSelectionForwardDirection()
		{
			switch (CurrentWorksheet.SelectionForwardDirection)
			{
				default:
				case SelectionForwardDirection.Right:
					selDirRightToolStripMenuItem.Checked = true;
					selDirDownToolStripMenuItem.Checked = false;
					break;
				case SelectionForwardDirection.Down:
					selDirRightToolStripMenuItem.Checked = false;
					selDirDownToolStripMenuItem.Checked = true;
					break;
			}
		}
		#endregion

		#region Document
		public FilePathBar CurrentHeader { get => GridControl == grid1 ? header1 : header2; }
		public string CurrentFilePath { get => CurrentHeader.Text; }

		private bool SaveOneFile(ReoGridControl grid, string path, object arg)
		{
			var dirty = true;
			FileFormat fm = FileFormat._Auto;
			if (path.EndsWith(".xlsx", StringComparison.CurrentCultureIgnoreCase))
			{
				fm = FileFormat.Excel2007;
			}
			else if (path.EndsWith(".rgf", StringComparison.CurrentCultureIgnoreCase))
			{
				fm = FileFormat.ReoGridFormat;
			}
			else if (arg as CSVFormatArgument != null)
			{
				fm = FileFormat.CSV;
			}
			try
			{
				grid.Save(path, fm, arg);
				dirty = false;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Save error: " + ex.Message, "Save Workbook", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			return dirty;
		}

		private void SaveOneFile(ReoGridControl grid, FilePathBar header, object arg)
		{
			if (header.Dirty)
				header.Dirty = SaveOneFile(grid, header.Text, arg);
		}

		private void RemoveUndoRedoActions(ReoGridControl grid)
		{
			var node = undoStack.First;
			while (node != null)
			{
				var next = node.Next;
				if (grid.CanUndo(node.Value))
					undoStack.Remove(node);
				node = next;
			}
			node = redoStack.First;
			while (node != null)
			{
				var next = node.Next;
				if (grid.CanRedo(node.Value))
					redoStack.Remove(node);
				node = next;
			}
		}

		private object LoadOneFile(ReoGridControl grid, FilePathBar header)
		{
			object arg = null;
			Cursor = Cursors.WaitCursor;
			try
			{
				RemoveUndoRedoActions(grid);
				grid.Visible = false;
				grid.Reset();
				switch (header.Selection)
				{
					case FilePathBar.SeletionType.TabDelimited:
						arg = grid.Load(header.Text, IO.FileFormat.CSV, Encoding.Default, csvTabDelimited);
						break;
					case FilePathBar.SeletionType.CommaDelimited:
						arg = grid.Load(header.Text, IO.FileFormat.CSV, Encoding.Default, csvCommaDelimited);
						break;
					case FilePathBar.SeletionType.SemicolonDelimited:
						arg = grid.Load(header.Text, IO.FileFormat.CSV, Encoding.Default, csvSemicolonDelimited);
						break;
					case FilePathBar.SeletionType.PipeDelimited:
						arg = grid.Load(header.Text, IO.FileFormat.CSV, Encoding.Default, csvPipeDelimited);
						break;
					case FilePathBar.SeletionType.AutoDetect:
						arg = grid.Load(header.Text, IO.FileFormat._Auto, Encoding.Default);
						break;
				}
			}
			catch (FileNotFoundException ex)
			{
				MessageBox.Show(LangResource.Msg_File_Not_Found + ex.FileName, "ReoGrid Editor", MessageBoxButtons.OK, MessageBoxIcon.Stop);
			}
			catch (Exception ex)
			{
				MessageBox.Show(LangResource.Msg_Load_File_Failed + ex.Message, "ReoGrid Editor", MessageBoxButtons.OK, MessageBoxIcon.Stop);
			}
			finally
			{
				Cursor = Cursors.Default;
			}
			if (header.Selection < FilePathBar.SeletionType.Clear)
			{
				grid.Visible = true;
				header.Modified = false;
				Rescan(RangePosition.EntireRange);
			}
			return arg;
		}

		private bool RescanCells(Cell cell1, Cell cell2)
		{
			var finding = false;
			if (cell1 == null)
			{
				if (cell2 != null)
				{
					cell2.SetDiffFlag(PlainStyleFlag.DiffInsert);
					finding = true;
				}
			}
			else if (cell2 == null)
			{
				if (cell1 != null)
				{
					cell1.SetDiffFlag(PlainStyleFlag.DiffInsert);
					finding = true;
				}
			}
			else if (!Equals(cell1.Data, cell2.Data))
			{
				cell1.SetDiffFlag(PlainStyleFlag.DiffChange);
				cell2.SetDiffFlag(PlainStyleFlag.DiffChange);
				finding = true;
			}
			else
			{
				cell1.SetDiffFlag(0);
				cell2.SetDiffFlag(0);
			}
			return finding;
		}

		private bool RescanCurrentSheet(RangePosition roi)
		{
			var sheet1 = grid1.CurrentWorksheet;
			var sheet2 = grid2.CurrentWorksheet;
			int o, n, m;
			if (roi == RangePosition.EntireRange)
			{
				n = Math.Max(sheet1.RowCount, sheet2.RowCount);
				m = Math.Max(sheet1.ColumnCount, sheet2.ColumnCount);
				sheet1.RowCount = n;
				sheet2.RowCount = n;
				sheet1.ColumnCount = m;
				sheet2.ColumnCount = m;
				sheet1.ColumnsWidthChanged -= worksheet_ColumnsWidthChanged;
				sheet2.ColumnsWidthChanged -= worksheet_ColumnsWidthChanged;
				sheet1.RowsHeightChanged -= worksheet_RowsHeightChanged;
				sheet2.RowsHeightChanged -= worksheet_RowsHeightChanged;
				// Align column widths on both sides
				for (var i = 0; i < m; ++i)
				{
					var colhdr1 = sheet1.GetColumnHeader(i);
					var colhdr2 = sheet2.GetColumnHeader(i);
					if (colhdr1.Width < colhdr2.Width)
						colhdr1.Width = colhdr2.Width;
					else if (colhdr2.Width < colhdr1.Width)
						colhdr2.Width = colhdr1.Width;
				}
				// Align row heights on both sides and clear row differences
				rowDiffCount = 0;
				for (var i = 0; i < n; ++i)
				{
					var rowhdr1 = sheet1.GetRowHeader(i);
					var rowhdr2 = sheet2.GetRowHeader(i);
					if (rowhdr1.Height < rowhdr2.Height)
						rowhdr1.Height = rowhdr2.Height;
					else if (rowhdr2.Height < rowhdr1.Height)
						rowhdr2.Height = rowhdr1.Height;
					rowhdr1.TextColor = Graphics.SolidColor.Transparent;
					rowhdr2.TextColor = Graphics.SolidColor.Transparent;
				}
				sheet1.ColumnsWidthChanged += worksheet_ColumnsWidthChanged;
				sheet2.ColumnsWidthChanged += worksheet_ColumnsWidthChanged;
				sheet1.RowsHeightChanged += worksheet_RowsHeightChanged;
				sheet2.RowsHeightChanged += worksheet_RowsHeightChanged;
				// Scan all rows which have some content
				o = 0;
				n = Math.Max(sheet1.MaxContentRow, sheet2.MaxContentRow) + 1;
			}
			else
			{
				// Scan only the rows which intersect the range of interest
				o = roi.Row;
				n = o + roi.Rows;
			}
			m = Math.Max(sheet1.MaxContentCol, sheet2.MaxContentCol) + 1;
			// Colorize the differences
			for (var i = o; i < n; ++i)
			{
				var finding = false;
				for (var j = 0; j < m; ++j)
				{
					var cell1 = sheet1.GetCell(i, j);
					var cell2 = sheet2.GetCell(i, j);
					if (RescanCells(cell1, cell2))
					{
						finding = true;
					}
				}
				var rowhdr1 = sheet1.GetRowHeader(i);
				var rowhdr2 = sheet2.GetRowHeader(i);
				if (finding)
				{
					if (rowhdr1.TextColor == Graphics.SolidColor.Transparent)
					{
						++rowDiffCount;
						rowhdr1.TextColor = Graphics.SolidColor.Red;
						rowhdr2.TextColor = Graphics.SolidColor.Red;
					}
				}
				else
				{
					if (rowhdr1.TextColor != Graphics.SolidColor.Transparent)
					{
						--rowDiffCount;
						rowhdr1.TextColor = Graphics.SolidColor.Transparent;
						rowhdr2.TextColor = Graphics.SolidColor.Transparent;
					}
				}
			}
			inScrolling = true;
			grid1.ScrollCurrentWorksheet(ViewLeft - sheet1.ViewLeft, ViewTop - sheet1.ViewTop);
			grid2.ScrollCurrentWorksheet(ViewLeft - sheet2.ViewLeft, ViewTop - sheet2.ViewTop);
			inScrolling = false;
			sheet1.RequestInvalidate();
			sheet2.RequestInvalidate();
			return rowDiffCount != 0;
		}

		private void Rescan(RangePosition roi)
		{
			var findings = false;
			if (KeepSheetsInSync)
			{
				Cursor = Cursors.WaitCursor;
				try
				{
					findings = RescanCurrentSheet(roi);
				}
				finally
				{
					Cursor = Cursors.Default;
				}
				compareInfoToolStripStatusLabel.Text = rowDiffCount != 0 ?
					string.Format(LangRes.LangResource.DifferencesInHowManyRows, rowDiffCount) :
					LangRes.LangResource.SheetsAreIdentical;
			}
			else
			{
				compareInfoToolStripStatusLabel.Text = string.Empty;
			}
			// Diff navigation commands
			nextDiffToolStripButton.Enabled = findings;
			prevDiffToolStripButton.Enabled = findings;
			firstDiffToolStripButton.Enabled = findings;
			lastDiffToolStripButton.Enabled = findings;
			left2rightToolStripButton.Enabled = findings;
			right2leftToolStripButton.Enabled = findings;
			// Save commands
			UpdateSaveCmdUI();
			saveAsLeftToolStripMenuItem.Enabled = grid1.Visible;
			saveAsRightToolStripMenuItem.Enabled = grid2.Visible;
			toolStripButton_EnabledChanged(this, EventArgs.Empty);
		}

		private void UpdateSaveCmdUI()
		{
			saveLeftToolStripMenuItem.Enabled = header1.Dirty;
			saveRightToolStripMenuItem.Enabled = header2.Dirty;
			bool dirty = header1.Dirty || header2.Dirty;
			saveToolStripMenuItem.Enabled = dirty;
			saveToolStripButton.Enabled = dirty;
		}

		/// <summary>
		/// Save current document
		/// </summary>
		private void Save(Side which)
		{
			if ((which & Side.Left) != 0)
			{
				SaveOneFile(grid1, header1, arg1);
			}
			if ((which & Side.Right) != 0)
			{
				SaveOneFile(grid2, header2, arg2);
			}
			UpdateSaveCmdUI();
			toolStripButton_EnabledChanged(this, EventArgs.Empty);
		}

		/// <summary>
		/// Save current document by specifying new file path
		/// </summary>
		/// <returns>true if operation is successful, otherwise false</returns>
		private void SaveAs(Side which)
		{
			using (SaveFileDialog sfd = new SaveFileDialog())
			{
				var header = which == Side.Left ? header1 : header2;
				sfd.Filter = LangResource.Filter_Save_File;
				var path = header.Text;
				if (!string.IsNullOrEmpty(path))
				{
					sfd.FileName = Path.GetFileNameWithoutExtension(path);
					var ext = Path.GetExtension(path);
					var pos = sfd.Filter.LastIndexOf(ext);
					if (pos != -1)
					{
						pos -= sfd.Filter.Substring(0, pos).Replace("|", "").Length;
						sfd.FilterIndex = (pos + 1) / 2;
					}
				}
				if (sfd.ShowDialog(this) == DialogResult.OK)
				{
					SaveOneFile(which == Side.Left ? grid1 : grid2,
						sfd.FileName, which == Side.Left ? arg1 : arg2);
					header.Text = sfd.FileName;
					header.Dirty = false;
				}
			}
			UpdateSaveCmdUI();
			toolStripButton_EnabledChanged(this, EventArgs.Empty);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			GridControl.Focus();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

#if DEBUG
			// Uncomment out the code below to allow autosave.
			// Only RGF(ReoGrid Format, *.rgf) is supported.

			// CurrentWorksheet.Save("..\\..\\autosave.rgf");
#endif // DEBUG
		}
		#endregion

		#region Alignment
		private void textLeftToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.HorizontalAlign,
				HAlign = ReoGridHorAlign.Left,
			}));
		}
		private void textCenterToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.HorizontalAlign,
				HAlign = ReoGridHorAlign.Center,
			}));
		}
		private void textRightToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.HorizontalAlign,
				HAlign = ReoGridHorAlign.Right,
			}));
		}
		private void distributedIndentToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.HorizontalAlign,
				HAlign = ReoGridHorAlign.DistributedIndent,
			}));
		}

		private void textAlignTopToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.VerticalAlign,
				VAlign = ReoGridVerAlign.Top,
			}));
		}
		private void textAlignMiddleToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.VerticalAlign,
				VAlign = ReoGridVerAlign.Middle,
			}));
		}
		private void textAlignBottomToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.VerticalAlign,
				VAlign = ReoGridVerAlign.Bottom,
			}));
		}
		#endregion

		#region Border Settings

		#region Outside Borders
		private void SetSelectionBorder(BorderPositions borderPos, BorderLineStyle style)
		{
			GridControl.DoAction(new SetRangeBorderAction(CurrentWorksheet.SelectionRange, borderPos,
				new RangeBorderStyle { Color = borderColorPickToolStripItem.SolidColor, Style = style }));
		}

		private void boldOutlineToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Outside, BorderLineStyle.BoldSolid);
		}
		private void dottedOutlineToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Outside, BorderLineStyle.Dotted);
		}
		private void boundsSolidToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Outside, BorderLineStyle.Solid);
		}
		private void solidOutlineToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Outside, BorderLineStyle.Solid);
		}
		private void dashedOutlineToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Outside, BorderLineStyle.Dashed);
		}
		#endregion // Outside Borders

		#region Inside Borders
		private void insideSolidToolStripSplitButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.InsideAll, BorderLineStyle.Solid);
		}
		private void insideSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.InsideAll, BorderLineStyle.Solid);
		}
		private void insideBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.InsideAll, BorderLineStyle.BoldSolid);
		}
		private void insideDottedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.InsideAll, BorderLineStyle.Dotted);
		}
		private void insideDashedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.InsideAll, BorderLineStyle.Dashed);
		}
		#endregion // Inside Borders

		#region Left & Right Borders
		private void leftRightSolidToolStripSplitButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.LeftRight, BorderLineStyle.Solid);
		}
		private void leftRightSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.LeftRight, BorderLineStyle.Solid);
		}
		private void leftRightBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.LeftRight, BorderLineStyle.BoldSolid);
		}
		private void leftRightDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.LeftRight, BorderLineStyle.Dotted);
		}
		private void leftRightDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.LeftRight, BorderLineStyle.Dashed);
		}
		#endregion // Left & Right Borders

		#region Top & Bottom Borders
		private void topBottomSolidToolStripSplitButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.TopBottom, BorderLineStyle.Solid);
		}
		private void topBottomSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.TopBottom, BorderLineStyle.Solid);
		}
		private void topBottomBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.TopBottom, BorderLineStyle.BoldSolid);
		}
		private void topBottomDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.TopBottom, BorderLineStyle.Dotted);
		}
		private void topBottomDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.TopBottom, BorderLineStyle.Dashed);
		}
		#endregion // Top & Bottom Borders

		#region All Borders
		private void allSolidToolStripSplitButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.All, BorderLineStyle.Solid);
		}
		private void allSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.All, BorderLineStyle.Solid);
		}
		private void allBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.All, BorderLineStyle.BoldSolid);
		}
		private void allDottedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.All, BorderLineStyle.Dotted);
		}
		private void allDashedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.All, BorderLineStyle.Dashed);
		}
		#endregion // All Borders

		#region Left Border
		private void leftSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Left, BorderLineStyle.Solid);
		}

		private void leftSolidToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Left, BorderLineStyle.Solid);
		}

		private void leftBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Left, BorderLineStyle.BoldSolid);
		}

		private void leftDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Left, BorderLineStyle.Dotted);
		}

		private void leftDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Left, BorderLineStyle.Dashed);
		}
		#endregion // Left Border

		#region Top Border
		private void topSolidToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Top, BorderLineStyle.Solid);
		}

		private void topSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Top, BorderLineStyle.Solid);
		}

		private void topBlodToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Top, BorderLineStyle.BoldSolid);
		}

		private void topDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Top, BorderLineStyle.Dotted);
		}

		private void topDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Top, BorderLineStyle.Dashed);
		}
		#endregion // Top Border

		#region Bottom Border
		private void bottomToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Bottom, BorderLineStyle.Solid);
		}

		private void bottomSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Bottom, BorderLineStyle.Solid);
		}

		private void bottomBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Bottom, BorderLineStyle.BoldSolid);
		}

		private void bottomDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Bottom, BorderLineStyle.Dotted);
		}

		private void bottomDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Bottom, BorderLineStyle.Dashed);
		}
		#endregion // Bottom Border

		#region Right Border
		private void rightSolidToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Right, BorderLineStyle.Solid);
		}

		private void rightSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Right, BorderLineStyle.Solid);
		}

		private void rightBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Right, BorderLineStyle.BoldSolid);
		}

		private void rightDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Right, BorderLineStyle.Dotted);
		}

		private void rightDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Right, BorderLineStyle.Dashed);
		}
		#endregion // Right Border

		#region Slash
		private void slashRightSolidToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Slash, BorderLineStyle.Solid);
		}

		private void slashRightSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Slash, BorderLineStyle.Solid);
		}

		private void slashRightBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Slash, BorderLineStyle.BoldSolid);
		}

		private void slashRightDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Slash, BorderLineStyle.Dotted);
		}

		private void slashRightDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Slash, BorderLineStyle.Dashed);
		}
		#endregion // Slash

		#region Backslash
		private void slashLeftSolidToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Backslash, BorderLineStyle.Solid);
		}

		private void slashLeftSolidToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Backslash, BorderLineStyle.Solid);
		}

		private void slashLeftBoldToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Backslash, BorderLineStyle.BoldSolid);
		}

		private void slashLeftDotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Backslash, BorderLineStyle.Dotted);
		}

		private void slashLeftDashToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSelectionBorder(BorderPositions.Backslash, BorderLineStyle.Dashed);
		}
		#endregion // Backslash

		#region Clear Borders
		private void clearBordersToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeBorderAction(CurrentWorksheet.SelectionRange, BorderPositions.All,
				new RangeBorderStyle { Color = Color.Empty, Style = BorderLineStyle.None }));
		}
		#endregion // Clear Borders

		#endregion // Border Settings

		#region Style
		private void backColorPickerToolStripButton_ColorPicked(object sender, EventArgs e)
		{
			//Color c = backColorPickerToolStripButton.SolidColor;
			//if (c.IsEmpty)
			//{
			//  workbook.DoAction(new SGRemoveRangeStyleAction(workbook.SelectionRange, PlainStyleFlag.FillColor));
			//}
			//else
			//{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle()
			{
				Flag = PlainStyleFlag.BackColor,
				BackColor = backColorPickerToolStripButton.SolidColor,
			}));
			//}
		}

		private void textColorPickToolStripItem_ColorPicked(object sender, EventArgs e)
		{
			var color = textColorPickToolStripItem.SolidColor;

			if (color.IsEmpty)
			{
				GridControl.DoAction(new RemoveRangeStyleAction(CurrentWorksheet.SelectionRange, PlainStyleFlag.TextColor));
			}
			else
			{
				GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
				{
					Flag = PlainStyleFlag.TextColor,
					TextColor = color,
				}));
			}
		}

		private void boldToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.FontStyleBold,
				Bold = boldToolStripButton.Checked,
			}));
		}

		private void italicToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.FontStyleItalic,
				Italic = italicToolStripButton.Checked,
			}));
		}

		private void underlineToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.FontStyleUnderline,
				Underline = underlineToolStripButton.Checked,
			}));
		}

		private void strikethroughToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.FontStyleStrikethrough,
				Strikethrough = strikethroughToolStripButton.Checked,
			}));
		}

		private void styleBrushToolStripButton_Click(object sender, EventArgs e)
		{
			CurrentWorksheet.StartPickRangeAndCopyStyle();
		}

		private void enlargeFontToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new StepRangeFontSizeAction(CurrentWorksheet.SelectionRange, true));
			UpdateMenuAndToolStrips();
		}

		private void fontSmallerToolStripButton_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new StepRangeFontSizeAction(CurrentWorksheet.SelectionRange, false));
			UpdateMenuAndToolStrips();
		}

		private void fontToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (isUIUpdating) return;

			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.FontName,
				FontName = fontToolStripComboBox.Text,
			}));
		}

		private void fontSizeToolStripComboBox_TextChanged(object sender, EventArgs e)
		{
			SetGridFontSize();
		}

		private void SetGridFontSize()
		{
			if (isUIUpdating) return;

			float size = 9;
			float.TryParse(fontSizeToolStripComboBox.Text, out size);

			if (size <= 0) size = 1f;

			GridControl.DoAction(new SetRangeStyleAction(CurrentWorksheet.SelectionRange, new WorksheetRangeStyle
			{
				Flag = PlainStyleFlag.FontSize,
				FontSize = size,
			}));
		}

		#endregion

		#region Cell & Range
		private void MergeSelectionRange(object sender, EventArgs e)
		{
			try
			{
				GridControl.DoAction(new MergeRangeAction(CurrentWorksheet.SelectionRange));
			}
			catch (RangeTooSmallException) { }
			catch (RangeIntersectionException)
			{
				MessageBox.Show(LangResource.Msg_Range_Intersection_Exception,
					Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private void UnmergeSelectionRange(object sender, EventArgs e)
		{
			GridControl.DoAction(new UnmergeRangeAction(CurrentWorksheet.SelectionRange));
		}

		void resizeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var worksheet = CurrentWorksheet;

			using (var rgf = new ResizeGridDialog())
			{
				rgf.Rows = worksheet.RowCount;
				rgf.Cols = worksheet.ColumnCount;

				if (rgf.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					WorksheetActionGroup ag = new WorksheetActionGroup();

					if (rgf.Rows < worksheet.RowCount)
					{
						ag.Actions.Add(new RemoveRowsAction(rgf.Rows, worksheet.RowCount - rgf.Rows));
					}
					else if (rgf.Rows > worksheet.RowCount)
					{
						ag.Actions.Add(new InsertRowsAction(worksheet.RowCount, rgf.Rows - worksheet.RowCount));
					}

					if (rgf.Cols < worksheet.ColumnCount)
					{
						ag.Actions.Add(new RemoveColumnsAction(rgf.Cols, worksheet.ColumnCount - rgf.Cols));
					}
					else if (rgf.Cols > worksheet.ColumnCount)
					{
						ag.Actions.Add(new InsertColumnsAction(worksheet.ColumnCount, rgf.Cols - worksheet.ColumnCount));
					}

					if (ag.Actions.Count > 0)
					{
						Cursor = Cursors.WaitCursor;
						try
						{
							GridControl.DoAction(ag);
						}
						finally
						{
							Cursor = Cursors.Default;
						}
					}
				}
			}
		}

		void ApplyFunctionToSelectedRange(string funName)
		{
			var sheet = CurrentWorksheet;
			var range = CurrentSelectionRange;

			// fill bottom rows
			if (range.Rows > 1)
			{
				for (int c = range.Col; c <= range.EndCol; c++)
				{
					var cell = sheet.Cells[range.EndRow, c];

					if (string.IsNullOrEmpty(cell.DisplayText))
					{
						cell.Formula = string.Format("{0}({1})", funName,
							RangePosition.FromCellPosition(range.Row, range.Col, range.EndRow - 1, c).ToAddress());
						break;
					}
				}
			}

			// fill right columns
			if (range.Cols > 1)
			{
				for (int r = range.Row; r <= range.EndRow; r++)
				{
					var cell = sheet.Cells[r, range.EndCol];

					if (string.IsNullOrEmpty(cell.DisplayText))
					{
						cell.Formula = string.Format("{0}({1})", funName,
							RangePosition.FromCellPosition(range.Row, range.Col, r, range.EndCol - 1).ToAddress());
						break;
					}
				}
			}
		}
		#endregion

		#region Context Menu
		private void insertColToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentSelectionRange.Cols >= 1)
			{
				GridControl.DoAction(new InsertColumnsAction(CurrentSelectionRange.Col, CurrentSelectionRange.Cols));
			}
		}
		private void insertRowToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentSelectionRange.Rows >= 1)
			{
				GridControl.DoAction(new InsertRowsAction(CurrentSelectionRange.Row, CurrentSelectionRange.Rows));
			}
		}
		private void deleteColumnToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentSelectionRange.Cols >= 1)
			{
				GridControl.DoAction(new RemoveColumnsAction(CurrentSelectionRange.Col, CurrentSelectionRange.Cols));
			}
		}
		private void deleteRowsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentSelectionRange.Rows >= 1)
			{
				GridControl.DoAction(new RemoveRowsAction(CurrentSelectionRange.Row, CurrentSelectionRange.Rows));
			}
		}
		private void resetToDefaultWidthToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetColumnsWidthAction(CurrentSelectionRange.Col, CurrentSelectionRange.Cols, Worksheet.InitDefaultColumnWidth));
		}
		private void resetToDefaultHeightToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new SetRowsHeightAction(CurrentSelectionRange.Row, CurrentSelectionRange.Rows, Worksheet.InitDefaultRowHeight));
		}
		#endregion

		#region Debug Form
#if DEBUG
		private DebugForm cellDebugForm = null;
		private DebugForm borderDebugForm = null;

		private void showDebugFormToolStripButton_Click(object sender, EventArgs e)
		{
			if (cellDebugForm == null)
			{
				cellDebugForm = new DebugForm();
				cellDebugForm.VisibleChanged += (ss, se) => showDebugFormToolStripButton.Checked = cellDebugForm.Visible;
			}
			else if (cellDebugForm.Visible)
			{
				cellDebugForm.Visible = false;
				borderDebugForm.Visible = false;
				return;
			}

			cellDebugForm.Grid = CurrentWorksheet;

			if (!cellDebugForm.Visible)
			{
				cellDebugForm.InitTabType = DebugForm.InitTab.Grid;
				cellDebugForm.Show(this);
			}

			if (borderDebugForm == null)
			{
				borderDebugForm = new DebugForm();
				borderDebugForm.Grid = CurrentWorksheet;
			}

			if (!borderDebugForm.Visible)
			{
				borderDebugForm.InitTabType = DebugForm.InitTab.Border;
				borderDebugForm.Show(this);
			}

			if (cellDebugForm.Visible || borderDebugForm.Visible) ResetDebugFormLocation();
		}
#endif // DEBUG

		protected override void OnShown(EventArgs e)
		{
			if (FormBorderStyle == FormBorderStyle.None)
			{
				menuStrip1.Visible = false;
				toolStrip1.Visible = false;
				WindowState = FormWindowState.Maximized;
				header1.AllowDrop = false;
				header2.AllowDrop = false;
			}
			if (header2.Modified)
				header2.Selection = FilePathBar.SeletionType.AutoDetect;
			if (header1.Modified)
				header1.Selection = FilePathBar.SeletionType.AutoDetect;
			base.OnShown(e);
		}

		protected override void OnMove(EventArgs e)
		{
			base.OnMove(e);

#if DEBUG
			ResetDebugFormLocation();
#endif // DEBUG
		}

#if DEBUG
		private void ResetDebugFormLocation()
		{
			if (cellDebugForm != null && cellDebugForm.Visible)
			{
				cellDebugForm.Location = new Point(Right, Top);
			}
			if (borderDebugForm != null && borderDebugForm.Visible)
			{
				borderDebugForm.Location = new Point(cellDebugForm.Left, cellDebugForm.Bottom);
			}
		}
#endif // DEBUG
		#endregion // Debug Form

		#region Editing
		private void cutRangeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Cut();
		}
		private void copyRangeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Copy();
		}
		private void pasteRangeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Paste();
		}

		private void Cut()
		{
			// Cut method will always perform action to do cut
			try
			{
				CurrentWorksheet.Cut();
			}
			catch (RangeIntersectionException)
			{
				MessageBox.Show(LangResource.Msg_Range_Intersection_Exception);
			}
			catch
			{
				MessageBox.Show(LangResource.Msg_Operation_Aborted);
			}
		}

		private void Copy()
		{
			try
			{
				CurrentWorksheet.Copy();
			}
			catch (RangeIntersectionException)
			{
				MessageBox.Show(LangResource.Msg_Range_Intersection_Exception);
			}
			catch
			{
				MessageBox.Show(LangResource.Msg_Operation_Aborted);
			}
		}

		private void Paste()
		{
			try
			{
				CurrentWorksheet.Paste();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void cutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			cutToolStripButton.PerformClick();
		}

		private void copyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			copyToolStripButton.PerformClick();
		}

		private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
		{
			pasteToolStripButton.PerformClick();
		}

		private void repeatLastActionToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GridControl.RepeatLastAction(CurrentWorksheet.SelectionRange);
		}

		private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CurrentWorksheet.SelectAll();
		}

		private void FindNextDiff(int i, int j)
		{
			var sheet1 = grid1.CurrentWorksheet;
			var sheet2 = grid2.CurrentWorksheet;
			var n = Math.Max(sheet1.MaxContentRow, sheet2.MaxContentRow) + 1;
			var m = Math.Max(sheet1.MaxContentCol, sheet2.MaxContentCol) + 1;
			do
			{
				if (++i >= m)
				{
					i = 0;
					do
					{
						if (++j >= n)
							return;
					} while (sheet1.GetRowHeight(j) == 0 && sheet2.GetRowHeight(j) == 0);
				}
			} while (Equals(sheet1[j, i], sheet2[j, i]));
			var pos = new CellPosition(j, i);
			grid1.CurrentWorksheet.FocusPos = pos;
			grid2.CurrentWorksheet.FocusPos = pos;
		}

		private void FindPrevDiff(int i, int j)
		{
			var sheet1 = grid1.CurrentWorksheet;
			var sheet2 = grid2.CurrentWorksheet;
			var n = Math.Max(sheet1.MaxContentRow, sheet2.MaxContentRow) + 1;
			var m = Math.Max(sheet1.MaxContentCol, sheet2.MaxContentCol) + 1;
			if (j == -1)
				j = n;
			do
			{
				if (i == 0)
				{
					do
					{
						if (j == 0)
							return;
						--j;
					} while (sheet1.GetRowHeight(j) == 0 && sheet2.GetRowHeight(j) == 0);
					i = m;
				}
				--i;
			} while (Equals(sheet1[j, i], sheet2[j, i]));
			var pos = new CellPosition(j, i);
			grid1.CurrentWorksheet.FocusPos = pos;
			grid2.CurrentWorksheet.FocusPos = pos;
		}

		private void nextDiffToolStripButton_Click(object sender, EventArgs e)
		{
			FindNextDiff(GridControl.CurrentWorksheet.FocusPos.Col, GridControl.CurrentWorksheet.FocusPos.Row);
		}

		private void prevDiffToolStripButton_Click(object sender, EventArgs e)
		{
			FindPrevDiff(GridControl.CurrentWorksheet.FocusPos.Col, GridControl.CurrentWorksheet.FocusPos.Row);
		}

		private void firstDiffToolStripButton_Click(object sender, EventArgs e)
		{
			FindNextDiff(-1, 0);
		}

		private void lastDiffToolStripButton_Click(object sender, EventArgs e)
		{
			FindPrevDiff(0, -1);
		}

		private void left2rightToolStripButton_Click(object sender, EventArgs e)
		{
			var sheet = grid1.CurrentWorksheet;
			var currentCopingRange = sheet.SelectionRange;
			var partialGrid = sheet.GetPartialGrid(currentCopingRange);
			grid2.DoAction(new SetPartialGridAction(currentCopingRange, partialGrid));
		}

		private void right2leftToolStripButton_Click(object sender, EventArgs e)
		{
			var sheet = grid2.CurrentWorksheet;
			var currentCopingRange = sheet.SelectionRange;
			var partialGrid = sheet.GetPartialGrid(currentCopingRange);
			grid1.DoAction(new SetPartialGridAction(currentCopingRange, partialGrid));
		}
		#endregion // Editing

		#region Window
		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void styleEditorToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ControlAppearanceEditorForm styleEditor = new ControlAppearanceEditorForm();
			styleEditor.Grid = GridControl;
			styleEditor.Show(this);
		}
		#endregion // Window

		#region View & Print

		private void formatCellToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (PropertyForm form = new PropertyForm(GridControl))
			{
				form.ShowDialog(this);
			}
		}

		private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
		{
			printPreviewToolStripButton.PerformClick();
		}

		private void printPreviewToolStripButton_Click(object sender, EventArgs e)
		{
			try
			{
				GridControl.CurrentWorksheet.AutoSplitPage();

				GridControl.CurrentWorksheet.EnableSettings(WorksheetSettings.View_ShowPageBreaks);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, Application.ProductName + " " + Application.ProductVersion,
					MessageBoxButtons.OK, MessageBoxIcon.Information);

				return;
			}

			using (var session = GridControl.CurrentWorksheet.CreatePrintSession())
			{
				using (PrintPreviewDialog ppd = new PrintPreviewDialog())
				{
					ppd.Document = session.PrintDocument;
					ppd.SetBounds(200, 200, 1024, 768);
					ppd.PrintPreviewControl.Zoom = 1d;
					ppd.ShowDialog(this);
				}
			}
		}

		private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PrintSession session = null;

			try
			{
				session = CurrentWorksheet.CreatePrintSession();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			using (var pd = new System.Windows.Forms.PrintDialog())
			{
				pd.Document = session.PrintDocument;
				pd.UseEXDialog = true;

				if (pd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					session.Print();
				}
			}

			if (session != null) session.Dispose();
		}

		void removeRowPageBreakToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GridControl.CurrentWorksheet.RemoveRowPageBreak(GridControl.CurrentWorksheet.FocusPos.Row);
		}

		void printSettingsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (PrintSettingsDialog psf = new PrintSettingsDialog())
			{
				var sheet = GridControl.CurrentWorksheet;

				if (sheet.PrintSettings == null)
				{
					sheet.PrintSettings = new PrintSettings();
				}

				psf.PrintSettings = (PrintSettings)sheet.PrintSettings.Clone();

				if (psf.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					sheet.PrintSettings = psf.PrintSettings;
					sheet.AutoSplitPage();
					sheet.EnableSettings(WorksheetSettings.View_ShowPageBreaks);
				}
			}
		}

		void removeColPageBreakToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				GridControl.CurrentWorksheet.RemoveColumnPageBreak(GridControl.CurrentWorksheet.FocusPos.Col);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		void insertRowPageBreakToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				CurrentWorksheet.RowPageBreaks.Add(CurrentWorksheet.FocusPos.Row);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		void insertColPageBreakToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CurrentWorksheet.ColumnPageBreaks.Add(CurrentWorksheet.FocusPos.Col);
		}

		#endregion // View & Print

		#region Freeze

		private void FreezeToEdge(FreezeArea freezePos)
		{
			var worksheet = CurrentWorksheet;

			if (!worksheet.SelectionRange.IsEmpty)
			{
				worksheet.FreezeToCell(worksheet.FocusPos, freezePos);
				UpdateMenuAndToolStrips();
			}
		}

		private void unfreezeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CurrentWorksheet.Unfreeze();
			UpdateMenuAndToolStrips();
		}

		#endregion // Freeze

		#region Help
		private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			new AboutForm().ShowDialog(this);
		}
		#endregion

		#region Outline

		void groupRowsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				GridControl.DoAction(new AddOutlineAction(RowOrColumn.Row, CurrentSelectionRange.Row, CurrentSelectionRange.Rows));
			}
			catch (OutlineOutOfRangeException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Out_Of_Range);
			}
			catch (OutlineAlreadyDefinedException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Already_Exist);
			}
			catch (OutlineIntersectedException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Intersected);
			}
			catch (OutlineTooMuchException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Too_Much);
			}
		}

		void ungroupRowsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var removeOutlineAction = new RemoveOutlineAction(RowOrColumn.Row, CurrentSelectionRange.Row, CurrentSelectionRange.Rows);

			try
			{
				GridControl.DoAction(removeOutlineAction);
			}
			catch { }

			if (removeOutlineAction.RemovedOutline == null)
			{
				MessageBox.Show(LangResource.Msg_Outline_Not_Found);
			}
		}

		void groupColumnsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var worksheet = CurrentWorksheet;

			try
			{
				GridControl.DoAction(new AddOutlineAction(RowOrColumn.Column, CurrentSelectionRange.Col, CurrentSelectionRange.Cols));
			}
			catch (OutlineOutOfRangeException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Out_Of_Range);
			}
			catch (OutlineAlreadyDefinedException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Already_Exist);
			}
			catch (OutlineIntersectedException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Intersected);
			}
			catch (OutlineTooMuchException)
			{
				MessageBox.Show(LangResource.Msg_Outline_Too_Much);
			}
		}

		void ungroupColumnsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var removeOutlineAction = new RemoveOutlineAction(RowOrColumn.Column, CurrentSelectionRange.Col, CurrentSelectionRange.Cols);

			try
			{
				GridControl.DoAction(removeOutlineAction);
			}
			catch { }

			if (removeOutlineAction.RemovedOutline == null)
			{
				MessageBox.Show(LangResource.Msg_Outline_Not_Found);
			}
		}

		void ungroupAllRowsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new ClearOutlineAction(RowOrColumn.Row));
		}

		void ungroupAllColumnsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GridControl.DoAction(new ClearOutlineAction(RowOrColumn.Column));
		}

		#endregion // Outline

		#region Filter
		private AutoColumnFilter columnFilter;

		void filterToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (columnFilter != null)
			{
				columnFilter.Detach();
			}

			CreateAutoFilterAction action = new CreateAutoFilterAction(CurrentWorksheet.SelectionRange);
			GridControl.DoAction(action);

			columnFilter = action.AutoColumnFilter;
		}

		void clearFilterToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (columnFilter != null)
			{
				columnFilter.Detach();
			}
		}
		#endregion // Filter
		private void Grid_ActionPerformed(object sender, ActionEventArgs e)
		{
			var grid = sender as ReoGridControl;
			if (e.Behavior == ActionBehavior.Do)
			{
				redoStack.Clear();
				undoStack.AddLast(e.Action);
				if (undoStack.Count() > 30)
					undoStack.RemoveFirst();
			}
			else
			{
				grid.Focus();
			}
			// TODO: Which actions are worth marking documents as dirty?
			if (e.Action as SetColumnsWidthAction == null &&
				e.Action as SetRowsHeightAction == null &&
				e.Action as CreateAutoFilterAction == null)
			{
				if (sender == grid1)
				{
					header1.Dirty = true;
					saveLeftToolStripMenuItem.Enabled = true;
					saveToolStripMenuItem.Enabled = true;
					saveToolStripButton.Enabled = true;
				}
				else if (sender == grid2)
				{
					header2.Dirty = true;
					saveRightToolStripMenuItem.Enabled = true;
					saveToolStripMenuItem.Enabled = true;
					saveToolStripButton.Enabled = true;
				}
				var roi = RangePosition.EntireRange;
				var reusableAction = e.Action as WorksheetReusableAction;
				if (reusableAction != null &&
					reusableAction as InsertColumnsAction == null &&
					reusableAction as RemoveColumnsAction == null &&
					reusableAction as InsertRowsAction == null &&
					reusableAction as RemoveRowsAction == null)
				{
					roi = reusableAction.Range;
				}
				else
				{
					var setCellDataAction = e.Action as SetCellDataAction;
					if (setCellDataAction != null)
					{
						roi = new RangePosition(setCellDataAction.Row, setCellDataAction.Col, 1, 1);
					}
				}
				Rescan(roi);
			}
			UpdateMenuAndToolStrips();
		}
		private void Grid_WorksheetInserted(object sender, WorksheetInsertedEventArgs e)
		{
			var worksheet = e.Worksheet;
			worksheet.SelectionRangeChanged += grid_SelectionRangeChanged;
			worksheet.SelectionModeChanged += worksheet_SelectionModeChanged;
			worksheet.SelectionStyleChanged += worksheet_SelectionModeChanged;
			worksheet.SelectionForwardDirectionChanged += worksheet_SelectionForwardDirectionChanged;
			worksheet.FocusPosStyleChanged += worksheet_SelectionModeChanged;
			worksheet.CellsFrozen += UpdateMenuAndToolStripsWhenAction;
			worksheet.Resetted += worksheet_Resetted;
			worksheet.SettingsChanged += worksheet_SettingsChanged;
			worksheet.Scaled += worksheet_GridScaled;
			worksheet.RowsHeightChanged += worksheet_RowsHeightChanged;
			worksheet.ColumnsWidthChanged += worksheet_ColumnsWidthChanged;
			grid_SelectionRangeChanged(worksheet, new RangeEventArgs(worksheet.SelectionRange));
		}
		private void Grid_WorksheetRemoved(object sender, WorksheetRemovedEventArgs e)
		{
			var worksheet = e.Worksheet;
			worksheet.SelectionRangeChanged -= grid_SelectionRangeChanged;
			worksheet.SelectionModeChanged -= worksheet_SelectionModeChanged;
			worksheet.SelectionStyleChanged -= worksheet_SelectionModeChanged;
			worksheet.SelectionForwardDirectionChanged -= worksheet_SelectionForwardDirectionChanged;
			worksheet.FocusPosStyleChanged -= worksheet_SelectionModeChanged;
			worksheet.CellsFrozen -= UpdateMenuAndToolStripsWhenAction;
			worksheet.Resetted -= worksheet_Resetted;
			worksheet.SettingsChanged -= worksheet_SettingsChanged;
			worksheet.Scaled -= worksheet_GridScaled;
			worksheet.RowsHeightChanged -= worksheet_RowsHeightChanged;
			worksheet.ColumnsWidthChanged -= worksheet_ColumnsWidthChanged;
		}
		private void Grid_CurrentWorksheetChanged(object sender, System.EventArgs e)
		{
			var owner = sender as ReoGridControl;
			owner.CurrentWorksheet.SelectionStyle = owner.Focused ? WorksheetSelectionStyle.Hybrid : WorksheetSelectionStyle.Default;
			if (sender == GridControl)
			{
				grid_SelectionRangeChanged(CurrentWorksheet, new RangeEventArgs(CurrentWorksheet.SelectionRange));
				worksheet_GridScaled(CurrentWorksheet, e);
				UpdateWorksheetSettings(CurrentWorksheet);
				UpdateSelectionModeAndStyle();
				UpdateSelectionForwardDirection();
			}
			Rescan(RangePosition.EntireRange);
		}
		private void Header_GotFocus(object sender, System.EventArgs e)
		{
			Grid_GotFocus(sender == header1 ? grid1 : grid2, e);
		}
		private void Grid_GotFocus(object sender, System.EventArgs e)
		{
			var owner = sender as ReoGridControl;
			if (formulaBar.GridControl != owner)
			{
				if (nameManagerForm != null)
				{
					nameManagerForm.Dispose();
					nameManagerForm = null;
				}
			}
			// Reassign even if already assigned to keep the formulaBar updated
			formulaBar.GridControl = owner;

			header1.Active = GridControl == grid1;
			header2.Active = GridControl == grid2;

			UpdateGridFocus(sender);

			grid_SelectionRangeChanged(CurrentWorksheet, new RangeEventArgs(CurrentWorksheet.SelectionRange));
			worksheet_GridScaled(CurrentWorksheet, e);
			UpdateWorksheetSettings(CurrentWorksheet);
			UpdateSelectionModeAndStyle();
			UpdateSelectionForwardDirection();
		}
		private void Grid_LostFocus(object sender, System.EventArgs e)
		{
			UpdateGridFocus(sender);
			toolStripButton_EnabledChanged(this, EventArgs.Empty);
		}
		private void UpdateGridFocus(object sender)
		{
			var owner = sender as ReoGridControl;
			owner.CurrentWorksheet.SelectionStyle = owner.Focused ? WorksheetSelectionStyle.Hybrid : WorksheetSelectionStyle.Default;
			var enable = owner.Focused;
			cutToolStripButton.Enabled = enable;
			cutToolStripMenuItem.Enabled = enable;
			pasteToolStripButton.Enabled = enable;
			pasteToolStripMenuItem.Enabled = enable;
			copyToolStripButton.Enabled = enable;
			copyToolStripMenuItem.Enabled = enable;
			undoToolStripButton.Enabled = enable;
			undoToolStripMenuItem.Enabled = enable;
			redoToolStripButton.Enabled = enable;
			redoToolStripMenuItem.Enabled = enable;
			repeatLastActionToolStripMenuItem.Enabled = enable;
			rowCutToolStripMenuItem.Enabled = enable;
			rowCopyToolStripMenuItem.Enabled = enable;
			rowPasteToolStripMenuItem.Enabled = enable;
			colCutToolStripMenuItem.Enabled = enable;
			colCopyToolStripMenuItem.Enabled = enable;
			colPasteToolStripMenuItem.Enabled = enable;
		}
#if DEBUG
		private void ForTest()
		{
			var sheet = GridControl.CurrentWorksheet;

		}

#endif // DEBUG
	}
}
