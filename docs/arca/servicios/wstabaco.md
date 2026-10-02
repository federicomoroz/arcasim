# wstabaco

Web service del Régimen Tabacalero para la "Gestión de Hebras" (tabaco en hebras). Lo usan las empresas tabacaleras con depósitos declarados en el Sistema Registral (destinos Depósito, Fábrica, Planta, Planta Fraccionadora o Consolidación en Planta Exportadora, manual §1.5). Gira alrededor del **CATHE** (Código de Autorización de Tabaco en Hebras). Primero se **solicitan** códigos para un depósito (elaboración) o para un despacho de importación. Después se los **vincula** al tabaco real (kilos, titular, tipo de mercadería). Al final se los da de **baja**, sea por elaboración de productos o por desnaturalización (con verificación de SEFI). En el medio pueden cambiar de titular sin moverse del depósito. También maneja los **CATA** (Código de Autorización de Tabaco Acondicionado), que existían antes de esta operatoria y se informan como existencia inicial. Antes de usar el WS hay que declarar el stock inicial en la aplicación web del Régimen Tabacalero (manual §1.5).

Manual: "Régimen tabacalero – WEB SERVICE TabacoService Gestión de Hebras – Manual para el Desarrollador", **Versión 1.0**, con portada ARCA. El PDF fue generado el 2025-08-14 (metadatos), pero los ejemplos son de 2017. 165 páginas, sin historial de cambios. URL: `https://www.afip.gob.ar/ws/WSTABACO/Manual_Desarrollador_WSTABACO_v1_0.pdf`. Las páginas citadas son las impresas al pie, que coinciden con las del PDF.

El manual tiene varias diferencias de nombres con el WSDL (ver "No verificado"). **El simulador sigue al WSDL.**

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Java JAX-WS**: respuesta observada con `<?xml version='1.0' encoding='UTF-8'?>` entre comillas simples, prefijo `S:` para el envelope y `ns2:` para el elemento de respuesta, y `S:Header/info` propio. Es el mismo stack que wsremharina/wsremcarne/wsremazucar. Está en `fwshomo`/`serviciosjava` |
| Endpoint homologación | `soap:address` del WSDL: `http://fwshomo.afip.gov.ar:80/wstabaco/TabacoService`. **El puerto 80 no conecta** (timeout de 21 s, 2026-10-02). El manual (§2.1, pág. 13) da `https://fwshomo.afip.gov.ar/wstabaco/TabacoService`, que es el que funciona (dummy OK el 2026-10-02) |
| Endpoint producción | `soap:address` del WSDL de producción: `http://serviciosjava.afip.gob.ar:80/wstabaco/TabacoService`. El manual da `https://serviciosjava.afip.gob.ar/wstabaco/TabacoService`. El cliente tiene que usar https e ignorar el `address` del WSDL |
| WSDL | `?wsdl` sobre el endpoint https (manual §2.1) |
| targetNamespace | `http://ar.gob.afip.wstabaco/TabacoService/` |
| service / port / binding / portType | `TabacoService` / `TabacoServiceSOAP` / `TabacoServiceSOAP` / `TabacoServicePortType`; `wsdl:definitions name="wstabaco"` |
| WSDL guardado | `docs/arca/wsdl/wstabaco-homologacion.wsdl`. Autocontenido: no tiene `wsdl:import`, `xsd:import` ni `xsd:include`. El de producción es idéntico salvo el `soap:address` (diff del 2026-10-02) |
| WSAA service id | **`wstabaco`**. Manual §2.3, pág. 14: "Al momento de solicitar un Ticket de Acceso por medio del WSAA tener en cuenta que debe enviar el tag service con el valor "wstabaco"". El certificado se asocia al servicio "Web Service de Tabaco – Régimen Tabacalero" |
| SOAPAction | targetNamespace + nombre de la operación, p. ej. `http://ar.gob.afip.wstabaco/TabacoService/solicitarCathesTabacoElaborado` (verificado en las 29 operaciones) |
| Versión SOAP | Sólo 1.1 (un único binding `soap:`) |
| Estilo | document/literal; elementos `<op>Request` / `<op>Response` |
| elementFormDefault | No declarado, o sea `unqualified`: sólo el elemento raíz del Body lleva namespace. `auth`, `token`, `deposito`, etc. van **sin namespace** |
| Operaciones | 29 |

## Autenticación

Elemento `auth` (tipo `AuthType`), **primer hijo** del `<op>Request`, en las 28 operaciones que no son `dummy`. Las 6 de parámetros/tipos y `consultarParametrosProductivos` usan un request (`ConsultarRequestType`) que tiene **sólo** `auth`.

```xml
<auth>
  <token>string</token>
  <sign>string</sign>
  <cuitRepresentada>long</cuitRepresentada>
</auth>
```

- Nombres exactos en minúscula: `auth`, `token`, `sign`, `cuitRepresentada`. Sin namespace (unqualified).
- `cuitRepresentada` es `CuitSimpleType`: `xsd:long` con `minExclusive 9999999999` y `maxInclusive 99999999999`, o sea 11 dígitos.
- Los tres son obligatorios (minOccurs por defecto 1).
- `dummy`: el mensaje `dummyRequest` no tiene parts, así que el Body va **vacío** (así lo muestra el manual, §2.4.29.1, pág. 159, y así se capturó). El WSDL declara un elemento `dummy` con un hijo `in` (string), pero ningún mensaje lo usa.

