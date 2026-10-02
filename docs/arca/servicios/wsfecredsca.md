# wsfecredsca

Web service de **Factura de Crédito Electrónica (FCE) MiPyMEs, Sistema de Circulación Abierta** (`FECredSCAService`). Sirve para "la Obtención de Facturas de Crédito con Opción de Transmisión "Sistema de Circulación Abierta" que ya fueron aceptadas por los contribuyentes receptores de la mismas en el Sistema de Registro de Facturas Electrónicas de Crédito de la AFIP" (manual §1.1, pág. 4). Lo usa un solo actor: "El único actor que interviene en el presente servicio es el Sistema de Circulación Abierta" (§1.5, pág. 12). El manual no dice qué entidad opera el SCA.

Tiene dos operaciones de negocio: **consultar** las facturas aceptadas que quedaron a disposición del SCA y **confirmar su recepción** (§1.1.1, pág. 4).

Marco normativo: Ley 27.440 (Título I) y RG 4367/2018, ver `docs/arca/normativa.md` §8. El manual no cita ninguna RG (§2.4.1, pág. 17).

Las facturas que consulta nacen en `wsfecred`, cuando el comprador acepta una factura con opción de transferencia "Sistema de Circulación Abierta" (ver "Comportamiento a simular"). `wsfecred` lo documenta otro agente; acá se cita su manual sólo para la relación.

### Fuentes

| Id | Fuente | URL | Uso |
|---|---|---|---|
| **[MAN]** | "Factura Electrónica – Web Service Sistema de Circulación Abierta – Manual para el desarrollador", **versión 1.3.0**, 44 págs. Changelog (pág. 44): 1.0.0-beta1 22/01/2021 "Versión Inicial – beta 1"; 1.0.0 31/03/2021 "Versión Inicial"; 1.1.0 27/07/2021 "Se agregan los campos al tipo FacturaType: cbuEmisor ... aliasCBUEmisor"; 1.3.0 16/12/2024 "Ajustes Internos sin Impacto Funcional" (no hay 1.2.0 en el changelog) | `https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECREDSCA-1.3.0.pdf` | Leído completo. Tablas verificadas sobre la imagen de cada página |
| **[WSDL]** | WSDL de homologación (`docs/arca/wsdl/wsfecredsca-homologacion.wsdl`) | `https://fwshomo.afip.gov.ar/wsfecredsca/FECredSCAService?wsdl` | Estructura. **Manda sobre el manual.** El de producción (`https://serviciosjava.afip.gob.ar/wsfecredsca/FECredSCAService?wsdl`) es idéntico salvo `soap:address` (comparado el 2026-10-02), aunque el servidor de producción es una versión más nueva (ver "Contrato") |
| **[VIVO]** | Llamadas propias sin autenticación válida, 2026-10-02, homologación (y `dummy` en producción) | `scratchpad\vivo\sca-*` (req/hdr/body); resumen en `agente-sca-NOTAS.txt` | Forma real de respuestas y errores |
| **[MAN-FECRED]** | Manual de `wsfecred` v2.0.3, 86 págs. | `https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECRED_v2.0.3.pdf` | Sólo para la relación con `wsfecred` |

Marcas: **[MAN]**, **[WSDL]**, **[VIVO]**: verificado. **[INFERIDO]**: deducido, sin texto oficial. **NO VERIFICADO**: no se pudo comprobar. "pág. N" es la página física del PDF.

Fecha de relevamiento: **2026-10-02**.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Spring-WS** (Spring Boot), igual que `wsfecredagente`: header `info` con namespace `http://headers.springbootws.factu.fisca.afip.gob.ar/xml`, envelope `soap:`, respuesta `ns2:`, `Content-Type: text/xml;charset=UTF-8` sin `<?xml?>`, errores de autenticación dentro del resultado con HTTP 200 [VIVO]. No es el dialecto JAX-WS de `wsfecred` (diferencias en `wsfecredagente.md`, "Contrato → Diferencias con wsfecred") |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsfecredsca/FECredSCAService/` (`soap:address`, **con** barra final) [WSDL]. El manual da la URL sin barra (§2.1, pág. 13); las llamadas de este relevamiento usaron la URL sin barra [VIVO] |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsfecredsca/FECredSCAService` (`soap:address` del WSDL de producción y manual §2.1, pág. 13; el manual escribe la URL del WSDL con espacios: `https://serviciosjava.afip.gob.ar/ wsfecredsca /FECredSCAService?wsdl`). `dummy` respondió OK el 2026-10-02 [VIVO] |
| WSDL | `<endpoint>?wsdl`. Archivo: `docs/arca/wsdl/wsfecredsca-homologacion.wsdl`. Esquema inline, sin `xsd:import` |
| `targetNamespace` | `http://ar.gob.afip.wsfecredsca/FECredSCAService/` |
| `elementFormDefault` | **No declarado → `unqualified`**. Sólo el elemento raíz del Body va calificado; los hijos (`autenticacion`, `nroPagina`, `filtroFechas`...) van sin namespace [WSDL; VIVO: los requests sin calificar se procesan] |
| Service id WSAA | **`wsfecredsca`**. Fuente: manual §2.3.1, pág. 15: "debe enviar el tag service con el valor "wsfecredsca"". El certificado se asocia al servicio "Web Service del Sistema de Circulacion Abierta" (misma página). Coincide con `catalogo.md` §3.1 |
| `SOAPAction` | `http://ar.gob.afip.wsfecredsca/FECredSCAService/<operación>` para las 3: `dummy`, `consultarFacturasAceptadas`, `confirmarRecepcionFacturas` [WSDL]. El enrutamiento no se probó en detalle en este servicio; en `wsfecredagente` (mismo framework) enruta por `SOAPAction` y, si falta, por el elemento raíz [INFERIDO por analogía] |
| SOAP 1.1 / 1.2 | Sólo SOAP 1.1: un binding (`FECredSCAServiceSOAP`, `document`/`literal`), un port [WSDL]. SOAP 1.2 no se probó acá; en `wsfecredagente` da `BL...500` |
| Header de respuesta | `<soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente/><fecha/><id/></info></soap:Header>` [VIVO] |
| Nombres de elementos | Request `<op>Request`, response `<op>Response` con un único hijo `resultado`. `dummy` no tiene elemento de request (mensaje sin `part`) [WSDL] |
| Transporte | HTTP/1.1 chunked, cookies F5 (`f5avr..._session_`, `TS01761d9e`), `Strict-Transport-Security`, `X-Frame-Options`, `X-Content-Type-Options` [VIVO] |

### Header `info`

| Ambiente | `ambiente` | `fecha` | `id` | Fuente |
|---|---|---|---|---|
| Homologación | `homologacion-externa - FI1` | `2026-10-02T15:07:36` (sin milisegundos ni zona) | `wsfecredsca 1.2.0 2021-11-01T19:14:02.780Z` | [VIVO] |
| Producción | `produccion - SJ4` | `2026-10-02T15:15:22` | **`wsfecredsca 1.3.0 2024-12-12T20:19:17.914Z`** | [VIVO] |
| Manual | `Testing - vii` / `Produccion - bus` | `2018-06-22T17:49:06.970-03:00` | `fecred-sca-ws 1.0.0 2020-10-20T20:52:30.332Z` | [MAN] §1.2, págs. 5-6 |

