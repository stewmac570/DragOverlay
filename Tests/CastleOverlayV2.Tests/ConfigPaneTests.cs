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
            PaneHeights = new List<double> { 0.4, 0.35, 0.25 },
            PaneFocusedChannels = new Dictionary<int, string>
            {
                [1] = "Voltage",
                [2] = "RaceBox Distance",
                [3] = "Logger RPM"
            }
        };

        string json = JsonConvert.SerializeObject(config);
        Config loaded = Assert.IsType<Config>(JsonConvert.DeserializeObject<Config>(json));

        Assert.Equal(3, loaded.PaneCount);
        Assert.Equal(2, loaded.ChannelPanes["RaceBox Speed"]);
        Assert.Equal(new[] { 0.4, 0.35, 0.25 }, loaded.PaneHeights);
        Assert.Equal("Voltage", loaded.PaneFocusedChannels[1]);
        Assert.Equal("RaceBox Distance", loaded.PaneFocusedChannels[2]);
        Assert.Equal("Logger RPM", loaded.PaneFocusedChannels[3]);
    }

    [Fact]
    public void Old_config_without_pane_focus_loads_with_no_saved_focus()
    {
        string json = """
            {
              "paneCount": 3,
              "channelPanes": {
                "RPM": 1,
                "RaceBox Speed": 2,
                "Logger RPM": 3
              },
              "paneHeights": [0.4, 0.35, 0.25]
            }
            """;

        Config loaded = Assert.IsType<Config>(JsonConvert.DeserializeObject<Config>(json));

        Assert.Empty(loaded.PaneFocusedChannels);
    }
}
