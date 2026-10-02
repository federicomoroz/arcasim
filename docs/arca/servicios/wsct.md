# wsct

**Comprobantes clase T** de ARCA (ex AFIP): autoriza con CAE las facturas, notas de débito y notas de crédito "T" que emiten los prestadores de **alojamiento** y las **agencias de viaje** por servicios turísticos prestados a **turistas extranjeros no residentes** (RG 3971). Lo distintivo frente a wsfev1/wsmtxca es el **reintegro del IVA** de la hotelería: el comprobante lleva un `importeReintegro` negativo igual al IVA de los ítems de alojamiento, y una **relación emisor-receptor** (hotel a turista, hotel a agencia, agencia a turista, etc.) que condiciona el tipo de documento, el país y las formas de pago del receptor.

Lo usan: emisores empadronados en una actividad de hospedaje/turismo o registrados en el padrón de agencias de la Secretaría de Turismo, inscriptos en IVA, con un punto de venta habilitado para web services [MAN pág. 18 y 33].

Fecha de relevamiento: **2026-10-02**.

Documento hermano: `wsct-codigos.json` (todos los códigos de la sección "Validaciones y errores", en formato de máquina).

### Fuentes

| Id | Fuente | URL / ubicación | Uso |
|---|---|---|---|
| **[MAN]** | "Factura Electrónica – Web Service Turismo – Manual para el desarrollador", **versión 1.6.4** (historial: 17/01/2025, RG 5616/2024), 144 págs. | <https://www.afip.gob.ar/ws/documentacion/manuales/Manual_Desarrollador_WSCT_v1.6.4.pdf> (enlazado desde <https://www.afip.gob.ar/ws/documentacion/ws-factura-electronica.asp>) | Fuente principal. Leído completo. Las tablas de validaciones se re-extrajeron con PyMuPDF (`find_tables`) y se miraron las páginas 30, 31 y 139 renderizadas, porque el texto plano las desarma. |
| **[WSDL]** | WSDL de homologación (todo el XSD inline, sin imports) | `docs/arca/wsdl/wsct-homologacion.wsdl` (= `https://fwshomo.afip.gov.ar/wsct/CTService?wsdl`). El de producción se bajó el 2026-10-02 y es idéntico salvo `soap:address` (no se guardó) | Estructura y tipos. **Manda sobre el manual** cuando difieren. |
| **[VIVO]** | Llamadas propias sin certificado a homologación, 2026-10-02 | `scratchpad/vivo/ct-*` (req/hdr/body) | Forma real de `dummy`, header `info`, máscara `BL…500`, SOAP 1.2, GET. |
| **[CAS]** | Respuestas reales de homologación grabadas en junio-julio 2021 en los tests de PyAfipWs (con certificado y TA válidos) | <https://github.com/PyAr/pyafipws/tree/main/tests/cassettes/test_wsct> y el cliente <https://github.com/PyAr/pyafipws/blob/main/wsct.py> | **Secundaria.** Única fuente con un TA real para wsct, un Fault de autenticación real y un `arrayErroresFormato` real. Datos de 2021: pueden haber cambiado. |
| **[TAB]** | Tabla de países de factura electrónica | `docs/arca/tablas/TABLA-PAISES-V.0-28072022.xlsx` (hoja "Paises FE") | Para leer los códigos de país que nombra la validación 355. |

Las páginas se citan como "pág. N" = página física del PDF (1..144; coincide con el "N de 144" del pie).

Marcas: **[MAN]**, **[WSDL]**, **[VIVO]** verificado en fuente oficial o en el servicio; **[CAS]** confirmado solo con fuente secundaria; **[INFERIDO]** deducido, sin texto oficial explícito; **NO VERIFICADO** no se pudo comprobar.

## Contrato

### Dialecto

**Java JAX-WS (implementación de referencia, Metro)** detrás del balanceador F5 de ARCA.

- `GET https://fwshomo.afip.gov.ar/wsct/CTService` (sin `?wsdl`) devuelve la página estándar de JAX-WS RI: "Service Name: `{http://ar.gob.afip.wsct/CTService/}CTService`", "Port Name: `{http://ar.gob.afip.wsct/CTService/}CTServiceSOAP`", "Implementation class: `ar.gov.afip.wsct.service.CTServicePortTypeImpl`" [VIVO, `ct-get.*`].
- En 2021 las respuestas traían `X-Powered-By: Servlet/3.0; JBossAS-6` [CAS]; en 2026 ese header ya no aparece [VIVO].
- Mismo dialecto que wsfecred (header `info` con namespace `https://`, `dummy` con Body vacío, `BL…500` ante cualquier Fault). Distinto de wsmtxca (según su manual, `soapenv:Fault` con `faultcode` `soapenv:Client`) y de los ASMX (wsfev1).

### Endpoints

| Ambiente | URL del servicio (`soap:address`) | WSDL | Fuente |
|---|---|---|---|
| Homologación | `https://fwshomo.afip.gov.ar:443/wsct/CTService` | `https://fwshomo.afip.gov.ar/wsct/CTService?wsdl` | [WSDL]; manual sin `:443`, pág. 17 |
| Producción | `https://serviciosjava.afip.gob.ar:443/wsct/CTService` | `https://serviciosjava.afip.gob.ar/wsct/CTService?wsdl` | WSDL de producción (2026-10-02); manual sin `:443`, pág. 18 |

Ojo: homologación es `.gov.ar` y producción `.gob.ar`. ArcaSim debería exponer la ruta `/wsct/CTService` y servir `?wsdl` con `soap:address` apuntando a sí mismo.

### Namespaces y forma del XML

- `targetNamespace` del WSDL y del esquema: **`http://ar.gob.afip.wsct/CTService/`** (con `http`, `.gob.ar`, barra final) [WSDL].
- El `xsd:schema` **no declara `elementFormDefault`**, así que vale el default `unqualified` [WSDL]. Consecuencia práctica:
  - Solo el **elemento raíz** de cada mensaje va calificado: `<cts:autorizarComprobanteRequest>`, `<ns2:autorizarComprobanteResponse>`.
  - **Todos los hijos van sin namespace**: `<authRequest>`, `<token>`, `<comprobanteRequest>`, `<arrayErrores>`, etc. Así los muestra el manual en todos los esquemas (págs. 19, 22, 46, …) y así responde el servicio [VIVO, CAS].
- Header de respuesta: `<info xmlns="https://ar.gob.afip.wsct/CTService/">` (con **`https`**, distinto del namespace del body) [MAN pág. 8; VIVO].
- Prefijos que usa el servidor: `S:` para el envelope SOAP 1.1 y `ns2:` para el namespace del servicio en el elemento raíz [VIVO, CAS]. El manual usa `cts:` en sus ejemplos de request.
- El manual tiene una errata en el ejemplo de dummy: `xmlns:ns2="http://ar.gob.afip.wsct/WSCTService/"` (pág. 125). El real es `http://ar.gob.afip.wsct/CTService/` [VIVO].

### Archivo WSDL

`docs/arca/wsdl/wsct-homologacion.wsdl` (1 565 líneas, un solo `xsd:schema` inline). `wsdl:definitions name="CTService"`, `portType` `CTServicePortType`, `binding` `CTServiceSOAP`, `service` `CTService`, `port` `CTServiceSOAP`.

### Service id de WSAA

**`wsct`** [CAS]. **El manual no lo dice**: se buscó en las 144 páginas y no hay ejemplo de TRA ni mención del `<service>` (pág. 7 solo remite "a los documentos correspondientes al Servicio de Autenticación y Autorización (WSAA)"). La página de ARCA que lista los servicios de factura electrónica lo nombra "(wsct)", pero esa misma página escribe "wsfev1" para un servicio cuyo id es `wsfe`, así que no prueba nada.

La evidencia que sí hay es secundaria y fuerte: los cassettes de PyAfipWs (2021) contienen un TA emitido por `wsaahomo` cuyo token decodificado dice `<login entity="33693450239" service="wsct" …>`, y con ese TA wsct respondió errores de negocio (`5000`) y de formato, no un Fault de autenticación. Es decir: WSAA homologación emitió TA para `wsct` y wsct lo aceptó. **NO VERIFICADO en fuente oficial** y no probado en 2026 (requiere certificado).

### SOAPAction

Patrón: `http://ar.gob.afip.wsct/CTService/` + nombre de la operación, para las 22 [WSDL]:

```
http://ar.gob.afip.wsct/CTService/dummy
http://ar.gob.afip.wsct/CTService/autorizarComprobante
http://ar.gob.afip.wsct/CTService/consultarComprobanteTipoPVentaNro
http://ar.gob.afip.wsct/CTService/consultarUltimoComprobanteAutorizado
http://ar.gob.afip.wsct/CTService/consultarPuntosVenta
http://ar.gob.afip.wsct/CTService/consultarTiposComprobantes
http://ar.gob.afip.wsct/CTService/consultarMonedas
http://ar.gob.afip.wsct/CTService/consultarCotizacion
http://ar.gob.afip.wsct/CTService/consultarTiposDocumento
http://ar.gob.afip.wsct/CTService/consultarPaises
http://ar.gob.afip.wsct/CTService/consultarCUITsPaises
http://ar.gob.afip.wsct/CTService/consultarTiposIVA
http://ar.gob.afip.wsct/CTService/consultarTiposDatosAdicionales
http://ar.gob.afip.wsct/CTService/consultarTiposTributo
http://ar.gob.afip.wsct/CTService/consultarCondicionesIVA
http://ar.gob.afip.wsct/CTService/consultarNovedades
http://ar.gob.afip.wsct/CTService/consultarFormasPago
http://ar.gob.afip.wsct/CTService/consultarTiposItem
http://ar.gob.afip.wsct/CTService/consultarCodigosItemTurismo
http://ar.gob.afip.wsct/CTService/consultarRelacionEmisorReceptor
http://ar.gob.afip.wsct/CTService/consultarTiposCuenta
http://ar.gob.afip.wsct/CTService/consultarTiposTarjeta
```

(Este es el orden del `binding`; el del `portType`, que usa la sección "Operaciones", tiene `consultarUltimoComprobanteAutorizado` antes de `consultarComprobanteTipoPVentaNro`.)

Comportamiento observado [VIVO, 2026-10-02]: el `SOAPAction` se acepta con o sin comillas (`"http://…/dummy"` y `http://…/dummy` dan 200). Sin `SOAPAction` y con Body vacío, el servicio no sabe qué operación es y responde `BL…500` (es decir, un Fault). Los clientes de PyAfipWs mandan el valor entre comillas [CAS].

### Elementos de cada mensaje

Todas las operaciones son `document/literal` con un `part` llamado `parameters` [WSDL]. Request `<op>Request`, response `<op>Response` y, adentro, un único hijo `<op>Return`, salvo las excepciones marcadas:

| Operación | Elemento de request | Elemento de response | Hijo de la response |
|---|---|---|---|
| `dummy` | **ninguno**: el mensaje `dummyRequest` no tiene `part`; Body vacío | `dummyResponse` | `dummyReturn` |
| `autorizarComprobante` | `autorizarComprobanteRequest` | `autorizarComprobanteResponse` | `autorizarComprobanteReturn` |
| `consultarUltimoComprobanteAutorizado` | `consultarUltimoComprobanteAutorizadoRequest` | `consultarUltimoComprobanteAutorizadoResponse` | `consultarUltimoComprobanteAutorizadoReturn` |
| `consultarComprobanteTipoPVentaNro` | `consultarComprobanteTipoPVentaNroRequest` | `consultarComprobanteTipoPVentaNroResponse` | **`consultarComprobanteReturn`** |
| `consultarPuntosVenta` | `consultarPuntosVentaRequest` | `consultarPuntosVentaResponse` | `consultarPuntosVentaReturn` |
| `consultarTiposComprobantes` | `consultarTiposComprobantesRequest` | `consultarTiposComprobantesResponse` | `consultarTiposComprobantesReturn` |
| `consultarMonedas` | `consultarMonedasRequest` | `consultarMonedasResponse` | `consultarMonedasReturn` |
| `consultarCotizacion` | `consultarCotizacionRequest` | `consultarCotizacionResponse` | `consultarCotizacionReturn` |
| `consultarTiposDocumento` | `consultarTiposDocumentoRequest` | `consultarTiposDocumentoResponse` | `consultarTiposDocumentoReturn` |
| `consultarPaises` | `consultarPaisesRequest` | `consultarPaisesResponse` | `consultarPaisesReturn` |
| `consultarCUITsPaises` | `consultarCUITsPaisesRequest` | `consultarCUITsPaisesResponse` | `consultarCUITsPaisesReturn` |
| `consultarTiposIVA` | `consultarTiposIVARequest` | `consultarTiposIVAResponse` | `consultarTiposIVAReturn` |
| `consultarTiposDatosAdicionales` | `consultarTiposDatosAdicionalesRequest` | `consultarTiposDatosAdicionalesResponse` | `consultarTiposDatosAdicionalesReturn` |
| `consultarTiposTributo` | `consultarTiposTributoRequest` | `consultarTiposTributoResponse` | `consultarTiposTributoReturn` |
| `consultarCondicionesIVA` | `consultarCondicionesIVARequest` | `consultarCondicionesIVAResponse` | `consultarCondicionesIVAReturn` |
| `consultarNovedades` | `consultarNovedadesRequest` | `consultarNovedadesResponse` | **`ConsultarNovedadesReturn`** (con **C mayúscula**) |
| `consultarFormasPago` | `consultarFormasPagoRequest` | `consultarFormasPagoResponse` | `consultarFormasPagoReturn` |
| `consultarTiposItem` | `consultarTiposItemRequest` | `consultarTiposItemResponse` | `consultarTiposItemReturn` |
| `consultarCodigosItemTurismo` | `consultarCodigosItemTurismoRequest` | `consultarCodigosItemTurismoResponse` | `consultarCodigosItemTurismoReturn` |
| `consultarRelacionEmisorReceptor` | `consultarRelacionEmisorReceptorRequest` | `consultarRelacionEmisorReceptorResponse` | `consultarRelacionEmisorReceptorReturn` |
| `consultarTiposCuenta` | `consultarTiposCuentaRequest` | `consultarTiposCuentaResponse` | `consultarTiposCuentaReturn` |
| `consultarTiposTarjeta` | `consultarTiposTarjetaRequest` | `consultarTiposTarjetaResponse` | `consultarTiposTarjetaReturn` |

### SOAP 1.1 y 1.2

El WSDL declara **un solo binding**, `CTServiceSOAP`, con `soap:binding` (SOAP 1.1), `style="document"`, transporte HTTP, y `soap:body use="literal"` en todas las operaciones [WSDL]. No hay binding SOAP 1.2.

| Caso [VIVO 2026-10-02] | Request | Respuesta |
|---|---|---|
| `dummy` SOAP 1.1, Body vacío, `SOAPAction` correcto (con o sin comillas) | `ct-dummy-quotedaction`, `ct-dummy-unquotedaction`, `ct-dummy-emptybody` | `HTTP/1.1 200`, `Content-Type: text/xml;charset=utf-8`, `Transfer-Encoding: chunked`, cookies F5 (`f5avr…_session_`, `TS01761d9e`), `Strict-Transport-Security`, `X-XSS-Protection`, `X-Frame-Options: sameorigin`, `X-Content-Type-Options: nosniff`. Body SOAP (ver `dummy`) |
| `dummy` con `<x:dummy/>` dentro del Body (con o sin `SOAPAction`) | `ct-dummy`, `ct-dummy-elem-action` | `BL<n> <fecha> 500` (ver abajo) |
| Body vacío sin `SOAPAction` | `ct-dummy-noaction` | `BL<n> <fecha> 500` |
| `SOAPAction` inexistente (`…/noExiste`) | `ct-dummy-badaction` | `BL<n> <fecha> 500` |
| XML mal formado (cortado) | `ct-malformed` | `BL<n> <fecha> 500` |
| SOAP 1.2 (`application/soap+xml;…;action="…/dummy"`, envelope `http://www.w3.org/2003/05/soap-envelope`) | `ct-dummy-soap12` | `HTTP/1.1 200`, `Content-Type: text/html; charset=utf-8`, `Connection: close`, 81 bytes: `<html><head><title></title>5794109048105226820</head><body><br><br></body></html>`. Es una página de bloqueo del WAF, no SOAP |
| `GET /wsct/CTService` | `ct-get` | 200 `text/html`, página de endpoints de JAX-WS RI (ver "Dialecto") |

