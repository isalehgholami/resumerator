using System.Text;
using System.Text.Json;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Application.Services;

public sealed class CoverLetterService(ITemplateRepository repository, ITemplateEngine engine, LeadHistory? history = null)
{
    public const string TemplateId = "cover-letter";
    public const string HistoryKind = "cover_letter";
    public const int MaxWords = 600;

    public string GenerateLead(Profile profile, CoverLetterOptions options)
    {
        var fa = options.Language == "fa";
        var sb = new StringBuilder();
        sb.AppendLine("# System Role")
            .AppendLine("You are an expert cover letter writer who creates concise, persuasive cover letters tailored to specific job postings.")
            .AppendLine()
            .AppendLine("# Project Brief")
            .AppendLine("Write a cover letter for the following:")
            .AppendLine($"- Target company: {options.Company}")
            .AppendLine($"- Target position: {options.Position}")
            .AppendLine($"- Tone: {options.Tone}")
            .AppendLine($"- Language: {(fa ? "Persian (fa)" : "English (en)")}")
            .AppendLine($"- Length: maximum one A4 page (about {(fa ? "350-450 words in Persian" : "300-400 words in English")})")
            .AppendLine()
            .AppendLine("# Candidate Profile (JSON)")
            .AppendLine(SummarizeProfile(profile))
            .AppendLine();
        AppendKeywords(sb, options);
        AppendStructure(sb);
        AppendConstraints(sb, options, fa);
        var prompt = sb.ToString();
        history?.Add(HistoryKind, prompt);
        return prompt;
    }

    private static void AppendKeywords(StringBuilder sb, CoverLetterOptions options)
    {
        sb.AppendLine("# Job Posting Keywords");
        foreach (var k in options.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            sb.AppendLine($"- {k.Trim()}");
        }
        sb.AppendLine();
    }

    private static void AppendStructure(StringBuilder sb) =>
        sb.AppendLine("# Structure (suggested)")
            .AppendLine("1. Opening paragraph: express interest, mention position source.")
            .AppendLine("2. Middle paragraph 1: highlight relevant experience.")
            .AppendLine("3. Middle paragraph 2: connect achievements to job requirements.")
            .AppendLine("4. Closing paragraph: call to action, contact info.")
            .AppendLine();

    private static void AppendConstraints(StringBuilder sb, CoverLetterOptions options, bool fa) =>
        sb.AppendLine("# Constraints")
            .AppendLine("- Output ONLY the cover letter text (no commentary, no markdown). Separate paragraphs with a blank line.")
            .AppendLine($"- {ToneInstruction(options.Tone, fa)}")
            .AppendLine("- Keep sentences short and clear.")
            .AppendLine("- No buzzwords without context.")
            .AppendLine("- Reference specific achievements from the profile.");

    public static string ToneInstruction(string tone, bool fa) =>
        (tone, fa) switch
        {
            ("friendly", true) => "Use a warm, friendly Persian tone (ما، من، با سلام و احترام) while staying professional.",
            ("bold", true) => "Use a confident, direct Persian tone with strong action verbs (رهبری کردم، ساختم، افزایش دادم).",
            ("friendly", false) => "Use a warm, friendly English tone (I'm excited, I'd love to) while staying professional.",
            ("bold", false) => "Use a confident, direct English tone with strong action verbs (led, built, increased).",
            (_, true) => "Use formal Persian tone (شما، جناب، کارشناس).",
            _ => "Use formal English tone (Dear Hiring Manager, I am writing to, Sincerely).",
        };

    /// <summary>Compact profile JSON (enabled sections only, null/empty values omitted).</summary>
    public static string SummarizeProfile(Profile profile)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            WriteIfAny(w, "name", profile.Contact.FullName);
            WriteIfAny(w, "title", profile.Contact.Title);
            WriteIfAny(w, "location", profile.Contact.Location);
            WriteIfAny(w, "summary", profile.Summary);
            w.WriteStartArray("sections");
            foreach (var s in profile.Sections.Where(s => s.Enabled).OrderBy(s => s.Order))
            {
                WriteSection(w, s);
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSection(Utf8JsonWriter w, Section s)
    {
        w.WriteStartObject();
        w.WriteString("type", s.Type.ToString().ToLowerInvariant());
        w.WriteString("title", s.Title);
        w.WriteStartArray("items");
        foreach (var i in s.Items)
        {
            w.WriteStartObject();
            WriteIfAny(w, "title", i.Title ?? i.Name ?? i.Degree);
            WriteIfAny(w, "company", i.Company ?? i.Institution);
            WriteIfAny(w, "start", i.Start);
            WriteIfAny(w, "end", i.End);
            WriteIfAny(w, "description", i.Description);
            if (i.Tags is { Count: > 0 })
            {
                w.WriteString("tags", string.Join(", ", i.Tags));
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteIfAny(Utf8JsonWriter w, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            w.WriteString(name, value);
        }
    }

    public async Task<string> RenderAsync(Profile profile, CoverLetter letter, CancellationToken ct = default)
    {
        var template =
            await repository.GetByIdAsync(TemplateId, ct).ConfigureAwait(false)
            ?? throw new TemplateNotFoundException(TemplateId);
        return await engine.RenderCoverLetterAsync(template.HtmlContent, profile, letter, ct).ConfigureAwait(false);
    }

    public static int CountWords(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Plain-text validation: empty body is an error, more than ~600 words is a warning.</summary>
    public static ValidationResult Validate(CoverLetter letter)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(letter.Body))
        {
            issues.Add(new("body_empty", "Cover letter text must not be empty.", true));
        }
        else if (CountWords(letter.Body) > MaxWords)
        {
            issues.Add(new("body_too_long", $"Cover letter exceeds about {MaxWords} words and may not fit one A4 page.", false));
        }
        return new ValidationResult(issues);
    }
}
