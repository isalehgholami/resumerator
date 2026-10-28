using ResumeMaker.Core.Application.Services;
using ResumeMaker.Infrastructure;

namespace ResumeMaker.Web;

/// <summary>Manually composed service graph (no reflection-based DI).</summary>
public sealed class AppServices
{
    public AppServices(InfrastructureServices infra)
    {
        Infra = infra;
        var history = new LeadHistory();
        Profiles = new ProfileService(infra.Profiles, infra.Profiles);
        Templates = new TemplateService(infra.Templates, infra.Engine);
        Lead = new LeadGenerator(history);
        CoverLetters = new CoverLetterService(infra.Templates, infra.Engine, history);
        Export = new ExportService(Templates, CoverLetters, infra.Chrome, infra.Chrome);
        Validation = new ValidationService();
    }

    public InfrastructureServices Infra { get; }
    public ProfileService Profiles { get; }
    public TemplateService Templates { get; }
    public LeadGenerator Lead { get; }
    public CoverLetterService CoverLetters { get; }
    public ExportService Export { get; }
    public ValidationService Validation { get; }
}
