using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>One row of a reference table: its code, its description and its validity, AAAAMMDD, with 30001231 for "no end" as the manual writes it.</summary>
public sealed record ReferenceRow(string Codigo, string Descripcion, string Desde = "20000101", string Hasta = "30001231");

/// <summary>A reference table: its id, what it holds, the method that lists it and the services that read its codes.</summary>
public sealed record ReferenceTable(string Id, string Description, string Method, string[] Services, IReadOnlyList<ReferenceRow> Rows);

/// <summary>
/// The reference tables of the customs services ArcaSim simulates, as wgesTabRef
/// serves them. A client's codes are checked against them where the manuals say
/// they come from a table: the carpeta codes (DFCOD_DESC), the PEMA states
/// (ETAPEMA_DESC) and the alarms (ESTMON_DESC). DFEST_DESC and ESTCEL_DESC list
/// the states the services write themselves (a legajo's, a precinto's); no rule
/// checks a request against them (ConsultarPrecintos filters by Estado and
/// answers 10121 for one that matches nothing). Only tables whose codes the
/// manuals document are here, with those codes. The texts of DFCOD_DESC come
/// from the wDigDepFiel and wConsDepFiel manuals; the other descriptions are
/// ArcaSim's wording of what the manuals say each state is, not the table's own
/// texts. Which table holds the PEMA device states (ETAPEMA_DESC) is ArcaSim's
/// reading of the name.
/// </summary>
public static class AduanaTables
{
    public static readonly IReadOnlyList<ReferenceTable> All =
    [
        new("DFCOD_DESC", "Codigos de carpeta de Depositario Fiel", "ListaDescripcion", ["wConsDepFiel", "wDigDepFiel"],
        [
            new("000", "Carpeta completa"),
            new("001", "Documentacion adicional"),
            new("002", "Rectificativa B total"),
            new("003", "Rectificativa B parcial"),
            new("004", "Post-libramiento"),
            new("100", "Manifiesto general de carga de importacion (PDF)"),
            new("101", "Manifiesto general de carga de importacion"),
        ]),
        new("DFEST_DESC", "Estados de legajo de Depositario Fiel", "ListaDescripcion", ["wConsDepFiel", "wDigDepFiel"],
        [
            new("ENDO", "Documentacion entregada"),
            new("PSAD", "Recibido por el PSAD"),
            new("DIGI", "Documentacion digitalizada"),
        ]),
        new("ETAPEMA_DESC", "Estados de dispositivos PEMA", "ListaDescripcion", ["WDiaUtiDES"],
        [
            new("ZGSA", "Zona de carga, salida"),
            new("PASA", "En transito"),
            new("ZGAR", "Zona de arribo"),
            new("DISP", "Disponible"),
            new("PFER", "Perdido o fuera de servicio"),
        ]),
        new("ESTCEL_DESC", "Estados de precintos CEMA", "ListaDescripcion", ["wgesprecintosdepfis"],
        [
            new("CIDE", "Colocado en puerta cerrada"),
            new("SOAC", "Solicitud de activacion"),
            new("ACTI", "Activo"),
            new("SODE", "Solicitud de desactivacion"),
            new("DESA", "Desactivado"),
        ]),
        new("ESTMON_DESC", "Estados de monitoreo y alarmas de precintos CEMA", "ListaDescripcion", ["wgesprecintosdepfis"],
        [
            new("MONI", "Monitoreo normal"),
            new("ABIE", "Precinto abierto"),
            new("BTBJ", "Bateria baja"),
        ]),
    ];

    public static ReferenceTable? Find(string id) => All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static bool Has(string table, string code) => Find(table)?.Rows.Any(r => r.Codigo == code) == true;

    /// <summary>When ArcaSim's tables were last loaded: one fixed moment, ArcaSim's choice.</summary>
    public static readonly DateTimeOffset LastUpdate = ArgentinaTime.StartOf(new DateOnly(2026, 1, 2));
}

