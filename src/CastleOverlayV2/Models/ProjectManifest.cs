namespace CastleOverlayV2.Models
{
    public sealed class ProjectManifest
    {
        public int SchemaVersion { get; set; } = ProjectFormat.CurrentSchemaVersion;
        public string AppBuild { get; set; } = "";
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public ProjectRunMode RunMode { get; set; } = ProjectRunMode.Drag;
        public Dictionary<string, bool> ChannelVisibility { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<ProjectRunEntry> Runs { get; set; } = new();
    }

    public sealed class ProjectRunEntry
    {
        public ProjectSourceType SourceType { get; set; }
        public int UiSlot { get; set; }
        public int PlotSlot { get; set; }
        public string DisplayFileName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public bool IsVisible { get; set; } = true;
        public double TimeShiftMs { get; set; }
        public string? TunePath { get; set; }
        public RadioTuneSettings? RadioSettings { get; set; }
    }

    public enum ProjectRunMode
    {
        Drag,
        Speed
    }

    public enum ProjectSourceType
    {
        Castle,
        RaceBox
    }
}
