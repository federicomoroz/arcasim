# wslpg

Liquidación Primaria Electrónica de Granos (`LpgService`). Con este servicio, el comprador o el corredor de granos obtiene el **COE** (Código de Operación Electrónico) de una liquidación primaria (LPG, el ex formulario F1116 B/C). El mismo WSDL incluye además los ajustes de esa liquidación (por COE o por contrato), las liquidaciones secundarias (LSG, `lsg*`), los certificados de depósito, retiro, transferencia y preexistentes (`cg*`), los anticipos (`lpgAutorizarAnticipo` y `lpgCancelarAnticipo`), la anulación por contradocumento y 13 consultas de parámetros. Lo usan acopiadores, exportadores, industriales y corredores inscriptos en el RFOG o el RUCA.

Manual: "Certificación y Liquidación de Granos — WEB SERVICE LpgService — Manual para el Desarrollador", **versión 1.25 del 25/09/2026**, en `https://www.afip.gob.ar/ws/WSLiquiGranos/manual-wslpg.pdf`. Las citas de abajo usan la numeración de secciones y páginas del manual.

En homologación hay que pedir que habiliten la CUIT emisora escribiendo a `wslpg@afip.gob.ar`. Para los demás roles hay CUIT genéricas: vendedor 23000000000, 23000000019, 23000000027, 23000000035 y 33000000006; comprador 27000000014 (acondicionador) y 20400000000 (exportador); corredor 20200000006 (manual §1.3, p. 9).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Java JAX-WS** (stack Metro/RI). Lo observado: la respuesta de `dummy` viene en un sobre `S:Envelope` con `ns2:dummyResp`, sin `soap:Header`, y trae un `<return>` con `appserver`, `authserver` y `dbserver` = `OK` en mayúsculas (captura del 2026-10-02). Un `GET` al endpoint devuelve la página HTML "Web Services", que es la típica de JAX-WS. No es el stack de wscpe/wslca (`soap:Envelope` con header propio y `Ok`). |
| Endpoint de homologación | `https://fwshomo.afip.gov.ar:443/wslpg/LpgService` (`soap:address` del WSDL). El manual (§2.2, p. 17) da el mismo host sin `:443` |
| Endpoint de producción | `https://serviciosjava.afip.gob.ar:443/wslpg/LpgService` (`soap:address` del WSDL de producción). El manual da el mismo |
| targetNamespace | `http://serviciosjava.afip.gob.ar/wslpg/` |
| service / port / binding / portType | `LpgService` / `LpgEndPoint` / `LpgBinding` / `LpgPortType` |
| WSDL guardado | `docs/arca/wsdl/wslpg-homologacion.wsdl` (5742 líneas, autocontenido: sin `wsdl:import`, `xsd:import` ni `xsd:include`). El de producción solo cambia en el `soap:address` (diff del 2026-10-02) |
| WSAA service id | **No indicado en el manual: NO VERIFICADO.** Revisé las 18.883 líneas del texto: "wslpg" aparece solo en las URLs, en el namespace y en el mail `wslpg@afip.gob.ar`. Las secciones §2.3 y §4.2 hablan de "la información obtenida del WSAA" sin dar el nombre del servicio. Por la convención de los otros servicios Java (wslsp, wslca) lo más probable es `wslpg`, pero eso es una inferencia |
| SOAPAction | targetNamespace + nombre de la operación, por ejemplo `http://serviciosjava.afip.gob.ar/wslpg/liquidacionAutorizar`. Verificado en las 48 operaciones del binding |
| Versión SOAP | Solo SOAP 1.1 (un único binding `soap:`), `transport http://schemas.xmlsoap.org/soap/http` |
| Estilo | document/literal. Cada mensaje tiene una sola part, `parameters` |
| elementFormDefault | No está declarado, así que vale `unqualified`. Solo el elemento raíz del Body lleva namespace; `auth`, `token`, `coe` y el resto van sin namespace |
| Operaciones | **48** en `LpgPortType`. Ninguna declara `wsdl:fault` |
| Restos en el WSDL | Hay 137 `wsdl:message`, de los cuales 41 no los usa ninguna operación. Son métodos viejos o eliminados: `liquidacionAjustar*`, `liquidacionSecundaria*`, `autorizarCertificado*`, `cgTiposTitularGrano`, `cgModificarCuitCorredor`, `cg/lsg/lpgConsultarXCoePdf`, entre otros. El simulador expone solo las 48 operaciones del portType |

## Autenticación

Va dentro del elemento raíz de cada request, en un hijo `auth` de tipo `LpgAuthType`:

```xml
<auth>
  <token>xsd:string</token>   <!-- 1..1 -->
  <sign>xsd:string</sign>     <!-- 1..1 -->
  <cuit>LpgCuitType</cuit>    <!-- 1..1: xsd:long, >9999999999 y <=99999999999 (11 dígitos) -->
</auth>
```

- Aparece en las **47 operaciones que no son `dummy`** y siempre es el primer hijo, 1..1. El request de `dummy` no tiene parts: el Body va vacío.
- `cuit` es la CUIT emisora o representada. Según el manual (§2.3, p. 18): "Se validará en todos los casos que la CUIT emisora se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos. De no superarse algunas de las situaciones descriptas anteriormente retornará un error del tipo excepcional."
- **Falla documentada, no observada.** El "error excepcional" (§1.4, p. 10) es un Fault. El ejemplo del manual, copiado tal cual (fíjense que trae un código de SOAP 1.2 dentro de un sobre SOAP 1.1):

