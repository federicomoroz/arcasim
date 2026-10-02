# WSFEv1 (Factura Electrónica, RG 4291) — Referencia para ArcaSim

Documento de referencia para construir un simulador del web service **WSFEv1** de ARCA (ex AFIP) que se comporte como
el real, de modo que el código cliente pase de ArcaSim al servicio real cambiando sólo las URLs y el certificado.

Las tablas completas de códigos de error/observación están en **[wsfev1-codigos.md](wsfev1-codigos.md)** (496 filas,
todas las del manual). Este archivo cubre protocolo, operaciones, estructuras, reglas y comportamiento.

Fecha de relevamiento: 2026-10-01.

---

## 0. Fuentes y convenciones

### 0.1 Fuentes

| Id | Fuente | URL | Uso |
|---|---|---|---|
| **[MAN]** | Manual para el desarrollador, "RG 4291 – Proyecto FE **v4.7**", ARCA-SDG SIT, revisión 1-sep-2026, 203 págs. | <https://www.afip.gob.ar/ws/documentacion/manuales/manual-desarrollador-ARCA-COMPG.pdf> (enlazado desde <https://www.afip.gob.ar/ws/documentacion/ws-factura-electronica.asp>) | Fuente principal. Leído completo. |
| **[MAN48]** | Mismo manual, **v4.8**, revisión 1-dic-2026 (homologación externa, RG 5616/2024) | <https://www.afip.gob.ar/fe/ayuda/documentos/wsfev1-RG-4291.pdf> (desde <https://www.afip.gob.ar/ws/documentacion/homologacion-externa.asp>) | Comparado con v4.7: único cambio de contenido = entrada 4.8 del historial (ver §4.6). |
| **[WSDL]** | WSDL oficial homologación y producción | `docs/arca/wsdl/wsfev1-homologacion.wsdl`, `wsfev1-produccion.wsdl` (= `https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL`, mismo tamaño 77 307 bytes el 2026-10-01) | Estructuras y tipos: **manda sobre el manual** cuando difieren. Los dos WSDL son idénticos salvo el host. |
| **[VIVO]** | Llamadas propias a homologación y producción el 2026-10-01 (FEDummy y métodos con token inválido) | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx`, `https://servicios1.afip.gov.ar/wsfev1/service.asmx` | Formato real de respuestas, faults HTTP, mensajes de token. |
| **[CAS]** | Respuestas reales de homologación grabadas (julio 2021) en los tests de PyAfipWs | <https://github.com/PyAr/pyafipws/tree/main/tests/cassettes/test_wsfev1> | **Secundaria**: sólo para confirmar ambigüedades (valores de parámetros, mensajes, formato). Datos de 2021, pueden haber cambiado. |
| **[AFIPJS]** | Documentación del cliente afipjs | <https://github.com/egnuez/afipjs/blob/master/doc/wsfev1.md> | Secundaria, idem. |
| **[TAB]** | "Tablas del sistema" de factura electrónica (genéricas, no específicas de WSFEv1) | <https://www.afip.gob.ar/fe/ayuda/tablas.asp> → `https://www.afip.gob.ar/fe/documentos/*.xls(x)` | Contraste de parámetros. |

Las páginas se citan como "pág. PDF N" (página física del PDF de [MAN], 1..203; el índice del manual usa otra numeración).

### 0.2 Marcas de confianza

- **[MAN]**, **[WSDL]**, **[VIVO]**: verificado en fuente oficial o en el servicio.
- **[CAS]**, **[AFIPJS]**: confirmado sólo con fuente secundaria.
- **[INFERIDO]**: deducido de ejemplos o de varias fuentes, sin texto oficial explícito.
- **[NO VERIFICADO]**: no se pudo comprobar; no implementar como hecho sin validar contra homologación con certificado.

---

## 1. Protocolo

### 1.1 Endpoints

| Ambiente | URL del servicio | WSDL | Fuente |
|---|---|---|---|
| Homologación (testing) | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx` | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL` | [MAN] "Dirección URL", pág. PDF 22; [WSDL] |
| Producción | `https://servicios1.afip.gov.ar/wsfev1/service.asmx` | `https://servicios1.afip.gov.ar/wsfev1/service.asmx?WSDL` | [MAN] pág. PDF 22; [WSDL] |

[VIVO] El host es un servicio ASP.NET ASMX:
- `GET ...?WSDL` y `?wsdl` → 200 `text/xml; charset=utf-8` (77 307 bytes).
- `GET service.asmx` y `GET service.asmx?op=FEDummy` → 200 `text/html` (página de ayuda estándar de ASMX).
- Las respuestas traen cookies de balanceador F5 (`f5avr..._session_`, `TS010b76f1=...`). No son necesarias para operar.

Para que un cliente cambie sólo la URL, ArcaSim debería exponer la misma ruta `/wsfev1/service.asmx` y servir `?WSDL`
con `soap:address`/`soap12:address` apuntando a sí mismo.

### 1.2 SOAP 1.1 y 1.2

El WSDL define dos bindings sobre el mismo `portType` y la misma URL [WSDL]:

| Binding | Port | Transporte | Estilo |
|---|---|---|---|
| `tns:ServiceSoap` | `ServiceSoap` (`soap:address`) | `http://schemas.xmlsoap.org/soap/http` | `document`/`literal` |
| `tns:ServiceSoap12` | `ServiceSoap12` (`soap12:address`) | `http://schemas.xmlsoap.org/soap/http` | `document`/`literal` |

Comportamiento real [VIVO, 2026-10-01]:

| Caso | Request | Respuesta |
|---|---|---|
| SOAP 1.1 | `Content-Type: text/xml; charset=utf-8`, `SOAPAction: "http://ar.gov.afip.dif.FEV1/FEDummy"`, envelope `http://schemas.xmlsoap.org/soap/envelope/` | 200, `Content-Type: text/xml; charset=utf-8`, envelope SOAP 1.1 |
| SOAP 1.2 | `Content-Type: application/soap+xml; charset=utf-8; action="http://ar.gov.afip.dif.FEV1/FEDummy"`, envelope `http://www.w3.org/2003/05/soap-envelope` | 200, `Content-Type: application/soap+xml; charset=utf-8`, envelope SOAP 1.2 (prefijo `soap:`) |
| SOAP 1.1 **sin** header `SOAPAction` | igual a SOAP 1.1 sin el header | 200 OK: el servicio enruta por el elemento del Body |
| SOAP 1.1 con `SOAPAction` inexistente | `SOAPAction: "http://ar.gov.afip.dif.FEV1/FEXXX"` | **HTTP 500**, `soap:Fault`, `faultcode` `soap:Client`, `faultstring` `System.Web.Services.Protocols.SoapException: Server did not recognize the value of HTTP Header SOAPAction: http://ar.gov.afip.dif.FEV1/FEXXX.` + stack trace .NET |
| XML mal formado (cortado) | | **HTTP 400**, cuerpo vacío |
| Valor no numérico en un campo `int`/`long` (p. ej. `<PtoVta>abc</PtoVta>`, `<Cuit>abc</Cuit>`) | | **HTTP 500**, `soap:Fault` `soap:Client`, `Server was unable to read request. ---> System.InvalidOperationException: There is an error in XML document (1, 309). ---> System.FormatException: Input string was not in a correct format.` + stack trace |
| Elementos en otro orden que el del esquema | | Se aceptan (el deserializador .NET los lee igual; probado con `CbteTipo` antes de `Auth`) |
| Elemento desconocido | | Se ignora |
| Elementos sin namespace (`<Auth>` en vez de `<ar:Auth>`) | | Se ignoran → `Err 500 Campo Auth no fue ingresado o esta mal formado.` |
| Enteros obligatorios ausentes (p. ej. sin `PtoVta`) | | No hay fault: toman valor `0` |

Los errores de negocio (incluido token inválido) se devuelven con **HTTP 200** dentro de `Errors`; nunca como fault.

### 1.3 Namespace y SOAPAction

- Namespace de destino (`targetNamespace`) y de todos los elementos: **`http://ar.gov.afip.dif.FEV1/`**
  (mayúsculas exactas; el manual a veces escribe `http://ar.gov.afip.dif.fev1/` en minúsculas en ejemplos de respuesta,
  lo cual es una errata: las respuestas reales usan `FEV1`) [WSDL] [VIVO].
- `elementFormDefault="qualified"`: todos los hijos van en ese namespace [WSDL].
- `SOAPAction` = `http://ar.gov.afip.dif.FEV1/` + nombre de la operación, para las 22 [WSDL]:

```
http://ar.gov.afip.dif.FEV1/FECAESolicitar
http://ar.gov.afip.dif.FEV1/FECompTotXRequest
http://ar.gov.afip.dif.FEV1/FEDummy
http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado
http://ar.gov.afip.dif.FEV1/FECompConsultar
http://ar.gov.afip.dif.FEV1/FECAEARegInformativo
http://ar.gov.afip.dif.FEV1/FECAEASolicitar
http://ar.gov.afip.dif.FEV1/FECAEASinMovimientoConsultar
http://ar.gov.afip.dif.FEV1/FECAEASinMovimientoInformar
http://ar.gov.afip.dif.FEV1/FECAEAConsultar
http://ar.gov.afip.dif.FEV1/FEParamGetCotizacion
http://ar.gov.afip.dif.FEV1/FEParamGetTiposTributos
http://ar.gov.afip.dif.FEV1/FEParamGetTiposMonedas
http://ar.gov.afip.dif.FEV1/FEParamGetTiposIva
http://ar.gov.afip.dif.FEV1/FEParamGetTiposOpcional
http://ar.gov.afip.dif.FEV1/FEParamGetTiposConcepto
http://ar.gov.afip.dif.FEV1/FEParamGetPtosVenta
http://ar.gov.afip.dif.FEV1/FEParamGetTiposCbte
http://ar.gov.afip.dif.FEV1/FEParamGetCondicionIvaReceptor
http://ar.gov.afip.dif.FEV1/FEParamGetTiposDoc
http://ar.gov.afip.dif.FEV1/FEParamGetTiposPaises
http://ar.gov.afip.dif.FEV1/FEParamGetActividades
```

- Respuesta: elemento `<{Op}Response xmlns="http://ar.gov.afip.dif.FEV1/">` con hijo `<{Op}Result>`, salvo
  `FECAEASinMovimientoInformar`, cuyo resultado es `<FECAEASinMovimientoInformarResult>` [WSDL] [CAS] (el manual lo llama
  `FECAEASinMovimientoResponse/FECAEASinMovimientoResult`, pág. PDF 123: **errata del manual**).

### 1.4 Autenticación (`Auth`)

Todas las operaciones menos `FEDummy` reciben `Auth` de tipo `FEAuthRequest` [WSDL]:

| Campo | Tipo XSD | Ocurrencia XSD | Obligatorio (manual) | Descripción |
|---|---|---|---|---|
| `Token` | `string` | 0..1 | S | Token del Ticket de Acceso (TA) del WSAA |
| `Sign` | `string` | 0..1 | S | Firma del TA |
| `Cuit` | `long` | 1..1 | S | "Cuit contribuyente (representado o Emisora)" |

- El TA se pide al WSAA con `service` = **`wsfe`** y "la duración del mismo es de 12 hs" [MAN, "Autenticación", pág. PDF 19].
- `Cuit` debe estar en las relaciones del token; si no, el servicio responde `600 ValidacionDeToken: No apareció CUIT en
  lista de relaciones: ...` [MAN pág. PDF 170]. La tabla general define además `601 CUIT representada no incluida en token.`
  [MAN pág. PDF 21]. **[NO VERIFICADO]** cuál de los dos devuelve hoy el servicio.
