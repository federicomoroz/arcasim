# ws_sr_padron_a13

Consulta a Padrón Alcance 13: devuelve los datos de identificación de una persona física o jurídica y sus domicilios fiscal y legal a partir de la CUIT/CUIL. Además, a partir de un número de documento, devuelve las CUIT/CUIL asociadas.

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/ws-padron-a13/manual-ws-sr-padron-a13-v1.4.pdf`, versión 1.4 del 14/08/2026. Se descargó el 2026-10-02 (HTTP 200, `application/pdf`). "PDF p.N" es la página del archivo; en este manual coincide con el número impreso ("Pág. N de 25").
- WSDL: `docs/arca/wsdl/ws_sr_padron_a13-homologacion.wsdl` (bajado el 2026-10-01). Esquema inline: **no hay `xsd:import`, `xsd:include` ni `wsdl:import`**, así que no hubo XSD para bajar.
- WSDL de producción pedido el 2026-10-02 a `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA13?WSDL`: es idéntico al de homologación salvo el host. `aws.arca.gob.ar` y `awshomo.arca.gob.ar` también responden HTTP 200.
- Resumen previo: `catalogo.md` 5.2. Pruebas en vivo del 2026-10-01 (catálogo 4.1) y del 2026-10-02 (acá).

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Padrón** (Apache CXF, JAX-WS, document/literal wrapped) | WSDL; faults observados |
| Endpoint homologación | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA13` | `soap:address`; manual 2.3, PDF p.5 |
| Endpoint producción | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA13` | `soap:address` de producción; manual PDF p.5 |
| Servicio / puerto | `PersonaServiceA13`, puerto `PersonaServiceA13Port`, binding `PersonaServiceA13SoapBinding` | WSDL |
| Namespace | `http://a13.soap.ws.server.puc.sr/` | WSDL |
| `elementFormDefault` | `unqualified` | WSDL |
| WSAA service id | `ws_sr_padron_a13` | Manual 2.4, PDF p.6 |
| SOAPAction | `soapAction=""` | WSDL |
| SOAP | Solo SOAP 1.1 | WSDL |
| Content-Type de respuesta | `text/xml;charset=UTF-8`, sin `<?xml?>`, sin `soap:Header` | En vivo |
| Soporte | `sri@arca.gob.ar` o "Consultas Web"; para LoginCMS y certificados, `webservices-desa@arca.gob.ar` (PDF p.5) | Manual |

Operaciones del `portType` `PersonaServiceA13`: **4** (`dummy`, `getIdPersonaListByDocumento`, `getPersona`, `getPersonaV2`). Todas menos `dummy` declaran el fault `SRValidationException`.

## Autenticación

- `token`, `sign` y `cuitRepresentada` van sueltos en el wrapper. TA de WSAA para `ws_sr_padron_a13` (PDF p.5-6).
- `cuitRepresentada`: "Debe coincidir con alguna de las CUITS listadas en la sección relations del token enviado" (PDF p.9, p.13 y p.14).
- `dummy` no requiere token. El manual dice que verifica "aplicación y base de datos" (PDF p.6), pero devuelve igual los tres campos, `authserver` incluido.
- Fallas: **HTTP 500**, `soap:Fault`, `faultcode` `soap:Server`. Se chequean antes que el `idPersona`: con token malo e `idPersona` de 12 dígitos sale "Token malformado", no "El Id de la persona no es valido".

Respuestas observadas en homologación (cuerpo crudo completo):

| Fecha | Caso enviado | HTTP | Cuerpo |
|---|---|---|---|
| 2026-10-02 | `getPersonaV2` o `getIdPersonaListByDocumento` sin elementos `token` ni `sign` | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Falta token y/o sign.</faultstring><detail><ns1:SRValidationException xmlns:ns1="http://a13.soap.ws.server.puc.sr/"/></detail></soap:Fault></soap:Body></soap:Envelope>` |
| 2026-10-01 / 2026-10-02 | `getPersona` (con `idPersona` de 12 dígitos) o `getIdPersonaListByDocumento`, con `token`=`abc` | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Token malformado</faultstring></soap:Fault></soap:Body></soap:Envelope>` |
| — | Token XML válido con firma falsa | — | No se probó en A13. En A4, A5, A10 y A100 da "No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token>". El manual de A13 en cambio lista "No autorizado, par token/sign invalido." para "La firma del token en el login es invalida" (ver Validaciones) |

