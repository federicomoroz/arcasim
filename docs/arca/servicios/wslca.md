# wslca

Liquidación Electrónica de Compra de Caña de Azúcar (`LcaService`). Con este servicio, el ingenio o comprador de caña (la CUIT representada, que tiene que ser responsable inscripto en IVA) emite la liquidación de compra al productor cañero y obtiene un **CAE**. La liquidación referencia los remitos de caña (`nroRemito` + kilos). El servicio también permite generar ajustes de precio a favor del emisor o del comprador, y ajustes físicos (que anulan la liquidación); consultar una liquidación por punto de venta, tipo y número; pedir el último número por punto de venta; y leer siete tablas de parámetros.

Manual: "Liquidación Electrónica de Compra de Caña de Azúcar — Web Service LcaService — Manual para el Desarrollador". La portada dice **"Versión 1.0.1 — 19/07/201"** (el año está cortado en el PDF), pero el historial solo registra la "Versión 1.1 (19/07/2019)", que agregó los datos de prueba. Son 62 págs., en `https://www.afip.gob.ar/ws/WSLCA/manual_wslca.pdf`. Las citas usan las secciones del manual y la página impresa del índice.

Es parte de una familia de liquidaciones sectoriales Java con el mismo esqueleto: `wslsp`, `wslum`, `wslca` y `wsltv`. Todas tienen `auth` dentro del request, `generarLiquidacion` (que devuelve el CAE), consulta por número de comprobante, último número por punto de venta, ajustes, errores en `respuesta/errores/error` y un `pdf` en base64. **wslca corre en otro stack que las demás**: es el mismo que wscpe. Tiene targetNamespace **https**, `auth/cuitRepresentada` en lugar de `auth/cuit`, `soap:Header` propio en las respuestas y Faults tipados (`Exception`). Esta ficha no depende de las otras.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **CXF-like**, el mismo stack que wscpe. Lo observado en la captura del 2026-10-02: status line `HTTP/1.1 200 200` (sí, la reason phrase es "200"), `Content-Type: text/xml;charset=UTF-8`, sin declaración XML. El sobre es `soap:Envelope` con un `soap:Header` que trae `serverTime` (`02/10/2026 15:08:28 GMT-03:00`), `serverName` (`fiscaws-homoext01`) y `elapsedTime` (`1790964508503ms`, un valor absurdo, del orden de una época en ms), todos sin namespace y sin `id` (wscpe sí lo trae). El body es `ns2:DummyResp` con `appserver`, `authserver` y `dbserver` = **`Ok`** (no `OK`). El Fault de ejemplo del manual dice "Unmarshalling Error: cvc-...", que es la redacción típica de Apache CXF y JAXB |
| Endpoint de homologación | `https://fwshomo.afip.gov.ar/wslca/services/soap` (`soap:address` del WSDL, sin puerto). El manual (§2.2, p. 7) da `https://fwshomo.afip.gov.ar/wslca/services/` para la conexión (parece cortado; el WSDL dice `.../services/soap`) y `.../services/soap?wsdl` para ver el WSDL |
| Endpoint de producción | `https://serviciosjava.afip.gob.ar/wslca/services/soap` (`soap:address` del WSDL de producción). El manual da lo mismo, con el mismo corte en la URL de conexión |
| targetNamespace | `https://serviciosjava.afip.gob.ar/wslca/` (**https**). **El manual usa `http://serviciosjava.afip.gob.ar/wslca/` en casi todos los ejemplos** (45 veces, incluido el de autenticación del §2.3) y `https` solo en 4: el esquema de `generarLiquidacion` y el Fault. La respuesta real usa `https`. Un cliente armado con los ejemplos del manual manda el namespace equivocado. El simulador sigue al WSDL |
| service / port / binding / portType | `LcaService` / `LcaEndPoint` / `wslcaSOAP` / `LcaPortType` |
| WSDL guardado | `docs/arca/wsdl/wslca-homologacion.wsdl` (2026 líneas, 65 331 bytes; autocontenido: sin `wsdl:import`, `xsd:import` ni `xsd:include`). El de producción es idéntico salvo el `soap:address` (diff del 2026-10-02). Los 29 `wsdl:message` están todos en uso |
| WSAA service id | **`wslca`**. El manual (§2.3, p. 7) dice: "Nota: el token debe solicitarse para el servicio wslca." |
| SOAPAction | targetNamespace + nombre de la operación, por ejemplo `https://serviciosjava.afip.gob.ar/wslca/generarLiquidacion` (con https). Verificado en las 14 operaciones |
| Versión SOAP | Solo SOAP 1.1 (un único binding `soap:`), estilo document/literal y una sola part `parameters` por mensaje |
| elementFormDefault | No está declarado, así que vale `unqualified`. Solo el elemento raíz del Body lleva namespace; `auth`, `solicitud` y todos sus hijos van **sin** namespace (confirmado en la respuesta real) |
| Operaciones | **14**. Las 14, incluida `dummy`, declaran `wsdl:fault name="Exception"`, con el elemento `tns:Exception` de tipo `ExceptionType` |
| `dummy` | El mensaje de entrada no tiene parts y el Body va vacío. La respuesta es `DummyResp/respuesta/{appserver,authserver,dbserver}`. El ejemplo del manual (§2.7.1) muestra el estilo JAX-WS (`S:Envelope`, sin header, `OK`), pero **el servidor real responde distinto** (ver la fila Dialecto). El simulador sigue a la captura |

