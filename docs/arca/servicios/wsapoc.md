# wsapoc

Consulta de **contribuyentes apócrifos** (base APOC): la lista de CUIT que ARCA calificó como apócrifas (facturas truchas). Es la misma información que se publica libremente en el micrositio de Facturación Apócrifa; el WS sirve para integrarla en sistemas. Lo puede usar cualquier empresa u organismo que dé de alta el servicio "wsapoc" en el Administrador de Relaciones.

Fuentes:

- Manual "WSAPOC Webservice Consulta contribuyentes apócrifos" 1.0.9 (21/01/2025): `https://www.afip.gob.ar/ws/wsapoc/ManualUsuario-1.0.9.pdf`. Los ejemplos de mensajes del PDF son imágenes y no se pudieron extraer.
- WSDL de homologación y producción, descargados el 2026-10-02 (iguales salvo el `soap:address`).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | .NET. El WSDL tiene estilo WCF (mensajes `Service_Dummy_InputMessage`, namespaces `msc`, `wsam`, `wsp`) y la ruta se llama `Service.asmx`, pero no es ASMX clásico: responde con prefijo `s:` y, en SOAP 1.2, agrega WS-Addressing. Probablemente SoapCore sobre ASP.NET Core: **NO VERIFICADO** |
| SOAP | 1.1 (`ServiceSoap`) y 1.2 (`ServiceSoap12`), document/literal |
| Endpoint homologación | `https://eapoc-ws-qaext.afip.gob.ar/Service.asmx` |
| Endpoint producción | `https://eapoc-ws.afip.gob.ar/service.asmx` (minúscula en el manual y en el WSDL de producción) |
| WSDL | `?WSDL` |
| targetNamespace | `http://tempuri.org/` (el namespace por defecto de .NET, sin cambiar), `elementFormDefault="qualified"` |
| portType / service | `Service` / `Service` |
| SOAPAction | `http://tempuri.org/Service/<Operación>`. En vivo **no hace falta**: con `SOAPAction: ""` despacha igual por el elemento del body |
| Header de respuesta | `env-description`, `env-version` y `exec-ts`, cada uno con `xmlns="s"` (un namespace relativo, literalmente la letra "s"). No están en el WSDL |
| Archivo guardado | `wsdl/wsapoc-homologacion.wsdl` (sin imports) |
| WSAA service id | `wsapoc` (manual, Autenticación) |
| Versión | `env-version` = `v1.0.12`, `env-description` = `qa` (homologación, 2026-10-02) |

## Autenticación

```xml
<GetPublicacionAPOC xmlns="http://tempuri.org/">
  <Credencial>
    <Token>...</Token>
    <Sign>...</Sign>
    <CUITDelegado>20111111112</CUITDelegado>
  </Credencial>
  <cuit>20111111112</cuit>
</GetPublicacionAPOC>
```

`CUITDelegado` (string): "CUIT de la Empresa / Organismo consultante". No se llama `cuitRepresentada`.

**Los errores de autenticación vienen en la respuesta normal**, HTTP 200, con `codigo` 201 (verificado en homologación, 2026-10-02):

| Caso | `codigo` | `descripcion` |
|---|---|---|
| `Token` = `abc` | 201 | `Token invalido` |
| `Token` = TA bien formado en base64 (de otro servicio, vencido), `Sign` = `abc` | 201 | `Sign invalida` |

Respuesta exacta del primer caso (`Content-Type: text/xml; charset=utf-8`, `Content-Length: 622`, indentada con dos espacios y saltos LF):

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <s:Header>
    <env-description xmlns="s">qa</env-description>
    <env-version xmlns="s">v1.0.12</env-version>
    <exec-ts xmlns="s">10/02/2026 15:25:21</exec-ts>
  </s:Header>
  <s:Body>
    <GetPublicacionAPOCResponse xmlns="http://tempuri.org/">
      <GetPublicacionAPOCResult>
        <codigo>201</codigo>
        <descripcion>Token invalido</descripcion>
      </GetPublicacionAPOCResult>
    </GetPublicacionAPOCResponse>
  </s:Body>
