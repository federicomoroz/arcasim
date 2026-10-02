# wsremazucar

Remito electrónico de azúcar y derivados (el catálogo lo lista como "azúcar, alcohol y subproductos"; el manual habla de unidades en Kg y Lt). Lo usan ingenios y elaboradores que **generan y emiten** el remito; el **titular** de la mercadería, que lo **autoriza** cuando no es el emisor; y el **receptor**, que confirma la recepción. Como novedad frente a los otros remitos agro, el emisor tiene que **convalidar** las recepciones parciales o rechazadas. ARCA asigna un código interno (`codigoRemito`/`codRemito`) y, al emitir, número de comprobante y código de autorización.

Manual: "Remito electrónico azúcar y derivados - Web Service RemAzucarService - Manual para el Desarrollador", **versión 2.0.9 (Versión Borrador)**, historial al 12-08-2021, `https://www.afip.gob.ar/ws/remitoElecAzucar/Manual-DesarrolladorWSREMAZUCAR%20v2_0_9.pdf`. El texto extraído pierde muchos espacios ("esdeltipo…"); las citas son "manual sección N (p.N)" con la página impresa del índice.

Es de la misma familia que `wsremharina` y `wsremcarne` (mismo stack Java, mismo `info` header, mismos `arrayErrores`/`arrayErroresFormato`), pero es **el más distinto de los tres**: no tiene `anularRemito` ni `consultarUltimoRemitoEmitido`, los requests de escritura envuelven los datos en un sub-elemento (`emitirRemito`, `autorizarRemitoTitular`, `modificarConductor`…), los nombres de campo usan `codigoRemito`, la autorización es `S`/`N` (no `A`/`D`), no hay `evento`, el `codigo` de error es `long` y la paginación es distinta. El WSDL tiene además muchos elementos y tipos huérfanos (ver Operaciones).

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Java JAX-WS** (familia "Java" de `catalogo.md` 4.1). Respuesta `S:Envelope` con `<?xml version='1.0' encoding='UTF-8'?>`, raíz del body con prefijo `ns2`, `S:Header` con `info`. `Content-Type: text/xml;charset=utf-8`, `Transfer-Encoding: chunked` | Captura `dummy` 2026-10-02 |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsremazucar/RemAzucarService` (igual en manual 12, p.10) | WSDL `soap:address`; manual |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsremazucar/RemAzucarService` (igual en manual 12) | WSDL de producción; manual |
| targetNamespace | `http://ar.gob.afip.wsremazucar/RemAzucarService/` | WSDL |
| Service / port / binding / portType | `RemAzucarService` / `RemAzucarServiceSOAP` / `RemAzucarServiceSOAP` / `RemAzucarServicePortType` | WSDL |
| WSDL guardado | `docs/arca/wsdl/wsremazucar-homologacion.wsdl` (autocontenido, sin imports). Producción idéntico salvo `soap:address` (diff 2026-10-02) | Descarga 2026-10-02 |
| WSAA service id | `wsremazucar`: "debe enviar el tag service con el valor "wsremazucar"" | Manual 15 (p.11-12) |
| SOAPAction | `targetNamespace + operación`, p.ej. `http://ar.gob.afip.wsremazucar/RemAzucarService/generarRemito` | WSDL (verificado para las 27) |
| SOAP | Solo **SOAP 1.1**, document/literal | WSDL |
| `elementFormDefault` | No declarado → `unqualified` | WSDL; respuesta real |
| Faults / `soap:header` en WSDL | Ninguno | WSDL |
| Operaciones | **27** (portType). El manual documenta las mismas 27 | WSDL; manual 16 |

## Autenticación

- `authRequest` (`AuthRequestType`: `token`, `sign`, `cuitRepresentada` long de 11 dígitos, todos obligatorios), primer hijo del request, en todas las operaciones salvo `dummy`.
- **Excepciones de esquema**: `consultarAduanasRequest`, `consultarRedesOperativasTrenesRequest` y `consultarTiposTitularRequest` están declarados directamente como `AuthRequestType`, así que `token`, `sign` y `cuitRepresentada` van **sueltos bajo el elemento raíz, sin `authRequest`**. Los ejemplos del manual (25, 26 y 21) lo muestran así.
- Manual 15 (p.12): "Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional."
- Error excepcional documentado (manual 5, p.6), con los espacios perdidos en la extracción:

