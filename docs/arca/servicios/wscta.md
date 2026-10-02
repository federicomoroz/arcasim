# wscta

Web Service de Certificados de Transferencia de Automotores (CTA, opción del formulario F.381). Lo usa la **DNRPA** (Registro de la Propiedad del Automotor): sus funcionarios consultan el PDF del certificado que el vendedor tramitó en ARCA y lo **aprueban o rechazan** desde el registro seccional. Solo funcionarios habilitados por el Administrador de Relaciones.

Fuentes:

- Manual "Web Service de Certificados de Transferencia de Automotores (wscta)", sin versión ni fecha (el ejemplo de error es de diciembre de 2008): `https://www.afip.gob.ar/ws/WSCTA/WSCTA-ManualParaElDesarrollador.pdf`. Incluye un WSDL completo en el anexo.
- WSDL de **producción**, descargado el 2026-10-02. Homologación no existe.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, Apache Axis2 (port `...HttpSoap11Endpoint`; un `GET` al endpoint responde "The endpoint reference (EPR) for the Operation not found is /wscta/services/CertificadoDNRPAService and the WSA Action = null") |
| SOAP | 1.1, document/literal |
| Endpoint homologación | Manual: `https://fwshomo.afip.gov.ar/wscta/services/CertificadoDNRPAService`. **404** el 2026-10-02 (también `?wsdl`, `/wscta/` y `/wscta/services/`) |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wscta/services/CertificadoDNRPAService`. No figura en el manual, pero **responde** (HTTP 200 con WSDL; el catálogo lo daba como no publicado) |
| WSDL | `?wsdl` |
| targetNamespace | `http://impl.service.cta.afip.gov.ar/CertificadoDNRPAService/` (sin `elementFormDefault`: hijos sin namespace) |
| portType / binding / service | `CertificadoDNRPAServicePortType` / `CertificadoDNRPAServiceSoap11Binding` / `CertificadoDNRPAService`, port `CertificadoDNRPAServiceHttpSoap11Endpoint` |
| SOAPAction | `http://impl.service.cta.afip.gov.ar/CertificadoDNRPAService/<operación>` |
| Archivo guardado | `wsdl/wscta-produccion.wsdl` (sin imports). No hay archivo de homologación |
| WSAA service id | **No indicado** en el manual. Probablemente `wscta`: **NO VERIFICADO** |
| Versión | No expone versión |

**El WSDL real tiene más que el manual**: 6 operaciones contra 4 (`dummy`, `getCertificadoPDF`, `aprobarCertificado`, `rechazarCertificado`). Las nuevas son `consultarEstadoCertificado` y `getListCertificatesByDomain`. Además, el request de esta última se llama `CertificatesByDomain`, no `getListCertificatesByDomain`.

**Inconsistencias del manual.** El texto de `getCertificadoPDF` lo llama `getDocumentoPDF`; el campo de CUIT aparece como `cuitRepresentando`, `cuitRepresentado` y `cuitRepresentante` según el ejemplo. El WSDL (manual y real) dice siempre `cuitRepresentado`.

## Autenticación

Campos sueltos dentro del request, sin wrapper:

```xml
<cer:getCertificadoPDF xmlns:cer="http://impl.service.cta.afip.gov.ar/CertificadoDNRPAService/">
  <token>...</token>
  <sign>...</sign>
  <nroCertificado>12345678901234</nroCertificado>
  <cuitRepresentado>20111111112</cuitRepresentado>
  <nroDominio>ABC123</nroDominio>
</cer:getCertificadoPDF>
```

`cuitRepresentado` (long): CUIT o CUIL del funcionario del DNRPA. Tiene que estar habilitado en el Administrador de Relaciones; si no, SOAP Fault.

Errores de autenticación: SOAP Fault. Respuesta real con `token` = `abc` (**producción**, 2026-10-02), HTTP 500, `Content-Type: text/xml;charset=utf-8`:

```xml
<?xml version='1.0' encoding='utf-8'?><soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"><soapenv:Body><soapenv:Fault><faultcode>soapenv:Client</faultcode><faultstring>Token inválido</faultstring><detail /></soapenv:Fault></soapenv:Body></soapenv:Envelope>
```

Ejemplo del manual (token vencido), con `faultcode` `soapenv:Server`:

```
Token vencido Fecha y Hora de Vencimiento del Token Enviado: 26-09-2008 00:32:37 - Fecha y Hora Actual del Servidor: 05-12-2008 13:49:41
```

(El salto de línea del PDF dentro del `faultstring` es del documento, no del mensaje.)

Headers HTTP reales: `Strict-Transport-Security: max-age=300; includeSubDomains; preload`, `X-XSS-Protection: 1; mode=block`, `X-Frame-Options: sameorigin`, `X-Content-Type-Options: nosniff`, `Vary: Accept-Encoding`, `Transfer-Encoding: chunked`. En `serviciosjava` el Fault llega completo (no hay reemplazo `BL... 500` como en `fwshomo`).

