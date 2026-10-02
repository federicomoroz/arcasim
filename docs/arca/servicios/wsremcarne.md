# wsremcarne

Remito electrónico cárnico: el comprobante que acompaña el traslado automotor de carnes y subproductos de la faena de bovinos y porcinos (medias reses, cortes, menudencias, vísceras, grasas). Lo usan frigoríficos, distribuidores y carnicerías que **generan** el remito; el **titular** o el **depositario** que lo **autorizan** cuando no son el emisor; y el **receptor**, que registra la recepción. ARCA asigna un código interno (`codRemito`) al generar y, al emitir, un número de comprobante por punto de emisión más un código de autorización ("CRE").

Manual: "Remito Electrónico Cárnico - Web Service RemCarneService - Manual para el Desarrollador", **versión 3.6** (historial: 3.6 fechada 15-03-2022, anterior a la 3.5 del 29-04-2022, sic), `https://www.afip.gob.ar/ws/remitoElecCarnico/Manual_Desarrollador_WSREMCARNE_v3_6.pdf`. Las citas "manual X.Y (p.N)" usan la numeración de secciones y la página impresa del índice.

Es de la misma familia que `wsremharina` y `wsremazucar` (mismo stack Java, mismo `info` header, mismo esquema `arrayErrores`/`arrayErroresFormato`, mismo ciclo generar → autorizar → emitir → recepción). Diferencias propias: la clave de idempotencia se llama **`idReq`** (en harina y azúcar es `idReqCliente`), el remito es plano (`RemitoType` con `cuitReceptor`, `codDomDestino`, etc., sin sub-estructuras de receptor), hay variantes "Importe" (importe COT para ARBA) y "RecNoCateg" (receptor sin CUIT), y el redestino se hace con `generarRemito` (`tipoMovimiento` RED).

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Java JAX-WS** (familia "Java" de `catalogo.md` 4.1). Respuesta `S:Envelope` con `<?xml version='1.0' encoding='UTF-8'?>`, raíz del body con prefijo `ns2`, `S:Header` con `info` propio. `Content-Type: text/xml;charset=utf-8`, `Transfer-Encoding: chunked` | Captura `dummy` 2026-10-02 |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsremcarne/RemCarneService` (igual en manual 2.1, p.18) | WSDL `soap:address`; manual |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsremcarne/RemCarneService` (igual en manual 2.1) | WSDL de producción; manual |
| targetNamespace | `http://ar.gob.afip.wsremcarne/RemCarneService/` | WSDL |
| Service / port / binding / portType | `RemCarneService` / `RemCarneServiceSOAP` / `RemCarneServiceSOAP` / `RemCarneServicePortType` | WSDL |
| WSDL guardado | `docs/arca/wsdl/wsremcarne-homologacion.wsdl` (autocontenido, sin imports). Producción idéntico salvo `soap:address` (diff 2026-10-02) | Descarga 2026-10-02 |
| WSAA service id | `wsremcarne`: "debe enviar el tag service con el valor "wsremcarne"" | Manual 2.4 (p.19) |
| SOAPAction | `targetNamespace + operación`, p.ej. `http://ar.gob.afip.wsremcarne/RemCarneService/generarRemito` | WSDL (verificado para las 29) |
| SOAP | Solo **SOAP 1.1**, document/literal | WSDL |
| `elementFormDefault` | No declarado → `unqualified`: los hijos van sin namespace | WSDL; respuesta real |
| Faults / `soap:header` en WSDL | Ninguno | WSDL |
| Operaciones | **29** | WSDL |

## Autenticación

- Contenedor `authRequest` (`AuthRequestType`), primer hijo del request, con `token` (string), `sign` (string) y `cuitRepresentada` (`CuitSimpleType`: `xsd:long`, `> 9999999999` y `<= 99999999999`, 11 dígitos). Todos obligatorios. En todas las operaciones salvo `dummy`; acá no hay excepciones (todas usan `authRequest`).
- Manual 2.4 (p.19): "Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional."
- Error excepcional documentado (manual 1.3.1, p.9), "(ejemplo)":

```xml
<S:Envelope xmlns:S= "http://schemas.xmlsoap.org/soap/envelope/">
  <S:Body>
    <ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:ns3="http://www.w3.org/2003/05/soap-envelope">
       <faultcode>ns3: Receiver</faultcode>
       <faultstring>[wscommon_007] La firma no corresponde al token enviado.</faultstring>
     </ns2:Fault>
  </S:Body>
</S:Envelope>
```

  `faultcode` con el namespace de SOAP 1.2 dentro de un sobre 1.1 y un espacio después de `ns3:`. HTTP status no documentado. **Documentado, no observado.**
