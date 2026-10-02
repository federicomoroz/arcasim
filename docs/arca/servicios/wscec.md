# wscec

WSCEC: información de facturación electrónica de los beneficiarios de la **Ley de Economía del Conocimiento**. Un organismo autorizado (el manual habla de "CUIT del organismo que hace la consulta") consulta los **comprobantes de exportación** que emitió un contribuyente caracterizado en el régimen, por período o para los doce períodos previos a su inscripción. Las consultas grandes son asincrónicas: se crean, quedan pendientes y después se leen por código.

Fuentes:

- Manual WSCEC 1.0: `https://www.afip.gob.ar/ws/wscec/manual-wscec.pdf`.
- WSDL de homologación y producción, descargados el 2026-10-02 (iguales salvo el `soap:address`).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, Spring Boot WS "factu.fisca" (header `info` como wsrgiva), detrás del F5 de `fwshomo` |
| SOAP | 1.1, document/literal |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wscec/CECService/` (con barra final, así lo dicen el WSDL y el manual) |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wscec/CECService/` |
| WSDL | `https://fwshomo.afip.gov.ar/wscec/CECService?wsdl` (sin barra) |
| targetNamespace | `http://ar.gov.afip.wscec/CECService/` (sin `elementFormDefault`: hijos sin namespace) |
| portType / binding / service | `CECPortType` / `CECBinding` / `CECService`, port `CECEndpoint` |
| SOAPAction | `http://ar.gov.afip.wscec/CECService/<operación>` |
| Header de respuesta | `info{ambiente, fecha, id}` en `http://headers.springbootws.factu.fisca.afip.gob.ar/xml` |
| Archivo guardado | `wsdl/wscec-homologacion.wsdl` (sin imports) |
| WSAA service id | **No indicado** en el manual. Probablemente `wscec`: **NO VERIFICADO** |
| Versión | Header en vivo (2026-10-02): `id` = `CECService 1.0.1 2022-08-17T18:23:06.445Z`, `ambiente` = `homologacion-externa - FI1` |

**Trampas del contrato.**

- **Nombre de elemento con espacio.** En `consultarComprobantesExpoCodigoConsultaRequest` el WSDL declara `name="codigoConsulta "` (con un espacio al final). No es un NCName válido; generadores estrictos de clientes pueden fallar o renombrar. ArcaSim tiene que servir el WSDL tal cual y aceptar el elemento `codigoConsulta` (qué acepta el servicio real está **NO VERIFICADO**).
- **Namespace del manual.** El manual mezcla `http://ar.gob.afip.wscec/CECService/` (con `gob`) y el header `https://ar.gob.afip.wsCEC/CECService/`. El real es `http://ar.gov.afip.wscec/CECService/` y el header es el de factu.fisca.
- **Nombre del wrapper de autenticación.** La sección 2.2 del manual dice `authRequest`; el WSDL y los esquemas de cada operación dicen `autenticacion`.
- **Errores.** El manual muestra `errores/codigoDescripcion` y `arrayErroresFormato/codigoDescripcionString`; el WSDL y la respuesta real usan `errores/error{codigo, descripcion}` y `erroresFormato/errorFormato{codigo, descripcion}`.
- **CUIT.** `CuitSimpleType` exige `20000000000 < cuit < 99999999999`.
- **`dummy`** sin cuerpo; despacho por SOAPAction.

## Autenticación

```xml
<cec:obtenerConsultasRequest xmlns:cec="http://ar.gov.afip.wscec/CECService/">
  <autenticacion>
    <token>...</token>
    <sign>...</sign>
    <cuitRepresentada>30000000007</cuitRepresentada>
  </autenticacion>
  ...
</cec:obtenerConsultasRequest>
```

