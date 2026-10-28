using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Tests;

internal sealed class FakeProfileStore : IProfileReader, IProfileWriter
{
    public Dictionary<Guid, Profile> Items { get; } = [];

    public Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProfileSummary>>(
            [.. Items.Values.Select(p => new ProfileSummary(p.Id, p.Name, p.Language, p.TemplateId, p.UpdatedAt))]
        );

    public Task<Profile?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.GetValueOrDefault(id));

    public Task SaveAsync(Profile profile, CancellationToken ct = default)
    {
        Items[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Remove(id));
}

internal sealed class FakeTemplateRepo(params string[] ids) : ITemplateRepository
{
    public Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Template>>([.. ids.Select(i => new Template { Id = i, HtmlContent = "T:" + i })]);

    public Task<Template?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult<Template?>(ids.Contains(id) ? new Template { Id = id, HtmlContent = "T:" + id } : null);

    public Task SaveCustomAsync(Template template, CancellationToken ct = default) => Task.CompletedTask;

    public Task<bool> DeleteCustomAsync(string id, CancellationToken ct = default) => Task.FromResult(false);
}

internal sealed class FakeEngine : ITemplateEngine
{
    public Task<string> RenderAsync(string templateHtml, Profile profile, CancellationToken ct = default) =>
        Task.FromResult($"<html>{templateHtml}|{profile.Contact.FullName}</html>");

    public Task<string> RenderCoverLetterAsync(
        string templateHtml,
        Profile profile,
        CoverLetter letter,
        CancellationToken ct = default
    ) => Task.FromResult($"<html>{templateHtml}|{letter.Body}</html>");
}

internal sealed class FakeExporter : IPdfExporter, IPngExporter
{
    public string? LastHtml { get; private set; }
    public string? LastName { get; private set; }
    public int LastDpi { get; private set; }

    public Task<ExportResult> ExportPdfAsync(string html, string fileName, CancellationToken ct = default)
    {
        LastHtml = html;
        LastName = fileName;
        return Task.FromResult(new ExportResult([1], "application/pdf", fileName + ".pdf"));
    }

    public Task<ExportResult> ExportPngAsync(string html, string fileName, int dpi, CancellationToken ct = default)
    {
        LastHtml = html;
        LastName = fileName;
        LastDpi = dpi;
        return Task.FromResult(new ExportResult([2], "image/png", fileName + ".png"));
    }
}

internal static class Samples
{
    public static Profile Rich() =>
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Maryam",
            Language = "fa",
            TemplateId = "modern",
            Contact = new ContactInfo
            {
                FullName = "مریم احمدی",
                Title = "Engineer",
                Email = "m@example.com",
                Links = new() { ["linkedin"] = "https://linkedin.com/in/m", ["GitHub"] = "https://github.com/m" },
            },
            Summary = "Summary",
            Theme = new ThemeOverrides { PrimaryColor = "#000000" },
            Sections =
            [
                new Section
                {
                    Id = "s1",
                    Type = SectionType.Experience,
                    Title = "تجربه",
                    Order = 0,
                    Items =
                    [
                        new SectionItem
                        {
                            Title = "Dev",
                            Company = "X",
                            Start = "1401",
                            Tags = ["C#", "SQL"],
                            Description = "Did things",
                        },
                    ],
                },
                new Section
                {
                    Id = "s2",
                    Type = SectionType.Skills,
                    Title = "مهارت",
                    Order = 1,
                    Items = [new SectionItem { Category = "Code", Entries = [new SkillEntry { Name = "C#", Level = "5" }] }],
                },
                new Section { Id = "s3", Type = SectionType.Custom, Title = "Misc", Order = 2 },
            ],
        };
}
