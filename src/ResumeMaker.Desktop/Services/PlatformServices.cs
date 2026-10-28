namespace ResumeMaker.Desktop.Services;

/// <summary>Clipboard access, implemented by the view layer (TopLevel.Clipboard).</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string?> GetTextAsync();
}

/// <summary>File dialogs, implemented by the view layer (StorageProvider).</summary>
public interface IFilePicker
{
    /// <param name="extensions">Extensions without dot, e.g. "json".</param>
    Task<string?> PickOpenAsync(string title, params string[] extensions);

    Task<string?> PickSaveAsync(string title, string suggestedFileName, string extension);
}

/// <summary>Opens URLs and local files with the OS default handler.</summary>
public interface ILauncher
{
    void OpenUrl(string url);
    void OpenFile(string path);
}

/// <summary>Applies UI-only theme (never affects exports).</summary>
public interface IThemeService
{
    /// <param name="theme">"system" | "light" | "dark"</param>
    void Apply(string theme);
}
