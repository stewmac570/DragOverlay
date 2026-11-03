namespace CastleOverlayV2.Services;

/// <summary>
/// One extra column of a Big logger CSV (rc-logger firmware/big_logger). The Big logger writes
/// the Mini Logger's four columns first, then these. Axis ranges and colours are display choices.
/// </summary>
public sealed record BigLoggerChannel(
    string Column,
    string Channel,
    string DisplayName,
    string Format,
    double AxisMin,
    double AxisMax,
    ScottPlot.Color Color,
    bool VisibleByDefault);

public static class BigLoggerChannels
{
    /// <summary>
    /// Columns after the first four, as rc-logger PR #23 writes them. Ride height comes from the
    /// lasers already corrected on the logger, so nothing here offsets it.
    /// </summary>
    public static readonly IReadOnlyList<BigLoggerChannel> All = new[]
    {
        new BigLoggerChannel("Pack Volts (V)", "Logger Pack Volts", "Pack Volts", "F2", 0, 10,
            new ScottPlot.Color(0xF4, 0xA2, 0x61), true),
        // + = speeding up
        new BigLoggerChannel("Accel Fwd (g)", "Logger Accel Fwd", "Accel Fwd", "F2", -4, 4,
            new ScottPlot.Color(0xFF, 0x6B, 0x6B), true),
        // + = nose up
        new BigLoggerChannel("Pitch (deg)", "Logger Pitch", "Pitch", "F1", -30, 30,
            new ScottPlot.Color(0x4E, 0xCD, 0xC4), true),
        // + = left side up (leaning right)
        new BigLoggerChannel("Roll (deg)", "Logger Roll", "Roll", "F1", -30, 30,
            new ScottPlot.Color(0xA7, 0x8B, 0xFA), true),
        new BigLoggerChannel("Front Laser (mm)", "Logger Front Ride Height", "Front Ride Height", "F1", 0, 50,
            new ScottPlot.Color(0x90, 0xBE, 0x6D), true),
        new BigLoggerChannel("Rear Laser (mm)", "Logger Rear Ride Height", "Rear Ride Height", "F1", 0, 50,
            new ScottPlot.Color(0x43, 0xAA, 0x8B), true),
    };

    public static BigLoggerChannel? Find(string channel) =>
        All.FirstOrDefault(item => item.Channel == channel);
}