## Autenticación

Va en el hijo `auth` (tipo `Auth`), que es el primer hijo 1..1 del elemento raíz de cada request:

```xml
<auth>
  <token>xsd:string</token>               <!-- 1..1 -->
  <sign>xsd:string</sign>                 <!-- 1..1 -->
  <cuitRepresentada>CUIT</cuitRepresentada> <!-- 1..1: xsd:long entre 10000000000 y 99999999999 (11 dígitos) -->
</auth>
```

- Se manda en las **13 operaciones que no son `dummy`** (§2.3). Ojo: es `cuitRepresentada`, no `cuit` como en wslsp, wslum y wsltv.
- `token` y `sign` "tienen longitud variable según la respuesta del WSAA" (§4.3).
- **Falla documentada:** ninguna en concreto. El manual no da un texto para token inválido o vencido, y la tabla de §4.2 no tiene códigos de autenticación. Por el stack, lo más probable es un Fault como el de wscpe, que en `cpea-ws-qaext` (2026-10-02) devolvió HTTP 500, `faultcode` `soap:Client` y `faultstring` "Error en la autenticacion: Token invalido" (token "abc"), "Error en la autenticacion: Token o Sign nulos" (vacíos) o "Error en la autenticacion: El sign no se conrresponde con el token" (firma falsa), sin `detail`. Para wslca eso es **[INFERIDO]**, NO VERIFICADO.
- **Falla observada (homologación, 2026-10-02).** Se probó `ConsultarProvinciasReq` con token y sign "abc", con token y sign vacíos, y sin `auth`. Las tres respuestas fueron iguales: `HTTP/1.0 200 OK`, sin `Content-Type`, con `Connection: Keep-Alive`, `Content-Length: 39` y un body de texto plano, por ejemplo `BL5323257360292 2026-10-02 15:08:28 500`. No es SOAP. Es lo mismo que pasa en todos los servicios de fwshomo. La hipótesis, NO VERIFICADA, es que un WAF/F5 reemplaza cualquier HTTP 500. Las respuestas buenas traen las cookies `f5avr..._session_` y `TS01761d9e`. **El Fault real no se pudo ver.**
- Request mínimo válido según el esquema (SOAPAction `"https://serviciosjava.afip.gob.ar/wslca/consultarUltimoNroComprobantePorPtoVta"`, `Content-Type: text/xml; charset=utf-8`):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsl="https://serviciosjava.afip.gob.ar/wslca/">
  <soapenv:Header/>
  <soapenv:Body>
    <wsl:ConsultarUltimoNroComprobantePorPtoVtaReq>
      <auth><token>...</token><sign>...</sign><cuitRepresentada>20190000207</cuitRepresentada></auth>
      <solicitud><puntoVenta>1</puntoVenta><tipoComprobante>171</tipoComprobante></solicitud>
    </wsl:ConsultarUltimoNroComprobantePorPtoVtaReq>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta real de `dummy` (homologación, 2026-10-02, `HTTP/1.1 200 200`):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Header><serverTime>02/10/2026 15:08:28 GMT-03:00</serverTime><serverName>fiscaws-homoext01</serverName><elapsedTime>1790964508503ms</elapsedTime></soap:Header><soap:Body><ns2:DummyResp xmlns:ns2="https://serviciosjava.afip.gob.ar/wslca/"><respuesta><appserver>Ok</appserver><authserver>Ok</authserver><dbserver>Ok</dbserver></respuesta></ns2:DummyResp></soap:Body></soap:Envelope>
