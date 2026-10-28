using CommunityToolkit.Mvvm.ComponentModel;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;
using ResumeMaker.Desktop.Localization;
using ResumeMaker.Desktop.Services;

namespace ResumeMaker.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _store;
    private readonly IThemeService _theme;
    private readonly StatusService _status;
    private AppSettings _settings = new();
    private bool _loading;
    private CancellationTokenSource? _pending;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public SettingsViewModel(ISettingsStore store, IThemeService theme, StatusService status)
    {
        _store = store;
        _theme = theme;
        _status = status;
        ThemeOptions = [new("system", "theme.system"), new("light", "theme.light"), new("dark", "theme.dark")];
        _selectedTheme = ThemeOptions[0];
        _selectedUiLanguage = LocalizedOption.Languages[1];
        _selectedResumeLanguage = LocalizedOption.Languages[0];
    }

    public IReadOnlyList<LocalizedOption> ThemeOptions { get; }
    public IReadOnlyList<LocalizedOption> LanguageOptions => LocalizedOption.Languages;

    /// <summary>Delay before settings are written after a change (0 = immediate).</summary>
    public TimeSpan SaveDelay { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>Raised when the default resume language changes (new profile defaults).</summary>
    public event Action<string>? DefaultResumeLanguageChanged;

    public string DefaultResumeLanguage => SelectedResumeLanguage.Value;

    [ObservableProperty]
    private LocalizedOption _selectedTheme;

    [ObservableProperty]
    private LocalizedOption _selectedUiLanguage;

    [ObservableProperty]
    private LocalizedOption _selectedResumeLanguage;

    [ObservableProperty]
    private string _chromePath = "";

    public async Task LoadAsync()
    {
        _settings = await _store.LoadAsync();
        _loading = true;
        try
        {
            SelectedTheme = LocalizedOption.Find(ThemeOptions, _settings.Theme)!;
            SelectedUiLanguage = LocalizedOption.Find(LanguageOptions, _settings.UiLanguage)!;
            SelectedResumeLanguage = LocalizedOption.Find(LanguageOptions, _settings.DefaultResumeLanguage)!;
            ChromePath = _settings.ChromePath ?? "";
        }
        finally
        {
            _loading = false;
        }
        Apply();
        DefaultResumeLanguageChanged?.Invoke(SelectedResumeLanguage.Value);
    }

    /// <summary>Applies UI theme and language (UI only, never affects exports).</summary>
    private void Apply()
    {
        _theme.Apply(SelectedTheme.Value);
        Localizer.Instance.SetLanguage(SelectedUiLanguage.Value);
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading)
            return;
        switch (e.PropertyName)
        {
            case nameof(SelectedTheme):
            case nameof(SelectedUiLanguage):
                Apply();
                ScheduleSave();
                break;
            case nameof(SelectedResumeLanguage):
                DefaultResumeLanguageChanged?.Invoke(SelectedResumeLanguage.Value);
                ScheduleSave();
                break;
            case nameof(ChromePath):
                ScheduleSave();
                break;
        }
    }

    private void ScheduleSave()
    {
        _pending?.Cancel();
        var cts = _pending = new CancellationTokenSource();
        _ = SaveLaterAsync(cts.Token);
    }

    private async Task SaveLaterAsync(CancellationToken ct)
    {
        try
        {
            if (SaveDelay > TimeSpan.Zero)
                await Task.Delay(SaveDelay, ct);
            ct.ThrowIfCancellationRequested();
            await SaveAsync();
        }
        catch (OperationCanceledException) { }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            _settings = _settings with
            {
                Theme = SelectedTheme.Value,
                UiLanguage = SelectedUiLanguage.Value,
                DefaultResumeLanguage = SelectedResumeLanguage.Value,
                ChromePath = string.IsNullOrWhiteSpace(ChromePath) ? null : ChromePath.Trim(),
            };
            await _store.SaveAsync(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Error(ex.Message);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
