namespace ResumeMaker.Infrastructure;

/// <summary>Resolves the on-disk layout of the application data directory (architecture section 9.1).</summary>
public sealed class AppPaths
{
    public const string DataEnvVar = "RESUMEMAKER_DATA";

    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }
    public string SettingsPath => Path.Combine(Root, "settings.json");
    public string ProfilesDir => Path.Combine(Root, "profiles");
    public string ProfileIndexPath => Path.Combine(ProfilesDir, "index.json");
    public string BackupsDir => Path.Combine(Root, "backups");
    public string CustomTemplatesDir => Path.Combine(Root, "templates", "custom");

    public string ProfilePath(Guid id) => Path.Combine(ProfilesDir, id.ToString("D") + ".json");

    /// <summary>RESUMEMAKER_DATA override, else the per-OS default.</summary>
    public static AppPaths Default() => new(ResolveDefaultRoot());

    public static string ResolveDefaultRoot()
    {
        var overrideDir = Environment.GetEnvironmentVariable(DataEnvVar);
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;

        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResumeMaker");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            home = Environment.GetEnvironmentVariable("HOME") ?? ".";

        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "ResumeMaker");

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = string.IsNullOrWhiteSpace(xdg) ? Path.Combine(home, ".local", "share") : xdg;
        return Path.Combine(dataHome, "ResumeMaker");
    }
}
