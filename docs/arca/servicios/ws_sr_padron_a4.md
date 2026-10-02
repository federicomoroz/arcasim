# ws_sr_padron_a4

Consulta a Padrón Alcance 4: recibe una CUIT y devuelve los datos del contribuyente en el padrón con foco en su situación tributaria (impuestos, regímenes, categorías, actividades, domicilios, relaciones, teléfonos y e-mails).

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/ws_sr_padron_a4/manual_ws_sr_padron_a4_v1.3.pdf`, versión 1.3 del 04/01/23. Se descargó el 2026-10-02 (HTTP 200, `application/pdf`). "PDF p.N" es la página N del archivo PDF, no el número impreso.
- WSDL: `docs/arca/wsdl/ws_sr_padron_a4-homologacion.wsdl` (bajado el 2026-10-01). El esquema está inline en `wsdl:types`, **no hay `xsd:import`, `xsd:include` ni `wsdl:import`**, así que no hubo XSD para bajar.
- WSDL de producción pedido el 2026-10-02 a `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA4?WSDL`: es idéntico al de homologación salvo el host del `soap:address`.
- Datos de prueba: `https://www.afip.gob.ar/ws/ws_sr_padron_a4/datos-prueba-padron-a4.txt` (bajado el 2026-10-02, HTTP 200 `text/plain`, 477 bytes).
- Llamadas reales sin credenciales contra homologación: 2026-10-02, alrededor de las 18:08 GMT.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Padrón** (Apache CXF, JAX-WS, document/literal wrapped). Ver `catalogo.md` 4.1 | WSDL; faults observados |
| Endpoint homologación | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA4` | `soap:address` del WSDL; manual PDF p.5 |
| Endpoint producción | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA4` | `soap:address` del WSDL de producción; manual PDF p.5 |
| Hosts alternativos | `aws.arca.gob.ar` responde el WSDL con HTTP 200 (probado 2026-10-02). El `soap:address` se arma con el host pedido | Prueba en vivo |
| WSDL | `?WSDL` sobre el endpoint. Archivo local `wsdl/ws_sr_padron_a4-homologacion.wsdl` | — |
| Servicio / puerto | `wsdl:service name="PersonaServiceA4"`, `port name="PersonaServiceA4Port"`, binding `PersonaServiceA4SoapBinding` | WSDL |
| Namespace | `http://a4.soap.ws.server.puc.sr/` (targetNamespace del WSDL y del esquema) | WSDL |
| `elementFormDefault` | `unqualified`: solo el elemento wrapper (`getPersona`, `getPersonaResponse`) va calificado; los hijos van sin namespace | WSDL |
| WSAA service id | `ws_sr_padron_a4` | Manual 2.4, PDF p.6 |
| SOAPAction | `soapAction=""` en todas las operaciones. Un POST **sin** header `SOAPAction` también funciona (probado contra `awshomo.arca.gob.ar`) | WSDL; prueba en vivo |
| SOAP | Solo **SOAP 1.1** (`http://schemas.xmlsoap.org/wsdl/soap/`). Un sobre SOAP 1.2 recibe HTTP 500 `VersionMismatch` (ver Validaciones) | WSDL; prueba en vivo |
| Content-Type de respuesta | `text/xml;charset=UTF-8`. Sin declaración `<?xml?>`, sin `soap:Header`, todo en una sola línea | Prueba en vivo |
| Prefijos de respuesta | Sobre `soap:`; wrapper `ns2:` con `xmlns:ns2="http://a4.soap.ws.server.puc.sr/"` | Prueba en vivo; manual |

Operaciones del `portType` `PersonaServiceA4`: **2** (`dummy`, `getPersona`). `getPersona` declara el fault `SRValidationException` (elemento vacío `tns:SRValidationException`); `dummy` no declara faults.

## Autenticación

- `token` y `sign` van como elementos sueltos dentro del wrapper `getPersona`, junto con `cuitRepresentada`. No hay `<Auth>` ni header SOAP. Salen del Ticket de Acceso que emite WSAA para el service id `ws_sr_padron_a4` (manual 2.2 y 2.4, PDF p.5-6). El formato del token está en `wsaa.md` 4.3.
- `cuitRepresentada` "Debe coincidir con alguna de las CUITS listadas en la sección relations del token enviado. Debe ser en representación de que organismo se solicita la operación" (manual 3.2.1, PDF p.8).
- `dummy` no requiere token (manual 2.2, PDF p.5).
- Todas las fallas de autenticación vuelven como **HTTP 500** con `soap:Fault`, `faultcode` `soap:Server`. Se validan **antes** que cualquier otro dato: con token malo y `idPersona` de 12 dígitos, o sin `cuitRepresentada`, sigue saliendo el error de token.

Respuestas observadas en homologación el 2026-10-02 (cuerpo crudo completo):

