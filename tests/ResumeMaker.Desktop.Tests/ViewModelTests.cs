using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Domain;
using ResumeMaker.Desktop.Localization;
using ResumeMaker.Desktop.Services;
using ResumeMaker.Desktop.ViewModels;

namespace ResumeMaker.Desktop.Tests;

[Collection("Desktop")]
public class ProfilesViewModelTests
{
    [Fact]
    public async Task Create_Duplicate_Delete_Flow()
    {
        using var app = new TestApp();
        var vm = app.Root.Main.Profiles;
        var opened = 0;
        vm.ProfileOpened += () => opened++;

        vm.NewName = "Maryam";
        vm.NewLanguage = LocalizedOption.Languages[1];
        await vm.CreateAsync();

        Assert.Equal(1, opened);
        Assert.Single(vm.Items);
        Assert.Equal("Maryam", app.Root.Session.Current!.Name);
        Assert.Equal("en", app.Root.Session.Current.Language);
        Assert.Same(app.Root.Main.Editor, app.Root.Main.CurrentPage);

        vm.Selected = vm.Items[0];
        await vm.DuplicateAsync();
        Assert.Equal(2, vm.Items.Count);
        Assert.Contains(vm.Items, p => p.Name == "Maryam (copy)");

        vm.Selected = vm.Items.First(p => p.Name == "Maryam (copy)");
        await vm.DeleteAsync();
        Assert.Single(vm.Items);
    }

    [Fact]
    public async Task Json_Export_Then_Import_Roundtrips_With_New_Id()
    {
        using var app = new TestApp();
        var vm = app.Root.Main.Profiles;
        vm.NewName = "Round Trip";
        await vm.CreateAsync();
        vm.Selected = vm.Items[0];
        var file = Path.Combine(app.Dir, "export.json");
        app.Picker.Save.Enqueue(file);
        await vm.ExportSelectedAsync();
        Assert.True(File.Exists(file));

        app.Picker.Open.Enqueue(file);
        await vm.ImportAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(2, vm.Items.Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public async Task Import_Of_Garbage_Reports_Error_Without_Throwing()
    {
        using var app = new TestApp();
        var file = Path.Combine(app.Dir, "bad.json");
        Directory.CreateDirectory(app.Dir);
        await File.WriteAllTextAsync(file, "{ not json");
        app.Picker.Open.Enqueue(file);

        await app.Root.Main.Profiles.ImportAsync();

        Assert.True(app.Root.Main.Status.IsError);
        Assert.Empty(app.Root.Main.Profiles.Items);
    }
}

[Collection("Desktop")]
public class EditorViewModelTests
{
    private static async Task<(TestApp App, EditorViewModel Editor)> OpenAsync()
    {
        var app = new TestApp();
        app.Root.Main.Profiles.NewName = "Editor Test";
        await app.Root.Main.Profiles.CreateAsync();
        return (app, app.Root.Main.Editor);
    }

    [Fact]
    public async Task Opening_A_Profile_Loads_Fields_And_Default_Sections()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            Assert.True(editor.HasProfile);
            Assert.Equal("Editor Test", editor.ProfileName);
            Assert.Equal(7, editor.Sections.Count);
            Assert.Equal(SectionType.Experience, editor.Sections[0].Type);
        }
    }

    [Fact]
    public async Task Field_Edits_Update_Session_And_Autosave_To_Disk()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            editor.FullName = "Maryam Ahmadi";
            editor.Email = "m@example.com";
            editor.LinkedIn = "https://linkedin.com/in/m";
            editor.PrimaryColor = "#112233";

            var cur = app.Root.Session.Current!;
            Assert.Equal("Maryam Ahmadi", cur.Contact.FullName);
            Assert.Equal("https://linkedin.com/in/m", cur.Contact.Links["linkedin"]);
            Assert.Equal("#112233", cur.Theme.PrimaryColor);

