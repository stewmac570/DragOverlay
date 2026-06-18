using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public class ThrottlePercentTests
{
    // Castle radio defaults (matches CsvLoader.cs).
    private const double FullReverse = 1.048;
    private const double Neutral = 1.500;
    private const double FullForward = 1.910;

    [Fact]
    public void Neutral_returns_zero()
    {
        Assert.Equal(0, ThrottlePercent.FromMilliseconds(Neutral, FullReverse, Neutral, FullForward));
    }

    [Fact]
    public void FullForward_returns_plus_100()
    {
        Assert.Equal(100, ThrottlePercent.FromMilliseconds(FullForward, FullReverse, Neutral, FullForward));
    }

    [Fact]
    public void FullReverse_returns_minus_100()
    {
        Assert.Equal(-100, ThrottlePercent.FromMilliseconds(FullReverse, FullReverse, Neutral, FullForward));
    }

    [Fact]
    public void Halfway_forward_returns_plus_50()
    {
        double halfway = Neutral + (FullForward - Neutral) / 2.0;
        Assert.Equal(50, ThrottlePercent.FromMilliseconds(halfway, FullReverse, Neutral, FullForward), precision: 6);
    }

    [Fact]
    public void Halfway_reverse_returns_minus_50()
    {
        double halfway = Neutral - (Neutral - FullReverse) / 2.0;
        Assert.Equal(-50, ThrottlePercent.FromMilliseconds(halfway, FullReverse, Neutral, FullForward), precision: 6);
    }

    [Fact]
    public void Above_full_forward_clamps_to_100()
    {
        Assert.Equal(100, ThrottlePercent.FromMilliseconds(FullForward + 0.5, FullReverse, Neutral, FullForward));
    }

    [Fact]
    public void Below_full_reverse_clamps_to_minus_100()
    {
        Assert.Equal(-100, ThrottlePercent.FromMilliseconds(FullReverse - 0.5, FullReverse, Neutral, FullForward));
    }

    [Fact]
    public void Inverted_cal_returns_zero()
    {
        // Neutral outside [min, max] → guard rail: return 0
        Assert.Equal(0, ThrottlePercent.FromMilliseconds(1.7, 1.5, 1.0, 1.9));
    }

    [Fact]
    public void Zero_forward_span_returns_zero_at_neutral_or_above()
    {
        // neutralMs == fullForwardMs collapses forward span.
        // Below-neutral path still works; at/above-neutral path must guard.
        double pct = ThrottlePercent.FromMilliseconds(1.6, 1.0, 1.5, 1.5);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void Zero_reverse_span_returns_zero_below_neutral()
    {
        // neutralMs == fullReverseMs collapses reverse span.
        // Above-neutral path still works; below-neutral path must guard.
        double pct = ThrottlePercent.FromMilliseconds(1.4, 1.5, 1.5, 2.0);
        Assert.Equal(0, pct);
    }
}
