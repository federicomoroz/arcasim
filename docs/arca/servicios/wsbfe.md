# wsbfe

**Qué hace.** Es la versión anterior de `wsbfev1`: autoriza con CAE, **de a un comprobante por llamada**, los
comprobantes A y B del régimen de **Bonos Fiscales Electrónicos**. Cada comprobante lleva ítems con código NCM
[MAN pág. 7-9]. El catálogo lo asocia a la **RG 2557** [CAT §3.1]. El manual no cita ninguna resolución: su título es
solo "Emisión de Bonos Fiscales Electrónicos" [MAN pág. 1].

**Estado.** El catálogo general lo sigue listando y no lo marca como deprecado. La página de factura electrónica solo
lista wsbfev1 [CAT §3.1]. El 2026-10-02 respondió en homologación y en producción (`BFEDummy` OK) [VIVO]. Por
estructura es un **subconjunto de wsbfev1 sin Factura de Crédito Electrónica**: no tiene `Opcionales`, `Fecha_vto_pago`,
`Cuit`/`Fecha_cbte` en comprobantes asociados ni `BFEGetPARAM_Tipo_Opc`. Pero **sí** recibió los agregados de 2025
(`CondicionIVAReceptorId`, `CanMisMonExt`, `BFEGetCotizacion` y `BFEGetPARAM_CondicionIvaReceptor`), que su manual no
documenta [WSDL].

El manual no dice quién usa este servicio en lugar de wsbfev1: **NO VERIFICADO**.

Fecha de relevamiento: **2026-10-02**.

### Fuentes

| Id | Fuente | URL / ubicación | Uso |
|---|---|---|---|
| **[MAN]** | "Facturación Electrónica – Emisión de Bonos Fiscales Electrónicos – Manuales para el desarrollador", **V1.1**, revisión 01-oct-2018, AFIP-SDG SIT, 23 págs. | <https://www.afip.gob.ar/ws/WSBFE/WSBFE%20-%20Manual%20para%20el%20desarrollador_V1_1.pdf> | Leído completo. Viejo: ver abajo |
| **[WSDL]** | WSDL de homologación (51 812 bytes, esquema inline, sin imports). El de producción es idéntico salvo el `soap:address`. | `docs/arca/wsdl/wsbfe-homologacion.wsdl` (= `https://wswhomo.afip.gov.ar/wsbfe/service.asmx?WSDL`) | Estructura: **manda** |
| **[VIVO]** | Llamadas propias sin certificado, 2026-10-02 (homologación; un `BFEDummy` a producción) | `scratchpad/vivo/bfe-*` | Respuestas reales |
| **[V1]** | Documento hermano `wsbfev1.md` y sus manuales V3.0/V3.2 | `docs/arca/servicios/wsbfev1.md` | Referencia para lo que el manual V1.1 no cubre. Todo lo que se toma de ahí está marcado [INFERIDO] |
| **[CAT]** | Catálogo de ArcaSim | `docs/arca/catalogo.md` §3.1, §4.1, §8 | Contexto |

No hay grabaciones de terceros de wsbfe: PyAfipWs solo graba wsbfev1. Las páginas se citan como "pág. N" = página física
del PDF de [MAN].

Marcas de confianza: las mismas que en `wsbfev1.md` ([MAN], [WSDL], [VIVO] verificados; [INFERIDO]; **NO VERIFICADO**).

### Lo que el manual V1.1 no explica

El manual se escribió en 2009 y su última revisión es de 2018. El WSDL actual tiene bastante más:

| Tema | WSDL actual | Manual V1.1 |
|---|---|---|
| Operaciones | 14 | Documenta 7: `BFEAuthorize`, `BFEGetCMP`, `BFEGetPARAM_MON`, `BFEGetPARAM_NCM`, `BFEGetPARAM_Tipo_cbte`, `BFEGetPARAM_Tipo_iva` y `BFEGetPARAM_Zonas` (índice, pág. 3) |
| `BFEGetPARAM_CondicionIvaReceptor` | Existe (y es la **primera** del `portType`) | **No existe**. Agregada para RG 5616 |
| `BFEGetCotizacion` | Existe | **No existe** |
| `BFEGetLast_CMP`, `BFEGetLast_ID`, `BFEDummy`, `BFEGetPARAM_Tipo_doc`, `BFEGetPARAM_UMed` | Existen | **No existen** (solo cita "BFEGetPARAM_UMed" en el ítem, pág. 9) |
| `Cmp/CondicionIVAReceptorId`, `Cmp/CanMisMonExt` | En el request de `BFEAuthorize` y en la respuesta de `BFEGetCMP` | No figuran |
| Obligatoriedad de la condición frente al IVA (RG 5616) | — | No figura. El evento 102 en vivo la anuncia: "El dia 9 de junio de 2025 se actualizo la version del Web Service (WS) en el ambiente de Homologacion Externa en la cual se establece como obligatorio el campo Condicion Frente al IVA del receptor" |
| Códigos de las validaciones nuevas (moneda, cotización, condición IVA) | — | Ninguno. En wsbfev1 son 4957-4963 y 4964-4967; **NO VERIFICADO** si wsbfe usa los mismos |
| `Fch_venc_Cae` en `BFEGetCMP` | Existe | No figura (el manual lista un `Fecha_cae` y un `Zona` que no existen) |

Detalle curioso: el manual fue retocado después del cambio de nombre de AFIP a ARCA (dice "www.arca.gob.ar/ws" en pág.
4, y "suministra ARCA como orientativo" en el historial de pág. 2), **sin** cambiar versión ni fecha de revisión.

## Contrato

### Dialecto

**.NET ASMX**, idéntico a wsbfev1 y wsfexv1: un comprobante por llamada con `Id` propio, un único `BFEErr{ErrCode,
ErrMsg}` y un único `BFEEvents{EventCode, EventMsg}`. Errores de negocio y de token con HTTP 200 y `FEHeaderInfo`
[WSDL] [VIVO].

### Endpoints

| Ambiente | URL del servicio (`soap:address`) | WSDL | Fuente |
|---|---|---|---|
| Homologación | `https://wswhomo.afip.gov.ar/wsbfe/service.asmx` | `https://wswhomo.afip.gov.ar/wsbfe/service.asmx?WSDL` | [MAN] pág. 6; [WSDL] |
| Producción | `https://servicios1.afip.gov.ar/wsbfe/service.asmx` | `https://servicios1.afip.gov.ar/wsbfe/service.asmx?WSDL` | [MAN] pág. 6; WSDL de producción bajado el 2026-10-02 |

Las secciones de cada método muestran `http://wswhomo.afip.gov.ar/wsbfe/service.asmx?op=<Método>`, la página de ayuda
ASMX (pág. 7 y siguientes).

### Namespace y forma del XML

