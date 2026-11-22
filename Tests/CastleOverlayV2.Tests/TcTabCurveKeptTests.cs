using CastleOverlayV2.Controls;
using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using Xunit;

namespace CastleOverlayV2.Tests;

// The TC tab's curve must still be there after DragOverlay is closed and opened again, so the
// Logs chart can draw it over a TC run (Stew, 22 Nov 2026: "it didn't show the curve from the TC tab").
public class TcTabCurveKeptTests
{
    [Fact]
    public void TcTabCurveComesBackAfterRestartAndFeedsTheLogsChart()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"tc-tab-curve-{Guid.NewGuid():N}");
        var hard = new List<TcCurvePoint> { new(0, 9000), new(500, 30000), new(2000, 52000) };
        var soft = new List<TcCurvePoint> { new(0, 8000), new(1800, 49000) };

        RunOnSta(() =>
        {
            var first = new ConfigService(folder);
            first.Config.TcTabHardCurve = hard;
            first.Config.TcTabSoftCurve = soft;
            first.Save();

            TcCurveStore.Set(Array.Empty<TcCurvePoint>(), Array.Empty<TcCurvePoint>());
            var reopened = new ConfigService(folder);
            using var panel = new TcPanel(reopened);

            Assert.Equal(hard, TcCurveStore.Hard);
            Assert.Equal(soft, TcCurveStore.Soft);
            // Opening the tab must not wipe what was kept.
            Assert.Equal(hard, new ConfigService(folder).Config.TcTabHardCurve);
            Assert.Equal(soft, new ConfigService(folder).Config.TcTabSoftCurve);
        });

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void EmptyTcTabFallsBackToTheCurveLastSentToTheLogger()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"tc-tab-curve-{Guid.NewGuid():N}");
        var sent = new List<TcCurvePoint> { new(0, 7000), new(1500, 45000) };

        RunOnSta(() =>
        {
            var config = new ConfigService(folder);
            config.Config.TcSentHardCurve = sent;
            config.Save();

            TcCurveStore.Set(Array.Empty<TcCurvePoint>(), Array.Empty<TcCurvePoint>());
            using var panel = new TcPanel(new ConfigService(folder));

            Assert.Equal(sent, TcCurveStore.Hard);
        });

        Directory.Delete(folder, recursive: true);
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
