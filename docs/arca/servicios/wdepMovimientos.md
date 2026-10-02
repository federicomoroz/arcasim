# wdepMovimientos

"Ingresos y Salidas para Terminales". Lo usan las terminales portuarias y los depositarios (tipo de agente `DEPO`) para registrar en el SIM los ingresos de mercadería a depósito (de importación o de exportación) o a zona portuaria, las salidas de depósito (salida de zona primaria, por destinación o por medio de transporte) y para consultar rutas y títulos por contenedor (manual, "Objetivo y alcance", p.4). El catálogo lo describe como "Salidas de zona primaria y movimientos de terminales y depositarios".

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/WDEPMOVIMIENTOS/wdepmovimientos-ManualParaElDesarrollador.pdf`, historial desde 2008 hasta la versión 0.13.00 (25/05/2019) con agregados posteriores sin fecha (QR, `CantTotal`, `PesoTotal`, `ConocimientoEmbarque`). Descargado el 2026-10-02 (HTTP 200). Páginas impresas ("Página N de 46").
- WSDL de homologación: `docs/arca/wsdl/wdepMovimientos-homologacion.wsdl`, de `https://testdia.afip.gov.ar/dia/ws/wdepMovimientos/wdepMovimientos.asmx?WSDL` (2026-10-02). Esquema inline: las tres coincidencias de "import" del archivo son texto de `wsdl:documentation`, no imports. No hay XSD aparte.
- WSDL de producción: `docs/arca/wsdl/wdepMovimientos/wdepMovimientos-produccion.wsdl`. Idéntico salvo `soap:address`.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:15 (-03:00).
- Catálogo: tipo de agente `DEPO`, RG 630/94. README: `https://www.afip.gob.ar/ws/WDEPMOVIMIENTOS/README.txt` (el mismo que, por error, enlaza la entrada de `WDiaUtiDES`). El README da como producción un host viejo, `servicios1.afip.gov.ar`, que el 2026-10-02 respondió **HTTP 503** "Service Unavailable" para todas las rutas DIA probadas: la producción vigente es `servicios3.arca.gob.ar`.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA" (`Recibo` con `CodErr`/`DesError`/`DescAdicErr`) | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/dia/ws/wdepMovimientos/wdepMovimientos.asmx` | `soap:address`; manual p.4 |
| Endpoint producción | `https://servicios3.arca.gob.ar/dia/ws/wdepMovimientos/wdepMovimientos.asmx` | WSDL de producción; manual p.4 |
| Namespace | `ar.gov.afip.dia.serviciosweb.wdepMovimientos.wdepMovimientos` (el manual escribe `serviciosWeb`) | WSDL |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `wdepMovimientosSoap` (1.1) y `wdepMovimientosSoap12` (1.2) | WSDL |
| SOAPAction | `ar.gov.afip.dia.serviciosweb.wdepMovimientos.wdepMovimientos/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | `wDepMovimientos` (con D mayúscula, homologación y producción) | `https://www.afip.gob.ar/ws/WDEPMOVIMIENTOS/README.txt` |
| Operaciones | 6 en los dos ambientes. El manual describe 4 + `Dummy`; `WdepListaTitulosPorContenedor` no está en el manual | WSDL; manual pp.4-5 |

## Autenticación

`argAutentica` (`Autenticacion` extiende `AutenticacionBase`). Orden XML: `Token` (1..1, nillable), `Sign` (1..1, nillable), `Cuit`, `TipoAgente`, `Rol`. Valores (p.7): CUIT del depositario, `TipoAgente` `DEPO`, `Rol` `EXTE`.

Tabla del manual (p.7), que acá sí trae los textos reales:

| Código | Mensaje |
|---|---|
| 6005 | CUIT,CUIL y/o tipo de agente invalido para el servicio solicitado |
| 6006 | Rol invalido para el tipo de agente y el servicio solicitado |
| 7005 | Token no vigente o caducado. |
| 7006 | Debe ingresar la Firma o la Firma es invalida. |
| 7007 | Debe ingresar el Token y Firma. / Debe ingresar el Token. |
| 7008 | Token invalido. / El Token no se encuentra en formato base 64. |
| 7013 | El Servicio no se corresponde con el informado en el Token. |
| 7014 | Cuit con el que se desea operar no informado. / El Cuit con el que se desea operar no se encuentra dentro de los posibles habilitados para el token informado. |