- `targetNamespace` y namespace de todos los elementos: **`http://ar.gov.afip.dif.bfe/`** [WSDL].
- `elementFormDefault="qualified"` [WSDL].
- `wsdl:documentation`: "Web Service orientado  al  servicio  de Bonos Fiscales electronicos" (sin "V1") [WSDL].
- Los ejemplos de `FEHeaderInfo` del manual usan el sobre SOAP 1.2 (`http://www.w3.org/2003/05/soap-envelope`, pág.
  4-5) y los de cada método, SOAP 1.1. El servicio contesta en la versión del request [VIVO].

### Service id de WSAA

**`wsbfe`**. Literal: "debe enviar el tag service con el valor "wsbfe" y que la duración del mismo es de 12 hs"; el
certificado se asocia al servicio de negocio "Bonos Fiscales Electronicos - BFE" [MAN pág. 4, §1.3]. Es **el mismo id
que wsbfev1** [V1: MAN30 pág. 7], así que un TA sirve para los dos servicios [INFERIDO].

### SOAPAction

`http://ar.gov.afip.dif.bfe/` + operación, igual en SOAP 1.1 y 1.2, en el orden del `portType` [WSDL]:

```
http://ar.gov.afip.dif.bfe/BFEGetPARAM_CondicionIvaReceptor
http://ar.gov.afip.dif.bfe/BFEAuthorize
http://ar.gov.afip.dif.bfe/BFEGetCMP
http://ar.gov.afip.dif.bfe/BFEGetPARAM_Tipo_doc
http://ar.gov.afip.dif.bfe/BFEGetPARAM_Tipo_IVA
http://ar.gov.afip.dif.bfe/BFEGetPARAM_Zonas
http://ar.gov.afip.dif.bfe/BFEGetPARAM_Tipo_Cbte
http://ar.gov.afip.dif.bfe/BFEGetPARAM_UMed
http://ar.gov.afip.dif.bfe/BFEGetPARAM_NCM
http://ar.gov.afip.dif.bfe/BFEGetPARAM_MON
http://ar.gov.afip.dif.bfe/BFEGetLast_CMP
http://ar.gov.afip.dif.bfe/BFEDummy
http://ar.gov.afip.dif.bfe/BFEGetLast_ID
http://ar.gov.afip.dif.bfe/BFEGetCotizacion
```

Respuesta: `<{Op}Response xmlns="http://ar.gov.afip.dif.bfe/"><{Op}Result>…` [WSDL] [VIVO].

### Bindings y comportamiento HTTP

Los mismos cuatro bindings que wsbfev1 [WSDL]:
- `ServiceSoap` (SOAP 1.1) y `ServiceSoap12` (SOAP 1.2), con las 14 operaciones;
- `ServiceHttpGet` y `ServiceHttpPost`, **solo** con `BFEDummy`.

Comportamiento real [VIVO, 2026-10-02], **idéntico a wsbfev1**:

| Caso | Respuesta | Archivo |
|---|---|---|
| SOAP 1.1 correcto | 200 `text/xml; charset=utf-8` | `bfe-dummy` |
| SOAP 1.2 (`application/soap+xml; …; action="…"`) | 200 `application/soap+xml; charset=utf-8`, sobre SOAP 1.2 | `bfe-dummy-soap12`, `bfe-badtoken-soap12` |
| Sin header `SOAPAction` | 200, enruta por el Body | `bfe-nosoapaction-header` |
| `SOAPAction: ""` | 500 `soap:Client` `Server did not recognize the value of HTTP Header SOAPAction: .` | `bfe-noaction-dummy` |
| `SOAPAction` inexistente | 500 `soap:Client` `Server did not recognize the value of HTTP Header SOAPAction: http://ar.gov.afip.dif.bfe/BFEXXX.`, sin header ni stack trace | `bfe-badaction` |
| `short` no numérico | 500 `soap:Client` `Server was unable to read request. ---&gt; There is an error in XML document (1, 284). ---&gt; Input string was not in a correct format.`, **con** `FEHeaderInfo` | `bfe-badint` |
| XML mal formado | 400, cuerpo vacío | `bfe-malformed` |
| `GET …/service.asmx/BFEDummy` | 200, `<DummyResponse … xmlns="http://ar.gov.afip.dif.bfe/">` indentado, sin sobre | `bfe-httpget-dummy` |
| `auth`/`Auth` con el nombre equivocado | se ignora → 1000 `Usuario no autorizado a realizar esta operacion. ` | `bfe-tipocbte-upperAuth`, `bfe-ncm-lowerauth` |

Headers HTTP: `BFEDummy` con `Cache-Control: private, max-age=0`; el resto con `no-cache`, `Pragma: no-cache` y
`Expires: -1`; cookies F5 [VIVO].

### Header de respuesta `FEHeaderInfo`

"Los mensajes de respuesta que se transmiten tienen implementado el subelemento FEHeaderInfo [...] El procesamiento de
dicha información no es obligatoria" [MAN pág. 4]. Namespace `http://ar.gov.afip.dif.bfe/`; hijos `ambiente`, `fecha` e
`id`.

| Ambiente | `ambiente` | `id` | Fuente |
|---|---|---|---|
| Homologación 2026-10-02 | `Homologacion - srt` | `3.0.0.0` | [VIVO] `bfe-dummy.body` |
| Producción 2026-10-02 | `Produccion - Se4` | **`2.0.1.0`** | [VIVO] `bfe-prod-dummy.body` |
| Manual | `Desarrollo - Clo` / `Produccion - Pto` | `1.0.7.0` | [MAN] pág. 4-5 |

Diferencias con wsbfev1:
- wsbfe **sí** manda `<ambiente>` en homologación (wsbfev1 no).
- En wsbfe la versión de producción (`2.0.1.0`) es **anterior** a la de homologación (`3.0.0.0`). Sugiere que
  homologación tiene cambios (RG 5616) que producción todavía no [INFERIDO]. En wsbfev1, homologación y producción están
  en `3.0.0.1`.

### Diferencias de estructura con wsbfev1 (WSDL contra WSDL, tipo por tipo)

Se compararon los dos WSDL con un volcado de cada elemento y `complexType` (nombre, tipo, `minOccurs`, `maxOccurs`,
`nillable`), más `portType`, bindings y `SOAPAction`. **Estas son todas las diferencias**:

