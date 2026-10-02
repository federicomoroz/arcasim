# wConsDepFiel

"Consultas Depositario Fiel". Lo usan los PSAD (prestadores de servicios de archivo y digitalización) y los despachantes para consultar los legajos de Depositario Fiel: qué legajos pasaron a estado `ENDO` (entregados) entre dos fechas y en qué estado está un legajo puntual (manual, "Objetivo y alcance", p.4). Es la contraparte de consulta de `wDigDepFiel`, que es donde se informan la recepción y la digitalización.

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/documentos/Manual-Desarrollador-wConsDepFiel.pdf`, versión 1.7 del 14/02/2014 (portada: "Revisión correspondiente al 12 de marzo de 2014"). Descargado el 2026-10-02 (HTTP 200). Páginas impresas ("Página N de 20").
- WSDL de homologación: `docs/arca/wsdl/wConsDepFiel-homologacion.wsdl`, de `https://testdia.afip.gov.ar/Dia/Ws/wConsDepFiel/wConsDepFiel.asmx?WSDL` (2026-10-02). Esquema inline, sin XSD aparte.
- WSDL de producción: `docs/arca/wsdl/wConsDepFiel/wConsDepFiel-produccion.wsdl`. Idéntico salvo `soap:address`.
- **Hay una versión nueva en homologación**: `docs/arca/wsdl/wConsDepFiel/wconsdepfiel-diav2-homologacion.wsdl`, bajada el 2026-10-02 de `https://wsaduhomoext.afip.gob.ar/diav2/wconsdepfiel/wconsdepfiel.asmx?WSDL`. En producción (`webservicesadu.afip.gob.ar/diav2/...`) dio 404. No tiene manual publicado. Ver la sección "Variante diav2".
- Llamadas sin credenciales: 2026-10-02, 15:08-15:19 (-03:00).
- Catálogo: tipos de agente `PSAD` y `DESP`, RG 3069/11. README: `https://www.afip.gob.ar/ws/documentos/README_ConsDepFiel.txt`. El README da como producción un host viejo, `servicios1.afip.gov.ar`, que el 2026-10-02 respondió **HTTP 503** "Service Unavailable" para todas las rutas DIA probadas: la producción vigente es `servicios3.arca.gob.ar`.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA" (`Recibo`) | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/Dia/Ws/wConsDepFiel/wConsDepFiel.asmx` | `soap:address`; manual p.5 |
| Endpoint producción | `https://servicios3.arca.gob.ar/Dia/Ws/wConsDepFiel/wConsDepFiel.asmx` | WSDL de producción; manual p.5 |
| Namespace | `ar.gov.afip.dia.ServiciosWeb.wConsDepFiel` (con **S mayúscula** en `ServiciosWeb`). El manual escribe `serviciosWeb`, y sus ejemplos mezclan tres capitalizaciones | WSDL; manual p.5 |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `wConsDepFielSoap` (1.1) y `wConsDepFielSoap12` (1.2) | WSDL |
| SOAPAction | `ar.gov.afip.dia.ServiciosWeb.wConsDepFiel/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | `wConsDepFiel` (homologación y producción) | `https://www.afip.gob.ar/ws/documentos/README_ConsDepFiel.txt` |
| Operaciones | 3: `PndListaEndo`, `ListaEstado`, `Dummy`. El manual describe además `ListaPedidoLegajo` (pp.16-19), que **no está** en el WSDL de homologación ni en el de producción | WSDL; manual |

## Autenticación

`argAutentica`, tipo `Autenticacion` que extiende `AutenticacionBase`. Orden XML: `Token` (1..1, nillable), `Sign` (1..1, nillable), `Cuit`, `TipoAgente`, `Rol`. El manual (p.7) pide `TipoAgente` `DESP` o `PSAD` y `UsuRol` `EXTE` (en el WSDL es `Rol`). La tabla de errores del manual (p.7) es la estándar de la DIA: 7004, 7005, 7006, 7007, 7008, 7013, 7014, 6005, 6006, 6003.

Este servicio usa **`DescErr`** en el `Recibo` (no `DesError` como `WDiaUtiDES`) y deja `DescAdicErr` vacío.

