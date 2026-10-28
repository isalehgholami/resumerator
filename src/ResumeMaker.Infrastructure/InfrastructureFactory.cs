using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Infrastructure.Export;
using ResumeMaker.Infrastructure.Fonts;
using ResumeMaker.Infrastructure.Storage;
using ResumeMaker.Infrastructure.Templating;

namespace ResumeMaker.Infrastructure;

/// <summary>All Infrastructure services wired together (no DI container, AOT friendly).</summary>
public sealed record InfrastructureServices(
    AppPaths Paths,
    JsonProfileRepository Profiles,
    ISettingsStore Settings,
    IBackupService Backup,
    ITemplateRepository Templates,
    ITemplateEngine Engine,
    ITemplateValidator Validator,
    IFontResourceProvider Fonts,
    ChromeHeadlessExporter Chrome
);

public static class InfrastructureFactory
{
    public static InfrastructureServices Create(AppPaths? paths = null, IProcessRunner? processRunner = null)
    {
        paths ??= AppPaths.Default();
        var settings = new JsonSettingsStore(paths);
        var fonts = new EmbeddedFontProvider();
        return new InfrastructureServices(
            paths,
            new JsonProfileRepository(paths),
            settings,
            new FileBackupService(paths, settings),
            new TemplateRepository(paths),
            new ScribanTemplateEngine(fonts),
            new TemplateValidator(),
            fonts,
            new ChromeHeadlessExporter(settings, processRunner ?? new SystemProcessRunner())
        );
    }
}
