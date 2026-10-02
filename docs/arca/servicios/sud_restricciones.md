# sud_restricciones

Consulta de deuda por CUIT. Un organismo externo (el manual dice "organismo externo"; el catálogo describe restricciones como "reservado a entidades bancarias" y contrataciones como la verificación de deuda de proveedores del Estado) pregunta si una CUIT tiene deuda y, con `tieneDeudaV2`, en qué impuesto y período.

Desde la versión 1.3 del manual (30/10/2023) la respuesta está acotada a dos impuestos:

- 301, Aportes Seguridad Social: falta de presentación y falta de pago.
- 351, Contribuciones Seguridad Social: falta de pago.

`sud_contrataciones` **no es otro servicio**: su manual (`/ws/SudContrataciones/manual_sud_contrataciones.pdf`) es una versión vieja (1.1, 11/10/2018) del mismo documento, con el mismo ID, el mismo endpoint y el mismo WSDL. Ver la sección "sud_contrataciones" abajo.

Fuentes:

- Manual 1.3: `https://www.afip.gob.ar/ws/SudRestricciones/manual_sud_restricciones_1.3.pdf` (la portada dice 17/04/19; el historial llega a 30/10/2023).
- Manual de contrataciones 1.1: `https://www.afip.gob.ar/ws/SudContrataciones/manual_sud_contrataciones.pdf`.
- WSDL de producción, descargado el 2026-10-02.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, Spring-WS (por la forma: `SudPortSoap11`, `SOAP-ENV` en mayúsculas, elementos `*Request`/`*Response` con tipos `*RequestType`) |
| SOAP | 1.1, document/literal |
| Endpoint homologación | `https://sud-ws.cloudhomo.afip.gob.ar/sud_restricciones` (manual) |
| Endpoint producción | `https://sud-ws.cloud.afip.gob.ar/sud_restricciones` |
| WSDL | `?wsdl` sobre cada endpoint |
| targetNamespace | `http://afip.gob.ar/ws/sud_restricciones` (esquema con `elementFormDefault="qualified"`) |
| portType / binding / service | `SudPort` / `SudPortSoap11` / `SudPortService`, port `SudPortSoap11` |
| SOAPAction | `""` en las tres operaciones |
| Archivo guardado | `wsdl/sud_restricciones-produccion.wsdl` (no hay copia de homologación, ver abajo). No importa nada: el esquema va inline |
| WSAA service id | `sud_restricciones` (manual 1.3, sección 2.4, y manual de contrataciones) |
| Versión | No expone versión en la respuesta |

**Homologación no se pudo descargar** (2026-10-02). Sin `-k`, curl con schannel falla con `SEC_E_UNTRUSTED_ROOT`: el certificado es `CN=*.cloudhomo.afip.gob.ar`, emitido por `CN=AFIP-SSL-CA-TEST, O=AFIP, C=AR`, que no es una CA pública. Con `-k` el handshake TLS pasa, pero el servidor corta la conexión (`Recv failure: Connection was reset`) en cualquier ruta, incluso `/`, y también desde Python sin verificación. Se guardó el WSDL de **producción**. Se supone idéntico salvo el `soap:address`: **NO VERIFICADO**.

**Trampa de namespace.** Los ejemplos de request de ambos manuales usan `xmlns:sud="http://afip.gob.ar/ws/sud"`, y los de respuesta de `tieneDeuda` también. El WSDL real usa `http://afip.gob.ar/ws/sud_restricciones`. Probado en producción el 2026-10-02: un `tieneDeudaRequest` con el namespace del manual devuelve **HTTP 404, `Content-Length: 0`, `Content-Type: text/plain`** (Spring-WS no encuentra endpoint para ese QName). El simulador tiene que seguir al WSDL y responder 404 vacío con el namespace del manual.

## Autenticación

Los cuatro campos van **sueltos dentro del elemento de request**, sin wrapper, en este orden (WSDL):

```xml
<sud:tieneDeudaRequest>
  <sud:cuit>20000000004</sud:cuit>
  <sud:cuitRepresentado>20000000006</sud:cuitRepresentado>
  <sud:token>...</sud:token>
  <sud:sign>...</sud:sign>
</sud:tieneDeudaRequest>
```

- `cuit` (`xs:long`): la CUIT consultada.
- `cuitRepresentado` (`xs:long`): "Debe coincidir con alguna de las CUITS listadas en la sección relations del token enviado. Debe ser en representación de qué organismo se solicita la operación" (manual). El texto del manual lo llama `cuitRepresentada`; el elemento real es `cuitRepresentado`.
- `token`, `sign` (`xs:string`): del TA de WSAA. `dummy` no los pide.