Producción corre la 1.3.0 (la del manual) y homologación sigue en 1.2.0. El changelog dice que la 1.3.0 son "Ajustes Internos sin Impacto Funcional" y los WSDL son idénticos, así que el contrato es el mismo. El manual pone el header con `xmlns="http://ar.gob.afip.wsfecredsca/FECredSCAService/"` y un envelope `S:`; el real usa el namespace `springbootws` y `soap:` [VIVO].

### Llamadas en vivo [VIVO, 2026-10-02]

| Request | Respuesta |
|---|---|
| `dummy` con `<x:dummy/>` | 200, `dummyResponse/resultado` OK |
| `dummy` con Body vacío | 200, ídem |
| `GET` al endpoint | 200 con un `dummyResponse` completo |
| `consultarFacturasAceptadas`, token `abc`, filtro válido | 200, `<facturas/><nroPagina>0</nroPagina><hayMas>N</hayMas>` + errores 505 y 506 |
| `consultarFacturasAceptadas`, token base64 bien formado + firma falsa, filtro válido | 200, sólo 505 con token y firma completos entre llaves |
| `consultarFacturasAceptadas` con `filtroFechas/tipo` = `A` o `fechaDisponible` (fuera del enum) | `BL<n> <fecha> 500` (Fault por XSD), aunque el token también era inválido |
| `consultarFacturasAceptadas` con `estadoFactura` = `A` (fuera del enum `D`/`P`/`R`) | `BL<n> <fecha> 500` |
| `confirmarRecepcionFacturas`, token `abc` | 200, `<errores>`505, 506`</errores><resultados/>` |

`BL<n> <fecha> 500`: HTTP/1.0 200, sin `Content-Type`, `Content-Length: 39`, texto plano. Es el balanceador F5 tapando un HTTP 500 del backend (un SOAP Fault) [INFERIDO, evidencia fuerte; ver brief común y `wsfecredagente.md`]. **La validación contra el XSD ocurre antes que la autenticación.**

## Autenticación

Todas las operaciones salvo `dummy` llevan como primer hijo `autenticacion` (`AutenticacionType`) [WSDL]:

| Campo | Tipo XSD | Ocurrencia | Manual (§2.3, pág. 14) |
|---|---|---|---|
| `token` | `xsd:string` | 1..1 | S. "Token devuelto por el WSAA" |
| `sign` | `xsd:string` | 1..1 | S. "Signature devuelta por el WSAA" |
| `cuitRepresentada` | `CuitSimpleType` (`long`, 11 dígitos: `minExclusive` 9999999999, `maxInclusive` 99999999999) | 1..1 | S. "CUIT del SCA" |

- "Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos" (§2.3.1, pág. 14). La longitud de `token` y `sign` no se especifica (pág. 43, aclaración 1).
- El ejemplo del manual (pág. 13) declara el namespace con un espacio adelante: `xmlns:ser=" http://ar.gob.afip.wsfecredsca/FECredSCAService/"`. Errata tipográfica.
- Códigos 501-516 (§2.3.1, págs. 14-15) y, como "Validaciones excluyentes de Autorización" (§2.3.2, págs. 15-16), 4008 y 4009. Textos completos en "Validaciones y errores". Dos textos difieren de `wsfecredagente`: 501 dice "autenticar **un request**" y 508 dice "El Servicio asociado **al** Token **no se corresponde a este sistema**".

### Respuestas reales de falla [VIVO, 2026-10-02]

HTTP 200, header `info`, y:

```xml
<!-- token "abc", sign "abc" -->
<ns2:consultarFacturasAceptadasResponse xmlns:ns2="http://ar.gob.afip.wsfecredsca/FECredSCAService/"><resultado><facturas/><nroPagina>0</nroPagina><hayMas>N</hayMas><errores><codigoDescripcion><codigo>505</codigo><descripcion>El Token no se corresponde con la Firma. {token = [abc]firma = [abc]}</descripcion></codigoDescripcion><codigoDescripcion><codigo>506</codigo><descripcion>El Token no cumple con el Esquema (XSD) de Autenticacion. {abc}</descripcion></codigoDescripcion></errores></resultado></ns2:consultarFacturasAceptadasResponse>

<!-- token XML bien formado en base64 (service="wsfecred") + firma falsa -->
<ns2:consultarFacturasAceptadasResponse xmlns:ns2="..."><resultado><facturas/><nroPagina>0</nroPagina><hayMas>N</hayMas><errores><codigoDescripcion><codigo>505</codigo><descripcion>El Token no se corresponde con la Firma. {token = [PD94bWwg…(token completo)]firma = [ZmlybWFmYWxzYQ==]}</descripcion></codigoDescripcion></errores></resultado></ns2:consultarFacturasAceptadasResponse>

<!-- confirmarRecepcionFacturas, token "abc" -->
<ns2:confirmarRecepcionFacturasResponse xmlns:ns2="http://ar.gob.afip.wsfecredsca/FECredSCAService/"><resultado><errores><codigoDescripcion><codigo>505</codigo>...</codigoDescripcion><codigoDescripcion><codigo>506</codigo>...</codigoDescripcion></errores><resultados/></resultado></ns2:confirmarRecepcionFacturasResponse>
```

- Texto real = texto del manual + `. {detalle}`; 506 sin tilde (`Autenticacion`). En `wsfecredagente` el token vacío dio `503 El Formato del Token es inválido. {}` y `504 El Formato de la Firma es inválida. {}`; acá no se probó [INFERIDO igual].
- El manual (§1.3.1, pág. 7) pone como ejemplo de error excepcional un SOAP Fault con `faultcode` `ns3: Receiver` y `faultstring` "[wscommon_007] La firma no corresponde al token enviado.". **El servicio real no hace eso**: una firma inválida vuelve como 505 dentro del resultado con HTTP 200 [VIVO]. El ejemplo del manual parece de otra generación del servicio.
- No se pudo ver (hace falta certificado): 507-516, 4008, 4009.

## Operaciones

El `portType` `FECredSCAServicePortType` tiene **3 operaciones** [WSDL]: `dummy`, `consultarFacturasAceptadas`, `confirmarRecepcionFacturas`. Las tres están en el binding y en el manual (§2.4.1, pág. 17).

Convenciones: "XSD" = `tipo minOccurs..maxOccurs` del WSDL; "Man." = columna "Oblig" del manual. Cuando difieren, manda el XSD.

Forma de los requests (hijos sin namespace):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://ar.gob.afip.wsfecredsca/FECredSCAService/">
  <soapenv:Header/>
  <soapenv:Body>
    <ser:consultarFacturasAceptadasRequest>
      <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
      …
    </ser:consultarFacturasAceptadasRequest>
  </soapenv:Body>
