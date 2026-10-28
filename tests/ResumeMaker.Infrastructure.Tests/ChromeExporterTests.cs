using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure.Export;
using ResumeMaker.Infrastructure.Storage;

namespace ResumeMaker.Infrastructure.Tests;

public class ChromeExporterTests
{
    private sealed class FakeRunner(bool produceOutput = true) : IProcessRunner
    {
        public string? FileName;
        public List<string> Args = [];
        public string? HtmlSeen;
        public string? WorkDir;

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct = default)
        {
            FileName = fileName;
            Args = arguments.ToList();
            var uri = new Uri(arguments[^1]);
            HtmlSeen = File.ReadAllText(uri.LocalPath);
            WorkDir = Path.GetDirectoryName(uri.LocalPath);
            if (produceOutput)
            {
                var o = arguments.First(a => a.StartsWith("--print-to-pdf=") || a.StartsWith("--screenshot="));
                File.WriteAllBytes(o[(o.IndexOf('=') + 1)..], [1, 2, 3]);
            }
            return Task.FromResult(new ProcessResult(0, "", "boom"));
        }
    }

    private sealed class FakeSettings(string? chrome) : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(new AppSettings { ChromePath = chrome });
        public Task SaveAsync(AppSettings s, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ChromeHeadlessExporter Make(FakeRunner runner, bool noSandbox = false) =>
        new(new FakeSettings("C:\\fake\\chrome.exe"), runner, new ChromeLocator(_ => true), () => noSandbox);

    [Fact]
    public async Task Pdf_BuildsSafeArguments_AndCleansUp()
    {
        var runner = new FakeRunner();
        var result = await Make(runner).ExportPdfAsync("<html>ü</html>", "my resume");

        Assert.Equal("C:\\fake\\chrome.exe", runner.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("my resume.pdf", result.FileName);
        Assert.Equal([1, 2, 3], result.Content);
        Assert.Equal("<html>ü</html>", runner.HtmlSeen);

        Assert.Contains("--headless=new", runner.Args);
        Assert.Contains("--disable-gpu", runner.Args);
        Assert.Contains("--disable-extensions", runner.Args);
        Assert.Contains("--no-first-run", runner.Args);
        Assert.Contains("--virtual-time-budget=5000", runner.Args);
        Assert.Contains("--no-pdf-header-footer", runner.Args);
        Assert.DoesNotContain("--no-sandbox", runner.Args);
        Assert.Contains(runner.Args, a => a.StartsWith("--user-data-dir="));
        Assert.Contains(runner.Args, a => a.StartsWith("--print-to-pdf="));
        Assert.StartsWith("file:///", runner.Args[^1]);
        Assert.False(Directory.Exists(runner.WorkDir));
    }

    [Fact]
    public async Task Png_UsesWindowSizeAndScale()
    {
        var runner = new FakeRunner();
        var result = await Make(runner).ExportPngAsync("<html/>", "cv/x", 192);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal("cv_x.png", result.FileName);
        Assert.Contains("--window-size=794,1123", runner.Args);
        Assert.Contains("--force-device-scale-factor=2", runner.Args);
        Assert.Contains(runner.Args, a => a.StartsWith("--screenshot="));
        Assert.DoesNotContain("--no-pdf-header-footer", runner.Args);
    }

    [Fact]
    public async Task NoSandbox_OnlyWhenRequested()
    {
        var runner = new FakeRunner();
        await Make(runner, noSandbox: true).ExportPdfAsync("<html/>", "a");
        Assert.Contains("--no-sandbox", runner.Args);
    }

    [Fact]
    public async Task MissingOutput_ThrowsExportFailed_AndStillCleansUp()
    {
        var runner = new FakeRunner(produceOutput: false);
        var ex = await Assert.ThrowsAsync<ExportFailedException>(() => Make(runner).ExportPdfAsync("<html/>", "a"));
        Assert.Contains("boom", ex.Message);
        Assert.False(Directory.Exists(runner.WorkDir));
    }

    [Fact]
    public async Task NoChrome_Throws()
    {
        var exporter = new ChromeHeadlessExporter(new FakeSettings(null), new FakeRunner(), new ChromeLocator(_ => false));
        await Assert.ThrowsAsync<ChromeNotFoundException>(() => exporter.ExportPdfAsync("<html/>", "a"));
    }

    [Fact]
    public void Locator_PrefersConfiguredPath_ThenCandidates()
    {
        Assert.Equal("/custom/chrome", new ChromeLocator(p => p == "/custom/chrome").Locate("/custom/chrome"));
        var loc = new ChromeLocator(_ => true);
        Assert.NotEqual("/missing", loc.Locate(null));
    }

    [Fact]
    public void Factory_WiresEverything()
    {
        using var dir = new TempDir();
        var s = InfrastructureFactory.Create(dir.Paths);
        Assert.NotNull(s.Engine);
        Assert.NotNull(s.Chrome);
        Assert.Equal(dir.Paths.Root, s.Paths.Root);
    }
}
