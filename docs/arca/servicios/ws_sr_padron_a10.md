# ws_sr_padron_a10

Consulta a Padrón Alcance 10: recibe una CUIT y devuelve los datos "resumidos" del contribuyente, la "versión mínima" del padrón (identificación, dependencia, actividad principal y domicilios).

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/ws_sr_padron_a10/manual_ws_sr_padron_a10_v1.2.pdf`, versión 1.2 del 21/08/2024. Se descargó el 2026-10-02 (HTTP 200, `application/pdf`). "PDF p.N" es la página N del archivo, no el número impreso ("Pág."), que va 2 atrás.
- WSDL: `docs/arca/wsdl/ws_sr_padron_a10-homologacion.wsdl` (bajado el 2026-10-01). Esquema inline: **no hay `xsd:import`, `xsd:include` ni `wsdl:import`**, así que no hubo XSD para bajar.
- WSDL de producción pedido el 2026-10-02 a `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA10?WSDL`: es idéntico al de homologación salvo el `soap:address`.
- Llamadas reales sin credenciales contra homologación: 2026-10-02, alrededor de las 18:08 GMT.
- El catálogo no menciona datos de prueba propios de A10.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Padrón** (Apache CXF, JAX-WS, document/literal wrapped). Ver `catalogo.md` 4.1 | WSDL; faults observados |
| Endpoint homologación | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA10` (manual PDF p.5). **Ojo**: el `soap:address` del WSDL de homologación dice `http://awshomo.afip.gov.ar/...`, y el puerto 80 no responde (timeout de conexión probado el 2026-10-02). Pidiendo el WSDL por `awshomo.arca.gob.ar` el `soap:address` también sale con `http://`. Un cliente que use el `soap:address` tal cual falla: hay que forzar `https` | WSDL; prueba en vivo |
| Endpoint producción | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA10` (el WSDL de producción sí dice `https://`) | WSDL de producción; manual PDF p.5 |
| Hosts alternativos | `aws.arca.gob.ar` y `awshomo.arca.gob.ar` responden el WSDL con HTTP 200 (2026-10-02) | Prueba en vivo |
| Servicio / puerto | `PersonaServiceA10`, puerto `PersonaServiceA10Port`, binding `PersonaServiceA10SoapBinding` | WSDL |
| Namespace | `http://a10.soap.ws.server.puc.sr/` | WSDL |
| `elementFormDefault` | `unqualified`: los hijos del wrapper van sin namespace | WSDL |
| WSAA service id | `ws_sr_padron_a10` | Manual 2.4, PDF p.6 |
| SOAPAction | `soapAction=""` en las dos operaciones | WSDL |
| SOAP | Solo **SOAP 1.1**. En A4, del mismo stack, un sobre 1.2 recibe `VersionMismatch`; en A10 no se probó | WSDL |
| Content-Type de respuesta | `text/xml;charset=UTF-8`, sin `<?xml?>`, sin `soap:Header`, una sola línea | Prueba en vivo |

Operaciones del `portType` `PersonaServiceA10`: **2** (`getPersona`, `dummy`). `getPersona` declara el fault `SRValidationException`.

## Autenticación

- `token`, `sign` y `cuitRepresentada` van sueltos dentro del wrapper `getPersona`, sin `<Auth>` ni header SOAP. Salen del TA de WSAA para el service id `ws_sr_padron_a10` (manual 2.2 y 2.4, PDF p.5-6). El formato del token está en `wsaa.md` 4.3.
- `cuitRepresentada`: "Debe coincidir con alguna de las CUITS listadas en la sección relations del token enviado. Debe ser en representación de que organismo se solicita la operación" (manual 3.2.1, PDF p.8).
- `dummy` no requiere token.
- Las fallas de autenticación son **HTTP 500** con `soap:Fault`, `faultcode` `soap:Server`, y se chequean antes que el resto de los datos.

Respuestas observadas en homologación el 2026-10-02 (cuerpo crudo completo):

