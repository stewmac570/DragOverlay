using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public sealed class TcCurveEditorTests
{
    [Fact]
    public void Insert_places_point_in_time_order()
    {
        var points = new List<TcCurvePoint>
        {
            new(0, 10_000),
            new(20, 12_000)
        };

        bool added = TcCurveEditor.TryInsert(points, 10, 11_000, out string error);

        Assert.True(added, error);
        Assert.Equal(new[] { 0, 10, 20 }, points.Select(point => point.TimeMs));
    }

    [Fact]
    public void Insert_refuses_a_129th_point()
    {
        var points = Enumerable.Range(0, TcCurveBuilder.MaximumPoints)
            .Select(time => new TcCurvePoint(time, 1_000 + time))
            .ToList();

        bool added = TcCurveEditor.TryInsert(points, 500, 20_000, out string error);

        Assert.False(added);
        Assert.Contains("128", error);
    }

    [Theory]
    [InlineData(-1, "before zero")]
    [InlineData(10, "already exists")]
    public void Insert_refuses_invalid_time(int timeMs, string message)
    {
        var points = new List<TcCurvePoint> { new(10, 10_000) };

        bool added = TcCurveEditor.TryInsert(points, timeMs, 11_000, out string error);

        Assert.False(added);
        Assert.Contains(message, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Move_clamps_time_between_neighbours_and_rpm_to_one()
    {
        var points = new List<TcCurvePoint>
        {
            new(10, 10_000),
            new(20, 20_000),
            new(30, 30_000)
        };

        TcCurvePoint low = TcCurveEditor.Move(points, 1, 0, 0);
        TcCurvePoint high = TcCurveEditor.Move(points, 1, 100, 40_000);

        Assert.Equal(new TcCurvePoint(11, 1), low);
        Assert.Equal(new TcCurvePoint(29, 40_000), high);
    }

    [Fact]
    public void Delete_removes_the_selected_point()
    {
        var points = new List<TcCurvePoint> { new(0, 10_000), new(10, 20_000) };

        TcCurveEditor.Delete(points, 0);

        Assert.Equal(new[] { new TcCurvePoint(10, 20_000) }, points);
    }

    [Fact]
    public void Hit_test_returns_nearest_point_inside_radius_and_none_outside()
    {
        TcEditorPixel[] pixels = { new(0, 0), new(4, 0), new(20, 20) };

        int nearest = TcCurveEditor.HitTest(pixels, new TcEditorPixel(3, 0), 5);
        int outside = TcCurveEditor.HitTest(pixels, new TcEditorPixel(10, 10), 5);

        Assert.Equal(1, nearest);
        Assert.Equal(-1, outside);
    }
}
