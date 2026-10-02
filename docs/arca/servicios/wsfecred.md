# wsfecred

**FECredService**, el "Web Service Registro de Facturas de Crédito Electrónica MiPyMEs". Gestiona la **cuenta corriente** que nace cada vez que se autoriza una Factura de Crédito Electrónica MiPyME (FCE, tipos 201, 206 y 211) por wsfev1, wsmtxca o Comprobantes en línea. Con este servicio:

- el **comprador** (receptor) acepta o rechaza la factura y su cuenta corriente, rechaza notas de débito o crédito sueltas e informa la cancelación total (pago);
- el **vendedor** (emisor) cambia la opción de transferencia (ADC o SCA) mientras la cuenta es modificable e informa la factura aceptada a un Agente de Depósito Colectivo;
- los dos consultan comprobantes, cuentas corrientes, historiales de estado, remitos asociados y tablas de parámetros (retenciones, motivos de rechazo, formas de cancelación, ajustes);
- cualquiera consulta si una CUIT está obligada a recibir FCE y desde qué monto.

Marco: Ley 27.440 (Título I) y RG 4367/2018 (ver `docs/arca/normativa.md` §8). La emisión de la factura **no** pasa por este servicio: se autoriza con CAE/CAEA en wsfev1 o wsmtxca. La lectura del lado del agente y del Sistema de Circulación Abierta tampoco: son wsfecredagente y wsfecredsca.

Fecha de relevamiento: 2026-10-02.

### Fuentes

| Id | Fuente | URL / ruta | Uso |
|---|---|---|---|
| **[MAN]** | Manual para el Desarrollador WSFECRED, **versión 2.0.3** (20/12/2023), 86 págs. | <https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECRED_v2.0.3.pdf> | Fuente principal. Leído completo. Las tablas de códigos salen corridas en el texto plano; se re-extrajeron con PyMuPDF (`find_tables`) y se revisaron las imágenes de los esquemas (págs. 14, 21, 36, 42, 65, 68, 72, 75, 77, 79). |
| **[WSDL]** | WSDL de homologación | `docs/arca/wsdl/wsfecred-homologacion.wsdl` (81 362 bytes). Se volvió a bajar el 2026-10-02 (`vivo/fecred-wsdl-get.body`) y es byte a byte igual. El de producción es idéntico salvo `soap:address` (brief del relevamiento). | Estructura. **Manda sobre el manual** cuando difieren. |
| **[VIVO]** | Llamadas sin certificado a homologación, 2026-10-02 | `scratchpad/vivo/fecred-*` (`.req.xml`, `.hdr`, `.body`, `.raw`) | Dummy, forma real del header, comportamiento ante token inválido, SOAP 1.2, SOAPAction. |
| **[PYAF]** | Cliente `wsfecred.py` de PyAfipWs (2018-2021) | <https://github.com/PyAr/pyafipws/blob/main/wsfecred.py> | Secundaria. Sólo un ejemplo de cuenta corriente en un docstring. No hay cassettes grabados de este servicio. |
| **[NORM]** | `docs/arca/normativa.md` §8 y `docs/arca/wsfev1.md` (relevamientos propios) | repo | Monto mínimo FCE, CUITs de prueba, reglas de emisión FCE en wsfev1. |

Las páginas se citan como "pág. N": página física del PDF (1..86). El índice del manual coincide con esa numeración.

Marcas de confianza: **[MAN]**, **[WSDL]**, **[VIVO]** = verificado en fuente oficial o en el servicio. **[PYAF]** = sólo fuente secundaria. **[INFERIDO]** = deducido, sin texto oficial explícito. **NO VERIFICADO** = no se pudo comprobar.

## Contrato

| Campo | Valor | Fuente |
|---|---|---|
| Dialecto | **Java JAX-WS** (Metro): envelope con prefijo `S:`, header `info`, respuesta con `ns2:`, declaración `<?xml version='1.0' encoding='UTF-8'?>` con comillas simples | [VIVO] |
| Endpoint homologación | `https://fwshomo.afip.gov.ar:443/wsfecred/FECredService` (`soap:address`). El manual lo escribe sin `:443` | [WSDL]; [MAN] pág. 16 |
| Endpoint producción | `https://serviciosjava.afip.gob.ar:443/wsfecred/FECredService` | `soap:address` de producción; [MAN] pág. 16 |
| WSDL | `?wsdl` sobre cada endpoint | [MAN] pág. 16; [VIVO] |
| `targetNamespace` | `http://ar.gob.afip.wsfecred/FECredService/` (con `http`, `.gob.ar` y barra final) | [WSDL] |
| `elementFormDefault` | **No declarado → `unqualified`**. Sólo el elemento global del Body (`xxxRequest`) va en el namespace; todos sus hijos van **sin namespace**. La respuesta real lo confirma: `<ns2:dummyResponse xmlns:ns2="..."><dummyReturn>...` | [WSDL]; [VIVO] |
| portType / binding / service / port | `FECredServicePortType` / `FECredServiceSOAP` / `FECredService` / `FECredServiceSOAP` | [WSDL] |
| SOAP | Sólo **SOAP 1.1** (`soap:binding style="document"`, `use="literal"`). No hay binding SOAP 1.2 | [WSDL] |
| SOAPAction | `http://ar.gob.afip.wsfecred/FECredService/<operación>` (lista abajo) | [WSDL] |
| Header de respuesta | `<info xmlns="https://ar.gob.afip.wsfecred/FECredService/">` con `ambiente`, `fecha`, `id`. **Ojo: el namespace del header es `https://`**, el del body `http://` | [VIVO]; [MAN] pág. 5 |
| WSAA service id | **`wsfecred`**: "tener en cuenta que debe enviar el tag service con el valor "wsfecred"" | [MAN] pág. 17 |
| Relación en clave fiscal | Asociar el certificado al servicio "Web Service Registro de Facturas de Crédito Electrónica MiPyMEs" | [MAN] pág. 17 |
| Versión del servicio | `<id>WS-2.1.6</id>` en homologación (2026-10-02). El manual es 2.0.3 | [VIVO] |
| Archivo guardado | `docs/arca/wsdl/wsfecred-homologacion.wsdl`; esquema inline, sin imports | — |

### Operaciones, elementos y SOAPAction

En el orden del `portType` (21 operaciones). El nombre del mensaje WSDL coincide con el del elemento salvo en `consultarCuentasEnAgtDptoCltv`, cuyos mensajes se llaman `consultarCtaAgenteRequest`/`consultarCtaAgenteResponse` (no afecta al XML).

| # | Operación | Elemento de request | Elemento de response | Hijo único de la response | Manual |
|---:|---|---|---|---|---|
| 1 | `dummy` | — (Body vacío) | `dummyResponse` | `dummyReturn` | §2.4.23, págs. 61-62 |
| 2 | `consultarComprobantes` | `consultarComprobantesRequest` | `consultarComprobantesResponse` | `consultarCmpReturn` | §2.4.9, págs. 36-38 |
| 3 | `rechazarNotaDC` | `rechazarNotaDCRequest` | `rechazarNotaDCResponse` | `rechazarNotaDCReturn` | §2.4.5, págs. 28-29 |
| 4 | `consultarCtasCtes` | `consultarCtasCtesRequest` | `consultarCtasCtesResponse` | `consultarCtasCtesReturn` | §2.4.10, págs. 39-41 |
| 5 | `consultarCtaCte` | `consultarCtaCteRequest` | `consultarCtaCteResponse` | `consultarCtaCteReturn` | §2.4.11, págs. 42-43 |
| 6 | `informarCancelacionTotalFECred` | `informarCancelacionTotalFECredRequest` | `informarCancelacionTotalFECredResponse` | `operacionFECredReturn` | §2.4.7, págs. 32-33 |
| 7 | `aceptarFECred` | `aceptarFECredRequest` | `aceptarFECredResponse` | `operacionFECredReturn` | §2.4.3, págs. 20-25 |
| 8 | `rechazarFECred` | `rechazarFECredRequest` | `rechazarFECredResponse` | `operacionFECredReturn` | §2.4.4, págs. 26-27 |
| 9 | `informarFacturaAgtDptoCltv` | `informarFacturaAgtDptoCltvRequest` | `informarFacturaAgtDptoCltvResponse` | `operacionFECredReturn` | §2.4.6, págs. 30-31 |
| 10 | `consultarFacturasAgtDptoCltv` | `consultarFacturasAgtDptoCltvRequest` | `consultarFacturasAgtDptoCltvResponse` | `consultarFacturasAgtDptoCltvReturn` | §2.4.16, págs. 50-51 |
| 11 | `consultarCuentasEnAgtDptoCltv` | `consultarCuentasEnAgtDptoCltvRequest` | `consultarCuentasEnAgtDptoCltvResponse` | `consultarCuentasEnAgtDptoCltvReturn` | §2.4.12, págs. 44-45 |
| 12 | `consultarObligadoRecepcion` | `consultarObligadoRecepcionRequest` | `consultarObligadoRecepcionResponse` | `consultarObligadoRecepcionReturn` | §2.4.13, págs. 46-47 (**deprecada**) |
| 13 | `consultarTiposRetenciones` | `consultarTiposRetencionesRequest` | `consultarTiposRetencionesResponse` | `consultarTiposRetencionesReturn` | §2.4.14, pág. 48 |
| 14 | `consultarTiposMotivosRechazo` | `consultarTiposMotivosRechazoRequest` | `consultarTiposMotivosRechazoResponse` | `codigoDescripcionReturn` | §2.4.15, pág. 49 |
| 15 | `consultarTiposFormasCancelacion` | `consultarTiposFormasCancelacionRequest` | `consultarTiposFormasCancelacionResponse` | `codigoDescripcionReturn` | §2.4.17, pág. 52 |
| 16 | `obtenerRemitos` | `obtenerRemitosRequest` | `obtenerRemitosResponse` | `obtenerRemitosReturn` | §2.4.18, págs. 53-54 |
| 17 | `consultarHistorialEstadosComprobante` | `consultarHistorialEstadosComprobanteRequest` | `consultarHistorialEstadosComprobanteResponse` | `consultarHistorialEstadosComprobanteReturn` | §2.4.19, págs. 55-56 |
| 18 | `consultarHistorialEstadosCtaCte` | `consultarHistorialEstadosCtaCteRequest` | `consultarHistorialEstadosCtaCteResponse` | `consultarHistorialEstadosCtaCteReturn` | §2.4.20, pág. 57 |
| 19 | `consultarTiposAjustesOperacion` | `consultarTiposAjustesOperacionRequest` | `consultarTiposAjustesOperacionResponse` | `codigoDescripcionReturn` | §2.4.21, pág. 58 |
| 20 | `consultarMontoObligadoRecepcion` | `consultarMontoObligadoRecepcionRequest` | `consultarMontoObligadoRecepcionResponse` | `consultarMontoObligadoRecepcionReturn` | §2.4.22, págs. 59-60 |
| 21 | `modificarOpcionTransferencia` | `modificarOpcionTransferenciaRequest` | `modificarOpcionTransferenciaResponse` | `operacionFECredReturn` | §2.4.8, págs. 34-35 |

Manual vs WSDL: el manual describe las mismas 21 operaciones (§2.4.3 a §2.4.23). En la lista de §2.4.1 (pág. 18) nombra además `consultarCuentasComitente`, "reemplazado por consultarCuentasEnAgtDptoCltv"; el change log 2.0.0 dice que se eliminó (pág. 84). **No está en el WSDL.** No hay operaciones en el WSDL que falten en el manual.

SOAPAction (exactos, [WSDL]):

```
http://ar.gob.afip.wsfecred/FECredService/dummy
http://ar.gob.afip.wsfecred/FECredService/consultarComprobantes
http://ar.gob.afip.wsfecred/FECredService/rechazarFECred
http://ar.gob.afip.wsfecred/FECredService/consultarCtasCtes
http://ar.gob.afip.wsfecred/FECredService/rechazarNotaDC
http://ar.gob.afip.wsfecred/FECredService/informarFacturaAgtDptoCltv
http://ar.gob.afip.wsfecred/FECredService/consultarTiposRetenciones
http://ar.gob.afip.wsfecred/FECredService/consultarObligadoRecepcion
http://ar.gob.afip.wsfecred/FECredService/consultarFacturasAgtDptoCltv
http://ar.gob.afip.wsfecred/FECredService/informarCancelacionTotalFECred
http://ar.gob.afip.wsfecred/FECredService/consultarTiposMotivosRechazo
http://ar.gob.afip.wsfecred/FECredService/consultarTiposFormasCancelacion
http://ar.gob.afip.wsfecred/FECredService/aceptarFECred
http://ar.gob.afip.wsfecred/FECredService/consultarCtaCte
http://ar.gob.afip.wsfecred/FECredService/consultarCuentasEnAgtDptoCltv
http://ar.gob.afip.wsfecred/FECredService/obtenerRemitos
http://ar.gob.afip.wsfecred/FECredService/consultarHistorialEstadosComprobante
http://ar.gob.afip.wsfecred/FECredService/consultarHistorialEstadosCtaCte
http://ar.gob.afip.wsfecred/FECredService/consultarTiposAjustesOperacion
http://ar.gob.afip.wsfecred/FECredService/consultarMontoObligadoRecepcion
http://ar.gob.afip.wsfecred/FECredService/modificarOpcionTransferencia
```

### Forma del request

Hijos sin namespace (por `unqualified`). Esta es la forma que usaron todas las llamadas reales [VIVO]:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:x="http://ar.gob.afip.wsfecred/FECredService/">
  <soapenv:Header/>
  <soapenv:Body>
    <x:consultarTiposRetencionesRequest>
      <authRequest>
        <token>...</token>
        <sign>...</sign>
        <cuitRepresentada>20111111112</cuitRepresentada>
      </authRequest>
    </x:consultarTiposRetencionesRequest>
  </soapenv:Body>