</soapenv:Envelope>
```

Forma de las respuestas [VIVO]: una línea, sin `<?xml?>`:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente>homologacion-externa - FI1</ambiente><fecha>2026-10-02T15:09:19</fecha><id>wsfecredsca 1.2.0 2021-11-01T19:14:02.780Z</id></info></soap:Header><soap:Body><ns2:xxxResponse xmlns:ns2="http://ar.gob.afip.wsfecredsca/FECredSCAService/"><resultado>...</resultado></ns2:xxxResponse></soap:Body></soap:Envelope>
```

### Tipos compartidos

**Tipos simples** [WSDL; manual §3.1, págs. 32-33]:

| Tipo | Base y restricción XSD | Manual |
|---|---|---|
| `CuitSimpleType` | `long`, `minExclusive` 9999999999, `maxInclusive` 99999999999 | "Longitud 11." |
| `TipoAceptacionSimpleType` | `string`, `length` 1, enum `T`, `E` | "Tipo de Aceptacion de la Factura de Credito": Tacita "vencido el plazo de Aceptación la Factura se dio por aceptada tácitamente"; Expresa "el comprador aceptó expresamente la factura dentro del plazo de aceptación" |
| `CBUSimpleType` | `string`, `length` 22 | "Longitud 22" |
| `ResultadoSimpleType` | `string`, enum `A`, `O`, `R` | A: Aceptado, O: Observada, R: Rechazado |
| `NumeroComprobanteSimpleType` | `long`, 1 a 99999999 | 1 a 99999999 (el manual lo llama `NumeroComprobantSimpleType`) |
| `PuntoVentaSimpleType` | `int`, 1 a 99999 | 1 a 99999 |
| `Texto250SimpleType` | `string`, `minLength` 3, `maxLength` 250 | "Texto hasta 250 caracteres." |
| `SiNoSimpleType` | `string`, `length` 1, enum `S`, `N` | S: Si, N: No |
| `ImporteSimpleType` | `decimal`, ±9999999999999.99, `totalDigits` 15, `fractionDigits` 2 | 13 enteros y 2 decimales |
| `DecimalSimpleType` | `decimal`, 0 a 999999999999.999999 | — (no lo usa ningún elemento) |
| `TipoFechasFacturasSimpleType` | `string`, enum `Disponible`, `Consultada`, `Recibida` | Disponible: "Fecha en la que el Comprador la puso a Disposición"; Consultada: "Fecha en la que el SCA la Consulto"; Recibida: "Fecha en la que el SCA informo la Recepcion de la Factura de AFIP". El XSD documenta las columnas `fechaDisponible`, `fechaConsultadaSCA`, `fechaRecibidaSCA` |
| `EstadoFacturaSimpleType` | `string`, enum `D`, `P`, `R` | "Estado de la Factura de Crédito Aceptada por el Comprador": D: Disponible "el Comprador Acepto la Factura en WSFECRED"; P: Pendiente "el SCA ya la consulto y esta a la espera de ser recepcionada"; R: Recibida "el SCA confirma que pudo recibir la factura". El XSD: "D - Disponible - Fue Aceptada en el WSFECRED y esta Disponible para ser tomada por el SCA; P - Pendiente - Fue consultada por el SCA y esta pendiente la confirmacion de la Recepcion; R - Recibido - Fue recibido por el SCA" |

`date` = `AAAA-MM-DD` sin huso; separador decimal punto (pág. 43).

**Tipos complejos** [WSDL; manual §3.3, págs. 35-42]:

| Tipo | Campos (XSD) | Notas |
|---|---|---|
| `CodigoDescripcionType` | `codigo` **`short`** 1..1, `descripcion` `string` 1..1 | Manual igual (pág. 35). En `wsfecredagente` `codigo` es `long` |
| `CodigoDescripcionStringType` | `codigo` `string` 1..1, `descripcion` `string` 1..1 | |
| `ArrayCodigosDescripcionesType` | `codigoDescripcion` 1..unbounded | |
| `ArrayCodigosDescripcionesStringType` | `codigoDescripcionString` 1..unbounded | |
| `ArrayFacturasType` | `factura` (`FacturaType`) **0**..unbounded | |
| `ArrayFacturasRecibidasType` | `factura` (`FacturaRecibidaType`) 1..unbounded | |
| `ArrayResultadosConfirmacionFacturasType` | `resultado` (`ConfirmacionFacturaResultadoType`) 0..unbounded | |
| `FiltroFechasFacturasType` | `tipo` `TipoFechasFacturasSimpleType` 1..1, `desde` `date` 1..1, `hasta` `date` 1..1 | El manual (pág. 42) describe `tipo` como "(Fecha Disponible, Fecha Consultada, Fecha Confirmada)"; el enum dice `Recibida`, no `Confirmada` |
| `IdComprobanteType` | `cuitEmisor` `CuitSimpleType`, `tipoCmp` `short`, `ptoVta` `PuntoVentaSimpleType`, `nroCmp` `NumeroComprobanteSimpleType`, todos 1..1 | La tabla del manual (pág. 37) dice **`codTipoCmp`**; el diagrama de la pág. 38 y el WSDL dicen `tipoCmp`. Manda el WSDL. En `wsfecred` el tipo homónimo usa `CUITEmisor` y `codTipoCmp` |

Definidos en el WSDL pero **no usados por ninguna operación**: `ConsultaParametricaRequestType`, `ConsultaParametricaResponseType`, `ConsultaParametricaReturnType` (con `parametros` 1..1), `ArrayParametrosType` (choice de 2), `ArrayTexto250SimpleType`, `ArrayCuitsType` (`cuit` 1..unbounded), `DecimalSimpleType`. El manual describe `ConsultaParametricaResponseType` (pág. 41) aunque no hay operación paramétrica.

`evento`: `CodigoDescripcionType` suelto (0..1), con `codigo` y `descripcion` como hijos directos [WSDL]. El manual (§1.4, págs. 10-11) lo dibuja con un `codigoDescripcion` adentro; manda el WSDL.

### 1. dummy

Propósito: "Metodo dummy." [WSDL]; "Permite verificar el funcionamiento del presente Servicio" (manual §2.4.4, pág. 19). Sin autenticación.

Request: ninguno (Body vacío, pág. 19). También acepta `<ser:dummy/>` y `GET` [VIVO].

Response `dummyResponse` → `resultado` (`DummyReturnType`): `appserver`, `authserver`, `dbserver`, `string` 1..1 cada uno (manual pág. 20).

Ejemplo real [VIVO]:

```xml
<ns2:dummyResponse xmlns:ns2="http://ar.gob.afip.wsfecredsca/FECredSCAService/"><resultado><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></resultado></ns2:dummyResponse>
```

El ejemplo del manual (pág. 21) omite `<resultado>`: errata.

### 2. consultarFacturasAceptadas

Propósito: "consultar aquellas facturas de crédito aceptadas por los Compradores en el Registro de Factura Electrónica de Crédito de AFIP. Estas facturas estarán Disponibles para el SCA en el instante en el cual son aceptadas por el contribuyente. Una vez consultadas por esta operación, pasarán a un estado de Pendiente de Recepción del SCA." (manual §2.4.5, pág. 22).