**Respuesta real con token falso** (producción, 2026-10-02, tres variantes: `token`=`abc`; `token` = un XML en base64 bien formado y `sign` falso; `token` y `sign` vacíos en `tieneDeudaV2Request`). Las tres dieron lo mismo:

```
HTTP/1.1 500 Server Error
Accept: text/xml, text/html, image/gif, image/jpeg, *; q=.2, */*; q=.2
SOAPAction: ""
Content-Type: text/xml;charset=utf-8
Content-Length: 369
Connection: close

<SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/"><SOAP-ENV:Header/><SOAP-ENV:Body><SOAP-ENV:Fault><faultcode>SOAP-ENV:Server</faultcode><faultstring xml:lang="en">El servicio SUD se encuentra congestionado, por favor vuelva a intentar mas tarde (nro transaccion: 763808542)</faultstring></SOAP-ENV:Fault></SOAP-ENV:Body></SOAP-ENV:Envelope>
```

- El número de transacción sube en cada llamada (763808542, 763808692, 763808697, 763808701 en segundos).
- **El servicio no distingue el error de autenticación**: un token inválido devuelve el mismo Fault genérico de "congestionado". No se sabe si es la respuesta a cualquier error interno o si el servicio de verdad estaba congestionado (el `dummy` dio todo OK en el mismo minuto). **NO VERIFICADO** qué devuelve con un token válido de otro servicio o con un `cuitRepresentado` fuera de relations.

## Operaciones

3 operaciones en el `portType` `SudPort`.

| Operación | Propósito | Entrada (elemento) | Salida (elemento) | Estado |
|---|---|---|---|---|
| `dummy` | Verifica infraestructura. Sin token | `dummyRequest` (vacío) | `dummyResponse/return/{appserver, authserver, dbserver}` | Ninguno |
| `tieneDeuda` | ¿La CUIT tiene deuda? | `tieneDeudaRequest{cuit, cuitRepresentado, token, sign}` | `tieneDeudaResponse{tieneDeuda: boolean, consultaId: long, metadata{fechaHora, servidor}}` | Consulta. Cada consulta genera un `consultaId` nuevo; no hay operación para releerlo |
| `tieneDeudaV2` | Igual, más el detalle de impuesto y período | `tieneDeudaV2Request{cuit, cuitRepresentado, token, sign}` | `tieneDeudaV2Response{tieneDeuda, deudas{deuda[1..n]{impuesto: long, periodoFiscal: string}}, consultaId, metadata{fechaHora, servidor}}` | Consulta, igual que arriba |

Detalles del esquema que importan para generar respuestas válidas:

- En `tieneDeudaResponse`, `metadata` es **obligatorio** en el WSDL, aunque el ejemplo del manual no lo muestra.
- En `tieneDeudaV2Response`, `deudas` es obligatorio y `deuda` tiene `minOccurs` 1 (por defecto). **Si no hay deuda, el esquema no admite un `deudas` vacío.** Qué devuelve el servicio real en ese caso está **NO VERIFICADO**.
- `periodoFiscal`: `YYYYMM` (manual, 4.1). `fechaHora`: en el ejemplo, `2018-10-10T15:41:08-0300` (sin `:` en el offset). `servidor`: `servicios-externos` en el ejemplo.
- `consultaId`: en los ejemplos, `214454` y `2005`.

Respuesta real de `dummy` (producción, 2026-10-02), HTTP 200:

```xml
<SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/"><SOAP-ENV:Header/><SOAP-ENV:Body><ns2:dummyResponse xmlns:ns2="http://afip.gob.ar/ws/sud_restricciones"><ns2:return><ns2:appserver>OK</ns2:appserver><ns2:authserver>OK</ns2:authserver><ns2:dbserver>OK</ns2:dbserver></ns2:return></ns2:dummyResponse></SOAP-ENV:Body></SOAP-ENV:Envelope>
```

Headers de esa respuesta: `Content-Type: text/xml;charset=utf-8`, `SOAPAction: ""` y `Accept: text/xml, text/html, image/gif, image/jpeg, *; q=.2, */*; q=.2` (el servidor devuelve los headers `Accept` y `SOAPAction` en la respuesta).

Ejemplo de respuesta de `tieneDeudaV2` (manual 1.3, 3.2.3):