</soapenv:Envelope>
```

Qué pasa si el cliente califica los hijos (`<x:authRequest>`): **NO VERIFICADO** en vivo (toda llamada sin TA válido termina en `BL...`). Por el esquema, es un error de validación [INFERIDO]. El manual muestra la declaración `xmlns:ser=" http://ar.gob.afip.wsfecred/FECredService/"` con comillas tipográficas y un espacio antes del URI (págs. 9-11, 16): es una errata de edición.

### Header `info` y envelope de respuesta

Respuesta real completa de `dummy` [VIVO, `fecred-dummy-emptybody.raw`]:

```
HTTP/1.1 200 OK
X-XSS-Protection: 1; mode=block
X-Frame-Options: sameorigin
X-Content-Type-Options: nosniff
Content-Type: text/xml;charset=utf-8
Transfer-Encoding: chunked
```
```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Header><info xmlns="https://ar.gob.afip.wsfecred/FECredService/"><ambiente>Testing - FI1</ambiente><fecha>2026-10-02T15:08:21</fecha><id>WS-2.1.6</id></info></S:Header><S:Body><ns2:dummyResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/"><dummyReturn><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></dummyReturn></ns2:dummyResponse></S:Body></S:Envelope>
```

| Ambiente | `ambiente` | `fecha` | `id` | Fuente |
|---|---|---|---|---|
| Homologación real | `Testing - FI1` | `2026-10-02T15:08:21` (sin milisegundos ni zona) | `WS-2.1.6` | [VIVO] |
| Manual, testing | `Testing - vii` | `2018-06-22T17:49:06.970-03:00` | (no trae) | [MAN] pág. 5 |
| Manual, producción | `Produccion - bus` | `2018-06-22T17:49:06.970` | `v1.0.0` | [MAN] pág. 5 |

El manual dice que el procesamiento del header "no es obligatorio" (pág. 5). Que el header aparezca también en respuestas con `arrayErrores`: **NO VERIFICADO** [INFERIDO que sí, porque el manual lo describe para "los mensajes de respuesta"].

### Comportamiento HTTP observado [VIVO, 2026-10-02]

| Caso | Request | Respuesta |
|---|---|---|
| `dummy` con Body vacío y `SOAPAction` `.../dummy` | `<soapenv:Body/>` | 200, SOAP correcto (arriba) |
| `dummy` con `<x:dummy/>` en el Body | el mensaje `dummyRequest` no tiene parts | **200 sin `Content-Type`, `HTTP/1.0`, `Content-Length: 39`, body de texto `BL<número> <aaaa-mm-dd hh:mm:ss> 500`** |
| `dummy` con Body vacío **sin** header `SOAPAction` | | `BL... 500`. Con el Body vacío el servidor sólo puede enrutar por `SOAPAction` |
| SOAP 1.2 (`application/soap+xml`, envelope 2003/05) | Body vacío | **200 `text/html; charset=utf-8`**, `Connection: close`, `Cache-Control: no-cache`: `<html><head><title></title>5794109047829095733</head><body><br><br></body></html>`. Es una página de bloqueo del firewall de aplicación con un id de soporte, no del servicio |
| Elemento de operación inexistente (`<x:noExisteRequest/>`) | | `BL... 500` |
| Token `abc`, token vacío, token XML con firma falsa, sin `authRequest` | `consultarTiposRetencionesRequest` | `BL... 500` |
| `cuitRepresentada` = `abc` (no numérico) | idem | `BL... 500` |
| `idCtaCte` con `codCtaCte` **e** `idFactura` (choice violado) y token `abc` | `consultarCtaCteRequest` | `BL... 500` |
| `consultarMontoObligadoRecepcionRequest` sin `fechaEmision` y token `abc` | | `BL... 500` (Content-Length 38: el número `BL` tiene largo variable) |
| `GET ?wsdl` | | 200, WSDL idéntico al guardado |

Interpretación [INFERIDO, evidencia fuerte, ver el brief del relevamiento]: el balanceador F5 reemplaza toda respuesta HTTP 500 del backend (un SOAP Fault) por `BL<n> <fecha> 500` con HTTP 200 y sin Content-Type. Como el token se valida antes de que se vea cualquier otra cosa, **no se pudo distinguir** si un error de esquema sale como Fault o como `arrayErroresFormato` (ver Autenticación y Validaciones).

### Discrepancias manual vs WSDL

Manda el WSDL para la estructura.

| # | Manual | WSDL / real | Se toma |
|---:|---|---|---|
| 1 | Respuesta de `dummy` con `<return>` (pág. 61) o sin envoltorio (ejemplo pág. 62) | `<dummyReturn>` | WSDL y [VIVO] |
| 2 | `dummy`: `authserver` = "Servidor de base de datos", `dbserver` = "Servidor de autenticacion" (pág. 61) | — | Errata: los nombres hablan solos |
| 3 | Estructura de errores de negocio con `<resultado>`, `<errores>` y `<observaciones>` (págs. 9-10) | `arrayErrores`, `arrayObservaciones` (o `arrayObservacion`, ver 4) | WSDL |
| 4 | `consultarCuentasEnAgtDptoCltv`: `arrayObservaciones` (pág. 44); `consultarFacturasAgtDptoCltv`: `arrayObservacion` (pág. 50) | Al revés: `consultarCuentasEnAgtDptoCltvReturn/arrayObservacion` y `consultarFacturasAgtDptoCltvReturn/arrayObservaciones`. `consultarObligadoRecepcion` y `consultarMontoObligadoRecepcion` usan `arrayObservacion` en los dos | WSDL |
| 5 | `consultarMontoObligadoRecepcion` devuelve `respuesta` (pág. 59) | `obligado` | WSDL |
| 6 | Hijo `consultarObligadosRecepcionReturn` / `consultarMontoObligadosRecepcionReturn` (págs. 46, 59) | `consultarObligadoRecepcionReturn` / `consultarMontoObligadoRecepcionReturn` | WSDL |
| 7 | `consultarCuentasEnAgtDptoCltv`: `arrayCuentasEnAgente` obligatorio "S" (pág. 44) | `minOccurs="0"` | WSDL |
| 8 | `CuentaEnAgenteType`: `cuitAgente` int, `idCuenta` int (pág. 65, tabla) | `CuitSimpleType` y `IdCuentaAgenteSimpleType` (string 3..20). La imagen del esquema de la misma página coincide con el WSDL | WSDL |
| 9 | `ConfirmarNotaDCType/acepta` short (pág. 75, tabla) | `SiNoSimpleType` (`S`/`N`); la imagen coincide con el WSDL | WSDL |
| 10 | `ComprobanteType/opcionTransferencia` obligatorio, `[1..1]` (págs. 68, 71) | `minOccurs="0"` | WSDL (en la práctica viene en facturas; ver Comportamiento) |
| 11 | `InfoTransferenciaType`: `infoAgtDptoCltv [1..*]` (imagen pág. 79) | `maxOccurs="1"` | WSDL |
| 12 | `InfoAgtDptoCltvType/idPagoAgDptoCltv` (tabla pág. 80) | `idPagoAgtDptoCltv` | WSDL |
| 13 | `CuentaCorrienteType` con `fechaHoraEstado` suelto y `codMotivoRechazo`/`descMotivoRechazo` (págs. 72-73) | `estadoCtaCte` es `EstadoCtaCteType` (`estado` + `fechaHoraEstado`); no existen `codMotivoRechazo` ni `descMotivoRechazo`. La imagen de pág. 72 coincide con el WSDL | WSDL. Los motivos de rechazo de la cuenta se leen en `factura/arrayMotivosRechazo` [INFERIDO] |
| 14 | Tabla de `ItemType` copiada de `OtroTributoType` (pág. 77) | 14 campos (`orden` ... `importeItem`); la imagen coincide con el WSDL | WSDL |
| 15 | Tabla de `ComprobanteType` con descripciones corridas una fila (pág. 70: `razonSocialEmi` = "Tipo de Comprobante") | — | Errata; las descripciones de abajo están reacomodadas |
| 16 | `PorcentajeSimpleType` "0.00 < P < 100.00" (pág. 63) | `minInclusive 0.0`, `maxInclusive 100.00` | WSDL (acepta 0 y 100) |
| 17 | `Texto250SimpleType` "Texto hasta 250 caracteres" (pág. 63) | `minLength 3`, `maxLength 250` | WSDL (menos de 3 caracteres es error de formato) |
| 18 | `CBUSimpleType` "Numérico de 22 caracteres" (pág. 63) | `string` con `length 22`, sin patrón numérico | WSDL para el formato; que sea numérico es regla de negocio [INFERIDO] |
| 19 | `NumeroSimpleType` (pág. 63) | No existe | Ignorar |
| 20 | Ejemplo de error de formato sobre `cuitTitularMercaderia` (pág. 7) | No es un campo de este servicio | El ejemplo vale como forma, no como contenido |
| 21 | Fault de ejemplo: `faultcode` `ns3: Receiver` (namespace SOAP 1.2) dentro de un envelope SOAP 1.1 (pág. 6) | — | Se documenta tal cual; ver Autenticación |
| 22 | Request de historial "es del tipo ConsultarHistorialEstadosComprobanteType" (pág. 55) | `ConsultarHistorialEstadosComprobanteRequestType` | WSDL (no afecta al XML) |
| 23 | `MotivoRechazoType/justificación` con tilde (pág. 76) | `justificacion` | WSDL |
| 24 | §2.4.1 lista `consultarCuentasComitente` | No existe | WSDL |

## Autenticación

Todas las operaciones salvo `dummy` llevan `authRequest` como **primer hijo** del elemento de request.

`AuthRequestType` [WSDL]; [MAN] págs. 16-17 y 64:

| Campo | Tipo XSD | Ocurrencia | Manual | Descripción |
|---|---|---|---|---|
| `token` | `xsd:string` | 1..1 | S, longitud variable (pág. 82) | Token del TA de WSAA |
| `sign` | `xsd:string` | 1..1 | S | Firma del TA |
| `cuitRepresentada` | `CuitSimpleType` (`long`, > 9999999999 y ≤ 99999999999) | 1..1 | S, long 11 | "CUIT de la Contribuyente representada o emisora" |

Reglas [MAN] pág. 17:

- "Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos."
- "De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional."
- TA de WSAA con `service` = `wsfecred`.

### Cómo informa la falla el servicio (manual)

"Errores excepcionales" = SOAP Fault [MAN] pág. 6. Ejemplo literal del manual:

```xml
<S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
  <S:Body>
    <ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:ns3="http://www.w3.org/2003/05/soap-envelope">
      <faultcode>ns3: Receiver</faultcode>
      <faultstring>[wscommon_007] La firma no corresponde al token enviado.</faultstring>
    </ns2:Fault>
  </S:Body>
</S:Envelope>
```

- `faultstring` "Describe al error que se generó al procesar la solicitud" [MAN] pág. 6.
- "Los errores excepcionales incluyen también errores de estructura (ej: tags sin cerrar, con nombres incorrectos o en orden incorrecto) y de tipos de datos" [MAN] pág. 6.
- El único texto de auth que da el manual es `[wscommon_007] La firma no corresponde al token enviado.`. Los textos para token vencido, token mal formado o CUIT no representada: **NO VERIFICADO**. El prefijo `[wscommon_NNN]` sugiere una biblioteca común de los servicios Java de ARCA [INFERIDO].
- `faultcode` real: **NO VERIFICADO**. El ejemplo trae `ns3: Receiver` con un espacio y con el namespace de SOAP 1.2. El catálogo (§4.1) dice que wsmtxca, del mismo dialecto, usa `soapenv:Client` según su manual.

### Cómo llega en la práctica [VIVO]

Toda falla de autenticación probada (token `abc`, token y sign vacíos, token XML bien formado con firma falsa, `authRequest` ausente) devuelve:

```
HTTP/1.0 200 OK
Connection: Keep-Alive
Content-Length: 39

BL9727904321499 2026-10-02 15:07:35 500
```

Sin `Content-Type`, sin SOAP. El número que sigue a `BL` cambia en cada llamada y tiene 12 o 13 dígitos. La fecha es hora local de Argentina con espacio.

**Para ArcaSim:** emitir el Fault del manual (HTTP 500, `text/xml`) como comportamiento por defecto, y ofrecer un modo "F5" que lo reemplace por `BL<n> <aaaa-mm-dd hh:mm:ss> 500` con HTTP/1.0 200 y sin Content-Type, que es lo que ve hoy un cliente real.

### Validaciones de `cuitRepresentada` (excluyentes)

[MAN] pág. 17, §2.3.1. Transporte (Fault o `arrayErrores`): **NO VERIFICADO**. Están bajo "Autenticación" pero tienen código numérico, como los errores de negocio; lo más probable es `arrayErrores` [INFERIDO].

| Código | Mensaje |
|---|---|
| 1000 | "LA CUIT NO SE ENCUENTRA ACTIVA EN EL PUC" |
| 1001 | "LA CUIT REGISTRA INCONVENIENTES. DEBERÁ DIRIGIRSE A LA DEPENDENCIA EN LA CUAL SE ENCUENTRA INSCRIPTO" |
| 1002 | "LA CUIT NO REGISTRA ALTA EN IVA o Monotributo o IVA Exento" |
| 1003 | "LA CUIT NO REGISTRA ALTA EN NINGUNA ACTIVIDAD" |
| 1004 | "LA CUIT NO SE ENCUENTRA CON ALTA EN EL DOMICILIO FISCAL ELECTRÓNICO O REGISTRA INCONVENIENTES EN EL MISMO. Sólo puede consultar en este sistema." |

El texto de 1004 ("Sólo puede consultar en este sistema") sugiere que sin DFE se rechazan las operaciones de escritura pero se permiten las consultas [INFERIDO].

## Operaciones

Convenciones: "XSD" = `minOccurs..maxOccurs` del WSDL; "Man." = columna "Oblig" del manual (S/N); "—" = el manual no lo dice. Todos los hijos van sin namespace. El manual no trae ejemplos XML de request ni de response, salvo `dummy` (págs. 61-62) y los fragmentos de error (págs. 6-11). Los ejemplos marcados **[DERIVADO]** se armaron desde el WSDL: la forma es exacta y los valores son ilustrativos.

### Tipos simples

| Tipo | Base XSD | Restricción XSD | Manual (pág. 63) |
|---|---|---|---|
| `CuitSimpleType` | `long` | `minExclusive 9999999999`, `maxInclusive 99999999999` (11 dígitos) | "Longitud 11" |
| `PuntoVentaSimpleType` | `int` | 1..99999 | 1..99999 |
| `NumeroComprobanteSimpleType` | `long` | 1..99999999 | 1..99999999 |
| `ImporteSimpleType` | `decimal` | -9999999999999.99..9999999999999.99, `totalDigits 15`, `fractionDigits 2` | 13 enteros + 2 decimales |
| `DecimalSimpleType` | `decimal` | 0..999999999999.999999, `totalDigits 18`, `fractionDigits 6` | 12 enteros + 6 decimales, "0 < d" |
| `PorcentajeSimpleType` | `decimal` | 0.0..100.00 (inclusive) | "0.00 < P < 100.00" |
| `Texto250SimpleType` | `string` | `minLength 3`, `maxLength 250` | "hasta 250 caracteres" |
| `CBUSimpleType` | `string` | `length 22` | "Numérico de 22 caracteres" |
| `IdCuentaAgenteSimpleType` | `string` | `minLength 3`, `maxLength 20` | (el manual dice `int`) |
| `SiNoSimpleType` | `string` | `length 1`; `S` \| `N` | S = Si, N = No |
| `RolSimpleType` | `string` | `Emisor` \| `Receptor` | |
| `ResultadoSimpleType` | `string` | `A` \| `O` \| `R` | A: Aprobado, O: Observado, R: Rechazado |
| `TipoCodAutorizacionType` | `string` | `A` \| `E` | **A = CAEA, E = CAE** |
| `TipoAceptacionSimpleType` | `string` | `Tacita` \| `Expresa` | |
| `TipoCancelacionSimpleType` | `string` | `PAR` \| `TOT` | PAR = Parcial, TOT = Total |
| `OpcionTransferenciaSimpleType` | `string` | `SCA` \| `ADC` | SCA = Sistema de Circulación Abierta, ADC = Agentes de Depósito Colectivo |
| `TipoFechaSimpleType` | `string` | `Emision` \| `PuestaDispo` \| `VenPago` \| `VenAcep` \| `Acep` \| `InfoAgDptoCltv` | Fechas de emisión, de puesta a disposición, de vencimiento de pago, de vencimiento de aceptación, de aceptación y de informe al agente |
| `EstadoCmpSimpleType` | `string` | `PendienteRecepcion` \| `Recepcionado` \| `Aceptado` \| `Rechazado` \| `InformadaAgDpto` | ídem |
| `EstadoCtaCteSimpleType` | `string` | `Modificable` \| `Aceptada` \| `Rechazada` \| `CanceladaTotal` \| `InformadaAgDpto` | ídem |

Formatos [MAN] pág. 82: `date` = `AAAA-MM-DD` "sin uso horario"; separador decimal `.`; redondeo **Round Half Even**; error absoluto = |calculado − real|, error relativo = absoluto / real (en valor absoluto). El manual no publica los márgenes de tolerancia: **NO VERIFICADO**.

### Tipos complejos compartidos

Todos son `sequence`: el orden de los hijos es obligatorio.

**`CodigoDescripcionType`** (dentro de `ArrayCodigosDescripcionesType`, con hijo `codigoDescripcion` 1..unbounded) [WSDL]; [MAN] pág. 64:

| Campo | Tipo | XSD | Man. |
|---|---|---|---|
| `codigo` | `short` | 1..1 | S |
| `descripcion` | `string` | 1..1 | S |

Se usa en `evento` (uno solo), `arrayObservaciones`, `arrayErrores`, `arrayFormasCancelacion` y en las tablas de parámetros. Como `codigo` es `short`, los códigos de este servicio caben en -32768..32767. Por eso el "sin resultados" es **32767** [INFERIDO].

**`CodigoDescripcionStringType`** (dentro de `ArrayCodigosDescripcionesStringType`, con hijo `codigoDescripcionString` 1..unbounded): `codigo` string 1..1, `descripcion` string 1..1. Sólo se usa en `arrayErroresFormato` [WSDL]; [MAN] págs. 6-7 y 64.

**`IdComprobanteType`** [WSDL]; [MAN] pág. 67:

| Campo | Tipo | XSD | Man. | Descripción |
|---|---|---|---|---|
| `CUITEmisor` | `CuitSimpleType` | 1..1 | S | CUIT emisora (ojo: `CUIT` en mayúsculas) |
| `codTipoCmp` | `short` | 1..1 | S | Tipo de comprobante (201..213) |
| `ptoVta` | `PuntoVentaSimpleType` | 1..1 | S | Punto de venta |
| `nroCmp` | `NumeroComprobanteSimpleType` | 1..1 | S | Número |

**`IdCtaCteType`**: `sequence` con un `choice` 1..1 entre dos opciones [WSDL]; [MAN] págs. 7-8 y 67:

| Campo | Tipo | Descripción |
|---|---|---|
| `codCtaCte` | `long` | "Código único asignado por este sistema a una Cta Cte de una FECRED" |
| `idFactura` | `IdComprobanteType` | "Id de la Factura que dio origen a esa Cta Cte" |

Va uno y sólo uno. Si se mandan los dos, sale el error de formato `cvc-complex-type.2.4.d` (pág. 8). Ejemplos del manual (pág. 8):

```xml
<idCtaCte><codCtaCte>1200</codCtaCte></idCtaCte>

<idCtaCte>
  <idFactura>
    <CUITEmisor>30999999999</CUITEmisor>
    <codTipoCmp>201</codTipoCmp>
    <ptoVta>1</ptoVta>
    <nroCmp>1</nroCmp>
  </idFactura>
