# wsltv

Liquidación de Tabaco Verde (`LtvService`). Con este servicio, el acopiador de tabaco (la CUIT representada, inscripta en el registro de acopiadores) liquida el tabaco que compra a los productores y obtiene un **CAE**. La liquidación detalla los romaneos y sus fardos, con código de trazabilidad, clase y peso, y el precio por clase. El servicio también permite:

- Ajustar una o varias liquidaciones anteriores con un crédito o débito (`ajustarLiquidacion`), con una consulta auxiliar que suma kilos y fardos por clase de los comprobantes a ajustar.
- Hacer un ajuste físico sobre un único comprobante (`generarAjusteFisico`).
- Consultar una liquidación por CAE o por punto de venta, tipo y número, y pedir el último número por punto de venta.
- Leer siete tablas de parámetros.

Manual: "Liquidación de Tabaco Verde — Web Service LtvService — Manual para el Desarrollador", **versión 1.4 del 11/01/2019** (72 págs.), en `https://www.afip.gob.ar/ws/tabaco/manual-wsltv-1.4.pdf`. El PDF ya trae URLs, namespaces y mails reescritos a "arca" (ver Contrato). Las citas usan las secciones del manual y la página impresa del índice.

Es parte de una familia de liquidaciones sectoriales Java con el mismo esqueleto: `wslsp`, `wslum`, `wslca` y `wsltv`. Todas tienen `auth` dentro del request, `generarLiquidacion` (que devuelve el CAE), consulta por número de comprobante, último número por punto de venta, ajustes, errores en `respuesta/errores/error` y un `pdf` en base64. wsltv se parece mucho a wslum: no tiene `metadata`, se puede consultar por CAE y el PDF se pide con un flag. Además, varios nombres de operación no siguen el patrón de la familia (`consultarUltimoComprobanteXPuntoVenta`, `consultarPuntosVentas`, `...XCAE`). Esta ficha no depende de las otras.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Java JAX-WS** (stack Metro/RI). Lo observado en la captura del 2026-10-02: `<?xml version='1.0' encoding='UTF-8'?><S:Envelope ...>` sin `Header`, el body es `ns2:DummyResp` con `<respuesta>` y `appserver`, `authserver` y `dbserver` = `OK` en mayúsculas, con `Content-Type: text/xml;charset=utf-8` y `Transfer-Encoding: chunked`. El Fault de ejemplo del manual (§2.4) es de JAX-WS RI y Woodstox |
| Endpoint de homologación | `https://fwshomo.afip.gov.ar:443/wsltv/LtvService` (`soap:address` del WSDL). **El manual (§2.2, p. 6) da `https://fwshomo.arca.gov.ar/wsltv/LtvService`, que no resuelve en DNS** (catalogo.md §3.3 y §4.2, probado el 2026-10-01) |
| Endpoint de producción | `https://serviciosjava.afip.gob.ar:443/wsltv/LtvService` (`soap:address` del WSDL de producción). El manual da `https://serviciosjava.arca.gov.ar/wsltv/LtvService`, que no se probó. Los otros hosts `*.arca.gov.ar` que se probaron no resuelven. La tabla de URLs del manual está desarmada en el PDF: las cuatro URLs quedaron en una columna y las cuatro descripciones en otra |
| targetNamespace | `http://serviciosjava.afip.gob.ar/wsltv/`. **El manual usa en todos los ejemplos `http://serviciosjava.arca.gob.ar/wsltv/`** (56 veces), que **no** es el del WSDL. La respuesta real (2026-10-02) usa `afip`. El simulador sigue al WSDL |
| service / port / binding / portType | `LtvService` / `LtvEndPoint` / `wsltvSOAP` / `LtvPortType` |
| WSDL guardado | `docs/arca/wsdl/wsltv-homologacion.wsdl` (1466 líneas, 71 699 bytes; autocontenido: sin `wsdl:import`, `xsd:import` ni `xsd:include`). El de producción es idéntico salvo el `soap:address` (diff del 2026-10-02). Los 30 `wsdl:message` están todos en uso |
| WSAA service id | **No indicado en el manual: NO VERIFICADO.** El §2.3 habla de "la información obtenida del WSAA" pero no da el nombre del servicio. Por la convención de wslsp y wslca lo más probable es `wsltv`, pero es una inferencia. catalogo.md §8 lo tiene como pendiente |
| SOAPAction | targetNamespace + nombre de la operación, por ejemplo `http://serviciosjava.afip.gob.ar/wsltv/generarLiquidacion`. Verificado en las 15 operaciones |
| Versión SOAP | Solo SOAP 1.1 (un único binding `soap:`), estilo document/literal y una sola part `parameters` por mensaje |
| elementFormDefault | No está declarado, así que vale `unqualified`. Solo el elemento raíz del Body lleva namespace; `auth`, `solicitud` y todos sus hijos van **sin** namespace (confirmado en la respuesta real) |
| Operaciones | **15**. Ninguna declara `wsdl:fault` |
| `dummy` | El mensaje de entrada no tiene parts y el Body va vacío. La respuesta es `DummyResp/respuesta/{appserver,authserver,dbserver}` |

