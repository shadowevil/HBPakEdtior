using Newtonsoft.Json;
using PAKLib;
using PluginContracts;
using PluginContracts.HostInterfaces;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Security.Policy;

namespace PAKFile
{
    public partial class PAKInterface : IPlugin, IPluginTabIndexChanged, IPluginMainWindowLoaded, IPluginMainWindowResize, IPluginMainWindowClosing
    {
        private Dictionary<TabPage, (string, PAK)> _pakFiles = new Dictionary<TabPage, (string, PAK)>();
        private IPluginHostPublic PluginHost = null!;
        IPluginHostPublic? IPlugin.PluginHost => PluginHost;
        public string Name => "PAK Interface Plugin";

        private IPluginMainWindow? _mainWindow => PluginHost.GetService<IPluginMainWindow>();
        private MenuStrip FileMenuStrip = null!;
        private TabPage _tabTemplate = null!;
        private TabControl tabControl1 = null!;

        private TabPage? _selectedTab => _pakFiles.Keys.FirstOrDefault(t => ReferenceEquals(t, tabControl1?.SelectedTab));
        private TreeView? spriteTreeView = null;
        private PictureBox? pbSpriteImage = null;
        private MenuStrip? pbImageControlBar = null;
        private ToolStripMenuItem? zoomToolStripMenuItem = null;
        private float zoomScale = 0.95f;

        private StatusStrip? statusStrip = null;
        private ToolStripStatusLabel? scaleLabel = null;
        private ToolStripStatusLabel? imageSizeLabel = null;
        private ToolStripButton? imageFormatButton = null;

        private TextBox? numPivY = null;
        private TextBox? numHeight = null;
        private TextBox? numY = null;
        private TextBox? numPivX = null;
        private TextBox? numWidth = null;
        private TextBox? numX = null;

        private Pen _selectionPen = new Pen(Color.Yellow, 1);
        private Pen _unselectedPen = new Pen(Color.Red, 1);

        public const string AllFilesPattern = "All Files (*.*)|*.*";
        public const string PAKFilePattern = "PAK Files (*.pak)|*.pak";
        public string AcceptedImageFilePatterns => string.Join("|", (IEnumerable<string>)[
            "All Supported Image Files|*.bmp;*.png;*.jpg;*.jpeg;*.gif",
            "Bitmap Files (*.bmp)|*.bmp",
            "PNG Files (*.png)|*.png",
            "JPEG Files (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            "GIF Files (*.gif)|*.gif",
            AllFilesPattern
        ]);


        public PAKInterface(IPluginHostPublic pluginHost)
        {
            PluginHost = pluginHost;
        }

        private void newToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            TabPage newTab = CopyTemplateTab("New PAK File", "NewPakFile");
            _pakFiles[newTab] = (string.Empty, new PAK());
            _pakFiles[newTab].Item2.Data = new PAKData();
            _mainWindow!.OnTabPageAddition(newTab);
            LoadSelectedTab(newTab);
        }

