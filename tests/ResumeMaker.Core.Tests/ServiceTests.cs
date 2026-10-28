using System.Text;
using ResumeMaker.Core.Application;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Common;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Tests;

public class ProfileServiceTests
{
    private static (ProfileService Svc, FakeProfileStore Store) Create()
    {
        var store = new FakeProfileStore();
        return (new ProfileService(store, store), store);
    }

    [Fact]
    public void Json_round_trip_uses_snake_case_and_lowercase_enums()
    {
        var p = Samples.Rich();
        var json = ProfileService.ExportJson(p);
        Assert.Contains("\"full_name\"", json);
        Assert.Contains("\"template_id\"", json);
        Assert.Contains("\"created_at\"", json);
        Assert.Contains("\"type\": \"experience\"", json);
        Assert.Contains("\"linkedin\"", json);
        Assert.DoesNotContain("FullName", json);
        Assert.DoesNotContain("\"direction\"", json);
        var back = ProfileService.ImportJson(json);
        Assert.Equal(p.Id, back.Id);
        Assert.Equal(p.Contact.FullName, back.Contact.FullName);
        Assert.Equal(3, back.Sections.Count);
        Assert.Equal(SectionType.Skills, back.Sections[1].Type);
        Assert.Equal(["C#", "SQL"], back.Sections[0].Items[0].Tags);
        Assert.Equal("5", back.Sections[1].Items[0].Entries![0].Level);
    }

