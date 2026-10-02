# miargentina-ws

Servicio de ARCA para la app **Mi Argentina** (sección "Mi trabajo"): dado un CUIL, devuelve la vida laboral del trabajador (relaciones laborales declaradas, estado de aportes y contribuciones, servicio doméstico) y, según el manual, el resumen de su liquidación de sueldo. El consumidor es la Secretaría de Innovación que administra la app; un particular no lo puede usar.

Fuentes:

- Manual "MiArgentina Web Service" 2.2 (05/09/2023): `https://www.afip.gob.ar/ws/Mi-argentina/MiArgentina-Webservice-Manual-del-Desarrollador-v2.2.pdf`. Varias páginas del PDF están recortadas: faltan el request completo y la tabla de campos de `ObtenerInformacionLaboral`.
- WSDL de homologación y producción, descargados el 2026-10-02 (iguales salvo el `soap:address`).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | .NET ASMX (`servicios.asmx`, `System.Web.Services` en los Fault) |
| SOAP | 1.1 y 1.2 (bindings `serviciosSoap` y `serviciosSoap12`), document/literal |
| Endpoint homologación | `https://webserviceshomoext.afip.gob.ar/miargentina-ws/servicios.asmx` |
| Endpoint producción | `https://wswss.afip.gob.ar/miargentina-ws/servicios.asmx` |
| WSDL | `?wsdl` |
| targetNamespace | `https://serviciosnet.afip.gob.ar/miargentina-ws/` (con `https`; `elementFormDefault="qualified"`) |
| portType / service | `serviciosSoap` / `servicios` |
| SOAPAction | `https://serviciosnet.afip.gob.ar/miargentina-ws/Dummy` y `.../ObtenerInformacionLaboral` |
| Header de respuesta | `HeaderInfo{ambiente?, fechaHora?, hostName?, version?}` en el namespace del servicio, declarado como `soap:header` de salida en las dos operaciones |
| Archivo guardado | `wsdl/miargentina-ws-homologacion.wsdl` (sin imports) |
| WSAA service id | `miargentina-ws` (manual, 2.2). Lo confirma el error en vivo "Servicio inválido o no implementado" |
| Versión | `HeaderInfo/version` = `1.0.1`, `hostName` = `WswHomoExt`, `ambiente` = `Homologación` (homologación, 2026-10-02) |

**El manual documenta una operación que no existe.** `ObtenerResumenLiquidacion` (manual 2.5, desde la versión 2.0) **no está en el WSDL** de homologación ni en el de producción. Llamarla en homologación (2026-10-02) devuelve el Fault de ASMX "The request element <ObtenerResumenLiquidacion ...> was not recognized." Ver Errores.

## Autenticación

```xml
<ObtenerInformacionLaboral xmlns="https://serviciosnet.afip.gob.ar/miargentina-ws/">
  <credencial>
    <Token>...</Token>
    <Sign>...</Sign>
    <CUITrepresentada>23112233449</CUITrepresentada>
  </credencial>
  <cuil>27147961753</cuil>
</ObtenerInformacionLaboral>
```

Ojo con las mayúsculas: `credencial` y `cuil` en minúscula; `Token`, `Sign` y `CUITrepresentada` con mayúscula. `CUITrepresentada` es "CUIT del contribuyente que utiliza el webservice, o que delega el webservice a un tercero".

**Los errores de autenticación vienen dentro de la respuesta normal**, HTTP 200, en `EstadoTransaccion` (verificado en homologación, 2026-10-02):

| Caso | `Codigo` | `Descripcion` |
|---|---|---|
| `Token` = `abc` | 9101 | `La codificación del token no es base64` |
| `Token` = TA bien formado en base64 para el servicio `veconsumerws`, `Sign` falso | 9101 | `Servicio inválido o no implementado. Nombre de servicio recibido: veconsumerws` |

El segundo caso muestra que **valida el servicio del token antes que la firma**. `Id` fue `4701` y `4703` en llamadas consecutivas: un contador numérico global, no el GUID que muestra el manual en `ObtenerResumenLiquidacion`.