</idCtaCte>
```

**`FiltroFechaType`** [WSDL]; [MAN] pág. 65: `tipo` `TipoFechaSimpleType` 1..1; `desde` `date` 1..1; `hasta` `date` 1..1. El rango máximo y la validación de `desde` ≤ `hasta` son **NO VERIFICADO**: el manual no da códigos.

**`EstadoCmpType`** [MAN] pág. 77: `estado` `EstadoCmpSimpleType` 1..1; `fechaHoraEstado` `dateTime` 1..1 ("Fecha y hora desde la cual está en ese estado").

**`EstadoCtaCteType`** [MAN] pág. 78: `estado` `EstadoCtaCteSimpleType` 1..1; `fechaHoraEstado` `dateTime` 1..1.

**`MotivoRechazoType`** (dentro de `ArrayMotivosRechazoType`, con hijo `motivoRechazo` 1..unbounded) [MAN] pág. 76:

| Campo | Tipo | XSD | Man. | Descripción |
|---|---|---|---|---|
| `codMotivo` | `short` | 1..1 | S | Código del motivo normado (`consultarTiposMotivosRechazo`) |
| `descMotivo` | `Texto250SimpleType` | 1..1 | S | Descripción del motivo normado |
| `justificacion` | `Texto250SimpleType` | 1..1 | S | Justificación del rechazo |

**`RetencionType`** (dentro de `ArrayRetencionesType`, con hijo `retencion` 1..unbounded) [MAN] pág. 74:

| Campo | Tipo | XSD | Man. | Descripción |
|---|---|---|---|---|
| `codTipo` | `short` | 1..1 | S | Tipo de retención (el `codigoJurisdiccion` de `consultarTiposRetenciones` [INFERIDO]) |
| `importe` | `ImporteSimpleType` | 1..1 | S | Importe retenido |
| `porcentaje` | `PorcentajeSimpleType` | 1..1 | S | Porcentaje aplicado |
| `descMotivo` | `Texto250SimpleType` | 0..1 | N | Motivo "por el cual aplica un porcentaje de retención distinto al de tabla" |

**`AjusteOperacionType`** (dentro de `ArrayAjustesOperacionType`, con hijo `ajuste` 1..unbounded) [MAN] pág. 78: `codigo` `short` 1..1 (de `consultarTiposAjustesOperacion`); `importe` `ImporteSimpleType` 1..1.

**`ConfirmarNotaDCType`** (dentro de `ArrayConfirmarNotasType`, con hijo `confirmarNota` 1..unbounded) [WSDL]; [MAN] pág. 75: `acepta` `SiNoSimpleType` 1..1; `idNota` `IdComprobanteType` 1..1.

**`CuentaEnAgenteType`** (dentro de `ArrayCuentasEnAgenteType`, con hijo `cuentaEnAgente` 1..unbounded) [WSDL]; [MAN] pág. 65:

| Campo | Tipo | XSD | Man. | Descripción |
|---|---|---|---|---|
| `cuitAgente` | `CuitSimpleType` | 1..1 | S | CUIT del Agente de Depósito Colectivo |
| `razonSocialAgente` | `string` | 0..1 | N | "El sistema envía la razón social del Agente" (sólo salida) |
| `idCuenta` | `IdCuentaAgenteSimpleType` | 1..1 | S | Identificador de la cuenta que da el agente |
| `denominacion` | `Texto250SimpleType` | 0..1 | N | Denominación "(usada como dato de salida de este sistema)" |

**`InfoTransferenciaType`**: `sequence` con un `choice` 1..1 entre `infoAgtDptoCltv` (`InfoAgtDptoCltvType`) e `infoSCA` (`InfoSCAType`) [WSDL]; [MAN] pág. 79.

**`InfoAgtDptoCltvType`** [WSDL]; [MAN] págs. 79-80:

| Campo | Tipo | XSD | Man. | Descripción |
|---|---|---|---|---|
| `fechaInfo` | `date` | 1..1 | S | Fecha en que se pidió el informe al agente |
| `ctaAgente` | `CuentaEnAgenteType` | 1..1 | S | Cuenta elegida por el vendedor |
| `recibida` | `SiNoSimpleType` | 1..1 | S | Si el agente la recibió efectivamente |
| `fechaLectura` | `date` | 0..1 | N | Fecha en que el agente tomó la información de AFIP |
| `fechaRecep` | `date` | 0..1 | N | Fecha de recepción que informó el agente |
| `aceptada` | `SiNoSimpleType` | 0..1 | N | Si el agente la aceptó o la rechazó "por algún inconveniente" |
| `motivoRechazo` | `string` | 0..1 | N | Motivo que dio el agente si no la aceptó |
| `idPagoAgtDptoCltv` | `string` | 0..1 | N | Id del pago para el agente, si la factura está informada y aceptada |
| `CBUAgtDptoCltv` | `CBUSimpleType` | 0..1 | N | CBU del agente donde hay que pagar |

**`InfoSCAType`** [WSDL]; [MAN] págs. 80-81:

| Campo | Tipo | XSD | Man. | Descripción |
|---|---|---|---|---|
| `fechaAceptacionFactura` | `date` | 1..1 | S | Fecha en que se aceptó y quedó a disposición del SCA |
| `informaCBUReceptor` | `SiNoSimpleType` | 1..1 | S | Si el receptor indicó una CBU al aceptar |
| `CBUReceptor` | `CBUSimpleType` | 0..1 | N | La CBU indicada |
| `CBUValidada` | `SiNoSimpleType` | 0..1 | N | Si se validó online contra el BCRA al aceptar |
| `fechaLecturaSCA` | `dateTime` | 0..1 | N | Cuándo consultó el SCA la factura en AFIP |

**`SubtotalIVAType`** (dentro de `ArraySubtotalesIVAType`, con hijo `subtotalIVA` 1..unbounded) [MAN] pág. 76: `codigo` `short` 1..1 (tipo de IVA); `baseImponible` `ImporteSimpleType` 1..1; `importe` `ImporteSimpleType` 1..1.

**`OtroTributoType`** (dentro de `ArrayOtrosTributosType`, con hijo `otroTributo` 1..unbounded) [MAN] pág. 76: `codigo` `short` 1..1; `detalle` `Texto250SimpleType` 0..1; `baseImponible` `ImporteSimpleType` 1..1; `importe` `ImporteSimpleType` 1..1.

**`ItemType`** (dentro de `ArrayItemsType`, con hijo `item` 1..unbounded). Sólo aparece en comprobantes emitidos por Comprobantes en línea y por el web service con detalle de ítems (wsmtxca, RG 2904) [MAN] págs. 71 y 85. La estructura sale del [WSDL], porque la tabla del manual está mal (Discrepancias #14). El manual no describe los campos:

| # | Campo | Tipo | XSD |
|---:|---|---|---|
| 1 | `orden` | `int` | 1..1 |
| 2 | `unidadesMtx` | `int` | 0..1 |
| 3 | `codigoMtx` | `string` | 0..1 |
| 4 | `codigo` | `string` | 0..1 |
| 5 | `descripcion` | `string` | 1..1 |
| 6 | `codNomMercosur` | `string` | 0..1 |
| 7 | `cantidad` | `DecimalSimpleType` | 0..1 |
| 8 | `codigoUnidadMedida` | `short` | 0..1 |
| 9 | `precioUnitario` | `ImporteSimpleType` | 0..1 |
| 10 | `importeBonificacion` | `ImporteSimpleType` | 0..1 |
| 11 | `codigoCondicionIVA` | `short` | 1..1 |
| 12 | `importeIVA` | `ImporteSimpleType` | 0..1 |
| 13 | `importeItem` | `ImporteSimpleType` | 1..1 |

Los nombres coinciden con el ítem de wsmtxca [INFERIDO], y su significado está en ese manual.

**`ArrayTexto250SimpleType`**: hijo `texto` (`Texto250SimpleType`) 1..unbounded.

**`ComprobanteType`** (dentro de `ArrayComprobantesType`, con hijo `comprobante` 1..unbounded) [WSDL]; [MAN] págs. 68-71. Es la vista de un comprobante FCE: factura, nota de débito o nota de crédito. La columna "Origen en wsfev1" es [INFERIDO] y sale de `docs/arca/wsfev1.md` §3.3 y §7.7:

| # | Campo | Tipo | XSD | Man. | Descripción | Origen en wsfev1 [INFERIDO] |
|---:|---|---|---|---|---|---|
| 1 | `cuitEmisor` | `CuitSimpleType` | 1..1 | S | CUIT emisora | `Auth/Cuit` |
| 2 | `razonSocialEmi` | `string` | 1..1 | S | Razón social del emisor | padrón |
| 3 | `codTipoCmp` | `short` | 1..1 | S | Tipo de comprobante | `CbteTipo` |
| 4 | `ptovta` | `PuntoVentaSimpleType` | 1..1 | S | Punto de venta (**`ptovta` en minúscula**, distinto de `IdComprobanteType/ptoVta`) | `PtoVta` |
| 5 | `nroCmp` | `NumeroComprobanteSimpleType` | 1..1 | S | Número | `CbteDesde` |
| 6 | `cuitReceptor` | `CuitSimpleType` | 1..1 | S | CUIT receptora | `DocNro` (DocTipo 80) |
| 7 | `razonSocialRecep` | `string` | 1..1 | S | Razón social del receptor | padrón |
| 8 | `tipoCodAuto` | `TipoCodAutorizacionType` | 1..1 | S | `E` = CAE, `A` = CAEA | método usado |
| 9 | `codAutorizacion` | `long` | 1..1 | S | CAE o CAEA | `CAE` / `CAEA` |
| 10 | `fechaEmision` | `date` | 1..1 | S | Fecha de emisión | `CbteFch` |
| 11 | `fechaPuestaDispo` | `date` | 0..1 | N | "Fecha en la que se puso a disposición en el sistema de gestión" | — |
| 12 | `fechaVenPago` | `date` | 0..1 | N | Vencimiento del pago | `FchVtoPago` |
| 13 | `fechaVenAcep` | `date` | 0..1 | N | Vencimiento del plazo de aceptación | — (lo calcula ARCA) |
| 14 | `importeTotal` | `ImporteSimpleType` | 1..1 | S | Importe total | `ImpTotal` |
| 15 | `codMoneda` | `string` | 1..1 | S | Moneda | `MonId` |
| 16 | `cotizacionMoneda` | `decimal` | 1..1 | S | Cotización | `MonCotiz` |
| 17 | `CBUEmisor` | `CBUSimpleType` | 0..1 | N | CBU del emisor | opcional 2101 |
| 18 | `AliasEmisor` | `Texto250SimpleType` | 0..1 | N | Alias de la CBU | opcional 2102 |
| 19 | `esAnulacion` | `SiNoSimpleType` | 0..1 | N | En ND/NC, si es de anulación | opcional 22 |
| 20 | `esPostAceptacion` | `SiNoSimpleType` | 0..1 | N | En ND/NC, si se emitió después de la aceptación o del rechazo de la factura | lo calcula ARCA |
| 21 | `idComprobanteAsociado` | `IdComprobanteType` | 0..1 | N | En ND/NC, el comprobante asociado | `CbtesAsoc` |
| 22 | `referenciasComerciales` | `ArrayTexto250SimpleType` | 0..1 | N | Texto libre del emisor | opcional 23 |
| 23 | `arraySubtotalesIVA` | `ArraySubtotalesIVAType` | 0..1 | N | Subtotales de IVA | `Iva/AlicIva` |
| 24 | `arrayOtrosTributos` | `ArrayOtrosTributosType` | 0..1 | N | Otros tributos | `Tributos/Tributo` |
| 25 | `arrayItems` | `ArrayItemsType` | 0..1 | N | Ítems (Comprobantes en línea y RG 2904) | wsmtxca |
| 26 | `datosGenerales` | `string` | 0..1 | N | Datos generales del emisor | — |
| 27 | `datosComerciales` | `string` | 0..1 | N | Datos comerciales del emisor | — |
| 28 | `leyendaComercial` | `Texto250SimpleType` | 0..1 | N | Leyenda comercial | — |
| 29 | `codCtaCte` | `long` | 1..1 | S | "Codigo de la Cuenta Corriente que integra este comprobante" | lo asigna ARCA |
| 30 | `estado` | `EstadoCmpType` | 1..1 | S | Estado actual y desde cuándo | — |
| 31 | `tipoAcep` | `TipoAceptacionSimpleType` | 0..1 | N | `Tacita` o `Expresa` | — |
| 32 | `fechaHoraAcep` | `dateTime` | 0..1 | N | "Fecha y hora de la aceptación o rechazo" | — |
| 33 | `arrayMotivosRechazo` | `ArrayMotivosRechazoType` | 0..1 | N | Motivos, si fue rechazado | — |
| 34 | `opcionTransferencia` | `OpcionTransferenciaSimpleType` | 0..1 (manual: S) | S | "Para las Facturas, la opción de transferencia seleccionada" | opcional 27 |
| 35 | `infoTransferencia` | `InfoTransferenciaType` | 0..1 | N | Datos de la transferencia al ADC o al SCA | — |

**`CuentaCorrienteType`** [WSDL]; [MAN] págs. 72-73:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `codCtaCte` | `long` | 1..1 | S | Código de la cuenta |
| 2 | `estadoCtaCte` | `EstadoCtaCteType` | 1..1 | S | Estado y "fecha y hora desde la cual la cta.cte se encuentra en ese estado" |
| 3 | `factura` | `ComprobanteType` | 1..1 | S | "Comprobante que dio origen a la Cta.Cte" |
| 4 | `arrayNotasDCAsociadas` | `ArrayComprobantesType` | 0..1 | N | "Listado de Comprobantes que intervienen en el cálculo del saldo" |
| 5 | `arrayFormasCancelacion` | `ArrayCodigosDescripcionesType` | 0..1 | N | Formas de cancelación informadas |
| 6 | `arrayRetenciones` | `ArrayRetencionesType` | 0..1 | N | Retenciones informadas |
| 7 | `arrayAjustesOperacion` | `ArrayAjustesOperacionType` | 0..1 | N | Ajustes informados al aceptar |
| 8 | `importeInicial` | `ImporteSimpleType` | 1..1 | S | "Importe Inicial que se toma de la factura de crédito" |
| 9 | `importeTotalNotasDC` | `ImporteSimpleType` | 0..1 | N | "Importe total de la suma de todas las notas de débito y crédito" |
| 10 | `importeCancelado` | `ImporteSimpleType` | 0..1 | N | Importe cancelado por el comprador |
| 11 | `importeTotalRetPesos` | `ImporteSimpleType` | 0..1 | N | Total de retenciones en pesos |
| 12 | `importeEmbargoPesos` | `ImporteSimpleType` | 0..1 | N | Embargo en pesos |
| 13 | `saldoAceptado` | `ImporteSimpleType` | 0..1 | N | "Saldo aceptado por el Comprador" |
| 14 | `saldo` | `ImporteSimpleType` | 1..1 | S | "Saldo de la operación comercial: Importe de Factura + Importe de las Notas de Débito (No rechazadas) – Importe de las Notas de Crédito (No rechazadas). Ignora montos de cancelación, embargos, retenciones, etc. informados en la aceptación" |
| 15 | `codMoneda` | `string` | 1..1 | S | "Misma moneda que los comprobantes que forman la Cta.Cte" |
| 16 | `cotizacionMonedaUlt` | `decimal` | 1..1 | S | "Última cotización informada en el último comprobante asociado a la Cta.Cte" |

**`InfoCtaCteType`** (dentro de `ArrayInfosCtaCteType`, con hijo `infoCtaCte` 1..unbounded) [WSDL]; [MAN] pág. 66:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `codCtaCte` | `long` | 1..1 | S | "Campo necesario para consultas puntuales ... mediante el método consultarCtaCte" |
| 2 | `estadoCtaCte` | `EstadoCtaCteType` | 1..1 | S | Estado actual. El manual dice `EstadoCtaCteSimpleType`; el WSDL usa el tipo complejo, con fecha |
| 3 | `idFacturaCredito` | `IdComprobanteType` | 1..1 | S | Factura de la cuenta |
| 4 | `importeTotalFC` | `ImporteSimpleType` | 1..1 | S | Importe total de la factura |
| 5 | `saldo` | `ImporteSimpleType` | 1..1 | S | Saldo actual |
| 6 | `saldoAceptado` | `ImporteSimpleType` | 0..1 | N | "Saldo aceptado (si fue aceptada)" |
| 7 | `codMoneda` | `string` | 1..1 | S | Moneda de la factura |
| 8 | `opcionTransferencia` | `OpcionTransferenciaSimpleType` | 1..1 | S | Opción vigente |

**`OperacionFECredReturnType`**: es la respuesta de las 5 operaciones que escriben sobre cuentas (`aceptarFECred`, `rechazarFECred`, `informarCancelacionTotalFECred`, `informarFacturaAgtDptoCltv` y `modificarOpcionTransferencia`). Siempre va dentro de `operacionFECredReturn` [WSDL]; [MAN] págs. 23, 27, 31, 33 y 35:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `resultado` | `ResultadoSimpleType` | 1..1 | S | A / O / R |
| 2 | `idCtaCte` | `IdCtaCteType` | 1..1 | S | "Identificación de Cta.Cte de la FECRED operada" |
| 3 | `evento` | `CodigoDescripcionType` | 0..1 | N | "un anuncio informativo del sistema" |
| 4 | `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | N | Motivos de observación |
| 5 | `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | N | Motivos del rechazo |
| 6 | `arrayErroresFormato` | `ArrayCodigosDescripcionesStringType` | 0..1 | N | Errores de formato |

`idCtaCte` es obligatorio aun cuando hay error. No se sabe si el servicio devuelve el mismo identificador que recibió (`codCtaCte` o `idFactura`) o siempre el `codCtaCte`: **NO VERIFICADO** [INFERIDO: devuelve lo que recibió].

**`ConsultarCodigoDescripcionReturnType`** es la respuesta de motivos de rechazo, formas de cancelación y ajustes, dentro de `codigoDescripcionReturn`: `arrayCodigoDescripcion` `ArrayCodigosDescripcionesType` 0..1 y `arrayErroresFormato` 0..1 [WSDL]. No tiene `arrayErrores`.

**`ConsultarCodigoDescripcionRequestType`** es el request de las 4 tablas de parámetros: sólo lleva `authRequest` 1..1.

El WSDL define además `ConsultarCodigoDescripcionStringResponseType`, `ConsultarCodigoDescripcionStringReturnType` y el grupo vacío `NewGroupDefinition`. **Ninguna operación los usa.**

### 1. dummy

Propósito: "Permite verificar el funcionamiento del presente WS" [MAN] pág. 61; "Metodo dummy." [WSDL]. No requiere autenticación.

Request: **Body vacío** (el mensaje `dummyRequest` no tiene `part`) más el header `SOAPAction: "http://ar.gob.afip.wsfecred/FECredService/dummy"`. El ejemplo del manual (pág. 62) es el que funciona:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
  <soapenv:Header/>
  <soapenv:Body/>
</soapenv:Envelope>
```

Con `<x:dummy/>` dentro del Body, o sin el header `SOAPAction`, responde `BL... 500` [VIVO].

Response `dummyResponse/dummyReturn` (`DummyReturnType`): `appserver`, `authserver` y `dbserver`, los tres `string` 1..1 [WSDL]. El ejemplo real está en Contrato. Qué valores salen cuando algo falla, en lugar de `OK`: **NO VERIFICADO**.

### 2. consultarComprobantes

