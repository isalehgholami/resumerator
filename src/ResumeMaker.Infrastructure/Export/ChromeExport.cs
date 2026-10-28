using System.Diagnostics;
using System.Globalization;
using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Application.Interfaces;

namespace ResumeMaker.Infrastructure.Export;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Abstraction over process spawning so the exporter can be tested without Chrome.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken ct = default
    );
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken ct = default
    )
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in arguments)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi) ?? throw new ExportFailedException("Failed to start the browser process.");
        var stdout = proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = proc.StandardError.ReadToEndAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillTree(proc);
            if (ct.IsCancellationRequested)
                throw;
            throw new ExportFailedException($"Browser export timed out after {timeout.TotalSeconds:0} seconds.");
        }

        return new ProcessResult(proc.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    private static void KillTree(Process proc)
    {
        try { proc.Kill(entireProcessTree: true); }
        catch (Exception) { /* already exited */ }
    }
}

/// <summary>Finds Chrome / Edge / Chromium: settings path first, then well-known locations and PATH.</summary>
public sealed class ChromeLocator(Func<string, bool>? fileExists = null)
{
    private readonly Func<string, bool> _exists = fileExists ?? File.Exists;

    public string Locate(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && _exists(configuredPath))
            return configuredPath;

        foreach (var candidate in Candidates())
        {
            if (_exists(candidate))
                return candidate;
        }
        throw new ChromeNotFoundException();
    }

    public IEnumerable<string> Candidates()
    {
        if (OperatingSystem.IsWindows())
        {
            var roots = new[]
            {
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                Environment.GetEnvironmentVariable("LocalAppData"),
            }.Where(r => !string.IsNullOrEmpty(r)).Select(r => r!);
            foreach (var root in roots)
                yield return Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe");
            foreach (var root in roots)
                yield return Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            yield return "/Applications/Chromium.app/Contents/MacOS/Chromium";
            yield return "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";
        }
        else
        {
            foreach (var name in new[] { "google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "microsoft-edge" })
            {
                foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    yield return Path.Combine(dir, name);
            }
        }
    }
}

/// <summary>PDF and PNG export through headless Chrome (architecture 10.2, 14.4).</summary>
public sealed class ChromeHeadlessExporter(
    ISettingsStore settings,
    IProcessRunner runner,
    ChromeLocator? locator = null,
    Func<bool>? noSandbox = null
) : IPdfExporter, IPngExporter
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private const int A4WidthPx = 794;
    private const int A4HeightPx = 1123;

    private readonly ChromeLocator _locator = locator ?? new ChromeLocator();
    private readonly Func<bool> _noSandbox = noSandbox ?? DefaultNoSandbox;

    public Task<ExportResult> ExportPdfAsync(string html, string fileName, CancellationToken ct = default) =>
        RunAsync(html, fileName, "pdf", "application/pdf", (output, profileDir, uri) => BuildPdfArgs(output, profileDir, uri, _noSandbox()), ct);

    public Task<ExportResult> ExportPngAsync(string html, string fileName, int dpi, CancellationToken ct = default)
    {
        if (dpi is < 72 or > 600)
            throw new ArgumentOutOfRangeException(nameof(dpi), "dpi must be between 72 and 600.");
        return RunAsync(html, fileName, "png", "image/png", (output, profileDir, uri) => BuildPngArgs(output, profileDir, uri, dpi, _noSandbox()), ct);
    }

    private async Task<ExportResult> RunAsync(
        string html,
        string fileName,
        string ext,
        string contentType,
        Func<string, string, string, List<string>> argsFactory,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(html);
        var chrome = _locator.Locate((await settings.LoadAsync(ct).ConfigureAwait(false)).ChromePath);

        var workDir = Path.Combine(Path.GetTempPath(), "rm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var htmlPath = Path.Combine(workDir, "input.html");
            await File.WriteAllTextAsync(htmlPath, html, new System.Text.UTF8Encoding(false), ct).ConfigureAwait(false);
            var outPath = Path.Combine(workDir, "output." + ext);
            var profileDir = Path.Combine(workDir, "profile");

            var args = argsFactory(outPath, profileDir, new Uri(htmlPath).AbsoluteUri);
            var result = await runner.RunAsync(chrome, args, Timeout, ct).ConfigureAwait(false);

            if (!File.Exists(outPath) || new FileInfo(outPath).Length == 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StdErr) ? $"exit code {result.ExitCode}" : Truncate(result.StdErr);
                throw new ExportFailedException($"Browser did not produce the {ext.ToUpperInvariant()} file: {detail}");
            }

            var bytes = await File.ReadAllBytesAsync(outPath, ct).ConfigureAwait(false);
            return new ExportResult(bytes, contentType, $"{SafeFileName(fileName)}.{ext}");
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); }
            catch (Exception) { /* best effort: chrome may still hold files briefly */ }
        }
    }

    internal static List<string> BuildPdfArgs(string outputPath, string profileDir, string fileUri, bool noSandbox) =>
        Common(profileDir, noSandbox, [$"--print-to-pdf={outputPath}"], fileUri);

    internal static List<string> BuildPngArgs(string outputPath, string profileDir, string fileUri, int dpi, bool noSandbox)
    {
        var scale = (dpi / 96.0).ToString("0.###", CultureInfo.InvariantCulture);
        return Common(
            profileDir,
            noSandbox,
            [
                $"--screenshot={outputPath}",
                $"--window-size={A4WidthPx},{A4HeightPx}",
                $"--force-device-scale-factor={scale}",
                "--hide-scrollbars",
            ],
            fileUri
        );
    }

    private static List<string> Common(string profileDir, bool noSandbox, string[] specific, string? fileUri)
    {
        var args = new List<string>
        {
            "--headless=new",
            "--disable-gpu",
            "--disable-extensions",
            "--no-first-run",
            $"--user-data-dir={profileDir}",
            "--virtual-time-budget=5000",
        };
        if (specific[0].StartsWith("--print-to-pdf", StringComparison.Ordinal))
            args.Add("--no-pdf-header-footer");
        if (noSandbox)
            args.Add("--no-sandbox");
        args.AddRange(specific);
        if (fileUri is not null)
            args.Add(fileUri);
        return args;
    }

    private static bool DefaultNoSandbox()
    {
        if (Environment.GetEnvironmentVariable("RESUMEMAKER_NO_SANDBOX") == "1")
            return true;
        return OperatingSystem.IsLinux() && Environment.UserName == "root";
    }

    private static string SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "resume";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "resume" : cleaned;
    }

    private static string Truncate(string s) => s.Length <= 500 ? s.Trim() : s[..500].Trim() + "...";
}