| # | Tipo / elemento | wsbfev1 | wsbfe |
|---:|---|---|---|
| 1 | `targetNamespace` | `http://ar.gov.afip.dif.bfev1/` | `http://ar.gov.afip.dif.bfe/` |
| 2 | `wsdl:documentation` | "…Bonos Fiscales electronicos V1" | "…Bonos Fiscales electronicos" |
| 3 | `portType ServiceSoap` | 15 operaciones; `BFEGetPARAM_CondicionIvaReceptor` es la última | 14: **falta `BFEGetPARAM_Tipo_Opc`**; `BFEGetPARAM_CondicionIvaReceptor` es la **primera** |
| 4 | `ClsBFERequest` (request de `BFEAuthorize`) | … `Fecha_cbte`, **`Fecha_vto_pago`**, `CondicionIVAReceptorId`, `CanMisMonExt`, **`Opcionales`**, `Items`, `CbtesAsoc` | … `Fecha_cbte`, `CondicionIVAReceptorId`, `CanMisMonExt`, `Items`, `CbtesAsoc` (**sin `Fecha_vto_pago` ni `Opcionales`**) |
| 5 | `CbteAsoc` | `Tipo_cbte`, `Punto_vta`, `Cbte_nro`, **`Cuit`** (string 0..1), **`Fecha_cbte`** (string 0..1) | Solo `Tipo_cbte`, `Punto_vta`, `Cbte_nro` |
| 6 | `ClsBFEGetCMPR` (respuesta de `BFEGetCMP`) | … `Fecha_cbte_cae`, **`Fecha_vto_pago`**, `Fch_venc_Cae`, … `CanMisMonExt`, **`Opcionales`**, `Items`, `CbtesAsoc` | … `Fecha_cbte_cae`, `Fch_venc_Cae`, … `CanMisMonExt`, `Items`, `CbtesAsoc` (**sin `Fecha_vto_pago` ni `Opcionales`**) |
| 7 | `ArrayOfOpcional`, `Opcional` | Existen | **No existen** |
| 8 | Elementos `BFEGetPARAM_Tipo_Opc` / `…Response` y tipos `BFEResponse_Opc`, `ArrayOfClsBFEResponse_Opc`, `ClsBFEResponse_Opc` | Existen | **No existen** |
| 9 | Mensajes y `SOAPAction` de `BFEGetPARAM_Tipo_Opc` | Existen | **No existen** |

**Idénticos** en los dos (mismos campos, tipos, ocurrencias y orden):
- `ClsBFEAuthRequest`, `ClsBFEErr`, `ClsBFEEvents`, `Item`, `ArrayOfItem`, `ArrayOfCbteAsoc`;
- `BFEResponseAuthorize`, `ClsBFEOutAuthorize`, `ClsBFEGetCMP`, `BFEGetCMPResponse`;
- los recuperadores `_Tipo_doc`, `_Tipo_IVA`, `_Zonas`, `_Tipo_Cbte`, `_UMed`, `_NCM` y `_MON`, con sus tipos;
- `ClsBFE_LastCMP`, `BFEResponseLast_CMP`, `ClsBFE_LastCMP_Response`, `DummyResponse`, `BFEResponse_LastID`,
  `ClsBFEResponse_LastID`, `BFEResponse_Cotizacion`, `Cotizacion` y los tipos de `_CondicionIvaReceptor`;
- el uso de `Auth` contra `auth` por operación, los bindings HTTP GET/POST de `BFEDummy` y los textos de
  `wsdl:documentation` de cada operación.

Consecuencia: wsbfe **no puede emitir FCE**. Sin `Fecha_vto_pago` ni `Opcionales` no se cumplen las reglas 4900, 4905,
4910 ni 4954 de wsbfev1. Además, su 1035 solo admite 01, 02, 03, 06, 07 y 08 [MAN pág. 12] [INFERIDO].

## Autenticación

### Forma del bloque

`ClsBFEAuthRequest`: `Token` `s:string` 0..1, `Sign` `s:string` 0..1, `Cuit` `s:long` 1..1 [WSDL]. Manual: los tres
obligatorios; "Token devuelto por el WSAA", "Sign devuelto por el WSAA", "Cuit contribuyente (representado o
Emisora)" [MAN pág. 8].

Nombre del elemento por operación [WSDL]. Con el nombre equivocado se ignora [VIVO]:

| Elemento | Operaciones |
|---|---|
| `Auth` | `BFEAuthorize`, `BFEGetCMP`, `BFEGetPARAM_NCM`, `BFEGetLast_ID`, `BFEGetCotizacion` |
| `auth` | `BFEGetPARAM_CondicionIvaReceptor`, `BFEGetPARAM_Tipo_doc`, `BFEGetPARAM_Tipo_IVA`, `BFEGetPARAM_Zonas`, `BFEGetPARAM_Tipo_Cbte`, `BFEGetPARAM_UMed`, `BFEGetPARAM_MON` |
| `Auth` de tipo `ClsBFE_LastCMP` (`Token`, `Sign`, `Cuit`, `Pto_venta`, `Tipo_cbte`) | `BFEGetLast_CMP` |
| — | `BFEDummy` |

El manual muestra `<Auth>` en `_MON`, `_Tipo_Cbte` y `_Tipo_Iva` (pág. 15, 18, 20), `<auth>…</Auth>` en `_Zonas`
(pág. 22) y `<Cuit>string</Cuit>` en `_NCM`, `_Tipo_Cbte`, `_Tipo_Iva` y `_Zonas`. Son erratas: **manda el WSDL**.

### Validaciones del manual

1000 "Usuario no autorizado a realizar esta operación" y 1001 "Cuit solicitante no se encuentra entre sus
representados" [MAN pág. 11, 16, 18, 20, 21, 23].

### Respuestas reales de falla

[VIVO, 2026-10-02, homologación]. Se probaron `BFEGetPARAM_Tipo_Cbte` (los cuatro casos) y token "abc" en
`BFEAuthorize`, `BFEGetCMP`, `BFEGetLast_CMP`, `BFEGetLast_ID`, `BFEGetCotizacion`, `BFEGetPARAM_CondicionIvaReceptor` y
`BFEGetPARAM_NCM`. Los textos son **idénticos** a los de wsbfev1:

| Caso | `ErrCode` | `ErrMsg` literal |
|---|---|---|
| `Token` = "abc" | 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc` |
| `Token` y `Sign` vacíos | 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: No validó la firma digital. ` |
| Token XML en base64 con firma falsa | 1000 | ídem |
| Sin `auth` (o con el nombre equivocado) | 1000 | `Usuario no autorizado a realizar esta operacion. ` |

