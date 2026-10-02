# wsrgiva

Web Service del Régimen de Percepción de IVA (RG 5319/2023). Los **portales virtuales de comercio** (plataformas digitales) nominados como agentes de percepción consultan, para cada comprador, qué alícuota de percepción de IVA le corresponde. El resultado sale de "una serie de controles puntuales" sobre el comprador (manual, 1.5.1). Solo lo pueden usar CUIT habilitadas en el Administrador de Relaciones para este servicio.

Fuentes:

- Manual WSRGIVA 1.0 (03/04/2023): `https://www.afip.gob.ar/ws/documentacion/manuales/manualdesarrolladorWSRGIVA.pdf`.
- WSDL de homologación y producción, descargados el 2026-10-02 (iguales salvo el `soap:address`).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, Spring Boot WS de la familia "factu.fisca" (mismo header `info` que publica el manual). Detrás del F5 de `fwshomo` |
| SOAP | 1.1, document/literal |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsrgiva/services/RegimenPercepcionIVAService`. El manual da `fwshomo.arca.gov.ar` (**no resuelve DNS**), y el WSDL anexo del manual trae `https://serviciosexternos-homo.cloudhomo.arca.gob.ar/...` |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsrgiva/services/RegimenPercepcionIVAService` (el manual da `serviciosjava.arca.gob.ar`) |
| WSDL | `?wsdl` |
| targetNamespace | `http://impl.service.wsrgiva.afip.gov.ar/RegimenPercepcionIVAService/` (sin `elementFormDefault`: hijos sin namespace) |
| portType / binding / service | `RegimenPercepcionIVAServicePortType_v2` / `RegimenPercepcionIVAServiceSoap11Binding` / `RegimenPercepcionIVAService_v2.0`, port `RegimenPercepcionIVAServiceHttpSoap11Endpoint` |
| SOAPAction | `http://impl.service.wsrgiva.afip.gov.ar/RegimenPercepcionIVAService/<operación>` |
| Archivo guardado | `wsdl/wsrgiva-homologacion.wsdl` (sin imports) |
| WSAA service id | **No indicado** en el manual. El error 508 ("El Servicio asociado a Token difiere del especificado para el Sistema") confirma que se valida, pero no dice el nombre. Probablemente `wsrgiva`: **NO VERIFICADO** |
| Versión | Header `info/id`: `wsrgiva 1.0.0-SNAPSHOT 2023-03-29T17:47:20.710Z` (homologación, 2026-10-02) |

**Trampas.**

- **Namespace**: el manual (ejemplos y WSDL anexo) usa `http://impl.service.wsrgiva.arca.gov.ar/RegimenPercepcionIVAService/`; el WSDL real, `...wsrgiva.afip.gov.ar...`. Seguir al real.
- **Nombre de la operación**: el manual describe `consultarConstanciaPorLoteRequest` con `cuitContribuyenteArray/cuitContribuyente`, que es la versión 1. El WSDL real solo tiene `consultarConstanciaPorLote_v2`, con `datosTransaccionArray/datosTransaccion{cuitContribuyente, tipoBienesInvolucrados}`. El WSDL anexo al manual ya es el v2.
- **`dummy` sin cuerpo**: `dummyRequest` no tiene partes; despacho por SOAPAction.

## Autenticación

```xml
<authRequest>
  <token>...</token>
  <sign>...</sign>
  <cuitRepresentada>30000000001</cuitRepresentada>
</authRequest>
```

`cuitRepresentada` (long) es "la CUIT del sujeto obligado por la RG".

**Los errores de autenticación no son Fault**: vuelven como una respuesta normal, HTTP 200, con uno o más `constancia` que llevan `codigoError` y `descripcionError` (manual, 1.4.3). Verificado en vivo. En esas constancias, `idContribuyente` es la **`cuitRepresentada`**, no la CUIT consultada, y se devuelve **una constancia por error** de autenticación, no una por CUIT del lote.

