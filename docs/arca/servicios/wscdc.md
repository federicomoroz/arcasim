# wscdc

**Constatación de Comprobantes.** Le permite a cualquiera que tenga un ticket de WSAA para este servicio (el emisor, el receptor o un tercero) preguntarle a ARCA si un comprobante con tal CUIT emisora, tipo, punto de venta, número, fecha, importe y código de autorización (CAE, CAEA o CAI) está registrado y autorizado. No emite nada: compara lo que le mandan contra lo que ARCA tiene guardado y contesta `Resultado` A (constatado) o R (rechazado), con los motivos.

Relevamiento hecho el 2026-10-02. Fuentes y marcas:

| Marca | Fuente |
|---|---|
| **[MAN]** | "Especificaciones técnicas de Servicios Web – WSCDC – Constatación de Comprobantes", versión 0.4, revisión del 1 de diciembre de 2025, ARCA-SDG SIT, 27 páginas con contenido. URL: `https://www.afip.gob.ar/ws/WSCDCV1/WSCDC-manual-desarrollador-v4.pdf` (bajado el 2026-10-02, HTTP 200, 934 294 bytes). "pág. N" = página física del PDF, que coincide con la numeración del manual. |
| **[WSDL]** | `docs/arca/wsdl/wscdc-homologacion.wsdl` (bajado el 2026-10-01). El WSDL de producción (`https://servicios1.afip.gov.ar/WSCDC/service.asmx?WSDL`, bajado el 2026-10-02) es idéntico salvo `soap:address` y `soap12:address`. No importa XSD externos: el esquema está embebido en `wsdl:types`, así que no hay carpeta `wsdl/wscdc/`. |
| **[VIVO]** | Llamadas propias sin credenciales a homologación y producción el 2026-10-02 (dummy, token falso, sin `Auth`). |
| **[SEC]** | Fuente secundaria: el cliente open source PyAfipWs (`https://github.com/PyAr/pyafipws/blob/main/wscdc.py`). Sólo para huecos del manual. |

Cuando el WSDL y el manual no coinciden, **manda el WSDL** (es lo que serializa el servicio).

## Contrato

| Aspecto | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX (el mismo de wsfev1 y wsfexv1). Errores de negocio y de autenticación dentro del body con HTTP 200 | [WSDL] [VIVO] |
| Endpoint homologación | `https://wswhomo.afip.gov.ar/WSCDC/service.asmx` (es el `soap:address` del WSDL). El manual (pág. 5) escribe `https://wswhomo.afip.gob.ar/WSCDC/service.asmx`; ese host también responde 200 | [WSDL] [MAN] [VIVO] |
| Endpoint producción | `https://servicios1.afip.gov.ar/WSCDC/service.asmx` (`soap:address` del WSDL de producción). El manual (pág. 5) escribe `https://servicios1.arca.gob.ar/WSCDC/service.asmx`, que **falla por TLS** (el certificado es de `servicios1.afip.gov.ar`, ver `catalogo.md` 4.2) | [WSDL] [MAN] |
| Path | `/WSCDC/service.asmx`. En vivo `/wscdc/service.asmx` (minúsculas) también responde 200: IIS no distingue mayúsculas | [VIVO] |
| WSDL | `GET <endpoint>?WSDL` → 200 `text/xml`, 21 454 bytes (homologación) y 21 460 (producción) | [VIVO] |
| Namespace | `http://servicios1.afip.gob.ar/wscdc/` (**`.gob.ar`**, a diferencia de wsfev1 y wsfexv1 que usan `ar.gov.afip.dif...`). `elementFormDefault="qualified"`: todos los hijos van en ese namespace. El header `FEHeaderInfo` usa el mismo namespace | [WSDL] [VIVO] |
| Archivo WSDL | `docs/arca/wsdl/wscdc-homologacion.wsdl` | — |
| WSAA service id | `wscdc`. **El manual no lo dice**; sale de [SEC] (`wsaa.Autenticar("wscdc", ...)`). NO VERIFICADO en fuente oficial | [SEC] |
| SOAPAction | `http://servicios1.afip.gob.ar/wscdc/<Operación>` para las 6 operaciones | [WSDL] |
| SOAP 1.1 / 1.2 | Ambos. Bindings `ServiceSoap` y `ServiceSoap12` sobre el mismo `portType` y la misma URL. En vivo, SOAP 1.2 (`application/soap+xml; action="..."`) devuelve 200 `application/soap+xml` con envelope 1.2 | [WSDL] [VIVO] |
| Header de respuesta | `soap:Header/FEHeaderInfo{ambiente, fecha, id}`. Valores vistos: homologación `Homologacion-Ext - srt`, producción `Produccion - sr5`, `id` = `3.1.1.0` en los dos, `fecha` con 7 decimales y huso `-03:00` (ej. `2026-10-02T15:11:47.6553224-03:00`). El manual no documenta este header | [VIVO] |
| Operaciones | 6: `ComprobanteConstatar`, `ComprobantesModalidadConsultar`, `ComprobantesTipoConsultar`, `DocumentosTipoConsultar`, `OpcionalesTipoConsultar`, `ComprobanteDummy` | [WSDL] [MAN pág. 6] |

Envelope de request, tal como lo arman los ejemplos del manual (pág. 15): prefijo libre (`wsf:`/`wsc:`) para el namespace del servicio. Ojo, **los ejemplos del manual omiten el prefijo** en `<ComprobanteConstatar>` y en sus hijos aunque declaran `xmlns:wsf`: así escrito, en .NET esos elementos quedan sin namespace y el servicio no los ve (en wsfev1 eso da `Campo Auth no fue ingresado o esta mal formado`). Un cliente que copie el ejemplo literal recibe el error 500 de abajo.

## Autenticación

`Auth` (`CmpAuthRequest`) va como primer hijo del elemento de la operación, en todas menos `ComprobanteDummy`:

| Campo | Tipo XSD | Ocurr. | Significado |
|---|---|---|---|
| `Token` | `string` | 0..1 | Token del TA de WSAA |
| `Sign` | `string` | 0..1 | Firma del TA |
| `Cuit` | `long` | 1..1 | CUIT del contribuyente que consulta (representado). Si falta, .NET lo deja en 0 |

