using System.Globalization;
using System.Text;

namespace CastleOverlayV2.Services;

/// <summary>
/// Turns the tune bytes read off a Castle ESC into a Castle Link style .dat file.
/// Every byte and label comes from Docs/Castle_Link_Tune_Read.md, which was proven
/// on a GhostRC Cobra 8 Ace (firmware 1.36). Values the ESC does not hold are left out.
/// </summary>
public static class CastleTuneDat
{
    public const int SettingsLength = 48;
    public const int CurvesLength = 2560;
    private const int TableLength = 256;

    private static readonly string[] AuxWireModes =
    {
        "None", "Max Power Adjustment", "Reverse Percentage Adjustment", "Max Brake Adjustment",
        "Drag Brake Adjustment", "Reverse Enable/Disable", "Torque Control Adjustment", "Start Power Control"
    };

    private static readonly string[] DragBrakeRamps =
        { "Disabled (Instantaneous)", "Very Fast", "Fast", "Medium", "Slow", "Very Slow" };

    private static readonly string[] MotorTypes =
        { "Smart Sense™ Brushless", "Sensored Only (Sensors Required)", "Brushed Reversing" };

    private static readonly string[] Orientations =
        { "Power Wires to the Front", "Power Wires to the Back", "Power Wires to the Left", "Power Wires to the Right" };

    private static readonly (int Celsius, string Label)[] TemperatureCutoffs =
    {
        (49, "Low (120°F / 49°C)"), (54, "Low (130°F / 54°C)"), (60, "Low (140°F / 60°C)"),
        (66, "Medium (150°F / 66°C)"), (71, "Medium (160°F / 71°C)"), (77, "Medium (170°F / 77°C)"),
        (82, "Medium (180°F / 82°C)"), (88, "High (190°F / 88°C)"), (93, "High (200°F / 93°C)"),
        (99, "High (210°F / 99°C)")
    };

    private static readonly (double Ms, string Label)[] DeadBands =
    {
        (0.0250, "Very Small (0.0250 ms)"), (0.0375, "Very Small (0.0375 ms)"), (0.0500, "Small (0.0500 ms)"),
        (0.0625, "Small (0.0625 ms)"), (0.0750, "Small (0.0750 ms)"), (0.0875, "Average (0.0875 ms)"),
        (0.1000, "Average (0.1000 ms)"), (0.1125, "Average (0.1125 ms)"), (0.1250, "Large (0.1250 ms)"),
        (0.1500, "Large (0.1500 ms)")
    };

    private static readonly Dictionary<int, string> SensorlessTimings = new()
    {
        [0] = "Lowest (0)", [5] = "Low (5)", [10] = "Normal (10)", [15] = "High (15)", [20] = "Highest (20)"
    };

