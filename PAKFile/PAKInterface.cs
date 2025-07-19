using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Formats.Tar;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PluginContracts;
using PluginContracts.HostInterfaces;

namespace PAKFile
{
    public partial class PAKInterface : IPlugin, IPluginTabIndexChanged, IPluginMainWindowLoaded, IPluginMainWindowResize, IPluginMainWindowClosing
    {
        private Dictionary<TabPage, PAK> _pakFiles = new Dictionary<TabPage, PAK>();
        public const string AllFilesPattern = "All Files (*.*)|*.*";
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
        private ToolStripStatusLabel? imageFormatLabel = null;

        private TextBox? numPivY    = null;
        private TextBox? numHeight  = null;
        private TextBox? numY       = null;
        private TextBox? numPivX    = null;
        private TextBox? numWidth   = null;
        private TextBox? numX       = null;

        private Pen _selectionPen = new Pen(Color.Yellow, 1);
        private Pen _unselectedPen = new Pen(Color.Red, 1);

        public PAKInterface(IPluginHostPublic pluginHost)
        {
            PluginHost = pluginHost;
        }

        private void newToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            TabPage newTab = CopyTemplateTab("New PAK File", "NewPakFile");
            _pakFiles[newTab] = PAK.CreateNewPak();
            _mainWindow!.OnTabPageAddition(newTab);
            LoadSelectedTab(newTab);
        }

