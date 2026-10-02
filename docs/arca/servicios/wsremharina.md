# wsremharina

Remito electrónico harinero: el comprobante que acompaña el traslado de harinas de trigo y subproductos de la molienda (afrechillo, sémolas, premezclas, etc.). Lo usan molinos, usuarios de molienda y mayoristas/depósitos de harina que **generan** el remito; el **titular** de la mercadería o el **depositario** que lo **autorizan** cuando no son el emisor; y el **receptor**, que registra la recepción. ARCA asigna un código interno (`codRemito`) al generar y, al emitir, un número de comprobante por punto de emisión más un código de autorización ("CRE").

Manual: "Remito Electrónico Harinero - Web Service RemHarinaService - Manual para el Desarrollador", **versión 2.9 del 25/09/2024**, `https://www.afip.gob.ar/ws/remitoHTSDMT/Manual_Desarrollador_WSREMHARINA_v2.9.pdf`. Las citas "manual X.Y (p.N)" usan la numeración de secciones y la página impresa del índice.

Es de la misma familia que `wsremcarne` y `wsremazucar` (mismo stack Java, mismo `info` header, mismo esquema de errores `arrayErrores`/`arrayErroresFormato`, mismo ciclo generar → autorizar → emitir → recepción). Cada ficha es autocontenida; las diferencias se marcan donde importan.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Java JAX-WS** (familia "Java" de `catalogo.md` 4.1). Sobre de respuesta `S:Envelope` con `<?xml version='1.0' encoding='UTF-8'?>` (comillas simples), raíz del body con prefijo `ns2`, `S:Header` con `info` propio. `Content-Type: text/xml;charset=utf-8`, `Transfer-Encoding: chunked` | Captura `dummy` 2026-10-02 |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsremharina/RemHarinaService` (igual en manual 2.1, p.16) | WSDL `soap:address`; manual |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsremharina/RemHarinaService` (igual en manual 2.1) | WSDL de producción; manual |
| targetNamespace | `http://ar.gob.afip.wsremharina/RemHarinaService/` | WSDL |
| Service / port / binding / portType | `RemHarinaService` / `RemHarinaServiceSOAP` / `RemHarinaServiceSOAP` / `RemHarinaServicePortType` | WSDL |
| WSDL guardado | `docs/arca/wsdl/wsremharina-homologacion.wsdl` (autocontenido, sin imports). El de producción es idéntico salvo `soap:address` (diff 2026-10-02) | Descarga 2026-10-02 |
| WSAA service id | `wsremharina`: "debe enviar el tag service con el valor "wsremharina"" | Manual 2.4 (p.17) |
| SOAPAction | `targetNamespace + operación`, p.ej. `http://ar.gob.afip.wsremharina/RemHarinaService/generarRemito` | WSDL (verificado para las 29) |
| SOAP | Solo **SOAP 1.1**, document/literal | WSDL |
| `elementFormDefault` | No declarado → `unqualified`: solo el elemento raíz del body lleva namespace; `authRequest`, `token`, `codRemito`, etc. van sin namespace | WSDL; respuesta real |
| Faults en WSDL | Ninguna operación declara `wsdl:fault` | WSDL |
| `soap:header` en WSDL | No hay. El `info` de la respuesta no está declarado | WSDL |
| Operaciones | **29** | WSDL |

## Autenticación

- Contenedor `authRequest` (tipo `AuthRequestType`) como **primer hijo** del elemento request, con `token` (string), `sign` (string) y `cuitRepresentada` (`CuitSimpleType`: `xsd:long`, `> 9999999999` y `<= 99999999999`, o sea 11 dígitos). Todos obligatorios.
- Va en todas las operaciones salvo `dummy`. **Excepción de esquema**: `consultarAduanasRequest` está declarado directamente como `AuthRequestType`, así que `token`, `sign` y `cuitRepresentada` van **sueltos bajo `consultarAduanasRequest`, sin `authRequest`** (el ejemplo del manual 2.5.28 coincide).
- Manual 2.4 (p.17): "Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional."
- Forma documentada del error excepcional (manual 1.3.1, p.8), textual:

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

  Ojo: el `faultcode` usa el namespace de SOAP 1.2 (`Receiver`) dentro de un sobre 1.1, y trae un espacio después de `ns3:`. El HTTP status no se documenta. **Documentado, no observado.**
