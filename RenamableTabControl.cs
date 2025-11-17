using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DarkModeForms;

namespace HBPakEditor
{
    public class RenamableTabControl<T> : TabControl where T : TabPage
    {
        // Events/Delegates
        public Func<T, bool>? BeforeRename { get; set; }
        public Action<T, string>? AfterRename { get; set; }
        public Func<T, bool>? BeforeClose { get; set; }
        public Action<T>? AfterClose { get; set; }
        public Action<ContextMenuStrip>? BeforeContextMenuShown { get; set; }

        // Private fields
        private TextBox? _renameBox;
        private int _renamingTabIndex = -1;
        private readonly Dictionary<T, bool> _tabDirtyStates = new();
        private readonly Dictionary<T, ContextMenuStrip> _tabPageMenus = new();

        private ToolTip _tabToolTip;
        private int _lastHoveredTabIndex = -1;

        // Constructor
        public RenamableTabControl()
        {
            MouseDoubleClick += OnMouseDoubleClick;

            _tabToolTip = new ToolTip();
            _tabToolTip.InitialDelay = 300;
            _tabToolTip.AutoPopDelay = 5000;
            _tabToolTip.ReshowDelay = 100;
            _tabToolTip.ShowAlways = true;

            MouseMove += OnTabMouseMove;
            MouseLeave += OnTabMouseLeave;
        }

        private void OnTabMouseMove(object? sender, MouseEventArgs e)
        {
            int hoveredTabIndex = -1;

            for (int i = 0; i < TabCount; i++)
            {
                Rectangle tabRect = GetTabRect(i);
                if (tabRect.Contains(e.Location))
                {
                    hoveredTabIndex = i;
                    break;
                }
            }

            if (hoveredTabIndex != _lastHoveredTabIndex)
            {
                _lastHoveredTabIndex = hoveredTabIndex;
                _tabToolTip.Hide(this);

                if (hoveredTabIndex >= 0)
                {
                    T tabPage = GetTabPage(hoveredTabIndex);

                    if (tabPage is PAKTabPage pakTabPage && !string.IsNullOrEmpty(pakTabPage.FilePath))
                    {
                        _tabToolTip.Show(pakTabPage.FilePath, this, e.X, e.Y, 5000);
                    }
                }
            }
        }

        private void OnTabMouseLeave(object? sender, EventArgs e)
        {
            _lastHoveredTabIndex = -1;
            _tabToolTip.Hide(this);
        }

        // Public methods - Dirty state management
        public bool IsTabDirty(T tabPage)
        {
            return _tabDirtyStates.TryGetValue(tabPage, out bool isDirty) && isDirty;
        }

        public bool AreTabsDirty()
        {
            return _tabDirtyStates.Any(x => x.Value);
        }

        public void SetTabDirty(T tabPage, bool isDirty)
        {
            _tabDirtyStates[tabPage] = isDirty;
            UpdateTabText(tabPage, isDirty);
        }

        // Public methods - Context menu management
        public void SetTabContextMenu(T tab, ContextMenuStrip menu)
        {
            _tabPageMenus[tab] = menu;
        }

        public ContextMenuStrip? GetTabContextMenu(T tab)
        {
            return _tabPageMenus.TryGetValue(tab, out var menu) ? menu : null;
        }

        // Public methods - Tab access
        public new T? SelectedTab => base.SelectedTab as T;

        public T GetTabPage(int index)
        {
            return (T)TabPages[index];
        }

        // Public methods - Tab closing
        public void CloseTab(int index)
        {
            if (index < 0 || index >= TabCount) return;

            T tabPage = GetTabPage(index);

            bool canClose = BeforeClose?.Invoke(tabPage) ?? true;
            if (!canClose) return;

            TabPages.Remove(tabPage);
            _tabDirtyStates.Remove(tabPage);
            _tabPageMenus.Remove(tabPage);

            AfterClose?.Invoke(tabPage);
        }

        public void CloseTab(T tabPage)
        {
            int index = TabPages.IndexOf(tabPage);
            if (index >= 0)
            {
                CloseTab(index);
            }
        }

