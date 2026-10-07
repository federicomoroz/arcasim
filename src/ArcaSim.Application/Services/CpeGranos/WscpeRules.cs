using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using static ArcaSim.Application.Services.CpeGranos.WscpeTables;

namespace ArcaSim.Application.Services.CpeGranos;

/// <summary>A carta de porte as wscpe keeps it: its key, its CTG, its state and the request it was authorized with, as later operations changed it.</summary>
public sealed record StoredCpe(
    string Family,
    long Issuer,
    int Type,
    int Branch,
    int OrderNumber,
    long Ctg,
    string State,
    DateTimeOffset IssuedAt,
    DateTimeOffset StateSince,
    string Request,
    int Detours = 0,
    int NewDestinations = 0,
    int Edits = 0,
    int? VoidReason = null,
    string? VoidNotes = null);

public sealed record CpeKey(string Key);

public sealed record CpeLastOrder(int Number);

/// <summary>
/// Carta de Porte Electrónica (docs/arca/servicios/wscpe.md): the CPE
/// lifecycle of the automotor, ferroviaria and derivados granarios families.
/// Authorizing takes the next order number of the issuer's branch and type
/// and assigns a CTG (XXYY + 8 digits for grains, XX + 10 for DG); consults
/// read it back by key or CTG; the transitions follow the state diagrams;
/// the edits change data without changing the state. Business errors go in
/// respuesta/errores with the codes and texts of anexo 4.1.
/// ArcaSim's choices where the spec says NO VERIFICADO: consultarUltNroOrden
/// counts per CUIT, branch and type and answers 0 before the first CPE; the
/// order number must be exactly the last + 1 (961), and one already used
/// answers 2241 with its CTG; an invalid transition answers 2034 with the
/// state codes; a CPE answers only to the CUITs it names (2037 or 2039); each
/// active state lasts 15 days and anularCPE after that answers 2121.
/// </summary>
public sealed class WscpeRules(IDocumentStore store, IClock clock, SequenceLocks locks) : IServiceBehavior
{
    private const string Collection = "wscpe";
    private const string CtgCollection = "wscpe-ctg";
    private const string LastCollection = "wscpe-ultimo";

