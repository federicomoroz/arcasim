# ws_sr_constancia_inscripcion

Consulta de la Constancia de Inscripción (ex `ws_sr_padron_a5`): recibe una CUIT, o una lista de hasta 250, y devuelve los datos de la constancia. Trae datos generales y domicilio fiscal, régimen general (impuestos, actividades, regímenes, categoría de autónomos) o monotributo, caracterizaciones, y los errores que impiden emitir la constancia.

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/WSCI/manual_ws_sr_ws_constancia_inscripcion.pdf`, versión 4.1, "Marzo 2026" (historial hasta 27/02/2026). Se descargó el 2026-10-02 (HTTP 200, `application/pdf`). "PDF p.N" es la página del archivo; el número impreso ("Pág. N de 32") va 1 atrás.
- WSDL: `docs/arca/wsdl/ws_sr_constancia_inscripcion-homologacion.wsdl` (bajado el 2026-10-01). Esquema inline: **no hay `xsd:import`, `xsd:include` ni `wsdl:import`**, así que no hubo XSD para bajar.
- WSDL de producción pedido el 2026-10-02 a `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5?WSDL`: es idéntico al de homologación salvo el host.
- Resumen previo: `catalogo.md` 5.1. Las pruebas en vivo del 2026-10-01 están en `catalogo.md` 4.1; las del 2026-10-02 están acá.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Padrón** (Apache CXF, JAX-WS, document/literal wrapped) | WSDL; faults observados |
| Endpoint homologación | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5` (`soap:address`). El manual 4.1 da `https://awshomo.arca.gob.ar/sr-padron/webservices/personaServiceA5?WSDL` (PDF p.5), que también responde | WSDL; manual 2.5 |
| Endpoint producción | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5` (`soap:address` de producción). El manual da `https://aws.arca.gob.ar/...` (PDF p.5), que también responde (catálogo 3.2) | WSDL; manual |
| Servicio / puerto | `PersonaServiceA5`, puerto `PersonaServiceA5Port`, binding `PersonaServiceA5SoapBinding` | WSDL |
| Namespace | `http://a5.soap.ws.server.puc.sr/` | WSDL |
| `elementFormDefault` | `unqualified` | WSDL |
| WSAA service id | `ws_sr_constancia_inscripcion` (manual 2.4, PDF p.5). El nombre histórico `ws_sr_padron_a5` usa el mismo endpoint; que WSAA siga emitiendo TA para ese id está NO VERIFICADO (catálogo 3.2) | Manual |
| SOAPAction | `soapAction=""` en las 5 operaciones | WSDL |
| SOAP | Solo SOAP 1.1 (A4, del mismo stack, rechaza 1.2 con `VersionMismatch`) | WSDL |
| Content-Type de respuesta | `text/xml;charset=UTF-8`, sin `<?xml?>`, sin `soap:Header` | En vivo |

Operaciones del `portType` `PersonaServiceA5`: **5** (`getPersona`, `getPersonaList`, `getPersona_v2`, `dummy`, `getPersonaList_v2`). Todas menos `dummy` declaran el fault `SRValidationException`.

## Autenticación

- `token`, `sign` y `cuitRepresentada` van sueltos dentro del wrapper, sin `<Auth>` ni header SOAP. TA de WSAA para `ws_sr_constancia_inscripcion` (manual 2.3-2.4, PDF p.5): "Exceptuando el método dummy, todas las invocaciones requieren credenciales válidas (token y sign)", "específicas para cada ambiente".
- `cuitRepresentada`: "Debe coincidir con alguna de las CUITS listadas en la sección relations del token enviado" (manual 3.2.1, PDF p.8).
- Fallas de autenticación: **HTTP 500**, `soap:Fault`, `faultcode` `soap:Server`. Se chequean antes que cualquier dato de negocio: con token malo y 251 `idPersona`, sale "Token malformado".

Respuestas observadas en homologación (cuerpo crudo completo):

| Fecha | Caso enviado | HTTP | Cuerpo |
|---|---|---|---|
| 2026-10-02 | `getPersona_v2` sin elementos `token` ni `sign` (también `getPersonaList_v2` con 251 ids o con 0 ids) | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Falta token y/o sign.</faultstring><detail><ns1:SRValidationException xmlns:ns1="http://a5.soap.ws.server.puc.sr/"/></detail></soap:Fault></soap:Body></soap:Envelope>` |
| 2026-10-02 | `getPersona_v2` y `getPersonaList_v2` (251 ids) con `token`=`abc` | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Token malformado</faultstring></soap:Fault></soap:Body></soap:Envelope>` |
| 2026-10-01 | Token XML bien formado, firma falsa | 500 | `faultstring` "No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token>" (catálogo 4.1), sin `detail` (igual en A4, A10 y A100 el 2026-10-02) |

En A4 se vio además que `<token></token>` vacío da "Token malformado" y que el elemento ausente da "Falta token y/o sign.". Se asume igual acá.

## Operaciones

### Orden de elementos

El esquema del WSDL ya es **alfabético** en todos los tipos de respuesta, incluido `personaReturn`, que pone `metadata` **al final**: `datosGenerales`, `datosMonotributo`, `datosRegimenGeneral`, `errorConstancia`, `errorMonotributo`, `errorRegimenGeneral`, `metadata`. En cambio `personaListReturn` pone `metadata` **primero**, porque alfabéticamente va antes que `persona`. Los esquemas de respuesta del manual (PDF p.9 y p.15) ponen `metadata` arriba en los dos casos, pero el ejemplo real de `getPersona_v2` (PDF p.10-13) la trae al final. **Vale el orden del WSDL.**

### Tipos compartidos

`getPersona` y `getPersona_v2` devuelven **el mismo tipo** `personaReturn`; `getPersonaList` y `getPersonaList_v2`, el mismo `personaListReturn`. La diferencia entre v1 y v2 es de contenido, no de esquema: la v2 "incluye todas las actividades del monotributista y las caracterizaciones vigentes" (PDF p.13 y p.21).

