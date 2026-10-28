using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Application.Dtos;

public sealed record TemplateRenderRequest(Profile Profile, string? TemplateId = null);

public sealed record ExportRequest(Profile Profile, ExportFormat Format, string FileName = "resume", int Dpi = 300);

public sealed record ExportResult(byte[] Content, string ContentType, string FileName);

public sealed record ValidationIssue(string Code, string Message, bool IsError);

public sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => !Issues.Any(i => i.IsError);
    public static ValidationResult Ok { get; } = new([]);
}

public sealed record TemplateLeadOptions
{
    public string Style { get; init; } = "minimal";
    public string PrimaryColor { get; init; } = "#1a73e8";
    public string AccentColor { get; init; } = "#ff6b6b";
    public int ColumnCount { get; init; } = 1;
    /// <summary>"none" | "left" | "right"</summary>
    public string SidebarPosition { get; init; } = "none";
    public string Language { get; init; } = "fa";
    public List<string> SectionPriority { get; init; } = [];
}

public sealed record CoverLetterOptions
{
    public string Company { get; init; } = "";
    public string Position { get; init; } = "";
    public List<string> Keywords { get; init; } = [];
    /// <summary>"formal" | "friendly" | "bold"</summary>
    public string Tone { get; init; } = "formal";
    public string Language { get; init; } = "fa";
}

public sealed record CoverLetter
{
    public Guid ProfileId { get; init; }
    public string Company { get; init; } = "";
    public string Position { get; init; } = "";
    public string Language { get; init; } = "fa";
    public string Body { get; init; } = "";
}
