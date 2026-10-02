# wsjaza

JAZA Service: los explotadores de **juegos de azar** informan a ARCA la operatoria de cada punto de explotación. Cubre el resumen diario de cada máquina electrónica (tragamonedas: contadores de juegos, coin-in, coin-out y jackpot), el resumen diario de partidas de bingo y de cajas de bingo y mesas vivas, el alta, baja y modificación de máquinas por lote, y la respuesta a pedidos puntuales que ARCA publica en Ventanilla Electrónica. Lo usan las CUIT que figuran en el padrón JAzA, y la CUIT emisora tiene que estar en "la lista de CUITs proveedoras homologadas" (manual, 2.2).

Requisito previo fuera del WS: el punto de explotación y sus máquinas, sillas y mesas se registran en la aplicación web JAzA (manual, 1.7).

Fuentes:

- Manual "WEB SERVICE JAZAService" 1.0.5 (historial hasta 22/01/2024): `https://www.afip.gob.ar/juegosdeazar/documentos/Web_Service_JAZA_20240122_v1.0.5.pdf`.
- WSDL de homologación y producción, descargados el 2026-10-02 (iguales salvo el `soap:address`).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, JAX-WS RI (Metro). La página `GET` del endpoint dice "Implementation class: ar.gov.afip.wsjaza.service.JAZAServicePortTypeImpl" |
| SOAP | 1.1, document/literal |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsjaza/JAZAService` (el WSDL dice `https://fwshomo.afip.gov.ar:443/wsjaza/JAZAService`) |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsjaza/JAZAService` (`:443` en el WSDL) |
| WSDL | `?wsdl` |
| targetNamespace | `http://ar.gob.afip.wsjaza/JAZAService/` (esquema inline, **sin** `elementFormDefault`: los hijos van sin namespace) |
| portType / binding / service | `JAZAServicePortType` / `JAZAServiceSOAP` / `JAZAService`, port `JAZAServiceSOAP` |
| SOAPAction | `http://ar.gob.afip.wsjaza/JAZAService/<operación>` en todas |
| Archivo guardado | `wsdl/wsjaza-homologacion.wsdl` (sin imports) |
| WSAA service id | **No indicado** en el manual. Lo más probable es `wsjaza`, pero está **NO VERIFICADO** |
| Versión | El servicio no expone versión |

**Trampas de namespace en el manual.** Varios esquemas de request usan `xmlns:jaz="http://ar.gob.afip.jaza/JAZAService/"` (sin `ws`), y el dummy usa `http://ar.gob.afip.wsjaza/WSJAZAService/`. El WSDL y las respuestas reales usan `http://ar.gob.afip.wsjaza/JAZAService/`. El esquema de respuesta de 2.4.8.2 escribe `infromarLoteMEResponse` (errata); el ejemplo y el WSDL dicen `informarLoteMEResponse`.

**`dummy` no tiene cuerpo.** El mensaje `dummyRequest` del WSDL no tiene partes: el request es `<soapenv:Body/>` vacío y el servicio lo despacha **por SOAPAction**. Sin `SOAPAction: "http://ar.gob.afip.wsjaza/JAZAService/dummy"` falla (ver Errores).

## Autenticación

`authRequest` es el primer hijo de cada request (salvo `dummy`), sin namespace:

```xml
<authRequest>
  <token>...</token>
  <sign>...</sign>
  <cuitRepresentada>30000000007</cuitRepresentada>
</authRequest>
```

- `cuitRepresentada`: `CuitSimpleType`, long entre 10000000000 y 99999999999. "Se validará en todos los casos que la CUIT informante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional" (manual, 2.3).
- Validaciones de la entidad (manual, 2.2): `cuitRepresentada` activa en el Sistema Registral, dentro del padrón de JAzA, y la CUIT del token en la lista de proveedoras homologadas. Todas rechazan.

Error excepcional según el manual (1.3): SOAP Fault con `faultstring` descriptivo y prefijo de código `wscommon_NNN`:

```xml
<S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
  <S:Body>
    <ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns3="http://www.w3.org/2003/05/soap-envelope">
      <faultcode>ns3:Receiver</faultcode>
      <faultstring>[wscommon_007] La firma no corresponde al token enviado.</faultstring>
    </ns2:Fault>
  </S:Body>
</S:Envelope>
```

(En el manual dice `ns3: Receiver` con espacio; es un faultcode SOAP 1.2 metido en un Fault 1.1, típico de Metro.)

**Lo que llega de verdad** (homologación, 2026-10-02): con `token` = `abc`, con un TA bien formado y firma falsa, y con `cuitRepresentada` fuera de rango, la respuesta **no es SOAP**:

```
HTTP/1.0 200 OK
Connection: Keep-Alive
Content-Length: 39

BL9594984967072 2026-10-02 15:16:27 500
```

Sin `Content-Type`. Es la misma respuesta que el catálogo vio en wsmtxca. Se repite con XML mal formado y con el `dummy` enviado sin SOAPAction. Conclusión (inferida, consistente con todas las pruebas): **el WAF F5 de `fwshomo` reemplaza cualquier respuesta HTTP 500 del backend (es decir, todo SOAP Fault) por esta línea**: `BL` + 15 dígitos (un id de incidente) + fecha y hora local + `500`. El Fault `wscommon_007` del manual existe detrás, pero el cliente de Internet no lo ve.

Las respuestas buenas traen `Strict-Transport-Security: max-age=300; includeSubDomains; preload`, `X-XSS-Protection: 1; mode=block`, `X-Frame-Options: sameorigin`, `X-Content-Type-Options: nosniff`, `Transfer-Encoding: chunked` y cookies F5 (`f5avraaaaaaaaaaaaaaaa_session_`, `TS01761d9e`).

## Operaciones

11 operaciones en `JAZAServicePortType`. Todas las respuestas llevan, cuando corresponde, `arrayErrores{codigoDescripcion[1..n]{codigo: short, descripcion}}` (negocio) o `arrayErroresFormato{codigoDescripcionString[1..n]{codigo: string, descripcion}}` (formato); los dos son excluyentes.