**La máscara `BL…500`.** Cualquier respuesta que en el backend sería HTTP 500 (Fault) llega al cliente como `HTTP/1.0 200 OK`, `Connection: Keep-Alive`, **sin `Content-Type`**, con `Content-Length` 38 o 39 y un body de texto plano: `BL` + un número de 12 o 13 dígitos + espacio + `aaaa-mm-dd hh:mm:ss` (hora de Argentina) + ` 500`. Ejemplos: `BL6197492664771 2026-10-02 15:07:36 500`, `BL290735592269 2026-10-02 15:15:13 500` [VIVO]. **[INFERIDO, evidencia fuerte]** lo pone el F5: en 2021, sin esa máscara, el mismo servicio devolvía el Fault con HTTP 500 [CAS, ver "Autenticación"].

### Header de respuesta (`info`)

Toda respuesta SOAP trae un `S:Header` informativo, de procesamiento opcional para el cliente [MAN pág. 8]:

```xml
<S:Header><info xmlns="https://ar.gob.afip.wsct/CTService/"><ambiente>Producción - FI1</ambiente><fecha>2026-10-02 15:08:22</fecha><id>1.6.4</id></info></S:Header>
```

| Fuente | `ambiente` | `fecha` | `id` |
|---|---|---|---|
| [VIVO] homologación 2026-10-02 | `Producción - FI1` (sí: homologación dice "Producción", con tilde) | `2026-10-02 15:08:22`: espacio en vez de `T`, sin milisegundos ni zona | `1.6.4` (= versión del manual) |
| [CAS] homologación 2021 | `Testing - VII` | `2021-06-20 13:45:06` (mismo formato) | no existía |
| [MAN] págs. 8-10, 127 | `Testing - vii` / `Produccion - bus` | `2017-06-22T17:49:06.970-03:00` | no figura |

El esquema genérico del manual pone `<fecha>date</fecha>` (págs. 65, 68, …), pero el valor real es fecha y hora. El header **también** aparece en las respuestas Fault [CAS]. El manual lista "estructura general opcional en los mensajes de respuesta" como cambio de v1.1 (pág. 141).

### Serialización de las respuestas

[VIVO, CAS]: declaración `<?xml version='1.0' encoding='UTF-8'?>` (comillas simples), todo en una línea, envelope `S:` = `http://schemas.xmlsoap.org/soap/envelope/`, elemento raíz con `xmlns:ns2="http://ar.gob.afip.wsct/CTService/"`, hijos sin namespace. Los elementos opcionales sin dato se omiten (no se emiten vacíos) [CAS].

## Autenticación

### Bloque `authRequest`

Todas las operaciones menos `dummy` llevan `authRequest` (tipo `AuthRequestType`) como **primer hijo, 1..1** [WSDL]:

| Campo | Tipo XSD | Ocurrencia XSD | Oblig. manual | Long. manual | Descripción [MAN págs. 19-20, 129] |
|---|---|---|---|---|---|
| `token` | `xsd:string` | 1..1 | S | — ("variable, depende de la respuesta del WSAA", pág. 143) | Token devuelto por el WSAA |
| `sign` | `xsd:string` | 1..1 | S | — | Signature devuelta por el WSAA |
| `cuitRepresentada` | `CuitSimpleType` (`long`, > 9999999999 y ≤ 99999999999) | 1..1 | S | 11 | CUIT de la entidad representada (el emisor) |

Reglas [MAN pág. 20]: "Se validará en todos los casos que la CUIT informante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional." Error excepcional = SOAP Fault (ver abajo).

Además, en producción se validan los datos del emisor (`cuitRepresentada`) con los códigos 100 a 104 (pág. 18), que **no se aplican en homologación** (pág. 17). Están en la tabla de "Validaciones y errores".

### Falla de autenticación según el manual: SOAP Fault

"Los errores excepcionales serán del tipo descriptivo" e "incluyen también errores graves de estructura XML (ej: tags sin cerrar)" [MAN págs. 10-11]. Ejemplo del manual (pág. 10):

```xml
<S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
<S:Header>
<info xmlns="https://ar.gob.afip.wsct/CTService/">
          <ambiente>Testing - vii</ambiente>
          <fecha>2017-06-22T17:49:06.970-03:00</fecha>
</info>
</S:Header>
   <S:Body>
      <ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:ns3="http://www.w3.org/2003/05/soap-envelope">
         <faultcode>ns3: Receiver</faultcode>
         <faultstring>[wscommon_007] La firma no corresponde al token
          enviado.</faultstring>
      </ns2:Fault>
   </S:Body>
</S:Envelope>
```

Fault real de 2021 (token vencido), **HTTP 500 `Internal Server Error`**, `Content-Type: text/xml;charset=utf-8` [CAS, `test_main_prueba.yaml`]:

```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Header><info xmlns="https://ar.gob.afip.wsct/CTService/"><ambiente>Testing - VII</ambiente><fecha>2021-07-22 18:38:27</fecha></info></S:Header><S:Body><ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns3="http://www.w3.org/2003/05/soap-envelope"><faultcode>ns3: Receiver</faultcode><faultstring>[wscommon_002] Token vencido Fecha y Hora de Vencimiento del Token Enviado: 21-06-2021 01:44:40 - Fecha y Hora Actual del Servidor: 22-07-2021 18:38:27</faultstring></ns2:Fault></S:Body></S:Envelope>
```

Detalles que un cliente puede notar:
- `faultcode` es el texto literal `ns3: Receiver` **con un espacio** después de los dos puntos (no es un QName válido), con `ns3` declarado como el namespace de SOAP 1.2 aunque el envelope es SOAP 1.1.
- Sin `<detail>`. Sin `<faultactor>`.
- El `faultstring` empieza con un código entre corchetes `[wscommon_NNN]`. Se conocen dos: `wscommon_002` (token vencido, [CAS]) y `wscommon_007` (firma que no corresponde al token, [MAN]). Los demás (token mal formado, CUIT no representada, etc.) son **NO VERIFICADO**.
- Formato de fechas dentro del texto: `dd-mm-aaaa hh:mm:ss`.

### Falla de autenticación en vivo (2026): `BL…500`

Token `abc`, token XML bien formado en base64 con firma falsa, o cualquier request con autenticación inválida → `HTTP/1.0 200`, sin `Content-Type`, body `BL<n> <aaaa-mm-dd hh:mm:ss> 500` [VIVO, `ct-badtoken`, `ct-fakesign`, `ct-badtoken-noaction`]. Lo mismo con un request que **además** viola el esquema (`numeroPuntoVenta` 0, `ct-fmt-ptovta0`; `cuitRepresentada` = `abc`, `ct-fmt-cuitabc`): con token inválido no se llega a ver `arrayErroresFormato`. **[INFERIDO]** el token se valida antes que el formato del resto del request (con token válido, el formato sí sale en `arrayErroresFormato`, [CAS]).

Para ArcaSim: emitir el Fault real del manual (HTTP 500, estructura de arriba) y ofrecer un modo opcional "F5" que lo reemplace por `HTTP/1.0 200` + `BL<n> <fecha> 500` sin `Content-Type`.

## Operaciones

22 operaciones en el `portType` `CTServicePortType` [WSDL]; el manual documenta las 22 (págs. 21-127). Abajo van en el orden del `portType`. Primero, los tipos compartidos.

Convención de las tablas: "XSD" = tipo y `minOccurs..maxOccurs` del WSDL; "Man." = columna "Oblig." del manual (S/N: obligatorio "a nivel estructura", pág. 143); "Long." = columna "Longitud (máx)" del manual. Cuando el manual y el WSDL difieren se anota y **manda el WSDL** para la estructura.

Reglas generales del manual (pág. 143): `date` = `AAAA-MM-DD` sin huso; `dateTime` = `AAAA-MM-DDThh:mm:ss[Z|(+|-)hh:mm]`; separador decimal `.`; "Cuando un elemento es opcional y no se desea enviar ningún valor para este, no deberá enviarse el tag"; redondeo **Round Half Even**; error absoluto = |calculado − real|; error relativo = error absoluto / |real|.

### Tipos simples (restricciones del XSD)

El XSD **no tiene ningún `xsd:pattern`**. Todas las restricciones son de rango, longitud, dígitos o enumeración [WSDL]:

| Tipo | Base | Restricción XSD exacta | Manual (pág. 128) | Diferencia / nota |
|---|---|---|---|---|
| `CuitSimpleType` | `xsd:long` | `minExclusive 9999999999`, `maxInclusive 99999999999` (o sea 10000000000..99999999999) | "Valor numérico con un total de 11 dígitos" | Coinciden |
| `NumeroPuntoVentaSimpleType` | `xsd:short` | `minInclusive 1`, `maxInclusive 9999` | "desde 1 a 9999" | Coinciden. (wsfev1 admite hasta 99998) |
| `NumeroComprobanteSimpleType` | `xsd:long` | `minInclusive 1`, `maxInclusive 99999999` | "desde 1 hasta 99999999" | Coinciden. **0 es inválido**: un "último comprobante = 0" no se puede devolver en este tipo |
| `CodigoTipoAutorizacionSimpleType` | `xsd:string` | `enumeration A`, `E` | `{ 'A', 'E' }` | Solo `E` (CAE) es aceptado por negocio (201) |
| `ImporteSimpleType` | `xsd:decimal` | `minInclusive -9999999999999.99`, `maxInclusive 9999999999999.99`, `totalDigits 15`, `fractionDigits 2` | "Total de dígitos 15 (13 enteros y 2 decimales)", mismo intervalo | Coinciden. Admite negativos |
| `ImporteNoNegativoSimpleType` | `xsd:decimal` | `minInclusive 0`, `maxInclusive 9999999999999.99` (**sin** `totalDigits` ni `fractionDigits`) | "Total de dígitos 15 (13 enteros y 2 decimales)", 0..9999999999999.99 | **El XSD no limita los decimales**: `1.234` pasa el esquema [INFERIDO]. El manual dice 2 |
| `SiNoSimpleType` | `xsd:string` | `enumeration S`, `N` | `{ 'S', 'N' }` | El manual dice que `cancelaEnMismaMonedaExtranjera` "Puede ser S, N o Vacío" (pág. 132): vacío **viola** el enum del XSD |
| `ResultadoSimpleType` | `xsd:string` | `enumeration A`, `O`, `R` | `{ 'A', 'O', 'R' }` | Coinciden |
| `Texto50SimpleType` | `xsd:string` | `minLength 3`, `maxLength 50` | "Alfanumérico hasta 50 caracteres" | **El manual omite el mínimo de 3**. Afecta `item/codigo` y `otroTributo/descripcion` |
| `Texto4000SimpleType` | `xsd:string` | `minLength 1`, `maxLength 4000` | "Alfanumérico hasta 4000 caracteres" | Manual omite el mínimo de 1 |
| `SwiftCodeSimpleType` | `xsd:string` | `minLength 8`, `maxLength 11` | "Alfanumérico de 8 a 11 caracteres" | Sin patrón: no se valida el formato BIC |
| `TipoCuentaSimpleType` | `xsd:short` | `minInclusive 0`, `maxInclusive 99` | "entre 0 y 99" | Coinciden |
| `TipoTarjetaSimpleType` | `xsd:short` | `minInclusive 0`, `maxInclusive 99` | "entre 0 y 99" | Coinciden |
| `NumeroCuentaSimpleType` | `xsd:decimal` | `minExclusive 9999`, `maxInclusive 99999999999999999999` (20 nueves), `fractionDigits 0` | "Valor numérico de 20 dígitos" | XSD: entero de 5 a 20 dígitos (≥ 10000); manual habla de 20 |
| `NumeroTarjetaSeisPrimerosSimpleType` | `xsd:long` | `minExclusive 99999`, `maxInclusive 999999` (100000..999999) | "Valor numérico de 6 dígitos" | Un BIN que empiece con 0 no se puede expresar |

Tipos XSD nativos que restringen por sí mismos: `xsd:short` (−32768..32767: todos los códigos de tipo, documento, país, IVA, tributo, ítem, relación, forma de pago, dato adicional y el `codigo` de errores), `xsd:long`, `xsd:decimal`, `xsd:date` (`AAAA-MM-DD`, admite zona opcional por XSD).

### Tipos complejos compartidos

**`AuthRequestType`**: ver "Autenticación".

**`CodigoDescripcionType`** (elemento `codigoDescripcion`) — usado en `arrayErrores`, `arrayObservaciones` y varias tablas de parámetros:

| Campo | XSD | Man. | Long. | Descripción [MAN pág. 129] |
|---|---|---|---|---|
| `codigo` | `xsd:short` 1..1 | S | 5 | Código |
| `descripcion` | `xsd:string` 1..1 | S | 2000 | Descripción |

**`CodigoDescripcionStringType`** (elemento `codigoDescripcionString`) — usado en `arrayErroresFormato` y en tablas de parámetros con código alfanumérico:

| Campo | XSD | Man. | Long. | Descripción [MAN pág. 130] |
|---|---|---|---|---|
| `codigo` | `xsd:string` 1..1 | S | 100 | Código |
| `descripcion` | `xsd:string` 1..1 | S | 2000 | Descripción |

**`ArrayCodigosDescripcionesType`** = secuencia de `codigoDescripcion` **1..unbounded**. **`ArrayCodigosDescripcionesStringType`** = secuencia de `codigoDescripcionString` **1..unbounded**. Como el mínimo es 1, una lista vacía no se puede emitir como `<arrayX/>`: el array entero se omite [INFERIDO del XSD].

**Errores en las respuestas** [MAN págs. 11-13, 15]:
- `arrayErroresFormato` (`ArrayCodigosDescripcionesStringType`): errores de validación del request contra el esquema. "De no superar alguna de las validaciones de formato, el WS devolverá el arrayErroresFormato y no continuará con las validaciones de negocio, por lo cual no existirá el elemento arrayErrores. Son excluyentes" (pág. 12). Los códigos son los del validador de esquemas (Xerces), con texto en español. Ejemplos del manual (pág. 12): `cvc-datatype-valid.1.2.1` / `'?' no es un valor válido para un tipo de dato entero.` y `cvc-type.3.1.3` / `El valor '?' en el elemento 'cuitRepresentada' no es válido.`. Ejemplo real [CAS 2021], **HTTP 200**: `<arrayErroresFormato><codigoDescripcionString><codigo>cvc-minInclusive-valid</codigo><descripcion> El valor '0' no cumple con la restricción minInclusive '1' para el tipo 'NumeroComprobanteSimpleType'.</descripcion></codigoDescripcionString><codigoDescripcionString><codigo>cvc-type.3.1.3</codigo><descripcion> El valor '0' del elemento 'numeroComprobante' no es válido.</descripcion></codigoDescripcionString></arrayErroresFormato>` (ojo: la `descripcion` real empieza con un **espacio**).
- `arrayErrores` (`ArrayCodigosDescripcionesType`): validaciones de negocio no superadas y errores de aplicación (5000).
- `arrayObservaciones` (`ArrayCodigosDescripcionesType`): solo en `autorizarComprobante` y `consultarComprobanteTipoPVentaNro`.
- El manual dibuja `arrayErrores` con `codigo`/`descripcion` directos (pág. 13), y en algunos esquemas con `<codigo>string</codigo>` (págs. 75, 81, 88). Según el WSDL siempre es `arrayErrores/codigoDescripcion/{codigo short, descripcion}`; manda el WSDL.

**`ComprobanteType`** (elemento `comprobanteRequest` en `autorizarComprobante`; elemento `comprobante` en la respuesta de `consultarComprobanteTipoPVentaNro`). Orden de esquema [WSDL] con lo que dice el manual (págs. 130-132, más las validaciones de págs. 29-45):

