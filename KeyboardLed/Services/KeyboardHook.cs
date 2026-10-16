using System;
using System.Runtime.InteropServices;
using System.Windows;
using KeyboardLed.Models;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Billentyűzet LED állapot lekérdezése a Windows-tól.
    /// </summary>
    public static class KeyStateReader
    {
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public static bool IsToggled(LockKey key) => (GetKeyState(LockKeys.VirtualKey(key)) & 1) != 0;

        public static KeyboardState GetCurrentState(bool includeInsert) => new()
        {
            NumLock = IsToggled(LockKey.NumLock),
            CapsLock = IsToggled(LockKey.CapsLock),
            ScrollLock = IsToggled(LockKey.ScrollLock),
            Insert = includeInsert && IsToggled(LockKey.Insert)
        };
    }

    /// <summary>
    /// Low-level keyboard hook: jelzi a lock billentyűk leütését (eseményvezérelt frissítéshez),
    /// lenyeli a kényszerített billentyűk leütését, és kezeli a Ctrl+Scroll Lock workaround-ot.
    /// </summary>
    public sealed class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_CANCEL = 0x03;  // Ctrl+Scroll Lock = Break

        private readonly LowLevelKeyboardProc _proc;
        private readonly bool[] _isDown = new bool[LockKeys.All.Length];
        private readonly bool[] _swallowedDown = new bool[LockKeys.All.Length];
        private IntPtr _hookId = IntPtr.Zero;
        private bool _disposed;

        /// <summary>
        /// Lock billentyű esemény történt (a UI szálon fut, gyorsnak kell lennie).
        /// </summary>
        public event Action? LockKeyActivity;

        /// <summary>
        /// Visszaadja egy billentyű kényszerített állapotát.
        /// </summary>
        public Func<LockKey, ForcedState>? ForcedStateProvider { get; set; }

        public KeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetHook(_proc);
                if (_hookId == IntPtr.Zero)
                {
                    Log.Warn($"SetWindowsHookEx failed (error {Marshal.GetLastWin32Error()}), falling back to polling only");
                }
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

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    int msg = (int)wParam;
                    bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                    if (vkCode == VK_CANCEL && isDown)
                    {
                        // Ctrl+Scroll Lock (Break) nem váltja a Scroll Lock-ot, ezért szimuláljuk.
                        // Nem a hook callback-ben injektálunk, hanem utána.
                        Application.Current?.Dispatcher.BeginInvoke(
                            new Action(() => KeyboardInput.Toggle(LockKey.ScrollLock)));
                    }
                    else if (LockKeys.FromVirtualKey(vkCode) is LockKey key)
                    {
                        if (ShouldSwallow(key, isDown, isUp))
                        {
                            return (IntPtr)1;
                        }
                        LockKeyActivity?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Keyboard hook callback failed");
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool ShouldSwallow(LockKey key, bool isDown, bool isUp)
        {
            int i = (int)key;
            if (isDown)
            {
                // Auto-repeat: a kezdeti lenyomásnál hozott döntés marad érvényben
                if (_isDown[i])
                {
                    return _swallowedDown[i];
                }

                _isDown[i] = true;
                var forced = ForcedStateProvider?.Invoke(key) ?? ForcedState.None;
                _swallowedDown[i] = ForcedStateRules.ShouldBlockToggle(forced, KeyStateReader.IsToggled(key));
                return _swallowedDown[i];
            }

            if (isUp)
            {
                // Csak akkor nyeljük le a felengedést, ha a lenyomást is lenyeltük
                bool swallowed = _swallowedDown[i];
                _isDown[i] = false;
                _swallowedDown[i] = false;
                return swallowed;
            }

            return false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
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