```xml
<S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
  <S:Body>
    <ns2:Fault xmlns:ns2="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:ns3="http://www.w3.org/2003/05/soap-envelope">
      <faultcode>ns3: Receiver</faultcode>
      <faultstring>[wscommon_007] La firma no corresponde al token enviado.</faultstring>
    </ns2:Fault>
  </S:Body>
</S:Envelope>
```

  El manual aclara que los errores excepcionales "incluyen también errores de estructura (ej: tags sin cerrar, con nombres incorrectos)". No da el status HTTP. En JAX-WS un Fault va con HTTP 500.
- **Falla observada (homologación, 2026-10-02).** Se probó `provinciasReq` con token y sign "abc", con token y sign vacíos y sin `auth`, y también un body que no es XML. Las cuatro respuestas fueron iguales: `HTTP/1.0 200 OK`, sin `Content-Type`, con `Connection: Keep-Alive`, `Content-Length: 39` y un body de texto plano del tipo `BL8689766870201 2026-10-02 15:08:18 500`. Por eso **no se pudo ver el Fault real**. La hipótesis, NO VERIFICADA, es que un WAF/F5 delante de fwshomo reemplaza cualquier HTTP 500.
- Request mínimo válido según el esquema:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsl="http://serviciosjava.afip.gob.ar/wslpg/">
  <soapenv:Header/>
  <soapenv:Body>
    <wsl:liqUltNroOrdenReq>
      <auth><token>...</token><sign>...</sign><cuit>20111111112</cuit></auth>
      <ptoEmision>1</ptoEmision>
    </wsl:liqUltNroOrdenReq>
  </soapenv:Body>