```

## Operaciones

Todos los elementos son `<Op>Req` y `<Op>Resp` con la primera letra en mayúscula, y cada respuesta trae un único hijo `respuesta`. Las de parámetros devuelven listas `CodigoDescripcion` más `errores` y `metadata` opcionales. Las cuatro operaciones que generan o leen liquidaciones devuelven `LiquidacionDetalleRespuesta`.

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Estado de app, auth y base | Body vacío | `respuesta/{appserver,authserver,dbserver}` = `Ok` | — |
| `consultarProvincias` | Provincias | `auth` | `provincia*` | Consulta tabla de parámetros |
| `consultarLocalidadesPorProvincia` | Localidades de una provincia | `solicitud/codProvincia` (int) | `localidad*` | Consulta tabla de parámetros |
| `consultarPuntosVenta` | Puntos de venta habilitados | `auth` | `puntoVenta*` | Consulta tabla de parámetros (por CUIT) |
| `consultarUltimoNroComprobantePorPtoVta` | Último número autorizado | `solicitud/{puntoVenta,tipoComprobante}` | `nroComprobante` (`xsd:long`, 1..1) | **Lee estado**: el último número autorizado para CUIT + pto vta + tipo (§2.7.5) |
| `consultarTiposComprobante` | Tipos de comprobante | `auth` | `tipoComprobante*` (171 = Clase A, 172 = Clase B, §2.7.6.3) | Consulta tabla de parámetros |
| `consultarCondicionesVenta` | Condiciones de venta | `auth` | `condicionVenta*` | Consulta tabla de parámetros |
| `consultarMediosPago` | Medios de pago | `auth` | `medioPago*` | Consulta tabla de parámetros |
| `consultarOtrosConceptos` | Otros conceptos (con IVA) | `auth` | `concepto*` | Consulta tabla de parámetros |
| `consultarTributos` | Tributos | `auth` | `tributo*` | Consulta tabla de parámetros |
| `generarLiquidacion` | Liquidación de compra de caña | `solicitud`: `emisor{comprobante{puntoVenta,tipoComprobante,nroComprobante},fechaInicioActividades,iibb?,leyenda?}`, `receptor{cuit,localidad,provincia,iibb?}`, `datosGenerales{fechaComprobante,condicionVenta+,medioPago+}`, `remito+{nroRemito,kilos}`, `detalle+{producto,cantidad,unidadMedida,precioUnitario,alicuotaIVA}`, `otroConcepto*`, `tributo*` | `autorizacion{cae,fechaVencimientoCae,fechaProcesoAFIP}`, `emisor`, `receptor`, `datosGenerales`, `remito*`, `detalle*` (con `nroOrden`), `otroConcepto*`, `tributo*`, `resumenTotales`, `pdf`, `errores`, `metadata` | **Crea** una liquidación y devuelve el **CAE**. Se lee con `consultarLiquidacionPorNroComprobante` por pto vta + tipo + nro (`Comprobante`) |
| `consultarLiquidacionPorNroComprobante` | Detalle de una liquidación o ajuste | `solicitud/comprobante{puntoVenta,tipoComprobante,nroComprobante}` | `LiquidacionDetalleRespuesta` | **Lee estado** por pto vta + tipo + nro |
| `generarAjustePrecio` | Ajuste de precio sobre ítems de una o más liquidaciones | `solicitud`: `emisor{comprobante{pto,tipo,nro},tipoAjuste}`, `datosGenerales`, `detalle+{comprobanteAjustado{pto,tipo,nro},nroOrdenItemAjustado,diferenciaPrecio}`, `otroConcepto*`, `tributo*` | `LiquidacionDetalleRespuesta` con `ajuste{tipoAjuste,...}` y `detalle/detalleAjuste{comprobanteAjustado,nroOrdenAjustado}` | **Crea** un ajuste con su propio CAE y número. Se vincula a la original por `comprobanteAjustado` (pto + tipo + nro) + `nroOrden` del ítem. Se lee con `consultarLiquidacionPorNroComprobante` |
| `generarAjusteFisico` | Ajuste físico, que **anula** la liquidación (§2.7.14) | `solicitud`: `emisor{comprobante{pto,tipo,nro},comprobanteAjustado{pto,tipo,nro}}`, `fechaComprobante`, `devolucionMercaderia` (boolean) | `LiquidacionDetalleRespuesta` con `ajuste{tipoAjuste=1,esDevolucionMercaderia}` | **Crea** un ajuste con CAE que deja anulada la original (error 1604 si ya estaba anulada). Se lee con `consultarLiquidacionPorNroComprobante` |

Los esquemas de respuesta de §2.7.13.2 y §2.7.14.2 dicen que estas operaciones devuelven `ConsultarLiquidacionPorNroComprobanteResp`. Es un error de copiar y pegar: en el WSDL son `GenerarAjustePrecioResp` y `GenerarAjusteFisicoResp`, y los ejemplos del mismo manual también.

## Errores

**Estructura de negocio.** Los errores de negocio van **dentro de la respuesta**, con HTTP 200, en `respuesta/errores/error` (`Errores`, con `error` 0..n de tipo `CodigoDescripcion`, `codigo` y `descripcion` `xsd:string`). Toda respuesta trae además `metadata{servidor,fechaHora}` (§2.5). En los ejemplos de este manual `fechaHora` lleva milisegundos y offset (`2019-05-06T14:46:02.765-03:00`), y `servidor` vale `servername`. Cuando un error se refiere a un ítem de una lista, el mensaje lleva `#[n° orden]` (§4.2). El ejemplo del ajuste físico trae un `<errores/>` vacío cuando no hay errores.

