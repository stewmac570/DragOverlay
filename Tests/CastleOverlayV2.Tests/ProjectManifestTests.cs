using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using Newtonsoft.Json.Linq;

namespace CastleOverlayV2.Tests;

public sealed class ProjectManifestTests
{
    [Fact]
    public void Manifest_RoundTrips_AllVersionOneState()
    {
        var manifest = CreateManifest();
        var json = new ProjectManifestJson();

        string text = json.Serialize(manifest);
        ProjectManifestLoadResult result = json.Deserialize(text);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
        ProjectManifest loaded = Assert.IsType<ProjectManifest>(result.Manifest);
        Assert.Equal(ProjectFormat.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(ProjectRunMode.Drag, loaded.RunMode);
        Assert.False(loaded.ChannelVisibility["RPM"]);
        Assert.Equal(2, loaded.Runs.Count);

        ProjectRunEntry castle = loaded.Runs[0];
        Assert.Equal(ProjectSourceType.Castle, castle.SourceType);
        Assert.Equal(1, castle.UiSlot);
        Assert.Equal(1, castle.PlotSlot);
        Assert.Equal("tunes/castle-slot-1.dat", castle.TunePath);
        Assert.Equal(42.5, castle.TimeShiftMs);
        Assert.Equal(3, castle.RadioSettings?.Mode);
        Assert.Equal(60, castle.RadioSettings?.Point2Percent);

        ProjectRunEntry raceBox = loaded.Runs[1];
        Assert.Equal(ProjectSourceType.RaceBox, raceBox.SourceType);
        Assert.Equal(1, raceBox.UiSlot);
        Assert.Equal(4, raceBox.PlotSlot);
        Assert.False(raceBox.IsVisible);
    }

    [Fact]
    public void Deserialize_IgnoresUnknownFutureProperties()
    {
        var json = new ProjectManifestJson();
        JObject document = JObject.Parse(json.Serialize(CreateManifest()));
        document["futureSetting"] = "ignored";
        ((JObject)document["runs"]![0]!)["futureRunSetting"] = 123;

        ProjectManifestLoadResult result = json.Deserialize(document.ToString());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(2, result.Manifest?.Runs.Count);
    }

    [Fact]
    public void Deserialize_ReturnsUnsupportedVersionResult()
    {
        JObject document = JObject.Parse(new ProjectManifestJson().Serialize(CreateManifest()));
        document["schemaVersion"] = ProjectFormat.CurrentSchemaVersion + 1;

        ProjectManifestLoadResult result =
            new ProjectManifestJson().Deserialize(document.ToString());

        Assert.Equal(ProjectManifestLoadStatus.UnsupportedVersion, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("newer than", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"C:\logs\run.csv")]
    [InlineData("/logs/run.csv")]
    [InlineData("../run.csv")]
    [InlineData("logs/../run.csv")]
    [InlineData(@"logs\run.csv")]
    [InlineData("logs//run.csv")]
    public void Validator_RejectsUnsafePackagePaths(string path)
    {
        ProjectManifest manifest = CreateManifest();
        manifest.Runs[0].SourcePath = path;

        ProjectManifestValidationResult result =
            new ProjectManifestValidator().Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("SourcePath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsInvalidSlotMappingAndSourceTypeRules()
    {
        ProjectManifest manifest = CreateManifest();
        manifest.Runs[1].PlotSlot = 1;
        manifest.Runs[1].TunePath = "tunes/racebox-slot-1.dat";

        ProjectManifestValidationResult result =
            new ProjectManifestValidator().Validate(manifest);

        Assert.Contains(result.Errors, error => error.Contains("PlotSlot must be 4", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("only valid for Castle", StringComparison.Ordinal));
    }

    [Fact]
    public void Serialize_RejectsInvalidManifest()
    {
        ProjectManifest manifest = CreateManifest();
        manifest.AppBuild = "";

        ProjectManifestException exception = Assert.Throws<ProjectManifestException>(
            () => new ProjectManifestJson().Serialize(manifest));

        Assert.Contains(exception.Errors, error => error.Contains("AppBuild", StringComparison.Ordinal));
    }

    private static ProjectManifest CreateManifest() => new()
    {
        AppBuild = "1.13",
        CreatedUtc = new DateTimeOffset(2026, 6, 20, 2, 0, 0, TimeSpan.Zero),
        RunMode = ProjectRunMode.Drag,
        ChannelVisibility = new Dictionary<string, bool>
        {
            ["RPM"] = false,
            ["Throttle %"] = true,
            ["RaceBox Speed"] = true
        },
        Runs =
        {
            new ProjectRunEntry
            {
                SourceType = ProjectSourceType.Castle,
                UiSlot = 1,
                PlotSlot = 1,
                DisplayFileName = "castle run.csv",
                SourcePath = "logs/castle-slot-1.csv",
                IsVisible = true,
                TimeShiftMs = 42.5,
                TunePath = "tunes/castle-slot-1.dat",
                RadioSettings = new RadioTuneSettings
                {
                    ProfileName = "Race",
                    ThrottleSpeedMode = "Mode 3",
                    Mode = 3,
                    SpeedType = "Normal",
                    HighTurnPercent = 68,
                    HighReturnPercent = 100,
                    Point2Percent = 60,
                    MiddleTurnPercent = 36,
                    MiddleReturnPercent = 100,
                    Point1Percent = 19,
                    LowTurnPercent = 100,
                    LowReturnPercent = 100
                }
            },
            new ProjectRunEntry
            {
                SourceType = ProjectSourceType.RaceBox,
                UiSlot = 1,
                PlotSlot = 4,
                DisplayFileName = "racebox run.csv",
                SourcePath = "logs/racebox-slot-1.csv",
                IsVisible = false,
                TimeShiftMs = -15
            }
        }
    };
}
