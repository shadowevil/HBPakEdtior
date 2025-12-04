using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.RegularExpressions;
using PAKLib;
using HBPakEditor; // For RenderedPanel

namespace SpritePacker
{
    public class SpriteRectangle
    {
        public int x { get; set; }
        public int y { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int pivotX { get; set; }
        public int pivotY { get; set; }
    }

    public class SpriteSheet
    {
        public string ImagePath { get; set; }
        public string JsonPath { get; set; }
        public Bitmap Image { get; set; }
        public List<SpriteRectangle> Rectangles { get; set; }
        public string Prefix { get; set; }
        public bool IsBmp { get; set; }
    }

    public class PackedSheet
    {
        public Bitmap Image { get; set; }
        public List<SpriteRectangle> Rectangles { get; set; }
        public int SpriteCount { get; set; }
    }

    public enum SourceMode
    {
        Directory,
        PAK
    }

    public enum PackingMethod
    {
        RowAligned,
        TightCompact
    }

    public enum OutputFormat
    {
        PNG,
        BMP
    }

    public class SpritePackerForm : Form
    {
        // Packing parameters
        private int maxWidth = 2000;
        private int maxHeight = 5000;
        private int spacing = 4;
        private bool useGridMode = false;
        private int gridColumns = 8;
        private int gridRows = 100;

        // Output format
        private OutputFormat outputFormat = OutputFormat.PNG;
        private static readonly Color BmpBackgroundColor = Color.FromArgb(255, 0, 255); // Bright pink

        // UI Controls
        private MenuStrip menuStrip;
        private TabControl tabControl;
        private Panel bottomPanel;
        private Panel settingsPanel;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel zoomLabel;


        // Settings controls
        private NumericUpDown maxWidthInput;
        private NumericUpDown maxHeightInput;
        private NumericUpDown spacingInput;
        private CheckBox gridModeCheckbox;
        private Label maxWidthLabel;
        private Label maxHeightLabel;
        private RadioButton rowAlignedRadio;
        private RadioButton tightCompactRadio;
        private GroupBox packingMethodGroup;

        // Action buttons
        private Button repackButton;
        private Button primaryButton;   // "Load Directory" or "Compact"
        private Button secondaryButton; // "Export to Disk" or "Discard"
        private Button tertiaryButton;  // "Import to PAK" allows importing directory sprites to open PAK file.

        // Data
        private string sourceDirectory;
        private string targetDirectory;
        public string OutputDirectory => targetDirectory;
        private List<SpriteSheet> loadedSheets;
        private List<PackedSheet> packedSheets;
        private List<Bitmap> pakBitmaps;
        private string outputPrefix;

        // Mode
        private SourceMode sourceMode = SourceMode.Directory;
        private PackingMethod packingMethod = PackingMethod.RowAligned;
        private PAK sourcePAK;

        public SpritePackerForm() : this(SourceMode.Directory, null, "packed")
        {
        }

        public SpritePackerForm(PAK pak, string prefix = "packed") : this(SourceMode.PAK, pak, prefix)
        {
        }

        private SpritePackerForm(SourceMode mode, PAK? pak, string prefix)
        {
            loadedSheets = new List<SpriteSheet>();
            packedSheets = new List<PackedSheet>();
            pakBitmaps = new List<Bitmap>();

            InitializeForm(mode);

            if (mode == SourceMode.PAK && pak != null)
            {
                LoadFromPAK(pak, prefix);
            }
        }

        private void InitializeForm(SourceMode mode)
        {
            sourceMode = mode;

            Text = mode == SourceMode.PAK ? "Sprite Compactor" : "Sprite Sheet Packer";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(800, 600);

            // Menu Strip
            menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");

            if (mode == SourceMode.Directory)
            {
                var loadItem = new ToolStripMenuItem("Load Directory...", null, (s, e) => LoadDirectory());
                loadItem.ShortcutKeys = Keys.Control | Keys.O;
                fileMenu.DropDownItems.Add(loadItem);

                var exportItem = new ToolStripMenuItem("Export to Disk...", null, (s, e) => ExportToDisk());
                exportItem.ShortcutKeys = Keys.Control | Keys.S;
                fileMenu.DropDownItems.Add(exportItem);
            }
            else
            {
                var exportItem = new ToolStripMenuItem("Export to Disk...", null, (s, e) => ExportToDisk());
                exportItem.ShortcutKeys = Keys.Control | Keys.S;
                fileMenu.DropDownItems.Add(exportItem);
            }

            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            var closeItem = new ToolStripMenuItem("Close", null, (s, e) => Close());
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    Close();
            };
            fileMenu.DropDownItems.Add(closeItem);

            menuStrip.Items.Add(fileMenu);
            MainMenuStrip = menuStrip;

