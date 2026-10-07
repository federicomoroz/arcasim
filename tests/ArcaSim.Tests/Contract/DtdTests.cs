using System.Xml.Linq;

namespace ArcaSim.Tests.Contract;

/// <summary>A request with a DTD is refused while it is read, as ARCA's servers refuse it: no entity is ever expanded.</summary>
public class DtdTests
{
    [Fact]
    public async Task An_envelope_with_a_DTD_gets_the_dialects_unreadable_fault()
    {
        await using var sim = ArcaSimHarness.Start();
        const string envelope =
            "<?xml version=\"1.0\"?><!DOCTYPE e [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">]>" +
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><dummy>&b;</dummy></soapenv:Body></soapenv:Envelope>";

        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost/sr-padron/webservices/personaServiceA4"), envelope, "");

        Assert.Equal(500, status);
        var fault = XDocument.Parse(body).Descendants("faultstring").Single().Value;
        Assert.StartsWith("Error reading XMLStreamReader: ", fault);
        Assert.Contains("DTD", fault);
        Assert.DoesNotContain("aaaaaaaaaa", body);
    }
}
