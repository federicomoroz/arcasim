# wgestiendaslibres

"Control de Stock en Depósitos de Tiendas Libres" (free shops). Lo usan los permisionarios de tiendas libres y sus depósitos (tipo de agente `TILI`) para llevar el stock aduanero: ingreso de mercadería al depósito mayor, traslados entre depósitos, ventas, notas de crédito y débito, armado y desarmado de packs, destrucción, devolución al proveedor, cambio de código de producto, justificación de diferencias por stock negativo (DIFE), salida de oficio de una particular y consultas de stock, movimientos, packs y DIFE (manual, "Objetivo y alcance", p.4).

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/documentos/ManualDesa-wgestiendaslibres.pdf`, 71 páginas, historial hasta la versión 1.2 (última fecha 05/03/2026). Descargado el 2026-10-02 (HTTP 200). Páginas impresas ("Página N de 71"). La extracción de texto pierde las ligaduras (`"` en lugar de "ti", `L` en lugar de "tt") y desalinea varias tablas de errores.
- WSDL de homologación: `docs/arca/wsdl/wgestiendaslibres-homologacion.wsdl`, de `https://wsaduhomoext.afip.gob.ar/diav2/wgestiendaslibres/wgestiendaslibres.asmx?WSDL` (2026-10-02). Esquema inline, sin XSD aparte.
- WSDL de producción: `docs/arca/wsdl/wgestiendaslibres/wgestiendaslibres-produccion.wsdl`, de `https://webservicesadu.afip.gob.ar/diav2/wgestiendaslibres/wgestiendaslibres.asmx?WSDL`. Idéntico salvo `soap:address`.
- El catálogo decía "Sin URL en el manual". **La URL sí está** en la tabla "Especificaciones del servicio" (p.5), pero la extracción la dejó mezclada con los nombres de métodos. Este servicio **no** está en `testdia`/`servicios3`: `https://testdia.afip.gov.ar/Dia/Ws/wgestiendaslibres/...` devuelve la página HTML de error 500 que `testdia` da para cualquier `.asmx` inexistente, y `servicios3.arca.gob.ar` redirige (302) a `mgenErrorFileNotFound.htm`.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:15 (-03:00).
- Catálogo: "Control de stock en tiendas libres", tipo de agente `TILI`, sin README ni resolución enlazada.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "diav2" (servidores `wsaduhomoext`/`webservicesadu`): resultado `ResultadoEjecucionSerializable` con `ListaErrores/DetalleError`, `Server` y `TimeStamp`. Las fallas del framework salen **en inglés** | WSDL; prueba en vivo |
| Endpoint homologación | `https://wsaduhomoext.afip.gob.ar/diav2/wgestiendaslibres/wgestiendaslibres.asmx` | `soap:address`; manual p.5 |
| Endpoint producción | `https://webservicesadu.afip.gob.ar/diav2/wgestiendaslibres/wgestiendaslibres.asmx` | WSDL de producción; manual p.5 |
| Namespace | `Ar.Gob.Afip.Dga.wGesTiendasLibres`. Los ejemplos del manual usan `ar.gov.afip.dia.serviciosweb.wgestiendaslibres` (y una vez `wgestiendasslibres`), que **no** es el del servicio | WSDL |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `wgestiendaslibresSoap` (1.1) y `wgestiendaslibresSoap12` (1.2) | WSDL |
| SOAPAction | `Ar.Gob.Afip.Dga.wGesTiendasLibres/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | **NO VERIFICADO**. El catálogo no publica README para esta entrada y el manual no lo dice | — |
| Operaciones | 17 (16 de negocio + `Dummy`) en los dos ambientes | WSDL |
| Headers | `Cache-Control: private, max-age=0`, `Content-Type: text/xml; charset=utf-8`, `X-Frame-Options: SAMEORIGIN`, `X-Xss-Protection: 1; mode=block`, `X-Content-Type-Options: nosniff`, `Vary: Accept-Encoding` y cookies de F5 (`f5avr..._session_`, `TS010aafc4`) | Prueba en vivo |

## Autenticación

Primer parámetro de todos los métodos de negocio: `argWSAutenticacionEmpresa`, tipo `WSAutenticacionEmpresa` que extiende `WSAutenticacion`. Orden XML:

```xml
<argWSAutenticacionEmpresa>
  <Token>...</Token>                                 <!-- 0..1 -->
  <Sign>...</Sign>                                   <!-- 0..1 -->
  <CuitEmpresaConectada>...</CuitEmpresaConectada>   <!-- long, 1..1 -->
  <TipoAgente>TILI</TipoAgente>                      <!-- 0..1 -->
  <Rol>TILI</Rol>                                    <!-- 0..1 -->