- Orden de validación del token observado [VIVO]: presencia de `Auth` (500) → `Token` no vacío (600 "Parametro nulo o
  vacio") → base64/XML (600 "No valido token...") → fechas `gen_time`/`exp_time` (600 "No validaron las fechas...") →
  firma (600 "Error al verificar hash"). Mensajes literales en [wsfev1-codigos.md §2](wsfev1-codigos.md).
- El Token es un base64 de un XML `<sso version="2.0">` con `<id src=... unique_id gen_time exp_time/>` y
  `<operation type="login" value="granted"><login entity="33693450239" service="wsfe" uid=... authmethod="cms" regmethod="22"><relations><relation key="CUIT" reltype="4"/></relations></login></operation>` [CAS, `test_dummy.yaml`].
  Para ArcaSim: el formato exacto del TA es tema del documento de WSAA.

### 1.5 Encabezado `FEHeaderInfo`

Cada respuesta de negocio trae un `soap:Header` informativo (su procesamiento no es obligatorio) [MAN, "Estructura general
del mensaje de respuesta", pág. PDF 19-20]:

```xml
<soap:Header>
  <FEHeaderInfo xmlns="http://ar.gov.afip.dif.FEV1/">
    <ambiente>HomologacionExterno - srt</ambiente>
    <fecha>2026-10-01T22:47:00.4411169-03:00</fecha>
    <id>7.0.0.53</id>
  </FEHeaderInfo>
</soap:Header>
```

| Ambiente | `ambiente` observado | `id` observado | Fuente |
|---|---|---|---|
| Homologación | `HomologacionExterno - srt` | `7.0.0.53` | [VIVO] 2026-10-01 |
| Homologación | `HomologacionExterno - efa` | `4.3.0.0` | [CAS] 2021 |
| Producción | `Produccion - sr4` | `7.0.0.60` | [VIVO] 2026-10-01 |
| (manual) | `Homologacion - Clo` / `Produccion - Pto` | `1.0.2.0` | [MAN] pág. PDF 19-20 |

`fecha` es hora local Argentina con offset `-03:00` y 7 decimales. El sufijo de `ambiente` parece ser el nodo servidor
[INFERIDO]. Los faults (HTTP 500) **no** traen `FEHeaderInfo` [VIVO].

### 1.6 Forma general del envelope de respuesta

Respuesta real completa [VIVO, FEDummy SOAP 1.1]:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.FEV1/"><ambiente>HomologacionExterno - srt</ambiente><fecha>2026-10-01T22:47:00.4411169-03:00</fecha><id>7.0.0.53</id></FEHeaderInfo></soap:Header><soap:Body><FEDummyResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FEDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></FEDummyResult></FEDummyResponse></soap:Body></soap:Envelope>
```

Detalles de serialización observados [VIVO] [CAS] (serializador XML de .NET):
- Todo en una sola línea, sin indentación; declaración `<?xml version="1.0" encoding="utf-8"?>`.
- Elementos `string` nulos se omiten; strings vacíos se emiten como `<CAE />` (con espacio antes de `/>`).
- Elementos `int`/`long`/`double` con `minOccurs=1` se emiten siempre (valor `0` si no hay dato).
- `double` sin ceros de relleno: `122`, `0`, `101.202`, `5.25`; separador decimal `.`.
- Fechas "sin valor" en tablas de parámetros vienen como el **string literal `NULL`** (`<FchHasta>NULL</FchHasta>`,
  `<FchBaja>NULL</FchBaja>`) [CAS] [AFIPJS].

### 1.7 Estructuras comunes de errores y eventos

```xml
<Errors><Err><Code>int</Code><Msg>string</Msg></Err>...</Errors>
<Events><Evt><Code>int</Code><Msg>string</Msg></Evt>...</Events>
```

| Tipo | Campos | XSD |
|---|---|---|
| `Err` | `Code` int 1..1, `Msg` string 0..1 | `ArrayOfErr` (0..unbounded `Err`) |
| `Evt` | `Code` int 1..1, `Msg` string 0..1 | `ArrayOfEvt` |
| `Obs` | `Code` int 1..1, `Msg` string 0..1 | `ArrayOfObs`, dentro de `Observaciones` |

Manual: `Code` Int(5), `Msg` String(255) para observaciones [MAN pág. PDF 39].

Errores de infraestructura (todas las operaciones) [MAN pág. PDF 21]: 500 Error interno de aplicación; 501 Error interno de
base de datos; 502 Error interno de base de datos - Autorizador CAE / Régimen CAEA – Transacción Activa; 600 No se
corresponden token y firma. Usuario no autorizado a realizar esta operación; 601 CUIT representada no incluida en token;
602 No existen datos en nuestros registros.

---

## 2. Operaciones (las 22)

Clasificación según la RG [MAN, "Operaciones a realizar según la RG de aplicación", pág. PDF 24-25]:
- **CAE**: `FECAESolicitar`.
- **CAEA**: `FECAEASolicitar`, `FECAEAConsultar`, `FECAEASinMovimientoInformar`, `FECAEARegInformativo`,
  `FECAEASinMovimientoConsultar`.
- **Ambos**: `FEParamGetTiposCbte`, `FEParamGetTiposConcepto`, `FEParamGetTiposDoc`, `FEParamGetTiposIva`,
  `FEParamGetTiposMonedas`, `FEParamGetTiposOpcional`, `FEParamGetTiposTributos`, `FEParamGetPtosVenta`,
  `FEParamGetCotizacion`, `FEDummy`, `FECompUltimoAutorizado`, `FECompTotXRequest`, `FECompConsultar`,
  `FEParamGetActividades` (+ `FEParamGetTiposPaises` y `FEParamGetCondicionIvaReceptor`, documentados aparte en el manual).

Convención de las tablas: "XSD" = `minOccurs..maxOccurs` del WSDL; "Man." = columna "Obligatorio" del manual; "Long." =
longitud/precisión del manual (`Double (13+2)` = 13 enteros + 2 decimales).

En todos los ejemplos `xmlns:ar="http://ar.gov.afip.dif.FEV1/"`. Los requests se envían como:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
  <soapenv:Header/>
  <soapenv:Body> ... </soapenv:Body>
</soapenv:Envelope>
```

Y las respuestas como en §1.6 (con `FEHeaderInfo`). En los ejemplos de abajo se muestra sólo el contenido del Body.

### 2.1 FEDummy

Propósito: "Metodo dummy para verificacion de funcionamiento" [WSDL]; "comprobación vía ping de los elementos principales
de infraestructura" [MAN pág. PDF 125]. **No requiere `Auth`.**

Request: sin parámetros (`<ar:FEDummy/>`).

Response `FEDummyResult` (`DummyResponse`):

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `AppServer` | string 0..1 | S | String(2) | Servidor de aplicaciones |
| `DbServer` | string 0..1 | S | String(2) | Servidor de base de datos |
| `AuthServer` | string 0..1 | S | String(2) | Servidor de autenticación |

Ejemplo real [VIVO, homologación y producción, 2026-10-01]:

```xml
<ar:FEDummy/>
```
```xml
<FEDummyResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FEDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></FEDummyResult></FEDummyResponse>
```

Valores distintos de `OK` cuando un componente falla: **[NO VERIFICADO]** (el manual sólo dice String(2)).

### 2.2 FECompUltimoAutorizado

Propósito: "Retorna el ultimo comprobante autorizado para el tipo de comprobante / cuit / punto de venta ingresado / Tipo
de Emisión" [WSDL] [MAN pág. PDF 126].

Request:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `Auth` | FEAuthRequest 0..1 | S | |
| `PtoVta` | int 1..1 | S | Punto de venta |
| `CbteTipo` | int 1..1 | S | Tipo de comprobante |

Response `FECompUltimoAutorizadoResult` (`FERecuperaLastCbteResponse`):

| Campo | XSD | Man. | Long. |
|---|---|---|---|
| `PtoVta` | int 1..1 | S | Int(5) |
| `CbteTipo` | int 1..1 | S | Int(3) |
| `CbteNro` | **int** 1..1 (manual: Long(8)) | N | Long(8) |
| `Errors`, `Events` | 0..1 | N | |

Validaciones [MAN pág. PDF 128]: 11000 (PtoVta entre 1 y 99998), 11001 (CbteTipo habilitado), 11002 (PtoVta habilitado
en este WS).

Comportamiento real: si nunca se emitió ese tipo en ese punto de venta, `CbteNro` = `0` [CAS `test_main_prueba_usados.yaml`].
Con error, devuelve `PtoVta`, `CbteTipo` y `CbteNro` en `0` además de `Errors` [VIVO].

Ejemplo real [CAS, 2021]:

```xml
<ar:FECompUltimoAutorizado>
  <ar:Auth><ar:Token>…</ar:Token><ar:Sign>…</ar:Sign><ar:Cuit>20267565393</ar:Cuit></ar:Auth>
  <ar:PtoVta>4000</ar:PtoVta>
  <ar:CbteTipo>1</ar:CbteTipo>
</ar:FECompUltimoAutorizado>
```
```xml
<FECompUltimoAutorizadoResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECompUltimoAutorizadoResult><PtoVta>4000</PtoVta><CbteTipo>1</CbteTipo><CbteNro>1835</CbteNro></FECompUltimoAutorizadoResult></FECompUltimoAutorizadoResponse>
```

Con token inválido [VIVO 2026-10-01]:

```xml
<FECompUltimoAutorizadoResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECompUltimoAutorizadoResult><PtoVta>0</PtoVta><CbteTipo>0</CbteTipo><CbteNro>0</CbteNro><Errors><Err><Code>600</Code><Msg>ValidacionDeToken: No valido token. Excepcion: CargarStringBase64Token: Excepción: CargarNodos: Error al cargar token XML. Excepcion: Data at the root level is invalid. Line 1, position 1.</Msg></Err></Errors></FECompUltimoAutorizadoResult></FECompUltimoAutorizadoResponse>
```

### 2.3 FECAESolicitar

Propósito: "Solicitud de Código de Autorización Electrónico (CAE)" [WSDL]. Detalle completo en **§3**.

### 2.4 FECompConsultar

Propósito: "Consulta Comprobante emitido y su código" [WSDL]; permite consultar por tipo, número y punto de venta los
datos de un comprobante ya emitido, incluido el tipo de emisión (CAE o CAEA) [MAN pág. PDF 190]. Es el mecanismo previsto
para resolver timeouts (ver §5.4).

Request:

| Campo | XSD | Man. |
|---|---|---|
| `Auth` | 0..1 | S |
| `FeCompConsReq/CbteTipo` | int 1..1 | S |
| `FeCompConsReq/CbteNro` | long 1..1 | S |
| `FeCompConsReq/PtoVta` | int 1..1 | S |

Response `FECompConsultarResult` (`FECompConsultaResponse`): `ResultGet` (0..1), `Errors`, `Events`.

`ResultGet` es `FECompConsResponse`, que **extiende `FECAEDetRequest`** [WSDL]: contiene en el mismo orden todos los campos
del detalle de `FECAESolicitar` (§3.3: `Concepto`, `DocTipo`, `DocNro`, `CbteDesde`, `CbteHasta`, `CbteFch`, `ImpTotal`,
`ImpTotConc`, `ImpNeto`, `ImpOpEx`, `ImpTrib`, `ImpIVA`, `FchServDesde`, `FchServHasta`, `FchVtoPago`, `MonId`, `MonCotiz`,
`CanMisMonExt`, `CondicionIVAReceptorId`, `CbtesAsoc`, `Tributos`, `Iva`, `Opcionales`, `Compradores`, `PeriodoAsoc`,
`Actividades`) y a continuación:

| Campo | XSD | Man. | Descripción [MAN pág. PDF 193-194] |
|---|---|---|---|
| `Resultado` | string 0..1 | S | Resultado del procesamiento del comprobante |
| `CodAutorizacion` | string 0..1 | S | Código de autorización (CAE o CAEA) |
| `EmisionTipo` | string 0..1 | S | `CAE` o `CAEA` |
| `FchVto` | string 0..1 | S | Si CAE: vencimiento obtenido al autorizar. Si CAEA: "vigencia hasta" del CAEA |
| `FchProceso` | string 0..1 | S | Fecha de procesamiento (real: `yyyymmddhhmiss`, [CAS]) |
| `Observaciones` | ArrayOfObs 0..1 | N | Observaciones al generar el comprobante |
| `PtoVta` | int 1..1 | S | |
| `CbteTipo` | int 1..1 | S | |

Validaciones [MAN pág. PDF 194]: 10200 (PtoVta), 10201 (CbteTipo), 10104 (PtoVta no registrado), 10202 (CbteNro).
Comprobante inexistente → `602 No existen datos en nuestros registros para los parametros ingresados.` sin `ResultGet` [CAS].

El manual muestra la respuesta con `<soap12:Envelope>` y sin namespace en `FECompConsultarResponse` (pág. PDF 191);
la respuesta real lleva `xmlns="http://ar.gov.afip.dif.FEV1/"` [CAS].

Ejemplo real [CAS, `test_consulta.yaml`, 2021]:

```xml
<ar:FECompConsultar>
  <ar:Auth><ar:Token>…</ar:Token><ar:Sign>…</ar:Sign><ar:Cuit>20267565393</ar:Cuit></ar:Auth>
  <ar:FeCompConsReq><ar:CbteTipo>1</ar:CbteTipo><ar:CbteNro>1837</ar:CbteNro><ar:PtoVta>4000</ar:PtoVta></ar:FeCompConsReq>
</ar:FECompConsultar>
```
```xml
<FECompConsultarResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECompConsultarResult><ResultGet><Concepto>3</Concepto><DocTipo>80</DocTipo><DocNro>30000000007</DocNro><CbteDesde>1837</CbteDesde><CbteHasta>1837</CbteHasta><CbteFch>20210701</CbteFch><ImpTotal>122</ImpTotal><ImpTotConc>0</ImpTotConc><ImpNeto>100</ImpNeto><ImpOpEx>0</ImpOpEx><ImpTrib>1</ImpTrib><ImpIVA>21</ImpIVA><FchServDesde>20210701</FchServDesde><FchServHasta>20210701</FchServHasta><FchVtoPago>20210701</FchVtoPago><MonId>PES</MonId><MonCotiz>1</MonCotiz><Tributos><Tributo><Id>99</Id><Desc>Impuesto Municipal Matanza</Desc><BaseImp>100</BaseImp><Alic>1</Alic><Importe>1</Importe></Tributo></Tributos><Iva><AlicIva><Id>5</Id><BaseImp>100</BaseImp><Importe>21</Importe></AlicIva></Iva><Resultado>A</Resultado><CodAutorizacion>71263951827477</CodAutorizacion><EmisionTipo>CAE</EmisionTipo><FchVto>20210711</FchVto><FchProceso>20210701172106</FchProceso><Observaciones><Obs><Code>10217</Code><Msg>El credito fiscal discriminado en el presente comprobante solo podra ser computado a efectos del Procedimiento permanente de transicion al Regimen General.</Msg></Obs></Observaciones><PtoVta>4000</PtoVta><CbteTipo>1</CbteTipo></ResultGet></FECompConsultarResult></FECompConsultarResponse>
```

Si el comprobante se autorizó sin `FchServDesde`/`FchServHasta`/`FchVtoPago`, la consulta los devuelve vacíos
(`<FchServDesde />`) [CAS `test_reproceso_productos.yaml`]. Los importes vuelven normalizados (`122`, no `122.00`).

### 2.5 FECompTotXRequest

Propósito: "Retorna la cantidad maxima de registros que puede tener una invocacion al metodo FECAESolicitar /
FECAEARegInformativo" [WSDL] [MAN pág. PDF 129].

Request: sólo `Auth`. Response `FECompTotXRequestResult` (`FERegXReqResponse`): `RegXReq` int 1..1 (Man.: Int(4), S),
`Errors`, `Events`.

Valor: el manual no lo publica. Homologación devolvió **`250`** en julio 2021 [CAS `test_main_comptox.yaml`,
`test_main_prueba_multiple.yaml`]. Producción: **[NO VERIFICADO]**. Independientemente, `CantReg` debe estar entre 1 y 9998
(10001) y para FCE el máximo es 1 comprobante por request (10003) [MAN pág. PDF 40].

```xml
<ar:FECompTotXRequest><ar:Auth><ar:Token>…</ar:Token><ar:Sign>…</ar:Sign><ar:Cuit>20267565393</ar:Cuit></ar:Auth></ar:FECompTotXRequest>
```
```xml
<FECompTotXRequestResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECompTotXRequestResult><RegXReq>250</RegXReq></FECompTotXRequestResult></FECompTotXRequestResponse>
```

### 2.6 FECAEASolicitar

Propósito: "Solicitud de Código de Autorización Electrónico Anticipado (CAEA)" [WSDL]. Superadas las validaciones otorga un
CAEA y su período de vigencia [MAN pág. PDF 90]. Ver §6 para el flujo CAEA.

Request (los parámetros van directamente bajo el elemento de la operación; el manual menciona un contenedor `FeCAEAReq` que
**no existe** en el WSDL):

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `Auth` | 0..1 | S | | |
| `Periodo` | int 1..1 | S | Int(6) | `yyyymm` |
| `Orden` | short 1..1 | S | Short(1) | Quincena: 1 o 2 |

Response `FECAEASolicitarResult` (`FECAEAGetResponse`): `ResultGet` (`FECAEAGet`), `Errors`, `Events`.

`FECAEAGet` [WSDL] [MAN pág. PDF 93]:

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `CAEA` | string 0..1 | N | String(14) | Código de Autorización Electrónico Anticipado |
| `Periodo` | int 1..1 | S | Int(6) | `yyyymm` |
| `Orden` | short 1..1 | S | Short(1) | 1 o 2 |
| `FchVigDesde` | string 0..1 | N | String(8) | Vigencia desde |
| `FchVigHasta` | string 0..1 | N | String(8) | Vigencia hasta |
| `FchTopeInf` | string 0..1 | N | String(8) | Fecha tope para informar los comprobantes del CAEA |
| `FchProceso` | string 0..1 | N | String(14) | `yyyymmddhhmiss` |
| `Observaciones` | ArrayOfObs 0..1 | N | | Observaciones del CAEA (p. ej. 15014, 15015, 15017, 15018) |

Validaciones: 15000–15016 excluyentes, 15014/15015/15017/15018 observaciones [MAN pág. PDF 94-95]; tabla en
[wsfev1-codigos.md §4.3](wsfev1-codigos.md).

Ejemplo del manual [MAN pág. PDF 95-96] (el manual muestra la respuesta como SOAP 1.2):

```xml
<ar:FECAEASolicitar>
  <ar:Auth><ar:Token>un string</ar:Token><ar:Sign>un string</ar:Sign><ar:Cuit>33333333333</ar:Cuit></ar:Auth>
  <ar:Periodo>201011</ar:Periodo>
  <ar:Orden>1</ar:Orden>
</ar:FECAEASolicitar>
```
```xml
<FECAEASolicitarResponse xmlns="http://ar.gov.afip.dif.FEV1/">
  <FECAEASolicitarResult>
    <ResultGet>
      <CAEA>12345678901234</CAEA>
      <Periodo>201011</Periodo>
      <Orden>1</Orden>
      <FchVigDesde>20101101</FchVigDesde>
      <FchVigHasta>20101115</FchVigHasta>
      <FchTopeInf>20101215</FchTopeInf>
      <FchProceso>20101028</FchProceso>
    </ResultGet>
  </FECAEASolicitarResult>
</FECAEASolicitarResponse>
```

Con observación (mismo ejemplo + `<Observaciones><Obs><Code>15015</Code><Msg> Registra problemas en el domicilio fiscal
electrónico. No se encuentra adherido </Msg></Obs></Observaciones>` dentro de `ResultGet`) [MAN pág. PDF 97].

Error real fuera de ventana [CAS `test_main_solicitar_caea.yaml`, 2021-07-22]: **sin** `ResultGet`:

```xml
<FECAEASolicitarResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAEASolicitarResult><Errors><Err><Code>15006</Code><Msg>Fecha de envío podrá ser desde 5 días corridos anteriores al inicio hasta el último dia de cada quincena. Del 6/26/2021 hasta 7/15/2021</Msg></Err></Errors></FECAEASolicitarResult></FECAEASolicitarResponse>
```

### 2.7 FECAEAConsultar

Propósito: "Consultar CAEA emitidos" [WSDL]; consulta un CAEA ya otorgado para un período/orden [MAN pág. PDF 97].

Request: `Auth`, `Periodo` int 1..1, `Orden` short 1..1 (idéntico a FECAEASolicitar).
Response: `FECAEAConsultarResult` del mismo tipo `FECAEAGetResponse` (§2.6). El manual describe `FchProceso` como String(8)
en esta operación (pág. PDF 100) y String(14) en FECAEASolicitar.

Validaciones [MAN pág. PDF 100]: 15004 (Periodo AAAAMM), 15005 (Orden 1 o 2). Sin CAEA para ese período: 602 [CAS].

Comportamiento real con error [CAS]: **sí** devuelve `ResultGet` con `Periodo` y `Orden` (eco de los enteros obligatorios):

```xml
<FECAEAConsultarResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAEAConsultarResult><ResultGet><Periodo>202107</Periodo><Orden>1</Orden></ResultGet><Errors><Err><Code>602</Code><Msg>No existen datos en nuestros registros para los parametros ingresados.</Msg></Err></Errors></FECAEAConsultarResult></FECAEAConsultarResponse>
```

Respuesta exitosa: igual al ejemplo de FECAEASolicitar con `FECAEAConsultarResponse/FECAEAConsultarResult` [MAN pág. PDF 101].

### 2.8 FECAEARegInformativo

Propósito: "Rendición de comprobantes asociados a un CAEA" [WSDL]; informa la totalidad de comprobantes emitidos con cada
CAEA [MAN pág. PDF 131]. Mismo esquema de aprobación/rechazo/observación que FECAESolicitar.

Request `FeCAEARegInfReq` (`FECAEARequest`):
- `FeCabReq` (`FECAEACabRequest` = `FECabRequest`): `CantReg`, `PtoVta`, `CbteTipo` (idéntico a §3.2).
- `FeDetReq` (`ArrayOfFECAEADetRequest`) con 0..n `FECAEADetRequest`, que **extiende `FEDetRequest`** (todos los campos de
  §3.3, incluidos `CanMisMonExt`, `CondicionIVAReceptorId`, `Compradores`, `PeriodoAsoc`, `Actividades`) y agrega al final:

| Campo | XSD | Man. | Long. | Descripción [MAN pág. PDF 135] |
|---|---|---|---|---|
| `CAEA` | string 0..1 | S | String(14) | CAEA con el que se emitió |
| `CbteFchHsGen` | string 0..1 | N (obligatorio desde v4.6, ver abajo) | String(14) | Fecha y hora de generación del comprobante por contingencia, `yyyymmddhhmiss` |

Diferencias de reglas respecto de CAE (detalle en [wsfev1-codigos.md §4.2](wsfev1-codigos.md)):
- `CbteFch` debe estar dentro de la vigencia del CAEA (702) y ser ≥ a la del último informado (704).
- `CbteFchHsGen`: obligatorio si el punto de venta es de contingencia (1440); formato (1441); no informarlo si no es
  contingencia (1442, observación); `CbteFch` en N±5 (concepto 1) o N±10 (concepto 2/3) respecto de `CbteFchHsGen` (1445,
  observación). Desde v4.6 (RG 5782/2025) "Todos los puntos de venta CAEA pasarán a ser considerados como Contingencia" y
  "El campo CbteFchHsGen pasará a ser de integración obligatoria" [MAN historial v4.6, pág. PDF 15].
- `Compradores` no habilitado en CAEA (1432).
- Muchas validaciones que en CAE rechazan, en CAEA **observan** (p. ej. 724 total, 725 IVA, 1406 tributos, 1408 base
  imponible, 708 receptor no activo; `CondicionIVAReceptorId` inválido para la clase: CAE rechaza 10243, CAEA observa 824).

Response `FECAEARegInformativoResult` (`FECAEAResponse`): `FeCabResp` (`FECabResponse`, §3.5), `FeDetResp` con 0..n
`FECAEADetResponse` (= `FEDetResponse` + `CAEA`), `Events`, `Errors`.

| Campo de `FECAEADetResponse` | XSD | Man. |
|---|---|---|
| `Concepto`, `DocTipo`, `DocNro`, `CbteDesde`, `CbteHasta` | int/long 1..1 | S |
| `CbteFch` | string 0..1 | N |
| `Resultado` | string 0..1 | S |
| `Observaciones` | ArrayOfObs 0..1 | N |
| `CAEA` | string 0..1 | N |

Ejemplo del manual (aprobado) [MAN pág. PDF 168-169]:

```xml
<ar:FECAEARegInformativo>
  <ar:Auth><ar:Token>PD…</ar:Token><ar:Sign>IT…</ar:Sign><ar:Cuit>23000000004</ar:Cuit></ar:Auth>
  <ar:FeCAEARegInfReq>
    <ar:FeCabReq><ar:CantReg>1</ar:CantReg><ar:PtoVta>9800</ar:PtoVta><ar:CbteTipo>1</ar:CbteTipo></ar:FeCabReq>
    <ar:FeDetReq>
      <ar:FECAEADetRequest>
        <ar:Concepto>1</ar:Concepto><ar:DocTipo>80</ar:DocTipo><ar:DocNro>30000000007</ar:DocNro>
        <ar:CbteDesde>33</ar:CbteDesde><ar:CbteHasta>33</ar:CbteHasta><ar:CbteFch>20110211</ar:CbteFch>
        <ar:ImpTotal>100.00</ar:ImpTotal><ar:ImpTotConc>100.00</ar:ImpTotConc><ar:ImpNeto>0</ar:ImpNeto>
        <ar:ImpOpEx>0.00</ar:ImpOpEx><ar:ImpIva>0</ar:ImpIva><ar:ImpTrib>0</ar:ImpTrib>
        <ar:MonId>PES</ar:MonId><ar:MonCotiz>1</ar:MonCotiz>
        <ar:CondicionIVAReceptorId>1</ar:CondicionIVAReceptorId>
        <ar:CAEA>21064126523746</ar:CAEA>
      </ar:FECAEADetRequest>
    </ar:FeDetReq>
  </ar:FeCAEARegInfReq>
</ar:FECAEARegInformativo>
```
(Nota: el manual escribe `<ar:ImpIva>`; el elemento correcto es `ImpIVA`. Con el nombre en otra capitalización el
deserializador lo ignora y el valor queda en 0 [INFERIDO de §1.2].)

```xml
<FECAEARegInformativoResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAEARegInformativoResult><FeCabResp><Cuit>23000000004</Cuit><PtoVta>9800</PtoVta><CbteTipo>1</CbteTipo><FchProceso>20110306</FchProceso><CantReg>1</CantReg><Resultado>A</Resultado><Reproceso>N</Reproceso></FeCabResp><FeDetResp><FECAEADetResponse><Concepto>1</Concepto><DocTipo>80</DocTipo><DocNro>30000000007</DocNro><CbteDesde>33</CbteDesde><CbteHasta>33</CbteHasta><CbteFch>20110211</CbteFch><Resultado>A</Resultado><CAEA>21064126523746</CAEA></FECAEADetResponse></FeDetResp></FECAEARegInformativoResult></FECAEARegInformativoResponse>
```

Rechazo real por CAEA vacío [CAS `test_main_prueba_caea.yaml`, 2021] (error en `Errors` y detalle con `R`):

```xml
<FECAEARegInformativoResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAEARegInformativoResult><FeCabResp><Cuit>20267565393</Cuit><PtoVta>3</PtoVta><CbteTipo>3</CbteTipo><FchProceso>20210722160501</FchProceso><CantReg>1</CantReg><Resultado>R</Resultado><Reproceso>N</Reproceso></FeCabResp><FeDetResp><FECAEADetResponse><Concepto>3</Concepto><DocTipo>80</DocTipo><DocNro>30500010912</DocNro><CbteDesde>504</CbteDesde><CbteHasta>504</CbteHasta><CbteFch>20210701</CbteFch><Resultado>R</Resultado><CAEA /></FECAEADetResponse></FeDetResp><Errors><Err><Code>782</Code><Msg>El &lt;CAEA&gt; es obligatorio informarlo.</Msg></Err></Errors></FECAEARegInformativoResult></FECAEARegInformativoResponse>
```

Otros ejemplos del manual (págs. PDF 169-187): rechazo por token (600), rechazo de cabecera por `CbteTipo` inválido (700,
con `<CbteTipo>0</CbteTipo>` en `FeCabResp`), rechazo de detalle por concepto (713 en `Obs`, `Resultado=R`), aprobación con
observación (724), lote de 3 aprobado, lote parcial (38 A, 39 R por 900, 40 R "no procesado"), lote con `CantReg`
inconsistente (10002 en `Errors`, los 3 detalles con `R`).

### 2.9 FECAEASinMovimientoInformar

Propósito: "Informa CAEA sin movimientos" [WSDL]: informa qué CAEA otorgados no tuvieron movimiento para un punto de venta
[MAN pág. PDF 122].

Request:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `Auth` | 0..1 | S | |
| `PtoVta` | int 1..1 | S | Punto de venta en el que no se usó el CAEA |
| `CAEA` | string 0..1 | S | CAEA informado como no utilizado |

Response `FECAEASinMovimientoInformarResult` (`FECAEASinMovResponse` = `FECAEASinMov` + `Resultado`, `Errors`, `Events`),
en este orden [WSDL] [CAS]:

| Campo | XSD | Man. | Long. |
|---|---|---|---|
| `CAEA` | string 0..1 | S | String(14) |
| `FchProceso` | string 0..1 | N | String(8) (real: `20210722`) |
| `PtoVta` | int 1..1 | S | Int(5) |
| `Resultado` | string 0..1 | N | String(1): A / R |
| `Errors`, `Events` | | N | |

Validaciones [MAN pág. PDF 124-125]: 1200–1207, 1209.

Ejemplo real [CAS `test_main_sinmovimiento_caea.yaml`, 2021]:

```xml
<ar:FECAEASinMovimientoInformar>
  <ar:Auth><ar:Token>…</ar:Token><ar:Sign>…</ar:Sign><ar:Cuit>20267565393</ar:Cuit></ar:Auth>
  <ar:PtoVta>4000</ar:PtoVta>
  <ar:CAEA>71293955911805</ar:CAEA>
</ar:FECAEASinMovimientoInformar>
```
```xml
<FECAEASinMovimientoInformarResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAEASinMovimientoInformarResult><CAEA>71293955911805</CAEA><FchProceso>20210722</FchProceso><PtoVta>4000</PtoVta><Resultado>R</Resultado><Errors><Err><Code>1200</Code><Msg>El codigo de autorizacion debe ser del tipo CAEA</Msg></Err></Errors></FECAEASinMovimientoInformarResult></FECAEASinMovimientoInformarResponse>
```

Respuesta aprobada: misma estructura con `Resultado` `A` y sin `Errors` [INFERIDO; no hay ejemplo real].

### 2.10 FECAEASinMovimientoConsultar

Propósito: "Consulta CAEA informado como sin movimientos" [WSDL]: qué puntos de venta se notificaron sin movimiento para un
CAEA; si se informa el punto de venta, sólo ese [MAN pág. PDF 187].

Request:

| Campo | XSD | Man. |
|---|---|---|
| `Auth` | 0..1 | S |
| `CAEA` | string 0..1 | S |
| `PtoVta` | int 1..1 | S (el texto dice "En caso de informar el punto de venta...": comportamiento con `0` **[NO VERIFICADO]**) |

Response `FECAEASinMovimientoConsultarResult` (`FECAEASinMovConsResponse`): `ResultGet` = `ArrayOfFECAEASinMov` con 0..n
`FECAEASinMov` {`CAEA` String(14), `FchProceso` String(8) "fecha en que se informó como sin movimiento", `PtoVta` Int(5)},
`Errors`, `Events` [WSDL] [MAN pág. PDF 188-190].

Validaciones [MAN pág. PDF 190]: 10100, 10101, 10102, 10105.

Estructura (sin ejemplo real; derivada del WSDL):

```xml
<ar:FECAEASinMovimientoConsultar>
  <ar:Auth>…</ar:Auth>
  <ar:CAEA>21064126523746</ar:CAEA>
  <ar:PtoVta>9800</ar:PtoVta>
</ar:FECAEASinMovimientoConsultar>
```
```xml
<FECAEASinMovimientoConsultarResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAEASinMovimientoConsultarResult><ResultGet><FECAEASinMov><CAEA>21064126523746</CAEA><FchProceso>20110216</FchProceso><PtoVta>9800</PtoVta></FECAEASinMov></ResultGet></FECAEASinMovimientoConsultarResult></FECAEASinMovimientoConsultarResponse>
```

### 2.11 Recuperadores de parámetros sin argumentos

`FEParamGetTiposCbte`, `FEParamGetTiposConcepto`, `FEParamGetTiposDoc`, `FEParamGetTiposIva`, `FEParamGetTiposMonedas`,
`FEParamGetTiposOpcional`, `FEParamGetTiposTributos`, `FEParamGetTiposPaises`, `FEParamGetPtosVenta`,
`FEParamGetActividades`: request con **sólo `Auth`**; response `{Op}Result` con `ResultGet` (array), `Errors`, `Events`.
Ninguno tiene validaciones propias en el manual (sólo §1.7).

| Operación | Propósito [WSDL] | Elemento de `ResultGet` | Campos (XSD) | Longitudes [MAN] | Pág. PDF |
|---|---|---|---|---|---|
| `FEParamGetTiposCbte` | Tipos de comprobante utilizables | `CbteTipo` | `Id` int 1..1, `Desc` string, `FchDesde` string, `FchHasta` string | Id Int(3), Desc String(250), FchDesde String(8) S, FchHasta String(8) N | 101-103 |
| `FEParamGetTiposConcepto` | Identificadores para `Concepto` | `ConceptoTipo` | `Id` int, `Desc`, `FchDesde`, `FchHasta` | Id Int(2) | 103-105 |
| `FEParamGetTiposDoc` | Tipos de documento | `DocTipo` | `Id` int, `Desc`, `FchDesde`, `FchHasta` | Id Int(2) | 106-108 |
| `FEParamGetTiposIva` | Tipos de IVA | `IvaTipo` | `Id` **string**, `Desc`, `FchDesde`, `FchHasta` | Id Int(2) | 108-110 |
| `FEParamGetTiposMonedas` | Monedas | `Moneda` | `Id` string, `Desc`, `FchDesde`, `FchHasta` | Id String(3) | 110-112 |
| `FEParamGetTiposOpcional` | Identificadores de `Opcionales` | `OpcionalTipo` | `Id` string, `Desc`, `FchDesde`, `FchHasta` | Id String(4) | 112-114 |
| `FEParamGetTiposTributos` | Tributos | `TributoTipo` | `Id` short, `Desc`, `FchDesde`, `FchHasta` | Id Int(2) | 114-116 |
| `FEParamGetTiposPaises` | Países | `PaisTipo` | `Id` short, `Desc` (**sin fechas**) | Id Int(3), Desc String(250) | 196-198 |
| `FEParamGetPtosVenta` | Puntos de venta y su estado | `PtoVenta` | `Nro` int 1..1, `EmisionTipo`, `Bloqueado`, `FchBaja` | Nro Int(5), EmisionTipo String(8), Bloqueado String(1) S/N, FchBaja String(8) | 116-119 |
| `FEParamGetActividades` | Actividades habilitadas del emisor | `ActividadesTipo` | `Id` long 1..1, `Orden` short 1..1, `Desc` | Id Long, Orden Short(3), Desc String(180) | 198-199 |

Ejemplo real [CAS `test_main_parametros.yaml`, 2021] (`FEParamGetTiposIva`):

```xml
<ar:FEParamGetTiposIva><ar:Auth><ar:Token>…</ar:Token><ar:Sign>…</ar:Sign><ar:Cuit>20267565393</ar:Cuit></ar:Auth></ar:FEParamGetTiposIva>
```
```xml
<FEParamGetTiposIvaResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FEParamGetTiposIvaResult><ResultGet><IvaTipo><Id>3</Id><Desc>0%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>4</Id><Desc>10.5%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>5</Id><Desc>21%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>6</Id><Desc>27%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>8</Id><Desc>5%</Desc><FchDesde>20141020</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>9</Id><Desc>2.5%</Desc><FchDesde>20141020</FchDesde><FchHasta>NULL</FchHasta></IvaTipo></ResultGet></FEParamGetTiposIvaResult></FEParamGetTiposIvaResponse>
```

Ejemplo real `FEParamGetPtosVenta` [CAS `test_main_ptos_venta.yaml`, 2021]:

```xml
<FEParamGetPtosVentaResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FEParamGetPtosVentaResult><ResultGet><PtoVenta><Nro>3</Nro><EmisionTipo>CAE - Ri Iva</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja>NULL</FchBaja></PtoVenta><PtoVenta><Nro>4</Nro><EmisionTipo>CAE - Ri Iva</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja>NULL</FchBaja></PtoVenta><PtoVenta><Nro>6</Nro><EmisionTipo>CAE - Ri Iva</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja>NULL</FchBaja></PtoVenta></ResultGet></FEParamGetPtosVentaResult></FEParamGetPtosVentaResponse>
```

Notas:
- `EmisionTipo` real es más largo que el String(8) del manual (`CAE - Ri Iva`). Otros valores (CAEA, monotributo, exento,
  contingencia, SEGWS): **[NO VERIFICADO]**.
- El manual dice que un contribuyente que opta por CAEA "no es necesario que implemente soporte para los métodos
  FEParamGetPtosVenta" [MAN pág. PDF 25].
- Sin puntos de venta: probablemente 602 **[NO VERIFICADO]**.
- `FEParamGetActividades`: el ejemplo del manual tiene espacios dentro de las etiquetas (`< ActividadesTipo>`) y la tabla
  se titula por error `FEParamGetTiposPaisesResult` (pág. PDF 199). Valores reales: **[NO VERIFICADO]** (dependen del padrón
  del emisor).

### 2.12 FEParamGetCotizacion

Propósito: "Recupera la cotizacion de la moneda consultada y su fecha" [WSDL]; "Retorna la última cotización de la base de
datos aduanera de la moneda ingresada. Este valor es orientativo." [MAN pág. PDF 119].

Request:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `Auth` | 0..1 | S | |
| `MonId` | string 0..1 | S | Moneda a cotizar |
| `FchCotiz` | string 0..1 | N | Fecha a consultar (agregado en v4.0, 17/03/2025), `yyyymmdd` |

Response `FEParamGetCotizacionResult` (`FECotizacionResponse`): `ResultGet` (`Cotizacion`), `Errors`, `Events`.

| Campo de `Cotizacion` | XSD | Man. | Long. |
|---|---|---|---|
| `MonId` | string 0..1 | S | String(3) |
| `MonCotiz` | double 1..1 | N | Double(4+6) |
| `FchCotiz` | string 0..1 | N | String(8) `yyyymmdd` |

Validaciones [MAN pág. PDF 122]: 12000 (MonId habilitado), 12001 (MonId obligatorio), 12002 (FchCotiz formato).

Ejemplo real [CAS `test_main_cotizacion.yaml`, consultado 2021-07-22]:

```xml
<ar:FEParamGetCotizacion><ar:Auth>…</ar:Auth><ar:MonId>DOL</ar:MonId></ar:FEParamGetCotizacion>
```
```xml
<FEParamGetCotizacionResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FEParamGetCotizacionResult><ResultGet><MonId>DOL</MonId><MonCotiz>101.202</MonCotiz><FchCotiz>20210721</FchCotiz></ResultGet></FEParamGetCotizacionResult></FEParamGetCotizacionResponse>
```
(Consultado el 22/07, devolvió la cotización del día hábil anterior.)

`PES` como `MonId`: **[NO VERIFICADO]** si devuelve 1 o error.

### 2.13 FEParamGetCondicionIvaReceptor

Propósito: "Recupera la condicion frente al IVA del receptor (para una clase de comprobante determinada o para todos si no
se especifica)" [WSDL]. Agregado en v4.0 (17/03/2025), modificado en v4.1 [MAN historial].

Request:

| Campo | XSD | Man. | Descripción [MAN pág. PDF 200] |
|---|---|---|---|
| `Auth` | 0..1 | S | |
| `ClaseCmp` | string 0..1 | N | `A`, `ALEY` (A con leyenda "operación sujeta a retención"), `B`, `C` o `49` (Bienes Usados). Si no se informa, lista todas las combinaciones |

Errata del manual: el ejemplo de request usa el elemento `ar:FEParamGetCondicionFrenteIvaReceptor`, que **no existe**; el
correcto es `ar:FEParamGetCondicionIvaReceptor` [WSDL]. La validación 10244 habla de `<Cmp_Clase>` aunque el parámetro de
entrada es `ClaseCmp`.

Response `FEParamGetCondicionIvaReceptorResult`: `ResultGet` con 0..n `CondicionIvaReceptor` {`Id` int 1..1 (Int(3)),
`Desc` string (String(250)), `Cmp_Clase` string (String(5))}, `Errors`, `Events`.

Validación [MAN pág. PDF 202]: 10244 (ClaseCmp inválida).

Valores: ver §7.8. **[NO VERIFICADO]** los textos exactos de `Desc` y de `Cmp_Clase` que devuelve el servicio (no hay
respuesta real disponible: requiere certificado). Estructura derivada del WSDL:

```xml
<ar:FEParamGetCondicionIvaReceptor><ar:Auth>…</ar:Auth><ar:ClaseCmp>A</ar:ClaseCmp></ar:FEParamGetCondicionIvaReceptor>
```
```xml
<FEParamGetCondicionIvaReceptorResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FEParamGetCondicionIvaReceptorResult><ResultGet><CondicionIvaReceptor><Id>1</Id><Desc>IVA Responsable Inscripto</Desc><Cmp_Clase>A</Cmp_Clase></CondicionIvaReceptor>…</ResultGet></FEParamGetCondicionIvaReceptorResult></FEParamGetCondicionIvaReceptorResponse>
```

### 2.14 Ejemplos de request de los demás recuperadores

Todos tienen la misma forma (sólo cambia el nombre del elemento):

```xml
<ar:FEParamGetTiposCbte>
  <ar:Auth><ar:Token>string</ar:Token><ar:Sign>string</ar:Sign><ar:Cuit>long</ar:Cuit></ar:Auth>
</ar:FEParamGetTiposCbte>
```

y responden `<{Op}Response xmlns="http://ar.gov.afip.dif.FEV1/"><{Op}Result><ResultGet><Elem>…</Elem>…</ResultGet></{Op}Result></{Op}Response>`
con los valores de §7. Ante error de token: `<{Op}Result><Errors><Err><Code>600</Code>…</Err></Errors></{Op}Result>` sin
`ResultGet` [VIVO].

---

## 3. FECAESolicitar en profundidad

Fuente principal: [MAN] "Método de autorización de comprobantes electrónicos por CAE (FECAESolicitar)", págs. PDF 25-90;
estructuras: [WSDL].

### 3.1 Semántica

El cliente envía un comprobante o un lote. Posibles resultados por comprobante [MAN pág. PDF 25]:
- supera todas las validaciones → aprobado, se asigna CAE y fecha de vencimiento;
- no supera alguna **no excluyente** → aprobado **con observaciones**, con CAE;
- no supera alguna **excluyente** → no aprobado, sin CAE.

Resultado de cabecera `FeCabResp/Resultado`: `A` = aprobado, `R` = rechazado, `P` = parcial [MAN ejemplo, pág. PDF 79;
"Operatoria ante errores", pág. PDF 167 para CAEA].

Request: `Auth` + `FeCAEReq` (`FECAERequest` = `FeCabReq` + `FeDetReq`). Todos los comprobantes de un request deben ser del
mismo `CbteTipo` y `PtoVta` [MAN pág. PDF 28].

### 3.2 FeCabReq (`FECAECabRequest` → `FECabRequest`)

| Campo | XSD | Man. | Long. | Reglas | Códigos |
|---|---|---|---|---|---|
| `CantReg` | int 1..1 | S | Int(4) | 1..9998; igual a la cantidad de `FECAEDetRequest` enviados; ≤ `FECompTotXRequest`; FCE: 1 | 10001, 10002, 10003 |
| `PtoVta` | int 1..1 | S | Int(5) (ampliado de 4 a 5 dígitos el 01/10/2018, historial v2.12) | 1..99998; dado de alta y de tipo RECE; para emisores exentos clase C, tipo "COMPROBANTES – EXENTO EN IVA – WEB SERVICES"; SEGWS sólo para Seguros de Caución | 10004, 10005, 10096, 10282 |
| `CbteTipo` | int 1..1 | S | Int(3) | > 0; uno de los habilitados (§7.1) | 10006, 10007 |

### 3.3 FeDetReq / FECAEDetRequest (`FEDetRequest`)

Orden exacto del esquema [WSDL]; obligatoriedad y longitudes del manual [MAN págs. PDF 28-36].

| # | Campo | XSD | Man. | Long./formato | Descripción y reglas principales | Códigos |
|---:|---|---|---|---|---|---|
| 1 | `Concepto` | int 1..1 | S | Int(2) | 1 Productos, 2 Servicios, 3 Productos y Servicios. Bienes usados (49): sólo 1 | 10030, 10085 |
| 2 | `DocTipo` | int 1..1 | S | Int(2) | Código del documento del comprador (§7.2) | 10013, 10015 (incluye "FCE: 80 CUIT"), 10270–10272 |
| 3 | `DocNro` | long 1..1 | S | Long(11) | Número del comprador; distinto del emisor | 10015, 10017, 10063, 10069, 10238, 10247–10249, 10271, 10284 |
| 4 | `CbteDesde` | long 1..1 | S | Long(8), 1..99999999 | Debe ser último autorizado + 1 | 10008, 10016 |
| 5 | `CbteHasta` | long 1..1 | S | Long(8), 1..99999999 | = `CbteDesde` salvo clase B; ≥ `CbteDesde` | 10010, 10011, 10012 |
| 6 | `CbteFch` | string 0..1 | N | String(8) `yyyymmdd` | Si no se envía "se asignará la fecha de proceso". Rangos en §4.2 | 10016, 10152 |
| 7 | `ImpTotal` | double 1..1 | S | Double(13+2) | = ImpTotConc + ImpNeto + ImpOpEx + ImpTrib + ImpIVA (clase C: ImpNeto + ImpTrib) | 10048, 10065 |
| 8 | `ImpTotConc` | double 1..1 | S | Double(13+2) | Neto no gravado; ≥ 0; ≤ total; clase C = 0; bienes usados con emisor monotributista = subtotal | 10043 |
| 9 | `ImpNeto` | double 1..1 | S | Double(13+2) | Neto gravado; ≥ 0; ≤ total; clase C = subtotal; bienes usados monotributista: 0 o ausente | 10045 |
| 10 | `ImpOpEx` | double 1..1 | S | Double(13+2) | Exento; ≥ 0; ≤ total; clase C = 0 | 10044 |
| 11 | `ImpTrib` | double 1..1 | S | Double(13+2) | Suma de `Tributo/Importe` | 10029, 10046 |
| 12 | `ImpIVA` | double 1..1 | S | Double(13+2) | Suma de `AlicIva/Importe`; clase C = 0 | 10023, 10047 |
| 13 | `FchServDesde` | string 0..1 | N | String(8) | Obligatorio para Concepto 2 o 3 | 10031, 10032, 10049 |
| 14 | `FchServHasta` | string 0..1 | N | String(8) | Obligatorio para Concepto 2 o 3; ≥ `FchServDesde` | 10032, 10033, 10049 |
| 15 | `FchVtoPago` | string 0..1 | N | String(8) | Obligatorio para Concepto 2 o 3 y para facturas FCE (201/206/211); ≥ `CbteFch` | 10035, 10036, 10049, 10163, 10164, 10175 |
| 16 | `MonId` | string 0..1 | S | String(3) | Código de moneda (§7.5) | 10037 |
| 17 | `MonCotiz` | double 0..1 | N | Double(4+6) | Cotización; `PES` → 1; si se informa no puede quedar vacío; obligatorio salvo `CanMisMonExt=S` | 10038, 10039, 10119, 10240 |
| 18 | `CanMisMonExt` | string 0..1 | N | String(1) `S`/`N` | "Marca que identifica si el comprobante se cancela en misma moneda del comprobante (moneda extranjera)". Con `PES`: ausente o `N`. Agregado en v4.0 | 10239, 10241 |
| 19 | `CondicionIVAReceptorId` | int 0..1 | N (obligatorio por RG 5616; ver §4.6) | Int(2) | Condición frente al IVA del receptor (§7.8). "Si el valor informado no es valido, para CAE rechazará y en CAEA observará. Si el valor no existe rechazará en ambos casos." | 10242, 10243, 10245, 10246, 10272 |
| 20 | `CbtesAsoc` | ArrayOfCbteAsoc 0..1 | N | | Comprobantes asociados | 10040, 10041, 10057–10060, 10062, 10120–10122, 10151, 10153–10160, 10197, 10210–10213, 10232, 10237 |
| 21 | `Tributos` | ArrayOfTributo 0..1 | N | | Otros tributos | 10024–10029, 10042, 10067, 10283 |
| 22 | `Iva` | ArrayOfAlicIva 0..1 | N | | Alícuotas; prohibido en clase C y bienes usados monotributista | 10018–10023, 10051, 10061, 10070, 10071, 10075 |
| 23 | `Opcionales` | ArrayOfOpcional 0..1 | N | | "Adicionales por R.G." | 10052–10055, 10064, 10066, 10068, 10076–10099, 10110–10132, 10162–10174, 10189, 10190, 10214–10216, 10273–10281 |
| 24 | `Compradores` | ArrayOfComprador 0..1 | N | | Múltiples compradores (RG 4109) | 10133–10150, 10194 |
| 25 | `PeriodoAsoc` | Periodo 0..1 | N | | Período de comprobantes asociados (RG 4540) | 10196–10199, 10203–10209 |
| 26 | `Actividades` | ArrayOfActividad 0..1 | N | | Actividades del comprobante (RG 5259/5264) | 10218–10231 |

**Discrepancia de orden:** el manual muestra `ImpTrib` antes que `ImpIVA` en FECAESolicitar (pág. PDF 26) pero
`ImpIVA` antes que `ImpTrib` en FECAEARegInformativo (pág. PDF 131-132); el WSDL (que manda) pone `ImpTrib` antes que
`ImpIVA` en ambos. El servicio acepta cualquier orden (§1.2).

Subestructuras [WSDL] [MAN págs. PDF 31-36]:

**`CbteAsoc`** (en `CbtesAsoc`, 0..n):

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `Tipo` | int 1..1 | S | Int(3) | Tipo de comprobante asociado (§7.1 y remitos 88, 91, 988, 990, 991, 993–997) |
| `PtoVta` | int 1..1 | S | Int(5) | > 0 y < 99999 (10058) |
| `Nro` | long 1..1 | S | Long(8) | > 0 y < 99999999 (10059) |
| `Cuit` | string 0..1 | N | String(11) | CUIT emisor del asociado; si se informa, 11 dígitos; obligatorio en ND/NC FCE (10151) |
| `CbteFch` | string 0..1 | N | String(8) | Fecha del asociado; obligatoria en ND/NC FCE (10158) y si el asociado es de Controlador Fiscal o FactuWeb (10211) |

**`Tributo`** (en `Tributos`, 0..n):

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `Id` | short 1..1 | S | Int(2) | §7.6 |
| `Desc` | string 0..1 | N | String(80) | Obligatoria si `Id` = 99, y siempre en FCE (10042) |
| `BaseImp` | double 1..1 | S | Double(13+2) | ≥ 0 |
| `Alic` | double 1..1 | S | Double(3+2) | ≥ 0 |
| `Importe` | double 1..1 | S | Double(13+2) | ≥ 0 |

**`AlicIva`** (en `Iva`, 0..n):

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `Id` | int 1..1 | S | Int(2) | §7.4; no repetir (totalizar por alícuota, 10022) |
| `BaseImp` | double 1..1 | S | Double(13+2) | > 0 (salvo tipos 2, 3, 7, 8, 52, 53: puede ser 0 o no informarse) |
| `Importe` | double 1..1 | S | Double(13+2) | ≥ 0 |

**`Opcional`** (en `Opcionales`, 0..n): `Id` string 0..1 (Man.: String(4), S), `Valor` string 0..1 (Man.: String(250), S).
**`Comprador`** (en `Compradores`, 0..n): `DocTipo` int 1..1 (Int(2)), `DocNro` **long** 1..1 (el manual dice String(80);
10139 exige numérico de 11), `Porcentaje` double 1..1 (Double(2+2)).
**`Periodo`** (`PeriodoAsoc`): `FchDesde` string 0..1, `FchHasta` string 0..1 (ambos `yyyymmdd`, obligatorios si se envía la
estructura).
**`Actividad`** (en `Actividades`, 0..n): `Id` long 1..1 (Long(6)), de `FEParamGetActividades`.

Ejemplos de `Opcionales` del manual (págs. PDF 32-35): `{2, "12345678"}` (Promoción Industrial); `{10,"1"},{1011,"80"},{1012,"30000000007"}`
(RG 3368); `{11,"1"}` (RG 2820); `{12,"1"}` (RG 3687); `{13,"1"}`, `{14,"1"}`, `{15,"1"}` (RG 2863); `{17,"2"}` o `{17,"1"}`
(RG 4004-E); `{1801,"30000000007"},{1802,"DENOMINACION EJEMPLO"}` (RG 4004-E, cotitulares).

### 3.4 Respuesta

`FECAESolicitarResult` (`FECAEResponse`), en este orden [WSDL]: `FeCabResp` 0..1, `FeDetResp` 0..1, `Events` 0..1,
`Errors` 0..1.

### 3.5 FeCabResp (`FECAECabResponse` → `FECabResponse`)

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `Cuit` | long 1..1 | S | Long(11) | CUIT del emisor |
| `PtoVta` | int 1..1 | S | Int(5) | |
| `CbteTipo` | int 1..1 | S | Int(3) | |
| `FchProceso` | string 0..1 | S | String(14) | `yyyymmddhhmiss` (real: `20210701172101`) [CAS]. Los ejemplos viejos del manual muestran 8 dígitos |
| `CantReg` | int 1..1 | S | Int(4) | Eco del request |
| `Resultado` | string 0..1 | N | String(1) | `A`, `R`, `P` |
| `Reproceso` | string 0..1 | — | String | "Campo no operativo para esta versión" [MAN pág. PDF 38]. Siempre `N` en todos los ejemplos y respuestas reales |

### 3.6 FeDetResp / FECAEDetResponse (`FEDetResponse` + `CAE`, `CAEFchVto`)

Orden real [WSDL] [CAS]: `Concepto`, `DocTipo`, `DocNro`, `CbteDesde`, `CbteHasta`, `CbteFch`, `Resultado`,
`Observaciones`, `CAE`, `CAEFchVto`.

| Campo | XSD | Man. | Long. | Descripción |
|---|---|---|---|---|
| `Concepto` | int 1..1 | S | Int(2) | Eco |
| `DocTipo` | int 1..1 | S | Int(2) | Eco |
| `DocNro` | long 1..1 | S | Long(11) | Eco |
| `CbteDesde` | long 1..1 | S | Long(8) | Eco |
| `CbteHasta` | long 1..1 | S | Long(8) | Eco |
| `CbteFch` | string 0..1 | N | String(8) | Fecha del comprobante (la asignada si no se envió) |
| `Resultado` | string 0..1 | S | String(1) | `A` / `R` |
| `Observaciones` | ArrayOfObs 0..1 | N | | `<Observaciones><Obs><Code/><Msg/></Obs></Observaciones>` |
| `CAE` | string 0..1 | N | String(14) | Vacío (`<CAE />`) si rechazado |
| `CAEFchVto` | string 0..1 | N | String(8) | `yyyymmdd`; vacío si rechazado |

Errata del manual: la estructura genérica de la pág. PDF 37 anida al revés (`<Obs><Observaciones>`); el WSDL y las
respuestas reales usan `<Observaciones><Obs>`. El manual también muestra la respuesta como SOAP 1.2 y sin namespace en
`FECAESolicitarResponse`; la real lleva `xmlns="http://ar.gov.afip.dif.FEV1/"`.

### 3.7 Ejemplos

**Ejemplo 1 — Factura A aprobada** (manual, págs. PDF 77-80; dos alícuotas y un tributo 99):

```xml
<ar:FECAESolicitar>
  <ar:Auth><ar:Token>PD94…..</ar:Token><ar:Sign>tYft0….....</ar:Sign><ar:Cuit>33693450239</ar:Cuit></ar:Auth>
  <ar:FeCAEReq>
    <ar:FeCabReq><ar:CantReg>1</ar:CantReg><ar:PtoVta>12</ar:PtoVta><ar:CbteTipo>1</ar:CbteTipo></ar:FeCabReq>
    <ar:FeDetReq>
      <ar:FECAEDetRequest>
        <ar:Concepto>1</ar:Concepto>
        <ar:DocTipo>80</ar:DocTipo>
        <ar:DocNro>20111111112</ar:DocNro>
        <ar:CbteDesde>1</ar:CbteDesde>
        <ar:CbteHasta>1</ar:CbteHasta>
        <ar:CbteFch>20100903</ar:CbteFch>
        <ar:ImpTotal>184.05</ar:ImpTotal>
        <ar:ImpTotConc>0</ar:ImpTotConc>
        <ar:ImpNeto>150</ar:ImpNeto>
        <ar:ImpOpEx>0</ar:ImpOpEx>
        <ar:ImpTrib>7.8</ar:ImpTrib>
        <ar:ImpIVA>26.25</ar:ImpIVA>
        <ar:FchServDesde></ar:FchServDesde>
        <ar:FchServHasta></ar:FchServHasta>
        <ar:FchVtoPago></ar:FchVtoPago>
        <ar:MonId>PES</ar:MonId>
        <ar:MonCotiz>1</ar:MonCotiz>
        <ar:CondicionIVAReceptorId>1</ar:CondicionIVAReceptorId>
        <ar:Tributos>
          <ar:Tributo><ar:Id>99</ar:Id><ar:Desc>Impuesto Municipal Matanza</ar:Desc><ar:BaseImp>150</ar:BaseImp><ar:Alic>5.2</ar:Alic><ar:Importe>7.8</ar:Importe></ar:Tributo>
        </ar:Tributos>
        <ar:Iva>
          <ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>100</ar:BaseImp><ar:Importe>21</ar:Importe></ar:AlicIva>
          <ar:AlicIva><ar:Id>4</ar:Id><ar:BaseImp>50</ar:BaseImp><ar:Importe>5.25</ar:Importe></ar:AlicIva>
        </ar:Iva>
      </ar:FECAEDetRequest>
    </ar:FeDetReq>
  </ar:FeCAEReq>
</ar:FECAESolicitar>
```

Respuesta del manual (pág. PDF 79): `Resultado` `A`, `CAE` `41124578989845`, `CAEFchVto` `20100913`, `FchProceso`
`20100902` (formato viejo). Aritmética: 0 + 150 + 0 + 7.8 + 26.25 = 184.05.

**Ejemplo 2 — respuesta real aprobada con observación** [CAS `test_autorizar_comprobante.yaml`, 2021-07-01]
(request: Factura A, Concepto 3, `CbteDesde` 1836, `ImpTotal` 122.00 = 100 neto + 21 IVA + 1 tributo; receptor
30000000007, monotributista en homologación):

```xml
<FECAESolicitarResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAESolicitarResult><FeCabResp><Cuit>20267565393</Cuit><PtoVta>4000</PtoVta><CbteTipo>1</CbteTipo><FchProceso>20210701172101</FchProceso><CantReg>1</CantReg><Resultado>A</Resultado><Reproceso>N</Reproceso></FeCabResp><FeDetResp><FECAEDetResponse><Concepto>3</Concepto><DocTipo>80</DocTipo><DocNro>30000000007</DocNro><CbteDesde>1836</CbteDesde><CbteHasta>1836</CbteHasta><CbteFch>20210701</CbteFch><Resultado>A</Resultado><Observaciones><Obs><Code>10217</Code><Msg>El credito fiscal discriminado en el presente comprobante solo podra ser computado a efectos del Procedimiento permanente de transicion al Regimen General.</Msg></Obs></Observaciones><CAE>71263951827464</CAE><CAEFchVto>20210711</CAEFchVto></FECAEDetResponse></FeDetResp></FECAESolicitarResult></FECAESolicitarResponse>
```

**Ejemplo 3 — respuesta real rechazada por numeración** [CAS `test_reproceso_productos.yaml`]: el mismo request
reenviado tras haber sido aprobado:

```xml
<FECAESolicitarResponse xmlns="http://ar.gov.afip.dif.FEV1/"><FECAESolicitarResult><FeCabResp><Cuit>20267565393</Cuit><PtoVta>4000</PtoVta><CbteTipo>1</CbteTipo><FchProceso>20210701172115</FchProceso><CantReg>1</CantReg><Resultado>R</Resultado><Reproceso>N</Reproceso></FeCabResp><FeDetResp><FECAEDetResponse><Concepto>1</Concepto><DocTipo>80</DocTipo><DocNro>30000000007</DocNro><CbteDesde>1839</CbteDesde><CbteHasta>1839</CbteHasta><CbteFch>20210701</CbteFch><Resultado>R</Resultado><CAE /><CAEFchVto /></FECAEDetResponse></FeDetResp><Errors><Err><Code>10016</Code><Msg>El numero o fecha del comprobante no se corresponde con el proximo a autorizar. Consultar metodo FECompUltimoAutorizado.</Msg></Err></Errors></FECAESolicitarResult></FECAESolicitarResponse>
```

**Ejemplo 4 — rechazo por fecha, en `Obs`** [CAS `test_main_prueba_usados.yaml`, enviado 2021-07-22 con `CbteFch`
20210701, Concepto 1, `CbteTipo` 49]:

```xml
<FECAEDetResponse><Concepto>1</Concepto><DocTipo>30</DocTipo><DocNro>30500010912</DocNro><CbteDesde>1</CbteDesde><CbteHasta>1</CbteHasta><CbteFch>20210701</CbteFch><Resultado>R</Resultado><Observaciones><Obs><Code>10016</Code><Msg>Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N+5 siendo N la fecha de envio del pedido  de autorizacion para 1 - Productos</Msg></Obs></Observaciones><CAE /><CAEFchVto /></FECAEDetResponse>
```
(con `FeCabResp/Resultado` = `R` y sin `Errors`).

**Ejemplo 5 — rechazo de cabecera** (manual, págs. PDF 83-84): `CantReg` = 2 con un solo detalle y punto de venta no
habilitado → `FeCabResp` con `Resultado` `R` y `Errors` 10002 y "1005" (sic, ver [wsfev1-codigos.md §5.2](wsfev1-codigos.md));
el ejemplo no incluye `FeDetResp`.

**Ejemplo 6 — Factura C de monotributista (construido para este documento, no es respuesta real):**

```xml
<ar:FECAESolicitar>
  <ar:Auth><ar:Token>…</ar:Token><ar:Sign>…</ar:Sign><ar:Cuit>20111111112</ar:Cuit></ar:Auth>
  <ar:FeCAEReq>
    <ar:FeCabReq><ar:CantReg>1</ar:CantReg><ar:PtoVta>1</ar:PtoVta><ar:CbteTipo>11</ar:CbteTipo></ar:FeCabReq>
    <ar:FeDetReq>
      <ar:FECAEDetRequest>
        <ar:Concepto>2</ar:Concepto>
        <ar:DocTipo>99</ar:DocTipo>
        <ar:DocNro>0</ar:DocNro>
        <ar:CbteDesde>49</ar:CbteDesde>
        <ar:CbteHasta>49</ar:CbteHasta>
        <ar:CbteFch>20261001</ar:CbteFch>
        <ar:ImpTotal>1000</ar:ImpTotal>
        <ar:ImpTotConc>0</ar:ImpTotConc>
        <ar:ImpNeto>1000</ar:ImpNeto>
        <ar:ImpOpEx>0</ar:ImpOpEx>
        <ar:ImpTrib>0</ar:ImpTrib>
        <ar:ImpIVA>0</ar:ImpIVA>
        <ar:FchServDesde>20260901</ar:FchServDesde>
        <ar:FchServHasta>20260930</ar:FchServHasta>
        <ar:FchVtoPago>20261010</ar:FchVtoPago>
        <ar:MonId>PES</ar:MonId>
        <ar:MonCotiz>1</ar:MonCotiz>
        <ar:CondicionIVAReceptorId>5</ar:CondicionIVAReceptorId>
      </ar:FECAEDetRequest>
    </ar:FeDetReq>
  </ar:FeCAEReq>
</ar:FECAESolicitar>
```
Reglas que lo hacen válido: clase C sin `Iva` (10071), `ImpTotConc`/`ImpOpEx`/`ImpIVA` = 0 (10043/10044/10047), `ImpNeto`
= subtotal y `ImpTotal` = ImpNeto + ImpTrib (10045/10048), `CbteHasta` = `CbteDesde` (10011), Concepto 2 con las tres
fechas (10049), `CondicionIVAReceptorId` 5 válido para clase C (§7.8).

---

## 4. Reglas de validación por tema

Resumen operativo. El texto literal de cada código y la lista completa están en [wsfev1-codigos.md](wsfev1-codigos.md).
"Excl." = excluyente (rechaza), "Obs." = no excluyente (aprueba con observación).

### 4.1 Orden de evaluación y alcance

[MAN págs. PDF 39-74] agrupa los controles en: `<Auth>` (10000) → `<FeCabReq>` (10001–10007) → `<FeDetReq>` (resto).
- Fallas de `Auth`/cabecera → `Errors`, `Resultado=R` para todo el request [MAN "Operatoria ante errores", pág. PDF 76-77].
- Dentro de un lote, los comprobantes se procesan en orden; "ante una inconsistencia los comprobantes subsiguientes
  también se rechazaran" (ejemplo del manual: 100 facturas 51..150, se aprueban 51..100, 101 rechazada, 102..150 "no
  procesado") [MAN pág. PDF 77]. Real [CAS `test_main_prueba_multiple.yaml`]: 250 notas de crédito A con el primer número
  inválido → 250 `FECAEDetResponse` con `Resultado=R`, `<CAE />`, sin `Obs`, un único `Err 10016` y cabecera `R`.
- Si hay aprobados y rechazados → cabecera `P`.
- El orden interno de evaluación de las validaciones de detalle y si se acumulan varias `Obs` excluyentes en un mismo
  comprobante: el ejemplo 2 del manual muestra dos `Obs` (10030 y 10016) en el mismo comprobante rechazado; **[NO VERIFICADO]**
  el orden real.

### 4.2 Fechas

| Regla | Código | Fuente |
|---|---|---|
| `CbteFch` Concepto 1: nula o en [N-5, N+5] (N = fecha de envío); no puede exceder el mes de presentación | 10016 (Obs.), 10152 | [MAN pág. PDF 43, 61] |
| `CbteFch` Concepto 2 o 3: nula o en [N-10, N+10] | 10016 | [MAN pág. PDF 43] |
| `CbteFch` ≥ fecha del último comprobante del mismo tipo y punto de venta | 10016 | [MAN pág. PDF 43] |
| FCE: [N-5, N+1]; ND/NC FCE hasta N-5 (mensaje real: "rango N-5 y N") | 10016 | [MAN pág. PDF 43]; [CAS] |
| Si `CbteFch` > N (Concepto 1 o FCE), mismo mes que N | 10152 | [MAN pág. PDF 61] |
| Sin `CbteFch` → se asigna la fecha de proceso | — | [MAN pág. PDF 28] |
| `FchServDesde`, `FchServHasta`, `FchVtoPago` obligatorios si Concepto 2 o 3, formato `yyyymmdd` | 10049 | [MAN pág. PDF 49] |
| Si viene una de las tres, las otras dos son obligatorias | 10031, 10033, 10035 | [MAN pág. PDF 45-46] |
| `FchServDesde` ≤ `FchServHasta` | 10032 | [MAN pág. PDF 45] |
| `FchVtoPago` ≥ `CbteFch` | 10036 | [MAN pág. PDF 46] |
| FCE: `FchVtoPago` ≥ max(`CbteFch`, fecha actual); obligatoria en facturas 201/206/211; en ND/NC sólo si es de anulación | 10163, 10164, 10175 | [MAN págs. PDF 63-65] |
| `PeriodoAsoc`: fechas válidas, > 01/01/2006, Hasta ≥ Desde, Hasta ≤ `CbteFch` | 10204–10208 | [MAN pág. PDF 68] |
| Comprobante asociado electrónico con fecha posterior al nuevo: mismo mes/año | 10210 | [MAN pág. PDF 68] |
| `CbtesAsoc/CbteFch` `yyyymmdd`, no posterior a hoy si es de Controlador Fiscal/FactuWeb | 10212, 10213 | [MAN pág. PDF 69] |

Zona horaria para "N": el servicio trabaja en hora de Argentina (`-03:00`, ver `FEHeaderInfo.fecha`) [INFERIDO].

### 4.3 Importes y aritmética

| Regla | Código | Fuente |
|---|---|---|
| Todos los importes ≥ 0 | 10043–10047, 10065 | [MAN págs. PDF 47-50] |
| Precisión: 13 enteros + 2 decimales (`MonCotiz` 4+6, `Alic` de tributo 3+2, `Porcentaje` 2+2) | 10056 | [MAN pág. PDF 50] |
| `ImpTotal = ImpTotConc + ImpNeto + ImpOpEx + ImpTrib + ImpIVA` | 10048 (Excl.) | [MAN pág. PDF 48] |
| Clase C: `ImpTotal = ImpNeto + ImpTrib` | 10048 | [MAN pág. PDF 48-49] |
| Bienes usados (49) con emisor monotributista: `ImpTotal = ImpTotConc + ImpTrib` | 10048 | [MAN pág. PDF 49] |
| `ImpIVA = Σ AlicIva/Importe` | 10023 | [MAN pág. PDF 44] |
| `ImpTrib = Σ Tributo/Importe` | 10029 | [MAN pág. PDF 45] |
| `ImpNeto = Σ AlicIva/BaseImp` (no aplica a 2, 3, 7, 8, clase C, 52, 53) | 10061 | [MAN pág. PDF 50] |
| `AlicIva/Importe` coherente con `BaseImp` × alícuota de `Id` (no aplica a 2, 3, 7, 8, 52, 53 ni clase C) | 10051 | [MAN pág. PDF 49] |
| Margen de error: error relativo ≤ 0,01 % **o** error absoluto ≤ 0,01 (× cantidad de alícuotas o tributos en las sumas) | 10023, 10029, 10048, 10051, 10061 | [MAN] |
| Error absoluto = \|calculado − real\|; relativo = absoluto / \|real\|; redondeo **Round Half Even** | — | [MAN "Margen de error", pág. PDF 202] |

`Iva` / `AlicIva`:
- `ImpIVA` > 0 → `Iva` obligatorio (10018); `ImpNeto` > 0 → `Iva` obligatorio (10070).
- `ImpIVA` = 0 → `Iva` sólo puede informarse con alícuota `Id` 3 (0 %) (10018: "solo deben informarse con ImpIVA = 3 (iva 0)").
- `Iva` presente → al menos un `AlicIva` (10018); `Id` obligatorio salvo en 2, 3, 7, 8, 52, 53 (10019); sin `Id` repetidos (10022).
- Clase C: no informar `Iva` (10071). Bienes usados monotributista: no informar `Iva` (10075).

`Tributos`: `ImpTrib` > 0 → obligatorio; `ImpTrib` = 0 → **no** enviar (10024); `Id` de §7.6 (10025); `Desc` si `Id` = 99 o
FCE (10042).

### 4.4 Reglas por clase de comprobante

| Regla | A | B | C | A c/leyenda (51–54) | 49 Bienes usados | FCE (201–213) | Código |
|---|---|---|---|---|---|---|---|
| `CbteDesde` = `CbteHasta` | sí | no (lote permitido bajo umbral) | sí | sí | sí | sí | 10011, 10012 |
| `DocTipo` = 80 | sí | — | — | sí | ≠ 99 | sí (80) | 10013, 10015 |
| Receptor en padrón, activo | Obs. 10017 | si 80/86/87, padrón (salvo 23000000000) | — | Obs. 10017 | si 80/86/87 | Excl. 10176 | |
| Receptor activo en IVA o Monotributo | Obs. 10063 | — | — | Obs. 10063 | — | A: IVA; B/C: IVA, Monotributo o Exento (10177) | |
| Receptor monotributista → leyenda de crédito fiscal | Obs. 10217 | — | — | Obs. 10217 | — | — | |
| `Iva` | obligatorio si hay neto | ídem | prohibido | ídem (ver 10061) | prohibido si monotributista | ídem A/B/C | 10070, 10071, 10075 |
| `ImpTotConc`, `ImpOpEx`, `ImpIVA` | libres | libres | = 0 | libres | monotributista: ver §4.3 | | 10043, 10044, 10047 |
| `Opcionales` permitidos (tipos) | 1, 2, 3, 4, 63 | 6, 7, 8, 9, 64 | 11, 12, 13, 15 | 51–54 | 49 (obligatorio: 91 y 93; 92 si DocTipo 30/91/94) | 201–213 (obligatorio) | 10068, 10076–10084, 10162 |
| Cantidad por request | ≤ RegXReq | ≤ RegXReq | ≤ RegXReq | ≤ RegXReq | ≤ RegXReq | 1 | 10003 |

Tipos por clase según 10007 [MAN pág. PDF 40]: **A** 1, 2, 3, 4, 5, 34, 39, 60, 63, 201, 202, 203; **B** 6, 7, 8, 9, 10, 35, 40,
64, 61, 206, 207, 208; **C** 11, 12, 13, 15, 211, 212, 213; **"A con leyenda operación sujeta a retención"** 51, 52, 53, 54;
**Bienes Usados** 49. Los 51–54 eran "M" hasta v4.1 ("Adecuaciones para el reemplazo de comprobantes clase M", 01/12/2025)
[MAN historial pág. PDF 14].

Comprobantes asociados permitidos (10040) [MAN págs. PDF 46-47]: `CbtesAsoc` sólo para `CbteTipo` 1, 2, 3, 6, 7, 8, 12, 13,
51, 52, 53, 201, 206, 211 (y 202/203/207/208/212/213 por las reglas FCE). Para 2/3 → 1, 2, 3, 4, 5, 34, 39, 60, 63, 88, 991;
7/8 → 6, 7, 8, 9, 10, 35, 40, 61, 64, 88, 991; 12/13 → 11, 12, 13, 15; 52/53 → 51, 52, 53, 54, 88, 991; 1/6/51 → 88, 991;
201/206/211 → 91, 990, 991, 993, 994, 995; 202/203 → 201, 202, 203; 207/208 → 206, 207, 208; 212/213 → 211, 212, 213.
Nota de débito/crédito sin asociados ni `PeriodoAsoc` → 10197; factura con `PeriodoAsoc` → 10198.

### 4.5 Receptor, consumidor final y umbral RG 4444

[MAN 10014, 10015, págs. PDF 41-42]:
- Clase B en **lote** (`CbteDesde` ≠ `CbteHasta`): `ImpTotal / (CbteHasta − CbteDesde + 1)` < "monto en pesos resultante
  según RG4444" (10014) y `DocTipo` = 99 con `DocNro` = 0 (10015).
- Clase B **individual** bajo el umbral: si `DocTipo` = 99 → `DocNro` = 0; si 80/86/87 → debe existir en el padrón
  (23000000000 "No Categorizado" exento de este control); otro `DocTipo` → de `FEParamGetTiposDoc` y con `DocNro`.
- Clase B individual **sobre** el umbral: `DocTipo` ≠ 99 y `DocNro` informado.
- Bienes usados (49): `DocTipo` ≠ 99, con `DocNro`; si 80/86/87, en padrón.
- La comparación usa `ImpTotal × MonCotiz` (en `PES` la cotización es 1) [MAN historial v2.11 y v2.16].
- **El manual no publica el monto.** El historial cita "$10000" (2018-2019). Según prensa, la RG 5700/2025 (29/05/2025) lo
  llevó a **$10.000.000** ([iProfesional](https://www.iprofesional.com/impuestos/429313-arca-elevo-a-10-millones-el-monto-para-identificar-en-las-facturas-a-consumidores-finales),
  [Blog del Contador](https://blogdelcontador.com.ar/news-45898-arca-eleva-a-10-millones-el-limite-para-identificar-al-consumidor-final-en-comprobantes)).
  **[NO VERIFICADO en Boletín Oficial ni en el servicio]** → en ArcaSim debe ser configurable.
- "No Categorizado" (`DocTipo` 80, `DocNro` 23000000000) en 6/7/8 con ImpNeto + ImpIVA > 0: exige tributo `Id` 13 >
  0 (10067 si `ImpTrib` = 0, excluyente; 10283 si `ImpTrib` > 0, observación). FCE no admite 23000000000 (10178).
- `DocNro` ≠ CUIT emisora (10069). `DocTipo` 31 (Fondo Común de Inversiones CNV) sólo entidades financieras, `DocNro` de
  hasta 4 dígitos, `CondicionIVAReceptorId` 15 (10270–10272).

### 4.6 Condición frente al IVA del receptor (RG 5616)

- Agregado en v4.0 (17/03/2025). "A partir del 6 de abril de 2025 podrá enviarse de forma opcional [...] hasta tanto entre
  en vigencia su obligatoriedad reglamentada por la Resolución General N°5616, en cuyo momento pasará a rechazar la emisión
  de comprobantes sin este dato." [MAN historial v4.0, pág. PDF 13].
- v4.7 (producción hoy) contiene **las dos** validaciones: 10245 (Obs.) "resultará obligatorio" y 10246 (Excl.) "es
  obligatorio". v4.8 (homologación, revisión 01/12/2026): "Será obligatorio el campo [...]. Por tal motivo los códigos de
  error 10245 para CAE y 825 para CAEA quedaran en desuso." [MAN48 historial].
- Interpretación para ArcaSim **[INFERIDO]**: un flag `condicionIvaObligatoria`: `false` → falta del campo = Obs. 10245
  (aprobado); `true` → Excl. 10246. Qué devuelve hoy cada ambiente: **[NO VERIFICADO]**.
- Valor inexistente → 10242 (Excl.); valor no válido para la clase → 10243 en CAE (Excl.) y 824 en CAEA (Obs.).
- Compatibilidad clase/condición: §7.8.

### 4.7 Moneda y cotización

| Regla | Código |
|---|---|
| `MonId` obligatorio, de `FEParamGetTiposMonedas` | 10037 |
| `MonId` = `PES` → `MonCotiz` obligatorio e igual a 1 | 10039 |
| `MonCotiz` obligatorio salvo `CanMisMonExt` = S; si se informa, > 0 | 10038 |
| Si `CanMisMonExt` = S, `MonCotiz` debe coincidir exactamente con la cotización ARCA del día hábil anterior a `CbteFch` (si `CbteFch` < hoy) o del día hábil anterior a hoy (si `CbteFch` > hoy); o se omite | 10038 |
| Moneda ≠ PES: `MonCotiz` entre 2 % y 400 % de la cotización orientativa de ARCA (historial: 20 %–200 % en v3.1, 400 % en v3.2/v3.3) | 10119 |
| "Si informa el campo MonCotiz, el mismo no podra superar en 1 a la cotizacion oficial" | 10240 (Excl. en CAE; 821 Obs. en CAEA) |
| `CanMisMonExt`: S o N, no vacío; con PES ausente o N | 10239, 10241 |

La interpretación exacta de 10240 ("no podrá superar en 1") es ambigua: **[NO VERIFICADO]** si es diferencia absoluta de 1
unidad, 1 % u otra cosa.

### 4.8 Validaciones que dependen de padrones de ARCA

Imposibles de reproducir fielmente sin los datos de ARCA (ver §8.3): 10000 (situación registral del emisor: 11 mensajes),
10005 (punto de venta RECE), 10015/10017/10063/10176/10177/10180 (receptor en padrón / impuestos / categoría PyME),
10096 (exento), 10115/10126 (CUIT de opcionales en padrón), 10120–10122/10232 (remitos registrados y confirmados),
10148/10149 (compradores), 10160 (asociado FCE existe), 10161 (domicilio fiscal electrónico), 10174 (CBU registrado),
10184 (saldo de cuenta corriente FCE), 10188/10192 (obligación de FCE según categorización y monto), 10195 (receptor
apócrifo), 10220/10223 (actividades del emisor), 10234–10236 (alertas de habilitación clase A y topes de monotributo),
10237 (NC mayor que el asociado), 10238/10247–10249 (CUIT receptora inexistente, inactiva, no confiable, fallecido),
10273–10282 (nómina de Seguros de Caución).

---

## 5. Numeración

### 5.1 Correlatividad

- La numeración es correlativa por **CUIT emisora + punto de venta + tipo de comprobante**: "El número de comprobante
  informado <CbteDesde> debe ser mayor en 1 al último informado para igual punto de venta y tipo de comprobante. Consultar
  método FECompUltimoAutorizado" (10016) [MAN pág. PDF 42-43]. Ídem CAEA (703) [MAN pág. PDF 142].
- La fecha también es monotónica: `CbteFch` ≥ fecha del último comprobante del mismo tipo y punto de venta (10016; CAEA 704).
- `FECompUltimoAutorizado(PtoVta, CbteTipo)` devuelve el último número; el próximo es `CbteNro + 1`; si no hay ninguno,
  `CbteNro` = 0 [CAS].
- En un lote clase B con `CbteDesde` < `CbteHasta`, el rango consume todos los números; el siguiente comprobante debe
  empezar en `CbteHasta + 1` [INFERIDO de 10016 y del ejemplo de lote del manual].
- En un lote de varios `FECAEDetRequest`, cada uno debe continuar la numeración del anterior (ejemplo del manual 51..150 y
  [CAS] 1008..1257).
- Los puntos de venta CAE y CAEA son distintos (tipo de punto de venta distinto: RECE vs CAEA/contingencia), por lo que
  cada uno lleva su propia secuencia [INFERIDO de 10005, 701, 1444].

### 5.2 Sin idempotencia

"si el cliente envía la misma nueva solicitud de CAE para la misma factura, WsfeV1 devolvería un error de consecutividad
puesto que en la base de datos de arca esa factura ya figura como emitida" [MAN pág. PDF 77]. Confirmado: reenviar un
request aprobado devuelve `Err 10016` [CAS `test_reproceso_*.yaml`]. "Este webservice no tiene ID secuencial ni reproceso"
(documentación de PyAfipWs, secundaria). `Reproceso` siempre `N`.

### 5.3 Límite por request

`FECompTotXRequest.RegXReq` (250 en homologación 2021 [CAS]); `CantReg` 1..9998 (10001); FCE 1 por request (10003).

### 5.4 Recuperación ante timeout

Procedimiento oficial [MAN "Operatoria con errores de comunicación", pág. PDF 77]: ante timeout, consultar
`FECompConsultar(CbteTipo, PtoVta, CbteNro)`; si existe, devuelve todo lo enviado + CAE y vencimiento; si no, reenviar.
También `FECompUltimoAutorizado`. ArcaSim debería poder simular "CAE otorgado pero respuesta perdida" (registrar el
comprobante y cortar la conexión) para que los clientes ejerciten este camino.

---

## 6. CAE y CAEA

### 6.1 Formato del CAE

- `CAE`: String(14) [MAN pág. PDF 38]. Todos los ejemplos son 14 dígitos numéricos: `41124578989845`, `63288001286615`
  [MAN]; `71263951827464`, `71263951828643` [CAS 2021]; `69148738398949` [AFIPJS 2019].
- Estructura interna del número (si codifica fecha, punto de venta, dígito verificador): **[NO VERIFICADO]**; el manual no
  la describe. ArcaSim puede generar 14 dígitos únicos.
- CAEA: "Obligatorio, numérico de 14 posiciones" (782) [MAN pág. PDF 144].

### 6.2 CAEFchVto

- El manual no da una regla escrita ("Fecha de vencimiento o vencimiento de la autorización", pág. PDF 38).
- **[INFERIDO]**: en todos los casos observados `CAEFchVto = CbteFch + 10 días corridos`: manual 20100903→20100913,
  20130720→20130730, 20130715→20130725; [CAS] 20210701→20210711 (Concepto 1 y 3, tipos 1 y 2); [AFIPJS] 20190407→20190417
  (Factura C). En ningún ejemplo `CbteFch` ≠ fecha de proceso con concepto 2/3 a futuro, por lo que no se pudo distinguir
  "CbteFch + 10" de otras reglas para esos casos.
- `FECompConsultar.FchVto` devuelve el mismo valor; para CAEA devuelve `FchVigHasta` [MAN pág. PDF 193-194].

### 6.3 CAEA: períodos y quincenas

- Dos quincenas por mes: 1 = del 1 al 15, 2 = del 16 al último día [MAN pág. PDF 91].
- Se puede solicitar "dentro de cada quincena y hasta 5 (cinco) días corridos anteriores al comienzo de cada quincena"
  [MAN pág. PDF 91]; fuera de esa ventana → 15006 (mensaje real con las fechas de la ventana en formato M/d/yyyy).
- Un CAEA por CUIT + período + orden (15008).
- `FchVigDesde`/`FchVigHasta` = límites de la quincena (ejemplo: 201011/1 → 20101101–20101115) [MAN pág. PDF 96].
- `FchTopeInf`: en el único ejemplo, 20101215 para la quincena 20101101–20101115 [MAN pág. PDF 96]. Regla general:
  **[NO VERIFICADO]**.
- Obs. 15014: deuda de rendición (2 quincenas consecutivas o 4 alternadas), devuelve las pendientes como
  "periodo;orden;puntoVenta" [MAN pág. PDF 94-95].

### 6.4 CAEA: RG 5782/2025 (v4.6, 01/08/2026)

[MAN historial v4.6, pág. PDF 15; tablas pág. PDF 94-95]:
- "Todos los puntos de venta CAEA pasarán a ser considerados como Contingencia."
- "Los puntos de venta CAEA deberán estar asociados a un domicilio de factura electrónica (CAE o Controlador Fiscal de
  nueva generación)" → 15016 (Excl.: requiere al menos un punto de venta CAE o CF de nueva tecnología activo como modalidad
  principal), 15017 (Obs.: lista puntos CAEA sin domicilio vinculado), 15018 (Obs.: recordatorio de informar fecha y hora
  de generación).
- `CbteFchHsGen` pasa a ser obligatorio (1440).
- 15001 (autoimpresor) queda fuera de vigencia desde 01/06/2026.

### 6.5 Flujo CAEA

1. `FECAEASolicitar(Periodo, Orden)` dentro de la ventana → CAEA, vigencia y tope de información.
2. `FECAEAConsultar(Periodo, Orden)` para recuperarlo (602 si no existe).
3. Durante la vigencia el emisor emite offline (contingencia) usando el CAEA.
4. `FECAEARegInformativo` informa cada comprobante (mismo esquema de lote que CAE; requiere `CAEA` y `CbteFchHsGen`), antes
   de `FchTopeInf` [INFERIDO del nombre del campo; el manual no define la consecuencia de informar tarde].
5. Por cada punto de venta CAEA sin uso: `FECAEASinMovimientoInformar(PtoVta, CAEA)`; la fecha de envío debe ser > inicio
   de vigencia (1203); no debe haber comprobantes informados con ese CAEA/PtoVta (1202); una sola vez (1209). Informar un
   comprobante en un punto de venta ya declarado sin movimiento → Obs. 1424.
6. `FECAEASinMovimientoConsultar(CAEA, PtoVta)` para verificar.

---

## 7. Tablas de parámetros

**Importante:** los métodos `FEParamGet*` requieren `Auth` válido (sin certificado propio sólo se obtiene `Err 600`,
verificado [VIVO] 2026-10-01). Por eso las tablas de abajo combinan:
- respuestas **reales** de homologación grabadas en julio de 2021 [CAS `test_main_parametros.yaml`] (orden, `Desc`,
  `FchDesde` y `FchHasta` literales tal como los devolvió el servicio, incluido el string `NULL`);
- agregados posteriores que surgen del manual v4.7 (marcados);
- las "Tablas del sistema" genéricas de factura electrónica [TAB] para contraste.

Todo lo agregado después de 2021 tiene `Desc`/`FchDesde` **[NO VERIFICADO]**. Antes de congelar los datos de ArcaSim,
conviene volver a capturar estas tablas con un certificado de homologación.

### 7.1 Tipos de comprobante (`FEParamGetTiposCbte`)

Respuesta real 2021 [CAS], en el orden en que la devolvió el servicio:

| `Id` | `Desc` | `FchDesde` | `FchHasta` |
|---|---|---|---|
| 1 | Factura A | 20100917 | NULL |
| 2 | Nota de Débito A | 20100917 | NULL |
| 3 | Nota de Crédito A | 20100917 | NULL |
| 6 | Factura B | 20100917 | NULL |
| 7 | Nota de Débito B | 20100917 | NULL |
| 8 | Nota de Crédito B | 20100917 | NULL |
| 4 | Recibos A | 20100917 | NULL |
| 5 | Notas de Venta al contado A | 20100917 | NULL |
| 9 | Recibos B | 20100917 | NULL |
| 10 | Notas de Venta al contado B | 20100917 | NULL |
| 63 | Liquidacion A | 20100917 | NULL |
| 64 | Liquidacion B | 20100917 | NULL |
| 34 | Cbtes. A del Anexo I, Apartado A,inc.f),R.G.Nro. 1415 | 20100917 | NULL |
| 35 | Cbtes. B del Anexo I,Apartado A,inc. f),R.G. Nro. 1415 | 20100917 | NULL |
| 39 | Otros comprobantes A que cumplan con R.G.Nro. 1415 | 20100917 | NULL |
| 40 | Otros comprobantes B que cumplan con R.G.Nro. 1415 | 20100917 | NULL |
| 60 | Cta de Vta y Liquido prod. A | 20100917 | NULL |
| 61 | Cta de Vta y Liquido prod. B | 20100917 | NULL |
| 11 | Factura C | 20110330 | NULL |
| 12 | Nota de Débito C | 20110330 | NULL |
| 13 | Nota de Crédito C | 20110330 | NULL |
| 15 | Recibo C | 20110330 | NULL |
| 49 | Comprobante de Compra de Bienes Usados a Consumidor Final | 20130401 | NULL |
| 51 | Factura M | 20150522 | NULL |
| 52 | Nota de Débito M | 20150522 | NULL |
| 53 | Nota de Crédito M | 20150522 | NULL |
| 54 | Recibo M | 20150522 | NULL |
| 201 | Factura de Crédito electrónica MiPyMEs (FCE) A | 20181226 | NULL |
| 202 | Nota de Débito electrónica MiPyMEs (FCE) A | 20181226 | NULL |
| 203 | Nota de Crédito electrónica MiPyMEs (FCE) A | 20181226 | NULL |
| 206 | Factura de Crédito electrónica MiPyMEs (FCE) B | 20181226 | NULL |
| 207 | Nota de Débito electrónica MiPyMEs (FCE) B | 20181226 | NULL |
| 208 | Nota de Crédito electrónica MiPyMEs (FCE) B | 20181226 | NULL |
| 211 | Factura de Crédito electrónica MiPyMEs (FCE) C | 20181226 | NULL |
| 212 | Nota de Débito electrónica MiPyMEs (FCE) C | 20181226 | NULL |
| 213 | Nota de Crédito electrónica MiPyMEs (FCE) C | 20181226 | NULL |

