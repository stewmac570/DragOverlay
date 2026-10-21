using CastleOverlayV2.Plot;
using CastleOverlayV2.Utils;
using Xunit;

namespace CastleOverlayV2.Tests;

public class ChannelPaneMapTests
{
    [Fact]
    public void Known_channels_default_to_first_pane()
    {
        var map = new ChannelPaneMap(ChannelText.Order);

        Assert.All(ChannelText.Order, channel => Assert.Equal(0, map.GetPaneIndex(channel)));
    }

    [Fact]
    public void Channel_can_be_assigned_to_another_pane()
    {
        var map = new ChannelPaneMap(ChannelText.Order);

        map.Assign("RPM", 2);

        Assert.Equal(2, map.GetPaneIndex("RPM"));
    }

    [Fact]
    public void Unknown_channel_defaults_to_first_pane()
    {
        var map = new ChannelPaneMap(ChannelText.Order);

        Assert.Equal(0, map.GetPaneIndex("Future Channel"));
    }
}
