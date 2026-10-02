# SETIWS-PAGO-API

API REST para que un organismo externo **cree VEPs** (Volantes Electrónicos de Pago), los envíe a una Entidad de Pago (EDP: Red Link, Banelco, Interbanking, o ninguna para pago por QR) y después **consulte el VEP y su comprobante de pago (CP)**. Reemplaza a WSCREATEVEP. "Es condición mandataria que el organismo solicitante posea un convenio de intercambio de información vigente" (manual) y, desde la versión 1.0.2, "El servicio es de uso exclusivo para organismos que tengan un convenio con ARCA".

Es el único servicio del catálogo que no es SOAP.

Fuentes:

- Manual 1.1.1 (25/08/2026): `https://www.afip.gob.ar/ws/SETIWS-PAGO-API/Manual_para_el_desarrollador_setiws-pago-api.pdf`.
- Anexo VEP F3012, Anticipo para el Pago de Tributos Aduaneros, 1.0.0 (25/06/2026): `https://www.afip.gob.ar/ws/SETIWS-PAGO-API/Manual_para_el_desarrollador_setiws-pago-api-anexo-F3012.pdf`.
- OpenAPI de homologación, descargado el 2026-10-02.
- Llamadas reales sin credenciales a homologación, 2026-10-02.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | REST/JSON (también acepta XML en el POST y responde XML en el GET según `Accept`). Spring Boot con springdoc, detrás de un gateway de autenticación (ver abajo) |
| Base homologación | `https://seti-setipago-api-qaext.arca.gob.ar/` |
| Base producción | `https://setiws-pago-api.arca.gob.ar/` |
| OpenAPI | `GET /v3/api-docs` (JSON, OpenAPI 3.1.0) en los dos ambientes; Swagger UI en `/swagger-ui/index.html` (homologación, HTTP 200) |
| Archivo guardado | `wsdl/SETIWS-PAGO-API-homologacion.json` |
| `info.version` | `1.0.26` (homologación y producción) |
| `servers` del OpenAPI | `https://seti-setipago-api-qa-ext.dqs.afip.gob.ar/` ("QA server URL"), un host interno que no es el público. En producción, `info` y `servers` traen los placeholders sin resolver: `${swagger.api.title}`, `${swagger.api.host.url}`, etc. Fuera de eso, los dos documentos son iguales |
| Auth | **No usa el SOAP de WSAA en el body**. Headers HTTP: `Authorization: Bearer <JWT>` de WSAUTH, **o** `WSAA-AUTH-PROXY-TOKEN` + `WSAA-AUTH-PROXY-SIGN` (el par token/sign de un TA de WSAA), más `WSAA-AUTH-PROXY-REPRESENTADO: <CUIT>`. La segunda variante no está en el manual: la anuncia el propio gateway en su mensaje de error (ver Autenticación) |
| ID del servicio | `seti-setipago-api`. El manual dice "El ID del servicio es seti-setipago-api" (en el PDF las ligaduras "ti" se pierden y se lee "se -se pago-api"). **Lo confirma el gateway**: "Servicios admitidos: \"seti-setipago-api\"" |
| Security en el OpenAPI | Ninguna: no hay `securitySchemes`. La autenticación la hace el gateway, no la app |

### Endpoints

| Método y ruta | operationId | Auth | Éxito según el manual | Éxito según el OpenAPI |
|---|---|---|---|---|
| `GET /dummy` | `getStatus` | No | 200 `{"appserver":"OK","dbserver":"OK"}` | 200, `DummyResponse` |
| `POST /api/v1/veps` | `createVep` | Sí | **201 Created** `{"nroVEP": 55495965, "fechaExpiracion": "2026-09-30"}` | **200**, `ExternalVepReceptorResponseDTO{vepId, fechaExpiracion (date-time), qrBase64, entidadDePagoUrl}` |
| `GET /api/v1/veps` | `findMyVEPByTransactionId` | Sí | 200, `{"VEP": {...}, "CP": {...}}` | 200, `CPVEP{CP, VEP, qrBase64, entidadDePagoUrl}`, en `application/json` o `application/xml` |