- Observado en homologación (2026-10-02, `consultarTiposComprobante`): token y sign = `abc`, token y sign vacíos, y sin `authRequest` → siempre **`HTTP/1.0 200 OK`, sin `Content-Type`, `Connection: Keep-Alive`, body de texto plano** `BL<13-15 dígitos> <yyyy-MM-dd HH:mm:ss> 500` (ej. `BL6613267150993 2026-10-02 15:08:35 500`, sin auth). Mismo fenómeno que wsmtxca (`catalogo.md` 4.1). Hipótesis **NO VERIFICADA**: un WAF/F5 delante de fwshomo reemplaza cualquier HTTP 500. El Fault real de autenticación **no se pudo observar**.

Request mínimo válido a nivel esquema:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:ns="http://ar.gob.afip.wsremcarne/RemCarneService/">
  <soapenv:Header/>
  <soapenv:Body>
    <ns:consultarTiposComprobanteRequest>
      <authRequest>
        <token>...</token>
        <sign>...</sign>
        <cuitRepresentada>20111111112</cuitRepresentada>
      </authRequest>
    </ns:consultarTiposComprobanteRequest>
  </soapenv:Body>
</soapenv:Envelope>
```

`dummy`: Body vacío (el mensaje de entrada no tiene parts); el servidor despacha por SOAPAction.

## Operaciones

"Ret. remito" = `RemitoReturnType`: `codRemito?`, `tipoComprobante?`, `puntoEmision?`, `datosEmision?` (`nroRemito`, `codAutorizacion`, `fechaEmision`, `fechaVencimiento`), `estado?`, `qr?`, `resultado` (A/O/R), `evento?`, `arrayResultadoReceptoresValidos?`, `arrayObservaciones?`, `arrayErrores?`, `arrayErroresFormato?`. "Ret. op." = `{codRemito, resultado, evento?, arrayObservaciones?, arrayErrores?, arrayErroresFormato?}`, con un tipo distinto por operación pero la misma forma.

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Salud | Body vacío | `dummyResponse/dummyReturn` (`appserver`, `authserver`, `dbserver`) | — |
| `generarRemito` | Alta de remito (ENV, REP, PLA o RED) | `idReq`, `remito` (`RemitoType`) | `generarRemitoResponse/generarRemitoReturn` (Ret. remito) | **Crea** remito, asigna `codRemito`; queda PAT, PAD o emitido. Se lee con `consultarRemito` por `codRemito` o `idReq`+`puntoEmision` |
| `generarRemitoImporte` | Igual, con `importeCot` (COT de ARBA) | `idReq`, `remito` (`RemitoImporteType`) | `generarRemitoImporteResponse/generarRemitoReturn` | Igual que `generarRemito`; se lee con `consultarRemitoImporte` |
| `generarRemitoRecNoCateg` | Alta hacia receptor sin CUIT/CUIL/CDI | `idReq`, `remito` (`RemitoNoCategType`: `documentoReceptor`, `denomReceptor`, `domDestino*`) | **`GenerarRemitoRecNoCategResponse`** (G mayúscula) `/generarRemitoReturn` | Crea remito. "Luego del plazo de vigencia … quedará automáticamente aceptado" (2.5.26) |
| `generarRemitoRecNoCategImporte` | Ídem con `importeCot` | `idReq`, `remito` (`RemitoNoCategImporteType`) | `generarRemitoRecNoCategImporteResponse/generarRemitoReturn` | Ídem |
| `autorizarRemito` | Titular o depositario autoriza (A) o deniega (D) | `codRemito`, `estado` (A/D) | `autorizarRemitoResponse/autorizarRemitoReturn` (Ret. op.) | PAT/PAD → PEM / DEN |
| `anularRemito` | Emisor anula un remito no emitido | `codRemito` | `anularRemitoResponse/anularRemitoReturn` (Ret. op. **sin** `arrayObservaciones`) | → anulado (código **NO VERIFICADO**) |
| `emitirRemito` | Emite un remito pendiente; puede actualizar el viaje | `codRemito`, `viaje?` | `emitirRemitoResponse/emitirRemitoReturn` (Ret. remito) | PEM → emitido; asigna `datosEmision` y `qr` |
| `registrarRecepcion` | Receptor acepta total, parcial o rechaza | `codRemito`, `estado` (ACE/ACP/NAC/PEN), `arrayRecepcionMercaderia?` (`orden`, `kilos`, `unidades`), `categoriaReceptor` | `registrarRecepcionResponse/registrarRecepcionReturn` (Ret. op.) | Emitido → ACE / ACP / NAC. El estado lo **elige el receptor** (en harina lo calcula ARCA) |
| `modificarViaje` | Cambia transportista, conductor o vehículo | `codRemito`, `cuitTransportista`, `cuitConductor?`, `vehiculo` | `modificarViajeResponse/modificarViajeReturn` (Ret. op.) | Sin cambio de estado; dentro de 24 hs de emitido y si no fue recibido |
| `informarContingencia` | Contingencia que impide el envío | `codRemito`, `contingencia` (`tipoContingencia` long, `observacion?`) | `informarContingenciaResponse/informarContingenciaReturn` (Ret. op.) | "realiza la anulación del remito" (2.5.9) |
| `consultarUltimoRemitoEmitido` | Último emitido por tipo y punto | `tipoComprobante`, `puntoEmision` | `.../consultarUltimoRemitoReturn` (`ConsultarRemitoReturnType`) | Devuelve el remito completo. Clave (cuit, tipo, punto) |
| `consultarRemito` | Datos de un remito | Uno de: `codRemito`; `idReq`+`puntoEmision`; `cuitEmisor`+`tipoComprobante`+`puntoEmision`+`nroComprobante` | `.../consultarRemitoReturn` (`idReq?`, `remito?` (`RemitoType` con `codRemito`, `estado`, `datosEmision`, `arrayContingencias`), `qr?`, `evento?`, arrays; **sin `resultado`**) | Lee lo creado por los `generarRemito*` |
| `consultarRemitoImporte` | Ídem para remitos con importe | Igual que `consultarRemito` | `.../consultarRemitoImporteReturn` (`remito` de tipo `RemitoImporteType`) | Ídem |
| `consultarEstadosRemito` | Historial de estados | Request de `consultarRemito` | `consultarEstadosRemitoResponse/`**`estadosRemitosReturn`** (`codRemito?`, `arrayEstados/estados`: `estado`, `fecha`, `cuitUsuario`, `descUsuario`) | Lee la máquina de estados. Ojo: `estadosRemitosReturn` con "s" (en harina es `estadosRemitoReturn`) |
| `consultarRemitosEmisor` | Remitos del emisor | `rangoFechas`, `puntoEmision`, `tipoComprobante?`, `estado?`, `nroPagina?` | `.../consultarRemitosReturn` (`arrayRemitos/remitosConsulta`, `nroPagina?`, `hayMas?`) | Lista |
| `consultarRemitosAutorizador` | Como titular o depositario | `rolAutorizador` (TIT/DEP), `estadoAutorizacion` (PE/AU/RE), `rangoFechas?`, `cuitEmisor?`, `nroPagina?` | `.../consultarRemitosReturn` | Lista |
| `consultarRemitosReceptor` | Como receptor | `estadoRecepcion` (PEN/ACE/ACP/NAC), `cuitEmisor?`, `rangoFechas?`, `nroPagina?` | `.../consultarRemitosReturn` | Lista |
| `consultarReceptoresValidos` | Qué CUIT no pueden recibir remitos nuevos | `arrayReceptores/receptores/cuitReceptor` | `.../consultarReceptoresValidosReturn` (`resultado`, arrays) | Consulta |
| `consultarCodigosDomicilio` | Domicilios de un CUIT (0 = fiscal) | `cuitTitularDomicilio` | `.../consultarCodigosDomicilioReturn/arrayDomicilios` | Tabla de parámetros |
| `consultarPuntosEmision` | Puntos habilitados | solo auth | `.../consultarPuntosEmisionReturn/arrayPuntosEmision` | Tabla de parámetros |
| `consultarTiposComprobante` | 995 | solo auth | `.../consultarTiposComprobanteReturn/arrayTiposComprobante` (sin `arrayErrores`) | Tabla de parámetros |
| `consultarTiposEstado` | Estados posibles | solo auth | `.../consultarTiposEstadoReturn` (0..1) `/arrayTiposEstado` (códigos string, sin `arrayErrores`) | Tabla de parámetros |
| `consultarTiposContingencia` | Tipos de contingencia | solo auth | `.../consultarTiposContingenciaReturn/arrayTiposContingencia` | Tabla de parámetros |
| `consultarTiposCategoriaEmisor` | Categorías de emisor | solo auth | `.../consultarCategoriasEmisorReturn/arrayCategoriasEmisor` (string) | Tabla de parámetros |
| `consultarTiposCategoriaReceptor` | Categorías de receptor | solo auth | `.../consultarCategoriasReceptorReturn/arrayCategoriasReceptor` (string) | Tabla de parámetros |
| `consultarGruposCarne` | Grupos de productos | solo auth | `.../consultarGruposCarneReturn/arrayGruposCarne` (string) | Tabla de parámetros |
| `consultarTiposCarne` | Productos de un grupo | `codGrupoCarne` (string) | `.../consultarTiposCarneReturn/arrayTiposCarne` (string, p.ej. `6.24`) | Tabla de parámetros |
| `consultarProvincias` | Provincias | solo auth | `.../consultarProvinciasReturn/arrayProvincias` | Tabla de parámetros |

Detalles de esquema:

- `RemitoType` (generar): `codRemito?`, `tipoComprobante`, `tipoMovimiento` (ENV, REP, RED, PLA), `categoriaEmisor?`, `puntoEmision?`, `cuitTitularMercaderia?`, `cuitDepositario?`, `tipoReceptor?` (MI mercado interno / EM depósito emisor), `categoriaReceptor?`, `cuitReceptor?`, `codDomOrigen?`, `codDomDestino?`, `viaje?` (`cuitTransportista?`, `cuitConductor?`, `fechaInicioViaje`, `distanciaKm`, `vehiculo?` {`dominioVehiculo`, `dominioAcoplado?`}), `arrayMercaderias` (`mercaderia`: `orden` 1-9999, `codTipoProd` string, `tropa?`, `kilos?`, `unidades?`, `kilosRec?`, `unidadesRec?`), `estado?`, `datosEmision?`, `codRemRedestinado?`, `arrayContingencias?`. El mismo tipo sirve de request y de respuesta, así que tiene campos que solo llena ARCA (`estado`, `datosEmision`). Qué pasa si el cliente los manda queda **NO VERIFICADO**.
- Obligatoriedad según el movimiento (manual 2.5.2.4): punto de emisión, fecha de viaje, km, categoría de emisor, transportista y dominio son obligatorios salvo en RED. En RED se exige `codRemRedestinado`. En REP no se informan receptor, categoría ni depósito destino.
- Tipos huérfanos (sin operación): `ConsultarRemitoEmitidoRequestType`/`ResponseType`, `ModificarConductorType`, `EstadoConsultaRecepcionSimpleType`.

## Errores

Tres niveles (manual 1.3, p.9-13):

1. **Excepcionales**: `soap:Fault` (ver Autenticación). Incluyen "errores de estructura (ej: tags sin cerrar, con nombres incorrectos o en orden incorrecto) y de tipos de datos" (1.3.1).
2. **Formato**: `arrayErroresFormato/codigoDescripcionString/{codigo, descripcion}` con códigos Xerces (`cvc-datatype-valid.1.2.1`, `cvc-type.3.1.3`). Si hay errores de formato no se ejecutan las validaciones de negocio y no viene `arrayErrores` (1.3.2).
3. **Negocio**: `resultado` R + `arrayErrores/codigoDescripcion/{codigo, descripcion}`; **observaciones** con `resultado` O + `arrayObservaciones`; anuncios en `evento`.

**Contradicciones manual vs WSDL:**

- Los esquemas genéricos del manual (1.3.3, 1.3.4) muestran `<errores>` y `<observaciones>`; el WSDL dice `arrayErrores` y `arrayObservaciones`. Además ahí `codigo` figura como `string`; en el WSDL `CodigoDescripcionType.codigo` es `xsd:short`. El simulador sigue al WSDL.
- Para `informarContingencia` el manual da `tipoContingencia` como `short`; el WSDL, como `long`.
- `dummy`: el manual 2.5.25.2 muestra `<return>`, y su ejemplo 2.5.25.3 pone `appserver` directo bajo `dummyResponse`. El WSDL y la respuesta real usan `dummyReturn`.

Códigos (los que el manual tabula):

| Código | Mensaje | Fuente |
|---|---|---|
| 100 / 101 / 102 | CUIT no registrada / no activa o con limitaciones / problemas con el domicilio fiscal | Manual 2.3 |
| 140 | La fecha de inicio del viaje no puede ser anterior a la fecha de proceso | 2.5.2.4 |
| 170 | El remito debe encontrarse pendiente de autorización | 2.5.4.4 |
| 2201 | Que la CUIT sea un autorizador válido para el remito | 2.5.4.4 |
| 1205-1208 | En un movimiento que no sea RED debe informar punto de emisión / fecha de inicio de viaje / km / categoría del emisor | 2.5.2.4 |
| 1212-1215 | Titular y depositario incoherentes: emisor no titular desde depositario, depositario = emisor, depósito de origen faltante o sobrante | 2.5.2.4 |
| 1300 | En un movimiento de REDESTINO debe informar el código de remito que está redestinando | 2.5.2.4 |
| 1302-1314 | Depósito destino, CUIT y categoría del receptor obligatorios en ENV/RED, prohibidos en REP; `tipoReceptor` inválido | 2.5.2.4 |
| 1400 / 1401 | Debe informar la CUIT del transportista / el dominio del vehículo. En emitir, 1400 = "Si modifica algún dato del viaje debe informar la cuit del transportista" | 2.5.2.4; 2.5.6.3 |
| 1502-1507 | Código de mercadería inválido; tropa obligatoria para los productos de la "Tabla 1"; kilos obligatorios para tablas 1 a 6; kilos o unidades para tablas 7 o más | 2.5.2.4 |
| 1510 | Si modifica algún dato del viaje debe completar también los datos del vehículo | 2.5.6.3 |
| 1033 / 1034 | `importeCot`: máximo 13 dígitos enteros / máximo 2 decimales | 2.5.3.3 |

Son **unos 32 códigos** en total en el manual. Las tablas salen desalineadas en el texto extraído: la correspondencia código-mensaje de arriba se reconstruyó por orden y hay que confirmarla en el PDF (p.34-35). No hay tabla de errores generales como la de wsremharina (3070, 500, etc.), ni código documentado para un `idReq` duplicado.

## Comportamiento a simular

**Header `info`** (todas las respuestas, incluido `dummy`), no declarado en el WSDL:

```xml
<S:Header><info xmlns="https://ar.gob.afip.wsremcarne/RemCarneService/"><ambiente>Producciï¿½n - FI1 - Versión BETA sujeta a modificaciones</ambiente><fecha>2026-10-02 15:08:35</fecha></info></S:Header>
```

- Namespace **`https://`** (el targetNamespace es `http://`). Al ser namespace por defecto, `ambiente` y `fecha` heredan ese namespace.
- `ambiente` en homologación (2026-10-02) dice "Producción" con mojibake real (bytes `Producci\xc3\xaf\xc2\xbf\xc2\xbdn`). El manual 1.2 muestra "Testing - vii" y "Produccion - bus".
- `fecha`: observado `yyyy-MM-dd HH:mm:ss`. El manual 1.2 muestra `2017-06-22T17:49:06.970-03:00`.