- Observado en homologación (2026-10-02, `consultarTiposComprobante`): con token y sign = `abc`, con token y sign vacíos, y sin `authRequest`, la respuesta fue siempre **`HTTP/1.0 200 OK`, sin `Content-Type`, `Connection: Keep-Alive`, body de texto plano** `BL<13-15 dígitos> <yyyy-MM-dd HH:mm:ss> 500` (ej. `BL813330677716 2026-10-02 15:08:34 500`, `Content-Length: 38`). Es el mismo fenómeno que `catalogo.md` 4.1 vio en wsmtxca. Hipótesis **NO VERIFICADA**: un WAF/F5 delante de fwshomo reemplaza cualquier HTTP 500 (o sea, cualquier Fault). Por eso el texto real del Fault de autenticación **no se pudo observar**.

Request mínimo válido a nivel esquema (el mismo que se usó en la captura `token-abc`):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:ns="http://ar.gob.afip.wsremharina/RemHarinaService/">
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

`dummy`: el mensaje de entrada no tiene parts, el Body va **vacío**; el servidor solo puede despachar por SOAPAction.

## Operaciones

Notación de salida: `elementoResponse/hijo`. "Ret. operación" = `OperacionReturnType` (`codRemito` obligatorio, `resultado` A/O/R, `evento?`, `arrayObservaciones?`, `arrayErrores?`, `arrayErroresFormato?`). "Ret. remito" = `RemitoReturnType` (`remitoOutput?`, `resultado`, `evento?`, `arrayObservaciones?`, `arrayErrores?`, `arrayErroresFormato?`).

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Salud de app, auth y base | Body vacío | `dummyResponse/dummyReturn` (`appserver`, `authserver`, `dbserver`) | — |
| `generarRemito` | Alta de remito | `generarRemitoRequest`: `authRequest`, `idReqCliente`, `remito` (`RemitoBaseType`) | `generarRemitoResponse/generarRemitoReturn` (Ret. remito) | **Crea** remito: asigna `codRemito`; queda PAT, PAD o EMI (ver máquina de estados). Si queda EMI trae `datosAutAFIP` (nro + CRE) y `qr`. Se lee con `consultarRemito` por `codRemito` o por `idReqCliente`+`puntoEmision` |
| `autorizarRemito` | Titular o depositario autoriza (`A`) o deniega (`D`) | `codRemito`, `estado` (`AutorizacionSimpleType` A/D) | `autorizarRemitoResponse/operacionReturn` | PAT/PAD → PEM (o al otro pendiente) / DEN. Clave `codRemito` |
| `anularRemito` | Emisor anula un remito no emitido | `codRemito`, `observacion?` (3-250) | `anularRemitoResponse/operacionReturn` | → anulado (código exacto ANS o ANU: **NO VERIFICADO**). Clave `codRemito` |
| `emitirRemito` | Emite un remito pendiente de emitir; puede actualizar el viaje | `codRemito`, `viaje?` (`ViajeType`) | `emitirRemitoResponse/emitirRemitoReturn` (Ret. remito) | PEM → EMI; asigna `nroRemito`, `codAutorizacion`, fechas y `qr` |
| `registrarRecepcion` | Receptor acepta total/parcial o rechaza | `codRemito`, `fecha`, `aceptado` (S/N), `arrayRecepcionMercaderia?` (`recepcionMercaderia`: `orden`, `pesoNetoKG`) | `registrarRecepcionResponse/operacionReturn` | EMI → ACE / ACP / NAC (el sistema calcula total o parcial según los ítems). Clave `codRemito` |
| `modificarViaje` | Cambia transportista/vehículo tras emitir | `codRemito`, `viaje` | `modificarViajeResponse/operacionReturn` | Sin cambio de estado; plazo según distancia (24 a 240 hs) |
| `informarContingencia` | Pérdida, demora o anulación en viaje | `codRemito`, `contingencia` (`codTipoContingencia`, `fecha`, `arrayMercaderiaPerdida?`, `observacion?`) | `informarContingenciaResponse/operacionReturn` | Puede anular (→ ANU, **NO VERIFICADO**) o extender validez 1 día (demora). Se ve en `remitoOutput/arrayContingencias` |
| `registrarReingreso` | Emisor declara el reingreso a planta de lo no entregado | `idReqCliente`, `codRemito` | `registrarReingresoResponse/operacionReturn` | Sobre ACP, NAC o remitos de reparto; cierra redestinos/contingencias. Estado resultante **NO VERIFICADO** |
| `registrarRedestino` | Emisor redestina mercadería no aceptada a otro receptor | `idReqCliente`, `codRemito`, `cuitReceptor`, `tipoDomReceptor` (1 o 3), `codDomReceptor`, `arrayRedestinoMercaderia` | `registrarRedestinoResponse/registrarRedestinoReturn` (Ret. remito) | **Crea un remito nuevo** `tipoMovimiento` RED con otro `codRemito` (ejemplo: 8398 → 8399) |
| `registrarExportacion` | Confirmación de exportación (no está en el manual) | Igual que `registrarRecepcion`: `codRemito`, `fecha`, `aceptado`, `arrayRecepcionMercaderia?` | `registrarExportacionResponse/operacionReturn` | Probablemente → EXT/EXP/EXR. **NO VERIFICADO** |
| `consultarUltimoRemitoEmitido` | Último remito emitido o exportado por tipo y punto | `tipoComprobante`, `puntoEmision` | `consultarUltimoRemitoEmitidoResponse/consultarUltimoRemitoReturn` (`ConsultarRemitoReturnType`) | Devuelve el **remito completo** (`remitoOutput`), no solo el número. Clave (cuit, `tipoComprobante`, `puntoEmision`) |
| `consultarRemito` | Datos de un remito | Uno de: `codRemito`; `idReqCliente`+`puntoEmision`; `tipoComprobante`+`puntoEmision`+`nroComprobante`+`cuitEmisor` | `consultarRemitoResponse/consultarRemitoReturn` (`remitoOutput?`, `evento?`, arrays; **sin `resultado`**) | Lee lo creado por generar/emitir/redestino |
| `consultarEstadosRemito` | Historial de estados | Mismo request que `consultarRemito` (`ConsultarRemitoRequestType`) | `consultarEstadosRemitoResponse/estadosRemitoReturn` (`codRemito?`, `arrayEstados/estados`: `estado`, `fecha`, `cuitUsuario`, `cuitDesc`) | Lee la máquina de estados |
| `consultarRemitosEmisor` | Remitos del emisor | `rangoFecha` (`fechaDesde`, `fechaHasta`), `ptoEmision`, `tipoComprobante?`, `estado?`, `nroPagina?` | `.../consultarRemitosReturn` (`arrayRemitos/infoRemito`, `nroPagina?`, `hayMas?`) | Lista; ojo `ptoEmision` y `rangoFecha` (no `puntoEmision`/`rangoFechas`) |
| `consultarRemitosAutorizador` | Remitos donde el CUIT es titular o depositario | `rolAutorizador` (TIT/DEP), `estadoAutorizacion` (PE/AU/RE), `rangoFecha?`, `cuitEmisor?`, `nroPagina?` | `.../consultarRemitosReturn` | Lista |
| `consultarRemitosReceptor` | Remitos donde el CUIT es receptor | `estadoRecepcion` (PEN/ACE/ACP/NAC), `cuitEmisor?`, `rangoFecha?`, `nroPagina?` | `.../consultarRemitosReturn` | Lista |
| `consultarReceptoresValidos` | Indica qué CUIT no pueden recibir remitos nuevos | `arrayReceptores/receptores/cuitReceptor` (1..n) | `.../consultarReceptoresValidosReturn` (`resultado`, arrays) | Consulta (error 2602 por CUIT inhabilitada) |
| `consultarCodigosDomicilio` | Domicilios de un CUIT | `cuitTitularDomicilio` | `.../consultarCodigosDomicilioReturn/arrayDomicilios` | Tabla de parámetros |
| `consultarPuntosEmision` | Puntos de emisión habilitados | solo auth | `.../consultarPuntosEmisionReturn/arrayPuntosEmision` | Tabla de parámetros |
| `consultarTiposComprobante` | 993 automotor / 994 ferroviario | solo auth | `.../codigoDescripcionReturn/arrayCodigoDescripcion` | Tabla de parámetros |
| `consultarTiposEstado` | Estados posibles | solo auth | `.../codigoDescripcionReturn/arrayCodigoDescripcion/codigoDescripcionString` (códigos string) | Tabla de parámetros |
| `consultarTiposContingencia` | Tipos de contingencia | solo auth | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarTiposMercaderia` | Productos bajo control | solo auth | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarUnidadesVenta` | Unidades de venta | solo auth | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarTiposEmbalaje` | Embalajes | solo auth | `.../codigoDescripcionReturn` | Tabla de parámetros |
| `consultarPaises` | Código y CUIT país | solo auth | `.../consultarCodigosPaisReturn/arrayPaises/pais` (`codigo`, `cuit`, `nombre`, `tipoSujeto`) | Tabla de parámetros |
| `consultarProvincias` | Provincias | solo auth | `.../consultarProvinciasReturn/arrayProvincias` (códigos string) | Tabla de parámetros |
| `consultarAduanas` | Aduanas | `token`, `sign`, `cuitRepresentada` sueltos | `.../codigoDescripcionReturn` | Tabla de parámetros |

Detalles de esquema que un cliente nota:

- En `ConsultarCodigoDescripcionReturnType` (y su variante String) el orden es `arrayCodigoDescripcion`, **`arrayErroresFormato`, `arrayErrores`** (formato antes que negocio). En los demás tipos de retorno es `arrayErrores` y después `arrayErroresFormato`.
- `RemitoOutputType`: `codRemito`, `idReqCliente?`, `cuitEmisor`, `remito` (`RemitoBaseType` tal cual se generó), `datosAutAFIP?`, `estadoRemito`, `qr?` (base64Binary), `arrayLeyendas?` (`leyenda`: `codLeyenda`, `descripcion`, `grupo` EMI/TIT/DEP/DES/OPE/TRA), `arrayContingencias?`, `fechaAut?`, `fechaRec?`.
- `RemitoBaseType` (generar): `tipoMovimiento` (ENV, CAN, RET, RED, REP), `tipoCmp?` (si no se informa "el sistema lo calcula", manual 3.6), `esEntregaMostrador?`, `esMercaderiaEnConsignacion?`, `tipoEmisor` (U/I/M), `rucaEstEmisor?`, `puntoEmision`, `cuitTitular`, `depositario` (`tipoDepositario` E/I/D, …), `receptor?` (`cuitPaisReceptor` + choice `receptorNacional` / `receptorNacionalNoCateg` / `receptorExtranjero`), `viaje?`, `arrayMercaderia`, `codRemRedestinar?`, `reingresado?`, `importeCot?`, `observaciones?`. Según el manual (cambios 2.9), CAN ya no está disponible y en REP el receptor no se informa.
- Elementos huérfanos en el esquema: `registrarReingreso` (elemento sin tipo, no usado por ninguna operación) y un `xsd:group NewGroupDefinition` vacío. No afectan el contrato.

## Errores

Tres niveles (manual 1.3, p.8-11):

1. **Excepcionales** (auth, estructura): `soap:Fault`. Ver Autenticación. En fwshomo se observa la página `BL... 500` en su lugar.
2. **Formato**: `arrayErroresFormato/codigoDescripcionString/{codigo, descripcion}` (códigos string tipo Xerces). Ejemplo del manual: `cvc-datatype-valid.1.2.1` "'?' no es un valor válido para un tipo de dato entero." y `cvc-type.3.1.3` "El valor '?' en el elemento ' cuitTitularMercaderia' no es válido.". "De no superar alguna de las validaciones de formato, el WS devolverá el arrayErroresFormato y no continuará con las validaciones de negocio, por lo cual no existirá el elemento arrayErrores. Son excluyentes." (1.3.2).
3. **Negocio**: `resultado` = `R` y `arrayErrores/codigoDescripcion/{codigo (short), descripcion}`. **Observaciones** (no rechazan): `resultado` = `O` y `arrayObservaciones/codigoDescripcion` (ej. real del manual: código 1404 "Emisor: es un Industrial de Molienda de Harina…").

Además, `evento` (`CodigoDescripcionType`) informa anuncios programados (manual 1.4).

**Contradicción manual vs WSDL**: los esquemas genéricos del manual (1.3.3 y 1.3.4) muestran los arrays como `<errores>` y `<observaciones>`; el WSDL (y los ejemplos concretos del propio manual) usan `arrayErrores` y `arrayObservaciones`. El simulador sigue al WSDL.

También queda ambiguo dónde caen los errores de tipo de dato: 1.3.1 dice que son excepcionales (Fault) y 1.3.2 los muestra en `arrayErroresFormato`. Hipótesis **NO VERIFICADA**: XML mal formado → Fault; XML bien formado que no valida contra el XSD → `arrayErroresFormato` con HTTP 200.

Códigos generales:

| Código | Mensaje | Fuente |
|---|---|---|
| 100 | CUIT debe encontrarse en el Sistema Registral | Manual 2.3 |
| 101 | Debe encontrarse activa y sin limitaciones | Manual 2.3 |
| 102 | No debe registrar inconvenientes con su domicilio fiscal | Manual 2.3 |
| 3070 | Operación no permitida | Manual 2.5.2 |
| 1000 | Debe informar este valor [campo obligatorio] | Manual 2.5.2 |
| 120 | Se encuentra otra transacción activa operando sobre los datos informados | Manual 2.5.2 |
| 160 | Remito no encontrado o inválido [codRemito] | Manual 2.5.2 |
| 500 | Error [ticketId]: si el problema persiste consulte con el administrador o reintente más tarde | Manual 2.5.2 |
| 151 | El ID de request [idRequest] ya existe para el punto de emisión [puntoEmision] | Manual 2.5.3.4 |
| 152 | ID de request inválido | Manual 2.5.3.4 |
| 3022 | Remito no encontrado (autorizar, anular, consultas por remito) | Manual 2.5.4.4 y siguientes |
| 3034 | Remitos no encontrados (consultas de listas) | Manual 2.5.14.5 |
| 140 / 141 | La fecha de inicio del viaje no puede ser anterior a hoy / posterior a la fecha de entrega | Manual 2.5.6.5 |
| 5540 | Para pérdida total debe informar una contingencia del tipo "pérdida Total ... que ocasiona anulación del Remito" | Manual 2.5.9.5 |
| 2602 | Actualmente, la CUIT X NO está habilitada para ser destinatario de nuevos remitos de mercadería | Ejemplo 2.5.29.6 |

Los códigos de negocio por operación están en las secciones "Validaciones excluyentes" de cada método (2.5.3.4 a 2.5.25.5, p.25-104): son **unos 47 distintos** (100-160, 1000, 1570, 3001-3034, 5002, 5003, 5540). Las tablas del PDF salen desalineadas en el texto extraído, así que la correspondencia campo-código hay que leerla en el PDF.

## Comportamiento a simular

**Header `info`** (todas las respuestas, incluido `dummy`): no está en el WSDL.

```xml
<S:Header><info xmlns="https://ar.gob.afip.wsremharina/RemHarinaService/"><ambiente>Producciï¿½n - FI1 - Versión BETA sujeta a modificaciones</ambiente><fecha>2026-10-02 15:08:33</fecha></info></S:Header>
```

- Namespace **`https://`** (el targetNamespace es `http://`). Como es namespace por defecto, `ambiente` y `fecha` también quedan en ese namespace.
- `ambiente` en homologación (2026-10-02) dice "Producción" con mojibake real: bytes `Producci\xc3\xaf\xc2\xbf\xc2\xbdn`. Los ejemplos del manual muestran el mismo mojibake ("Producciï¿½n - WS4 - Versión BETA…", 2020) y otros valores ("Testing - VII", "Desarrollo - WS8").
- `fecha`: observado `yyyy-MM-dd HH:mm:ss` (hora local). El manual 1.2 muestra `2018-06-22T17:49:06.970-03:00`, pero los ejemplos de cada método usan el formato observado.
- El cliente no está obligado a procesarlo (manual 1.2).