    /// <summary>The only ESC this has been proven on. Other IDs are written as the raw ID.</summary>
    private static readonly Dictionary<string, string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GL"] = "GhostRC Cobra 8 Ace"
    };

    public static string Build(byte[] tune, string escId, DateTime created)
    {
        if (tune is null || tune.Length != SettingsLength + CurvesLength)
            throw new ArgumentException("The tune must be 48 settings bytes and 2,560 curve bytes.", nameof(tune));

        byte[] s = tune[..SettingsLength];
        int[][] tables = Enumerable.Range(0, 5)
            .Select(table => Enumerable.Range(0, TableLength)
                .Select(i => tune[SettingsLength + (table * TableLength + i) * 2] |
                             (tune[SettingsLength + (table * TableLength + i) * 2 + 1] << 8))
                .ToArray())
            .ToArray();

        int HighFirst(int index) => (s[index] << 8) | s[index + 1];
        string Pick(string[] labels, int value) => value < labels.Length ? labels[value] : value.ToString(CultureInfo.InvariantCulture);
        string Table(int index) => "0." + string.Join('.', tables[index].Select(v => v.ToString(CultureInfo.InvariantCulture)));
        string N(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

        bool throttleBoost = (s[16] & 0x80) != 0;
        int boostDegrees = s[16] & 0x7F;
        bool boostOn = boostDegrees != 0 || HighFirst(8) != 0 || HighFirst(10) != 0;
        bool turboOn = s[39] != 0 || s[40] != 0 || s[41] != 0 || s[42] != 0;
        double deadBandMs = s[25] / 1000.0;
        double lockout = Math.Round(s[26] / 128.0 * 4, MidpointRounding.AwayFromZero) / 4;
        int dragBrake = (int)Math.Round(s[18] / 2.55, MidpointRounding.AwayFromZero);

        var fields = new Dictionary<string, string>
        {
            ["Acceleration Degrees Per Second"] = $"{s[41]} Deg/Second",
            ["Activation Throttle Percent"] = $"{s[40]}%",
            ["Auto-Lipo Volts/Cell"] = $"{N(s[31] / 10.0, "0.0")} Volts/Cell",
            ["Auxiliary Wire Mode"] = Pick(AuxWireModes, s[34]),
            ["BEC Voltage"] = $"{N(s[35] / 10.0, "0.0")}V",
            ["Boost By Throttle Table 65535"] = Table(4),
            ["Boost Mode Enable"] = boostOn ? "YES" : "NO",
            ["Boost Timing Amount"] = $"{boostDegrees} degrees",
            ["Boost Timing Mode"] = throttleBoost ? "Throttle Percentage Based Boost" : "RPM Based Boost",
            ["Brake Amount"] = $"{s[23]}%",
            ["Brake Curve 65535"] = Table(1),
            ["Brake Lockout Time"] = $"{N(lockout, "0.00")} Seconds",
            ["Controller Orientation"] = Pick(Orientations, s[38]),
            ["Current Firmware"] = FirmwareFromId(escId),
            ["Cutoff Voltage"] = s[32].ToString(CultureInfo.InvariantCulture),
            ["Data Log Enabled"] = (s[0] | (s[1] << 8)).ToString(CultureInfo.InvariantCulture),
            ["Deceleration Degrees Per Second"] = $"{s[42]} Deg/Second",
            ["Device Name"] = DeviceNameFromId(escId),
            ["Drag Brake Ramp"] = Pick(DragBrakeRamps, s[19]),
            ["Drag Brake"] = dragBrake == 0 ? "0.0%, Disabled" : $"{dragBrake}.0%",
            ["Forward Curve 65535"] = Table(0),
            ["Full Forward"] = HighFirst(2).ToString(CultureInfo.InvariantCulture),
            ["Full Reverse"] = HighFirst(4).ToString(CultureInfo.InvariantCulture),
            ["Idle/Error Beep Disable"] = s[27].ToString(CultureInfo.InvariantCulture),
            ["Max Power"] = $"{s[21]}%",
            ["Motor Direction"] = s[30] == 0 ? "Normal" : "Reverse",
            ["Motor Temperature Cutoff"] = s[33] == 0
                ? "Disabled"
                : TemperatureCutoffs.Where(t => t.Celsius == s[33]).Select(t => t.Label).FirstOrDefault()
                  ?? $"{s[33]}°C",
            ["Motor Type"] = $"{Pick(MotorTypes, s[17] & 0x7F)}, Checkbox: {((s[17] & 0x80) != 0 ? "True" : "False")}",
            ["Neutral"] = HighFirst(6).ToString(CultureInfo.InvariantCulture),
            ["Punch Control"] = s[20] == 0 ? "0%, Disabled" : $"{s[20]}%",
            ["Reverse Curve 65535"] = Table(2),
            ["Reverse Percentage"] = $"{s[22]}%",
            ["Reverse To Forward Brake Amount"] = $"{s[24]}%",
            ["Reverse Type"] = s[29] == 0 ? "With Reverse" : "Without Reverse",
            ["RPM Limit"] = s[43] == 0 ? "0 RPM(Disabled)" : $"{s[43]} kRPM",
            ["Sample Frequency"] = s[36] == 0 ? "Disabled" : $"{s[36]} Sample / Sec",
            ["Sensorless Motor Timing"] = SensorlessTimings.TryGetValue(s[15], out string? timing)
                ? timing
                : s[15].ToString(CultureInfo.InvariantCulture),
            ["Start Power Hold Time"] = $"{s[45]}ms",
            ["Start Power Ramp Time"] = $"{s[44]}ms",
            ["Start Power"] = s[28] == 0 ? "0%, Disabled" : $"{s[28]}%",
            ["Throttle Dead Band"] = DeadBands.OrderBy(d => Math.Abs(d.Ms - deadBandMs)).First().Label,
            ["Turbo Enable"] = turboOn ? "YES" : "NO",
            ["Turbo Timing Amount"] = $"{s[39]} degrees",
        };

        // The ESC keeps one start/end pair; which pair it is depends on the boost mode.
        if (throttleBoost)
        {
            fields["Boost Throttle Activation Range - Start RPM"] = $"{HighFirst(8)}%";
            fields["Boost Throttle Activation Range - End RPM"] = $"{HighFirst(10)}%";
        }
        else
        {
            fields["Boost RPM Activation Range - Start RPM"] = $"{HighFirst(8) * 10} RPM";
            fields["Boost RPM Activation Range - End RPM"] = $"{HighFirst(10) * 10} RPM";
        }

        // Programmed Throttle: entry 200 packs the time scale (x100) and the enable throttle %.
        int packed = tables[3][200];
        bool programmed = tables[3].Any(v => v != 0);
        if (programmed)
        {
            fields["Programmed Throttle Time Scale"] = $"{N((packed & 0xFF) / 100.0, "0.00")}s";
            fields["Programmed Throttle Enable At Throttle"] = $"{packed >> 8}%";
        }
        else
        {
            fields["Programmed Throttle"] = "Off";
        }

        var text = new StringBuilder();
        text.Append("#######################################################\r\n");
        text.Append("# Castle Link Data File\r\n");
        text.Append("# Created: ").Append(created.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture)).Append("\r\n");
        text.Append("# Do Not Edit This File By Hand\r\n");
        text.Append("# ").Append(escId).Append("\r\n");
        text.Append("# Read off the ESC by DragOverlay\r\n");
        text.Append("#######################################################\r\n");
        // Castle sorts whole lines, so "Drag Brake Ramp:" comes before "Drag Brake:".
        foreach ((string key, string value) in fields.OrderBy(f => f.Key + ":", StringComparer.OrdinalIgnoreCase))
            text.Append(key).Append(": ").Append(value).Append("\r\n");
        return text.ToString();
    }

    /// <summary>"GL00136" is firmware 1.36: the last three digits in hundredths.</summary>
    public static string FirmwareFromId(string escId)
    {
        string digits = new(escId.Where(char.IsDigit).ToArray());
        if (digits.Length < 3 || !int.TryParse(digits[^3..], NumberStyles.None, CultureInfo.InvariantCulture, out int hundredths))
            return escId;
        return (hundredths / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static string DeviceNameFromId(string escId)
    {
        string prefix = new(escId.TakeWhile(char.IsLetter).ToArray());
        return DeviceNames.TryGetValue(prefix, out string? name) ? name : escId;
    }
}
