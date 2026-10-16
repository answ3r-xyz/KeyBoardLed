using System;

namespace KeyboardLed.Models
{
    /// <summary>
    /// Struct a heap allokáció elkerülésére - minden polling-nál újrahasználható
    /// </summary>
    public readonly struct KeyboardState : IEquatable<KeyboardState>
    {
        // Bit layout az index-hez - csak itt van definiálva
        private const int NumLockBit = 1;
        private const int CapsLockBit = 2;
        private const int ScrollLockBit = 4;
        private const int InsertBit = 8;

        /// <summary>
        /// Lehetséges állapotok száma (2^4)
        /// </summary>
        public const int StateCount = 16;

        public bool NumLock { get; init; }
        public bool CapsLock { get; init; }
        public bool ScrollLock { get; init; }
        public bool Insert { get; init; }

        public bool this[LockKey key] => key switch
        {
            LockKey.NumLock => NumLock,
            LockKey.CapsLock => CapsLock,
            LockKey.ScrollLock => ScrollLock,
            LockKey.Insert => Insert,
            _ => false
        };

        /// <summary>
        /// Index alapú elérés a cached icon-okhoz (0-15)
        /// </summary>
        public int ToIndex() =>
            (NumLock ? NumLockBit : 0) | (CapsLock ? CapsLockBit : 0) |
            (ScrollLock ? ScrollLockBit : 0) | (Insert ? InsertBit : 0);

        /// <summary>
        /// Állapot visszaállítása index-ből (ToIndex inverze)
        /// </summary>
        public static KeyboardState FromIndex(int index) => new KeyboardState
        {
            NumLock = (index & NumLockBit) != 0,
            CapsLock = (index & CapsLockBit) != 0,
            ScrollLock = (index & ScrollLockBit) != 0,
            Insert = (index & InsertBit) != 0
        };

        public bool Equals(KeyboardState other) => ToIndex() == other.ToIndex();

        public override bool Equals(object? obj) => obj is KeyboardState other && Equals(other);

        public override int GetHashCode() => ToIndex();
    }
}