Respuestas reales de error de autenticación, todas **HTTP 200**, `Content-Type: text/xml; charset=utf-8`, con `FEHeaderInfo` en el header ([VIVO] 2026-10-02, homologación):

| Caso | Contenido de `<...Result>` |
|---|---|
| Sin `Auth` (`<wsc:ComprobantesModalidadConsultar/>`) | `<Errors><Err><Code>500</Code><Msg>Campo Auth no fue ingresado o esta mal formado.</Msg></Err></Errors><Events><Evt><Code>0</Code></Evt></Events>` |
| `Token`=`abc`, `Sign`=`abc` | `<Errors><Err><Code>600</Code><Msg>ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</Msg></Err></Errors><Events><Evt><Code>0</Code></Evt></Events>` |
| `Token` y `Sign` vacíos | `Code` 600, `Msg` = `ValidacionDeToken: Error al verificar hash: VerificacionDeHash: No validó la firma digital. ` (con espacio final) |
| Token base64 de un `<sso>` bien formado (vencido) con `Sign`=`AAAA` | `Code` 600, mismo `Msg` "No validó la firma digital. ". O sea: **la firma se verifica antes que las fechas** |

Detalles que el simulador tiene que copiar:

- `Events/Evt` trae sólo `<Code>0</Code>`, **sin `<Msg>`**.
- En `ComprobanteConstatar`, aunque falle el token, la respuesta **devuelve igual el eco `CmpResp`** con los datos enviados (sólo los que vinieron), `Resultado`=`R`, después `Events` y por último `Errors`. **No trae `FchProceso`**. Ejemplo literal:

```xml
<ComprobanteConstatarResponse xmlns="http://servicios1.afip.gob.ar/wscdc/"><ComprobanteConstatarResult><CmpResp><CbteModo>CAE</CbteModo><CuitEmisor>20111111112</CuitEmisor><PtoVta>1</PtoVta><CbteTipo>1</CbteTipo><CbteNro>1</CbteNro><CbteFch>20261001</CbteFch><ImpTotal>121</ImpTotal><CodAutorizacion>76123456789012</CodAutorizacion></CmpResp><Resultado>R</Resultado><Events><Evt><Code>0</Code></Evt></Events><Errors><Err><Code>600</Code><Msg>ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</Msg></Err></Errors></ComprobanteConstatarResult></ComprobanteConstatarResponse>
```

- El token se valida **antes** que el formato: con token falso y todos los campos inválidos (`CbteModo`=`XXXX`, `PtoVta`=0, `CbteFch`=`2026`, `ImpTotal`=-1) sólo vuelve el 600 ([VIVO]).
- Códigos del manual para `Auth` (pág. 11): 600 "No se corresponden token y firma. Usuario no autorizado a realizar esta operación", 601 "CUIT representada no incluida en token.", 602 "CUIT representada no se encuentre activa y vigente." Los textos reales de 601/602 no se observaron (requieren un token válido): NO VERIFICADO.
- Un `SOAPAction` inexistente da HTTP 500 con `soap:Fault` `soap:Client` "Server did not recognize the value of HTTP Header SOAPAction" (igual que wsfexv1; ver allí el literal). Un valor no numérico en un campo `long`/`int` da HTTP 500 `soap:Fault` "Server was unable to read request" (observado en wsfexv1 con el mismo stack .NET; en wscdc NO VERIFICADO).

## Operaciones

Orden de los elementos = orden del esquema ([WSDL]). Respuesta: `<{Op}Response xmlns="http://servicios1.afip.gob.ar/wscdc/"><{Op}Result>...`.

### ComprobanteConstatar

Recibe los datos de un comprobante y los compara contra lo registrado bajo `CodAutorizacion` ([MAN pág. 6-18]). "Los comprobantes pueden ser constatados por el emisor, por el receptor, o por cualquiera que tenga acceso al ws".

Request:

Elemento raíz: `ComprobanteConstatar`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `CmpAuthRequest` | 0..1 | Credenciales (ver Autenticación) |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT que consulta (representada). Manual: S |
| `CmpReq` | `CmpDatos` | 0..1 | Comprobante a constatar. Manual: S |
| `CmpReq/CbteModo` | `string` | 0..1 | Modalidad de autorización: `CAE`, `CAEA` o `CAI`. String(4). Valores de `ComprobantesModalidadConsultar` (validación 1) |
| `CmpReq/CuitEmisor` | `long` | 1..1 | CUIT del emisor. Long(11), válida (validación 2) |
| `CmpReq/PtoVta` | `int` | 1..1 | Punto de venta, 1 a 99998 (validación 3). Manual: Int(5) en request, Int(4) en respuesta |
| `CmpReq/CbteTipo` | `int` | 1..1 | Tipo de comprobante, hasta 3 dígitos, de `ComprobantesTipoConsultar` (validación 4) |
| `CmpReq/CbteNro` | `long` | 1..1 | Número, 1 a 99999999 (validación 5) |
| `CmpReq/CbteFch` | `string` | 0..1 | Fecha del comprobante `yyyymmdd` (validación 6). No anterior a 20130101 (101) |
| `CmpReq/ImpTotal` | `double` | 1..1 | Importe total, `double`, ≥ 0, 13 enteros y 2 decimales (validación 7). 0 para comprobantes sin importe |
| `CmpReq/CodAutorizacion` | `string` | 0..1 | CAE, CAEA o CAI: 14 caracteres numéricos (validación 10) |
| `CmpReq/DocTipoReceptor` | `string` | 0..1 | Tipo de documento del receptor, String(2), de `DocumentosTipoConsultar` (validación 8). Manual: N |
| `CmpReq/DocNroReceptor` | `string` | 0..1 | Número de documento del receptor, String(20); el manual pide 11 numéricos sin letras (validación 9). Manual: N |
| `CmpReq/Opcionales` | `ArrayOfOpcional` | 0..1 | Array de opcionales, "reservado usos futuros". Manual: N |
| `CmpReq/Opcionales/Opcional` | `Opcional` | 0..n nil | Un opcional |
| `CmpReq/Opcionales/Opcional/Id` | `string` | 0..1 | Código de opcional, String(4), de `OpcionalesTipoConsultar` (151, 152) |
| `CmpReq/Opcionales/Opcional/Valor` | `string` | 0..1 | Valor, String(250), obligatorio (153) |