El manual recomienda usar el filtro de estado así (pág. 22):
- "Disponible: para consultar las novedades, es decir, las facturas que no han sido consultadas previamente en este sistema."
- "Pendiente de Recepción: para volver a consultar facturas que puedan haber sido consultadas pero no leídas por el cliente de este servicio por algún inconveniente técnico."
- "Recibidas: para obtener un historial de aquellas facturas que ya han sido leidas y confirmada su recepción por el cliente de este servicio."

Request `consultarFacturasAceptadasRequest` (`ConsultarFacturasAceptadasRequestType`):

| Campo | XSD | Man. (págs. 22-23) | Descripción |
|---|---|---|---|
| `autenticacion` | `AutenticacionType` 1..1 | S | |
| `estadoFactura` | `EstadoFacturaSimpleType` 0..1 | N | "El estado en el que se encuentra la factura de Crédito" (`D`/`P`/`R`). La tabla dice tipo `EstadoInformeSimpleType`; el diagrama y el WSDL, `EstadoFacturaSimpleType` |
| `nroPagina` | `xsd:short` 1..1 | S | "Numero de página para obtener más resultados de una misma búsqueda" |
| `cuitEmisor` | `CuitSimpleType` 0..1 | N | "CUIT del Emisor de la Factura" |
| `cuitReceptor` | `CuitSimpleType` 0..1 | N | "CUIT del Receptor de la Factura" |
| `filtroFechas` | `FiltroFechasFacturasType` 1..1 | S | "Fecha desde y hasta que indica el rango a consultar." La tabla dice `filtroFecha` / `FiltroFechasCuentaType`; el diagrama y el WSDL, `filtroFechas` / `FiltroFechasFacturasType` |

Response `consultarFacturasAceptadasResponse` → `resultado` (`ConsultarFacturasAceptadasReturnType`), en este orden:

| Campo | XSD | Man. (págs. 24-25) | Descripción |
|---|---|---|---|
| `facturas` | `ArrayFacturasType` 1..1 | S | "Conjunto de Facturas Aceptadas resultantes de la Búsqueda" |
| `nroPagina` | `short` 1..1 | S | "Número de Pagina Devuelto" |
| `hayMas` | `SiNoSimpleType` 1..1 | S | "Indica si existen más resultados posteriores a los devueltos en esta página" |
| `evento` | `CodigoDescripcionType` 0..1 | N | "Anuncios informativos del sistema." |
| `observaciones` | `ArrayCodigosDescripcionesType` 0..1 | **no figura** | (sólo en el WSDL) |
| `errores` | `ArrayCodigosDescripcionesType` 0..1 | N | motivos del rechazo |
| `erroresFormato` | `ArrayCodigosDescripcionesStringType` 0..1 | N | errores de formato |

`FacturaType` (cada `facturas/factura`), en orden de esquema:

| Campo | XSD | Man. (págs. 38-40) | Descripción del manual |
|---|---|---|---|
| `idFactura` | `IdComprobanteType` 1..1 | S | "Identificador de la Factura de Crédito obtenida de la Consulta" |
| `tipoAceptacion` | `TipoAceptacionSimpleType` 1..1 | S | "Tipo de Aceptación asociada a la Factura de Credito" (`T`/`E`) |
| `razonSocialEmisor` | `Texto250SimpleType` 1..1 | S | "Razón Social del Emisor de la Factura" |
| `cbuEmisor` | `CBUSimpleType` 0..1 | N | "CBU del Emisor de la Factura de Crédito" (agregado en 1.1.0) |
| `aliasCBUEmisor` | `Texto250SimpleType` 0..1 | N | "Alias asociado al CBU del Emisor de la Factura de Crédito" (agregado en 1.1.0) |
| `cuitComprador` | `CuitSimpleType` 1..1 | S | "Cuit del Comprador de la Factura de Crédito" |
| `razonSocialComprador` | `Texto250SimpleType` 1..1 | S | "Razón Social del Comprador de la Factura de Crédito" |
| `informaCbuComprador` | `SiNoSimpleType` 1..1 | S | "Indica si el Comprador Informo una CBU al momento de la Aceptación de la Factura" |
| `cbuValidada` | `SiNoSimpleType` 0..1 | N | "Si el campo informaCbuComprador fue S, este campo indica si la cbuComprador pudo ser validada contra el servicio de Validacion del BCRA" |
| `cbuComprador` | `CBUSimpleType` 0..1 | N | "Si el campo informaCbuComprador fue S, se devolverá en este campo la CBU que el comprador asoció a la Factura" |
| `errorValidacionCbu` | `xsd:long` 0..1 | N | "Si el campo informaCbuComprador fue S y el campo cbuValidada fue N, este campo indica el Tipo de Error de Validacion de CBU que se obtuvo al momento de validar contra el Servicio de Validacion del BCRA" (ver "Errores de Validación de CBU") |
| `fechaEmision` | `xsd:date` 1..1 | S | "Fecha en la que se Emitió la Factura de Crédito" |
| `fechaVencimientoPago` | `xsd:date` 1..1 | S | "Fecha de Vencimiento del Pago" |
| `saldoAceptado` | `ImporteSimpleType` 1..1 | S | "Saldo aceptado por el Comprador de la Factura de Crédito" |
| `codMoneda` | `xsd:string` 1..1 | S | "Moneda utilizada por la Factura de Crédito" |
| `estadoFactura` | `EstadoFacturaSimpleType` 1..1 | S | "Estado de la Factura de Crédito" |
| `fechaHoraDisponible` | `xsd:dateTime` 1..1 | S | "Fecha y Hora del instante en que el Comprador Acepto la Factura de Crédito" |
| `fechaHoraLecturaSCA` | `xsd:dateTime` 0..1 | N | "Fecha y Hora del instante en que el SCA Consulto la Factura de crédito" |
| `fechaHoraConfirmacionSCA` | `xsd:dateTime` 0..1 | N | "Fecha y Hora del instante en que el SCA Confirmo la Recepción de la Factura de Crédito" |

**Errores de Validación de CBU** (§2.4.5.3, pág. 26): "Para los casos en los cuales el comprador haya informado una CBU y la misma no haya podido ser validada ya sea porque ocurrió un error interno en alguno de los servicios de AFIP o bien porque el mismo se produjo en el Servicio API Alias de Coelsa, se retornaran los siguientes códigos de error según el caso: Errores Internos de AFIP: Códigos del 51-53 al 1001-1017; Errores en el Servicio API Alias de Coelsa: Códigos propios del Servicio API Alias de Coelsa". Estos códigos van en el campo `errorValidacionCbu` de cada factura; **no rechazan** la consulta. El texto "del 51-53 al 1001-1017" es ambiguo (¿51 a 53 y 1001 a 1017?) y el manual no da la lista. NO VERIFICADO.

Validaciones propias (§2.4.5.4, pág. 27): **4015** `<facturas>` "Ocurrió un error inesperado intentando consultar las Facturas Aceptadas". Más las comunes (2002, 2003, 2004, 4008, 4009, 4001-4007).