            var saved = await TestApp.Eventually(() =>
                app.Infra.Profiles.GetAsync(cur.Id).GetAwaiter().GetResult() is { Contact.FullName: "Maryam Ahmadi" } p ? p : null
            );
            Assert.Equal("m@example.com", saved.Contact.Email);
        }
    }

    [Fact]
    public async Task Clearing_A_Link_Removes_It()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            editor.GitHub = "https://github.com/x";
            Assert.True(app.Root.Session.Current!.Contact.Links.ContainsKey("github"));
            editor.GitHub = "";
            Assert.False(app.Root.Session.Current.Contact.Links.ContainsKey("github"));
        }
    }

    [Fact]
    public async Task Section_Toggle_Move_Add_Delete_Update_Profile()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            var first = editor.Sections[0];
            first.Enabled = false;
            Assert.False(app.Root.Session.Current!.Sections.First(s => s.Id == first.Id).Enabled);

            first.MoveDownCommand.Execute(null);
            Assert.Equal(1, app.Root.Session.Current.Sections.First(s => s.Id == first.Id).Order);

            editor.NewSectionType = editor.SectionTypeOptions.First(o => o.Value == nameof(SectionType.Awards));
            var before = editor.Sections.Count;
            editor.AddSectionCommand.Execute(null);
            Assert.Equal(before + 1, app.Root.Session.Current.Sections.Count);
            Assert.Equal(SectionType.Awards, editor.SelectedSection!.Type);

            first.RemoveCommand.Execute(null);
            Assert.DoesNotContain(app.Root.Session.Current.Sections, s => s.Id == first.Id);
        }
    }

    [Fact]
    public async Task Skills_Entries_And_Tags_Are_Parsed_From_Text()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            var skills = editor.Sections.First(s => s.Type == SectionType.Skills);
            skills.AddItemCommand.Execute(null);
            var item = skills.Items[0];
            item.Category = "Languages";
            item.EntriesText = "C# | Expert\nSQL\n | ignored";

            var section = app.Root.Session.Current!.Sections.First(s => s.Id == skills.Id);
            var entries = section.Items[0].Entries!;
            Assert.Equal(2, entries.Count);
            Assert.Equal(new SkillEntry { Name = "C#", Level = "Expert" }, entries[0]);
            Assert.Equal("", entries[1].Level);

            var exp = editor.Sections.First(s => s.Type == SectionType.Experience);
            exp.AddItemCommand.Execute(null);
            exp.Items[0].TagsText = "C#, .NET ،Azure,, ";
            var tags = app.Root.Session.Current.Sections.First(s => s.Id == exp.Id).Items[0].Tags!;
            Assert.Equal(["C#", ".NET", "Azure"], tags);
        }
    }

    [Fact]
    public async Task Item_Fields_Visible_Per_Section_Type()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            var education = editor.Sections.First(s => s.Type == SectionType.Education);
            education.AddItemCommand.Execute(null);
            Assert.True(education.Items[0].ShowEducation);
            Assert.False(education.Items[0].ShowCompany);

            var languages = editor.Sections.First(s => s.Type == SectionType.Languages);
            languages.AddItemCommand.Execute(null);
            Assert.True(languages.Items[0].ShowName);
            Assert.True(languages.Items[0].ShowLevel);
            Assert.False(languages.Items[0].ShowDescription);
        }
    }

    [Fact]
    public async Task Other_Pages_Changing_The_Profile_Reload_The_Editor()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            var templates = app.Root.Main.Templates;
            await templates.RefreshAsync();
            templates.AtsMode = true;
            Assert.True(app.Root.Session.Current!.AtsMode);
            Assert.Equal("Editor Test", editor.ProfileName);
        }
    }

    [Fact]
    public async Task Preview_Writes_Temp_File_With_Refresh_And_Opens_Browser_Once()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            editor.FullName = "Preview Person";
            await editor.PreviewCommand.ExecuteAsync(null);

            Assert.Single(app.Launcher.Files);
            var path = app.Launcher.Files[0];
            var html = await File.ReadAllTextAsync(path);
            Assert.Contains("Preview Person", html);
            Assert.Contains("http-equiv=\"refresh\"", html);

            editor.FullName = "Changed Name";
            var updated = await TestApp.Eventually(() => TryRead(path) is { } t && t.Contains("Changed Name") ? t : null);
            Assert.Contains("refresh", updated);
            Assert.Single(app.Launcher.Files);
        }
    }

    private static string? TryRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    [Fact]
    public async Task Exports_Do_Not_Contain_The_Preview_Refresh_Tag()
    {
        var (app, editor) = await OpenAsync();
        using (app)
        {
            editor.FullName = "Export Person";
            editor.Email = "e@example.com";
            await editor.PreviewCommand.ExecuteAsync(null);
            var export = app.Root.Main.Export;
            export.SelectedFormat = export.FormatOptions.First(o => o.Value == "Html");
            var target = Path.Combine(app.Dir, "out.html");
            app.Picker.Save.Enqueue(target);
            await export.ExportAsync();
            var html = await File.ReadAllTextAsync(target);
            Assert.Contains("Export Person", html);
            Assert.DoesNotContain("http-equiv=\"refresh\"", html);
        }
    }

    [Fact]
    public void InjectRefresh_Puts_Meta_In_Head_Or_Prepends()
    {
        Assert.Contains("<head><meta http-equiv=\"refresh\" content=\"2\">", PreviewService.InjectRefresh("<html><head><title>x</title></head></html>", 2));
        Assert.StartsWith("<meta", PreviewService.InjectRefresh("<p>no head</p>", 2));
        Assert.Equal("<p>x</p>", PreviewService.InjectRefresh("<p>x</p>", 0));
    }
}