| Caso enviado | HTTP | Cuerpo |
|---|---|---|
| Sin elementos `token` ni `sign`, o solo con `token` | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Falta token y/o sign.</faultstring><detail><ns1:SRValidationException xmlns:ns1="http://a4.soap.ws.server.puc.sr/"/></detail></soap:Fault></soap:Body></soap:Envelope>` |
| `token`=`abc`, o `token` y `sign` vacíos (`<token></token>`), o `token`=`YWJj` (Base64 de "abc") | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Token malformado</faultstring></soap:Fault></soap:Body></soap:Envelope>` (sin `detail`) |
| `token` = Base64 de un XML `sso` 2.0 bien formado; `sign` falso | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token></faultstring></soap:Fault></soap:Body></soap:Envelope>` (sin `detail`) |

Ojo con el detalle: elemento ausente da "Falta token y/o sign." **con** `detail`; elemento presente pero vacío da "Token malformado" **sin** `detail`.

Headers de esas respuestas 500: `Content-Type: text/xml;charset=UTF-8`, `Connection: close`, más `Strict-Transport-Security`, `X-XSS-Protection`, `X-Frame-Options`, `X-Content-Type-Options` y cookies del balanceador (F5 / `TS01...`). El simulador no necesita las cookies.

No verificado para este servicio (no hay certificado de homologación): texto exacto de token vencido, de TA emitido para otro service id, de `cuitRepresentada` fuera de `relations` y de `cuitRepresentada` nula con token válido. Los textos que da el manual de A13 para estos casos están en "Validaciones y errores" como inferencia.

## Operaciones

Orden de elementos: el esquema del WSDL ya declara los tipos de respuesta en **orden alfabético** (lo genera CXF/JAXB sin `propOrder`), y los ejemplos del manual salen en ese mismo orden. Las tablas de "Definiciones de tipos" del manual usan otro orden (lógico, no alfabético): **el simulador tiene que seguir el orden del WSDL**. Los tipos de request sí tienen orden propio (`token`, `sign`, `cuitRepresentada`, `idPersona`).

### dummy

Propósito: verifica la disponibilidad de la aplicación, la autenticación y la base de datos (manual 3.1, PDF p.6). No lleva credenciales.

Request: `<a4:dummy/>`, tipo `dummy` con `xs:sequence` vacía.

Response: `dummyResponse` → `return` (tipo `dummyReturn`, minOccurs 0):

| Elemento | Tipo | Occurs | Significado |
|---|---|---|---|
| `appserver` | xs:string | 0..1 | `OK` o `ERROR` |
| `authserver` | xs:string | 0..1 | `OK` o `ERROR` |
| `dbserver` | xs:string | 0..1 | `OK` o `ERROR` |

Ejemplo del manual (PDF p.6-7), request:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a4="http://a4.soap.ws.server.puc.sr/">
<soapenv:Header/>
<soapenv:Body>
<a4:dummy/>
</soapenv:Body>
</soapenv:Envelope>
```