| # | Campo | XSD | Man. | Long. | Significado y restricciones | Validaciones |
|---:|---|---|---|---|---|---|
| 1 | `codigoTipoComprobante` | `xsd:short` 1..1 | S | 4 (3 en otras tablas) | Tipo de comprobante; ver `consultarTiposComprobantes`. Conocidos: 195 Factura T, 196 Nota de Débito T, 197 Nota de Crédito T (por las validaciones) | 300, 318, 415, 800 |
| 2 | `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` 1..1 | S | 4 | Punto de venta por el que se emite | 301, 104 |
| 3 | `numeroComprobante` | `NumeroComprobanteSimpleType` 1..1 | S | 8 | Número; debe ser el próximo a autorizar | 302 |
| 4 | `fechaEmision` | `xsd:date` 0..1 | N | — | Fecha de emisión. Si se envía, hasta 10 días antes o después de la fecha de generación; si no, se asigna la fecha de proceso | 303, 805 |
| 5 | `codigoTipoAutorizacion` | `CodigoTipoAutorizacionSimpleType` 0..1 | N | 1 | `E` = CAE, `A` = CAEA. "CAEA no autorizado en esta versión" (pág. 131). Aunque el XSD lo hace opcional, la validación 200 dice "Siempre debe informarse" | 200, 201 |
| 6 | `codigoAutorizacion` | `xsd:long` 0..1 | N | 14 | Código de autorización. En el request con `E` no se informa; en la consulta trae el CAE [INFERIDO] | 202 |
| 7 | `fechaVencimiento` | `xsd:date` 0..1 | N | — | Vencimiento del código de autorización. En el request con `E` no se informa; en la consulta trae el vencimiento del CAE [INFERIDO] | 203 |
| 8 | `codigoTipoDocumento` | `xsd:short` 1..1 | S | 2 | Tipo de documento del receptor; ver `consultarTiposDocumento`. Conocidos: 80 CUIT, 91 CI Extranjera, 94 Pasaporte, 96 DNI (validación 353) | 309, 311, 313, 353, 356 |
| 9 | `numeroDocumento` | **`xsd:string`** 1..1 | S | 11 | Número de documento del receptor. **Discrepancia:** el manual dice `long` (pág. 131); el WSDL `string`, coherente con 312 y 315 que piden "valor alfanumérico" para pasaporte/CI | 310, 312, 314, 315, 316, 354 |
| 10 | `idImpositivo` | `xsd:string` 1..1 | — | — | "Identificador impositivo o condición de IVA del receptor"; ver `consultarCondicionesIVA`. Valores nombrados: "IVA Responsable Inscripto", "Consumidor Final", "Cliente del Exterior" (sus códigos no figuran). **No está en la tabla de `ComprobanteType` del manual** | 308-316, 356 |
| 11 | `codigoPais` | `xsd:short` 0..1 | — | — | País del receptor; ver `consultarPaises`. **No está en la tabla del manual** | 307, 355 |
| 12 | `domicilioReceptor` | `xsd:string` 0..1 | — | 300 (por 350) | "El domicilio del receptor es obligatorio informarlo. Su dimensión máxima son 300 caracteres alfanuméricos" (350). XSD opcional y sin largo. **No está en la tabla del manual** | 350 |
| 13 | `codigoRelacionEmisorReceptor` | `xsd:short` 1..1 | — | — | Relación entre emisor y receptor (1 a 6, ver "Tablas y datos"); ver `consultarRelacionEmisorReceptor`. **No está en la tabla del manual** | 351-356, 724, 732 |
| 14 | `importeGravado` | `ImporteNoNegativoSimpleType` 0..1 | N | 15.2 | Neto gravado. Manual: `ImporteSimpleType`. Por 360 es obligatorio y ≥ 0 | 360, 361, 369 |
| 15 | `importeNoGravado` | `ImporteNoNegativoSimpleType` 0..1 | N | 15.2 | No gravado. "No informar el campo. Previsto para alícuotas de IVA futuras. Si informa el campo, el mismo debe venir en cero" | 362, 369 |
| 16 | `importeExento` | `ImporteNoNegativoSimpleType` 0..1 | N | 15.2 | Exento. Misma regla que el anterior | 363, 369 |
| 17 | `importeOtrosTributos` | `ImporteNoNegativoSimpleType` 0..1 | N | 15.2 | Total de otros tributos; = suma de `otroTributo/importe` | 367, 368, 369 |
| 18 | `importeReintegro` | `ImporteSimpleType` 0..1 | — | — | Reintegro del IVA de alojamiento: **≤ 0**, obligatorio si hay ítems con `codigoTurismo` 1 o 2 (364). **No está en la tabla del manual** | 364, 365, 366, 369 |
| 19 | `importeTotal` | `ImporteNoNegativoSimpleType` 1..1 | S | 15.2 | Total del comprobante. Manual: `ImporteSimpleType` | 369, 807 |
| 20 | `codigoMoneda` | `xsd:string` 1..1 | S | 3 | Moneda; ver `consultarMonedas`. `PES` = pesos | 304, 305, 306, 319, 320 |
| 21 | `cotizacionMoneda` | `xsd:decimal` **0..1** | **S** | 10.6 | "Total de dígitos 10 (4 enteros y 6 decimales). Mayor a cero. Máximo permitido: 9999.999999" (pág. 131). El XSD no restringe nada. Se volvió opcional en v1.6.4 (pág. 142): obligatorio salvo el caso de 322 | 305, 306, 317, 320, 322 |
| 22 | `cancelaEnMismaMonedaExtranjera` | `SiNoSimpleType` 0..1 | N | 1 | Indica que la factura (no habilitado para notas de crédito y débito) se paga en la misma moneda extranjera. Agregado en v1.6.4 (RG 5616) | 318, 319, 320, 322 |
| 23 | `observaciones` | `xsd:string` 0..1 | N | 2000 | "Observaciones comerciales (Importante: NO es necesario completar con espacios)". XSD sin largo | — |
| 24 | `arrayItems` | `ArrayItemsType` **1..1** | S | — | Ítems (1..n `item`) | 400-415 |
| 25 | `arrayComprobantesAsociados` | `ArrayComprobantesAsociadosType` 0..1 | N | — | Comprobantes asociados (1..n `comprobanteAsociado`) | 800-807 |
| 26 | `arrayOtrosTributos` | `ArrayOtrosTributosType` 0..1 | N | — | Otros tributos (1..n `otroTributo`) | 600-604, 368 |
| 27 | `arraySubtotalesIVA` | `ArraySubtotalesIVAType` 0..1 | N | — | Subtotales de IVA (1..n `subtotalIVA`). **Por 500 es obligatorio** | 500-504 |
| 28 | `arrayDatosAdicionales` | `ArrayTiposDatosAdicionalesType` 0..1 | N | — | Datos adicionales (1..n `tipoDatoAdicional`) | 900 |
| 29 | `arrayFormasPago` | `ArrayFormasPagoType` 0..1 | N | — | Formas de pago (1..n `formaPago`) | 700-732 |

**`ItemType`** (`arrayItems/item`, 1..unbounded) [WSDL; MAN pág. 135]:

| Campo | XSD | Man. | Long. | Descripción | Validaciones |
|---|---|---|---|---|---|
| `tipo` | `xsd:short` 1..1 | S | 3 | Tipo de ítem; ver `consultarTiposItem`. Conocidos: 0 Item general, 97 Anticipo, 99 Descuento General | 400, 406-411 |
| `codigoTurismo` | `xsd:short` **0..1** | **S** | 3 | Código de turismo; ver `consultarCodigosItemTurismo`. Conocidos: 1 hotelería sin desayuno, 2 hotelería con desayuno, 5 Excedente | 401, 364-366, 415 |
| `codigo` | `Texto50SimpleType` 0..1 | N | 50 | Código interno del emisor. XSD: **3 a 50** caracteres | — |
| `descripcion` | `Texto4000SimpleType` 1..1 | S | 4000 | Descripción del ítem (1 a 4000) | 404 |
| `codigoAlicuotaIVA` | `xsd:short` 1..1 | S | 2 | Alícuota de IVA; ver `consultarTiposIVA`. Solo se acepta 5 (21 %) | 403, 412, 413, 504 |
| `importeIVA` | `ImporteSimpleType` 1..1 | S | — | IVA del ítem | 409-411, 413, 414, 504 |
| `importeItem` | `ImporteSimpleType` 1..1 | S | — | "Importe total del Item" (con IVA, por 361) | 406-408, 413, 414 |

Errata del manual: en los esquemas de págs. 23 y 49 el campo se llama `<codigoCondicionIVA>`; en la tabla (pág. 135), en las validaciones y en el WSDL es `codigoAlicuotaIVA`. Manda el WSDL.

**`ComprobanteAsociadoType`** (`arrayComprobantesAsociados/comprobanteAsociado`, 1..unbounded) [MAN pág. 133]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `codigoTipoComprobante` | `xsd:short` 1..1 | S | 3 | Tipo del comprobante asociado; ver `consultarTiposComprobantes` |
| `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` 1..1 | S | 4 | Punto de venta del asociado |
| `numeroComprobante` | `NumeroComprobanteSimpleType` 1..1 | S | 8 | Número del asociado |

No lleva CUIT ni fecha: el asociado tiene que ser del mismo emisor (803).

**`OtroTributoType`** (`arrayOtrosTributos/otroTributo`, 1..unbounded) [MAN pág. 134]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `codigo` | `xsd:short` 1..1 | S | 2 | Código de tributo; ver `consultarTiposTributo` (600). 99 = "Otro" (602) |
| `descripcion` | `Texto50SimpleType` 0..1 | N | 50 | Descripción. XSD 3..50. Obligatoria para el código 99 (602) |
| `baseImponible` | `ImporteSimpleType` 0..1 | N | — | Base imponible; si se informa, ≥ 0 (603) |
| `importe` | `ImporteSimpleType` 1..1 | S | — | Importe del tributo; ≥ 0 (604) |

**`SubtotalIVAType`** (`arraySubtotalesIVA/subtotalIVA`, 1..unbounded) [MAN pág. 136]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `codigo` | `xsd:short` 1..1 | S | 3 | Alícuota; ver `consultarTiposIVA` (501). Sin repetir (502) |
| `importe` | `ImporteSimpleType` 1..1 | S | — | IVA total de esa alícuota; ≥ 0 (503); = suma de `importeIVA` de los ítems con esa alícuota (504). (El manual lo describe como "Campo multipropósito": errata) |

No hay base imponible por alícuota (a diferencia de wsfev1 `AlicIva/BaseImp`).

**`TipoDatoAdicionalType`** (`arrayDatosAdicionales/tipoDatoAdicional`, 1..unbounded) [MAN pág. 137]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `t` | `xsd:short` 1..1 | S | 3 | Tipo de dato adicional "según RG"; ver `consultarTiposDatosAdicionales` (900) |
| `c1` … `c6` | `xsd:string` 0..1 cada uno | N | 50 c/u | "Campo multipropósito". El XSD no limita el largo |

**`FormaPagoType`** (`arrayFormasPago/formaPago`, 1..unbounded) [WSDL; MAN pág. 138]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `codigo` | `xsd:short` **1..1** | **N** | 3 | Forma de pago; ver `consultarFormasPago` (700) |
| `tipoTarjeta` | `TipoTarjetaSimpleType` 0..1 | N | 2 | Tipo de tarjeta; ver `consultarTiposTarjeta` |
| `numeroTarjeta` | `NumeroTarjetaSeisPrimerosSimpleType` 0..1 | N | 6 | Solo los primeros 6 dígitos |
| `swiftCode` | `SwiftCodeSimpleType` 0..1 | N | 11 | Código SWIFT |
| `tipoCuenta` | `TipoCuentaSimpleType` 0..1 | N | 2 | Tipo de cuenta; ver `consultarTiposCuenta` |
| `numeroCuenta` | `NumeroCuentaSimpleType` 0..1 | N | 20 | Número de cuenta |

Discrepancias: el esquema de ejemplo del manual (págs. 24 y 50-51) pone otro orden (`codigo`, `swiftCode`, `tipoCuenta`, `numeroCuenta`, `numeroTarjeta`), omite `tipoTarjeta` y agrega un `<importe>` que **no existe** en el WSDL. Como es una `xsd:sequence` validada contra el esquema, el orden del WSDL es el que vale [INFERIDO: el servicio valida con Xerces, ver `arrayErroresFormato`].

**`ComprobanteResponseType`** (`autorizarComprobanteReturn/comprobanteResponse`) [WSDL; MAN págs. 28-29]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `cuit` | `CuitSimpleType` 1..1 | S | 11 | CUIT del emisor |
| `codigoTipoComprobante` | `xsd:short` 1..1 | S | 3 | Tipo autorizado |
| `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` 1..1 | S | 4 | Punto de venta autorizado |
| `numeroComprobante` | `NumeroComprobanteSimpleType` 1..1 | S | 8 | Número autorizado |
| `fechaEmision` | `xsd:date` 1..1 | S | — | Fecha de emisión (la enviada o la de proceso) |
| `CAE` | `xsd:long` 1..1 | S | 14 | Código de autorización electrónico (nombre del elemento en mayúsculas) |
| `fechaVencimientoCAE` | `xsd:date` 1..1 | S | — | Vencimiento del CAE |

**`PuntosVentaType`** (`arrayPuntosVenta/puntoVenta`, 1..unbounded) [WSDL; MAN pág. 62]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` 1..1 | S | 5 | Número de punto de venta |
| `bloqueado` | `SiNoSimpleType` 1..1 | S | 1 | "‘Si’: Bloqueado, ‘No’: No Bloqueado" (los valores reales del enum son `S`/`N`) |
| `fechaBaja` | `xsd:date` 0..1 | N | — | Fecha de baja, `AAAA-MM-DD` |

Envelope de request para todos los ejemplos (prefijo del manual):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:cts="http://ar.gob.afip.wsct/CTService/">
  <soapenv:Header/>
  <soapenv:Body> … </soapenv:Body>
</soapenv:Envelope>
```

Las respuestas llegan como en "Serialización de las respuestas" (con `S:Header/info`). Abajo se muestra solo el contenido del Body.

### 1. `dummy`

Propósito: "Permite verificar el funcionamiento del presente WS"; "Retorna el resultado de la verificación de los elementos principales de infraestructura del servicio" [MAN pág. 125]. **Sin autenticación.**

Request: **Body vacío** (`<soapenv:Body/>`) más `SOAPAction: http://ar.gob.afip.wsct/CTService/dummy` [MAN pág. 125; WSDL: `dummyRequest` sin `part`]. Mandar `<cts:dummy/>` dentro del Body da Fault (`BL…500`) [VIVO].

Response `dummyResponse/dummyReturn` (`DummyReturnType`):

| Campo | XSD | Man. | Descripción [MAN pág. 126] |
|---|---|---|---|
| `appserver` | `xsd:string` 1..1 | S | Servidor de aplicaciones |
| `authserver` | `xsd:string` 1..1 | S | Servidor de autenticación |
| `dbserver` | `xsd:string` 1..1 | S | Servidor de base de datos |

Ejemplo real [VIVO 2026-10-02, `ct-dummy-unquotedaction`], completo:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"><soapenv:Header/><soapenv:Body/></soapenv:Envelope>
```
```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Header><info xmlns="https://ar.gob.afip.wsct/CTService/"><ambiente>Producción - FI1</ambiente><fecha>2026-10-02 15:15:13</fecha><id>1.6.4</id></info></S:Header><S:Body><ns2:dummyResponse xmlns:ns2="http://ar.gob.afip.wsct/CTService/"><dummyReturn><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></dummyReturn></ns2:dummyResponse></S:Body></S:Envelope>
```

El orden es `appserver`, `authserver`, `dbserver` (wsfev1 usa `AppServer`, `DbServer`, `AuthServer`). Valores distintos de `OK`: **NO VERIFICADO**.

### 2. `autorizarComprobante`

Propósito: "autorizar un comprobante clase T identificando, emisor, receptor, tipo de comprobante, punto de venta, número y fecha de emisión" [MAN pág. 21]. Tres salidas posibles: supera todo → aprobado con CAE; no supera validaciones **no excluyentes** → aprobado con observaciones y CAE; no supera alguna **excluyente** → rechazado (pág. 21). Un comprobante por llamada (no hay lotes).

Request `autorizarComprobanteRequest` (`AutorizarComprobanteRequestType`):

| Campo | XSD | Man. | Descripción [MAN pág. 25] |
|---|---|---|---|
| `authRequest` | `AuthRequestType` 1..1 | S | Autenticación del emisor |
| `comprobanteRequest` | `ComprobanteType` 1..1 | S | Atributos del comprobante (tabla de `ComprobanteType`) |

Response `autorizarComprobanteResponse/autorizarComprobanteReturn` (`AutorizarComprobanteReturnType`):

| Campo | XSD | Man. | Descripción [MAN págs. 27-28] |
|---|---|---|---|
| `comprobanteResponse` | `ComprobanteResponseType` 0..1 | N | Datos del comprobante autorizado (con `CAE`) |
| `arrayObservaciones` | `ArrayCodigosDescripcionesType` 0..1 | N | Validaciones no excluyentes no superadas |
| `arrayErrores` | `ArrayCodigosDescripcionesType` 0..1 | N | Errores de negocio |
| `arrayErroresFormato` | `ArrayCodigosDescripcionesStringType` 0..1 | N | Errores de formato |
| `resultado` | `ResultadoSimpleType` **1..1** | S | Resultado del procesamiento: `A`, `O` o `R` |

Validaciones: 100-104 (emisor, solo producción), 200-203, 300-322, 350-356, 360-369 (cabecera e importes), 400-415 (ítems), 500-504 (subtotales IVA), 600-604 (otros tributos), 700-732 (formas de pago), 800-807 (asociados), 900 (datos adicionales). Todas "Rechaza" salvo **807 "Observa"**. Detalle en "Validaciones y errores"; las reglas aritméticas y de relación, en "Comportamiento a simular".

Ejemplo de request real [CAS 2021, `test_autorizar_comprobante.yaml`; token abreviado]. Estructuralmente válido (no dio `arrayErroresFormato`), pero ese día homologación devolvió `5000`:

```xml
<ser:autorizarComprobanteRequest xmlns:ser="http://ar.gob.afip.wsct/CTService/">
  <authRequest><token>PD94bWwg…</token><sign>TMd0DHUQ…</sign><cuitRepresentada>20267565393</cuitRepresentada></authRequest>
  <comprobanteRequest>
    <codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>4000</numeroPuntoVenta><numeroComprobante>1</numeroComprobante>
    <fechaEmision>2021-06-20</fechaEmision><codigoTipoAutorizacion>E</codigoTipoAutorizacion>
    <codigoTipoDocumento>80</codigoTipoDocumento><numeroDocumento>50000000059</numeroDocumento><idImpositivo>9</idImpositivo>
    <codigoPais>203</codigoPais><domicilioReceptor>Rua N.76 km 34.5 Alagoas</domicilioReceptor><codigoRelacionEmisorReceptor>3</codigoRelacionEmisorReceptor>
    <importeGravado>100.00</importeGravado><importeNoGravado>0.00</importeNoGravado><importeExento>0.00</importeExento>
    <importeOtrosTributos>1.00</importeOtrosTributos><importeReintegro>-21.0</importeReintegro><importeTotal>101.00</importeTotal>
    <codigoMoneda>PES</codigoMoneda><cotizacionMoneda>1.000</cotizacionMoneda><observaciones>Observaciones Comerciales, libre</observaciones>
    <arrayItems><item><tipo>0</tipo><codigoTurismo>1</codigoTurismo><codigo>T0001</codigo><descripcion>Descripcion del producto P0001</descripcion><codigoAlicuotaIVA>5</codigoAlicuotaIVA><importeIVA>21.0</importeIVA><importeItem>121.0</importeItem></item></arrayItems>
    <arrayOtrosTributos><otroTributo><codigo>99</codigo><descripcion>Impuesto Municipal Matanza</descripcion><baseImponible>100.00</baseImponible><importe>1.00</importe></otroTributo></arrayOtrosTributos>
    <arraySubtotalesIVA><subtotalIVA><codigo>5</codigo><importe>21</importe></subtotalIVA></arraySubtotalesIVA>
    <arrayFormasPago><formaPago><codigo>68</codigo><tipoTarjeta>99</tipoTarjeta><numeroTarjeta>999999</numeroTarjeta></formaPago></arrayFormasPago>
  </comprobanteRequest>
