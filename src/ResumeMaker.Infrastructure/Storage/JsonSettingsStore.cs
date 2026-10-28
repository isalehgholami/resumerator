using System.Text.Json;
using ResumeMaker.Core;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Infrastructure.Storage;

public sealed class JsonSettingsStore(AppPaths paths) : ISettingsStore
{
    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(paths.SettingsPath))
                return new AppSettings();
            var bytes = await File.ReadAllBytesAsync(paths.SettingsPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize(bytes, ResumeMakerJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var json = JsonSerializer.Serialize(settings, ResumeMakerJsonContext.Default.AppSettings);
        return FileUtil.WriteAtomicTextAsync(paths.SettingsPath, json, ct);
    }
}