**Ids que asigna ARCA:**

- `codRemito` (long): id interno, se asigna en `generarRemito` y en `registrarRedestino`. En los ejemplos son números de 4 dígitos que crecen con el tiempo y no se reinician por punto de emisión (8314 en 2019-10, 8399 en 2019-10, 9297 en 2020-05). Que sea un correlativo global es una inferencia **NO VERIFICADA**.
- `datosAutAFIP` (solo cuando el remito queda emitido): `nroRemito` (1 a 99999999, correlativo por CUIT emisor + `tipoCmp` + punto de emisión), `codAutorizacion` (long, el "CRE", manual 3.6), `fechaEmision`, `fechaVencimiento`.
- Los cinco CRE de ejemplo del manual (39414000068689, 39424000068763, 40224000075841, 40224000075854, y 39484000071322 en wsremazucar) tienen 14 dígitos y encajan con `(año - 1980)` + semana ISO (2 dígitos) + `4` + correlativo de 9 dígitos (2019-10-09 → `3941`; 2020-05-29 → `4022`). Es una inferencia sobre ejemplos, **NO VERIFICADA**.
- `qr`: base64Binary de una imagen JPEG (los ejemplos empiezan con `/9j/4AAQSkZJRg`), solo si quedó emitido.

**Tipos de comprobante:** 993 "Remito Electrónico Harinero Automotor" y 994 "Remito Electrónico Harinero Ferroviario" (manual 2.5.19). Si `tipoCmp` no viene, ARCA lo deduce del vehículo.

