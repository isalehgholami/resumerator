using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;

namespace ResumeMaker.Infrastructure.Templating;

/// <summary>Validates and sanitizes custom (possibly AI-generated) templates. Tolerates Scriban tags.</summary>
public sealed partial class TemplateValidator : ITemplateValidator
{
    private static readonly string[] ForbiddenEmbeds = ["iframe", "object", "embed"];

    public ValidationResult Validate(string html)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(html))
        {
            issues.Add(new("empty", "Template is empty.", true));
            return new ValidationResult(issues);
        }

        if (!FullNameRegex().IsMatch(html))
            issues.Add(new("missing-full-name", "Template must output {{ profile.contact.full_name }}.", true));
        if (!SectionsLoopRegex().IsMatch(html))
            issues.Add(new("missing-sections-loop", "Template must contain {{ for section in profile.sections }} ... {{ end }}.", true));

        var doc = Parse(html);

        if (doc.QuerySelectorAll("script").Length > 0)
            issues.Add(new("script-tag", "<script> elements are not allowed.", true));
        foreach (var tag in ForbiddenEmbeds)
        {
            if (doc.QuerySelectorAll(tag).Length > 0)
                issues.Add(new($"{tag}-tag", $"<{tag}> elements are not allowed.", true));
        }

        var externalReported = false;
        void External(string what)
        {
            if (externalReported)
                return;
            externalReported = true;
            issues.Add(new("external-url", $"External resources are not allowed ({what}).", true));
        }

        foreach (var link in doc.QuerySelectorAll("link"))
        {
            if (IsExternal(link.GetAttribute("href")))
                External("<link href>");
        }
        foreach (var el in doc.QuerySelectorAll("[src]"))
        {
            if (IsExternal(el.GetAttribute("src")))
                External($"<{el.LocalName} src>");
        }
        var styleBlocks = doc.QuerySelectorAll("style").Select(s => s.TextContent).ToList();
        styleBlocks.AddRange(doc.QuerySelectorAll("[style]").Select(e => e.GetAttribute("style") ?? ""));
        foreach (var css in styleBlocks)
        {
            if (ExternalCssUrlRegex().IsMatch(css) || ExternalImportRegex().IsMatch(css))
                External("CSS url()/@import");
        }

        var handlerFound = doc.QuerySelectorAll("*").Any(e => e.Attributes.Any(a => IsEventHandler(a.Name)));
        if (handlerFound)
            issues.Add(new("event-handler", "Inline event handler attributes (on*) are not allowed.", true));

        if (!styleBlocks.Any(s => s.Contains("@font-face", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new("missing-font-face", "Template must embed its font with @font-face.", true));

        if (doc.QuerySelector("header") is null)
            issues.Add(new("missing-header", "Template has no <header> element.", false));
        if (doc.QuerySelector("main") is null)
            issues.Add(new("missing-main", "Template has no <main> element.", false));
        if (doc.QuerySelector("section") is null)
            issues.Add(new("missing-section", "Template has no <section> element.", false));

        return new ValidationResult(issues);
    }

    public string Sanitize(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";
        var doc = Parse(html);

        foreach (var el in doc.QuerySelectorAll("script, iframe, object, embed").ToList())
            el.Remove();
        foreach (var link in doc.QuerySelectorAll("link").ToList())
        {
            if (IsExternal(link.GetAttribute("href")))
                link.Remove();
        }
        foreach (var el in doc.QuerySelectorAll("*"))
        {
            foreach (var attr in el.Attributes.Where(a => IsEventHandler(a.Name)).ToList())
                el.RemoveAttribute(attr.Name);
        }
        foreach (var style in doc.QuerySelectorAll("style"))
            style.TextContent = ExternalImportStatementRegex().Replace(style.TextContent, "");

        return "<!DOCTYPE html>" + doc.DocumentElement.OuterHtml;
    }

    private static IDocument Parse(string html) => new HtmlParser().ParseDocument(html);

    private static bool IsEventHandler(string name) => name.StartsWith("on", StringComparison.OrdinalIgnoreCase);

    private static bool IsExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        var u = url.Trim();
        return u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || u.StartsWith("//", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\{\{\s*profile\.contact\.full_name\s*(\|\s*\w+\s*)?\}\}")]
    private static partial Regex FullNameRegex();

    [GeneratedRegex(@"\{\{\s*for\s+\w+\s+in\s+profile\.sections\s*\}\}")]
    private static partial Regex SectionsLoopRegex();

    [GeneratedRegex(@"url\(\s*['""]?\s*(https?:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalCssUrlRegex();

    [GeneratedRegex(@"@import\s+(url\(\s*)?['""]?\s*(https?:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalImportRegex();

    [GeneratedRegex(@"@import\s+(url\(\s*)?['""]?\s*(https?:)?//[^;]*;?", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalImportStatementRegex();
}
