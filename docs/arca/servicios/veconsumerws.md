# veconsumerws

Consumo de la Ventanilla Electrónica (VE) por web service, también llamado WSCCOMU. Un sistema externo lista las comunicaciones que ARCA le publicó a una CUIT, las lee (con sus adjuntos) y consulta los sistemas publicadores y los estados posibles. Lo puede usar cualquier sistema con certificado y TA de WSAA para `veconsumerws`, siempre que la CUIT consultada esté en las relations del TA.

Fuentes:

- Manual "VE-CU-WS-Consumir-Comunicaciones", versión 1.4.0 del 27/07/2026: `https://www.afip.gob.ar/ws/WSCComu/vecuwsconcomunicaciones.pdf`.
- WSDL de homologación y de producción, descargados el 2026-10-02.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, Apache CXF (los `Content-ID` dicen `@cxf.apache.org`) |
| SOAP | **1.2** en el WSDL (`soap12:binding`, document/literal). En vivo también acepta SOAP 1.1 y contesta en 1.1 |
| MTOM | **Siempre activo en la respuesta**, incluso en `dummy`: `Content-Type: multipart/related; type="application/xop+xml"` (ver abajo) |
| Endpoint homologación | `https://stable-middleware-tecno-ext.afip.gob.ar/ve-ws/services/veconsumer` |
| Endpoint producción | `https://infraestructura.afip.gob.ar/ve-ws/services/veconsumer` |
| WSDL | `?wsdl` sobre cada endpoint |
| targetNamespace del WSDL | `http://ve.tecno.afip.gov.ar/domain/service/ws` (prefijos `tns`/`vews`) |
| Otros namespaces | `http://ve.tecno.afip.gov.ar/domain/service/ws/types` (`vewst`, requests y tipos) y `http://core.tecno.afip.gov.ar/model/ws/types` (`core`, `AuthRequest`) |
| portType / binding / service | `VEConsumer` / `VEConsumerServiceSoapBinding` / `VEConsumerService`, port `VEConsumerPort` |
| SOAPAction | El binding no declara `soapAction`. Con SOAP 1.2 no hace falta `action` en el `Content-Type`; con SOAP 1.1 se mandó `SOAPAction: ""` y funcionó |
| Archivo guardado | `wsdl/veconsumerws-homologacion.wsdl` (esquemas inline, sin imports externos) |
| Producción vs homologación | Mismos tipos y operaciones. Producción agrega dos `xs:import` sin `schemaLocation` entre los esquemas inline y cambia el espaciado y el `soap12:address` |
| WSAA service id | `veconsumerws` (manual, sección 7: "El id del servicio es veconsumerws") |
| Versión | El servicio no expone versión |

### Namespaces de cada elemento (trampa)

Los esquemas son `elementFormDefault="unqualified"`, salvo los elementos marcados `form="qualified"`. En la práctica:

- Request: el elemento raíz (`consultarComunicaciones`, `consumirComunicacion`, `consultarSistemasPublicadores`, `consultarEstados`) va en `.../ws/types`. `dummy`, en cambio, va en `.../ws`.
- `authRequest` y los hijos de `filter` van **sin namespace**. `token`, `sign` y `cuitRepresentada` van en `http://core.tecno.afip.gov.ar/model/ws/types`.
- Response: `*Response` y su hijo (`RespuestaPaginada`, `Comunicacion`, `Sistemas`, `Estados`, `DummyResult`) van en `.../ws`. `items`, `ComunicacionSimplificada`, `Estado` y `Sistema` van en `.../ws/types` (`ns4` en los ejemplos). `adjuntos` y `adjunto` van en `.../ws` según el WSDL, aunque en el ejemplo 6.4 del manual salen sin prefijo ni namespace. El resto, sin namespace.

## Autenticación

