using CastleOverlayV2.Models;
using System.IO.Compression;
using System.Text;

namespace CastleOverlayV2.Services
{
    /// <summary>
    /// Opens a <c>.dragoverlay</c> package and parses every required source file via the
    /// existing Castle / RaceBox / Tune loaders (no parser duplication).
    ///
    /// Behaviour:
    ///   - Validates ZIP layout + manifest before extracting anything.
    ///   - Extracts into an application-owned temp directory under <c>%LOCALAPPDATA%/DragOverlay/projects/&lt;guid&gt;</c>.
    ///   - On any required-run failure, removes the temp directory and returns an error
    ///     with the original session left untouched (the presenter swaps state in only
    ///     once a <see cref="RestoredProject"/> has been returned).
    ///   - Missing optional tune: tune is omitted and a warning is added to <see cref="RestoredProject.Warnings"/>.
    /// </summary>
    public sealed class ProjectLoader
    {
        private readonly ConfigService _configService;
        private readonly ProjectManifestJson _manifestJson;

        public ProjectLoader(ConfigService configService, ProjectManifestJson? manifestJson = null)
        {
            _configService = configService;
            _manifestJson = manifestJson ?? new ProjectManifestJson();
        }

        public LoadResult<RestoredProject> Load(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                return LoadResult<RestoredProject>.Error("Open Project", "No project file was selected.");
            if (!File.Exists(packagePath))
                return LoadResult<RestoredProject>.Error("Open Project",
                    "The project file does not exist:\n" + packagePath);

            // -------- Step 1: open ZIP + validate layout + manifest. -----------------
            ProjectManifest manifest;
            var warnings = new List<string>();
            ZipArchive archive;
            try
            {
                archive = ZipFile.OpenRead(packagePath);
            }
            catch (Exception ex)
            {
                return LoadResult<RestoredProject>.Error("Open Project",
                    "This file is not a valid .dragoverlay package:\n" + ex.Message);
            }

            string tempDir = "";

            try
            {
                // Safety: reject unsafe entry paths up front before any extraction.
                foreach (var entry in archive.Entries)
                {
                    if (!ProjectManifestValidator.IsSafePackagePath(entry.FullName))
                        return LoadResult<RestoredProject>.Error("Open Project",
                            $"The package contains an unsafe entry path: {entry.FullName}");
                }

                var manifestEntry = archive.GetEntry(ProjectFormat.ManifestFileName);
                if (manifestEntry == null)
                    return LoadResult<RestoredProject>.Error("Open Project",
                        "This file is not a valid .dragoverlay package (missing project.json).");

                string manifestJson;
                using (var ms = new MemoryStream())
                {
                    using (var s = manifestEntry.Open()) s.CopyTo(ms);
                    manifestJson = Encoding.UTF8.GetString(ms.ToArray());
                }

                var parseResult = _manifestJson.Deserialize(manifestJson);
                if (!parseResult.IsSuccess)
                {
                    string title = parseResult.Status == ProjectManifestLoadStatus.UnsupportedVersion
                        ? "Unsupported Project Version"
                        : "Open Project";
                    return LoadResult<RestoredProject>.Error(title,
                        string.Join(Environment.NewLine, parseResult.Errors));
                }
                manifest = parseResult.Manifest!;

                // Manifest must declare at least one run.
                if (manifest.Runs.Count == 0)
                    return LoadResult<RestoredProject>.Error("Open Project",
                        "This project does not contain any runs.");

                // Validate each declared run's archive entry exists.
                foreach (var entry in manifest.Runs)
                {
                    if (archive.GetEntry(entry.SourcePath) == null)
                        return LoadResult<RestoredProject>.Error("Open Project",
                            $"The project is missing its declared log file: {entry.SourcePath}");
                }

                // -------- Step 2: extract into an app-owned temp dir. ---------------
                tempDir = CreateTempDir();
                foreach (var entry in archive.Entries)
                    ExtractEntryFlat(entry, tempDir);
            }
            catch (Exception ex)
            {
                TryDeleteTempDir(tempDir);
                return LoadResult<RestoredProject>.Error("Open Project",
                    "An error occurred while validating the project:\n" + ex.Message);
            }
            finally
            {
                archive.Dispose();
            }

            // -------- Step 3: parse every required run via existing loaders. --------
            var loaded = new Dictionary<int, RunData>();
            try
            {
                var castleLoader = new CsvLoader(_configService);
                var raceBoxLoader = new RaceBoxLoader();
                var tuneLoader = new CastleTuneLoader();

                bool trimForDrag = manifest.RunMode != ProjectRunMode.Speed;

                foreach (var entry in manifest.Runs.OrderBy(r => r.PlotSlot))
                {
                    string localPath = Path.Combine(tempDir, entry.SourcePath.Replace('/', Path.DirectorySeparatorChar));

                    RunData? run = null;
                    if (entry.SourceType == ProjectSourceType.Castle)
                    {
                        var loadResult = castleLoader.Load(localPath, trimForDrag);
                        if (!loadResult.Ok)
                        {
                            TryDeleteTempDir(tempDir);
                            return LoadResult<RestoredProject>.Error("Open Project",
                                $"Failed to parse Castle log for Run {entry.UiSlot}:\n{loadResult.Message}");
                        }
                        run = loadResult.Value!;
                    }
                    else
                    {
                        var headerResult = RaceBoxLoader.LoadHeaderOnly(localPath);
                        if (!headerResult.Ok)
                        {
                            TryDeleteTempDir(tempDir);
                            return LoadResult<RestoredProject>.Error("Open Project",
                                $"Failed to parse RaceBox header for Run {entry.UiSlot}:\n{headerResult.Message}");
                        }
                        var rbHeader = headerResult.Value!;
                        int runIndex = rbHeader.FirstCompleteRunIndex ?? 1;
                        var telemetryResult = raceBoxLoader.LoadTelemetry(localPath, runIndex);
                        if (!telemetryResult.Ok)
                        {
                            TryDeleteTempDir(tempDir);
                            return LoadResult<RestoredProject>.Error("Open Project",
                                $"Failed to parse RaceBox telemetry for RaceBox {entry.UiSlot}:\n{telemetryResult.Message}");
                        }
                        var points = telemetryResult.Value!;
                        run = new RunData
                        {
                            IsRaceBox = true,
                            SplitTimes = rbHeader.SplitTimes,
                            SplitLabels = rbHeader.SplitLabels,
                            FileName = string.IsNullOrEmpty(entry.DisplayFileName) ? Path.GetFileName(localPath) : entry.DisplayFileName,
                        };
                        run.Data["RaceBox Speed"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.SpeedMph }).ToList();
                        run.Data["RaceBox G-Force X"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.GForceX }).ToList();
                        run.Data["RaceBox Distance"] = IntegrateDistanceFeet(points);
                        run.DataPoints = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds }).ToList();
                    }

