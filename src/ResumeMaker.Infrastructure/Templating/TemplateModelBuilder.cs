using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ResumeMaker.Core;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Domain;
using Scriban.Runtime;

namespace ResumeMaker.Infrastructure.Templating;

/// <summary>
/// Builds the Scriban model described in templates/SYNTAX.md without reflection:
/// Profile -> JsonNode (source-generated) -> ScriptObject/ScriptArray with escaped strings.
/// </summary>
public static partial class TemplateModelBuilder
{
    public const string DefaultPrimaryColor = "#1a73e8";
    public const string DefaultAccentColor = "#ff6b6b";

    public static ScriptObject Build(
        Profile profile,
        string fontBase64,
        string fontBoldBase64,
        CoverLetter? letter = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var lang = profile.Language == "fa" ? "fa" : "en";

        var node = JsonSerializer.SerializeToNode(profile, ResumeMakerJsonContext.Default.Profile) as JsonObject
            ?? new JsonObject();

        // Only enabled sections, sorted by order (stable).
        var sections = new List<(int Order, int Index, JsonObject Section)>();
        if (node["sections"] is JsonArray all)
        {
            var i = 0;
            foreach (var s in all)
            {
                if (s is JsonObject so && (so["enabled"]?.GetValue<bool>() ?? true))
                {
                    var order = so["order"] is JsonValue ov && ov.TryGetValue<int>(out var o) ? o : 0;
                    sections.Add((order, i, so));
                }
                i++;
            }
        }

        var root = new ScriptObject();
        var prof = new ScriptObject();
        foreach (var (key, value) in node)
        {
            if (key is "sections" or "theme" or "created_at" or "updated_at")
                continue;
            var converted = Convert(value, key);
            if (converted is not null)
                prof[key] = converted;
        }

        var sectionArray = new ScriptArray();
        foreach (var (_, _, section) in sections.OrderBy(t => t.Order).ThenBy(t => t.Index))
        {
            var so = new ScriptObject();
            foreach (var (key, value) in section)
            {
                if (key is "enabled" or "order")
                    continue;
                var converted = Convert(value, key);
                if (key == "type" && converted is string t)
                    converted = t.ToLowerInvariant();
                if (converted is not null)
                    so[key] = converted;
            }
            sectionArray.Add(so);
        }
        prof["sections"] = sectionArray;
        root["profile"] = prof;

        var theme = profile.Theme ?? new ThemeOverrides();
        root["direction"] = lang == "fa" ? "rtl" : "ltr";
        root["lang"] = lang;
        root["font_base64"] = fontBase64;
        root["font_bold_base64"] = fontBoldBase64;
        root["font_family"] = SafeFontFamily(theme.FontFamily) ?? (lang == "fa" ? "Vazirmatn" : "Inter");
        root["primary_color"] = SafeColor(theme.PrimaryColor) ?? DefaultPrimaryColor;
        root["accent_color"] = SafeColor(theme.AccentColor) ?? DefaultAccentColor;
        root["labels"] = BuildLabels(lang);

        if (letter is not null)
        {
            var l = new ScriptObject
            {
                ["company"] = Escape(letter.Company),
                ["position"] = Escape(letter.Position),
                ["body"] = Escape(letter.Body),
            };
            root["letter"] = l;
            var paragraphs = new ScriptArray();
            foreach (var p in ParagraphSplit().Split(letter.Body ?? ""))
            {
                var trimmed = p.Trim();
                if (trimmed.Length > 0)
                    paragraphs.Add(Escape(trimmed));
            }
            root["paragraphs"] = paragraphs;
        }

        return root;
    }

    public static ScriptObject BuildLabels(string lang)
    {
        var fa = lang == "fa";
        var labels = new ScriptObject();
        void Set(string key, string faText, string enText) => labels[key] = fa ? faText : enText;
        Set("summary", "خلاصه", "Summary");
        Set("experience", "سوابق کاری", "Experience");
        Set("education", "تحصیلات", "Education");
        Set("skills", "مهارت‌ها", "Skills");
        Set("languages", "زبان‌ها", "Languages");
        Set("projects", "پروژه‌ها", "Projects");
        Set("awards", "جوایز و افتخارات", "Awards");
        Set("interests", "علایق", "Interests");
        Set("contact", "اطلاعات تماس", "Contact");
        Set("present", "اکنون", "Present");
        Set("to", "تا", "to");
        Set("cover_letter", "نامه همراه", "Cover Letter");
        Set("dear", "با سلام و احترام", "Dear Hiring Manager,");
        Set("sincerely", "با احترام", "Sincerely,");
        return labels;
    }

    public static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        StringBuilder? sb = null;
        for (var i = 0; i < s.Length; i++)
        {
            var rep = s[i] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => null,
            };
            if (rep is null)
            {
                sb?.Append(s[i]);
                continue;
            }
            sb ??= new StringBuilder(s.Length + 16).Append(s, 0, i);
            sb.Append(rep);
        }
        return sb?.ToString() ?? s;
    }

    /// <summary>Converts a JSON node; returns null for null/empty values so they are omitted.</summary>
    private static object? Convert(JsonNode? node, string key)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue v:
                if (v.TryGetValue<string>(out var s))
                    return string.IsNullOrEmpty(s) ? null : Escape(s);
                if (v.TryGetValue<bool>(out var b))
                    return b ? true : null;
                if (v.TryGetValue<long>(out var l))
                    return l;
                if (v.TryGetValue<double>(out var d))
                    return d;
                var text = v.ToString();
                return string.IsNullOrEmpty(text) ? null : Escape(text);
            case JsonArray arr:
                var list = new ScriptArray();
                foreach (var item in arr)
                {
                    var c = Convert(item, key);
                    if (c is not null)
                        list.Add(c);
                }
                return list.Count == 0 ? null : list;
            case JsonObject obj:
                var so = new ScriptObject();
                foreach (var (k, value) in obj)
                {
                    var c = Convert(value, k);
                    if (c is not null)
                        so[k] = c;
                }
                return so.Count == 0 ? null : so;
            default:
                return null;
        }
    }

    private static string? SafeColor(string? c) =>
        c is not null && ColorRegex().IsMatch(c.Trim()) ? c.Trim() : null;

    private static string? SafeFontFamily(string? f) =>
        f is not null && FontFamilyRegex().IsMatch(f.Trim()) ? f.Trim() : null;

    [GeneratedRegex(@"^#[0-9a-fA-F]{3,8}$")]
    private static partial Regex ColorRegex();

    [GeneratedRegex(@"^[\p{L}\p{N} _-]{1,64}$")]
    private static partial Regex FontFamilyRegex();

    [GeneratedRegex(@"\r?\n[ \t]*\r?\n")]
    private static partial Regex ParagraphSplit();
}