Respuesta exacta del primer caso (`Content-Type: text/xml; charset=utf-8`, `Cache-Control: private, max-age=0`):

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><HeaderInfo xmlns="https://serviciosnet.afip.gob.ar/miargentina-ws/"><ambiente>Homologación</ambiente><fechaHora>2026-10-02T15:22:40.942-03:00</fechaHora><hostName>WswHomoExt</hostName><version>1.0.1</version></HeaderInfo></soap:Header><soap:Body><ObtenerInformacionLaboralResponse xmlns="https://serviciosnet.afip.gob.ar/miargentina-ws/"><ObtenerInformacionLaboralResult><EstadoTransaccion><Id>4701</Id><Codigo>9101</Codigo><Descripcion>La codificación del token no es base64</Descripcion></EstadoTransaccion></ObtenerInformacionLaboralResult></ObtenerInformacionLaboralResponse></soap:Body></soap:Envelope>
```

(Sin `Datos` cuando hay error.)

## Operaciones

2 operaciones en el WSDL (3 en el manual).

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| `Dummy` | Infraestructura, sin auth | `Dummy` vacío | `DummyResponse/DummyResult{AppServer?, DBServer?, AuthServer?}` + `HeaderInfo` | — |
| `ObtenerInformacionLaboral` | Vida laboral de un CUIL | `credencial{Token?, Sign?, CUITrepresentada}`, `cuil` (long) | `ObtenerInformacionLaboralResult{EstadoTransaccion{Id?, Codigo (long), Descripcion?}, Datos?{Cuil, Nombre?, Apellido?, CodigoMensaje (int), Mensaje?, MisAportes?{Item*}, MisAportesCasasParticulares?{Item*}}}` | Consulta. Clave: `cuil` |
| `ObtenerResumenLiquidacion` | Resumen de la liquidación de sueldo (Libro de Sueldos Digital) | `credencial`, `cuil`, `cuitEmpleador` | `ObtenerResumenLiquidacionResult{EstadoTransaccion, Datos{Cuil, CuitEmpleador, RazonSocial, Periodo, Mensaje, TotalNeto, TotalBruto, TotalDescuentos, ItemsBruto{Concepto*{Codigo, Descripcion, Importe}}, ItemsDescuento{Concepto*}}}` | **Solo en el manual**: no está en el WSDL y el servicio la rechaza |

`MisAportes/Item` (`MisAportesModel`): `CodigoMensaje` (int), `Mensaje`, `Cuit`, `RazonSocial`, `FechaInicioRelacionLaboral` (`dd/mm/aaaa`), `DomicilioLaboral`, `CodigoDomicilioLaboral` (0 existe, 1 ambulante, 2 no existe en la base), `UltimaVezQueFuisteDeclaradoPorEsteEmpleador` (`mm/aaaa`), `ModalidadContratacion`, `EstadoAportesSeguridadSocial`, `EstadoAportesObraSocial` y `EstadoContribucionesObraSocial` (1 verde, totalmente pago; 2 amarillo, parcial; 3 rojo, impago). Todos los campos de texto son string, incluso los numéricos.

`MisAportesCasasParticulares/Item`: `CodigoMensaje`, `Mensaje`, `Cuit`, `RazonSocial`, `ObraSocial`, `FechaInicioRelacionLaboral`, `DomicilioLaboral`, `CategoriaProfesional`, `HorasSemanales`, `ModalidadContratacion`, `AseguradoraDeRiesgosDelTrabajo` y `UltimoPagoRegistrado`.

Valores de los ejemplos del manual: `CodigoMensaje` 2001 "Última vez que fuiste declarado por este empleador: Período 01/2019"; 3002 "No se registran pagos efectuados por tu empleador". En `ObtenerResumenLiquidacion`, los importes son strings con formato argentino (`$ 440.256,87`) y `Periodo` es `12/2022`.

Respuesta real de `Dummy` (homologación, 2026-10-02), HTTP 200, `Content-Length: 686`:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><HeaderInfo xmlns="https://serviciosnet.afip.gob.ar/miargentina-ws/"><ambiente>Homologación</ambiente><fechaHora>2026-10-02T15:22:40.348-03:00</fechaHora><hostName>WswHomoExt</hostName><version>1.0.1</version></HeaderInfo></soap:Header><soap:Body><DummyResponse xmlns="https://serviciosnet.afip.gob.ar/miargentina-ws/"><DummyResult><AppServer>OK</AppServer><DBServer>OK</DBServer><AuthServer>OK</AuthServer></DummyResult></DummyResponse></soap:Body></soap:Envelope>
```

