using DarkModeForms;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HBPakEditor
{
    public partial class MainWindow : Form
    {
        private DarkModeCS darkMode = null!;
        private RenamableTabControl<PAKTabPage> pakTabControl = null!;
        private MenuStrip fileMenuStrip = null!;
        private StatusStrip statusStrip = null!;
        private ToolStripMenuItem fileToolStripMenuItem = null!;
            private ToolStripMenuItem newToolStripMenuItem = null!;
            private ToolStripMenuItem openToolStripMenuItem = null!;
            private ToolStripMenuItem saveToolStripMenuItem = null!;
            private ToolStripMenuItem saveAsToolStripMenuItem = null!;
            private ToolStripMenuItem saveAllToolStripMenuItem = null!;
            private ToolStripMenuItem exportAllSpritesToolStripMenuItem = null!;
            private ToolStripMenuItem importAllSpritesToolStripMenuItem = null!;
            private ToolStripMenuItem compactAllSpritesToolStripMenuItem = null!;
            private ToolStripMenuItem exitToolStripMenuItem = null!;
            private ToolStripMenuItem darkModeToolStripMenuItem = null!;
        private ToolStripMenuItem editToolStripMenuItem = null!;
            private ToolStripMenuItem undoToolStripMenuItem = null!;
            private ToolStripMenuItem redoToolStripMenuItem = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IconFactory.DisposeIcons();
            }
            base.Dispose(disposing);
        }

        public void InitializeComponents()
        {
            IconFactory.Initialize(Color.White, Color.Gray, 128, 64f);
            this.SuspendLayout();
            this.ClientSize = new Size(1024, 768);
            this.MinimumSize = new Size(1024, 768);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Name = "MainWindow";
            this.Text = "Helbreath PAK Editor";
            this.Icon = GetEmbeddedResourceIcon("PAKIcon.ico");

            fileMenuStrip = new MenuStrip();
            this.MainMenuStrip = fileMenuStrip;

            statusStrip = new StatusStrip();
            statusStrip.Name = "statusStrip";

            fileToolStripMenuItem = new ToolStripMenuItem();
            fileToolStripMenuItem.Text = "&File";

            newToolStripMenuItem = new ToolStripMenuItem();
            newToolStripMenuItem.Text = "&New";
            newToolStripMenuItem.Click += NewToolStripMenuItem_Click;

            openToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem.Text = "&Open";
            openToolStripMenuItem.Click += OpenToolStripMenuItem_Click;

            saveToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem.Text = "&Save";
            saveToolStripMenuItem.Click += SaveToolStripMenuItem_Click;
            saveToolStripMenuItem.Enabled = false;

            saveAsToolStripMenuItem = new ToolStripMenuItem();
            saveAsToolStripMenuItem.Text = "Save &As";
            saveAsToolStripMenuItem.Click += SaveAsToolStripMenuItem_Click;
            saveAsToolStripMenuItem.Enabled = false;

            saveAllToolStripMenuItem = new ToolStripMenuItem();
            saveAllToolStripMenuItem.Text = "Save A&ll";
            saveAllToolStripMenuItem.Click += SaveAllToolStripMenuItem_Click;
            saveAllToolStripMenuItem.Enabled = false;

            exportAllSpritesToolStripMenuItem = new ToolStripMenuItem();
            exportAllSpritesToolStripMenuItem.Text = "&Export All Sprites";
            exportAllSpritesToolStripMenuItem.Click += ExportAllSpritesToolStripMenuItem_Click;
            exportAllSpritesToolStripMenuItem.Enabled = false;

            importAllSpritesToolStripMenuItem = new ToolStripMenuItem();
            importAllSpritesToolStripMenuItem.Text = "&Import All Sprites";
            importAllSpritesToolStripMenuItem.Click += ImportAllSpritesToolStripMenuItem_Click;
            importAllSpritesToolStripMenuItem.Enabled = false;

            compactAllSpritesToolStripMenuItem = new ToolStripMenuItem();
            compactAllSpritesToolStripMenuItem.Text = "&Compact All Sprites";
            compactAllSpritesToolStripMenuItem.Click += CompactAllSpritesToolStripMenuItem_Click;
            compactAllSpritesToolStripMenuItem.Enabled = false;

            exitToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem.Text = "E&xit";
            exitToolStripMenuItem.Click += ExitToolStripMenuItem_Click;

            fileToolStripMenuItem.DropDownItems.Add(newToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(openToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            fileToolStripMenuItem.DropDownItems.Add(saveToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(saveAsToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(saveAllToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            fileToolStripMenuItem.DropDownItems.Add(exportAllSpritesToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(importAllSpritesToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            fileToolStripMenuItem.DropDownItems.Add(compactAllSpritesToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            fileToolStripMenuItem.DropDownItems.Add(exitToolStripMenuItem);

            editToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem.Text = "&Edit";
            editToolStripMenuItem.DropDownOpening += EditToolStripMenuItem_DropDownOpening;

            undoToolStripMenuItem = new ToolStripMenuItem();
            undoToolStripMenuItem.Text = "&Undo";
            undoToolStripMenuItem.Click += UndoToolStripMenuItem_Click;

            redoToolStripMenuItem = new ToolStripMenuItem();
            redoToolStripMenuItem.Text = "&Redo";
            redoToolStripMenuItem.Click += RedoToolStripMenuItem_Click;

            newToolStripMenuItem.Image = IconFactory.GetIcon(IconType.New);
            openToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Open);

            // Disabled icons for save-related actions
            saveToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Save);
            saveAsToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.SaveAs);
            saveAllToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.SaveAll);
            exportAllSpritesToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Export);
            importAllSpritesToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Import);
            compactAllSpritesToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Compress);

            exitToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Exit);

            undoToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Undo);
            redoToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Redo);

            editToolStripMenuItem.DropDownItems.Add(undoToolStripMenuItem);
            editToolStripMenuItem.DropDownItems.Add(redoToolStripMenuItem);

            darkModeToolStripMenuItem = new ToolStripMenuItem();
            darkModeToolStripMenuItem.Text = "&Dark Mode";
            darkModeToolStripMenuItem.CheckOnClick = true;
            darkModeToolStripMenuItem.Alignment = ToolStripItemAlignment.Right;
            darkModeToolStripMenuItem.CheckedChanged += DarkModeToolStripMenuItem_CheckedChanged;

            fileMenuStrip.Items.Add(fileToolStripMenuItem);
            fileMenuStrip.Items.Add(editToolStripMenuItem);

            fileMenuStrip.Items.Add(darkModeToolStripMenuItem);

            this.pakTabControl = new RenamableTabControl<PAKTabPage>();
            this.pakTabControl.Dock = DockStyle.Fill;
            this.pakTabControl.Name = "pakTabControl";
            this.pakTabControl.TabPages.Add(new PAKTabEmpty(this));
            this.pakTabControl.Enabled = false;
            this.pakTabControl.BeforeRename = OnBeforeRename_Tab;
            this.pakTabControl.AfterRename = OnAfterRename_Tab;
            this.pakTabControl.BeforeClose = OnBeforeClose_Tab;
            this.pakTabControl.AfterClose = OnAfterClose_Tab;
            this.pakTabControl.BeforeContextMenuShown = OnBeforeContextMenuShown_Tab;

            this.Controls.Add(this.pakTabControl);
            this.Controls.Add(fileMenuStrip);
            this.Controls.Add(statusStrip);
            this.ResumeLayout(false);

            darkMode = new DarkModeCS(this);
            darkMode.ColorMode = DarkModeCS.DisplayMode.SystemDefault;
            if(darkMode.isDarkMode())
            {
                darkModeToolStripMenuItem.Checked = true;
            }
        }

        private void DarkModeToolStripMenuItem_CheckedChanged(object? sender, EventArgs e)
        {
            darkModeToolStripMenuItem.BackColor = darkModeToolStripMenuItem.Checked ? SystemColors.Highlight : SystemColors.Control;
            darkModeToolStripMenuItem.ForeColor = darkModeToolStripMenuItem.Checked ? SystemColors.HighlightText : SystemColors.ControlText;
            darkModeToolStripMenuItem.Text = darkModeToolStripMenuItem.Checked ? "&Light Mode" : "&Dark Mode";
            darkMode.ApplyTheme(darkModeToolStripMenuItem.Checked);
        }

        public void SetStatusLabel(string text)
        {
            statusStrip.Items.Clear();
            statusStrip.Items.Add(text);
        }

        public static Stream? GetEmbeddedResource(string filePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName().Name;

            // Convert file path to resource name format
            // Replace directory separators with dots and prepend namespace
            string resourceName = $"{assemblyName}.{filePath.Replace('\\', '.').Replace('/', '.')}";

            return assembly.GetManifestResourceStream(resourceName);
        }

        public static byte[]? GetEmbeddedResourceBytes(string filePath)
        {
            using var stream = GetEmbeddedResource(filePath);
            if (stream == null) return null;

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        public static string? GetEmbeddedResourceString(string filePath)
        {
            using var stream = GetEmbeddedResource(filePath);
            if (stream == null) return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static Image? GetEmbeddedResourceImage(string filePath)
        {
            using var stream = GetEmbeddedResource(filePath);
            if (stream == null) return null;

            return Image.FromStream(stream);
        }

        public static Icon? GetEmbeddedResourceIcon(string filePath)
        {
            using var stream = GetEmbeddedResource(filePath);
            if (stream == null) return null;
            return new Icon(stream);
        }


        // instance handlers
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_COPYDATA = 0x004A;
            if (m.Msg == WM_COPYDATA)
            {
                COPYDATASTRUCT cds = Marshal.PtrToStructure<COPYDATASTRUCT>(m.LParam);
                byte[] data = new byte[cds.cbData];
                Marshal.Copy(cds.lpData, data, 0, cds.cbData);
                string message = Encoding.UTF8.GetString(data);
                string[] files = message.Split('|');

                Program.ProcessArgs(this, files);
            }
            base.WndProc(ref m);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }
    }
}

