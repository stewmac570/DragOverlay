using CastleOverlayV2.Models;

namespace CastleOverlayV2.Tests;

public class TrimUndoTests
{
    [Fact]
    public void Undo_puts_back_one_cut_at_a_time()
    {
        RunData run = CastleRun();

        Assert.True(run.SetTrimWindow(0.2, null));   // cut before 0.2
        Assert.True(run.SetTrimWindow(0.2, 0.8));    // cut after 0.8
        Assert.Equal(7, run.DataPoints.Count);

        Assert.True(run.UndoTrim());                 // second cut comes back, first stays
        Assert.Equal(0.2, run.DataPoints[0].Time, 6);
        Assert.Equal(1.0, run.DataPoints[^1].Time, 6);

        Assert.True(run.UndoTrim());                 // back to full length
        Assert.Equal(11, run.DataPoints.Count);
        Assert.False(run.UndoTrim());                // nothing left to undo
    }

    [Fact]
    public void A_cut_that_would_remove_everything_is_ignored_and_not_recorded()
    {
        RunData run = CastleRun();
        Assert.True(run.SetTrimWindow(0.5, null));

        Assert.False(run.SetTrimWindow(0.5, 0.4));   // end before start: no samples left

        Assert.Equal(0.5, run.TrimStartTime);
        Assert.Null(run.TrimEndTime);
        Assert.Equal(6, run.DataPoints.Count);
        Assert.Single(run.TrimHistory);
    }

    [Fact]
    public void Undo_after_reset_brings_the_cuts_back()
    {
        RunData run = CastleRun();
        run.SetTrimWindow(0.3, 0.7);

        // What Reset trim does on the presenter: remember the window, then clear it.
        run.TrimHistory.Push((run.TrimStartTime, run.TrimEndTime));
        run.TrimStartTime = null;
        run.TrimEndTime = null;
        run.ApplyManualTrim();
        Assert.Equal(11, run.DataPoints.Count);

        Assert.True(run.UndoTrim());
        Assert.Equal(5, run.DataPoints.Count);
    }

    private static RunData CastleRun()
    {
        var run = new RunData { FileName = "castle.csv" };
        for (int i = 0; i <= 10; i++)
            run.DataPoints.Add(new DataPoint { Time = i / 10.0, Throttle = 1.5, Speed = 1000 * i });
        run.CaptureTrimBaseline();
        return run;
    }
}
