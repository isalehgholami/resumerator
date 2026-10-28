using System.Text;
using ResumeMaker.Core.Application.Dtos;

namespace ResumeMaker.Core.Application.Services;

public sealed record ShortcutLink(string Name, string Url);

/// <summary>Builds the multi-layer AI prompt for custom templates (FR-30..35, architecture Appendix A).</summary>
public sealed class LeadGenerator(LeadHistory? history = null)
{
    public const string HistoryKind = "template";

    public static IReadOnlyList<ShortcutLink> ShortcutLinks { get; } =
        [
            new("ChatGPT", "https://chatgpt.com"),
            new("Claude", "https://claude.ai"),
            new("Gemini", "https://gemini.google.com"),
        ];

    private static readonly string[] DefaultPriority =
    [
        "experience", "education", "skills", "languages", "projects", "awards", "interests",
    ];

    public string GenerateTemplateLead(TemplateLeadOptions options)
    {
        var rtl = options.Language == "fa";
        var sb = new StringBuilder();
        AppendSystemRole(sb);
        AppendBrief(sb, options, rtl);
        AppendSchema(sb, rtl);
        AppendModelVariables(sb);
        AppendConstraints(sb, rtl);
        AppendExample(sb);
        AppendOutputRequirements(sb, rtl);
        var prompt = sb.ToString();
        history?.Add(HistoryKind, prompt);
        return prompt;
    }

    private static void AppendSystemRole(StringBuilder sb) =>
        sb.AppendLine("# System Role")
            .AppendLine(
                "You are an expert HTML/CSS resume template designer who creates beautiful, minimal, ATS-friendly resume templates."
            )
            .AppendLine();

    private static void AppendBrief(StringBuilder sb, TemplateLeadOptions o, bool rtl)
    {
        var priority = o.SectionPriority.Count > 0 ? o.SectionPriority : [.. DefaultPriority];
        sb.AppendLine("# Project Brief")
            .AppendLine("Design a single-page resume template with the following specifications:")
            .AppendLine($"- Style: {o.Style}")
            .AppendLine($"- Primary color: {o.PrimaryColor}")
            .AppendLine($"- Accent color: {o.AccentColor}")
            .AppendLine($"- Columns: {o.ColumnCount}")
            .AppendLine($"- Sidebar: {o.SidebarPosition}")
            .AppendLine($"- Language: {o.Language} ({(rtl ? "RTL" : "LTR")})")
            .AppendLine($"- Text direction: {(rtl ? "rtl" : "ltr")}")
            .AppendLine($"- Section priority (most important first): {string.Join(" > ", priority)}")
            .AppendLine(
                rtl
                    ? "- Font: Vazirmatn (Persian) embedded as base64"
                    : "- Font: Inter embedded as base64"
            )
            .AppendLine();
    }

    private static void AppendSchema(StringBuilder sb, bool rtl)
    {
        sb.AppendLine("# JSON Schema (data we will inject; shown as `profile` in the template)")
            .AppendLine("```json")
            .AppendLine(
                rtl
                    ? """{"name":"مریم احمدی","language":"fa","contact":{"full_name":"مریم احمدی","title":"مهندس نرم‌افزار","email":"maryam@example.com","phone":"+98 912 345 6789","location":"تهران","links":{"linkedin":"https://linkedin.com/in/maryam","github":"https://github.com/maryam"}},"summary":"توسعه‌دهنده فول‌استک با 5 سال سابقه...","sections":[{"id":"a1b2c3d4","type":"experience","title":"تجربه کاری","items":[{"title":"توسعه‌دهنده ارشد","company":"شرکت X","start":"1401","end":"1403","description":"مسئول...","tags":["C#","SQL"]}]}]}"""
                    : """{"name":"Maryam Ahmadi","language":"en","contact":{"full_name":"Maryam Ahmadi","title":"Software Engineer","email":"maryam@example.com","phone":"+98 912 345 6789","location":"Tehran","links":{"linkedin":"https://linkedin.com/in/maryam","github":"https://github.com/maryam"}},"summary":"Full-stack developer with 5 years of experience...","sections":[{"id":"a1b2c3d4","type":"experience","title":"Experience","items":[{"title":"Senior Developer","company":"Company X","start":"2022","end":"2024","description":"Responsible for...","tags":["C#","SQL"]}]}]}"""
            )
            .AppendLine("```")
            .AppendLine(
                "Section types: experience, education, skills, languages, projects, awards, interests, custom. "
                    + "Item fields (subset per type): title company location start end description tags[] degree institution category entries[{name,level}] name level url issuer date."
            )
            .AppendLine();
    }

