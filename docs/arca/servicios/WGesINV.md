# WGesINV

"Consulta y Desbloqueo de Despachos". Lo usa un solo cliente: el INV (Instituto Nacional de Vitivinicultura). Consulta las novedades de despachos de exportación de vinos (oficializados, sin diferencias, post-embarques, reversiones, anulaciones, rectificaciones), aprueba o deniega el desbloqueo de cada despacho y aprueba o rechaza los formularios cargados en la VUCEA (manual, "Objetivo y alcance", p.4).

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/WGESINV/wgesinv-ManualParaElDesarrollador.pdf`, revisión del 11/12/2020 (última entrada del historial: `ConsultaIdTransaccionDespacho`). Descargado el 2026-10-02 (HTTP 200). Páginas impresas ("Página N de 45").
- WSDL de homologación: `docs/arca/wsdl/WGesINV-homologacion.wsdl`, bajado el 2026-10-02 de `https://testdia.afip.gov.ar/Dia/Ws/WGesINV/WGesINV.asmx?WSDL`. Esquema inline, sin XSD aparte.
- **La URL de testing del manual no funciona.** El manual (p.5) dice `https://testdia.homo.afip.gob.ar/Dia/Ws/WGesINV/WGesINV.asmx` (el catálogo la marcó [X]). La ruta en el host `testdia.afip.gov.ar`, igual que la de los otros servicios DIA, respondió el WSDL el 2026-10-02.
- WSDL de producción: `docs/arca/wsdl/WGesINV/WGesINV-produccion.wsdl` (`https://servicios3.arca.gob.ar/Dia/Ws/WGesINV/WGesINV.asmx?WSDL`). Idéntico al de homologación salvo `soap:address`.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:15 (-03:00).
- Catálogo: tipo de agente `OTEN`, RG Conjunta 3150/11. README: `https://www.afip.gob.ar/ws/WGESINV/README.txt`. El README da como producción un host viejo, `servicios1.afip.gov.ar`, que el 2026-10-02 respondió **HTTP 503** "Service Unavailable" para todas las rutas DIA probadas: la producción vigente es `servicios3.arca.gob.ar`. Ejemplo oficial en PHP (2010): `https://www.afip.gob.ar/ws/WGESINV/ejemplos/wgesinv-client-php.zip`; usa `TipoAgente` `OTEN`, `Rol` `EXTE`, `argIdTransaccion` = `1006` y trata como error todo `CodErr` distinto de `0` (no de 20304).

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA" (`Recibo` con `CodErr`/`DesError`/`DescAdicErr`) | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/Dia/Ws/WGesINV/WGesINV.asmx` | `soap:address` del WSDL |
| Endpoint producción | `https://servicios3.arca.gob.ar/Dia/Ws/WGesINV/WGesINV.asmx` | WSDL de producción; manual p.5 |
| Namespace | `ar.gov.afip.dia.serviciosweb.WGesINV` (todo en minúscula `serviciosweb`). El manual escribe `serviciosWeb`: hay que seguir al WSDL | WSDL |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `WGesINVSoap` (1.1) y `WGesINVSoap12` (1.2) | WSDL |
| SOAPAction | `ar.gov.afip.dia.serviciosweb.WGesINV/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2. `Dummy` respondió por las dos el 2026-10-02 | Prueba en vivo |
| WSAA service id | `WGesINV` (homologación y producción). El ejemplo PHP oficial trae un TA de homologación de 2010 cuyo token dice `service="wgesinv"`, en minúscula: WSAA no distingue mayúsculas en el nombre o lo normaliza | `https://www.afip.gob.ar/ws/WGESINV/README.txt`; `https://www.afip.gob.ar/ws/WGESINV/ejemplos/wgesinv-client-php.zip` (`TA.xml`) |
| Operaciones | 7 (homologación y producción) | WSDL |

## Autenticación

`argAutentica` (`Autenticacion` extiende `AutenticacionBase`). Orden XML: `Token` (1..1, nillable), `Sign` (1..1, nillable), `Cuit`, `TipoAgente`, `Rol`. El manual (p.15) pide `TipoAgente` = `OTEN` y `UsuRol` = `EXTE` (en el WSDL es `Rol`).

Tabla del manual (p.15-16): 7004 Error Interno; 7005 Token vencido; 7006 Debe ingresar la firma; 7007 Debe ingresar el token; 7008 Token Inválido; 7013 El Servicio no se corresponde con el informado en el Token; 7014 Cuit con el que desea operar no informado; 6005 CUIT,CUIL y/o tipo de agente invalido para el servicio; 6006 Rol invalido para el tipo de agente y el servicio solicitado; 6003 Validación de conexión no coincide con opciones seleccionadas.

