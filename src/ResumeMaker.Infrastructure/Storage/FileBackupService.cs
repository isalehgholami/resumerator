using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ResumeMaker.Core;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Infrastructure.Storage;

/// <summary>backups/{id}.{timestamp}.json snapshots; unchanged content is skipped, oldest are pruned.</summary>
public sealed class FileBackupService(AppPaths paths, ISettingsStore settings) : IBackupService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string?> SnapshotAsync(Profile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var bytes = FileUtil.Utf8NoBom.GetBytes(
            JsonSerializer.Serialize(profile, ResumeMakerJsonContext.Default.Profile));
        var max = Math.Max(1, (await settings.LoadAsync(ct).ConfigureAwait(false)).BackupMaxCount);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(paths.BackupsDir);
            var id = profile.Id.ToString("D");
            var existing = ListBackups(id);

            if (existing.Count > 0)
            {
                var last = await File.ReadAllBytesAsync(existing[^1], ct).ConfigureAwait(false);
                if (SHA256.HashData(last).AsSpan().SequenceEqual(SHA256.HashData(bytes)))
                    return null;
            }

            var now = DateTime.UtcNow;
            string path;
            do
            {
                path = Path.Combine(paths.BackupsDir,
                    $"{id}.{now.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture)}.json");
                now = now.AddMilliseconds(1);
            }
            while (File.Exists(path));

            await FileUtil.WriteAtomicAsync(path, bytes, ct).ConfigureAwait(false);

            existing = ListBackups(id);
            for (var i = 0; i < existing.Count - max; i++)
            {
                try { File.Delete(existing[i]); } catch (IOException) { }
            }
            return path;
        }
        finally
        {
            _gate.Release();
        }
    }

    private List<string> ListBackups(string id) =>
        Directory.EnumerateFiles(paths.BackupsDir, id + ".*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
}