</ser:autorizarComprobanteRequest>
```

Cuentas de ese ejemplo: ítem 121 con IVA 21 → gravado 100 (361); reintegro −21 = −IVA de ítems con `codigoTurismo` 1 (364/366); total = 100 + 0 + 0 + (−21) + 1 + 21 = **101** (369).

Respuesta real de ese request [CAS 2021]:

```xml
<ns2:autorizarComprobanteResponse xmlns:ns2="http://ar.gob.afip.wsct/CTService/"><autorizarComprobanteReturn><arrayErrores><codigoDescripcion><codigo>5000</codigo><descripcion>Error interno [E-20210620-13:44:56.434-20267565393-vii]</descripcion></codigoDescripcion></arrayErrores><resultado>R</resultado></autorizarComprobanteReturn></ns2:autorizarComprobanteResponse>
```

Respuesta aprobada **derivada del WSDL** (no hay ejemplo real ni del manual con valores; CAE y fechas ficticios):

```xml
<ns2:autorizarComprobanteResponse xmlns:ns2="http://ar.gob.afip.wsct/CTService/"><autorizarComprobanteReturn><comprobanteResponse><cuit>20267565393</cuit><codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>4000</numeroPuntoVenta><numeroComprobante>1</numeroComprobante><fechaEmision>2021-06-20</fechaEmision><CAE>71250000000001</CAE><fechaVencimientoCAE>2021-06-30</fechaVencimientoCAE></comprobanteResponse><resultado>A</resultado></autorizarComprobanteReturn></ns2:autorizarComprobanteResponse>
```

### 3. `consultarUltimoComprobanteAutorizado`

Propósito: "consultar el último comprobante que fue autorizado para la combinación CUIT, punto de venta y tipo de comprobante" [MAN pág. 54].

Request `consultarUltimoComprobanteAutorizadoRequest`:

| Campo | XSD | Man. | Long. | Descripción [MAN pág. 55] |
|---|---|---|---|---|
| `authRequest` | `AuthRequestType` 1..1 | S | — | |
| `codigoTipoComprobante` | `xsd:short` 1..1 | S | 3 | Tipo de comprobante |
| `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` 1..1 | S | — | Punto de venta |

Response `consultarUltimoComprobanteAutorizadoReturn`:

| Campo | XSD | Man. | Descripción [MAN pág. 57] |
|---|---|---|---|
| `numeroComprobante` | `NumeroComprobanteSimpleType` 0..1 | N | Último número registrado para CUIT + punto de venta + tipo |
| `fechaEmision` | `xsd:date` 0..1 | N | Fecha de emisión de ese último comprobante |
| `arrayErrores` | 0..1 | N | |
| `arrayErroresFormato` | 0..1 | N | |

Validaciones [MAN págs. 57-58]: 1000 (tipo habilitado), 1001 (punto de venta habilitado), **1002 "Debe tener al menos un comprobante emitido para la combinación CUIT, punto de venta y tipo de comprobante"**. O sea: sin comprobantes previos **no devuelve 0**, devuelve el error 1002 sin `numeroComprobante` (además, 0 no cabe en `NumeroComprobanteSimpleType`). Ver "Comportamiento a simular".

Ejemplo de request real [CAS 2021]:

```xml
<ser:consultarUltimoComprobanteAutorizadoRequest xmlns:ser="http://ar.gob.afip.wsct/CTService/"><authRequest><token>…</token><sign>…</sign><cuitRepresentada>20267565393</cuitRepresentada></authRequest><codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>4000</numeroPuntoVenta></ser:consultarUltimoComprobanteAutorizadoRequest>
```

Respuesta con datos, **derivada del WSDL**: `<ns2:consultarUltimoComprobanteAutorizadoResponse xmlns:ns2="http://ar.gob.afip.wsct/CTService/"><consultarUltimoComprobanteAutorizadoReturn><numeroComprobante>15</numeroComprobante><fechaEmision>2026-10-01</fechaEmision></consultarUltimoComprobanteAutorizadoReturn></ns2:consultarUltimoComprobanteAutorizadoResponse>`. Respuesta de error real [CAS 2021]: `…<consultarUltimoComprobanteAutorizadoReturn><arrayErrores><codigoDescripcion><codigo>5000</codigo><descripcion>Error interno [E-20210620-13:44:54.313-20267565393-vii]</descripcion></codigoDescripcion></arrayErrores></consultarUltimoComprobanteAutorizadoReturn>…`.

### 4. `consultarComprobanteTipoPVentaNro`

Propósito: "consultar los datos de un comprobante previamente autorizado … De ser estos datos válidos se devolverán todos los datos asociados a ese comprobante, caso contrario retornará el error asociado" [MAN pág. 46].

Request `consultarComprobanteTipoPVentaNroRequest` (`ConsultarComprobanteTipoPVentaNroRequestType`):

| Campo | XSD | Man. | Long. | Descripción [MAN pág. 47] |
|---|---|---|---|---|
| `authRequest` | `AuthRequestType` 1..1 | S | — | |
| `codigoTipoComprobante` | `xsd:short` 1..1 | S | 3 | Tipo a consultar |
| `numeroPuntoVenta` | `NumeroPuntoVentaSimpleType` 1..1 | S | — | Punto de venta |
| `numeroComprobante` | `NumeroComprobanteSimpleType` 1..1 | S | 8 | Número |

Response `consultarComprobanteTipoPVentaNroResponse/consultarComprobanteReturn` (`ConsultarComprobanteReturnType`):

| Campo | XSD | Man. | Descripción [MAN pág. 52] |
|---|---|---|---|
| `comprobante` | `ComprobanteType` 0..1 | N | El comprobante, con **la misma estructura del request de autorización** (29 campos, ver `ComprobanteType`). El manual lo describe por error como "Ultimo número de comprobante registrado…" |
| `arrayObservaciones` | `ArrayCodigosDescripcionesType` 0..1 | N | Observaciones |
| `arrayErrores` | 0..1 | N | |
| `arrayErroresFormato` | 0..1 | N | |

No hay `resultado`, ni CUIT del emisor, ni fecha de proceso, ni un elemento `CAE`: el CAE solo puede volver en `comprobante/codigoAutorizacion`, con `codigoTipoAutorizacion` = `E` y su vencimiento en `fechaVencimiento` [INFERIDO]. Qué observaciones trae `arrayObservaciones` (¿las de la autorización original?): **NO VERIFICADO**.

Validaciones [MAN págs. 52-53]: 2000 (tipo habilitado), 2001 (punto de venta habilitado), 2002 ("Debe tener al menos un comprobante emitido para la combinación CUIT, punto de venta y tipo de comprobante"). El manual encabeza esta tabla con `<consultarUltimoComprobanteAutorizadoRequest>` (errata, pág. 52). Qué error devuelve si existe la combinación pero no ese número: **NO VERIFICADO** (ningún código lo cubre).

Ejemplo real de error de formato [CAS 2021, `test_consultar_comprobante.yaml`], request con `numeroComprobante` 0:

```xml
<ser:consultarComprobanteTipoPVentaNroRequest xmlns:ser="http://ar.gob.afip.wsct/CTService/"><authRequest>…</authRequest><codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>4000</numeroPuntoVenta><numeroComprobante>0</numeroComprobante></ser:consultarComprobanteTipoPVentaNroRequest>
```
```xml
<ns2:consultarComprobanteTipoPVentaNroResponse xmlns:ns2="http://ar.gob.afip.wsct/CTService/"><consultarComprobanteReturn><arrayErroresFormato><codigoDescripcionString><codigo>cvc-minInclusive-valid</codigo><descripcion> El valor '0' no cumple con la restricción minInclusive '1' para el tipo 'NumeroComprobanteSimpleType'.</descripcion></codigoDescripcionString><codigoDescripcionString><codigo>cvc-type.3.1.3</codigo><descripcion> El valor '0' del elemento 'numeroComprobante' no es válido.</descripcion></codigoDescripcionString></arrayErroresFormato></consultarComprobanteReturn></ns2:consultarComprobanteTipoPVentaNroResponse>
```

### 5. `consultarPuntosVenta`

Propósito: "consultar los puntos de venta que tienen habilitados para utilizar con el servicio" [MAN pág. 59].

Request `consultarPuntosVentaRequest`: solo `authRequest` 1..1.

Response `consultarPuntosVentaReturn`:

| Campo | XSD | Man. | Descripción [MAN págs. 61-62] |
|---|---|---|---|
| `arrayPuntosVenta` | `ArrayPuntosVentaType` 0..1 (1..n `puntoVenta` de tipo `PuntosVentaType`) | N | Puntos de venta habilitados a interactuar con el WS |
| `arrayErrores` | 0..1 | N | |
| `arrayErroresFormato` | 0..1 | N | |

Validación [MAN pág. 63]: **1106** "Deberá contener al menos un punto de venta habilitado a utilizar con el presente ws" (Rechaza). En homologación "no será validada la existencia" de los puntos de venta y por eso "la ejecución del método consultarPuntosVenta no devolverá datos en su respuesta" (pág. 17). Si en homologación eso se traduce en un 1106 o en un `Return` vacío: **NO VERIFICADO**.

Respuesta con datos, **derivada del WSDL**: `<consultarPuntosVentaReturn><arrayPuntosVenta><puntoVenta><numeroPuntoVenta>1</numeroPuntoVenta><bloqueado>N</bloqueado></puntoVenta><puntoVenta><numeroPuntoVenta>2</numeroPuntoVenta><bloqueado>S</bloqueado><fechaBaja>2025-01-31</fechaBaja></puntoVenta></arrayPuntosVenta></consultarPuntosVentaReturn>`.

### 6 a 22. Operaciones de parámetros

Quince de las diecisiete restantes reciben **solo `authRequest` 1..1** y devuelven un `Return` con tres hijos opcionales, en este orden: el array de datos (0..1), `arrayErrores` (0..1, `ArrayCodigosDescripcionesType`) y `arrayErroresFormato` (0..1, `ArrayCodigosDescripcionesStringType`) [WSDL]. Las otras dos, `consultarCotizacion` y `consultarTiposTarjeta`, llevan parámetros y van aparte (8 y 22).

Ojo con el tipo del array de datos: según la operación, el código es `short` (`codigoDescripcion`) o `string` (`codigoDescripcionString`). Un cliente generado desde el WSDL falla si el simulador usa el equivocado.

| # | Operación | Hijo del `Return` con los datos | Elemento de cada fila | `codigo` | Propósito (cita del manual) | Págs. | Diferencias del manual con el WSDL |
|---:|---|---|---|---|---|---|---|
| 6 | `consultarTiposComprobantes` | `arrayTiposComprobantes` | `codigoDescripcion` | short | "consultar los tipos de comprobantes habilitados interactuar con el presente web service" | 67-69 | El esquema cierra con un `</arrayCodigosItem>` suelto (errata); la tabla dice que el `Return` es `ConsultarTiposComprobantesResponseType` |
| 7 | `consultarMonedas` | `arrayTiposMoneda` | `codigoDescripcionString` | string | "consultar las diferentes monedas disponibles a informar al momento de autorizar el comprobante" | 90-93 | — |
| 9 | `consultarTiposDocumento` | `arrayTiposDocumento` | `codigoDescripcion` | short | "consultar los tipos de documentos de receptores de comprobantes habilitados a ser informados en el presente ws" | 70-73 | El esquema escribe `<cts: consultarTiposDocumentoRequest>` con un espacio (errata) |
| 10 | `consultarPaises` | `arrayPaises` | `codigoDescripcionString` | string | "consultar diferentes países disponibles a informar al momento de autorizar el comprobante" | 87-89 | El esquema de `arrayErrores` pone `codigo` string |
| 11 | `consultarCUITsPaises` | `arrayCuitPaises` | `codigoDescripcionString` | string | "consultar los diferentes CUIT de países habilitados a informar al momento de autorizar el comprobante" | 102-104 | Nombre del tipo del `Return` en el WSDL: `ConsultarCuitPaisesReturnType` (el manual: `ConsultarCUITsPaisesReturnType`); el elemento es `consultarCUITsPaisesReturn` en ambos |
| 12 | `consultarTiposIVA` | `arrayTiposIVA` | `codigoDescripcionString` | string | "consultar los tipos de IVA habilitados a informar al momento de identificar el detalle del comprobante" | 77-79 | El manual dibuja `codigoDescripcion` (short) y un `<porcentaje>?</porcentaje>` dentro del array; la tabla dice `ArrayCodigosDescripcionesType`. **WSDL: `ArrayCodigosDescripcionesStringType`, sin `porcentaje`** |
| 13 | `consultarTiposDatosAdicionales` | `arrayTiposDatosAdicionales` | `codigoDescripcionString` | string | "consultar todos los datos adicionales a informar sobre un comprobante según RG" | 120-124 | La tabla del manual dice `ArrayCodigosDescripcionesType`; el esquema y el ejemplo usan `codigoDescripcionString` (como el WSDL). Nombres de tipos en el WSDL: `ConsultarTiposDatosAdicionalesRequest`/`…Response` (sin `Type`) |
| 14 | `consultarTiposTributo` | `arrayTiposTributo` | `codigoDescripcionString` | string | "consultar los tipos de tributos habilitados a informar al momento de identificar el detalle del comprobante" | 80-82 | El esquema pone `<codigo>short</codigo>` dentro de `codigoDescripcionString`. Tipo del request en el WSDL: `ConsultarTiposTributosRequestType` (con "s"). El texto de 600 cita `consultarTiposTributo()` y la pág. 134 `consultarTiposTributos` |
| 15 | `consultarCondicionesIVA` | `arrayCondicionesIVA` | `codigoDescripcionString` | string | "consultar las diferentes condiciones de IVA disponibles a informar al momento de autorizar el comprobante" | 98-101 | Es la tabla de valores de `idImpositivo` (por 308) |
| 16 | `consultarNovedades` | `arrayNovedades` | `codigoDescripcionString` | string | "consultar todas las notificaciones referentes al servicio en cuestión" | 108-111 | El `Return` es **`ConsultarNovedadesReturn`** (C mayúscula), igual en manual y WSDL |
| 17 | `consultarFormasPago` | `arrayFormasPago` | `codigoDescripcion` | short | "consultar las diferentes formas de pago disponibles a informar al momento de autorizar el comprobante" | 94-97 | Mismo nombre `arrayFormasPago` que el array del comprobante, pero aquí es `ArrayCodigosDescripcionesType` |
| 18 | `consultarTiposItem` | `arrayTiposItem` | `codigoDescripcion` | short | (sin texto de propósito; título "Consulta los tipos de ítems") | 74-76 | El esquema de respuesta omite el `consultarTiposItemReturn` y pone `arrayTiposItem` directo bajo la `Response`; **el WSDL tiene `consultarTiposItemReturn`** |
| 19 | `consultarCodigosItemTurismo` | `arrayCodigosItem` | `codigoDescripcion` | short | "consultar los códigos correspondientes a los ítems de Turismo" | 64-66 | — |
| 20 | `consultarRelacionEmisorReceptor` | `arrayRelacionesEmisorReceptor` | `codigoDescripcion` | short | "consultar diferentes relaciones a informar entre el emisor y el receptor del comprobante" | 83-86 | — |
| 21 | `consultarTiposCuenta` | `arrayTiposCuenta` | `codigoDescripcion` | short | "consultar las diferentes tipos de cuenta a utilizar al momento de autorizar el comprobante" | 115-119 | El esquema abre `codigoDescripcion>` sin `<` (errata) |

Ninguna de estas quince tiene validaciones de negocio propias en el manual. Los valores que devuelven **no figuran en el manual** (solo los que se deducen de las validaciones, ver "Tablas y datos").

Ejemplo del manual para `consultarTiposDatosAdicionales` (págs. 123-124), con un valor de relleno:

```xml
<cts:consultarTiposDatosAdicionalesRequest><authRequest><token>Un string </token><sign>Un string </sign><cuitRepresentada>un cuit</cuitRepresentada></authRequest></cts:consultarTiposDatosAdicionalesRequest>
```
```xml
<ns2:consultarTiposDatosAdicionalesResponse xmlns:ns2="http://ar.gob.afip.wsct/CTService/"><consultarTiposDatosAdicionalesReturn><arrayTiposDatosAdicionales><codigoDescripcionString><codigo>1</codigo><descripcion> CAMPO PARA RG ….</descripcion></codigoDescripcionString></arrayTiposDatosAdicionales></consultarTiposDatosAdicionalesReturn></ns2:consultarTiposDatosAdicionalesResponse>
```

Ejemplo real de request [CAS 2021] para cualquiera de ellas (cambia solo el nombre):

```xml
<ser:consultarTiposComprobantesRequest xmlns:ser="http://ar.gob.afip.wsct/CTService/"><authRequest><token>…</token><sign>…</sign><cuitRepresentada>20267565393</cuitRepresentada></authRequest></ser:consultarTiposComprobantesRequest>
```

Todas las grabaciones de 2021 de estas operaciones (14 de las 15) devolvieron `arrayErrores` con `5000 Error interno [E-<aaaammdd>-<hh:mm:ss.mmm>-<cuit>-vii]` [CAS]; no hay ninguna respuesta real con datos.

Respuesta con datos **derivada del WSDL** (valores ilustrativos tomados de las validaciones del manual):

```xml
<ns2:consultarTiposComprobantesResponse xmlns:ns2="http://ar.gob.afip.wsct/CTService/"><consultarTiposComprobantesReturn><arrayTiposComprobantes><codigoDescripcion><codigo>195</codigo><descripcion>Factura T</descripcion></codigoDescripcion><codigoDescripcion><codigo>196</codigo><descripcion>Nota de Débito T</descripcion></codigoDescripcion><codigoDescripcion><codigo>197</codigo><descripcion>Nota de Crédito T</descripcion></codigoDescripcion></arrayTiposComprobantes></consultarTiposComprobantesReturn></ns2:consultarTiposComprobantesResponse>
```

### 8. `consultarCotizacion`

Propósito: "consultar la cotización de la moneda al momento de invocar al método público" [MAN pág. 105].

Request `consultarCotizacionRequest` (`ConsultarCotizacionRequestType`):

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `authRequest` | `AuthRequestType` 1..1 | S | — | |
| `codigoMoneda` | `xsd:string` 1..1 | S | 3 | "Código de la Moneda por la cual se intenta consultar la última cotización disponible" (pág. 106) |
| `fechaCotizacion` | `xsd:date` **1..1** | — | — | Fecha para la que se pide la cotización. **No aparece** en el esquema ni en la tabla del manual (págs. 105-106); solo en el historial v1.6.4: "Se modifico el metodo consultarCotizacionMoneda para agregar el campo obligatorio fechaCotizacion" (pág. 142; el nombre del método también está mal). Manda el WSDL: es obligatorio |

Response `consultarCotizacionReturn` (`ConsultarCotizacionReturnType`):

| Campo | XSD | Man. | Descripción [MAN págs. 107-108] |
|---|---|---|---|
| `cotizacionMoneda` | `xsd:decimal` 0..1 | N | Cotización de la moneda pedida |
| `arrayErrores` | 0..1 | N | |
| `arrayErroresFormato` | 0..1 | N | |

El esquema del manual (pág. 107) muestra además `<cancelaEnMismaMonedaExtranjera>` en la respuesta: **no existe en el WSDL**.

Validación [MAN pág. 108]: **210** "Deberá coincidir con alguno de los códigos de moneda disponibles. Consultar método consultarMonedas" (Rechaza). (En v1.1 se cambió el código de esta validación de 300 a 210, pág. 141.)

Qué devuelve sin cotización para esa fecha, con fecha futura o con `PES`: **NO VERIFICADO**.

Request **derivado del WSDL**: `<cts:consultarCotizacionRequest><authRequest>…</authRequest><codigoMoneda>DOL</codigoMoneda><fechaCotizacion>2026-10-01</fechaCotizacion></cts:consultarCotizacionRequest>` → `<consultarCotizacionReturn><cotizacionMoneda>…</cotizacionMoneda></consultarCotizacionReturn>`.

### 22. `consultarTiposTarjeta`

Propósito: "consultar las diferentes tipos de tarjetas a utilizar al momento de autorizar un comprobante dependiendo de su forma de pago" [MAN pág. 112].

Request `consultarTiposTarjetaRequest` (`ConsultarTiposTarjetaRequestType`):

| Campo | XSD | Man. | Long. | Descripción [MAN pág. 113] |
|---|---|---|---|---|
| `authRequest` | `AuthRequestType` 1..1 | S | — | |
| `formaPago` | `xsd:short` 1..1 | S | 3 | "Forma de pago por la cual se quieren consultar los tipos de tarjetas habilitadas" |

Response `consultarTiposTarjetaReturn`: `arrayTiposTarjeta` (`ArrayCodigosDescripcionesType`, `codigo` short) 0..1, `arrayErrores` 0..1, `arrayErroresFormato` 0..1 [WSDL; MAN pág. 115].

Validación [MAN pág. 115]: **1200** "Deberá coincidir con las formas de pago posibles 1 - debito, 2 - crédito. Consultar método consultarFormasPago" (Rechaza). Ojo: PyAfipWs usa `codigo` 68 como "tarjeta de crédito" en `formaPago` [CAS, comentario en `wsct.py`]; no se pudo confirmar qué códigos devuelve `consultarFormasPago` (**NO VERIFICADO**).

Request **derivado del WSDL**: `<cts:consultarTiposTarjetaRequest><authRequest>…</authRequest><formaPago>2</formaPago></cts:consultarTiposTarjetaRequest>`.

## Validaciones y errores

Todos los códigos del manual: **104 numéricos** (incluido el 321, que solo aparece en el historial) y **3 alfanuméricos** (`wscommon_007` y los dos `cvc-…` de ejemplo), más **2 observados solo en [CAS]** que el manual no trae (`wscommon_002`, `cvc-minInclusive-valid`). Total: **109 filas**, las mismas que `wsct-codigos.json`.

Cómo se transportan:
- **Fault** (HTTP 500, `faultstring` con `[wscommon_NNN]`): autenticación y "errores graves de estructura XML" (págs. 10-11). En vivo 2026 el F5 los convierte en `BL<n> <fecha> 500`.
- **`arrayErroresFormato`** (HTTP 200): violaciones del XSD; códigos string del validador (`cvc-…`). Excluye a `arrayErrores` (pág. 12).
- **`arrayErrores`** (HTTP 200): todo lo marcado "Rechaza" y el 5000. En `autorizarComprobante`, `resultado` = `R` y sin `comprobanteResponse` [INFERIDO; el 5000 real trae `R`].
- **`arrayObservaciones`** (HTTP 200): solo el 807 ("Observa"); hay CAE y `resultado` = `O`.

Notas de lectura de la tabla del manual:
- Faltan del manual los códigos 402, 405, 601, 802 (la numeración salta) y no hay códigos de validación para 15 de las 22 operaciones.
- 316 y 356 tienen la celda de efecto **vacía** en el PDF; como todas las tablas de `autorizarComprobante` salvo la del 807 son "Validaciones Excluyentes", se toman como rechazo [INFERIDO].
- En la pág. 30 la columna "Campo" de 302 dice `<fechaEmision>` y la de 303 lista `<cuitRepresentada>/…/<numeroComprobante>/<fechaEmision>`; por el texto, parecen invertidas [INFERIDO]. Se transcribe tal cual.
- Las filas partidas entre páginas (411, 602, 704, 724, 801, 900, 1000, 2000) se unieron.
- El manual no da el texto exacto que devuelve el servicio en `descripcion` para ningún código de negocio: el "texto" de la tabla es la **condición** que describe el manual. Lo que diga `descripcion` en la realidad es **NO VERIFICADO** (salvo 5000, [CAS]).

| Código | Texto / condición (literal del manual) | Efecto | Dónde (operación · campo) | Pág. | Nota |
|---|---|---|---|---|---|
| 5000 | Error general de aplicación | Rechaza | todas | 14 | Va en arrayErrores (HTTP 200). Real [CAS]: `Error interno [E-20210620-13:44:56.434-20267565393-vii]`; en autorizarComprobante con resultado R |
| `wscommon_007` | [wscommon_007] La firma no corresponde al token enviado. | Fault | todas · `authRequest` | 10 | SOAP Fault, faultcode `ns3: Receiver`. En vivo 2026 llega como `BL<n> <fecha> 500` |
| `wscommon_002` | [wscommon_002] Token vencido Fecha y Hora de Vencimiento del Token Enviado: 21-06-2021 01:44:40 - Fecha y Hora Actual del Servidor: 22-07-2021 18:38:27 | Fault | todas · `authRequest` | — [CAS] | **No está en el manual**: Fault real HTTP 500 de 2021 [CAS]. Las fechas varían |
| `cvc-datatype-valid.1.2.1` | '?' no es un valor válido para un tipo de dato entero. | Rechaza | todas · `cuitRepresentada` | 12 | Error de formato (arrayErroresFormato, código string del validador XSD). Ejemplo del manual |
| `cvc-type.3.1.3` | El valor '?' en el elemento 'cuitRepresentada' no es válido. | Rechaza | todas · `cuitRepresentada` | 12 | Error de formato. Ejemplo del manual; real [CAS]: ` El valor '0' del elemento 'numeroComprobante' no es válido.` (con espacio inicial) |
| `cvc-minInclusive-valid` |  El valor '0' no cumple con la restricción minInclusive '1' para el tipo 'NumeroComprobanteSimpleType'. | Rechaza | todas · `numeroComprobante` | — [CAS] | **No está en el manual**: error de formato real de 2021 [CAS] en consultarComprobanteTipoPVentaNro |
| 100 | El emisor del comprobante debe encontrarse activo en el sistema registral. | Rechaza | `autorizarComprobante` · `cuitRepresentada` | 18 | Validaciones sobre el emisor: no se hacen en homologación (pág. 17). La sección no nombra el método; por el texto es la emisión [INFERIDO] |
| 101 | El emisor del comprobante debe estar empadronado en alguna actividad de hospedaje/turismo. | Rechaza | `autorizarComprobante` · `cuitRepresentada` | 18 | Solo producción |
| 102 | El emisor del comprobante no debe tener domicilios con inconsistencias. | Rechaza | `autorizarComprobante` · `cuitRepresentada` | 18 | Solo producción |
| 103 | El emisor del comprobante debe estar registrado en el impuesto al Valor Agregado al momento de autorizar el comprobante. | Rechaza | `autorizarComprobante` · `cuitRepresentada` | 18 | Solo producción |
| 104 | El emisor del comprobante debe tener un punto de venta habilitado a “RECE para aplicativo y web services” o “Codificación de producto - Web services”, vigente, no bloqueado y no dado de baja. | Rechaza | `autorizarComprobante` · `cuitRepresentada` | 18 | Solo producción |
| 200 | Informar el campo codigoTipoAutorizacion según el tipo de autorización a efectuar. Siempre debe informarse. | Rechaza | `autorizarComprobante` · `codigoTipoAutorizacion` | 29 | El XSD lo hace opcional (0..1) |
| 201 | Solo se encuentra disponible la modalidad de autorización E = CAE | Rechaza | `autorizarComprobante` · `codigoTipoAutorizacion` | 29 | `A` (CAEA) pasa el XSD pero se rechaza |
| 202 | Si informa &lt;codigoTipoAutorizacion> = “E”, no informar el código de autorización. | Rechaza | `autorizarComprobante` · `codigoTipoAutorizacion/codigoAutorizacion` | 29 |  |
| 203 | Si informa &lt;codigoTipoAutorizacion> = “E”, no informar la fecha de vencimiento del código de autorización. | Rechaza | `autorizarComprobante` · `codigoTipoAutorizacion/fechaVencimiento` | 29 |  |
| 300 | El tipo de comprobante debe corresponder a alguno de los comprobantes habilitados. Ver el método consultarTiposComprobantes(). | Rechaza | `autorizarComprobante` · `codigoTipoComprobante` | 29 |  |
| 301 | El punto de venta debe ser un punto de venta habilitado. Consultar el método consultarPuntosVenta(). | Rechaza | `autorizarComprobante` · `numeroPuntoVenta` | 29 | En homologación no se valida la existencia del punto de venta (pág. 17) |
| 302 | La numeración no corresponde con el próximo a autorizar. Consultar método consultarUltimoComprobanteAutorizado(). | Rechaza | `autorizarComprobante` · `fechaEmision` | 30 | El manual pone `<fechaEmision>` como campo de 302 y la lista cuit/tipo/punto de venta/número/fecha en 303: parecen invertidos [INFERIDO]. Se usa también ante un reenvío duplicado [INFERIDO] |
| 303 | Si se envía puede ser hasta 10 días anteriores o posteriores a la fecha de generación. Si no se envía se le asignará la fecha de proceso | Rechaza | `autorizarComprobante` · `cuitRepresentada/codigoTipoComprobante/numeroPuntoVenta/numeroComprobante/fechaEmision` | 30 | Regla de `fechaEmision` |
| 304 | El tipo de moneda debe ser alguno de los tipos de moneda habilitados. Consultar el método consultarMonedas(). | Rechaza | `autorizarComprobante` · `codigoMoneda` | 30 |  |
| 305 | Si el código de moneda es PES, la cotización debe ser 1. | Rechaza | `autorizarComprobante` · `codigoMoneda/cotizacionMoneda` | 30 |  |
| 306 | El tipo de cambio no podrá ser inferior al 2% ni superior en un 400% del que suministra ARCA como orientativo de acuerdo a la cotización oficial. Consultar método consultarCotizacion(). | Rechaza | `autorizarComprobante` · `codigoMoneda/cotizacionMoneda` | 30 | Banda cambiada en v1.2 (200 %/20 %), v1.3 (400 %/20 %) y v1.4 (400 %/2 %), pág. 141 |
| 307 | El código de país debe ser uno de los habilitados. Consultar el método consultarPaises(). | Rechaza | `autorizarComprobante` · `codigoPais` | 30 |  |
| 308 | El identificador impositivo o condición de IVA del receptor debe ser uno de los habilitados. Consultar el método consultarCondicionesIVA(). | Rechaza | `autorizarComprobante` · `idImpositivo` | 30 |  |
| 309 | Cuando informa identificador impositivo “IVA Responsable Inscripto” el tipo de documento debe ser CUIT. | Rechaza | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento` | 30 |  |
| 310 | Cuando informa identificador impositivo “IVA Responsable Inscripto”, con tipo de documento CUIT, el número de documento debe encontrarse registrado en las bases de esta administración de forma activa. | Rechaza | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento/numeroDocumento` | 31 | Depende del padrón de ARCA |
| 311 | Cuando informa identificador impositivo “Consumidor Final”, los tipos de documentos habilitados para informar sobre el receptor son CUIT país / CI Extranjera / Pasaporte / DNI. | Rechaza | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento` | 31 |  |
| 312 | Cuando informa identificador impositivo “Consumidor Final”, el número de documento debe ser un valor alfanumérico. | Rechaza | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento/numeroDocumento` | 31 | Alta en v1.1 |
| 313 | Cuando informa identificador impositivo “Cliente del Exterior”, los tipos de documentos habilitados para informar sobre el receptor son CUIT país, CI Extranjera / Pasaporte / DNI. | Rechaza | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento` | 31 |  |
| 314 | Cuando informa identificador impositivo “Cliente del Exterior”, con tipo de documento CUIT, es obligatorio informar un CUIT País habilitado. Consultar método consultarCUITsPaises() | Rechaza | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento/numeroDocumento` | 31 |  |
| 315 | Cuando informa identificador impositivo “Cliente del Exterior”, el número de documento debe ser un valor alfanumérico. | Rechaza | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento/numeroDocumento` | 31 | Alta en v1.1 |
| 316 | Cuando informa identificador impositivo “IVA Responsable Inscripto”, con tipo de documento CUIT, el número de documento debe encontrarse registrado en las bases de esta en el Impuesto al Valor Agregado. | Rechaza [INFERIDO] | `autorizarComprobante` · `idImpositivo/codigoTipoDocumento/numeroDocumento` | 31 | La celda "NO es superada" está vacía en el manual; la tabla es de validaciones excluyentes |
| 317 | Deberá debe ser mayor a cero | Rechaza | `autorizarComprobante` · `cotizacionMoneda` | 31 | Listado entre los errores de v1.6.4 (pág. 142) |
| 318 | En caso de enviar la marca de que el pago del comprobante se realiza en la misma moneda extranjera para comprobantes que no sean facturas. Unicamente se puede utilizar con los códigos habilitados (195) | Rechaza | `autorizarComprobante` · `cancelaEnMismaMonedaExtranjera` | 32 | RG 5616, v1.6.4 |
| 319 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera y enviar como código de moneda el Peso Argentino | Rechaza | `autorizarComprobante` · `codigoMoneda/cancelaEnMismaMonedaExtranjera` | 32 | RG 5616, v1.6.4 |
| 320 | En caso de enviar la marca de que el pago de la factura se realiza en la misma moneda extranjera, que codigoMoneda es del grupo de monedas con cotización del Banco de la Nación Argentina (ver Anexo Monedas BNA), que haya cotización y que la misma no coincida exactamente con el valor enviado en el campo cotizacionMoneda. En cuyo caso se podrá omitir el mismo para que la cotización de la factura sea la obtenida de los registros de ARCA | Rechaza | `autorizarComprobante` · `codigoMoneda/cotizacionMoneda/cancelaEnMismaMonedaExtranjera` | 32 | RG 5616, v1.6.4 |
| 321 | codigoMoneda, cotizacionMoneda, cancelaEnMismaMonedaExtranjera: Errores: 317, 318, 319, 320, 321, 322 | — (sin definir) | `autorizarComprobante` · `codigoMoneda/cotizacionMoneda/cancelaEnMismaMonedaExtranjera` | 142 | Solo aparece en esa lista del historial v1.6.4; no tiene fila en la tabla de validaciones ni texto. **NO VERIFICADO** qué condición lo dispara |
| 322 | El campo es obligatorio a excepción de los casos para los cuales se envia el campo cancelaEnMismaMonedaExtranjera y se puede obtener la cotizacion asociada al codigoMoneda si esta es del grupo de monedas del Banco de la Nación Argentina (ver Anexo Monedas BNA) | Rechaza | `autorizarComprobante` · `cotizacionMoneda` | 32 | RG 5616, v1.6.4 |
| 350 | El domicilio del receptor es obligatorio informarlo. Su dimensión máxima son 300 caracteres alfanuméricos. | Rechaza | `autorizarComprobante` · `domicilioReceptor` | 32 | El XSD lo hace opcional y sin largo |
| 351 | La relación entre el emisor y el receptor debe ser alguna de las habilitadas. Consultar el método consultarRelacionEmisorReceptor() | Rechaza | `autorizarComprobante` · `codigoRelacionEmisorReceptor` | 32 |  |
| 352 | Si el tipo de relación Emisor Receptor seleccionada corresponde a: 1 - Alojamiento Directo a Turista No Residente 2 - Alojamiento a Agencia de Viaje Residente 3 - Alojamiento a Agencia de Viaje No Residente el emisor debe tener al menos una actividad de hospedaje vigente. Si el tipo de relación es: 4 - Agencia de Viaje Residente a Agencia de Viaje No Residente 5 - Agencia de Viaje Residente a Turista No Residente 6 - Agencia de Viaje Residente a Agencia de Viaje Residente el emisor debe estar registrado en el padrón de agencias de la secretaría de turismo. | Rechaza | `autorizarComprobante` · `cuitRepresentada/codigoRelacionEmisorReceptor` | 33 | Depende de padrones de ARCA |
| 353 | Cuando la relación Emisor Receptor es: 1 - Alojamiento Directo a Turista No Residente / 5 - Agencia de Viaje Residente a Turista No Residente, los tipos de documentos de receptor habilitados son 80 - Cuit / 91 - CI Extranjera / 94 - Pasaporte / 96 – DNI Cuando es: 2 - Alojamiento a Agencia de Viaje Residente / 3 - Alojamiento a Agencia de Viaje No Residente / 4 - Agencia de Viaje Residente a Agencia de Viaje No Residente / 6 - Agencia de Viaje Residente a Agencia de Viaje Residente El tipo de documento habilitado es 80 – CUIT | Rechaza | `autorizarComprobante` · `codigoRelacionEmisorReceptor/codigoTipoDocumento` | 34 |  |
| 354 | Al momento de informar emisor y receptor, los mismos deben ser distintos. | Rechaza | `autorizarComprobante` · `cuitRepresentada/numeroDocumento` | 34 |  |
| 355 | Si el tipo de relación Emisor Receptor seleccionada corresponde a 1,3,4 o 5, el código de país informado no podrá ser 200 – Argentina ni los sig. valores sig. valores 295, 296 y rango 250 al 265. Si el tipo de relación es 2 o 6, el código de país informado deberá ser 200 – Argentina. | Rechaza | `autorizarComprobante` · `codigoRelacionEmisorReceptor/codigoPais` | 34 | 250-265, 295 y 296 son zonas francas y territorios argentinos en la tabla de países [TAB] |
| 356 | Si el identificador impositivo es IVA Responsable Inscripto, la relación emisor receptor debe ser 2 o 6. Si el identificador impositivo es Consumidor final o Cliente del Exterior y el tipo de documento es 80 - CUIT, la relación debe ser 3 o 4. Si el identificador impositivo es Consumidor final o Cliente del Exterior y el tipo de documento es 91 - CI Extranjera / 94 - Pasaporte / 96 – DNI, la relación debe ser 1 o 5. | Rechaza [INFERIDO] | `autorizarComprobante` · `codigoRelacionEmisorReceptor/codigoTipoDocumento` | 35 | La celda "NO es superada" está vacía en el manual. También involucra `idImpositivo` |
| 360 | Es obligatorio informarlo y debe ser mayor o igual a cero. | Rechaza | `autorizarComprobante` · `importeGravado` | 35 | El XSD lo hace opcional |
| 361 | Deberá coincidir con la sumatoria de &lt;importeItem> menos el IVA correspondiente &lt;importeIVA>(calculado en base al importe y la alícuota de cada ítem), para la totalidad de los ítems. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de ítems gravados * | Rechaza | `autorizarComprobante` · `importeGravado` | 35 |  |
| 362 | No informar el campo. Previsto para alícuotas de IVA futuras. Si informa el campo, el mismo debe venir en cero. | Rechaza | `autorizarComprobante` · `importeNoGravado` | 35 |  |
| 363 | No informar el campo. Previsto para alícuotas de IVA futuras. Si informa el campo, el mismo debe venir en cero. | Rechaza | `autorizarComprobante` · `importeExento` | 35 |  |
| 364 | El campo &lt;importeReintegro> debe ser informado, menor o igual a cero si dentro del array de item existe al menos un item con &lt;codigoTurismo> igual a 1 - Servicio de hotelería - alojamiento sin desayuno / 2 - Servicio de hotelería - alojamiento con desayuno. | Rechaza | `autorizarComprobante` · `importeReintegro/codigoTurismo/codigoTipoComprobante` | 36 |  |
| 365 | El campo &lt;importeReintegro> no debe ser informado o ser igual a 0 por tener todos sus items con &lt;codigoTurismo> igual a 5 - Excedente cuando el tipo de comprobante es 196 - Nota de Débito T / 197 - Nota de Crédito T | Rechaza | `autorizarComprobante` · `importeReintegro/codigoTurismo/codigoTipoComprobante` | 36 |  |
| 366 | El campo &lt;importeReintegro> debe ser igual a la sumatoria de los importes de iva de todos los ítems que tengan &lt;codigoTurismo> igual a 1 - Servicio de hotelería - alojamiento sin desayuno / 2 - Servicio de hotelería - alojamiento con desayuno Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de ítems gravados con código de turismo igual a 1 - Servicio de hotelería - alojamiento sin desayuno / 2 - Servicio de hotelería - alojamiento con desayuno * | Rechaza | `autorizarComprobante` · `importeReintegro/codigoTurismo` | 36 | El reintegro es ≤ 0 (364) y la suma de IVA es positiva: se compara en valor absoluto [INFERIDO; el ejemplo de PyAfipWs usa −21 para un IVA de 21] |
| 367 | Si informa el campo, el mismo debe ser mayor o igual a cero. | Rechaza | `autorizarComprobante` · `importeOtrosTributos` | 36 |  |
| 368 | La sumatoria de campos importe de otros tributos debe ser igual al campo &lt;importeOtrosTributos>. Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * cantidad de tributos * | Rechaza | `autorizarComprobante` · `otroTributo/importe/importeOtrosTributos` | 37 |  |
| 369 | El importe total debe ser igual a la sumatoria de los campos: &lt;importeGravado>, &lt;importeNoGravado>, &lt;importeExento>, &lt;importeReintegro>, &lt;importeOtrosTributos>, sumatoria de &lt;subtotalIVA>&lt;importe> (dentro del arraySubtotalesIVA). Margen de error: Error relativo porcentual deberá ser &lt;= 0.01% o el error absoluto &lt;=0.01 * | Rechaza | `autorizarComprobante` · `importeTotal` | 37 | El factor del error absoluto quedó cortado en el manual ("<=0.01 *") |
| 400 | Al momento de informar los ítems, el tipo de ítem debe ser alguno de los habilitados. Consultar el método consultarTiposItem() | Rechaza | `autorizarComprobante` · `item/tipo` | 37 |  |
| 401 | Al momento de informar los códigos de turismo, informar alguno de los habilitados. Consultar el método consultarCodigosItemTurismo() | Rechaza | `autorizarComprobante` · `item/codigoTurismo` | 38 | No hay código 402 |
| 403 | Al momento de identificar la alícuota de IVA aplicada al ítem verificar que solo se corresponda con el código 5 - 21% | Rechaza | `autorizarComprobante` · `item/codigoAlicuotaIVA` | 38 |  |
| 404 | Es obligatorio identificar la descripción del ítem. Su dimensión no puede superar los 4000 caracteres alfanuméricos y no puede venir vacío. | Rechaza | `autorizarComprobante` · `item/descripcion` | 38 | No hay código 405 |
| 406 | Si el tipo de ítem es 0 - Item general el importe del item debe ser mayor o igual a 0 (cero). | Rechaza | `autorizarComprobante` · `item/tipo/importeItem` | 38 |  |
| 407 | Si el tipo de ítem es 97 - Anticipo se aceptan importes positivos o negativos. | Rechaza | `autorizarComprobante` · `item/tipo/importeItem` | 38 | Redactada como permiso; no queda claro qué la dispara |
| 408 | Si el tipo de ítem es 99 - Descuento General, el importe deberá ser negativo. | Rechaza | `autorizarComprobante` · `item/tipo/importeItem` | 38 |  |
| 409 | Si el tipo de ítem es 0 - Item general el importe IVA del ítem debe ser mayor o igual a 0 (cero). | Rechaza | `autorizarComprobante` · `item/tipo/importeIVA` | 38 |  |
| 410 | Si el tipo de ítem es 97 - Anticipo se aceptan importes de importe IVA positivos o negativos. | Rechaza | `autorizarComprobante` · `item/tipo/importeIVA` | 38 | Redactada como permiso |
| 411 | Si el tipo de ítem es 99 - Descuento General, el importe de IVA deberá ser negativo. | Rechaza | `autorizarComprobante` · `item/tipo/importeIVA` | 38-39 |  |
| 412 | La alícuota de IVA debe ser del tipo (5 - 21%). | Rechaza | `autorizarComprobante` · `item/codigoAlicuotaIVA` | 39 | Repite a 403 |
| 413 | Aplicando el porcentaje de IVA al importe del ítem &lt;importeItem>, el valor resultante debe ser igual al importe de IVA &lt;importeIVA> informado. | Rechaza | `autorizarComprobante` · `item/codigoAlicuotaIVA/importeIVA/importeItem` | 39 | Sin margen de error escrito. Fórmula exacta NO VERIFICADO (ver Comportamiento) |
| 414 | El signo del campo &lt;importeIVA> debe corresponder con el signo &lt;importeItem> | Rechaza | `autorizarComprobante` · `item/importeIVA/importeItem` | 39 |  |
| 415 | Para comprobantes 195 - Factura T, no se permite facturar solo (codigoTurismo) 5 – Excedente. | Rechaza | `autorizarComprobante` · `codigoTipoComprobante/item/codigoTurismo` | 39 |  |
| 500 | El array de subtotales de IVA es obligatorio informarlo | Rechaza | `autorizarComprobante` · `arraySubtotalesIVA` | 39 | El XSD lo hace opcional |
| 501 | Al momento de informar los subtotales de IVA, informar códigos habilitados. Consultar método consultarTiposIVA(). | Rechaza | `autorizarComprobante` · `subtotalIVA/codigo` | 39 |  |
| 502 | No se permite repetir los códigos de los subtotales de IVA. | Rechaza | `autorizarComprobante` · `subtotalIVA/codigo` | 40 |  |
| 503 | El campo importe de subtotales de IVA es obligatorio informarlo y mayor o igual a cero. | Rechaza | `autorizarComprobante` · `subtotalIVA/importe` | 40 |  |
| 504 | La suma de todos importes de IVA del array de IVA para la misma alícuota debe ser igual al valor identificado en el campo importe de subtotales de IVA para la alícuota en cuestión. | Rechaza | `autorizarComprobante` · `subtotalIVA/codigo/importe/item/codigoAlicuotaIVA/importeIVA` | 40 | Sin margen de error escrito |
| 600 | El código de tributo debe corresponder con alguno de los habilitados. Consultar el método consultarTiposTributo() | Rechaza | `autorizarComprobante` · `otroTributo/codigo` | 40 | No hay código 601 |
| 602 | La descripción de otros tributos puede no informarse. Si se informa, su dimensión no puede superar los 50 caracteres alfanuméricos. Es obligatoria para &lt;codigo> 99 – Otro. | Rechaza | `autorizarComprobante` · `otroTributo/descripcion/codigo` | 40-41 | La fila sigue en la pág. 41. XSD: 3 a 50 caracteres |
| 603 | El campo base imponible puede no informarse. Si se informa, debe contener un valor mayor o igual a cero. | Rechaza | `autorizarComprobante` · `otroTributo/baseImponible` | 41 |  |
| 604 | El campo importe debe informarse con un valor mayor o igual a cero. | Rechaza | `autorizarComprobante` · `otroTributo/importe` | 41 |  |
| 700 | Verificar que el código sea uno de los habilitados. Consultar el método consultarFormasPago() | Rechaza | `autorizarComprobante` · `formaPago/codigo` | 41 |  |
| 701 | Si el código de forma de pago hace referencia a Tarjeta de Débito o Crédito, no informar código SWIFT. | Rechaza | `autorizarComprobante` · `formaPago/codigo/swiftCode` | 41 |  |
| 702 | Si el código de forma de pago hace referencia a Tarjeta de Débito o Crédito, no informar tipo de cuenta. | Rechaza | `autorizarComprobante` · `formaPago/codigo/tipoCuenta` | 41 |  |
| 703 | Si el código de forma de pago hace referencia a Tarjeta de Débito o Crédito, no informar número de cuenta. | Rechaza | `autorizarComprobante` · `formaPago/codigo/numeroCuenta` | 41 |  |
| 704 | Si el código de forma de pago hace referencia a Tarjeta de Débito o Crédito, el tipo de tarjeta es obligatorio informarlo y debe ser un tipo habilitado. Consultar el método consultarTiposTarjeta(). | Rechaza | `autorizarComprobante` · `formaPago/codigo/tipoTarjeta` | 41-42 |  |
| 705 | Si el código de forma de pago hace referencia a Tarjeta de Débito o Crédito, el número de tarjeta es obligatorio informarlo y solo los primeros 6 caracteres. | Rechaza | `autorizarComprobante` · `formaPago/codigo/numeroTarjeta` | 42 |  |
| 720 | Si el código de forma de pago hace referencia a Transferencia Bancaria, no informar tipo de tarjeta. | Rechaza | `autorizarComprobante` · `formaPago/codigo/tipoTarjeta` | 42 |  |
| 721 | Si el código de forma de pago hace referencia a Transferencia Bancaria, no informar número de tarjeta | Rechaza | `autorizarComprobante` · `formaPago/codigo/numeroTarjeta` | 42 |  |
| 722 | Si el código de forma de pago hace referencia a Transferencia Bancaria, es obligatorio informar el tipo de cuenta y debe estar habilitado. Consultar el método consultarTiposCuenta(). | Rechaza | `autorizarComprobante` · `formaPago/codigo/tipoCuenta` | 42 |  |
| 723 | Si el código de forma de pago hace referencia a Transferencia Bancaria, informar un n° de cuenta válido. | Rechaza | `autorizarComprobante` · `formaPago/codigo/numeroCuenta` | 42 | Qué es "válido" más allá del XSD: NO VERIFICADO |
| 724 | Si el código de forma de pago hace referencia a Transferencia Bancaria e informa un código Swift, el mismo debe corresponder a un código Swift de Argentina cuando &lt;codigoRelacionEmisorReceptor> es 2 o 6. Y del exterior cuando &lt;codigoRelacionEmisorReceptor> es 1,3,4 o 5. | Rechaza | `autorizarComprobante` · `formaPago/codigo/swiftCode/codigoRelacionEmisorReceptor` | 42-43 | "Swift de Argentina" = país `AR` en las posiciones 5-6 del BIC [INFERIDO] |
| 730 | Si el código de forma de pago hace referencia a Cuenta Corriente no informar los tags &lt;tipoTarjeta> &lt;numeroTarjeta> &lt;swiftCode> &lt;tipoCuenta> &lt;numeroCuenta> | Rechaza | `autorizarComprobante` · `formaPago/codigo/tipoTarjeta/numeroTarjeta/swiftCode/tipoCuenta/numeroCuenta` | 43 | Alta en v1.1 |
| 731 | Si el código de forma de pago hace referencia a Cuenta Corriente, solo puede informarse una sola vez en el array. | Rechaza | `autorizarComprobante` · `formaPago/codigo` | 43 | Alta en v1.1 |
| 732 | Si el código de forma de pago hace referencia a Cuenta Corriente, &lt;codigoRelacionEmisorReceptor> debe ser 2 o 6 | Rechaza | `autorizarComprobante` · `formaPago/codigo/codigoRelacionEmisorReceptor` | 43 | Alta en v1.1 |
| 800 | Si el tipo de comprobante es 195 – Factura T, no asociar comprobantes. Si el tipo de comprobante a autorizar es 196 - Nota de Débito T o 197 - Nota de Crédito T es obligatorio informar comprobantes asociados. | Rechaza | `autorizarComprobante` · `codigoTipoComprobante/arrayComprobantesAsociados` | 43 |  |
| 801 | Al momento de asociar un comprobante, el tipo de comprobante asociado debe ser alguno de los habilitados. Consultar método consultarTiposComprobantes(). | Rechaza | `autorizarComprobante` · `comprobanteAsociado/codigoTipoComprobante` | 43-44 | No hay código 802 |
| 803 | El comprobante asociado debe pertenecer a un comprobante registrado y autorizado en las bases de esta administración donde los emisores de ambos sean iguales. De ser electrónicos deberían ser iguales los receptores. | Rechaza | `autorizarComprobante` · `comprobanteAsociado/codigoTipoComprobante/numeroPuntoVenta/numeroComprobante` | 44 |  |
| 804 | No se permite informar comprobantes asociados repetidos. | Rechaza | `autorizarComprobante` · `comprobanteAsociado/codigoTipoComprobante/numeroPuntoVenta/numeroComprobante` | 44 |  |
| 805 | La fecha de emisión del comprobante a autorizar debe ser mayor o igual a la fecha del comprobante asociado | Rechaza | `autorizarComprobante` · `fechaEmision/comprobanteAsociado` | 44 | Alta en v1.1 |
| 806 | El receptor del comprobante a autorizar debe coincidir con el receptor del comprobante asociado | Rechaza | `autorizarComprobante` · `comprobanteAsociado/codigoTipoDocumento/numeroDocumento` | 44 | Alta en v1.1 |
| 807 | Cuando se requiere autorizar un comprobante de Tipo Nota de Crédito, y el importe de la misma, supera el monto del o los comprobante/s asociado/s que esta ajustando. | Observa | `autorizarComprobante` · `codigoTipoComprobante/importeTotal` | 44 | Única no excluyente: va en arrayObservaciones, se otorga CAE y resultado = O. Alta en v1.5 |
| 900 | Si informa datos adicionales, verificar que sea uno de los datos habilitados. Consultar método consultarTiposDatosAdicionales() | Rechaza | `autorizarComprobante` · `tipoDatoAdicional/t` | 44-45 |  |
| 2000 | Evaluar que el tipo de comprobante que se está consultando sea uno habilitado a usar en el ws actual. Consultar el método consultarTiposComprobantes(). | Rechaza | `consultarComprobanteTipoPVentaNro` · `codigoTipoComprobante` | 52-53 |  |
| 2001 | Evaluar si el punto de venta se encuentra habilitado a utilizar en el servicio en cuestión. | Rechaza | `consultarComprobanteTipoPVentaNro` · `numeroPuntoVenta` | 53 |  |
| 2002 | Debe tener al menos un comprobante emitido para la combinación CUIT, punto de venta y tipo de comprobante | Rechaza | `consultarComprobanteTipoPVentaNro` · `cuitRepresentada/numeroPuntoVenta/codigoTipoComprobante` | 53 |  |
| 1000 | Evaluar que el tipo de comprobante que se está consultando sea uno habilitado a usar en el ws actual. Consultar el método consultarTiposComprobantes(). | Rechaza | `consultarUltimoComprobanteAutorizado` · `codigoTipoComprobante` | 57-58 |  |
| 1001 | Evaluar si el punto de venta se encuentra habilitado a utilizar en el servicio en cuestión. | Rechaza | `consultarUltimoComprobanteAutorizado` · `numeroPuntoVenta` | 58 |  |
| 1002 | Debe tener al menos un comprobante emitido para la combinación CUIT, punto de venta y tipo de comprobante | Rechaza | `consultarUltimoComprobanteAutorizado` · `cuitRepresentada/numeroPuntoVenta/codigoTipoComprobante` | 58 | Es lo que se recibe cuando todavía no se emitió nada (no hay "último = 0") |
| 1106 | Deberá contener al menos un punto de venta habilitado a utilizar con el presente ws | Rechaza | `consultarPuntosVenta` · `cuitRepresentada` | 63 |  |
| 210 | Deberá coincidir con alguno de los códigos de moneda disponibles. Consultar método consultarMonedas | Rechaza | `consultarCotizacion` · `codigoMoneda` | 108 | Reemplazó al 300 en v1.1 (pág. 141) |
| 1200 | Deberá coincidir con las formas de pago posibles 1 - debito, 2 - crédito. Consultar método consultarFormasPago | Rechaza | `consultarTiposTarjeta` · `formaPago` | 115 | Alta en v1.1 |