```xml
<typ:consultarEstados xmlns:typ="http://ve.tecno.afip.gov.ar/domain/service/ws/types"
                      xmlns:typ1="http://core.tecno.afip.gov.ar/model/ws/types">
  <authRequest>
    <typ1:token>...</typ1:token>
    <typ1:sign>...</typ1:sign>
    <typ1:cuitRepresentada>20111111112</typ1:cuitRepresentada>
  </authRequest>
</typ:consultarEstados>
```

`cuitRepresentada` es la CUIT dueña de las comunicaciones. Tiene que estar en las relations del TA. El sistema "filtrará aquellas Comunicaciones no pertenecientes a la CUIT indicada en el elemento AuthRequest.cuitRepresentada" (manual, 2.2).

Errores de autenticación reales (homologación, 2026-10-02). Todos son SOAP 1.2 Fault, **HTTP 500**, `Content-Type: application/soap+xml;charset=UTF-8`, sin multipart, `Code/Value` = `soap:Receiver`, sin `Detail`:

| Caso | `soap:Text xml:lang="en"` |
|---|---|
| `token` = `abc` (no es base64 de un XML) | `No se pudo procesar el SSO Token xml recibido, error [Parsing Error : Content is not allowed in prolog.` + salto + `Line : 1` + salto + `Column : 1` + salto + `{file: [not available]; line: 1; column: 1}]` |
| `token` = XML de TA bien formado en base64, `sign` falso | `Falló la verificación del Token recibo, verificar que la firma incluida en el elemento Sign corresponda con el Token indicado` |
| Sin `token` ni `sign` | `No se recibió el elemento &lt;token>` |

Respuesta exacta del caso de firma falsa:

```xml
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope"><soap:Body><soap:Fault><soap:Code><soap:Value>soap:Receiver</soap:Value></soap:Code><soap:Reason><soap:Text xml:lang="en">Falló la verificación del Token recibo, verificar que la firma incluida en el elemento Sign corresponda con el Token indicado</soap:Text></soap:Reason></soap:Fault></soap:Body></soap:Envelope>
```

Headers de las respuestas reales: `Strict-Transport-Security: max-age=300; includeSubDomains; preload`, `X-XSS-Protection: 1; mode=block`, `X-Frame-Options: sameorigin`, `X-Content-Type-Options: nosniff`, `Vary: Accept-Encoding`. En los errores, además, `Connection: close`.

## Operaciones

5 operaciones en el `portType` `VEConsumer`. Todas, salvo `dummy`, declaran el fault `VentanillaWSFault`.

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Verifica infraestructura, sin token | `ws:dummy` vacío | `dummyResponse/DummyResult{dbserver, appserver, authserver}` | Ninguno |
| `consultarComunicaciones` | Lista paginada de comunicaciones de la CUIT | `authRequest`, `filter{estado?, fechaDesde, fechaHasta?, comunicacionIdDesde?, comunicacionIdHasta?, tieneAdjunto?, sistemaPublicadorId?, pagina? (default 1), resultadosPorPagina?, referencia1?, referencia2?}` | `RespuestaPaginada{pagina, totalPaginas, itemsPorPagina, totalItems, items{ComunicacionSimplificada*}}` | Consulta. Clave: `idComunicacion` |
| `consumirComunicacion` | Lee una comunicación completa, con adjuntos opcionales por MTOM | `authRequest`, `idComunicacion`, `incluirAdjuntos?` | `Comunicacion` = `ComunicacionSimplificada` + `mensaje`, `tiempoDeVida`, `adjuntos{adjunto*{filename, content?, compressed?, signed?, encrypted?, processed?, public?, md5?, contentSize}}` | **Cambia estado**: "Se registra el evento de Lectura" (manual, 3.1). La comunicación pasa de 1 (No leída) a 2 (Leída). Clave: `idComunicacion` |
| `consultarSistemasPublicadores` | Sistemas que publican en VE | `authRequest`, `idSistemaPublicador?` | `Sistemas{Sistema*{id, descripcion, certCNs?, subservicios{Subservicio*{nombre, descripcion}}?}}` | Consulta. Clave: `id` (= `sistemaPublicadorId` del filtro) |
| `consultarEstados` | Estados posibles de una comunicación | `authRequest` | `Estados{Estado*{id, descripcion}}` | Consulta |

