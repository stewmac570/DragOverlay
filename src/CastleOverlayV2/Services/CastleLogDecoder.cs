using System.Globalization;
using System.Text;

namespace CastleOverlayV2.Services;

/// <summary>
/// Turns the bytes of a Castle session into rows and a Castle-format CSV.
/// The format and formulas are in Docs/Castle_Link_Download_Plan.md.
/// </summary>
public static class CastleLogDecoder
{
    public const int SlotCount = 13;

    /// <summary>Sample time of both captured logs. The ESC's own field for it is not identified.</summary>
    public const string SampleTime = "0.050";

    private const int Voltage = 0, Ripple = 1, Current = 2, Temperature = 3, Throttle = 4, PowerOut = 5,
        SpeedHigh = 6, SpeedLow = 7, GovGain = 8, BecVoltage = 9, MotorTemp = 10, Acceleration = 11, MotorTiming = 12;

    /// <summary>Each row holds the raw value of all <see cref="SlotCount"/> slots.</summary>
    public static IReadOnlyList<int[]> Decode(byte[] log)
    {
        var rows = new List<int[]>();
        var current = new int[SlotCount];
        int offset = 0;
        while (offset < log.Length)
        {
            int length = log[offset];
            if (length == 0)
            {
                offset++; // padding at the end of a page
                continue;
            }
            if (length < 2 || offset + length > log.Length)
                throw new InvalidDataException($"A log record at byte {offset} runs past the end of the log.");

            int sum = 0;
            for (int i = 0; i < length; i++)
                sum += log[offset + i];
            if ((sum & 0xFF) != 0)
                throw new InvalidDataException($"A log record at byte {offset} failed its checksum.");

            ApplyRecord(log, offset + 1, offset + length - 1, current);
            rows.Add((int[])current.Clone());
            offset += length;
        }
        return rows;
    }

    public static string ToCsv(IReadOnlyList<int[]> rows, int sessionNumber, string escFirmware)
    {
        var csv = new StringBuilder();
        csv.AppendLine($"# Note - session {sessionNumber.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine("# Source - DragOverlay direct download from the Castle Link");
        csv.AppendLine($"# ESC firmware - {escFirmware}");
        csv.AppendLine($"# Start Of Session - Sample Time: {SampleTime}");
        csv.AppendLine($"# Number: {sessionNumber.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine("Throttle, Power-Out, Voltage, Ripple, Current, Temperature, Speed, Gov. Gain, BEC Voltage, Motor Temp., Motor Timing., Acceleration.");
        csv.AppendLine(string.Join(", ", Enumerable.Repeat("True", 12)));
        foreach (int[] row in rows)
            csv.AppendLine(string.Join(", ", FormatRow(row)));
        return csv.ToString();
    }

    private static void ApplyRecord(byte[] log, int start, int end, int[] current)
    {
        if (start == end)
            return; // nothing changed: this row repeats the one before it

        int index = start;
        int first = log[index++];
        int mask;
        if ((first & 0x80) != 0)
        {
            if (index >= end)
                throw new InvalidDataException("A log record is missing its second mask byte.");
            mask = ((first & 0x7F) << 8) | log[index++];
        }
        else
        {
            mask = first << 8;
        }

        for (int slot = 0; slot < 15; slot++)
        {
            if ((mask & (1 << slot)) == 0)
                continue;
            if (slot >= SlotCount || index >= end)
                throw new InvalidDataException("A log record does not match its change mask.");
            current[slot] = log[index++];
        }
        if (index != end)
            throw new InvalidDataException("A log record has more values than its change mask.");
    }

    private static IEnumerable<string> FormatRow(int[] raw)
    {
        yield return Number(0.5 + raw[Throttle] / 127.6, "F4");
        yield return Number(raw[PowerOut] * 100.0 / 255.0, "F3");
        yield return Number(raw[Voltage] / 8.0, "F2");
        yield return Number(raw[Ripple] / 8.0, "F3");
        yield return Number(raw[Current] * 3.0, "F1");
        yield return Number(ToFahrenheit(raw[Temperature]), "F1");
        yield return Number((raw[SpeedHigh] * 256 + raw[SpeedLow]) * 10.0, "F0");
        yield return Number(raw[GovGain], "F0");
        yield return Number(raw[BecVoltage] * 0.0404, "F2");
        yield return Number(ToFahrenheit(raw[MotorTemp]), "F2");
        yield return Number(raw[MotorTiming], "F0");
        yield return Number(raw[Acceleration] / 16.0, "F2");
    }

    private static double ToFahrenheit(int celsius) => celsius * 1.8 + 32;

    // Castle's export rounds halves up: 7.625 prints as 7.63.
    private static string Number(double value, string format) =>
        Math.Round(value, format[1] - '0', MidpointRounding.AwayFromZero)
            .ToString(format, CultureInfo.InvariantCulture);
}