## Tablas y datos

El manual **no trae las tablas** que devuelven las operaciones de parámetros. Lo que sigue es lo único que se puede afirmar, sacado de los textos de validación (y marcado cuando viene de otra fuente). Para el simulador, todas estas tablas tienen que ser **configurables**.

### Tipos de comprobante (`codigoTipoComprobante`)

| Código | Descripción | Fuente |
|---|---|---|
| 195 | Factura T | [MAN] 318, 415, 800 |
| 196 | Nota de Débito T | [MAN] 365, 800 |
| 197 | Nota de Crédito T | [MAN] 365, 800 |

### Relaciones emisor-receptor (`codigoRelacionEmisorReceptor`)

Descripciones literales de 352 (pág. 33). Las columnas resumen las reglas 352, 353, 355, 356, 724 y 732:

| Código | Descripción | Emisor debe (352) | Tipos de documento del receptor (353) | País del receptor (355) | `idImpositivo` compatible (356) | SWIFT en transferencia (724) | Cuenta Corriente (732) |
|---|---|---|---|---|---|---|---|
| 1 | Alojamiento Directo a Turista No Residente | tener actividad de hospedaje vigente | 80, 91, 94, 96 | ≠ 200, 295, 296, 250-265 | Consumidor Final / Cliente del Exterior con doc. 91/94/96 | del exterior | no |
| 2 | Alojamiento a Agencia de Viaje Residente | tener actividad de hospedaje vigente | 80 | = 200 | IVA Responsable Inscripto | de Argentina | sí |
| 3 | Alojamiento a Agencia de Viaje No Residente | tener actividad de hospedaje vigente | 80 | ≠ 200, 295, 296, 250-265 | Consumidor Final / Cliente del Exterior con doc. 80 | del exterior | no |
| 4 | Agencia de Viaje Residente a Agencia de Viaje No Residente | estar en el padrón de agencias de la Secretaría de Turismo | 80 | ≠ 200, 295, 296, 250-265 | Consumidor Final / Cliente del Exterior con doc. 80 | del exterior | no |
| 5 | Agencia de Viaje Residente a Turista No Residente | estar en el padrón de agencias | 80, 91, 94, 96 | ≠ 200, 295, 296, 250-265 | Consumidor Final / Cliente del Exterior con doc. 91/94/96 | del exterior | no |
| 6 | Agencia de Viaje Residente a Agencia de Viaje Residente | estar en el padrón de agencias | 80 | = 200 | IVA Responsable Inscripto | de Argentina | sí |