`personaReturn` (respuesta de `getPersona`/`getPersona_v2`), todo 0..1:

| Elemento | Tipo | Significado |
|---|---|---|
| `datosGenerales` | tns:datosGenerales | Datos propios del contribuyente |
| `datosMonotributo` | tns:datosMonotributo | Datos del régimen de monotributo |
| `datosRegimenGeneral` | tns:datosRegimenGeneral | Datos del régimen general |
| `errorConstancia` | tns:errorConstancia | Errores comunes de la constancia |
| `errorMonotributo` | tns:errorMonotributo | Errores de la constancia de monotributo |
| `errorRegimenGeneral` | tns:errorRegimenGeneral | Errores de la constancia de régimen general |
| `metadata` | tns:metadata | `fechaHora` (xs:dateTime), `servidor` (xs:string) |

`persona` (cada elemento de `personaListReturn`): los mismos seis primeros, sin `metadata`.

`personaListReturn`: `metadata` (0..1) y `persona` (0..*, nillable).

`datosGenerales`, en orden del WSDL. Las descripciones son del manual, PDF p.23-24:

| Elemento | Tipo | Occurs | Significado |
|---|---|---|---|
| `apellido` | xs:string | 0..1 | Apellido |
| `caracterizacion` | tns:caracterizacion, nillable | 0..* | Caracterizaciones |
| `dependencia` | tns:dependencia | 0..1 | Dependencia del contribuyente |
| `domicilioFiscal` | tns:domicilio | 0..1 | Domicilio fiscal (`tipoDomicilio`="FISCAL") |
| `esSucesion` | xs:string | 0..1 | "Corresponde a persona física fallecida". Valores vistos: `NO` (y `SI` en el esquema del manual) |
| `estadoClave` | xs:string | 0..1 | `ACTIVO`, `INACTIVO` |
| `fechaContratoSocial` | xs:dateTime | 0..1 | Fecha del contrato social |
| `fechaFallecimiento` | xs:dateTime | 0..1 | Está en el WSDL y no en la tabla del manual. La v3.6 "incorpora el dato de persona fallecida en Datos Generales" (PDF p.2) |
| `idPersona` | xs:long | 0..1 | CUIT consultada |
| `mesCierre` | xs:int | 0..1 | Mes de cierre de balance |
| `nombre` | xs:string | 0..1 | Nombre |
| `razonSocial` | xs:string | 0..1 | Razón social |
| `tipoClave` | xs:string | 0..1 | `CUIT`, `CUIL`, `CDI` |
| `tipoPersona` | xs:string | 0..1 | `FISICA`, `JURIDICA` |

Subtipos (orden del WSDL, todo `minOccurs="0"`):

- `caracterizacion`: `descripcionCaracterizacion` (xs:string, según `SUPA.E_CARACTERIZACION`), `fechaSolicitud` (xs:**int**, AAAAMMDD, "Fecha de trámite"; nuevo en v4.1), `idCaracterizacion` (xs:int), `periodo` (xs:int, AAAAMMDD, "Período de validez para la caracterización"). PDF p.26.
- `dependencia`: `codPostal`, `descripcionDependencia`, `descripcionProvincia`, `direccion` (xs:string), `idDependencia`, `idProvincia` (xs:int), `localidad` (xs:string). Es más rica que la de A4 y A10 (PDF p.24-25).
- `domicilio` (tipo de `domicilioFiscal`): `codPostal` (xs:string), `datoAdicional`, `descripcionProvincia`, `direccion` (xs:string), `idProvincia` (xs:int), `localidad`, `tipoDatoAdicional` (tabla 5.2), `tipoDomicilio` (xs:string). PDF p.26.
- `datosRegimenGeneral`: `actividad` (0..*), `categoriaAutonomo` (tns:categoria, 0..1), `impuesto` (0..*), `regimen` (0..*). PDF p.24.
- `datosMonotributo`: `actividad` (0..*; la v2 trae todas las actividades), `actividadMonotributista` (tns:actividad, 0..1), `categoriaMonotributo` (tns:categoria, 0..1), `componenteDeSociedad` (tns:relacion, 0..*), `impuesto` (0..*). La v3.7 del manual "elimina la referencia a 'TIPO COMPONENTE - Componentes de la persona jurídica monotributista' por no estar vigente" (PDF p.2), pero el WSDL lo conserva.
- `actividad`: `descripcionActividad` (xs:string), `idActividad` (xs:long), `nomenclador` (xs:int: 883 en régimen general, 1 en la actividad monotributista del ejemplo), `orden` (xs:int: 1 = principal; 0 en la actividad monotributista del ejemplo), `periodo` (xs:int AAAAMM).
- `categoria`: `descripcionCategoria` (xs:string), `idCategoria` (xs:int), `idImpuesto` (xs:int), `periodo` (xs:int AAAAMM). **No tiene `estado`**, a diferencia de A4.
- `impuesto`: `descripcionImpuesto` (xs:string), `estadoImpuesto` (xs:string; "Identificador del estado del impuesto en SUPA (tipo_estado_impuesto)", ej. `AC`), `idImpuesto` (xs:int), `motivo` (xs:string; "Descripción del motivo del impuesto"), `periodo` (xs:int AAAAMM). `estadoImpuesto` y `motivo` son de la v3.8 (06/02/2026). **No tiene `estado` en texto, `diaPeriodo` ni `ffInscripcion`** (A4 sí).
- `regimen`: `descripcionRegimen`, `idImpuesto` (xs:int), `idRegimen` (xs:int), `periodo` (xs:int), `tipoRegimen` (xs:string, tabla 5.1). Sin `estado` ni `diaPeriodo`.
- `relacion` (tipo de `componenteDeSociedad`): `apellidoPersonaAsociada`, `ffRelacion` (xs:dateTime), `ffVencimiento` (xs:dateTime), `idPersonaAsociada` (xs:long), `nombrePersonaAsociada`, `razonSocialPersonaAsociada`, `tipoComponente` (xs:string).
- `errorConstancia`: `apellido` (xs:string), `error` (xs:string, nillable, **0..***), `idPersona` (xs:long), `nombre` (xs:string). PDF p.24.
- `errorRegimenGeneral` y `errorMonotributo`: `error` (xs:string, 0..*) y `mensaje` (xs:string). Los mensajes son fijos: "No cumple con las condiciones para enviar datos del regimen general" y "No cumple con las condiciones para enviar datos monotributo" (PDF p.24).