</s:Envelope>
```

- `exec-ts` usa formato **norteamericano** `MM/dd/yyyy HH:mm:ss` (el 2 de octubre sale `10/02/2026`).
- Sin `resultados` cuando hay error.
- Header HTTP: `Strict-Transport-Security: max-age=15724800; includeSubDomains`. Sin cookies F5.

## Operaciones

4 operaciones.

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| `Dummy` | Infraestructura, sin auth | `Dummy` vacío | `DummyResponse/DummyResult{appserver?, dbserver?, authserver?}` | — |
| `GetAll` | Todos los casos vigentes de la base APOC | `Credencial` | `GetAllResult: MessageResponse` | Consulta |
| `GetPublicacionAPOC` | ¿Una CUIT está en la base? | `Credencial`, `cuit` (long) | `GetPublicacionAPOCResult: MessageResponse` con el caso si está vigente | Consulta. Clave: `cuit` |
| `GetAllByPublicacion` | Novedades publicadas en un rango de fechas | `Credencial`, `desde?`, `hasta?` (string `DD/MM/YYYY`) | `GetAllByPublicacionResult: MessageResponse` | Consulta. Filtra por `FechaPublicacion` |

**MessageResponse**: `codigo?` (string; "0" OK, mayor a cero error), `descripcion?`, `resultados?` (nillable) `{PublicacionAPOC*{Cuit (long), Descripcion?, FechaCondicion?, FechaPublicacion?}}`. Las fechas son string `DD/MM/YYYY` desde la versión 1.0.2 del manual. `FechaCondicion` = fecha de detección de la condición apócrifa; `FechaPublicacion` = fecha de publicación en la base.

Respuesta real de `Dummy` (homologación, 2026-10-02), HTTP 200, `Content-Length: 593`. **`authserver` responde `NO`**; el manual aclara que "puede no contestar OK, no es determinante para verificar la funcionalidad":

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <s:Header>
    <env-description xmlns="s">qa</env-description>
    <env-version xmlns="s">v1.0.12</env-version>
    <exec-ts xmlns="s">10/02/2026 15:25:21</exec-ts>
  </s:Header>
  <s:Body>
    <DummyResponse xmlns="http://tempuri.org/">
      <DummyResult>
        <appserver>OK</appserver>
        <dbserver>OK</dbserver>
        <authserver>NO</authserver>
      </DummyResult>
    </DummyResponse>
  </s:Body>
</s:Envelope>
```

Con SOAP 1.2 (`application/soap+xml`, sin `action`) la respuesta lleva además el header HTTP `SOAPAction: http://tempuri.org/Service/DummyResponse` y, en el envelope, `<Action s:mustUnderstand="1" xmlns="http://www.w3.org/2005/08/addressing">http://tempuri.org/Service/DummyResponse</Action>` antes de los tres headers de entorno.

## Errores

| Código | Tipo | Descripción (manual) |
|---|---|---|
| 0 | Ejecución | Ejecución OK |
| 100 | Error | Errores de configuración interna del servicio |
| 200 | Error | Errores de validación de parámetros |
| 201 | Error | Error al validar credenciales de autenticación (en vivo: "Token invalido", "Sign invalida") |
| 300 | Error | Errores internos de ejecución del servicio |

Los errores "excepcionales" son SOAP Fault estándar con `faultstring` descriptivo (manual). No se capturó ninguno.

## Comportamiento a simular

- Envelope con prefijo `s:`, indentación de dos espacios, headers `env-description`, `env-version` y `exec-ts` con `xmlns="s"`, `exec-ts` en `MM/dd/yyyy HH:mm:ss` hora local.
- Despacho por elemento del body; SOAPAction opcional. SOAP 1.2 con el header WS-Addressing `Action`.
- `Dummy` con `authserver` configurable (real: `NO`).
- Auth en `codigo` 201: texto "Token invalido" si el token no es un TA parseable, "Sign invalida" si la firma no verifica. El orden real (firma antes que vencimiento y servicio) sale de la segunda prueba.
- Base APOC simulada: lista de CUIT con `Descripcion`, `FechaCondicion` y `FechaPublicacion`. `GetPublicacionAPOC` de una CUIT que no está: `codigo` 0 y `resultados` vacío o ausente (**NO VERIFICADO** cuál). `GetAllByPublicacion` valida el formato `DD/MM/YYYY` (error 200).
- Solo lectura.

## No verificado

- El texto de los errores 200 (por ejemplo, fecha mal formada) y de 201 para token vencido, servicio equivocado o CUIT sin relación.
- Si `GetPublicacionAPOC` devuelve `resultados` vacío, ausente o nil para una CUIT limpia.
- Los ejemplos del manual (son imágenes en el PDF).
- El stack exacto (SoapCore).
