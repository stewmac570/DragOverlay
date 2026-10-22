using CastleOverlayV2.Services;
using System.Globalization;
using System.Text;

namespace CastleOverlayV2.Tests;

public sealed class MiniLoggerTyreDiameterTests
{
    [Fact]
    public void Wheel_mph_uses_configured_front_tyre_diameter()
    {
        string path = WriteRun();
        try
        {
            var at70 = MiniLoggerLoader.Load(path, tyreDiameterMm: 70).Value!
                .Data[MiniLoggerLoader.WheelSpeedChannel];
            var at84 = MiniLoggerLoader.Load(path, tyreDiameterMm: 84).Value!
                .Data[MiniLoggerLoader.WheelSpeedChannel];

            int sample = at70.Count - 1;
            Assert.Equal(at70[sample].Y * 1.2, at84[sample].Y, 10);
            Assert.Equal(Math.PI * 0.070 * 2.23694,
                MiniLoggerLoader.MphPerRevPerSec(70), 10);
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
            double wheel = t < 1.0 ? 0 : (t - 1.0) * 30;
            content.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{t:0.0000},{throttle:0.000},0,{wheel:0.00}"));
        }

        string path = Path.Combine(Path.GetTempPath(), $"minilogger-tyre-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content.ToString());
        return path;
    }
}