## Operaciones

Orden de elementos: el esquema del WSDL es **alfabético** en las respuestas y los ejemplos del manual salen así (`idPersonaListReturn`: `idPersona`... y después `metadata`; `personaReturn`: `metadata` y después `persona`). El manual lista los campos en otro orden (PDF p.21-22): **seguir el WSDL**.

### dummy

Request `<a13:dummy/>`. Response: `dummyResponse` → `return` → `appserver`, `authserver`, `dbserver` (xs:string 0..1, `OK`/`ERROR`).

Ejemplo del manual (PDF p.7), request:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a13="http://a13.soap.ws.server.puc.sr/">
  <soapenv:Header/>
  <soapenv:Body>
     <a13:dummy/>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta real (2026-10-01, catálogo 4.1), HTTP 200: `<ns2:dummyResponse xmlns:ns2="http://a13.soap.ws.server.puc.sr/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse>`.

### getPersona

Propósito: "Devuelve el detalle de todos los datos, existentes en el padrón único de contribuyentes, del contribuyente solicitado" (PDF p.8). Solo para claves activas: si la clave está inactiva, el manual lista el error "La clave (CUIT/CUIL) consultada se encuentra INACTIVA" (PDF p.24), y para ese caso existe `getPersonaV2`.

Request (`getPersona`; todos 1..1):

| Elemento | Tipo | Occurs | Significado y límites |
|---|---|---|---|
| `token` | xs:string | 1..1 | Token |
| `sign` | xs:string | 1..1 | Firma |
| `cuitRepresentada` | xs:long | 1..1 | CUIT representada; no puede ser nula y debe estar en `relations` |
| `idPersona` | xs:long | 1..1 | CUIT/CUIL consultada. Con más de 11 dígitos → "El Id de la persona no es valido" |

Response: `getPersonaResponse` → `personaReturn` (0..1) → `metadata` (0..1: `fechaHora`, `servidor`) y `persona` (0..1).

`persona`, en orden del WSDL. Las descripciones y tipos del manual son de PDF p.21-22:

| Elemento | Tipo WSDL | Occurs | Tipo manual | Significado |
|---|---|---|---|---|
| `apellido` | xs:string | 0..1 | String | Apellido |
| `claveInactivaAsociada` | xs:long, nillable | 0..* | Cuit | Claves inactivas asociadas |
| `descripcionActividadPrincipal` | xs:string | 0..1 | String | Actividad principal declarada |
| `domicilio` | tns:domicilio, nillable | 0..* | Domicilio | Domicilios (FISCAL y LEGAL/REAL) |
| `estadoClave` | xs:string | 0..1 | EstadoClave | `ACTIVO`, `INACTIVO` |
| `fechaContratoSocial` | xs:dateTime | 0..1 | DateTime | Fecha del contrato social |
| `fechaFallecimiento` | xs:dateTime | 0..1 | DateTime | Fecha de fallecimiento |
| `fechaNacimiento` | xs:dateTime | 0..1 | DateTime | Fecha de nacimiento |
| `formaJuridica` | xs:string | 0..1 | TipoFormaJuridica | Descripción de la forma jurídica (para personas jurídicas): es el `DESC_TIPO_EMPRESA_JURIDICA` de `SUPA.TIPO_EMPRESA_JURIDICA` (PDF p.24). El ejemplo de getPersonaV2 la trae en una persona FISICA ("FIDEICOMISO TESTAMENTARIO") |
| `idActividadPrincipal` | xs:long | 0..1 | Long | Id de la actividad principal según SUPA |
| `idPersona` | xs:long | 0..1 | CUIT | CUIT/CUIL |
| `mesCierre` | xs:int | 0..1 | Integer | Mes de cierre |
| `nombre` | xs:string | 0..1 | String | Nombre |
| `numeroDocumento` | xs:string | 0..1 | String | Número de documento |
| `periodoActividadPrincipal` | xs:int | 0..1 | YYYYMM | Período de la actividad principal |
| `razonSocial` | xs:string | 0..1 | String | Razón social |
| `tipoClave` | xs:string | 0..1 | TipoClave | `CUIT`, `CUIL`, `CDI` |
| `tipoDocumento` | xs:string | 0..1 | TipoDocumento | Tabla 5.1 |
| `tipoPersona` | xs:string | 0..1 | TipoPersona | `FISICA`, `JURIDICA` |

