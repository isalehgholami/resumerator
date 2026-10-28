using ResumeMaker.Core;
using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure;

namespace ResumeMaker.Web.Endpoints;

public static class SettingsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/settings", async (InfrastructureServices infra, CancellationToken ct) =>
            Results.Json(await infra.Settings.LoadAsync(ct), ResumeMakerJsonContext.Default.AppSettings));

        api.MapPut("/settings", async (AppSettings body, InfrastructureServices infra, CancellationToken ct) =>
        {
            await infra.Settings.SaveAsync(body, ct);
            return Results.Json(await infra.Settings.LoadAsync(ct), ResumeMakerJsonContext.Default.AppSettings);
        });
    }
}
