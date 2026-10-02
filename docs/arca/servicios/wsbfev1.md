# wsbfev1

**Qué hace.** Autoriza con CAE los comprobantes del régimen de **Bonos Fiscales Electrónicos** ("bienes de capital",
según el catálogo). La portada del manual cita la **R.G. N° 5427/2023 y la R.G. N° 2.861** [MAN30 pág. 1]. Se autoriza
**un comprobante por llamada**. Cada comprobante lleva su propio identificador de requerimiento (`Id`) y un detalle de
ítems. Cada ítem tiene su código **NCM** (nomenclador común del Mercosur), que sale de `BFEGetPARAM_NCM` [MAN30 pág. 13-14].
Admite comprobantes clase A y B (1, 2, 3, 6, 7, 8) y Factura de Crédito Electrónica MiPyMEs (201-203, 206-208)
[CAS, `BFEGetPARAM_Tipo_Cbte`, 2021; MAN30 pág. 18, código 1035].

El manual no explica el régimen: no dice quién está obligado a usar el servicio, qué bienes dan derecho al bono ni cómo
se liquida. Eso queda **NO VERIFICADO** y fuera del alcance del simulador. Lo que sí se ve es que la tabla NCM trae
notas "Bonos Fisc.", "Bonos Fisc. Motos - Beneficiarios" y "Bonos Fisc. Motos - Proveedores", y un código comodín
`9999.99.99` "(item no incluído en el Beneficio Fiscal)" [CAS 2021].

Fecha de relevamiento: **2026-10-02**.

### Fuentes

| Id | Fuente | URL / ubicación | Uso |
|---|---|---|---|
| **[MAN30]** | "Manuales para el desarrollador – Emisión de Bonos Fiscales Electrónicos **v3.0**", R.G. 5427/2023 y R.G. 2.861, ARCA-SDG SIT, revisión 17-mar-2025, 40 págs. | <https://www.afip.gob.ar/ws/documentacion/manuales/WSBFEV1-ManualParaElDesarrollador_ARCA_V3_0.pdf> | Fuente principal para citar páginas. Leído completo. |
| **[MAN32]** | Mismo manual, **v3.2**, revisión 9-jun-2025 (versión de homologación externa, RG 5616), 39 págs. | <https://www.afip.gob.ar/fe/ayuda/documentos/wsbfev1-RG-5427-y-2861.pdf> | Comparado palabra por palabra con v3.0. **Manda en comportamiento** (ver abajo). |
| **[WSDL]** | WSDL de homologación (55 975 bytes, esquema inline, sin imports). El de producción es idéntico salvo el `soap:address`. | `docs/arca/wsdl/wsbfev1-homologacion.wsdl` (= `https://wswhomo.afip.gov.ar/wsbfev1/service.asmx?WSDL`) | Estructura: **manda sobre el manual**. |
| **[VIVO]** | Llamadas propias sin certificado, 2026-10-02 (homologación; un `BFEDummy` a producción). | `scratchpad/vivo/bfev1-*` (`.req.xml`, `.hdr`, `.body`) | Forma real de respuestas, faults y errores de token. |
| **[CAS]** | Respuestas reales de homologación grabadas en junio-agosto 2021 en los tests de PyAfipWs (versión del servicio `1.7.0.0`). | <https://github.com/PyAr/pyafipws/tree/main/tests/cassettes/test_wsbfev1> y `test_wsbfev1_receb1` | **Secundaria**: CAE, fechas, `Reproceso`, `Obs`, tablas de parámetros. Datos de 2021. |
| **[CAT]** | Catálogo de ArcaSim | `docs/arca/catalogo.md` §3.1, §4.1, §5.4 | Contexto y analogía con wsfexv1. |

Las páginas se citan como "pág. N" = página física del PDF de [MAN30], salvo que diga [MAN32].

### Marcas de confianza

- **[MAN30]**, **[MAN32]**, **[WSDL]**, **[VIVO]**: verificado en fuente oficial o en el servicio.
- **[CAS]**: confirmado solo con grabaciones de terceros de 2021.
- **[INFERIDO]**: deducido de ejemplos o de varias fuentes, sin texto oficial explícito.
- **NO VERIFICADO**: no se pudo comprobar. No implementarlo como hecho sin validar contra homologación con certificado.

### Manual V3.0 contra V3.2: cuál manda

Se compararon los dos textos palabra por palabra y las tablas de códigos celda por celda. El contenido técnico es
**idéntico** salvo esto:

| # | Cambio en V3.2 | Fuente |
|---:|---|---|
| 1 | Historial **3.1 (20-05-2025)**: "Se modifica el código de validación del método BFEGetPARAM_CondicionIvaReceptor. Se cambia el código 4963 de dicho método por el 4967." En V3.0 el **4963** se usaba para dos cosas: la obligatoriedad de `CondicionIVAReceptorId` en `BFEAuthorize` (pág. 22) y la clase inválida en `BFEGetPARAM_CondicionIvaReceptor` (pág. 37). V3.2 deja el 4963 solo para `BFEAuthorize`. | [MAN32] pág. 4 y 37 |
| 2 | Historial **3.2 (09-06-2025)**: "Será obligatorio el campo Condición Frente al IVA del receptor, atento a la entrada en vigencia reglamentada por la Resolución General N°5616. Por tal motivo el código de observacion 26 quedará en desuso." | [MAN32] pág. 4 |
| 3 | El texto del historial 3.0 que en V3.0 dice "se agregan los códigos 4963 en validaciones del métodos BFEGetPARAM_CondicionIvaReceptor" en V3.2 dice "4967". | [MAN32] pág. 4 |
| 4 | Erratas tipográficas ("nomenclador14" → "nomenclador13", "Condicion" → "Condición"). Sin efecto. | — |
| 5 | Paginación: desde la sección 2.1.5 todo se corre. Las tablas de 2.1.7 pasan de las págs. 18-23 (V3.0) a 17-22 (V3.2). `BFEGetCotizacion` §2.10.4 pasa de la pág. 39 a la 38. La tabla de códigos de este documento trae las dos páginas. | — |

Lo que **no** cambia: no hay campos nuevos ni códigos nuevos. La observación 26 sigue escrita en el cuerpo del manual
V3.2 (pág. 16), aunque el historial la declara en desuso.

**Manda V3.2** para el comportamiento: es la más nueva y es la que publica la página de homologación externa. Además,
la obligatoriedad que anuncia ya regía antes de este relevamiento: el evento 102 que hoy devuelve wsbfe dice que "el dia
9 de junio de 2025 se actualizo la version del Web Service (WS) en el ambiente de Homologacion Externa en la cual se
establece como obligatorio el campo Condicion Frente al IVA del receptor" [VIVO, `bfe-badtoken.body`]. Consecuencia
para ArcaSim:

- `CondicionIVAReceptorId` ausente → **rechazo 4963** ("Campo Condición Frente al IVA del receptor es obligatorio...",
  pág. 22). Ya no se emite la observación 26 [INFERIDO de la entrada 3.2 más el texto del 4963]. Conviene dejarlo
  **configurable** (modo "pre RG 5616" = observación 26), como en wsfev1.
- `BFEGetPARAM_CondicionIvaReceptor` con `ClaseCmp` inválida → **4967**.