Propósito: "obtener información sobre los comprobantes Emitidos y Recibidos. Debe indicar el rol de la CUIT Representada, Emisor o Receptor", con filtros opcionales [MAN] pág. 36. Tiene paginado desde la v1.3.0: se piden las páginas 1, 2, 3... "manteniendo los mismos filtros entre los consecutivos request hasta que el servicio le responda que "NO hay mas" comprobantes. El tamaño de página será manejado por el servicio de manera dinámica" [MAN] pág. 36.

Request `consultarComprobantesRequest` (`ConsultarComprobanteRequestType`) [WSDL]; [MAN] pág. 37:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `authRequest` | `AuthRequestType` | 1..1 | S | |
| 2 | `rolCUITRepresentada` | `RolSimpleType` | 1..1 | S | `Emisor` o `Receptor`: el rol de `cuitRepresentada` en los comprobantes que se buscan |
| 3 | `CUITContraparte` | `CuitSimpleType` | 0..1 | N | Si el rol es Emisor, filtra por receptor; si es Receptor, por emisor |
| 4 | `codTipoCmp` | `short` | 0..1 | N | Tipo de comprobante |
| 5 | `estadoCmp` | `EstadoCmpSimpleType` | 0..1 | N | Estado del comprobante |
| 6 | `fecha` | `FiltroFechaType` | 0..1 | N | Tipo de fecha y rango |
| 7 | `codCtaCte` | `long` | 0..1 | N | Sólo los comprobantes de esa cuenta. "Si ingresa el valor 0 la búsqueda no arrojará resultados" |
| 8 | `estadoCtaCte` | `EstadoCtaCteSimpleType` | 0..1 | N | Estado de la cuenta a la que pertenecen |
| 9 | `nroPagina` | `short` | 0..1 | N | "(Opcional) Numero de página solicitada" |

Response `consultarComprobantesResponse/consultarCmpReturn` (`ConsultarCmpReturnType`) [WSDL]; [MAN] pág. 38:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `arrayComprobantes` | `ArrayComprobantesType` | 0..1 | N | Comprobantes que cumplen los filtros |
| 2 | `nroPagina` | `short` | 0..1 | N | Página a la que pertenecen |
| 3 | `hayMas` | `SiNoSimpleType` | 0..1 | N | Si hay más páginas |
| 4 | `evento` | `CodigoDescripcionType` | 0..1 | N | |
| 5 | `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | N | 32767 si no hay resultados |
| 6 | `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | N | |
| 7 | `arrayErroresFormato` | `ArrayCodigosDescripcionesStringType` | 0..1 | N | |

Validaciones: 12011 (`nroPagina` ≤ 0) [MAN] pág. 38; observación 32767 cuando no hay resultados (pág. 38); además las comunes 1100-1108 y 10000 (págs. 18-19).

Ejemplo [DERIVADO]:

```xml
<x:consultarComprobantesRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>30999999999</cuitRepresentada></authRequest>
  <rolCUITRepresentada>Receptor</rolCUITRepresentada>
  <estadoCmp>Recepcionado</estadoCmp>
  <fecha><tipo>Emision</tipo><desde>2026-09-01</desde><hasta>2026-09-30</hasta></fecha>
  <nroPagina>1</nroPagina>
</x:consultarComprobantesRequest>
```
```xml
<ns2:consultarComprobantesResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <consultarCmpReturn>
    <arrayComprobantes>
      <comprobante>
        <cuitEmisor>20111111112</cuitEmisor><razonSocialEmi>EMISOR SA</razonSocialEmi>
        <codTipoCmp>201</codTipoCmp><ptovta>1</ptovta><nroCmp>15</nroCmp>
        <cuitReceptor>30999999999</cuitReceptor><razonSocialRecep>RECEPTOR SA</razonSocialRecep>
        <tipoCodAuto>E</tipoCodAuto><codAutorizacion>76123456789012</codAutorizacion>
        <fechaEmision>2026-09-10</fechaEmision><fechaPuestaDispo>2026-09-10</fechaPuestaDispo>
        <fechaVenPago>2026-10-30</fechaVenPago><fechaVenAcep>2026-10-10</fechaVenAcep>
        <importeTotal>6000000.00</importeTotal><codMoneda>PES</codMoneda><cotizacionMoneda>1</cotizacionMoneda>
        <CBUEmisor>0000000000000000000000</CBUEmisor>
        <codCtaCte>1200</codCtaCte>
        <estado><estado>Recepcionado</estado><fechaHoraEstado>2026-09-12T00:00:00</fechaHoraEstado></estado>
        <opcionTransferencia>ADC</opcionTransferencia>
      </comprobante>
    </arrayComprobantes>
    <nroPagina>1</nroPagina>
    <hayMas>N</hayMas>
  </consultarCmpReturn>
</ns2:consultarComprobantesResponse>
```

### 3. rechazarNotaDC

Propósito: "Método que permite al Comprador rechazar Notas de Débito / Crédito individualmente mientras la Factura de Crédito no haya sido Aceptada o Rechazada. Al rechazarla no afectará a la Cta Cte. Debe indicar al menos un motivo de rechazo y justificarlo" [MAN] pág. 28. El WSDL agrega: "para desafectarlas del cálculo del saldo de la Cta. Cte. vinculada".

Request `rechazarNotaDCRequest` (`RechazarNotaDCRequestType`) [WSDL]; [MAN] pág. 28:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `authRequest` | `AuthRequestType` | 1..1 | S | |
| 2 | `idComprobante` | `IdComprobanteType` | 1..1 | S | La ND o NC que se rechaza |
| 3 | `arrayMotivosRechazo` | `ArrayMotivosRechazoType` | 1..1 | S | Al menos un motivo con justificación |

Response `rechazarNotaDCResponse/rechazarNotaDCReturn` (`RechazarNotaDCReturnType`) [WSDL]; [MAN] pág. 29:

| # | Campo | Tipo | XSD | Man. |
|---:|---|---|---|---|
| 1 | `idComprobante` | `IdComprobanteType` | 1..1 | S |
| 2 | `resultado` | `ResultadoSimpleType` | 1..1 | S |
| 3 | `evento` | `CodigoDescripcionType` | 0..1 | N |
| 4 | `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | N |
| 5 | `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | N |
| 6 | `arrayErroresFormato` | `ArrayCodigosDescripcionesStringType` | 0..1 | N |

Validaciones [MAN] pág. 29: 5000 (el rechazo deja el saldo en cero o negativo), 15000 (el estado del comprobante no lo permite), 3000 (falta la justificación), 3001 (motivo inválido o repetido). Las comunes 1105 a 1108 también aplicarían [INFERIDO].

Ejemplo [DERIVADO]:

```xml
<x:rechazarNotaDCRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>30999999999</cuitRepresentada></authRequest>
  <idComprobante><CUITEmisor>20111111112</CUITEmisor><codTipoCmp>202</codTipoCmp><ptoVta>1</ptoVta><nroCmp>3</nroCmp></idComprobante>
  <arrayMotivosRechazo>
    <motivoRechazo><codMotivo>1</codMotivo><descMotivo>Descripción del motivo</descMotivo><justificacion>El débito no corresponde</justificacion></motivoRechazo>
  </arrayMotivosRechazo>
</x:rechazarNotaDCRequest>
```
```xml
<ns2:rechazarNotaDCResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <rechazarNotaDCReturn>
    <idComprobante><CUITEmisor>20111111112</CUITEmisor><codTipoCmp>202</codTipoCmp><ptoVta>1</ptoVta><nroCmp>3</nroCmp></idComprobante>
    <resultado>A</resultado>
  </rechazarNotaDCReturn>
</ns2:rechazarNotaDCResponse>
```

### 4. consultarCtasCtes

Propósito: "obtener las cuentas corrientes que fueron generadas a partir de la facturación, que coinciden con los parámetros de búsqueda" [MAN] pág. 39. Pagina igual que `consultarComprobantes`.

Request `consultarCtasCtesRequest` (`ConsultarCtasCtesRequestType`) [WSDL]; [MAN] págs. 39-40:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `authRequest` | `AuthRequestType` | 1..1 | S | |
| 2 | `rolCUITRepresentada` | `RolSimpleType` | 1..1 | S | Rol de `cuitRepresentada` en las cuentas |
| 3 | `CUITContraparte` | `CuitSimpleType` | 0..1 | N | La CUIT que ocupa el rol opuesto |
| 4 | `fecha` | `FiltroFechaType` | 0..1 | N | Filtro de fechas |
| 5 | `estadoCtaCte` | `EstadoCtaCteSimpleType` | 0..1 | N | Estado de las cuentas |
| 6 | `nroPagina` | `short` | 0..1 | N | Página |
| 7 | `opcionTransferencia` | `OpcionTransferenciaSimpleType` | 0..1 | N | Opción vigente de la factura (desde 2.0.0) |

Response `consultarCtasCtesResponse/consultarCtasCtesReturn` (`ConsultarCtasCtesReturnType`) [WSDL]; [MAN] págs. 40-41: `arrayInfosCtaCte` (`ArrayInfosCtaCteType`, 0..1), `nroPagina` (short 0..1), `hayMas` (SiNo 0..1), `evento` 0..1, `arrayObservaciones` 0..1, `arrayErrores` 0..1, `arrayErroresFormato` 0..1.

Validaciones [MAN] pág. 41: 12011, observación 32767 y las comunes.

Ejemplo de response [DERIVADO]. Los valores salen del docstring de [PYAF]: cuenta 2561, factura 201, PES 12850000, estado Modificable.

```xml
<ns2:consultarCtasCtesResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <consultarCtasCtesReturn>
    <arrayInfosCtaCte>
      <infoCtaCte>
        <codCtaCte>2561</codCtaCte>
        <estadoCtaCte><estado>Modificable</estado><fechaHoraEstado>2019-05-13T09:25:32</fechaHoraEstado></estadoCtaCte>
        <idFacturaCredito><CUITEmisor>20267565393</CUITEmisor><codTipoCmp>201</codTipoCmp><ptoVta>999</ptoVta><nroCmp>22</nroCmp></idFacturaCredito>
        <importeTotalFC>12850000</importeTotalFC>
        <saldo>12850000</saldo>
        <codMoneda>PES</codMoneda>
        <opcionTransferencia>ADC</opcionTransferencia>
      </infoCtaCte>
    </arrayInfosCtaCte>
    <nroPagina>1</nroPagina>
    <hayMas>N</hayMas>
  </consultarCtasCtesReturn>
</ns2:consultarCtasCtesResponse>
```

[PYAF] muestra `saldo_aceptado` = 0 en una cuenta Modificable. No se sabe si el servicio manda `<saldoAceptado>0</saldoAceptado>` o lo omite: **NO VERIFICADO**. `opcionTransferencia` no figura en ese docstring porque es anterior a la 2.0.0.

### 5. consultarCtaCte

Propósito: "obtener el detalle y composición de una Cuenta Corriente de una Factura Electrónica de Crédito" [MAN] pág. 42.

Request `consultarCtaCteRequest` (`ConsultarCtaCteRequestType`): `authRequest` 1..1; `idCtaCte` (`IdCtaCteType`) 1..1 [WSDL]; [MAN] pág. 42.

Response `consultarCtaCteResponse/consultarCtaCteReturn` (`ConsultarCtaCteReturnType`) [WSDL]; [MAN] pág. 43: `ctaCte` (`CuentaCorrienteType`, 0..1), `evento` 0..1, `arrayObservaciones` 0..1, `arrayErrores` 0..1, `arrayErroresFormato` 0..1.

Validaciones: las comunes. Cuenta inexistente → 1102 [INFERIDO]; cuenta de terceros → 1100 [INFERIDO].

Ejemplo de response [DERIVADO]:

```xml
<ns2:consultarCtaCteResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <consultarCtaCteReturn>
    <ctaCte>
      <codCtaCte>1200</codCtaCte>
      <estadoCtaCte><estado>Modificable</estado><fechaHoraEstado>2026-09-10T10:00:00</fechaHoraEstado></estadoCtaCte>
      <factura>…ComprobanteType…</factura>
      <arrayNotasDCAsociadas><comprobante>…ND 202…</comprobante></arrayNotasDCAsociadas>
      <importeInicial>6000000.00</importeInicial>
      <importeTotalNotasDC>100000.00</importeTotalNotasDC>
      <saldo>6100000.00</saldo>
      <codMoneda>PES</codMoneda>
      <cotizacionMonedaUlt>1</cotizacionMonedaUlt>
    </ctaCte>
  </consultarCtaCteReturn>
</ns2:consultarCtaCteResponse>
```

### 6. informarCancelacionTotalFECred

Propósito: "el Comprador, dentro los plazos establecidos, habiendo aceptado previamente la FECRED, informa que ha cancelado (pagado) totalmente la deuda al vendedor, debiendo indicar la forma de cancelación". Si la cancelación total ya se informó en `aceptarFECred`, "NO debe informarse en este método". "Este método no es válido para aquella facturas aceptadas con opción de transferencia "Sistema de Circulación Abierta"" [MAN] pág. 32. El WSDL dice: "Solo puede cancelar las aceptadas dentros de los plazos establecidos".

Request `informarCancelacionTotalFECredRequest` (`InformarCancelacionTotalFECredRequestType`) [WSDL]; [MAN] pág. 32:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `authRequest` | `AuthRequestType` | 1..1 | S | |
| 2 | `idCtaCte` | `IdCtaCteType` | 1..1 | S | |
| 3 | `arrayFormasCancelacion` | `ArrayCodigosDescripcionesType` | 1..1 | S | Una o varias formas (`consultarTiposFormasCancelacion`) |
| 4 | `importeCancelacion` | `ImporteSimpleType` | 1..1 | S | Importe cancelado. Se llama **`importeCancelacion`**, no `importeCancelado` como en `aceptarFECred` |

Response: `OperacionFECredReturnType` dentro de `operacionFECredReturn`.

Validaciones [MAN] pág. 33: 4000 (no cancela totalmente el saldo), 4001 (falta la forma), 4002 (forma inválida o repetida); además las comunes (1107 fuera de plazo, 1108 estado). Si 4003 también aplica acá: **NO VERIFICADO**, porque sólo figura en `aceptarFECred`.

Ejemplo [DERIVADO]:

```xml
<x:informarCancelacionTotalFECredRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>30999999999</cuitRepresentada></authRequest>
  <idCtaCte><codCtaCte>1200</codCtaCte></idCtaCte>
  <arrayFormasCancelacion><codigoDescripcion><codigo>1</codigo><descripcion>Transferencia bancaria</descripcion></codigoDescripcion></arrayFormasCancelacion>
  <importeCancelacion>6100000.00</importeCancelacion>
</x:informarCancelacionTotalFECredRequest>
```
```xml
<ns2:informarCancelacionTotalFECredResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <operacionFECredReturn><resultado>A</resultado><idCtaCte><codCtaCte>1200</codCtaCte></idCtaCte></operacionFECredReturn>
</ns2:informarCancelacionTotalFECredResponse>
```

El código y la descripción de la forma de cancelación del ejemplo son inventados. La tabla real es **NO VERIFICADO**.

### 7. aceptarFECred

Propósito [MAN] pág. 20. El comprador puede hacer dos cosas:

- **a) Aceptar** la factura, "pudiendo además informar la cancelación parcial, ajustes, retenciones y/o embargos". Aceptar la factura "implica la aceptación de todos aquellos comprobantes asociados que modificaron el saldo de la Cuenta Corriente y no fueron rechazados". Por web service "se solicita que se confirme la aceptación o rechazo de cada uno de los comprobantes asociados (ND o NC)" y que se indique "el saldo resultante negociable". Si sale bien, la factura y la cuenta quedan **Aceptadas** con el saldo negociable calculado.
  - Opción **SCA**: el receptor "optará por informar un CBU Pagador". La factura, con su saldo aceptado, queda a disposición del SCA, y el comprador "ya NO podrá informar la cancelación total en este sistema".
  - Opción **ADC**: mientras no venza el plazo de aceptación, el comprador puede informar la cancelación total, siempre que el vendedor no haya pedido el informe al agente. El vendedor puede informarla al agente mientras no esté cancelada totalmente.
- **b) Cancelar totalmente**: el importe a cancelar es el saldo de la operación (FC + ND − NC) y hay que indicar la forma de cancelación. No hace falta informar retenciones. La cancelación alcanza también a las ND/NC asociadas; las que no correspondan se rechazan antes, una por una. Si sale bien, factura y cuenta quedan **Canceladas** con saldo negociable 0 y ya no pueden informarse al agente. "No debe informarse un CBU Pagador para el caso del Sistema de Circulación Abierta".

Request `aceptarFECredRequest` (`AceptarFECredRequestType`) [WSDL]; [MAN] págs. 21-22:

| # | Campo | Tipo | XSD | Man. | Descripción [MAN] pág. 22 |
|---:|---|---|---|---|---|
| 1 | `authRequest` | `AuthRequestType` | 1..1 | S | |
| 2 | `idCtaCte` | `IdCtaCteType` | 1..1 | S | Cuenta de la FECRED |
| 3 | `arrayConfirmarNotasDC` | `ArrayConfirmarNotasType` | 0..1 | N | "Debe reconfirmar si acepta o rechaza cada comprobante (ND/NC) asociado a la Cta Cte para verificar el cálculo del saldo" |
| 4 | `arrayFormasCancelacion` | `ArrayCodigosDescripcionesType` | 0..1 | N | Si canceló total o parcialmente: una o varias formas |
| 5 | `arrayRetenciones` | `ArrayRetencionesType` | 0..1 | N | Retenciones que aplica al vendedor |
| 6 | `arrayAjustesOperacion` | `ArrayAjustesOperacionType` | 0..1 | N | Ajustes al saldo "para el caso de moneda extranjera" |
| 7 | `tipoCancelacion` | `TipoCancelacionSimpleType` | 0..1 | N | `PAR` o `TOT`, si canceló |
| 8 | `importeCancelado` | `ImporteSimpleType` | 0..1 | N | Importe cancelado |
| 9 | `importeTotalRetPesos` | `ImporteSimpleType` | 0..1 | N | Total de retenciones en pesos |
| 10 | `importeEmbargoPesos` | `ImporteSimpleType` | 0..1 | N | Embargo en pesos |
| 11 | `saldoAceptado` | `ImporteSimpleType` | 1..1 | S | El saldo aceptado |
| 12 | `codMoneda` | `string` | 1..1 | S | Moneda (la de la factura, 12001) |
| 13 | `cotizacionMonedaUlt` | `decimal` | 1..1 | S | "Última cotización informada en la Cta.Cte. con la cual se realiza el cálculo del saldo cuando la moneda de la FECRED difiere de PESOS" |
| 14 | `informaCBU` | `SiNoSimpleType` | 0..1 | N | "Obligatorio en cuentas corrientes con la factura con opcionTransferencia "SCA" (no debe enviarse en otro caso)" |
| 15 | `CBUComprador` | `CBUSimpleType` | 0..1 | N | CBU que informa el comprador |