**Idempotencia:** `idReqCliente` (1 a 999999999999999) es "único por Punto de Emisión. Su principal uso es evitar la generación repetida ante un envío por error del mismo comprobante" (2.5.3.1). El manual 1.6 dice que ante falta de respuesta "puede volver a enviar el mismo remito con el mismo <idReqCliente> o puede utilizar los métodos de consulta". Pero la validación 151 rechaza un `idReqCliente` repetido para el punto de emisión. Qué devuelve exactamente un reenvío (el remito original o el error 151) queda **NO VERIFICADO**; la forma segura para un cliente es `consultarRemito` por `idReqCliente` + `puntoEmision`. `registrarReingreso` y `registrarRedestino` también llevan `idReqCliente`.

**Máquina de estados.** Códigos del ejemplo de `consultarTiposEstado` (manual 2.5.20.3.2), en el orden en que aparecen:

| Código | Descripción |
|---|---|
| EMI | Emitido |
| VEN | Vencido |
| PAD | Pendiente de Autorizar por Depositario |
| EXO | Exportado |
| PAT | Pendiente de Autorizar por Titular |
| EXP | Exportado Parcialmente |
| ANS | Anulado sin emisión |
| NFI | No finalizado |
| NAC | No Aceptado |
| ANUR | Anulado por Redestino |
| ACP | Aceptado Parcialmente |
| BOR | Borrador |
| EXT | Exportado Totalmente |
| PEM | Pendiente de Emitir |
| ACE | Aceptado |
| ANU | Anulado |
| DEN | Denegado |
| EXR | Exportación Rechazada |