Siempre HTTP 200, **solo `BFEErr` y `BFEEvents`**, sin elemento de resultado. El evento es el **102**, no el 39 de
wsbfev1:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.bfe/"><ambiente>Homologacion - srt</ambiente><fecha>2026-10-02T15:09:01.1691795-03:00</fecha><id>3.0.0.0</id></FEHeaderInfo></soap:Header><soap:Body><BFEGetPARAM_Tipo_CbteResponse xmlns="http://ar.gov.afip.dif.bfe/"><BFEGetPARAM_Tipo_CbteResult><BFEErr><ErrCode>1000</ErrCode><ErrMsg>Usuario no autorizado a realizar esta operacion. </ErrMsg></BFEErr><BFEEvents><EventCode>102</EventCode><EventMsg>IMPORTANTE: El dia 9 de junio de 2025 se actualizo la version del Web Service (WS) en el ambiente de Homologacion Externa en la cual se establece como obligatorio el campo Condicion Frente al IVA del receptor. Cabe destacar que la Resolucion General Nro 5616 indica que ese dato debe enviarse de manera obligatoria. Para mas informacion, consultar el manual en: https://www.arca.gob.ar/fe/ayuda/webservice.asp, https://www.arca.gob.ar/ws/documentacion/ws-factura-electronica.asp</EventMsg></BFEEvents></BFEGetPARAM_Tipo_CbteResult></BFEGetPARAM_Tipo_CbteResponse></soap:Body></soap:Envelope>
```

**NO VERIFICADO**: el texto real de 1001, y los de token vencido o emitido para otro service id.

## Operaciones

Las 14 del `portType ServiceSoap`, en el orden del WSDL. Prefijo de los ejemplos:
`xmlns:x="http://ar.gov.afip.dif.bfe/"`. Las columnas siguen la convención de `wsbfev1.md`.

### Tipos compartidos

Idénticos a wsbfev1 (ver `wsbfev1.md`, Operaciones → Tipos compartidos) [WSDL]:

- `ClsBFEErr` (`BFEErr`): `ErrCode` `s:int` 1..1 y `ErrMsg` `s:string` 0..1. El manual escribe `errcode`/`errmsg` o
  `ErrCode`/`Errmsg` (pág. 5, 10, 14); manda el WSDL. Para errores de infraestructura vale la misma estructura: 500, 501
  y 502 (pág. 5).
- `ClsBFEEvents` (`BFEEvents`): `EventCode` `s:int` 1..1 y `EventMsg` `s:string` 0..1. "Código de evento (unico e
  irrepetible)" (pág. 6).
- Todas las respuestas, salvo `BFEDummy`: `{Op}Result` = resultado (0..1) + `BFEErr` + `BFEEvents`.
- `Item` (`ArrayOfItem` → `Item` 0..unbounded, `nillable`): `Pro_codigo_ncm`, `Pro_codigo_sec` y `Pro_ds` `s:string`
  0..1; `Pro_qty` `s:double`, `Pro_umed` `s:int`, `Pro_precio_uni`, `Imp_bonif` e `Imp_total` `s:double`, todos 1..1;
  `Iva_id` `s:short` 1..1. Manual (pág. 9): todos S salvo `Pro_codigo_sec` (N); `Iva_id` "Int"; `Pro_umed` remite a
  `BFEGetPARAM_UMed`; `Iva_id` remite a `BFEGetPARAM_Tipo_IVA`.
- `CbteAsoc` (`ArrayOfCbteAsoc` → `CbteAsoc` 0..unbounded, `nillable`): **solo** `Tipo_cbte` `s:short` 1..1, `Punto_vta`
  `s:int` 1..1 y `Cbte_nro` `s:long` 1..1. Los tres S en el manual (pág. 9). "Aclaración" (pág. 9-10): a facturas 01/06
  solo se asocia el 91 "remito electrónico"; a ND/NC 02/03, 01, 02, 03 o 91; a ND/NC 07/08, 06, 07, 08 o 91.

### 1. BFEGetPARAM_CondicionIvaReceptor

**Propósito.** "Recupera la condicion frente al IVA del receptor (para una clase de comprobante determinada o para todos
si no se especifica)" [WSDL]. **No está en el manual V1.1.**

Estructura idéntica a wsbfev1 [WSDL]:
- **Request**: `auth` (`ClsBFEAuthRequest` 0..1) y `ClaseCmp` `s:string` 0..1.
- **Response** `BFEResponse_CondicionIvaReceptor`: `BFEResultGet` (lista de `ClsBFEResponse_CondicionIvaReceptor`, con
  `Id` `s:int` 1..1, `Desc` y `Cmp_Clase` `s:string` 0..1), `BFEErr` y `BFEEvents`.

Semántica, valores y errores: los de wsbfev1 (`ClaseCmp` A o B; inválida → 4967 en wsbfev1; anexo de 11 condiciones)
[INFERIDO]. **NO VERIFICADO** en wsbfe.

```xml
<x:BFEGetPARAM_CondicionIvaReceptor><x:auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:auth><x:ClaseCmp>A</x:ClaseCmp></x:BFEGetPARAM_CondicionIvaReceptor>
```

Con token "abc" responde `BFEErr` 1000 y `BFEEvents` 102 [VIVO, `bfe-condiva-badtoken`].

### 2. BFEAuthorize

**Propósito.** "Autoriza un comprobante, devolviendo su CAE correspondiente" [WSDL]. "Recibe la información de
factura/lote de ingreso" y "Retorna la información del comprobante de ingreso agregándole el CAE otorgado. Ante
cualquier anomalía se retorna un código de error cancelando la ejecución del WS" [MAN pág. 7 y 10].

**Request**: `Auth` (`ClsBFEAuthRequest` 0..1) y `Cmp` (`ClsBFERequest` 0..1). `Cmp` en orden de esquema [WSDL; MAN
pág. 7-9]:

| # | Campo | XSD | Tipo man. | Man. | Descripción y límites |
|---:|---|---|---|---|---|
| 1 | `Id` | `s:long` 1..1 | Long | (vacío) | "Identificador del requerimiento"; > 0 (1014, pág. 11) |
| 2 | `Tipo_doc` | `s:short` 1..1 | Int | S | "Código de documento identificatorio del comprador"; clase A → 80 (1014) |
| 3 | `Nro_doc` | `s:long` 1..1 | Long | S | Número de identificación del comprador |
| 4 | `Zona` | `s:short` 1..1 | Short | S | Código de zona |
| 5 | `Tipo_cbte` | `s:short` 1..1 | Int | S | `BFEGetPARAM_Tipo_Cbte`; inválido → 1014 |
| 6 | `Punto_vta` | `s:int` 1..1 | Int | S | 1 a 99998, único para el requerimiento (1014). Ampliado a 5 dígitos en la V1.1 (pág. 2) |
| 7 | `Cbte_nro` | `s:long` 1..1 | Long | S | 1 a 99999999 (1014) |
| 8-14 | `Imp_total`, `Imp_tot_conc`, `Imp_neto`, `Impto_liq`, `Impto_liq_rni`, `Imp_op_ex`, `Imp_perc` | `s:double` 1..1 | Double | S | Mismas descripciones que wsbfev1 (pág. 9) |
| 15-16 | `Imp_iibb`, `Imp_perc_mun` | `s:double` 1..1 | — | — | En el XML de pág. 8, no en la tabla |
| 17 | `Imp_internos` | `s:double` 1..1 | Double | S | |
| 18 | `Imp_moneda_Id` | `s:string` 0..1 | "Double" (errata) | S | `BFEGetPARAM_MON` |
| 19 | `Imp_moneda_ctz` | `s:double` 0..1 | Double | **S** | En wsbfev1 el manual la pasa a N |
| 20 | `Fecha_cbte` | `s:string` 0..1 | String | S | `yyyymmdd`, ±5 días y mismo mes; si falta, "se asignará la fecha de proceso" (pág. 11) |
| 21 | `CondicionIVAReceptorId` | `s:int` 0..1 | — | — | **No está en el manual.** Obligatorio desde el 9-jun-2025 en homologación externa (evento 102) [VIVO]. Tabla: anexo de wsbfev1 [INFERIDO] |
| 22 | `CanMisMonExt` | `s:string` 0..1 | — | — | **No está en el manual.** En wsbfev1: S/N, misma moneda extranjera [INFERIDO] |
| 23 | `Items` | `ArrayOfItem` 0..1 | Item | S | |
| 24 | `CbtesAsoc` | `ArrayOfCbteAsoc` 0..1 | CbteAsoc | N | Reglas 1030-1039 |

**Response** `BFEAuthorizeResult` (`BFEResponseAuthorize`): `BFEResultAuth` (`ClsBFEOutAuthorize` 0..1), `BFEErr` y
`BFEEvents`. `ClsBFEOutAuthorize`: `Id` `s:long` 1..1, `Cuit` `s:long` 1..1, y `Cae`, `Fch_venc_Cae`, `Fch_cbte`,
`Resultado`, `Reproceso` y `Obs` `s:string` 0..1 [WSDL]. Manual (pág. 10-11), todos S: "Identificador del
requerimiento", "Cuit del contribuyente", "CAE", "Fecha de vencimiento del CAE", "Fecha de comprobante", "Resultado",
"Indica si es un reproceso "S" o "N"" y "Observaciones, motivo de rechazo según tabla de motivos". El manual **no
publica** la tabla de motivos.

Ejemplo **derivado del WSDL** (no hay grabaciones reales de wsbfe). Datos de ejemplo:

```xml
<x:BFEAuthorize>
  <x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:Auth>
  <x:Cmp><x:Id>1</x:Id><x:Tipo_doc>80</x:Tipo_doc><x:Nro_doc>30000000007</x:Nro_doc><x:Zona>1</x:Zona><x:Tipo_cbte>1</x:Tipo_cbte><x:Punto_vta>1</x:Punto_vta><x:Cbte_nro>1</x:Cbte_nro><x:Imp_total>121</x:Imp_total><x:Imp_tot_conc>0</x:Imp_tot_conc><x:Imp_neto>100</x:Imp_neto><x:Impto_liq>21</x:Impto_liq><x:Impto_liq_rni>0</x:Impto_liq_rni><x:Imp_op_ex>0</x:Imp_op_ex><x:Imp_perc>0</x:Imp_perc><x:Imp_iibb>0</x:Imp_iibb><x:Imp_perc_mun>0</x:Imp_perc_mun><x:Imp_internos>0</x:Imp_internos><x:Imp_moneda_Id>PES</x:Imp_moneda_Id><x:Imp_moneda_ctz>1</x:Imp_moneda_ctz><x:Fecha_cbte>20261002</x:Fecha_cbte><x:CondicionIVAReceptorId>1</x:CondicionIVAReceptorId>
    <x:Items><x:Item><x:Pro_codigo_ncm>7308.10.00</x:Pro_codigo_ncm><x:Pro_ds>Producto</x:Pro_ds><x:Pro_qty>1</x:Pro_qty><x:Pro_umed>7</x:Pro_umed><x:Pro_precio_uni>100</x:Pro_precio_uni><x:Imp_bonif>0</x:Imp_bonif><x:Imp_total>121</x:Imp_total><x:Iva_id>5</x:Iva_id></x:Item></x:Items>
  </x:Cmp>
