using System.Text.Json.Serialization;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Web;

public sealed record CreateProfileBody(string? Name, string? Language, Profile? Profile);

public sealed record RenderBody(Profile Profile, string? TemplateId);

public sealed record ExportBody(Profile Profile, string? FileName, int? Dpi);

public sealed record CustomTemplateBody(string? Name, string? Html, string? SourceLead);

public sealed record IssueBody(string Code, string Message, bool IsError);

public sealed record ErrorBody(string Message, IReadOnlyList<IssueBody>? Issues = null);

public sealed record PromptBody(string Prompt);

public sealed record HealthBody(string Status, string Version);

public sealed record ShortcutBody(string Name, string Url);

public sealed record CoverLetterLeadBody(Profile Profile, CoverLetterOptions? Options);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CreateProfileBody))]
[JsonSerializable(typeof(RenderBody))]
[JsonSerializable(typeof(ExportBody))]
[JsonSerializable(typeof(CustomTemplateBody))]
[JsonSerializable(typeof(ErrorBody))]
[JsonSerializable(typeof(PromptBody))]
[JsonSerializable(typeof(HealthBody))]
[JsonSerializable(typeof(List<ShortcutBody>))]
[JsonSerializable(typeof(CoverLetterLeadBody))]
internal sealed partial class WebJsonContext : JsonSerializerContext;