Cambios posteriores según el manual:
- 51, 52, 53, 54: desde v4.1 (01/12/2025) son "A con leyenda 'operación sujeta a retención'" (reemplazo de la clase M)
  [MAN historial pág. PDF 14; 10007]. La `Desc` actual que devuelve el servicio: **[NO VERIFICADO]**.
- Los demás códigos de [TAB] (`TABLACOMPROBANTES.xls`) no son autorizables por WSFEv1 (10007) pero pueden aparecer como
  **comprobantes asociados**: 88 "REMITO ELECTRONICO", 91 "REMITOS R", 991 "REMITO ELECTRÓNICO DE TABACO EN HEBRA",
  993 "REMITO ELECTRÓNICO HARINERO - AUTOMOTOR", 994 "REMITO ELECTRÓNICO HARINERO - FERROVIARIO", 995 "REMITO ELECTRÓNICO
  CÁRNICO", 997 "Remito Electrónico para Azúcar, Alcohol y Subproductos -Mercado Interno-" [TAB]. El manual también cita 988,
  990 y 996 como asociables (10120) sin describirlos; no figuran en [TAB] **[NO VERIFICADO]**.

### 7.2 Tipos de documento (`FEParamGetTiposDoc`)

Respuesta real 2021 [CAS]:

| `Id` | `Desc` | `FchDesde` | `FchHasta` |
|---|---|---|---|
| 80 | CUIT | 20080725 | NULL |
| 86 | CUIL | 20080725 | NULL |
| 87 | CDI | 20080725 | NULL |
| 89 | LE | 20080725 | NULL |
| 90 | LC | 20080725 | NULL |
| 91 | CI Extranjera | 20080725 | NULL |
| 92 | en trámite | 20080725 | NULL |
| 93 | Acta Nacimiento | 20080725 | NULL |
| 95 | CI Bs. As. RNP | 20080725 | NULL |
| 96 | DNI | 20080725 | NULL |
| 94 | Pasaporte | 20080725 | NULL |
| 0 | CI Policía Federal | 20080725 | NULL |
| 1 | CI Buenos Aires | 20080725 | NULL |
| 2 | CI Catamarca | 20080725 | NULL |
| 3 | CI Córdoba | 20080725 | NULL |
| 4 | CI Corrientes | 20080728 | NULL |
| 5 | CI Entre Ríos | 20080728 | NULL |
| 6 | CI Jujuy | 20080728 | NULL |
| 7 | CI Mendoza | 20080728 | NULL |
| 8 | CI La Rioja | 20080728 | NULL |
| 9 | CI Salta | 20080728 | NULL |
| 10 | CI San Juan | 20080728 | NULL |
| 11 | CI San Luis | 20080728 | NULL |
| 12 | CI Santa Fe | 20080728 | NULL |
| 13 | CI Santiago del Estero | 20080728 | NULL |
| 14 | CI Tucumán | 20080728 | NULL |
| 16 | CI Chaco | 20080728 | NULL |
| 17 | CI Chubut | 20080728 | NULL |
| 18 | CI Formosa | 20080728 | NULL |
| 19 | CI Misiones | 20080728 | NULL |
| 20 | CI Neuquén | 20080728 | NULL |
| 21 | CI La Pampa | 20080728 | NULL |
| 22 | CI Río Negro | 20080728 | NULL |
| 23 | CI Santa Cruz | 20080728 | NULL |
| 24 | CI Tierra del Fuego | 20080728 | NULL |
| 99 | Doc. (Otro) | 20080728 | NULL |

