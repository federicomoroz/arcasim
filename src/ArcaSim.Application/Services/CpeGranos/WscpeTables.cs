namespace ArcaSim.Application.Services.CpeGranos;

/// <summary>
/// One family of cartas de porte (docs/arca/servicios/wscpe.md, Operaciones):
/// the operations that create and read it, the CPE types it takes (tabla
/// TipoCPE, pág. 212-213), the first two digits of its CTG and the state the
/// web service leaves it in.
/// </summary>
internal sealed record CpeFamily(string Name, string Authorize, string Consult, int[] Types, bool Grains, string CtgPrefix, string InitialState)
{
    public const string Automotor = "Automotor";
    public const string Ferroviaria = "Ferroviaria";
    public const string AutomotorDg = "AutomotorDG";
    public const string FerroviariaDg = "FerroviariaDG";
    public const string EmisionDestinoDg = "EmisionDestinoDG";

    /// <summary>
    /// Ductos DG is left out: autorizarCPEDuctosDG is discontinued since v2.1.0
    /// and keeps the contract's answer. Ferroviaria and Ferroviaria DG get no
    /// type in the request: 75 and 285 are inferred (NO VERIFICADO). Automotor
    /// DG starts in AC: PE belongs to industry users, a profile ArcaSim does not
    /// model, so aceptar/rechazarEmisionDG answer 2034 (ArcaSim's choice).
    /// </summary>
    public static readonly CpeFamily[] All =
    [
        new(Automotor, "autorizarCPEAutomotor", "consultarCPEAutomotor", [74, 274], true, "01", "AC"),
        new(Ferroviaria, "autorizarCPEFerroviaria", "consultarCPEFerroviaria", [75], true, "02", "AC"),
        new(AutomotorDg, "autorizarCPEAutomotorDG", "consultarCPEAutomotorDG", [284], false, "03", "AC"),
        new(FerroviariaDg, "autorizarCPEFerroviariaDG", "consultarCPEFerroviariaDG", [285], false, "04", "AC"),
        new(EmisionDestinoDg, "autorizarCPEEmisionDestinoDG", "consultarCPEEmisionDestinoDG", [286], false, "05", "PO"),
    ];

    public static CpeFamily Named(string name) => All.First(f => f.Name == name);

    /// <summary>The types the manual lists (tabla TipoCPE); 287 has no operation that creates it.</summary>
    public static readonly int[] KnownTypes = [74, 75, 274, 284, 285, 286, 287];
}

/// <summary>What a transition does besides changing the state.</summary>
internal enum CpeEffect
{
    None,
    Void,
    Reject,
    CloseContingency,
    FinalConfirmation,
    Detour,
    NewDestination,
    ReturnToOrigin,
}

/// <summary>One state transition of the diagrams in section 2.7.2 (pág. 15-17) and table 2.9.</summary>
internal sealed record CpeMove(string[] Families, string[] From, string To, CpeEffect Effect = CpeEffect.None)
{
    private static readonly string[] Every =
        [CpeFamily.Automotor, CpeFamily.Ferroviaria, CpeFamily.AutomotorDg, CpeFamily.FerroviariaDg, CpeFamily.EmisionDestinoDg];

    private static readonly string[] Emission = [CpeFamily.EmisionDestinoDg];