Respuesta real con `token` = `abc`, `sign` = `abc` (homologación, 2026-10-02), HTTP 200, `Content-Type: text/xml;charset=UTF-8`:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente>homologacion-externa - FI1</ambiente><fecha>2026-10-02T15:19:21</fecha><id>wsrgiva 1.0.0-SNAPSHOT 2023-03-29T17:47:20.710Z</id></info></soap:Header><soap:Body><ns2:consultarConstanciaPorLote_v2Response xmlns:ns2="http://impl.service.wsrgiva.afip.gov.ar/RegimenPercepcionIVAService/"><return><constancia><fechaConsulta>02-10-2026</fechaConsulta><idContribuyente>30000000001</idContribuyente><codigoError>505</codigoError><descripcionError>El Token no se corresponde con la Firma. {token = [abc]firma = [abc]}</descripcionError></constancia><constancia><fechaConsulta>02-10-2026</fechaConsulta><idContribuyente>30000000001</idContribuyente><codigoError>506</codigoError><descripcionError>El Token no cumple con el Esquema (XSD) de Autenticacion. {abc}</descripcionError></constancia></return></ns2:consultarConstanciaPorLote_v2Response></soap:Body></soap:Envelope>
```

Con un TA bien formado en base64 (de otro servicio, vencido) y firma falsa salió **una sola** constancia: `505` "El Token no se corresponde con la Firma. {token = [<base64 completo>]firma = [abc]}". Es decir, con la firma inválida no llega a evaluar 508 ni 509.

Códigos de autenticación (manual, 1.4.3):

| Código | Descripción |
|---|---|
| 502 | La Seccion de Autenticación del Request no cumple con el Esquema (XSD) de Autenticacion. |
| 503 | El Formato del Token es inválido. |
| 504 | El Formato de la Firma es inválida. |
| 505 | El Token no se corresponde con la Firma. (en vivo agrega ` {token = [..]firma = [..]}`) |
| 506 | El Token no cumple con el Esquema (XSD) de Autenticacion. (en vivo agrega ` {<token>}`) |
| 507 | El Formato del Servicio asociado a Token esinválido. [sic] |
| 508 | El Servicio asociado a Token difiere del especificado para el Sistema. |
| 509 | El Token se encuentra Expirado. (ejemplo: ` {tiempo_expiracion_token = [1678949096000]tiempo_actual = [1679607223526]}`, en milisegundos) |
| 510 | La CUIT, CUIL o CDI es Nula, esta Vacia o tiene un Formato inválido. |
| 511 | La CUIT, CUIL o CDI no pudo ser encontrada. |
| 512 | La CUIL o CDI no esta activa. |
| 513 | La CUIT no esta activa. |
| 514 | La CUIT no tiene un domicilio activa. |
| 515 | La CUIT no tiene una actividad activa. |
| 516 | El Servicio de Autenticacion no se encuentra Operativo. |
| 517 | La CUIT Representada no se encuentra entre las que pueden ser Representadas en Relations. |

## Operaciones

2 operaciones.

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Infraestructura | Body vacío; SOAPAction `.../dummy` | `dummyResponse/return{appserver, authserver, dbserver}` | — |
| `consultarConstanciaPorLote_v2` | Situación fiscal y alícuota de percepción de 1 a 100 compradores | `authRequest`, `datosTransaccionArray{datosTransaccion[1..100]{cuitContribuyente: long, tipoBienesInvolucrados: unsignedByte}}` | `return{constancia[1..100]{fechaConsulta, idContribuyente, descripcionContribuyente?, vigencia?, codigoLeyenda?, descripcionLeyenda?, codigoSeguridad?, codigoError?, descripcionError?}}` | Consulta. Una constancia por CUIT; clave `idContribuyente`. `codigoSeguridad` es un hash de la consulta (es la prueba que guarda el agente) |

- `fechaConsulta` y `vigencia`: string `dd-mm-aaaa` (en vivo, `02-10-2026`).
- `tipoBienesInvolucrados`: "En esta versión sólo se admite el valor 1 (uno)" (error 4002).
- Cada constancia es de éxito (`descripcionContribuyente`, `vigencia`, `codigoLeyenda`, `descripcionLeyenda`, `codigoSeguridad`) o de error (`codigoError`, `descripcionError`).

**Leyendas** (manual, 2.3):

| Código | Leyenda |
|---|---|
| 2 | Alícuota 0% - Sujeto Exento o No Alcanzado en IVA |
| 3 | Alícuota 8% - El sujeto registra alguna de las situaciones dispuestas en el segundo párrafo del artículo 8° de la RG 5319/2023 |
| 4 | Alícuota 7% - El contribuyente se encuentra obligado a emitir comprobantes clase "M" |
| 5 | Alícuota 7% - El contribuyente registra en SIPER Categoría D: Alto o Categoría E: Muy Alto |
| 6 | Alícuota 7% - El contribuyente presenta la C.U.I.T. limitada o inactiva |
| 7 | Alícuota 5% - El contribuyente posee algunos de los domicilios fiscal, legal y/o comercial inválido |
| 8 | Alícuota 5% - El contribuyente no tiene constituido el Domicilio fiscal electrónico. |
| 9 | Alícuota 3% - El contribuyente registra falta de presentación de la Declaración Jurada de IVA período/s: XXXX |
| 10 | Alícuota 3% - ... Declaración Jurada del Impuesto a las Ganancias período fiscal |
| 11 | Alícuota 3% - ... Declaración Jurada de Seguridad Social período/s: XXXX |
| 12 | Alícuota 3% - ... Declaración Jurada de Bienes Personales / Bienes Personales Acciones o Participaciones, período/s: XXXX |
| 13 | Alícuota 3% - ... Declaración Jurada de Libro de Iva Digital período/s: XXXX |
| 14 | Alícuota 3% - ... Declaración Jurada de Memoria y Estados Contables, período/s: XXXX |
| 15 | Alícuota 3% - ... Declaración Jurada de Participaciones societarias, período/s: XXXX |
| 16 | Alícuota 0% - El contribuyente se encuentra exceptuado de la percepción por estar comprendido en el inciso a) del Artículo 4° de la RG 5319/2023 |
| 17 | Alícuota 0% - El contribuyente se encuentra nominado como agente de percepción de la RG 5319/2023 |
| 18 | Alícuota 1% - Responsables inscriptos, sin incumplimientos. |
| 20 | Sujeto adherido al RS de Monotributo. Se encuentra exceptuado de la percepción, salvo aplicación del Art. 9°de la RG 5319/2023 |
| 22 | Alícuota 8% - El sujeto registra alguna de las situaciones dispuestas en el segundo párrafo del artículo 8° de la RG 5319/2023 |
| 23 | Alícuota 8% - Sujeto no categorizado |

"..." = "El contribuyente registra falta de presentación de la". Los textos 3 y 22 son idénticos en el manual.

**Datos de prueba de homologación** (manual, 1.3; la tabla del PDF está desalineada, esta es la lectura por orden): 24219972942 → 4; 30709778079 → 5; 30636414936 → 6; 23922228094 → 7; 33692433349 → 8; 27140463618 → 23; 30684587559 → 2.

Respuesta real de `dummy` (homologación, 2026-10-02), HTTP 200:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente>homologacion-externa - FI1</ambiente><fecha>2026-10-02T15:19:21</fecha><id>wsrgiva 1.0.0-SNAPSHOT 2023-03-29T17:47:20.710Z</id></info></soap:Header><soap:Body><ns2:dummyResponse xmlns:ns2="http://impl.service.wsrgiva.afip.gov.ar/RegimenPercepcionIVAService/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse></soap:Body></soap:Envelope>
```

