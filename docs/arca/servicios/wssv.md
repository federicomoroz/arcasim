# wssv

Web Service de Seguimiento Vehicular (WSSV). Los prestadores de seguimiento (OLS) informan a la Aduana el inicio y fin de cada **traslado** de mercadería bajo control aduanero y, durante el viaje, la **posición y las alarmas** del precinto electrónico (PEMA / DES) instalado en el contenedor o camión. Lo usan prestadores habilitados por la Aduana.

Fuentes:

- Manual "Web Service de Seguimiento Vehicular (WSSV)", última revisión del 06/05/2010: `https://www.afip.gob.ar/ws/WSSV/WSSV-ManualParaElDesarrollador.pdf`.
- WSDL de homologación, descargado el 2026-10-02.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | .NET ASMX (header `MicrosoftOfficeWebServer: 5.0_Pub`, Fault de `System.Web.Services`) |
| SOAP | 1.1 (`ServiceSoap`) y 1.2 (`ServiceSoap12`), document/literal; además `dummy` por HTTP GET y POST (`ServiceHttpGet`, `ServiceHttpPost`) |
| Endpoint homologación | `https://wswhomo.afip.gov.ar/wssv/service.asmx`. El manual da `https://wswhomo.arca.gov.ar/...`, que **no resuelve DNS** |
| Endpoint producción | Manual: `https://wsw.arca.gob.ar/wssv/service.asmx`. **Ninguna variante respondió** el 2026-10-02: `wsw.arca.gob.ar` y `wsw.afip.gov.ar` fallan en la conexión (el catálogo registró certificado de otro nombre y conexión reseteada), y `servicios1.afip.gov.ar/wssv/...` da 404. **NO VERIFICADO** |
| WSDL | `?WSDL` |
| targetNamespace | `https://wswhomo.afip.gov.ar/wssv/service.asmx` (**la URL de homologación es el namespace**; `elementFormDefault="qualified"`). Qué namespace usa producción está **NO VERIFICADO**: si fuera la URL de producción, el cliente tendría que cambiar de namespace al cambiar de ambiente |
| portType / service | `ServiceSoap` (7 ops), `ServiceHttpGet` y `ServiceHttpPost` (solo `dummy`) / `Service` |
| SOAPAction | `https://wswhomo.afip.gov.ar/wssv/service.asmx/<Operación>` (namespace + `/` + nombre). ASMX despacha por SOAPAction y la exige |
| Archivo guardado | `wsdl/wssv-homologacion.wsdl` (sin imports) |
| WSAA service id | **No indicado** en el manual. Probablemente `wssv`: **NO VERIFICADO** |
| Versión | No expone versión ni `FEHeaderInfo` |

**Trampa de namespace, verificada.** Los ejemplos del manual usan `https://wswhomo.arca.gov.ar/wssv/service.asmx`. Con ese namespace y su SOAPAction la respuesta real (homologación, 2026-10-02) es HTTP 500:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><soap:Fault><faultcode>soap:Client</faultcode><faultstring>Server did not recognize the value of HTTP Header SOAPAction: https://wswhomo.arca.gov.ar/wssv/service.asmx/dummy.</faultstring><detail /></soap:Fault></soap:Body></soap:Envelope>
```

## Autenticación

```xml
<ListarAlarmas xmlns="https://wswhomo.afip.gov.ar/wssv/service.asmx">
  <AuthObj>
    <Token>...</Token>
    <Sign>...</Sign>
    <CUIT>20139999999</CUIT>
  </AuthObj>
</ListarAlarmas>
```

`CUIT` (long): "CUIT del contribuyente que delegó el servicio o del que informa en caso de que no haya delegación de servicio".

A diferencia de wscdc y wsfev1 (que devuelven los errores de token dentro de `Errors` con HTTP 200), **WSSV usa SOAP Fault con HTTP 500** para las credenciales (manual: "reporta los mismos a través de Soap Faults en caso de errores en las credenciales de acceso"). Verificado (homologación, 2026-10-02), `faultcode` `soap:Client`, `<detail />`:

| Caso | `faultstring` |
|---|---|
| `Token` = `abc` | `CargarStringBase64Token: Excepción: Invalid length for a Base-64 char array.` |
| `Token` = TA bien formado en base64, `Sign` = `abc` | `VerificacionDeHash: Error al convertir de Base64 a la firma: abc` |

Respuesta exacta del primer caso (`Content-Type: text/xml; charset=utf-8`, `Cache-Control: private`):

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><soap:Fault><faultcode>soap:Client</faultcode><faultstring>CargarStringBase64Token: Excepción: Invalid length for a Base-64 char array.</faultstring><detail /></soap:Fault></soap:Body></soap:Envelope>
```

Los textos son de la misma librería de validación de tokens que wscdc ("VerificacionDeHash", "Error al convertir de Base64"), pero sin el prefijo "ValidacionDeToken:".

