# wsseg

Factura electrónica de **Seguros de Caución** (RG 2668). El WSDL lo describe como "Web Service orientado al servicio de Empresas de Seguros". Las aseguradoras piden acá el CAE de un comprobante por llamada, con tipos 01, 02, 03, 06, 07 y 08 [M10 pág. 13]. Por los códigos de wsfev1, son factura, nota de débito y nota de crédito de clases A y B [INFERIDO]. Cada ítem puede llevar el número de **póliza** y de **endoso** de la caución. Es un servicio de nicho (catalogo.md §7). Quién puede usarlo además del certificado asociado (por ejemplo, una nómina de aseguradoras): **NO VERIFICADO** en el manual.

**Aviso importante: el servicio tiene fecha de baja.** Producción devolvió este evento el 2026-10-02 en una llamada con token inválido [VIVO]:

> `EventCode` 47: "El servicio WSSEG sera dado de baja. Cabe destacar que la Resolución General Nro 5866/2026 indica que este servicio sera reemplazado por WSFEV1 a partir del 01/01/2027. En caso de querer emitir comprobantes electrónicos de Seguros de Caución, el contribuyente podrá informarlos utilizando un Punto de Venta de tipo 'Comprobantes de Seguros de Caución - Webservices' por medio del servicio WSFEV1. En caso de requerir consultar comprobantes emitidos vía WSSEG, para tal fin se adecuó el método FECompConsultar para obtener ademas la póliza y el endoso"

Coincide con `normativa.md` §9.3 y su cronograma: `wsseg` sigue disponible hasta el 31/12/2026 (RG 5866/2026, art. 4). En wsfev1 el reemplazo son los opcionales 2901 (póliza) y 2902 (endoso) y los puntos de venta SEGWS (wsfev1-codigos.md, 10273 a 10282). Homologación todavía no muestra este aviso: su evento es otro (39, ver "Autenticación").

Fecha de relevamiento: 2026-10-02.

### Fuentes

| Id | Fuente | URL | Uso |
|---|---|---|---|
| **[M10]** | "Seguros de Caución ("wsseg") - R.G. N° 2.668", ARCA-SDG SIT, revisión **9 de junio de 2025**, versión **1.0** en el historial, 29 págs. | https://www.afip.gob.ar/fe/ayuda/documentos/wsseg-RG-2668.pdf (enlazado desde la página de homologación externa) | Manual principal. Leído completo. |
| **[M09]** | Mismo manual, revisión **21 de mayo de 2025**, versión **0.9**, 29 págs. | https://www.afip.gob.ar/ws/documentacion/manuales/WSSEG-ManualParaElDesarrollador_ARCA.pdf (enlazado desde ws-factura-electronica.asp) | Comparado línea por línea con [M10]. Leído completo. |
| **[WSDL]** | WSDL de homologación | `docs/arca/wsdl/wsseg-homologacion.wsdl` (42 064 bytes; vuelto a bajar el 2026-10-02: idéntico byte a byte) | Estructura. **Manda sobre los manuales.** Producción es idéntico salvo `soap:address`. |
| **[VIVO]** | Llamadas sin certificado del 2026-10-02 (homologación y producción) | scratchpad `vivo\seg-*` (request, headers y body de cada una) | Forma real de respuestas, errores de token, faults, eventos. |
| **[FEX]** | Manual de WSFEXV1, versión 3.1.0 del 18-ago-2025 | https://www.afip.gob.ar/ws/documentacion/manuales/WSFEX-Manualparaeldesarrollador_V3.1.1_ARCA.pdf | **Secundaria.** Mismo dialecto y mismos métodos `GetLast_ID`/`GetLast_CMP`/`Reproceso`. Solo para lo que el manual de wsseg no explica; todo lo que sale de acá va como [INFERIDO]. |
| **[NORM]** | `docs/arca/normativa.md` del repo | — | RG 5866 y la baja del servicio. |

Las páginas se citan como "pág. N": es la página física del PDF (1 a 29). Las dos revisiones tienen la misma paginación salvo donde se aclara. La pág. 29 está en blanco en las dos.

Marcas: **[M10]**, **[M09]**, **[WSDL]**, **[VIVO]** = verificado en fuente oficial o en el servicio. **[INFERIDO]** = deducido, sin texto oficial explícito. **NO VERIFICADO** = no se pudo comprobar.

### Los dos manuales: cuál manda

El cuerpo técnico es el mismo. Cambia la paginación de algunas tablas y solo hay **tres** diferencias de contenido:

| # | Tema | [M09] 21-may-2025 | [M10] 9-jun-2025 |
|---|---|---|---|
| 1 | Historial | Termina en 0.9 (alta de `SEGGetPARAM_Ctz` y de los códigos 1003 a 1006) | Agrega **1.0 (09/06/2025)**: "Será obligatorio el campo Condición Frente al IVA del receptor, atento a la entrada en vigencia reglamentada por la Resolución General N°5616. Por tal motivo el código de observación 23 quedará en desuso." (pág. 3) |
| 2 | `Obs` 21, receptor APÓCRIFO | "Para todos los comprobantes donde el receptor se encuentra identificado como APÓCRIFO, salimos con el siguiente codigo de **rechazo**: 21 - LA CUIT RECEPTORA SE ENCUENTRA INACTIVA POR HABER SIDO INCLUÍDA EN LA CONSULTA DE FACTURAS APÓCRIFAS." (pág. 12) | "[...] identificado como APOCRIFO, salimos con la sig. **observación**: 21 - LA CUIT RECEPTORA SE ENCUENTRA INACTIVA POR HABER SIDO INCLUÍDA EN LA CONSULTA DE FACTURAS APÓCRIFAS. **NO PODRÁ COMPUTARSE EL CRÉDITO FISCAL**" (pág. 12) |
| 3 | Paginación | Obs. 22 y 23 en pág. 13. La fila de `fecha_cbte` empieza en pág. 13 y sigue en 14. La fila 1034 empieza en pág. 14 y sigue en 15 | Obs. 22 empieza en pág. 12 y sigue en 13. La fila de `fecha_cbte` entra completa en pág. 13. La fila 1034 entra completa en pág. 14 |

Todo lo demás coincide: campos, tipos, obligatoriedad, códigos, textos de error, ejemplos y anexo. Se comparó el texto completo con `diff` y las páginas con tablas, como imagen.

Cosas que **no** cambiaron en [M10] y quedaron incoherentes:
- La tabla de `Cmp` sigue diciendo `CondicionIVAReceptorId` Obligatorio **N** (pág. 11). Lo mismo dice el WSDL (`minOccurs="0"`). Solo el historial 1.0 dice que es obligatorio.
- El texto de 1032 sigue en futuro: "resultara obligatorio" (pág. 14).
- La obs. 23 sigue en la tabla de `Obs` (pág. 13). Sin la nota del historial no se sabría que está en desuso.

**Manda [M10]**, por tres razones:
1. Es la revisión más nueva por contenido: tiene la entrada 1.0 del historial, que [M09] no tiene.
2. Es la que ARCA publica para homologación externa, el ambiente que usa un desarrollador. El servicio hermano `wsbfe` devuelve en homologación un evento que dice que "El dia 9 de junio de 2025 se actualizo la version del Web Service (WS) en el ambiente de Homologacion Externa en la cual se establece como obligatorio el campo Condicion Frente al IVA del receptor" [VIVO, `vivo\bfe-badtoken.body`]. Que pase lo mismo en wsseg es [INFERIDO].
3. El texto de APÓCRIFO de [M09] contradice su propio historial: la entrada 0.5 dice "Se agrega a modo **observación** el código 21" (pág. 2). [M10] corrige esa contradicción.

Ojo: el PDF de [M09] se generó el 29-jul-2025 (metadato `creationDate`) y el de [M10] el 6-jun-2025. Es decir, el archivo de la página general se volvió a exportar después, pero con el contenido viejo. No es una versión posterior.

Para ArcaSim: comportamiento de [M10] por defecto y un flag para emular [M09] (ver "Comportamiento a simular"). Qué versión corre hoy producción: **NO VERIFICADO**. Producción y homologación informan el mismo `FEHeaderInfo/id` = `3.0.4.0` [VIVO]. Eso sugiere el mismo binario, pero no lo prueba.

### Lo que el WSDL tiene y ningún manual explica

Ni [M09] ni [M10] documentan esto:

- **Cuatro operaciones**: `SEGGetPARAM_Tipo_doc`, `SEGGetLast_CMP`, `SEGDummy` y `SEGGetLast_ID`. Están en el WSDL y responden en vivo; el manual no las menciona.
- **Campos de `Cmp` (`ClsSEGRequest`)**:
  - `Imp_otrib_prov` (`double`, **`minOccurs="1"`**): no aparece en ninguna tabla ni en ningún ejemplo.
  - `Imp_iibb` e `Imp_perc_mun`: aparecen en el XML de ejemplo (pág. 9) y en la tabla de validaciones (pág. 14), pero no en la tabla de campos.
- **Campos de `Item`**: `Imp_valor_aseg` (`double`, `minOccurs="1"`) e `Imp_moneda_vaseg` (`string`).
- **Respuesta de `SEGGetCMP`**: `Id`, `Cuit`, `Imp_iibb`, `Imp_perc_mun`, `Imp_otrib_prov` y `Fch_venc_Cae` no están en la tabla del manual.
- **Bindings `ServiceHttpGet` y `ServiceHttpPost`**, solo para `SEGDummy`, y el elemento global `DummyResponse`.
- **Nombres en minúscula**: en las cinco operaciones `SEGGetPARAM_*` el parámetro de autenticación se llama `auth`. En el resto se llama `Auth`. El manual siempre escribe `Auth`, y con esa grafía el servicio lo ignora (ver "Autenticación").

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **.NET ASMX** (`service.asmx`; los faults traen trazas de `System.Web.Services`) [VIVO] |
| Endpoint homologación | `https://wswhomo.afip.gov.ar/wsseg/service.asmx` [WSDL `soap:address`] [M10 pág. 7] |
| Endpoint producción | `https://servicios1.afip.gov.ar/wsseg/service.asmx` [WSDL de producción, `soap:address`] [M10 pág. 8] |
| WSDL | `?WSDL` sobre cada endpoint [M10 págs. 7-8]. Guardado: `docs/arca/wsdl/wsseg-homologacion.wsdl`. Todo el esquema va inline, sin imports |
| Página de ayuda ASMX | El manual da `http://wswhomo.afip.gov.ar/wsseg/service.asmx?op=<Operación>` (con `http`) en cada método (pág. 9 y siguientes). Es la página HTML de ayuda de ASMX, no un endpoint SOAP |
| `targetNamespace` | **`http://ar.gov.afip.dif.seg/`**, en minúsculas [WSDL]. En los requests de ejemplo el manual escribe `http://ar.gov.afip.dif.SEG/` (págs. 9, 17, 19, 21, 23). Es una errata: con ese namespace el servicio no lee los parámetros [VIVO, `seg-ns-upper`] |
| `elementFormDefault` | `qualified`: todos los hijos van en el namespace del servicio [WSDL] |
| portType | `ServiceSoap` (11 operaciones), `ServiceHttpGet` y `ServiceHttpPost` (solo `SEGDummy`) |
| Bindings / ports | `ServiceSoap` (SOAP 1.1), `ServiceSoap12` (SOAP 1.2), `ServiceHttpGet` (`GET /SEGDummy`), `ServiceHttpPost` (`POST /SEGDummy`, `application/x-www-form-urlencoded`). Los cuatro usan la misma URL. Estilo `document`/`literal` |
| Service | `Service`. `wsdl:documentation`: "Web Service orientado  al  servicio  de Empresas de Seguros " (con dobles espacios) |
| WSAA service id | **`wsseg`**. Fuente: [M10] y [M09], pág. 5, §1.3: "debe enviar el tag service con el valor "wsseg" y que la duración del mismo es de 12 hs". El certificado se asocia al WS de negocio "Operacion de Seguros de Caucion - SEG" (misma página). Coincide con catalogo.md §3.1 |
| SOAPAction | `http://ar.gov.afip.dif.seg/` + nombre de la operación, igual en SOAP 1.1 y 1.2 [WSDL] |
| Header de respuesta | `soap:Header/FEHeaderInfo` (namespace del servicio) con `ambiente`, `fecha` e `id` [M10 págs. 5-6] [VIVO] |