[Collection("Desktop")]
public class TemplatesViewModelTests
{
    [Fact]
    public async Task Lists_Built_Ins_And_Selecting_One_Updates_Profile()
    {
        using var app = new TestApp();
        app.Root.Main.Profiles.NewName = "T";
        await app.Root.Main.Profiles.CreateAsync();
        var vm = app.Root.Main.Templates;
        await vm.RefreshAsync();

        Assert.Contains(vm.Templates, t => t.Id == "minimal" && t.IsBuiltIn);
        Assert.DoesNotContain(vm.Templates, t => t.Id == "cover-letter");
        Assert.Equal("minimal", vm.SelectedTemplate!.Id);

        vm.SelectedTemplate = vm.Templates.First(t => t.Id == "modern");
        Assert.Equal("modern", app.Root.Session.Current!.TemplateId);
    }

    [Fact]
    public async Task Import_Valid_Template_Saves_And_Selects_It_With_Source_Lead()
    {
        using var app = new TestApp();
        app.Root.Main.Profiles.NewName = "T";
        await app.Root.Main.Profiles.CreateAsync();
        var vm = app.Root.Main.Templates;
        await vm.RefreshAsync();

        // Produce a lead so SourceLead can be attached.
        app.Root.Main.LeadBuilder.GenerateCommand.Execute(null);

        var file = Path.Combine(app.Dir, "My Fancy.html");
        await File.WriteAllTextAsync(
            file,
            "<!DOCTYPE html><html dir=\"{{ direction }}\" lang=\"{{ lang }}\"><head><style>@font-face{font-family:F;src:url(data:font/woff2;base64,{{ font_base64 }}) format(\"woff2\")}body{color:#123}</style></head><body><h1>{{ profile.contact.full_name }}</h1>{{ for section in profile.sections }}<section>{{ section.title }}</section>{{ end }}</body></html>"
        );
        app.Picker.Open.Enqueue(file);
        await vm.ImportAsync();

        var custom = Assert.Single(vm.Templates, t => !t.IsBuiltIn);
        Assert.Equal("custom-my-fancy", custom.Id);
        Assert.Equal(custom.Id, vm.SelectedTemplate!.Id);
        Assert.Equal(custom.Id, app.Root.Session.Current!.TemplateId);
        var stored = (await app.Infra.Templates.GetByIdAsync(custom.Id))!;
        Assert.DoesNotContain("<script", stored.HtmlContent);
        Assert.NotNull(stored.SourceLead);
        Assert.True(vm.HasReport);

        await vm.DeleteCustomAsync();
        Assert.DoesNotContain(vm.Templates, t => !t.IsBuiltIn);
        Assert.Equal("minimal", app.Root.Session.Current.TemplateId);
    }

