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
                new DataPoint { Time = 0.04, Throttle = 1.70, PowerOut = 20, PowerOutValid = true }
            ]
        };

        Assert.Equal(-40, AlignmentHelper.GetAutoOffsetMs(run));
    }

    [Fact]
    public void CastleLaunch_IgnoresInvalidPowerOutSamples()
    {
        var run = new RunData
        {
            DataPoints =
            [
                new DataPoint
                {
                    Time = 0.04,
                    Throttle = 1.70,
                    PowerOut = 20,
                    PowerOutValid = false
                },
                new DataPoint
                {
                    Time = 0.08,
                    Throttle = 1.70,
                    PowerOut = 20,
                    PowerOutValid = true
                }
            ]
        };

        Assert.Equal(-80, AlignmentHelper.GetAutoOffsetMs(run));
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

    // Bench test, 3 Nov 2026: Castle session 36 and Big logger run 19 from the same power-up.
    // A separate script matched the raw logs: logger time = Castle time + 2.622 s on RPM
    // (2.608 s on throttle). The old throttle-step method put them 3.4 s apart, because each
    // log took a different blip as its launch.
    [Fact]
    public void Real_bench_pair_lines_up_on_rpm()
    {
        string folder = SamplesFolder();
        string configFolder = Path.Combine(Path.GetTempPath(), $"align-config-{Guid.NewGuid():N}");
        var castle = new CsvLoader(new ConfigService(configFolder))
            .Load(Path.Combine(folder, "bench-2026-11-03-castle.csv"), trimForDrag: true, keepFullLog: true).Value!;
        var logger = MiniLoggerLoader.Load(Path.Combine(folder, "bench-2026-11-03-logger.csv"), keepFullLog: true).Value!;

        double offsetMs = AlignmentHelper.GetMiniLoggerOffsetMs(logger, castle)!.Value;

        // Where each log's own t = 0 sits in its raw time, so the offset can be checked against the script.
        double castleZero = 10 * 0.05 - castle.DataPoints[10].Time;
        double loggerZero = logger.MiniLoggerSamples[0].TimeSeconds - logger.MiniLoggerSamples[0].LogsPageTimeSeconds;
        double expectedMs = (loggerZero - castleZero - 2.622) * 1000.0;
        Assert.True(Math.Abs(offsetMs - expectedMs) <= 20,
            $"offset {offsetMs:F0}, expected {expectedMs:F0}, unanchored rpm {AlignmentHelper.RpmMatchOffsetMs(logger, castle):F0}, " +
            $"castle zero {castleZero:F3} s ({castle.DataPoints.Count} rows, first t {castle.DataPoints[0].Time:F3}), " +
            $"logger zero {loggerZero:F3} s ({logger.MiniLoggerSamples.Count} rows)");

        // Auto trim on: the logger run is trimmed, and the presenter matches against the full
        // Castle file. Both keep the same t = 0, so the offset must not change.
        var trimmedLogger = MiniLoggerLoader.Load(Path.Combine(folder, "bench-2026-11-03-logger.csv")).Value!;
        double trimmedMs = AlignmentHelper.GetMiniLoggerOffsetMs(trimmedLogger, castle)!.Value;
        Assert.True(Math.Abs(trimmedMs - expectedMs) <= 20, $"auto trim on: offset {trimmedMs:F0}, expected {expectedMs:F0}");
    }

    // Why the presenter reads the full Castle file: 3 s of Castle RPM matched the wrong blip, 1.2 s out.
    [Fact]
    public void Trimmed_castle_window_alone_is_too_short_to_trust()
    {
        string folder = SamplesFolder();
        var config = new ConfigService(Path.Combine(Path.GetTempPath(), $"align-config-{Guid.NewGuid():N}"));
        var full = new CsvLoader(config).Load(Path.Combine(folder, "bench-2026-11-03-castle.csv"), true, keepFullLog: true).Value!;
        var trimmed = new CsvLoader(config).Load(Path.Combine(folder, "bench-2026-11-03-castle.csv"), true, keepFullLog: false).Value!;
        var logger = MiniLoggerLoader.Load(Path.Combine(folder, "bench-2026-11-03-logger.csv"), keepFullLog: true).Value!;

        double fullMs = AlignmentHelper.GetMiniLoggerOffsetMs(logger, full)!.Value;
        double trimmedMs = AlignmentHelper.GetMiniLoggerOffsetMs(logger, trimmed)!.Value;

        Assert.True(Math.Abs(trimmedMs - fullMs) > 500, $"full {fullMs:F0}, trimmed {trimmedMs:F0}");
    }

    [Fact]
    public void No_castle_rpm_falls_back_to_the_throttle_step()
    {
        var logger = new RunData { IsMiniLogger = true };
        var castle = new RunData();
        for (int i = 0; i < 3000; i++)
        {
            double t = i / 1000.0 - 1.0;
            logger.MiniLoggerSamples.Add(new MiniLoggerSample(i / 1000.0, t, 0, 1000 + i, 0, null, null));
        }
        logger.Data[MiniLoggerLoader.ThrottleChannel] = Enumerable.Range(0, 3000)
            .Select(i => new DataPoint { Time = i / 1000.0 - 1.0, Y = i < 1000 ? 0 : 100 }).ToList();
        for (int i = 0; i < 60; i++)
            castle.DataPoints.Add(new DataPoint { Time = i * 0.05 - 1.0, ThrottlePercent = i < 24 ? 0 : 100, Speed = 0 });

        Assert.Null(AlignmentHelper.RpmMatchOffsetMs(logger, castle));
        Assert.NotNull(AlignmentHelper.GetMiniLoggerOffsetMs(logger, castle));
    }

    private static string SamplesFolder()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null)
        {
            string candidate = Path.Combine(folder.FullName, "Tests", "alignment-samples");
            if (Directory.Exists(candidate))
                return candidate;
            folder = folder.Parent;
        }
        throw new DirectoryNotFoundException("Tests/alignment-samples was not found.");
    }
}
