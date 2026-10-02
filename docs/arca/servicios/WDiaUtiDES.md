# WDiaUtiDES

"Actualización y Consulta Prestador PEMA". Lo usan los prestadores de dispositivos PEMA (precintos electrónicos de monitoreo satelital que viajan con el contenedor o la carga suelta) para informarle al SIM aduanero el estado de cada dispositivo, consultar los medios transportadores y destinaciones asociados, consultar datos de un ATA (Agente de Transporte Aduanero) y mantener el padrón de sus dispositivos (manual, "Objetivo y alcance", PDF p.5).

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/WDIAUTIDES/ManualDesarrollador-WdiaUtiDEs.pdf`, versión 1.5.2 del 11/12/2019. Descargado el 2026-10-02 (HTTP 200, `application/pdf`). Las páginas citadas son las impresas ("Página N de 38").
- WSDL de homologación: `docs/arca/wsdl/WDiaUtiDES-homologacion.wsdl` (bajado el 2026-10-02 de `https://testdia.afip.gov.ar/dia/ws/WDiaUtiDES/WDiaUtiDES.asmx?WSDL`). Esquema inline, sin `import` ni `include`: no hay XSD aparte.
- WSDL de producción: `docs/arca/wsdl/WDiaUtiDES/WDiaUtiDES-produccion.wsdl` (de `https://servicios3.arca.gob.ar/Dia/Ws/WDiaUtiDES/WDiaUtiDES.asmx?WSDL`, 2026-10-02). **Difiere**: le faltan `IdentificadorDispositivoSatelital` e `IdentificadorDispositivoNuevo` en `InNovedadDispositivo`. Homologación va un paso adelante.
- Llamadas sin credenciales contra homologación: 2026-10-02 entre 15:08 y 15:15 (hora de Argentina, -03:00).
- Catálogo (`catalogo.asp`): "Actualización / Consulta PEMA", tipo de agente habilitado `OTEN`, RG 2889/10.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA" (familia `testdia`/`servicios3`). No es el dialecto de `wswhomo` de `catalogo.md` 4.1: no hay `FEHeaderInfo` ni `Errors/Err`; el error viaja en un `Recibo` | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/dia/ws/WDiaUtiDES/WDiaUtiDES.asmx` | `soap:address` del WSDL; manual p.5 |
| Endpoint producción | `https://servicios3.arca.gob.ar/Dia/Ws/WDiaUtiDES/WDiaUtiDES.asmx` | `soap:address` del WSDL de producción; manual p.5 |
| Namespace | `ar.gov.afip.dia.serviciosWeb.WDiaUtiDES` (sin `http://`, con `W` mayúscula en `serviciosWeb`) | WSDL `targetNamespace` |
| `elementFormDefault` | `qualified`: todos los hijos llevan el namespace del servicio | WSDL |
| Bindings | `WDiaUtiDESSoap` (SOAP 1.1) y `WDiaUtiDESSoap12` (SOAP 1.2), los dos en el mismo endpoint | WSDL |
| SOAPAction | `ar.gov.afip.dia.serviciosWeb.WDiaUtiDES/<Operación>`, sensible a mayúsculas | WSDL; prueba en vivo |
| SOAP | 1.1 (`text/xml`) y 1.2 (`application/soap+xml; action=...`). `Dummy` respondió por las dos el 2026-10-02 | Prueba en vivo |
| WSAA service id | Probablemente `WDiaUtiDES`, **sin fuente directa**. El manual no lo nombra. En `catalogo.asp` el README de esta entrada apunta por error a `/ws/WDEPMOVIMIENTOS/README.txt` (el de `wdepMovimientos`). El único README que nombra `WDiaUtiDES` es `/ws/documentos/README.txt`, que está titulado "wGesTabRef" | Manual p.5; READMEs del catálogo |
| Operaciones | 8 en homologación y 8 en producción | WSDL |

## Autenticación

Cada método, salvo `Dummy`, recibe primero `argAutentica` de tipo `Autenticacion`. En el WSDL `Autenticacion` extiende `AutenticacionBase`, así que **el orden del XML es**:

```xml
<argAutentica>
  <Token>...</Token>        <!-- 1..1, nillable -->
  <Sign>...</Sign>          <!-- 1..1, nillable -->
  <Cuit>...</Cuit>          <!-- 0..1 -->
  <TipoAgente>...</TipoAgente>
  <Rol>...</Rol>
</argAutentica>
```

El manual (p.11) pide `Cuit` C(11), `TipoAgente` = `OTEN`, `UsuRol` = `EXTE` (en el WSDL el elemento se llama `Rol`, no `UsuRol`), `Token` y `Sign` del TA de WSAA. El token no va en header SOAP.

Las fallas de autenticación vuelven con **HTTP 200** y el código en `Recibo` (`CodErr`, `DesError`, `DescAdicErr`). Tabla del manual (p.11):

| Código | Mensaje del manual |
|---|---|
| 7004 | Error Interno. |
| 7005 | Token vencido |
| 7006 | Debe ingresar la firma |
| 7007 | Debe ingresar el token |
| 7008 | Token Inválido |
| 7013 | El Servicio no se corresponde con el informado en el Token |
| 7014 | Cuit con el que desea operar no informado |
| 6005 | CUIT,CUIL y/o tipo de agente invalido para el servicio |
| 6006 | Rol invalido para el tipo de agente y el servicio solicitado |
| 6003 | Validación de conexión no coincide con opciones seleccionadas |

Respuestas reales a `ConsultaDispositivo` (homologación, 2026-10-02). Los textos **no** son los del manual:

| Caso | HTTP | `Recibo` devuelto |
|---|---|---|
| `Token`=`abc`, `Sign`=`abc` | 200 | `<CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr>` y después `<Dispositivos />` |
| `Token` y `Sign` vacíos | 200 | `7007` / `Debe ingresar el Token y Firma.` / mismo `DescAdicErr` |
| Sin el elemento `argAutentica` | 200 | `7007` / `Debe ingresar el Token y Firma.` |
| `Token` = Base64 de `<sso><id/></sso>` | 200 | `7004` / `ID MWE: 73329194` / mismo `DescAdicErr`, **sin** `<Dispositivos />` |
| `Token` = Base64 de un `sso` 2.0 bien formado, `Sign` falso | 200 | `7006` / `Debe ingresar la Firma o la Firma es invalida.` |

