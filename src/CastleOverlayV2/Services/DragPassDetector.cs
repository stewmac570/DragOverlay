using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services
{
    public sealed record DragPassDetectionResult(
        bool Found,
        int LaunchIndex,
        int TrimStartIndex,
        int TrimEndIndex,
        double Score);

    public static class DragPassDetector
    {
        private const double PreLaunchSeconds = 0.5;
        private const double PostLaunchSeconds = 2.5;
        private const double ScoreWindowSeconds = 2.0;

        public static DragPassDetectionResult Detect(IReadOnlyList<DataPoint> data, double sampleIntervalSeconds)
        {
            if (data.Count < 10)
                return NotFound();

            sampleIntervalSeconds = sampleIntervalSeconds > 0 ? sampleIntervalSeconds : 0.05;
            int scoreWindow = Math.Max(10, (int)Math.Round(ScoreWindowSeconds / sampleIntervalSeconds));
            int preLaunchSamples = Math.Max(1, (int)Math.Round(PreLaunchSeconds / sampleIntervalSeconds));
            int postLaunchSamples = Math.Max(1, (int)Math.Round(PostLaunchSeconds / sampleIntervalSeconds));

            Candidate? best = null;
            int i = 1;
            while (i < data.Count - 1)
            {
                if (!IsCandidateStart(data, i))
                {
                    i++;
                    continue;
                }

                int burstStart = i;
                int burstEnd = i;
                while (burstEnd + 1 < data.Count && IsLoaded(data[burstEnd + 1]))
                    burstEnd++;

                Candidate candidate = ScoreCandidate(data, burstStart, burstEnd, scoreWindow);
                if (candidate.IsViable && (best == null || candidate.Score > best.Score))
                    best = candidate;

                i = Math.Max(burstEnd + 1, i + 1);
            }

            if (best == null)
                return NotFound();

            int launchIndex = FindLaunchAnchor(data, best.StartIndex, preLaunchSamples);
            int trimStart = Math.Max(0, launchIndex - preLaunchSamples);
            int trimEnd = Math.Min(data.Count - 1, launchIndex + postLaunchSamples);

            return new DragPassDetectionResult(true, launchIndex, trimStart, trimEnd, best.Score);
        }

        private static DragPassDetectionResult NotFound() =>
            new(false, -1, -1, -1, 0);

        private static bool IsCandidateStart(IReadOnlyList<DataPoint> data, int index)
        {
            DataPoint previous = data[index - 1];
            DataPoint current = data[index];

            if (!HasValidLoadSignal(current))
                return false;

            bool wasIdle = !IsLoaded(previous);
            bool currentRise = current.CurrentValid && current.Current >= 20.0 &&
                               (!previous.CurrentValid || current.Current - previous.Current >= 15.0 || previous.Current < 5.0);
            bool rpmRise = current.SpeedValid && current.Speed >= 5000.0 &&
                           (!previous.SpeedValid || current.Speed - previous.Speed >= 2500.0 || previous.Speed < 1000.0);
            bool powerRise = current.PowerOutValid && current.PowerOut >= 20.0 &&
                             (!previous.PowerOutValid || current.PowerOut - previous.PowerOut >= 15.0 || previous.PowerOut < 5.0);
            bool throttleMove = current.Throttle > 1.62 && previous.Throttle <= 1.62;

            return wasIdle || currentRise || rpmRise || powerRise || throttleMove;
        }

        private static bool IsLoaded(DataPoint point) =>
            (point.CurrentValid && point.Current >= 8.0) ||
            (point.PowerOutValid && point.PowerOut >= 18.0) ||
            (point.SpeedValid && point.Speed >= 4000.0);

        private static bool HasValidLoadSignal(DataPoint point) =>
            (point.CurrentValid && point.Current >= 15.0) ||
            (point.PowerOutValid && point.PowerOut >= 25.0) ||
            (point.SpeedValid && point.Speed >= 7000.0);

        private static Candidate ScoreCandidate(
            IReadOnlyList<DataPoint> data,
            int startIndex,
            int burstEndIndex,
            int scoreWindow)
        {
            int endIndex = Math.Min(data.Count - 1, startIndex + scoreWindow);
            double peakCurrent = 0;
            double currentArea = 0;
            double peakPower = 0;
            double powerArea = 0;
            double startRpm = data[startIndex].SpeedValid ? data[startIndex].Speed : 0;
            double peakRpm = startRpm;
            int loadedSamples = 0;
            int highPowerSamples = 0;

            for (int i = startIndex; i <= endIndex; i++)
            {
                DataPoint point = data[i];

                if (point.CurrentValid)
                {
                    peakCurrent = Math.Max(peakCurrent, point.Current);
                    currentArea += Math.Max(0, point.Current);
                }

                if (point.PowerOutValid)
                {
                    peakPower = Math.Max(peakPower, point.PowerOut);
                    powerArea += Math.Max(0, point.PowerOut);
                    if (point.PowerOut >= 40.0)
                        highPowerSamples++;
                }

                if (point.SpeedValid)
                    peakRpm = Math.Max(peakRpm, point.Speed);

                if (IsLoaded(point))
                    loadedSamples++;
            }

            double rpmRise = Math.Max(0, peakRpm - startRpm);
            double score =
                peakCurrent * 4.0 +
                currentArea * 0.35 +
                peakPower * 12.0 +
                powerArea * 0.12 +
                rpmRise * 0.01 +
                loadedSamples * 25.0 +
                highPowerSamples * 30.0;

            bool viable =
                peakCurrent >= 300.0 &&
                peakPower >= 75.0 &&
                peakRpm >= 40000.0 &&
                loadedSamples >= 8;

            return new Candidate(startIndex, burstEndIndex, score, viable);
        }

        private static int FindLaunchAnchor(IReadOnlyList<DataPoint> data, int startIndex, int preLaunchSamples)
        {
            int searchStart = Math.Max(1, startIndex - preLaunchSamples);
            int anchor = startIndex;

            for (int i = searchStart; i <= startIndex; i++)
            {
                DataPoint previous = data[i - 1];
                DataPoint current = data[i];

                bool previousIdle =
                    (!previous.CurrentValid || previous.Current <= 5.0) &&
                    (!previous.SpeedValid || previous.Speed <= 1500.0) &&
                    (!previous.PowerOutValid || previous.PowerOut <= 10.0);

                bool currentLoaded =
                    (current.CurrentValid && current.Current >= 10.0) ||
                    (current.SpeedValid && current.Speed >= 2500.0) ||
                    (current.PowerOutValid && current.PowerOut >= 20.0);

                if (previousIdle && currentLoaded)
                {
                    anchor = i;
                    break;
                }
            }

            return anchor;
        }

        private sealed record Candidate(int StartIndex, int EndIndex, double Score, bool IsViable);
    }
}