| Caso enviado | HTTP | Cuerpo |
|---|---|---|
| Sin elementos `token` ni `sign` | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Falta token y/o sign.</faultstring><detail><ns1:SRValidationException xmlns:ns1="http://a10.soap.ws.server.puc.sr/"/></detail></soap:Fault></soap:Body></soap:Envelope>` |
| `token`=`abc`, o `token` y `sign` vacíos | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Token malformado</faultstring></soap:Fault></soap:Body></soap:Envelope>` |
| `token` = Base64 de un XML `sso` 2.0 bien formado; `sign` falso | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token></faultstring></soap:Fault></soap:Body></soap:Envelope>` |

Headers de las respuestas 500: `Content-Type: text/xml;charset=UTF-8`, `Connection: close`, más headers de seguridad y cookies del balanceador.

## Operaciones

Orden de elementos: el esquema del WSDL declara los tipos de respuesta en **orden alfabético**, y el ejemplo del manual sale igual. El manual describe los tipos en otro orden (PDF p.12-13). **Hay que seguir el WSDL.**

### dummy

Propósito: verifica la disponibilidad de la aplicación, la autenticación y la base de datos (manual 3.1, PDF p.6). Sin credenciales.

Request: `<a10:dummy/>` (secuencia vacía). Response: `dummyResponse` → `return` (`dummyReturn`) con `appserver`, `authserver` y `dbserver` (xs:string, 0..1, `OK` o `ERROR`).

Ejemplo del manual (PDF p.7), request:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a10="http://a10.soap.ws.server.puc.sr/">
  <soapenv:Header/>
  <soapenv:Body>
     <a10:dummy/>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta real observada el 2026-10-02 (HTTP 200, 286 bytes):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><ns2:dummyResponse xmlns:ns2="http://a10.soap.ws.server.puc.sr/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse></soap:Body></soap:Envelope>
```

### getPersona

Propósito: "Devuelve el detalle de todos los datos, existentes en el padrón único de contribuyentes, del contribuyente solicitado" (manual 3.2, PDF p.7). En la práctica es un subconjunto de A4.

Request (`getPersona`; sin `minOccurs`, todos 1..1):

| Elemento | Tipo WSDL | Occurs | Significado y límites |
|---|---|---|---|
| `token` | xs:string | 1..1 | Token del TA |
| `sign` | xs:string | 1..1 | Firma del TA |
| `cuitRepresentada` | xs:long | 1..1 | CUIT representada; debe estar en `relations`. Manual: CUIT, long de 11 dígitos (PDF p.11-12) |
| `idPersona` | xs:long | 1..1 | CUIT de la que se piden los datos |

Response: `getPersonaResponse` → `personaReturn` (0..1) → `metadata` (0..1: `fechaHora` xs:dateTime, `servidor` xs:string) y `persona` (0..1).

`persona`, en el orden del WSDL. "Manual" es la tabla de PDF p.12:

| Elemento | Tipo WSDL | Occurs | Tipo manual | Significado |
|---|---|---|---|---|
| `apellido` | xs:string | 0..1 | String | Apellido |
| `claveInactivaAsociada` | xs:long, nillable | 0..* | CUIT | Clave inactiva asociada |
| `dependencia` | tns:dependencia | 0..1 | Dependencia | Dependencia |
| `descripcionActividadPrincipal` | xs:string | 0..1 | String (1..1 en el manual) | Descripción de la actividad principal según SUPA |
| `domicilio` | tns:domicilio, nillable | 0..* | Domicilio | Domicilios |
| `estadoClave` | xs:string | 0..1 | EstadoClave | `ACTIVO`, `INACTIVO` |
| `idActividadPrincipal` | xs:long | 0..1 | Long (1..1 en el manual) | Id de la actividad principal según SUPA |
| `idPersona` | xs:long | 0..1 | CUIT | CUIT consultada |
| `nombre` | xs:string | 0..1 | String | Nombre |
| `numeroDocumento` | xs:string | 0..1 | String | Número de documento |
| `razonSocial` | xs:string | 0..1 | String | Razón social |
| `tipoClave` | xs:string | 0..1 | TipoClave | `CUIT`, `CUIL`, `CDI` |
| `tipoDocumento` | xs:string | 0..1 | TipoDocumento | Tabla 5.1 |
| `tipoPersona` | xs:string | 0..1 | TipoPersona | `FISICA`, `JURIDICA` (el manual escribe "FÍSICA o JURÍDICA" con tilde; el ejemplo y A4 usan `FISICA`) |

El manual lista además un campo `actividad` (0..1, tipo Actividad: `descripcionActividad`, `idActividad`, `nomenclador`, `orden` "1-Principal", `periodo` AAAAMM; PDF p.12-13) que **no existe en el WSDL**. La v1.2 del manual dice "Corrección sobre tipo de dato actividad" (PDF p.2). El servicio real expone la actividad principal aplanada en `idActividadPrincipal` y `descripcionActividadPrincipal`; no hay `periodo` (A13 sí tiene `periodoActividadPrincipal`).

Subtipos (orden del WSDL, todos `minOccurs="0"`; el manual los marca 1..1 salvo los adicionales):

