namespace ArcaSim.Application.Services.Remitos;

/// <summary>
/// The parameter tables the remito specs document. Codes are ARCA's; where a
/// spec gives only the meaning of a code, the description is ArcaSim's
/// wording of it, as each list says.
/// </summary>
public static class RemitoTables
{
    /// <summary>wsremharina manual 2.5.19.</summary>
    public static readonly (object Code, string Text)[] FlourVoucherTypes =
    [
        (993, "Remito Electrónico Harinero Automotor"),
        (994, "Remito Electrónico Harinero Ferroviario"),
    ];

    /// <summary>wsremharina manual 2.5.20.3.2, in the example's order.</summary>
    public static readonly (object Code, string Text)[] States =
    [
        ("EMI", "Emitido"),
        ("VEN", "Vencido"),
        ("PAD", "Pendiente de Autorizar por Depositario"),
        ("EXO", "Exportado"),
        ("PAT", "Pendiente de Autorizar por Titular"),
        ("EXP", "Exportado Parcialmente"),
        ("ANS", "Anulado sin emisión"),
        ("NFI", "No finalizado"),
        ("NAC", "No Aceptado"),
        ("ANUR", "Anulado por Redestino"),
        ("ACP", "Aceptado Parcialmente"),
        ("BOR", "Borrador"),
        ("EXT", "Exportado Totalmente"),
        ("PEM", "Pendiente de Emitir"),
        ("ACE", "Aceptado"),
        ("ANU", "Anulado"),
        ("DEN", "Denegado"),
        ("EXR", "Exportación Rechazada"),
    ];

    /// <summary>
    /// wsremharina manual 2.5.21.3.2: codes 9 to 14. The spec gives each
    /// code's meaning, not its text; the descriptions are ArcaSim's wording.
    /// </summary>
    public static readonly (object Code, string Text)[] FlourContingencies =
    [
        (9, "Anulación del remito sin pérdida de mercadería"),
        (10, "Pérdida parcial de mercadería sin anulación del remito"),
        (11, "Pérdida parcial de mercadería con anulación del remito"),
        (12, "Pérdida total de mercadería que ocasiona anulación del remito"),
        (13, "Demoras en traslado"),
        (14, "Corrección de pérdida informada, mercadería recuperada"),
    ];

    /// <summary>The flour contingencies that cancel the remito, and the one that extends its validity a day.</summary>
    public static readonly int[] CancellingContingencies = [9, 11, 12];

    public const int DelayContingency = 13;

    /// <summary>wsremharina manual 4 (anexo): units 01 to 05.</summary>
    public static readonly (object Code, string Text)[] SaleUnits =
    [
        (1, "Kg"),
        (2, "Tonelada"),
        (3, "Unidad"),
        (4, "Litro"),
        (5, "m³"),
    ];

    /// <summary>wsremcarne manual 2.5.10: the only type, 995. The description is ArcaSim's wording.</summary>
    public static readonly (object Code, string Text)[] MeatVoucherTypes = [(995, "Remito Electrónico Cárnico")];

    /// <summary>wsremazucar manual 17 and 18: 997 and 998 "(Exp)". The descriptions are ArcaSim's wording.</summary>
    public static readonly (object Code, string Text)[] SugarVoucherTypes =
    [
        (997, "Remito Electrónico Azúcar"),
        (998, "Remito Electrónico Azúcar (Exp)"),
    ];

    /// <summary>
    /// wsremazucar's EstadoRemitoType. CON and NCO are documented (manual 14
    /// and 23); the rest take harina's descriptions, and PAR's is ArcaSim's
    /// guess (NO VERIFICADO).
    /// </summary>
    public static readonly (object Code, string Text)[] SugarStates =
    [
        ("BOR", "Borrador"),
        ("PAT", "Pendiente de Autorizar por Titular"),
        ("PAR", "Pendiente de Autorizar por Receptor"),
        ("EXT", "Exportado Totalmente"),
        ("PEM", "Pendiente de Emitir"),
        ("EMI", "Emitido"),
        ("ANU", "Anulado"),
        ("DEN", "Denegado"),
        ("ACE", "Aceptado"),
        ("ACP", "Aceptado Parcialmente"),
        ("NAC", "No Aceptado"),
        ("CON", "Convalidado"),
        ("NCO", "No Convalidado"),
    ];

    /// <summary>wsremazucar manual 10. The order of the codes is inferred from the text (NO VERIFICADO).</summary>
    public static readonly (object Code, string Text)[] SugarHolderTypes =
    [
        (1, "Propia"),
        (2, "Producto por contrato de maquila"),
        (3, "Tercero por servicio de fasón"),
    ];
}