</argWSAutenticacionEmpresa>
```

El manual (p.7) pide `TipoAgente` y `Rol` = `TILI` (antes `TLIB`, cambio de la versión 0.6). `CuitEmpresaConectada` es `long` en el WSDL: un valor no numérico rompe la deserialización.

Tabla del manual (pp.7-8): 6003, 6005, 6006, 6007 (Aduana invalida para el CUIT y el tipo de agente informados), 6008 (Lugar Operativo invalido para el CUIT y la Aduana informados), 6009, 6012 (Tipo de Agente invalido para el servicio solicitado), 7000 (mensajes varios de conexión), 7001 (No se encontro la empresa conectada en la lista de empresas del token), 7004 Error Interno, 7005 Token vencido, 7006 Firma no informada, 7007 Token no informado, 7008 Token Inválido, 7013 El token recibido no pudo ser identificado, 7015 El proceso de autenticación de firma no fue superado, 7016 El Servicio no se corresponde con el informado en el Token, 7017 No existe coincidencia entre el servicio invocado y el informado en el token, 7025 Cuit con el que desea operar no informado; siguen sin código en la extracción "Tipo Agente ... no informado", "Rol ... no informado", "Web Method no autorizado o inexistente" y "Método no vigente".

Observado con `ConsultarStock` (homologación, 2026-10-02). Todas HTTP 200:

| Caso | `ListaErrores/DetalleError` |
|---|---|
| `Token`=`abc` | `<Codigo>7008</Codigo><Descripcion>token invalido</Descripcion><DescripcionAdicional>El Token no se encuentra en formato base 64.</DescripcionAdicional>` |
| `Token` y `Sign` vacíos | `<Codigo>7007</Codigo><Descripcion>Token no informado</Descripcion>` |
| Sin `argWSAutenticacionEmpresa` | `<Codigo>42034</Codigo><Descripcion>Falta el dato obligatorio argWSAutenticacionEmpresa</Descripcion><DescripcionDetallada />` |
| Base64 de `<sso><id/></sso>` | `<Codigo>7004</Codigo><Descripcion>Identificador del Error: 6000</Descripcion>` |
| `sso` 2.0 bien formado, `Sign` falso | `<Codigo>7058</Codigo><Descripcion>Token o Firma invalidos</Descripcion>` (código que **no** figura en el manual) |

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><ConsultarStockResponse xmlns="Ar.Gob.Afip.Dga.wGesTiendasLibres"><ConsultarStockResult><ListaErrores><DetalleError><Codigo>7008</Codigo><Descripcion>token invalido</Descripcion><DescripcionAdicional>El Token no se encuentra en formato base 64.</DescripcionAdicional></DetalleError></ListaErrores><Server>10.30.32.108</Server><TimeStamp>2026-10-02T15:08:50.6877888-03:00</TimeStamp></ConsultarStockResult></ConsultarStockResponse></soap:Body></soap:Envelope>
```

**El servidor real no es válido contra su propio WSDL**: el tipo `Mensaje` declara `Codigo`, `Descripcion`, `DescripcionDetallada` y `TextoAclaratorio`, pero el 7008 sale con `DescripcionAdicional`, que no existe en el esquema. El 42034 sí usa `DescripcionDetallada`. Un cliente generado desde el WSDL ignora `DescripcionAdicional` en silencio.