`EstadoRemitoSimpleType` es `xsd:string length=3`: **`ANUR` (4 caracteres) no valida contra el propio esquema**. Un remito en ese estado no se podría devolver en `estadoRemito` sin violar el XSD.

Transiciones documentadas (manual 2.5.3 a 2.5.10, 2.5.25):

```
generarRemito ─┬─ emisor ≠ titular ............................ PAT
               ├─ emisor = titular, depósito de un tercero ..... PAD
               └─ emisor = titular, depósito propio ............ EMI  (trae datosAutAFIP y qr)
PAT / PAD ── autorizarRemito(A) ─► PEM (o al otro pendiente, si faltan autorizaciones)
PAT / PAD ── autorizarRemito(D) ─► DEN  (terminal, "ya no podrá ser modificado")
PAT/PAD/PEM ── anularRemito ─────► anulado (ANS o ANU: NO VERIFICADO)
PEM ── emitirRemito ─────────────► EMI
EMI ── registrarRecepcion ───────► ACE / ACP / NAC
EMI ── informarContingencia ─────► con anulación: anulado; demora: EMI y +1 día de validez
ACP / NAC ── registrarRedestino ─► nuevo remito RED (codRemito nuevo)
ACP / NAC / reparto ── registrarReingreso ─► cierre (estado NO VERIFICADO)
```