</x:BFEAuthorize>
```

La respuesta tendría la forma de wsbfev1: `BFEResultAuth` con `Cae`, `Fch_venc_Cae`, `Resultado` `A` y `Reproceso`, más
`BFEErr` 0/OK [INFERIDO de la identidad de tipos]. Con token "abc": solo `BFEErr` 1000 y `BFEEvents` 102 [VIVO,
`bfe-authorize-badtoken`].

### 3. BFEGetCMP

**Propósito.** "Recupera los datos completos de un comprobante ya autorizado" [WSDL]. "Retorna los detalles de un
comprobante ya enviado y autorizado" [MAN pág. 13].

**Request**: `Auth` y `Cmp` (`ClsBFEGetCMP`: `Tipo_cbte` `s:short`, `Punto_vta` `s:int` y `Cbte_nro` `s:long`, todos
1..1) [WSDL; MAN pág. 13].

**Response** `BFEGetCMPResult`: `BFEResultGet` (`ClsBFEGetCMPR` 0..1), `BFEErr` y `BFEEvents`. `ClsBFEGetCMPR` en orden
[WSDL]:

1. `Id` `long` 1..1, `Cuit` `long` 1..1.
2. `Tipo_doc` `short`, `Nro_doc` `long`, `Tipo_cbte` `short`, `Punto_vta` `int`, `Cbte_nro` `long`, todos 1..1.
3. `Imp_total`, `Imp_tot_conc`, `Imp_neto`, `Impto_liq`, `Impto_liq_rni`, `Imp_op_ex`, `Imp_perc`, `Imp_iibb`,
   `Imp_perc_mun` e `Imp_internos`, `double` 1..1.
4. `Imp_moneda_Id` `string` 0..1, `Imp_moneda_ctz` `double` **1..1**.
5. `Fecha_cbte_orig`, `Fecha_cbte_cae`, `Fch_venc_Cae`, `Cae`, `Resultado` y `Obs`, `string` 0..1.
6. `CondicionIVAReceptorId` `int` 0..1, `CanMisMonExt` `string` 0..1.
7. `Items` (`ArrayOfItem`) y `CbtesAsoc` (`ArrayOfCbteAsoc`), 0..1.

El manual (pág. 13-15):
- describe `Fecha_cbte_orig` ("Fecha de comprobante ingreso", N) y `Fecha_cbte_cae` ("Fecha de comprobante otorgado en
  caso de omitirla en la presentación", S);
- lista `Zona` y `Fecha_cae` ("Fecha de autorización"), que **no existen** en el WSDL;
- no menciona `Fch_venc_Cae`, `CondicionIVAReceptorId` ni `CanMisMonExt`.

Manda el WSDL.

**Error**: 1020 "Comprobante inexistente" (pág. 15). Con token "abc": `BFEErr` 1000 [VIVO, `bfe-getcmp-badtoken`].

### 4 a 10. Recuperadores de parámetros sin argumentos

Mismos tipos y nombres que en wsbfev1 [WSDL]. Cada uno responde `BFEResultGet` (lista) + `BFEErr` + `BFEEvents`:

| # | Operación | Elem. auth | Registro y campos (XSD) | Manual V1.1 |
|---:|---|---|---|---|
| 4 | `BFEGetPARAM_Tipo_doc` | `auth` | `ClsBFEResponse_Tipo_doc`: `Doc_Id` `short` 1..1, `Doc_Ds`, `Doc_vig_desde`, `Doc_vig_hasta` | **Sin sección** |
| 5 | `BFEGetPARAM_Tipo_IVA` | `auth` | `ClsBFEResponse_Tipo_IVA`: `IVA_Id` `short` 1..1, `IVA_Ds`, `IVA_vig_desde`, `IVA_vig_hasta` | §2.6, pág. 20-21. Mismas erratas que el de wsbfev1: `BFEGetPARAM_Tipo_Iva`, `Iva_Id`, respuesta rotulada `BFEGetPARAM_Tipo_CbteResponse`. "Retorna el universo de tipos de comprobante validos" (sic) |
| 6 | `BFEGetPARAM_Zonas` | `auth` | `ClsBFEResponse_Zon`: `Zon_Id` `short` 1..1, `Zon_Ds`, `Zon_vig_desde`, `Zon_vig_hasta` | §2.7, pág. 21-23. "Retorna el total de zonas validas" |
| 7 | `BFEGetPARAM_Tipo_Cbte` | `auth` | `ClsBFEResponse_Tipo_Cbte`: `Cbte_Id` `short` 1..1, `Cbte_Ds`, `Cbte_vig_desde`, `Cbte_vig_hasta` | §2.5, pág. 18-20. "Retorna el universo de tipos de comprobante validos" |
| 8 | `BFEGetPARAM_UMed` | `auth` | `ClsBFEResponse_UMed`: `Umed_Id` `short` 1..1, `Umed_Ds`, `Umed_vig_desde`, `Umed_vig_hasta` | **Sin sección** |
| 9 | `BFEGetPARAM_NCM` | **`Auth`** | `ClsBFEResponse_NCM`: `NCM_Codigo`, `NCM_Ds`, `NCM_Nota`, `NCM_vig_desde`, `NCM_vig_hasta` | §2.4, pág. 17-18. "Retorna el listado completo de código de productos autorizados" |
| 10 | `BFEGetPARAM_MON` | `auth` | `ClsBFEResponse_Mon`: `Mon_Id`, `Mon_Ds`, `Mon_vig_desde`, `Mon_vig_hasta` | §2.3, pág. 15-16. "Retorna el total de monedas validas" |

Los campos sin tipo indicado son `s:string` 0..1. En el manual, `*_vig_hasta` es N y el resto S. Errores: 1000 y 1001.
Con token "abc" y el nombre correcto del elemento: 1000 [VIVO, `bfe-badtoken`, `bfe-ncm-badtoken`].

### 11. BFEGetLast_CMP

**No está en el manual.** Estructura idéntica a wsbfev1 [WSDL]:
- **Request**: `Auth` de tipo `ClsBFE_LastCMP` con `Token`, `Sign`, `Cuit` `long` 1..1, `Pto_venta` `int` 1..1 y
  `Tipo_cbte` `short` 1..1.
- **Response**: `BFEResult_LastCMP` (`Cbte_nro` `long` 1..1, `Cbte_fecha` `string` 0..1), `BFEErr` y `BFEEvents`.

Con token "abc": 1000 [VIVO, `bfe-lastcmp-badtoken`].

```xml
<x:BFEGetLast_CMP><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit><x:Pto_venta>1</x:Pto_venta><x:Tipo_cbte>1</x:Tipo_cbte></x:Auth></x:BFEGetLast_CMP>
```

### 12. BFEDummy

**No está en el manual.** Sin autenticación. Respuesta `BFEDummyResult` (`DummyResponse`: `AppServer`, `DbServer` y
`AuthServer`), sin `BFEErr` [WSDL]. Real [VIVO, homologación]:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.bfe/"><ambiente>Homologacion - srt</ambiente><fecha>2026-10-02T15:07:37.0789699-03:00</fecha><id>3.0.0.0</id></FEHeaderInfo></soap:Header><soap:Body><BFEDummyResponse xmlns="http://ar.gov.afip.dif.bfe/"><BFEDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></BFEDummyResult></BFEDummyResponse></soap:Body></soap:Envelope>
```

