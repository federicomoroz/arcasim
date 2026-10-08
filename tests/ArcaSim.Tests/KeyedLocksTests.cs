using ArcaSim.Application;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Tests;

/// <summary>The per-key locks behind every numbering sequence: exclusive per key, and gone once nobody holds or waits for them.</summary>
public class KeyedLocksTests
{
    [Fact]
    public async Task A_key_is_held_by_one_request_at_a_time_and_other_keys_do_not_wait()
    {
        var locks = new KeyedLocks<string>();
        var first = await locks.AcquireAsync("a", CancellationToken.None);

        var second = locks.AcquireAsync("a", CancellationToken.None);
        var other = await locks.AcquireAsync("b", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        Assert.False(second.IsCompleted);

        first.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
        other.Dispose();
        Assert.Equal(0, locks.Count);
    }

    [Fact]
    public async Task A_wait_that_is_cancelled_leaves_nothing_behind()
    {
        var locks = new KeyedLocks<string>();
        var held = await locks.AcquireAsync("a", CancellationToken.None);
        using var cancel = new CancellationTokenSource();

        var waiting = locks.AcquireAsync("a", cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(1, locks.Count);

        held.Dispose();
        held.Dispose();
        Assert.Equal(0, locks.Count);
        (await locks.AcquireAsync("a", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    [Fact]
    public async Task The_same_sequence_of_two_services_is_two_locks()
    {
        var locks = new SequenceLocks();
        using var remitos = await locks.AcquireAsync("wsremcarne.requests", 20111111112, 9000, 0, CancellationToken.None);

        using var numbering = await locks.AcquireAsync("wsremcarne", 20111111112, 9000, 0, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, locks.Count);
    }
}
