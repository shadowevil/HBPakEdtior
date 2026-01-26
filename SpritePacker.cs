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

    public enum ItemModeType
    {
        None,
        Melee,  // Sword, Axe, Staff - uses Attack index 4 for slots 5 & 6
        Bow,    // Bow, Crossbow - uses Attack index 5 for slot 7
        Shield  // Shield - 7 sprites reordered to 12 armor format
    }

    /// <summary>
    /// Static class for CLI/headless compact operations
    /// </summary>
    public static class SpritePackerCLI
    {
        /// <summary>
        /// Main CLI entry point - routes to appropriate command handler
        /// </summary>
        public static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 1;
            }

            string command = args[0].ToLower();
            return command switch
            {
                "help" or "--help" or "-h" => PrintHelp(),
                "import" => RunImport(args),
                "extract" => RunExtract(args),
                "info" => RunInfo(args),
                "compact" => RunCompact(args),
                _ => UnknownCommand(command)
            };
        }

        private static int UnknownCommand(string command)
        {
            Console.WriteLine($"Unknown command: {command}");
            Console.WriteLine("Use 'HBPakEditor help' to see available commands.");
            return 1;
        }

        private static int PrintHelp()
        {
            Console.WriteLine("HBPakEditor CLI - PAK file management tool");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine();
            Console.WriteLine("  import <directory> <output.pak> [--rects]");
            Console.WriteLine("    Import sprites from directory into a new PAK file");
            Console.WriteLine("    --rects  Include rectangle data from JSON files");
            Console.WriteLine();
            Console.WriteLine("  extract <pak_file> <output_dir> [--rects] [--sprite <index>]");
            Console.WriteLine("    Extract sprites from PAK to directory");
            Console.WriteLine("    --rects         Also export rectangle data as JSON");
            Console.WriteLine("    --sprite <n>    Extract only sprite at index n");
            Console.WriteLine();
            Console.WriteLine("  info <pak_file>");
            Console.WriteLine("    Show PAK file information (sprites, rectangles, format, size)");
            Console.WriteLine();
            Console.WriteLine("  compact <mode> [options] <pak_file>");
            Console.WriteLine("    Repack sprites in a PAK file");
            Console.WriteLine("    Modes: row | tight | melee | bow | shield");
            Console.WriteLine("    Options:");
            Console.WriteLine("      grid <cols> <rows>  Use grid-based packing");
            Console.WriteLine("      spacing <n>         Set spacing between sprites (default: 1)");
            Console.WriteLine("      clear               Clear existing sprites before adding compacted ones");
            Console.WriteLine("      -o <output.pak>     Output to different file instead of overwriting");
            Console.WriteLine();
            Console.WriteLine("  help");
            Console.WriteLine("    Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  HBPakEditor import ./sprites/ weapon.pak --rects");
            Console.WriteLine("  HBPakEditor extract weapon.pak ./output/ --rects");
            Console.WriteLine("  HBPakEditor extract weapon.pak ./output/ --sprite 5 --rects");
            Console.WriteLine("  HBPakEditor info weapon.pak");
            Console.WriteLine("  HBPakEditor compact melee clear -o new.pak weapon.pak");
            Console.WriteLine("  HBPakEditor compact tight grid 8 8 spacing 2 weapon.pak");
            return 0;
        }

        #region Import Command

        private static int RunImport(string[] args)
        {
            // import <directory> <output.pak> [--rects]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: HBPakEditor import <directory> <output.pak> [--rects]");
                return 1;
            }

            string directory = args[1];
            string outputPath = args[2];
            bool importRects = args.Any(a => a.Equals("--rects", StringComparison.OrdinalIgnoreCase));

            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"Error: Directory not found: {directory}");
                return 1;
            }

            try
            {
                Console.WriteLine($"Importing from: {directory}");
                Console.WriteLine($"Output: {outputPath}");
                Console.WriteLine($"Include rectangles: {importRects}");

                // Get all image files sorted by trailing number
                var sprites = Directory.GetFiles(directory, "*.bmp")
                    .Concat(Directory.GetFiles(directory, "*.png"))
                    .OrderBy(f => ExtractTrailingNumber(f))
                    .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (sprites.Count == 0)
                {
                    Console.WriteLine("Error: No BMP or PNG files found in directory");
                    return 1;
                }

                Console.WriteLine($"Found {sprites.Count} sprite files");

                // Get rectangle files if needed
                List<string> rectangles = new();
                if (importRects)
                {
                    rectangles = Directory.GetFiles(directory, "*.json")
                        .OrderBy(f => ExtractTrailingNumber(f))
                        .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (rectangles.Count != sprites.Count)
                    {
                        Console.WriteLine($"Error: Sprite/rectangle count mismatch. Found {sprites.Count} sprites but {rectangles.Count} rectangle files.");
                        return 1;
                    }
                }

                // Load existing PAK or create new one
                PAK pak;
                if (File.Exists(outputPath))
                {
                    Console.WriteLine($"Appending to existing PAK...");
                    pak = PAK.ReadFromFile(outputPath, "");
                    if (pak?.Data == null)
                    {
                        pak = new PAK();
                        pak.Data = new PAKLib.PAKData();
                    }
                }
                else
                {
                    pak = new PAK();
                    pak.Data = new PAKLib.PAKData();
                }

                int errors = 0;
                for (int i = 0; i < sprites.Count; i++)
                {
                    var sprite = new PAKLib.Sprite();
                    sprite.data = File.ReadAllBytes(sprites[i]);

                    if (importRects && i < rectangles.Count)
                    {
                        var rects = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PAKLib.SpriteRectangle>>(File.ReadAllText(rectangles[i]));
                        if (rects != null)
                            sprite.Rectangles = rects;
                        else
                            errors++;
                    }

                    pak.Data.Sprites.Add(sprite);
                    Console.Write($"\rImported {i + 1}/{sprites.Count} sprites");
                }
                Console.WriteLine();

                if (errors > 0)
                    Console.WriteLine($"Warning: {errors} rectangle files failed to parse");

                // Save PAK
                Console.WriteLine($"Saving PAK: {outputPath}");
                PAK.SaveToFile(pak, outputPath, "");

                Console.WriteLine("Done!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        #endregion

        #region Extract Command

        private static int RunExtract(string[] args)
        {
            // extract <pak_file> <output_dir> [--rects] [--sprite <index>]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: HBPakEditor extract <pak_file> <output_dir> [--rects] [--sprite <index>]");
                return 1;
            }

            string pakPath = args[1];
            string outputDir = args[2];
            bool exportRects = args.Any(a => a.Equals("--rects", StringComparison.OrdinalIgnoreCase));
            int? spriteIndex = null;

            // Parse --sprite <index>
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--sprite", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(args[i + 1], out int idx))
                        spriteIndex = idx;
                    else
                    {
                        Console.WriteLine($"Error: Invalid sprite index: {args[i + 1]}");
                        return 1;
                    }
                }
            }

            if (!File.Exists(pakPath))
            {
                Console.WriteLine($"Error: PAK file not found: {pakPath}");
                return 1;
            }

            if (Path.GetExtension(pakPath).Equals(".epak", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Error: Encrypted PAK files (.epak) are not supported in CLI mode");
                return 1;
            }

            try
            {
                Console.WriteLine($"Loading PAK: {pakPath}");
                var pak = PAK.ReadFromFile(pakPath, "");

                if (pak?.Data == null || pak.Data.Sprites.Count == 0)
                {
                    Console.WriteLine("Error: PAK contains no sprites");
                    return 1;
                }

                Console.WriteLine($"Found {pak.Data.Sprites.Count} sprites");

                // Validate sprite index
                if (spriteIndex.HasValue)
                {
                    if (spriteIndex.Value < 0 || spriteIndex.Value >= pak.Data.Sprites.Count)
                    {
                        Console.WriteLine($"Error: Sprite index {spriteIndex.Value} out of range (0-{pak.Data.Sprites.Count - 1})");
                        return 1;
                    }
                }

                // Create output directory
                Directory.CreateDirectory(outputDir);

                string pakName = Path.GetFileNameWithoutExtension(pakPath);
                int startIdx = spriteIndex ?? 0;
                int endIdx = spriteIndex.HasValue ? spriteIndex.Value + 1 : pak.Data.Sprites.Count;

                for (int i = startIdx; i < endIdx; i++)
                {
                    var sprite = pak.Data.Sprites[i];
                    if (sprite?.data == null) continue;

                    // Determine extension from file signature
                    string ext = HBPakEditor.FileSignatureDetector.GetFileExtension(sprite.data) ?? ".bin";
                    string fileName = $"{pakName}_sprite_{i}{ext}";
                    string filePath = Path.Combine(outputDir, fileName);

                    File.WriteAllBytes(filePath, sprite.data);

                    if (exportRects)
                    {
                        string rectFileName = $"{pakName}_rectangles_{i}.json";
                        string rectPath = Path.Combine(outputDir, rectFileName);
                        var rects = sprite.Rectangles ?? new List<PAKLib.SpriteRectangle>();
                        File.WriteAllText(rectPath, Newtonsoft.Json.JsonConvert.SerializeObject(rects, Newtonsoft.Json.Formatting.Indented));
                    }

                    if (!spriteIndex.HasValue)
                        Console.Write($"\rExtracted {i + 1}/{pak.Data.Sprites.Count} sprites");
                }

                if (!spriteIndex.HasValue)
                    Console.WriteLine();
                else
                    Console.WriteLine($"Extracted sprite {spriteIndex.Value}");

                Console.WriteLine($"Output directory: {outputDir}");
                Console.WriteLine("Done!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        #endregion

        #region Info Command

        private static int RunInfo(string[] args)
        {
            // info <pak_file>
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: HBPakEditor info <pak_file>");
                return 1;
            }

            string pakPath = args[1];

            if (!File.Exists(pakPath))
            {
                Console.WriteLine($"Error: PAK file not found: {pakPath}");
                return 1;
            }

            if (Path.GetExtension(pakPath).Equals(".epak", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Error: Encrypted PAK files (.epak) are not supported in CLI mode");
                return 1;
            }

            try
            {
                var pak = PAK.ReadFromFile(pakPath, "");

                if (pak?.Data == null)
                {
                    Console.WriteLine("Error: Failed to read PAK file");
                    return 1;
                }

                var fileInfo = new FileInfo(pakPath);
                long fileSize = fileInfo.Length;
                string sizeStr = FormatFileSize(fileSize);

                Console.WriteLine($"File: {pakPath}");
                Console.WriteLine($"Size: {sizeStr}");
                Console.WriteLine($"Sprites: {pak.Data.Sprites.Count}");

                if (pak.Data.Sprites.Count == 0)
                    return 0;

                // Determine format from first sprite
                string format = "Unknown";
                if (pak.Data.Sprites[0]?.data != null)
                {
                    format = HBPakEditor.FileSignatureDetector.DetectFileType(pak.Data.Sprites[0].data) ?? "Unknown";
                }
                Console.WriteLine($"Format: {format}");

                // Count total rectangles
                int totalRects = pak.Data.Sprites.Sum(s => s?.Rectangles?.Count ?? 0);
                Console.WriteLine($"Total Rectangles: {totalRects}");

                Console.WriteLine();
                Console.WriteLine("Sprite Details:");
                for (int i = 0; i < pak.Data.Sprites.Count; i++)
                {
                    var sprite = pak.Data.Sprites[i];
                    if (sprite?.data == null)
                    {
                        Console.WriteLine($"  [{i}] (empty)");
                        continue;
                    }

                    // Get dimensions by loading image
                    string dims = "?x?";
                    try
                    {
                        using var ms = new MemoryStream(sprite.data);
                        using var img = System.Drawing.Image.FromStream(ms);
                        dims = $"{img.Width}x{img.Height}";
                    }
                    catch { }

                    int rectCount = sprite.Rectangles?.Count ?? 0;
                    string spriteFormat = HBPakEditor.FileSignatureDetector.DetectFileType(sprite.data) ?? "?";
                    Console.WriteLine($"  [{i}] {dims}, {rectCount} rects, {spriteFormat}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        #endregion

        #region Compact Command

        private static int RunCompact(string[] args)
        {
            // compact <mode> [grid <cols> <rows>] [spacing <n>] [clear] [-o <output.pak>] <pak_file>
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: HBPakEditor compact <mode> [options] <pak_file>");
                Console.WriteLine("  Modes: row | tight | melee | bow | shield");
                Console.WriteLine("  Options:");
                Console.WriteLine("    grid <cols> <rows>  Use grid-based packing");
                Console.WriteLine("    spacing <n>         Set spacing between sprites (default: 1)");
                Console.WriteLine("    clear               Clear existing sprites before adding compacted ones");
                Console.WriteLine("    -o <output.pak>     Output to different file instead of overwriting");
                return 1;
            }

            // Parse mode
            string modeStr = args[1].ToLower();
            bool isItemMode = modeStr is "melee" or "weapon" or "bow" or "shield";
            bool isPackMode = modeStr is "row" or "tight";

            if (!isItemMode && !isPackMode)
            {
                Console.WriteLine($"Error: Unknown mode '{modeStr}'. Use row, tight, melee, bow, or shield.");
                return 1;
            }

            // Parse options
            bool clearExisting = false;
            bool useGrid = false;
            int gridCols = 8;
            int gridRows = 100;
            int spacing = 1;
            string? outputPath = null;
            string? pakPath = null;

            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i].ToLower();

                if (arg == "clear")
                {
                    clearExisting = true;
                }
                else if (arg == "grid" && i + 2 < args.Length)
                {
                    useGrid = true;
                    if (!int.TryParse(args[i + 1], out gridCols) || !int.TryParse(args[i + 2], out gridRows))
                    {
                        Console.WriteLine("Error: Invalid grid dimensions");
                        return 1;
                    }
                    i += 2;
                }
                else if (arg == "spacing" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[i + 1], out spacing))
                    {
                        Console.WriteLine("Error: Invalid spacing value");
                        return 1;
                    }
                    i++;
                }
                else if (arg == "-o" && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                    i++;
                }
                else if (!arg.StartsWith("-"))
                {
                    pakPath = args[i];
                }
            }

            if (string.IsNullOrEmpty(pakPath))
            {
                Console.WriteLine("Error: No PAK file specified");
                return 1;
            }

            if (!File.Exists(pakPath))
            {
                Console.WriteLine($"Error: File not found: {pakPath}");
                return 1;
            }

            if (Path.GetExtension(pakPath).Equals(".epak", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Error: Encrypted PAK files (.epak) are not supported in CLI mode");
                return 1;
            }

            outputPath ??= pakPath;

            try
            {
                Console.WriteLine($"Loading PAK: {pakPath}");
                var pak = PAK.ReadFromFile(pakPath, "");

                if (pak?.Data == null || pak.Data.Sprites.Count == 0)
                {
                    Console.WriteLine("Error: PAK contains no sprites");
                    return 1;
                }

                Console.WriteLine($"Found {pak.Data.Sprites.Count} sprites");
                Console.WriteLine($"Mode: {modeStr}");
                Console.WriteLine($"Clear existing: {clearExisting}");
                if (useGrid)
                    Console.WriteLine($"Grid: {gridCols}x{gridRows}");
                Console.WriteLine($"Spacing: {spacing}");

                if (isItemMode)
                {
                    // Item mode (melee/bow/shield)
                    ItemModeType itemMode = modeStr switch
                    {
                        "melee" or "weapon" => ItemModeType.Melee,
                        "bow" => ItemModeType.Bow,
                        "shield" => ItemModeType.Shield,
                        _ => ItemModeType.None
                    };

                    var compactor = new SpritePackerForm(pak, Path.GetFileNameWithoutExtension(pakPath));
                    compactor.SetItemModeForCLI(itemMode, spacing);

                    var compactedSprites = compactor.GetSprites();

                    if (compactedSprites.Count == 0)
                    {
                        Console.WriteLine("Error: Compaction produced no sprites");
                        return 1;
                    }

                    Console.WriteLine($"Compacted to {compactedSprites.Count} sprites");

                    if (clearExisting)
                        pak.Data.Sprites.Clear();

                    pak.Data.Sprites.AddRange(compactedSprites);
                }
                else
                {
                    // Pack mode (row/tight)
                    var compactor = new SpritePackerForm(pak, Path.GetFileNameWithoutExtension(pakPath));
                    compactor.SetPackModeForCLI(
                        modeStr == "tight" ? PackingMethod.TightCompact : PackingMethod.RowAligned,
                        useGrid, gridCols, gridRows, spacing);

                    var compactedSprites = compactor.GetSprites();

                    if (compactedSprites.Count == 0)
                    {
                        Console.WriteLine("Error: Compaction produced no sprites");
                        return 1;
                    }

                    Console.WriteLine($"Compacted to {compactedSprites.Count} sprites");

                    if (clearExisting)
                        pak.Data.Sprites.Clear();

                    pak.Data.Sprites.AddRange(compactedSprites);
                }

                Console.WriteLine($"Saving PAK: {outputPath}");
                PAK.SaveToFile(pak, outputPath, "");

                Console.WriteLine("Done!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        #endregion

        #region Helpers

        private static int ExtractTrailingNumber(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int underscore = name.LastIndexOf('_');
            if (underscore < 0)
                return int.MaxValue; // No number, sort at end

            string tail = name[(underscore + 1)..];
            return int.TryParse(tail, out int n) ? n : int.MaxValue;
        }

        #endregion
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

        // Item mode
        private ItemModeType itemMode = ItemModeType.None;
        private CheckBox itemModeCheckbox;
        private GroupBox itemModeGroup;
        private RadioButton meleeRadio;
        private RadioButton bowRadio;
        private RadioButton shieldRadio;

        // Clear existing sprites flag (set by confirmation dialog)
        public bool ClearExistingSprites { get; private set; } = false;

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
                ColumnCount = 10,
                RowCount = 1,
                AutoSize = false
            };

            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));  // Packing group
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));  // Item mode group
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

            // Item mode GroupBox (for PAK mode only)
            itemModeGroup = new GroupBox
            {
                Text = "Item Mode",
                Dock = DockStyle.Fill,
                Visible = mode == SourceMode.PAK
            };

            itemModeCheckbox = new CheckBox
            {
                Text = "Enable",
                AutoSize = true,
                Location = new Point(8, centerInGroupbox)
            };
            itemModeCheckbox.CheckedChanged += ItemModeCheckbox_CheckedChanged;

            meleeRadio = new RadioButton
            {
                Text = "Melee",
                AutoSize = true,
                Checked = true,
                Location = new Point(85, centerInGroupbox),
                Enabled = false
            };
            meleeRadio.CheckedChanged += ItemModeType_CheckedChanged;

            bowRadio = new RadioButton
            {
                Text = "Bow",
                AutoSize = true,
                Location = new Point(160, centerInGroupbox),
                Enabled = false
            };
            bowRadio.CheckedChanged += ItemModeType_CheckedChanged;

            shieldRadio = new RadioButton
            {
                Text = "Shield",
                AutoSize = true,
                Location = new Point(225, centerInGroupbox),
                Enabled = false
            };
            shieldRadio.CheckedChanged += ItemModeType_CheckedChanged;

            itemModeGroup.Controls.Add(itemModeCheckbox);
            itemModeGroup.Controls.Add(meleeRadio);
            itemModeGroup.Controls.Add(bowRadio);
            itemModeGroup.Controls.Add(shieldRadio);

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
            settingsLayout.Controls.Add(itemModeGroup, 1, 0);
            settingsLayout.Controls.Add(gridModeCheckbox, 2, 0);
            settingsLayout.Controls.Add(maxWidthLabel, 3, 0);
            settingsLayout.Controls.Add(maxWidthInput, 4, 0);
            settingsLayout.Controls.Add(maxHeightLabel, 5, 0);
            settingsLayout.Controls.Add(maxHeightInput, 6, 0);
            settingsLayout.Controls.Add(spacingLabel, 7, 0);
            settingsLayout.Controls.Add(spacingInput, 8, 0);
            settingsLayout.Controls.Add(repackButton, 9, 0);

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
                    // Ask if user wants to clear existing sprites
                    var result = MessageBox.Show(
                        "Do you want to clear the existing sprites and rectangles?\n\n" +
                        "Yes = Replace all with compacted sprites\n" +
                        "No = Append compacted sprites to existing\n" +
                        "Cancel = Don't compact",
                        "Clear Existing Sprites?",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == System.Windows.Forms.DialogResult.Cancel)
                        return;

                    ClearExistingSprites = (result == System.Windows.Forms.DialogResult.Yes);
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

        private void ItemModeCheckbox_CheckedChanged(object? sender, EventArgs e)
        {
            bool enabled = itemModeCheckbox.Checked;
            meleeRadio.Enabled = enabled;
            bowRadio.Enabled = enabled;
            shieldRadio.Enabled = enabled;

            if (enabled)
            {
                if (meleeRadio.Checked)
                    itemMode = ItemModeType.Melee;
                else if (bowRadio.Checked)
                    itemMode = ItemModeType.Bow;
                else if (shieldRadio.Checked)
                    itemMode = ItemModeType.Shield;
                // Disable other packing options when item mode is active (but keep spacing)
                packingMethodGroup.Enabled = false;
                gridModeCheckbox.Enabled = false;
                maxWidthInput.Enabled = false;
                maxHeightInput.Enabled = false;
                // spacingInput stays enabled for item mode
            }
            else
            {
                itemMode = ItemModeType.None;
                packingMethodGroup.Enabled = true;
                gridModeCheckbox.Enabled = true;
                maxWidthInput.Enabled = true;
                maxHeightInput.Enabled = true;
            }
        }

        private void ItemModeType_CheckedChanged(object? sender, EventArgs e)
        {
            if (!itemModeCheckbox.Checked) return;

            if (meleeRadio.Checked)
                itemMode = ItemModeType.Melee;
            else if (bowRadio.Checked)
                itemMode = ItemModeType.Bow;
            else if (shieldRadio.Checked)
                itemMode = ItemModeType.Shield;
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

        /// <summary>
        /// Set item mode and repack for CLI/headless use
        /// </summary>
        public void SetItemModeForCLI(ItemModeType mode, int spacingValue)
        {
            itemMode = mode;
            spacing = spacingValue;

            // Clear and reprocess with item mode
            packedSheets.Clear();

            if (itemMode == ItemModeType.Shield)
            {
                ProcessShieldModeCLI();
            }
            else
            {
                ProcessItemModeCLI();
            }
        }

        /// <summary>
        /// Set packing mode for CLI (row/tight)
        /// </summary>
        public void SetPackModeForCLI(PackingMethod method, bool gridMode, int cols, int rows, int spacingValue)
        {
            packingMethod = method;
            useGridMode = gridMode;
            spacing = spacingValue;

            if (gridMode)
            {
                gridColumns = cols;
                gridRows = rows;
            }
            else
            {
                maxWidth = 2000;
                maxHeight = 5000;
            }

            // Clear and reprocess
            packedSheets.Clear();
            PackSpritesFromLoadedCLI();
        }

        /// <summary>
        /// Pack sprites without UI updates (for CLI)
        /// </summary>
        private void PackSpritesFromLoadedCLI()
        {
            var allSprites = new List<(Bitmap source, SpriteRectangle rect)>();
            foreach (var sheet in loadedSheets)
            {
                foreach (var rect in sheet.Rectangles)
                    allSprites.Add((sheet.Image, rect));
            }

            int spriteIndex = 0;

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

                spriteIndex += count;
            }
        }

        /// <summary>
        /// Process item mode without UI updates (for CLI)
        /// </summary>
        private void ProcessItemModeCLI()
        {
            if (loadedSheets.Count == 0) return;

            // Validate sprite count
            if (loadedSheets.Count < 56)
            {
                throw new InvalidOperationException($"Item mode requires 56 sprites (7 animations × 8 directions). Found only {loadedSheets.Count} sprites.");
            }

            // Define mapping from armor index to weapon index
            int[] weaponMapping;
            if (itemMode == ItemModeType.Melee)
            {
                weaponMapping = new[]
                {
                    WeaponAnimIndex.StandingPeace,
                    WeaponAnimIndex.StandingCombat,
                    WeaponAnimIndex.WalkingPeace,
                    WeaponAnimIndex.WalkingCombat,
                    WeaponAnimIndex.Running,
                    -1,  // Attack Peace (blank)
                    WeaponAnimIndex.AttackMelee,
                    -1,  // Attack Bow (blank)
                    -1, -1, -1, -1  // Cast, Pickup, Damage, Dying
                };
            }
            else // Bow
            {
                // Note: Bows store their attack animation at PAK index 4 (AttackMelee slot),
                // not index 5 (AttackBow slot). This is how original HB bow PAKs are structured.
                weaponMapping = new[]
                {
                    WeaponAnimIndex.StandingPeace,
                    WeaponAnimIndex.StandingCombat,
                    WeaponAnimIndex.WalkingPeace,
                    WeaponAnimIndex.WalkingCombat,
                    WeaponAnimIndex.Running,
                    -1, -1,  // Attack Peace, Attack Combat (blank)
                    WeaponAnimIndex.AttackMelee,  // 7: Attack Bow - bows use melee slot in PAK
                    -1, -1, -1, -1  // Cast, Pickup, Damage, Dying
                };
            }

            using var blankSprite = CreateBlankSprite();

            for (int armorIdx = 0; armorIdx < 12; armorIdx++)
            {
                int weaponIdx = weaponMapping[armorIdx];

                Bitmap sheetBitmap;
                var outputRects = new List<SpriteRectangle>();

                if (weaponIdx < 0)
                {
                    sheetBitmap = new Bitmap(blankSprite);
                }
                else
                {
                    var directionSheets = new List<SpriteSheet>();
                    for (int dir = 0; dir < 8; dir++)
                    {
                        int spriteIdx = weaponIdx * 8 + dir;
                        if (spriteIdx < loadedSheets.Count)
                        {
                            directionSheets.Add(loadedSheets[spriteIdx]);
                        }
                    }
                    (sheetBitmap, outputRects) = PackSpritesGrid8xN(directionSheets);
                }

                packedSheets.Add(new PackedSheet
                {
                    Image = sheetBitmap,
                    Rectangles = outputRects,
                    SpriteCount = outputRects.Count
                });
            }
        }

        /// <summary>
        /// Process shield mode without UI updates (for CLI)
        /// </summary>
        private void ProcessShieldModeCLI()
        {
            if (loadedSheets.Count == 0) return;

            if (loadedSheets.Count < 7)
            {
                throw new InvalidOperationException($"Shield mode requires 7 sprites. Found only {loadedSheets.Count} sprites.");
            }

            int[] shieldMapping = new[]
            {
                ShieldAnimIndex.StandingPeace,
                ShieldAnimIndex.StandingCombat,
                ShieldAnimIndex.WalkingPeace,
                ShieldAnimIndex.WalkingCombat,
                ShieldAnimIndex.Running,
                -1,  // Attack Peace (blank)
                ShieldAnimIndex.AttackMelee,
                -1,  // Attack Bow (blank)
                -1,  // Attack Cast (blank)
                -1,  // Pickup (blank)
                ShieldAnimIndex.TakeDamage,
                -1   // Dying (blank)
            };

            using var blankSprite = CreateBlankSprite();

            for (int armorIdx = 0; armorIdx < 12; armorIdx++)
            {
                int shieldIdx = shieldMapping[armorIdx];

                Bitmap sheetBitmap;
                var outputRects = new List<SpriteRectangle>();

                if (shieldIdx < 0)
                {
                    sheetBitmap = new Bitmap(blankSprite);
                }
                else
                {
                    var sourceSheet = loadedSheets[shieldIdx];
                    sheetBitmap = new Bitmap(sourceSheet.Image);
                    outputRects = sourceSheet.Rectangles.Select(r => new SpriteRectangle
                    {
                        x = r.x,
                        y = r.y,
                        width = r.width,
                        height = r.height,
                        pivotX = r.pivotX,
                        pivotY = r.pivotY
                    }).ToList();
                }

                packedSheets.Add(new PackedSheet
                {
                    Image = sheetBitmap,
                    Rectangles = outputRects,
                    SpriteCount = outputRects.Count
                });
            }
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

        #region Item Mode Processing

        /// <summary>
        /// Weapon PAK sprite indices (each has 8 direction frames)
        /// Note: Bows store their attack animation at AttackMelee (index 4), not AttackBow (index 5).
        /// The AttackBow slot in bow PAKs is typically empty/unused.
        /// </summary>
        private static class WeaponAnimIndex
        {
            public const int StandingPeace = 0;   // Sprites 0-7
            public const int StandingCombat = 1;  // Sprites 8-15
            public const int WalkingPeace = 2;    // Sprites 16-23
            public const int WalkingCombat = 3;   // Sprites 24-31
            public const int AttackMelee = 4;     // Sprites 32-39 (also used by bows for their attack)
            public const int AttackBow = 5;       // Sprites 40-47 (unused in bow PAKs)
            public const int Running = 6;         // Sprites 48-55
        }

        /// <summary>
        /// Armor output sprite sheet indices
        /// </summary>
        private static class ArmorAnimIndex
        {
            public const int StandingPeace = 0;
            public const int StandingCombat = 1;
            public const int WalkingPeace = 2;
            public const int WalkingCombat = 3;
            public const int Running = 4;
            public const int AttackPeace = 5;
            public const int AttackCombat = 6;
            public const int AttackBow = 7;
            public const int AttackCast = 8;
            public const int Pickup = 9;
            public const int TakeDamage = 10;
            public const int Dying = 11;
        }

        private static readonly string[] ArmorAnimNames = new[]
        {
            "Standing Peace", "Standing Combat", "Walking Peace", "Walking Combat",
            "Running", "Attack Peace", "Attack Combat", "Attack Bow",
            "Attack Cast", "Pickup", "Take Damage", "Dying"
        };

        /// <summary>
        /// Creates a blank 100x100 sprite with "NOT USED" text
        /// </summary>
        private Bitmap CreateBlankSprite()
        {
            const int size = 100;
            var bitmap = new Bitmap(size, size, GetPixelFormat());

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(GetBackgroundColor());

                // Draw "NOT USED" text - use smaller font to ensure it fits
                using var font = new Font("Arial", 9, FontStyle.Bold);
                using var brush = new SolidBrush(Color.Gray);
                var text = "NOT USED";
                var textSize = g.MeasureString(text, font);
                float x = (size - textSize.Width) / 2;
                float y = (size - textSize.Height) / 2;
                g.DrawString(text, font, brush, x, y);
            }

            return bitmap;
        }

        /// <summary>
        /// Process weapon PAK sprites (56) into armor format (12 sheets)
        /// Each output sheet is 8 rows (directions) × N columns (frames)
        /// </summary>
        private void ProcessItemMode()
        {
            if (loadedSheets.Count == 0) return;

            // Shield mode has different processing (7 sprites → 12 sprites)
            if (itemMode == ItemModeType.Shield)
            {
                ProcessShieldMode();
                return;
            }

            // In PAK mode, each loadedSheet is one sprite with multiple rectangles (frames)
            // Weapon PAK: 56 sprites = 7 animations × 8 directions
            // Each sprite contains the frames for that animation+direction

            // Validate sprite count (should be 56 for weapons)
            if (loadedSheets.Count < 56)
            {
                MessageBox.Show($"Item mode requires 56 sprites (7 animations × 8 directions).\nFound only {loadedSheets.Count} sprites.",
                    "Invalid Sprite Count", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Clear existing packed sheets
            tabControl.TabPages.Clear();
            foreach (var packed in packedSheets)
                packed.Image?.Dispose();
            packedSheets.Clear();

            // Define mapping from armor index to weapon index
            // -1 means use blank sprite
            // Note: In peace mode attacks, weapons are HIDDEN (not rendered) - body uses fist animation
            int[] weaponMapping;
            if (itemMode == ItemModeType.Melee)
            {
                weaponMapping = new[]
                {
                    WeaponAnimIndex.StandingPeace,   // 0: Standing Peace
                    WeaponAnimIndex.StandingCombat,  // 1: Standing Combat
                    WeaponAnimIndex.WalkingPeace,    // 2: Walking Peace
                    WeaponAnimIndex.WalkingCombat,   // 3: Walking Combat
                    WeaponAnimIndex.Running,         // 4: Running
                    -1,                              // 5: Attack Peace (blank - weapon hidden in peace attacks)
                    WeaponAnimIndex.AttackMelee,     // 6: Attack Combat (melee)
                    -1,                              // 7: Attack Bow (blank for melee)
                    -1,                              // 8: Attack Cast (always blank)
                    -1,                              // 9: Pickup (always blank)
                    -1,                              // 10: Take Damage (always blank)
                    -1                               // 11: Dying (always blank)
                };
            }
            else // Bow
            {
                // Note: Bows store their attack animation at PAK index 4 (AttackMelee slot),
                // not index 5 (AttackBow slot). This is how original HB bow PAKs are structured.
                weaponMapping = new[]
                {
                    WeaponAnimIndex.StandingPeace,   // 0: Standing Peace
                    WeaponAnimIndex.StandingCombat,  // 1: Standing Combat
                    WeaponAnimIndex.WalkingPeace,    // 2: Walking Peace
                    WeaponAnimIndex.WalkingCombat,   // 3: Walking Combat
                    WeaponAnimIndex.Running,         // 4: Running
                    -1,                              // 5: Attack Peace (blank for bow)
                    -1,                              // 6: Attack Combat (blank for bow)
                    WeaponAnimIndex.AttackMelee,     // 7: Attack Bow - bows use melee slot in PAK
                    -1,                              // 8: Attack Cast (always blank)
                    -1,                              // 9: Pickup (always blank)
                    -1,                              // 10: Take Damage (always blank)
                    -1                               // 11: Dying (always blank)
                };
            }

            // Create blank sprite for unused slots
            using var blankSprite = CreateBlankSprite();

            // Process each of the 12 armor animation slots
            for (int armorIdx = 0; armorIdx < 12; armorIdx++)
            {
                int weaponIdx = weaponMapping[armorIdx];

                Bitmap sheetBitmap;
                var outputRects = new List<SpriteRectangle>();

                if (weaponIdx < 0)
                {
                    // Create blank sheet with single 100x100 sprite, no rectangles
                    sheetBitmap = new Bitmap(blankSprite);
                    // No rectangles for blank sprites
                }
                else
                {
                    // Get the 8 direction sprites for this weapon animation
                    // Weapon sprite index = animationIndex * 8 + direction
                    var directionSheets = new List<SpriteSheet>();
                    for (int dir = 0; dir < 8; dir++)
                    {
                        int spriteIdx = weaponIdx * 8 + dir;
                        if (spriteIdx < loadedSheets.Count)
                        {
                            directionSheets.Add(loadedSheets[spriteIdx]);
                        }
                    }

                    // Pack into 8 rows × N columns grid
                    (sheetBitmap, outputRects) = PackSpritesGrid8xN(directionSheets);
                }

                packedSheets.Add(new PackedSheet
                {
                    Image = sheetBitmap,
                    Rectangles = outputRects,
                    SpriteCount = outputRects.Count
                });

                // Create tab with RenderedPanel
                string tabName = $"{armorIdx}: {ArmorAnimNames[armorIdx]}";
                if (weaponIdx < 0) tabName += " (BLANK)";

                var tabPage = new TabPage(tabName);

                var renderedPanel = new RenderedPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(40, 40, 40),
                    ViewOnlyMode = true,
                    ShowHotkeyLegend = true,
                    CurrentBitmap = new Bitmap(sheetBitmap),
                    DirectRectangles = outputRects.Select(r => new Rectangle(r.x, r.y, r.width, r.height)).ToList()
                };
                renderedPanel.ZoomStatusLabel = zoomLabel;

                string formatStr = outputFormat == OutputFormat.BMP ? "BMP" : "PNG";
                string spriteInfo = weaponIdx < 0 ? "BLANK (no rectangles)" : $"Sprites: {outputRects.Count}";
                var infoLabel = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 25,
                    Text = $"Size: {sheetBitmap.Width}x{sheetBitmap.Height} | {spriteInfo} | Format: {formatStr}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = weaponIdx < 0 ? Color.FromArgb(80, 60, 60) : Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
                };

                tabPage.Controls.Add(renderedPanel);
                tabPage.Controls.Add(infoLabel);
                tabControl.TabPages.Add(tabPage);
            }

            string modeStr = itemMode == ItemModeType.Melee ? "Melee" : "Bow";
            statusLabel.Text = $"Item Mode ({modeStr}): Created 12 armor-format sheets from 56 weapon sprites";
            repackButton.Enabled = true;
            primaryButton.Enabled = true;
            secondaryButton.Enabled = true;
        }

        /// <summary>
        /// Shield PAK sprite indices (each has all frames: 8 dirs × N frames per dir)
        /// Original 3.82 loads indices 0-6 (7 sprites per shield)
        /// </summary>
        private static class ShieldAnimIndex
        {
            public const int StandingPeace = 0;
            public const int StandingCombat = 1;
            public const int WalkingPeace = 2;
            public const int WalkingCombat = 3;
            public const int AttackMelee = 4;
            public const int TakeDamage = 5;  // 4 frames per direction - used in OnDamage
            public const int Running = 6;
        }

        /// <summary>
        /// Process shield PAK sprites (7) into armor format (12 sheets)
        /// Unlike weapons, shields already have all frames in each sprite
        /// </summary>
        private void ProcessShieldMode()
        {
            if (loadedSheets.Count == 0) return;

            // Validate sprite count (should be 7 for shields)
            if (loadedSheets.Count < 7)
            {
                MessageBox.Show($"Shield mode requires 7 sprites.\nFound only {loadedSheets.Count} sprites.",
                    "Invalid Sprite Count", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Clear existing packed sheets
            tabControl.TabPages.Clear();
            foreach (var packed in packedSheets)
                packed.Image?.Dispose();
            packedSheets.Clear();

            // Define mapping from armor index to shield index
            // -1 means use blank sprite
            // Shield indices: 0=StandPeace, 1=StandCombat, 2=WalkPeace, 3=WalkCombat, 4=AttackMelee, 5=TakeDamage, 6=Running
            // Note: In peace mode attacks, weapons and shields are HIDDEN (not rendered)
            // Note: Shields cannot be equipped with bows, so Attack Bow is blank
            // Note: Shields not rendered during Pickup in 3.82
            int[] shieldMapping = new[]
            {
                ShieldAnimIndex.StandingPeace,   // 0: Standing Peace
                ShieldAnimIndex.StandingCombat,  // 1: Standing Combat
                ShieldAnimIndex.WalkingPeace,    // 2: Walking Peace
                ShieldAnimIndex.WalkingCombat,   // 3: Walking Combat
                ShieldAnimIndex.Running,         // 4: Running
                -1,                              // 5: Attack Peace (blank - shield hidden in peace attacks)
                ShieldAnimIndex.AttackMelee,     // 6: Attack Combat (melee)
                -1,                              // 7: Attack Bow (blank - shields not usable with bows)
                -1,                              // 8: Attack Cast (blank - not rendered in 3.82)
                -1,                              // 9: Pickup (blank - not rendered in 3.82)
                ShieldAnimIndex.TakeDamage,      // 10: Take Damage (sprite 5 - used in OnDamage)
                -1                               // 11: Dying (blank - not rendered in 3.82)
            };

            // Create blank sprite for unused slots
            using var blankSprite = CreateBlankSprite();

            // Process each of the 12 armor animation slots
            for (int armorIdx = 0; armorIdx < 12; armorIdx++)
            {
                int shieldIdx = shieldMapping[armorIdx];

                Bitmap sheetBitmap;
                var outputRects = new List<SpriteRectangle>();

                if (shieldIdx < 0)
                {
                    // Create blank sheet with single 100x100 sprite, no rectangles
                    sheetBitmap = new Bitmap(blankSprite);
                    // No rectangles for blank sprites
                }
                else
                {
                    // Get the shield sprite (already contains all direction frames)
                    var sourceSheet = loadedSheets[shieldIdx];

                    // Clone the sprite image and rectangles
                    sheetBitmap = new Bitmap(sourceSheet.Image);
                    outputRects = sourceSheet.Rectangles.Select(r => new SpriteRectangle
                    {
                        x = r.x,
                        y = r.y,
                        width = r.width,
                        height = r.height,
                        pivotX = r.pivotX,
                        pivotY = r.pivotY
                    }).ToList();
                }

                packedSheets.Add(new PackedSheet
                {
                    Image = sheetBitmap,
                    Rectangles = outputRects,
                    SpriteCount = outputRects.Count
                });

                // Create tab with RenderedPanel
                string tabName = $"{armorIdx}: {ArmorAnimNames[armorIdx]}";
                if (shieldIdx < 0) tabName += " (BLANK)";

                var tabPage = new TabPage(tabName);

                var renderedPanel = new RenderedPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(40, 40, 40),
                    ViewOnlyMode = true,
                    ShowHotkeyLegend = true,
                    CurrentBitmap = new Bitmap(sheetBitmap),
                    DirectRectangles = outputRects.Select(r => new Rectangle(r.x, r.y, r.width, r.height)).ToList()
                };
                renderedPanel.ZoomStatusLabel = zoomLabel;

                string formatStr = outputFormat == OutputFormat.BMP ? "BMP" : "PNG";
                string spriteInfo = shieldIdx < 0 ? "BLANK (no rectangles)" : $"Sprites: {outputRects.Count}";
                var infoLabel = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 25,
                    Text = $"Size: {sheetBitmap.Width}x{sheetBitmap.Height} | {spriteInfo} | Format: {formatStr}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = shieldIdx < 0 ? Color.FromArgb(80, 60, 60) : Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
                };

                tabPage.Controls.Add(renderedPanel);
                tabPage.Controls.Add(infoLabel);
                tabControl.TabPages.Add(tabPage);
            }

            statusLabel.Text = $"Item Mode (Shield): Created 12 armor-format sheets from 7 shield sprites";
            repackButton.Enabled = true;
            primaryButton.Enabled = true;
            secondaryButton.Enabled = true;
        }

        /// <summary>
        /// Pack sprites tightly - 8 directions stacked vertically, frames packed horizontally
        /// No column/row alignment - each frame is placed immediately after the previous
        /// </summary>
        private (Bitmap bitmap, List<SpriteRectangle> rectangles) PackSpritesGrid8xN(List<SpriteSheet> directionSheets)
        {
            if (directionSheets.Count == 0)
                return (new Bitmap(1, 1, GetPixelFormat()), new List<SpriteRectangle>());

            var outputRects = new List<SpriteRectangle>();

            // Calculate row heights and max width for each direction
            var rowHeights = new int[8];
            var rowWidths = new int[8];

            for (int dir = 0; dir < 8 && dir < directionSheets.Count; dir++)
            {
                var sheet = directionSheets[dir];
                int maxHeight = 0;
                int rowWidth = 0;

                for (int frame = 0; frame < sheet.Rectangles.Count; frame++)
                {
                    var rect = sheet.Rectangles[frame];
                    maxHeight = Math.Max(maxHeight, rect.height);
                    rowWidth += rect.width;
                    if (frame < sheet.Rectangles.Count - 1)
                        rowWidth += spacing;
                }

                rowHeights[dir] = maxHeight;
                rowWidths[dir] = rowWidth;
            }

            // Calculate total dimensions
            int totalWidth = rowWidths.Max();
            int totalHeight = rowHeights.Sum() + (7 * spacing); // 7 gaps between 8 rows

            if (totalWidth <= 0) totalWidth = 1;
            if (totalHeight <= 0) totalHeight = 1;

            var bitmap = new Bitmap(totalWidth, totalHeight, GetPixelFormat());
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(GetBackgroundColor());
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                int currentY = 0;

                // Process each direction (row)
                for (int dir = 0; dir < 8 && dir < directionSheets.Count; dir++)
                {
                    var sheet = directionSheets[dir];
                    int currentX = 0;

                    // Process each frame - pack tightly
                    for (int frame = 0; frame < sheet.Rectangles.Count; frame++)
                    {
                        var sourceRect = sheet.Rectangles[frame];

                        // Draw the frame
                        var sourceRegion = new Rectangle(sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height);
                        g.DrawImage(sheet.Image,
                            new Rectangle(currentX, currentY, sourceRect.width, sourceRect.height),
                            sourceRegion, GraphicsUnit.Pixel);

                        // Add rectangle for this frame
                        outputRects.Add(new SpriteRectangle
                        {
                            x = currentX,
                            y = currentY,
                            width = sourceRect.width,
                            height = sourceRect.height,
                            pivotX = sourceRect.pivotX,
                            pivotY = sourceRect.pivotY
                        });

                        currentX += sourceRect.width + spacing;
                    }

                    currentY += rowHeights[dir] + spacing;
                }
            }

            return (bitmap, outputRects);
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
            // Check if item mode is enabled
            if (itemMode != ItemModeType.None)
            {
                ProcessItemMode();
                return;
            }

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
            if(tertiaryButton != null)
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