Respuesta real observada el 2026-10-02 (HTTP 200, `Content-Type: text/xml;charset=UTF-8`, 285 bytes):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><ns2:dummyResponse xmlns:ns2="http://a4.soap.ws.server.puc.sr/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse></soap:Body></soap:Envelope>
```

### getPersona

Propósito: "Devuelve el detalle de todos los datos, existentes en el padrón único de contribuyentes, del contribuyente solicitado" (manual 3.2, PDF p.8).

Request (`getPersona`; ningún elemento tiene `minOccurs`, así que todos son 1..1 según el esquema):

| Elemento | Tipo WSDL | Occurs | Significado y límites |
|---|---|---|---|
| `token` | xs:string | 1..1 | Token del TA de WSAA |
| `sign` | xs:string | 1..1 | Firma del TA |
| `cuitRepresentada` | xs:long | 1..1 | CUIT en cuya representación se consulta; debe estar en `relations` del token. Manual: tipo CUIT, long de 11 dígitos (PDF p.15, p.17) |
| `idPersona` | xs:long | 1..1 | "CUIT del cual se solicitan los datos" (PDF p.17). Long de 11 dígitos. Un valor no numérico corta en el unmarshalling de CXF (ver Validaciones) |

Response: `getPersonaResponse` → `personaReturn` (0..1) → `metadata` (0..1) y `persona` (0..1).

`metadata`:

| Elemento | Tipo | Occurs | Significado |
|---|---|---|---|
| `fechaHora` | xs:dateTime | 0..1 | Fecha y hora de proceso, con milisegundos y offset (ej. `2016-12-02T11:12:37.087-03:00`) |
| `servidor` | xs:string | 0..1 | Nombre del equipo que procesó (ej. `setiwebhomoext.afip.gob.ar`) |

`persona`, en el orden del WSDL. "Manual" es lo que dice la tabla de tipos (PDF p.17-19); todo es 0..1 o 0..* en el WSDL:

| Elemento | Tipo WSDL | Occurs | Tipo manual | Significado (manual) |
|---|---|---|---|---|
| `actividad` | tns:actividad, nillable | 0..* | Actividad | Actividades declaradas |
| `apellido` | xs:string | 0..1 | String | Apellido |
| `cantidadSociosEmpresaMono` | xs:int | 0..1 | Integer | Cantidad de socios de la empresa monotributista |
| `categoria` | tns:categoria, nillable | 0..* | Categoria | Categorías del contribuyente |
| `claveInactivaAsociada` | xs:long, nillable | 0..* | Cuit | Claves inactivas asociadas |
| `dependencia` | tns:dependencia | 0..1 | Dependencia | Dependencia de ARCA |
| `domicilio` | tns:domicilio, nillable | 0..* | Domicilio | Domicilios |
| `email` | tns:email, nillable | 0..* | Email | E-mails |
| `estadoClave` | xs:string | 0..1 | EstadoClave | `ACTIVO` o `INACTIVO` |
| `fechaContratoSocial` | xs:dateTime | 0..1 | DateTime | Fecha del contrato social |
| `fechaFallecimiento` | xs:dateTime | 0..1 | DateTime | Fecha de fallecimiento |
| `fechaInscripcion` | xs:dateTime | 0..1 | DateTime | Fecha de inscripción |
| `fechaJubilado` | xs:dateTime | 0..1 | DateTime | Fecha de jubilación |
| `fechaNacimiento` | xs:dateTime | 0..1 | DateTime | Fecha de nacimiento |
| `fechaVencimientoMigracion` | xs:dateTime | 0..1 | DateTime | Vencimiento del certificado de migración |
| `formaJuridica` | xs:string | 0..1 | String | Forma jurídica |
| `idPersona` | xs:long | 0..1 | CUIT | CUIT consultada |
| `impuesto` | tns:impuesto, nillable | 0..* | Impuesto | Impuestos inscriptos |
| `leyJubilacion` | xs:int | 0..1 | Integer | Ley por la cual se jubiló |
| `localidadInscripcion` | xs:string | 0..1 | String | Localidad donde se inscribió |
| `mesCierre` | xs:int | 0..1 | Integer | Mes de cierre de balance |
| `nombre` | xs:string | 0..1 | String | Nombre |
| `numeroDocumento` | xs:string | 0..1 | String | Número de documento |
| `numeroInscripcion` | xs:string | 0..1 | String | Número de inscripción asignado (la v1.2 "modifica el tipo de dato para número de inscripción", PDF p.2) |
| `organismoInscripcion` | xs:string | 0..1 | String | Organismo que realizó la inscripción |
| `organismoOriginante` | xs:string | 0..1 | String | Organismo originante |
| `porcentajeCapitalNacional` | xs:double | 0..1 | Double | Porcentaje de capital nacional |
| `provinciaInscripcion` | xs:string | 0..1 | String | Provincia donde se inscribió |
| `razonSocial` | xs:string | 0..1 | String | Razón social |
| `regimen` | tns:regimen, nillable | 0..* | Regimen | Regímenes inscriptos |
| `relacion` | tns:relacion, nillable | 0..* | Relacion | Relaciones con otras claves |
| `sexo` | xs:string | 0..1 | Sexo | `MASCULINO`, `FEMENINO`, `DESCONOCIDO` |
| `telefono` | tns:telefono, nillable | 0..* | Teléfono | Teléfonos |
| `tipoClave` | xs:string | 0..1 | TipoClave | `CUIT`, `CUIL` o `CDI` |
| `tipoDocumento` | xs:string | 0..1 | TipoDocumento | Tabla 5.1 |
| `tipoOrganismoOriginante` | xs:string | 0..1 | String | Tipo de organismo originante |
| `tipoPersona` | xs:string | 0..1 | TipoPersona | `FISICA` o `JURIDICA` |
| `tipoResidencia` | xs:string | 0..1 | TipoResidencia | Tabla 5.2 |

Subtipos, en orden del WSDL (el manual los marca 1..1, el WSDL todos `minOccurs="0"`):

- `actividad`: `descripcionActividad` (xs:string, según SUPA), `idActividad` (xs:long), `nomenclador` (xs:int; ej. 883), `orden` (xs:int; 1 = principal, 2 = secundaria, etc.), `periodo` (xs:int, AAAAMM de alta).
- `categoria`: `descripcionCategoria` (xs:string), `estado` (xs:string, tabla 5.3), `idCategoria` (xs:int), `idImpuesto` (xs:int), `periodo` (xs:int, AAAAMM inicial del estado).
- `dependencia`: `descripcionDependencia` (xs:string), `idDependencia` (xs:int).
- `domicilio`: `codPostal` (xs:string; el manual dice Int), `datoAdicional` (xs:string), `descripcionProvincia` (xs:string), `direccion` (xs:string; "Calle, numero, depto, etc."), `idProvincia` (xs:int, según SUPA), `localidad` (xs:string), `orden` (xs:int; posición dentro de los domicilios del mismo tipo, empieza en 1), `tipoDatoAdicional` (xs:string, tabla 5.9), `tipoDomicilio` (xs:string: `FISCAL`, `LEGAL/REAL`, `LOCALES Y ESTABLECIMIENTOS`).
- `email`: `direccion` (xs:string), `estado` (xs:string: "Confirmado", "No confirmado" o "No Verificado" según el tipo TipoEstadoEMail del manual), `tipoEmail` (xs:string, tabla 5.8).
- `impuesto`: `descripcionImpuesto` (xs:string), `diaPeriodo` (xs:int, 1 a 31: día de inicio del estado), `estado` (xs:string, tabla 5.3), `ffInscripcion` (xs:dateTime: fecha de inscripción en el impuesto), `idImpuesto` (xs:int), `periodo` (xs:int, AAAAMM inicial del estado).
- `regimen`: `descripcionRegimen` (xs:string), `diaPeriodo` (xs:int; está en el WSDL, no en la tabla del manual), `estado` (xs:string), `idImpuesto` (xs:int), `idRegimen` (xs:int), `periodo` (xs:int, AAAAMM), `tipoRegimen` (xs:string, tabla 5.4; en el ejemplo sale el texto `PERCEPCION`, no el número).
- `relacion`: `ffRelacion` (xs:dateTime), `ffVencimiento` (xs:dateTime), `idPersona` (xs:long), `idPersonaAsociada` (xs:long), `subtipoRelacion` (xs:string, tabla 5.7), `tipoRelacion` (xs:string, tabla 5.6). **El manual nombra los campos distinto**: `personaAsociada`, `fechaRelacion`, `fechaVencimiento` (PDF p.21). Vale el WSDL.
- `telefono`: `numero` (xs:long; el manual lo llama `NumeroTelefono`), `tipoLinea` (xs:string: "Fijo o Movil" según el manual), `tipoTelefono` (xs:string, tabla 5.5).

Ejemplo del manual, request (PDF p.10):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a4="http://a4.soap.ws.server.puc.sr/">
<soapenv:Header/>
<soapenv:Body>
<a4:getPersona>
       <token>ICAgIDwvcmVsYXRpb25zPgogICAgICAgIDwvbG9naW4+CiAgICA8L29wZXJhdGlvbj4KPC9zc28+Cgo=</token>
        <sign>+7rFJNrEcIKRQ+A2xx0m9B9hlVXzU/XHvZEEY7XsvRMIDPRiSlsLR8+MBYHTcfsO4=</sign>
<cuitRepresentada>20135464605</cuitRepresentada>
<idPersona>20000000516</idPersona>
</a4:getPersona>
</soapenv:Body>
</soapenv:Envelope>
```