**Excepcionales y de formato: Fault tipado (§2.4, p. 8-9).** Es la diferencia principal con el resto de la familia. El ejemplo del manual:

```xml
<soap:Fault>
  <faultcode>soap:Client</faultcode>
  <faultstring>Fallo en la validacion del esquema XML</faultstring>
  <detail>
    <ns2:ExceptionType xmlns:ns2="https://serviciosjava.afip.gob.ar/wslca/">
      <uuid>0x3319bc60910b48309b1fb91618e7bb10</uuid>
      <businessErrorId>07</businessErrorId>
      <exceptionDetails>Unmarshalling Error: cvc-minInclusive-valid: Value '456465' is not facet-valid with respect to minInclusive '10000000000' for type 'CUIT'.</exceptionDetails>
      <serverName>fg-kubuntu</serverName>
    </ns2:ExceptionType>
  </detail>
</soap:Fault>
```

Manual y WSDL difieren en dos puntos, y el simulador sigue al WSDL:

1. El WSDL declara el elemento de detalle como **`tns:Exception`** (de tipo `ExceptionType`). El ejemplo usa `ns2:ExceptionType` como nombre de elemento.
2. `ExceptionType` exige los cinco campos 1..1, en este orden: `uuid` (string), **`timestamp` (`xsd:dateTime`)**, `businessErrorId` (string), `exceptionDetails` (string) y `serverName` (string). El ejemplo del manual no trae `timestamp`.

En wscpe, que corre en el mismo stack, los Faults de autenticación y de esquema que se observaron (2026-10-02) vinieron **sin `detail`**, con `faultstring` "Error en la validacion del esquema XML: [Unmarshalling Error: ...]" y HTTP 500. Para wslca no se pudo observar ningún Fault porque fwshomo los reemplaza por la página `BL... 500`.

**Internos (§2.4).** `500` "Error general de aplicación." (Rechazada) y `550` "Error al generar el archivo pdf." (Aceptada: la respuesta llega sin `pdf` y con el error en `errores`).

**Códigos de negocio (§4.2, "Tabla 5", págs. 58-62): 65 filas.** Al extraer el texto con `pdftotext -layout` las columnas salen corridas. La tabla se reconstruyó con la posición de las palabras en el PDF (PyMuPDF) y se controló con el código 1500 del §1.3. El código **1453 aparece dos veces en el manual** con descripciones distintas. Además, el ejemplo de §4.2 usa `1002` para "Ítem #[n° orden]: El producto informado es inexistente.", pero en la tabla ese texto es el **1307** y el 1002 es otra cosa. Todos son **R** salvo el 550.

