using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure;

namespace ResumeMaker.Infrastructure.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rmtest-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public AppPaths Paths => new(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch (IOException) { }
    }
}

public sealed class FakeFonts : IFontResourceProvider
{
    public Task<string> GetFontBase64Async(string language, bool bold = false, CancellationToken ct = default) =>
        Task.FromResult(bold ? "BOLD64" : "REG64");
}

public static class Samples
{
    public static Profile Profile(string lang = "en", string name = "Ada Lovelace") => new()
    {
        Name = "Main",
        Language = lang,
        Contact = new ContactInfo
        {
            FullName = name,
            Title = "Engineer",
            Email = "ada@example.com",
            Links = new() { ["github"] = "https://github.com/ada" },
        },
        Summary = "Pioneer <b>of</b> computing & more",
        Sections =
        [
            new Section
            {
                Id = "exp", Type = SectionType.Experience, Title = "Experience", Order = 2,
                Items =
                [
                    new SectionItem { Title = "Analyst", Company = "Babbage & Co", Start = "2020", End = "2023", Tags = ["math", "c#"], Description = "Did \"things\"" },
                    new SectionItem { Title = "Intern", Company = "Acme", Start = "2019" },
                ],
            },
            new Section
            {
                Id = "skl", Type = SectionType.Skills, Title = "Skills", Order = 1,
                Items = [new SectionItem { Category = "Lang", Entries = [new SkillEntry { Name = "C#", Level = "Expert" }] }],
            },
            new Section { Id = "off", Type = SectionType.Awards, Title = "Hidden", Enabled = false, Order = 0, Items = [new SectionItem { Title = "X" }] },
        ],
    };
}
