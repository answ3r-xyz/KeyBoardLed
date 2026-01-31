using System;
using System.Runtime.InteropServices;

namespace KeyboardLed.Services
{
    public class KeyboardState
    {
        public bool NumLock { get; set; }
        public bool CapsLock { get; set; }
        public bool ScrollLock { get; set; }
    }

    /// <summary>
    /// Keyboard állapot olvasó polling-gal és Ctrl+Scroll Lock workaround-dal.
    /// </summary>
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        
        // Virtual key codes
        private const int VK_NUMLOCK = 0x90;
        private const int VK_CAPITAL = 0x14;
        private const int VK_SCROLL = 0x91;
        private const int VK_CANCEL = 0x03;  // Ctrl+Scroll Lock = Break

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private bool _disposed;

        public KeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetHook(_proc);
            }
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// Aktuális billentyűzet LED állapot lekérdezése a Windows-tól.
        /// </summary>
        public static KeyboardState GetCurrentState()
        {
            return new KeyboardState
            {
                NumLock = (GetKeyState(VK_NUMLOCK) & 0x0001) != 0,
                CapsLock = (GetKeyState(VK_CAPITAL) & 0x0001) != 0,
                ScrollLock = (GetKeyState(VK_SCROLL) & 0x0001) != 0
            };
        }

        /// <summary>
        /// Scroll Lock toggle szimulálása (Ctrl+Scroll Lock workaround)
        /// </summary>
        private static void SimulateScrollLockToggle()
        {
            keybd_event(VK_SCROLL, 0x46, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(VK_SCROLL, 0x46, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int msg = (int)wParam;
                
                // Ctrl+Scroll Lock (Break) workaround
                // Ha Break-et kapunk, szimuláljuk a Scroll Lock toggle-t
                if (vkCode == VK_CANCEL && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN))
                {
                    // Scroll Lock toggle szimulálása
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Send,
                        new Action(() =>
                        {
                            SimulateScrollLockToggle();
                        }));
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
