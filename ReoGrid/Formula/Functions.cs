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

#if FORMULA

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using unvell.ReoGrid.Utility;

namespace unvell.ReoGrid.Formula
{
	internal static class ExcelFunctions
	{
		static ReferenceRange RangeFrom(Cell cell, FormulaValue arg)
		{
			var range = arg.value as ReferenceRange;
			if (range == null)
				throw new FormulaTypeMismatchException(cell);
			return range;
		}

		static Worksheet WorksheetFrom(Cell cell, ReferenceRange range)
		{
			var worksheet = range.Worksheet != null ? range.Worksheet : cell.Worksheet;
			if (worksheet == null)
				throw new FormulaTypeMismatchException(cell);
			return worksheet;
		}

		#region Statistics

		#region Sum

		#region Sum
		public static FormulaValue Sum(Cell cell, FormulaValue[] args)
		{
			double val = 0;

			foreach (var arg in args)
			{
				switch (arg.type)
				{
					case FormulaValueType.Range:
						ReferenceRange range = RangeFrom(cell, arg);
						Worksheet worksheet = WorksheetFrom(cell, range);
						worksheet.IterateCells(range.Position, (r, c, inCell) =>
						{
							double data;
							if (CellUtility.TryGetNumberData(worksheet, inCell, out data))
							{
								val += data;
							}
							return true;
						});
						break;

					case FormulaValueType.Number:
						val += (double)arg.value;
						break;
				}
			}

			return val;
		}
		#endregion // Sum

		#region SumIf
		public static FormulaValue SumIf(Cell cell, FormulaValue[] args)
		{
			if (cell == null || cell.Worksheet == null) return null;

			if (args[1].type != FormulaValueType.String)
			{
				// todo: support not only string
				return null;
			}

			double val = 0;
			double data;

			ReferenceRange evalRange = RangeFrom(cell, args[0]);
			Worksheet evalWorksheet = WorksheetFrom(cell, evalRange);

			ReferenceRange sumRange = null;
			Worksheet sumWorksheet = null;
			if (args.Length > 2)
			{
				sumRange = RangeFrom(cell, args[2]);
				sumWorksheet = WorksheetFrom(cell, sumRange);
			}

			string expStr = (string)args[1].value;

			STValueNode leftExp = new STValueNode(null);
			STNode compExp = Parser.ParseInterCompareExp(cell, expStr);

			int rows = cell.Worksheet.Rows;
			int cols = cell.Worksheet.Columns;

			evalWorksheet.IterateCells(evalRange.Position, (r, c, inCell) =>
			{
				leftExp.Value = Evaluator.CreateFormulaValue(inCell);
				compExp[0] = leftExp;

				var result = Evaluator.Evaluate(cell, compExp);
				if (result.type == FormulaValueType.Boolean && ((bool)result.value))
				{
					if (sumRange == null)
					{
						if (CellUtility.TryGetNumberData(evalWorksheet, inCell, out data))
						{
							val += data;
						}
					}
					else
					{
						int tr = sumRange.Position.Row + r - evalRange.Position.Row;
						int tc = sumRange.Position.Col + c - evalRange.Position.Col;

						if (tr < rows && tc < cols)
						{
							var sumCell = sumWorksheet.GetCell(tr, tc);

							if (sumCell != null && sumCell.InnerData != null
								&& CellUtility.TryGetNumberData(sumWorksheet, sumCell, out data))
							{
								val += data;
							}
						}
					}
				}

				return true;
			});

			return val;
		}
		#endregion // SumIf

		#endregion // Sum

		#region Count

		#region Count
		public static FormulaValue Count(Cell cell, FormulaValue[] args, bool includeEmpty = false)
		{
			double count = 0;
			double data;

			foreach (var arg in args)
			{
				switch (arg.type)
				{
					case FormulaValueType.Range:
						ReferenceRange range = RangeFrom(cell, arg);
						Worksheet worksheet = WorksheetFrom(cell, range);
						worksheet.IterateCells(range.Position, (r, c, inCell) =>
						{
							if (includeEmpty)
							{
								if (inCell.InnerData != null)
								{
									count++;
								}
							}
							else
							{
								if (CellUtility.TryGetNumberData(worksheet, inCell, out data))
								{
									count++;
								}
							}
							return true;
						});
						break;

					case FormulaValueType.Number:
						count++;
						break;

					case FormulaValueType.String:
						if (includeEmpty && !string.IsNullOrEmpty((string)arg.value))
						{
							count++;
						}
						break;
				}
			}

			return count;
		}
		#endregion // Count

