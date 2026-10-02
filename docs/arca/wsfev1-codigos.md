# WSFEv1 — Códigos de error, observación y evento

Complemento de [wsfev1.md](wsfev1.md). Contiene **todas** las tablas de validación del manual oficial, extraídas
programáticamente del PDF (PyMuPDF, tabla por tabla, uniendo filas partidas entre páginas) y revisadas a mano.

- **Fuente principal:** "Manuales para el desarrollador – Facturación – RG 4291 – Proyecto FE **v4.7**", ARCA-SDG SIT,
  revisión del 1 de septiembre de 2026, 203 páginas.
  URL: <https://www.afip.gob.ar/ws/documentacion/manuales/manual-desarrollador-ARCA-COMPG.pdf>
  (enlazado como "Manual para el desarrollador V. 4.7" desde <https://www.afip.gob.ar/ws/documentacion/ws-factura-electronica.asp>).
- **Versión siguiente (homologación externa):** "Proyecto FE **v4.8**", revisión del 1 de diciembre de 2026.
  URL: <https://www.afip.gob.ar/fe/ayuda/documentos/wsfev1-RG-4291.pdf> (enlazada desde
  <https://www.afip.gob.ar/ws/documentacion/homologacion-externa.asp>, "adecuaciones introducidas por la RG N° 5.616/2024").
  Se comparó v4.7 contra v4.8 palabra por palabra y tabla por tabla: **el único cambio de contenido es la entrada 4.8 del
  historial** ("Será obligatorio el campo Condición Frente al IVA del receptor [...] los códigos de error 10245 para CAE y
  825 para CAEA quedaran en desuso"). Todas las tablas son idénticas.
- "Pág. PDF N" = número de página física del PDF (1..203). El índice interno del manual usa otra numeración y está desfasado.

## 1. Cómo leer estas tablas

| Concepto | Dónde aparece en la respuesta | Efecto | Fuente |
|---|---|---|---|
| **Error** (`<Errors><Err><Code/><Msg/></Err></Errors>`) | En el `...Result` del método, al mismo nivel que `FeCabResp`/`ResultGet` | Rechazo de toda la solicitud (problemas del emisor, de autenticación, de cabecera) | Manual "Tratamiento de errores en el WS" (pág. PDF 20-21) y "Operatoria ante errores" (pág. PDF 76-77) |
| **Validación excluyente** de detalle | En `FECAEDetResponse/Observaciones/Obs` **o** en `Errors` (ver nota) | El comprobante queda `Resultado=R`, sin CAE | Manual pág. PDF 25 ("No supere alguna de las validaciones excluyentes, el comprobante no es aprobado") |
| **Validación no excluyente** (observación) | En `FECAEDetResponse/Observaciones/Obs` | El comprobante queda `Resultado=A` **con** CAE y con observaciones | Manual pág. PDF 25 |
| **Evento** (`<Events><Evt>`) | En el `...Result` | Informativo (avisos de la administración); no cambia el resultado | Manual pág. PDF 21-22. El manual **no** publica ningún código de evento. |

**Nota importante sobre dónde va un código excluyente de detalle (verificado en respuestas reales, no en el manual):**
el mismo código 10016 aparece en ambas ubicaciones según el caso:

- Como `Err` a nivel `FECAESolicitarResult`, con `FeDetResp/FECAEDetResponse/Resultado=R` y `<CAE />`, cuando el número no
  es el próximo o la fecha es anterior a la del último comprobante. Mensaje real:
  `El numero o fecha del comprobante no se corresponde con el proximo a autorizar. Consultar metodo FECompUltimoAutorizado.`
- Como `Obs` dentro del `FECAEDetResponse`, cuando falla el rango de `CbteFch`. Mensajes reales:
  `Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N+5 siendo N la fecha de envio del pedido  de autorizacion para 1 - Productos`
  y `Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N siendo N la fecha de envio del pedido de autorizacion para Facturas de Credito del tipo Nota de Debito o Nota de Credito`
  (los dobles espacios son literales).

Fuente: respuestas grabadas del servicio de homologación (julio 2021) en los tests del cliente open source PyAfipWs,
<https://github.com/PyAr/pyafipws/tree/main/tests/cassettes/test_wsfev1> (`test_main_prueba.yaml`, `test_main_prueba_usados.yaml`,
`test_main_prueba_fce.yaml`, `test_reproceso_*.yaml`). Usadas sólo para confirmar comportamiento ambiguo.

**El texto de la columna "Condición" es la descripción de la validación del manual, NO el `Msg` que devuelve el servicio.**
Los `Msg` reales son distintos y, por lo general, sin tildes (ver §5). Un simulador fiel debe usar el `Code` exacto y, para
el `Msg`, preferir los literales observados en §5; donde no haya literal observado, el texto del manual es la mejor
aproximación disponible y se debe marcar como tal.

## 2. Errores de infraestructura y de autenticación (todos los métodos)

Fuente: manual v4.7, "Tratamiento de errores en el WS", pág. PDF 21.

| Código | Causa (texto del manual) |
|---:|---|
| 500 | Error interno de aplicación. |
| 501 | Error interno de base de datos. |
| 502 | Error interno de base de datos - Autorizador CAE / Régimen CAEA – Transacción Activa |
| 600 | No se corresponden token y firma. Usuario no autorizado a realizar esta operación |
| 601 | CUIT representada no incluida en token. |
| 602 | No existen datos en nuestros registros. |

Mensajes reales observados para estos códigos:

| Código | `Msg` literal | Situación | Fuente |
|---:|---|---|---|
| 500 | `Campo Auth no fue ingresado o esta mal formado.` | Request sin `<Auth>` (o con elementos sin el namespace `http://ar.gov.afip.dif.FEV1/`, que el deserializador ignora) | Llamada en vivo a homologación, 2026-10-01 |
| 600 | `ValidacionDeToken: No valido token. Excepcion: CargarStringBase64Token: Excepción: CargarNodos: Error al cargar token XML. Excepcion: Data at the root level is invalid. Line 1, position 1.` | `Token` que no es base64 de un XML | En vivo, 2026-10-01 |
| 600 | `ValidacionDeToken: Parametro nulo o vacio (token)` | `Auth` sin `Token` | En vivo, 2026-10-01 |
| 600 | `ValidacionDeToken: No validaron las fechas del token. GenTime=1, ExpTime=2, NowUTC=1790905655` | Token XML con `gen_time`/`exp_time` vencidos (se valida **antes** que la firma) | En vivo, 2026-10-01 |
| 600 | `ValidacionDeToken: Error al verificar hash: ` | Token con fechas vigentes pero `Sign` que no corresponde | En vivo, 2026-10-01 |
| 600 | `ValidacionDeToken: No apareció CUIT en lista de relaciones: 23000000000` | `Auth/Cuit` que no está en las relaciones del token (el manual lo muestra con código 600, no 601) | Manual, ejemplo de FECAEARegInformativo, pág. PDF 170 |
| 600 | `ValidacionDeToken: No valido Id Sistema: wsfe(Id Sistema de token es: wsfex)` | Token emitido por WSAA para otro servicio | Cliente afipjs, <https://github.com/egnuez/afipjs/blob/master/doc/wsfev1.md> (secundario) |
| 600 | `ValidacionDeToken: No validaron las fechas del token GenTime, ExpTime, NowUTC: 1554661566 (4/7/2019 6:25:36 PM), 1554704826 (4/8/2019 6:27:06 AM), 4/20/2019 2:23:35 AM` | Token vencido (formato de 2019; difiere del observado en 2026) | afipjs (secundario) |
| 602 | `No existen datos en nuestros registros para los parametros ingresados.` | `FECompConsultar` de un comprobante inexistente; `FECAEAConsultar` sin CAEA para ese período/orden | Cassettes PyAfipWs 2021 |

Con error 600/500 la respuesta **no** incluye `FeCabResp` en `FECAESolicitar`; en `FECompUltimoAutorizado` sí incluye
`<PtoVta>0</PtoVta><CbteTipo>0</CbteTipo><CbteNro>0</CbteNro>` (enteros obligatorios del esquema) y en `FECompTotXRequest`
`<RegXReq>0</RegXReq>`. Verificado en vivo el 2026-10-01.

## 3. Índice de rangos de códigos

| Rango | Método | Notas |
|---|---|---|
| 500–602 | Todos | Infraestructura / token (§2) |
| 10000–10284 | FECAESolicitar | Excluyentes y no excluyentes (§4.1) |
| 700–829, 900–1009, 1100–1106, 1300, 1401–1528, 10000–10003, 16000–16014 | FECAEARegInformativo | (§4.2) |
| 15000–15018 | FECAEASolicitar | (§4.3) |
| 15004–15005 | FECAEAConsultar | (§4.4) |
| 1200–1209 | FECAEASinMovimientoInformar | (§4.5) |
| 10100–10105 | FECAEASinMovimientoConsultar | (§4.6) |
| 11000–11002 | FECompUltimoAutorizado | (§4.7) |
| 10104, 10200–10202 | FECompConsultar | (§4.8) |
| 12000–12002 | FEParamGetCotizacion | (§4.9) |
| 10244 | FEParamGetCondicionIvaReceptor | (§4.10) |

Los métodos FEDummy, FECompTotXRequest, FEParamGetTiposCbte, FEParamGetTiposConcepto, FEParamGetTiposDoc,
FEParamGetTiposIva, FEParamGetTiposMonedas, FEParamGetTiposOpcional, FEParamGetTiposTributos, FEParamGetTiposPaises,
FEParamGetPtosVenta y FEParamGetActividades **no tienen tabla de validaciones propia** en el manual; sólo aplican los
códigos de §2.

Códigos que el manual cita en el historial pero que **no** figuran en ninguna tabla vigente (baja o reemplazo):
1428, 15013 (baja v2.8), 1430, 15100 (baja v2.11), 15007 (baja v2.11), 10179, 10182 (baja v2.14), 10185 (baja v2.16),
10087, 10093 (baja v2.19; 10093 sigue en la tabla con la leyenda "Se encuentra dado de baja mediante redmine RM57235").

Huecos en la numeración 10000–10284 que **no** aparecen en ninguna tabla de FECAESolicitar (calculado sobre la extracción):
10009, 10034, 10050, 10072–10074, 10087, 10100–10109 (10100–10102, 10104 y 10105 son de otros métodos), 10179, 10182,
10185, 10191, 10200–10202 (FECompConsultar), 10233, 10244 (FEParamGetCondicionIvaReceptor), 10250, 10252–10269.
**No inventar** respuestas para esos códigos.

## 4. Tablas completas del manual

Las tablas siguen el orden y la agrupación del manual. Los `<` `>` del manual se conservan (escapados para Markdown).
Las erratas del texto original se conservan ("ACTIVAD", "consular", "activida", "obligatgorio", etc.). Sólo se corrigieron
artefactos de la extracción del PDF (guiones de corte de línea, un número de página incrustado en 1415). Notas entre
corchetes y en negrita son agregados de este documento, no del manual.

### 4.1. FECAESolicitar


#### FECAESolicitar · Controles aplicados al objeto &lt;Auth&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 39.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10000 | &lt;Cuit&gt; | Verificación de datos registrales, Inscripción en el régimen, autorización de emisión de comprobantes, domicilio fiscal. Etc. Los mensajes posibles son<br>01 “LA CUIT INFORMADA NO CORRESPONDE A UN RESPONSABLE INSCRIPTO EN EL IMPUESTO”<br>02 “LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR COMPROBANTES ELECTRONICOS ORIGINALES O EL PERIODO DE INICIO AUTORIZADO ES POSTERIOR AL DE LA GENERACION DE LA SOLICITUD”<br>03 “LA CUIT INFORMADA REGISTRA INCONVENIENTES CON EL DOMICILIO FISCAL”<br>04 “LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR COMPROBANTES CLASE “A” O FACTURA DE CREDITO, (Esta validación no aplica para comprobantes tipo C)”<br>05 “EL CUIT INFORMADO COMO EMISOR NO SE ENCUENTRA REGISTRADO DE FORMA ACTIVA EN LAS BASES DE LA ADMINISTRACIÓN.”<br>06 “DEBE POSEER AL MENOS UNA ACTIVAD ACTIVA.” (Esta validación no aplica para comprobantes tipo C”<br>07 “NO AUTORIZADO A EMITIR COMPROBANTES – LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR COMPROBANTES SEGÚN RG 3411” (Esta validación solo aplica para comprobante 49 – Bien Usado”)<br>08 “NO AUTORIZADO A EMITIR COMPROBANTES – LA CUIT INFORMADA NO CORRESPONDE A UN EXENTO EN IVA.<br>09 “LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR COMPROBANTES CLASE A CON LEYENDA ‘OPERACIÓN SUJETA A RETENCIÓN’”<br>10 “LA CUIT INFORMADA NO SE ENCUENTRA REGISTRADA COMO PYME SEGÚN EL REGIMEN FCE”<br>11 “LA CUIT INFORMADA NO TIENE ACTIVO EL DOMICILIO FISCAL ELECTRONICO” |

#### FECAESolicitar · Controles aplicados al objeto &lt;FeCabReq&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 40.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10001 | &lt;CantReg&gt; | Cantidad de registros de detalle del comprobante o lote de comprobantes de ingreso &lt;CantReg&gt; debe estar comprendido entre 1 y 9998. |
| 10002 | &lt;CantReg&gt; | La cantidad de registros del detalle del comprobante o lote de comprobantes de ingreso debe ser igual a lo informado en cabecera del comprobante o lote de comprobantes de ingreso &lt;CantReg&gt; |
| 10003 | Cantidad de registros incluidos | La cantidad de registros en detalle debe ser menor igual al valor permitido. Consulte método FECompTotXRequest para obtener cantidad máxima de registros por cada requerimiento. Para comprobantes del tipo MiPyMEs (FCE), la cantidad habilitada es 1 comprobante por request |
| 10004 | &lt;PtoVta&gt; | Campo &lt;PtoVta&gt; debe estar comprendido entre 1 y 99998. |
| 10005 | &lt;PtoVta&gt; | El punto de venta informado debe estar dado de alta y ser del tipo RECE. |
| 10006 | &lt;CbteTipo&gt; | Campo CbteTipo debe ser un valor numérico mayor a 0. |
| 10007 | &lt;CbteTipo&gt; | Campo CbteTipo sea: - 01, 02, 03, 04, 05 ,34, 39, 60, 63, 201, 202, 203 para los clase A - 06, 07, 08, 09, 10, 35, 40,64, 61, 206, 207, 208 para los clase B. - 11, 12, 13, 15, 211, 212, 213 para los clase C. - 51, 52, 53, 54 para los clase “A con leyenda operación sujeta a retención”. - 49 para los Bienes Usados. Consultar método FEParamGetTiposCbte. |

#### FECAESolicitar · Controles aplicados al objeto &lt;FeDetReq&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 41–73.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10008 | &lt;CbteDesde&gt; | Campo &lt;CbteDesde&gt; se encuentre entre 1 y 99999999. |
| 10010 | &lt;CbteHasta&gt; | Campo &lt;CbteHasta&gt; se encuentre entre 1 y 99999999. |
| 10011 | &lt;CbteHasta&gt; | Campo &lt;CbteHasta&gt; sea mayor o igual a &lt;CbteDesde&gt; para comprobantes tipo B. Para comprobantes tipo C &lt;CbteHasta&gt; debe ser igual a &lt;CbteDesde&gt;. |
| 10012 | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | Para comprobantes clase “A”, “C”, “A con leyenda operación sujeta a retención”, “49” – Bienes Usados y comprobantes MiPyMEs (FCE) el campo CbteDesde debe ser igual al campo CbteHasta |
| 10013 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; | Para comprobantes clase “A” y “A con leyenda operación sujeta a retención” el campo DocTipo tenga valor 80 (CUIT) |
| 10014 | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | Para comprobantes clase B y CbteHasta distinto a CbteDesde el resultado de la operación ImpTotal / (CbteHasta –CbteDesde +1) &lt; monto en pesos resultante según RG4444. |
| 10015 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | Para comprobantes tipo B en pedidos múltiples (CbteDesde distinto a CbteHasta) y el resultado de la operación ImpTotal / (CbteHasta – CbteDesde + 1 ) &lt; monto en pesos resultante según RG4444. el campo DocTipo deberá ser igual a 99, el campo DocNro deberá ser cero (0). Para comprobantes tipo B en pedidos individuales (CbteDesde igual a CbteHasta) y el resultado de la operación ImpTotal / (CbteHasta – CbteDesde + 1 ) &lt; monto en pesos resultante según RG4444 si el campo DocTipo es igual a 99, el campo DocNro deberá ser cero. Para comprobantes tipo B individuales (CbteDesde igual a CbteHasta), si el campo DocTipo es 80, 86 u 87, deberá verificarse que el número consignado se encuentre en los padrones de ARCA. Si DocTipo es 80 y DocNro es 23000000000 (No Categorizado) esta validación no se tendrá en cuenta. Si el campo DocTipo es distinto de 80, 86 u 87, deberá verificarse que se ingrese uno de los valores devueltos por el método FEParamGetTiposDoc y que se informe el campo DocNro. Para pedidos individuales (CbteDesde igual a CbteHasta) tipo B con montos superiores a monto en pesos resultante según RG4444 el campo DocTipo deberá ser igual a algunos de los valores devueltos por el método FEParamGetTiposDoc excepto 99 y deberá informar el campo DocNro. Para comprobantes tipo 49 – Bienes Usados, DocTipo deberá ser igual a algunos de los valores devueltos por el método FEParamGetTiposDoc excepto el 99 y deberá informar el campo DocNro. Para comprobantes tipo 49 – Bienes Usados, si DocTipo es 80, 86 u 87, deberá verificarse que el número consignado se encuentra en los padrones de arca. Para comprobantes MiPyMEs (FCE) el documento del receptor debe ser 80 CUIT. |
| 10016 | &lt;CbteDesde&gt; / &lt;CbteFch&gt; | El número de comprobante informado &lt;CbteDesde&gt; debe ser mayor en 1 al último informado para igual punto de venta y tipo de comprobante. Consultar método FECompUltimoAutorizado El campo &lt;CbteFch&gt; podrá ser: - Nulo o comprendido en el rango N-5 y N+5 siendo N la fecha de envío del pedido de autorización, para Concepto= 01 Productos. La misma no podrá exceder el mes de presentación. - Para Concepto 02, 03 el campo CbteFch puede ser nulo o comprendido en el rango N-10 y N+10 siendo N la fecha de envío del pedido de autorización. - Deberá ser mayor o igual al del ultimo comprobante emitido para ese tipo y punto de venta - Para comprobantes MiPyMEs (FCE) estar comprendido en el rango N-5 y N+1 siendo N la fecha de envío del pedido de autorización. De tratarse de notas de débito o crédito, la fecha del comprobante puede ser hasta N-5. |
| 10018 | &lt;AlicIVA&gt; | Si &lt;ImpIVA&gt; es igual a 0 los objetos &lt;IVA&gt; y &lt;AlicIva&gt; solo deben informarse con ImpIVA = 3 (iva 0) Si &lt;ImpIVA&gt; es mayor a 0 el objeto &lt;IVA&gt; y &lt;AlicIva&gt; son obligatorios. El objeto &lt;AlicIva&gt; es obligatorio y no debe ser nulo si ingresa &lt;IVA&gt; No aplica para comprobantes tipo C. |
| 10019 | &lt;AlicIVA&gt;&lt;id&gt; | El campo Id en AlicIVA es obligatorio informarlo. Si el tipo de comprobante es 2, 3, 7, 8, 52 o 53 informarlo es opcional. Siempre que se informe Id, debe ser un valor devuelto por el método FEParamGetTiposIva. No aplica para comprobantes tipo C. |
| 10020 | &lt;AlicIVA&gt;&lt;BaseImp&gt; | El campo BaseImp en AlicIVA es obligatorio y debe ser mayor a 0 cero. Excepto para comprobantes 2, 3, 7, 8, 52 o 53 que puede ser cero o no ser informado. No aplica para comprobantes tipo C. |
| 10021 | &lt;AlicIVA&gt;&lt;Importe&gt; | El campo Importe en AlicIVA es obligatorio, mayor o igual 0 cero. Excepto para comprobantes 2, 3, 7, 8, 52 o 53 que puede ser cero o no ser informado. No aplica para comprobantes tipo C. |
| 10022 | &lt;AlicIVA&gt;&lt;id&gt; | El campo Id en AlicIVA no debe repetirse. Deberá totalizarse por alícuota. No aplica para comprobantes tipo C. |
| 10023 | &lt;ImpIVA&gt; / &lt;AlicIVA&gt;&lt;importe&gt; | La suma de los campos &lt;importe&gt; en &lt;IVA&gt; debe ser igual al valor ingresado en ImpIVA. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de alícuotas de IVA ingresadas * No aplica para comprobantes tipo C. |
| 10024 | &lt;Tributo&gt; | Si ImpTrib es mayor a 0 el objeto &lt;Tributos&gt; y &lt;Tributo&gt; son obligatorios. El objeto &lt;Tributo&gt; es obligatorio y no deber ser nulo si se incluye el objeto &lt;Tributos&gt; Si impTrib es igual a cero el objeto &lt;Tributos&gt; y &lt;Tributo&gt; no deben enviarse. |
| 10025 | &lt;Tributo&gt;&lt;id&gt; | El campo &lt;Id&gt; en &lt;Tributo&gt; es obligatorio y debe ser alguno de los devueltos por el método FEParamGetTiposTributos |
| 10026 | &lt;Tributo&gt;&lt;BaseImp&gt; | El campo &lt;BaseImp&gt; en &lt;Tributo&gt; es obligatorio y debe ser mayor o igual a 0 cero |
| 10027 | &lt;Tributo&gt;&lt;Alic&gt; | El campo &lt;Alic&gt; en &lt;Tributo&gt; es obligatorio , mayor o igual 0 cero |
| 10028 | &lt;Tributo&gt;&lt;importe&gt; | El campo &lt;Importe&gt; en &lt;Tributo&gt; es obligatorio , mayor o igual 0 cero |
| 10029 | &lt;ImpTrib&gt; / &lt;Tributo&gt;&lt;importe&gt; | La suma de los importes en &lt;Tributo&gt; debe ser igual al valor ingresado en &lt;ImpTrib&gt; Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de tributos * |
| 10030 | &lt;concepto&gt; | El campo &lt;Concepto&gt; es obligatorio y debe corresponder con algún valor devuelto por el método FEParamGetTiposConcepto 1 Productos 2 Servicios 3 Productos y Servicios |
| 10031 | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | El campo “fecha desde del servicio a facturar” &lt;FchServDesde&gt; es obligatorio si se informa “fecha hasta del servicio a facturar” &lt;FchServHasta&gt; y/o “fecha de vencimiento para el pago” &lt;FchVtoPago&gt;. |
| 10032 | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; | El campo “fecha desde del servicio a facturar” &lt;FchServDesde&gt; no puede ser posterior al campo “fecha hasta del servicio a facturar” &lt;FchServHasta&gt;. |
| 10033 | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | El campo “fecha hasta del servicio a facturar” &lt;FchServHasta&gt; es obligatorio si se informa “fecha desde del servicio a facturar” &lt;FchServDesde&gt; y/o “fecha de vencimiento para el pago” &lt;FchVtoPago&gt;. |
| 10035 | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | El campo “fecha de vencimiento para el pago” &lt;FchVtoPago&gt; es obligatorio si se informa “fecha desde del servicio a facturar” &lt;FchServDesde&gt; y/o “fecha hasta del servicio a facturar” &lt;FchServHasta&gt;. |
| 10036 | &lt;FchVtoPago&gt; | El campo “fecha de vencimiento para el pago” &lt;FchVtoPago&gt; no puede ser anterior a la fecha del comprobante. |
| 10037 | &lt;MonId&gt; | El campo &lt;MonId&gt; es obligatorio y debe corresponder a algún valor devuelto por el método FEParamGetTiposMonedas |
| 10038 | &lt;MonCotiz&gt; | El campo &lt;MonCotiz&gt; es obligatorio si no informa el campo CanMisMonExt = S, y de informarse debe ser mayor a 0. Si se indica que el pago del comprobante se realiza en la misma moneda extranjera que la factura, la cotización de la moneda provista debe coincidir exactamente con la registrada en las bases de ARCA para el día hábil anterior a la fecha de emisión del comprobante, si esta es anterior a la fecha actual, o bien con la registrada para el día hábil anterior a la fecha actual, si la fecha de emisión es posterior a esta. En caso contrario, se puede omitir el campo de Cotización de Moneda. |
| 10039 | &lt;MonId&gt; / &lt;MonCotiz&gt; | El campo &lt;MonCotiz&gt; es obligatorio , e igual a 1 cuando se trate de &lt;MonId&gt;=PES |
| 10040 | &lt;CbtesAsoc&gt; / &lt;CbteTipo&gt; | De enviarse el tag &lt;CbtesAsoc&gt;, entonces el campo “código de tipo de comprobante” &lt;CbteTipo&gt; a autorizar tiene que ser 01, 02, 03, 06, 07, 08, 12, 13, 51, 52, 53, 201, 206 o 211 Para 02 y 03 pueden asociarse los tipos de comprobante 01, 02, 03, 04, 05, 34, 39, 60, 63, 88 y 991 Para 07 y 08 pueden asociarse 06, 07, 08, 09, 10, 35, 40, 61, 64, 88 y 991 Para 12 o 13 pueden asociarse 11, 12, 13 y 15. Para 52 o 53 pueden asociarse 51, 52, 53, 54, 88 y 991 Para 01,06 y 51 pueden asociarse 88 y 991 Para comprobantes MiPyMEs (FCE) 201, 206 o 211 puede asociarse los comprobantes (91, 990, 991, 993, 994, 995). Para comprobantes MiPyMEs (FCE) A 202 y 203, puede asociar 201,202 o 203. Para comprobantes MiPyMEs (FCE) B 207, 208 puede asociar 206, 207, 208. Para comprobantes MiPyMEs (FCE) C 212, 213 puede asociarse 211, 212, 213. |
| 10042 | &lt;Tirbuto&gt;&lt;Id&gt; / &lt;Tirbuto&gt;&lt;Desc&gt; | El campo &lt;Desc&gt; en Tributo es obligatorio cuando se informe &lt;Id&gt; = 99. Para comprobantes MiPyMEs (FCE) siempre es obligatoria la descripción |
| 10043 | &lt;ImpTotConc&gt; | El campo “Importe neto no gravado” &lt;ImpTotConc&gt;. No puede ser menor a cero (0). Para comprobantes tipo C debe ser igual a cero (0). Para comprobantes tipo 49 – Bienes usados, si el emisor es MONOTRIBUTISTA, este campo corresponde al importe del subtotal de la operación |
| 10044 | &lt;ImpOpEx&gt; | El campo “importe exento” &lt;ImpOpEx&gt;. No puede ser menor a cero (0). Para comprobantes tipo C debe ser igual a cero (0). Para comprobantes tipo 49 – Bienes usados, si se encuentra inscripto en MONOTRIBUTO no debe informarse o debe ser igual a cero (0). |
| 10045 | &lt;ImpNeto&gt; | El campo “Importe neto gravado” &lt;ImpNeto&gt;. No puede ser menor a cero (0). Para comprobantes tipo C este campo corresponde al Importe del Sub Total. Para comprobantes tipo 49 – Bienes usados, si se encuentra inscripto en MONOTRIBUTO no debe informarse o debe ser igual a cero (0). |
| 10046 | &lt;ImpTrib&gt; | El campo “Importe de tributos” &lt;ImpTrib&gt;. No puede ser menor a cero (0). |
| 10047 | &lt;ImpIVA&gt; | El campo “Importe de IVA” &lt;ImpIVA&gt;. No puede ser menor a cero (0). Para comprobantes tipo C debe ser igual a cero (0). Para comprobantes tipo 49 – Bienes usados, si se encuentra inscripto en MONOTRIBUTO no debe informarse o debe ser igual a cero (0). |
| 10048 | &lt;ImpTotConc&gt; / &lt;ImpOpEx&gt; / &lt;ImpNeto&gt; / &lt;ImpTrib&gt; / &lt;ImpIVA&gt; / &lt;ImpTotal&gt; | El campo “Importe Total” &lt;ImpTotal&gt;, debe ser igual a la suma de ImpTotConc + ImpNeto + ImpOpEx + ImpTrib + ImpIVA Para comprobantes tipo C, el campo “Importe Total” &lt;ImpTotal&gt;, debe ser igual a la suma de ImpNeto + ImpTrib. Para comprobantes tipo 49 – Bienes Usados, si se encuentra inscripto en MONOTRIBUTO el campo “Importe Total” &lt;ImpTotal&gt;, debe ser igual a la suma de ImpTotConc + ImpTrib. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 |
| 10049 | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | Los campos &lt;FchServDesde&gt;, &lt;FchServHasta&gt;, &lt;FchVtoPago&gt;, es obligatorio cuando el campo &lt;Concepto&gt; es igual a 2 o 3. Si se informa deberá tener el siguiente formato yyyymmdd. |
| 10051 | &lt;AlicIVA&gt; | Los importes informados en AlicIVA se deben corresponder según el tipo de iva seleccionado. Para comprobantes tipo 2, 3, 7, 8, 52 y 53 no se tiene en cuenta esta validación. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 No aplica para comprobantes tipo C |
| 10052 | &lt;Opcionales&gt; | Si envía &lt;Opcionales&gt;, &lt;Opcional&gt; es obligatorio. |
| 10053 | &lt;Opcional&gt; | El campo &lt;Id&gt; en &lt;Opcionales&gt; es obligatorio y debe ser alguno de los devueltos por el método FEParamGetTiposOpcional. |
| 10054 | &lt;Opcional&gt; | El campo &lt;Id&gt; en &lt;Opcionales&gt; es obligatorio y no debe repetirse. Solo pueden repetirse los identificadores 1801 y 1802 informados para la RG 4004-E, y los identificadores 2901 y 2902 para la RG 4291-Seguros de Caución. |
| 10055 | &lt;Opcional&gt; | El campo &lt;Valor&gt; en Opcionales es obligatorio |
| 10056 | Importes en general | Que se informen los mismos con la precisión indicada. |
| 10057 | &lt;CbteAsoc&gt;&lt;Tipo&gt; | De enviarse el tag CbteAsoc debe enviarse Tipo &gt; a 0 |
| 10058 | &lt;CbteAsoc&gt;&lt;PtoVta&gt; | De enviarse el tag CbteAsoc debe enviarse PtoVta &gt; a 0 y &lt; a 99999 |
| 10059 | &lt;CbteAsoc&gt;&lt;Nro&gt; | De enviarse el tag CbteAsoc debe enviarse Nro &gt; a 0 y &lt; a 99999999 |
| 10060 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | De enviarse el tag CbteAsoc, los comprobantes no deben repetirse. |
| 10061 | &lt;ImpNeto&gt; / &lt;AlicIVA&gt;&lt;BaseImp&gt; | La suma de los campos &lt;BaseImp&gt; en &lt;AlicIva&gt; debe ser igual al valor ingresado en ImpNeto. Esta validación no deberá ser tenida en cuenta, cuando el &lt;CbteTipo&gt; sea 02, 03 ,07, 08, para comprobantes tipo C (11, 12, 13, 15) y para Comprobantes tipo “A con leyenda operación sujeta a retención” (52, 53) Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de alícuotas de IVA ingresadas * |
| 10062 | &lt;CbtesAsoc&gt;&lt;CbteAsoc&gt; | Si envía &lt;CbtesAsoc&gt;, &lt;CbteAsoc&gt; es obligatorio. |
| 10064 | &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si selecciona Id = 2 el valor ingresado debe ser un numérico de 8 (ocho) dígitos mayor o igual a 0 (cero). |
| 10065 | &lt;ImpTotal&gt; | El campo “Importe Total” &lt;ImpTotal&gt;. No puede ser menor a cero (0). |
| 10066 | &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si Id = 2 y el comprobante corresponde a una actividad alcanzada por el beneficio de Promoción Industrial en el campo &lt;Valor&gt; se deberá informar el número identificador del proyecto (el mismo deberá corresponder a la CUIT emisora del comprobante), si no corresponde a una actividad alcanzada por el beneficio el campo &lt;Valor&gt; deberá ser 0 (cero). El Id = 2 solo podrá informarse cuando &lt;CbteTipo&gt; es igual a 1, 2, 3, 6, 7, 8. |
| 10067 | &lt;ImpTrib&gt; &lt;DocTipo&gt;&lt;DocNro&gt; | Falta informar en la sección Otros Tributos el tributo con ID = 13 – Percepción de IVA No Categorizado según la RG 2126/2006. Si &lt;CbteTipo&gt; es igual a 6, 7 u 8, &lt;DocTipo&gt; es 80 (CUIT), &lt;DocNro&gt; es 23000000000 (No Categorizado), la suma de ImpNeto + ImpIVA es mayor a 0 (cero), y el campo ImpTrib = 0, debe informar el ID “13 – Percepción de IVA No Categorizado“ en el array de tributos con un importe mayor a 0 (cero) |
| 10068 | &lt;Opcionales&gt;&lt;CbteTipo&gt; | El array &lt;Opcionales&gt; no es obligatorio. Solo puede informarse si &lt;CbteTipo&gt; es 1, 2, 3, 4, 6, 7, 8, 9, 11,12, 13, 15, 49, 51, 52, 53, 54, 63, 64, 201, 206, 211, 203, 208, 213, 202, 207, 212. |
| 10069 | &lt;DocNro&gt; | El N° de documento del receptor del comprobante no puede ser igual al del emisor. |
| 10070 | &lt;ImpNeto&gt;/ &lt;Iva&gt; | Si el importe neto es mayor a cero, es obligatorio informar el array de iva. |
| 10071 | &lt;Iva&gt; | Si el tipo de comprobante es C, el array de IVA no debe informarse. |
| 10075 | &lt;CbteTipo&gt;/&lt;AlicIVA&gt; | Si el comprobante informado es tipo 49 – Bienes Usados, el emisor del comprobante se encuentra inscripto en el MONOTRIBUTO. El objeto &lt;IVA&gt; y &lt;AlicIva&gt; no deben informarse. |
| 10076 | &lt;Opcionales&gt;&lt;CbteTipo&gt;/&lt;DocTipo&gt; | Si el comprobante informado es tipo 49 – Bienes Usados, es obligatorio informar opcionales. Ver método FEParamGetTiposOpcional() |
| 10077 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 91 el valor ingresado no puede ser un blanco y debe ser un alfanumérico de 100 caracteres como máximo. |
| 10078 | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | Si el comprobante es del tipo 49 – Bienes Usados es obligatorio informar el Nombre y Apellido mediante el ID = 91. |
| 10079 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 92 el valor ingresado debe ser un valor numérico de 3 posiciones. |
| 10080 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 92, el contenido del campo &lt;Valor&gt; debe corresponder a un código de país valido. Ver método FEParamGetTiposPaises |
| 10081 | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | Si el comprobante es del tipo 49 – Bienes Usados, los valores posibles para el id son 91, 92, 93. |
| 10082 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;CbteTipo&gt; | Si en el campo TipoDoc se informa 30, 91 o 94 se deberá informar el id 92 con el código del país del vendedor. Consultar Método FEParamGetTiposPaises. Si TIPODOC es distinto de 30, 91 o 94 no debe informarse el id 92. |
| 10083 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 93, el valor ingresado no puede ser blanco y debe ser alfanumérico de 250 caracteres como máximo |
| 10084 | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | Si el comprobante es del tipo 49 – Bienes Usados es obligatorio informar el Domicilio del receptor/vendedor el ID = 93. |
| 10085 | &lt;concepto&gt; | Para comprobantes tipo 49 – Bienes usados, solo informar 1 – Productos |
| 10086 | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | Si el comprobante es del tipo A (1, 2, 3, 4, 5, 34, 39, 60, 63) e intenta informar datos opcionales según Resolución General 3668, los valores posibles para los identificadores son 5, 61, 62, 7. |
| 10088 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 5, el valor ingresado no puede ser blanco y debe ser alfanumérico de 2 caracteres. |
| 10089 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 5, el contenido del campo &lt;Valor&gt; debe corresponder a un código de EXCEPCION válido comprendido por alguno de los sig: 01 – Locador / Prestador del mismo 02 – Congresos / Eventos 03 – Operación contemplada en RG 74 04 – Bienes de Cambio 05 – Ropa de trabajo 06 – Intermediario |
| 10090 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 61, el valor ingresado no puede ser blanco y debe ser numérico de 2 caracteres. |
| 10091 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 61, el contenido del campo &lt;Valor&gt; debe corresponder a un código que represente el tipo de documento del firmante. Ver método FEParamGetTiposDoc. |
| 10092 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 62, el valor ingresado no puede ser blanco y debe ser numérico de 11 caracteres como máximo. |
| 10093 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Se encuentra dado de baja mediante redmine RM57235 **[dado de baja]** |
| 10094 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 7, el valor ingresado no puede ser blanco y debe ser numérico de 2 caracteres. |
| 10095 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 7, el contenido del campo &lt;Valor&gt; debe corresponder a un código de carácter firmante válido comprendido por alguno de los sig: 01 – Titular 02 – Director / Presidente 03 – Apoderado 04 – Empleado |
| 10096 | &lt;PtoVta&gt; / &lt;CbteTipo&gt; | Para comprobantes tipo C, si el contribuyente se encuentra registrado en las bases del organismo como exento, el punto de venta a utilizar al momento de autorizar el comprobante debe ser del tipo “COMPROBANTES – EXENTO EN IVA – WEB SERVICES”. |
| 10097 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 10 (RG 3.368 Establecimientos de educación pública de gestión privada), el valor ingresado no puede ser blanco y debe ser un numerico de 1 carácter: 0 – Actividades no comprendidas 1 – Actividades comprendidas |
| 10098 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 1011 (RG 3.368 Establecimientos de educación pública de gestión privada), el valor ingresado no puede ser blanco y debe corresponder al tipo de documento del titular del pago. Ver método FEParamGetTiposDoc. |
| 10099 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 1012 (RG 3.368 Establecimientos de educación pública de gestión privada), el valor ingresado no puede ser blanco y debe corresponder al n° de documento del titular del pago. Numérico de 11 caracteres como máximo para tipo de documento 80, 86, 87, 96 o alfanumérico de 20 como máximo para el resto de los tipos de documentos. |
| 10110 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 11 (RG 2.820 Operaciones económicas vinculadas con bienes inmuebles), el valor ingresado no puede ser blanco y debe ser un numérico de 1 carácter: 0 – Actividades no comprendidas 1 – Actividades comprendidas |
| 10111 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 12 (RG 3.687 Locación temporaria de inmuebles con fines turísticos), el valor ingresado no puede ser blanco y debe ser un numérico de 1 carácter: 0 – Actividades no comprendidas 1 – Actividades comprendidas |
| 10112 | &lt;Opcionales&gt;&lt;Id&gt; | Si intenta informar datos opcionales según Resolución General: RG 3.368 Establecimientos de educación pública de gestión privada (identificador 10) RG 2.820 Operaciones económicas vinculadas con bienes inmuebles (identificador 11) RG 3.687 Locación temporaria de inmuebles con fines turísticos (identificador 12). RG 2.863 Representantes de Modelos (identificador 13). RG 2.863 Agencias de publicidad (identificador 14). RG 2.863 Personas físicas que desarrollen actividad de modelaje (identificador 15). RG 4004-E Alquiler de inmuebles con destino casa habitación (identificador 17, en caso de ser necesario informar titular o cotitular, el identificador que acompaña al 17 es el 1801 y 1802). Recordar que en un mismo comprobante solo puede informar identificadores opcionales para solo 1 resolución por comprobante. |
| 10113 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 10 (RG 3.368 Establecimientos de educación pública de gestión privada) con valor “1 – Actividades comprendidas” Informar 1011 – Tipo de Documento 1012 – N° de documento Si informa id = 10 (RG 3.368 Establecimientos de educación pública de gestión privada) con valor “0 – Actividades No comprendidas” No informar 1011 – Tipo de Documento 1012 – N° de documento |
| 10114 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 1011 o 1012 (RG 3.368 Establecimientos de educación pública de gestión privada) es obligatorio informar el identificador que representa si se encuentra comprendida (id = 10, valor = 1) |
| 10115 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 10 (RG 3.368 Establecimientos de educación pública de gestión privada) con valor “1 – Actividades comprendidas” e informa ID = 1011 (Tipo de Documento) con un valor que se corresponde al 80, 86, 87, 96 (CUIT, CUIL, CDI, DNI respectivamente), deberá verificarse que el número consignado en el ID = 1012 (n° de documento del titular del pago), se encuentra en los padrones de arca. |
| 10116 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 13 (RG 2.863 Representantes de Modelos), el valor ingresado no puede ser blanco y debe ser un numérico de 1 carácter: 0 - Actividades no comprendidas 1 - Actividades comprendidas |
| 10117 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 14 (RG 2.863 Agencias de publicidad), el valor ingresado no puede ser blanco y debe ser un numérico de 1 carácter: 0 - Actividades no comprendidas 1 - Actividades comprendidas |
| 10118 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 15 (RG 2.863 Personas físicas que desarrollen actividad de modelaje), el valor ingresado no puede ser blanco y debe ser un numérico de 1 carácter: 0 - Actividades no comprendidas 1 - Actividades comprendidas |
| 10119 | &lt;MonId&gt;/&lt;MonCotiz&gt; | Si la moneda es &lt;&gt; PES, el tipo de cambio no podrá ser inferior al 2% ni superior en un 400% del que suministra arca como orientativo de acuerdo a la cotización oficial. Para poder obtener la cotización ver Metodo FEParamGetCotizacion. |
| 10120 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | Si informa comprobantes asociados, y sus códigos son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, los mismos deben encontrarse registrados. |
| 10121 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | Si informa comprobantes asociados, y sus códigos son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, los mismos deben encontrarse confirmados. |
| 10122 | &lt;DocTipo&gt; / &lt;DocNro&gt; &lt;CbteAsoc&gt;&lt;Cuit&gt; | Si informa comprobantes asociados y sus códigos son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, el receptor del comprobante a autorizar debe ser igual al receptor del comprobante asociado. |
| 10123 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si el comprobante es del tipo B o C e intenta informar datos opcionales según Resolución General 4004-E, los valores posibles para los identificadores son 17, 1801, 1802. |
| 10124 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 17 (RG 4004-E Locación de inmuebles destino "casa-habitación"), el valor ingresado no puede ser blanco y debe ser un numérico de 1 carácter: 1 (uno) = facturación a través de intermediario 2 (dos) = facturación directa |
| 10125 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación"), el valor ingresado no puede ser blanco y debe corresponder al CUIT del propietario/locador. Numérico de 11 caracteres. |
| 10126 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación"), verificar que el número consignado se encuentra en los padrones de arca. |
| 10127 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 1802 (RG 4004-E Locación de inmuebles destino "casa-habitación") el valor ingresado no puede ser un blanco y debe ser un alfanumérico de 100 caracteres como máximo que representa el Nombre y Apellido propietario/locador. |
| 10128 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación") con un CUIT propietario/locador no repetirlo dentro de la lista de propietarios/locadores. |
| 10129 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id 17 con valor 1 (Intermediario) (RG 4004-E Locación de inmuebles destino "casa- habitación"), deben informarse obligatoriamente los identificadores 1801 y 1802. Si informa id 17 con valor 2 (Directo) (RG 4004-E Locación de inmuebles destino "casa-habitación"), pueden no informarse los identificadores 1801, 1802. Solo informarlos cuando hay otro/s propietarios/locadores. |
| 10130 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id 1801 y id 1802 (RG 4004-E Locación de inmuebles destino "casa-habitación"), la cantidad de opcionales con id 1801 y 1802 deben ser iguales. |
| 10131 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; &lt;Auth&gt;&lt;Cuit&gt; | Si informa Id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación") con un CUIT propietario/locador no puede ser el mismo que el emisor del comprobante. |
| 10132 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa id 1801 y id 1802 (RG 4004-E Locación de inmuebles destino "casa-habitación"), es obligatorio informar el id 17 con valor 1 (Intermediario) o 2 (Directo). |
| 10133 | &lt;Compradores&gt;/&lt;Comprador&gt; | Si envía compradores, comprador es obligatorio y no debe ser vacío. |
| 10134 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Compradores&gt; | La estructura compradores se encuentra habilitada para comprobantes tipo A, B, C o M. |
| 10135 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocTipo&gt; | Si envía compradores, el tipo de documento del comprador es obligatorio informarlo. |
| 10136 | &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;/ &lt;Compradores&gt; | Solo informar compradores cuando el tipo de documento del receptor del comprobante es 80, 86, 87 (CUIT, CUIL, CDI respectivamente). |
| 10137 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocTipo&gt; | Los tipos de documentos habilitados a informar sobre el comprador son 80, 86, 87 (CUIT, CUIL, CDI respectivamente). |
| 10138 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | Si envía compradores, el número de documento del comprador es obligatorio informarlo. |
| 10139 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | Si envía compradores, el número de documento debe ser un documento con formato válido, numérico de 11 caracteres. |
| 10140 | &lt;Auth&gt;/&lt;Cuit&gt; &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | Si envía compradores, el número de documento del comprador no puede ser igual al número de documento del emisor del comprobante |
| 10141 | &lt;Compradores&gt;/&lt;Comprador&gt; | Si envía compradores, los mismos no pueden repetirse en la lista. |
| 10142 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | Si envía compradores, el porcentaje de titularidad del comprador es obligatorio informarlo. |
| 10143 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | Si envía compradores, el porcentaje de titularidad debe ser un valor numérico de 2 enteros y 2 decimales, los cuales deben ser valores mayores a cero. |
| 10144 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | Si envía compradores, el porcentaje de titularidad debe ser mayor a cero. |
| 10145 | &lt;Compradores&gt;/&lt;Comprador&gt; | Si envía compradores, los compradores informados deben ser al menos 2. Uno de los dos debe ser el receptor del comprobante. |
| 10146 | &lt;FECAEDetRequest&gt;&lt;DocTipo&gt; &lt;FECAEDetRequest&gt;&lt;DocNro&gt; &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocTipo&gt; &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | Si envía compradores, el comprador de mayor porcentaje de titularidad debe coincidir con el receptor del comprobante. |
| 10147 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | Si envía compradores, la sumatoria de todos los porcentajes de titularidad debe ser del 100%. |
| 10148 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | Si envía compradores, los compradores deben encontrarse registrados en el padrón de arca, en condición activa. |
| 10149 | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | Si envía compradores, y el tipo de comprobante es “A” o “A con leyenda operación sujeta a retención”, el receptor o al menos uno de los compradores deben encontrarse registrados de forma activa en el Impuesto al Valor Agregado o Responsable Monotributo. |
| 10150 | &lt;FECAEDetRequest&gt;&lt;Concepto&gt;/ &lt;Compradores&gt; | Solo enviar compradores cuando el concepto es 1 – PRODUCTO |
| 10151 | &lt;CbteAsoc&gt;&lt;Cuit&gt; | Si informa Cuit en comprobantes asociados, no informar en blanco, el mismo debe ser un valor de 11 caracteres numéricos. Para comprobante del tipo MiPyMEs (FCE) del tipo débito o crédito es obligatorio informar el campo. |
| 10152 | &lt;CbteFch&gt;/&lt;Concepto&gt; | Si informa fecha de comprobante &lt;CbteFch&gt; para el Concepto del tipo “01 – Productos” con fecha superior a la fecha de envío de autorización, el mes de la fecha del comprobante &lt;CbteFch&gt; debe coincidir con el mes de la fecha de envío de autorización. Si informa fecha de comprobante &lt;CbteFch&gt; para comprobantes del tipo MiPyMEs (FCE) con fecha superior a la fecha de envío de autorización, el mes de la fecha del comprobante &lt;CbteFch&gt; debe coincidir con el mes de la fecha de envío de autorización. |
| 10153 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) y corresponde a un comprobante de débito o crédito, es obligatorio informar comprobantes asociados. |
| 10154 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt;&lt;PtoVta&gt;&lt;Nro&gt;&lt;Cuit&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) y corresponde a un comprobante de débito o crédito. Tener en cuenta que: - sí el comprobante asociado se encuentra rechazado por el comprador hay que informar el código de anulación correspondiente sobre el campo "Adicionales por RG", códigos 22 - Anulación. Valor “S” - sí el comprobante asociado no se encuentra rechazado por el comprador hay que informar el código de no anulación correspondiente sobre el campo "Adicionales por RG", códigos 22 - Anulación. Valor “N” |
| 10155 | &lt;Auth&gt;&lt;Cuit&gt; &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Cuit&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el CUIT del emisor del comprobante asociado debe coincidir con el CUIT del emisor del comprobante a autorizar. |
| 10156 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito sin código de Anulación siempre debe asociar 1 comprobante tipo factura |
| 10157 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito sin código de Anulación solo puede asociar: Para comprobantes A, asociar 201 o (91, 88, 988, 990, 991, 993, 994, 995, 996, 997). Para comprobantes B, asociar 206 o (91, 88, 988, 990, 991, 993, 994, 995, 996, 997). Para comprobantes C, asociar 211 o (91, 88, 988, 990, 991, 993, 994, 995, 996, 997). |
| 10158 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, es obligatorio informar la fecha del comprobante asociado |
| 10159 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FeDetReq&gt;/&lt;CbteFch&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, la fecha del comprobante asociado tiene que ser igual o menor a la fecha del comprobante que se está autorizando |
| 10160 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, el comprobante debe existir autorizado en las bases de esta Administración con la misma fecha informada en el asociado. |
| 10161 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el receptor del comprobante debe tener habilitado el domicilio fiscal electrónico |
| 10162 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) es obligatorio informar &lt;Opcionales&gt; |
| 10163 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Tipo 201 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) A / 206 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) B / 211 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) C, es obligatorio informar FchVtoPago |
| 10164 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; / &lt;FECAEDetRequest&gt;&lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), la fecha de vencimiento de pago &lt;FchVtoPago&gt; debe ser posterior o igual a la fecha de emisión &lt;CbteFch&gt; o fecha de presentación (fecha actual), la que sea posterior |
| 10165 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2101 es un CBU numérico de 22 caracteres. |
| 10166 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2102 es un ALIAS alfanumérico de 6 a 20 caracteres. |
| 10167 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 22 es “S” o “N”: S = Es de Anulación N = No es de Anulación |
| 10168 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es Factura (201, 206, 211) del tipo MiPyMEs (FCE), informa opcionales, es obligatorio informar CBU. |
| 10169 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando NO es MiPyMEs (FCE), no informar los códigos 2101, 2102, 22, 27 |
| 10170 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es obligatorio informar al menos uno de los sig. códigos 2101, 22, 27. |
| 10171 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Factura (201, 206, 211), no informar Código de Anulación |
| 10172 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Debito (202, 207, 212) o Crédito (203, 208, 213) No informar CBU, ALIAS y Transferencia. |
| 10173 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Debito (202, 207, 212) o Crédito (203, 208, 213) informar Código de Anulación |
| 10174 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es factura MiPyMEs (FCE), el CBU debe estar registrado en las bases de esta administración, vigente y pertenecer al emisor del comprobante. |
| 10175 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el campo “fecha de vencimiento para el pago” &lt;FchVtoPago&gt; no debe informarse si NO es Factura de Crédito. En el caso de ser Débito o Crédito, solo puede informarse si es de Anulación. |
| 10176 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el campo DocNro para comprobantes deberá ser un valor registrado en el padrón de arca, en condición activa. |
| 10177 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) del tipo A, el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente activo en el Impuesto al Valor Agregado. Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) del tipo B, el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente activo en el Impuesto Iva, Monotributo o Exento. Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) del tipo C, el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente activo en el Impuesto Iva, Monotributo o Exento. |
| 10178 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) no se permite informar DocNro 23000000000 (No Categorizado) |
| 10180 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente caracterizado como GRANDE o que opto por PYME. Su activida principal debe corresponderse con alguna de las alcanzadas por el régimen. |
| 10181 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/&lt;MonId&gt; &lt;CbteAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), débito o crédito, el mismo debe tener la misma moneda que el comprobante asociado o Pesos para ajuste en las diferencias de cambio (post aceptación/rechazo) |
| 10183 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt;/ &lt;CbteAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es débito o crédito, deben coincidir emisores y receptores. Si el comprobante ES de anulación, para autorizar un débito, el tipo de comprobante a asociar debe ser crédito y para autorizar un crédito, el tipo de comprobante a asociar debe ser una factura o un débito. |
| 10184 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/&lt;MonId&gt; &lt;CbteAsoc&gt;/ &lt;FeCabReq&gt;&lt;ImpTotal&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito el monto del comprobante a autorizar no puede ser mayor o igual al saldo actual de la cuenta corriente. Ver micrositio factura de crédito |
| 10186 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito A, de anulación, solo se encuentra habilitado asociar un comprobante de crédito A. Utilizar debito para anular crédito o utilizar crédito para anular débito o factura. |
| 10187 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito B, de anulación, solo se encuentra habilitado asociar un comprobante de crédito B. Utilizar debito para anular crédito o utilizar crédito para anular débito o factura. |
| 10189 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Puede identificar una o varias Referencias Comerciales según corresponda. Informar bajo el código 23. Campo alfanumérico de 50 caracteres como máximo. |
| 10190 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa opcionales con más de un identificador 23 – Referencia Comercial, no repetir el valor. |
| 10192 | &lt;Auth&gt;&lt;Cuit&gt;/ &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocNro&gt;/ &lt;FECAEDetRequest&gt;&lt;ImpTotal&gt; / &lt;FECAEDetRequest&gt;&lt;MonCotiz&gt; / Tope | Según la categorización de las CUITs emisora y receptora y el monto facturado debe realizar una factura de crédito electrónica MiPyMEs (FCE). Ver micrositio. |
| 10193 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito C, de anulación, solo se encuentra habilitado asociar un comprobante de crédito C. Utilizar debito para anular crédito o utilizar crédito para anular débito o factura. |
| 10194 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Compradores&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), no se encuentra habilitado informar compradores. |
| 10195 | &lt;DocTipo&gt;/ &lt;DocNro&gt;/ | Si el tipo de documento del receptor del comprobante que está autorizando es CUIT (código Tipo de Documento 80) y la CUIT se encuentra inactiva por haber sido incluida en la consulta de facturas apócrifas. |
| 10196 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), no se encuentra habilitado informar PeriodoAsoc. |
| 10197 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt; | Si el comprobante es Debito o Credito, se deberá informar de forma obligatoria los campos Fecha Comprobantes Asociados Desde/Hasta, o al menos un comprobante asociado. |
| 10198 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt; | Si el comprobante es Factura no se deberá informar los campos Fecha Comprobantes Asociados Desde/Hasta |
| 10199 | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | Si envía estructura &lt;PeriodoAsoc&gt; es obligatorio enviar &lt;FchDesde&gt;. |
| 10203 | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Si envía estructura &lt;PeriodoAsoc&gt; es obligatorio enviar &lt;FchHasta&gt;. |
| 10204 | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | El campo &lt;PeriodoAsoc&gt; &lt;FchDesde&gt; debe corresponder a una fecha valida con formato YYYYMMDD |
| 10205 | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | El campo PeriodoAsoc.FchHasta debe corresponder a una fecha valida con formato YYYYMMDD |
| 10206 | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Las fechas informadas en &lt;PeriodoAsoc&gt; deben ser superiores a 01/01/2006 |
| 10207 | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Las fechas informadas en &lt;PeriodoAsoc&gt;,&lt;FchHasta&gt; debe ser superior o igual a &lt;FchDesde&gt;. |
| 10208 | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Las fecha informada en &lt;PeriodoAsoc&gt;.&lt;FchHasta&gt; debe ser anterior o igual a la fecha de emisión del comprobante que estamos autorizando |
| 10210 | &lt;FECAEDetRequest&gt;&lt;CbteFch&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | Si el comprobante asociado se autorizó de forma electrónica y tiene una fecha de emisión posterior a la fecha de emisión del comprobante por el cual se está solicitando la autorización, ambos deberán ser del mismo mes/año. |
| 10211 | &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | Informar de forma obligatoria la fecha de Emisión del comprobante asociado si el punto de venta del comprobante asociado es Controlador Fiscal o FactuWeb y el tipo de Comprobante asociado es Factura, Recibo, Nota de Débito/Nota de Crédito |
| 10212 | &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | De informar fecha de Emisión del comprobante asociado y el punto de venta del comprobante asociado es Controlador Fiscal o FactuWeb, la fecha no puede ser posterior al día de hoy. |
| 10213 | &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | Si se informan deben tener el siguiente formato yyyymmdd. |
| 10214 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el tipo de dato correcto para el código 27 es un alfanumérico de 3 caracteres. |
| 10215 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales y el código es 27, los valores posibles son: SCA = "TRANSFERENCIA AL SISTEMA DE CIRCULACION ABIERTA" ADC = "AGENTE DE DEPOSITO COLECTIVO" |
| 10216 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es Factura del tipo MiPyMEs (201, 206, 211), es obligatorio informar &lt;Opcionales&gt; con id = 27. Los valores posibles son SCA o ADC. |
| 10218 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt; | Si envía estructura de Actividades, Actividad es obligatorio enviarlo. |
| 10219 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | Si envía estructura de Actividades, Actividad es obligatorio enviarlo y no debe estar vacío. |
| 10220 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | El identificador de actividad informado tiene que ser una de las actividades habilitadas. Consultar método FEParamGetActividades. |
| 10221 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | De enviarse el tag Actividades, las actividades no deben repetirse. |
| 10222 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | De enviarse actividades, las mismas no deben corresponder a distintos grupos según RG. Es decir, si informa actividades Cárnicas, no pueden estar combinadas con actividades Harineras, de Tabaco, etc. |
| 10223 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | De enviarse actividades, las mismas deben encontrarse activas para el emisor del comprobante. Ver método FEParamGetActividades. |
| 10224 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; / &lt;Concepto&gt; | De enviarse actividades pertenecientes al grupo de actividades Cárnicas, las mismas deben enviarse con comprobantes con Concepto del tipo Producto o Productos y Servicios. |
| 10225 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si las actividades informadas corresponden a actividades Cárnicas, informar remito asociado 995 - Remito Electrónico Cárnico. |
| 10226 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si las actividades informadas corresponden a actividades Harinas, informar remito asociado 993 - Remito Electrónico Harinero - Automotor o 994 - Remito Electrónico Harinero - Ferroviario. |
| 10227 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si las actividades informadas corresponden a actividades Tabaco en Hebras, informar remito asociado 88 - Remito Electrónico de Tabaco Acondicionado. |
| 10228 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si informa remito 995 - Remito Electrónico Cárnico, es obligatorio informar una actividad Cárnica. |
| 10229 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si informa remito 993 - Remito Electrónico Harinero - Automotor o 994 - Remito Electrónico Harinero – Ferroviario, es obligatorio informar una actividad Harinera. |
| 10230 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si informa remito 88 - Remito Electrónico de Tabaco Acondicionado., es obligatorio informar una actividad correspondiente a Tabaco Acondicionado. |
| 10231 | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc | Si informa actividades indicadas en la RG, es obligatorio informar comprobantes asociados. |
| 10232 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | Si informa comprobantes asociados, y sus códigos corresponden a 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, los mismos no deben encontrarse asociado a otro comprobante. |
| 10239 | &lt;FECAEDetRequest&gt; / &lt;CanMisMonExt&gt; | Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacio. |
| 10240 | &lt;FECAEDetRequest&gt; / &lt;CanMisMonExt&gt; | Si informa el campo MonCotiz, el mismo no podra superar en 1 a la cotizacion oficial. Ver Metodo FEParamGetCotizacion. |
| 10241 | &lt;FECAEDetRequest&gt; / &lt;CanMisMonExt&gt; | Si informa MonId = PES, el campo CanMisMonExt no debe informarse o informarse con el valor N. |
| 10242 | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | El campo de identificación de la Condición de IVA del receptor no es un valor permitido. Para mayor detalle consular el método FEParamGetCondicionIvaReceptor. |
| 10243 | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | El campo de identificación de Condición de IVA del receptor no es valido para la clase de comprobante informado. Para mas detalle consultar el Método: FEParamGetCondicionIvaReceptor |
| 10246 | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | Campo Condición Frente al IVA del receptor es obligatorio conforme a lo reglamentado por la Resolución General N° 5616. Para mas información consular método FEParamGetCondicionIvaReceptor. |
| 10247 | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | La CUIT receptora informada está inactiva o es inválida. Esta validación es excluyente salvo si el tipo de comprobante informado es Nota de Crédito. **[excluyente salvo Nota de Crédito → en NC se comporta como observación (interpretación del texto)]** |
| 10248 | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | La CUIT receptora se encuentra limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. Esta validación es excluyente salvo si el tipo de comprobante informado es Nota de Crédito. **[ídem 10247]** |
| 10251 | &lt;Cuit&gt; / &lt;FeCabReq&gt;&lt;CbteTipo&gt; | Por las Condiciones de la CUIT Emisora, No corresponde realizar el Comprobante. Las entidades financieras no pueden emitir comprobantes del tipo MiPyMEs (FCE). |
| 10270 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | Por las condiciones de la CUIT emisora, no corresponde la utilización del tipo de documento ( DocTipo) = 31. El mismo solo esta destinado a entidades financieras. |
| 10271 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | El campo DocNro es invalido. Si informa DocTipo = 31, el numero de documento debe ser numérico hasta 4 dígitos |
| 10272 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;CondicionIVAReceptorId&gt; | Campo Condición IVA receptor no permitido. Para entidades financieras, si selecciona TipoDoc=31, el campo Condición Frente al IVA del Receptor debe completarse con el valor = 15 (IVA No Alcanzado) |
| 10273 | &lt;Opcionales&gt;&lt;Id&gt; | Opcionales. Id 2901 , solo se encuentran disponibles para los comprobantes que tipo 'A' (1, 2, 3) o 'B' (6, 7, 8), siempre y cuando el emisor corresponda a contribuyente de Seguros de Caución y use un Punto de Venta SEGWS. |
| 10274 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Opcionales. Id 2901 - La póliza ingresada en el campo VALOR no es valido. Se espera un alfanumérico de 30 caracteres como máximo (el valor no puede ser vacío) |
| 10275 | &lt;Opcionales&gt;&lt;Id&gt; | Opcionales. Id 2902 , solo se encuentran disponibles para los comprobantes que tipo 'A' (1, 2, 3) o 'B' (6, 7, 8), siempre y cuando el emisor corresponda a contribuyente de Seguros de Caución y use un Punto de Venta SEGWS |
| 10276 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Opcionales. Id 2902 - El endoso ingresado en el campo VALOR no es valido. Se espera un alfanumérico de 30 caracteres como máximo (el valor no puede ser vacío) |
| 10277 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si esta empadronado como emisor de Seguros de Caución, y utiliza puntos de venta SEGWS, deben informarse obligatoriamente los identificadores 2901 y 2902. |
| 10278 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Para emisores de Seguros de Caución, si utiliza punto de venta del tipo SEGWS, deben informarse obligatoriamente los identificadores 2901 y 2902. |
| 10279 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Los identificadores 2901 / 2902 solo deben informarse si el emisor corresponde a Seguros de Caución, si utiliza punto de venta del tipo SEGWS. |
| 10280 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | De informar los identificadores 2901 y 2902, no se deben informar mas de 6000 opcionales (no informar mas de 3000 combinaciones de póliza/endoso). |
| 10281 | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Opcionales Póliza/Endoso - La cantidad de identificadores 2901 deben ser iguales a la cantidad de identificadores 2902. |
| 10282 | &lt;Cuit&gt; / &lt;PtoVta&gt; | Si utiliza punto de venta del tipo SEGWS, la CUIT emisora debe pertenecer a la nomina de Seguros de Caucion. |
| 10284 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | El número de documento receptor informado no corresponde al Tipo de Documento receptor indicado (CUIT). |

