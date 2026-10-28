using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Common;
using ResumeMaker.Core.Domain;
using ResumeMaker.Desktop.Localization;
using ResumeMaker.Desktop.Services;

namespace ResumeMaker.Desktop.ViewModels;

/// <summary>Shared error presentation for export operations.</summary>
public static class ExportErrors
{
    public const string ChromeUrl = "https://www.google.com/chrome";

    public static (string Message, bool ChromeMissing) Describe(Exception ex, Localizer loc) =>
        ex switch
        {
            ChromeNotFoundException => (loc["export.chrome_missing"], true),
            ExportFailedException e => (loc.Format("export.failed", e.Message), false),
            _ => (loc.Format("export.failed", ex.Message), false),
        };
}

public sealed partial class ExportViewModel : ViewModelBase
{
    private readonly ExportService _export;
    private readonly ValidationService _validation;
    private readonly ProfileSession _session;
    private readonly IFilePicker _picker;
    private readonly ILauncher _launcher;
    private readonly StatusService _status;

    public ExportViewModel(
        ExportService export,
        ValidationService validation,
        ProfileSession session,
        IFilePicker picker,
        ILauncher launcher,
        StatusService status
    )
    {
        _export = export;
        _validation = validation;
        _session = session;
        _picker = picker;
        _launcher = launcher;
        _status = status;
        FormatOptions =
        [
            new("Pdf", literal: "PDF"),
            new("Png", literal: "PNG"),
            new("Html", literal: "HTML"),
            new("Json", literal: "JSON"),
        ];
        DpiOptions = [new("150", literal: "150 DPI"), new("300", literal: "300 DPI")];
        _selectedFormat = FormatOptions[0];
        _selectedDpi = DpiOptions[1];
        _session.Changed += (_, _) => OnPropertyChanged(nameof(HasProfile));
    }

    public IReadOnlyList<LocalizedOption> FormatOptions { get; }
    public IReadOnlyList<LocalizedOption> DpiOptions { get; }

    public bool HasProfile => _session.Current is not null;