    [Fact]
    public void ExportJsonBytes_has_no_bom()
    {
        var bytes = ProfileService.ExportJsonBytes(Samples.Rich());
        Assert.Equal((byte)'{', bytes[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("null")]
    public void ImportJson_invalid_throws(string json) =>
        Assert.Throws<InvalidProfileException>(() => ProfileService.ImportJson(json));

    [Fact]
    public void ImportJson_error_message_is_clear()
    {
        var ex = Assert.Throws<InvalidProfileException>(() => ProfileService.ImportJson("{oops"));
        Assert.Contains("Invalid profile JSON", ex.Message);
    }

    [Fact]
    public async Task Create_adds_ordered_localized_default_sections()
    {
        var (svc, store) = Create();
        var fa = await svc.CreateAsync("A", "fa");
        var en = await svc.CreateAsync("B", "en");
        var other = await svc.CreateAsync("C", "xx");
        Assert.Equal(7, fa.Sections.Count);
        Assert.Equal("تجربه کاری", fa.Sections[0].Title);
        Assert.Equal("Experience", en.Sections[0].Title);
        Assert.Equal("fa", other.Language);
        Assert.Equal(Enumerable.Range(0, 7), fa.Sections.Select(s => s.Order));
        Assert.Equal(SectionType.Interests, fa.Sections[6].Type);
        Assert.Equal(3, store.Items.Count);
    }

    [Fact]
    public async Task Save_bumps_UpdatedAt_and_persists()
    {
        var (svc, store) = Create();
        var p = Samples.Rich() with { UpdatedAt = DateTimeOffset.UnixEpoch };
        var saved = await svc.SaveAsync(p);
        Assert.True(saved.UpdatedAt > p.UpdatedAt);
        Assert.Equal(saved.UpdatedAt, store.Items[p.Id].UpdatedAt);
    }

    [Fact]
    public async Task List_get_delete_delegate()
    {
        var (svc, _) = Create();
        var p = await svc.CreateAsync("N");
        Assert.Single(await svc.ListAsync());
        Assert.NotNull(await svc.GetAsync(p.Id));
        Assert.True(await svc.DeleteAsync(p.Id));
        Assert.Null(await svc.GetAsync(p.Id));
    }

    [Fact]
    public async Task Duplicate_creates_new_id_and_fresh_timestamps()
    {
        var (svc, store) = Create();
        var src = Samples.Rich() with { CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch };
        store.Items[src.Id] = src;
        var copy = await svc.DuplicateAsync(src.Id);
        var named = await svc.DuplicateAsync(src.Id, "Other");
        Assert.NotEqual(src.Id, copy.Id);
        Assert.Equal("Maryam (copy)", copy.Name);
        Assert.Equal("Other", named.Name);
        Assert.True(copy.CreatedAt > src.CreatedAt);
        Assert.Equal(src.Sections.Count, copy.Sections.Count);
        Assert.Equal(3, store.Items.Count - 0);
    }

    [Fact]
    public async Task Duplicate_missing_throws()
    {
        var (svc, _) = Create();
        await Assert.ThrowsAsync<InvalidProfileException>(() => svc.DuplicateAsync(Guid.NewGuid()));
    }

    [Fact]
    public void MoveSection_renumbers_and_clamps()
    {
        var p = Samples.Rich();
        var moved = ProfileService.MoveSection(p, "s3", 0);
        Assert.Equal(["s3", "s1", "s2"], moved.Sections.Select(s => s.Id));
        Assert.Equal([0, 1, 2], moved.Sections.Select(s => s.Order));
        var last = ProfileService.MoveSection(p, "s1", 99);
        Assert.Equal(["s2", "s3", "s1"], last.Sections.Select(s => s.Id));
        var first = ProfileService.MoveSection(p, "s2", -5);
        Assert.Equal("s2", first.Sections[0].Id);
        var unknown = ProfileService.MoveSection(p, "zzz", 1);
        Assert.Equal(["s1", "s2", "s3"], unknown.Sections.Select(s => s.Id));
    }

    [Fact]
    public void Add_remove_toggle_sections()
    {
        var p = Samples.Rich();
        var added = ProfileService.AddSection(p, new Section { Id = "n", Title = "New" });
        Assert.Equal(4, added.Sections.Count);
        Assert.Equal(3, added.Sections[3].Order);
        var removed = ProfileService.RemoveSection(added, "s1");
        Assert.Equal(["s2", "s3", "n"], removed.Sections.Select(s => s.Id));
        Assert.Equal([0, 1, 2], removed.Sections.Select(s => s.Order));
        var toggled = ProfileService.ToggleSection(p, "s2");
        Assert.False(toggled.Sections[1].Enabled);
        Assert.True(ProfileService.ToggleSection(toggled, "s2").Sections[1].Enabled);
        Assert.True(p.Sections[1].Enabled);
    }
}

public class TemplateAndExportTests
{
    private static (ExportService Export, FakeExporter Fake, TemplateService Templates, CoverLetterService Cover) Create(
        bool withPng = true,
        params string[] ids
    )
    {
        var repo = new FakeTemplateRepo(ids.Length == 0 ? ["modern", "ats", "cover-letter"] : ids);
        var engine = new FakeEngine();
        var t = new TemplateService(repo, engine);
        var c = new CoverLetterService(repo, engine);
        var fake = new FakeExporter();
        return (new ExportService(t, c, fake, withPng ? fake : null), fake, t, c);
    }

    [Fact]
    public async Task Template_selection_rules()
    {
        var (_, _, t, _) = Create();
        var p = Samples.Rich();
        Assert.Contains("T:modern", await t.RenderAsync(new TemplateRenderRequest(p)));
        Assert.Contains("T:ats", await t.RenderAsync(new TemplateRenderRequest(p with { AtsMode = true })));
        Assert.Contains("T:ats", await t.RenderAsync(new TemplateRenderRequest(p, "ats")));
        Assert.Equal("ats", TemplateService.ResolveTemplateId(new TemplateRenderRequest(p, "ats")));
    }

    [Fact]
    public async Task Missing_template_throws()
    {
        var (_, _, t, _) = Create();
        await Assert.ThrowsAsync<TemplateNotFoundException>(() =>
            t.RenderAsync(new TemplateRenderRequest(Samples.Rich(), "nope"))
        );
    }

    [Fact]
    public async Task Export_html_and_json()
    {
        var (e, _, _, _) = Create();
        var html = await e.ExportAsync(new ExportRequest(Samples.Rich(), ExportFormat.Html, "my/res:ume"));
        Assert.Equal("text/html", html.ContentType);
        Assert.Equal("my_res_ume.html", html.FileName);
        Assert.StartsWith("<html>", Encoding.UTF8.GetString(html.Content));
        var json = await e.ExportAsync(new ExportRequest(Samples.Rich(), ExportFormat.Json));
        Assert.Equal("application/json", json.ContentType);
        Assert.Equal("resume.json", json.FileName);
        Assert.Equal((byte)'{', json.Content[0]);
    }

    [Fact]
    public async Task Export_pdf_and_png_receive_rendered_html()
    {
        var (e, fake, _, _) = Create();
        var pdf = await e.ExportAsync(new ExportRequest(Samples.Rich() with { AtsMode = true }, ExportFormat.Pdf, "cv"));
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Contains("T:ats", fake.LastHtml);
        var png = await e.ExportAsync(new ExportRequest(Samples.Rich(), ExportFormat.Png, "cv", 150));
        Assert.Equal("image/png", png.ContentType);
        Assert.Equal(150, fake.LastDpi);
        Assert.Equal("cv", fake.LastName);
    }

    [Fact]
    public async Task Png_without_exporter_fails()
    {
        var (e, _, _, _) = Create(withPng: false);
        await Assert.ThrowsAsync<ExportFailedException>(() =>
            e.ExportAsync(new ExportRequest(Samples.Rich(), ExportFormat.Png))
        );
    }

    [Fact]
    public async Task Cover_letter_export_routes_and_rejects_png()
    {
        var (e, fake, _, _) = Create();
        var letter = new CoverLetter { Body = "Hello" };
        var html = await e.ExportCoverLetterAsync(Samples.Rich(), letter, ExportFormat.Html, "");
        Assert.Equal("cover-letter.html", html.FileName);
        Assert.Contains("Hello", Encoding.UTF8.GetString(html.Content));
        var pdf = await e.ExportCoverLetterAsync(Samples.Rich(), letter, ExportFormat.Pdf, "cl?");
        Assert.Equal("cl", fake.LastName);
        Assert.Equal("application/pdf", pdf.ContentType);
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            e.ExportCoverLetterAsync(Samples.Rich(), letter, ExportFormat.Png, "x")
        );
    }

    [Fact]
    public async Task Cover_letter_missing_template_throws()
    {
        var (_, _, _, c) = Create(true, "modern");
        await Assert.ThrowsAsync<TemplateNotFoundException>(() => c.RenderAsync(Samples.Rich(), new CoverLetter()));
    }

    [Theory]
    [InlineData("a/b\\c:d*e", "a_b_c_d_e")]
    [InlineData("  ..name.. ", "name")]
    [InlineData("", "resume")]
    [InlineData(null, "resume")]
    [InlineData("???", "resume")]
    [InlineData("رزومه من", "رزومه من")]
    public void Filename_sanitization(string? input, string expected) =>
        Assert.Equal(expected, ProfileFileName.Sanitize(input));

    [Fact]
    public void Filename_is_truncated() =>
        Assert.Equal(100, ProfileFileName.Sanitize(new string('a', 300)).Length);
}

public class LeadTests
{
    [Fact]
    public void Template_lead_has_all_layers_and_real_scriban_syntax()
    {
        var history = new LeadHistory();
        var gen = new LeadGenerator(history);
        var prompt = gen.GenerateTemplateLead(
            new TemplateLeadOptions
            {
                PrimaryColor = "#123456",
                AccentColor = "#abcdef",
                SectionPriority = ["skills", "experience"],
            }
        );
        foreach (var h in new[] { "# System Role", "# Project Brief", "# JSON Schema", "# Constraints", "# Example", "# Output Requirements" })
        {
            Assert.Contains(h, prompt);
        }
        Assert.Contains("#123456", prompt);
        Assert.Contains("#abcdef", prompt);
        Assert.Contains("RTL", prompt);
        Assert.Contains("skills > experience", prompt);
        Assert.Contains("{{ for section in profile.sections }}", prompt);
        Assert.Contains("{{ font_base64 }}", prompt);
        Assert.Contains("{{ direction }}", prompt);
        Assert.Contains("labels.present", prompt);
        Assert.DoesNotContain("{{#each", prompt.Replace("`{{#each}}`", ""));
        Assert.Single(history.Entries);
        Assert.Equal(prompt, history.Last!.Prompt);
        Assert.Equal("template", history.Last.Kind);
    }

