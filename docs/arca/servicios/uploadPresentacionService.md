# uploadPresentacionService

WebService de **Presentación de DDJJ** (SETI / Osiris). Presenta declaraciones juradas en ARCA de forma automática: el cliente sube el archivo de la DJ por MTOM y recibe un número único de transacción. Tiene tres perfiles, cada uno con su service id de WSAA (manual, 2.2):

| Perfil | Quién | WSAA service id |
|---|---|---|
| Organismo | Un organismo (ejemplo del manual: AGIP) presenta la DJ que el contribuyente completó en la web del organismo | `presentacionprocessor` |
| Contribuyente | El contribuyente presenta sus propias DJ, o las de quien represente | `djprocessorcontribuyente` |
| Contribuyente Controlador Fiscal | Igual, pero solo formularios de Controladores Fiscales | `djprocessorcontribuyente_cf` |

Fuentes:

- Manual "WebService Presentación de DDJJ", revisión del 24/11/2016: `https://www.afip.gob.ar/ws/wsddjj/WSPresentaciondeDDJJManualparaelDesarrollador.pdf`.
- WSDL de homologación (dos archivos), descargado el 2026-10-02. El de producción es igual salvo el `soap:address` y la URL del import.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, Apache CXF (WSDL partido en "hijo" con binding y "padre" con portType, estilo CXF; `Unexpected wrapper element` es un mensaje de CXF). Mismo host que el padrón (`awshomo` / `aws`) |
| SOAP | 1.1, document/literal, **MTOM** en el `upload` |
| Endpoint homologación | `https://awshomo.afip.gov.ar/setiws/webservices/uploadPresentacionService`. El manual da `awshomo.arca.gov.ar`, que **no resuelve DNS** |
| Endpoint producción | `https://aws.afip.gov.ar/setiws/webservices/uploadPresentacionService` (manual: `aws.arca.gov.ar`, no resuelve) |
| WSDL | `?wsdl`, que importa `?wsdl=uploadPresentacionServiceParent.wsdl` |
| Namespaces | WSDL hijo: `http://ws.implementation.service.domain.presentacion.seti.osiris.afip.gov/` (service `upload`, binding `uploadSoapBinding`, port `PresentacionProcessorMTOMImplPort`). WSDL padre y elementos: `http://domain.presentacion.seti.osiris.afip.gov/` (portType `PresentacionProcessorMTOMService`) |
| Esquema | `elementFormDefault="unqualified"`: solo el elemento raíz va con namespace |
| SOAPAction | `""` en las tres operaciones |
| Archivos guardados | `wsdl/uploadPresentacionService-homologacion.wsdl` y `wsdl/uploadPresentacionService/uploadPresentacionServiceParent.wsdl` |
| WSAA service id | Ver tabla de perfiles |
| Versión | El servicio no expone versión |