		#region CountIf
		public static FormulaValue CountIf(Cell cell, FormulaValue[] args)
		{
			if (cell == null || cell.Worksheet == null) return null;

			double count = 0;

			ReferenceRange evalRange = RangeFrom(cell, args[0]);
			Worksheet evalWorksheet = WorksheetFrom(cell, evalRange);

			STValueNode leftExp = new STValueNode(null);
			STNode compExp;

			FormulaValue compVal = args[1];

			if (args[1].type == FormulaValueType.String)
			{
				compExp = Parser.ParseInterCompareExp(cell, (string)compVal.value);
			}
			else
			{
				compExp = new STNode(STNodeType.EQUALS, 0, 0, new List<STNode>(2) { leftExp, new STValueNode(args[1]) });
			}

			evalWorksheet.IterateCells(evalRange, (r, c, inCell) =>
			{
				leftExp.Value = Evaluator.CreateFormulaValue(inCell);
				compExp[0] = leftExp;

				var result = (Evaluator.Evaluate(cell, compExp));
				if (result.type == FormulaValueType.Boolean && ((bool)result.value))
				{
					count++;
				}

				return true;
			});

			return count;
		}
		#endregion // CountIf

		#endregion // Count

		#region Average

		#region Average
		public static FormulaValue Average(Cell cell, FormulaValue[] args)
		{
			double val = 0, count = 0;
			double data;

			foreach (var arg in args)
			{
				switch (arg.type)
				{
					case FormulaValueType.Range:
						ReferenceRange range = RangeFrom(cell, arg);
						Worksheet worksheet = WorksheetFrom(cell, range);
						worksheet.IterateCells(range.Position, (r, c, inCell) =>
						{
							if (CellUtility.TryGetNumberData(worksheet, inCell, out data))
							{
								val += data;
								count++;
							}
							return true;
						});
						break;

					case FormulaValueType.Number:
						val += (double)arg.value;
						count++;
						break;

					case FormulaValueType.Nil:
						continue;
				}
			}

			return count > 0 ? (val / count) : 0;
		}
		#endregion // Average

		#region AverageIf
		public static FormulaValue AverageIf(Cell cell, FormulaValue[] args)
		{
			if (cell == null || cell.Worksheet == null) return null;

			if (args[1].type != FormulaValueType.String)
			{
				// todo: support not only string
				return null;
			}

			double val = 0, count = 0;
			double data;

			ReferenceRange evalRange = RangeFrom(cell, args[0]);
			Worksheet evalWorksheet = WorksheetFrom(cell, evalRange);

			ReferenceRange sumRange = null;
			Worksheet sumWorksheet = null;
			if (args.Length > 2)
			{
				sumRange = RangeFrom(cell, args[2]);
				sumWorksheet = WorksheetFrom(cell, sumRange);
			}

			string expStr = (string)args[1].value;

			STValueNode leftExp = new STValueNode(null);
			STNode compExp = Parser.ParseInterCompareExp(cell, expStr);

			int rows = cell.Worksheet.Rows;
			int cols = cell.Worksheet.Columns;

			evalWorksheet.IterateCells(evalRange.Position, (r, c, inCell) =>
			{
				leftExp.Value = Evaluator.CreateFormulaValue(inCell);
				compExp[0] = leftExp;

				var result = (Evaluator.Evaluate(cell, compExp));
				if (result.type == FormulaValueType.Boolean && ((bool)result.value))
				{
					if (sumRange == null)
					{
						if (CellUtility.TryGetNumberData(evalWorksheet, inCell, out data))
						{
							val += data;
							count++;
						}
					}
					else
					{
						int tr = sumRange.Row + r - evalRange.Row;
						int tc = sumRange.Col + c - evalRange.Col;

						if (tr < rows && tc < cols)
						{
							var sumCell = sumWorksheet.GetCell(tr, tc);

							if (sumCell != null && sumCell.InnerData != null
								&& CellUtility.TryGetNumberData(sumWorksheet, sumCell, out data))
							{
								val += data;
								count++;
							}
						}
					}
				}

				return true;
			});

			return count > 0 ? (FormulaValue)(val / count) : FormulaValue.Nil;
		}
		#endregion // AverageIf

