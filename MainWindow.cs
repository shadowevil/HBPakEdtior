using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace HBPakEditor
{
    public partial class MainWindow
    {
        public static void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });

            foreach (Control child in control.Controls)
            {
                EnableDoubleBuffering(child);
            }
        }

        public MainWindow()
        {
            InitializeComponents();
            EnableDoubleBuffering(this);
        }

        private void OnBeforeContextMenuShown_Tab(ContextMenuStrip strip)
        {
            strip.Items.Add(new ToolStripMenuItem("Set Encryption Key", null, (s, e) =>
            {
                var selectedTab = pakTabControl.SelectedTab;
                if (selectedTab == null)
                    return;
                var config = new InputBoxConfiguration
                {
                    HideInput = true,
                    MinLength = 1,
                    Required = true
                };
                var result = InputBox.Show("Enter new encryption key:", "Set Encryption Key", out string outkey, config);
                if (result != DialogResult.OK)
                    return;
                selectedTab.KeyBytes = Encoding.UTF8.GetBytes(outkey);
                selectedTab.Text = Path.ChangeExtension(selectedTab.Text, ".epak");
                selectedTab.Name = Path.GetFileName(selectedTab.FilePath!) + Guid.NewGuid().ToString();
                selectedTab.FilePath = Path.ChangeExtension(selectedTab.FilePath, ".epak");
                pakTabControl.SetTabDirty(selectedTab, true);
            }));
        }

        private void OnAfterClose_Tab(PAKTabPage page)
        {
            // Check if there are no non-empty tabs left
            bool hasNoTabs = !pakTabControl.TabPages.Cast<PAKTabPage>()
                .Any(t => t is not PAKTabEmpty);

            if (hasNoTabs)
            {
                // Add empty tab after closing the last real tab
                pakTabControl.TabPages.Add(new PAKTabEmpty(this));
                pakTabControl.Enabled = false;
                saveToolStripMenuItem.Enabled = false;
                saveAsToolStripMenuItem.Enabled = false;
                saveAllToolStripMenuItem.Enabled = false;
                exportAllSpritesToolStripMenuItem.Enabled = false;
                importAllSpritesToolStripMenuItem.Enabled = false;
                compactAllSpritesToolStripMenuItem.Enabled = false;

                saveToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Save);
                saveAsToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.SaveAs);
                saveAllToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.SaveAll);
                exportAllSpritesToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Export);
                importAllSpritesToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Import);
                compactAllSpritesToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Compress);
            }
        }

        private bool OnBeforeClose_Tab(PAKTabPage page)
        {
            // Never allow closing the empty tab
            if (page is PAKTabEmpty)
                return false;

            // Check if tab is dirty
            if (pakTabControl.IsTabDirty(page))
            {
                var result = MessageBox.Show(
                    $"Do you want to save changes to {page.Text}?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Cancel)
                {
                    return false; // Cancel the close
                }

                if (result == DialogResult.Yes)
                {
                    SaveToolStripMenuItem_Click(this, EventArgs.Empty);

                    // If still dirty after save attempt, cancel close
                    if (pakTabControl.IsTabDirty(page))
                    {
                        return false;
                    }
                }
                // If result is No, continue with close
            }

            return true;
        }

        private void OnAfterRename_Tab(PAKTabPage page, string new_name)
        {
            if (page.FilePath == null)
                throw new NullReferenceException("This should not happen!");

            string current_directory = Path.GetDirectoryName(page.FilePath!)!;

            // new_name already has valid extension
            if (Path.HasExtension(new_name) &&
                (Path.GetExtension(new_name).Equals(".pak", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(new_name).Equals(".epak", StringComparison.OrdinalIgnoreCase)))
            {
                page.FilePath = Path.Combine(current_directory, new_name);
                page.Text = Path.GetFileName(page.FilePath);
                return;
            }

            // Remove invalid extension
            if (Path.HasExtension(new_name) &&
                !(Path.GetExtension(new_name).Equals(".pak", StringComparison.OrdinalIgnoreCase) ||
                  Path.GetExtension(new_name).Equals(".epak", StringComparison.OrdinalIgnoreCase)))
            {
                new_name = Path.GetFileNameWithoutExtension(new_name);
            }

            // Assign correct extension based on key presence
            string extension = page.KeyBytes.Length > 0 ? ".epak" : ".pak";
            page.FilePath = Path.Combine(current_directory, new_name + extension);
            page.Text = Path.GetFileName(page.FilePath);
            page.Name = Path.GetFileName(page.FilePath) + Guid.NewGuid().ToString();
            pakTabControl.SetTabDirty(page, true);
        }

        private bool OnBeforeRename_Tab(PAKTabPage page)
        {
            if (page is PAKTabEmpty)
                return false;
            return true;
        }

        private void NewToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            CreateNewPAK();
        }

        private void ImportAllSpritesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            PAKTabTemplate? selectedTab = (PAKTabTemplate?)pakTabControl.SelectedTab;
            if (selectedTab == null)
                return;

            var OpenPAK = selectedTab.OpenPAK;
            if (OpenPAK == null)
                return;

            if (OpenPAK?.Data != null)
            {
                var result = MessageBox.Show("Import sprite rectangles along with images?", "Import Options", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if(result == DialogResult.Cancel)
                    return;
                bool ImportRectangles = result == DialogResult.Yes;
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select folder to import sprites";
                    fbd.ShowNewFolderButton = true;
                    fbd.UseDescriptionForTitle = true;

                    if (fbd.ShowDialog() != DialogResult.OK)
                        return;

                    List<string> sprites = [.. Directory.GetFiles(fbd.SelectedPath, "*.bmp"), .. Directory.GetFiles(fbd.SelectedPath, "*.png")];
                    sprites.Sort((a, b) => CompareSpriteNames(a, b));
                    List<string> rectangles = [];
                    if (ImportRectangles)
                    {
                        rectangles = [.. Directory.GetFiles(fbd.SelectedPath, "*.json")];
                        rectangles.Sort((a, b) => CompareSpriteNames(a, b));

                        if (sprites.Count != rectangles.Count)
                        {
                            MessageBox.Show("You chose to import rectangles as well, there is a missmatch between sprites and rectangles.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    int errors = 0;
                    for(int i=0;i<sprites.Count;i++)
                    {
                        var path = sprites[i];
                        PAKLib.Sprite spr = new();
                        spr.data = File.ReadAllBytes(path);
                        List<PAKLib.SpriteRectangle>? rects = null;

                        if (ImportRectangles)
                        {
                            rects = JsonConvert.DeserializeObject<List<PAKLib.SpriteRectangle>>(File.ReadAllText(rectangles[i]));
                            if (rects == null)
                                errors++;
                        }
                        spr.Rectangles = rects ?? new();
                        OpenPAK.Data.Sprites.Add(spr);
                    }
                    if(errors != 0)
                        MessageBox.Show("Failed to read json, specifically rectangles...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    selectedTab.PopulateTreeItems();
                    pakTabControl.SetTabDirty(selectedTab, true);
                }
            }
        }

        public static int CompareSpriteNames(string a, string b)
        {
            int na = ExtractTrailingNumber(a);
            int nb = ExtractTrailingNumber(b);

            if (na >= 0 && nb >= 0)
                return na.CompareTo(nb);

            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        private static int ExtractTrailingNumber(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path); // Whr_sprite_0
            int underscore = name.LastIndexOf('_');
            if (underscore < 0)
                return -1;

            string tail = name[(underscore + 1)..]; // "0"
            return int.TryParse(tail, out int n) ? n : -1;
        }

        private void ExportAllSpritesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var selectedTab = pakTabControl.SelectedTab;
            if (selectedTab == null)
                return;

            var OpenPAK = selectedTab.OpenPAK;
            if (OpenPAK == null)
                return;

            if (OpenPAK?.Data != null)
            {
                var result = MessageBox.Show("Export sprite rectangles along with image?", "Export Options", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel)
                    return;
                bool exportRectangles = result == DialogResult.Yes;
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select folder to export sprites";
                    fbd.ShowNewFolderButton = true;
                    fbd.UseDescriptionForTitle = true;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        for (int i = 0; i < OpenPAK.Data.Sprites.Count; i++)
                        {
                            var sprite = OpenPAK.Data.Sprites[i];
                            if (sprite != null)
                            {
                                // Check data signature to determine file extension
                                string fileExtension = FileSignatureDetector.GetFileExtension(sprite.data) ?? ".bin";
                                string safeName = Path.GetFileNameWithoutExtension(selectedTab.FilePath!) + $"_sprite_{i}{fileExtension}";
                                string safePath = Path.Combine(fbd.SelectedPath, safeName);
                                File.WriteAllBytes(safePath, sprite.data);
                                if (exportRectangles)
                                {
                                    string rectFileName = Path.GetFileName(Path.ChangeExtension(safeName, ".json"));
                                    rectFileName = rectFileName.Replace("_sprite_", "_rectangles_");
                                    string rectDestPath = Path.Combine(fbd.SelectedPath, rectFileName);
                                    File.WriteAllText(rectDestPath, JsonConvert.SerializeObject(sprite.Rectangles, Formatting.Indented));
                                }
                            }
                        }
                    }
                }
            }
        }

        private void SaveAllToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            for(int i=0; i < pakTabControl.TabPages.Count; i++)
            {
                if (!pakTabControl.IsTabDirty((PAKTabPage)pakTabControl.TabPages[i]))
                    continue;

                pakTabControl.SelectedIndex = i;
                SaveToolStripMenuItem_Click(sender, e);
            }
        }

        private void OpenToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "PAK Files (*.pak;*.epak)|*.pak;*.epak|All Files (*.*)|*.*";
                openFileDialog.Title = "Open PAK File";
                openFileDialog.Multiselect = true;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Title = "Open PAK File";

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                if(openFileDialog.FileNames.Length == 0)
                    return;

                if (openFileDialog.FileNames.Length == 1)
                {
                    AddNewPAKTab(openFileDialog.FileNames.First());
                    pakTabControl.SelectedIndex = pakTabControl.TabCount - 1;
                }
                else
                {
                    foreach (var filePath in openFileDialog.FileNames)
                        AddNewPAKTab(filePath);
                }
                saveToolStripMenuItem.Enabled = true;
                saveAsToolStripMenuItem.Enabled = true;
                saveAllToolStripMenuItem.Enabled = true;
                exportAllSpritesToolStripMenuItem.Enabled = true;
                importAllSpritesToolStripMenuItem.Enabled = true;
                compactAllSpritesToolStripMenuItem.Enabled = true;

                saveToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Save);
                saveAsToolStripMenuItem.Image = IconFactory.GetIcon(IconType.SaveAs);
                saveAllToolStripMenuItem.Image = IconFactory.GetIcon(IconType.SaveAll);
                exportAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Export);
                importAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Import);
                compactAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Compress);
            }
        }

        private void ExitToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!pakTabControl.AreTabsDirty())
            {
                Application.Exit();
                return;
            }

            if(MessageBox.Show("Are you sure you want to exit?", "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!pakTabControl.AreTabsDirty())
            {
                base.OnFormClosing(e);
                return;
            }

            if (MessageBox.Show("Are you sure you want to exit?", "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
            } else
            {
                base.OnFormClosing(e);
            }
        }

        private bool CheckKey(PAKTabPage page, string filepath, out string outkey)
        {
            var pak = page.OpenPAK;
            string intkey = Encoding.UTF8.GetString(page.KeyBytes);

            if (string.IsNullOrEmpty(intkey) && Path.GetExtension(filepath) == ".epak")
            {
                var config = new InputBoxConfiguration
                {
                    HideInput = true,
                    MinLength = 1,
                    Required = true
                };
                var result = InputBox.Show("Enter key:", "Encryption Key Input", out string _outkey, config);
                if (result != DialogResult.OK)
                {
                    var res2 = MessageBox.Show("Canceled encryption key, save as unencrypted PAK file?", "Alert", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (res2 == DialogResult.Yes)
                    {
                        outkey = string.Empty;
                        return true;
                    }
                    else
                    {
                        outkey = string.Empty;
                        return false;
                    }
                }
                else
                {
                    outkey = _outkey;
                    return true;
                }
            }
            outkey = intkey;
            return true;
        }

        private void SaveAsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var selectedTab = pakTabControl.SelectedTab;
            if (selectedTab == null)
                return;

            if (selectedTab.OpenPAK == null)
                return;

            try
            {
                if (!CheckKey(selectedTab, selectedTab.FilePath ?? "", out string key))
                    return;

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PAK Files (*.pak;*.epak)|*.pak;*.epak";
                    sfd.DefaultExt = ".pak";
                    sfd.RestoreDirectory = true;

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    if (!CheckKey(selectedTab, sfd.FileName, out string key2))
                        return;

                    key = key2;

                    PAKLib.PAK.SaveToFile(selectedTab.OpenPAK, sfd.FileName, key);
                    MessageBox.Show("PAK file saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    pakTabControl.SetTabDirty(selectedTab, false);
                    selectedTab.FilePath = sfd.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var selectedTab = pakTabControl.SelectedTab;
            if(selectedTab == null)
                return;

            if(selectedTab.OpenPAK == null)
                return;

            if (string.IsNullOrEmpty(selectedTab.FilePath))
            {
                SaveAsToolStripMenuItem_Click(sender, e);
                return;
            }

            try
            {
                if (!CheckKey(selectedTab, selectedTab.FilePath!, out string key))
                    return;

                PAKLib.PAK.SaveToFile(selectedTab.OpenPAK, selectedTab.FilePath!, key);
                MessageBox.Show("PAK file saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                pakTabControl.SetTabDirty(selectedTab, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void AddNewPAKTab(string filePath)
        {
            PAKTabTemplate newTabPage = new PAKTabTemplate(this);
            try
            {
                // Check and ask for encryption key as needed
                bool isEncrypted = Path.GetExtension(filePath) == ".epak";
                string key = "";
                if (isEncrypted)
                {
                    var config = new InputBoxConfiguration
                    {
                        HideInput = true,
                        MinLength = 1,
                        Required = true
                    };
                    var result = InputBox.Show("Enter key:", "Encryption Key Input", out string outkey, config);
                    if (result != DialogResult.OK)
                        return;
                    key = outkey;
                }

                // Open and create PAK
                newTabPage.OpenPAK = PAKLib.PAK.ReadFromFile(filePath, key);
                newTabPage.Text = Path.GetFileName(filePath);
                newTabPage.Name = Path.GetFileName(filePath) + Guid.NewGuid().ToString();
                newTabPage.FilePath = filePath;
                newTabPage.KeyBytes = Encoding.UTF8.GetBytes(key);      // Store key as a byte array and not plain text.

                // Clear the empty tab
                if (pakTabControl.TabPages.Count == 1 && pakTabControl.TabPages[0].Text.ToLower() == "empty" && !pakTabControl.Enabled)
                    pakTabControl.TabPages.Clear();

                newTabPage.PopulateTreeItems();

                // Always add the new tab
                pakTabControl.TabPages.Add(newTabPage);

                // Always enable the pakTabControl
                pakTabControl.Enabled = true;

                saveToolStripMenuItem.Enabled = true;
                saveAsToolStripMenuItem.Enabled = true;
                saveAllToolStripMenuItem.Enabled = true;
                exportAllSpritesToolStripMenuItem.Enabled = true;
                importAllSpritesToolStripMenuItem.Enabled = true;
                compactAllSpritesToolStripMenuItem.Enabled = true;

                saveToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Save);
                saveAsToolStripMenuItem.Image = IconFactory.GetIcon(IconType.SaveAs);
                saveAllToolStripMenuItem.Image = IconFactory.GetIcon(IconType.SaveAll);
                exportAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Export);
                importAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Import);
                compactAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Compress);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void CreateNewPAK()
        {
            PAKTabTemplate newTabPage = new PAKTabTemplate(this);
            try
            {
                // Open and create PAK
                newTabPage.OpenPAK = new PAKLib.PAK();
                newTabPage.OpenPAK.Data = new PAKLib.PAKData();
                newTabPage.Text = "New File.pak";
                newTabPage.Name = "New File" + Guid.NewGuid().ToString();
                newTabPage.FilePath = "";
                newTabPage.KeyBytes = [];

                // Clear the empty tab
                if (pakTabControl.TabPages.Count == 1 && pakTabControl.TabPages[0].Text.ToLower() == "empty" && !pakTabControl.Enabled)
                    pakTabControl.TabPages.Clear();

                newTabPage.PopulateTreeItems();

                // Always add the new tab
                pakTabControl.TabPages.Add(newTabPage);

                // Always enable the pakTabControl
                pakTabControl.Enabled = true;

                saveToolStripMenuItem.Enabled = true;
                saveAsToolStripMenuItem.Enabled = true;
                saveAllToolStripMenuItem.Enabled = true;
                exportAllSpritesToolStripMenuItem.Enabled = true;
                importAllSpritesToolStripMenuItem.Enabled = true;
                compactAllSpritesToolStripMenuItem.Enabled = true;

                saveToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Save);
                saveAsToolStripMenuItem.Image = IconFactory.GetIcon(IconType.SaveAs);
                saveAllToolStripMenuItem.Image = IconFactory.GetIcon(IconType.SaveAll);
                exportAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Export);
                importAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Import);
                compactAllSpritesToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Compress);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // CTRL + S => Save current active PAK File
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveToolStripMenuItem_Click(this, EventArgs.Empty);
                return true;
            }

            // CTRL + SHIFT + S => Save current active PAK File AS...
            if (keyData == (Keys.Control | Keys.Shift | Keys.S))
            {
                SaveAsToolStripMenuItem_Click(this, EventArgs.Empty);
                return true;
            }

            // CTRL + ALT + S => Save all tabs
            if (keyData == (Keys.Control | Keys.Alt | Keys.S))
            {
                SaveAllToolStripMenuItem_Click(this, EventArgs.Empty);
                return true;
            }

            var paktab = pakTabControl.SelectedTab as PAKTabTemplate;

            if (keyData == (Keys.Control | Keys.Z))
            {
                paktab?.UndoManager.Undo();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Y))
            {
                paktab?.UndoManager.Redo();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void EditToolStripMenuItem_DropDownOpening(object? sender, EventArgs e)
        {
            var paktab = pakTabControl.SelectedTab as PAKTabTemplate;

            if (paktab == null)
            {
                undoToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Undo);
                redoToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Redo);
                undoToolStripMenuItem.Enabled = false;
                redoToolStripMenuItem.Enabled = false;
                return;
            }

            if (paktab!.UndoManager.CanRedo)
            {
                redoToolStripMenuItem.Enabled = true;
                redoToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Redo);
            }
            else
            {
                redoToolStripMenuItem.Enabled = false;
                redoToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Redo);
            }

            if (paktab!.UndoManager.CanUndo)
            {
                undoToolStripMenuItem.Enabled = true;
                undoToolStripMenuItem.Image = IconFactory.GetIcon(IconType.Undo);
            }
            else
            {
                undoToolStripMenuItem.Enabled = false;
                undoToolStripMenuItem.Image = IconFactory.GetIconDisabled(IconType.Undo);
            }
        }

        private void RedoToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var paktab = pakTabControl.SelectedTab as PAKTabTemplate;
            paktab?.UndoManager.Redo();
        }

        private void UndoToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var paktab = pakTabControl.SelectedTab as PAKTabTemplate;
            paktab?.UndoManager.Undo();
        }

        private void CompactAllSpritesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("Do you want to compact all sprites in the current PAK?", "Confirm Compact", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if(result == DialogResult.Cancel)
                return;

            bool directoryMode = result == DialogResult.No;
            bool pakMode = result == DialogResult.Yes;

            if (directoryMode)
            {
                // Default directory mode
                SpritePacker.SpritePackerForm compact = new SpritePacker.SpritePackerForm();
                darkMode.ThemeControl(compact);
                if(compact.ShowDialog(this) == DialogResult.Yes)
                {
                    var tab = pakTabControl.SelectedTab as PAKTabTemplate;
                    tab.OpenPAK!.Data.Sprites.AddRange(compact.GetSprites());
                    tab.PopulateTreeItems();
                    pakTabControl.SetTabDirty(tab, true);
                }
            } else if(pakMode)
            {
                var tab = pakTabControl.SelectedTab as PAKTabTemplate;
                SpritePacker.SpritePackerForm compact = new SpritePacker.SpritePackerForm(tab!.OpenPAK!, Path.GetFileNameWithoutExtension(tab.Name));
                darkMode.ThemeControl(compact);
                if(compact.ShowDialog(this) == DialogResult.OK)
                {
                    tab.OpenPAK!.Data.Sprites.AddRange(compact.GetSprites());
                    tab.PopulateTreeItems();
                    pakTabControl.SetTabDirty(tab, true);
                }
            }
        }
    }
}