**Ninguno de los dos manuales documenta 5 de las 15 operaciones**: `BFEGetPARAM_Tipo_doc`, `BFEGetPARAM_UMed`,
`BFEGetLast_CMP`, `BFEDummy` y `BFEGetLast_ID`. El manual las menciona de pasada ("Código de unidad de medida
(BFEGetPARAM_Umed)", pág. 14), pero no tienen sección. Para esas 5, la estructura sale del [WSDL] y el comportamiento
de [CAS] y [VIVO].

## Contrato

### Dialecto

**.NET ASMX**, igual que wsfev1 y wsfexv1 [CAT §4.1]. Se nota en:

- el WSDL generado por ASP.NET, con los bindings `ServiceSoap`, `ServiceSoap12`, `ServiceHttpGet` y `ServiceHttpPost`
  y el namespace `s:` de XSD;
- `FEHeaderInfo` en el `soap:Header`;
- los errores de negocio y de token, que vuelven con **HTTP 200** dentro de `BFEErr`;
- los faults `soap:Client` de ASMX [VIVO].

Mismo estilo que wsfexv1: un comprobante por llamada, `Id` de requerimiento propio, **un único** `BFEErr{ErrCode, ErrMsg}`
y **un único** `BFEEvents{EventCode, EventMsg}`, en vez de las listas `Errors`/`Events` de wsfev1.

### Endpoints

| Ambiente | URL del servicio (`soap:address`) | WSDL | Fuente |
|---|---|---|---|
| Homologación | `https://wswhomo.afip.gov.ar/wsbfev1/service.asmx` | `https://wswhomo.afip.gov.ar/wsbfev1/service.asmx?WSDL` | [MAN30] pág. 9; [WSDL] |
| Producción | `https://servicios1.afip.gov.ar/wsbfev1/service.asmx` | `https://servicios1.afip.gov.ar/wsbfev1/service.asmx?WSDL` | [MAN30] pág. 9-10; WSDL de producción bajado el 2026-10-02 |

- Las secciones de cada método muestran `http://wswhomo.afip.gov.ar/wsbfev1/service.asmx?op=<Método>` (con `http`,
  pág. 11 y siguientes). Es la página de ayuda de ASMX, no un endpoint aparte.
- [CAS] llamó a `https://wswhomo.afip.gov.ar/WSBFEv1/service.asmx` (con mayúsculas) y funcionó: IIS no distingue
  mayúsculas en la ruta. ArcaSim debería aceptar la ruta sin distinguir mayúsculas [INFERIDO].
- `GET ...?WSDL` → 200 `text/xml; charset=utf-8` [VIVO].

### Namespace y forma del XML

- `targetNamespace` y namespace de todos los elementos: **`http://ar.gov.afip.dif.bfev1/`** (minúsculas) [WSDL].
- `elementFormDefault="qualified"`: **todos** los hijos van en ese namespace. Un `<Auth>` sin namespace se ignora
  (ver Autenticación) [WSDL] [VIVO].
- Erratas del manual: el ejemplo de respuesta de `BFEGetPARAM_Tipo_Opc` usa `http://ar.gov.afip.dif.bfe/` (el namespace
  de wsbfe, pág. 34). Varios ejemplos parten la URL en dos ("`http://ar.gov-` / `.afip.dif.bfev1/`", págs. 29, 31, 33,
  36). Se toma el WSDL.
- `wsdl:documentation` del servicio: "Web Service orientado  al  servicio  de Bonos Fiscales electronicos V1" [WSDL].

### Service id de WSAA

**`wsbfe`**, no "wsbfev1". Literal del manual: "debe enviar el tag service con el valor "wsbfe" y que la duración del
mismo es de 12 hs" [MAN30 pág. 7, §1.3; MAN32 pág. 7]. Antes hay que asociar el certificado al servicio de negocio
"Bonos Fiscales Electrónicos - BFE" (misma página).

Confirmación secundaria: en [CAS] (`test_dummy.yaml`, 2021) el TA que se usó contra wsbfev1 dice
`<login ... service="wsbfe">` y `dst="CN=wsbfe, O=AFIP, C=AR"`. wsbfe usa el mismo id (ver `wsbfe.md`), así que **un
mismo TA sirve para los dos servicios** [INFERIDO de que ambos manuales piden `wsbfe`].

### SOAPAction

Patrón: `http://ar.gov.afip.dif.bfev1/` + nombre de la operación, igual para SOAP 1.1 y 1.2 [WSDL]:

```
http://ar.gov.afip.dif.bfev1/BFEAuthorize
http://ar.gov.afip.dif.bfev1/BFEGetCMP
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_Tipo_doc
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_Tipo_IVA
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_Tipo_Opc
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_Zonas
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_Tipo_Cbte
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_UMed
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_NCM
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_MON
http://ar.gov.afip.dif.bfev1/BFEGetLast_CMP
http://ar.gov.afip.dif.bfev1/BFEDummy
http://ar.gov.afip.dif.bfev1/BFEGetLast_ID
http://ar.gov.afip.dif.bfev1/BFEGetCotizacion
http://ar.gov.afip.dif.bfev1/BFEGetPARAM_CondicionIvaReceptor
```

Respuesta: `<{Op}Response xmlns="http://ar.gov.afip.dif.bfev1/"><{Op}Result>…</{Op}Result></{Op}Response>` en las 15
[WSDL] [VIVO].

### Bindings: SOAP 1.1, SOAP 1.2 y HTTP

| Binding | Port | Operaciones | Fuente |
|---|---|---|---|
| `tns:ServiceSoap` (`soap:binding`, `document`/`literal`) | `ServiceSoap`, `soap:address` | las 15 | [WSDL] |
| `tns:ServiceSoap12` (`soap12:binding`) | `ServiceSoap12`, `soap12:address` | las 15 | [WSDL] |
| `tns:ServiceHttpGet` (`http:binding verb="GET"`) | `ServiceHttpGet` | solo `BFEDummy`, `location="/BFEDummy"`, salida `mime:mimeXml` → elemento `DummyResponse` | [WSDL] |
| `tns:ServiceHttpPost` (`verb="POST"`, `application/x-www-form-urlencoded`) | `ServiceHttpPost` | solo `BFEDummy` | [WSDL] |

Comportamiento real [VIVO, 2026-10-02, homologación; igual en wsbfe]:

| Caso | Respuesta |
|---|---|
| SOAP 1.1, `Content-Type: text/xml; charset=utf-8`, `SOAPAction` correcto | 200 `text/xml; charset=utf-8` |
| SOAP 1.2, `Content-Type: application/soap+xml; charset=utf-8; action="…/BFEDummy"` | 200 `application/soap+xml; charset=utf-8`, sobre SOAP 1.2 con prefijo `soap:` y el mismo `FEHeaderInfo` (`bfev1-dummy-soap12`, `bfev1-badtoken-soap12`) |
| SOAP 1.1 **sin** header `SOAPAction` | 200: enruta por el elemento del Body (`bfev1-nosoapaction-header`) |
| `SOAPAction: ""` (vacío) | **HTTP 500** `soap:Fault`, `faultcode` `soap:Client`, `faultstring` `Server did not recognize the value of HTTP Header SOAPAction: .` (`bfev1-noaction-dummy`) |
| `SOAPAction` inexistente (`…/BFEXXX`) | **HTTP 500**, `faultstring` `Server did not recognize the value of HTTP Header SOAPAction: http://ar.gov.afip.dif.bfev1/BFEXXX.` Sin `soap:Header` y **sin stack trace** (wsfev1 sí lo trae) (`bfev1-badaction`) |
| `SOAPAction` correcto pero elemento del Body con otro nombre (`BFEGetPARAM_Tipo_Iva`, como escribe el manual, con `SOAPAction …/BFEGetPARAM_Tipo_IVA`) | 200: manda el `SOAPAction`. El envoltorio desconocido se ignora, `auth` se pierde y vuelve `1000 Usuario no autorizado a realizar esta operacion. ` (`bfev1-tipoiva-bodycase`) |
| `SOAPAction …/BFEGetPARAM_Tipo_Iva` (con la mayúscula del manual) | **HTTP 500** "Server did not recognize the value of HTTP Header SOAPAction" (`bfev1-tipoiva-manualcase`): **los nombres distinguen mayúsculas** |
| Valor no numérico en un `short` (`<Tipo_cbte>abc</Tipo_cbte>`) | **HTTP 500** `soap:Client`, `faultstring` `Server was unable to read request. ---&gt; There is an error in XML document (1, 286). ---&gt; Input string was not in a correct format.` **Este fault sí trae `FEHeaderInfo`** (`bfev1-badint`) |
| XML mal formado (cortado) | **HTTP 400**, cuerpo vacío (`bfev1-malformed`) |
| `GET https://wswhomo.afip.gov.ar/wsbfev1/service.asmx/BFEDummy` | 200 `text/xml; charset=utf-8`, XML **indentado** sin sobre SOAP: `<DummyResponse xmlns:xsd=… xmlns:xsi=… xmlns="http://ar.gov.afip.dif.bfev1/">` con `AppServer`, `DbServer` y `AuthServer` en `OK` (`bfev1-httpget-dummy`) |

Headers HTTP observados [VIVO]:
- `BFEDummy`: `Cache-Control: private, max-age=0`.
- Las demás operaciones: `Cache-Control: no-cache`, `Pragma: no-cache` y `Expires: -1`.
- Todas traen cookies del balanceador F5 (`f5avraaaaaaaaaaaaaaaa_session_`, `TS010b76f1`), que no hacen falta para operar.

A diferencia de los servicios Java (`BL<n> … 500`, brief común), acá el F5 **no** enmascara los HTTP 500: el fault ASMX
llega intacto.

### Header de respuesta `FEHeaderInfo`

"Los mensajes de respuesta que se transmiten tienen implementado el subelemento FEHeaderInfo contenido en el elemento
opcional Header [...] El procesamiento de dicha información no es obligatoria" [MAN30 pág. 7]. Hijos en orden:
`ambiente`, `fecha`, `id`. Namespace `http://ar.gov.afip.dif.bfev1/`.

| Ambiente | `ambiente` | `id` | Fuente |
|---|---|---|---|
| Homologación 2026-10-02 | **no viene el elemento** | `3.0.0.1` | [VIVO] `bfev1-dummy.body` y todas las demás |
| Producción 2026-10-02 | `Produccion - sr5` | `3.0.0.1` | [VIVO] `bfev1-prod-dummy.body` |
| Homologación 2021 | `Homologacion - efa` | `1.7.0.0` | [CAS] |
| Manual | `Desarrollo - Clo` / `Produccion - Pto` | `1.0.3.0` | [MAN30] pág. 7-8 |

Homologación de wsbfev1 **omite `<ambiente>`**: `<FEHeaderInfo xmlns="http://ar.gov.afip.dif.bfev1/"><fecha>2026-10-02T15:07:36.7130486-03:00</fecha><id>3.0.0.1</id></FEHeaderInfo>`.
wsbfe sí lo manda (`Homologacion - srt`), y en 2021 wsbfev1 también lo mandaba. Un cliente que lea `ambiente` como
obligatorio falla contra la homologación actual. ArcaSim debería poder **omitirlo** por configuración.

`fecha`: hora local con offset `-03:00` y hasta 7 decimales de segundo. .NET quita los ceros finales: se vio
`15:16:05.758954-03:00`, con 6 decimales [VIVO].

### Serialización observada

[VIVO] [CAS], serializador XML de .NET:

- Una sola línea, con `<?xml version="1.0" encoding="utf-8"?>` y la raíz
  `<soap:Envelope xmlns:soap=… xmlns:xsi=… xmlns:xsd=…>`.
- Strings vacíos como `<Pro_codigo_sec />` y `<EventMsg />`. Strings nulos: se omite el elemento.
- `double` normalizado, sin ceros de relleno: se envía `1754.50` y vuelve `1754.5`; se envía `10.00` y vuelve `10`.
- Fechas "sin valor" en las tablas de parámetros: string literal **`NULL`** (`<Cbte_vig_hasta>NULL</Cbte_vig_hasta>`,
  `<NCM_Ds>NULL</NCM_Ds>`).
- `Obs` sin observaciones: **un espacio**, `<Obs> </Obs>` [CAS].
- Un `Pro_ds` volvió **rellenado con espacios hasta 250 caracteres** en `BFEGetCMP` (`ACCESORIOS UTILIZADOS` + espacios),
  pero otro (`Cafe`) no [CAS, `test_main_get.yaml` y `test_consulta.yaml`]. Ver No verificado.

## Autenticación

### Forma del bloque

Tipo `ClsBFEAuthRequest` [WSDL]:

| Campo | Tipo XSD | Ocurrencia | Manual | Descripción [MAN30 pág. 12-13] |
|---|---|---|---|---|
| `Token` | `s:string` | 0..1 | S | "Token devuelto por el WSAA" |
| `Sign` | `s:string` | 0..1 | S | "Sign devuelto por el WSAA" |
| `Cuit` | `s:long` | 1..1 | S | "Cuit contribuyente (representado o Emisora)" |

**Trampa: el nombre del elemento cambia según la operación** [WSDL]. Esto define exactamente qué acepta el servicio:

| Elemento | Tipo | Operaciones |
|---|---|---|
| `Auth` (mayúscula) | `ClsBFEAuthRequest` | `BFEAuthorize`, `BFEGetCMP`, `BFEGetPARAM_NCM`, `BFEGetLast_ID`, `BFEGetCotizacion` |
| `auth` (minúscula) | `ClsBFEAuthRequest` | `BFEGetPARAM_Tipo_doc`, `BFEGetPARAM_Tipo_IVA`, `BFEGetPARAM_Tipo_Opc`, `BFEGetPARAM_Zonas`, `BFEGetPARAM_Tipo_Cbte`, `BFEGetPARAM_UMed`, `BFEGetPARAM_MON`, `BFEGetPARAM_CondicionIvaReceptor` |
| `Auth` de tipo **`ClsBFE_LastCMP`** | `Token`, `Sign`, `Cuit`, **`Pto_venta`**, **`Tipo_cbte`** | `BFEGetLast_CMP`: los argumentos de la consulta van **dentro** de `Auth` |
| — | — | `BFEDummy` (sin autenticación) |

Con el nombre equivocado (`Auth` en `BFEGetPARAM_Tipo_Cbte`, `auth` en `BFEGetPARAM_NCM`), el deserializador ignora el
elemento y responde como si no hubiera autenticación: `1000 Usuario no autorizado a realizar esta operacion. `
[VIVO, `bfev1-tipocbte-upperAuth`, `bfev1-ncm-lowerauth`]. El manual se equivoca en esto: muestra `<Auth>` en
`BFEGetPARAM_MON`, `_Tipo_Cbte`, `_Tipo_Iva` y `_CondicionIvaReceptor`, y `<auth>…</Auth>` en `_Zonas` (págs. 26-36).
También escribe `<Cuit>string</Cuit>` en `_NCM`, `_Tipo_Cbte`, `_Tipo_Iva` y `_Zonas`, cuando es `long`. **Manda el WSDL.**

### Validaciones del manual

Mismas en todas las operaciones que tienen sección [MAN30 pág. 17, 27, 29, 30, 32, 33-34, 35, 37, 39]:

| Validación | Código | Mensaje |
|---|---|---|
| Verificación de Token y Firma | 1000 | Usuario no autorizado a realizar esta operación |
| Cuit solicitante se encuentra entre sus representados | 1001 | Cuit solicitante no se encuentra entre sus representados |

### Respuestas reales de falla

[VIVO, 2026-10-02, homologación; se probó `BFEGetPARAM_Tipo_Cbte` con cada caso y token "abc" en `BFEAuthorize`,
`BFEGetCMP`, `BFEGetLast_CMP`, `BFEGetLast_ID`, `BFEGetCotizacion`, `BFEGetPARAM_CondicionIvaReceptor` y
`BFEGetPARAM_NCM`]:

| Caso | `ErrCode` | `ErrMsg` literal (atención a los espacios finales) |
|---|---|---|
| `Token` = "abc" (no es base64) | 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc` |
| `Token` y `Sign` vacíos | 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: No validó la firma digital. ` |
| Token XML bien formado en base64 con firma falsa | 1000 | ídem anterior |
| Sin elemento `auth`/`Auth` (o con el nombre equivocado) | 1000 | `Usuario no autorizado a realizar esta operacion. ` |

Forma de la respuesta, igual en las 8 operaciones probadas: **HTTP 200**, con **solo `BFEErr` y `BFEEvents`**. No viene el
elemento de resultado (`BFEResultAuth`, `BFEResultGet` ni `BFEResult_LastCMP`). Ejemplo completo:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.bfev1/"><fecha>2026-10-02T15:07:36.8605843-03:00</fecha><id>3.0.0.1</id></FEHeaderInfo></soap:Header><soap:Body><BFEGetPARAM_Tipo_CbteResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEGetPARAM_Tipo_CbteResult><BFEErr><ErrCode>1000</ErrCode><ErrMsg>Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</ErrMsg></BFEErr><BFEEvents><EventCode>39</EventCode><EventMsg>IMPORTANTE: Por motivos de mantenimiento, el servicio estara fuera de linea el domingo 14 de junio desde las 21:00 hs durante aproximadamente 3 horas. Agradecemos tu comprension y pedimos disculpas por las molestias ocasionadas.</EventMsg></BFEEvents></BFEGetPARAM_Tipo_CbteResult></BFEGetPARAM_Tipo_CbteResponse></soap:Body></soap:Envelope>
```

- Aunque el manual habla de "operación" con tilde, el servicio real escribe "operacion" sin tilde y agrega el detalle
  de la validación después de `. ` [VIVO].
- La validación del token es anterior a la del esquema de negocio: con token inválido, `BFEAuthorize` responde 1000
  aunque el comprobante esté completo (`bfev1-authorize-badtoken`).
- **NO VERIFICADO**: el texto real de 1001; los textos de token vencido, de TA emitido para otro service id y de CUIT
  fuera de las relaciones del token. Por analogía con los demás ASMX, probablemente sean 1000 o 1001 seguidos del
  detalle de `ValidacionDeToken` [INFERIDO].
- El evento que acompaña (código 39, aviso de mantenimiento del "domingo 14 de junio") viene en toda respuesta de
  homologación, incluidos los errores. Ver Comportamiento a simular.

## Operaciones

Las 15 del `portType ServiceSoap`, en el orden del WSDL. Convenciones de las tablas:

- "XSD" = tipo y `minOccurs..maxOccurs` del WSDL.
- "Man." = columna "Obligatorio" del manual.
- "Tipo man." = tipo que dice el manual, cuando difiere del XSD.

En los ejemplos, `xmlns:x="http://ar.gov.afip.dif.bfev1/"`. Los requests van dentro de
`<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:x="…"><soapenv:Header/><soapenv:Body>…</soapenv:Body></soapenv:Envelope>`,
y las respuestas llevan el sobre y el `FEHeaderInfo` descriptos en Contrato.

### Tipos compartidos

**`ClsBFEErr`** (elemento `BFEErr`, 0..1 en todos los resultados salvo `BFEDummy`) [WSDL]:

| Campo | XSD | Descripción |
|---|---|---|
| `ErrCode` | `s:int` 1..1 | Código de error. **`0` si no hubo error** [CAS] |
| `ErrMsg` | `s:string` 0..1 | Mensaje. **`OK` si no hubo error** [CAS; MAN30 pág. 38, ejemplo de `BFEGetCotizacion`] |

El manual la describe con `errcode`/`errmsg` en minúscula (pág. 8, 28, 31, 33) y con `ErrCode`/`Errmsg` en otros
ejemplos (pág. 14, 24). **Manda el WSDL: `ErrCode` y `ErrMsg`**. Hay un solo error por respuesta: no es una lista.

**`ClsBFEEvents`** (elemento `BFEEvents`, 0..1) [WSDL]:

| Campo | XSD | Descripción |
|---|---|---|
| `EventCode` | `s:int` 1..1 | "Código de evento (único e irrepetible)" [MAN30 pág. 9] |
| `EventMsg` | `s:string` 0..1 | Mensaje |

Sin eventos: `EventCode` `0` con `EventMsg` `Ok` (recuperadores, `BFEGetLast_*`) o `<EventMsg />` (`BFEAuthorize`,
`BFEGetCMP`) [CAS]. Con evento: un solo evento por respuesta (por ejemplo el 39, [VIVO]).

**Forma de todas las respuestas** (salvo `BFEDummy`): `{Op}Result` = elemento de resultado (0..1) + `BFEErr` +
`BFEEvents`. El elemento de resultado se llama `BFEResultAuth` (`BFEAuthorize`), `BFEResult_LastCMP` (`BFEGetLast_CMP`)
o `BFEResultGet` (las demás) [WSDL].

**`Item`** (lista `ArrayOfItem` → `Item` 0..unbounded, `nillable`) [WSDL; MAN30 pág. 14]:

| Campo | XSD | Tipo man. | Man. | Descripción |
|---|---|---|---|---|
| `Pro_codigo_ncm` | `s:string` 0..1 | String | S | "Código de producto (nomenclador común del MERCOSUR)". Valores de `BFEGetPARAM_NCM`, con formato `9999.99.99` [CAS] |
| `Pro_codigo_sec` | `s:string` 0..1 | String | N | "Código de producto según Secretaria" |
| `Pro_ds` | `s:string` 0..1 | String | S | Descripción del producto |
| `Pro_qty` | `s:double` 1..1 | Double | S | Cantidad |
| `Pro_umed` | `s:int` 1..1 | Int | S | Unidad de medida (`BFEGetPARAM_UMed`) |
| `Pro_precio_uni` | `s:double` 1..1 | Double | S | Precio unitario |
| `Imp_bonif` | `s:double` 1..1 | Double | S | Importe de bonificación |
| `Imp_total` | `s:double` 1..1 | Double | S | Importe total del ítem. En los ejemplos aprobados es `(Pro_qty × Pro_precio_uni − Imp_bonif) × (1 + alícuota)`, o sea **con IVA**: `(10×150−50)×1,21 = 1754,50`; `(2×100−0)×1,21 = 242`; `(4×50−10)×1,21 = 229,90` [CAS; regla INFERIDA] |
| `Iva_id` | `s:short` 1..1 | Int | S | Alícuota (`BFEGetPARAM_Tipo_IVA`) |

**`Opcional`** (lista `ArrayOfOpcional` → `Opcional` 0..unbounded, `nillable`) [WSDL; MAN30 pág. 13]:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `Id` | `s:string` 0..1 | S | "Código del tipo de opcional. Los id aceptados se obtienen del método BFEGetPARAM_Tipo_Opc" |
| `Valor` | `s:string` 0..1 | S | Valor a registrar |

**`CbteAsoc`** (lista `ArrayOfCbteAsoc` → `CbteAsoc` 0..unbounded, `nillable`) [WSDL; MAN30 pág. 14]:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `Tipo_cbte` | `s:short` 1..1 | S | Tipo del comprobante asociado |
| `Punto_vta` | `s:int` 1..1 | S | Punto de venta |
| `Cbte_nro` | `s:long` 1..1 | S | Número |
| `Cuit` | `s:string` 0..1 | N | "Cuit emisor del comprobante Asociado". 11 dígitos (4887, 4917). Obligatorio en FCE débito/crédito (4887) |
| `Fecha_cbte` | `s:string` 0..1 | N | "Fecha emisión del comprobante Asociado". `yyyymmdd`, ≤ fecha del comprobante (4895). Obligatoria en FCE débito/crédito (4896) |

"Aclaración" del manual (pág. 14):
- a una factura (01 o 06) solo se le puede asociar el 91 (remito);
- a una ND o NC A (02 o 03): 01, 02, 03 o 91;
- a una ND o NC B (07 o 08): 06, 07, 08 o 91.

Las reglas FCE están en los códigos 4886-4956.

### 1. BFEAuthorize

**Propósito.** "Autoriza un comprobante, devolviendo su CAE correspondiente" [WSDL]. "Recibe la información de
factura/lote de ingreso" y "Retorna la información del comprobante de ingreso agregándole el CAE otorgado. Ante
cualquier anomalía se retorna un código de error cancelando la ejecución del WS" [MAN30 pág. 11 y 14].

**Request** `BFEAuthorize`: `Auth` (`ClsBFEAuthRequest` 0..1) y `Cmp` (`ClsBFERequest` 0..1) [WSDL].

`Cmp` (`ClsBFERequest`), en orden de esquema [WSDL; MAN30 pág. 11-13]:

| # | Campo | XSD | Tipo man. | Man. | Descripción y límites |
|---:|---|---|---|---|---|
| 1 | `Id` | `s:long` 1..1 | Long | (vacío) | "Identificador del requerimiento". Debe ser > 0 (1014, pág. 17). Ver Comportamiento a simular (idempotencia) |
| 2 | `Tipo_doc` | `s:short` 1..1 | Int | S | Código de documento del comprador. Clase A → 80 (1014). La tabla solo trae 80 (CUIT) [CAS] |
| 3 | `Nro_doc` | `s:long` 1..1 | Long | S | Número de identificación del comprador |
| 4 | `Zona` | `s:short` 1..1 | Short | S | Código de zona (`BFEGetPARAM_Zonas`; único valor 1 "Nacional" [CAS]) |
| 5 | `Tipo_cbte` | `s:short` 1..1 | Int | S | Tipo de comprobante (`BFEGetPARAM_Tipo_Cbte`). Inválido → 1014 "Tipo de comprobante inválido." |
| 6 | `Punto_vta` | `s:int` 1..1 | Int | S | 1 a 99998, "único para el requerimiento" (1014). Pasó de 4 a 5 dígitos en la versión 2.1 (pág. 2) |
| 7 | `Cbte_nro` | `s:long` 1..1 | Long | S | 1 a 99999999 (1014) |
| 8 | `Imp_total` | `s:double` 1..1 | Double | S | Importe total de la operación. FCE: ≥ 0 (4955) |
| 9 | `Imp_tot_conc` | `s:double` 1..1 | Double | S | Conceptos que no integran el precio neto gravado |
| 10 | `Imp_neto` | `s:double` 1..1 | Double | S | Neto gravado |
| 11 | `Impto_liq` | `s:double` 1..1 | Double | S | Impuesto liquidado |
| 12 | `Impto_liq_rni` | `s:double` 1..1 | Double | S | "Impuesto liquidado a RNI o percepción a no categorizados" |
| 13 | `Imp_op_ex` | `s:double` 1..1 | Double | S | Operaciones exentas. > 0 si hay algún ítem con IVA exento (1014) |
| 14 | `Imp_perc` | `s:double` 1..1 | Double | S | Percepciones o pagos a cuenta de impuestos nacionales |
| 15 | `Imp_iibb` | `s:double` 1..1 | — | — | No está en la tabla del manual (sí en el XML de pág. 11). Percepción de Ingresos Brutos [INFERIDO del texto del 1014, pág. 17] |
| 16 | `Imp_perc_mun` | `s:double` 1..1 | — | — | Ídem. Percepción de impuestos municipales [INFERIDO] |
| 17 | `Imp_internos` | `s:double` 1..1 | Double | S | Impuestos internos |
| 18 | `Imp_moneda_Id` | `s:string` 0..1 | **Double** (errata) | S | Moneda (`BFEGetPARAM_MON`). `PES` → `CanMisMonExt` ausente o `N` (4958) |
| 19 | `Imp_moneda_ctz` | `s:double` 0..1 | Double | N | "De informar el campo, el mismo no puede quedar vacío". Obligatorio salvo `CanMisMonExt`=S, moneda sin cotización BNA o comprobante que no es factura; > 0 (4957); no supera "en 1" la oficial (4960); banda del 1014 |
| 20 | `Fecha_cbte` | `s:string` 0..1 | String | S | `yyyymmdd`; ±5 días de la fecha de envío y mismo mes (1014). FCE: N-5 a N+1 (4897) y mismo mes si es futura (4898). "Si no se envía la fecha del comprobante se asignará la fecha de proceso" (pág. 17) |
| 21 | `Fecha_vto_pago` | `s:string` 0..1 | String | N | El manual dice "Fecha de comprobante (yyyymmdd)" (errata). Es el vencimiento del pago FCE (pág. 25): obligatorio en 201/206 (4900), ≥ emisión o presentación (4901), prohibido fuera de FCE salvo ND/NC de anulación (4902) |
| 22 | `CondicionIVAReceptorId` | `s:int` 0..1 | Int | N (V3.0) → **obligatorio (V3.2)** | Tabla del anexo 3.1. Inválido → 4961; no corresponde a la clase → 4962; ausente → 4963 |
| 23 | `CanMisMonExt` | `s:string` 0..1 | String | N | "S" o "N", no vacío (4959). Indica si se cancela en la misma moneda extranjera |
| 24 | `Opcionales` | `ArrayOfOpcional` 0..1 | Opcional | N | Ver `Opcional`. Reglas 1015-1020, 4905-4916, 4931-4932, 4946, 4949, 4952-4954 |
| 25 | `Items` | `ArrayOfItem` 0..1 | Item | S | Ver `Item`. Suma de ítems ≤ totales (1014) |
| 26 | `CbtesAsoc` | `ArrayOfCbteAsoc` 0..1 | CbteAsoc | N | Ver `CbteAsoc`. Reglas 1030-1039, 4880-4929, 4945, 4947, 4950, 4951, 4956 |

**Response** `BFEAuthorizeResult` (`BFEResponseAuthorize`): `BFEResultAuth` (`ClsBFEOutAuthorize` 0..1), `BFEErr` y
`BFEEvents` [WSDL]. `BFEResultAuth` [WSDL; MAN30 pág. 15]:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `Id` | `s:long` 1..1 | S | Identificador del requerimiento (eco). **`0` si hubo error de negocio** [CAS] |
| `Cuit` | `s:long` 1..1 | S | CUIT del contribuyente. **`0` si hubo error** [CAS] |
| `Cae` | `s:string` 0..1 | S | CAE. 14 dígitos [CAS] |
| `Fch_venc_Cae` | `s:string` 0..1 | S | Vencimiento del CAE, `yyyymmdd` |
| `Fch_cbte` | `s:string` 0..1 | S | Fecha del comprobante, `yyyymmdd` (la asignada si no se envió) |
| `Resultado` | `s:string` 0..1 | S | Resultado. Se vio `A` [CAS] |
| `Reproceso` | `s:string` 0..1 | S | "Indica si es un reproceso "S" o "N"" |
| `Obs` | `s:string` 0..1 | S | "Observaciones, motivo de rechazo según tabla de motivos" (códigos 15-18 y 21-26, pág. 15-16). Sin observaciones: `" "` [CAS] |

**Validaciones**: todas las de la tabla de Validaciones y errores con operación `BFEAuthorize`. El manual no dice en qué
orden se evalúan. Como `BFEErr` lleva un solo código, gana el primero que falla [INFERIDO].

**Ejemplo real aprobado** [CAS, `test_autorizar.yaml`, 20-jun-2021; Token y Sign omitidos]:

```xml
<BFEAuthorize xmlns="http://ar.gov.afip.dif.bfev1/">
  <Auth><Token>…</Token><Sign>…</Sign><Cuit>20267565393</Cuit></Auth>
  <Cmp><Id>993456789012387</Id><Tipo_doc>80</Tipo_doc><Nro_doc>20888888883</Nro_doc><Zona>1</Zona><Tipo_cbte>1</Tipo_cbte><Punto_vta>5</Punto_vta><Cbte_nro>2581</Cbte_nro><Imp_total>1754.50</Imp_total><Imp_tot_conc>0.00</Imp_tot_conc><Imp_neto>1450.00</Imp_neto><Impto_liq>304.50</Impto_liq><Impto_liq_rni>0.00</Impto_liq_rni><Imp_op_ex>0.00</Imp_op_ex><Imp_perc>0.00</Imp_perc><Imp_iibb>0.00</Imp_iibb><Imp_perc_mun>0.00</Imp_perc_mun><Imp_internos>0.00</Imp_internos><Imp_moneda_Id>PES</Imp_moneda_Id><Imp_moneda_ctz>1</Imp_moneda_ctz><Fecha_cbte>20210621</Fecha_cbte>
    <Items><Item><Pro_codigo_ncm>2101.11.10</Pro_codigo_ncm><Pro_codigo_sec></Pro_codigo_sec><Pro_ds>Cafe</Pro_ds><Pro_qty>10.00</Pro_qty><Pro_umed>5</Pro_umed><Pro_precio_uni>150.00</Pro_precio_uni><Imp_bonif>50.00</Imp_bonif><Imp_total>1754.50</Imp_total><Iva_id>5</Iva_id></Item></Items>
  </Cmp>
</BFEAuthorize>
```
```xml
<BFEAuthorizeResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEAuthorizeResult><BFEResultAuth><Id>993456789012387</Id><Cuit>20267565393</Cuit><Cae>71253948358918</Cae><Fch_venc_Cae>20210701</Fch_venc_Cae><Fch_cbte>20210621</Fch_cbte><Resultado>A</Resultado><Reproceso>N</Reproceso><Obs> </Obs></BFEResultAuth><BFEErr><ErrCode>0</ErrCode><ErrMsg>OK</ErrMsg></BFEErr><BFEEvents><EventCode>0</EventCode><EventMsg /></BFEEvents></BFEAuthorizeResult></BFEAuthorizeResponse>
```

**Ejemplo real rechazado** [CAS, `test_main_prueba.yaml`, 22-jul-2021]. Una FCE 201 sin `Fecha_vto_pago`:

```xml
<BFEAuthorizeResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEAuthorizeResult><BFEResultAuth><Id>0</Id><Cuit>0</Cuit></BFEResultAuth><BFEErr><ErrCode>4900</ErrCode><ErrMsg>Para Factura de Credito, es obligatorio informar el campo Fecha_vto_pago.</ErrMsg></BFEErr><BFEEvents><EventCode>0</EventCode><EventMsg /></BFEEvents></BFEAuthorizeResult></BFEAuthorizeResponse>
```

Con un error de negocio, `BFEResultAuth` **sí viene**, con `Id` y `Cuit` en 0 y sin los demás campos. Con un error de
token **no viene** (ver Autenticación). El `ErrMsg` real **no** es el texto de la tabla del manual: para 4900 el manual
dice "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Tipo 201 [...] es obligatorio informar el campo
Fecha_vto_pago". Otro caso real, el 4954: `Si informa comprobante MiPyMEs (FCE) del tipo Factura, es obligatorio
informar opcional por RG con ID 27 y su valor correspondiente. Valores esperados SCA = 'TRANSFERENCIA AL SISTEMA DE
CIRCULACION ABIERTA' o ADC = 'AGENTE DE DEPOSITO COLECTIVO'` [CAS, `test_main_prueba_fce.yaml`].

### 2. BFEGetCMP

**Propósito.** "Recupera los datos completos de un comprobante ya autorizado" [WSDL]. "Retorna los detalles de un
comprobante ya enviado y autorizado" [MAN30 pág. 23].

**Request**: `Auth` (`ClsBFEAuthRequest` 0..1) y `Cmp` (`ClsBFEGetCMP` 0..1) con `Tipo_cbte` `s:short` 1..1,
`Punto_vta` `s:int` 1..1 y `Cbte_nro` `s:long` 1..1 [WSDL; MAN30 pág. 22].

**Response** `BFEGetCMPResult` (`BFEGetCMPResponse`): `BFEResultGet` (`ClsBFEGetCMPR` 0..1), `BFEErr` y `BFEEvents`.
`ClsBFEGetCMPR` en orden de esquema [WSDL], con el manual al lado [MAN30 pág. 23-25]:

| # | Campo | XSD | Man. | Notas |
|---:|---|---|---|---|
| 1 | `Id` | `s:long` 1..1 | (en el XML, no en la tabla) | `Id` del requerimiento que lo autorizó [CAS] |
| 2 | `Cuit` | `s:long` 1..1 | (ídem) | CUIT emisor |
| 3-7 | `Tipo_doc`, `Nro_doc`, `Tipo_cbte`, `Punto_vta`, `Cbte_nro` | `short`, `long`, `short`, `int`, `long`, 1..1 | S | Como en el request de `BFEAuthorize` |
| 8-17 | `Imp_total`, `Imp_tot_conc`, `Imp_neto`, `Impto_liq`, `Impto_liq_rni`, `Imp_op_ex`, `Imp_perc`, `Imp_iibb`, `Imp_perc_mun`, `Imp_internos` | `s:double` 1..1 | S (sin `Imp_iibb` ni `Imp_perc_mun` en la tabla) | Normalizados (`1754.5`) [CAS] |
| 18 | `Imp_moneda_Id` | `s:string` 0..1 | S ("double", errata) | |
| 19 | `Imp_moneda_ctz` | `s:double` **1..1** | S | En el request es 0..1; acá siempre viene |
| 20 | `Fecha_cbte_orig` | `s:string` 0..1 | N | "Fecha de comprobante ingreso (yyyymmdd)", la que envió el cliente |
| 21 | `Fecha_cbte_cae` | `s:string` 0..1 | S | "Fecha de comprobante otorgado en caso de omitirla en la presentación (yyyymmdd)". En [CAS] es igual a `Fecha_cbte_orig` |
| 22 | `Fecha_vto_pago` | `s:string` 0..1 | N | "Solo para Factura de Crédito MiPyme" |
| 23 | `Fch_venc_Cae` | `s:string` 0..1 | **no figura** | Vencimiento del CAE [CAS]. El manual lista en su lugar un `Fecha_cae` "Fecha de autorización (yyyymmdd)" que **no existe** en el WSDL |
| 24 | `Cae` | `s:string` 0..1 | (XML) | |
| 25 | `Resultado` | `s:string` 0..1 | (XML) | `A` [CAS] |
| 26 | `Obs` | `s:string` 0..1 | (XML) | `" "` o código (se vio `13`) [CAS] |
| 27 | `CondicionIVAReceptorId` | `s:int` 0..1 | N | Agregado en V3.0 |
| 28 | `CanMisMonExt` | `s:string` 0..1 | N | Agregado en V3.0 |
| 29 | `Opcionales` | `ArrayOfOpcional` 0..1 | N | |
| 30 | `Items` | `ArrayOfItem` 0..1 | S | Un `Pro_codigo_sec` vacío vuelve como `<Pro_codigo_sec />` |
| 31 | `CbtesAsoc` | `ArrayOfCbteAsoc` 0..1 | N | |

El manual lista además un campo **`Zona`** (pág. 24) que el WSDL **no** devuelve, y no lista `Reproceso` (que tampoco
existe en `ClsBFEGetCMPR`).

**Error**: `1020` "Comprobante inexistente" [MAN30 pág. 25]. **NO VERIFICADO**: si en ese caso viene `BFEResultGet`
(con ceros, como `BFEAuthorize`) o no viene.

**Ejemplo real** [CAS, `test_consulta.yaml`]:

```xml
<x:BFEGetCMP><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20267565393</x:Cuit></x:Auth><x:Cmp><x:Tipo_cbte>1</x:Tipo_cbte><x:Punto_vta>5</x:Punto_vta><x:Cbte_nro>2581</x:Cbte_nro></x:Cmp></x:BFEGetCMP>
```
```xml
<BFEGetCMPResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEGetCMPResult><BFEResultGet><Id>993456789012387</Id><Cuit>20267565393</Cuit><Tipo_doc>80</Tipo_doc><Nro_doc>20888888883</Nro_doc><Tipo_cbte>1</Tipo_cbte><Punto_vta>5</Punto_vta><Cbte_nro>2581</Cbte_nro><Imp_total>1754.5</Imp_total><Imp_tot_conc>0</Imp_tot_conc><Imp_neto>1450</Imp_neto><Impto_liq>304.5</Impto_liq><Impto_liq_rni>0</Impto_liq_rni><Imp_op_ex>0</Imp_op_ex><Imp_perc>0</Imp_perc><Imp_iibb>0</Imp_iibb><Imp_perc_mun>0</Imp_perc_mun><Imp_internos>0</Imp_internos><Imp_moneda_Id>PES</Imp_moneda_Id><Imp_moneda_ctz>1</Imp_moneda_ctz><Fecha_cbte_orig>20210621</Fecha_cbte_orig><Fecha_cbte_cae>20210621</Fecha_cbte_cae><Fch_venc_Cae>20210701</Fch_venc_Cae><Cae>71253948358918</Cae><Resultado>A</Resultado><Obs> </Obs><Items><Item><Pro_codigo_ncm>2101.11.10</Pro_codigo_ncm><Pro_codigo_sec /><Pro_ds>Cafe</Pro_ds><Pro_qty>10</Pro_qty><Pro_umed>5</Pro_umed><Pro_precio_uni>150</Pro_precio_uni><Imp_bonif>50</Imp_bonif><Imp_total>1754.5</Imp_total><Iva_id>5</Iva_id></Item></Items></BFEResultGet><BFEErr><ErrCode>0</ErrCode><ErrMsg>OK</ErrMsg></BFEErr><BFEEvents><EventCode>0</EventCode><EventMsg /></BFEEvents></BFEGetCMPResult></BFEGetCMPResponse>
```

En 2021 la consulta **no** devolvía `CondicionIVAReceptorId` ni `CanMisMonExt` (no existían). Con nulos se omiten.

### 3 a 10. Recuperadores de parámetros sin argumentos

`BFEGetPARAM_Tipo_doc`, `BFEGetPARAM_Tipo_IVA`, `BFEGetPARAM_Tipo_Opc`, `BFEGetPARAM_Zonas`, `BFEGetPARAM_Tipo_Cbte`,
`BFEGetPARAM_UMed`, `BFEGetPARAM_NCM` y `BFEGetPARAM_MON`. Solo reciben el bloque de autenticación. Fíjense bien en
`Auth` contra `auth`. Responden `{Op}Result` = `BFEResultGet` (lista, 0..1) + `BFEErr` + `BFEEvents`. Cada registro de
la lista es `nillable` y va 0..unbounded [WSDL].

| # | Operación | Elem. auth | Tipo del resultado → lista → registro | Campos del registro (XSD, todos `s:string` 0..1 salvo indicación) | Propósito (WSDL / manual) | Manual |
|---:|---|---|---|---|---|---|
| 3 | `BFEGetPARAM_Tipo_doc` | `auth` | `BFEResponse_Tipo_doc` → `ArrayOfClsBFEResponse_Tipo_doc` → `ClsBFEResponse_Tipo_doc` | `Doc_Id` **`s:short` 1..1**, `Doc_Ds`, `Doc_vig_desde`, `Doc_vig_hasta` | WSDL: "Recupera el listado de los tipos de comprobante..." (errata, son tipos de **documento**) | **Sin sección** |
| 4 | `BFEGetPARAM_Tipo_IVA` | `auth` | `BFEResponse_Tipo_IVA` → `ArrayOfClsBFEResponse_Tipo_IVA` → `ClsBFEResponse_Tipo_IVA` | `IVA_Id` **`s:short` 1..1**, `IVA_Ds`, `IVA_vig_desde`, `IVA_vig_hasta` | WSDL: "Recupera el listado de las alicuotas de IVA..." | §2.6, pág. 30-32. Llama a la operación `BFEGetPARAM_Tipo_Iva` y a los campos `Iva_Id`/`Iva_Ds`/`Iva_vig_*` (con `Iva_Id` string en el XML y Short en la tabla). El ejemplo de respuesta está rotulado `BFEGetPARAM_Tipo_CbteResponse`. **Manda el WSDL**: el nombre con `Iva` da HTTP 500 [VIVO] |
| 5 | `BFEGetPARAM_Tipo_Opc` | `auth` | `BFEResponse_Opc` → `ArrayOfClsBFEResponse_Opc` → `ClsBFEResponse_Opc` | `Opc_Id` (string), `Opc_Ds`, `Opc_vig_desde`, `Opc_vig_hasta` | "Retorna el total de opcionales válidos" (pág. 34) | §2.8, pág. 34-35. `Opc_Id` "short" en el XML, String en la tabla; respuesta con namespace `…dif.bfe/` (errata) |
| 6 | `BFEGetPARAM_Zonas` | `auth` | `BFEResponse_Zon` → `ArrayOfClsBFEResponse_Zon` → `ClsBFEResponse_Zon` | `Zon_Id` **`s:short` 1..1**, `Zon_Ds`, `Zon_vig_desde`, `Zon_vig_hasta` | "Retorna el total de zonas válidas" (pág. 32). El WSDL copia la descripción de IVA (errata) | §2.7, pág. 32-34. `Zon_Id` string en el XML, Int en la tabla |
| 7 | `BFEGetPARAM_Tipo_Cbte` | `auth` | `BFEResponse_Tipo_Cbte` → `ArrayOfClsBFEResponse_Tipo_Cbte` → `ClsBFEResponse_Tipo_Cbte` | `Cbte_Id` **`s:short` 1..1**, `Cbte_Ds`, `Cbte_vig_desde`, `Cbte_vig_hasta` | "Retorna el universo de tipos de comprobante válidos" (pág. 29) | §2.5, pág. 28-30 |
| 8 | `BFEGetPARAM_UMed` | `auth` | `BFEResponse_Umed` → `ArrayOfClsBFEResponse_UMed` → `ClsBFEResponse_UMed` | `Umed_Id` **`s:short` 1..1**, `Umed_Ds`, `Umed_vig_desde`, `Umed_vig_hasta` | WSDL: "Recupera el listado de las unidades de medida..." | **Sin sección** (citado como `BFEGetPARAM_Umed`, pág. 14) |
| 9 | `BFEGetPARAM_NCM` | **`Auth`** | `BFEResponse_NCM` → `ArrayOfClsBFEResponse_NCM` → `ClsBFEResponse_NCM` | `NCM_Codigo`, `NCM_Ds`, `NCM_Nota`, `NCM_vig_desde`, `NCM_vig_hasta` | "Retorna el listado completo de código de productos autorizados" (pág. 27) | §2.4, pág. 27-29 |
| 10 | `BFEGetPARAM_MON` | `auth` | `BFEResponse_Mon` → `ArrayOfClsBFEResponse_Mon` → `ClsBFEResponse_Mon` | `Mon_Id`, `Mon_Ds`, `Mon_vig_desde`, `Mon_vig_hasta` | "Retorna el total de monedas válidas" (pág. 26) | §2.3, pág. 25-27 |

En las tablas del manual, `*_vig_hasta` es N y los demás S. Las fechas van en `yyyymmdd`. Una fecha "hasta" vacía
llega como el string `NULL`, y lo mismo pasa con `NCM_Ds` [CAS]. Errores: 1000 y 1001 (pág. 27, 29, 30, 32, 33-34, 35).
Valores en Tablas y datos.

Ejemplo real [CAS, `test_parametros.yaml`, recortado]:

```xml
<x:BFEGetPARAM_Tipo_Cbte><x:auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20267565393</x:Cuit></x:auth></x:BFEGetPARAM_Tipo_Cbte>
```
```xml
<BFEGetPARAM_Tipo_CbteResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEGetPARAM_Tipo_CbteResult><BFEResultGet><ClsBFEResponse_Tipo_Cbte><Cbte_Id>1</Cbte_Id><Cbte_Ds>Factura A
</Cbte_Ds><Cbte_vig_desde>20090620</Cbte_vig_desde><Cbte_vig_hasta>NULL</Cbte_vig_hasta></ClsBFEResponse_Tipo_Cbte>…</BFEResultGet><BFEErr><ErrCode>0</ErrCode><ErrMsg>OK</ErrMsg></BFEErr><BFEEvents><EventCode>0</EventCode><EventMsg>Ok</EventMsg></BFEEvents></BFEGetPARAM_Tipo_CbteResult></BFEGetPARAM_Tipo_CbteResponse>
```

Detalle literal: en 2021 `Cbte_Ds` de "Factura A" y "Factura B" traía un **salto de línea** al final [CAS].

### 11. BFEGetLast_CMP

**Propósito.** "Recupera el ultimos comprobante autorizado" [WSDL]. Sin sección en el manual.

**Request**: un único hijo `Auth` de tipo **`ClsBFE_LastCMP`** [WSDL]:

| Campo | XSD | Descripción |
|---|---|---|
| `Token` | `s:string` 0..1 | Token del TA |
| `Sign` | `s:string` 0..1 | Firma del TA |
| `Cuit` | `s:long` 1..1 | CUIT emisora |
| `Pto_venta` | `s:int` 1..1 | Punto de venta. Ojo: se llama `Pto_venta`, no `Punto_vta` |
| `Tipo_cbte` | `s:short` 1..1 | Tipo de comprobante |

**Response** `BFEGetLast_CMPResult` (`BFEResponseLast_CMP`): **`BFEResult_LastCMP`** (`ClsBFE_LastCMP_Response` 0..1),
`BFEErr` y `BFEEvents`. `ClsBFE_LastCMP_Response`: `Cbte_nro` `s:long` 1..1 y `Cbte_fecha` `s:string` 0..1 (`yyyymmdd`,
fecha del último comprobante) [WSDL; CAS].

**Ejemplo real** [CAS, `test_autorizar.yaml`]:

```xml
<x:BFEGetLast_CMP><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20267565393</x:Cuit><x:Pto_venta>5</x:Pto_venta><x:Tipo_cbte>1</x:Tipo_cbte></x:Auth></x:BFEGetLast_CMP>
```
```xml
<BFEGetLast_CMPResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEGetLast_CMPResult><BFEResult_LastCMP><Cbte_nro>2580</Cbte_nro><Cbte_fecha>20210620</Cbte_fecha></BFEResult_LastCMP><BFEErr><ErrCode>0</ErrCode><ErrMsg>OK</ErrMsg></BFEErr><BFEEvents><EventCode>0</EventCode><EventMsg>Ok</EventMsg></BFEEvents></BFEGetLast_CMPResult></BFEGetLast_CMPResponse>
```

Después de autorizar el 2581, la misma consulta devolvió `2581` / `20210621` [CAS, `test_consulta.yaml`]. Con token
inválido: solo `BFEErr` 1000 y `BFEEvents` [VIVO, `bfev1-lastcmp-badtoken`]. **NO VERIFICADO**: qué devuelve si nunca se
emitió ese tipo en ese punto de venta (wsfev1 devuelve 0) y qué error da un punto de venta inexistente.

### 12. BFEDummy

**Propósito.** "Metodo dummy para verificacion de funcionamiento" [WSDL]. Sin sección en el manual. **No lleva
autenticación** y su resultado **no** tiene `BFEErr` ni `BFEEvents`.

Request: `<x:BFEDummy/>` (elemento vacío). Response `BFEDummyResult` (`DummyResponse`): `AppServer`, `DbServer` y
`AuthServer`, `s:string` 0..1 [WSDL].

Ejemplo real [VIVO, 2026-10-02, homologación]:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.bfev1/"><fecha>2026-10-02T15:07:36.7130486-03:00</fecha><id>3.0.0.1</id></FEHeaderInfo></soap:Header><soap:Body><BFEDummyResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></BFEDummyResult></BFEDummyResponse></soap:Body></soap:Envelope>
```

También responde por HTTP GET y POST en `…/service.asmx/BFEDummy` (ver Contrato). **NO VERIFICADO**: los valores
cuando un componente no está `OK`.

### 13. BFEGetLast_ID

**Propósito.** "Recupera el ultimo ID y su fecha" [WSDL]. No hay ninguna fecha en la respuesta: es una errata del
WSDL. Sin sección en el manual.

**Request**: `Auth` (`ClsBFEAuthRequest` 0..1). **Response** `BFEGetLast_IDResult` (`BFEResponse_LastID`):
`BFEResultGet` (`ClsBFEResponse_LastID` 0..1, con `Id` `s:long` 1..1), `BFEErr` y `BFEEvents` [WSDL].

**Ejemplo real** [CAS, `test_autorizar.yaml`]:

```xml
<x:BFEGetLast_ID><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20267565393</x:Cuit></x:Auth></x:BFEGetLast_ID>
```
```xml
<BFEGetLast_IDResponse xmlns="http://ar.gov.afip.dif.bfev1/"><BFEGetLast_IDResult><BFEResultGet><Id>993456789012386</Id></BFEResultGet><BFEErr><ErrCode>0</ErrCode><ErrMsg>OK</ErrMsg></BFEErr><BFEEvents><EventCode>0</EventCode><EventMsg>Ok</EventMsg></BFEEvents></BFEGetLast_IDResult></BFEGetLast_IDResponse>
```

Semántica observada: ver Comportamiento a simular. **NO VERIFICADO**: el valor para un CUIT que nunca autorizó.

### 14. BFEGetCotizacion

**Propósito.** "Recupera la cotizacion de la moneda consultada y su fecha" [WSDL]. "Retorna el valor de la cotización de
la moneda consultada a la fecha indicada (si no especifico fecha se tomara la del día actual)" [MAN30 pág. 38].
Agregada en V3.0.

**Request** [WSDL; MAN30 pág. 37-38]:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `Auth` | `ClsBFEAuthRequest` 0..1 | S | |
| `MonId` | `s:string` 0..1 | S | Moneda (`BFEGetPARAM_MON`). Inválida o vacía → 4965 |
| `FchCotiz` | `s:string` 0..1 | N | `YYYYMMDD`. "de no informarse, se tomara la fecha del día como default". Mal formada → 4966 |

**Response** `BFEGetCotizacionResult` (`BFEResponse_Cotizacion`): `BFEResultGet` (`Cotizacion` 0..1), `BFEErr` y
`BFEEvents`. `Cotizacion` [WSDL; MAN30 pág. 38-39]:

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `MonId` | `s:string` 0..1 | S | Moneda consultada |
| `MonCotiz` | `s:double` 1..1 | S | "Valor de la cotización registrada en el organismo" |
| `FchCotiz` | `s:string` 0..1 | S, String(C8) | "Fecha a la cual fue consultada la cotización de la moneda" |

Errores (pág. 39): 1000, 1001, **4964** sin resultados para esa moneda y fecha, **4965** moneda inválida y **4966** fecha
inválida.

Ejemplo del manual (pág. 38), respuesta:
`<BFEResultGet><MonId>string</MonId><MonCotiz>double</MonCotiz><FchCotiz>string</FchCotiz></BFEResultGet><BFEErr><ErrCode>0</ErrCode><ErrMsg>OK</ErrMsg></BFEErr>`.
El ejemplo de request está roto: abre `<MonId>string<MonId>` y cierra con `</BFEGetPARAM_CondicionIvaReceptor>`
(errata). Request derivado del WSDL:

```xml
<x:BFEGetCotizacion><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:Auth><x:MonId>DOL</x:MonId><x:FchCotiz>20261001</x:FchCotiz></x:BFEGetCotizacion>
```

No hay ninguna respuesta real con datos: **NO VERIFICADO** el formato de `MonCotiz` y si `FchCotiz` devuelve la fecha
pedida o la del día hábil efectivamente usado.

### 15. BFEGetPARAM_CondicionIvaReceptor

**Propósito.** "Recupera la condicion frente al IVA del receptor (para una clase de comprobante determinada o para todos
si no se especifica)" [WSDL]. "Retorna el listado de identificadores de IVA del receptor" [MAN30 pág. 36]. Agregada en
V3.0.

**Request** [WSDL; MAN30 pág. 35-36]: `auth` (minúscula, `ClsBFEAuthRequest` 0..1) y `ClaseCmp` `s:string` 0..1, Man. N:
"Clase de comprobante. Valor opcional. De informarlo debe ser A o B o C". La validación dice otra cosa: "de ingresar un
valor solo puede ser A o B" (**4967**; 4963 en V3.0, pág. 37). La clase C no aparece en ninguna tabla de este
servicio; **NO VERIFICADO** si `C` se acepta.

**Response** `BFEGetPARAM_CondicionIvaReceptorResult` (`BFEResponse_CondicionIvaReceptor`): `BFEResultGet`
(`ArrayOfClsBFEResponse_CondicionIvaReceptor` → `ClsBFEResponse_CondicionIvaReceptor` 0..unbounded, `nillable`),
`BFEErr` y `BFEEvents`. Registro [WSDL; MAN30 pág. 37]:

| Campo | XSD | Tipo man. | Man. | Descripción |
|---|---|---|---|---|
| `Id` | `s:int` 1..1 | Short(N2) | S | "Código de IVA" (condición) |
| `Desc` | `s:string` 0..1 | String(C250) | S | Descripción |
| `Cmp_Clase` | `s:string` 0..1 | String(C1) | S | "Clase de Comprobante" |

Valores esperados: el anexo 3.1 (ver Tablas y datos). **NO VERIFICADO** el listado real y cómo se representa una
condición válida para A y para B a la vez (en el anexo ninguna lo es).

### Discrepancias manual / WSDL (y cuál se toma)

| # | Manual | WSDL / real | Se toma |
|---:|---|---|---|
| 1 | Operación `BFEGetPARAM_Tipo_Iva`, campos `Iva_Id`/`Iva_Ds` | `BFEGetPARAM_Tipo_IVA`, `IVA_Id`/`IVA_Ds`. El nombre del manual da HTTP 500 [VIVO] | WSDL |
| 2 | `<Auth>` en `_MON`, `_Tipo_Cbte`, `_Tipo_Iva`, `_CondicionIvaReceptor`; `<auth>…</Auth>` en `_Zonas` | `auth` en minúscula en todos los recuperadores salvo `_NCM` (`Auth`) | WSDL (el nombre equivocado = sin autenticación) |
| 3 | `BFEErr` con `errcode`/`errmsg` o `ErrCode`/`Errmsg` | `ErrCode`/`ErrMsg` | WSDL |
| 4 | `BFEGetCMP` devuelve `Zona` y `Fecha_cae` | No existen; existe `Fch_venc_Cae` | WSDL |
| 5 | `Imp_moneda_Id` "Double" | `s:string` | WSDL |
| 6 | `Tipo_doc`, `Tipo_cbte`, `Iva_id` "Int" | `s:short` | WSDL |
| 7 | `Cuit` "string" en el `Auth` de varios recuperadores | `s:long` | WSDL |
| 8 | `Opc_Id` "short"; `Zon_Id` e `Iva_Id` "string" en ejemplos | `Opc_Id` string; `Zon_Id` e `IVA_Id` short | WSDL |
| 9 | `Fecha_vto_pago` descripto como "Fecha de comprobante" | Vencimiento de pago FCE (pág. 25) | Contenido |
| 10 | Tabla de `Cmp` sin `Imp_iibb` ni `Imp_perc_mun` | Están, 1..1 | WSDL |
| 11 | Respuesta de `_Tipo_Opc` con namespace `http://ar.gov.afip.dif.bfe/` | `http://ar.gov.afip.dif.bfev1/` | WSDL |
| 12 | Respuesta de `_Tipo_Iva` rotulada `BFEGetPARAM_Tipo_CbteResponse` | `BFEGetPARAM_Tipo_IVAResponse` | WSDL |
| 13 | `ClaseCmp` "A o B o C" | Validación 4967: "solo puede ser A o B" | **NO VERIFICADO** |
| 14 | 5 operaciones sin documentar | Están en el WSDL y responden | WSDL + [CAS] |
| 15 | `FEHeaderInfo` siempre con `ambiente` | Homologación 2026 sin `ambiente` | Real (configurable) |
| 16 | WSDL: "Recupera el ultimo ID y su fecha" | Solo `Id` | WSDL (estructura) |
| 17 | `ErrMsg` = texto de la tabla | El servicio usa otros textos (4900, 4954) [CAS] | Real donde se conoce; si no, el del manual |
| 18 | 4946 remite a `FEParamGetTiposOpcional()` (método de wsfev1) | En este servicio es `BFEGetPARAM_Tipo_Opc` | Errata del manual |
| 19 | 4954 menciona el tipo 211 | 211 no está en `BFEGetPARAM_Tipo_Cbte` [CAS 2021] | **NO VERIFICADO** |

## Validaciones y errores

### Cómo viajan

- **Errores de negocio y de autenticación**: HTTP 200, un único `BFEErr{ErrCode, ErrMsg}` dentro del `{Op}Result`.
  En `BFEAuthorize` con error de negocio viene además `BFEResultAuth` con `Id` 0 y `Cuit` 0 [CAS]. Con error de token,
  solo `BFEErr` y `BFEEvents` [VIVO].
- **Éxito**: `BFEErr` con `ErrCode` `0` y `ErrMsg` `OK` [CAS].
- **Observaciones**: códigos en `BFEResultAuth/Obs` (string). Sin observaciones viene `" "` [CAS]. **NO VERIFICADO**
  cómo se separan varias.
- **Faults (HTTP 500)**: solo por problemas de SOAP o de deserialización (ver Contrato). El manual no documenta
  ninguno.
- **Infraestructura**: "Para errores internos de infraestructura, los errores se devuelven en la misma estructura
  (BFEerror)": 500, 501, 502 [MAN30 pág. 9].
- **1014 es el comodín**: "Los mensajes de error que aún no están contemplados salen por código 1014 incluyendo un texto
  que explica la causa exacta del error" [MAN30 pág. 18]. Lo usan muchas validaciones distintas de las secciones 2.1.4
  a 2.1.6. ArcaSim debería devolver 1014 + texto propio para toda regla sin código (por ejemplo, la correlatividad).
- **Textos reales ≠ tabla del manual**: los dos `ErrMsg` reales conocidos (4900 y 4954, ver §1) no coinciden con la
  tabla. La tabla describe la **condición**. El JSON guarda el texto del manual. Para 4900 y 4954 conviene emitir el texto
  real.

### Inconsistencias del manual sobre códigos

| # | Inconsistencia | Fuente | Qué hacer |
|---:|---|---|---|
| 1 | **1020** significa dos cosas: Opcional `Id`=2 de Promoción Industrial (`BFEAuthorize`) y "Comprobante inexistente" (`BFEGetCMP`) | pág. 18 y 25 | Resolver por operación (el JSON lo hace) |
| 2 | **4963** significa dos cosas en V3.0: falta `CondicionIVAReceptorId` (`BFEAuthorize`) y clase inválida (`BFEGetPARAM_CondicionIvaReceptor`) | pág. 22 y 37 | V3.2 lo corrige: la segunda pasa a **4967** |
| 3 | Banda del tipo de cambio (1014): la tabla dice "inferior al 2% ni superior en un 400%" (pág. 18). El historial dice 20%/100% (v2.2 Beta 1) y después 20%/200% (v2.12, pág. 3). A eso se suma el 4960: "no podra superar en 1 a la cotización oficial" | pág. 2, 3, 18, 22 | **NO VERIFICADO**. Implementar la banda de la tabla y el 4960 como reglas configurables |
| 4 | El historial v2.2 da de baja 4899, 4903 y 4904, pero el **4903** sigue en la tabla vigente (moneda de ND/NC FCE) | pág. 2, 20 | Se mantiene el 4903 de la tabla |
| 5 | Huecos de numeración sin texto: 4885, 4899, 4904 y 4934-4943 | — | No existen para este servicio |
| 6 | Observaciones 15-18 y 22: el manual las presenta bajo "Obs [...] motivo de rechazo según tabla de motivos [...] Para Facturas de Crédito las validaciones especificas son". El historial da 21-26 como "a modo de observación" | pág. 2-4, 15 | Tratarlas como observación. Si alguna rechaza: **NO VERIFICADO** |
| 7 | 4930 está en "Validaciones NO excluyentes" (pág. 23): no rechaza. El manual no dice por dónde se informa | pág. 23 | Emitirla en `Obs` [INFERIDO] |
| 8 | 1032: "debe enviarse mayor a 0 y menor a 99998" (falta el nombre del campo; en wsbfe dice `<CbteAsoc><PtoVta>`) | pág. 18 | Es `CbteAsoc/Punto_vta` |
| 9 | Obs 26 en desuso (V3.2), pero sigue impresa | [MAN32] pág. 4 y 16 | No emitir 26 en modo RG 5616; emitir 4963 |

### Tabla completa de códigos del manual

Fuente de los textos: tablas de [MAN32] cotejadas con [MAN30] (idénticas). "Página" = V3.0 / V3.2. "Efecto" según la
forma vista en [CAS] y [VIVO]. Los mismos datos están en `wsbfev1-codigos.json` (las páginas del JSON son las de V3.0,
salvo el 4967, que es de V3.2, pág. 37).

| código | texto / condición (literal del manual) | efecto | dónde | página V3.0 / V3.2 |
|---|---|---|---|---|
| 500 | Error interno de aplicación. | error en `BFEErr` | * | 9 / 9 |
| 501 | Error interno de base de datos. | error en `BFEErr` | * | 9 / 9 |
| 502 | Error interno – Autorizador - Transacción Activa | error en `BFEErr` | * | 9 / 9 |
| 1000 | Usuario no autorizado a realizar esta operación | rechaza: solo `BFEErr` + `BFEEvents`, sin elemento de resultado | * · `Auth` | 17 / 16 (y en cada recuperador) |
| 1001 | Cuit solicitante no se encuentra entre sus representados | rechaza: solo `BFEErr` + `BFEEvents`, sin elemento de resultado | * · `Auth/Cuit` | 17 / 16 (y en cada recuperador) |
| 1014 | Tipo de dato y longitud de cada campo | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize | 16 / 16 |
| 1014 | Identificador del requerimiento sea mayor que 0. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Id` | 17 / 16 |
| 1014 | Campo punto_vta se encuentre entre 1 y 99998 y que sea único para el requerimiento. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Punto_vta` | 17 / 16 |
| 1014 | Tipo de comprobante inválido. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Tipo_cbte` | 17 / 16 |
| 1014 | Campo cbte_nro esté entre 1 y 99999999. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Cbte_nro` | 17 / 16 |
| 1014 | El tipo de documento debe ser igual a 80 (CUIT) en comprobantes tipo A. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Tipo_doc` | 17 / 17 |
| 1014 | No es una fecha valida. Debe ser numérico de 8 con formato (yyyymmdd). | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_cbte` | 17 / 17 |
| 1014 | No podrá exceder el mes de la fecha de envío del pedido de autorización. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_cbte` | 17 / 17 |
| 1014 | La fecha debe estar incluida en el periodo +- 5 días de la fecha de presentación. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_cbte` | 17 / 17 |
| 1014 | Se valida que la suma de importes de los ítems sea menor igual a los importes totales del comprobante. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Items` | 17 / 17 |
| 1014 | Se valida que el importe de operaciones exentas sea mayor a 0 en los casos donde exista alguna ítem de factura con Iva exento | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_op_ex` | 17 / 17 |
| 1014 | El tipo de cambio no podrá ser inferior al 2% ni superior en un 400% del que suministra ARCA como orientativo de acuerdo a la cotización oficial. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_moneda_ctz` | 18 / 17 |
| 1014 | Valor inválido en campo (a este código se le agregará una descripción detallada del origen del error (nombre de campo y causa)) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize | 18 / 17 |
| 1015 | Opcionales ->Opcional : de informar &lt;Opcionales> debe informar de forma completa la estructura &lt;Opcionales>&lt;Opcional>&lt;Id> | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales` | 18 / 17 |
| 1016 | El valor ingresado en &lt;Id> debe ser alguno permitido. Consultar método BFEGetPARAM_Tipo_Opc. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 18 / 17 |
| 1017 | El campo &lt;Id> en &lt;Opcionales> es obligatorio y no debe repetirse. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 18 / 17 |
| 1018 | El campo &lt;Valor> en Opcionales es obligatorio | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 18 / 17 |
| 1019 | &lt;Opcionales>&lt;Id>&lt;Valor>. Si selecciona Id = 2 el valor ingresado debe ser un numérico de 8 (ocho) dígitos mayor o igual a 0 (cero). | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 18 / 17 |
| 1020 | Si Id = 2 y el comprobante corresponde a una actividad alcanzada por el beneficio de Promoción Industrial en el campo &lt;Valor> se deberá informar el número identificatorio del proyecto (el mismo deberá corresponder a la cuit emisora del comprobante), si no corresponde a una actividad alcanzada por el beneficio el campo &lt;Valor> deberá ser 0 (cero). | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 18 / 17 |
| 1030 | Si envía CbtesAsoc, CbteAsoc es obligatorio y no puede estar vacío | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc` | 18 / 17 |
| 1031 | De enviarse el tag CbteAsoc debe enviarse &lt;CbteAsoc>&lt;Tipo>mayor a 0 | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 18 / 18 |
| 1032 | De enviarse el tag CbteAsoc debe enviarse mayor a 0 y menor a 99998. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Punto_vta` | 18 / 18 |
| 1033 | De enviarse el tag CbteAsoc debe enviarse &lt;CbteAsoc>&lt;Nro> > a 0 y &lt; a 99999999. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cbte_nro` | 18 / 18 |
| 1034 | De enviarse el tag CbteAsoc, los comprobantes no deben repetirse. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc` | 18 / 18 |
| 1035 | De enviarse el tag &lt;CbtesAsoc>, entonces el campo tipo de comprobante &lt;Cmp>&lt;Tipo_cbte> a autorizar tiene que ser 01, 02, 03, 06, 07, 08, 91, 201, 202, 203, 206, 207, 208 | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Tipo_cbte` | 18 / 18 |
| 1036 | Para &lt;Cmp>&lt;Tipo_cbte>01 o 06 solo puede asociarse el tipo de comprobante &lt;CbtesAsoc>&lt;Tipo_cbte> 91. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 18 / 18 |
| 1037 | Para &lt;Cmp>&lt;Tipo_cbte> 02 o 03 pueden asociarse los tipos de comprobante &lt;CbtesAsoc>&lt;Tipo_cbte> 01, 02, 03, 91. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 18 / 18 |
| 1038 | Para &lt;Cmp>&lt;Tipo_cbte> 07 u 08 pueden asociarse los tipos de comprobante &lt;CbtesAsoc>&lt;Tipo_cbte> 06, 07, 08, 91. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 18 / 18 |
| 1039 | Si el punto de venta del comprobante asociado (CbtesAsoc.Punto_vta) es electrónico y del tipo Bonos, el número de comprobante debe obrar en las bases del organismo para el punto de venta y tipo de comprobante informado. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cbte_nro` | 18 / 18 |
| 4880 | Si el punto de venta del comprobante asociado (campo &lt; Punto_vta > de &lt;CbteAsoc>) es electrónico, el número de comprobante debe obrar en las bases del organismo para el punto de venta y tipo de comprobante informado. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cbte_nro` | 18 / 18 |
| 4881 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) y corresponde a un comprobante de débito o crédito, tener en cuenta que: - sí y el comprobante asociado se encuentra rechazado por el comprador hay que informar el código de anulación correspondiente sobre el campo "Adicionales por RG", códigos 22 - Anulación. Valor “S” (&lt;CbteAsoc>&lt;Tipo_cbte>&lt;Punto_vta>&lt;Cbte_nro>&lt;Cuit>) - sí y el comprobante asociado se encuentra aceptado por el comprador hay que informar el código de no anulación correspondiente sobre el campo "Adicionales por RG", códigos 22 - Anulación. Valor “N” (&lt;CbteAsoc>&lt;Tipo_cbte>&lt;Punto_vta>&lt;Cbte_nro>&lt;Cuit>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 18 / 18 |
| 4882 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, el comprobante debe existir autorizado en las bases de esta Administración con la misma fecha informada en el asociado (&lt;CbteAsoc>&lt;Tipo_cbte>&lt;Punto_vta>&lt;Cbte_nro>&lt;Cuit>&lt;Fecha_cbte>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Fecha_cbte` | 19 / 18 |
| 4883 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es débito o crédito, deben coincidir emisores y receptores. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cuit` | 19 / 18 |
| 4884 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), y es crédito y coinciden emisores y receptores el monto del comprobante a autorizar no puede ser mayor o igual al saldo actual de la cuenta corriente. Ver micro sitio factura de crédito | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_total` | 19 / 18 |
| 4886 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) y corresponde a un comprobante de débito o crédito, es obligatorio informar comprobantes asociados. (&lt;BFEAuthorize>&lt;Cmp>&lt;Tipo_cbte>/ &lt;BFEAuthorize>&lt;Cmp> /&lt;CbtesAsoc>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc` | 19 / 18 |
| 4887 | Si informa Cuit en comprobantes asociados, no informar en blanco, el mismo debe ser un valor de 11 caracteres numéricos. Para comprobante del tipo MiPyMEs (FCE) del tipo débito o crédito es obligatorio informar el campo (campo &lt;CbteAsoc>&lt;Cuit>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cuit` | 19 / 18 |
| 4888 | De enviarse el tag &lt;CbtesAsoc>, para &lt;Tipo_cbte> comprobantes MiPyMEs (FCE) 201 o 206, solo puede asociarse comprobante 91. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 19 / 18 |
| 4889 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo A sin código de Anulación, siempre debe asociar 1 y solo 1 comprobante tipo factura A (201). No puede haber dos o más comprobantes tipo factura A (201) asociados a un comprobante a autorizar. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 19 / 18 |
| 4890 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo A, sin código de Anulación, solo puede asociar: Para comprobantes A, asociar 201 o 91 (&lt;BFEAuthorize>&lt;Cmp>&lt;Tipo_cbte >/ &lt;BFEAuthorize>&lt;Cmp>&lt;CbteAsoc>&lt;Tipo>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 19 / 19 |
| 4891 | Para comprobante de anulación, campo CbtesAsoc con tipo invalido. Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo A y el comprobante es de anulación, el campo CbtesAsoc debe contener uno de los siguientes valores: 201, 202, 203 (&lt;CbtesAsoc> / &lt;Tipo_cbte>). De forma complementaria se puede asociar el código 91. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 19 / 19 |
| 4892 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo B sin código de Anulación, siempre debe asociar 1 y solo 1 comprobante tipo factura B (206). No puede haber dos o más comprobantes tipo factura B (206) asociados a un comprobante a autorizar. De forma complementaria se puede asociar el código 91. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 19 / 19 |
| 4893 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo B, sin código de Anulación, solo puede asociar: Para comprobantes B, asociar 206 (&lt;BFEAuthorize>&lt;Cmp>&lt;Tipo_cbte >/ &lt;BFEAuthorize>&lt;Cmp>&lt;CbteAsoc>&lt;Tipo>) . De forma complementaria se puede asociar el código 91. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 19 / 19 |
| 4894 | Para comprobante de anulación, campo CbtesAsoc con tipo invalido. Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo B y el comprobante es de anulación, el campo CbtesAsoc debe contener uno de los siguientes valores: 206, 207, 208. &lt;CbtesAsoc> / &lt;Tipo_cbte> | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 19 / 19 |
| 4895 | De informar el campo Fecha del Comprobante Asociado &lt;Fecha_cbte>, la fecha del comprobante asociado tiene que ser igual o menor a la fecha del comprobante que se está autorizando | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Fecha_cbte` | 19 / 19 |
| 4896 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito, es obligatorio informar la fecha del comprobante asociado | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Fecha_cbte` | 19 / 19 |
| 4897 | Para comprobantes MiPyMEs (FCE), el campo &lt;Fecha_cbte> podrá estar comprendido en el rango N-5 y N+1 siendo N la fecha de envío del pedido de autorización. (&lt;Cbte_nro>/&lt;Fecha_cbte>/&lt;Cmp>/&lt;Fecha_cbte>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_cbte` | 19 / 19 |
| 4898 | Si informa fecha de comprobante &lt;Fecha_cbte> para comprobante del tipo MiPyMEs (FCE) con fecha superior a la fecha de envío de autorización, el mes de la fecha del comprobante &lt;Fecha_cbte> debe coincidir con el mes de la fecha de envío de autorización. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_cbte` | 20 / 19 |
| 4900 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Tipo 201 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) A / 206 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) B , es obligatorio informar el campo Fecha_vto_pago (&lt;Fecha_vto_pago>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_vto_pago` | 20 / 19 |
| 4901 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), la fecha de vencimiento de pago &lt;Fecha_vto_pago> debe ser posterior o igual a la fecha de emisión (CbteFch) o fecha de presentación (fecha actual), la que sea posterior (&lt;Fecha_vto_pago>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_vto_pago` | 20 / 19 |
| 4902 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el campo “fecha de vencimiento para el pago” &lt;Fecha_vto_pago> no debe informarse si NO es Factura de Crédito (Cbte_tipo 201 / 206). En el caso de ser Nota de Débito o Crédito, solo puede informarse si es de Anulación. (&lt;Fecha_vto_pago>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Fecha_vto_pago` | 20 / 19 |
| 4903 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Nota Débito o Nota Crédito, la moneda del comprobante a autorizar debe ser igual a la moneda del comprobante asociado o Pesos para ajuste en las diferencias de cambio (post aceptación/rechazo). | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_moneda_Id` | 20 / 19 |
| 4905 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Tipo 201 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) A / 206 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) B, es obligatorio informar &lt;Opcionales> | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales` | 20 / 19 |
| 4906 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2101 es un CBU numérico de 22 caracteres. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 20 / 19 |
| 4907 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2102 es un ALIAS alfanumérico de 6 a 20 caracteres. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 20 / 19 |
| 4908 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 22 es “S” o “N”: S = Es de Anulación N = No es de Anulación &lt;Opcionales>&lt;Id>&lt;Valor> | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 20 / 20 |
| 4909 | Opcionales. No informar identificadores de resoluciones distintas en un mismo comprobante. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 20 / 20 |
| 4910 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es obligatorio informar al menos uno de los siguientes códigos 2101, 2102, 22, 27. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 20 / 20 |
| 4911 | Si el tipo de comprobante que está autorizando es Factura (201, 206) del tipo MiPyMEs (FCE), informa opcionales, es obligatorio informar CBU. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 20 / 20 |
| 4912 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Factura (201 o 206), no informar Código de Anulación. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 20 / 20 |
| 4913 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa CBU o Alias, el mismo debe estar registrado en las bases de esta administración, vigente y pertenecer al emisor del comprobante. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 20 / 20 |
| 4914 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Debito (202, 207) o Crédito (203, 208) No informar CBU o ALIAS. Solo se permite informar Código de Anulación. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 20 / 20 |
| 4915 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Debito (202, 207) o Crédito (203, 208) informar Código de Anulación. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 20 / 20 |
| 4916 | Si el tipo de comprobante que está autorizando NO es MiPyMEs (FCE), no informar los códigos 2101, 2102, 22, 27. (&lt;Opcionales>&lt;Id>&lt;Valor>) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Id` | 21 / 20 |
| 4917 | Si informa el campo CbtesAsoc, el Cuit debe informarlo como numérico de 11 caracteres. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cuit` | 21 / 20 |
| 4918 | Para comprobantes MIPYMEs (FECRED), tipo cmp. N. Débito o N. Crédito A de ANULACION, no se puede informar más de una ND/NC A como Cmp. Asociado (202/203) . | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4919 | Para comprobantes MIPYMEs (FECRED), tipo Nota de Debito A (202) de ANULACION, no se puede informar Factura A (201) ni Nota de Debito A (202) como CMP. Asociado | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4920 | Para comprobantes MIPYMEs (FECRED), tipo Nota de Credito A (203) de ANULACION, no se puede informar una Nota de Credito A (203) como CMP. Asociado | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4921 | Para comprobantes MIPYMEs (FECRED), tipo cmp. N. Débito o N. Credito B de ANULACION, no se puede informar más de una ND/NC B como Cmp. Asociado (207/208) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4922 | Para comprobantes MIPYMEs (FECRED), tipo Nota de Debito B (207) de ANULACION, no se puede informar Factura B (206) ni Nota de Debito B (207) como CMP. Asociado | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4923 | Para comprobantes MIPYMEs (FECRED), tipo Nota de Credito B (208) de ANULACION, no se puede informar una Nota de Credito B (208) como CMP. Asociado | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4924 | Para comprobantes MIPYMEs (FECRED), tipo Nota de Debito/Credito A (202/203) No de ANULACION, debe informarse una y solo una Factura A (201) obligatoriamente. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4925 | Para comprobantes MIPYMEs (FECRED), tipo Nota de Debito/Credito B (207/208) No de ANULACION, debe informarse una y solo una Factura B (206) obligatoriamente. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 20 |
| 4926 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito el monto del comprobante a autorizar no puede ser mayor o igual al saldo actual de la cuenta corriente. Ver micrositio factura de crédito | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_total` | 21 / 21 |
| 4927 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito, el comprobante asociado debe estar aceptado o rechazado por el sistema de gestión de créditos. Ver micrositio factura de crédito | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc` | 21 / 21 |
| 4928 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito A, de anulación, solo se encuentra habilitado asociar un comprobante de crédito A . | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 21 |
| 4929 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), es crédito o débito B, de anulación, solo se encuentra habilitado asociar un comprobante de crédito B. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 21 / 21 |
| 4931 | Puede identificar una o varias Referencias Comerciales según corresponda. Informar bajo el código 23. Campo alfanumérico de 50 caracteres como máximo. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 21 / 21 |
| 4932 | Si informa opcionales con más de un identificador 23 – Referencia Comercial, no repetir el valor. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 21 / 21 |
| 4933 | El importe Total de la Factura de Crédito (código tipo comprobante 201, 206) debe ser mayor o igual al tope establecido para emisión de factura de crédito. Ver micrositio. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_total` | 21 / 21 |
| 4944 | Según la categorización de las CUITs emisora y receptora y el monto facturado debe realizar una factura de crédito electrónica MiPyMEs (FCE). Ver micrositio. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Tipo_cbte` | 21 / 21 |
| 4945 | El comprobante electrónico asociado se encuentra autorizado pero los receptores no coinciden. Si está autorizando un comprobante MiPyMEs (FCE), el receptor del comprobante asociado debe ser el mismo que el receptor del comprobante que está intentando autorizar. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Nro_doc` | 21 / 21 |
| 4946 | Para Notas de Debito / Crédito del tipo MiPMEs (FCE), es obligatorio informar Opcionales - Marca de Anulación (S/N). Ver método FEParamGetTiposOpcional() para mayor información. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales` | 21 / 21 |
| 4947 | Comprobante asociado no existe en los registros del organismo. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc` | 22 / 21 |
| 4948 | Para comprobante MiPyMEs (FCE), del tipo Nota de Crédito, el monto del comprobante a autorizar no puede ser mayor o igual al saldo actual de la cuenta corriente. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_total` | 22 / 21 |
| 4949 | Comprobante electrónico asociado autorizado. No es válida la marca de anulación para el estado actual del comprobante. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 22 / 21 |
| 4950 | Tipo de comprobante asociado invalido para comprobantes MiPyMEs (FCE) de anulación. Para comprobante MiPyMEs (FCE), del tipo Nota de Debito A con Anulación, solo informar 1 comprobante Asociado 203. Para comprobante MiPyMEs (FCE), del tipo Nota de Crédito A con Anulación, solo informar 1 comprobante Asociado 201 o 202. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 22 / 21 |
| 4951 | Tipo de comprobante asociado invalido para comprobantes MiPyMEs (FCE) de anulación. Para comprobante MiPyMEs (FCE), del tipo Nota de Debito B con Anulación, solo informar 1 comprobante Asociado 208. Para comprobante MiPyMEs (FCE), del tipo Nota de Crédito B con Anulación, solo informar 1 comprobante Asociado 206 o 207. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 22 / 21 |
| 4952 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el tipo de dato correcto para el código 27 es un alfanumérico de 3 caracteres. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 22 / 21 |
| 4953 | Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales y el código es 27, los valores posibles son: SCA = "TRANSFERENCIA AL SISTEMA DE CIRCULACION ABIERTA" ADC = "AGENTE DE DEPOSITO COLECTIVO" | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales/Opcional/Valor` | 22 / 21 |
| 4954 | Si el tipo de comprobante que está autorizando es Factura del tipo MiPyMEs (201, 206, 211), es obligatorio informar &lt;Opcionales> con id = 27. Los valores posibles son SCA o ADC. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Opcionales` | 22 / 21 |
| 4955 | Si el tipo de comprobante que está autorizando es Factura del tipo MiPyMEs (201, 202, 203, 206, 207, 208), el campo &lt;Cmp>.&lt;Imp_total> (Importe total de la operación) deber ser igual o mayor a 0 (cero). | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_total` | 22 / 22 |
| 4956 | Al momento de autorizar un comprobante del tipo débito o crédito, al asociar sus comprobantes, tener en cuenta que los puntos de venta deben pertenecer al mismo régimen de facturación. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Punto_vta` | 22 / 22 |
| 4957 | El campo Imp_moneda_ctz es obligatorio si no informa el campo CanMisMonExt con valor S o si la moneda del comprobante no tiene cotización en Banco Nación o el comprobante no es del tipo factura. El mismo debe ser mayor a 0. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_moneda_ctz` | 22 / 22 |
| 4958 | Si informa Imp_moneda_Id = PES, el campo CanMisMonExt NO debe informarse (o informarse con el valor N) | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CanMisMonExt` | 22 / 22 |
| 4959 | Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacío. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CanMisMonExt` | 22 / 22 |
| 4960 | Si informa el campo Imp_moneda_ctz, el mismo no podra superar en 1 a la cotización oficial. Ver Método BFEGetCotizacion. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/Imp_moneda_ctz` | 22 / 22 |
| 4961 | El campo Condición IVA receptor no es un valor permitido. Consular método BFEGetPARAM_CondicionIvaReceptor. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CondicionIVAReceptorId` | 22 / 22 |
| 4962 | El campo Condición IVA receptor no es valido para la clase de comprobante informado. Consular método BFEGetPARAM_CondicionIvaReceptor. | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CondicionIVAReceptorId` | 22 / 22 |
| 4963 | Campo Condición Frente al IVA del receptor es obligatorio conforme a lo reglamentado por la Resolución General N° 5616. Para mas información consular método BFEGetPARAM_CondicionIvaReceptor | rechaza: `BFEErr`, `BFEResultAuth` con `Id`/`Cuit` 0 y sin CAE | BFEAuthorize · `Cmp/CondicionIVAReceptorId` | 22 / 22 |
| 4930 | Si el tipo de comprobante que está autorizando es 1 – Factura A o 6 - Factura B, por la categorización de las cuits emisora y receptora, se debería realizar una factura de crédito electrónica. | observa (validación "no excluyente"); se informa en `Obs` [INFERIDO] | BFEAuthorize · `Cmp/Tipo_cbte` | 23 / 22 |
| 15 | LA CUIT INFORMADA DEL EMISOR NO CUMPLE LAS CONDICIONES SEGÚN EL RÉGIMEN FCE | observa: código en `BFEResultAuth/Obs` [el manual lo llama "motivo de rechazo"; efecto real NO VERIFICADO] | BFEAuthorize · `BFEResultAuth/Obs` | 15 / 15 |
| 16 | LA CUIT INFORMADA DEL EMISOR NO TIENE ACTIVO EL DOMICILIO FISCAL ELECTRONICO | observa: código en `BFEResultAuth/Obs` [el manual lo llama "motivo de rechazo"; efecto real NO VERIFICADO] | BFEAuthorize · `BFEResultAuth/Obs` | 15 / 15 |
| 17 | SI EL TIPO DE COMPROBANTE QUE ESTÁ AUTORIZANDO ES MIPYMES (FCE), EL RECEPTOR DEL COMPROBANTE INFORMADO EN DOCTIPO Y DOCNRO DEBE CORRESPONDER A UN CONTRIBUYENTE CARACTERIZADO COMO GRANDE O PYME QUE OPTÓ. | observa: código en `BFEResultAuth/Obs` [el manual lo llama "motivo de rechazo"; efecto real NO VERIFICADO] | BFEAuthorize · `BFEResultAuth/Obs` | 15 / 15 |
| 18 | SI EL TIPO DE COMPROBANTE QUE ESTÁ AUTORIZANDO ES MIPYMES (FCE), EL RECEPTOR DEL COMPROBANTE DEBE TENER HABILITADO EL DOMICILIO FISCAL ELECTRÓNICO | observa: código en `BFEResultAuth/Obs` [el manual lo llama "motivo de rechazo"; efecto real NO VERIFICADO] | BFEAuthorize · `BFEResultAuth/Obs` | 15 / 15 |
| 21 | LA CUIT RECEPTORA SE ENCUENTRA INACTIVA POR HABER SIDO INCLUÍDA EN LA CONSULTA DE FACTURAS APÓCRIFAS. NO PODRÁ COMPUTARSE EL CRÉDITO FISCAL | observa: código en `BFEResultAuth/Obs` | BFEAuthorize · `BFEResultAuth/Obs` | 16 / 15 |
| 22 | SI EL TIPO DE COMPROBANTE QUE ESTÁ AUTORIZANDO ES MIPYMES (FCE) CLASE "B", EL RECEPTOR DEL COMPROBANTE INFORMADO EN DOCTIPO Y DOCNRO DEBE ENCONTRARSE REGISTRADOS DE FORMA ACTIVA EN EL IMPUESTO IVA, MONOTIBUTO o EXENTO. | observa: código en `BFEResultAuth/Obs` [el manual lo llama "motivo de rechazo"; efecto real NO VERIFICADO] | BFEAuthorize · `BFEResultAuth/Obs` | 15-16 / 15 |
| 23 | DETECTAMOS QUE TENES PENDIENTE DE PRESENTACIÓN EL FORMULARIO DE HABILITACIÓN DE COMPROBANTES, O SU FECHA DE PRESENTACIÓN ES ANTERIOR A TU ALTA EN IVA. Para el caso de facturas/Notas de Débito, tenés que proceder a anular la operación emitida, mediante una Nota de Crédito. | observa: código en `BFEResultAuth/Obs` | BFEAuthorize · `BFEResultAuth/Obs` | 16 / 15 |
| 24 | LA CUIT RECEPTORA QUE INGRESASTE NO EXISTE. Para el caso de facturas/Notas de Débito, tenés que emitir una Nota de Crédito o anular la operación, según corresponda. | observa: código en `BFEResultAuth/Obs` | BFEAuthorize · `BFEResultAuth/Obs` | 16 / 16 |
| 25 | El importe de la nota de crédito supera el monto del comprobante asociado que estás ajustando. Verificá los montos ingresados y de tratarse de un error, tenés que efectuar el ajuste o anulación de la operación según corresponda. | observa: código en `BFEResultAuth/Obs` | BFEAuthorize · `BFEResultAuth/Obs` | 16 / 16 |
| 26 | El campo Condición Frente al IVA del receptor resultara obligatorio conforme lo reglamentado por la Resolución General Nro 5616. Para mas informacion consular método BFEGetPARAM_CondicionIvaReceptor. | observa: código en `BFEResultAuth/Obs` — **en desuso desde V3.2** (9-jun-2025) | BFEAuthorize · `BFEResultAuth/Obs` | 16 / 16 |
| 1020 | Comprobante inexistente | error en `BFEErr` (forma de la respuesta NO VERIFICADA) | BFEGetCMP · `Cmp` | 25 / 25 |
| 4967 | El valor ingresado para la clase de comprobante no es valido. La clase de Comprobante es opcional, de ingresar un valor solo puede ser A o B | error en `BFEErr` (en V3.0 este mismo texto tenía el código **4963**) | BFEGetPARAM_CondicionIvaReceptor · `ClaseCmp` | 37 (4963) / 37 (4967) |
| 4964 | Sin Resultados. A la fecha consultada no se registran valores de cotización para la moneda indicada | error en `BFEErr` | BFEGetCotizacion | 39 / 38 |
| 4965 | El identificador de moneda (MonId) ingresado es invalido. Este campo es obligatorio y no puede quedar vacío. Verificar los códigos mediante el metodo BFEGetPARAM_MON. | error en `BFEErr` | BFEGetCotizacion · `MonId` | 39 / 38 |
| 4966 | Campo FchCotiz no corresponde a una fecha valida con formato YYYYMMDD. Este campo es opcional, de informarlo la fecha debe tener el formato YYYYMMDD donde YYYY corresponde al año, MM al mes y DD al día solicitado. De no informarlo se tomara la fecha del día actual como valor por default. | error en `BFEErr` | BFEGetCotizacion · `FchCotiz` | 39 / 38 |

### Códigos vistos en el servicio que no están en el manual

| Código | Dónde | Texto | Fuente |
|---|---|---|---|
| `ErrCode` 0 | `BFEErr` en toda respuesta exitosa | `OK` | [CAS] |
| `EventCode` 0 | `BFEEvents` sin eventos | `Ok` o vacío (`<EventMsg />`) | [CAS] |
| `Obs` 13 | `BFEGetCMP/BFEResultGet/Obs` de un comprobante con `Resultado` `A` | — (el manual no lista la observación 13) | [CAS, `test_main_get.yaml`] |
| `EventCode` 39 | `BFEEvents` de toda respuesta de homologación, también en errores | `IMPORTANTE: Por motivos de mantenimiento, el servicio estara fuera de linea el domingo 14 de junio desde las 21:00 hs durante aproximadamente 3 horas. Agradecemos tu comprension y pedimos disculpas por las molestias ocasionadas.` | [VIVO] 2026-10-02 |

## Tablas y datos

El manual **no trae** tablas de parámetros, salvo el anexo de condición frente al IVA. Los valores de abajo salen de
[CAS] (homologación, 20-jun-2021). Sirven para cargar ArcaSim, pero pueden haber cambiado: **capturarlos de nuevo con
certificado**.

### Tipos de comprobante (`BFEGetPARAM_Tipo_Cbte`) [CAS]

| `Cbte_Id` | `Cbte_Ds` | Vigencia desde | Clase [INFERIDO por la descripción] |
|---:|---|---|---|
| 1 | Factura A | 20090620 | A |
| 2 | Nota de Débito A | 20090620 | A |
| 3 | Nota de Crédito A | 20090620 | A |
| 6 | Factura B | 20090620 | B |
| 7 | Nota de Débito B | 20090620 | B |
| 8 | Nota de Crédito B | 20090620 | B |
| 201 | Factura de Crédito electrónica MiPyMEs (FCE) A | 20190112 | A |
| 202 | Nota de Débito electrónica MiPyMEs (FCE) A | 20190112 | A |
| 203 | Nota de Crédito electrónica MiPyMEs (FCE) A | 20190112 | A |
| 206 | Factura de Crédito electrónica MiPyMEs (FCE) B | 20190112 | B |
| 207 | Nota de Débito electrónica MiPyMEs (FCE) B | 20190112 | B |
| 208 | Nota de Crédito electrónica MiPyMEs (FCE) B | 20190112 | B |

`Cbte_vig_hasta` = `NULL` en todos. El **91** (remito) solo aparece como comprobante **asociable** (1035-1038). El 211
que menciona el 4954 no está.

### Otras tablas chicas [CAS]

- **Tipos de documento** (`BFEGetPARAM_Tipo_doc`): solo `80` CUIT (desde 20090620).
- **Alícuotas de IVA** (`BFEGetPARAM_Tipo_IVA`, desde 20090220): `1` No gravado, `2` Exento, `3` 0%, `4` 10.5%,
  `5` 21%, `6` 27%.
- **Zonas** (`BFEGetPARAM_Zonas`): solo `1` Nacional (desde 20090215). En un request real se mandó `Zona` 0 y el
  rechazo fue por otra causa, así que no se sabe si 0 se acepta [CAS].

### Monedas (`BFEGetPARAM_MON`) [CAS], 50 registros, todas con `Mon_vig_hasta` `NULL`

`PES` Pesos Argentinos; `DOL` Dólar Estadounidense; `002` Dólar Libre EEUU; `007` Florines Holandeses; `010` Pesos
Mejicanos; `011` Pesos Uruguayos; `014` Coronas Danesas; `015` Coronas Noruegas; `016` Coronas Suecas; `018` Dólar
Canadiense; `019` Yens; `021` Libra Esterlina; `023` Bolívar Venezolano; `024` Corona Checa; `025` Dinar Yugoslavo;
`026` Dólar Australiano; `027` Dracma Griego; `028` Florín (Antillas Holandesas); `029` Güaraní; `031` Peso Boliviano;
`032` Peso Colombiano; `033` Peso Chileno; `034` Rand Sudafricano; `036` Sucre Ecuatoriano; `051` Dólar de Hong Kong;
`052` Dólar de Singapur; `053` Dólar de Jamaica; `054` Dólar de Taiwan; `055` Quetzal Guatemalteco; `056` Forint
(Hungría); `057` Baht (Tailandia); `059` Dinar Kuwaiti; `012` Real; `030` Shekel (Israel); `035` Nuevo Sol Peruano;
`060` Euro; `040` Lei Rumano; `042` Peso Dominicano; `043` Balboas Panameñas; `044` Córdoba Nicaragüense; `045` Dirham
Marroquí; `046` Libra Egipcia; `047` Riyal Saudita; `061` Zloty Polaco; `062` Rupia Hindú; `063` Lempira Hondureña;
`064` Yuan (Rep. Pop. China); `009` Franco Suizo; `041` Derechos Especiales de Giro; `049` Gramos de Oro Fino.

### Unidades de medida (`BFEGetPARAM_UMed`) [CAS], 48 registros

41 miligramos; 14 gramos; 1 kilogramos; 29 toneladas; 10 quilates; 47 mililitros; 5 litros; 27 cm cúbicos;
15 milimetros; 20 centímetros; 17 kilómetros; 7 unidades; 8 pares; 9 docenas; 11 millares; 96 packs; 97 hormas;
2 metros; 3 metros cuadrados; 4 metros cúbicos; 6 1000 kWh; 99 otras unidades; 16 mm cúbicos; 18 hectolitros;
25 jgo. pqt. mazo naipes; 30 dam cúbicos; 31 hm cúbicos; 32 km cúbicos; 33 microgramos; 34 nanogramos;
35 picogramos; 48 curie; 49 milicurie; 50 microcurie; 51 uiacthor; 52 muiacthor; 53 kg base; 54 gruesa; 61 kg bruto;
62 uiactant; 63 muiactant; 64 uiactig; 65 muiactig; 66 kg activo; 67 gramo activo; 68 gramo base; **0 (descripción
vacía, desde 20091211)**; 98 otras unidades (desde 20201022). Las demás rigen desde 20080704.

### Productos NCM (`BFEGetPARAM_NCM`) [CAS]

- **3209 registros** en una respuesta de 656 KB. ArcaSim debería cargarlos desde una captura, por ejemplo la de
  `tests/cassettes/test_wsbfev1/test_parametros.yaml` de PyAfipWs, en vez de transcribirlos.
- Dos formatos de código: `9999.99.99` (2413 registros, vigentes desde 20090215 y sin fecha hasta) y `99.99.99.99`
  (796 registros, ya vencidos: hasta 20120630 o 20101231).
- `NCM_Ds` vale `NULL` en todos, salvo los comodines "(item no incluído en el Beneficio Fiscal)": `9999.99.99`
  (vigente) y `99.99.99.99`.
- `NCM_Nota`: "Bonos Fisc." (3203), "Bonos Fisc. Motos - Beneficiarios" (3) y "Bonos Fisc. Motos - Proveedores" (3).
- 39 registros tienen vigencia 20201211-20991231.
- Códigos usados en comprobantes **aprobados** reales: `2101.11.10`, `7308.10.00`, `7308.20.00` y `9999.99.99`; los
  cuatro están en la lista.
- El manual no dice qué error da un NCM fuera de la lista. Sería un 1014 con texto [INFERIDO]; **NO VERIFICADO**.

### Opcionales (`BFEGetPARAM_Tipo_Opc`)

No hay captura real: la operación no existía en 2021 o no fue grabada. Ids que menciona el manual:

| Id | Uso según el manual | Códigos |
|---|---|---|
| `2` | Promoción Industrial: número de proyecto (8 dígitos, ≥ 0) o `0` si la actividad no está alcanzada | 1019, 1020 (pág. 18) |
| `22` | FCE: marca de anulación, `S` o `N` | 4881, 4908, 4912, 4914, 4915, 4946, 4949 |
| `23` | Referencia comercial, alfanumérico de hasta 50 caracteres, puede repetirse con valores distintos | 4931, 4932 |
| `27` | FCE: `SCA` (transferencia al sistema de circulación abierta) o `ADC` (agente de depósito colectivo) | 4952-4954 |
| `2101` | FCE: CBU numérico de 22 caracteres | 4906, 4911, 4913 |
| `2102` | FCE: alias de 6 a 20 caracteres alfanuméricos | 4907, 4913 |

**NO VERIFICADO**: las descripciones (`Opc_Ds`) y si la tabla trae otros ids.

### Condición frente al IVA del receptor (anexo 3.1) [MAN30 pág. 39-40; MAN32 pág. 39]

| Código | Descripción | Clase A | Clase B |
|---:|---|:---:|:---:|
| 1 | IVA Responsable Inscripto | X | |
| 4 | IVA Sujeto Exento | | X |
| 5 | Consumidor Final | | X |
| 6 | Responsable Monotributo | X | |
| 7 | Sujeto No Categorizado | | X |
| 8 | Proveedor del Exterior | | X |
| 9 | Cliente del Exterior | | X |
| 10 | IVA Liberado – Ley N° 19.640 | | X |
| 13 | Monotributista Social | X | |
| 15 | IVA No Alcanzado | | X |
| 16 | Monotributo Trabajador Independiente Promovido | X | |

Es la base de 4961 (valor fuera de la tabla) y 4962 (valor que no corresponde a la clase del comprobante).

### Datos de prueba de homologación

El manual **no publica** CUITs ni puntos de venta de prueba. En [CAS] se ven el CUIT emisor `20267565393` (certificado
propio del autor de PyAfipWs), los puntos de venta `5` y `2` habilitados para bonos y los receptores `20888888883`,
`23111111113` y `30516768106`. **No son reutilizables**: cada integrador homologa con su propio certificado y sus puntos
de venta.

## Comportamiento a simular

### Estado

- **Comprobantes autorizados**, por clave (`Cuit` emisor, `Tipo_cbte`, `Punto_vta`, `Cbte_nro`). Se guarda todo el `Cmp`
  enviado más `Cae`, `Fch_venc_Cae`, `Fecha_cbte_orig`, `Fecha_cbte_cae`, `Resultado`, `Obs` e `Id`. Con eso se responde
  `BFEGetCMP` con los 31 campos de `ClsBFEGetCMPR`.
- **Requerimientos por `Id`**, por clave (`Cuit`, `Id`), con la respuesta `BFEResultAuth` dada, para el reproceso.
- **Último `Id`** por `Cuit`, para `BFEGetLast_ID`.
- **Último número y fecha** por (`Cuit`, `Tipo_cbte`, `Punto_vta`), para `BFEGetLast_CMP`.
- **Tablas de parámetros** (secciones de arriba), cotizaciones por moneda y fecha, y un "padrón" mínimo (CUITs, puntos
  de venta habilitados, condición FCE) para las reglas que dependen de datos de ARCA.

### Idempotencia por `Id` y `BFEGetLast_ID`

Lo que se sabe:

- **Manual**: `Id` es el "Identificador del requerimiento" (pág. 13), debe ser > 0 (1014, pág. 17) y se devuelve en
  `BFEResultAuth`. `Reproceso` "Indica si es un reproceso "S" o "N"" (pág. 15). **No explica más**: no dice qué es un
  reproceso ni qué pasa al repetir un `Id`.
- **[CAS], secuencia real del mismo CUIT**:
  - `BFEGetLast_ID` → 993456789012386.
  - `BFEAuthorize` con `Id` 993456789012387 → `A`, `Reproceso` `N`.
  - `BFEGetLast_ID` → 993456789012387.
  - Un mes después, `BFEGetLast_ID` → …388.
  - `BFEAuthorize` con `Id` …389 → **rechazo 4900**.
  - `BFEGetLast_ID` → sigue …388.
  - Otro intento con `Id` …389 → **rechazo 4954**.
  - Conclusión: **un `Id` rechazado por `BFEErr` no queda registrado** y se puede reusar.
- **Analogía con wsfexv1** (mismo diseño ASMX, mismos campos `Id`/`Reproceso`). Su manual sí lo explica: "El sistema
  busca la solicitud recibida en su base de datos y, si la encuentra, retorna la respuesta con el campo `<Reproceso>` =
  "S". Si no la encuentra, la procesa normalmente" (citado en `wsfexv1.md`; ver también [CAT §5.4]).
- PyAfipWs arma el `Id` nuevo como `GetLastID() + 1` (`wsbfev1.py`, `main`).

Comportamiento recomendado para ArcaSim [INFERIDO de lo anterior]:

1. `BFEAuthorize` con un `Id` que ya tiene un comprobante **aprobado** para ese `Cuit` → devolver el **mismo**
   `BFEResultAuth` guardado (mismo `Cae`, `Fch_venc_Cae` y `Fch_cbte`) con `Reproceso` = `S`, sin numerar ni emitir de
   nuevo.
2. `Id` nuevo → procesar. Si se aprueba, registrar el `Id` y devolver `Reproceso` = `N`.
3. Rechazo con `BFEErr` → no registrar el `Id`.
4. `BFEGetLast_ID` → el último `Id` registrado (aprobado) para el `Cuit` de `Auth`.
5. Dejar configurable lo desconocido: mismo `Id` con datos distintos, `Id` menor que el último y `Id` que no es último + 1.
   Por defecto, no validar correlatividad del `Id`.

### Numeración

- Por (`Cuit`, `Tipo_cbte`, `Punto_vta`). `BFEGetLast_CMP` devuelve `Cbte_nro` y `Cbte_fecha` del último autorizado.
  Después de autorizar el 2581 devolvió 2581 [CAS].
- `Cbte_nro` va de 1 a 99999999 (1014). El manual **no** tiene un código de "número no correlativo". Todos los ejemplos
  reales usan último + 1. ArcaSim: rechazar lo no correlativo con 1014 y un texto propio [INFERIDO]. **NO VERIFICADO** el
  texto real.
- Último número sin comprobantes previos: **NO VERIFICADO** (wsfev1 devuelve 0; se recomienda 0 y `Cbte_fecha` vacío).
- `Punto_vta` "único para el requerimiento" (1014): en la misma llamada no puede haber dos puntos de venta. Con un solo
  `Cmp` por llamada, la regla se cumple siempre [INFERIDO].

### CAE, fechas y resultado

- **CAE**: string de **14 dígitos** (`71253948358918`, `71293955912704`, `71313958066688`) [CAS]. Los 4 primeros dígitos
  parecen ser "7" + último dígito del año + semana del año (2021: semanas 25, 29 y 31). Es una inferencia **débil**: con
  generar 14 dígitos únicos alcanza.
- **`Fch_venc_Cae` = `Fch_cbte` + 10 días corridos** en los 3 casos reales (20210621 → 20210701, 20210722 → 20210801,
  20210801 → 20210811) [CAS]. Es la misma regla inferida para wsfev1. No hay texto oficial.
- **Fecha del comprobante**: si no viene, "se asignará la fecha de proceso" (pág. 17). En `BFEGetCMP`, `Fecha_cbte_orig`
  es la enviada y `Fecha_cbte_cae` la otorgada. Si no se envió fecha, `Fecha_cbte_orig` vendría vacía [INFERIDO de pág.
  24-25]. Reglas: `yyyymmdd`, ±5 días de la fecha de envío y dentro del mismo mes (1014). En FCE, N-5 a N+1 (4897) y
  mismo mes si es futura (4898). **El reloj del simulador tiene que ser configurable** (zona horaria de Argentina).
- **`Resultado`**: `A` en aprobados [CAS]. Los errores no traen `Resultado` (solo `Id`/`Cuit` 0). **NO VERIFICADO**:
  si existe `R` (con `Obs` como "motivo de rechazo", pág. 15) o `P`.
- **`Obs`**: `" "` si no hay observaciones; si hay, el código (se vio `"13"`) [CAS].

### Importes e ítems

- Suma de ítems ≤ totales del comprobante. `Imp_op_ex` > 0 si hay un ítem exento (1014, pág. 17). Las percepciones,
  exentas e internos no pueden superar el total (pág. 17).
- En los ejemplos reales: `Imp_neto` = Σ(`Pro_qty`×`Pro_precio_uni`−`Imp_bonif`); `Impto_liq` = IVA de ese neto;
  `Imp_total` del comprobante = Σ `Imp_total` de ítems (con IVA) [CAS]. El margen de redondeo no está documentado:
  **NO VERIFICADO**.
- Cada ítem: `Pro_codigo_ncm` vigente en la tabla NCM, `Pro_umed` en la tabla de unidades e `Iva_id` en la de
  alícuotas [INFERIDO de las referencias del manual a cada método, pág. 13-14].

### Moneda y cotización

- `BFEGetCotizacion` necesita una tabla configurable de cotizaciones por moneda y fecha. Sin `FchCotiz` → hoy. Sin
  dato → 4964.
- Con `CanMisMonExt` = `S`, la cotización informada "debe coincidir exactamente con la registrada en las bases de ARCA
  para el día hábil anterior a la fecha de emisión del comprobante, si esta es anterior a la fecha actual, o bien con la
  registrada para el día hábil anterior a la fecha actual, si la fecha de emisión es posterior a esta. En caso contrario,
  se puede omitir el campo de Cotización de Moneda" [MAN30 pág. 4, historial 3.0]. Hace falta un **calendario de días
  hábiles**. El manual no dice qué código da la falta de coincidencia: 1014 o 4960 [INFERIDO]. **NO VERIFICADO**.
- Reglas 4957-4960 y la banda del 1014 (ver inconsistencias).

### Condición frente al IVA del receptor (RG 5616)

Flag configurable `rg5616_obligatoria`, por defecto **activo** (V3.2):

- Ausente → rechazo 4963 (activo) u observación 26 (inactivo).
- Fuera del anexo → 4961.
- No corresponde a la clase del comprobante → 4962. Clase por tipo: A = 1, 2, 3, 201, 202, 203; B = 6, 7, 8, 206, 207,
  208 [INFERIDO].

### Qué se devuelve cuando no hay datos

| Situación | Respuesta | Confianza |
|---|---|---|
| `BFEGetCMP` de un comprobante inexistente | `BFEErr` 1020 "Comprobante inexistente" | [MAN30 pág. 25]; forma NO VERIFICADA |
| `BFEGetCotizacion` sin cotización | `BFEErr` 4964 | [MAN30 pág. 39] |
| `BFEGetLast_CMP` sin comprobantes previos | — | **NO VERIFICADO** |
| `BFEGetLast_ID` para un CUIT sin requerimientos | — | **NO VERIFICADO** |
| Recuperador de parámetros con la tabla vacía | — | **NO VERIFICADO** (probablemente `BFEResultGet` vacío y `ErrCode` 0) |

### Eventos y header

- `BFEEvents` lleva **un** evento. Sin evento: código 0, `Ok`. Homologación hoy devuelve siempre el 39 (aviso de
  mantenimiento vencido) [VIVO]. ArcaSim: evento configurable, 0 por defecto.
- `FEHeaderInfo` configurable: `id` `3.0.0.1` y `ambiente` presente u omitido. En modo "homologación real" se omite.

### Coexistencia con wsbfe

Mismo service id (`wsbfe`) y mismo modelo de datos. Las versiones son distintas (`id` `3.0.0.1` contra `3.0.0.0` en
homologación), lo que sugiere despliegues separados. **NO VERIFICADO** si comparten numeración, `Id` y comprobantes (un
comprobante autorizado por wsbfe, ¿se ve en `BFEGetCMP` de wsbfev1?). Recomendación: un almacén común configurable.

### Imposible de simular fielmente

- Toda regla que depende de bases de ARCA. Con datos ficticios solo se reproduce el **contrato**:
  - CUIT representada (1001);
  - comprobantes asociados existentes (1039, 4880, 4882, 4947);
  - cuenta corriente FCE y saldos (4884, 4926, 4927, 4948, 4949);
  - topes y categorización FCE (4933, 4944, 4930);
  - CBU y alias registrados (4913);
  - régimen de los puntos de venta (4956);
  - receptor (4945; observaciones 15-18 y 21-25);
  - proyectos de Promoción Industrial (1020).
- Cotizaciones oficiales exactas y el "orientativo" de la banda del 1014.
- Textos reales de `ErrMsg` (solo se conocen 1000, 4900 y 4954).
- Validez del CAE ante terceros.

## No verificado

1. Texto real de 1001 y de los errores de token vencido, de TA de otro service id y de CUIT fuera de las relaciones.
2. Si la homologación actual de wsbfev1 **ya rechaza** sin `CondicionIVAReceptorId` (4963) o todavía observa (26).
   El evento de wsbfev1 no lo dice; el de wsbfe sí.
3. Reproceso: qué devuelve un `Id` repetido (solo hay analogía con wsfexv1), con datos distintos o menor que el último;
   si `Id` debe ser último + 1; si el `Id` es por CUIT o global.
4. `BFEGetLast_CMP` y `BFEGetLast_ID` sin datos previos; punto de venta inexistente.
5. Forma de la respuesta de `BFEGetCMP` con 1020 (¿viene `BFEResultGet` con ceros?).
6. Existencia de `Resultado` `R`/`P`; formato de `Obs` con varias observaciones; significado de la observación `13`.
7. Si 15-18 y 22 rechazan u observan.
8. Banda real del tipo de cambio (2%/400%, 20%/200% o 20%/100%) y el significado de "no podrá superar en 1" (4960).
9. `ClaseCmp` = `C` en `BFEGetPARAM_CondicionIvaReceptor`.
10. Contenido actual de todas las tablas (solo hay captura de 2021); `BFEGetPARAM_Tipo_Opc` y
    `BFEGetPARAM_CondicionIvaReceptor` nunca se capturaron.
11. Formato de `MonCotiz` y de `FchCotiz` en la respuesta real de `BFEGetCotizacion`.
12. Error y texto para NCM, unidad de medida o alícuota fuera de tabla; para `Zona` 0; para número no correlativo.
13. Regla exacta de `Fch_venc_Cae` (inferida `Fch_cbte` + 10) y si el CAE tiene estructura.
14. Por qué un `Pro_ds` volvió rellenado con espacios hasta 250 y otro no.
15. Valores de `BFEDummy` con un componente caído; límites de tasa o de tamaño de request.
16. Si wsbfe y wsbfev1 comparten datos.
17. El tipo 211 que menciona el 4954.
