using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _workDir;

    public ConfigServiceTests()
    {
        _workDir = Path.Combine(
            Path.GetTempPath(),
            "DragOverlayConfigTests_" + Guid.NewGuid().ToString("N"));

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
            // Best effort cleanup.
        }
    }

    [Fact]
    public void Save_WritesConfigJson()
    {
        string configPath = Path.Combine(_workDir, "config.json");
        var service = new ConfigService(configPath);

        service.SetRpmMode(true);

        Assert.True(File.Exists(configPath));
        Assert.False(File.Exists(configPath + ".tmp"));

        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(
            File.ReadAllText(configPath));

        Assert.NotNull(loaded);
        Assert.True(loaded.IsFourPoleMode);
    }

    [Fact]
    public void Save_ReplacesExistingConfig()
    {
        string configPath = Path.Combine(_workDir, "config.json");
        var service = new ConfigService(configPath);

        service.SetRpmMode(false);
        service.SetRpmMode(true);

        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(
            File.ReadAllText(configPath));

        Assert.NotNull(loaded);
        Assert.True(loaded.IsFourPoleMode);
        Assert.False(File.Exists(configPath + ".tmp"));
    }
    [Fact]
    public void Save_WhenDestinationIsDirectory_DoesNotThrow()
    {
        string destinationPath = Path.Combine(_workDir, "not-a-file");
        Directory.CreateDirectory(destinationPath);

        var service = new ConfigService(destinationPath);

        service.SetRpmMode(true);

        Assert.True(Directory.Exists(destinationPath));
        Assert.False(File.Exists(destinationPath + ".tmp"));
    }
}
