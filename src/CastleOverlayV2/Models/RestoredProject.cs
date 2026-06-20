namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Output of <see cref="Services.ProjectLoader"/>: the manifest, the parsed runs
    /// (already keyed by plot slot, with <see cref="RunData.SourcePath"/> pointing into
    /// <see cref="TempDir"/>), and the application-owned temp directory the package was
    /// extracted into. The caller is responsible for cleaning up <see cref="TempDir"/>
    /// when the restored session is replaced or closed.
    /// </summary>
    public sealed class RestoredProject
    {
        public ProjectManifest Manifest { get; init; } = new();
        public IReadOnlyDictionary<int, RunData> RunsBySlot { get; init; } =
            new Dictionary<int, RunData>();
        public string TempDir { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }
}
