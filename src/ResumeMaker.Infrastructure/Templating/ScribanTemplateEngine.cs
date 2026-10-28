using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;
using ScribanTemplate = Scriban.Template;

namespace ResumeMaker.Infrastructure.Templating;

/// <summary>Renders the Scriban subset described in templates/SYNTAX.md. No reflection, no file access.</summary>
public sealed class ScribanTemplateEngine(IFontResourceProvider fonts) : ITemplateEngine
{
    private const int MaxCacheEntries = 64;
    private const int MaxTemplateChars = 2_000_000;
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, ScribanTemplate> _cache = new();

    public Task<string> RenderAsync(string templateHtml, Profile profile, CancellationToken ct = default) =>
        RenderCoreAsync(templateHtml, profile, null, ct);

    public Task<string> RenderCoverLetterAsync(
        string templateHtml,
        Profile profile,
        CoverLetter letter,
        CancellationToken ct = default
    ) => RenderCoreAsync(templateHtml, profile, letter ?? throw new ArgumentNullException(nameof(letter)), ct);

    private async Task<string> RenderCoreAsync(
        string templateHtml,
        Profile profile,
        CoverLetter? letter,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(templateHtml);
        ArgumentNullException.ThrowIfNull(profile);
        if (templateHtml.Length > MaxTemplateChars)
            throw new TemplateRenderException("Template is too large.");

        var template = GetOrParse(templateHtml);

        var lang = profile.Language == "fa" ? "fa" : "en";
        var regular = await fonts.GetFontBase64Async(lang, false, ct).ConfigureAwait(false);
        var bold = await fonts.GetFontBase64Async(lang, true, ct).ConfigureAwait(false);
        var model = TemplateModelBuilder.Build(profile, regular, bold, letter);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RenderTimeout);

        var globals = new ScriptObject();
        globals["digits"] = new DigitsFunction(lang == "fa");
        foreach (var kv in model)
            globals[kv.Key] = kv.Value;

        var context = new TemplateContext
        {
            TemplateLoader = null, // includes are not available: no file access
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            OutputLimit = 50_000_000,
            StrictVariables = false,
            LoopLimit = 10_000,
            RecursiveLimit = 50,
            LimitToString = 4_000_000,
            ObjectRecursionLimit = 20,
            CancellationToken = timeout.Token,
            EnableBreakAndContinueAsReturnOutsideLoop = false,
        };
        context.PushGlobal(globals);

        try
        {
            return template.Render(context);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TemplateRenderException("Template rendering exceeded the time limit.");
        }
        catch (ScriptRuntimeException e)
        {
            throw new TemplateRenderException($"Template rendering failed: {e.Message}", [e.Message], e);
        }
    }

    private ScribanTemplate GetOrParse(string html)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)));
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var template = ScribanTemplate.Parse(html, lexerOptions: new LexerOptions { Lang = ScriptLang.Default });
        if (template.HasErrors)
        {
            var errors = template.Messages.Select(m => m.ToString()).ToList();
            throw new TemplateRenderException("Template syntax error: " + string.Join("; ", errors), errors);
        }

        if (_cache.Count >= MaxCacheEntries)
            _cache.Clear();
        _cache[key] = template;
        return template;
    }

    /// <summary>`value | digits`: ASCII digits to Persian digits when the resume language is fa.</summary>
    private sealed class DigitsFunction(bool persian) : IScriptCustomFunction
    {
        public int ParameterCount => 1;
        public int RequiredParameterCount => 1;
        public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
        public Type ReturnType => typeof(string);
        public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(object), "value");

        public object? Invoke(
            TemplateContext context,
            ScriptNode? callerContext,
            ScriptArray arguments,
            ScriptBlockStatement? blockStatement
        )
        {
            var value = arguments.Count > 0 ? arguments[0] : null;
            var text = value switch
            {
                null => "",
                string s => s,
                _ => System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "",
            };
            if (!persian)
                return text;
            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] is >= '0' and <= '9')
                    chars[i] = (char)('۰' + (chars[i] - '0'));
            }
            return new string(chars);
        }

        public ValueTask<object?> InvokeAsync(
            TemplateContext context,
            ScriptNode? callerContext,
            ScriptArray arguments,
            ScriptBlockStatement? blockStatement
        ) => new(Invoke(context, callerContext, arguments, blockStatement));
    }
}