/// <summary>
/// wgesTabRef, the customs reference tables (docs/arca/servicios/wgesTabRef.md),
/// as the deployed contract answers them: CodError 0 on success, the requested
/// IdReferencia repeated. A table ArcaSim does not have answers 10121 "No hay
/// datos para los criterios ingresados", the DIA's text for an empty query:
/// what ARCA answers is not verified, so that is ArcaSim's choice.
/// </summary>
public sealed class WgesTabRefRules : IServiceBehavior
{
    public string Service => "wgesTabRef";

    public Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => Task.FromResult(Answer(call));

    private static ContractAnswer? Answer(ServiceCall call)
    {
        switch (call.Name)
        {
            case "ListaTablasReferencia":
                return Tables(call, AduanaTables.All);
            case "ListaTablasReferenciaServicio":
                var service = call.Request.Text("IdServicio") ?? "";
                return Tables(call, AduanaTables.All.Where(t => t.Services.Contains(service, StringComparer.OrdinalIgnoreCase)).ToList());
            case "ConsultarFechaUltAct":
                var id = call.Request.Text("IdReferencia") ?? "";
                var known = AduanaTables.Find(id) is not null;
                return call.Done(Result(call, known ? 0 : 10121, known ? null : Dia.NoData, id).Set("Fecha", AduanaTables.LastUpdate));
            case "ListaDescripcion":
            case "ListaVigencias":
            case "ListaDescripcionDecodificacion":
            case "ListaPaisesAduanas":
            case "ListaArancel":
            case "ListaLugaresOperativos":
            case "ListaDatoComplementario":
                return Rows(call);
            case "ListaEmpresas":
            case "DocumentosVigentes":
                var none = Result(call, 10121, Dia.NoData, call.Request.Text("IdReferencia") ?? "");
                none.Descendants().Where(e => e.Name.LocalName is "Empresa" or "VigenciaIndicador").Remove();
                return call.Done(none);
            default:
                return null;
        }
    }

    /// <summary>
    /// The rows of a table in the view each method gives: code and description,
    /// with validity, ISO code, country, aduana or option where the view has
    /// them. ArcaSim has no company tables, so ListaEmpresas finds none.
    /// </summary>
    private static ContractAnswer Rows(ServiceCall call)
    {
        var id = call.Request.Text("IdReferencia") ?? "";
        var table = AduanaTables.Find(id);
        var answer = Result(call, table is null ? 10121 : 0, table is null ? Dia.NoData : null, id);
        var item = call.Name switch
        {
            "ListaVigencias" => "Vigencia",
            "ListaDescripcionDecodificacion" => "DescripcionCodificacion",
            "ListaPaisesAduanas" => "PaisAduana",
            "ListaArancel" => "Opcion",
            "ListaLugaresOperativos" => "LugarOperativo",
            "ListaDatoComplementario" => "DatoComplementario",
            _ => "Descripcion",
        };
        var holder = answer.Descendants().First(e => e.Name.LocalName == item && e.HasElements);
        holder.Parent!.Repeat(item, table?.Rows ?? [], (row, value) =>
        {
            // Direct children only: Descripcion, Opcion and LugarOperativo are also the names of their rows.
            foreach (var (field, text) in new[] { ("Codigo", value.Codigo), ("Descripcion", value.Descripcion), ("VigenciaDesde", value.Desde),
                         ("VigenciaHasta", value.Hasta), ("CodigoIso", value.Codigo) })
                if (row.Child(field) is { } child) child.Value = text;
        });
        return call.Done(answer);
    }

    private static ContractAnswer Tables(ServiceCall call, IReadOnlyList<ReferenceTable> tables)
    {
        var answer = Result(call, 0, null, null);
        answer.Repeat("TablaReferencia", tables, (row, table) => row
            .Set("IdTabRef", table.Id)
            .Set("TabRefDesc", table.Description)
            .Set("WebMethod", table.Method));
        return call.Done(answer);
    }

    private static XElement Result(ServiceCall call, long code, string? info, string? id) =>
        call.Sample().Receipt(code, info, "InfoAdicional").Set("IdReferencia", id);
}