| Operación | Propósito | Entrada (además de `authRequest`) | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Infraestructura | Body vacío, despacho por SOAPAction | `dummyResponse/dummyReturn{appserver, authserver, dbserver}` | — |
| `informarResumenDiaME` | Resumen diario de **una** máquina electrónica, una secuencia | `nroPuntoExplotacion` (short), `fechaPresentacion` (date), `nroPresentacion` (short), `detalleMaquinaElectronica{idMaquina (1-50), secuencia, fechaHoraSecuenciaInicio, fechaHoraSecuenciaFin, denomContabilidad, contadoresInicial{juegosJugados, coinIn, coinOut, jackpot}, contadoresFinal{...}}` | `informarResumenDiaMEReturn{resultado, arrayErrores?, arrayErroresFormato?}` | **Crea**. Clave: CUIT + punto + `idMaquina` + `fechaPresentacion` + `nroPresentacion` + `secuencia` |
| `removerResumenDiaME` | Quita el resumen de una máquina para una fecha | `nroPuntoExplotacion`, `fechaPresentacion`, `idMaquina` | `removerResumenDiaMEReturn{resultado, arrayErrores?, arrayErroresFormato?}` | **Borra**. Misma clave sin presentación ni secuencia. **No está en el manual**: solo en el WSDL |
| `consultarMEInformada` | Lo informado de una máquina en una fecha | `nroPuntoExplotacion`, `fechaPresentacion`, `idMaquina` | `consultarMEInformadaReturn{arrayDetalleMaquinasElectronicas{detalleMaquinaElectronica*}?, arrayErrores?, arrayErroresFormato?}` | Consulta |
| `consultarIdsMEInformadas` | Ids de máquinas informadas en una fecha | `nroPuntoExplotacion`, `fechaPresentacion?` | `consultarIdsMEInformadasReturn{nroPuntoExplotacion?, fechaPresentacion?, arrayIdsMaquinasElectronicas{idMaquina*}?, arrayErrores?, ...}` | Consulta |
| `consultarIdsMEPendientes` | Máquinas con días sin informar y desde qué fecha | `nroPuntoExplotacion` | `consultarIdsMEPendientesReturn{nroPuntoExplotacion?, arrayIdsFechasMaquinasElectronicas{idFechaMaquina*{idMaquina, fecha}}?, ...}` | Consulta |
| `informarResumenDiaOtros` | Resumen diario de bingo, caja de bingo y mesas vivas del punto | `nroPuntoExplotacion`, `fechaPresentacion`, `nroPresentacion`, `arrayDetallePartidasBingo{detallePartidaBingo*}?`, `arrayCajasConsolidadaMesasVivas{cajaConsolidadaMesasVivas*}?`, `cajaConsolidadaBingos?` | `informarResumenDiaOtrosReturn{resultado, arrayErrores?, arrayErroresFormato?}` | **Crea**. Clave: CUIT + punto + fecha + `nroPresentacion`. Un día sin operaciones se informa igual, sin los tres bloques |
| `consultarResumenDiaOtros` | Lo informado en "otros" para una fecha | `nroPuntoExplotacion`, `fechaPresentacion` | `consultarResumenDiaOtrosReturn{arrayDetallePartidasBingo?, arrayCajasConsolidadaMesasVivas?, cajaConsolidadaBingos?, arrayErrores?, ...}` | Consulta. El manual dice que sin fecha devuelve el último resumen, pero en el WSDL la fecha es obligatoria |
| `responderSolicitudME` | Responde un pedido puntual de contadores publicado en Ventanilla Electrónica | `idSolicitud` (long), `estado` (`OK`, `BA` baja, `ND` no disponible), `detalleMaquinaElectronica?` (solo con `OK`) | `responderSolicitudMEReturn{resultado, ...}` | **Cambia estado** de la solicitud. Clave: `idSolicitud` (+ `idMaquina`) |
| `informarLoteME` | Alta, baja y modificación de máquinas | `ptoExplotacion` (long), `arrayME{me[1..n]{oper (1 alta, 2 baja, 3 modificación), uid (1-30), codTipoMaquina?, codMarca?, descTipoMarca?, codModelo?, descModelo?, codJuego?, descJuego?, nroSerie?, software?, multipuesto? S/N, particOArrend? S/N, cuitArrend?, codTipoComision?, porcComision?, cannonComision?, observComision?, fecIniOp?, codBaja?, descBaja?, fechaBaja?}}` | `informarLoteMEResponse{nroLote, resultado, arrayErrores?, arrayErroresFormato?}` (sin elemento `*Return`) | **Crea un lote** asíncrono, "pendiente de procesamiento". Clave: `nroLote` |
| `consultarLoteME` | Resultado de lotes (máximo 20 por consulta) | `ptoExplotacion`, `nroLoteDesde?`, `nroLoteHasta?`, `fechaDesde?`, `fechaHasta?` | `consultarLoteMEResponse{resultado, arrayErrores?, arrayErroresFormato?, arrayLotesME{loteME*{nroLote, estado, fechaEnvio, origen (WS u OL), observaciones?, arrayErrores{errorME*{nroLinea, erroresLinea{errorLineaME[1..n]{codError, descError?}}}}?}}?}` | Consulta. Clave: `nroLote` |

Tipos y valores que importan:

- `resultado` (`ResultadoSimpleType`): el WSDL admite `A`, `I` y `R`; el manual solo documenta A (aceptado) y R (rechazado). Qué significa `I` está **NO VERIFICADO**.
- `estado` de lote (`EstadoLoteSimpleType`): `ER`, `RP`, `PE`, `TE`, `OP`, `CA`. Los ejemplos muestran `ER` (con errores) y `TE` (terminado). El significado de los demás está **NO VERIFICADO** (por el nombre, `PE` sería pendiente).
- `fechaEnvio` sale con zona: `2015-01-05-03:00`.
- `observaciones` del lote: "Cant. operaciones registradas: 1" o "No se Registraron operaciones.\nCant. operaciones rechazadas: 1".
- Contadores: long de 0 a 999999999999999999. Importes: decimal con 2 decimales, 0 a 9999999999.99 (`diferenciaCaja` admite negativos).
- `tipoMesa`: 1 Ruleta, 2 Naipes, 3 Dados, 4 Torneo, 99 Otros.
- Restricción de texto en `informarLoteME`: patrón `[^;/<>]*`.

Respuesta real de `dummy` (homologación, 2026-10-02), HTTP 200, `Content-Type: text/xml;charset=utf-8`:

```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:dummyResponse xmlns:ns2="http://ar.gob.afip.wsjaza/JAZAService/"><dummyReturn><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></dummyReturn></ns2:dummyResponse></S:Body></S:Envelope>
```

## Errores

Tres niveles (manual, 1.3 a 1.5): excepcional (SOAP Fault, que en Internet se ve como `BL... 500`), formato (`arrayErroresFormato`, con códigos del validador XML como `cvc-datatype-valid.1.2.1` y `cvc-type.3.1.3` y mensajes en español) y negocio (`arrayErrores`). Con error de formato no se evalúa el negocio.

Códigos de negocio. Las tablas del PDF están desalineadas; la asignación de código a texto se hizo por orden y se confirma con los ejemplos (1003, 2111, 7002).

| Código | Operación | Validación |
|---|---|---|
| 1000 | informarResumenDiaME | El punto de explotación debe estar dado de alta en JAzA para la CUIT |
| 1020 | informarResumenDiaME | La fecha de presentación no puede ser futura |
| 1001 | informarResumenDiaME | La máquina debe estar declarada en JAzA para CUIT, punto y fecha |
| 1002 | informarResumenDiaME | La fecha no puede ser anterior al inicio de operaciones de la máquina |
| 1003 | informarResumenDiaME | Hay que informar primero la primera fecha pendiente. Texto real: "Antes de informar los datos para la fecha 20/10/2014 debe informar los datos para la fecha 06/10/2014" |
| 1004 | informarResumenDiaME | Solo se rectifica dentro de los 30 días |
| 1005 | informarResumenDiaME | Primer envío de una fecha: presentación 1 |
| 1006 | informarResumenDiaME | Primer envío de una fecha: secuencia 1 |
| 1007 | informarResumenDiaME | Secuencia adicional: misma presentación y secuencia + 1 |
| 1008 | informarResumenDiaME | Rectificativa: secuencia 1 |
| 1009 | informarResumenDiaME | Presentación 1 si es el primer envío; la misma (nueva secuencia) o + 1 (rectificativa) |
| 1010 | informarResumenDiaME | Inicio de la nueva secuencia ≥ fin de la anterior |
| 1011 a 1014 | informarResumenDiaME | El contador inicial de juegos jugados, coin-in, coin-out y jackpot de la secuencia 1 tiene que coincidir con el final del día anterior |
| 1101 a 1104 | informarResumenDiaME | Contador final ≥ inicial (juegos, coin-in, coin-out, jackpot) |
| 1105 | informarResumenDiaME | Fin de secuencia ≥ inicio |
| 2000 a 2004 | informarResumenDiaOtros | Punto dado de alta; fecha no futura; presentación 1 o última + 1; sin días salteados; no anterior a la última autorizada |
| 2101 a 2112 | informarResumenDiaOtros (bingo) | Cartones de hasta 6 dígitos; vendidos ≤ serie; serie ≥ rango vendido; vendidos ≤ rango; premios = 0 si no hubo ventas y > 0 si hubo; fecha-hora no futura; partida única por punto (salvo series distintas, 2111); 2112 eliminada en 1.0.4 |
| 2201 | informarResumenDiaOtros (caja bingo) | apertura + ventas + diferencia − pagos = cierre |
| 2301 a 2304 | informarResumenDiaOtros (mesas) | Tipo de mesa válido; cantidad ≥ 0; cantidad ≤ 9999; un tipo de mesa por presentación |
| 3001 a 3003 | consultarMEInformada | Fecha no futura; punto dado de alta; debe existir presentación |
| 4001 a 4003 | consultarResumenDiaOtros | Fecha no futura; punto dado de alta; debe existir presentación |
| 5001, 5002 | consultarIdsMEInformadas | Punto dado de alta; fecha no futura |
| 6001 | consultarIdsMEPendientes | Punto dado de alta |
| 7000 a 7003 | responderSolicitudME | Con ND o BA no va detalle; con OK va detalle; no existe la solicitud para CUIT, `idSolicitud` e `idMaquina` (7002: "No registra solicitud de máquina electrónica para el idSolicitud: 2 idMaquina: SDFGDFG1"); ya fue respondida |
| 8000, 9998, 8001 | informarLoteME | `oper` inválido; el punto no existe para la CUIT; "Para una operación de [alta/baja/modificación] el campo [nombre] no puede ser nulo" |
| 9002, 9009 a 9013 | consultarLoteME | Punto dado de alta; `nroLoteDesde` > `nroLoteHasta`; `fechaDesde` futura; `fechaHasta` futura; `fechaHasta` < `fechaDesde`; rango mayor a 30 días |

