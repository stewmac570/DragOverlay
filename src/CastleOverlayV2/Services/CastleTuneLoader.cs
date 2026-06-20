using CastleOverlayV2.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CastleOverlayV2.Services
{
    public sealed class CastleTuneLoader
    {
        private static readonly Regex NumberRegex = new(@"[-+]?\d+(?:\.\d+)?", RegexOptions.Compiled);

        public LoadResult<TuneSettings> Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return LoadResult<TuneSettings>.Error("Tune Import Failed", "The selected tune file does not exist.");

            try
            {
                var lines = File.ReadAllLines(path);
                var tune = Parse(lines);
                tune.SourceFileName = Path.GetFileName(path);

                bool hasUsefulData =
                    tune.ThrottleCurve.Count > 0 ||
                    tune.BoostByThrottleCurve.Count > 0 ||
                    !string.IsNullOrWhiteSpace(tune.DeviceName);

                if (!hasUsefulData)
                    return LoadResult<TuneSettings>.Error(
                        "Tune Import Failed",
                        "This file did not look like a Castle Link tune export.");

                return LoadResult<TuneSettings>.Success(tune);
            }
            catch (Exception ex)
            {
                return LoadResult<TuneSettings>.Error(
                    "Tune Import Failed",
                    "The Castle tune file could not be read.\n\n" + ex.Message);
            }
        }

        internal TuneSettings Parse(IEnumerable<string> lines)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tune = new TuneSettings();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("# Created:", StringComparison.OrdinalIgnoreCase))
                {
                    tune.CreatedText = line.TrimStart('#').Trim();
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int colonIndex = line.IndexOf(':');
                if (colonIndex <= 0)
                    continue;

                string key = line[..colonIndex].Trim();
                string value = line[(colonIndex + 1)..].Trim();
                fields[key] = value;
            }

            tune.DeviceName = Get(fields, "Device Name");
            tune.Firmware = Get(fields, "Current Firmware");
            tune.MaxPower = Get(fields, "Max Power");
            tune.DragBrake = Get(fields, "Drag Brake");
            tune.SampleFrequency = Get(fields, "Sample Frequency");

            AddCurve(tune.ThrottleCurve, Get(fields, "Forward Curve 65535"));
            AddCurve(tune.BoostByThrottleCurve, Get(fields, "Boost By Throttle Table 65535"));

            tune.BoostModeEnableText = Get(fields, "Boost Mode Enable");
            tune.BoostEnabled = ParseYesNo(tune.BoostModeEnableText);
            tune.BoostTimingMode = Get(fields, "Boost Timing Mode");
            tune.BoostTimingAmountDeg = ParseFirstNumber(Get(fields, "Boost Timing Amount"));
            tune.BoostStartRpm = ParseFirstNumber(Get(fields, "Boost RPM Activation Range - Start RPM"));
            tune.BoostEndRpm = ParseFirstNumber(Get(fields, "Boost RPM Activation Range - End RPM"));
            tune.BoostStartThrottlePct = ParseFirstNumber(Get(fields, "Boost Throttle Activation Range - Start RPM"));
            tune.BoostEndThrottlePct = ParseFirstNumber(Get(fields, "Boost Throttle Activation Range - End RPM"));

            tune.TurboEnableText = Get(fields, "Turbo Enable");
            tune.TurboEnabled = ParseYesNo(tune.TurboEnableText);
            tune.TurboTimingAmountDeg = ParseFirstNumber(Get(fields, "Turbo Timing Amount"));
            tune.TurboActivationThrottlePct = ParseFirstNumber(Get(fields, "Activation Throttle Percent"));
            tune.TurboAccelDegPerSecond = ParseFirstNumber(Get(fields, "Acceleration Degrees Per Second"));
            tune.TurboDecelDegPerSecond = ParseFirstNumber(Get(fields, "Deceleration Degrees Per Second"));

            return tune;
        }

        private static string Get(IReadOnlyDictionary<string, string> fields, string key) =>
            fields.TryGetValue(key, out var value) ? value : "";

        private static bool ParseYesNo(string value) =>
            value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("ENABLED", StringComparison.OrdinalIgnoreCase);

        private static double? ParseFirstNumber(string value)
        {
            var match = NumberRegex.Match(value);
            if (!match.Success)
                return null;

            return double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static void AddCurve(ICollection<TuneCurvePoint> destination, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var raw))
                    continue;

                double x = parts.Length == 1 ? 0 : (double)i / (parts.Length - 1) * 100.0;
                double y = Math.Clamp(raw / 65535.0 * 100.0, 0.0, 100.0);
                destination.Add(new TuneCurvePoint(x, y));
            }
        }
    }
}
