namespace KeyboardLed.Models
{
    /// <summary>
    /// Kényszerített állapot döntési szabályai (tiszta függvények, tesztelhető)
    /// </summary>
    public static class ForcedStateRules
    {
        /// <summary>
        /// Le kell-e nyelni a billentyű lenyomást? Igen, ha az állapot már a kívánt értéken van,
        /// így a leütés nem tudja elrontani.
        /// </summary>
        public static bool ShouldBlockToggle(ForcedState forced, bool isOn) => forced switch
        {
            ForcedState.AlwaysOn => isOn,
            ForcedState.AlwaysOff => !isOn,
            _ => false
        };

        /// <summary>
        /// Vissza kell-e állítani az állapotot (pl. más program vagy bejelentkezés átállította)?
        /// </summary>
        public static bool NeedsCorrection(ForcedState forced, bool isOn) => forced switch
        {
            ForcedState.AlwaysOn => !isOn,
            ForcedState.AlwaysOff => isOn,
            _ => false
        };
    }
}
