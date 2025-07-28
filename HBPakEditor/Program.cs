namespace HBPakEdtior
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ApplicationConfiguration.Initialize();
            Application.Run(new MainWindow(args));
        }
    }
}