```xml
<S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
<S:Body>
<ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns3="http://www.w3.org/2003/05/soap-envelope">
<faultcode>ns3:Receiver</faultcode>
<faultstring>[wscommon_007]Lafirmanocorrespondealtokenenviado.</faultstring>
</ns2:Fault>
</S:Body>
</S:Envelope>
```

  El mismo ejemplo aparece en harina y carne como "[wscommon_007] La firma no corresponde al token enviado.", con `faultcode` `ns3: Receiver` (namespace de SOAP 1.2). HTTP status no documentado. **Documentado, no observado.**
- Observado en homologación (2026-10-02, `consultarTiposComprobante`): token y sign = `abc`, vacíos, o sin `authRequest` → **`HTTP/1.0 200 OK`, sin `Content-Type`, `Connection: Keep-Alive`, body de texto plano** `BL<13-15 dígitos> <yyyy-MM-dd HH:mm:ss> 500` (ej. `BL8456020992461 2026-10-02 15:08:36 500` con token `abc`). Hipótesis **NO VERIFICADA**: un WAF/F5 delante de fwshomo reemplaza cualquier HTTP 500 (`catalogo.md` 4.1). El Fault real **no se pudo observar**.

Request mínimo válido a nivel esquema:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:ns="http://ar.gob.afip.wsremazucar/RemAzucarService/">
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

`dummy`: Body vacío; despacho por SOAPAction.

## Operaciones

