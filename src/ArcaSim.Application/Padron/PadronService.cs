using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Access;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Soap;
using ArcaSim.Domain;

namespace ArcaSim.Application.Padron;

/// <summary>
/// The padrón web services: the constancia de inscripción (A5) and A13
/// (docs/arca/catalogo.md §5.1, §5.2). They answer in the Java dialect:
/// unqualified elements in alphabetical order, errors as faults with HTTP 500.
/// </summary>
public sealed class PadronService(TicketReader tickets, PadronDirectory directory, IClock clock)
{
    public const string A5Namespace = "http://a5.soap.ws.server.puc.sr/";
    public const string A13Namespace = "http://a13.soap.ws.server.puc.sr/";

    /// <summary>The constancia keeps answering to its old WSAA name as well.</summary>
    public static readonly string[] A5Services = ["ws_sr_constancia_inscripcion", "ws_sr_padron_a5"];
    public static readonly string[] A13Services = ["ws_sr_padron_a13"];

    private const int MaxListSize = 250;

    public static readonly string[] A5Operations = ["dummy", "getPersona", "getPersona_v2", "getPersonaList", "getPersonaList_v2"];
    public static readonly string[] A13Operations = ["dummy", "getPersona", "getPersonaV2", "getIdPersonaListByDocumento"];

    public Task<SoapResult> A5Async(string operation, XElement request, CancellationToken ct) => operation switch
    {
        "dummy" => Task.FromResult(Dummy(A5Namespace)),
        "getPersona" or "getPersona_v2" => ConstanciaAsync(operation, request, list: false, ct),
        "getPersonaList" or "getPersonaList_v2" => ConstanciaAsync(operation, request, list: true, ct),
        _ => throw new ArgumentException(operation),
    };

    public Task<SoapResult> A13Async(string operation, XElement request, CancellationToken ct) => operation switch
    {
        "dummy" => Task.FromResult(Dummy(A13Namespace)),
        "getPersona" or "getPersonaV2" => A13PersonaAsync(operation, request, ct),
        "getIdPersonaListByDocumento" => A13ByDocumentAsync(request, ct),
        _ => throw new ArgumentException(operation),
    };

    // ---- A5: constancia de inscripción -----------------------------------------

    private async Task<SoapResult> ConstanciaAsync(string operation, XElement request, bool list, CancellationToken ct)
    {
        if (Authenticate(request, A5Services, A5Namespace) is { } fault) return SoapResult.Fail(fault);
        XNamespace ns = A5Namespace;
        var current = operation.EndsWith("_v2", StringComparison.Ordinal);

        if (!list)
        {
            var id = request.ChildLong("idPersona") ?? 0;
            var taxpayer = Cuits.IsValid(id) ? await directory.FindAsync(id, ct) : null;
            if (taxpayer is null) return SoapResult.Fail(Fault(Cuits.IsValid(id) ? "No existe persona con ese Id" : "La clave ingresada no es una CUIT", A5Namespace));
            return SoapResult.Ok(new XElement(ns + $"{operation}Response",
                new XElement("personaReturn", ConstanciaOf(taxpayer, current), Metadata())));
        }

        var ids = request.Children("idPersona").Select(e => e.Value).ToList();
        if (ids.Count > MaxListSize) return SoapResult.Fail(Fault($"La cantidad de claves a consultar no puede superar {MaxListSize}", A5Namespace));
        var people = new List<XElement>();
        foreach (var text in ids)
        {
            var id = long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            var taxpayer = Cuits.IsValid(id) ? await directory.FindAsync(id, ct) : null;
            people.Add(taxpayer is null
                ? new XElement("persona", new XElement("errorConstancia",
                    new XElement("error", Cuits.IsValid(id) ? "No existe persona con ese Id" : "La clave ingresada no es una CUIT"),
                    new XElement("idPersona", id)))
                : new XElement("persona", ConstanciaOf(taxpayer, current)));
        }
        return SoapResult.Ok(new XElement(ns + $"{operation}Response",
            new XElement("personaListReturn", Metadata(), people)));
    }