También responde por `GET`/`POST` en `…/wsbfe/service.asmx/BFEDummy` [VIVO].

### 13. BFEGetLast_ID

**No está en el manual.** Request: `Auth` (`ClsBFEAuthRequest`). Response: `BFEResultGet` (`ClsBFEResponse_LastID`:
`Id` `long` 1..1), `BFEErr` y `BFEEvents` [WSDL]. WSDL: "Recupera el ultimo ID y su fecha" (no hay fecha). Con token
"abc": 1000 [VIVO, `bfe-lastid-badtoken`].

### 14. BFEGetCotizacion

**No está en el manual.** Request: `Auth`, `MonId` `string` 0..1 y `FchCotiz` `string` 0..1. Response: `BFEResultGet`
(`Cotizacion`: `MonId` `string` 0..1, `MonCotiz` `double` 1..1 y `FchCotiz` `string` 0..1), `BFEErr` y `BFEEvents`
[WSDL]. Semántica y errores: los de wsbfev1 (`FchCotiz` `YYYYMMDD`, por defecto hoy; 4964, 4965 y 4966) [INFERIDO].
Con token "abc": 1000 [VIVO, `bfe-cotizacion-badtoken`].

```xml
<x:BFEGetCotizacion><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:Auth><x:MonId>DOL</x:MonId></x:BFEGetCotizacion>
```

### Discrepancias manual / WSDL (y cuál se toma)

| # | Manual V1.1 | WSDL / real | Se toma |
|---:|---|---|---|
| 1 | 7 operaciones | 14 | WSDL |
| 2 | `BFEAuthorize` sin `CondicionIVAReceptorId` ni `CanMisMonExt` | Están (0..1) | WSDL |
| 3 | `Imp_moneda_ctz` obligatorio | 0..1 en el esquema | Validar como obligatorio en negocio (manual), aceptar ausente en el esquema |
| 4 | `BFEGetCMP` con `Zona` y `Fecha_cae` | No existen; existe `Fch_venc_Cae` | WSDL |
| 5 | `BFEGetPARAM_Tipo_Iva`, `Iva_Id` | `BFEGetPARAM_Tipo_IVA`, `IVA_Id` | WSDL |
| 6 | `Auth`/`auth` mezclados en los ejemplos | Ver Autenticación | WSDL |
| 7 | `errcode`/`errmsg`/`Errmsg` | `ErrCode`/`ErrMsg` | WSDL |
| 8 | `Imp_moneda_Id` "Double"; `Tipo_doc`, `Tipo_cbte`, `Iva_id` "Int"; `Cuit` "string" en recuperadores | `string`; `short`; `long` | WSDL |
| 9 | `FEHeaderInfo` en sobre SOAP 1.2 | Según el request | Real |

