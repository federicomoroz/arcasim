# wsmtxca

**Factura electrónica con detalle de ítems (RG 2904, "Codificación de Productos – opción Factura con Detalle").** Autoriza comprobantes A y B (y los de Factura de Crédito Electrónica MiPyME 201 a 208) **con sus ítems**, uno por llamada, y devuelve un CAE. También maneja el régimen CAEA: pedir el código por quincena, informar después cada comprobante emitido con él, y avisar los CAEA o puntos de venta que no se usaron. Tiene 27 operaciones, la mayoría consultas de parámetros.

Relevamiento hecho el 2026-10-02. Fuentes y marcas:

| Marca | Fuente |
|---|---|
| **[MAN]** | "Factura Electrónica – Web Service MTXCA – Manual para el desarrollador", versión 0.25.8 (última entrada del historial: 01/08/2026), 380 páginas. URL: `https://www.afip.gob.ar/fe/ayuda/documentos/wsmtxca-RG-2904.pdf` (bajado el 2026-10-02, HTTP 200, 6 429 087 bytes). **"pág. N" = página física del PDF.** El pie de página del manual dice "N-6 de 380" (la numeración interna arranca después del índice): la pág. PDF 37 lleva el pie "31 de 380". |
| **[WSDL]** | `docs/arca/wsdl/wsmtxca-homologacion.wsdl` (bajado el 2026-10-01, 76 912 bytes). El de producción (`https://serviciosjava.afip.gob.ar/wsmtxca/services/MTXCAService?wsdl`, bajado el 2026-10-02, 77 406 bytes) tiene **los mismos tipos, elementos y operaciones**: se compararon parseados y sólo cambian espacios y `soap:address`. El esquema está embebido (sin `import` ni `include`), así que no hay carpeta `wsdl/wsmtxca/`. |
| **[VIVO]** | Llamadas propias sin credenciales a homologación y producción el 2026-10-02. |
| **[SEC]** | PyAfipWs (`https://github.com/PyAr/pyafipws/blob/main/wsmtx.py`), sólo para huecos del manual. |

Ojo con el WSDL de homologación **hoy**: el 2026-10-02, tres `GET https://fwshomo.afip.gov.ar/wsmtxca/services/MTXCAService?wsdl` seguidos devolvieron HTTP 200 con un WSDL de 31 144 bytes cuyo `wsdl:types` tiene un esquema casi vacío (sólo un `authRequest` suelto), con los 54 `message` y las 27 operaciones intactos. El archivo guardado el día anterior está completo. Un cliente que genere código desde el WSDL de homologación ese día obtiene tipos rotos. Si es intermitente o permanente: NO VERIFICADO.

Cuando WSDL y manual no coinciden, manda el WSDL.

## Contrato

| Aspecto | Valor | Fuente |
|---|---|---|
| Dialecto | Java, **Apache Axis2** (el fault de operación inexistente es el típico de Axis2: "The endpoint reference (EPR) for the Operation not found ... WSA Action"). Errores excepcionales como `soapenv:Fault` con HTTP 500; errores de negocio dentro de la respuesta (`arrayErrores`) | [VIVO] [MAN pág. 9-12] |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsmtxca/services/MTXCAService` | [WSDL] [MAN pág. 14] |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsmtxca/services/MTXCAService` (**`.gob.ar`**) | [WSDL prod] [MAN pág. 14] |
| Namespace | `http://impl.service.wsmtxca.afip.gov.ar/service/` (**`.gov.ar`**, el del WSDL). Los ejemplos del manual usan `http://impl.service.wsmtxca.afip.gob.ar/service/` (**`.gob.ar`**). Ver abajo | [WSDL] [MAN] |
| Forma de los elementos | El esquema **no declara `elementFormDefault`**, así que vale `unqualified`: sólo el elemento raíz del Body lleva namespace (`ser:autorizarComprobanteRequest`); los hijos (`authRequest`, `token`, `comprobanteCAERequest`...) van **sin namespace**, como en los ejemplos del manual. La respuesta igual: `ns1:...Response` con `xmlns:ns1` y los hijos sin prefijo | [WSDL] [VIVO] |
| Archivo WSDL | `docs/arca/wsdl/wsmtxca-homologacion.wsdl` | — |
| WSAA service id | `wsmtxca`. **El manual no lo dice** (pág. 16-18 sólo explica `authRequest`). Sale de [SEC] (`WSAA().Autenticar("wsmtxca", ...)`). NO VERIFICADO en fuente oficial | [SEC] |
| SOAPAction | `http://impl.service.wsmtxca.afip.gov.ar/service/<operación>` (27). Axis2 rutea **primero por `SOAPAction`**: con `SOAPAction` de `dummy` y cualquier body (incluso XML cortado) responde el dummy. Con `SOAPAction` vacío o sin el header, rutea por el elemento raíz del body | [WSDL] [VIVO] |
| SOAP 1.1 / 1.2 | El WSDL sólo tiene el binding `MTXCAServiceSoap11Binding` (port `MTXCAServiceHttpSoap11Endpoint`). Pero en vivo (producción) un `dummy` por SOAP 1.2 respondió 200 `application/soap+xml; action="http://impl.service.wsmtxca.afip.gov.ar/service/MTXCAServicePortType/dummyResponse";charset=utf-8` con envelope 1.2 | [WSDL] [VIVO] |
| Content-Type de respuesta | `text/xml;charset=utf-8` (sin espacio), `<?xml version='1.0' encoding='utf-8'?>` con comillas simples, todo en una línea | [VIVO] |
| Fault declarado | Todas las operaciones declaran `wsdl:fault name="exception"` con elemento `exceptionResponse{exception: string 0..1}`. En vivo el `detail` vino vacío (`<detail />`) | [WSDL] [VIVO] |
| Operaciones | 27 | [WSDL] |

**La trampa del namespace (`.gob.ar` vs `.gov.ar`).** El WSDL declara `.gov.ar`; todos los ejemplos del manual, `.gob.ar`. En vivo (2026-10-02):

- `dummy` con el namespace `.gob.ar` responde OK (homologación y producción), pero eso no prueba nada: el dummy rutea por `SOAPAction` e ignora el body.
- `consultarTiposComprobante` con raíz `.gob.ar` y token falso, en producción, llega hasta la validación del token (`Token inválido`), igual que con `.gov.ar`. O sea, Axis2 aceptó el elemento raíz con el namespace del manual.
- Con los hijos calificados (`ser:authRequest`, `ser:token`, contra el esquema `unqualified`) también llega a `Token inválido`, no a "token o firma nulos". O sea, también los leyó.
- La respuesta siempre sale con `.gov.ar`.

Para el simulador: aceptar los dos namespaces en el elemento raíz, aceptar hijos calificados o no, y responder siempre `.gov.ar`. Si con un token válido el servicio procesa igual un request `.gob.ar`: NO VERIFICADO.

## Autenticación

Todas las operaciones menos `dummy` llevan `authRequest` (`AuthRequestType`) como primer hijo (pág. 16-18):

| Campo | Tipo | Ocurr. | Significado |
|---|---|---|---|
| `token` | `string` | 1..1 | Token del TA de WSAA |
| `sign` | `string` | 1..1 | Firma del TA |
| `cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada |

"Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional" (pág. 18). Error excepcional = `soapenv:Fault`, `faultcode` `soapenv:Client`. "Los errores excepcionales incluyen también errores de estructura (ej: tags sin cerrar, con nombres incorrectos o en orden incorrecto) y de tipos de datos" (pág. 9).

**Producción, respuestas reales** (2026-10-02). Todas **HTTP 500**, `Content-Type: text/xml;charset=utf-8`, cuerpo `<?xml version='1.0' encoding='utf-8'?><soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"><soapenv:Body><soapenv:Fault><faultcode>soapenv:Client</faultcode><faultstring>…</faultstring><detail /></soapenv:Fault></soapenv:Body></soapenv:Envelope>`:

| Caso | `faultstring` literal |
|---|---|
| Sin `authRequest` (`<ser:consultarTiposComprobanteRequest/>`) | `Acceso Denegado  - El token o la firma son nulos.` (dos espacios antes del guion) |
| `token`=`abc`, o `token` y `sign` vacíos, o `cuitRepresentada`=`abc` | `Token inválido` (con tilde, UTF-8) |
| Token base64 de un `<sso>` bien formado con `exp_time` vencido y firma falsa | `Token vencido Fecha y Hora de Vencimiento del Token Enviado: 02-10-2025 19:13:20 - Fecha y Hora Actual del Servidor: 02-10-2026 15:10:53`. Formato `dd-MM-yyyy HH:mm:ss`, hora de Argentina (el `exp_time` enviado fue 1759443200 = 2025-10-02 22:13:20 UTC). **Las fechas se validan antes que la firma** (al revés que en los servicios ASMX) |
| Mismo token con fechas futuras y firma falsa | `La firma no corresponde al token enviado.` |
| XML cortado | `Acceso Denegado  - com.ctc.wstx.exc.WstxEOFException: Unexpected EOF; was expecting a close tag for element &lt;authRequest>`, salto de línea, ` at [row,col {unknown-source}]: [1,253].` |
| `SOAPAction` inexistente | `soapenv:Fault xmlns:axis2ns621="http://schemas.xmlsoap.org/soap/envelope/"`, `faultcode` `axis2ns621:Client`, `faultstring` `The endpoint reference (EPR) for the Operation not found is https://serviciosjava.afip.gob.ar/wsmtxca/services/MTXCAService and the WSA Action = http://impl.service.wsmtxca.afip.gov.ar/service/noexiste` (el número del prefijo `axis2nsNNN` varía) |

El ejemplo del manual (pág. 9) es el mismo "Token vencido" con fechas de 2010. Los textos para CUIT fuera de las relaciones del token: NO VERIFICADO.

**Homologación, respuestas reales** (2026-10-02): **cualquier** respuesta que no sea el dummy (token falso, sin `authRequest`, `SOAPAction` inexistente, `GET` al endpoint) llegó como:

```
HTTP/1.0 200 OK
Connection: Keep-Alive
Content-Length: 39

BL4988006257912 2026-10-02 15:09:57 500
```

Sin `Content-Type`, sin SOAP: `BL` + un número de 11 a 13 dígitos que cambia en cada llamada + fecha y hora local + `500`. Comparando con producción, donde los mismos requests dan HTTP 500 con `soapenv:Fault`, lo más probable es que el balanceador de homologación (F5, se ven sus cookies en las respuestas buenas) **reemplace toda respuesta HTTP 500 del backend** por esa línea. Es una inferencia: NO VERIFICADO. Consecuencia práctica: en homologación un cliente no puede leer el `faultstring` de ningún error excepcional. Para ArcaSim conviene simular el comportamiento de producción y, como opción configurable, el de homologación.

## Operaciones

Orden de los elementos = orden del esquema ([WSDL]). Las respuestas de negocio usan:

- `arrayErrores/codigoDescripcion{codigo: short, descripcion: string}`: motivos de rechazo.
- `arrayObservaciones/codigoDescripcion{codigo, descripcion}`: aprobado con observaciones.
- `evento{codigo, descripcion}` (uno solo, 0..1): "anuncio informativo del sistema" (pág. 12).

Tipos simples ([WSDL], que coincide con la pág. 338 del manual salvo la errata del máximo de `ImporteSubtotalSimpleType`):

| Tipo | Base | Restricción |
|---|---|---|
| `NumeroPuntoVentaSimpleType` | int | 1 a 99998 |
| `NumeroComprobanteSimpleType` | long | 1 a 99999999 |
| `SiNoSimpleType` | string | `S`, `N` |
| `CodigoTipoAutorizacionSimpleType` | string | `A` (CAEA), `E` (CAE) |
| `ResultadoSimpleType` | string | `A`, `O`, `R` |
| `ImporteSubtotalSimpleType` | decimal | -9999999999999.99 a 9999999999999.99, 15 dígitos, 2 decimales |
| `ImporteTotalSimpleType` | decimal | 0 a 9999999999999.99, 15 dígitos, 2 decimales |
| `DecimalSimpleType` | decimal | 0 a 999999999999.999999, 18 dígitos, 6 decimales |
| `PorcentajeSimpleType` | decimal | 0 a 100, 5 dígitos, 2 decimales |

Si un valor viola estas restricciones, ¿lo rechaza Axis2 como error de esquema (fault) o pasa a las validaciones de negocio? NO VERIFICADO (Axis2 ADB no suele validar facetas).

### autorizarComprobante

Autoriza un comprobante CAE con ítems (pág. 21-72). Resultado: aprobado (`A`, con CAE), aprobado con observaciones (`O`, con CAE) o rechazado (`R`, sin CAE).

Request:

Elemento raíz: `autorizarComprobanteRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `comprobanteCAERequest` | `ComprobanteType` | 1..1 | Comprobante (`ComprobanteType`, pág. 341-354) |
| `comprobanteCAERequest/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante (3). Ver `consultarTiposComprobante` |
| `comprobanteCAERequest/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |
| `comprobanteCAERequest/numeroComprobante` | `NumeroComprobanteSimpleType` | 1..1 | Número de comprobante (8) |
| `comprobanteCAERequest/fechaEmision` | `date` | 0..1 | Fecha de emisión `yyyy-mm-dd`. Opcional: si falta, la de proceso (103) |
| `comprobanteCAERequest/codigoTipoAutorizacion` | `CodigoTipoAutorizacionSimpleType` | 0..1 | `E` CAE, `A` CAEA. No se informa al autorizar (105) |
| `comprobanteCAERequest/codigoAutorizacion` | `long` | 0..1 | CAE o CAEA (14). No se informa al autorizar (106) |
| `comprobanteCAERequest/fechaVencimiento` | `date` | 0..1 | Vencimiento del código de autorización. No se informa al autorizar (107) |
| `comprobanteCAERequest/codigoTipoDocumento` | `short` | 0..1 | Tipo de documento del receptor (2). Ver `consultarTiposDocumento` |
| `comprobanteCAERequest/numeroDocumento` | `long` | 0..1 | Número de documento del receptor (11) |
| `comprobanteCAERequest/condicionIVAReceptor` | `short` | 0..1 | Condición frente al IVA del receptor (2). Ver `consultarCondicionesIVAReceptor` (RG 5616) |
| `comprobanteCAERequest/importeGravado` | `ImporteSubtotalSimpleType` | 0..1 | Neto gravado (15.2) (110) |
| `comprobanteCAERequest/importeNoGravado` | `ImporteSubtotalSimpleType` | 0..1 | No gravado (15.2) (111) |
| `comprobanteCAERequest/importeExento` | `ImporteSubtotalSimpleType` | 0..1 | Exento (15.2) (112) |
| `comprobanteCAERequest/importeSubtotal` | `ImporteSubtotalSimpleType` | 1..1 | Subtotal = no gravado + gravado + exento (15.2) (113) |
| `comprobanteCAERequest/importeOtrosTributos` | `ImporteTotalSimpleType` | 0..1 | Total de otros tributos (15.2) (114) |
| `comprobanteCAERequest/importeTotal` | `ImporteTotalSimpleType` | 1..1 | Total (15.2) (115, 116) |
| `comprobanteCAERequest/codigoMoneda` | `string` | 1..1 | Moneda (3). Ver `consultarMonedas` (117) |
| `comprobanteCAERequest/cotizacionMoneda` | `decimal` | 0..1 | Cotización, 4 enteros y 6 decimales, > 0. 1 si PES (119, 120, 192, 194, 195) |
| `comprobanteCAERequest/cancelaEnMismaMonedaExtranjera` | `SiNoSimpleType` | 0..1 | `S`, `N` o vacío. Sólo facturas 1, 6, 51, 201, 206 (118, 164, 169) |
| `comprobanteCAERequest/observaciones` | `string` | 0..1 | Observaciones comerciales (2000) |
| `comprobanteCAERequest/codigoConcepto` | `short` | 1..1 | 1 Productos, 2 Servicios, 3 Productos y Servicios (121) |
| `comprobanteCAERequest/fechaServicioDesde` | `date` | 0..1 | Sólo con concepto 2 o 3 (122) |
| `comprobanteCAERequest/fechaServicioHasta` | `date` | 0..1 | Sólo con concepto 2 o 3 (123) |
| `comprobanteCAERequest/fechaVencimientoPago` | `date` | 0..1 | Sólo con concepto 2 o 3 (124); obligatoria en FCE 201/206 (148) |
| `comprobanteCAERequest/fechaHoraGen` | `dateTime` | 0..1 | Fecha y hora de generación `AAAA-MM-DDTHH:MM:SS`. Sólo CAEA (146); obligatoria en CAEA desde 0.25.7 |
| `comprobanteCAERequest/arrayComprobantesAsociados` | `ArrayComprobantesAsociadosType` | 0..1 | Comprobantes asociados (validaciones 200-225) |
| `comprobanteCAERequest/arrayComprobantesAsociados/comprobanteAsociado` | `ComprobanteAsociadoType` | 1..n | Un comprobante asociado |
| `comprobanteCAERequest/arrayComprobantesAsociados/comprobanteAsociado/codigoTipoComprobante` | `short` | 1..1 | Tipo del asociado (3) (200, 203, 208) |
| `comprobanteCAERequest/arrayComprobantesAsociados/comprobanteAsociado/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |
| `comprobanteCAERequest/arrayComprobantesAsociados/comprobanteAsociado/numeroComprobante` | `NumeroComprobanteSimpleType` | 1..1 | Número de comprobante (8) |
| `comprobanteCAERequest/arrayComprobantesAsociados/comprobanteAsociado/cuit` | `long` | 0..1 | CUIT del emisor del asociado (11). Sólo para remitos de terceros 88/990 y FCE (204, 209, 210) |
| `comprobanteCAERequest/arrayComprobantesAsociados/comprobanteAsociado/fechaEmision` | `date` | 0..1 | Fecha del asociado (218-225) |
| `comprobanteCAERequest/periodoComprobantesAsociados` | `PeriodoComprobantesAsociadosType` | 0..1 | Período de los asociados, en vez de comprobantes puntuales (160-162, 2200-2202) |
| `comprobanteCAERequest/periodoComprobantesAsociados/fechaDesde` | `date` | 1..1 | Desde |
| `comprobanteCAERequest/periodoComprobantesAsociados/fechaHasta` | `date` | 1..1 | Hasta |
| `comprobanteCAERequest/arrayOtrosTributos` | `ArrayOtrosTributosType` | 0..1 | Otros tributos (300-302) |
| `comprobanteCAERequest/arrayOtrosTributos/otroTributo` | `OtroTributoType` | 1..n | Un tributo |
| `comprobanteCAERequest/arrayOtrosTributos/otroTributo/codigo` | `short` | 1..1 | Código (2). Ver `consultarTiposTributo` (300) |
| `comprobanteCAERequest/arrayOtrosTributos/otroTributo/descripcion` | `string` | 0..1 | Descripción (25). Obligatoria si `codigo` = 99 (301) |
| `comprobanteCAERequest/arrayOtrosTributos/otroTributo/baseImponible` | `ImporteTotalSimpleType` | 1..1 | Base imponible (15.2) |
| `comprobanteCAERequest/arrayOtrosTributos/otroTributo/importe` | `ImporteTotalSimpleType` | 1..1 | Importe (15.2) |
| `comprobanteCAERequest/arrayItems` | `ArrayItemsType` | 1..1 | Ítems (obligatorio, al menos uno) |
| `comprobanteCAERequest/arrayItems/item` | `ItemType` | 1..n | Un ítem (validaciones 500-521) |
| `comprobanteCAERequest/arrayItems/item/unidadesMtx` | `int` | 0..1 | Unidades de consumo en la presentación (6). Obligatorio salvo unidades 97/99 (500-502) |
| `comprobanteCAERequest/arrayItems/item/codigoMtx` | `string` | 0..1 | GTIN 13, 12 u 8 (13). Obligatorio salvo 97/99 (503, 504) |
| `comprobanteCAERequest/arrayItems/item/codigo` | `string` | 0..1 | Código interno (50) (505) |
| `comprobanteCAERequest/arrayItems/item/descripcion` | `string` | 1..1 | Descripción (4000), obligatoria (506) |
| `comprobanteCAERequest/arrayItems/item/cantidad` | `DecimalSimpleType` | 0..1 | Cantidad (18.6). No va con 97/99 (507) |
| `comprobanteCAERequest/arrayItems/item/codigoUnidadMedida` | `short` | 1..1 | Unidad de medida (2). Ver `consultarUnidadesMedida` (508) |
| `comprobanteCAERequest/arrayItems/item/precioUnitario` | `DecimalSimpleType` | 0..1 | Precio unitario (18.6): sin IVA en clase A, con IVA en clase B. No va con 97/99 (509) |
| `comprobanteCAERequest/arrayItems/item/importeBonificacion` | `DecimalSimpleType` | 0..1 | Bonificación (18.6), ≤ precio × cantidad (510, 511) |
| `comprobanteCAERequest/arrayItems/item/codigoCondicionIVA` | `short` | 1..1 | Condición de IVA del ítem (2). Ver `consultarCondicionesIVA` (512) |
| `comprobanteCAERequest/arrayItems/item/importeIVA` | `ImporteSubtotalSimpleType` | 0..1 | IVA del ítem (15.2). Obligatorio en clase A, no va en clase B (514-517, 521) |
| `comprobanteCAERequest/arrayItems/item/importeItem` | `ImporteSubtotalSimpleType` | 1..1 | Total del ítem (15.2) (518, 519) |
| `comprobanteCAERequest/arraySubtotalesIVA` | `ArraySubtotalesIVAType` | 0..1 | Subtotales por alícuota. Obligatorio si hay ítems con condición 4, 5 o 6 (127) |
| `comprobanteCAERequest/arraySubtotalesIVA/subtotalIVA` | `SubtotalIVAType` | 1..n | Un subtotal |
| `comprobanteCAERequest/arraySubtotalesIVA/subtotalIVA/codigo` | `short` | 1..1 | Alícuota: 4, 5 o 6 (400). Sin repetir (402) |
| `comprobanteCAERequest/arraySubtotalesIVA/subtotalIVA/importe` | `ImporteSubtotalSimpleType` | 1..1 | IVA liquidado de esa alícuota (15.2) (401, 405) |
| `comprobanteCAERequest/arrayDatosAdicionales` | `ArrayDatosAdicionalesType` | 0..1 | Datos adicionales. Uno solo por comprobante (322) |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional` | `DatoAdicionalType` | 1..n | Un dato adicional |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional/t` | `short` | 1..1 | Tipo de dato adicional (4). Ver `consultarTiposDatosAdicionales` (320) |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional/c1` | `string` | 0..1 | Campo multipropósito 1 (50); su sentido depende de `t` |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional/c2` | `string` | 0..1 | Campo multipropósito 2 (50) |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional/c3` | `string` | 0..1 | Campo multipropósito 3 (50) |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional/c4` | `string` | 0..1 | Campo multipropósito 4 (50) |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional/c5` | `string` | 0..1 | Campo multipropósito 5 (50) |
| `comprobanteCAERequest/arrayDatosAdicionales/datoAdicional/c6` | `string` | 0..1 | Campo multipropósito 6 (50) |
| `comprobanteCAERequest/arrayCompradores` | `ArrayCompradoresType` | 0..1 | Compradores múltiples de bienes registrables; sólo concepto 1 y no en FCE (420-433) |
| `comprobanteCAERequest/arrayCompradores/comprador` | `CompradorType` | 1..n | Un comprador |
| `comprobanteCAERequest/arrayCompradores/comprador/codigoTipoDocumento` | `short` | 1..1 | CUIT, CUIL o CDI (422) |
| `comprobanteCAERequest/arrayCompradores/comprador/numeroDocumento` | `long` | 1..1 | Número (11) |
| `comprobanteCAERequest/arrayCompradores/comprador/porcentaje` | `PorcentajeSimpleType` | 1..1 | Porcentaje de titularidad, > 0 y < 100; la suma da 100 (424-427) |
| `comprobanteCAERequest/arrayActividades` | `ArrayActividadesType` | 0..1 | Actividades asociadas, de `consultarActividadesVigentes` (165-168) |
| `comprobanteCAERequest/arrayActividades/actividad` | `ActividadType` | 1..n | Una actividad |
| `comprobanteCAERequest/arrayActividades/actividad/codigo` | `long` | 1..1 | Código de actividad (6) |


Response:

Elemento raíz: `autorizarComprobanteResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `resultado` | `ResultadoSimpleType` | 1..1 | `A` aprobado, `O` observado, `R` rechazado |
| `comprobanteResponse` | `ComprobanteCAEResponseType` | 0..1 | Existe si fue aprobado (`ComprobanteCAEResponseType`) |
| `comprobanteResponse/cuit` | `long` | 1..1 | CUIT emisora (11) |
| `comprobanteResponse/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante (3). Ver `consultarTiposComprobante` |
| `comprobanteResponse/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |
| `comprobanteResponse/numeroComprobante` | `NumeroComprobanteSimpleType` | 1..1 | Número de comprobante (8) |
| `comprobanteResponse/fechaEmision` | `date` | 1..1 | Fecha de emisión |
| `comprobanteResponse/CAE` | `long` | 1..1 | CAE otorgado (14) |
| `comprobanteResponse/fechaVencimientoCAE` | `date` | 1..1 | Vencimiento del CAE |
| `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Diferencias manual / WSDL: el esquema del manual (pág. 23) tipa `importeGravado`, `importeNoGravado`, `importeExento` e `importeSubtotal` como `ImporteTotalSimpleType` (≥ 0); el WSDL los tipa `ImporteSubtotalSimpleType` (admite negativos), igual que la tabla de tipos del manual (pág. 342). `subtotalIVA/importe` también es `ImporteSubtotalSimpleType` en el WSDL.

Ejemplo del manual, Factura A (pág. 30-33), con los erratas tal cual (`<descripcion>...<descripcion>` sin cerrar):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://impl.service.wsmtxca.afip.gob.ar/service/">
  <soapenv:Header/>
  <soapenv:Body>
    <ser:autorizarComprobanteRequest>
      <authRequest><token>string</token><sign>string</sign><cuitRepresentada>66666666666</cuitRepresentada></authRequest>
      <comprobanteCAERequest>
        <codigoTipoComprobante>1</codigoTipoComprobante>
        <numeroPuntoVenta>4000</numeroPuntoVenta>
        <numeroComprobante>1</numeroComprobante>
        <fechaEmision>2010-11-01</fechaEmision>
        <codigoTipoDocumento>80</codigoTipoDocumento>
        <numeroDocumento>30000000007</numeroDocumento>
        <condicionIVAReceptor>1</condicionIVAReceptor>
        <importeGravado>100.00</importeGravado>
        <importeNoGravado>0.00</importeNoGravado>
        <importeExento>0.00</importeExento>
        <importeSubtotal>100.00</importeSubtotal>
        <importeOtrosTributos>1.00</importeOtrosTributos>
        <importeTotal>122.00</importeTotal>
        <codigoMoneda>PES</codigoMoneda>
        <cotizacionMoneda>1</cotizacionMoneda>
        <cancelaEnMismaMonedaExtranjera>N</cancelaEnMismaMonedaExtranjera>
        <observaciones>Observaciones Comerciales, libre</observaciones>
        <codigoConcepto>1</codigoConcepto>
        <arrayOtrosTributos>
          <otroTributo><codigo>99</codigo><descripcion>Otro Tributo</descripcion><baseImponible>100.00</baseImponible><importe>1.00</importe></otroTributo>
        </arrayOtrosTributos>
        <arrayItems>
          <item>
            <unidadesMtx>123456</unidadesMtx><codigoMtx>0123456789913</codigoMtx><codigo>P0001</codigo>
            <descripcion>Descripción del producto P0001<descripcion>
            <cantidad>1.00</cantidad><codigoUnidadMedida>7</codigoUnidadMedida><precioUnitario>100.00</precioUnitario>
            <importeBonificacion>0.00</importeBonificacion><codigoCondicionIVA>5</codigoCondicionIVA>
            <importeIVA>21.00</importeIVA><importeItem>121.00</importeItem>
          </item>
        </arrayItems>
        <arraySubtotalesIVA><subtotalIVA><codigo>5</codigo><importe>21.00</importe></subtotalIVA></arraySubtotalesIVA>
        <arrayActividades><actividad><codigo>120010</codigo></actividad><actividad><codigo>463300</codigo></actividad></arrayActividades>
      </comprobanteCAERequest>
    </ser:autorizarComprobanteRequest>
  </soapenv:Body>
</soapenv:Envelope>
```