### Falla documentada

Manual §2.3 (pág. 14): "Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional." O sea, un **SOAP Fault**. El ejemplo del manual (§1.3.1, pág. 7), textual:

```xml
<S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
  <S:Body>
    <ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:ns3="http://www.w3.org/2003/05/soap-envelope">
      <faultcode>ns3: Receiver</faultcode>
      <faultstring>[wscommon_007] La firma no corresponde al token enviado.</faultstring>
    </ns2:Fault>
  </S:Body>
</S:Envelope>
```

(Así está en el manual: `faultcode` con el QName SOAP 1.2 `Receiver` dentro de un envelope SOAP 1.1, y con un espacio después de `ns3:`. Es un faultcode raro; un cliente sólo debería mirar `faultstring`.) No hay más códigos `wscommon_*` en el manual.

Además, cada operación valida la CUIT representada como error **de negocio** (dentro de `errores`, no Fault), códigos 100-104 (ver "Errores").

### Falla observada (homologación, 2026-10-02, `consultarTiposComprobante`)

En los tres casos (token "abc", token y sign vacíos, request sin `auth`) la respuesta fue la misma: **`HTTP/1.0 200 OK`, sin `Content-Type`, `Connection: Keep-Alive`, body de texto plano** `BL<13-15 dígitos> <yyyy-MM-dd HH:mm:ss> 500`. Ejemplos: `BL7349081042850 2026-10-02 15:08:32 500` (token abc), `BL664918427665 2026-10-02 15:08:32 500` (vacío), `BL1493647495048 2026-10-02 15:08:32 500` (sin auth). Es el fenómeno de catalogo.md §4.1. La hipótesis, **NO VERIFICADA**, es que un WAF/F5 delante de fwshomo reemplaza cualquier HTTP 500 (cualquier Fault) por esa página. Por eso **el texto real del Fault de autenticación no se pudo observar**: lo de arriba queda "documentado, no observado".

### Request mínimo válido contra el esquema

```http
POST /wstabaco/TabacoService HTTP/1.1
Host: fwshomo.afip.gov.ar
Content-Type: text/xml; charset=utf-8
SOAPAction: "http://ar.gob.afip.wstabaco/TabacoService/consultarTiposComprobante"
```

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:tab="http://ar.gob.afip.wstabaco/TabacoService/">
  <soapenv:Header/>
  <soapenv:Body>
    <tab:consultarTiposComprobanteRequest>
      <auth>
        <token>PD94...</token>
        <sign>tYft0...</sign>
        <cuitRepresentada>20111111112</cuitRepresentada>
      </auth>
    </tab:consultarTiposComprobanteRequest>
  </soapenv:Body>
