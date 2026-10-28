using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services;

public readonly record struct TcEditorPixel(double X, double Y);

/// <summary>UI-free rules for editing a firmware-ready TC curve.</summary>
public static class TcCurveEditor
{
    public static bool TryInsert(
        IList<TcCurvePoint> points,
        int timeMs,
        int rpm,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count >= TcCurveBuilder.MaximumPoints)
        {
            error = $"The curve already has {TcCurveBuilder.MaximumPoints} points.";
            return false;
        }
        if (timeMs < 0)
        {
            error = "Curve time cannot be before zero.";
            return false;
        }
        if (points.Any(point => point.TimeMs == timeMs))
        {
            error = $"A point already exists at {timeMs} ms.";
            return false;
        }

        int index = 0;
        while (index < points.Count && points[index].TimeMs < timeMs) index++;
        points.Insert(index, new TcCurvePoint(timeMs, Math.Max(1, rpm)));
        error = "";
        return true;
    }

    public static TcCurvePoint Move(IList<TcCurvePoint> points, int index, int timeMs, int rpm)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (index < 0 || index >= points.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        int minimum = index == 0 ? 0 : points[index - 1].TimeMs + 1;
        int maximum = index == points.Count - 1 ? int.MaxValue : points[index + 1].TimeMs - 1;
        var moved = new TcCurvePoint(Math.Clamp(timeMs, minimum, maximum), Math.Max(1, rpm));
        points[index] = moved;
        return moved;
    }

    public static void Delete(IList<TcCurvePoint> points, int index)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (index < 0 || index >= points.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        points.RemoveAt(index);
    }

    public static int HitTest(
        IReadOnlyList<TcEditorPixel> pointPixels,
        TcEditorPixel mousePixel,
        double radiusPixels)
    {
        ArgumentNullException.ThrowIfNull(pointPixels);
        if (!double.IsFinite(radiusPixels) || radiusPixels < 0)
            throw new ArgumentOutOfRangeException(nameof(radiusPixels));

        double radiusSquared = radiusPixels * radiusPixels;
        double nearestSquared = double.PositiveInfinity;
        int nearest = -1;
        for (int i = 0; i < pointPixels.Count; i++)
        {
            double dx = pointPixels[i].X - mousePixel.X;
            double dy = pointPixels[i].Y - mousePixel.Y;
            double distanceSquared = dx * dx + dy * dy;
            if (distanceSquared <= radiusSquared && distanceSquared < nearestSquared)
            {
                nearest = i;
                nearestSquared = distanceSquared;
            }
        }
        return nearest;
    }
}