</soapenv:Envelope>
```

  Con `SOAPAction: "http://serviciosjava.afip.gob.ar/wslpg/liquidacionUltimoNroOrdenConsultar"`. Respuesta del manual (§2.4.13.3): `<ns2:liqUltNroOrdenResp><liqUltNroOrdenReturn><nroOrden>6</nroOrden></liqUltNroOrdenReturn></ns2:liqUltNroOrdenResp>`.

## Operaciones

Sigue el formato de "elemento de entrada → elemento de salida (hijo de retorno)". **Todos** los hijos de retorno, salvo el de `dummy`, tienen al final `errores`, `erroresFormato` y `eventos` (todos 0..1). En algunos tipos aparecen en otro orden: ver Errores. Abreviaturas: "pe+no" = `ptoEmision` + `nroOrden`; LPG = liquidación primaria; LSG = liquidación secundaria; CG = certificado de granos.

### Liquidación primaria (LPG)

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Verifica la infraestructura | Body vacío | `dummyResp/return/{appserver,authserver,dbserver}` | — |
| `liquidacionAutorizar` | Autoriza una LPG y obtiene el COE | `liquidacionReq`: `auth`, `liquidacion` (`LpgLiquidacionBaseType`, con `ptoEmision`, `nroOrden`, partes, grano, precio, certificados...), `deducciones?`, `retenciones?`, `percepciones?` | `liquidacionResp/liqReturn/autorizacion` (`ptoEmision`, `nroOrden`, `codTipoOperacion`, importes, `coe`, `numeroContrato`, `estado`) | **Crea una LPG con `estado` AC y devuelve el `coe`.** Se lee con `liquidacionXCoeConsultar` (por `coe`) o `liquidacionXNroOrdenConsultar` (por pe+no). Avanza el contador de `liquidacionUltimoNroOrdenConsultar` |
| `liquidacionXCoeConsultar` | Consulta una LPG por COE | `liqConsXCoeReq`: `auth`, `coe`, `pdf?` (S/N) | `liqConsXCoeResp/liqConsReturn/{liquidacion, autorizacion, pdf}` | Lee lo que creó `liquidacionAutorizar`. Rechaza los COE de ajuste (1861, 1723) |
| `liquidacionXNroOrdenConsultar` | Consulta una LPG por pe+no | `liqConsXNroOrdenReq`: `auth`, `ptoEmision`, `nroOrden` | `liqConsXNroOrdenResp/liqConsReturn` (el mismo tipo que la anterior) | Lee por pe+no. Si no existe: error 600 (§1.9.1) |
| `liquidacionUltimoNroOrdenConsultar` | Último `nroOrden` autorizado de la CUIT para un pto. de emisión | `liqUltNroOrdenReq`: `auth`, `ptoEmision` | `liqUltNroOrdenResp/liqUltNroOrdenReturn/nroOrden` | Lee el contador por (CUIT, `ptoEmision`). Devuelve 0 si no hay ninguna (§2.4.13.2) |
| `liquidacionAnular` | Anula una LPG activa. **Discontinuada** desde v1.22 en favor de `lpgAnularContraDocumento`, aunque sigue en el WSDL (§4.1) | `anulacionReq`: `auth`, `coe` | `anulacionResp/anulacionReturn/{coe, resultado (A/R), pdf}` | Pasa la LPG de AC a AN. El cambio se ve en `estado` de las consultas |
| `lpgAnularContraDocumento` | Anula una LPG generando un contradocumento, que es "similar a un ajuste unificado" | `LpgAnularContraDocumentoReq`: `auth`, `anulacionBase{puntoEmision, nroOrden, coeAnular}` (ojo: `puntoEmision`, no `ptoEmision`) | `LpgAnularContraDocumentoResp/liqConsReturn` (`LpgLiqConsReturnType`) | Crea un documento nuevo con COE propio (pe+no nuevos) sobre `coeAnular`. Ver estados en Comportamiento |
| `liquidacionAjustarUnificado` | Ajuste de crédito y/o débito sobre una LPG | `ajustarUnificadoReq`: `auth`, `ajusteBase{ptoEmision, nroOrden, coeAjustado, certificados?, codLocalidad, codProv, fusion?}`, `ajusteCredito?`, `ajusteDebito?` | `ajustarUnificadoResp/ajusteUnifReturn/ajusteUnificado` (`ptoEmision`, `nroOrden`, `nroContrato`, `coeAjustado`, `codTipoOperacion`, `ajusteCredito`, `ajusteDebito`, `totalesUnificados`, `coe`, `estado`) | **Crea un ajuste con COE propio** vinculado por `coeAjustado`. Usa el **mismo contador pe+no** que `liquidacionAutorizar` (§1.9.1). Se lee con `ajusteXCoeConsultar` (COE del ajuste) o `ajusteXNroOrdenConsultar` (pe+no) |
| `liquidacionAjustarContrato` | Un ajuste único para todas las LPG activas de un contrato | `ajustarContratoReq`: `auth`, `ajusteBase{ptoEmision, nroOrden, nroContrato, actividad, codGrano, cuitVendedor, cuitCorredor?, cuitComprador, precios, codPuerto, codLocalidad, codProv...}`, `ajusteCredito?`, `ajusteDebito?` | `ajustarContratoResp/ajusteContratoReturn/ajusteContrato` | Crea un ajuste por contrato con COE. Se lee con `ajustePorContratoConsultar` (`nroContrato`). Si se ajustó por contrato no se puede ajustar por COE, y al revés (§2.4.4) |
| `ajusteXCoeConsultar` | Consulta un ajuste por su COE | `ajusteXCoeConsReq`: `auth`, `coe`, `pdf?` | `ajusteXcoeConsResp/ajusteConsReturn/{ajusteUnificado, pdf}` (ojo con la `c` minúscula de `ajusteXcoeConsResp`) | Solo acepta COE de ajuste, no de originales (§2.4.9) |
| `ajusteXNroOrdenConsultar` | Consulta un ajuste por pe+no | `ajusteXNroOrdenConsReq`: `auth`, `ptoEmision`, `nroOrden` | `ajusteXNroOrdenConsResp/ajusteXNroOrdenConsReturn/{ajusteUnificado, pdf}` | Lee lo que creó `liquidacionAjustarUnificado` |
| `ajustePorContratoConsultar` | Consulta el ajuste activo de un contrato | `ajustePorContratoConsultarReq`: `auth`, `nroContrato` | `ajustePorContratoConsultarResp/ajusteContratoReturn/{ajusteUnificado, pdf}` | Lee lo que creó `liquidacionAjustarContrato`. Solo ajustes activos. Si no hay: 2109 "El Contrato no tiene un ajuste vigente." |
| `asociarLiquidacionAContrato` | Asocia una LPG original a un contrato | `asociarLiqAContratoReq`: `auth`, `coe`, `nroContrato`, `cuitComprador`, `cuitVendedor`, `cuitCorredor?`, `codGrano` | `asociarLiqAContratoResp/liquidacion` (tipo `LpgLiqConsReturnType`, así que queda `liquidacion/liquidacion`) | Vincula un `coe` con un `nroContrato`. Se lee con `liquidacionPorContratoConsultar` |
| `liquidacionPorContratoConsultar` | Lista los COE asociados a un contrato | `liquidacionPorContratoConsultarReq`: `auth`, `nroContrato`, `cuitComprador`, `cuitVendedor`, `cuitCorredor?`, `codGrano` | `liquidacionPorContratoConsultarResp/liqPorContratoCons/coeRelacionados/coe*` (acá `errores`, `erroresFormato` y `eventos` van **antes** de `coeRelacionados`) | Lee asociaciones y autorizaciones con `numeroContrato` |
| `lpgAutorizarAnticipo` | Autoriza una LPG de anticipo. Requiere un contrato con pago anticipado | `LpgAutorizarAnticipoReq`: `auth`, `anticipo{liquidacion (LpgLiquidacionAnticipoBaseType con pe+no), retenciones?, deducciones?}` | `LpgAutorizarAnticipoResp/liqReturn/autorizacion` (igual que `liquidacionAutorizar`) | Crea una LPG de anticipo con COE. Que comparta el contador pe+no de LPG: **NO VERIFICADO** |
| `lpgCancelarAnticipo` | Cancela un anticipo | `LpgCancelarAnticipoReq`: `auth`, `coe`, `ptoEmision`, `nroOrden`, `pdf` (**1..1 en el WSDL**) | `LpgCancelarAnticipoResp/liqConsReturn` | Crea un documento nuevo (pe+no propios). En el ejemplo §2.4.46.4 se envía el COE 330200008400 y vuelve una `autorizacion` con COE 330200008412 y `estado` AC. Error 1915 "La liquidacion anticipada seleccionada no se puede cancelar." |

### Liquidación secundaria (LSG)

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `lsgAutorizar` | Autoriza una LSG y obtiene el COE | `lsgAutorizarReq`: `auth`, `liqSecundariaBase` (pe+no, partes, `codGrano`, `cantidadTn`, precios, `deduccion*`, `percepcion*`...), `facturaPapel?` | `lsgAutorizarResp/oReturn/autorizacion{ptoEmision, nroOrden, fechaLiquidacion, subTotal, importeIva, operacionConIva, coe, totalDeducciones, totalPercepciones}` | Crea una LSG con COE. Se lee con `lsgConsultarXCoe` o `lsgConsultarXNroOrden`. Contador: `lsgConsultarUltimoNroOrden` |
| `lsgConsultarXCoe` | Consulta una LSG por COE | `lsgConsultarXCoeReq`: `auth`, `coe`, `pdf?` | `lsgConsultarXCoeResp/oReturn/{liquidaciones* {liquidacion, autorizacion, ajuste}, pdf}` | Lee la LSG y sus ajustes |
| `lsgConsultarXNroOrden` | Consulta una LSG por pe+no | `lsgConsultarXNroOrdenReq`: `auth`, `ptoEmision`, `nroOrden` | Igual que la anterior | Lee por pe+no |
| `lsgConsultarUltimoNroOrden` | Último `nroOrden` de LSG | `lsgConsultarUltimoNroOrdenReq`: `auth`, `ptoEmision` | `lsgConsultarUltimoNroOrdenResp/liqUltNroOrdenReturn/nroOrden` | Contador de LSG por (CUIT, `ptoEmision`). Que sea independiente del de LPG: **NO VERIFICADO**, aunque es lo que sugiere que haya un método aparte |
| `lsgAnular` | Anula una LSG activa. **Discontinuada** en favor de `lsgAnularContraDocumento` (§4.1) | `lsgAnularReq`: `auth`, `coe`, `pdf?` | `lsgAnularResp/anulacionReturn/{coe, resultado, pdf}` | Pasa de AC a AN. Si se repite: `resultado` R + 1527 (ver Errores) |
| `lsgAnularContraDocumento` | Anulación de una LSG por contradocumento. **No está documentada en el manual**: solo figura en el historial §4.1 | `LsgAnularContraDocumentoReq`: `auth`, `anulacionBase{puntoEmision, nroOrden, coeAnular}` | `LsgAnularContraDocumentoResp/liqConsReturn` (`LpgLiqConsReturnType`, tipo de LPG) | Igual que `lpgAnularContraDocumento`. Detalle NO VERIFICADO |
| `lsgAjustarXCoe` | Ajuste de una LSG por COE | `lsgAjustarXCoeReq`: `auth`, `coe`, `ptoEmision`, `nroOrden`, `codLocalidad`, `codProvincia`, `ajusteCredito?`, `ajusteDebito?`, `fusion?` | `lsgAjustarXCoeResp/oReturn` (aquí `errores`, `erroresFormato` y `eventos` van **primero**; después `ptoEmision`, `nroOrden`, `nroContrato`, `coeAjustado`, `coe`, `ajusteCredito`, `ajusteDebito`, `totalesUnificados`, `estado`) | Crea un ajuste de LSG con COE. Se lee con `lsgConsultarXCoe` (dentro de `liquidaciones/ajuste`) |
| `lsgAjustarXContrato` | Ajuste de LSG por contrato | `lsgAjustarXContratoReq`: `auth`, `nroContrato`, pe+no, `codLocalidad`, `codProvincia`, CUITs, `codGrano`, `ajusteCredito?`, `ajusteDebito?` | `lsgAjustarXContratoResp/oReturn` (igual que la anterior) | Crea un ajuste por contrato |
| `lsgAsociarAContrato` | Asocia una LSG a un contrato | `lsgAsociarAContratoReq`: los mismos campos que `asociarLiqAContratoReq` | `lsgAsociarAContratoResp/oReturn` (`LsgConsultaReturnType`) | Vincula `coe` y `nroContrato`. Se lee con `lsgConsultarXContrato` |
| `lsgConsultarXContrato` | COE de LSG asociados a un contrato | `lsgConsultarXContratoReq`: `auth`, `nroContrato`, CUITs, `codGrano` | `lsgConsultarXContratoResp/liqPorContratoCons/coeRelacionados/coe*` | Lee asociaciones |

### Certificados de granos (CG)

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `cgAutorizar` | Autoriza un certificado: primaria (depósito), retiro, transferencia o preexistente | `cgAutorizarReq`: `auth`, `cabecera{tipoCertificado (P/R/T/E), ptoEmision, nroOrden, ...}`, y uno de `primaria?`, `retiroTransferencia?`, `preexistente?`, `preexistenteFusion?` | `cgAutorizarResp/oReturn/autorizacion{ptoEmision, nroOrden, coe, estado, fechaCertificacion, pesosResumen?, serviciosResumen?, planta?}`. **`autorizacion` es 1..1, y dentro `coe`, `estado` y `fechaCertificacion` también son 1..1** | Crea un CG con `estado` AC (enum `LpgEstadoCertificadoType`). Se lee con `cgConsultarXCoe` o `cgConsultarXNroOrden`. Contador: `cgConsultarUltimoNroOrden` |
| `cgConsultarXCoe` | Consulta un CG por COE | `cgConsultarXCoeReq`: `auth`, `coe`, `pdf?` | `cgConsultarXCoeResp/oReturn` (`autorizacion` 1..1, `cabecera` 1..1, `primaria?`, `retiroTransferencia?`, `preexistente?`, `nroPlanta?`, ..., `kilosDisponible?`, `pdf?`) | Lee el CG |
| `cgConsultarXNroOrden` | Consulta un CG por pe+no | `CgConsultarXNroOrdenReq` (con **C mayúscula**): `auth`, `ptoEmision`, `nroOrden` | `CgConsultarXNroOrdenResp/oReturn` (igual que la anterior) | Lee por pe+no |
| `cgConsultarUltimoNroOrden` | Último `nroOrden` de CG | `cgConsultarUltimoNroOrdenReq`: `auth`, `ptoEmision` | `cgConsultarUltimoNroOrdenResp/liqUltNroOrdenReturn/nroOrden` | Contador de CG por (CUIT, `ptoEmision`) |
| `cgSolicitarAnulacion` | El depositario pide anular un CG | `cgSolicitarAnulacionReq`: `auth`, `coe`, `pdf?` | `cgSolicitarAnulacionResp/oReturn/{estadoCertificado, pdf}` (`xsd:all`) | AC → AN si se pide hasta el día 15 del mes siguiente; si es después, AC → PA |
| `cgConfirmarAnulacion` | El depositante confirma una anulación pendiente | `cgConfirmarAnulacionReq`: `auth`, `coe`, `pdf?` | `cgConfirmarAnulacionResp/oReturn/{estadoCertificado, pdf}` | PA → AN |
| `cgInformarCalidad` | Informa la calidad de un CG ya emitido | `CgInformarCalidadReq`: `auth`, `coe`, `calidad` (`CgCalidadType`) | `CgInformarCalidadResp/oReturn` (`CgConsultarReturnType`) | Modifica el CG y devuelve el certificado. Validaciones 3017 y 3018 (§4.1) |
| `cgBuscarCtg` | Busca CTG o cartas de porte que se pueden certificar | `cgBuscarCtgReq`: `auth`, `tipoCertificado`, `cuitDepositante`, `nroPlanta?`, `codGrano`, `campania`, `nroCtg?`, `tipoCtg?` (CTG/FC/CPE), `nroCartaPorte?`, rango de fechas | `cgBuscarCtgResp/oReturn/ctg+` (**1..unbounded**) | Consulta datos externos (CTG/CPE) |
| `cgBuscarCertConSaldoDisponible` | Certificados con saldo disponible para liquidar, retirar o transferir | `cgBuscarCertConSaldoDisponibleReq`: `auth`, `cuitDepositante`, `codGrano`, `campania`, `coe?`, rango de fechas de emisión | `cgBuscarCertConSaldoDisponibleResp/oReturn/certificado+` (**1..unbounded**) | Lee los CG con saldo |

### Parámetros (13 operaciones; todas consultan una tabla de parámetros)

`provinciasConsultar` (`provinciasReq` → `provinciasReturn/provincias`), `localidadXProvinciaConsultar` (`localidadReq{codProvincia}` → `localidadesReturn/localidades`), `tipoOperacionXActividadConsultar` (`tipoOperacionReq{nroActLiquida}` → `tipoOperacionReturn/tiposOperacion`), `puertoConsultar` (→ `puertoReturn/puertos`), `tipoActividadConsultar` (→ `tipoActividadReturn/tiposActividad`), `tipoActividadRepresentadoConsultar` (→ `tipoActividadReturn/tiposActividad`; son las actividades de la CUIT de `auth`), `tipoCertificadoDepositoConsultar` (`tipoCertificadoDepReq` → `tipoCertDepReturn/tiposCertDep`), `tipoRetencionConsultar` (→ `tipoRetencionReturn/tiposRetencion`), `tipoDeduccionConsultar` (→ `tipoDeduccionReturn/tiposDeduccion`), `tipoGranoConsultar` (→ `tipoGranoReturn/granos`), `codigoGradoReferenciaConsultar` (`gradoReferenciaReq` → `gradoRefReturn/gradosRef`), `codigoGradoEntregadoXTipoGranoConsultar` (`gradoEntregadoReq{codGrano}` → `gradoEntReturn/gradoEnt/gradoEnt*{codigoDescripcion, valor}`), `campaniasConsultar` (`campaniaReq` → `campaniaReturn/campanias`).

Salvo `gradoEnt`, todas devuelven `LpgArrCodigoDescripcionType`: una lista de `codigoDescripcion{codigo, descripcion}` (strings). Algunos valores de ejemplo del manual: puertos `1` SAN LORENZO/SAN MARTIN, `2` ROSARIO, `3` BAHIA BLANCA, `4` NECOCHEA; campaña `708` "2007/2008"; tipo de certificado `1` "F1116/RT". La entrada es solo `auth`, salvo en las tres que muestran su parámetro entre llaves.

## Errores

Hay cuatro canales (§1.4 a §1.8, pp. 10-13):

1. **Excepcionales: `S:Fault`.** Fallas de autenticación (token, sign, CUIT no representada) y errores de estructura del XML. El ejemplo del manual está en la sección Autenticación (`[wscommon_007] La firma no corresponde al token enviado.`). No hay `detail`.
2. **De formato: `erroresFormato/error/{codigo,descripcion}`** (`LpgArrErrorType`, de 1 a n `error` del tipo `LpgCodigoDescripcionType`). Se producen al validar el XML contra los tipos o por elementos fuera de orden. `codigo` trae el código del validador XSD, por ejemplo `cvc-complex-type.2.4.a`, y `descripcion` el mensaje del parser, por ejemplo "Invalid content was found starting with element 'x'. One of '{x}' is expected.". Si hay errores de formato, **no se ejecutan las validaciones de negocio**.
3. **De negocio, aplicación o infraestructura: `errores/error/{codigo,descripcion}`** (el mismo tipo).
4. **Eventos: `eventos/evento/{codigo,descripcion}`** (`LpgArrEventoType`). Son avisos programados, por ejemplo una "bajada de servicio por mantenimiento". Pueden acompañar una respuesta exitosa.

Las tres listas van **dentro del hijo de retorno** de cada operación (`liqReturn`, `liqConsReturn`, `oReturn`, etc.), no en el Body. Cada lista es 0..1 y, si está presente, tiene al menos un ítem. El orden en la secuencia **depende del tipo**: casi siempre van al final (`... , errores, erroresFormato, eventos`), pero en `LpgLiqPorContratoConsReturnType` y `LsgAjustarXCoeContratoReturnType` van primero, y en `CgSolicitarAnulacionReturnType` es un `xsd:all`. El simulador tiene que serializar en el orden del WSDL.

Cuando una autorización (§1.9.2) tiene errores, la respuesta trae **solo** `erroresFormato` o **solo** `errores`, sin `autorizacion`, y no se registra nada. Ejemplo del manual (§2.4.2.4, ejemplo 11):

```xml
<ns2:liquidacionResp xmlns:ns2="http://serviciosjava.afip.gob.ar/wslpg/">
  <liqReturn>
    <errores><error><codigo>2100</codigo><descripcion>El contrato ingresado no se encuentra registrado.</descripcion></error></errores>
  </liqReturn>