## Validaciones y errores

### Cómo viajan

Igual que wsbfev1: un único `BFEErr` con HTTP 200. `ErrCode` 0 y `ErrMsg` `OK` en éxito [INFERIDO de la identidad de
tipos; en wsbfe no hay respuestas exitosas grabadas]. Observaciones en `BFEResultAuth/Obs`. 1014 como comodín: "Los
mensajes de error que aún no están contemplados salen por código 1014 incluyendo un texto que explica la causa exacta
del error" [MAN pág. 12].

Diferencias de reglas con wsbfev1 según los manuales:

| Tema | wsbfe (V1.1) | wsbfev1 (V3.0/V3.2) |
|---|---|---|
| Banda del tipo de cambio (1014) | "no podrá ser inferior al 20% ni superior en un 100%" del orientativo (pág. 12) | 2% / 400% en la tabla (historial: 20%/200%) |
| 1032 | "`<CbteAsoc><PtoVta>` mayor a 0 y menor a 99998" | Sin nombre de campo |
| 1035: tipos que admiten `CbtesAsoc` | 01, 02, 03, 06, 07, 08 | Además 91, 201, 202, 203, 206, 207, 208 |
| Opcionales (1015-1020) | No existen (no hay `Opcionales`) | Existen |
| FCE (4880-4956) | No existen | Existen |
| `Imp_moneda_ctz`, `CanMisMonExt`, condición IVA (4957-4963) | **No documentados**, aunque los campos están en el WSDL | Existen |
| `BFEGetCotizacion` (4964-4966), `BFEGetPARAM_CondicionIvaReceptor` (4967) | **No documentados**, aunque las operaciones existen | Existen |

**NO VERIFICADO**: qué códigos usa wsbfe para las reglas de RG 5616 y de cotización que su manual no cubre. Lo más
probable es que comparta los de wsbfev1 (4957-4967), porque los tipos son idénticos y el evento 102 habla de la misma
obligatoriedad [INFERIDO].

### Tabla completa de códigos del manual V1.1

Mismos datos en `wsbfe-codigos.json`.

| código | texto / condición (literal del manual) | efecto | dónde | página V1.1 |
|---|---|---|---|---|
| 500 | Error interno de aplicación. | error en `BFEErr` | * | 5 |
| 501 | Error interno de base de datos. | error en `BFEErr` | * | 5 |
| 502 | Error interno – Autorizador - Transacción Activa | error en `BFEErr` | * | 5 |
| 1000 | Usuario no autorizado a realizar esta operación | rechaza: solo `BFEErr` + `BFEEvents`, sin elemento de resultado | * · `Auth` | 11 (y en cada recuperador) |
| 1001 | Cuit solicitante no se encuentra entre sus representados | rechaza: solo `BFEErr` + `BFEEvents`, sin elemento de resultado | * · `Auth/Cuit` | 11 (y en cada recuperador) |
| 1014 | Tipo de dato y longitud de cada campo | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize | 11 |
| 1014 | Identificador del requerimiento sea mayor que 0. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Id` | 11 |
| 1014 | Campo punto_vta se encuentre entre 1 y 99998 y que sea único para el requerimiento. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Punto_vta` | 11 |
| 1014 | Tipo de comprobante inválido. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Tipo_cbte` | 11 |
| 1014 | Campo cbte_nro esté entre 1 y 99999999. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Cbte_nro` | 11 |
| 1014 | El tipo de documento debe ser igual a 80 (CUIT) en comprobantes tipo A. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Tipo_doc` | 11 |
| 1014 | No es una fecha valida. Debe ser numérico de 8 con formato (yyyymmdd). | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Fecha_cbte` | 11 |
| 1014 | No podrá exceder el mes de la fecha de envío del pedido de autorización. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Fecha_cbte` | 11 |
| 1014 | La fecha debe estar incluida en el periodo +- 5 días de la fecha de presentación. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Fecha_cbte` | 11 |
| 1014 | Se valida que la suma de importes de los ítems sea menor igual a los importes totales del comprobante. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Items` | 11-12 |
| 1014 | Se valida que el importe de operaciones exentas sea mayor a 0 en los casos donde exista alguna item de factura con Iva exento | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Imp_op_ex` | 12 |
| 1014 | El tipo de cambio no podrá ser inferior al 20% ni superior en un 100% del que suministra AFIP como orientativo de acuerdo a la cotización oficial. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Imp_moneda_ctz` | 12 |
| 1014 | Valor inválido en campo (a este código se le agregará una descripción detallada del origen del error (nombre de campo y causa)) | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize | 12 |
| 1030 | Si envía CbtesAsoc, CbteAsoc es obligatorio y no puede estar vacío | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc` | 12 |
| 1031 | De enviarse el tag CbteAsoc debe enviarse &lt;CbteAsoc>&lt;Tipo>mayor a 0 | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 12 |
| 1032 | De enviarse el tag CbteAsoc debe enviarse &lt;CbteAsoc>&lt;PtoVta> mayor a 0 y menor a 99998. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Punto_vta` | 12 |
| 1033 | De enviarse el tag CbteAsoc debe enviarse &lt;CbteAsoc>&lt;Nro> > a 0 y &lt; a 99999999. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cbte_nro` | 12 |
| 1034 | De enviarse el tag CbteAsoc, los comprobantes no deben repetirse. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc` | 12 |
| 1035 | De enviarse el tag &lt;CbtesAsoc>, entonces el campo tipo de comprobante &lt;Cmp>&lt;Tipo_cbte> a autorizar tiene que ser 01, 02, 03, 06, 07, 08 | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/Tipo_cbte` | 12 |
| 1036 | Para &lt;Cmp>&lt;Tipo_cbte> 01 o 06 solo puede asociarse el tipo de comprobante &lt;CbtesAsoc>&lt;Tipo_cbte> 91. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 12 |
| 1037 | Para &lt;Cmp>&lt;Tipo_cbte> 02 o 03 pueden asociarse los tipos de comprobante &lt;CbtesAsoc>&lt;Tipo_cbte> 01, 02, 03, 91. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 12 |
| 1038 | Para &lt;Cmp>&lt;Tipo_cbte> 07 u 08 pueden asociarse los tipos de comprobante &lt;CbtesAsoc>&lt;Tipo_cbte> 06, 07, 08, 91. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Tipo_cbte` | 12 |
| 1039 | Si el punto de venta del comprobante asociado (CbtesAsoc.Punto_vta) es electrónico y del tipo Bonos, el número de comprobante debe obrar en las bases del organismo para el punto de venta y tipo de comprobante informado. | rechaza: `BFEErr`, sin CAE (forma exacta NO VERIFICADA en wsbfe) | BFEAuthorize · `Cmp/CbtesAsoc/CbteAsoc/Cbte_nro` | 12 |
| 1020 | Comprobante inexistente | error en `BFEErr` (forma de la respuesta NO VERIFICADA) | BFEGetCMP · `Cmp` | 15 |

### Códigos vistos en el servicio que no están en el manual

| Código | Dónde | Texto | Fuente |
|---|---|---|---|
| `EventCode` 102 | `BFEEvents` de toda respuesta de homologación, también en errores | `IMPORTANTE: El dia 9 de junio de 2025 se actualizo la version del Web Service (WS) en el ambiente de Homologacion Externa en la cual se establece como obligatorio el campo Condicion Frente al IVA del receptor. Cabe destacar que la Resolucion General Nro 5616 indica que ese dato debe enviarse de manera obligatoria. Para mas informacion, consultar el manual en: https://www.arca.gob.ar/fe/ayuda/webservice.asp, https://www.arca.gob.ar/ws/documentacion/ws-factura-electronica.asp` | [VIVO] 2026-10-02 |

