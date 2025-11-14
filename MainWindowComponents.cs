using DarkModeForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private ToolStripMenuItem exitToolStripMenuItem = null!;
        private ToolStripMenuItem darkModeToolStripMenuItem = null!;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        public void InitializeComponents()
        {
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
            fileToolStripMenuItem.DropDownItems.Add(exitToolStripMenuItem);

            darkModeToolStripMenuItem = new ToolStripMenuItem();
            darkModeToolStripMenuItem.Text = "&Dark Mode";
            darkModeToolStripMenuItem.CheckOnClick = true;
            darkModeToolStripMenuItem.Alignment = ToolStripItemAlignment.Right;
            darkModeToolStripMenuItem.CheckedChanged += DarkModeToolStripMenuItem_CheckedChanged;

            fileMenuStrip.Items.Add(fileToolStripMenuItem);

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
    }
}