Response: `OperacionFECredReturnType` dentro de `operacionFECredReturn` [MAN] pág. 23.

Validaciones [MAN] págs. 23-25 (el texto completo está en Validaciones y errores): 2000-2010, 2012-2018, 12000-12010, 12012-12015 y 4000-4003. Observación 9000: la CBU no se pudo validar online, pero la aceptación se registra igual (pág. 25, nota 2). El código 2011 se eliminó en la v1.2.0 (pág. 84).

Ejemplo [DERIVADO]: aceptación ADC en pesos, con una ND confirmada y una retención.

```xml
<x:aceptarFECredRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>30999999999</cuitRepresentada></authRequest>
  <idCtaCte><codCtaCte>1200</codCtaCte></idCtaCte>
  <arrayConfirmarNotasDC>
    <confirmarNota><acepta>S</acepta><idNota><CUITEmisor>20111111112</CUITEmisor><codTipoCmp>202</codTipoCmp><ptoVta>1</ptoVta><nroCmp>3</nroCmp></idNota></confirmarNota>
  </arrayConfirmarNotasDC>
  <arrayRetenciones>
    <retencion><codTipo>1</codTipo><importe>61000.00</importe><porcentaje>1.00</porcentaje></retencion>
  </arrayRetenciones>
  <importeTotalRetPesos>61000.00</importeTotalRetPesos>
  <saldoAceptado>6100000.00</saldoAceptado>
  <codMoneda>PES</codMoneda>
  <cotizacionMonedaUlt>1</cotizacionMonedaUlt>
</x:aceptarFECredRequest>
```
```xml
<ns2:aceptarFECredResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <operacionFECredReturn><resultado>A</resultado><idCtaCte><codCtaCte>1200</codCtaCte></idCtaCte></operacionFECredReturn>
</ns2:aceptarFECredResponse>
```

Qué valor exacto de `saldoAceptado` espera el servicio cuando hay retenciones, cancelación parcial o embargo: **NO VERIFICADO** (ver Comportamiento a simular).

### 8. rechazarFECred

Propósito: "el Comprador, dentro del plazo estipulado para el rechazo, puede Rechazar la Cta. Cte. de una Factura Electrónica de Crédito debiendo indicar el motivo del rechazo. De esta manera la Factura y su Cta. Cte asociada alcanzarán el estado final Rechazadas quedando la imposibilidad al vendedor de informarla al Agente de Depósito Colectivo" [MAN] pág. 26.

Request `rechazarFECredRequest` (`RechazarFECredRequestType`): `authRequest` 1..1; `idCtaCte` 1..1; `arrayMotivosRechazo` (`ArrayMotivosRechazoType`) 1..1, con "al menos un motivo" [WSDL]; [MAN] pág. 26.

Response: `OperacionFECredReturnType` [MAN] pág. 27.

Validaciones [MAN] pág. 27: 3000 (`descMotivo`, falta la justificación) y 3001 (`codMotivo` inválido o repetido); además las comunes.

Ejemplo [DERIVADO]:

```xml
<x:rechazarFECredRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>30999999999</cuitRepresentada></authRequest>
  <idCtaCte><idFactura><CUITEmisor>20111111112</CUITEmisor><codTipoCmp>201</codTipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idFactura></idCtaCte>
  <arrayMotivosRechazo>
    <motivoRechazo><codMotivo>1</codMotivo><descMotivo>Descripción del motivo</descMotivo><justificacion>Mercadería no recibida</justificacion></motivoRechazo>
  </arrayMotivosRechazo>
</x:rechazarFECredRequest>
```

### 9. informarFacturaAgtDptoCltv

Propósito: para las facturas con opción ADC, permite al Vendedor "informar a un Agente de Depósito Colectivo la factura de crédito con el saldo negociable resultante de la cuenta corriente vinculada aceptada por el comprador, debiendo indicar una de sus cuentas abiertas en algún Agente de Depósito Colectivo informadas por estos a la AFIP". Esas cuentas se consultan con `consultarCuentasEnAgtDptoCltv` [MAN] pág. 30.

Request `informarFacturaAgtDptoCltvRequest` (`InformarFacturaAgtDptoCltvRequestType`): `authRequest` 1..1; `idCtaCte` 1..1; `ctaAgente` (`CuentaEnAgenteType`) 1..1 [WSDL]; [MAN] pág. 30.

Response: `OperacionFECredReturnType` [MAN] pág. 31.

Validaciones [MAN] pág. 31: 6000-6007; además las comunes (por ejemplo, 1108 si la cuenta no está Aceptada [INFERIDO]).

Ejemplo [DERIVADO]:

```xml
<x:informarFacturaAgtDptoCltvRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>20111111112</cuitRepresentada></authRequest>
  <idCtaCte><codCtaCte>1200</codCtaCte></idCtaCte>
  <ctaAgente><cuitAgente>30500000009</cuitAgente><idCuenta>12345</idCuenta></ctaAgente>
</x:informarFacturaAgtDptoCltvRequest>
```

### 10. consultarFacturasAgtDptoCltv

Propósito: "obtener información sobre los facturas informadas al Agente de Depósito Colectivo. Permite obtener si el Agente ha recibido efectivamente el informe, dicho de otra manera, exterioriza el acuse de recibo del Agente. Puede realizar una consulta particular identificando la Cuenta Corriente o Factura, o una consulta por rango de fechas" [MAN] pág. 50.

Request `consultarFacturasAgtDptoCltvRequest`: `authRequest` 1..1; `idCtaCte` (`IdCtaCteType`) 0..1; `filtroFecha` (`FiltroFechaType`) 0..1 [WSDL]; [MAN] pág. 50. Qué pasa si no se manda ninguno de los dos filtros: **NO VERIFICADO**. No tiene paginado.

Response `consultarFacturasAgtDptoCltvResponse/consultarFacturasAgtDptoCltvReturn` [WSDL]; [MAN] págs. 50-51:

| # | Campo | Tipo | XSD | Man. |
|---:|---|---|---|---|
| 1 | `arrayFacturasAgtDptoCltv` | `ArrayFacturasAgtDptoCltvType` (hijo `facturaInformada` 1..unbounded) | 0..1 | N |
| 2 | `evento` | `CodigoDescripcionType` | 0..1 | N |
| 3 | `arrayObservaciones` | `ArrayCodigosDescripcionesType` | 0..1 | N (el manual dice `arrayObservacion`) |
| 4 | `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | N |
| 5 | `arrayErroresFormato` | `ArrayCodigosDescripcionesStringType` | 0..1 | N |

`FacturaInformadaAgtDptoCltvType` [MAN] pág. 75: `idFactura` (`IdComprobanteType`) 1..1; `infoAgtDptoCltv` (`InfoAgtDptoCltvType`) 1..1.

Validaciones: observación 32767 [MAN] pág. 51.

### 11. consultarCuentasEnAgtDptoCltv

Propósito: "permite al Vendedor consultar sus Cuentas en Agentes de Depósito Colectivo que fueron informadas por ellos a la AFIP" [MAN] pág. 44.

Request `consultarCuentasEnAgtDptoCltvRequest`: sólo `authRequest` 1..1.

Response `consultarCuentasEnAgtDptoCltvResponse/consultarCuentasEnAgtDptoCltvReturn` [WSDL]: `arrayCuentasEnAgente` (`ArrayCuentasEnAgenteType`, 0..1; el manual dice "S"), **`arrayObservacion`** 0..1 (en singular), `arrayErrores` 0..1, `arrayErroresFormato` 0..1.

Validaciones: observación 32767 cuando no hay cuentas [MAN] pág. 45.

Ejemplo de response [DERIVADO]:

```xml
<ns2:consultarCuentasEnAgtDptoCltvResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <consultarCuentasEnAgtDptoCltvReturn>
    <arrayCuentasEnAgente>
      <cuentaEnAgente><cuitAgente>30500000009</cuitAgente><razonSocialAgente>AGENTE DE DEPOSITO SA</razonSocialAgente><idCuenta>12345</idCuenta><denominacion>Cuenta comitente</denominacion></cuentaEnAgente>
    </arrayCuentasEnAgente>
  </consultarCuentasEnAgtDptoCltvReturn>
</ns2:consultarCuentasEnAgtDptoCltvResponse>
```

### 12. consultarObligadoRecepcion (deprecada)

Propósito: "conocer si la CUIT consultada se encuentra obligada a recibir una Factura Electrónica de Crédito". **"DEPRECADO: La información suministrada por este método es incompleta. La obligación corresponde a partir de determinado monto de factura que está en función de la actividad principal del contribuyente y de la fecha de emisión del comprobante. Utilice la operación consultarMontoObligadoRecepción"** [MAN] pág. 46. Sigue en el WSDL.

Request `consultarObligadoRecepcionRequest`: `authRequest` 1..1; `cuitConsultada` (`CuitSimpleType`) 1..1.

Response `consultarObligadoRecepcionResponse/consultarObligadoRecepcionReturn` [WSDL]; [MAN] págs. 46-47:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `respuesta` | `SiNoSimpleType` | 0..1 | N | "Indica si está obligado" |
| 2 | `desde` | `date` | 0..1 | N | "Fecha a partir de la cual está obligado" |
| 3 | `arrayObservacion` | `ArrayCodigosDescripcionesType` | 0..1 | N | |
| 4 | `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | N | |
| 5 | `arrayErroresFormato` | `ArrayCodigosDescripcionesStringType` | 0..1 | N | |

El manual no lista códigos propios. Qué devuelve para una CUIT inexistente: **NO VERIFICADO**.

### 13. consultarTiposRetenciones

Propósito: "consultar los tipos de retenciones habilitadas con sus respectivos porcentajes" [MAN] pág. 48.

Request `consultarTiposRetencionesRequest` (`ConsultarCodigoDescripcionRequestType`): sólo `authRequest`.

Response `consultarTiposRetencionesResponse/consultarTiposRetencionesReturn` [WSDL]; [MAN] págs. 48 y 74: `arrayTiposRetenciones` (hijo `tipoRetencion` 1..unbounded, de tipo `TipoRetencionType`) 0..1; `arrayErroresFormato` 0..1. No tiene `arrayErrores`.

`TipoRetencionType` [MAN] pág. 74: `codigoJurisdiccion` short 1..1; `descripcionJurisdiccion` string 1..1; `porcentajeRetencion` `PorcentajeSimpleType` 1..1.

Ejemplo de request: es el de la llamada real [VIVO], con token falso.

```xml
<x:consultarTiposRetencionesRequest><authRequest><token>abc</token><sign>abc</sign><cuitRepresentada>20111111112</cuitRepresentada></authRequest></x:consultarTiposRetencionesRequest>
```

Response [DERIVADO]: `<consultarTiposRetencionesReturn><arrayTiposRetenciones><tipoRetencion><codigoJurisdiccion>…</codigoJurisdiccion><descripcionJurisdiccion>…</descripcionJurisdiccion><porcentajeRetencion>…</porcentajeRetencion></tipoRetencion>…</arrayTiposRetenciones></consultarTiposRetencionesReturn>`. Los valores son **NO VERIFICADO**.

### 14, 15 y 19. Tablas de código y descripción

Las tres operaciones tienen la misma forma. Request `ConsultarCodigoDescripcionRequestType` (sólo `authRequest`). Response `ConsultarCodigoDescripcionResponseType`, con `codigoDescripcionReturn` (`arrayCodigoDescripcion` 0..1 + `arrayErroresFormato` 0..1) [WSDL].

| # | Operación | Request / response | Contenido | Manual |
|---:|---|---|---|---|
| 14 | `consultarTiposMotivosRechazo` | `consultarTiposMotivosRechazoRequest` / `...Response` | "tipos de motivos de rechazo habilitados para una Factura Electrónica de Crédito y su Cuenta Corriente vinculada". Alimenta `codMotivo`/`descMotivo` | pág. 49 |
| 15 | `consultarTiposFormasCancelacion` | `consultarTiposFormasCancelacionRequest` / `...Response` | "tipos de formas de cancelación habilitados". Alimenta `arrayFormasCancelacion` | pág. 52 |
| 19 | `consultarTiposAjustesOperacion` | `consultarTiposAjustesOperacionRequest` / `...Response` | "tipos de ajustes disponibles al momento de informar la aceptación". Alimenta `arrayAjustesOperacion/ajuste/codigo` | pág. 58 |

Response [DERIVADO]:

```xml
<ns2:consultarTiposFormasCancelacionResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <codigoDescripcionReturn>
    <arrayCodigoDescripcion>
      <codigoDescripcion><codigo>…</codigo><descripcion>…</descripcion></codigoDescripcion>
    </arrayCodigoDescripcion>
  </codigoDescripcionReturn>
</ns2:consultarTiposFormasCancelacionResponse>
```

Ninguna de las tres tablas trae `arrayErrores` ni `evento`. Los valores están en Tablas y datos.

### 16. obtenerRemitos

Propósito: "obtener los remitos asociados a un comprobante en su emisión" [MAN] pág. 53. No tiene `wsdl:documentation`.

Request `obtenerRemitosRequest`: `authRequest` 1..1; `idComprobante` (`IdComprobanteType`) 1..1.

Response `obtenerRemitosResponse/obtenerRemitosReturn` [WSDL]; [MAN] págs. 53-54: `arrayIdsRemitos` (`ArrayIdsComprobantesType`, con hijo **`idsComprobantes`** 1..unbounded de `IdComprobanteType`) 0..1; `arrayErrores` 0..1; `arrayErroresFormato` 0..1. No trae `evento` ni observaciones.

Validaciones: 1105 si el comprobante no existe [INFERIDO]. Qué devuelve si el comprobante no tiene remitos (¿sin `arrayIdsRemitos`?, ¿32767?): **NO VERIFICADO**. Esta respuesta no tiene `arrayObservaciones`.

### 17. consultarHistorialEstadosComprobante

Propósito: "obtener el historial con los cambios de estado de un comprobante" [MAN] pág. 55.

Request: `authRequest` 1..1; `idComprobante` 1..1.

Response `consultarHistorialEstadosComprobanteReturn` [WSDL]; [MAN] pág. 56: `idComprobante` 1..1; `arrayHistorialEstados` (`ArrayHistorialEstadosComprobanteType`, con hijo `estadoHistorico` de tipo `EstadoCmpType` 1..unbounded) **1..1**; `arrayErrores` 0..1; `arrayErroresFormato` 0..1.

`arrayHistorialEstados` es obligatorio y lleva al menos un elemento. Cómo responde el servicio ante un error (por ejemplo 1105) sin violar su propio esquema: **NO VERIFICADO**.

Ejemplo de response [DERIVADO]:

```xml
<consultarHistorialEstadosComprobanteReturn>
  <idComprobante><CUITEmisor>20111111112</CUITEmisor><codTipoCmp>201</codTipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idComprobante>
  <arrayHistorialEstados>
    <estadoHistorico><estado>PendienteRecepcion</estado><fechaHoraEstado>2026-09-10T10:00:00</fechaHoraEstado></estadoHistorico>
    <estadoHistorico><estado>Recepcionado</estado><fechaHoraEstado>2026-09-12T00:00:00</fechaHoraEstado></estadoHistorico>
    <estadoHistorico><estado>Aceptado</estado><fechaHoraEstado>2026-09-20T11:30:00</fechaHoraEstado></estadoHistorico>
  </arrayHistorialEstados>
</consultarHistorialEstadosComprobanteReturn>
```

El orden del historial (cronológico ascendente o descendente) es **NO VERIFICADO**.

### 18. consultarHistorialEstadosCtaCte

Propósito: "obtener el historial con los cambios de estado de una cuenta corriente" [MAN] pág. 57.

Request: `authRequest` 1..1; `idCtaCte` 1..1. Response `consultarHistorialEstadosCtaCteReturn`: `idCtaCte` 1..1; `arrayHistorialEstados` (`ArrayHistorialEstadosCtaCteType`, con hijo `estadoHistorico` de tipo `EstadoCtaCteType` 1..unbounded) 1..1; `arrayErrores` 0..1; `arrayErroresFormato` 0..1 [WSDL]; [MAN] pág. 57. Tiene el mismo problema de obligatoriedad que la operación anterior.

### 20. consultarMontoObligadoRecepcion

Propósito: "conocer si la CUIT consultada se encuentra obligada a recibir una Factura Electrónica de Crédito para una determinada fecha de emisión del comprobante indicando el monto umbral a partir del cual debe confeccionarse una Factura Electrónica de Crédito en lugar de una Factura o Recibo Electrónico del Régimen General" [MAN] pág. 59.

Request `consultarMontoObligadoRecepcionRequest` [WSDL]; [MAN] pág. 59:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `authRequest` | `AuthRequestType` | 1..1 | S | |
| 2 | `cuitConsultada` | `CuitSimpleType` | 1..1 | S | CUIT del posible receptor |
| 3 | `fechaEmision` | `date` | 1..1 | S | "Fecha en la cual se quiere emitir el comprobante" |

