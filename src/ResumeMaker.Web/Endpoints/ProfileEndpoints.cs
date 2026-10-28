using ResumeMaker.Core;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Common;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Web.Endpoints;

public static class ProfileEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/profiles");

        g.MapGet("/", async (ProfileService svc, CancellationToken ct) =>
            Results.Json((await svc.ListAsync(ct)).ToList(), ResumeMakerJsonContext.Default.ListProfileSummary));

        g.MapPost("/", async (CreateProfileBody body, ProfileService svc, CancellationToken ct) =>
        {
            var profile = body.Profile is not null
                ? await svc.SaveAsync(body.Profile with { Id = Guid.NewGuid() }, ct)
                : await svc.CreateAsync(
                    string.IsNullOrWhiteSpace(body.Name) ? "Untitled" : body.Name!,
                    body.Language ?? "fa",
                    ct);
            return Results.Json(profile, ResumeMakerJsonContext.Default.Profile, statusCode: StatusCodes.Status201Created);
        });

        // Raw profile JSON import (as produced by /export).
        g.MapPost("/import", async (HttpRequest request, ProfileService svc, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync(ct);
            var imported = ProfileService.ImportJson(json) with { Id = Guid.NewGuid() };
            var saved = await svc.SaveAsync(imported, ct);
            return Results.Json(saved, ResumeMakerJsonContext.Default.Profile, statusCode: StatusCodes.Status201Created);
        });

        g.MapGet("/{id:guid}", async (Guid id, ProfileService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } p
                ? Results.Json(p, ResumeMakerJsonContext.Default.Profile)
                : NotFound($"Profile '{id}' was not found."));

        g.MapPut("/{id:guid}", async (Guid id, Profile body, ProfileService svc, CancellationToken ct) =>
        {
            if (await svc.GetAsync(id, ct) is null)
                return NotFound($"Profile '{id}' was not found.");
            var saved = await svc.SaveAsync(body with { Id = id }, ct);
            return Results.Json(saved, ResumeMakerJsonContext.Default.Profile);
        });

        g.MapDelete("/{id:guid}", async (Guid id, ProfileService svc, CancellationToken ct) =>
            await svc.DeleteAsync(id, ct) ? Results.NoContent() : NotFound($"Profile '{id}' was not found."));

        g.MapPost("/{id:guid}/duplicate", async (Guid id, ProfileService svc, CancellationToken ct) =>
        {
            if (await svc.GetAsync(id, ct) is null)
                return NotFound($"Profile '{id}' was not found.");
            var copy = await svc.DuplicateAsync(id, null, ct);
            return Results.Json(copy, ResumeMakerJsonContext.Default.Profile, statusCode: StatusCodes.Status201Created);
        });

        g.MapGet("/{id:guid}/export", async (Guid id, ProfileService svc, CancellationToken ct) =>
        {
            if (await svc.GetAsync(id, ct) is not { } p)
                return NotFound($"Profile '{id}' was not found.");
            return Results.File(
                ProfileService.ExportJsonBytes(p),
                "application/json",
                ProfileFileName.Sanitize(p.Name) + ".json");
        });
    }

    internal static IResult NotFound(string message) =>
        Results.Json(new ErrorBody(message), WebJsonContext.Default.ErrorBody, statusCode: StatusCodes.Status404NotFound);
}
