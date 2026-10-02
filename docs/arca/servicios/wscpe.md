# wscpe

Carta de Porte Electrónica (CPE). Es el comprobante que acompaña el traslado de granos y derivados granarios por camión, tren o ducto. Quien despacha la mercadería pide la autorización de la carta de porte, ARCA le asigna un número de CTG y después los distintos intervinientes la van moviendo por sus estados: el destino confirma el arribo, la rechaza o la confirma en forma definitiva; el solicitante puede anularla, informar una contingencia, desviarla o cambiarle el destino. Lo usan acopios, cerealeras, plantas industriales, productores y transportistas. Reemplazó a la carta de porte en papel y al viejo `wsctg` (ver catalogo.md, sección 3.3).

Hay seis familias de CPE y cada una tiene su juego de operaciones: **Automotor** (granos por camión), **Ferroviaria** (granos por tren) y cuatro de **Derivados Granarios (DG)**: Automotor DG, Ferroviaria DG, Emitida en Destino DG y Ductos DG. Las operaciones de transición (confirmar arribo, anular, rechazar, informar o cerrar contingencia, descargado en destino) son en su mayoría comunes a todas las familias. En cambio, autorizar, consultar, desviar, cambiar de destino, regresar a origen, confirmar en forma definitiva y editar tienen una variante por familia.

Manual: "Carta de Porte Electrónica - Web Service CpeService - Manual para el Desarrollador", **versión 2.2.1, revisión 4.7.20 (22/07/2026)**, 275 páginas. URL: https://www.afip.gob.ar/ws/documentos/manual-wscpe.pdf. Copia local en `SCR\manuales\wscpe.pdf` y `.txt`. En este documento las páginas que se citan son las impresas, que coinciden con las del PDF.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Java, tipo Apache CXF** (no JAX-WS RI). Evidencia observada el 2026-10-02: el sobre de respuesta usa el prefijo `soap:` (JAX-WS RI usa `S:`), hay un `soap:Header` propio, el error de esquema tiene el formato `Unmarshalling Error: cvc-...` y un SOAPAction desconocido devuelve "The given SOAPAction x does not match an operation.". Los dos mensajes son típicos de CXF; decir que es CXF es una inferencia. Los ejemplos del manual usan `S:Envelope`, que corresponde a una versión anterior. |
| Endpoint homologación | `https://cpea-ws-qaext.afip.gob.ar/wscpe/services/soap` (`soap:address` del WSDL; el manual dice lo mismo en la sección 2.2, Tabla 1, pág. 10-11) |
| Endpoint producción | `https://cpea-ws.afip.gob.ar/wscpe/services/soap` (`soap:address` del WSDL de producción; igual en el manual) |
| Host | `cpea-ws-qaext` / `cpea-ws`, **no** `fwshomo`/`servicios1`. Delante hay un F5: las respuestas traen las cookies `f5avraaaaaaaaaaaaaaaa_session_` y `TS01e00ac4`. A diferencia de fwshomo, los 500 llegan con el Fault intacto. |
| targetNamespace | `https://serviciosjava.afip.gob.ar/wscpe/` (**https**). Varios ejemplos del manual usan `http://serviciosjava.afip.gob.ar/wscpe/` (secciones 2.3, 2.4, 2.5 y 2.10.1-2.10.4); a partir de 2.10.5 usan https. El simulador sigue al WSDL. |
| WSDL | `wsdl:definitions name="wscpe"`, service `CpeService`, port `CpeEndPoint`, binding `wscpeSOAP`, portType `CpePortType` |
| Archivo guardado | `docs/arca/wsdl/wscpe-homologacion.wsdl`, autocontenido (sin `wsdl:import`, `xsd:import` ni `xsd:include`). El de producción solo difiere en `soap:address` (diff del 2026-10-02). |
| WSAA service id | `wscpe`. Fuente: el manual, sección 2.3 (pág. 11): "se debe dar de alta la solicutud para utilizar el servicio wscpe en el sistema Autogestión de certificados". |
| SOAPAction | `https://serviciosjava.afip.gob.ar/wscpe/<operación>` (targetNamespace + nombre de la operación, sin separador extra). Se verificó para las 75. Con un SOAPAction desconocido, el servidor responde con un Fault (ver Errores). |
| Versión SOAP | Solo 1.1 (un único binding `soap:`), document/literal |
| elementFormDefault | No declarado, o sea **unqualified**: solo el elemento raíz del body (`ns:XxxReq`) lleva namespace; `auth`, `solicitud` y todos los hijos van sin namespace. Las respuestas también: `<ns2:DummyResp xmlns:ns2="..."><respuesta>...` |
| Operaciones | **75** en el WSDL. El manual tiene 77 secciones de método (2.10.1 a 2.10.77); las diferencias están en "No verificado". |
| Fault declarado | Todas las operaciones declaran `wsdl:fault name="Exception"`, cuyo elemento es `tns:Exception` (`ExceptionType`: `uuid`, `timestamp` dateTime, `businessErrorId`, `exceptionDetails`, `serverName`, todos obligatorios). En ninguna captura apareció dentro de un `detail`. |
| Nombres de elementos | En general la entrada es `<Op con mayúscula>Req` y la salida `<...>Resp`. Las excepciones son: `dummy` (sin entrada, sale `DummyResp`), `editarCPEDGConfirmadaAutomotor` (`EditarCPEConfirmadaAutomotorDgReq`/`Resp`), `editarCPEDGConfirmadaFerroviaria` (`EditarCPEConfirmadaFerroviariaDgReq`/`Resp`) y `consultarRenspa` (sale **`ConsultarRenspaRes`**, sin "p"). La operación `consultarCPEPPendientesDeResolucion` lleva la doble "P" en el WSDL (el manual escribe `consultarCPEPendientesDeResolucion` en el título y la doble P en el XML). |

### Forma de la respuesta observada (2026-10-02)

