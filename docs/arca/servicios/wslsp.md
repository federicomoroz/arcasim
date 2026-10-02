# wslsp

Liquidación del Sector Pecuario (`LspService`). Con este servicio, quien compra o consigna hacienda (consignatarios, frigoríficos, matarifes, productores en compra o venta directa) genera una liquidación de hacienda bovina, porcina o avícola y obtiene un **CAE** (Código de Autorización Electrónico, no COE como en wslpg). También permite ajustar una liquidación ya emitida (crédito o débito; físico, monetario o financiero; una anulación es un ajuste de crédito total), consultarla por punto de venta, tipo y número de comprobante, y leer 15 tablas de parámetros. Desde la v2.0 (01/02/2019) el mismo WSDL trae un juego aparte de operaciones avícolas (`*Avicola*`).

Manual: "Liquidación Sector Pecuario — Web Service LSP — Manual para el Desarrollador", **versión 2.0.6 del 23/06/2026** (171 págs.), en `https://www.afip.gob.ar/ws/WSLSP/manual_wslsp_2.0.6.pdf`. Las citas usan las secciones del manual y la página impresa del índice.

Es parte de una familia de liquidaciones sectoriales Java con el mismo esqueleto: `wslsp`, `wslum`, `wslca` y `wsltv`, y en parte `wslpg`. Todas tienen `auth` dentro del request, `generarLiquidacion` (que devuelve el CAE), consulta por número de comprobante, último número por punto de venta, ajustes, errores en `respuesta/errores/error` y un `pdf` en base64. Esta ficha no depende de las otras.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Java JAX-WS** (stack Metro/RI). Lo observado en la captura del 2026-10-02: `<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S=...>` sin `Header`, el body es `ns2:DummyResp` con `<respuesta>` y `appserver`, `authserver` y `dbserver` = `OK` en mayúsculas, con `Content-Type: text/xml;charset=utf-8` y `Transfer-Encoding: chunked`. El Fault de ejemplo del manual (§2.4) también es típico de JAX-WS RI: `S:Fault` con `xmlns:ns4="http://www.w3.org/2003/05/soap-envelope"` y un mensaje de Woodstox. No es el stack de wslca/wscpe, que usa `soap:Envelope`, header propio y `Ok` |
| Endpoint de homologación | `https://fwshomo.afip.gov.ar:443/wslsp/LspService` (`soap:address` del WSDL). El manual (§2.2, p. 12) da el mismo host sin `:443` |
| Endpoint de producción | `https://serviciosjava.afip.gob.ar:443/wslsp/LspService` (`soap:address` del WSDL de producción). El manual da el mismo sin `:443` |
| targetNamespace | `http://serviciosjava.afip.gob.ar/wslsp/` (http, no https) |
| service / port / binding / portType | `LspService` / `LspEndPoint` / `wslspSOAP` / `LspPortType` |
| WSDL guardado | `docs/arca/wsdl/wslsp-homologacion.wsdl` (2639 líneas, 125 708 bytes; autocontenido: sin `wsdl:import`, `xsd:import` ni `xsd:include`). El de producción es idéntico salvo el `soap:address` (diff del 2026-10-02). Los 46 `wsdl:message` están todos en uso |
| WSAA service id | **`wslsp`**. El manual (§2.3, p. 13) dice: "Nota: el token debe solicitarse para el servicio wslsp." |
| SOAPAction | targetNamespace + nombre de la operación, por ejemplo `http://serviciosjava.afip.gob.ar/wslsp/generarLiquidacion`. Verificado en las 23 operaciones |
| Versión SOAP | Solo SOAP 1.1 (un único binding `soap:`), estilo document/literal y una sola part `parameters` por mensaje |
| elementFormDefault | No está declarado, así que vale `unqualified`. Solo el elemento raíz del Body lleva namespace (`wsl:GenerarLiquidacionReq`); `auth`, `solicitud` y todos sus hijos van **sin** namespace. La respuesta real lo confirma (`<ns2:DummyResp ...><respuesta>`) |
| Operaciones | **23**. Ninguna declara `wsdl:fault` |
| `dummy` | El mensaje de entrada no tiene parts y el Body va vacío. La respuesta es `DummyResp/respuesta/{appserver,authserver,dbserver}`. Ojo: el esquema del manual (§2.7.1.2) escribe `dummyResp` en minúscula, pero el WSDL y la respuesta real dicen `DummyResp` |