La recepción sobre un remito no emitido o ya recibido se rechaza. `VEN`, `NFI`, `BOR`, `EXO`/`EXP`/`EXT`/`EXR` y `ANUR` no tienen una transición documentada en el manual. **NO VERIFICADO** cuándo aparecen.

**Recepción:** con `aceptado` = N y sin ítems, rechazo total. Para aceptar se mandan **todos** los ítems (`orden`, `pesoNetoKG`, en cero los no recibidos) y "el sistema calculará si se trata de una aceptación total o parcial" (2.5.7). El tope por ítem es el peso enviado (error 3023).

**Plazos** (manual Anexo, "Tabla de período de validez según distancia", p.140; tabla desalineada en el texto, reconstruida):

| Km | Validez | Plazo para modificar transporte |
|---|---|---|
| 0-100 | emisión + 2 días | 24 hs |
| 101-500 | emisión + 3 días | 48 hs |
| 501-1000 | emisión + 5 días | 96 hs |
| 1001 o más | emisión + 10 días | 240 hs |

Coincide con el ejemplo 2.5.3.3.2 (200 km, emisión 2020-05-29, vencimiento 2020-06-01). El texto de 2.5.8 dice "24hs desde la emisión" sin distinguir distancia. La contingencia "Demoras en traslado" extiende la validez 1 día (2.5.9).

