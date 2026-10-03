namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// The error and observation texts of the three FCE services, literal from
/// docs/arca/servicios/wsfecred-codigos.json, wsfecredagente-codigos.json and
/// wsfecredsca-codigos.json (trailing spaces included, as the manuals print them).
/// </summary>
public static class FceTexts
{
    public static readonly IReadOnlyDictionary<int, string> Fecred = new Dictionary<int, string>
    {
        [1100] = "Ud no puede operar sobre la cuenta corriente indicada",
        [1101] = "Ud no puede realizar esa operación en la cuenta corriente indicada",
        [1102] = "No existe la cuenta corriente indicada",
        [1105] = "No existe el comprobante indicado",
        [1106] = "Debe aguardar hasta la hora 24 del día siguiente de la fecha de puesta a disposición en DFE para operar con esta cuenta corriente.",
        [1107] = "Ha excedido el plazo que le permite realizar esta operación.",
        [1108] = "El estado actual no le permite realizar esta operación.",
        [2001] = "Debe rechazar la Nota de Débito/Crédito individualmente si desea rechazarla",
        [2002] = "Informa aceptar una Nota de Débito/Crédito que fue rechazada",
        [2003] = "Número de jurisdicción de retención inválido o repetido.",
        [2005] = "El importe retenido no coincide con nuestros cálculos a partir del saldo de la factura de crédito",
        [2006] = "Debe indicar una cotización al aceptar",
        [2009] = "El tipo de cambio no podrá ser inferior al 2% ni superior en un 400% del que suministra AFIP como orientativo de acuerdo a la cotización oficial",
        [2010] = "Informa un importe de negativo, lo que no es válido. ",
        [2012] = "La cotización informada coincide con la de la Factura, no debe informar ajuste por tipo de cambio. ",
        [2013] = "Tipo de Ajuste de operación no válido o informado repetido.",
        [2014] = "No corresponde informar CBU cuando la opción de transferencia no es Sistema de Circulación Abierta.",
        [2015] = "La CBU no es válida para la CUIT compradora.",
        [2017] = "No corresponde informar CBU cuando la moneda de la factura es distinta a pesos o dólares",
        [3000] = "Debe indicar una justificación por el rechazo",
        [3001] = "Código de motivo de rechazo inválido o repetido. ",
        [4000] = "El monto informado no cancela totalmente el saldo de la FECRED",
        [4001] = "Falta informar al menos una forma de cancelación",
        [4002] = "Código de forma de cancelación inválido o repetido. ",
        [4003] = "Las formas de cancelación \"Cesión\" y “Locación de Bienes Inmuebles” deben ser usadas para cancelación total y sin informar otro tipo de forma de cancelación entre los usados",
        [5000] = "El rechazo de esta nota no puede realizarse, deja el saldo de la operación inválido (cero o negativo) ",
        [6000] = "La factura ya fue informada al agente de depósito colectivo",
        [6001] = "La factura ya fue informada al agente de depósito colectivo y se encuentra pendiente de confirmación de recepción",
        [6002] = "Los datos de la cuenta en el agente no son válidos para la CUIT representada",
        [6007] = "La factura se encuentra a la espera de la lectura del agente de depósito colectivo",
        [7000] = "Indica la misma opción de transferencia ya elegida",
        [12000] = "Informa una cotización para pesos distinta de 1.",
        [12001] = "Informa una moneda distinta a la de la Factura de Crédito.",
        [12002] = "El saldo aceptado que informa no coincide por el calculado por nuestros registros, verifique sus cuentas.",
        [12003] = "Falta indicar su informe de confirmación de aceptación de al menos una Nota de Débito/Crédito de la cuenta corriente que se encuentra registrada",
        [12004] = "No coincide su informe de confirmación de aceptación de las Nota de Débito/Crédito de la cuenta corriente con el estado en el cual se encuentran registradas",
        [12005] = "Tiene diferencias entre el total y los parciales de los importes de retenciones informadas",
        [12006] = "Al informar un porcentaje de retención distinto a lo normado, debe informar una justificación.",
        [12007] = "La información de retenciones está incompleta. De informar retenciones debe informar importe mayor a 0 y al menos una retención",
        [12008] = "La información de cancelación está incompleta. De informar cancelación debe informar importe mayor a 0, al menos una forma de cancelación y el tipo (si es parcial o total)",
        [12009] = "El código de la retención es inválido (consultarTiposRetenciones).",
        [12010] = "Sólo puede informar ajustes de operación para moneda extranjera.",
        [12011] = "El número de página solicitado debe ser mayor a cero.",
        [12012] = "No debe informar CBU Comprador con Cancelación Total.",
        [12013] = "Si indica que informa CBU debe enviarla y viceversa.",
        [12014] = "Debe indicar si desea informar la CBU comprador al aceptar una factura con opcion de transferencia de Sistema de Circulacion Abierta",
        [12015] = "No debe indicar si desea informar la CBU comprador al aceptar una factura con opcion de transferencia distinta de Sistema de Circulacion Abierta",
        [15000] = "Ud no puede realizar esa operación en el comprobante en el estado en el que se encuentra",
        [32767] = "La búsqueda no ha arrojado resultados con los filtros indicados",
    };

    public static readonly IReadOnlyDictionary<int, string> Agente = new Dictionary<int, string>
    {
        [2002] = "Formato CUIT, CUIL o CDI inválido",
        [2003] = "Rango de Fechas inválido (*)",
        [2004] = "Número de Página inválido",
        [2005] = "ID Cuenta Inválido",
        [2006] = "Código Rechazo Inexistente o Inválido",
        [2009] = "Supera la cantidad de elementos que pueden procesarse (*)",
        [4009] = "La CUIT del Agente no se encuentra en los Registros de AFIP",
        [4012] = "Ocurrio un error intentando dar de Alta la Cuenta Enviada",
        [4014] = "No se encontro una Cuenta Activa asociada a los datos Enviados",
        [4015] = "El Titular con el que intenta efectuar la operación no esta asociado a la cuenta indicada",
        [4019] = "No se encontró ninguna Solicitud de Informe al Agente con los Datos del Comprobante Enviados que se encuentre Pendiente de Confirmar",
        [4020] = "La Solicitud de Informe ya fue Aceptada por el Agente",
        [4021] = "La Solicitud de Informe debe ser Consultada por el Agente antes de ser enviada para Confirmar",
    };

    public static readonly IReadOnlyDictionary<int, string> Sca = new Dictionary<int, string>
    {
        [2002] = "Formato CUIT, CUIL o CDI inválido",
        [2003] = "Rango de Fechas inválido (2)",
        [2004] = "Número de Página inválido",
        [2006] = "Supera la cantidad de elementos que pueden procesarse (2)",
        [4009] = "La CUIT no se encuentra en los Registros de AFIP",
        [4012] = "No se encontró ninguna Factura al SCA con los Datos del Comprobante Enviados que se encuentre Pendiente de Confirmar.",
        [4013] = "La Factura ya fue confirmada.",
        [4014] = "La Factura debe ser consultada antes de ser confirmada.",
    };
}