Contradicciones entre manual y OpenAPI que el simulador tiene que resolver:

- **Nombre del campo del número de VEP en la respuesta del POST**: el manual dice `nroVEP` y el OpenAPI, `vepId`. **NO VERIFICADO** cuál sale de verdad. El manual es más nuevo (1.1.1, agosto 2026) y muestra ejemplos concretos, así que conviene seguirlo y documentar la duda.
- **Código de éxito del POST**: 201 (manual) contra 200 (OpenAPI, que springdoc genera por defecto aunque el controller devuelva 201). Lo más probable es 201: **NO VERIFICADO**.
- **Parámetros del GET**: el OpenAPI marca `owner-transaction-id`, `owner-cuit` y `nro-vep` como `required: true`. El manual dice que solo `owner-cuit` es obligatorio y que "Alternativamente puede realizarse la búsqueda por nro-vep o owner-transaccion-id" [sic]. El ejemplo del manual usa solo `nro-vep` y `owner-cuit`.
- El ejemplo del manual escribe `POST /api/v1/veps=with-qr=true`; es una errata de `?with-qr=true`.

## Autenticación

Headers (manual, "Acceso al Servicio"):

| Header | Valor |
|---|---|
| `Authorization` | `Bearer <JWT>`, "JWT emitido por WSAUTH. Consultar el manual del WSAUTH." |
| `WSAA-AUTH-PROXY-REPRESENTADO` | CUIT del organismo representado. "Debe coincidir con alguna de las relaciones incluidas en el JWT." Si no hay delegación, la CUIT del propio organismo |

El manual de WSAUTH **no está publicado** en las páginas de `/ws/` revisadas (catalogo.asp, arquitectura-general.asp y wsaa.asp no lo enlazan). El formato del JWT, quién lo emite y cómo se pide están **NO VERIFICADOS**.

**Respuestas reales del gateway** (homologación, 2026-10-02). Todas son **HTTP 401**, `Content-Type: application/json`, `Transfer-Encoding: chunked`, con la forma:

```json
{"error":{"message":"<texto>","code":"401","date":"2026-10-02T15:13:12-03:00"},"status":"error"}
```

`code` es string; `date` es ISO con offset `-03:00`; los mensajes múltiples se separan con `\n`; las `/` dentro del texto salen escapadas como `\/`.

| Caso | `message` |
|---|---|
| Sin ningún header | `Falta header con representado seleccionado ("WSAA-AUTH-PROXY-REPRESENTADO").\nFaltan headers requeridos de autenticación\/autorización ("Authorization" o "WSAA-AUTH-PROXY-TOKEN" y "WSAA-AUTH-PROXY-SIGN").` |
| `Authorization: Bearer abc`, sin representado | `Falta header con representado seleccionado ("WSAA-AUTH-PROXY-REPRESENTADO").` |
| `Authorization: Bearer abc` (o `Basic abc`) con representado | `El header "Authorization" no contiene un JWT válido.` |
| JWT con forma válida (`header.payload.firma`, RS256 falso, sin claim de fecha esperado) | `attempt to perform arithmetic on local 'date' (a nil value)`: es un error de Lua del gateway que se filtra. Indica un gateway OpenResty/Kong con plugin propio "WSAA-AUTH-PROXY" |
| `WSAA-AUTH-PROXY-TOKEN: abc`, `WSAA-AUTH-PROXY-SIGN: abc` | `La firma no es válida para el token.\nFormato inválido del token.` |
| `WSAA-AUTH-PROXY-TOKEN` = TA bien formado en base64 de otro servicio, vencido, con otra CUIT en relations y firma falsa | `El token no es válido para este servicio. (Servicio autorizado: "veconsumerws". Servicios admitidos: "seti-setipago-api").\nEl token está vencido. (Vencimiento: 2026-09-21T23:13:20-03:00. Hora del servidor: 2026-10-02T15:13:24-03:00).\nLa CUIT representada no está entre las autorizadas. (Seleccionada: 20131507969. Autorizadas: 20111111112).\nLa firma no es válida para el token.` |
| Ruta inexistente (`/api/v1/nada`) sin headers | El mismo 401 de "Falta header...": el gateway autentica antes de rutear |