Response:

Elemento raíz: `ComprobanteConstatarResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `ComprobanteConstatarResult` | `CmpResponse` | 0..1 | Resultado |
| `ComprobanteConstatarResult/CmpResp` | `CmpDatos` | 0..1 | Eco de los datos enviados (mismo tipo `CmpDatos`) |
| `ComprobanteConstatarResult/CmpResp/CbteModo` | `string` | 0..1 | Modalidad de autorización: `CAE`, `CAEA` o `CAI`. String(4). Valores de `ComprobantesModalidadConsultar` (validación 1) |
| `ComprobanteConstatarResult/CmpResp/CuitEmisor` | `long` | 1..1 | CUIT del emisor. Long(11), válida (validación 2) |
| `ComprobanteConstatarResult/CmpResp/PtoVta` | `int` | 1..1 | Punto de venta, 1 a 99998 (validación 3). Manual: Int(5) en request, Int(4) en respuesta |
| `ComprobanteConstatarResult/CmpResp/CbteTipo` | `int` | 1..1 | Tipo de comprobante, hasta 3 dígitos, de `ComprobantesTipoConsultar` (validación 4) |
| `ComprobanteConstatarResult/CmpResp/CbteNro` | `long` | 1..1 | Número, 1 a 99999999 (validación 5) |
| `ComprobanteConstatarResult/CmpResp/CbteFch` | `string` | 0..1 | Fecha del comprobante `yyyymmdd` (validación 6). No anterior a 20130101 (101) |
| `ComprobanteConstatarResult/CmpResp/ImpTotal` | `double` | 1..1 | Importe total, `double`, ≥ 0, 13 enteros y 2 decimales (validación 7). 0 para comprobantes sin importe |
| `ComprobanteConstatarResult/CmpResp/CodAutorizacion` | `string` | 0..1 | CAE, CAEA o CAI: 14 caracteres numéricos (validación 10) |
| `ComprobanteConstatarResult/CmpResp/DocTipoReceptor` | `string` | 0..1 | Tipo de documento del receptor, String(2), de `DocumentosTipoConsultar` (validación 8). Manual: N |
| `ComprobanteConstatarResult/CmpResp/DocNroReceptor` | `string` | 0..1 | Número de documento del receptor, String(20); el manual pide 11 numéricos sin letras (validación 9). Manual: N |
| `ComprobanteConstatarResult/CmpResp/Opcionales` | `ArrayOfOpcional` | 0..1 | Array de opcionales, "reservado usos futuros". Manual: N |
| `ComprobanteConstatarResult/CmpResp/Opcionales/Opcional` | `Opcional` | 0..n nil | Un opcional |
| `ComprobanteConstatarResult/CmpResp/Opcionales/Opcional/Id` | `string` | 0..1 | Código de opcional, String(4), de `OpcionalesTipoConsultar` (151, 152) |
| `ComprobanteConstatarResult/CmpResp/Opcionales/Opcional/Valor` | `string` | 0..1 | Valor, String(250), obligatorio (153) |
| `ComprobanteConstatarResult/Resultado` | `string` | 0..1 | `A` constatado, `R` rechazado |
| `ComprobanteConstatarResult/Observaciones` | `ArrayOfObs` | 0..1 | Validaciones funcionales no superadas (y la 200) |
| `ComprobanteConstatarResult/Observaciones/Obs` | `Obs` | 0..n nil | Una observación |
| `ComprobanteConstatarResult/Observaciones/Obs/Code` | `int` | 1..1 | Código |
| `ComprobanteConstatarResult/Observaciones/Obs/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |
| `ComprobanteConstatarResult/FchProceso` | `string` | 0..1 | Fecha y hora de la constatación, `yyyyMMddHHmmss` (ejemplo del manual) |
| `ComprobanteConstatarResult/Events` | `ArrayOfEvt` | 0..1 | Eventos. En vivo: `<Evt><Code>0</Code></Evt>` sin `Msg` |
| `ComprobanteConstatarResult/Events/Evt` | `Evt` | 0..n nil | Un evento |
| `ComprobanteConstatarResult/Events/Evt/Code` | `int` | 1..1 | Código |
| `ComprobanteConstatarResult/Events/Evt/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |
| `ComprobanteConstatarResult/Errors` | `ArrayOfErr` | 0..1 | Errores de infraestructura, `Auth` y formato |
| `ComprobanteConstatarResult/Errors/Err` | `Err` | 0..n nil | Un error |
| `ComprobanteConstatarResult/Errors/Err/Code` | `int` | 1..1 | Código |
| `ComprobanteConstatarResult/Errors/Err/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |


Diferencias entre manual y WSDL en esta operación:

- El manual dice `PtoVta` Int(5) en el request e Int(4) en la respuesta; el WSDL usa `int` en los dos. Se toma 5 (validación 3: 1 a 99998).
- El manual presenta `CbteModo`, `CbteFch`, `CodAutorizacion` como obligatorios; en el WSDL son `0..1` (la obligatoriedad la imponen las validaciones de formato).
- El manual ordena la respuesta `CmpResp, Resultado, Observaciones, FchProceso, Events, Errors`; el WSDL también. En vivo, con error de token, no vino `FchProceso`.

Ejemplo del manual (pág. 15), CAE constatado:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:wsf="http://servicios1.afip.gob.ar/wscdc/">
  <soapenv:Header/>
  <soapenv:Body>
    <ComprobanteConstatar>
      <Auth><Token>111</Token><Sign>11111111</Sign><Cuit>300000000007</Cuit></Auth>
      <CmpReq>
        <CbteModo>CAE</CbteModo><CuitEmisor>20000000001</CuitEmisor><PtoVta>1</PtoVta>
        <CbteTipo>1</CbteTipo><CbteNro>2</CbteNro><CbteFch>20101014</CbteFch>
        <ImpTotal>300.8</ImpTotal><CodAutorizacion>60428000005029</CodAutorizacion>
        <DocTipoReceptor>80</DocTipoReceptor><DocNroReceptor>300000000007</DocNroReceptor>
      </CmpReq>
    </ComprobanteConstatar>
  </soapenv:Body>