</ns2:liquidacionResp>
```

Las anulaciones son la excepción: devuelven `resultado` R **y además** `errores` (ejemplo de §2.4.31.5, `lsgAnular` repetido):

```xml
<anulacionReturn><coe>331000000155</coe><resultado>R</resultado>
  <errores><error><codigo>1527</codigo><descripcion>La liquidacion fue anulada con anterioridad.</descripcion></error></errores>
</anulacionReturn>
```

**Ojo con el esquema:** en `cgAutorizar`, `cgConsultarXCoe`, `cgConsultarXNroOrden` y `cgInformarCalidad`, `autorizacion` (y en las consultas también `cabecera`) es 1..1, y en `cgBuscarCtg` y `cgBuscarCertConSaldoDisponible` la lista es 1..unbounded. Una respuesta que trae solo `errores` **no valida** contra el WSDL. No sé qué hace el servicio real en ese caso (NO VERIFICADO). Propuesta: que el simulador emita solo `errores`, como en el resto, y documente que viola el esquema. JAX-WS no valida las respuestas.

**Códigos generales** (§1.7, p. 12; la tabla del PDF está desalineada y la asignación de 700 y 800 es mi lectura):

| código | causa |
|---|---|
| 500 | Error General de Aplicación |
| 501 | Error General de Aplicación (agregado en v1.17) |
| 600 | No existen datos en las bases de la Administración según los parámetros de búsqueda informados |
| 700 | Error de sincronismo |
| 800 | Servicio no disponible |
| 1021 | Error al generar el archivo pdf. **La operación se acepta igual**: se responde sin `<pdf>` y con este error en `errores` (pp. 108-109) |

**Códigos de negocio útiles para el simulador** (texto del manual):

| código | dónde | mensaje / condición |
|---|---|---|
| 1508 | autorizar / ajustar | `nroOrden` no correlativo al último registrado para el pto. de emisión (§1.9.1) |
| 1510 | consultas y anulaciones | La liquidación corresponde a otra CUIT ("Solo se pueden anular/consultar liquidaciones emitidas por la CUIT representada") |
| 1519 | anular | La liquidación no se puede anular (por ejemplo, pasó el día 15 del mes siguiente o tiene un ajuste activo) |
| 1527 | anular | La liquidacion fue anulada con anterioridad (ya está en AN) |
| 2108 | anular | Relacionada con un contrato que tiene un ajuste activo o vigente |
| 4301 | anular | No se puede ajustar una LPG en proceso de anulación por contradocumento |
| 1861, 1723 | `liquidacionXCoeConsultar` | El COE debe ser de una liquidación original / no pertenece a una liquidación primaria |
| 1909 | `liquidacionAjustarUnificado` | El coe ya registra un ajuste activo |
| 2100 | autorizar / ajustar contrato | El contrato ingresado no se encuentra registrado |
| 2109 | `ajustePorContratoConsultar` | El Contrato no tiene un ajuste vigente |
| 1618 | autorizar | Si no es propia produccion y actua corredor, debe informar el cuit del corredor |
| 1915 | `lpgCancelarAnticipo` | La liquidacion anticipada seleccionada no se puede cancelar |
| 3500-3504 | `cgSolicitarAnulacion` / `cgConfirmarAnulacion` | El certificado no es anulable (por tener LPG o retiro/transferencia asociados, por una transición de estado inválida, por falta de permisos, porque el CTG no existe o no está anulado) |

El catálogo completo de negocio **no es compacto**: está repartido en las tablas "Validaciones del Negocio" de cada método (§2.4.x.3, pp. 24-249), con **unos 230 códigos distintos** (1000-4301) que se repiten entre métodos. La extracción a texto rompe las columnas, así que para mapear código y mensaje hay que leer el PDF. No transcribirlos.

## Comportamiento a simular

**Punto de emisión y número de orden** (§1.9.1, pp. 13-14; §1.10):
- `ptoEmision`: `LpgPtoEmision`, `xsd:long` de 1 a 9999. Lo elige el cliente y no tiene que ser correlativo.
- `nroOrden`: `xsd:long` (el manual dice longitud 18). Es secuencial, sube de a uno **por CUIT (`auth/cuit`) y por punto de emisión**, y arranca en 1.
- Solo avanza cuando la solicitud **se autoriza** (cuando hay COE). Si se rechaza, se reusa el mismo número.
- Un `nroOrden` que no es "último + 1" se rechaza con **1508**. Según esa regla, reenviar un `nroOrden` ya autorizado también da 1508. **No hay idempotencia:** para recuperar algo después de un timeout, el manual indica consultar `liquidacionXNroOrdenConsultar(pe, no)`. Si existe, devuelve la liquidación; si no, da error 600. También se puede usar `liquidacionUltimoNroOrdenConsultar(pe)`.
- `liquidacionUltimoNroOrdenConsultar` devuelve `nroOrden` = 0 si no hay liquidaciones aprobadas para ese punto de emisión.
- **El contador de LPG es el mismo para `liquidacionAutorizar` y para los ajustes** (el manual dice "liquidacionAjustar", el nombre viejo; hoy son `liquidacionAjustarUnificado` y `liquidacionAjustarContrato`). Lo más probable es que `lpgAutorizarAnticipo`, `lpgCancelarAnticipo` y `lpgAnularContraDocumento`, que piden pe+no, usen ese mismo contador: **NO VERIFICADO**.
- LSG y CG tienen su propio "último número" (`lsgConsultarUltimoNroOrden`, `cgConsultarUltimoNroOrden`). La regla de correlatividad sería la misma (NO VERIFICADO para LSG y CG).
- El pe+no no da validez fiscal; solo el COE la da.

**COE:**
- `xsd:long`, de **12 dígitos** según las tablas del manual (§2.4.6, §3.2 y otras). Una sola tabla (`lpgCancelarAnticipo`, p. 247) dice 11.
- Todos los ejemplos del manual tienen 12 dígitos y empiezan con `33`: `3301xxxxxxxx` en LPG originales, `3302xxxxxxxx` en ajustes, anticipos y algunas LPG, `3310xxxxxxxx` en LSG y `3320xxxxxxxx` en certificados. El prefijo por tipo es **una observación de los ejemplos, no una regla documentada**. Para el simulador alcanzan 12 dígitos con un prefijo por familia.

**Estados:**
- LPG, LSG y ajustes: `estado` es un `xsd:string` en el WSDL (sin enum), con longitud 2. Valores documentados: **AC** (Activa) y **AN** (Anulada) (§3.2, pp. 259 y 277). `liquidacionAutorizar` y los ajustes devuelven AC.
- Transiciones: `liquidacionAnular` / `lsgAnular` hacen AC → AN (§1.9.4). Solo se puede anular una liquidación en AC; si ya está en AN da 1527. Plazo: hasta el día 15 del mes siguiente a la autorización (1519). No se puede anular si hay un ajuste activo (1519/2108).
- Ajustes: la LPG tiene que estar en AC (§2.4.3). El texto de §2.4.3 dice "que no tenga un ajuste relacionado activo", pero el historial (§4.1) dice que ahora "se permiten múltiples ajustes monetarios sobre una misma liquidación" (validaciones 4300 y 4301). El código 1909 sigue documentado. **Contradicción interna del manual.**
- Contradocumento (`lpgAnularContraDocumento` / `lsgAnularContraDocumento`): genera una liquidación nueva que replica la original "como un ajuste unificado", con COE nuevo. Según la descripción de §2.4.6.2, "el estado inicial de la anulación por contradocumento es 'PA' (Pendiente de Aceptación)" y después el vendedor la acepta desde la web. Así que la transición a AN de la original no la dispara ninguna operación del WS. En el WSDL, `estado` es un string libre y admite PA.
- Certificados (`LpgEstadoCertificadoType`, enum del WSDL): **AC**, **PA** (Pendiente de Anulación) y **AN**. `cgSolicitarAnulacion`: AC → AN si se pide hasta las 24 h del día 15 del mes siguiente a la certificación; si es después, AC → PA. `cgConfirmarAnulacion` (el depositante): PA → AN. No se puede anular si el certificado tiene una LPG asociada o se usó en un retiro o transferencia (3500).
- `resultado` de las anulaciones: `A` (aprobada) o `R` (rechazada).

**PDF:**
- `pdf` es un `xsd:base64Binary` en la respuesta. Se pide con `pdf` = `S` (`LpgSiNoType`) en `liquidacionXCoeConsultar`, `ajusteXCoeConsultar`, `lsgConsultarXCoe`, `lsgAnular`, `cgConsultarXCoe`, `cgSolicitarAnulacion`, `cgConfirmarAnulacion` y `lpgCancelarAnticipo`. Es el mismo PDF que imprime la aplicación web.
- Si falla la generación del PDF: se responde igual, sin `<pdf>`, y con 1021 en `errores`.
- Para el simulador alcanza un PDF mínimo válido en base64.

**Otros:**
- Fechas `xsd:date` en formato `AAAA-MM-DD`, sin zona horaria (§4.2).
- Redondeo Round Half Even.
- Las campañas van del 1/9 al 31/8 (código tipo `1213`).
- No hay paginación: las listas vuelven completas.
- `nroOpComercial` "se devolverá 0 en todos los casos" (p. 259).
- Respuesta exitosa: `autorizacion` (o el equivalente) sin `errores` ni `erroresFormato`. Puede traer `eventos`.
- `dummy`: HTTP 200, `Content-Type: text/xml;charset=utf-8`, body `<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:dummyResp xmlns:ns2="http://serviciosjava.afip.gob.ar/wslpg/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResp></S:Body></S:Envelope>` (captura del 2026-10-02, en una sola línea, sin header).
- Fault de autenticación según el manual (§1.4). Opcionalmente, un modo que imite lo observado en fwshomo: `HTTP/1.0 200`, texto `BL<n> <fecha> 500`.