El gateway valida **todo junto** y acumula los errores (servicio, vencimiento, relations, firma) en un solo mensaje. Es el comportamiento más útil para copiar.

Headers de las respuestas reales: `Strict-Transport-Security: max-age=15724800; includeSubDomains` y, en homologación, dos cookies de F5 (`f5avraaaaaaaaaaaaaaaa_session_=...; HttpOnly; secure;` y `TS01907152=...; Path=/; Domain=.seti-setipago-api-qaext.arca.gob.ar;`). La línea de estado del 200 es `HTTP/1.1 200 ` (sin texto de razón).

## Operaciones

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| `GET /dummy` | Salud del servicio | — | `{"appserver":"OK","dbserver":"OK"}`. Valores "OK" o "ERROR" | — |
| `POST /api/v1/veps[?with-qr=true]` | Crea un VEP y lo envía a la EDP | Body `EdpVep{entidadDePago, vep: VEP}` en JSON, o XML `<createVEP><entidadDePago/><VEP ownerCuit=".." ...><Obligaciones impuesto=".." importe=".."/></VEP></createVEP>` (atributos, ejemplo del manual) | `{nroVEP, fechaExpiracion}` (+ `qrBase64`, `entidadDePagoUrl` según la tabla de QR) | **Crea**. Clave: `nroVEP`. Idempotente por (`vep.ownerCuit`, `vep.ownerTransactionId`): si ya existe, devuelve el `nroVEP` existente |
| `GET /api/v1/veps?owner-cuit=&nro-vep=&owner-transaction-id=&with-qr=` | Consulta un VEP y su CP | Query (ver contradicción arriba). `Accept: application/json` (default) o `application/xml` | `{"VEP": VEP, "CP": CP?}` (+ `qrBase64`, `entidadDePagoUrl`) | Consulta. Claves: `nroVEP`, o `ownerTransactionId` + `ownerCuit` |

**VEP** (OpenAPI; requeridos: `importe`, `obligaciones`): `nroVEP` (int64), `fechaHoraCreacion` (19 caracteres, `YYYY-MM-DD HH:MI:SS`), `fechaExpiracion` (10, `YYYY-MM-DD`; default "hoy + 25 días"), `nroFormulario`, `orgRecaudDesc` (default "ARCA"), `codTipoPago`, `pagoDesc`, `pagoDescExtracto` (máx. 22), `usuarioCUIT` (si falta, sale del token; si viene, "se valida que exista en el padrón"), `contribuyenteCUIT`, `establecimiento` (default 0), `concepto` (default 19), `conceptoDesc`, `subConcepto` (default 19), `subConceptoDesc`, `periodoFiscal` (int `YYYYMM`; MM entre 00 y 12, 00 para períodos anuales), `anticipoCuota` (default 0), `importe` (number; 0.01 a 9999999999.99), `ownerCuit`, `ownerTransactionId`, `obligaciones[]`, `detalles[]`, `idVepDuplicado`, `presentacionNroTransaccion`, `pagadorCuit`, `origenPago`.

- **Obligacion**: `impuesto` (1 a 9999), `impuestoDesc`, `importe` (requerido). Según el anexo F3012, "La sumatoria de estos importes debe coincidir con el importe total del VEP".
- **Detalle**: `campo` (requerido), `campoTipo` (`N` default o `C`), `campoDesc`, `contenido` (requerido, 1 a 60), `contenidoDesc`.
- **CP** (requeridos: `bancoPagador`, `contribuyenteCUIT`, `entidadDePago`, `fechaHoraPago`, `fechaPosting`, `importe`, `nroTransaccion`, `nroVEP`, `tipoSucursal`): además `cpId`, `sucursal`, `terminal`, `operador`, `formaPago`, `moneda` (1 pesos, 4 USD), `codControl`, `nroTarjeta`, `posEstablecimiento`, `posNombre`, `cbu`, `codTipoPago`, `codRechazoDebDir` y `fechaAnulacion`.
- En el request del manual los números van como strings (`"ownerCuit": "33715676929"`, `"importe": "1446.00"`); en la respuesta salen como números (`"importe": 602.0`). Jackson acepta las dos formas.