## Autenticación

Va en el hijo `auth` (tipo `Auth`), que es el primer hijo 1..1 del elemento raíz de cada request:

```xml
<auth>
  <token>xsd:string</token>  <!-- 1..1 -->
  <sign>xsd:string</sign>    <!-- 1..1 -->
  <cuit>CUIT</cuit>          <!-- 1..1: xsd:long entre 10000000000 y 99999999999 (11 dígitos) -->
</auth>
```

- Se manda en las **22 operaciones que no son `dummy`** (§2.3: "A excepción del método dummy"). El elemento se llama `cuit`, no `cuitRepresentada`: es la CUIT representada, que emite la liquidación.
- El manual (§4.10) aclara que `token` y `sign` "tienen longitud variable según la respuesta del WSAA".
- **Falla documentada:** ninguna en concreto. El manual no trae un texto ni un código de error para un token inválido o vencido. Las únicas pistas son que los errores "excepcionales" van en `S:Fault` (§2.4) y que la tabla de errores de negocio (§4.9) no tiene códigos de autenticación. **NO VERIFICADO** si una falla de auth vuelve como Fault o en `errores`.
- **Falla observada (homologación, 2026-10-02).** Se probó `ConsultarProvinciasReq` con token y sign "abc", con token y sign vacíos, y sin `auth` (`<ns:ConsultarProvinciasReq/>`). Las tres respuestas fueron iguales: `HTTP/1.0 200 OK`, sin `Content-Type`, con `Connection: Keep-Alive`, `Content-Length: 39` y un body de texto plano, por ejemplo `BL6212898970680 2026-10-02 15:08:26 500`. No es SOAP. Es el mismo fenómeno que se vio en wsmtxca (catalogo.md §4.1) y en todos los servicios de fwshomo. La hipótesis, NO VERIFICADA, es que un WAF/F5 reemplaza cualquier HTTP 500 (o sea, cualquier Fault). Las respuestas buenas traen cookies `f5avr..._session_` y `TS01761d9e`. **El Fault real de autenticación no se pudo ver.**
- Request mínimo válido según el esquema (SOAPAction `"http://serviciosjava.afip.gob.ar/wslsp/consultarUltimoNroComprobantePorPtoVta"`, `Content-Type: text/xml; charset=utf-8`):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsl="http://serviciosjava.afip.gob.ar/wslsp/">
  <soapenv:Header/>
  <soapenv:Body>
    <wsl:ConsultarUltimoNroComprobantePorPtoVtaReq>
      <auth><token>...</token><sign>...</sign><cuit>20111111112</cuit></auth>
      <solicitud><puntoVenta>1</puntoVenta><tipoComprobante>180</tipoComprobante></solicitud>
    </wsl:ConsultarUltimoNroComprobantePorPtoVtaReq>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta real de `dummy` (homologación, 2026-10-02, HTTP 200):

```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:DummyResp xmlns:ns2="http://serviciosjava.afip.gob.ar/wslsp/"><respuesta><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></respuesta></ns2:DummyResp></S:Body></S:Envelope>
```

## Operaciones