El manual lista también `fechaInscripcion` (0..1, DateTime; PDF p.21), que **no está en el WSDL**.

`domicilio`, en orden del WSDL. **El WSDL tiene bastantes más campos que el manual** (PDF p.22):

| Elemento | Tipo WSDL | En el manual | Significado |
|---|---|---|---|
| `calle` | xs:string | no (sí en los ejemplos) | Calle |
| `codigoPostal` | xs:string | figura como `codPostal` | Código postal. **El nombre real es `codigoPostal`** (WSDL y ejemplos); en A4, A5 y A10 se llama `codPostal` |
| `datoAdicional` | xs:string | sí, 0..1 | Dato adicional |
| `descripcionProvincia` | xs:string | sí, 1..1 | Provincia según SUPA |
| `direccion` | xs:string | sí, 1..1 | "Calle, numero, depto, etc." (en los ejemplos: calle + número) |
| `estadoDomicilio` | xs:string | no (sí en los ejemplos) | Valores vistos: `CONFIRMADO`, `NO DENUNCIADO`, `DECLARADO` |
| `idProvincia` | xs:int | sí, 1..1 | Provincia según SUPA |
| `localidad` | xs:string | sí, 1..1 | Localidad |
| `manzana` | xs:string | no | — |
| `numero` | xs:int | no (sí en los ejemplos) | Número de puerta |
| `oficinaDptoLocal` | xs:string | no | — |
| `piso` | xs:string | no | — |
| `sector` | xs:string | no | — |
| `tipoDatoAdicional` | xs:string | sí, 0..1 | `DESC_TIPO_DATO_ADICIONAL_DOM` de `SUPA.TIPO_DATO_ADICIONAL_DOMICILIO` |
| `tipoDomicilio` | xs:string | sí, 1..1 | `FISCAL`, `LEGAL/REAL` |
| `torre` | xs:string | no | — |

