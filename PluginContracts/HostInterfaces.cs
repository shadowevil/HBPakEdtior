using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginContracts.HostInterfaces
{
    public interface IPluginMainWindow
    {
        MenuStrip? FileMenuStrip { get; }
        TabControl? MainTabControl { get; }
        TabPage? TabTemplate { get; }
        TabPage? EmptyTab { get; }
        List<string> OpenState { get; set; }
        List<string> ClosedState { get; set; }
        void OnTabPageAddition(TabPage page);
        bool IsTabDirty(TabPage page);
        void SetTabDirty(TabPage page, bool isDirty);
    }
}