Lista de `SOAPAction`, en el orden del WSDL:

```
http://ar.gov.afip.dif.seg/SEGAuthorize
http://ar.gov.afip.dif.seg/SEGGetCMP
http://ar.gov.afip.dif.seg/SEGGetPARAM_Tipo_doc
http://ar.gov.afip.dif.seg/SEGGetPARAM_Tipo_IVA
http://ar.gov.afip.dif.seg/SEGGetPARAM_Tipo_Cbte
http://ar.gov.afip.dif.seg/SEGGetPARAM_MON
http://ar.gov.afip.dif.seg/SEGGetPARAM_Ctz
http://ar.gov.afip.dif.seg/SEGGetLast_CMP
http://ar.gov.afip.dif.seg/SEGDummy
http://ar.gov.afip.dif.seg/SEGGetLast_ID
http://ar.gov.afip.dif.seg/SEGGetCondicionIvaReceptor
```

Respuesta: `<{Op}Response xmlns="http://ar.gov.afip.dif.seg/">` con un hijo `<{Op}Result>` [WSDL] [VIVO].

### `FEHeaderInfo`

| Ambiente | `ambiente` | `id` | Fuente |
|---|---|---|---|
| Homologación | `HomologacionExterno - srt` | `3.0.4.0` | [VIVO] 2026-10-02 |
| Producción | `Produccion - sr5` (dummy), `Produccion - sr3` (otra llamada) | `3.0.4.0` | [VIVO] 2026-10-02 |
| Manual, testing | `Desarrollo - Clo` | `1.0.3.0` | [M10] pág. 5-6 |
| Manual, producción | `Produccion -Pto` | `1.0.3.0` | [M10] pág. 6 |

`fecha` es hora local con offset `-03:00` y hasta 7 decimales (`2026-10-02T15:15:02.52387-03:00`: el serializador recorta los ceros finales) [VIVO]. El sufijo de `ambiente` cambia entre llamadas: parece ser el nodo que atendió [INFERIDO]. El manual aclara que procesar este header no es obligatorio (pág. 5).

### Comportamiento HTTP observado [VIVO, homologación, 2026-10-02]

| Caso | Respuesta | Archivo |
|---|---|---|
| SOAP 1.1 (`text/xml; charset=utf-8` + `SOAPAction`) | 200 `text/xml; charset=utf-8`, envelope 1.1 en una sola línea, con `<?xml version="1.0" encoding="utf-8"?>` | `seg-dummy` |
| SOAP 1.2 (`application/soap+xml; charset=utf-8; action="..."`) | 200 `application/soap+xml; charset=utf-8`, envelope `http://www.w3.org/2003/05/soap-envelope` con prefijo `soap:` y `FEHeaderInfo` | `seg-dummy-soap12` |
| SOAP 1.1 sin header `SOAPAction` | 200 OK: enruta por el elemento del Body | `seg-dummy-noaction` |
| `SOAPAction` inexistente (`.../SEGXXX`) | **HTTP 500**, `soap:Fault`, `faultcode` `soap:Client`, `faultstring` `System.Web.Services.Protocols.SoapException: Server did not recognize the value of HTTP Header SOAPAction: http://ar.gov.afip.dif.seg/SEGXXX.` + stack trace. **Sin** `FEHeaderInfo` | `seg-badaction` |
| Valor no numérico en un `long` (`<Cuit>abc</Cuit>`) | **HTTP 500**, `soap:Fault` `soap:Client`, `Server was unable to read request. ---> System.InvalidOperationException: There is an error in XML document (1, 235). ---> System.FormatException: Input string was not in a correct format.` + stack trace. **Con** `FEHeaderInfo` (a diferencia del fault anterior) | `seg-cuit-abc` |
| XML mal formado (cortado) | **HTTP 400**, `Content-Type: text/xml; charset=utf-8`, cuerpo vacío | `seg-malformed` |
| Wrapper en el namespace del manual (`http://ar.gov.afip.dif.SEG/`) | 200; los parámetros se ignoran y sale `1000` "Usuario no autorizado a realizar esta operacion. " | `seg-ns-upper` |
| `<Auth>` en una operación que espera `auth` (o al revés) | 200; el elemento se ignora y sale el mismo `1000` | `seg-param-AuthUpper`, `seg-lastid-authLower` |
| `GET /wsseg/service.asmx/SEGDummy` | 200 `text/xml; charset=utf-8`; documento `DummyResponse` **indentado** en varias líneas, sin envelope (ver `SEGDummy`) | `seg-dummy-httpget` |
| `POST /wsseg/service.asmx/SEGDummy` (form vacío) | Igual que el GET | `seg-dummy-httppost` |
| `GET ?WSDL` | 200, idéntico al archivo guardado | — |

Headers de respuesta: el dummy trae `Cache-Control: private, max-age=0`. Las operaciones con `SEGErr` traen `Cache-Control: no-cache`, `Pragma: no-cache` y `Expires: -1`. Todas traen cookies del balanceador F5 (`f5avr..._session_`, `TS010b76f1`), que no hacen falta para operar.

Con esto, ArcaSim tiene que exponer `/wsseg/service.asmx` y servir `?WSDL` con las cuatro `address` apuntando a sí mismo.

## Autenticación

Tipo `ClsSEGAuthRequest` [WSDL]:

| Campo | Tipo XSD | Ocurrencia | Manual | Significado |
|---|---|---|---|---|
| `Token` | `string` | 0..1 | S | "Token devuelto por el WSAA" |
| `Sign` | `string` | 0..1 | S | "Sign devuelto por el WSAA" |
| `Cuit` | `long` | 1..1 | S | "Cuit contribuyente (representado o Emisora)" |

Fuente: [M10] págs. 10, 20, 21, 23, 25 y 27. En los requests de ejemplo de las págs. 21, 23, 25 y 26, el manual tipa `Cuit` como `string`. Manda el WSDL: es `long`, y un valor no numérico produce un fault (ver "Contrato").

El nombre del elemento **cambia según la operación**, y el deserializador .NET distingue mayúsculas [WSDL] [VIVO]:

| Operación | Elemento | Tipo |
|---|---|---|
| `SEGAuthorize`, `SEGGetCMP`, `SEGGetLast_ID`, `SEGGetCondicionIvaReceptor` | `Auth` | `ClsSEGAuthRequest` |
| `SEGGetPARAM_Tipo_doc`, `SEGGetPARAM_Tipo_IVA`, `SEGGetPARAM_Tipo_Cbte`, `SEGGetPARAM_MON`, `SEGGetPARAM_Ctz` | **`auth`** | `ClsSEGAuthRequest` |
| `SEGGetLast_CMP` | `Auth` | **`ClsSEG_LastCMP`**: `Token`, `Sign`, `Cuit`, más `Pto_venta` y `Tipo_cbte` |
| `SEGDummy` | — | No requiere autenticación |

El manual escribe `<Auth>` en todas (págs. 9, 17, 19, 21, 23, 25 y 26). En las operaciones `SEGGetPARAM_*` eso equivale a no mandar autenticación.

Respuestas de falla [VIVO, 2026-10-02; iguales en todas las operaciones probadas: `SEGAuthorize`, `SEGGetCMP`, `SEGGetPARAM_Tipo_Cbte`, `SEGGetPARAM_Ctz`, `SEGGetLast_CMP`, `SEGGetLast_ID`, `SEGGetCondicionIvaReceptor`]. Siempre HTTP 200:

| Caso | `ErrCode` | `ErrMsg` literal |
|---|---|---|
| `Token` no es base64 (`abc`) | 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc` (el token va al final, tal cual) |
| `Token` y `Sign` vacíos | 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: No validó la firma digital. ` (con espacio final) |
| Token XML bien formado en base64 con firma falsa | 1000 | Igual al anterior |
| Sin elemento de autenticación (o mal escrito, o en otro namespace) | 1000 | `Usuario no autorizado a realizar esta operacion. ` (con espacio final) |

Según el manual (texto con tilde, sin detalle) [M10 págs. 13, 21, 22, 24, 26, 28]:
- 1000 "Usuario no autorizado a realizar esta operación" (Verificación de Token y Firma).
- 1001 "CUIT solicitante no se encuentra entre sus representados" (págs. 13 y 21) o "Cuit solicitante no se encuentra entre sus representados" (págs. 22, 24, 26 y 28). Qué texto exacto devuelve el servicio: **NO VERIFICADO**.

Forma de la respuesta con error de autenticación [VIVO]. Viene **solo** `SEGErr` + `SEGEvents`, sin `SEGResultAuth`, `SEGResultGet` ni `SEGResult_LastCMP`:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.seg/"><ambiente>HomologacionExterno - srt</ambiente><fecha>2026-10-02T15:14:44.7410812-03:00</fecha><id>3.0.4.0</id></FEHeaderInfo></soap:Header><soap:Body><SEGAuthorizeResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGAuthorizeResult><SEGErr><ErrCode>1000</ErrCode><ErrMsg>Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</ErrMsg></SEGErr><SEGEvents><EventCode>39</EventCode><EventMsg>IMPORTANTE: Por motivos de mantenimiento, el servicio estara fuera de linea el domingo 14 de junio desde las 21:00 hs durante aproximadamente 3 horas. Agradecemos tu comprension y pedimos disculpas por las molestias ocasionadas.</EventMsg></SEGEvents></SEGAuthorizeResult></SEGAuthorizeResponse></soap:Body></soap:Envelope>
```

Eventos observados junto a los errores:
- Homologación: `EventCode` 39, el aviso de mantenimiento del "domingo 14 de junio" de arriba. Es un aviso viejo que quedó cargado.
- Producción: `EventCode` 47, el aviso de baja citado al principio [VIVO `seg-badtoken-prod`].

Orden de validación [INFERIDO de lo observado]:
1. La autenticación se valida **antes** que los datos de negocio. `SEGAuthorize` sin `Auth` y con `Id` = 0 da 1000, no 1014 (`seg-authorize-noauth-id0`). `SEGGetCondicionIvaReceptor` con token inválido y `ClaseCmp` = `Z` da 1000, no 1002 (`seg-condiva-badtoken`).
2. Recién después vienen 1001 y las validaciones propias.

El TA lo emite WSAA para el service `wsseg` y dura 12 horas (pág. 5). El formato del TA es tema de `wsaa.md`.

## Operaciones

Las 11 operaciones del `portType` `ServiceSoap`, en el orden del WSDL. Convenciones de las tablas:
- **XSD**: tipo y `minOccurs..maxOccurs` del WSDL.
- **Man.**: columna "Obligatorio" del manual. "—" si el manual no lista el campo.
- **Long.**: formato del manual. `N15,2` son 15 dígitos con 2 decimales; `C4` es un texto de 4.

Todo `double`, `int`, `long` y `short` con `minOccurs="1"` toma valor `0` si no viene: así deserializa .NET [INFERIDO, comportamiento observado en wsfev1.md §1.2, mismo stack]. El manual pide algunos de esos campos como obligatorios, pero el XSD no lo hace cumplir.

Envelope de request, para todos los ejemplos:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:x="http://ar.gov.afip.dif.seg/">
  <soapenv:Header/>
  <soapenv:Body> ... </soapenv:Body>
</soapenv:Envelope>
```

### Tipos compartidos

**`ClsSEGErr`** (elemento `SEGErr`, 0..1 en todas las respuestas salvo `SEGDummy`) [WSDL]:

| Campo | XSD | Man. | Significado |
|---|---|---|---|
| `ErrCode` | int 1..1 | S | Código de error |
| `ErrMsg` | string 0..1 | S | Mensaje de error |

Hay **un solo** error por respuesta, no una lista. El manual dice: "Ante cualquier anomalía se retorna un código de error cancelando la ejecución del WS." [M10 pág. 11].