Todas las respuestas llevan el header `info` (`ambiente`, `fecha` sin zona, `id`), incluso `dummy`. El manual muestra `xmlns="http://headers.springbootws.factu.fisca.arca.gob.ar/xml"`; en vivo es `...fisca.afip.gob.ar/xml`.

## Errores

| Tipo | Transporte | Detalle |
|---|---|---|
| Excepcional (CUIT del representado o consultante, excepciones, formato) | SOAP Fault. Manual: `faultcode` `soap:WebServiceFault`, `faultstring` "Ocurrió un error intentando parsear el Soap Request.", `detail{codigo, descripcion}` (ejemplo: código 2, "Unmarshalling Error: For input string: \"123ABCD1234\"") | **En vivo** (2026-10-02), un `cuitContribuyente` = `123ABCD1234` devolvió el `BL<id> <fecha> 500` del WAF de `fwshomo` (HTTP/1.0 200, sin Content-Type). El Fault del manual queda oculto |
| Autenticación | `constancia{codigoError, descripcionError}`, HTTP 200 | Códigos 502 a 517 (arriba) |
| Negocio | `constancia{codigoError, descripcionError}`, HTTP 200 | 4001 Contribuyente no habilitado a realizar la consulta; 4002 Solo se admite tipo de bien 1; 4003 CUIT Inexistente; 4004 Error en la ejecución de validaciones de negocio |

En el ejemplo de 4001 del manual, `idContribuyente` es una CUIT 20000000001 y sale una sola constancia: es un error del consultante, no de cada CUIT. Para 4002 y 4003 lo lógico es una constancia por CUIT afectada (**NO VERIFICADO**).

## Comportamiento a simular

- `dummy` por SOAPAction, con header `info`.
- Header `info` en todas las respuestas: `ambiente` configurable (real: `homologacion-externa - FI1`), `fecha` local sin zona, `id` = `wsrgiva 1.0.0-SNAPSHOT 2023-03-29T17:47:20.710Z`.
- Autenticación como constancias de error, HTTP 200, `idContribuyente` = `cuitRepresentada`, una por error; con token no parseable devolver 505 y 506 juntos; con token parseable y firma mala, solo 505.
- Faults de formato: `BL... 500` del WAF.
- Padrón simulado de compradores con su leyenda; sembrar los siete CUIT de prueba del manual con sus códigos. Devolver `descripcionContribuyente` (apellido y nombre o razón social), `vigencia` (fecha límite; el plazo está **NO VERIFICADO**) y un `codigoSeguridad` determinístico (hash de consulta).
- Sin estado: cada consulta es independiente. El manual dice que la alícuota "será informada a los sujetos pasibles por medio de una comunicación digital" (efecto lateral fuera del WS).

## No verificado

- El service id de WSAA.
- El formato y algoritmo de `codigoSeguridad` y el plazo de `vigencia`.
- Si 4002 y 4003 salen por CUIT o por lote.
- El Fault real (oculto por el WAF).
- Qué hace la constancia con `descripcionLeyenda` para códigos con "período/s: XXXX" (si reemplaza XXXX por los períodos).
