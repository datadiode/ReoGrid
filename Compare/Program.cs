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
using unvell.ReoGrid.WinForm;

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
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static int Main(string[] args)
		{
			var arguments = new ArrayList(args);
			int i;
			if ((i = arguments.IndexOf("/About")) != -1)
			{
				arguments.RemoveAt(i);
				var dlg = new AboutForm();
				if (i < arguments.Count)
					dlg.Text = (string)arguments[i];
				dlg.ShowDialog();
				return 0;
			}
			var mainForm = new ReoGridCompare();
			mainForm.ParseArguments(arguments);
			if (mainForm.FormBorderStyle == FormBorderStyle.None)
				mainForm.Show();
			else
				Application.Run(mainForm);
			return 0;
		}
	}
}
