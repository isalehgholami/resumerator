using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;

namespace ResumeMaker.Desktop.Services;

public sealed class AvaloniaClipboardService(Func<TopLevel?> topLevel) : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (topLevel()?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetTextAsync()
    {
        if (topLevel()?.Clipboard is not { } clipboard)
            return null;
#pragma warning disable CS0618 // TryGetTextAsync only exists in Avalonia 12; GetTextAsync is the 11.x API.
        return await clipboard.GetTextAsync();
#pragma warning restore CS0618
    }
}

public sealed class AvaloniaFilePicker(Func<TopLevel?> topLevel) : IFilePicker
{
    public async Task<string?> PickOpenAsync(string title, params string[] extensions)
    {
        if (topLevel()?.StorageProvider is not { } storage)
            return null;
        var files = await storage.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = title, AllowMultiple = false, FileTypeFilter = [Filter(extensions)] }
        );
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveAsync(string title, string suggestedFileName, string extension)
    {
        if (topLevel()?.StorageProvider is not { } storage)
            return null;
        var file = await storage.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedFileName,
                DefaultExtension = extension,
                ShowOverwritePrompt = true,
                FileTypeChoices = [Filter(extension)],
            }
        );
        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType Filter(params string[] extensions) =>
        new(string.Join(", ", extensions).ToUpperInvariant()) { Patterns = [.. extensions.Select(e => "*." + e)] };
}

public sealed class AvaloniaThemeService : IThemeService
{
    public void Apply(string theme)
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant = theme switch
        {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
    }
}