                    // Common fields from the manifest.
                    run.FileName = string.IsNullOrEmpty(entry.DisplayFileName)
                        ? Path.GetFileName(localPath)
                        : entry.DisplayFileName;
                    run.SourcePath = localPath;
                    run.TimeShiftMs = entry.TimeShiftMs;

                    // Optional tune (Castle only). Missing tune = warning, not failure.
                    if (entry.SourceType == ProjectSourceType.Castle && !string.IsNullOrWhiteSpace(entry.TunePath))
                    {
                        string tuneLocalPath = Path.Combine(tempDir, entry.TunePath!.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(tuneLocalPath))
                        {
                            warnings.Add($"Tune file missing for Run {entry.UiSlot}; opened without it.");
                        }
                        else
                        {
                            var tuneResult = tuneLoader.Load(tuneLocalPath);
                            if (!tuneResult.Ok)
                                warnings.Add($"Tune file for Run {entry.UiSlot} could not be parsed; opened without it: {tuneResult.Message}");
                            else
                            {
                                tuneResult.Value!.SourcePath = tuneLocalPath;
                                if (entry.RadioSettings != null)
                                    tuneResult.Value.Radio = entry.RadioSettings;
                                run.Tune = tuneResult.Value;
                            }
                        }
                    }
                    else if (entry.SourceType == ProjectSourceType.Castle && entry.RadioSettings != null)
                    {
                        // Radio-only (no tune file): still preserve the settings.
                        run.Tune = new TuneSettings { Radio = entry.RadioSettings };
                    }

                    loaded[entry.PlotSlot] = run;
                }
            }
            catch (Exception ex)
            {
                TryDeleteTempDir(tempDir);
                return LoadResult<RestoredProject>.Error("Open Project",
                    "An error occurred while restoring the project:\n" + ex.Message);
            }

            return LoadResult<RestoredProject>.Success(new RestoredProject
            {
                Manifest = manifest,
                RunsBySlot = loaded,
                TempDir = tempDir,
                Warnings = warnings
            });
        }

        public static void TryDeleteTempDir(string tempDir)
        {
            if (string.IsNullOrEmpty(tempDir)) return;
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }

        // ---- Helpers --------------------------------------------------------

        private static string CreateTempDir()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DragOverlay", "projects", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void ExtractEntryFlat(ZipArchiveEntry entry, string baseDir)
        {
            // Manifest is at the root; logs/ and tunes/ keep their forward-slash paths.
            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            string outPath = Path.Combine(baseDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? baseDir);
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                return; // directory entry, just ensure it exists
            using var entryStream = entry.Open();
            using var fileStream = File.Create(outPath);
            entryStream.CopyTo(fileStream);
        }

        private static List<DataPoint> IntegrateDistanceFeet(List<RaceBoxPoint> points)
        {
            // Same rectangular integration as MainFormPresenter (Phase 5 / #76).
            const double FeetPerMile = 5280.0;
            const double SecondsPerHour = 3600.0;
            const double MphToFeetPerSec = FeetPerMile / SecondsPerHour;

            var result = new List<DataPoint>(points.Count);
            double distFt = 0;
            double prevSec = points.Count > 0 ? points[0].Time.TotalSeconds : 0;
            for (int i = 0; i < points.Count; i++)
            {
                double sec = points[i].Time.TotalSeconds;
                if (i > 0)
                {
                    double dt = sec - prevSec;
                    if (dt > 0)
                        distFt += Math.Max(0, points[i].SpeedMph) * MphToFeetPerSec * dt;
                }
                result.Add(new DataPoint { Time = sec, Y = distFt });
                prevSec = sec;
            }
            return result;
        }
    }
}
