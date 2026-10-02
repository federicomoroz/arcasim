# wgesprecintosdepfis

"Candado electrónico de depósitos fiscales" (WSCES, Coraza Electrónica de Seguridad, en el catálogo). Lo usan los prestadores de precintos electrónicos CEMA que se colocan en las puertas de los depósitos fiscales: consultan qué precintos hay que activar o desactivar, informan el inicio y el fin del monitoreo, reportan periódicamente el estado y las alarmas, y mantienen el padrón de sus precintos (manual, "Objetivo y alcance", p.4). El catálogo marca la RG 3871/16 que lo respalda como "Derogada"; el servicio sigue respondiendo en los dos ambientes.

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/WSCES/ManualDesa-wgesprecintosdepfis.pdf`, revisión del 20/11/2015 (última entrada del historial: 02/11/16, "Revisión del documento"). Descargado el 2026-10-02 (HTTP 200). Páginas impresas ("Página N de 23").
- WSDL de homologación: `docs/arca/wsdl/wgesprecintosdepfis-homologacion.wsdl`, de `https://testdia.afip.gob.ar/Dia/Ws/wgesprecintosdepfis/wgesprecintosdepfis.asmx?WSDL` (2026-10-02). Esquema inline, sin XSD aparte.
- WSDL de producción: `docs/arca/wsdl/wgesprecintosdepfis/wgesprecintosdepfis-produccion.wsdl`. Idéntico salvo `soap:address`.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:15 (-03:00).
- Catálogo: tipo de agente `ISTA`, RG 3871/16 (marcada "Derogada"). README: `https://www.afip.gob.ar/ws/WSCES/README.txt`. El README da como producción un host viejo, `servicios1.afip.gov.ar`, que el 2026-10-02 respondió **HTTP 503** "Service Unavailable" para todas las rutas DIA probadas: la producción vigente es `servicios3.arca.gob.ar`.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA" (`Recibo` con `CodErr`/`DesError`/`DescAdicErr`) | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gob.ar/Dia/Ws/wgesprecintosdepfis/wgesprecintosdepfis.asmx` (`.gob.ar`; el host `testdia.afip.gov.ar` también sirve los servicios DIA) | `soap:address`; manual p.5 |
| Endpoint producción | `https://servicios3.arca.gob.ar/Dia/Ws/wgesprecintosdepfis/wgesprecintosdepfis.asmx` | WSDL de producción; manual p.5 |
| Namespace | `ar.gov.afip.dia.serviciosweb.wgesprecintosdepfis` (el manual dice `serviciosWeb`) | WSDL |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `wgesprecintosdepfisSoap` (1.1) y `wgesprecintosdepfisSoap12` (1.2) | WSDL |
| SOAPAction | `ar.gov.afip.dia.serviciosweb.wgesprecintosdepfis/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | `wgesprecintosdepfis` (homologación y producción) | `https://www.afip.gob.ar/ws/WSCES/README.txt` |
| Operaciones | 8 en los dos ambientes | WSDL |

## Autenticación

`argAutentica` (`Autenticacion` extiende `AutenticacionBase`). Orden XML: `Token` (1..1, nillable), `Sign` (1..1, nillable), `Cuit`, `TipoAgente`, `Rol`. El manual (p.7) pide `TipoAgente` = `ISTA` ("debe estar registrado como tal") y `UsuRol` = `EXTE` (`Rol` en el WSDL).

Tabla del manual (p.7; la extracción desalinea códigos y textos, este es el orden coherente con `wdepMovimientos`): 6005 CUIT,CUIL y/o tipo de agente invalido para el servicio solicitado; 6006 Rol invalido...; 7005 Token no vigente o caducado.; 7006 Debe ingresar la Firma o la Firma es invalida.; 7007 Debe ingresar el Token y Firma. / Debe ingresar el Token.; 7008 Token invalido. / El Token no se encuentra en formato base 64.; 7013 El Servicio no se corresponde con el informado en el Token.; 7014 Cuit con el que se desea operar no informado. / ...no se encuentra dentro de los posibles habilitados para el token informado.

Observado con `ConsultarPrecintosPendientes` (homologación, 2026-10-02). Ojo: en este `Result` el `Recibo` va **después** del array:

| Caso | HTTP | Respuesta |
|---|---|---|
| `Token`=`abc` | 200 | `<PrecintosPendientes /><Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr></Recibo>` |
| `Token` y `Sign` vacíos | 200 | `<PrecintosPendientes />` + `7007` / `Debe ingresar el Token y Firma.` |
| Base64 de `<sso><id/></sso>` | 200 | `<PrecintosPendientes />` + `7004` / `ID MWE: 73329202` |
| `sso` 2.0 bien formado, `Sign` falso | 200 | `<PrecintosPendientes />` + `7006` / `Debe ingresar la Firma o la Firma es invalida.` |
| Sin `argAutentica` | 500 | `soap:Fault` `soap:Server` "El servidor no puede procesar la solicitud. ---&gt; Referencia a objeto no establecida como instancia de un objeto." |

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><ConsultarPrecintosPendientesResponse xmlns="ar.gov.afip.dia.serviciosweb.wgesprecintosdepfis"><ConsultarPrecintosPendientesResult><PrecintosPendientes /><Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr></Recibo></ConsultarPrecintosPendientesResult></ConsultarPrecintosPendientesResponse></soap:Body></soap:Envelope>
```

El manual muestra `DescErr` en el `Recibo`; el WSDL y el servidor usan `DesError`.

## Operaciones

| Operación | Propósito | Entrada (tras `argAutentica`) | Salida | Estado |
|---|---|---|---|---|
| `ConsultarPrecintosPendientes` | Precintos del prestador en `SOAC` o `SODE` (sondeo, inicialmente cada 5 minutos) | — (solo `argAutentica`) | `PrecintosPendientes/PrecintoPendiente[]` (`IdPrecinto`, `Estado`, `FechaEstado`) + `Recibo` | Consulta |
| `IniciarMonitoreo` | Informa que activó los precintos (`SOAC` → `ACTI`) | `argIniciarMonitoreo/IdPrecinto/string[]` (1 a 250) | `Recibo` | Cambia estado. Clave: `IdPrecinto` |
| `TerminarMonitoreo` | Informa que los desactivó (`SODE` → `DESA`) | `argTerminarMonitoreo/IdPrecinto/string[]` (1 a 250) | `Recibo` | Cambia estado |
| `InformarEstadoPrecintos` | Estado y alarmas de los precintos en `ACTI` (inicialmente cada 15 minutos) | `argInformarEstadoPrecintos/EventoPrecintos/EventoPrecinto[]`: `IdPrecinto`, `CodAlarma` (varios con `+`, ej. `BTBJ+ABIE`), `FechaEvento` | `Recibo` | Crea eventos |
| `ConsultarPrecintos` | Datos de los precintos del prestador | `argConsultaPrecintos`: `IdPrecinto` y/o `Estado` (al menos uno) | `Precintos/Precinto[]` (`IdPrecinto`, `Estado`, `CodAlarma`, `FUltEstado`, `FUltEvento`) + `Recibo` | Consulta |
| `NovedadPrecinto` | Alta, actualización o baja en el padrón | `argPrecinto`: `IdPrecinto`, `Aduana`, `LugarOperativo` | `Recibo` | Crea/cambia el padrón |
| `ConsultaCemaPadron` | Padrón de precintos del prestador | `argConsulta`: `IdPrecinto`, `Aduana`, `LugarOperativo`, `EstadoPrecinto`, `EstadoAcepDepo` | `Recibo` + `Dispositivos/Dispositivo[]` (`IdPrecinto`, `Aduana`, `LugarOperativo`, `EstadoPrecinto`, `FechaEstado`, `EstadoAcepDepo`) | Consulta |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` | — |

`NovedadPrecinto` no tiene un campo de tipo de novedad. El manual (p.17) define: ALTA (obligatorios `IdPrecinto`, `Aduana`, `LugarOperativo`), ACTUALIZACIÓN (mismos obligatorios, cambia aduana o lugar operativo) y BAJA (solo `IdPrecinto`; `Aduana` y `LugarOperativo` prohibidos). Que el servidor distinga alta de actualización según si el precinto ya existe es una **inferencia**, no está escrito.