        private void openToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = $"{PAK.FilePattern}|{AllFilesPattern}";
            ofd.Title = "Open PAK File";
            ofd.Multiselect = false;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string filePath = ofd.FileName;
                TabPage newTab = CopyTemplateTab(Path.GetFileName(filePath), Path.GetFileNameWithoutExtension(filePath).Replace(" ", ""));
                _pakFiles[newTab] = PAK.OpenPakFile(filePath);
                _mainWindow!.OnTabPageAddition(newTab);
                LoadSelectedTab(newTab);
            }
        }

        private void saveToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var tab = _selectedTab;
            if(tab == null)
            {
                MessageBox.Show("No tab selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_pakFiles.TryGetValue(tab, out var pakFile) && string.IsNullOrWhiteSpace(pakFile.FilePath))
            {
                saveAsToolStripMenuItem_Click(sender, e);
                return;
            }

            if (tab == null || !_pakFiles.ContainsKey(tab))
            {
                MessageBox.Show("No tab selected or PAK file not loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (pakFile == null)
            {
                MessageBox.Show("PAK file not loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            pakFile.Save(pakFile.FilePath);
            _mainWindow!.SetTabDirty(tab, false);
            MessageBox.Show($"PAK file saved successfully to {Path.GetFileName(pakFile.FilePath)}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void saveAsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var tab = _selectedTab;
            if (tab == null)
            {
                MessageBox.Show("No tab selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_pakFiles.TryGetValue(tab, out var pakFile) && string.IsNullOrWhiteSpace(pakFile.FilePath))
            {
                // Prompt for save location if not already set
                return;
            }

            if (tab == null || !_pakFiles.ContainsKey(tab))
            {
                MessageBox.Show("No tab selected or PAK file not loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (pakFile == null)
            {
                MessageBox.Show("PAK file not loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using SaveFileDialog sfd = new SaveFileDialog();

            sfd.Filter = $"{PAK.FilePattern}|{AllFilesPattern}";
            sfd.Title = "Save PAK File";
            sfd.FileName = tab.Text.Trim('*');
            sfd.RestoreDirectory = true;
            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            pakFile.Save(sfd.FileName);
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
            imageFormatLabel = new ToolStripStatusLabel("Unknown");
            statusStrip.Items.AddRange([
                scaleLabel,
                new ToolStripSeparator(),
                imageSizeLabel,
                new ToolStripSeparator(),
                imageFormatLabel
            ]);
            container.Panel1.Controls.Add(statusStrip);
            return newTab;
        }

        public void LoadSelectedTab(TabPage selectedTab)
        {
            if (!LoadSelectedTabControls())
                return;

            if (selectedTab == null || spriteTreeView == null)
            {
                MessageBox.Show("No tab selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!_pakFiles.TryGetValue(selectedTab, out var pak) || pak == null)
            {
                MessageBox.Show("Failed to load PAK file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var nodes = spriteTreeView.Nodes;
            nodes.Clear();

            for (int i = 0; i < pak.Sprites.Count; i++)
            {
                AddSprite(pak.Sprites[i], pak.Sprites[i].spriteRectangles, true);
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

            if (_selectedTab == null || !_pakFiles.TryGetValue(_selectedTab, out var pak))
            {
                MessageBox.Show("No PAK file loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            pak.Sprites.Remove(sprite);
            spriteTreeView.Nodes.Remove(node);
            _mainWindow!.SetTabDirty(_selectedTab, true);
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

            if (_selectedTab == null || !_pakFiles.TryGetValue(_selectedTab, out var pak))
            {
                MessageBox.Show("No PAK file loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (node.Parent?.Tag is not Sprite sprite)
            {
                MessageBox.Show("No sprite found for the selected rectangle.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            sprite.spriteRectangles.Remove(rect);
            node.Remove();
            _mainWindow!.SetTabDirty(_selectedTab, true);
        }

        private void AddSprite(Sprite sprite, List<SpriteRectangle> rects, bool nodeOnly = false)
        {
            if(spriteTreeView == null)
            {
                MessageBox.Show("Sprite TreeView not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if(!nodeOnly)
                _pakFiles[_selectedTab!].Sprites.Add(sprite);
            var spriteNode = new TreeNode($"Sprite {spriteTreeView.Nodes.Count}") { Tag = sprite };
            foreach (var rect in rects)
            {
                AddRectangle(spriteNode, sprite, rect, nodeOnly);
            }
            spriteTreeView.Nodes.Add(spriteNode);
            _mainWindow!.SetTabDirty(_selectedTab!, true);
        }

        private void AddRectangle(TreeNode node, Sprite sprite, SpriteRectangle rect, bool nodeOnly = false)
        {
            if (!nodeOnly)
            {
                sprite.spriteRectangles.Add(rect);
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

                    Sprite? sprite = node.Tag as Sprite;
                    SpriteRectangle? spriteRectangle = node.Tag as SpriteRectangle;

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
                            new ToolStripMenuItem("Export Sprite", null, (s, ev) =>
                            {
                                sprite.ExportSprite();
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
                            Sprite.GetAllAcceptedImageFileTypes(out string extension, out string filter);
                            ofd.Filter = filter;

                            if(ofd.ShowDialog() != DialogResult.OK)
                                return;

                            AddSprite(Sprite.CreateNewSprite(new Bitmap(Image.FromFile(ofd.FileName))), []);
                        }),
                        ]);
                }
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

            int checkerSize = 10;
            Color light = Color.LightGray;
            Color dark = Color.Gray;

            // Only draw checkerboard in the area where the image will be drawn
            float imgW = bmp.Width * zoomScale;
            float imgH = bmp.Height * zoomScale;
            RectangleF imageArea = new RectangleF(x, y, imgW, imgH);

            using SolidBrush lightBrush = new SolidBrush(light);
            using SolidBrush darkBrush = new SolidBrush(dark);

            // Clip to image area
            g.SetClip(imageArea);

            // Draw checkerboard
            for (int cy = 0; cy < imgH; cy += checkerSize)
            {
                for (int cx = 0; cx < imgW; cx += checkerSize)
                {
                    bool isDark = ((cx / checkerSize) + (cy / checkerSize)) % 2 == 0;
                    RectangleF tile = new RectangleF(x + cx, y + cy, checkerSize, checkerSize);
                    g.FillRectangle(isDark ? darkBrush : lightBrush, tile);
                }
            }

            // Reset clip
            g.ResetClip();

            g.DrawImage(bmp, x, y, bmp.Width * zoomScale, bmp.Height * zoomScale);

            SpriteRectangle? spriteRectangle = null;
            if (selectedNode.Tag is SpriteRectangle r)
            {
                spriteRectangle = r;
                selectedNode = selectedNode.Parent;
            }

            Sprite? sprite = selectedNode?.Tag as Sprite;
            if (sprite == null)
                return;

            foreach (SpriteRectangle rect in sprite.spriteRectangles)
            {
                if (rect == spriteRectangle)
                    continue;
                g.DrawRectangle(_unselectedPen, rect.ToRectangleF(x, y, zoomScale));
            }

            if (spriteRectangle != null)
            {
                g.DrawRectangle(_selectionPen, spriteRectangle.ToRectangleF(x, y, zoomScale));
            }

            if (statusStrip != null)
            {
                scaleLabel!.Text = $"{(zoomScale * 100.0f).ToString("0.00")}%";
                imageSizeLabel!.Text = $"{sprite.sprite?.Width}x{sprite.sprite?.Height}";
                imageFormatLabel!.Text = sprite.sprite?.PixelFormat.ToString();
            }
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
                pbSpriteImage.Image = sprite.sprite;
            }
            else if (node.Tag is SpriteRectangle rect)
            {
                if (node.Parent!.Tag is Sprite spr && pbSpriteImage.Image != spr.sprite)
                {
                    pbSpriteImage.Image = spr.sprite;
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

        public void OnMainWindowLoaded()
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
                ]);

            for (int i = 0; i < newDropDownItems.Count; i++)
            {
                dropDownItems.Insert(i, newDropDownItems[i]);
            }
            _mainWindow.ClosedState.AddRange(["openPakFileToolStripMenuItem", "newPakFileToolStripMenuItem"]);
            _mainWindow.OpenState.AddRange(["openPakFileToolStripMenuItem", "savePakFileToolStripMenuItem", "saveAsPakFileToolStripMenuItem", "newPakFileToolStripMenuItem"]);
        }

        public bool OnMainWindowClosing()
        {
            if (_pakFiles.Count == 0)
                return false;

            if(_mainWindow == null)
            {
                PluginHost.Log("IPluginMainWindow is not available.");
                return false;
            }

            if (_pakFiles.Any(x => _mainWindow.IsTabDirty(x.Key)))
            {
                DialogResult result = MessageBox.Show("You have unsaved changes in one or more PAK files. Do you want to save them before closing?", "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                switch(result)
                {
                    case DialogResult.Yes:
                        break;
                    case DialogResult.No:
                        return false;
                    case DialogResult.Cancel:
                        return true; // Cancel closing
                }

                foreach (var (tab, pak) in _pakFiles)
                {
                    if (_mainWindow.IsTabDirty(tab))
                    {
                        pak.Save(pak.FilePath);
                        _mainWindow!.SetTabDirty(tab, false);
                    }
                }
            }

            return false;
        }
    }
}