    /// <summary>
    /// The transitions by operation. ArcaSim's choices on what the manual does
    /// not say: confirmarArriboCPE also takes DD to CF ("confirmación de
    /// descarga en destino" names no operation); nuevo destino and regreso a
    /// origen only from RE, as the diagram draws them; aceptarEmisionDestinoDG
    /// takes PO to AC, as its description implies.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, CpeMove> ByOperation = new Dictionary<string, CpeMove>
    {
        ["confirmarArriboCPE"] = new(Every, ["AC", "DD"], "CF"),
        ["descargadoDestinoCPE"] = new(Every, ["AC"], "DD"),
        ["descargadoDestinoCPEEmisionDestinoDG"] = new(Emission, ["AC"], "DD"),
        ["rechazoCPE"] = new(Every, ["CF"], "RE", CpeEffect.Reject),
        ["confirmacionDefinitivaCPEAutomotor"] = new([CpeFamily.Automotor], ["CF"], "CN", CpeEffect.FinalConfirmation),
        ["confirmacionDefinitivaCPEFerroviaria"] = new([CpeFamily.Ferroviaria], ["CF"], "CN", CpeEffect.FinalConfirmation),
        ["confirmacionDefinitivaCPEAutomotorDG"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], ["CF"], "CN", CpeEffect.FinalConfirmation),
        ["confirmacionDefinitivaCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], ["CF"], "CN", CpeEffect.FinalConfirmation),
        ["anularCPE"] = new(Every, ["AC"], "AN", CpeEffect.Void),
        ["anularCPEEmisionDestinoDG"] = new(Emission, ["AC"], "AN", CpeEffect.Void),
        ["informarContingencia"] = new(Every, ["AC"], "CO"),
        ["informarContingenciaEmisionDestinoDG"] = new(Emission, ["AC"], "CO"),
        ["cerrarContingenciaCPE"] = new(Every, ["CO"], "AC", CpeEffect.CloseContingency),
        ["cerrarContingenciaCPEEmisionDestinoDG"] = new(Emission, ["CO"], "AC", CpeEffect.CloseContingency),
        ["desvioCPEAutomotor"] = new([CpeFamily.Automotor], ["CF"], "AC", CpeEffect.Detour),
        ["desvioCPEFerroviaria"] = new([CpeFamily.Ferroviaria], ["CF"], "AC", CpeEffect.Detour),
        ["desvioCPEAutomotorDG"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], ["CF"], "AC", CpeEffect.Detour),
        ["desvioCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], ["CF"], "AC", CpeEffect.Detour),
        ["nuevoDestinoDestinatarioCPEAutomotor"] = new([CpeFamily.Automotor], ["RE"], "AC", CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEFerroviaria"] = new([CpeFamily.Ferroviaria], ["RE"], "AC", CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEAutomotorDG"] = new([CpeFamily.AutomotorDg], ["RE"], "AC", CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], ["RE"], "AC", CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEEmisionDestinoDG"] = new(Emission, ["RE"], "AC", CpeEffect.NewDestination),
        ["regresoOrigenCPEAutomotor"] = new([CpeFamily.Automotor], ["RE"], "AC", CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEFerroviaria"] = new([CpeFamily.Ferroviaria], ["RE"], "AC", CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEAutomotorDG"] = new([CpeFamily.AutomotorDg], ["RE"], "AC", CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], ["RE"], "AC", CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEEmisionDestinoDG"] = new(Emission, ["RE"], "AC", CpeEffect.ReturnToOrigin),
        ["aceptarEmisionDG"] = new([CpeFamily.AutomotorDg, CpeFamily.FerroviariaDg], ["PE"], "AC"),
        ["rechazarEmisionDG"] = new([CpeFamily.AutomotorDg, CpeFamily.FerroviariaDg], ["PE"], "IN"),
        ["aceptarEmisionDestinoDG"] = new(Emission, ["PO"], "AC"),
        ["rechazarEmisionDestinoDG"] = new(Emission, ["PO"], "IN"),
    };
}

/// <summary>An edit: the families it applies to and the states it accepts (docs/arca/servicios/wscpe.md, Ediciones).</summary>
internal sealed record CpeEdit(string[] Families, string[] States)
{
    public static readonly IReadOnlyDictionary<string, CpeEdit> ByOperation = new Dictionary<string, CpeEdit>
    {
        ["editarCPEAutomotor"] = new([CpeFamily.Automotor], ["AC", "CN"]),
        ["editarCPEFerroviaria"] = new([CpeFamily.Ferroviaria], ["AC", "CN"]),
        ["editarCPEDGAutomotor"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], ["AC"]),
        ["editarCPEDGFerroviaria"] = new([CpeFamily.FerroviariaDg], ["AC", "CN"]),
        ["editarCPEConfirmadaAutomotor"] = new([CpeFamily.Automotor], ["CN"]),
        ["editarCPEConfirmadaFerroviaria"] = new([CpeFamily.Ferroviaria], ["CN"]),
        ["editarCPEDGConfirmadaAutomotor"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], ["CN"]),
        ["editarCPEDGConfirmadaFerroviaria"] = new([CpeFamily.FerroviariaDg], ["CN"]),
    };
}