## Autenticación

Va en el hijo `auth` (tipo `Auth`), que es el primer hijo 1..1 del elemento raíz de cada request:

```xml
<auth>
  <token>xsd:string</token>  <!-- 1..1 -->
  <sign>xsd:string</sign>    <!-- 1..1 -->
  <cuit>Cuit</cuit>          <!-- 1..1: xsd:long entre 10000000000 y 99999999999 (11 dígitos) -->
</auth>
```

- Se manda en las **14 operaciones que no son `dummy`** (§2.3). `cuit` es la CUIT representada, es decir el acopiador emisor.
- `token` y `sign` "tienen longitud variable según la respuesta del WSAA" (§4.2).
- **Falla documentada:** ninguna en concreto. El manual no da un texto para token inválido o vencido. La tabla §4.1 tiene dos códigos de autorización: `0001` "No se encuentra la dirección IP del equipo utilizado para emitir el comprobante." y `0002` "No se encuentra la cuit del representante conectado al servicio." **NO VERIFICADO** si un token inválido vuelve como Fault o en `errores`.
- **Falla observada (homologación, 2026-10-02).** Se probó `ConsultarProvinciasReq` con token y sign "abc", con token y sign vacíos, y sin `auth`. Las tres respuestas fueron iguales: `HTTP/1.0 200 OK`, sin `Content-Type`, con `Connection: Keep-Alive`, `Content-Length: 39` y un body de texto plano, por ejemplo `BL6343623393375 2026-10-02 15:08:30 500`. No es SOAP. Es lo mismo que pasa en todos los servicios de fwshomo. La hipótesis, NO VERIFICADA, es que un WAF/F5 reemplaza cualquier HTTP 500. **El Fault real no se pudo ver.**
- Request mínimo válido según el esquema (SOAPAction `"http://serviciosjava.afip.gob.ar/wsltv/consultarUltimoComprobanteXPuntoVenta"`, `Content-Type: text/xml; charset=utf-8`):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsl="http://serviciosjava.afip.gob.ar/wsltv/">
  <soapenv:Header/>
  <soapenv:Body>
    <wsl:ConsultarUltimoComprobanteXPuntoVentaReq>
      <auth><token>...</token><sign>...</sign><cuit>20111111112</cuit></auth>
      <solicitud><puntoVenta>1</puntoVenta><tipoComprobante>150</tipoComprobante></solicitud>
    </wsl:ConsultarUltimoComprobanteXPuntoVentaReq>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta real de `dummy` (homologación, 2026-10-02, HTTP 200):

```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:DummyResp xmlns:ns2="http://serviciosjava.afip.gob.ar/wsltv/"><respuesta><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></respuesta></ns2:DummyResp></S:Body></S:Envelope>
```

## Operaciones

