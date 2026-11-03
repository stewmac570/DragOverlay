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
    bool VisibleByDefault,
    bool IsRideHeight = false);

public static class BigLoggerChannels
{
    /// <summary>The lasers read about 8 mm long (tested 31 Oct 2026). Users can change it in Settings.</summary>
    public const double DefaultLaserOffsetMm = 8.0;

    public static readonly IReadOnlyList<BigLoggerChannel> All = new[]
    {
        new BigLoggerChannel("Pack Volts (V)", "Logger Pack Volts", "Pack Volts", "F2", 0, 10,
            new ScottPlot.Color(0xF4, 0xA2, 0x61), true),
        new BigLoggerChannel("Accel X (g)", "Logger Accel X", "Accel X", "F2", -4, 4,
            new ScottPlot.Color(0xFF, 0x6B, 0x6B), true),
        new BigLoggerChannel("Accel Y (g)", "Logger Accel Y", "Accel Y", "F2", -4, 4,
            new ScottPlot.Color(0x4E, 0xCD, 0xC4), true),
        new BigLoggerChannel("Accel Z (g)", "Logger Accel Z", "Accel Z", "F2", -4, 4,
            new ScottPlot.Color(0xA7, 0x8B, 0xFA), true),
        new BigLoggerChannel("Gyro X (dps)", "Logger Gyro X", "Gyro X", "F0", -1000, 1000,
            new ScottPlot.Color(0xE7, 0x6F, 0x51), false),
        new BigLoggerChannel("Gyro Y (dps)", "Logger Gyro Y", "Gyro Y", "F0", -1000, 1000,
            new ScottPlot.Color(0x2A, 0x9D, 0x8F), false),
        new BigLoggerChannel("Gyro Z (dps)", "Logger Gyro Z", "Gyro Z", "F0", -1000, 1000,
            new ScottPlot.Color(0xC0, 0x8B, 0xA8), false),
        new BigLoggerChannel("Front Laser (mm)", "Logger Front Ride Height", "Front Ride Height", "F0", 0, 100,
            new ScottPlot.Color(0x90, 0xBE, 0x6D), true, IsRideHeight: true),
        new BigLoggerChannel("Rear Laser (mm)", "Logger Rear Ride Height", "Rear Ride Height", "F0", 0, 100,
            new ScottPlot.Color(0x43, 0xAA, 0x8B), true, IsRideHeight: true),
    };

    public static BigLoggerChannel? Find(string channel) =>
        All.FirstOrDefault(item => item.Channel == channel);
}
