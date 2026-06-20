using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public class AlignmentHelperTests
{
    [Fact]
    public void CastleLaunch_ReturnsOffsetToZero()
    {
        var run = new RunData
        {
            DataPoints =
            [
                new DataPoint { Time = -0.05, Throttle = 1.60, PowerOut = 0 },
                new DataPoint { Time = 0.04, Throttle = 1.70, PowerOut = 20 }
            ]
        };

        Assert.Equal(-40, AlignmentHelper.GetAutoOffsetMs(run));
    }

    [Fact]
    public void RaceBoxLaunch_RequiresThreeConsecutiveMovingSamples()
    {
        var run = new RunData { IsRaceBox = true };
        run.Data["RaceBox Speed"] =
        [
            new DataPoint { Time = -0.04, Y = 1.2 },
            new DataPoint { Time = 0.00, Y = 0.2 },
            new DataPoint { Time = 0.04, Y = 1.1 },
            new DataPoint { Time = 0.08, Y = 1.3 },
            new DataPoint { Time = 0.12, Y = 1.5 }
        ];

        Assert.Equal(-40, AlignmentHelper.GetAutoOffsetMs(run));
    }

    [Fact]
    public void MissingLaunch_ReturnsNullAndDoesNotMutateRun()
    {
        var run = new RunData
        {
            TimeShiftMs = 25,
            DataPoints =
            [
                new DataPoint { Time = 0, Throttle = 1.60, PowerOut = 0 }
            ]
        };

        Assert.Null(AlignmentHelper.GetAutoOffsetMs(run));
        Assert.Equal(25, run.TimeShiftMs);
    }
}