Ejemplo del manual, request (PDF p.10):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a13="http://a13.soap.ws.server.puc.sr/">
  <soapenv:Header/>
  <soapenv:Body>
     <a13:getPersona>
       <token>PDbmNvZGluZz0iVVRGLTgiIHN0YW5kYWxvbmU9InllcyI/Pgoo=</token>
       <sign>aoknbFR2TB9cPBLBcPpWpA7BHO2s9tbn8=</sign>
       <cuitRepresentada>20140453605</cuitRepresentada>
       <idPersona>27015942210</idPersona>
     </a13:getPersona>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta del ejemplo (PDF p.10-12, completa):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <ns2:getPersonaResponse xmlns:ns2="http://a13.soap.ws.server.puc.sr/">
       <personaReturn>
          <metadata>
            <fechaHora>2018-11-28T14:55:19.023-03:00</fechaHora>
            <servidor>127.0.0.1</servidor>
          </metadata>
          <persona>
            <apellido>INTENTAR</apellido>
            <descripcionActividadPrincipal>SERVICIOS DE ALOJAMIENTO EN HOTELES, HOSTERÍAS Y RESIDENCIALES SIMILARES, EXCEPTO POR HORA, QUE NO INCLUYEN SERVICIO DE RESTAURANTE AL PÚBLICO</descripcionActividadPrincipal>
             <domicilio>
               <calle>AV LOS INCAS</calle>
               <codigoPostal>5881</codigoPostal>
               <descripcionProvincia>SAN LUIS</descripcionProvincia>
               <direccion>AV LOS INCAS 4137</direccion>
               <estadoDomicilio>CONFIRMADO</estadoDomicilio>
               <idProvincia>11</idProvincia>
               <localidad>MERLO</localidad>
               <numero>4137</numero>
               <tipoDomicilio>FISCAL</tipoDomicilio>
             </domicilio>
             <domicilio>
               <calle>AV LOS INCAS</calle>
               <codigoPostal>5881</codigoPostal>
               <descripcionProvincia>SAN LUIS</descripcionProvincia>
               <direccion>AV LOS INCAS 8732</direccion>
               <estadoDomicilio>NO DENUNCIADO</estadoDomicilio>
               <idProvincia>11</idProvincia>
               <localidad>MERLO</localidad>
               <numero>8732</numero>
               <tipoDomicilio>LEGAL/REAL</tipoDomicilio>
             </domicilio>
             <estadoClave>ACTIVO</estadoClave>
             <fechaNacimiento>1936-02-11T12:00:00-03:00</fechaNacimiento>
             <idActividadPrincipal>551023</idActividadPrincipal>
             <idPersona>27015942210</idPersona>
             <mesCierre>12</mesCierre>
             <nombre>JAZMIN</nombre>
             <numeroDocumento>1594221</numeroDocumento>
             <periodoActividadPrincipal>201311</periodoActividadPrincipal>
            <tipoClave>CUIT</tipoClave>
            <tipoDocumento>LC</tipoDocumento>
            <tipoPersona>FISICA</tipoPersona>
          </persona>
       </personaReturn>
     </ns2:getPersonaResponse>
  </soap:Body>