Todos los elementos son `<Op>Req` y `<Op>Resp` con la primera letra en mayúscula, y cada respuesta trae un único hijo `respuesta`. Las liquidaciones vuelven en `LiquidacionDetalleRespuesta`, que tiene `liquidacion` (`LiquidacionDetalle`) y `errores`.

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Estado de app, auth y base | Body vacío | `respuesta/{appserver,authserver,dbserver}` | — |
| `consultarProvincias` | Provincias | `auth` | `provincia*` | Consulta tabla de parámetros |
| `consultarPuntosVentas` | Puntos de venta habilitados (en plural, sic) | `auth` | `puntoVenta*` | Consulta tabla de parámetros (por CUIT) |
| `consultarCondicionesVenta` | Condiciones de venta | `auth` | `condicionVenta*` | Consulta tabla de parámetros |
| `consultarDepositosAcopio` | Depósitos de acopio de la CUIT | `auth` | `acopio*{codigo,direccion,localidad,codigoPostal}` | Consulta tabla de parámetros (por CUIT; error 1069 si no hay) |
| `consultarUltimoComprobanteXPuntoVenta` | Último número autorizado | `solicitud/{puntoVenta,tipoComprobante}` | `nroComprobante` (`NroComprobante`, **0..1**) | **Lee estado**: el último número emitido para CUIT + pto vta + tipo |
| `consultarVariedadesClasesTabaco` | Variedades de tabaco y sus clases | `auth` | `variedad*{codigo,descripcion,clase*}` | Consulta tabla de parámetros |
| `consultarRetencionesTabacaleras` | Retenciones tabacaleras | `auth` | `retencion*` | Consulta tabla de parámetros |
| `consultarTributos` | Tributos | `auth` | `tributo*{codigo,descripcion}` | Consulta tabla de parámetros |
| `consultarLiquidacionXCAE` | Detalle de una liquidación o ajuste | `solicitud/{cae (NroCAE, 14 dígitos), pdf (boolean)}` | `LiquidacionDetalleRespuesta` | **Lee estado** por CAE |
| `consultarLiquidacionXNroComprobante` | Detalle de una liquidación o ajuste | `solicitud/{puntoVenta,tipoComprobante,nroComprobante,pdf (boolean)}` | `LiquidacionDetalleRespuesta` | **Lee estado** por pto vta + tipo + nro (la CUIT es la de `auth`) |
| `generarLiquidacion` | Liquidación de compra de tabaco verde | `solicitud`: `liquidacion{tipoComprobante,nroComprobante,puntoVenta,codDepositoAcopio,fechaLiquidacion,tipoCompra,titularCompra?,condicionVenta+,variedadTabaco,codProvinciaOrigenTabaco,...,fechaInicioActividad}`, `receptor{cuit,iibb?,nroSocio?,nroFET?}`, `romaneo+{nroRomaneo,fechaRomaneo,fardo+{codTrazabilidad,claseTabaco,peso}}`, `precioClase+{claseTabaco,precio}`, `retencion*`, `tributo*`, `flete?`, `bonificacion?` | `liquidacion/cabecera{tipoComprobante,fechaLiquidacion,fechaVencimiento,puntoVenta,...,nroComprobante,cae}`, `emisor`, `receptor`, `datosOperacion`, `detalleOperacion`, `retencion*`, `tributo*`, `totalesOperacion`, `pdf` | **Crea** una liquidación y devuelve el **CAE**. Se lee con `consultarLiquidacionXCAE` (CAE) o con `consultarLiquidacionXNroComprobante` (pto vta + tipo + nro) |
| `consultarTotalesDeClasesPorComprobantesParaAjustar` | Antes de ajustar: totales de kilos y fardos por clase y precio de los comprobantes a ajustar | `solicitud/comprobante+{tipoComprobante,puntoVenta,nroComprobante}` | `detalleTotalClase*{codClase,descripcionClase,totalFardos,totalKilos,precioXKilo}` | **Lee estado**, agregado sobre varias liquidaciones identificadas por pto vta + tipo + nro |
| `ajustarLiquidacion` | Ajuste de crédito o débito sobre una o varias liquidaciones | `solicitud`: `liquidacionAjuste{tipoComprobante,nroComprobante,fechaAjusteLiquidacion,puntoVenta,codDepositoAcopio,tipoAjuste (C/D),comprobanteAAjustar+,cuitReceptor,...,fechaInicioActividad}`, `precioClase+{claseTabaco,totalKilos,totalFardos,precio}`, `retencion*`, `tributo*` | `LiquidacionDetalleRespuesta` con `cabecera/tipoAjuste` y `caeAjustado*` (los CAE de las liquidaciones ajustadas) | **Crea** un ajuste con su propio CAE y número. Se vincula a 1..n originales por `comprobanteAAjustar` (pto + tipo + nro), y en la respuesta por `caeAjustado`. Se lee con las dos consultas |
| `generarAjusteFisico` | Ajuste físico sobre una liquidación (v1.3) | `solicitud/{tipoComprobante,puntoVenta,nroComprobante,fechaLiquidacion,fechaInicioActividad,comprobanteAAjustar}` (exactamente uno) | `LiquidacionDetalleRespuesta` con `cabecera/tipoAjuste` = `F` (ejemplo §2.6.15.3) | **Crea** un ajuste con CAE sobre un único comprobante. Si ya se había ajustado, da error 1135. Se lee con las dos consultas |

