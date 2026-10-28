using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Services;

namespace ResumeMaker.Web.Endpoints;

public static class RenderEndpoints
{
    public static void Map(RouteGroupBuilder api) =>
        api.MapPost("/render", async (RenderBody body, TemplateService templates, CancellationToken ct) =>
        {
            var html = await templates.RenderAsync(new TemplateRenderRequest(body.Profile, body.TemplateId), ct);
            return Results.Content(html, "text/html; charset=utf-8");
        });
}