- `dependencia`: `descripcionDependencia` (xs:string), `idDependencia` (xs:int). A diferencia de A5, sin dirección.
- `domicilio`: `codPostal` (xs:string), `datoAdicional` (xs:string, 0..1 en el manual), `descripcionProvincia` (xs:string), `direccion` (xs:string), `idProvincia` (xs:int), `localidad` (xs:string), `tipoDatoAdicional` (xs:string, tabla 5.2, 0..1 en el manual), `tipoDomicilio` (xs:string: `FISCAL`, `LEGAL/REAL`). **No tiene `orden`** (A4 sí) y el manual de A10 no menciona `LOCALES Y ESTABLECIMIENTOS`.

Ejemplo del manual, request (PDF p.9; token y sign unidos en una línea, el PDF los corta):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a10="http://a10.soap.ws.server.puc.sr/">
  <soapenv:Header/>
  <soapenv:Body>
     <a10:getPersona>
<token>ICAgIDwvcmVsYXRpb25zPgogICAgICAgIDwvbG9naW4+CiAgICA8L29wZXJhdGlvbj4KPC9zc28+Cgo=</token>
<sign>+7rFJNrEcIKRQ+A2xx0m9B9hlVXzU/XHvZEEY7XsvRMIDPRiSlsLR8+MBYHTcfsO4=</sign>
       <cuitRepresentada>20135464605</cuitRepresentada>
       <idPersona>20000000516</idPersona>
     </a10:getPersona>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta del ejemplo (PDF p.9-10):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <ns2:getPersonaResponse xmlns:ns2="http://a10.soap.ws.server.puc.sr/">
       <personaReturn>
          <metadata>
            <fechaHora>2016-12-07T11:21:59.978-03:00</fechaHora>
            <servidor>setiwsh2</servidor>
          </metadata>
          <persona>
            <apellido>ERNESTO DANIEL</apellido>
            <dependencia>
               <descripcionDependencia>DISTRITO ZAPALA</descripcionDependencia>
               <idDependencia>702</idDependencia>
            </dependencia>
            <descripcionActividadPrincipal>CONSTRUCCIÓN, REFORMA Y REPARACIÓN DE EDIFICIOS RESIDENCIALES</descripcionActividadPrincipal>
            <domicilio>
               <codPostal>8371</codPostal>
               <datoAdicional>N/A PATRICIA ANDREA</datoAdicional>
               <descripcionProvincia>NEUQUEN</descripcionProvincia>
               <direccion>LAGUNA LOS SAUCES 2</direccion>
               <idProvincia>20</idProvincia>
               <localidad>JUNIN DE LOS ANDES</localidad>
               <tipoDatoAdicional>NO DETERMINADO</tipoDatoAdicional>
               <tipoDomicilio>FISCAL</tipoDomicilio>
            </domicilio>
            <domicilio>
               <codPostal>1425</codPostal>
               <descripcionProvincia>CIUDAD AUTONOMA BUENOS AIRES</descripcionProvincia>
               <direccion>ARAOZ 1901</direccion>
               <idProvincia>0</idProvincia>
               <tipoDomicilio>LEGAL/REAL</tipoDomicilio>
            </domicilio>
            <estadoClave>ACTIVO</estadoClave>
            <idActividadPrincipal>410011</idActividadPrincipal>
            <idPersona>20000000516</idPersona>
            <nombre>MARCELO NICOLAS</nombre>
            <numeroDocumento>51</numeroDocumento>
            <tipoClave>CUIT</tipoClave>
            <tipoPersona>FISICA</tipoPersona>
          </persona>
       </personaReturn>
     </ns2:getPersonaResponse>
  </soap:Body>
