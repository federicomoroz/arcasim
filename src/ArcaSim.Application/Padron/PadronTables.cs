namespace ArcaSim.Application.Padron;

/// <summary>One row of an A100 collection: its id, its description and the attributes the manual names.</summary>
public sealed record ParameterRow(string Id, string Description, IReadOnlyDictionary<string, string> Attributes);

public sealed record ParameterCollection(IReadOnlyList<ParameterRow> Rows);

/// <summary>
/// The A100 collections ArcaSim serves. The manual publishes the collection
/// names and their id attributes, but not their contents (ws_sr_padron_a100.md,
/// "No verificado"). Provinces carry AFIP's codes (0 CABA ... 24 Tierra del
/// Fuego; the manuals' examples confirm 0, 1, 6, 11, 19, 20 and 22). In the
/// other collections the descriptions are texts the padrón manuals show, and
/// the codes are ArcaSim's own: ARCA does not publish them. What matters is
/// that every text A4, A5, A10 and A13 send is in a table.
/// </summary>
public static class PadronTables
{
    private static ParameterRow Row(string idAttribute, string id, string descriptionAttribute, string description, params (string, string)[] more) =>
        new(id, description, new[] { (idAttribute, id), (descriptionAttribute, description) }.Concat(more).ToDictionary(p => p.Item1, p => p.Item2));

    private static readonly (int Code, string Name, string Short)[] Provinces =
    [
        (0, "CIUDAD AUTONOMA BUENOS AIRES", "CF"), (1, "BUENOS AIRES", "BA"), (2, "CATAMARCA", "CA"), (3, "CORDOBA", "CB"),
        (4, "CORRIENTES", "CR"), (5, "ENTRE RIOS", "ER"), (6, "JUJUY", "JY"), (7, "MENDOZA", "MZ"), (8, "LA RIOJA", "LR"),
        (9, "SALTA", "SA"), (10, "SAN JUAN", "SJ"), (11, "SAN LUIS", "SL"), (12, "SANTA FE", "SF"), (13, "SANTIAGO DEL ESTERO", "SE"),
        (14, "TUCUMAN", "TU"), (16, "CHACO", "CH"), (17, "CHUBUT", "CT"), (18, "FORMOSA", "FO"), (19, "MISIONES", "MI"),
        (20, "NEUQUEN", "NQ"), (21, "LA PAMPA", "LP"), (22, "RIO NEGRO", "RN"), (23, "SANTA CRUZ", "SC"), (24, "TIERRA DEL FUEGO", "TF"),
    ];

    public static readonly IReadOnlyDictionary<string, ParameterCollection> Collections = new Dictionary<string, ParameterCollection>(StringComparer.Ordinal)
    {
        ["SUPA.E_PROVINCIA"] = new(Provinces.Select(p =>
            Row("COD_PROVINCIA", p.Code.ToString(), "NOMBRE_PROVINCIA", p.Name, ("CODIGO_SIM_PROVINCIA", p.Short))).ToList()),
        ["SUPA.TIPO_EMPRESA_JURIDICA"] = new(new[]
        {
            ("86", "SOC. ANONIMA"), ("87", "SOC. RESPONSABILIDAD LIMITADA"), ("88", "SOC. COLECTIVA"), ("89", "SOC. EN COMANDITA SIMPLE"),
            ("90", "SOC. EN COMANDITA POR ACCIONES"), ("91", "SOC. DE CAPITAL E INDUSTRIA"), ("92", "SOC. DE HECHO"), ("93", "COOPERATIVA"),
            ("94", "ASOCIACION CIVIL"), ("95", "FUNDACION"), ("96", "SOC. ANONIMA UNIPERSONAL"), ("97", "SOC. POR ACCIONES SIMPLIFICADA"),
        }.Select(f => Row("COD_TIPO_EMPRESA_JURIDICA", f.Item1, "DESC_TIPO_EMPRESA_JURIDICA", f.Item2)).ToList()),
        ["SUPA.TIPO_DATO_ADICIONAL_DOMICILIO"] = new(new[]
        {
            ("1", "PISO"), ("2", "DEPARTAMENTO"), ("3", "OFICINA"), ("4", "LOCAL"), ("5", "BARRIO"), ("6", "MANZANA"), ("7", "TORRE"), ("8", "PARCELA"),
        }.Select(d => Row("COD_TIPO_DATO_ADICIONAL_DOM", d.Item1, "DESC_TIPO_DATO_ADICIONAL_DOM", d.Item2)).ToList()),
        ["SUPA.TIPO_CLAVE_IDENTIFICACION"] = new(new[] { ("CUIT", "CLAVE UNICA DE IDENTIFICACION TRIBUTARIA"), ("CUIL", "CODIGO UNICO DE IDENTIFICACION LABORAL"), ("CDI", "CLAVE DE IDENTIFICACION") }
            .Select(c => Row("COD_CLAVE_IDENTIFICACION", c.Item1, "DESC_CLAVE_IDENTIFICACION", c.Item2)).ToList()),
        ["SUPA.TIPO_DOCUMENTO"] = new(new[] { ("DNI", "DOCUMENTO NACIONAL DE IDENTIDAD"), ("LE", "LIBRETA DE ENROLAMIENTO"), ("LC", "LIBRETA CIVICA"), ("CI", "CEDULA DE IDENTIDAD"), ("PAS", "PASAPORTE") }
            .Select(d => Row("COD_TIPO_DOCUMENTO", d.Item1, "DESC_TIPO_DOCUMENTO", d.Item2)).ToList()),
        ["E_SEXO"] = new(new[] { ("M", "MASCULINO"), ("F", "FEMENINO"), ("X", "NO BINARIO") }
            .Select(s => Row("COD_TIPO_SEXO", s.Item1, "DESC_TIPO_SEXO", s.Item2)).ToList()),
        ["SUPA.TIPO_DOMICILIO"] = new(new[] { ("1", "FISCAL"), ("2", "LEGAL/REAL"), ("3", "COMERCIAL") }
            .Select(d => Row("COD_TIPO_DOMICILIO", d.Item1, "DESC_TIPO_DOMICILIO", d.Item2)).ToList()),
        ["SUPA.TIPO_EMAIL"] = new(new[] { ("1", "PERSONAL"), ("2", "LABORAL"), ("3", "DOMICILIO FISCAL ELECTRONICO") }
            .Select(e => Row("COD_TIPO_EMAIL", e.Item1, "DESC_TIPO_EMAIL", e.Item2)).ToList()),
    };
}
