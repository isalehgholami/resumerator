using ResumeMaker.Core.Application.Interfaces;

namespace ResumeMaker.Infrastructure.Fonts;

/// <summary>Serves the woff2 fonts embedded in this assembly as base64 (Vazirmatn for fa, Inter otherwise).</summary>
public sealed class EmbeddedFontProvider : IFontResourceProvider
{
    private readonly Dictionary<string, string> _cache = [];
    private readonly object _lock = new();

    public Task<string> GetFontBase64Async(string language, bool bold = false, CancellationToken ct = default)
    {
        var name = language == "fa"
            ? (bold ? "vazir-bold.woff2" : "vazir.woff2")
            : "inter-regular.woff2";
        return Task.FromResult(Load(name));
    }

    private string Load(string name)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(name, out var cached))
                return cached;
            using var stream = typeof(EmbeddedFontProvider).Assembly.GetManifestResourceStream("fonts/" + name)
                ?? throw new FileNotFoundException($"Embedded font '{name}' is missing.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return _cache[name] = Convert.ToBase64String(ms.ToArray());
        }
    }
}
