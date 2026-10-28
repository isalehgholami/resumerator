using System.Text;
using System.Text.Json;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Application.Services;

public sealed class ProfileService(IProfileReader reader, IProfileWriter writer, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private static readonly (SectionType Type, string Fa, string En)[] DefaultSections =
    [
        (SectionType.Experience, "تجربه کاری", "Experience"),
        (SectionType.Education, "تحصیلات", "Education"),
        (SectionType.Skills, "مهارت‌ها", "Skills"),
        (SectionType.Languages, "زبان‌ها", "Languages"),
        (SectionType.Projects, "پروژه‌ها", "Projects"),
        (SectionType.Awards, "جوایز", "Awards"),
        (SectionType.Interests, "علایق", "Interests"),
    ];

    public Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken ct = default) => reader.ListAsync(ct);

    public Task<Profile?> GetAsync(Guid id, CancellationToken ct = default) => reader.GetAsync(id, ct);

    public async Task<Profile> CreateAsync(string name, string language = "fa", CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        var lang = language == "en" ? "en" : "fa";
        var sections = DefaultSections
            .Select(
                (d, i) =>
                    new Section
                    {
                        Type = d.Type,
                        Title = lang == "fa" ? d.Fa : d.En,
                        Order = i,
                    }
            )
            .ToList();
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            Name = name,
            Language = lang,
            CreatedAt = now,
            UpdatedAt = now,
            Sections = sections,
        };
        await writer.SaveAsync(profile, ct).ConfigureAwait(false);
        return profile;
    }

    public async Task<Profile> SaveAsync(Profile profile, CancellationToken ct = default)
    {
        var updated = profile with { UpdatedAt = _time.GetUtcNow() };
        await writer.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => writer.DeleteAsync(id, ct);

    public async Task<Profile> DuplicateAsync(Guid id, string? newName = null, CancellationToken ct = default)
    {
        var source =
            await reader.GetAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidProfileException($"Profile '{id}' was not found.");
        var now = _time.GetUtcNow();
        var copy = source with
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(newName) ? source.Name + " (copy)" : newName,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await writer.SaveAsync(copy, ct).ConfigureAwait(false);
        return copy;
    }

    public static string ExportJson(Profile profile) =>
        JsonSerializer.Serialize(profile, ResumeMakerJsonContext.Default.Profile);

    public static byte[] ExportJsonBytes(Profile profile) => new UTF8Encoding(false).GetBytes(ExportJson(profile));

    public static Profile ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidProfileException("Profile JSON is empty.");
        }
        try
        {
            return JsonSerializer.Deserialize(json, ResumeMakerJsonContext.Default.Profile)
                ?? throw new InvalidProfileException("Profile JSON is empty (null).");
        }
        catch (JsonException ex)
        {
            throw new InvalidProfileException($"Invalid profile JSON: {ex.Message}", ex);
        }
    }

    public static Profile AddSection(Profile profile, Section section)
    {
        var list = Ordered(profile);
        list.Add(section);
        return profile with { Sections = Renumber(list) };
    }

    public static Profile RemoveSection(Profile profile, string sectionId)
    {
        var list = Ordered(profile);
        list.RemoveAll(s => s.Id == sectionId);
        return profile with { Sections = Renumber(list) };
    }

    public static Profile MoveSection(Profile profile, string sectionId, int newIndex)
    {
        var list = Ordered(profile);
        var current = list.FindIndex(s => s.Id == sectionId);
        if (current < 0)
        {
            return profile with { Sections = Renumber(list) };
        }
        var section = list[current];
        list.RemoveAt(current);
        list.Insert(Math.Clamp(newIndex, 0, list.Count), section);
        return profile with { Sections = Renumber(list) };
    }

    public static Profile ToggleSection(Profile profile, string sectionId)
    {
        var list = Ordered(profile).Select(s => s.Id == sectionId ? s with { Enabled = !s.Enabled } : s).ToList();
        return profile with { Sections = Renumber(list) };
    }

    private static List<Section> Ordered(Profile profile) => [.. profile.Sections.OrderBy(s => s.Order)];

    private static List<Section> Renumber(List<Section> list) => [.. list.Select((s, i) => s with { Order = i })];
}
