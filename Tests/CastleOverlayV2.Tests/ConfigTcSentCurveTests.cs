using CastleOverlayV2.Models;
using Newtonsoft.Json;
using Xunit;

namespace CastleOverlayV2.Tests;

// The Logs chart draws the curves last sent to the logger, read back from config.json after a
// restart (Stew, 19 Nov 2026). The points must survive the round trip exactly.
public class ConfigTcSentCurveTests
{
    [Fact]
    public void SentCurvesSurviveSaveAndLoad()
    {
        var config = new Config
        {
            TcSentHardCurve = new List<TcCurvePoint> { new(0, 9000), new(500, 30000), new(2000, 52000) },
            TcSentSoftCurve = new List<TcCurvePoint> { new(0, 8000), new(1800, 49000) },
        };

        Config loaded = JsonConvert.DeserializeObject<Config>(JsonConvert.SerializeObject(config, Formatting.Indented))!;

        Assert.Equal(config.TcSentHardCurve, loaded.TcSentHardCurve);
        Assert.Equal(config.TcSentSoftCurve, loaded.TcSentSoftCurve);
    }

    [Fact]
    public void OldConfigWithoutSentCurvesLoadsAsNone()
    {
        Config loaded = JsonConvert.DeserializeObject<Config>("{\"AutoTrim\": true}")!;

        Assert.Null(loaded.TcSentHardCurve);
        Assert.Null(loaded.TcSentSoftCurve);
    }
}