</soap:Envelope>
```

### getIdPersonaListByDocumento

Propósito: "Devuelve la lista de claves asociadas al número de documento" (PDF p.12). Sirve "para encadenar posteriormente la consulta de datos de la persona" (PDF p.4).

Request (`getIdPersonaListByDocumento`; todos 1..1):

| Elemento | Tipo | Occurs | Significado y límites |
|---|---|---|---|
| `token` | xs:string | 1..1 | Token |
| `sign` | xs:string | 1..1 | Firma |
| `cuitRepresentada` | xs:long | 1..1 | CUIT representada |
| `documento` | xs:**string** | 1..1 | "Es el documento de la clave que se solicitan los datos" (PDF p.13). No se pasa el tipo de documento. Longitud y formato aceptados: NO VERIFICADO |

Response: `getIdPersonaListByDocumentoResponse` → `idPersonaListReturn` (0..1):

| Elemento | Tipo | Occurs | Significado |
|---|---|---|---|
| `idPersona` | xs:long, nillable | 0..* | Cada CUIT/CUIL asociada al documento |
| `metadata` | tns:metadata | 0..1 | Va **después** de los `idPersona` (alfabético) |

Ejemplo del manual (PDF p.12-13). El manual da el request como "Esquema", con placeholders:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:a13="http://a13.soap.ws.server.puc.sr/">
<soapenv:Header/>
<soapenv:Body>
<a13:getIdPersonaListByDocumento>
<token>${tokensign#token}</token>
<sign>${tokensign#sign}</sign>
<cuitRepresentada>${parametros#cuit_representada}</cuitRepresentada>
<documento>11709676</documento>
</a13:getIdPersonaListByDocumento>
</soapenv:Body>
</soapenv:Envelope>
```

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
<soap:Body>
<ns2:getIdPersonaListByDocumentoResponse xmlns:ns2="http://a13.soap.ws.server.puc.sr/">
<idPersonaListReturn>
<idPersona>23117096769</idPersona>
<idPersona>27117096764</idPersona>
<metadata>
<fechaHora>2025-07-17T16:22:09.168-03:00</fechaHora>
<servidor>qa-sr-padron-ws.cloudhomo.afip.gob.ar</servidor>
</metadata>
</idPersonaListReturn>
</ns2:getIdPersonaListByDocumentoResponse>
</soap:Body>
</soap:Envelope>
```

Un mismo DNI puede tener varias claves (en el ejemplo, prefijos 23 y 27 con el DNI 11709676 adentro). Qué devuelve si no hay ninguna (lista vacía o Fault) y si incluye claves inactivas: NO VERIFICADO.

### getPersonaV2

Propósito: "La versión getPersonaV2 extiende la funcionalidad de la versión base, permitiendo consultar la información aún cuando la clave se encuentre en estado INACTIVA" (PDF p.14; agregada en la v1.4 del 14/08/2026).

Request: igual que `getPersona` (`token`, `sign`, `cuitRepresentada`, `idPersona`, todos 1..1). Response: `getPersonaV2Response` → `personaReturn`, **el mismo tipo** que `getPersona`.

Ejemplo del manual, request (PDF p.16):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a13="http://a13.soap.ws.server.puc.sr/">
  <soapenv:Header/>
  <soapenv:Body>
     <a13:getPersonaV2>
          <token>PDbmNvZGluZz0iVVRGLTgiIHN0YW5kYWxvbmU9InllcyI/Pgoo=</token>
       <sign>aoknbFR2TB9cPBLBcPpWpA7BHO2s9tbn8=</sign>
       <cuitRepresentada>20140453605</cuitRepresentada>
       <idPersona>27015942210</idPersona>
     </a13:getPersonaV2>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta del ejemplo (PDF p.16-18, completa). Ojo: pide 27015942210 y responde 20002444233, otra persona:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <ns2:getPersonaV2Response xmlns:ns2="http://a13.soap.ws.server.puc.sr/">
       <personaReturn>
          <metadata>
            <fechaHora>2026-08-14T11:52:48.525-03:00</fechaHora>
            <servidor>127.0.0.1</servidor>
          </metadata>
          <persona>
            <apellido>GEOFFREY WILLIAM</apellido>
            <descripcionActividadPrincipal>FABRICACIÓN DE PARTES, PIEZAS Y ACCESORIOS PARA VEHÍCULOS AUTOMOTORES Y SUS MOTORES N.C.P.</descripcionActividadPrincipal>
            <domicilio>
               <calle>SAN RAMON</calle>
               <codigoPostal>4600</codigoPostal>
               <descripcionProvincia>JUJUY</descripcionProvincia>
               <direccion>SAN RAMON 415</direccion>
               <estadoDomicilio>DECLARADO</estadoDomicilio>
               <idProvincia>6</idProvincia>
               <localidad>CHIJRA</localidad>
               <numero>415</numero>
               <tipoDomicilio>FISCAL</tipoDomicilio>
             </domicilio>
             <domicilio>
               <calle>SAN RAMON</calle>
               <codigoPostal>4600</codigoPostal>
               <descripcionProvincia>JUJUY</descripcionProvincia>
               <direccion>SAN RAMON 415</direccion>
               <estadoDomicilio>DECLARADO</estadoDomicilio>
               <idProvincia>6</idProvincia>
               <localidad>CHIJRA</localidad>
               <numero>415</numero>
               <tipoDomicilio>LEGAL/REAL</tipoDomicilio>
             </domicilio>
             <estadoClave>INACTIVO</estadoClave>
             <fechaNacimiento>1916-01-31T12:43:12-04:16</fechaNacimiento>
             <formaJuridica>FIDEICOMISO TESTAMENTARIO</formaJuridica>
             <idActividadPrincipal>293090</idActividadPrincipal>
             <idPersona>20002444233</idPersona>
             <mesCierre>12</mesCierre>
             <nombre>PHILLIP</nombre>
             <numeroDocumento>265181</numeroDocumento>
             <periodoActividadPrincipal>201412</periodoActividadPrincipal>
            <tipoClave>CUIT</tipoClave>
            <tipoDocumento>DNI</tipoDocumento>
            <tipoPersona>FISICA</tipoPersona>
          </persona>
       </personaReturn>
     </ns2:getPersonaV2Response>
  </soap:Body>
</soap:Envelope>
```

