using System;

namespace CastleOverlayV2.Services
{
    /// <summary>
    /// Converts radio pulse width (milliseconds) to normalized throttle percentage [-100..+100],
    /// using user/ESC calibration endpoints.
    /// </summary>
    public static class ThrottlePercent
    {
        public static double FromMilliseconds(
            double ms,
            double fullReverseMs,  // e.g. 1.048
            double neutralMs,      // e.g. 1.500
            double fullForwardMs)  // e.g. 1.910
        {
            // Guard rails (don’t throw, just return 0 if config is bad)
            if (!(fullReverseMs < neutralMs && neutralMs < fullForwardMs))
                return 0;

            if (ms >= neutralMs)
            {
                double span = fullForwardMs - neutralMs;
                if (span == 0) return 0;
                double pct = 100.0 * (ms - neutralMs) / span;
                return Math.Max(-100, Math.Min(100, pct));
            }
            else
            {
                double span = neutralMs - fullReverseMs;
                if (span == 0) return 0;
                double pct = -100.0 * (neutralMs - ms) / span;
                return Math.Max(-100, Math.Min(100, pct));
            }
        }
    }
}
