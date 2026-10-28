using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Desktop.ViewModels;

/// <summary>Editable view of one <see cref="SectionItem"/>; only the fields relevant to the section type are shown.</summary>
public sealed partial class ItemViewModel : ViewModelBase
{
    private readonly Action _changed;
    private readonly Action<ItemViewModel, int> _move;
    private readonly Action<ItemViewModel> _remove;

    public ItemViewModel(
        SectionType type,
        SectionItem item,
        Action changed,
        Action<ItemViewModel, int> move,
        Action<ItemViewModel> remove
    )
    {
        _changed = changed;
        _move = move;
        _remove = remove;
        Type = type;
        _title = item.Title ?? "";
        _company = item.Company ?? "";
        _location = item.Location ?? "";
        _start = item.Start ?? "";
        _end = item.End ?? "";
        _description = item.Description ?? "";
        _tagsText = item.Tags is null ? "" : string.Join(", ", item.Tags);
        _degree = item.Degree ?? "";
        _institution = item.Institution ?? "";
        _category = item.Category ?? "";
        _entriesText = item.Entries is null ? "" : string.Join("\n", item.Entries.Select(e => $"{e.Name} | {e.Level}".TrimEnd(' ', '|')));
        _name = item.Name ?? "";
        _level = item.Level ?? "";
        _url = item.Url ?? "";
        _issuer = item.Issuer ?? "";
        _date = item.Date ?? "";
        _original = item;
    }

    private readonly SectionItem _original;

    public SectionType Type { get; }

    public bool ShowTitle => Type is SectionType.Experience or SectionType.Projects or SectionType.Awards or SectionType.Custom;
    public bool ShowCompany => Type is SectionType.Experience or SectionType.Custom;
    public bool ShowLocation => Type is SectionType.Experience or SectionType.Education or SectionType.Custom;
    public bool ShowDates => Type is SectionType.Experience or SectionType.Education or SectionType.Projects or SectionType.Custom;
    public bool ShowDescription => Type is not (SectionType.Skills or SectionType.Languages or SectionType.Interests);
    public bool ShowTags => Type is SectionType.Experience or SectionType.Projects or SectionType.Custom;
    public bool ShowEducation => Type == SectionType.Education;
    public bool ShowSkills => Type == SectionType.Skills;
    public bool ShowName => Type is SectionType.Languages or SectionType.Interests;
    public bool ShowLevel => Type == SectionType.Languages;
    public bool ShowUrl => Type is SectionType.Projects or SectionType.Custom;
    public bool ShowAward => Type == SectionType.Awards;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _company;

    [ObservableProperty]
    private string _location;

    [ObservableProperty]
    private string _start;

    [ObservableProperty]
    private string _end;

    [ObservableProperty]
    private string _description;

    /// <summary>Comma separated tags.</summary>
    [ObservableProperty]
    private string _tagsText;

    [ObservableProperty]
    private string _degree;

    [ObservableProperty]
    private string _institution;

    [ObservableProperty]
    private string _category;

    /// <summary>One "name | level" entry per line.</summary>
    [ObservableProperty]
    private string _entriesText;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _level;

    [ObservableProperty]
    private string _url;

    [ObservableProperty]
    private string _issuer;

    [ObservableProperty]
    private string _date;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not null && !e.PropertyName.StartsWith("Show", StringComparison.Ordinal) && e.PropertyName != nameof(Loc))
            _changed();
    }

    [RelayCommand]
    private void MoveUp() => _move(this, -1);

    [RelayCommand]
    private void MoveDown() => _move(this, 1);

    [RelayCommand]
    private void Remove() => _remove(this);

    public string Summary =>
        FirstNonEmpty(Title, Degree, Name, Category, Institution, Company, Issuer) is { Length: > 0 } s ? s : Loc["editor.item"];

    public SectionItem ToItem() =>
        _original with
        {
            Title = Blank(Title),
            Company = Blank(Company),
            Location = Blank(Location),
            Start = Blank(Start),
            End = Blank(End),
            Description = Blank(Description),
            Tags = ParseTags(TagsText),
            Degree = Blank(Degree),
            Institution = Blank(Institution),
            Category = Blank(Category),
            Entries = ParseEntries(EntriesText),
            Name = Blank(Name),
            Level = Blank(Level),
            Url = Blank(Url),
            Issuer = Blank(Issuer),
            Date = Blank(Date),
        };

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    public static List<string>? ParseTags(string? text)
    {
        var tags = (text ?? "")
            .Split([',', '،', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .ToList();
        return tags.Count == 0 ? null : tags;
    }

    public static List<SkillEntry>? ParseEntries(string? text)
    {
        var entries = new List<SkillEntry>();
        foreach (var raw in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = raw.IndexOf('|');
            var name = (idx < 0 ? raw : raw[..idx]).Trim();
            var level = idx < 0 ? "" : raw[(idx + 1)..].Trim();
            if (name.Length > 0)
                entries.Add(new SkillEntry { Name = name, Level = level });
        }
        return entries.Count == 0 ? null : entries;
    }
}

public sealed partial class SectionViewModel : ViewModelBase
{
    private readonly Action _changed;
    private readonly Action<SectionViewModel, int> _move;
    private readonly Action<SectionViewModel> _remove;

    public SectionViewModel(Section section, Action changed, Action<SectionViewModel, int> move, Action<SectionViewModel> remove)
    {
        _changed = changed;
        _move = move;
        _remove = remove;
        Id = section.Id;
        Type = section.Type;
        _title = section.Title;
        _enabled = section.Enabled;
        foreach (var item in section.Items)
            Items.Add(NewItem(item));
        Items.CollectionChanged += (_, _) => _changed();
    }

    public string Id { get; }

    public SectionType Type { get; }

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _enabled;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Title) or nameof(Enabled))
            _changed();
    }

    [RelayCommand]
    private void MoveUp() => _move(this, -1);

    [RelayCommand]
    private void MoveDown() => _move(this, 1);

    [RelayCommand]
    private void Remove() => _remove(this);

    [RelayCommand]
    private void AddItem() => Items.Add(NewItem(new SectionItem()));

    private ItemViewModel NewItem(SectionItem item) =>
        new(
            Type,
            item,
            _changed,
            (vm, delta) =>
            {
                var i = Items.IndexOf(vm);
                var j = i + delta;
                if (i >= 0 && j >= 0 && j < Items.Count)
                    Items.Move(i, j);
            },
            vm => Items.Remove(vm)
        );

    public Section ToSection(int order) =>
        new()
        {
            Id = Id,
            Type = Type,
            Title = Title,
            Enabled = Enabled,
            Order = order,
            Items = [.. Items.Select(i => i.ToItem())],
        };
}
