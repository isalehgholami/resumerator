using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;

namespace ResumeMaker.Core.Application.Services;

public sealed class TemplateService(ITemplateRepository repository, ITemplateEngine engine)
{
    public const string AtsTemplateId = "ats";

    public static string ResolveTemplateId(TemplateRenderRequest request) =>
        request.TemplateId ?? (request.Profile.AtsMode ? AtsTemplateId : request.Profile.TemplateId);

    public async Task<string> RenderAsync(TemplateRenderRequest request, CancellationToken ct = default)
    {
        var id = ResolveTemplateId(request);
        var template =
            await repository.GetByIdAsync(id, ct).ConfigureAwait(false) ?? throw new TemplateNotFoundException(id);
        return await engine.RenderAsync(template.HtmlContent, request.Profile, ct).ConfigureAwait(false);
    }
}
