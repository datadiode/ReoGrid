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
 * ReoGrid and ReoGridEditor is released under MIT license.
 *
 * Copyright (c) 2012-2016 Jing <lujing at unvell.com>
 * Copyright (c) 2012-2016 unvell.com, all rights reserved.
 * 
 ****************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace unvell.ReoGrid.Editor
{
	class ToolStripRenderer : ToolStripProfessionalRenderer
	{
		protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
		{
			// Suppress pressedness visualization while doing PerformClick()
			if (e.Item.Pressed && !e.Item.Selected)
				return;
			base.OnRenderButtonBackground(e);
		}
	}

	static class Program
	{
		static Program()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
		}

		/// <summary>
		/// https://github.com/adamabdelhamed/PowerArgs
		/// Copyright (c) 2013 Adam Abdelhamed
		/// SPDX-License-Identifier: MIT
		/// Converts a single string that represents a command line to be executed into a
		/// string list, accounting for quoted arguments that may or may not contain spaces.
		/// </summary>
		/// <param name="commandLine">The raw arguments as a single string</param>
		/// <returns>a converted string list with the arguments properly broken up</returns>
		public static List<string> SplitCommandLine(string commandLine)
		{
			List<string> ret = new List<string>();
			string currentArg = string.Empty;
			bool insideDoubleQuotes = false;

			for (int i = 0; i < commandLine.Length; i++)
			{
				var c = commandLine[i];

				if (insideDoubleQuotes && c == '"')
				{
					ret.Add(currentArg);
					currentArg = string.Empty;
					insideDoubleQuotes = !insideDoubleQuotes;
				}
				else if (!insideDoubleQuotes && c == ' ')
				{
					if (currentArg.Length > 0)
					{
						ret.Add(currentArg);
						currentArg = string.Empty;
					}
				}
				else if (c == '"')
				{
					insideDoubleQuotes = !insideDoubleQuotes;
				}
				else if (c == '\\' && i < commandLine.Length - 1 && commandLine[i + 1] == '"')
				{
					currentArg += '"';
					i++;
				}
				else
				{
					currentArg += c;
				}
			}

			if (currentArg.Length > 0)
			{
				ret.Add(currentArg);
			}

			return ret;
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			var arguments = new ArrayList(Environment.GetCommandLineArgs());
			arguments.RemoveAt(0);
			var mainForm = new ReoGridCompare();
			mainForm.ParseArguments(arguments);
			Application.Run(mainForm);
		}
	}
}