Todos los elementos son `<Op>Req` y `<Op>Resp` con la primera letra en mayúscula (por ejemplo `GenerarLiquidacionReq`), y cada respuesta trae un único hijo `respuesta`. Las de parámetros devuelven listas `CodigoDescripcion` (`codigo` y `descripcion`, los dos `xsd:string`) más `errores` y `metadata` opcionales.

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Estado de app, auth y base | Body vacío | `respuesta/{appserver,authserver,dbserver}` | — |
| `consultarProvincias` | Provincias | `auth` | `provincia*` | Consulta tabla de parámetros |
| `consultarLocalidadesPorProvincia` | Localidades de una provincia | `solicitud/codProvincia` (0..99) | `localidad*` | Consulta tabla de parámetros |
| `consultarPuntosVenta` | Puntos de venta habilitados para el WS | `auth` | `puntoVenta*` (código y domicilio) | Consulta tabla de parámetros (por CUIT) |
| `consultarUltimoNroComprobantePorPtoVta` | Último número autorizado | `solicitud/{puntoVenta,tipoComprobante}` | `nroComprobante` (`xsd:long`, 1..1) | **Lee estado**: el máximo número de liquidación o ajuste emitido para CUIT + pto vta + tipo |
| `consultarOperaciones` | Tipos de operación | `auth` | `operacion*` (1-6), `operacionPorcina*` (101-106), `operacionAvicola*` (201-208) | Consulta tabla de parámetros |
| `consultarTiposComprobante` | Tipos de comprobante | `auth` | `tipoComprobante*` | Consulta tabla de parámetros |
| `consultarTiposLiquidacion` | Tipos de liquidación (por cabeza, kilo vivo, etc.) | `auth` | `tipoLiquidacion*` | Consulta tabla de parámetros |
| `consultarCaracteresParticipante` | Caracteres de emisor y receptor | `auth` | `caracter*`, `caracterPorcino*`, `caracterAvicola*` | Consulta tabla de parámetros |
| `consultarCategorias` | Categorías de hacienda | `auth` | `categoria*`, `categoriaPorcina*`, `categoriaAvicola*` | Consulta tabla de parámetros |
| `consultarCategoriasPorMotivo` | Categorías válidas para una especie y un motivo (v2.0.1) | `solicitud/{especie (int), motivo}` | Igual que `consultarCategorias` (reusa `ConsultarCategoriasRespuesta`) | Consulta tabla de parámetros |
| `consultarMotivos` | Motivos de la liquidación | `auth` | `motivo*`, `motivoPorcino*`, `motivoAvicola*` | Consulta tabla de parámetros |
| `consultarRazas` | Razas | `auth` | `raza*`, `razaPorcina*` | Consulta tabla de parámetros |
| `consultarCortes` | Cortes | `auth` | `corte*`, `cortePorcino*` | Consulta tabla de parámetros |
| `consultarGastos` | Tipos de gasto | `auth` | `gasto*` | Consulta tabla de parámetros |
| `consultarTributos` | Tipos de tributo | `auth` | `tributo*`, `tributoPorcino*`, `tributoAvicola*` | Consulta tabla de parámetros |
| `generarLiquidacion` | Liquidación bovina o porcina | `solicitud`: `codOperacion`, `emisor{puntoVenta,tipoComprobante,nroComprobante,codCaracter,fechaInicioActividades,...}`, `receptor{codCaracter,operador{cuit,...}}`, `datosLiquidacion`, `guia*`, `dte*`, `remito*`, `itemDetalleLiquidacion+`, `gasto*`, `tributo*` | `LiquidacionDetalleRespuesta`: `cabecera{codOperacion,cae,fechaVencimientoCae,nroCodigoBarras,fechaProcesoAFIP}`, `emisor`, `receptor`, ítems con `nroItem`, `resumenTotales`, `pdf` | **Crea** una liquidación y devuelve el **CAE**. Se lee con `consultarLiquidacionPorNroComprobante` por pto vta + tipo + nro |
| `consultarLiquidacionPorNroComprobante` | Detalle de una liquidación o ajuste bovino/porcino | `solicitud/{puntoVenta,tipoComprobante,nroComprobante}` | `LiquidacionDetalleRespuesta` (la misma de `generarLiquidacion`, con `pdf`) | **Lee estado** por pto vta + tipo + nro |
| `generarAjuste` | Ajuste de crédito o débito sobre una liquidación bovina/porcina | `solicitud`: `tipoAjuste` (C/D), `fechaComprobante`, `emisor{puntoVenta,nroComprobante,comprobanteAAjustar{tipoComprobante,puntoVenta,nroComprobante}}`, `itemDetalleAjusteLiquidacion*{nroItemAjustar,ajusteFisico|ajusteMonetario,...}`, `ajusteFinanciero{gasto*,tributo*}` | `LiquidacionDetalleRespuesta` con `ajuste{tipoAjuste,modoAjuste,comprobanteAjustado}` | **Crea** un ajuste con su propio CAE, vinculado a la liquidación original por pto vta + tipo + nro (`comprobanteAAjustar`). Se lee con `consultarLiquidacionPorNroComprobante` |
| `consultarConceptosAvicola` | Conceptos de bonificación y penalización avícola | `auth` | `concepto*{codigo,tipo,descripcion}` | Consulta tabla de parámetros |
| `generarLiquidacionAvicola` | Liquidación avícola | `solicitud`: `codOperacion` (201-208), `emisor`, `receptor{codCaracter,tipoDoc(80/96),nroDoc,...}`, `datosLiquidacion{...,granja,condicionVenta+}`, `dte*`, `remito*`, `itemDetalleLiquidacion+`, `resultadoProductivo`, `bonificacionesPenalizaciones*`, `gasto*`, `tributo*` | `LiquidacionAvicolaDetalleRespuesta` (cabecera con CAE, `pdf`) | **Crea** una liquidación avícola con CAE. Se lee con `consultarLiquidacionAvicolaPorNroComp` |
| `consultarLiquidacionAvicolaPorNroComp` | Detalle de una liquidación avícola | `solicitud/{puntoVenta,tipoComprobante,nroComprobante}` (mismo tipo que la bovina) | `LiquidacionAvicolaDetalleRespuesta` | **Lee estado** por pto vta + tipo + nro |
| `generarAjusteAvicola` | Ajuste sobre una liquidación avícola | Como `generarAjuste`, más `bonificacionesPenalizaciones*` | `LiquidacionAvicolaDetalleRespuesta` | **Crea** un ajuste con CAE, vinculado por `comprobanteAAjustar` |

