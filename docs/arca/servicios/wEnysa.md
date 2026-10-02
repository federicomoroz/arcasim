# wEnysa

ENYSA, "Entrada y Salida de Vehículos". Lo usa la aduana del país vecino (el catálogo dice **Chile**; el manual habla de "País Vecino") con tipo de agente `OTEN`: le envía a ARCA los datos de vehículos que salen de su país hacia Argentina y los eventos de entrada y salida, y consulta los datos de egreso de un vehículo argentino (manual, "Objetivo y alcance", p.4; `catalogo.asp`).

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/wEnysa/wEnysa-ManualDesarrollador.pdf`, "Revisión correspondiente a Junio del 2025", versión 0.1 del 27/08/08. Descargado el 2026-10-02 (HTTP 200). Páginas impresas ("Página N de 25"). Trae el WSDL embebido (p.17 en adelante).
- WSDL de homologación: `docs/arca/wsdl/wEnysa-homologacion.wsdl`, de `https://testdia.afip.gov.ar/DIA/WS/wEnysa/wEnysa.asmx?WSDL` (2026-10-02). Esquema inline, sin XSD aparte.
- WSDL de producción: `docs/arca/wsdl/wEnysa/wEnysa-produccion.wsdl` (`https://servicios3.arca.gob.ar/DIA/WS/wEnysa/wEnysa.asmx?WSDL`). Idéntico salvo `soap:address`.
- README del catálogo: `https://www.afip.gob.ar/ws/wEnysa/README.txt`. Da homologación `https://testdia.afip.gov.ar/DIA/WS/Testing wEnysa/wEnysa.asmx` (con un "Testing " de más) y producción `https://dia.afip.gov.ar/DIA/WS/wEnysa/wEnysa.asmx`, que el 2026-10-02 no aceptó conexión (timeout de 21 s). El manual (p.4) además lista una producción "privada" `https://10.20.152.101/DIA/WS/wEnysa/wEnysa.asmx` (red interna).
- Catálogo: tipo de agente `OTEN`, RG 2623/09.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:30 (-03:00).

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA", variante propia: error en `MsgError` (`codigoError`, `descripcion`, `descripcionAdicional`) y autenticación en camelCase sin CUIT | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/DIA/WS/wEnysa/wEnysa.asmx` | `soap:address`; manual p.4 |
| Endpoint producción | `https://servicios3.arca.gob.ar/DIA/WS/wEnysa/wEnysa.asmx` | WSDL de producción; manual p.4 |
| Namespace | `Ar.Gov.Afip.Dia.ServiciosWeb.ServiciosEnysa`. El manual da tres distintos: `ar.gov.afip.dia.serviciosWeb.wEnysa` (tabla, p.4), `http://ar.gov.afip.enysa/` (ejemplos) y `ar.gov.afip.dia.serviciosWeb.ServiciosEnysa` (WSDL embebido). Ninguno coincide en mayúsculas con el real | WSDL; manual |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `wEnysaSoap` (1.1) y `wEnysaSoap12` (1.2) | WSDL |
| SOAPAction | `Ar.Gov.Afip.Dia.ServiciosWeb.ServiciosEnysa/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | `wEnysa` (homologación y producción) | README del catálogo |
| Operaciones | 6: `CargaDatosVehiculo`, `CargaEventoEntradaSalida`, `NotificaSalidaObservada`, `ConsultaDatosVehiculo`, `GetVersion`, `Dummy`. El manual no documenta `NotificaSalidaObservada` | WSDL |

## Autenticación

Parámetro `autenticacion` (minúscula), tipo `Autenticacion` plano, en este orden:

```xml
<autenticacion>
  <tipoAgente>OTEN</tipoAgente>   <!-- 0..1 -->
  <usuRol>EXTE</usuRol>           <!-- 0..1 -->
  <usuAduana>...</usuAduana>      <!-- 0..1, aduana de conexión C(3) -->
  <lugOper>...</lugOper>          <!-- 0..1, lugar operativo C(5) -->
  <token>...</token>              <!-- 1..1, nillable -->
  <firma>...</firma>              <!-- 1..1, nillable -->
</autenticacion>
```

No hay CUIT: la entidad sale del token. El manual (p.8-9) pide `tipoAgente` `OTEN` y `usuRol` `EXTE`, y que `codigoPais` de los datos coincida con el del ticket.

Errores de autenticación del manual (p.7), en `codigoError`:

| Código | Mensaje |
|---|---|
| 500 | No autorizado para utilizar este servicio |
| 501 | Token no vigente o caducado |
| 502 | Debe ingresar la firma |
| 503 | Debe ingresar el token |
| 504 | Token Inválido |
| 505 | Aduana, Rol, Tipo de Agente o Lugar Operativo inválido |

Observado (homologación, 2026-10-02):

| Caso | HTTP | Respuesta |
|---|---|---|
| `CargaEventoEntradaSalida` o `NotificaSalidaObservada` con `token` y `firma` vacíos | 200 | `<codigoError>503</codigoError><descripcion>Debe ingresar el Token y Firma.</descripcion><descripcionAdicional />` |
| `CargaDatosVehiculo`, `CargaEventoEntradaSalida` o `NotificaSalidaObservada` con `token`=`abc` | 200 | `<codigoError>1</codigoError><descripcion>Error Interno</descripcion><descripcionAdicional>ID: 73329227</descripcionAdicional>` (no 504) |
| `ConsultaDatosVehiculo` con `token` `abc`, vacío o Base64 basura, con o sin los cuatro parámetros | 500 | `soap:Fault` `soap:Server` "El servidor no puede procesar la solicitud. ---&gt; Referencia a objeto no establecida como instancia de un objeto." |
| Cualquier método sin `autenticacion` | 500 | El mismo fault |
| `GetVersion` (sin autenticación) | 200 | `<GetVersionResult>1.0</GetVersionResult>` |

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><CargaEventoEntradaSalidaResponse xmlns="Ar.Gov.Afip.Dia.ServiciosWeb.ServiciosEnysa"><CargaEventoEntradaSalidaResult><codigoError>503</codigoError><descripcion>Debe ingresar el Token y Firma.</descripcion><descripcionAdicional /></CargaEventoEntradaSalidaResult></CargaEventoEntradaSalidaResponse></soap:Body></soap:Envelope>
```