        // Protected overrides
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Right && SelectedTab != null)
            {
                Rectangle tabRect = GetTabRect(SelectedIndex);
                if (tabRect.Contains(e.Location))
                {
                    if (_tabPageMenus.TryGetValue(SelectedTab, out var menu))
                    {
                        menu.Show(this, e.Location);
                    }
                    else
                    {
                        ShowDefaultContextMenu(SelectedTab, e.Location);
                    }
                }
            }
        }

        // Private event handlers
        private void OnMouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 2 && SelectedTab != null)
            {
                if (GetTabRect(SelectedIndex).Contains(e.Location))
                {
                    BeginTabRename(SelectedIndex);
                }
            }
        }

        private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
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

        private void OnRenameBoxLostFocus(object? sender, EventArgs e)
        {
            CommitRename();
        }

        // Private helper methods
        private void ShowDefaultContextMenu(T tab, Point location)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Close", null, (s, e) => CloseTab(tab));
            if(BeforeContextMenuShown != null)
            {
                menu.Items.Add(new ToolStripSeparator());
                BeforeContextMenuShown.Invoke(menu);

                // If the user clears the context menu, add the Close option back
                if (menu == null)
                    menu = new ContextMenuStrip();

                if (menu.Items.Count <= 0)
                {
                    menu.Items.Add("Close", null, (s, e) => CloseTab(tab));
                }

                // If the first item is not close add it back
                else if (menu.Items[0].Text != "Close")
                {
                    menu.Items.Insert(0, new ToolStripMenuItem("Close", null, (s, e) => CloseTab(tab)));
                    if(menu.Items.Count > 1)
                        menu.Items.Add(new ToolStripSeparator());
                }
            }
            menu.Show(this, location);
        }

        private void BeginTabRename(int tabIndex)
        {
            if (_renameBox != null || tabIndex < 0 || tabIndex >= TabCount)
                return;

            T tabPage = GetTabPage(tabIndex);
            if (!CanRename(tabPage))
                return;

            _renamingTabIndex = tabIndex;
            CreateRenameTextBox(tabIndex);
        }

        private bool CanRename(T tabPage)
        {
            bool canRename = BeforeRename?.Invoke(tabPage) ?? true;
            if (!canRename)
            {
                _renamingTabIndex = -1;
            }
            return canRename;
        }

        private void CreateRenameTextBox(int tabIndex)
        {
            Rectangle tabRect = GetTabRect(tabIndex);
            Point parentLocation = Parent!.PointToClient(PointToScreen(tabRect.Location));

            _renameBox = new TextBox
            {
                Bounds = new Rectangle(parentLocation.X + 2, parentLocation.Y, tabRect.Width - 4, tabRect.Height - 6),
                Font = Font,
                Text = TabPages[tabIndex].Text.TrimEnd('*'),
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle,
                Parent = Parent
            };

            _renameBox.LostFocus += OnRenameBoxLostFocus;
            _renameBox.KeyDown += OnRenameBoxKeyDown;
            _renameBox.BringToFront();
            _renameBox.Focus();
            _renameBox.SelectAll();
        }

        private void CommitRename()
        {
            if (_renameBox != null && _renamingTabIndex >= 0)
            {
                T tabPage = GetTabPage(_renamingTabIndex);
                string newName = _renameBox.Text;
                tabPage.Text = newName;
                AfterRename?.Invoke(tabPage, newName);
                UpdateTabText(tabPage, IsTabDirty(tabPage));
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
                _renameBox.LostFocus -= OnRenameBoxLostFocus;
                _renameBox.KeyDown -= OnRenameBoxKeyDown;
                Controls.Remove(_renameBox);
                _renameBox.Dispose();
                _renameBox = null;
            }

            _renamingTabIndex = -1;
        }

        private void UpdateTabText(T tabPage, bool isDirty)
        {
            string baseText = tabPage.Text.TrimEnd('*');
            tabPage.Text = isDirty ? $"{baseText}*" : baseText;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tabToolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}