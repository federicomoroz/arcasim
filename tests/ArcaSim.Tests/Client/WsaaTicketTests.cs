using System.Collections.Concurrent;
using Arca.Client;

namespace ArcaSim.Tests.Client;

/// <summary>
/// How WsaaClient keeps the tickets in memory: until their renewal margin, one login at a time,
/// and without making a caller with a good ticket wait for somebody else's login.
/// </summary>
public class WsaaTicketTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_ticket_is_kept_until_ten_minutes_before_it_expires()
    {
        using var rig = new ClientRig();
        var first = await rig.Wsaa.GetTicketAsync("wsfe");

        rig.Time.Advance(TimeSpan.FromHours(11) + TimeSpan.FromMinutes(49));
        var stillThere = await rig.Wsaa.GetTicketAsync("wsfe");
        rig.Time.Advance(TimeSpan.FromMinutes(2));
        var renewed = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Same(first, stillThere);
        Assert.NotEqual(first.Token, renewed.Token);
        Assert.Equal(2, rig.Arca.Logins);
        Assert.True(renewed.ExpiresAt > first.ExpiresAt);
    }

    [Fact]
    public async Task The_renewal_margin_is_the_one_in_the_options()
    {
        using var rig = new ClientRig(renewalMargin: TimeSpan.FromHours(2));
        var first = await rig.Wsaa.GetTicketAsync("wsfe");

        rig.Time.Advance(TimeSpan.FromHours(9) + TimeSpan.FromMinutes(59));
        Assert.Same(first, await rig.Wsaa.GetTicketAsync("wsfe"));
        rig.Time.Advance(TimeSpan.FromMinutes(2));
        var renewed = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.NotEqual(first.Token, renewed.Token);
    }

    [Fact]
    public async Task Each_service_has_its_own_ticket()
    {
        using var rig = new ClientRig();

        var wsfe = await rig.Wsaa.GetTicketAsync("wsfe");
        var padron = await rig.Wsaa.GetTicketAsync("ws_sr_padron_a13");

        Assert.Equal(StubArca.CredentialsOf("wsfe", 1).Token, wsfe.Token);
        Assert.Equal(StubArca.CredentialsOf("ws_sr_padron_a13", 2).Token, padron.Token);
        Assert.Same(wsfe, await rig.Wsaa.GetTicketAsync("wsfe"));
    }

    [Fact]
    public async Task A_valid_ticket_is_returned_while_another_service_is_logging_in()
    {
        using var rig = new ClientRig();
        var wsfe = await rig.Wsaa.GetTicketAsync("wsfe");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loggingIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Arca.OnLogin = async login =>
        {
            loggingIn.SetResult();
            await release.Task;
            return rig.Arca.LoginAnswer(login.Service, login.Number);
        };
        var slow = rig.Wsaa.GetTicketAsync("ws_sr_padron_a13");
        await loggingIn.Task.WaitAsync(Patience);

        var again = await rig.Wsaa.GetTicketAsync("wsfe").WaitAsync(Patience);
        Assert.False(slow.IsCompleted);
        release.SetResult();
        var padron = await slow.WaitAsync(Patience);

        Assert.Same(wsfe, again);
        Assert.Equal("token-ws_sr_padron_a13-2", padron.Token);
    }

    [Fact]
    public async Task Callers_that_ask_together_share_one_login()
    {
        using var rig = new ClientRig();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Arca.OnLogin = async login =>
        {
            await release.Task;
            return rig.Arca.LoginAnswer(login.Service, login.Number);
        };

        var callers = Enumerable.Range(0, 20).Select(_ => Task.Run(() => rig.Wsaa.GetTicketAsync("wsfe"))).ToList();
        await Task.Delay(100);
        release.SetResult();
        var tickets = await Task.WhenAll(callers).WaitAsync(Patience);

        Assert.Equal(1, rig.Arca.Logins);
        Assert.All(tickets, ticket => Assert.Same(tickets[0], ticket));
    }

    [Fact]
    public async Task Forgetting_the_ticket_that_was_refused_makes_the_next_call_log_in()
    {
        using var rig = new ClientRig();
        var refused = await rig.Wsaa.GetTicketAsync("wsfe");

        rig.Wsaa.Forget("wsfe", refused);
        var next = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.NotEqual(refused.Token, next.Token);
        Assert.Equal(2, rig.Arca.Logins);
    }

    [Fact]
    public async Task Forgetting_a_ticket_that_was_already_replaced_leaves_the_new_one()
    {
        using var rig = new ClientRig();
        var stale = await rig.Wsaa.GetTicketAsync("wsfe");
        rig.Wsaa.Forget("wsfe", stale);
        var current = await rig.Wsaa.GetTicketAsync("wsfe");

        rig.Wsaa.Forget("wsfe", stale);

        Assert.Same(current, await rig.Wsaa.GetTicketAsync("wsfe"));
        Assert.Equal(2, rig.Arca.Logins);
    }

    [Fact]
    public async Task Forgetting_a_service_drops_whatever_ticket_it_has()
    {
        using var rig = new ClientRig();
        var first = await rig.Wsaa.GetTicketAsync("wsfe");
        var other = await rig.Wsaa.GetTicketAsync("ws_sr_padron_a13");

        rig.Wsaa.Forget("wsfe");

        Assert.NotEqual(first.Token, (await rig.Wsaa.GetTicketAsync("wsfe")).Token);
        Assert.Same(other, await rig.Wsaa.GetTicketAsync("ws_sr_padron_a13"));
    }

    [Fact]
    public async Task Callers_that_forget_and_ask_at_once_always_get_a_ticket()
    {
        using var rig = new ClientRig();
        var services = Enumerable.Range(0, 8).Select(n => $"service-{n}").ToArray();
        var failures = new ConcurrentBag<Exception>();

        await Task.WhenAll(services.Select(service => Task.Run(async () =>
        {
            try
            {
                for (var round = 0; round < 50; round++)
                {
                    var ticket = await rig.Wsaa.GetTicketAsync(service);
                    if (round % 3 == 0) rig.Wsaa.Forget(service, ticket);
                    if (round % 7 == 0) rig.Wsaa.Forget(service);
                }
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }))).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Empty(failures);
    }

    [Fact]
    public async Task A_disposed_client_hands_out_no_tickets()
    {
        using var rig = new ClientRig();
        var wsaa = rig.NewWsaa();
        await wsaa.GetTicketAsync("wsfe");

        wsaa.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => wsaa.GetTicketAsync("wsfe"));
    }
}