</soapenv:Envelope>
```

```xml
<ComprobanteConstatarResponse>
  <ComprobanteConstatarResult>
    <CmpResp>
      <CbteModo>CAE</CbteModo><CuitEmisor>20000000001</CuitEmisor><PtoVta>1</PtoVta>
      <CbteTipo>1</CbteTipo><CbteNro>2</CbteNro><CbteFch>20101014</CbteFch>
      <ImpTotal>300.8</ImpTotal><CodAutorizacion>60428000005029</CodAutorizacion>
      <DocTipoReceptor>80</DocTipoReceptor><DocNroReceptor>30000000007</DocNroReceptor>
    </CmpResp>
    <Resultado>A</Resultado>
    <FchProceso>20130729204436</FchProceso>
  </ComprobanteConstatarResult>
</ComprobanteConstatarResponse>
```

Erratas del ejemplo: el `Cuit` y el `DocNroReceptor` del request tienen 12 dígitos (`300000000007`) y el eco devuelve 11 (`30000000007`); los elementos no llevan el prefijo `wsf:`. `FchProceso` sale como `yyyyMMddHHmmss`.

Los otros tres ejemplos del manual (pág. 16-19), resumidos con sus mensajes literales:

| Caso | `Resultado` | Dónde | Código y `Msg` |
|---|---|---|---|
| CAEA todavía no informado, dentro del plazo para informarlo | A | `Observaciones/Obs` | 200 `Existe CAEA, no fue rendido o no coincide con los datos registrados.` |
| `CuitEmisor`=`222222222222` (12 dígitos) | R | `Errors/Err` (y `FchProceso` presente antes de `Errors`) | 2 `El campo CuitEmisor es invalido.` |
| CAI con fecha fuera de rango | R | `Observaciones/Obs` | 108 `La fecha consignada no se encuentra dentro del rango de fechas habilitadas para el CAI ingresado` |

### ComprobantesModalidadConsultar

Devuelve las modalidades de autorización válidas para `CbteModo` ([MAN pág. 19-20]).

Request:

Elemento raíz: `ComprobantesModalidadConsultar`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `CmpAuthRequest` | 0..1 | Credenciales (ver Autenticación) |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT que consulta (representada). Manual: S |


Response:

Elemento raíz: `ComprobantesModalidadConsultarResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `ComprobantesModalidadConsultarResult` | `FacModTipoResponse` | 0..1 | Resultado |
| `ComprobantesModalidadConsultarResult/ResultGet` | `ArrayOfFacModTipo` | 0..1 | Lista de valores |
| `ComprobantesModalidadConsultarResult/ResultGet/FacModTipo` | `FacModTipo` | 0..n nil | Una modalidad |
| `ComprobantesModalidadConsultarResult/ResultGet/FacModTipo/Cod` | `string` | 0..1 | Código de modalidad, String(4) |
| `ComprobantesModalidadConsultarResult/ResultGet/FacModTipo/Desc` | `string` | 0..1 | Descripción, String(250) |
| `ComprobantesModalidadConsultarResult/ResultGet/FacModTipo/FchDesde` | `string` | 0..1 | Vigencia desde, `yyyymmdd` |
| `ComprobantesModalidadConsultarResult/ResultGet/FacModTipo/FchHasta` | `string` | 0..1 | Vigencia hasta, `yyyymmdd` |
| `ComprobantesModalidadConsultarResult/Errors` | `ArrayOfErr` | 0..1 | Errores de infraestructura, `Auth` y formato |
| `ComprobantesModalidadConsultarResult/Errors/Err` | `Err` | 0..n nil | Un error |
| `ComprobantesModalidadConsultarResult/Errors/Err/Code` | `int` | 1..1 | Código |
| `ComprobantesModalidadConsultarResult/Errors/Err/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |
| `ComprobantesModalidadConsultarResult/Events` | `ArrayOfEvt` | 0..1 | Eventos. En vivo: `<Evt><Code>0</Code></Evt>` sin `Msg` |
| `ComprobantesModalidadConsultarResult/Events/Evt` | `Evt` | 0..n nil | Un evento |
| `ComprobantesModalidadConsultarResult/Events/Evt/Code` | `int` | 1..1 | Código |
| `ComprobantesModalidadConsultarResult/Events/Evt/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |


El manual no trae ejemplo con valores. Las validaciones nombran `CAE`, `CAEA` y `CAI` como modos (códigos 106, 107, 108); la lista real con sus descripciones es NO VERIFICADO.

### ComprobantesTipoConsultar

Tipos de comprobante que se pueden constatar ([MAN pág. 20-22]).

Request:

Elemento raíz: `ComprobantesTipoConsultar`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `CmpAuthRequest` | 0..1 | Credenciales (ver Autenticación) |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT que consulta (representada). Manual: S |


Response:

Elemento raíz: `ComprobantesTipoConsultarResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `ComprobantesTipoConsultarResult` | `CbteTipoResponse` | 0..1 | Resultado |
| `ComprobantesTipoConsultarResult/ResultGet` | `ArrayOfCbteTipo` | 0..1 | Lista de valores |
| `ComprobantesTipoConsultarResult/ResultGet/CbteTipo` | `CbteTipo` | 0..n nil | Un tipo de comprobante |
| `ComprobantesTipoConsultarResult/ResultGet/CbteTipo/Id` | `int` | 1..1 | Código de tipo de comprobante, Int(3) |
| `ComprobantesTipoConsultarResult/ResultGet/CbteTipo/Desc` | `string` | 0..1 | Descripción, String(250) |
| `ComprobantesTipoConsultarResult/Errors` | `ArrayOfErr` | 0..1 | Errores de infraestructura, `Auth` y formato |
| `ComprobantesTipoConsultarResult/Errors/Err` | `Err` | 0..n nil | Un error |
| `ComprobantesTipoConsultarResult/Errors/Err/Code` | `int` | 1..1 | Código |
| `ComprobantesTipoConsultarResult/Errors/Err/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |
| `ComprobantesTipoConsultarResult/Events` | `ArrayOfEvt` | 0..1 | Eventos. En vivo: `<Evt><Code>0</Code></Evt>` sin `Msg` |
| `ComprobantesTipoConsultarResult/Events/Evt` | `Evt` | 0..n nil | Un evento |
| `ComprobantesTipoConsultarResult/Events/Evt/Code` | `int` | 1..1 | Código |
| `ComprobantesTipoConsultarResult/Events/Evt/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |


El manual muestra `CbteTipo{Id, Desc, FchDesde, FchHasta}`; **el WSDL sólo tiene `Id` y `Desc`**. Manda el WSDL. Sin ejemplo con valores en el manual.

### DocumentosTipoConsultar

Tipos de documento del receptor ([MAN pág. 22-24]).

Request:

Elemento raíz: `DocumentosTipoConsultar`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `CmpAuthRequest` | 0..1 | Credenciales (ver Autenticación) |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT que consulta (representada). Manual: S |


Response:

Elemento raíz: `DocumentosTipoConsultarResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `DocumentosTipoConsultarResult` | `DocTipoResponse` | 0..1 | Resultado |
| `DocumentosTipoConsultarResult/ResultGet` | `ArrayOfDocTipo` | 0..1 | Lista de valores |
| `DocumentosTipoConsultarResult/ResultGet/DocTipo` | `DocTipo` | 0..n nil | Un tipo de documento |
| `DocumentosTipoConsultarResult/ResultGet/DocTipo/Id` | `string` | 0..1 | Código de tipo de documento (string en el WSDL; Int(2) en el manual) |
| `DocumentosTipoConsultarResult/ResultGet/DocTipo/Desc` | `string` | 0..1 | Descripción, String(250) |
| `DocumentosTipoConsultarResult/Errors` | `ArrayOfErr` | 0..1 | Errores de infraestructura, `Auth` y formato |
| `DocumentosTipoConsultarResult/Errors/Err` | `Err` | 0..n nil | Un error |
| `DocumentosTipoConsultarResult/Errors/Err/Code` | `int` | 1..1 | Código |
| `DocumentosTipoConsultarResult/Errors/Err/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |
| `DocumentosTipoConsultarResult/Events` | `ArrayOfEvt` | 0..1 | Eventos. En vivo: `<Evt><Code>0</Code></Evt>` sin `Msg` |
| `DocumentosTipoConsultarResult/Events/Evt` | `Evt` | 0..n nil | Un evento |
| `DocumentosTipoConsultarResult/Events/Evt/Code` | `int` | 1..1 | Código |
| `DocumentosTipoConsultarResult/Events/Evt/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |


El manual dice `Id` Int(2) y `FchDesde`/`FchHasta`; el WSDL tiene `Id` **string** y sólo `Id`, `Desc`. El ejemplo de request del manual tiene una errata (`< DocumentosTipoConsultar>` con espacio).

### OpcionalesTipoConsultar

Tipos de dato opcional para `CmpReq/Opcionales` ([MAN pág. 24-25]). El manual dice que `Opcionales` está "reservado para usos futuros".

Request:

Elemento raíz: `OpcionalesTipoConsultar`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `CmpAuthRequest` | 0..1 | Credenciales (ver Autenticación) |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT que consulta (representada). Manual: S |


Response:

Elemento raíz: `OpcionalesTipoConsultarResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `OpcionalesTipoConsultarResult` | `OpcionalTipoResponse` | 0..1 | Resultado |
| `OpcionalesTipoConsultarResult/ResultGet` | `ArrayOfOpcionalTipo` | 0..1 | Lista de valores |
| `OpcionalesTipoConsultarResult/ResultGet/OpcionalTipo` | `OpcionalTipo` | 0..n nil | Un tipo de opcional |
| `OpcionalesTipoConsultarResult/ResultGet/OpcionalTipo/Id` | `string` | 0..1 | Identificador del opcional, String(4) |
| `OpcionalesTipoConsultarResult/ResultGet/OpcionalTipo/Desc` | `string` | 0..1 | Descripción, String(250) |
| `OpcionalesTipoConsultarResult/Errors` | `ArrayOfErr` | 0..1 | Errores de infraestructura, `Auth` y formato |
| `OpcionalesTipoConsultarResult/Errors/Err` | `Err` | 0..n nil | Un error |
| `OpcionalesTipoConsultarResult/Errors/Err/Code` | `int` | 1..1 | Código |
| `OpcionalesTipoConsultarResult/Errors/Err/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |
| `OpcionalesTipoConsultarResult/Events` | `ArrayOfEvt` | 0..1 | Eventos. En vivo: `<Evt><Code>0</Code></Evt>` sin `Msg` |
| `OpcionalesTipoConsultarResult/Events/Evt` | `Evt` | 0..n nil | Un evento |
| `OpcionalesTipoConsultarResult/Events/Evt/Code` | `int` | 1..1 | Código |
| `OpcionalesTipoConsultarResult/Events/Evt/Msg` | `string` | 0..1 | Mensaje (String(255) en `Obs`) |


El manual agrega `FchDesde`/`FchHasta`; el WSDL no los tiene.

### ComprobanteDummy

Ping de infraestructura, sin `Auth` ([MAN pág. 26]).

Request: `<wsc:ComprobanteDummy/>` (el elemento existe pero no tiene hijos).

Response:

Elemento raíz: `ComprobanteDummyResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `ComprobanteDummyResult` | `DummyResponse` | 0..1 | Resultado |
| `ComprobanteDummyResult/AppServer` | `string` | 0..1 | Servidor de aplicaciones (`OK`) |
| `ComprobanteDummyResult/DbServer` | `string` | 0..1 | Servidor de base de datos (`OK`) |
| `ComprobanteDummyResult/AuthServer` | `string` | 0..1 | Servidor de autenticación (`OK`) |


Respuesta real (homologación, 2026-10-02):

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://servicios1.afip.gob.ar/wscdc/"><ambiente>Homologacion-Ext - srt</ambiente><fecha>2026-10-02T15:11:47.6553224-03:00</fecha><id>3.1.1.0</id></FEHeaderInfo></soap:Header><soap:Body><ComprobanteDummyResponse xmlns="http://servicios1.afip.gob.ar/wscdc/"><ComprobanteDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></ComprobanteDummyResult></ComprobanteDummyResponse></soap:Body></soap:Envelope>
```

## Validaciones y errores

Cómo se reparten (manual pág. 8 y 14):