    public string ChromeUrl => ExportErrors.ChromeUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPng))]
    private LocalizedOption _selectedFormat;

    [ObservableProperty]
    private LocalizedOption _selectedDpi;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _error = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private bool _chromeMissing;

    [ObservableProperty]
    private string _lastPath = "";

    public bool IsPng => SelectedFormat.Value == "Png";

    public bool HasError => Error.Length > 0;

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand]
    private void OpenChromeDownload() => _launcher.OpenUrl(ChromeUrl);

    [RelayCommand]
    public async Task ExportAsync()
    {
        Error = "";
        ChromeMissing = false;
        if (_session.Current is not { } profile)
        {
            Error = Loc["export.no_profile"];
            return;
        }
        var validation = _validation.Validate(profile);
        if (!validation.IsValid)
        {
            Error = Loc["export.invalid"] + "\n" + string.Join("\n", validation.Issues.Where(i => i.IsError).Select(i => "✗ " + i.Message));
            _status.Error(Loc["export.invalid"]);
            return;
        }

        var format = Enum.Parse<ExportFormat>(SelectedFormat.Value);
        var ext = format.ToString().ToLowerInvariant();
        var baseName = ProfileFileName.Sanitize(profile.Name);
        var path = await _picker.PickSaveAsync(Loc["export.title"], baseName + "." + ext, ext);
        if (path is null)
            return;

        IsBusy = true;
        try
        {
            await _session.FlushAsync();
            var result = await _export.ExportAsync(new ExportRequest(profile, format, baseName, int.Parse(SelectedDpi.Value)));
            await File.WriteAllBytesAsync(path, result.Content);
            LastPath = path;
            _status.Info(Loc.Format("status.exported", path));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var (message, chrome) = ExportErrors.Describe(ex, Loc);
            Error = message;
            ChromeMissing = chrome;
            _status.Error(message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed partial class CoverLetterViewModel : ViewModelBase
{
    private readonly CoverLetterService _service;
    private readonly ExportService _export;
    private readonly ValidationService _validation;
    private readonly ProfileSession _session;
    private readonly IClipboardService _clipboard;
    private readonly IFilePicker _picker;
    private readonly ILauncher _launcher;
    private readonly StatusService _status;

    public CoverLetterViewModel(
        CoverLetterService service,
        ExportService export,
        ValidationService validation,
        ProfileSession session,
        IClipboardService clipboard,
        IFilePicker picker,
        ILauncher launcher,
        StatusService status
    )
    {
        _service = service;
        _export = export;
        _validation = validation;
        _session = session;
        _clipboard = clipboard;
        _picker = picker;
        _launcher = launcher;
        _status = status;
        ToneOptions = [new("formal", "tone.formal"), new("friendly", "tone.friendly"), new("bold", "tone.bold")];
        _selectedTone = ToneOptions[0];
        _selectedLanguage = LocalizedOption.Languages[0];
        _session.Changed += (_, _) => OnPropertyChanged(nameof(HasProfile));
    }

    public IReadOnlyList<LocalizedOption> ToneOptions { get; }
    public IReadOnlyList<LocalizedOption> LanguageOptions => LocalizedOption.Languages;
    public IReadOnlyList<ShortcutLink> Shortcuts => LeadGenerator.ShortcutLinks;
    public string ChromeUrl => ExportErrors.ChromeUrl;
    public bool HasProfile => _session.Current is not null;

    [ObservableProperty]
    private string _company = "";

    [ObservableProperty]
    private string _position = "";

    /// <summary>Comma or line separated job-posting keywords.</summary>
    [ObservableProperty]
    private string _keywords = "";

    [ObservableProperty]
    private LocalizedOption _selectedTone;

    [ObservableProperty]
    private LocalizedOption _selectedLanguage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLead))]
    private string _lead = "";

    /// <summary>Text pasted back from the AI.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordCount))]
    private string _body = "";

    [ObservableProperty]
    private CoverLetter? _saved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = "";

    [ObservableProperty]
    private bool _chromeMissing;

    public bool HasLead => Lead.Length > 0;
    public bool HasError => Error.Length > 0;
    public int WordCount => CoverLetterService.CountWords(Body);

    public void SetDefaultLanguage(string language) => SelectedLanguage = LocalizedOption.Find(LanguageOptions, language)!;

    [RelayCommand]
    private void Generate()
    {
        if (_session.Current is not { } profile)
        {
            _status.Error(Loc["export.no_profile"]);
            return;
        }
        Lead = _service.GenerateLead(
            profile,
            new CoverLetterOptions
            {
                Company = Company.Trim(),
                Position = Position.Trim(),
                Keywords = ItemViewModel.ParseTags(Keywords) ?? [],
                Tone = SelectedTone.Value,
                Language = SelectedLanguage.Value,
            }
        );
        _status.Info(Loc["status.lead_generated"]);
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (!HasLead)
            Generate();
        if (!HasLead)
            return;
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
    private async Task PasteAsync()
    {
        var text = await _clipboard.GetTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
            Body = text;
    }

    private CoverLetter? BuildLetter() =>
        _session.Current is { } p
            ? new CoverLetter
            {
                ProfileId = p.Id,
                Company = Company.Trim(),
                Position = Position.Trim(),
                Language = SelectedLanguage.Value,
                Body = Body.Trim(),
            }
            : null;

    [RelayCommand]
    private void Save()
    {
        Error = "";
        if (BuildLetter() is not { } letter)
        {
            Error = Loc["export.no_profile"];
            return;
        }
        var result = _validation.ValidateCoverLetter(letter);
        if (!result.IsValid)
        {
            Error = string.Join("\n", result.Issues.Where(i => i.IsError).Select(i => "✗ " + i.Message));
            return;
        }
        Saved = letter;
        _status.Info(result.Issues.Count > 0 ? string.Join(" ", result.Issues.Select(i => i.Message)) : Loc["status.letter_saved"]);
    }

    [RelayCommand]
    private Task ExportPdfAsync() => ExportAsync(ExportFormat.Pdf);

    [RelayCommand]
    private Task ExportHtmlAsync() => ExportAsync(ExportFormat.Html);

    [RelayCommand]
    private void OpenChromeDownload() => _launcher.OpenUrl(ChromeUrl);

    public async Task ExportAsync(ExportFormat format)
    {
        Error = "";
        ChromeMissing = false;
        Save();
        if (Saved is not { } letter || _session.Current is not { } profile)
            return;
        var ext = format == ExportFormat.Pdf ? "pdf" : "html";
        var name = ProfileFileName.Sanitize($"{profile.Name}-cover-letter", "cover-letter");
        var path = await _picker.PickSaveAsync(Loc["cover.export"], name + "." + ext, ext);
        if (path is null)
            return;
        try
        {
            var result = await _export.ExportCoverLetterAsync(profile, letter, format, name);
            await File.WriteAllBytesAsync(path, result.Content);
            _status.Info(Loc.Format("status.exported", path));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var (message, chrome) = ExportErrors.Describe(ex, Loc);
            Error = message;
            ChromeMissing = chrome;
            _status.Error(message);
        }
    }
}
