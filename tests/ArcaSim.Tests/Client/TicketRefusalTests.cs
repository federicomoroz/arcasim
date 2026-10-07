using Arca.Client;

namespace ArcaSim.Tests.Client;

/// <summary>What WsfeClient does when WSFEv1 says the ticket it sent does not hold: ask for a new one and call again, once.</summary>
public class TicketRefusalTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_refused_ticket_is_replaced_and_the_call_is_made_again_with_the_new_one()
    {
        using var rig = new ClientRig();
        rig.Arca.LastVoucher = 41;
        var refusals = 0;
        rig.Arca.OnWsfe = call => Task.FromResult(call.Token == "token-wsfe-1" && Interlocked.Increment(ref refusals) == 1
            ? StubArca.StaleTicket(call.Operation)
            : StubArca.Soap(StubArca.Result(call.Operation, "<PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><CbteNro>41</CbteNro>")));

        var last = await rig.Wsfe.LastAuthorizedAsync(1, 6);

        Assert.Equal(41, last);
        Assert.Equal(["FECompUltimoAutorizado:token-wsfe-1", "FECompUltimoAutorizado:token-wsfe-2"], rig.Arca.WsfeRequests);
        Assert.Equal(2, rig.Arca.Logins);
    }

    [Fact]
    public async Task A_ticket_that_is_refused_twice_is_not_asked_for_again_and_again()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = call => Task.FromResult(StubArca.StaleTicket(call.Operation));

        var failure = await Assert.ThrowsAsync<WsfeErrorException>(() => rig.Wsfe.LastAuthorizedAsync(1, 6));

        Assert.Equal(600, Assert.Single(failure.Errors).Code);
        Assert.Equal(2, rig.Arca.Logins);
        Assert.Equal(2, rig.Arca.WsfeRequests.Count);
    }

    [Fact]
    public async Task Two_calls_refused_with_the_same_ticket_replace_it_once()
    {
        using var rig = new ClientRig();
        var stale = await rig.Wsaa.GetTicketAsync("wsfe");
        var firstReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleCalls = 0;
        rig.Arca.OnWsfe = async call =>
        {
            if (call.Token != stale.Token) return StubArca.Soap(StubArca.Result(call.Operation, "<PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><CbteNro>7</CbteNro>"));
            // Both calls reach the server with the ticket that no longer holds. Each is refused only when
            // the test lets it go: the first one finishes (it logs in again) before the second hears.
            if (Interlocked.Increment(ref staleCalls) == 1)
            {
                firstReached.SetResult();
                await releaseFirst.Task;
            }
            else
            {
                bothReached.SetResult();
                await releaseSecond.Task;
            }
            return StubArca.StaleTicket(call.Operation);
        };

        var first = rig.Wsfe.LastAuthorizedAsync(1, 6);
        await firstReached.Task.WaitAsync(Patience);
        var second = rig.Wsfe.LastAuthorizedAsync(1, 6);
        await bothReached.Task.WaitAsync(Patience);
        releaseFirst.SetResult();
        await first.WaitAsync(Patience);
        releaseSecond.SetResult();
        await second.WaitAsync(Patience);

        // One login for the ticket both had, one for the ticket that replaced it: the second call found
        // the new ticket in place and used it instead of throwing it away.
        Assert.Equal(2, rig.Arca.Logins);
        Assert.Equal(["token-wsfe-1", "token-wsfe-1", "token-wsfe-2", "token-wsfe-2"], rig.Arca.WsfeRequests.Select(r => r.Split(':')[1]));
    }
}