**Regla de QR y URL de EDP** (manual; la tabla del PDF está desarmada, se reconstruye así): `qrBase64` y `entidadDePagoUrl` solo salen si el VEP está **pendiente de pago**. Con `with-qr=true` sale `qrBase64` (`data:image/jpeg;base64,...`). Si además `entidadDePago` > 0, sale `entidadDePagoUrl`. Con `with-qr` ausente o false, la tabla del PDF parece indicar que con EDP > 0 sale solo `entidadDePagoUrl`; la combinación exacta está **NO VERIFICADA**.

Tablas del manual:

- **EDP**: `0` = no se envía a ninguna EDP, queda disponible para pago QR; `1001` RED LINK; `1002` BANELCO; `1003` INTERBANKING. En el PDF los nombres están corridos una fila; esta asignación es la que cuadra con 4 ids y 4 textos.
- **TIPO_SUCURSAL**: 3 Sucursal Bancaria, 6 Debito Directo, 7 Cajero Automatico, 8 HomeBanking, 9 Pago Telefonico, 11 Terminal POS Aduana, 13 Sucursal Bancaria SEPSA - Pago Facil, 15 Internet Bancos Pagos, 16 HomeBanking SETI VISA, 17 Sucursal Bancaria - Pagos SETI, 18 a 20 Sucursal Banelsip NN - Punta de Caja, 21 Home Banking SETI Master, 22 Transferencia Bancaria del Exterior, 23 Sucursal Bapro Pagos 23 - Punta de Caja, 24 PRESTADOR POR CUENTA Y ORDEN - PAGO EFECTIVO, 25 Efectivo Dolar, 30 Caja - Pago Electrónico, 31 a 33 Punta de Caja NN Pago Electronico Banelsip, 34 Pago Facil 34 Pago Electronico SEPSA, 35 Punta de Caja 35 Pago Electronico Bapro Pagos, 36 PRESTADOR POR CUENTA Y ORDEN - PAGO ELECTRONICO, 37 Aplicaciones Móviles, 38 QR - Aplicaciones Móviles, 39 VEP Carga Manual - Aplicaciones Móviles, 40 VEP disponibilizados en Aplicaciones Móviles.
- **FORMA_PAGO**: 1 EFECTIVO, 2 CHEQUE 24 HS, 3 CHEQUE 48 HS, 4 CHEQUE 72 HS, 5 EFECTIVO USD, 41 a 43 ORD.ENT.NNHS.S-2000, 62 a 64 ORD.ENTREGA NN HS, 68 TARJETA DE CREDITO, 69 TARJETA DE DEBITO, 91 DINERO EN CUENTA.

Ejemplo de formulario concreto (anexo F3012): `nroFormulario` 3012, `codTipoPago` 932, `concepto`/`subConcepto` 800 ("PAGO ADUANERO"), obligación con `impuesto` 2555 ("ANTIC. PAGO TRIBUTOS ADUANEROS"), detalle `campo` 2 = CUIT del despachante (`campoDesc` "CUIT DESPACHANTE", `contenidoDesc` con el nombre), `orgRecaudDesc` "ARCA - ADUANA", `pagoDescExtracto` "ANTIC".

## Errores

Hay **dos formatos**, según quién responde:

1. **Gateway** (autenticación): `{"error":{"message","code","date"},"status":"error"}` con HTTP 401. Verificado en vivo (tabla de arriba).
2. **Aplicación** (manual, "Manejo de errores"): HTTP 4xx o **555**, con:

```json
{"id": "UUID del error", "timestamp": "2026-04-01T10:30:00", "message": "Descripción del error",
 "url": "URL invocada", "method": "GET", "status": 400, "type": "Nombre de la excepción"}
```