Diferencias de nombres entre el manual y el WSDL (el simulador sigue al **WSDL**):

- El manual llama `ajustarLiquidacion` al método de §2.7.19 (y así figura en el historial v1.1), pero en el WSDL es `generarAjuste`, con elementos `GenerarAjusteReq`/`GenerarAjusteResp`. El ejemplo de anulación (§2.7.19, Respuesta 2) además está envuelto en `GenerarLiquidacionResp`.
- El manual llama `consultarLiquidacionAvicolaPorNroComprobante` (§2.7.22) a lo que en el WSDL es `consultarLiquidacionAvicolaPorNroComp` (`ConsultarLiquidacionAvicolaPorNroCompReq`). La tabla del §4.7 sí usa el nombre corto.
- El esquema de ajuste del manual (§2.7.19.1) pone `cuitCliente`, `codCategoria`, `tipoLiquidacion` y `codRaza` dentro de `itemDetalleAjusteLiquidacion`. En el WSDL `ItemDetalleAjusteSolicitud` solo tiene `nroItemAjustar`, `ajusteFisico`, `ajusteMonetario` y `ajusteCompraAsociada*`.
- El ejemplo de PDF del §2.6 llama a `port.consultarLiquidacionPorCae(...)`, que no existe en este WSDL (es de wslum).

Qué operaciones sirven para cada especie (§4.7): las bovinas y porcinas usan `generarLiquidacion`, `generarAjuste` y `consultarLiquidacionPorNroComprobante`; las avícolas usan solo las `*Avicola*`. `consultarCategoriasPorMotivo` es solo para bovinos. `consultarRazas` y `consultarCortes` no aplican a avícola.

## Errores

**Estructura.** Los errores van **dentro de la respuesta**, con HTTP 200, en `respuesta/errores/error` (tipo `Errores`, con `error` 0..n de tipo `CodigoDescripcion`):

```xml
<respuesta>
  ...
  <errores>
    <error><codigo>1009</codigo><descripcion>N° de comprobante incorrecto para el tipo de comprobante y punto de venta ingresados.</descripcion></error>
  </errores>
  <metadata><servidor>pecuaria-ws-desa</servidor><fechaHora>2016-11-17T12:00:39</fechaHora></metadata>
</respuesta>
```

`codigo` es `xsd:string`. Toda respuesta trae `metadata` (§2.5): `servidor` y `fechaHora` (`xsd:dateTime` sin huso horario, por ejemplo `2016-11-17T12:00:39`, según §4.10). Cuando un error se refiere a un ítem de una lista, el mensaje lleva `#{n° orden}`, por ejemplo "Item #2: cantidadCabezas" (§4.9).

**Tipos de error (§2.4, p. 13-15):**