`ComunicacionSimplificada`: `idComunicacion` (long), `cuitDestinatario` (long), `fechaPublicacion` (string), `fechaVencimiento?`, `sistemaPublicador` (long), `sistemaPublicadorDesc`, `estado` (int), `estadoDesc`, `asunto`, `prioridad` (int: 1 alta, 2 media, 3 baja), `tieneAdjunto` (boolean), `referencia1?`, `referencia2?`.

Valores de los ejemplos del manual:

- `consultarEstados`: `1` "Comunicacion No Leida", `2` "Comunicacion Leida", `0` "Comunicacion sin procesar - No disponible".
- `fechaPublicacion` aparece con dos formatos: `2012-03-01 00:00:00` (en la consulta) y `2011-04-18 13:06:00.0` (en el consumo, estilo `java.sql.Timestamp`). `fechaVencimiento`, como `2012-03-01`.
- `asunto`: "Solo si la Comunicación no tiene un 'asunto' asociado se devuelven los primeros 50 caracteres del mensaje" (manual, tabla 2).
- `itemsPorPagina`: "El valor máximo permitido es de 500 resultados, pudiendo variar en el tiempo" (manual).
- Adjunto por MTOM: `<content><xop:Include href="cid:886ee04d-...-13@cxf.apache.org" xmlns:xop="http://www.w3.org/2004/08/xop/include"/></content>`.

Respuesta real de `dummy` (homologación, 2026-10-02), HTTP 200. **Viene en multipart MTOM aunque no tenga binarios**:

```
Content-Type: multipart/related; type="application/xop+xml"; boundary="uuid:90358ea8-5336-4979-8c9c-487b133396c0"; start="<root.message@cxf.apache.org>"; start-info="application/soap+xml"

--uuid:90358ea8-5336-4979-8c9c-487b133396c0
Content-Type: application/xop+xml; charset=UTF-8; type="application/soap+xml";
Content-Transfer-Encoding: binary
Content-ID: <root.message@cxf.apache.org>

<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope"><soap:Body><ns1:dummyResponse xmlns:ns1="http://ve.tecno.afip.gov.ar/domain/service/ws"><ns2:DummyResult xmlns:ns2="http://ve.tecno.afip.gov.ar/domain/service/ws" xmlns:ns3="http://core.tecno.afip.gov.ar/model/ws/types" xmlns:ns4="http://ve.tecno.afip.gov.ar/domain/service/ws/types"><appserver>OK</appserver><dbserver>OK</dbserver><authserver>OK</authserver></ns2:DummyResult></ns1:dummyResponse></soap:Body></soap:Envelope>
--uuid:90358ea8-5336-4979-8c9c-487b133396c0--
```

- El body empieza con una línea vacía antes del primer boundary.
- **El orden real de `DummyResult` (appserver, dbserver, authserver) no respeta la secuencia del WSDL** (dbserver, appserver, authserver). Un cliente que valide contra el esquema fallaría; los clientes reales no validan. ArcaSim debería copiar el orden real.
- Con SOAP 1.1 (`text/xml`) la respuesta es la misma con envelope 1.1 y `start-info="text/xml"`.

## Errores

Todos son SOAP Fault con HTTP 500. Los de negocio tienen la forma `Error NNN: <mensaje>` en `soap:Reason/soap:Text`, con `soap:Value` = `soap:Receiver` (manual, 6.2 y 6.5). El WSDL declara un detail `VentanillaWSFault{faultCode, faultMessage, category, possibleSolutions*}`, pero **ningún ejemplo del manual lo trae**; si viaja está **NO VERIFICADO**.