| status | type | Cuándo (manual; la columna del PDF está desalineada, se asigna por orden) |
|---|---|---|
| 400 | `VepCreationException` | Formulario no permitido para EDP 0 |
| 400 | `InvalidContribuyenteException` | CUIT del contribuyente inválida o inexistente |
| 400 | `ValidationException` | EDP inválida; VEP no informado; EDP no acepta el tipo de pago del VEP; VEP sin owner/ownerTransaction; `ownerCuit` del VEP distinto de la CUIT del sistema de autenticación; formulario inexistente; impuesto, concepto o subconcepto inválido |
| 400 | `InputFormularioException` | Falta un dato del VEP o el dato es inválido para el formulario |
| 400 | `MethodArgumentNotValidException` | El request no pasó las validaciones del modelo |
| 400 | `UnsatisfiedServletRequestParameterException` | El request no pasó las validaciones del modelo |
| 400 | `HttpMediaTypeNotSupportedException` | Content-Type que la API no entiende |
| 400 | `HttpRequestMethodNotSupportedException` | Método HTTP no definido para esa URL |
| 400 | `MethodArgumentTypeMismatchException` | Tipo de dato distinto del esperado |
| 400 | `MissingServletRequestParameterException` | Falta un parámetro requerido |
| 400 | `HttpMessageNotReadableException` | JSON mal formado |
| 403 | `ForbiddenException` | `ownerCuit` no autorizada a generar VEPs del formulario |
| 404 | `VepNotFoundException` | VEP inexistente |
| 404 | `OwnerCuitException` | El VEP pertenece a otro `ownerCuit` |
| 555 | `InternalSeverError` [sic] | Error atribuible a ARCA |

## Comportamiento a simular

- `GET /dummy` sin auth, siempre `{"appserver":"OK","dbserver":"OK"}`.
- Gateway delante de todo (incluso rutas inexistentes): aceptar `Authorization: Bearer` (JWT emitido por un WSAUTH simulado) **o** `WSAA-AUTH-PROXY-TOKEN`/`WSAA-AUTH-PROXY-SIGN` de un TA del WSAA simulado con servicio `seti-setipago-api`; exigir `WSAA-AUTH-PROXY-REPRESENTADO` en las relations. Errores acumulados en un solo 401 con los textos exactos de arriba.
- `POST /api/v1/veps`: validar que `vep.ownerCuit` sea el representado, que la suma de obligaciones sea igual a `importe`, EDP en {0, 1001, 1002, 1003}, formulario y tipo de pago en un catálogo configurable; asignar `nroVEP` secuencial y `fechaExpiracion` = hoy + 25 días si no viene. Idempotencia: el mismo (`ownerCuit`, `ownerTransactionId`) devuelve el mismo `nroVEP`.
- `GET /api/v1/veps`: buscar por `nro-vep` o por `owner-transaction-id`, siempre con `owner-cuit`; 404 `VepNotFoundException` o `OwnerCuitException`. Sin pago, sin `CP`.
- Par de estado: VEP creado → pendiente (con QR si se pide) → un hook de prueba de ArcaSim simula el pago de la EDP y genera el `CP` → el GET devuelve `VEP` + `CP` y ya no ofrece QR. La transición del pago no tiene endpoint en la API: el pago lo informa la EDP por fuera.
- Responder XML en el GET si `Accept: application/xml` (estructura **NO VERIFICADA**: serialización Jackson XML de `CPVEP`).

## No verificado

- El manual de WSAUTH y el formato del JWT (claims, firma, emisor). El gateway espera al menos un claim de fecha, por el error de Lua.
- `nroVEP` o `vepId` en la respuesta del POST, y 201 o 200.
- Si el GET exige los tres parámetros (OpenAPI) o acepta `nro-vep` u `owner-transaction-id` (manual).
- El formato XML real del POST (el ejemplo usa atributos) y de la respuesta del GET.
- La combinación exacta de `with-qr` y EDP que hace aparecer `entidadDePagoUrl`.
- Los textos de `message` de cada error de aplicación (el manual solo da la descripción).
- Si existe una URL de QR o de EDP real a la que redirigir en homologación.
