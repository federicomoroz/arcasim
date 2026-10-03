namespace ArcaSim.Application.Services.FacturacionE;

/// <summary>
/// WSFEXv1's codes and fixed tables (docs/arca/servicios/wsfexv1.md and
/// wsfexv1-codigos.json). The texts are the manual's descriptions of each
/// validation: the real ErrMsg is only known for 1000. Tables marked as
/// ArcaSim's are not published by ARCA for this service.
/// </summary>
public static class Wsfexv1Tables
{
    public static readonly IReadOnlyDictionary<int, string> Texts = new Dictionary<int, string>
    {
        [1003] = "De informar el campo, el mismo debe tener el sig. formato YYYY-MM-DD",
        [1014] = "Debe ser un valor numerico mayor o igual a 0.",
        [1020] = "Comprobante inexistente",
        [1500] = "Nulo, o comprendido entre N-5 y N+5 siendo N la Fecha de envío. Para el caso de comprobantes de servicios de exportación, la fecha de emisión del comprobante no puede ser posterior al mes en curso según normativa vigente",
        [1510] = "Valor comprendido entre 1 – 99998 y dado de alta como punto de venta “Comprobantes de Exportación - Web Services” (Código FEEWS) Consultar método: FEXGetPARAM_PtoVenta",
        [1520] = "Comprendido entre 1 y 99999999.",
        [1530] = "Los posibles tipo de comprobantes son: 19, 20, 21 19 – Factura de Exportación “E” 20 – Nota de Débito por operaciones con el Exterior 21 – Nota de Crédito por operaciones con el Exterior Ver método FEXGetPARAM_Cbte_Tipo",
        [1535] = "Verifica que el comprobante ingresado corresponde en secuencia al próximo inmediato a autorizar.",
        [1540] = "Deberá ser algunos de los valores permitidos. Valores Permitidos: 1, 2, 4 1= Exportación definitiva de bienes 2= Servicios 4= Otros Ver método FEXGetPARAM_Tipo_Expo",
        [1550] = "Valores posibles: S, N o “vacío” “S” si ya se dispone del despacho de exportación. “N” si aún no se dispone del despacho de exportación. “vacío” si el campo Cbte_Tipo es 20 ó 21 o si Cbte_Tipo es igual a 19 y el campo Tipo_expo es igual a 2 ó 4.",
        [1560] = "Obligatorio. Deberá ser algunos de los valores permitidos. Ver método FEXGetPARAM_DST_pais",
        [1570] = "Deberá ser algunos de los valores permitidos. Ver método FEXGetPARAM_DST_CUIT",
        [1580] = "Se deberá consignar al menos un campo.",
        [1590] = "Deberá ser algunos de los valores permitidos. Ver método FEXGetPARAM_MON. Para el caso de estar autorizando comprobantes de Servicio (Tipo_expo=2) se deben informar solo monedas que tengan cotización al cierre del día hábil anterior (Para este caso ver método FEXGetPARAM_MON_CON_COTIZACION).",
        [1600] = "Deberá ser mayor a 0, hasta 4 enteros y 6 decimales",
        [1601] = "Moneda_ctz deberá ser igual a 1 cuando de indique Moneda_Id = PES",
        [1602] = "El campo Moneda_ctz es obligatorio si no informa el campo CanMisMonExt con el valor S, y debe ser mayor a 0",
        [1603] = "Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacío.",
        [1605] = "Si informa Tipo Cbte 19 y MonID = PES, o Tipo Cbte 20 y 21 el campo CanMisMonExt no debe informarse",
        [1606] = "Campo Cbte_Tipo no se corresponde con alguno de los comprobantes habilitados. Recuerde que los valores son 19, 20 o 21",
        [1607] = "Campo Pto_venta no es valido o no esta dado de alta como punto de venta de 'Comprobantes de Exportación - Web Services'",
        [1610] = "Deberá ser mayor igual a cero e igual a la suma de los campos Item.Pro_total_item Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems ingresados",
        [1620] = "Obligatorio si el tipo de comprobantes es 19",
        [1630] = "Obligatorio. Deberá ser algunos de los valores permitidos. Valores posibles: 1, 2, 3 1: Español 2: Inglés 3: Portugués Ver método FEXGetPARAM_Idiomas",
        [1640] = "Obligatorio en el caso que el tipo de comprobante sea igual a 19 y tipo de operación sea igual a 1 (Productos). Para el resto de los casos es opcional. Para obtener los valores permitidos consultar el método FEXGetPARAM_Incoterms",
        [1641] = "Si se ingresó un valor, el campo Incoterms no puede estar vacío.",
        [1642] = "Longitud máxima es de 20 caracteres.",
        [1650] = "Campo Obligatorio, no podrá estar vacío.",
        [1651] = "Longitud máxima es de 200 caracteres",
        [1660] = "Campo Obligatorio, no podrá estar vacío",
        [1661] = "Longitud máxima es de 300 caracteres",
        [1665] = "Opcionales. El campo Obs la longitud máxima es 1000 y para el campo Obs_comerciales la longitud máxima es 4000",
        [1666] = "La estructura <Items> es inválida, ya sea porque no se ingresó, o bien porque posee 0 ítems, o bien porque supera los 9999 ítems.",
        [1667] = "Si Moneda_Id <> PES, el campo Moneda_ctz no puede ser superior en un 400% ni inferior al 2% de la cotización oficial (ver método FEXGetPARAM_Ctz)",
        [1671] = "Si informa fecha de pago <Cmp><Fecha_pago> debe tener formato váli do YYYYMMDD.",
        [1672] = "Para comprobantes del tipo “19 - Facturas de Exportación” donde el tipo de exportación es “2 – Servicios / 4 - Otros” la fecha de pago <Cmp><Fecha_pago> es obligatoria.",
        [1673] = "Para comprobantes que no son tipo “19 - Facturas de Exportación” la fe cha de pago <Cmp><Fecha_pago> no debe informarse.",
        [1674] = "Para comprobantes del tipo “19 - Facturas de Exportación” donde el tipo de exportación es “2 – Servicios / 4 - Otros”, la fecha de pago debe ser igual o posterior a la fecha de emisión del comprobante.",
        [1680] = "Los posibles tipo de comprobantes son: 19, 20, 21, 88, 89 19 – Factura de Exportación “E” 20 – Nota de Débito por operaciones con el Exterior 21 – Nota de Crédito por operaciones con el Exterior 88 -Remito Electrónico de Tabaco Acondicionado 89 - Resumen de Datos de Exportación de Tabaco Acondicionado 91 - Remito R 993 - Remito Electrónico Harinero - Automotor 994 - Remito Electrónico Harinero – Ferroviario Ver método FEXGetPARAM_Cbte_Tipo",
        [1690] = "De informarse deberá estar comprendido entre 1 – 99998.",
        [1700] = "De informarse podrá tomar los valores desde 1 hasta 999999999",
        [1720] = "- Obligatorio para Tipo_expo = 1 Cmp.Cbte_Tipo = 19 y Cmp.Permiso_existente = “S” - Enviado. El mismo no debe enviarse cuando Cmp.Permiso_existente = “N\" - Obligatorio (tag Permisos), Si envía <Permisos>, <Permiso> es obligatorio.",
        [1730] = "Si se informó el campo Id_permiso deberá informase el campo Dst_merc, como así también si se informó el campo Dst_merc deberá informarse el campo Id_permiso.",
        [1736] = "No es posible informar estos campos con tipo expo = 2 ó 4.",
        [1740] = "Deberá ser un permiso válido, formato 99999AAXX999999A (donde XX podrán ser números o letras). Ver método FEXCHECK_PERMISO. Importante: la combinación Id_permiso y Dst_merc no pueden repetirse dentro del array de <Permisos>.",
        [1749] = "Si el tipo de comprobante asociado (Cbte_tipo) es igual a 19, 20 o 21 y el punto de venta informado es electrónico, el punto de venta deberá corresponder a alguno de los tipos de puntos de venta habilitados para Comprobantes de Exportación. Si se cumple, el tipo y número de comprobante informado deberá estar autorizado.",
        [1750] = "Para los posibles valores consultar método FEXGetPARAM_DST_pais. El destino de la mercadería debe corresponder a un país del permiso de embarque (código despacho) asignado al campo Id_permiso. Se puede validar la existencia de un permiso de embarque / destino de la mercadería mediante el método: FEXCHECK_PERMISO",
        [1754] = "No se puede informar más de 1 comprobante asociado, excepto que los mismos sean 88 u 89.",
        [1755] = "No se pueden informar comprobantes asociados cuando el tipo de comprobante a autorizar es 19 (Factura E), excepto que los mismos sean del tipo 89, 88, 91, 993, 994",
        [1760] = "No podrá superar longitud de 50 caracteres",
        [1770] = "Campo obligatorio. No podrá exceder los 4000 caracteres de longitud.",
        [1775] = "Si Pro_umed es igual a 0, 97 ó 99 deberán informar Item.Pro_qty, Item.Pro_precio_uni y Pro_bonificacion igual a 0 ó no informarse.",
        [1780] = "Es obligatorio si se informa el precio unitario (Pro_precio_uni) o si Pro_umed es distinto a 0, 97 y 99. De ingresarse valor deberá ser mayor a cero.",
        [1790] = "Valores posible Ver Método FEXGetPARAM_UMed",
        [1800] = "Es obligatorio si se informa la cantidad (Pro_qty) o si Pro_umed es distinto a 0, 97 y 99. De ingresarse valor deberá ser mayor o igual a cero.",
        [1810] = "Obligatorio. Si Pro_umed es distinto a 97 y 99, el valor deber ser mayor o igual a 0. Si Pro_umed = 97, sin restricción, el valor puede ser menor, igual o mayor a cero. Si Pro_umed = 99, el valor debe ser menor a 0.",
        [1811] = "Si Pro_umed es distinto de 97, 99 y 0, entonces el valor informado para Pro_bonificacion debe ser mayor o igual a 0",
        [1812] = "Si es mayor a 0 debe ser menor o igual a Pro_precio_uni * Pro_qty.",
        [1813] = "Valor máximo permitido 12 enteros y 6 decimales.",
        [1814] = "Valor máximo permitido 12 enteros y 6 decimales.",
        [1815] = "Si Pro_umed es distinto a 0, 97 ó 99 deberá ser igual a <Pro_precio_uni> * <Pro_qty> - Pro_bonificacion Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01",
        [1816] = "Valor máximo permitido 13 enteros y 2 decimales.",
        [1817] = "Valor máximo permitido 12 enteros y 6 decimales.",
        [1820] = "Si envía Cmps_asoc, Cmp_asoc es obligatorio",
        [2001] = "Si envía opcionales, opcional es obligatorio informarlo.",
        [2002] = "Si envía opcionales, opcional es obligatorio y no debe estar vacío.",
        [2003] = "El campo Id en Opcionales es obligatorio y debe ser alguno de los devueltos por el método FEXGetPARAM_Opcionales.",
        [2004] = "El campo Id en Opcionales es obligatorio y no debe repetirse.",
        [2005] = "Si envía opcionales con el identificador 2401, el campo valor es obligatorio informarlo.",
        [2006] = "Si envía opcionales con el identificador 2401, el campo valor debe contener el documento de exportación. Alfanumérico de 11 caracteres",
        [2007] = "Si envía opcionales con el identificador 2402, el campo valor es obligatorio informarlo.",
        [2008] = "Si envía opcionales con el identificador 2402, el campo valor debe representar al monto FOB. Se espera un numérico de 13 valores enteros y 2 decimales. Separador de decimales usar el punto.",
        [2010] = "Si informa opcionales para el régimen de exportación simplificada es obligatorio informar: - para “19 – Facturas” el documento de exportación simplificada y el monto FOB con valor mayor o igual a 0. - para “20 - Nota de Débito” y “21 – Nota de Crédito” solo informar el monto FOB.",
        [2011] = "Si informa opcionales para el régimen de exportación simplificada, solo se encuentra habilitada para el tipo de exportación 1 – PRODUCTO.",
        [2016] = "Si envía opcionales para el régimen de exportación simplificada, solo se permite moneda en DOLARES, Moneda_Id = DOL.",
        [2031] = "Si el tipo de comprobante asociado Cbte_tipo es distinto a 88 u 89 e informa el cuit del emisor del comprobante asociado (Cbte_cuit), no puede ser distinto al emisor del comprobante que se solicita autorización (<Cuit>).",
        [2047] = "Si esta autorizando una N.D. o N.C. comprobante de Servicio (Tipo=2), el campo Cmp.Cmps_asoc es de caracter obligatorio.",
        [2054] = "El campo Fecha_CTZ es de integración obligatoria y debe tener el siguiente formato: YYYYMMDD",
        [2056] = "Si envía opcionales para el régimen de exportación simplificada no informar <Permios><Permiso>",
        [2057] = "Si el comprobante es 20 - Nota de Débito o 21 - Nota de Crédito e intenta autorizar un comprobante del tipo exportación simplificada no informar el documento de exportación simplificada",
        [2058] = "Si el comprobante es 20 - Nota de Débito o 21 - Nota de Crédito e intenta autorizar un comprobante del tipo exportación simplificada es obligatorio informar el monto FOB.",
    };