    /// <summary>
    /// The constancia's blocks for one taxpayer. The VAT condition shows the
    /// way the padrón shows it: IVA (30) or IVA EXENTO (32) under the general
    /// regime, MONOTRIBUTO (20) under its own block, or neither.
    /// </summary>
    private static IEnumerable<XElement> ConstanciaOf(Taxpayer taxpayer, bool current)
    {
        var period = PadronDirectory.PeriodOf(taxpayer);
        var (activityId, activity) = PadronDirectory.ActivityOf(taxpayer);
        XElement Activity(string name) => new(name,
            new XElement("descripcionActividad", activity),
            new XElement("idActividad", activityId),
            new XElement("nomenclador", 883),
            new XElement("orden", 1),
            new XElement("periodo", period));
        XElement Tax(int id, string description) => new("impuesto",
            new XElement("descripcionImpuesto", description),
            new XElement("estadoImpuesto", "AC"),
            new XElement("idImpuesto", id),
            new XElement("motivo", "ALTA DE IMPUESTO"),
            new XElement("periodo", period));

        yield return GeneralData(taxpayer, current);

        switch (taxpayer.VatCondition)
        {
            case VatCondition.Monotributo or VatCondition.MonotributistaSocial or VatCondition.MonotributoTrabajadorIndependientePromovido:
                yield return new XElement("datosMonotributo",
                    Activity("actividad"),
                    Activity("actividadMonotributista"),
                    new XElement("categoriaMonotributo",
                        new XElement("descripcionCategoria", "A LOCACIONES DE SERVICIOS"),
                        new XElement("idCategoria", 20),
                        new XElement("idImpuesto", 20),
                        new XElement("periodo", period)),
                    Tax(20, "MONOTRIBUTO"));
                yield return new XElement("errorRegimenGeneral",
                    new XElement("mensaje", "No cumple con las condiciones para enviar datos del regimen general"));
                break;

            case VatCondition.ResponsableInscripto or VatCondition.Exento:
                yield return new XElement("datosRegimenGeneral",
                    Activity("actividad"),
                    taxpayer.Kind == PersonKind.Juridica ? Tax(10, "GANANCIAS SOCIEDADES") : Tax(11, "GANANCIAS PERSONAS FISICAS"),
                    taxpayer.VatCondition == VatCondition.Exento ? Tax(32, "IVA EXENTO") : Tax(30, "IVA"));
                yield return new XElement("errorMonotributo",
                    new XElement("mensaje", "No cumple con las condiciones para enviar datos del monotributo"));
                break;

            default:
                yield return new XElement("errorMonotributo",
                    new XElement("mensaje", "No cumple con las condiciones para enviar datos del monotributo"));
                yield return new XElement("errorRegimenGeneral",
                    new XElement("mensaje", "No cumple con las condiciones para enviar datos del regimen general"));
                break;
        }
    }

    private static XElement GeneralData(Taxpayer taxpayer, bool current)
    {
        var address = PadronDirectory.AddressOf(taxpayer);
        var physical = taxpayer.Kind == PersonKind.Fisica;
        var (first, last) = PadronDirectory.NamesOf(taxpayer);
        return new XElement("datosGenerales",
            physical ? new XElement("apellido", last) : null,
            current && taxpayer.Profile.RegisteredOn is { } since
                ? new XElement("caracterizacion",
                    new XElement("descripcionCaracterizacion", "GANANCIAS SIMPLIFICADA LEY 27.779"),
                    new XElement("idCaracterizacion", 639),
                    new XElement("periodo", since.ToString("yyyyMMdd", CultureInfo.InvariantCulture)))
                : null,
            new XElement("domicilioFiscal",
                new XElement("codPostal", address.PostalCode),
                new XElement("descripcionProvincia", address.Province),
                new XElement("direccion", address.Street),
                new XElement("idProvincia", address.ProvinceId),
                new XElement("localidad", address.Locality),
                new XElement("tipoDomicilio", "FISCAL")),
            new XElement("esSucesion", "NO"),
            new XElement("estadoClave", taxpayer.Active ? "ACTIVO" : "INACTIVO"),
            physical ? null : new XElement("fechaContratoSocial", Timestamp(taxpayer.Profile.RegisteredOn)),
            new XElement("idPersona", taxpayer.Cuit),
            new XElement("mesCierre", 12),
            physical ? new XElement("nombre", first) : null,
            physical ? null : new XElement("razonSocial", taxpayer.Name.ToUpperInvariant()),
            new XElement("tipoClave", "CUIT"),
            new XElement("tipoPersona", physical ? "FISICA" : "JURIDICA"));
    }

    // ---- A13 ---------------------------------------------------------------------

    private async Task<SoapResult> A13PersonaAsync(string operation, XElement request, CancellationToken ct)
    {
        if (Authenticate(request, A13Services, A13Namespace) is { } fault) return SoapResult.Fail(fault);
        var id = request.ChildLong("idPersona") ?? 0;
        if (!Cuits.IsValid(id)) return SoapResult.Fail(Fault("El Id de la persona no es valido", A13Namespace));
        var taxpayer = await directory.FindAsync(id, ct);
        if (taxpayer is null) return SoapResult.Fail(Fault("La Clave (CUIT/CUIL) consultada es inexistente", A13Namespace));
        if (!taxpayer.Active && operation == "getPersona") return SoapResult.Fail(Fault("La clave (CUIT/CUIL) consultada se encuentra INACTIVA", A13Namespace));

        XNamespace ns = A13Namespace;
        return SoapResult.Ok(new XElement(ns + $"{operation}Response",
            new XElement("personaReturn", Metadata(), A13PersonOf(taxpayer))));
    }

