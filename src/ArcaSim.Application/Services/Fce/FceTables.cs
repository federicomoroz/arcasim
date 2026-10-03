namespace ArcaSim.Application.Services.Fce;

public sealed record FceWithholdingType(short Code, string Jurisdiction, decimal Rate);

public sealed record FceCancellationForm(short Code, string Description, bool TotalOnly);

/// <summary>
/// The parameter tables of the three FCE services. ARCA's manuals publish none
/// of their values (wsfecred.md "Tablas y datos", wsfecredagente.md "Tablas y
/// datos"), so every code and percentage here is ArcaSim's own. Only some
/// names come from the manual: the cancellation forms "Cesión", "Locación de
/// Bienes Inmuebles" and "Otros medios de pago habilitados por el BCRA" (the
/// first two only for a total cancellation, code 4003), and the adjustment for
/// the exchange rate (2012).
/// </summary>
public static class FceTables
{
    public static readonly IReadOnlyList<FceCodeText> RejectionReasons =
    [
        new(1, "Mercadería no recibida o servicio no prestado"),
        new(2, "Mercadería o servicio no conforme"),
        new(3, "Precio o importe distinto del acordado"),
        new(4, "Comprobante con errores formales"),
        new(5, "Otros motivos"),
    ];

    public static readonly IReadOnlyList<FceCancellationForm> CancellationForms =
    [
        new(1, "Transferencia bancaria", false),
        new(2, "Cheque", false),
        new(3, "Efectivo", false),
        new(4, "Compensación", false),
        new(5, "Cesión", true),
        new(6, "Locación de Bienes Inmuebles", true),
        new(7, "Otros medios de pago habilitados por el BCRA", false),
    ];

    /// <summary>Code 1 is the exchange-rate adjustment that 2012 refers to.</summary>
    public static readonly IReadOnlyList<FceCodeText> Adjustments =
    [
        new(ExchangeRateAdjustment, "Ajuste por tipo de cambio"),
        new(2, "Ajuste por diferencias de redondeo"),
    ];

    public const short ExchangeRateAdjustment = 1;

    public static readonly IReadOnlyList<FceWithholdingType> Withholdings =
    [
        new(1, "NACIONAL - IMPUESTO A LAS GANANCIAS", 2.00m),
        new(2, "NACIONAL - IMPUESTO AL VALOR AGREGADO", 3.00m),
        new(901, "CIUDAD AUTONOMA DE BUENOS AIRES - INGRESOS BRUTOS", 2.00m),
        new(902, "BUENOS AIRES - INGRESOS BRUTOS", 1.75m),
    ];

    /// <summary>The agent's reasons for rejecting a reported invoice (obtenerMotivosRechazo).</summary>
    public static readonly IReadOnlyList<FceCodeText> AgentRejectionReasons =
    [
        new(1, "La cuenta comitente indicada no corresponde al titular"),
        new(2, "La cuenta comitente indicada se encuentra inhabilitada"),
        new(3, "Los datos de la factura de crédito no pueden ser procesados"),
    ];

    /// <summary>
    /// The FCE threshold from 14/04/2026 (Res. 1/2026, docs/arca/normativa.md
    /// §8.1). ARCA's figure depends on the receiver's main activity; ArcaSim
    /// uses this one for everyone.
    /// </summary>
    public const decimal MinimumAmount = 5_549_862.00m;
}
