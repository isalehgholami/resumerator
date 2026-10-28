using System.Text.Json;
using ResumeMaker.Core.Application;
using ResumeMaker.Core.Application.Interfaces;

namespace ResumeMaker.Web;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string Csp =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; "
        + "font-src 'self' data:; connect-src 'self'; frame-src 'self' blob: data:";

    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["Content-Security-Policy"] = Csp;
        h["X-Content-Type-Options"] = "nosniff";
        h["Referrer-Policy"] = "no-referrer";
        return next(context);
    }
}

/// <summary>Maps domain exceptions to JSON error responses.</summary>
public sealed class ErrorMappingMiddleware(RequestDelegate next, ILogger<ErrorMappingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, message) = ex switch
            {
                ChromeNotFoundException => (StatusCodes.Status503ServiceUnavailable, ex.Message),
                TemplateNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                InvalidProfileException => (StatusCodes.Status400BadRequest, ex.Message),
                BadHttpRequestException or JsonException => (StatusCodes.Status400BadRequest, ex.Message),
                ExportFailedException => (StatusCodes.Status500InternalServerError, ex.Message),
                NotSupportedException => (StatusCodes.Status400BadRequest, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "Internal server error."),
            };
            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled error");
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ErrorBody(message), WebJsonContext.Default.ErrorBody);
        }
    }
}