    [Fact]
    public async Task Import_Invalid_Template_Lists_Errors_And_Does_Not_Save()
    {
        using var app = new TestApp();
        var vm = app.Root.Main.Templates;
        await vm.RefreshAsync();
        var file = Path.Combine(app.Dir, "broken.html");
        Directory.CreateDirectory(app.Dir);
        await File.WriteAllTextAsync(file, "<html><body>no placeholders at all</body></html>");
        app.Picker.Open.Enqueue(file);

        await vm.ImportAsync();

        Assert.True(vm.HasReport);
        Assert.NotEmpty(vm.Report);
        Assert.All(vm.Templates, t => Assert.True(t.IsBuiltIn));
        Assert.True(app.Root.Main.Status.IsError);
    }

    [Theory]
    [InlineData("My Fancy", "custom-my-fancy")]
    [InlineData("  ", "custom-template")]
    [InlineData("Résumé!!", "custom-r-sum")]
    public void Slug_Is_A_Valid_Template_Id(string name, string expected)
    {
        var slug = TemplatesViewModel.Slug(name);
        Assert.Equal(expected, slug);
        Assert.True(ResumeMaker.Infrastructure.Templating.TemplateRepository.IsValidId(slug));
    }
}

[Collection("Desktop")]
public class LeadAndCoverLetterTests
{
    [Fact]
    public async Task Lead_Builder_Generates_Copies_And_Opens_Shortcuts()
    {
        using var app = new TestApp();
        var vm = app.Root.Main.LeadBuilder;
        vm.PrimaryColor = "#abcdef";
        vm.SelectedColumns = vm.ColumnOptions[1];
        vm.SelectedSidebar = vm.SidebarOptions.First(o => o.Value == "left");
        vm.GenerateCommand.Execute(null);

        Assert.Contains("#abcdef", vm.Lead);
        Assert.Contains("Columns: 2", vm.Lead);
        Assert.Contains("Sidebar: left", vm.Lead);

        await vm.CopyCommand.ExecuteAsync(null);
        Assert.Equal(vm.Lead, app.Clipboard.Text);

        vm.OpenLinkCommand.Execute(vm.Shortcuts[1]);
        Assert.Equal("https://claude.ai", Assert.Single(app.Launcher.Urls));
    }

    [Fact]
    public async Task Lead_Builder_Import_Navigates_To_Templates()
    {
        using var app = new TestApp();
        app.Picker.Open.Enqueue(null);
        await app.Root.Main.LeadBuilder.ImportHtmlCommand.ExecuteAsync(null);
        Assert.Same(app.Root.Main.Templates, app.Root.Main.CurrentPage);
    }

    [Fact]
    public async Task Cover_Letter_Needs_Profile_Then_Generates_Validates_And_Exports_Html()
    {
        using var app = new TestApp();
        var vm = app.Root.Main.CoverLetter;
        vm.GenerateCommand.Execute(null);
        Assert.False(vm.HasLead);

        app.Root.Main.Profiles.NewName = "CL";
        await app.Root.Main.Profiles.CreateAsync();
        app.Root.Main.Editor.FullName = "Cover Person";
        vm.Company = "Acme";
        vm.Position = "Engineer";
        vm.Keywords = "C#, Azure";
        vm.GenerateCommand.Execute(null);
        Assert.Contains("Acme", vm.Lead);
        Assert.Contains("Azure", vm.Lead);

        vm.Body = "";
        vm.SaveCommand.Execute(null);
        Assert.True(vm.HasError);

        app.Clipboard.Text = "Dear Acme team, I would love to join as an engineer.";
        await vm.PasteCommand.ExecuteAsync(null);
        Assert.True(vm.WordCount > 5);

        var target = Path.Combine(app.Dir, "letter.html");
        app.Picker.Save.Enqueue(target);
        await vm.ExportHtmlCommand.ExecuteAsync(null);
        Assert.False(vm.HasError);
        Assert.Contains("Dear Acme team", await File.ReadAllTextAsync(target));
    }
}