public enum IconType
{
    // File operations
    New,
    Open,
    Save,
    SaveAs,
    SaveAll,
    Export,
    Import,
    Close,
    Exit,

    // Edit operations
    Undo,
    Redo,
    Cut,
    Copy,
    Paste,
    Delete,

    // View operations
    ZoomIn,
    ZoomOut,
    Refresh,
    Search,
    Fullscreen,

    // Common actions
    Add,
    Remove,
    Edit,
    Settings,
    Help,
    Info,
    Warning,
    Error,

    // Navigation
    Up,
    Down,
    Left,
    Right,
    Home,
    Back,
    Forward,

    // Media
    Play,
    Pause,
    Stop,

    // Misc
    Folder,
    File,
    Image,
    Compress,
    Expand,
    Collapse,
    Lock,
    Unlock,
    Pin,
    Favorite,
    Check,
    Cancel
}

public static class IconFactory
{
    private static Dictionary<IconType, Image>? _icons;
    private static Dictionary<IconType, Image>? _iconsDisabled;
    private static readonly object _lock = new();

    private static readonly Dictionary<IconType, string> _glyphMap = new()
    {
        // File operations
        [IconType.New] = "\uE8A5",
        [IconType.Open] = "\uE838",
        [IconType.Save] = "\uE74E",
        [IconType.SaveAs] = "\uE792",
        [IconType.SaveAll] = "\uE78C",
        [IconType.Export] = "\uE898",
        [IconType.Import] = "\uE896",
        [IconType.Close] = "\uE8BB",
        [IconType.Exit] = "\uE7E8",

        // Edit operations
        [IconType.Undo] = "\uE7A7",
        [IconType.Redo] = "\uE7A6",
        [IconType.Cut] = "\uE8C6",
        [IconType.Copy] = "\uE8C8",
        [IconType.Paste] = "\uE77F",
        [IconType.Delete] = "\uE74D",

        // View operations
        [IconType.ZoomIn] = "\uE8A3",
        [IconType.ZoomOut] = "\uE71F",
        [IconType.Refresh] = "\uE72C",
        [IconType.Search] = "\uE721",
        [IconType.Fullscreen] = "\uE740",

        // Common actions
        [IconType.Add] = "\uE710",
        [IconType.Remove] = "\uE738",
        [IconType.Edit] = "\uE70F",
        [IconType.Settings] = "\uE713",
        [IconType.Help] = "\uE897",
        [IconType.Info] = "\uE946",
        [IconType.Warning] = "\uE7BA",
        [IconType.Error] = "\uE783",

        // Navigation
        [IconType.Up] = "\uE74A",
        [IconType.Down] = "\uE74B",
        [IconType.Left] = "\uE74E",
        [IconType.Right] = "\uE74F",
        [IconType.Home] = "\uE80F",
        [IconType.Back] = "\uE72B",
        [IconType.Forward] = "\uE72A",

        // Media
        [IconType.Play] = "\uE768",
        [IconType.Pause] = "\uE769",
        [IconType.Stop] = "\uE71A",

        // Misc
        [IconType.Folder] = "\uE8B7",
        [IconType.File] = "\uE8A5",
        [IconType.Image] = "\uE8B9",
        [IconType.Compress] = "\uE94D",
        [IconType.Expand] = "\uE8A0",
        [IconType.Collapse] = "\uE89F",
        [IconType.Lock] = "\uE72E",
        [IconType.Unlock] = "\uE785",
        [IconType.Pin] = "\uE718",
        [IconType.Favorite] = "\uE734",
        [IconType.Check] = "\uE73E",
        [IconType.Cancel] = "\uE711"
    };

