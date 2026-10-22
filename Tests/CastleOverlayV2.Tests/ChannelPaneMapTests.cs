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
        map.SetPaneCount(3);

        map.Assign("RPM", 2);

        Assert.Equal(2, map.GetPaneIndex("RPM"));
    }

    [Fact]
    public void Unknown_channel_defaults_to_first_pane()
    {
        var map = new ChannelPaneMap(ChannelText.Order);

        Assert.Equal(0, map.GetPaneIndex("Future Channel"));
    }

    [Fact]
    public void By_source_assigns_castle_gps_and_logger_to_separate_panes()
    {
        var map = new ChannelPaneMap(ChannelText.Order);

        map.ApplyBySource();

        Assert.Equal(3, map.PaneCount);
        Assert.Equal(0, map.GetPaneIndex("RPM"));
        Assert.Equal(1, map.GetPaneIndex("RaceBox Speed"));
        Assert.Equal(2, map.GetPaneIndex("Logger RPM"));
    }

    [Fact]
    public void Moving_channel_cycles_and_respects_pane_count()
    {
        var map = new ChannelPaneMap(ChannelText.Order);
        map.SetPaneCount(3);

        Assert.Equal(1, map.MoveToNextPane("RPM"));
        Assert.Equal(2, map.MoveToNextPane("RPM"));
        Assert.Equal(0, map.MoveToNextPane("RPM"));

        map.SetPaneCount(2);
        Assert.Equal(1, map.MoveToNextPane("RPM"));
        Assert.Equal(0, map.MoveToNextPane("RPM"));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Assign("RPM", 2));
    }
}
