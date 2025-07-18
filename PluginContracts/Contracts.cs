namespace PluginContracts
{
    public interface IPlugin
    {
        IPluginHostPublic? PluginHost { get; }
        string Name { get; }
    }

    public interface IPluginHostPublic
    {
        void Log(string message);
        T? GetService<T>() where T : class;
    }
}
