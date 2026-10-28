namespace CastleOverlayV2.Models;

/// <summary>
/// One raw Mini Logger CSV sample. <see cref="TimeSeconds"/> is the file's original clock;
/// <see cref="LogsPageTimeSeconds"/> is the existing 50%-throttle launch alignment used by
/// the Logs page. TC fields are null for a four-column Mini Logger file.
/// </summary>
public readonly record struct MiniLoggerSample(
    double TimeSeconds,
    double LogsPageTimeSeconds,
    double ThrottleMs,
    double MotorRpm,
    double WheelRevsPerSecond,
    double? ThrottleOutMs,
    int? TcState);
