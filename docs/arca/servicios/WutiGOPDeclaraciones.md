# WutiGOPDeclaraciones

Declaraciones de Grandes Operadores (GOP), "nuevo esquema". Lo usan los depositarios (tipo de agente `DEPO` según el manual) para bajar las destinaciones detalladas oficializadas que les corresponden y sus datos: carátula, ítems, liquidación, cancelaciones, estados y bloqueos. Es un servicio de **consulta pura**: no tiene métodos que escriban. El manual dice que el "nuevo esquema" solo adapta el servicio viejo a los estándares actuales "sin modificar las reglas del negocio" (manual, "Objetivo y alcance", p.5).

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/USAduaneros/documentos/WutiGOPDeclaraciones_NuevoEsquema_Manual-Usuario_.pdf`, "Revisión correspondiente a Junio del 2025", 60 páginas, historial 1.0 (14/02/12) y 1.0.1 (11/03/2017). Descargado el 2026-10-02 (HTTP 200). Páginas impresas ("Página N de 60").
- WSDL de homologación: `docs/arca/wsdl/WutiGOPDeclaraciones-homologacion.wsdl`, de `https://testdia.afip.gov.ar/Dia/Ws/WutiGOPDeclaraciones/WutiGOPDeclaraciones.asmx?WSDL` (2026-10-02). Esquema inline, sin XSD aparte.
- **El catálogo decía "Sin URL completa en el manual".** El manual (p.6) trae `https:/testdia.afip.gov.arr/Dia/Ws/wutiGopDeclaraciones/wutiGopDeclaraciones.asmx` (con `https:/` y `.arr`, dos errores de tipeo) y `https://servicios3.arca.gob.ar /Dia/Ws/wutiGopDeclaraciones/wutiGopDeclaraciones.asmx` (con un espacio). Corregidas, las dos rutas responden.
- WSDL de producción: `docs/arca/wsdl/WutiGOPDeclaraciones/WutiGOPDeclaraciones-produccion.wsdl` (`https://servicios3.arca.gob.ar/Dia/Ws/WutiGOPDeclaraciones/WutiGOPDeclaraciones.asmx?WSDL`). Idéntico salvo `soap:address`.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:25 (-03:00).
- Catálogo: "Grandes Operadores / Domiciliarias – Salidas GOP", sin tipo de agente, README ni resolución.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA" (`Recibo` con `CodErr`/`DesError`/`DescAdicErr`) | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/Dia/Ws/WutiGOPDeclaraciones/WutiGOPDeclaraciones.asmx` | `soap:address` |
| Endpoint producción | `https://servicios3.arca.gob.ar/Dia/Ws/WutiGOPDeclaraciones/WutiGOPDeclaraciones.asmx` | WSDL de producción |
| Namespace | `ar.gov.afip.dia.serviciosweb.WutiGOPDeclaraciones`. **Todos los ejemplos del manual usan `http://tempuri.org/`**, que el servicio no reconoce; la tabla de especificaciones dice `ar.gov.afip.dia.serviciosWeb.WutiGOPDeclaraciones` | WSDL; manual p.6 y pp.9-57 |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `WutiGOPDeclaracionesSoap` (1.1) y `WutiGOPDeclaracionesSoap12` (1.2) | WSDL |
| SOAPAction | `ar.gov.afip.dia.serviciosweb.WutiGOPDeclaraciones/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | **NO VERIFICADO**. Ni el manual ni el catálogo (que no tiene README para esta entrada) lo dicen | — |
| Operaciones | 10 en los dos ambientes | WSDL |

## Autenticación

`argAutentica` (`Autenticacion` extiende `AutenticacionBase`). Orden XML del WSDL: `Token` (1..1, nillable), `Sign` (1..1, nillable), `Cuit`, `TipoAgente`, `Rol`. Los ejemplos del manual ponen `Cuit`, `TipoAgente`, `UsuRol`, `Token`, `Sign` (otro orden y otro nombre): hay que seguir al WSDL. Valores del manual (p.9-10): `TipoAgente` `DEPO`, `UsuRol` `EXTE`.

Controles comunes a todos los métodos (p.5): que la aduana y el lugar operativo informados pertenezcan al usuario de conexión y que el usuario esté habilitado.

Tabla del manual (pp.58-59): la estándar de la DIA (7004, 7005, 7006, 7007, 7008, 7013, 7014, 6005, 6006, 6003).

Observado (homologación, 2026-10-02):

| Caso | HTTP | Respuesta |
|---|---|---|
| `PndListaGOPDetallada`, `PndListaGOPEstados` o `ListaGOPEstados` sin `IdDecla`, con `Token`=`abc` | 200 | `<Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:10.30.11.75) </DescAdicErr></Recibo>` |
| `ListaGOPCaratDeta` con `Token` vacío e `IdDecla`=`26001EC01000001A` | 200 | `7004` / `ID MWE: 73329322` / `(SERVER:10.30.11.75) ` |
| `ListaGOPEstados` o `ListaGOPBloqueos` con `IdDecla`=`26001EC01000001A` y cualquier token (`abc`, vacío, Base64 basura, `sso` falso) | 500 | `soap:Fault` `soap:Server`: `El servidor no puede procesar la solicitud. ---&gt; Execute non query del Command: ORA-21000: error number argument to raise_application_error of -9993 is out of range ORA-06512: at "SFX.XML_ENGINE", line 305 ORA-06512: at line 1  ---&gt; ORA-21000: ...` (con saltos de línea reales) |
| Cualquier método sin `argAutentica` o sin el parámetro de negocio | 500 | `soap:Fault` `soap:Server` "... Referencia a objeto no establecida como instancia de un objeto." |

Lecturas:

- Con `IdDecla` informado, la base valida la declaración **antes** que el token, y en homologación esa validación revienta con un error Oracle (`raise_application_error` con un número fuera de rango). Es un bug del ambiente, pero es lo que un cliente real recibe.
- A diferencia de los otros servicios en `testdia`, acá `DescAdicErr` muestra la IP interna real (`10.30.11.75`), no `xxx.xxx.xxx.103`.

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><PndListaGOPDetalladaResponse xmlns="ar.gov.afip.dia.serviciosweb.WutiGOPDeclaraciones"><PndListaGOPDetalladaResult><Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:10.30.11.75) </DescAdicErr></Recibo></PndListaGOPDetalladaResult></PndListaGOPDetalladaResponse></soap:Body></soap:Envelope>
```

