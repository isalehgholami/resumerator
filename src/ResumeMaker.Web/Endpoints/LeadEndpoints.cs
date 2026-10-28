using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Services;

namespace ResumeMaker.Web.Endpoints;

public static class LeadEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/lead");

        g.MapPost("/template", (TemplateLeadOptions options, LeadGenerator lead) =>
            Results.Json(new PromptBody(lead.GenerateTemplateLead(options with { SectionPriority = options.SectionPriority ?? [] })), WebJsonContext.Default.PromptBody));

        g.MapPost("/cover-letter", (CoverLetterLeadBody body, CoverLetterService svc) =>
            Results.Json(
                new PromptBody(svc.GenerateLead(body.Profile, (body.Options ?? new CoverLetterOptions()) with { Keywords = body.Options?.Keywords ?? [] })),
                WebJsonContext.Default.PromptBody));

        g.MapGet("/shortcuts", () =>
            Results.Json(
                LeadGenerator.ShortcutLinks.Select(s => new ShortcutBody(s.Name, s.Url)).ToList(),
                WebJsonContext.Default.ListShortcutBody));
    }
}
