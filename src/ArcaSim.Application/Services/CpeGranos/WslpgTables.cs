namespace ArcaSim.Application.Services.CpeGranos;

/// <summary>
/// The parameter tables of wslpg with the values the manual (v1.25) prints in
/// its examples, and the texts of the validations the rules send. The manual
/// shows only part of most tables ("..."): ArcaSim serves those rows and no
/// invented ones.
/// </summary>
internal static class WslpgTables
{
    public static readonly IReadOnlyDictionary<string, (string List, (string Code, string Description)[] Rows)> Lists =
        new Dictionary<string, (string, (string, string)[])>
        {
            ["puertoConsultar"] = ("puertos", [("1", "SAN LORENZO/SAN MARTIN"), ("2", "ROSARIO"), ("3", "BAHIA BLANCA"), ("4", "NECOCHEA")]),
            ["tipoCertificadoDepositoConsultar"] = ("tiposCertDep", [("1", "F1116/RT")]),
            ["tipoDeduccionConsultar"] = ("tiposDeduccion", [("CO", "Comision o Gastos Administrativos")]),
            ["tipoRetencionConsultar"] = ("tiposRetencion", [("RI", "I.V.A.")]),
            ["tipoActividadConsultar"] = ("tiposActividad", [("107", "FRACCIONADOR"), ("36", "ACOPIADOR - CONSIGNATARIO")]),
            ["tipoActividadRepresentadoConsultar"] = ("tiposActividad",
                [("93", "MERCADO DE FUTUROS Y OPCIONES O MERCADO A TERMINO"), ("40", "EXPORTADOR"), ("38", "CORREDOR")]),
            ["tipoOperacionXActividadConsultar"] = ("tiposOperacion", [("1", "Compraventa de granos"), ("2", "Consignación de granos")]),
            ["tipoGranoConsultar"] = ("granos", [("1", "LINO")]),
            ["codigoGradoReferenciaConsultar"] = ("gradosRef", [("G1", "Grado 1"), ("G2", "Grado 2"), ("G3", "Grado 3")]),
        };

    /// <summary>codigoGradoEntregadoXTipoGranoConsultar, example of §2.4.17.</summary>
    public static readonly (string Code, string Description, decimal Value)[] DeliveredGrades =
        [("G1", "Grado 1", 1.01m), ("G2", "Grado 2", 1.00m), ("G3", "Grado 3", 0.985m)];

    /// <summary>
    /// Campaigns run from 1/9 to 31/8 and are coded by the two years: 708 is
    /// "2007/2008" (§2.4.14). ArcaSim lists them from that one to the current.
    /// </summary>
    public static IEnumerable<(string Code, string Description)> Campaigns(DateTimeOffset now)
    {
        var current = now.Month >= 9 ? now.Year : now.Year - 1;
        for (var year = 2007; year <= current; year++)
            yield return (((year % 100) * 100 + (year + 1) % 100).ToString(System.Globalization.CultureInfo.InvariantCulture), $"{year}/{year + 1}");
    }

    public static class Codes
    {
        public const string NoData = "No existen datos en las bases de la Administración según los parámetros de búsqueda informados.";
        public const string NotConsecutive = "El nro de orden, no es consecutivo al último utilizado para el punto de emisión indicado.";
        public const string OtherCuit = "La liquidación consultada, corresponde a otra CUIT.";
        public const string VoidOnlyOwn = "Solo se pueden anular liquidaciones emitidas por la CUIT representada.";
        public const string CannotVoid = "La liquidacion no se puede anular.";
        public const string AlreadyVoided = "La liquidacion fue anulada con anterioridad.";
        public const string OriginalOnly = "El COE indicado debe corresponder a una liquidación original. Mediante este método no se pueden consultar liquidaciones de ajuste.";
        public const string NotPrimary = "El coe no pertenece a una liquidación primaria.";
        public const string AdjustmentOnly = "El COE consultado debe corresponder a un ajuste.";
        public const string AdjustedMustExist = "El COE informado <coeAjustado> debe estar asociado a una liquidación previamente autorizada.";
        public const string AdjustedNotAdjustment = "El COE informado <coeAjustado> no puede corresponder a una liquidación de Ajuste, es decir no pudo haber sido generado por alguno de los métodos de ajustes existentes.";
        public const string AdjustedSameCuit = "El COE informado <coeAjustado> debe haber sido liquidado por la misma CUIT que solicita el ajuste <auth><cuit>.";
        public const string BrokerRequired = "Si no es propia produccion y actua corredor, debe informar el cuit del corredor.";
        public const string CertificateInUse = "La certificación seleccionada no es anulable ya que o bien tiene asociado una liquidacion primaria o bien fue utilizado para un retiro o una transferencia.";
        public const string CertificateTransition = "La certificación seleccionada no es anulable ya que la transición de estados no es la correcta.";
        public const string CertificatePermission = "El certificado no se puede anular, ya que el usuario que intenta efectuar la operacion no tiene los permisos adecuados.";
    }
}
