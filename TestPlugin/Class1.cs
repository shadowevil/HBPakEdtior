using PluginContracts;
using PluginContracts.HostInterfaces;

namespace TestPlugin
{
    public class TestPlugin : IPlugin, IPluginMainWindowLoaded, IPluginTreeViewActions
    {
        private IPluginHostPublic PluginHost { get; }
        IPluginHostPublic? IPlugin.PluginHost => PluginHost;
        public string Name => "Test Plugin";

        private MenuStrip? FileMenuStrip;
        private TabControl? MainTabControl;
        private TreeView? ActiveTreeView;
        private PictureBox? ActivePictureBox;
        private MenuStrip? PictureBoxMenuStrip;
        private TreeNode? SelectedTreeNode;

        public TestPlugin(IPluginHostPublic pluginHost)
        {
            PluginHost = pluginHost;
        }

        public void OnMainWindowLoaded()
        {
            FileMenuStrip = PluginHost.GetService<IPluginMainWindow>()?.FileMenuStrip;
            MainTabControl = PluginHost.GetService<IPluginMainWindow>()?.MainTabControl;
            ActiveTreeView = PluginHost.GetService<IPluginMainWindow>()?.ActiveTreeView;
            ActivePictureBox = PluginHost.GetService<IPluginMainWindow>()?.ActivePictureBox;
            PictureBoxMenuStrip = PluginHost.GetService<IPluginMainWindow>()?.PictureBoxMenuStrip;
            SelectedTreeNode = PluginHost.GetService<IPluginMainWindow>()?.SelectedTreeNode;
        }
    }
}
