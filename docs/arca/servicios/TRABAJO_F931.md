# TRABAJO_F931

Consulta de F931 (SSF931, "Web Service de Seguridad Social de Información del F931"). Un **organismo externo** (el título del manual dice "para el MTEySS", el Ministerio de Trabajo) consulta las remuneraciones declaradas en las DDJJ F931 de Seguridad Social: por empleado (CUIT del empleador + CUIL + período) o el total de la DJ (CUIT + período). No es para contribuyentes.

Fuentes:

- Manual "Consulta de F931 para el MTEySS", revisión del 23 de agosto de 2010 (el historial dice 23-10-2010, versión 1.0): `https://www.afip.gob.ar/ws/TRABAJO_F931/TrabajoF931-ManualParaElDesarrollador.pdf`.
- **No hay WSDL alcanzable.** El manual solo muestra `POST /WebService/F931.asmx` con `Host: localhost`: copió la página de ayuda de ASMX de una máquina de desarrollo. El 2026-10-02 se probaron, sin éxito, `/WebService/F931.asmx`, `/ssf931/F931.asmx`, `/F931/F931.asmx`, `/trabajo_f931/F931.asmx` y `/wsf931/F931.asmx` (con `?WSDL`) en `wswhomo.afip.gov.ar`, `webserviceshomoext.afip.gob.ar`, `servicios1.afip.gov.ar` y `wswss.afip.gob.ar` (todas 404) y en `wsw.afip.gov.ar` (sin conexión). **Todo el contrato de abajo sale del manual.**

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | .NET ASMX (página de ayuda de ASMX, namespace `http://tempuri.org/`; el apéndice habla de "Exceptions de .Net") |
| SOAP | 1.1 según el manual (los ASMX publican además 1.2, **NO VERIFICADO**) |
| Endpoint homologación | **NO VERIFICADO**. Ruta del manual: `/WebService/F931.asmx`, sin host |
| Endpoint producción | **NO VERIFICADO** |
| WSDL | No disponible |
| Namespace | `http://tempuri.org/` (el default de .NET); los elementos van calificados con namespace por defecto, como en todo ASMX |
| SOAPAction | `"http://tempuri.org/getRemEmpleado"` y `"http://tempuri.org/getDeterminativa"` |
| Archivo guardado | Ninguno |
| WSAA service id | **No indicado**. El manual exige un TA de WSAA, pero no dice el nombre. El código 97004 ("Token con identificador de servicio inválido") confirma que se valida |
| Versión | — |

Para ArcaSim habrá que **construir el WSDL** a partir de esto. Lo que no está en el manual (minOccurs, nillable, SOAP 1.2, `dummy`) queda marcado como decisión de ArcaSim.

## Autenticación

```xml
<getRemEmpleado xmlns="http://tempuri.org/">
  <credencial>
    <Token>string</Token>
    <Sign>string</Sign>
    <CUITDelegado>string</CUITDelegado>
  </credencial>
  <cuitEmpleador>string</cuitEmpleador>
  <cuilEmpleado>string</cuilEmpleado>
  <periodo>string</periodo>
</getRemEmpleado>
```

- `credencial` en minúscula; `Token`, `Sign` y `CUITDelegado` con mayúscula.
- `CUITDelegado`: "CUIT del contribuyente que utiliza el webservice". Tiene que estar en las relations del TA (códigos 98004 a 98006).
- La página de ayuda muestra `string` en todos los parámetros de entrada, aunque la tabla diga "Numérico(11)". Es lo que dice el WSDL generado por .NET para propiedades `string`: los CUIT y el período viajan como texto.

**Los errores de ticket vienen dentro de la respuesta normal**, en `CodigoRespuesta` y `DescripcionRespuesta` (manual, Apéndice 1). No hay Fault documentado. Ninguna respuesta se pudo capturar en vivo.