#### FECAESolicitar · Controles aplicados al objeto &lt;FeDetReq&gt; · NO EXCLUYENTES (observaciones)

Fuente: manual v4.7, págs. PDF 74–76.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10017 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | El campo DocNro para comprobantes Tipo “A” y “A con leyenda operación sujeta a retención” deberá ser un valor registrado en el padrón de ARCA, en condición activa. |
| 10041 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | Si el punto de venta del comprobante asociado (campo &lt;PtoVta&gt; de &lt;CbtesAsoc&gt;) es electrónico, el número de comprobante debe obrar en las bases del organismo para el punto de venta y tipo de comprobante informado. |
| 10063 | DocTipo / DocNro | Para comprobantes Clase “A” y “A con leyenda operación sujeta a retención” el receptor del comprobante informado en &lt;DocTipo&gt; y &lt;DocNro&gt; debe corresponder a un contribuyente activo en el Impuesto al Valor Agregado o Responsable Monotributo. |
| 10188 | &lt;Auth&gt;&lt;Cuit&gt;/ &lt;DocTipo&gt;/ &lt;DocNro&gt;/ | Si el tipo de comprobante que está autorizando es 1 - Factura A o 4 - Recibo A o 6 - Factura B o 9 - Recibo B o 11 - Factura C o 15 - Recibo C, por la categorización de las cuits emisora y receptora, se deberia realizar una factura de crédito electrónica. |
| 10209 | &lt;FECAEDetRequest&gt;&lt;Tributos&gt;&lt;Id&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Si en la estructura Tributos informa percepciones, &lt;PeriodoAsoc&gt;.&lt;FchDesde&gt; y &lt;PeriodoAsoc&gt;.&lt;FchHasta&gt; deben corresponder al mismo Mes/Anio |
| 10217 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | Para comprobantes Clase “A” y “A con leyenda operación sujeta a retención”, donde el receptor del comprobante informado en &lt;DocTipo&gt; y &lt;DocNro&gt; se encuentra activo en el Impuesto Responsable Monotributo, “El crédito fiscal discriminado en el presente comprobante solo podrá ser computado a efectos del Procedimiento permanente de transición al Régimen General.” |
| 10234 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | Para comprobantes Clase “A” y “A con leyenda operación sujeta a retención”, se ha detectado que esta pendiente de presentación el formulario de habilitación de comprobantes o su fecha de presentación es anterior a la fecha de alta en IVA. Para el caso que el comprobante no sea una Nota de Crédito, se debe proceder a anular la operación clase “A” o “A con leyenda operación sujeta a retención” emitida, mediante una Nota de Crédito. |
| 10235 | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEDetRequest&gt; / ImpTotal | El monto total del comprobante emitido, excede el límite establecido para la categoría máxima de Monotributo. Por tal motivo, quedarías excluido automáticamente teniendo que solicitar el alta de los tributos (impositivos y de los recursos de la seguridad social) en el régimen general de acuerdo con tu actividad. |
| 10236 | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEDetRequest&gt; / ImpTotal | El monto del comprobante emitido, excede el límite establecido para su categoría de Monotributo. Tenelo en cuenta para la próxima recategorización. |
| 10237 | &lt;FECAEDetRequest&gt; / ImpTotal / &lt;CbteAsoc&gt; | El importe de la nota de crédito supera el monto del comprobante asociado que estás ajustando. Verificá los montos ingresados y de tratarse de un error, tenés que efectuar el ajuste o anulación de la operación según corresponda. |
| 10238 | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | La CUIT receptora ingresada no existe. |
| 10245 | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | El campo Condición Frente al IVA del receptor resultará obligatorio conforme lo reglamentado por la Resolución General N° 5616. Para mas información consular método FEParamGetCondicionIvaReceptor. **[v4.8: queda en desuso]** |
| 10249 | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | El número de documento informado para el sujeto receptor corresponde a un sujeto fallecido, sin sucesión indivisa registrada. |
| 10283 | &lt;DocTipo&gt; / &lt;DocNro&gt; / &lt;Tributo&gt; | Falta informar en la sección Otros Tributos el tributo con ID = 13 – Percepción de IVA No Categorizado según la RG 2126/2006. Si &lt;CbteTipo&gt; es igual a 6, 7 u 8, &lt;DocTipo&gt; es 80 (CUIT), &lt;DocNro&gt; es 23000000000 (No Categorizado), la suma de ImpNeto + ImpIVA es mayor a 0 (cero), y el campo ImpTrib &gt; 0, debe informar el ID “13 – Percepción de IVA No Categorizado“ en el array de tributos con un importe mayor a 0 (cero) |