Todas las respuestas, también los Fault, traen este header (sin namespace en los hijos):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Header>
    <serverTime>02/10/2026 15:08:16 GMT-03:00</serverTime>
    <serverName>cpea-ws-deployment-c94f6d46c-h847l</serverName>
    <elapsedTime>1790964496465ms</elapsedTime>
    <id>20261002030816472vncblMjuQIKfZcbdAOqdPaxsjbmPUYiE</id>
  </soap:Header>
  <soap:Body>
    <ns2:DummyResp xmlns:ns2="https://serviciosjava.afip.gob.ar/wscpe/">
      <respuesta><appserver>Ok</appserver><authserver>Ok</authserver><dbserver>Ok</dbserver></respuesta>
    </ns2:DummyResp>
  </soap:Body>
</soap:Envelope>
```

- HTTP: `HTTP/1.1 200 ` y `HTTP/1.1 500 `, **sin reason phrase**. `Content-Type: text/xml;charset=UTF-8`, `Strict-Transport-Security: max-age=15724800; includeSubDomains`. Las operaciones con auth agregan `Set-Cookie: JSESSIONID=...; Path=/; Secure; HttpOnly`.
- `serverTime`: `dd/MM/yyyy HH:mm:ss GMT-03:00`.
- `id`: `yyyyMMddhhmmssSSS` con la hora en formato de **12 h** (a las 15:08 aparece "03") seguido de 32 letras al azar. Esto se dedujo de las capturas; no está documentado.
- `elapsedTime`: a veces un valor razonable (`63ms`, `9ms`) y a veces uno absurdo, del orden del epoch en ms (`1790964496465ms`), que se vio en `dummy` y en los Fault de esquema y de SOAPAction. También es comportamiento observado, no documentado.
- Valores del `dummy`: `Ok`, no `OK`. El manual (pág. 24) muestra `OK` y además escribe el elemento como `dummyResp` en el esquema y `DummyResp` en el ejemplo; vale el WSDL (`DummyResp`).
- Las respuestas de negocio llevan además `respuesta/metadata/{servidor, fechaHora}` (sección 2.5; en el ejemplo de la pág. 27, `<servidor>pecuaria-ws-desa</servidor><fechaHora>2016-11-17T12:00:39</fechaHora>`). `dummy` no lo trae.

## Autenticación

- Contenedor: elemento **`auth`** (tipo `Auth`), primer hijo de cada `XxxReq`, sin namespace. Hijos en este orden: **`token`** (string), **`sign`** (string) y **`cuitRepresentada`** (tipo `CUIT` = `xsd:long` entre 10000000000 y 99999999999, o sea 11 dígitos). Los tres son obligatorios (1..1).
- Se usa en las **74 operaciones** que no son `dummy`. El body de `dummy` va vacío (su mensaje de entrada no tiene parts).
- Contradicción: en la sección 2.3, en los ejemplos de 2.10.2 a 2.10.4 y en la tabla `Auth` de la sección 3.2 (pág. 216-217), el manual llama `<cuit>` al tercer hijo. El WSDL y los ejemplos desde 2.10.5 usan `cuitRepresentada`. Vale el WSDL: con `<cuit>` el request no valida contra el esquema.
- El manual no documenta los errores de autenticación. Lo que sigue se **observó** contra homologación el 2026-10-02 (`consultarProvincias`); todas las respuestas fueron HTTP 500 con `soap:Fault`, sin `detail`:

| Caso | `faultcode` | `faultstring` exacto |
|---|---|---|
| `token`="abc", `sign`="abc" | `soap:Client` | `Error en la autenticacion: Token invalido` |
| `token` y `sign` vacíos | `soap:Client` | `Error en la autenticacion: Token o Sign nulos` |
| `token` = XML de TA bien formado en base64 (dst `CN=wscpe`), `sign`="YWJj" | `soap:Client` | `Error en la autenticacion: El sign no se conrresponde con el token` (sic, "conrresponde") |
| Sin el elemento `auth` | `soap:Client` | `Error en la validacion del esquema XML: [Unmarshalling Error: cvc-complex-type.2.4.b: The content of element 'ns:ConsultarProvinciasReq' is not complete. One of '{auth}' is expected.]` |

El nombre del elemento en el mensaje de esquema repite el prefijo que mandó el cliente (`ns:`). No se observaron los casos de token vencido, CUIT no autorizada ni servicio equivocado en el TA.

Request mínimo válido según el esquema:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:ns="https://serviciosjava.afip.gob.ar/wscpe/">
  <soapenv:Header/>
  <soapenv:Body>
    <ns:ConsultarUltNroOrdenReq>
      <auth>
        <token>PD94bWwg...</token>
        <sign>abc...</sign>
        <cuitRepresentada>20111111112</cuitRepresentada>
      </auth>
      <solicitud>
        <sucursal>1</sucursal>
        <tipoCPE>74</tipoCPE>
      </solicitud>
    </ns:ConsultarUltNroOrdenReq>
  </soapenv:Body>
</soapenv:Envelope>
```

Header HTTP: `SOAPAction: "https://serviciosjava.afip.gob.ar/wscpe/consultarUltNroOrden"`, `Content-Type: text/xml; charset=utf-8`.

## Operaciones

Clave de una CPE (tipo `CartaPorte`): **`cartaPorte{tipoCPE, sucursal, nroOrden}`**. La usan todas las transiciones salvo las ediciones. Los `consultarCPE*` aceptan esa clave **o** `nroCTG` (los dos son opcionales en el esquema; la tabla `ConsultarFerroviariaSolicitud` del manual, pág. 225, dice "por datos de carta de porte o N.º de CTG"). Las ediciones (`editar*`) se identifican **solo por `nroCTG`**. Las autorizaciones devuelven `cabecera{tipoCartaPorte, sucursal, nroOrden, nroCTG, fechaEmision, estado, fechaInicioEstado, fechaVencimiento, observaciones, anulacionMotivo, anulacionObservaciones}` más el detalle completo y `pdf`.

