using System.Text;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Common;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Application.Services;

public sealed class ExportService(
    TemplateService templates,
    CoverLetterService coverLetters,
    IPdfExporter pdf,
    IPngExporter? png = null
)
{
    private const string HtmlType = "text/html";
    private const string JsonType = "application/json";
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public async Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken ct = default)
    {
        var name = ProfileFileName.Sanitize(request.FileName);
        if (request.Format == ExportFormat.Json)
        {
            return new(ProfileService.ExportJsonBytes(request.Profile), JsonType, name + ".json");
        }
        var html = await templates.RenderAsync(new TemplateRenderRequest(request.Profile), ct).ConfigureAwait(false);
        return request.Format switch
        {
            ExportFormat.Html => new(Utf8NoBom.GetBytes(html), HtmlType, name + ".html"),
            ExportFormat.Pdf => await pdf.ExportPdfAsync(html, name, ct).ConfigureAwait(false),
            ExportFormat.Png => await ExportPngAsync(html, name, request.Dpi, ct).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Unsupported export format '{request.Format}'."),
        };
    }

    public async Task<ExportResult> ExportCoverLetterAsync(
        Profile profile,
        CoverLetter letter,
        ExportFormat format,
        string fileName,
        CancellationToken ct = default
    )
    {
        var name = ProfileFileName.Sanitize(fileName, "cover-letter");
        var html = await coverLetters.RenderAsync(profile, letter, ct).ConfigureAwait(false);
        return format switch
        {
            ExportFormat.Html => new(Utf8NoBom.GetBytes(html), HtmlType, name + ".html"),
            ExportFormat.Pdf => await pdf.ExportPdfAsync(html, name, ct).ConfigureAwait(false),
            _ => throw new NotSupportedException("Cover letters can be exported as Pdf or Html only."),
        };
    }

    private Task<ExportResult> ExportPngAsync(string html, string name, int dpi, CancellationToken ct) =>
        png is null
            ? throw new ExportFailedException("PNG export is not available.")
            : png.ExportPngAsync(html, name, dpi, ct);
}