El manual escribe estos nombres de varias formas: `errcode`/`errmsg` (págs. 6 y 24), `Errmsg` (págs. 18, 20 y 22) y `SEGEErr` (pág. 7). Manda el WSDL: `ErrCode`/`ErrMsg` [VIVO].

En la respuesta de `SEGAuthorize`, el manual describe `SEGErr` como "Información del error producido (0 – OK )" (pág. 12). Si en una respuesta exitosa viene `SEGErr` con `ErrCode` 0 o no viene: **NO VERIFICADO**. Solo hay respuestas reales con error.

Errores de infraestructura, "en la misma estructura" [M10 pág. 7]: 500 "Error interno de aplicación.", 501 "Error interno de base de datos.", 502 "Error interno – Autorizador - Transacción Activa".

**`ClsSEGEvents`** (elemento `SEGEvents`, 0..1) [WSDL]:

| Campo | XSD | Man. | Significado |
|---|---|---|---|
| `EventCode` | int 1..1 | S | "Código de evento (único e irrepetible)" [M10 pág. 7] |
| `EventMsg` | string 0..1 | S | Mensaje |

Es **un solo** evento, no una lista. El ejemplo del manual (pág. 7) es "eventid=1 eventmsg=”Por razones de mantenimiento este ws estará fuera de línea el 1 de enero del 2020”". El manual dice "(0 – OK )" (pág. 12); si sin eventos viene `EventCode` 0 o no viene el elemento: **NO VERIFICADO**. En vivo siempre vino uno (39 en homologación, 47 en producción). La excepción es `SEGDummy`, que no tiene `SEGEvents`.

**`Item`** (dentro de `ArrayOfItem`: 0..unbounded `Item`, `nillable="true"`). Se usa en el request de `SEGAuthorize` y en la respuesta de `SEGGetCMP` [WSDL]:

| Campo | XSD | Man. | Long. | Significado [M10 pág. 11] |
|---|---|---|---|---|
| `Poliza` | string 0..1 | N | C30 | Póliza |
| `Endoso` | string 0..1 | N | C30 | Endoso |
| `Ds` | string 0..1 | S | C4000 | "Descripción del producto" |
| `Qty` | double 1..1 | S | N18,4 | Cantidad |
| `Precio_uni` | double 1..1 | S | N18,4 | Precio unitario |
| `Imp_bonif` | double 1..1 | S | N18,4 | Importe bonificación |
| `Imp_total` | double 1..1 | S | N18,4 | Importe total |
| `Imp_valor_aseg` | double 1..1 | — | — | **No está en ningún manual.** Por el nombre, importe del valor asegurado [INFERIDO]; reglas **NO VERIFICADAS** |
| `Imp_moneda_vaseg` | string 0..1 | — | — | **No está en ningún manual.** Por el nombre, moneda del valor asegurado [INFERIDO]; reglas **NO VERIFICADAS** |
| `Iva_id` | short 1..1 | S | Int (N2) | "Código de IVA (ver método SEGGetPARAM_Tipo_IVA)" |

En los dos ejemplos del manual, `Imp_total` = `Qty` × `Precio_uni` − `Imp_bonif` (1 × 100 − 20 = 80; 1 × 1200 − 200 = 1000) [M10 págs. 15-17]. Si el servicio valida esa cuenta: **NO VERIFICADO**. El manual no lo dice.

**`DummyResponse`**: `AppServer`, `DbServer`, `AuthServer`, todos string 0..1 [WSDL].

### SEGAuthorize

Propósito: "Autoriza un comprobante, devolviendo  su CAE correspondiente" [WSDL]. "Recibe la información de factura/lote de ingreso" [M10 pág. 9]. "Retorna la información del comprobante de ingreso agregándole el CAE otorgado. Ante cualquier anomalía se retorna un código de error cancelando la ejecución del WS." [M10 pág. 11].

Un comprobante por llamada: `Cmp` es 0..1, no un array. La palabra "lote" del manual quedó de la versión inicial (ver 1014 "IMPORTE TOTAL POR LOTE").

**Request** `SEGAuthorize`: `Auth` (`ClsSEGAuthRequest`, 0..1) y `Cmp` (`ClsSEGRequest`, 0..1). Man.: `Auth` S, `Cmp` S, `Ítems` S (pág. 10).

`Cmp` (`ClsSEGRequest`), en orden de esquema [WSDL]; manual págs. 10-11:

| Campo | XSD | Man. | Long. | Significado | Restricciones y validaciones |
|---|---|---|---|---|---|
| `Id` | long 1..1 | (vacío en la tabla) | N15 | "Identificador del requerimiento" | Mayor que 0 (1014, pág. 13). Clave de idempotencia y reproceso: ver "Comportamiento a simular" |
| `Tipo_doc` | short 1..1 | S | Int (N2) | "Código de documento identificatorio del comprador" | Clase A: debe ser 80, CUIT (1014 "El tipo de documento debe ser igual a 80 (CUIT) en comprobantes tipo A.") |
| `Nro_doc` | long 1..1 | S | Long (N11) | "Nro. De identificación del comprador" | Dispara las obs. 21 y 22 según el padrón |
| `Tipo_cbte` | short 1..1 | S (el manual escribe `tipo_cbte`) | Int (N2) | "Tipo de comprobante (SEGGetPARAM_Tipo_Cbte)" | 01, 02, 03, 06, 07, 08 (1014 "Tipo de comprobante inválido.") |
| `Punto_vta` | int 1..1 | S | Int (N5) | Punto de venta | Entre 1 y 99998, "y que sea único para el requerimiento" (1014). Pasó de 4 a 5 dígitos en la versión 0.4, 01-10-2018 (pág. 2) |
| `Cbte_nro` | long 1..1 | S (el manual escribe `Cbt_nro`) | Long (N8) | "Nro. De comprobante" | Entre 1 y 99999999 (1014) |
| `Imp_total` | double 1..1 | S | N15,2 | "Importe total de la operación" | Tope de los importes parciales (1014) |
| `Imp_tot_conc` | double 1..1 | S | N15,2 | "Importe total de conceptos que no integran el precio neto gravado" | — |
| `Imp_neto` | double 1..1 | S | N15,2 | "Importe neto gravado" | — |
| `Impto_liq` | double 1..1 | S (escrito `impto_liq`) | N15,2 | "Importe liquidado" | — |
| `Impto_liq_rni` | double 1..1 | S (escrito `impto_liq_rni`) | N15,2 | "Impuesto liquidado a RNI o percepción a no categorizados" | — |
| `Imp_op_ex` | double 1..1 | S | N15,2 | "Importe de operaciones exentas" | ≤ `Imp_total`. Mayor que 0 si algún ítem tiene IVA exento (1014) |
| `Imp_perc` | double 1..1 | S | N15,2 | "Importe de percepciones" | ≤ `Imp_total` (1014; "percepciones o pagos a cuenta de impuestos nacionales") |
| `Imp_iibb` | double 1..1 | — | — | No está en la tabla. Por la tabla de validaciones, percepción de Ingresos Brutos [INFERIDO] | ≤ `Imp_total` (1014) |
| `Imp_perc_mun` | double 1..1 | — | — | No está en la tabla. Por la tabla de validaciones, percepción de impuestos municipales [INFERIDO] | ≤ `Imp_total` (1014) |
| `Imp_internos` | double 1..1 | S | N15,2 | "Importe de impuestos internos" | ≤ `Imp_total` (1014) |
| `Imp_moneda_Id` | string 0..1 | S | C4 | "Código de moneda(SEGGetPARAM_MON)" | Si es `PES`, `CanMisMonExt` no se informa o vale `N` (1035) |
| `Imp_moneda_ctz` | double 0..1 | S | N15,4 | "Cotización de moneda." | Obligatorio salvo `CanMisMonExt` = S y, si se informa, mayor que 0. "no podrá ser inferior al 20% ni superior en un 200%" de la cotización orientativa de ARCA (lectura exacta NO VERIFICADA). Con `CanMisMonExt` = S, igual a la oficial del día hábil anterior (1014). No puede superar en 1 a la oficial (1034) |
| `Imp_otrib_prov` | double **1..1** | — | — | **No está en ningún manual ni ejemplo.** Por el nombre, otros tributos provinciales [INFERIDO] | **NO VERIFICADO** |
| `Fecha_cbte` | string 0..1 | S | C8 `yyyymmdd` | "Fecha de comprobante" | ±5 días de la fecha de generación, dentro del mes de presentación. Si no se envía, se asigna la fecha de proceso (1014, pág. 13). La tabla la marca "S", pero el XSD la deja opcional y la regla de pág. 13 dice qué pasa si falta |
| `CanMisMonExt` | string 0..1 | N | C1 | "Marca que identifica si el comprobante se cancela en misma moneda del comprobante (moneda extranjera). Valores posibles S o N." | `S` o `N`, no vacío (1033). Con `PES`, ausente o `N` (1035). Agregado en la versión 0.8, 17-03-2025 |
| `CondicionIVAReceptorId` | int 0..1 | N (pág. 11, en [M09] y en [M10]) | Int (N2) | "Identificador de la condición frente al IVA del receptor. De informarse debe corresponder a la tabla “Condición frente al IVA del receptor”." | Valor de la tabla (1030). Válido para la clase (1031). Obligatorio por RG 5616 (1032; [M10] historial 1.0). Si falta, en [M09] sale la obs. 23 |
| `Items` | ArrayOfItem 0..1 | S | — | "Detalle de ítem" | Ver `Item` en "Tipos compartidos". La suma de los ítems tiene que ser ≤ a los totales (1014) |

Cantidad máxima de ítems: **NO VERIFICADO** (no hay límite en el manual ni en el XSD).

**Response** `SEGAuthorizeResult` (`SEGResponseAuthorize`): `SEGResultAuth` (`ClsSEGOutAuthorize`, 0..1), `SEGErr`, `SEGEvents`.

`SEGResultAuth`, en orden de esquema [WSDL]; manual pág. 12:

| Campo | XSD | Man. | Significado |
|---|---|---|---|
| `Id` | long 1..1 | S | "Identificador del requerimiento" (eco del `Id` enviado) [INFERIDO por el nombre] |
| `Cuit` | long 1..1 | S | "Cuit del contribuyente" |
| `Cae` | string 0..1 | S | "CAE". Longitud y formato: **NO VERIFICADO** en wsseg. El CAE de wsfexv1 es `String(C14)` [FEX pág. 17] |
| `Fch_venc_Cae` | string 0..1 | S | "Fecha de vencimiento del CAE". Formato y regla de cálculo: **NO VERIFICADO**. En wsfexv1 es `String(C8)` [FEX pág. 17] |
| `Fch_cbte` | string 0..1 | S | "Fecha de comprobante": la enviada o la asignada si se omitió [INFERIDO por pág. 13] |
| `Resultado` | string 0..1 | S | "Resultado". Valores posibles: **NO VERIFICADO** (el manual no los enumera) |
| `Reproceso` | string 0..1 | S | "Indica si es un reproceso “S” o “N”" |
| `Obs` | string 0..1 | S | "Observaciones, motivo de rechazo según tabla de motivos." Es **un solo string**, no una lista. Códigos documentados: 21, 22 y 23 (ver "Validaciones y errores"). El manual no trae ninguna "tabla de motivos". Formato cuando hay más de una observación: **NO VERIFICADO** |

Validaciones propias: 1000, 1001, 1014 (todas sus variantes), 1030 a 1035 y observaciones 21, 22 y 23. Detalle en "Validaciones y errores".

**Ejemplos.** El manual trae dos "Ejemplo Request" (págs. 15-17). No son requests SOAP: son un `ClsSEGRequest` serializado suelto, sin namespace, sin envelope y sin `Auth`. Además usan el **mismo `Id` (45)** con números distintos (6 y 7). Ejemplo 1 [M10 págs. 15-16], con el mismo contenido y los saltos de línea compactados:

```xml
<ClsSEGRequest xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
<Id >45</Id> <Tipo_doc >80</Tipo_doc> <Nro_doc >33693450239</Nro_doc> <Tipo_cbte >1</Tipo_cbte> <Punto_vta >1900</Punto_vta>
<Cbte_nro >6</Cbte_nro> <Imp_total >96.8</Imp_total> <Imp_tot_conc >0</Imp_tot_conc> <Imp_neto >80</Imp_neto>
<Impto_liq >16.8</Impto_liq> <Impto_liq_rni >0</Impto_liq_rni> <Imp_op_ex >0</Imp_op_ex> <Imp_perc >0</Imp_perc>
<Imp_iibb >0</Imp_iibb> <Imp_perc_mun >0</Imp_perc_mun> <Imp_internos >0</Imp_internos> <Imp_moneda_Id >PES</Imp_moneda_Id>
<Imp_moneda_ctz >1</Imp_moneda_ctz> <Fecha_cbte >20091006</Fecha_cbte>
<Items>
  <Item><Poliza>SESC8301/2009</Poliza><Endoso>84300</Endoso><Ds>TRC5 caucion contra ARCA por 10kusd </Ds><Qty>1</Qty><Precio_uni>0</Precio_uni><Imp_bonif>0</Imp_bonif><Imp_total>0</Imp_total><Iva_id>5</Iva_id></Item>
  <Item><Poliza></Poliza><Endoso></Endoso><Ds>Total por poliza/s anteriores </Ds><Qty>1</Qty><Precio_uni>100</Precio_uni><Imp_bonif>20</Imp_bonif><Imp_total>80</Imp_total><Iva_id>5</Iva_id></Item>
</Items>
</ClsSEGRequest>
```

El ejemplo 2 (págs. 16-17) es igual con `Cbte_nro` 7, `Imp_total` 1210, `Imp_neto` 1000, `Impto_liq` 210 y `Fecha_cbte` 20091007. Tiene un solo ítem: `Poliza` `SESC8302/2009`, `Endoso` `843220/23`, `Ds` `TRC5 caucion contra ARCA por 100kusd `, `Precio_uni` 1200, `Imp_bonif` 200, `Imp_total` 1000, `Iva_id` 5.

En los dos ejemplos, `Impto_liq` = 21 % de `Imp_neto` con `Iva_id` 5, y la suma de `Item/Imp_total` = `Imp_neto` [INFERIDO de los números; en wsfev1 el id 5 de IVA es 21 %, wsfev1.md §7.4].

Request SOAP equivalente, **derivado del WSDL** a partir del ejemplo 1 (no lo trae el manual):
- `Auth` y namespace correcto.
- Lleva `Imp_otrib_prov` e `Imp_valor_aseg`, que el XSD declara 1..1 y el manual no menciona. Van en `0`, que es lo que el servidor asumiría si faltan [INFERIDO].
- `CondicionIVAReceptorId` 1 (Responsable Inscripto, clase A) por [M10].
- El token, la CUIT emisora y el `Id` son de relleno.

```xml
<x:SEGAuthorize>
  <x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:Auth>
  <x:Cmp>
    <x:Id>45</x:Id><x:Tipo_doc>80</x:Tipo_doc><x:Nro_doc>33693450239</x:Nro_doc><x:Tipo_cbte>1</x:Tipo_cbte>
    <x:Punto_vta>1900</x:Punto_vta><x:Cbte_nro>6</x:Cbte_nro><x:Imp_total>96.8</x:Imp_total><x:Imp_tot_conc>0</x:Imp_tot_conc>
    <x:Imp_neto>80</x:Imp_neto><x:Impto_liq>16.8</x:Impto_liq><x:Impto_liq_rni>0</x:Impto_liq_rni><x:Imp_op_ex>0</x:Imp_op_ex>
    <x:Imp_perc>0</x:Imp_perc><x:Imp_iibb>0</x:Imp_iibb><x:Imp_perc_mun>0</x:Imp_perc_mun><x:Imp_internos>0</x:Imp_internos>
    <x:Imp_moneda_Id>PES</x:Imp_moneda_Id><x:Imp_moneda_ctz>1</x:Imp_moneda_ctz><x:Imp_otrib_prov>0</x:Imp_otrib_prov>
    <x:Fecha_cbte>20091006</x:Fecha_cbte><x:CondicionIVAReceptorId>1</x:CondicionIVAReceptorId>
    <x:Items>
      <x:Item><x:Poliza>SESC8301/2009</x:Poliza><x:Endoso>84300</x:Endoso><x:Ds>TRC5 caucion contra ARCA por 10kusd </x:Ds><x:Qty>1</x:Qty><x:Precio_uni>0</x:Precio_uni><x:Imp_bonif>0</x:Imp_bonif><x:Imp_total>0</x:Imp_total><x:Imp_valor_aseg>0</x:Imp_valor_aseg><x:Iva_id>5</x:Iva_id></x:Item>
      <x:Item><x:Poliza></x:Poliza><x:Endoso></x:Endoso><x:Ds>Total por poliza/s anteriores </x:Ds><x:Qty>1</x:Qty><x:Precio_uni>100</x:Precio_uni><x:Imp_bonif>20</x:Imp_bonif><x:Imp_total>80</x:Imp_total><x:Imp_valor_aseg>0</x:Imp_valor_aseg><x:Iva_id>5</x:Iva_id></x:Item>
    </x:Items>
  </x:Cmp>
</x:SEGAuthorize>
```

Response exitosa: el manual solo trae el esqueleto con tipos (págs. 11-12), con el namespace en minúscula correcto. **No hay ninguna respuesta exitosa real** (sin certificado no se puede obtener). Forma según el WSDL, sin valores inventados:

```xml
<SEGAuthorizeResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGAuthorizeResult><SEGResultAuth><Id>long</Id><Cuit>long</Cuit><Cae>string</Cae><Fch_venc_Cae>string</Fch_venc_Cae><Fch_cbte>string</Fch_cbte><Resultado>string</Resultado><Reproceso>string</Reproceso><Obs>string</Obs></SEGResultAuth><SEGErr><ErrCode>int</ErrCode><ErrMsg>string</ErrMsg></SEGErr><SEGEvents><EventCode>int</EventCode><EventMsg>string</EventMsg></SEGEvents></SEGAuthorizeResult></SEGAuthorizeResponse>
```

Response con error de token [VIVO `seg-authorize-badtoken`]: ver "Autenticación". Viene sin `SEGResultAuth`.

### SEGGetCMP

Propósito: "Recupera los datos completos de un comprobante ya autorizado" [WSDL]. "Retorna los detalles de un comprobante ya enviado y autorizado." [M10 pág. 17].

**Request**: `Auth` (`ClsSEGAuthRequest`, 0..1) y `Cmp` (`ClsSEGGetCMP`, 0..1):

| Campo | XSD | Man. | Significado |
|---|---|---|---|
| `Cmp/Tipo_cbte` | short 1..1 | (sin tabla; aparece en el XML de pág. 17) | Tipo de comprobante |
| `Cmp/Punto_vta` | int 1..1 | idem | Punto de venta |
| `Cmp/Cbte_nro` | long 1..1 | idem | Número de comprobante |

**Response** `SEGGetCMPResult` (`SEGGetCMPResponse`): `SEGResultGet` (`ClsSEGGetCMPR`, 0..1), `SEGErr`, `SEGEvents`.

`SEGResultGet`, en orden de esquema [WSDL]; manual págs. 18-19:

| Campo | XSD | Man. | Significado y notas |
|---|---|---|---|
| `Id` | long 1..1 | — | No está en la tabla del manual (sí en su XML de pág. 17). `Id` del requerimiento original [INFERIDO] |
| `Cuit` | long 1..1 | — | Idem. CUIT emisora [INFERIDO] |
| `Tipo_doc` | short 1..1 | S (manual: `int`) | "Código de documento identificador del comprador" |
| `Nro_doc` | long 1..1 | S | "Nro. de identificación del comprador" |
| `Tipo_cbte` | short 1..1 | S (manual: `tipo_cbte`, `int`) | "Tipo de comprobante (ver anexo A)". El manual no tiene "anexo A" |
| `Punto_vta` | int 1..1 | S | Punto de venta |
| `Cbte_nro` | long 1..1 | S (manual: `Cbt_nro`) | "Nro. de comprobante" |
| `Imp_total` | double 1..1 | S | |
| `Imp_tot_conc` | double 1..1 | S | |
| `Imp_neto` | double 1..1 | S | |
| `Impto_liq` | double 1..1 | S | |
| `Impto_liq_rni` | double 1..1 | S | |
| `Imp_op_ex` | double 1..1 | S (manual: `imp_op_ex`) | |
| `Imp_perc` | double 1..1 | S | |
| `Imp_iibb` | double 1..1 | — | No está en la tabla |
| `Imp_perc_mun` | double 1..1 | — | No está en la tabla |
| `Imp_internos` | double 1..1 | S | |
| `Imp_moneda_Id` | string 0..1 | S (manual: `double`, errata) | "Código de moneda(ver anexo A)" |
| `Imp_moneda_ctz` | double **1..1** | S | En el request es 0..1; acá siempre viene |
| `Imp_otrib_prov` | double 1..1 | — | No está en ningún manual |
| `Fecha_cbte_orig` | string 0..1 | N | "Fecha de comprobante ingreso (yyyymmdd)": la que mandó el cliente; vacía si la omitió [INFERIDO] |
| `Fecha_cbte_cae` | string 0..1 | S | "Fecha de comprobante otorgado en caso de omitirla en la presentación (yyyymmdd)" |
| `Fch_venc_Cae` | string 0..1 | — | No está en la tabla. En su lugar el manual lista `Fecha_cae` "Fecha de autorización (yyyymmdd)", que **no existe en el WSDL**. Manda el WSDL. La fecha de autorización no se devuelve |
| `Cae` | string 0..1 | (en el XML, no en la tabla) | CAE |
| `Resultado` | string 0..1 | S | "Resultado" |
| `Obs` | string 0..1 | S | "Observaciones, motivo de rechazo según tabla de motivos." |
| `CanMisMonExt` | string 0..1 | N | Agregado en la versión 0.8 |
| `CondicionIVAReceptorId` | int 0..1 | N | Agregado en la versión 0.8 |
| `Items` | ArrayOfItem 0..1 | S | La tabla de ítems de pág. 19 omite `Endoso`, `Imp_valor_aseg` e `Imp_moneda_vaseg`, pero el tipo es el mismo `Item` del request. El XML de pág. 18 muestra `<Item xsi:nil="true" />` |

No hay `Reproceso` en esta respuesta.

Validaciones propias: 1020 "Comprobante inexistente" [M10 pág. 19]. Además 1000 en vivo. El manual no lista 1001 para este método; si aplica: **NO VERIFICADO**.

Ejemplo: el manual solo trae esqueletos con tipos (págs. 17-18). Request derivado del WSDL:

```xml
<x:SEGGetCMP>
  <x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:Auth>
  <x:Cmp><x:Tipo_cbte>1</x:Tipo_cbte><x:Punto_vta>1</x:Punto_vta><x:Cbte_nro>1</x:Cbte_nro></x:Cmp>
</x:SEGGetCMP>
```

Con token inválido devuelve `SEGErr` 1000 + `SEGEvents`, sin `SEGResultGet` [VIVO `seg-getcmp-badtoken`].

### Recuperadores de parámetros: SEGGetPARAM_Tipo_doc, SEGGetPARAM_Tipo_IVA, SEGGetPARAM_Tipo_Cbte, SEGGetPARAM_MON

Los cuatro tienen la misma forma:
- **Request**: solo `auth` (en minúscula, `ClsSEGAuthRequest`, 0..1).
- **Response**: `{Op}Result` con `SEGResultGet` (un array con un elemento por fila), `SEGErr` y `SEGEvents`.
- **Errores**: 1000 y 1001 en los que documenta el manual. `SEGGetPARAM_Tipo_doc` no está en el manual.

