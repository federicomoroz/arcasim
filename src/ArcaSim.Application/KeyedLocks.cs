namespace ArcaSim.Application;

/// <summary>
/// One async lock per key, created on first use and dropped as soon as nobody
/// holds it or waits for it, so keys a request chooses (a certificate number,
/// a point of sale) do not pile up for the life of the process.
/// </summary>
public sealed class KeyedLocks<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, Entry> _entries = [];

    public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken ct)
    {
        Entry entry;
        lock (_entries)
        {
            if (!_entries.TryGetValue(key, out entry!)) _entries[key] = entry = new Entry();
            entry.Users++;
        }

        try
        {
            await entry.Gate.WaitAsync(ct);
        }
        catch
        {
            Leave(key, entry);
            throw;
        }
        return new Release(this, key, entry);
    }

    /// <summary>The keys with a lock right now: held or waited for.</summary>
    public int Count
    {
        get
        {
            lock (_entries) return _entries.Count;
        }
    }

    private void Leave(TKey key, Entry entry)
    {
        lock (_entries)
            if (--entry.Users == 0) _entries.Remove(key);
    }

    /// <summary>A key's lock and how many requests hold it or wait for it.</summary>
    private sealed class Entry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public int Users;
    }

    private sealed class Release(KeyedLocks<TKey> owner, TKey key, Entry entry) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1) return;
            entry.Gate.Release();
            owner.Leave(key, entry);
        }
    }
}