| Código | Mensaje (patrón `MessageFormat` de Java) | Operación |
|---|---|---|
| 100 | Número de página inválida [{0,number,#}] | consultarComunicaciones |
| 101 | Fecha desde no soportada. Mínima fecha [{0}] (ejemplo: `[04/04/11 00:00]`) | consultarComunicaciones |
| 102 | Formato de fecha no soportado para [{0}]. Se esperaba [{1}] | consultarComunicaciones |
| 103 | Código de estado inválido [{0,number,#}] | consultarComunicaciones |
| 104 | La Comunicación [{0,number,#}] no existe | consumirComunicacion |
| 105 | La CUIT representada [{0,number,#}] no es la destinataria de la Comunicación indicada [{1,number,#}] | consumirComunicacion |
| 106 | Cantidad de ítems por página no válida [{0,number,#}] | consultarComunicaciones |
| 107 | Id Comunicación desde [{0,number,#}] se solapa con Id Comunicación hasta [{1,number,#}] | consultarComunicaciones |
| 108 | Fecha desde [{0}] se solapa con Fecha hasta [{1}] | consultarComunicaciones |
| 109 | idSistema [{0,number,#}] no es valido | consultarComunicaciones, consultarSistemasPublicadores |
| 110 | La Comunicación por la que se está consultando [{0,number,#}] no es posible obtenerla a través de este servicio | consultarComunicaciones, consumirComunicacion ("La Comunicación solicitada es interna") |
| 111 | La cantidad de días no puede superar los [{0}] entre la fechaDesde y fechaHasta o entre la fechaDesde y la fecha actual | consultarComunicaciones |
| 300 | Se ha producido un error no identificado, por favor vuelva a intentar la operación o comuníquese con mayuda@afip.gov.ar | cualquiera |

`{0,number,#}` imprime el número sin separador de miles.

## Comportamiento a simular

- Responder **siempre** en multipart MTOM las respuestas exitosas, y en SOAP plano (`application/soap+xml`) los Fault. Aceptar SOAP 1.2 y 1.1 y contestar en la misma versión.
- Validar el TA para `veconsumerws` y que `cuitRepresentada` esté en relations, con los textos de la tabla de autenticación.
- Bandeja de comunicaciones por CUIT con `idComunicacion` global, sistema publicador, prioridad, asunto, mensaje, `tiempoDeVida` y adjuntos.
- `consultarComunicaciones`: filtrar por CUIT, estado, rango de ids, rango de fechas, adjunto, sistema y referencias. Reglas del manual: `fechaDesde` no puede ser anterior a 360 días (error 101); el rango no puede superar 31 días (111); `fechaDesde` > `fechaHasta` (108); ids invertidos (107); página fuera de rango (100); `resultadosPorPagina` < 0 o mayor que el máximo (106); máximo de 500 por página.
- `consumirComunicacion`: 104 si no existe, 105 si es de otra CUIT, 110 si es "interna"; marca la comunicación como leída (estado 1 → 2). Con `incluirAdjuntos` = true, adjuntar los binarios como partes MTOM referenciadas por `xop:Include`.
- `consultarEstados`: devolver los tres estados del ejemplo (0, 1, 2).
- `consultarSistemasPublicadores`: catálogo fijo; 109 si el id no existe.
- Par de estado obvio: `consultarComunicaciones(estado=1)` muestra la comunicación; después de `consumirComunicacion`, aparece con `estado=2`.

## No verificado

- Si el detail `VentanillaWSFault` viaja dentro del Fault real de negocio.
- El valor de `tiempoDeVida` y su unidad (el manual no lo describe; el WSDL lo exige).
- Los ejemplos del manual omiten `mensaje`, `tiempoDeVida` y `tieneAdjunto` en `Comunicacion`, que el WSDL exige; no se sabe si el servicio real los manda siempre.
- El manual nombra `certCN`; el WSDL, `certCNs`. Se toma el WSDL.
- Si la lectura por web service cuenta como notificación fehaciente (efecto legal): fuera del manual.
- Qué devuelve con un TA válido de otro servicio, o con `cuitRepresentada` fuera de relations.
- Cuál es la "fecha mínima" exacta del error 101 (el ejemplo muestra `04/04/11 00:00`, no 360 días).
