using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResumeMaker.Core.Domain;

/// <summary>Serializes enums as snake_case lowercase strings (e.g. "experience"), AOT-safe.</summary>
public sealed class SectionTypeConverter() : JsonStringEnumConverter<SectionType>(JsonNamingPolicy.SnakeCaseLower);

public sealed class TemplateStyleConverter() : JsonStringEnumConverter<TemplateStyle>(JsonNamingPolicy.SnakeCaseLower);

public sealed class ExportFormatConverter() : JsonStringEnumConverter<ExportFormat>(JsonNamingPolicy.SnakeCaseLower);