| Operación | Propósito [WSDL] | Tipo de `SEGResultGet` → elemento de fila | Campos de la fila (XSD) | Manual (longitud y obligatoriedad) | Pág. |
|---|---|---|---|---|---|
| `SEGGetPARAM_Tipo_doc` | "Recupera el listado de los  tipos de comprobante  y su codigo utilizables en servicio de autorizacion". El texto dice "comprobante" por error: devuelve tipos de **documento** | `ArrayOfClsSEGResponse_Tipo_doc` → `ClsSEGResponse_Tipo_doc` | `Doc_Id` short 1..1, `Doc_Ds` string 0..1, `Doc_vig_desde` string 0..1, `Doc_vig_hasta` string 0..1 | **No documentado** | — |
| `SEGGetPARAM_Tipo_IVA` | "Recupera el listado de las alicuotas de IVA  y su codigo utilizables en servicio de autorizacion" | `ArrayOfClsSEGResponse_Tipo_IVA` → `ClsSEGResponse_Tipo_IVA` | `IVA_Id` short 1..1, `IVA_Ds` string 0..1, `IVA_vig_desde` string 0..1, `IVA_vig_hasta` string 0..1 | `Iva_id` Short(N2) S; `IVA_ds` String(C250) S; `IVA_vig_desde` String(C8) S; `IVA_vig_hasta` String(C8) N | 23-24 |
| `SEGGetPARAM_Tipo_Cbte` | "Recupera el listado de los tipos de comprobante  y su codigo utilizables en servicio de autorizacion" | `ArrayOfClsSEGResponse_Tipo_Cbte` → `ClsSEGResponse_Tipo_Cbte` | `Cbte_Id` short 1..1, `Cbte_Ds` string 0..1, `Cbte_vig_desde` string 0..1, `Cbte_vig_hasta` string 0..1 | `Cbte_id` Short(N2) S; `Cbte_ds` String(C250) S; `Cbte_vig_desde` String(C8) S; `Cbte_vig_hasta` String(C8) N | 21-22 |
| `SEGGetPARAM_MON` | "Recupera el listado  de monedas y su codigo utilizables en servicio de autorizacion" | `ArrayOfClsSEGResponse_Mon` → `ClsSEGResponse_Mon` | `Mon_Id` string 0..1, `Mon_Ds` string 0..1, `Mon_vig_desde` string 0..1, `Mon_vig_hasta` string 0..1 | `Mon_id` String(C4) S; `Mon_ds` String(C250) S; `Mon_vig_desde` String(C8) S; `Mon_vig_hasta` String(C8) N | 19-21 |

Lo que dice el manual de cada uno:
- `SEGGetPARAM_MON`: "Retorna el total de monedas válidas." (pág. 20).
- `SEGGetPARAM_Tipo_Cbte`: "Retorna el universo de tipos de comprobante válidos." (pág. 21).
- `SEGGetPARAM_Tipo_IVA`: repite el texto de comprobantes (pág. 23), copiado por error.

Erratas del manual en este grupo (manda el WSDL):
- El manual titula los métodos `SEGGetPARAM_Tipo_cbte` y `SEGGetPARAM_Tipo_iva` y en el XML escribe `SEGGetPARAM_Tipo_Iva` (págs. 21 y 23). El nombre real es `SEGGetPARAM_Tipo_IVA`.
- La respuesta de ejemplo de IVA abre con `<SEGGetPARAM_Tipo_CbteResponse>`/`<SEGGetPARAM_Tipo_CbteResult>` y cierra con `</SEGGetPARAM_Tipo_IvaResult>` (págs. 23-24).
- La fila de IVA se llama `ClsSEGResponse_Tipo_Iva`, con `Iva_Id`, `Iva_Ds`, `Iva_vig_desde` e `Iva_vig_hasta`, y `Iva_Id` tipado `string` (pág. 23). En el WSDL es `ClsSEGResponse_Tipo_IVA` con `IVA_*` en mayúsculas e `IVA_Id` `short`. Como el serializador distingue mayúsculas, un cliente armado con el manual no leería estos campos.
- En la respuesta de monedas, `SEGGetPARAM_MONResponse mlns=` (pág. 20) es una errata por `xmlns=`.

Formato de las fechas de vigencia: `C8`, o sea `yyyymmdd` [INFERIDO]. Cómo viene una vigencia "hasta" vacía (string `NULL` como en wsfev1, vacío u omitida): **NO VERIFICADO**. Los valores de las tablas: **NO VERIFICADOS** (ver "Tablas y datos").

Request de ejemplo, derivado del WSDL (el request real `seg-badtoken.req.xml` usa esta forma):

```xml
<x:SEGGetPARAM_Tipo_Cbte><x:auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:auth></x:SEGGetPARAM_Tipo_Cbte>
```

Response, forma del manual con los nombres del WSDL:

```xml
<SEGGetPARAM_Tipo_CbteResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGGetPARAM_Tipo_CbteResult><SEGResultGet><ClsSEGResponse_Tipo_Cbte><Cbte_Id>short</Cbte_Id><Cbte_Ds>string</Cbte_Ds><Cbte_vig_desde>string</Cbte_vig_desde><Cbte_vig_hasta>string</Cbte_vig_hasta></ClsSEGResponse_Tipo_Cbte>…</SEGResultGet><SEGErr>…</SEGErr><SEGEvents>…</SEGEvents></SEGGetPARAM_Tipo_CbteResult></SEGGetPARAM_Tipo_CbteResponse>
```

### SEGGetPARAM_Ctz

Propósito: "Recupera la cotizacion registrada en el organismo para la moneda y fecha indicadas" [WSDL]. "Retorna la cotización registrada por el organismo según la moneda y fecha indicadas." [M10 pág. 27]. Lo agregó la versión 0.9 (21/05/2025), junto con los códigos 1003 a 1006 (pág. 2).

**Request**:

| Campo | XSD | Man. | Significado [M10 pág. 27] |
|---|---|---|---|
| `auth` | ClsSEGAuthRequest 0..1 | S | El manual escribe `Auth`: no funciona, ver "Autenticación" |
| `MonId` | string 0..1 | S | "Código de moneda a consultar cotización" |
| `FchCotiz` | string 0..1 | S | "Fecha de la cotización a consultar con el formato YYYYMMDD" |

**Response** `SEGGetPARAM_CtzResult` (`SEGResponse_Ctz`): `SEGResultGet` (`ClsSEGResponse_Ctz`, 0..1; **un solo objeto, no un array**), `SEGErr`, `SEGEvents`.

| Campo | XSD | Man. | Long. | Significado [M10 pág. 27] |
|---|---|---|---|---|
| `MonId` | string 0..1 | S | C3 | "Código de la moneda por la cual se consulto la cotización" |
| `MonCotiz` | double 1..1 | S | Double | "Cotización registrada por el organismo" |
| `FchCotiz` | string 0..1 | S | C8 | "Fecha a la cual pertenece la cotización" |

Que `FchCotiz` de la respuesta pueda ser distinta de la pedida (por ejemplo, el último día hábil anterior) es posible por la redacción, pero **NO VERIFICADO**. La moneda acá es `C3`; en `SEGGetPARAM_MON` y en `Imp_moneda_Id` es `C4`.

Validaciones [M10 pág. 28]: 1000, 1001, 1003 (moneda inválida), 1004 (moneda no informada), 1005 (fecha inválida) y 1006 (sin resultados). Con `MonId` = `PES`: **NO VERIFICADO**.

Ejemplo: el manual (págs. 26-27) usa `<SEGGetPARAM_Ctz>` sin namespace y `<Auth>`. Request derivado del WSDL, igual al probado en vivo (`seg-ctz-badtoken`, respondió 1000):

```xml
<x:SEGGetPARAM_Ctz><x:auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:auth><x:MonId>DOL</x:MonId><x:FchCotiz>20261001</x:FchCotiz></x:SEGGetPARAM_Ctz>
```

Response del manual (pág. 27), en el namespace correcto y sin `EventMsg`:

```xml
<SEGGetPARAM_CtzResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGGetPARAM_CtzResult><SEGResultGet><MonId>string</MonId><MonCotiz>double</MonCotiz><FchCotiz>string</FchCotiz></SEGResultGet><SEGErr><ErrCode>int</ErrCode><ErrMsg>string</ErrMsg></SEGErr><SEGEvents><EventCode>int</EventCode></SEGEvents></SEGGetPARAM_CtzResult></SEGGetPARAM_CtzResponse>
```

### SEGGetLast_CMP

Propósito: "Recupera el ultimos comprobante autorizado" [WSDL]. **No está en ningún manual de wsseg.**

**Request**: un único elemento `Auth` de tipo `ClsSEG_LastCMP`. Los parámetros de búsqueda van **dentro** de `Auth` [WSDL]:

| Campo | XSD | Significado |
|---|---|---|
| `Auth/Token` | string 0..1 | Token del TA |
| `Auth/Sign` | string 0..1 | Firma del TA |
| `Auth/Cuit` | long 1..1 | CUIT emisora |
| `Auth/Pto_venta` | int 1..1 | Punto de venta. Ojo: acá se llama `Pto_venta`, en el resto `Punto_vta` |
| `Auth/Tipo_cbte` | short 1..1 | Tipo de comprobante |

**Response** `SEGGetLast_CMPResult` (`SEGResponseLast_CMP`): `SEGResult_LastCMP` (`ClsSEG_LastCMP_Response`, 0..1), `SEGErr`, `SEGEvents`.

| Campo | XSD | Significado |
|---|---|---|
| `Cbte_nro` | long 1..1 | Último número autorizado para la CUIT, el punto de venta y el tipo [INFERIDO por el nombre y por [FEX págs. 36-37]: en wsfexv1, `FEXGetLast_CMP` "Retorna el último número de comprobante autorizado para el punto de venta y tipo de comprobante enviado", `Cbte_nro` Long(N8)] |
| `Cbte_fecha` | string 0..1 | Fecha de ese comprobante. En wsfexv1 es "Fecha (yyyymmdd)", `String(C8)` [FEX pág. 37] [INFERIDO] |

Validaciones: no documentadas. En vivo, 1000 sin `SEGResult_LastCMP` [`seg-lastcmp-badtoken`]. Respuesta cuando nunca se emitió ese tipo en ese punto de venta: **NO VERIFICADO**. Lo más probable es `Cbte_nro` 0, como en wsfev1 [INFERIDO]. Validación de `Tipo_cbte` o `Pto_venta` inválidos (en wsfexv1 existe el código 1606 [FEX pág. 37]): **NO VERIFICADO**.

Request derivado del WSDL (igual al probado en vivo):

```xml
<x:SEGGetLast_CMP><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit><x:Pto_venta>1</x:Pto_venta><x:Tipo_cbte>1</x:Tipo_cbte></x:Auth></x:SEGGetLast_CMP>
```

Response, forma del WSDL:

```xml
<SEGGetLast_CMPResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGGetLast_CMPResult><SEGResult_LastCMP><Cbte_nro>long</Cbte_nro><Cbte_fecha>string</Cbte_fecha></SEGResult_LastCMP><SEGErr>…</SEGErr><SEGEvents>…</SEGEvents></SEGGetLast_CMPResult></SEGGetLast_CMPResponse>
```

### SEGDummy

Propósito: "Metodo dummy para verificacion de funcionamiento" [WSDL]. **No está en el manual.** No requiere autenticación. El request es el elemento vacío `<x:SEGDummy/>`.

**Response** `SEGDummyResult` (`DummyResponse`): `AppServer`, `DbServer`, `AuthServer`, todos string 0..1. Sin `SEGErr` ni `SEGEvents` [WSDL].

Ejemplo real [VIVO `seg-dummy`, homologación]:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.seg/"><ambiente>HomologacionExterno - srt</ambiente><fecha>2026-10-02T15:07:37.5418384-03:00</fecha><id>3.0.4.0</id></FEHeaderInfo></soap:Header><soap:Body><SEGDummyResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></SEGDummyResult></SEGDummyResponse></soap:Body></soap:Envelope>
```

Producción responde igual, con `<ambiente>Produccion - sr5</ambiente>` [VIVO `seg-dummy-prod`].

Por los bindings HTTP (`GET` o `POST` a `.../service.asmx/SEGDummy`) [VIVO `seg-dummy-httpget`, `seg-dummy-httppost`], la respuesta es exactamente esta, con saltos de línea y dos espacios de sangría:

```xml
<?xml version="1.0" encoding="utf-8"?>
<DummyResponse xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://ar.gov.afip.dif.seg/">
  <AppServer>OK</AppServer>
  <DbServer>OK</DbServer>
  <AuthServer>OK</AuthServer>
