using System.Text.RegularExpressions;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Desktop.Services;

/// <summary>
/// Writes the rendered resume to a temp file and opens it in the default browser. While active, the file is
/// rewritten after every edit; the temp copy carries a meta refresh so the browser reloads it every 2 seconds.
/// The refresh tag is injected for the preview copy only and never reaches an export.
/// </summary>
public sealed partial class PreviewService : IDisposable
{
    private readonly ProfileSession _session;
    private readonly TemplateService _templates;
    private readonly ILauncher _launcher;
    private readonly StatusService _status;
    private CancellationTokenSource? _pending;

    public PreviewService(ProfileSession session, TemplateService templates, ILauncher launcher, StatusService status, string? directory = null)
    {
        _session = session;
        _templates = templates;
        _launcher = launcher;
        _status = status;
        var dir = directory ?? Path.Combine(Path.GetTempPath(), "ResumeMaker");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, $"preview-{Environment.ProcessId}.html");
        _session.Changed += OnChanged;
    }

    public string FilePath { get; }

    public bool IsActive { get; private set; }

    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(300);

    public int RefreshSeconds { get; set; } = 2;

    /// <summary>Renders once, writes the temp file and opens it in the browser.</summary>
    public async Task OpenAsync()
    {
        if (!await WriteAsync())
            return;
        IsActive = true;
        _launcher.OpenFile(FilePath);
    }

    public async Task<bool> WriteAsync()
    {
        var profile = _session.Current;
        if (profile is null)
            return false;
        try
        {
            var html = await _templates.RenderAsync(new TemplateRenderRequest(profile));
            await WriteAtomicAsync(InjectRefresh(html, RefreshSeconds));
            return true;
        }
        catch (Exception ex)
        {
            _status.Error(ex.Message);
            return false;
        }
    }

    /// <summary>Writes via a temp file so the browser never reads a half-written preview.</summary>
    private async Task WriteAtomicAsync(string content)
    {
        var tmp = FilePath + ".tmp";
        await File.WriteAllTextAsync(tmp, content);
        File.Move(tmp, FilePath, overwrite: true);
    }

    public static string InjectRefresh(string html, int seconds)
    {
        if (seconds <= 0)
            return html;
        var tag = $"<meta http-equiv=\"refresh\" content=\"{seconds}\">";
        var m = HeadOpen().Match(html);
        return m.Success ? html.Insert(m.Index + m.Length, tag) : tag + html;
    }

    [GeneratedRegex("<head[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadOpen();

    private void OnChanged(Profile? profile, object? source)
    {
        if (!IsActive)
            return;
        _pending?.Cancel();
        var cts = _pending = new CancellationTokenSource();
        _ = RefreshLaterAsync(cts.Token);
    }

    private async Task RefreshLaterAsync(CancellationToken ct)
    {
        try
        {
            if (Debounce > TimeSpan.Zero)
                await Task.Delay(Debounce, ct);
            ct.ThrowIfCancellationRequested();
            await WriteAsync();
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _session.Changed -= OnChanged;
        _pending?.Cancel();
        try
        {
            File.Delete(FilePath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
