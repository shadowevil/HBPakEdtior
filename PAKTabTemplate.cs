using Newtonsoft.Json;
using PAKLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;

namespace HBPakEditor
{
    public class PAKTabTemplate : PAKTabPage
    {
        MainWindow mainWindow = null!;

        private UndoRedoManager _undoManager = new();
        public UndoRedoManager UndoManager => _undoManager;

        // Left Panel
        private SplitContainer _mainSplitContainer;
        private TreeView _itemTreeView;

        // Right Top Panel
        private RenderedPanel _topPanel;
        private MenuStrip _topMenuStrip;
        private StatusStrip _topStatusStrip;
        private ToolStripStatusLabel _zoomStatusLabel;
        private ToolStripStatusLabel _dimensionsStatusLabel;
        private ToolStripStatusLabel _rectangleDimensionsStatusLabel;
        private ToolStripStatusLabel _imageInfoStatusLabel;

        // Right Bottom Panel
        private SplitContainer _rightSplitContainer;
        private Panel _bottomPanel;
        private TableLayoutPanel _propertiesTableLayout;

        // Property inputs
        private Label _xLabel;
        private NumericTextBox<Int16> _xTextBox;
        private Label _yLabel;
        private NumericTextBox<Int16> _yTextBox;
        private Label _widthLabel;
        private NumericTextBox<Int16> _widthTextBox;
        private Label _heightLabel;
        private NumericTextBox<Int16> _heightTextBox;
        private Label _pivotXLabel;
        private NumericTextBox<Int16> _pivotXTextBox;
        private Label _pivotYLabel;
        private NumericTextBox<Int16> _pivotYTextBox;

        // Selection tracking
        private SpriteReference? _selectedItem;

        // Auto-rectangle color picker state
        private SpriteReference? _pendingAutoRectangleSprite;

        private float _savedZoomLevel = 1.0f;
        private PointF _savedPanOffset = PointF.Empty;

        // Properties
        public Int16 X
        {
            get => _xTextBox.GetValue();
            set => _xTextBox.SetValue(value);
        }

        public Int16 Y
        {
            get => _yTextBox.GetValue();
            set => _yTextBox.SetValue(value);
        }

        public Int16 Width
        {
            get => _widthTextBox.GetValue();
            set => _widthTextBox.SetValue(value);
        }

        public Int16 Height
        {
            get => _heightTextBox.GetValue();
            set => _heightTextBox.SetValue(value);
        }

        public Int16 OffsetX
        {
            get => _pivotXTextBox.GetValue();
            set => _pivotXTextBox.SetValue(value);
        }

        public Int16 OffsetY
        {
            get => _pivotYTextBox.GetValue();
            set => _pivotYTextBox.SetValue(value);
        }

        public SpriteReference? SelectedItem
        {
            get => _selectedItem;
            private set => _selectedItem = value;
        }

        public PAKTabTemplate(MainWindow mainWindow)
        {
            Text = "Template";
            InitializeComponents();
            MainWindow.EnableDoubleBuffering(this);
            this.mainWindow = mainWindow;
        }

        private void InitializeComponents()
        {
            SuspendLayout();

            // Main split container (left tree | right content)
            _mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 150 // Start at minimum
            };
            _mainSplitContainer.Panel1MinSize = 150;
            _mainSplitContainer.Panel2MinSize = 100;
            _mainSplitContainer.SplitterMoved += OnMainSplitterMoved;

            // Left panel - TreeView
            _itemTreeView = new TreeView
            {
                Dock = DockStyle.Fill
            };
            _itemTreeView.AfterSelect += OnTreeViewItemSelected;
            //_itemTreeView.NodeMouseClick += OnTreeViewNodeMouseClick;
            _itemTreeView.MouseUp += OnTreeViewMouseClick;
            //_itemTreeView.KeyPress += OnItemViewKeyPress;
            _itemTreeView.KeyUp += OnItemViewKeyUp;
            _mainSplitContainer.Panel1.Controls.Add(_itemTreeView);

            // Right split container (top viewport | bottom properties)
            _rightSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2
            };

            _rightSplitContainer.Panel1MinSize = 200;
            _rightSplitContainer.Panel2MinSize = 100;
            _rightSplitContainer.SplitterMoved += OnRightSplitterMoved;

            // Top panel with MenuStrip and StatusStrip
            _topPanel = new RenderedPanel
            {
                Dock = DockStyle.Fill
            };

            _topPanel.OnRectangleDrawn = OnRectangleDrawnHandler;
            _topPanel.OnRectangleClicked = OnRectangleClickedHandler;
            _topPanel.OnColorPicked = OnColorPickedHandler;
            _topPanel.OnColorPickerCancelled = OnColorPickerCancelledHandler;
            _topPanel.OnDeleteKeyPressed = OnDeleteKeyPressedHandler;

            _topMenuStrip = new MenuStrip
            {
                ForeColor = Color.White,
                Dock = DockStyle.Top
            };

            _topStatusStrip = new StatusStrip
            {
                ForeColor = Color.White,
                Dock = DockStyle.Bottom,
                SizingGrip = false
            };

            _zoomStatusLabel = new ToolStripStatusLabel("100%");
            _dimensionsStatusLabel = new ToolStripStatusLabel("0x0");
            _rectangleDimensionsStatusLabel = new ToolStripStatusLabel("0x0");
            _imageInfoStatusLabel = new ToolStripStatusLabel("Unknown");

            _topStatusStrip.Items.Add(_zoomStatusLabel);
            _topStatusStrip.Items.Add(new ToolStripStatusLabel("|"));
            _topStatusStrip.Items.Add(_dimensionsStatusLabel);
            _topStatusStrip.Items.Add(new ToolStripStatusLabel("|"));
            _topStatusStrip.Items.Add(_imageInfoStatusLabel);
            _topStatusStrip.Items.Add(new ToolStripStatusLabel("|"));
            _topStatusStrip.Items.Add(_rectangleDimensionsStatusLabel);

            _topPanel.ZoomStatusLabel = _zoomStatusLabel;
            _topPanel.DimensionsStatusLabel = _dimensionsStatusLabel;
            _topPanel.RectangleDimensionsStatusLabel = _rectangleDimensionsStatusLabel;
            _topPanel.ImageInfoStatusLabel = _imageInfoStatusLabel;

            _topPanel.Controls.Add(_topStatusStrip);
            _topPanel.Controls.Add(_topMenuStrip);

            _rightSplitContainer.Panel1.Controls.Add(_topPanel);

            // Bottom panel - Properties
            _bottomPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Create labels and textboxes
            _xLabel = CreateLabel("X:");
            _xTextBox = new NumericTextBox<Int16> { AllowNegative = true, TabIndex = 0 };
            _xTextBox.LostFocus += OnNumeric_FocusLost;

            _yLabel = CreateLabel("Y:");
            _yTextBox = new NumericTextBox<Int16> { AllowNegative = true, TabIndex = 1 };
            _yTextBox.LostFocus += OnNumeric_FocusLost;

            _widthLabel = CreateLabel("Width:");
            _widthTextBox = new NumericTextBox<Int16> { AllowNegative = true, TabIndex = 2 };
            _widthTextBox.LostFocus += OnNumeric_FocusLost;

            _heightLabel = CreateLabel("Height:");
            _heightTextBox = new NumericTextBox<Int16> { AllowNegative = true, TabIndex = 3 };
            _heightTextBox.LostFocus += OnNumeric_FocusLost;

