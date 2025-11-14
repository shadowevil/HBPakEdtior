namespace HBPakEditor
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            MainWindow window = new MainWindow();

            if(args.Length > 0)
            {
                foreach(var arg in args)
                {
                    if (!File.Exists(arg))
                        continue;

                    if (Path.GetExtension(arg) != ".pak" || Path.GetExtension(arg) != ".epak")
                        continue;

                    window.AddNewPAKTab(arg);
                }
            }

            Application.Run(window);
        }
    }
}