**Ids que asigna ARCA:**

- `codRemito` (long): se asigna al generar (ejemplos del manual: 1811, 8500).
- `datosEmision`: `nroRemito` (1 a 99999999, por emisor + tipo 995 + punto de emisión), `codAutorizacion` (long, "Codigo de autorización asignado al remito (CRE)", manual 3.2), `fechaEmision`, `fechaVencimiento`. El manual de carne no trae ejemplos de CRE. En harina y azúcar los ejemplos tienen 14 dígitos con un patrón (año-1980)(semana)4(correlativo de 9), inferido y **NO VERIFICADO**.
- `qr` (base64Binary) "Para remito … QR para imprimir" (manual 3.2).

**Tipo de comprobante:** único, **995** (remito cárnico; manual 2.5.10, 2.5.12). Los ejemplos usan punto de emisión 9000.

**Idempotencia:** `idReq` (1 a 999999999999999), "debe ser único por Punto de Emisión. Su principal uso es evitar la generación repetida ante un envío por error del mismo comprobante" (2.5.2.1). Manual 1.6: ante falta de respuesta "puede volver a enviar el mismo remito con el mismo <idReq> o puede utilizar los métodos de consulta de Remito". **No hay código documentado** para el duplicado. En wsremharina es el 151 "El ID de request [idRequest] ya existe para el punto de emisión". Qué hace carne ante un `idReq` repetido queda **NO VERIFICADO**. Se consulta con `consultarRemito` por `idReq` + `puntoEmision`.

