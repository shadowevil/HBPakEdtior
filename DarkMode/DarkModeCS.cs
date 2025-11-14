using Microsoft.Win32;
using System.Drawing;
using System;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Xml.Linq;
using System.Reflection.Metadata;


namespace DarkModeForms
{
    /// <summary>This tries to automatically apply Windows Dark Mode (if enabled) to a Form.
    /// <para>Author: BlueMystic (bluemystic.play@gmail.com)  2024</para></summary>
    public class DarkModeCS
    {
        #region Win32 API Declarations

        public struct DWMCOLORIZATIONcolors
        {
            public uint ColorizationColor,
              ColorizationAfterglow,
              ColorizationColorBalance,
              ColorizationAfterglowBalance,
              ColorizationBlurBalance,
              ColorizationGlassReflectionIntensity,
              ColorizationOpaqueBlend;
        }

        [Flags]
        public enum DWMWINDOWATTRIBUTE : uint
        {
            DWMWA_NCRENDERING_ENABLED = 1,
            DWMWA_NCRENDERING_POLICY,
            DWMWA_TRANSITIONS_FORCEDISABLED,
            DWMWA_ALLOW_NCPAINT,
            DWMWA_CAPTION_BUTTON_BOUNDS,
            DWMWA_NONCLIENT_RTL_LAYOUT,
            DWMWA_FORCE_ICONIC_REPRESENTATION,
            DWMWA_FLIP3D_POLICY,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            DWMWA_HAS_ICONIC_BITMAP,
            DWMWA_DISALLOW_PEEK,
            DWMWA_EXCLUDED_FROM_PEEK,
            DWMWA_CLOAK,
            DWMWA_CLOAKED,
            DWMWA_FREEZE_REPRESENTATION,
            DWMWA_USE_HOSTBACKDROPBRUSH,
            DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
            DWMWA_BORDER_COLOR,
            DWMWA_CAPTION_COLOR,
            DWMWA_TEXT_COLOR,
            DWMWA_VISIBLE_FRAME_BORDER_THICKNESS,
            DWMWA_LAST,
        }

        [Flags]
        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        private const int GWLP_WNDPROC = -4;
        private const int WM_SETTINGSCHANGE = 0x001A;
        private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
        private const int WM_THEMECHANGED = 0x031A;

        [DllImport("dwmapi.dll", CharSet = CharSet.Auto)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Auto)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("dwmapi.dll", EntryPoint = "#127", PreserveSig = false)]
        private static extern void DwmGetColorizationParameters(ref DWMCOLORIZATIONcolors colors);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(HandleRef hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        #endregion Win32 API Declarations

        #region Private Static Members

        private static readonly ControlStatusStorage controlStatusStorage = new ControlStatusStorage();
        private static ControlEventHandler ownerFormControlAdded;
        private static EventHandler controlHandleCreated;
        private static ControlEventHandler controlControlAdded;

        private bool _IsDarkMode;
        private IntPtr originalWndProc;
        private WndProcDelegate newWndProcDelegate;
        private IntPtr formHandle;
        private bool applyingTheme;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        #endregion


        #region Public Members

        public enum DisplayMode
        {
            SystemDefault,
            ClearMode,
            DarkMode
        }

        public DisplayMode ColorMode { get; set; } = DisplayMode.SystemDefault;
        public bool IsDarkMode { get { return _IsDarkMode; } }
        public bool ColorizeIcons { get; set; } = true;
        public bool RoundedPanels { get; set; } = false;
        public Form OwnerForm { get; set; }
        public ComponentCollection Components { get; set; }
        public OSThemeColors OScolors { get; set; }

        #endregion Public Members

        #region Constructors

        public DarkModeCS(Form _Form, bool _ColorizeIcons = true, bool _RoundedPanels = false)
        {
            if (_Form == null)
                return;

            OwnerForm = _Form;
            Components = null;
            ColorizeIcons = _ColorizeIcons;
            RoundedPanels = _RoundedPanels;

            if (originalWndProc == IntPtr.Zero)
            {
                _Form.HandleCreated += (sender, e) =>
                {
                    HandleRef handleRef = new HandleRef(_Form, _Form.Handle);
                    newWndProcDelegate = CustomWndProc;
                    originalWndProc = SetWindowLongPtr(handleRef, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(newWndProcDelegate));
                };
            }

            _Form.Load += (sender, e) =>
            {
                _IsDarkMode = isDarkMode();
                if (ColorMode != DisplayMode.SystemDefault)
                {
                    _IsDarkMode = ColorMode == DisplayMode.DarkMode;
                }

                ApplyTheme(_IsDarkMode);
            };
        }

        private IntPtr CustomWndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_SETTINGSCHANGE && !applyingTheme)
            {
                applyingTheme = true;
                _IsDarkMode = isDarkMode();
                if (ColorMode != DisplayMode.SystemDefault)
                {
                    _IsDarkMode = ColorMode == DisplayMode.DarkMode;
                }

                ApplyTheme(_IsDarkMode);
                applyingTheme = false;
            }