- **De formato:** son errores de esquema, como un dato de tipo incorrecto o un orden inválido. El manual dice que vienen en `errores` con un código `cvc-*` y da tres ejemplos: `cvc-type.3.1.3` "El valor 'xxxxx' del elemento 'periodo' no es válido.", `cvc-complex-type.2.4.a` "Se encontró contenido inválido en el elemento 'puntoVenta'. Se espera '{periodo}'." y `cvc-datatype-valid.1.2.1` "'e1' no es un valor válido para 'integer'." Documentado, no observado.
- **Internos:** `500` "Error general de aplicación." (operación **Rechazada**) y `550` "Error al generar el archivo pdf." (operación **Aceptada**: la respuesta llega completa pero sin `pdf`, y el error va en `errores`).
- **De negocio:** son las validaciones de §4.9.
- **Excepcionales:** un `S:Fault`. El ejemplo del manual es XML mal formado:

```xml
<S:Fault xmlns:ns4="http://www.w3.org/2003/05/soap-envelope">
  <faultcode>S:Client</faultcode>
  <faultstring>No se puede crear el mensaje SOAP debido a la excepción: error de lectura XML: com.ctc.wstx.exc.WstxEOFException: EOF inesperado; se esperaba un tag de cierre para el elemento &lt;soapenv:Envelope> en [fila,columna {origen-desconocido}]:[2,3]</faultstring>
</S:Fault>
```

  No hay `detail`. El status HTTP no está documentado; en JAX-WS un Fault va con 500, y en fwshomo eso termina en la página `BL... 500` (ver Autenticación).

Algunos errores son "excluyentes", es decir, rechazan la operación. Otros se admiten y la operación sigue (la columna Estado Operación de §4.9 dice R o A).

**Códigos de negocio.** La tabla "Código y descripción de errores / validaciones" está en §4.9, págs. 159-164, y tiene **108 códigos** (500 a 7000). Es demasiado larga para copiarla acá. Los que un cliente se cruza en un flujo normal:

| Código | Descripción | Estado |
|---|---|---|
| 500 | Error general de aplicación. | R |
| 550 | Error al generar el archivo pdf. | A |
| 1000 | No se encontraron resultados según los parámetros de búsqueda informados. | R |
| 1002 | Los siguientes campos son obligatorios para el tipo de operación que intenta realizar: [campo1, campo2, ...] | R |
| 1007 | La CUIT representada no tiene puntos de venta activos para emitir una liquidación. | R |
| 1008 | El punto de venta informado no es válido. | R |
| 1009 | N° de comprobante incorrecto para el tipo de comprobante y punto de venta ingresados. | R |
| 1010 | Error en la solicitud del CAE. | R |
| 2006 | Emisor: El tipo de comprobante no es válido para el tipo de operación que intenta realizar. | R |
| 2200 | Liquidación: La fecha de comprobante debe estar comprendida entre los N días próximos o anteriores a la fecha de proceso (N = 5 en compra-venta; 10 en avícolas de servicio de crianza). | R |
| 2227 | El valor 'conversión' ... debe estar entre {0} y {1}. | **A** (el único de negocio que no rechaza) |
| 3000 | La liquidación que intenta ajustar es inexistente. | R |
| 3001 | La fecha de comprobante de la liquidación que intenta ajustar debe estar dentro de los 180 días previos a la fecha de comprobante del ajuste. | R |
| 3002 | No se pueden realizar ajustes físicos y monetario en un mismo comprobante. | R |
| 3006 | No se determinó ningún tipo de ajuste (físico, monetario o financiero). | R |
| 3007 | No se puede realizar un ajuste sobre otro ajuste. | R |
| 3011 | El ajuste debe ser de la misma especie que la liquidación que intenta ajustar. | R |
| 4000 | El importe neto de la operación no puede ser negativo. | R |
| 5000 | Solo se puede anular un comprobante hasta el día 6 inclusive del mes siguiente al de la liquidación. | R |
| 5001 | No se puede anular un ajuste. | R |
| 5002 | La liquidación ya se encuentra anulada. | R |

Al extraer el texto con `pdftotext -layout`, la tabla de §4.9 sale con las columnas corridas. Para leerla hay que usar la tabla del PDF (con PyMuPDF `find_tables` sale bien). El código 1009 de arriba coincide con lo que dice §1.3.