		#endregion // Average

		#region Min/Max
		public static FormulaValue Min(Cell cell, FormulaValue[] args)
		{
			double min = 0;
			double data;
			bool first = true;

			foreach (var arg in args)
			{
				switch (arg.type)
				{
					case FormulaValueType.Range:
						ReferenceRange range = RangeFrom(cell, arg);
						Worksheet worksheet = WorksheetFrom(cell, range);
						worksheet.IterateCells(range.Position, (r, c, inCell) =>
						{
							if (CellUtility.TryGetNumberData(worksheet, inCell, out data))
							{
								if (first)
								{
									min = data;
									first = false;
								}
								else if (min > data)
								{
									min = data;
								}
							}
							return true;
						});
						break;

					case FormulaValueType.Number:
						data = (double)arg.value;

						if (first)
						{
							min = data;
							first = false;
						}
						else if (min > data)
						{
							min = data;
						}
						break;

					default:
						return null;
				}
			}

			return min;
		}

		public static FormulaValue Max(Cell cell, FormulaValue[] args)
		{
			double max = 0;
			double data;
			bool first = true;

			foreach (var arg in args)
			{
				switch (arg.type)
				{
					case FormulaValueType.Range:
						ReferenceRange range = RangeFrom(cell, arg);
						Worksheet worksheet = WorksheetFrom(cell, range);
						worksheet.IterateCells(range.Position, (r, c, inCell) =>
						{
							if (CellUtility.TryGetNumberData(worksheet, inCell, out data))
							{
								if (first)
								{
									max = data;
									first = false;
								}
								else if (max < data)
								{
									max = data;
								}
							}
							return true;
						});
						break;

					case FormulaValueType.Number:
						data = (double)arg.value;
						if (first)
						{
							max = data;
							first = false;
						}
						else if (max < data)
						{
							max = data;
						}
						break;

					default:
						return null;
				}
			}

			return max;
		}

		#endregion // Min/Max

		#region VLookup
		public static FormulaValue VLookup(Cell cell, FormulaValue[] args)
		{
			FormulaValue argTarget = args[0];
			ReferenceRange searchRange = RangeFrom(cell, args[1]);
			Worksheet searchWorksheet = WorksheetFrom(cell, searchRange);
			FormulaValue argReturnIndex = args[2];
			FormulaValue argExactMatch = args.Length > 3 ? args[3] : FormulaValue.Nil;
			bool exactMatch = false;
					
			//int matchValueInt = 0;
			double matchValueDouble = 0;
			string matchValueText = null;
			bool matchNumber = true;

			if (argReturnIndex.type != FormulaValueType.Number)
			{
				return null;
			}

			if (argExactMatch.type != FormulaValueType.Nil)
			{
				if (argExactMatch.type != FormulaValueType.Boolean)
				{
					return null;
				}

				exactMatch = (bool)argExactMatch.value;
			}

			#region Match Value
			switch (argTarget.type)
			{
				case FormulaValueType.Number:
					matchValueDouble = (double)argTarget.value;
					break;

				case FormulaValueType.String:
					matchValueText = (string)argTarget.value;
					matchNumber = false;
					break;

				default:
					return null;
			}

			#endregion // Match Value

			int selectColumnIndex = (int)Math.Ceiling((double)argReturnIndex.value);

			int matchedRowIndex = searchRange.EndRow;
			bool found = false;

			if (exactMatch)
			{
				for (int r = searchRange.Row; r <= searchRange.EndRow; r++)
				{
					var cel = searchWorksheet.GetCell(r, searchRange.Col);

					if (cel != null)
					{
						if (matchNumber)
						{
							double cellNumVal = 0;

							if (CellUtility.TryGetNumberData(searchWorksheet, cel, out cellNumVal))
							{
								if (matchValueDouble == cellNumVal)
								{
									matchedRowIndex = r;
									found = true;
									break;
								}
							}
						}
						else
						{
							string cellText = cel.DisplayText;

							if (string.Compare(matchValueText, cellText, true) == 0)
							{
								matchedRowIndex = r;
								found = true;
								break;
							}
						}
					}
				}

				if (!found)
				{
					return null;
				}
			}
			else
			{
				matchedRowIndex = Data.ArrayHelper.QuickFind(searchRange.Row, searchRange.EndRow, i =>
					{
						var cel = searchWorksheet.GetCell(i, searchRange.Col);

						if (cel == null) return 0;

						if (matchNumber)
						{
							double cellNumVal = 0;

							if (CellUtility.TryGetNumberData(searchWorksheet, cel, out cellNumVal))
							{
								if (matchValueDouble < cellNumVal)
								{
									return -1;
								}
								else if (matchValueDouble > cellNumVal)
								{
									return 1;
								}
							}

							return 0;
						}
						else
						{
							string cellText = cel.DisplayText;

							return string.Compare(matchValueText, cellText, true);
						}

					});
			}

			return Evaluator.CreateFormulaValue(searchWorksheet.GetCellData(matchedRowIndex, searchRange.Col + selectColumnIndex - 1));
		}
		#endregion // VLookup

