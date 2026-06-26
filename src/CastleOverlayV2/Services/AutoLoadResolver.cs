using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CastleOverlayV2.Services
{
    /// <summary>
    /// Resolves which file to pull from a log folder for the Auto Load feature.
    /// UI-free and side-effect-free so it stays unit-testable (services must not call
    /// MessageBox — see CLAUDE.md). All selection is by <see cref="FileInfo.LastWriteTimeUtc"/>.
    /// </summary>
    public static class AutoLoadResolver
    {
        /// <summary>
        /// The newest file matching <paramref name="searchPattern"/> in <paramref name="folder"/>,
        /// or null when the folder is missing/blank or contains no match.
        /// </summary>
        public static FileInfo? Newest(string? folder, string searchPattern)
        {
            return Enumerate(folder, searchPattern)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
        }

        /// <summary>
        /// The file matching <paramref name="searchPattern"/> whose write-time is closest to
        /// <paramref name="anchorUtc"/> (ties broken toward the newer file), or null when the
        /// folder is missing/blank or contains no match. Used to group the tune + RaceBox logs
        /// saved around the same time as the anchoring Castle log.
        /// <para>
        /// When <paramref name="within"/> is set, a match is only returned if it is within that
        /// window of the anchor — so a stray file from a different session isn't pulled in.
        /// </para>
        /// </summary>
        public static FileInfo? Nearest(string? folder, string searchPattern, DateTime anchorUtc, TimeSpan? within = null)
        {
            var best = Enumerate(folder, searchPattern)
                .OrderBy(f => Math.Abs((f.LastWriteTimeUtc - anchorUtc).Ticks))
                .ThenByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (best != null && within is TimeSpan window &&
                (best.LastWriteTimeUtc - anchorUtc).Duration() > window)
                return null;

            return best;
        }

        private static IEnumerable<FileInfo> Enumerate(string? folder, string searchPattern)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return Enumerable.Empty<FileInfo>();

            try
            {
                return new DirectoryInfo(folder)
                    .EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly)
                    .ToList();
            }
            catch (Exception)
            {
                // Unreadable folder (permissions, transient IO) — treat as no match.
                return Enumerable.Empty<FileInfo>();
            }
        }
    }
}
