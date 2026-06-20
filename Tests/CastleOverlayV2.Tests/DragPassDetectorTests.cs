using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public class DragPassDetectorTests
{
    [Fact]
    public void Selects_stronger_real_pass_over_earlier_false_event()
    {
        List<DataPoint> data = Idle(30);
        AddBurst(data, current: 60, power: 78, rpm: 124_000, acceleration: 1.5, samples: 18);
        data.AddRange(Idle(120, data.Count));
        int expectedLaunch = data.Count;
        AddBurst(data, current: 670, power: 100, rpm: 171_000, acceleration: -4.1, samples: 35);

        DragPassDetectionResult result = DragPassDetector.Detect(data, 0.05);

        Assert.True(result.Found);
        Assert.InRange(result.LaunchIndex, expectedLaunch, expectedLaunch + 2);
    }

    [Fact]
    public void Does_not_require_positive_acceleration()
    {
        List<DataPoint> data = Idle(40);
        int expectedLaunch = data.Count;
        AddBurst(data, current: 520, power: 100, rpm: 150_000, acceleration: -3.2, samples: 30);

        DragPassDetectionResult result = DragPassDetector.Detect(data, 0.05);

        Assert.True(result.Found);
        Assert.InRange(result.LaunchIndex, expectedLaunch, expectedLaunch + 2);
    }

    [Fact]
    public void Ignores_bad_marked_power_when_detecting_launch()
    {
        List<DataPoint> data = Idle(20);
        for (int i = 0; i < 15; i++)
        {
            data.Add(Point(data.Count, current: 0, power: 100, rpm: 0, acceleration: 0, powerValid: false));
        }

        DragPassDetectionResult result = DragPassDetector.Detect(data, 0.05);

        Assert.False(result.Found);
    }

    private static List<DataPoint> Idle(int count, int startIndex = 0)
    {
        var data = new List<DataPoint>();
        for (int i = 0; i < count; i++)
            data.Add(Point(startIndex + i, current: 0, power: 0, rpm: 0, acceleration: 0));
        return data;
    }

    private static void AddBurst(List<DataPoint> data, double current, double power, double rpm, double acceleration, int samples)
    {
        for (int i = 0; i < samples; i++)
        {
            double scale = Math.Min(1.0, 0.35 + i * 0.08);
            data.Add(Point(data.Count, current * scale, power * scale, rpm * scale, acceleration));
        }
    }

    private static DataPoint Point(
        int index,
        double current,
        double power,
        double rpm,
        double acceleration,
        bool powerValid = true)
    {
        return new DataPoint
        {
            Time = index * 0.05,
            Throttle = current > 0 ? 1.8 : 1.5,
            PowerOut = power,
            PowerOutValid = powerValid,
            Current = current,
            CurrentValid = true,
            Speed = rpm,
            SpeedValid = true,
            Acceleration = acceleration
        };
    }

}