Respuesta del ejemplo (PDF p.10-14). Se copia tal cual; el manual pierde el comienzo del `impuesto` 365 en un salto de página, y así queda:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <ns2:getPersonaResponse xmlns:ns2="http://a4.soap.ws.server.puc.sr/">
       <personaReturn>
          <metadata>
            <fechaHora>2016-12-02T11:12:37.087-03:00</fechaHora>
            <servidor>setiwebhomoext.afip.gob.ar</servidor>
          </metadata>
          <persona>
            <actividad>
               <descripcionActividad>CONSTRUCCIÓN, REFORMA Y REPARACIÓN DE EDIFICIOS RESIDENCIALES</descripcionActividad>
               <idActividad>410011</idActividad>
               <nomenclador>883</nomenclador>
               <orden>1</orden>
               <periodo>201311</periodo>
            </actividad>
            <apellido>ERNESTO DANIEL</apellido>
            <dependencia>
              <descripcionDependencia>DISTRITO ZAPALA</descripcionDependencia>
              <idDependencia>702</idDependencia>
            </dependencia>
            <domicilio>
              <codPostal>8371</codPostal>
              <datoAdicional>N/A PATRICIA ANDREA</datoAdicional>
              <descripcionProvincia>NEUQUEN</descripcionProvincia>
              <direccion>LAGUNA LOS SAUCES 2</direccion>
              <idProvincia>20</idProvincia>
              <localidad>JUNIN DE LOS ANDES</localidad>
              <orden>1</orden>
              <tipoDatoAdicional>NO DETERMINADO</tipoDatoAdicional>
              <tipoDomicilio>FISCAL</tipoDomicilio>
            </domicilio>
            <domicilio>
              <codPostal>1425</codPostal>
              <descripcionProvincia>CIUDAD AUTONOMA BUENOS AIRES</descripcionProvincia>
              <direccion>ARAOZ 1901</direccion>
              <idProvincia>0</idProvincia>
              <orden>1</orden>
              <tipoDomicilio>LEGAL/REAL</tipoDomicilio>
            </domicilio>
            <domicilio>
              <codPostal>1425</codPostal>
              <descripcionProvincia>CIUDAD AUTONOMA BUENOS AIRES</descripcionProvincia>
              <direccion>CHARCAS 4321</direccion>
              <idProvincia>1</idProvincia>
              <orden>1</orden>
              <tipoDomicilio>LOCALES Y ESTABLECIMIENTOS</tipoDomicilio>
            </domicilio>
            <domicilio>
              <codPostal>1425</codPostal>
              <descripcionProvincia>CIUDAD AUTONOMA BUENOS AIRES</descripcionProvincia>
              <direccion>MANSILLA 3037</direccion>
              <idProvincia>0</idProvincia>
              <orden>2</orden>
              <tipoDomicilio>LOCALES Y ESTABLECIMIENTOS</tipoDomicilio>
            </domicilio>
            <estadoClave>ACTIVO</estadoClave>
            <fechaInscripcion>2010-10-30T12:00:00-03:00</fechaInscripcion>
            <fechaNacimiento>1891-01-01T12:00:00-03:00</fechaNacimiento>
            <idPersona>20000000516</idPersona>
            <impuesto>
              <descripcionImpuesto>GANANCIAS PERSONAS FISICAS</descripcionImpuesto>
              <diaPeriodo>30</diaPeriodo>
              <estado>BAJA DEFINITIVA</estado>
              <ffInscripcion>2002-04-05T12:00:00-03:00</ffInscripcion>
              <idImpuesto>11</idImpuesto>
              <periodo>200611</periodo>
            </impuesto>
            <impuesto>
              <descripcionImpuesto>MONOTRIBUTO</descripcionImpuesto>
              <diaPeriodo>30</diaPeriodo>
              <estado>BAJA DEFINITIVA</estado>
              <ffInscripcion>2000-03-11T12:00:00-03:00</ffInscripcion>
              <idImpuesto>20</idImpuesto>
              <periodo>200406</periodo>
            </impuesto>
            <impuesto>
              <descripcionImpuesto>MONOTRIBUTO AUTONOMO</descripcionImpuesto>
              <diaPeriodo>30</diaPeriodo>
              <estado>BAJA DEFINITIVA</estado>
              <ffInscripcion>2000-03-11T12:00:00-03:00</ffInscripcion>
              <idImpuesto>21</idImpuesto>
              <periodo>200406</periodo>
            </impuesto>
            <impuesto>
              <descripcionImpuesto>GANANCIA MINIMA PRESUNTA</descripcionImpuesto>
              <diaPeriodo>30</diaPeriodo>
              <estado>BAJA DEFINITIVA</estado>
              <ffInscripcion>2005-03-15T12:00:00-03:00</ffInscripcion>
              <idImpuesto>25</idImpuesto>
              <periodo>200611</periodo>
            </impuesto>
            <impuesto>
              <descripcionImpuesto>SICORE-IMPTO.EMERG.AUTOMOTORES</descripcionImpuesto>
              <diaPeriodo>31</diaPeriodo>
              <estado>BAJA DEFINITIVA</estado>
              <ffInscripcion>2008-01-29T12:00:00-02:00</ffInscripcion>
              <idImpuesto>64</idImpuesto>
              <periodo>200708</periodo>
            </impuesto>
            <impuesto>
              <descripcionImpuesto>EMPLEADOR-APORTES SEG. SOCIAL</descripcionImpuesto>
              <diaPeriodo>15</diaPeriodo>
              <estado>ACTIVO</estado>
              <ffInscripcion>2005-03-15T12:00:00-03:00</ffInscripcion>
              <idImpuesto>301</idImpuesto>
              <periodo>200503</periodo>
            </impuesto>
            <impuesto>
              <descripcionImpuesto>APORTES SEG.SOCIAL AUTONOMOS</descripcionImpuesto>
              <diaPeriodo>10</diaPeriodo>
              <estado>BAJA DEFINITIVA</estado>
              <ffInscripcion>2008-01-25T12:00:00-02:00</ffInscripcion>
              <idImpuesto>308</idImpuesto>
              <periodo>200804</periodo>
            </impuesto>
            <impuesto>