Agregados / diferencias:
- **31** "Fondo Común de Inversiones CNV" — agregado en v4.5 (02/07/2026), sólo entidades financieras (10270–10272)
  [MAN historial pág. PDF 14]. `Desc`/`FchDesde` exactos **[NO VERIFICADO]**.
- **30** "Certificado de Migración" figura en [TAB] y el manual lo nombra en 10082 (bienes usados: "Si en el campo TipoDoc
  se informa 30, 91 o 94 se deberá informar el id 92"), pero **no** aparecía en la respuesta de 2021. Un request real con
  `DocTipo` 30 y `CbteTipo` 49 fue rechazado por fecha, no por tipo de documento [CAS `test_main_prueba_usados.yaml`], así
  que no se sabe si 30 es aceptado **[NO VERIFICADO]**.
- **99**: en el servicio `Doc. (Otro)`; en [TAB] "Sin identificar/venta global diaria".
- [TAB] incluye 88 "Usado por Anses para Padrón", que no devuelve el servicio.

### 7.3 Conceptos (`FEParamGetTiposConcepto`)

| `Id` | `Desc` | `FchDesde` | `FchHasta` |
|---|---|---|---|
| 1 | Producto | 20100917 | NULL |
| 2 | Servicios | 20100917 | NULL |
| 3 | Productos y Servicios | 20100917 | NULL |

[MAN] los describe como "1 Productos, 2 Servicios, 3 Productos y Servicios" (10030). [TAB] agrega 4 "Otro", que WSFEv1 no
acepta (no figura en la respuesta del servicio).

### 7.4 Alícuotas de IVA (`FEParamGetTiposIva`)

| `Id` | `Desc` | `FchDesde` | `FchHasta` |
|---|---|---|---|
| 3 | 0% | 20090220 | NULL |
| 4 | 10.5% | 20090220 | NULL |
| 5 | 21% | 20090220 | NULL |
| 6 | 27% | 20090220 | NULL |
| 8 | 5% | 20141020 | NULL |
| 9 | 2.5% | 20141020 | NULL |

Coincide con [AFIPJS] (2019) y con [TAB] `OperacionCondicionIVA.xls` (3 = 0 %, 4 = 10,5 %, 5 = 21 %, 6 = 27 %, 8 = 5 %,
9 = 2,5 %; [TAB] además lista 0 "No Corresponde", 1 "No Gravado", 2 "Exento", 7 "Gravado" que no son alícuotas de WSFEv1).
Las alícuotas 8 y 9 vienen de la Ley 26982 (v2.3 del manual). Para 10051 ArcaSim necesita el porcentaje numérico:
3→0, 4→0.105, 5→0.21, 6→0.27, 8→0.05, 9→0.025.

### 7.5 Monedas (`FEParamGetTiposMonedas`)

Respuesta real 2021 [CAS] (50 monedas, en el orden devuelto):

| `Id` | `Desc` | `FchDesde` | `FchHasta` |
|---|---|---|---|
| PES | Pesos Argentinos | 20090403 | NULL |
| DOL | Dólar Estadounidense | 20090403 | NULL |
| 002 | Dólar Libre EEUU | 20090416 | NULL |
| 007 | Florines Holandeses | 20090403 | NULL |
| 010 | Pesos Mejicanos | 20090403 | NULL |
| 011 | Pesos Uruguayos | 20090403 | NULL |
| 014 | Coronas Danesas | 20090403 | NULL |
| 015 | Coronas Noruegas | 20090403 | NULL |
| 016 | Coronas Suecas | 20090403 | NULL |
| 018 | Dólar Canadiense | 20090403 | NULL |
| 019 | Yens | 20090403 | NULL |
| 021 | Libra Esterlina | 20090403 | NULL |
| 023 | Bolívar Venezolano | 20090403 | NULL |
| 024 | Corona Checa | 20090403 | NULL |
| 025 | Dinar Yugoslavo | 20090403 | NULL |
| 026 | Dólar Australiano | 20090403 | NULL |
| 027 | Dracma Griego | 20090403 | NULL |
| 028 | Florín (Antillas Holandesas) | 20090403 | NULL |
| 029 | Güaraní | 20090403 | NULL |
| 031 | Peso Boliviano | 20090403 | NULL |
| 032 | Peso Colombiano | 20090403 | NULL |
| 033 | Peso Chileno | 20090403 | NULL |
| 034 | Rand Sudafricano | 20090403 | NULL |
| 036 | Sucre Ecuatoriano | 20090403 | NULL |
| 051 | Dólar de Hong Kong | 20090403 | NULL |
| 052 | Dólar de Singapur | 20090403 | NULL |
| 053 | Dólar de Jamaica | 20090403 | NULL |
| 054 | Dólar de Taiwan | 20090403 | NULL |
| 055 | Quetzal Guatemalteco | 20090403 | NULL |
| 056 | Forint (Hungría) | 20090403 | NULL |
| 057 | Baht (Tailandia) | 20090403 | NULL |
| 059 | Dinar Kuwaiti | 20090403 | NULL |
| 012 | Real | 20090403 | NULL |
| 030 | Shekel (Israel) | 20090403 | NULL |
| 035 | Nuevo Sol Peruano | 20090403 | NULL |
| 060 | Euro | 20090403 | NULL |
| 040 | Lei Rumano | 20090415 | NULL |
| 042 | Peso Dominicano | 20090415 | NULL |
| 043 | Balboas Panameñas | 20090415 | NULL |
| 044 | Córdoba Nicaragüense | 20090415 | NULL |
| 045 | Dirham Marroquí | 20090415 | NULL |
| 046 | Libra Egipcia | 20090415 | NULL |
| 047 | Riyal Saudita | 20090415 | NULL |
| 061 | Zloty Polaco | 20090415 | NULL |
| 062 | Rupia Hindú | 20090415 | NULL |
| 063 | Lempira Hondureña | 20090415 | NULL |
| 064 | Yuan (Rep. Pop. China) | 20090415 | NULL |
| 009 | Franco Suizo | 20091110 | NULL |
| 041 | Derechos Especiales de Giro | 20100125 | NULL |
| 049 | Gramos de Oro Fino | 20100125 | NULL |

[TAB] `TABLA MONEDAS V.0 25082010.xls` lista además códigos que el servicio no devolvió (000, 003, 004, 005, 006, 008, 013,
017, 022, 048, 050, 058). Altas o bajas posteriores a 2021: **[NO VERIFICADO]**.

### 7.6 Tributos (`FEParamGetTiposTributos`)

| `Id` | `Desc` | `FchDesde` | `FchHasta` |
|---|---|---|---|
| 1 | Impuestos nacionales | 20100917 | NULL |
| 2 | Impuestos provinciales | 20100917 | NULL |
| 3 | Impuestos municipales | 20100917 | NULL |
| 4 | Impuestos Internos | 20100917 | NULL |
| 99 | Otro | 20100917 | NULL |
| 5 | IIBB | 20170719 | NULL |
| 6 | Percepción de IVA | 20170719 | NULL |
| 7 | Percepción de IIBB | 20170719 | NULL |
| 8 | Percepciones por Impuestos Municipales | 20170719 | NULL |
| 9 | Otras Percepciones | 20170719 | NULL |
| 13 | Percepción de IVA a no Categorizado | 20170719 | NULL |

[TAB] `otros_Tributos.xlsx` agrega 10 "Impuesto interno a nivel item" (sólo controladores fiscales) y 14–19 (liquidaciones
pecuarias), no devueltos por WSFEv1. El 13 es el que exigen 10067/10283 para "No Categorizado".

### 7.7 Datos opcionales (`FEParamGetTiposOpcional`)

Respuesta real 2021 [CAS]:

| `Id` | `Desc` | `FchDesde` | `FchHasta` |
|---|---|---|---|
| 2 | RG Empresas Promovidas - Indentificador de proyecto vinculado a Régimen de Promoción Industrial | 20100917 | NULL |
| 91 | RG Bienes Usados 3411 - Nombre y Apellido o Denominación del vendedor del bien usado. | 20130401 | NULL |
| 92 | RG Bienes Usados 3411 - Nacionalidad del vendedor del bien usado. | 20130401 | NULL |
| 93 | RG Bienes Usados 3411 - Domicilio del vendedor del bien usado. | 20130401 | NULL |
| 5 | Excepcion computo IVA Credito Fiscal | 20141016 | NULL |
| 61 | RG 3668 Impuesto al Valor Agregado - Art.12 IVA Firmante Doc Tipo | 20141016 | NULL |
| 62 | RG 3668 Impuesto al Valor Agregado - Art.12 IVA Firmante Doc Nro | 20141016 | NULL |
| 7 | RG 3668 Impuesto al Valor Agregado - Art.12 IVA Carácter del Firmante | 20141016 | NULL |
| 10 | RG 3.368 Establecimientos de educación pública de gestión privada - Actividad Comprendida | 20150605 | NULL |
| 1011 | RG 3.368 Establecimientos de educación pública de gestión privada - Tipo de Documento | 20150605 | NULL |
| 1012 | RG 3.368 Establecimientos de educación pública de gestión privada - Número de Documento | 20150605 | NULL |
| 11 | RG 2.820 Operaciones económicas vinculadas con bienes inmuebles - Actividad Comprendida | 20150605 | NULL |
| 12 | RG 3.687 Locación temporaria de inmuebles con fines turísticos - Actividad Comprendida | 20150605 | NULL |
| 13 | RG 2.863 Representantes de Modelos | 20160101 | NULL |
| 14 | RG 2.863 Agencias de publicidad | 20160101 | NULL |
| 15 | RG 2.863 Personas físicas que desarrollen actividad de modelaje | 20160101 | NULL |
| 17 | RG 4004-E Locación de inmuebles destino 'casa-habitación'. Dato 2 (dos) = facturación directa / Dato 1 (uno) = facturación a través de intermediario | 20170309 | NULL |
| 1801 | RG 4004-E Locación de inmuebles destino 'casa-habitación'. Clave Única de Identificación Tributaria (CUIT). | 20170309 | NULL |
| 1802 | RG 4004-E Locación de inmuebles destino 'casa-habitación'. Apellido y nombres, denominación y/o razón social. | 20170309 | NULL |
| 2101 | Factura de Crédito Electrónica MiPyMEs (FCE) - CBU del Emisor | 20181226 | NULL |
| 2102 | Factura de Crédito Electrónica MiPyMEs (FCE) - Alias del Emisor | 20181226 | NULL |
| 22 | Factura de Crédito Electrónica MiPyMEs (FCE) - Anulación | 20181226 | NULL |
| 23 | Factura de Crédito Electrónica MiPyMEs (FCE) - Referencia Comercial | 20190308 | NULL |
| 27 | Factura de Credito Electronica MiPyMEs (FCE) - Transferencia | 20201126 | NULL |

Agregados posteriores según el manual (`Desc`/`FchDesde` **[NO VERIFICADO]**):
- **2901** póliza y **2902** endoso de Seguros de Caución (v4.7, 01/09/2026): alfanuméricos de hasta 30, sólo tipos 1, 2,
  3, 6, 7, 8 con punto de venta SEGWS; pueden repetirse en pares, máximo 6000 opcionales (10054, 10273–10281).

Dominios de valores que impone el manual (10064–10132, 10165–10172, 10189, 10214–10216):

| Id | Valor | Código |
|---|---|---|
| 2 | numérico de 8 dígitos ≥ 0; sólo `CbteTipo` 1, 2, 3, 6, 7, 8 | 10064, 10066 |
| 5 | 2 caracteres: 01 Locador / Prestador del mismo, 02 Congresos / Eventos, 03 Operación contemplada en RG 74, 04 Bienes de Cambio, 05 Ropa de trabajo, 06 Intermediario; sólo clase A | 10086, 10088, 10089 |
| 61 | numérico de 2: tipo de documento del firmante (`FEParamGetTiposDoc`) | 10090, 10091 |
| 62 | numérico de hasta 11 | 10092 |
| 7 | numérico de 2: 01 Titular, 02 Director / Presidente, 03 Apoderado, 04 Empleado | 10094, 10095 |
| 10 | 1 carácter: 0 no comprendidas / 1 comprendidas; si 1 → informar 1011 y 1012 | 10097, 10113, 10114 |
| 1011 | tipo de documento del titular del pago | 10098 |
| 1012 | numérico ≤ 11 (80, 86, 87, 96) o alfanumérico ≤ 20 | 10099, 10115 |
| 11, 12, 13, 14, 15 | 1 carácter: 0 / 1 | 10110, 10111, 10116–10118 |
| 17 | 1 = facturación a través de intermediario, 2 = facturación directa; sólo B o C | 10123, 10124, 10129 |
| 1801 | CUIT (11), en padrón, ≠ emisor, sin repetir | 10125, 10126, 10128, 10131 |
| 1802 | alfanumérico ≤ 100 | 10127, 10130 |
| 91 | alfanumérico ≤ 100 (nombre del vendedor de bien usado) | 10077, 10078 |
| 92 | numérico de 3, código de país (`FEParamGetTiposPaises`) | 10079, 10080, 10082 |
| 93 | alfanumérico ≤ 250 (domicilio) | 10083, 10084 |
| 2101 | CBU numérico de 22; obligatorio en facturas FCE; registrado y del emisor | 10165, 10168, 10174 |
| 2102 | alias alfanumérico de 6 a 20 | 10166 |
| 22 | `S` (anulación) / `N`; prohibido en facturas FCE, obligatorio en ND/NC FCE | 10167, 10171, 10173 |
| 23 | alfanumérico ≤ 50, sin valores repetidos; puede repetirse el Id | 10189, 10190 |
| 27 | `SCA` "TRANSFERENCIA AL SISTEMA DE CIRCULACION ABIERTA" o `ADC` "AGENTE DE DEPOSITO COLECTIVO"; obligatorio en facturas FCE | 10214–10216 |

Sólo pueden repetirse 1801/1802, 2901/2902 (10054) y 23 (10190). Un comprobante sólo puede llevar opcionales de **una** RG
(10112). 2101, 2102, 22 y 27 sólo en FCE (10169); FCE exige al menos uno de 2101, 22, 27 (10170).

### 7.8 Condición frente al IVA del receptor (`FEParamGetCondicionIvaReceptor`)

Tabla del Anexo del manual [MAN "Condición Frente al IVA del receptor", págs. PDF 202-203], que coincide en códigos y
descripciones con [TAB] `TABLA-TIPO-RESPONSABLES-V.0-06022025.xls`:

| Código | Descripción | A / ALEY | B | C | 49 |
|---:|---|:---:|:---:|:---:|:---:|
| 1 | IVA Responsable Inscripto | X | | X | |
| 4 | IVA Sujeto Exento | | X | X | |
| 5 | Consumidor Final | | X | X | X |
| 6 | Responsable Monotributo | X | | X | |
| 7 | Sujeto No Categorizado | | X | X | |
| 8 | Proveedor del Exterior | | X | X | |
| 9 | Cliente del Exterior | | X | X | |
| 10 | IVA Liberado – Ley N° 19.640 | | X | X | |
| 13 | Monotributista Social | X | | X | |
| 15 | IVA No Alcanzado | | X | X | |
| 16 | Monotributo Trabajador Independiente Promovido | X | | X | |

ALEY = "A con leyenda 'operación sujeta a retención'". La forma en que el servicio devuelve esta matriz (una fila por
combinación con `Cmp_Clase` = `A`, `B`, ...; o una fila por condición con varias clases): **[NO VERIFICADO]**. La
descripción de 7 en [TAB] es "Sujeto no Categorizado" (minúscula).

### 7.9 Países (`FEParamGetTiposPaises`)

Sin respuesta real disponible. [TAB] publica `TABLA-20-PAISES-20V.0-20-2028072022.xlsx` (hoja "Paises FE", 309 filas de datos,
<https://www.afip.gob.ar/fe/documentos/TABLA-20-PAISES-20V.0-20-2028072022.xlsx>). Se usa para el opcional 92 (10080).
Ejemplo del manual: valor `225` (URUGUAY en [TAB]) [MAN pág. PDF 89]; [CAS] usó `200` (ARGENTINA en [TAB]). Correspondencia exacta con el servicio:
**[NO VERIFICADO]**.

### 7.10 Puntos de venta (`FEParamGetPtosVenta`)

Valor real de `EmisionTipo`: `CAE - Ri Iva` [CAS]. Tipos de punto de venta que el manual nombra (no necesariamente iguales
al texto de `EmisionTipo`): "RECE" (10005); "COMPROBANTES – EXENTO EN IVA – WEB SERVICES" (10096); "SEGWS" (10273);
"CAEA - Fact. Elect. (RECE) - RI IVA", "CAEA – Fact. Elect. (RECE) - Contingencias", "CAEA – Fact. Elect. (RECE) - Exento en
IVA - Contingencias", "CAEA – Fact. Elect. (RECE) - Monotributo - Contingencias" (701, 1444). `Bloqueado` S/N; si está
bloqueado "se deberá ingresar al ABM de puntos de venta a regularizar la situación" [MAN pág. PDF 119].

### 7.11 Cotizaciones

`FEParamGetCotizacion` devuelve la cotización "orientativa" de la base aduanera; para `MonCotiz` con `CanMisMonExt=S` exige
coincidencia exacta con la del día hábil anterior (10038). ArcaSim necesita una fuente de cotizaciones configurable por
fecha (ver §8.2).

---

## 8. Comportamiento que un simulador fiel debe reproducir

### 8.1 Protocolo y forma

1. Misma ruta `/wsfev1/service.asmx`, `?WSDL` con las 22 operaciones y los dos bindings (§1.2).
2. SOAP 1.1 y 1.2 con los `Content-Type` de §1.2; enrutar por `SOAPAction`/`action` y, si falta, por el elemento del Body.
3. `SOAPAction` inexistente → HTTP 500 `soap:Fault` `soap:Client`; XML mal formado → HTTP 400; tipo numérico inválido →
   HTTP 500 `soap:Fault` "Server was unable to read request..." (§1.2).
4. Tolerancias del deserializador .NET: orden libre de elementos, elementos desconocidos ignorados, elementos sin namespace
   ignorados, enteros obligatorios ausentes = 0, `double` con o sin decimales (§1.2).
5. Header `FEHeaderInfo` en toda respuesta no-fault (§1.5). Elegir `ambiente` distinto de los reales (p. ej.
   `ArcaSim - local`) para que nadie confunda ambientes [recomendación].
6. Serialización de §1.6: una línea, strings vacíos `<X />`, `double` normalizado, `NULL` literal en fechas vacías de
   tablas de parámetros, `xmlns="http://ar.gov.afip.dif.FEV1/"` en el elemento de respuesta.
7. Errores de negocio siempre HTTP 200 dentro de `Errors`; observaciones en `Observaciones/Obs`; `Events` vacío salvo
   inyección configurable.
8. Eco de enteros obligatorios con `0` en error (`FECompUltimoAutorizado`, `FECompTotXRequest`, `FECAEAConsultar`
   `ResultGet/Periodo/Orden`, `FECAEASinMovimientoInformar/PtoVta`).

### 8.2 Estado y reglas

1. **Autenticación**: validar Token/Sign contra un WSAA simulado; reproducir el orden y los textos de 500/600 de §1.4;
   relaciones CUIT (600/601); expiración de 12 h; service `wsfe`.
2. **Numeración** por (CUIT, PtoVta, CbteTipo) con `FECompUltimoAutorizado` y 10016; fecha monotónica; sin idempotencia.
3. **Lotes**: procesamiento secuencial; el primer rechazo corta la secuencia (siguientes `R`); `Resultado` A/R/P;
   `CantReg` = cantidad de detalles; límite `RegXReq` configurable (250 por defecto, como homologación 2021); FCE = 1.
4. **CAE**: 14 dígitos únicos; `CAEFchVto = CbteFch + 10` [INFERIDO]; persistir todo lo enviado para `FECompConsultar`
   (con `FchProceso` `yyyymmddhhmiss`, `EmisionTipo` `CAE`, observaciones).
5. **Fechas relativas a "hoy"**: reloj del simulador configurable (para probar N-5/N+5/N-10/N+10, mes de presentación,
   quincenas CAEA). Zona horaria Argentina.
6. **Aritmética** con los márgenes y el redondeo Round Half Even de §4.3.
7. **Cotizaciones**: tabla configurable por moneda y fecha, con lógica de "día hábil anterior" (requiere calendario de
   feriados) para 10038, 10119 y 10240.
8. **Umbral RG 4444** configurable (§4.5) y flag de obligatoriedad de `CondicionIVAReceptorId` (§4.6).
9. **CAEA**: ventana de solicitud de 15006, unicidad 15008, vigencia, `FchTopeInf`, informar comprobantes, sin movimiento.
10. **Padrón simulado** (ver §8.3): CUITs emisoras con su condición (RI, monotributo, exento), actividades, puntos de venta
    (tipo, bloqueado, baja), habilitación clase A, PyME/FCE, domicilio fiscal electrónico, CBU; CUITs receptoras con su
    estado. Con eso se pueden emular 10000, 10005, 10017, 10063, 10161, 10174, 10176–10180, 10195, 10238, 10247–10249 de
    forma determinística (no fiel a los datos reales, sí al contrato).
11. **Fallas inducidas** para que el cliente pruebe su manejo: timeout después de registrar (§5.4), 500/501/502
    ("Transacción Activa"), FEDummy con `DbServer`/`AuthServer` distinto de `OK`.

### 8.3 Imposible de simular fielmente

- Toda validación contra padrones y bases de ARCA (§4.8): situación registral real de emisores y receptores, apócrifos,
  fallecidos, sujetos no confiables, categorías de monotributo y sus topes (10235/10236), alertas de habilitación clase A
  (10234), saldos de cuenta corriente FCE (10184), obligación de FCE por categorización (10188/10192), CBU registrados,
  remitos electrónicos registrados/confirmados/asociados (10120–10122, 10232), actividades por CUIT, nómina de Seguros de
  Caución. Un simulador sólo puede reproducir el **contrato** (códigos y forma) sobre datos ficticios.
- Las cotizaciones oficiales exactas de cada día hábil y el "orientativo" que usa 10119.
- Los textos exactos de `Msg` para la mayoría de los códigos (sólo se conocen los de [wsfev1-codigos.md §5](wsfev1-codigos.md)).
- Los eventos (`Evt`) que ARCA publica.
- Tiempos de respuesta, timeouts del servidor y límites de tasa: **el manual no documenta ninguno** y no hay fuente oficial.
  No se observaron límites en las llamadas de prueba (una decena de requests). **[NO VERIFICADO]** si existen.
- Validez de CAE/CAEA ante terceros (constatación de comprobantes en
  <https://serviciosweb.afip.gob.ar/genericos/comprobantes/Default.aspx>): un CAE simulado nunca va a validar ahí.
- La cadena de certificados real (AC de homologación "Computadores Test" / producción) y la emisión de TA por el WSAA real.

---

## 9. Discrepancias encontradas (y cuál se toma)

| # | Manual | WSDL / real | Se toma |
|---:|---|---|---|
| 1 | Namespace a veces `http://ar.gov.afip.dif.fev1/` en ejemplos de respuesta | `http://ar.gov.afip.dif.FEV1/` | WSDL |
| 2 | Respuestas de ejemplo como SOAP 1.2 o sin `xmlns` en el elemento de respuesta | Según la versión SOAP del request; `xmlns` siempre presente | Real |
| 3 | `<Obs><Observaciones>` en la estructura genérica | `<Observaciones><Obs>` | WSDL |
| 4 | `FECAEASinMovimientoResponse/FECAEASinMovimientoResult` | `FECAEASinMovimientoInformarResponse/FECAEASinMovimientoInformarResult` | WSDL |
| 5 | Request `FEParamGetCondicionFrenteIvaReceptor` | `FEParamGetCondicionIvaReceptor` | WSDL |
| 6 | `FeCAEAReq` como contenedor de FECAEASolicitar | `Periodo` y `Orden` directo bajo la operación | WSDL |
| 7 | `CbteNro` Long(8) en FECompUltimoAutorizado | `int` | WSDL (ambos alcanzan para 8 dígitos) |
| 8 | `Comprador/DocNro` String(80) | `long` | WSDL |
| 9 | `CbteAsoc/Cuit` String(11) en CAE, Long(11) en CAEA | `string` en ambos | WSDL |
| 10 | `ImpIVA`/`ImpTrib` en distinto orden según la sección | `ImpTrib` antes de `ImpIVA` | WSDL (el servicio acepta ambos) |
| 11 | `FchProceso` de 8 dígitos en ejemplos viejos | 14 dígitos `yyyymmddhhmiss` | Real |
| 12 | `EmisionTipo` String(8) | `CAE - Ri Iva` (12 caracteres) | Real |
| 13 | Ejemplos con códigos que no coinciden con las tablas (10030 por CUIT no registrada, "1005", 900) | — | Tablas |
| 14 | 1527 listado en la tabla de FECAEASolicitar | Por contenido es de FECAEARegInformativo | Contenido |
| 15 | Código 1445 usado dos veces en FECAEARegInformativo (excluyente FCE DocNro y observación CbteFch vs CbteFchHsGen) | — | Documentar ambos; **[NO VERIFICADO]** cuál emite el servicio |
| 16 | Ejemplo con `<ar:ImpIva>` | `ImpIVA` | WSDL |
| 17 | `FEParamGetActividades`: tabla rotulada `FEParamGetTiposPaisesResult`, etiquetas con espacios | `FEParamGetActividadesResult`, `ActividadesTipo` | WSDL |
| 18 | `FchProceso` String(8) en FECAEAConsultar vs String(14) en FECAEASolicitar | — | **[NO VERIFICADO]** |
| 19 | 10016 descripto como excluyente de detalle | Aparece como `Err` (numeración) o como `Obs` (rango de fecha) | Real (§4.1 de códigos) |

---

## 10. No verificado / preguntas abiertas

1. Valores actuales de todas las tablas `FEParamGet*` (sólo hay captura real de 2021; faltan `CondicionIvaReceptor`,
   `Paises`, `Actividades` y los agregados 31, 2901, 2902, 51–54 renombrados). **Acción:** capturarlas con un certificado
   de homologación propio.
2. `RegXReq` actual en homologación y en producción.
3. Regla exacta de `CAEFchVto` (inferida `CbteFch + 10`) y de `FchTopeInf`.
4. Monto actual del umbral RG 4444 (prensa: $10.000.000, RG 5700/2025) y si el servicio lo aplica ya.
5. Comportamiento vigente de `CondicionIVAReceptorId` ausente: 10245 (observa) o 10246 (rechaza), en cada ambiente.
6. Textos de `Msg` para casi todos los códigos; existencia y textos de eventos.
7. Si el código 601 se usa o si "CUIT no incluida en token" sale siempre como 600.
8. Significado preciso de 10240 ("no podrá superar en 1 a la cotización oficial").
9. Orden de evaluación entre validaciones de un mismo comprobante y cuántas `Obs` excluyentes se acumulan.
10. Valores de `FEDummy` cuando un componente no está `OK`.
11. Límites de tasa, timeouts del servidor, tamaño máximo de request.
12. Formato interno del CAE/CAEA (si tiene estructura o dígito verificador).
13. Respuesta de `FEParamGetPtosVenta` sin puntos de venta y valores posibles de `EmisionTipo`.
14. `FECAEASinMovimientoConsultar` con `PtoVta` = 0 (¿lista todos?).
15. `FEParamGetCotizacion` con `MonId` = `PES`.
16. Si `DocTipo` 30 es aceptado.
