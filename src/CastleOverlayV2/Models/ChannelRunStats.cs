namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Aggregate stats for one channel of one loaded run, surfaced in the channel drawer's stat cards.
    /// </summary>
    public sealed record ChannelRunStats(
        int Slot,
        string DisplayLabel,
        double Max,
        double Avg);
}