Detalle de serialización que muestra el ejemplo: `1916-01-31T12:43:12-04:16` es una fecha guardada a las 12:00 en `-03:00` y convertida a la zona histórica de Buenos Aires de 1916 (offset de -4:16:48, que se trunca a minutos al serializar). Es un efecto de `java.util.TimeZone` "America/Buenos_Aires". Un simulador muy fiel lo reproduciría con la base de zonas horarias (tzdb) para fechas viejas. Inferencia **NO VERIFICADA**: el ejemplo de A4 muestra 1891 con `-03:00`, que no encaja con la misma regla.

## Validaciones y errores

Tabla 5.3 del manual (PDF p.24, completa). **El manual no dice el transporte.** Las cuatro de autenticación se vieron como Fault HTTP 500. Para las de negocio, que sean Fault `soap:Server` con `detail/SRValidationException` (porque las tres operaciones declaran ese fault y A13 no tiene campos de error en la respuesta) es inferencia NO VERIFICADA.

| Mensaje (texto exacto) | Condición (manual) | Dónde aparece | Fuente |
|---|---|---|---|
| `El Id de la persona no es valido` | La clave (cuit/cuil) tiene más de 11 dígitos | Fault (inferido) | PDF p.24 |
| `La Clave (CUIT/CUIL) consultada es inexistente` | La clave no está en la base | Fault (inferido) | PDF p.24 |
| `La clave (CUIT/CUIL) consultada se encuentra INACTIVA` | La clave está inactiva (en `getPersona`; `getPersonaV2` no falla) | Fault (inferido) | PDF p.24 y p.14 |
| `No autorizado, par token/sign invalido.` | La firma del token en el login es inválida | Fault (inferido). En vivo, en los otros servicios del padrón, una firma falsa dio "No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token>"; a qué caso real corresponde este texto está NO VERIFICADO | PDF p.24 |
| `Debe enviar la CUIT representada` | `cuitRepresentada` nula | Fault (inferido) | PDF p.24 |
| `Este token no le permite actuar en representacion de la CUIT ` + cuitRepresentada | `cuitRepresentada` no está en `relations` del token | Fault (inferido) | PDF p.24 |
| `Falta token y/o sign.` | No se enviaron el token o la firma | `soap:Fault` `soap:Server` + `detail/ns1:SRValidationException`, HTTP 500 | PDF p.24; en vivo 2026-10-02 |
| `Token malformado` | Token no decodificable | `soap:Fault` `soap:Server` sin `detail`, HTTP 500 | En vivo 2026-10-01 y 2026-10-02 (no está en el manual) |
| `Unmarshalling Error: ...`, etc. | XML inválido, `idPersona` no numérico | `soap:Fault` `soap:Client`, HTTP 500 | Observado en A4; inferencia |

Sobre "Debe enviar la CUIT representada": `cuitRepresentada` es obligatoria en el esquema (sin `minOccurs="0"`), pero CXF no valida el esquema. Con token malo y sin `cuitRepresentada`, A4 respondió "Token malformado" y no un error de esquema. Eso sugiere, sin confirmarlo, que el elemento faltante llega como null a la lógica, donde aplicaría este mensaje.

## Tablas y datos

Tipos simples (PDF p.19): CUIT long de 11 dígitos; TipoPersona `FISICA`/`JURIDICA`; TipoClave `CUIT`, `CUIL`, `CDI`; EstadoClave `ACTIVO`, `INACTIVO`; TipoDocumento tabla 5.1; TipoDomicilio `FISCAL`, `LEGAL/REAL`; TipoFormaJuridica y TipoDatoAdicional: anexo 5.2 (A100).

