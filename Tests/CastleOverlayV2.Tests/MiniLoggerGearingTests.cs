using CastleOverlayV2.Services;
using System.Globalization;
using System.Text;

namespace CastleOverlayV2.Tests;

public sealed class MiniLoggerGearingTests
{
    [Fact]
    public void Rear_speed_uses_motor_rpm_and_final_drive_ratio()
    {
        double speed = MiniLoggerLoader.RearSpeedMph(30_000, 70, 30, 72);

        double expected = (30_000.0 / 60.0 / 2.4) * Math.PI * 0.070 * 2.23694;
        Assert.Equal(expected, speed, 10);
        Assert.InRange(speed, 102.4, 102.6);
    }

    [Fact]
    public void Transmission_ratio_two_halves_rear_speed()
    {
        double direct = MiniLoggerLoader.RearSpeedMph(30_000, 70, 30, 72, 1.0);
        double geared = MiniLoggerLoader.RearSpeedMph(30_000, 70, 30, 72, 2.0);

        Assert.Equal(direct / 2.0, geared, 10);
    }

    [Theory]
    [InlineData(50, 40, 20)]
    [InlineData(1.99, 40, 0)]
    [InlineData(50, 75, -50)]
    [InlineData(50, 125, -100)]
    public void Slip_is_calculated_guarded_and_clamped(
        double rearSpeed, double frontSpeed, double expected)
    {
        Assert.Equal(expected, MiniLoggerLoader.SlipPercent(rearSpeed, frontSpeed), 10);
    }

    [Fact]
    public void Rear_channels_require_rear_diameter_pinion_and_spur()
    {
        string path = WriteRun();
        try
        {
            var missingRear = MiniLoggerLoader.Load(path,
                rearWheelDiameterMm: null, pinionTeeth: 30, spurTeeth: 72).Value!;
            var missingPinion = MiniLoggerLoader.Load(path,
                rearWheelDiameterMm: 70, pinionTeeth: null, spurTeeth: 72).Value!;
            var missingSpur = MiniLoggerLoader.Load(path,
                rearWheelDiameterMm: 70, pinionTeeth: 30, spurTeeth: null).Value!;

            foreach (var run in new[] { missingRear, missingPinion, missingSpur })
            {
                Assert.DoesNotContain(MiniLoggerLoader.RearSpeedChannel, run.Data.Keys);
                Assert.DoesNotContain(MiniLoggerLoader.SlipChannel, run.Data.Keys);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rear_channels_are_present_when_required_gearing_is_set()
    {
        string path = WriteRun();
        try
        {
            var run = MiniLoggerLoader.Load(path,
                rearWheelDiameterMm: 70, pinionTeeth: 30, spurTeeth: 72).Value!;

            Assert.NotEmpty(run.Data[MiniLoggerLoader.RearSpeedChannel]);
            Assert.NotEmpty(run.Data[MiniLoggerLoader.SlipChannel]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteRun()
    {
        var content = new StringBuilder(MiniLoggerLoader.HeaderLine).AppendLine();
        for (int i = 0; i < 3000; i++)
        {
            double t = i / 1000.0;
            double throttle = t < 1.0 ? 1.50 : 1.90;
            double rpm = t < 1.0 ? 0 : 30_000;
            double wheel = t < 1.0 ? 0 : 80;
            content.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{t:0.0000},{throttle:0.000},{rpm:0},{wheel:0.00}"));
        }

        string path = Path.Combine(Path.GetTempPath(), $"minilogger-gearing-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content.ToString());
        return path;
    }
}