| Código | Descripción |
|---|---|
| 500 | Error general de aplicación. |
| 550 | Error al generar el archivo pdf. (A) |
| 800 | No se encontraron resultados según los parámetros de búsqueda informados. |
| 801 | Los siguientes campos son obligatorios para el tipo de operación que intenta realizar: [campo1, campo2, …]. |
| 802 | Los siguientes campos deben ser nulos para el carácter indicado: [campo1, campo2, …]. |
| 803 | La localidad [codigo_localidad] informada es inexistente. |
| 804 | La provincia [codigo_provincia] informada es inexistente. |
| 805 | La localidad [codigo_localidad] no pertenece a la provincia [codigo_provincia]. |
| 900 | La CUIT [número de cuit] se encuentra INACTIVA. |
| 901 | La CUIT [número de cuit] se encuentra limitada por inclusión en Base de Contribuyentes No Confiables. Regularizar la situación en la dependencia AFIP correspondiente. |
| 902 | La CUIT [número de cuit] se encuentra limitada por falta de inscripción en Impuestos y/o Regímenes. |
| 903 | La CUIT [número de cuit] se encuentra inactiva a partir de información de RENAPER / OFICIO JUDICIAL. |
| 904 | La CUIT [número de cuit] pertenece a una Cooperativa Efectora inactiva por requerimiento del Ministerio de Desarrollo Social. |
| 905 | La CUIT [número de cuit] inactiva pertenece a una Sociedad en Formación con incumplimiento. |
| 906 | La CUIT [número de cuit] inactiva pertenece a una Cooperativa inactiva por INAES. |
| 907 | La CUIT [número de cuit] es inexistente. |
| 908 | Sr. Contribuyente, deberá regularizar la situación del domicilio fiscal en su dependencia. |
| 909 | La CUIT [número de cuit] NO registra domicilio fiscal electrónico. |
| 910 | No posee puntos de venta habilitados para Comprobantes en Línea. |
| 911 | No tiene declarado ante AFIP alguna actividad relacionada para la emisión de la liquidación electrónica. |
| 1000 | La fecha de inicio de actividades del no puede ser posterior a la fecha actual. |
| 1001 | El punto de venta informado es inválido. |
| 1002 | El emisor no corresponde a un contribuyente inscripto en el Impuesto al Valor Agregado. |
| 1100 | Emisor y receptor no pueden ser iguales. |
| 1101 | El domicilio fiscal del receptor no es válido. |
| 1200 | La fecha de liquidación debe ser cinco días anteriores o posteriores a la fecha de generación del comprobante. |
| 1201 | La fecha de liquidación debe ser mayor o igual a la fecha de la última liquidación autorizada para el mismo tipo de comprobante. |
| 1202 | Debe informar al menos una condición de venta. |
| 1203 | El campo detalle se informa si y solo si cuando la condición de venta es del tipo 'Otra'. |
| 1204 | No puede informar condiciones de venta repetidas en una misma liquidación. |
| 1205 | La condición de venta informada es inexistente: [código condición de venta]. |
| 1206 | El tipo de comprobante informado es inexistente: [código tipo de comprobante]. |
| 1207 | Tipo de comprobante inexistente o incorrecto según la situación frente al IVA de los actores. |
| 1208 | Debe informar al menos un medio de pago. |
| 1209 | El campo detalle se informa si y solo si el medio de pago es del tipo 'Otro'. |
| 1210 | No puede informar medios de pago repetidos en una misma liquidación. |
| 1211 | El medio de pago informado es inexistente: [código medio de pago]. |
| 1212 | Si la fecha de comprobante es mayor a la fecha actual, ambas deben pertenecer al mismo mes calendario. |
| 1301 | El remito informado es inexistente: {n° de remito}. |
| 1302 | No puede informar remitos repetidos en una misma liquidación. |
| 1303 | Remito #[n° remito]: El remito que desea agregar ya se encuentra liquidado. |
| 1304 | La cantidad de kilos informada en los remitos debe ser igual a la cantidad de kilos en el detalle de la liquidación. |
| 1305 | Debe informar un ítem en el detalle de la liquidación. |
| 1306 | Sólo debe informar un sólo ítem en el detalle para el tipo de liquidación que intenta realizar. |
| 1307 | Item #[n° orden]: El producto informado es inexistente. |
| 1308 | Item #[n° orden]: La unidad de medida informada es inexistente. |
| 1309 | Item #[n° orden]: La alícuota de IVA informada no es válida para la operación que intenta realizar. |
| 1400 | Concepto #[n° orden]: El concepto informado es inexistente. |
| 1401 | Concepto #[n° orden]: El campo detalle se informa si y solo si el concepto es de tipo 'Otros'. |
| 1402 | Concepto #[n° orden]: Los campos 'base imponible' y 'alícuota' son mutuamente excluyente con el campo 'importe'. |
| 1403 | No puede informar conceptos repetidos en una misma liquidación. |
| 1404 | Concepto #[n° orden]: La alícuota de IVA informada no es válida para la operación que intenta realizar. |
| 1405 | Concepto #[n° orden]: La obligatoriedad del campo 'alícuota IVA' no se corresponde según el tipo de comprobante que intenta generar. |
| 1450 | Tributo #[n° orden]: El tributo ingresado es inexistente. |
| 1451 | Tributo #[n° orden]: El campo detalle se informa si y solo si el tributo el tributo es de tipo 'Otro'. |
| 1452 | Tributo #[n° orden]: Los campos 'base imponible' y 'alícuota' son mutuamente excluyentes con el campo 'importe'. |
| 1453 | No puede informar tributos repetidos en una misma liquidación. |
| 1453 (sic, repetido) | Tributo #[n° orden]: Los tributos automáticos no deben informarse por el usuario. |
| 1500 | N° de comprobante incorrecto para el tipo de comprobante y punto de venta ingresados. |
| 1501 | Se produjo un error en la solicitud del CAE. |
| 1600 | El tipo de comprobante informado para el ajuste debe ser igual al tipo de la liquidación que intenta ajustar. |
| 1601 | La liquidación que intenta ajustar es inexistente: [n° de comprobante]. |
| 1602 | El ítem [n° orden ítem] de la liquidación [n° comprobante] que intenta ajustar es inexistente. |
| 1603 | El tipo de ajuste informado es inexistente: [código tipo de ajuste]. |
| 1604 | La liquidación que intenta ajustar se encuentra anulada: [n° de comprobante]. |