Ojo: con relación 1 o 5, 353 permite doc. 80 pero 356 exige 3 o 4 cuando el doc. es 80 y el receptor es Consumidor Final o Cliente del Exterior; o sea, en la práctica 1 y 5 van con 91/94/96 [INFERIDO de cruzar ambas reglas].

### Tipos de documento (`codigoTipoDocumento`)

| Código | Descripción [MAN 353, 356] |
|---|---|
| 80 | CUIT (los textos también hablan de "CUIT país", ver `consultarCUITsPaises`) |
| 91 | CI Extranjera |
| 94 | Pasaporte |
| 96 | DNI |

### Identificador impositivo / condición de IVA del receptor (`idImpositivo`, `consultarCondicionesIVA`)

El manual solo nombra tres valores, sin código: "IVA Responsable Inscripto", "Consumidor Final", "Cliente del Exterior" (309-316, 356). El campo es `string`. PyAfipWs manda `9` con el comentario `# "Cliente del Exterior"` [CAS]. Los demás códigos: **NO VERIFICADO**.

| Valor | Doc. permitido | Reglas |
|---|---|---|
| IVA Responsable Inscripto | solo 80 (309) | CUIT activa (310) e inscripta en IVA (316); relación 2 o 6 (356) |
| Consumidor Final | 80 "CUIT país", 91, 94, 96 (311) | número alfanumérico (312); relación 3/4 con doc. 80, 1/5 con 91/94/96 (356) |
| Cliente del Exterior | 80 "CUIT país", 91, 94, 96 (313) | con doc. 80, CUIT país habilitado (314); número alfanumérico (315); relación como Consumidor Final (356) |

