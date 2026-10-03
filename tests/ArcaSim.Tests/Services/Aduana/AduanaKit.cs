using System.Security;
using System.Xml.Linq;
using System.Xml.Schema;
using Arca.Client;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Aduana;

/// <summary>
/// ArcaSim with a customs operator registered, frozen on a weekday, and a way
/// to call each customs service the way its WSDL words the request: the
/// authentication in the service's own wrapper, a real WSAA ticket in it, and
/// every answer checked against the WSDL before a test looks at it.
/// </summary>
internal sealed class AduanaKit : IAsyncDisposable
{
    public const long Caller = ArcaSimHarness.Issuer;
    public const long Other = 20222222223;

    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

    public static readonly DateTimeOffset Today = new(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3));

    private AduanaKit(ArcaSimHarness sim) => Sim = sim;

    public ArcaSimHarness Sim { get; }

    public static async Task<AduanaKit> StartAsync()
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(Today);
        await sim.PutTaxpayerAsync(Caller, "Operador Aduanero SA", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(Other, "Ana Gomez", VatCondition.ResponsableInscripto);
        return new AduanaKit(sim);
    }

    /// <summary>A service called by one CUIT, its ticket written by the wrapper the service uses.</summary>
    public async Task<AduanaService> ServiceAsync(string id, Func<AccessTicket, long, string> auth, long cuit = Caller)
    {
        var definition = Catalog.Find(id)!;
        var certificate = await Sim.IssueCertificateAsync(cuit, $"{id}-{cuit}", definition.Wsaa[0]);
        var ticket = await Sim.Wsaa(cuit, certificate).LoginAsync(definition.Wsaa[0]);
        var contract = ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", definition.Wsdl));
        return new AduanaService(Sim, contract, auth(ticket, cuit));
    }

    /// <summary>argAutentica: Autenticacion extends AutenticacionBase, so Token and Sign go first.</summary>
    public static string ArgAutentica(AccessTicket t, long cuit, string agent = "OTEN") =>
        $"<argAutentica><Token>{t.Token}</Token><Sign>{t.Sign}</Sign><Cuit>{cuit}</Cuit><TipoAgente>{agent}</TipoAgente><Rol>EXTE</Rol></argAutentica>";

    /// <summary>The flat Autenticacion of wgesTabRef (Autentica) and wDigDepFiel (autentica): the CUIT first, the ticket last.</summary>
    public static string Flat(string wrapper, AccessTicket t, long cuit, string agent) =>
        $"<{wrapper}><Cuit>{cuit}</Cuit><TipoAgente>{agent}</TipoAgente><Rol>EXTE</Rol><Token>{t.Token}</Token><Sign>{t.Sign}</Sign></{wrapper}>";

    public static string Empresa(AccessTicket t, long cuit) =>
        $"<argWSAutenticacionEmpresa><Token>{t.Token}</Token><Sign>{t.Sign}</Sign><CuitEmpresaConectada>{cuit}</CuitEmpresaConectada><TipoAgente>TILI</TipoAgente><Rol>TILI</Rol></argWSAutenticacionEmpresa>";

    public ValueTask DisposeAsync() => Sim.DisposeAsync();

    /// <summary>The answer is valid for the WSDL ARCA publishes: what a generated client deserializes.</summary>
    public static void Validate(ServiceContract contract, XElement answer)
    {
        var problems = new List<string>();
        new XDocument(new XElement(answer)).Validate(contract.Schemas, (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + answer);
    }
}

internal sealed class AduanaService(ArcaSimHarness sim, ServiceContract contract, string auth)
{
    public ServiceContract Contract => contract;

    public string Ns => contract.TargetNamespace;

    /// <summary>Calls an operation and returns its *Result element, after checking the answer against the WSDL.</summary>
    public async Task<XElement> CallAsync(string operation, string inner)
    {
        var (status, body) = await RawAsync(operation, inner);
        Assert.True(status == 200, body);
        var answer = XDocument.Parse(body).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
        Assert.Equal(XName.Get(operation + "Response", Ns), answer.Name);
        AduanaKit.Validate(contract, answer);
        return answer.Elements().First();
    }

    public Task<(int Status, string Body)> RawAsync(string operation, string inner) =>
        sim.PostSoapAsync(new Uri("http://localhost" + contract.AddressPath),
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
            $"<{operation} xmlns=\"{SecurityElement.Escape(Ns)}\">{auth}{inner}</{operation}></soap:Body></soap:Envelope>",
            $"{Ns}/{operation}");
}

internal static class AduanaXml
{
    /// <summary>A child of the answer by local name; every customs service qualifies all of them.</summary>
    public static string V(this XElement element, string name) =>
        element.DescendantsAndSelf().First(e => e.Name.LocalName == name).Value;

    public static IEnumerable<XElement> All(this XElement element, string name) =>
        element.Descendants().Where(e => e.Name.LocalName == name);

    public static string Code(this XElement result) =>
        result.DescendantsAndSelf().First(e => e.Name.LocalName is "CodErr" or "codError" or "CodError" or "codigoError" or "Codigo").Value;
}