**Máquina de estados** (manual 2.5.2 a 2.5.9). El manual **no lista** los códigos que devuelve `consultarTiposEstado`; el WSDL solo dice `length=3`. Por lo que dice el texto y por los nombres de wsremharina:

```
generar* ─┬─ emisor ≠ titular .............................. Pendiente de Autorizar por Titular (PAT?)
          ├─ emisor = titular, desde depositario ........... Pendiente de Autorizar por Depositario (PAD?)
          └─ emisor = titular, depósito propio ............. Emitido (EMI?)
pendiente autorización ── autorizarRemito(A) ─► Pendiente de Emisión (PEM?)
pendiente autorización ── autorizarRemito(D) ─► Denegado (DEN?), terminal
no emitido ── anularRemito ─────────────────────► Anulado
PEM ── emitirRemito ────────────────────────────► Emitido
Emitido ── registrarRecepcion(estado) ──────────► ACE / ACP / NAC
Emitido ── informarContingencia ────────────────► Anulado
ACP / NAC / REP ── generarRemito(RED, codRemRedestinado) ─► remito nuevo
no categorizado, vencida la vigencia ───────────► aceptado automáticamente
```

ACE, ACP, NAC y PEN sí están documentados (`EstadoRecepcionSimpleType`, manual 3.1). Los demás códigos (con "?") se toman de wsremharina: **NO VERIFICADO** que carne use los mismos.