En la tabla: "CPR" = respuesta `CartaPorteRespuesta` (`cabecera?`, `pdf?`, `errores?`, `metadata?`). "Detalle" = `Detalle<Familia>Respuesta` (`cabecera`, `origen`, `intervinientes`, `datosCarga`, `destino`, `destinatario`, `transporte` (salvo Ductos), `pdf`, `errores`, `metadata`). Las transiciones de estado vienen de los diagramas de la sección 2.7.2 (pág. 15-17) y de la tabla 2.9 (pág. 18-23); ver "Comportamiento a simular".

### Servicio y parámetros

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Estado de app, auth y base | Body vacío | `DummyResp/respuesta{appserver, authserver, dbserver}` | — |
| `consultarProvincias` | Provincias | solo `auth` | `provincia*{codigo, descripcion}` | consulta tabla de parámetros. El ejemplo de la pág. 25-27 trae 24 provincias, códigos 0-24 sin el 15 (0 = CAP.FEDERAL, 1 = BUENOS AIRES ... 24 = TIER.DEL FUEGO) |
| `consultarLocalidadesPorProvincia` | Localidades de una provincia | `solicitud{codProvincia}` | `localidad*{codigo, descripcion}` | consulta tabla de parámetros |
| `consultarLocalidadesProductor` | Localidades declaradas por un productor | `solicitud{cuit}` | `localidad*` (la descripción incluye el ID de provincia, pág. 57) | consulta tabla de parámetros |
| `consultarTiposGrano` | Granos permitidos | solo `auth` | `grano*{codigo, descripcion}` | consulta tabla de parámetros |
| `consultarDerivadosGranarios` | Derivados granarios | solo `auth` | `derivadoGranario*` | consulta tabla de parámetros |
| `consultarTiposEmbalaje` | Tipos de embalaje (DG) | solo `auth` | `tipoEmbalaje*` | consulta tabla de parámetros |
| `consultarUnidadesMedida` | Unidades de medida (DG) | solo `auth` | `unidadMedida*` | consulta tabla de parámetros |
| `consultarCategoriasSemillas` | Categorías de semilla (v2.2.0) | solo `auth` | `categoria*` | consulta tabla de parámetros |
| `consultarVariedadesSemillas` | Variedades de semilla por grano (v2.2.0) | `codGrano?` **suelto, sin `solicitud`** | `variedad*` | consulta tabla de parámetros |
| `consultarDomiciliosPorCUIT` | Domicilios PUC de una CUIT | `cuit` **suelto, sin `solicitud`** | `domicilio*` | consulta tabla de parámetros |
| `consultarPlantas` | Plantas activas de una CUIT (granos) | `solicitud{cuit}` | `planta*{nroPlanta, codProvincia, codLocalidad, latitud, longitud, ubicacionGeoreferencial}` | consulta tabla de parámetros |
| `consultarPlantasDG` | Plantas de derivados granarios | `solicitud{cuit}` | `planta*` | consulta tabla de parámetros |
| `consultarRenspa` | RENSPA vigentes por CUIT y provincia (v2.0.6) | `cuit`, `codProvincia` **sueltos** | `ConsultarRenspaRes/respuesta/renspa*{nroRenspa, descripcion}` | consulta tabla de parámetros |
| `consultarUltNroOrden` | Último número de orden autorizado | `solicitud{sucursal, tipoCPE}` | `nroOrden?` (long) | consulta el contador por CUIT, sucursal y tipo. Ver Comportamiento |

### Autorización (crean la CPE)

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `autorizarCPEAutomotor` | Nueva CPE automotor de granos | `cabecera{tipoCP, cuitSolicitante, sucursal, nroOrden}`, `origen`, `correspondeRetiroProductor`, `esSolicitanteCampo`, `retiroProductor?`, `intervinientes?`, `datosCarga`, `destino`, `destinatario`, `transporte`, `observaciones?` | Detalle Automotor + `pdf` | **Crea** la CPE en AC y asigna `nroCTG`. `tipoCP` 74 (o 274, flete corto). Se lee con `consultarCPEAutomotor` |
| `autorizarCPEFerroviaria` | Nueva CPE ferroviaria de granos | `cabecera{sucursal, nroOrden, planta}` (**sin tipo**), `correspondeRetiroProductor`, `retiroProductor?`, `intervinientes?`, `datosCarga`, `destino`, `destinatario`, `transporte`, `observaciones?` | Detalle Ferroviaria + `pdf` | **Crea** la CPE en AC. El tipo queda implícito (75). Se lee con `consultarCPEFerroviaria` |
| `autorizarCPEAutomotorDG` | Nueva CPE automotor DG | `cabecera{tipoCP, sucursal, nroOrden}`, `origen`, `intervinientes?`, `datosCarga`, `destino`, `destinatario`, `transporte`, `observaciones?` | Detalle AutomotorDG + `pdf` | **Crea** (284). Estado inicial AC, o PE si la pide un usuario de industria (diagrama DG). Se lee con `consultarCPEAutomotorDG` |
| `autorizarCPEFerroviariaDG` | Nueva CPE ferroviaria DG | `cabecera{sucursal, nroOrden}` (**sin tipo**), `origen`, `intervinientes?`, `datosCarga`, `destino`, `destinatario`, `transporte`, `observaciones?` | Detalle FerroviariaDG + `pdf` | **Crea** (285 implícito). AC o PE. Se lee con `consultarCPEFerroviariaDG` |
| `autorizarCPEEmisionDestinoDG` | Nueva CPE DG emitida por el destino | `cabecera{tipoCP, sucursal, nroOrden}` (tipo `CabeceraAutomotorDGSolicitud`), `origen`, `intervinientes?`, `datosCarga`, `destino`, `destinatario`, `transporte`, `observaciones?` | Detalle EmisionDestinoDG + `pdf` | **Crea** (286) en PO, pendiente de que acepte el origen. Se lee con `consultarCPEEmisionDestinoDG` |
| `autorizarCPEDuctosDG` | Nueva CPE DG por ducto | `cabecera{sucursal, nroOrden}` (**sin tipo**), `origen`, `intervinientes` (obligatorio), `datosCarga`, `destino`, `destinatario`, `observaciones?` (sin transporte) | Detalle DuctosDG + `pdf` | **Discontinuado** desde v2.1.0 (13/02/2026, pág. 274): sigue en el WSDL. Se lee con `consultarCPEDuctos` |

