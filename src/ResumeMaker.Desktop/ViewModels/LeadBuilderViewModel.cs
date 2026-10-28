using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Desktop.Services;

namespace ResumeMaker.Desktop.ViewModels;

public sealed partial class LeadBuilderViewModel : ViewModelBase
{
    private readonly LeadGenerator _generator;
    private readonly IClipboardService _clipboard;
    private readonly ILauncher _launcher;
    private readonly StatusService _status;
    private readonly TemplatesViewModel _templates;
    private readonly Action _showTemplates;

    public LeadBuilderViewModel(
        LeadGenerator generator,
        IClipboardService clipboard,
        ILauncher launcher,
        StatusService status,
        TemplatesViewModel templates,
        Action showTemplates
    )
    {
        _generator = generator;
        _clipboard = clipboard;
        _launcher = launcher;
        _status = status;
        _templates = templates;
        _showTemplates = showTemplates;
        StyleOptions = [.. new[] { "minimal", "classic", "modern", "compact", "creative", "elegant" }.Select(s => new LocalizedOption(s, "style." + s))];
        SidebarOptions = [new("none", "sidebar.none"), new("left", "sidebar.left"), new("right", "sidebar.right")];
        ColumnOptions = [new("1", literal: "1"), new("2", literal: "2")];
        _selectedStyle = StyleOptions[0];
        _selectedSidebar = SidebarOptions[0];
        _selectedColumns = ColumnOptions[0];
        _selectedLanguage = LocalizedOption.Languages[0];
    }

    public IReadOnlyList<LocalizedOption> StyleOptions { get; }
    public IReadOnlyList<LocalizedOption> SidebarOptions { get; }
    public IReadOnlyList<LocalizedOption> ColumnOptions { get; }
    public IReadOnlyList<LocalizedOption> LanguageOptions => LocalizedOption.Languages;
    public IReadOnlyList<ShortcutLink> Shortcuts => LeadGenerator.ShortcutLinks;

    [ObservableProperty]
    private LocalizedOption _selectedStyle;

    [ObservableProperty]
    private LocalizedOption _selectedSidebar;

    [ObservableProperty]
    private LocalizedOption _selectedColumns;

    [ObservableProperty]
    private LocalizedOption _selectedLanguage;

    [ObservableProperty]
    private string _primaryColor = "#1a73e8";

    [ObservableProperty]
    private string _accentColor = "#ff6b6b";

    /// <summary>Comma separated section keys, most important first (empty = default order).</summary>
    [ObservableProperty]
    private string _sectionPriority = "experience, education, skills, languages, projects, awards, interests";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLead))]
    private string _lead = "";

    public bool HasLead => Lead.Length > 0;

    public void SetDefaultLanguage(string language) => SelectedLanguage = LocalizedOption.Find(LanguageOptions, language)!;

    [RelayCommand]
    private void Generate()
    {
        Lead = _generator.GenerateTemplateLead(
            new TemplateLeadOptions
            {
                Style = SelectedStyle.Value,
                PrimaryColor = PrimaryColor.Trim(),
                AccentColor = AccentColor.Trim(),
                ColumnCount = int.Parse(SelectedColumns.Value),
                SidebarPosition = SelectedSidebar.Value,
                Language = SelectedLanguage.Value,
                SectionPriority = ItemViewModel.ParseTags(SectionPriority) ?? [],
            }
        );
        _status.Info(Loc["status.lead_generated"]);
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (!HasLead)
            Generate();
        await _clipboard.SetTextAsync(Lead);
        _status.Info(Loc["status.copied"]);
    }

    [RelayCommand]
    private void OpenLink(ShortcutLink? link)
    {
        if (link is not null)
            _launcher.OpenUrl(link.Url);
    }

    [RelayCommand]
    private async Task ImportHtmlAsync()
    {
        await _templates.ImportAsync();
        _showTemplates();
    }
}