Response `consultarMontoObligadoRecepcionReturn` [WSDL]; [MAN] págs. 59-60:

| # | Campo | Tipo | XSD | Man. | Descripción |
|---:|---|---|---|---|---|
| 1 | `obligado` | `SiNoSimpleType` | 0..1 | N | Si está obligado (el manual lo llama `respuesta`) |
| 2 | `montoDesde` | `ImporteSimpleType` | 0..1 | N | "Importe de factura a partir del cual está obligado a recibir Factura de Crédito Electrónica (inclusive)" |
| 3 | `arrayObservacion` | `ArrayCodigosDescripcionesType` | 0..1 | N | |
| 4 | `arrayErrores` | `ArrayCodigosDescripcionesType` | 0..1 | N | |
| 5 | `arrayErroresFormato` | `ArrayCodigosDescripcionesStringType` | 0..1 | N | |

Si `montoDesde` viene cuando `obligado` = `N`: **NO VERIFICADO**. Según [NORM], el monto vigente es $5.549.862 desde el 14/04/2026 (Res. 1/2026). El manual aclara que depende de la actividad principal del receptor y de la fecha (pág. 46), así que no es un valor único.

Ejemplo [DERIVADO]:

```xml
<x:consultarMontoObligadoRecepcionRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>20111111112</cuitRepresentada></authRequest>
  <cuitConsultada>30500010912</cuitConsultada>
  <fechaEmision>2026-10-02</fechaEmision>
</x:consultarMontoObligadoRecepcionRequest>
```
```xml
<ns2:consultarMontoObligadoRecepcionResponse xmlns:ns2="http://ar.gob.afip.wsfecred/FECredService/">
  <consultarMontoObligadoRecepcionReturn><obligado>S</obligado><montoDesde>5549862.00</montoDesde></consultarMontoObligadoRecepcionReturn>
</ns2:consultarMontoObligadoRecepcionResponse>
```

### 21. modificarOpcionTransferencia

Propósito: "permite al emisor o vendedor modificar la opción de transferencia de una Factura de Crédito previo a la aceptación o rechazo de la misma, es decir el estado de la cuenta corriente debe ser "Modificable"". Las opciones son ADC y SCA [MAN] pág. 34.

Request `modificarOpcionTransferenciaRequest` (`ModificarOpcionTransferenciaRequestType`): `authRequest` 1..1; `idCtaCte` 1..1; `opcionTransferencia` 1..1 [WSDL]; [MAN] pág. 34.

Response: `OperacionFECredReturnType` [MAN] pág. 35.

Validaciones [MAN] pág. 35: 7000 (ya tenía elegida esa opción); además las comunes (1108 si la cuenta no está Modificable [INFERIDO]).

Ejemplo [DERIVADO]:

```xml
<x:modificarOpcionTransferenciaRequest>
  <authRequest><token>…</token><sign>…</sign><cuitRepresentada>20111111112</cuitRepresentada></authRequest>
  <idCtaCte><codCtaCte>1200</codCtaCte></idCtaCte>
  <opcionTransferencia>SCA</opcionTransferencia>
</x:modificarOpcionTransferenciaRequest>
```

## Validaciones y errores

### Canales de error

El manual define tres tipos de error que rechazan el pedido, más las observaciones y los eventos [MAN] págs. 6-11:

| Canal | Dónde va | Forma | Cuándo |
|---|---|---|---|
| **Excepcional** | SOAP Fault (HTTP 500) | `faultcode` + `faultstring` (ejemplo `[wscommon_007] ...`) | Autenticación (token, firma, vencimiento, CUIT no representada), XML mal formado, "tags ... con nombres incorrectos o en orden incorrecto" y "tipos de datos" (pág. 6). En vivo llega como `BL<n> <fecha> 500` |
| **Formato** | `arrayErroresFormato/codigoDescripcionString` (HTTP 200) | `codigo` string con la clave de Xerces (`cvc-...`), `descripcion` en español | Validación XSD. "de no superar alguna de las validaciones de formato, el WS devolverá el arrayErroresFormato y no continuará con las validaciones de negocio, por lo cual no existirá el elemento arrayErrores. Son excluyentes" (pág. 7) |
| **Negocio** | `arrayErrores/codigoDescripcion` (HTTP 200) | `codigo` short, `descripcion` | Reglas del régimen (pág. 9). En operaciones de escritura, `resultado` = `R` [INFERIDO] |
| **Observación** | `arrayObservaciones` (o `arrayObservacion`) | igual | La operación se aprueba "con observaciones" (pág. 10): `resultado` = `O` [INFERIDO]. En las consultas, 32767 = sin resultados |
| **Evento** | `evento` (uno solo, `CodigoDescripcionType`) | `codigo` "Único para un evento dado", `descripcion` | "Los eventos programados" (pág. 11). El manual no lista ninguno |

El manual se contradice sobre los errores de tipo de dato: los pone como excepcionales (pág. 6) y también como errores de formato, con el ejemplo `cvc-datatype-valid.1.2.1` "no es un valor válido para un tipo de dato entero" (pág. 7). En vivo no se pudo distinguir, porque todo request sin TA válido termina en `BL... 500`. **Recomendación para ArcaSim** [INFERIDO]: XML mal formado y elemento raíz desconocido → Fault; violaciones del XSD dentro de un request bien formado (tipos, enumeraciones, `choice`, `minOccurs`) → HTTP 200 con `arrayErroresFormato`, más los obligatorios del tipo de retorno (`resultado` = `R` y el `idCtaCte` recibido, en las operaciones de escritura). Dejarlo configurable.

Texto de los errores de formato: el manual muestra mensajes de Xerces traducidos al español (págs. 7-8). Para ArcaSim conviene usar el `codigo` estándar de Xerces (`cvc-datatype-valid.1.2.1`, `cvc-complex-type.2.4.a`, `cvc-enumeration-valid`, `cvc-minLength-valid`, etc.) [INFERIDO].

Error interno: "Ante la recepción de una respuesta detallando error interno recibirá un código [xxxyyyzzz-cuitRepresentada-fechaHora-xyz], por favor, indíquelo al informar el error" [MAN] pág. 19. Si ese código va dentro de la `descripcion` del 10000: **NO VERIFICADO** [INFERIDO que sí].

Orden de evaluación entre validaciones de negocio, y si se acumulan varios errores en `arrayErrores` o se corta en el primero: **NO VERIFICADO**. El tipo admite varios.

### Tabla completa

"Efecto" = tipo (`kind` del JSON) + qué produce. "Dónde" = operación (`*` = todas) y campo del manual. Los textos son literales del manual (corregidos sólo los caracteres que la extracción del PDF desordena, como "repetdi o" → "repetido"), sin las comillas externas. Hay espacios finales que están en el manual (2010, 2011, 2012, 3001, 4002, 5000). El JSON (`wsfecred-codigos.json`) tiene exactamente estas filas.

Ojo: en el texto plano del PDF las columnas de código y mensaje salen corridas una fila (por ejemplo, parece que 1101 = "No existe la cuenta corriente indicada"). La asignación de abajo sale de la extracción de tablas con PyMuPDF, que respeta las celdas.

| Código | Texto / condición | Efecto | Dónde (operación / campo) | Pág. |
|---|---|---|---|---|
| `wscommon_007` | "[wscommon_007] La firma no corresponde al token enviado." | auth; fault (SOAP Fault; en vivo enmascarado como `BL<n> ... 500`) | `*` / `authRequest` | 6 |
| `BL` | `BL<n> <aaaa-mm-dd hh:mm:ss> 500` | infraestructura; HTTP/1.0 200 sin Content-Type, texto plano [VIVO]; reemplaza todo Fault | `*` / — | [VIVO] |
| `cvc-datatype-valid.1.2.1` | "'?' no es un valor válido para un tipo de dato entero." | error; rechaza, en `arrayErroresFormato`; no sigue con validaciones de negocio | `*` / — | 7 |
| `cvc-type.3.1.3` | "El valor '?' en el elemento ' cuitTitularMercaderia' no es válido." | error; rechaza, en `arrayErroresFormato` (el campo del ejemplo no es de este servicio) | `*` / `cuitTitularMercaderia` | 7 |
| `cvc-complex-type.2.4.d` | "Contenido inválido se encontró al comienzo del elemento 'idFactura'. No se esperan hijos en este punto." | error; rechaza, en `arrayErroresFormato` (se mandaron `codCtaCte` e `idFactura` juntos) | `*` / `idCtaCte` | 8 |
| 1000 | "LA CUIT NO SE ENCUENTRA ACTIVA EN EL PUC" | error; rechaza (transporte NO VERIFICADO) | `*` / `cuitRepresentada` | 17 |
| 1001 | "LA CUIT REGISTRA INCONVENIENTES. DEBERÁ DIRIGIRSE A LA DEPENDENCIA EN LA CUAL SE ENCUENTRA INSCRIPTO" | error; rechaza (transporte NO VERIFICADO) | `*` / `cuitRepresentada` | 17 |
| 1002 | "LA CUIT NO REGISTRA ALTA EN IVA o Monotributo o IVA Exento" | error; rechaza (transporte NO VERIFICADO) | `*` / `cuitRepresentada` | 17 |
| 1003 | "LA CUIT NO REGISTRA ALTA EN NINGUNA ACTIVIDAD" | error; rechaza (transporte NO VERIFICADO) | `*` / `cuitRepresentada` | 17 |
| 1004 | "LA CUIT NO SE ENCUENTRA CON ALTA EN EL DOMICILIO FISCAL ELECTRÓNICO O REGISTRA INCONVENIENTES EN EL MISMO. Sólo puede consultar en este sistema." | error; rechaza (transporte NO VERIFICADO) | `*` / `cuitRepresentada` | 17 |
| 1100 | "Ud no puede operar sobre la cuenta corriente indicada" | error; rechaza, `arrayErrores` | `*` / `idCtaCte` | 18 |
| 1101 | "Ud no puede realizar esa operación en la cuenta corriente indicada" | error; rechaza, `arrayErrores` | `*` / `idCtaCte` | 18 |
| 1102 | "No existe la cuenta corriente indicada" | error; rechaza, `arrayErrores` | `*` / `idCtaCte` | 18 |
| 1103 | "La operación no pudo realizarse, reinténtelo más tarde" | error; rechaza, `arrayErrores` (campo: genérico) | `*` / — | 18 |
| 1104 | "La cuenta corriente fue recientemente modificada, actualice" | error; rechaza, `arrayErrores` | `*` / `idCtaCte` | 18 |
| 1105 | "No existe el comprobante indicado" | error; rechaza, `arrayErrores` | `*` / `idComprobante` | 18 |
| 1106 | "Debe aguardar hasta la hora 24 del día siguiente de la fecha de puesta a disposición en DFE para operar con esta cuenta corriente." | error; rechaza, `arrayErrores` | `*` / `idCtaCte` | 18 |
| 1107 | "Ha excedido el plazo que le permite realizar esta operación." | error; rechaza, `arrayErrores` | `*` / `idCtaCte` | 18 |
| 1108 | "El estado actual no le permite realizar esta operación." | error; rechaza, `arrayErrores` | `*` / `idCtaCte` | 18 |
| 10000 | "Error interno de la aplicación" | error; rechaza, `arrayErrores` (campo: genérico); con un código de seguimiento `[xxxyyyzzz-cuitRepresentada-fechaHora-xyz]` | `*` / — | 19 |
| 32767 | "La búsqueda no ha arrojado resultados con los filtros indicados." | observacion; listado en la tabla común de errores (campo: genérico); en las consultas sale como observación | `*` / — | 19 |
| 2000 | "Falta informar la aceptación de una Nota de Débito/Crédito" | error; rechaza | `aceptarFECred` / `arrayConfirmarNotasDC` | 23 |
| 2001 | "Debe rechazar la Nota de Débito/Crédito individualmente si desea rechazarla" | error; rechaza | `aceptarFECred` / `arrayConfirmarNotasDC` | 23 |
| 2002 | "Informa aceptar una Nota de Débito/Crédito que fue rechazada" | error; rechaza | `aceptarFECred` / `arrayConfirmarNotasDC` | 23 |
| 2003 | "Número de jurisdicción de retención inválido o repetido." | error; rechaza | `aceptarFECred` / `arrayRetenciones` | 23 |
| 2004 | "Debe justificar si modifica el porcentaje de retención normado" | error; rechaza | `aceptarFECred` / `arrayRetenciones` | 23 |
| 2005 | "El importe retenido no coincide con nuestros cálculos a partir del saldo de la factura de crédito" | error; rechaza | `aceptarFECred` / `arrayRetenciones` | 24 |
| 2006 | "Debe indicar una cotización al aceptar" | error; rechaza | `aceptarFECred` / `cotizacionMonedaUlt` | 24 |
| 2007 | "La cotización para PESOS debe ser 1.0" | error; rechaza | `aceptarFECred` / `cotizacionMonedaUlt, codMoneda` | 24 |
| 2008 | "El saldo aceptado informado no coincide con nuestros cálculos a partir del saldo de la factura de crédito" | error; rechaza | `aceptarFECred` / `saldoAceptado` | 24 |
| 2009 | "El tipo de cambio no podrá ser inferior al 2% ni superior en un 400% del que suministra AFIP como orientativo de acuerdo a la cotización oficial" | error; rechaza | `aceptarFECred` / `cotizacionMonedaUlt` | 24 |
| 2010 | "Informa un importe de negativo, lo que no es válido. " | error; rechaza | `aceptarFECred` / `importeCancelado, importeTotalRetPesos, importeEmbargoPesos` | 24 |
| 2011 | "La cotización informada difiere con la de la Factura, debe informar ajuste por tipo de cambio. " | error; **dado de baja en v1.2.0** ("Se quita validación"); no emitir | `aceptarFECred` / `cotizacionMonedaUlt, arrayAjustesOperacion` | 84 |
| 2012 | "La cotización informada coincide con la de la Factura, no debe informar ajuste por tipo de cambio. " | error; rechaza | `aceptarFECred` / `cotizacionMonedaUlt, arrayAjustesOperacion` | 24 |
| 2013 | "Tipo de Ajuste de operación no válido o informado repetido." | error; rechaza | `aceptarFECred` / `arrayAjustesOperacion` | 24 |
| 2014 | "No corresponde informar CBU cuando la opción de transferencia no es Sistema de Circulación Abierta." | error; rechaza | `aceptarFECred` / `CBUComprador` | 24 |
| 2015 | "La CBU no es válida para la CUIT compradora." | error; rechaza | `aceptarFECred` / `CBUComprador` | 24 |
| 2016 | "No corresponde informar CBU cuando el comprobante quedará cancelado totalmente (el saldo aceptado es 0)." | error; rechaza | `aceptarFECred` / `CBUComprador` | 24 |
| 2017 | "No corresponde informar CBU cuando la moneda de la factura es distinta a pesos o dólares" | error; rechaza | `aceptarFECred` / `codMoneda, informaCBU, CBUComprador` | 24 |
| 2018 | "La moneda de la cuenta de la CBU indicada no coincide con la moneda de la factura" | error; rechaza | `aceptarFECred` / `CBUComprador` | 24 |
| 12000 | "Informa una cotización para pesos distinta de 1." | error; rechaza | `aceptarFECred` / `cotizacionMonedaUlt, codMoneda` | 24 |
| 12001 | "Informa una moneda distinta a la de la Factura de Crédito." | error; rechaza | `aceptarFECred` / `codMoneda` | 24 |
| 12002 | "El saldo aceptado que informa no coincide por el calculado por nuestros registros, verifique sus cuentas." | error; rechaza | `aceptarFECred` / `saldoAceptado` | 24 |
| 12003 | "Falta indicar su informe de confirmación de aceptación de al menos una Nota de Débito/Crédito de la cuenta corriente que se encuentra registrada" | error; rechaza | `aceptarFECred` / `arrayConfirmarNotasDC` | 24 |
| 12004 | "No coincide su informe de confirmación de aceptación de las Nota de Débito/Crédito de la cuenta corriente con el estado en el cual se encuentran registradas" | error; rechaza | `aceptarFECred` / `arrayConfirmarNotasDC` | 24 |
| 12005 | "Tiene diferencias entre el total y los parciales de los importes de retenciones informadas" | error; rechaza | `aceptarFECred` / `arrayRetenciones, importeTotalRetPesos` | 24 |
| 12006 | "Al informar un porcentaje de retención distinto a lo normado, debe informar una justificación." | error; rechaza | `aceptarFECred` / `arrayRetenciones` | 24 |
| 12007 | "La información de retenciones está incompleta. De informar retenciones debe informar importe mayor a 0 y al menos una retención" | error; rechaza | `aceptarFECred` / `arrayRetenciones, importeTotalRetPesos` | 24 |
| 12008 | "La información de cancelación está incompleta. De informar cancelación debe informar importe mayor a 0, al menos una forma de cancelación y el tipo (si es parcial o total)" | error; rechaza | `aceptarFECred` / `arrayFormasCancelacion, tipoCancelacion, importeCancelado` | 24 |
| 12009 | "El código de la retención es inválido (consultarTiposRetenciones)." | error; rechaza | `aceptarFECred` / `arrayRetenciones` | 24 |
| 12010 | "Sólo puede informar ajustes de operación para moneda extranjera." | error; rechaza | `aceptarFECred` / `arrayAjustesOperacion` | 25 |
| 12012 | "No debe informar CBU Comprador con Cancelación Total." | error; rechaza | `aceptarFECred` / `informaCBU` | 25 |
| 12013 | "Si indica que informa CBU debe enviarla y viceversa." | error; rechaza | `aceptarFECred` / `informaCBU, CBUComprador` | 25 |
| 12014 | "Debe indicar si desea informar la CBU comprador al aceptar una factura con opcion de transferencia de Sistema de Circulacion Abierta" | error; rechaza | `aceptarFECred` / `informaCBU` | 25 |
| 12015 | "No debe indicar si desea informar la CBU comprador al aceptar una factura con opcion de transferencia distinta de Sistema de Circulacion Abierta" | error; rechaza | `aceptarFECred` / `informaCBU` | 25 |
| 4000 | "El monto informado no cancela totalmente el saldo de la FECRED" | error; rechaza | `aceptarFECred` / `importeCancelado, tipoCancelacion` | 25 |
| 4001 | "Falta informar al menos una forma de cancelación" | error; rechaza | `aceptarFECred` / `arrayFormasCancelacion` | 25 |
| 4002 | "Código de forma de cancelación inválido o repetido. " | error; rechaza | `aceptarFECred` / `arrayFormasCancelacion` | 25 |
| 4003 | "Las formas de cancelación "Cesión" y “Locación de Bienes Inmuebles” deben ser usadas para cancelación total y sin informar otro tipo de forma de cancelación entre los usados" | error; rechaza | `aceptarFECred` / `arrayFormasCancelacion` | 25 |
| 9000 | "La CBU no pudo validarse." | observacion; observa: la aceptación se registra (`resultado` O [INFERIDO]); falla de validación online contra el BCRA | `aceptarFECred` / `CBUComprador` | 25 |
| 3000 | "Debe indicar una justificación por el rechazo" | error; rechaza | `rechazarFECred` / `descMotivo` | 27 |
| 3001 | "Código de motivo de rechazo inválido o repetido. " | error; rechaza | `rechazarFECred` / `codMotivo` | 27 |
| 5000 | "El rechazo de esta nota no puede realizarse, deja el saldo de la operación inválido (cero o negativo) " | error; rechaza (campo: "Resultado de la operación") | `rechazarNotaDC` / — | 29 |
| 15000 | "Ud no puede realizar esa operación en el comprobante en el estado en el que se encuentra" | error; rechaza | `rechazarNotaDC` / `idComprobante` | 29 |
| 3000 | "Debe indicar una justificación por el rechazo" | error; rechaza | `rechazarNotaDC` / `arrayMotivosRechazo` | 29 |
| 3001 | "Código de motivo de rechazo inválido o repetido. " | error; rechaza | `rechazarNotaDC` / `arrayMotivosRechazo` | 29 |
| 6000 | "La factura ya fue informada al agente de depósito colectivo" | error; rechaza | `informarFacturaAgtDptoCltv` / `idCtaCte` | 31 |
| 6001 | "La factura ya fue informada al agente de depósito colectivo y se encuentra pendiente de confirmación de recepción" | error; rechaza | `informarFacturaAgtDptoCltv` / `idCtaCte` | 31 |
| 6002 | "Los datos de la cuenta en el agente no son válidos para la CUIT representada" | error; rechaza | `informarFacturaAgtDptoCltv` / `ctaAgente` | 31 |
| 6003 | "Problema con método de envío al agente de depósito colectivo" | error; rechaza (campo: "Error de sistema") | `informarFacturaAgtDptoCltv` / — | 31 |
| 6004 | "Esta factura de crédito no constituye un “título ejecutivo y valor no cartular” por la fecha de vencimiento de pago con la cual fue emitida (Ley 27440 - Art 4)" | error; rechaza | `informarFacturaAgtDptoCltv` / `idCtaCte` | 31 |
| 6005 | "Debe aguardar al fin del plazo de aceptación para poder solicitar el informe al agente de depósito colectivo, por la fecha de vencimiento de pago con la cual fue emitida (Ley 27440 - Art 4 inc. d -2do párrafo)" | error; rechaza | `informarFacturaAgtDptoCltv` / `idCtaCte` | 31 |
| 6006 | "Debe aguardar a que se cumpla la fecha de vencimiento de pago para solicitar el informe a caja de valores (Ley 27.400 - Art 18)" | error; rechaza | `informarFacturaAgtDptoCltv` / `idCtaCte` | 31 |
| 6007 | "La factura se encuentra a la espera de la lectura del agente de depósito colectivo" | error; rechaza | `informarFacturaAgtDptoCltv` / `idCtaCte` | 31 |
| 4000 | "El monto informado no cancela totalmente el saldo de la FECRED" | error; rechaza | `informarCancelacionTotalFECred` / `importeCancelacion` | 33 |
| 4001 | "Falta informar al menos una forma de cancelación" | error; rechaza | `informarCancelacionTotalFECred` / `arrayFormasCancelacion` | 33 |
| 4002 | "Código de forma de cancelación inválido o repetido. " | error; rechaza | `informarCancelacionTotalFECred` / `arrayFormasCancelacion` | 33 |
| 7000 | "Indica la misma opción de transferencia ya elegida" | error; rechaza | `modificarOpcionTransferencia` / `opcionTransferencia` | 35 |
| 32767 | "La búsqueda no ha arrojado resultados con los filtros indicados" | observacion; observa: sin `arrayComprobantes` | `consultarComprobantes` / `arrayObservaciones` | 38 |
| 12011 | "El número de página solicitado debe ser mayor a cero." | error; rechaza | `consultarComprobantes` / `nroPagina` | 38 |
| 32767 | "La búsqueda no ha arrojado resultados con los filtros indicados" | observacion; observa: sin `arrayInfosCtaCte` | `consultarCtasCtes` / `arrayObservaciones` | 41 |
| 12011 | "El número de página solicitado debe ser mayor a cero." | error; rechaza | `consultarCtasCtes` / `nroPagina` | 41 |
| 32767 | "La búsqueda no ha arrojado resultados con los filtros indicados" | observacion; observa: sin `arrayCuentasEnAgente` (el manual dice `arrayObservaciones`; el WSDL, `arrayObservacion`) | `consultarCuentasEnAgtDptoCltv` / `arrayObservacion` | 45 |
| 32767 | "La búsqueda no ha arrojado resultados con los filtros indicados" | observacion; observa: sin `arrayFacturasAgtDptoCltv` | `consultarFacturasAgtDptoCltv` / `arrayObservaciones` | 51 |

