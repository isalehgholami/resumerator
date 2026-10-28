using ResumeMaker.Core.Application.Interfaces;

namespace ResumeMaker.Desktop.Services;

/// <summary>Snapshots the current profile; the view layer drives <see cref="TickAsync"/> from a DispatcherTimer.</summary>
public sealed class BackupCoordinator(ProfileSession session, IBackupService backup, ISettingsStore settings)
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    public async Task<TimeSpan> GetIntervalAsync()
    {
        try
        {
            var s = await settings.LoadAsync();
            return s.BackupIntervalMinutes > 0 ? TimeSpan.FromMinutes(s.BackupIntervalMinutes) : DefaultInterval;
        }
        catch (Exception)
        {
            return DefaultInterval;
        }
    }

    /// <summary>Returns the snapshot path, or null when nothing was written.</summary>
    public async Task<string?> TickAsync()
    {
        var profile = session.Current;
        if (profile is null)
            return null;
        try
        {
            return await backup.SnapshotAsync(profile);
        }
        catch (Exception)
        {
            return null; // Backups are best-effort and must never disturb editing.
        }
    }
}