"Ret. remito" = `RemitoReturnType`: **`resultado` primero**, después `remitoDatosAutorizacion?` (`codigoRemito?`, `nroComprobante?`, `idTipoComprobante`, `codigoAutorizacion?`, `fechaEmision?`, `fechaVencimiento`, `estado`), `arrayObservaciones?`, `arrayErrores?`, `arrayErroresFormato?`. "Ret. simple" = `{resultado, arrayErrores?, arrayErroresFormato?}` (sin `codRemito`, sin `evento`).

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Salud | Body vacío | `dummyResponse/dummyReturn` (`appserver`, `authserver`, `dbserver`) | — |
| `generarRemito` | Alta de remito | `idReqCliente` (`IdRequestSimpleType`), `remito` (`RemitoBaseType`) | `generarRemitoResponse/generarRemitoReturn` (Ret. remito) | **Crea** remito con `codigoRemito`. Ejemplo del manual: `resultado` A, `estado` EMI, `nroComprobante` 69, `idTipoComprobante` 997, CRE `39484000071322`. Se lee con `consultarRemito` |
| `autorizarRemitoTitular` | El titular autoriza o deniega | `autorizarRemitoTitular` {`codigoRemito`, `autorizar` (S/N)} | `autorizarRemitoTitularResponse/autorizarRemitoReturn` (Ret. simple) | PAT → PEM (S) / DEN (N) |
| `emitirRemito` | Emite un remito pendiente; puede actualizar transporte | `emitirRemito` {`codigoRemito`, `transporte?` {`conductor` (choice nacional/extranjero), `dominioVehiculo`, `dominioAcoplado?`}, `fechaInicioViaje?`} | `emitirRemitoResponse/emitirRemitoReturn` (Ret. remito) | PEM → EMI |
| `confirmarRecepcionMercaderia` | Receptor acepta o rechaza | `codigoRemito`, `arrayMercaderiaRecibida?` (`mercaderia`: `orden`, `cantidad`), `aceptaRecepcion` (S/N), `observacion?` | `confirmarRecepcionMercaderiaResponse/confirmarRecepcionMercaderiaReturn` (Ret. simple) | EMI → ACE / ACP / NAC |
| `confirmarExportacionMercaderia` | Igual que la recepción, pero para receptor del exterior; la hace **el emisor** | Como la anterior + `numeroDespacho?` (16) | `confirmarExportacionMercaderiaResponse/confirmarRecepcionMercaderiaReturn` | Estado resultante **NO VERIFICADO** (¿EXT?) |
| `convalidarEmisor` | El emisor convalida (S) o no (N) una recepción ACP o NAC | `convalidaRechazoReceptor` {`codigoRemito`, `convalida` (S/N), `observaciones?`} | `convalidarEmisorResponse/convalidarEmisorReturn` (Ret. simple) | ACP/NAC → CON / NCO |
| `corregirConvalidacionEmisor` | Corrige un "No convalidado" | **`CorregirConvalidacionEmisorRequest`** (C mayúscula): `codRemito` | `corregirConvalidacionEmisorResponse/convalidarEmisorReturn` | NCO → CON |
| `modificarConductor` | Cambia conductor, vehículo o acoplado tras emitir | `modificarConductor` {`codRemito`, `conductor`, `dominioVehiculo?`, `dominioAcoplado?`} | `modificarConductorResponse/modificarConductorReturn` (`resultado`, `arrayObservaciones?`, …) | Sin cambio de estado; dentro de 24 hs de emitido y si no fue recibido |
| `informarContingencia` | Contingencia que impide el envío | `informarContingencia` {`codigoRemito`, `tipoContingencia` (int), `arrayMercaderiaPerdida?`, `observaciones` (obligatorio)} | `informarContingenciaResponse/informarContingenciaReturn` (Ret. simple) | "realiza la anulación del remito" (manual 6) → ANU (**NO VERIFICADO** el código) |
| `consultarRemito` | Datos de un remito | Uno de: `codRemito`; `idReqCliente`+`puntoEmision`; `tipoComprobante`+`puntoEmision`+`nroComprobante`+`cuitEmisor` | `.../consultarRemitoReturn` (`resultado`, `remito?` (`RemitoOutputType`), arrays) | Lee lo que crea `generarRemito` |
| `consultarEstadosRemito` | Historial | `codRemito` (único criterio) | `consultarEstadosRemitoResponse/consultarEstadosRemitoReturn/arrayEstadosRemito/historialAcciones` (`fecha`, `estado`, `activo` S/N) | Lee la máquina de estados |
| `consultarRemitosEmisor` | Remitos del emisor | `fechaDesde`, `fechaHasta`, `estado?` (string), `cuitTitular?`, `cuitReceptor?`, `tipoComprobante?`, `numeroPagina?` | `.../consultarRemitosEmisorReturn` | Lista paginada |
| `consultarRemitosTitular` | Remitos del titular | `fechaDesde`, `fechaHasta`, `estado?`, `cuitEmisor?`, `cuitReceptor?`, `tipoComprobante?`, `numeroPagina?` | `.../consultarRemitosTitularReturn` | Lista paginada |
| `consultarRemitosReceptor` | Remitos del receptor | `fechaDesde`, `fechaHasta`, `estado?` (EMI, ACE, ACP, NAC, CON, NCO), `cuitEmisor?`, `tipoComprobante?`, `numeroPagina?` | `.../consultarRemitosReceptorReturn` | Lista paginada |
| `consultarCodigosDomicilio` | Domicilios de un CUIT | `cuitTitularDomicilio` | `.../consultarCodigosDomicilioReturn/arrayDomicilios` | Tabla de parámetros |
| `consultarPuntosEmision` | Puntos habilitados | solo auth | `.../consultarPuntosEmisionReturn/arrayPuntosEmision` | Tabla de parámetros |
| `consultarTiposComprobante` | 997, 998 (exportación) | solo auth | `.../consultarTiposComprobanteReturn/arrayTiposComprobante` | Tabla de parámetros |
| `consultarTiposEstado` | Estados posibles | solo auth | `.../consultarTiposEstadoReturn` (0..1) `/arrayTiposEstado` (códigos string) | Tabla de parámetros |
| `consultarTiposEmbalaje` | Embalajes | solo auth | `.../codigoDescripcionReturn/arrayCodigoDescripcion` | Tabla de parámetros |
| `consultarUnidadesMedida` | Unidades | solo auth | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarTiposMercaderia` | Productos | solo auth | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarTiposContingencia` | Tipos de contingencia | solo auth | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarTiposTitular` | Propia, maquila, fasón | `token`/`sign`/`cuitRepresentada` sueltos | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarPaises` | Código y CUIT país | solo auth | `.../consultarCodigosPaisReturn/arrayPaises/pais` | Tabla de parámetros |
| `consultarAduanas` | Aduanas | sueltos (sin `authRequest`) | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarRedesOperativasTrenes` | Redes ferroviarias | sueltos (sin `authRequest`) | `.../codigoDescripcionReturn` (variante String: `codigoDescripcionString`) | Tabla de parámetros |

Detalles de esquema que un cliente nota:

- `CodigoDescripcionType.codigo` es **`xsd:long`** (en harina y carne es `short`). `ArrayCodigosDescripcionesType` admite **0** elementos (`minOccurs=0`); en harina y carne el mínimo es 1.
- En `ConsultarCodigoDescripcionReturnType` el orden es `arrayCodigoDescripcion`, `arrayErroresFormato`, `arrayErrores`. En `ConsultarRemitosReturnType` el orden es `resultado`, `arrayErrores?`, `arrayErroresFormato?`, `arrayRemitos?` (`remito`: `codigoRemito`, `estadoRemito`, `puntoEmision`, `cuitReceptor?`, `cuitTitularMercaderia?`, `idTipoComprobante`), y **obligatorios** `numeroPagina`, `maxPaginas`, `maxRegistros`. Es otra paginación que la de harina y carne (`nroPagina` + `hayMas`).
- `RemitoBaseType` (generar): `esEntregaMostrador?`, `puntoEmision`, `cuitTitularMercaderia?`, `tipoTitularMercaderia` (1, 2 o 3), `numeroMaquila?` (integer de hasta 20 dígitos), `cuitAutorizadoRetirar?`, `receptor` (`cuitPaisReceptor` + choice `receptorNacional` {`cuitReceptor`, `codDomReceptor?`} / `receptorExtranjero` {`denominacionReceptor`, `domicilioReceptor`, `cuitDespachante`, `codigoAduana`, `numeroFactura?`}), `viaje?` {`fechaInicioViaje`, `kmDistancia?`, `tramo` 1..n con choice `ferroviario` {`redOperativa`, `cuitOperador`, `numeroLocomotora`} / `automotor` {`codPaisTransportista`, choice nacional/extranjero, `dominioVehiculo`, `dominioAcoplado?`} / `ducto` (`S`)}, `arrayMercaderias` (`mercaderia`: `orden`, `anioZafra`, `cantidad` int 1-999999, `tipoProducto`, `unidadMedida`, `tipoEmbalaje`), `importeCot?`, `comentarios?` (250).
- `RemitoOutputType` (consulta) agrega `codRemito`, `idRequest?`, `estado` (string), `cuitEmisor`, `cuitProductorContrato?`, `arrayContingencias?` y `datosAutorizacion?` (`RemitoAutorizacionType`), con `cantidadEnviada`/`cantidadRecibida?`/`cantidadPerdida?` por ítem.
- **Elementos huérfanos** (en el esquema, sin operación en el portType): `autorizarRemitoReceptorRequest/Response` (la autorización del receptor se eliminó en la versión 2.0.0 del manual), `consultarUltimoRemitoRequest/Response`, `consultarRemitoEmitidoRequest/Response`, `consultarRemitoIdRequest` / `consultarRemitoIdResponse`, `consultarRemitosVencidosNoValidadosRequest/Response`, `consultarCUITPaisesRequest/Response`, `consultarRelacionEmisorReceptorRequest/Response` y `autorizarComprobanteRequest/Response` (los dos últimos pares como `xsd:string`), `consultarContingenciaRequest`, `consultarRemito` (`xsd:string`), `convalidarEmisor`, `consultarRemitosEmisor` y `NewOperationResponse` (sin tipo). Un simulador fiel los deja en el WSDL servido, pero no hay operación que los use.

## Errores

Tres niveles (manual 4 a 9, p.6-9):

1. **Excepcionales**: `soap:Fault` (ver Autenticación). Incluyen errores de estructura y de tipos de datos (manual 5).
2. **Formato**: `arrayErroresFormato/codigoDescripcionString/{codigo, descripcion}` con códigos Xerces (`cvc-datatype-valid.1.2.1`, `cvc-type.3.1.3`). Son excluyentes con `arrayErrores` (manual 6).
3. **Negocio**: `resultado` R + `arrayErrores/codigoDescripcion/{codigo (long), descripcion}`; observaciones con `resultado` O + `arrayObservaciones` (solo existen en `generarRemito`, `emitirRemito` y `modificarConductor`). **No hay `evento`** en ningún tipo de retorno de este WSDL, aunque el manual 9 lo describe.

Contradicción manual vs WSDL: los esquemas genéricos (manual 7 y 8) hablan de `<errores>` y `<observaciones>`; el WSDL usa `arrayErrores` y `arrayObservaciones`. El simulador sigue al WSDL.

Códigos generales (manual 14, p.11):

| Código | Mensaje | Aplica a |
|---|---|---|
| 3070 | Operación no permitda (sic) | Violación de acceso a operación |
| 500 | Error [nro. tcket]: si el problema persiste consulte con el administrador o reintente más tarde | Error general |

Códigos de negocio más relevantes (tablas "Validaciones" de cada método):

| Código | Mensaje | Dónde |
|---|---|---|
| 151 | El ID de request [id. Request] ya existe para el punto de emisión [nro. punto emisión] | generarRemito |
| 120 | Se encuentra otra transacción activa operando sobre los datos informados | generarRemito, emitirRemito |
| 130 | La fecha del comprobante no se corresponde con la del próximo a autorizar | generarRemito, emitirRemito |
| 140 / 141 | Fecha de inicio de viaje anterior a hoy / posterior a la fecha de entrega | generarRemito, emitirRemito |
| 1000 | Debe informar este valor | recepción, exportación |
| 7000 / 7001 / 7006 / 7013 | CUIT limitada / problemas con el domicilio fiscal / no activo en Ganancias-IVA o Monotributo / sin domicilio fiscal electrónico | generarRemito (emisor, titular, receptor, transportista, chofer) |
| 7002 | No posee declarada actividad de Elaboración de Azúcar | generarRemito |
| 7008 | El contrato ingresado no concuerda con un contrato registrado en AFIP | `numeroMaquila` |
| 7014 | Usted posee remitos electrónicos pendientes de convalidación o "No convalidados" con el receptor de la mercadería | generarRemito |
| 7016 | Punto de emisión inexistente o inválido | generarRemito |
| 7100 / 7101 | La unidad de medida debe ser Kg. / Lt. | mercadería |
| 7102 | El año no puede ser posterior al actual, ni anterior a 10 años | `anioZafra` |
| 7104 / 7111 | "Solo se permite una sola combinación 'Año zafra' - 'Tipo producto' - 'Tipo embalaje' por mercadería de remito" y "El año zafra informado no corresponde con el año de la maquila". El texto no deja claro cuál código va con cuál mensaje (7111 se agregó en la 2.0.9) | mercadería |
| 7005 | El receptor tiene remitos electrónicos emitidos con su CUIT, pendientes de aceptación | generarRemito |
| 7106-7108 | No existe mercadería para recibir / el valor debe ser mayor a 0 / no debe superar la cantidad emitida | recepción, exportación |
| 7109 / 7110 | Cantidad perdida o remanente excedida | informarContingencia |
| 7015 / 7022 | Valor informado inválido / repetido | varios |
| 7017-7021 | Reglas del rango de fechas y "No se han encontrado remitos" | consultarRemitos* |
| 7023 | Debe informar algún dato del chofer, vehículo o acoplado | modificarConductor |

Son **unos 40 códigos**. Las tablas salen desalineadas en el texto extraído; hay que confirmar la correspondencia código-mensaje en el PDF (p.17-19, 25, 30-31, 36, 61, 79). El 7019 aparece con dos mensajes distintos: "No se han encontrado datos del domicilio del receptor" en generarRemito y una de las reglas de rango de fechas en las consultas.

## Comportamiento a simular

**Header `info`** (todas las respuestas, incluido `dummy`), no declarado en el WSDL:

```xml
<S:Header><info xmlns="https://ar.gob.afip.wsremazucar/RemAzucarService/"><ambiente>Producciï¿½n - FI1 - Versión BETA sujeta a modificaciones</ambiente><fecha>2026-10-02 15:08:36</fecha></info></S:Header>
```

- Namespace **`https://`** (el targetNamespace es `http://`). Por ser namespace por defecto, `ambiente` y `fecha` quedan en él.
- `ambiente` en homologación (2026-10-02): "Producción" con mojibake real (bytes `Producci\xc3\xaf\xc2\xbf\xc2\xbdn`). El ejemplo del manual (2019) dice "Testing - SS6 - Versión BETA sujeta a modificaciones" (espacios perdidos en la extracción).
- `fecha`: observado `yyyy-MM-dd HH:mm:ss`. El manual 3 muestra `2017-06-22T17:49:06.970-03:00`, pero su ejemplo de `generarRemito` usa el formato observado.

