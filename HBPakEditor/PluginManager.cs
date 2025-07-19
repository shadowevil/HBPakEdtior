using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using PluginContracts;

namespace HBPakEdtior
{
    public class PluginHost : IPluginHostPublic
    {
        private readonly Dictionary<Type, object> _services = new();

        public T? GetService<T>() where T : class
        {
            return (T?)(_services.TryGetValue(typeof(T), out var svc) ? svc : null);
        }

        public void RegisterService<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        public void Log(string message)
        {
            // Implement logging logic here, e.g., write to a log file or console
            Debug.WriteLine($"[PluginHost] {message}");
        }
    }

    public class PluginManager
    {
        private static PluginManager _pluginManager = null!;
        public static PluginManager Instance => _pluginManager ??= new PluginManager();

        private PluginHost _pluginHost = null!;

        public PluginHost PluginHost => _pluginHost;

        private PluginManager()
        {
            // Singleton pattern to ensure only one instance of PluginManager exists
            if (_pluginManager == null)
            {
                _pluginManager = this;
                _pluginHost = new PluginHost();
                LoadPlugins("Plugins", _pluginHost);
            }
        }

        private readonly List<IPlugin> _plugins = new();

        public void LoadPlugins(string folder, IPluginHostPublic host)
        {
            foreach (var dll in Directory.GetFiles(folder, "*.dll"))
            {
                try
                {
                    var asm = Assembly.LoadFrom(dll);
                    foreach (var type in asm.GetTypes())
                    {
                        if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            if (Activator.CreateInstance(type, host) is IPlugin plugin)
                                _plugins.Add(plugin);
                        }
                    }
                }
                catch (Exception ex)
                {
                    host.Log($"Failed to load plugin from {dll}: {ex.Message}");
                }
            }
        }

        public void OnMainWindowLoaded()
        {
            foreach (var plugin in _plugins)
            {
                if (plugin is IPluginMainWindowLoaded mainWindowLoadedPlugin)
                {
                    mainWindowLoadedPlugin.OnMainWindowLoaded();
                }
            }
        }

        public void OnSelectedTabChanged(TabPage tabPage)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin is IPluginTabIndexChanged tabIndexChangedPlugin)
                {
                    tabIndexChangedPlugin.OnSelectedTabChanged(tabPage);
                }
            }
        }

        public void OnWindowResize()
        {
            foreach (var plugin in _plugins)
            {
                if (plugin is IPluginMainWindowResize resizePlugin)
                {
                    resizePlugin.OnWindowResize();
                }
            }
        }

        public bool OnMainWindowClosing()
        {
            foreach (var plugin in _plugins)
            {
                if (plugin is IPluginMainWindowClosing closingPlugin)
                {
                    if (!closingPlugin.OnMainWindowClosing())
                    {
                        return false; // If any plugin cancels the closing, return false
                    }
                }
            }
            return true; // All plugins allowed the closing
        }
    }
}
