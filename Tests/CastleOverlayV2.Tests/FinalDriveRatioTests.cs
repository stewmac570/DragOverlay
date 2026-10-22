namespace CastleOverlayV2.Tests;

public sealed class FinalDriveRatioTests
{
    [Fact]
    public void Fdr_is_spur_over_pinion_times_transmission_ratio()
    {
        double? fdr = SettingsForm.FinalDriveRatio("21", "93", "2.6");

        Assert.NotNull(fdr);
        Assert.Equal(93.0 / 21.0 * 2.6, fdr!.Value, 6); // 11.51
    }

    [Fact]
    public void Blank_ratio_counts_as_no_gearbox()
    {
        Assert.Equal(2.4, SettingsForm.FinalDriveRatio("30", "72", "")!.Value, 6);
    }

    [Theory]
    [InlineData("", "93", "2.6")]
    [InlineData("21", "", "2.6")]
    [InlineData("0", "93", "2.6")]
    [InlineData("21", "93", "abc")]
    public void Fdr_is_blank_until_inputs_are_valid(string pinion, string spur, string ratio)
    {
        Assert.Null(SettingsForm.FinalDriveRatio(pinion, spur, ratio));
    }
}
