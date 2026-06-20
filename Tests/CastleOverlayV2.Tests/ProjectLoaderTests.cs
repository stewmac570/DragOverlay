using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using System.IO.Compression;
using System.Text;

namespace CastleOverlayV2.Tests;

public sealed class ProjectLoaderTests : IDisposable
{
    private readonly string _workDir;

    public ProjectLoaderTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(),
            "DragOverlayLoaderTests_" + Guid.NewGuid().ToString("N"));
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
    public void Load_FailsCleanly_WhenManifestMissing()
    {
        string pkg = Path.Combine(_workDir, "no-manifest.dragoverlay");
        using (var fs = new FileStream(pkg, FileMode.Create))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("logs/castle-1.csv");
            using var s = entry.Open();
            var bytes = Encoding.UTF8.GetBytes("Time,Throttle\n");
            s.Write(bytes, 0, bytes.Length);
        }

        var loader = new ProjectLoader(new ConfigService());
        var result = loader.Load(pkg);

        Assert.False(result.Ok);
        Assert.Contains("project.json", result.Message ?? "");
    }

    [Fact]
    public void Load_FailsCleanly_OnNonZipFile()
    {
        string fake = Path.Combine(_workDir, "not-a-zip.dragoverlay");
        File.WriteAllText(fake, "this is not a zip");

        var loader = new ProjectLoader(new ConfigService());
        var result = loader.Load(fake);

        Assert.False(result.Ok);
    }

    [Fact]
    public void Load_FailsCleanly_WhenDeclaredFileMissing()
    {
        // Build a manifest that declares a Castle CSV that isn't in the archive.
        var manifest = new ProjectManifest
        {
            AppBuild = "test",
            CreatedUtc = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            RunMode = ProjectRunMode.Drag,
            ChannelVisibility = new Dictionary<string, bool>(),
            Runs =
            {
                new ProjectRunEntry
                {
                    SourceType = ProjectSourceType.Castle,
                    UiSlot = 1,
                    PlotSlot = 1,
                    DisplayFileName = "missing.csv",
                    SourcePath = "logs/castle-1.csv",
                    IsVisible = true
                }
            }
        };

        string pkg = Path.Combine(_workDir, "missing-decl.dragoverlay");
        using (var fs = new FileStream(pkg, FileMode.Create))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("project.json");
            using var s = entry.Open();
            byte[] bytes = Encoding.UTF8.GetBytes(new ProjectManifestJson().Serialize(manifest));
            s.Write(bytes, 0, bytes.Length);
        }

        var loader = new ProjectLoader(new ConfigService());
        var result = loader.Load(pkg);

        Assert.False(result.Ok);
        Assert.Contains("missing", result.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
