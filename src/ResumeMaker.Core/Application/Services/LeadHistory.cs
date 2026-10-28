namespace ResumeMaker.Core.Application.Services;

public sealed record LeadHistoryEntry(DateTimeOffset At, string Kind, string Prompt);

/// <summary>In-memory history of generated prompts for debugging (FR-35).</summary>
public sealed class LeadHistory(int capacity = 50)
{
    private readonly object _gate = new();
    private readonly List<LeadHistoryEntry> _entries = [];

    public void Add(string kind, string prompt, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            _entries.Add(new LeadHistoryEntry(at ?? DateTimeOffset.UtcNow, kind, prompt));
            if (_entries.Count > capacity)
            {
                _entries.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<LeadHistoryEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public LeadHistoryEntry? Last
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count == 0 ? null : _entries[^1];
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
