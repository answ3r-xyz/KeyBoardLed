using System;

namespace KeyboardLed.Models
{
    /// <summary>
    /// A figyelt billentyűk metaadatai (virtual key kód, alapértelmezett név)
    /// </summary>
    public static class LockKeys
    {
        public const int VK_NUMLOCK = 0x90;
        public const int VK_CAPITAL = 0x14;
        public const int VK_SCROLL = 0x91;
        public const int VK_INSERT = 0x2D;

        public static readonly LockKey[] All = { LockKey.NumLock, LockKey.CapsLock, LockKey.ScrollLock, LockKey.Insert };

        public static int VirtualKey(LockKey key) => key switch
        {
            LockKey.NumLock => VK_NUMLOCK,
            LockKey.CapsLock => VK_CAPITAL,
            LockKey.ScrollLock => VK_SCROLL,
            LockKey.Insert => VK_INSERT,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };

        public static LockKey? FromVirtualKey(int vk) => vk switch
        {
            VK_NUMLOCK => LockKey.NumLock,
            VK_CAPITAL => LockKey.CapsLock,
            VK_SCROLL => LockKey.ScrollLock,
            VK_INSERT => LockKey.Insert,
            _ => null
        };

        public static string DefaultName(LockKey key) => key switch
        {
            LockKey.NumLock => "Num Lock",
            LockKey.CapsLock => "Caps Lock",
            LockKey.ScrollLock => "Scroll Lock",
            LockKey.Insert => "Insert",
            _ => key.ToString()
        };

        /// <summary>
        /// Insert-et nem lehet kényszeríteni: a programok saját beszúrás/felülírás módot tartanak nyilván,
        /// így az injektált Insert leütések összezavarnák őket.
        /// </summary>
        public static bool CanForce(LockKey key) => key != LockKey.Insert;
    }
}
