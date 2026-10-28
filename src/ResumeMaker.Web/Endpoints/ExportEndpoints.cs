using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Web.Endpoints;

public static class ExportEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/export");
        g.MapPost("/pdf", (ExportBody body, ExportService svc, CancellationToken ct) => Export(ExportFormat.Pdf, body, svc, ct));
        g.MapPost("/png", (ExportBody body, ExportService svc, CancellationToken ct) => Export(ExportFormat.Png, body, svc, ct));
        g.MapPost("/html", (ExportBody body, ExportService svc, CancellationToken ct) => Export(ExportFormat.Html, body, svc, ct));
        g.MapPost("/json", (ExportBody body, ExportService svc, CancellationToken ct) => Export(ExportFormat.Json, body, svc, ct));
    }

    private static async Task<IResult> Export(ExportFormat format, ExportBody body, ExportService svc, CancellationToken ct)
    {
        var request = new ExportRequest(body.Profile, format, body.FileName ?? "resume", body.Dpi ?? 300);
        var result = await svc.ExportAsync(request, ct);
        return Results.File(result.Content, result.ContentType, result.FileName);
    }
}