</soapenv:Envelope>
```

## Operaciones

Abreviaturas de la última columna: **CATHE** = código de 14 dígitos; **idSolicitud** = `long` que asigna ARCA.

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Estado de la infraestructura | Body vacío | `dummyResponse/return/{appserver,authserver,dbserver}` | Sin estado |
| `informarParametrosProductivos` | Alta (`A`) o modificación (`M`) de la capacidad productiva por depósito: kilos promedio diarios y unidades de embalaje diarias (§2.4.2) | `arrayParametros/parametros+ {deposito, kilos, cantidad}`, `operacion` A/M | `resultado`, `observaciones`, `errores`, `eventos` | **Crea/modifica** parámetros por depósito. Se leen con `consultarParametrosProductivos` (clave: depósito). Son requisito de `solicitarCathesTabacoElaborado` |
| `consultarParametrosProductivos` | Últimos parámetros informados, por depósito (§2.4.12) | sólo `auth` | `arrayParametros/parametros* {deposito, kilos, cantidad}`, `errores`, `eventos` | Consulta lo de `informarParametrosProductivos` |
| `informarExistenciaInicialCata` | Informa los CATA que había en depósito al entrar en vigencia la operatoria, con kilos remanentes (§2.4.3) | `arrayExistenciasCata/datosCata+ {cata, deposito, kilos}` | `resultado`, `errores`, `eventos` (sin `observaciones`) | **Crea** existencia inicial por CATA. Se lee con `consultarExistenciaInicialCata` (clave: depósito; CATA). Los CATA quedan disponibles para `arrayCatasUsados` al vincular |
| `consultarExistenciaInicialCata` | CATA informados en existencia inicial en un depósito (§2.4.13) | `deposito` | `arrayExistenciasCata/datosCata*`, `errores`, `eventos` | Consulta lo anterior. Según el manual, `kilos` = kilos disponibles **actualmente** (descontados los usos) |
| `solicitarCathesTabacoElaborado` | Pide N CATHE para elaboración en un depósito (§2.4.4) | `inicial` S/N (opcional, default N), `deposito`, `cantidad` | `resultado`, `arrayCathes/cathe+`, `observaciones`, `errores`, `eventos` | **Crea** N CATHE en estado solicitado (sin vincular) para el depósito. Se leen con `consultarCathesSolicitados` (filtro por depósito/fechas) |
| `solicitarCathesTabacoImportado` | Pide CATHE para un despacho de importación, repartidos por depósito (§2.4.5) | `inicial` (opcional), `nroDespachoImp`, `cuitDespachante`, `cantBultos`, `arrayCantSolicitadas/cantPorDeposito+ {deposito, cantidad}` | `resultado`, `arrayCathes/datosCathes+ {cathe, deposito}`, `observaciones`, `errores`, `eventos` | **Crea** CATHE solicitados por depósito, ligados al despacho. Se leen con `consultarCathesSolicitados` (filtro `nroDespachoImp`) |
| `consultarCathesSolicitados` | CATHE solicitados y todavía sin vincular (§2.4.14) | opcionales: `deposito`, `nroDespachoImp`, `fechaDesde`, `fechaHasta` (de solicitud) | `resultado`, `arrayCathes/datosCathe* {cathe, deposito, nroDespachoImp?}`, `errores`, `observaciones`, `eventos` | Consulta CATHE en estado solicitado |
| `vincularCathesTabacoElaborado` | Informa el tabaco elaborado al que se vinculan CATHE ya solicitados, y qué CATHE/CATA (y cuántos kilos) se usaron para elaborarlo; o un recupero de hebras (§2.4.6) | `recupero` S/N, `tipoMercaderia`, `fechaElaboracion`?, `nroOrdenProduccion`?, `arrayCathesElaborados/datosTabacoElaborado+ {cathe, cuitTitular, kilosBrutos, kilosNetos}`, `arrayCathesUsados/datosCatheUsado*`, `arrayCatasUsados/datosCataUsado*` | `resultado`, `observaciones`, `errores`, `eventos` | **Pasa** los CATHE de solicitado a vinculado (con titular y kilos). **Descuenta** kilos de los CATHE/CATA usados; si se agotan, quedan dados de baja. Se lee con `consultarCathesVinculados` |
| `vincularCathesTabacoImportado` | Informa los datos del tabaco importado vinculado a CATHE solicitados (§2.4.7) | `arrayCathesImp/datosTabacoImportado+ {cathe, tipoMercaderia, fechaIngreso, cuitTitular, kilosBrutos, kilosNetos}` | `resultado`, `observaciones`, `errores`, `eventos` | **Pasa** de solicitado a vinculado. Se lee con `consultarCathesVinculados` (filtro `nroDespachoImp`) |
| `consultarCathesVinculados` | CATHE vinculados y en stock disponibles para quien consulta (§2.4.15) | igual que `consultarCathesSolicitados` | igual (`ConsultarCathesResponseType`) | Consulta CATHE en estado vinculado (no dados de baja) |
| `informarElaboracionProductos` | Informa por depósito y día qué CATHE se usaron y en qué producto; esos CATHE quedan dados de baja. Admite rectificativas (§2.4.8) | `deposito`, `fechaElaboracion`, `rectificativa` (0 = original), `arrayCathes/datosCatheProducto+ {cathe, tipoProducto, descripcionOtroProducto?}` | `resultado`, `observaciones`, `errores`, `eventos` | **Crea** un informe (depósito + fecha + nro. de rectificativa) y **da de baja** los CATHE. Una rectificativa deja sin efecto el informe anterior. Se lee con `consultarElaboracionProductos` (clave: depósito + fecha) |
| `consultarElaboracionProductos` | Informe de elaboración de un depósito en una fecha (§2.4.16) | `deposito`, `fecha` | `resultado`, `deposito`, `fechaElaboracion`, `fechaInformacion`, `arrayCathes/datosCatheProducto*`, `errores`, `observaciones`, `eventos` | Consulta lo anterior (se asume la última rectificativa vigente: **NO VERIFICADO**) |
| `solicitarDesnaturalizacion` | Solicita la desnaturalización de CATHE vinculados; genera una solicitud de verificación que procesa SEFI (§2.4.9) | `deposito`, `fecha` (de desnaturalización), `motivo` (1-100), `arrayCathes/cathe+` | `resultado`, `idSolicitud`, `observaciones`, `errores`, `eventos` | **Crea** solicitud de desnaturalización en estado P ("pendiente de procesar"), devuelve `idSolicitud`. Los CATHE quedan bloqueados. Se lee con `consultarSolicitudDesnaturalizacion` (clave `idSolicitud`) y aparece en `consultarSolicDesnatPendientes` y, ya resuelta, en `consultarSolicDesnatProcesadas` |
| `consultarSolicitudDesnaturalizacion` | Detalle de una solicitud de desnaturalización (§2.4.23) | `idSolicitud` | `idSolicitud`, `deposito`, `fechaDesnat`, `motivo`, `arrayCathesDesnat/estadoCathe* {cathe, estado P/S/N}`, `resultadoSolicitud`, `fechaResultado`, `errores`, `eventos` (todo opcional, **sin `resultado`**) | Lee la solicitud por `idSolicitud` |
| `consultarSolicDesnatPendientes` | Solicitudes de desnaturalización pendientes de SEFI (§2.4.20) | `fechaDesde`?, `fechaHasta`? (de desnaturalización) | `resultado`, `arraySolicitudes/datosSolicitud* {idSolicitud, deposito, fechaDesnat, motivo}`, `errores`, `observaciones`, `eventos` | Lista solicitudes con resultado P |
| `consultarSolicDesnatProcesadas` | Solicitudes ya procesadas por SEFI (§2.4.21) | `fechaDesde`?, `fechaHasta`? | `resultado`, `arraySolicitudes/datosSolicitud* {idSolicitud, deposito, fechaDesnat, motivo, resultado, fechaResultado}`, ... | Lista solicitudes con resultado distinto de P |
| `consultarTiposResultadoDesnaturalizacion` | Tabla de resultados de desnaturalización (§2.4.28) | sólo `auth` | `arrayTiposResultado/codigoDescripcion+`, `eventos` | Consulta tabla de parámetros |
| `solicitarCambioTitularSinMovFisico` | Cambio de titular del tabaco de un grupo de CATHE sin moverlo del depósito; lo pide quien tiene los CATHE en depósito, con los datos del comprobante (§2.4.10) | `cuitComprador`, `cuitVendedor`, `fechaCambio`, `modoFactura` P/E, `tipoComprobante`, `puntoVenta`, `numeroComprobante`, `importeNetoGravado`, `importeTotal`, `arrayCathes/cathe+` | `resultado`, `idSolicitud`, `estado`, `observaciones`, `errores`, `eventos` | **Crea** solicitud de cambio de titular en estado pendiente (PC o PV), devuelve `idSolicitud` y `estado`. Los CATHE quedan bloqueados. Se lee con `consultarSolicitudCambioTitular` (`idSolicitud`) y aparece en las listas Pendientes/Aprobadas/Rechazadas |
| `confirmarCambioTitularSinMovFisico` | La contraparte aprueba (`S`) o no (`N`) la solicitud (§2.4.11) | `idSolicitud`, `confirma` S/N | `resultado`, `observaciones`, `errores`, `eventos` | **Transición** de la solicitud (ver máquina de estados). Si queda aprobada, cambia el titular de los CATHE |
| `consultarSolicitudCambioTitular` | Detalle de una solicitud de cambio de titular (§2.4.22) | `idSolicitud` | `idSolicitud` (obligatorio), `cuitInformante`, `cuitVendedor`, `cuitComprador`, `fechaCambio`, `modoFactura`, `tipoComprobante`, `puntoVenta`, `numeroComprobante`, `importeNetoGravado`, `importeTotal`, `arrayCathes`, `estado`, `errores`, `eventos` (**sin `resultado`**) | Lee la solicitud por `idSolicitud` |
| `consultarSolicCambioTitularPendientes` | Solicitudes pendientes de aprobación del vendedor, del comprador o de ambos (§2.4.17) | `fechaDesde`?, `fechaHasta`? (de cambio de titularidad) | `resultado`, `arraySolicitudes/datosSolicitud* {idSolicitud, cuitInformante, cuitComprador, cuitVendedor, fechaCambio, tipoComprobante, puntoVenta, numeroComprobante, estado}`, `errores`, `observaciones`, `eventos` | Lista solicitudes PC/PV donde participa la CUIT representada |
| `consultarSolicCambioTitularAprobadas` | Solicitudes aprobadas (§2.4.18) | ídem | ídem (`ConsultarSolicitudesCambioTitularResponseType`) | Lista solicitudes aprobadas |
| `consultarSolicCambioTitularRechazadas` | Solicitudes rechazadas por vendedor o comprador (§2.4.19) | ídem | ídem | Lista solicitudes rechazadas |
| `consultarTiposEstadoSolicitudCambioTitular` | Tabla de estados de la solicitud de cambio de titular (§2.4.27) | sólo `auth` | `arrayTiposEstados/codigoDescripcion+`, `eventos` | Consulta tabla de parámetros |
| `consultarTiposEstadoCathe` | Tabla de estados de CATHE | sólo `auth` | `arrayTiposEstado/codigoDescripcion+` (sin "s" final, a diferencia del anterior), `eventos` | Consulta tabla de parámetros. **El manual la nombra (§1.1.1, §2.4.1) pero no tiene sección propia**: no hay esquema, ejemplo ni valores. Ninguna otra respuesta del WSDL devuelve un estado de CATHE con estos códigos |
| `consultarTiposComprobante` | Tipos de comprobante para el cambio de titular (§2.4.24) | sólo `auth` | `arrayTiposComprobante/codigoDescripcion+`, `eventos` | Consulta tabla de parámetros. Ejemplo: `1` Factura A, `6` Factura B |
| `consultarTiposMercaderia` | Tipos de mercadería (§2.4.25) | sólo `auth` | `arrayTiposMercaderia/codigoDescripcion+`, `eventos` | Consulta tabla de parámetros. Ejemplo: `1` Tabaco en Hebras, `2` Tabaco reconstituido, ... |
| `consultarTiposProductoElaborado` | Tipos de producto elaborado (§2.4.26) | sólo `auth` | `arrayTiposProductoElaborado/codigoDescripcion+`, `eventos` (**obligatorio** en el WSDL) | Consulta tabla de parámetros. Ejemplo: `1` Cigarrillos, `2` Cigarros, ... |

Tipos útiles del WSDL:

- `ArrayCodigoDescripcionType` = `codigoDescripcion` 1..n de `{codigo: string 1-6, descripcion: string 1-250}`. Es el tipo de `errores`, `observaciones`, `eventos` y de todas las tablas de parámetros.
- `ConsultarRangoFechasRequestType` = `auth`, `fechaDesde`?, `fechaHasta`?. Lo usan las 5 listas de solicitudes.
- `ConsultarCathesRequestType` = `auth`, `deposito`?, `nroDespachoImp`?, `fechaDesde`?, `fechaHasta`?.
- `DespachoImportacionSimpleType`: pattern `\d{5}[A-Z0-9]{4}\d{6}[A-Z]` (16 caracteres; ejemplo `99999ZZZZ999999E`).
- `KilosSimpleType`: decimal, > 0, ≤ 999999.99. `CantidadSimpleType`: long 1-999999. `ImporteSimpleType`: decimal, > 0, ≤ 9999999999999.99.
- `NumeroPtoVentaSimpleType`: long 1-9999 (el WSDL escribe `09999`). `NumeroCbteSimpleType`: long 1-99999999.
- `RectificativaSimpleType`: short 0-99. `OrdenProduccionSimpleType`: string 1-30. `MotivoSimpleType`: string 1-100. `DescripcionSimpleType` (`descripcionOtroProducto`): string 0-50.
- Enumeraciones: `ResultadoSimpleType` A/O/R; `SiNoSimpleType` S/N; `OperacionSimpleType` A/M; `ModoFacturaSimpleType` P (papel) / E (electrónica); `EstadoCatheDesnatSimpleType` P/S/N.
- `tipoMercaderia` y `tipoProducto` son `xsd:short`; `tipoComprobante` es `xsd:string` (el manual dice short, longitud 3); `estado` (de cambio de titular) y `resultado`/`resultadoSolicitud` (de desnaturalización) son `xsd:string` libres.
- Fechas `xsd:date`, formato `AAAA-MM-DD` sin huso horario (manual §3.3). Separador decimal: punto. Redondeo: Round Half Even.

## Errores

Cuatro canales (manual §1.3-§1.4):

1. **Excepcionales** (auth, estructura, tipos de dato): **SOAP Fault** (ver "Autenticación"). En fwshomo, lo observado el 2026-10-02 es la página `BL... 500` del gateway.
2. **Negocio, con rechazo**: `resultado` = `R` y `errores/codigoDescripcion+ {codigo, descripcion}` dentro de la respuesta normal (HTTP 200).
3. **Observaciones**: `resultado` = `O` y `observaciones/codigoDescripcion+`. La operación se aprueba igual.
4. **Eventos**: `eventos/codigoDescripcion+`, anuncios del sistema. Pueden venir en cualquier respuesta.

Detalles:

- `codigo` es **string** (1-6), no int.
- El WSDL define un `ArrayErroresType` (`error+` de `CodigoDescripcionType`) que **ninguna operación usa**; el contenedor real es `errores/codigoDescripcion`.
- Las 6 tablas de parámetros (`consultarTipos*`) **no tienen `errores`** en la respuesta: sólo el array y `eventos`. Si fallan las validaciones de CUIT 100-104, el error no se puede expresar en la respuesta. Lo esperable es un Fault (**NO VERIFICADO**).
- `consultarParametrosProductivos` y `consultarExistenciaInicialCata` tienen `errores` pero no `resultado`. `consultarSolicitudDesnaturalizacion` y `consultarSolicitudCambioTitular` tampoco tienen `resultado`.
- `informarExistenciaInicialCata` no tiene `observaciones` en el WSDL (el manual sí la dibuja).

### Códigos de error de negocio (manual §2.2 y §2.4.x)

Unos 75 códigos. Tabla compacta, por grupo:

| Códigos | Dónde | Validación (resumen del manual) | Efecto |
|---|---|---|---|
| 100 / 101 / 102 / 103 / 104 | `cuitRepresentada`, todas las operaciones | 100: debe estar en el Sistema Registral. 101: activa y sin limitaciones. 102: sin inconvenientes con el domicilio fiscal. 103: domicilio fiscal electrónico registrado. 104: actividad tabacalera (de manufactura) vigente | Rechaza |
| 104 / 105 | `cuitComprador` (cambio de titular) | 104: actividad tabacalera vigente. 105: la CUIT no debe tener inconsistencias | Rechaza |
| 200 | `deposito` | Debe ser un depósito con domicilio comercial válido | Rechaza |
| 201 | `arrayCathes` (cambio de titular) | Cada CATHE debe estar en el depósito de quien hace la solicitud | Rechaza |
| 300 / 301 | `cuitTitular` (vinculaciones) | 300: actividad tabacalera de manufactura vigente. 301: activa y sin limitaciones | Rechaza |
| 1000-1002 | `informarParametrosProductivos` | 1000: depósito repetido en el envío. 1001: con `A` no debe haber parámetros previos. 1002: con `M` deben existir | Rechaza |
| 1100-1104 | `informarExistenciaInicialCata` | 1100: CATA repetido. 1101: código válido. 1102: el CATA debe figurar en stock en el depósito según el Régimen Tabacalero. 1103: no informado antes. 1104: los kilos no superan los kilos con que se vinculó originalmente | Rechaza |
| 1200-1202 | `solicitarCathesTabacoElaborado` (si `inicial` ≠ S) | 1200: el depósito debe tener parámetros productivos. 1201: no superar la producción quincenal estimada. 1202: deben estar vinculados al menos el 80 % de los CATHE solicitados antes | Rechaza |
| 1300-1302 | `solicitarCathesTabacoImportado` | 1300: `cuitDespachante` válida. 1301: depósito repetido. 1302: la suma de cantidades no supera `cantBultos` | Rechaza |
| 1400-1415, 1420 | `vincularCathesTabacoElaborado` | 1400: tipo de mercadería válido. 1401: `nroOrdenProduccion` obligatorio para mercadería 1 y 2. 1420: hay que informar CATHE usados o CATA usados. 1402: CATHE válido, solicitado y no vinculado. 1403: repetido. 1404: todos del mismo depósito. 1405: coherencia `fechaElaboracion` / stock inicial. 1406-1410 (CATHE usados): sólo si no es recupero y sin repetir / existentes / VINCULADOS / mismo depósito / kilos ≤ disponibles. 1411-1415 (CATA usados): ídem | Rechaza |
| 1500-1503 | `vincularCathesTabacoImportado` | 1500: tipo de mercadería válido. 1501: repetido. 1502: no vinculado antes. 1503: coherencia `fechaIngreso` / stock inicial | Rechaza |
| 1600-1608 | `informarElaboracionProductos` | 1600: fecha ≤ hoy. 1601: rectificativa 0 = primer informe para depósito + fecha. 1602: rectificativa > 0 exige informe previo. 1603: debe ser la correlativa siguiente. 1604: CATHE repetido. 1605: CATHE VINCULADO. 1606: en el depósito indicado. 1607: tipo de producto válido. 1608: con tipo 4 ("Otro") hay que mandar `descripcionOtroProducto` | Rechaza |
| 1700-1704 | `solicitarDesnaturalizacion` | 1700: fecha posterior a hoy (**Rechaza**). 1701: al menos 15 días corridos después de hoy (**Observa**). 1702: CATHE repetido. 1703: CATHE válido y VINCULADO. 1704: CATHE en el depósito indicado | Rechaza, salvo 1701 |
| 1800-1803 | `solicitarCambioTitularSinMovFisico` | 1800: comprobante válido (vendedor + pto. vta. + tipo + número). 1801: CATHE repetido. 1802: CATHE VINCULADO. 1803: el vendedor es el titular actual | Rechaza |
| 1900-1902 | `confirmarCambioTitularSinMovFisico` | 1900: la solicitud existe. 1901: está pendiente de aprobación. 1902: la aprueba quien corresponde | Rechaza |
| 2000 / 2001 / 2002 | listas de cambio de titular | 2000: `fechaDesde` ≤ `fechaHasta` (Rechaza). 2001: no hay solicitudes (Observa). 2002: se superó el máximo de resultados; achicar el rango (Observa) | ver columna |
| 2100 / 2101 / 2102 | listas de desnaturalización | Ídem 2000-2002 | ver columna |
| 2200 | `consultarSolicitudCambioTitular` | La CUIT representada presentó la solicitud consultada | Observa (según la tabla) |
| 2300 | `consultarSolicitudDesnaturalizacion` | Ídem (el texto del manual está copiado de cambio de titular) | Observa |

El manual no da el texto exacto de `descripcion` para ninguno; el simulador puede usar el resumen de la validación.

## Comportamiento a simular

### CATHE y CATA

- Tipo `CodAutorizacionTabacoSimpleType`: `xsd:long`, entre 10000000000000 y 99999999999999, es decir **exactamente 14 dígitos** (manual §3.1: "Debe tener 14 dígitos"). El mismo tipo se usa para CATA.
- Los CATHE de los ejemplos empiezan con el año: `20171600135808`, `20172000321328`, `20170123456789`. Los CATA, con `7803`: `78031002166396`. La estructura interna (año + algo + secuencia) es **NO VERIFICADA**. El simulador puede usar `AAAA` + 10 dígitos secuenciales.
- Algunos ejemplos del manual traen CATHE de 15 dígitos (`201701234567890`, §2.4.6.3), que no validan contra el WSDL. Errores del manual.
- Los asigna ARCA en `solicitarCathes*`: una lista de `cantidad` códigos nuevos, todos del mismo depósito (elaborado) o repartidos por depósito (importado, `datosCathes {cathe, deposito}`).

### Ciclo de vida del CATHE (sale del texto del manual; los códigos de `consultarTiposEstadoCathe` no están documentados)

```
solicitarCathes*  ──►  SOLICITADO (sin vincular)       consultarCathesSolicitados
vincularCathes*   ──►  VINCULADO (en stock, titular, kilos)   consultarCathesVinculados
   ├─ usado al vincular otros CATHE (arrayCathesUsados): descuenta kilosNetos;
   │    si llega a 0 ──► dado de baja
   ├─ informarElaboracionProductos ──► dado de baja (por elaboración)
   ├─ solicitarDesnaturalizacion   ──► bloqueado mientras la solicitud está P;
   │    resultado A ──► todos desnaturalizados; B ──► vuelven a VINCULADO;
   │    C ──► los que tienen estado S se dan de baja, los N vuelven
   └─ solicitarCambioTitularSinMovFisico ──► bloqueado mientras está pendiente;
        aprobada ──► cambia cuitTitular; rechazada ──► vuelve sin cambios