Observado con `ConsultaDespachosPendientes` (homologación, 2026-10-02):

| Caso | HTTP | Respuesta |
|---|---|---|
| `Token`=`abc` | 200 | `Recibo`: `7008` / `El Token no se encuentra en formato base 64.` / `(SERVER:xxx.xxx.xxx.103) ` + los 7 arrays vacíos + los 8 contadores en `0` |
| `Token` y `Sign` vacíos | 200 | `7007` / `Debe ingresar el Token y Firma.` + arrays vacíos y contadores en `0` |
| Base64 de `<sso><id/></sso>` | 200 | `7004` / `ID MWE: 73329195`. **Faltan** `Oficializaciones`, `SinDiferencias`, `PostEmbarques`, `Reversiones` y `Anulaciones`; arranca en `<Rectificaciones />` |
| `sso` 2.0 bien formado, `Sign` falso | 200 | `7006` / `Debe ingresar la Firma o la Firma es invalida.` |
| Sin `argAutentica` | 500 | `soap:Fault` `soap:Server`: `El servidor no puede procesar la solicitud. ---&gt; Referencia a objeto no establecida como instancia de un objeto.` |

Cuerpo del caso `abc`:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><ConsultaDespachosPendientesResponse xmlns="ar.gov.afip.dia.serviciosweb.WGesINV"><ConsultaDespachosPendientesResult><Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr></Recibo><Oficializaciones /><SinDiferencias /><PostEmbarques /><Reversiones /><Anulaciones /><Rectificaciones /><RectificacionesPaisDestino /><CantidadOficializaciones>0</CantidadOficializaciones><CantidadSinDiferencias>0</CantidadSinDiferencias><CantidadPostEmbarques>0</CantidadPostEmbarques><CantidadReversiones>0</CantidadReversiones><CantidadAnulaciones>0</CantidadAnulaciones><CantidadRectificaciones>0</CantidadRectificaciones><CantidadRectificacionesPaisDestino>0</CantidadRectificacionesPaisDestino><CantidadTotal>0</CantidadTotal></ConsultaDespachosPendientesResult></ConsultaDespachosPendientesResponse></soap:Body></soap:Envelope>
```

## Operaciones

| Operación | Propósito | Entrada (tras `argAutentica`) | Salida | Estado |
|---|---|---|---|---|
| `ConsultaDespachosPendientes` | Novedades de despachos de vino | `argIdTransaccion` (long, 1..1) | `RtaDespachosPendientes`: `Recibo`, `Oficializaciones`, `SinDiferencias`, `PostEmbarques`, `Reversiones`, `Anulaciones`, `Rectificaciones`, `RectificacionesPaisDestino` y 8 contadores `Cantidad*` | Consulta. Clave de cada despacho: `IdDestinacion` |
| `AprobarDespacho` | Aprueba el desbloqueo | `argAprobarDespacho`: `Aduana`, `IdDestinacion`, `IdAutorizacionINV` (long), `IdUsuarioDesbloqueo` | `RtaAprobarDespacho`: `Recibo` + `NroSecuencia` (long) | Crea. Clave: `Aduana` + `IdDestinacion` |
| `DenegarDespacho` | Deniega el desbloqueo | `argDenegarDespacho`: `Aduana`, `IdDestinacion`, `IdUsuarioDenegacion`, `MotivoDenegacion` | `Recibo` | Crea. Misma clave |
| `ConsultaVUCEAPendientes` | Formularios VUCEA pendientes de aprobar o rechazar | `argIdTransaccion` (long) | `Recibo` + `FormulariosVUCEA` (`CuitRegistro`, `FechaRegistro`, `NroTramite`, `IdTransaccionTramite`, `IdDestinacion`, `Campos`, `ItemsVUCEA`) | Consulta |
| `AsignarEstadoVUCEA` | Aprueba (`A`) o rechaza (`R`) un formulario VUCEA | `argAsignarEstadoVUCEA`: `NroTramite`, `IdTransaccionTramite` (long), `IdDestinacion`, `Estado`, `ErroresCabeceraVUCEA[]`, `ErroresItemsVUCEA[]` | `Recibo` | Crea. Clave: `NroTramite` + `IdTransaccionTramite` |
| `ConsultaIdTransaccionDespacho` | Id de transacción y de VUCE de un despacho | `argIdDespacho` (string) | `RtaIdTransaccionDespacho`: `Recibo`, `IdTransaccion` (string), `IdVuce` | Consulta |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` | — |

