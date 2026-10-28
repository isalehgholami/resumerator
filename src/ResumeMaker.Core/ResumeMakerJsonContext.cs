using System.Text.Json.Serialization;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(List<Profile>))]
[JsonSerializable(typeof(List<ProfileSummary>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(TemplateMetadata))]
[JsonSerializable(typeof(Template))]
[JsonSerializable(typeof(List<Template>))]
[JsonSerializable(typeof(TemplateLeadOptions))]
[JsonSerializable(typeof(CoverLetterOptions))]
[JsonSerializable(typeof(CoverLetter))]
[JsonSerializable(typeof(ExportRequest))]
[JsonSerializable(typeof(ValidationResult))]
public sealed partial class ResumeMakerJsonContext : JsonSerializerContext;