```

"Mientras tanto, los CATHE incluidos en la solicitud no podrán participar de ninguna otra operación" (manual §2.4.9 y §2.4.10). Los kilos disponibles de un CATHE/CATA son los kilos netos vinculados menos los usados (§2.4.6.4, código 1410).

### Solicitud de desnaturalización

- `solicitarDesnaturalizacion` devuelve `idSolicitud` (`long`, sin formato documentado; en los ejemplos `50`, `114`) si `resultado` es A u O.
- Estado inicial: **P "Pendiente de procesar en SEFI"**. La verificación (SEFI, fuera del WS) la resuelve con uno de estos resultados (manual §2.4.9, pág. 62):
  - **A**: procedente la baja de la totalidad de los CATHE solicitados.
  - **B**: no procedente la baja de la totalidad.
  - **C**: procedente la baja de los CATHE que se detallan.
- Los códigos de resultado salen de `consultarTiposResultadoDesnaturalizacion`. El ejemplo (pág. 158) muestra `P` "Pendiente de procesar en SEFI" y `A` "Procedente la baja de la totalidad de los CATHE solicitados", y corta con "...". Que B y C usen esas letras como `codigo` es lo que dice §2.4.9; el texto exacto de su descripción es **NO VERIFICADO**.
- Por CATHE (`consultarSolicitudDesnaturalizacion/arrayCathesDesnat/estadoCathe/estado`): **P** pendiente, **S** sí puede desnaturalizar, **N** no puede (manual §3.1).
- `fechaResultado` = fecha de proceso de SEFI. Pendientes = solicitudes con resultado P; procesadas = el resto.
- Plazo: la fecha de desnaturalización tiene que ser posterior a hoy (1700, rechaza) y conviene que sea a 15 días corridos o más (1701, observa).
- **El simulador necesita un endpoint de administración que haga de SEFI** (resolver una solicitud con A, B o C y elegir los CATHE en S/N). Si no, nada sale nunca de P.

### Solicitud de cambio de titular sin movimiento físico

- La genera quien tiene los CATHE en depósito (`cuitInformante` = `cuitRepresentada`). Devuelve `idSolicitud` y `estado`.
- Estados (códigos de `consultarTiposEstadoSolicitudCambioTitular`, sacados de los ejemplos):
  - `PC`: "Pendiente aprobación del Comprador" (ejemplo pág. 155).
  - `PV`: "Pendiente aprobación del Vendedor" (ejemplo pág. 155).
  - `AP`: aprobada. Aparece en el ejemplo de detalle (pág. 136); la descripción no está en el manual.
  - `RC`: rechazada. Aparece en el ejemplo de rechazadas (pág. 119); por analogía sería "rechazada por el comprador", y habría un `RV` para el vendedor: **NO VERIFICADO**.
  - La tabla del manual corta con "...": puede haber más estados.
- Transiciones (manual §2.4.10 y §2.4.11):
  - Informante = vendedor → `PC`; `confirmar` del comprador.
  - Informante = comprador → `PV`; `confirmar` del vendedor.
  - Informante = tercero (ni comprador ni vendedor) → `PV`; cuando confirma el vendedor pasa a `PC`; cuando confirma el comprador queda aprobada.
  - `confirma` = `S` de quien corresponde → avanza; al quedar aprobada, se registra el cambio de titular del tabaco de los CATHE.
  - `confirma` = `N` → rechazada (código exacto **NO VERIFICADO**: `RC`/`RV`).
  - Errores: 1900 si no existe, 1901 si no está pendiente, 1902 si confirma quien no corresponde.
- Las listas Pendientes/Aprobadas/Rechazadas filtran por `fechaCambio` (`fechaDesde`/`fechaHasta`) y por participación de la CUIT representada. Sin resultados → `resultado` O + observación 2001. Demasiados → O + 2002, con la lista cortada en el máximo (el máximo no está documentado).

### Parámetros productivos y cupo quincenal

- Por depósito: `kilos` (producción diaria promedio) y `cantidad` (unidades de embalaje diarias). `A` crea; `M` reemplaza.
- `solicitarCathesTabacoElaborado` sin `inicial` = S: máximo "la cantidad de CATHEs necesarios para una producción quincenal, calculada en base a estos parámetros" (1201). La fórmula no está en el manual; la lectura obvia es 15 × `cantidad` diaria (**NO VERIFICADO**). Además, al menos el 80 % de los CATHE solicitados antes en ese depósito tienen que estar vinculados (1202). Con `inicial` = S no aplica el límite.
- Importado: el total pedido no puede superar `cantBultos` (1302).

### Elaboración de productos y rectificativas

- Clave del informe: depósito + `fechaElaboracion`. Original = `rectificativa` 0. Cada rectificativa siguiente es n+1, reenvía el informe completo y deja sin efecto el anterior: quedan dados de baja los CATHE del informe rectificado (§2.4.8). El manual no aclara si los CATHE que estaban en el anterior y no en el nuevo vuelven a VINCULADO (**NO VERIFICADO**).
- `tipoProducto` 4 = "Otro" exige `descripcionOtroProducto` (1608). Los ejemplos usan `99` para "otro producto": contradicción interna del manual.

### Idempotencia y reintentos

- No hay clave de idempotencia. Manual §1.6: si no llega la respuesta de `solicitarCathes*`, hay que consultar `consultarCathesSolicitados` para ver si se generaron. Lo mismo para vinculación, cambio de titular y desnaturalización con sus consultas.
- Reenviar un `solicitarCathes*` genera **otros** CATHE (no hay nada que lo deduplique); el cupo 1201/1202 es lo único que lo frena.
- Reenviar una vinculación da 1402 / 1502 (ya vinculado). Reenviar `informarParametrosProductivos` con `A` da 1001. Reenviar una existencia inicial da 1103. Reenviar elaboración con la misma rectificativa da 1601 / 1603.

### Header de respuesta

Toda respuesta trae `S:Header/info` (manual §1.2; dummy capturado el 2026-10-02):

```xml
<S:Header>
  <info xmlns="https://ar.gob.afip.wstabaco/TabacoService/">
    <ambiente>Producción - fi1</ambiente>
    <fecha>2026-10-02 15:08:32</fecha>
  </info>