Errores por línea de lote (`codError`), solo los que aparecen en ejemplos: 130 "Si informó N en el campo que indica si es Máquina en participación o arrendada, no debe enviar los datos relacionados al arrendamiento"; 301 "No existe una maquina informada con el UID que desea modificar". El catálogo completo está **NO VERIFICADO**.

## Comportamiento a simular

- `dummy` por SOAPAction con body vacío.
- Cualquier Fault (auth, XML roto, operación desconocida) sale como `HTTP/1.0 200` + `BL<15 dígitos> <yyyy-MM-dd HH:mm:ss> 500`, sin Content-Type, para imitar el WAF. Conviene un modo de depuración que muestre el Fault `[wscommon_NNN] ...` de detrás.
- Estado por CUIT y punto de explotación: máquinas con períodos de vigencia (alta por lote), presentaciones diarias por máquina con secuencias y rectificativas, resúmenes "otros" por fecha, solicitudes puntuales y lotes.
- Reglas fuertes a copiar: presentaciones diarias consecutivas sin huecos (1003, 2003); continuidad de contadores entre días salvo secuencia nueva (1011 a 1014); rectificativa con `nroPresentacion` + 1 dentro de 30 días, que invalida las presentaciones posteriores; duplicado exacto (fecha, presentación y secuencia) rechazado.
- `informarLoteME` devuelve `nroLote` secuencial con `resultado` A y el lote queda en proceso; `consultarLoteME` lo muestra luego en `TE` o `ER` con errores por línea. Las validaciones de alta, baja y modificación de 1.7.3 se evalúan al procesar el lote, no en el `informarLoteME`.
- Pares de estado obvios: `informarResumenDiaME` → `consultarMEInformada` y `consultarIdsMEInformadas` lo muestran, y `consultarIdsMEPendientes` corre la fecha pendiente; `informarLoteME` → `consultarLoteME`; `removerResumenDiaME` → la máquina vuelve a figurar pendiente (inferido).

## No verificado

- El service id de WSAA.
- El texto real de los Fault (`wscommon_NNN`), invisibles detrás del WAF.
- `removerResumenDiaME`: reglas y códigos (no está en el manual).
- El valor `I` de `resultado` y los estados de lote `RP`, `PE`, `OP` y `CA`.
- El catálogo de `codError` de lotes, de marcas, modelos, juegos, tipos de máquina, comisiones y códigos de baja.
- Si `consultarResumenDiaOtros` acepta la fecha vacía (manual) o no (WSDL).
- Cómo se publica una solicitud puntual (`idSolicitud`) en Ventanilla Electrónica.
