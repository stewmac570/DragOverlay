using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services;

/// <summary>
/// The curves the Logs chart draws over a TC logger run: the TC tab's curve when one is made,
/// otherwise the one last sent to the logger (saved in the config, so it shows after a restart).
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