    /// <summary>FEXCheck_Permiso's 1810, which shares its number with an item code of FEXAuthorize.</summary>
    public const string PermitCheckText = "En caso de omisión de alguno de los campos de ingreso. En caso de no existir el país registrado en nuestras bases.";

    /// <summary>The manual's voucher types (pág. 17, code 1530).</summary>
    public static readonly IReadOnlyList<(int Id, string Desc)> VoucherTypes =
    [
        (19, "Factura de Exportación \"E\""),
        (20, "Nota de Débito por operaciones con el Exterior"),
        (21, "Nota de Crédito por operaciones con el Exterior"),
    ];

    /// <summary>Types a Cmp_asoc may have (pág. 20, code 1680).</summary>
    public static readonly IReadOnlySet<int> AssociableTypes = new HashSet<int> { 19, 20, 21, 88, 89, 91, 993, 994 };

    /// <summary>Remitos and tobacco summaries: associable to any voucher, without a count limit (pág. 19).</summary>
    public static readonly IReadOnlySet<int> DeliveryNoteTypes = new HashSet<int> { 88, 89, 91, 993, 994 };

    /// <summary>The manual's export types (pág. 14, code 1540).</summary>
    public static readonly IReadOnlyList<(int Id, string Desc)> ExportTypes =
    [
        (1, "Exportación definitiva de bienes"),
        (2, "Servicios"),
        (4, "Otros"),
    ];

