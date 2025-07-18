using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginContracts
{
    public interface IPluginMainWindowLoaded
    {
        public void OnMainWindowLoaded();
    }

    public interface IPluginMainWindowResize
    {
        public void OnWindowResize();
    }

    public interface IPluginTabIndexChanged
    {
        public void OnSelectedTabChanged(TabPage tabPage);
    }
}
