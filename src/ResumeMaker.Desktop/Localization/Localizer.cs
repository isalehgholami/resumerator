using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResumeMaker.Desktop.Localization;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
internal sealed partial class LocaleJsonContext : JsonSerializerContext;

/// <summary>
/// Key/value localizer backed by embedded JSON files (architecture section 13). Reflection free:
/// bind with <c>{Binding Loc[some.key]}</c>; changing the language raises "Item[]" so every binding refreshes.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public const string Fa = "fa";
    public const string En = "en";

    private readonly Func<string, Stream?> _open;
    private IReadOnlyDictionary<string, string> _current = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> _fallback = new Dictionary<string, string>();

    public Localizer(Func<string, Stream?> openLocale, string language = En)
    {
        _open = openLocale;
        Language = Normalize(language);
        Reload();
    }

    /// <summary>Process-wide instance loading the embedded locale files.</summary>
    public static Localizer Instance { get; } = new(OpenEmbedded);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; }

    public bool IsRtl => Language == Fa;

    public string this[string key] =>
        _current.TryGetValue(key, out var v) ? v
        : _fallback.TryGetValue(key, out var f) ? f
        : key;

    public static string Normalize(string? code) => string.Equals(code, Fa, StringComparison.OrdinalIgnoreCase) ? Fa : En;

    public string Format(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, this[key], args);

    public void SetLanguage(string? code)
    {
        var next = Normalize(code);
        if (next == Language)
            return;
        Language = next;
        Reload();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRtl)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public static Dictionary<string, string> Parse(Stream stream) =>
        JsonSerializer.Deserialize(stream, LocaleJsonContext.Default.DictionaryStringString) ?? [];

    private void Reload()
    {
        _fallback = Load(En);
        _current = Language == En ? _fallback : Load(Language);
    }

    private Dictionary<string, string> Load(string code)
    {
        using var stream = _open(code);
        return stream is null ? [] : Parse(stream);
    }

    private static Stream? OpenEmbedded(string code) =>
        typeof(Localizer).Assembly.GetManifestResourceStream($"locales/{code}.json");
}
