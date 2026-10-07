using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Arca.Client;

/// <summary>
/// WSFEv1, the electronic invoicing service: the calls an application needs to
/// authorize vouchers and find out what happened to them. It talks plain
/// SOAP 1.1 to whatever WsfeUrl says, ArcaSim or ARCA.
/// </summary>
public sealed class WsfeClient(HttpClient http, WsaaClient wsaa, ArcaOptions options)
{
    public const string Service = "wsfe";
    private const string Ns = "http://ar.gov.afip.dif.FEV1/";
    private static readonly XNamespace X = Ns;

    public async Task<ServerStatus> DummyAsync(CancellationToken ct = default)
    {
        var result = await CallAsync("FEDummy", _ => { }, authenticated: false, ct);
        return new ServerStatus(Text(result, "AppServer"), Text(result, "DbServer"), Text(result, "AuthServer"));
    }

    /// <summary>The last number authorized for a point of sale and voucher type; 0 when there is none yet.</summary>
    public async Task<long> LastAuthorizedAsync(int pointOfSale, int voucherType, CancellationToken ct = default)
    {
        var result = await CallAsync("FECompUltimoAutorizado", w =>
        {
            w.WriteElementString("PtoVta", Ns, Invariant(pointOfSale));
            w.WriteElementString("CbteTipo", Ns, Invariant(voucherType));
        }, authenticated: true, ct);
        ThrowOnErrors(result);
        return Long(result, "CbteNro");
    }

    /// <summary>A voucher already authorized, or null when ARCA has no such number.</summary>
    public async Task<AuthorizedVoucher?> QueryAsync(int pointOfSale, int voucherType, long number, CancellationToken ct = default)
    {
        var result = await CallAsync("FECompConsultar", w =>
        {
            w.WriteStartElement("FeCompConsReq", Ns);
            w.WriteElementString("CbteTipo", Ns, Invariant(voucherType));
            w.WriteElementString("CbteNro", Ns, Invariant(number));
            w.WriteElementString("PtoVta", Ns, Invariant(pointOfSale));
            w.WriteEndElement();
        }, authenticated: true, ct);

        var found = result.Element(X + "ResultGet");
        if (found is null)
        {
            var errors = Messages(result.Element(X + "Errors"));
            if (errors.Count == 0 || errors.All(e => e.Code == 602)) return null;
            throw Errors(errors);
        }
        return new AuthorizedVoucher(
            Int(found, "PtoVta"),
            Int(found, "CbteTipo"),
            Long(found, "CbteDesde"),
            Date(found, "CbteFch"),
            Int(found, "DocTipo"),
            Long(found, "DocNro"),
            Decimal(found, "ImpTotal"),
            Text(found, "CodAutorizacion"),
            Text(found, "EmisionTipo"),
            Date(found, "FchVto"),
            Messages(found.Element(X + "Observaciones")));
    }

    /// <summary>
    /// Asks for the CAE of one voucher with the number it carries. A rejection
    /// comes back as a result with its errors and observations.
    /// </summary>
    public async Task<AuthorizationResult> AuthorizeAsync(int pointOfSale, int voucherType, Voucher voucher, CancellationToken ct = default)
    {
        var result = await CallAsync("FECAESolicitar", w => WriteRequest(w, pointOfSale, voucherType, voucher), authenticated: true, ct);
        var errors = Messages(result.Element(X + "Errors"));
        var detail = result.Element(X + "FeDetResp")?.Element(X + "FECAEDetResponse");
        if (detail is null)
        {
            ThrowOnErrors(result);
            throw new ArcaUnavailableException("FECAESolicitar answered without a detail.");
        }

        var approved = Text(detail, "Resultado") == "A";
        var cae = Text(detail, "CAE");
        var due = Text(detail, "CAEFchVto");
        return new AuthorizationResult(
            approved,
            voucher.Number,
            Date(detail, "CbteFch"),
            approved ? cae : null,
            approved && due.Length == 8 ? Date(detail, "CAEFchVto") : null,
            Messages(detail.Element(X + "Observaciones")),
            errors);
    }

    /// <summary>
    /// Takes the next number and authorizes the voucher with it. If the answer
    /// gets lost (a timeout, a dropped connection) or cannot be read, it asks
    /// FECompConsultar whether that number was authorized before giving up, which
    /// is ARCA's own recovery procedure (wsfev1.md §5.4): a blind retry would
    /// fail with 10016.
    /// </summary>
    public async Task<AuthorizationResult> AuthorizeNextAsync(int pointOfSale, int voucherType, Voucher voucher, CancellationToken ct = default)
    {
        var number = await LastAuthorizedAsync(pointOfSale, voucherType, ct) + 1;
        var numbered = voucher with { Number = number };
        try
        {
            return await AuthorizeAsync(pointOfSale, voucherType, numbered, ct);
        }
        catch (Exception ex) when (ex is ArcaUnavailableException or ArcaBadResponseException)
        {
            var existing = await QueryAsync(pointOfSale, voucherType, number, ct);
            if (existing is null || existing.Total != voucher.Total || existing.DocumentNumber != voucher.DocumentNumber) throw;
            return new AuthorizationResult(true, number, existing.Date, existing.AuthorizationCode, existing.Due, existing.Observations, [])
            {
                Recovered = true,
            };
        }
    }

