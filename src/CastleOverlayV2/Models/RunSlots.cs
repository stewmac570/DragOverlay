namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Plot slot numbers. Three groups of three, each shown in the run strip as buttons 1-3:
    ///   1..3 = Castle ESC logs, 4..6 = GPS logs (RaceBox, RT Pro), 7..9 = data logs (Mini Logger).
    /// Run N of every group shares a line style (solid / dashed / dotted).
    /// </summary>
    public static class RunSlots
    {
        public const int Count = 9;

        public static int Gps(int buttonNumber) => buttonNumber + 3;
        public static int DataLog(int buttonNumber) => buttonNumber + 6;

        public static bool IsCastle(int slot) => slot is >= 1 and <= 3;
        public static bool IsGps(int slot) => slot is >= 4 and <= 6;
        public static bool IsDataLog(int slot) => slot is >= 7 and <= 9;

        /// <summary>The 1..3 button number of a slot within its group.</summary>
        public static int ButtonNumber(int slot) => (slot - 1) % 3 + 1;
    }
}
