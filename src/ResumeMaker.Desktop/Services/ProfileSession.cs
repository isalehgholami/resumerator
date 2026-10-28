using ResumeMaker.Core.Application.Services;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Desktop.Services;

/// <summary>
/// The profile currently being edited. All edits go through <see cref="Update"/>, which notifies
/// listeners (preview, other pages) and schedules a debounced save via <see cref="ProfileService"/>.
/// </summary>
public sealed class ProfileSession(ProfileService profiles, StatusService status)
{
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private bool _dirty;

    public Profile? Current { get; private set; }

    /// <summary>Delay between the last edit and the autosave.</summary>
    public TimeSpan SaveDelay { get; set; } = TimeSpan.FromMilliseconds(800);

    /// <summary>Raised with the new profile and the object that caused the change (null = opened/replaced).</summary>
    public event Action<Profile?, object?>? Changed;

    public event Action? Saved;

    public Exception? LastSaveError { get; private set; }

    public void Open(Profile? profile)
    {
        CancelPending();
        _dirty = false;
        Current = profile;
        Changed?.Invoke(profile, null);
    }

    public void Update(Profile profile, object? source)
    {
        Current = profile;
        _dirty = true;
        Changed?.Invoke(profile, source);
        ScheduleSave();
    }

    /// <summary>Saves immediately if there are pending edits.</summary>
    public async Task FlushAsync()
    {
        CancelPending();
        await SaveNowAsync();
    }

    private void ScheduleSave()
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            _pending?.Cancel();
            cts = _pending = new CancellationTokenSource();
        }
        _ = SaveAfterDelayAsync(cts.Token);
    }

    private void CancelPending()
    {
        lock (_gate)
        {
            _pending?.Cancel();
            _pending = null;
        }
    }

    private async Task SaveAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            if (SaveDelay > TimeSpan.Zero)
                await Task.Delay(SaveDelay, ct);
            ct.ThrowIfCancellationRequested();
            await SaveNowAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task SaveNowAsync()
    {
        var snapshot = Current;
        if (snapshot is null || !_dirty)
            return;
        _dirty = false;
        try
        {
            var saved = await profiles.SaveAsync(snapshot);
            LastSaveError = null;
            if (Current is { } cur && cur.Id == saved.Id)
                Current = cur with { UpdatedAt = saved.UpdatedAt };
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            _dirty = true;
            LastSaveError = ex;
            status.Error(ex.Message);
        }
    }
}
