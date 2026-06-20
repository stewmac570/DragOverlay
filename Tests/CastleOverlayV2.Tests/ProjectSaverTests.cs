using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using System.IO.Compression;
using System.Text;

namespace CastleOverlayV2.Tests;

public sealed class ProjectSaverTests : IDisposable
{
    private readonly string _workDir;

    public ProjectSaverTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(),
            "DragOverlayTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workDir))
                Directory.Delete(_workDir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void Save_WritesPackage_For_CastleOnly_Session()
    {
        var castleBytes = SyntheticBytes("castle-1");
        string castleCsvPath = WriteFile("castle-original.csv", castleBytes);

        var snapshot = new ProjectSnapshot
        {
            Manifest = BaseManifest(new ProjectRunEntry
            {
                SourceType = ProjectSourceType.Castle,
                UiSlot = 1,
                PlotSlot = 1,
                DisplayFileName = "castle-original.csv",
                SourcePath = "logs/castle-1.csv",
                IsVisible = true,
                TimeShiftMs = 0
            }),
            Files = new[]
            {
                new ProjectSnapshotFile(castleCsvPath, "logs/castle-1.csv")
            }
        };

        string dest = Path.Combine(_workDir, "session.dragoverlay");
        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.True(result.Ok, result.Message);
        Assert.Equal(dest, result.Value);
        Assert.True(File.Exists(dest));

        using var archive = ZipFile.OpenRead(dest);
        Assert.NotNull(archive.GetEntry("project.json"));
        var entry = Assert.IsAssignableFrom<ZipArchiveEntry>(archive.GetEntry("logs/castle-1.csv"));
        Assert.Equal(castleBytes, ReadEntryBytes(entry));
    }

    [Fact]
    public void Save_WritesPackage_For_RaceBoxOnly_Session()
    {
        var raceboxBytes = SyntheticBytes("racebox-1");
        string raceboxCsvPath = WriteFile("racebox.csv", raceboxBytes);

        var snapshot = new ProjectSnapshot
        {
            Manifest = BaseManifest(new ProjectRunEntry
            {
                SourceType = ProjectSourceType.RaceBox,
                UiSlot = 1,
                PlotSlot = 4,
                DisplayFileName = "racebox.csv",
                SourcePath = "logs/racebox-1.csv",
                IsVisible = true,
                TimeShiftMs = 0
            }),
            Files = new[]
            {
                new ProjectSnapshotFile(raceboxCsvPath, "logs/racebox-1.csv")
            }
        };

        string dest = Path.Combine(_workDir, "racebox-only.dragoverlay");
        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.True(result.Ok, result.Message);
        using var archive = ZipFile.OpenRead(dest);
        Assert.NotNull(archive.GetEntry("project.json"));
        Assert.NotNull(archive.GetEntry("logs/racebox-1.csv"));
    }

    [Fact]
    public void Save_WritesPackage_For_Mixed_Session_WithTune()
    {
        var castleBytes = SyntheticBytes("castle-mixed");
        var raceboxBytes = SyntheticBytes("racebox-mixed");
        var tuneBytes = SyntheticBytes("tune-mixed");

        string castlePath = WriteFile("c.csv", castleBytes);
        string raceboxPath = WriteFile("rb.csv", raceboxBytes);
        string tunePath = WriteFile("t.dat", tuneBytes);

        var snapshot = new ProjectSnapshot
        {
            Manifest = BaseManifest(
                new ProjectRunEntry
                {
                    SourceType = ProjectSourceType.Castle,
                    UiSlot = 1,
                    PlotSlot = 1,
                    DisplayFileName = "c.csv",
                    SourcePath = "logs/castle-1.csv",
                    IsVisible = true,
                    TimeShiftMs = 25.0,
                    TunePath = "tunes/castle-1.dat",
                    RadioSettings = new RadioTuneSettings { Mode = 2, Point1Percent = 30, Point2Percent = 70 }
                },
                new ProjectRunEntry
                {
                    SourceType = ProjectSourceType.RaceBox,
                    UiSlot = 1,
                    PlotSlot = 4,
                    DisplayFileName = "rb.csv",
                    SourcePath = "logs/racebox-1.csv",
                    IsVisible = false,
                    TimeShiftMs = -10.0
                }),
            Files = new[]
            {
                new ProjectSnapshotFile(castlePath, "logs/castle-1.csv"),
                new ProjectSnapshotFile(raceboxPath, "logs/racebox-1.csv"),
                new ProjectSnapshotFile(tunePath, "tunes/castle-1.dat")
            }
        };

        string dest = Path.Combine(_workDir, "mixed.dragoverlay");
        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.True(result.Ok, result.Message);
        using var archive = ZipFile.OpenRead(dest);

        // Source bytes preserved verbatim.
        Assert.Equal(castleBytes, ReadEntryBytes(archive.GetEntry("logs/castle-1.csv")!));
        Assert.Equal(raceboxBytes, ReadEntryBytes(archive.GetEntry("logs/racebox-1.csv")!));
        Assert.Equal(tuneBytes, ReadEntryBytes(archive.GetEntry("tunes/castle-1.dat")!));

        // Manifest round-trips and includes the radio settings + offsets.
        string manifestJson;
        using (var ms = new MemoryStream())
        {
            archive.GetEntry("project.json")!.Open().CopyTo(ms);
            manifestJson = Encoding.UTF8.GetString(ms.ToArray());
        }
        var loaded = new ProjectManifestJson().Deserialize(manifestJson);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Errors));
        Assert.Equal(2, loaded.Manifest!.Runs.Count);
        Assert.Equal(25.0, loaded.Manifest.Runs[0].TimeShiftMs);
        Assert.Equal(2, loaded.Manifest.Runs[0].RadioSettings?.Mode);
        Assert.Equal(-10.0, loaded.Manifest.Runs[1].TimeShiftMs);
        Assert.False(loaded.Manifest.Runs[1].IsVisible);
    }

    [Fact]
    public void Save_ReplacesExistingDestination()
    {
        var bytes = SyntheticBytes("replace-test");
        string srcPath = WriteFile("c.csv", bytes);
        string dest = Path.Combine(_workDir, "exists.dragoverlay");

        // Pre-existing destination with different content.
        File.WriteAllText(dest, "not a zip");

        var snapshot = new ProjectSnapshot
        {
            Manifest = BaseManifest(new ProjectRunEntry
            {
                SourceType = ProjectSourceType.Castle,
                UiSlot = 1,
                PlotSlot = 1,
                DisplayFileName = "c.csv",
                SourcePath = "logs/castle-1.csv",
                IsVisible = true
            }),
            Files = new[] { new ProjectSnapshotFile(srcPath, "logs/castle-1.csv") }
        };

        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.True(result.Ok, result.Message);
        using var archive = ZipFile.OpenRead(dest);
        Assert.NotNull(archive.GetEntry("project.json"));
    }

    [Fact]
    public void Save_DoesNotLeavePartialFile_WhenSourceMissing()
    {
        string dest = Path.Combine(_workDir, "wont-exist.dragoverlay");

        var snapshot = new ProjectSnapshot
        {
            Manifest = BaseManifest(new ProjectRunEntry
            {
                SourceType = ProjectSourceType.Castle,
                UiSlot = 1,
                PlotSlot = 1,
                DisplayFileName = "missing.csv",
                SourcePath = "logs/castle-1.csv",
                IsVisible = true
            }),
            Files = new[]
            {
                new ProjectSnapshotFile(
                    Path.Combine(_workDir, "this-file-does-not-exist.csv"),
                    "logs/castle-1.csv")
            }
        };

        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.False(result.Ok);
        Assert.False(File.Exists(dest));
        // No leftover temp files.
        Assert.Empty(Directory.GetFiles(_workDir, "*.tmp"));
    }

    [Fact]
    public void Save_RejectsDuplicateArchivePaths()
    {
        string srcA = WriteFile("a.csv", SyntheticBytes("a"));
        string srcB = WriteFile("b.csv", SyntheticBytes("b"));

        var snapshot = new ProjectSnapshot
        {
            Manifest = BaseManifest(new ProjectRunEntry
            {
                SourceType = ProjectSourceType.Castle,
                UiSlot = 1,
                PlotSlot = 1,
                DisplayFileName = "a.csv",
                SourcePath = "logs/dup.csv",
                IsVisible = true
            }),
            Files = new[]
            {
                new ProjectSnapshotFile(srcA, "logs/dup.csv"),
                new ProjectSnapshotFile(srcB, "logs/dup.csv")
            }
        };

        string dest = Path.Combine(_workDir, "dup.dragoverlay");
        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.False(result.Ok);
        Assert.Contains("dup", result.Message);
        Assert.False(File.Exists(dest));
    }

    [Fact]
    public void Save_RejectsUnsafeArchivePath()
    {
        string src = WriteFile("a.csv", SyntheticBytes("a"));

        var snapshot = new ProjectSnapshot
        {
            Manifest = BaseManifest(new ProjectRunEntry
            {
                SourceType = ProjectSourceType.Castle,
                UiSlot = 1,
                PlotSlot = 1,
                DisplayFileName = "a.csv",
                SourcePath = "logs/castle-1.csv",
                IsVisible = true
            }),
            Files = new[]
            {
                // Manifest entry above is fine; the file entry uses an unsafe path.
                new ProjectSnapshotFile(src, "..\\evil.csv")
            }
        };

        string dest = Path.Combine(_workDir, "unsafe.dragoverlay");
        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.False(result.Ok);
        Assert.False(File.Exists(dest));
    }

    [Fact]
    public void Save_RejectsInvalidManifest()
    {
        string src = WriteFile("a.csv", SyntheticBytes("a"));

        var bad = BaseManifest(new ProjectRunEntry
        {
            SourceType = ProjectSourceType.Castle,
            UiSlot = 1,
            PlotSlot = 1,
            DisplayFileName = "a.csv",
            SourcePath = "logs/castle-1.csv",
            IsVisible = true
        });
        bad.AppBuild = ""; // validator requires AppBuild

        var snapshot = new ProjectSnapshot
        {
            Manifest = bad,
            Files = new[] { new ProjectSnapshotFile(src, "logs/castle-1.csv") }
        };

        string dest = Path.Combine(_workDir, "invalid.dragoverlay");
        var result = new ProjectSaver().Save(dest, snapshot);

        Assert.False(result.Ok);
        Assert.False(File.Exists(dest));
    }

    // ---- Helpers --------------------------------------------------------

    private string WriteFile(string name, byte[] bytes)
    {
        string path = Path.Combine(_workDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] SyntheticBytes(string seed)
    {
        // Deterministic, but distinguishable per fixture, and long enough that
        // accidental truncation is detectable.
        var sb = new StringBuilder();
        for (int i = 0; i < 32; i++)
            sb.Append(seed).Append('-').Append(i).Append('\n');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var ms = new MemoryStream();
        using (var s = entry.Open())
            s.CopyTo(ms);
        return ms.ToArray();
    }

    private static ProjectManifest BaseManifest(params ProjectRunEntry[] runs)
    {
        var manifest = new ProjectManifest
        {
            AppBuild = "1.13-test",
            CreatedUtc = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            RunMode = ProjectRunMode.Drag,
            ChannelVisibility = new Dictionary<string, bool>
            {
                ["RPM"] = true,
                ["Throttle %"] = true,
            }
        };
        foreach (var run in runs)
            manifest.Runs.Add(run);
        return manifest;
    }
}