### 4.2. FECAEARegInformativo


#### FECAEARegInformativo · Controles aplicados al objeto &lt;Auth&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 140.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10000 | &lt;Auth&gt;&lt;Cuit&gt; | La CUIT del emisor debe estar registrada y activa en las bases de la Administración. |

#### FECAEARegInformativo · Controles aplicados al objeto &lt;FeCabReq&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 140–142.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10001 | &lt;CantReg&gt; | Cantidad de registros de detalle del comprobante o lote de comprobantes de ingreso &lt;CantReg&gt; debe estar comprendido entre 1 y 9998 |
| 10002 | &lt;CantReg&gt; | La cantidad de registros del detalle del comprobante o lote de comprobantes de ingreso debe ser igual a lo informado en cabecera del comprobante o lote de comprobantes de ingreso &lt;CantReg&gt;. |
| 10003 | Cantidad de registros incluidos | La cantidad de registros en detalle debe ser menor igual al valor permitido. Consulte método FECompTotXRequest para obtener cantidad máxima de registros por cada requerimiento. Para comprobantes del tipo MiPyMEs (FCE), la cantidad habilitada es 1 comprobante por request |
| 700 | CbteTipo | Obligatorio. Valores permitidos: 1: Factura A 2: Nota de Débito A 3: Nota de Crédito A 4: Recibo A 6: Factura B 7: Nota de Débito B 8: Nota de Crédito B 9: Recibo B 11: Factura C 12: Nota de Débito C 13: Nota de Crédito C 15: Recibo C 51: Factura “A con leyenda operación sujeta a retención” (CAEA observa comprobante) 52: Nota de Débito “A con leyenda operación sujeta a retención” (CAEA observa comprobante) 53: Nota de Crédito “A con leyenda operación sujeta a retención” (CAEA observa comprobante) 54: Recibo “A con leyenda operación sujeta a retención” 63: Liquidaciones A 64: Liquidaciones B 201: Factura de Crédito electrónica MiPyMEs (FCE) A 202: Nota de Débito electrónica MiPyMEs (FCE) A 203: Nota de Crédito electrónica MiPyMEs (FCE) A 206: Factura de Crédito electrónica MiPyMEs (FCE) B 207: Nota de Débito electrónica MiPyMEs (FCE) B 208: Nota de Crédito electrónica MiPyMEs (FCE) B 211: Factura de Crédito electrónica MiPyMEs (FCE) C 212: Nota de Débito electrónica MiPyMEs (FCE) C 213: Nota de Crédito electrónica MiPyMEs (FCE) C Consultar método FEParamGetTiposCbte |
| 1300 | PtoVta | Campo PtoVta debe estar comprendido entre 1 y 99998. |
| 701 | PtoVta | El punto de Venta debe ser del tipo habilitado para CAEA - Fact. Elect. (RECE) - RI IVA / CAEA – Fact. Elect. (RECE) - Contingencias / CAEA – Fact. Elect. (RECE) - Exento en IVA - Contingencias / CAEA – Fact. Elect. (RECE) - Monotributo - Contingencias y no debe estar bloqueado a la fecha en que se emitió el comprobante. Consultar método FEParamGetPtosVenta. |