## Operaciones

Todas son consultas. Todos los parámetros de negocio extienden `ReqBurLot` (`Aduana`, `LugarOperativo`). La clave es `IdDecla` (16 caracteres).

| Operación | Propósito | Entrada (tras `argAutentica`) | Salida (además de `Recibo`) |
|---|---|---|---|
| `PndListaGOPDetallada` | Destinaciones detalladas oficializadas pendientes de transmitir | `argPndListaGOPDetallada`: `Aduana`, `LugarOperativo` | `Pendientes/Pendiente[]` (`IdDecla`) |
| `ListaGOPCaratDeta` | Carátula de una destinación | `argListaGOPCaratDeta`: + `IdDecla` | `Declaracion` (`IdDecla`, `CodAduReg`, `CodTipDecla`, `CodImpoExpo`, `NomImpoExpo`, `CuitImpoExpo`, `CuitDesp`, ... unos 50 campos, embarques y bultos) |
| `ListaGOPItemsDeta` | Ítems, paginados por lote | `argListaGOPItemsDeta`: + `NroLote` (decimal 1..1), `IdDecla` | `Items` (`IdDecla`, `NroLote`, `IndUltLote`, `Items/Item[]`) |
| `ListaGOPLiquiDeta` | Liquidación general | `argListaGOPLiquiDeta`: + `IdDecla` | `Liquidaciones` (`IdDecla`, `Liquidacion[]` con `NroLiq`, `MontoPagar`, ...) |
| `ListaGOPCancelaA` | Destinaciones a las que cancela un ítem | `argListaGOPCancelaA`: + `IdDecla`, `NroItem` (int) | `CancelacionesA` |
| `ListaGOPItemsCancelados` | Destinaciones que cancelaron un ítem | `argListaGOPItemsCancelados`: + `IdDecla`, `NroItem` | `CancelacionesPor` |
| `PndListaGOPEstados` | Destinaciones con cambios de estado | `argPndListaGOPEstados`: `Aduana`, `LugarOperativo` | `Pendientes/Pendiente[]` (`IdDecla`) |
| `ListaGOPEstados` | Fechas de cada estado de una destinación | `argListaGOPEstados`: + `IdDecla` | `EstadosDeclaracion` (`IdDecla`, `CuitImpoExpo`, `CodEstDecla`, `CodCanal`, `FechOfic`, `FechPresen`, `FechObtAutoriza`, ...) |
| `ListaGOPBloqueos` | Bloqueos de una destinación | `argListaBloqueos`: + `IdDecla` | `Bloqueos` (`Destinacion`, `ListaBloqueos/Bloqueo[]`: `CuitImportador`, `CuitDespachante`, `CodigoBloqueo`, fechas de bloqueo, desbloqueo y denegación, causa) |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` |

El manual llama `ListaBloqueos` al método que el WSDL publica como `ListaGOPBloqueos` (p.8 y p.55), y nombra las clases de salida `Rpta...` donde el WSDL usa `Rta...`.

## Errores

En `Recibo`, HTTP 200. El manual escribe `DescErr`; el servidor usa `DesError`.

| Código | Mensaje |
|---|---|
| 0 | OK Procesado |
| 30286 | No hay datos para los criterios ingresados |

La tabla de negocio del manual (p.59) habla de `IdMicDta` (códigos 42034, 10566, 10708, 10743, 10744, 10728): está **copiada de un servicio de MIC-DTA** y no aplica a estos métodos. Lo único propio es el 30286, agregado en la versión 1.0.1. Los controles de `Aduana`/`LugarOperativo` probablemente usen 6007/6008 como el resto de la DIA: **NO VERIFICADO**.

## Comportamiento a simular

- Sin escrituras. El estado lo genera el resto del SIM: ArcaSim necesita sembrar declaraciones (`IdDecla`) con carátula, ítems, liquidaciones, estados y bloqueos.
- Dos colas: `PndListaGOPDetallada` (nuevas oficializadas) y `PndListaGOPEstados` (cambios de estado). El manual no explica si una destinación sale de la cola al consultarla: **NO VERIFICADO**. Simularlo como cola que se vacía al leer sería una decisión de diseño.
- Paginación de ítems por `NroLote` con `IndUltLote` como fin.
- Reproducir el fault Oracle de homologación es opcional; conviene tenerlo como modo "homologación fiel".

## No verificado

- WSAA service id.
- Respuestas autenticadas, códigos de error reales del negocio y semántica de las colas `Pnd*`.
- Si el fault ORA-21000 se da también en producción.