```xml
<ns2:tieneDeudaV2Response xmlns:ns2="http://afip.gob.ar/ws/sud_restricciones">
  <ns2:tieneDeuda>true</ns2:tieneDeuda>
  <ns2:deudas>
    <ns2:deuda><ns2:impuesto>301</ns2:impuesto><ns2:periodoFiscal>201201</ns2:periodoFiscal></ns2:deuda>
    <ns2:deuda><ns2:impuesto>301</ns2:impuesto><ns2:periodoFiscal>201202</ns2:periodoFiscal></ns2:deuda>
  </ns2:deudas>
  <ns2:consultaId>214454</ns2:consultaId>
  <ns2:metadata>
    <ns2:fechaHora>2018-10-10T15:41:08-0300</ns2:fechaHora>
    <ns2:servidor>servicios-externos</ns2:servidor>
  </ns2:metadata>
</ns2:tieneDeudaV2Response>
```

## Errores

No hay estructura de error en la respuesta: todo error es `SOAP-ENV:Fault` con HTTP 500.

| Caso | Fuente | Respuesta |
|---|---|---|
| CUIT que "no corresponde" | Manual (3.2 y 3.3) | `faultstring` "CUIT sud:cuit INVALIDA". El texto literal del manual incluye `sud:cuit`; si en la realidad se reemplaza por el número está **NO VERIFICADO** |
| Token inválido, malformado o vacío | En vivo, producción, 2026-10-02 | HTTP 500, `faultcode` `SOAP-ENV:Server`, `faultstring xml:lang="en"` "El servicio SUD se encuentra congestionado, por favor vuelva a intentar mas tarde (nro transaccion: N)" |
| Namespace del manual (`http://afip.gob.ar/ws/sud`) | En vivo, producción, 2026-10-02 | HTTP 404, cuerpo vacío, `Content-Type: text/plain` |

## Comportamiento a simular

- `dummy` siempre OK, sin token.
- Validar el TA de WSAA para `sud_restricciones`; cualquier falla de token responde con el Fault genérico "congestionado" y un `nro transaccion` creciente. Para depurar, conviene que ArcaSim pueda **opcionalmente** devolver un mensaje más claro, pero el modo fiel es el genérico.
- `cuitRepresentado` tiene que estar en las relations del TA (regla del manual; la respuesta cuando no está es **NO VERIFICADO**: usar el mismo Fault genérico).
- Una tabla de deudas por CUIT, limitada a impuestos 301 y 351 con períodos `YYYYMM`. `tieneDeuda` = hay al menos una fila. `tieneDeudaV2` lista las filas.
- CUIT con dígito verificador inválido o inexistente: Fault "CUIT sud:cuit INVALIDA" (texto literal del manual).
- `consultaId` secuencial global; `metadata.fechaHora` con offset `-0300` sin dos puntos; `servidor` = `servicios-externos`.
- Un QName de request con otro namespace responde 404 vacío.
- No hay estado que crear: es un servicio de solo lectura.

## sud_contrataciones

- Manual: `https://www.afip.gob.ar/ws/SudContrataciones/manual_sud_contrataciones.pdf`. Título interno: "Consulta servicio de deuda sud_restricciones", versión 1.1 del 11/10/2018. Es la misma versión 1.1 que figura en el historial del manual de restricciones.
- Mismo ID de servicio (`sud_restricciones`), mismos endpoints, mismo WSDL.
- Diferencia de documentación: describe una sola operación de negocio, `tieneDeuda`, pero con la respuesta que hoy tiene `tieneDeudaV2` (con `deudas`). En el WSDL actual, `tieneDeudaResponse` **no** tiene `deudas`. Seguir al WSDL.
- No dice que la consulta esté acotada a los impuestos 301 y 351 (eso se agregó en la 1.3).

## No verificado

- El WSDL de homologación (inaccesible) y si difiere del de producción.
- La respuesta de `tieneDeudaV2` para una CUIT sin deuda (el esquema obliga a al menos un `deuda`).
- Qué Fault da un token válido pero sin `cuitRepresentado` en relations, o un TA de otro servicio.
- Si "CUIT sud:cuit INVALIDA" sale con ese texto literal o con el número.
- Si el Fault "congestionado" es la respuesta normal al token inválido o una falla del momento.
- Quién puede pedir acceso: el catálogo dice "reservado a entidades bancarias" para restricciones y "organismos" para contrataciones; los manuales hablan de "organismo externo".