```xml
<ser:autorizarComprobanteResponse>
  <resultado>A</resultado>
  <comprobanteResponse>
    <cuit>66666666666</cuit><codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>4000</numeroPuntoVenta>
    <numeroComprobante>1</numeroComprobante><fechaEmision>2010-11-01</fechaEmision>
    <CAE>12345678901234</CAE><fechaVencimientoCAE>2010-11-16</fechaVencimientoCAE>
  </comprobanteResponse>
</ser:autorizarComprobanteResponse>
```

Cuenta de control del ejemplo: `importeTotal` 122.00 = `importeSubtotal` 100 + `importeOtrosTributos` 1 + IVA 21 (validación 115).

El ejemplo Factura B (pág. 33-36) usa `codigoTipoDocumento` 96 (DNI) 24999999, `condicionIVAReceptor` 5, dos ítems (uno gravado 21 % con `precioUnitario` 121 **IVA incluido** y sin `importeIVA`, otro exento por 100), `importeGravado` 100, `importeExento` 100, `importeSubtotal` 200, otros tributos 0.01, total 221.01. Su respuesta (con prefijo `ns1:` y namespace `.gob.ar`) devuelve **otro** punto de venta y número (1 y 10, contra 4000 y 1 del request) y `CAE` 60504000053157 con vencimiento a 10 días: es un ejemplo armado, no una respuesta real.

### autorizarAjusteIVA

Autoriza un comprobante de ajuste de IVA CAE (pág. 73-111). Mismo request (`comprobanteCAERequest` de tipo `ComprobanteType`) y misma respuesta que `autorizarComprobante`; cambian las validaciones. Las principales: 136, sólo notas de débito y crédito (2, 3, 7, 8, 52, 53); 137, 138, 139 y 141, `importeGravado`, `importeNoGravado`, `importeExento` e `importeOtrosTributos` "No debe informarse"; 140, `importeSubtotal` en 0; 142, `importeTotal` igual a la suma de `subtotalIVA/importe`. O sea, el comprobante sólo ajusta IVA.

Request:

Elemento raíz: `autorizarAjusteIVARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `comprobanteCAERequest` | `ComprobanteType` | 1..1 | Comprobante (`ComprobanteType`, pág. 341-354) (estructura completa en `autorizarComprobante`) |


Response:

Elemento raíz: `autorizarAjusteIVAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `resultado` | `ResultadoSimpleType` | 1..1 | `A` aprobado, `O` observado, `R` rechazado |
| `comprobanteResponse` | `ComprobanteCAEResponseType` | 0..1 | Existe si fue aprobado (`ComprobanteCAEResponseType`) |
| `comprobanteResponse/cuit` | `long` | 1..1 | CUIT emisora (11) |
| `comprobanteResponse/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante (3). Ver `consultarTiposComprobante` |
| `comprobanteResponse/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |
| `comprobanteResponse/numeroComprobante` | `NumeroComprobanteSimpleType` | 1..1 | Número de comprobante (8) |
| `comprobanteResponse/fechaEmision` | `date` | 1..1 | Fecha de emisión |
| `comprobanteResponse/CAE` | `long` | 1..1 | CAE otorgado (14) |
| `comprobanteResponse/fechaVencimientoCAE` | `date` | 1..1 | Vencimiento del CAE |
| `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### solicitarCAEA

Pide un CAEA para un período y quincena (pág. 112-120). "Podrá ser solicitado dentro de los 5 (cinco) días corridos anteriores al comienzo de cada quincena y hasta el final de la misma. [...] la primera abarca desde el primero hasta el quince de cada mes y la segunda desde el dieciséis hasta el último día del mes."

Request:

Elemento raíz: `solicitarCAEARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `solicitudCAEA` | `SolicitudCAEAType` | 1..1 | Período y quincena pedidos |
| `solicitudCAEA/periodo` | `int` | 1..1 | Año y mes `AAAAMM` (600) |
| `solicitudCAEA/orden` | `short` | 1..1 | 1 primera quincena, 2 segunda (601) |


Response:

Elemento raíz: `solicitarCAEAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `CAEAResponse` | `CAEAResponseType` | 0..1 | CAEA otorgado (`CAEAResponseType`) |
| `CAEAResponse/fechaProceso` | `date` | 1..1 | Fecha de proceso |
| `CAEAResponse/CAEA` | `long` | 1..1 | CAEA (14) |
| `CAEAResponse/periodo` | `int` | 1..1 | Año y mes `AAAAMM` (600) |
| `CAEAResponse/orden` | `short` | 1..1 | 1 primera quincena, 2 segunda (601) |
| `CAEAResponse/fechaDesde` | `date` | 1..1 | Inicio de vigencia |
| `CAEAResponse/fechaHasta` | `date` | 1..1 | Fin de vigencia |
| `CAEAResponse/fechaTopeInforme` | `date` | 1..1 | Fecha tope para informar los comprobantes |
| `CAEAResponse/arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `CAEAResponse/arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `CAEAResponse/arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `CAEAResponse/arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Ejemplo (pág. 117-118): request `periodo` 201011, `orden` 1; respuesta:

```xml
<ser:solicitarCAEAResponse>
  <CAEAResponse>
    <fechaProceso>2010-10-28</fechaProceso><CAEA>12345678901235</CAEA><periodo>201011</periodo><orden>1</orden>
    <fechaDesde>2010-11-01</fechaDesde><fechaHasta>2010-11-15</fechaHasta><fechaTopeInforme>2010-12-15</fechaTopeInforme>
  </CAEAResponse>
</ser:solicitarCAEAResponse>
```

### informarComprobanteCAEA

Informa un comprobante emitido con un CAEA (pág. 121-178). Uno por llamada. El comprobante va completo, con `codigoTipoAutorizacion`=`A`, `codigoAutorizacion` = el CAEA y `fechaVencimiento`.

Request:

Elemento raíz: `informarComprobanteCAEARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `comprobanteCAEARequest` | `ComprobanteType` | 1..1 | Comprobante (`ComprobanteType`), con `codigoTipoAutorizacion` = `A` y el CAEA (estructura completa en `autorizarComprobante`) |


Response:

Elemento raíz: `informarComprobanteCAEAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `resultado` | `ResultadoSimpleType` | 1..1 | `A` aprobado, `O` observado, `R` rechazado |
| `fechaProceso` | `date` | 1..1 | Fecha de proceso |
| `comprobanteCAEAResponse` | `ComprobanteCAEAResponseType` | 0..1 | Existe si fue aprobado (`ComprobanteCAEAResponseType`) |
| `comprobanteCAEAResponse/CAEA` | `long` | 1..1 | CAEA (14) |
| `comprobanteCAEAResponse/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante (3). Ver `consultarTiposComprobante` |
| `comprobanteCAEAResponse/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |
| `comprobanteCAEAResponse/numeroComprobante` | `NumeroComprobanteSimpleType` | 1..1 | Número de comprobante (8) |
| `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Ejemplo (pág. 130-137), Factura A: `numeroPuntoVenta` 1000, `fechaEmision` 2010-11-01, `codigoTipoAutorizacion` `A`, `codigoAutorizacion` 12345678901235, `fechaVencimiento` 2010-11-15, importes gravado 10916.04, no gravado 12.00, exento 4132.00, subtotal 15060.04, otros tributos 16.00, total 17645.00, con un ítem de "Descuento general" `codigoUnidadMedida` 99, `importeIVA` -31.47, `importeItem` -1498.43.

### informarAjusteIVACAEA

Informa un ajuste de IVA emitido con CAEA (pág. 179-215). Mismo request y respuesta que `informarComprobanteCAEA`.

Request:

Elemento raíz: `informarAjusteIVACAEARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `comprobanteCAEARequest` | `ComprobanteType` | 1..1 | Comprobante (`ComprobanteType`), con `codigoTipoAutorizacion` = `A` y el CAEA (estructura completa en `autorizarComprobante`) |


Response:

Elemento raíz: `informarAjusteIVACAEAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `resultado` | `ResultadoSimpleType` | 1..1 | `A` aprobado, `O` observado, `R` rechazado |
| `fechaProceso` | `date` | 1..1 | Fecha de proceso |
| `comprobanteCAEAResponse` | `ComprobanteCAEAResponseType` | 0..1 | Existe si fue aprobado (`ComprobanteCAEAResponseType`) |
| `comprobanteCAEAResponse/CAEA` | `long` | 1..1 | CAEA (14) |
| `comprobanteCAEAResponse/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante (3). Ver `consultarTiposComprobante` |
| `comprobanteCAEAResponse/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |
| `comprobanteCAEAResponse/numeroComprobante` | `NumeroComprobanteSimpleType` | 1..1 | Número de comprobante (8) |
| `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### informarCAEANoUtilizado

Avisa que un CAEA no se usó en ningún comprobante; después no se puede usar (pág. 216-221).

Request:

Elemento raíz: `informarCAEANoUtilizadoRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `CAEA` | `long` | 1..1 | CAEA (14) |


Response:

Elemento raíz: `informarCAEANoUtilizadoResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `resultado` | `ResultadoSimpleType` | 1..1 | `A` aprobado, `O` observado, `R` rechazado |
| `fechaProceso` | `date` | 1..1 | Fecha de proceso |
| `CAEA` | `long` | 1..1 | CAEA (14) |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


`resultado`: A aprobada, R rechazada (pág. 219).

### informarCAEANoUtilizadoPtoVta

Avisa que un CAEA no se usó en un punto de venta (pág. 222-227).

Request:

Elemento raíz: `informarCAEANoUtilizadoPtoVtaRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `CAEA` | `long` | 1..1 | CAEA (14) |
| `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta CAEA (1204-1207) |


Response:

Elemento raíz: `informarCAEANoUtilizadoPtoVtaResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `resultado` | `ResultadoSimpleType` | 1..1 | `A` aprobado, `O` observado, `R` rechazado |
| `fechaProceso` | `date` | 1..1 | Fecha de proceso |
| `CAEA` | `long` | 1..1 | CAEA (14) |
| `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta CAEA (1204-1207) |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Ejemplo (pág. 225): request `CAEA` 12345678901234, `numeroPuntoVenta` 123; respuesta `<resultado>A</resultado><fechaProceso>2010-12-10</fechaProceso><CAEA>12345678901234</CAEA><numeroPuntoVenta>123</numeroPuntoVenta>`.

### consultarPtosVtaCAEANoInformados

Puntos de venta CAEA que todavía no informaron nada para un CAEA (pág. 228-234).

Request:

Elemento raíz: `consultarPtosVtaCAEANoInformadosRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `CAEA` | `long` | 1..1 | CAEA (14) |


Response:

Elemento raíz: `consultarPtosVtaCAEANoInformadosResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayPuntosVenta` | `ArrayPuntosVentaType` | 0..1 | Puntos de venta |
| `arrayPuntosVenta/puntoVenta` | `PuntoVentaType` | 0..n | Un punto de venta |
| `arrayPuntosVenta/puntoVenta/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Número (5) |
| `arrayPuntosVenta/puntoVenta/bloqueado` | `SiNoSimpleType` | 1..1 | `S` bloqueado, `N` no (los ejemplos muestran `Si`/`No`) |
| `arrayPuntosVenta/puntoVenta/fechaBaja` | `date` | 0..1 | Fecha de baja, si tiene |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Ejemplo (pág. 232): puntos 193, 243 y 410, todos `<bloqueado>No</bloqueado>`. Ojo: el ejemplo usa `No`/`Si`, pero `SiNoSimpleType` sólo admite `S` y `N`. Lo mismo en los ejemplos de `consultarPuntosVenta*`. Cuál sale de verdad: NO VERIFICADO (el WSDL dice `S`/`N`).

### consultarCAEA

Datos de un CAEA ya otorgado (pág. 235-241).

Request:

Elemento raíz: `consultarCAEARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `CAEA` | `long` | 1..1 | CAEA (14) |


Response:

Elemento raíz: `consultarCAEAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `CAEAResponse` | `CAEAResponseType` | 0..1 | CAEA otorgado (`CAEAResponseType`) |
| `CAEAResponse/fechaProceso` | `date` | 1..1 | Fecha de proceso |
| `CAEAResponse/CAEA` | `long` | 1..1 | CAEA (14) |
| `CAEAResponse/periodo` | `int` | 1..1 | Año y mes `AAAAMM` (600) |
| `CAEAResponse/orden` | `short` | 1..1 | 1 primera quincena, 2 segunda (601) |
| `CAEAResponse/fechaDesde` | `date` | 1..1 | Inicio de vigencia |
| `CAEAResponse/fechaHasta` | `date` | 1..1 | Fin de vigencia |
| `CAEAResponse/fechaTopeInforme` | `date` | 1..1 | Fecha tope para informar los comprobantes |
| `CAEAResponse/arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `CAEAResponse/arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `CAEAResponse/arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `CAEAResponse/arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarCAEAEntreFechas

CAEA otorgados entre dos fechas (pág. 242-247).

Request:

Elemento raíz: `consultarCAEAEntreFechasRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `fechaDesde` | `date` | 1..1 | Desde |
| `fechaHasta` | `date` | 1..1 | Hasta |


Response:

Elemento raíz: `consultarCAEAEntreFechasResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayCAEAResponse` | `ArrayCAEAResponseType` | 0..1 | CAEA del rango |
| `arrayCAEAResponse/CAEAResponse` | `CAEAResponseType` | 0..n | CAEA otorgado (`CAEAResponseType`) |
| `arrayCAEAResponse/CAEAResponse/fechaProceso` | `date` | 1..1 | Fecha de proceso |
| `arrayCAEAResponse/CAEAResponse/CAEA` | `long` | 1..1 | CAEA (14) |
| `arrayCAEAResponse/CAEAResponse/periodo` | `int` | 1..1 | Año y mes `AAAAMM` (600) |
| `arrayCAEAResponse/CAEAResponse/orden` | `short` | 1..1 | 1 primera quincena, 2 segunda (601) |
| `arrayCAEAResponse/CAEAResponse/fechaDesde` | `date` | 1..1 | Inicio de vigencia |
| `arrayCAEAResponse/CAEAResponse/fechaHasta` | `date` | 1..1 | Fin de vigencia |
| `arrayCAEAResponse/CAEAResponse/fechaTopeInforme` | `date` | 1..1 | Fecha tope para informar los comprobantes |
| `arrayCAEAResponse/CAEAResponse/arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `arrayCAEAResponse/CAEAResponse/arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayCAEAResponse/CAEAResponse/arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayCAEAResponse/CAEAResponse/arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


El ejemplo (pág. 246) devuelve dos CAEA de 201011, el segundo con `fechaHasta` **2010-11-31** (fecha inexistente: errata).

### consultarUltimoComprobanteAutorizado

Último número autorizado o informado para un punto de venta y tipo, CAE o CAEA (pág. 248-254).

Request:

Elemento raíz: `consultarUltimoComprobanteAutorizadoRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `consultaUltimoComprobanteAutorizadoRequest` | `ConsultaUltimoComprobanteAutorizadoRequestType` | 1..1 | Filtro |
| `consultaUltimoComprobanteAutorizadoRequest/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante (3). Ver `consultarTiposComprobante` |
| `consultaUltimoComprobanteAutorizadoRequest/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |


Response:

Elemento raíz: `consultarUltimoComprobanteAutorizadoResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `numeroComprobante` | `NumeroComprobanteSimpleType` | 0..1 | Último número autorizado o informado |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Ejemplo (pág. 251): tipo 1, punto 4000 → `<numeroComprobante>1</numeroComprobante>`. **Si no hay ningún comprobante, no devuelve 0: rechaza con 1502** "Debe obrar en las bases del organismo al menos un comprobante emitido con el tipo de comprobante y punto de ventas indicados" (pág. 253). Es distinto de wsfev1. `numeroComprobante` es `NumeroComprobanteSimpleType` (≥ 1), así que 0 ni siquiera sería válido.

### consultarComprobante

Devuelve un comprobante CAE o CAEA ya autorizado (pág. 255-267). Es el método para recuperarse de un corte de comunicación (pág. 13).

Request:

Elemento raíz: `consultarComprobanteRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `consultaComprobanteRequest` | `ConsultaComprobanteRequestType` | 1..1 | Clave del comprobante |
| `consultaComprobanteRequest/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante (3). Ver `consultarTiposComprobante` |
| `consultaComprobanteRequest/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Punto de venta (5) |
| `consultaComprobanteRequest/numeroComprobante` | `NumeroComprobanteSimpleType` | 1..1 | Número de comprobante (8) |


Response:

Elemento raíz: `consultarComprobanteResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `comprobante` | `ComprobanteType` | 0..1 | Comprobante consultado (`ComprobanteType`), si existe (estructura completa en `autorizarComprobante`) |
| `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | Observaciones (validaciones no excluyentes) |
| `arrayObservaciones/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayObservaciones/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayObservaciones/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Ejemplo (pág. 263-266): devuelve el comprobante del primer ejemplo con `codigoTipoAutorizacion` `E` (CAE), `codigoAutorizacion` 12345678901234 y `fechaVencimiento` 2010-11-16 (o sea, el CAE y su vencimiento van en `codigoAutorizacion` y `fechaVencimiento` de `ComprobanteType`). Otra errata del ejemplo: `importeOtrosTributos` 100.00 con un tributo de 1.00.

### consultarTiposComprobante

Tipos de comprobante habilitados en este WS (pág. 268-272).

Request:

Elemento raíz: `consultarTiposComprobanteRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarTiposComprobanteResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayTiposComprobante` | `ArrayCodigosDescripcionesType` | 1..1 | Tipos de comprobante |
| `arrayTiposComprobante/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayTiposComprobante/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayTiposComprobante/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarTiposDocumento

Tipos de documento (pág. 273-276).

Request:

Elemento raíz: `consultarTiposDocumentoRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarTiposDocumentoResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayTiposDocumento` | `ArrayCodigosDescripcionesType` | 1..1 | Tipos de documento |
| `arrayTiposDocumento/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayTiposDocumento/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayTiposDocumento/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarAlicuotasIVA

Alícuotas de IVA (pág. 277-280).

Request:

Elemento raíz: `consultarAlicuotasIVARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarAlicuotasIVAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayAlicuotasIVA` | `ArrayCodigosDescripcionesType` | 1..1 | Alícuotas |
| `arrayAlicuotasIVA/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayAlicuotasIVA/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayAlicuotasIVA/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarCondicionesIVA

Condiciones de IVA **de un ítem** (no gravado, exento, alícuotas) (pág. 281-285).

Request:

Elemento raíz: `consultarCondicionesIVARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarCondicionesIVAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayCondicionesIVA` | `ArrayCodigosDescripcionesType` | 1..1 | Condiciones de IVA del ítem |
| `arrayCondicionesIVA/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayCondicionesIVA/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayCondicionesIVA/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarCondicionesIVAReceptor

Condiciones frente al IVA que puede tener el receptor según el tipo de comprobante (pág. 286-292). Agregado en 0.25.0 (RG 5616/2024).

Request:

Elemento raíz: `consultarCondicionesIVAReceptorRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `consultaCondicionesIVAReceptorRequest` | `ConsultaCondicionesIVARequestType` | 1..1 | Filtro |
| `consultaCondicionesIVAReceptorRequest/codigoTipoComprobante` | `short` | 1..1 | Tipo de comprobante a autorizar (196) |


Response:

Elemento raíz: `consultarCondicionesIVAReceptorResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayCondicionesIVAReceptor` | `ArrayCodigosDescripcionesType` | 0..1 | Condiciones posibles del receptor |
| `arrayCondicionesIVAReceptor/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayCondicionesIVAReceptor/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayCondicionesIVAReceptor/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


El esquema del manual tipa `cuitRepresentada` como `string` en este método; el WSDL, `long` como en todos. Error propio: 196.

### consultarMonedas

Monedas (pág. 293-296).

Request:

Elemento raíz: `consultarMonedasRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarMonedasResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayMonedas` | `ArrayCodigosDescripcionesStringType` | 1..1 | Monedas (`codigo` string) |
| `arrayMonedas/codigoDescripcion` | `CodigoDescripcionStringType` | 1..n | Un código |
| `arrayMonedas/codigoDescripcion/codigo` | `string` | 1..1 | Código de moneda (string, 3) |
| `arrayMonedas/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


Es el único array de parámetros con `codigo` **string** (`CodigoDescripcionStringType`).

### consultarCotizacionMoneda

Cotización de una moneda a una fecha (pág. 296-301). "a) De existir la cotización devolverá el valor correspondiente. b) Si no existe cotización para la moneda indicada no retornará valor alguno. c) Si el código de moneda enviado es inválido devolverá un error" (1600).

Request:

Elemento raíz: `consultarCotizacionMonedaRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |
| `codigoMoneda` | `string` | 1..1 | Moneda a cotizar (1600) |
| `fechaCotizacion` | `date` | 1..1 | Fecha de la cotización `yyyy-mm-dd` |


Response:

Elemento raíz: `consultarCotizacionMonedaResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `cotizacionMoneda` | `decimal` | 0..1 | Cotización; no viene si no hay |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


`fechaCotizacion` es obligatorio desde 0.25.0. Ejemplo (pág. 300-301): `codigoMoneda` DOL → `<cotizacionMoneda>3.943216</cotizacionMoneda>`. **El ejemplo de request no trae `authRequest`**, que en el WSDL es obligatorio: errata del manual.

### consultarUnidadesMedida

Unidades de medida (pág. 302-305).

Request:

Elemento raíz: `consultarUnidadesMedidaRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarUnidadesMedidaResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayUnidadesMedida` | `ArrayCodigosDescripcionesType` | 1..1 | Unidades de medida |
| `arrayUnidadesMedida/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayUnidadesMedida/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayUnidadesMedida/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarPuntosVenta

Puntos de venta CAE y CAEA del emisor "habilitados para este WS"; si no hay, no devuelve ninguno (pág. 306-310).

Request:

Elemento raíz: `consultarPuntosVentaRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarPuntosVentaResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayPuntosVenta` | `ArrayPuntosVentaType` | 1..1 | Puntos de venta |
| `arrayPuntosVenta/puntoVenta` | `PuntoVentaType` | 0..n | Un punto de venta |
| `arrayPuntosVenta/puntoVenta/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Número (5) |
| `arrayPuntosVenta/puntoVenta/bloqueado` | `SiNoSimpleType` | 1..1 | `S` bloqueado, `N` no (los ejemplos muestran `Si`/`No`) |
| `arrayPuntosVenta/puntoVenta/fechaBaja` | `date` | 0..1 | Fecha de baja, si tiene |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarPuntosVentaCAE

Sólo los CAE (pág. 311-315). Mismo tipo de respuesta (`ConsultarPuntosVentaResponseType`).

Request:

Elemento raíz: `consultarPuntosVentaCAERequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarPuntosVentaCAEResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayPuntosVenta` | `ArrayPuntosVentaType` | 1..1 | Puntos de venta |
| `arrayPuntosVenta/puntoVenta` | `PuntoVentaType` | 0..n | Un punto de venta |
| `arrayPuntosVenta/puntoVenta/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Número (5) |
| `arrayPuntosVenta/puntoVenta/bloqueado` | `SiNoSimpleType` | 1..1 | `S` bloqueado, `N` no (los ejemplos muestran `Si`/`No`) |
| `arrayPuntosVenta/puntoVenta/fechaBaja` | `date` | 0..1 | Fecha de baja, si tiene |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarPuntosVentaCAEA

Sólo los CAEA (pág. 316-320).

Request:

Elemento raíz: `consultarPuntosVentaCAEARequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarPuntosVentaCAEAResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayPuntosVenta` | `ArrayPuntosVentaType` | 1..1 | Puntos de venta |
| `arrayPuntosVenta/puntoVenta` | `PuntoVentaType` | 0..n | Un punto de venta |
| `arrayPuntosVenta/puntoVenta/numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` | 1..1 | Número (5) |
| `arrayPuntosVenta/puntoVenta/bloqueado` | `SiNoSimpleType` | 1..1 | `S` bloqueado, `N` no (los ejemplos muestran `Si`/`No`) |
| `arrayPuntosVenta/puntoVenta/fechaBaja` | `date` | 0..1 | Fecha de baja, si tiene |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarTiposTributo

Tipos de tributo para `arrayOtrosTributos` (pág. 321-324).

Request:

Elemento raíz: `consultarTiposTributoRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarTiposTributoResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayTiposTributo` | `ArrayCodigosDescripcionesType` | 1..1 | Tipos de tributo |
| `arrayTiposTributo/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayTiposTributo/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayTiposTributo/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarTiposDatosAdicionales

Tipos de dato adicional para `arrayDatosAdicionales` (pág. 325-330). El esquema del manual (pág. 328) llama al array `arrayTiposTributo` (errata); el WSDL y el ejemplo, `arrayTiposDatosAdicionales`.

Request:

Elemento raíz: `consultarTiposDatosAdicionalesRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarTiposDatosAdicionalesResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayTiposDatosAdicionales` | `ArrayCodigosDescripcionesType` | 1..1 | Tipos de dato adicional |
| `arrayTiposDatosAdicionales/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayTiposDatosAdicionales/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayTiposDatosAdicionales/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### consultarActividadesVigentes

Actividades vigentes del emisor, que se pueden asociar al comprobante (pág. 331-335). Agregado en 0.18 (RG 5259/2022 y 5264/2022). El "ejemplo" del manual es el de `consultarTiposDatosAdicionales` copiado (errata).

Request:

Elemento raíz: `consultarActividadesVigentesRequest`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `authRequest` | `AuthRequestType` | 1..1 | Credenciales (ver Autenticación) |
| `authRequest/token` | `string` | 1..1 | Token del TA de WSAA |
| `authRequest/sign` | `string` | 1..1 | Firma del TA |
| `authRequest/cuitRepresentada` | `long` | 1..1 | CUIT emisora o representada (11) |


Response:

Elemento raíz: `consultarActividadesVigentesResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `arrayActividades` | `ArrayActividadesVigentesType` | 0..1 | Actividades vigentes (`ArrayActividadesVigentesType`) |
| `arrayActividades/actividad` | `ActividadVigenteType` | 1..n | Una actividad |
| `arrayActividades/actividad/codigo` | `long` | 1..1 | Código de actividad (6) |
| `arrayActividades/actividad/orden` | `long` | 1..1 | Orden (3) |
| `arrayActividades/actividad/descripcion` | `string` | 1..1 | Descripción (200) |
| `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | Errores (validaciones excluyentes) |
| `arrayErrores/codigoDescripcion` | `CodigoDescripcionType` | 1..n | Un código |
| `arrayErrores/codigoDescripcion/codigo` | `short` | 1..1 | Código de moneda (string, 3) |
| `arrayErrores/codigoDescripcion/descripcion` | `string` | 1..1 | Descripción |
| `evento` | `CodigoDescripcionType` | 0..1 | Anuncio informativo del sistema (uno solo) |
| `evento/codigo` | `short` | 1..1 | Código |
| `evento/descripcion` | `string` | 1..1 | Descripción |


### dummy

Ping (pág. 336-337). El mensaje de entrada del WSDL **no tiene `part`**: el Body puede ir vacío o con `<ser:dummy/>`; en vivo las dos formas responden igual.

Request:

_El mensaje no tiene `part`: el Body puede ir vacío (WSDL)._


Response:

