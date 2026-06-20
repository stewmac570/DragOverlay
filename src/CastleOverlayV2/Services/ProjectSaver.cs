using CastleOverlayV2.Models;
using System.IO.Compression;
using System.Text;

namespace CastleOverlayV2.Services
{
    /// <summary>
    /// Writes a complete analysis session as a portable <c>.dragoverlay</c> ZIP package
    /// (per <c>Docs/DragOverlay_UI_Spec.md</c> + issue #86).
    ///
    /// Atomicity: the ZIP is written to a temp file alongside the destination and only
    /// renamed/replaced after every entry has been written and the archive closed.
    /// Mid-write failure deletes the temp file and surfaces an error via
    /// <see cref="LoadResult{T}"/> — the saver does not show UI dialogs itself
    /// (the caller is responsible for that, per issue requirements).
    /// </summary>
    public sealed class ProjectSaver
    {
        private readonly ProjectManifestJson _manifestJson;

        public ProjectSaver(ProjectManifestJson? manifestJson = null)
        {
            _manifestJson = manifestJson ?? new ProjectManifestJson();
        }

        public LoadResult<string> Save(string destinationPath, ProjectSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                return LoadResult<string>.Error("Save Failed", "Destination path is required.");
            if (snapshot is null)
                return LoadResult<string>.Error("Save Failed", "Project snapshot is required.");

            // Reject duplicate archive paths up front — the validator already rejects unsafe
            // paths embedded in the manifest, but the file list is separate.
            var seenArchivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in snapshot.Files)
            {
                if (string.IsNullOrWhiteSpace(file.ArchivePath))
                    return LoadResult<string>.Error("Save Failed", "Project file list contains an entry with no archive path.");
                if (!ProjectManifestValidator.IsSafePackagePath(file.ArchivePath))
                    return LoadResult<string>.Error("Save Failed", $"Archive path '{file.ArchivePath}' is not safe.");
                if (!seenArchivePaths.Add(file.ArchivePath))
                    return LoadResult<string>.Error("Save Failed", $"Archive path '{file.ArchivePath}' is duplicated in the project.");
                if (string.IsNullOrWhiteSpace(file.SourcePath))
                    return LoadResult<string>.Error("Save Failed", $"Missing source file for archive entry '{file.ArchivePath}'.");
                if (!File.Exists(file.SourcePath))
                    return LoadResult<string>.Error("Save Failed", $"Source file for '{file.ArchivePath}' no longer exists on disk:\n{file.SourcePath}");
            }

            string destinationDir = Path.GetDirectoryName(destinationPath) ?? "";
            if (!string.IsNullOrEmpty(destinationDir))
                Directory.CreateDirectory(destinationDir);

            // Temp file in the same directory so File.Replace/Move stays atomic.
            string tempPath = destinationPath + ".saving-" + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                string manifestJson;
                try
                {
                    manifestJson = _manifestJson.Serialize(snapshot.Manifest);
                }
                catch (ProjectManifestException ex)
                {
                    return LoadResult<string>.Error("Save Failed", "Project manifest is invalid:\n" + ex.Message);
                }

                using (var zipStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: Encoding.UTF8))
                {
                    var manifestEntry = archive.CreateEntry(ProjectFormat.ManifestFileName, CompressionLevel.Optimal);
                    using (var entryStream = manifestEntry.Open())
                    using (var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                    {
                        writer.Write(manifestJson);
                    }

                    foreach (var file in snapshot.Files)
                    {
                        var entry = archive.CreateEntry(file.ArchivePath, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        using var source = File.OpenRead(file.SourcePath);
                        source.CopyTo(entryStream);
                    }
                }

                // Replace the existing file atomically, or rename the temp file into place.
                if (File.Exists(destinationPath))
                    File.Replace(tempPath, destinationPath, destinationBackupFileName: null);
                else
                    File.Move(tempPath, destinationPath);

                return LoadResult<string>.Success(destinationPath);
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(tempPath);
                return LoadResult<string>.Error("Save Failed",
                    "An error occurred while saving the project:\n" + ex.Message);
            }
        }

        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup; do not throw from the catch block.
            }
        }
    }
}
