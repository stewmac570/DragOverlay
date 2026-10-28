using System.Globalization;
using System.Text.RegularExpressions;

namespace CastleOverlayV2.Services;

public enum TcLoggerState
{
    Unknown,
    Disarmed,
    Armed,
    Active,
    Done,
    NoSignal
}

public sealed class TcLoggerStatus
{
    public bool IsTcLogger { get; init; }
    public TcLoggerState State { get; init; }
    public string StateText { get; init; } = "UNKNOWN";
    public int InputUs { get; init; }
    public int OutputUs { get; init; }
    public int? CeilingUs { get; init; }
    public double MotorRpm { get; init; }
    public int CurvePointCount { get; init; }
    public int NeutralUs { get; init; }
    public int FullUs { get; init; }
    public int BrakeUs { get; init; }
    public IReadOnlyDictionary<string, double> Settings { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public string Firmware { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
}

public sealed record TcCurveTransferResult(bool Success, string Message, TcLoggerStatus? Status = null);

public static class TcStatusParser
{
    private static readonly Regex Header = new(
        @"^TC (?<state>\S+) \| in (?<input>\d+) us \| out (?<output>\d+) us \| ceiling (?<ceiling>\S+) \| rpm (?<rpm>[-+0-9.]+) \| curve (?<points>\d+) points",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Travel = new(
        @"^neutral (?<neutral>\d+) us \| full (?<full>\d+) us \| brake (?<brake>\d+) us(?: \||$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsStatusHeader(string line) => Header.IsMatch(line.Trim());

    public static TcLoggerStatus Parse(IEnumerable<string> replyLines)
    {
        var lines = replyLines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (lines.Any(line => line.StartsWith("Commands:", StringComparison.Ordinal)))
            return new TcLoggerStatus { IsTcLogger = false, Lines = lines };

        string? headerLine = lines.FirstOrDefault(IsStatusHeader);
        if (headerLine is null)
            return new TcLoggerStatus { IsTcLogger = false, Lines = lines };

        Match header = Header.Match(headerLine);
        if (!header.Success)
            throw new InvalidDataException("The TC logger returned an unrecognised tc status header.");

        static int Int(Match match, string name) =>
            int.Parse(match.Groups[name].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        static double Double(Match match, string name) =>
            double.Parse(match.Groups[name].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

        string stateText = header.Groups["state"].Value;
        TcLoggerState state = stateText switch
        {
            "DISARMED" => TcLoggerState.Disarmed,
            "ARMED" => TcLoggerState.Armed,
            "ACTIVE" => TcLoggerState.Active,
            "DONE" => TcLoggerState.Done,
            "NO_SIGNAL" => TcLoggerState.NoSignal,
            _ => TcLoggerState.Unknown
        };

        int neutral = 0;
        int full = 0;
        int brake = 0;
        string? travelLine = lines.FirstOrDefault(line => line.StartsWith("neutral ", StringComparison.Ordinal));
        if (travelLine is not null)
        {
            Match travel = Travel.Match(travelLine);
            if (travel.Success)
            {
                neutral = Int(travel, "neutral");
                full = Int(travel, "full");
                brake = Int(travel, "brake");
            }
        }

        var settings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        string firmware = "";
        foreach (string line in lines)
        {
            if (line.StartsWith("firmware ", StringComparison.Ordinal))
            {
                firmware = line;
                continue;
            }
            string[] fields = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 2 &&
                double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                settings[fields[0]] = value;
        }

        int? ceiling = header.Groups["ceiling"].Value == "none"
            ? null
            : int.Parse(header.Groups["ceiling"].Value, NumberStyles.None, CultureInfo.InvariantCulture);

        return new TcLoggerStatus
        {
            IsTcLogger = true,
            State = state,
            StateText = stateText,
            InputUs = Int(header, "input"),
            OutputUs = Int(header, "output"),
            CeilingUs = ceiling,
            MotorRpm = Double(header, "rpm"),
            CurvePointCount = Int(header, "points"),
            NeutralUs = neutral,
            FullUs = full,
            BrakeUs = brake,
            Settings = settings,
            Firmware = firmware,
            Lines = lines
        };
    }
}