Diferencias entre el manual y el WSDL (el simulador sigue al **WSDL**):

- La respuesta de ejemplo del §2.6.14 se llama `ConsultarTotalesDeClasesPorComprobanteParaAjustarResp`, con "Comprobante" en singular. En el WSDL es `ConsultarTotalesDeClasesPorComprobantesParaAjustarResp`.
- El ejemplo de alta (§2.6.3.3) pone `condicionVenta` dentro de `cabecera`. En el WSDL no está en `CabeceraLiquidacion`: va en `datosOperacion` (`DatosOperacionDetalle`).
- El ejemplo de PDF (§2.5) llama a `port.consultarLiquidacionPorCAE(...)` con una clase `ConsultarLiquidacionPorCAESolicitud`. En el WSDL son `consultarLiquidacionXCAE` y `ConsultarLiquidacionXCAESolicitud`.
- El §1.3 explica la numeración con los tipos de comprobante "34" y "35". Es un texto copiado de otro servicio: en el WSDL `TipoComprobante` solo admite **150 y 151**.

## Errores

**Estructura.** Los errores van **dentro de la respuesta**, con HTTP 200, en `respuesta/errores/error` (`Errores`, con `error` 0..n de tipo `CodigoDescripcion`, `codigo` y `descripcion` `xsd:string`). En las liquidaciones, `errores` es hermano de `liquidacion`. **No hay `metadata`.** Como `codigo` es string, los códigos `0001` y `0002` conservan los ceros a la izquierda.

**Tipos de error (§2.4, p. 7-8):**

- **De formato:** son errores de esquema. El manual dice que vienen en `errores` con un código `cvc-*` y da dos ejemplos: `cvc-type.3.1.3` "The value 'xxxxx' of element 'periodo' is not valid." y `cvc-complex-type.2.4.a` "Invalid content was found starting with element 'puntoVenta'. One of '{periodo}' is expected." Documentado, no observado.
- **Internos:** `500` "Error general de aplicación." (Rechazada), `550` "Error al generar el archivo pdf." (Aceptada: la respuesta llega sin `pdf` y con el error en `errores`), `700` "Error de sincronismo." (Rechazada) y `800` "Servicio no disponible." (Rechazada).
- **De negocio:** están en §4.1.
- **Excepcionales:** un `S:Fault`. Ejemplo del manual:

```xml
<S:Fault xmlns:ns4="http://www.w3.org/2003/05/soap-envelope">
  <faultcode>S:Client</faultcode>
  <faultstring>Couldn't create SOAP message due to exception: XML reader error: com.ctc.wstx.exc.WstxEOFException: Unexpected EOF; was expecting a close tag for element &lt;soapenv:Envelope>
 at [row,col {unknown-source}]: [2,3]</faultstring>
</S:Fault>
```

  No hay `detail`. El status HTTP no está documentado (en JAX-WS es 500; en fwshomo eso termina en la página `BL... 500`).

**Códigos de negocio.** La tabla "Código y descripción de errores / validaciones" está en §4.1, "Tabla 4", págs. 65-70, y tiene **86 códigos**, de `0001` a `1201`. Es demasiado larga para copiarla acá. Al extraer el texto con `pdftotext -layout` las columnas salen corridas: hay que leerla del PDF. Los que importan para un flujo normal y para el simulador:

| Código | Descripción |
|---|---|
| 0001 | No se encuentra la dirección IP del equipo utilizado para emitir el comprobante. |
| 0002 | No se encuentra la cuit del representante conectado al servicio. |
| 1000 | La CUIT ingresada se encuentra pasiva o inactiva en el PUC. |
| 1003 | No posee puntos de venta habilitados para el actual sistema de ingreso. |
| 1006 | El punto de venta ingresado no es válido. |
| 1011 | La CUIT no se encuentra activa en el registro de acopiadores de tabaco. |
| 1012 | La fecha de liquidación no puede diferir en más de 10 días anteriores o posteriores a la fecha actual. |
| 1013 | La fecha de comprobante no puede ser anterior a la fecha del último comprobante generado para el mismo punto de venta. |
| 1014 / 1015 | Tipo de comprobante no válido para la cuit de receptor. La misma corresponde a tipo A / B. |
| 1039 | Un código de trazabilidad de un fardo que intenta agregar, ya fue utilizado en otra liquidación. |
| 1065 | El número de comprobante no puede ser nulo para una transacción por Web-Service. |
| 1066 | Error de autorización de número de comprobante. {Detalle de error} |
| 1068 | El código de depósito de acopio ingresado no es válido para la CUIT del emisor. |
| 1071 | Número de Comprobante no válido. |
| 1072 | El importe total de la liquidación o ajuste debe ser mayor a cero. |
| 1117 | Para generar un comprobante de ajuste debe agregar al menos un comprobante correspondiente a una liquidación. |
| 1118 | Uno o más comprobantes que intenta ajustar, pertenecen a liquidaciones emitidas para distintos vendedores. |
| 1120 | El comprobante ingresado es inexistente. |
| 1127 | No hay datos para los comprobantes ingresados, o bien, alguno de ellos corresponde a un ajuste. |
| 1134 | Para realizar un ajuste físico se de debe cargar exactamente un comprobante asociado. |
| 1135 | El comprobante ingresado ya fue ajustado previamente. |
| 1136 | El tipo de comprobante del ajuste debe ser el mismo que el del comprobante a ajustar. |
| 1200 | El tipo de liquidación seleccionado en la consulta es inválido. |
| 1201 | El código de liquidación consultado es inválido. |

Todos los códigos de la tabla son **R** (rechazada). El §1.3 dice que una combinación incorrecta de pto vta, tipo y número da "un mensaje de error" pero no dice cuál. Los candidatos son 1066 y 1071: **NO VERIFICADO**.

## Comportamiento a simular

**Numeración (§1.3, p. 4-5).**

- Un CAE autorizado se identifica por **CUIT + `puntoVenta` + `tipoComprobante` + `nroComprobante`**. El número arranca en 1 y sube de a uno, por separado para cada CUIT, punto de venta y tipo de comprobante.
- Si la solicitud se rechaza, el número no se consume. El próximo número válido es `consultarUltimoComprobanteXPuntoVenta + 1`.
- **Los ajustes comparten tipo de comprobante y numeración con la liquidación original.** El error 1136 exige que el tipo del ajuste sea igual al del comprobante ajustado. En los ejemplos, un ajuste tipo 151 nro 13 ajusta una 151, y un ajuste físico 151 nro 1 ajusta una 151.
- `TipoComprobante` es un **`xsd:string` con enumeración** `150` (Liquidación de compra primaria para el sector tabacalero A) y `151` (la B), según el WSDL y §3.1.
- Rangos (WSDL): `PuntoVenta` int 1..99999 (la v1.1 lo amplió de 4 a 5 dígitos) y `NroComprobante` int 1..99999999.
- La respuesta de `consultarUltimo...` tiene `nroComprobante` **0..1**, así que puede venir vacía. Cuando todavía no hay comprobantes, lo esperable es que no venga, pero eso está **NO VERIFICADO**. El ejemplo del manual devuelve `2`.

**Id que asigna ARCA.** Viene en `liquidacion/cabecera/cae` (`xsd:long`). En las consultas el tipo es `NroCAE`: long entre 10000000000000 y 99999999999999, o sea **14 dígitos** (§3.1: "NroCAE long 14"). Ejemplos: `85523002502850` y `86399016432409`. La cabecera también trae:

- `fechaVencimiento` (en los ejemplos cae 10 a 15 días después de `fechaLiquidacion`; la regla no está documentada).
- `nroComprobante`, `puntoVenta` y `tipoComprobante`.
- `domicilioPuntoVenta`, `codDepositoAcopio` y `domicilioDepositoAcopio`.
- En los ajustes, `tipoAjuste`: `C` o `D` en `ajustarLiquidacion`, `F` en `generarAjusteFisico`.

`LiquidacionDetalle/caeAjustado` (0..n, long) lista los CAE de las liquidaciones afectadas por un ajuste (agregado en la v1.1).

**Idempotencia.** No hay un id de request. La única clave es pto vta + tipo + nro. Reenviar un número ya autorizado debería dar un error de numeración (1066 o 1071) **[INFERIDO]**. Hay otras dos reglas que funcionan como deduplicación de negocio: un `codTrazabilidad` de fardo no se puede usar en dos liquidaciones (1039), y un comprobante no se puede ajustar físicamente dos veces (1135). La recuperación documentada para un timeout (§1.4) es consultar el último número y leer la liquidación.