| Tipo | Dónde | `Resultado` |
|---|---|---|
| Infraestructura (500-503) y `Auth` (600-602) | `Errors/Err` | En `ComprobanteConstatar`: R (visto en vivo con 600). En los recuperadores no hay `Resultado` |
| Validaciones excluyentes **de formato** (1-10) | `Errors/Err`, "todas las causas involucradas" | R |
| Validaciones excluyentes **funcionales** (100-118 y 150-153) | **`Observaciones/Obs`**, no `Errors` | R |
| Validación no excluyente (200) | `Observaciones/Obs` | A |

Tabla completa del manual. La columna "Texto / condición" es la descripción de la validación del manual, **no** el `Msg` que devuelve el servicio. Los únicos `Msg` reales conocidos son los de los ejemplos (2, 108, 200) y los observados en vivo (500, 600).

Errores de infraestructura (pág. 4), para todas las operaciones:

| Código | Causa |
|---:|---|
| 500 | Error interno de aplicación. (En vivo también sale como "Campo Auth no fue ingresado o esta mal formado.") |
| 501 | Error interno de base de datos. |
| 502 | Transacción Activa |
| 503 | No existen datos en nuestros registros. |

`ComprobanteConstatar` (pág. 4 y 11-14):

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | Errors/Err | (infraestructura) | 4 |
| 501 | Error interno de base de datos. | Error | Errors/Err | (infraestructura) | 4 |
| 502 | Transacción Activa | Error | Errors/Err | (infraestructura) | 4 |
| 503 | No existen datos en nuestros registros. | Error | Errors/Err | (infraestructura) | 4 |
| 600 | No se corresponden token y firma. Usuario no autorizado a realizar esta operación | Error | Errors/Err | Cuit | 11 |
| 601 | CUIT representada no incluida en token. | Error | Errors/Err | Cuit | 11 |
| 602 | CUIT representada no se encuentre activa y vigente. | Error | Errors/Err | Cuit | 11 |
| 1 | El modo indicado debe ser alfanumérico de 4 caracteres como máximo y debe ser alguno de los devueltos por el método ComprobantesModalidadConsultar() | Error | Errors/Err (Resultado R) | CbteModo | 11 |
| 2 | La cuit del emisor indicado debe ser numérica de 11 dígitos y debe ser valida. | Error | Errors/Err (Resultado R) | CuitEmisor | 11 |
| 3 | Campo `<PtoVta>` debe ser numérico de 5 dígitos como máximo y debe estar comprendido entre 1 y 99998. | Error | Errors/Err (Resultado R) | PtoVta | 11 |
| 4 | El tipo de comprobante debe ser numérico de 3 dígitos como máximo y debe ser alguno de los definidos en el método ComprobantesTipoConsultar() | Error | Errors/Err (Resultado R) | CbteTipo | 11 |
| 5 | Campo correspondiente al N° de comprobante, debe ser numérico de 8 dígitos como máximo y se debe encontrar entre 1 y 99999999. | Error | Errors/Err (Resultado R) | CbteNro | 11 |
| 6 | Campo correspondiente a la fecha del comprobante, debe tener el siguiente formato yyyymmdd | Error | Errors/Err (Resultado R) | CbteFch | 11 |
| 7 | Campo correspondiente al importe total del comprobante. Debe ser numérico mayor o igual a 0 de 13 enteros y 2 decimales. | Error | Errors/Err (Resultado R) | ImpTotal | 11 |
| 8 | El tipo de documento del receptor debe ser numérico de 2 dígitos y debe ser alguno de los devueltos por el método DocumentosTipoConsultar(). | Error | Errors/Err (Resultado R) | DocTipoReceptor | 11 |
| 9 | El número de documento del receptor, debe contener un valor numérico de 11 caracteres. Si el número del doucumento contiene letras no informarlas, solamente informar los caracteres numéricos. | Error | Errors/Err (Resultado R) | DocNroReceptor | 11 |
| 10 | Código de autorización del comprobante, debe ser de 14 caracteres numéricos. | Error | Errors/Err (Resultado R) | CodAutorizacion | 11 |
| 100 | Verificar que el CAE/CAI/CAEA exista registrado y autorizado en las bases del organismo. | Rechaza | Observaciones/Obs (Resultado R) | CodAutorizacion | 12 |
| 101 | La fecha del comprobante `<CbteFch>` no podrá ser an terior a 20130101. | Rechaza | Observaciones/Obs (Resultado R) | CbteFch | 12 |
| 102 | Verifica que la CUIT del emisor informada se corresponda con la cuit registrada bajo el código de autorización `<CodAutorizacion>`. | Rechaza | Observaciones/Obs (Resultado R) | CodAutorizacion / CuitEmisor | 12 |
| 103 | Verifica que el tipo de comprobante `<CbteTipo>` se corresponda con el registrado bajo el código de autorización informado `<CodAutorizacion>` | Rechaza | Observaciones/Obs (Resultado R) | CodAutorizacion / CbteTipo | 12 |
| 104 | Verifica que el punto de venta `<PtoVta>` se corresponda con el punto de venta registrado bajo el código de autorización informado `<CodAutorizacion>` | Rechaza | Observaciones/Obs (Resultado R) | CodAutorizacion / PtoVta | 12 |
| 105 | Verifica que el Nº de comprobante `<CbteNro>` se corresponda con el Nº de comprobante registrado bajo el código de autorización informado `<CodAutorizacion>` | Rechaza | Observaciones/Obs (Resultado R) | CodAutorizacion / CbteNro | 12 |
| 106 | Para modo `<CbteModo>` = “CAEA” , en caso de no encontrar el comprobante rendido, verifica que se encuentre vigente la rendición del mismo. | Rechaza | Observaciones/Obs (Resultado R) | CbteModo / CodAutorizacion | 12 |
| 107 | Para modo `<CbteModo>` = “CAE” o `<CbteModo>` = “CAEA”, verifica que la fecha del comprobante `<CbteFch>` se corresponda con el código de autorización informado `<CodAutorizacion>` | Rechaza | Observaciones/Obs (Resultado R) | CbteModo / CodAutorizacion / CbteFch | 12 |
| 108 | Para modo `<CbteModo>` = “CAI” verifica que la fecha del comprobante `<CbteFch>` se encuentre dentro del rango habilitado para el código de autorización informado `<CodAutorizacion>` | Rechaza | Observaciones/Obs (Resultado R) | CbteModo / CodAutorizacion / CbteFch | 12 |
| 109 | Para modo `<CbteModo>` = “CAEA”, verifica que el punto de venta sea un punto de venta habilitado para emitir comprobantes. | Rechaza | Observaciones/Obs (Resultado R) | CbteModo / CodAutorizacion / PtoVta | 13 |
| 110 | Verificar que el importe de la operación informado se corresponda con lo registrado en las bases del organismo. Para los tipos de comprobantes sin ImpTotal se debe informar el campo en cero. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=1. | Rechaza | Observaciones/Obs (Resultado R) | CbteModo / CodAutorizacion / ImpTotal | 13 |
| 111 | Verifica que el tipo de documento del receptor `<DocTipoReceptor>` se corresponda con el registrado bajo el código de autorización informado `<CodAutorizacion>` | Rechaza | Observaciones/Obs (Resultado R) | CodAutorizacion / DocTipoReceptor | 13 |
| 112 | Verifica que el número de documento del receptor `<DocNroReceptor>` se corresponda con el registrado bajo el código de autorización informado `<CodAutorizacion>` | Rechaza | Observaciones/Obs (Resultado R) | CodAutorizacion / DocNroReceptor | 13 |
| 113 | Para comprobantes tipo “A”, “A con leyenda operación sujeta a retención” o MiPyme el tipo de documento del receptor es obligatorio informarlo y debe ser CUIT (CbteTipo = 80). | Rechaza | Observaciones/Obs (Resultado R) | CbteTipo DocTipoReceptor | 13 |
| 114 | Para comprobantes tipo “A” o tipo “A con leyenda operación sujeta a retención”, el Nº de documento del receptor es obligatorio informarlo. | Rechaza | Observaciones/Obs (Resultado R) | CbteTipo DocNroReceptor | 13 |
| 115 | Para comprobantes tipo B, C , R, 31, 30, 37, 38, 41 y 49 el tipo de documento del receptor solo es obligatorio informarlo cuando el importe es superior a 10.000.000 pesos. | Rechaza | Observaciones/Obs (Resultado R) | CbteTipo DocTipoReceptor | 13 |
| 116 | Para comprobantes tipo B, C , R, 31, 30, 37, 38, 41 y 49 , el número de documento del receptor solo es obligatorio informarlo cuando el importe es superior a 10.000.000 pesos. | Rechaza | Observaciones/Obs (Resultado R) | CbteTipo DocNroReceptor | 13 |
| 117 | Si informa `<DocTipoReceptor>` o `<DocNroReceptor>` es obligatorio informar ambos. | Rechaza | Observaciones/Obs (Resultado R) | CbteTipo DocTipoReceptor DocNroReceptor | 14 |
| 118 | Si informa comprobante del tipo T (195, 196, 197), los tipos de documento válido son 80, 91, 94, 96. | Rechaza | Observaciones/Obs (Resultado R) | CbteTipo DocTipoReceptor | 14 |
| 150 | Si envía `<Opcionales>`, `<Opcional>` es obligatorio. | Rechaza | Observaciones/Obs (Resultado R) | Opcionales | 14 |
| 151 | El campo `<Id>` en `<Opcionales>` es obligatorio y debe ser alguno de los devueltos por el método OpcionalesTipoConsultar. | Rechaza | Observaciones/Obs (Resultado R) | Opcional | 14 |
| 152 | El campo `<Id>` en `<Opcionales>` es obligatorio y no debe repetirse." | Rechaza | Observaciones/Obs (Resultado R) | Opcional | 14 |
| 153 | El campo `<Valor>` en Opcionales es obligatorio | Rechaza | Observaciones/Obs (Resultado R) | Opcional | 14 |
| 200 | Para modo `<CbteModo>` = “CAEA” , en caso de no encontrar el comprobante rendido, verifica que se encuentre vigente la rendición del mismo. Si se encuentra en vigencia el comprobante queda observado por no encontrarse rendido. | Observa | Observaciones/Obs (Resultado A) | CodAutorizacion | 14 |


