using System.Text.Json;
using System.Text.RegularExpressions;
using ResumeMaker.Core;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure.Storage;

namespace ResumeMaker.Infrastructure.Templating;

/// <summary>Built-in templates (embedded) plus custom ones in {data}/templates/custom.</summary>
public sealed partial class TemplateRepository(AppPaths paths) : ITemplateRepository
{
    public const string CoverLetterId = "cover-letter";

    private static readonly (string Id, string Name, TemplateStyle Style)[] BuiltIns =
    [
        ("minimal", "Minimal", TemplateStyle.Minimal),
        ("classic", "Classic", TemplateStyle.Classic),
        ("modern", "Modern", TemplateStyle.Modern),
        ("compact", "Compact", TemplateStyle.Compact),
        ("sidebar", "Sidebar", TemplateStyle.Sidebar),
        ("ats", "ATS", TemplateStyle.Ats),
    ];

    public static bool IsBuiltInId(string id) =>
        id == CoverLetterId || BuiltIns.Any(b => b.Id == id);

    public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && SlugRegex().IsMatch(id);

    public Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct = default)
    {
        var result = new List<Template>();
        foreach (var (id, _, _) in BuiltIns)
        {
            var t = LoadBuiltIn(id);
            if (t is not null)
                result.Add(t);
        }
        result.AddRange(LoadCustomTemplates().OrderBy(t => t.CreatedAt).ThenBy(t => t.Id, StringComparer.Ordinal));
        return Task.FromResult<IReadOnlyList<Template>>(result);
    }

    public Task<Template?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(id))
            return Task.FromResult<Template?>(null);
        if (IsBuiltInId(id))
            return Task.FromResult(LoadBuiltIn(id));
        return Task.FromResult(IsValidId(id) ? LoadCustom(id) : null);
    }

    public async Task SaveCustomAsync(Template template, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (IsBuiltInId(template.Id))
            throw new InvalidOperationException($"'{template.Id}' is a built-in template and cannot be overwritten.");
        if (!IsValidId(template.Id))
            throw new ArgumentException("Template id must be a lowercase slug (a-z, 0-9, '-' or '_').", nameof(template));

        var meta = new TemplateMetadata
        {
            Id = template.Id,
            Name = string.IsNullOrWhiteSpace(template.Name) ? template.Id : template.Name,
            CreatedAt = template.CreatedAt,
            SourceLead = template.SourceLead,
        };
        var metaJson = JsonSerializer.Serialize(meta, ResumeMakerJsonContext.Default.TemplateMetadata);
        await FileUtil.WriteAtomicTextAsync(HtmlPath(template.Id), template.HtmlContent ?? "", ct).ConfigureAwait(false);
        await FileUtil.WriteAtomicTextAsync(MetaPath(template.Id), metaJson, ct).ConfigureAwait(false);
    }

    public Task<bool> DeleteCustomAsync(string id, CancellationToken ct = default)
    {
        if (IsBuiltInId(id) || !IsValidId(id))
            return Task.FromResult(false);
        var existed = false;
        foreach (var path in new[] { HtmlPath(id), MetaPath(id) })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                existed = true;
            }
        }
        return Task.FromResult(existed);
    }

    private string HtmlPath(string id) => Path.Combine(paths.CustomTemplatesDir, id + ".html");
    private string MetaPath(string id) => Path.Combine(paths.CustomTemplatesDir, id + ".meta.json");

    private static Template? LoadBuiltIn(string id)
    {
        using var stream = typeof(TemplateRepository).Assembly.GetManifestResourceStream($"templates/{id}.html");
        if (stream is null)
            return null;
        using var reader = new StreamReader(stream, FileUtil.Utf8NoBom);
        var html = reader.ReadToEnd();
        var info = BuiltIns.FirstOrDefault(b => b.Id == id);
        return new Template
        {
            Id = id,
            Name = info.Name ?? "Cover Letter",
            Style = info.Name is null ? TemplateStyle.Minimal : info.Style,
            IsBuiltIn = true,
            HtmlContent = html,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
    }

    private IEnumerable<Template> LoadCustomTemplates()
    {
        if (!Directory.Exists(paths.CustomTemplatesDir))
            yield break;
        foreach (var file in Directory.EnumerateFiles(paths.CustomTemplatesDir, "*.html"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (!IsValidId(id) || IsBuiltInId(id))
                continue;
            var t = LoadCustom(id);
            if (t is not null)
                yield return t;
        }
    }

    private Template? LoadCustom(string id)
    {
        try
        {
            if (!File.Exists(HtmlPath(id)))
                return null;
            var html = File.ReadAllText(HtmlPath(id), FileUtil.Utf8NoBom);
            TemplateMetadata? meta = null;
            if (File.Exists(MetaPath(id)))
            {
                try
                {
                    meta = JsonSerializer.Deserialize(
                        File.ReadAllBytes(MetaPath(id)),
                        ResumeMakerJsonContext.Default.TemplateMetadata);
                }
                catch (JsonException) { }
            }
            return new Template
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(meta?.Name) ? id : meta!.Name,
                Style = TemplateStyle.Custom,
                IsBuiltIn = false,
                HtmlContent = html,
                CreatedAt = meta?.CreatedAt ?? File.GetCreationTimeUtc(HtmlPath(id)),
                SourceLead = meta?.SourceLead,
            };
        }
        catch (IOException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]{0,63}$")]
    private static partial Regex SlugRegex();
}
