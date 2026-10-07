using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ArcaSim.Application;
using ArcaSim.Domain;

namespace ArcaSim.Tests;

/// <summary>
/// ArcaSim's clock the way it starts: running, on a moment the system gives in
/// UTC. What the services write must still be Argentina's time and day, and
/// 22:30 in Argentina, which is 01:30 UTC of the next day, is where a UTC leak shows.
/// The rest of the suite freezes the clock at -03:00, which hides that leak.
/// </summary>
public class LiveClockTests
{
    private const long Caller = ArcaSimHarness.Issuer;
    private static readonly DateTimeOffset NightInUtc = new(2026, 10, 8, 1, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_running_clock_reads_in_Argentinas_time()
    {
        var clock = new SimulatedClock(new FixedTime(NightInUtc));

        Assert.Equal(TimeSpan.FromHours(-3), clock.Now.Offset);
        Assert.Equal(new DateTime(2026, 10, 7, 22, 30, 0), clock.Now.DateTime);
    }

    [Fact]
    public void A_clock_frozen_at_a_UTC_moment_reads_the_same_moment_in_Argentinas_time()
    {
        var clock = new SimulatedClock(TimeProvider.System);

        clock.Freeze(NightInUtc);

        Assert.Equal(TimeSpan.FromHours(-3), clock.Now.Offset);
        Assert.Equal(NightInUtc, clock.Now);
    }

    [Fact]
    public async Task At_night_the_padron_and_a_VEP_write_Argentinas_time_and_day()
    {
        await using var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(NightInUtc);
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var certificate = await sim.IssueCertificateAsync(Caller, "noche", "ws_sr_padron_a4", "seti-setipago-api");

        var padron = await sim.Wsaa(Caller, certificate).LoginAsync("ws_sr_padron_a4");
        var (_, body) = await sim.PostSoapAsync(new Uri("http://localhost/sr-padron/webservices/personaServiceA4"),
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:a4=\"http://a4.soap.ws.server.puc.sr/\"><soapenv:Body><a4:getPersona>" +
            $"<token>{padron.Token}</token><sign>{padron.Sign}</sign><cuitRepresentada>{Caller}</cuitRepresentada><idPersona>{Caller}</idPersona>" +
            "</a4:getPersona></soapenv:Body></soapenv:Envelope>", "");
        var stamp = XDocument.Parse(body).Descendants("fechaHora").Single().Value;

        var vep = await sim.Wsaa(Caller, certificate).LoginAsync("seti-setipago-api");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/setiws-pago-api/api/v1/veps")
        {
            Content = new StringContent(
                "{\"entidadDePago\":\"1001\",\"vep\":{\"ownerCuit\":\"20111111112\",\"ownerTransactionId\":\"T-1\",\"nroFormulario\":\"6042\"," +
                "\"periodoFiscal\":\"202609\",\"importe\":\"10.00\",\"obligaciones\":[{\"impuesto\":\"10\",\"importe\":\"10.00\"}]}}",
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("WSAA-AUTH-PROXY-TOKEN", vep.Token);
        request.Headers.Add("WSAA-AUTH-PROXY-SIGN", vep.Sign);
        request.Headers.Add("WSAA-AUTH-PROXY-REPRESENTADO", $"{Caller}");
        using var created = await sim.Http.SendAsync(request);
        var expiry = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("fechaExpiracion").GetString();

        Assert.Equal("2026-10-07T22:30:00.000-03:00", stamp);
        Assert.Equal("2026-11-01", expiry);
    }

    private sealed class FixedTime(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }
}
