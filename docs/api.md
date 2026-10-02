# ArcaSim · Referencia de la API

ArcaSim expone los web services de ARCA con sus mismas rutas, sus mismos WSDL y su mismo protocolo, y agrega una API propia para preparar escenarios de prueba. Este documento sirve para integrarlo a cualquier proyecto, en cualquier lenguaje.

**[English version](api.en.md)** · [README](../README.md) · [Estudio de la API de ARCA](arca/)

> ArcaSim no tiene relación con ARCA. Los CAE que otorga no tienen validez fiscal y sus tickets de acceso solo sirven contra ArcaSim.

## Contenido

1. [Ambientes y direcciones](#1-ambientes-y-direcciones)
2. [Inicio rápido](#2-inicio-rápido)
3. [Convenciones del protocolo](#3-convenciones-del-protocolo)
4. [WSAA: autenticación](#4-wsaa-autenticación)
5. [WSFEv1: factura electrónica](#5-wsfev1-factura-electrónica)
6. [Errores](#6-errores)
7. [API de administración](#7-api-de-administración)
8. [Escenarios de prueba](#8-escenarios-de-prueba)
9. [Integrarlo a un proyecto](#9-integrarlo-a-un-proyecto)
10. [Pasar a ARCA](#10-pasar-a-arca)
11. [Qué no es igual a ARCA](#11-qué-no-es-igual-a-arca)

---

## 1. Ambientes y direcciones

| | WSAA (ticket de acceso) | WSFEv1 (factura electrónica) |
|---|---|---|
| **ArcaSim** | `http(s)://<host>/ws/services/LoginCms` | `http(s)://<host>/wsfev1/service.asmx` |
| ARCA homologación | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx` |
| ARCA producción | `https://wsaa.afip.gov.ar/ws/services/LoginCms` | `https://servicios1.afip.gov.ar/wsfev1/service.asmx` |

| Recurso | Método y ruta |
|---|---|
| WSDL de WSAA | `GET /ws/services/LoginCms?wsdl` |
| WSDL de WSFEv1 | `GET /wsfev1/service.asmx?WSDL` |
| Panel | `GET /arcasim/` |
| API de administración | `/arcasim/api/...` ([§7](#7-api-de-administración)) |

Los WSDL son los oficiales de ARCA: solo cambia la dirección del servicio por la de ArcaSim.

## 2. Inicio rápido

**1. Levantarlo.**

```bash
git clone https://github.com/federicomoroz/arcasim && cd arcasim
docker compose up -d                                   # http://localhost:7080, con PostgreSQL
# o, sin Docker y en memoria:
dotnet run --project src/ArcaSim.Api --urls http://localhost:7080
```

**2. Un certificado.** Por defecto ArcaSim arranca con **acceso abierto**: acepta cualquier certificado que tenga el CUIT en el DN. Sirve el que WSASS emitió para homologación, o uno autofirmado:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 365 -keyout key.pem -out cert.pem \
  -subj "/CN=mi-aplicacion/serialNumber=CUIT 20111111112"
```

**3. Apuntar la aplicación.** Las dos URL de [§1](#1-ambientes-y-direcciones) y ese certificado. No hace falta cargar nada antes: el contribuyente y el punto de venta se crean la primera vez que se usan.

[`docs/ejemplos/curl.sh`](ejemplos/curl.sh) hace todo el recorrido con openssl y curl, sin ninguna librería: firma el pedido de acceso, obtiene el ticket, pregunta el último número y pide el CAE de una factura B.

```bash
ARCASIM=http://localhost:7080 CUIT=20111111112 bash docs/ejemplos/curl.sh
```

## 3. Convenciones del protocolo

| | WSAA | WSFEv1 |
|---|---|---|
| Servidor que imita | Apache Axis 1.4 | ASP.NET ASMX |
| SOAP | 1.1 y 1.2 | 1.1 y 1.2 |
| Namespace de los mensajes | `http://wsaa.view.sua.dvadac.desein.afip.gov` | `http://ar.gov.afip.dif.FEV1/` |
| `SOAPAction` (SOAP 1.1) | Obligatorio, cualquier valor (`""` sirve) | `http://ar.gov.afip.dif.FEV1/<Operación>`; sin él, se enruta por el elemento del Body |
| `Content-Type` | `text/xml; charset=utf-8` (1.1) · `application/soap+xml; charset=utf-8; action="…"` (1.2) | ídem |

**Códigos HTTP**, como en ARCA:

| Código | Cuándo |
|---|---|
| 200 | Respuesta de negocio, aunque traiga errores: en WSFEv1 los errores van dentro de `Errors` |
| 500 | Fault SOAP: todo error de WSAA; en WSFEv1, `SOAPAction` desconocida o un valor que no se puede leer (texto en un campo numérico) |
| 400 | XML mal formado, sin cuerpo |
| 503 | Servicio saturado o caído ([§8](#8-escenarios-de-prueba)): conviene reintentar |

**Formato de las respuestas de WSFEv1**: una sola línea, con el encabezado `FEHeaderInfo` (ambiente, fecha con offset `-03:00` y versión); los strings vacíos como `<CAE />`; los importes sin ceros de relleno (`122`, no `122.00`); las fechas `yyyyMMdd` y los momentos de proceso `yyyyMMddHHmmss`, en hora de Argentina.

**Lectura de los pedidos**, también como ASMX: los elementos pueden venir en cualquier orden, los desconocidos se ignoran, los que vienen sin el namespace también, y un entero obligatorio que falta vale 0.

## 4. WSAA: autenticación

### `loginCms`

`POST /ws/services/LoginCms`

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsaa="http://wsaa.view.sua.dvadac.desein.afip.gov">
  <soapenv:Body>
    <wsaa:loginCms><wsaa:in0>{CMS en base64}</wsaa:in0></wsaa:loginCms>
  </soapenv:Body>
</soapenv:Envelope>
```

`in0` es un CMS **SignedData** en base64, con el pedido de acceso (TRA) adentro (no *detached*) y el certificado del firmante incluido.

**El TRA:**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<loginTicketRequest version="1.0">
  <header>
    <uniqueId>1727780000</uniqueId>                         <!-- entero sin signo -->
    <generationTime>2026-10-02T10:00:00-03:00</generationTime> <!-- ≤ ahora, > ahora − 24 h -->
    <expirationTime>2026-10-02T10:20:00-03:00</expirationTime> <!-- > ahora, < ahora + 24 h -->
  </header>
  <service>wsfe</service>
</loginTicketRequest>
```

`source` y `destination` en el header son opcionales; si van, se validan contra el DN del certificado y el de WSAA.

**Respuesta**: `loginCmsReturn` trae el ticket de acceso (TA) como texto escapado:

```xml
<loginTicketResponse version="1">
    <header>
        <source>CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239</source>
        <destination>SERIALNUMBER=CUIT 20111111112, CN=mi-aplicacion</destination>
        <uniqueId>…</uniqueId>
        <generationTime>2026-10-02T10:00:01.123-03:00</generationTime>
        <expirationTime>2026-10-02T22:00:01.123-03:00</expirationTime>
    </header>
    <credentials>
        <token>{base64}</token>
        <sign>{base64}</sign>
    </credentials>
</loginTicketResponse>
```

- El ticket dura **12 horas**. Se reusa en cada llamada a WSFEv1 dentro del bloque `Auth`.
- **Ventana anti-repetición**: mientras el último ticket de un certificado para un servicio sigue vigente, un pedido nuevo en los últimos 10 minutos (2 en el perfil de producción) recibe `coe.alreadyAuthenticated`. Las aplicaciones tienen que guardar el ticket, como con ARCA. Se apaga desde la [API de administración](#7-api-de-administración).
- El `token` es un XML `sso` en base64 que se puede leer: trae `exp_time` y las relaciones (los CUIT que el certificado puede representar).

**Servicios** que acepta `<service>`: `wsfe`, `ws_sr_constancia_inscripcion`, `ws_sr_padron_a13`, `wscdc`, `wsfex`. Por ahora ArcaSim responde las operaciones de `wsfe`.

**Faults**, en el orden en que se validan:

| `faultcode` | `faultstring` |
|---|---|
| `ns1:cms.bad.base64` | No se puede decodificar el BASE64 |
| `ns1:cms.bad` | El CMS no es valido |
| `ns1:cms.cert.notFound` | No se ha encontrado certificado de firmador |
| `ns1:cms.sign.invalid` | Firma inválida o algoritmo no soportado |
| `ns1:cms.cert.invalid` | Certificado con fecha de generacion posterior a la actual |
| `ns1:cms.cert.expired` | Certificado expirado |
| `ns1:cms.cert.untrusted` | Certificado no emitido por AC de confianza |
| `ns1:xml.bad` | No se ha podido interpretar el XML contra el SCHEMA |
| `ns1:xml.version.notSupported` | La versión del documento no es soportada |
| `ns1:xml.source.invalid` | El atributo 'source' no se corresponde con el DN del Certificado |
| `ns1:xml.destination.invalid` | El atributo 'destination' no se corresponde con el DN del WSAA |
| `ns1:xml.generationTime.invalid` | generationTime posee formato o dato inválido (…) |
| `ns1:xml.expirationTime.expired` | El tiempo de expiración es inferior a la hora actual |
| `ns1:xml.expirationTime.invalid` | El tiempo de expiración del documento es superior a 24 horas |
| `ns1:wsn.notFound` | Servicio informado inexistente |
| `ns1:coe.notAuthorized` | Computador no autorizado a acceder al servicio |
| `ns1:coe.alreadyAuthenticated` | El CEE ya posee un TA valido para el acceso al WSN solicitado |
| `ns1:wsn.unavailable` | El servicio al que se desea acceder se encuentra momentáneamente fuera de servicio |
| `ns1:wsaa.unavailable` | El servicio de autenticación/autorización se encuentra momentáneamente fuera de servicio |

Todos salen con HTTP 500, el prefijo `ns1` ligado a `http://xml.apache.org/axis/` y los caracteres no ASCII escritos como referencias (`inv&#xE1;lida`), como los escribe Axis.

## 5. WSFEv1: factura electrónica

`POST /wsfev1/service.asmx`, `SOAPAction: "http://ar.gov.afip.dif.FEV1/<Operación>"`.

Todas las operaciones menos `FEDummy` reciben el bloque `Auth`:

```xml
<ar:Auth>
  <ar:Token>{token del ticket}</ar:Token>
  <ar:Sign>{sign del ticket}</ar:Sign>
  <ar:Cuit>20111111112</ar:Cuit>   <!-- el CUIT que factura: tiene que estar en las relaciones del ticket -->
</ar:Auth>
```

### Las 22 operaciones

| Operación | Para qué | Parámetros (además de `Auth`) | Resultado |
|---|---|---|---|
| `FEDummy` | Saber si el servicio está arriba | — | `AppServer`, `DbServer`, `AuthServer` (`OK`) |
| `FECompUltimoAutorizado` | Último número autorizado | `PtoVta`, `CbteTipo` | `PtoVta`, `CbteTipo`, `CbteNro` (0 si no hay ninguno) |
| `FECAESolicitar` | CAE de un comprobante o de un lote | `FeCAEReq` ([detalle](#fecaesolicitar)) | `FeCabResp`, `FeDetResp`, `Errors` |
| `FECompConsultar` | Un comprobante ya emitido, con todo lo enviado | `FeCompConsReq`: `CbteTipo`, `CbteNro`, `PtoVta` | `ResultGet` con el comprobante, `CodAutorizacion`, `EmisionTipo` (CAE o CAEA), `FchVto`, `FchProceso`, `Observaciones` |
| `FECompTotXRequest` | Cuántos comprobantes entran en un pedido | — | `RegXReq` (250) |
| `FECAEASolicitar` | Un CAEA para una quincena | `Periodo` (`yyyyMM`), `Orden` (1 o 2) | `ResultGet`: `CAEA`, `FchVigDesde`, `FchVigHasta`, `FchTopeInf`, `FchProceso` |
| `FECAEAConsultar` | El CAEA de una quincena | `Periodo`, `Orden` | Igual que `FECAEASolicitar` |
| `FECAEARegInformativo` | Informar comprobantes emitidos con CAEA | `FeCAEARegInfReq`: igual que `FeCAEReq`, cada detalle con `CAEA` y `CbteFchHsGen` | `FeCabResp`, `FeDetResp` con el `CAEA` |
| `FECAEASinMovimientoInformar` | Un punto de venta que no usó el CAEA | `PtoVta`, `CAEA` | `CAEA`, `FchProceso`, `PtoVta`, `Resultado` |
| `FECAEASinMovimientoConsultar` | Los informados sin movimiento | `CAEA`, `PtoVta` (0 = todos) | `ResultGet`: lista de `FECAEASinMov` |
| `FEParamGetTiposCbte` | Tipos de comprobante | — | `ResultGet`: `CbteTipo` (`Id`, `Desc`, `FchDesde`, `FchHasta`) |
| `FEParamGetTiposConcepto` | Conceptos (productos, servicios) | — | `ConceptoTipo` |
| `FEParamGetTiposDoc` | Tipos de documento | — | `DocTipo` |
| `FEParamGetTiposIva` | Alícuotas de IVA | — | `IvaTipo` |
| `FEParamGetTiposMonedas` | Monedas | — | `Moneda` |
| `FEParamGetTiposOpcional` | Datos opcionales por resolución | — | `OpcionalTipo` |
| `FEParamGetTiposTributos` | Otros tributos | — | `TributoTipo` |
| `FEParamGetTiposPaises` | Países | — | `PaisTipo` |
| `FEParamGetCondicionIvaReceptor` | Condiciones frente al IVA del receptor | `ClaseCmp` opcional (`A`, `ALEY`, `B`, `C`, `49`) | `CondicionIvaReceptor` (`Id`, `Desc`, `Cmp_Clase`) |
| `FEParamGetPtosVenta` | Puntos de venta del emisor | — | `PtoVenta` (`Nro`, `EmisionTipo`, `Bloqueado`, `FchBaja`) |
| `FEParamGetCotizacion` | Cotización de una moneda | `MonId`, `FchCotiz` opcional | `MonId`, `MonCotiz`, `FchCotiz` |
| `FEParamGetActividades` | Actividades del emisor | — | `602` (ArcaSim no tiene actividades por contribuyente) |

Las tablas de parámetros son las que devolvió el servicio de homologación de ARCA, con sus textos y el literal `NULL` en las fechas sin valor.

### FECAESolicitar

```xml
<ar:FECAESolicitar>
  <ar:Auth>…</ar:Auth>
  <ar:FeCAEReq>
    <ar:FeCabReq>
      <ar:CantReg>1</ar:CantReg>      <!-- cantidad de FECAEDetRequest -->
      <ar:PtoVta>1</ar:PtoVta>
      <ar:CbteTipo>6</ar:CbteTipo>    <!-- 1 Factura A, 6 Factura B, 11 Factura C… -->
    </ar:FeCabReq>
    <ar:FeDetReq>
      <ar:FECAEDetRequest>
        <ar:Concepto>1</ar:Concepto>           <!-- 1 productos, 2 servicios, 3 ambos -->
        <ar:DocTipo>99</ar:DocTipo>            <!-- 80 CUIT, 96 DNI, 99 consumidor final -->
        <ar:DocNro>0</ar:DocNro>
        <ar:CbteDesde>1</ar:CbteDesde>         <!-- último autorizado + 1 -->
        <ar:CbteHasta>1</ar:CbteHasta>
        <ar:CbteFch>20261002</ar:CbteFch>      <!-- opcional: sin ella, la fecha del pedido -->
        <ar:ImpTotal>1210</ar:ImpTotal>
        <ar:ImpTotConc>0</ar:ImpTotConc>       <!-- no gravado -->
        <ar:ImpNeto>1000</ar:ImpNeto>          <!-- gravado -->
        <ar:ImpOpEx>0</ar:ImpOpEx>             <!-- exento -->
        <ar:ImpTrib>0</ar:ImpTrib>             <!-- suma de Tributos -->
        <ar:ImpIVA>210</ar:ImpIVA>             <!-- suma de Iva -->
        <ar:MonId>PES</ar:MonId>
        <ar:MonCotiz>1</ar:MonCotiz>
        <ar:CondicionIVAReceptorId>5</ar:CondicionIVAReceptorId>
        <ar:Iva>
          <ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>1000</ar:BaseImp><ar:Importe>210</ar:Importe></ar:AlicIva>
        </ar:Iva>
      </ar:FECAEDetRequest>
    </ar:FeDetReq>
  </ar:FeCAEReq>
</ar:FECAESolicitar>
```

Otros campos del detalle, todos opcionales: `FchServDesde`, `FchServHasta`, `FchVtoPago` (obligatorios con concepto 2 o 3), `CanMisMonExt`, `CbtesAsoc` (los comprobantes asociados de una nota de crédito o débito), `Tributos`, `Opcionales`, `Compradores`, `PeriodoAsoc`, `Actividades`. El orden y los tipos exactos están en el WSDL.

**Respuesta aprobada:**

```xml
<FECAESolicitarResult>
  <FeCabResp><Cuit>20111111112</Cuit><PtoVta>1</PtoVta><CbteTipo>6</CbteTipo>
    <FchProceso>20261002100001</FchProceso><CantReg>1</CantReg><Resultado>A</Resultado><Reproceso>N</Reproceso></FeCabResp>
  <FeDetResp><FECAEDetResponse>
    <Concepto>1</Concepto><DocTipo>99</DocTipo><DocNro>0</DocNro><CbteDesde>1</CbteDesde><CbteHasta>1</CbteHasta>
    <CbteFch>20261002</CbteFch><Resultado>A</Resultado><CAE>71263951827464</CAE><CAEFchVto>20261012</CAEFchVto>
  </FECAEDetResponse></FeDetResp>
</FECAESolicitarResult>
```

(En la respuesta real, todo en una sola línea.)

**Reglas que conviene conocer:**

- **Numeración** correlativa por CUIT, punto de venta y tipo de comprobante. El próximo es `FECompUltimoAutorizado` + 1.
- **No es idempotente.** Reenviar un comprobante ya aprobado devuelve el error 10016. Si una respuesta se pierde, lo que corresponde es consultarlo con `FECompConsultar` antes de reintentar.
- **Lotes:** se procesan en orden. El primer rechazo corta el lote y los siguientes vuelven sin procesar (`R`, sin observaciones). La cabecera dice `A`, `R` o `P` (parcial).
- **Dónde va cada problema:** los de autenticación y cabecera, y la numeración, en `Errors`; las validaciones del comprobante, en `Observaciones` del detalle. Una observación excluyente deja el comprobante en `R`; una no excluyente lo deja en `A` con su CAE.
- **Fechas:** con concepto 1, `CbteFch` entre N−5 y N+5 (N es el día del pedido); con 2 o 3, entre N−10 y N+10.
- **Importes:** `ImpTotal` = `ImpTotConc` + `ImpNeto` + `ImpOpEx` + `ImpTrib` + `ImpIVA` (clase C: `ImpNeto` + `ImpTrib`), con el margen de error del manual: 0,01 por línea sumada o 0,01 %.
- **Clases:** A y «A con leyenda» van a CUIT (`DocTipo` 80); C no lleva `Iva`; los consumidores finales desde $ 10.000.000 se identifican.
- **Condición frente al IVA del receptor:** sin ella, observación 10245 hasta el 30/11/2026 y rechazo 10246 desde el 01/12/2026, según la fecha del reloj de ArcaSim.
- **CAE:** 14 dígitos; vence 10 días después de la fecha del comprobante.

### CAEA

1. `FECAEASolicitar` dentro de la quincena o hasta 5 días antes de que empiece (fuera de esa ventana, 15006).
2. Se emite sin conexión con ese CAEA.
3. `FECAEARegInformativo` informa cada comprobante, con `CAEA` y `CbteFchHsGen` (`yyyyMMddHHmmss`).
4. `FECAEASinMovimientoInformar` por cada punto de venta CAEA que no lo usó.

## 6. Errores

**Estructura** (WSFEv1):

```xml
<Errors><Err><Code>10016</Code><Msg>…</Msg></Err></Errors>
<Observaciones><Obs><Code>10217</Code><Msg>…</Msg></Obs></Observaciones>
```

**De infraestructura y autenticación**, en todas las operaciones:

| Código | `Msg` | Cuándo |
|---|---|---|
| 500 | `Campo Auth no fue ingresado o esta mal formado.` | Falta `Auth`, o viene sin el namespace |
| 600 | `ValidacionDeToken: Parametro nulo o vacio (token)` | `Token` vacío |
| 600 | `ValidacionDeToken: No valido token. Excepcion: …` | El token no es un ticket |
| 600 | `ValidacionDeToken: No validaron las fechas del token. GenTime=…, ExpTime=…, NowUTC=…` | Ticket vencido |
| 600 | `ValidacionDeToken: Error al verificar hash: ` | `Sign` que no corresponde al token |
| 600 | `ValidacionDeToken: No valido Id Sistema: wsfe(Id Sistema de token es: …)` | Ticket de otro servicio |
| 600 | `ValidacionDeToken: No apareció CUIT en lista de relaciones: …` | `Cuit` que el ticket no puede representar |
| 602 | `No existen datos en nuestros registros para los parametros ingresados.` | Consulta sin resultado |

**Frecuentes al pedir un CAE:**

| Código | Dónde | Qué |
|---|---|---|
| 10000 | `Errors` | El emisor no puede emitir esa clase (por ejemplo, un monotributista una factura B) |
| 10002 | `Errors` | `CantReg` no coincide con los detalles enviados |
| 10005 | `Errors` | Punto de venta no habilitado para web services |
| 10016 | `Errors` | El número o la fecha no son los próximos |
| 10016 | `Obs` | `CbteFch` fuera de rango |
| 10015 | `Obs` | Receptor mal identificado, o consumidor final sobre el monto que exige identificarlo |
| 10048 | `Obs` | El total no suma |
| 10018, 10023 | `Obs` | IVA faltante o que no suma |
| 10217 | `Obs` (aprobado) | Leyenda del crédito fiscal para receptor monotributista |
| 10242, 10243 | `Obs` | Condición frente al IVA del receptor inexistente o no válida para la clase |
| 10245 / 10246 | `Obs` | Falta la condición frente al IVA del receptor (observa / rechaza) |

Los textos son los que devuelve ARCA donde se conocen, con sus faltas de tildes. La lista completa de los 495 códigos del manual está en [`arca/wsfev1-codigos.md`](arca/wsfev1-codigos.md), y cuáles devuelve ArcaSim, en [`cobertura.md`](cobertura.md).

## 7. API de administración

No existe en ARCA: prepara los escenarios de prueba. JSON, sin autenticación (es para entornos de desarrollo). Base: `/arcasim/api`.

| Método y ruta | Qué hace |
|---|---|
| `GET /status` | Ambiente, versión del manual, acceso, reloj y fallas activas |
| `PUT /settings` | `environment` (`Homologacion`/`Produccion`), `manualVersion` (`V4_7`/`V4_8`) o `followCalendar`, `replayWindowEnabled`, `openAccess`, `finalConsumerIdentificationThreshold`, `maxRecordsPerRequest`, `caeLifetimeDays` |
| `POST /reset` | Borra contribuyentes, comprobantes, fallas, límites y actividad, y vuelve a la hora real |
| `GET /taxpayers` · `GET /taxpayers/{cuit}` | Contribuyentes |
| `PUT /taxpayers/{cuit}` | Crea o actualiza: `name`, `vatCondition` (`ResponsableInscripto`, `Monotributo`, `Exento`…), `active`, `pointsOfSale` (`number`, `kind`: `WebServiceCae`, `WebServiceCaea` u `Other`; `blocked`; `deactivatedOn`) |
| `POST /certificates` | `cuit`, `alias`, `services` (por defecto `["wsfe"]`), y `csr` (devuelve el certificado en PEM) o `password` (devuelve un PFX con la clave) |
| `GET /ca` | La autoridad certificante de ArcaSim, en PEM |
| `GET /authorizations` · `POST` · `DELETE` | Qué alias puede representar a qué CUIT en qué servicio |
| `PUT /chaos/{servicio}` | `down`, `delayMilliseconds`, `dropNextResponse`, `forceRejection` (código) — servicio `wsfe` o `wsaa` |
| `GET /traffic` · `PUT /traffic/{servicio}` | Medidor del último minuto y límites: `requestsPerMinute`, `capacity`, `serviceTimeMilliseconds`, `queueLimit` |
| `POST /clock` · `DELETE /clock` | `freezeAt` (momento) y/o `advanceMinutes`; `DELETE` vuelve a la hora real |
| `GET /vouchers?cuit=&limit=` | Comprobantes emitidos |
| `GET /activity?limit=` | Registro en vivo: tickets, CAE, rechazos, pedidos saturados |
| `PUT /rates` | `currency`, `day`, `rate`: la cotización que usan las validaciones de moneda extranjera |

## 8. Escenarios de prueba

| Para probar | Cómo |
|---|---|
| ARCA caída | `PUT /chaos/wsfe {"down": true}`: HTTP 503 en WSFEv1, y `wsn.unavailable` al pedir el ticket |
| Lentitud | `PUT /chaos/wsfe {"delayMilliseconds": 8000}` |
| Un rechazo concreto | `PUT /chaos/wsfe {"forceRejection": 10048}`: el próximo comprobante vuelve rechazado con ese código |
| CAE otorgado y respuesta perdida | `PUT /chaos/wsfe {"dropNextResponse": true}`: el próximo `FECAESolicitar` queda autorizado y la conexión se corta antes de responder |
| Saturación | `PUT /traffic/wsfe {"requestsPerMinute": 30}`: lo que pase el límite recibe HTTP 503 |
| Cuello de botella | `PUT /traffic/wsfe {"capacity": 2, "serviceTimeMilliseconds": 500, "queueLimit": 10}`: dos a la vez, el resto espera en cola, y lo que no entra en la cola recibe 503 |
| Ticket vencido | `POST /clock {"advanceMinutes": 780}` |
| La obligatoriedad del 01/12/2026 | `POST /clock {"freezeAt": "2026-12-01T09:00:00-03:00"}` |
| Errores de registro de ARCA | `PUT /settings {"openAccess": false}` o `ArcaSim:Access=Strict`: certificado de la autoridad de ArcaSim, autorización por servicio, contribuyente y punto de venta registrados |

El panel `/arcasim/` hace lo mismo con botones y muestra el medidor de saturación de cada servicio.

## 9. Integrarlo a un proyecto

**Lo que cambia en la aplicación:** las dos URL y el certificado. Nada más.

**Con una librería de ARCA** (en cualquier lenguaje): la mayoría permite configurar las URL de WSAA y WSFEv1, o elegir «homologación» y cambiar su dirección. Apuntarlas a ArcaSim y darles el certificado. Si la librería guarda el ticket en un archivo, como debe, también funciona con la ventana anti-repetición de ArcaSim.

**Con un cliente generado del WSDL** (`dotnet-svcutil`, `wsimport`, zeep, `soap` de Node…): generarlo del WSDL de ArcaSim o del de ARCA da el mismo cliente. Como las direcciones de ARCA son HTTPS, los clientes generados suelen exigir HTTPS: correr ArcaSim con `--urls https://…` y confiar en su certificado de desarrollo (`dotnet dev-certs https --trust`).

**En .NET**, el repositorio trae `Arca.Client`:

```csharp
var options = new ArcaOptions
{
    WsaaUrl = new Uri("http://localhost:7080/ws/services/LoginCms"),
    WsfeUrl = new Uri("http://localhost:7080/wsfev1/service.asmx"),
    Certificate = ArcaOptions.LoadCertificate("cert.pfx", "clave"),
    Cuit = 20111111112,
    TicketCacheDirectory = "data/tickets",
};
var wsfe = new WsfeClient(http, new WsaaClient(http, options), options);
var result = await wsfe.AuthorizeNextAsync(1, 6, new Voucher { /* … */ });
```

`AuthorizeNextAsync` pregunta el próximo número, pide el CAE y, si la respuesta se pierde, consulta el comprobante antes de dar error.

**En los tests de una aplicación:** ArcaSim en memoria arranca en milisegundos. Un `docker compose` del proyecto puede levantarlo al lado de la aplicación, y `POST /arcasim/api/reset` lo deja vacío entre corridas. En .NET se puede levantar dentro del proceso de tests con `WebApplicationFactory`, como hace Comanda.

**Configuración de ArcaSim** (`appsettings.json` o variables de entorno con `__`):

| Clave | Valores |
|---|---|
| `ArcaSim:Access` | `Open` (por defecto) · `Strict` |
| `ArcaSim:Storage` | `Memory` (por defecto) · `Postgres`, con `ConnectionStrings:ArcaSim` |
| `ArcaSim:Environment` | `Homologacion` (por defecto) · `Produccion` |
| `ArcaSim:ReplayWindowEnabled` | `true` (por defecto) · `false` |
| `ArcaSim:DataDirectory` | Dónde guarda su autoridad certificante y la clave de los tickets |

## 10. Pasar a ARCA

1. Generar la clave y el pedido de certificado (CSR) con el DN `SERIALNUMBER=CUIT n, CN=alias`.
2. En homologación, cargar el CSR en **WSASS**; en producción, en **Administración de certificados digitales**. Asociar el certificado al servicio `wsfe`.
3. Dar de alta el punto de venta como **Web service** (RECE).
4. Cambiar las dos URL por las de [§1](#1-ambientes-y-direcciones) y el certificado por el de ARCA.

El código de la aplicación no cambia.

## 11. Qué no es igual a ARCA

- Los **tickets de acceso** de ArcaSim solo sirven contra ArcaSim: la firma real la pone ARCA.
- Los **CAE** no tienen validez fiscal ni se pueden constatar.
- El **padrón** es ficticio: los contribuyentes son los cargados o los creados al usarlos, y un CUIT desconocido con dígito verificador válido se toma como activo.
- Las **cotizaciones** se cargan a mano con `PUT /rates`; sin cotización, se omiten las validaciones que dependen de ella.
- Algunos códigos del manual se responden con el texto del manual, porque el de ARCA nunca se capturó.
- El certificado **TLS** no puede ser el de `*.afip.gov.ar`.
