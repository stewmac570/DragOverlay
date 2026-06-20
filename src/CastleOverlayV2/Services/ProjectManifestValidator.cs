using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services
{
    public sealed class ProjectManifestValidator
    {
        public ProjectManifestValidationResult Validate(ProjectManifest? manifest)
        {
            var errors = new List<string>();
            if (manifest == null)
            {
                errors.Add("The project manifest is missing.");
                return new ProjectManifestValidationResult(errors);
            }

            if (manifest.SchemaVersion > ProjectFormat.CurrentSchemaVersion)
            {
                errors.Add(
                    $"Project schema version {manifest.SchemaVersion} is newer than the supported version " +
                    $"{ProjectFormat.CurrentSchemaVersion}.");
                return new ProjectManifestValidationResult(errors, isUnsupportedVersion: true);
            }

            if (manifest.SchemaVersion < 1)
                errors.Add("SchemaVersion must be 1 or greater.");
            if (string.IsNullOrWhiteSpace(manifest.AppBuild))
                errors.Add("AppBuild is required.");
            if (manifest.CreatedUtc == default)
                errors.Add("CreatedUtc is required.");
            if (!Enum.IsDefined(manifest.RunMode))
                errors.Add($"RunMode '{manifest.RunMode}' is not supported.");
            if (manifest.ChannelVisibility == null)
                errors.Add("ChannelVisibility is required.");
            if (manifest.Runs == null)
            {
                errors.Add("Runs is required.");
                return new ProjectManifestValidationResult(errors);
            }

            var plotSlots = new HashSet<int>();
            var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < manifest.Runs.Count; index++)
            {
                ProjectRunEntry? run = manifest.Runs[index];
                string prefix = $"Runs[{index}]";
                if (run == null)
                {
                    errors.Add($"{prefix} is missing.");
                    continue;
                }

                ValidateRun(run, prefix, errors);
                if (!plotSlots.Add(run.PlotSlot))
                    errors.Add($"{prefix}.PlotSlot duplicates plot slot {run.PlotSlot}.");
                if (!string.IsNullOrWhiteSpace(run.SourcePath) && !sourcePaths.Add(run.SourcePath))
                    errors.Add($"{prefix}.SourcePath duplicates '{run.SourcePath}'.");
            }

            return new ProjectManifestValidationResult(errors);
        }

        public static bool IsSafePackagePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                Path.IsPathRooted(path) ||
                path.StartsWith('/') ||
                path.Contains('\\') ||
                path.Contains(':'))
            {
                return false;
            }

            string[] segments = path.Split('/');
            return segments.All(segment =>
                !string.IsNullOrWhiteSpace(segment) &&
                segment != "." &&
                segment != "..");
        }

        private static void ValidateRun(
            ProjectRunEntry run,
            string prefix,
            ICollection<string> errors)
        {
            if (!Enum.IsDefined(run.SourceType))
                errors.Add($"{prefix}.SourceType is not supported.");
            if (run.UiSlot is < 1 or > 3)
                errors.Add($"{prefix}.UiSlot must be between 1 and 3.");

            int expectedPlotSlot = run.SourceType == ProjectSourceType.RaceBox
                ? run.UiSlot + 3
                : run.UiSlot;
            if (run.PlotSlot != expectedPlotSlot)
            {
                errors.Add(
                    $"{prefix}.PlotSlot must be {expectedPlotSlot} for {run.SourceType} UI slot {run.UiSlot}.");
            }

            if (string.IsNullOrWhiteSpace(run.DisplayFileName))
                errors.Add($"{prefix}.DisplayFileName is required.");
            if (!IsSafePackagePath(run.SourcePath))
                errors.Add($"{prefix}.SourcePath must be a safe package-relative path using forward slashes.");
            if (!string.IsNullOrWhiteSpace(run.TunePath) && !IsSafePackagePath(run.TunePath))
                errors.Add($"{prefix}.TunePath must be a safe package-relative path using forward slashes.");
            if (run.SourceType == ProjectSourceType.RaceBox && !string.IsNullOrWhiteSpace(run.TunePath))
                errors.Add($"{prefix}.TunePath is only valid for Castle runs.");
            if (double.IsNaN(run.TimeShiftMs) || double.IsInfinity(run.TimeShiftMs))
                errors.Add($"{prefix}.TimeShiftMs must be a finite number.");

            ValidateRadio(run.RadioSettings, $"{prefix}.RadioSettings", errors);
        }

        private static void ValidateRadio(
            RadioTuneSettings? radio,
            string prefix,
            ICollection<string> errors)
        {
            if (radio == null)
                return;

            if (radio.Mode is < 1 or > 3)
                errors.Add($"{prefix}.Mode must be between 1 and 3.");

            foreach ((string name, double? value) in GetRadioValues(radio))
            {
                if (value.HasValue &&
                    (double.IsNaN(value.Value) ||
                     double.IsInfinity(value.Value) ||
                     value.Value is < 1 or > 100))
                {
                    errors.Add($"{prefix}.{name} must be between 1 and 100.");
                }
            }
        }

        private static IEnumerable<(string Name, double? Value)> GetRadioValues(RadioTuneSettings radio)
        {
            yield return (nameof(radio.AllTurnPercent), radio.AllTurnPercent);
            yield return (nameof(radio.AllReturnPercent), radio.AllReturnPercent);
            yield return (nameof(radio.HighTurnPercent), radio.HighTurnPercent);
            yield return (nameof(radio.HighReturnPercent), radio.HighReturnPercent);
            yield return (nameof(radio.MiddleTurnPercent), radio.MiddleTurnPercent);
            yield return (nameof(radio.MiddleReturnPercent), radio.MiddleReturnPercent);
            yield return (nameof(radio.LowTurnPercent), radio.LowTurnPercent);
            yield return (nameof(radio.LowReturnPercent), radio.LowReturnPercent);
            yield return (nameof(radio.Point1Percent), radio.Point1Percent);
            yield return (nameof(radio.Point2Percent), radio.Point2Percent);
        }
    }

    public sealed class ProjectManifestValidationResult
    {
        public ProjectManifestValidationResult(
            IReadOnlyList<string> errors,
            bool isUnsupportedVersion = false)
        {
            Errors = errors;
            IsUnsupportedVersion = isUnsupportedVersion;
        }

        public bool IsValid => Errors.Count == 0;
        public bool IsUnsupportedVersion { get; }
        public IReadOnlyList<string> Errors { get; }
    }
}