[Collection("Desktop")]
public class ExportViewModelTests
{
    private static ExportViewModel Create(TestApp app, Exception pdfError, out ProfileSession session, out StatusService status)
    {
        session = app.Root.Session;
        status = app.Root.Main.Status;
        var templates = new TemplateService(app.Infra.Templates, app.Infra.Engine);
        var letters = new CoverLetterService(app.Infra.Templates, app.Infra.Engine);
        var service = new ExportService(templates, letters, new ThrowingPdf(pdfError), new ThrowingPdf(pdfError));
        return new ExportViewModel(service, new ValidationService(), session, app.Picker, app.Launcher, status);
    }

    private static async Task<Profile> ValidProfileAsync(TestApp app)
    {
        app.Root.Main.Profiles.NewName = "Exp";
        await app.Root.Main.Profiles.CreateAsync();
        app.Root.Main.Editor.FullName = "Export Person";
        app.Root.Main.Editor.Email = "e@example.com";
        return app.Root.Session.Current!;
    }

    [Fact]
    public async Task Chrome_Missing_Shows_Friendly_Message_And_Link()
    {
        using var app = new TestApp();
        var vm = Create(app, new ChromeNotFoundException(), out _, out var status);
        await ValidProfileAsync(app);
        app.Picker.Save.Enqueue(Path.Combine(app.Dir, "x.pdf"));

        await vm.ExportAsync();

        Assert.True(vm.ChromeMissing);
        Assert.True(vm.HasError);
        Assert.Contains("Chrome", vm.Error);
        Assert.True(status.IsError);
        Assert.Equal("https://www.google.com/chrome", vm.ChromeUrl);
        vm.OpenChromeDownloadCommand.Execute(null);
        Assert.Equal("https://www.google.com/chrome", Assert.Single(app.Launcher.Urls));
    }

    [Fact]
    public async Task Other_Failures_Are_Reported_Without_Chrome_Link()
    {
        using var app = new TestApp();
        var vm = Create(app, new ExportFailedException("boom"), out _, out _);
        await ValidProfileAsync(app);
        app.Picker.Save.Enqueue(Path.Combine(app.Dir, "x.pdf"));

        await vm.ExportAsync();

        Assert.False(vm.ChromeMissing);
        Assert.Contains("boom", vm.Error);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Invalid_Profile_Is_Blocked_Before_The_Picker()
    {
        using var app = new TestApp();
        var vm = Create(app, new ExportFailedException("never"), out _, out _);
        app.Root.Main.Profiles.NewName = "No Name Set";
        await app.Root.Main.Profiles.CreateAsync(); // no full name / email -> validation errors

        await vm.ExportAsync();

        Assert.True(vm.HasError);
        Assert.Null(app.Picker.LastSuggestedName);
    }

    [Fact]
    public async Task Json_Export_Writes_File_And_Status()
    {
        using var app = new TestApp();
        var vm = Create(app, new ChromeNotFoundException(), out _, out var status);
        await ValidProfileAsync(app);
        vm.SelectedFormat = vm.FormatOptions.First(o => o.Value == "Json");
        var target = Path.Combine(app.Dir, "p.json");
        app.Picker.Save.Enqueue(target);

        await vm.ExportAsync();

        Assert.Equal("Exp.json", app.Picker.LastSuggestedName);
        Assert.Contains("Export Person", await File.ReadAllTextAsync(target));
        Assert.False(status.IsError);
        Assert.Contains(target, status.Message);
    }

    [Fact]
    public void Png_Dpi_Selector_Only_Enabled_For_Png()
    {
        using var app = new TestApp();
        var vm = Create(app, new ChromeNotFoundException(), out _, out _);
        Assert.False(vm.IsPng);
        vm.SelectedFormat = vm.FormatOptions.First(o => o.Value == "Png");
        Assert.True(vm.IsPng);
        Assert.Equal("300", vm.SelectedDpi.Value);
    }
}

[Collection("Desktop")]
public class SettingsAndInfrastructureTests
{
    [Fact]
    public async Task Settings_Apply_Theme_And_Language_And_Persist()
    {
        using var app = new TestApp();
        var vm = app.Root.Main.Settings;
        vm.SaveDelay = TimeSpan.Zero;
        await vm.LoadAsync();
        Assert.Equal("system", app.Theme.Applied);

        vm.SelectedTheme = vm.ThemeOptions.First(o => o.Value == "dark");
        vm.SelectedUiLanguage = LocalizedOption.Languages[0];
        vm.ChromePath = "  C:\\chrome.exe ";

        Assert.Equal("dark", app.Theme.Applied);
        Assert.Equal("fa", Localizer.Instance.Language);
        Assert.True(Localizer.Instance.IsRtl);
        Assert.Equal("پروفایل‌ها", app.Root.Main.NavItems[0].Label);

        var saved = await TestApp.Eventually(() =>
            app.Infra.Settings.LoadAsync().GetAwaiter().GetResult() is { ChromePath: "C:\\chrome.exe", Theme: "dark" } s ? s : null
        );
        Assert.Equal("fa", saved.UiLanguage);
    }

