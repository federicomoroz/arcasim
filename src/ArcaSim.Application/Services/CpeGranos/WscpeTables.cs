using static ArcaSim.Application.Services.CpeGranos.CpeStates;

namespace ArcaSim.Application.Services.CpeGranos;

/// <summary>
/// The codes of cabecera/estado (docs/arca/servicios/wscpe.md, "Códigos en cabecera/estado"). BR (borrador), PA and AP
/// appear in no diagram and ArcaSim never sends them.
/// </summary>
internal static class CpeStates
{
    /// <summary>AC: activa.</summary>
    public const string Active = "AC";

    /// <summary>CF: activa con confirmación de arribo.</summary>
    public const string Arrived = "CF";

    /// <summary>CN: confirmada.</summary>
    public const string Confirmed = "CN";

    /// <summary>CO: activa con contingencia.</summary>
    public const string Contingency = "CO";

    /// <summary>DE: desactivada.</summary>
    public const string Deactivated = "DE";

    /// <summary>RE: rechazada.</summary>
    public const string Rejected = "RE";

    /// <summary>AN: anulada.</summary>
    public const string Voided = "AN";

    /// <summary>DD: descargado en destino.</summary>
    public const string Unloaded = "DD";

    /// <summary>PE: pendiente de emisión (a DG CPE the industry has to issue).</summary>
    public const string AwaitingIssue = "PE";

    /// <summary>IN: inactiva.</summary>
    public const string Inactive = "IN";

    /// <summary>PO: pendiente de aceptación por el origen (a CPE issued at destination).</summary>
    public const string AwaitingOrigin = "PO";
}

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
        new(Automotor, "autorizarCPEAutomotor", "consultarCPEAutomotor", [74, 274], true, "01", Active),
        new(Ferroviaria, "autorizarCPEFerroviaria", "consultarCPEFerroviaria", [75], true, "02", Active),
        new(AutomotorDg, "autorizarCPEAutomotorDG", "consultarCPEAutomotorDG", [284], false, "03", Active),
        new(FerroviariaDg, "autorizarCPEFerroviariaDG", "consultarCPEFerroviariaDG", [285], false, "04", Active),
        new(EmisionDestinoDg, "autorizarCPEEmisionDestinoDG", "consultarCPEEmisionDestinoDG", [286], false, "05", AwaitingOrigin),
    ];

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
        ["confirmarArriboCPE"] = new(Every, [Active, Unloaded], Arrived),
        ["descargadoDestinoCPE"] = new(Every, [Active], Unloaded),
        ["descargadoDestinoCPEEmisionDestinoDG"] = new(Emission, [Active], Unloaded),
        ["rechazoCPE"] = new(Every, [Arrived], Rejected, CpeEffect.Reject),
        ["confirmacionDefinitivaCPEAutomotor"] = new([CpeFamily.Automotor], [Arrived], Confirmed, CpeEffect.FinalConfirmation),
        ["confirmacionDefinitivaCPEFerroviaria"] = new([CpeFamily.Ferroviaria], [Arrived], Confirmed, CpeEffect.FinalConfirmation),
        ["confirmacionDefinitivaCPEAutomotorDG"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], [Arrived], Confirmed, CpeEffect.FinalConfirmation),
        ["confirmacionDefinitivaCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], [Arrived], Confirmed, CpeEffect.FinalConfirmation),
        ["anularCPE"] = new(Every, [Active], Voided, CpeEffect.Void),
        ["anularCPEEmisionDestinoDG"] = new(Emission, [Active], Voided, CpeEffect.Void),
        ["informarContingencia"] = new(Every, [Active], Contingency),
        ["informarContingenciaEmisionDestinoDG"] = new(Emission, [Active], Contingency),
        ["cerrarContingenciaCPE"] = new(Every, [Contingency], Active, CpeEffect.CloseContingency),
        ["cerrarContingenciaCPEEmisionDestinoDG"] = new(Emission, [Contingency], Active, CpeEffect.CloseContingency),
        ["desvioCPEAutomotor"] = new([CpeFamily.Automotor], [Arrived], Active, CpeEffect.Detour),
        ["desvioCPEFerroviaria"] = new([CpeFamily.Ferroviaria], [Arrived], Active, CpeEffect.Detour),
        ["desvioCPEAutomotorDG"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], [Arrived], Active, CpeEffect.Detour),
        ["desvioCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], [Arrived], Active, CpeEffect.Detour),
        ["nuevoDestinoDestinatarioCPEAutomotor"] = new([CpeFamily.Automotor], [Rejected], Active, CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEFerroviaria"] = new([CpeFamily.Ferroviaria], [Rejected], Active, CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEAutomotorDG"] = new([CpeFamily.AutomotorDg], [Rejected], Active, CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], [Rejected], Active, CpeEffect.NewDestination),
        ["nuevoDestinoDestinatarioCPEEmisionDestinoDG"] = new(Emission, [Rejected], Active, CpeEffect.NewDestination),
        ["regresoOrigenCPEAutomotor"] = new([CpeFamily.Automotor], [Rejected], Active, CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEFerroviaria"] = new([CpeFamily.Ferroviaria], [Rejected], Active, CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEAutomotorDG"] = new([CpeFamily.AutomotorDg], [Rejected], Active, CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEFerroviariaDG"] = new([CpeFamily.FerroviariaDg], [Rejected], Active, CpeEffect.ReturnToOrigin),
        ["regresoOrigenCPEEmisionDestinoDG"] = new(Emission, [Rejected], Active, CpeEffect.ReturnToOrigin),
        ["aceptarEmisionDG"] = new([CpeFamily.AutomotorDg, CpeFamily.FerroviariaDg], [AwaitingIssue], Active),
        ["rechazarEmisionDG"] = new([CpeFamily.AutomotorDg, CpeFamily.FerroviariaDg], [AwaitingIssue], Inactive),
        ["aceptarEmisionDestinoDG"] = new(Emission, [AwaitingOrigin], Active),
        ["rechazarEmisionDestinoDG"] = new(Emission, [AwaitingOrigin], Inactive),
    };
}

/// <summary>An edit: the families it applies to and the states it accepts (docs/arca/servicios/wscpe.md, Ediciones).</summary>
internal sealed record CpeEdit(string[] Families, string[] States)
{
    public static readonly IReadOnlyDictionary<string, CpeEdit> ByOperation = new Dictionary<string, CpeEdit>
    {
        ["editarCPEAutomotor"] = new([CpeFamily.Automotor], [Active, Confirmed]),
        ["editarCPEFerroviaria"] = new([CpeFamily.Ferroviaria], [Active, Confirmed]),
        ["editarCPEDGAutomotor"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], [Active]),
        ["editarCPEDGFerroviaria"] = new([CpeFamily.FerroviariaDg], [Active, Confirmed]),
        ["editarCPEConfirmadaAutomotor"] = new([CpeFamily.Automotor], [Confirmed]),
        ["editarCPEConfirmadaFerroviaria"] = new([CpeFamily.Ferroviaria], [Confirmed]),
        ["editarCPEDGConfirmadaAutomotor"] = new([CpeFamily.AutomotorDg, CpeFamily.EmisionDestinoDg], [Confirmed]),
        ["editarCPEDGConfirmadaFerroviaria"] = new([CpeFamily.FerroviariaDg], [Confirmed]),
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
    public static readonly string[] Pending = [Active, Arrived, Contingency, Rejected, Unloaded];

    /// <summary>States a CPE stays in for good: they get no expiry date.</summary>
    public static readonly string[] Final = [Confirmed, Voided, Deactivated, Inactive];

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
