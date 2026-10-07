using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Setiws;

/// <summary>SETIWS-PAGO-API: the gateway's 401s with ARCA's texts, and a VEP from creation to payment.</summary>
public class SetiwsTests
{
    private const long Owner = ArcaSimHarness.Issuer;
    private const string Veps = "/setiws-pago-api/api/v1/veps";

    [Fact]
    public async Task Dummy_answers_without_credentials()
    {
        await using var sim = ArcaSimHarness.Start();

        var body = await sim.Http.GetStringAsync("/setiws-pago-api/dummy");

        Assert.Equal("{\"appserver\":\"OK\",\"dbserver\":\"OK\"}", body);
    }

    [Fact]
    public async Task Gateway_without_headers_lists_both_missing_headers_before_routing()
    {
        await using var sim = ArcaSimHarness.Start();

        var (status, body) = await SendAsync(sim, HttpMethod.Get, "/setiws-pago-api/api/v1/nada");

        Assert.Equal(401, status);
        Assert.Contains("\"code\":\"401\"", body);
        Assert.Contains("autenticación\\/autorización", body);
        Assert.Equal(
            "Falta header con representado seleccionado (\"WSAA-AUTH-PROXY-REPRESENTADO\").\n" +
            "Faltan headers requeridos de autenticación/autorización (\"Authorization\" o \"WSAA-AUTH-PROXY-TOKEN\" y \"WSAA-AUTH-PROXY-SIGN\").",
            GatewayMessage(body));
        Assert.Equal("error", JsonDocument.Parse(body).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Gateway_refuses_a_bearer_it_cannot_read_and_a_token_that_is_not_one()
    {
        await using var sim = ArcaSimHarness.Start();

        var (_, bearer) = await SendAsync(sim, HttpMethod.Get, Veps, headers: new() { ["Authorization"] = "Bearer abc", ["WSAA-AUTH-PROXY-REPRESENTADO"] = $"{Owner}" });
        var (_, garbage) = await SendAsync(sim, HttpMethod.Get, Veps, headers: new()
        {
            ["WSAA-AUTH-PROXY-TOKEN"] = "abc", ["WSAA-AUTH-PROXY-SIGN"] = "abc", ["WSAA-AUTH-PROXY-REPRESENTADO"] = $"{Owner}",
        });

        Assert.Equal("El header \"Authorization\" no contiene un JWT válido.", GatewayMessage(bearer));
        Assert.Equal("La firma no es válida para el token.\nFormato inválido del token.", GatewayMessage(garbage));
    }

    [Fact]
    public async Task Gateway_reports_every_problem_of_a_ticket_at_once()
    {
        await using var sim = await StartAsync();
        var ticket = await sim.TicketAsync(Owner, "wsfe");
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 2, 15, 13, 24, TimeSpan.FromHours(-3)));

        var (status, body) = await SendAsync(sim, HttpMethod.Get, Veps, headers: new()
        {
            ["WSAA-AUTH-PROXY-TOKEN"] = ticket.Token,
            ["WSAA-AUTH-PROXY-SIGN"] = Convert.ToBase64String(new byte[256]),
            ["WSAA-AUTH-PROXY-REPRESENTADO"] = "20131507969",
        });

        Assert.Equal(401, status);
        Assert.Equal(
            "El token no es válido para este servicio. (Servicio autorizado: \"wsfe\". Servicios admitidos: \"seti-setipago-api\").\n" +
            $"El token está vencido. (Vencimiento: {ticket.ExpiresAt.ToOffset(TimeSpan.FromHours(-3)):yyyy-MM-dd'T'HH:mm:ss}-03:00. Hora del servidor: 2026-10-02T15:13:24-03:00).\n" +
            $"La CUIT representada no está entre las autorizadas. (Seleccionada: 20131507969. Autorizadas: {Owner}).\n" +
            "La firma no es válida para el token.",
            GatewayMessage(body));
    }