Otros detalles: `Server` es la IP interna (`10.30.32.108`) y `TimeStamp` lleva 7 decimales y zona `-03:00`.

## Operaciones

Todas las respuestas extienden `ResultadoEjecucionSerializable` (`ListaErrores`, `Server`, `TimeStamp` 1..1) y suman los campos de la última columna. Los parámetros de negocio van en un segundo elemento `arg<Operación>Params`, con una excepción: **`GetMovimiento` recibe `argConsultarMovimientosParams`** (de tipo `GetMovimientoParams`).

| Operación | Propósito | Entrada principal | Salida propia | Estado |
|---|---|---|---|---|
| `IngresarMercaderia` | Ingreso al depósito mayor, de origen extranjero (declaración PIxx) o nacional (trámite SITA) | `aduana`, `lugarOperativo`, `idComprobante`, `origen`, `comprobanteAsociado`, `transaccion`, `ListaMercaderiaIngresada` | `id`, `idMovimiento` | Crea. **No** suma stock por sí solo (ver abajo) |
| `TrasladarMercaderia` | Traslado entre depósitos RETL/VATR | `aduanaOrigen`, `lugarOperativoOrigen`, `aduanaDestino`, `lugarOperativoDestino`, `valorTotalMercaderia`, `nroPoliza`, `nroCarro`, `tipoTraslado`, `transaccion`, `listaMercaderiaRETL` | `IdRETL`, `idMovimiento` | Resta en origen, suma en destino |
| `VentaMercaderia` | Venta en tienda | `aduana`, `lugarOperativo`, `tipoLocal`, `docIdentidad`, `nacionalidad`, `edad` (int 1..1), `tipoComprobante`, `nroComprobante`, `indContingencia`, `nroVuelo`, `listaMercaderiaVendida`, `transaccion` | `idMovimiento` | Resta stock; con stock insuficiente igual registra y crea DIFE |
| `RegistrarNotaDebitoCredito` | ND/NC sobre una venta previa | `aduana`, `lugarOperativo`, `aduanaDestino`, `lugarOperativoDestino`, `tipoComprobante`, `nroComprobante`, `fechaComprobante`, `tipoComprobanteAsociado`, `nroComprobanteAsociado`, `tipoLocal`, `indContingencia`, `transaccion`, `listaMercaderiaNotaDebitoCredito` | `idMovimiento` | Ajusta stock. Clave de enlace: comprobante asociado = venta |
| `ArmarPack` | Arma un pack | `aduana`, `lugarOperativo`, `NCM`, `codProducto`, `descProducto`, `origen`, `cantidad`, `valorUnitarioDol`, `transaccion`, `listaItems` | `idMovimiento` | Resta ítems, suma el pack. Clave: `codProducto` del pack |
| `DesarmarPack` | Desarma packs no vendidos | `aduana`, `lugarOperativo`, `NCM`, `codProducto`, `origen`, `cantidad`, `transaccion` | `idMovimiento` | Inversa de `ArmarPack` |
| `DestruirMercaderia` | Destrucción | `aduana`, `lugarOperativo`, `idComprobante`, `listaMercaderiaDestruida`, `transaccion` | `idMovimiento` | Resta stock |
| `DevolverMercaderia` | Devolución al proveedor | `aduana`, `lugarOperativo`, `origen`, `idComprobante`, `idActa`, `listaMercaderiaDevuelta`, `transaccion` | `idMovimiento` | Resta stock |
| `CambiarCodigoProducto` | Cambia código de producto y/o NCM | `NCMAnterior`, `codProductoAnterior`, `NCMNueva`, `codProductoNuevo`, `descProducto`, `transaccion` | `idMovimiento` | Renombra clave de stock |
| `RegistrarJustificacionDIFE` | Justifica una DIFE (motivos `CANT` o `LOTI`) | `idDIFE`, `listaJustificacion`, `transaccion` | `idMovimiento` | DIFE `REG`/`REC` → `PRE` |
| `SalidaParticular` | Salida y control de salida de oficio de una particular PIxx, sin aviso de carga | `aduana`, `lugarOperativo`, `idDeclaracion`, `precinto`, `contenedor`, `nombrePortador`, `tipoDocPortador`, `nroDocPortador`, `transaccion` | `nroSalida` | Habilita el stock del ingreso extranjero |
| `ConsultarStock` | Stock de un depósito | `Aduana`, `LugarOperativo`, `NCM`, `CodProducto`, `Origen` | `ListaStockMercaderia` | Consulta |
| `ConsultarMovimientos` | Movimientos en un período | `Aduana`, `LugarOperativo`, `FechaDesde`, `FechaHasta` | `ListaMovimientosMercaderia` | Consulta |
| `GetMovimiento` | Detalle de un movimiento | `Aduana`, `LugarOperativo`, `IdMovimiento` | `ListaDetalleMov` | Consulta. Clave: `idMovimiento` devuelto por las altas |
| `ConsultarPack` | Composición de un pack | `CodProducto` | `Aduana`, `LugarOperativo`, `NCM`, `CodProducto`, `Origen`, `ListaItems` | Consulta |
| `ConsultarDIFE` | DIFE por venta o traslado sin stock | `idDIFE`, `idMovimiento`, `tipoComprobante`, `nroComprobante`, `codEstado`, `fechaDesde`, `fechaHasta` | `ListaDIFE` | Consulta |
| `Dummy` | Salud | — | `AppServer`, `DbServer`, `AuthServer` (con mayúscula inicial, a diferencia del resto de la DIA) | — |

