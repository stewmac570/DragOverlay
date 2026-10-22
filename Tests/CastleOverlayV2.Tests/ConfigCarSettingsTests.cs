using CastleOverlayV2.Models;
using Newtonsoft.Json;

namespace CastleOverlayV2.Tests;

public sealed class ConfigCarSettingsTests
{
    [Fact]
    public void Car_gearing_settings_round_trip()
    {
        var config = new Config
        {
            FrontTyreDiameterMm = 69.5,
            RearWheelDiameterMm = 71.2,
            PinionTeeth = 30,
            SpurTeeth = 72,
            TransmissionRatio = 1.85,
        };

        string json = JsonConvert.SerializeObject(config);
        Config loaded = Assert.IsType<Config>(JsonConvert.DeserializeObject<Config>(json));

        Assert.Equal(69.5, loaded.FrontTyreDiameterMm);
        Assert.Equal(71.2, loaded.RearWheelDiameterMm);
        Assert.Equal(30, loaded.PinionTeeth);
        Assert.Equal(72, loaded.SpurTeeth);
        Assert.Equal(1.85, loaded.TransmissionRatio);
    }

    [Fact]
    public void Old_config_leaves_car_gearing_unset_with_direct_drive_ratio()
    {
        Config loaded = Assert.IsType<Config>(JsonConvert.DeserializeObject<Config>("{}"));

        Assert.Null(loaded.RearWheelDiameterMm);
        Assert.Null(loaded.PinionTeeth);
        Assert.Null(loaded.SpurTeeth);
        Assert.Equal(1.0, loaded.TransmissionRatio);
    }
}
