using System.Text.Json;
using ResumeMaker.Core;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure.Fonts;
using ResumeMaker.Infrastructure.Templating;

namespace ResumeMaker.Infrastructure.Tests;

public class BuiltInTemplateTests
{
    private static string TemplatesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "templates")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "templates");
    }

    private static Profile LoadSample(string lang) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(TemplatesDir(), $"sample-profile.{lang}.json")),
            ResumeMakerJsonContext.Default.Profile)!;

    public static IEnumerable<object[]> Cases() =>
        from id in new[] { "minimal", "classic", "modern", "compact", "sidebar", "ats" }
        from lang in new[] { "fa", "en" }
        select new object[] { id, lang };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BuiltIn_ValidatesAndRenders(string id, string lang)
    {
        using var dir = new TempDir();
        var template = await new TemplateRepository(dir.Paths).GetByIdAsync(id);
        Assert.NotNull(template);

        var validation = new TemplateValidator().Validate(template!.HtmlContent);
        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Where(i => i.IsError).Select(i => i.Code + ": " + i.Message)));

        var profile = LoadSample(lang);
        var html = await new ScribanTemplateEngine(new EmbeddedFontProvider()).RenderAsync(template.HtmlContent, profile);

        Assert.DoesNotContain("{{", html);
        Assert.DoesNotContain("}}", html);
        Assert.Contains(profile.Contact.FullName, html);
        Assert.Contains(lang == "fa" ? "dir=\"rtl\"" : "dir=\"ltr\"", html);
        Assert.Contains("data:font/woff2;base64,d09GMg", html); // wOF2
        Assert.True(new TemplateValidator().Validate(html).Issues.All(i => i.Code != "script-tag" && i.Code != "external-url"));
    }

    [Theory]
    [InlineData("fa")]
    [InlineData("en")]
    public async Task CoverLetter_Renders(string lang)
    {
        using var dir = new TempDir();
        var template = await new TemplateRepository(dir.Paths).GetByIdAsync("cover-letter");
        var letter = new CoverLetter { Company = "Acme", Position = "Dev", Language = lang, Body = "Para one.\n\nPara two." };
        var html = await new ScribanTemplateEngine(new EmbeddedFontProvider())
            .RenderCoverLetterAsync(template!.HtmlContent, LoadSample(lang), letter);
        Assert.DoesNotContain("{{", html);
        Assert.Contains("Para two.", html);
    }
}