Ojo con mayúsculas: los parámetros de las altas van en minúscula (`aduana`, `lugarOperativo`) y los de las consultas en mayúscula (`Aduana`, `LugarOperativo`). Los ejemplos del manual escriben `listaMercaderiaIngresada`; el WSDL, `ListaMercaderiaIngresada`.

`Dummy` real (2026-10-02, HTTP 200):

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><DummyResponse xmlns="Ar.Gob.Afip.Dga.wGesTiendasLibres"><DummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></DummyResult></DummyResponse></soap:Body></soap:Envelope>
```

El manual (pp.69-71) describe otro `Dummy` (`Server`, `TimeStamp`, `Resultado` y `Errores/ErrorEjecucion`). Lo real es lo del WSDL.

## Errores

`ListaErrores` puede traer varios `DetalleError`. Éxito: `Codigo` `0`. Venta con stock insuficiente: `0` con el texto "Se registra diferencia por stock en negativo" como descripción adicional (manual p.17).

Códigos de negocio que definen el comportamiento (el manual tiene más de 100; la extracción desalinea algunos). La columna de contexto indica en qué tabla del manual aparece cada código:

| Código | Mensaje | Contexto |
|---|---|---|
| 42034 | Falta el dato obligatorio xxxxx | Todos |
| 41973 | La transaccion xxxxx ya se encuentra en proceso - acceso denegado. | Altas (todas menos `RegistrarJustificacionDIFE` en la extracción) con `transaccion` repetida y todavía en curso |
| 42302 | No hay stock disponible para afectar. | `DestruirMercaderia`, `DevolverMercaderia`, `ArmarPack`, `DesarmarPack`, `RegistrarJustificacionDIFE` (y aparece en la tabla de `VentaMercaderia`, aunque la venta sin stock se acepta) |
| 42303 | Producto inexistente para la combinacion CUIT-Aduana-Lugar Operativo. | Destrucción, devolución, packs, `CambiarCodigoProducto`, `RegistrarJustificacionDIFE` |
| 42323 | No se encuentra stock en Deposito de Tiendas Libres | `CambiarCodigoProducto` |
| 30286 | No hay datos para los criterios ingresados | Las 5 consultas y `RegistrarJustificacionDIFE` |
| 21488 | Venta previa no registrada xxxxx xxxxx | `RegistrarNotaDebitoCredito` |
| 21524 | Ya se devolvio la totalidad de la venta xxxxx | `RegistrarNotaDebitoCredito` |
| 21526 | Venta ya registrada xxxxx xxxxx | `VentaMercaderia` |
| 21494 | Pack xxxxx inexistente | `DesarmarPack` |
| 21496 | Codigo de Producto xxxxx ya registrado | `CambiarCodigoProducto` |
| 21543 | NCM/Cod.producto anterior es igual a NCM/Cod.producto nuevo | `CambiarCodigoProducto` |
| 21550 | Total de cant justificadas xxxxx difiere de cant registrada en la DIFE xxxxx | `RegistrarJustificacionDIFE` |
| 21571 | Mercaderia afectada a un traslado no finalizado | `CambiarCodigoProducto` |
| 21572 | Mercaderia con DIFE activo | `CambiarCodigoProducto` |
| 21345 | Si se informa xxxxx debe informarse xxxxx | `RegistrarNotaDebitoCredito` (`aduanaDestino`/`lugarOperativoDestino`) |
| 10859 | El rango entre fechas supera el maximo de xxxxx dias. | `ConsultarDIFE` |
| 20337 / 20341 | La fecha HASTA debe ser mayor o igual a la fecha DESDE / menor o igual a la del dia | `ConsultarDIFE` |
| 20545 | No se permite utilizar este método del webservice para salidas de operaciones Directo a Plaza | `SalidaParticular` |
| 7026 | Los parametros en la llamada al web method son obligatorios | `ConsultarDIFE` |

Transporte (2026-10-02): `SOAPAction` desconocida → HTTP 500 `soap:Client` `Server did not recognize the value of HTTP Header SOAPAction: Ar.Gob.Afip.Dga.wGesTiendasLibres/NoExiste.` (en inglés); XML roto → HTTP 400 con `Content-Type: text/xml; charset=utf-8` y cuerpo vacío.

## Comportamiento a simular

- **Idempotencia por `transaccion`** (p.8-9): en las 11 altas, repetir el mismo `transaccion` en el mismo método devuelve la respuesta original sin reprocesar, aunque cambien los demás datos. Mientras la primera sigue en curso → 41973. Es el comportamiento más importante para un simulador fiel.
- Stock por (CUIT, aduana, lugar operativo, NCM, código de producto, origen). `IngresarMercaderia` registra pero no suma: la suma ocurre con la autorización aduanera (nacional) o con `SalidaParticular` (extranjera). ArcaSim necesita un disparador para la autorización nacional.
- Stock negativo (versión 0.7 en adelante): `VentaMercaderia` y `TrasladarMercaderia` no fallan por falta de stock; registran y generan una DIFE por artículo.
- Ciclo DIFE (pp.60-61): `REG` → `PRE` (con `RegistrarJustificacionDIFE`) → `ACE` (ajusta stock) o `REC` (pide nueva justificación); `VEN` si vence el plazo sin justificar; `RGL` si se regulariza una vencida; `NOT` si queda en `PRE` 15 días corridos sin tratar. `ACE`, `REC`, `RGL` y `NOT` dependen del personal aduanero o del reloj.
- Pares de estado: cada alta devuelve `idMovimiento` → `GetMovimiento`/`ConsultarMovimientos`; `ArmarPack` → `ConsultarPack`; venta → `RegistrarNotaDebitoCredito` vía comprobante asociado; venta/traslado sin stock → `ConsultarDIFE` → `RegistrarJustificacionDIFE`.
- Reproducir `DescripcionAdicional` fuera de esquema en el 7008 y el `Server`/`TimeStamp` en todas las respuestas.

## No verificado

- WSAA service id.
- Respuestas autenticadas y forma real de `ListaStockMercaderia`, `ListaDetalleMov` y `ListaDIFE`.
- Asignación exacta de varios códigos de error a métodos: la extracción del PDF los desalinea.
- Plazo de vencimiento de una DIFE.