| Código | Descripción |
|---|---|
| 97001 | Formato de token erróneo. No es base 64 válido. |
| 97002 | Formato de token erróneo. Formato xml inconsistente. |
| 97003 | Token expirado. |
| 97004 | Token con identificador de servicio inválido. |
| 97005 | Fecha de Generación del Token Inválido. |
| 97006 | Token Nulo |
| 98001 | Formato de firma erróneo. No es base 64 válido. |
| 98002 | Firma inválida para el token. |
| 98003 | Firma Nula |
| 98004 | CUITDelegado no especificado. |
| 98005 | Lista de relaciones vacia (no se puede verificar el CUITDelegado. [sic, sin cerrar el paréntesis] |
| 98006 | CUITDelegado inválido. |
| 98099 | Exception genérica de validacion de ticket |

## Operaciones

2 operaciones (según el manual). No se documenta `dummy`.

| Operación | Propósito | Entrada (además de `credencial`) | Salida | Estado |
|---|---|---|---|---|
| `getRemEmpleado` | Remuneraciones de un empleado en un período | `cuitEmpleador` (11), `cuilEmpleado` (11), `periodo` (`AAAAMM`) | `getRemEmpleadoResponse/getRemEmpleadoResult{CodigoRespuesta, DescripcionRespuesta, PeriodoFiscal, RemuneracionTotal, RemuneracionImponibleAPSS, RemuneracionImponibleCOSS}` | Consulta. Clave: CUIT + CUIL + período |
| `getDeterminativa` | Totales de la DJ F931 de un empleador en un período | `cuitEmpleador`, `periodo` | `getDeterminativaResponse/getDeterminativaResult{CodigoRespuesta, DescripcionRespuesta, PeriodoFiscal, RemuneracionTotal, RemuneracionImponibleAPSS, CantidadEmpleados}` | Consulta. Clave: CUIT + período |

Tipos de la respuesta (manual): `CodigoRespuesta` string de 5 (`'00000'`); `DescripcionRespuesta` string; `PeriodoFiscal` string `AAAAMM`; remuneraciones `decimal` (15,2); `CantidadEmpleados` `long`. El manual escribe "CódigoRespuesta" y "DescripciónRespuesta" con tilde en una tabla, pero el XML de ejemplo usa `CodigoRespuesta` y `DescripcionRespuesta` sin tilde: usar los del XML.

Reglas:

- `periodo` dentro de los últimos 12 meses incluyendo el actual (ejemplo del manual: consultando el 25/08/2010, de 09/2009 a 08/2010).
- Si `CodigoRespuesta` = `00000`, todos los campos traen datos; si no, "solo corresponde evaluar el campo DescripcionRespuesta". Con error, los decimales igual tienen que viajar (son `decimal` no anulables en .NET), probablemente en 0: **NO VERIFICADO**.

Respuesta de ejemplo (página de ayuda del manual, con marcadores):

```xml
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <getRemEmpleadoResponse xmlns="http://tempuri.org/">
      <getRemEmpleadoResult>
        <CodigoRespuesta>string</CodigoRespuesta>
        <DescripcionRespuesta>string</DescripcionRespuesta>
        <PeriodoFiscal>string</PeriodoFiscal>
        <RemuneracionTotal>decimal</RemuneracionTotal>
        <RemuneracionImponibleAPSS>decimal</RemuneracionImponibleAPSS>
        <RemuneracionImponibleCOSS>decimal</RemuneracionImponibleCOSS>
      </getRemEmpleadoResult>
    </getRemEmpleadoResponse>
  </soap:Body>
</soap:Envelope>
```

## Errores

Todo en `CodigoRespuesta` / `DescripcionRespuesta`, HTTP 200 (inferido del ejemplo `HTTP/1.1 200 OK` del manual).

| Código | Descripción | Tipo |
|---|---|---|
| 00000 | CONSULTA OK – NO HAY ERROR | Negocio |
| 00001 | SE REGISTRA DJ PARA EL CUIT PERO NO CONTIENE AL CUIL | Negocio |
| 10014 | NO SE REGISTRAN DDJJ PARA LA RELACION CUIT/CUIL/PERIODO SOLICITADA | Negocio |
| 10015 | CUIT / CUIL ASOCIADO CON OTRO | Negocio |
| 10021 | NO SE REGISTRAN DDJJ PARA LA RELACION CUIT/PERIODO SOLICITADA | Negocio |
| 97001 a 98099 | Ver tabla de autenticación | Ticket |
| 99901 | Exception por argumentos inválidos. | .NET |
| 99902 | Excepcion de comunicación con el cics-web. | .NET |
| 99998 | Exception Genérica – No tipificada. | .NET |
| 99999 | Flujo de proceso del webservice con resultado no contemplado. | .NET |

El manual no dice qué código sale para un período fuera de los últimos 12 meses (probablemente 99901: **NO VERIFICADO**).

## Comportamiento a simular

- ASMX en `/WebService/F931.asmx` bajo un host configurable, con WSDL generado por ArcaSim con los nombres de arriba, namespace `http://tempuri.org/` y SOAPAction `http://tempuri.org/<op>`.
- Validación del TA con los códigos 97xxx y 98xxx, en el orden de la tabla (formato del token, XML, vencimiento, servicio, fecha de generación; formato y verificación de la firma; `CUITDelegado` presente, relations no vacías, CUIT en relations).
- Datos: DDJJ F931 por empleador y período, con nómina de CUIL y sus tres remuneraciones. `getDeterminativa` suma totales y cuenta empleados. Códigos 00001 (DJ sin ese CUIL), 10014, 10021.
- Validar el período dentro de la ventana de 12 meses.
- Solo lectura.
- Marcar en ArcaSim que este servicio es una reconstrucción: no hay WSDL oficial para comparar.

## No verificado

- Host, WSDL, namespace real y existencia de `dummy`.
- El service id de WSAA.
- El código para período fuera de rango, el contenido de los decimales en error y el significado de 10015.
- Si el servicio sigue en funcionamiento (el manual es de 2010).
