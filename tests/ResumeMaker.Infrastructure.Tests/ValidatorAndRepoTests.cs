using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure.Templating;

namespace ResumeMaker.Infrastructure.Tests;

public class ValidatorTests
{
    private const string Good = """
        <!DOCTYPE html><html dir="{{ direction }}" lang="{{ lang }}"><head><style>
        @font-face { font-family: X; src: url(data:font/woff2;base64,{{ font_base64 }}); }
        body { color: {{ primary_color }}; }
        </style></head><body><header>{{ profile.contact.full_name }}</header><main>
        {{ for section in profile.sections }}<section><h2>{{ section.title }}</h2>
        {{ if section.type == "skills" }}<p class="{{ lang }}">x</p>{{ end }}</section>{{ end }}
        </main><a href="https://example.com/me">link</a></body></html>
        """;

    private readonly TemplateValidator _v = new();

    private static string[] Codes(ResumeMaker.Core.Application.Dtos.ValidationResult r) => r.Issues.Select(i => i.Code).ToArray();

    [Fact]
    public void ValidTemplate_Passes_NoIssues()
    {
        var r = _v.Validate(Good);
        Assert.True(r.IsValid);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void MissingRequiredPlaceholders_AreErrors()
    {
        var r = _v.Validate("<html><style>@font-face{}</style><body><header></header><main><section></section></main></body></html>");
        Assert.False(r.IsValid);
        Assert.Contains("missing-full-name", Codes(r));
        Assert.Contains("missing-sections-loop", Codes(r));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "script-tag")]
    [InlineData("<iframe src=\"x\"></iframe>", "iframe-tag")]
    [InlineData("<object data=\"x\"></object>", "object-tag")]
    [InlineData("<embed src=\"x\">", "embed-tag")]
    [InlineData("<link rel=\"stylesheet\" href=\"https://evil.test/a.css\">", "external-url")]
    [InlineData("<img src=\"http://evil.test/a.png\">", "external-url")]
    [InlineData("<img src=\"//evil.test/a.png\">", "external-url")]
    [InlineData("<style>@import url(https://evil.test/a.css);</style>", "external-url")]
    [InlineData("<div style=\"background:url('https://evil.test/a.png')\"></div>", "external-url")]
    [InlineData("<p onclick=\"x()\">a</p>", "event-handler")]
    [InlineData("<body onload=\"x()\"></body>", "event-handler")]
    public void DangerousConstructs_AreErrors(string injected, string code)
    {
        var r = _v.Validate(Good.Replace("</main>", injected + "</main>"));
        Assert.False(r.IsValid);
        Assert.Contains(code, Codes(r));
    }

    [Fact]
    public void MissingFontFace_IsError_MissingSemanticTags_AreWarnings()
    {
        var r = _v.Validate("<div>{{ profile.contact.full_name }}{{ for s in profile.sections }}{{ end }}</div>");
        Assert.Contains(r.Issues, i => i.Code == "missing-font-face" && i.IsError);
        foreach (var code in new[] { "missing-header", "missing-main", "missing-section" })
            Assert.Contains(r.Issues, i => i.Code == code && !i.IsError);
    }

    [Fact]
    public void ScribanTagsInsideAttributes_AreTolerated()
    {
        var html = Good.Replace("<p class=\"{{ lang }}\">", "<p class=\"{{ if a == \"b\" }}x{{ end }}\" title=\"{{ profile.summary }}\">");
        Assert.True(_v.Validate(html).IsValid);
    }

    [Fact]
    public void Sanitize_StripsDangerousContent_KeepsRest()
    {
        var dirty = Good.Replace("</main>",
            "<script>x()</script><iframe src=\"a\"></iframe><object></object><embed src=\"a\"><p onclick=\"y()\" id=\"k\">hi</p></main>")
            .Replace("<style>", "<link rel=\"stylesheet\" href=\"https://evil.test/a.css\"><style>@import url(https://evil.test/z.css);");
        var clean = _v.Sanitize(dirty);

        Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<embed", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.test", clean);
        Assert.Contains("id=\"k\"", clean);
        Assert.Contains("{{ profile.contact.full_name }}", clean);
        Assert.Contains("@font-face", clean);
        Assert.True(_v.Validate(clean).IsValid);
    }
}

public class TemplateRepositoryTests
{
    [Fact]
    public async Task ListsBuiltIns_InOrder_WithoutCoverLetter()
    {
        using var dir = new TempDir();
        var repo = new TemplateRepository(dir.Paths);
        var list = await repo.ListAsync();
        Assert.Equal(["minimal", "classic", "modern", "compact", "sidebar", "ats"], list.Select(t => t.Id).ToArray());
        Assert.All(list, t => { Assert.True(t.IsBuiltIn); Assert.NotEmpty(t.HtmlContent); });

        var cover = await repo.GetByIdAsync("cover-letter");
        Assert.NotNull(cover);
        Assert.True(cover!.IsBuiltIn);
        Assert.Null(await repo.GetByIdAsync("nope"));
        Assert.Null(await repo.GetByIdAsync("../etc/passwd"));
    }

    [Fact]
    public async Task Custom_Save_Get_List_Delete()
    {
        using var dir = new TempDir();
        var repo = new TemplateRepository(dir.Paths);
        await repo.SaveCustomAsync(new Template { Id = "my-tpl", Name = "Mine", HtmlContent = "<p>hi فارسی</p>", SourceLead = "lead text" });

        var got = await repo.GetByIdAsync("my-tpl");
        Assert.NotNull(got);
        Assert.False(got!.IsBuiltIn);
        Assert.Equal(TemplateStyle.Custom, got.Style);
        Assert.Equal("Mine", got.Name);
        Assert.Equal("lead text", got.SourceLead);
        Assert.Equal("<p>hi فارسی</p>", got.HtmlContent);
        Assert.True(File.Exists(Path.Combine(dir.Paths.CustomTemplatesDir, "my-tpl.meta.json")));

        var list = await repo.ListAsync();
        Assert.Equal("my-tpl", list[^1].Id);
        Assert.Equal(7, list.Count);

        Assert.True(await repo.DeleteCustomAsync("my-tpl"));
        Assert.False(await repo.DeleteCustomAsync("my-tpl"));
        Assert.Null(await repo.GetByIdAsync("my-tpl"));
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("ats")]
    [InlineData("cover-letter")]
    public async Task SaveCustom_RefusesBuiltInIds(string id)
    {
        using var dir = new TempDir();
        var repo = new TemplateRepository(dir.Paths);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveCustomAsync(new Template { Id = id, HtmlContent = "x" }));
        Assert.False(await repo.DeleteCustomAsync(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("UPPER")]
    [InlineData("a b")]
    public async Task SaveCustom_RefusesUnsafeIds(string id)
    {
        using var dir = new TempDir();
        var repo = new TemplateRepository(dir.Paths);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.SaveCustomAsync(new Template { Id = id, HtmlContent = "x" }));
    }
}