### Consultas de CPE

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `consultarCPEAutomotor` | Detalle de una CPE automotor | `cuitSolicitante?`, `cartaPorte?{tipoCPE, sucursal, nroOrden}`, `nroCTG?` | Detalle Automotor + `pdf` | Lee lo que creó `autorizarCPEAutomotor`, por clave o por CTG |
| `consultarCPEFerroviaria` | Detalle de una CPE ferroviaria | igual que la anterior | Detalle Ferroviaria + `pdf` | Lee `autorizarCPEFerroviaria`. El ejemplo Java de la sección 2.6 consulta `tipoCPE=75, sucursal=171, nroOrden=5` |
| `consultarCPEAutomotorDG` | Detalle de una automotor DG | `cartaPorte?`, `cuitSolicitante?`, `cuitTitularPlanta?`, `nroCTG?` | Detalle AutomotorDG + `pdf` | Lee `autorizarCPEAutomotorDG` |
| `consultarCPEFerroviariaDG` | Detalle de una ferroviaria DG | igual que la anterior | Detalle FerroviariaDG + `pdf` | Lee `autorizarCPEFerroviariaDG` |
| `consultarCPEEmisionDestinoDG` | Detalle de una emitida en destino | igual que la anterior | Detalle EmisionDestinoDG + `pdf` | Lee `autorizarCPEEmisionDestinoDG` |
| `consultarCPEDuctos` | Detalle de una de ductos | `cartaPorte?`, `cuitSolicitante?`, `nroCTG?` | Detalle DuctosDG + `pdf` | Lee `autorizarCPEDuctosDG` |
| `consultaCPEFerroviariaPorNroOperativo` | Resumen de las CPE de un operativo ferroviario (solo transportistas) | `solicitud{nroOperativo}` | `fecha`, `cuitTransportista`, `cuitTransportistaTramo2`, `nroOperativo`, `cartaPorte*{nroCTG, nroVagon, grano, nroPrecinto*, pesoBruto, pesoTara}`, `pdf` | Lista las CPE ferroviarias con ese `transporte/nroOperativo` |
| `consultarCPEPorDestino` | CPE que tienen como destino una planta | `solicitud{planta, fechaPartidaDesde, fechaPartidaHasta (xsd:date), tipoCartaPorte?}` | `cartaPorte*{tipoCartaPorte, nroCTG, fechaPartida, estado, fechaUltimaModificacion}` | Lista por fecha de partida. Error 2152: rango de 3 días como máximo |
| `consultarCPEPPendientesDeResolucion` | CPE pendientes de resolución | `solicitud{perfil (S=solicitante, D=destino), planta?}` | igual que la anterior | Lista |
| `consultarCPEDGPendienteActivacion` | CPE DG en estado PE de una planta | `solicitud{planta}` | `cartaPorte*{tipoCartaPorte, sucursal, nroOrden, cuitSolicitante, fechaPartida}` | Lista las PE (ferroviaria y automotor DG) |
| `consultarCPEEmitidasDestinoDGPendientesActivacion` | Emitidas en destino en estado PO | `solicitud{planta}` | igual que la anterior | Lista las PO |

### Transiciones (todas devuelven CPR: `cabecera` con el nuevo `estado`, y `pdf`)