### dummy

Propósito: verifica aplicación, autenticación y base de datos (PDF p.6). Sin credenciales.

Request `<a5:dummy/>`. Response: `dummyResponse` → `return` → `appserver`, `authserver`, `dbserver` (`OK`/`ERROR`).

Ejemplo del manual (PDF p.7), request:

```xml
<soapenv:Envelope    xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a5="http://a5.soap.ws.server.puc.sr/">
<soapenv:Header/>
<soapenv:Body>
<a5:dummy/>
</soapenv:Body>
</soapenv:Envelope>
```

Respuesta real (2026-10-01, catálogo 4.1), HTTP 200: `<return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return>` dentro de `<ns2:dummyResponse xmlns:ns2="http://a5.soap.ws.server.puc.sr/">`.

### getPersona_v2

Propósito: "Devuelve el detalle de todos los datos, correspondientes a la constancia de inscripción, del contribuyente solicitado" (PDF p.8).

Request (`getPersona_v2`; todos 1..1):

| Elemento | Tipo | Occurs | Significado |
|---|---|---|---|
| `token` | xs:string | 1..1 | Token del TA |
| `sign` | xs:string | 1..1 | Firma |
| `cuitRepresentada` | xs:long | 1..1 | "CUIT del contribuyente emisor o representado"; debe estar en `relations` |
| `idPersona` | xs:long | 1..1 | "CUIT del cual se solicitan los datos" |

Response: `getPersona_v2Response` → `personaReturn` (ver Tipos compartidos).