    public string Service => "wscpe";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name == "dummy") return Dummy(call);
        if (call.Name == "consultarProvincias") return ListProvinces(call);
        if (call.Name == "consultarUltNroOrden") return await LastOrderAsync(call, ct);
        if (CpeFamily.All.FirstOrDefault(f => f.Authorize == call.Name) is { } authorize) return await AuthorizeAsync(call, authorize, ct);
        if (CpeFamily.All.FirstOrDefault(f => f.Consult == call.Name) is { } consult) return await ConsultAsync(call, consult, ct);
        if (CpeMove.ByOperation.TryGetValue(call.Name, out var move)) return await MoveAsync(call, move, ct);
        if (CpeEdit.ByOperation.TryGetValue(call.Name, out var edit)) return await EditAsync(call, edit, ct);
        return call.Name switch
        {
            "consultarCPEPorDestino" => await ByDestinationAsync(call, ct),
            "consultarCPEPPendientesDeResolucion" => await PendingAsync(call, ct),
            "consultarCPEDGPendienteActivacion" => await AwaitingActivationAsync(call, "PE", ct),
            "consultarCPEEmitidasDestinoDGPendientesActivacion" => await AwaitingActivationAsync(call, "PO", ct),
            _ => null,
        };
    }

    // ---- Service and parameters ------------------------------------------------------

    /// <summary>Observed in homologación: "Ok", not the manual's "OK".</summary>
    private static ContractAnswer Dummy(ServiceCall call)
    {
        var answer = call.Sample();
        foreach (var name in new[] { "appserver", "authserver", "dbserver" }) answer.Set(name, "Ok");
        return call.Ok(answer);
    }

    private ContractAnswer ListProvinces(ServiceCall call)
    {
        var answer = call.Sample().Repeat("provincia", Provinces, (e, p) => e.Set("codigo", p.Code).Set("descripcion", p.Name));
        Metadata(answer);
        return call.Ok(answer);
    }

    private async Task<ContractAnswer> LastOrderAsync(ServiceCall call, CancellationToken ct)
    {
        var request = Solicitud(call);
        var type = request.Int("tipoCPE");
        var branch = request.Int("sucursal");
        if (!CpeFamily.KnownTypes.Contains(type)) return call.Error(801, Codes.InvalidQueryType);
        if (branch < 1) return call.Error(962, Codes.WrongBranch);
        var answer = call.Sample().Set("nroOrden", await LastAsync(call.Cuit, type, branch, ct));
        Metadata(answer);
        return call.Ok(answer);
    }

    // ---- Authorization ---------------------------------------------------------------

    private async Task<ContractAnswer> AuthorizeAsync(ServiceCall call, CpeFamily family, CancellationToken ct)
    {
        var request = Solicitud(call);
        var header = request.Child("cabecera") ?? new XElement("cabecera");
        var type = header.Child("tipoCP") is null && family.Types.Length == 1 ? family.Types[0] : header.Int("tipoCP");
        if (!family.Types.Contains(type)) return call.Error(2055, Codes.NotForThisType);
        if (header.Child("cuitSolicitante") is { } applicant && header.Long("cuitSolicitante") != call.Cuit)
            return call.Error(2037, Fill(Codes.NotYourApplicant, applicant.Value.Trim()));
        var branch = header.Int("sucursal");
        if (branch < 1) return call.Error(962, Codes.WrongBranch);
        var order = header.Int("nroOrden");

        using var _ = await locks.AcquireAsync(Service, call.Cuit, branch, type, ct);
        if (await store.GetAsync<StoredCpe>(Collection, Key(call.Cuit, type, branch, order), ct) is { } issued)
            return call.Error(2241, Fill(Codes.AlreadyIssued, issued.Ctg));
        if (order != await LastAsync(call.Cuit, type, branch, ct) + 1) return call.Error(961, Codes.WrongOrderNumber);

        var now = clock.Now;
        var cpe = new StoredCpe(family.Name, call.Cuit, type, branch, order, await NextCtgAsync(family, request, ct),
            family.InitialState, now, now, request.ToString(SaveOptions.DisableFormatting));
        await SaveAsync(cpe, ct);
        await store.PutAsync(CtgCollection, cpe.Ctg.ToString(CultureInfo.InvariantCulture), new CpeKey(Key(cpe)), ct);
        await store.PutAsync(LastCollection, LastKey(call.Cuit, type, branch), new CpeLastOrder(order), ct);
        return call.Ok(Detail(call, cpe));
    }

    /// <summary>Granos: XX family, YY 01 from a producer's field or 02 from a plant, 8 digits; DG: XX and 10 digits (pág. 213).</summary>
    private async Task<long> NextCtgAsync(CpeFamily family, XElement request, CancellationToken ct)
    {
        var sequence = await store.NextAsync("wscpe-ctg", ct);
        if (!family.Grains) return long.Parse(family.CtgPrefix + (sequence % 10_000_000_000).ToString("D10", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var field = request.Child("origen")?.Child("productor") is not null || request.Text("esSolicitanteCampo") == "true";
        return long.Parse(family.CtgPrefix + (field ? "01" : "02") + (sequence % 100_000_000).ToString("D8", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    // ---- Consults --------------------------------------------------------------------

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CpeFamily family, CancellationToken ct)
    {
        var request = Solicitud(call);
        StoredCpe? cpe;
        if (request.Child("cartaPorte") is { } key)
        {
            if (!family.Types.Contains(key.Int("tipoCPE"))) return call.Error(801, Codes.InvalidQueryType);
            var issuer = request.Child("cuitSolicitante") is null ? call.Cuit : request.Long("cuitSolicitante");
            cpe = await store.GetAsync<StoredCpe>(Collection, Key(issuer, key.Int("tipoCPE"), key.Int("sucursal"), key.Int("nroOrden")), ct);
        }
        else if (request.Child("nroCTG") is not null) cpe = await ByCtgAsync(request.Long("nroCTG"), ct);
        else return call.Error(950, Fill(Codes.Required, "nroCTG"));

        if (cpe is null || cpe.Family != family.Name) return call.Error(1302, Codes.NotFound);
        if (!Names(cpe).Contains(call.Cuit)) return call.Error(2039, Codes.NotYourRequest);
        return call.Ok(Detail(call, cpe));
    }

    private async Task<ContractAnswer> ByDestinationAsync(ServiceCall call, CancellationToken ct)
    {
        var request = Solicitud(call);
        var from = request.Date("fechaPartidaDesde");
        var to = request.Date("fechaPartidaHasta");
        if (from is null || to is null) return call.Error(950, Fill(Codes.Required, from is null ? "fechaPartidaDesde" : "fechaPartidaHasta"));
        if (to < from || to.Value.DayNumber - from.Value.DayNumber > 3) return call.Error(2152, Codes.DateRange);
        var plant = request.Long("planta");
        var type = request.Int("tipoCartaPorte");
        var found = (await store.ListAsync<StoredCpe>(Collection, "", ct)).Where(cpe =>
        {
            var stored = XElement.Parse(cpe.Request);
            var departure = Departure(stored);
            return DestinationCuit(stored) == call.Cuit && stored.Child("destino")?.Long("planta") == plant
                   && (type == 0 || cpe.Type == type)
                   && departure is { } d && d.ArgentinaDate() is var day && day >= from && day <= to;
        }).ToList();
        return Summaries(call, found);
    }

    /// <summary>Perfil S: the caller's own CPE still open; D: the open ones that go to the caller (optionally to one plant).</summary>
    private async Task<ContractAnswer> PendingAsync(ServiceCall call, CancellationToken ct)
    {
        var request = Solicitud(call);
        var asDestination = string.Equals(request.Text("perfil"), "D", StringComparison.OrdinalIgnoreCase);
        var plant = request.Child("planta") is null ? (long?)null : request.Long("planta");
        var found = (await store.ListAsync<StoredCpe>(Collection, "", ct)).Where(cpe =>
        {
            if (!Pending.Contains(cpe.State)) return false;
            var stored = XElement.Parse(cpe.Request);
            if (!asDestination) return cpe.Issuer == call.Cuit && (plant is null || OriginPlant(stored) == plant);
            return DestinationCuit(stored) == call.Cuit && (plant is null || stored.Child("destino")?.Long("planta") == plant);
        }).ToList();
        return Summaries(call, found);
    }

    /// <summary>PE (industry DG) or PO (issued at destination) whose origin is the caller's plant.</summary>
    private async Task<ContractAnswer> AwaitingActivationAsync(ServiceCall call, string state, CancellationToken ct)
    {
        var plant = Solicitud(call).Long("planta");
        var found = (await store.ListAsync<StoredCpe>(Collection, "", ct))
            .Where(cpe => cpe.State == state && Names(cpe).Contains(call.Cuit) && OriginPlant(XElement.Parse(cpe.Request)) == plant).ToList();
        if (found.Count == 0) return call.Error(800, Codes.NoRequests);
        var answer = call.Sample().Repeat("cartaPorte", found, (e, cpe) => e
            .Set("tipoCartaPorte", cpe.Type).Set("sucursal", cpe.Branch).Set("nroOrden", cpe.OrderNumber)
            .Set("cuitSolicitante", cpe.Issuer).Set("fechaPartida", GrainsFormat.DateTime(Departure(XElement.Parse(cpe.Request)) ?? cpe.IssuedAt)));
        Metadata(answer);
        return call.Ok(answer);
    }

    private ContractAnswer Summaries(ServiceCall call, List<StoredCpe> found)
    {
        if (found.Count == 0) return call.Error(800, Codes.NoRequests);
        var answer = call.Sample().Repeat("cartaPorte", found, (e, cpe) => e
            .Set("tipoCartaPorte", cpe.Type).Set("nroCTG", cpe.Ctg)
            .Set("fechaPartida", GrainsFormat.DateTime(Departure(XElement.Parse(cpe.Request)) ?? cpe.IssuedAt))
            .Set("estado", cpe.State).Set("fechaUltimaModificacion", GrainsFormat.DateTime(cpe.StateSince)));
        Metadata(answer);
        return call.Ok(answer);
    }

    // ---- Transitions -----------------------------------------------------------------

    private async Task<ContractAnswer> MoveAsync(ServiceCall call, CpeMove move, CancellationToken ct)
    {
        var request = Solicitud(call);
        var key = request.Child("cartaPorte");
        if (key is null) return call.Error(950, Fill(Codes.Required, "cartaPorte"));
        var issuer = request.Child("cuitDestino") is not null ? request.Long("cuitDestino")
            : request.Child("cuitSolicitante") is not null ? request.Long("cuitSolicitante")
            : call.Cuit;
        var type = key.Int("tipoCPE");
        var branch = key.Int("sucursal");

        using var _ = await locks.AcquireAsync(Service, issuer, branch, type, ct);
        var cpe = await store.GetAsync<StoredCpe>(Collection, Key(issuer, type, branch, key.Int("nroOrden")), ct);
        if (cpe is null) return call.Error(1302, Codes.NotFound);
        if (!Names(cpe).Contains(call.Cuit))
            return issuer != call.Cuit && request.Child("cuitSolicitante") is not null
                ? call.Error(2037, Fill(Codes.NotYourApplicant, issuer))
                : call.Error(2039, Codes.NotYourRequest);
        if (!move.Families.Contains(cpe.Family)) return call.Error(2055, Codes.NotForThisType);

        var target = move.To;
        if (move.Effect == CpeEffect.CloseContingency)
        {
            target = request.Text("concepto")?.ToUpperInvariant() switch { "A" => "AC", "B" => "CO", "C" => "DE", _ => "" };
            if (target == "") return call.Error(950, Fill(Codes.Required, "concepto"));
        }
        if (!move.From.Contains(cpe.State)) return call.Error(2034, Fill(Codes.InvalidTransition, cpe.State, target));

        var now = clock.Now;
        var stored = XElement.Parse(cpe.Request);
        switch (move.Effect)
        {
            case CpeEffect.Void:
                var reason = request.Child("anulacionMotivo") is null ? (int?)null : request.Int("anulacionMotivo");
                if (reason is not null and not (1 or 2 or 3)) return call.Error(2220, Codes.InvalidVoidReason);
                if (now > cpe.StateSince + StateValidity) return call.Error(2121, Codes.VoidTooLate);
                cpe = cpe with { VoidReason = reason, VoidNotes = request.Text("anulacionObservaciones") };
                break;
            case CpeEffect.Reject:
                if (request.Child("rechazoMotivo") is not null && request.Int("rechazoMotivo") is not (1 or 2 or 3))
                    return call.Error(2225, Codes.InvalidRejectReason);
                break;
            case CpeEffect.FinalConfirmation:
                var load = stored.Child("datosCarga") ?? Add(stored, new XElement("datosCarga"));
                foreach (var weight in new[] { "pesoBrutoDescarga", "pesoTaraDescarga" })
                    if (request.Child(weight) is { } value) Replace(load, new XElement(weight, value.Value.Trim()));
                Replace(stored, request.Child("intervinientes"));
                Replace(stored, request.Child("destinatario"));
                break;
            case CpeEffect.Detour:
                if (cpe.Detours >= MaxDetours) return call.Error(2130, Codes.TooManyDetours);
                Replace(stored, request.Child("destino"));
                Replace(stored, request.Child("transporte"));
                cpe = cpe with { Detours = cpe.Detours + 1 };
                break;
            case CpeEffect.NewDestination:
                if (cpe.NewDestinations >= MaxNewDestinations) return call.Error(2232, Codes.TooManyNewDestinations);
                Replace(stored, request.Child("destino"));
                Replace(stored, request.Child("destinatario"));
                Replace(stored, request.Child("transporte"));
                cpe = cpe with { NewDestinations = cpe.NewDestinations + 1 };
                break;
            case CpeEffect.ReturnToOrigin:
                ReturnToOrigin(stored, request, cpe.Issuer);
                break;
        }

        cpe = cpe with { State = target, StateSince = now, Request = stored.ToString(SaveOptions.DisableFormatting) };
        await SaveAsync(cpe, ct);
        return call.Ok(Summary(call, cpe));
    }

    /// <summary>
    /// The load goes back where it came from: the issuer becomes the
    /// destinatario (historial v1.3) and the destination becomes the origin's
    /// plant, or the plant and destinatario the DG variants name.
    /// </summary>
    private static void ReturnToOrigin(XElement stored, XElement request, long issuer)
    {
        Replace(stored, request.Child("transporte"));
        var origin = stored.Child("origen");
        var destination = new XElement("destino",
            new XElement("cuit", issuer),
            origin?.Descendants().FirstOrDefault(e => e.Name.LocalName == "codProvincia") is { } province ? new XElement("codProvincia", province.Value) : null,
            origin?.Descendants().FirstOrDefault(e => e.Name.LocalName == "codLocalidad") is { } locality ? new XElement("codLocalidad", locality.Value) : null,
            request.Child("planta") is { } plant ? new XElement("planta", plant.Value) : OriginPlant(stored) is { } own ? new XElement("planta", own) : null);
        Replace(stored, destination);
        var recipient = request.Child("cuitDestinatario")?.Value.Trim() ?? issuer.ToString(CultureInfo.InvariantCulture);
        Replace(stored, new XElement("destinatario", new XElement("cuit", recipient)));
    }

    // ---- Edits -----------------------------------------------------------------------

    /// <summary>
    /// Edits find the CPE by CTG and overwrite what they bring: whole blocks
    /// (intervinientes, destino) and loose values, each where the stored
    /// request keeps one of its name; cuitDestinatario is the destinatario.
    /// </summary>
    private async Task<ContractAnswer> EditAsync(ServiceCall call, CpeEdit edit, CancellationToken ct)
    {
        var request = Solicitud(call);
        if (request.Child("nroCTG") is null) return call.Error(950, Fill(Codes.Required, "nroCTG"));
        if (await ByCtgAsync(request.Long("nroCTG"), ct) is not { } found) return call.Error(1302, Codes.NotFound);
        using var _ = await locks.AcquireAsync(Service, found.Issuer, found.Branch, found.Type, ct);
        var cpe = await store.GetAsync<StoredCpe>(Collection, Key(found), ct) ?? found;
        if (!Names(cpe).Contains(call.Cuit)) return call.Error(2039, Codes.NotYourRequest);
        if (!edit.Families.Contains(cpe.Family)) return call.Error(2055, Codes.NotForThisType);
        if (!edit.States.Contains(cpe.State)) return call.Error(2004, Codes.InvalidState);
        if (cpe.Edits >= MaxEdits) return call.Error(2238, Codes.TooManyEdits);

        var stored = XElement.Parse(cpe.Request);
        foreach (var change in request.Elements().Where(e => e.Name.LocalName != "nroCTG").ToList())
        {
            var name = change.Name.LocalName;
            if (change.HasElements) Replace(stored, change);
            else if (name == "cuitDestinatario") Replace(stored, new XElement("destinatario", new XElement("cuit", change.Value.Trim())));
            else if (name == "observaciones") Replace(stored, change);
            else if (stored.Descendants().Where(e => e.Name.LocalName == name && !e.HasElements).ToList() is { Count: > 0 } targets)
            {
                var repeated = request.Elements().Where(e => e.Name.LocalName == name).ToList();
                if (repeated.Count > 1 && repeated[0] != change) continue;
                var anchor = targets[0];
                foreach (var extra in targets.Skip(1)) extra.Remove();
                foreach (var value in repeated.Skip(1).Reverse()) anchor.AddAfterSelf(new XElement(name, value.Value.Trim()));
                anchor.Value = repeated[0].Value.Trim();
            }
        }
        cpe = cpe with { Edits = cpe.Edits + 1, Request = stored.ToString(SaveOptions.DisableFormatting) };
        await SaveAsync(cpe, ct);
        return call.Ok(Summary(call, cpe));
    }

    // ---- Answers ---------------------------------------------------------------------

    /// <summary>The Detalle answer: the header and every block the stored request carries, in the schema's shape.</summary>
    private XElement Detail(ServiceCall call, StoredCpe cpe)
    {
        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        var answer = fill.Root.Child("respuesta")!;
        var stored = XElement.Parse(cpe.Request);
        foreach (var block in answer.Elements().ToList())
        {
            var name = block.Name.LocalName;
            if (name is "cabecera" or "pdf" or "errores" or "metadata") continue;
            fill.Merge(block, stored.Child(name));
        }
        if (answer.Child("origen")?.Child("cuit") is { } originCuit)
            fill.Set(originCuit, "cuit", stored.Child("origen")?.Text("cuitOrigen") ?? cpe.Issuer.ToString(CultureInfo.InvariantCulture));
        if (answer.Child("origen")?.Child("planta") is { } originPlant && OriginPlant(stored) is { } plant)
            fill.Set(originPlant, "planta", plant);
        Close(fill, answer, cpe, stored);
        return fill.Done();
    }

    /// <summary>The CartaPorteRespuesta every transition and edit answers: the header with the new state, and the PDF.</summary>
    private XElement Summary(ServiceCall call, StoredCpe cpe)
    {
        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        Close(fill, fill.Root.Child("respuesta")!, cpe, XElement.Parse(cpe.Request));
        return fill.Done();
    }

    private void Close(AnswerFill fill, XElement answer, StoredCpe cpe, XElement stored)
    {
        var final = Final.Contains(cpe.State);
        var header = new XElement("cabecera",
            new XElement("tipoCartaPorte", cpe.Type),
            new XElement("sucursal", cpe.Branch),
            new XElement("nroOrden", cpe.OrderNumber),
            new XElement("nroCTG", cpe.Ctg),
            new XElement("fechaEmision", GrainsFormat.DateTime(cpe.IssuedAt)),
            new XElement("estado", cpe.State),
            new XElement("fechaInicioEstado", GrainsFormat.DateTime(cpe.StateSince)),
            final ? null : new XElement("fechaVencimiento", GrainsFormat.DateTime(cpe.StateSince + StateValidity)),
            stored.Text("observaciones") is { Length: > 0 } notes ? new XElement("observaciones", notes) : null,
            cpe.VoidReason is { } reason && cpe.State == "AN" ? new XElement("anulacionMotivo", reason) : null,
            cpe.VoidNotes is { Length: > 0 } voidNotes && cpe.State == "AN" ? new XElement("anulacionObservaciones", voidNotes) : null);
        if (answer.Child("cabecera") is { } template) template.ReplaceWith(header);
        else answer.AddFirst(header);
        fill.Keep(header);
        if (answer.Child("pdf") is { } pdf) fill.Set(pdf, "pdf", GrainsFormat.Pdf($"Carta de Porte Electronica - CTG {cpe.Ctg} - {cpe.State}"));
        if (answer.Child("metadata") is { } metadata)
        {
            Metadata(metadata);
            fill.Keep(metadata);
        }
    }

    private void Metadata(XElement scope)
    {
        scope.Set("servidor", "arcasim");
        scope.Set("fechaHora", GrainsFormat.DateTime(clock.Now));
    }

    // ---- Store -----------------------------------------------------------------------

    private async Task<int> LastAsync(long cuit, int type, int branch, CancellationToken ct) =>
        (await store.GetAsync<CpeLastOrder>(LastCollection, LastKey(cuit, type, branch), ct))?.Number ?? 0;

    private async Task<StoredCpe?> ByCtgAsync(long ctg, CancellationToken ct) =>
        await store.GetAsync<CpeKey>(CtgCollection, ctg.ToString(CultureInfo.InvariantCulture), ct) is { } index
            ? await store.GetAsync<StoredCpe>(Collection, index.Key, ct)
            : null;

    private Task SaveAsync(StoredCpe cpe, CancellationToken ct) => store.PutAsync(Collection, Key(cpe), cpe, ct);

    private static string Key(StoredCpe cpe) => Key(cpe.Issuer, cpe.Type, cpe.Branch, cpe.OrderNumber);

    private static string Key(long cuit, int type, int branch, int order) =>
        $"{cuit}/{type:D3}/{branch:D5}/{order:D8}";

    private static string LastKey(long cuit, int type, int branch) => $"{cuit}/{type:D3}/{branch:D5}";

    // ---- Reading the stored request ----------------------------------------------------

    private static XElement Solicitud(ServiceCall call) => call.Request.Child("solicitud") ?? call.Request;

    /// <summary>Every CUIT the CPE names, plus its issuer: who may read or move it.</summary>
    private static HashSet<long> Names(StoredCpe cpe)
    {
        var names = new HashSet<long> { cpe.Issuer };
        foreach (var e in XElement.Parse(cpe.Request).Descendants().Where(e => !e.HasElements))
            if (e.Name.LocalName.StartsWith("cuit", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(e.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var cuit))
                names.Add(cuit);
        return names;
    }

    private static long? DestinationCuit(XElement stored) =>
        stored.Child("destino")?.Child("cuit") is { } cuit && long.TryParse(cuit.Value.Trim(), CultureInfo.InvariantCulture, out var value) ? value : null;

    private static long? OriginPlant(XElement stored)
    {
        var plant = stored.Child("origen")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "planta")
                    ?? stored.Child("cabecera")?.Child("planta");
        return plant is not null && long.TryParse(plant.Value.Trim(), CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static DateTimeOffset? Departure(XElement stored)
    {
        var text = stored.Child("transporte")?.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("fechaHoraPartida", StringComparison.Ordinal))?.Value.Trim();
        if (text is null || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment)) return null;
        return moment.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(moment, ArgentinaTime.Offset)
            : new DateTimeOffset(moment.ToUniversalTime()).ToArgentina();
    }

    /// <summary>Puts a block in place of the stored one of the same name, or at the end when there is none.</summary>
    private static void Replace(XElement stored, XElement? block)
    {
        if (block is null) return;
        var copy = new XElement(block.Name.LocalName, block.Nodes().Select(n => n is XElement e ? Unqualified(e) : n));
        if (stored.Child(block.Name.LocalName) is { } existing) existing.ReplaceWith(copy);
        else stored.Add(copy);
    }

    private static XElement Add(XElement parent, XElement child)
    {
        parent.Add(child);
        return child;
    }

    private static XElement Unqualified(XElement e) =>
        new(e.Name.LocalName, e.Attributes().Where(a => !a.IsNamespaceDeclaration), e.Nodes().Select(n => n is XElement c ? Unqualified(c) : n));

    private static string Fill(string text, params object[] values)
    {
        for (var i = 0; i < values.Length; i++) text = text.Replace("{" + i + "}", ContractXml.Format(values[i]), StringComparison.Ordinal);
        return text;
    }
}

internal static class XmlChildren
{
    /// <summary>The direct child with that local name: the requests of the Java services leave their children unqualified.</summary>
    public static XElement? Child(this XElement element, string name) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
}