**Ids que asigna ARCA:**

- `codigoRemito` (long): id interno, se asigna en `generarRemito`. Ejemplo: 8732. Se llama `codigoRemito` en `remitoDatosAutorizacion` y en los requests envueltos, y `codRemito` en `consultarRemito`, `consultarEstadosRemito`, `corregirConvalidacionEmisor`, `modificarConductor` y `RemitoOutputType`.
- Al emitir: `nroComprobante` (long; en `consultarRemito` filtra como `NumeroRemitoSimpleType`, 1 a 99999999), `idTipoComprobante` (997), `codigoAutorizacion` (long, 14 dígitos en el ejemplo: `39484000071322`), `fechaEmision`, `fechaVencimiento`. Con los ejemplos de wsremharina, el patrón (año-1980)(semana ISO)4(correlativo de 9 dígitos) encaja también acá: 2019-11-27 → `3948`. Inferencia **NO VERIFICADA**.
- No hay `qr` en las respuestas de este WSDL (sí en harina y carne).

**Tipos de comprobante:** 997 y 998 "(Exp)" (manual 17 y 18). El ejemplo emite un 997 con receptor nacional; se supone que 998 corresponde a receptor extranjero (**NO VERIFICADO**).

**Idempotencia:** `idReqCliente` (1 a 999999999999999) "debe ser único por Punto de Emisión. Su principal uso es evitar la generación repetida ante un envío por error del mismo comprobante" (manual 16.1). El error 151 rechaza el duplicado. El manual 11 recomienda reenviar "el mismo remito con el mismo <idReq>" (otro nombre de campo) o consultar. Qué responde ante el duplicado (¿error 151 o el remito original?) queda **NO VERIFICADO**; el cliente debería usar `consultarRemito` por `idReqCliente` + `puntoEmision`. En la respuesta de consulta el campo se llama `idRequest`.

