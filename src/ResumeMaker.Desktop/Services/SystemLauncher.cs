using System.Diagnostics;

namespace ResumeMaker.Desktop.Services;

public sealed class SystemLauncher : ILauncher
{
    public void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            Start(url);
    }

    public void OpenFile(string path) => Start(path);

    private static void Start(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception) when (OperatingSystem.IsLinux())
        {
            Process.Start("xdg-open", target);
        }
    }
}