    /// <summary>The manual's languages (pág. 14, code 1630), the same as the generic table.</summary>
    public static readonly IReadOnlyList<(int Id, string Desc)> Languages =
    [
        (1, "Español"),
        (2, "Inglés"),
        (3, "Portugués"),
    ];

    /// <summary>The simplified export regime's optional data (pág. 15 and 23).</summary>
    public static readonly IReadOnlyList<(int Id, string Desc)> Optionals =
    [
        (2401, "Documento de exportación simplificada"),
        (2402, "Monto FOB"),
    ];

    /// <summary>ARCA's generic Incoterms table (TABLA INCOTERMS V.0.1 26012011), not this service's own: NO VERIFICADO.</summary>
    public static readonly IReadOnlyList<(string Id, string Desc)> Incoterms =
    [
        ("EXW", "Ex Works"),
        ("FCA", "Free Carrier"),
        ("FAS", "Free Alongside Ship"),
        ("FOB", "Free On Board"),
        ("CFR", "Cost and Freight"),
        ("CIF", "Cost, Insurance and Freight"),
        ("CPT", "Carriage Paid To"),
        ("CIP", "Carriage and Insurance Paid To"),
        ("DAF", "Delivered At Frontier"),
        ("DES", "Delivered Ex Ship"),
        ("DEQ", "Delivered Ex Quay"),
        ("DDU", "Delivered Duty Unpaid"),
        ("DDP", "Delivered Duty Paid"),
        ("DAP", "Delivered At Port"),
    ];