Diferencias manual/WSDL que el simulador tiene que resolver a favor del WSDL:

- `ConsultaIdTransaccionDespacho`: el manual (p.39) muestra el parámetro como `argConsultaIdTransaccionDespacho` de tipo `InConsultaIdTransaccionDespacho` con `IdDespacho`; el WSDL tiene `argIdDespacho` string directo. La salida en el manual es `IdVUCE`; en el WSDL, `IdVuce`.
- `ErrorCabeceraVUCEA`/`ErrorItemVUCEA` en el WSDL tienen `IdRubro`, que la tabla de la p.14 no muestra (sí aparece en la p.37).

## Errores

Todo en `Recibo` con HTTP 200. Éxito: **20304 "Procedimiento terminado OK."** en todos los métodos (pp.18, 21, 31, 35, 38, 40).

| Código | Mensaje | Métodos |
|---|---|---|
| 20304 | Procedimiento terminado OK. | Todos |
| 42034 | Falta dato obligatorio {Parámetro} | Todos |
| 10566 | Campo {Parámetro} longitud invalida. | Todos |
| 10121 | No hay datos para los criterios ingresados. | `ConsultaDespachosPendientes`, `ConsultaVUCEAPendientes` |
| 10065 | Ese identificador no corresponde a ninguna declaración | Aprobar, Denegar |
| 10015 | Código de aduana no valido o inexistente | Aprobar, Denegar |
| 20150 | Destinación Inexistente. | Aprobar, Denegar |
| 30330 | Destinación no tiene el motivo de desbloqueo pendiente de desbloquear | Aprobar |
| 30687 | Desbloqueo ya registrado {Parámetro} | Denegar |
| 30688 | Denegacion de desbloqueo ya registrado {Parámetro} | Denegar |
| 30349 | Código $1 $2 inexistente | `AsignarEstadoVUCEA`, `ConsultaIdTransaccionDespacho` |
| 411 | Estado inválido | `AsignarEstadoVUCEA`, `ConsultaIdTransaccionDespacho` |

Tablas de referencia del anexo (pp.44-45): identificadores de campo VUCEA 100-127 (ejercicio de registro, fecha de registro, bodega INV, ...) y rubros 5001-5006 (cabecera, exportador, importador, otros certificados, planilla de terceros, productos exportados). Se usan en `IdCampo`/`IdRubro` de los errores VUCEA.

Transporte: igual que el resto de la DIA en `testdia` (ver `WDiaUtiDES.md`): `SOAPAction` desconocida → HTTP 500 `soap:Client` "El servidor no reconoció el valor del encabezado HTTP SOAPAction: ..."; XML roto → HTTP 400 vacío.

## Comportamiento a simular

- Ciclo del despacho: aparece en `ConsultaDespachosPendientes` → `AprobarDespacho` (devuelve `NroSecuencia`) o `DenegarDespacho`. Un segundo `DenegarDespacho` da 30688; un `DenegarDespacho` sobre uno aprobado da 30687; un `AprobarDespacho` sin bloqueo pendiente da 30330. Después de resolver, el despacho no debería volver a salir como pendiente (inferido del nombre; **NO VERIFICADO**).
- Ciclo VUCEA: `ConsultaVUCEAPendientes` → `AsignarEstadoVUCEA` con `Estado` `A` o `R`. Con `R` los errores de cabecera o ítem son condicionales (p.36-37). `Estado` fuera de `A`/`R` → 411.
- `argIdTransaccion` parece ser un cursor de novedades: el manual no explica su semántica (solo "Identificador de la transacción", N(16), obligatorio). `ConsultaIdTransaccionDespacho` devuelve el `IdTransaccion` de un despacho. Simularlo como cursor incremental es una decisión de ArcaSim, no un dato.
- En errores de autenticación la respuesta conserva arrays vacíos y contadores en 0 (salvo el 7004, que omite los cinco primeros arrays).

## No verificado

- Si WSAA acepta hoy `WGesINV` y `wgesinv` indistintamente (no se pidió un TA).
- Semántica de `argIdTransaccion` y si los pendientes desaparecen al aprobar o denegar.
- Código de éxito real: el manual dice 20304 en todas las tablas y el cliente PHP oficial espera `0`.
- Respuestas autenticadas reales.
- Si `testdia.homo.afip.gob.ar` existió alguna vez.