Notas sobre la tabla:

- **Códigos repetidos con distinto alcance.** 3000/3001 están en `rechazarFECred` y en `rechazarNotaDC`; 4000-4002, en `aceptarFECred` y en `informarCancelacionTotalFECred` (en esta última, el campo es `importeCancelacion`); 12011, en las dos consultas paginadas; 32767, en la tabla común y en cuatro consultas.
- **1100 a 1108** son "validaciones comunes a todos los métodos" (pág. 18). Para cada operación hay que decidir cuáles aplican. Ver la matriz en Comportamiento a simular [INFERIDO].
- **2003 vs 12009:** los dos hablan de un código de retención inválido. 2003 dice "Número de jurisdicción ... inválido o repetido"; 12009, "El código de la retención es inválido". Cuál sale en cada caso: **NO VERIFICADO**. Mismo solapamiento entre 2004 y 12006 (justificación del porcentaje), 2008 y 12002 (saldo aceptado), 2007 y 12000 (cotización en pesos) y 2000/2002 frente a 12003/12004 (confirmación de notas). Parece que la serie 12xxx reemplazó a la 2xxx sin borrarla [INFERIDO].
- **2009** cambió tres veces: 200% (v2.0.1), 400% (v2.0.2) y el piso de 2% (v2.0.3) (pág. 84). El texto literal ("no podrá ser inferior al 2% ni superior en un 400% del que suministra AFIP como orientativo") no deja claro si el rango es [2%, 400%] de la cotización orientativa o ±2% / +400%: **NO VERIFICADO**.
- **6006** cita la "Ley 27.400 - Art 18": es así en el manual. Probablemente sea una errata por 27.440 [INFERIDO]. Se conserva el texto literal.
- **4003**: el mensaje se reescribió en la v1.2.0 para sumar "Locación de Bienes Inmuebles" (pág. 84). El código nació en la v1.1.0 junto con "Cesión" y "Otros medios de pago habilitados por el BCRA" (pág. 85).
- **2011** se quitó en la v1.2.0. Figura sólo en el change log (pág. 84) y no hay que emitirlo.
- **5000**: el campo del manual es "Resultado de la operación". Se dispara cuando, al sacar la nota del cálculo, el saldo queda ≤ 0. Rechazar una NC sube el saldo, así que en la práctica sólo puede pasar al rechazar una ND en una cuenta cuyas NC casi igualan a la factura [INFERIDO].

## Tablas y datos

### Lo que trae el manual

El manual **no publica los valores** de ninguna tabla de parámetros. Lo único que se sabe:

| Tabla | Operación | Valores conocidos | Fuente |
|---|---|---|---|
| Formas de cancelación | `consultarTiposFormasCancelacion` | Por nombre, sin código: "Cesión", "Otros medios de pago habilitados por el BCRA" (v1.1.0) y "Locación de Bienes Inmuebles" (v1.2.0). "Cesión" y "Locación de Bienes Inmuebles" sólo con cancelación total y solas (4003) | [MAN] págs. 25, 84-85 |
| Ajustes de operación | `consultarTiposAjustesOperacion` | Existe al menos un "ajuste por tipo de cambio" (2011 histórico, 2012). Códigos: **NO VERIFICADO** | [MAN] págs. 24, 84 |
| Tipos de retención | `consultarTiposRetenciones` | Son jurisdicciones con un porcentaje normado (`codigoJurisdiccion`, `descripcionJurisdiccion`, `porcentajeRetencion`). Valores: **NO VERIFICADO** | [MAN] págs. 48, 74 |
| Motivos de rechazo | `consultarTiposMotivosRechazo` | **NO VERIFICADO** | [MAN] pág. 49 |
| Tipos de comprobante | — | 201/202/203 (FCE A: factura, ND, NC), 206/207/208 (B), 211/212/213 (C). El manual usa 201 en su ejemplo (pág. 8) | [NORM] `normativa.md` §8.2; `wsfev1.md` §7.1 |
| Monedas | — | `PES` (cotización 1, 2007/12000). La CBU sólo se admite con moneda "pesos o dólares" (2017). Códigos de moneda: los de `FEParamGetTiposMonedas` de wsfev1 (`PES`, `DOL`, ...) [INFERIDO] | [MAN] pág. 24; `wsfev1.md` §7.5 |
| Estados | enums del WSDL | Ver Tipos simples | [WSDL] |
| Monto mínimo FCE | `consultarMontoObligadoRecepcion` | $5.549.862 desde el 14/04/2026 (Res. 1/2026). Depende de la actividad principal y de la fecha, así que conviene una tabla `(actividad, desde) → monto` | [NORM] `normativa.md` §8.1; [MAN] pág. 46 |

Para el simulador: semillas configurables con códigos propios y documentados como "valores de ArcaSim", hasta que alguien capture las tablas reales con un certificado de homologación.

### Datos de prueba

- El manual no publica CUITs ni cuentas de prueba. Sus ejemplos usan `30999999999`, `codCtaCte` 1200, factura 201 / PV 1 / Nº 1 (pág. 8).
- ARCA publica para homologación una lista de 1201 CUITs de "empresas grandes" (receptores FCE): <https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Pruebas-homologacion-WS-FCE.xlsx> ([NORM] §8.3). Sirve como padrón semilla de receptores.
- [PYAF] usa en su docstring la cuenta 2561 de la CUIT 20267565393, factura 201 PV 999 Nº 22, PES 12.850.000, Modificable (2019).
- Ambiente de homologación real: `ambiente` = `Testing - FI1`, `id` = `WS-2.1.6` [VIVO]. Para ArcaSim conviene un valor distinto, por ejemplo `ArcaSim - local`, para que nadie confunda ambientes.

## Comportamiento a simular

### Modelo de negocio

**Actores** [MAN] págs. 12-13:

- **Vendedor (emisor)**: emite la FCE "ante la entrega de la mercadería o servicios. En ese momento se genera la Cuenta Corriente de esa Factura". Puede modificarla con ND/NC asociadas y, después de la aceptación, "informarla al Agente de Depósito Colectivo (si es la opción de transferencia elegida)".
- **Comprador (receptor)**: "Es el responsable de aceptar o rechazar las Facturas Electrónicas de Crédito y sus comprobantes asociados".
- **Sistema AFIP**: da por "Recepcionado" un comprobante cuando vence el plazo de notificación y "Se ejecuta la Aceptación Tácita de aquellas Cuentas Corrientes en estado "Modificable" vencido el plazo de aceptación normado".

**Rol en las llamadas.** `cuitRepresentada` define quién llama. En las consultas, `rolCUITRepresentada` dice si se buscan los comprobantes o cuentas donde esa CUIT es `Emisor` o `Receptor`. Matriz de permisos [INFERIDO a partir de las descripciones de cada operación]:

| Operación | Quién puede | Si llama otro |
|---|---|---|
| `aceptarFECred`, `rechazarFECred`, `rechazarNotaDC`, `informarCancelacionTotalFECred` | Comprador (`cuitRepresentada` = `cuitReceptor`) | 1101 si es el vendedor; 1100 si es un tercero [INFERIDO] |
| `modificarOpcionTransferencia`, `informarFacturaAgtDptoCltv`, `consultarCuentasEnAgtDptoCltv` | Vendedor (`cuitRepresentada` = `cuitEmisor`) | ídem |
| `consultarCtaCte`, historiales, `obtenerRemitos`, `consultarFacturasAgtDptoCltv` | Emisor o receptor | 1100 / 1105 [INFERIDO] |
| `consultarComprobantes`, `consultarCtasCtes` | Cualquiera, filtrado por su rol | — |
| Tablas, `consultarMontoObligadoRecepcion`, `consultarObligadoRecepcion` | Cualquiera autenticado | — |

**Cuenta corriente (`codCtaCte`).** Hay una por cada **factura** FCE (201, 206, 211) autorizada: "{Si es FACT} crear CTA CTE (S)" (diagrama, pág. 14). El código es un `long` que asigna ARCA y no tiene formato conocido; para el simulador basta un secuencial [INFERIDO]. Las ND/NC (202, 203, 207, 208, 212, 213) no crean cuenta. Se suman a la cuenta de la factura asociada, y el `codCtaCte` de su `ComprobanteType` es el de esa factura [MAN] pág. 71.

**Saldo** [MAN] pág. 73: `saldo` = importe de la factura + ND no rechazadas − NC no rechazadas. Ignora cancelaciones, embargos y retenciones. `importeInicial` = importe de la factura. `saldoAceptado` = el saldo negociable que fijó la aceptación: positivo si se aceptó sin cancelación total y 0 si se canceló totalmente (págs. 13, 20). Cómo se calcula con cancelación parcial, retenciones, embargo y ajustes: **NO VERIFICADO**. Propuesta [INFERIDO]: `saldoAceptado` = `saldo` ± ajustes − `importeCancelado` − retenciones − embargo, con los importes en pesos convertidos a la moneda de la factura usando `cotizacionMonedaUlt`. Validar con 2008/12002 dentro de un margen configurable.

**Vínculo con la emisión** (wsfev1/wsmtxca) [INFERIDO, a partir de `wsfev1.md` y `normativa.md` §8.3]:

- La factura FCE tiene que traer el opcional 27 (`SCA`/`ADC`) → `opcionTransferencia` inicial; 2101 → `CBUEmisor`; 2102 → `AliasEmisor`; 23 → `referenciasComerciales`; `FchVtoPago` → `fechaVenPago`.
- Las ND/NC traen el opcional 22 (`S`/`N`) → `esAnulacion`, y asocian una factura del mismo tipo de clase (o un remito) → `idComprobanteAsociado`.
- wsfev1 rechaza una NC FCE cuyo monto sea ≥ el saldo actual de la cuenta (10184). Para eso necesita leer el saldo que mantiene este servicio, así que el estado tiene que ser **compartido** entre los dos servicios simulados.
- Remitos: los `CbtesAsoc` de tipo remito informados al emitir son lo que devuelve `obtenerRemitos` (los tipos asociables a 201/206/211 son 91, 990, 991, 993, 994 y 995 según `wsfev1.md` §4.4) [INFERIDO].

### Estados del comprobante

[MAN] págs. 12-14 (texto y diagrama). El diagrama distingue:

- **Tipo A**: "Factura y Notas de Débito/Crédito "PRE-APROBACIÓN/RECHAZO"".
- **Tipo B**: "Notas de Débito/Crédito de Anulación y Notas de Débito/Crédito Post-Aprobación/Rechazo".

| Desde | Hacia | Disparador | Actor | Aplica a |
|---|---|---|---|---|
| (emisión) | `PendienteRecepcion` | autorización en wsfev1/wsmtxca | S | todos. Se suma al saldo si es Tipo A |
| `PendienteRecepcion` | `Recepcionado` | vence el "plazo normado tras el envío de la comunicación al domicilio fiscal electrónico del comprador" | S | todos. **Fin para Tipo B** |
| `Recepcionado` (Tipo A, ND/NC) | `Rechazado` | `rechazarNotaDC`, sólo mientras la factura no esté aceptada ni rechazada | R | ND/NC Tipo A. Sale del saldo |
| `Recepcionado` (Tipo A) | `Aceptado` | la cuenta pasa a Aceptada o CanceladaTotal | R o S | la factura y las ND/NC no rechazadas |
| `Recepcionado` (Tipo A) | `Rechazado` | la cuenta pasa a Rechazada | R | la factura y **todas** sus ND/NC Tipo A |
| `Aceptado` (factura) | `InformadaAgDpto` | `informarFacturaAgtDptoCltv` | E | sólo la factura |

