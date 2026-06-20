namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Input to the project saver: the manifest about to be written + the list of
    /// source files that need to be embedded byte-for-byte. <see cref="ProjectSnapshotFile.SourcePath"/>
    /// is a local-disk path; <see cref="ProjectSnapshotFile.ArchivePath"/> is the safe in-zip
    /// path that already appears in the manifest (under <c>Runs[i].SourcePath</c> or
    /// <c>Runs[i].TunePath</c>).
    /// </summary>
    public sealed class ProjectSnapshot
    {
        public ProjectManifest Manifest { get; init; } = new();
        public IReadOnlyList<ProjectSnapshotFile> Files { get; init; } = Array.Empty<ProjectSnapshotFile>();
    }

    public sealed record ProjectSnapshotFile(string SourcePath, string ArchivePath);
}