    private static void WriteRequest(XmlWriter w, int pointOfSale, int voucherType, Voucher v)
    {
        w.WriteStartElement("FeCAEReq", Ns);
        w.WriteStartElement("FeCabReq", Ns);
        w.WriteElementString("CantReg", Ns, "1");
        w.WriteElementString("PtoVta", Ns, Invariant(pointOfSale));
        w.WriteElementString("CbteTipo", Ns, Invariant(voucherType));
        w.WriteEndElement();
        w.WriteStartElement("FeDetReq", Ns);
        w.WriteStartElement("FECAEDetRequest", Ns);
        w.WriteElementString("Concepto", Ns, Invariant(v.Concept));
        w.WriteElementString("DocTipo", Ns, Invariant(v.DocumentType));
        w.WriteElementString("DocNro", Ns, Invariant(v.DocumentNumber));
        w.WriteElementString("CbteDesde", Ns, Invariant(v.Number));
        w.WriteElementString("CbteHasta", Ns, Invariant(v.Number));
        if (v.Date is { } date) w.WriteElementString("CbteFch", Ns, FormatDate(date));
        w.WriteElementString("ImpTotal", Ns, Invariant(v.Total));
        w.WriteElementString("ImpTotConc", Ns, Invariant(v.NotTaxed));
        w.WriteElementString("ImpNeto", Ns, Invariant(v.Net));
        w.WriteElementString("ImpOpEx", Ns, Invariant(v.Exempt));
        w.WriteElementString("ImpTrib", Ns, Invariant(v.OtherTaxes));
        w.WriteElementString("ImpIVA", Ns, Invariant(v.Vat));
        if (v.ServiceFrom is { } from) w.WriteElementString("FchServDesde", Ns, FormatDate(from));
        if (v.ServiceTo is { } to) w.WriteElementString("FchServHasta", Ns, FormatDate(to));
        if (v.PaymentDue is { } due) w.WriteElementString("FchVtoPago", Ns, FormatDate(due));
        w.WriteElementString("MonId", Ns, v.Currency);
        w.WriteElementString("MonCotiz", Ns, Invariant(v.ExchangeRate));
        if (v.PaidInSameCurrency is { } same) w.WriteElementString("CanMisMonExt", Ns, same ? "S" : "N");
        if (v.ReceiverVatCondition is { } condition) w.WriteElementString("CondicionIVAReceptorId", Ns, Invariant(condition));

        if (v.Associated.Count > 0)
        {
            w.WriteStartElement("CbtesAsoc", Ns);
            foreach (var a in v.Associated)
            {
                w.WriteStartElement("CbteAsoc", Ns);
                w.WriteElementString("Tipo", Ns, Invariant(a.Type));
                w.WriteElementString("PtoVta", Ns, Invariant(a.PointOfSale));
                w.WriteElementString("Nro", Ns, Invariant(a.Number));
                if (a.Cuit is { } cuit) w.WriteElementString("Cuit", Ns, Invariant(cuit));
                if (a.Date is { } associatedDate) w.WriteElementString("CbteFch", Ns, FormatDate(associatedDate));
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        if (v.OtherTaxLines.Count > 0)
        {
            w.WriteStartElement("Tributos", Ns);
            foreach (var t in v.OtherTaxLines)
            {
                w.WriteStartElement("Tributo", Ns);
                w.WriteElementString("Id", Ns, Invariant(t.Id));
                if (t.Description is not null) w.WriteElementString("Desc", Ns, t.Description);
                w.WriteElementString("BaseImp", Ns, Invariant(t.Base));
                w.WriteElementString("Alic", Ns, Invariant(t.Rate));
                w.WriteElementString("Importe", Ns, Invariant(t.Amount));
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        if (v.VatLines.Count > 0)
        {
            w.WriteStartElement("Iva", Ns);
            foreach (var line in v.VatLines)
            {
                w.WriteStartElement("AlicIva", Ns);
                w.WriteElementString("Id", Ns, Invariant(line.Id));
                w.WriteElementString("BaseImp", Ns, Invariant(line.Base));
                w.WriteElementString("Importe", Ns, Invariant(line.Amount));
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>
    /// One operation: Auth first when the operation takes it, then the
    /// parameters. A token WSFEv1 refuses is dropped, and only that one (a
    /// caller that already replaced it keeps its new ticket), and the call is
    /// made once more with a new one.
    /// </summary>
    private async Task<XElement> CallAsync(string operation, Action<XmlWriter> parameters, bool authenticated, CancellationToken ct)
    {
        var (result, ticket) = await SendAsync(operation, parameters, authenticated, ct);
        if (ticket is not null && Messages(result.Element(X + "Errors")).Any(e => e.Code == 600 && IsTicketProblem(e.Message)))
        {
            wsaa.Forget(Service, ticket);
            (result, _) = await SendAsync(operation, parameters, authenticated, ct);
        }
        return result;
    }

    /// <summary>One request and its answer's result element, with the ticket the request carried (null when the operation takes none).</summary>
    private async Task<(XElement Result, AccessTicket? Ticket)> SendAsync(string operation, Action<XmlWriter> parameters, bool authenticated, CancellationToken ct)
    {
        var ticket = authenticated ? await wsaa.GetTicketAsync(Service, ct) : null;
        var builder = new StringBuilder();
        using (var w = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            w.WriteStartElement("soap", "Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
            w.WriteStartElement("soap", "Body", "http://schemas.xmlsoap.org/soap/envelope/");
            w.WriteStartElement(operation, Ns);
            if (ticket is not null)
            {
                w.WriteStartElement("Auth", Ns);
                w.WriteElementString("Token", Ns, ticket.Token);
                w.WriteElementString("Sign", Ns, ticket.Sign);
                w.WriteElementString("Cuit", Ns, Invariant(options.Cuit));
                w.WriteEndElement();
            }
            parameters(w);
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        using var content = new StringContent(builder.ToString(), Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8");
        using var message = new HttpRequestMessage(HttpMethod.Post, options.WsfeUrl) { Content = content };
        message.Headers.Add("SOAPAction", $"\"{Ns}{operation}\"");

        var (status, body) = await SoapTransport.SendAsync(http, message, "WSFEv1", ct);
        if (status != 200) throw SoapTransport.Failed("WSFEv1", status, SoapTransport.FaultText(body));

        var document = SoapTransport.Parse(body, "WSFEv1");
        var result = document.Descendants(X + $"{operation}Result").FirstOrDefault();
        if (result is null)
        {
            // A fault that travels with HTTP 200 is read like the one that comes with 500.
            var fault = SoapTransport.FaultText(document);
            if (SoapTransport.IsUnreadableRequest(fault)) throw SoapTransport.Failed("WSFEv1", status, fault);
            throw new ArcaUnavailableException($"WSFEv1's answer has no {operation}Result.");
        }
        var errors = Messages(result.Element(X + "Errors"));
        if (errors.Count > 0 && errors.All(e => e.Code is 500 or 501 or 502) && !errors.Any(e => e.Message.StartsWith("Campo Auth", StringComparison.Ordinal)))
            throw new ArcaUnavailableException("WSFEv1: " + string.Join("; ", errors.Select(e => $"{e.Code} {e.Message}")));
        return (result, ticket);
    }

    private static bool IsTicketProblem(string message) =>
        message.Contains("fechas del token", StringComparison.Ordinal) || message.Contains("verificar hash", StringComparison.Ordinal);

    private static void ThrowOnErrors(XElement result)
    {
        var errors = Messages(result.Element(X + "Errors"));
        if (errors.Count > 0) throw Errors(errors);
    }

    private static WsfeErrorException Errors(IReadOnlyList<ArcaMessage> errors) => new(errors);

    private static IReadOnlyList<ArcaMessage> Messages(XElement? list) =>
        list?.Elements()
            .Select(e => new ArcaMessage(Int(e, "Code"), Text(e, "Msg")))
            .ToList() ?? [];

    private static string Text(XElement parent, string name) => parent.Element(X + name)?.Value ?? "";

    // A field that should be a number or a date and is not: ARCA's answer cannot be read.

    private static int Int(XElement parent, string name) =>
        int.TryParse(Text(parent, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw Unreadable(parent, name);

    private static long Long(XElement parent, string name) =>
        long.TryParse(Text(parent, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw Unreadable(parent, name);

    private static decimal Decimal(XElement parent, string name) =>
        decimal.TryParse(Text(parent, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : throw Unreadable(parent, name);

    private static DateOnly Date(XElement parent, string name) =>
        DateOnly.TryParseExact(Text(parent, name), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : throw Unreadable(parent, name);

    private static ArcaBadResponseException Unreadable(XElement parent, string name) =>
        new($"WSFEv1 sent \"{Text(parent, name)}\" in <{name}> of <{parent.Name.LocalName}>, which cannot be read.");

    private static string Invariant(IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
