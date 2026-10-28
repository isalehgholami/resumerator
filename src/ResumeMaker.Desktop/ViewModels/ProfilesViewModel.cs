using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Common;
using ResumeMaker.Core.Domain;
using ResumeMaker.Desktop.Services;

namespace ResumeMaker.Desktop.ViewModels;

public sealed partial class ProfilesViewModel : ViewModelBase
{
    private readonly ProfileService _profiles;
    private readonly ProfileSession _session;
    private readonly IFilePicker _picker;
    private readonly StatusService _status;

    public ProfilesViewModel(ProfileService profiles, ProfileSession session, IFilePicker picker, StatusService status)
    {
        _profiles = profiles;
        _session = session;
        _picker = picker;
        _status = status;
        _newLanguage = LocalizedOption.Languages[0];
    }

    public ObservableCollection<ProfileSummary> Items { get; } = [];

    public IReadOnlyList<LocalizedOption> LanguageOptions => LocalizedOption.Languages;

    [ObservableProperty]
    private ProfileSummary? _selected;

    [ObservableProperty]
    private string _newName = "";

    [ObservableProperty]
    private LocalizedOption _newLanguage;

    /// <summary>Raised after a profile was opened so the shell can navigate to the editor.</summary>
    public event Action? ProfileOpened;

    public Guid? ActiveId => _session.Current?.Id;

    public void SetDefaultLanguage(string language) => NewLanguage = LocalizedOption.Find(LanguageOptions, language)!;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var list = await _profiles.ListAsync();
        var selectedId = Selected?.Id;
        Items.Clear();
        foreach (var p in list.OrderByDescending(p => p.UpdatedAt))
            Items.Add(p);
        Selected = Items.FirstOrDefault(p => p.Id == selectedId);
    }

    /// <summary>Opens the most recently updated profile if nothing is open yet.</summary>
    public async Task OpenLatestIfNoneAsync()
    {
        await RefreshAsync();
        if (_session.Current is null && Items.Count > 0)
            await OpenAsync(Items[0], navigate: false);
    }

    [RelayCommand]
    public async Task CreateAsync()
    {
        var name = string.IsNullOrWhiteSpace(NewName) ? Loc["profiles.untitled"] : NewName.Trim();
        var profile = await _profiles.CreateAsync(name, NewLanguage.Value);
        NewName = "";
        await RefreshAsync();
        _session.Open(profile);
        _status.Info(Loc.Format("status.created", profile.Name));
        ProfileOpened?.Invoke();
    }

    [RelayCommand]
    public async Task OpenSelectedAsync()
    {
        if (Selected is { } s)
            await OpenAsync(s, navigate: true);
    }

    private async Task OpenAsync(ProfileSummary summary, bool navigate)
    {
        await _session.FlushAsync();
        var profile = await _profiles.GetAsync(summary.Id);
        if (profile is null)
        {
            _status.Error(Loc.Format("status.profile_missing", summary.Name));
            await RefreshAsync();
            return;
        }
        _session.Open(profile);
        if (navigate)
            ProfileOpened?.Invoke();
    }

    [RelayCommand]
    public async Task DuplicateAsync()
    {
        if (Selected is not { } s)
            return;
        await _session.FlushAsync();
        var copy = await _profiles.DuplicateAsync(s.Id);
        await RefreshAsync();
        Selected = Items.FirstOrDefault(p => p.Id == copy.Id);
        _status.Info(Loc.Format("status.duplicated", copy.Name));
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (Selected is not { } s)
            return;
        if (_session.Current?.Id == s.Id)
            _session.Open(null);
        await _profiles.DeleteAsync(s.Id);
        await RefreshAsync();
        _status.Info(Loc.Format("status.deleted", s.Name));
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        var path = await _picker.PickOpenAsync(Loc["profiles.import"], "json");
        if (path is null)
            return;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var imported = ProfileService.ImportJson(json) with { Id = Guid.NewGuid() };
            var saved = await _profiles.SaveAsync(imported);
            await RefreshAsync();
            Selected = Items.FirstOrDefault(p => p.Id == saved.Id);
            _status.Info(Loc.Format("status.imported", saved.Name));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _status.Error(Loc.Format("status.import_failed", ex.Message));
        }
    }

    [RelayCommand]
    public async Task ExportSelectedAsync()
    {
        if (Selected is not { } s)
            return;
        var profile = _session.Current?.Id == s.Id ? _session.Current : await _profiles.GetAsync(s.Id);
        if (profile is null)
            return;
        var path = await _picker.PickSaveAsync(Loc["profiles.export"], ProfileFileName.Sanitize(profile.Name) + ".json", "json");
        if (path is null)
            return;
        try
        {
            await File.WriteAllBytesAsync(path, ProfileService.ExportJsonBytes(profile));
            _status.Info(Loc.Format("status.exported", path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Error(ex.Message);
        }
    }
}