| operación | propósito | entrada principal (además de `cartaPorte{tipoCPE, sucursal, nroOrden}`) | transición (diagramas pág. 15-17) |
|---|---|---|---|
| `confirmarArriboCPE` | El destino confirma el arribo | `cuitSolicitante` | AC → CF. Sirve para todas las familias (tabla 2.9) |
| `descargadoDestinoCPE` | El solicitante indica que la mercadería "ha sido enviada" (descripción del manual, pág. 58) | `cuitSolicitante` | AC → DD. Después, "Confirmación de descarga en destino" lleva DD → CF; no está documentado con qué operación. No aplica a Ductos |
| `descargadoDestinoCPEEmisionDestinoDG` | Igual que la anterior, para emitidas en destino | `cuitSolicitante`, `cuitDestino` (antes que `cartaPorte`) | AC → DD |
| `rechazoCPE` | El destino rechaza la CPE | `cuitSolicitante`, `rechazoMotivo?` (1 producto equivocado, 2 calidad, 3 otro), `rechazoObservaciones?` (Texto100) | CF → RE ("Rechazo CUIT Destino") |
| `confirmacionDefinitivaCPEAutomotor` | Cierre con los pesos de descarga | `cuitSolicitante`, `intervinientes?`, `pesoBrutoDescarga`, `pesoTaraDescarga` | CF → CN |
| `confirmacionDefinitivaCPEFerroviaria` | Igual, ferroviaria | `cuitSolicitante`, `intervinientes?`, `destinatario?`, `pesoBrutoDescarga`, `pesoTaraDescarga`, `ramalDescarga` | CF → CN |
| `confirmacionDefinitivaCPEAutomotorDG` | Igual, automotor DG y emitida en destino | `cuitSolicitante`, `pesoBrutoDescarga`, `pesoTaraDescarga` | CF → CN |
| `confirmacionDefinitivaCPEFerroviariaDG` | Igual, ferroviaria DG | `cuitSolicitante`, `pesoBrutoDescarga`, `pesoTaraDescarga`, `ramalDescarga` | CF → CN |
| `confirmacionDefinitivaCPEDuctosDG` | Igual, ductos | `cuitSolicitante`, `pesoBrutoDescarga` | CF → CN |
| `anularCPE` | El solicitante anula | `anulacionMotivo?` (1 siniestro, 2 pérdida de la carga, 3 otro), `anulacionObservaciones?` | AC → AN. Errores 2121 (plazo vencido), 2141, 2239 (tope de anuladas) |
| `anularCPEEmisionDestinoDG` | Igual, emitida en destino | `cuitDestino`, `anulacionMotivo?`, `anulacionObservaciones?` | AC → AN |
| `informarContingencia` | Alta de contingencia (siniestro, desperfecto, demora) | `contingencia{concepto (MotivoContingencia A-G), descripcion?}` | AC → CO |
| `informarContingenciaEmisionDestinoDG` | Igual, emitida en destino | `cuitDestino`, `contingencia` | AC → CO |
| `cerrarContingenciaCPE` | Cierre de la contingencia | `concepto` (A, B o C), `reactivacionDestino?{cuitTransportista?, nroOperativo?}`, `motivoDesactivacionCP?{concepto, descripcion?}` | A: CO → AC (reactivación para descarga). B: CO → CO (extensión del plazo). C: CO → DE (desactivación definitiva) |
| `cerrarContingenciaCPEEmisionDestinoDG` | Igual, emitida en destino | `cuitDestino` y lo mismo que la anterior | igual que la anterior |
| `desvioCPEAutomotor` | Desvío a otro destino | `cuitSolicitante`, `destino`, `transporte` | CF → AC ("Desvío"). Error 2130: se superó la cantidad de desvíos |
| `desvioCPEFerroviaria` | Igual, ferroviaria | `cuitSolicitante?` (opcional desde v1.3), `destino`, `transporte` | CF → AC |
| `desvioCPEAutomotorDG` | Igual, automotor DG **y emitida en destino** (tabla 2.9) | `cuitSolicitante`, `destino`, `transporte` | CF → AC |
| `desvioCPEFerroviariaDG` | Igual, ferroviaria DG | `cuitSolicitante`, `destino`, `transporte` | CF → AC |
| `nuevoDestinoDestinatarioCPEAutomotor` | Nuevo destino o destinatario después de un rechazo | `destino`, `destinatario?`, `transporte` | RE → AC (en granos, hasta 2 nuevos destinos). Error 2232 |
| `nuevoDestinoDestinatarioCPEFerroviaria` | Igual, ferroviaria | `destino`, `destinatario?`, `transporte` | RE → AC |
| `nuevoDestinoDestinatarioCPEAutomotorDG` | Igual, automotor DG | `destino`, `destinatario?`, `transporte` | RE → AC ("Nuevos destino (Máximo 2)") |
| `nuevoDestinoDestinatarioCPEFerroviariaDG` | Igual, ferroviaria DG | `destino`, `destinatario?`, `transporte` | RE → AC |
| `nuevoDestinoDestinatarioCPEEmisionDestinoDG` | Igual, emitida en destino | `cuitDestino`, `destino`, `destinatario?`, `transporte` | RE → AC |
| `regresoOrigenCPEAutomotor` | La carga vuelve al origen tras un rechazo | `transporte` | RE → AC (el solicitante pasa a ser destinatario, historial v1.3) |
| `regresoOrigenCPEFerroviaria` | Igual, ferroviaria | `transporte` | RE → AC |
| `regresoOrigenCPEAutomotorDG` | Igual, automotor DG | `transporte`, `planta?`, `domicilioDestino?`, `cuitDestinatario?` | RE → AC ("Permitir cambiar planta misma cuit") |
| `regresoOrigenCPEFerroviariaDG` | Igual, ferroviaria DG | `transporte`, `planta?`, `domicilioDestino?`, `cuitDestinatario` (obligatorio) | RE → AC |
| `regresoOrigenCPEEmisionDestinoDG` | Igual, emitida en destino | `cuitDestino`, `transporte`, `planta?` | RE → AC |
| `aceptarEmisionDG` | Aceptar una CPE DG pendiente de emisión | `cuitSolicitante` | PE → AC ("CPE Aceptada") |
| `rechazarEmisionDG` | Rechazar una CPE DG pendiente de emisión | `cuitSolicitante` | PE → IN ("CPE Rechazada") |
| `aceptarEmisionDestinoDG` | El origen acepta una emitida en destino | `cuitSolicitante`, `cuitDestino` | PO → AC. El diagrama no dibuja esta flecha; se infiere de la descripción ("aceptar una carta de porte en estado PO") |
| `rechazarEmisionDestinoDG` | El origen rechaza una emitida en destino | `cuitSolicitante`, `cuitDestino` | PO → IN. La tabla 2.9 también la lista como el "Rechazo" de la familia emitida en destino |

### Ediciones (identifican la CPE por `nroCTG`, no cambian el estado y devuelven CPR)

| operación | propósito | entrada principal | estado requerido |
|---|---|---|---|
| `editarCPEAutomotor` | Modifica datos de una automotor | `nroCTG`, intervinientes sueltos, `cuitDestinatario`, `cuitChofer`, `cuitTransportista`, `destino`, `cosecha?`, `pesoBruto`, `codGrano`, `dominio*`, `kmRecorrer?`, `tarifa?`, `observaciones?` | AC o CN, según la descripción (pág. 76). Error 2238: tope de modificaciones |
| `editarCPEFerroviaria` | Modifica datos de una ferroviaria | `nroCTG`, intervinientes sueltos, `cuitTransportista?`, `destino?`, `nroVagon?`, `pesoBruto`, `codGrano` | AC o CN, según la descripción (pág. 74) |
| `editarCPEDGAutomotor` | Modifica una automotor DG (y emitida en destino, tabla 2.9) | `nroCTG`, transporte, carga, `intervinientes?`, `destino?`, `cuitPagadorFlete`, `kmRecorrer`, ... | AC |
| `editarCPEDGFerroviaria` | Modifica una ferroviaria DG | `nroCTG`, `nroOperativo`, `ramal`, `cuitPagadorFlete`, `kmRecorrer`, ... | AC o CN, según la descripción |
| `editarCPEDGDuctos` | Modifica una de ductos | `nroCTG`, `codGrano?`, `codDerivadoGranario?`, `unidadMedida?`, `tipoEmbalaje?`, `intervinientes?`, `cuitDestinatario?`, `observaciones?` | AC |
| `editarCPEConfirmadaAutomotor` | Corrige intervinientes de una confirmada | `nroCTG`, `intervinientes` | CN |
| `editarCPEConfirmadaFerroviaria` | Igual, ferroviaria | `nroCTG`, `intervinientes` | CN |
| `editarCPEDGConfirmadaAutomotor` | Igual, automotor DG | `nroCTG`, `intervinientes?`, `observaciones?` | CN |
| `editarCPEDGConfirmadaFerroviaria` | Igual, ferroviaria DG | `nroCTG`, `intervinientes?`, `observaciones?` | CN |
| `editarCPEConfirmadaDuctos` | Igual, ductos | `nroCTG`, `intervinientes?`, `observaciones?` | CN |