Cuerpo completo del caso `abc`:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><ConsultaDispositivoResponse xmlns="ar.gov.afip.dia.serviciosWeb.WDiaUtiDES"><ConsultaDispositivoResult><Recibo><CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr></Recibo><Dispositivos /></ConsultaDispositivoResult></ConsultaDispositivoResponse></soap:Body></soap:Envelope>
```

- `DescAdicErr` trae literalmente `(SERVER:xxx.xxx.xxx.103) `, con espacio final.
- El número de `ID MWE` es un contador global del backend: subió de a uno entre llamadas a distintos servicios de la DIA (73329194 acá, 73329195 en WGesINV un segundo después).
- Headers observados: `Cache-Control: private, max-age=0`, `Content-Type: text/xml; charset=utf-8`, `Content-Length` y una cookie `TS010b76f1` del balanceador. No hay `Server` ni `X-AspNet-Version`.

## Operaciones

| Operación | Propósito | Entrada (tras `argAutentica`) | Salida | Estado |
|---|---|---|---|---|
| `ActualizaDispositivo` | Cambia el estado de un dispositivo (`ZGSA`, `PASA`, `ZGAR`, `DISP`, `PFER`) | `argDispositivo`: `IdentificadorDispositivo`, `IdentificadorDestinacion`, `Contenedor` (id de contenedor o país ISO-2 + patente), `Estado`, `FechaEstado` (dd/mm/aaaa) | `Recibo` | Crea/cambia. Clave: `IdentificadorDispositivo` + `IdentificadorDestinacion` + `Contenedor` |
| `ConsultaContenedor` | Medios transportadores asociados al prestador | `argContenedor`: `IdentificadorDestinacion`, `IdentificadorDispositivo`, `IdentificadorContenedor`, `EstadoOperacion`, `AduanaOrigen` | `Recibo` + `Contenedores/Contenedor[]` | Consulta |
| `ConsultaDispositivo` | Estado de un dispositivo y su operación (`SALI`, `SACO`, `ARRI`, `INGR`, `ANUL`...) | `argIdentificadorDispositivo` (string) | `Recibo` + `Dispositivos/Dispositivo[]` | Consulta. Lee lo que escribe `ActualizaDispositivo`/`InicioCargaSuelta` |
| `InicioCargaSuelta` | Arranca el circuito de carga suelta (deja el dispositivo en `ZGSA`) | `argInicioCargaSuelta`: `IdentificadorDispositivo`, `IdentificadorDestinacion`, `PaisPatente`, `Fecha` | `Recibo` | Crea. Clave: dispositivo + destinación + `PaisPatente` |
| `ConsultaDatosATA` | Datos y estado de un ATA | `argCuitATA` (string) | `Recibo` + `DatosATA` (`CuitATA`, `CodigoEstado`, `RazonSocial`) | Consulta (padrón externo) |
| `NovedadDispositivo` | Alta (`A`), baja (`B`) o modificación (`M`) en el padrón PEMA | `argNovedadDispositivo`: `IdentificadorDispositivo`, `TipoDispositivo`, `IdentificadorDispositivoInterno`, `ModeloDispositivo`, `Novedad`, y en homologación `IdentificadorDispositivoSatelital`, `IdentificadorDispositivoNuevo` | `Recibo` | Crea/cambia el padrón. Clave: `IdentificadorDispositivo` |
| `ConsultaPemaPadron` | Consulta el padrón PEMA | `argConsultaPemaPadron`: `IdentificadorDispositivo`, `EstadoDispositivo`, `TipoDispositivo` | `Recibo` + `DispositivosPema/Pema[]` | Consulta. Lee lo que escribe `NovedadDispositivo` |
| `Dummy` | Salud de app, base y autenticación | — | `appserver`, `dbserver`, `authserver` | — |

Respuesta real de `Dummy` (2026-10-02, HTTP 200, SOAP 1.1):

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><DummyResponse xmlns="ar.gov.afip.dia.serviciosWeb.WDiaUtiDES"><DummyResult><appserver>OK</appserver><dbserver>OK</dbserver><authserver>OK</authserver></DummyResult></DummyResponse></soap:Body></soap:Envelope>
```

Por SOAP 1.2 vuelve igual, con `xmlns:soap="http://www.w3.org/2003/05/soap-envelope"` y `Content-Type: application/soap+xml; charset=utf-8`.

## Errores

Todo error va en `Recibo` con HTTP 200. `CodErr` es `int` obligatorio. El éxito no es siempre `0`: `ActualizaDispositivo`, `ConsultaDispositivo`, `InicioCargaSuelta` y `ConsultaDatosATA` listan **20304 "Procedimiento terminado OK."** (manual p.18, p.25, p.28, p.31); `NovedadDispositivo` y `ConsultaPemaPadron` listan `0` "Procedimiento terminado OK" (p.34, p.37). La tabla de `ConsultaContenedor` (p.22) no trae código de éxito.

Errores de negocio más relevantes del manual (pp.17-18, 22, 25, 27-28, 30-31, 34, 37):

