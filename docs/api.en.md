# ArcaSim · API reference

ArcaSim serves ARCA's web services (ARCA is Argentina's tax authority) on the same paths, with the same WSDL files and the same protocol, and adds its own API to set up test scenarios. This document is for integrating it into any project, in any language.

**[Versión en español](api.md)** · [README](../README.en.md) · [Study of ARCA's API (Spanish)](arca/)

> ArcaSim is not related to ARCA. The CAEs it grants have no tax validity and its access tickets only work against ArcaSim.

## Contents

1. [Environments and addresses](#1-environments-and-addresses)
2. [Quick start](#2-quick-start)
3. [Protocol conventions](#3-protocol-conventions)
4. [WSAA: authentication](#4-wsaa-authentication)
5. [WSFEv1: electronic invoicing](#5-wsfev1-electronic-invoicing)
6. [The rest of ARCA's services](#6-the-rest-of-arcas-services)
7. [Errors](#7-errors)
8. [Admin API](#8-admin-api)
9. [Test scenarios](#9-test-scenarios)
10. [Integrating it into a project](#10-integrating-it-into-a-project)
11. [Moving to ARCA](#11-moving-to-arca)
12. [What is not the same as ARCA](#12-what-is-not-the-same-as-arca)

---

## 1. Environments and addresses

| | WSAA (access ticket) | WSFEv1 (electronic invoicing) |
|---|---|---|
| **ArcaSim** | `http(s)://<host>/ws/services/LoginCms` | `http(s)://<host>/wsfev1/service.asmx` |
| ARCA homologación (test) | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx` |
| ARCA production | `https://wsaa.afip.gov.ar/ws/services/LoginCms` | `https://servicios1.afip.gov.ar/wsfev1/service.asmx` |

| Resource | Method and path |
|---|---|
| WSAA's WSDL | `GET /ws/services/LoginCms?wsdl` |
| WSFEv1's WSDL | `GET /wsfev1/service.asmx?WSDL` |
| Panel | `GET /arcasim/` |
| The rest of the services | The same path as in ARCA, with its WSDL at `?wsdl` ([§6](#6-the-rest-of-arcas-services)) |
| Admin API | `/arcasim/api/...` ([§8](#8-admin-api)) |

The WSDL files are ARCA's own: only the service address changes to ArcaSim's.

## 2. Quick start

**1. Start it.**

```bash
docker run -d -p 7080:8080 -v arcasim-data:/data ghcr.io/federicomoroz/arcasim   # http://localhost:7080
```

In memory: it starts empty and goes away with the container. The volume keeps its certification authority and the ticket key, so a saved ticket still works after a restart. With PostgreSQL, `docker compose up -d` from the repository; without Docker, `dotnet run --project src/ArcaSim.Api --urls http://localhost:7080`.

**2. A certificate.** By default ArcaSim starts with **open access**: it accepts any certificate with a CUIT in its DN. The one WSASS issued for homologación works, and so does a self-signed one:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 365 -keyout key.pem -out cert.pem \
  -subj "/CN=my-app/serialNumber=CUIT 20111111112"
```

**3. Point the application at it.** The two URLs of [§1](#1-environments-and-addresses) and that certificate. Nothing has to be loaded first: the taxpayer and the point of sale are created the first time they are used.

[`docs/ejemplos/curl.sh`](ejemplos/curl.sh) walks the whole path with openssl and curl, no library at all: it signs the access request, gets the ticket, asks for the last number and requests the CAE of an invoice B.

```bash
ARCASIM=http://localhost:7080 CUIT=20111111112 bash docs/ejemplos/curl.sh
```

## 3. Protocol conventions

| | WSAA | WSFEv1 |
|---|---|---|
| Server it imitates | Apache Axis 1.4 | ASP.NET ASMX |
| SOAP | 1.1 and 1.2 | 1.1 and 1.2 |
| Message namespace | `http://wsaa.view.sua.dvadac.desein.afip.gov` | `http://ar.gov.afip.dif.FEV1/` |
| `SOAPAction` (SOAP 1.1) | Required, any value (`""` works) | `http://ar.gov.afip.dif.FEV1/<Operation>`; without it, routed by the Body element |
| `Content-Type` | `text/xml; charset=utf-8` (1.1) · `application/soap+xml; charset=utf-8; action="…"` (1.2) | same |

**HTTP status codes**, as in ARCA:

| Code | When |
|---|---|
| 200 | A business answer, even one with errors: in WSFEv1 errors go inside `Errors` |
| 500 | SOAP fault: every WSAA error; in WSFEv1, an unknown `SOAPAction` or a value that cannot be read (text in a numeric field) |
| 400 | Malformed XML, empty body |
| 503 | Service saturated or down ([§9](#9-test-scenarios)): worth retrying |

**WSFEv1 answer format**: one line, with the `FEHeaderInfo` header (environment, date with a `-03:00` offset and version); empty strings as `<CAE />`; amounts without trailing zeros (`122`, not `122.00`); dates as `yyyyMMdd` and processing moments as `yyyyMMddHHmmss`, in Argentina's time.

**Reading requests**, also like ASMX: elements may come in any order, unknown ones are ignored, so are those without the namespace, and a missing mandatory integer counts as 0.

## 4. WSAA: authentication

### `loginCms`

`POST /ws/services/LoginCms`

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsaa="http://wsaa.view.sua.dvadac.desein.afip.gov">
  <soapenv:Body>
    <wsaa:loginCms><wsaa:in0>{base64 CMS}</wsaa:in0></wsaa:loginCms>
  </soapenv:Body>
</soapenv:Envelope>
```

`in0` is a base64 CMS **SignedData** with the access request (TRA) inside (not detached) and the signer's certificate included.

**The TRA:**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<loginTicketRequest version="1.0">
  <header>
    <uniqueId>1727780000</uniqueId>                         <!-- unsigned integer -->
    <generationTime>2026-10-02T10:00:00-03:00</generationTime> <!-- ≤ now, > now − 24 h -->
    <expirationTime>2026-10-02T10:20:00-03:00</expirationTime> <!-- > now, < now + 24 h -->
  </header>
  <service>wsfe</service>
</loginTicketRequest>
```

`source` and `destination` in the header are optional; when present they are checked against the certificate's DN and WSAA's.

**Answer**: `loginCmsReturn` carries the access ticket (TA) as escaped text:

```xml
<loginTicketResponse version="1">
    <header>
        <source>CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239</source>
        <destination>SERIALNUMBER=CUIT 20111111112, CN=my-app</destination>
        <uniqueId>…</uniqueId>
        <generationTime>2026-10-02T10:00:01.123-03:00</generationTime>
        <expirationTime>2026-10-02T22:00:01.123-03:00</expirationTime>
    </header>
    <credentials>
        <token>{base64}</token>
        <sign>{base64}</sign>
    </credentials>
</loginTicketResponse>
```

- The ticket lasts **12 hours**. It is reused on every WSFEv1 call inside the `Auth` block.
- **Anti-replay window**: while a certificate's last ticket for a service is still valid, a new request within 10 minutes (2 with the production profile) gets `coe.alreadyAuthenticated`. Applications have to keep their ticket, as with ARCA. It can be switched off from the [admin API](#8-admin-api).
- The `token` is a readable base64 `sso` XML: it carries `exp_time` and the relations (the CUITs the certificate can act for).

**Services** `<service>` accepts: `wsfe` and the WSAA id of every service in [§6](#6-the-rest-of-arcas-services) (`wsmtxca`, `wsfex`, `wscdc`, `ws_sr_padron_a13`, `seti-setipago-api`…). An id ArcaSim does not know gets `wsn.notFound`, as in ARCA.

**Faults**, in the order they are checked:

| `faultcode` | `faultstring` |
|---|---|
| `ns1:cms.bad.base64` | No se puede decodificar el BASE64 |
| `ns1:cms.bad` | El CMS no es valido |
| `ns1:cms.cert.notFound` | No se ha encontrado certificado de firmador |
| `ns1:cms.sign.invalid` | Firma inválida o algoritmo no soportado |
| `ns1:cms.cert.invalid` | Certificado con fecha de generacion posterior a la actual |
| `ns1:cms.cert.expired` | Certificado expirado |
| `ns1:cms.cert.untrusted` | Certificado no emitido por AC de confianza |
| `ns1:xml.bad` | No se ha podido interpretar el XML contra el SCHEMA |
| `ns1:xml.version.notSupported` | La versión del documento no es soportada |
| `ns1:xml.source.invalid` | El atributo 'source' no se corresponde con el DN del Certificado |
| `ns1:xml.destination.invalid` | El atributo 'destination' no se corresponde con el DN del WSAA |
| `ns1:xml.generationTime.invalid` | generationTime posee formato o dato inválido (…) |
| `ns1:xml.expirationTime.expired` | El tiempo de expiración es inferior a la hora actual |
| `ns1:xml.expirationTime.invalid` | El tiempo de expiración del documento es superior a 24 horas |
| `ns1:wsn.notFound` | Servicio informado inexistente |
| `ns1:coe.notAuthorized` | Computador no autorizado a acceder al servicio |
| `ns1:coe.alreadyAuthenticated` | El CEE ya posee un TA valido para el acceso al WSN solicitado |
| `ns1:wsn.unavailable` | El servicio al que se desea acceder se encuentra momentáneamente fuera de servicio |
| `ns1:wsaa.unavailable` | El servicio de autenticación/autorización se encuentra momentáneamente fuera de servicio |

All go out with HTTP 500, the `ns1` prefix bound to `http://xml.apache.org/axis/`, and non-ASCII characters written as references (`inv&#xE1;lida`), as Axis writes them. The texts are ARCA's, in Spanish.

## 5. WSFEv1: electronic invoicing

`POST /wsfev1/service.asmx`, `SOAPAction: "http://ar.gov.afip.dif.FEV1/<Operation>"`.

Every operation but `FEDummy` takes the `Auth` block:

```xml
<ar:Auth>
  <ar:Token>{ticket token}</ar:Token>
  <ar:Sign>{ticket sign}</ar:Sign>
  <ar:Cuit>20111111112</ar:Cuit>   <!-- the invoicing CUIT: it has to be among the ticket's relations -->
</ar:Auth>
```

### The 22 operations

| Operation | What for | Parameters (besides `Auth`) | Result |
|---|---|---|---|
| `FEDummy` | Is the service up | — | `AppServer`, `DbServer`, `AuthServer` (`OK`) |
| `FECompUltimoAutorizado` | Last number authorized | `PtoVta`, `CbteTipo` | `PtoVta`, `CbteTipo`, `CbteNro` (0 when there is none) |
| `FECAESolicitar` | CAE for one voucher or a batch | `FeCAEReq` ([detail](#fecaesolicitar)) | `FeCabResp`, `FeDetResp`, `Errors` |
| `FECompConsultar` | An issued voucher, with everything sent | `FeCompConsReq`: `CbteTipo`, `CbteNro`, `PtoVta` | `ResultGet` with the voucher, `CodAutorizacion`, `EmisionTipo` (CAE or CAEA), `FchVto`, `FchProceso`, `Observaciones` |
| `FECompTotXRequest` | How many vouchers fit in a request | — | `RegXReq` (250) |
| `FECAEASolicitar` | A CAEA for a fortnight | `Periodo` (`yyyyMM`), `Orden` (1 or 2) | `ResultGet`: `CAEA`, `FchVigDesde`, `FchVigHasta`, `FchTopeInf`, `FchProceso` |
| `FECAEAConsultar` | A fortnight's CAEA | `Periodo`, `Orden` | Same as `FECAEASolicitar` |
| `FECAEARegInformativo` | Report vouchers issued under a CAEA | `FeCAEARegInfReq`: like `FeCAEReq`, each detail with `CAEA` and `CbteFchHsGen` | `FeCabResp`, `FeDetResp` with the `CAEA` |
| `FECAEASinMovimientoInformar` | A point of sale that did not use the CAEA | `PtoVta`, `CAEA` | `CAEA`, `FchProceso`, `PtoVta`, `Resultado` |
| `FECAEASinMovimientoConsultar` | Those reported without movement | `CAEA`, `PtoVta` (0 = all) | `ResultGet`: list of `FECAEASinMov` |
| `FEParamGetTiposCbte` | Voucher types | — | `ResultGet`: `CbteTipo` (`Id`, `Desc`, `FchDesde`, `FchHasta`) |
| `FEParamGetTiposConcepto` | Concepts (products, services) | — | `ConceptoTipo` |
| `FEParamGetTiposDoc` | Document types | — | `DocTipo` |
| `FEParamGetTiposIva` | VAT rates | — | `IvaTipo` |
| `FEParamGetTiposMonedas` | Currencies | — | `Moneda` |
| `FEParamGetTiposOpcional` | Optional data by regulation | — | `OpcionalTipo` |
| `FEParamGetTiposTributos` | Other taxes | — | `TributoTipo` |
| `FEParamGetTiposPaises` | Countries | — | `PaisTipo` |
| `FEParamGetCondicionIvaReceptor` | Receiver VAT conditions | optional `ClaseCmp` (`A`, `ALEY`, `B`, `C`, `49`) | `CondicionIvaReceptor` (`Id`, `Desc`, `Cmp_Clase`) |
| `FEParamGetPtosVenta` | The issuer's points of sale | — | `PtoVenta` (`Nro`, `EmisionTipo`, `Bloqueado`, `FchBaja`) |
| `FEParamGetCotizacion` | A currency's exchange rate | `MonId`, optional `FchCotiz` | `MonId`, `MonCotiz`, `FchCotiz` |
| `FEParamGetActividades` | The issuer's activities | — | `602` (ArcaSim keeps no activities per taxpayer) |

The parameter tables are the ones ARCA's homologación returned, with their texts and the literal `NULL` for dates without a value.

### FECAESolicitar

```xml
<ar:FECAESolicitar>
  <ar:Auth>…</ar:Auth>
  <ar:FeCAEReq>
    <ar:FeCabReq>
      <ar:CantReg>1</ar:CantReg>      <!-- number of FECAEDetRequest -->
      <ar:PtoVta>1</ar:PtoVta>
      <ar:CbteTipo>6</ar:CbteTipo>    <!-- 1 Invoice A, 6 Invoice B, 11 Invoice C… -->
    </ar:FeCabReq>
    <ar:FeDetReq>
      <ar:FECAEDetRequest>
        <ar:Concepto>1</ar:Concepto>           <!-- 1 products, 2 services, 3 both -->
        <ar:DocTipo>99</ar:DocTipo>            <!-- 80 CUIT, 96 DNI, 99 final consumer -->
        <ar:DocNro>0</ar:DocNro>
        <ar:CbteDesde>1</ar:CbteDesde>         <!-- last authorized + 1 -->
        <ar:CbteHasta>1</ar:CbteHasta>
        <ar:CbteFch>20261002</ar:CbteFch>      <!-- optional: without it, the day of the request -->
        <ar:ImpTotal>1210</ar:ImpTotal>
        <ar:ImpTotConc>0</ar:ImpTotConc>       <!-- not taxed -->
        <ar:ImpNeto>1000</ar:ImpNeto>          <!-- taxed net -->
        <ar:ImpOpEx>0</ar:ImpOpEx>             <!-- exempt -->
        <ar:ImpTrib>0</ar:ImpTrib>             <!-- sum of Tributos -->
        <ar:ImpIVA>210</ar:ImpIVA>             <!-- sum of Iva -->
        <ar:MonId>PES</ar:MonId>
        <ar:MonCotiz>1</ar:MonCotiz>
        <ar:CondicionIVAReceptorId>5</ar:CondicionIVAReceptorId>
        <ar:Iva>
          <ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>1000</ar:BaseImp><ar:Importe>210</ar:Importe></ar:AlicIva>
        </ar:Iva>
      </ar:FECAEDetRequest>
    </ar:FeDetReq>
  </ar:FeCAEReq>
</ar:FECAESolicitar>
```

Other detail fields, all optional: `FchServDesde`, `FchServHasta`, `FchVtoPago` (required with concept 2 or 3), `CanMisMonExt`, `CbtesAsoc` (the vouchers a credit or debit note refers to), `Tributos`, `Opcionales`, `Compradores`, `PeriodoAsoc`, `Actividades`. The exact order and types are in the WSDL.

**Approved answer:**

```xml
<FECAESolicitarResult>
  <FeCabResp><Cuit>20111111112</Cuit><PtoVta>1</PtoVta><CbteTipo>6</CbteTipo>
    <FchProceso>20261002100001</FchProceso><CantReg>1</CantReg><Resultado>A</Resultado><Reproceso>N</Reproceso></FeCabResp>
  <FeDetResp><FECAEDetResponse>
    <Concepto>1</Concepto><DocTipo>99</DocTipo><DocNro>0</DocNro><CbteDesde>1</CbteDesde><CbteHasta>1</CbteHasta>
    <CbteFch>20261002</CbteFch><Resultado>A</Resultado><CAE>71263951827464</CAE><CAEFchVto>20261012</CAEFchVto>
  </FECAEDetResponse></FeDetResp>
</FECAESolicitarResult>
```

(In the real answer, all on one line.)

**Rules worth knowing:**

- **Numbering** runs per CUIT, point of sale and voucher type. The next one is `FECompUltimoAutorizado` + 1.
- **Not idempotent.** Sending an approved voucher again answers error 10016. If an answer gets lost, the right move is to look the voucher up with `FECompConsultar` before retrying.
- **Batches** are processed in order. The first rejection stops the batch and the rest come back unprocessed (`R`, no observations). The header says `A`, `R` or `P` (partial).
- **Where each problem goes:** authentication, header and numbering problems in `Errors`; the voucher's validations in the detail's `Observaciones`. A rejecting observation leaves the voucher at `R`; a non-rejecting one leaves it at `A` with its CAE.
- **Dates:** with concept 1, `CbteFch` between N−5 and N+5 (N is the day of the request); with 2 or 3, between N−10 and N+10.
- **Amounts:** `ImpTotal` = `ImpTotConc` + `ImpNeto` + `ImpOpEx` + `ImpTrib` + `ImpIVA` (class C: `ImpNeto` + `ImpTrib`), with the manual's margin of error: 0.01 per added line or 0.01 %.
- **Classes:** A and "A con leyenda" go to a CUIT (`DocTipo` 80); C has no `Iva`; final consumers from $ 10,000,000 must be identified.
- **Receiver VAT condition:** without it, observation 10245 until 30/11/2026 and rejection 10246 from 01/12/2026, by ArcaSim's clock.
- **CAE:** 14 digits; it expires 10 days after the voucher's date.

### CAEA

1. `FECAEASolicitar` within the fortnight or up to 5 days before it starts (outside that window, 15006).
2. Vouchers are issued offline with that CAEA.
3. `FECAEARegInformativo` reports each voucher, with `CAEA` and `CbteFchHsGen` (`yyyyMMddHHmmss`).
4. `FECAEASinMovimientoInformar` for each CAEA point of sale that did not use it.

## 6. The rest of ARCA's services

Besides WSAA and WSFEv1, ArcaSim answers 50 more ARCA web services, with 613 operations: special invoicing, the taxpayer registry, agriculture and delivery notes, government agencies, customs and VEP payments. Of the 53 current services in ARCA's catalog it covers 52 (`sud_contrataciones` is the same service as `sud_restricciones`); the missing one, the A.P.E. revocation automation, has no published manual, WSDL or address.

**How they answer.** Each one on the same path as in ARCA, with its official WSDL (`GET <path>?wsdl`) and in its server's dialect: .NET ASMX, Apache Axis2, Apache CXF, JAX-WS or Spring-WS, with their prefixes, headers and faults. The ticket is checked as in WSFEv1, but each service refuses it with its own codes and texts (for example 7004 to 7014 in the customs services, `[wscommon_002]` in the JAX-WS ones, an `SRValidationException` fault in the registry). The certificate must be authorized for the service's WSAA id; with open access, any certificate is.

**What they simulate.** Each one applies its manual's rules over what ArcaSim keeps: they number "last + 1", grant CAE, CAEA, COE or CTG codes with their expiry, walk the states the manual documents (accept, reject, cancel, adjust, confirm), answer queries with what was issued and refuse with the manual's codes and texts. Operations the manual does not document well enough answer the contract with data valid for the WSDL. Every choice ArcaSim makes where the manual falls short is written in the doc comment of the class that simulates the service, and each service's research is in [`arca/servicios/`](arca/servicios/) (in Spanish).

**Vouchers that can be verified.** What any invoicing service authorizes (WSFEv1, WSMTXCA, WSFEXv1, WSCT, WSBFE, WSSEG and the agriculture settlements) is recorded, and WSCDC verifies it as ARCA does: approved when the data matches, with observations when it does not.

**Registries nobody writes through the API.** SUD's debts, the apócrifos, the Ventanilla Electrónica inbox, WSAGR's ratings or customs declarations have no operation to load them: ArcaSim fills them with fictitious data the first time they are queried. To test with your own data, load it beforehand through the admin API ([§8](#8-admin-api)):

```bash
curl -X PUT http://localhost:7080/arcasim/api/documents/sud_restricciones.deudas/30000000007/301 \
     -H 'Content-Type: application/json' -d '{ ... }'
curl http://localhost:7080/arcasim/api/documents/sud_restricciones.deudas   # what is there, and its shape
```

Each service keeps its documents in collections named after it (`wsagr`, `veconsumerws.comunicaciones`, `wscpe`…); `GET /documents/{collection}` shows what it stored and in which shape.

**Invoicing**

| Service | Path | WSAA id | Operations |
|---|---|---|---:|
| Factura con detalle de ítems (WSMTXCA) | `/wsmtxca/services/MTXCAService` | `wsmtxca` | 27 |
| Factura de exportación (WSFEXv1) | `/wsfexv1/service.asmx` | `wsfex` | 19 |
| Bonos fiscales electrónicos (WSBFEv1) | `/wsbfev1/service.asmx` | `wsbfe` | 15 |
| Bonos fiscales electrónicos, versión anterior (WSBFE) | `/wsbfe/service.asmx` | `wsbfe` | 14 |
| Comprobantes T, turismo (WSCT) | `/wsct/CTService` | `wsct` | 22 |
| Seguros de caución (WSSEG) | `/wsseg/service.asmx` | `wsseg` | 11 |
| Constatación de comprobantes (WSCDC) | `/WSCDC/service.asmx` | `wscdc` | 6 |
| Factura de Crédito Electrónica MiPyMEs (WSFECRED) | `/wsfecred/FECredService` | `wsfecred` | 21 |
| FCE MiPyMEs, agentes de depósito colectivo | `/wsfecredagente/FECredAgenteService/` | `wsfecredagente` | 8 |
| FCE MiPyMEs, Sistema de Circulación Abierta | `/wsfecredsca/FECredSCAService/` | `wsfecredsca` | 3 |

**Taxpayer registry**

| Service | Path | WSAA id | Operations |
|---|---|---|---:|
| Constancia de inscripción (A5) | `/sr-padron/webservices/personaServiceA5` | `ws_sr_constancia_inscripcion`, `ws_sr_padron_a5` | 5 |
| Padrón A13 | `/sr-padron/webservices/personaServiceA13` | `ws_sr_padron_a13` | 4 |
| Padrón A4: situación tributaria | `/sr-padron/webservices/personaServiceA4` | `ws_sr_padron_a4` | 2 |
| Padrón A10: datos resumidos | `/sr-padron/webservices/personaServiceA10` | `ws_sr_padron_a10` | 2 |
| Padrón A100: tablas de parámetros | `/sr-parametros/webservices/parameterServiceA100` | `ws_sr_padron_a100` | 2 |

**Agriculture and delivery notes**

| Service | Path | WSAA id | Operations |
|---|---|---|---:|
| Carta de Porte Electrónica (WSCPE) | `/wscpe/services/soap` | `wscpe` | 75 |
| Liquidación primaria de granos (WSLPG) | `/wslpg/LpgService` | `wslpg` | 48 |
| Liquidación del sector pecuario (WSLSP) | `/wslsp/LspService` | `wslsp` | 23 |
| Liquidación única mensual de lechería (WSLUM) | `/wslum/LumService` | `wslum` | 10 |
| Liquidación de tabaco verde (WSLTV) | `/wsltv/LtvService` | `wsltv` | 15 |
| Liquidación de caña de azúcar (WSLCA) | `/wslca/services/soap` | `wslca` | 14 |
| Régimen tabacalero (WSTABACO) | `/wstabaco/TabacoService` | `wstabaco` | 29 |
| Remito electrónico de harinas (WSREMHARINA) | `/wsremharina/RemHarinaService` | `wsremharina` | 29 |
| Remito electrónico cárnico (WSREMCARNE) | `/wsremcarne/RemCarneService` | `wsremcarne` | 29 |
| Remito electrónico de azúcar (WSREMAZUCAR) | `/wsremazucar/RemAzucarService` | `wsremazucar` | 27 |
| Registro AGR / Reproweb (WSAGR) | `/wsagr/wsagr.asmx` | `wsagr` | 8 |

**Government agencies**

| Service | Path | WSAA id | Operations |
|---|---|---|---:|
| Economía del Conocimiento (WSCEC) | `/wscec/CECService/` | `wscec` | 5 |
| Contribuyentes y facturas apócrifas (WSAPOC) | `/Service.asmx` | `wsapoc` | 4 |
| Deuda de proveedores y clientes (SUD) | `/sud_restricciones` | `sud_restricciones` | 3 |
| Ventanilla Electrónica (WSCCOMU) | `/ve-ws/services/veconsumer` | `veconsumerws` | 5 |
| Juegos de azar (WSJAZA) | `/wsjaza/JAZAService` | `wsjaza` | 11 |
| Mi Argentina: vida laboral | `/miargentina-ws/servicios.asmx` | `miargentina-ws` | 2 |
| Presentación de declaraciones juradas | `/setiws/webservices/uploadPresentacionService` | `presentacionprocessor`, `djprocessorcontribuyente`, `djprocessorcontribuyente_cf` | 3 |
| Régimen de percepción de IVA (WSRGIVA) | `/wsrgiva/services/RegimenPercepcionIVAService` | `wsrgiva` | 2 |
| Beneficios en créditos y débitos bancarios (WSICDB) | `/wsicdb/IcdbService` | `wsicdb` | 8 |
| Retenciones electrónicas, certificado F2005 (SIRE) | `/sire/ws/v1/c2005/2005` | `sire-ws` | 3 |
| Seguimiento vehicular (WSSV) | `/wssv/service.asmx` | `wssv` | 7 |

**Customs**

| Service | Path | WSAA id | Operations |
|---|---|---|---:|
| Aduana: dispositivos PEMA | `/dia/ws/WDiaUtiDES/WDiaUtiDES.asmx` | `wdiautides`, `WDiaUtiDES` | 8 |
| Aduana: despachos de vitivinicultura (INV) | `/Dia/Ws/WGesINV/WGesINV.asmx` | `wgesinv`, `WGesINV` | 7 |
| Aduana: tablas de referencia | `/Dia/ws/wgesTabRef/wgesTabRef.asmx` | `wgestabref`, `WDiaUtiDES`, `wdiautides` | 13 |
| Aduana: legajos de depositario fiel | `/Dia/Ws/wConsDepFiel/wConsDepFiel.asmx` | `wconsdepfiel`, `wConsDepFiel` | 3 |
| Aduana: tiendas libres | `/diav2/wgestiendaslibres/wgestiendaslibres.asmx` | `wgestiendaslibres` | 17 |
| Aduana: precintos de depositario fiscal | `/Dia/Ws/wgesprecintosdepfis/wgesprecintosdepfis.asmx` | `wgesprecintosdepfis` | 8 |
| Aduana: digitalización de legajos | `/Dia/Ws/wDigDepFiel/wDigDepFiel.asmx` | `wdigdepfiel`, `wDigDepFiel` | 3 |
| Aduana: declaraciones de grandes operadores | `/Dia/Ws/WutiGOPDeclaraciones/WutiGOPDeclaraciones.asmx` | `wutigopdeclaraciones` | 10 |
| Aduana: movimientos de terminales y depósitos | `/dia/ws/wdepMovimientos/wdepMovimientos.asmx` | `wdepmovimientos`, `wDepMovimientos` | 6 |
| Aduana: entradas y salidas desde Chile | `/DIA/WS/wEnysa/wEnysa.asmx` | `wenysa`, `wEnysa` | 6 |

**Government agencies**

| Service | Path | WSAA id | Operations |
|---|---|---|---:|
| Certificados de transferencia de automotores (WSCTA) | `/wscta/services/CertificadoDNRPAService` | `wscta` | 6 |
| Consulta de F931 para el MTEySS (SSF931) | `/WebService/F931.asmx` | `trabajo_f931`, `ssf931` | 2 |

### SETIWS-PAGO-API (REST)

The one ARCA service that is not SOAP: a government agency creates VEPs and checks their payment. In ARCA it lives at the root of its own domain; in ArcaSim, under `/setiws-pago-api/`.

| Method and path | What it does |
|---|---|
| `GET /setiws-pago-api/dummy` | `{"appserver":"OK","dbserver":"OK"}`, without authentication |
| `POST /setiws-pago-api/api/v1/veps[?with-qr=true]` | Creates the VEP: `{"entidadDePago": 1001, "vep": {...}}`. Answers 201 with `nroVEP` and `fechaExpiracion` (today + 25 days). The same `ownerCuit` + `ownerTransactionId` returns the same VEP |
| `GET /setiws-pago-api/api/v1/veps?owner-cuit=&nro-vep=` (or `&owner-transaction-id=`) | `{"VEP": {...}, "CP": {...}}`; while pending, without `CP` and with the QR or the payment entity's URL |

Authentication travels in HTTP headers, not in the body: `WSAA-AUTH-PROXY-TOKEN` and `WSAA-AUTH-PROXY-SIGN` with the WSAA ticket for the `seti-setipago-api` service, and `WSAA-AUTH-PROXY-REPRESENTADO` with the CUIT. Like ARCA's gateway, ArcaSim checks everything together and answers a single 401 listing every problem, with its texts. WSAUTH's JWT (`Authorization: Bearer`) is not published, so ArcaSim does not issue it.

The payment entity reports the payment outside the API; in ArcaSim, `POST /arcasim/api/setiws/veps/{nroVEP}/payment` simulates it (`branchType`, `paymentForm`, `bank`, all optional). After that, the query returns the payment receipt (`CP`).

## 7. Errors

**Structure** (WSFEv1):

```xml
<Errors><Err><Code>10016</Code><Msg>…</Msg></Err></Errors>
<Observaciones><Obs><Code>10217</Code><Msg>…</Msg></Obs></Observaciones>
```

**Infrastructure and authentication**, on every operation:

| Code | `Msg` | When |
|---|---|---|
| 500 | `Campo Auth no fue ingresado o esta mal formado.` | `Auth` missing, or without the namespace |
| 600 | `ValidacionDeToken: Parametro nulo o vacio (token)` | Empty `Token` |
| 600 | `ValidacionDeToken: No valido token. Excepcion: …` | The token is not a ticket |
| 600 | `ValidacionDeToken: No validaron las fechas del token. GenTime=…, ExpTime=…, NowUTC=…` | Expired ticket |
| 600 | `ValidacionDeToken: Error al verificar hash: ` | A `Sign` that does not match the token |
| 600 | `ValidacionDeToken: No valido Id Sistema: wsfe(Id Sistema de token es: …)` | A ticket for another service |
| 600 | `ValidacionDeToken: No apareció CUIT en lista de relaciones: …` | A `Cuit` the ticket cannot act for |
| 602 | `No existen datos en nuestros registros para los parametros ingresados.` | A lookup with no result |

**Common when asking for a CAE:**

| Code | Where | What |
|---|---|---|
| 10000 | `Errors` | The issuer cannot issue that class (a monotributista and an invoice B, for instance) |
| 10002 | `Errors` | `CantReg` does not match the details sent |
| 10005 | `Errors` | Point of sale not enabled for web services |
| 10016 | `Errors` | The number or date is not the next one |
| 10016 | `Obs` | `CbteFch` out of range |
| 10015 | `Obs` | Receiver badly identified, or a final consumer above the amount that requires identification |
| 10048 | `Obs` | The total does not add up |
| 10018, 10023 | `Obs` | VAT missing or not adding up |
| 10217 | `Obs` (approved) | The tax credit legend for a monotributista receiver |
| 10242, 10243 | `Obs` | Receiver VAT condition unknown or not valid for the class |
| 10245 / 10246 | `Obs` | Receiver VAT condition missing (observes / rejects) |

The texts are ARCA's where known, missing accents included. The full list of the manual's 495 codes is in [`arca/wsfev1-codigos.md`](arca/wsfev1-codigos.md), and which ones ArcaSim answers, in [`cobertura.md`](cobertura.md).

## 8. Admin API

It does not exist at ARCA: it sets up test scenarios. JSON, no authentication (it is for development environments). Base: `/arcasim/api`.

| Method and path | What it does |
|---|---|
| `GET /status` | Environment, manual version, access, clock and the failures switched on (a service set back to normal no longer appears) |
| `PUT /settings` | `environment` (`Homologacion`/`Produccion`), `manualVersion` (`V4_7`/`V4_8`) or `followCalendar`, `replayWindowEnabled`, `openAccess`, `finalConsumerIdentificationThreshold`, `maxRecordsPerRequest`, `caeLifetimeDays` |
| `POST /reset` | Clears taxpayers, vouchers, failures, limits, activity and the sequences that number the services' texts, and goes back to real time |
| `GET /taxpayers` · `GET /taxpayers/{cuit}` | Taxpayers |
| `PUT /taxpayers/{cuit}` | Creates or updates: `name`, `vatCondition` (`ResponsableInscripto`, `Monotributo`, `Exento`…), `active`, `pointsOfSale` (`number`, `kind`: `WebServiceCae`, `WebServiceCaea` or `Other`; `blocked`; `deactivatedOn`) |
| `POST /certificates` | `cuit`, `alias`, `services` (default `["wsfe"]`), and `csr` (returns the certificate as PEM) or `password` (returns a PFX with the key). A CSR that does not read answers 400 and saves nothing |
| `GET /ca` | ArcaSim's certification authority, as PEM |
| `GET /authorizations` · `POST` · `DELETE` | Which alias may act for which CUIT on which service |
| `PUT /chaos/{service}` | `down`, `delayMilliseconds`, `dropNextResponse`, `forceRejection` (code) for `wsfe` and `wsaa`; `down`, `delayMilliseconds` and `balancerMask` for the rest, by their id (`wsmtxca`, `wscpe`, `seti-setipago-api`…). A name ArcaSim does not serve answers 404 |
| `GET /traffic` · `PUT /traffic/{service}` | Last-minute meter and limits: `requestsPerMinute`, `capacity`, `serviceTimeMilliseconds`, `queueLimit`. A name ArcaSim does not serve answers 404 |
| `POST /clock` · `DELETE /clock` | `freezeAt` (a moment) and/or `advanceMinutes`; `DELETE` goes back to real time |
| `GET /vouchers?cuit=&limit=` | Issued vouchers (a negative `limit` answers 400) |
| `GET /activity?limit=` | Live log: tickets, CAEs, rejections, saturated requests |
| `PUT /rates` | `currency`, `day`, `rate`: the rate foreign currency validations use |
| `GET /documents/{collection}?prefix=` · `GET`, `PUT`, `DELETE /documents/{collection}/{key}` | The state of the rest of the services ([§6](#6-the-rest-of-arcas-services)): read what they stored, or load before a test the registries ARCA's API cannot write |
| `POST /setiws/veps/{nroVEP}/payment` | The payment entity reports the VEP as paid: `branchType` (TIPO_SUCURSAL), `paymentForm` (FORMA_PAGO), `bank` |

## 9. Test scenarios

| To test | How |
|---|---|
| ARCA down | `PUT /chaos/wsfe {"down": true}`: HTTP 503 from WSFEv1, and `wsn.unavailable` when asking for the ticket |
| Slowness | `PUT /chaos/wsfe {"delayMilliseconds": 8000}` |
| A specific rejection | `PUT /chaos/wsfe {"forceRejection": 10048}`: the next voucher comes back rejected with that code |
| CAE granted, answer lost | `PUT /chaos/wsfe {"dropNextResponse": true}`: the next `FECAESolicitar` is authorized and the connection drops before the answer |
| Saturation | `PUT /traffic/wsfe {"requestsPerMinute": 30}`: past the limit, HTTP 503 |
| Bottleneck | `PUT /traffic/wsfe {"capacity": 2, "serviceTimeMilliseconds": 500, "queueLimit": 10}`: two at a time, the rest wait in a queue, and what does not fit gets 503 |
| Expired ticket | `POST /clock {"advanceMinutes": 780}` |
| The homologación load balancer that hides errors | `PUT /chaos/wsmtxca {"balancerMask": true}`: every HTTP 500 of the service comes out as `fwshomo`'s F5 leaves it, the line `BL<n> <date> 500` with HTTP 200 |
| The 01/12/2026 requirement | `POST /clock {"freezeAt": "2026-12-01T09:00:00-03:00"}` |
| ARCA's registration errors | `PUT /settings {"openAccess": false}` or `ArcaSim:Access=Strict`: a certificate from ArcaSim's authority, an authorization per service, a registered taxpayer and point of sale |

The `/arcasim/` panel does the same with buttons and shows each service's saturation meter.

## 10. Integrating it into a project

**What changes in the application:** the two URLs and the certificate. Nothing else.

**With an ARCA library** (any language): most let you set the WSAA and WSFEv1 URLs, or pick "homologación" and change its address. Point them at ArcaSim and give them the certificate. If the library keeps the ticket in a file, as it should, it also works with ArcaSim's anti-replay window.

**With a client generated from the WSDL** (`dotnet-svcutil`, `wsimport`, zeep, Node's `soap`…): generating it from ArcaSim's WSDL or from ARCA's gives the same client. Since ARCA's addresses are HTTPS, generated clients usually require HTTPS: run ArcaSim with `--urls https://…` and trust its development certificate (`dotnet dev-certs https --trust`).

**In .NET**, the repository ships `Arca.Client`:

```csharp
var options = new ArcaOptions
{
    WsaaUrl = new Uri("http://localhost:7080/ws/services/LoginCms"),
    WsfeUrl = new Uri("http://localhost:7080/wsfev1/service.asmx"),
    Certificate = ArcaOptions.LoadCertificate("cert.pfx", "password"),
    Cuit = 20111111112,
    TicketCacheDirectory = "data/tickets",
};
var wsfe = new WsfeClient(http, new WsaaClient(http, options), options);
var result = await wsfe.AuthorizeNextAsync(1, 6, new Voucher { /* … */ });
```

`AuthorizeNextAsync` asks for the next number, requests the CAE and, if the answer gets lost, looks the voucher up before failing.

**In an application's tests:** ArcaSim in memory starts in milliseconds. The project's `docker compose` can run it next to the application, and `POST /arcasim/api/reset` empties it between runs. In .NET it can run inside the test process with `WebApplicationFactory`, as Comanda does.

**ArcaSim's configuration** (`appsettings.json` or environment variables with `__`):

| Key | Values |
|---|---|
| `ArcaSim:Access` | `Open` (default) · `Strict` |
| `ArcaSim:Storage` | `Memory` (default) · `Postgres`, with `ConnectionStrings:ArcaSim` |
| `ArcaSim:Environment` | `Homologacion` (default) · `Produccion` |
| `ArcaSim:ReplayWindowEnabled` | `true` (default) · `false` |
| `ArcaSim:DataDirectory` | Where it keeps its certification authority and the ticket key |

## 11. Moving to ARCA

1. Create the key and the certificate request (CSR) with the DN `SERIALNUMBER=CUIT n, CN=alias`.
2. For homologación, upload the CSR to **WSASS**; for production, to **Administración de certificados digitales**. Associate the certificate with the `wsfe` service.
3. Register the point of sale as **Web service** (RECE).
4. Change the two URLs to those of [§1](#1-environments-and-addresses) and the certificate to ARCA's.

The application's code does not change.

## 12. What is not the same as ARCA

- ArcaSim's **access tickets** only work against ArcaSim: the real signature is ARCA's.
- **CAEs** have no tax validity and can only be verified in ArcaSim's WSCDC.
- The **registries** ARCA fills outside its web services (debts, apócrifos, communications, customs declarations, ratings) start with fictitious data; replace them with `PUT /documents`.
- Where a manual does not publish a table's values (FCE's rejection reasons, for example), the values are ArcaSim's, and the class's doc comment says so.
- **TRABAJO_F931** has no published WSDL: ArcaSim's is reconstructed from the manual.
- **SETIWS-PAGO-API** answers JSON only and does not take WSAUTH's JWT, which is not published; it authenticates with the WSAA ticket headers.
- The **taxpayer registry** is fictitious: taxpayers are the ones loaded or created on use, and an unknown CUIT with a valid check digit counts as active.
- **Exchange rates** are loaded by hand with `PUT /rates`; without one, the validations that depend on it are skipped.
- Some of the manual's codes answer with the manual's text, because ARCA's was never captured.
- A request no rule foresaw (a letter where a number goes, for example) gets the fault of that service's server, with its framework's default text (ASMX, CXF, the Java stacks, WSAA's `wsaa.internalError`, Spring Boot's error body), not a text captured from ARCA. Never an empty HTTP 500.
- The **TLS** certificate cannot be the one for `*.afip.gov.ar`.