    private static XElement A13PersonOf(Taxpayer taxpayer)
    {
        var address = PadronDirectory.AddressOf(taxpayer);
        var physical = taxpayer.Kind == PersonKind.Fisica;
        var (first, last) = PadronDirectory.NamesOf(taxpayer);
        var (activityId, activity) = PadronDirectory.ActivityOf(taxpayer);
        var words = address.Street.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasNumber = words.Length > 1 && int.TryParse(words[^1], out _);

        return new XElement("persona",
            physical ? new XElement("apellido", last) : null,
            new XElement("descripcionActividadPrincipal", activity),
            new XElement("domicilio",
                new XElement("calle", hasNumber ? string.Join(' ', words[..^1]) : address.Street),
                new XElement("codigoPostal", address.PostalCode),
                new XElement("descripcionProvincia", address.Province),
                new XElement("direccion", address.Street),
                new XElement("estadoDomicilio", "CONFIRMADO"),
                new XElement("idProvincia", address.ProvinceId),
                new XElement("localidad", address.Locality),
                hasNumber ? new XElement("numero", words[^1]) : null,
                new XElement("tipoDomicilio", "FISCAL")),
            new XElement("estadoClave", taxpayer.Active ? "ACTIVO" : "INACTIVO"),
            physical ? null : new XElement("fechaContratoSocial", Timestamp(taxpayer.Profile.RegisteredOn)),
            physical ? null : new XElement("formaJuridica", taxpayer.Profile.LegalForm ?? "SOC. ANONIMA"),
            new XElement("idActividadPrincipal", activityId),
            new XElement("idPersona", taxpayer.Cuit),
            new XElement("mesCierre", 12),
            physical ? new XElement("nombre", first) : null,
            physical ? new XElement("numeroDocumento", PadronDirectory.DocumentOf(taxpayer)) : null,
            new XElement("periodoActividadPrincipal", PadronDirectory.PeriodOf(taxpayer)),
            physical ? null : new XElement("razonSocial", taxpayer.Name.ToUpperInvariant()),
            new XElement("tipoClave", "CUIT"),
            physical ? new XElement("tipoDocumento", "DNI") : null,
            new XElement("tipoPersona", physical ? "FISICA" : "JURIDICA"));
    }

    private async Task<SoapResult> A13ByDocumentAsync(XElement request, CancellationToken ct)
    {
        if (Authenticate(request, A13Services, A13Namespace) is { } fault) return SoapResult.Fail(fault);
        var document = request.ChildText("documento") ?? "";
        var ids = await directory.ByDocumentAsync(document, ct);
        if (ids.Count == 0) return SoapResult.Fail(Fault("No existe persona con ese documento", A13Namespace));
        XNamespace ns = A13Namespace;
        return SoapResult.Ok(new XElement(ns + "getIdPersonaListByDocumentoResponse",
            new XElement("idPersonaListReturn", ids.Select(id => new XElement("idPersona", id)), Metadata())));
    }

    // ---- Shared --------------------------------------------------------------------

    private static SoapResult Dummy(string ns) => SoapResult.Ok(new XElement(XName.Get("dummyResponse", ns),
        new XElement("return",
            new XElement("appserver", "OK"),
            new XElement("authserver", "OK"),
            new XElement("dbserver", "OK"))));

    /// <summary>
    /// token, sign and cuitRepresentada travel loose in the request. The texts
    /// are the ones observed against homologación and listed in the manuals
    /// (catalogo.md §4.1, §5.2).
    /// </summary>
    private SoapFault? Authenticate(XElement request, string[] services, string ns)
    {
        var cuit = request.ChildLong("cuitRepresentada") ?? 0;
        var check = tickets.Check(request.Child("token")?.Value, request.Child("sign")?.Value, cuit, services);
        return check.Problem switch
        {
            TicketProblem.None => null,
            TicketProblem.MissingToken or TicketProblem.MissingSign => Fault("Falta token y/o sign.", ns),
            TicketProblem.Unreadable => Fault("Token malformado", ns),
            TicketProblem.BadSignature => Fault("No se pudo verificar que <sign> contenga una firma valida de <token>", ns),
            TicketProblem.OutOfDate => Fault("Token vencido", ns),
            TicketProblem.WrongService => Fault("No autorizado, par token/sign invalido.", ns),
            _ => cuit == 0
                ? Fault("Debe enviar la CUIT representada", ns)
                : Fault($"Este token no le permite actuar en representacion de la CUIT {cuit}", ns),
        };
    }

    /// <summary>Every padrón error is a SRValidationException fault blamed on the server.</summary>
    private static SoapFault Fault(string message, string ns) => new("soap:Server", message,
        new XElement(XName.Get("SRValidationException", ns)));

    private XElement Metadata() => new("metadata",
        new XElement("fechaHora", clock.Now.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)),
        new XElement("servidor", "arcasim"));

    private static string Timestamp(DateOnly? day) =>
        (day ?? new DateOnly(2015, 1, 1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00-03:00";
}