En total: 15 de servicio y parámetros, 6 autorizaciones, 11 consultas, 33 transiciones y 10 ediciones, o sea 75.

## Errores

Hay cuatro clases (sección 2.4, pág. 11-13):

1. **De negocio e internos: dentro de la respuesta**, en `respuesta/errores/error*/{codigo, descripcion}` (`Errores` → `CodigoDescripcion`, con `codigo` de tipo **string**). Según el manual, algunos son "excluyentes" (rechazan la operación) y otros "admitidos" (la operación se hace igual). El status HTTP de estas respuestas no se observó; por el diseño, tiene que ser 200 (**NO VERIFICADO**).
2. **Internos**: `500` "Error general de aplicación." (la operación se rechaza) y `550` "Error al generar el archivo pdf." (la operación **se acepta**, la respuesta viene sin `<pdf>` y trae el 550 en `errores`).
3. **De formato** (esquema): el manual los muestra con códigos `cvc-type.3.1.3` y `cvc-complex-type.2.4.a`, como si vinieran en `errores`. Lo observado el 2026-10-02 es distinto: un request sin `auth` volvió como **HTTP 500, `soap:Fault`, `faultcode` `soap:Client`**, `faultstring` "Error en la validacion del esquema XML: [Unmarshalling Error: cvc-...]". El simulador sigue lo observado.
4. **Excepcionales**: `soap:Fault`. El ejemplo del manual (pág. 13) es `S:Client` con "Couldn't create SOAP message due to exception: XML reader error: ...". Se observó:
   - errores de autenticación: `soap:Client`, HTTP 500 (ver Autenticación);
   - SOAPAction desconocido (`x`): HTTP 500, **`soap:Server`**, `The given SOAPAction x does not match an operation.`;
   - el `detail` con `Exception{uuid, timestamp, businessErrorId, exceptionDetails, serverName}` que declara el WSDL no apareció en ningún caso.

Tabla de códigos: **Anexo 4.1, "Validaciones / errores de Negocio", pág. 265-269 (Tabla 4), unos 139 códigos** del 500 al 2247. Algunos llevan parámetros `{0}`, `{1}`. **Ojo:** el texto extraído con `pdftotext -layout` (`SCR\manuales\wscpe.txt`, líneas 14124-14334) desalinea códigos y descripciones (pone, por ejemplo, "2032 La transición..." cuando en realidad es 2034). La extracción con `-raw` (`SCR\an\wscpe-raw.txt`) sale bien alineada. Los códigos que más importan para simular son:

| Código | Descripción (texto del manual) |
|---|---|
| 500 | Error general de aplicación. |
| 550 | Error al generar el archivo PDF. |
| 800 | No existen solicitudes para los parámetros indicados. |
| 801 | Debe ingresar un tipo de carta de porte válido para la consulta. |
| 950 | El campo '{0}' es requerido. |
| 956 | La cuit {0} no es válida en PUC. |
| 961 | Número de orden incorrecto para el tipo de carta de porte y sucursal ingresados. |
| 962 | El número de sucursal no es válido. |
| 1302 | No existe una CPE con los parámetros indicados |
| 2004 | Estado no válido |
| 2006 | La planta {0} no existe |
| 2014 | No se encontró información para el Código de Grano solicitado. |
| 2034 | La transición desde el estado {0} hacia el estado {1} es inválida. |
| 2037 | Usted no puede realizar operaciones para la CUIT solicitante {0}. |
| 2038 | Usted no puede realizar operaciones para la CUIT destino {0}. |
| 2039 | Usted no puede realizar operaciones para la solicitud indicada. |
| 2121 | El plazo para la Anulación de la actual Carta de Porte Electrónica fue superado. |
| 2130 | Se superó la cantidad de desvíos permitidos. |
| 2152 | El rango de fechas debe ser como máximo de 3 días. |
| 2220 | El motivo anulación es inválido. |
| 2225 | El motivo de rechazo es inválido. |
| 2232 | Se superó la cantidad de nuevos destinatarios permitidos permitidos. |
| 2238 | La Carta de Porte no puede modificarse porque alcanzó el tope máximo de modificaciones realizadas. |
| 2241 | La Carta de Porte ya fue emitida con el Nro de CTG {0}. |

