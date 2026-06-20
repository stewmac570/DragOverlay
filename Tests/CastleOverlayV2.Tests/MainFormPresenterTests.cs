using CastleOverlayV2;
using CastleOverlayV2.Controls;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using ScottPlot.WinForms;

namespace CastleOverlayV2.Tests;

/// <summary>
/// Smoke tests for the MainFormPresenter against a <see cref="FakeMainView"/>
/// (issue #69). Establishes that the interface + fake combination work
/// end-to-end; more presenter behaviour can be tested by extending these.
/// </summary>
public sealed class MainFormPresenterTests
{
    [Fact]
    public void FakeMainView_records_every_view_call()
    {
        IMainView view = new FakeMainView();
        view.ShowError("Title", "Body");
        view.SetSlotLoadedUI(2, "C:\\run.csv", true);
        view.ResetSlotUI(4);
        view.ApplyRunTypeUI(true);
        view.HideAlignmentUI();

        var fake = (FakeMainView)view;
        Assert.Single(fake.Errors);
        Assert.Equal(("Title", "Body"), fake.Errors[0]);
        Assert.Single(fake.SlotLoaded);
        Assert.Single(fake.SlotReset);
        Assert.Equal(4, fake.SlotReset[0]);
        Assert.Single(fake.RunTypeApplies);
        Assert.True(fake.RunTypeApplies[0]);
        Assert.Equal(1, fake.AlignmentHidden);
    }

    [Fact]
    public void Presenter_can_be_constructed_against_a_fake_view()
    {
        // Construction exercises the ctor's event-subscriptions on every
        // collaborator. The ChannelDrawer + TunePanel + PlotManager need
        // real WinForms instances; the test project targets net8.0-windows
        // so this is supported.
        var view = new FakeMainView();
        var config = new ConfigService();
        var formsPlot = new FormsPlot();
        try
        {
            var plot = new PlotManager(formsPlot);
            var drawer = new ChannelDrawer(
                new List<string> { "RPM", "Throttle %" },
                new Dictionary<string, bool> { ["RPM"] = true, ["Throttle %"] = true });
            var tune = new TunePanel();

            var presenter = new MainFormPresenter(view, config, plot, drawer, tune);

            Assert.False(presenter.IsSpeedRunMode);
            Assert.False(presenter.IsAnyRunLoaded);
            Assert.False(presenter.IsAlignmentArmed);
        }
        finally
        {
            formsPlot.Dispose();
        }
    }

    [Fact]
    public async Task LoadCastleRunAsync_returns_immediately_when_picker_cancels()
    {
        var view = new FakeMainView { CsvFileToReturn = null };
        var config = new ConfigService();
        var formsPlot = new FormsPlot();
        try
        {
            var plot = new PlotManager(formsPlot);
            var drawer = new ChannelDrawer(
                new List<string> { "RPM" },
                new Dictionary<string, bool> { ["RPM"] = true });
            var tune = new TunePanel();
            var presenter = new MainFormPresenter(view, config, plot, drawer, tune);

            await presenter.LoadCastleRunAsync(1);

            // Nothing was loaded → no UI updates, no errors, no infos.
            Assert.Empty(view.SlotLoaded);
            Assert.Empty(view.Errors);
            Assert.Empty(view.Infos);
            Assert.False(presenter.IsAnyRunLoaded);
        }
        finally
        {
            formsPlot.Dispose();
        }
    }
}
