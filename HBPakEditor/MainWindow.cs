using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms.VisualStyles;
using DarkModeForms;
using PluginContracts.HostInterfaces;

namespace HBPakEdtior
{
    public partial class MainWindow : Form, IPluginMainWindow
    {
        public MenuStrip? FileMenuStrip => menuStrip1;
        public TabControl? MainTabControl => tabControl1;
        public TabPage? TabTemplate => _tabTemplate;
        public TabPage? EmptyTab => _emptyTab;

        private List<string> _openState = ["closeToolStripMenuItem"];
        public List<string> OpenState { get => _openState; set => _openState = value; }

        private List<string> _closedState = ["closeToolStripMenuItem"];
        public List<string> ClosedState { get => _closedState; set => _closedState = value; }

        private PluginManager _pluginManager;
        private DarkModeCS _darkMode;
        private const string DefaultWindowName = "HB Pak Editor";

        private readonly TabPage _tabTemplate;
        private readonly TabPage _emptyTab;

        public MainWindow()
        {
            // Theme Setup
            _darkMode = new DarkModeCS(this, true, false)
            {
                ColorMode = DarkModeCS.DisplayMode.SystemDefault
            };

            // Component Init
            InitializeComponent();
            RecursivelySetDoubleBuffered(this);

            // Tab Setup
            _tabTemplate = tabControl1.TabPages.OfType<TabPage>().First(x => x.Name == "tabTemplate").DeepClone();
            tabControl1.TabPages.Clear();

            _emptyTab = new TabPage("Empty Tab")
            {
                Name = "emptyTab",
                UseVisualStyleBackColor = true
            };
            _emptyTab.Controls.Add(new Label
            {
                Text = "No file opened",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 16, FontStyle.Bold),
                ForeColor = Color.Gray
            });
            tabControl1.TabPages.Add(_emptyTab);

            // Event Subscriptions
            darkModeToolStripMenuItem.CheckedChanged += darkModeMenuItem_CheckChanged;
            darkModeToolStripMenuItem.Checked = _darkMode.isDarkMode();

            tabControl1.BeforeRename += TabControl_BeforeRename;
            tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;

            _pluginManager = PluginManager.Instance;
            _pluginManager.PluginHost.RegisterService<IPluginMainWindow>(this);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _pluginManager.OnMainWindowLoaded();

            SetMenuState(_closedState.ToArray());
            SetWindowName();
        }

        private void TabControl1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == null)
                return;

            SetWindowName();
            _pluginManager.OnSelectedTabChanged(tabControl1.SelectedTab);
            SetMenuState(_openState.ToArray());
        }

        private void SetWindowName()
        {
            this.Text = tabControl1.SelectedTab != null && tabControl1.SelectedTab.Name != "emptyTab"
                ? $"{DefaultWindowName} - {tabControl1.SelectedTab.Text}"
                : DefaultWindowName;
        }

        private bool TabControl_BeforeRename()
        {
            if (tabControl1.SelectedTab == null)
                return false;

            if (tabControl1.SelectedTab.Name == "emptyTab")
            {
                return false;
            }

            return true;
        }

        void IPluginMainWindow.OnTabPageAddition(TabPage page)
        {
            if(tabControl1.TabPages.Contains(_emptyTab))
            {
                tabControl1.TabPages.Clear();
                tabControl1.TabPages.Add(page);
                tabControl1.SelectedTab = page;
                SetMenuState(_openState.ToArray());
                return;
            }

            if (tabControl1.TabPages.Contains(page))
                return;

            tabControl1.TabPages.Add(page);
            tabControl1.SelectedTab = page;
            SetMenuState(_openState.ToArray());
            SetWindowName();
        }

        private void SetMenuState(string[] state)
        {
            fileToolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>().ToList().ForEach(x => x.Enabled = state.Contains(x.Name));
        }

        private void darkModeMenuItem_CheckChanged(object? sender, EventArgs e)
        {
            if (darkModeToolStripMenuItem.Checked)
            {
                darkModeToolStripMenuItem.BackColor = Color.FromArgb(128, Color.CornflowerBlue);
                _darkMode.ApplyTheme(true);
            }
            else
            {
                darkModeToolStripMenuItem.BackColor = Color.Transparent;
                _darkMode.ApplyTheme(false);
            }
        }

        void RecursivelySetDoubleBuffered(Control control)
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered", System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, control, new object[] { true }
                );

            foreach (Control child in control.Controls)
                RecursivelySetDoubleBuffered(child);
        }


        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            _pluginManager?.OnWindowResize();
        }
    }
}