**Máquina de estados.** El manual no trae ejemplo de `consultarTiposEstado`. El WSDL enumera los estados en `EstadoRemitoType` (usado como filtro del huérfano `consultarUltimoRemitoRequest`): **BOR, PAT, PAR, EXT, PEM, EMI, ANU, DEN, ACE, ACP, NAC, CON, NCO**. Los filtros del receptor (`EstadoRemReceptorSimpleType`) son EMI, ACE, ACP, NAC, CON y NCO. Significados documentados: ACE/ACP/NAC (manual 17.a), NCO "No Convalidado" y CON "Convalidado" (manual 14 y 23). PAT, PEM, EMI, DEN y ANU se interpretan como en wsremharina; BOR (¿borrador?), PAR (¿pendiente de autorizar por el receptor, eliminado en la 2.0.0?) y EXT (¿exportado totalmente?) quedan **NO VERIFICADOS**.

```
generarRemito ─┬─ emisor ≠ titular ............. PAT
               └─ emisor = titular ............. EMI  (ejemplo del manual: resultado A, estado EMI)
PAT ── autorizarRemitoTitular(S) ─► PEM ── emitirRemito ─► EMI
PAT ── autorizarRemitoTitular(N) ─► DEN  (terminal)
EMI ── confirmarRecepcionMercaderia(aceptaRecepcion, ítems) ─► ACE / ACP / NAC
EMI ── confirmarExportacionMercaderia (emisor, receptor extranjero) ─► ¿EXT? (NO VERIFICADO)
ACP / NAC ── convalidarEmisor(S) ─► CON     ACP / NAC ── convalidarEmisor(N) ─► NCO
NCO ── corregirConvalidacionEmisor ─► CON
EMI ── informarContingencia ─► anulado (ANU, NO VERIFICADO)
```

