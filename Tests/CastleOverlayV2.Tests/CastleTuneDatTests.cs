using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

/// <summary>
/// Rebuilds the .dat files Castle Link 2 saved on 2026-11-06 from the bytes read off the ESC
/// at the same moment (Tests/castle-link-samples/tune-2026-11-06-round*.bin).
/// </summary>
public sealed class CastleTuneDatTests
{
    // Lines the ESC does not hold, so Castle's app fills them from its own memory.
    private static readonly HashSet<string> AppOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "Current Limiting", "Torque Control Motor Constant", "Torque Control Value", "Programmed Throttle 65534"
    };

    [Theory]
    [InlineData("round3", new string[] { "Cutoff Voltage", "Auto-Lipo Volts/Cell", "Sensorless Motor Timing" })]
    [InlineData("round8", new string[0])]
    public void Rebuilt_tune_matches_the_dat_castle_saved(string round, string[] knownDifferences)
    {
        byte[] tune = File.ReadAllBytes(Path.Combine(SamplesFolder(), $"tune-2026-11-06-{round}.bin"));
        Dictionary<string, string> castle = Fields(File.ReadAllLines(Path.Combine(SamplesFolder(), $"tune-2026-11-06-{round}.dat")));

        Dictionary<string, string> rebuilt = Fields(CastleTuneDat.Build(tune, "GL00136", DateTime.Now).Split("\r\n"));

        var mismatches = rebuilt
            .Where(field => castle.ContainsKey(field.Key) && !AppOnly.Contains(field.Key) && !knownDifferences.Contains(field.Key))
            .Where(field => Clean(castle[field.Key]) != field.Value)
            .Select(field => $"{field.Key}: built '{field.Value}', Castle '{castle[field.Key]}'")
            .ToList();
        Assert.Empty(mismatches);

        var missing = castle.Keys.Where(key => !rebuilt.ContainsKey(key) && !AppOnly.Contains(key)).ToList();
        Assert.All(missing, key => Assert.Contains("Activation Range", key));
    }

    [Fact]
    public void Rebuilt_tune_loads_in_the_tune_page_with_its_curves()
    {
        byte[] tune = File.ReadAllBytes(Path.Combine(SamplesFolder(), "tune-2026-11-06-round8.bin"));

        string path = Path.Combine(Path.GetTempPath(), $"rebuilt-{Guid.NewGuid():N}.dat");
        File.WriteAllText(path, CastleTuneDat.Build(tune, "GL00136", DateTime.Now));
        var loaded = new CastleTuneLoader().Load(path).Value!;
        File.Delete(path);

        Assert.Equal("GhostRC Cobra 8 Ace", loaded.DeviceName);
        Assert.Equal("1.36", loaded.Firmware);
        Assert.Equal(257, loaded.ThrottleCurve.Count);
        Assert.Equal(35, loaded.TurboTimingAmountDeg);
    }

    [Fact]
    public void Programmed_throttle_settings_are_read_from_its_table()
    {
        byte[] tune = File.ReadAllBytes(Path.Combine(SamplesFolder(), "tune-2026-11-06-round9.bin"));

        Dictionary<string, string> rebuilt = Fields(CastleTuneDat.Build(tune, "GL00136", DateTime.Now).Split("\r\n"));

        Assert.Equal("2.00s", rebuilt["Programmed Throttle Time Scale"]);
        Assert.Equal("50%", rebuilt["Programmed Throttle Enable At Throttle"]);
        Assert.Equal("Drag Brake Adjustment", rebuilt["Auxiliary Wire Mode"]);
        Assert.Equal("Medium (160°F / 71°C)", rebuilt["Motor Temperature Cutoff"]);
    }

    [Fact]
    public void Unknown_esc_keeps_its_raw_id() =>
        Assert.Equal("XY00210", CastleTuneDat.DeviceNameFromId("XY00210"));

    private static string Clean(string value) => value.Replace("(*)", "").Trim();

    private static Dictionary<string, string> Fields(IEnumerable<string> lines) =>
        lines.Where(line => !line.StartsWith('#') && line.Contains(':'))
            .Select(line => (Key: line[..line.IndexOf(':')].Trim(), Value: line[(line.IndexOf(':') + 1)..].Trim()))
            .ToDictionary(field => field.Key, field => field.Value);

    private static string SamplesFolder()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null)
        {
            string candidate = Path.Combine(folder.FullName, "Tests", "castle-link-samples");
            if (Directory.Exists(candidate))
                return candidate;
            folder = folder.Parent;
        }
        throw new DirectoryNotFoundException("Tests/castle-link-samples was not found.");
    }
}