## Comportamiento a simular

**Numeración (§1.3, p. 4-5).**

- Un CAE autorizado se identifica por **CUIT + `puntoVenta` + `tipoComprobante` + `nroComprobante`**, que en el esquema forman el tipo `Comprobante`. El número arranca en 1 y sube de a uno, por separado para cada CUIT, punto de venta y tipo.
- Si la solicitud se rechaza, el número no se consume. El próximo número válido es `consultarUltimoNroComprobantePorPtoVta + 1`. Si la combinación es incorrecta, el servicio responde el error **1500**.
- **Los ajustes comparten la numeración y el tipo de la liquidación original.** El error 1600 exige que el tipo del ajuste sea igual al de la liquidación ajustada. En los ejemplos del manual, sobre el pto vta 3000 y el tipo 171, la liquidación es el nro 7, el ajuste de precio el nro 8 y el ajuste físico el nro 9.
- Rangos (WSDL): `PuntoVenta` int 1..99999, `NroComprobante` int 1..99999999 y `tipoComprobante` `xsd:int` sin restricción. Los valores son 171 (Clase A) y 172 (Clase B), según §2.7.6.3.
- La respuesta de `consultarUltimo...` tipa `nroComprobante` como `xsd:long` 1..1. Qué devuelve sin comprobantes previos está **NO VERIFICADO**.
- El punto de venta tiene que estar asociado a RECE para aplicativo y Web Services, o a Codificación de productos – Web Services. Solo se aceptan **responsables inscriptos en IVA** (§1.3; errores 1001 y 1002).

**Id que asigna ARCA.** Viene en `autorizacion/cae` (`xsd:long`, sin restricción), junto con `fechaVencimientoCae` y `fechaProcesoAFIP` (`xsd:date`). **En los ejemplos del manual el CAE vale `7`, `8` y `9`**, que son los mismos números de comprobante, así que parecen datos de un ambiente de desarrollo. El formato real no está documentado. Por coherencia con el resto de la familia, donde el CAE tiene 14 dígitos, conviene emitir 14 dígitos **[INFERIDO]**.

En los ejemplos, las fechas de la respuesta vienen **con offset**: `2019-05-06-03:00`, `1983-11-08-03:00`. Es la serialización de `xsd:date` con zona horaria que usa este stack, aunque §4.3 diga "aaaa-MM-dd sin huso horario" (eso vale para la entrada). Los ítems de la respuesta llevan `nroOrden` (1, 2, ...), que es lo que después pide `nroOrdenItemAjustado` en un ajuste de precio.