El manual dice que los errores de autenticación son "excepcionales" (Fault). **En vivo no**: vuelven en `errores`, HTTP 200, igual que en wsrgiva (homologación, 2026-10-02):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente>homologacion-externa - FI1</ambiente><fecha>2026-10-02T15:24:17</fecha><id>CECService 1.0.1 2022-08-17T18:23:06.445Z</id></info></soap:Header><soap:Body><ns2:obtenerConsultasResponse xmlns:ns2="http://ar.gov.afip.wscec/CECService/"><obtenerConsultasReturn><pagina>0</pagina><hayMas>N</hayMas><errores><error><codigo>505</codigo><descripcion>El Token no se corresponde con la Firma. {token = [abc]firma = [abc]}</descripcion></error><error><codigo>506</codigo><descripcion>El Token no cumple con el Esquema (XSD) de Autenticacion. {abc}</descripcion></error></errores></obtenerConsultasReturn></ns2:obtenerConsultasResponse></soap:Body></soap:Envelope>
```

- Con un TA bien formado en base64 y firma falsa: un solo error, 505, con el token completo entre corchetes.
- En error, la respuesta trae `pagina` 0 y `hayMas` N.

Códigos 502 a 517 (manual, 2.2.1; mismos textos que wsrgiva):

| Código | Mensaje |
|---|---|
| 502 | La Seccion de Autenticación del Request no cumple con el Esquema (XSD) de Autenticacion. |
| 503 | El Formato del Token es inválido. |
| 504 | El Formato de la Firma es inválida. |
| 505 | El Token no se corresponde con la Firma. |
| 506 | El Token no cumple con el Esquema (XSD) de Autenticacion. |
| 507 | El Formato del Servicio asociado a Token es inválido. |
| 508 | El Servicio asociado a Token difiere del especificado para el Sistema. |
| 509 | El Token se encuentra Expirado. |
| 510 | La CUIT, CUIL o CDI es Nula, esta Vacia o tiene un Formato inválido. |
| 511 | La CUIT, CUIL o CDI no pudo ser encontrada. |
| 512 | La CUIL o CDI no esta activa. |
| 513 | La CUIT no esta activa. |
| 514 | La CUIT no tiene un domicilio activa. |
| 515 | La CUIT no tiene una actividad activa. |
| 516 | El Servicio de Autenticacion no se encuentra Operativo. |
| 517 | La CUIT Representada no se encuentra entre las que pueden ser Representadas en Relations. |

## Operaciones

5 operaciones en `CECPortType`.

| Operación | Propósito | Entrada (además de `autenticacion`) | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Infraestructura | Body vacío | `dummyResponse/return{appserver, authserver, dbserver}` | — |
| `obtenerConsultas` | Lista las consultas hechas por el organismo | `filtro{cuit?, fechaDesde?, fechaHasta?}`, `pagina` | `obtenerConsultasReturn{consultas{consulta[1..n]: DatosConsultaType}?, pagina?, hayMas? (S/N), errores?, erroresFormato?}` | Consulta. Clave: `codigoConsulta` |
| `consultarComprobantesExpoPeriodo` | Comprobantes de exportación de una CUIT en un período | `cuit`, `periodo` (int `YYYYMM`, > 201901), `pagina` | `consultarComprobantesExpoReturn{datosConsulta?, comprobantesExportacion{comprobanteExportacion[1..n]}?, pagina?, hayMas, errores?, erroresFormato?}` | **Crea una consulta** (sincrónica `TE` o asincrónica `PE`). Clave: `codigoConsulta` |
| `consultarComprobantesExpoInscripcion` | Comprobantes de los 12 períodos previos a la inscripción de la CUIT | `cuit`, `pagina` | Igual | **Crea una consulta**. Clave: `codigoConsulta` |
| `consultarComprobantesExpoCodigoConsulta` | Lee (página a página) el resultado de una consulta ya creada | `codigoConsulta` (ver trampa del espacio), `pagina` | Igual | Consulta del resultado. Clave: `codigoConsulta` |

- **DatosConsultaType**: `codigoConsulta` (long), `fechaConsulta` (date), `cuit`, `estado` (`PE` pendiente de ejecución, `TE` terminado, `CA` cancelado), `periodoDesde` (int), `periodoHasta` (int).
- **ComprobanteExportacionType**: `puntoVenta`, `tipoComprobante`, `numero`, `tipoExportacion`, `destino?`, `fecha` (string), `cliente`, `cuitPais?`, `claveTributariaDestino?`, `moneda`, `cotizacionMoneda`, `importeTotal`, `comprobantesAsociados{comprobanteAsociado[1..n]{tipoCbte, puntoVenta, numero, cuitEmisor}}?`, `cae?` (long), `fechaVencimiento?`, `items{itemComprobanteExportacion[1..n]{descripcionProducto, totalItem}}`. Son los datos de una factura E de wsfexv1.
- `obtenerConsultas` (manual, 2.3.1.4): con `cuit` y sin fechas, todas las consultas de esa CUIT; con fechas y sin `cuit`, todas las del rango; si viene solo una de las dos fechas, se ignoran; sin `cuit` ni fechas, error 2004.

Respuesta real de `dummy` (homologación, 2026-10-02), HTTP 200, `Content-Type: text/xml;charset=UTF-8`:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente>homologacion-externa - FI1</ambiente><fecha>2026-10-02T15:24:17</fecha><id>CECService 1.0.1 2022-08-17T18:23:06.445Z</id></info></soap:Header><soap:Body><ns2:dummyResponse xmlns:ns2="http://ar.gov.afip.wscec/CECService/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse></soap:Body></soap:Envelope>
```

## Errores

Tres niveles: excepcional (Fault; desde Internet se ve como el `BL... 500` del WAF de `fwshomo`), formato (`erroresFormato/errorFormato`) y negocio (`errores/error`), ambos con HTTP 200. Con errores de formato no se evalúa el negocio.