### Ítems

| Tabla | Código | Descripción | Fuente |
|---|---|---|---|
| Tipo de ítem (`tipo`) | 0 | Item general (importe e IVA ≥ 0) | [MAN] 406, 409 |
| | 97 | Anticipo (importe e IVA positivos o negativos) | [MAN] 407, 410 |
| | 99 | Descuento General (importe e IVA negativos) | [MAN] 408, 411 |
| Código de turismo (`codigoTurismo`) | 1 | Servicio de hotelería - alojamiento sin desayuno (genera reintegro) | [MAN] 364, 366 |
| | 2 | Servicio de hotelería - alojamiento con desayuno (genera reintegro) | [MAN] 364, 366 |
| | 5 | Excedente (una Factura T no puede tener solo esto) | [MAN] 365, 415 |
| | 3, 4, otros | **NO VERIFICADO** | — |
| Alícuota de IVA (`codigoAlicuotaIVA`, `subtotalIVA/codigo`) | 5 | 21 % (la única aceptada en ítems) | [MAN] 403, 412 |
| Tributo (`otroTributo/codigo`) | 99 | Otro (requiere descripción) | [MAN] 602 |

### Formas de pago

El manual agrupa los códigos en categorías, pero no dice qué código es cada una:
- "Tarjeta de Débito o Crédito": exige `tipoTarjeta` y `numeroTarjeta` (6 dígitos); prohíbe `swiftCode`, `tipoCuenta`, `numeroCuenta` (701-705).
- "Transferencia Bancaria": exige `tipoCuenta` y `numeroCuenta`; prohíbe `tipoTarjeta` y `numeroTarjeta`; `swiftCode` opcional, argentino o extranjero según la relación (720-724).
- "Cuenta Corriente": sin ninguno de los cinco campos, una sola vez, solo con relación 2 o 6 (730-732).

La validación 1200 de `consultarTiposTarjeta` dice "formas de pago posibles 1 - debito, 2 - crédito". PyAfipWs, en cambio, manda `codigo` 68 como "tarjeta de crédito" y `tipoTarjeta` 99 como "otra" [CAS]. Valores reales: **NO VERIFICADO**.

### Países (`codigoPais`)

200 = Argentina [MAN 355]. Los códigos que 355 excluye para relaciones 1, 3, 4 y 5 son, según la tabla de países de factura electrónica [TAB] (que wsct use esa misma tabla es [INFERIDO]):

| Código | Descripción [TAB] |
|---|---|
| 200 | ARGENTINA |
| 250 | AAE Tierra del Fuego - ARGENTINA |
| 251 | ZF La Plata - ARGENTINA |
| 252 | ZF Justo Daract - ARGENTINA |
| 253 | ZF Río Gallegos - ARGENTINA |
| 254 | ISLAS MALVINAS - ARGENTINA |
| 255 | ZF Tucumán - ARGENTINA |
| 256 | ZF Córdoba - ARGENTINA |
| 257 | ZF Mendoza - ARGENTINA |
| 258 | ZF General Pico - ARGENTINA |
| 259 | ZF Comodoro Rivadavia - ARGENTINA |
| 260 | ZF Iquique (CHILE) |
| 261 | ZF Punta Arenas (CHILE) |
| 262 | ZF Salta - ARGENTINA |
| 263 | ZF Paso de los Libres - ARGENTINA |
| 264 | ZF Puerto Iguazú - ARGENTINA |
| 265 | SECTOR ANTARTICO ARG. |
| 295 | MAR ARG ZONA ECO.EX |
| 296 | RIOS ARG NAVEG INTER |

(260 y 261 son zonas francas chilenas, pero el rango del manual las incluye.) El ejemplo de PyAfipWs usa 203 (BRASIL en [TAB]).

### Monedas

`PES` = Peso Argentino, cotización 1 (305, 319). Anexo "Cotización Monedas del Banco de la Nación Argentina" (pág. 139), monedas para las que `cancelaEnMismaMonedaExtranjera` puede tomar la cotización automática:

| Código (tal como figura) | Descripción |
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

Si `consultarMonedas` devuelve estos códigos con ceros a la izquierda (`009`, `002`, como en wsfev1) o tal cual: **NO VERIFICADO**.

### Datos de prueba de homologación

- El manual no publica CUITs ni puntos de venta de prueba. Dice que en homologación **no se validan** los datos del emisor (100-104) y que se puede usar **cualquier punto de venta** (pág. 17).
- PyAfipWs (2021) usó: emisor `20267565393`, punto de venta `4000`, receptor CUIT `50000000059` con `idImpositivo` 9, país 203 y relación 3 [CAS].

## Comportamiento a simular

### Estado que hay que guardar

