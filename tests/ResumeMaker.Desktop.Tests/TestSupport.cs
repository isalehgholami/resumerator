using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Application.Services;
using ResumeMaker.Desktop;
using ResumeMaker.Desktop.Localization;
using ResumeMaker.Desktop.Services;
using ResumeMaker.Infrastructure;

namespace ResumeMaker.Desktop.Tests;

/// <summary>Localizer.Instance is process-wide, so all tests that touch it run serially.</summary>
[CollectionDefinition("Desktop", DisableParallelization = true)]
public sealed class DesktopCollection;

public sealed class FakeClipboard : IClipboardService
{
    public string? Text { get; set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync() => Task.FromResult(Text);
}

public sealed class FakePicker : IFilePicker
{
    public Queue<string?> Open { get; } = new();
    public Queue<string?> Save { get; } = new();
    public string? LastSuggestedName { get; private set; }

    public Task<string?> PickOpenAsync(string title, params string[] extensions) =>
        Task.FromResult(Open.Count > 0 ? Open.Dequeue() : null);

    public Task<string?> PickSaveAsync(string title, string suggestedFileName, string extension)
    {
        LastSuggestedName = suggestedFileName;
        return Task.FromResult(Save.Count > 0 ? Save.Dequeue() : null);
    }
}

public sealed class FakeLauncher : ILauncher
{
    public List<string> Urls { get; } = [];
    public List<string> Files { get; } = [];

    public void OpenUrl(string url) => Urls.Add(url);

    public void OpenFile(string path) => Files.Add(path);
}

public sealed class FakeTheme : IThemeService
{
    public string? Applied { get; private set; }

    public void Apply(string theme) => Applied = theme;
}

public sealed class ThrowingPdf(Exception ex) : IPdfExporter, IPngExporter
{
    public Task<ExportResult> ExportPdfAsync(string html, string fileName, CancellationToken ct = default) => throw ex;

    public Task<ExportResult> ExportPngAsync(string html, string fileName, int dpi, CancellationToken ct = default) => throw ex;
}

/// <summary>Temp data directory + fully composed app (no window).</summary>
public sealed class TestApp : IDisposable
{
    public TestApp()
    {
        Dir = Path.Combine(Path.GetTempPath(), "rm-desktop-tests-" + Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(Dir);
        Infra = InfrastructureFactory.Create(Paths);
        Root = CompositionRoot.Create(Infra, Clipboard, Picker, Launcher, Theme, Path.Combine(Dir, "preview"));
        Root.Session.SaveDelay = TimeSpan.Zero;
        Root.Preview.Debounce = TimeSpan.Zero;
        Localizer.Instance.SetLanguage("en");
    }

    public string Dir { get; }
    public AppPaths Paths { get; }
    public InfrastructureServices Infra { get; }
    public FakeClipboard Clipboard { get; } = new();
    public FakePicker Picker { get; } = new();
    public FakeLauncher Launcher { get; } = new();
    public FakeTheme Theme { get; } = new();
    public CompositionRoot Root { get; }

    public void Dispose()
    {
        Root.Dispose();
        Localizer.Instance.SetLanguage("en");
        try
        {
            Directory.Delete(Dir, true);
        }
        catch (IOException) { }
    }

    public static async Task<T> Eventually<T>(Func<T?> probe, int timeoutMs = 15000)
        where T : class
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < end)
        {
            if (probe() is { } v)
                return v;
            await Task.Delay(20);
        }
        throw new TimeoutException("Condition not met in time.");
    }
}