    [Fact]
    public async Task A_VEP_is_created_once_per_transaction_found_pending_and_then_paid()
    {
        await using var sim = await StartAsync();
        var auth = await AuthAsync(sim);

        var (created, first) = await SendAsync(sim, HttpMethod.Post, Veps, VepJson("T-1", "1446.00", ("1000.00", "446.00")), auth);
        var (_, again) = await SendAsync(sim, HttpMethod.Post, Veps, VepJson("T-1", "1446.00", ("1000.00", "446.00")), auth);

        Assert.Equal(201, created);
        var vep = JsonDocument.Parse(first).RootElement;
        Assert.Equal(55000001, vep.GetProperty("nroVEP").GetInt64());
        Assert.Equal("2026-10-26", vep.GetProperty("fechaExpiracion").GetString());
        Assert.StartsWith("https://edp-1001.", vep.GetProperty("entidadDePagoUrl").GetString());
        Assert.False(vep.TryGetProperty("qrBase64", out _));
        Assert.Equal(55000001, JsonDocument.Parse(again).RootElement.GetProperty("nroVEP").GetInt64());

        var (_, pending) = await SendAsync(sim, HttpMethod.Get, $"{Veps}?owner-cuit={Owner}&owner-transaction-id=T-1&with-qr=true", headers: auth);
        var found = JsonDocument.Parse(pending).RootElement;
        Assert.Equal(1446m, found.GetProperty("VEP").GetProperty("importe").GetDecimal());
        Assert.Equal("ARCA", found.GetProperty("VEP").GetProperty("orgRecaudDesc").GetString());
        Assert.StartsWith("data:image/jpeg;base64,", found.GetProperty("qrBase64").GetString());
        Assert.False(found.TryGetProperty("CP", out _));

        (await sim.Http.PostAsJsonAsync("/arcasim/api/setiws/veps/55000001/payment", new { branchType = 38, paymentForm = 91 })).EnsureSuccessStatusCode();

        var (_, paid) = await SendAsync(sim, HttpMethod.Get, $"{Veps}?owner-cuit={Owner}&nro-vep=55000001&with-qr=true", headers: auth);
        var receipt = JsonDocument.Parse(paid).RootElement;
        Assert.Equal(55000001, receipt.GetProperty("CP").GetProperty("nroVEP").GetInt64());
        Assert.Equal(38, receipt.GetProperty("CP").GetProperty("tipoSucursal").GetInt32());
        Assert.False(receipt.TryGetProperty("qrBase64", out _));
        Assert.False(receipt.TryGetProperty("entidadDePagoUrl", out _));
    }

    [Fact]
    public async Task The_application_refuses_with_the_manuals_status_and_exception_type()
    {
        await using var sim = await StartAsync();
        var auth = await AuthAsync(sim);

        var (sumStatus, sum) = await SendAsync(sim, HttpMethod.Post, Veps, VepJson("T-2", "1500.00", ("1000.00", "446.00")), auth);
        var (_, owner) = await SendAsync(sim, HttpMethod.Post, Veps, VepJson("T-3", "10.00", ("10.00", null), ownerCuit: 20222222223), auth);
        var (missingStatus, missing) = await SendAsync(sim, HttpMethod.Get, $"{Veps}?nro-vep=1", headers: auth);
        var (unknownStatus, unknown) = await SendAsync(sim, HttpMethod.Get, $"{Veps}?owner-cuit={Owner}&nro-vep=99", headers: auth);

        Assert.Equal(400, sumStatus);
        Assert.Equal("InputFormularioException", TypeOf(sum));
        Assert.Equal("ValidationException", TypeOf(owner));
        Assert.Equal(400, missingStatus);
        Assert.Equal("MissingServletRequestParameterException", TypeOf(missing));
        Assert.Equal(404, unknownStatus);
        Assert.Equal("VepNotFoundException", TypeOf(unknown));
        var error = JsonDocument.Parse(unknown).RootElement;
        Assert.Equal("GET", error.GetProperty("method").GetString());
        Assert.EndsWith(Veps, error.GetProperty("url").GetString());
        Assert.True(Guid.TryParse(error.GetProperty("id").GetString(), out _));
    }

    private static async Task<ArcaSimHarness> StartAsync()
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(TestTime.Reference);
        await sim.PutTaxpayerAsync(Owner, "Organismo de Prueba", VatCondition.Exento);
        return sim;
    }

    private static async Task<Dictionary<string, string>> AuthAsync(ArcaSimHarness sim)
    {
        var ticket = await sim.TicketAsync(Owner, "seti-setipago-api");
        return new()
        {
            ["WSAA-AUTH-PROXY-TOKEN"] = ticket.Token,
            ["WSAA-AUTH-PROXY-SIGN"] = ticket.Sign,
            ["WSAA-AUTH-PROXY-REPRESENTADO"] = $"{Owner}",
        };
    }

    /// <summary>A VEP the way the manual's example writes it: numbers as strings.</summary>
    private static string VepJson(string transaction, string total, (string First, string? Second) amounts, long ownerCuit = Owner)
    {
        var obligations = new List<string> { $"{{\"impuesto\":\"10\",\"importe\":\"{amounts.First}\"}}" };
        if (amounts.Second is not null) obligations.Add($"{{\"impuesto\":\"11\",\"importe\":\"{amounts.Second}\"}}");
        return $"{{\"entidadDePago\":\"1001\",\"vep\":{{\"ownerCuit\":\"{ownerCuit}\",\"ownerTransactionId\":\"{transaction}\"," +
               $"\"nroFormulario\":\"6042\",\"periodoFiscal\":\"202609\",\"importe\":\"{total}\",\"obligaciones\":[{string.Join(",", obligations)}]}}}}";
    }

    private static async Task<(int Status, string Body)> SendAsync(
        ArcaSimHarness sim, HttpMethod method, string url, string? json = null, Dictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        foreach (var (name, value) in headers ?? []) request.Headers.TryAddWithoutValidation(name, value);
        using var response = await sim.Http.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string GatewayMessage(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("message").GetString()!;

    private static string TypeOf(string body) => JsonDocument.Parse(body).RootElement.GetProperty("type").GetString()!;
}