Ejemplo de request [derivado del WSDL; el manual no trae ejemplos]:

```xml
<ser:consultarFacturasAceptadasRequest>
  <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
  <estadoFactura>D</estadoFactura>
  <nroPagina>1</nroPagina>
  <filtroFechas><tipo>Disponible</tipo><desde>2026-09-01</desde><hasta>2026-09-30</hasta></filtroFechas>
</ser:consultarFacturasAceptadasRequest>
```

Ejemplo de respuesta [derivado del WSDL, valores ficticios, formato de `dateTime` NO VERIFICADO]:

```xml
<ns2:consultarFacturasAceptadasResponse xmlns:ns2="http://ar.gob.afip.wsfecredsca/FECredSCAService/"><resultado><facturas><factura><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idFactura><tipoAceptacion>E</tipoAceptacion><razonSocialEmisor>PYME SRL</razonSocialEmisor><cbuEmisor>0110599520000001234567</cbuEmisor><cuitComprador>30500010912</cuitComprador><razonSocialComprador>EMPRESA GRANDE SA</razonSocialComprador><informaCbuComprador>S</informaCbuComprador><cbuValidada>S</cbuValidada><cbuComprador>0170001540000001234567</cbuComprador><fechaEmision>2026-08-01</fechaEmision><fechaVencimientoPago>2026-10-30</fechaVencimientoPago><saldoAceptado>121000.00</saldoAceptado><codMoneda>PES</codMoneda><estadoFactura>P</estadoFactura><fechaHoraDisponible>2026-09-10T11:20:00</fechaHoraDisponible><fechaHoraLecturaSCA>2026-10-02T15:30:00</fechaHoraLecturaSCA></factura></facturas><nroPagina>1</nroPagina><hayMas>N</hayMas></resultado></ns2:consultarFacturasAceptadasResponse>
```

(Si `estadoFactura` vuelve con el valor anterior o posterior a la consulta: NO VERIFICADO.)

### 3. confirmarRecepcionFacturas

Propósito: "confirmar a AFIP la recepción de un conjunto de facturas de crédito consultadas previamente mediante el método de consulta. Este mecanismo es necesario, para posibilitar la consulta de sólo las novedades en el método de consulta. Este método retornará el resultado sobre cada una de las facturas informadas en el arreglo" (manual §2.4.6, pág. 28).

Request `confirmarRecepcionFacturasRequest` (`ConfirmarFacturasRecibidasRequestType`):

| Campo | XSD | Man. (pág. 28) | Descripción |
|---|---|---|---|
| `autenticacion` | `AutenticacionType` 1..1 | S | |
| `facturas` | `ArrayFacturasRecibidasType` 1..1 → `factura` 1..unbounded | S | El manual dice "Conjunto de Facturas de Crédito Informadas por los Vendedores a Rechazar o Aceptar": texto copiado de `wsfecredagente`. Acá sólo se confirma la recepción |

`FacturaRecibidaType`: `idFactura` (`IdComprobanteType`) 1..1, "Identificador de la Factura de Crédito a Confirmar" (pág. 38). No hay aceptar/rechazar.

Response `confirmarRecepcionFacturasResponse` → `resultado` (`ConfirmarFacturasRecibidasReturnType`), en este orden:

| Campo | XSD | Man. (págs. 29-30) | Descripción |
|---|---|---|---|
| `evento` | `CodigoDescripcionType` 0..1 | N | anuncio informativo |
| `errores` | `ArrayCodigosDescripcionesType` 0..1 | N | errores del lote |
| `erroresFormato` | `ArrayCodigosDescripcionesStringType` 0..1 | N | errores de formato |
| `resultados` | `ArrayResultadosConfirmacionFacturasType` 1..1 → `resultado` 0..unbounded | S | "Conjunto de Resultados de Confirmación de Facturas de Crédito Enviadas" |

`ConfirmacionFacturaResultadoType`:

| Campo | XSD | Man. (pág. 37) | Descripción |
|---|---|---|---|
| `idFactura` | `IdComprobanteType` 1..1 | S | "Identificador de la Factura de Crédito" |
| `resultado` | `ResultadoSimpleType` 1..1 | S | "Resultado de la Confirmación" (`A`/`O`/`R`) |
| `errores` | `ArrayCodigosDescripcionesType` 0..1 | N; el manual lo llama **`observaciones`** ("Listado de Observaciones si las hubiera") | Manda el WSDL: el elemento se llama `errores` |

Validaciones propias (§2.4.6.3, pág. 31):

| Código | Grupo | Texto |
|---|---|---|
| 4010 | `<facturas>` | "Ocurrió un error inesperado intentando marcar recibido el lote de Facturas Enviado" |
| 4011 | `<factura>` | "No se pudo marcar como recibida la Factura." |
| 4012 | `<factura>` | "No se encontró ninguna Factura al SCA con los Datos del Comprobante Enviados que se encuentre Pendiente de Confirmar." |
| 4013 | `<factura>` | "La Factura ya fue confirmada." |
| 4014 | `<factrura>` (sic) | "La Factura debe ser consultada antes de ser confirmada." |

Más 2005 (`idFactura`), 2006 (demasiadas facturas) y las comunes. 4010 va al `errores` del lote y 4011-4014 al `errores` del `resultado` de cada factura con `resultado` `R` [INFERIDO por el "Campo/Grupo"].

Ejemplo de request [derivado del WSDL]:

```xml
<ser:confirmarRecepcionFacturasRequest>
  <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
  <facturas>
    <factura><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idFactura></factura>
    <factura><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>16</nroCmp></idFactura></factura>
  </facturas>
</ser:confirmarRecepcionFacturasRequest>
```

Ejemplo de respuesta [derivado del WSDL; la segunda factura ya estaba confirmada]:

```xml
<ns2:confirmarRecepcionFacturasResponse xmlns:ns2="http://ar.gob.afip.wsfecredsca/FECredSCAService/"><resultado><resultados><resultado><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idFactura><resultado>A</resultado></resultado><resultado><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>16</nroCmp></idFactura><resultado>R</resultado><errores><codigoDescripcion><codigo>4013</codigo><descripcion>La Factura ya fue confirmada.</descripcion></codigoDescripcion></errores></resultado></resultados></resultado></ns2:confirmarRecepcionFacturasResponse>
```

## Validaciones y errores

Tres familias (manual §1.3, págs. 7-10), excluyentes:

1. **Excepcionales**: SOAP Fault. El manual (pág. 7) da como ejemplo `<ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns3="http://www.w3.org/2003/05/soap-envelope"><faultcode>ns3: Receiver</faultcode><faultstring>[wscommon_007] La firma no corresponde al token enviado.</faultstring></ns2:Fault>` y dice que "incluyen también errores de estructura (ej: tags sin cerrar, con nombres incorrectos o en orden incorrecto) y de tipos de datos". En la realidad, los errores de estructura y de tipos vuelven `BL...500` [VIVO], y la firma inválida vuelve como 505 en el resultado [VIVO].
2. **Formato**: `erroresFormato`. El manual (pág. 8) lo dibuja con hijos `codigoDescripcion` y dice que es `ArrayCodigoDescripcionType`; el WSDL dice `ArrayCodigosDescripcionesStringType` con hijos **`codigoDescripcionString`** (código `string`). Manda el WSDL. "de no superar alguna de las validaciones de formato, el WS devolverá el erroresFormato y no continuará con las validaciones de negocio, por lo cual no existirá el elemento errores" (pág. 9).
3. **Negocio o autenticación**: `errores/codigoDescripcion` (pág. 10).