		#endregion // Statistics

		#region Round/Ceiling/Floor

		public static FormulaValue Round(Cell cell, FormulaValue[] args)
		{
			double input = Evaluator.CheckAndGetDefaultValueDouble(cell, args[0]);

			int digits = 0;

			if (args.Length > 1)
			{
				if (args[1].type != FormulaValueType.Number)
				{
					throw new FormulaTypeMismatchException(cell);
				}

				digits = (int)(double)args[1].value;
			}

			return Math.Round(input, digits);
		}

		public static FormulaValue RoundUp(Cell cell, FormulaValue[] args)
		{
			double input = Evaluator.CheckAndGetDefaultValueDouble(cell, args[0]);

			double scale = 1;

			if (args.Length > 1)
			{
				if (args[1].type != FormulaValueType.Number)
				{
					throw new FormulaTypeMismatchException(cell);
				}

				scale = Math.Pow(10, (int)(double)args[1].value);
			}

			return Math.Ceiling(input * scale) / scale;
		}

		public static FormulaValue RoundDown(Cell cell, FormulaValue[] args)
		{
			double input = Evaluator.CheckAndGetDefaultValueDouble(cell, args[0]);

			double scale = 1;

			if (args.Length > 1)
			{
				if (args[1].type != FormulaValueType.Number)
				{
					throw new FormulaTypeMismatchException(cell);
				}

				scale = Math.Pow(10, (int)(double)args[1].value);
			}

			return Math.Floor(input * scale) / scale;
		}

		public static FormulaValue Ceiling(Cell cell, FormulaValue[] args)
		{
			double input = Evaluator.CheckAndGetDefaultValueDouble(cell, args[0]);

			if (args.Length < 2)
			{
				return Math.Ceiling(input);
			}

			if (args[1].type != FormulaValueType.Number)
			{
				throw new FormulaTypeMismatchException(cell);
			}

			double significance = (double)args[1].value;

			if (significance == 0) return 0;

			double m = input % significance;

			if (m == 0)
			{
				return input;
			}
			else
			{
				return input - m + significance;
			}
		}

		public static FormulaValue Floor(Cell cell, FormulaValue[] args)
		{
			double input = Evaluator.CheckAndGetDefaultValueDouble(cell, args[0]);

			if (args.Length < 2)
			{
				return Math.Floor(input);
			}

			if (args[1].type != FormulaValueType.Number)
			{
				throw new FormulaTypeMismatchException(cell);
			}

			double significance = (double)args[1].value;

			if (significance == 0) return 0;

			return input - input % significance;
		}

		#endregion // Round/Ceiling

		#region Address

		public static FormulaValue Row(Cell cell, List<STNode> args)
		{
			if (args == null || args.Count < 1)
			{
				return cell.Row + 1;
			}

			if (args[0].Type != STNodeType.CELL)
			{
				throw new FormulaParameterMismatchException(cell);
			}

			return ((STCellNode)args[0]).Position.Row + 1;
		}

		public static FormulaValue Column(Cell cell, List<STNode> args)
		{
			if (args == null || args.Count < 1)
			{
				return cell.Column + 1;
			}

			if (args[0].Type != STNodeType.CELL)
			{
				throw new FormulaParameterMismatchException(cell);
			}

			return ((STCellNode)args[0]).Position.Col + 1;
		}