**Ajustes y estados.**

- Flujo de crédito o débito: primero `consultarTotalesDeClasesPorComprobantesParaAjustar` con los comprobantes a ajustar. Eso devuelve los kilos, fardos y precio por clase. Después, `ajustarLiquidacion` con `tipoAjuste` C o D, la lista de `comprobanteAAjustar` (1..n) y un `precioClase` por cada clase involucrada (errores 1110-1113).
- Todos los comprobantes ajustados tienen que ser del mismo vendedor (1118) y del mismo receptor (1122).
- No se puede ajustar un ajuste (1127: "alguno de ellos corresponde a un ajuste").
- La fecha del CAE ajustado tiene que ser ≤ la fecha del ajuste (1116 y 1126).
- Ajuste físico: `generarAjusteFisico` con **exactamente un** comprobante (1134), que no haya sido ajustado antes (1135).
- El manual no dice si el ajuste físico anula la liquidación. Los estados posibles ("vigente", "ajustada") son una inferencia a partir del 1135.

**Plazos.**

- `fechaLiquidacion` a ±10 días de hoy (1012).
- No puede ser anterior al último comprobante del mismo punto de venta (1013).
- `fechaRomaneo` ≤ `fechaLiquidacion` (1034).
- `fechaInicioActividad` anterior a hoy (1076).

**Validaciones de forma que conviene imitar.**

- `codDepositoAcopio` tiene que ser uno de los que devuelve `consultarDepositosAcopio` para esa CUIT (1068).
- La clase del fardo tiene que tener precio en `precioClase` y no puede sobrar ninguno (1051-1053).
- `variedadTabaco` es un string de 2 caracteres (por ejemplo `BR` o `CA`), y los fardos tienen que coincidir con la variedad (1042 y 1043).
- `tipoCompra` es una enumeración: `CPS` (por sí), `CPT` (por terceros) o `CPST` (por sí y por terceros). `titularCompra{cuit,porcentaje}` es opcional.
- `AlicuotaIVA` (0.0 o 21.0) y `TipoLiquidacion` (L/A) están definidos en el WSDL, pero **ningún elemento los usa**.

**PDF (§2.5).**

- `pdf` (`xsd:base64Binary`) es "el mismo archivo que se imprime por la aplicación web".
- En las dos consultas se pide con `solicitud/pdf` (boolean, **obligatorio** en el esquema).
- `generarLiquidacion`, `ajustarLiquidacion` y `generarAjusteFisico` no tienen flag. Los ejemplos de alta (§2.6.3.3) y de ajuste físico (§2.6.15.3) traen `pdf` en la respuesta, pero que venga siempre está **NO VERIFICADO**.
- Con el error 550 la operación se acepta y la respuesta llega sin `pdf`.

**Formatos (§4.2).**

- Fecha: `AAAA-MM-DD` sin huso horario.
- Redondeo: Round Half Even a 2 decimales.
- Importes de la respuesta: `xsd:double` y `xsd:float`, no `decimal`. Por ejemplo, el ejemplo devuelve `<importe>20.0</importe>`.
- `NroRomaneo`: long de hasta 15 dígitos. `PesoFardo`: short 1..999. `CodigoTrazabilidad`: string 1..30.

**Paginación:** no hay.

## No verificado

- El WSAA service id. El manual no lo da; se supone `wsltv`.
- El formato real del error de autenticación. En fwshomo cualquier error llega como la página `BL<n> <fecha> 500` (capturas del 2026-10-02), y el manual no lo documenta.
- Qué código devuelve una numeración incorrecta (¿1066 o 1071?) y qué pasa al reenviar un número ya autorizado.
- Qué devuelve `consultarUltimoComprobanteXPuntoVenta` sin comprobantes previos (el elemento es opcional).
- Si `serviciosjava.arca.gov.ar` existe. `fwshomo.arca.gov.ar` no resuelve (2026-10-01).
- Qué responde el servidor real si recibe el namespace `http://serviciosjava.arca.gob.ar/wsltv/` de los ejemplos del manual.
- Si el ajuste físico anula la liquidación y qué estados tiene una liquidación.
- Si `generarLiquidacion` y los ajustes devuelven el `pdf` siempre.
- La regla de `fechaVencimiento` del CAE.
- Si los errores `cvc-*` llegan de verdad en `errores` con HTTP 200.