En el anexo hay detalles raros: el 2239 aparece dos veces con el mismo texto, el 2203 incluye un sitio literal `www.xxx.gob.ar\` y el historial (v2.0.5) anuncia como nuevos los códigos 2073, 2074 y 2243-2247, aunque en la tabla actual 2073 y 2074 son de campaña. Los motivos de contingencia por familia están en el anexo 4.2 (pág. 269-270): ferroviaria A-G, automotor y emitida en destino A-F, ductos A, C, D, E, F. Los motivos de anulación y de rechazo, en 4.3 y 4.4 (1, 2 y 3; con el 3 "debería" ir la observación).

## Comportamiento a simular

**Identidad y numeración**

- La clave del cliente es `(tipoCPE, sucursal, nroOrden)`. El emisor elige el `nroOrden` (`NumeroOrden`, int de 1 a 99999999) y `sucursal` (`NumeroSucursal`, de 0 a 99999 según el WSDL; el manual dice de 1 a 99999).
- `consultarUltNroOrden(sucursal, tipoCPE)` devuelve "el último número de orden de CPE autorizado según número de sucursal" (pág. 31). En la sección 1.3: "La última carta de porte se determina por medio del último número de orden registrado en las bases para una determinada sucursal y tipo de carta de porte". Que el contador también dependa de la `cuitRepresentada` es lo razonable pero **NO VERIFICADO**. Lo que devuelve si todavía no hay ninguna CPE (¿`nroOrden` ausente, 0 o error 800?) **NO VERIFICADO**.
- Flujo feliz: `consultarUltNroOrden` → N, `autorizarCPE*` con `nroOrden = N+1`. El código 961 ("Número de orden incorrecto para el tipo de carta de porte y sucursal ingresados") hace pensar que el servidor exige ese correlativo. **NO VERIFICADO** si exige exactamente N+1 o solo que sea mayor.
- Recuperación ante timeout (sección 1.3): si una autorización no tiene respuesta, se consulta el último número de orden. Si coincide con el enviado, la CPE quedó autorizada y se recupera con `consultarCPE*` por `cartaPorte`.
- Reenvío del mismo `nroOrden`: el código **2241 "La Carta de Porte ya fue emitida con el Nro de CTG {0}"** es el candidato natural. Que sea ese y no 961 o 954/1321 ("registro ya existe") **NO VERIFICADO**. No es idempotente: devuelve error, no la CPE existente (inferido).
- En la tabla `TipoCPE` (sección 3.1, pág. 212-213) los valores válidos son: **74** Automotor, **75** Ferroviaria, **274** Automotor Flete Corto, **284** CPEDG Automotor, **285** CPEDG Ferroviaria, **286** CPEDG Emisión Destino y **287** CPEDG Otros Medios Terrestres. `autorizarCPEFerroviaria`, `autorizarCPEFerroviariaDG` y `autorizarCPEDuctosDG` no reciben tipo. Para las dos primeras el tipo es 75 y 285 (inferido); para ductos, ¿287? **NO VERIFICADO**.

**CTG que asigna ARCA**

- `nroCTG` va como `xsd:long` en el WSDL; el manual lo describe como el tipo simple `NumeroCTG` de 12 dígitos, entre 010100000000 y 999999999999 (pág. 213).
- Granos: `XXYYZZZZZZZZ`. `XX` es 01 para automotor y 02 para ferroviaria; `YY` es 01 si el origen es el campo de un productor y 02 si es una planta RUCA; `ZZZZZZZZ` es un correlativo del sistema. Como el número arranca con 0, en un `long` el cero inicial se pierde (por ejemplo, 10100000123).
- DG: `XXZZZZZZZZZZ` (12 dígitos; el manual escribe `XXZZZZZZZZZZZZ`, pero el rango es de 12). `XX` es 03 para automotor DG, 04 para ferroviaria DG, 05 para emitida por destino y 06 para ductos.
- El CTG vuelve en `cabecera/nroCTG` en la autorización y en todas las respuestas CPR. Es la clave de las ediciones y una clave alternativa en las consultas.

**Máquina de estados** (sección 2.7, pág. 14-17)

Códigos en `cabecera/estado` (string): `BR` Borrador, `AC` Activa, `CF` Activa con confirmación de arribo, `CN` Confirmada, `CO` Activa con contingencia, `DE` Desactivada, `RE` Rechazada, `AN` Anulada, `DD` Descargado en destino, `PE` Pendiente de emisión, `IN` Inactiva, `PO` Pendiente de aceptación por el origen, `PA` Pendiente de aceptación por el productor, `AP` Anulación por el productor. PA y AP no aparecen en ningún diagrama.

```
Granos (automotor 74/274, ferroviaria 75):
  (web) -> BR --confirmar--> AC          (WS)  autorizarCPE* -> AC
  AC --anularCPE--> AN
  AC --informarContingencia--> CO
  CO --cerrarContingenciaCPE A--> AC  | B--> CO (extiende) | C--> DE
  AC --descargadoDestinoCPE--> DD --"confirmación de descarga en destino"--> CF
  AC --confirmarArriboCPE--> CF
  CF --confirmacionDefinitivaCPE*--> CN
  CF --rechazoCPE--> RE
  CF --desvioCPE*--> AC
  RE --regresoOrigenCPE* | nuevoDestinoDestinatarioCPE* (máx. 2)--> AC

Derivados granarios (284/285/286): lo mismo, más
  (industria) -> PE --aceptarEmisionDG--> AC   | --rechazarEmisionDG--> IN
  autorizarCPEEmisionDestinoDG -> PO --aceptarEmisionDestinoDG--> AC (inferido)
                                     --rechazarEmisionDestinoDG--> IN

Ductos (autorizar discontinuado): AC, AN, CO, DE, DD, CF, CN, PE, IN; sin RE,
  sin desvío ni nuevo destino (la tabla 2.9 dice N/A). El diagrama dibuja
  una flecha AN -> DD que parece un error del dibujo.
