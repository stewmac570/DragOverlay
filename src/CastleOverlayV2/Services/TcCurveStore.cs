using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services;

/// <summary>
/// The hard and soft curves last sent to the logger, so the Logs chart can draw them over a TC
/// logger run. Loaded from the config at start-up, and set again by the TC tab on each send.
/// </summary>
public static class TcCurveStore
{
    public static IReadOnlyList<TcCurvePoint> Hard { get; private set; } = Array.Empty<TcCurvePoint>();
    public static IReadOnlyList<TcCurvePoint> Soft { get; private set; } = Array.Empty<TcCurvePoint>();

    public static event Action? Changed;

    public static void Set(IReadOnlyList<TcCurvePoint> hard, IReadOnlyList<TcCurvePoint> soft)
    {
        if (hard.SequenceEqual(Hard) && soft.SequenceEqual(Soft)) return;
        Hard = hard.ToArray();
        Soft = soft.ToArray();
        Changed?.Invoke();
    }
}
