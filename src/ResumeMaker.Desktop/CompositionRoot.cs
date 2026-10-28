using ResumeMaker.Core.Application.Services;
using ResumeMaker.Desktop.Services;
using ResumeMaker.Desktop.ViewModels;
using ResumeMaker.Infrastructure;

namespace ResumeMaker.Desktop;

/// <summary>Manual composition (no DI container, no reflection) so the app stays Native AOT friendly.</summary>
public sealed class CompositionRoot : IDisposable
{
    private CompositionRoot(
        InfrastructureServices infrastructure,
        ProfileSession session,
        PreviewService preview,
        BackupCoordinator backup,
        MainViewModel main
    )
    {
        Infrastructure = infrastructure;
        Session = session;
        Preview = preview;
        Backup = backup;
        Main = main;
    }

    public InfrastructureServices Infrastructure { get; }
    public ProfileSession Session { get; }
    public PreviewService Preview { get; }
    public BackupCoordinator Backup { get; }
    public MainViewModel Main { get; }

    public static CompositionRoot Create(
        InfrastructureServices infrastructure,
        IClipboardService clipboard,
        IFilePicker picker,
        ILauncher launcher,
        IThemeService theme,
        string? previewDirectory = null
    )
    {
        var status = new StatusService();
        var history = new LeadHistory();
        var profileService = new ProfileService(infrastructure.Profiles, infrastructure.Profiles);
        var templateService = new TemplateService(infrastructure.Templates, infrastructure.Engine);
        var leadGenerator = new LeadGenerator(history);
        var coverLetterService = new CoverLetterService(infrastructure.Templates, infrastructure.Engine, history);
        var exportService = new ExportService(templateService, coverLetterService, infrastructure.Chrome, infrastructure.Chrome);
        var validation = new ValidationService();

        var session = new ProfileSession(profileService, status);
        var preview = new PreviewService(session, templateService, launcher, status, previewDirectory);
        var backup = new BackupCoordinator(session, infrastructure.Backup, infrastructure.Settings);

        var profiles = new ProfilesViewModel(profileService, session, picker, status);
        var editor = new EditorViewModel(session, preview, validation);
        var templates = new TemplatesViewModel(infrastructure.Templates, infrastructure.Validator, session, picker, history, status);
        var settings = new SettingsViewModel(infrastructure.Settings, theme, status);
        var export = new ExportViewModel(exportService, validation, session, picker, launcher, status);
        var coverLetter = new CoverLetterViewModel(coverLetterService, exportService, validation, session, clipboard, picker, launcher, status);

        MainViewModel? main = null;
        var leadBuilder = new LeadBuilderViewModel(leadGenerator, clipboard, launcher, status, templates, () => main!.Navigate(templates));
        main = new MainViewModel(status, session, profiles, editor, templates, leadBuilder, coverLetter, export, settings);

        settings.DefaultResumeLanguageChanged += lang =>
        {
            profiles.SetDefaultLanguage(lang);
            leadBuilder.SetDefaultLanguage(lang);
            coverLetter.SetDefaultLanguage(lang);
        };
        return new CompositionRoot(infrastructure, session, preview, backup, main);
    }

    public void Dispose() => Preview.Dispose();
}