**Flujo feliz mínimo:**
1. `liquidacionUltimoNroOrdenConsultar(pe)` → n.
2. `liquidacionAutorizar(pe, n+1, ...)` → `coe`, `estado` AC.
3. `liquidacionXCoeConsultar(coe, pdf=S)` o `liquidacionXNroOrdenConsultar(pe, n+1)` → la misma liquidación.
4. Opcional: `liquidacionAjustarUnificado(pe, n+2, coeAjustado=coe)` → COE de ajuste, que se lee con `ajusteXCoeConsultar` o `ajusteXNroOrdenConsultar`.
5. `liquidacionAnular(coe)` → `resultado` A; repetirlo → R + 1527.

## No verificado

- **WSAA service id**: el manual no lo da. Lo probable es `wslpg`; hay que confirmarlo en WSASS.
- El Fault real de autenticación en homologación: fwshomo devolvió `BL... 500` en texto plano para token "abc", token vacío y request sin `auth`. Tampoco se sabe el status HTTP del Fault (en JAX-WS sería 500) ni si `faultcode` es realmente `ns3:Receiver` (el ejemplo del manual mezcla SOAP 1.1 y 1.2).
- Si el servicio verifica el header `SOAPAction` o despacha solo por el elemento del Body.
- **Manual vs WSDL (el simulador sigue al WSDL):**
  - `liquidacionAnular`: el manual (§2.4.6.2) muestra `coeAnulacion` y `estadoAnulacion` en la respuesta (en el ejemplo: `coeAnulacion` 330100000338, `estadoAnulacion` PA). `LpgAnulacionReturnType` en el WSDL **no los tiene**: solo `coe`, `resultado`, `pdf`, `errores`, `erroresFormato` y `eventos`.
  - `lpgAnularContraDocumento`: el manual dice que la respuesta "es la misma que la del método liquidacionAjustarUnificado"; el WSDL dice `LpgLiqConsRespType` (`liqConsReturn`).
  - `lpgCancelarAnticipo`: el manual marca `pdf` como optativo y el COE de 11 dígitos; en el WSDL `pdf` es 1..1.
  - Ejemplo de `liquidacionXNroOrdenConsultar` (§2.4.7.4): el elemento raíz de la respuesta es `liqConsXCoeResp`; el WSDL dice `liqConsXNroOrdenResp`.
  - `lsgAnularContraDocumento` está en el WSDL pero no tiene sección en el manual.
- Si los contadores de LSG, CG, anticipos y contradocumentos son independientes o comparten el de LPG.
- Qué responde el servicio real cuando hay error en las operaciones `cg*`, donde el esquema exige `autorizacion`, `ctg` o `certificado`.
- Ajustes: si hoy se permiten varios ajustes activos por COE (historial) o se rechazan con 1909 (§2.4.3).
- Si el prefijo del COE (3301, 3302, 3310, 3320) depende del tipo de documento.
- Cómo pasa la liquidación original a AN después de una anulación por contradocumento (la aceptación del vendedor se hace por web).
- La asignación exacta de los códigos 700 y 800 en la tabla desalineada de §1.7.