</S:Header>
```

- El namespace de `info` es **https**, distinto del targetNamespace (http).
- Observado en homologación: `ambiente` = `Producción - fi1` (UTF-8 correcto, aunque es homologación) y `fecha` en formato `yyyy-MM-dd HH:mm:ss`.
- El manual muestra `<ambiente>Testing - vii</ambiente>` / `Produccion - bus` y `fecha` en ISO con milisegundos y zona (`2017-06-22T17:49:06.970-03:00`). **El simulador sigue lo observado.**
- Dummy observado completo: `<ns2:dummyResponse xmlns:ns2="http://ar.gob.afip.wstabaco/TabacoService/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse>`, HTTP 200, `Content-Type: text/xml;charset=utf-8`, `Transfer-Encoding: chunked`.

### Otros

- No hay PDFs ni base64.
- No hay paginación explícita: sólo el corte por máximo de resultados (2002/2102) en las listas de solicitudes.
- `consultarCathesVinculados`: según el manual, filtra por "fechas de vinculación", aunque la tabla de campos copia "solicitados". Usar fecha de vinculación.

## No verificado

- Texto real del Fault de autenticación en fwshomo (tapado por la página `BL... 500`) y si el `faultcode` es de verdad `ns3: Receiver`.
- Los códigos y descripciones de `consultarTiposEstadoCathe` (el manual no la documenta).
- La lista completa de estados de cambio de titular (sólo PC, PV, AP, RC aparecen en ejemplos) y el código que deja `confirma` = N.
- Las descripciones de los resultados B y C de desnaturalización en la tabla de parámetros.
- La fórmula del cupo quincenal (1201).
- El formato interno del CATHE y del `idSolicitud` (y si desnaturalización y cambio de titular comparten numeración).
- El máximo de resultados de las listas (2002/2102).
- Qué pasa con los CATHE que salen de un informe de elaboración al rectificarlo.
- Cómo responden las `consultarTipos*` cuando fallan las validaciones 100-104 (no tienen `errores`).
- El valor real de las tablas de parámetros (los ejemplos del manual cortan con "...").
- Diferencias manual vs WSDL (el simulador sigue al WSDL):
  - `informarExistenciaInicialCata` y `consultarExistenciaInicialCata`: el manual usa `kilosRemanentes` en el esquema y en un ejemplo; el WSDL usa **`kilos`**.
  - `consultarParametrosProductivos`: el manual devuelve `unidades`; el WSDL, **`cantidad`**.
  - `solicitarCathesTabacoElaboradoResponse/arrayCathes`: el manual dibuja `<CATHE>`; el WSDL, **`cathe`**.
  - `informarElaboracionProductos`: el esquema del manual dice `arrayCathesBajas`; su ejemplo y el WSDL, **`arrayCathes`**.
  - `vincularCathesTabacoImportado`: el esquema del manual dice `arrayCathesImportados`; su ejemplo y el WSDL, **`arrayCathesImp`**.
  - `informarExistenciaInicialCataResponse`: el manual incluye `observaciones`; el WSDL no.
  - `solicitarCathesTabacoImportado/inicial`: el manual dice "se debe indicar"; el WSDL lo hace opcional (`minOccurs=0`).
  - `consultarTiposProductoElaboradoResponse/eventos`: en el WSDL es obligatorio (`minOccurs` por defecto 1) y, como `ArrayCodigoDescripcionType` exige al menos un `codigoDescripcion`, una respuesta válida tendría que traer un evento. En los otros `consultarTipos*` es opcional. Probablemente sea un descuido del WSDL; cómo responde el servicio real está **NO VERIFICADO**.
  - Dummy: el ejemplo del manual (pág. 160) pone `appserver`/`authserver`/`dbserver` directo bajo `dummyResponse`; el esquema del manual, el WSDL y la captura los ponen bajo **`return`**.
  - `tipoComprobante`: el manual dice short (3); el WSDL, `xsd:string`.
  - En el ejemplo de solicitudes **aprobadas** (pág. 113) el `estado` es `PV`, que es un estado pendiente. Error del manual.
  - §2.4.22 y §2.4.23 traen la descripción y el ejemplo de un `consultarTiposDocumento` que no existe en el servicio (copiado de otro manual).
  - Header `info/fecha`: el formato del manual (ISO con zona) no es el observado (`yyyy-MM-dd HH:mm:ss`).
- `soap:address` en http puerto 80 (no conecta) contra https en el manual: se sigue al manual, porque el WSDL ahí está mal.