#### FECAEARegInformativo · Verificaciones que se realizan sobre el elemento &lt;FECAEADetRequest&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 142–155.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 702 | CbteFch | Debe estar comprendida dentro de la fecha desde y fecha hasta de vigencia del CAEA |
| 703 | CbteDesde / CbteHasta / PtoVta / CbteTipo | El número de comprobante informado debe ser mayor en 1 al último informado para igual punto de venta y tipo de comprobante. Consultar método FECompUltimoAutorizado |
| 704 | CbteFch / PtoVta / CbteTipo | La fecha del comprobante debe ser mayor o igual a la fecha del último comprobante informado para igual tipo de comprobante y punto de venta. |
| 705 | CAEA | Debe corresponder a la CUIT que está informando |
| 1414 | Fecha de envío de la solicitud | Al informar un comprobante con la modalidad CAEA, la fecha en la que se informa el comprobante debe ser mayor a la fecha de entrada en vigencia del CAEA vinculado |
| 709 | CAEA / PtoVta | La fecha de alta del punto de venta deberá ser menor o igual a la fecha de vigencia “hasta” del CAEA |
| 1401 | MonId | El campo MonId es obligatorio y debe corresponder a algún valor devuelto por el método FEParamGetTiposMonedas. |
| 713 | Concepto | Valores permitidos: 1 Productos 2 Servicios 3 Productos y Servicios Consultar método FEParamGetTiposConcepto |
| 715 | ImpIVA / Iva / AlicIva | Si ImpIVA es igual a 0 los objetos Iva y AlicIva solo deben informarse con ImpIVA = 3 (iva 0) Si ImpIVA es mayor a 0 el objeto Iva y AlicIva son obligatorios. El objeto AlicIva es obligatorio y no debe ser nulo si ingresa Iva. |
| 717 | &lt;ImpTotConc&gt; | El campo ImpTotConc (Importe neto no gravado) no puede ser menor a cero (0). El campo ImpTotConc soporta 13 números para la parte entera y 2 para los decimales. |
| 718 | &lt;ImpOpEx&gt; | El campo ImpOpEx soporta 13 números para la parte entera y 2 para los decimales. El campo ImpOpEx (importe exento) no puede ser menor a cero (0). |
| 719 | &lt;ImpNeto&gt; | El campo ImpNeto (Importe neto gravado) no puede ser menor a cero (0) El campo ImpNeto soporta 13 números para la parte entera y 2 para los decimales. |
| 723 | &lt;ImpTrib&gt; | El campo ImpTrib (Importe de tributos) no puede ser menor a cero (0). El campo ImpTrib soporta 13 números para la parte entera y 2 para los decimales. |
| 1407 | &lt;ImpIVA&gt; | El campo ImpIVA (Importe de IVA) no puede ser menor a cero (0). El campo ImpIVA soporta 13 números para la parte entera y 2 para los decimales. |
| 726 | &lt;MonCotiz&gt; | El campo MonCotiz es obligatorio y mayor a 0 Debe ser igual a 1 (uno) si &lt;MonId&gt; es igual a PES. Si &lt;MonId&gt; es diferente a PES que &lt;MonCotiz&gt; sea Mayor a 0. El campo MonCotiz es opcional si informa el campo CanMisMonExt con el valor S y el tipo de comprobante es factura y la moneda tiene cotización en Banco Nación. El campo MonCotiz soporta 4 números para la parte entera y 6 para los decimales. |
| 780 | CAEA | Deberá corresponder a un CAEA registrado en las bases de la Administración |
| 781 | PtoVta / CbteFch | La fecha de alta del punto de venta deberá ser menor o igual a la fecha del comprobante |
| 782 | CAEA | Obligatorio, numérico de 14 posiciones |
| 783 | CbteFch | Obligatorio, formato yyyymmdd |
| 784 | CbteDesde / CbteHasta | Obligatorio, entero; valores comprendidos entre 1 y 99999999. |
| 1416 | &lt;CbteHasta&gt; / &lt;CbteDesde&gt; | Para comprobantes tipo B, &lt;CbteHasta&gt; sea mayor o igual a &lt;CbteDesde&gt; |
| 1415 | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | Para comprobantes tipo B (CbteDesde distinto a CbteHasta) y el resultado de la operación ImpTotal / (CbteHasta – CbteDesde + 1 ) &lt; monto en pesos resultante según RG4444, el campo DocNro deberá ser cero (0) y el campo DocTipo 99. |
| 1417 | DocTipo / DocNro / CbteDesde / CbteHasta | Para comprobantes B o C (CbteDesde igual a CbteHasta) mayor o igual a monto en pesos resultante según RG4444, DocTipo debe ser uno de los valores devueltos por el método FEParamGetTiposDoc distinto a 99 y DocNro deberá ser mayor a 0. |
| 1418 | DocTipo / DocNro / CbteDesde / CbteHasta | Para comprobantes B o C (CbteDesde igual a CbteHasta) menor a monto en pesos resultante según RG4444, si DocTipo = 99 DocNro debe ser igual a 0. |
| 1419 | DocTipo / DocNro / CbteDesde / CbteHasta | Para comprobantes B o C (CbteDesde igual a CbteHasta) menor a monto en pesos resultante según RG4444, si DocTipo es distinto a 99, DocNro debe ser mayor a 0. |
| 1422 | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | Para comprobantes tipo B, &lt;CbteDesde&gt; distinto a &lt;CbteHasta&gt; el resultado de la operación ImpTotal / (CbteHasta – CbteDesde + 1 ) &lt; monto en pesos resultante según RG4444. |
| 711 | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | Para comprobantes clase A y comprobantes MiPyMEs (FCE) el campo CbteDesde debe ser igual al campo CbteHasta |
| 1403 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; | Para comprobantes clase A el campo DocTipo debe ser igual a 80 (CUIT) |
| 1409 | &lt;ImpTotal&gt; | El campo ImpTotal no puede ser menor a cero (0). El campo ImpTotal soporta 13 números para la parte entera y 2 para los decimales. |
| 1404 | &lt;DocTipo&gt; / &lt;DocNro&gt; | Para comprobantes tipo B o tipo C, si informa &lt;DocTipo&gt; y &lt;DocNro&gt;, &lt;DocTipo&gt; debe ser un valor devuelto por el método FEParamGetTiposDoc. |
| 1405 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | Para comprobantes tipo B o tipo C el campo DocNro debe ser un valor comprendido entre 0 y 99999999999 |
| 1421 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | Para comprobantes tipo A el campo DocNro debe ser un valor comprendido entre 20000000000 y 60000000000 |
| 788 | DocTipo / DocNro | Cuando se informa tipo de comprobante 80, el documento informado no puede ser el mismo al ingresado en el campo &lt;Auth&gt;&lt;Cuit&gt; |
| 1423 | &lt;ImpTrib&gt; / &lt;Tributos&gt; / &lt;Tributo&gt; | Si ImpTrib es igual a 0 el objeto Tributos y Tributo no deben informarse. Si ImpTrib es mayor a 0 el objeto Tributos y Tributo son obligatorios. Si ImpTrib mayor a 0, Tributos y Tributo no pueden venir vacíos. |
| 1426 | &lt;Opcionales&gt;&lt;CbteTipo&gt; | El array &lt;Opcionales&gt; no es obligatorio. Solo puede informarse si &lt;CbteTipo&gt; es 1, 2, 3, 4, 5, 6, 7, 8, 34, 39, 60, 63, 64, 201, 202, 203, 206, 207, 208, 211, 212, 213 |
| 1432 | &lt;Compradores&gt; | No se encuentra habilitado informar compradores en el régimen de información para la modalidad CAEA. |
| 1433 | &lt;CbteTipo&gt;/ &lt;CbteDesde&gt;/ &lt;CbteHasta&gt; | Para comprobantes tipo C &lt;CbteHasta&gt; debe ser igual a &lt;CbteDesde&gt;. |
| 1434 | &lt;CbteTipo&gt;/ &lt;ImpTotConc&gt; | Para comprobantes tipo C, el campo “Importe neto no gravado” &lt;ImpTotConc&gt; debe ser igual a cero (0). |
| 1435 | &lt;CbteTipo&gt;/ &lt;ImpOpEx&gt; | Para comprobantes tipo C, el campo &lt;ImpOpEx&gt; debe ser igual a cero (0). |
| 1436 | &lt;CbteTipo&gt;/ &lt;ImpNeto&gt; | Para comprobantes tipo C el campo &lt;ImpNeto&gt; corresponde al Importe del Sub Total. |
| 1437 | &lt;CbteTipo&gt;/ &lt;ImpTrib&gt; | Para comprobantes tipo C, el campo “Importe de tributos” &lt;ImpTrib&gt;. No puede ser menor a cero (0). |
| 1438 | &lt;CbteTipo&gt;/ &lt;ImpIVA&gt; | Para comprobantes tipo C, el campo “Importe de IVA” &lt; ImpIVA&gt; debe ser igual a cero (0). |
| 1439 | &lt;CbteTipo&gt;/ &lt;ImpTotal&gt;/ &lt;ImpNeto&gt; / &lt;ImpTrib&gt; / | Para comprobantes tipo C, el campo “Importe Total” &lt;ImpTotal&gt;, debe ser igual a la suma de ImpNeto + ImpTrib. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 |
| 1440 | &lt;CbteFchHsGen&gt; | Si el punto de venta es para CONTINGENCIAS CAEA el campo es obligatorio informarlo |
| 1441 | &lt;CbteFchHsGen&gt; | Si informa el campo, el mismo tiene que contener un valor según lo definido en la estructura. Formato yyyymmddhhmiss |
| 1443 | &lt;Iva&gt; | Si el tipo de comprobante es C, el array de IVA no debe informarse. |
| 1444 | &lt;PtoVta&gt; /&lt;CbteTipo&gt; | Si el comprobante es tipo “A”, “B”, “A con leyenda operación sujeta a retención” los puntos de venta habilitados son CAEA - Fact. Elect. (RECE) - RI IVA / CAEA - Fact. Elect. (RECE) - RI IVA - Contingencias. Si el comprobante es tipo C, los puntos de venta habilitados son CAEA - Fact. Elect. (RECE) - Exento en IVA – Contingencias / CAEA - Fact. Elect. (RECE) - Monotributo - Contingencias |
| 1445 | &lt;FeCabReq&gt;&lt;CbteTipo&gt; / &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el campo DocNro para comprobantes deberá ser un valor registrado en el padrón de arca, en condición activa. |
| 1446 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el campo DocNro para comprobantes deberá ser un valor registrado en el padrón de arca, en condición activa. |
| 1450 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) y corresponde a un comprobante de débito o crédito, es obligatorio informar comprobantes asociados. |
| 1451 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt;&lt;PtoVta&gt;&lt;Nro&gt;&lt;Cuit&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) y corresponde a un comprobante de débito o crédito. Tener en cuenta que: - sí el comprobante asociado se encuentra rechazado por el comprador hay que informar el código de anulación correspondiente sobre el campo "Adicionales por RG", códigos 22 - Anulación. Valor “S” - sí el comprobante asociado no se encuentra rechazado por el comprador hay que informar el código de no anulación correspondiente sobre el campo "Adicionales por RG", códigos 22 - Anulación. Valor “N” |
| 1452 | &lt;Auth&gt;&lt;Cuit&gt; &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Cuit&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el CUIT del emisor del comprobante asociado debe coincidir con el CUIT del emisor del comprobante a autorizar. |
| 1453 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito sin código de Anulación siempre debe asociar 1 solo comprobante tipo factura. |
| 1454 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito sin código de Anulación solo puede asociar: Para comprobantes A, asociar 201 o (91, 88, 988, 990, 991, 993, 994, 995, 996, 997). Para comprobantes B, asociar 206 o (91, 88, 988, 990, 991, 993, 994, 995, 996, 997). Para comprobantes C, asociar 211 o (91, 88, 988, 990, 991, 993, 994, 995, 996, 997). |
| 1455 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, es obligatorio informar la fecha del comprobante asociado |
| 1456 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FeDetReq&gt;/&lt;CbteFch&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, la fecha del comprobante asociado tiene que ser igual o menor a la fecha del comprobante que se está autorizando |
| 1457 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, el comprobante debe existir autorizado en las bases de esta Administración con la misma fecha informada en el asociado. |
| 1458 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el receptor del comprobante debe tener habilitado el domicilio fiscal electrónico |
| 1459 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) es obligatorio informar &lt;Opcionales&gt; |
| 1460 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Tipo 201 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) A / 206 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) B / 211 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) C, es obligatorio informar FchVtoPago |
| 1461 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; / &lt;FECAEDetRequest&gt;&lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), la fecha de vencimiento de pago (FchVtoPago) debe ser posterior o igual a la fecha de emisión (CbteFch) o fecha de presentación (fecha actual), la que sea posterior |
| 1462 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2101 es un CBU numérico de 22 caracteres. |
| 1463 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2102 es un ALIAS alfanumérico de 6 a 20 caracteres. |
| 1464 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 22 es “S” o “N”: S = Es de Anulación N = No es de Anulación |
| 1465 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es Factura (201, 206, 211) del tipo MiPyMEs (FCE), informa opcionales, es obligatorio informar CBU. |
| 1466 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando NO es MiPyMEs (FCE), no informar los códigos 2101, 2102, 22, 27 |
| 1467 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es obligatorio informar al menos uno de los sig. códigos 2101, 22, 27 |
| 1468 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Factura (201, 206, 211), no informar Código de Anulación |
| 1469 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Debito (202, 207, 212) o Crédito (203, 208, 213) No informar CBU y ALIAS. |
| 1470 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Debito (202, 207, 212) o Crédito (203, 208, 213) informar Código de Anulación |
| 1471 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es factura MiPyMEs (FCE), el CBU debe estar registrado en las bases de esta administración, vigente y pertenecer al emisor del comprobante. |
| 1472 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el campo “fecha de vencimiento para el pago” &lt;FchVtoPago&gt; no debe informarse si NO es Factura de Crédito. En el caso de ser Débito o Crédito, solo puede informarse si es de Anulación. |
| 1474 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) del tipo A, el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente activo en el Impuesto al Valor Agregado o Responsable Monotributo. Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) del tipo B, el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente activo en el Impuesto Iva, Monotributo o Exento. Si el tipo de comprobante que esta autorizando es MiPyMEs (FCE) del tipo C, el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente activo en el Impuesto Iva, Monotributo o Exento. |
| 1475 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) no se permite informar DocNro 23000000000 (No Categorizado) |
| 1476 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el receptor del comprobante informado en DocTipo y DocNro debe corresponder a un contribuyente caracterizado como GRANDE o que opto por PYME. Su activida principal debe corresponderse con alguna de las alcanzadas por el régimen. |
| 1477 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;MonId&gt; &lt;CbteAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), débito o crédito, el mismo debe tener la misma moneda que el comprobante asociado o Pesos para ajuste en las diferencias de cambio (post aceptación/rechazo) |
| 1478 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt;/ &lt;CbteAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es débito o crédito, deben coincidir emisores y receptores. Si el comprobante ES de anulación, para autorizar un débito, el tipo de comprobante a asociar debe ser crédito y para autorizar un crédito, el tipo de comprobante a asociar debe ser una factura o un débito. |
| 1479 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;MonId&gt; &lt;CbteAsoc&gt;/ &lt;FeCabReq&gt;&lt;ImpTotal&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito el monto del comprobante a autorizar no puede ser mayor o igual al saldo actual de la cuenta corriente. Ver micrositio factura de crédito |
| 1480 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito A, de anulación, solo se encuentra habilitado asociar un comprobante de crédito A. Utilizar debito para anular crédito o utilizar crédito para anular débito o factura. |
| 1481 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito B, de anulación, solo se encuentra habilitado asociar un comprobante de crédito B. Utilizar debito para anular crédito o utilizar crédito para anular débito o factura. |
| 1482 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Puede identificar una o varias Referencias Comerciales según corresponda. Informar bajo el código 23. Campo alfanumérico de 50 caracteres como máximo. |
| 1483 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa opcionales con más de un identificador 23 – Referencia Comercial, no repetir el valor. |
| 1486 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito C, de anulación, solo se encuentra habilitado asociar un comprobante de crédito C. Utilizar debito para anular crédito o utilizar crédito para anular débito o factura. |
| 1487 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | Para comprobantes MiPyMEs (FCE) el documento del receptor debe ser 80 CUIT. |
| 1488 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, el comprobante debe existir autorizado en las bases de esta Administración. |
| 1490 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), no se encuentra habilitado informar PeriodoAsoc. |
| 1491 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt; | Si el comprobante es Debito o Crédito, se deberá informar de forma obligatoria los campos Fecha Comprobantes Asociados Desde/Hasta, o al menos un comprobante asociado. |
| 1492 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt; | Si el comprobante es Factura no se deberá informar los campos Fecha Comprobantes Asociados Desde/Hasta |
| 1493 | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | Si envía estructura PeriodoAsoc es obligatorio enviar FchDesde. |
| 1494 | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Si envía estructura PeriodoAsoc es obligatorio enviar FchHasta. |
| 1495 | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | El campo PeriodoAsoc.FchDesde debe corresponder a una fecha valida con formato YYYYMMDD |
| 1496 | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | El campo PeriodoAsoc.FchHasta debe corresponder a una fecha valida con formato YYYYMMDD |
| 1497 | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Las fechas informadas en PeriodoAsoc deben ser superiores a 01/01/2006 |
| 1498 | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Las fechas informadas en PeriodoAsoc , FchHasta debe ser superior o igual a FchDesde. |
| 1499 | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Las fecha informada en PeriodoAsoc.FchHasta debe ser anterior o igual a la fecha de emisión del comprobante que estamos autorizando |
| 1502 | &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | Informar de forma obligatoria la fecha de Emisión del comprobante asociado si el punto de venta del comprobante asociado es Controlador Fiscal o FactuWeb y el tipo de Comprobante asociado es Factura, Recibo, Nota de Débito/Nota de Crédito |
| 1503 | &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | De informar fecha de Emisión del comprobante asociado y el punto de venta es Controlador Fiscal o FactuWeb, la fecha no puede ser posterior al día de hoy. |
| 1504 | &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | Si se informan deben tener el siguiente formato yyyymmdd. |
| 1505 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | Si el comprobante es del tipo A (1, 2, 3, 4, 5, 34, 39, 60, 63) e intenta informar datos opcionales según Resolución General 3668, los valores posibles para los identificadores son 5, 61, 62, 7. |
| 1506 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 5, el valor ingresado no puede ser blanco y debe ser alfanumérico de 2 caracteres. |
| 1507 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 5, el contenido del campo &lt;Valor&gt; debe corresponder a un código de EXCEPCION válido comprendido por alguno de los sig: 01 – Locador / Prestador del mismo 02 – Congresos / Eventos 03 – Operación contemplada en RG 74 04 – Bienes de Cambio 05 – Ropa de trabajo 06 – Intermediario |
| 1508 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 61, el valor ingresado no puede ser blanco y debe ser numérico de 2 caracteres. |
| 1509 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 61, el contenido del campo &lt;Valor&gt; debe corresponder a un código que represente el tipo de documento del firmante. Ver método FEParamGetTiposDoc. |
| 1510 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 62, el valor ingresado no puede ser blanco y debe ser numérico de 11 caracteres como máximo. |
| 1511 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 7, el valor ingresado no puede ser blanco y debe ser numérico de 2 caracteres. |
| 1512 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | Si informa Id = 7, el contenido del campo &lt;Valor&gt; debe corresponder a un código de carácter firmante válido comprendido por alguno de los sig: 01 – Titular 02 – Director / Presidente 03 – Apoderado 04 – Empleado |
| 1513 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el tipo de dato correcto para el código 27 es un alfanumérico de 3 caracteres. |
| 1514 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales y el código es 27, los valores posibles son: SCA = "TRANSFERENCIA AL SISTEMA DE CIRCULACION ABIERTA" ADC = "AGENTE DE DEPOSITO COLECTIVO" |
| 1515 | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si el tipo de comprobante que está autorizando es Factura del tipo MiPyMEs (201, 206, 211), es obligatorio informar &lt;Opcionales&gt; con id = 27. Los valores posibles son SCA o ADC. |
| 1517 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt; | Si el comprobante es del tipo B e intenta informar datos opcionales según Resolución General 4004- E, los valores posibles para los identificadores son 17, 1801, 1802. |
| 1518 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa id = 17 (RG 4004-E Locación de inmuebles destino "casa-habitación"), el valor ingresado no puede ser blanco y debe ser un numérico de 1 carácter: 1 (uno) = facturación a través de intermediario 2 (dos) = facturación directa |
| 1519 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa Id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación") con un CUIT propietario/locador no repetirlo dentro de la lista de propietarios/locadores |
| 1520 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa Id = 1802 (RG 4004-E Locación de inmuebles destino "casa-habitación") el valor ingresadono puede ser un blanco y debe ser un alfanumérico de 100 caracteres como máximo que representa el Nombre y Apellido propietario/locador. |
| 1521 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa id 17 con valor 1 (Intermediario) (RG 4004-E Locación de inmuebles destino "casahabitación"), deben informarse obligatoriamente los identificadores 1801 y 1802. Si informa id 17 con valor 2 (Directo) (RG 4004-E Locación de inmuebles destino "casa-habitación"), pueden no informarse los identificadores 1801, 1802. Solo informarlos cuando hay otro/s propietarios/locadores |
| 1522 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación"), el valor ingresado no puede ser blanco y debe corresponder al CUIT del propietario/locador. Numérico de 11 caracteres. |
| 1523 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa Id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación"), verificar que el número consignado se encuentra en los padrones de ARCA. |
| 1524 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa Id = 1801 (RG 4004-E Locación de inmuebles destino "casa-habitación") con un CUIT propietario/locador no puede ser el mismo que el emisor del comprobante. |
| 1525 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa id 1801 y id 1802 (RG 4004-E Locación de inmuebles destino "casa-habitación"), la cantidad de opcionales con id 1801 y 1802 deben ser iguales. |
| 1526 | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | Si informa id 1801 y id 1802 (RG 4004-E Locación de inmuebles destino "casa-habitación"), es obligatorio informar el id 17 con valor 1 (Intermediario) o 2 (Directo) |
| 820 | &lt;FECAEADetRequest&gt; / &lt;CanMisMonExt&gt; | Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacío. |
| 823 | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | El campo de identificación de la Condición de IVA del receptor no es un valor permitido. Para mayor detalle consular el método FEParamGetCondicionIvaReceptor. |
| 826 | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | Campo Condición Frente al IVA del receptor es obligatorio conforme a lo reglamentado por la Resolución General N° 5616. Para mas información consular método FEParamGetCondicionIvaReceptor. |
| 1528 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | El número de documento receptor informado no corresponde al Tipo de Documento receptor indicado (CUIT) |

