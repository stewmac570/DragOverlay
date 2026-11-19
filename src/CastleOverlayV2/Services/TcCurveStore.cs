using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services;

/// <summary>
/// The TC tab's current hard and soft curves, so the Logs chart can draw them over a TC logger
/// run. The TC tab sets them whenever its curves change; the Logs chart replots on <see cref="Changed"/>.
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
