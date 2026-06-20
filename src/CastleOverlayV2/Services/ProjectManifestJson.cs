using CastleOverlayV2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace CastleOverlayV2.Services
{
    public sealed class ProjectManifestJson
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() }
        };

        private readonly ProjectManifestValidator _validator = new();

        public string Serialize(ProjectManifest manifest)
        {
            ProjectManifestValidationResult validation = _validator.Validate(manifest);
            if (!validation.IsValid)
                throw new ProjectManifestException(validation.Errors);

            return JsonConvert.SerializeObject(manifest, Settings);
        }

        public ProjectManifestLoadResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return ProjectManifestLoadResult.Invalid("The project manifest is empty.");

            try
            {
                ProjectManifest? manifest = JsonConvert.DeserializeObject<ProjectManifest>(json, Settings);
                ProjectManifestValidationResult validation = _validator.Validate(manifest);
                if (!validation.IsValid)
                {
                    return validation.IsUnsupportedVersion
                        ? ProjectManifestLoadResult.Unsupported(validation.Errors)
                        : ProjectManifestLoadResult.Invalid(validation.Errors);
                }

                return ProjectManifestLoadResult.Success(manifest!);
            }
            catch (JsonException ex)
            {
                return ProjectManifestLoadResult.Invalid($"The project manifest JSON is invalid: {ex.Message}");
            }
        }
    }

    public sealed class ProjectManifestLoadResult
    {
        private ProjectManifestLoadResult(
            ProjectManifest? manifest,
            ProjectManifestLoadStatus status,
            IReadOnlyList<string> errors)
        {
            Manifest = manifest;
            Status = status;
            Errors = errors;
        }

        public ProjectManifest? Manifest { get; }
        public ProjectManifestLoadStatus Status { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsSuccess => Status == ProjectManifestLoadStatus.Success;

        public static ProjectManifestLoadResult Success(ProjectManifest manifest) =>
            new(manifest, ProjectManifestLoadStatus.Success, Array.Empty<string>());

        public static ProjectManifestLoadResult Invalid(params string[] errors) =>
            Invalid((IReadOnlyList<string>)errors);

        public static ProjectManifestLoadResult Invalid(IReadOnlyList<string> errors) =>
            new(null, ProjectManifestLoadStatus.Invalid, errors);

        public static ProjectManifestLoadResult Unsupported(IReadOnlyList<string> errors) =>
            new(null, ProjectManifestLoadStatus.UnsupportedVersion, errors);
    }

    public enum ProjectManifestLoadStatus
    {
        Success,
        Invalid,
        UnsupportedVersion
    }

    public sealed class ProjectManifestException : Exception
    {
        public ProjectManifestException(IReadOnlyList<string> errors)
            : base(string.Join(Environment.NewLine, errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }
}