El manual 16.1 también dice que si la mercadería sale "desde un depósito de un tercero el remito quedará Pendiente de Autorizar", pero este WSDL no tiene depositario ni autorización del depositario. Es texto heredado de carne.

**Recepción:** si se acepta o rechaza todo, el array no hace falta. Con aceptación parcial se mandan todos los ítems (`orden`, `cantidad`), en cero los no recibidos (manual 16.4). Tope: la cantidad emitida (7106-7108). Mientras haya remitos ACP/NAC sin convalidar con un receptor, el emisor no puede generarle remitos nuevos (7014).

**Titularidad** (manual 10): 1 Propia, 2 Producto por contrato de maquila, 3 Tercero por servicio de fasón (orden de los códigos inferido del texto; confirmarlo con `consultarTiposTitular`, **NO VERIFICADO**). Con maquila se valida `numeroMaquila` contra los contratos registrados (7008) y el año de zafra contra el de la maquila (7104 o 7111).

**Plazos:** `modificarConductor` dentro de las 24 hs de la emisión y si no fue recibido (manual 16.5). La vigencia no está documentada; el ejemplo da emisión 2019-11-27 y vencimiento 2019-12-01 con 100 km (+4 días, no coincide con la tabla de harina). **NO VERIFICADO**.

**Paginación** (`consultarRemitos*`): rango obligatorio de máximo 31 días dentro del mismo año (7017-7020); hasta 2000 por página, ordenados por generación; la respuesta trae `numeroPagina`, `maxPaginas` y `maxRegistros` (los tres obligatorios en el XSD, aun cuando hay error).