**Movimientos** (manual 1.5): Envío común (ENV) a un receptor; Reparto (REP) sin receptor previo, que "deberá completarse generando los movimientos de Redestino necesarios"; Retiro en Planta (PLA), el receptor retira en el domicilio del emisor; Redestino (RED), para mercadería rechazada o de reparto. Los redestinos se vinculan con `codRemRedestinado`.

**Domicilios:** código `0` = domicilio fiscal, válido como destino, y como origen solo si se envía desde un depositario (historial 3.1). Sin depositario, el origen es el domicilio del punto de emisión (comercial).

**Recepción:** si se acepta o rechaza todo, no hace falta el array. Con aceptación parcial se mandan todos los ítems, en cero los no recibidos (2.5.7). `categoriaReceptor` es obligatoria en el request.

**Plazos:** `modificarViaje` dentro de las 24 hs de la emisión y si no fue recibido (2.5.8). El manual no da la tabla de vigencia por distancia (en harina sí): `fechaVencimiento` **NO VERIFICADO** para carne.

**Productos:** `codTipoProd` es un string "tabla.item" (p.ej. `6.24` Cuadril sin tapa, `1.1` Media res con hueso, `13.x` porcinos). Las tablas 1 a 13 están en el manual 2.5.2.4 (p.35-45), y también se obtienen con `consultarGruposCarne` + `consultarTiposCarne`. Hay una lista de códigos con tropa obligatoria (1.1, 2.10-2.26, 3.8-3.25, 4.1, 5.21-5.37, 6.25, 6.43-6.46, 13.18, 13.30).

