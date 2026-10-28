using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Domain;
using ResumeMaker.Desktop.Services;

namespace ResumeMaker.Desktop.ViewModels;

public sealed class TemplateItemViewModel(Template template)
{
    public string Id { get; } = template.Id;
    public string Name { get; } = template.Name;
    public bool IsBuiltIn { get; } = template.IsBuiltIn;
    public string Kind => IsBuiltIn ? "built-in" : "custom";
    public string Display => IsBuiltIn ? Name : Name + " *";
}

public sealed partial class TemplatesViewModel : ViewModelBase
{
    private readonly ITemplateRepository _repository;
    private readonly ITemplateValidator _validator;
    private readonly ProfileSession _session;
    private readonly IFilePicker _picker;
    private readonly LeadHistory _history;
    private readonly StatusService _status;
    private bool _syncing;

    public TemplatesViewModel(
        ITemplateRepository repository,
        ITemplateValidator validator,
        ProfileSession session,
        IFilePicker picker,
        LeadHistory history,
        StatusService status
    )
    {
        _repository = repository;
        _validator = validator;
        _session = session;
        _picker = picker;
        _history = history;
        _status = status;
        _session.Changed += (_, source) =>
        {
            if (!ReferenceEquals(source, this))
                SyncFromSession();
        };
    }

    public ObservableCollection<TemplateItemViewModel> Templates { get; } = [];

    public ObservableCollection<string> Report { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    private TemplateItemViewModel? _selectedTemplate;

    [ObservableProperty]
    private bool _atsMode;

    [ObservableProperty]
    private bool _hasReport;

    public bool HasProfile => _session.Current is not null;

    public bool CanDelete => SelectedTemplate is { IsBuiltIn: false };

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var list = await _repository.ListAsync();
        _syncing = true;
        try
        {
            Templates.Clear();
            foreach (var t in list.Where(t => t.Id != CoverLetterService.TemplateId))
                Templates.Add(new TemplateItemViewModel(t));
        }
        finally
        {
            _syncing = false;
        }
        SyncFromSession();
    }

    private void SyncFromSession()
    {
        _syncing = true;
        try
        {
            var p = _session.Current;
            AtsMode = p?.AtsMode ?? false;
            SelectedTemplate = p is null ? null : Templates.FirstOrDefault(t => t.Id == p.TemplateId);
            OnPropertyChanged(nameof(HasProfile));
        }
        finally
        {
            _syncing = false;
        }
    }

    partial void OnSelectedTemplateChanged(TemplateItemViewModel? value)
    {
        if (_syncing || value is null || _session.Current is not { } p || p.TemplateId == value.Id)
            return;
        _session.Update(p with { TemplateId = value.Id }, this);
        _status.Info(Loc.Format("status.template_selected", value.Name));
    }

    partial void OnAtsModeChanged(bool value)
    {
        if (_syncing || _session.Current is not { } p || p.AtsMode == value)
            return;
        _session.Update(p with { AtsMode = value }, this);
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        var path = await _picker.PickOpenAsync(Loc["templates.import"], "html", "htm");
        if (path is null)
            return;
        await ImportFileAsync(path);
    }

    /// <summary>Validates a template file, lists problems and saves the sanitized version when it has no errors.</summary>
    public async Task<bool> ImportFileAsync(string path)
    {
        Report.Clear();
        string html;
        try
        {
            html = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Error(ex.Message);
            return false;
        }

        var result = _validator.Validate(html);
        foreach (var issue in result.Issues)
            Report.Add((issue.IsError ? "✗ " : "• ") + issue.Message);
        if (!result.IsValid)
        {
            HasReport = true;
            _status.Error(Loc["templates.invalid"]);
            return false;
        }

        var name = Path.GetFileNameWithoutExtension(path);
        var existing = (await _repository.ListAsync()).Select(t => t.Id).ToHashSet();
        var id = UniqueId(Slug(name), existing);
        var lead = _history.Entries.LastOrDefault(e => e.Kind == LeadGenerator.HistoryKind)?.Prompt;
        await _repository.SaveCustomAsync(
            new Template
            {
                Id = id,
                Name = name,
                Style = TemplateStyle.Custom,
                HtmlContent = _validator.Sanitize(html),
                SourceLead = lead,
            }
        );
        if (result.Issues.Count == 0)
            Report.Add(Loc["templates.valid"]);
        HasReport = true;
        await RefreshAsync();
        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == id);
        _status.Info(Loc.Format("status.template_imported", name));
        return true;
    }

    [RelayCommand]
    public async Task DeleteCustomAsync()
    {
        if (SelectedTemplate is not { IsBuiltIn: false } t)
            return;
        await _repository.DeleteCustomAsync(t.Id);
        if (_session.Current is { } p && p.TemplateId == t.Id)
            _session.Update(p with { TemplateId = "minimal" }, this);
        await RefreshAsync();
        _status.Info(Loc.Format("status.template_deleted", t.Name));
    }

    public static string Slug(string name)
    {
        var sb = new StringBuilder("custom-");
        foreach (var c in name.ToLowerInvariant())
            sb.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' ? c : '-');
        var slug = sb.ToString().Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Length <= 7 ? "custom-template" : slug;
    }

    private static string UniqueId(string slug, HashSet<string> existing)
    {
        if (!existing.Contains(slug))
            return slug;
        for (var i = 2; ; i++)
        {
            var candidate = $"{slug}-{i}";
            if (!existing.Contains(candidate))
                return candidate;
        }
    }
}
