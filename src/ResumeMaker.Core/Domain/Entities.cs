using System.Text.Json.Serialization;

namespace ResumeMaker.Core.Domain;

[JsonConverter(typeof(SectionTypeConverter))]
public enum SectionType { Experience, Education, Skills, Languages, Projects, Awards, Interests, Custom }

[JsonConverter(typeof(TemplateStyleConverter))]
public enum TemplateStyle { Minimal, Classic, Modern, Compact, Sidebar, Ats, Custom }

[JsonConverter(typeof(ExportFormatConverter))]
public enum ExportFormat { Pdf, Png, Html, Json }

public sealed record ContactInfo
{
    public string FullName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Location { get; init; } = "";
    /// <summary>Keys such as linkedin, github, website.</summary>
    public Dictionary<string, string> Links { get; init; } = [];
}

public sealed record ThemeOverrides
{
    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public string? FontFamily { get; init; }
}

public sealed record SkillEntry
{
    public string Name { get; init; } = "";
    public string Level { get; init; } = "";
}

/// <summary>Flat item shared by all section types; each type uses the subset of fields it needs.</summary>
public sealed record SectionItem
{
    public string? Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public string? Start { get; init; }
    public string? End { get; init; }
    public string? Description { get; init; }
    public List<string>? Tags { get; init; }
    public string? Degree { get; init; }
    public string? Institution { get; init; }
    public string? Category { get; init; }
    public List<SkillEntry>? Entries { get; init; }
    public string? Name { get; init; }
    public string? Level { get; init; }
    public string? Url { get; init; }
    public string? Issuer { get; init; }
    public string? Date { get; init; }
}

public sealed record Section
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public SectionType Type { get; init; } = SectionType.Custom;
    public string Title { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public int Order { get; init; }
    public List<SectionItem> Items { get; init; } = [];
}

public sealed record Profile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    /// <summary>Resume language: "fa" or "en".</summary>
    public string Language { get; init; } = "fa";
    public string TemplateId { get; init; } = "minimal";
    public bool AtsMode { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ContactInfo Contact { get; init; } = new();
    public string Summary { get; init; } = "";
    public List<Section> Sections { get; init; } = [];
    public ThemeOverrides Theme { get; init; } = new();

    [JsonIgnore]
    public string Direction => Language == "fa" ? "rtl" : "ltr";
}

public sealed record ProfileSummary(Guid Id, string Name, string Language, string TemplateId, DateTimeOffset UpdatedAt);

public sealed record Template
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public TemplateStyle Style { get; init; } = TemplateStyle.Custom;
    public bool IsBuiltIn { get; init; }
    public string HtmlContent { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>Lead prompt that produced a custom template (FR-35 debug).</summary>
    public string? SourceLead { get; init; }
}

public sealed record TemplateMetadata
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? SourceLead { get; init; }
}

public sealed record AppSettings
{
    /// <summary>"system" | "light" | "dark"</summary>
    public string Theme { get; init; } = "system";
    public string UiLanguage { get; init; } = "fa";
    public string DefaultResumeLanguage { get; init; } = "fa";
    public string? ChromePath { get; init; }
    public int BackupIntervalMinutes { get; init; } = 5;
    public int BackupMaxCount { get; init; } = 10;
}
