using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Input;
using unvell.Common.Win32Lib;
using unvell.ReoGrid.Editor.LangRes;

namespace unvell.ReoGrid.Editor
{
	public class FilePathBar : TextBox
	{
		public enum SeletionType
		{
			TabDelimited,
			CommaDelimited,
			SemicolonDelimited,
			PipeDelimited,
			AutoDetect,
			Clear,
			Nop
		}

		private string GetDroppedFileName(DragEventArgs e)
		{
			IDataObject data = e.Data;
			if (data == null)
				return null;
			Array array = data.GetData(System.Windows.DataFormats.FileDrop) as Array;
			if (array == null || array.Length == 0)
				return null;
			object value = array.GetValue(0);
			if (value == null)
				return null;
			return value.ToString();
		}

		private static readonly StringFormat format = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Near
		};

		private readonly TabControl tc;
		private string path;
		private bool dirty = false;
		private bool active = false;

		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Dirty
		{
			get => dirty;
			set
			{
				if (dirty != value)
				{
					dirty = value;
					LayoutNeeded(this, EventArgs.Empty);
				}
			}
		}

		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public SeletionType Selection
		{
			get => (SeletionType)tc.SelectedIndex;
			set => tc.SelectedIndex = (int)value;
		}

		public event EventHandler SelectionChanged;

		private void AlignButtons()
		{
			tc.Left = Width - tc.GetTabRect(tc.AllowDrop ? 5 : 4).Right;
			tc.Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
			tc.SelectedIndex = -1;
		}