		public static FormulaValue Address(Cell cell, FormulaValue[] args)
		{
			if (args[0].type != FormulaValueType.Number
				|| args[1].type != FormulaValueType.Number)
			{
				throw new FormulaTypeMismatchException(cell);
			}

			return (new CellPosition((int)(double)args[0].value - 1, (int)(double)args[1].value - 1)).ToAbsoluteAddress();
		}

		public static FormulaValue Indirect(Cell cell, FormulaValue[] args)
		{
			if (args[0].type != FormulaValueType.String)
			{
				throw new FormulaTypeMismatchException(cell);
			}

			string address = (string)args[0].value;

			if (CellPosition.IsValidAddress(address))
			{
				var pos = new CellPosition(address);
				return cell == null || cell.Worksheet == null ? null : Evaluator.CreateFormulaValue(cell.Worksheet.GetCell(pos));
			}
			else if (RangePosition.IsValidAddress(address))
			{
				return new RangePosition(address);
			}
			else
			{
				throw new FormulaTypeMismatchException(cell);
			}
		}

		#endregion // Address

		#region Condition
		#region IF
		public static FormulaValue If(Cell cell, STNode funNode)
		{
			if (funNode.Children.Count < 1)
			{
				throw new FormulaParameterMismatchException(cell);
			}
			FormulaValue val = Evaluator.Evaluate(cell, funNode.Children[0]);
			int index = 1;
			if (val.type == FormulaValueType.Boolean)
			{
				if ((bool)val.value == false)
				{
					index = 2;
				}
			}
			else if (val.type == FormulaValueType.Number)
			{
				if ((double)val.value == 0)
				{
					index = 2;
				}
			}
			else if (val.type == FormulaValueType.DateTime)
			{
				if (((DateTime)val.value).ToOADate() == 0)
				{
					index = 2;
				}
			}
			else
			{
				throw new FormulaTypeMismatchException(cell);
			}
			if (index < funNode.Children.Count)
			{
				val = Evaluator.Evaluate(cell, funNode.Children[index]);
			}
			else
			{
				val = null;
			}
			return val;
		}
		#endregion // IF

		#region AND
		public static FormulaValue And(Cell cell, List<STNode> list)
		{
			foreach (var node in list)
			{
				FormulaValue val = Evaluator.Evaluate(cell, node);

				if (val.type == FormulaValueType.Boolean)
				{
					if ((bool)val.value == false)
					{
						return false;
					}
				}
				else if (val.type == FormulaValueType.Number)
				{
					if ((double)val.value == 0)
					{
						return false;
					}
				}
				else if (val.type == FormulaValueType.DateTime)
				{
					if (((DateTime)val.value).ToOADate() == 0)
					{
						return false;
					}
				}
				else
				{
					throw new FormulaTypeMismatchException(cell);
				}
			}

			return true;
		}
		#endregion // AND

		#region OR
		public static FormulaValue Or(Cell cell, List<STNode> list)
		{
			if (list == null || list.Count <= 0)
			{
				throw new FormulaParameterMismatchException(cell, "At least one parameter is needed, but nothing specified.");
			}

			foreach (var node in list)
			{
				FormulaValue val = Evaluator.Evaluate(cell, node);

				if (val.type == FormulaValueType.Boolean)
				{
					if ((bool)val.value == true)
					{
						return true;
					}
				}
				else if (val.type == FormulaValueType.Number)
				{
					if ((double)val.value != 0)
					{
						return true;
					}
				}
				else if (val.type == FormulaValueType.DateTime)
				{
					if (((DateTime)val.value).ToOADate() != 0)
					{
						return true;
					}
				}
				else
				{
					throw new FormulaTypeMismatchException(cell);
				}
			}

			return false;
		}
		#endregion // OR

		#region NOT
		public static FormulaValue Not(Cell cell, STNode arg0)
		{
			FormulaValue val = Evaluator.Evaluate(cell, arg0);

			if (val.type == FormulaValueType.Boolean)
			{
				return (bool)val.value == false;
			}
			else if (val.type == FormulaValueType.Number)
			{
				return (double)val.value == 0;
			}
			else if (val.type == FormulaValueType.DateTime)
			{
				return ((DateTime)val.value).ToOADate() == 0;
			}
			else
			{
				throw new FormulaTypeMismatchException(cell);
			}
		}
		#endregion // NOT



		#endregion // Condition

		#region Date

		#endregion // Date
	}
}

#endif // FORMULA