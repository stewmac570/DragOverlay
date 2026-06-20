using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services
{
    public static class AlignmentHelper
    {
        /// <summary>
        /// Calculate the non-destructive offset required to place the detected launch at t = 0.
        /// Returns null when no clear launch can be found.
        /// </summary>
        public static double? GetAutoOffsetMs(RunData run)
        {
            if (!run.IsRaceBox)
            {
                foreach (var point in run.DataPoints)
                {
                    if (point.Throttle > 1.65 && point.PowerOutValid && point.PowerOut > 10)
                        return -point.Time * 1000.0;
                }

                return null;
            }

            if (!run.Data.TryGetValue("RaceBox Speed", out var speed))
                return null;

            for (int i = 0; i <= speed.Count - 3; i++)
            {
                if (speed[i].Y > 1.0 &&
                    speed[i + 1].Y > 1.0 &&
                    speed[i + 2].Y > 1.0)
                    return -speed[i].Time * 1000.0;
            }

            return null;
        }
    }
}