**Idempotencia.** No hay. La única clave es pto vta + tipo + nro. Reenviar un número ya autorizado da 1500 **[INFERIDO]**: el manual no tiene un ejemplo de reenvío. La recuperación documentada para un timeout (§1.4) es consultar el último número y leer la liquidación.

**Remitos.**

- Cada liquidación lista `remito+` con `nroRemito` (patrón `\d{5}-\d{8}`, por ejemplo `00007-00979871`) y `kilos`.
- El servidor verifica que el remito exista (1301), que no esté repetido (1302) y que no se haya liquidado antes (1303). También verifica que la suma de kilos de los remitos coincida con los kilos del detalle (1304).
- El manual no dice de dónde salen los remitos (¿wsremazucar?): **NO VERIFICADO**.
- Un simulador de contrato puede aceptar cualquier remito con formato válido y marcarlo como liquidado para devolver 1303 si se repite.

**Estados y ajustes.**

- Una liquidación queda **vigente** o **anulada**.
- `generarAjusteFisico` anula la liquidación entera: "Los ajustes físicos resultan en la anulación del comprobante / liquidación a ajustar" (§2.7.14). Tiene un flag `devolucionMercaderia`. No se puede volver a anular (1604).
- `generarAjustePrecio` corrige el precio de uno o más ítems, identificados por `comprobanteAjustado` + `nroOrdenItemAjustado` con su `diferenciaPrecio` (`Importe`, > 0). El sentido lo da `emisor/tipoAjuste`: **2** = ajuste de precio a favor del emisor y **3** = a favor del comprador (§3.2, `AjusteRespuesta`). En la respuesta, `ajuste/tipoAjuste` vale **1** para el ajuste físico.
- En el WSDL `TipoAjuste` es int 1..99999. El error 1603 es para un tipo de ajuste inexistente, y el 1601/1602 para una liquidación o un ítem inexistente.
- El manual no documenta límites de tiempo para ajustar.

**Plazos.**

- `fechaComprobante` a ±5 días de la fecha de generación (1200).
- No puede ser anterior a la última liquidación autorizada del mismo tipo (1201).
- Si es futura, tiene que caer en el mismo mes calendario (1212).

**Cálculos (§4.1, p. 57-58).** Por ítem: `importeSubtotal` = cantidad × precio e `importeIVA` = subtotal × alícuota / 100 (solo en tipo A). En los totales: neto gravado = subtotal + otros conceptos, subtotal general = neto gravado + IVA, total = subtotal general + tributos. El redondeo es Round Half Even a 2 decimales (§4.3). `AlicuotaIVA` es una enumeración: 10.5 o 21.

**PDF (§2.6).** `pdf` (`xsd:base64Binary`) viene en las respuestas de generación, de ajuste y de consulta. No hay un flag para pedirlo. Con el error 550 la operación se acepta y la respuesta llega sin `pdf`.

**Datos de prueba (§1.5, p. 6).**

- Responsables inscriptos: 20190000207, 20190000215, 30190000091 y 30190000105. Las CUIT 33190000139 y 23190000249 son RI que "incumplen RG 4132".
- Monotributistas: 20190000223 y 30190000113.
- Exentos: 20190000231 y 30190000121.

**Paginación:** no hay.

## No verificado

- El formato real de los Faults de wslca: si el detalle se llama `Exception` (como dice el WSDL) o `ExceptionType` (como el ejemplo), si trae `timestamp`, y si los Faults de autenticación traen `detail` (en wscpe no lo traen). Tampoco se conocen los textos de autenticación. En fwshomo todo Fault llega como `BL<n> <fecha> 500` (2026-10-02).
- Qué devuelve el servidor real ante el namespace `http://` que usan los ejemplos del manual. Lo esperable en este stack es un Fault de esquema o de operación.
- El formato y largo real del CAE. Los ejemplos tienen 1 dígito.
- Qué devuelve `consultarUltimoNroComprobantePorPtoVta` sin comprobantes previos.
- Qué error da reenviar un número ya autorizado (se supone 1500).
- El origen de los remitos que valida el servicio (1301, 1303).
- Si las fechas de las respuestas reales llevan offset (`-03:00`), como muestran los ejemplos.
- Si `soap:Header/serverTime|serverName|elapsedTime` aparece en todas las respuestas o solo en `dummy`. Solo se capturó `dummy`.
