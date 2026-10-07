using System.Text.Json;
using Arca.Client;

namespace ArcaSim.Tests.Client;

/// <summary>
/// The ticket cache on disk: ARCA refuses a second login while the first ticket lives, so a process that
/// starts again has to find the ticket it had, and nothing the file system does may fail a call.
/// </summary>
public class TicketCacheTests
{
    [Fact]
    public async Task A_ticket_is_kept_on_disk_and_a_client_that_starts_later_uses_it()
    {
        using var rig = new ClientRig(withCache: true);
        var first = await rig.Wsaa.GetTicketAsync("wsfe");

        using var later = rig.NewWsaa();
        var second = await later.GetTicketAsync("wsfe");

        Assert.Equal(first, second);
        Assert.Equal(1, rig.Arca.Logins);
        Assert.True(File.Exists(rig.CacheFile("wsfe", "localhost_7080")));
    }

    [Fact]
    public async Task The_port_is_part_of_the_file_name_so_two_simulators_do_not_share_a_ticket()
    {
        using var rig = new ClientRig(withCache: true);
        await rig.Wsaa.GetTicketAsync("wsfe");

        using var other = rig.NewWsaa(new Uri("http://localhost:5199/ws/services/LoginCms"));
        var ticket = await other.GetTicketAsync("wsfe");

        Assert.Equal(2, rig.Arca.Logins);
        Assert.Equal("token-wsfe-2", ticket.Token);
        Assert.True(File.Exists(rig.CacheFile("wsfe", "localhost_7080")));
        Assert.True(File.Exists(rig.CacheFile("wsfe", "localhost_5199")));
    }

    [Fact]
    public async Task An_address_on_its_default_port_keeps_the_file_name_earlier_versions_used()
    {
        using var rig = new ClientRig(withCache: true, wsaaUrl: new Uri("https://wsaahomo.afip.gov.ar/ws/services/LoginCms"));

        await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.True(File.Exists(rig.CacheFile("wsfe", "wsaahomo.afip.gov.ar")));
        Assert.Single(Directory.GetFiles(rig.CacheDirectory!));
    }

    [Fact]
    public async Task A_ticket_an_earlier_version_left_is_found_and_copied_to_the_new_name()
    {
        using var rig = new ClientRig(withCache: true);
        var left = Left(rig, expiresIn: TimeSpan.FromHours(6));
        Write(rig.CacheFile("wsfe", "localhost"), left);

        var found = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Equal(left, found);
        Assert.Equal(0, rig.Arca.Logins);
        Assert.Equal(left, Read(rig.CacheFile("wsfe", "localhost_7080")));
    }

    [Fact]
    public async Task When_both_names_hold_a_ticket_the_one_that_lasts_longer_wins()
    {
        using var rig = new ClientRig(withCache: true);
        Write(rig.CacheFile("wsfe", "localhost_7080"), Left(rig, TimeSpan.FromHours(3), "current"));
        var longer = Left(rig, TimeSpan.FromHours(8), "earlier version");
        Write(rig.CacheFile("wsfe", "localhost"), longer);

        var found = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Equal(longer, found);
        Assert.Equal(0, rig.Arca.Logins);
    }

    [Fact]
    public async Task A_ticket_on_disk_that_is_about_to_expire_is_not_used()
    {
        using var rig = new ClientRig(withCache: true);
        Write(rig.CacheFile("wsfe", "localhost_7080"), Left(rig, TimeSpan.FromMinutes(9)));

        var ticket = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Equal("token-wsfe-1", ticket.Token);
        Assert.Equal(ticket, Read(rig.CacheFile("wsfe", "localhost_7080")));
    }

    [Fact]
    public async Task A_damaged_file_is_replaced_by_a_new_login()
    {
        using var rig = new ClientRig(withCache: true);
        Directory.CreateDirectory(rig.CacheDirectory!);
        File.WriteAllText(rig.CacheFile("wsfe", "localhost_7080"), "{ this is not a ticket");

        var ticket = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Equal(1, rig.Arca.Logins);
        Assert.Equal(ticket, Read(rig.CacheFile("wsfe", "localhost_7080")));
    }

    [Fact]
    public async Task A_file_another_process_is_holding_is_skipped_not_an_error()
    {
        using var rig = new ClientRig(withCache: true);
        var path = rig.CacheFile("wsfe", "localhost_7080");
        Write(path, Left(rig, TimeSpan.FromHours(6)));
        await using var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ticket = await rig.Wsaa.GetTicketAsync("wsfe");
        var again = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Equal("token-wsfe-1", ticket.Token);
        Assert.Same(ticket, again);
        Assert.Equal(1, rig.Arca.Logins);
    }

    [Fact]
    public async Task A_folder_that_cannot_be_written_does_not_fail_a_login_that_worked()
    {
        using var rig = new ClientRig(withCache: true);
        File.WriteAllText(rig.CacheDirectory!, "a file where the folder should be");

        var ticket = await rig.Wsaa.GetTicketAsync("wsfe");
        var again = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Equal("token-wsfe-1", ticket.Token);
        Assert.Same(ticket, again);
        Assert.Equal(1, rig.Arca.Logins);
    }

    [Fact]
    public async Task Forgetting_a_refused_ticket_deletes_its_file()
    {
        using var rig = new ClientRig(withCache: true);
        var refused = await rig.Wsaa.GetTicketAsync("wsfe");

        rig.Wsaa.Forget("wsfe", refused);

        Assert.False(File.Exists(rig.CacheFile("wsfe", "localhost_7080")));
        using var later = rig.NewWsaa();
        Assert.Equal("token-wsfe-2", (await later.GetTicketAsync("wsfe")).Token);
    }

    [Fact]
    public async Task Forgetting_a_refused_ticket_keeps_the_newer_one_another_process_wrote()
    {
        using var rig = new ClientRig(withCache: true);
        var refused = await rig.Wsaa.GetTicketAsync("wsfe");
        var newer = Left(rig, TimeSpan.FromHours(11), "written by another process");
        Write(rig.CacheFile("wsfe", "localhost_7080"), newer);

        rig.Wsaa.Forget("wsfe", refused);

        Assert.Equal(newer, Read(rig.CacheFile("wsfe", "localhost_7080")));
    }

    [Fact]
    public async Task Forgetting_a_service_deletes_the_files_of_both_names()
    {
        using var rig = new ClientRig(withCache: true);
        await rig.Wsaa.GetTicketAsync("wsfe");
        Write(rig.CacheFile("wsfe", "localhost"), Left(rig, TimeSpan.FromHours(6)));

        rig.Wsaa.Forget("wsfe");

        Assert.Empty(Directory.GetFiles(rig.CacheDirectory!));
    }

    [Fact]
    public async Task Without_a_folder_the_ticket_lives_in_memory_only()
    {
        using var rig = new ClientRig();
        var first = await rig.Wsaa.GetTicketAsync("wsfe");

        using var later = rig.NewWsaa();
        var second = await later.GetTicketAsync("wsfe");

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(2, rig.Arca.Logins);
    }

    /// <summary>A ticket that is alive for <paramref name="expiresIn"/> from the rig's clock.</summary>
    private static AccessTicket Left(ClientRig rig, TimeSpan expiresIn, string token = "left on disk") =>
        new(token, "sign of " + token, rig.Time.GetUtcNow().AddHours(-1), rig.Time.GetUtcNow() + expiresIn);

    private static void Write(string path, AccessTicket ticket)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(ticket));
    }

    private static AccessTicket? Read(string path) => JsonSerializer.Deserialize<AccessTicket>(File.ReadAllText(path));
}