**Contingencias:** el ejemplo de `consultarTiposContingencia` (2.5.21.3.2) da los códigos 9 (anula sin pérdida), 10 (pérdida parcial, no anula), 11 (pérdida parcial, anula), 12 (pérdida total, anula), 13 (demoras en traslado) y 14 (corrección de pérdida informada, mercadería recuperada). La "Tabla de Causales de Contingencias" del anexo numera las mismas causas **1 a 5** y no tiene la 14. Contradicción interna del manual. El simulador debería servir la lista de 9 a 14, que es la que devuelve el WS en el ejemplo.

**Fechas en respuestas:** los `xsd:date` salen con zona: `2020-05-29-03:00`. El manual 3.7 pide `AAAA-MM-DD` sin zona en los requests.

**Paginación** (`consultarRemitos*`): hasta 2000 resultados por página, ordenados por generación; `nroPagina` + `hayMas` (S/N). Rango de fechas obligatorio en Emisor, con máximo 31 días dentro del mismo año calendario. En Autorizador y Receptor el rango solo filtra lo ya procesado; con "pendiente" se ignora (2.5.14-2.5.16).

**Tablas del anexo** (manual 4, p.138-140, para sembrar parámetros): tipos de mercadería 1-14 (Harina 0 … Otros productos ajenos a molienda), embalajes 01-18, unidades de venta 01-05 (Kg, Tonelada, Unidad, Litro, m³), actividad de remitente 106110, actividades de transportista, tabla de CUIT país y tabla de aduanas.

**Dummy:** responde `OK` en los tres campos con el header `info` (captura 2026-10-02). El manual 2.5.30 muestra `<return>` como hijo. El WSDL y la respuesta real usan `dummyReturn`.

## No verificado

- Texto, `faultcode` y HTTP status reales del Fault de autenticación (fwshomo devuelve `BL... 500` ante cualquier error). Tampoco se sabe si la página `BL` la arma un WAF.
- Si los errores de esquema van como Fault o como `arrayErroresFormato` con HTTP 200.
- Qué devuelve un `generarRemito` repetido con el mismo `idReqCliente` (¿el remito original o el error 151?).
- Estado exacto tras `anularRemito` (ANS vs ANU), tras una contingencia con anulación y tras `registrarReingreso`. Cuándo pasa a VEN, NFI y BOR.
- `registrarExportacion`: no figura en el manual. Su semántica y los estados EXO/EXP/EXT/EXR se deducen del nombre.
- Cómo devuelve el WS un remito `ANUR` si el esquema limita el estado a 3 caracteres.
- Formato del CRE (`codAutorizacion`): patrón inferido de 5 ejemplos.
- Comportamiento sin SOAPAction o con SOAPAction inválido (no se probó en este servicio).
- Si `remitoOutput` puede venir sin `datosAutAFIP` en estados PAT/PAD/PEM (el esquema lo permite; no hay ejemplo).
- Valores reales de las tablas (`consultarTiposMercaderia`, etc.) en homologación: solo hay ejemplos del manual.
