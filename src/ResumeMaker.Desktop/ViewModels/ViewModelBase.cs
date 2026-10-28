using CommunityToolkit.Mvvm.ComponentModel;
using ResumeMaker.Desktop.Localization;

namespace ResumeMaker.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Bind with <c>{Binding Loc[key]}</c>; refreshes on language change.</summary>
    public Localizer Loc => Localizer.Instance;
}

/// <summary>Combo-box option: stable value plus a label that follows the UI language.</summary>
public sealed class LocalizedOption : ObservableObject
{
    private readonly string? _key;
    private readonly string? _literal;

    public LocalizedOption(string value, string? labelKey = null, string? literal = null)
    {
        Value = value;
        _key = labelKey;
        _literal = literal;
        if (_key is not null)
            Localizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public string Value { get; }

    public string Label => _key is null ? _literal ?? Value : Localizer.Instance[_key];

    public override string ToString() => Label;

    public static LocalizedOption? Find(IReadOnlyList<LocalizedOption> options, string? value) =>
        options.FirstOrDefault(o => o.Value == value) ?? (options.Count > 0 ? options[0] : null);

    public static IReadOnlyList<LocalizedOption> Languages { get; } =
        [new("fa", literal: "فارسی"), new("en", literal: "English")];
}
