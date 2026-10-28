namespace ResumeMaker.Web.Endpoints;

public static class HealthEndpoints
{
    public static void Map(RouteGroupBuilder api) =>
        api.MapGet("/health", () => Results.Json(new HealthBody("ok", "1.0.0"), WebJsonContext.Default.HealthBody));
}