		private void BrowseForFile(string defext)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.AddExtension = true;
			dlg.DefaultExt = defext;
			dlg.FileName = Text;
			dlg.Filter = LangResource.Filter_Load_File;
			if (dlg.ShowDialog(this) == DialogResult.OK)
			{
				Text = dlg.FileName;
				Modified = true;
			}
		}

		public void SetupUILanguage()
		{
			tc.TabPages[0].ToolTipText = LangResource.OpenTabDelimitedFile;
			tc.TabPages[1].ToolTipText = LangResource.OpenCommaDelimitedFile;
			tc.TabPages[2].ToolTipText = LangResource.OpenSemicolonDelimitedFile;
			tc.TabPages[3].ToolTipText = LangResource.OpenPipeDelimitedFile;
			tc.TabPages[4].ToolTipText = LangResource.OpenNativeWorkbook;
			tc.TabPages[5].ToolTipText = LangResource.CloseCurrentFile;
			LayoutNeeded(this, EventArgs.Empty);
		}

		protected override void OnBorderStyleChanged(EventArgs e)
		{
			AlignButtons();
			base.OnBorderStyleChanged(e);
		}

		// Constructor
		public FilePathBar()
		{
			Multiline = true;
			ReadOnly = true;
			GotFocus += (s, e) =>
			{
				ReadOnly = true;
				LayoutNeeded(this, EventArgs.Empty);
				if (string.IsNullOrEmpty(path))
				{
					Win32.HideCaret(Handle);
				}
			};
			LostFocus += (s, e) =>
			{
				// Mirror Active as ReadOnly for get_accState()
				ReadOnly = active;
				LayoutNeeded(this, EventArgs.Empty);
			};
			SizeChanged += LayoutNeeded;
			Layout += LayoutNeeded;
			tc = new TabControl();
			tc.Appearance = TabAppearance.Buttons;
			tc.CreateControl();
			tc.ShowToolTips = true;
			tc.TabPages.Add("tsv");
			tc.TabPages.Add("csv");
			tc.TabPages.Add("; ; ;");
			tc.TabPages.Add("| | |");
			tc.TabPages.Add("...");
			tc.TabPages.Add("\u00D7");
			tc.TabPages.Add("");
			tc.TabIndex = tc.TabCount;
			tc.SizeMode = TabSizeMode.Fixed;
			tc.DrawMode = TabDrawMode.OwnerDrawFixed;
			tc.DrawItem += (s, e) =>
			{
				TabPage page = tc.TabPages[e.Index];
				Rectangle rect = tc.GetTabRect(e.Index);
				e.Graphics.FillRectangle(SystemBrushes.ButtonFace, rect);
				rect = new Rectangle(rect.Left, rect.Top + 1, rect.Width - 2, rect.Height - 2);
				e.Graphics.DrawString(page.Text, tc.Font, SystemBrushes.WindowText, rect, format);
				if (e.Index == tc.TabIndex)
				{
					rect = new Rectangle(rect.Left + 3, rect.Top + tc.Font.Height, rect.Width - 5, 1);
					e.Graphics.FillRectangle(SystemBrushes.WindowText, rect);
				}
			};
			tc.ItemSize = new System.Drawing.Size(25, Height);
			tc.TabStop = true;
			tc.SelectedIndexChanged += (s, e) =>
			{
				if (tc.SelectedIndex == -1)
					return;
				if (!AllowDrop)
				{
					Modified = true;
				}
				else if (!Modified)
				{
					switch (tc.SelectedIndex)
					{
						case 0:
							BrowseForFile("tsv");
							break;
						case 1:
						case 2:
						case 3:
							BrowseForFile("csv");
							break;
						case 4:
							BrowseForFile("xlsx");
							break;
						case 5:
							Text = string.Empty;
							Modified = true;
							break;
					}
				}
				if (Modified)
				{
					Dirty = false;
					SelectionChanged?.Invoke(s, e);
					tc.TabIndex = Modified ? tc.TabCount : tc.SelectedIndex;
					Modified = false;
					tc.Invalidate();
				}
				Enabled = string.IsNullOrEmpty(path);
				Win32.SendMessage(tc.Handle, (int)Win32.WMessages.TCM_SETCURFOCUS, (IntPtr)SeletionType.Nop, IntPtr.Zero);
				tc.SelectedIndex = -1;
				if (Enabled)
					Focus();
				else
					Enabled = true;
			};
			tc.DragDrop += (s, e) =>
			{
				var path = GetDroppedFileName(e);
				if (path != null)
				{
					Text = path;
					Modified = true;
					tc.SelectedIndex = Win32.SendMessage(tc.Handle, (int)Win32.WMessages.TCM_GETCURFOCUS, IntPtr.Zero, IntPtr.Zero);
				}
				else
				{
					Win32.SendMessage(tc.Handle, (int)Win32.WMessages.TCM_SETCURFOCUS, (IntPtr)SeletionType.Nop, IntPtr.Zero);
				}
			};
			tc.DragEnter += tab_DragOver;
			tc.DragOver += tab_DragOver;
			tc.DragLeave += (s, e) =>
			{
				Win32.SendMessage(tc.Handle, (int)Win32.WMessages.TCM_SETCURFOCUS, (IntPtr)SeletionType.Nop, IntPtr.Zero);
			};
			tc.Cursor = System.Windows.Forms.Cursors.Arrow;
			Controls.Add(tc);
			tc.Width = 4000;
			AllowDrop = true;
		}
		private void tab_DragOver(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
			{
				Win32.TCHITTESTINFO hittest = new Win32.TCHITTESTINFO();
				hittest.pt = tc.PointToClient(new Point(e.X, e.Y));
				int hit = Win32.SendMessage(tc.Handle, (int)Win32.WMessages.TCM_HITTEST, IntPtr.Zero, ref hittest);
				if (hit == -1 || hit == (int)SeletionType.Clear)
				{
					hit = (int)SeletionType.Nop;
					e.Effect = DragDropEffects.None;
				}
				else
				{
					e.Effect = DragDropEffects.Copy;
				}
				Win32.SendMessage(tc.Handle, (int)Win32.WMessages.TCM_SETCURFOCUS, (IntPtr)hit, IntPtr.Zero);
			}
		}

		private void ShowText(string text)
		{
			bool tmp = Modified;
			base.Text = text;
			Modified = tmp;
		}

		private void LayoutNeeded(object sender, EventArgs e)
		{
			var rc = new Win32.RECT();
			rc.left = 2;
			rc.top = 2;
			rc.right = tc.Left;
			rc.bottom = ClientSize.Height;
			Win32.SendMessage(Handle, (int)Win32.WMessages.EM_SETRECT, IntPtr.Zero, ref rc);
			if (string.IsNullOrEmpty(path))
			{
				ShowText(LangResource.PleaseSelectSomeFile);
				Select(0, 0);
			}
			else if (Focused)
			{
				ShowText(path);
				if (tc.SelectedIndex != -1)
					Select(0, 0);
			}
			else
			{
				var ellipsified = string.Copy(path);
				var sizeAvail = new System.Drawing.Size(rc.right - rc.left, Height);
				if (dirty)
					sizeAvail.Width -= TextRenderer.MeasureText("* ", Font).Width;
				TextRenderer.MeasureText(ellipsified, Font, sizeAvail, TextFormatFlags.ModifyString | TextFormatFlags.PathEllipsis);
				ShowText(dirty ? "* " + ellipsified : ellipsified);
			}
		}

		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Active
		{
			get => active;
			set
			{
				active = value;
				// Unless focused, mirror Active as ReadOnly for get_accState()
				if (!Focused)
					ReadOnly = active;
				BackColor = active ? SystemColors.ActiveCaption : SystemColors.InactiveCaption;
				ForeColor = active ? SystemColors.ActiveCaptionText : SystemColors.InactiveCaptionText;
			}
		}

		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public new bool AllowDrop
		{
			get
			{
				return tc.AllowDrop;
			}
			set
			{
				tc.AllowDrop = value;
				AlignButtons();
			}
		}

		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public new string Text
		{
			get
			{
				return path;
			}
			set
			{
				path = value;
				LayoutNeeded(this, EventArgs.Empty);
			}
		}
	}
}