            // Status Strip
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            zoomLabel = new ToolStripStatusLabel("100%") { Width = 60, TextAlign = ContentAlignment.MiddleRight };
            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(new ToolStripSeparator());
            statusStrip.Items.Add(zoomLabel);

            // Settings Panel (top)
            settingsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                Padding = new Padding(5)
            };

            var settingsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 9,
                RowCount = 1,
                AutoSize = false
            };

            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));  // Packing group
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Grid checkbox
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Max W label
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Max W input
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Max H label
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Max H input
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Spacing label
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Spacing input
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Repack (fill remaining, anchor right)

            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Packing method GroupBox
            packingMethodGroup = new GroupBox
            {
                Text = "Packing",
                Dock = DockStyle.Fill
            };

            rowAlignedRadio = new RadioButton
            {
                Text = "Row",
                AutoSize = true,
                Checked = true,
                Location = new Point(8, 15)
            };
            rowAlignedRadio.CheckedChanged += PackingMethod_CheckedChanged;

            tightCompactRadio = new RadioButton
            {
                Text = "Tight",
                AutoSize = true,
                Location = new Point(rowAlignedRadio.Width, 15)
            };
            tightCompactRadio.CheckedChanged += PackingMethod_CheckedChanged;

            var groupboxHeight = packingMethodGroup.Height;
            var averageHeightOfRadioButton = (rowAlignedRadio.Height + tightCompactRadio.Height);
            var centerInGroupbox = ((groupboxHeight - averageHeightOfRadioButton) / 2) - 7;

            rowAlignedRadio.Location = new Point(8, centerInGroupbox);
            tightCompactRadio.Location = new Point(rowAlignedRadio.Width, centerInGroupbox);

            packingMethodGroup.Controls.Add(rowAlignedRadio);
            packingMethodGroup.Controls.Add(tightCompactRadio);

            gridModeCheckbox = new CheckBox
            {
                Text = "Grid",
                AutoSize = true,
                Anchor = AnchorStyles.None
            };
            gridModeCheckbox.CheckedChanged += GridModeCheckbox_CheckedChanged;

            maxWidthLabel = new Label
            {
                Text = "Max Width:",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleRight
            };

            maxWidthInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 10000,
                Value = maxWidth,
            };

            maxHeightLabel = new Label
            {
                Text = "Max Height:",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleRight,
            };

            maxHeightInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 10000,
                Value = maxHeight,
                Anchor = AnchorStyles.None
            };

            var spacingLabel = new Label
            {
                Text = "Spacing:",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleRight
            };

            spacingInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = spacing,
                Anchor = AnchorStyles.None
            };

            repackButton = new Button
            {
                Text = "Repack",
                Size = new Size(75, 32),
                Anchor = AnchorStyles.Right,
                Enabled = false
            };
            repackButton.Click += (s, e) => RepackSprites();

            int offset = centerInGroupbox - 1;

            foreach (Control c in new Control[] {
                gridModeCheckbox, maxWidthLabel, maxHeightLabel, spacingLabel,
                maxWidthInput, maxHeightInput, spacingInput, repackButton
            })
            {
                var m = c.Margin;
                c.Margin = new Padding(m.Left, m.Top + offset, m.Right, m.Bottom);
            }

            settingsLayout.Controls.Add(packingMethodGroup, 0, 0);
            settingsLayout.Controls.Add(gridModeCheckbox, 1, 0);
            settingsLayout.Controls.Add(maxWidthLabel, 2, 0);
            settingsLayout.Controls.Add(maxWidthInput, 3, 0);
            settingsLayout.Controls.Add(maxHeightLabel, 4, 0);
            settingsLayout.Controls.Add(maxHeightInput, 5, 0);
            settingsLayout.Controls.Add(spacingLabel, 6, 0);
            settingsLayout.Controls.Add(spacingInput, 7, 0);
            settingsLayout.Controls.Add(repackButton, 8, 0);

            settingsPanel.Controls.Add(settingsLayout);

            // Bottom Panel (action buttons)
            bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            if (mode == SourceMode.Directory)
            {
                primaryButton = new Button
                {
                    Text = "Load Directory",
                    Size = new Size(120, 30),
                    Anchor = AnchorStyles.Left
                };
                primaryButton.Click += (s, e) => LoadDirectory();

                secondaryButton = new Button
                {
                    Text = "Export to Disk",
                    Size = new Size(120, 30),
                    Anchor = AnchorStyles.Right,
                    Enabled = false
                };
                secondaryButton.Click += (s, e) => ExportToDisk();

                tertiaryButton = new Button
                {
                    Text = "Import to PAK",
                    Size = new Size(120, 30),
                    Anchor = AnchorStyles.Right,
                    Enabled = false
                };
                tertiaryButton.Click += (s, e) =>
                {
                    DialogResult = DialogResult.Yes;
                    Close();
                };
            }
            else
            {
                primaryButton = new Button
                {
                    Text = "Compact",
                    Size = new Size(120, 30),
                    Anchor = AnchorStyles.Right,
                    Enabled = false
                };
                primaryButton.Click += (s, e) =>
                {
                    DialogResult = DialogResult.OK;
                    Close();
                };

                secondaryButton = new Button
                {
                    Text = "Discard",
                    Size = new Size(120, 30),
                    Anchor = AnchorStyles.Right
                };
                secondaryButton.Click += (s, e) =>
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                };
            }

            // Position buttons
            bottomPanel.Resize += (s, e) =>
            {
                if (mode == SourceMode.Directory)
                {
                    primaryButton.Location = new Point(10, 10);
                    secondaryButton.Location = new Point(bottomPanel.Width - secondaryButton.Width - 10, 10);
                    tertiaryButton.Location = new Point(secondaryButton.Left - tertiaryButton.Width - 10, 10);
                }
                else
                {
                    secondaryButton.Location = new Point(bottomPanel.Width - secondaryButton.Width - 10, 10);
                    primaryButton.Location = new Point(secondaryButton.Left - primaryButton.Width - 10, 10);
                }
            };

            bottomPanel.Controls.Add(primaryButton);
            bottomPanel.Controls.Add(secondaryButton);
            if (mode == SourceMode.Directory)
                bottomPanel.Controls.Add(tertiaryButton);

            // Tab Control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Add controls in order
            Controls.Add(tabControl);
            Controls.Add(settingsPanel);
            Controls.Add(bottomPanel);
            Controls.Add(statusStrip);
            Controls.Add(menuStrip);
        }

        private void PackingMethod_CheckedChanged(object? sender, EventArgs e)
        {
            if (rowAlignedRadio.Checked)
                packingMethod = PackingMethod.RowAligned;
            else if (tightCompactRadio.Checked)
                packingMethod = PackingMethod.TightCompact;
        }

        public void LoadFromPAK(PAK pak, string prefix = "packed")
        {
            if (pak?.Data == null || pak.Data.Sprites.Count == 0)
            {
                statusLabel.Text = "PAK contains no sprites";
                return;
            }

            ClearAll();

            sourceMode = SourceMode.PAK;
            sourcePAK = pak;
            outputPrefix = prefix;

            // Detect format from PAK sprites
            int bmpCount = 0;
            int pngCount = 0;

            foreach (var sprite in pak.Data.Sprites)
            {
                if (IsBmpData(sprite.data))
                    bmpCount++;
                else
                    pngCount++;
            }

            // Determine output format
            if (bmpCount > 0 && pngCount == 0)
                outputFormat = OutputFormat.BMP;
            else
                outputFormat = OutputFormat.PNG;

            foreach (var sprite in pak.Data.Sprites)
            {
                using var ms = new MemoryStream(sprite.data);
                var bitmap = new Bitmap(ms);
                pakBitmaps.Add(bitmap);

                var rects = sprite.Rectangles.Select(r => new SpriteRectangle
                {
                    x = r.x,
                    y = r.y,
                    width = r.width,
                    height = r.height,
                    pivotX = r.pivotX,
                    pivotY = r.pivotY
                }).ToList();

                loadedSheets.Add(new SpriteSheet
                {
                    Image = bitmap,
                    Rectangles = rects,
                    Prefix = prefix,
                    IsBmp = IsBmpData(sprite.data)
                });
            }

            UpdatePackingParameters();
            PackSpritesFromLoaded();

            string formatStr = outputFormat == OutputFormat.BMP ? "BMP" : "PNG";
            statusLabel.Text = $"Loaded {pak.Data.Sprites.Count} sprites from PAK (Output: {formatStr})";
        }

        private static bool IsBmpData(byte[] data)
        {
            if (data == null || data.Length < 2)
                return false;
            return data[0] == 0x42 && data[1] == 0x4D; // "BM" signature
        }

        #region Public Accessors

        public PAK GetPAK()
        {
            var pak = new PAK { Data = new PAKData() };

            foreach (var packed in packedSheets)
            {
                using var ms = new MemoryStream();

                if (outputFormat == OutputFormat.BMP)
                    packed.Image.Save(ms, ImageFormat.Bmp);
                else
                    packed.Image.Save(ms, ImageFormat.Png);

                var sprite = new Sprite
                {
                    data = ms.ToArray(),
                    Rectangles = packed.Rectangles.Select(r => new PAKLib.SpriteRectangle
                    {
                        x = (short)r.x,
                        y = (short)r.y,
                        width = (short)r.width,
                        height = (short)r.height,
                        pivotX = (short)r.pivotX,
                        pivotY = (short)r.pivotY
                    }).ToList()
                };

                pak.Data.Sprites.Add(sprite);
            }

            return pak;
        }

        public List<Sprite> GetSprites()
        {
            return packedSheets.Select(packed =>
            {
                using var ms = new MemoryStream();

                if (outputFormat == OutputFormat.BMP)
                    packed.Image.Save(ms, ImageFormat.Bmp);
                else
                    packed.Image.Save(ms, ImageFormat.Png);

                return new Sprite
                {
                    data = ms.ToArray(),
                    Rectangles = packed.Rectangles.Select(r => new PAKLib.SpriteRectangle
                    {
                        x = (short)r.x,
                        y = (short)r.y,
                        width = (short)r.width,
                        height = (short)r.height,
                        pivotX = (short)r.pivotX,
                        pivotY = (short)r.pivotY
                    }).ToList()
                };
            }).ToList();
        }

        public List<List<PAKLib.SpriteRectangle>> GetRectangles()
        {
            return packedSheets.Select(packed =>
                packed.Rectangles.Select(r => new PAKLib.SpriteRectangle
                {
                    x = (short)r.x,
                    y = (short)r.y,
                    width = (short)r.width,
                    height = (short)r.height,
                    pivotX = (short)r.pivotX,
                    pivotY = (short)r.pivotY
                }).ToList()
            ).ToList();
        }

        public List<PackedSheet> GetPackedSheets() => packedSheets;

        public OutputFormat GetOutputFormat() => outputFormat;

        #endregion

        #region UI Event Handlers

        private void GridModeCheckbox_CheckedChanged(object? sender, EventArgs e)
        {
            useGridMode = gridModeCheckbox.Checked;

            if (useGridMode)
            {
                maxWidthLabel.Text = "Columns:";
                maxHeightLabel.Text = "Rows:";
                maxWidthInput.Maximum = 100;
                maxWidthInput.Value = Math.Min(maxWidthInput.Value, 100);
                maxHeightInput.Maximum = 1000;
                maxHeightInput.Value = Math.Min(maxHeightInput.Value, 1000);
            }
            else
            {
                maxWidthLabel.Text = "Max Width:";
                maxHeightLabel.Text = "Max Height:";
                maxWidthInput.Maximum = 10000;
                maxWidthInput.Value = 2000;
                maxHeightInput.Maximum = 10000;
                maxHeightInput.Value = 5000;
            }
        }

        private void LoadDirectory()
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                ClearAll();
                sourceDirectory = dialog.SelectedPath;
                sourceMode = SourceMode.Directory;
                LoadAndPackSprites();
            }
        }

        private void RepackSprites()
        {
            if (loadedSheets.Count == 0)
                return;

            UpdatePackingParameters();
            PackSpritesFromLoaded();
        }

        private void ExportToDisk()
        {
            if (packedSheets.Count == 0)
                return;

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select output folder for packed sprites"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                string outputDir = dialog.SelectedPath;
                string ext = outputFormat == OutputFormat.BMP ? "bmp" : "png";
                ImageFormat imgFormat = outputFormat == OutputFormat.BMP ? ImageFormat.Bmp : ImageFormat.Png;

                for (int i = 0; i < packedSheets.Count; i++)
                {
                    int sheetNumber = i + 1;
                    string imgPath = Path.Combine(outputDir, $"{outputPrefix}_sprite_{sheetNumber}.{ext}");
                    string jsonPath = Path.Combine(outputDir, $"{outputPrefix}_rectangles_{sheetNumber}.json");

                    using (var ms = new MemoryStream())
                    {
                        packedSheets[i].Image.Save(ms, imgFormat);
                        File.WriteAllBytes(imgPath, ms.ToArray());
                    }

                    var jsonText = JsonSerializer.Serialize(packedSheets[i].Rectangles,
                        new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(jsonPath, jsonText);
                }

                statusLabel.Text = $"Exported {packedSheets.Count} sheets to {outputDir}";
                MessageBox.Show($"Saved {packedSheets.Count} sprite sheets to:\n{outputDir}", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Export failed";
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Packing Logic

        private void ClearAll()
        {
            tabControl.TabPages.Clear();

            foreach (var sheet in loadedSheets)
                sheet.Image?.Dispose();
            loadedSheets.Clear();

            foreach (var packed in packedSheets)
                packed.Image?.Dispose();
            packedSheets.Clear();

            foreach (var bmp in pakBitmaps)
                bmp?.Dispose();
            pakBitmaps.Clear();

            sourcePAK = null;
            sourceDirectory = null;
            outputFormat = OutputFormat.PNG;
        }

        private void UpdatePackingParameters()
        {
            spacing = (int)spacingInput.Value;

            if (useGridMode)
            {
                gridColumns = (int)maxWidthInput.Value;
                gridRows = (int)maxHeightInput.Value;
            }
            else
            {
                maxWidth = (int)maxWidthInput.Value;
                maxHeight = (int)maxHeightInput.Value;
            }
        }

        private void LoadAndPackSprites()
        {
            loadedSheets = LoadSpriteSheets(sourceDirectory);

            if (loadedSheets.Count == 0)
            {
                statusLabel.Text = "No sprite sheets found in directory";
                repackButton.Enabled = false;
                secondaryButton.Enabled = false;
                tertiaryButton.Enabled = false;
                return;
            }

            // Determine output format based on loaded sheets
            int bmpCount = loadedSheets.Count(s => s.IsBmp);
            int pngCount = loadedSheets.Count(s => !s.IsBmp);

            if (bmpCount > 0 && pngCount == 0)
                outputFormat = OutputFormat.BMP;
            else
                outputFormat = OutputFormat.PNG;

            outputPrefix = loadedSheets[0].Prefix;
            UpdatePackingParameters();
            PackSpritesFromLoaded();

            string formatStr = outputFormat == OutputFormat.BMP ? "BMP" : "PNG";
            statusLabel.Text = $"Loaded {loadedSheets.Count} source sheets → {packedSheets.Count} packed sheets (Output: {formatStr})";
        }

        private Color GetBackgroundColor()
        {
            return outputFormat == OutputFormat.BMP ? BmpBackgroundColor : Color.Transparent;
        }

        private PixelFormat GetPixelFormat()
        {
            return outputFormat == OutputFormat.BMP ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
        }

        private void PackSpritesFromLoaded()
        {
            tabControl.TabPages.Clear();

            foreach (var packed in packedSheets)
                packed.Image?.Dispose();
            packedSheets.Clear();

            var allSprites = new List<(Bitmap source, SpriteRectangle rect)>();
            foreach (var sheet in loadedSheets)
            {
                foreach (var rect in sheet.Rectangles)
                    allSprites.Add((sheet.Image, rect));
            }

            int spriteIndex = 0;
            int sheetNumber = 1;

            while (spriteIndex < allSprites.Count)
            {
                var (bitmap, rects, count) = useGridMode
                    ? PackSpritesGrid(allSprites, spriteIndex)
                    : packingMethod == PackingMethod.TightCompact
                        ? PackSpritesTight(allSprites, spriteIndex)
                        : PackSprites(allSprites, spriteIndex);

                packedSheets.Add(new PackedSheet
                {
                    Image = bitmap,
                    Rectangles = rects,
                    SpriteCount = count
                });

                // Create tab with RenderedPanel
                var tabPage = new TabPage($"Sheet {sheetNumber}");

                var renderedPanel = new RenderedPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(40, 40, 40),
                    ViewOnlyMode = true,
                    ShowHotkeyLegend = true,
                    CurrentBitmap = new Bitmap(bitmap), // Clone to avoid disposal issues
                    DirectRectangles = rects.Select(r => new Rectangle(r.x, r.y, r.width, r.height)).ToList()
                };
                renderedPanel.ZoomStatusLabel = zoomLabel;

                string formatStr = outputFormat == OutputFormat.BMP ? "BMP" : "PNG";
                var infoLabel = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 25,
                    Text = $"Size: {bitmap.Width}x{bitmap.Height} | Sprites: {count} | Format: {formatStr}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
                };

                tabPage.Controls.Add(renderedPanel);
                tabPage.Controls.Add(infoLabel);
                tabControl.TabPages.Add(tabPage);

                spriteIndex += count;
                sheetNumber++;
            }

            string statusFormatStr = outputFormat == OutputFormat.BMP ? "BMP" : "PNG";
            statusLabel.Text = $"Loaded {loadedSheets.Count} source sheets → {packedSheets.Count} packed sheets (Output: {statusFormatStr})";
            repackButton.Enabled = true;
            primaryButton.Enabled = true;
            secondaryButton.Enabled = true;
            tertiaryButton.Enabled = true;
        }

        private List<SpriteSheet> LoadSpriteSheets(string directory)
        {
            var sheets = new List<SpriteSheet>();

            var pngFiles = Directory.GetFiles(directory, "*.png");
            var bmpFiles = Directory.GetFiles(directory, "*.bmp");
            var allFiles = pngFiles.Concat(bmpFiles).ToArray();

            Array.Sort(allFiles, CompareSpriteNames);

            var regexPng = new Regex(@"^(.+)_sprite_(\d+)\.png$", RegexOptions.IgnoreCase);
            var regexBmp = new Regex(@"^(.+)_sprite_(\d+)\.bmp$", RegexOptions.IgnoreCase);

            foreach (var imgPath in allFiles)
            {
                string fileName = Path.GetFileName(imgPath);
                bool isBmp = imgPath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase);

                var match = isBmp ? regexBmp.Match(fileName) : regexPng.Match(fileName);

                if (!match.Success)
                    continue;

                string prefix = match.Groups[1].Value;
                string number = match.Groups[2].Value;

                string jsonPath = Path.Combine(directory, $"{prefix}_rectangles_{number}.json");

                if (!File.Exists(jsonPath))
                    continue;

                try
                {
                    var image = new Bitmap(imgPath);
                    var jsonText = File.ReadAllText(jsonPath);
                    var rectangles = JsonSerializer.Deserialize<List<SpriteRectangle>>(jsonText);

                    sheets.Add(new SpriteSheet
                    {
                        ImagePath = imgPath,
                        JsonPath = jsonPath,
                        Image = image,
                        Rectangles = rectangles,
                        Prefix = prefix,
                        IsBmp = isBmp
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading {fileName}: {ex.Message}",
                        "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return sheets;
        }

        private static int CompareSpriteNames(string a, string b)
        {
            int na = ExtractTrailingNumber(a);
            int nb = ExtractTrailingNumber(b);
            return (na >= 0 && nb >= 0) ? na.CompareTo(nb) : StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        private static int ExtractTrailingNumber(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int idx = name.LastIndexOf('_');
            if (idx < 0) return -1;
            return int.TryParse(name[(idx + 1)..], out int n) ? n : -1;
        }

        private (Bitmap bitmap, List<SpriteRectangle> rectangles, int count) PackSpritesGrid(
            List<(Bitmap source, SpriteRectangle rect)> sprites, int startIndex)
        {
            var outputRects = new List<SpriteRectangle>();
            int maxSpritesPerSheet = gridColumns * gridRows;
            int count = Math.Min(maxSpritesPerSheet, sprites.Count - startIndex);

            if (count == 0)
                return (new Bitmap(1, 1), outputRects, 0);

            // For tight mode in grid, use tight packing but limit sprite count
            if (packingMethod == PackingMethod.TightCompact)
            {
                var subset = sprites.Skip(startIndex).Take(count).ToList();
                return PackSpritesTightWithLimit(subset, count);
            }

            // Original row-aligned grid logic
            int currentX = 0, currentY = 0, currentColumn = 0, maxRowHeight = 0;
            int sheetWidth = 0, sheetHeight = 0;

            for (int i = 0; i < count; i++)
            {
                var (_, sourceRect) = sprites[startIndex + i];

                if (currentColumn >= gridColumns)
                {
                    currentX = 0;
                    currentY += maxRowHeight + spacing;
                    currentColumn = 0;
                    maxRowHeight = 0;
                }

                maxRowHeight = Math.Max(maxRowHeight, sourceRect.height);
                sheetWidth = Math.Max(sheetWidth, currentX + sourceRect.width);
                sheetHeight = Math.Max(sheetHeight, currentY + sourceRect.height);

                currentX += sourceRect.width + spacing;
                currentColumn++;
            }

            var bitmap = new Bitmap(sheetWidth, sheetHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                currentX = currentY = currentColumn = maxRowHeight = 0;

                for (int i = 0; i < count; i++)
                {
                    var (sourceBitmap, sourceRect) = sprites[startIndex + i];

                    if (currentColumn >= gridColumns)
                    {
                        currentX = 0;
                        currentY += maxRowHeight + spacing;
                        currentColumn = 0;
                        maxRowHeight = 0;
                    }

                    var sourceRegion = new Rectangle(sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height);
                    g.DrawImage(sourceBitmap,
                        new Rectangle(currentX, currentY, sourceRect.width, sourceRect.height),
                        sourceRegion, GraphicsUnit.Pixel);

                    outputRects.Add(new SpriteRectangle
                    {
                        x = currentX,
                        y = currentY,
                        width = sourceRect.width,
                        height = sourceRect.height,
                        pivotX = sourceRect.pivotX,
                        pivotY = sourceRect.pivotY
                    });

                    maxRowHeight = Math.Max(maxRowHeight, sourceRect.height);
                    currentX += sourceRect.width + spacing;
                    currentColumn++;
                }
            }

            return (bitmap, outputRects, count);
        }

        private (Bitmap bitmap, List<SpriteRectangle> rectangles, int count) PackSpritesTight(
    List<(Bitmap source, SpriteRectangle rect)> sprites, int startIndex)
        {
            // Sort by area descending (largest first)
            var remaining = sprites
                .Skip(startIndex)
                .Select((s, i) => (sprite: s, originalIndex: startIndex + i))
                .OrderByDescending(x => x.sprite.rect.height * x.sprite.rect.width)
                .ThenByDescending(x => x.sprite.rect.height)
                .ToList();

            var outputRects = new List<SpriteRectangle>();
            var placed = new List<Rectangle>(); // Track placed rectangles for overlap detection
            int usedWidth = 0;
            int usedHeight = 0;
            int placedCount = 0;

            var tempBitmap = new Bitmap(maxWidth, maxHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(tempBitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                foreach (var (sprite, originalIndex) in remaining)
                {
                    var (sourceBitmap, sourceRect) = sprite;
                    int spriteWidth = sourceRect.width;
                    int spriteHeight = sourceRect.height;

                    // Find best position scanning left-to-right, top-to-bottom
                    Point? bestPosition = FindBestPosition(spriteWidth, spriteHeight, placed);

                    if (bestPosition == null)
                        break; // No space left, move to next sheet

                    int placeX = bestPosition.Value.X;
                    int placeY = bestPosition.Value.Y;

                    var sourceRegion = new Rectangle(sourceRect.x, sourceRect.y, spriteWidth, spriteHeight);
                    g.DrawImage(sourceBitmap,
                        new Rectangle(placeX, placeY, spriteWidth, spriteHeight),
                        sourceRegion, GraphicsUnit.Pixel);

                    placed.Add(new Rectangle(placeX, placeY, spriteWidth + spacing, spriteHeight + spacing));

                    outputRects.Add(new SpriteRectangle
                    {
                        x = placeX,
                        y = placeY,
                        width = spriteWidth,
                        height = spriteHeight,
                        pivotX = sourceRect.pivotX,
                        pivotY = sourceRect.pivotY
                    });

                    usedWidth = Math.Max(usedWidth, placeX + spriteWidth);
                    usedHeight = Math.Max(usedHeight, placeY + spriteHeight);
                    placedCount++;
                }
            }

            int finalWidth = Math.Max(1, usedWidth);
            int finalHeight = Math.Max(1, usedHeight);

            var finalBitmap = new Bitmap(finalWidth, finalHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(finalBitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(tempBitmap, 0, 0, new Rectangle(0, 0, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }
            tempBitmap.Dispose();

            return (finalBitmap, outputRects, placedCount);
        }

        private Point? FindBestPosition(int width, int height, List<Rectangle> placed)
        {
            int spriteWidth = width + spacing;
            int spriteHeight = height + spacing;

            if (spriteWidth > maxWidth || spriteHeight > maxHeight)
                return null;

            Point? bestPos = null;
            int bestY = int.MaxValue;
            int bestX = int.MaxValue;

            var candidates = new List<Point> { new Point(0, 0) };

            foreach (var rect in placed)
            {
                candidates.Add(new Point(rect.Right, rect.Y));
                candidates.Add(new Point(rect.X, rect.Bottom));
                candidates.Add(new Point(rect.Right, rect.Bottom));
            }

            foreach (var candidate in candidates)
            {
                int testX = candidate.X;
                int testY = candidate.Y;

                if (testX + spriteWidth > maxWidth || testY + spriteHeight > maxHeight)
                    continue;

                var testRect = new Rectangle(testX, testY, spriteWidth, spriteHeight);
                bool overlaps = placed.Any(r => testRect.IntersectsWith(r));

                if (!overlaps)
                {
                    if (testY < bestY || (testY == bestY && testX < bestX))
                    {
                        bestY = testY;
                        bestX = testX;
                        bestPos = new Point(testX, testY);
                    }
                }
            }

            return bestPos;
        }

        private (Bitmap bitmap, List<SpriteRectangle> rectangles, int count) PackSpritesTightWithLimit(
            List<(Bitmap source, SpriteRectangle rect)> sprites, int maxCount)
        {
            // Sort by area descending
            var sorted = sprites
                .Take(maxCount)
                .OrderByDescending(x => x.rect.height * x.rect.width)
                .ThenByDescending(x => x.rect.height)
                .ToList();

            // Calculate canvas size estimate
            int totalArea = sorted.Sum(s => (s.rect.width + spacing) * (s.rect.height + spacing));
            int maxSpriteWidth = sorted.Max(s => s.rect.width) + spacing;
            int maxSpriteHeight = sorted.Max(s => s.rect.height) + spacing;
            int estimatedSide = (int)Math.Ceiling(Math.Sqrt(totalArea * 1.2));
            int canvasWidth = Math.Max(maxSpriteWidth, estimatedSide);
            int canvasHeight = Math.Max(maxSpriteHeight, estimatedSide * 2);

            var outputRects = new List<SpriteRectangle>();
            var placed = new List<Rectangle>();
            int usedWidth = 0;
            int usedHeight = 0;

            var tempBitmap = new Bitmap(canvasWidth, canvasHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(tempBitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                foreach (var (sourceBitmap, sourceRect) in sorted)
                {
                    int spriteWidth = sourceRect.width;
                    int spriteHeight = sourceRect.height;

                    Point? bestPos = FindBestPositionWithBounds(spriteWidth, spriteHeight, placed, canvasWidth, canvasHeight);

                    if (bestPos == null)
                        continue; // Skip if can't place

                    int placeX = bestPos.Value.X;
                    int placeY = bestPos.Value.Y;

                    var sourceRegion = new Rectangle(sourceRect.x, sourceRect.y, spriteWidth, spriteHeight);
                    g.DrawImage(sourceBitmap,
                        new Rectangle(placeX, placeY, spriteWidth, spriteHeight),
                        sourceRegion, GraphicsUnit.Pixel);

                    placed.Add(new Rectangle(placeX, placeY, spriteWidth + spacing, spriteHeight + spacing));

                    outputRects.Add(new SpriteRectangle
                    {
                        x = placeX,
                        y = placeY,
                        width = spriteWidth,
                        height = spriteHeight,
                        pivotX = sourceRect.pivotX,
                        pivotY = sourceRect.pivotY
                    });

                    usedWidth = Math.Max(usedWidth, placeX + spriteWidth);
                    usedHeight = Math.Max(usedHeight, placeY + spriteHeight);
                }
            }

            int finalWidth = Math.Max(1, usedWidth);
            int finalHeight = Math.Max(1, usedHeight);

            var finalBitmap = new Bitmap(finalWidth, finalHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(finalBitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(tempBitmap, 0, 0, new Rectangle(0, 0, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }
            tempBitmap.Dispose();

            return (finalBitmap, outputRects, outputRects.Count);
        }

        private Point? FindBestPositionWithBounds(int width, int height, List<Rectangle> placed, int maxW, int maxH)
        {
            int spriteWidth = width + spacing;
            int spriteHeight = height + spacing;

            if (spriteWidth > maxW || spriteHeight > maxH)
                return null;

            Point? bestPos = null;
            int bestY = int.MaxValue;
            int bestX = int.MaxValue;

            var candidates = new List<Point> { new Point(0, 0) };

            foreach (var rect in placed)
            {
                candidates.Add(new Point(rect.Right, rect.Y));
                candidates.Add(new Point(rect.X, rect.Bottom));
                candidates.Add(new Point(rect.Right, rect.Bottom));
            }

            foreach (var candidate in candidates)
            {
                int testX = candidate.X;
                int testY = candidate.Y;

                if (testX + spriteWidth > maxW || testY + spriteHeight > maxH)
                    continue;

                var testRect = new Rectangle(testX, testY, spriteWidth, spriteHeight);
                bool overlaps = placed.Any(r => testRect.IntersectsWith(r));

                if (!overlaps)
                {
                    if (testY < bestY || (testY == bestY && testX < bestX))
                    {
                        bestY = testY;
                        bestX = testX;
                        bestPos = new Point(testX, testY);
                    }
                }
            }

            return bestPos;
        }

        private (Bitmap bitmap, List<SpriteRectangle> rectangles, int count) PackSprites(
            List<(Bitmap source, SpriteRectangle rect)> sprites, int startIndex)
        {
            var outputRects = new List<SpriteRectangle>();
            int currentX = 0, currentY = 0, rowHeight = 0, maxUsedWidth = 0, count = 0;

            var tempBitmap = new Bitmap(maxWidth, maxHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(tempBitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                for (int i = startIndex; i < sprites.Count; i++)
                {
                    var (sourceBitmap, sourceRect) = sprites[i];
                    int spriteWidth = sourceRect.width;
                    int spriteHeight = sourceRect.height;

                    if (currentX > 0 && currentX + spriteWidth + spacing > maxWidth)
                    {
                        currentX = 0;
                        currentY += rowHeight + spacing;
                        rowHeight = 0;
                    }

                    if (currentY + spriteHeight + spacing > maxHeight)
                        break;

                    var sourceRegion = new Rectangle(sourceRect.x, sourceRect.y, spriteWidth, spriteHeight);
                    g.DrawImage(sourceBitmap,
                        new Rectangle(currentX, currentY, spriteWidth, spriteHeight),
                        sourceRegion, GraphicsUnit.Pixel);

                    outputRects.Add(new SpriteRectangle
                    {
                        x = currentX,
                        y = currentY,
                        width = spriteWidth,
                        height = spriteHeight,
                        pivotX = sourceRect.pivotX,
                        pivotY = sourceRect.pivotY
                    });

                    currentX += spriteWidth + spacing;
                    rowHeight = Math.Max(rowHeight, spriteHeight);
                    maxUsedWidth = Math.Max(maxUsedWidth, currentX);
                    count++;
                }
            }

            int finalWidth = Math.Max(1, maxUsedWidth - spacing);
            int finalHeight = Math.Max(1, currentY + rowHeight);

            var finalBitmap = new Bitmap(finalWidth, finalHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(finalBitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(tempBitmap, 0, 0, new Rectangle(0, 0, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }
            tempBitmap.Dispose();

            return (finalBitmap, outputRects, count);
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
        }
    }
}