Ejemplo del manual, request (PDF p.10):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a5="http://a5.soap.ws.server.puc.sr/">
  <soapenv:Header/>
  <soapenv:Body>
     <a5:getPersona_v2>
       <token>${tokensign#token}</token>
       <sign>${tokensign#sign}</sign>
       <cuitRepresentada>${parametros#cuit_representada}</cuitRepresentada>
      <idPersona>20164755100</idPersona>
     </a5:getPersona_v2>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta del ejemplo (PDF p.10-13, completa):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <ns2:getPersona_v2Response xmlns:ns2="http://a5.soap.ws.server.puc.sr/">
       <personaReturn>
          <datosGenerales>
            <apellido>SKIOGEK OGIUPE</apellido>
            <caracterizacion>
               <descripcionCaracterizacion>IVA ANUAL AGROPECUARIO</descripcionCaracterizacion>
               <fechaSolicitud>20030316</fechaSolicitud>
               <idCaracterizacion>17</idCaracterizacion>
               <periodo>19010101</periodo>
            </caracterizacion>
            <caracterizacion>
               <descripcionCaracterizacion>CUENTA CORRIENTE</descripcionCaracterizacion>
               <fechaSolicitud>20070901</fechaSolicitud>
               <idCaracterizacion>82</idCaracterizacion>
               <periodo>20070901</periodo>
            </caracterizacion>
            <caracterizacion>
               <descripcionCaracterizacion>CATEGORÍA A: MUY BAJO RIESGO</descripcionCaracterizacion>
               <fechaSolicitud>20161025</fechaSolicitud>
               <idCaracterizacion>354</idCaracterizacion>
               <periodo>20161025</periodo>
            </caracterizacion>
            <caracterizacion>
               <descripcionCaracterizacion>GANANCIAS SIMPLIFICADA LEY 27.779</descripcionCaracterizacion>
               <fechaSolicitud>20260220</fechaSolicitud>
               <idCaracterizacion>639</idCaracterizacion>
               <periodo>20250101</periodo>
            </caracterizacion>
            <domicilioFiscal>
               <codPostal>7007</codPostal>
               <datoAdicional>BARRIO ANTONIO CARLOS</datoAdicional>
               <descripcionProvincia>BUENOS AIRES</descripcionProvincia>
               <direccion>SAN MANUEL 9</direccion>
               <idProvincia>1</idProvincia>
               <localidad>SAN MANUEL</localidad>
               <tipoDatoAdicional>BARRIO</tipoDatoAdicional>
               <tipoDomicilio>FISCAL</tipoDomicilio>
            </domicilioFiscal>
            <esSucesion>NO</esSucesion>
            <estadoClave>ACTIVO</estadoClave>
            <idPersona>20164755100</idPersona>
            <mesCierre>12</mesCierre>
            <nombre>OGIEK KPSGR</nombre>
            <tipoClave>CUIT</tipoClave>
            <tipoPersona>FISICA</tipoPersona>
          </datosGenerales>
          <datosRegimenGeneral>
            <actividad>
               <descripcionActividad>CULTIVO DE CEREALES N.C.P., EXCEPTO LOS DE USO FORRAJERO</descripcionActividad>
               <idActividad>11119</idActividad>
               <nomenclador>883</nomenclador>
               <orden>1</orden>
               <periodo>201311</periodo>
            </actividad>
            <categoriaAutonomo>
               <descripcionCategoria>T3 CAT II INGRESOS DESDE $25.001</descripcionCategoria>
               <idCategoria>302</idCategoria>
               <idImpuesto>308</idImpuesto>
               <periodo>200703</periodo>
            </categoriaAutonomo>
            <impuesto>
               <descripcionImpuesto>GANANCIAS PERSONAS FISICAS</descripcionImpuesto>
               <estadoImpuesto>AC</estadoImpuesto>
               <idImpuesto>11</idImpuesto>
               <motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo>
               <periodo>199203</periodo>
            </impuesto>
            <impuesto>
               <descripcionImpuesto>IVA</descripcionImpuesto>
               <estadoImpuesto>AC</estadoImpuesto>
               <idImpuesto>30</idImpuesto>
               <motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo>
               <periodo>199911</periodo>
             </impuesto>
             <impuesto>
               <descripcionImpuesto>APORTES SEG.SOCIAL AUTONOMOS</descripcionImpuesto>
               <estadoImpuesto>AC</estadoImpuesto>
               <idImpuesto>308</idImpuesto>
               <motivo>INSCRIPCIÓN NO TRAMITADA EN AGENCIA</motivo>
               <periodo>200703</periodo>
             </impuesto>
          </datosRegimenGeneral>
          <metadata>
             <fechaHora>2026-02-26T15:52:37.653-03:00</fechaHora>
             <servidor>qa-sr-padron-ws.cloudhomo.afip.gob.ar</servidor>
          </metadata>
        </personaReturn>
     </ns2:getPersona_v2Response>
   </soap:Body>
</soap:Envelope>
```

### getPersonaList_v2

Propósito: "Devuelve idénticos datos que el método getPersona_v2, pero para una lista de hasta 250 claves tributarias" (PDF p.14).

Request (`getPersonaList_v2`):

| Elemento | Tipo | Occurs (WSDL) | Significado y límites |
|---|---|---|---|
| `token` | xs:string | 1..1 | Token |
| `sign` | xs:string | 1..1 | Firma |
| `cuitRepresentada` | xs:long | 1..1 | CUIT representada |
| `idPersona` | xs:long | 1..unbounded | CUIT a consultar, repetida. **Máximo 250** según el manual (el WSDL no lo limita). Qué pasa con 251 o más está NO VERIFICADO (con token malo, sale primero el error de token) |

Response: `getPersonaList_v2Response` → `personaListReturn` → `metadata` + un `persona` por clave pedida. Una clave inexistente no corta la lista: produce un `persona` que solo trae `errorConstancia` con `error` = "No existe persona con ese Id" e `idPersona` (PDF p.21). Que la lista respete el orden de los `idPersona` pedidos no se puede afirmar, aunque el ejemplo lo sugiere.

Ejemplo del manual, request (PDF p.16; token unido en una línea):

```xml
<soapenv:Envelope       xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a5="http://a5.soap.ws.server.puc.sr/">
<soapenv:Header/>
<soapenv:Body>
<a5:getPersonaList_v2>
       <token>PD94bWwgdmVylcnNpb249IjIuMCI+CiAgICA8aWQgc3JjPSJDTj13c2FhaG9tbywgTz1BRklQLCBNvPgo=</token>
<sign>Dtjsx5ddKZPm86zJO1mtpGEXFAMfF4p9xQQq9wjc3S04=</sign>
<cuitRepresentada>20140363605</cuitRepresentada>
<idPersona>20224107030</idPersona>
<idPersona>27015942210</idPersona>
<idPersona>12345678901</idPersona> <!--CUIT inexistente-->
</a5:getPersonaList_v2>
</soapenv:Body>
</soapenv:Envelope>
```

Respuesta del ejemplo (PDF p.16-21, completa):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <ns2:getPersonaList_v2Response xmlns:ns2="http://a5.soap.ws.server.puc.sr/">
       <personaListReturn>
          <metadata>
            <fechaHora>2018-11-27T12:03:48.839-03:00</fechaHora>
            <servidor>localhost</servidor>
          </metadata>
          <persona>
            <datosGenerales>
               <apellido>FRANCO AGUSTIN</apellido>
               <domicilioFiscal>
                 <codPostal>1828</codPostal>
                 <descripcionProvincia>BUENOS AIRES</descripcionProvincia>
                 <direccion>ALEM 5982</direccion>
                 <idProvincia>1</idProvincia>
                 <localidad>BANFIELD</localidad>
                 <tipoDomicilio>FISCAL</tipoDomicilio>
               </domicilioFiscal>
               <esSucesion>NO</esSucesion>
               <estadoClave>ACTIVO</estadoClave>
               <idPersona>20224107030</idPersona>
               <mesCierre>12</mesCierre>
               <nombre>SEVERINO</nombre>
               <tipoClave>CUIT</tipoClave>
               <tipoPersona>FISICA</tipoPersona>
            </datosGenerales>
            <datosRegimenGeneral>
               <actividad>
                 <descripcionActividad>CULTIVO DE TRIGO</descripcionActividad>
                 <idActividad>11112</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>9</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>CULTIVO DE MAÍZ</descripcionActividad>
                 <idActividad>11121</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>5</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>CULTIVO DE CEREALES DE USO FORRAJERO N.C.P.</descripcionActividad>
                 <idActividad>11129</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>8</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>CULTIVO DE PASTOS DE USO FORRAJERO</descripcionActividad>
                 <idActividad>11130</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>6</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>CULTIVO DE SOJA</descripcionActividad>
                 <idActividad>11211</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>7</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>CULTIVO DE GIRASOL</descripcionActividad>
                 <idActividad>11291</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>3</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>CULTIVO DE OLEAGINOSAS N.C.P. EXCEPTO SOJA Y GIRASOL</descripcionActividad>
                 <idActividad>11299</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>2</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>CULTIVO DE LEGUMBRES FRESCAS</descripcionActividad>
                 <idActividad>11341</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>4</orden>
                 <periodo>201311</periodo>
               </actividad>
               <actividad>
                 <descripcionActividad>SERVICIOS DE ASESORAMIENTO, DIRECCIÓN Y GESTIÓN EMPRESARIAL REALIZADOS POR INTEGRANTES DE CUERPOS DE DIRECCIÓN EN SOCIEDADES EXCEPTO LAS ANÓNIMAS</descripcionActividad>
                 <idActividad>702092</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>1</orden>
                 <periodo>201311</periodo>
               </actividad>
               <impuesto>
                 <descripcionImpuesto>GANANCIAS PERSONAS FISICAS</descripcionImpuesto>
                 <estadoImpuesto>AC</estadoImpuesto>
                 <idImpuesto>11</idImpuesto>
                 <motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo>
                 <periodo>200512</periodo>
               </impuesto>
               <impuesto>
                 <descripcionImpuesto>IVA</descripcionImpuesto>
                 <estadoImpuesto>AC</estadoImpuesto>
                 <idImpuesto>30</idImpuesto>
                 <motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo>
                 <periodo>200705</periodo>
               </impuesto>
               <impuesto>
                 <descripcionImpuesto>APORTES SEG.SOCIAL AUTONOMOS</descripcionImpuesto>
                 <estadoImpuesto>AC</estadoImpuesto>
                 <idImpuesto>308</idImpuesto>
                 <motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo>
                 <periodo>200512</periodo>
               </impuesto>
            </datosRegimenGeneral>
          </persona>
          <persona>
            <datosGenerales>
               <apellido>INTENTAR</apellido>
               <domicilioFiscal>
                 <codPostal>5881</codPostal>
                 <descripcionProvincia>SAN LUIS</descripcionProvincia>
                 <direccion>AV LOS INCAS 4137</direccion>
                 <idProvincia>11</idProvincia>
                 <localidad>MERLO</localidad>
                 <tipoDomicilio>FISCAL</tipoDomicilio>
               </domicilioFiscal>
               <esSucesion>NO</esSucesion>
               <estadoClave>ACTIVO</estadoClave>
               <idPersona>27015942210</idPersona>
               <mesCierre>12</mesCierre>
               <nombre>JAZMIN</nombre>
               <tipoClave>CUIT</tipoClave>
               <tipoPersona>FISICA</tipoPersona>
            </datosGenerales>
            <datosMonotributo>
               <actividadMonotributista>
                 <descripcionActividad>PREST. DE SERVICIO O LOCACIÓN</descripcionActividad>
                 <idActividad>8</idActividad>
                 <nomenclador>1</nomenclador>
                 <orden>0</orden>
                 <periodo>201803</periodo>
               </actividadMonotributista>
               <categoriaMonotributo>
                 <descripcionCategoria>B LOCACIONES DE SERVICIO</descripcionCategoria>
                 <idCategoria>36</idCategoria>
                 <idImpuesto>20</idImpuesto>
                 <periodo>201804</periodo>
               </categoriaMonotributo>
               <impuesto>
                 <descripcionImpuesto>MONOTRIBUTO</descripcionImpuesto>
                 <estadoImpuesto>AC</estadoImpuesto>
                 <idImpuesto>20</idImpuesto>
                 <motivo>Recategorización</motivo>
                 <periodo>201803</periodo>
               </impuesto>
            </datosMonotributo>
            <datosRegimenGeneral>
               <actividad>
                 <descripcionActividad>SERVICIOS DE ALOJAMIENTO EN HOTELES, HOSTERÍAS Y RESIDENCIALES SIMILARES, EXCEPTO POR HORA, QUE NO INCLUYEN SERVICIO DE RESTAURANTE AL PÚBLICO</descripcionActividad>
                 <idActividad>551023</idActividad>
                 <nomenclador>883</nomenclador>
                 <orden>1</orden>
                 <periodo>201311</periodo>
               </actividad>
               <impuesto>
                 <descripcionImpuesto>EMPLEADOR-APORTES SEG. SOCIAL</descripcionImpuesto>
                 <estadoImpuesto>AC</estadoImpuesto>
                 <idImpuesto>301</idImpuesto>
                 <motivo>Inscripción tramitada en agencia</motivo>
                 <periodo>200004</periodo>
               </impuesto>
            </datosRegimenGeneral>
          </persona>
          <persona>
                    <errorConstancia>
                              <error>No existe persona con ese Id</error>
                              <idPersona>12345678901</idPersona>
                    </errorConstancia>
          </persona>
        </personaListReturn>
     </ns2:getPersonaList_v2Response>
   </soap:Body>
</soap:Envelope>
```

Lo que muestran los dos ejemplos:

- Las actividades salen **ordenadas por `idActividad`**, no por `orden`: la principal (`orden` 1) va última en el primer `persona`.
- Los impuestos salen ordenados por `idImpuesto` (11, 30, 308). Como regla del servicio es **NO VERIFICADO**.
- Un monotributista que además es empleador trae **los dos bloques**, `datosMonotributo` (impuesto 20) y `datosRegimenGeneral` (impuesto 301, sin IVA).
- El texto de `motivo` no está normalizado en mayúsculas ("Recategorización", "Inscripción tramitada en agencia").
- `estadoImpuesto` es un código corto (`AC`). Los demás códigos de `tipo_estado_impuesto` no están en el manual (NO VERIFICADO).

### getPersona (legado)

Mismo request que `getPersona_v2` (`token`, `sign`, `cuitRepresentada`, `idPersona`, todos 1..1) y misma respuesta (`getPersonaResponse` → `personaReturn`). El manual 4.1 ya no la documenta aparte: "¿Y el método getPersona? Este sigue estando para conservar la compatibilidad con soluciones ya desarrolladas pero alentamos a la adopción de este nuevo método que incluye todas las actividades del monotributista y las caracterizaciones vigentes" (PDF p.13). Qué omite exactamente frente a la v2 (se infiere: `caracterizacion` y `datosMonotributo/actividad`) está **NO VERIFICADO**.

### getPersonaList (legado)

Mismo request que `getPersonaList_v2` (`idPersona` 1..unbounded) y misma respuesta (`getPersonaListResponse` → `personaListReturn`). Apareció en la v3.0 (27/11/18, PDF p.2). El límite de 250 lo dice el manual para la v2; que valga para la legada está NO VERIFICADO. Diferencias de contenido: como en `getPersona`.

## Validaciones y errores

Autenticación y capa SOAP (Fault, HTTP 500):

| Texto | Condición | Dónde aparece | Fuente |
|---|---|---|---|
| `Falta token y/o sign.` | Falta el elemento `token` o `sign` | `soap:Fault` `soap:Server` + `detail/ns1:SRValidationException` | En vivo 2026-10-01 y 2026-10-02 |
| `Token malformado` | Token no decodificable | `soap:Fault` `soap:Server` sin `detail` | En vivo |
| `No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token>` | Firma inválida | `soap:Fault` `soap:Server` sin `detail` | En vivo 2026-10-01 |
| `Unmarshalling Error: ...`, `Message part ... was not recognized.  (Does it exist in service WSDL?)`, `VersionMismatch` | Errores de CXF | `soap:Fault` `soap:Client` | Observado en A4 (`ws_sr_padron_a4.md`); acá, inferencia |

Errores de negocio. Van **dentro de una respuesta normal (HTTP 200)**, en `errorConstancia/error`, salvo los fijos de `errorRegimenGeneral/mensaje` y `errorMonotributo/mensaje`. Así lo muestra el ejemplo de CUIT inexistente y así lo definen los tipos (PDF p.24). El manual no dice en cuál de los tres bloques cae cada mensaje de la tabla 5.3. Que la mayoría vaya en `errorConstancia` es inferencia, y los que nombran monotributo podrían ir en `errorMonotributo/error` (NO VERIFICADO).

| Mensaje (texto exacto) | Condición (manual) | Dónde aparece | Fuente |
|---|---|---|---|
| `No existe persona con ese Id` | `idPersona` inexistente en `getPersonaList_v2` | `persona/errorConstancia/error` + `errorConstancia/idPersona` | Ejemplo, PDF p.21 |
| `No cumple con las condiciones para enviar datos del regimen general` | No se puede emitir la constancia de régimen general | `errorRegimenGeneral/mensaje` (con el detalle en `error`) | PDF p.24 |
| `No cumple con las condiciones para enviar datos monotributo` | No se puede emitir la de monotributo | `errorMonotributo/mensaje` | PDF p.24 |
| `La clave ingresada no es una CUIT` | El tipo de clave es distinto de C | `errorConstancia/error` (inferido) | Tabla 5.3, PDF p.30 |
| `La CUIT del contribuyente fue limitada en los términos de la RG AFIP 3832/16.` | Sin baja de oficio y clave inactiva por caracterizaciones de bloqueo 328 (Falta Inscripción en Impuestos o Regimenes), 329 (Falta de Presentación DDJJ), 330 (Falta Movimiento y Empleados en DDJJ) | ídem | PDF p.30 |
| `La CUIT del contribuyente fue limitada en los términos de la RG AFIP 3832/16. Motivo: - descripción de la caracterización -` | Sin baja de oficio y clave inactiva por caracterización 195 (Incluido en Base Contribuyentes NO Confiable) | ídem | PDF p.30 |
| `La CUIT fue cancelada de acuerdo a: - descripción de la caracterización -` | Una por cada caracterización: 108 CUIT Inactiva; 195 Incluido en Base Contribuyentes NO Confiable; 196 Baja proyecto productivo solicitada por Min. Desarrollo Soc.; 263 Cancelación CUIT p/Excl. pleno der.R.G. AFIP Nº 3640 - Art.9; 339 Partido Político - Reg. Caducidad S/Info Min. Del Interior; 340 Clave inactivada Renaper/Oficio Judicial; 342 COOP.EFECTORAS INACTIVADAS POR REQ.DEL MIN.DESARROLLO SOCIAL; 343 SOCIEDAD EN FORMACION - INACTIVADA POR INCUMPLIMIENTO; 344 COOPERATIVA INACTIVA INAES; 350 CLAVES INVALIDAS | ídem | PDF p.30 |
| `La CUIT fue cancelada de acuerdo a lo establecido en la RG 3358` | Tiene caracterización 160 (Cancelación de CUIT RG 3358) | ídem | PDF p.30 |
| `La CUIT que ingresaste se encuentra inactiva, ingresá tu CUIT activa` | Clave inactiva pero no bloqueada | ídem | PDF p.30 |
| `No tiene dependencia informada` | Dependencia nula o región de dependencia 0 | ídem | PDF p.30 |
| `PASIVO DECRETO 1299/98` | `tipo_bloqueo` distinto de nulo | ídem | PDF p.30 |
| `El contribuyente registra fecha de fallecimiento y no es sucesión indivisa` | Tiene fecha de fallecimiento y no es sucesión | ídem | PDF p.30 |
| `Documento erróneo` | Número de documento cero y persona física | ídem | PDF p.30 |
| `Nombre erróneo` | Nombre nulo y persona física | ídem | PDF p.30 |
| `Razón social errónea` | Razón social nula y persona jurídica | ídem | PDF p.30 |
| `Domicilio Incompleto` | Clave CUIT sin domicilio fiscal | ídem | PDF p.30 |
| `Código postal erróneo` | Domicilio fiscal con código postal nulo o cero | ídem | PDF p.30 |
| `Provincia errónea` | Domicilio fiscal con código de provincia nulo | ídem | PDF p.30 |
| `Si no tiene calle, debe tener Dato adicional` | Domicilio fiscal con código de calle nulo y dato adicional nulo | ídem | PDF p.31 |
| `No tiene localidad` | Domicilio fiscal con código de localidad nulo y código de provincia cero | ídem | PDF p.31 |
| `Estado erróneo del domicilio` | Domicilio fiscal con estado inexistente, no denunciado, no notificado o sin correspondencia con el tipo de domicilio | ídem | PDF p.31 |
| `No responde al requerimiento` | Caracterización 61 (NO RESPONDIO AL REQUERIMIENTO) | ídem | PDF p.31 |
| `Debe responder requerimientos pendientes en verificaciones` | Caracterización 463 (NO RESPONDIO AL REQUERIMIENTO – VERIFICA) | ídem | PDF p.31 |
| `Debe responder fiscalizaciones electrónicas pendientes` | Caracterización 483 (NO RESPONDIO AL REQUERIMIENTO - VERIFICACIÓN ELECTRÓNICA) | ídem | PDF p.31 |
| `No es posible mostrar la Constancia de Inscripción Opción de Monotributo, atento que el contribuyente consultado registra irregularidades en los relevamientos de personal efectuados por esta Administración Federal. El contribuyente o su representante podrán consultar las irregularidades detectadas en el relevamiento de personal accediendo con clave fiscal al servicio IDR` | Caracterización 321 (RELEVAMIENTO DE TRABAJADORES CON IRREGULARIDADES DETECTADAS) | `errorConstancia` o `errorMonotributo` (NO VERIFICADO) | PDF p.31 |
| `No consta en nuestros registros que Ud. ha cumplido con la adhesión al domicilio fiscal electrónico, según lo normado por la R.G. 3990-E, hasta tanto no normalice su situación no podrá visualizar su constancia.` | Sin domicilio fiscal electrónico, monotributista fuera de las categorías 61 (B MONOTRIBUTO SOCIAL AGROP.), 98 (B ASOCIADO COOPERATIVA), 99 (B MONOTRIBUTO SOCIAL LOCACION), 100 (B MONOTRIBUTO SOCIAL VENTAS), 101 (D 2 SOCIOS PROY. SERVICIOS), 102 (D 2 SOCIOS PROY. PRODUCTIVO), 103 (E 3 SOCIOS PROY. SERVICIOS), 104 (E 3 SOCIOS PROY. PRODUCTIVO), y sin caracterización 418 (EXCEPTUADO DE CONSTITUIR DFE) | ídem | PDF p.31 |
| `Tu Constancia se encuentra bloqueada. El 30-nov-2018 venció el plazo para constituir el domicilio fiscal electrónico. Constituilo para desbloquearla.` | Sin domicilio fiscal electrónico y NO monotributista | ídem | PDF p.31 |
| `Sr. Contribuyente de acuerdo a lo establecido en el Art. Nº 7 de la R.G. AFIP Nº 3537/13 para obtener la constancia de inscripción/opción, deberá realizar previamente la actualización de todas sus actividades económicas. A tales efectos ingrese al servicio Sistema Registral, opción Registro Tributario/ F. 420/D Declaración de actividades, y declare las actividades del nomenclador f. 883 que correspondan.` | No cumplió con el reempadronamiento de actividades económicas | ídem | PDF p.31-32 |
| `Datos de monotributo incompletos- no posee categoría.` | Registra impuesto 20 (Monotributo) pero no categoría | ídem | PDF p.32 |
| `La sociedad no posee la cantidad de integrantes requerida` | Las personas activas asociadas a la persona jurídica como integrantes no cumplen el mínimo de 2 y máximo de 3 | ídem | PDF p.32 |
| `Ud se encuentra bajo la modalidad de trabajador independiente promovido, no puede tener Locales/establecimientos..` | Trabajador independiente promovido con local o establecimiento de alta (`T_DOMICILIO id_tipo_domicilio = 3`) | ídem | PDF p.32 |
| `No puede estar inscripto en el/los impuestos 308/180/301 y simultáneamente como Trabajador Independiente Promovido del régimen de Monotributo` | Trabajador independiente promovido con impuestos activos 308 (autónomo), 180 (Bs Ps) o 301 (empleador) | ídem | PDF p.32 |
| `No puede tener más de una actividad pues Ud es Trabajador Independiente Promovido.` | Trabajador independiente promovido con más de una actividad con código > 100 | ídem | PDF p.32 |
| `Para obtener la constancia de inscripción/opción, deberá realizar previamente la actualización de todas sus actividades económicas.` | La actividad principal no es del nomenclador vigente | ídem | PDF p.32 |
| `Actividad económica principal inexistente` | Código de actividad nulo | ídem | PDF p.32 |
| `Mes de cierre declarado es invalido` | Mes de cierre menor que 1 o mayor que 12 | ídem | PDF p.32 |
| `La sociedad no registra forma jurídica` | Persona jurídica con forma jurídica nula | ídem | PDF p.32 |
| `El contribuyente cuenta con impuestos con baja de oficio por Decreto 1299/98','14 - Baja motivo 163` (así, con el `','` en el medio) | No monotributista, forma jurídica distinta de 95 COOPERATIVA EFECTORA, 108 ECONOMIA MIXTA, 116 EMPRESA DEL ESTADO, 124 CON PARTICIPACION ESTATAL MAYORITARIA, 125 ORGAN. PUBLICO, 126 ORGAN. PUBLICO INTERNACIONAL, 175 DIRECCION ADMINISTRATIVA ESTATAL, 245 SOCIEDAD BINACIONAL FUERA DE JURISDICCION, 246 ENTIDADES DE DERECHO PUBLICO NO ESTATAL, y con baja de oficio por decreto 1299/98 en algún impuesto | ídem | PDF p.32 |
| `El contribuyente no cuenta con impuestos activos y posee impuestos con baja de oficio por Decreto 1299/98` | Igual que la anterior, pero además sin impuestos activos | ídem | PDF p.32-33 |
| `La constancia de inscripción se encuentra bloqueada porque el contribuyente, o su Administrador de Relaciones, no registró los datos biométricos. Puede hacerlo desde la aplicación ARCA, o en una dependencia de ARCA con un turno previo.` | Inscriptos desde el 26/05/2010 no inscriptos por CUIT-DIGITAL; monotributistas fuera de las categorías 61, 99, 100, 101, 102, 103, 104; sin baja de oficio por RG 3402; sin datos biométricos | ídem | PDF p.33 |
| `La CUIT del contribuyente fue limitada en los terminos de la RG AFIP 4320/18. Motivo:` + descripción de las caracterizaciones | Caracterizaciones 412 (Limitado por solicitud de CUIT digital observada. DNI), 413 (... Foto y DNI) o 414 (... Domicilio) | ídem | PDF p.33 |

Los textos con salto de línea en el PDF se unieron con un espacio. Los separadores exactos (espacios dobles, saltos) están NO VERIFICADOS.

## Tablas y datos

5.1 Valores TipoRegimen (PDF p.28):

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

5.2 Valores TipoDatoAdicional (PDF p.29): BARRIO, PARAJE, NO DETERMINADO, ESTAFETA, ENTRE LAS CALLES:, ESQUINA, SITIO WEB.

Tipos simples (PDF p.22): CUIT long de 11 dígitos; TipoPersona `FISICA`/`JURIDICA`; TipoClave `CUIT`, `CUIL`, `CDI`; EstadoClave `ACTIVO`, `INACTIVO`; TipoRegimen tabla 5.1; TipoDomicilio `FISCAL`, `LEGAL/REAL`; TipoDatoAdicional tabla 5.2.

Caracterización del Régimen de Declaración Jurada Simplificada de Ganancias, según el art. 2 del anexo del Dto 93/26 (PDF p.26-27): `idCaracterizacion` 639; `descripcionCaracterizacion` "GANANCIAS SIMPLIFICADA LEY 27.779"; `periodo` "el período fiscal desde el cual se ejerció la opción de adhesión"; `fechaSolicitud` "la fecha de ejercicio de la opción de adhesión".

Códigos que aparecen en el manual (semilla para la base ficticia; no son tablas completas):

- Impuestos: 11 GANANCIAS PERSONAS FISICAS, 20 MONOTRIBUTO, 30 IVA, 180 (Bs Ps), 301 EMPLEADOR-APORTES SEG. SOCIAL, 308 APORTES SEG.SOCIAL AUTONOMOS.
- `estadoImpuesto`: `AC`.
- Caracterizaciones: 17 IVA ANUAL AGROPECUARIO, 82 CUENTA CORRIENTE, 354 CATEGORÍA A: MUY BAJO RIESGO, 639 GANANCIAS SIMPLIFICADA LEY 27.779, y las de bloqueo o cancelación de la tabla de errores (61, 108, 160, 195, 196, 263, 321, 328, 329, 330, 339, 340, 342, 343, 344, 350, 412, 413, 414, 418, 463, 483).
- Categorías: autónomos 302 "T3 CAT II INGRESOS DESDE $25.001" (impuesto 308); monotributo 36 "B LOCACIONES DE SERVICIO" (impuesto 20) y las 61, 98-104 de la tabla de errores.
- Actividad monotributista: 8 "PREST. DE SERVICIO O LOCACIÓN", nomenclador 1.
- Formas jurídicas: 95, 108, 116, 124, 125, 126, 175, 245, 246 (tabla de errores).
- Provincias: 1 BUENOS AIRES, 11 SAN LUIS.

CUITs de los ejemplos: 20164755100 (régimen general con caracterizaciones), 20224107030 (régimen general con 9 actividades), 27015942210 (monotributo B + empleador; es la misma persona del ejemplo de A13), 12345678901 (inexistente). Cuáles existen en homologación: **NO VERIFICADO**. El manual no trae archivo de datos de prueba; el de A4 está en `ws_sr_padron_a4.md`.

Condición frente al IVA: el servicio no la devuelve como campo. Hay que deducirla del `impuesto` 30 activo en `datosRegimenGeneral` o del 20 en `datosMonotributo` (catálogo 5.1). La regla para exentos y no alcanzados es NO VERIFICADA.

## Comportamiento a simular

- Sin estado. Las 4 operaciones de consulta leen el mismo padrón ficticio que A4, A10 y A13.
- `getPersona_v2(x)` = `getPersonaList_v2([x]).persona[0]` + `metadata`. Así lo dice el manual: "idénticos datos".
- Las versiones legadas devuelven la misma estructura con menos contenido (sin caracterizaciones; en monotributo, sin la lista completa de `actividad`). Es inferencia.
- Una constancia puede salir con datos **y** errores a la vez: por ejemplo `datosGenerales` + `errorRegimenGeneral` si no puede emitir la de régimen general. Ningún ejemplo del manual combina los dos; NO VERIFICADO.
- `getPersonaList_v2`: un `persona` por cada `idPersona`, y para el inexistente solo `errorConstancia` (`error` + `idPersona`). Validar el máximo de 250. El error para más de 250 es NO VERIFICADO; elegir un Fault `SRValidationException` y documentarlo como decisión propia.
- `getPersona_v2` con CUIT inexistente: NO VERIFICADO si es Fault o `errorConstancia`. Por coherencia con la lista, `errorConstancia` en HTTP 200; pero A13, del mismo stack, documenta "La Clave (CUIT/CUIL) consultada es inexistente", que parece Fault.
- Serialización: orden alfabético del WSDL; `metadata` al final en `personaReturn` y al principio en `personaListReturn`; actividades e impuestos ordenados por id; campos vacíos omitidos.
- Aceptar TA de `ws_sr_constancia_inscripcion` y, si se decide, también de `ws_sr_padron_a5`.

## No verificado

- Si WSAA sigue emitiendo TA para `ws_sr_padron_a5`.
- `getPersona_v2` con CUIT inexistente: Fault o `errorConstancia`.
- En qué bloque (`errorConstancia`, `errorRegimenGeneral/error`, `errorMonotributo/error`) cae cada mensaje de la tabla 5.3, y si una misma respuesta combina datos y errores.
- El límite de 250 (texto y transporte del error) y si aplica a `getPersonaList`.
- Diferencias reales de contenido entre `getPersona`/`getPersonaList` y sus v2.
- Los códigos de `estadoImpuesto` distintos de `AC`.
- La regla de condición frente al IVA para exentos y no alcanzados.
- Si `esSucesion` usa `SI`/`NO` y si `fechaFallecimiento` viene informada.
- Si `componenteDeSociedad` sigue saliendo (el manual lo dio de baja en v3.7 y el WSDL lo mantiene).
- Textos de falla de TA vencido, TA de otro service id y `cuitRepresentada` fuera de `relations`.