    /// <summary>ARCA's generic units table (TABLA UNIDADES DE MEDIDA V.0 25082010), not this service's own: NO VERIFICADO.</summary>
    public static readonly IReadOnlyList<(int Id, string Desc)> Units =
    [
        (0, "SIN DESCRIPCION"), (1, "KILOGRAMO"), (2, "METROS"), (3, "METRO CUADRADO"), (4, "METRO CUBICO"),
        (5, "LITROS"), (6, "1000 KILOWATT HORA"), (7, "UNIDAD"), (8, "PAR"), (9, "DOCENA"), (10, "QUILATE"),
        (11, "MILLAR"), (12, "MEGA U. INTER. ACT. ANTIB"), (13, "UNIDAD INT. ACT. INMUNG"), (14, "GRAMO"),
        (15, "MILIMETRO"), (16, "MILIMETRO CUBICO"), (17, "KILOMETRO"), (18, "HECTOLITRO"),
        (19, "MEGA UNIDAD INT. ACT. INMUNG"), (20, "CENTIMETRO"), (21, "KILOGRAMO ACTIVO"), (22, "GRAMO ACTIVO"),
        (23, "GRAMO BASE"), (24, "UIACTHOR"), (25, "JGO.PQT. MAZO NAIPES"), (26, "MUIACTHOR"),
        (27, "CENTIMETRO CUBICO"), (28, "UIACTANT"), (29, "TONELADA"), (30, "DECAMETRO CUBICO"),
        (31, "HECTOMETRO CUBICO"), (32, "KILOMETRO CUBICO"), (33, "MICROGRAMO"), (34, "NANOGRAMO"),
        (35, "PICOGRAMO"), (36, "MUIACTANT"), (37, "UIACTIG"), (41, "MILIGRAMO"), (47, "MILILITRO"),
        (48, "CURIE"), (49, "MILICURIE"), (50, "MICROCURIE"), (51, "U.INTER. ACT. HORMONAL"),
        (52, "MEGA U. INTER. ACT. HOR."), (53, "KILOGRAMO BASE"), (54, "GRUESA"), (55, "MUIACTIG"),
        (61, "KILOGRAMO BRUTO"), (62, "PACK"), (63, "HORMA"), (97, "SEÑAS/ANTICIPOS"), (98, "OTRAS UNIDADES"),
        (99, "BONIFICACION"),
    ];

    /// <summary>Units without quantity or price: 0, 97 (advances, any sign) and 99 (discounts, negative) (code 1775).</summary>
    public static bool IsSpecialUnit(int unit) => unit is 0 or 97 or 99;

    /// <summary>
    /// Country CUITs (FEXGetPARAM_DST_CUIT). ARCA's list is not public: these
    /// are the two the manual's examples use, each with the country it goes
    /// with there (Dst_cmp 203 and 220). The pairing is ArcaSim's choice.
    /// </summary>
    public static readonly IReadOnlyList<(long Cuit, string Desc)> CountryCuits =
    [
        (50000000016, "BRASIL"),
        (55000000050, "PANAMA"),
    ];

    public static string Text(int code) => Texts[code];
}