1. **Comprobantes autorizados** por clave (`cuitRepresentada`, `codigoTipoComprobante`, `numeroPuntoVenta`, `numeroComprobante`): el `ComprobanteType` completo tal como se recibió (más la fecha de emisión asignada si vino vacía y la cotización BNA si se tomó automática), el CAE, su vencimiento y las observaciones. `consultarComprobanteTipoPVentaNro` devuelve eso.
2. **Último número y su fecha** por (CUIT, tipo, punto de venta), para `consultarUltimoComprobanteAutorizado` y la correlatividad.
3. **Puntos de venta** por CUIT, con `bloqueado` (`S`/`N`) y `fechaBaja`.
4. **Tablas de parámetros** (las 15 de "6 a 22"), cotizaciones por moneda y fecha, calendario de días hábiles (para la regla BNA), lista de CUIT país y lista de novedades.
5. **Padrón simulado** (solo para modo producción): emisores con estado registral, actividad de hospedaje, padrón de agencias, inscripción en IVA y domicilio; receptores CUIT con estado e inscripción en IVA (310, 316). Determinístico, no fiel a los datos reales.
6. **Modo de ambiente**: en "homologación" se saltean 100-104 y no se valida la existencia del punto de venta (301/2001/1001 pasan con cualquier número, y `consultarPuntosVenta` no devuelve datos); en "producción" se aplica todo (pág. 17).

### Orden de procesamiento de un request

1. Ruteo por `SOAPAction` (con o sin comillas). Sin `SOAPAction` válido o con XML mal formado → Fault (HTTP 500; modo F5 → `BL…500`).
2. Autenticación (token, firma, vencimiento, `cuitRepresentada` entre los representados, service `wsct`) → Fault `[wscommon_NNN] …`, `faultcode` `ns3: Receiver`. Va antes que el formato [INFERIDO de VIVO].
3. Validación XSD del resto → `arrayErroresFormato` con códigos `cvc-…`, HTTP 200, y nada de `arrayErrores` (pág. 12). Para un valor fuera de rango, Xerces reporta dos entradas: la de la faceta (`cvc-minInclusive-valid`) y `cvc-type.3.1.3` [CAS]. En `autorizarComprobante`, `resultado` es obligatorio en el XSD, así que va `R` [INFERIDO].
4. Validaciones de negocio → `arrayErrores`. Si se acumulan todas las que fallan o se corta en la primera: **NO VERIFICADO**. Conviene que sea configurable.
5. Observaciones (solo 807) → `arrayObservaciones`, con CAE.

### Numeración y correlatividad

- Independiente por (CUIT, tipo, punto de venta). El próximo a autorizar es último + 1; si no hay ninguno, 1 [INFERIDO: `NumeroComprobanteSimpleType` empieza en 1]. Cualquier otro número → **302** (rechazo).
- `consultarUltimoComprobanteAutorizado` **sin comprobantes previos** → `arrayErrores` con **1002** ("Debe tener al menos un comprobante emitido…"), sin `numeroComprobante` ni `fechaEmision` [MAN pág. 58]. **Diferencia con wsfev1**, que devuelve `CbteNro` = 0. El 0 ni siquiera es representable acá (mínimo 1). PyAfipWs interpreta "sin `numeroComprobante`" como 0 [CAS, `wsct.py`]: un cliente así funciona igual contra el simulador si este devuelve 1002 sin número.
- Con comprobantes: `numeroComprobante` y `fechaEmision` del último.

### Reintento de un comprobante ya autorizado (idempotencia)

- **No hay idempotencia.** "Si se reenvía la información sin verificar previamente la no recepción del envío previo, el sistema rechazará el envío en caso de ser un duplicado (mismo CUIT, punto de venta, tipo de comprobante y número de comprobante)" [MAN pág. 16]. **No devuelve el mismo CAE.**
- El manual no dice con qué código. Lo coherente es **302** ("La numeración no corresponde con el próximo a autorizar"), porque el número reenviado ya no es último + 1 [INFERIDO]. Es lo mismo que hace wsmtxca ("indicando un error de correlatividad", catálogo §5.3) y wsfev1 (10016).
- Recuperación prevista ante timeout: `consultarUltimoComprobanteAutorizado` para ver si quedó registrado; si sí, `consultarComprobanteTipoPVentaNro` para obtener el CAE (que vuelve en `comprobante/codigoAutorizacion` [INFERIDO]); si no, reenviar (págs. 15-16).

### Fechas

- `fechaEmision` opcional: si viene, hasta **10 días antes o después** de "la fecha de generación" (303); si no viene, se usa la fecha de proceso y se devuelve en `comprobanteResponse/fechaEmision` (pág. 30). "Fecha de generación" = fecha del día en que se procesa [INFERIDO].
- Que la fecha no sea anterior a la del último comprobante del mismo tipo y punto de venta (como en wsfev1): el manual **no lo dice**; **NO VERIFICADO**. Sí hay regla para asociados: fecha ≥ fecha del asociado (805).
- Reloj del simulador configurable y en hora de Argentina (también para el header `info`).

### CAE

- `CAE` es `xsd:long` de **14 dígitos** (pág. 29), único. Su estructura interna: **NO VERIFICADO**.
- `fechaVencimientoCAE` (`xsd:date`): el manual no da la regla. **NO VERIFICADO**. Hacerla configurable (por ejemplo, fecha de emisión + 10 días, que es lo inferido para wsfev1).
- `codigoTipoAutorizacion` tiene que venir y ser `E` (200, 201); con `E` no se mandan `codigoAutorizacion` ni `fechaVencimiento` (202, 203). CAEA no existe en wsct.

### Resultado

| `resultado` | Cuándo | `comprobanteResponse` | Arrays |
|---|---|---|---|
| `A` | Pasó todo | sí | ninguno ("el response NO va a contener el arrayErrores ni el arrayErroresFormato", pág. 15) |
| `O` | Solo falló 807 | sí | `arrayObservaciones` |
| `R` | Algún error de formato, de negocio o 5000 | no [INFERIDO por `minOccurs=0` y "la solicitud es rechazada"] | `arrayErroresFormato` **o** `arrayErrores` |

### Aritmética (todas con Round Half Even, pág. 143)

Tolerancia de 361, 366, 368 y 369: pasa si el error relativo ≤ 0,01 % **o** el error absoluto ≤ 0,01 × n, con n = cantidad de ítems gravados (361), de ítems con `codigoTurismo` 1/2 (366) o de tributos (368). Para 369 el factor quedó cortado en el manual ("<=0.01 *"); **NO VERIFICADO** (usar 0,01 × cantidad de ítems como aproximación). 413 y 504 no traen margen escrito: **NO VERIFICADO** (aplicar la misma tolerancia es la opción razonable).

1. **IVA por ítem (413):** solo alícuota 5 = 21 % (403, 412). `importeItem` es el importe **con IVA**, porque 361 define el gravado como "sumatoria de `importeItem` menos el IVA". Entonces `importeIVA` ≈ `importeItem` × 21/121 [INFERIDO; cuadra con el ejemplo de PyAfipWs: ítem 121, IVA 21]. Signo de `importeIVA` = signo de `importeItem` (414). Por tipo: 0 → ambos ≥ 0 (406, 409); 99 → ambos < 0 (408, 411); 97 → cualquier signo.
2. **Subtotales (500-504):** `arraySubtotalesIVA` obligatorio; códigos válidos y sin repetir; `importe` ≥ 0; por alícuota, `importe` = Σ `importeIVA` de los ítems con esa alícuota.
3. **Gravado (360, 361):** obligatorio, ≥ 0, = Σ (`importeItem` − `importeIVA`).
4. **No gravado y exento (362, 363):** ausentes o 0.
5. **Reintegro (364-366):**
   - Si algún ítem tiene `codigoTurismo` 1 o 2: `importeReintegro` obligatorio, ≤ 0, y |`importeReintegro`| = Σ `importeIVA` de esos ítems.
   - Si el comprobante es 196 o 197 y **todos** los ítems son `codigoTurismo` 5: `importeReintegro` ausente o 0.
   - Si no hay ítems 1/2 en otro caso: el manual no lo dice; aceptar ausente o 0 [INFERIDO].
6. **Otros tributos (367, 368, 600-604):** `importeOtrosTributos` ≥ 0 y = Σ `otroTributo/importe`; cada tributo con código válido, descripción opcional (obligatoria para 99), base ≥ 0, importe ≥ 0.
7. **Total (369):** `importeTotal` = gravado + no gravado + exento + **reintegro** + otros tributos + Σ `subtotalIVA/importe`. Como el reintegro es negativo, el turista no paga el IVA del alojamiento. Ejemplo: ítem de hotel 121 (IVA 21), tributo 1 → 100 + 0 + 0 − 21 + 1 + 21 = 101.
8. **Nota de crédito (807):** si el total de una NC supera la suma de los totales de sus asociados → observación, no rechazo.

### Moneda y cotización

1. `codigoMoneda` válido (304). `PES` → `cotizacionMoneda` = 1 (305) y sin `cancelaEnMismaMonedaExtranjera` = `S` (319).
2. `cotizacionMoneda` > 0 (317). Es obligatoria (322) salvo que se mande `cancelaEnMismaMonedaExtranjera` y la moneda esté en la tabla BNA con cotización disponible: entonces se puede omitir y se toma la de ARCA.
3. Si viene `cancelaEnMismaMonedaExtranjera`, solo para 195 (318). Si además viene `cotizacionMoneda` con moneda BNA y hay cotización, tiene que coincidir **exactamente** (320).
4. Fecha que se usa para la cotización BNA (págs. 139-140): si `fechaEmision` ≥ hoy, el día hábil anterior a hoy; si `fechaEmision` < hoy, el día hábil anterior a `fechaEmision`.
5. Banda (306): la cotización informada no puede ser "inferior al 2% ni superior en un 400%" de la orientativa de ARCA. Lectura literal: entre 0,02 × ref y 5 × ref ("superior en un 400 %"), o entre 0,02 × ref y 4 × ref. **NO VERIFICADO**: hacerlo configurable. La banda cambió tres veces (pág. 141). Si 306 se aplica también cuando el valor viene de BNA: **NO VERIFICADO**.
6. `cancelaEnMismaMonedaExtranjera` = `N`: el manual no le da efecto propio; equivale a no mandarlo [INFERIDO].
7. `consultarCotizacion(codigoMoneda, fechaCotizacion)` devuelve la cotización de la tabla del simulador para esa fecha; moneda inválida → 210. Sin dato para la fecha: **NO VERIFICADO** (proponer: `Return` sin `cotizacionMoneda` y sin errores, o error configurable).

### Receptor y relación emisor-receptor

Aplicar la matriz de "Tablas y datos" (351-356), más: emisor ≠ receptor comparando `cuitRepresentada` con `numeroDocumento` (354); `domicilioReceptor` obligatorio, máximo 300 caracteres (350); `codigoPais` válido (307) y coherente con la relación (355). `codigoPais` es opcional en el XSD, pero 355 lo exige para todas las relaciones [INFERIDO].

### Formas de pago y comprobantes asociados

- Formas de pago: la categoría (tarjeta, transferencia, cuenta corriente) de cada código tiene que ser un dato configurable de la tabla de `consultarFormasPago`, porque el manual no la da. Reglas 700-732 en la tabla de códigos. SWIFT "de Argentina" = país `AR` en las posiciones 5 y 6 [INFERIDO].
- Asociados: 195 no lleva (800); 196 y 197 sí (800). Cada uno con tipo válido (801), existente y autorizado para el mismo emisor (803), sin repetir (804), con fecha ≤ la del nuevo (805) y el mismo receptor (806).

### Operaciones de parámetros, novedades y "sin datos"

- El elemento `…Return` siempre está (`minOccurs=1`). Los tres hijos son opcionales y los arrays exigen al menos una fila, así que **"sin datos" = `Return` vacío** (por ejemplo `<consultarNovedadesReturn/>`, o `<ConsultarNovedadesReturn/>` en novedades) [INFERIDO del XSD; no hay respuesta real sin datos].
- `consultarNovedades`: lista libre de avisos (`codigo` string, `descripcion`). El manual no dice nada más. Simularla como lista configurable, vacía por defecto.
- `consultarPuntosVenta`: en producción, sin puntos de venta → 1106; en homologación, `Return` vacío (pág. 17) o 1106 (**NO VERIFICADO** cuál).
- `consultarTiposTarjeta`: `formaPago` fuera de los válidos → 1200.
- `consultarComprobanteTipoPVentaNro`: sin comprobantes para la combinación → 2002. Número inexistente dentro de una combinación con comprobantes: código **NO VERIFICADO** (proponer 2002 también).
- No hay paginación en ninguna operación.

### Forma de las respuestas

- Header `info` en toda respuesta SOAP, incluidos los Fault: `ambiente` configurable (conviene uno que no se confunda con ARCA, por ejemplo `ArcaSim - local`), `fecha` `aaaa-mm-dd hh:mm:ss` hora de Argentina, `id` `1.6.4`.
- `<?xml version='1.0' encoding='UTF-8'?>`, prefijos `S:`/`ns2:`, hijos sin namespace, una línea, `Content-Type: text/xml;charset=utf-8`.
- Fault con la forma exacta de "Autenticación" y modo F5 opcional (`HTTP/1.0 200`, sin `Content-Type`, `BL` + 13 dígitos + ` aaaa-mm-dd hh:mm:ss 500`).
- SOAP 1.2: el real responde una página HTML de bloqueo; para el simulador alcanza con rechazarlo (o imitar esa página).
- `GET /wsct/CTService`: página HTML de JAX-WS; opcional.

### Comparación breve con wsmtxca y wsfev1 (solo lo que cambia la implementación)

| Aspecto | wsct | wsmtxca | wsfev1 |
|---|---|---|---|
| Stack / errores de auth | JAX-WS; Fault `ns3: Receiver` `[wscommon_NNN]` (F5 → `BL…500`) | Java; Fault `soapenv:Client` según su manual (en vivo también `BL…500`) | ASMX; HTTP 200 con `Errors` (600) |
| Namespace de hijos | sin namespace (`unqualified`) | ver su doc. | calificados (`qualified`) |
| Comprobantes por llamada | 1 | 1 | lote |
| Último sin comprobantes | error 1002 | ver su doc. | `CbteNro` 0 |
| Duplicado | rechazo (302 [INFERIDO]) | "error de correlatividad" | 10016 |
| Resultado | `A`/`O`/`R` | `A`/`O`/`R` | `A`/`R`/`P` |
| Ítems | sí, con `codigoTurismo` y solo IVA 21 % | sí | no |
| Específico | `importeReintegro`, relación emisor-receptor, formas de pago, `idImpositivo` | `arrayCompradores`, ajustes de IVA, CAEA | CAEA |

## No verificado

1. **Service id de WSAA**: `wsct` solo por [CAS] 2021 (TA real aceptado). El manual no lo dice. Confirmar con un certificado de homologación.
2. Textos reales de `descripcion` para todos los códigos de negocio (el manual da condiciones, no mensajes). Solo se conoce el de 5000.
3. Códigos `wscommon_NNN` de los demás errores de autenticación (token mal formado, firma inválida en vivo, CUIT no representada, service equivocado) y si ARCA sigue usando `ns3: Receiver`.
4. Código 321: aparece en la lista del historial v1.6.4 sin definición.
5. Qué código rechaza un reenvío duplicado (302 [INFERIDO]).
6. Regla de `fechaVencimientoCAE` y estructura del CAE.
7. Si los errores de negocio se acumulan o se corta en el primero; orden de evaluación entre grupos (100-104 vs 200-900).
8. Fórmula exacta de 413 (¿21/121 o 21 % sobre neto?) y tolerancias de 369, 413 y 504.
9. Banda exacta de 306 (×4 o ×5) y si aplica a la cotización BNA automática.
10. Respuesta de `consultarCotizacion` sin cotización para la fecha, con fecha futura o con `PES`.
11. Respuesta de `consultarPuntosVenta` en homologación (¿vacía o 1106?).
12. Error de `consultarComprobanteTipoPVentaNro` para un número inexistente; contenido de su `arrayObservaciones`; que el CAE vuelva en `codigoAutorizacion`.
13. Valores de todas las tablas de parámetros: tipos de documento completos, condiciones de IVA (códigos de `idImpositivo`), códigos de turismo 3 y 4, formas de pago (1/2 según 1200 vs 68 en PyAfipWs) y su categoría, tipos de tarjeta, tipos de cuenta, tributos, datos adicionales, CUIT país, países, monedas (¿`009` o `9`?), novedades.
14. Si la fecha de emisión tiene que ser ≥ la del último comprobante.
15. `importeReintegro` cuando no hay ítems de hotelería y el comprobante no es una ND/NC de excedentes.
16. Valores de `dummy` distintos de `OK`.
17. Si ARCA acepta hoy `cancelaEnMismaMonedaExtranjera` vacío (el manual dice "S, N o Vacío"; el XSD lo prohíbe).
18. Límites de tasa, timeouts y tamaño máximo de request: el manual no documenta ninguno.
19. Comportamiento con autenticación válida y Body sin elemento o con `SOAPAction` que no coincide con el elemento (solo se probó sin token válido).
