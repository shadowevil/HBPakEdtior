using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace HBPakEditor
{
    internal static class Program
    {
        private const int WM_COPYDATA = 0x004A;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            if (args.Length > 0)
            {
                List<IntPtr> existingWindows = FindAllWindows();
                bool sent = false;

                foreach (IntPtr hwnd in existingWindows)
                {
                    if (SendFilesToWindow(hwnd, args))
                    {
                        sent = true;
                        break;
                    }
                }

                if (sent)
                    return;
            }

            MainWindow window = new MainWindow();
            if (args.Length > 0)
            {
                ProcessArgs(window, args);
            }
            Application.Run(window);
        }

        private static List<IntPtr> FindAllWindows()
        {
            List<IntPtr> foundWindows = new List<IntPtr>();
            string targetClass = "WindowsForms10.Window";
            const uint GW_OWNER = 4;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
                    return true;

                StringBuilder className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);

                if (className.ToString().StartsWith(targetClass))
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    if (Process.GetProcessById((int)processId).ProcessName == Process.GetCurrentProcess().ProcessName)
                    {
                        foundWindows.Add(hWnd);
                    }
                }
                return true;
            }, IntPtr.Zero);

            return foundWindows;
        }

        private static bool SendFilesToWindow(IntPtr hwnd, string[] files)
        {
            try
            {
                string data = string.Join("|", files);
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);

                COPYDATASTRUCT cds = new COPYDATASTRUCT
                {
                    dwData = IntPtr.Zero,
                    cbData = bytes.Length,
                    lpData = ptr
                };

                SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, ref cds);
                Marshal.FreeHGlobal(ptr);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ProcessArgs(MainWindow window, string[] args)
        {
            foreach (var arg in args)
            {
                if (!File.Exists(arg))
                    continue;
                if (Path.GetExtension(arg) != ".pak" && Path.GetExtension(arg) != ".epak")
                    continue;
                window.AddNewPAKTab(arg);
            }

            if (window.WindowState == FormWindowState.Minimized)
            {
                window.WindowState = FormWindowState.Normal;
            }
            window.Activate();
            window.TopMost = true;
            window.TopMost = false;
            window.Focus();
        }
    }
}