            _pivotXLabel = CreateLabel("Pivot X:");
            _pivotXTextBox = new NumericTextBox<Int16> { AllowNegative = true, TabIndex = 4 };
            _pivotXTextBox.LostFocus += OnNumeric_FocusLost;

            _pivotYLabel = CreateLabel("Pivot Y:");
            _pivotYTextBox = new NumericTextBox<Int16> { AllowNegative = true, TabIndex = 5 };
            _pivotYTextBox.LostFocus += OnNumeric_FocusLost;

            _bottomPanel.Controls.Add(_xLabel);
            _bottomPanel.Controls.Add(_xTextBox);
            _bottomPanel.Controls.Add(_yLabel);
            _bottomPanel.Controls.Add(_yTextBox);
            _bottomPanel.Controls.Add(_widthLabel);
            _bottomPanel.Controls.Add(_widthTextBox);
            _bottomPanel.Controls.Add(_heightLabel);
            _bottomPanel.Controls.Add(_heightTextBox);
            _bottomPanel.Controls.Add(_pivotXLabel);
            _bottomPanel.Controls.Add(_pivotXTextBox);
            _bottomPanel.Controls.Add(_pivotYLabel);
            _bottomPanel.Controls.Add(_pivotYTextBox);

            _bottomPanel.Resize += OnBottomPanelResize;
            OnBottomPanelResize(_bottomPanel, EventArgs.Empty);

            _rightSplitContainer.Panel2.Controls.Add(_bottomPanel);

            // Assemble hierarchy
            _mainSplitContainer.Panel2.Controls.Add(_rightSplitContainer);
            Controls.Add(_mainSplitContainer);