            return CallWindowProc(originalWndProc, hWnd, msg, wParam, lParam);
        }


        public bool isDarkMode()
        {
            return GetWindowsColorMode() <= 0;
        }

        #endregion Constructors

        #region Public Methods

        public void ApplyTheme(bool pIsDarkMode = true)
        {
            if (OwnerForm == null)
                return;

            try
            {
                _IsDarkMode = _IsDarkMode != pIsDarkMode ? pIsDarkMode : _IsDarkMode;

                OScolors = GetSystemColors(OwnerForm, pIsDarkMode ? 0 : 1);

                if (OScolors != null)
                {
                    ApplySystemDarkTheme(OwnerForm, pIsDarkMode);

                    OwnerForm.BackColor = OScolors.Background;
                    OwnerForm.ForeColor = OScolors.TextInactive;

                    if (OwnerForm != null && OwnerForm.Controls != null)
                    {
                        foreach (Control _control in OwnerForm.Controls)
                        {
                            ThemeControl(_control);
                        }

                        if (ownerFormControlAdded == null)
                            ownerFormControlAdded = (sender, e) =>
                            {
                                ThemeControl(e.Control);
                            };
                        OwnerForm.ControlAdded -= ownerFormControlAdded;
                        OwnerForm.ControlAdded += ownerFormControlAdded;
                    }

                    if (Components != null)
                    {
                        foreach (var item in Components.OfType<ContextMenuStrip>())
                            ThemeControl(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ApplyTheme(DisplayMode pColorMode)
        {
            if (ColorMode == pColorMode) return;

            ColorMode = pColorMode;
            _IsDarkMode = isDarkMode();
            if (ColorMode != DisplayMode.SystemDefault)
            {
                _IsDarkMode = ColorMode == DisplayMode.DarkMode;
            }

            ApplyTheme(_IsDarkMode);
        }

        public void ThemeControl(Control control)
        {
            var info = controlStatusStorage.GetControlStatusInfo(control);
            if (info != null)
            {
                if (info.IsExcluded) return;
                if (info.LastThemeAppliedIsDark == IsDarkMode) return;
                info.LastThemeAppliedIsDark = IsDarkMode;
            }
            else
            {
                controlStatusStorage.RegisterProcessedControl(control, IsDarkMode);
            }

            BorderStyle BStyle = (IsDarkMode ? BorderStyle.FixedSingle : BorderStyle.Fixed3D);
            FlatStyle FStyle = (IsDarkMode ? FlatStyle.Flat : FlatStyle.Standard);

            if (controlHandleCreated == null) controlHandleCreated = (sender, e) =>
            {
                ApplySystemDarkTheme((Control)sender, IsDarkMode);
            };
            control.HandleCreated -= controlHandleCreated;
            control.HandleCreated += controlHandleCreated;

            if (controlControlAdded == null) controlControlAdded = (sender, e) =>
            {
                ThemeControl(e.Control);
            };
            control.ControlAdded -= controlControlAdded;
            control.ControlAdded += controlControlAdded;

            string Mode = IsDarkMode ? "DarkMode_Explorer" : "ClearMode_Explorer";
            SetWindowTheme(control.Handle, Mode, null);

            control.GetType().GetProperty("BackColor")?.SetValue(control, OScolors.Control);
            control.GetType().GetProperty("ForeColor")?.SetValue(control, OScolors.TextActive);

            if (control is Label lbl)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, control.Parent.BackColor);
                control.GetType().GetProperty("BorderStyle")?.SetValue(control, BorderStyle.None);
                control.Paint += (sender, e) =>
                {
                    if (control.Enabled == false && IsDarkMode)
                    {
                        e.Graphics.Clear(control.Parent.BackColor);
                        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

                        using (Brush B = new SolidBrush(control.ForeColor))
                        {
                            MethodInfo mi = lbl.GetType().GetMethod("CreateStringFormat", BindingFlags.NonPublic | BindingFlags.Instance);
                            StringFormat sf = mi.Invoke(lbl, new object[] { }) as StringFormat;

                            e.Graphics.DrawString(lbl.Text, lbl.Font, B, new PointF(1, 0), sf);
                        }
                    }
                };
            }
            if (control is LinkLabel)
            {
                control.GetType().GetProperty("LinkColor")?.SetValue(control, OScolors.AccentLight);
                control.GetType().GetProperty("VisitedLinkColor")?.SetValue(control, OScolors.Primary);
            }
            if (control is TextBox)
            {
                control.GetType().GetProperty("BorderStyle")?.SetValue(control, BStyle);
            }
            if (control is NumericUpDown)
            {
                Mode = IsDarkMode ? "DarkMode_ItemsView" : "ClearMode_ItemsView";
                SetWindowTheme(control.Handle, Mode, null);
            }
            if (control is Button)
            {
                var button = control as Button;
                button.FlatStyle = IsDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                button.FlatAppearance.CheckedBackColor = OScolors.Accent;
                button.BackColor = OScolors.Control;
                button.FlatAppearance.BorderColor = (OwnerForm.AcceptButton == button) ?
                  OScolors.Accent : OScolors.Control;
            }
            if (control is ComboBox comboBox)
            {
                if (comboBox.DropDownStyle != ComboBoxStyle.DropDownList)
                {
                    comboBox.SelectionStart = comboBox.Text.Length;
                }
                control.BeginInvoke(new Action(() =>
                {
                    if (!((ComboBox)control).DropDownStyle.Equals(ComboBoxStyle.DropDownList))
                        ((ComboBox)control).SelectionLength = 0;
                }));

                if (!control.Enabled && IsDarkMode)
                {
                    comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                }

                Mode = IsDarkMode ? "DarkMode_CFD" : "ClearMode_CFD";
                SetWindowTheme(control.Handle, Mode, null);
            }
            if (control is Panel)
            {
                var panel = control as Panel;
                panel.BackColor = OScolors.Background;
                panel.BorderStyle = BorderStyle.None;
                if (!(panel.Parent is TabControl) || !(panel.Parent is TableLayoutPanel))
                {
                    if (RoundedPanels)
                    {
                        SetRoundBorders(panel, 6, OScolors.SurfaceDark, 1);
                    }
                }
            }
            if (control is GroupBox)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, control.Parent.BackColor);
                control.GetType().GetProperty("ForeColor")?.SetValue(control, OScolors.TextActive);
                control.Paint += (sender, e) =>
                {
                    if (control.Enabled == false && IsDarkMode)
                    {
                        var radio = (sender as GroupBox);
                        Brush B = new SolidBrush(control.ForeColor);

                        e.Graphics.DrawString(radio.Text, radio.Font,
                          B, new PointF(6, 0));
                    }
                };
            }
            if (control is TableLayoutPanel)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, control.Parent.BackColor);
                control.GetType().GetProperty("ForeColor")?.SetValue(control, OScolors.TextInactive);
                control.GetType().GetProperty("BorderStyle")?.SetValue(control, BorderStyle.None);
            }
            if (control is TabControl && control is not FlatTabControl)
            {
                var tab = control as TabControl;
                tab.Appearance = TabAppearance.Normal;
                tab.DrawMode = TabDrawMode.OwnerDrawFixed;
                tab.DrawItem += (sender, e) =>
                {
                    using (SolidBrush backColor = new SolidBrush(tab.Parent!.BackColor))
                    {
                        e.Graphics.FillRectangle(backColor, tab.ClientRectangle);
                    }

                    using (Brush tabBack = new SolidBrush(OScolors.Surface))
                    {
                        for (int i = 0; i < tab.TabPages.Count; i++)
                        {
                            TabPage tabPage = tab.TabPages[i];
                            tabPage.BackColor = OScolors.Surface;
                            tabPage.BorderStyle = BorderStyle.FixedSingle;
                            tabPage.ControlAdded += (_s, _e) =>
                            {
                                ThemeControl(_e.Control);
                            };

                            var tBounds = e.Bounds;

                            bool IsSelected = (tab.SelectedIndex == i);
                            if (IsSelected)
                            {
                                e.Graphics.FillRectangle(tabBack, tBounds);
                                TextRenderer.DrawText(e.Graphics, tabPage.Text, tabPage.Font, e.Bounds, OScolors.TextActive);
                            }
                            else
                            {
                                TextRenderer.DrawText(e.Graphics, tabPage.Text, tabPage.Font, tab.GetTabRect(i), OScolors.TextInactive);
                            }
                        }
                    }
                };
            }
            if (control is PictureBox)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, control.Parent.BackColor);
                control.GetType().GetProperty("ForeColor")?.SetValue(control, OScolors.TextActive);
                control.GetType().GetProperty("BorderStyle")?.SetValue(control, BorderStyle.None);
            }
            if (control is CheckBox)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, control.Parent.BackColor);
                control.ForeColor = control.Enabled ? OScolors.TextActive : OScolors.TextInactive;
                control.Paint += (sender, e) =>
                {
                    if (control.Enabled == false && IsDarkMode)
                    {
                        var radio = (sender as CheckBox);
                        Brush B = new SolidBrush(control.ForeColor);

                        e.Graphics.DrawString(radio.Text, radio.Font,
                          B, new PointF(16, 0));
                    }
                };
            }
            if (control is RadioButton)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, control.Parent.BackColor);
                control.ForeColor = control.Enabled ? OScolors.TextActive : OScolors.TextInactive;
                control.Paint += (sender, e) =>
                {
                    if (control.Enabled == false && IsDarkMode)
                    {
                        var radio = (sender as RadioButton);
                        Brush B = new SolidBrush(control.ForeColor);

                        e.Graphics.DrawString(radio.Text, radio.Font,
                          B, new PointF(16, 0));
                    }
                };
            }
            if (control is MenuStrip)
            {
                (control as MenuStrip).RenderMode = ToolStripRenderMode.Professional;
                (control as MenuStrip).Renderer = new MyRenderer(new CustomColorTable(OScolors), ColorizeIcons)
                {
                    MyColors = OScolors
                };
            }
            if (control is ToolStrip)
            {
                (control as ToolStrip).RenderMode = ToolStripRenderMode.Professional;
                (control as ToolStrip).Renderer = new MyRenderer(new CustomColorTable(OScolors), ColorizeIcons) { MyColors = OScolors };
            }
            if (control is ToolStripPanel)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, control.Parent.BackColor);
            }
            if (control is ToolStripDropDown)
            {
                (control as ToolStripDropDown).Opening -= Tsdd_Opening;
                (control as ToolStripDropDown).Opening += Tsdd_Opening;
            }
            if (control is ToolStripDropDownMenu)
            {
                (control as ToolStripDropDownMenu).Opening -= Tsdd_Opening;
                (control as ToolStripDropDownMenu).Opening += Tsdd_Opening;
            }
            if (control is ContextMenuStrip)
            {
                (control as ContextMenuStrip).RenderMode = ToolStripRenderMode.Professional;
                (control as ContextMenuStrip).Renderer = new MyRenderer(new CustomColorTable(OScolors), ColorizeIcons) { MyColors = OScolors };
                (control as ContextMenuStrip).Opening -= Tsdd_Opening;
                (control as ContextMenuStrip).Opening += Tsdd_Opening;
            }
            if (control is MdiClient)
            {
                control.GetType().GetProperty("BackColor")?.SetValue(control, OScolors.Surface);
            }
            if (control is PropertyGrid)
            {
                var pGrid = control as PropertyGrid;
                pGrid.BackColor = OScolors.Control;
                pGrid.ViewBackColor = OScolors.Control;
                pGrid.LineColor = OScolors.Surface;
                pGrid.ViewForeColor = OScolors.TextActive;
                pGrid.ViewBorderColor = OScolors.ControlDark;
                pGrid.CategoryForeColor = OScolors.TextActive;
                pGrid.CategorySplitterColor = OScolors.ControlLight;
            }
            if (control is ListView)
            {
                var lView = control as ListView;
                Mode = IsDarkMode ? "DarkMode_Explorer" : "ClearMode_Explorer";
                SetWindowTheme(control.Handle, Mode, null);

                if (lView.View == View.Details)
                {
                    lView.Items[0].UseItemStyleForSubItems = false;
                    lView.OwnerDraw = true;
                    lView.DrawColumnHeader += (sender, e) =>
                    {
                        using (SolidBrush backBrush = new SolidBrush(OScolors.ControlLight))
                        {
                            using (SolidBrush foreBrush = new SolidBrush(OScolors.TextActive))
                            {
                                using (var sf = new StringFormat())
                                {
                                    sf.Alignment = StringAlignment.Center;
                                    e.Graphics.FillRectangle(backBrush, e.Bounds);
                                    e.Graphics.DrawString(e.Header.Text, lView.Font, foreBrush, e.Bounds, sf);
                                }
                            }
                        }
                    };
                    lView.DrawItem += (sender, e) => { e.DrawDefault = true; };
                    lView.DrawSubItem += (sender, e) =>
                    {
                        e.DrawDefault = true;
                    };

                    Mode = IsDarkMode ? "DarkMode_Explorer" : "ClearMode_Explorer";
                    SetWindowTheme(control.Handle, Mode, null);
                }
            }
            if (control is TreeView)
            {
                control.GetType().GetProperty("BorderStyle")?.SetValue(control, BorderStyle.None);
            }
            if (control is DataGridView)
            {
                var grid = control as DataGridView;
                grid.EnableHeadersVisualStyles = false;
                grid.BorderStyle = BorderStyle.FixedSingle;
                grid.BackgroundColor = OScolors.Control;
                grid.GridColor = OScolors.Control;

                grid.Paint += (sender, e) =>
                {
                    DataGridView dgv = sender as DataGridView;

                    HScrollBar hs = (HScrollBar)typeof(DataGridView).GetProperty("HorizontalScrollBar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dgv);
                    if (hs.Visible)
                    {
                        VScrollBar vs = (VScrollBar)typeof(DataGridView).GetProperty("VerticalScrollBar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dgv);

                        if (vs.Visible)
                        {
                            Brush brush = new SolidBrush(OScolors.SurfaceDark);
                            var w = vs.Size.Width;
                            var h = hs.Size.Height;
                            e.Graphics.FillRectangle(brush, dgv.ClientRectangle.X + dgv.ClientRectangle.Width - w - 1,
                              dgv.ClientRectangle.Y + dgv.ClientRectangle.Height - h - 1, w, h);
                        }
                    }
                };

                grid.DefaultCellStyle.BackColor = OScolors.Surface;
                grid.DefaultCellStyle.ForeColor = OScolors.TextActive;

                grid.ColumnHeadersDefaultCellStyle.BackColor = OScolors.Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = OScolors.TextActive;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = OScolors.Surface;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

                grid.RowHeadersDefaultCellStyle.BackColor = OScolors.Surface;
                grid.RowHeadersDefaultCellStyle.ForeColor = OScolors.TextActive;
                grid.RowHeadersDefaultCellStyle.SelectionBackColor = OScolors.Surface;
                grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            }
            if (control is RichTextBox richText)
            {
                richText.BackColor = richText.Parent.BackColor;
                richText.BorderStyle = BorderStyle.None;
            }
            if (control is FlowLayoutPanel flowLayout)
            {
                flowLayout.BackColor = flowLayout.Parent.BackColor;
                flowLayout.BorderStyle = BorderStyle.None;
            }

            if (control.ContextMenuStrip != null)
                ThemeControl(control.ContextMenuStrip);

            foreach (Control childControl in control.Controls)
            {
                ThemeControl(childControl);
            }
        }

        public static void ExcludeFromProcessing(Control control)
        {
            controlStatusStorage.ExcludeFromProcessing(control);
        }

        public static int GetWindowsColorMode(bool GetSystemColorModeInstead = false)
        {
            try
            {
                return (int)Registry.GetValue(
                  @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                  GetSystemColorModeInstead ? "SystemUsesLightTheme" : "AppsUseLightTheme",
                  -1);
            }
            catch
            {
                return 1;
            }
        }

        public static Color GetWindowsAccentColor()
        {
            try
            {
                DWMCOLORIZATIONcolors colors = new DWMCOLORIZATIONcolors();
                DwmGetColorizationParameters(ref colors);

                if (IsWindows10orGreater())
                {
                    var color = colors.ColorizationColor;
                    var colorValue = long.Parse(color.ToString(), System.Globalization.NumberStyles.HexNumber);

                    var transparency = (colorValue >> 24) & 0xFF;
                    var red = (colorValue >> 16) & 0xFF;
                    var green = (colorValue >> 8) & 0xFF;
                    var blue = (colorValue >> 0) & 0xFF;

                    return Color.FromArgb((int)transparency, (int)red, (int)green, (int)blue);
                }
                else
                {
                    return Color.CadetBlue;
                }
            }
            catch (Exception)
            {
                return Color.CadetBlue;
            }
        }

        public static Color GetWindowsAccentOpaqueColor()
        {
            DWMCOLORIZATIONcolors colors = new DWMCOLORIZATIONcolors();
            DwmGetColorizationParameters(ref colors);

            if (IsWindows10orGreater())
            {
                var color = colors.ColorizationColor;
                var colorValue = long.Parse(color.ToString(), System.Globalization.NumberStyles.HexNumber);

                var red = (colorValue >> 16) & 0xFF;
                var green = (colorValue >> 8) & 0xFF;
                var blue = (colorValue >> 0) & 0xFF;

                return Color.FromArgb(255, (int)red, (int)green, (int)blue);
            }
            else
            {
                return Color.CadetBlue;
            }
        }

        public static OSThemeColors GetSystemColors(Form Window = null, int ColorMode = 0)
        {
            OSThemeColors _ret = new OSThemeColors();

            if (ColorMode <= 0)
            {
                // Dark Mode - Try to get colors from Windows Registry
                var darkColors = GetWindowsDarkModeColors();

                _ret.Background = darkColors.Background;
                _ret.BackgroundDark = darkColors.BackgroundDark;
                _ret.BackgroundLight = darkColors.BackgroundLight;

                _ret.Surface = darkColors.Surface;
                _ret.SurfaceLight = darkColors.SurfaceLight;
                _ret.SurfaceDark = darkColors.SurfaceDark;

                _ret.TextActive = darkColors.TextActive;
                _ret.TextInactive = darkColors.TextInactive;
                _ret.TextInAccent = GetReadableColor(_ret.Accent);

                _ret.Control = darkColors.Control;
                _ret.ControlDark = darkColors.ControlDark;
                _ret.ControlLight = darkColors.ControlLight;

                _ret.Primary = GetWindowsAccentColor();
                _ret.Secondary = Color.MediumSlateBlue;
            }
            else
            {
                // Light Mode - Using native Windows colors
                _ret.Background = SystemColors.Control;
                _ret.BackgroundDark = SystemColors.ControlDark;
                _ret.BackgroundLight = SystemColors.ControlLight;

                _ret.Surface = SystemColors.Window;
                _ret.SurfaceLight = SystemColors.ControlLightLight;
                _ret.SurfaceDark = SystemColors.ControlLight;

                _ret.TextActive = SystemColors.WindowText;
                _ret.TextInactive = SystemColors.GrayText;
                _ret.TextInAccent = SystemColors.HighlightText;

                _ret.Control = SystemColors.Window;
                _ret.ControlDark = SystemColors.ControlDark;
                _ret.ControlLight = SystemColors.ControlLight;

                _ret.Primary = SystemColors.Highlight;
                _ret.Secondary = SystemColors.MenuHighlight;
            }

            return _ret;
        }

        private static (Color Background, Color BackgroundDark, Color BackgroundLight,
                        Color Surface, Color SurfaceLight, Color SurfaceDark,
                        Color TextActive, Color TextInactive,
                        Color Control, Color ControlDark, Color ControlLight) GetWindowsDarkModeColors()
        {
            try
            {
                // Read dark mode colors from Registry
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\History\Colors"))
                {
                    if (key != null)
                    {
                        var immersiveStartBackground = key.GetValue("ImmersiveStartBackground");
                        var immersiveApplicationBackground = key.GetValue("ImmersiveApplicationBackground");

                        if (immersiveStartBackground != null && immersiveApplicationBackground != null)
                        {
                            Color bg = ParseColorFromRegistry(immersiveStartBackground);
                            Color appBg = ParseColorFromRegistry(immersiveApplicationBackground);

                            return (
                                Background: bg,
                                BackgroundDark: ControlPaint.Dark(bg),
                                BackgroundLight: ControlPaint.Light(bg),
                                Surface: appBg,
                                SurfaceLight: ControlPaint.Light(appBg),
                                SurfaceDark: ControlPaint.Dark(appBg),
                                TextActive: Color.White,
                                TextInactive: Color.FromArgb(176, 176, 176),
                                Control: ControlPaint.Light(appBg),
                                ControlDark: ControlPaint.Dark(appBg),
                                ControlLight: ControlPaint.LightLight(appBg)
                            );
                        }
                    }
                }
            }
            catch
            {
                // Fall through to defaults
            }

            // Fallback to hardcoded values
            return (
                Background: Color.FromArgb(32, 32, 32),
                BackgroundDark: Color.FromArgb(18, 18, 18),
                BackgroundLight: Color.FromArgb(45, 45, 45),
                Surface: Color.FromArgb(43, 43, 43),
                SurfaceLight: Color.FromArgb(50, 50, 50),
                SurfaceDark: Color.FromArgb(29, 29, 29),
                TextActive: Color.White,
                TextInactive: Color.FromArgb(176, 176, 176),
                Control: Color.FromArgb(55, 55, 55),
                ControlDark: Color.FromArgb(40, 40, 40),
                ControlLight: Color.FromArgb(67, 67, 67)
            );
        }

        private static Color ParseColorFromRegistry(object value)
        {
            if (value is int intValue)
            {
                byte a = (byte)((intValue >> 24) & 0xFF);
                byte r = (byte)((intValue >> 16) & 0xFF);
                byte g = (byte)((intValue >> 8) & 0xFF);
                byte b = (byte)(intValue & 0xFF);
                return Color.FromArgb(a, r, g, b);
            }
            return Color.FromArgb(32, 32, 32);
        }

        public static void SetRoundBorders(Control _Control, int Radius = 10, Color? borderColor = null, int borderSize = 2, bool underlinedStyle = false)
        {
            borderColor = borderColor ?? Color.MediumSlateBlue;

            if (_Control != null)
            {
                _Control.GetType().GetProperty("BorderStyle")?.SetValue(_Control, BorderStyle.None);
                _Control.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, _Control.Width, _Control.Height, Radius, Radius));
                _Control.Paint += (sender, e) =>
                {
                    Graphics graph = e.Graphics;

                    if (Radius > 1)
                    {
                        var rectBorderSmooth = _Control.ClientRectangle;
                        var rectBorder = Rectangle.Inflate(rectBorderSmooth, -borderSize, -borderSize);
                        int smoothSize = borderSize > 0 ? borderSize : 1;

                        using (GraphicsPath pathBorderSmooth = GetFigurePath(rectBorderSmooth, Radius))
                        using (GraphicsPath pathBorder = GetFigurePath(rectBorder, Radius - borderSize))
                        using (Pen penBorderSmooth = new Pen(_Control.Parent.BackColor, smoothSize))
                        using (Pen penBorder = new Pen((Color)borderColor, borderSize))
                        {
                            _Control.Region = new Region(pathBorderSmooth);
                            if (Radius > 15)
                            {
                                using (GraphicsPath pathTxt = GetFigurePath(_Control.ClientRectangle, borderSize * 2))
                                {
                                    _Control.Region = new Region(pathTxt);
                                }
                            }
                            graph.SmoothingMode = SmoothingMode.AntiAlias;
                            penBorder.Alignment = PenAlignment.Center;

                            if (underlinedStyle)
                            {
                                graph.DrawPath(penBorderSmooth, pathBorderSmooth);
                                graph.SmoothingMode = SmoothingMode.None;
                                graph.DrawLine(penBorder, 0, _Control.Height - 1, _Control.Width, _Control.Height - 1);
                            }
                            else
                            {
                                graph.DrawPath(penBorderSmooth, pathBorderSmooth);
                                graph.DrawPath(penBorder, pathBorder);
                            }
                        }
                    }
                };
            }
        }

        public static Bitmap ChangeToColor(Bitmap bmp, Color c)
        {
            Bitmap bmp2 = new Bitmap(bmp.Width, bmp.Height);
            using (Graphics g = Graphics.FromImage(bmp2))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;

                float tR = c.R / 255f;
                float tG = c.G / 255f;
                float tB = c.B / 255f;

                System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] { 1,    0,  0,  0,  0 },
                    new float[] { 0,    1,  0,  0,  0 },
                    new float[] { 0,    0,  1,  0,  0 },
                    new float[] { 0,    0,  0,  1,  0 },
                    new float[] { tR,   tG, tB, 0,  1 }
                });

                System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                  0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
            }
            return bmp2;
        }

        public static Image ChangeToColor(Image bmp, Color c) => ChangeToColor((Bitmap)bmp, c);

        #endregion Public Methods

        #region Private Methods

        private void Tsdd_Opening(object sender, CancelEventArgs e)
        {
            ToolStripDropDown tsdd = sender as ToolStripDropDown;
            if (tsdd == null) return;

            foreach (ToolStripMenuItem toolStripMenuItem in tsdd.Items.OfType<ToolStripMenuItem>())
            {
                toolStripMenuItem.DropDownOpening -= Tsmi_DropDownOpening;
                toolStripMenuItem.DropDownOpening += Tsmi_DropDownOpening;
            }
        }

        private void Tsmi_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem tsmi = sender as ToolStripMenuItem;
            if (tsmi == null) return;

            if (tsmi.DropDown.Items.Count > 0) ThemeControl(tsmi.DropDown);

            tsmi.DropDownOpening -= Tsmi_DropDownOpening;
        }

        private static void ApplySystemDarkTheme(Control control = null, bool IsDarkMode = true)
        {
            int[] DarkModeOn = IsDarkMode ? new[] { 0x01 } : new[] { 0x00 };
            string Mode = IsDarkMode ? "DarkMode_Explorer" : "ClearMode_Explorer";

            SetWindowTheme(control.Handle, Mode, null);

            if (DwmSetWindowAttribute(control.Handle, (int)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, DarkModeOn, 4) != 0)
                DwmSetWindowAttribute(control.Handle, (int)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, DarkModeOn, 4);

            foreach (Control child in control.Controls)
            {
                if (child.Controls.Count != 0)
                    ApplySystemDarkTheme(child, IsDarkMode);
            }
        }

        private static bool IsWindows10orGreater()
        {
            if (WindowsVersion() >= 10)
                return true;
            else
                return false;
        }

        private static int WindowsVersion()
        {
            int result;
            try
            {
                var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string[] productName = reg.GetValue("ProductName").ToString().Split((char)32);
                int.TryParse(productName[1], out result);
            }
            catch (Exception)
            {
                OperatingSystem os = Environment.OSVersion;
                result = os.Version.Major;
            }

            return result;
        }

        private static Color GetReadableColor(Color backgroundColor)
        {
            double normalizedR = backgroundColor.R / 255.0;
            double normalizedG = backgroundColor.G / 255.0;
            double normalizedB = backgroundColor.B / 255.0;
            double luminance = 0.299 * normalizedR + 0.587 * normalizedG + 0.114 * normalizedB;

            return luminance < 0.5 ? Color.FromArgb(182, 180, 215) : Color.FromArgb(34, 34, 34);
        }

        private static GraphicsPath GetFigurePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float curveSize = radius * 2F;

            path.StartFigure();
            path.AddArc(rect.X, rect.Y, curveSize, curveSize, 180, 90);
            path.AddArc(rect.Right - curveSize, rect.Y, curveSize, curveSize, 270, 90);
            path.AddArc(rect.Right - curveSize, rect.Bottom - curveSize, curveSize, curveSize, 0, 90);
            path.AddArc(rect.X, rect.Bottom - curveSize, curveSize, curveSize, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion Private Methods
    }

    public class OSThemeColors
    {
        public OSThemeColors()
        {
        }

        public Color Background { get; set; } = SystemColors.Control;
        public Color BackgroundDark { get; set; } = SystemColors.ControlDark;
        public Color BackgroundLight { get; set; } = SystemColors.ControlLight;
        public Color Surface { get; set; } = SystemColors.ControlLightLight;
        public Color SurfaceDark { get; set; } = SystemColors.ControlLight;
        public Color SurfaceLight { get; set; } = SystemColors.Control;
        public Color TextActive { get; set; } = SystemColors.ControlText;
        public Color TextInactive { get; set; } = SystemColors.GrayText;
        public Color TextInAccent { get; set; } = SystemColors.HighlightText;
        public Color Control { get; set; } = SystemColors.ButtonFace;
        public Color ControlDark { get; set; } = SystemColors.ButtonShadow;
        public Color ControlLight { get; set; } = SystemColors.ButtonHighlight;
        public Color Accent { get; set; } = DarkModeCS.GetWindowsAccentColor();
        public Color AccentOpaque { get; set; } = DarkModeCS.GetWindowsAccentOpaqueColor();
        public Color AccentDark { get { return ControlPaint.Dark(Accent); } }
        public Color AccentLight { get { return ControlPaint.Light(Accent); } }
        public Color Primary { get; set; } = SystemColors.Highlight;
        public Color PrimaryDark { get { return ControlPaint.Dark(Primary); } }
        public Color PrimaryLight { get { return ControlPaint.Light(Primary); } }
        public Color Secondary { get; set; } = SystemColors.HotTrack;
        public Color SecondaryDark { get { return ControlPaint.Dark(Secondary); } }
        public Color SecondaryLight { get { return ControlPaint.Light(Secondary); } }
    }

    public class MyRenderer : ToolStripProfessionalRenderer
    {
        public bool ColorizeIcons { get; set; } = true;
        public OSThemeColors MyColors { get; set; }

        public MyRenderer(ProfessionalColorTable table, bool pColorizeIcons = true) : base(table)
        {
            ColorizeIcons = pColorizeIcons;
        }

        private void DrawTitleBar(Graphics g, Rectangle rect)
        {
        }

        protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
        {
            DrawTitleBar(e.Graphics, new Rectangle(0, 0, e.ToolStrip.Width, 7));
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            DrawTitleBar(e.Graphics, new Rectangle(0, 0, e.ToolStrip.Width, 7));
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.ToolStrip.BackColor = MyColors.Background;
            base.OnRenderToolStripBackground(e);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);

            Color gradientBegin = MyColors.Background;
            Color gradientEnd = MyColors.Background;

            Pen BordersPencil = new Pen(MyColors.Background);

            ToolStripButton button = e.Item as ToolStripButton;
            if (button.Pressed || button.Checked)
            {
                gradientBegin = MyColors.Control;
                gradientEnd = MyColors.Control;
            }
            else if (button.Selected)
            {
                gradientBegin = MyColors.Accent;
                gradientEnd = MyColors.Accent;
            }

            using (Brush b = new LinearGradientBrush(bounds, gradientBegin, gradientEnd, LinearGradientMode.Vertical))
            {
                g.FillRectangle(b, bounds);
            }

            e.Graphics.DrawRectangle(BordersPencil, bounds);
            g.DrawLine(BordersPencil, bounds.X, bounds.Y, bounds.Width - 1, bounds.Y);
            g.DrawLine(BordersPencil, bounds.X, bounds.Y, bounds.X, bounds.Height - 1);

            ToolStrip toolStrip = button.Owner;

            if (!(button.Owner.GetItemAt(button.Bounds.X, button.Bounds.Bottom + 1) is ToolStripButton nextItem))
            {
                g.DrawLine(BordersPencil, bounds.X, bounds.Height - 1, bounds.X + bounds.Width - 1, bounds.Height - 1);
            }
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);
            Color gradientBegin = MyColors.Background;
            Color gradientEnd = MyColors.Background;

            Pen BordersPencil = new Pen(MyColors.Background);

            if (e.Item.Pressed)
            {
                gradientBegin = MyColors.Control;
                gradientEnd = MyColors.Control;
            }
            else if (e.Item.Selected)
            {
                gradientBegin = MyColors.Accent;
                gradientEnd = MyColors.Accent;
            }

            using (Brush b = new LinearGradientBrush(bounds, gradientBegin, gradientEnd, LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(b, bounds);
            }
        }

        protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);
            Color gradientBegin = MyColors.Background;
            Color gradientEnd = MyColors.Background;

            if (e.Item.Pressed)
            {
                gradientBegin = MyColors.Control;
                gradientEnd = MyColors.Control;
            }
            else if (e.Item.Selected)
            {
                gradientBegin = MyColors.Accent;
                gradientEnd = MyColors.Accent;
            }

            using (Brush b = new LinearGradientBrush(bounds, gradientBegin, gradientEnd, LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(b, bounds);
            }

            int Padding = 2;
            Size cSize = new Size(8, 4);
            Pen ChevronPen = new Pen(MyColors.TextInactive, 2);
            Point P1 = new Point(bounds.Width - (cSize.Width + Padding), (bounds.Height / 2) - (cSize.Height / 2));
            Point P2 = new Point(bounds.Width - Padding, (bounds.Height / 2) - (cSize.Height / 2));
            Point P3 = new Point(bounds.Width - (cSize.Width / 2 + Padding), (bounds.Height / 2) + (cSize.Height / 2));

            e.Graphics.DrawLine(ChevronPen, P1, P3);
            e.Graphics.DrawLine(ChevronPen, P2, P3);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Enabled)
            {
                e.TextColor = MyColors.TextActive;
            }
            else
            {
                e.TextColor = MyColors.TextInactive;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);

            if (e.Item is ToolStripMenuItem menuItem && menuItem.Checked)
            {
                using (Brush b = new SolidBrush(menuItem.BackColor))
                    g.FillRectangle(b, bounds);
                return;
            }

            Color gradientBegin = MyColors.Background;
            Color gradientEnd = MyColors.Background;

            bool DrawIt = false;
            var _menu = e.Item;
            if (_menu.Pressed)
            {
                gradientBegin = MyColors.Control;
                gradientEnd = MyColors.Control;
                DrawIt = true;
            }
            else if (_menu.Selected)
            {
                gradientBegin = MyColors.Accent;
                gradientEnd = MyColors.Accent;
                DrawIt = true;
            }

            if (DrawIt)
            {
                using (Brush b = new LinearGradientBrush(bounds, gradientBegin, gradientEnd, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(b, bounds);
                }
            }
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item.GetType().FullName == "System.Windows.Forms.MdiControlStrip+ControlBoxMenuItem")
            {
                Image image = e.Image;
                Color _ClearColor = e.Item.Enabled ? MyColors.TextActive : MyColors.SurfaceDark;

                using (Image adjustedImage = DarkModeCS.ChangeToColor(image, _ClearColor))
                {
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    e.Graphics.CompositingQuality = CompositingQuality.AssumeLinear;
                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.DrawImage(adjustedImage, e.ImageRectangle);
                }

                return;
            }

            if (ColorizeIcons && e.Image != null)
            {
                Image image = e.Image;
                Color _ClearColor = e.Item.Enabled ? MyColors.TextInactive : MyColors.SurfaceDark;

                using (Image adjustedImage = DarkModeCS.ChangeToColor(image, _ClearColor))
                {
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.DrawImage(adjustedImage, e.ImageRectangle);
                }
            }
            else
            {
                base.OnRenderItemImage(e);
            }
        }
    }

    public class CustomColorTable : ProfessionalColorTable
    {
        public OSThemeColors Colors { get; set; }

        public CustomColorTable(OSThemeColors _Colors)
        {
            Colors = _Colors;
            UseSystemColors = false;
        }

        public override Color ImageMarginGradientBegin
        {
            get { return Colors.Control; }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return Colors.Control; }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return Colors.Control; }
        }
    }

    public class ControlStatusStorage
    {
        private readonly ConditionalWeakTable<Control, ControlStatusInfo> _controlsProcessed = new ConditionalWeakTable<Control, ControlStatusInfo>();

        public void ExcludeFromProcessing(Control control)
        {
            _controlsProcessed.Remove(control);
            _controlsProcessed.Add(control, new ControlStatusInfo() { IsExcluded = true });
        }

        public ControlStatusInfo GetControlStatusInfo(Control control)
        {
            _controlsProcessed.TryGetValue(control, out ControlStatusInfo info);
            return info;
        }

        public void RegisterProcessedControl(Control control, bool isDarkMode)
        {
            _controlsProcessed.Add(control, new ControlStatusInfo() { IsExcluded = false, LastThemeAppliedIsDark = isDarkMode });
        }
    }

    public class ControlStatusInfo
    {
        public bool IsExcluded { get; set; }
        public bool LastThemeAppliedIsDark { get; set; }
    }
}