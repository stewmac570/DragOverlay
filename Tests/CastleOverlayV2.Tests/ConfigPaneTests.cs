using CastleOverlayV2.Models;
using Newtonsoft.Json;

namespace CastleOverlayV2.Tests;

public sealed class ConfigPaneTests
{
    [Fact]
    public void Config_round_trips_split_view_state()
    {
        var config = new Config
        {
            PaneCount = 3,
            ChannelPanes = new Dictionary<string, int>
            {
                ["RPM"] = 1,
                ["RaceBox Speed"] = 2,
                ["Logger RPM"] = 3
            },
            PaneHeights = new List<double> { 0.4, 0.35, 0.25 }
        };

        string json = JsonConvert.SerializeObject(config);
        Config loaded = Assert.IsType<Config>(JsonConvert.DeserializeObject<Config>(json));

        Assert.Equal(3, loaded.PaneCount);
        Assert.Equal(2, loaded.ChannelPanes["RaceBox Speed"]);
        Assert.Equal(new[] { 0.4, 0.35, 0.25 }, loaded.PaneHeights);
    }
}