Un token que no es Base64 termina en "Error Interno" (código 1) con el número de log (`ID: n`, del mismo contador `ID MWE` que el resto de la DIA). `ConsultaDatosVehiculo` falla siempre con el fault de referencia nula en homologación, incluso antes de validar el token.

## Operaciones

| Operación | Propósito | Entrada (tras `autenticacion`) | Salida | Estado |
|---|---|---|---|---|
| `CargaDatosVehiculo` | El país vecino envía los datos de un vehículo que sale hacia Argentina | `datosVehiculo` (`DatosVehiculo`: `anioFabricacion`, `color`, `aduanaFormulario`, `anioFormulario`, apellidos, `arrastreSiNo`, `codigoAduanaMovimiento`, `codigoPais`, `codigoPaisVehiculo`, `codigoResguardo`, `direccion`, fechas, marca y modelo, `nacionalidad`, `nombres`, `numeroChasis`, `numeroDocumento`, `numeroFormulario`, `numeroMotor`, `observaciones`, `pasajeros` (int 1..1), `patente`, `propietario`, tipos de documento, `tipoVehiculo`, `datosAlquiler`) | `MsgError` | Crea. Clave: `aduanaFormulario` + `anioFormulario` + `numeroFormulario` (+ `tipoTransaccion`) |
| `CargaEventoEntradaSalida` | Evento `ED` (entrada al país destino), `SD` (salida del país destino) o `EO` (entrada al país origen) | `eventoEntradaSalida` (`aduanaEvento`, `aduanaFormulario`, `anioFormulario`, `codigoAduanaMovimiento`, `codigoPais`, `codigoResguardo`, fechas, `numeroDocumento`, `numeroFormulario`, `pasajeros`, `patente`, `tipoTransaccion`) | `MsgError` | Crea. Misma clave |
| `NotificaSalidaObservada` | Salida observada de un vehículo (sin manual) | `salidaObservada` (`codigoAduanaMovimiento`, `codigoResguardo`, `fechaMovimiento`, `codigoPaisVehiculo`, `patente`) | `MsgError` | Crea |
| `ConsultaDatosVehiculo` | Datos de un vehículo que sale de Argentina hacia el país vecino | `anioFormulario`, `tipoTransaccion` (debe ser `SO`), `aduanaFormulario`, `numeroFormulario`, sueltos | `DatosVehiculoResponse`: `aduanaFormulario`, `anioFormulario`, `datosVehiculo`, `msgError`, `numeroFormulario`, `tipoTransaccion` | Consulta |
| `GetVersion` | Versión del servicio | — | `string` (`1.0`) | — |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` | — |

Validaciones del manual (pp.10, 14-16): `anioFormulario` ≤ año actual; `numeroFormulario` > 0; `pasajeros` > 0; `propietario` y `arrastreSiNo` `S`/`N`; `nacionalidad` en la ISO de países; `codigoAduanaMovimiento`, `codigoResguardo` y `tipoVehiculo` contra tablas del país de origen; `fechaVencimiento` ≥ hoy en los eventos. Las fechas viajan como texto "yyyy/mm/dd hh:mi:ss tz" aunque el manual diga "números".

## Errores

`MsgError` con HTTP 200. Errores de negocio del manual (p.7):

| Código | Mensaje |
|---|---|
| 0 | Operación correcta |
| 1 | Error interno |
| 2 | Faltan datos |
| 3 | Datos inválidos |
| 4 | Transacción / Evento ya ingresado |
| 5 | Transacción inexistente |
| 6 | Operación inválida |
| 7 | País inexistente |
| 8 | Aduana inexistente |
| 9 | Resguardo inexistente |

Transporte: igual que el resto de la DIA en `testdia` (ver `WDiaUtiDES.md`).

## Comportamiento a simular

- Ciclo del vehículo por formulario: `CargaDatosVehiculo` crea el formulario; `CargaEventoEntradaSalida` le agrega eventos `ED`/`SD`/`EO`; repetir un evento → 4 "Transacción / Evento ya ingresado"; evento sobre formulario inexistente → 5.
- Dirección inversa: `ConsultaDatosVehiculo` (`tipoTransaccion` `SO`) lee vehículos argentinos que salieron; ArcaSim necesita sembrarlos.
- `codigoPais` tiene que coincidir con el país del token: el simulador debería atar el token emitido a un país.
- Reproducir que en homologación `ConsultaDatosVehiculo` responde con fault de referencia nula es opcional (modo "homologación fiel").

## No verificado

- Respuestas autenticadas y el error real de autenticación con token inválido (el manual dice 504; homologación devuelve 1).
- Semántica de `NotificaSalidaObservada`.
- Si producción sigue usando este servicio con Chile.