## Operaciones

6 operaciones en el WSDL real. Todas declaran el fault `Exception{Exception: string?}`.

| Operación | Propósito | Entrada (además de `token`, `sign`) | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Infraestructura | Body vacío (mensaje sin partes); despacho por SOAPAction | `dummyResponse/return{appserver, authserver, dbserver}` | — |
| `getCertificadoPDF` | PDF del certificado | `nroCertificado` (long, 14 dígitos), `cuitRepresentado`, `nroDominio` (string, 6) | `getCertificadoPDFResponse/return{documentoPDF (base64), idLog (long)}` | Consulta, pero **registra un log** (`idLog`). Clave: `nroCertificado` + `nroDominio` |
| `aprobarCertificado` | El registro seccional aprueba | `nroCertificado`, `cuitRepresentado`, `registroSeccionalDNRPA` (string, 50) | `aprobarCertificadoResponse{idLog}` | **Cambia estado** a Aprobado |
| `rechazarCertificado` | El registro seccional rechaza | Igual | `rechazarCertificadoResponse{idLog}` | **Cambia estado** a Rechazado |
| `consultarEstadoCertificado` | Estado de un certificado | `nroCertificado`, `cuitRepresentado`, `nroDominio` | `return{idLog, estado, fechaAprobacion?, fechaRechazo?, registroSeccionalDNRPA?, documentoPDF}` | Consulta. **Solo en el WSDL** |
| `getListCertificatesByDomain` | Certificados de un dominio (patente) | Elemento `CertificatesByDomain{token, sign, cuitRepresentado, nroDominio}` | `getListCertificatesByDomainResponse{return[1..n]{idLog, nroDominio, cuitSolicitante, nroTramite, estado, fechaAprobacion?, fechaRechazo?, registroSeccionalDNRPA?, documentoPDF}}` | Consulta. **Solo en el WSDL** |

Reglas de estado (manual):

- Solo se opera sobre certificados **activos** (no anulados por el contribuyente). Certificado inexistente o no activo: SOAP Fault.
- **Aprobar**: si está Pendiente; o si está Rechazado, el rechazo no tiene más de 5 días y no tuvo más de 3 cambios de estado.
- **Rechazar**: si está Pendiente; o si está Aprobado, la aprobación no tiene más de 5 días y no tuvo más de 3 cambios de estado.
- Cualquier otro caso: SOAP Fault.

Respuesta real de `dummy` (**producción**, 2026-10-02), HTTP 200:

```xml
<?xml version='1.0' encoding='utf-8'?><soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"><soapenv:Body><ns1:dummyResponse xmlns:ns1="http://impl.service.cta.afip.gov.ar/CertificadoDNRPAService/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns1:dummyResponse></soapenv:Body></soapenv:Envelope>
```

## Errores

Solo SOAP Fault, con HTTP 500, `faultstring` descriptivo y `<detail />` vacío ("Los errores son descriptivos", manual 1.3). No hay tabla de códigos.

| Caso | `faultcode` | `faultstring` | Fuente |
|---|---|---|---|
| Token no parseable | `soapenv:Client` | `Token inválido` | En vivo, producción |
| Token vencido | `soapenv:Server` | `Token vencido Fecha y Hora de Vencimiento del Token Enviado: dd-MM-yyyy HH:mm:ss - Fecha y Hora Actual del Servidor: dd-MM-yyyy HH:mm:ss` | Manual |
| Certificado inexistente, no activo, transición inválida, funcionario no habilitado | — | **NO VERIFICADO** | Manual (solo dice "SOAP Fault") |

## Comportamiento a simular

- Axis2: `dummy` por SOAPAction con body vacío; `GET` al endpoint devuelve el mensaje de EPR.
- Auth por Fault (`Token inválido`, `Token vencido ...`).
- Estado: certificados con `nroCertificado` (14 dígitos), `nroDominio`, `cuitSolicitante`, `nroTramite`, PDF, estado (Pendiente, Aprobado, Rechazado, Anulado), fechas de aprobación y rechazo, registro seccional, cantidad de cambios de estado e historial de logs (`idLog` secuencial por cada operación).
- Transiciones con la regla de 5 días y 3 cambios. Hook de prueba para que el "contribuyente" anule o cree certificados (fuera del WS).
- Pares: `aprobarCertificado` → `consultarEstadoCertificado` muestra Aprobado con `fechaAprobacion`; `rechazarCertificado` dentro de 5 días → Rechazado.
- Valores de `estado` en las respuestas: **NO VERIFICADOS** (el WSDL dice string).

## No verificado

- Si existe un ambiente de homologación en otra URL.
- El service id de WSAA.
- Los textos de los Fault de negocio y los valores literales de `estado`.
- Formato de `fechaAprobacion` y `fechaRechazo`.
- Si `getCertificadoPDF` genera un log en cada consulta (el `idLog` sugiere que sí).