            ResumeLayout(false);
            PerformLayout();
        }

        private void OnItemViewKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (_itemTreeView.SelectedNode != null && _itemTreeView.SelectedNode.Tag is SpriteReference reference)
                {
                    if (reference.RectangleIndex == -1)
                    {
                        if (MessageBox.Show("Are you sure you want to delete this sprite?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            int spriteIndexToSelect = reference.SpriteIndex > 0 ? reference.SpriteIndex - 1 : (OpenPAK?.Data?.Sprites.Count ?? 1) > 1 ? 0 : -1;
                            OnDeleteSprite(reference);
                            PopulateTreeItems();
                            if (spriteIndexToSelect != -1 && _itemTreeView.Nodes.Count >= spriteIndexToSelect)
                            {
                                _itemTreeView.SelectedNode = _itemTreeView.Nodes[spriteIndexToSelect];
                            }
                        }
                    }
                    else
                    {
                        if (MessageBox.Show("Are you sure you want to delete this rectangle?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            int spriteIndexToSelect = reference.SpriteIndex;
                            int rectangleIndexToSelect = reference.RectangleIndex > 0 ? reference.RectangleIndex - 1 : (OpenPAK?.Data?.Sprites[reference.SpriteIndex].Rectangles.Count ?? 1) > 1 ? 0 : -1;
                            OnDeleteRectangle(reference);
                            PopulateTreeItems();
                            if (rectangleIndexToSelect != -1 && _itemTreeView.Nodes[spriteIndexToSelect].Nodes.Count >= rectangleIndexToSelect)
                            {
                                _itemTreeView.SelectedNode = _itemTreeView.Nodes[spriteIndexToSelect].Nodes[rectangleIndexToSelect];
                            }
                        }
                    }
                }
            }
        }

        private void OnRectangleClickedHandler(SpriteReference rectangleRef, MouseButtons button, Point imageSpacePoint)
        {
            if (button == MouseButtons.Right || button == MouseButtons.Left)
            {
                // Select the rectangle in the tree
                TreeNode? spriteNode = FindSpriteNode(rectangleRef.SpriteIndex);
                if (spriteNode != null && rectangleRef.RectangleIndex < spriteNode.Nodes.Count)
                {
                    _itemTreeView.SelectedNode = spriteNode.Nodes[rectangleRef.RectangleIndex];
                    CaptureCurrentRectangle();
                } else
                {
                    _rectangleBeforeEdit = null;
                }
            }
        }

        private void OnDeleteKeyPressedHandler()
        {
            // Delete the currently selected rectangle (if a rectangle is selected)
            if (_selectedItem == null || _selectedItem.Value.RectangleIndex == -1)
                return;

            var reference = _selectedItem.Value;
            if (MessageBox.Show("Are you sure you want to delete this rectangle?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                int spriteIndexToSelect = reference.SpriteIndex;
                int rectangleIndexToSelect = reference.RectangleIndex > 0
                    ? reference.RectangleIndex - 1
                    : (OpenPAK?.Data?.Sprites[reference.SpriteIndex].Rectangles.Count ?? 1) > 1 ? 0 : -1;

                OnDeleteRectangle(reference);
                PopulateTreeItems();

                if (rectangleIndexToSelect != -1 && _itemTreeView.Nodes.Count > spriteIndexToSelect &&
                    _itemTreeView.Nodes[spriteIndexToSelect].Nodes.Count > rectangleIndexToSelect)
                {
                    _itemTreeView.SelectedNode = _itemTreeView.Nodes[spriteIndexToSelect].Nodes[rectangleIndexToSelect];
                }
                else if (_itemTreeView.Nodes.Count > spriteIndexToSelect)
                {
                    _itemTreeView.SelectedNode = _itemTreeView.Nodes[spriteIndexToSelect];
                }
            }
        }

        private bool OnRectangleDrawnHandler(Rectangle drawnRectangle)
        {
            if (OpenPAK?.Data != null && _selectedItem != null && _selectedItem.Value.SpriteIndex != -1)
            {
                var sprite = OpenPAK.Data.Sprites[_selectedItem.Value.SpriteIndex];
                if (sprite != null)
                {
                    int currentSpriteIndex = _selectedItem.Value.SpriteIndex;

                    _savedZoomLevel = _topPanel.ZoomLevel;
                    _savedPanOffset = _topPanel.PanOffset;

                    var newRect = new PAKLib.SpriteRectangle
                    {
                        x = (short)drawnRectangle.X,
                        y = (short)drawnRectangle.Y,
                        width = (short)drawnRectangle.Width,
                        height = (short)drawnRectangle.Height,
                        pivotX = 0,
                        pivotY = 0
                    };

                    var cmd = new AddRectangleCommand(OpenPAK, currentSpriteIndex, newRect,
                        onExecute: () =>
                        {
                            PopulateTreeItems();
                            int newRectIndex = OpenPAK.Data.Sprites[currentSpriteIndex].Rectangles.Count - 1;
                            TreeNode? spriteNode = FindSpriteNode(currentSpriteIndex);
                            if (spriteNode != null && newRectIndex >= 0 && newRectIndex < spriteNode.Nodes.Count)
                            {
                                spriteNode.Expand();
                                _itemTreeView.SelectedNode = spriteNode.Nodes[newRectIndex];
                            }
                            _topPanel.ZoomLevel = _savedZoomLevel;
                            _topPanel.PanOffset = _savedPanOffset;
                            _topPanel.Invalidate();
                            MarkTabDirty();
                        },
                        onUndo: () =>
                        {
                            PopulateTreeItems();
                            TreeNode? spriteNode = FindSpriteNode(currentSpriteIndex);
                            if (spriteNode != null)
                            {
                                spriteNode.Expand();
                                _itemTreeView.SelectedNode = spriteNode;
                            }
                            _topPanel.ZoomLevel = _savedZoomLevel;
                            _topPanel.PanOffset = _savedPanOffset;
                            _topPanel.Invalidate();
                            MarkTabDirty();
                        }
                    );

                    _undoManager.Execute(cmd);
                    return true;
                }
            }
            return false;
        }

        private void OnTreeViewMouseClick(object? sender, MouseEventArgs e)
        {
            if (this is PAKTabEmpty)
                return;

            if (e.Button == MouseButtons.Right)
            {
                var node = _itemTreeView.HitTest(e.Location).Node;
                if (node != null)
                {
                    OnTreeViewNodeMouseClick(sender, new TreeNodeMouseClickEventArgs(node, e.Button, e.Clicks, e.X, e.Y));
                    return;
                }

                ContextMenuStrip menu = CreateContextMenuForNode(null);
                menu.Show(_itemTreeView, e.Location);
            }
        }

        private void OnTreeViewNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _itemTreeView.SelectedNode = e.Node;

                if (e.Node?.Tag is SpriteReference reference)
                {
                    ContextMenuStrip menu = CreateContextMenuForNode(reference);
                    menu.Show(_itemTreeView, e.Location);
                }
            }
        }

        private ContextMenuStrip CreateContextMenuForNode(SpriteReference? reference)
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            if (reference != null)
            {
                if (reference.Value.RectangleIndex == -1)
                {
                    // Sprite node
                    menu.Items.Add("Add Rectangle", null, (s, e) => OnAddRectangle(reference.Value));

                    // Show Auto-Rectangle and Auto-Grid Rectangles for PNG and BMP files
                    if (OpenPAK?.Data != null && reference.Value.SpriteIndex >= 0 &&
                        reference.Value.SpriteIndex < OpenPAK.Data.Sprites.Count)
                    {
                        var spriteData = OpenPAK.Data.Sprites[reference.Value.SpriteIndex].data;
                        if (FileSignatureDetector.IsFileType(spriteData, "PNG") ||
                            FileSignatureDetector.IsFileType(spriteData, "BMP"))
                        {
                            menu.Items.Add("Auto-Rectangle", null, (s, e) => OnAutoRectangle(reference.Value));
                            menu.Items.Add("Auto-Grid Rectangles", null, (s, e) => OnAutoGridRectangles(reference.Value));
                        }
                    }

                    menu.Items.Add("Clear Rectangles", null, (s, e) => OnClearRectangles(reference.Value));
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add("Replace Sprite", null, (s, e) => OnReplaceSprite(reference.Value));
                    menu.Items.Add("Export Sprite", null, (s, e) => OnExportSprite(reference.Value));
                    menu.Items.Add("Export Rectangles", null, (s, e) => OnExportRectangles(reference.Value));
                    menu.Items.Add("Import Rectangles", null, (s, e) => OnImportRectangles(reference.Value));
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add("Delete Sprite", null, (s, e) => OnDeleteSprite(reference.Value));
                }
                else
                {
                    // Rectangle node menu
                    menu.Items.Add("Delete Rectangle", null, (s, e) => OnDeleteRectangle(reference.Value));
                }
            }
            else
            {
                menu.Items.Add("Add New Sprite", null, (s, e) => OnAddNewSprite());
                menu.Items.Add("Import New Sprite", null, (s, e) => OnImportNewSprite());
            }
            return menu;
        }

        private void OnImportRectangles(SpriteReference reference)
        {   
            if (OpenPAK == null)
                return;

            if (OpenPAK?.Data != null)
            {
                //var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "JSON File|*.json|All Files|*.*";
                    ofd.Title = "Import Rectangles";
                    ofd.Multiselect = true;

                    if(ofd.ShowDialog() != DialogResult.OK)
                        return;

                    List<string> json_imports = new List<string>();
                    List<SpriteRectangle[]> json = new List<SpriteRectangle[]>();
                    foreach(var filepath in ofd.FileNames)
                    {
                        json_imports.Add(File.ReadAllText(filepath));
                        SpriteRectangle[]? rects = JsonConvert.DeserializeObject<SpriteRectangle[]>(json_imports.Last());
                        if(rects == null)
                            throw new Exception("Failed to import rectangles from JSON file.");

                        json.Add(rects);
                    }

                    var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
                    foreach(var rects in json)
                    {
                        sprite.Rectangles.AddRange(rects);
                    }

                    PopulateTreeItems();
                    MarkTabDirty();
                }
            }
        }

        private void OnImportNewSprite()
        {
            if (OpenPAK == null)
                return;

            if (OpenPAK?.Data != null)
            {
                var result = MessageBox.Show("Import sprite rectangles along with images?", "Import Options", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel)
                    return;
                bool ImportRectangles = result == DialogResult.Yes;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Image Files|*.png;*.bmp";
                    ofd.Title = "Import New Sprite";
                    ofd.Multiselect = false;

                    if (ofd.ShowDialog() != DialogResult.OK)
                        return;

                    string directory = Path.GetDirectoryName(ofd.FileName) ?? "";
                    string expected_sprite_name = Path.GetFileName(ofd.FileName);

                    var sprite_path = Path.Combine(directory, expected_sprite_name);
                    PAKLib.Sprite spr = new();
                    spr.data = File.ReadAllBytes(sprite_path);
                    List<PAKLib.SpriteRectangle>? rects = null;

                    if (ImportRectangles)
                    {
                        string expected_rectangles_name = Path.ChangeExtension(expected_sprite_name, ".json").Replace("_sprite_", "_rectangles_");
                        var rectangles_path = Path.Combine(directory, expected_rectangles_name);
                        rects = JsonConvert.DeserializeObject<List<PAKLib.SpriteRectangle>>(File.ReadAllText(rectangles_path));
                        if (rects == null)
                        {
                            MessageBox.Show("Failed to import rectangles from JSON file.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                    spr.Rectangles = rects ?? new();
                    OpenPAK.Data.Sprites.Add(spr);

                    PopulateTreeItems();
                    MarkTabDirty();
                }
            }
        }

        private void OnAddNewSprite()
        {
            if (this is PAKTabEmpty)
                return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.png;*.bmp|All Files|*.*";
                openFileDialog.Title = "Import New Sprite";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    byte[] imageData = File.ReadAllBytes(openFileDialog.FileName);
                    PAKLib.Sprite newSprite = new PAKLib.Sprite
                    {
                        data = imageData,
                        Rectangles = new List<PAKLib.SpriteRectangle>()
                    };
                    OpenPAK?.Data?.Sprites.Add(newSprite);
                    PopulateTreeItems();
                    MarkTabDirty();
                }
            }
        }

        private void OnExportRectangles(SpriteReference reference)
        {
            if (OpenPAK?.Data != null && reference.SpriteIndex != -1)
            {
                var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
                var pakname = Path.GetFileNameWithoutExtension(FilePath ?? "new");
                if (sprite != null)
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "JSON File|*.json|All Files|*.*";
                        sfd.Title = "Export Sprite Rectangles";
                        sfd.FileName = $"{pakname}_rectangles_{reference.SpriteIndex}.json";
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            string json = JsonConvert.SerializeObject(sprite.Rectangles, Formatting.Indented);
                            File.WriteAllText(sfd.FileName, json);
                        }
                    }
                }
            }
        }

        private RenamableTabControl<PAKTabPage>? GetParentTabControl()
        {
            if (this.Parent is RenamableTabControl<PAKTabPage> tabControl)
                return tabControl;
            return null;
        }

        private void MarkTabDirty()
        {
            GetParentTabControl()?.SetTabDirty(this, true);
        }

        private void OnReplaceSprite(SpriteReference reference)
        {
            if (OpenPAK?.Data != null && reference.SpriteIndex != -1)
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Image Files|*.png;*.bmp|All Files|*.*";
                    ofd.Title = "Replace Sprite Image";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        byte[] imageData = File.ReadAllBytes(ofd.FileName);
                        string extension = Path.GetExtension(ofd.FileName).TrimStart('.').ToUpper();
                        byte[] converted = ImageConverter.Convert<byte[]>(imageData, extension);
                        OpenPAK.Data.Sprites[reference.SpriteIndex].data = converted;
                        // Refresh current view if this sprite is selected
                        if (_selectedItem != null && _selectedItem.Value.SpriteIndex == reference.SpriteIndex)
                        {
                            using (MemoryStream ms = new MemoryStream(imageData))
                            {
                                _topPanel.CurrentBitmap = new Bitmap(ms);
                            }
                            MarkTabDirty();
                        }
                    }
                }
            }
        }

        private void OnExportSprite(SpriteReference reference)
        {
            if (OpenPAK?.Data != null && reference.SpriteIndex != -1)
            {
                var result = MessageBox.Show("Export sprite rectangles along with image?", "Export Options", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if(result == DialogResult.Cancel)
                    return;
                bool exportRectangles = result == DialogResult.Yes;
                var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
                SpriteRectangle[]? rectangles = null;
                if(exportRectangles)
                    rectangles = (OpenPAK.Data.Sprites[reference.SpriteIndex].Rectangles ?? new List<SpriteRectangle>()).ToArray();
                var pakname = Path.GetFileNameWithoutExtension(FilePath ?? "new");
                if (sprite != null)
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "PNG Image|*.png|Bitmap Image|*.bmp";
                        sfd.Title = "Export Sprite Image";
                        sfd.AddExtension = true;
                        sfd.FileName = $"{pakname}_sprite_{reference.SpriteIndex}";
                        string? rectangleFileName = null;
                        if (exportRectangles)
                            rectangleFileName = $"{pakname}_rectangles_{reference.SpriteIndex}.json";
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            var signature = FileSignatureDetector.DetectFileType(sprite.data);

                            if (signature is not ("PNG" or "BMP"))
                                throw new Exception($"Unsupported image format: {signature ?? "unknown"}. Only PNG and BMP are supported.");

                            var targetFormat = Path.GetExtension(sfd.FileName).TrimStart('.').ToUpperInvariant();

                            var data = targetFormat == signature
                                ? sprite.data
                                : ImageConverter.Convert<byte[]>(sprite.data, targetFormat);

                            File.WriteAllBytes(sfd.FileName, data);
                            if (exportRectangles && rectangleFileName != null)
                            {
                                string json = JsonConvert.SerializeObject(rectangles, Formatting.Indented);
                                var rectangleFilePath = Path.Combine(Path.GetDirectoryName(sfd.FileName) ?? "", rectangleFileName);
                                File.WriteAllText(rectangleFilePath, json);
                            }
                        }
                    }
                }
            }
        }

        private void OnAddRectangle(SpriteReference reference)
        {
            var rect = new PAKLib.SpriteRectangle { x = 0, y = 0, width = 0, height = 0, pivotX = 0, pivotY = 0 };
            var cmd = new AddRectangleCommand(OpenPAK, reference.SpriteIndex, rect, () =>
            {
                PopulateTreeItems();
                MarkTabDirty();
            });
            _undoManager.Execute(cmd);
        }

        private void OnAutoRectangle(SpriteReference reference)
        {
            if (OpenPAK?.Data == null || reference.SpriteIndex < 0)
                return;

            var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
            if (sprite == null)
                return;

            // Store the pending sprite reference and enter color picker mode
            _pendingAutoRectangleSprite = reference;
            _topPanel.EnterColorPickerMode();
        }

        private void OnColorPickedHandler(Color pickedColor)
        {
            if (_pendingAutoRectangleSprite == null || OpenPAK?.Data == null)
                return;

            var reference = _pendingAutoRectangleSprite.Value;
            _pendingAutoRectangleSprite = null;

            var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
            if (sprite == null)
                return;

            // Determine if this is a PNG or BMP
            bool isPng = FileSignatureDetector.IsFileType(sprite.data, "PNG");

            // Load the sprite as a bitmap
            using var ms = new MemoryStream(sprite.data);
            using var bitmap = new Bitmap(ms);

            // Find all contiguous non-transparent regions using the picked color as transparency key
            var detectedRectangles = FindContiguousRegions(bitmap, pickedColor, isPng);

            if (detectedRectangles.Count == 0)
            {
                MessageBox.Show("No non-transparent regions found in the sprite.", "Auto-Rectangle",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Execute the auto-rectangle command
            int spriteIndex = reference.SpriteIndex;
            var cmd = new AutoRectangleCommand(OpenPAK, spriteIndex, detectedRectangles, () =>
            {
                PopulateTreeItems();
                MarkTabDirty();

                // Select the sprite node to refresh the RenderedPanel
                TreeNode? spriteNode = FindSpriteNode(spriteIndex);
                if (spriteNode != null)
                {
                    _itemTreeView.SelectedNode = spriteNode;
                }
            });
            _undoManager.Execute(cmd);
        }

        private void OnColorPickerCancelledHandler()
        {
            _pendingAutoRectangleSprite = null;
        }

        private void OnAutoGridRectangles(SpriteReference reference)
        {
            if (OpenPAK?.Data == null || reference.SpriteIndex < 0)
                return;

            var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
            if (sprite == null)
                return;

            // Get image dimensions
            using var ms = new MemoryStream(sprite.data);
            using var bitmap = new Bitmap(ms);
            int imageWidth = bitmap.Width;
            int imageHeight = bitmap.Height;

            // Show input dialog for cell size
            var config = new InputBoxConfiguration
            {
                Required = true,
                DefaultValue = "32",
                MinLength = 1
            };

            var result = InputBox.Show("Enter grid cell size (width and height):", "Auto-Grid Rectangles", out string input, config);
            if (result != DialogResult.OK)
                return;

            if (!int.TryParse(input, out int cellSize) || cellSize < 3)
            {
                MessageBox.Show("Cell size must be a number of at least 3.", "Invalid Input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Generate grid rectangles
            var gridRectangles = GenerateGridRectangles(imageWidth, imageHeight, cellSize);

            if (gridRectangles.Count == 0)
            {
                MessageBox.Show("No rectangles could be generated with the specified cell size.", "Auto-Grid Rectangles",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Execute the auto-rectangle command (reuse the same command class)
            int spriteIndex = reference.SpriteIndex;
            var cmd = new AutoRectangleCommand(OpenPAK, spriteIndex, gridRectangles, () =>
            {
                PopulateTreeItems();
                MarkTabDirty();

                // Select the sprite node to refresh the RenderedPanel
                TreeNode? spriteNode = FindSpriteNode(spriteIndex);
                if (spriteNode != null)
                {
                    _itemTreeView.SelectedNode = spriteNode;
                }
            });
            _undoManager.Execute(cmd);
        }

        private List<SpriteRectangle> GenerateGridRectangles(int imageWidth, int imageHeight, int cellSize)
        {
            var rectangles = new List<SpriteRectangle>();
            const int MIN_SIZE_THRESHOLD = 3;

            for (int y = 0; y < imageHeight; y += cellSize)
            {
                for (int x = 0; x < imageWidth; x += cellSize)
                {
                    // Calculate the actual width and height, clipping to image bounds
                    int rectWidth = Math.Min(cellSize, imageWidth - x);
                    int rectHeight = Math.Min(cellSize, imageHeight - y);

                    // Skip if either dimension is below the threshold
                    if (rectWidth < MIN_SIZE_THRESHOLD || rectHeight < MIN_SIZE_THRESHOLD)
                        continue;

                    rectangles.Add(new SpriteRectangle
                    {
                        x = (short)x,
                        y = (short)y,
                        width = (short)rectWidth,
                        height = (short)rectHeight,
                        pivotX = 0,
                        pivotY = 0
                    });
                }
            }

            return rectangles;
        }

        private void OnClearRectangles(SpriteReference reference)
        {
            if (OpenPAK?.Data == null || reference.SpriteIndex < 0)
                return;

            var sprite = OpenPAK.Data.Sprites[reference.SpriteIndex];
            if (sprite == null || sprite.Rectangles.Count == 0)
                return;

            // Use AutoRectangleCommand with an empty list to clear rectangles (supports undo)
            int spriteIndex = reference.SpriteIndex;
            var cmd = new AutoRectangleCommand(OpenPAK, spriteIndex, new List<SpriteRectangle>(), () =>
            {
                PopulateTreeItems();
                MarkTabDirty();

                // Select the sprite node to refresh the RenderedPanel
                TreeNode? spriteNode = FindSpriteNode(spriteIndex);
                if (spriteNode != null)
                {
                    _itemTreeView.SelectedNode = spriteNode;
                }
            });
            _undoManager.Execute(cmd);
        }

        private List<SpriteRectangle> FindContiguousRegions(Bitmap bitmap, Color transparencyKey, bool useAlpha)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            var visited = new bool[width, height];
            var regions = new List<SpriteRectangle>();

            // Lock bits for fast pixel access
            var rect = new Rectangle(0, 0, width, height);
            var bitmapData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                int bytesPerPixel = 4;
                int stride = bitmapData.Stride;
                var pixels = new byte[stride * height];
                System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);

                byte keyB = transparencyKey.B;
                byte keyG = transparencyKey.G;
                byte keyR = transparencyKey.R;
                byte keyA = transparencyKey.A;

                // Scan for non-transparent pixels and flood-fill to find regions
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (visited[x, y])
                            continue;

                        int pixelIndex = y * stride + x * bytesPerPixel;
                        byte b = pixels[pixelIndex];
                        byte g = pixels[pixelIndex + 1];
                        byte r = pixels[pixelIndex + 2];
                        byte a = pixels[pixelIndex + 3];

                        // Check if this pixel matches the transparency key
                        bool isTransparent;
                        if (useAlpha)
                        {
                            // PNG: compare full ARGB
                            isTransparent = (r == keyR && g == keyG && b == keyB && a == keyA);
                        }
                        else
                        {
                            // BMP: compare only RGB, ignore alpha
                            isTransparent = (r == keyR && g == keyG && b == keyB);
                        }

                        if (isTransparent)
                        {
                            visited[x, y] = true;
                            continue;
                        }

                        // Found a non-transparent pixel that hasn't been visited
                        // Flood-fill to find the entire contiguous region
                        var bounds = FloodFillRegion(pixels, visited, width, height, stride, bytesPerPixel,
                            x, y, keyR, keyG, keyB, keyA, useAlpha);
                        if (bounds.Width > 0 && bounds.Height > 0)
                        {
                            regions.Add(new SpriteRectangle
                            {
                                x = (short)bounds.X,
                                y = (short)bounds.Y,
                                width = (short)bounds.Width,
                                height = (short)bounds.Height,
                                pivotX = 0,
                                pivotY = 0
                            });
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return regions;
        }

        private Rectangle FloodFillRegion(byte[] pixels, bool[,] visited, int width, int height,
            int stride, int bytesPerPixel, int startX, int startY,
            byte keyR, byte keyG, byte keyB, byte keyA, bool useAlpha)
        {
            int minX = startX, maxX = startX;
            int minY = startY, maxY = startY;

            var stack = new Stack<(int x, int y)>();
            stack.Push((startX, startY));

            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();

                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                if (visited[x, y])
                    continue;

                int pixelIndex = y * stride + x * bytesPerPixel;
                byte b = pixels[pixelIndex];
                byte g = pixels[pixelIndex + 1];
                byte r = pixels[pixelIndex + 2];
                byte a = pixels[pixelIndex + 3];

                // Check if this pixel matches the transparency key
                bool isTransparent;
                if (useAlpha)
                {
                    // PNG: compare full ARGB
                    isTransparent = (r == keyR && g == keyG && b == keyB && a == keyA);
                }
                else
                {
                    // BMP: compare only RGB, ignore alpha
                    isTransparent = (r == keyR && g == keyG && b == keyB);
                }

                if (isTransparent)
                {
                    visited[x, y] = true;
                    continue;
                }

                visited[x, y] = true;

                // Update bounding box
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);

                // Add neighbors (4-connected)
                stack.Push((x + 1, y));
                stack.Push((x - 1, y));
                stack.Push((x, y + 1));
                stack.Push((x, y - 1));
            }

            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void OnDeleteSprite(SpriteReference reference)
        {
            OpenPAK?.Data?.Sprites.RemoveAt(reference.SpriteIndex);
            PopulateTreeItems();
            _itemTreeView.SelectedNode = null;
            _topPanel.CurrentBitmap = null;
            MarkTabDirty();
        }

        private void OnDeleteRectangle(SpriteReference reference)
        {
            var cmd = new DeleteRectangleCommand(OpenPAK, reference.SpriteIndex, reference.RectangleIndex, () =>
            {
                PopulateTreeItems();
                MarkTabDirty();
            });
            _undoManager.Execute(cmd);
        }

        // Modify OnNumeric_FocusLost (track old value):
        private SpriteRectangle? _rectangleBeforeEdit;

        // Call this when selection changes to capture old state:
        private void CaptureCurrentRectangle()
        {
            if (_selectedItem != null && OpenPAK?.Data != null &&
                _selectedItem.Value.RectangleIndex >= 0)
            {
                _rectangleBeforeEdit = OpenPAK.Data.Sprites[_selectedItem.Value.SpriteIndex]
                    .Rectangles[_selectedItem.Value.RectangleIndex];
            }
        }

        private void OnNumeric_FocusLost(object? sender, EventArgs e)
        {
            if (_selectedItem == null || OpenPAK?.Data == null || _rectangleBeforeEdit == null)
                return;

            var newRect = new SpriteRectangle { x = X, y = Y, width = Width, height = Height, pivotX = OffsetX, pivotY = OffsetY };

            // Skip if no change
            if (_rectangleBeforeEdit.Equals(newRect))
                return;

            var cmd = new ModifyRectangleCommand(OpenPAK, _selectedItem.Value.SpriteIndex,
                _selectedItem.Value.RectangleIndex, _rectangleBeforeEdit, newRect, () =>
                {
                    _topPanel.Invalidate();
                    MarkTabDirty();
                });
            _undoManager.Execute(cmd);
            _rectangleBeforeEdit = newRect;
        }

        private void OnMainSplitterMoved(object? sender, SplitterEventArgs e)
        {
            int maxDistance = Math.Min(400, _mainSplitContainer.Width - _mainSplitContainer.Panel2MinSize);
            if (_mainSplitContainer.SplitterDistance > maxDistance)
            {
                _mainSplitContainer.SplitterDistance = maxDistance;
            }
        }

        private void OnRightSplitterMoved(object? sender, SplitterEventArgs e)
        {
            int minDistance = Math.Max(0, _rightSplitContainer.Height - 200);
            if (_rightSplitContainer.Panel2.Height > 200 && _rightSplitContainer.SplitterDistance < minDistance)
            {
                _rightSplitContainer.SplitterDistance = minDistance;
            }
        }

        private void OnBottomPanelResize(object? sender, EventArgs e)
        {
            int panelWidth = _bottomPanel.ClientSize.Width - 20; // Account for padding
            int panelHeight = _bottomPanel.ClientSize.Height - 20;

            int labelWidth = 60;
            int textBoxWidth = 100;
            int pairWidth = labelWidth + textBoxWidth; // Total width of label + textbox
            int spacing = 60; // Spacing between pairs

            int totalWidth = (pairWidth * 3) + (spacing * 2); // 3 pairs + 2 spacings
            int startX = (panelWidth - totalWidth) / 2; // Center horizontally

            int controlHeight = 20;
            int rowSpacing = 20; // Spacing between rows
            int totalHeight = (controlHeight * 2) + rowSpacing; // 2 rows + spacing
            int startY = (panelHeight - totalHeight) / 2; // Center vertically

            int row2Y = startY + controlHeight + rowSpacing;

            // Row 0 - X, Width, Pivot X
            _xLabel.SetBounds(startX, startY, labelWidth, controlHeight);
            _xTextBox.SetBounds(_xLabel.Right, startY, textBoxWidth, controlHeight);

            _widthLabel.SetBounds(_xTextBox.Right + spacing, startY, labelWidth, controlHeight);
            _widthTextBox.SetBounds(_widthLabel.Right, startY, textBoxWidth, controlHeight);

            _pivotXLabel.SetBounds(_widthTextBox.Right + spacing, startY, labelWidth, controlHeight);
            _pivotXTextBox.SetBounds(_pivotXLabel.Right, startY, textBoxWidth, controlHeight);

            // Row 1 - Y, Height, Pivot Y
            _yLabel.SetBounds(startX, row2Y, labelWidth, controlHeight);
            _yTextBox.SetBounds(_yLabel.Right, row2Y, textBoxWidth, controlHeight);

            _heightLabel.SetBounds(_yTextBox.Right + spacing, row2Y, labelWidth, controlHeight);
            _heightTextBox.SetBounds(_heightLabel.Right, row2Y, textBoxWidth, controlHeight);

            _pivotYLabel.SetBounds(_heightTextBox.Right + spacing, row2Y, labelWidth, controlHeight);
            _pivotYTextBox.SetBounds(_pivotYLabel.Right, row2Y, textBoxWidth, controlHeight);
        }

        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false
            };
        }

        public void AddTreeItem(SpriteReference reference)
        {
            if (reference.RectangleIndex == -1)
            {
                TreeNode node = new TreeNode(reference.Text)
                {
                    Tag = reference
                };
                _itemTreeView.Nodes.Add(node);
            }
            else
            {
                TreeNode? parentNode = FindSpriteNode(reference.SpriteIndex);
                if (parentNode != null)
                {
                    TreeNode childNode = new TreeNode(reference.Text)
                    {
                        Tag = reference
                    };
                    parentNode.Nodes.Add(childNode);
                }
            }
        }

        public void AddTreeItems(IEnumerable<SpriteReference> items)
        {
            _itemTreeView.BeginUpdate();

            var itemList = items.ToList();

            // First pass: Add all parent nodes (RectangleIndex == -1)
            foreach (var reference in itemList.Where(i => i.RectangleIndex == -1))
            {
                TreeNode node = new TreeNode(reference.Text)
                {
                    Tag = reference
                };
                _itemTreeView.Nodes.Add(node);
            }

            // Second pass: Add all child nodes (RectangleIndex >= 0)
            foreach (var reference in itemList.Where(i => i.RectangleIndex >= 0))
            {
                TreeNode? parentNode = FindSpriteNode(reference.SpriteIndex);
                if (parentNode != null)
                {
                    TreeNode childNode = new TreeNode(reference.Text)
                    {
                        Tag = reference
                    };
                    parentNode.Nodes.Add(childNode);
                }
            }

            _itemTreeView.EndUpdate();
        }

        public void RemoveTreeItem(SpriteReference reference)
        {
            TreeNode? nodeToRemove = FindTreeNode(reference);
            if (nodeToRemove != null)
            {
                if (nodeToRemove.Parent != null)
                {
                    nodeToRemove.Parent.Nodes.Remove(nodeToRemove);
                }
                else
                {
                    _itemTreeView.Nodes.Remove(nodeToRemove);
                }

                if (_selectedItem?.Equals(reference) == true)
                {
                    _selectedItem = null;
                }
            }
        }

        public void RemoveTreeItems(IEnumerable<SpriteReference> items)
        {
            _itemTreeView.BeginUpdate();

            foreach (var reference in items)
            {
                RemoveTreeItem(reference);
            }

            _itemTreeView.EndUpdate();
        }

        public void RemoveSpriteNode(int spriteIndex)
        {
            TreeNode? spriteNode = FindSpriteNode(spriteIndex);
            if (spriteNode != null)
            {
                _itemTreeView.Nodes.Remove(spriteNode);

                if (_selectedItem?.SpriteIndex == spriteIndex)
                {
                    _selectedItem = null;
                }
            }
        }

        private TreeNode? FindTreeNode(SpriteReference reference)
        {
            if (reference.RectangleIndex == -1)
            {
                return FindSpriteNode(reference.SpriteIndex);
            }
            else
            {
                TreeNode? parentNode = FindSpriteNode(reference.SpriteIndex);
                if (parentNode != null)
                {
                    foreach (TreeNode childNode in parentNode.Nodes)
                    {
                        if (childNode.Tag is SpriteReference childRef &&
                            childRef.SpriteIndex == reference.SpriteIndex &&
                            childRef.RectangleIndex == reference.RectangleIndex)
                        {
                            return childNode;
                        }
                    }
                }
            }
            return null;
        }

        private TreeNode? FindSpriteNode(int spriteIndex)
        {
            foreach (TreeNode node in _itemTreeView.Nodes)
            {
                if (node.Tag is SpriteReference reference &&
                    reference.SpriteIndex == spriteIndex &&
                    reference.RectangleIndex == -1)
                {
                    return node;
                }
            }
            return null;
        }

        public void PopulateTreeItems()
        {
            ClearTreeItems();
            if (OpenPAK?.Data != null)
            {
                List<SpriteReference> spriteRefs = new List<SpriteReference>();
                for (int i = 0; i < OpenPAK.Data.Sprites.Count; i++)
                {
                    SpriteReference reference = new SpriteReference(i, -1, $"Sprite {Convert.ToString(i)}");
                    spriteRefs.Add(reference);
                    for (int r = 0; r < OpenPAK.Data.Sprites[i].Rectangles.Count(); r++)
                    {
                        spriteRefs.Add(new SpriteReference(i, r, Convert.ToString(r)));
                    }
                }

                AddTreeItems(spriteRefs);
                if (!HasNodesInTreeView())
                {
                    mainWindow.SetStatusLabel("No sprites loaded, right click the treeview to add some!");
                }
                else
                {
                    mainWindow.SetStatusLabel("Idle...");
                }
            }
        }

        public bool HasNodesInTreeView()
        {
            return _itemTreeView.Nodes.Count > 0;
        }

        public void ClearTreeItems()
        {
            _itemTreeView.Nodes.Clear();
            _selectedItem = null;
        }

        private int _currentSpriteIndex = -1;
        private void OnTreeViewItemSelected(object? sender, TreeViewEventArgs e)
        {
            _selectedItem = e.Node?.Tag is SpriteReference reference ? reference : null;

            if (_selectedItem == null || OpenPAK?.Data == null || _selectedItem.Value.SpriteIndex == -1)
                return;

            var spriteIndex = _selectedItem.Value.SpriteIndex;
            var sprite = OpenPAK.Data.Sprites[spriteIndex];
            if (sprite == null)
                return;

            bool spriteChanged = spriteIndex != _currentSpriteIndex;

            if (spriteChanged)
            {
                _currentSpriteIndex = spriteIndex;

                using var ms = new MemoryStream(sprite.data);
                _topPanel.CurrentBitmap = new Bitmap(ms);

                _topPanel.PAKData = OpenPAK;
                _imageInfoStatusLabel.Text = FileSignatureDetector.DetectFileType(sprite.data);
                _dimensionsStatusLabel.Text = $"{_topPanel.CurrentBitmap.Width}x{_topPanel.CurrentBitmap.Height}";
            }

            // Update rectangles list
            var rectangles = new List<SpriteReference>();
            for (int i = 0; i < sprite.Rectangles.Count; i++)
            {
                if (i != _selectedItem.Value.RectangleIndex)
                    rectangles.Add(new SpriteReference(spriteIndex, i, i.ToString()));
            }
            _topPanel.Rectangles = rectangles;

            // Update selected rectangle
            if (_selectedItem.Value.RectangleIndex >= 0 &&
                _selectedItem.Value.RectangleIndex < sprite.Rectangles.Count)
            {
                var sprRect = sprite.Rectangles[_selectedItem.Value.RectangleIndex];
                _topPanel.CurrentRectangle = new SpriteReference(spriteIndex, _selectedItem.Value.RectangleIndex, _selectedItem.Value.RectangleIndex.ToString());
                X = sprRect.x;
                Y = sprRect.y;
                Width = sprRect.width;
                Height = sprRect.height;
                OffsetX = sprRect.pivotX;
                OffsetY = sprRect.pivotY;

                CaptureCurrentRectangle();

                _xTextBox.Enabled = true;
                _yTextBox.Enabled = true;
                _widthTextBox.Enabled = true;
                _heightTextBox.Enabled = true;
                _pivotXTextBox.Enabled = true;
                _pivotYTextBox.Enabled = true;
                _rectangleDimensionsStatusLabel.Text = $"{sprRect.width}x{sprRect.height}";
            }
            else
            {
                _rectangleBeforeEdit = null;
                _topPanel.CurrentRectangle = null;
                X = 0;
                Y = 0;
                Width = 0;
                Height = 0;
                OffsetX = 0;
                OffsetY = 0;
                _xTextBox.Enabled = false;
                _yTextBox.Enabled = false;
                _widthTextBox.Enabled = false;
                _heightTextBox.Enabled = false;
                _pivotXTextBox.Enabled = false;
                _pivotYTextBox.Enabled = false;
            }
        }
    }

    public struct SpriteReference
    {
        public int SpriteIndex { get; set; }
        public int RectangleIndex { get; set; }
        public string Text { get; set; }

        public SpriteReference()
        {
            SpriteIndex = -1;
            RectangleIndex = -1;
            Text = string.Empty;
        }

        public SpriteReference(int spriteIndex, int rectangleIndex, string text)
        {
            SpriteIndex = spriteIndex;
            RectangleIndex = rectangleIndex;
            Text = text;
        }
    }

    public class NumericTextBox<T> : TextBox where T : INumber<T>
    {
        public bool AllowNegative { get; set; } = false;

        public NumericTextBox()
        {
            BorderStyle = BorderStyle.FixedSingle;
            TextAlign = HorizontalAlignment.Center;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                if (e.KeyChar == '-' && AllowNegative && SelectionStart == 0 && !Text.Contains('-'))
                {
                    base.OnKeyPress(e);
                    return;
                }
                e.Handled = true;
            }
            base.OnKeyPress(e);
        }

        public void SetValue(T value)
        {
            Text = value.ToString();
        }

        public T GetValue()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                return T.Zero;
            }

            // Try parsing as the largest type first to check for overflow
            if (!double.TryParse(Text, out double doubleValue))
            {
                return T.Zero;
            }

            // Check if the value fits in the target type's range
            T minValue = GetMinValue();
            T maxValue = GetMaxValue();

            if (T.TryParse(Text, System.Globalization.NumberStyles.Integer, null, out T? value))
            {
                // Verify the parsed value is within bounds
                if (value >= minValue && value <= maxValue)
                {
                    return value;
                }
            }

            // Value is out of range, clamp it
            if (doubleValue < Convert.ToDouble(minValue))
            {
                return minValue;
            }
            if (doubleValue > Convert.ToDouble(maxValue))
            {
                return maxValue;
            }

            return T.Zero;
        }

        protected override void OnLostFocus(EventArgs e)
        {
            T value = GetValue();
            SetValue(value);
            base.OnLostFocus(e);
        }

        private T GetMinValue()
        {
            TypeCode code = Type.GetTypeCode(typeof(T));
            return code switch
            {
                TypeCode.Byte => (T)(object)byte.MinValue,
                TypeCode.SByte => (T)(object)sbyte.MinValue,
                TypeCode.Int16 => (T)(object)short.MinValue,
                TypeCode.UInt16 => (T)(object)ushort.MinValue,
                TypeCode.Int32 => (T)(object)int.MinValue,
                TypeCode.UInt32 => (T)(object)uint.MinValue,
                TypeCode.Int64 => (T)(object)long.MinValue,
                TypeCode.UInt64 => (T)(object)ulong.MinValue,
                TypeCode.Single => (T)(object)float.MinValue,
                TypeCode.Double => (T)(object)double.MinValue,
                TypeCode.Decimal => (T)(object)decimal.MinValue,
                _ => T.Zero
            };
        }

        private T GetMaxValue()
        {
            TypeCode code = Type.GetTypeCode(typeof(T));
            return code switch
            {
                TypeCode.Byte => (T)(object)byte.MaxValue,
                TypeCode.SByte => (T)(object)sbyte.MaxValue,
                TypeCode.Int16 => (T)(object)short.MaxValue,
                TypeCode.UInt16 => (T)(object)ushort.MaxValue,
                TypeCode.Int32 => (T)(object)int.MaxValue,
                TypeCode.UInt32 => (T)(object)uint.MaxValue,
                TypeCode.Int64 => (T)(object)long.MaxValue,
                TypeCode.UInt64 => (T)(object)ulong.MaxValue,
                TypeCode.Single => (T)(object)float.MaxValue,
                TypeCode.Double => (T)(object)double.MaxValue,
                TypeCode.Decimal => (T)(object)decimal.MaxValue,
                _ => T.Zero
            };
        }
    }

    public class PAKTabEmpty : PAKTabTemplate
    {
        public PAKTabEmpty(MainWindow window) : base(window)
        {
            Text = "Empty";
            window.SetStatusLabel("Create or open a PAK file in the top menu!");
        }
    }

    #region Rectangle Commands

    public class AddRectangleCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly int _spriteIndex;
        private readonly SpriteRectangle _rectangle;
        private readonly Action _onExecute;
        private readonly Action _onUndo;

        public string Description => "Add Rectangle";

        public AddRectangleCommand(PAK pak, int spriteIndex, SpriteRectangle rectangle,
            Action onExecute, Action? onUndo = null)
        {
            _pak = pak;
            _spriteIndex = spriteIndex;
            _rectangle = rectangle;
            _onExecute = onExecute;
            _onUndo = onUndo ?? onExecute;
        }

        public void Execute()
        {
            _pak.Data.Sprites[_spriteIndex].Rectangles.Add(_rectangle);
            _onExecute();
        }

        public void Undo()
        {
            var rects = _pak.Data.Sprites[_spriteIndex].Rectangles;
            rects.RemoveAt(rects.Count - 1);
            _onUndo();
        }
    }

    public class DeleteRectangleCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly int _spriteIndex;
        private readonly int _rectangleIndex;
        private readonly SpriteRectangle _deletedRectangle;
        private readonly Action _refreshUI;

        public string Description => "Delete Rectangle";

        public DeleteRectangleCommand(PAK pak, int spriteIndex, int rectangleIndex, Action refreshUI)
        {
            _pak = pak;
            _spriteIndex = spriteIndex;
            _rectangleIndex = rectangleIndex;
            _deletedRectangle = pak.Data.Sprites[spriteIndex].Rectangles[rectangleIndex];
            _refreshUI = refreshUI;
        }

        public void Execute()
        {
            _pak.Data.Sprites[_spriteIndex].Rectangles.RemoveAt(_rectangleIndex);
            _refreshUI();
        }

        public void Undo()
        {
            _pak.Data.Sprites[_spriteIndex].Rectangles.Insert(_rectangleIndex, _deletedRectangle);
            _refreshUI();
        }
    }

    public class ModifyRectangleCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly int _spriteIndex;
        private readonly int _rectangleIndex;
        private readonly SpriteRectangle _oldValue;
        private readonly SpriteRectangle _newValue;
        private readonly Action _refreshUI;

        public string Description => "Modify Rectangle";

        public ModifyRectangleCommand(PAK pak, int spriteIndex, int rectangleIndex,
            SpriteRectangle oldValue, SpriteRectangle newValue, Action refreshUI)
        {
            _pak = pak;
            _spriteIndex = spriteIndex;
            _rectangleIndex = rectangleIndex;
            _oldValue = oldValue;
            _newValue = newValue;
            _refreshUI = refreshUI;
        }

        public void Execute()
        {
            _pak.Data.Sprites[_spriteIndex].Rectangles[_rectangleIndex] = _newValue;
            _refreshUI();
        }

        public void Undo()
        {
            _pak.Data.Sprites[_spriteIndex].Rectangles[_rectangleIndex] = _oldValue;
            _refreshUI();
        }
    }

    public class AutoRectangleCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly int _spriteIndex;
        private readonly List<SpriteRectangle> _newRectangles;
        private readonly List<SpriteRectangle> _oldRectangles;
        private readonly Action _refreshUI;

        public string Description => "Auto-Rectangle";

        public AutoRectangleCommand(PAK pak, int spriteIndex, List<SpriteRectangle> newRectangles, Action refreshUI)
        {
            _pak = pak;
            _spriteIndex = spriteIndex;
            _newRectangles = newRectangles;
            // Save the old rectangles for undo
            _oldRectangles = new List<SpriteRectangle>(pak.Data.Sprites[spriteIndex].Rectangles);
            _refreshUI = refreshUI;
        }

        public void Execute()
        {
            var rects = _pak.Data.Sprites[_spriteIndex].Rectangles;
            rects.Clear();
            rects.AddRange(_newRectangles);
            _refreshUI();
        }

        public void Undo()
        {
            var rects = _pak.Data.Sprites[_spriteIndex].Rectangles;
            rects.Clear();
            rects.AddRange(_oldRectangles);
            _refreshUI();
        }
    }

    #endregion

    #region Sprite Commands
    public class AddSpriteCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly Sprite _sprite;
        private readonly Action _refreshUI;

        public string Description => "Add Sprite";

        public AddSpriteCommand(PAK pak, Sprite sprite, Action refreshUI)
        {
            _pak = pak;
            _sprite = sprite;
            _refreshUI = refreshUI;
        }

        public void Execute()
        {
            _pak.Data.Sprites.Add(_sprite);
            _refreshUI();
        }

        public void Undo()
        {
            _pak.Data.Sprites.RemoveAt(_pak.Data.Sprites.Count - 1);
            _refreshUI();
        }
    }

    public class DeleteSpriteCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly int _spriteIndex;
        private readonly Sprite _deletedSprite;
        private readonly Action _refreshUI;

        public string Description => "Delete Sprite";

        public DeleteSpriteCommand(PAK pak, int spriteIndex, Action refreshUI)
        {
            _pak = pak;
            _spriteIndex = spriteIndex;
            _deletedSprite = pak.Data.Sprites[spriteIndex];
            _refreshUI = refreshUI;
        }

        public void Execute()
        {
            _pak.Data.Sprites.RemoveAt(_spriteIndex);
            _refreshUI();
        }

        public void Undo()
        {
            _pak.Data.Sprites.Insert(_spriteIndex, _deletedSprite);
            _refreshUI();
        }
    }

    public class ReplaceSpriteCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly int _spriteIndex;
        private readonly byte[] _oldData;
        private readonly byte[] _newData;
        private readonly Action _refreshUI;

        public string Description => "Replace Sprite";

        public ReplaceSpriteCommand(PAK pak, int spriteIndex, byte[] newData, Action refreshUI)
        {
            _pak = pak;
            _spriteIndex = spriteIndex;
            _oldData = pak.Data.Sprites[spriteIndex].data;
            _newData = newData;
            _refreshUI = refreshUI;
        }

        public void Execute()
        {
            _pak.Data.Sprites[_spriteIndex].data = _newData;
            _refreshUI();
        }

        public void Undo()
        {
            _pak.Data.Sprites[_spriteIndex].data = _oldData;
            _refreshUI();
        }
    }

    public class ImportRectanglesCommand : IUndoableCommand
    {
        private readonly PAK _pak;
        private readonly int _spriteIndex;
        private readonly List<SpriteRectangle> _importedRectangles;
        private readonly int _originalCount;
        private readonly Action _refreshUI;

        public string Description => "Import Rectangles";

        public ImportRectanglesCommand(PAK pak, int spriteIndex, List<SpriteRectangle> rectangles, Action refreshUI)
        {
            _pak = pak;
            _spriteIndex = spriteIndex;
            _importedRectangles = rectangles;
            _originalCount = pak.Data.Sprites[spriteIndex].Rectangles.Count;
            _refreshUI = refreshUI;
        }

        public void Execute()
        {
            _pak.Data.Sprites[_spriteIndex].Rectangles.AddRange(_importedRectangles);
            _refreshUI();
        }

        public void Undo()
        {
            var rects = _pak.Data.Sprites[_spriteIndex].Rectangles;
            while (rects.Count > _originalCount)
                rects.RemoveAt(rects.Count - 1);
            _refreshUI();
        }
    }

    #endregion
}