    [Fact]
    public async Task Default_Resume_Language_Flows_To_New_Profile_And_Lead_Forms()
    {
        using var app = new TestApp();
        var settings = app.Root.Main.Settings;
        settings.SaveDelay = TimeSpan.Zero;
        await settings.LoadAsync();
        settings.SelectedResumeLanguage = LocalizedOption.Languages[1];

        Assert.Equal("en", app.Root.Main.Profiles.NewLanguage.Value);
        Assert.Equal("en", app.Root.Main.LeadBuilder.SelectedLanguage.Value);
        Assert.Equal("en", app.Root.Main.CoverLetter.SelectedLanguage.Value);
    }

    [Fact]
    public async Task Backup_Tick_Snapshots_Current_Profile_And_Skips_When_Unchanged()
    {
        using var app = new TestApp();
        Assert.Null(await app.Root.Backup.TickAsync());

        app.Root.Main.Profiles.NewName = "Backup Me";
        await app.Root.Main.Profiles.CreateAsync();

        var path = await app.Root.Backup.TickAsync();
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Null(await app.Root.Backup.TickAsync());
        Assert.Equal(TimeSpan.FromMinutes(5), await app.Root.Backup.GetIntervalAsync());
    }

    [Fact]
    public async Task Session_Flush_Persists_Pending_Edits_Immediately()
    {
        using var app = new TestApp();
        app.Root.Session.SaveDelay = TimeSpan.FromMinutes(10);
        app.Root.Main.Profiles.NewName = "Flush";
        await app.Root.Main.Profiles.CreateAsync();
        app.Root.Main.Editor.Summary = "pending summary";

        await app.Root.Session.FlushAsync();

        var onDisk = await app.Infra.Profiles.GetAsync(app.Root.Session.Current!.Id);
        Assert.Equal("pending summary", onDisk!.Summary);
    }

    [Fact]
    public async Task Navigation_Items_Cover_All_Pages_And_Status_Clears()
    {
        using var app = new TestApp();
        var main = app.Root.Main;
        Assert.Equal(7, main.NavItems.Count);
        Assert.Equal(
            ["Profiles", "Editor", "Templates", "AI Template Builder", "Cover Letter", "Export", "Settings"],
            main.NavItems.Select(n => n.Label)
        );
        main.Status.ClearAfter = TimeSpan.FromMilliseconds(30);
        main.Status.Info("hello");
        Assert.True(main.Status.HasMessage);
        await TestApp.Eventually(() => main.Status.HasMessage ? null : "cleared");
    }
}
