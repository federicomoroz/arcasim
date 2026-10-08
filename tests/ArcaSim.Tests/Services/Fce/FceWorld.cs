using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using System.Xml.Schema;
using Arca.Client;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Fce;

/// <summary>
/// ArcaSim with the four parties of an FCE: the seller (the harness's issuer,
/// who invoices through WSFEv1), the buyer, an Agente de Depósito Colectivo and
/// the Sistema de Circulación Abierta, each with its own certificate. Every
/// answer is checked against the service's WSDL before a test sees it.
/// </summary>
public sealed class FceWorld : IAsyncDisposable
{
    public const long Seller = ArcaSimHarness.Issuer;
    public const long Buyer = 30712345671;
    public const long Agent = 30587654322;
    public const long Sca = 30700000016;

    /// <summary>A CBU whose two BCRA check digits are right.</summary>
    public const string BuyerCbu = "0170001554000000987650";

    private static readonly string[] Services = ["wsfecred", "wsfecredagente", "wsfecredsca"];
    private static readonly Dictionary<string, ServiceContract> Contracts = Services.ToDictionary(s => s,
        s => ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", $"{s}-homologacion.wsdl")));

    private readonly Dictionary<long, WsaaClient> _wsaa = [];

    private FceWorld(ArcaSimHarness sim, WsfeClient wsfe)
    {
        Sim = sim;
        Wsfe = wsfe;
    }

    public ArcaSimHarness Sim { get; }
    public WsfeClient Wsfe { get; }

    /// <summary>Frozen at 01/10/2026 12:00, as the harness starts.</summary>
    public static async Task<FceWorld> StartAsync()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        var world = new FceWorld(sim, wsfe);
        await sim.PutTaxpayerAsync(Buyer, "Comercial del Norte SA", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(Agent, "Caja de Valores SA", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(Sca, "Circulacion Abierta SA", VatCondition.ResponsableInscripto);
        foreach (var cuit in new[] { Seller, Buyer, Agent, Sca })
        {
            X509Certificate2 certificate = await sim.IssueCertificateAsync(cuit, "fce", Services);
            world._wsaa[cuit] = sim.Wsaa(cuit, certificate);
        }
        return world;
    }

    /// <summary>A CUIT with a ticket for the three services but no record in ArcaSim's padrón.</summary>
    public async Task<long> AddStrangerAsync(long cuit)
    {
        X509Certificate2 certificate = await Sim.IssueCertificateAsync(cuit, "stranger", Services);
        _wsaa[cuit] = Sim.Wsaa(cuit, certificate);
        return cuit;
    }

    public void Advance(TimeSpan by) => Sim.Clock.Advance(by);

    /// <summary>Moves to the day the buyer may first operate on today's vouchers (1106): 00:00 two days later.</summary>
    public void UntilOperable() => Advance(TimeSpan.FromHours(36));

    /// <summary>An FCE A for services, authorized by WSFEv1 to the buyer: $6.050.000 with VAT.</summary>
    public async Task<long> InvoiceAsync(decimal net = 5_000_000m)
    {
        var result = await Wsfe.AuthorizeNextAsync(1, 201, Fce(net));
        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        return result.Number;
    }

    /// <summary>A debit (202) or credit (203) note on invoice number.</summary>
    public async Task<long> NoteAsync(int type, long invoice, decimal net)
    {
        var result = await Wsfe.AuthorizeNextAsync(1, type, Fce(net) with { Associated = [new AssociatedVoucher(201, 1, invoice, Seller)] });
        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        return result.Number;
    }

    private static Voucher Fce(decimal net) => new()
    {
        Concept = 2,
        DocumentType = 80,
        DocumentNumber = Buyer,
        Net = net,
        Vat = net * 0.21m,
        Total = net * 1.21m,
        ServiceFrom = new DateOnly(2026, 9, 1),
        ServiceTo = new DateOnly(2026, 9, 30),
        PaymentDue = new DateOnly(2026, 11, 30),
        ReceiverVatCondition = 1,
        VatLines = [new VatLine(5, net, net * 0.21m)],
    };

    /// <summary>IdCtaCteType by the invoice that opened the account, as wsfecred takes it.</summary>
    public static string Account(long invoice) =>
        $"<idCtaCte><idFactura><CUITEmisor>{Seller}</CUITEmisor><codTipoCmp>201</codTipoCmp><ptoVta>1</ptoVta><nroCmp>{invoice}</nroCmp></idFactura></idCtaCte>";

    public static string Voucher(string name, int type, long number) =>
        $"<{name}><CUITEmisor>{Seller}</CUITEmisor><codTipoCmp>{type}</codTipoCmp><ptoVta>1</ptoVta><nroCmp>{number}</nroCmp></{name}>";

    /// <summary>IdComprobanteType as wsfecredagente and wsfecredsca name it.</summary>
    public static string IdFactura(long invoice) =>
        $"<idFactura><cuitEmisor>{Seller}</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>{invoice}</nroCmp></idFactura>";

    public Task<XElement> FecredAsync(long cuit, string operation, string inner = "") => CallAsync("wsfecred", cuit, operation, inner);

    public Task<XElement> AgenteAsync(string operation, string inner = "", long cuit = Agent) => CallAsync("wsfecredagente", cuit, operation, inner);

    public Task<XElement> ScaAsync(string operation, string inner = "", long cuit = Sca) => CallAsync("wsfecredsca", cuit, operation, inner);

    /// <summary>One operation as the party calls it; the answer's single child (xxxReturn or resultado), already validated.</summary>
    public async Task<XElement> CallAsync(string service, long cuit, string operation, string inner)
    {
        var contract = Contracts[service];
        var op = contract.Operations.Single(o => o.Name == operation);
        var ticket = await _wsaa[cuit].GetTicketAsync(service);
        var auth = service == "wsfecred" ? "authRequest" : "autenticacion";
        var envelope =
            $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ser=\"{contract.TargetNamespace}\"><soapenv:Header/><soapenv:Body>" +
            $"<ser:{op.Input!.LocalName}><{auth}><token>{ticket.Token}</token><sign>{ticket.Sign}</sign><cuitRepresentada>{cuit}</cuitRepresentada></{auth}>{inner}</ser:{op.Input.LocalName}>" +
            "</soapenv:Body></soapenv:Envelope>";

        var (status, body) = await Sim.PostSoapAsync(new Uri("http://localhost" + contract.AddressPath), envelope, op.Action ?? "");
        Assert.True(status == 200, body);
        var answer = XDocument.Parse(body).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
        Assert.Equal(op.Output, answer.Name);
        Validate(contract, answer);
        return answer.Elements().Single();
    }

    /// <summary>The answer is valid for the WSDL ARCA publishes: what a generated client deserializes.</summary>
    private static void Validate(ServiceContract contract, XElement answer)
    {
        var problems = new List<string>();
        new XDocument(answer).Validate(contract.Schemas, (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + answer);
    }

    /// <summary>The codes of a codigoDescripcion block (arrayErrores, errores, erroresFormato...).</summary>
    public static List<string> Codes(XElement? block) =>
        block?.Elements().Select(e => e.Element("codigo")!.Value).ToList() ?? [];

    public async ValueTask DisposeAsync() => await Sim.DisposeAsync();
}
