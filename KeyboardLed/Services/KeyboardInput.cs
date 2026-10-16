using System;
using System.Runtime.InteropServices;
using KeyboardLed.Models;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Billentyű leütések szimulálása SendInput-tal (az elavult keybd_event helyett)
    /// </summary>
    public static class KeyboardInput
    {
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MAPVK_VK_TO_VSC = 0;

        internal static int InputSize => Marshal.SizeOf<INPUT>();

        public static void Toggle(LockKey key) => Toggle(LockKeys.VirtualKey(key));

        public static void Toggle(int vk)
        {
            ushort scan = (ushort)MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
            uint flags = IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0;

            var inputs = new[]
            {
                CreateKeyInput(vk, scan, flags),
                CreateKeyInput(vk, scan, flags | KEYEVENTF_KEYUP)
            };

            uint sent = SendInput((uint)inputs.Length, inputs, InputSize);
            if (sent != inputs.Length)
            {
                Log.Warn($"SendInput sent {sent}/{inputs.Length} events for VK 0x{vk:X2} (error {Marshal.GetLastWin32Error()})");
            }
        }

        private static bool IsExtendedKey(int vk) => vk == LockKeys.VK_NUMLOCK || vk == LockKeys.VK_INSERT;

        private static INPUT CreateKeyInput(int vk, ushort scan, uint flags) => new()
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = scan, dwFlags = flags }
            }
        };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        // A union mérete a legnagyobb tagé (MOUSEINPUT), különben a SendInput elutasítja a cbSize-t
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}
