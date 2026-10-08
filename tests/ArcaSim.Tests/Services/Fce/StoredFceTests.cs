using System.Text.Json;
using ArcaSim.Application.Services.Fce;

namespace ArcaSim.Tests.Services.Fce;

/// <summary>
/// The FCE ledger reads its accounts back on every call, so a document written
/// when an account still carried the kind and instant of its acceptance, and an
/// agent's report the code it was rejected with (which nothing ever read), must
/// still read, with the stores' own JSON settings.
/// </summary>
public class StoredFceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void An_account_written_with_its_acceptance_kind_and_instant_still_reads()
    {
        const string stored = """
            {"code":7,"invoice":{"cuit":20111111112,"type":201,"pointOfSale":1,"number":3},"issuer":20111111112,"receiver":30712345671,
             "option":"SCA","currency":"PES","initial":6050000,"acceptanceDue":"2026-10-31",
             "history":[{"state":"Modificable","since":"2026-10-01T12:00:00-03:00"},{"state":"Aceptada","since":"2026-10-03T09:00:00-03:00"}],
             "acceptanceKind":"Expresa","acceptedAt":"2026-10-03T09:00:00-03:00","acceptedBalance":6050000}
            """;

        var account = JsonSerializer.Deserialize<FceAccount>(stored, Json)!;

        Assert.Equal(7, account.Code);
        Assert.Equal("SCA", account.Option);
        Assert.Equal(6_050_000m, account.AcceptedBalance);
        Assert.Equal("Aceptada", account.State.State);
    }

    [Fact]
    public void An_agents_report_written_with_its_rejection_code_still_reads()
    {
        const string stored = """
            {"agent":30587654322,"accountId":"0001234","availableAt":"2026-10-03T09:00:00-03:00","state":"R",
             "confirmedAt":"2026-10-04T09:00:00-03:00","rejectionCode":1,"rejectionReason":"La cuenta comitente indicada no corresponde al titular"}
            """;

        var report = JsonSerializer.Deserialize<FceAgentReport>(stored, Json)!;

        Assert.Equal("R", report.State);
        Assert.Equal("La cuenta comitente indicada no corresponde al titular", report.RejectionReason);
    }
}
