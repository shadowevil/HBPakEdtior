using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HBPakEditor
{
    public class RenamableTabControl : TabControl
    {
        public Func<bool>? BeforeRename { get; set; } = null;
        public Action<string>? AfterRename { get; set; } = null;

        private TextBox? _renameBox = null;
        private int _renamingTabIndex = -1;

        private Dictionary<TabPage, bool> _tabDirtyStates = new Dictionary<TabPage, bool>();
        private readonly Dictionary<TabPage, ContextMenuStrip> _tabPageMenus = new();

        public bool IsTabDirty(TabPage tabPage)
        {
            return _tabDirtyStates.TryGetValue(tabPage, out bool isDirty) && isDirty;
        }

        public void SetTabDirty(TabPage tabPage, bool isDirty)
        {
            _tabDirtyStates[tabPage] = isDirty;
            tabPage.Text = isDirty ? $"{tabPage.Text.TrimEnd('*')}*" : tabPage.Text.TrimEnd('*');
        }

        public RenamableTabControl()
        {
            this.MouseDoubleClick += RenamableTabControl_MouseDoubleClick;
        }

        public void SetTabContextMenu(TabPage tab, ContextMenuStrip menu)
        {
            _tabPageMenus[tab] = menu;
        }

        public ContextMenuStrip? GetTabContextMenu(TabPage tab)
        {
            return _tabPageMenus.TryGetValue(tab, out var menu) ? menu : null;
        }

        private void RenamableTabControl_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 2)
            {
                if (this.SelectedTab != null && this.GetTabRect(this.SelectedIndex).Contains(e.Location))
                {
                    BeginTabRename(this.SelectedIndex);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                if (this.SelectedTab != null && _tabPageMenus.TryGetValue(this.SelectedTab, out var selectedMenu) && this.GetTabRect(this.SelectedIndex).Contains(e.Location))
                {
                    selectedMenu.Show(this, e.Location);
                }
            }
        }


        private void BeginTabRename(int tabIndex)
        {
            if (_renameBox != null || tabIndex < 0 || tabIndex >= this.TabCount)
                return;

            _renamingTabIndex = tabIndex;
            Rectangle tabRect = this.GetTabRect(tabIndex);

            // Translate tabRect to screen coordinates and back to parent control
            Point screenLocation = this.PointToScreen(tabRect.Location);
            Point parentLocation = this.Parent!.PointToClient(screenLocation);

            bool canRename = BeforeRename?.Invoke() ?? true;
            if (!canRename)
            {
                _renamingTabIndex = -1;
                return;
            }

            _renameBox = new TextBox
            {
                Bounds = new Rectangle(parentLocation.X + 2, parentLocation.Y, tabRect.Width - 4, tabRect.Height - 6),
                Font = this.Font,
                Text = this.TabPages[tabIndex].Text.TrimEnd('*'),
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle,
                Parent = this.Parent
            };

            _renameBox.LostFocus += EndTabRename;
            _renameBox.KeyDown += RenameBox_KeyDown;

            _renameBox.BringToFront();
            _renameBox.Focus();
            _renameBox.SelectAll();
        }


        private void RenameBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitRename();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelRename();
            }
        }

        private void EndTabRename(object? sender, EventArgs e)
        {
            CommitRename();
        }

        private void CommitRename()
        {
            if (_renameBox != null && _renamingTabIndex >= 0)
            {
                this.TabPages[_renamingTabIndex].Text = _renameBox.Text;
                AfterRename?.Invoke(_renameBox.Text);
                SetTabDirty(this.TabPages[_renamingTabIndex], IsTabDirty(this.TabPages[_renamingTabIndex])); // Mark the tab as dirty after renaming
            }

            CleanupRename();
        }

        private void CancelRename()
        {
            CleanupRename();
        }

        private void CleanupRename()
        {
            if (_renameBox != null)
            {
                _renameBox.LostFocus -= EndTabRename;
                _renameBox.KeyDown -= RenameBox_KeyDown;
                this.Controls.Remove(_renameBox);
                _renameBox.Dispose();
                _renameBox = null;
            }

            _renamingTabIndex = -1;
        }
    }
}