    public static Image GetIcon(IconType type)
    {
        EnsureInitialized();
        return _icons![type];
    }

    public static Image GetIconDisabled(IconType type)
    {
        EnsureInitialized();
        return _iconsDisabled![type];
    }

    public static void Initialize(Color color, Color disabledColor, int size = 16, float fontSize = 10f)
    {
        lock (_lock)
        {
            DisposeIcons();

            _icons = new Dictionary<IconType, Image>();
            _iconsDisabled = new Dictionary<IconType, Image>();

            using var font = new Font("Segoe MDL2 Assets", fontSize);

            foreach (var kvp in _glyphMap)
            {
                _icons[kvp.Key] = CreateIconFromText(kvp.Value, font, color, size);
                _iconsDisabled[kvp.Key] = CreateIconFromText(kvp.Value, font, disabledColor, size);
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (_icons == null)
        {
            Initialize(Color.White, Color.Gray);
        }
    }

    private static Image CreateIconFromText(string text, Font font, Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            var textSize = g.MeasureString(text, font);
            float x = (size - textSize.Width) / 2;
            float y = (size - textSize.Height) / 2;

            using (var brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, x, y);
            }
        }
        return bitmap;
    }

    public static void DisposeIcons()
    {
        if (_icons != null)
        {
            foreach (var img in _icons.Values)
                img?.Dispose();
            _icons = null;
        }

        if (_iconsDisabled != null)
        {
            foreach (var img in _iconsDisabled.Values)
                img?.Dispose();
            _iconsDisabled = null;
        }
    }
}