**Trampa de namespace, verificada.** El manual usa `http://domain.presentacion.seti.osiris.arca.gov/`. Con ese namespace el servicio real responde (homologación, 2026-10-02), HTTP 500:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server.processError</faultcode><faultstring>Unexpected wrapper element {http://domain.presentacion.seti.osiris.arca.gov/}dummy found.   Expected {http://domain.presentacion.seti.osiris.afip.gov/}dummy.</faultstring><detail><server>67</server><url>awshomo.afip.gov.ar</url><faultid>efbb8b92-0f59-46fb-846d-3a181b197436</faultid><datetime>2026-10-02 15:21:47</datetime></detail></soap:Fault></soap:Body></soap:Envelope>
```

(Tres espacios entre "found." y "Expected".)

## Autenticación

Campos sueltos dentro del elemento de operación, sin wrapper:

```xml
<dom:consulta xmlns:dom="http://domain.presentacion.seti.osiris.afip.gov/">
  <token>...</token>
  <sign>...</sign>
  <representadoCuit>20267335568</representadoCuit>
  ...
</dom:consulta>
```

- `representadoCuit` es **string** en el WSDL (no long).
- Con los perfiles contribuyente, `representadoCuit` "debe estar contenido en la lista relations del token" y, en `upload`, "debe coincidir con la CUIT del contribuyente informada en la DJ" (manual, 2.3.2).

**Respuestas reales** (homologación, 2026-10-02). Los errores del cliente vienen como SOAP Fault **con HTTP 200**; los del servidor, con HTTP 500. Todos llevan un `detail` propio:

| Caso | HTTP | `faultcode` | `faultstring` |
|---|---|---|---|
| `token` = `abc` | 200 | `soap:Client.contentError` | `Acceso denegado. El token y/o la firma es invalido` |
| `token` = TA bien formado en base64, `sign` = `abc` | 200 | `soap:Client.contentError` | `Error [java.security.SignatureException: Signature length not correct: got 2 but was expecting 128] verificando el sso usando un SignatureUtil obtenido con la key [accesscontrol] y con el certificado alias [wsaahomo]` |
| Namespace equivocado | 500 | `soap:Server.processError` | `Unexpected wrapper element {...}dummy found.   Expected {...}dummy.` |

Respuesta exacta del primer caso (`Content-Type: text/xml;charset=utf-8`, `Content-Length: 419`):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Client.contentError</faultcode><faultstring>Acceso denegado. El token y/o la firma es invalido</faultstring><detail><server>67</server><url>awshomo.afip.gov.ar</url><faultid>7e1e9bbe-be0b-4e30-8a9f-c1f526a0196b</faultid><datetime>2026-10-02 15:21:46</datetime></detail></soap:Fault></soap:Body></soap:Envelope>
```

`detail`: `server` (número de nodo; 67 en todas las pruebas), `url` (el host), `faultid` (UUID nuevo por error) y `datetime` (`yyyy-MM-dd HH:mm:ss`, hora local). El ejemplo viejo del manual solo trae `<server>1</server>`.

El caso 2 muestra que la firma (`sign`) se espera como un bloque de 128 bytes, es decir, RSA de 1024 bits del certificado `wsaahomo`. ArcaSim tiene que producir los mensajes con su propio tamaño de clave.

Headers de las respuestas: `Strict-Transport-Security: max-age=300; includeSubDomains; preload`, `X-XSS-Protection: 1; mode=block`, `X-Frame-Options: sameorigin`, `X-Content-Type-Options: nosniff`, `Set-Cookie: HttpOnly;Secure` (vacía), cookies F5 (`TS01a1c3b4`) y `Vary: Accept-Encoding`.

## Operaciones

3 operaciones. Las tres declaran el fault `Exception{message?}`, que los Fault reales **no** usan.

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Infraestructura, sin auth | `dom:dummy` vacío | `dummyResponse/return{appserver?, authserver?, dbserver?}` | — |
| `upload` | Presenta una DJ | `token`, `sign`, `representadoCuit`, `presentacion{presentacionDataHandler (base64Binary, MTOM, `application/octet-stream`), fileName}` | `uploadResponse/return` (long): número único de transacción | **Crea** una presentación. Clave: `numeroTransaccion`. **Idempotente**: "Si el ws detecta que la DJ ya fue presentada retorna el número único de transacción asignado a la presentación original" (2.4) |
| `consulta` | ¿Se procesó la DJ? | `token`, `sign`, `representadoCuit`, `fileName`, `formulario` (int), `contribuyenteCuit` (long), `md5` | `consultaResponse/return{contribuyenteCuit, fechaHoraPresentacion?, fileName, formulario, md5, numeroTransaccion?}` | Consulta. Clave: `contribuyenteCuit` + `fileName` + `formulario` + `md5`. Sin `numeroTransaccion` ni `fechaHoraPresentacion` si "aún no fue presentada" |

Detalles:

- `fileName`: formato definido por el documento de interfaz de cada formulario. Para archivos planos se recomienda gzip y nombre `FXXXX.<md5>.gz`, por ejemplo `F5862.eb618f3d0369b3ff2eb971e7811f0c0f.gz`. Otros ejemplos del manual: `F5129.ac7bbcdfcb3cb65a4977eaff593795d6.zip`, `727385F0181.811aa80332aa35923584f2b98ebe480e.b64`, `951616F0159.dat`.
- `presentacionDataHandler` debe ser una referencia MTOM (`xop:Include`), no el contenido inline. Con contenido inline grande el resultado es "413 Request Entity Too Large".
- `fechaHoraPresentacion`: `yyyy-mm-dd hh24:mi:ss` (ejemplo: `2014-05-29 11:02:15`).
- Ejemplo de respuesta de `upload`: `<return>62372464</return>`. Ejemplo de `consulta`: `numeroTransaccion` 6057682.
- Timeout recomendado: al menos 60 s para archivos de menos de 1 MB y 5 min para más grandes.

Respuesta real de `dummy` (homologación, 2026-10-02), HTTP 200, `Content-Length: 301`:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><ns2:dummyResponse xmlns:ns2="http://domain.presentacion.seti.osiris.afip.gov/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse></soap:Body></soap:Envelope>
```

## Errores

SOAP Fault con `faultcode` en el prefijo `soap:` y sufijo propio (manual, 2.5):

| `faultcode` | HTTP (en vivo) | Significado y ejemplos |
|---|---|---|
| `soap:Client.contentError` | 200 | El cliente debe corregir. Token o firma inválidos (verificado) |
| `soap:User.businessError` | NO VERIFICADO (probablemente 200) | Negocio: "CUIT contribuyente inválida", "El período fiscal no corresponde", "Archivo adjunto inválido", "413 Request Entity Too Large", "Archivo Inexistente". Ejemplo del manual: "El nombre del archivo [F05680.txt] no corresponde con ningún formato válido" |
| `soap:Server.processError` | 500 | Reintentar: "Error accediendo a base de datos"; también el wrapper equivocado (verificado) |

`consulta` sin coincidencia: Fault "de tipo CLIENT" con el texto `Archivo inexistente. Parámetros: contribuyenteCuit [<contribuyenteCuit>] fileName [<fileName>] formulario [<formulario>] md5 [<md5>]` (manual, 2.3.3). El `faultcode` exacto (`Client.contentError` o `User.businessError`) está **NO VERIFICADO**.

## Comportamiento a simular

- Aceptar MTOM (multipart/related con `xop:Include`) en `upload` y también base64 inline para archivos chicos; si el inline supera un tamaño configurable, responder el 413.
- Validar el TA contra los tres service ids; con `djprocessorcontribuyente*`, exigir `representadoCuit` en relations.
- Fault con `detail{server, url, faultid (UUID), datetime}`; HTTP 200 para `Client.*` y `User.*`, 500 para `Server.*`.
- Validar `fileName` contra un patrón por formulario (`F<nro>` y `md5` del archivo); calcular el MD5 del adjunto y compararlo con el del nombre.
- `upload` devuelve un `numeroTransaccion` secuencial; si el mismo archivo (CUIT + nombre + md5) ya se presentó, devuelve el mismo número.
- `consulta` busca por CUIT + `fileName` + `formulario` + `md5`. Par de estado: `upload` → `consulta` devuelve `numeroTransaccion` y `fechaHoraPresentacion`. Para simular "aún no presentada" (procesamiento diferido), ArcaSim puede demorar la asignación.
- Con el namespace del manual (`...arca.gov/`), responder el Fault `Unexpected wrapper element` con HTTP 500.

## No verificado

- El formato de `fileName` para cada formulario (está en documentos de interfaz de cada formulario, fuera de este manual).
- El HTTP de los `User.businessError` y el `faultcode` del "Archivo inexistente".
- Si el procesamiento es síncrono (el `upload` ya valida el contenido) o diferido.
- Qué formularios acepta el perfil `_cf`.