Fault excepcional según el manual (1.3.1): `faultcode` `soap:WebServiceFault`, `faultstring` "Ocurrió un error intentando parsear el Soap Request.", `detail{codigo: 2, descripcion: "Unmarshalling Error: cvc-complex-type.2.4.a: Invalid content was found starting with element 'tokenn'. One of '{token}' is expected."}`.

Códigos de negocio y formato (manual; `<...>` son valores que se reemplazan):

| Código | Operaciones | Mensaje |
|---|---|---|
| 2002 | obtenerConsultas | Formato de CUIT inválido. Revisar '<cuit>': tiene un formato incorrecto. |
| 2003 | obtenerConsultas | Rango de fechas inválido. 'fechaHasta' tiene que ser más antigua que 'fechaDesde': <fechaHasta> es una fecha previa a <fechasDesde>. / Rango de fechas inválido. La diferencia entre <fechaHasta> y <fechaDesde> son <diferencia> días: el intervalo máximo de dias es 31. |
| 2004 | obtenerConsultas | Faltan campos. Si 'cuit' está vacío, se deben indicar 'fechaDesde' y 'fechaHasta'. |
| 2005 | todas las paginadas | Número de página inválido. El número de página no puede ser menor a 1. |
| 2008 | CodigoConsulta | El código de consulta debe ser mayor o igual a 1. El código <codigo> no es válido. |
| 2009 | Periodo (formato) | El periodo de consulta debe respetar el formato 'YYYYMM', con año (YYYY) y mes (MM) válidos. El año <año> no es válido. / ... El mes <mes> no es válido. |
| 4009 | Periodo, Inscripcion | La CUIT no se encuentra en los Registros de AFIP. '<cuit>' no está en el padrón. |
| 4010 | CodigoConsulta, Periodo, Inscripcion | No se encontraron datos. No existe consulta con código <codigo>. / No se encontraron datos. No se encontraron comprobantes para la consulta de código <codigo>. |
| 4011 | CodigoConsulta | Ocurrió un error al dar de alta una consulta. |
| 4012 | CodigoConsulta | Ocurrió un error al dar de alta el comprobante de una consulta. |
| 4013 | CodigoConsulta | Ocurrió un error al dar de alta información de auditoría. |
| 4014 | Periodo / Inscripcion | Caracterización de la CUIT no es válida. <cuit> no tiene la caracterización 451. (Periodo) / ... 450. (Inscripcion) |
| 4015 | Periodo | Periodo de consulta previo al de caracterización. <cuit> tiene un periodo de caracterización <periodoCaracterizacion>, posterior al periodo de consulta <periodo>. |
| 4016 | Periodo | Periodo de consulta igual al actual. El periodo actual <periodo> todavía no venció. Vence el día 5 del mes que le sigue. |
| 4019 | Periodo, Inscripcion | Ya existe una consulta pendiente con los mismos valores. Valores de consulta pendiente: cuit = <cuit>, cuit organismo = <cuit representada>, periodo = [<desde>, <hasta>]. |

El ejemplo de formato del manual devuelve `<pagina>0</pagina>` y `errorFormato` con código 2009: es decir, 2009 viaja como **error de formato** (`erroresFormato`), no de negocio.

## Comportamiento a simular

- `dummy` por SOAPAction, header `info` en todas las respuestas.
- Auth en `errores` con HTTP 200, `pagina` 0, `hayMas` N; 505 y 506 juntos si el token no parsea.
- Estado: consultas por organismo (`cuitRepresentada`) con `codigoConsulta` secuencial, `fechaConsulta`, `cuit`, rango de períodos y estado `PE`/`TE`/`CA`.
- `consultarComprobantesExpoPeriodo` e `...Inscripcion`: validar caracterización (451 para período, 450 para inscripción, según el manual), período vencido (día 5 del mes siguiente), período ≥ caracterización; crear consulta; si el volumen supera un umbral configurable, devolver `datosConsulta` en `PE` sin comprobantes y pasarla a `TE` después; si no, `TE` con la primera página. Rechazar con 4019 una consulta idéntica pendiente.
- `consultarComprobantesExpoCodigoConsulta`: devuelve la página pedida y `hayMas`; 4010 si no existe.
- `obtenerConsultas`: listado paginado con las reglas de filtro de 2.3.1.4.
- Los comprobantes pueden salir del mismo almacén que el wsfexv1 simulado (factura E), para que los datos sean coherentes.

## No verificado

- El service id de WSAA.
- Qué hace el servicio real con el elemento `codigoConsulta ` (con espacio) del WSDL.
- El tamaño de página y el umbral que vuelve asincrónica una consulta.
- Si 450 y 451 son de verdad caracterizaciones distintas para cada operación o una errata.
- El texto del Fault excepcional real (oculto por el WAF).
