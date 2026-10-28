using ResumeMaker.Core;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure;

namespace ResumeMaker.Web.Endpoints;

public static class TemplateEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/templates");

        g.MapGet("/", async (InfrastructureServices infra, CancellationToken ct) =>
            Results.Json((await infra.Templates.ListAsync(ct)).ToList(), ResumeMakerJsonContext.Default.ListTemplate));

        g.MapPost("/custom", async (CustomTemplateBody body, InfrastructureServices infra, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Html))
            {
                return Results.Json(
                    new ErrorBody("name and html are required."),
                    WebJsonContext.Default.ErrorBody,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var validation = infra.Validator.Validate(body.Html);
            if (!validation.IsValid)
            {
                var issues = validation.Issues.Select(i => new IssueBody(i.Code, i.Message, i.IsError)).ToList();
                return Results.Json(
                    new ErrorBody("Template validation failed.", issues),
                    WebJsonContext.Default.ErrorBody,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var template = new Template
            {
                Id = "custom-" + Guid.NewGuid().ToString("N")[..12],
                Name = body.Name.Trim(),
                Style = TemplateStyle.Custom,
                IsBuiltIn = false,
                HtmlContent = infra.Validator.Sanitize(body.Html),
                CreatedAt = DateTimeOffset.UtcNow,
                SourceLead = body.SourceLead,
            };
            await infra.Templates.SaveCustomAsync(template, ct);
            return Results.Json(template, ResumeMakerJsonContext.Default.Template, statusCode: StatusCodes.Status201Created);
        });

        g.MapDelete("/custom/{id}", async (string id, InfrastructureServices infra, CancellationToken ct) =>
            await infra.Templates.DeleteCustomAsync(id, ct)
                ? Results.NoContent()
                : ProfileEndpoints.NotFound($"Custom template '{id}' was not found."));
    }
}