Los recuperadores (`ComprobantesModalidadConsultar`, `ComprobantesTipoConsultar`, `DocumentosTipoConsultar`, `OpcionalesTipoConsultar`) sólo tienen los de infraestructura; el manual no les lista los de `Auth`, pero en vivo el 600 sale igual. Todo esto está en `wscdc-codigos.json` (69 filas: 41 de `ComprobanteConstatar` y 7 por cada recuperador).

El historial (pág. 2) dice que la versión 0.4 "modifica la descripción de los siguientes códigos de error: 113, 114" por el reemplazo de los comprobantes clase M.

## Tablas y datos

El manual **no lista** los valores de ninguna tabla de parámetros (modalidades, tipos de comprobante, tipos de documento, opcionales). Lo que sí se puede afirmar:

- **Modalidades (`CbteModo`)**: `CAE`, `CAEA` y `CAI`, nombrados en las validaciones 100, 106, 107, 108 y 109. Las descripciones exactas que devuelve `ComprobantesModalidadConsultar`: NO VERIFICADO.
- **Tipos de comprobante**: el manual menciona "A", "A con leyenda operación sujeta a retención", MiPyME, B, C, R, 31, 30, 37, 38, 41, 49 y T (195, 196, 197). O sea, wscdc constata comprobantes de varios servicios (wsfev1, wsmtxca, wsct, Factura de Crédito MiPyME, CAI). La lista real de `ComprobantesTipoConsultar`: NO VERIFICADO. Para los códigos de tipo de comprobante de factura electrónica en general ver `wsfev1.md` §7.1.
- **Tipos de documento**: el manual menciona 80 (CUIT), 91, 94 y 96 (validación 118). La tabla general de documentos de factura electrónica está en `wsfev1.md` §7.2. Que `DocumentosTipoConsultar` devuelva exactamente esa tabla: NO VERIFICADO.
- **Opcionales**: ninguno documentado ("reservado para usos futuros").

Datos de prueba del manual (sólo sirven como formato):