Orden observado [VIVO]: XSD (Fault) → autenticación (`errores` 50x). El resto, según el manual.

"Efecto": **fault** = SOAP Fault HTTP 500 (en la realidad `BL...500`); **rechaza (formato)** = `erroresFormato`; **rechaza** = `errores` del resultado; **rechaza ítem** = `errores` de una factura en `resultados`; **informa** = valor en un campo de la factura, sin rechazo.

| Código | Texto / condición | Efecto | Dónde | Página |
|---|---|---|---|---|
| `wscommon_007` | "[wscommon_007] La firma no corresponde al token enviado." | fault, según el ejemplo del manual. **El real devuelve 505 en su lugar** [VIVO] | todas | 7 |
| 1 | "Ocurrió un error intentando procesar el Request (1)" — nota (1): "Son devueltos como SoapFaults" | fault | todas, Request | 18 |
| 2 | "Ocurrió un error intentando parsear el Soap Request (1)" | fault | todas, Request | 18 |
| 1001 | "El Request no Cumple con el Esquema asociado al WSDL" | fault [VIVO: violar el XSD da `BL...500`; el código no se pudo ver] | todas, Request | 18 |
| 2001 | "Ocurrió un error intentando validar el pedido" | rechaza (formato) | todas, Request | 18 |
| 2002 | "Formato CUIT, CUIL o CDI inválido" | rechaza (formato) | todas, cualquier CUIT | 18 |
| 2003 | "Rango de Fechas inválido (2)" | rechaza (formato) | `consultarFacturasAceptadas`, `filtroFechas/desde`, `hasta` | 18 |
| 2004 | "Número de Página inválido" | rechaza (formato) | `consultarFacturasAceptadas`, `nroPagina` | 18 |
| 2005 | "Ocurrió un error intentando validar la factura recibida" | rechaza (formato) | `confirmarRecepcionFacturas`, `idFactura` | 18 |
| 2006 | "Supera la cantidad de elementos que pueden procesarse (2)" | rechaza (formato) | `confirmarRecepcionFacturas`, `facturas` | 18 |
| 501 | "Ocurrió un error intentando autenticar un request" | rechaza | todas menos `dummy`, `<autenticacion>` | 14 |
| 502 | "La Sección de Autenticación del Request no cumple con el Esquema (XSD) de Autenticación" | rechaza (en la realidad, un bloque fuera del XSD da fault) | `<autenticacion>` | 14 |
| 503 | "El Formato del Token es inválido" | rechaza | `<autenticacion>/token` | 14 |
| 504 | "El Formato de la Firma es inválida" | rechaza | `<autenticacion>/sign` | 14 |
| 505 | "El Token no se corresponde con la Firma" | rechaza. Real: `... con la Firma. {token = [<token>]firma = [<sign>]}` [VIVO] | `<autenticacion>` | 15 |
| 506 | "El Token no cumple con el Esquema (XSD) de Autenticación" | rechaza. Real: `... de Autenticacion. {<token>}` [VIVO] | `<autenticacion>/token` | 15 |
| 507 | "El Formato del Servicio asociado a Token es inválido" | rechaza | `<autenticacion>` | 15 |
| 508 | "El Servicio asociado al Token no se corresponde a este sistema" | rechaza | `<autenticacion>` | 15 |
| 509 | "El Token se encuentra Expirado" | rechaza | `<autenticacion>` | 15 |
| 510 | "La CUIT, CUIL o CDI en el Token es Nula, esta Vacia o tiene un Formato inválido" | rechaza | `<autenticacion>` | 15 |
| 511 | "La CUIT, CUIL o CDI no pudo ser encontrada" | rechaza | `<autenticacion>` | 15 |
| 512 | "La CUIL o CDI no esta activa" | rechaza | `<autenticacion>` | 15 |
| 513 | "La CUIT no esta activa" | rechaza | `<autenticacion>` | 15 |
| 514 | "La CUIT no tiene un domicilio activo" | rechaza | `<autenticacion>` | 15 |
| 515 | "La CUIT no tiene una actividad activa" | rechaza | `<autenticacion>` | 15 |
| 516 | "El Servicio de Autenticación no se encuentra Operativo" | rechaza | `<autenticacion>` | 15 |
| 4008 | "Ocurrió un error intentando realizar las validaciones generales para la operacion" | rechaza | todas, `<Request>` | 16 |
| 4009 | "La CUIT no se encuentra en los Registros de AFIP" | rechaza | todas, `<Request>` (`cuitRepresentada` no habilitada como SCA [INFERIDO]) | 16 |
| 4001 | "Ocurrió un error intentando ejecutar la operación" | rechaza (error interno; se informa un código `[xxxyyyzzz-cuitRepresentada-fechaHora-xyz]`) | todas | 18 |
| 4002 | ídem | ídem | todas | 18 |
| 4003 | ídem | ídem | todas | 18 |
| 4004 | ídem | ídem | todas | 18 |
| 4005 | ídem | ídem | todas | 18 |
| 4006 | ídem | ídem | todas | 18 |
| 4007 | ídem | ídem | todas | 18 |
| 4010 | "Ocurrió un error inesperado intentando marcar recibido el lote de Facturas Enviado" | rechaza (lote) | `confirmarRecepcionFacturas`, `<facturas>` | 31 |
| 4011 | "No se pudo marcar como recibida la Factura." | rechaza ítem | `confirmarRecepcionFacturas`, `<factura>` | 31 |
| 4012 | "No se encontró ninguna Factura al SCA con los Datos del Comprobante Enviados que se encuentre Pendiente de Confirmar." | rechaza ítem | `confirmarRecepcionFacturas`, `<factura>` | 31 |
| 4013 | "La Factura ya fue confirmada." | rechaza ítem | `confirmarRecepcionFacturas`, `<factura>` | 31 |
| 4014 | "La Factura debe ser consultada antes de ser confirmada." | rechaza ítem | `confirmarRecepcionFacturas`, `<factura>` | 31 |
| 4015 | "Ocurrió un error inesperado intentando consultar las Facturas Aceptadas" | rechaza | `consultarFacturasAceptadas`, `<facturas>` | 27 |
| 51-53 al 1001-1017 | "Errores Internos de AFIP: Códigos del 51-53 al 1001-1017" | informa (valor de `errorValidacionCbu`) | `consultarFacturasAceptadas`, `factura/errorValidacionCbu` | 26 |
| (Coelsa) | "Errores en el Servicio API Alias de Coelsa: Códigos propios del Servicio API Alias de Coelsa" | informa (valor de `errorValidacionCbu`) | `consultarFacturasAceptadas`, `factura/errorValidacionCbu` | 26 |