    private static void AppendModelVariables(StringBuilder sb) =>
        sb.AppendLine("# Available Template Variables")
            .AppendLine("- `profile.name`, `profile.summary`, `profile.language`")
            .AppendLine("- `profile.contact.full_name`, `.title`, `.email`, `.phone`, `.location`, `.links.linkedin`, `.links.github`, `.links.website`")
            .AppendLine("- `profile.sections` (only enabled sections, already sorted): each has `id`, `type`, `title`, `items`")
            .AppendLine("- `direction` (rtl|ltr), `lang` (fa|en)")
            .AppendLine("- `font_base64` (base64 woff2, body), `font_bold_base64` (bold), `font_family`")
            .AppendLine("- `primary_color`, `accent_color`")
            .AppendLine("- `labels.summary labels.experience labels.education labels.skills labels.languages labels.projects labels.awards labels.interests labels.contact labels.present labels.to`")
            .AppendLine("All strings are already HTML-escaped; do NOT add escape filters.")
            .AppendLine();

    private static void AppendConstraints(StringBuilder sb, bool rtl)
    {
        sb.AppendLine("# Constraints (CRITICAL)")
            .AppendLine("1. Output MUST be a single self-contained HTML file.")
            .AppendLine("2. ALL CSS MUST be inline (in one <style> tag, no external .css files, no external URLs).")
            .AppendLine("3. NO JavaScript allowed (no <script> tags, no inline event handlers).")
            .AppendLine("4. Use ONLY this Scriban subset: `{{ path.to.value }}`, `{{ if x }}...{{ else }}...{{ end }}`, `{{ if !x }}`, `{{ if a == \"literal\" }}` (also `!=`), `{{ for item in list }}...{{ end }}`, and the single filter `{{ value | digits }}` (Persian digits when lang is fa). Always write spaces inside braces.")
            .AppendLine("5. FORBIDDEN syntax: `&&`, `||`, `elsif`, `else if`, function calls, arithmetic, `{{~ ~}}`, comments, include, capture, while, forloop.*, and `{{#each}}` (that is Handlebars, not Scriban).")
            .AppendLine("6. Embed the font with @font-face using `src: url(data:font/woff2;base64,{{ font_base64 }}) format(\"woff2\");` (and `{{ font_bold_base64 }}` for bold).")
            .AppendLine($"7. Start with `<!DOCTYPE html>` and `<html dir=\"{{{{ direction }}}}\" lang=\"{{{{ lang }}}}\">` ({(rtl ? "RTL for Persian" : "LTR for English")}); use CSS logical properties and `html[dir=\"rtl\"]` / `html[dir=\"ltr\"]` selectors.")
            .AppendLine("8. Use semantic HTML5: <header>, <main>, <section>, <article>.")
            .AppendLine("9. Print-friendly: `@page { size: A4; margin: 18mm 20mm }`, no overflow, `break-inside: avoid` on entries.")
            .AppendLine("10. Maximum 2 colors (primary + accent) plus black/white; hierarchy through typography, generous white space.")
            .AppendLine("11. The template MUST contain `{{ profile.contact.full_name }}` and a `{{ for section in profile.sections }}` loop.")
            .AppendLine();
    }

    private static void AppendExample(StringBuilder sb) =>
        sb.AppendLine("# Example: Sections Loop in the Scriban subset")
            .AppendLine("```html")
            .AppendLine("{{ for section in profile.sections }}")
            .AppendLine("<section class=\"{{ section.type }}\">")
            .AppendLine("  <h2>{{ section.title }}</h2>")
            .AppendLine("  {{ for item in section.items }}")
            .AppendLine("  <article>")
            .AppendLine("    <h3>{{ item.title }}{{ if item.company }} — {{ item.company }}{{ end }}</h3>")
            .AppendLine("    {{ if item.start }}<span class=\"date\">{{ item.start | digits }} {{ labels.to }} {{ if item.end }}{{ item.end | digits }}{{ else }}{{ labels.present }}{{ end }}</span>{{ end }}")
            .AppendLine("    {{ if item.description }}<p>{{ item.description }}</p>{{ end }}")
            .AppendLine("  </article>")
            .AppendLine("  {{ end }}")
            .AppendLine("</section>")
            .AppendLine("{{ end }}")
            .AppendLine("```")
            .AppendLine();

    private static void AppendOutputRequirements(StringBuilder sb, bool rtl) =>
        sb.AppendLine("# Output Requirements")
            .AppendLine("- Return ONLY the HTML file content. No explanations, no markdown fences, no commentary.")
            .AppendLine("- Use ALL variables from the schema (contact, summary, every section type and item field).")
            .AppendLine("- Ensure visual hierarchy: section titles > entry titles > body text.")
            .AppendLine($"- Use only {(rtl ? "Vazirmatn" : "Inter")} (embedded) as font.");
}