Elemento raíz: `dummyResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `appserver` | `string` | 1..1 | Servidor de aplicaciones (`OK`) |
| `authserver` | `string` | 1..1 | Servidor de autenticación (`OK`) |
| `dbserver` | `string` | 1..1 | Servidor de base de datos (`OK`) |


Respuesta real (producción y homologación, 2026-10-02), HTTP 200 `text/xml;charset=utf-8`:

```xml
<?xml version='1.0' encoding='utf-8'?><soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"><soapenv:Body><ns1:dummyResponse xmlns:ns1="http://impl.service.wsmtxca.afip.gov.ar/service/"><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></ns1:dummyResponse></soapenv:Body></soapenv:Envelope>
```

Sin `<return>`, a diferencia del padrón. La tabla del manual (pág. 337) cruza las descripciones: dice `authserver` "Servidor de base de datos" y `dbserver` "Servidor de autenticacion".

## Validaciones y errores

Cómo se reparten (pág. 9-12, 21, 29):

| Tipo | Dónde | Efecto |
|---|---|---|
| Autenticación, estructura y tipos de datos | `soapenv:Fault`, HTTP 500 (producción) | No hay respuesta de negocio |
| Validación **excluyente** ("Rechaza") | `arrayErrores/codigoDescripcion` | `resultado`=`R`, sin `comprobanteResponse` |
| Validación **no excluyente** ("Observa") | `arrayObservaciones/codigoDescripcion` (en `solicitarCAEA`, dentro de `CAEAResponse`) | `resultado`=`O`, con CAE |
| Errores de los métodos de consulta | `arrayErrores` | Sin datos |

**Ojo: los códigos de error y de observación son espacios distintos.** El mismo número puede significar dos cosas según dónde aparezca. Ejemplos en `autorizarComprobante`: error 164 = valor inválido en `cancelaEnMismaMonedaExtranjera`, observación 164 = receptor monotributista; error 150 = receptor no obligado a FCE, observación 150 = falta la Percepción de IVA No Categorizado; error 169 = `cancelaEnMismaMonedaExtranjera` con pesos, observación 169 = emisor con formulario de habilitación pendiente; 253 y 265 son error para facturas y observación para notas de crédito.

La columna "Texto / condición" es la descripción del manual, **no** la `descripcion` que devuelve el servicio. El manual no publica ninguna `descripcion` real de error o de observación (todos los ejemplos de respuesta son aprobados). NO VERIFICADO para todas.

El manual separa cada método en bloques (`authRequest`, `comprobanteCAERequest`, `comprobanteAsociado`, `periodoComprobantesAsociados`, `otroTributo`, `subtotalIVA`, `item`, `datoAdicional`, `comprador`); la columna "Campo" lleva el bloque delante cuando se pudo detectar. Si devuelve todos los errores juntos o corta en el primero: NO VERIFICADO.

**Completitud:** están **todas** las tablas de validación del manual: las de las 14 operaciones que las tienen más la tabla del emisor (679 filas en total, extraídas tabla por tabla del PDF con PyMuPDF, uniendo filas partidas entre páginas, y cotejadas con los números que aparecen en el texto de cada página). Las otras 13 operaciones (`consultarTiposComprobante`, `consultarTiposDocumento`, `consultarAlicuotasIVA`, `consultarCondicionesIVA`, `consultarMonedas`, `consultarUnidadesMedida`, `consultarPuntosVenta`, `consultarPuntosVentaCAE`, `consultarPuntosVentaCAEA`, `consultarTiposTributo`, `consultarTiposDatosAdicionales`, `consultarActividadesVigentes` y `dummy`) **no tienen** validaciones en el manual, aunque `consultarActividadesVigentes` declara `arrayErrores`. Todo está también en `wsmtxca-codigos.json`, con los campos extra `section` (bloque del manual), `donde` y, cuando hace falta, `nota`.

Lo que el historial menciona pero el manual no tabula en el método esperado: 261, 297 y 304 (receptor/comprador inactivado, no confiable o apócrifo) sólo aparecen en `autorizarAjusteIVA`, no en `autorizarComprobante`; 10006 sólo en `informarComprobanteCAEA`. Se dejaron como están.

### Validaciones del emisor (pág. 16)

"Validaciones sobre el emisor del comprobante al solicitar CAE o CAEA". A qué métodos aplican exactamente: NO VERIFICADO (se asume `autorizarComprobante`, `autorizarAjusteIVA` y `solicitarCAEA`).

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10000 | Debe encontrarse activa en el Sistema Registral _(Tabla "Validaciones sobre el emisor del comprobante al solicitar CAE o CAEA" (pág. PDF 16). A qué métodos exactos aplica: NO VERIFICADO.)_ | Rechaza | arrayErrores | CUIT | 16 |
| 10001 | Debe poseer al menos una actividad activa. _(Tabla "Validaciones sobre el emisor del comprobante al solicitar CAE o CAEA" (pág. PDF 16). A qué métodos exactos aplica: NO VERIFICADO.)_ | Rechaza | arrayErrores | — | 16 |
| 10002 | No debe registrar inconvenientes con su domicilio fiscal. _(Tabla "Validaciones sobre el emisor del comprobante al solicitar CAE o CAEA" (pág. PDF 16). A qué métodos exactos aplica: NO VERIFICADO.)_ | Rechaza | arrayErrores | — | 16 |
| 10003 | Debe estar dado de alta en el Impuesto al Valor Agregado al momento del envío de la solicitud. _(Tabla "Validaciones sobre el emisor del comprobante al solicitar CAE o CAEA" (pág. PDF 16). A qué métodos exactos aplica: NO VERIFICADO.)_ | Rechaza | arrayErrores | — | 16 |


### autorizarComprobante (189)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10005 | La cuit emisora ha sido incluída en la consulta de facturas apócrifas | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 37 |
| 10010 | Debe encontrarse empadronado en Codificación de Productos - opción Factura con Detalle | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 37 |
| 100 | Podrá ser: 1 – Factura A 2 – Nota de Débito A 3 – Nota de Crédito A 6 – Factura B 7 – Nota de Débito B 8 – Nota de Crédito B 51 – Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN 52 – Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 – Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 201 - Factura de Crédito Electrónica MiPyMEs (FCE) A 202 - Nota de Débito Electrónica MiPyMEs (FCE) A 203 - Nota de Crédito Electrónica MiPyMEs (FCE) A 206- Factura de Crédito Electrónica MiPyMEs (FCE) B 207 - Nota de Débito Electrónica MiPyMEs (FCE) B 208 - Nota de Crédito Electrónica MiPyMEs (FCE) B Consultar método consultarTiposComprobante | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante | 38 |
| 100 | El contribuyente no se encuentra habilitado a emitir (según el tipo de comprobante indicado) comprobantes A, A con leyenda PAGO EN CBU INFORMADA o A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / cuitRepresentada | 39 |
| 101 | Debe ser del tipo habilitado para el régimen CAE Codificación de Productos – Web Services y no debe estar bloqueado. Consultar método consultarPuntosVenta o consultarPuntosVentaCAE | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroPuntoVenta | 39 |
| 102 | El número de comprobante informado debe ser mayor en 1 al último informado para igual punto de venta y tipo de comprobante. De no existir comprobante informado para igual punto de venta y codigoTipoComprobante, el número de comprobante debe ser igual a 1 (uno) | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 39 |
| 103 | Opcional. Para `<codigoConcepto>` igual a 1, la fecha de emisión del comprobante puede ser hasta 5 días anteriores o posteriores respecto de la fecha de generación, pero sin extenderse al mes siguiente; si se indica `<codigoConcepto>` igual a 2 ó 3 puede ser hasta 10 días anteriores o posteriores a la fecha de generación Obs.: Si no se envía se le asignará la fecha de proceso. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaEmision | 39 |
| 104 | La fecha de emisión debe ser mayor o igual a la fecha de emisión del último comprobante del mismo tipo e igual número de punto de venta. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaEmision / numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 39 |
| 105 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoAutorizacion | 39 |
| 106 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoAutorizacion | 39 |
| 107 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimiento | 39 |
| 108 | Si se informa uno de los campos debe informarse el otro. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 40 |
| 110 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52, 53, 201, 202 ó 203: Deberá ser igual a la sumatoria de `<importeItem>` menos `<importeIVA>` para la totalidad de los ítems con `<codigoCondicionIVA>` igual a 3, 4, 5 ó 6. Si `<codigoTipoComprobante>` es igual a 6, 7 , 8, 206, 207 u 208: Deberá ser igual a la sumatoria de `<importeItem>` menos el IVA correspondiente (calculado en base al importe y la alícuota de cada ítem), para la totalidad de los ítems con `<codigoCondicionIVA>` igual a 3, 4, 5 ó 6. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems gravados * | Rechaza | arrayErrores | `comprobanteCAERequest` › importeGravado | 40 |
| 111 | Deberá coincidir con la sumatoria de `<importeItem>` para los ítems con `<codigoCondicionIVA>` igual a 1. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems no gravados * | Rechaza | arrayErrores | `comprobanteCAERequest` › importeNoGravado | 40 |
| 112 | Deberá coincidir con la sumatoria de `<importeItem>` para los ítems con `<codigoCondicionIVA>` igual a 2. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems exentos * | Rechaza | arrayErrores | `comprobanteCAERequest` › importeExento | 41 |
| 113 | Deberá coincidir con la sumatoria de los campos `<importeNoGravado>`, `<importeGravado>`, `<importeExento>`. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * | Rechaza | arrayErrores | `comprobanteCAERequest` › importeSubtotal | 41 |
| 114 | Debe ser igual a la sumatoria de la totalidad de los campos `<otroTributo>``<importe>` (dentro de `<arrayOtrosTributos>`). Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de tributos * | Rechaza | arrayErrores | `comprobanteCAERequest` › importeOtrosTributos | 41 |
| 115 | Debe ser igual a `<importeSubtotal>`+ `<importeOtrosTributos>` + sumatoria de `<subtotalIVA>``<importe>` (dentro del arraySubtotalesIVA). Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * | Rechaza | arrayErrores | `comprobanteCAERequest` › importeTotal | 41 |
| 116 | Debe ser igual a `<importeOtrosTributos>` + la sumatoria de la totalidad de los campos `<importeItem>`. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems * | Rechaza | arrayErrores | `comprobanteCAERequest` › importeTotal | 42 |
| 117 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarMonedas | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoMoneda | 42 |
| 118 | En caso de enviar la marca de que el pago del comprobante se realiza en la misma moneda extranjera para comprobantes que no sean facturas. Unicamente se puede utilizar con los códigos habilitados (1,6,51,201,206) | Rechaza | arrayErrores | `comprobanteCAERequest` › cancelaEnMismaMonedaExtranjera | 42 |
| 119 | No podrá ser inferior al 2% ni superior en un 400% del que suministra ARCA como orientativo de acuerdo a la cotización oficial | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 42 |
| 120 | Debe ser igual a 1 (uno) si `<codigoMoneda>` es igual a PES | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 42 |
| 164 | En caso de enviar un valor inválido para la marca de que el pago de la factura se realiza en la misma moneda extranjera. Los valores válidos son S, N o vacío | Rechaza | arrayErrores | `comprobanteCAERequest` › cancelaEnMismaMonedaExtranjera | 42 |
| 169 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera y enviar como código de moneda el Peso Argentino | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoMoneda / cancelaEnMismaMonedaExtranjera | 42 |
| 192 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera, que codigoMoneda es del grupo de monedas con cotización del Banco de la Nación Argentina (ver Anexo Monedas BNA), que haya cotización y que la misma no coincida exactamente con el valor enviado en el campo cotizacionMoneda. En cuyo caso se podrá omitir el mismo para que la cotización de la factura sea la obtenida de los registros de ARCA | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoMoneda / cotizacionMoneda / cancelaEnMismaMonedaExtranjera | 43 |
| 194 | El campo es obligatorio a excepción de los casos para los cuales se envia el campo cancelaEnMismaMonedaExtranjera y se puede obtener la cotizacion asociada al codigoMoneda si esta es del grupo de monedas del Banco de la Nación Argentina (ver Anexo Monedas BNA) | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 43 |
| 195 | No es posible indicar una cotización negativa | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 43 |
| 253 | Si `<codigoTipoComprobante>` NO es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue inactivado o invalidado. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 43 |
| 265 | Si `<codigoTipoComprobante>` NO es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 43 |
| 303 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido marcada como Apocrifa. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 43 |
| 121 | Deberá ser igual a alguno de los siguientes valores: 1 – Productos 2 – Servicios 3 – Productos y Servicios | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoConcepto | 44 |
| 122 | Opcional. Debe informarse si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaServicioDesde | 44 |
| 123 | Opcional. Debe informarse si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaServicioHasta | 44 |
| 124 | Opcional. Debe informarse si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimientoPago | 44 |
| 125 | La fecha de vencimiento de pago debe ser posterior o igual a la fecha de emisión. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimientoPago / fechaEmision | 44 |
| 127 | Opcional. Debe informarse si algún ítem tiene `<codigoCondicionIVA>` igual a 4, 5 ó 6. En otro caso no corresponde. | Rechaza | arrayErrores | `comprobanteCAERequest` › arraySubtotalesIVA | 44 |
| 128 | Opcionales. Deberán informarse en los siguientes casos: - cuando `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52, 53, 201, 202, 203, 206, 207 ó 208. -cuando `<codigoTipoComprobante>` es igual a 6, 7 u 8 y el importe total del comprobante `<importeTotal>` es mayor ó igual al monto en pesos resultante según RG4444. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 44 |
| 129 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52, 53, 201, 202, 203, 206, 207 ó 208. `<codigoTipoDocumento>` deberá ser igual a 80 (CUIT) | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento | 44 |
| 131 | El Receptor no puede ser igual al Emisor | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroDocumento | 44 |
| 132 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarTiposDocumento | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento | 45 |
| 133 | La Fecha de Servicio desde debe ser menor o igual a la Fecha de Servicio Hasta | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaServicioDesde / fechaServicioHasta | 45 |
| 135 | Solicitudes de autorización para un mismo punto de venta y tipo de comprobante deben ser enviadas en forma sincrónica: si el WS recibe una nueva solicitud para un punto de venta y tipo de comprobante dado mientras la anterior está siendo procesada, la nueva solicitud será rechazada | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroPuntoVenta / codigoTipoComprobante | 45 |
| 145 | Falta incluir en la sección OtrosTributos la Percepción de IVA No Categorizado (ID 13), según lo dispuesto por la RG 2126/2006. Se debe informar el tributo `<codigo>` 13 (Percepción de IVA No Categorizado) con un `<importe>` mayor a 0 (cero) dentro del nodo `<arrayOtrosTributos>` cuando se cumplan simultáneamente las siguientes condiciones: - `<codigoTipoComprobante>` corresponda a un comprobante clase B (6, 7 u 8). - `<codigoTipoDocumento>` sea igual a 80 (CUIT). - `<numeroDocumento>` corresponda a 23000000000 (No Categorizado). - La suma de `<importeGravado>` más la sumatoria de los campos `<importe>` dentro de `<arraySubtotalesIVA>` sea mayor a 0 (cero). | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento / importeGravado / arraySubtotalesIVA / arrayOtrosTributos / importeOtrosTributos | 45 |
| 146 | La fecha/hora de generación solo debe informarse para comprobantes CAEA | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaHoraGen | 45 |
| 147 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208. Por las condiciones de la CUIT Emisora, no corresponde realizar FCE | Rechaza | arrayErrores | `comprobanteCAERequest` › cuitRepresentada | 46 |
| 148 | Si `<codigoTipoComprobante>` es igual a 201 ó 206. La Fecha de Vencimiento de Pago es obligatorio para Facturas de Crédito MiPyME | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimientoPago | 46 |
| 149 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208. La Fecha de Vencimiento de Pago no debe informarse para Notas de Crédito o Débito de las Facturas de Crédito MiPYME | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimientoPago | 46 |
| 150 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208. La CUIT Receptora no está incluida en el listado de empresas grandes según cronograma vigente ni optó por ser receptora de Factura de Crédito MiPyme | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 46 |
| 151 | - Si `<codigoTipoComprobante>` es igual a 1 ó 6, y - La CUIT Receptora está incluida en el listado de empresas grandes según cronograma vigente u optó por ser receptora de Factura de Crédito MiPyme, y - Por las condiciones de la CUIT Emisora, y - El monto facturado es mayor o igual al Reglamentado Corresponde realizar Factura Electrónica de Crédito MiPyME, realice un comprobante con `<codigoTipoComprobante>` 201 o 206. | Rechaza | arrayErrores | `comprobanteCAERequest` › cuitRepresentada / codigoTipoDocumento / numeroDocumento / importeTotal | 46 |
| 152 | - Si `<codigoTipoComprobante>` es igual a 201 ó 206, y - La CUIT Receptora está incluida en el listado de empresas grandes según cronograma vigente u optó por ser receptora de Factura de Crédito MiPyme, y - Por las condiciones de la CUIT Emisora, y - El monto facturado es menor al Reglamentado NO Corresponde realizar Factura Electrónica de Crédito MiPyME, realice un comprobante con `<codigoTipoComprobante>` 1 o 6. | Rechaza | arrayErrores | `comprobanteCAERequest` › cuitRepresentada / codigoTipoDocumento / numeroDocumento / importeTotal | 47 |
| 153 | Si `<codigoTipoComprobante>` es igual a 203 ó 208. El importe total del comprobante a autorizar no puede ser mayor o igual al saldo de la operación actual de la cuenta corriente | Rechaza | arrayErrores | `comprobanteCAERequest` › importeTotal | 47 |
| 154 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208, la moneda debe: - coincidir con la Factura vinculada, ó - ser Pesos Argentinos si la Factura vinculada ya fue aceptada, cancelada o rechazada y se desea realizar un ajuste por diferencia de cambio | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoMoneda | 47 |
| 155 | Si `<codigoTipoComprobante>` es igual a 201, 202, ó 203 la CUIT del receptor debe encontrarse activa en IVA o en monotributo. | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroDocumento | 47 |
| 156 | Si `<codigoTipoComprobante>` es igual a 206, 207, ó 208 la CUIT del receptor debe encontrarse activa como Responsable Inscripto en IVA, IVA Exento o Monotributista. | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroDocumento | 47 |
| 157 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208. La CUIT Receptora no registra alta en el Domicilio Fiscal Electrónico | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroDocumento | 48 |
| 158 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208. Para realizar una Nota de Débito o Crédito con moneda distinta a la Factura la `<fechaEmision>` de la misma debe ser posterior a la aceptación de la Factura o Cuenta Corriente Asociada | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaEmision / codigoMoneda | 48 |
| 159 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208 perteneciente a Factura de Crédito Electrónica no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / periodoComprobantesAsociados | 48 |
| 160 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. Falta informar comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito válido/s o informar un período de comprobantes asociados válido | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAsociados | 48 |
| 161 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. No debe informar un período de comprobantes asociados cuando informa comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAsociados | 48 |
| 162 | Si `<codigoTipoComprobante>` es igual a 1, 2, 51, 201 ó 206 correspondientes a Facturas no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / periodoComprobantesAsociados | 48 |
| 163 | La cuit receptora se encuentra inactiva por haber sido inlcuída en la consulta de facturas apócrifas. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 48 |
| 165 | Si ocurrió un error imprevisto al momento de validar las actividades a quedar asociadas al comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 49 |
| 166 | Si `<codigo>` se encuentra mas de una vez en el array de actividades (no admite repetidos). Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 49 |
| 167 | Si `<codigo>` no se encuentra entre las actividades vigentes para la cuit representada. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 49 |
| 168 | Si `<codigo>` se encuentra asociado a un conjunto de actividades de un “rubro” y se encontraron otros `<codigo>` dentro del array que se encuentran asociados a otro conjunto de un “rubro” distinto. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 49 |
| 170 | Si ocurrio un error imprevisto al validar los comprobantes asociados que sean de tipo remito (88, 990, 91, 995, 997, 993, 994). Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 49 |
| 171 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y no fue encontrado en los registros de ARCA, o bien fue encontrado, pero la información asociada al mismo no es la esperada. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 49 |
| 172 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero el mismo se encuentra en un estado inválido. Dichos estados varian según el tipo de remito del que se trate. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 50 |
| 173 | Si el comprobante asociado es del tipo remito (91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero la cuit del receptor de dicho remito no coincide con la cuit del receptor del comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroDocumento / arrayComprobantesAsociados | 50 |
| 175 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y el tipo de comprobante asociado es remito, pero el mismo no es carnico (88, 990, 91, 997, 993, 994), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 50 |
| 176 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” o “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado o Tabaco en Hebras (91, 997, 993, 994, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 50 |
| 177 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado (990, 91, 997, 993, 994, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 51 |
| 178 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco en Hebras (88, 91, 997, 993, 994, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 51 |
| 180 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y el tipo de comprobante asociado es remito, pero el mismo no es Harina (88, 91, 997, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 51 |
| 181 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y no se especifico ningún Remito del tipo Harina (993 y 994), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza u observa según fechas de la RG | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 51 |
| 182 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y no se especifico ningún Remito del tipo Carnico (995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza u observa según fechas de la RG | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 52 |
| 183 | Los códigos de concepto permitidos para asociar Remitos Cárnicos (995) al Comprobante son 1 – Productos y 3 – Productos y Servicios | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoConcepto / arrayComprobantesAsociados | 52 |
| 184 | Si no se especifican actividades, y el Remito a Asociar es un Remito Sectorial (88, 990, 993, 994, 995, 997), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados arrayActividades | 52 |
| 185 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero se encuentra marcado como de exportación, mientras que el presente servicio solo acepta Remitos para el Mercado. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 52 |
| 186 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y ya fue declarado una vez en el array de comprobantes asociados. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / arrayComprobantesAsociados | 52 |
| 109 | Si `<codigoTipoDocumento>` es igual a 80, 86 o 87, `<numeroDocumento>` debe ser válido y activo, excepto para `<codigoTipoComprobante>` 6, 7 u 8, `<codigoTipoDocumento>` 80 y `<numeroDocumento>` igual a 23000000000. | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 53 |
| 130 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 la CUIT del receptor debe encontrarse activa en IVA o en monotributo | Observa | arrayObservaciones | `comprobanteCAERequest` › numeroDocumento | 53 |
| 134 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 y `<codigoTipoDocumento>` es igual a 80 (CUIT), dicha CUIT deberá encontrarse activa en el Sistema Registral | Observa | arrayObservaciones | `comprobanteCAERequest` › numeroDocumento | 53 |
| 150 | Falta incluir en la sección OtrosTributos la Percepción de IVA No Categorizado (ID 13), según lo dispuesto por la RG 2126/2006. Se debe informar el tributo `<codigo>` 13 (Percepción de IVA No Categorizado) con un `<importe>` mayor a 0 (cero) dentro del nodo `<arrayOtrosTributos>` cuando se cumplan simultáneamente las siguientes condiciones: - `<codigoTipoComprobante>` corresponda a un comprobante clase B (6, 7 u 8). - `<codigoTipoDocumento>` sea igual a 80 (CUIT). - `<numeroDocumento>` corresponda a 23000000000 (No Categorizado). - La suma de `<importeGravado>` más la sumatoria de los campos `<importe>` dentro de `<arraySubtotalesIVA>` sea mayor a 0 (cero). | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento / importeGravado / arraySubtotalesIVA / arrayOtrosTributos / importeOtrosTributos | 54 |
| 164 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 la CUIT del receptor es activa en monotributo | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 54 |
| 169 | Si `<cuitRepresentada>` tiene pendiente de presentación el formulario de habilitación de comprobantes o su fecha de presentación es anterior a tu alta en IVA | Observa | arrayObservaciones | `comprobanteCAERequest` › cuitRepresentada | 54 |
| 188 | Si `<numeroDocumento>` es inexistente en el padron del Organismo | Observa | arrayObservaciones | `comprobanteCAERequest` › numeroDocumento | 54 |
| 190 | Si no se informa la condición de IVA del Receptor (obligatoria) o bien se informa un valor no contemplado por el servicio. Ver método consultarCondicionesIVAReceptor | Observa | arrayObservaciones | `comprobanteCAERequest` › condicionIVAReceptor / fechaEmision | 55 |
| 191 | Si se informa una combinación invalida de Condición de IVA del Receptor y Tipo de Comprobante. Ver método consultarCondicionesIVAReceptor | Observa | arrayObservaciones | `comprobanteCAERequest` › condicionIVAReceptor / codigoTipoComprobante / fechaEmision | 55 |
| 194 | Siendo `<codigoTipoComprobante>` una Nota de Crédito (3, 8, 53, 203 y 208), si la sumatoria de los importes totales de los elementos del array `<arrayComprobantesAsociados>` (sin incluir Remitos) supera el `<importeTotal>` de la Nota de Crédito | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados / importeTotal | 55 |
| 253 | Si `<codigoTipoComprobante>` es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue inactivado o invalidado. | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 55 |
| 265 | Si `<codigoTipoComprobante>` es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 55 |
| 311 | Si el `<numeroDocumento>` del receptor/comprador se encuentra marcada como fallecido y no está marcado como sucesión indivisa. | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / numeroDocumento | 55 |
| 200 | Deberá ser igual a 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 1, 6 o 51 Deberá ser igual a 1, 2, 3, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 2 o 3. Deberá ser igual a 6, 7, 8, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 7 u 8. Deberá ser igual a 51, 52, 53, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 52 o 53. Deberá ser igual a 201, 202, 203, 88, 91, 990 o 995 si el tipo de comprobante cuya autorización se solicita es igual a 202 o 203. Deberá ser igual a 206, 207, 208, 88, 91, 990 o 995 si el tipo de comprobante cuya autorización se solicita es igual a 207 u 208. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante | 56 |
| 202 | El tipo de punto de venta, en caso de ser electrónico, deberá ser alguno de los siguientes: RECE para aplicativo y web services, Factura en Línea - Responsable Inscripto, Factura en Línea - Método Alternativo al RECE (límite de 100), Codificación de Productos - Web services, Codificación de Productos - Factura en Línea, CAEA - Fact. Elect. (RECE) - RI IVA o CAEA - Codificación de Productos. | Rechaza | arrayErrores | `comprobanteAsociado` › numeroPuntoVenta | 56 |
| 203 | Deberá ser igual a 1, 2, 3, 6, 7, 8, 51, 52, 53, 201, 202, 203, 206, 207, 208, 88, 91, 990 o 995. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante | 56 |
| 204 | El campo cuit es opcional y solo puede completarse si el tipo de comprobante es 88 o 990 (solo es necesario si el remito fue emitido por un tercero) | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / cuit | 57 |
| 205 | El remito asociado deberá obrar en las bases del organismo. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 57 |
| 206 | Si remito asociado corresponde a tabaco de terceros, deberá estar en estado Confirmado | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 57 |
| 207 | El receptor del remito asociado deberá conicidir con el receptor del comprobante | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 57 |
| 208 | Deberá ser igual a 88, 91, 990 o 995 si el tipo de comprobante cuya autorización se solicita es igual a 201 o 206 | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante | 57 |
| 209 | Al autorizar una nota de débito o crédito de Factura Electrónica de Crédito MiPyME (202, 203, 207, 208), debe enviar el campo cuit para el tipo de comprobante asociado indicado | Rechaza | arrayErrores | `comprobanteAsociado` › cuit | 57 |
| 210 | Al autorizar una nota de débito o crédito de Factura Electrónica de Crédito MiPyME (202, 203, 207, 208), el campo cuit para el tipo de comprobante asociado indicado debe coincidir con la cuit emisora del comprobante a autorizar | Rechaza | arrayErrores | `comprobanteAsociado` › cuit | 57 |
| 211 | Si el punto de venta es del tipo electrónico el comprobante asociado `<codigoTipoComprobante>` `<numeroPuntoVenta>` `<numeroComprobante>` deberá obrar en las bases del organismo. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante | 57 |
| 212 | Al autorizar una nota de débito o crédito de Factura Electrónica de Crédito MiPyME (202, 203, 207, 208), debe haber un y sólo un comprobante asociado de Factura Electrónica de Crédito MiPyME: - 201 o 206, para NO anulación - 201, 202, 203, 206, 207 o 208, para Anulación | Rechaza | arrayErrores | `comprobanteAsociado` › arrayComprobantesAsociados | 57 |
| 213 | Para CUITS Emisoras y Receptoras candidatas al Régimen de Factura Electrónica de Crédito, al autorizar una nota de débito o crédito de Factura Electrónica (2, 3, 7, 8, 52, 53), debe haber al menos un comprobante asociado de Factura Electrónica (1, 2, 3, 6, 7, 8, 51, 52 o 53) | Rechaza | arrayErrores | `comprobanteAsociado` › arrayComprobantesAsociados | 58 |
| 214 | Si está presente el dato adicional código 22 en S (es una nota de anulación): - Si el tipo de comprobante a autorizar es una nota de crédito (203 o 208) el tipo de comprobante asociado a revertir debe ser 201, 202, 206 ó 207 Si el tipo de comprobante a autorizar es una nota de débito (202 o 207) el tipo de comprobante asociado a revertir debe ser 203 ó 208 | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante | 58 |
| 215 | Si está presente el dato adicional código 22 en N (NO es una nota de anulación), debe existir un comprobante asociado del tipo 201 o 206. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante | 58 |
| 216 | Si el comprobante a autorizar es de Anulación, el comprobante asociado debe haber sido rechazado por el comprador mediante el Sistema de Regitro de Facturas Electrónicas de Crédito MiPyME. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 58 |
| 217 | Si el comprobante a autorizar NO es de Anulación, el comprobante asociado NO debe haber sido rechazado por el comprador mediante el Sistema de Regitro de Facturas Electrónicas de Crédito MiPyME. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 58 |
| 218 | Al autorizar un comprobante de Factura Electrónica de Crédito MiPyME (201, 202, 203, 206, 207, 208), debe enviar el campo fechaEmision para el comprobante asociado del tipo Remito | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 59 |
| 219 | La fecha de emisión del comprobante asociado no puede ser posterior a la fecha del comprobante a autorizar | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 59 |
| 220 | La fecha de emisión del comprobante asociado informada no coincide con la existente en nuestros registros | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 59 |
| 221 | La fecha de emisión de este comprobante no puede ser anterior a la factura asociada | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 59 |
| 222 | El comprobante asociado no posee cuit del receptor | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 59 |
| 223 | El comprobante asociado posee otro cuit de receptor | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 59 |
| 224 | Si el punto de venta del comprobante asociado NO es del tipo electrónico debe informar la fecha de emisión | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 59 |
| 225 | Si el punto de venta del comprobante asociado NO es del tipo electrónico la fecha de emisión no puede ser posterior a la fecha de la autorización | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 59 |
| 342 | Si `<numeroDocumento>` es la CUIT para Sujeto No Categorizado (23000000000) y el `<codigoTipoDocumento>` es distinto de B (6, 7 u 8) o si la `<condicionIVAReceptor>` no se corresponde con Sujeto No Categorizado | Rechaza | arrayErrores | `comprobanteAsociado` › numeroDocumento / codigoTipoDocumento / condicionIVAReceptor | 59 |
| 349 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT), se debe validar que el número ingresado esté registrado en el padrón del Organismo y se encuentre en estado activo para dicho tipo de documento. Esta validación aplica para comprobantes: - Comprobantes Clase A - Comprobantes Clase B. Donde `<numeroDocumento>` es distinto de la CUIT de Sujeto No Categorizado y no sea una CUIT Pais, y el `<importeTotal>` del comprobante (pesificado al tipo de cambio informado en caso de moneda extranjera) sea igual o mayor a $10.000.000. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoDocumento / numeroDocumento / codigoTipoComprobante | 60 |
| 2200 | La fechaHasta debe ser posterior o igual fechaDesde | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 60 |
| 2201 | La fechaHasta del periodoComprobantesAsociados debe ser anterior o igual a la fecha de emisión del comprobante por el cual se está solicitando la autorización | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaHasta / fechaEmision | 61 |
| 2202 | Si el comprobante a autorizar incluye percepciones, el rango de fecha informado debe corresponder al mismo Mes/Año | Observa | arrayObservaciones | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 61 |
| 300 | Valores permitidos: consultar método consultarTiposTributo | Rechaza | arrayErrores | `otroTributo` › codigo | 62 |
| 301 | Opcional. Deberá informarse si `<codigo>` es igual a 99 | Rechaza | arrayErrores | `otroTributo` › descripcion | 62 |
| 302 | Es obligatorio ingresar una Descripción al realizar un tipo de comprobante de Factura Electrónica de Crédito MiPyME | Rechaza | arrayErrores | `otroTributo` › descripcion | 62 |
| 400 | Valores permitidos: 4, 5, 6 | Rechaza | arrayErrores | `subtotalIVA` › codigo | 62 |
| 401 | Para comprobantes clase “A” o “A con leyenda OPERACIÓN SUJETA A RETENCIÓN”: Deberá coincidir con la sumatoria de todos los `<importeIVA>` de `<item>` donde la alícuota de IVA coincida con la indicada, es decir, donde `<codigoCondicionIVA>` de `<item>` = `<codigo>` de `<subtotalIVA>`. Para comprobantes clase “B”: Deberá coincidir con la sumatoria de todos los importes IVA calculados en base al importe y alícuota IVA de `<item>` donde la alícuota de IVA coincida con la indicada, es decir, donde `<codigoCondicionIVA>` de `<item>` = `<codigo>` de `<subtotalIVA>`. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems con igual código de alícuota de IVA * | Rechaza | arrayErrores | `subtotalIVA` › importe | 62 |
| 402 | No se deberá repetir (no pueden incluírse dos subtotales IVA con el mismo código) | Rechaza | arrayErrores | `subtotalIVA` › codigo | 63 |
| 403 | Si existen uno o más ítems con una determinada alícuota IVA, deberá existir el correspondiente subtotal IVA para dicha alícuota. No se sebe incluír un subtotal IVA si dicha alícuota no está presente en al menos un ítem. | Rechaza | arrayErrores | `subtotalIVA` › codigo | 63 |
| 405 | La suma de los subtotales de IVA no puede ser negativa. | Rechaza | arrayErrores | `subtotalIVA` › importe | 63 |
| 504 | Si `<codigoMtx>` no se corresponde con un GTIN registrado, activo y vigente, el comprobante quedara observado. | Observa | arrayObservaciones | `item` › codigoMtx | 63 |
| 500 | Opcional si `<codigoUnidadMedida>` es 99 ó 97, para el resto de los casos es obligatorio. | Rechaza | arrayErrores | `item` › unidadesMtx | 63 |
| 501 | De informarse deberá ser mayor o igual a 1 (uno) | Rechaza | arrayErrores | `item` › unidadesMtx | 64 |
| 502 | Longitud máxima 6 posiciones. | Rechaza | arrayErrores | `item` › unidadesMtx | 64 |
| 503 | Opcional si `<codigoUnidadMedida>` es 99 ó 97, para el resto de los casos es obligatorio. | Rechaza | arrayErrores | `item` › codigoMtx | 64 |
| 505 | Opcional. Longitud máxima 50 posiciones. | Rechaza | arrayErrores | `item` › codigo | 64 |
| 506 | Cantidad máxima de caracteres permitidos es 4000. Importante: no es necesario (ni recomendable) completar con espacios. | Rechaza | arrayErrores | `item` › descripcion | 64 |
| 507 | No corresponde para `<codigoUnidadMedida>` igual a 99 o 97. En otro caso es obligatorio. | Rechaza | arrayErrores | `item` › cantidad | 64 |
| 508 | Deberá ser alguno de los valores permitidos: consultar método consultarUnidadesMedida | Rechaza | arrayErrores | `item` › codigoUnidadMedida | 64 |
| 509 | No corresponde para `<codigoUnidadMedida>` igual a 99 o 97. En otro caso es obligatorio. | Rechaza | arrayErrores | `item` › precioUnitario | 64 |
| 510 | Opcional. No corresponde para `<codigoUnidadMedida>` igual a 99 o 97. | Rechaza | arrayErrores | `item` › importeBonificacion | 64 |
| 511 | De informarse deberá ser menor o igual a `<precioUnitario>`*`<cantidad>` | Rechaza | arrayErrores | `item` › importeBonificacion | 64 |
| 512 | Deberá coincidir con alguno de los valores permitidos: consultar método consultarCondicionesIVA | Rechaza | arrayErrores | `item` › codigoCondicionIVA | 64 |
| 513 | Si `<codigoUnidadMedida>` es 99 deberá existir por lo menos otro ítem con igual `<codigoCondicionIVA>` y `<codigoUnidadMedida>` distinta a la informada para este ítem. | Rechaza | arrayErrores | `item` › codigoCondicionIVA / codigoUnidadMedida | 64 |
| 514 | Obligatorio si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53. No corresponde para `<codigoTipoComprobante>` igual a 6, 7 u 8. | Rechaza | arrayErrores | `item` › importeIVA | 64 |
| 515 | Para `<codigoTipoComprobante>` igual a 1, 2 ó 3 y unidad de medida distinto a 95, 97 o 99, deberá ser igual a (`<precioUnitario>` * `<cantidad>` -`<importeBonificacion>`) * alícuota de IVA correspondiente. Para `<codigoTipoComprobante>` igual a 1, 2, 3, 51, 52 ó 53 y unidad de medida igual a 95 deberá ser igual a (-1) * (`<precioUnitario>` * `<cantidad>` - `<importeBonificacion>`) * alícuota de IVA correspondiente. Para `<codigoTipoComprobante>` igual a 1, 2, 3, 51, 52 ó 53 y unidad de medida igual a 97 o 99, deberá ser igual a `<importeItem>` - `<importeItem>` / (1 + alícuota de IVA correspondiente). El error relativo porcentual deberá ser <= 0.01% o el error absoluto <= 0.01 * | Rechaza | arrayErrores | `item` › importeIVA | 65 |
| 516 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 y `<codigoUnidadMedida>` es 99, el valor absoluto de la sumatoria de los importes ingresados para este campo no puede superar a la sumatoria de los importes `<importeIVA>` informado con la misma alícuota. El error relativo porcentual deberá ser <= 0.01% o el error absoluto <= 0.01 * | Rechaza | arrayErrores | `item` › importeIVA | 65 |
| 517 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 y `<codigoUnidadMedida>` es: - 99 deberá ser menor o igual a 0 (cero), - 97 podrá ser menor, mayor o igual a 0 (cero). - 95 deberá ser menor o igual a 0 (cero), - Cualquier otro caso deberá ser mayor o igual a 0 (cero). | Rechaza | arrayErrores | `item` › importeIVA | 65 |
| 518 | Si `<codigoUnidadMedida>` es: - 99 deberá ser menor a 0 (cero), - 97 podrá ser menor, o mayor igual a 0 (cero). - 95 deberá ser menor a 0 (cero), - Cualquier otro caso deberá ser mayor o igual a 0 (cero). | Rechaza | arrayErrores | `item` › importeItem | 66 |
| 519 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 y `<codigoUnidadMedida>` es distinto a 95, 97 ó 99, deberá ser igual a (`<precioUnitario>` sin IVA * `<cantidad>` -`<importeBonificacion>`)*(1+alícuota). Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 y `<codigoUnidadMedida>` es igual a 95 ser igual a (-1) * (`<precioUnitario>` sin IVA * `<cantidad>` -`<importeBonificacion>`)*(1+alícuota). Si `<codigoTipoComprobante>` es igual a 6, 7 u 8 y `<codigoUnidadMedida>` es distinto a 95, 97 ó 99 deberá ser igual a (`<precioUnitario>` con IVA * `<cantidad>` -`<importeBonificacion>`). Si `<codigoTipoComprobante>` es igual a 6, 7 u 8 y `<codigoUnidadMedida>` es igual a 95 ser igual a (-1) * (`<precioUnitario>` con IVA * `<cantidad>` -`<importeBonificacion>`). En ambos casos el error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * | Rechaza | arrayErrores | `item` › importeItem | 66 |
| 520 | Si se informa el campo `<unidadesMtx>` entonces debe informarse el campo `<codigoMtx>` y viceversa. | Rechaza | arrayErrores | `item` › unidadesMtx / codigoMtx | 66 |
| 521 | Si `<codigoCondicionIVA>` es igual a 1, 2 ó 3 entonces `<importeIVA>` deberá ser igual a 0 (cero). | Rechaza | arrayErrores | `item` › importeIVA | 66 |
| 320 | Valores permitidos: consultar método consultarTiposDatosAdicionales | Rechaza | arrayErrores | `datoAdicional` › t | 67 |
| 321 | Si t es igual a 2 (“Dato Adicional para Empresas Promovidas”), en c1 se deberá indicar el id de proyecto (el mismo deberá corresponder a la cuit emisora del comprobante) o cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen de Promoción Industrial. Los campos c2 a c6 no deberán informarse (reservados para uso futuro) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 67 |
| 323 | Si t es igual a: 11(“Dato Adicional para Operaciones Económicas Relacionadas con Bienes Inmuebles”) 12(“Dato Adicional para Locacion temporaria de Inmuebles con fines Turisticos”) 13(“Dato Adicional para Representantes de Modelos”) 14 (“Dato Adicional para Agencias de Publicidad”) 15 (“Dato Adicional para Personas Físicas que desarrollen actividad de Modelaje”) En c1 se deberá indicar cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen o 1 (uno) en caso de que la actividad facturada esté alcanzada por el Régimen. Los campos c2 a c6 no deberán informarse (reservados para uso futuro) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 67 |
| 324 | Si t es igual a 10 (“Dato Adicional para Educación Pública de Gestión Privada”) En c1 se deberá indicar cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen o 1 (uno) en caso de que la actividad facturada esté alcanzada por el Régimen. Si se informa el campo c1 igual a 1(uno) debe informar en el campo c2 el Tipo de Documento y en el campo c3 el Numero de Documento (los mismos corresponden a los identificadores 10.11 y 10.12 respectivamente segun la R.G. 4291 - Anexo (art. 15, 17 y 19), 1 - Establecimientos de educación publica de gestion privadas ). Los campos c4 a c6 no deberán informarse (reservados para uso futuro) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 68 |
| 325 | Si t es igual a 10 (“Dato Adicional para Educación Pública de Gestión Privada”) y c1 igual a 1(uno). En c2 debe informar alguno de los valores permitidos: consultar método consultarTiposDocumento. Si se indica c2 con 80, 86 ú 87 (CUIT, CUIL y CDI respectivamente) el número informado en c3 deberá obrar en las bases del organismo. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 68 |
| 322 | No se puede incluír más de un dato adicional (sólo se permite un id por comprobante) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 326 | Los tipos de dato adicional 21, 22 o 23 sólo corresponden a comprobantes de Factura Electrónica de Crédito MiPyME | Rechaza | arrayErrores | `datoAdicional` › t | 69 |
| 327 | Para el tipo de dato adicional 22, Anulación, debe indicar en el campo c1 S (si) si es de anulación o N (no) si no es de anulación | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 328 | Para el tipo de dato adicional 21, CBU y Alias del Emisor, el CBU informado en el campo c1 no corresponde al Emisor según nuestros registros | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 329 | Si el tipo de Comprobante a autorizar es 202, 203, 207 o 208, debe indicar el dato adicional código 22, Anulación, para indicar si este es un comprobante de anulación o no | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 330 | Si el tipo de Comprobante a autorizar es 201 o 206, NO debe indicar el dato adicional código 22, Anulación. No corresponde a un comprobante Factura. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 331 | Si el tipo de Comprobante a autorizar es 201 o 206, debe indicar el dato adicional código 21, CBU y Alias emisor. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 332 | Si el tipo de Comprobante a autorizar es 202, 203, 207 o 208, NO debe indicar el dato adicional código 21, CBU y Alias emisor. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 333 | Para el tipo de dato adicional 21, 22 y 23, debe indicar el campo c1 | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 334 | Para el tipo de dato adicional 27, Opción de Transferencia, las opciones válidas son ADC para Agente de Depósito Colectivo o SCA para Sistema de Circulación Abierta | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 335 | Si el tipo de Comprobante a autorizar es 201 o 206, debe indicar el dato adicional código 27, Opción de Transferencia. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 336 | Si el tipo de Comprobante a autorizar es 202, 203, 207 o 208, NO debe indicar el dato adicional código 27, Opción de Transferencia. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 69 |
| 337 | Si el tipo de Comprobante a autorizar NO es 1, 2, 3, 201, 202, 203, NO debe indicar el dato adicional código 5, Motivo de Excepcion - Cómputo IVA Crédito Fiscal. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 70 |
| 338 | Si el tipo de Comprobante a autorizar es 1, 2, 3, 201, 202, 203, y se indica el dato adicional código 5, se debera indicar el campo c1 (Motivo de Excepcion) de forma obligatoria. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 70 |
| 339 | Si el tipo de Comprobante a autorizar es 1, 2, 3, 201, 202, 203, y se indica el dato adicional código 5, y el campo el campo c1 (Motivo de Excepcion) NO es un numérico del 1 al 6. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 70 |
| 340 | Si el tipo de Comprobante a autorizar es 1, 2, 3, 201, 202, 203, y se indica el dato adicional código 5, y no se deberán utilizar ninguno de los restantes campos reservados a futuro campos de c2 a c6. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 70 |
| 420 | Si se informar el grupo de compradores debe tener mas de un comprador | Rechaza | arrayErrores | `comprador` › arrayCompradores | 70 |
| 421 | Si se informa el grupo de compradores, el tipo y número de documento del Receptor es obligatorio. Cuando se informan compradores múltiples, el que se indique con mayor porcentaje deberá figurar como receptor del comprobante. En caso de no haber un único comprador con porcentaje mayor, debe informar uno de ellos. | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 70 |
| 422 | El tipo de documento de los compradores debe ser CUIT, CUIL o CDI | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento | 71 |
| 423 | Número de documento informado repetido. Sólo Se debe informar una vez al comprador | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 71 |
| 424 | El Porcentaje de Titularidad del Comprador debe ser mayor a 0 (cero) | Rechaza | arrayErrores | `comprador` › porcentaje | 71 |
| 425 | El Porcentaje de Titularidad del Comprador debe ser menor a 100 (cien) | Rechaza | arrayErrores | `comprador` › porcentaje | 71 |
| 426 | El Emisor del comprobante no puede ser comprador | Rechaza | arrayErrores | `comprador` › porcentaje | 71 |
| 427 | La suma de los porcentajes indicados en la lista de compradores debe ser igual a 100 | Rechaza | arrayErrores | `comprador` › porcentaje | 71 |
| 428 | El receptor del comprobante debe incluírse con el mismo tipo y número de documento en el grupo de compradores | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 71 |
| 429 | El receptor del comprobante (tipo y número de documento) debe coincidir con el comprador que tenga el mayor porcentaje en la lista de compradores. En caso de no haber un único comprador con porcentaje mayor, deberá coincidir con uno de ellos | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento / porcentaje | 71 |
| 430 | Las CUIT/CUIL/CDI de los compradores deberán encontrarse activas en el Sistema Registral | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 71 |
| 431 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 las CUITs de los compradores deben encontrarse activa en IVA o en monotributo. | Rechaza | arrayErrores | `comprador` › codigoTipoComprobante / numeroDocumento | 71 |
| 432 | Sólo se puede informar el arrayCompradores para codigoConcepto igual a 1 (Productos) | Rechaza | arrayErrores | `comprador` › arrayCompradores / codigoConcepto | 72 |
| 433 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208, no puede informar compradores múltiples. | Rechaza | arrayErrores | `comprador` › arrayCompradores / codigoTipoComprobante | 72 |


### consultarUltimoComprobanteAutorizado (3)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 1500 | Podrá ser: 1 – Factura A 2 – Nota de Débito A 3 – Nota de Crédito A 6 – Factura B 7 – Nota de Débito B 8 – Nota de Crédito B 51 – Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN 52 – Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 – Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN Consultar método consultarTiposComprobante | Rechaza | arrayErrores | codigoTipoComprobante | 253 |
| 1501 | Debe ser del tipo habilitado para el régimen CAE Codificación de Productos – Web Services ó del régimen CAEA. Consultar método consultarPuntosVenta, consultarPuntosVentaCAE o consultarPuntosVentaCAEA. | Rechaza | arrayErrores | numeroPuntoVenta | 253 |
| 1502 | Debe obrar en las bases del organismo al menos un comprobante emitido con el tipo de comprobante y punto de ventas indicados. | Rechaza | arrayErrores | codigoTipoComprobante / numeroPuntoVenta | 253 |


### consultarComprobante (3)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 1500 | Podrá ser: 1 – Factura A 2 – Nota de Débito A 3 – Nota de Crédito A 6 – Factura B 7 – Nota de Débito B 8 – Nota de Crédito B 51 – Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN 52 – Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 – Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN Consultar método consultarTiposComprobante | Rechaza | arrayErrores | `consultaComprobanteRequest` › codigoTipoComprobante | 266 |
| 1501 | Debe ser del tipo habilitado para el régimen CAE Codificación de Productos – Web Services ó del régimen CAEA. Consultar método consultarPuntosVenta, consultarPuntosVentaCAE o consultarPuntosVentaCAEA. | Rechaza | arrayErrores | `consultaComprobanteRequest` › numeroPuntoVenta | 266 |
| 1503 | Deberá obrar en las bases del organismo un comprobante con el tipo, punto de venta y número de comprobante indicados. | Rechaza | arrayErrores | `consultaComprobanteRequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante | 267 |


### consultarCondicionesIVAReceptor (1)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 196 | En caso de no estar contemplado dentro de los tipos de comprobantes validos para el servicio | Rechaza | arrayErrores | codigoTipoComprobante | 292 |


### consultarCotizacionMoneda (1)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 1600 | Deberá coincidir con alguno de los códigos de moneda disponibles. Consultar método consultarMonedas | Rechaza | arrayErrores | codigoMoneda | 301 |


### autorizarAjusteIVA (133)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10010 | Debe encontrarse empadronado en Codificación de Productos - opción Factura con Detalle | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 89 |
| 136 | Podrá ser: 2 – Nota de Débito A 3 – Nota de Crédito A 7 – Nota de Débito B 8 – Nota de Crédito B 52 – Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 – Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante | 89 |
| 136 | El contribuyente no se encuentra habilitado a emitir (según el tipo de comprobante indicado) comprobantes A, A con Leyenda o A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / cuitRepresentada | 89 |
| 101 | Debe ser del tipo habilitado para el régimen CAE Codificación de Productos – Web Services y no debe estar bloqueado. Consultar método consultarPuntosVenta o consultarPuntosVentaCAE | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroPuntoVenta | 90 |
| 102 | El número de comprobante informado debe ser mayor en 1 al último informado para igual punto de venta y tipo de comprobante. De no existir comprobante informado para igual punto de venta y codigoTipoComprobante, el número de comprobante debe ser igual a 1 (uno) | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 90 |
| 103 | Opcional. Para `<codigoConcepto>` igual a 1, la fecha de emisión del comprobante puede ser hasta 5 días anteriores o posteriores respecto de la fecha de generación, pero sin extenderse al mes siguiente; si se indica `<codigoConcepto>` igual a 2 ó 3 puede ser hasta 10 días anteriores o posteriores a la fecha de generación Obs.: Si no se envía se le asignará la fecha de proceso. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaEmision | 90 |
| 104 | La fecha de emisión debe ser mayor o igual a la fecha de emisión del último comprobante del mismo tipo e igual número de punto de venta. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaEmision / numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 90 |
| 105 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoAutorizacion | 90 |
| 106 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoAutorizacion | 90 |
| 107 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimiento | 90 |
| 108 | Si se informa uno de los campos debe informarse el otro. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 91 |
| 137 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › importeGravado | 91 |
| 138 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › importeNoGravado | 91 |
| 139 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › importeExento | 91 |
| 140 | Deberá informarse en 0 (cero) | Rechaza | arrayErrores | `comprobanteCAERequest` › importeSubtotal | 91 |
| 141 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › importeOtrosTributos | 91 |
| 142 | Debe ser igual a la sumatoria de `<subtotalIVA>``<importe>` (dentro del arraySubtotalesIVA). | Rechaza | arrayErrores | `comprobanteCAERequest` › importeTotal | 91 |
| 143 | Debe ser igual a la sumatoria de la totalidad de los campos `<importeItem>`. | Rechaza | arrayErrores | `comprobanteCAERequest` › importeTotal | 91 |
| 117 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarMonedas | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoMoneda | 91 |
| 118 | En caso de enviar la marca de que el pago del comprobante se realiza en la misma moneda extranjera para comprobantes que no sean facturas. Unicamente se puede utilizar con los códigos habilitados (1,6,51,201,206) | Rechaza | arrayErrores | `comprobanteCAERequest` › cancelaEnMismaMonedaE xtranjera | 91 |
| 119 | No podrá ser inferior al 2% ni superior en un 400% del que suministra ARCA como orientativo de acuerdo a la cotización oficial | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 91 |
| 120 | Debe ser igual a 1 (uno) si `<codigoMoneda>` es igual a PES | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 91 |
| 164 | En caso de enviar un valor inválido para la marca de que el pago de la factura se realiza en la misma moneda extranjera. Los valores válidos son S, N o vacío | Rechaza | arrayErrores | `comprobanteCAERequest` › cancelaEnMismaMonedaE xtranjera | 92 |
| 169 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera y enviar como código de moneda el Peso Argentino | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoMoneda / cancelaEnMismaMonedaE xtranjera | 92 |
| 192 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera, que codigoMoneda es del grupo de monedas con cotización del Banco de la Nación Argentina (ver Anexo Monedas BNA), que haya cotización y que la misma no coincida exactamente con el valor enviado en el campo cotizacionMoneda. En cuyo caso se podrá omitir el mismo para que la cotización de la factura sea la obtenida de los registros de ARCA | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoMoneda / cotizacionMoneda / cancelaEnMismaMonedaE xtranjera | 92 |
| 194 | El campo es obligatorio a excepción de los casos para los cuales se envia el campo cancelaEnMismaMonedaExtranjera y se puede obtener la cotizacion asociada al codigoMoneda si esta es del grupo de monedas del Banco de la Nación Argentina (ver Anexo Monedas BNA) | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 92 |
| 195 | No es posible indicar una cotización negativa | Rechaza | arrayErrores | `comprobanteCAERequest` › cotizacionMoneda | 92 |
| 121 | Deberá ser igual a alguno de los siguientes valores: 1 – Productos 2 – Servicios 3 – Productos y Servicios | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoConcepto | 93 |
| 122 | Opcional. Debe informarse si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaServicioDesde | 93 |
| 123 | Opcional. Debe informarse si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaServicioHasta | 93 |
| 124 | Opcional. Debe informarse si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimientoPago | 93 |
| 125 | La fecha de vencimiento de pago debe ser posterior o igual a la fecha de emisión. | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaVencimientoPago / fechaEmision | 93 |
| 144 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAERequest` › arrayOtrosTributos | 93 |
| 127 | Debe informarse si algún ítem tiene `<codigoCondicionIVA>` igual a 4, 5 ó 6. | Rechaza | arrayErrores | `comprobanteCAERequest` › arraySubtotalesIVA | 93 |
| 128 | Opcionales. Deberán informarse en los siguientes casos: - cuando `<codigoTipoComprobante>` es igual a 2, 3, 52 ó 53. -cuando `<codigoTipoComprobante>` es igual a 7 u 8 y el importe total del comprobante `<importeTotal>` es mayor ó igual al monto en pesos resultante según RG4444. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 93 |
| 129 | Si `<codigoTipoComprobante>` es igual a 2, 3, 52 ó 53.`<codigoTipoDocumento>` deberá ser igual a 80 (CUIT) | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento | 94 |
| 131 | El Receptor no puede ser igual al Emisor | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroDocumento | 94 |
| 132 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarTiposDocumento | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento | 94 |
| 133 | La Fecha de Servicio desde debe ser menor o igual a la Fecha de Servicio Hasta | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaServicioDesde / fechaServicioHasta | 94 |
| 135 | Solicitudes de autorización para un mismo punto de venta y tipo de comprobante deben ser enviadas en forma sincrónica: si el WS recibe una nueva solicitud para un punto de venta y tipo de comprobante dado mientras la anterior está siendo procesada, la nueva solicitud será rechazada | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroPuntoVenta / codigoTipoComprobante | 94 |
| 146 | La fecha/hora de generación solo debe informarse para comprobantes CAEA | Rechaza | arrayErrores | `comprobanteCAERequest` › fechaHoraGen | 94 |
| 159 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208 perteneciente a Factura de Crédito Electrónica no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / periodoComprobantesAso ciados | 94 |
| 160 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. Falta informar comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito válido/s o informar un período de comprobantes asociados válido | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAso ciados | 95 |
| 161 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. No debe informar un período de comprobantes asociados cuando informa comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAso ciados | 95 |
| 162 | Si `<codigoTipoComprobante>` es igual a 1, 2, 51, 201 ó 206 correspondientes a Facturas no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / periodoComprobantesAso ciados | 95 |
| 165 | Si ocurrió un error imprevisto al momento de validar las actividades a quedar asociadas al comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 95 |
| 261 | Si `<codigoTipoComprobante>` NO es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue inactivado o invalidado. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 95 |
| 297 | Si `<codigoTipoComprobante>` NO es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 96 |
| 304 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido marcada como Apocrifa. | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 96 |
| 266 | Si `<codigo>` se encuentra mas de una vez en el array de actividades (no admite repetidos). Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 96 |
| 267 | Si `<codigo>` no se encuentra entre las actividades vigentes para la cuit representada. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 96 |
| 268 | Si `<codigo>` se encuentra asociado a un conjunto de actividades de un “rubro” y se encontraron otros `<codigo>` dentro del array que se encuentran asociados a otro conjunto de un “rubro” distinto. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigo / arrayActividades | 96 |
| 270 | Si ocurrio un error imprevisto al validar los comprobantes asociados que sean de tipo remito (88, 990, 91, 995, 997, 993, 994). Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 97 |
| 271 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y no fue encontrado en los registros de ARCA, o bien fue encontrado, pero la información asociada al mismo no es la esperada. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 97 |
| 272 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero el mismo se encuentra en un estado inválido. Dichos estados varian según el tipo de remito del que se trate. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 97 |
| 273 | Si el comprobante asociado es del tipo remito (91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero la cuit del receptor de dicho remito no coincide con la cuit del receptor del comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › numeroDocumento / arrayComprobantesAsociados | 97 |
| 275 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y el tipo de comprobante asociado es remito, pero el mismo no es carnico (88, 990, 91, 997, 993, 994), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 98 |
| 276 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” o “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado o Tabaco en Hebras (91, 997, 993, 994, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 98 |
| 277 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado (990, 91, 997, 993, 994, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 98 |
| 278 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco en Hebras (88, 91, 997, 993, 994, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 99 |
| 280 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y el tipo de comprobante asociado es remito, pero el mismo no es Harina (88, 91, 997, 995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 99 |
| 281 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y no se especifico ningún Remito del tipo Harina (993 y 994), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza u observa según fechas de la RG | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 99 |
| 282 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y no se especifico ningún Remito del tipo Carnico (995), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza u observa según fechas de la RG | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 99 |
| 283 | Los códigos de concepto permitidos para asociar Remitos Cárnicos (995) al Comprobante son 1 – Productos y 3 – Productos y Servicios | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoConcepto / arrayComprobantesAsociados | 100 |
| 284 | Si no se especifican actividades, y el Remito a Asociar es un Remito Sectorial (88, 990, 993, 994, 995, 997), se rechazara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados arrayActividades | 100 |
| 285 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero se encuentra marcado como de exportación, mientras que el presente servicio solo acepta Remitos para el Mercado. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 100 |
| 286 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y ya fue declarado una vez en el array de comprobantes asociados. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAERequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / arrayComprobantesAsociados | 100 |
| 109 | Si `<codigoTipoDocumento>` es igual a 80, 86 o 87, `<numeroDocumento>` debe ser válido y activo, excepto para `<codigoTipoComprobante>` 6, 7 u 8, `<codigoTipoDocumento>` 80 y `<numeroDocumento>` igual a 23000000000. | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 101 |
| 130 | Si `<codigoTipoComprobante>` es igual a 2, 3, 52 ó 53 la CUIT del receptor debe encontrarse activa en IVA o en monotributo. | Observa | arrayObservaciones | `comprobanteCAERequest` › numeroDocumento | 101 |
| 134 | Si `<codigoTipoComprobante>` es igual a 2, 3, 52 ó 53 y `<codigoTipoDocumento>` es igual a 80 (CUIT), dicha CUIT deberá encontrarse activa en el Sistema Registral | Observa | arrayObservaciones | `comprobanteCAERequest` › numeroDocumento | 101 |
| 164 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 la CUIT del receptor es activa en monotributo | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoDocumento / numeroDocumento | 101 |
| 187 | Si `<cuitRepresentada>` tiene pendiente de presentación el formulario de habilitación de comprobantes o su fecha de presentación es anterior a tu alta en IVA | Observa | arrayObservaciones | `comprobanteCAERequest` › cuitRepresentada | 101 |
| 189 | Si `<numeroDocumento>` es inexistente en el padron del Organismo | Observa | arrayObservaciones | `comprobanteCAERequest` › numeroDocumento | 101 |
| 195 | Siendo `<codigoTipoComprobante>` una Nota de Crédito (3, 8, 53, 203 y 208), si la sumatoria de los importes totales de los elementos del array `<arrayComprobantesAsociados>` (sin incluir Remitos) supera el `<importeTotal>` de la Nota de Crédito | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / arrayComprobantesAsociados / importeTotal | 101 |
| 261 | Si `<codigoTipoComprobante>` es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue inactivado o invalidado. | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 102 |
| 297 | Si `<codigoTipoComprobante>` es 3, 8, 53, 203 o 208 (Nota de Crédito), `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. | Observa | arrayObservaciones | `comprobanteCAERequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento | 102 |
| 312 | Si el `<numeroDocumento>` del receptor/comprador se encuentra marcada como fallecido y no está marcado como sucesión indivisa. | Observa | arrayObservaciones | `comprobanteCAERequest` › numeroDocumento | 102 |
| 290 | Si no se informa la condición de IVA del Receptor (obligatoria) o bien se informa un valor no contemplado por el servicio. Ver método consultarCondicionesIVAReceptor | Observa | arrayObservaciones | `comprobanteCAERequest` › condicionIVAReceptor / fechaEmision | 102 |
| 291 | Si se informa una combinación invalida de Condición de IVA del Receptor y Tipo de Comprobante. Ver método consultarCondicionesIVAReceptor | Observa | arrayObservaciones | `comprobanteCAERequest` › condicionIVAReceptor / codigoTipoComprobante / fechaEmision | 102 |
| 200 | Deberá ser igual a 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 1, 6 o 51 Deberá ser igual a 1, 2, 3, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 2 o 3. Deberá ser igual a 6, 7, 8, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 7 u 8. Deberá ser igual a 51, 52, 53, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 52 o 53. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante | 102 |
| 202 | El tipo de punto de venta, en caso de ser electrónico, deberá ser alguno de los siguientes: RECE para aplicativo y web services, Factura en Línea - Responsable Inscripto, Factura en Línea - Método Alternativo al RECE (límite de 100), Codificación de Productos - Web services, Codificación de Productos - Factura en Línea, CAEA - Fact. Elect. (RECE) - RI IVA o CAEA - Codificación de Productos. | Rechaza | arrayErrores | `comprobanteAsociado` › numeroPuntoVenta | 103 |
| 203 | Deberá ser igual a 1, 2, 3, 6, 7, 8, 51, 52, 53, 88 o 990. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante | 103 |
| 204 | El campo cuit es opcional y solo puede completarse si el tipo de comprobante es 88 o 990 (solo es necesario si el remito fue emitido por un tercero) | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / cuit | 103 |
| 205 | El remito asociado deberá obrar en las bases del organismo. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 103 |
| 206 | Si remito asociado corresponde a tabaco de terceros, deberá estar en estado Confirmado | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 103 |
| 207 | El receptor del remito asociado deberá conicidir con el receptor del comprobante | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 103 |
| 220 | La fecha de emisión del comprobante asociado informada no coincide con la existente en nuestros registros | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 103 |
| 221 | La fecha de emisión de este comprobante no puede ser anterior a la factura asociada | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 104 |
| 222 | El comprobante asociado no posee cuit del receptor | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 104 |
| 223 | El comprobante asociado posee otro cuit de receptor | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 104 |
| 224 | Si el punto de venta del comprobante asociado NO es del tipo electrónico debe informar la fecha de emisión | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 104 |
| 225 | Si el punto de venta del comprobante asociado NO es del tipo electrónico la fecha de emisión no puede ser posterior a la fecha de la autorización | Rechaza | arrayErrores | `comprobanteAsociado` › fechaEmision | 104 |
| 342 | Si `<numeroDocumento>` es la CUIT para Sujeto No Categorizado (23000000000) y el `<codigoTipoDocumento>` es distinto de B (6, 7 u 8) o si la `<condicionIVAReceptor>` no se corresponde con Sujeto No Categorizado | Rechaza | arrayErrores | `comprobanteAsociado` › numeroDocumento / codigoTipoDocumento / condicionIVAReceptor | 104 |
| 353 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT), se debe validar que el número ingresado esté registrado en el padrón del Organismo y se encuentre en estado activo para dicho tipo de documento. Esta validación aplica para comprobantes: - Comprobantes Clase A - Comprobantes Clase B. Donde `<numeroDocumento>` es distinto de la CUIT de Sujeto No Categorizado y no sea una CUIT Pais, y el `<importeTotal>` del comprobante (pesificado al tipo de cambio informado en caso de moneda extranjera) sea igual o mayor a $10.000.000. | Rechaza | arrayErrores | `comprobanteAsociado` › codigoTipoDocumento / numeroDocumento / codigoTipoComprobante | 104 |
| 2200 | La fechaHasta debe ser posterior o igual fechaDesde | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 105 |
| 2201 | La fechaHasta del periodoComprobantesAsociados debe ser anterior o igual a la fecha de emisión del comprobante por el cual se está solicitando la autorización | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaHasta / fechaEmision | 105 |
| 2202 | Si el comprobante a autorizar incluye percepciones, el rango de fecha informado debe corresponder al mismo Mes/Año | Observa | arrayObservaciones | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 105 |
| 400 | Valores permitidos: 4, 5, 6 | Rechaza | arrayErrores | `subtotalIVA` › codigo | 106 |
| 402 | No se deberá repetir (no pueden incluírse dos subtotales IVA con el mismo código) | Rechaza | arrayErrores | `subtotalIVA` › codigo | 106 |
| 403 | Si existen uno o más ítems con una determinada alícuota IVA, deberá existir el correspondiente subtotal IVA para dicha alícuota. No se sebe incluír un subtotal IVA si dicha alícuota no está presente en al menos un ítem. | Rechaza | arrayErrores | `subtotalIVA` › codigo | 106 |
| 404 | Deberá coincidir con la sumatoria de todos los `<importeItem>` de `<item>` donde la alícuota de IVA coincida con la indicada, es decir, donde `<codigoCondicionIVA>` de `<item>` = `<codigo>` de `<subtotalIVA>`. | Rechaza | arrayErrores | `subtotalIVA` › importe | 106 |
| 522 | Deberá informarse 1 (uno). | Rechaza | arrayErrores | `item` › unidadesMtx | 107 |
| 523 | Deberá informarse el código 7790001001139 | Rechaza | arrayErrores | `item` › codigoMtx | 107 |
| 505 | Opcional. Longitud máxima 50 posiciones. | Rechaza | arrayErrores | `item` › codigo | 107 |
| 506 | Cantidad máxima de caracteres permitidos es 4000. Importante: no es necesario (ni recomendable) completar con espacios. | Rechaza | arrayErrores | `item` › descripcion | 107 |
| 524 | No debe informarse | Rechaza | arrayErrores | `item` › cantidad | 107 |
| 525 | Deberá informarse el código 7 - unidades | Rechaza | arrayErrores | `item` › codigoUnidadMedida | 107 |
| 526 | No debe informarse | Rechaza | arrayErrores | `item` › precioUnitario | 107 |
| 527 | No debe informarse | Rechaza | arrayErrores | `item` › importeBonificacion | 107 |
| 528 | Deberá coincidir con alguno de los siguientes valores permitidos: 4, 5 o 6 | Rechaza | arrayErrores | `item` › codigoCondicionIVA | 107 |
| 514 | Obligatorio si `<codigoTipoComprobante>` es igual a 2, 3, 52 ó 53. No corresponde para `<codigoTipoComprobante>` igual a 7 u 8. | Rechaza | arrayErrores | `item` › importeIVA | 107 |
| 529 | Para `<codigoTipoComprobante>` igual a 2, 3, 52 ó 53 deberá ser igual a `<importeItem>` | Rechaza | arrayErrores | `item` › importeIVA | 107 |
| 530 | Si `<codigoTipoComprobante>` es igual a 2, 3, 52 ó 53 deberá ser mayor a 0 (cero) | Rechaza | arrayErrores | `item` › importeIVA | 107 |
| 531 | Deberá ser mayor a 0 (cero) | Rechaza | arrayErrores | `item` › importeItem | 107 |
| 320 | Valores permitidos: consultar método consultarTiposDatosAdicionales | Rechaza | arrayErrores | `datoAdicional` › t | 108 |
| 321 | Si t es igual a 2 (“Dato Adicional para Empresas Promovidas”), en c1 se deberá indicar el id de proyecto (el mismo deberá corresponder a la cuit emisora del comprobante) o cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen de Promoción Industrial. Los campos c2 a c6 no deberán informarse (reservados para uso futuro) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 108 |
| 323 | Si t es igual a: 11(“Dato Adicional para Operaciones Económicas Relacionadas con Bienes Inmuebles”) 12(“Dato Adicional para Locacion temporaria de Inmuebles con fines Turisticos”) 13(“Dato Adicional para Representantes de Modelos”) 14 (“Dato Adicional para Agencias de Publicidad”) 15 (“Dato Adicional para Personas Físicas que desarrollen actividad de Modelaje”) En c1 se deberá indicar cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen o 1 (uno) en caso de que la actividad facturada esté alcanzada por el Régimen. Los campos c2 a c6 no deberán informarse (reservados para uso futuro) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 108 |
| 324 | Si t es igual a 10 (“Dato Adicional para Educación Pública de Gestión Privada”) En c1 se deberá indicar cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen o 1 (uno) en caso de que la actividad facturada esté alcanzada por el Régimen. Si se se informa c1 igual a 1(uno) debe informar: c2 = Tipo de Documento (corresponde a 10.11 según R.G.). c3 = Numero de Documento (corresponde 10.12 según R.G.). Los campos c4 a c6 no deberán informarse (reservados para uso futuro) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 109 |
| 325 | Si t es igual a 10 (“Dato Adicional para Educación Pública de Gestión Privada”) y c1 igual a 1(uno). En c2 debe informar alguno de los valores permitidos: consultar método consultarTiposDocumento. Si se indica c2 con 80, 86 ú 87 (CUIT, CUIL y CDI respectivamente) el número informado en c3 deberá obrar en las bases del organismo. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 109 |
| 322 | No se puede incluír más de un dato adicional (sólo se permite un id por comprobante) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 109 |
| 420 | Si se informar el grupo de compradores debe tener mas de un comprador | Rechaza | arrayErrores | `comprador` › arrayCompradores | 110 |
| 421 | Si se infroma el grupo de compradores, el tipo y número de documento del Receptor es obligatorio. Cuando se informan compradores múltiples, el que se indique con mayor porcentaje deberá figurar como receptor del comprobante. En caso de no haber un único comprador con porcentaje mayor, debe informar uno de ellos. | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 110 |
| 422 | El tipo de documento de los compradores debe ser CUIT, CUIL o CDI | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento | 110 |
| 423 | Número de documento informado repetido. Sólo Se debe informar una vez al comprador | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 110 |
| 424 | El Porcentaje de Titularidad del Comprador debe ser mayor a 0 (cero) | Rechaza | arrayErrores | `comprador` › porcentaje | 110 |
| 425 | El Porcentaje de Titularidad del Comprador debe ser menor a 100 (cien) | Rechaza | arrayErrores | `comprador` › porcentaje | 110 |
| 426 | El Emisor del comprobante no puede ser comprador | Rechaza | arrayErrores | `comprador` › porcentaje | 110 |
| 427 | La suma de los porcentajes indicados en la lista de compradores debe ser igual a 100 | Rechaza | arrayErrores | `comprador` › porcentaje | 110 |
| 428 | El receptor del comprobante debe incluírse con el mismo tipo y número de documento en el grupo de compradores | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 110 |
| 429 | El receptor del comprobante (tipo y número de documento) debe coincidir con el comprador que tenga el mayor porcentaje en la lista de compradores. En caso de no haber un único comprador con porcentaje mayor, deberá coincidir con uno de ellos | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento / porcentaje | 110 |
| 430 | Las CUIT/CUIL/CDI de los compradores deberán encontrarse activas en el Sistema Registral | Rechaza | arrayErrores | `comprador` › codigoTipoDocumento / numeroDocumento | 111 |
| 431 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 las CUITs de los compradores deben encontrarse activa en IVA o en monotributo. | Rechaza | arrayErrores | `comprador` › codigoTipoComprobante / numeroDocumento | 111 |
| 432 | Sólo se puede informar el arrayCompradores para codigoConcepto igual a 1 (Productos) | Rechaza | arrayErrores | `comprador` › arrayCompradores / codigoConcepto | 111 |


### solicitarCAEA (13)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10005 | La cuit emisora ha sido incluída en la consulta de facturas apócrifas | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 119 |
| 10020 | Deberá encontrarse empadronado y activo en el Régimen para solicitar CAEA. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026 | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 119 |
| 10021 | Deberá encontrarse empadronado y activo en Codificación de Productos – opción Facturas con Detalle | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 119 |
| 10022 | Deberá estar registrado como Autoimpresor. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026 | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 119 |
| 10024 | Deberá poseer al menos un punto de venta activo correspondiente al régimen CAEA - Codificación de Productos - opción Facturas con Detalle | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 119 |
| 10025 | Deberá estar adherida al Domicilio Fiscal Electrónico | Observa | CAEAResponse/arrayObservaciones | `authRequest` › cuitRepresentada | 119 |
| 10026 | El contribuyente registra incumplimientos en la rendición del régimen CAEA. La CUIT adeuda la presentación de 2 quincenas consecutivas o 4 alternadas. Retornará el listado de las rendiciones pendientes con el siguiente formato "periodo ; orden ; punto de venta". | Observa | CAEAResponse/arrayObservaciones | `authRequest` › cuitRepresentada | 119 |
| 10027 | Se recuerda que según la RG 5782/2025 el régimen de CAEA se aplica exclusivamente a situaciones de contingencia, motivo por el cual solo se permitirá su uso en domicilios que cuenten con al menos un punto de venta activo bajo la modalidad CAE o Controlador Fiscal como modalidad principal | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 120 |
| 10028 | Existen Puntos de Venta CAEA que no comparten domicilio con algún Punto de Venta CAE o Controlador Fiscal que actúe como modalidad principal. Retornará el listado de los Puntos de Venta afectados. | Observa | CAEAResponse/arrayObservaciones | `authRequest` › cuitRepresentada | 120 |
| 600 | Debe tener el formato AAAAMM, donde AAAA indica el año y MM el mes en números. | Rechaza | arrayErrores | `solicitudCAEA` › periodo | 120 |
| 601 | Debe ser igual a 1 ó 2. | Rechaza | arrayErrores | `solicitudCAEA` › orden | 120 |
| 602 | Fecha de envío podrá ser hasta 5 (cinco) días corridos anteriores del inicio cada quincena y hasta el final de la misma. | Rechaza | arrayErrores | `solicitudCAEA` › fecha en que se envía la solicitud | 120 |
| 604 | No debe existir un CAEA otorgado para la CUIT solicitante con igual periodo y orden. | Rechaza | arrayErrores | `solicitudCAEA` › periodo / orden | 120 |


### informarComprobanteCAEA (189)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10006 | La cuit emisora ha sido inlcuída en la consulta de facturas apócrifas | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 138 |
| 10030 | Debe estar empadronada en el régimen de CAEA con estado activo o baja. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 138 |
| 162 | Si `<codigoTipoComprobante>` es igual a 1, 2, 51, 201 ó 206 correspondientes a Facturas no corresponde informar un periodo de comprobantes asociados | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / periodoComprobantesAsociados | 138 |
| 163 | La cuit receptora se encuentra inactiva por haber sido inlcuída en la consulta de facturas apócrifas | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 138 |
| 165 | Si ocurrió un error imprevisto al momento de validar las actividades a quedar asociadas al comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigo / arrayActividades | 138 |
| 390 | Si no se informa la condición de IVA del Receptor (obligatoria) o bien se informa un valor no contemplado por el servicio. Ver método consultarCondicionesIVARecep tor | Observa | arrayObservaciones | `comprobanteCAEARequest` › condicionIVAReceptor / fechaEmision | 139 |
| 391 | A partir del 5 de abril de 2025 podrá enviarse de forma opcional el campo Condición Frente al IVA del receptor, hasta tanto entre en vigencia su obligatoriedad reglamentada por la Resolución General N° 5616, en cuyo momento pasará a rechazar la emisión de comprobantes sin este dato. Si se informa una combinación invalida de Condición de IVA del Receptor y Tipo de Comprobante. Ver método consultarCondicionesIVARecep tor | Observa | arrayObservaciones | `comprobanteCAEARequest` › condicionIVAReceptor / codigoTipoComprobante / fechaEmision | 139 |
| 700 | Podrá ser: 1 – Factura A 2 – Nota de Débito A 3 – Nota de Crédito A 6 – Factura B 7 – Nota de Débito B 8 – Nota de Crédito B 51 – Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN 52 – Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 – Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 201 - Factura de Crédito Electrónica MiPyMEs (FCE) A 202 - Nota de Débito Electrónica MiPyMEs (FCE) A 203 - Nota de Crédito Electrónica MiPyMEs (FCE) A 206- Factura de Crédito Electrónica MiPyMEs (FCE) B 207 - Nota de Débito Electrónica MiPyMEs (FCE) B 208 - Nota de Crédito Electrónica MiPyMEs (FCE) B | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante | 140 |
| 700 | El contribuyente no se encuentra habilitado a emitir (según el tipo de comprobante indicado) comprobantes A, A con Leyenda o A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / cuitRepresentada | 140 |
| 701 | Debe ser del tipo habilitado para CAEA - Codificación de Productos - opción Factura con Detalle y no debe estar bloqueado a la fecha en que se emitió el comprobante. Consultar método consultarPuntosVenta o consultarPuntosVentaCAEA | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta | 141 |
| 702 | Debe estar comprendida dentro de la fecha desde y fecha hasta de vigencia del CAEA | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 141 |
| 703 | El número de comprobante informado debe ser mayor en 1 al último informado para igual punto de venta y tipo de comprobante. De no existir comprobante informado para igual punto de venta y codigoTipoComprobante, el número de comprobante debe ser igual a 1 (uno) | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 141 |
| 704 | La fecha de emisión del comprobante debe ser mayor o igual a la fecha del último comprobante informado para igual tipo de comprobante y punto de venta. | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision / numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 141 |
| 705 | Debe informarse y corresponder a la CUIT | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoAutorizacion | 141 |
| 706 | Debe ser mayor a la fecha de entrada en vigencia del CAEA `<fechaDesde>` | Rechaza | arrayErrores | `comprobanteCAEARequest` › fecha en que se envía la solicitud | 141 |
| 707 | Si se informa uno de los campos debe informarse el otro. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 141 |
| 709 | La fecha de alta del numeroPuntoVenta debe ser menor o igual a la fechaHasta de la vigencia del CAEA que posee el comprobante que se está informando. | Rechaza | arrayErrores | `comprobanteCAEARequest` › CAEA / numeroPuntoVenta | 141 |
| 713 | Deberá ser igual a alguno de los siguientes valores: 1 – Productos 2 – Servicios 3 – Productos y Servicios | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoConcepto | 142 |
| 715 | Opcional. Debe informarse si algún ítem tiene `<codigoCondicionIVA>` igual a 4, 5 ó 6. | Rechaza | arrayErrores | `comprobanteCAEARequest` › arraySubtotalesIVA | 142 |
| 718 | Opcionales. Deberá informarse en los siguientes casos: - cuando `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52, 53, 201, 202, 203, 206, 207 o 208. -cuando `<codigoTipoComprobante>` es igual a 6, 7 u 8 y el importe total del comprobante `<importeTotal>` es mayor ó igual al monto en pesos resultante según RG4444. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 142 |
| 731 | Opcional. Si se informa debe informarse “A” (sin comillas) | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoAutorizacion | 142 |
| 732 | Opcional. Si se informa debe coincidir con la Fecha Hasta del CAEA informado | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaVencimiento | 142 |
| 733 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52, 53, 201, 202, 203, 206, 207 o 208 `<codigoTipoDocumento>` deberá ser igual a 80 (CUIT) | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento | 142 |
| 736 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarTiposDocumento | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento | 142 |
| 739 | Los informes de comprobantes para un mismo punto de venta y tipo de comprobante deben ser enviados en forma sincrónica: si el WS recibe una nueva solicitud para un punto de venta y tipo de comprobante dado mientras la anterior está siendo procesada, la nueva solicitud será rechazada | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta / codigoTipoComprobante | 143 |
| 753 | Grupo de compradores no habilitado para el método | Rechaza | arrayErrores | `comprobanteCAEARequest` › arrayCompradores | 143 |
| 754 | La fecha/hora de generación es obligatoria para comprobantes CAEA por contingencia (no se informó el campo fecha/hora generación y el punto de venta es del tipo CAEA por Contni gencia). A partir del 01/08/2026 sera obligatoria para comprobantes CAEA sin distni ción del tipo de punto de venta (por Contingencia o no) | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta / fechaHoraGen | 143 |
| 757 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208. Por las condiciones de la CUIT Emisora, no corresponde realizar FCE | Rechaza | arrayErrores | `comprobanteCAEARequest` › cuitRepresentada | 143 |
| 758 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208, `<codigoTipoDocumento>` debe ser igual a 80 y `<numeroDocumento>` debe ser válido y activo. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 143 |
| 759 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208. La CUIT Receptora no registra alta en el Domicilio Fiscal Electrónico | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 143 |
| 760 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208. La CUIT Receptora no está incluida en el listado de empresas grandes según cronograma vigente ni optó por ser receptora de Factura de Crédito MiPyMe | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 144 |
| 761 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208, el Receptor no puede ser igual al Emisor | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroDocumento | 144 |
| 762 | Si `<codigoTipoComprobante>` es igual a 201, 202 o 203 la CUIT del receptor debe encontrarse activa en IVA o en monotributo. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 144 |
| 763 | Si `<codigoTipoComprobante>` es igual a 206, 207 o 208 la CUIT del receptor debe encontrarse activa como Responsable Inscripto en IVA, IVA Exento o Monotributista. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 144 |
| 764 | Si `<codigoTipoComprobante>` es igual a 201 ó 206. La Fecha de Vencimiento de Pago es obligatorio para Facturas de Crédito MiPyME | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaVencimientoPago | 144 |
| 765 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208. La Fecha de Vencimiento de Pago no debe informarse para Notas de Crédito o Débito de las Facturas de Crédito MiPYME | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaVencimientoPago | 144 |
| 766 | La fecha de vencimiento de pago debe ser posterior o igual a la fecha de emisión. | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaVencimientoPago / fechaEmision | 144 |
| 769 | El importe no puede ser negativo ni nulo | Rechaza | arrayErrores | `comprobanteCAEARequest` › importeTotal | 144 |
| 770 | Si `<codigoTipoComprobante>` es igual a 203 ó 208. El importe total del comprobante a autorizar no puede ser mayor o igual al saldo de la operación actual de la cuenta corriente | Rechaza | arrayErrores | `comprobanteCAEARequest` › importeTotal | 145 |
| 771 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208, la moneda debe: - coincidir con la Factura vinculada, ó - ser Pesos Argentinos si la Factura vinculada ya fue aceptada, cancelada o rechazada y se desea realizar un ajuste por diferencia de cambio | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoMoneda | 145 |
| 774 | Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208, la Fecha de Emisión debe ser anterior a la fecha en que se envía la solicitud | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 145 |
| 776 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208. Para realizar una Nota de Débito o Crédito con moneda distinta a la Factura la `<fechaEmision>` de la misma debe ser posterior a la aceptación de la Factura o Cuenta Corriente Asociada | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision / codigoMoneda | 145 |
| 777 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208 perteneciente a Factura de Crédito Electrónica no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / periodoComprobantesAsociados | 145 |
| 778 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. Falta informar comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito válido/s o informar un período de comprobantes asociados válido | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAsociados | 146 |
| 779 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. No debe informar un período de comprobantes asociados cuando informa comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAsociados | 146 |
| 780 | Si `<codigoTipoComprobante>` es igual a 1, 2, 51, 201 ó 206 correspondientes a Facturas no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / periodoComprobantesAsociados | 146 |
| 884 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT), se debe validar que el número ingresado esté registrado en el padrón del Organismo y se encuentre en estado activo para dicho tipo de documento. Esta validación aplica para comprobantes: - Comprobantes Clase A - Comprobantes Clase B. Donde `<numeroDocumento>` es distinto de la CUIT de Sujeto No Categorizado y no sea una CUIT Pais, y el `<importeTotal>` del comprobante (pesificado al tipo de cambio informado en caso de moneda extranjera) sea igual o mayor a $10.000.000. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento / codigoTipoComprobante | 147 |
| 140 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue inactivado o invalidado. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 148 |
| 142 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 148 |
| 144 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido marcada como Apocrifa. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 148 |
| 146 | Si el `<numeroDocumento>` del receptor/comprador se encuentra marcada como fallecido y no está marcado como sucesión indivisa. | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 148 |
| 366 | Si `<codigo>` se encuentra mas de una vez en el array de actividades (no admite repetidos). Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigo / arrayActividades | 148 |
| 367 | Si `<codigo>` no se encuentra entre las actividades vigentes para la cuit representada. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigo / arrayActividades | 148 |
| 368 | Si `<codigo>` se encuentra asociado a un conjunto de actividades de un “rubro” y se encontraron otros `<codigo>` dentro del array que se encuentran asociados a otro conjunto de un “rubro” distinto. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigo / arrayActividades | 148 |
| 370 | Si ocurrio un error imprevisto al validar los comprobantes asociados que sean de tipo remito (88, 990, 91, 995, 997, 993, 994). Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 149 |
| 371 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y no fue encontrado en los registros de ARCA, o bien fue encontrado, pero la información asociada al mismo no es la esperada. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 149 |
| 372 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero el mismo se encuentra en un estado inválido. Dichos estados varian según el tipo de remito del que se trate. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 149 |
| 373 | Si el comprobante asociado es del tipo remito (91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero la cuit del receptor de dicho remito no coincide con la cuit del receptor del comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento / arrayComprobantesAsociados | 149 |
| 375 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y el tipo de comprobante asociado es remito, pero el mismo no es carnico (88, 990, 91, 997, 993, 994), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 150 |
| 376 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” o “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado o Tabaco en Hebras (91, 997, 993, 994, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 150 |
| 377 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado (990, 91, 997, 993, 994, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 150 |
| 378 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco en Hebras (88, 91, 997, 993, 994, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 150 |
| 380 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y el tipo de comprobante asociado es remito, pero el mismo no es Harina (88, 91, 997, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 151 |
| 381 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y no se especifico ningún Remito del tipo Harina (993 y 994), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 151 |
| 382 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y no se especifico ningún Remito del tipo Carnico (995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 151 |
| 383 | Los códigos de concepto permitidos para asociar Remitos Cárnicos (995) al Comprobante son 1 – Productos y 3 – Productos y Servicios | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoConcepto / arrayComprobantesAsociados | 151 |
| 384 | Si no se especifican actividades, y el Remito a Asociar es un Remito Sectorial (88, 990, 993, 994, 995, 997), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados arrayActividades | 151 |
| 385 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero se encuentra marcado como de exportación, mientras que el presente servicio solo acepta Remitos para el Mercado. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 152 |
| 386 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y ya fue declarado una vez en el array de comprobantes asociados. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / arrayComprobantesAsociados | 152 |
| 708 | Si `<codigoTipoDocumento>` es igual a 80, 86 o 87, `<numeroDocumento>` debe ser válido y activo, excepto para `<codigoTipoComprobante>` 6, 7 u 8, `<codigoTipoDocumento>` 80 y `<numeroDocumento>` igual a 23000000000. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 152 |
| 717 | No debe estar informado como CAEA No utilizado | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoAutorizacion | 152 |
| 719 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53: - Deberá ser igual a la sumatoria de importeItem menos importeIVA para los ítems con `<codigoCondicionIVA>` igual a 3, 4, 5, 6. Si `<codigoTipoComprobante>` es igual a 6, 7 u 8: - Deberá ser igual a la sumatoria de `<importeItem>` menos el IVA correspondiente (calculado en base al importe y la alícuota de cada ítem), para la totalidad de los ítems con `<codigoCondicionIVA>` igual a 3, 4, 5 ó 6. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems gravados * | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeGravado | 153 |
| 720 | Deberá coincidir con la sumatoria de `<importeItem>` para los ítems con `<codigoCondicionIVA>` igual a 1. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems no gravados * | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeNoGravado | 153 |
| 721 | Deberá coincidir con la sumatoria de `<importeItem>` para los ítems con `<codigoCondicionIVA>` igual a 2. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems exentos * | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeExento | 154 |
| 722 | Deberá coincidir con la sumatoria de los campos `<importeNoGravado>`, `<importeGravado>`, `<importeExento>`. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeSubtotal | 154 |
| 723 | Debe ser igual a la sumatoria de la totalidad de los campos `<importe>``<otroTributo>` (dentro de `<arrayOtrosTributos>`). Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de tributos * | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeOtrosTributos | 154 |
| 724 | Debe ser igual a `<importeSubtotal>`+ `<importeOtrosTributos>` + sumatoria de `<subtotalIVA>``<importe>` (dentro del arraySubtotalesIVA). Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeTotal | 154 |
| 725 | Debe ser igual a `<importeOtrosTributos>` + la sumatoria de la totalidad de los campos `<importeItem>`. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems * | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeTotal | 155 |
| 710 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarMonedas | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoMoneda | 155 |
| 122 | En caso de enviar la marca de que el pago del comprobante se realiza en la misma moneda extranjera para comprobantes que no sean facturas. Unicamente se puede utilizar con los códigos habilitados (1,6,51,201,206) | Observa | arrayObservaciones | `comprobanteCAEARequest` › cancelaEnMismaMonedaExtra njera | 155 |
| 182 | No podrá ser inferior al 2% ni superior en un 400% del que suministra ARCA como orientativo de acuerdo a la cotización oficial | Observa | arrayObservaciones | `comprobanteCAEARequest` › cotizacionMoneda | 155 |
| 726 | Debe ser igual a 1 (uno) si `<codigoMoneda>` es igual a PES | Observa | arrayObservaciones | `comprobanteCAEARequest` › cotizacionMoneda | 155 |
| 174 | En caso de enviar un valor inválido para la marca de que el pago de la factura se realiza en la misma moneda extranjera. Los valores válidos son S, N o vacío | Observa | arrayObservaciones | `comprobanteCAEARequest` › cancelaEnMismaMonedaExtra njera | 155 |
| 175 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera y enviar como código de moneda el Peso Argentino | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoMoneda / cancelaEnMismaMonedaExtra njera | 155 |
| 181 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera, que codigoMoneda es del grupo de monedas con cotización del Banco de la Nación Argentina (ver Anexo Monedas BNA), que haya cotización y que la misma no coincida exactamente con el valor enviado en el campo cotizacionMoneda. En cuyo caso se podrá omitir el mismo para que la cotización de la factura sea la obtenida de los registros de ARCA | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoMoneda / cotizacionMoneda / cancelaEnMismaMonedaExtra njera | 156 |
| 194 | El campo es obligatorio a excepción de los casos para los cuales se envia el campo cancelaEnMismaMonedaExtranjera y se puede obtener la cotizacion asociada al codigoMoneda si esta es del grupo de monedas del Banco de la Nación Argentina (ver Anexo Monedas BNA) | Rechaza | arrayErrores | `comprobanteCAEARequest` › cotizacionMoneda | 156 |
| 195 | No es posible indicar una cotización negativa | Rechaza | arrayErrores | `comprobanteCAEARequest` › cotizacionMoneda | 156 |
| 727 | Debe informarse solo si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaServicioDesde | 156 |
| 728 | Debe informarse solo si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaServicioHasta | 156 |
| 729 | Debe informarse solo si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaVencimientoPago | 156 |
| 730 | La fecha de vencimiento de pago debe ser mayor o igual a la fecha de emisión. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaVencimientoPago / fechaEmision | 156 |
| 734 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53 la CUIT del receptor debe encontrarse activa en IVA o en monotributo. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 156 |
| 735 | El Receptor no puede ser igual al Emisor | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 156 |
| 737 | La Fecha de Servicio desde debe ser menor o igual a la Fecha de Servicio Hasta | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaServicioDesde / fechaServicioHasta | 157 |
| 738 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53 y `<codigoTipoDocumento>` es igual a 80 (CUIT), dicha CUIT deberá encontrarse activa en el Sistema Registral | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 157 |
| 749 | Falta incluir en la sección OtrosTributos la Percepción de IVA No Categorizado (ID 13), según lo dispuesto por la RG 2126/2006. Se debe informar el tributo `<codigo>` 13 (Percepción de IVA No Categorizado) con un `<importe>` mayor a 0 (cero) dentro del nodo `<arrayOtrosTributos>` cuando se cumplan simultáneamente las siguientes condiciones: - `<codigoTipoComprobante>` corresponda a un comprobante clase B (6, 7 u 8). - `<codigoTipoDocumento>` sea igual a 80 (CUIT). - `<numeroDocumento>` corresponda a 23000000000 (No Categorizado). - La suma de `<importeGravado>` más la sumatoria de los campos `<importe>` dentro de `<arraySubtotalesIVA>` sea mayor a 0 (cero). | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento / importeGravado / arraySubtotalesIVA / arrayOtrosTributos / importeOtrosTributos | 157 |
| 178 | Falta incluir en la sección OtrosTributos la Percepción de IVA No Categorizado (ID 13), según lo dispuesto por la RG 2126/2006. Se debe informar el tributo `<codigo>` 13 (Percepción de IVA No Categorizado) con un `<importe>` mayor a 0 (cero) dentro del nodo `<arrayOtrosTributos>` cuando se cumplan simultáneamente las siguientes condiciones: - `<codigoTipoComprobante>` corresponda a un comprobante clase B (6, 7 u 8). - `<codigoTipoDocumento>` sea igual a 80 (CUIT). - `<numeroDocumento>` corresponda a 23000000000 (No Categorizado). - La suma de `<importeGravado>` más la sumatoria de los campos `<importe>` dentro de `<arraySubtotalesIVA>` sea mayor a 0 (cero). | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / codigoTipoDocumento / numeroDocumento / importeGravado / arraySubtotalesIVA / arrayOtrosTributos / importeOtrosTributos | 158 |
| 750 | Debe estar dado de alta en el Impuesto al Valor Agregado al momento de la fecha de emisión del comprobante | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada / fechaEmision | 158 |
| 751 | Debe encontrarse habilitado a comprobantes clase 'A' a la fecha de emisión del comprobante | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada / codigoTipoComprobante / fechaEmision | 158 |
| 755 | La fecha/hora de generación solo debe informarse para comprobantes CAEA por contingencia (se informó el campo fecha/hora generación pero el punto de venta no es del tipo CAEA por Contingencia). Se informa que esta validación quedará fuera de vigencia a partir del 31/07/2026, siendo absorbida por las condiciones de la validación 754. | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroPuntoVenta / fechaHoraGen | 159 |
| 756 | Para comprobantes CAEA: si se indica `<codigoConcepto>` igual a 1, la fecha de emisión del comprobante puede ser hasta 5 días anteriores o posteriores respecto de la fecha de generación, pero sin extenderse al mes siguiente; si se indica `<codigoConcepto>` igual a 2 ó 3 puede ser hasta 10 días anteriores o posteriores a la fecha de generación | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroPuntoVenta / fechaHoraGen / fechaEmision / codigoConcepto | 159 |
| 767 | - Si `<codigoTipoComprobante>` es igual a 1 ó 6, y - La CUIT Receptora está incluida en el listado de empresas grandes según cronograma vigente u optó por ser receptora de Factura de Crédito MiPyme, y - Por las condiciones de la CUIT Emisora, y - El monto facturado es mayor o igual al Reglamentado Corresponde realizar Factura Electrónica de Crédito MiPyME, realice un comprobante con `<codigoTipoComprobante>` 201 o 206. | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada / codigoTipoDocumento / numeroDocumento / importeTotal | 159 |
| 768 | - Si `<codigoTipoComprobante>` es igual a 201 ó 206, y - La CUIT Receptora está incluida en el listado de empresas grandes según cronograma vigente u optó por ser receptora de Factura de Crédito MiPyme, y - Por las condiciones de la CUIT Emisora, y - El monto facturado es menor al Reglamentado NO Corresponde realizar Factura Electrónica de Crédito MiPyME, realice un comprobante con `<codigoTipoComprobante>` 1 o 6. | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada / codigoTipoDocumento / numeroDocumento / importeTotal | 160 |
| 772 | Por las condiciones de la CUIT Emisora, no corresponde realizar FCE - Está habilitado para Comprobantes A con leyenda OPERACIÓN SUJETA A RETENCIÓN - EXCLUIDO – Art. N° 4 Resolución 209/2018 RESOL-2018-209-APN- MPYT | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada | 160 |
| 773 | La Fecha y Hora de Generación no puede ser posterior a un día corrido del vencimiento del CAEA | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaHoraGen | 160 |
| 775 | Régimen informado fuera de término. Si `<codigoTipoComprobante>` es igual a 201, 202, 203, 206, 207 ó 208, La Fecha de Emisión del comprobante debe ser hasta un día anterior a la fecha en que se envía la solicitud | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaEmision | 160 |
| 781 | LA CUIT RECEPTORA SE ENCUENTRA INACTIVA POR HABER SIDO INLCUÍDA EN LA CONSULTA DE FACTURAS APÓCRIFAS - NO PODRÁ COMPUTARSE EL CRÉDITO FISCAL. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento / fechaEmision | 160 |
| 782 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 la CUIT del receptor es activa en monotributo | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 161 |
| 783 | Si `<cuitRepresentada>` tiene pendiente de presentación el formulario de habilitación de comprobantes o su fecha de presentación es anterior a tu alta en IVA | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada | 161 |
| 785 | Si `<numeroDocumento>` es inexistente en el padron del Organismo | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 161 |
| 791 | Siendo `<codigoTipoComprobante>` una Nota de Crédito (3, 8, 53, 203 y 208), si la sumatoria de los importes totales de los elementos del array `<arrayComprobantesAsociados>` (sin incluir Remitos) supera el `<importeTotal>` de la Nota de Crédito | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados / importeTotal | 161 |
| 803 | El comprobante asociado podrá ser: 1 – Factura A 2 – Nota de Débito A 3 – Nota de Crédito A 6 – Factura B 7 – Nota de Débito B 8 – Nota de Crédito B 51 – Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN 52 – Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 – Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 201 - Factura de Crédito Electrónica MiPyMEs (FCE) A 202 - Nota de Débito Electrónica MiPyMEs (FCE) A 203 - Nota de Crédito Electrónica MiPyMEs (FCE) A 206- Factura de Crédito Electrónica MiPyMEs (FCE) B 207 - Nota de Débito Electrónica MiPyMEs (FCE) B 208 - Nota de Crédito Electrónica MiPyMEs (FCE) B 91 – Remito Papel 88 – Remito Electrónico de Tabaco Acondicionado 990 – Remito Electrónico de Tabaco en Hebras 993 – Remito Electrónico de Harina en Camion 994 – Remito Electrónico de Harina en Tren 995 – Remito Electrónico de Carne 997 – Remito Electrónico Azucar Mercado Interno Consultar método consultarTiposComprobante | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante | 163 |
| 804 | El campo cuit es opcional y solo puede completarse si el tipo de comprobante es 88 o 990 (solo es necesario si el remito fue emitido por un tercero) | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / cuit | 164 |
| 808 | Deberá ser igual a 88, 91, 990 o 995 si el tipo de comprobante cuya autorización se solicita es igual a 201 o 206 | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante | 164 |
| 809 | Al autorizar una nota de débito o crédito de Factura Electrónica de Crédito MiPyME (202, 203, 207, 208), debe enviar el campo cuit para el tipo de comprobante asociado indicado | Rechaza | arrayErrores | `comprobanteCAEARequest` › cuit | 164 |
| 810 | Al autorizar una nota de débito o crédito de Factura Electrónica de Crédito MiPyME (202, 203, 207, 208), el campo cuit para el tipo de comprobante asociado indicado debe coincidir con la cuit emisora del comprobante a autorizar | Rechaza | arrayErrores | `comprobanteCAEARequest` › cuit | 164 |
| 811 | Al autorizar una nota de débito o crédito de Factura Electrónica de Crédito MiPyME (202, 203, 207, 208), el comprobante asociado `<codigoTipoComprobante>` `<numeroPuntoVenta>` `<numeroComprobante>` deberá obrar en las bases del organismo. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante | 164 |
| 812 | Al autorizar una nota de débito o crédito de Factura Electrónica de Crédito MiPyME (202, 203, 207, 208), debe haber un y sólo un comprobante asociado de Factura Electrónica de Crédito MiPyME: - 201 o 206, para NO anulación - 201, 202, 203, 206, 207 o 208, para Anulación | Rechaza | arrayErrores | `comprobanteCAEARequest` › arrayComprobantesAso ciados | 165 |
| 814 | Si está presente el dato adicional código 22 en S (es una nota de anulación): - Si el tipo de comprobante a autorizar es una nota de crédito (203 o 208) el tipo de comprobante asociado a revertir debe ser 201, 202, 206 ó 207 Si el tipo de comprobante a autorizar es una nota de débito (202 o 207) el tipo de comprobante asociado a revertir debe ser 203 ó 208 | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante | 165 |
| 815 | Si está presente el dato adicional código 22 en N (NO es una nota de anulación), debe existir un comprobante asociado del tipo 201 o 206. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante | 165 |
| 816 | Si el comprobante a autorizar es de Anulación, el comprobante asociado debe haber sido rechazado por el comprador mediante el Sistema de Regitro de Facturas Electrónicas de Crédito MiPyME. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 165 |
| 817 | Si el comprobante a autorizar NO es de Anulación, el comprobante asociado NO debe haber sido rechazado por el comprador mediante el Sistema de Regitro de Facturas Electrónicas de Crédito MiPyME. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 166 |
| 818 | Al autorizar un comprobante de Factura Electrónica de Crédito MiPyME (201, 202, 203, 206, 207, 208), debe enviar el campo fechaEmision para el comprobante asociado del tipo Remito | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 166 |
| 819 | La fecha de emisión del comprobante asociado no puede ser posterior a la fecha del comprobante a autorizar | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 166 |
| 820 | La fecha de emisión del comprobante asociado informada no coincide con la existente en nuestros registros | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 166 |
| 821 | La fecha de emisión de este comprobante no puede ser anterior a la factura asociada | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 166 |
| 822 | El comprobante asociado no posee cuit del receptor | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 166 |
| 823 | El comprobante asociado posee otro cuit de receptor | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 166 |
| 824 | Si el punto de venta del comprobante asociado NO es del tipo electrónico debe informar la fecha de emisión | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 166 |
| 825 | Si el punto de venta del comprobante asociado NO es del tipo electrónico la fecha de emisión no puede ser posterior a la fecha de la autorización | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 166 |
| 889 | Si `<numeroDocumento>` es la CUIT para Sujeto No Categorizado (23000000000) y el `<codigoTipoDocumento>` es distinto de B (6, 7 u 8) o si la `<condicionIVAReceptor>` no se corresponde con Sujeto No Categorizado | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroDocumento / codigoTipoDocumento / condicionIVAReceptor | 167 |
| 800 | Deberá ser igual a 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 1, 6 o 51 Deberá ser igual a 1, 2, 3, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 2 o 3. Deberá ser igual a 6, 7, 8, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 7 u 8. Deberá ser igual a 51, 52, 53, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 52 o 53. Deberá ser igual a 201, 202, 203, 88, 91, 990 o 995 si el tipo de comprobante cuya autorización se solicita es igual a 202 o 203. Deberá ser igual a 206, 207, 208, 88, 91, 990 o 995 si el tipo de comprobante cuya autorización se solicita es igual a 207 u 208. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante | 167 |
| 801 | Si el punto de venta es del tipo electrónico el comprobante asociado `<codigoTipoComprobante>` `<numeroPuntoVenta>` `<numeroComprobante>` deberá obrar en las bases del organismo. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante | 167 |
| 802 | El tipo de punto de venta, en caso de ser electrónico, deberá ser alguno de los siguientes: RECE para aplicativo y web services, Factura en Línea - Responsable Inscripto, Factura en Línea - Método Alternatvi o al RECE (límite de 100), Codificación de Productos - Web services, Codificación de Productos - Factura en Línea, CAEA - Fact. Elect. (RECE) - RI IVA o CAEA - Codificación de Productos. | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroPuntoVenta | 168 |
| 805 | El remito asociado deberá obrar en las bases del organismo. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 168 |
| 806 | Si remito asociado corresponde a tabaco de terceros, deberá estar en estado Confirmado | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 168 |
| 807 | El receptor del remito asociado deberá conicidir con el receptor del comprobante | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 168 |
| 813 | Para CUITS Emisoras y Receptoras candidatas al Régimen de Factura Electrónica de Crédito, al autorizar una nota de débito o crédito de Factura Electrónica (2, 3, 7, 8, 52, 53), debe haber al menos un comprobante asociado de Factura Electrónica (1, 2, 3, 6, 7, 8, 51, 52 o 53) | Observa | arrayObservaciones | `comprobanteCAEARequest` › arrayComprobantesAso ciados | 168 |
| 2800 | La fechaHasta debe ser posterior o igual fechaDesde | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 169 |
| 2801 | La fechaHasta del periodoComprobantesAsociados debe ser anterior o igual a la fecha de emisión del comprobante por el cual se está solicitando la autorización | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaHasta / fechaEmision | 169 |
| 2802 | Si el comprobante a autorizar incluye percepciones, el rango de fecha informado debe corresponder al mismo Mes/Año | Observa | arrayObservaciones | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 169 |
| 900 | Valores permitidos: consultar método consultarTiposTributo | Rechaza | arrayErrores | `otroTributo` › codigo | 169 |
| 901 | Opcional. Debe informarse si `<codigo>` es igual a 99. | Rechaza | arrayErrores | `otroTributo` › descripcion | 169 |
| 1000 | Valores permitidos: 4, 5, 6 | Rechaza | arrayErrores | `subtotalIVA` › codigo | 170 |
| 1002 | No se deberá repetir (no pueden incluírse dos subtotales IVA con el mismo código) | Rechaza | arrayErrores | `subtotalIVA` › codigo | 170 |
| 1003 | Si existen uno o más ítems con una determinada alícuota IVA, deberá existir el correspondiente subtotal IVA para dicha alícuota. No se sebe incluír un subtotal IVA si dicha alícuota no está presente en al menos un ítem. | Rechaza | arrayErrores | `subtotalIVA` › codigo | 170 |
| 1001 | Para comprobantes clase “A”: Deberá coincidir con la sumatoria de todos los `<importeIVA>` de `<item>` donde la alícuota de IVA coincida con la indicada, es decir, donde `<codigoCondicionIVA>` de `<item>` = `<codigo>` de `<subtotalIVA>`. Para comprobantes clase “B”: Deberá coincidir con la sumatoria de todos los importes IVA calculados en base al importe y alícuota IVA de `<item>` donde la alícuota de IVA coincida con la indicada, es decir, donde `<codigoCondicionIVA>` de `<item>` = `<codigo>` de `<subtotalIVA>`. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems con igual código de alícuota de IVA * | Observa | arrayObservaciones | `subtotalIVA` › importe | 171 |
| 1005 | La suma de los subtotales de IVA no puede ser negativa. | Observa | arrayObservaciones | `subtotalIVA` › importe | 171 |
| 1104 | Si `<codigoMtx>` no se corresponde con un GTIN registrado, activo y vigente, el comprobante quedara observado. | Observa | arrayObservaciones | `item` › codigoMtx | 172 |
| 1100 | Es opcional si `<codigoUnidadMedida>` es 99 ó 97, para el resto de los casos es obligatorio. | Rechaza | arrayErrores | `item` › unidadesMtx | 172 |
| 1101 | De informarse deberá ser mayor o igual a 1 (uno) | Rechaza | arrayErrores | `item` › unidadesMtx | 172 |
| 1102 | Longitud máxima 6 posiciones. | Rechaza | arrayErrores | `item` › unidadesMtx | 172 |
| 1103 | Es opcional si `<codigoUnidadMedida>` es 99 ó 97, para el resto de los casos es obligatorio. | Rechaza | arrayErrores | `item` › codigoMtx | 172 |
| 1105 | Opcional. Longitud máxima 50 posiciones. | Rechaza | arrayErrores | `item` › codigo | 172 |
| 1106 | Cantidad máxima de caracteres permitidos 4000. Importante: no es necesario (ni recomendable) completar con espacios. | Rechaza | arrayErrores | `item` › descripcion | 172 |
| 1107 | No corresponde para `<codigoUnidadMedida>` igual a 99 o 97. En otro caso es obligatorio. | Rechaza | arrayErrores | `item` › cantidad | 172 |
| 1108 | Debe ser alguno de los valores permitidos: consultar método consultarUnidadesMedida | Rechaza | arrayErrores | `item` › codigoUnidadMedida | 172 |
| 1109 | No corresponde para `<codigoUnidadMedida>` igual a 99 o 97. En otro caso es obligatorio. | Rechaza | arrayErrores | `item` › precioUnitario | 172 |
| 1110 | No corresponde para `<codigoUnidadMedida>` igual a 99 o 97. Es opcional para el resto de los casos. | Rechaza | arrayErrores | `item` › importeBonificacion | 173 |
| 1111 | Deberá coincidir con alguno de los valores permitidos: consultar método consultarCondicionesIVA | Rechaza | arrayErrores | `item` › codigoCondicionIVA | 173 |
| 1112 | Obligatorio para `<codigoTipoComprobante>` igual a 1, 2, 3, 51, 52 o 53. No corresponde para `<codigoTipoComprobante>` igual a 6, 7 u 8. | Rechaza | arrayErrores | `item` › importeIVA | 173 |
| 1121 | Si se informa el campo `<unidadesMtx>` entonces debe informarse el campo `<codigoMtx>` y viceversa. | Rechaza | arrayErrores | `item` › unidadesMtx / codigoMtx | 173 |
| 1114 | De informarse deberá ser menor o igual a `<precioUnitario>`*`<cantidad>` | Observa | arrayObservaciones | `item` › importeBonificacion | 173 |
| 1115 | Si `<codigoUnidadMedida>` es 99 deberá existir por lo menos otro item con igual `<codigoCondicionIVA>` y `<codigoUnidadMedida>` distinta a la informada para este item. | Observa | arrayObservaciones | `item` › codigoCondicionIVA / codigoUnidadMedida | 173 |
| 1116 | Para `<codigoTipoComprobante>` igual a 1, 2, 3, 51, 52 o 53 y unidad de medida es distinto a 95, 97 o 99 deberá ser igual (`<precioUnitario>` * `<cantidad>` -`<importeBonificación>`) * alícuota de IVA correspondiente. Para `<codigoTipoComprobante>` igual a 1, 2, 3, 51, 52 o 53 y unidad de medida igual a 95 deberá ser igual a (-1) * (`<precioUnitario>` * `<cantidad>` - `<importeBonificacion>`) * alícuota de IVA correspondiente. Para `<codigoTipoComprobante>` igual a 1, 2, 3, 51, 52 o 53 y unidad de medida igual a 97 o 99, deberá ser igual a `<importeItem>` - `<importeItem>` / (1 + alícuota de IVA correspondiente). | Observa | arrayObservaciones | `item` › importeIVA | 174 |
| 1117 | Si `<codigoTipoComprobante>` es igual a 1, 2 ó 3 y `<codigoUnidadMedida>` es 99, el valor absoluto de la sumatoria de los importes ingresados para este campo no puede superar a la sumatoria de los importes `<importeIVA>` informado con la misma alícuota. | Observa | arrayObservaciones | `item` › importeIVA | 174 |
| 1118 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53 y `<codigoUnidadMedida>` es: - 99 deberá ser menor o igual a 0 (cero), - 97 podrá ser menor, mayor o igual a 0 (cero). - 95 deberá ser menor o igual a 0 (cero), - Cualquier otro caso deberá ser mayor o igual a 0 (cero) | Observa | arrayObservaciones | `item` › importeIVA | 174 |
| 1119 | Si `<codigoUnidadMedida>` es: - 99 deberá ser menor a 0 (cero), - 97 podrá ser menor, mayor o igual a 0 (cero) - 95 deberá ser menor a 0 (cero), - Cualquier otro caso deberá ser mayor o igual a 0 (cero). | Observa | arrayObservaciones | `item` › importeItem | 175 |
| 1120 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53 y `<codigoUnidadMedida>` es distinto a 95, 97 ó 99 deberá ser igual a (`<precioUnitario>` sin IVA *`<cantidad>` -`<importeBonificacion>`)*(1+alícuota). Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53 y `<codigoUnidadMedida>` es igual a 95 deberá ser igual a (-1) * (`<precioUnitario>` sin IVA * `<cantidad>` -`<importeBonificacion>`)*(1+alícuota). Si `<codigoTipoComprobante>` es igual a 6, 7 u 8 y `<codigoUnidadMedida>` es distinto a 95, 97 ó 99 deberá ser igual a (`<precioUnitario>` con IVA * `<cantidad>` - `<importeBonificacion>`). Si `<codigoTipoComprobante>` es igual a 6, 7 u 8 y `<codigoUnidadMedida>` es igual a 95 ser igual a (-1) * (`<precioUnitario>` con IVA * `<cantidad>` - `<importeBonificacion>`). En ambos casos el error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * | Observa | arrayObservaciones | `item` › importeItem | 175 |
| 1122 | Si `<codigoCondicionIVA>` es igual a 1, 2, 3, 51, 52 o 53 entonces `<importeIVA>` deberá ser igual a 0 (cero). | Observa | arrayObservaciones | `item` › importeIVA | 175 |
| 920 | Valores permitidos: consultar método consultarTiposDatosAdicionales | Rechaza | arrayErrores | `datoAdicional` › t | 176 |
| 922 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 6, 7, 8, 51, 52 o 53, sólo se puede incluír un dato adicional con t = 2 (sólo se permite un id de proyecto por comprobante) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 176 |
| 925 | Para el tipo de dato adicional 22, Anulación, debe indicar en el campo c1 S (si) si es de anulación o N (no) si no es de anulación | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 176 |
| 926 | Para el tipo de dato adicional 21, CBU y Alias del Emisor, el CBU informado en el campo c1 no corresponde al Emisor según nuestros registros | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 176 |
| 927 | Si el tipo de Comprobante a autorizar es 202, 203, 207 o 208, debe indicar el dato adicional código 22, Anulación, para indicar si este es un comprobante de anulación o no | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 176 |
| 928 | Si el tipo de Comprobante a autorizar es 201 o 206, NO debe indicar el dato adicional código 22, Anulación. No corresponde a un comprobante Factura. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 176 |
| 929 | Si el tipo de Comprobante a autorizar es 201 o 206, debe indicar el dato adicional código 21, CBU y Alias emisor. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 176 |
| 930 | Si el tipo de Comprobante a autorizar es 202, 203, 207 o 208, NO debe indicar el dato adicional código 21, CBU y Alias emisor. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 931 | Para el tipo de dato adicional 21, 22 y 23, debe indicar el campo c1 | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 932 | Para el tipo de dato adicional 27, Opción de Transferencia, las opciones válidas son ADC para Agente de Depósito Colectivo o SCA para Sistema de Circulación Abierta | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 933 | Si el tipo de Comprobante a autorizar es 201 o 206, debe indicar el dato adicional código 27, Opción de Transferencia. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 934 | Si el tipo de Comprobante a autorizar es 202, 203, 207 o 208, NO debe indicar el dato adicional código 27, Opción de Transferencia. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 935 | Si el tipo de Comprobante a autorizar NO es 1, 2, 3, 201, 202, 203, NO debe indicar el dato adicional código 5, Cómputo IVA Crédito Fiscal. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 936 | Si el tipo de Comprobante a autorizar es 1, 2, 3, 201, 202, 203, y se indica el dato adicional código 5, se debera indicar el campo c1 (Motivo de Excepcion) de forma obligatoria. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 937 | Si el tipo de Comprobante a autorizar es 1, 2, 3, 201, 202, 203, y se indica el dato adicional código 5, y el campo el campo c1 (Motivo de Excepcion) NO es un numérico del 1 al 6. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 938 | Si el tipo de Comprobante a autorizar es 1, 2, 3, 201, 202, 203, y se indica el dato adicional código 5, y no se deberán utilizar ninguno de los restantes campos reservados a futuro campos de c2 a c6. | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 177 |
| 921 | Si t es igual a 2 (“Dato Adicional para Empresas Promovidas”), en c1 se deberá indicar el id de proyecto (el mismo deberá corresponder a la cuit emisora del comprobante) o cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen de Promoción Industrial. Los campos c2 a c6 no deberán informarse (reservados para uso futuro) | Observa | arrayObservaciones | `datoAdicional` › t / c1…c6 | 178 |
| 923 | Los tipos de dato adicional 21, 22 o 23 sólo corresponden a comprobantes de Factura Electrónica de Crédito MiPyME | Observa | arrayObservaciones | `datoAdicional` › t | 178 |
| 924 | Para el tipo de dato adicional 21, los campos c3 a c6 no deberán informarse (reservados para uso futuro) Para los tipos de dato adicional 22 o 23, los campos c2 a c6 no deberán informarse (reservados para uso futuro) | Observa | arrayObservaciones | `datoAdicional` › t / c1…c6 | 178 |


### informarAjusteIVACAEA (121)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10030 | Debe estar empadronada en el régimen de CAEA con estado activo o baja. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 194 |
| 165 | Si ocurrió un error imprevisto al momento de validar las actividades a quedar asociadas al comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigo / arrayActividades | 194 |
| 740 | Valores permitidos: 2 - Nota de Débito A 3 - Nota de Crédito A 7 - Nota de Débito B 8 - Nota de Crédito B 52 - Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 - Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante | 195 |
| 740 | El contribuyente no se encuentra habilitado a emitir (según el tipo de comprobante indicado) comprobantes A, A con Leyenda o A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / cuitRepresentada | 195 |
| 701 | Debe ser del tipo habilitado para CAEA - Codificación de Productos - opción Factura con Detalle y no debe estar bloqueado a la fecha en que se emitió el comprobante. Consultar método consultarPuntosVenta o consultarPuntosVentaCAEA | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta | 195 |
| 702 | Debe estar comprendida dentro de la fecha desde y fecha hasta de vigencia del CAEA | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 195 |
| 703 | El número de comprobante informado debe ser mayor en 1 al último informado para igual punto de venta y tipo de comprobante. De no existir comprobante informado para igual punto de venta y codigoTipoComprobante, el número de comprobante debe ser igual a 1 (uno) | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 195 |
| 704 | La fecha de emisión del comprobante debe ser mayor o igual a la fecha del último comprobante informado para igual tipo de comprobante y punto de venta. | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision / numeroPuntoVenta / numeroComprobante / codigoTipoComprobante | 196 |
| 705 | Debe informarse y corresponder a la CUIT | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoAutorizacion | 196 |
| 706 | Debe ser mayor a la fecha de entrada en vigencia del CAEA `<fechaDesde>` | Rechaza | arrayErrores | `comprobanteCAEARequest` › fecha en que se envía la solicitud | 196 |
| 707 | Si se informa uno de los campos debe informarse el otro. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 196 |
| 709 | La fecha de alta del numeroPuntoVenta debe ser menor o igual a la fechaHasta de la vigencia del CAEA que posee el comprobante que se está informando. | Rechaza | arrayErrores | `comprobanteCAEARequest` › CAEA / numeroPuntoVenta | 196 |
| 713 | Deberá ser igual a alguno de los siguientes valores: 1 – Productos 2 – Servicios 3 – Productos y Servicios | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoConcepto | 196 |
| 715 | Opcional. Debe informarse si algún ítem tiene `<codigoCondicionIVA>` igual a 4, 5 ó 6. | Rechaza | arrayErrores | `comprobanteCAEARequest` › arraySubtotalesIVA | 196 |
| 718 | Opcionales. Deberá informarse en los siguientes casos: - cuando `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52, 53, 201, 202, 203, 205, 206 o 207. -cuando `<codigoTipoComprobante>` es igual a 6, 7 u 8 y el importe total del comprobante `<importeTotal>` es mayor ó igual al monto en pesos resultante según RG4444. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 196 |
| 731 | Opcional. Si se informa debe informarse “A” (sin comillas) | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoAutorizacion | 197 |
| 732 | Opcional. Si se informa debe coincidir con la Fecha Hasta del CAEA informado | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaVencimiento | 197 |
| 733 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53 `<codigoTipoDocumento>` deberá ser igual a 80 (CUIT) | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento | 197 |
| 736 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarTiposDocumento | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento | 197 |
| 739 | Los informes de comprobantes para un mismo punto de venta y tipo de comprobante deben ser enviados en forma sincrónica: si el WS recibe una nueva solicitud para un punto de venta y tipo de comprobante dado mientras la anterior está siendo procesada, la nueva solicitud será rechazada | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta / codigoTipoComprobante | 197 |
| 741 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAEARequest` › importeGravado | 197 |
| 742 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAEARequest` › importeNoGravado | 197 |
| 743 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAEARequest` › importeExento | 197 |
| 744 | Deberá informarse en 0 (cero) | Rechaza | arrayErrores | `comprobanteCAEARequest` › importeSubtotal | 197 |
| 745 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAEARequest` › importeOtrosTributos | 197 |
| 746 | No debe informarse | Rechaza | arrayErrores | `comprobanteCAEARequest` › arrayOtrosTributos | 197 |
| 753 | Grupo de compradores no habilitado para el método | Rechaza | arrayErrores | `comprobanteCAEARequest` › arrayCompradores | 197 |
| 754 | La fecha/hora de generación es obligatoria para comprobantes CAEA por contingencia (no se informó el campo fecha/hora generación y el punto de venta es del tipo CAEA por Contingencia). A partir del 01/08/2026 sera obligatoria para comprobantes CAEA sin distinción del tipo de punto de venta (por Contingencia o no) | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroPuntoVenta / fechaHoraGen | 197 |
| 886 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT), se debe validar que el número ingresado esté registrado en el padrón del Organismo y se encuentre en estado activo para dicho tipo de documento. Esta validación aplica para comprobantes: - Comprobantes Clase A - Comprobantes Clase B. Donde `<numeroDocumento>` es distinto de la CUIT de Sujeto No Categorizado y no sea una CUIT Pais, y el `<importeTotal>` del comprobante (pesificado al tipo de cambio informado en caso de moneda extranjera) sea igual o mayor a $10.000.000. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento / codigoTipoComprobante | 198 |
| 141 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue inactivado o invalidado. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 198 |
| 143 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido caracterizada como sujeto no confiable en materia de Seguridad Social. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 198 |
| 145 | Si `<codigoTipoDocumento>` es igual a 80 (CUIT) y el `<numeroDocumento>` del receptor/comprador fue limitada por haber sido marcada como Apocrifa. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 199 |
| 148 | Si el `<numeroDocumento>` del receptor/comprador se encuentra marcada como fallecido y no está marcado como sucesión indivisa. | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 199 |
| 466 | Si `<codigo>` se encuentra mas de una vez en el array de actividades (no admite repetidos). Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigo / arrayActividades | 199 |
| 467 | Si `<codigo>` no se encuentra entre las actividades vigentes para la cuit representada. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigo / arrayActividades | 199 |
| 468 | Si `<codigo>` se encuentra asociado a un conjunto de actividades de un “rubro” y se encontraron otros `<codigo>` dentro del array que se encuentran asociados a otro conjunto de un “rubro” distinto. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigo / arrayActividades | 199 |
| 470 | Si ocurrio un error imprevisto al validar los comprobantes asociados que sean de tipo remito (88, 990, 91, 995, 997, 993, 994). Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 199 |
| 471 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y no fue encontrado en los registros de ARCA, o bien fue encontrado, pero la información asociada al mismo no es la esperada. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 200 |
| 472 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero el mismo se encuentra en un estado inválido. Dichos estados varian según el tipo de remito del que se trate. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 200 |
| 473 | Si el comprobante asociado es del tipo remito (91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero la cuit del receptor de dicho remito no coincide con la cuit del receptor del comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento / arrayComprobantesAsociados | 200 |
| 475 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y el tipo de comprobante asociado es remito, pero el mismo no es carnico (88, 990, 91, 997, 993, 994), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 200 |
| 476 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” o “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado o Tabaco en Hebras (91, 997, 993, 994, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 201 |
| 477 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco Acondicionado” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco Acondicionado (990, 91, 997, 993, 994, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 201 |
| 478 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Tabaco en Hebras” y el tipo de comprobante asociado es remito, pero el mismo no es Tabaco en Hebras (88, 91, 997, 993, 994, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 201 |
| 480 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y el tipo de comprobante asociado es remito, pero el mismo no es Harina (88, 91, 997, 995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 201 |
| 481 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Harina” y no se especifico ningún Remito del tipo Harina (993 y 994), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 202 |
| 482 | Si el conjunto de códigos indicados en el Array de Actividades identifican el comprobante como perteneciente al rubro “Compra y Venta de Carne” y no se especifico ningún Remito del tipo Carnico (995), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados codigo / arrayActividades | 202 |
| 483 | Los códigos de concepto permitidos para asociar Remitos Cárnicos (995) al Comprobante son 1 – Productos y 3 – Productos y Servicios | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoConcepto / arrayComprobantesAsociados | 202 |
| 484 | Si no se especifican actividades, y el Remito a Asociar es un Remito Sectorial (88, 990, 993, 994, 995, 997), se observara el comprobante. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados arrayActividades | 202 |
| 485 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y fue encontrado en los registros de ARCA, pero se encuentra marcado como de exportación, mientras que el presente servicio solo acepta Remitos para el Mercado. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / fechaEmision / arrayComprobantesAsociados | 202 |
| 486 | Si el comprobante asociado es del tipo remito (88, 990, 91, 995, 997, 993, 994), y ya fue declarado una vez en el array de comprobantes asociados. Ver el Anexo de Rubros de Actividades y Remitos | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit / arrayComprobantesAsociados | 203 |
| 490 | A partir del 5 de abril de 2025 podrá enviarse de forma opcional el campo Condición Frente al IVA del receptor, hasta tanto entre en vigencia su obligatoriedad reglamentada por la Resolución General N° 5616, en cuyo momento pasará a rechazar la emisión de comprobantes sin este dato. Si no se informa la condición de IVA del Receptor (obligatoria) o bien se informa un valor no contemplado por el servicio. Ver método consultarCondicionesIVAReceptor | Observa | arrayObservaciones | `comprobanteCAEARequest` › condicionIVAReceptor / fechaEmision | 203 |
| 491 | A partir del 5 de abril de 2025 podrá enviarse de forma opcional el campo Condición Frente al IVA del receptor, hasta tanto entre en vigencia su obligatoriedad reglamentada por la Resolución General N° 5616, en cuyo momento pasará a rechazar la emisión de comprobantes sin este dato. Si se informa una combinación invalida de Condición de IVA del Receptor y Tipo de Comprobante. Ver método consultarCondicionesIVAReceptor | Observa | arrayObservaciones | `comprobanteCAEARequest` › condicionIVAReceptor / codigoTipoComprobante / fechaEmision | 203 |
| 708 | Si `<codigoTipoDocumento>` es igual a 80, 86 o 87, `<numeroDocumento>` debe ser válido y activo, excepto para `<codigoTipoComprobante>` 6, 7 u 8, `<codigoTipoDocumento>` 80 y `<numeroDocumento>` igual a 23000000000. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 204 |
| 717 | No debe estar informado como CAEA No utilizado | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoAutorizacion | 204 |
| 747 | Debe ser igual a la sumatoria de `<subtotalIVA>``<importe>` (dentro del arraySubtotalesIVA). | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeTotal | 204 |
| 710 | Deberá ser igual a alguno de los valores permitidos. Consultar método consultarMonedas | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoMoneda | 204 |
| 122 | En caso de enviar la marca de que el pago del comprobante se realiza en la misma moneda extranjera para comprobantes que no sean facturas. Unicamente se puede utilizar con los códigos habilitados (1,6,51,201,206) | Observa | arrayObservaciones | `comprobanteCAEARequest` › cancelaEnMismaMonedaE xtranjera | 204 |
| 182 | No podrá ser inferior al 2% ni superior en un 400% del que suministra ARCA como orientativo de acuerdo a la cotización oficial | Observa | arrayObservaciones | `comprobanteCAEARequest` › cotizacionMoneda | 204 |
| 726 | Debe ser igual a 1 (uno) si `<codigoMoneda>` es igual a PES | Observa | arrayObservaciones | `comprobanteCAEARequest` › cotizacionMoneda | 204 |
| 174 | En caso de enviar un valor inválido para la marca de que el pago de la factura se realiza en la misma moneda extranjera. Los valores válidos son S, N o vacío | Observa | arrayObservaciones | `comprobanteCAEARequest` › cancelaEnMismaMonedaE xtranjera | 204 |
| 175 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera y enviar como código de moneda el Peso Argentino | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoMoneda / cancelaEnMismaMonedaE xtranjera | 204 |
| 181 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera, que codigoMoneda es del grupo de monedas con cotización del Banco de la Nación Argentina (ver Anexo Monedas BNA), que haya cotización y que la misma no coincida exactamente con el valor enviado en el campo cotizacionMoneda. En cuyo caso se podrá omitir el mismo para que la cotización de la factura sea la obtenida de los registros de ARCA | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoMoneda / cotizacionMoneda / cancelaEnMismaMonedaE xtranjera | 205 |
| 194 | El campo es obligatorio a excepción de los casos para los cuales se envia el campo cancelaEnMismaMonedaExtranjera y se puede obtener la cotizacion asociada al codigoMoneda si esta es del grupo de monedas del Banco de la Nación Argentina (ver Anexo Monedas BNA) | Rechaza | arrayErrores | `comprobanteCAEARequest` › cotizacionMoneda | 205 |
| 195 | No es posible indicar una cotización negativa | Rechaza | arrayErrores | `comprobanteCAEARequest` › cotizacionMoneda | 205 |
| 748 | Debe ser igual a la sumatoria de la totalidad de los campos `<importeItem>`. | Observa | arrayObservaciones | `comprobanteCAEARequest` › importeTotal | 205 |
| 727 | Debe informarse solo si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaServicioDesde | 205 |
| 728 | Debe informarse solo si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaServicioHasta | 205 |
| 729 | Debe informarse solo si `<codigoConcepto>` es igual a 2 ó 3. En otro caso no corresponde. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaVencimientoPago | 205 |
| 730 | La fecha de vencimiento de pago debe ser mayor o igual a la fecha de emisión. | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaVencimientoPago / fechaEmision | 205 |
| 734 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53, la CUIT del receptor debe encontrarse activa en IVA o en monotributo. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 205 |
| 735 | El Receptor no puede ser igual al Emisor | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 206 |
| 737 | La Fecha de Servicio desde debe ser menor o igual a la Fecha de Servicio Hasta | Observa | arrayObservaciones | `comprobanteCAEARequest` › fechaServicioDesde / fechaServicioHasta | 206 |
| 738 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 o 53 y `<codigoTipoDocumento>` es igual a 80 (CUIT), dicha CUIT deberá encontrarse activa en el Sistema Registral | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 206 |
| 750 | Debe estar dado de alta en el Impuesto al Valor Agregado al momento de la fecha de emisión del comprobante | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada / fechaEmision | 206 |
| 751 | Debe encontrarse habilitado a comprobantes clase 'A' a la fecha de emisión del comprobante | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada / codigoTipoComprobante / fechaEmision | 206 |
| 755 | La fecha/hora de generación solo debe informarse para comprobantes CAEA por contingencia (se informó el campo fecha/hora generación pero el punto de venta no es del tipo CAEA por Contingencia). Se informa que esta validación quedará fuera de vigencia a partir del 31/07/2026, siendo absorbida por las condiciones de la validación 754. | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroPuntoVenta / fechaHoraGen | 206 |
| 756 | Para comprobantes CAEA por contingencia: si se indica `<codigoConcepto>` igual a 1, la fecha de emisión del comprobante puede ser hasta 5 días anteriores o posteriores respecto de la fecha de generación, pero sin extenderse al mes siguiente; si se indica `<codigoConcepto>` igual a 2 ó 3 puede ser hasta 10 días anteriores o posteriores a la fecha de generación | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroPuntoVenta / fechaHoraGen / fechaEmision / codigoConcepto | 206 |
| 777 | Si `<codigoTipoComprobante>` es igual a 202, 203, 207 ó 208 perteneciente a Factura de Crédito Electrónica no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / periodoComprobantesAso ciados | 207 |
| 778 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. Falta informar comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito válido/s o informar un período de comprobantes asociados válido | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAso ciados | 207 |
| 779 | Si `<codigoTipoComprobante>` es igual a 2, 3, 7, 8, 52 ó 53. No debe informar un período de comprobantes asociados cuando informa comprobante/s asociado/s puntual del tipo factura, nota de debito o nota de crédito | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados / periodoComprobantesAso ciados | 207 |
| 780 | Si `<codigoTipoComprobante>` es igual a 1, 2, 51, 201 ó 206 correspondientes a Facturas no corresponde informar un periodo de comprobantes asociados. | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / periodoComprobantesAso ciados | 207 |
| 782 | Si `<codigoTipoComprobante>` es igual a 1, 2, 3, 51, 52 ó 53 la CUIT del receptor es activa en monotributo | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoDocumento / numeroDocumento | 207 |
| 784 | Si `<cuitRepresentada>` tiene pendiente de presentación el formulario de habilitación de comprobantes o su fecha de presentación es anterior a tu alta en IVA | Observa | arrayObservaciones | `comprobanteCAEARequest` › cuitRepresentada | 207 |
| 786 | Si `<numeroDocumento>` es inexistente en el padron del Organismo | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroDocumento | 207 |
| 792 | Siendo `<codigoTipoComprobante>` una Nota de Crédito (3, 8, 53, 203 y 208), si la sumatoria de los importes totales de los elementos del array `<arrayComprobantesAsociados>` (sin incluir Remitos) supera el `<importeTotal>` de la Nota de Crédito | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / arrayComprobantesAsociados / importeTotal | 208 |
| 803 | El comprobante asociado podrá ser: 1 – Factura A 2 – Nota de Débito A 3 – Nota de Crédito A 6 – Factura B 7 – Nota de Débito B 8 – Nota de Crédito B 51 – Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN 52 – Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 53 – Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN 91 – Remito Papel 88 – Remito Electrónico de Tabaco Acondicionado 990 – Remito Electrónico de Tabaco en Hebras 993 – Remito Electrónico de Harina en Camion 994 – Remito Electrónico de Harina en Tren 995 – Remito Electrónico de Carne 997 – Remito Electrónico Azucar Mercado Interno Consultar método consultarTiposComprobante | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante | 209 |
| 820 | La fecha de emisión del comprobante asociado informada no coincide con la existente en nuestros registros | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 209 |
| 821 | La fecha de emisión de este comprobante no puede ser anterior a la factura asociada | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 210 |
| 822 | El comprobante asociado no posee cuit del receptor | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 210 |
| 823 | El comprobante asociado posee otro cuit de receptor | Rechaza | arrayErrores | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante / cuit | 210 |
| 824 | Si el punto de venta del comprobante asociado NO es del tipo electrónico debe informar la fecha de emisión | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 210 |
| 825 | Si el punto de venta del comprobante asociado NO es del tipo electrónico la fecha de emisión no puede ser posterior a la fecha de la autorización | Rechaza | arrayErrores | `comprobanteCAEARequest` › fechaEmision | 210 |
| 889 | Si `<numeroDocumento>` es la CUIT para Sujeto No Categorizado (23000000000) y el `<codigoTipoDocumento>` es distinto de B (6, 7 u 8) o si la `<condicionIVAReceptor>` no se corresponde con Sujeto No Categorizado | Rechaza | arrayErrores | `comprobanteCAEARequest` › numeroDocumento / codigoTipoDocumento / condicionIVAReceptor | 210 |
| 800 | Deberá ser igual a 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 1, 6 o 51 Deberá ser igual a 1, 2, 3, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 2 o 3. Deberá ser igual a 6, 7, 8, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 7 u 8. Deberá ser igual a 51, 52, 53, 88 o 990 si el tipo de comprobante cuya autorización se solicita es igual a 52 o 53. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante | 211 |
| 801 | Si el punto de venta es del tipo electrónico el comprobante asociado `<codigoTipoComprobante>` `<numeroPuntoVenta>` `<numeroComprobante>` deberá obrar en las bases del organismo. | Observa | arrayObservaciones | `comprobanteCAEARequest` › codigoTipoComprobante / numeroPuntoVenta / numeroComprobante | 211 |
| 802 | El tipo de punto de venta, en caso de ser electrónico, deberá ser alguno de los siguientes: RECE para aplicativo y web services, Factura en Línea - Responsable Inscripto, Factura en Línea - Método Alternatvi o al RECE (límite de 100), Codificación de Productos - Web services, Codificación de Productos - Factura en Línea, CAEA - Fact. Elect. (RECE) - RI IVA o CAEA - Codificación de Productos. | Observa | arrayObservaciones | `comprobanteCAEARequest` › numeroPuntoVenta | 211 |
| 2800 | La fechaHasta debe ser posterior o igual fechaDesde | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 212 |
| 2801 | La fechaHasta del periodoComprobantesAsociados debe ser anterior o igual a la fecha de emisión del comprobante por el cual se está solicitando la autorización | Rechaza | arrayErrores | `periodoComprobantesAsociados` › fechaHasta / fechaEmision | 212 |
| 2802 | Si el comprobante a autorizar incluye percepciones, el rango de fecha informado debe corresponder al mismo Mes/Año | Observa | arrayObservaciones | `periodoComprobantesAsociados` › fechaDesde / fechaHasta | 212 |
| 1000 | Valores permitidos: 4, 5, 6 | Rechaza | arrayErrores | `subtotalIVA` › codigo | 212 |
| 1002 | No se deberá repetir (no pueden incluírse dos subtotales IVA con el mismo código) | Rechaza | arrayErrores | `subtotalIVA` › codigo | 212 |
| 1003 | Si existen uno o más ítems con una determinada alícuota IVA, deberá existir el correspondiente subtotal IVA para dicha alícuota. No se sebe incluír un subtotal IVA si dicha alícuota no está presente en al menos un ítem. | Rechaza | arrayErrores | `subtotalIVA` › codigo | 213 |
| 1004 | Deberá coincidir con la sumatoria de todos los `<importeItem>` de `<item>` donde la alícuota de IVA coincida con la indicada, es decir, donde `<codigoCondicionIVA>` de `<item>` = `<codigo>` de `<subtotalIVA>`. | Observa | arrayObservaciones | `subtotalIVA` › importe | 213 |
| 1005 | La suma de los subtotales de IVA no puede ser negativa. | Observa | arrayObservaciones | `subtotalIVA` › importe | 213 |
| 1123 | Deberá informarse 1 (uno). | Rechaza | arrayErrores | `item` › unidadesMtx | 213 |
| 1124 | Deberá informarse el código 7790001001139 | Rechaza | arrayErrores | `item` › codigoMtx | 213 |
| 1105 | Opcional. Longitud máxima 50 posiciones. | Rechaza | arrayErrores | `item` › codigo | 213 |
| 1106 | Cantidad máxima de caracteres permitidos 4000. Importante: no es necesario (ni recomendable) completar con espacios. | Rechaza | arrayErrores | `item` › descripcion | 213 |
| 1125 | No debe informarse | Rechaza | arrayErrores | `item` › cantidad | 213 |
| 1126 | Deberá informarse el código 7 - unidades | Rechaza | arrayErrores | `item` › codigoUnidadMedida | 213 |
| 1127 | No debe informarse | Rechaza | arrayErrores | `item` › precioUnitario | 214 |
| 1128 | No debe informarse | Rechaza | arrayErrores | `item` › importeBonificacion | 214 |
| 1129 | Deberá coincidir con alguno de los siguientes valores permitidos: 4, 5 o 6 | Rechaza | arrayErrores | `item` › codigoCondicionIVA | 214 |
| 1112 | Obligatorio para `<codigoTipoComprobante>` igual a 1, 2, 3, 51, 52 o 53. No corresponde para `<codigoTipoComprobante>` igual a 6, 7 u 8. | Rechaza | arrayErrores | `item` › importeIVA | 214 |
| 1130 | Para `<codigoTipoComprobante>` igual a 2, 3, 52 o 53 deberá ser igual a `<importeItem>` | Rechaza | arrayErrores | `item` › importeIVA | 214 |
| 1131 | Si `<codigoTipoComprobante>` es igual a 2, 3, 52 o 53 deberá ser mayor a 0 (cero). | Rechaza | arrayErrores | `item` › importeIVA | 214 |
| 1132 | Deberá ser mayor a 0 (cero) | Rechaza | arrayErrores | `item` › importeItem | 214 |
| 920 | Valores permitidos: consultar método consultarTiposDatosAdicionales | Rechaza | arrayErrores | `datoAdicional` › t | 214 |
| 922 | Sólo se puede incluír un dato adicional con t = 2 (sólo se permite un id de proyecto por comprobante) | Rechaza | arrayErrores | `datoAdicional` › t / c1…c6 | 214 |
| 921 | Si t es igual a 2 (“Dato Adicional para Empresas Promovidas”), en c1 se deberá indicar el id de proyecto (el mismo deberá corresponder a la cuit emisora del comprobante) o cero (0) en caso de que la actividad facturada no esté alcanzada por el Régimen de Promoción Industrial. Los campos c2 a c6 no deberán informarse (reservados para uso futuro) | Observa | arrayObservaciones | `datoAdicional` › t / c1…c6 | 215 |


### informarCAEANoUtilizado (6)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10030 | Debe estar empadronada en el régimen de CAEA con estado activo o baja. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 220 |
| 1200 | Debe ser del tipo de código de autorización CAEA | Rechaza | arrayErrores | `informarCAEANoUtilizadoRequest` › CAEA | 221 |
| 1201 | Debe corresponder a la CUIT indicada en `<cuitRepresentada>` | Rechaza | arrayErrores | `informarCAEANoUtilizadoRequest` › CAEA | 221 |
| 1202 | No debe estar informado como utilizado en algún comprobante | Rechaza | arrayErrores | `informarCAEANoUtilizadoRequest` › CAEA | 221 |
| 1203 | La fecha de envío de la solicitud debe ser mayor a la fecha de inicio de vigencia del CAEA que se está informando. | Rechaza | arrayErrores | `informarCAEANoUtilizadoRequest` › fecha de envío de la solicitud | 221 |
| 1208 | No debe estar informado como no utilizado | Rechaza | arrayErrores | `informarCAEANoUtilizadoRequest` › CAEA | 221 |


### informarCAEANoUtilizadoPtoVta (8)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10030 | Debe estar empadronada en el régimen de CAEA con estado activo o baja. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 227 |
| 1200 | Debe ser del tipo de código de autorización CAEA | Rechaza | arrayErrores | `informarCAEANoUtilizadoPtoVtaRequest` › CAEA | 227 |
| 1201 | Corresponda a la CUIT indicada en `<cuitRepresentada>` | Rechaza | arrayErrores | `informarCAEANoUtilizadoPtoVtaRequest` › CAEA | 227 |
| 1203 | La fecha de envío de la solicitud debe ser mayor a la fecha de inicio de vigencia del CAEA que se está informando. | Rechaza | arrayErrores | `informarCAEANoUtilizadoPtoVtaRequest` › fecha de envío de la solicitud | 227 |
| 1204 | Debe corresponder a un punto de venta CAEA | Rechaza | arrayErrores | `informarCAEANoUtilizadoPtoVtaRequest` › numeroPuntoVenta | 227 |
| 1205 | El punto de venta deberá haber estado activo durante la vigencia del CAEA | Rechaza | arrayErrores | `informarCAEANoUtilizadoPtoVtaRequest` › numeroPuntoVenta | 227 |
| 1206 | No debe estar informado como utilizado en algún comprobante para el punto de venta indicado | Rechaza | arrayErrores | `informarCAEANoUtilizadoPtoVtaRequest` › CAEA / numeroPuntoVenta | 227 |
| 1207 | No debe estar informado como no utilizado para el punto de venta indicado | Rechaza | arrayErrores | `informarCAEANoUtilizadoPtoVtaRequest` › CAEA / numeroPuntoVenta | 227 |


### consultarPtosVtaCAEANoInformados (3)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10030 | Debe estar empadronada en el régimen de CAEA con estado activo o baja. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 234 |
| 1300 | Debe ser un CAEA previamente otorgado | Rechaza | arrayErrores | `consultarPtosVtaCAEANoInformadosRequest` › CAEA | 234 |
| 1301 | Debe corresponder a la CUIT indicada en `<cuitRepresentada>` | Rechaza | arrayErrores | `consultarPtosVtaCAEANoInformadosRequest` › CAEA | 234 |


### consultarCAEA (3)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10030 | Debe estar empadronada en el régimen de CAEA con estado activo o baja. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 240 |
| 1300 | Debe ser un CAEA previamente otorgado | Rechaza | arrayErrores | CAEA | 241 |
| 1301 | Debe corresponder a la CUIT indicada en `<cuitRepresentada>` | Rechaza | arrayErrores | CAEA | 241 |


### consultarCAEAEntreFechas (2)

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 10030 | Debe estar empadronada en el régimen de CAEA con estado activo o baja. Se informa que esta validación quedará fuera de vigencia a partir del 01/06/2026. | Rechaza | arrayErrores | `authRequest` › cuitRepresentada | 247 |
| 1400 | fechaDesde debe ser menor o igual a fechaHasta | Rechaza | arrayErrores | `consultarCAEAEntreFechasRequest` › fechaDesde / fechaHasta | 247 |


## Tablas y datos

**Tipos de comprobante** (ejemplo de `consultarTiposComprobante`, pág. 271-272, y validaciones 100 y 203):

| Código | Descripción | Uso |
|---:|---|---|
| 1 | Factura A | Autorizar / informar |
| 2 | Nota de Débito A | Autorizar / informar |
| 3 | Nota de Crédito A | Autorizar / informar |
| 6 | Factura B | Autorizar / informar |
| 7 | Nota de Débito B | Autorizar / informar |
| 8 | Nota de Crédito B | Autorizar / informar |
| 51 | Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Autorizar / informar |
| 52 | Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Autorizar / informar |
| 53 | Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN | Autorizar / informar |
| 201 | Factura de Crédito Electrónica MiPyMEs (FCE) A | Autorizar (validación 100); no aparece en el ejemplo de `consultarTiposComprobante` |
| 202 | Nota de Débito Electrónica MiPyMEs (FCE) A | ídem |
| 203 | Nota de Crédito Electrónica MiPyMEs (FCE) A | ídem |
| 206 | Factura de Crédito Electrónica MiPyMEs (FCE) B | ídem |
| 207 | Nota de Débito Electrónica MiPyMEs (FCE) B | ídem |
| 208 | Nota de Crédito Electrónica MiPyMEs (FCE) B | ídem |
| 88 | Remito Electrónico de Tabaco Acondicionado (sólo para comprobantes asociados) | Asociado |
| 990 | Remito Electrónico de Tabaco en Hebras (sólo para comprobantes asociados) | Asociado |
| 91, 993, 994, 995, 997 | Remito R, Remito Harinero (camión 993, tren 994), Remito Cárnico 995, y 997 | Asociados (validaciones 170-186, 203). La descripción de 997 no figura en el manual |

La versión 0.25.4 (01/12/2025) "actualiza el método consultarTiposComprobante" por el reemplazo de los comprobantes M: la lista vigente puede diferir del ejemplo. NO VERIFICADO.

**Alícuotas de IVA** (`consultarAlicuotasIVA`, pág. 280): 3 = 0 %, 4 = 10.5 %, 5 = 21 %, 6 = 27 %. Que no figuren 8 (5 %) y 9 (2,5 %), que sí existen en wsfev1: NO VERIFICADO si es completo.

**Condiciones de IVA del ítem** (`consultarCondicionesIVA`, pág. 285): 1 No gravado, 2 Exento, 3 0 %, 4 10.5 %, 5 21 %, 6 27 %.

**Condición frente al IVA del receptor** (`consultarCondicionesIVAReceptor`, ejemplo con `codigoTipoComprobante` 6, pág. 291): 4 IVA Sujeto Exento, 5 Consumidor Final, 7 Sujeto No Categorizado, 10 IVA Liberado – Ley N° 19.640, 15 IVA No Alcanzado. Para el resto de los tipos ver la tabla de wsfev1 (`wsfev1.md` §7.8); que sea la misma: NO VERIFICADO.

**Tipos de documento** (`consultarTiposDocumento`, pág. 276): el ejemplo muestra sólo 0 CI Policía Federal, 1 CI Buenos Aires, 2 CI Catamarca "...". La tabla genérica completa está en `wsfev1.md` §7.2.

**Monedas** (`consultarMonedas`, pág. 296): el ejemplo muestra DOL Dólar Estadounidense, PES Pesos Argentinos, 002 Dólar Libre EEUU "...". Tabla genérica en `wsfev1.md` §7.5.

**Monedas con cotización automática del Banco Nación** (Anexo, pág. 360-361; RG 5616/2024). Con `cancelaEnMismaMonedaExtranjera` se puede omitir `cotizacionMoneda` para estas monedas y el servicio toma la del día hábil anterior (a la fecha de emisión si es anterior a hoy; a hoy si es igual o posterior). Si se informa, tiene que coincidir exactamente (192):

| Código | Descripción |
|---|---|
| 9 | Franco Suizo |
| 14 | Coronas Danesas |
| 15 | Coronas Noruegas |
| 16 | Coronas Suecas |
| 18 | Dólar Canadiense |
| 19 | Yenes |
| 21 | Libra Esterlina |
| 26 | Dólar Australiano |
| 60 | Euro |
| 64 | Yuan |
| DOL | Dólar Estadounidense |
| 2 | Dólar Libre EEUU |

(El texto del PDF tiene las columnas corridas; el orden de arriba sale de la extracción por tablas. El manual escribe "9", "2" sin ceros; en `consultarMonedas` los códigos son de 3 caracteres, `002`. Cuál usar al comparar: NO VERIFICADO.)

**Unidades de medida** (`consultarUnidadesMedida`, pág. 305): el ejemplo muestra 0 " " (descripción en blanco), 1 kilogramos, 2 metros "...". Las validaciones usan 95 (cantidad negativa: anulación o devolución, según 515 y 519), 97 (señas o anticipos) y 99 (bonificación). La tabla genérica (ver `wsfexv1.md`) no tiene 95. La lista real: NO VERIFICADO.

**Tipos de tributo** (`consultarTiposTributo`, pág. 324): el ejemplo muestra `01` impuestos nacionales, `02` impuestos provinciales "...". El 13 es la Percepción de IVA No Categorizado (145, 150) y el 99 "otro" exige `descripcion` (301). Lista real: NO VERIFICADO; la de wsfev1 está en `wsfev1.md` §7.6.

**Tipos de datos adicionales** (pág. 330 y validaciones 320-340): 1 Entes Reguladores, 2 Empresas Promovidas (c1 = id de proyecto o 0), 5 Cómputo IVA Crédito Fiscal (c1 = motivo de excepción 1 a 6, RG 4520/19), 10 Educación Pública de Gestión Privada (c1 0/1, c2 tipo y c3 número de documento), 11 Bienes Inmuebles, 12 Locación temporaria de inmuebles con fines turísticos, 13 Representantes de Modelos, 14 Agencias de Publicidad, 15 Personas físicas de Modelaje (c1 0/1), 21 CBU y Alias del emisor (FCE), 22 Anulación (c1 S/N, FCE), 23 (FCE, sin descripción en el manual), 27 Opción de Transferencia (`ADC` o `SCA`, FCE). Sólo un dato adicional por comprobante (322).

**Rubros de actividades y remitos** (Anexo, pág. 355-359). Un conjunto de actividades pertenece a un rubro si todas son de ese rubro o de "Otros"; mezclar dos rubros con nombre es inválido (168):

| Rubro | Actividades | Remitos asociables |
|---|---|---|
| Tabaco | 461039, 463300 | 88 Tabaco Acondicionado o 990 Tabaco en Hebras |
| Tabaco (acondicionado) | 120010 PREPARACIÓN DE HOJAS DE TABACO | 88 |
| Tabaco (en hebras) | 120099 ELABORACIÓN DE PRODUCTOS DE TABACO N.C.P. | 990 |
| Harina | 469090, 471120, 471130, 472120, 472190, 106110, 463159, 463180, 463199 | 993 Harina en camión o 994 Harina en tren |
| Compra y Venta de Carne | 101040, 101099, 101011, 101012, 463121, 461031, 461032 | 995 Cárnico |

**Datos de prueba del manual:** `cuitRepresentada` 66666666666; receptores 30000000007 (CUIT) y 24999999 (DNI); puntos de venta 4000 (CAE) y 1000 (CAEA); `codigoMtx` 0123456789913, 0123456779914, 0123456744912; actividades 120010 y 463300. El historial (0.20) dice que en homologación se pueden usar los "Códigos Genéricos establecidos en el Apartado B, del Anexo VII de la RG AFIP N° 2.904/2010" como GTIN; no se relevaron.

## Comportamiento a simular

**Numeración.** Correlativa por CUIT + punto de venta + tipo: el número debe ser el último + 1, y 1 si no hay ninguno (102). La fecha de emisión no puede ser anterior a la del último del mismo tipo y punto (104). Los puntos de venta CAE y CAEA son distintos (101 exige punto "CAE Codificación de Productos – Web Services"); `consultarUltimoComprobanteAutorizado` sirve para los dos.

**Sin idempotencia.** "Si se envía una solicitud nuevamente y esta ya había sido aceptada, el sistema la rechazará indicando un error de correlatividad en la numeración del comprobante" (pág. 13), o sea 102. Para recuperarse de un corte: `consultarComprobante` o `consultarUltimoComprobanteAutorizado`. El simulador **no** debe devolver el CAE ya otorgado ante un reenvío.

**Concurrencia.** Pedidos simultáneos para el mismo punto de venta y tipo: el segundo se rechaza con 135 mientras el primero se procesa.

**Último comprobante.** Sin comprobantes previos, `consultarUltimoComprobanteAutorizado` responde error 1502 (no 0).

**Resultado.** `A` aprobado con CAE; `O` aprobado con observaciones, también con CAE (pág. 21); `R` rechazado, sin `comprobanteResponse` y con `arrayErrores`. El manual dice que `comprobanteResponse` "existe si el resultado es Aprobado" (pág. 29); por la pág. 21 también existe con `O`.

**CAE.** `long` de 14 dígitos. `fechaVencimientoCAE`: el ejemplo A da emisión + 15 días (2010-11-01 → 2010-11-16) y el B emisión + 10 (2010-12-15 → 2010-12-25). La regla: NO VERIFICADO. Si no se manda `fechaEmision`, se asigna la fecha de proceso (103).

**Fechas.** Con concepto 1, `fechaEmision` hasta 5 días antes o después de la generación sin pasar al mes siguiente; con 2 o 3, hasta 10 días (103). `fechaServicioDesde/Hasta` y `fechaVencimientoPago` sólo con concepto 2 o 3 (122-124). `fechaHoraGen` sólo para CAEA (146); obligatoria en CAEA desde la 0.25.7 (754).

**Cálculo por ítem (clase A, 1/2/3/51/52/53).** `precioUnitario` sin IVA. Si la unidad no es 95, 97 ni 99: `importeIVA` = (`precioUnitario` × `cantidad` − `importeBonificacion`) × alícuota (515) e `importeItem` = (`precioUnitario` × `cantidad` − `importeBonificacion`) × (1 + alícuota) (519). Con 95, lo mismo con signo negativo. Con 99 (bonificación) el ítem es negativo y su IVA ≤ 0; con 97 (señas) puede tener cualquier signo (517, 518). `importeIVA` es obligatorio en clase A (514).

**Cálculo por ítem (clase B, 6/7/8).** `precioUnitario` **con** IVA; `importeIVA` no corresponde (514). `importeGravado` = Σ (`importeItem` − IVA calculado con la alícuota de cada ítem) para condiciones 3 a 6 (110).

**Totales.** `importeNoGravado` = Σ ítems condición 1 (111); `importeExento` = Σ condición 2 (112); `importeSubtotal` = no gravado + gravado + exento (113); `importeOtrosTributos` = Σ `otroTributo/importe` (114); `importeTotal` = subtotal + otros tributos + Σ `subtotalIVA/importe` (115) y también = otros tributos + Σ `importeItem` (116). Un `subtotalIVA` por alícuota presente en los ítems, sin repetir (402, 403), igual a la suma de los `importeIVA` de esa alícuota (401). Tolerancia: error relativo ≤ 0,01 % o absoluto ≤ 0,01 × cantidad de elementos sumados.

**Moneda.** `cotizacionMoneda` = 1 con PES (120); si no, entre el 2 % y el 400 % de la orientativa de ARCA (119). Obligatoria salvo que venga `cancelaEnMismaMonedaExtranjera` y la moneda sea del Banco Nación (194). Ver la tabla de monedas BNA.

**Receptor.** Clase A y FCE: documento obligatorio y tipo 80 (128, 129). Clase B: obligatorio desde el monto de la RG 4444 (128). El receptor no puede ser el emisor (131). Para consumidor final no categorizado: CUIT 23000000000 sólo con clase B (342). `condicionIVAReceptor` hoy observa si falta o no combina con el tipo (190, 191); la RG 5616 lo hará obligatorio (historial 0.25.0).

**CAEA (régimen y RG 5782/2025).**

- `solicitarCAEA` con `periodo` (AAAAMM) y `orden` (1 = días 1 a 15, 2 = del 16 a fin de mes). Se puede pedir desde 5 días corridos antes del inicio de la quincena hasta su fin (602). Uno solo por CUIT, período y orden (604): un segundo pedido se rechaza, no devuelve el mismo.
- La respuesta trae `fechaDesde`/`fechaHasta` de la quincena y `fechaTopeInforme`. En los ejemplos el tope es fin de quincena + 1 mes (2010-11-15 → 2010-12-15). La regla: NO VERIFICADO.
- Desde el 01/06/2026 ya no hace falta empadronarse en CAEA (10020, 10022 y 10030 quedan en desuso). Desde el 01/08/2026 los puntos de venta CAEA son de contingencia y tienen que compartir domicilio con un punto CAE o controlador fiscal (10027 rechaza, 10028 observa); `fechaHoraGen` pasa a ser obligatoria (754 rechaza, 755 en desuso) (historial 0.25.6 y 0.25.7, pág. 378-379).
- `informarComprobanteCAEA`: un comprobante por llamada, con `codigoTipoAutorizacion` `A` y el CAEA en `codigoAutorizacion`. Respuesta `resultado` A/O/R, `fechaProceso` y `comprobanteCAEAResponse{CAEA, tipo, punto, número}`.
- `informarCAEANoUtilizado` / `informarCAEANoUtilizadoPtoVta`: marcan el CAEA (o el CAEA en un punto) como no usado; después no se puede usar (1202, 1206, 1207, 1208 controlan los dos sentidos).
- `consultarPtosVtaCAEANoInformados`: los puntos CAEA del emisor sin comprobantes informados ni marcados como no usados para ese CAEA.
- Los incumplimientos de rendición (2 quincenas consecutivas o 4 alternadas sin rendir) observan el `solicitarCAEA` con 10026.

**`consultarComprobante`.** Devuelve el `ComprobanteType` completo con `codigoTipoAutorizacion` (`E` CAE, `A` CAEA), `codigoAutorizacion` (el CAE o CAEA) y `fechaVencimiento`, más las observaciones con que se aprobó.

**Relación con wscdc.** Lo que se autoriza o informa acá es lo que wscdc constata: mapeo de campos en `wscdc.md` (`cuitRepresentada` → `CuitEmisor`, `codigoTipoComprobante` → `CbteTipo`, `fechaEmision` yyyy-mm-dd → `CbteFch` yyyymmdd, `importeTotal` → `ImpTotal`, CAE o CAEA → `CodAutorizacion`).

**Validaciones que dependen de padrones de ARCA** (no se pueden simular fielmente sin datos sembrados): emisor activo, con actividad, domicilio e IVA (10000-10003), apócrifos (10005, 163, 303), empadronamiento MTXCA (10010), receptor activo en IVA o monotributo (130, 134, 155, 156), FCE obligatoria según empresa grande (150-152), GTIN registrados (504), actividades vigentes (167), remitos existentes (171-173, 205-207), CBU del emisor (328).

## No verificado

- WSAA service id `wsmtxca` (sólo [SEC]).
- Si el WSDL roto de homologación del 2026-10-02 es intermitente.
- El origen exacto de `BL<n> <fecha> 500` en homologación (se infiere que el F5 tapa los HTTP 500).
- Si con un token válido el servicio acepta el namespace `.gob.ar` de los ejemplos del manual.
- Las `descripcion` reales de todos los errores y observaciones de negocio (el manual no trae ninguna).
- Si devuelve todos los errores juntos o sólo el primero, y en qué orden evalúa.
- Si las restricciones de los tipos simples del WSDL se validan como fault o como error de negocio.
- La regla de `fechaVencimientoCAE` y la de `fechaTopeInforme`.
- `bloqueado`: `S`/`N` (WSDL) o `Si`/`No` (ejemplos).
- Las listas reales de los recuperadores (tipos de comprobante vigentes tras el cambio de clase M, documentos, monedas, unidades con 95, tributos, datos adicionales).
- Los textos de 601-602 equivalentes (CUIT fuera del token) en el fault.
- Si SOAP 1.2, que respondió en producción para `dummy`, funciona igual para las demás operaciones.