## Tablas y datos

- **El manual V1.1 no trae ninguna tabla** de parámetros ni datos de prueba.
- **No hay capturas** de las tablas de wsbfe: PyAfipWs no lo graba y sin certificado no se pueden pedir.
- Lo más razonable es cargar las tablas de wsbfev1 (ver `wsbfev1.md`, Tablas y datos: comprobantes, documentos, IVA,
  zonas, monedas, unidades, NCM y condición frente al IVA), **sin** los tipos FCE 201-208 [INFERIDO de que wsbfe no tiene
  los campos FCE y su 1035 no los lista]. **NO VERIFICADO**: si `BFEGetPARAM_Tipo_Cbte` de wsbfe devuelve o no
  201-208.
- Comprobantes asociables según el manual: 91 (remito electrónico), 01, 02, 03, 06, 07 y 08 (pág. 9-10 y 12).
- Condición frente al IVA del receptor: wsbfe no tiene anexo. Usar el anexo 3.1 de wsbfev1 (11 condiciones con su clase
  A o B) [INFERIDO].
- Datos de homologación (CUITs, puntos de venta): **no publicados**.

## Comportamiento a simular

El de wsbfev1, con estas diferencias:

1. **Sin FCE**:
   - `Cmp` no tiene `Fecha_vto_pago` ni `Opcionales`, y `CbteAsoc` no tiene `Cuit` ni `Fecha_cbte`. Si llegan esos
     elementos, el deserializador .NET los **ignora** (elemento desconocido). Se verificó en vivo: un `BFEAuthorize` con
     `Fecha_vto_pago`, `Opcionales` y `CbteAsoc/Cuit`/`Fecha_cbte` no da fault y llega a la validación del token
     (1000) [VIVO, `bfe-authorize-fcefields`]. Lo que haga el negocio con un `Tipo_cbte` 201 no se pudo ver (falta
     token).
   - `Tipo_cbte` admitidos: 1, 2, 3, 6, 7 y 8 (más el 91 solo como asociado) [INFERIDO de 1035-1038].
2. **Mismo estado** que wsbfev1:
   - comprobantes por (`Cuit`, `Tipo_cbte`, `Punto_vta`, `Cbte_nro`);
   - requerimientos por (`Cuit`, `Id`);
   - último `Id` por `Cuit` (`BFEGetLast_ID`);
   - último número y fecha (`BFEGetLast_CMP`).
3. **Idempotencia por `Id`**: `ClsBFEOutAuthorize` trae `Reproceso` "S"/"N" (pág. 11), igual que wsbfev1. Implementar
   igual: mismo `Id` ya aprobado → misma respuesta con `Reproceso` S; `Id` rechazado no se registra [INFERIDO por
   analogía; en wsbfe no hay ninguna evidencia real].
4. **CAE**: 14 dígitos; `Fch_venc_Cae` = `Fch_cbte` + 10 días [INFERIDO de wsbfev1].
5. **Fechas**: ±5 días de la fecha de generación, mismo mes; si no se envía, fecha de proceso (1014, pág. 11). Reloj
   configurable.
6. **Moneda**: `Imp_moneda_ctz` obligatoria según el manual (pág. 9), banda 20%-100% del orientativo (1014, pág. 12).
   `CanMisMonExt` y `BFEGetCotizacion` como en wsbfev1 [INFERIDO]. Dejar la banda configurable.
7. **RG 5616**: el mismo flag que wsbfev1 (`CondicionIVAReceptorId` obligatorio → 4963), **activo en modo
   homologación** por el evento 102. Producción corre una versión anterior (`2.0.1.0`): **NO VERIFICADO** si ya lo exige.
8. **`FEHeaderInfo`** con `ambiente` (`Homologacion - srt` / `Produccion - Se4`) e `id` configurable (`3.0.0.0`
   homologación, `2.0.1.0` producción).
9. **Evento** configurable; homologación hoy devuelve siempre el 102.
10. **Datos compartidos con wsbfev1**: mismo service id y mismo modelo. **NO VERIFICADO** si un comprobante emitido por
    wsbfe se ve desde wsbfev1 y si comparten numeración e `Id`. ArcaSim: almacén común opcional.
11. **Sin datos**: `BFEGetCMP` inexistente → 1020 (pág. 15). El resto (último número o `Id` sin historia, tablas vacías,
    cotización sin datos) → **NO VERIFICADO**; usar lo mismo que en wsbfev1.
12. **Imposible de simular fielmente**: CUIT representada (1001), comprobante asociado existente en las bases de ARCA
    (1039), banda contra el orientativo oficial, cotizaciones reales.

## No verificado

1. Cualquier respuesta **exitosa** de wsbfe: no hay grabaciones ni certificado. Toda la forma de éxito se infiere de
   wsbfev1.
2. Códigos y textos de las validaciones nuevas (moneda, `CanMisMonExt`, condición IVA, cotización, clase de
   comprobante) que el manual V1.1 no documenta.
3. Si producción (`2.0.1.0`) ya exige `CondicionIVAReceptorId` o tiene las operaciones nuevas activas, aunque su WSDL sea
   igual al de homologación.
4. Contenido de `BFEGetPARAM_Tipo_Cbte` (¿incluye 201-208?) y del resto de las tablas.
5. Reproceso por `Id` y semántica de `BFEGetLast_ID` en wsbfe.
6. Si wsbfe y wsbfev1 comparten base (comprobantes, numeración, `Id`).
7. Texto real de 1001 y de token vencido o de otro service id.
8. Tabla de "motivos" de `Obs` (el manual la menciona y no la publica).
9. Qué pasa si se envían `Opcionales` o `Fecha_vto_pago` (¿se ignoran?) y si se aceptan tipos FCE.
10. Quién debe usar wsbfe en lugar de wsbfev1 y si está deprecado. El catálogo dice RG 2557; el manual no cita ninguna RG.