Observado con `WdepListarRutas` (con `argwdepListarRutas` informado; homologación, 2026-10-02):

| Caso | HTTP | Respuesta |
|---|---|---|
| `Token`=`abc` | 200 | `<Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr></Recibo><ListaRutas />` |
| `Token` y `Sign` vacíos | 200 | `7007` / `Debe ingresar el Token y Firma.` + `<ListaRutas />` |
| Base64 de `<sso><id/></sso>` | 200 | `7004` / `ID MWE: 73329221` + `<ListaRutas />` |
| `sso` 2.0 bien formado, `Sign` falso | 200 | `7006` / `Debe ingresar la Firma o la Firma es invalida.` |
| Sin `argAutentica`, o sin `argwdepListarRutas` | 500 | `soap:Fault` `soap:Server` "El servidor no puede procesar la solicitud. ---&gt; Referencia a objeto no establecida como instancia de un objeto." |

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><WdepListarRutasResponse xmlns="ar.gov.afip.dia.serviciosweb.wdepMovimientos.wdepMovimientos"><WdepListarRutasResult><Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr></Recibo><ListaRutas /></WdepListarRutasResult></WdepListarRutasResponse></soap:Body></soap:Envelope>
```

## Operaciones

Los parámetros de negocio extienden `argBase` (`Aduana`, `LugarOperativo`, `AduanaDestino`, `LugarOperativoDestino`); los de ingreso y salida extienden además `argNroTransaccion` (`NroTransaccion` long 1..1) y traen una `Carga`.

| Operación | Propósito | Entrada (tras `argAutentica`) | Salida | Estado |
|---|---|---|---|---|
| `WdepIngresos` | Ingreso a depósito de importación, de exportación o a zona portuaria (el tipo se deduce de los datos) | `argwdepIngresos`: base + `NroTransaccion` + `Carga` (`IdDeposito`, `NomMatMedio`, `IdDeclaracion`, `TituloTransporte`, `NroSalida`, `CierreIngreso` S/N, `CantTotal`, `PesoTotal`, `ConocimientoEmbarque`, `LineasMercaderia/IZPLineaMercaderia[]`, `Contenedores/IZPContenedor[]`) | `Recibo` | Crea. Idempotente por `NroTransaccion` |
| `WdepSalidas` | Salida de zona primaria por destinación | `argwdepSalidas` (`argSZPEntrada`): base + `NroTransaccion` + `Carga` (`wdepSZPEntrada`) | `RtaWdepSalidas`: `Recibo` + `NroSalida` | Crea. Idempotente por `NroTransaccion`; devuelve `NroSalida` |
| `WdepSalidasPorMT` | Salida de depósito por medio de transporte | `argwdepSalidas` (`argSZPEntradaMT`): base + `NroTransaccion` + `Carga` (`wdepSZPEntradaMT`) | `RtaWdepSalidas` | Crea. Idempotente |
| `WdepListarRutas` | Rutas entre origen y destino | `argwdepListarRutas`: `Aduana`, `LugarOperativo`, `AduanaDestino`, `LugarOperativoDestino` | `Recibo` + `ListaRutas/WdepHojaRuta[]` | Consulta (catálogo) |
| `WdepListaTitulosPorContenedor` | Declaraciones y títulos de un contenedor | `argwdepListaTitulosPorContenedor`: `Aduana`, `LugarOperativo`, `IdContenedor` | `Recibo` + `Contenedor` (`IdContenedor`, `Declaraciones/Declaracion[]` con `IdDeclaracion` y `Titulos/Titulo[]`) | Consulta |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` | — |

Notas:

- La `documentation` del WSDL de `WdepSalidas` dice "Ingresos a deposito de importacion/exportacion..." (copiada de `WdepIngresos`).
- El manual (p.23 en adelante) muestra en la salida de `WdepSalidas`/`WdepSalidasPorMT` una lista `CodigosQR/CodigoQR/CodQR` (cambio de la versión 0.13). **El WSDL de homologación y el de producción no la tienen**: `RtaWdepSalidas` es solo `Recibo` + `NroSalida`.
- Selección del tipo de ingreso (p.9-10): sin `IdDeclaracion` → zona portuaria; declaración sumaria `MANI`, `ABSD`, `TLAT` o `TLMD` → depósito de importación; detallada de exportación oficializada → depósito de exportación; detallada de exportación con autorización de retiro o salida → zona portuaria; detallada de importación `ECA1`/`ECA2`/`ECA3` → depósito de exportación; cualquier otro caso → error. La tabla de la p.10 fija qué campos son obligatorios, optativos, ignorados o prohibidos en cada caso.

## Errores

En `Recibo`, HTTP 200. Éxito: `0` "Proceso OK". El manual lista más de 150 códigos; los que cambian el flujo:

| Código | Mensaje |
|---|---|
| 0 | Proceso OK |
| 31209 | Aguarde, operacion en curso (no es un error: la transacción sigue procesándose) |
| 22 / 36 | Campo obligatorio / Valor invalido. |
| 10142 | Ya se efectuo el cierre de ingreso. No se puede ingresar otra vez |
| 10942 | Ya se efectuo el Ingreso a Zona Portuaria |
| 10973 | Contenedor ya afectado a una Salida |
| 10459 | El contenedor ya egreso del deposito informado |
| 10558 | Ya no hay mercaderia autorizada a egresar. |
| 10034 / 10035 / 10128 | Cantidad superior a la disponible / a la autorizada / total a egresar mayor que el total autorizado |
| 10074 | Declaracion no autorizada a egresar |
| 10179 | El total de mercaderia ingresada supera lo manifestado |
| 10003 / 20001 | Declaración sumaria / detallada inexistente |
| 10367 | Titulo de transporte inexistente. |
| 10644 | Titulo $1 Bloqueado - Operación prohibida |
| 10802 | El importador tiene EMBARGO pendiente |
| 12456 | La destinacion ya esta siendo tratada |
| 12542 | Existe carga sin Dispositivo PEMA asociado. |
| 6007 / 6008 / 6009 | Aduana o lugar operativo inválidos para el CUIT |
| 10121 | No hay datos para los criterios ingresados |

Transporte: igual que el resto de la DIA en `testdia` (ver `WDiaUtiDES.md`).

## Comportamiento a simular

- **Idempotencia por `NroTransaccion`** (p.8): en `WdepIngresos`, `WdepSalidas` y `WdepSalidasPorMT`, repetir un `NroTransaccion` devuelve la respuesta original aunque cambien los datos. Si la primera todavía se está procesando (la primera llamada pudo haber terminado en timeout), las siguientes responden `31209` "Aguarde, operacion en curso" hasta que termine; después, el código definitivo.
- Estado de stock en depósito por título/declaración: el ingreso suma y la salida resta; las salidas devuelven `NroSalida`, que es el que usan otros servicios (por ejemplo, el prestador PEMA de `WDiaUtiDES` ve la operación pasar a `SALI`).
- Cierre de ingreso (`CierreIngreso` = `S`) bloquea ingresos posteriores para el mismo documento (10142).
- Pares de estado: `WdepIngresos` → `WdepSalidas`/`WdepSalidasPorMT` (no se puede egresar más de lo ingresado o autorizado); contenedores ingresados → `WdepListaTitulosPorContenedor`.

## No verificado

- Si WSAA distingue `wDepMovimientos` de `wdepMovimientos`.
- Respuestas autenticadas y estructura real de `wdepSZPEntrada`/`wdepSZPEntradaMT` en uso (están en el WSDL).
- Si producción devuelve `CodigosQR` aunque no esté en su WSDL.
- Semántica exacta de `WdepListaTitulosPorContenedor` (sin manual).
