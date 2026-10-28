using System.Text.Json;
using ResumeMaker.Core;
using ResumeMaker.Core.Application.Interfaces;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Infrastructure.Storage;

/// <summary>profiles/{guid}.json per profile plus profiles/index.json for fast listing.</summary>
public sealed class JsonProfileRepository(AppPaths paths) : IProfileReader, IProfileWriter
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(paths.ProfilesDir))
                return [];

            var files = Directory
                .EnumerateFiles(paths.ProfilesDir, "*.json")
                .Where(f => Guid.TryParseExact(Path.GetFileNameWithoutExtension(f), "D", out _))
                .ToList();

            var indexed = await TryReadIndexAsync(ct).ConfigureAwait(false);
            if (indexed is not null
                && indexed.Count == files.Count
                && indexed.All(s => File.Exists(paths.ProfilePath(s.Id))))
            {
                return indexed.OrderByDescending(s => s.UpdatedAt).ToList();
            }

            var rebuilt = new List<ProfileSummary>();
            foreach (var file in files)
            {
                var profile = await TryReadProfileAsync(file, ct).ConfigureAwait(false);
                if (profile is not null)
                    rebuilt.Add(ToSummary(profile));
            }
            rebuilt = rebuilt.OrderByDescending(s => s.UpdatedAt).ToList();
            await WriteIndexAsync(rebuilt, ct).ConfigureAwait(false);
            return rebuilt;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Profile?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await TryReadProfileAsync(paths.ProfilePath(id), ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(Profile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            FileUtil.EnsurePrivateDirectory(paths.ProfilesDir);
            var json = JsonSerializer.Serialize(profile, ResumeMakerJsonContext.Default.Profile);
            await FileUtil.WriteAtomicTextAsync(paths.ProfilePath(profile.Id), json, ct).ConfigureAwait(false);

            var list = (await TryReadIndexAsync(ct).ConfigureAwait(false)) ?? [];
            list.RemoveAll(s => s.Id == profile.Id);
            list.Add(ToSummary(profile));
            await WriteIndexAsync(list.OrderByDescending(s => s.UpdatedAt).ToList(), ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = paths.ProfilePath(id);
            var existed = File.Exists(path);
            if (existed)
                File.Delete(path);

            var list = await TryReadIndexAsync(ct).ConfigureAwait(false);
            if (list is not null && list.RemoveAll(s => s.Id == id) > 0)
                await WriteIndexAsync(list, ct).ConfigureAwait(false);
            return existed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ProfileSummary ToSummary(Profile p) => new(p.Id, p.Name, p.Language, p.TemplateId, p.UpdatedAt);

    private async Task<List<ProfileSummary>?> TryReadIndexAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(paths.ProfileIndexPath))
                return null;
            var bytes = await File.ReadAllBytesAsync(paths.ProfileIndexPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize(bytes, ResumeMakerJsonContext.Default.ListProfileSummary);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private Task WriteIndexAsync(List<ProfileSummary> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, ResumeMakerJsonContext.Default.ListProfileSummary);
        return FileUtil.WriteAtomicTextAsync(paths.ProfileIndexPath, json, ct);
    }

    private static async Task<Profile?> TryReadProfileAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize(bytes, ResumeMakerJsonContext.Default.Profile);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