| Caso | CuitEmisor | PtoVta | CbteTipo | CbteNro | CbteFch | ImpTotal | CodAutorizacion | Receptor |
|---|---|---|---|---|---|---|---|---|
| CAE | 20000000001 | 1 | 1 | 2 | 20101014 | 300.8 | 60428000005029 | 80 / 30000000007 |
| CAEA no rendido | 30000000007 | 1112 | 6 | 7 | 20110315 | 2600 | 21088621021111 | 80 / 20000000001 |
| CAI fuera de rango | 30000000007 | 63 | 4 | 20 | 20130801 | 150.88 | 12345678901235 | 80 / 30000000007 |

## Comportamiento a simular

**Qué compara.** wscdc no tiene estado propio: lee el registro de comprobantes que generan los servicios emisores. En ArcaSim, `ComprobanteConstatar` tiene que leer el mismo almacén que escriben wsfev1 (`FECAESolicitar`, `FECAEARegInformativo`), wsmtxca (`autorizarComprobante`, `informarComprobanteCAEA`) y, si se decide, wsfexv1 (`FEXAuthorize`). Busca por `CodAutorizacion` y compara campo a campo:

| Dato enviado | Contra qué | Código si no coincide |
|---|---|---|
| `CodAutorizacion` | Que exista registrado y autorizado (CAE, CAEA o CAI) | 100 |
| `CbteFch` | No anterior a 20130101 | 101 |
| `CuitEmisor` | CUIT del emisor registrado bajo ese código | 102 |
| `CbteTipo` | Tipo registrado | 103 |
| `PtoVta` | Punto de venta registrado | 104 |
| `CbteNro` | Número registrado | 105 |
| `CbteFch` (CAE y CAEA) | Fecha registrada | 107 |
| `CbteFch` (CAI) | Dentro del rango habilitado del CAI | 108 |
| `PtoVta` (CAEA) | Punto de venta habilitado | 109 |
| `ImpTotal` | Importe registrado, con tolerancia: error relativo ≤ 0,01 % **o** error absoluto ≤ 1, redondeo Round Half Even (pág. 13 y 27). Los comprobantes sin importe total se mandan con 0 | 110 |
| `DocTipoReceptor` | Tipo de documento del receptor registrado | 111 |
| `DocNroReceptor` | Número de documento del receptor registrado | 112 |

Reglas de receptor que no dependen del registro (113-118): para A, A con leyenda y MiPyME el tipo de documento es obligatorio y debe ser 80; para A y A con leyenda el número es obligatorio; para B, C, R, 31, 30, 37, 38, 41 y 49 sólo son obligatorios si el importe supera $10.000.000; si viene uno de los dos tienen que venir ambos; para T (195-197) el tipo debe ser 80, 91, 94 o 96.

**Mapeo de nombres** entre servicios, para leer el registro común:

| wscdc | wsfev1 | wsmtxca | wsfexv1 |
|---|---|---|---|
| `CuitEmisor` | `Auth/Cuit` del emisor | `authRequest/cuitRepresentada` | `Auth/Cuit` |
| `CbteTipo` | `CbteTipo` | `codigoTipoComprobante` | `Cbte_Tipo` |
| `PtoVta` | `PtoVta` | `numeroPuntoVenta` | `Punto_vta` |
| `CbteNro` | `CbteDesde` (= `CbteHasta`) | `numeroComprobante` | `Cbte_nro` |
| `CbteFch` (yyyymmdd) | `CbteFch` (yyyymmdd) | `fechaEmision` (xsd:date, yyyy-mm-dd) | `Fecha_cbte` (yyyymmdd) |
| `ImpTotal` | `ImpTotal` | `importeTotal` | `Imp_total` |
| `CodAutorizacion` | `CAE` o `CAEA` | `CAE` o `codigoAutorizacion` | `Cae` |
| `DocTipoReceptor` / `DocNroReceptor` | `DocTipo` / `DocNro` | `codigoTipoDocumento` / `numeroDocumento` | no hay (receptor del exterior) |

Si wscdc constata comprobantes E (19, 20, 21) de wsfexv1 es NO VERIFICADO: el manual no los menciona.

**Resultado.**

- Todo bien: `Resultado`=`A`, `CmpResp` = eco del request, `FchProceso` = momento de la consulta en `yyyyMMddHHmmss`.
- Falla de formato: `Resultado`=`R` y **todas** las causas en `Errors`.
- Falla funcional: `Resultado`=`R` y todas las causas en `Observaciones`.
- CAEA no rendido pero dentro del plazo para rendirlo: `Resultado`=`A` con `Obs` 200. Si ya venció el plazo, rechaza con 106 (texto del manual: "en caso de no encontrar el comprobante rendido, verifica que se encuentre vigente la rendición").
- El orden de evaluación entre las funcionales y si se cortan en la primera: NO VERIFICADO. Los ejemplos del manual muestran una sola observación por caso.

**Idempotencia.** Es una consulta sin efectos: "con simplemente enviar la misma solicitud todo quedaría resuelto" (pág. 15). Repetir da la misma respuesta salvo `FchProceso`.

**CAI.** Los comprobantes con CAI no los emite ningún web service; el simulador necesita una forma de sembrar CAIs con su rango de fechas si quiere cubrir los códigos 100 y 108 para ese modo.

## No verificado

- El WSAA service id `wscdc` (sólo en [SEC]).
- Los valores reales de las cuatro tablas de parámetros y si `ComprobantesTipoConsultar` incluye los tipos E de exportación.
- Los textos reales (`Msg`) de casi todos los códigos: el manual sólo da la condición. Conocidos: 2, 108, 200 (ejemplos), 500 y 600 (vivo).
- El texto real de 601 y 602, y si el 600 por CUIT fuera de las relaciones del token sale como en wsfev1 ("No apareció CUIT en lista de relaciones").
- El orden de evaluación entre validaciones funcionales, y si devuelve una o todas.
- Si con `Resultado` A la respuesta incluye `Events` (en los ejemplos del manual no aparece; con error de token sí).
- Cómo responde a un valor no numérico en un campo `int`/`long` y a un `SOAPAction` inexistente: se asume igual que wsfexv1 (mismo stack ASMX), no se probó en wscdc.
- La tolerancia exacta del 110 con `ImpTotal` = 0.