#### FECAEARegInformativo · Verificaciones que se realizan sobre el elemento &lt;FECAEADetRequest&gt; · NO EXCLUYENTES (observaciones)

Fuente: manual v4.7, págs. PDF 155–159.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 708 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | El campo DocNro para comprobantes Tipo A deberá ser un valor registrado y ACTIVO en el padrón de arca. |
| 724 | &lt;ImpTotConc&gt; / &lt;ImpOpEx&gt; / &lt;ImpNeto&gt; / &lt;ImpTrib&gt; / &lt;ImpIVA&gt; / &lt;ImpTotal&gt; | El campo “Importe Total” &lt;ImpTotal&gt;, debe ser igual a la suma de ImpTotConc + ImpNeto + ImpOpEx + ImpTrib + ImpIVA Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 |
| 728 | FchServHasta | Debe informarse solo si &lt;Concepto&gt; es igual a 2 ó 3. En otro caso no corresponde. |
| 725 | &lt;ImpIVA&gt; | Debe ser igual a la sumatoria de la totalidad de los campos &lt;importe&gt; (dentro de &lt;AlicIVA&gt;) Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de alícuotas de IVA ingresadas* |
| 1402 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | Para comprobantes Tipo A deberá encontrarse registrado en condición activa en el Impuesto al Valor Agregado o Responsable Monotributo. |
| 727 | &lt;FchServDesde&gt; | FchServDesde debe informarse solo si Concepto es igual a 2 o 3. En otro caso no corresponde. |
| 1420 | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | Para comprobantes tipo B o C (CbteDesde igual a CbteHasta) y DocTipo 80, 86, 87, DocNro deberá ser un valor registrado en el padrón de arca. Si DocTipo es 80 y DocNro es 23000000000 (No Categorizado) esta validación no se tendrá en cuenta. |
| 1408 | &lt;ImpNeto&gt; / &lt;AlicIva&gt; &lt;BaseImp&gt; | La suma de los campos &lt;BaseImp&gt; en &lt;AlicIva&gt; debe ser igual al valor ingresado en ImpNeto. Esta validación no deberá ser tenida en cuenta, cuando el &lt;CbteTipo&gt; sea 02, 03, 07, 08. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de alícuotas de IVA ingresadas * |
| 1411 | FchVtoPago | Debe ser mayor o igual a la fecha del comprobante. |
| 729 | FchVtoPago | Debe informarse solo si &lt;Concepto&gt; es igual a 2 ó 3. En otro caso no corresponde. |
| 1412 | &lt;FchServDesde&gt;/ &lt;FchServHasta&gt; | &lt;FchServDesde&gt; no puede ser posterior al campo &lt;FchServHasta&gt;. |
| 1406 | &lt;ImpTrib&gt; | Debe ser igual a la sumatoria de la totalidad de los campos &lt;Importe&gt; (dentro de &lt;Tributos&gt;). Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de tributos * |
| 1424 | CAEA / &lt;PtoVta&gt; | El CAEA y punto de venta no debe estar informado sin movimientos. |
| 1425 | &lt;ImpTrib&gt; &lt;DocTipo&gt;&lt;DocNro&gt; | Falta informar en la sección Otros Tributos el tributo con ID = 13 – Percepción de IVA No Categorizado según la RG 2126/2006. Si &lt;CbteTipo&gt; es igual a 6, 7 u 8, &lt;DocTipo&gt; es 80 (CUIT), &lt;DocNro&gt; es 23000000000 (No Categorizado), la suma de ImpNeto + ImpIVA es mayor a 0 (cero), y el campo ImpTrib = 0, debe informar el ID “13 – Percepción de IVA No Categorizado“ en el array de tributos con un importe mayor a 0 (cero) |
| 1413 | &lt;FchServDesde&gt;/ &lt;FchServHasta&gt;/ &lt;FchVtoPago&gt; | Si se informan deben tener el siguiente formato yyyymmdd. |
| 1427 | &lt;ImpNeto&gt;/ &lt;Iva&gt;.&lt;AlicIva&gt; | Si ImpNeto es mayor a 0, el objeto AlicIva es obligatorio y no debe ser nulo. |
| 1429 | &lt;Auth&gt;&lt;Cuit&gt; / &lt;CbteTipo&gt; / &lt;CbteFch&gt; | No se encuentra habilitado a emitir comprobantes “A” a la fecha de emisión del comprobante. El comprobante queda observado. |
| 1431 | &lt;Auth&gt;&lt;Cuit&gt; / &lt;CbteTipo&gt; / &lt;CbteFch&gt; | Al momento de emitir el comprobante, debe estar dado de alta en el Impuesto. |
| 1442 | &lt;CbteFchHsGen&gt; | Si el punto de venta informado no es para CONTINGENCIA no informar el campo &lt;CbteFchHsGen&gt; |
| 1445 | &lt;CbteFch&gt;/ &lt;CbteFchHsGen&gt;/ &lt;Concepto&gt; | El campo &lt;CbteFch&gt; podrá estar comprendido en el rango N-5 y N+5 siendo N la fecha CbteFchHsGen (contingencia) para Concepto= 01 Productos. - Para Concepto 02, 03 el campo CbteFch debe estar comprendido en el rango N-10 y N+10 siendo N la fecha CbteFchHsGen (contingencia). |
| 1485 | &lt;Auth&gt;&lt;Cuit&gt;/ &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocNro&gt;/ &lt;FECAEDetRequest&gt;&lt;ImpTotal&gt; / &lt;FECAEDetRequest&gt;&lt;MonCotiz&gt; / Tope | Según la categorización de las CUITs emisora y receptora y el monto facturado debe realizar una factura de crédito electrónica MiPyMEs (FCE). Ver micrositio. |
| 1489 | &lt;DocTipo&gt;/ &lt;DocNro&gt;/ | Si el tipo de documento del receptor del comprobante que está autorizando es CUIT (código Tipo de Documento 80) y la CUIT se encuentra inactiva por haber sido incluida en la consulta de facturas apócrifas, no podrá computarse el crédito fiscal. |
| 1500 | &lt;FECAEADetRequest&gt;&lt;Tributos&gt;&lt;Id&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | Si en la estructura Tributos informa percepciones, PeriodoAsoc.FchDesde y PeriodoAsoc.FchHasta deben corresponder al mismo Mes/Anio |
| 1501 | &lt;FECAEADetRequest&gt;&lt;CbteFch&gt;/ &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | Si el comprobante asociado se autorizó de forma electrónica y tiene una fecha de emisión posterior a la fecha de emisión del comprobante por el cual se está solicitando la autorización, ambos deberán ser del mismo mes/año. |
| 1516 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | Para comprobantes Clase “A” y “A con leyenda operación sujeta a retención”, donde el receptor del comprobante informado en &lt;DocTipo&gt; y &lt;DocNro&gt; se encuentra activo en el Impuesto Responsable Monotributo, “El crédito fiscal discriminado en el presente comprobante solo podrá ser computado a efectos del Procedimiento permanente de transición al Régimen General.” |
| 814 | &lt;CbteTipo&gt; / &lt;DocNro&gt; | Para comprobantes Clase “A” y “A con leyenda operación sujeta a retención”, se ha detectado que esta pendiente de presentación el formulario de habilitación de comprobantes o su fecha de presentación es anterior a la fecha de alta en IVA. Para el caso que el comprobante no sea una Nota de Crédito, se debe proceder a anular la operación clase “A” y “A con leyenda operación sujeta a retención” emitida, mediante una Nota de Crédito. |
| 815 | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEADetRequest&gt;&lt;CbteFch&gt;/ | A la fecha de emisión del comprobante, no te encontrabas habilitado a la emisión de comprobantes clase “A” / “A con leyenda operación sujeta a retención”. Tenés que proceder a emitir la Nota de Crédito, o anular la operación, según corresponda. |
| 816 | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEADetRequest&gt; / ImpTotal | El monto total del comprobante emitido, excede el límite establecido para la categoría máxima de Monotributo. Por tal motivo, quedarías excluido automáticamente teniendo que solicitar el alta de los tributos (impositivos y de los recursos de la seguridad social) en el régimen general de acuerdo con tu actividad. |
| 817 | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEADetRequest&gt; / ImpTotal | El monto del comprobante emitido, excede el límite establecido para su categoría de Monotributo. Tenelo en cuenta para la próxima recategorización. |
| 818 | &lt;FECAEADetRequest&gt; / ImpTotal / &lt;CbteAsoc&gt; | El importe de la nota de crédito supera el monto del comprobante asociado que estás ajustando. Verificá los montos ingresados y de tratarse de un error, tenés que efectuar el ajuste o anulación de la operación según corresponda. |
| 819 | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | La CUIT receptora ingresada no existe. |
| 821 | &lt;FECAEADetRequest&gt; / &lt;CanMisMonExt&gt; | Si informa el campo MonCotiz, el mismo no podrá superar en 1 a la cotizacion oficial. Ver Método FEParamGetCotizacion. |
| 822 | &lt;FECAEADetRequest&gt; / &lt;CanMisMonExt&gt; | Si informa MonId = PES, el campo CanMisMonExt no debe informarse o informarse con el valor N. |
| 824 | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | El campo de identificación de Condición de IVA del receptor no es valido para la clase de comprobante informado. Para mas detalle consultar el Método: FEParamGetCondicionIvaReceptor |
| 825 | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | El campo Condición Frente al IVA del receptor resultará obligatorio conforme lo reglamentado por la Resolución General N° 5616. Para mas información consular método FEParamGetCondicionIvaReceptor. **[v4.8: queda en desuso]** |
| 827 | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | La CUIT receptora informada está inactiva o es inválida. Se bebe emitir una Nota de Crédito o anular la operación, según corresponda. |
| 828 | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | La CUIT receptora se encuentra limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. Se bebe emitir una Nota de Crédito o anular la operación, según corresponda. |
| 829 | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | El número de documento informado para el sujeto receptor corresponde a un sujeto fallecido, sin sucesión indivisa registrada. Se debe emitir una Nota de Crédito o anular la operación, según corresponda. |