</soap:Envelope>
```

Es la misma persona que el ejemplo de A4 (CUIT 20000000516). A10 devuelve los dos domicilios FISCAL y LEGAL/REAL pero no los dos LOCALES Y ESTABLECIMIENTOS que A4 sí muestra. Esto sugiere que A10 filtra por tipo de domicilio; como regla está **NO VERIFICADO**.

## Validaciones y errores

El manual de A10 **no trae tabla de errores**.

| Código / texto | Condición | Dónde aparece | Fuente |
|---|---|---|---|
| `Falta token y/o sign.` | Falta el elemento `token` o `sign` | `soap:Fault` `soap:Server` con `<detail><ns1:SRValidationException xmlns:ns1="http://a10.soap.ws.server.puc.sr/"/></detail>`; HTTP 500 | En vivo 2026-10-02 |
| `Token malformado` | Token vacío o que no decodifica a un `sso` válido | `soap:Fault` `soap:Server` sin `detail`; HTTP 500 | En vivo 2026-10-02 |
| `No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token>` | Firma inválida | `soap:Fault` `soap:Server` sin `detail`; HTTP 500 | En vivo 2026-10-02 |
| `Unmarshalling Error: ...`, `Message part ... was not recognized.  (Does it exist in service WSDL?)`, `Unexpected wrapper element ...`, `Error reading XMLStreamReader.`, `VersionMismatch` | Errores de la capa SOAP de CXF | `soap:Fault` `soap:Client` (o `VersionMismatch`); HTTP 500. Se observaron en A4, mismo stack; los textos exactos están en `ws_sr_padron_a4.md` | En vivo en A4; en A10, inferencia |
| `El Id de la persona no es valido`, `La Clave (CUIT/CUIL) consultada es inexistente`, `La clave (CUIT/CUIL) consultada se encuentra INACTIVA`, `Debe enviar la CUIT representada`, `Este token no le permite actuar en representacion de la CUIT ` + cuitRepresentada, `No autorizado, par token/sign invalido.` | Mismas condiciones que en A13 | NO VERIFICADO en A10; textos del manual de A13 (PDF p.24) | Inferencia |

## Tablas y datos

Tipos simples (manual 4.1, PDF p.11): CUIT long de 11 dígitos; TipoPersona `FISICA`/`JURIDICA`; TipoClave `CUIT`, `CUIL`, `CDI`; EstadoClave `ACTIVO`, `INACTIVO`; TipoDocumento tabla 5.1; TipoDomicilio `FISCAL`, `LEGAL/REAL`; TipoDatoAdicional tabla 5.2.

5.1 Valores de Tipo Documento (PDF p.14):

| tipoDocumento | Descripción |
|---|---|
| LC | LIBRETA CIVICA |
| LE | LIBRETA DE ENROLAMIENTO |
| CI | CEDULA DE IDENTIDAD |
| TRAM | EN TRAMITE |
| ACTA | ACTA DE NACIMIENTO |
| PAS | PASAPORTE |
| DNI | DOC.NACIONAL DE IDENTIDAD |
| INDET | INDETERMINADO |
| CERT | CERTIFICADO DE MIGRACIÓN |
| DIEXT | DOCUMENTO IDENTIDAD EXTRANJERO |
| DNI M | D.N.I. (N° MÚLTIPLE) |
| INDOC | ANSES INDOCUMENTADO |

5.2 Valores Tipo Dato Adicional (PDF p.14): BARRIO, PARAJE, NO DETERMINADO, ESTAFETA, ENTRE LAS CALLES:, ESQUINA, SITIO WEB.

Provincias, actividades y dependencias salen de SUPA (`ws_sr_padron_a100`). Del ejemplo: provincia 20 NEUQUEN, 0 CIUDAD AUTONOMA BUENOS AIRES; dependencia 702 DISTRITO ZAPALA; actividad 410011.

Datos de prueba: el manual no trae. Las 30 claves de `datos-prueba-padron-a4.txt` (ver `ws_sr_padron_a4.md`) son del mismo padrón de homologación; que respondan en A10 está **NO VERIFICADO**.

## Comportamiento a simular

- Sin estado; lectura pura del padrón ficticio compartido con A4, A5 y A13. Para la misma CUIT, A10 tiene que ser una proyección coherente de A4: mismo nombre, dependencia y domicilios FISCAL y LEGAL/REAL, y actividad principal = la `actividad` con `orden` 1 de A4.
- Orden de chequeos: igual que A4 (unmarshalling, presencia de token/sign, decodificación, firma y después datos).
- Serialización: orden alfabético, campos vacíos omitidos, `domicilio` sin `orden`.
- Para el `soap:address`, el simulador puede devolver `https://` (lo correcto) o imitar el `http://` de homologación. Si lo imita, un cliente que siga el WSDL va a fallar igual que contra ARCA; conviene que sea configurable.

## No verificado

- Errores de negocio (CUIT inexistente, inactiva, mal formada) y su transporte.
- Si A10 filtra domicilios (no devuelve LOCALES Y ESTABLECIMIENTOS) o si es casualidad del ejemplo.
- Si `getPersona` de A10 responde para claves INACTIVAS o falla.
- Textos de falla de TA vencido, TA de otro service id y `cuitRepresentada` fuera de `relations`.
- Si los datos de prueba de A4 sirven en A10.
- Respuesta a SOAP 1.2 en A10 (se probó solo en A4).
