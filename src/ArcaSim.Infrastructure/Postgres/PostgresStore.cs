using System.Text.Json;
using System.Xml.Serialization;
using ArcaSim.Application;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using Npgsql;
using NpgsqlTypes;

namespace ArcaSim.Infrastructure.Postgres;

/// <summary>
/// Every port in PostgreSQL, for an ArcaSim several developers or a CI share
/// and that keeps its taxpayers, certificates' authorizations and vouchers
/// across restarts. Plain SQL: eight small tables do not need an ORM.
/// </summary>
public sealed partial class PostgresStore(NpgsqlDataSource db) :
    ISimulatorStore
{
    private static readonly XmlSerializer DetailSerializer = new(typeof(FECAEDetRequest));

    public const string Schema = """
        CREATE TABLE IF NOT EXISTS aliases (
            cuit bigint NOT NULL,
            alias text NOT NULL,
            PRIMARY KEY (cuit, alias));
        CREATE TABLE IF NOT EXISTS authorizations (
            client_cuit bigint NOT NULL,
            alias text NOT NULL,
            represented_cuit bigint NOT NULL,
            service text NOT NULL,
            PRIMARY KEY (client_cuit, alias, represented_cuit, service));
        CREATE TABLE IF NOT EXISTS tickets (
            id bigserial PRIMARY KEY,
            client_dn text NOT NULL,
            service text NOT NULL,
            generation_time timestamptz NOT NULL,
            expiration_time timestamptz NOT NULL);
        CREATE INDEX IF NOT EXISTS tickets_by_client ON tickets (client_dn, service, id DESC);
        CREATE TABLE IF NOT EXISTS taxpayers (
            cuit bigint PRIMARY KEY,
            name text NOT NULL,
            vat_condition int NOT NULL,
            active boolean NOT NULL,
            points_of_sale jsonb NOT NULL);
        CREATE TABLE IF NOT EXISTS vouchers (
            cuit bigint NOT NULL,
            point_of_sale int NOT NULL,
            voucher_type int NOT NULL,
            number_from bigint NOT NULL,
            number_to bigint NOT NULL,
            date date NOT NULL,
            emission_type text NOT NULL,
            authorization_code text NOT NULL,
            authorization_due date NOT NULL,
            processed_at timestamptz NOT NULL,
            detail text NOT NULL,
            observations jsonb NOT NULL,
            PRIMARY KEY (cuit, point_of_sale, voucher_type, number_from));
        CREATE TABLE IF NOT EXISTS caeas (
            cuit bigint NOT NULL,
            period int NOT NULL,
            fortnight smallint NOT NULL,
            code text NOT NULL UNIQUE,
            valid_from date NOT NULL,
            valid_to date NOT NULL,
            report_deadline date NOT NULL,
            processed_at timestamptz NOT NULL,
            PRIMARY KEY (cuit, period, fortnight));
        CREATE TABLE IF NOT EXISTS caea_without_movement (
            cuit bigint NOT NULL,
            caea text NOT NULL,
            point_of_sale int NOT NULL,
            reported_on date NOT NULL,
            PRIMARY KEY (cuit, caea, point_of_sale));
        CREATE TABLE IF NOT EXISTS exchange_rates (
            currency text NOT NULL,
            day date NOT NULL,
            rate numeric NOT NULL,
            PRIMARY KEY (currency, day));
        ALTER TABLE taxpayers ADD COLUMN IF NOT EXISTS profile jsonb NOT NULL DEFAULT '{}';
        """;

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var command = db.CreateCommand(Schema + DocumentsSchema);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using var command = db.CreateCommand(
            "TRUNCATE aliases, authorizations, tickets, taxpayers, vouchers, caeas, caea_without_movement, exchange_rates, documents, counters");
        await command.ExecuteNonQueryAsync(ct);
    }

    // ---- Access ------------------------------------------------------------

    public Task<IReadOnlyList<ServiceAuthorization>> AuthorizationsForAsync(long clientCuit, string alias, string service, CancellationToken ct = default) =>
        ListAsync(
            "SELECT client_cuit, alias, represented_cuit, service FROM authorizations " +
            "WHERE client_cuit = $1 AND lower(alias) = lower($2) AND lower(service) = lower($3)",
            [clientCuit, alias, service], ReadAuthorization, ct);

    public Task<IReadOnlyList<ServiceAuthorization>> ListAuthorizationsAsync(CancellationToken ct = default) =>
        ListAsync("SELECT client_cuit, alias, represented_cuit, service FROM authorizations ORDER BY client_cuit, alias, service",
            [], ReadAuthorization, ct);

    public Task SaveAliasAsync(ClientAlias alias, CancellationToken ct = default) =>
        ExecuteAsync("INSERT INTO aliases (cuit, alias) VALUES ($1, $2) ON CONFLICT DO NOTHING", [alias.Cuit, alias.Alias], ct);

    public Task SaveAuthorizationAsync(ServiceAuthorization a, CancellationToken ct = default) =>
        ExecuteAsync("INSERT INTO authorizations VALUES ($1, $2, $3, $4) ON CONFLICT DO NOTHING",
            [a.ClientCuit, a.Alias, a.RepresentedCuit, a.Service], ct);

    public Task DeleteAuthorizationAsync(ServiceAuthorization a, CancellationToken ct = default) =>
        ExecuteAsync("DELETE FROM authorizations WHERE client_cuit = $1 AND alias = $2 AND represented_cuit = $3 AND service = $4",
            [a.ClientCuit, a.Alias, a.RepresentedCuit, a.Service], ct);

    private static ServiceAuthorization ReadAuthorization(NpgsqlDataReader r) =>
        new(r.GetInt64(0), r.GetString(1), r.GetInt64(2), r.GetString(3));

    // ---- Tickets -----------------------------------------------------------

    public async Task<IssuedTicket?> LatestAsync(string clientDn, string service, CancellationToken ct = default) =>
        (await ListAsync(
            "SELECT client_dn, service, generation_time, expiration_time FROM tickets WHERE client_dn = $1 AND service = $2 ORDER BY id DESC LIMIT 1",
            [clientDn, service],
            r => new IssuedTicket(r.GetString(0), r.GetString(1), r.GetFieldValue<DateTimeOffset>(2), r.GetFieldValue<DateTimeOffset>(3)), ct))
        .FirstOrDefault();

    public Task AddAsync(IssuedTicket ticket, CancellationToken ct = default) =>
        ExecuteAsync("INSERT INTO tickets (client_dn, service, generation_time, expiration_time) VALUES ($1, $2, $3, $4)",
            [ticket.ClientDn, ticket.Service, ticket.GenerationTime.ToUniversalTime(), ticket.ExpirationTime.ToUniversalTime()], ct);

    // ---- Taxpayers ---------------------------------------------------------

    public async Task<Taxpayer?> FindAsync(long cuit, CancellationToken ct = default) =>
        (await ListAsync("SELECT cuit, name, vat_condition, active, points_of_sale, profile FROM taxpayers WHERE cuit = $1", [cuit], ReadTaxpayer, ct))
        .FirstOrDefault();

    public Task<IReadOnlyList<Taxpayer>> ListAsync(CancellationToken ct = default) =>
        ListAsync("SELECT cuit, name, vat_condition, active, points_of_sale, profile FROM taxpayers ORDER BY cuit", [], ReadTaxpayer, ct);

    public Task SaveAsync(Taxpayer taxpayer, CancellationToken ct = default) =>
        ExecuteAsync(
            "INSERT INTO taxpayers (cuit, name, vat_condition, active, points_of_sale, profile) VALUES ($1, $2, $3, $4, $5, $6) " +
            "ON CONFLICT (cuit) DO UPDATE SET name = excluded.name, vat_condition = excluded.vat_condition, " +
            "active = excluded.active, points_of_sale = excluded.points_of_sale, profile = excluded.profile",
            [taxpayer.Cuit, taxpayer.Name, (int)taxpayer.VatCondition, taxpayer.Active, Json(taxpayer.PointsOfSale), Json(taxpayer.Profile)], ct);

    private static Taxpayer ReadTaxpayer(NpgsqlDataReader r)
    {
        var points = JsonSerializer.Deserialize<List<PointOfSale>>(r.GetString(4)) ?? [];
        var taxpayer = new Taxpayer(r.GetInt64(0), r.GetString(1), (VatCondition)r.GetInt32(2), points);
        taxpayer.Update(taxpayer.Name, taxpayer.VatCondition, r.GetBoolean(3));
        taxpayer.SetProfile(JsonSerializer.Deserialize<TaxpayerProfile>(r.GetString(5)) ?? TaxpayerProfile.Empty);
        return taxpayer;
    }

    // ---- Vouchers ----------------------------------------------------------

    private const string VoucherColumns =
        "cuit, point_of_sale, voucher_type, number_from, number_to, date, emission_type, authorization_code, " +
        "authorization_due, processed_at, detail, observations";

    public async Task<StoredVoucher?> LastAsync(long cuit, int pointOfSale, int voucherType, CancellationToken ct = default) =>
        (await ListAsync(
            $"SELECT {VoucherColumns} FROM vouchers WHERE cuit = $1 AND point_of_sale = $2 AND voucher_type = $3 ORDER BY number_to DESC LIMIT 1",
            [cuit, pointOfSale, voucherType], ReadVoucher, ct)).FirstOrDefault();

    public async Task<StoredVoucher?> FindAsync(long cuit, int pointOfSale, int voucherType, long number, CancellationToken ct = default) =>
        (await ListAsync(
            $"SELECT {VoucherColumns} FROM vouchers WHERE cuit = $1 AND point_of_sale = $2 AND voucher_type = $3 AND number_from <= $4 AND $4 <= number_to",
            [cuit, pointOfSale, voucherType, number], ReadVoucher, ct)).FirstOrDefault();

    public Task AddAsync(StoredVoucher v, CancellationToken ct = default)
    {
        using var detail = new StringWriter();
        var copy = new FECAEDetRequest();
        v.Detail.CopyTo(copy);
        DetailSerializer.Serialize(detail, copy);
        return ExecuteAsync(
            $"INSERT INTO vouchers ({VoucherColumns}) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)",
            [v.Cuit, v.PointOfSale, v.VoucherType, v.From, v.To, v.Date, v.EmissionType.ToString(), v.AuthorizationCode,
                v.AuthorizationDue, v.ProcessedAt.ToUniversalTime(), detail.ToString(),
                Json(v.Observations.Select(o => new ObservationRow(o.Code, o.Msg)))], ct);
    }

    public Task<IReadOnlyList<StoredVoucher>> ListAsync(long? cuit, int limit, CancellationToken ct = default) =>
        ListAsync(
            $"SELECT {VoucherColumns} FROM vouchers WHERE $1::bigint IS NULL OR cuit = $1 ORDER BY processed_at DESC LIMIT $2",
            [cuit is null ? DBNull.Value : cuit.Value, limit], ReadVoucher, ct);

    public async Task<bool> AnyWithCaeaAsync(long cuit, string caea, int pointOfSale, CancellationToken ct = default) =>
        (await ListAsync(
            "SELECT 1 FROM vouchers WHERE cuit = $1 AND point_of_sale = $2 AND emission_type = 'Caea' AND authorization_code = $3 LIMIT 1",
            [cuit, pointOfSale, caea], _ => true, ct)).Count > 0;

    private static StoredVoucher ReadVoucher(NpgsqlDataReader r)
    {
        using var detail = new StringReader(r.GetString(10));
        var observations = JsonSerializer.Deserialize<List<ObservationRow>>(r.GetString(11)) ?? [];
        return new StoredVoucher(
            r.GetInt64(0), r.GetInt32(1), r.GetInt32(2), r.GetInt64(3), r.GetInt64(4),
            r.GetFieldValue<DateOnly>(5), Enum.Parse<EmissionType>(r.GetString(6)), r.GetString(7),
            r.GetFieldValue<DateOnly>(8), r.GetFieldValue<DateTimeOffset>(9),
            (FECAEDetRequest)DetailSerializer.Deserialize(detail)!,
            observations.Select(o => new Obs { Code = o.Code, Msg = o.Msg }).ToList());
    }

    private sealed record ObservationRow(int Code, string? Msg);

    // ---- CAEA --------------------------------------------------------------

    private const string CaeaColumns = "cuit, period, fortnight, code, valid_from, valid_to, report_deadline, processed_at";

    public async Task<IssuedCaea?> FindAsync(long cuit, int period, short fortnight, CancellationToken ct = default) =>
        (await ListAsync($"SELECT {CaeaColumns} FROM caeas WHERE cuit = $1 AND period = $2 AND fortnight = $3",
            [cuit, period, fortnight], ReadCaea, ct)).FirstOrDefault();

    public async Task<IssuedCaea?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        (await ListAsync($"SELECT {CaeaColumns} FROM caeas WHERE code = $1", [code], ReadCaea, ct)).FirstOrDefault();

    public Task AddAsync(IssuedCaea c, CancellationToken ct = default) =>
        ExecuteAsync($"INSERT INTO caeas ({CaeaColumns}) VALUES ($1, $2, $3, $4, $5, $6, $7, $8)",
            [c.Cuit, c.Period, c.Fortnight, c.Code, c.ValidFrom, c.ValidTo, c.ReportDeadline, c.ProcessedAt.ToUniversalTime()], ct);

    public Task<IReadOnlyList<CaeaWithoutMovement>> WithoutMovementAsync(long cuit, string caea, CancellationToken ct = default) =>
        ListAsync("SELECT cuit, caea, point_of_sale, reported_on FROM caea_without_movement WHERE cuit = $1 AND caea = $2 ORDER BY point_of_sale",
            [cuit, caea], r => new CaeaWithoutMovement(r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetFieldValue<DateOnly>(3)), ct);

    public Task AddWithoutMovementAsync(CaeaWithoutMovement report, CancellationToken ct = default) =>
        ExecuteAsync("INSERT INTO caea_without_movement VALUES ($1, $2, $3, $4)",
            [report.Cuit, report.Caea, report.PointOfSale, report.ReportedOn], ct);

    private static IssuedCaea ReadCaea(NpgsqlDataReader r) => new(
        r.GetInt64(0), r.GetInt32(1), r.GetInt16(2), r.GetString(3), r.GetFieldValue<DateOnly>(4),
        r.GetFieldValue<DateOnly>(5), r.GetFieldValue<DateOnly>(6), r.GetFieldValue<DateTimeOffset>(7));

    // ---- Exchange rates ----------------------------------------------------

    public async Task<(decimal Rate, DateOnly Day)?> RateAsync(string currency, DateOnly onOrBefore, CancellationToken ct = default)
    {
        var rows = await ListAsync(
            "SELECT rate, day FROM exchange_rates WHERE currency = $1 AND day <= $2 ORDER BY day DESC LIMIT 1",
            [currency, onOrBefore], r => (r.GetDecimal(0), r.GetFieldValue<DateOnly>(1)), ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public Task SetAsync(string currency, DateOnly day, decimal rate, CancellationToken ct = default) =>
        ExecuteAsync("INSERT INTO exchange_rates VALUES ($1, $2, $3) ON CONFLICT (currency, day) DO UPDATE SET rate = excluded.rate",
            [currency, day, rate], ct);

    // ---- Plumbing ----------------------------------------------------------

    private static NpgsqlParameter Json(object value) =>
        new() { Value = JsonSerializer.Serialize(value), NpgsqlDbType = NpgsqlDbType.Jsonb };

    private async Task ExecuteAsync(string sql, object[] parameters, CancellationToken ct)
    {
        await using var command = Command(sql, parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<IReadOnlyList<T>> ListAsync<T>(string sql, object[] parameters, Func<NpgsqlDataReader, T> read, CancellationToken ct)
    {
        await using var command = Command(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct)) rows.Add(read(reader));
        return rows;
    }

    private NpgsqlCommand Command(string sql, object[] parameters)
    {
        var command = db.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.Add(parameter as NpgsqlParameter ?? new NpgsqlParameter { Value = parameter });
        return command;
    }
}