estado>BAJA DEFINITIVA</estado>
              <ffInscripcion>2005-09-12T12:00:00-03:00</ffInscripcion>
              <idImpuesto>365</idImpuesto>
              <periodo>200611</periodo>
            </impuesto>
            <impuesto>
              <descripcionImpuesto>ADICIONAL EMERG.CIGARRILLOS</descripcionImpuesto>
              <diaPeriodo>1</diaPeriodo>
              <estado>ACTIVO</estado>
              <ffInscripcion>2008-01-28T12:00:00-02:00</ffInscripcion>
              <idImpuesto>366</idImpuesto>
              <periodo>200708</periodo>
            </impuesto>
            <mesCierre>12</mesCierre>
            <nombre>MARCELO NICOLAS</nombre>
            <numeroDocumento>51</numeroDocumento>
            <regimen>
              <descripcionRegimen>REG.PER. IMPUESTO DE EMERGENCIA A LOS AUTOMOTORES Y OTROS - FONDO NACIONAL DE INCENTIVO DOCENTE.</descripcionRegimen>
              <estado>BAJA DEFINITIVA</estado>
              <idImpuesto>64</idImpuesto>
              <idRegimen>715</idRegimen>
              <periodo>200708</periodo>
              <tipoRegimen>PERCEPCION</tipoRegimen>
            </regimen>
            <sexo>MASCULINO</sexo>
            <tipoClave>CUIT</tipoClave>
            <tipoPersona>FISICA</tipoPersona>
          </persona>
       </personaReturn>
     </ns2:getPersonaResponse>
  </soap:Body>