**Paginación** (`consultarRemitos*`): hasta 2000 por página, ordenados por generación, `nroPagina` + `hayMas`. Rango de fechas de máximo 31 días dentro del mismo año calendario (2.5.12-2.5.14).

**Fechas:** requests `AAAA-MM-DD` sin zona (manual 3.3). Las respuestas de la familia (ejemplos de harina y azúcar) traen la zona (`2019-11-27-03:00`); en carne no hay ejemplo.

**Dummy:** `OK` en los tres campos, con el header `info` (captura 2026-10-02).

## No verificado

- Texto, `faultcode` y HTTP status reales del Fault de autenticación (fwshomo devuelve `BL... 500`).
- Lista real de `consultarTiposEstado` y códigos de estado para pendiente, emitido, anulado y denegado (se asumieron los de wsremharina).
- Comportamiento ante un `idReq` repetido (sin código en el manual).
- Contenido de `arrayResultadoReceptoresValidos` en `RemitoReturnType` (no documentado).
- Efecto de mandar en `generarRemito` los campos de salida (`codRemito`, `estado`, `datosEmision`, `arrayContingencias`).
- `fechaVencimiento` y vigencia por distancia; formato del CRE.
- Valores de las tablas de tipos de contingencia y categorías de emisor y receptor (el manual solo da la forma, sin ejemplos con datos).
- Comportamiento sin SOAPAction o con SOAPAction inválido.