**Fechas:** requests `AAAA-MM-DD` sin zona (manual 18.a). Respuestas con zona: `2019-11-27-03:00`.

**Dummy:** `OK` en los tres campos, con el header `info` (captura 2026-10-02). El manual 27 muestra `<return>` en el esquema y `appserver` directo bajo `dummyResponse` en el ejemplo. El WSDL y la respuesta real usan `dummyReturn`.

**Contradicciones manual vs WSDL** (el simulador sigue al WSDL):

- La introducción del manual (sección 2) lista "Anular Remito no Emitido", "Remitos Autorizador", "Tipos Categoría Emisor/Receptor" y "Modificar Viaje": no existen en este WSDL (texto copiado de carne).
- El ejemplo de `generarRemito` manda `cuitProductorContrato` (no está en `RemitoBaseType`, solo en `RemitoOutputType`) y omite `orden` en la mercadería (obligatorio en `MercaderiaAltaType`). Tampoco valida contra el XSD.
- `confirmarExportacionMercaderia`: la tabla del manual dice `codRemito`; el WSDL y el ejemplo, `codigoRemito`.
- La respuesta de ejemplo de `confirmarRecepcionMercaderia` usa el elemento `confirmarExportacionMercaderiaResponse` (copia del otro método).
- `consultarEstadosRemito`: la tabla del manual invierte los tipos de `arrayErrores` y `arrayErroresFormato`; el XSD manda `arrayEstadosRemito` (obligatorio), `arrayErroresFormato?`, `arrayErrores?`.

## No verificado

- Texto, `faultcode` y HTTP status reales del Fault de autenticación (fwshomo devuelve `BL... 500`).
- Lista real de `consultarTiposEstado` y significado de BOR, PAR y EXT; estado tras `informarContingencia` y tras `confirmarExportacionMercaderia`.
- Respuesta ante un `idReqCliente` repetido.
- Si 998 es el comprobante de exportación y qué determina 997 vs 998.
- Vigencia (`fechaVencimiento`) y formato del CRE.
- Códigos de `consultarTiposTitular` (1/2/3 inferidos del texto), unidades de medida, productos y contingencias: el manual no trae valores.
- Correspondencia exacta código-mensaje en las tablas de validación (texto extraído desalineado).
- Comportamiento sin SOAPAction o con SOAPAction inválido.