</soap:Envelope>
```

Lo que muestra el ejemplo y conviene imitar:

- Los elementos salen en orden alfabético, igual que en el WSDL. `metadata` va antes que `persona` porque también es alfabético.
- Las listas (`impuesto`, `actividad`) aparecen ordenadas por id ascendente (11, 20, 21, 25, 64, 301, 308, 365, 366). Que sea una regla del servicio está **NO VERIFICADO**.
- Las fechas sin hora salen como mediodía local con offset (`T12:00:00-03:00`, y `-02:00` en fechas del horario de verano argentino 2007-2008).
- Los campos sin valor se omiten; no salen vacíos ni con `xsi:nil`.
- El ejemplo es incoherente en `idProvincia`: CABA aparece con 0 y con 1. Es un dato del manual, no una regla.

## Validaciones y errores

El manual de A4 **no trae tabla de errores**. Lo que sigue sale de pruebas en vivo y, marcado como inferencia, del manual de A13, que es del mismo sistema (`sr-padron`).

| Código / texto | Condición | Dónde aparece | Fuente |
|---|---|---|---|
| `Falta token y/o sign.` | Falta el elemento `token` o `sign` | `soap:Fault`, `faultcode` `soap:Server`, con `<detail><ns1:SRValidationException xmlns:ns1="http://a4.soap.ws.server.puc.sr/"/></detail>`; HTTP 500 | En vivo 2026-10-02 |
| `Token malformado` | Token presente pero vacío, no Base64 o que no decodifica a un XML `sso` válido | `soap:Fault` `soap:Server` sin `detail`; HTTP 500 | En vivo 2026-10-02 |
| `No se pudo verificar que <sign> contenga una firma valida de <token>` (serializado como `&lt;sign>` y `&lt;token>`) | Token XML válido con firma que no verifica | `soap:Fault` `soap:Server` sin `detail`; HTTP 500 | En vivo 2026-10-02 |
| `Unmarshalling Error: For input string: "XYZ" ` (con espacio final) | `idPersona` (o cualquier `xs:long`) no numérico. Pasa antes de validar el token | `soap:Fault` `faultcode` `soap:Client`; HTTP 500 | En vivo 2026-10-02 |
| `Unmarshalling Error: Unexpected close tag </soapenv:Body>; expected </x:dummy>.` + salto de línea + ` at [row,col {unknown-source}]: [1,156] ` | XML mal formado | `soap:Fault` `soap:Client`; HTTP 500 | En vivo 2026-10-02 |
| `Error reading XMLStreamReader.` | POST con cuerpo vacío | `soap:Fault` `soap:Client`; HTTP 500 | En vivo 2026-10-02 |
| `Message part {http://a4.soap.ws.server.puc.sr/}getPersonaList was not recognized.  (Does it exist in service WSDL?)` (dos espacios antes del paréntesis) | Operación inexistente | `soap:Fault` `soap:Client`; HTTP 500 | En vivo 2026-10-02 |
| `Unexpected wrapper element {http://a5.soap.ws.server.puc.sr/}dummy found.   Expected {http://a4.soap.ws.server.puc.sr/}dummy.` (tres espacios) | Wrapper con namespace de otro servicio | `soap:Fault` `soap:Client`; HTTP 500 | En vivo 2026-10-02 |
| `A SOAP 1.2 message is not valid when sent to a SOAP 1.1 only endpoint.` | Sobre SOAP 1.2 (`application/soap+xml`) | Fault en sobre SOAP 1.2 (`http://www.w3.org/2003/05/soap-envelope`) con `<faultcode xmlns:ns1="http://schemas.xmlsoap.org/soap/envelope/">ns1:VersionMismatch</faultcode>`; HTTP 500, `Content-Type: application/soap+xml;charset=UTF-8` | En vivo 2026-10-02 |
| `No such operation: null (HTTP GET PATH_INFO: /sr-padron/webservices/personaServiceA4null)` | GET al endpoint sin `?WSDL` | `soap:Fault` `soap:Server`; HTTP 500 | En vivo 2026-10-02 |
| `El Id de la persona no es valido` | `idPersona` de más de 11 dígitos | NO VERIFICADO en A4; texto del manual de A13 (PDF p.24). Probablemente Fault | Inferencia |
| `La Clave (CUIT/CUIL) consultada es inexistente` | `idPersona` no existe | NO VERIFICADO en A4; manual A13 | Inferencia |
| `La clave (CUIT/CUIL) consultada se encuentra INACTIVA` | Clave inactiva | NO VERIFICADO en A4 (y A4 devuelve `estadoClave`, así que puede que no falle) | Inferencia |
| `No autorizado, par token/sign invalido.` | Firma del token inválida en el login | NO VERIFICADO; manual A13. En vivo se vio otro texto para firma falsa | Inferencia |
| `Debe enviar la CUIT representada` | `cuitRepresentada` nula | NO VERIFICADO en A4; manual A13 | Inferencia |
| `Este token no le permite actuar en representacion de la CUIT ` + cuitRepresentada | `cuitRepresentada` no está en `relations` | NO VERIFICADO en A4; manual A13 | Inferencia |

## Tablas y datos

Todas copiadas completas del anexo 5 del manual. La numeración de tablas que usa la sección 4.1 coincide con el anexo: TipoDocumento 5.1, TipoResidencia 5.2, Estado 5.3, TipoRegimen 5.4, TipoTelefono 5.5, TipoRelacion 5.6, SubtipoRelación 5.7, TipoEMail 5.8, TipoDatoAdicional 5.9 (PDF p.15-16). La fuente de verdad de estos valores es SUPA, consultable con `ws_sr_padron_a100`.

Tipos simples con valores fijos (PDF p.15-16):

| Tipo | Valores |
|---|---|
| CUIT | long, 11 dígitos |
| TipoPersona | `FISICA`, `JURIDICA` |
| TipoClave | `CUIT`, `CUIL`, `CDI` |
| EstadoClave | `ACTIVO`, `INACTIVO` |
| Sexo | `MASCULINO`, `FEMENINO`, `DESCONOCIDO` |
| Orden | Entero; posición dentro de un conjunto de elementos del mismo tipo; empieza en 1 |
| TipoDomicilio | `FISCAL`, `LEGAL/REAL`, `LOCALES Y ESTABLECIMIENTOS` |
| TipoLinea | "Fijo o Movil" (el manual no da el literal exacto) |
| TipoEstadoEMail | "Confirmado", "No confirmado" ó "No Verificado" |

5.1 TipoDocumento (PDF p.23):

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

5.2 TipoResidencia (PDF p.24): PERMANENTE, TEMPORARIA, TRANSITORIA, NO RESIDENTE, PRECARIA, NATURALIZADO.

5.3 TipoEstado (para `impuesto.estado`, `categoria.estado`, `regimen.estado`; PDF p.24): ACTIVO, ACTIVO EN TRAMITE, BAJA PROVISORIA, BAJA DEFINITIVA, EXENTO, EXENTO EN TRAMITE, NO APORTANTE.

5.4 TipoRegimen (PDF p.25):

| tipoRegimen | descripcionRegimen |
|---|---|
| 1 | RETENCION |
| 2 | PERCEPCION |
| 3 | REGIMEN EXCEPCIONAL DE INGRESO |
| 4 | NO RETENCIÓN |
| 5 | PAGO A CUENTA |
| 6 | CONVENIO CORRESPONSAB.GREMIAL |
| 7 | PAGO UNICO Y DEFINITIVO |
| 8 | PERCEP. NO COMP. DET. DE ANT. |
| 9 | PERCEP.COMP.COMO CRED.FISCAL |

En la respuesta, `tipoRegimen` sale como texto (`PERCEPCION`), no como el número.

5.5 TipoTelefono (PDF p.25): PARTICULAR, COMERCIAL, DEL CONTADOR, DEL ASESOR, PERSONAL INTERNET, CAMPAÑA TELEFÓNICA, CONTACTO ADUANERO.

5.6 TipoRelacion (PDF p.26): CLAVE INACTIVA, ORGANISMO CENTRAL, SOCIEDAD CON INTEGRANTES, RESPONSABLE SUSTITUTO GAN.MIN.PRES, PODERDANTE, RESPONSABLE SUSTITUTO BIENES PERS., ASOCIADO A COOPERATIVA, SUCESION JUDICIAL, MENOR, INCAPAZ, SUCESION NO INICIADA JUDICIALMENTE, ORGANISMO REPRESENTANTE DE LA PROVINCIA, PERSONA FISICA CON QUIEBRA, SUCESION BENEFICIARIO ANSES, RESIDENTE EN EL EXTERIOR, ALIANZA TRANSITORIA.

5.7 SubtipoRelación (PDF p.27-29). Copiado tal cual; varios textos vienen truncados en el propio manual (probablemente por el largo de la columna en SUPA):

```
ADM. CENTRAL PROVINCIAL
COMISIONES DE FOMENTO
COMUNA
COOPERATIVAS / SERV PUBLICOS
EMPRESAS DEL ESTADO
ENTES MUNICIPALES
MUNICIPIOS
ORG. DESCENTRALIZADOS PROV
SERV. PROVINCIALES
NOTIFICARSE DE EXPEDIENTES O SUMARIOS
SOLICITAR Y RETIRAR VALORES
FIRMAR DDJJ Y SOLICITUDES DE PLAZO O PRÓRROGA, ACEPTAR DETE
RETIRAR DOCUMENTACIÓN AGREGADA A EXPEDIENTES Y ACTUACIONES C
FIRMAR RECIBOS PROVISIONALES O DEFINITIVOS
FIRMAR LETRAS Y CUALQUIER OTRO DOCUMENTO QUE IMPORTE OBLIGAC
INTERPONER RECURSOS ADMINISTRATIVOS REFERENTES A LA LIQUIDAC
ALEGAR DEFENSA E INTERPONER RECURSOS ANTE LA ADMINISTRACIÓN,
PERCIBIR EL IMPORTE DE DEVOLUCIONES
RENUNCIAR A LA PRESCRIPCIÓN GANADA O AL TÉRMINO CORRIDO DE L
OTRAS
Formulario 3283 Puntos 1 a 10
F. 3283 Registración anterior al 23/11/2012
Otros cargos
Director Titular
Presidente
Socio
Representante
Administrador fiduciario
Condomino
Condomino Administrador
Sociedad Gerente (1)
Sociedad UTE (2)
Gerente Titular
Administrador Titular
Socio Comanditado
Socio Comanditario
Miembro del Consejo de Vigilancia
Síndico Titular
Representante del Consejo de Administración
Apoderado
Fundador
Fiduciante
Fideicomisario
Beneficiario
Sociedad Depositaria
Accionista
Representante del Órgano Directivo
Representante del Órgano de Fiscalización
Presidente del Consejo de Administración
Socios Partícipes
Socios Protectores
Propietarios
Promotores
Apoderado F.3283
Vicepresidente
Secretario
Prosecretario
Tesorero
Protesorero
Consejero
Director Suplente
Gerente Suplente
Administrador Suplente
Síndico Suplente
Liquidador Titular
Liquidador Suplente
Miembro del Consejo de Vigilancia Suplente
Administrador
Apoderado Legal
Apoderado Legal y Fiscal
Responsable Económico Financiero
```

5.8 TipoEMail (PDF p.30): COMERCIAL, PERSONAL, TRIBUTARIO, OTROS, PERSONAL INTERNET, CONTACTO ADUANERO, SICNEA, E-VENTANILLA.

5.9 TipoDatoAdicional (PDF p.30): BARRIO, PARAJE, NO DETERMINADO, ESTAFETA, ENTRE LAS CALLES:, ESQUINA, SITIO WEB.

Valores que aparecen en el ejemplo (útiles como semilla, no son tablas completas):

- Impuestos: 11 GANANCIAS PERSONAS FISICAS, 20 MONOTRIBUTO, 21 MONOTRIBUTO AUTONOMO, 25 GANANCIA MINIMA PRESUNTA, 64 SICORE-IMPTO.EMERG.AUTOMOTORES, 301 EMPLEADOR-APORTES SEG. SOCIAL, 308 APORTES SEG.SOCIAL AUTONOMOS, 365 (descripción perdida en el manual), 366 ADICIONAL EMERG.CIGARRILLOS. IVA es el 30 (lo muestra el manual de constancia).
- Provincias: 0 CIUDAD AUTONOMA BUENOS AIRES, 20 NEUQUEN (y el 1 aparece mal asociado a CABA en un domicilio del ejemplo). La tabla completa está en `ws_sr_padron_a100`, colección `SUPA.E_PROVINCIA`; **no está en ningún manual (NO VERIFICADO)**.
- Dependencia: 702 DISTRITO ZAPALA. Régimen: 715 (impuesto 64, PERCEPCION). Actividad: 410011, nomenclador 883.

Datos de prueba oficiales (`datos-prueba-padron-a4.txt`, copiado completo):

```
CUITes, Personas Fisicas:
20002307554
20002460123
20188192514
20221062583
20200083394
20220707513
20221124643
20221064233
20201731594
20201797064

CUILes, Personas Fisicas:
20203032723
20168598204
20188153853
20002195624
20002400783
20187850143
20187908303
20187986843
20188027963
20187387443

CUITes, Personas Juridicas:
30202020204
30558515305
30558521135
30558525025
30558525645
30558529535
30558535365
30558535985
30558539565
30558564675
```

Qué datos devuelve homologación para cada una de esas claves: **NO VERIFICADO** (requiere certificado).

## Comportamiento a simular

- Sin estado: `getPersona` es una lectura pura del padrón ficticio. No hay operaciones de escritura ni relación entre llamadas. `dummy` es independiente.
- Orden de chequeos observado: 1) parseo y unmarshalling SOAP (`soap:Client`); 2) presencia de `token`/`sign` ("Falta token y/o sign."); 3) decodificación del token ("Token malformado"); 4) firma ("No se pudo verificar..."); recién después, `cuitRepresentada` e `idPersona`. Los pasos que siguen al 4 no se pudieron observar.
- Serialización: elementos de `persona` en orden alfabético; omitir los vacíos; listas como elementos repetidos sin contenedor; `metadata.fechaHora` con milisegundos y offset `-03:00`; `metadata.servidor` con un nombre de host.
- Base de contribuyentes: la misma que A5, A10 y A13. A4 es la vista "completa" (impuestos con estado y `ffInscripcion`, regímenes, categorías, relaciones, teléfonos, e-mails). Conviene sembrar las 30 claves del archivo de prueba.
- Validar que el TA sea del service id `ws_sr_padron_a4` y que `cuitRepresentada` esté en `relations`. Los textos exactos para esos casos son inferencia (tabla de arriba).
- `idPersona` inexistente: el formato exacto (fault o `persona` vacío) está **NO VERIFICADO**. La opción más consistente con A13 es un Fault `soap:Server` con "La Clave (CUIT/CUIL) consultada es inexistente" y `detail` `SRValidationException`.

## No verificado

- Errores de negocio de A4 (CUIT inexistente, inactiva, más de 11 dígitos) y su transporte. El manual no tiene tabla de errores.
- Textos de falla para TA vencido, TA de otro servicio, `cuitRepresentada` nula o fuera de `relations` con token válido.
- Qué devuelve homologación para las 30 CUIT de prueba.
- Si las listas (`impuesto`, `actividad`, `domicilio`) salen siempre ordenadas por id.
- La regla exacta de serialización de fechas (mediodía local con offset histórico de Argentina, según los ejemplos).
- El literal exacto de `telefono.tipoLinea` y de `email.estado`.
- Si `regimen.diaPeriodo` (está en el WSDL y no en el manual) viene con datos.
- Tabla completa de provincias y de impuestos (están en A100 y no en este manual).
- Si hace falta alguna autorización especial: el manual habla de "organismo externo" (PDF p.4). Si WSASS de homologación deja asociar este servicio a cualquier certificado está NO VERIFICADO.