| Código | Mensaje | Dónde |
|---|---|---|
| 0 / 20304 | Procedimiento terminado OK | Todos |
| 10121 | No hay datos para los criterios ingresados | Consultas |
| 42034 | Falta dato obligatorio {Parámetro} | Todos |
| 10566 | Campo {Parámetro} longitud invalida | Todos |
| 10238 | Formato fecha inválido | `ActualizaDispositivo`, `InicioCargaSuelta` |
| 12404 | Dispositivo INEXISTENTE | Actualización, novedad |
| 12403 | El dispositivo está en estado incorrecto debe ser : | `ActualizaDispositivo` |
| 12409 | El dispositivo se encuentra asignado. | Actualización, carga suelta |
| 12623 | Dispositivo asignado a otro Medio Transportador | Actualización, carga suelta |
| 20150 | Destinación Inexistente. | Actualización, carga suelta |
| 11048 | Contenedor inexistente para la declaración | `ActualizaDispositivo` |
| 12482 | La Declaracion informada no contiene mercaderia suelta | `InicioCargaSuelta` |
| 11899 / 11900 | CUIT $1 no registrado / no habilitado como prestador | Actualización |
| 27045 / 20714 | Formato de CUIT Invalido / El CUIT ingresado es inválido | `ConsultaDatosATA` |
| 11891 | El identificador PEMA no corresponde a la empresa de conexion | `NovedadDispositivo` |
| 11895 | Numero interno de dispositivo ya informado | `NovedadDispositivo` |
| 30838 | El dispositivo se encuentra en estado xxx | `NovedadDispositivo` |
| 31353 | El campo $1 tiene un formato erroneo. Debe ser $2 | `ActualizaDispositivo`, `NovedadDispositivo` |

Estados del ATA en `CodigoEstado` (p.31): `HABI`, `SUSP`, `BAJA`, `BASU`, `FAGA`, `PROV`.

Errores de transporte observados (2026-10-02):

| Caso | HTTP | Respuesta |
|---|---|---|
| `SOAPAction` inexistente o con otra capitalización | 500 | `soap:Fault`, `faultcode` `soap:Client`, `faultstring` `El servidor no reconoció el valor del encabezado HTTP SOAPAction: ar.gov.afip.dia.serviciosWeb.WDiaUtiDES/NoExiste.` |
| XML cortado | 400 | Cuerpo vacío, sin `Content-Type` |
| `.asmx` inexistente en `testdia` | 500 | Página HTML "Error de servidor en la aplicación '/'" (no 404) |

## Comportamiento a simular

- Máquina de estados del dispositivo (manual pp.12-14): `InicioCargaSuelta` o `ActualizaDispositivo(ZGSA)` → la aduana autoriza la salida (operación `SALI`) → `ActualizaDispositivo(PASA)` → `ActualizaDispositivo(ZGAR)` → operación `ARRI`/`INGR` → `ActualizaDispositivo(DISP)`. `PFER` corta todo. Si la salida se anula (`ANUL`), el dispositivo vuelve a `DISP` y se reinicia en `ZGSA`. Desde `ZGSA` se puede pasar directo a `DISP`.
- Los estados de la operación (`SALI`, `SACO`, `ARRI`, `INGR`, `ANUL`) los mueve la aduana, no el prestador. El simulador necesita un disparador externo (endpoint de control o reloj) para avanzarlos.
- Pares de estado: `ActualizaDispositivo`/`InicioCargaSuelta` → `ConsultaDispositivo`/`ConsultaContenedor`; `NovedadDispositivo` → `ConsultaPemaPadron`. Una baja (`B`) debería hacer que `ConsultaPemaPadron` devuelva el estado de baja y que `ActualizaDispositivo` falle con 12404 o 12591.
- Carga suelta: el campo `Contenedor` lleva país ISO 3166 alfa-2 + patente concatenados (ej. `ARXYZ123`).
- En los errores de autenticación la respuesta sigue siendo el `Result` del método, con los arrays vacíos presentes (`<Dispositivos />`), salvo el 7004 que los omite.
- Copiar el orden real de autenticación: primero `7007` (vacío o ausente), después `7008` (no es Base64), después `7004` (Base64 que no es un `sso`) y `7006` (firma inválida).

## No verificado

- WSAA service id: si es `WDiaUtiDES` (el README que lo nombra es el de `wgesTabRef`).
- Respuestas autenticadas: forma real de `Dispositivos`, `Contenedores` y `DispositivosPema` con datos, y si el éxito sale como `0` o `20304` en cada método.
- Si producción acepta `IdentificadorDispositivoSatelital`/`IdentificadorDispositivoNuevo` (su WSDL no los tiene).
- Textos reales de 7005, 7013, 7014, 6003, 6005 y 6006.
- Comportamiento de SOAP 1.2 ante errores (solo se probó `Dummy`).
