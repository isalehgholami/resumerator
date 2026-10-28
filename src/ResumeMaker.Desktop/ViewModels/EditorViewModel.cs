using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Domain;
using ResumeMaker.Desktop.Services;

namespace ResumeMaker.Desktop.ViewModels;

public sealed partial class EditorViewModel : ViewModelBase
{
    private static readonly HashSet<string> Tracked =
    [
        nameof(ProfileName), nameof(FullName), nameof(JobTitle), nameof(Email), nameof(Phone), nameof(Location),
        nameof(LinkedIn), nameof(GitHub), nameof(Website), nameof(Summary), nameof(PrimaryColor), nameof(AccentColor),
        nameof(SelectedLanguage),
    ];

    private static readonly (SectionType Type, string Fa, string En)[] SectionNames =
    [
        (SectionType.Experience, "تجربه کاری", "Experience"),
        (SectionType.Education, "تحصیلات", "Education"),
        (SectionType.Skills, "مهارت‌ها", "Skills"),
        (SectionType.Languages, "زبان‌ها", "Languages"),
        (SectionType.Projects, "پروژه‌ها", "Projects"),
        (SectionType.Awards, "جوایز", "Awards"),
        (SectionType.Interests, "علایق", "Interests"),
        (SectionType.Custom, "بخش سفارشی", "Custom"),
    ];

    private readonly ProfileSession _session;
    private readonly PreviewService _preview;
    private readonly ValidationService _validation;
    private bool _loading;

    public EditorViewModel(ProfileSession session, PreviewService preview, ValidationService validation)
    {
        _session = session;
        _preview = preview;
        _validation = validation;
        _selectedLanguage = LocalizedOption.Languages[0];
        SectionTypeOptions = [.. SectionNames.Select(n => new LocalizedOption(n.Type.ToString(), "section.type." + n.Type.ToString().ToLowerInvariant()))];
        _newSectionType = SectionTypeOptions[0];
        Sections.CollectionChanged += (_, _) => Push();
        _session.Changed += OnSessionChanged;
        Load(_session.Current);
    }

    public IReadOnlyList<LocalizedOption> LanguageOptions => LocalizedOption.Languages;

    public IReadOnlyList<LocalizedOption> SectionTypeOptions { get; }

    public ObservableCollection<SectionViewModel> Sections { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoProfile))]
    private bool _hasProfile;

    public bool HasNoProfile => !HasProfile;

    [ObservableProperty]
    private string _profileName = "";

    [ObservableProperty]
    private string _fullName = "";

    [ObservableProperty]
    private string _jobTitle = "";

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _phone = "";

    [ObservableProperty]
    private string _location = "";

    [ObservableProperty]
    private string _linkedIn = "";

    [ObservableProperty]
    private string _gitHub = "";

    [ObservableProperty]
    private string _website = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private string _primaryColor = "";

    [ObservableProperty]
    private string _accentColor = "";

    [ObservableProperty]
    private LocalizedOption _selectedLanguage;

    [ObservableProperty]
    private SectionViewModel? _selectedSection;

    [ObservableProperty]
    private LocalizedOption _newSectionType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIssues))]
    private string _issues = "";

    public bool HasIssues => Issues.Length > 0;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_loading && e.PropertyName is not null && Tracked.Contains(e.PropertyName))
            Push();
    }

    private void OnSessionChanged(Profile? profile, object? source)
    {
        if (!ReferenceEquals(source, this))
            Load(profile);
    }

    private void Load(Profile? profile)
    {
        _loading = true;
        try
        {
            HasProfile = profile is not null;
            Sections.Clear();
            SelectedSection = null;
            if (profile is null)
            {
                Issues = "";
                return;
            }
            ProfileName = profile.Name;
            FullName = profile.Contact.FullName;
            JobTitle = profile.Contact.Title;
            Email = profile.Contact.Email;
            Phone = profile.Contact.Phone;
            Location = profile.Contact.Location;
            LinkedIn = Link(profile, "linkedin");
            GitHub = Link(profile, "github");
            Website = Link(profile, "website");
            Summary = profile.Summary;
            PrimaryColor = profile.Theme.PrimaryColor ?? "";
            AccentColor = profile.Theme.AccentColor ?? "";
            SelectedLanguage = LocalizedOption.Find(LanguageOptions, profile.Language)!;
            foreach (var s in profile.Sections.OrderBy(s => s.Order))
                Sections.Add(NewSection(s));
            SelectedSection = Sections.FirstOrDefault();
            Issues = FormatIssues(profile);
        }
        finally
        {
            _loading = false;
        }
    }

    private static string Link(Profile p, string key) => p.Contact.Links.TryGetValue(key, out var v) ? v : "";

    private SectionViewModel NewSection(Section s) => new(s, Push, MoveSection, RemoveSection);

    /// <summary>The profile as currently edited (the session profile with the form fields applied).</summary>
    public Profile? Build()
    {
        if (_session.Current is not { } cur)
            return null;
        var links = new Dictionary<string, string>(cur.Contact.Links);
        SetLink(links, "linkedin", LinkedIn);
        SetLink(links, "github", GitHub);
        SetLink(links, "website", Website);
        return cur with
        {
            Name = ProfileName,
            Language = SelectedLanguage.Value,
            Contact = cur.Contact with
            {
                FullName = FullName,
                Title = JobTitle,
                Email = Email,
                Phone = Phone,
                Location = Location,
                Links = links,
            },
            Summary = Summary,
            Theme = cur.Theme with { PrimaryColor = Blank(PrimaryColor), AccentColor = Blank(AccentColor) },
            Sections = [.. Sections.Select((s, i) => s.ToSection(i))],
        };
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void SetLink(Dictionary<string, string> links, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            links.Remove(key);
        else
            links[key] = value.Trim();
    }

    private void Push()
    {
        if (_loading || Build() is not { } profile)
            return;
        _session.Update(profile, this);
        Issues = FormatIssues(profile);
    }

    private string FormatIssues(Profile profile) =>
        string.Join("\n", _validation.Validate(profile).Issues.Select(i => (i.IsError ? "✗ " : "• ") + i.Message));

    private void MoveSection(SectionViewModel vm, int delta)
    {
        var i = Sections.IndexOf(vm);
        var j = i + delta;
        if (i >= 0 && j >= 0 && j < Sections.Count)
            Sections.Move(i, j);
    }

    private void RemoveSection(SectionViewModel vm)
    {
        if (ReferenceEquals(SelectedSection, vm))
            SelectedSection = null;
        Sections.Remove(vm);
    }

    [RelayCommand]
    private void AddSection()
    {
        var type = Enum.Parse<SectionType>(NewSectionType.Value);
        var names = SectionNames.First(n => n.Type == type);
        var title = SelectedLanguage.Value == "fa" ? names.Fa : names.En;
        var vm = NewSection(new Section { Type = type, Title = title });
        Sections.Add(vm);
        SelectedSection = vm;
    }

    [RelayCommand]
    private async Task PreviewAsync() => await _preview.OpenAsync();
}