Estados del padrón (p.19): `HABI` habilitado, `SUS` suspendido, `BAJA`. Aceptación del depositario: `PEND`, `ACEP`, `RECH`. Estados de monitoreo y alarmas en las tablas `ESTCEL_DESC` y `ESTMON_DESC` de `wgesTabRef`; el manual nombra `MONI` (monitoreo normal) y `ABIE` (precinto abierto) (p.13).

## Errores

En `Recibo`, HTTP 200. Éxito: `0` "OK".

| Código | Mensaje | Métodos |
|---|---|---|
| 0 | OK | Todos |
| 10121 | No hay datos para los criterios ingresados | `ConsultarPrecintosPendientes`, `ConsultarPrecintos` |
| 10566 | Campo xxxxx longitud invalida | Varios |
| 12404 | Dispositivo INEXISTENTE | Iniciar, Terminar, Informar |
| 12591 | CEMA NO HABILITADO para su uso | Iniciar, Terminar, Informar |
| 12592 | CEMA tipo XXXX incorrecto para esta operacion | Iniciar, Terminar, Informar, Novedad |
| 30839 | ERROR - Dispositivo informado mas de una vez | Iniciar, Terminar, Informar |
| 30840 | El dispositivo no se encuentra en estado xxxx | Iniciar (debe estar en `SOAC`), Terminar, Informar (debe estar en `ACTI`) |
| 30841 | Codigo de alarma xxxx inexistente | Informar |
| 30842 | Debe informarse precinto y/o estado | `ConsultarPrecintos` |
| 30843 | No es un dispositivo para puerta de deposito | Iniciar, Terminar, Informar |
| 31361 / 31362 | El array xxxxx no debe tener menos / mas de xxxxx datos (1 y 250) | Iniciar, Terminar, Informar |
| 42034 | Falta el dato obligatorio xxxxx | Varios |
| 10782 | Lugar Operativo INEXISTENTE o Fuera de Vigencia | Novedad |
| 30846 | xxxxx ya fue dada/o de alta | Novedad |
| 30850 | Puerta Deposito con dispositivo xxxxx asignado en estado xxxxx | Novedad (baja o cambio de un precinto no desactivado) |
| 31167 | Operación Prohibida xxx | Novedad |
| 42075 | Campo xxxxx, longitud invalida. xxxxx | Novedad, `ConsultaCemaPadron` |
| 70222 | Aduana INEXISTENTE o fuera de Vigencia | Novedad |

Fuente: pp.10-20. Las tablas del PDF desalinean código y mensaje; el emparejamiento de arriba sale de cruzar las tablas de Iniciar, Terminar e Informar. Si hay un error en un ítem del array, el precinto se informa en `DescAdicErr` (p.10, p.12).

Transporte: igual que el resto de la DIA en `testdia` (ver `WDiaUtiDES.md`).

## Comportamiento a simular

- Máquina de estados por precinto (p.4): `CIDE` (colocado en puerta cerrada, lo hace el depósito) → `SOAC` (el guarda pide activar) → `ACTI` (`IniciarMonitoreo`) → eventos periódicos (`InformarEstadoPrecintos`) → `SODE` (el guarda pide desactivar) → `DESA` (`TerminarMonitoreo`). Las transiciones `CIDE`, `SOAC` y `SODE` son de terceros: ArcaSim necesita un disparador externo para ellas.
- Pares de estado: `NovedadPrecinto` → `ConsultaCemaPadron`; `SOAC`/`SODE` → `ConsultarPrecintosPendientes`; `IniciarMonitoreo`/`TerminarMonitoreo`/`InformarEstadoPrecintos` → `ConsultarPrecintos` (`Estado`, `CodAlarma`, `FUltEstado`, `FUltEvento`).
- Validación por array: un ítem inválido rechaza el lote (inferido de "el precinto que identifica el ítem del array con el error se retorna como información adicional"); **NO VERIFICADO** si se procesan los demás.
- Baja o actualización solo con el precinto desactivado y no aceptado por el depósito (p.17): si no, 30850.

## No verificado

- Respuestas autenticadas, procesamiento parcial de arrays y cómo se diferencia ALTA de ACTUALIZACIÓN.
- Vigencia real del servicio frente a la RG derogada.
