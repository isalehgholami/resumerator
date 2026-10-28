using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Application.Interfaces;

public interface IProfileReader
{
    Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken ct = default);
    Task<Profile?> GetAsync(Guid id, CancellationToken ct = default);
}

public interface IProfileWriter
{
    Task SaveAsync(Profile profile, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITemplateRepository
{
    /// <summary>Built-in templates followed by custom ones.</summary>
    Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct = default);
    Task<Template?> GetByIdAsync(string id, CancellationToken ct = default);
    Task SaveCustomAsync(Template template, CancellationToken ct = default);
    Task<bool> DeleteCustomAsync(string id, CancellationToken ct = default);
}

/// <summary>Renders a Scriban template string against a profile.</summary>
public interface ITemplateEngine
{
    Task<string> RenderAsync(string templateHtml, Profile profile, CancellationToken ct = default);
    Task<string> RenderCoverLetterAsync(string templateHtml, Profile profile, CoverLetter letter, CancellationToken ct = default);
}

public interface ITemplateValidator
{
    ValidationResult Validate(string html);
    /// <summary>Removes script tags, external resources and event handler attributes.</summary>
    string Sanitize(string html);
}

public interface IFontResourceProvider
{
    /// <summary>Base64 woff2 for the given resume language ("fa" -> Vazirmatn, otherwise Inter).</summary>
    Task<string> GetFontBase64Async(string language, bool bold = false, CancellationToken ct = default);
}

public interface IPdfExporter
{
    Task<ExportResult> ExportPdfAsync(string html, string fileName, CancellationToken ct = default);
}

public interface IPngExporter
{
    Task<ExportResult> ExportPngAsync(string html, string fileName, int dpi, CancellationToken ct = default);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}

public interface IBackupService
{
    /// <summary>Writes a snapshot if profile changed since the last one; prunes old snapshots.</summary>
    Task<string?> SnapshotAsync(Profile profile, CancellationToken ct = default);
}

public class TemplateNotFoundException(string id) : Exception($"Template '{id}' was not found.");
public class ChromeNotFoundException() : Exception("Chrome/Edge/Chromium was not found. Install one or set its path in settings.");
public class ExportFailedException(string message) : Exception(message);