#### FECAEARegInformativo · Verificaciones que se realizan sobre el elemento &lt;CbtesAsoc&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 160–161.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 800 | CbtesAsoc | Si envía CbtesAsoc, CbteAsoc es obligatorio y no debe estar vacío. |
| 802 | PtoVta | De enviarse el tag CbtesAsoc, CbteAsoc debe enviarse con PtoVta mayor a 0 y &lt; a 99999 |
| 803 | Nro | De enviarse el tag CbtesAsoc, CbteAsoc debe enviarse con Nro mayor a 0 y menor a 99999999 |
| 804 | Tipo / PtoVta / Nro | Los comprobantes informados no podrán repetirse. |
| 805 | Tipo | De enviarse el tag CbtesAsoc, CbteAsoc debe enviarse con Tipo mayor a 0 |
| 807 | CbteTipo / CbtesAsoc | CbtesAsoc es opcional, solamente podrá informarse si CbteTipo es igual a 1, 2, 3, 6, 7, 8, 12, 13, 51, 52, 53, 63, 64, 201, 202, 203, 206, 207, 208, 211, 212, 213. |
| 808 | &lt;CbteAsoc&gt;&lt;Cuit&gt; | Si informa Cuit en comprobantes asociados, no informar en blanco, el mismo debe ser un valor de 11 caracteres numericos. Para comprobante del tipo MiPyMEs (FCE) del tipo débito o crédito es obligatorio informar el campo. |
| 812 | CbteTipo / CbtesAsoc | Para comprobantes MiPyMEs (FCE) 201, 206 o 211 puede asociarse los comprobantes (91, 990, 991, 993, 994, 995). Para comprobantes MiPyMEs (FCE) A 202 y 203, puede asociar 201,202 o 203. Para comprobantes MiPyMEs (FCE) B 207, 208 puede asociar 206, 207, 208. Para comprobantes MiPyMEs (FCE) C 212, 213 puede asociarse 211, 212, 213. |
| 813 | &lt;Opcionales&gt;&lt;Id&gt; | Si intenta informar datos opcionales según Resolución General, recordar que en un mismo comprobante solo puede informar identificadores opcionales para solo 1 resolución por comprobante. |