## Errores

`EstadoTransaccion{Id, Codigo, Descripcion}`: `Codigo` 0 = OK; distinto de 0 = error.

| Código | Texto | Fuente |
|---|---|---|
| 0 | OK | Manual |
| 9001 | Ud. fue declarado, pero no posee liquidación en este periodo (empleado declarado que por condiciones de negocio no requiere carga de liquidación) | Manual, `ObtenerResumenLiquidacion` |
| 9002 | Su empleador no se encuentra incluido en este régimen de información. Próximamente podrá visualizar información de su liquidación digital (empleador no obligado a Libro de Sueldos Digital) | Manual |
| 9003 | Usted no posee liquidación en este periodo / "No se encontró una liquidación para la relación laboral solicitada" (el empleador no cargó liquidaciones para el CUIL) | Manual (la tabla y el ejemplo dan textos distintos) |
| 9101 | Errores de autenticación, con texto variable (ver arriba) | En vivo |

Fault de ASMX para un elemento desconocido (homologación, 2026-10-02). **Llegó con HTTP 200**, `Cache-Control: private`:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><soap:Fault><faultcode>soap:Client</faultcode><faultstring>System.Web.Services.Protocols.SoapException: The request element &lt;ObtenerResumenLiquidacion xmlns='https://serviciosnet.afip.gob.ar/miargentina-ws/'&gt; was not recognized.
   at System.Web.Services.Protocols.Soap11ServerProtocolHelper.RouteRequest()
   at System.Web.Services.Protocols.SoapServerProtocol.Initialize()
   at System.Web.Services.Protocols.ServerProtocolFactory.Create(Type type, HttpContext context, HttpRequest request, HttpResponse response, Boolean&amp; abortProcessing)</faultstring><detail /></soap:Fault></soap:Body></soap:Envelope>
```

(ASMX suele mandar estos Fault con HTTP 500; acá el código fue 200. Saltos de línea CRLF o LF dentro del `faultstring`: **NO VERIFICADO**.)

## Comportamiento a simular

- `HeaderInfo` en todas las respuestas que no son Fault, con `ambiente` "Homologación", `fechaHora` con milisegundos y offset, `hostName` y `version` 1.0.1.
- Auth dentro de `EstadoTransaccion` con código 9101 y texto específico; verificar primero que el token sea base64, después el servicio (`miargentina-ws`), después la firma, el vencimiento y la relación (textos de esos tres **NO VERIFICADOS**).
- `Id` como contador global creciente.
- Padrón de trabajadores con sus relaciones laborales (`MisAportes`) y de casas particulares, con semáforo de aportes y `CodigoMensaje`/`Mensaje` por relación.
- Rechazar `ObtenerResumenLiquidacion` con el Fault de ASMX, salvo que ArcaSim decida ofrecerla como extensión documentada.
- Sin estado: solo lectura.

## No verificado

- La tabla completa de `CodigoMensaje` (2001, 3002 y los demás) y el contenido de `Nombre`, `Apellido`, `CodigoMensaje` y `Mensaje` a nivel de `Datos`: las páginas del manual que los describen están recortadas.
- Los textos de 9101 para firma inválida, token vencido o CUIT fuera de relations.
- Si `ObtenerResumenLiquidacion` existe en algún otro endpoint.
- Qué devuelve para un CUIL sin relaciones laborales.