    [Fact]
    public void English_lead_is_ltr_with_default_priority()
    {
        var prompt = new LeadGenerator().GenerateTemplateLead(new TemplateLeadOptions { Language = "en" });
        Assert.Contains("(LTR)", prompt);
        Assert.Contains("Inter", prompt);
        Assert.Contains("experience > education", prompt);
    }

    [Fact]
    public void Shortcut_links()
    {
        Assert.Equal(3, LeadGenerator.ShortcutLinks.Count);
        Assert.Contains(LeadGenerator.ShortcutLinks, l => l.Name == "Claude" && l.Url == "https://claude.ai");
        Assert.Contains(LeadGenerator.ShortcutLinks, l => l.Url == "https://chatgpt.com");
        Assert.Contains(LeadGenerator.ShortcutLinks, l => l.Url == "https://gemini.google.com");
    }

    [Fact]
    public void History_caps_and_clears()
    {
        var h = new LeadHistory(2);
        Assert.Null(h.Last);
        h.Add("a", "1");
        h.Add("b", "2");
        h.Add("c", "3");
        Assert.Equal(["2", "3"], h.Entries.Select(e => e.Prompt));
        h.Clear();
        Assert.Empty(h.Entries);
    }

    [Theory]
    [InlineData("formal", true, "شما")]
    [InlineData("friendly", true, "friendly Persian")]
    [InlineData("bold", true, "confident")]
    [InlineData("formal", false, "Dear Hiring Manager")]
    [InlineData("friendly", false, "warm")]
    [InlineData("bold", false, "led, built")]
    public void Tone_instructions(string tone, bool fa, string expected) =>
        Assert.Contains(expected, CoverLetterService.ToneInstruction(tone, fa));

