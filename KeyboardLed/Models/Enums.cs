namespace KeyboardLed.Models
{
    public enum LockKey { NumLock, CapsLock, ScrollLock, Insert }

    /// <summary>
    /// Billentyű állapot kényszerítése (pl. Num Lock mindig BE, Caps Lock mindig KI)
    /// </summary>
    public enum ForcedState { None, AlwaysOn, AlwaysOff }

    public enum KeySound { None, Beep, Asterisk, Exclamation, Hand, Question, Custom }

    public enum OverlayAnchor { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight, Custom }

    public enum OverlayMonitor { Primary, MouseCursor, ActiveWindow }

    public enum OverlaySize { Small, Normal, Large }

    public enum AppTheme { System, Light, Dark }

    public enum AppLanguage { Auto, Hungarian, English }
}