```

- Una transición desde un estado que no corresponde devuelve **2034** "La transición desde el estado {0} hacia el estado {1} es inválida." (o 2004 "Estado no válido"). Si la clave no existe, **1302** "No existe una CPE con los parámetros indicados".
- Cada transición devuelve en `cabecera` el estado nuevo, `fechaInicioEstado` y `fechaVencimiento` ("Fecha de vencimiento del estado actual", pág. 221).
- Leyenda de los diagramas: los estados naranja (AC, CO, RE) "cuando se vence bloquea planta (cuando el origen es planta) o el cuit (cuando el origen es un campo) del emisor"; el celeste (CF) bloquea planta o CUIT del receptor. Los errores 2002 y 2003 ("Cartas de Porte pendientes de resolución VENCIDAS") son la consecuencia en la siguiente autorización.
- En la WS la autorización deja la CPE directamente en AC: el borrador BR solo existe en la web ("Solicitud Web").

**Plazos y vigencias**: el manual no da números. Solo hay mensajes con parámetros: 2051 (partida del tren como máximo {0} hs antes), 2132 (fecha de partida del desvío), 2121 (plazo de anulación superado) y 2245. Para el simulador, `fechaVencimiento` tiene que ser configurable (**NO VERIFICADO** qué valores usa ARCA).

**PDF** (sección 2.6, pág. 14): elemento `<pdf>` de tipo `xsd:base64Binary`, "el mismo archivo que se imprime por la aplicación web", al final de `respuesta`, antes de `errores` y `metadata`. Puede venir en todas las respuestas `Detalle*`, `CartaPorteRespuesta` y en `consultaCPEFerroviariaPorNroOperativo`. Si falla la generación: la operación se acepta igual, no viene `<pdf>` y viene el error 550. El manual no dice qué operaciones lo llenan siempre. El texto de la sección habla de "detalle de la liquidación", copiado del manual de wslpg.

**Paginación**: no hay. Las listas (`cartaPorte*`, `planta*`, etc.) vienen enteras; el filtro de `consultarCPEPorDestino` está limitado por el rango de fechas.

**Formatos** (anexo 4.5): `date` = `AAAA-MM-DD` sin huso horario; `dateTime` = `aaaa-MM-ddThh:MM:ss` sin huso (ejemplo `2016-11-17T11:32:23`).

**Lo mínimo para un cliente**: `dummy`; `consultarUltNroOrden`; `autorizarCPEAutomotor` (devuelve CTG, estado AC y PDF); `consultarCPEAutomotor` por clave y por CTG; `confirmarArriboCPE` → `confirmacionDefinitivaCPEAutomotor`; `anularCPE`; y los errores típicos: Fault de autenticación, 961/2241 por número de orden y 2034 por transición inválida.

## No verificado

- Status HTTP y forma exacta de una respuesta con `errores` de negocio (se espera HTTP 200). No hubo token válido, así que nada después de la autenticación se observó.
- Faults por token vencido, CUIT no autorizada en el TA o TA de otro servicio: textos desconocidos.
- Si un SOAPAction vacío o ausente se resuelve por el elemento del body (lo típico de CXF).
- Alcance del contador de `consultarUltNroOrden` (¿incluye la CUIT?) y qué devuelve si no hay CPE previas.
- Si el número de orden tiene que ser exactamente el último + 1, y qué código se devuelve al reenviar uno ya usado (2241, 961, 954 o 1321).
- Estado inicial real de cada `autorizar*` por WS: AC en granos (diagrama); AC o PE en DG según el perfil; PO en emitida en destino.
- Qué operación hace la transición DD → CF ("confirmación de descarga en destino"); probablemente `confirmarArriboCPE`.
- Si `nuevoDestinoDestinatario*` y `regresoOrigen*` solo valen desde RE (así está en el diagrama) o también desde AC o CF.
- Estado destino de `aceptarEmisionDestinoDG` (PO → AC, por inferencia).
- `tipoCPE` implícito en ferroviaria (75), ferroviaria DG (285) y ductos (¿287?).
- Plazos de vencimiento por estado, tope de desvíos, tope de modificaciones y tope de anuladas.
- Qué operaciones devuelven `<pdf>` lleno.
- Diferencias entre el manual y el WSDL (vale el WSDL):
  - `auth/cuit` en el manual, `auth/cuitRepresentada` en el WSDL.
  - Namespace `http://` en los ejemplos del principio del manual, `https://` en el WSDL.
  - `dummyResp` en el manual, `DummyResp` en el WSDL; `OK` en el manual, `Ok` en lo observado.
  - El manual documenta `nuevoDestinatarioCPEDuctosDG` (2.10.57, `NuevoDestinatarioCPEDuctosDGReq` con `destinatario{cuit}`, `fechaHoraInicioEnvio`, `fechaHoraFinEnvio`). **No es una operación del WSDL**: solo existe el complexType `NuevoDestinatarioCPEDuctosDGResponse`, sin elemento ni operación.
  - La sección 2.10.55 "Editar CPE Ductos" dice "Nombre método: autorizarCPEDuctosDG"; en el WSDL la operación es `editarCPEDGDuctos`, que el manual no nombra en ningún lado (en la tabla 2.9 figura como `editarCPEDuctosDG`). En la misma tabla `editarCPEDGFerroviaria` aparece como `editarCPEFerroviariaDG`, y para la emitida en destino figura `descargadoDestinoCPE` en lugar de `descargadoDestinoCPEEmisionDestinoDG`.
  - `informarContingenciaEmisionDestinoDG` y `autorizarCPEDuctosDG` están documentadas dos veces cada una (2.10.53/2.10.62 y 2.10.54/2.10.55).
  - El manual pone `retiroProductor/certificadoCOE` en `autorizarCPEFerroviaria` (pág. 34); el `RetiroProductorSolicitud` del WSDL solo tiene `cuitRemitenteComercialProductor`. Igual quedan los errores 2032 y 2033 sobre el COE.
  - El manual pone `destinatario?` en `RegresoOrigenSolicitud` (tabla, pág. 227); el WSDL de `regresoOrigenCPEFerroviaria` y `regresoOrigenCPEAutomotor` no lo tiene (el historial v1.3 dice que se eliminó). Las variantes DG sí tienen `cuitDestinatario`.
  - Rangos de tipos simples: `NumeroSucursal` va de 0 a 99999 en el WSDL y de 1 a 99999 en el manual; `CodigoGrano`, de 0 a 999 en el WSDL y de 0 a 99 en el manual; `Kilogramos`, de 1 a 88000 en el WSDL y de 0 a 88000 en el manual; `NumeroVagon`, de 1 a 99999999 en el WSDL y de 10000000 a 99999999 en el manual (el historial v1.5 acepta ceros a la izquierda).
  - En la sección 2.4, el manual muestra los errores de esquema dentro de `errores`; lo observado es un `soap:Fault`.
- El significado de `elapsedTime` con valores del tamaño del epoch y el formato de 12 h del `id` del header son solo observaciones (2026-10-02).