(2) "El sistema es parametrizado internamente para ajustar la carga recibida" (pág. 18).

No hay códigos de `observaciones` ni de eventos en el manual. Lista en `wsfecredsca-codigos.json` (43 entradas).

## Tablas y datos

- **Estado de la factura** (`EstadoFacturaSimpleType`): `D` Disponible, `P` Pendiente, `R` Recibida (pág. 32 y documentación del XSD).
- **Tipo de aceptación** (`TipoAceptacionSimpleType`): `T` Tácita, `E` Expresa (pág. 33). Ojo: en `wsfecred` el mismo concepto usa los literales `Tacita`/`Expresa` [WSDL de wsfecred].
- **Tipos de fecha del filtro**: `Disponible`, `Consultada`, `Recibida` (pág. 33).
- **Resultado** (`ResultadoSimpleType`): `A`, `O`, `R` (pág. 32).
- **Códigos de `errorValidacionCbu`**: "del 51-53 al 1001-1017" (AFIP) y los propios de la API Alias de Coelsa (pág. 26). Sin lista ni textos. NO VERIFICADO.
- **Tipos de comprobante** (`tipoCmp`): el manual no los lista. Las facturas aceptadas son FCE de `wsfev1` (`wsfev1.md` §7.1): 201 (A), 206 (B), 211 (C) [INFERIDO: se acepta la factura, y sus ND/NC quedan dentro del saldo].
- **Monedas** (`codMoneda`): códigos de `FEParamGetTiposMonedas` (`wsfev1.md` §7.5) [INFERIDO].
- **Datos de prueba de homologación**: el manual no publica CUITs de SCA ni facturas de prueba.

## Comportamiento a simular

### Protocolo

1. Ruta `/wsfecredsca/FECredSCAService` (con y sin barra final) y `?wsdl` con el WSDL publicado tal cual (incluidos los tipos que no usa ninguna operación) y `soap:address` apuntando a ArcaSim.
2. Sólo SOAP 1.1, hijos sin namespace, validación contra el XSD **antes** de autenticar: si falla, Fault HTTP 500 (o `BL...500` en modo F5, ver `wsfecredagente.md` "Comportamiento a simular → Protocolo").
3. Mismas reglas de serialización y del header `info` que `wsfecredagente`, con `id` `wsfecredsca 1.3.0 2024-12-12T20:19:17.914Z` (producción) o `wsfecredsca 1.2.0 2021-11-01T19:14:02.780Z` (homologación) [VIVO].
4. `dummy` con Body vacío, `<ser:dummy/>` o `GET`.
5. Errores de autenticación con HTTP 200 dentro del resultado: `<facturas/><nroPagina>0</nroPagina><hayMas>N</hayMas><errores>…` o `<errores>…</errores><resultados/>` [VIVO].

### Estado que hay que guardar

Una **puesta a disposición** por factura aceptada con opción SCA:

| Dato | Origen |
|---|---|
| `idFactura` | factura de `wsfev1`/`wsfecred` |
| `tipoAceptacion` | `E` si la aceptó el comprador con `aceptarFECred`; `T` si fue aceptación tácita al vencer el plazo [INFERIDO de pág. 33] |
| `razonSocialEmisor`, `razonSocialComprador`, `cuitComprador` | factura y padrón simulado |
| `cbuEmisor`, `aliasCBUEmisor` | opcionales 2101 (CBU) y 2102 (alias) de la factura en `wsfev1` (`wsfev1.md` §7.7) [INFERIDO] |
| `informaCbuComprador`, `cbuComprador`, `cbuValidada`, `errorValidacionCbu` | `aceptarFECred` (`informaCBU`, `CBUComprador`) y el resultado de validar la CBU [INFERIDO; ver relación abajo] |
| `fechaEmision`, `fechaVencimientoPago`, `codMoneda` | factura |
| `saldoAceptado` | saldo aceptado en `aceptarFECred` |
| `estadoFactura` | `D` al crearse |
| `fechaHoraDisponible` | instante de la aceptación ("instante en que el Comprador Acepto la Factura", pág. 40) |
| `fechaHoraLecturaSCA` | instante de la consulta que la pasa a `P` |
| `fechaHoraConfirmacionSCA` | instante de la confirmación |

### Relación con `wsfecred`

Lo que dicen los manuales:

1. La factura se emite en `wsfev1` con el opcional 27 = `SCA` ("TRANSFERENCIA AL SISTEMA DE CIRCULACION ABIERTA") (`wsfev1.md` §7.7). En `wsfecred` el vendedor puede cambiar entre `ADC` y `SCA` mientras la cuenta corriente esté "Modificable" (`modificarOpcionTransferencia`, [MAN-FECRED] §2.4.8, pág. 34).
2. En `aceptarFECred`, "Opción "Sistema de Circulación Abierta": El receptor optará por informar un CBU Pagador con las implicancias normadas por este Sistema de Circulación Abierta. En consecuencia, la Factura, con su saldo aceptado, será puesta a disposición del Sistema de Circulación Abierta. El comprador ya NO podrá informar la cancelación total en este sistema." ([MAN-FECRED] §2.4.3, pág. 20). **Ése es el momento en que aparece en `D` para el SCA**: "Estas facturas estarán Disponibles para el SCA en el instante en el cual son aceptadas por el contribuyente" (manual pág. 22; también pág. 12).
3. Las facturas aceptadas con opción SCA "quedarán aceptadas sin posibilidad de cambiar su estado" ([MAN-FECRED] pág. 13, nota 1): no se informan a un agente ni se cancelan totalmente; `informarCancelacionTotalFECred` "no es válido para aquella facturas aceptadas con opción de transferencia "Sistema de Circulación Abierta"" ([MAN-FECRED] pág. 32).
4. Reglas de la CBU del comprador en `aceptarFECred` ([MAN-FECRED] págs. 24-25): 12014 "Debe indicar si desea informar la CBU comprador al aceptar una factura con opcion de transferencia de Sistema de Circulacion Abierta"; 2016 (texto: "No corresponde informar CBU cuando el comprobante quedará cancelado totalmente (el saldo aceptado es 0)"); 2017 "No corresponde informar CBU cuando la moneda de la factura es distinta a pesos o dólares"; 9000 (observación) "La CBU no pudo validarse.", con la nota: "La CBU es validada online con los sistemas habilitados por el BCRA. En caso de existir algún inconveniente sistémico, esta operación de aceptación será aceptada y se notificará al contribuyente con este mensaje" (pág. 25). Ese caso es el que llega al SCA con `cbuValidada` = `N` y un `errorValidacionCbu` [INFERIDO].
5. En `wsfecred`, la información de transferencia de una factura SCA (`infoSCA`, [MAN-FECRED] págs. 80-81) tiene `fechaAceptacionFactura` ("Fecha en que fue aceptada la factura y dejada a disposición para la consulta del Sistema de Circulación Abierta"), `informaCBUReceptor`, `CBUReceptor`, `CBUValidada` y `fechaLecturaSCA` ("Fecha y hora en que el Sistema de Circulación Abierta consultó los datos de la factura en el sistema de AFIP"). Correspondencia [INFERIDO]: `fechaAceptacionFactura` ↔ `fechaHoraDisponible`; `informaCBUReceptor` ↔ `informaCbuComprador`; `CBUReceptor` ↔ `cbuComprador`; `CBUValidada` ↔ `cbuValidada`; `fechaLecturaSCA` ↔ `fechaHoraLecturaSCA`. `wsfecred` no expone la confirmación de recepción del SCA (no hay campo equivalente a `fechaHoraConfirmacionSCA`) [WSDL de wsfecred].