Headers HTTP de todas las respuestas: `MicrosoftOfficeWebServer: 5.0_Pub`, `Expect-CT: enforce`, **`Strict-Transport-Security: nosniff`** (valor inválido, copiado tal cual), `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `X-Xss-Protection: 1; mode=block`, `Cache-Control: private, max-age=0` (200) o `private` (500), cookies F5 (`TS010b76f1` con `Domain=.wswhomo.afip.gov.ar`).

## Operaciones

7 operaciones SOAP. Todas las respuestas, salvo `dummy`, llevan `RError{ErrNum (short), ErrMsg?}`: `ErrNum` 0 = éxito.

| Operación | Propósito | Entrada (además de `AuthObj`) | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Health-check, sin auth | `dummy` vacío | `dummyResponse/dummyResult{appserver?, dbserver?, authserver?}` | — |
| `TrasladoBegin` | Inicia un traslado | `IdTras` (único), `IdDES` (precinto PEMA), `IdRuta`, `IdCont?`, `IdSalida?` | `TrasladoBeginResult{RError}` | **Crea** un traslado activo. Clave: `IdTras` |
| `Reporte` | Posición y alarmas del PEMA durante el traslado | `IdTras`, `IdDES`, `FHDES` (dateTime ISO 8601), `Lat`, `Lng` (double, WGS84 decimal), `Alarmas{string*}` | `ReporteResult{RError}` | **Agrega** una coordenada al traslado |
| `TrasladoEnd` | Cierra el traslado | `IdTras` | `TrasladoEndResult{RError}` | **Cierra** el traslado (deja de estar activo) |
| `ListarTrasladosActivos` | Traslados abiertos del prestador | — | `ListarTrasladosActivosResult{Traslados{Traslado*{IdTras, IdDES, IdRuta}}, RError}` | Consulta |
| `ListarTraslado` | Posiciones informadas de un traslado | `IdTras`, `IdDES` | `ListarTrasladoResult{Coordenadas{Coordenada*{Fecha, Lat, Lng}}, RError}` | Consulta. Clave: `IdTras` + `IdDES` |
| `ListarAlarmas` | Catálogo de alarmas | — | `ListarAlarmasResult{Alarmas{Alarma*{Id, Des}}, RError}` | Consulta (tabla fija) |

`IdCont` e `IdSalida` están en el WSDL pero **no en el manual**.

Reglas del manual:

- Frecuencia de `Reporte`: cada 200 m a menos de 50 km/h, cada 1000 m a más, y como mínimo cada 5 minutos.
- Sin datos del PEMA: coordenada 0/0 con alarma `NPM`. PEMA sin posición GPS: 0/0 con alarma `NPG`.
- Alarmas: solo se conocen `PTA` "Puerta Abierta", `NPM` y `NPG`; la lista completa sale de `ListarAlarmas` (**NO VERIFICADA**).
- **Homologación**: con el prefijo `TEST` al inicio de `IdTras` se evitan "los controles rigurosos de estado de un traslado".
- `Fecha` de `ListarTraslado` sale en UTC (`2009-11-18T20:38:57Z`) aunque se haya informado con `-03:00`.

Respuesta real de `dummy` por SOAP 1.1 (homologación, 2026-10-02), HTTP 200, `Content-Length: 433`:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><dummyResponse xmlns="https://wswhomo.afip.gov.ar/wssv/service.asmx"><dummyResult><appserver>OK</appserver><dbserver>OK</dbserver><authserver>OK</authserver></dummyResult></dummyResponse></soap:Body></soap:Envelope>
```

Respuesta real de `GET /wssv/service.asmx/dummy` (HTTP 200, `Content-Length: 313`, indentado):

```xml
<?xml version="1.0" encoding="utf-8"?>
<DummyResponse xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="https://wswhomo.afip.gov.ar/wssv/service.asmx">
  <appserver>OK</appserver>
  <dbserver>OK</dbserver>
  <authserver>OK</authserver>
</DummyResponse>
```

## Errores

| Nivel | Transporte | Detalle |
|---|---|---|
| Credenciales, SOAPAction desconocida, XML inválido | SOAP Fault `soap:Client`, HTTP 500, `<detail />` | Textos verificados arriba |
| Negocio | `RError{ErrNum ≠ 0, ErrMsg}` con HTTP 200 | El manual no lista códigos ni textos: **NO VERIFICADO** |

## Comportamiento a simular

- ASMX con SOAP 1.1, 1.2 y GET/POST de `dummy`; despacho por SOAPAction (Fault "Server did not recognize the value of HTTP Header SOAPAction: ...").
- Auth por Fault 500 con los textos de la librería de token.
- Estado por prestador (CUIT): traslados con `IdTras` único, `IdDES`, `IdRuta`, estado activo/cerrado y lista de coordenadas con alarmas.
- Reglas razonables (los códigos reales no se conocen): `TrasladoBegin` con `IdTras` repetido → error; `Reporte` o `TrasladoEnd` sobre un traslado inexistente o cerrado → error; con prefijo `TEST` en homologación, relajar esas reglas como dice el manual.
- Par de estado: `TrasladoBegin` → aparece en `ListarTrasladosActivos`; `Reporte` → aparece en `ListarTraslado`; `TrasladoEnd` → desaparece de los activos.
- Copiar los headers HTTP raros (`Strict-Transport-Security: nosniff`, `MicrosoftOfficeWebServer`).

## No verificado

- Endpoint, WSDL y namespace de producción.
- El service id de WSAA.
- Los `ErrNum` y `ErrMsg` de negocio, y el catálogo completo de alarmas.
- Significado de `IdCont` e `IdSalida`.