/// <summary>Tables and texts of the wscpe manual (v2.2.1).</summary>
internal static class WscpeTables
{
    /// <summary>consultarProvincias, as the example of pág. 25-27 lists them, in its order.</summary>
    public static readonly (int Code, string Name)[] Provinces =
    [
        (1, "BUENOS AIRES"), (2, "CATAMARCA"), (3, "CORDOBA"), (4, "CORRIENTES"), (5, "ENTRE RIOS"), (0, "CAP.FEDERAL"),
        (6, "JUJUY"), (7, "MENDOZA"), (8, "LA RIOJA"), (9, "SALTA"), (10, "SAN JUAN"), (11, "SAN LUIS"), (12, "SANTA FE"),
        (13, "SGO.DEL ESTERO"), (14, "TUCUMAN"), (16, "CHACO"), (17, "CHUBUT"), (18, "FORMOSA"), (19, "MISIONES"),
        (20, "NEUQUEN"), (21, "LA PAMPA"), (22, "RIO NEGRO"), (23, "SANTA CRUZ"), (24, "TIER.DEL FUEGO"),
    ];

    /// <summary>States that still wait for something (the "pendientes de resolución").</summary>
    public static readonly string[] Pending = ["AC", "CF", "CO", "RE", "DD"];

    /// <summary>States a CPE stays in for good: they get no expiry date.</summary>
    public static readonly string[] Final = ["CN", "AN", "DE", "IN"];

    /// <summary>
    /// How long a state lasts before it blocks the plant or the CUIT. The
    /// manual gives no number (Plazos y vigencias); 15 days is ArcaSim's choice.
    /// </summary>
    public static readonly TimeSpan StateValidity = TimeSpan.FromDays(15);

    /// <summary>Desvíos a CPE may take: the manual names the limit (2130) but not its value; 2 is ArcaSim's choice.</summary>
    public const int MaxDetours = 2;

    /// <summary>"Nuevos destino (Máximo 2)", from the DG diagram.</summary>
    public const int MaxNewDestinations = 2;

    /// <summary>Edits a CPE may take: the manual names the limit (2238) but not its value; 3 is ArcaSim's choice.</summary>
    public const int MaxEdits = 3;

    /// <summary>Anexo 4.1, the codes the rules send, with the manual's texts.</summary>
    public static class Codes
    {
        public const string NoRequests = "No existen solicitudes para los parámetros indicados.";
        public const string InvalidQueryType = "Debe ingresar un tipo de carta de porte válido para la consulta.";
        public const string Required = "El campo '{0}' es requerido.";
        public const string WrongOrderNumber = "Número de orden incorrecto para el tipo de carta de porte y sucursal ingresados.";
        public const string WrongBranch = "El número de sucursal no es válido.";
        public const string NotFound = "No existe una CPE con los parámetros indicados";
        public const string InvalidState = "Estado no válido";
        public const string InvalidTransition = "La transición desde el estado {0} hacia el estado {1} es inválida.";
        public const string NotYourApplicant = "Usted no puede realizar operaciones para la CUIT solicitante {0}.";
        public const string NotYourRequest = "Usted no puede realizar operaciones para la solicitud indicada.";
        public const string NotForThisType = "Operación no disponible para el tipo de CPE actual.";
        public const string VoidTooLate = "El plazo para la Anulación de la actual Carta de Porte Electrónica fue superado.";
        public const string TooManyDetours = "Se superó la cantidad de desvíos permitidos.";
        public const string DateRange = "El rango de fechas debe ser como máximo de 3 días.";
        public const string InvalidVoidReason = "El motivo anulación es inválido.";
        public const string InvalidRejectReason = "El motivo de rechazo es inválido.";
        public const string TooManyNewDestinations = "Se superó la cantidad de nuevos destinatarios permitidos permitidos.";
        public const string TooManyEdits = "La Carta de Porte no puede modificarse porque alcanzó el tope máximo de modificaciones realizadas.";
        public const string AlreadyIssued = "La Carta de Porte ya fue emitida con el Nro de CTG {0}.";
    }
}