    [Fact]
    public void Cover_letter_lead_contains_profile_keywords_and_structure()
    {
        var history = new LeadHistory();
        var svc = new CoverLetterService(new FakeTemplateRepo(), new FakeEngine(), history);
        var prompt = svc.GenerateLead(
            Samples.Rich(),
            new CoverLetterOptions { Company = "Acme", Position = "Dev", Keywords = ["ASP.NET Core", " ", "Leadership"], Tone = "formal" }
        );
        Assert.Contains("Acme", prompt);
        Assert.Contains("- ASP.NET Core", prompt);
        Assert.Contains("Leadership", prompt);
        Assert.Contains("مریم احمدی", prompt);
        Assert.Contains("# Structure", prompt);
        Assert.Contains("A4", prompt);
        Assert.Contains("350-450 words", prompt);
        Assert.Contains("\"type\": \"experience\"", prompt);
        Assert.DoesNotContain("m@example.com", prompt);
        Assert.Equal("cover_letter", history.Last!.Kind);
        var en = svc.GenerateLead(Samples.Rich(), new CoverLetterOptions { Language = "en" });
        Assert.Contains("300-400 words", en);
    }

    [Fact]
    public void Summarize_skips_disabled_sections()
    {
        var p = Samples.Rich();
        var json = CoverLetterService.SummarizeProfile(
            ProfileService.ToggleSection(p, "s2") with { Summary = "" }
        );
        Assert.DoesNotContain("\"skills\"", json);
        Assert.DoesNotContain("\"summary\"", json);
    }
}

public class ValidationTests
{
    private readonly ValidationService _svc = new();

    private static bool Has(ValidationResult r, string code) => r.Issues.Any(i => i.Code == code);

    [Fact]
    public void Valid_profile_passes()
    {
        var r = _svc.Validate(Samples.Rich());
        Assert.True(r.IsValid);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Required_fields_are_errors()
    {
        var r = _svc.Validate(new Profile { Name = " ", TemplateId = "", Language = "de" });
        Assert.False(r.IsValid);
        foreach (var code in new[] { "name_required", "full_name_required", "template_required", "language_invalid" })
        {
            Assert.True(Has(r, code), code);
        }
        var warn = r.Issues.Single(i => i.Code == "no_enabled_sections");
        Assert.False(warn.IsError);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("a@b")]
    [InlineData("a b@c.com")]
    [InlineData("@c.com")]
    [InlineData("a@@c.com")]
    [InlineData("a@c.")]
    [InlineData("a@.com")]
    public void Bad_emails(string email)
    {
        var p = Samples.Rich() with { Contact = Samples.Rich().Contact with { Email = email } };
        Assert.True(Has(_svc.Validate(p), "email_invalid"));
    }

    [Fact]
    public void Link_schemes()
    {
        var c = Samples.Rich().Contact;
        var bad = Samples.Rich() with { Contact = c with { Links = new() { ["x"] = "ftp://a.com", ["y"] = "linkedin.com/in/q", ["z"] = "" } } };
        var r = _svc.Validate(bad);
        Assert.Equal(2, r.Issues.Count(i => i.Code == "link_invalid"));
        var empty = Samples.Rich() with { Contact = c with { Email = "", Links = [] } };
        Assert.True(_svc.Validate(empty).IsValid);
    }

    [Fact]
    public void Cover_letter_validation()
    {
        var empty = _svc.ValidateCoverLetter(new CoverLetter());
        Assert.False(empty.IsValid);
        Assert.Contains(empty.Issues, i => i.Code == "company_required" && !i.IsError);
        var long_ = _svc.ValidateCoverLetter(
            new CoverLetter { Company = "A", Position = "B", Body = string.Join(' ', Enumerable.Repeat("word", 700)) }
        );
        Assert.True(long_.IsValid);
        Assert.Contains(long_.Issues, i => i.Code == "body_too_long");
        var ok = _svc.ValidateCoverLetter(new CoverLetter { Company = "A", Position = "B", Body = "Fine letter" });
        Assert.Empty(ok.Issues);
        Assert.Equal(2, CoverLetterService.CountWords("a  b\n"));
    }
}

public class NumeralTests
{
    [Fact]
    public void To_persian_maps_ascii_and_arabic_indic()
    {
        Assert.Equal("۰۱۲۳۴۵۶۷۸۹", "0123456789".ToPersianNumerals());
        Assert.Equal("۰۱۲۳۴۵۶۷۸۹", "٠١٢٣٤٥٦٧٨٩".ToPersianNumerals());
        Assert.Equal("سال ۱۴۰۱ - May", "سال 1401 - May".ToPersianNumerals());
    }

    [Fact]
    public void To_english_maps_back()
    {
        Assert.Equal("0123456789", "۰۱۲۳۴۵۶۷۸۹".ToEnglishNumerals());
        Assert.Equal("0123456789", "٠١٢٣٤٥٦٧٨٩".ToEnglishNumerals());
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Null_and_empty_are_safe(string? input, string expected)
    {
        Assert.Equal(expected, input.ToPersianNumerals());
        Assert.Equal(expected, input.ToEnglishNumerals());
    }
}