5.1 Valores de TipoDocumento (PDF p.23):

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

5.2 Valores de tipos de parámetros (PDF p.24):

| Nombre tipo parámetro | CollectionName (en `ws_sr_padron_a100`) | Comentario |
|---|---|---|
| TipoFormaJuridica | `SUPA.TIPO_EMPRESA_JURIDICA` | "El valor devuelto por este WS (A13), es el que corresponde al atributo DESC_TIPO_EMPRESA_JURIDICA" |
| TipoDatoAdicional | `SUPA.TIPO_DATO_ADICIONAL_DOMICILIO` | "El valor devuelto por este WS (A13), es el que corresponde al atributo DESC_TIPO_DATO_ADICIONAL_DOM" |

Los valores de TipoDatoAdicional que traen A4, A5 y A10 son BARRIO, PARAJE, NO DETERMINADO, ESTAFETA, ENTRE LAS CALLES:, ESQUINA, SITIO WEB.

Valores vistos en los ejemplos: `estadoDomicilio` `CONFIRMADO`, `NO DENUNCIADO`, `DECLARADO` (la lista completa no está en el manual: NO VERIFICADO); provincias 6 JUJUY y 11 SAN LUIS; forma jurídica "FIDEICOMISO TESTAMENTARIO".

Personas de los ejemplos: 27015942210 (JAZMIN INTENTAR, LC 1594221, activa; la misma del ejemplo de constancia), 20002444233 (PHILLIP GEOFFREY WILLIAM, DNI 265181, INACTIVO), DNI 11709676 → 23117096769 y 27117096764. Cuáles existen en homologación: NO VERIFICADO. No hay archivo de datos de prueba propio; el de A4 está en `ws_sr_padron_a4.md`.

## Comportamiento a simular

- Sin estado. Usa el mismo padrón ficticio que A4, A5 y A10. Relación clave: `getIdPersonaListByDocumento(dni)` devuelve las CUIT/CUIL cuyo `numeroDocumento` es ese DNI. Para CUIT/CUIL de persona física, el DNI está en los dígitos 3 a 10 (convención de la CUIT, visible en el ejemplo; no lo dice el manual). Después, `getPersona(cuit)` devuelve los datos.
- `getPersona` falla para claves inactivas, `getPersonaV2` no: devuelve `estadoClave` `INACTIVO`.
- Validaciones de `idPersona`: más de 11 dígitos → "El Id de la persona no es valido"; inexistente → "La Clave (CUIT/CUIL) consultada es inexistente".
- Domicilio: usar `codigoPostal` (no `codPostal`); incluir `calle`, `numero` y `estadoDomicilio`; `direccion` = calle + " " + número en los ejemplos.
- `formaJuridica` y `tipoDatoAdicional` como texto, tomados de las colecciones de A100.
- Serialización: orden alfabético, campos vacíos omitidos, `metadata` primero en `personaReturn` y último en `idPersonaListReturn`.

## No verificado

- El transporte (Fault o no) de los errores de negocio de la tabla 5.3, y si llevan `detail` `SRValidationException`.
- A qué situación responde "No autorizado, par token/sign invalido." frente al "No se pudo verificar..." observado en otros servicios.
- `getIdPersonaListByDocumento` sin resultados, con documento no numérico o muy largo, y si incluye claves inactivas.
- Los valores posibles de `estadoDomicilio`, y cuándo vienen `manzana`, `torre`, `sector`, `piso` y `oficinaDptoLocal`.
- Si `fechaInscripcion` (está en el manual y no en el WSDL) aparece alguna vez.
- La regla exacta de serialización de fechas históricas.
- Si `getPersonaV2` devuelve algo distinto de `getPersona` para claves activas.
- Textos de falla de TA vencido y TA de otro service id.