Observado con `ListaEstado` (homologación, 2026-10-02):

| Caso | HTTP | `Recibo` |
|---|---|---|
| `Token`=`abc`, con `argInListaEstado` | 200 | `<CodErr>7008</CodErr><DescErr>El Token no se encuentra en formato base 64.</DescErr><DescAdicErr />` |
| Sin `argAutentica`, con `argInListaEstado` | 200 | `7007` / `Debe ingresar el Token y Firma.` |
| `Token` y `Sign` vacíos | 200 | `7007` / `Debe ingresar el Token y Firma.` |
| Base64 de `<sso><id/></sso>` | 200 | `7004` / `ID MWE: 73329222` |
| `sso` 2.0 bien formado, `Sign` falso | 200 | `7006` / `Debe ingresar la Firma o la Firma es invalida.` |
| **Sin** `argInListaEstado` (cualquier token) | 200 | `<CodErr>73329198</CodErr><DescErr>ID MWE: 73329198</DescErr><DescAdicErr />`: el número del log va **también en `CodErr`** |
| Cuerpo con namespace `serviciosWeb` (el del manual) y `SOAPAction` correcta | 200 | Igual que el caso anterior: el servidor no reconoce los hijos, los toma como nulos y responde `CodErr` = `ID MWE` |
| `SOAPAction` con `serviciosWeb` | 500 | `soap:Fault` `soap:Client`: `El servidor no reconoció el valor del encabezado HTTP SOAPAction: ar.gov.afip.dia.serviciosWeb.wConsDepFiel/ListaEstado.` |

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><ListaEstadoResponse xmlns="ar.gov.afip.dia.ServiciosWeb.wConsDepFiel"><ListaEstadoResult><Recibo><CodErr>7008</CodErr><DescErr>El Token no se encuentra en formato base 64.</DescErr><DescAdicErr /></Recibo></ListaEstadoResult></ListaEstadoResponse></soap:Body></soap:Envelope>
```

## Operaciones

| Operación | Propósito | Entrada (tras `argAutentica`) | Salida | Estado |
|---|---|---|---|---|
| `PndListaEndo` | Legajos del PSAD que pasaron a `ENDO` entre dos fechas | `argInPndListaEndo`: `CuitDeclarante`, `CodigoCarpeta`, `FechaDesde` (dateTime 1..1), `FechaHasta` (dateTime 1..1) | `Recibo` + `Legajos/Legajo[]` (`CuitDeclarante`, `DescDeclarante`, `CuitIE`, `DescIE`, `NroLegajo`, `Codigo`, `Ticket`, `ImporteLiq`, `FechaOfic`, `FechaEndo`, `Sigea`, `NroReferencia`, `OptoCambioVia`) | Consulta |
| `ListaEstado` | Estado de un legajo | `argInListaEstado`: `CodigoCarpeta`, `NroLegajo`, `Ticket`, `Sigea` | `Recibo` + `LegajoEstado` (`NroLegajo`, `Codigo`, `Estado`, `FechaVtoPSAD`, `FechaVtoDIGI`, `CuitIE`, `DescIE`, `CuitDesp`, `DescDesp`, `Sigea`) | Consulta. Clave: `NroLegajo` + `CodigoCarpeta` (+ `Ticket` o `Sigea` según el código) |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` | — |

Códigos de carpeta (p.9, p.13): `000` carpeta completa, `001` documentación adicional, `002` rectificativa B total, `003` rectificativa B parcial, `004` post-libramiento, `101` manifiesto general de carga de importación. Los estados de legajo están en la tabla de referencia `DFEST_DESC` de `wgesTabRef`.

Reglas de `ListaEstado` (p.13): `Ticket` obligatorio si `CodigoCarpeta` ≠ `000`; `Sigea` prohibido para `000`/`001` y obligatorio para `002`/`003`/`004`; formato de `Sigea`: 31 posiciones `9999999999-999999999999999-9999` o 36 `9999999999-999999999999999-9999/9999`.

## Errores

En `Recibo` con HTTP 200. Éxito: **0 "OK Procesado"**.