## Comportamiento a simular

**Numeración (§1.3, p. 6-8).**

- La clave única de una liquidación autorizada es **CUIT + `puntoVenta` + `tipoComprobante` + `nroComprobante`**. El número arranca en 1 y sube de a uno, por separado para cada combinación de CUIT, punto de venta y tipo.
- El cliente elige el número. Si la solicitud se rechaza, el número no se consume y el próximo intento usa el mismo. El próximo número válido es `consultarUltimoNroComprobantePorPtoVta + 1`.
- "En todos los casos", si la combinación de pto vta, tipo y número es incorrecta, el servicio responde el error **1009**.
- Los ajustes comparten la numeración del tipo de comprobante de la liquidación que ajustan. Por eso `EmisorAjusteSolicitud` trae `puntoVenta` y `nroComprobante` pero no `tipoComprobante`. En los ejemplos, el ajuste de la 186-2000-3 sale como 186-3000-**34** y la anulación de una 185 sale como 185-3000-**40** (§2.7.19.3). Esto es **[INFERIDO]**: el manual no lo dice explícitamente.
- Tipos y rangos (WSDL): `PuntoVenta` int 1..99999, `TipoComprobante` short 1..999 y `NroComprobante` int 1..99999999. En `consultarUltimo...` la respuesta `nroComprobante` es `xsd:long` 1..1. Qué devuelve cuando todavía no hay ningún comprobante (¿0?) está **NO VERIFICADO**.
- El punto de venta tiene que estar habilitado en RECE, Codificación de productos, Factura Electrónica Exento o Monotributo, según el caso (§1.3). Los errores son 1007 y 1008.
- Tipos de comprobante (§2.7.7 y §4.1):
  - Bovinos y porcinos: 180 y 182 (cuenta de venta y líquido producto A/B, operaciones 1-3 y 101-103), 183 y 185 (liquidación de compra A/B, ops 4 y 104), 186, 188 y 189 (compra directa A/B/C, ops 5 y 105), 190 y 191 (venta directa A/B, ops 6 y 106).
  - Avícolas: 157-170 (ops 201-208).
  - La v2.0.5 cambió los tipos 164, 165, 169 y 170 según la situación de IVA del receptor.

**Id que asigna ARCA.** Viene en `cabecera/cae` (`xsd:long`). En todos los ejemplos tiene **14 dígitos** (por ejemplo `96465021584954` o `99058758135880`); el WSDL no le pone un rango. La cabecera también trae:

- `fechaVencimientoCae` (`xsd:date`): en los ejemplos cae entre 6 y 36 días después de `fechaProcesoAFIP`. La regla no está documentada.
- `fechaProcesoAFIP` (`xsd:date`).
- `codOperacion`.
- `nroCodigoBarras`: el WSDL lo mantiene, pero **desde la v1.3 no se devuelve** ("Por cuestiones de compatibilidad ya no se retornará valor en el campo <nroCodigoBarras>; la etiqueta no ha sido eliminada", §4.12.3). Los ejemplos viejos del §2.7.17 todavía lo muestran con 44 dígitos. El simulador no debe emitirlo.

Además, el sistema numera los ítems (`nroItem`, de 1 a 10000) en el alta de liquidaciones y ajustes (§4.12.2). Ese número se usa después en `nroItemAjustar` y en `liquidacionCompraAsociada/nroItem`.

**Idempotencia.** No hay. No existe un id de request ni un "reintento seguro". La única clave es pto vta + tipo + nro. Según §1.3 ("en todos los casos ..."), reenviar un número que ya fue autorizado da 1009 **[INFERIDO]**: el manual no tiene un ejemplo de reenvío. La recuperación documentada para un timeout en `generarLiquidacion` (§1.4) es consultar el último número y, si coincide con el enviado, leer la liquidación con `consultarLiquidacionPorNroComprobante`.

**Estados y ajustes (§2.7.19, §4.9).**