        private void openToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = $"{PAKFilePattern}|{AllFilesPattern}";
            ofd.Title = "Open PAK File";
            ofd.Multiselect = false;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                OpenPAKFile(ofd.FileName);
            }
        }

        private void OpenPAKFile(string filePath)
        {
            TabPage newTab = CopyTemplateTab(Path.GetFileName(filePath), Path.GetFileNameWithoutExtension(filePath).Replace(" ", ""));
            _pakFiles[newTab] = (filePath, PAK.ReadFromFile(filePath));
            _mainWindow!.OnTabPageAddition(newTab);
            LoadSelectedTab(newTab);
        }

        private void saveToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!PrecheckSelectedTab(out string? FilePath, out PAK? pakFile, out TabPage? tab))
                return;

            if (string.IsNullOrWhiteSpace(FilePath))
            {
                saveAsToolStripMenuItem_Click(sender, e);
                return;
            }

            pakFile!.Data!.Write(FilePath);
            _mainWindow!.SetTabDirty(tab!, false);
        }

        private bool PrecheckSelectedTab(out string? outFilePath, out PAK? outPakFile, out TabPage? outTab)
        {
            var tab = _selectedTab;
            if (tab == null)
            {
                MessageBox.Show("No tab selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                outFilePath = null;
                outPakFile = null;
                outTab = null;
                return false;
            }

            string? internalFilePath = null;
            PAK? internalPakFile = null;
            if (_pakFiles.TryGetValue(tab, out var entry))
            {
                internalFilePath = entry.Item1;
                internalPakFile = entry.Item2;
            }

            if (!_pakFiles.ContainsKey(tab))
            {
                MessageBox.Show("No tab selected or PAK file not loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                outFilePath = null;
                outPakFile = null;
                outTab = null;
                return false;
            }

            if (internalPakFile == null || internalPakFile.Data == null
                && internalFilePath == null)
            {
                MessageBox.Show("PAK file not loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                outFilePath = null;
                outPakFile = null;
                outTab = null;
                return false;
            }

            outFilePath = internalFilePath;
            outPakFile = internalPakFile;
            outTab = tab;
            return true;
        }

        private void saveAsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!PrecheckSelectedTab(out string? FilePath, out PAK? pakFile, out TabPage? tab))
                return;

            using SaveFileDialog sfd = new SaveFileDialog();

            sfd.Filter = $"{PAKFilePattern}|{AllFilesPattern}";
            sfd.Title = "Save PAK File";
            sfd.FileName = tab.Text.Trim('*');
            sfd.RestoreDirectory = true;
            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            pakFile!.Data!.Write(sfd.FileName);
            _mainWindow!.SetTabDirty(tab, false);
            MessageBox.Show($"PAK file saved successfully to {Path.GetFileName(sfd.FileName)}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private TabPage CopyTemplateTab(string text, string name)
        {
            var newTab = _tabTemplate.DeepClone();
            newTab.Text = text;
            newTab.Name = name + "Tab";
            var container = newTab.Controls.Find("splitContainer2", true).First() as SplitContainer;
            if(container == null)
                throw new InvalidOperationException("SplitContainer not found in the template tab.");
            container.Panel2.Controls.Clear();
            container.Panel2.Controls.Add(InitializePluginComponents());
            statusStrip = new StatusStrip()
            {
                Name = "statusStrip1"
            };
            scaleLabel = new ToolStripStatusLabel("95.00%");
            imageSizeLabel = new ToolStripStatusLabel("0x0");
            imageFormatButton = new ToolStripButton("Unknown")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Name = "imageFormatButton",
                ToolTipText = $"Change Pixel Format to {PixelFormat.Format8bppIndexed.ToString()}"
            };
            imageFormatButton.Click += ChangeImageFormat_Click;
            statusStrip.Items.AddRange([
                scaleLabel,
                new ToolStripSeparator(),
                imageSizeLabel,
                new ToolStripSeparator(),
                imageFormatButton
            ]);
            container.Panel1.Controls.Add(statusStrip);
            return newTab;
        }

        private void ChangeImageFormat_Click(object? sender, EventArgs e)
        {
            //if (!LoadSelectedTabControls())
            //    return;

            //if (spriteTreeView == null || pbSpriteImage == null || imageFormatButton == null)
            //{
            //    MessageBox.Show("UI components not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return;
            //}

            //TreeNode? selectedNode = spriteTreeView.SelectedNode;
            //if (selectedNode == null || selectedNode.Tag is not Sprite sprite)
            //{
            //    MessageBox.Show("No sprite selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return;
            //}
            //if (sprite.data == null)
            //{
            //    MessageBox.Show("Sprite image is not loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return;
            //}
            //// Change the pixel format of the sprite
            //sprite.sprite = Sprite.ConvertTo8bppBitmap(sprite.sprite);
            //pbSpriteImage.Image = sprite.sprite;
        }

        public void LoadSelectedTab(TabPage selectedTab)
        {
            if (!LoadSelectedTabControls())
                return;

            if (!PrecheckSelectedTab(out string? _, out PAK? pakFile, out TabPage? _))
                return;

            if (spriteTreeView == null)
            {
                MessageBox.Show("No tab selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var nodes = spriteTreeView.Nodes;
            nodes.Clear();

            for (int i = 0; i < pakFile!.Data!.Sprites.Count; i++)
            {
                AddSprite(pakFile.Data.Sprites[i], pakFile.Data.Sprites[i].Rectangles, true);
            }
        }

        private void RemoveSprite(TreeNode node)
        {
            if (node.Tag is not Sprite sprite)
            {
                MessageBox.Show("No sprite selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (spriteTreeView == null)
            {
                MessageBox.Show("Sprite TreeView not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!PrecheckSelectedTab(out string? _, out PAK? pakFile, out TabPage? tab))
                return;

            pakFile!.Data!.Sprites.Remove(sprite);
            spriteTreeView.Nodes.Remove(node);
            _mainWindow!.SetTabDirty(tab!, true);
        }
        private void RemoveRectangle(TreeNode node)
        {
            if (node.Tag is not SpriteRectangle rect)
            {
                MessageBox.Show("No rectangle selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (spriteTreeView == null)
            {
                MessageBox.Show("Sprite TreeView not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (node.Parent?.Tag is not Sprite sprite)
            {
                MessageBox.Show("No sprite found for the selected rectangle.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!PrecheckSelectedTab(out string? _, out PAK? _, out TabPage? tab))
                return;

            sprite.Rectangles.Remove(rect);
            node.Remove();
            _mainWindow!.SetTabDirty(tab!, true);
        }

        private void AddSprite(Sprite sprite, List<SpriteRectangle> rects, bool nodeOnly = false)
        {
            var tab = _selectedTab;

            if (spriteTreeView == null)
            {
                MessageBox.Show("Sprite TreeView not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if(!nodeOnly)
                _pakFiles[tab!].Item2.Data!.Sprites.Add(sprite);
            var spriteNode = new TreeNode($"Sprite {spriteTreeView.Nodes.Count}") { Tag = sprite };
            foreach (var rect in rects)
            {
                AddRectangle(spriteNode, sprite, rect, nodeOnly);
            }
            spriteTreeView.Nodes.Add(spriteNode);
            _mainWindow!.SetTabDirty(tab!, true);
        }

        private void AddRectangle(TreeNode node, Sprite sprite, SpriteRectangle rect, bool nodeOnly = false)
        {
            if (!nodeOnly)
            {
                sprite.Rectangles.Add(rect);
            }
            node.Nodes.Add(new TreeNode((node.Nodes.Count).ToString()) { Tag = rect });
            pbSpriteImage?.Invalidate();
        }

        private string _lastTabName = string.Empty;
        private bool LoadSelectedTabControls()
        {
            TabPage? tab = _selectedTab;
            if (tab == null)
            {
                MessageBox.Show("No tab selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if(tab.Name == _lastTabName)
            {
                return true;
            }
            _lastTabName = tab.Name;

            // locate new controls
            var newSpriteTreeView = tab.Controls.Find("spriteTreeView", true).FirstOrDefault() as TreeView;
            var newPbSpriteImage = tab.Controls.Find("pbSpriteImage", true).FirstOrDefault() as PictureBox;
            var newControlBar = tab.Controls.Find("pbImageControlBar", true).FirstOrDefault() as MenuStrip;
            var newStatusStrip = tab.Controls.Find("statusStrip1", true).FirstOrDefault() as StatusStrip;
            var newNumX = tab.Controls.Find("numX", true).FirstOrDefault() as TextBox;
            var newNumY = tab.Controls.Find("numY", true).FirstOrDefault() as TextBox;
            var newNumWidth = tab.Controls.Find("numWidth", true).FirstOrDefault() as TextBox;
            var newNumHeight = tab.Controls.Find("numHeight", true).FirstOrDefault() as TextBox;
            var newNumPivX = tab.Controls.Find("numPivX", true).FirstOrDefault() as TextBox;
            var newNumPivY = tab.Controls.Find("numPivY", true).FirstOrDefault() as TextBox;
            var newZoomItem = newControlBar?.Items.Find("zoomToolStripMenuItem", false).FirstOrDefault() as ToolStripMenuItem;

            if (newSpriteTreeView == null || newPbSpriteImage == null || newControlBar == null || newStatusStrip == null)
            {
                MessageBox.Show("Failed to load UI components.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // helper: update only if changed
            void UpdateHandler<T>(ref T? field, T? control, Action<T> attach, Action<T> detach) where T : class
            {
                if (!ReferenceEquals(field, control))
                {
                    if (field != null) detach(field);
                    field = control;
                    if (field != null) attach(field);
                }
            }

            statusStrip = newStatusStrip;

            UpdateHandler(ref spriteTreeView, newSpriteTreeView,
                c => {
                    c.AfterSelect += SpriteTreeView_AfterSelect;

                    c.ContextMenuStrip = new ContextMenuStrip();
                    var strip = c.ContextMenuStrip;

                    strip.Opening += TreeView_ContextMenuStrip_Opening;
                },
                c => {
                    c.AfterSelect -= SpriteTreeView_AfterSelect;
                    if (c.ContextMenuStrip != null)
                    {
                        c.ContextMenuStrip.Opening -= TreeView_ContextMenuStrip_Opening;
                        c.ContextMenuStrip = null;
                    }
                });

            UpdateHandler(ref pbSpriteImage, newPbSpriteImage,
                c => c.Paint += PbSpriteImage_Paint,
                c => c.Paint -= PbSpriteImage_Paint);

            UpdateHandler(ref zoomToolStripMenuItem, newZoomItem,
                c => c.Click += ZoomToolStripMenuItem_Click,
                c => c.Click -= ZoomToolStripMenuItem_Click);

            UpdateHandler(ref numX, newNumX,
                c => { c.KeyDown += AllowOnlyNumerics; c.LostFocus += UpdateSelectedRect_LostFocus; },
                c => { c.KeyDown -= AllowOnlyNumerics; c.LostFocus -= UpdateSelectedRect_LostFocus; });

            UpdateHandler(ref numY, newNumY,
                c => { c.KeyDown += AllowOnlyNumerics; c.LostFocus += UpdateSelectedRect_LostFocus; },
                c => { c.KeyDown -= AllowOnlyNumerics; c.LostFocus -= UpdateSelectedRect_LostFocus; });

            UpdateHandler(ref numWidth, newNumWidth,
                c => { c.KeyDown += AllowOnlyNumerics; c.LostFocus += UpdateSelectedRect_LostFocus; },
                c => { c.KeyDown -= AllowOnlyNumerics; c.LostFocus -= UpdateSelectedRect_LostFocus; });

            UpdateHandler(ref numHeight, newNumHeight,
                c => { c.KeyDown += AllowOnlyNumerics; c.LostFocus += UpdateSelectedRect_LostFocus; },
                c => { c.KeyDown -= AllowOnlyNumerics; c.LostFocus -= UpdateSelectedRect_LostFocus; });

            UpdateHandler(ref numPivX, newNumPivX,
                c => { c.KeyDown += AllowOnlyNumerics; c.LostFocus += UpdateSelectedRect_LostFocus; },
                c => { c.KeyDown -= AllowOnlyNumerics; c.LostFocus -= UpdateSelectedRect_LostFocus; });

            UpdateHandler(ref numPivY, newNumPivY,
                c => { c.KeyDown += AllowOnlyNumerics; c.LostFocus += UpdateSelectedRect_LostFocus; },
                c => { c.KeyDown -= AllowOnlyNumerics; c.LostFocus -= UpdateSelectedRect_LostFocus; });

            OnWindowResize();

            return true;
        }

        private void TreeView_ContextMenuStrip_Opening(object? sender, CancelEventArgs e)
        {
            if (sender is ContextMenuStrip strip)
            {
                Point p = spriteTreeView?.PointToClient(Cursor.Position) ?? Point.Empty;
                TreeNode? node = spriteTreeView?.GetNodeAt(p);
                strip.Items.Clear();
                
                if (node != null)
                {
                    spriteTreeView!.SelectedNode = node;

                    Sprite? sprite = node.Tag switch { Sprite s => s, _ => null };
                    SpriteRectangle? spriteRectangle = node.Tag switch { SpriteRectangle r => r, _ => null };

                    if (sprite != null)
                    {
                        strip.Items.AddRange([
                            new ToolStripMenuItem("Add Rectangle", null, (s, ev) =>
                            {
                                SpriteRectangle newRect = new SpriteRectangle
                                {
                                    x = 0,
                                    y = 0,
                                    width = 1,
                                    height = 1,
                                    pivotX = 0,
                                    pivotY = 0
                                };
                                AddRectangle(node, sprite, newRect, false);
                            }),
                            new ToolStripSeparator(),
                            new ToolStripMenuItem("Import Sprite", null, (s, ev) => {
                                using OpenFileDialog ofd = new OpenFileDialog
                                {
                                    Title = "Select an Image",
                                    Multiselect = false,
                                    RestoreDirectory = true
                                };
                                ofd.Filter = AcceptedImageFilePatterns;
                                if (ofd.ShowDialog() != DialogResult.OK)
                                    return;

                                sprite.data = File.ReadAllBytes(ofd.FileName);
                                using (MemoryStream ms = new MemoryStream(sprite.data))
                                {
                                    pbSpriteImage!.Image = new Bitmap(ms);
                                }
                            }),
                            new ToolStripMenuItem("Export Sprite", null, (s, ev) =>
                            {
                                using SaveFileDialog sfd = new SaveFileDialog
                                {
                                    Title = "Export Sprite",
                                    Filter = AcceptedImageFilePatterns,
                                    RestoreDirectory = true
                                };
                                if (sfd.ShowDialog() != DialogResult.OK)
                                    return;
                                if (pbSpriteImage?.Image == null)
                                {
                                    MessageBox.Show("No sprite image to export.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                                DoExportSprite(sprite.data, sfd.FileName);
                            }),
                            new ToolStripSeparator(),
                            new ToolStripMenuItem("Remove Sprite", null, (s, ev) => RemoveSprite(node))
                        ]);
                    }
                    else if (spriteRectangle != null)
                    {
                        strip.Items.AddRange([
                            new ToolStripMenuItem("Remove Rectangle", null, (s, ev) => RemoveRectangle(node))
                        ]);
                    }
                }
                else
                {
                    strip.Items.AddRange([
                        new ToolStripMenuItem("Add Sprite", null, (s, ev) =>
                        {
                            using OpenFileDialog ofd = new OpenFileDialog
                            {
                                Title = "Select an Image",
                                Multiselect = false,
                                RestoreDirectory = true
                            };
                            ofd.Filter = AcceptedImageFilePatterns;

                            if(ofd.ShowDialog() != DialogResult.OK)
                                return;

                            Sprite newSprite = new Sprite
                            {
                                data = File.ReadAllBytes(ofd.FileName),
                                Rectangles = new List<SpriteRectangle>()
                            };
                            AddSprite(newSprite, newSprite.Rectangles);
                        }),
                        ]);
                }
            }
        }

        private void DoExportSprite(byte[] data, string fileName)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (EndianBinaryReader reader = new EndianBinaryReader(ms))
            {
                ImageHeaderInfo info = ImageHeaderReader.ReadHeader(reader);
                ImageFormat format = info.Format.ToString().ToLowerInvariant() switch
                {
                    "png" => ImageFormat.Png,
                    "jpeg" => ImageFormat.Jpeg,
                    "gif" => ImageFormat.Gif,
                    "bmp" => ImageFormat.Bmp,
                    _ => ImageFormat.Bmp
                };
                fileName = Path.ChangeExtension(fileName, format.ToString().ToLowerInvariant());

                File.WriteAllBytes(fileName, ms.ToArray());
            }
        }

        private void UpdateSelectedRect_LostFocus(object? sender, EventArgs e)
        {
            if (!LoadSelectedTabControls())
                return;

            if(spriteTreeView == null || pbSpriteImage == null)
            {
                MessageBox.Show("UI components not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            TreeNode? selectedNode = spriteTreeView.SelectedNode;
            if (selectedNode == null || selectedNode.Tag is not SpriteRectangle rect)
                return;

            if (sender is not TextBox textBox)
                return;

            if (!double.TryParse(textBox.Text, out double dvalue))
                return;

            short value = (short)Math.Clamp(dvalue, short.MinValue, short.MaxValue);
            textBox.Text = value.ToString();

            bool updated = false;

            switch (textBox.Name)
            {
                case "numX":
                    if (rect.x != value) { rect.x = value; updated = true; }
                    break;
                case "numY":
                    if (rect.y != value) { rect.y = value; updated = true; }
                    break;
                case "numWidth":
                    if (rect.width != value) { rect.width = value; updated = true; }
                    break;
                case "numHeight":
                    if (rect.height != value) { rect.height = value; updated = true; }
                    break;
                case "numPivX":
                    if (rect.pivotX != value) { rect.pivotX = value; updated = true; }
                    break;
                case "numPivY":
                    if (rect.pivotY != value) { rect.pivotY = value; updated = true; }
                    break;
            }

            if (updated)
            {
                _mainWindow!.SetTabDirty(_selectedTab!, true);
                pbSpriteImage.Invalidate();
            }
        }

        private void ZoomToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!LoadSelectedTabControls())
                return;

            if(zoomToolStripMenuItem == null || pbSpriteImage == null)
            {
                MessageBox.Show("UI components not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (zoomToolStripMenuItem.Checked)
                zoomToolStripMenuItem.BackColor = Color.FromArgb(128, Color.CornflowerBlue);
            else
                zoomToolStripMenuItem.BackColor = Color.Transparent;

            pbSpriteImage.Invalidate();
        }

        private void PbSpriteImage_Paint(object? sender, PaintEventArgs e)
        {
            if(pbSpriteImage == null || spriteTreeView == null || zoomToolStripMenuItem == null)
                return;

            TreeNode? selectedNode = spriteTreeView.SelectedNode;
            if (selectedNode == null)
                return;

            if (pbSpriteImage.Image == null)
                return;

            using Bitmap bmp = new Bitmap(pbSpriteImage.Image);
            int width = pbSpriteImage.Width;
            int height = pbSpriteImage.Height;
            int midX = width / 2;
            int midY = height / 2;
            // calculate required zoom to fit the image in the picturebox but keep the aspect ratio
            zoomScale = Math.Min((float)width / bmp.Width, (float)height / bmp.Height) * 0.95f;
            if (!zoomToolStripMenuItem.Checked)
                zoomScale = 0.95f;

            // Center image
            float x = (width - (bmp.Width * zoomScale)) / 2.0f;
            float y = (height - (bmp.Height * zoomScale)) / 2.0f;

            Graphics g = e.Graphics;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            g.Clear(pbSpriteImage.BackColor);
            g.TranslateTransform(x, y);
            g.ScaleTransform(zoomScale, zoomScale);

            DrawCheckerBackground(g, bmp.Width, bmp.Height);

            g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);

            DrawRectangles(g, selectedNode);

            if (statusStrip != null)
            {
                scaleLabel!.Text = $"{(zoomScale * 100.0f).ToString("0.00")}%";
                imageSizeLabel!.Text = $"{bmp.Width}x{bmp.Height}";
                imageFormatButton!.Text = bmp.PixelFormat.ToString();
            }
        }

        private void DrawRectangles(Graphics g, TreeNode selectedNode)
        {
            SpriteRectangle? spriteRectangle = null;
            if (selectedNode.Tag is SpriteRectangle r)
            {
                spriteRectangle = r;
                selectedNode = selectedNode.Parent;
            }

            Sprite? sprite = selectedNode?.Tag as Sprite;
            if (sprite == null)
                return;

            foreach (SpriteRectangle rect in sprite.Rectangles)
            {
                if (rect.Equals(spriteRectangle))
                    continue;

                RectangleF rectf = new RectangleF(rect.x, rect.y, rect.width, rect.height);
                g.DrawRectangle(_unselectedPen, rectf);
            }

            if (spriteRectangle != null)
            {
                RectangleF rectf = new RectangleF(spriteRectangle.x, spriteRectangle.y, spriteRectangle.width, spriteRectangle.height);
                g.DrawRectangle(_selectionPen, rectf);
            }
        }

        private void DrawCheckerBackground(Graphics g, float width, float height)
        {
            int checkerSize = 10;
            Color light = Color.LightGray;
            Color dark = Color.Gray;

            // Only draw checkerboard in the area where the image will be drawn
            RectangleF imageArea = new RectangleF(0, 0, width, height);

            using SolidBrush lightBrush = new SolidBrush(light);
            using SolidBrush darkBrush = new SolidBrush(dark);

            // Clip to image area
            g.SetClip(imageArea);

            // Draw checkerboard
            for (int cy = 0; cy < height; cy += checkerSize)
            {
                for (int cx = 0; cx < width; cx += checkerSize)
                {
                    bool isDark = ((cx / checkerSize) + (cy / checkerSize)) % 2 == 0;
                    RectangleF tile = new RectangleF(cx, cy, checkerSize, checkerSize);
                    g.FillRectangle(isDark ? darkBrush : lightBrush, tile);
                }
            }

            // Reset clip
            g.ResetClip();
        }

        private void SpriteTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node == null)
                return;

            LoadSelectedNode(e.Node);
        }

        public void LoadSelectedNode(TreeNode node)
        {
            if (!LoadSelectedTabControls())
                return;

            if (spriteTreeView == null || pbSpriteImage == null)
            {
                MessageBox.Show("UI components not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (node.Tag is Sprite sprite)
            {
                using (MemoryStream ms = new MemoryStream(sprite.data))
                {
                    pbSpriteImage.Image = new Bitmap(ms);
                }
            }
            else if (node.Tag is SpriteRectangle rect)
            {
                if (node.Parent!.Tag is Sprite spr)
                {
                    using (MemoryStream ms = new MemoryStream(spr.data))
                    {
                        pbSpriteImage.Image = new Bitmap(ms);
                    }
                }

                if(numX == null || numY == null || numWidth == null || numHeight == null || numPivX == null || numPivY == null)
                {
                    MessageBox.Show("UI components not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                numX.Text = rect.x.ToString();
                numY.Text = rect.y.ToString();
                numWidth.Text = rect.width.ToString();
                numHeight.Text = rect.height.ToString();
                numPivX.Text = rect.pivotX.ToString();
                numPivY.Text = rect.pivotY.ToString();
                pbSpriteImage.Invalidate();
            }
        }

        ToolStripMenuItem? FindMenuItem(ToolStrip? menu, string name)
        {
            if (menu == null)
                return null;

            foreach (ToolStripItem item in menu.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if (menuItem.Name == name)
                        return menuItem;

                    ToolStripMenuItem? found = FindMenuItemRecursive(menuItem, name);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        ToolStripMenuItem? FindMenuItemRecursive(ToolStripMenuItem parent, string name)
        {
            foreach (ToolStripItem item in parent.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if (menuItem.Name == name)
                        return menuItem;

                    ToolStripMenuItem? found = FindMenuItemRecursive(menuItem, name);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        private void AllowOnlyNumerics(object? sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                {
                    if (textBox.SelectionStart == 0 && !textBox.Text.StartsWith("-"))
                    {
                        e.SuppressKeyPress = false;
                        return;
                    }
                }

                // Allow digits, backspace, delete, arrow keys
                bool isDigit = (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9) ||
                               (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9);
                bool isControl = e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete ||
                                 e.KeyCode == Keys.Left || e.KeyCode == Keys.Right;

                e.SuppressKeyPress = !(isDigit || isControl);
            }
        }

        public void OnWindowResize()
        {
            var layoutPanel = _selectedTab?.Controls.Find("tableLayoutPanel1", true).FirstOrDefault();
            if (layoutPanel == null)
                return;

            foreach (Panel p in layoutPanel.Controls.OfType<Panel>())
            {
                // Get total width of all controls + spacing
                int spacing = 5;
                int totalWidth = p.Controls.Cast<Control>().Sum(c => c.Width) + spacing * (p.Controls.Count - 1);

                // Starting left position to center
                int startX = (p.Width - totalWidth) / 2;

                int x = startX;
                foreach (Control c in p.Controls)
                {
                    c.Left = x;
                    c.Top = (p.Height - c.Height) / 2;
                    c.Anchor = AnchorStyles.None;
                    x += c.Width + spacing;
                }
            }
        }

        public void OnSelectedTabChanged(TabPage tabPage)
        {
            LoadSelectedTab(tabPage);
        }

        public void OnMainWindowLoaded(string[] args)
        {
            if (_mainWindow == null)
            {
                PluginHost.Log("IPluginMainWindow is not available.");
                return;
            }

            if(_mainWindow.MainTabControl is not TabControl && _mainWindow.TabTemplate is not TabPage && _mainWindow.FileMenuStrip is not MenuStrip)
            {
                PluginHost.Log("Error, failed to load necessary components.");
                return;
            }

            tabControl1 = _mainWindow.MainTabControl!;
            _tabTemplate = _mainWindow.TabTemplate!;
            FileMenuStrip = _mainWindow.FileMenuStrip!;

            var fileToolStripMenuItem = FindMenuItem(FileMenuStrip, "fileToolStripMenuItem");
            var dropDownItems = fileToolStripMenuItem?.DropDownItems;
            if(dropDownItems == null)
            {
                PluginHost.Log("Error, fileToolStripMenuItem not found or has no DropDownItems.");
                return;
            }

            var newDropDownItems = new ToolStripItemCollection(FileMenuStrip, [
                new ToolStripMenuItem("New", null, newToolStripMenuItem_Click)
                {
                    Name = "newPakFileToolStripMenuItem",
                    ShortcutKeys = Keys.Control | Keys.N
                },
                new ToolStripMenuItem("Open", null, openToolStripMenuItem_Click)
                {
                    Name = "openPakFileToolStripMenuItem",
                    ShortcutKeys = Keys.Control | Keys.O
                },
                new ToolStripSeparator(),
                new ToolStripMenuItem("Save", null, saveToolStripMenuItem_Click)
                {
                    Name = "savePakFileToolStripMenuItem",
                    ShortcutKeys = Keys.Control | Keys.S
                },
                new ToolStripMenuItem("Save As", null, saveAsToolStripMenuItem_Click)
                {
                    Name = "saveAsPakFileToolStripMenuItem",
                    ShortcutKeys = Keys.Control | Keys.Shift | Keys.S
                },
                new ToolStripSeparator(),
                new ToolStripMenuItem("Export All Sprites", null, exportAllSpritesToolStripMenuItem_Click)
                {
                    Name = "exportAllSpritesPakFileToolStripMenuItem",
                    ShortcutKeys = Keys.Control | Keys.E
                },
                new ToolStripMenuItem("Import Sprites", null, importSpritesToolStripMenuItem_Click)
                {
                    Name = "importSpritesPakFileToolStripMenuItem",
                    ShortcutKeys = Keys.Control | Keys.I
                },
                new ToolStripSeparator(),
                ]);

            for (int i = 0; i < newDropDownItems.Count; i++)
            {
                dropDownItems.Insert(i, newDropDownItems[i]);
            }
            _mainWindow.ClosedState.AddRange(["openPakFileToolStripMenuItem", "newPakFileToolStripMenuItem"]);
            _mainWindow.OpenState.AddRange([
                    "openPakFileToolStripMenuItem", "savePakFileToolStripMenuItem",
                    "saveAsPakFileToolStripMenuItem", "newPakFileToolStripMenuItem",
                    "exportAllSpritesPakFileToolStripMenuItem", "importSpritesPakFileToolStripMenuItem"
                ]);

            if(args.Length > 0)
            {
                // If there are arguments, assume the first one is a file path to open
                string filePath = args[0];
                if (File.Exists(filePath))
                {
                    if (Path.GetExtension(filePath).ToLower() != ".pak")
                        return;

                    OpenPAKFile(filePath);
                }
                else
                {
                    PluginHost.Log($"File not found: {filePath}");
                }
            }
        }

        private void importSpritesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!PrecheckSelectedTab(out string? _, out PAK? pakFile, out TabPage? _))
                return;

            if (pakFile!.Data!.Sprites.Count > 0)
            {
                DialogResult result = MessageBox.Show("This will replace all existing sprites. Do you want to continue?", "Import Sprites", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                    return;
            }

            using OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Select Sprite Files",
                Multiselect = true,
                RestoreDirectory = true
            };

            ofd.Filter = AcceptedImageFilePatterns;

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            bool import_rectangles = MessageBox.Show("Do you want to import rectangles as well?\nNote the rectangle file must be of similar name as the sprite itself\njust ending in the \"json\" file extension.", "Import Rectangles", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            pakFile.Data!.Sprites.Clear();
            spriteTreeView?.Nodes.Clear();

            List<string> errors = new List<string>();
            foreach (string file in ofd.FileNames)
            {
                if (!File.Exists(file))
                {
                    errors.Add($"{Path.GetFileName(file)}");
                    continue;
                }

                string file_extension = Path.GetExtension(file).ToLowerInvariant();
                switch(file_extension)
                {
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".gif":
                    case ".bmp":
                        break;
                    default:
                        errors.Add($"{Path.GetFileName(file)} is not a supported image format.");
                        continue;
                }

                Sprite newSprite = new Sprite
                {
                    data = File.ReadAllBytes(file),
                    Rectangles = new List<SpriteRectangle>()
                };

                // Check if sprite rectangle exists
                if (import_rectangles)
                {
                    string rectanglesFile = Path.ChangeExtension(file, "json");
                    rectanglesFile = rectanglesFile.Replace("Sprite_", "SpriteRectangle_");
                    if (File.Exists(rectanglesFile))
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(rectanglesFile);
                            List<SpriteRectangle>? rectangles = JsonConvert.DeserializeObject<List<SpriteRectangle>>(jsonContent);
                            if (rectangles != null)
                            {
                                newSprite.Rectangles = rectangles!;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to load rectangles from {Path.GetFileName(rectanglesFile)}: {ex.Message}");
                        }
                    }
                    else
                    {
                        errors.Add($"No rectangle file found for {Path.GetFileName(file)}.");
                    }
                }

                AddSprite(newSprite, newSprite.Rectangles, true);
            }

            if(errors.Count > 0)
            {
                string errorMessage = "The following files could not be imported:\n" + string.Join("\n", errors);
                if(errorMessage.Length > 500)
                    errorMessage = errorMessage.Substring(0, 500) + "\n...";

                MessageBox.Show(errorMessage, "Import Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void exportAllSpritesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!PrecheckSelectedTab(out string? _, out PAK? pakFile, out TabPage? _))
                return;


            if (pakFile!.Data!.Sprites.Count <= 0)
                return;

            using FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "Select a folder to export all sprites";
            fbd.ShowNewFolderButton = true;
            if(fbd.ShowDialog() != DialogResult.OK)
                return;

            string exportPath = fbd.SelectedPath;

            const string export_sprtie_file_name_template = "Sprite_{num}.empty";
            const string export_spriterectangle_file_name_template = "SpriteRectangle_{num}.json";

            bool export_rectangles = MessageBox.Show("Do you want to export rectangles as well?", "Export Rectangles", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            for (int i = 0; i < pakFile.Data.Sprites.Count; i++)
            {
                Sprite sprite = pakFile.Data.Sprites[i];
                DoExportSprite(sprite.data, Path.Combine(exportPath, export_sprtie_file_name_template.Replace("{num}", Convert.ToString(i))));
                if(export_rectangles)
                {
                    string rectanglesFilePath = Path.Combine(exportPath, export_spriterectangle_file_name_template.Replace("{num}", Convert.ToString(i)));
                    File.WriteAllText(rectanglesFilePath, JsonConvert.SerializeObject(sprite.Rectangles));
                }
            }
        }

        public bool OnMainWindowClosing()
        {
            if (_pakFiles.Count == 0)
                return true;

            if(_mainWindow == null)
            {
                PluginHost.Log("IPluginMainWindow is not available.");
                return true;
            }

            if (_pakFiles.Any(x => _mainWindow.IsTabDirty(x.Key)))
            {
                DialogResult result = MessageBox.Show("You have unsaved changes in one or more PAK files. Do you want to save them before closing?", "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                switch(result)
                {
                    case DialogResult.Yes:
                        break;
                    case DialogResult.No:
                        return true;
                    case DialogResult.Cancel:
                        return false; // Cancel closing
                }

                foreach (var (tab, (path, pak)) in _pakFiles)
                {
                    if (_mainWindow.IsTabDirty(tab))
                    {
                        pak!.Data!.Write(path);
                        _mainWindow!.SetTabDirty(tab, false);
                    }
                }
            }

            return true;
        }
    }
}
