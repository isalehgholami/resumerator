using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ResumeMaker.Desktop.Localization;
using ResumeMaker.Desktop.Services;

namespace ResumeMaker.Desktop.ViewModels;

/// <summary>Left-navigation entry; its label follows the UI language.</summary>
public sealed class NavItem : ObservableObject
{
    private readonly string _labelKey;

    public NavItem(string labelKey, string glyph, ViewModelBase page)
    {
        _labelKey = labelKey;
        Glyph = glyph;
        Page = page;
        Localizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public string Glyph { get; }
    public ViewModelBase Page { get; }
    public string Label => Localizer.Instance[_labelKey];
}

public sealed partial class MainViewModel : ViewModelBase
{
    public MainViewModel(
        StatusService status,
        ProfileSession session,
        ProfilesViewModel profiles,
        EditorViewModel editor,
        TemplatesViewModel templates,
        LeadBuilderViewModel leadBuilder,
        CoverLetterViewModel coverLetter,
        ExportViewModel export,
        SettingsViewModel settings
    )
    {
        Status = status;
        Session = session;
        Profiles = profiles;
        Editor = editor;
        Templates = templates;
        LeadBuilder = leadBuilder;
        CoverLetter = coverLetter;
        Export = export;
        Settings = settings;

        NavItems =
        [
            new("nav.profiles", "👤", profiles),
            new("nav.editor", "✎", editor),
            new("nav.templates", "▦", templates),
            new("nav.lead_builder", "✨", leadBuilder),
            new("nav.cover_letter", "✉", coverLetter),
            new("nav.export", "⇩", export),
            new("nav.settings", "⚙", settings),
        ];
        _selectedNav = NavItems[0];
        profiles.ProfileOpened += () => Navigate(editor);
        session.Changed += (_, _) => OnPropertyChanged(nameof(ActiveProfileName));
    }

    public StatusService Status { get; }
    public ProfileSession Session { get; }
    public ProfilesViewModel Profiles { get; }
    public EditorViewModel Editor { get; }
    public TemplatesViewModel Templates { get; }
    public LeadBuilderViewModel LeadBuilder { get; }
    public CoverLetterViewModel CoverLetter { get; }
    public ExportViewModel Export { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private NavItem _selectedNav;

    public ViewModelBase CurrentPage => SelectedNav.Page;

    public string ActiveProfileName => Session.Current?.Name ?? "";

    public void Navigate(ViewModelBase page) => SelectedNav = NavItems.First(n => ReferenceEquals(n.Page, page));

    /// <summary>Initial data load after the window is constructed.</summary>
    public async Task InitializeAsync()
    {
        await Settings.LoadAsync();
        await Templates.RefreshAsync();
        await Profiles.OpenLatestIfNoneAsync();
    }
}