- Una liquidación puede quedar **emitida** o **anulada**, y puede tener cero o más ajustes. Cada ajuste es un comprobante con su propio CAE, de tipo `C` (crédito, reduce) o `D` (débito, aumenta).
- El modo del ajuste es Físico (cantidades), Monetario (precios) o Financiero (gastos y tributos). Físico y monetario no se pueden combinar (3002), pero cualquiera de los dos se puede combinar con financiero.
- La respuesta del ajuste informa `ajuste/modoAjuste` como texto: "Monetario" o "Fisico" en los ejemplos.
- Una **anulación** es un ajuste C que combina un ajuste físico de cada ítem por su cantidad total con el financiero de los gastos y tributos originales (§2.7.19.3, Solicitud 2). Reglas:
  - Solo se puede anular hasta el **día 6 inclusive del mes siguiente** (5000).
  - No se puede anular un ajuste (5001), anular dos veces (5002) ni ajustar un ajuste (3007).
  - El ajuste tiene que caer dentro de los **180 días** de la liquidación (3001) y ser de la misma especie (3011).
- **Saldos (§4.5):** las liquidaciones de compra tienen una cantidad disponible por ítem. Las cuentas de venta y líquido producto de hacienda (op. 1/101) la consumen con `liquidacionCompraAsociada{tipoComprobante,puntoVenta,nroComprobante,nroItem,cantidadAsociada}`, y los ajustes la devuelven. Los errores son 2500-2511 y 3004. Un simulador mínimo puede llevar un saldo por ítem o ignorar la validación.

**Plazos.**

- `fechaComprobante` dentro de ±5 días de la fecha de proceso, o ±10 en avícola de crianza (2200).
- `fechaOperacion` ≤ `fechaComprobante` (2201).
- Ajuste dentro de los 180 días (3001).
- Anulación hasta el día 6 del mes siguiente (5000).

**PDF (§2.6).** `generarLiquidacion`, `generarAjuste`, las dos consultas por número y las equivalentes avícolas devuelven `pdf` (`xsd:base64Binary`): es el mismo PDF que imprime la aplicación web. En los ejemplos empieza con `JVBERi0xLjQK` (`%PDF-1.4`). No hay un flag para pedirlo o no; siempre viene, salvo con el error 550.

**Homologación (§1.5).**

- Las validaciones del rol emisor **no se hacen**. En la respuesta, `emisor/situacionIVA` vale siempre `RI` para los comprobantes A y B, y `MO` para los C.
- Hay CUIT genéricas que solo sirven como receptores: tabla 1 en §1.5.1, por ejemplo 20160000024 (productor/criador, IVA) o 20160000105 (feed lot). También hay CUIT para autorizados, números de RUCA, plantas frigoríficas y sujetos no categorizados.
- El N° de DTE de ejemplo que vale en homologación es `123456789-2` (§4.12.5).

**Formatos (§4.10).**

- Fecha: `aaaa-MM-dd` sin huso horario.
- Fecha y hora: `aaaa-MM-ddThh:mm:ss`.
- Redondeo: Round Half Even a 2 decimales.
- Importes: `Importe` es `decimal(3)` de 0.001 a 9999999999999.998. `BigDecimal` (en las respuestas) es `decimal(2)`.
- Patrones: `NroRenspa` `(0[0-9]|1[0-9]|2[0-3])[.]\d{3}[.]\d{1}.\d{5}[/]\w{2}`, `NroDTE` `\d{1,9}-\d`, `NroRemito` `\d{5}-\d{8}`.

**Paginación:** no hay.

## No verificado

- El formato real del error de autenticación (token inválido, vencido o vacío): si es Fault o `errores`, con qué texto y qué status HTTP. En fwshomo cualquier error llega como la página `BL<n> <fecha> 500` (capturas del 2026-10-02), y el manual no lo documenta.
- El origen de la página `BL... 500` (¿un WAF/F5?).
- Qué devuelve `consultarUltimoNroComprobantePorPtoVta` cuando todavía no se emitió ningún comprobante.
- Qué error da reenviar un número ya autorizado. Se infiere 1009, sin ejemplo.
- Que los ajustes usen la secuencia del tipo de comprobante original (inferido de los ejemplos).
- La regla de `fechaVencimientoCae` y el algoritmo del CAE de 14 dígitos.
- Si los errores de formato (`cvc-*`) llegan de verdad en `errores` con HTTP 200, como dice §2.4, o como Fault.
- Si producción sigue respondiendo con `S:Envelope`/`OK` igual que homologación (solo se capturó homologación).