| Código | Mensaje | Método |
|---|---|---|
| 0 | OK Procesado | Ambos |
| 2 | Error Atributo/Parametro: "xxxxx" Obligatorio | Ambos |
| 3 | Error Atributo/Parametro: "xxxxx" Formato Incorrecto | Ambos |
| 4 | Error Parametro: "codigo" Valor incorrecto | Ambos |
| 5 | Error Fecha Desde mayor a fecha del dia | `PndListaEndo` |
| 5 | Debe informar Ticket si el codigo de carpeta es "xxxxx" | `ListaEstado` |
| 6 | Error Fecha Hasta mayor a fecha del dia | `PndListaEndo` |
| 7 | Error Fecha Desde mayor a Fecha Hasta. | `PndListaEndo` |
| 101 | No existen legajos en estado ENDO entre las fechas solicitadas | `PndListaEndo` |
| 101 | Legajo inexistente | `ListaEstado` |
| 102 | Usted no es depositario fiel del legajo informado | `ListaEstado` |
| 103 | Ticket no encontrado para sigea | `ListaEstado` |
| 104 | Validaciones de formato del SIGEA (varios textos) | `ListaEstado` |
| 105 | Tipo de código de carpeta no permitido / Problema al obtener Ticket por sigea | `ListaEstado` |

Fuente: manual p.11 y pp.14-15. Los códigos de negocio se repiten entre métodos con distinto significado.

## Comportamiento a simular

- Estado compartido con `wDigDepFiel`: un legajo nace en el SIM (oficialización), pasa a `ENDO` al entregarse la documentación, y el PSAD lo mueve con `AvisoRecepAcept` y `AvisoDigit` (este último lo deja en `DIGI`, según `wDigDepFiel`). `PndListaEndo` lista los que están en `ENDO` en el rango de `FechaEndo`; `ListaEstado` devuelve el estado vigente. El simulador necesita un disparador para crear legajos en `ENDO`.
- El control 102 implica relación PSAD-legajo: `ListaEstado` sobre un legajo de otro PSAD falla.
- Errores de forma: el `CodErr` = número de log cuando falta el parámetro de negocio o el namespace del cuerpo no coincide es real y conviene imitarlo.

## Variante diav2 (solo homologación)

`https://wsaduhomoext.afip.gob.ar/diav2/wconsdepfiel/wconsdepfiel.asmx`, namespace `Ar.Gob.Afip.Dga.wconsdepfiel`, estilo de `wgestiendaslibres` (autenticación `argWSAutenticacionEmpresa` con `Token`, `Sign`, `CuitEmpresaConectada`, `TipoAgente`, `Rol`, todos 0..1; respuestas con `ListaErrores/DetalleError`, `Server`, `TimeStamp`). Siete operaciones: `DepFielEstadosLegajoLista`, `DepFielInhabilitarOficLista`, `DepFielRectificacionLegajosLista`, `DepFielDocumentacionPendienteLista`, `DepFielDevolucionPendienteLista`, `DepFielHistorialLegajoLista`, `Dummy`. Probado el 2026-10-02:

- `Dummy`: HTTP 200, `<DummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></DummyResult>`.
- `DepFielHistorialLegajoLista` con `Token`=`abc`: HTTP 200, `<ListaErrores><DetalleError><Codigo>7008</Codigo><Descripcion>token invalido</Descripcion><DescripcionAdicional>El Token no se encuentra en formato base 64.</DescripcionAdicional></DetalleError></ListaErrores><Server>10.30.32.108</Server><TimeStamp>2026-10-02T15:19:11.689471-03:00</TimeStamp>`.

Es probable que reemplace a `wConsDepFiel` (cubre `ListaPedidoLegajo` con `DepFielDocumentacionPendienteLista`/`DepFielDevolucionPendienteLista`), pero eso es una inferencia: **NO VERIFICADO**.

## No verificado

- WSAA service id de la variante diav2.
- Respuestas autenticadas.
- Si `ListaPedidoLegajo` existió en algún momento en el WSDL.
- Fecha de salida a producción de la variante diav2 y su manual.