</DummyResponse>
```

Valores distintos de `OK` cuando un componente falla: **NO VERIFICADO**.

### SEGGetLast_ID

Propósito: "Recupera el ultimo ID y su  fecha " [WSDL]. Pero la respuesta **no tiene fecha**: solo `Id`. **No está en ningún manual de wsseg.**

**Request**: `Auth` (`ClsSEGAuthRequest`, 0..1, con mayúscula).

**Response** `SEGGetLast_IDResult` (`SEGResponse_LastID`): `SEGResultGet` (`ClsSEGResponse_LastID`, 0..1), `SEGErr`, `SEGEvents`.

| Campo | XSD | Significado |
|---|---|---|
| `Id` | long 1..1 | Último `Id` de requerimiento recibido para la CUIT [INFERIDO por [FEX]: "Este método devuelve el último <Id> (el máximo) recibido por WSFEXV1" (pág. 9); "Retorna el último id de requerimiento para la cuit enviada" (pág. 35)] |

Si cuenta también los `Id` de pedidos rechazados: **NO VERIFICADO**. Qué devuelve una CUIT sin pedidos: **NO VERIFICADO** (probablemente `Id` 0 [INFERIDO]). En vivo, 1000 sin `SEGResultGet` [`seg-lastid-badtoken`].

```xml
<x:SEGGetLast_ID><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:Auth></x:SEGGetLast_ID>
```
```xml
<SEGGetLast_IDResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGGetLast_IDResult><SEGResultGet><Id>long</Id></SEGResultGet><SEGErr>…</SEGErr><SEGEvents>…</SEGEvents></SEGGetLast_IDResult></SEGGetLast_IDResponse>
```

### SEGGetCondicionIvaReceptor

Propósito: "Recupera la condicion frente al IVA del receptor (para una clase de comprobante determinada o para todos si no se especifica)." [WSDL]. "Retorna los identificadores de IVA disponibles para la clase de comprobante informado. En caso de no informar el campo retorna todas las combinaciones posibles de las clases de comprobantes con los identificadores de IVA que corresponden." [M10 pág. 25]. Lo agregó la versión 0.8 (17-03-2025), junto con el código 1002 (pág. 2).

**Request**:

| Campo | XSD | Man. | Significado [M10 pág. 25] |
|---|---|---|---|
| `Auth` | ClsSEGAuthRequest 0..1 | S | Con mayúscula, a diferencia de `SEGGetPARAM_*` |
| `ClaseCmp` | string 0..1 | N | "Clase de comprobante. Valores posibles A o B (para seguros). En caso de no informar el dicho campo, el metodo lista todas las combinaciones posibles de todas las clases de comprobantes." |

**Response** `SEGGetCondicionIvaReceptorResult`: `SEGResultGet` (`ArrayOfClsSEGResponse_CondicionIvaReceptor`, filas `ClsSEGResponse_CondicionIvaReceptor`), `SEGErr`, `SEGEvents`.

| Campo | XSD | Man. | Long. | Significado [M10 pág. 26] |
|---|---|---|---|---|
| `Id` | **int** 1..1 | S | Short(N2) | El manual dice "Código de IVA". Es el código de condición frente al IVA (Anexo 3.1) |
| `Desc` | string 0..1 | S | String(C250) | Descripción |
| `Cmp_Clase` | string 0..1 | S | String(C1) | Clase de comprobante |

Validaciones [M10 pág. 26]: 1000, 1001 y 1002, que significa `ClaseCmp` distinto de A o B. En vivo, la autenticación se evalúa antes que 1002 [`seg-condiva-badtoken`].

Cómo se arma la respuesta sin `ClaseCmp`: "todas las combinaciones posibles" sugiere **una fila por par (condición, clase)**. Con el Anexo serían 11 filas [INFERIDO]. Texto exacto de `Desc` y orden de las filas: **NO VERIFICADO**. Con `ClaseCmp` vacío (`<ClaseCmp/>`): **NO VERIFICADO** si se toma como ausente o da 1002.

El request del manual (pág. 25) no tiene namespace y cierra con `</ar:ClaseCmp>`, que es XML inválido. Request derivado del WSDL:

```xml
<x:SEGGetCondicionIvaReceptor><x:Auth><x:Token>…</x:Token><x:Sign>…</x:Sign><x:Cuit>20111111112</x:Cuit></x:Auth><x:ClaseCmp>A</x:ClaseCmp></x:SEGGetCondicionIvaReceptor>
```

Response del manual (pág. 25), en el namespace correcto y sin `EventMsg`:

```xml
<SEGGetCondicionIvaReceptorResponse xmlns="http://ar.gov.afip.dif.seg/"><SEGGetCondicionIvaReceptorResult><SEGResultGet><ClsSEGResponse_CondicionIvaReceptor><Id>int</Id><Desc>string</Desc><Cmp_Clase>string</Cmp_Clase></ClsSEGResponse_CondicionIvaReceptor>…</SEGResultGet><SEGErr><ErrCode>int</ErrCode><ErrMsg>string</ErrMsg></SEGErr><SEGEvents><EventCode>int</EventCode></SEGEvents></SEGGetCondicionIvaReceptorResult></SEGGetCondicionIvaReceptorResponse>
```

## Validaciones y errores

Cómo leer la tabla:
- **Efecto**:
  - **error**: `SEGErr` cancela la operación ("Ante cualquier anomalía se retorna un código de error cancelando la ejecución del WS", pág. 11); no se otorga CAE.
  - **observación**: texto en `SEGResultAuth/Obs`.
  - **evento**: `SEGEvents`.
  - **fault**: HTTP 500 `soap:Fault`.
- **Pág.**: página de [M10]; entre paréntesis, la de [M09] si cambia. "—" = sale de [VIVO].
- **Texto**: literal del manual. Cuando la columna "Mensaje de error" del manual está vacía, va entre corchetes la "Descripción de la validación".

| Código | Texto / condición | Efecto | Dónde | Pág. |
|---|---|---|---|---|
| 500 | Error interno de aplicación. | error | Todas, `SEGErr` | 7 |
| 501 | Error interno de base de datos. | error | Todas, `SEGErr` | 7 |
| 502 | Error interno – Autorizador - Transacción Activa | error | Todas, `SEGErr` | 7 |
| 1000 | Usuario no autorizado a realizar esta operación [Verificación de Token y Firma] | error | `SEGAuthorize`, `SEGGetPARAM_MON`, `SEGGetPARAM_Tipo_Cbte`, `SEGGetPARAM_Tipo_IVA`, `SEGGetCondicionIvaReceptor`, `SEGGetPARAM_Ctz`; `Auth`/`auth` | 13, 21, 22, 24, 26, 28 |
| 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: <token>` | error | Todas salvo `SEGDummy`; `Token` que no es base64 | — [VIVO] |
| 1000 | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: No validó la firma digital. ` | error | Todas salvo `SEGDummy`; `Token`/`Sign` vacíos o firma que no valida | — [VIVO] |
| 1000 | `Usuario no autorizado a realizar esta operacion. ` | error | Todas salvo `SEGDummy`; sin elemento de autenticación (o con otra grafía u otro namespace) | — [VIVO] |
| 1001 | CUIT solicitante no se encuentra entre sus representados | error | `SEGAuthorize`, `SEGGetPARAM_MON`; `Cuit` | 13, 21 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | error | `SEGGetPARAM_Tipo_Cbte`, `SEGGetPARAM_Tipo_IVA`, `SEGGetCondicionIvaReceptor`, `SEGGetPARAM_Ctz`; `Cuit` | 22, 24, 26, 28 |
| 1002 | El valor ingresado para la clase de comprobante no es valido. La clase de Comprobante es opcional, de ingresar un valor solo puede ser A o B, | error | `SEGGetCondicionIvaReceptor`; `ClaseCmp` | 26 |
| 1003 | El código de moneda ingresado es invalido. Verificar los codigos mediante el método SEGGetPARAM_MON | error | `SEGGetPARAM_Ctz`; `MonId` | 28 |
| 1004 | No ingreso el código de moneda. Ingresar un valor valido. Ver método SEGGetPARAM_MON | error | `SEGGetPARAM_Ctz`; `MonId` | 28 |
| 1005 | Campo FchCotiz No corresponde a una fecha valida con formato YYYYMMDD | error | `SEGGetPARAM_Ctz`; `FchCotiz` | 28 |
| 1006 | Sin Resultados: - Método SEGGetPARAM_Ctz | error (consulta sin resultados) | `SEGGetPARAM_Ctz` | 28 |
| 1014 | [Tipo de dato y longitud de cada campo] | error | `SEGAuthorize`; cualquier campo | 13 |
| 1014 | [Identificador del requerimiento sea mayor que 0.] | error | `SEGAuthorize`; `Cmp/Id` | 13 |
| 1014 | [Campo punto_vta se encuentre entre 1 y 99998 y que sea único para el requerimiento.] | error | `SEGAuthorize`; `Cmp/Punto_vta` | 13 |
| 1014 | Tipo de comprobante inválido. [Campo tipo_cbte sea: 01,02,03 06,07,08] | error | `SEGAuthorize`; `Cmp/Tipo_cbte` | 13 |
| 1014 | [Campo cbte_nro esté entre 1 y 99999999.] | error | `SEGAuthorize`; `Cmp/Cbte_nro` | 13 |
| 1014 | El tipo de documento debe ser igual a 80 (CUIT) en comprobantes tipo A. | error | `SEGAuthorize`; `Cmp/Tipo_doc` | 13 |
| 1014 | No es una fecha valida. Debe ser numérico de 8 con formato (yyyymmdd). | error | `SEGAuthorize`; `Cmp/Fecha_cbte` | 13 |
| 1014 | No podrá exceder el mes de la fecha de envío del pedido de autorización. | error | `SEGAuthorize`; `Cmp/Fecha_cbte` | 13 |
| 1014 | La fecha debe estar incluida en el periodo +- 5 días de la fecha de presentación. | error | `SEGAuthorize`; `Cmp/Fecha_cbte` (regla: "puede ser hasta 5 días anteriores o posteriores respecto de la fecha de generación. La misma no podrá exceder el mes de presentación. Si no se envía la fecha del comprobante se asignará la fecha de proceso") | 13 (13-14) |
| 1014 | Se valida que la suma de importes de los ítems sea menor igual a los importes totales del comprobante. [IMPORTE DE OPERACIONES EXENTAS, IMPORTE DE PERCEPCIONES O PAGOS A CUENTA DE IMPUESTOS NACIONALES, IMPORTE DE PERCEPCION DE INGRESOS BRUTOS, IMPORTE DE PERCEPCION DE IMPUESTOS MUNICIPALES, IMPORTE DE IMPUESTOS INTERNOS sean menores o iguales al IMPORTE TOTAL DE LA OPERACIÓN / IMPORTE TOTAL POR LOTE] | error | `SEGAuthorize`; `Imp_op_ex`, `Imp_perc`, `Imp_iibb`, `Imp_perc_mun`, `Imp_internos` frente a `Imp_total`; `Items` | 14 |
| 1014 | Se valida que el importe de operaciones exentas sea mayor a 0 en los casos donde exista alguna item de factura con Iva exento | error | `SEGAuthorize`; `Imp_op_ex`, `Item/Iva_id` | 14 |
| 1014 | El tipo de cambio no podrá ser inferior al 20% ni superior en un 200% del que suministra ARCA como orientativo de acuerdo a la cotización oficial. | error | `SEGAuthorize`; `Imp_moneda_Id`/`Imp_moneda_ctz` | 14 |
| 1014 | El campo <Imp_moneda_ctz> es obligatorio si no informa el campo CanMisMonExt = S, y de informarse debe ser mayor a 0. Si se indica que el pago del comprobante se realiza en la misma moneda extranjera que la factura, la cotización de la moneda provista debe coincidir exactamente con la registrada en las bases de ARCA para el día hábil anterior a la fecha de emisión del comprobante, si esta es anterior a la fecha actual, o bien con la registrada para el día hábil anterior a la fecha actual, si la fecha de emisión es posterior a esta. En caso contrario, se puede omitir el campo de Cotización de Moneda. | error | `SEGAuthorize`; `Imp_moneda_Id`/`Imp_moneda_ctz`/`CanMisMonExt` | 14 |
| 1014 | Valor inválido en campo (a este código se le agregará una descripción detallada del origen del error (nombre de campo y causa)) | error | `SEGAuthorize`; cualquier caso no contemplado ("Otros errores") | 15 |
| 1020 | Comprobante inexistente | error | `SEGGetCMP`; `Cmp` | 19 |
| 1030 | El campo Condición IVA receptor no es un valor permitido. Para mas información consular método SEGGetCondicionIvaReceptor | error | `SEGAuthorize`; `CondicionIVAReceptorId` | 14 |
| 1031 | El campo Condición IVA receptor no es valido para la clase de comprobante informado. Para mas información consular método SEGGetCondicionIvaReceptor | error | `SEGAuthorize`; `CondicionIVAReceptorId` | 14 |
| 1032 | El campo Condición Frente al IVA del receptor resultara obligatorio conforme lo reglamentado por la Resolución General N° 5616. Para mas información consular método SEGGetCondicionIvaReceptor | error. Según [M10], desde 1.0 es lo que sale si falta el campo; antes salía la obs. 23 [INFERIDO de la versión 0.8 + la 1.0] | `SEGAuthorize`; `CondicionIVAReceptorId` | 14 |
| 1033 | Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacío, | error | `SEGAuthorize`; `CanMisMonExt` | 14 |
| 1034 | Si informa el campo Imp_moneda_ctz, el mismo no podrá superar en 1 a la cotizacion oficial. Ver Método SEGGetPARAM_Ctz.. | error | `SEGAuthorize`; `Imp_moneda_ctz` (la tabla del manual lo rotula `<CanMisMonExt>`) | 14 (14-15) |
| 1035 | Si informa Imp_moneda_Id = PES, el campo CanMisMonExt no debe informarse o informarse con el valor N. | error | `SEGAuthorize`; `CanMisMonExt`/`Imp_moneda_Id` | 15 |
| 21 | [M10]: LA CUIT RECEPTORA SE ENCUENTRA INACTIVA POR HABER SIDO INCLUÍDA EN LA CONSULTA DE FACTURAS APÓCRIFAS. NO PODRÁ COMPUTARSE EL CRÉDITO FISCAL | observación ([M10]: "salimos con la sig. observación"). [M09] lo llama "codigo de rechazo" y no trae la segunda oración | `SEGAuthorize`; `SEGResultAuth/Obs`; receptor (`Nro_doc`) apócrifo | 12 |
| 22 | La CUIT RECEPTORA INGRESADA NO EXISTE. Para el caso de Facturas y Notas de Débito, emitir una Nota de Crédito o anular la operación, según corresponda. | observación ("Para todos los comprobantes"; versión 0.7: "a modo observación") | `SEGAuthorize`; `SEGResultAuth/Obs`; `Nro_doc` | 12-13 (13) |
| 23 | El campo Condición Frente al IVA del receptor resultará obligatorio conforme lo reglamentado por la Resolución General N° 5616. Para mas información consultar el método “SEGGetCondicionIvaReceptor” o la tabla “Condición frente al IVA del receptor”. Ver Anexo | observación. **En desuso** según [M10], historial 1.0 (pág. 3) | `SEGAuthorize`; `SEGResultAuth/Obs`; falta `CondicionIVAReceptorId` | 13 |
| 39 | IMPORTANTE: Por motivos de mantenimiento, el servicio estara fuera de linea el domingo 14 de junio desde las 21:00 hs durante aproximadamente 3 horas. Agradecemos tu comprension y pedimos disculpas por las molestias ocasionadas. | evento | Todas con `SEGEvents` (homologación) | — [VIVO] |
| 47 | El servicio WSSEG sera dado de baja. Cabe destacar que la Resolución General Nro 5866/2026 indica que este servicio sera reemplazado por WSFEV1 a partir del 01/01/2027. [...] (texto completo al principio del documento) | evento | Todas con `SEGEvents` (producción) | — [VIVO] |
| `soap:Client` | `System.Web.Services.Protocols.SoapException: Server did not recognize the value of HTTP Header SOAPAction: <valor>.` + stack trace | fault, HTTP 500, sin `FEHeaderInfo` | Cualquiera; `SOAPAction` inexistente | — [VIVO] |
| `soap:Client` | `System.Web.Services.Protocols.SoapException: Server was unable to read request. ---> System.InvalidOperationException: There is an error in XML document (<línea>, <columna>). ---> System.FormatException: Input string was not in a correct format.` + stack trace | fault, HTTP 500, con `FEHeaderInfo` | Cualquiera; valor no numérico en un campo numérico | — [VIVO] |
| HTTP 400 | (cuerpo vacío) | rechazo HTTP | Cualquiera; XML mal formado | — [VIVO] |

Notas:
- **1014 es un código paraguas.** El texto real que devuelve el servicio para cada caso, con el "nombre de campo y causa" que promete la pág. 15: **NO VERIFICADO**. Las filas sin mensaje en el manual (Id, punto de venta, número) no tienen texto conocido.
- **El historial de la versión 0.8 dice "códigos de rechazo 1030, 1031 y 1032, 1033, 1034, 1035"** (pág. 2). Se tratan como `SEGErr` [INFERIDO por pág. 11]. Si en cambio vienen como `SEGResultAuth` con `Resultado` de rechazo y el código en `Obs`: **NO VERIFICADO**.
- **Versiones 0.3, 0.4 y 0.6 del historial** (pág. 2): la ventana de ±5 días entró en la 0.3. La banda del tipo de cambio fue 20 %–100 % en la 0.4 y es 20 %–200 % desde la 0.6, del 01-01-2023.
- **Orden de evaluación** entre las validaciones de negocio: **NO VERIFICADO**. Solo se sabe que la autenticación va primero y que sale un solo error.

El mismo contenido, en formato máquina: `wsseg-codigos.json` (52 entradas). Ahí 1000 y 1001 del manual van con una entrada por operación, y las filas [VIVO] llevan `page` null.

## Tablas y datos

### Condición frente al IVA del receptor (Anexo 3.1, [M10] pág. 28; idéntico en [M09])

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

Es la misma tabla de wsfev1 (wsfev1.md §7.8), restringida a las clases A y B. La usan 1030 (código inexistente), 1031 (código no válido para la clase) y `SEGGetCondicionIvaReceptor`.

### Tipos de comprobante

El manual solo da la lista de valores válidos: "01,02,03 06,07,08" (pág. 13). Descripciones y vigencias de `SEGGetPARAM_Tipo_Cbte`: **NO VERIFICADAS**. Por wsfev1.md §7.1, los mismos códigos son [INFERIDO]:

| Código | Descripción en wsfev1 | Clase |
|---:|---|:---:|
| 1 | Factura A | A |
| 2 | Nota de Débito A | A |
| 3 | Nota de Crédito A | A |
| 6 | Factura B | B |
| 7 | Nota de Débito B | B |
| 8 | Nota de Crédito B | B |

La clase sale del código: 1-3 = A, 6-8 = B [INFERIDO]. Hace falta para 1014 (`Tipo_doc` 80 en clase A) y para 1031.

### Otras tablas

| Tabla | Qué dice el manual | Estado |
|---|---|---|
| Tipos de documento (`SEGGetPARAM_Tipo_doc`) | Nada. Solo que 80 = CUIT (pág. 13) | Valores **NO VERIFICADOS**. Como referencia, wsfev1.md §7.2 |
| Alícuotas de IVA (`SEGGetPARAM_Tipo_IVA`) | Nada. Los ejemplos usan `Iva_id` 5 con IVA al 21 % | Valores **NO VERIFICADOS**. Como referencia, wsfev1.md §7.4 (5 = 21 %). Cuál `Iva_id` cuenta como "exento" para la regla de `Imp_op_ex`: **NO VERIFICADO** (wsfev1 no tiene alícuota "exento") |
| Monedas (`SEGGetPARAM_MON`) | Nada. Los ejemplos usan `PES` con cotización 1 | Valores **NO VERIFICADOS**. Como referencia, wsfev1.md §7.5 |
| Cotizaciones | Las de "las bases de ARCA" (págs. 14 y 27) | Datos de ARCA; ArcaSim necesita una tabla configurable |

### Datos de prueba

El manual no trae CUITs ni puntos de venta de homologación. Los ejemplos usan:
- `Nro_doc` 33693450239 como receptor. Es la CUIT de ARCA (ex AFIP) [INFERIDO; es la misma que aparece como `entity` en los TA de WSAA].
- `Punto_vta` 1900, `Id` 45.
- Pólizas `SESC8301/2009` y `SESC8302/2009`, endosos `84300` y `843220/23`.
- Fechas de 2009.

No hay datos de prueba oficiales: **NO VERIFICADO** si homologación exige algo más que el certificado asociado al servicio.

## Comportamiento a simular

### Estado

1. **Comprobantes autorizados**, por clave (`Cuit`, `Tipo_cbte`, `Punto_vta`, `Cbte_nro`). Se guarda todo lo enviado:
   - `Cmp` completo con sus `Items`, incluidos `Imp_otrib_prov`, `Imp_valor_aseg` e `Imp_moneda_vaseg`.
   - `Id`.
   - `Cae`, `Fch_venc_Cae`.
   - La fecha original (`Fecha_cbte_orig`) y la asignada (`Fecha_cbte_cae`).
   - `Resultado` y `Obs`.

   Sirve para `SEGGetCMP`, `SEGGetLast_CMP` y la idempotencia.
2. **Índice por (`Cuit`, `Id`)** para el reproceso, y el máximo `Id` por `Cuit` para `SEGGetLast_ID`. Que el `Id` sea por CUIT y no global sale de [FEX pág. 35] [INFERIDO].
3. **Tablas de parámetros** configurables: documentos, IVA, comprobantes, monedas, condición IVA (Anexo) y cotizaciones por moneda y fecha.
4. **Padrón simulado del receptor**: CUIT apócrifa (obs. 21) e inexistente (obs. 22). Del emisor: representados de cada certificado (1001).
5. **Reloj** configurable, en la zona horaria de Argentina, para la ventana de ±5 días, el "mes de presentación" y el "día hábil anterior". Necesita un calendario de feriados.

### Idempotencia y reproceso por `Id`

El manual de wsseg no lo explica. Solo define `Reproceso` ("Indica si es un reproceso “S” o “N”", pág. 12) y el `Id` ("Identificador del requerimiento", mayor que 0). El mecanismo está escrito, palabra por palabra, en el manual de wsfexv1, que tiene los mismos campos y métodos [FEX pág. 8]:

> "b) Se corta la comunicación cuando WSFEXV1 envía la respuesta hacia el cliente (error de time-out). WSFEXV1 almacena en su base de datos todas las solicitudes de CAE que fueron aprobadas. Solución: En este caso el usuario simplemente debe volver a enviar la misma solicitud de CAE (igual Cmp.Id) a WSFEXV1. El sistema busca la solicitud recibida en su base de datos y, si la encuentra, retorna la respuesta con el campo <Reproceso> = "S". Si no la encuentra, la procesa normalmente, generando una respuesta con el campo <Reproceso>="N"."

Para ArcaSim [INFERIDO por analogía con [FEX]]:
- Llega `SEGAuthorize` con (`Cuit`, `Id`) ya **aprobado**: devolver el `SEGResultAuth` guardado (mismo `Cae`, `Fch_venc_Cae`, `Fch_cbte`, `Resultado`, `Obs`) con `Reproceso` = `S`. No se valida de nuevo ni se consume otro número.
- Llega un `Id` nuevo: procesar normalmente y responder `Reproceso` = `N`.
- Un pedido que terminó en `SEGErr` no se guarda, porque "almacena [...] todas las solicitudes de CAE que fueron **aprobadas**". Reenviarlo se procesa de cero.

Sin respuesta en ninguna fuente (**NO VERIFICADO**):
- Mismo `Id` con **contenido distinto** (otro número u otros importes): ¿devuelve lo guardado, da error o procesa? Los dos ejemplos del manual reusan `Id` 45 con `Cbte_nro` 6 y 7, pero son ejemplos sueltos, no una secuencia probada. Recomendación: modo configurable, por defecto "devolver lo guardado" (literal de [FEX]: "busca la solicitud recibida").
- Si el `Id` tiene que ser mayor que el último (monotonía). El manual solo exige "> 0".
- Si `SEGGetLast_ID` cuenta los pedidos rechazados.

[FEX pág. 8-9] además recomienda "no repetir accidentalmente el <Id>" (usar una secuencia o la fecha y hora) y archivarlo, "puesto que va a ser el único modo de recuperar en caso de error en la comunicación de retorno".

### Numeración

- **Clave**: (`Cuit`, `Punto_vta`, `Tipo_cbte`). `SEGGetLast_CMP` devuelve el último `Cbte_nro` y su `Cbte_fecha` [INFERIDO por [FEX págs. 36-37]].
- **Rango**: `Cbte_nro` entre 1 y 99999999; `Punto_vta` entre 1 y 99998 (1014).
- **Correlatividad** (que `Cbte_nro` sea el último + 1): **el manual de wsseg no la menciona** y no hay código para ella. **NO VERIFICADO** si el servicio la exige. Recomendación: flag configurable. Activarla por defecto si se quiere el comportamiento de wsfev1 (10016), avisando que no es dato de wsseg.
- Si se exige un punto de venta dado de alta y de algún tipo particular: **NO VERIFICADO** (el manual no lo valida).
- Sin comprobantes previos, `SEGGetLast_CMP`: probablemente `Cbte_nro` 0 [INFERIDO]. Lo que viene en `Cbte_fecha` en ese caso: **NO VERIFICADO**.

### Fecha del comprobante

- `Fecha_cbte` es `yyyymmdd`. Si no se envía, se asigna la fecha de proceso (pág. 13).
- Validaciones (1014, pág. 13), en tres pasos:
  1. Formato numérico de 8.
  2. No exceder el mes de la fecha de envío.
  3. ±5 días respecto de la fecha de generación.
- La fecha que quedó vuelve en `SEGResultAuth/Fch_cbte`. En `SEGGetCMP` aparecen las dos: `Fecha_cbte_orig` (la enviada) y `Fecha_cbte_cae` (la asignada si se omitió).
- Qué trae `Fecha_cbte_cae` cuando el cliente sí mandó la fecha (¿la misma o vacía?): **NO VERIFICADO**.

### CAE y vencimiento

- **Formato del `Cae`**: **NO VERIFICADO** en wsseg. En wsfexv1 es `String(C14)` [FEX pág. 17] y en wsfev1, 14 dígitos (wsfev1.md §6.1). Recomendación: 14 dígitos únicos [INFERIDO].
- **`Fch_venc_Cae`**: ninguna fuente da la regla. En wsfev1 se infirió fecha del comprobante + 10 días corridos (wsfev1.md §6.2). Para wsseg: **NO VERIFICADO**. Dejarlo configurable.

### Resultado y observaciones

- **Valores de `Resultado`**: el manual no los enumera. Lo razonable es `A` (aprobado) cuando hay CAE, también con observaciones 21 o 22 [INFERIDO por analogía con wsfev1/wsfexv1]. Si existe un `R` con `SEGResultAuth`, o si todo rechazo va por `SEGErr`: **NO VERIFICADO**. La pág. 11 apoya lo segundo: "Ante cualquier anomalía se retorna un código de error cancelando la ejecución del WS".
- **`Obs`**: un solo string. Cómo se concatenan varias observaciones y si lleva el prefijo "21 - ": **NO VERIFICADO**.
- **Obs. 21** (receptor apócrifo):
  - [M10]: observación, el comprobante se autoriza.
  - [M09]: "código de rechazo".
  - Flag `wsseg.manual` = `1.0` (por defecto) o `0.9`.
- **Obs. 22** (CUIT receptora inexistente): observación en las dos versiones.
- **Obs. 23 y error 1032** (falta `CondicionIVAReceptorId`):
  - Con `1.0`: falta el campo → error 1032. La obs. 23 no se emite.
  - Con `0.9`: falta el campo → se autoriza con obs. 23.
  - En las dos: valor fuera de tabla → 1030; valor que no corresponde a la clase → 1031.
- Para referencia: el servicio hermano wsbfe ya anuncia en homologación que el campo es obligatorio desde el 9-jun-2025 [VIVO wsbfe]. Para wsfev1, el rechazo en producción está anunciado para el 01/12/2026 (normativa.md). Qué hace hoy wsseg en producción: **NO VERIFICADO**.

### Moneda y cotización

- `Imp_moneda_Id` tiene que estar en la tabla de monedas [INFERIDO; el manual remite a `SEGGetPARAM_MON`, sin código propio].
- Con `PES`, `CanMisMonExt` ausente o `N` (1035).
- `Imp_moneda_ctz`:
  - Obligatorio salvo `CanMisMonExt` = S; si viene, mayor que 0 (1014).
  - Con `CanMisMonExt` = S: igual a la cotización de ARCA del día hábil anterior a `Fecha_cbte` si esa fecha es pasada, o del día hábil anterior a hoy si es futura (1014).
  - Dentro de la banda de 20 % a 200 % de la cotización orientativa (1014).
  - No más de 1 por encima de la oficial (1034).

  La lectura exacta de "inferior al 20 % ni superior en un 200 %" (¿80 %–300 %? ¿20 %–200 %?) y de "superar en 1" (¿1 unidad? ¿1 %?): **NO VERIFICADO**. Dejar los límites configurables.
- `SEGGetPARAM_Ctz` lee de la misma tabla de cotizaciones:
  - Moneda inválida → 1003.
  - Moneda vacía → 1004.
  - Fecha que no es `YYYYMMDD` válida → 1005.
  - Sin cotización para esa fecha → 1006.

### Relaciones entre operaciones

- `SEGGetLast_ID` y `SEGGetLast_CMP` sirven para recuperarse de un corte, junto con el reenvío del mismo `Id`. `SEGGetCMP` devuelve lo autorizado.
- `SEGGetCondicionIvaReceptor`, `SEGGetPARAM_*` y `SEGGetPARAM_Ctz` leen las mismas tablas que valida `SEGAuthorize`.
- **Con wsfev1**: el evento 47 de producción dice que `FECompConsultar` de wsfev1 "se adecuó [...] para obtener ademas la póliza y el endoso" de comprobantes emitidos por WSSEG. Si ArcaSim simula los dos servicios, conviene que compartan el almacén de comprobantes [INFERIDO]. Cómo aparecen póliza y endoso en esa respuesta, y si wsseg y wsfev1 comparten la numeración por punto de venta: **NO VERIFICADO**.

### Qué devuelve cuando no hay datos

| Caso | Respuesta | Fuente |
|---|---|---|
| `SEGGetCMP` de un comprobante que no existe | `SEGErr` 1020 "Comprobante inexistente", sin `SEGResultGet` [INFERIDO lo de "sin `SEGResultGet`", por lo observado con errores de token] | pág. 19 |
| `SEGGetPARAM_Ctz` sin cotización | `SEGErr` 1006 "Sin Resultados: - Método SEGGetPARAM_Ctz" | pág. 28 |
| `SEGGetLast_CMP` sin comprobantes | **NO VERIFICADO** (`Cbte_nro` 0 [INFERIDO]) | — |
| `SEGGetLast_ID` sin pedidos | **NO VERIFICADO** (`Id` 0 [INFERIDO]) | — |
| `SEGGetCondicionIvaReceptor` sin `ClaseCmp` | Todas las combinaciones (11 filas por el Anexo [INFERIDO]) | pág. 25 |
| Cualquier error | Solo `SEGErr` + `SEGEvents`, sin el elemento de resultado | [VIVO] |

### Protocolo (resumen para la implementación)

1. Ruta `/wsseg/service.asmx`.
2. `?WSDL` con las 11 operaciones y los cuatro bindings.
3. SOAP 1.1 y 1.2. Enrutar por `SOAPAction`/`action` y, si falta, por el elemento del Body.
4. `GET` y `POST` a `/SEGDummy`.
5. Faults y 400 como en "Contrato".
6. Tolerancias del deserializador .NET:
   - Distingue mayúsculas.
   - Ignora elementos desconocidos o en otro namespace, que quedan nulos.
   - Los numéricos ausentes valen 0.
7. Serialización:
   - Una línea, con `<?xml version="1.0" encoding="utf-8"?>`.
   - `xmlns="http://ar.gov.afip.dif.seg/"` en el elemento de respuesta.
   - `FEHeaderInfo` en toda respuesta no-fault y en los faults de deserialización. Para no confundir ambientes, conviene un `ambiente` propio, por ejemplo `ArcaSim - local`.
8. `SEGEvents`, configurable:
   - Modo "homologación": evento 39.
   - Modo "producción": evento 47.
   - Modo limpio.
9. **Baja del servicio**: qué responderá ARCA desde el 01/01/2027 (fault, error, 404) es **NO VERIFICADO**. Si se quiere simular, hace falta un flag `wsseg.dadoDeBaja` con una respuesta configurable.
10. Fallas inducidas para que el cliente pruebe su manejo:
    - Timeout después de autorizar: es el caso de uso del reproceso.
    - Errores 500, 501 y 502.
    - `SEGDummy` con valores distintos de `OK`.

## No verificado

1. Si la versión de producción ya aplica [M10]: 1032 en vez de la obs. 23, y la obs. 21 como observación. Solo se sabe que los dos ambientes informan `id` `3.0.4.0`.
2. Semántica completa del reproceso por `Id`: contenido distinto con el mismo `Id`, monotonía, alcance por CUIT, si los rechazos cuentan. Hoy todo sale por analogía con [FEX pág. 8].
3. Valores de `Resultado`. Si un rechazo de negocio puede venir como `SEGResultAuth` con `Resultado` `R` además de, o en lugar de, `SEGErr`.
4. Formato del `Cae` (¿14 dígitos?) y regla de `Fch_venc_Cae`.
5. Formato de `Obs`: separador entre observaciones, prefijo de código.
6. Textos reales de 1014 para cada validación ("nombre de campo y causa") y de 1001.
7. Significado y validaciones de `Imp_otrib_prov`, `Item/Imp_valor_aseg` e `Item/Imp_moneda_vaseg` (están en el WSDL, no en los manuales).
8. Las cuatro operaciones sin manual: respuestas sin datos de `SEGGetLast_CMP` y `SEGGetLast_ID`, contenido de `SEGGetPARAM_Tipo_doc`, validaciones propias.
9. Contenido real de las tablas `SEGGetPARAM_Tipo_doc`, `_Tipo_IVA`, `_Tipo_Cbte` y `_MON`, y formato de las vigencias vacías.
10. Forma de la respuesta de `SEGGetCondicionIvaReceptor` sin clase (filas, orden, `Desc`) y con `ClaseCmp` vacío.
11. Si se exige correlatividad de `Cbte_nro` y punto de venta habilitado para el servicio.
12. Qué `Iva_id` cuenta como "exento" para la regla de `Imp_op_ex`.
13. Interpretación numérica de la banda de tipo de cambio ("20 %" / "200 %") y de "no podrá superar en 1" (1034).
14. Si `SEGErr` y `SEGEvents` vienen con código 0 en una respuesta exitosa ("0 – OK", pág. 12) o se omiten.
15. Valores de `SEGDummy` cuando un componente no está `OK`.
16. Comportamiento del servicio después de la baja anunciada para el 01/01/2027. Si wsseg y wsfev1 comparten la numeración y cómo `FECompConsultar` devuelve póliza y endoso.
17. Límites de tasa, timeouts y tamaño máximo de request (cantidad de ítems): no hay fuente.
18. Requisitos de habilitación además del certificado asociado a "Operacion de Seguros de Caucion - SEG" (por ejemplo, estar en una nómina de aseguradoras).