No está escrito en ningún manual:
- Si las facturas con **aceptación tácita** llegan al SCA. El enum `T` lo sugiere [INFERIDO]. Con aceptación tácita no hay CBU del comprador, así que irían con `informaCbuComprador` = `N` [INFERIDO].
- Si una factura aceptada con **saldo 0** (cancelación total en la aceptación) llega al SCA. NO VERIFICADO.
- Si una factura **rechazada** por el comprador llega al SCA: por el propósito ("facturas aceptadas"), no [INFERIDO].

### Transiciones

| Estado actual | Evento | Resultado | Fuente |
|---|---|---|---|
| (no existe) | aceptación con opción SCA en `wsfecred` | `D`, `fechaHoraDisponible` = instante de aceptación | [MAN] págs. 12, 22 y 32 |
| `D` | aparece en una respuesta de `consultarFacturasAceptadas` | `P`, `fechaHoraLecturaSCA` = ahora | [MAN] págs. 12 y 22 |
| `P` | se consulta de nuevo (filtro `P` o sin filtro) | sigue `P` ("para volver a consultar facturas que puedan haber sido consultadas pero no leídas", pág. 22) | [MAN] pág. 22 |
| `R` | se consulta (filtro `R`) | sigue `R` ("historial") | [MAN] pág. 22 |
| `P` | `confirmarRecepcionFacturas` | `R`, `fechaHoraConfirmacionSCA` = ahora, ítem `resultado` = `A` | [MAN] págs. 12, 28 y 32 |
| `D` | confirmar | 4014 "La Factura debe ser consultada antes de ser confirmada." | [MAN] pág. 31 |
| `R` | confirmar de nuevo | 4013 "La Factura ya fue confirmada." | [MAN] pág. 31 |
| no existe | confirmar | 4012 | [MAN] pág. 31 |

**Idempotencia**: confirmar dos veces no es idempotente; la segunda devuelve 4013 en ese ítem. El SCA no puede rechazar una factura: sólo confirma recepción.

Casos abiertos (configurables en ArcaSim):
- **Qué pasa a `P`**: lo más seguro es pasar sólo las `D` devueltas en la página [INFERIDO]. Si pasaran todas las que cumplen el filtro, la página 2 de un filtro `D` vendría vacía. NO VERIFICADO.
- **`fechaHoraLecturaSCA` en relecturas**: si se pisa en cada consulta de una `P` o queda la primera: NO VERIFICADO (lo natural es la primera [INFERIDO]).
- Facturas de otro SCA: el manual habla de "el Sistema de Circulación Abierta" en singular. Si hay más de una CUIT habilitada y cómo se reparten las facturas: NO VERIFICADO. En ArcaSim, una lista configurable de CUITs habilitadas que ven todas las facturas SCA [recomendación].

### Paginación, filtros y vacío

- `nroPagina` obligatorio; respuesta con `nroPagina` y `hayMas`. Primera página 1 y tamaño dinámico, como en `wsfecred` ([MAN-FECRED] §2.4.9, pág. 36) [INFERIDO]. Tamaño configurable. Página inválida → 2004 [INFERIDO para el criterio].
- `filtroFechas/tipo`: `Disponible` → `fechaHoraDisponible`, `Consultada` → `fechaHoraLecturaSCA`, `Recibida` → `fechaHoraConfirmacionSCA` (pág. 33 y documentación del XSD). `desde`/`hasta` inclusivas [INFERIDO]; `desde` > `hasta` o rango mayor al configurado → 2003 [INFERIDO].
- `cuitEmisor` y `cuitReceptor` filtran por `idFactura/cuitEmisor` y `cuitComprador` [INFERIDO por el nombre].
- Sin resultados: `<facturas/>`, `hayMas` = `N` [WSDL: 0 elementos]. Si viene alguna observación (en `wsfecred` es la 32767) y qué `nroPagina` se devuelve: NO VERIFICADO. Con error, el real devuelve `nroPagina` = `0` [VIVO].

### Lotes

`confirmarRecepcionFacturas` acepta 1..n facturas; límite configurable (2006). Resultado por factura en `resultados/resultado` (pág. 28: "retornará el resultado sobre cada una de las facturas informadas en el arreglo"). Si una falla, las demás se procesan igual [INFERIDO de "el resultado sobre cada una"].

### Autorización

Token de `service` = `wsfecredsca` (508 si no). `cuitRepresentada` = "CUIT del SCA", en las relaciones del token; si no está habilitada → 4009 [MAN págs. 14-16].

## No verificado

1. Contenido del SOAP Fault real detrás de `BL...500` y si el ejemplo `[wscommon_007]` del manual sigue vigente en algún caso.
2. Si 502 y 1001 aparecen alguna vez dentro del resultado o siempre terminan en Fault.
3. Lista y significado de los códigos de `errorValidacionCbu` ("del 51-53 al 1001-1017" y los de Coelsa).
4. Si la consulta pasa a `P` sólo las facturas de la página devuelta o todas las del filtro; si el `estadoFactura` devuelto es el anterior o el posterior; si `fechaHoraLecturaSCA` se actualiza en relecturas.
5. Si llegan al SCA las facturas con aceptación tácita, las de saldo aceptado 0 y las de moneda distinta de pesos o dólares.
6. Qué CUIT(s) están habilitadas como SCA y si varias ven el mismo universo de facturas.
7. Tamaño de página, primera página, criterio de 2004, `nroPagina` devuelto sin datos y observación de "sin resultados".
8. Rango máximo de fechas (2003) y máximo de facturas por lote (2006).
9. Formato de `xsd:dateTime` en las respuestas.
10. Textos reales de 2xxx y 40xx (si llevan sufijo `. {...}` como los de autenticación).
11. Códigos 503, 504 y 507-516, 4008, 4009 y 4001-4007 en vivo (hace falta certificado de homologación asociado a `wsfecredsca`).
12. Enrutamiento por `SOAPAction`, SOAP 1.2 y endpoint con barra final en este servicio (se probaron sólo en `wsfecredagente`).
13. Cuándo se usa el resultado `O` y el elemento `observaciones` de la consulta (el manual no los menciona).
14. Diferencias de comportamiento entre homologación (1.2.0) y producción (1.3.0): el changelog dice que no hay impacto funcional.
15. Valores de `dummy` distintos de `OK`; eventos publicados por ARCA.