Si una ND/NC Tipo A se puede rechazar cuando todavía está `PendienteRecepcion`: el texto lo permite ("si es emitido previo a la aceptación o rechazo"), el diagrama dibuja el rechazo desde `Recepcionado` y 1106 exige esperar "hasta la hora 24 del día siguiente de la fecha de puesta a disposición en DFE para operar". **NO VERIFICADO**. Para el simulador: permitirlo sólo después del corte de 1106 [INFERIDO].

`esPostAceptacion` = `S` en las ND/NC emitidas después de que la cuenta dejó de estar Modificable (págs. 12-13, 70). Si las ND/NC Tipo B mueven el `saldo`: **NO VERIFICADO**. El texto general dice que los comprobantes "son incorporados en el cálculo del saldo" (pág. 12), pero el diagrama sólo une a Tipo A con la cuenta. Los estados de los comprobantes después de una cancelación total no cambian (no existe un estado "Cancelado" en `EstadoCmpSimpleType`; quedan `Aceptado` [INFERIDO]).

### Estados de la cuenta corriente

[MAN] págs. 13-14 y 20; diagrama pág. 14:

| Desde | Hacia | Disparador | Actor | Condiciones |
|---|---|---|---|---|
| (factura emitida) | `Modificable` | autorización | S | Mientras está Modificable, las ND/NC cambian el saldo y el vendedor puede usar `modificarOpcionTransferencia` |
| `Modificable` | `Aceptada` | `aceptarFECred` sin `tipoCancelacion` = `TOT` | R | `tipoAcep` = `Expresa`; `saldoAceptado` > 0 |
| `Modificable` | `Aceptada` | vence el plazo de aceptación (`fechaVenAcep`) | S | **Aceptación tácita**: `tipoAcep` = `Tacita` |
| `Modificable` | `CanceladaTotal` | `aceptarFECred` con `tipoCancelacion` = `TOT` | R | `importeCancelado` = saldo (4000); saldo negociable 0 |
| `Modificable` | `Rechazada` | `rechazarFECred` | R | "{Dentro del Plazo de Rechazo}". **Final** |
| `Aceptada` (ADC) | `CanceladaTotal` | `informarCancelacionTotalFECred` | R | Lo que ocurra primero entre esto y el informe al agente. **Final** |
| `Aceptada` (ADC) | `InformadaAgDpto` | `informarFacturaAgtDptoCltv` | E | Con saldo negociable positivo. **Final** |
| `Aceptada` (SCA) | — | — | — | "quedarán aceptadas sin posibilidad de cambiar su estado" (pág. 13, nota 1). **Final** |

Cada cambio se registra con `fechaHoraEstado` en el historial (`consultarHistorialEstadosCtaCte`). Lo mismo con los comprobantes.

Cuál es la opción de transferencia de una cuenta aceptada **tácitamente**, y qué `infoSCA` le corresponde (sin CBU del receptor): **NO VERIFICADO**.

### Plazos

El manual habla de "plazo normado", "plazo de aceptación" y "plazo de rechazo", pero **no da ningún número**. El simulador necesita como parámetros configurables:

| Parámetro | Uso | Fuente |
|---|---|---|
| Plazo de notificación | `PendienteRecepcion` → `Recepcionado`; define `fechaPuestaDispo` | [MAN] pág. 12, sin valor |
| Corte de 1106 | No se opera hasta el fin del día siguiente a `fechaPuestaDispo` | [MAN] pág. 18 |
| Plazo de aceptación | `fechaVenAcep`; después de esa fecha, aceptación tácita, y 1107 para aceptar, rechazar o rechazar notas | [MAN] págs. 13, 18, 20 |
| Plazo de rechazo | Igual al de aceptación [INFERIDO] | [MAN] pág. 26 |
| Plazo para cancelación total | "mientras no se haya vencido el plazo de aceptación estipulado" (pág. 20); 1107 después | [MAN] |
| Reglas de 6004/6005/6006 | Dependen de `fechaVenPago` y de la Ley 27.440, art. 4 | [MAN] pág. 31, sin detalle |

Los valores legales (días) están en la Ley 27.440 y en la RG 4367, que este relevamiento no leyó: **NO VERIFICADO**. Usar un reloj configurable, como en wsfev1.

### Reglas de `aceptarFECred` a implementar

Detalle por código en la tabla de errores. En resumen [MAN] págs. 20-25:

1. Estado `Modificable` y dentro del plazo (1108, 1107, 1106).
2. `arrayConfirmarNotasDC` tiene que listar todas las ND/NC Tipo A de la cuenta: `acepta` = `S` para las vigentes (2000, 12003); `N` no vale, hay que rechazar antes con `rechazarNotaDC` (2001); no se puede aceptar una rechazada (2002); la lista tiene que coincidir con lo registrado (12004).
3. Moneda: `codMoneda` = la de la factura (12001). En PES, `cotizacionMonedaUlt` = 1 (2007, 12000). En moneda extranjera, dentro del rango de 2009 respecto de una cotización orientativa (configurable). La cotización es obligatoria (2006).
4. Ajustes: sólo en moneda extranjera (12010); códigos válidos y sin repetir (2013); no se informa ajuste por tipo de cambio si la cotización coincide con la de la factura (2012).
5. Retenciones: códigos válidos y sin repetir (2003, 12009); si el porcentaje difiere del de tabla, hace falta `descMotivo` (2004, 12006); el importe tiene que coincidir con el cálculo sobre el saldo (2005); la suma de las retenciones tiene que igualar `importeTotalRetPesos` (12005); si se informan, deben estar completas y ser > 0 (12007).
6. Cancelación: si viene cualquiera de `arrayFormasCancelacion`, `tipoCancelacion` o `importeCancelado`, tienen que venir los tres y el importe > 0 (12008); formas válidas y sin repetir (4002), al menos una (4001); "Cesión" y "Locación de Bienes Inmuebles" sólo con `TOT` y solas (4003); con `TOT`, el importe tiene que ser igual al saldo (4000).
7. Importes no negativos (2010).
8. `saldoAceptado` = el cálculo del servicio (2008, 12002).
9. CBU (sólo para SCA): `informaCBU` obligatorio con SCA (12014) y prohibido con ADC (12015); CBU sólo con SCA (2014); `informaCBU` = `S` si y sólo si viene `CBUComprador` (12013); sin CBU con cancelación total (12012, 2016); moneda PES o DOL (2017); la moneda de la cuenta bancaria tiene que coincidir con la de la factura (2018); la CBU tiene que pertenecer al comprador (2015). Si el BCRA no responde: se acepta con la observación 9000 y `CBUValidada` = `N` [INFERIDO].
10. Si sale bien: la cuenta pasa a `Aceptada` (o a `CanceladaTotal`); la factura y las ND/NC Tipo A vigentes pasan a `Aceptado`; se guardan `tipoAcep` = `Expresa`, `fechaHoraAcep`, formas, retenciones, ajustes, importes y `saldoAceptado`. Con SCA se crea `infoTransferencia/infoSCA` (`fechaAceptacionFactura`, `informaCBUReceptor`, `CBUReceptor`, `CBUValidada`).

### Agente de Depósito Colectivo y SCA

- **ADC**: el vendedor ve las cuentas que **los agentes informaron a AFIP** (`consultarCuentasEnAgtDptoCltv`). En el simulador, esas cuentas son una semilla por CUIT vendedora, o las carga el wsfecredagente simulado [INFERIDO]. `informarFacturaAgtDptoCltv` exige una de esas cuentas (6002) y crea `infoAgtDptoCltv` con `fechaInfo` = hoy, `ctaAgente` y `recibida` = `N`. Desde ese momento, la cuenta y la factura quedan `InformadaAgDpto`.
- El agente lee la factura (`fechaLectura`), confirma la recepción (`recibida` = `S`, `fechaRecep`) y la acepta o la rechaza (`aceptada`, `motivoRechazo`), con `idPagoAgtDptoCltv` y `CBUAgtDptoCltv`. Eso pasa por wsfecredagente [INFERIDO por los nombres de los campos y el título de ese servicio], y es lo que expone `consultarFacturasAgtDptoCltv` ("exterioriza el acuse de recibo del Agente", pág. 50). 6001 ("pendiente de confirmación de recepción") y 6007 ("a la espera de la lectura") son los rechazos de un segundo informe mientras el primero sigue en curso. Si después de un rechazo del agente (`aceptada` = `N`) se puede volver a informar a otra cuenta: **NO VERIFICADO**.
- **SCA**: al aceptar con SCA, la factura queda a disposición del Sistema de Circulación Abierta; `fechaLecturaSCA` se completa cuando el SCA la consulta (wsfecredsca [INFERIDO]). No admite cancelación total posterior ni informe al ADC.
- **`modificarOpcionTransferencia`**: sólo en `Modificable`; con la misma opción → 7000. Cambia `opcionTransferencia` en `ComprobanteType` e `InfoCtaCteType`.

### Obligación de recibir FCE

- `consultarMontoObligadoRecepcion(cuitConsultada, fechaEmision)` → `obligado` S/N y `montoDesde` (inclusive). Semilla: CUITs "empresa grande" (lista de homologación de [NORM]) → `S` con el monto vigente a esa fecha; el resto → `N` [INFERIDO]. El monto depende de la actividad principal y de la fecha (pág. 46).
- `consultarObligadoRecepcion(cuitConsultada)` → `respuesta` S/N y `desde` (fecha de inicio de la obligación). Está deprecada, pero sigue en el WSDL y hay que responderla.

### Paginación

- `consultarComprobantes` y `consultarCtasCtes`: `nroPagina` opcional; ≤ 0 → 12011; la respuesta repite `nroPagina` y trae `hayMas` = `S`/`N`. El tamaño de página es "dinámico" (pág. 36): para el simulador, configurable (por ejemplo 100) [recomendación].
- Si falta `nroPagina`: ¿página 1 con `hayMas`, o todos los resultados sin paginar (el comportamiento anterior a la v1.3.0)? **NO VERIFICADO**. Propuesta: página 1 [INFERIDO].
- Página posterior a la última: **NO VERIFICADO**. Propuesta: 32767 sin array.
- Orden de los resultados: **NO VERIFICADO**. Propuesta: estable por `codCtaCte` o por fecha de emisión.
- `consultarFacturasAgtDptoCltv` y `consultarCuentasEnAgtDptoCltv` no paginan.

### Cuando no hay datos

| Operación | Respuesta |
|---|---|
| `consultarComprobantes`, `consultarCtasCtes`, `consultarCuentasEnAgtDptoCltv`, `consultarFacturasAgtDptoCltv` | Sin el array de resultados; observación 32767 en `arrayObservaciones` (`arrayObservacion` en cuentas en agente) [MAN] págs. 38, 41, 45, 51 |
| `consultarComprobantes` con `codCtaCte` = 0 | "la búsqueda no arrojará resultados" → 32767 [MAN] pág. 37 |
| `consultarCtaCte`, operaciones sobre una cuenta | 1102 "No existe la cuenta corriente indicada" [MAN] pág. 18 |
| `rechazarNotaDC`, `obtenerRemitos`, historial de comprobante | 1105 "No existe el comprobante indicado" [MAN] pág. 18 |
| `obtenerRemitos` sin remitos | **NO VERIFICADO** (sin `arrayIdsRemitos` [INFERIDO]) |
| Tablas de parámetros vacías | **NO VERIFICADO** |

### Idempotencia y reproceso

No hay clave de idempotencia ni número de transacción. "Si se invoca a un método y no se obtiene respuesta, deberá utilizarse los métodos de consultas previstos" [MAN] pág. 15. Repetir una operación ya aplicada no es neutro, choca con el estado:

| Repetición | Respuesta |
|---|---|
| `aceptarFECred` sobre una cuenta ya `Aceptada`, `Rechazada`, etc. | 1108 [INFERIDO] |
| `rechazarFECred` repetido | 1108 [INFERIDO] |
| `rechazarNotaDC` sobre una nota ya `Rechazado` | 15000 [INFERIDO] |
| `informarCancelacionTotalFECred` repetido | 1108 [INFERIDO] |
| `informarFacturaAgtDptoCltv` repetido | 6000, 6001 o 6007, según el estado del informe [MAN] pág. 31 |
| `modificarOpcionTransferencia` con la misma opción | 7000 [MAN] pág. 35 |

- **1104** "La cuenta corriente fue recientemente modificada, actualice" sugiere un control de concurrencia: por ejemplo, entró una ND entre la consulta del comprador y su aceptación [INFERIDO]. El disparador exacto es **NO VERIFICADO**. Simularlo como falla inducible.
- **1103** "reinténtelo más tarde": falla transitoria inducible.
- Un simulador fiel debería permitir provocar un timeout **después** de aplicar la operación, para que el cliente pruebe la recuperación con `consultarCtaCte` o con los historiales (como en `wsfev1.md` §8.2).

### Estado que hay que guardar

1. **Comprobantes FCE** (compartidos con wsfev1/wsmtxca): todos los campos de `ComprobanteType`, más el historial de `EstadoCmpType`, los remitos asociados y el vínculo con la factura.
2. **Cuentas corrientes**: `codCtaCte`, factura, notas, estado e historial, `opcionTransferencia`, `cotizacionMonedaUlt`, los datos de aceptación (`tipoAcep`, `fechaHoraAcep`, formas, retenciones, ajustes, `importeCancelado`, `importeTotalRetPesos`, `importeEmbargoPesos`, `saldoAceptado`), los motivos de rechazo e `infoTransferencia`.
3. **Padrón simulado**: razón social, alta en el PUC, IVA/monotributo, actividad, DFE (1000-1004), condición de "empresa grande" u obligación con su monto por fecha, CBUs por CUIT con su moneda (2015, 2018) y cuentas en agentes por vendedor.
4. **Tablas**: retenciones (con porcentaje), motivos de rechazo, formas de cancelación (marcando las que exigen cancelación total) y ajustes.
5. **Reloj y plazos** configurables, y la cotización orientativa por moneda y fecha (2009).
6. **Fallas inducibles**: 1103, 1104, 10000 (con el código de seguimiento), 9000 (BCRA caído), Fault de autenticación, modo F5 (`BL...`), `dummy` con componentes que no están en `OK`.

### Protocolo

1. Ruta `/wsfecred/FECredService`; `?wsdl` sirve el WSDL con `soap:address` apuntando al simulador.
2. Sólo SOAP 1.1. Enrutar por `SOAPAction` y, si falta, por el elemento raíz del Body. Con Body vacío, sólo `SOAPAction` `.../dummy` responde; sin `SOAPAction` → Fault (o `BL` en modo F5). SOAP 1.2 → en el real sale la página HTML del WAF (opcional imitarla).
3. Respuestas con `<?xml version='1.0' encoding='UTF-8'?>`, prefijo `S:`, header `info` con namespace `https://...`, elemento de respuesta con `ns2:` y el namespace `http://...`, hijos sin namespace. `Content-Type: text/xml;charset=utf-8`.
4. Errores de negocio con HTTP 200 dentro del `...Return`; errores de esquema en `arrayErroresFormato`; autenticación y XML roto → Fault.

## No verificado

1. **Texto, `faultcode` y transporte reales de los errores de autenticación** (token vencido o mal formado, firma inválida, CUIT no representada). En vivo, todo sale enmascarado como `BL<n> <fecha> 500`. El manual sólo da `[wscommon_007] La firma no corresponde al token enviado.` con `faultcode` `ns3: Receiver`. Hace falta un certificado de homologación (y quizás una ruta que no pase por el F5) para verlo.
2. **Si los errores de esquema salen como Fault o en `arrayErroresFormato`**, y con qué textos (el manual se contradice; en vivo no se puede separar de la autenticación).
3. Transporte de 1000-1004 (Fault o `arrayErrores`).
4. **Valores de las cuatro tablas de parámetros** (retenciones, motivos de rechazo, formas de cancelación, ajustes de operación).
5. **Plazos legales** (notificación, aceptación, rechazo) y las reglas exactas de 6004, 6005 y 6006 sobre `fechaVenPago`.
6. **Fórmula de `saldoAceptado`** con cancelación parcial, retenciones, embargo y ajustes, y el margen de tolerancia de 2005, 2008 y 12002.
7. Cotización orientativa y alcance exacto del rango de 2009.
8. Cuál de los códigos que se solapan (2003/12009, 2004/12006, 2007/12000, 2008/12002, 2000-2002/12003-12004) emite hoy el servicio.
9. Orden de evaluación de las validaciones y si `arrayErrores` acumula varios códigos.
10. `resultado` exacto en cada caso (¿`R` con error de formato?, ¿`O` con 9000?) y qué `idCtaCte` se devuelve (¿eco o `codCtaCte` resuelto?).
11. Paginación: comportamiento sin `nroPagina`, página fuera de rango, tamaño real y orden.
12. Respuesta de los historiales (con `arrayHistorialEstados` obligatorio) cuando hay error, y su orden.
13. `obtenerRemitos` sin remitos; tablas vacías; `consultarFacturasAgtDptoCltv` sin filtros.
14. Si las ND/NC de anulación o post-aceptación (Tipo B) cambian el `saldo`, y qué es exactamente `importeTotalNotasDC` (¿suma neta ND − NC?).
15. Si una ND/NC puede rechazarse estando `PendienteRecepcion`.
16. Opción de transferencia e `infoSCA` de una cuenta aceptada tácitamente.
17. Si se puede volver a informar al ADC después de un rechazo del agente.
18. Si `saldoAceptado` aparece en cuentas no aceptadas (con 0) o se omite.
19. Si el header `info` aparece también en respuestas con error de negocio; formato de `fecha` en producción (el manual muestra `.970-03:00` en testing y sin zona en producción; homologación real hoy: sin milisegundos ni zona).
20. Tolerancia a hijos calificados con namespace (el esquema es `unqualified`).
21. Valores de `dummy` cuando un componente falla.
22. Límites de tasa, timeouts y tamaño máximo de request: el manual no los menciona.
23. Semántica de `codCtaCte` (rango, si es secuencial global).