#### FECAEARegInformativo · Verificaciones que se realizan sobre el elemento &lt;CbtesAsoc&gt; · NO EXCLUYENTES (observaciones)

Fuente: manual v4.7, págs. PDF 161–162.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 806 | Tipo | Obligatorio. Deberá ser igual a 1, 2, 3, 04, 05, 34, 39, 60, 63, 88 o 991 si el tipo de comprobante que se informa es igual a 2 ó 3. Deberá ser igual a 6, 7, 8, 09, 10, 35, 40, 61, 64 ,88 o 991 si el tipo de comprobante que se informa es igual a 7 ú 8. Deberá ser igual a 11, 12, 13, 15 si el tipo de comprobante que informa es igual a 12 o 13 Deberá ser igual a 51, 52, 53, 54 si el tipo de comprobante que se informa es igual a 52 o 53. Deberá ser 91, 88, 988, 990, 991, 993, 994, 995, 996, 997 si el tipo de comprobante que se informa es 1, 6 o 51 |
| 801 | Tipo/ PtoVta / Nro | Si el punto de venta del comprobante asociado (campo PtoVta de CbtesAsoc) es electrónico, el número de comprobante debe obrar en las bases del organismo para el punto de venta y tipo de comprobante informado. |
| 809 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | Si informa comprobantes asociados, y sus códigos son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, los mismos deben encontrarse registrados. |
| 810 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | Si informa comprobantes asociados, y sus códigos son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, los mismos deben encontrarse confirmados. |
| 811 | &lt;DocTipo&gt; / &lt;DocNro&gt; &lt;CbteAsoc&gt;&lt;Cuit&gt; | Si informa comprobantes asociados y sus códigos son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, el receptor del comprobante debe ser igual al receptor del comprobante asociado. |

#### FECAEARegInformativo · Controles que se realizan sobre el elemento &lt;Tributo&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 162.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 900 | Id | Obligatorio. Valores permitidos: consultar método FEParamGetTiposTributos |
| 908 | Desc | Opcional. Debe informarse si &lt;codigo&gt; es igual a 99. |
| 907 | Importe | El valor informado debe ser mayor o igual a 0. El campo Importe de Tributos soporta 13 números para la parte entera y 2 para los decimales. |
| 905 | BaseImp | El campo BaseImp en Tributo es obligatorio, mayor o igual 0 cero. El campo BaseImp de Tributos soporta 13 números para la parte entera y 2 para los decimales. |
| 906 | Alic | El campo Alic en Tributo es obligatorio, mayor o igual 0 cero. El campo Alic de Tributos soporta 3 números para la parte entera y 2 para los decimales. |

#### FECAEARegInformativo · Controles que se realizan sobre el elemento &lt;IVA&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 163.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 1000 | Id | Consultar el método FEParamGetTiposIva. Es opcional para comprobantes 2, 3, 7, 8. |
| 1003 | Id | El campo Id en AlicIVA no debe repetirse. Deberá totalizarse por alícuota. |
| 1008 | Importe | El campo Importe en AlicIVA es obligatorio , mayor o igual 0 cero. El campo Importe de AlicIva soporta 13 números para la parte entera y 2 para los decimales. |
| 1009 | BaseImp | El campo BaseImp en AlicIVA es obligatorio y debe ser mayor a 0 cero. Excepto para comprobantes 2, 3, 7, 8 que puede ser cero o no ser informado. El campo BaseImp de AlicIva soporta 13 números para la parte entera y 2 para los decimales. |

#### FECAEARegInformativo · Controles que se realizan sobre el elemento &lt;IVA&gt; · NO EXCLUYENTES (observaciones)

Fuente: manual v4.7, págs. PDF 163.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 1006 | Importe / AlicIva / BaseImp | Los importes informados en AlicIVA no se corresponden con los porcentajes. Excepto para comprobantes 2, 3, 7, 8 que puede ser cero o no ser informado. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 |

#### FECAEARegInformativo · Controles que se realizan sobre el elemento &lt;Opcionales&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 164.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 1100 | Id | El campo Id en Opcionales es obligatorio y debe ser igual a 2 (Régimen de Promoción Industrial). |
| 1101 | Id | El campo Id en Opcionales es obligatorio y no debe repetirse. |
| 1105 | Valor | El campo Valor en Opcionales es obligatorio. |
| 1103 | &lt;Opcionales&gt;&lt;Opcional&gt;&lt;Id&gt;&lt;Valor&gt; | Si envía Opcionales, Opcional, Id y Valor son obligatorios. |
| 1104 | Valor | Si selecciona Id = 2 el valor ingresado debe ser un numérico de 8 (ocho) dígitos mayor o igual a 0 (cero). |

#### FECAEARegInformativo · Controles que se realizan sobre el elemento &lt;Opcionales&gt; · NO EXCLUYENTES (observaciones)

Fuente: manual v4.7, págs. PDF 164.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 1106 | Valor | Si Id = 2 y el comprobante corresponde a una actividad alcanzada por el beneficio de Promoción Industrial en el campo &lt;Valor&gt; se deberá informar el número identificatorio del proyecto (el mismo deberá corresponder a la cuit emisora del comprobante), si no corresponde a una actividad alcanzada por el beneficio el campo &lt;Valor&gt; deberá ser 0 (cero). |

#### FECAEARegInformativo · Controles que se realizan sobre el elemento &lt;Actividades&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 164–165.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 16000 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt; | Si envía estructura de Actividades, Actividad es obligatorio enviarlo. |
| 16001 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | Si envía estructura de Actividades, Actividad es obligatorio enviarlo y no debe estar vacío. |
| 16002 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | El identificador de actividad informado tiene que ser una de las actividades habilitadas. Consultar método FEParamGetActividades. |
| 16003 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | De enviarse el tag Actividades, las actividades no deben repetirse. |
| 16004 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | De enviarse actividades, las mismas no deben corresponder a distintos grupos según RG. Es decir, si informa actividades Cárnicas, no pueden estar combinadas con actividades Harineras, de Tabaco, etc. |
| 16005 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | De enviarse actividades, las mismas deben encontrarse activas para el emisor del comprobante. Ver método FEParamGetActividades. |
| 16006 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; / &lt;Concepto&gt; | De enviarse actividades pertenecientes al grupo de actividades Cárnicas, las mismas deben enviarse con comprobantes con Concepto del tipo Producto o Productos y Servicios. |

#### FECAEARegInformativo · Controles que se realizan sobre el elemento &lt;Actividades&gt; y &lt;CbtesAsoc&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 165–166.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 16007 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si las actividades informadas corresponden a actividades Cárnicas, informar remito asociado 995 - Remito Electrónico Cárnico. |
| 16008 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si las actividades informadas corresponden a actividades Harinas, informar remito asociado 993 - Remito Electrónico Harinero - Automotor o 994 - Remito Electrónico Harinero - Ferroviario. |
| 16009 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si las actividades informadas corresponden a actividades Tabaco en Hebras, informar remito asociado 88 - Remito Electrónico de Tabaco Acondicionado. |
| 16010 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si informa remito 995 - Remito Electrónico Cárnico, es obligatorio informar una actividad Cárnica. |
| 16011 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si informa remito 993 - Remito Electrónico Harinero - Automotor o 994 - Remito Electrónico Harinero – Ferroviario, es obligatgorio informar una actividad Harinera. |
| 16012 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | Si informa remito 88 - Remito Electrónico de Tabaco Acondicionado., es obligatorio informar una actividad correspondiente a Tabaco Acondicionado. |
| 16013 | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc | Si informa actividades indicadas en la RG, es obligatorio informar comprobantes asociados. |
| 16014 | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | Si informa comprobantes asociados, y sus códigos corresponden a 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, los mismos no deben encontrarse asociado a otro comprobante. |

### 4.3. FECAEASolicitar


#### FECAEASolicitar · Controles aplicados al elemento &lt;FeCAEAReq&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 94.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 15000 | &lt;Cuit&gt; | Campo CUIT: Debe encontrarse activa en el Sistema Registral. |
| 15001 | &lt;Cuit&gt; | Campo CUIT: Deberá estar registrado como Autoimpresor. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. **[fuera de vigencia desde 01/06/2026 según el propio texto; historial v4.3: "quedará en desuso"]** |
| 15003 | &lt;Cuit&gt; | Campo CUIT: Deberá poseer al menos un punto de venta activo correspondiente al régimen CAEA |
| 15004 | &lt;Periodo&gt; | Campo Periodo: Debe tener el formato AAAAMM, donde AAAA indica el año y MM el mes en números. |
| 15005 | &lt;Orden&gt; | Campo Orden: Debe ser igual a 1 ó 2. |
| 15006 | Fecha de envío | Al momento de solicitar CAEA, la fecha de envío podrá ser desde 5 (cinco) días corridos anteriores al inicio de cada quincena hasta el último día de la misma quincena. |
| 15008 | &lt;Periodo&gt; / &lt;Orden&gt; | No debe existir un CAEA otorgado para la CUIT solicitante con igual periodo y orden. |
| 15009 | &lt;Cuit&gt; | Campo CUIT: Registra problemas de domicilio |
| 15010 | &lt;Cuit&gt; | Campo CUIT: Deberá estar inscripto en alguno de los sig. impuestos: 20 - MONOTRIBUTO 30 - IVA 32 - IVA EXENTO |
| 15011 | &lt;Cuit&gt; | Campo CUIT: Deberá tener al menos una actividad económica declarada |
| 15012 | &lt;Cuit&gt; | Campo CUIT: Deberá estar empadronado en el régimen de emisión de comprobantes electrónicos |
| 15016 | &lt;Cuit&gt; | Se recuerda que según la RG 5782/2025 el régimen de CAEA se aplica exclusivamente a situaciones de contingencia, motivo por el cual solo se permitirá su uso en domicilios que cuenten con al menos un punto de venta activo bajo la modalidad CAE o Controlador Fiscal de Nueva Tecnología como modalidad principal. |

#### FECAEASolicitar · Controles aplicados al elemento &lt;FeCAEAReq&gt; · NO EXCLUYENTES (observaciones)

Fuente: manual v4.7, págs. PDF 94–95.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 15014 | &lt;Cuit&gt;/ &lt;Periodo&gt; / &lt;Orden&gt; | El contribuyente registra incumplimientos en la rendición del régimen CAEA. La CUIT adeuda la presentación de 2 quincenas consecutivas o 4 alternadas. Retornará el listado de las rendiciones pendientes con el siguiente formato "periodo;orden;puntoVenta". |
| 15015 | &lt;Cuit&gt; | Campo CUIT: Registra problemas en el domicilio fiscal electrónico. No se encuentra adherido. |
| 15017 | &lt;Cuit&gt; | Existen Puntos de Venta CAEA que no comparten domicilio con algún Punto de Venta CAE o Controlador Fiscal que actúe como modalidad principal. Se detallara un listado de puntos de venta CAEA sin vinculación con un domicilio de factura electrónica (CAE o Controlador Fiscal de Nueva Tecnología). |
| 15018 | &lt;Cuit&gt; / CbteFchHsGen | Se recuerda que según la RG 5782/2025 el régimen de CAEA resulta aplicable exclusivamente como modalidad de emisión ante contingencias y las operaciones realizadas deberán ser informadas, con indicación de la fecha y hora de generación de los comprobantes emitidos. |
| 1527 | &lt;DocTipo&gt; / &lt;DocNro&gt; / &lt;Tributo&gt; | Falta informar en la sección Otros Tributos el tributo con ID = 13 – Percepción de IVA No Categorizado según la RG 2126/2006. Si &lt;CbteTipo&gt; es igual a 6, 7 u 8, &lt;DocTipo&gt; es 80 (CUIT), &lt;DocNro&gt; es 23000000000 (No Categorizado), la suma de ImpNeto + ImpIVA es mayor a 0 (cero), y el campo ImpTrib &gt; 0, debe informar el ID “13 – Percepción de IVA No Categorizado“ en el array de tributos con un importe mayor a 0 (cero) **[el manual lo lista en la tabla de FECAEASolicitar, pero por su contenido y por el historial v4.7 ("1527 para CAEA") aplica a FECAEARegInformativo]** |

### 4.4. FECAEAConsultar


#### FECAEAConsultar · Controles aplicados al objeto &lt;FECAEAConsultar&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 100.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 15004 | &lt;Periodo&gt; | El valor indicado en el campo &lt;Periodo&gt; es obligatorio.. Debe tener formato AAAAMM, donde AAAA indica el año y MM el mes en números. |
| 15005 | &lt;Orden&gt; | El valor indicado en el campo &lt;Orden&gt; es obligatorio. Valores permitidos 1 o 2. |

### 4.5. FECAEASinMovimientoInformar


#### FECAEASinMovimientoInformar · Controles aplicados · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 124–125.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 1200 | &lt;CAEA&gt; | El código de CAEA que se está informando debe ser del tipo de código de autorización CAEA |
| 1201 | &lt;CUIT&gt; | Corresponda a la CUIT del Emisor indicada en &lt;Auth&gt;&lt;Cuit&gt; |
| 1202 | &lt;CAEA&gt; / &lt;PtoVta&gt; | Que el CAEA / PtoVta no esté informado como utilizado en algún comprobante |
| 1203 | Fecha de envío de la solicitud | La fecha de envío de la solicitud debe ser mayor a la fecha de inicio de vigencia del CAEA que se está informando. |
| 1204 | &lt;PtoVta&gt; | El PtoVta debe corresponder a un punto de venta habilitado para el régimen CAEA |
| 1205 | &lt;PtoVta&gt; | El punto de venta deberá haber estado activo durante la vigencia del CAEA |
| 1206 | &lt;PtoVta&gt; | El punto de venta deberá haber estar comprendido entre 1 y 99998 |
| 1207 | &lt;CAEA&gt; | CAEA y formato válido |
| 1209 | PtoVta | El punto de venta informado como sin movimiento ya fue notificado |

### 4.6. FECAEASinMovimientoConsultar


#### FECAEASinMovimientoConsultar · Controles aplicados · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 190.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10100 | CAEA | No ingreso el CAEA o el formato es inválido. |
| 10101 | PtoVta | No ingreso el Punto de Venta o el formato es inválido. |
| 10102 | CAEA | El CAEA informado no se encuentra registrado en las bases de la Administración como sin movimientos. |
| 10105 | CAEA / PtoVta | El punto de venta ingresado registra comprobantes informados |

### 4.7. FECompUltimoAutorizado


#### FECompUltimoAutorizado · Controles aplicados · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 128.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 11000 | &lt;PtoVta&gt; | El PtoVta debe ser válido comprendido entre 1 y 99998 |
| 11001 | &lt;CbteTipo&gt; | Debe de ser algunos de los habilitados en este WS. Consultar método FEParamGetTiposCbte |
| 11002 | &lt;PtoVta&gt; | Debe ser un punto de venta habilitado en este WS. Consultar método FEParamGetPtosVenta |

### 4.8. FECompConsultar


#### FECompConsultar · Controles aplicados · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 194.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10200 | PtoVta | No ingreso el Punto de Venta o el formato es inválido. |
| 10201 | CbteTipo | No ingreso el Tipo de Comprobante, o el tipo de comprobante es inválido. |
| 10104 | PtoVta | El punto de venta ingresado no se encuentra registrado. |
| 10202 | CbteNro | No ingreso el número de comprobante o el formato es inválido. |

### 4.9. FEParamGetCotizacion


#### FEParamGetCotizacion · Validaciones que se aplican sobre el objeto &lt;FEParamGetCotizacion&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 122.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 12000 | MonId | Campo &lt;MonId&gt; debe ser algunos de los habilitados en el presente WS. Para consultar los valores posible utilizar el método FEParamGetTiposMonedas |
| 12001 | MonId | Campo &lt;MonId&gt; es obligatorio ingresarlo. |
| 12002 | FchCotiz | Campo FchCotiz No corresponde a una fecha valida con formato YYYYMMDD |

### 4.10. FEParamGetCondicionIvaReceptor


#### FEParamGetCondicionIvaReceptor · Validaciones que se aplican sobre el objeto &lt;FEParamGetCondicionIvaReceptor&gt; · EXCLUYENTES

Fuente: manual v4.7, págs. PDF 202.

| Código | Campo / grupo | Condición (texto literal del manual) |
|---:|---|---|
| 10244 | Cmp_Clase | El valor del campo &lt;Cmp_Clase&gt; ingresado para la clase de comprobante no es valido. La clase de Comprobante es opcional, de ingresar un valor solo puede ser A, B, C, ALEY (A con leyenda ‘operación sujeta a retención’) o 49 (Bienes Usados), |

## 5. Mensajes (`Msg`) literales conocidos

Estos son los únicos textos de `Msg` que se pudieron ver devueltos por el servicio (o reproducidos en ejemplos del manual).
Para todo otro código el `Msg` real **no se pudo verificar**.

### 5.1 Vistos en respuestas reales del servicio de homologación

| Código | Ubicación | `Msg` literal | Fuente |
|---:|---|---|---|
| 500 | `Errors` | `Campo Auth no fue ingresado o esta mal formado.` | En vivo 2026-10-01 |
| 600 | `Errors` | ver §2 (varias variantes `ValidacionDeToken: ...`) | En vivo 2026-10-01 |
| 602 | `Errors` | `No existen datos en nuestros registros para los parametros ingresados.` | Cassettes PyAfipWs (2021) |
| 10016 | `Errors` | `El numero o fecha del comprobante no se corresponde con el proximo a autorizar. Consultar metodo FECompUltimoAutorizado.` | Cassettes PyAfipWs (2021): `test_main_prueba.yaml`, `test_reproceso_*.yaml`, `test_main_prueba_multiple.yaml` |
| 10016 | `Obs` | `Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N+5 siendo N la fecha de envio del pedido  de autorizacion para 1 - Productos` | Cassette `test_main_prueba_usados.yaml` (2021) |
| 10016 | `Obs` | `Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N siendo N la fecha de envio del pedido de autorizacion para Facturas de Credito del tipo Nota de Debito o Nota de Credito` | Cassette `test_main_prueba_fce.yaml` (2021) |
| 10217 | `Obs` (con `Resultado=A`) | `El credito fiscal discriminado en el presente comprobante solo podra ser computado a efectos del Procedimiento permanente de transicion al Regimen General.` | Cassettes `test_autorizar_comprobante.yaml`, `test_consulta.yaml` (2021) |
| 782 | `Errors` (FECAEARegInformativo) | `El &lt;CAEA&gt; es obligatorio informarlo.` | Cassette `test_main_prueba_caea.yaml` (2021) |
| 15004 | `Errors` (FECAEAConsultar) | `El campo &lt;Periodo&gt; debe tener el formato AAAAMM, donde AAAA indica el año y MM el mes en números.` | Cassette `test_main_prueba_caea.yaml` (2021) |
| 15006 | `Errors` (FECAEASolicitar) | `Fecha de envío podrá ser desde 5 días corridos anteriores al inicio hasta el último dia de cada quincena. Del 6/26/2021 hasta 7/15/2021` (fechas en formato M/d/yyyy) | Cassette `test_main_solicitar_caea.yaml` (2021) |
| 1200 | `Errors` (FECAEASinMovimientoInformar) | `El codigo de autorizacion debe ser del tipo CAEA` | Cassette `test_main_sinmovimiento_caea.yaml` (2021) |

Observaciones sobre el estilo real de los mensajes: sin tildes ("numero", "metodo", "autorizacion"), con dobles espacios
ocasionales, con `<` `>` escapados como entidades dentro de `Msg`, y a veces con valores interpolados
("Informado: 2, Enviado:3", "Del 6/26/2021 hasta 7/15/2021").

### 5.2 Mostrados en los ejemplos del manual v4.7

Los ejemplos del manual son antiguos (2010–2013) y en varios casos **inconsistentes** con las tablas (p. ej. usa 10030 para
"Cuit no registrada"). Se listan sólo como referencia de formato.

| Código | `Msg` en el ejemplo | Pág. PDF | Comentario |
|---:|---|---:|---|
| 10030 | `Cuit 10222222222 no registrada en padrón arca` | 82 | Inconsistente: 10030 en la tabla es "Concepto" |
| 10016 | `comp. 4 no coincide con el próximo a autorizar` | 82 | Como `Obs` |
| 10002 | `No coincide la cantidad de registros informadas con la cantidad real enviada` | 84 | Como `Err` |
| 1005 | `El punto de venta no se encuentra empadronado` | 84 | Código de 4 dígitos que no existe en ninguna tabla; probable errata de 10005 |
| 10030 | `Para comprobantes de Bienes Usados, Concepto debe ser igual a 1 – PRODUCTOS` | 86 | Según tabla correspondería 10085 |
| 10076 | `Si el comprobante es CbteTipo = 49 (Bienes Usados), es obligatorio informar opcionales. Ver método FEParamGetTiposOpcional()` | 86 | |
| 15015 | ` Registra problemas en el domicilio fiscal electrónico. No se encuentra adherido ` | 97 | |
| 600 | `ValidacionDeToken: No apareció CUIT en lista de relaciones: 23000000000` | 170 | |
| 700 | `Campo CbteTipo no se corresponde con alguno de los habilitados 1, 2 ,3, 6, 7 u 8.` | 172 | Como `Err` (rechazo de cabecera) |
| 713 | `El campo Concepto es obligatorio y debe corresponder con algún valor devuelto por el método FEParamGetTiposConcepto` | 173 | Como `Obs` con `Resultado=R` |
| 724 | `El campo 'Importe Total' ImpTotal, debe ser igual a la suma de ImpTotConc + ImpNeto + ImpOpEx + ImpTrib + ImpIVA.` | 175 | Como `Obs` con `Resultado=A` |
| 900 | `Si ImpTrib es mayor a 0 el objeto Tributos y Tributo son obligatorios.` | 180 | Inconsistente: la tabla asigna ese texto a 1423 |
| 10002 | `Campo CantReg debe ser igual a lo informado en detalle. Informado: 2, Enviado:3` | 183 | Como `Err`, todos los detalles con `Resultado=R` |

## 6. Eventos

El manual define la estructura `Events/Evt{Code,Msg}` (pág. PDF 21-22) pero no publica ningún código ni texto de evento.
En las llamadas en vivo y en los intercambios grabados revisados (cassettes PyAfipWs, julio 2021) no apareció ningún `Evt`. **No verificado**: qué
eventos emite hoy el servicio y con qué códigos. Un simulador debería permitir inyectar eventos configurables.
