# Catálogo de Web Services de ARCA

Relevamiento hecho el 2026-10-01 sobre la documentación pública de ARCA (ex AFIP). Este documento define qué significa "toda la API de ARCA" y propone qué cubre ArcaSim. WSAA y wsfev1 se documentan en profundidad en otros archivos de esta carpeta; acá solo aparecen como filas de la tabla.

## Cómo se hizo y cómo leerlo

- Fuentes primarias:
  - el catálogo de "otros WS de negocio": https://www.afip.gob.ar/ws/documentacion/catalogo.asp
  - la página de factura electrónica: https://www.afip.gob.ar/ws/documentacion/ws-factura-electronica.asp
  - la página de homologación externa: https://www.afip.gob.ar/ws/documentacion/homologacion-externa.asp
  - la arquitectura: https://www.afip.gob.ar/ws/documentacion/arquitectura-general.asp
  - WSAA: https://www.afip.gob.ar/ws/documentacion/wsaa.asp
  - los servicios migrados: https://www.afip.gob.ar/ws/documentacion/servicios-migrados.asp
- Se descargaron los 50 manuales PDF enlazados desde esas páginas (todos respondieron HTTP 200 `application/pdf`) y se extrajo su texto. Las URLs de endpoint y los service IDs de WSAA salen de esos manuales.
- Cada URL de WSDL se pidió con `curl` el 2026-10-01. La columna de estado de red usa estas marcas:
  - **[V]**: respondió HTTP 200 con un `wsdl:definitions` real.
  - **[M]**: solo figura en el manual; no se probó o no hay URL completa.
  - **[X]**: falla. El motivo se indica en cada caso.
- Se hicieron llamadas reales a `dummy` y llamadas con token falso contra homologación de los servicios estudiados en detalle, para registrar cómo responden los errores de verdad (sección 4).
- Si un dato no está en ninguna fuente, aparece como **NO VERIFICADO**. No se completó nada por convención sin marcarlo.

Base de los manuales: salvo que se indique otra cosa, las rutas de manual son relativas a `https://www.afip.gob.ar`.

## 1. Arquitectura común

Fuente: https://www.afip.gob.ar/ws/documentacion/arquitectura-general.asp

- Todos los "web services de negocio" (WSN) son **SOAP sobre HTTPS**, accesibles por Internet, sin VPN.
- El acceso lo regula **WSAA**: el cliente firma un TRA (CMS/PKCS#7) con su certificado X.509 y recibe un Ticket de Acceso (TA) **para un WSN en particular**, válido **12 horas**. Del TA extrae `token` y `sign` y los manda en cada llamada al WSN.
- El certificado de testing y su asociación a cada WSN se gestionan con **WSASS**. En producción se usan "Administrador de Certificados Digitales" y "Administrador de Relaciones de Clave Fiscal" (https://www.afip.gob.ar/ws/documentacion/wsaa.asp).
- WSAA: testing `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` y producción `https://wsaa.afip.gov.ar/ws/services/LoginCms` (misma fuente). El detalle está en el documento de WSAA.
- El catálogo advierte: "Para usar ciertos servicios se requieren autorizaciones y acuerdos especiales con ARCA" (catalogo.asp). Muchos de los servicios de abajo son para organismos, bancos o agentes aduaneros, no para cualquier contribuyente.

La única excepción a SOAP+WSAA en todo el catálogo es **SETIWS-PAGO-API**. Es REST/JSON con OpenAPI y se autentica con `Authorization: Bearer <JWT>` emitido por "WSAUTH", no por WSAA (manual: `/ws/SETIWS-PAGO-API/Manual_para_el_desarrollador_setiws-pago-api.pdf`).

## 2. Cuántos servicios hay

| Fuente | Entradas | Observaciones |
|---|---|---|
| `catalogo.asp`: sección general | 37 | Incluye 3 entradas deprecadas: ws_sr_padron_a5, Padrón N3/N10 y WSCREATEVEP |
| `catalogo.asp`: sección Aduana | 11 | Además hay una entrada de relleno, "Ejemplo del componente acordeón #2 / Lorem ipsum...". **No es un servicio** |
| `ws-factura-electronica.asp` | 7 | wsfe (reemplazado), wsseg (repetido en el catálogo), wsfev1, wsmtxca, wsbfev1, wsfexv1, wsct |
| Fuera de `/ws/`: Factura de Crédito Electrónica MiPyMEs | 3 | wsfecred, wsfecredagente, wsfecredsca. Sus manuales están en `servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/`, no en el catálogo |
| WSAA | 1 | Es el servicio de autenticación, no un WSN |

Total: **57 entradas de servicios de negocio** más WSAA. Cuentan 48 del catálogo y 6 de la página de factura electrónica que no están en el catálogo (wsseg está en las dos). Se suman los 3 de FCE. "Padrón N3/N10" se cuenta como una sola entrada, aunque son dos servicios deprecados. De esas 57, 4 entradas están deprecadas o reemplazadas: wsfe, ws_sr_padron_a5, N3/N10 y WSCREATEVEP. Quedan **53 vigentes o sin indicación de estado**.

El nombre `wsctg` (Código de Trazabilidad de Granos) **no aparece** en el catálogo actual. La URL de homologación probada (`https://fwshomo.afip.gov.ar/wsctg/services/CTGService_v4.0?wsdl`) devolvió 404. Fuentes de terceros lo describen como antecesor de WSCPE (https://www.pyafipws.com.ar/agropecuarios/wsctg). Su estado es **NO VERIFICADO** en fuente oficial.

## 3. Tabla completa

Notas de lectura:
- La columna "WSAA service" indica el valor del tag `<service>` del TRA tal como lo dice el manual. "No indicado" significa que el manual no lo dice. **No se completó por convención.**
- La columna "Ops" indica la cantidad de operaciones del `portType` en el WSDL de homologación descargado el 2026-10-01.

### 3.1 Facturación electrónica

Todos son SOAP con WSAA.

| Servicio | Qué hace | WSAA service | Homologación (WSDL) | Producción (WSDL) | Manual | Estado | Ops |
|---|---|---|---|---|---|---|---|
| `wsfev1` | Comprobantes A, B, C y M **sin detalle de ítems**, CAE y CAEA (RG 4291) | `wsfe` | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL` [V] | `https://servicios1.afip.gov.ar/wsfev1/service.asmx?WSDL` [V] | `/ws/documentacion/manuales/manual-desarrollador-ARCA-COMPG.pdf` (V4.7); RG 5616: `/fe/ayuda/documentos/wsfev1-RG-4291.pdf` (V4.8) | Vigente | 22 |
| `wsmtxca` | Comprobantes A y B **con detalle de ítems**, CAE y CAEA (RG 2904) | No indicado | `https://fwshomo.afip.gov.ar/wsmtxca/services/MTXCAService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsmtxca/services/MTXCAService?wsdl` [V] | `/fe/ayuda/documentos/wsmtxca-RG-2904.pdf` (V0.25.8) | Vigente | 27 |
| `wsfexv1` | Factura de exportación, tipo E (RG 2758) | `wsfex` | `https://wswhomo.afip.gov.ar/wsfexv1/service.asmx?WSDL` [V] | `https://servicios1.afip.gov.ar/wsfexv1/service.asmx?WSDL` [V] | `/ws/documentacion/manuales/WSFEX-Manualparaeldesarrollador_V3.1.1_ARCA.pdf` (la portada dice 3.1.0, 18-ago-2025) | Vigente | 19 |
| `wsbfev1` | Bonos Fiscales Electrónicos por bienes de capital (RG 5427/2023 y RG 2861) | `wsbfe` | `https://wswhomo.afip.gov.ar/wsbfev1/service.asmx?WSDL` [V] | `https://servicios1.afip.gov.ar/wsbfev1/service.asmx?WSDL` [V] | `/ws/documentacion/manuales/WSBFEV1-ManualParaElDesarrollador_ARCA_V3_0.pdf`; RG 5616: `/fe/ayuda/documentos/wsbfev1-RG-5427-y-2861.pdf` (V3.2) | Vigente | 15 |
| `wsbfe` | Bonos Fiscales Electrónicos, versión anterior (RG 2557 según el catálogo) | `wsbfe` | `https://wswhomo.afip.gov.ar/wsbfe/service.asmx?WSDL` [V] | `https://servicios1.afip.gov.ar/wsbfe/service.asmx?WSDL` [V] | `/ws/WSBFE/WSBFE%20-%20Manual%20para%20el%20desarrollador_V1_1.pdf` | El catálogo no lo marca como deprecado; la página de FE solo lista wsbfev1 | 14 |
| `wsct` | Comprobantes T: alojamiento a turistas extranjeros (RG 3971) | No indicado | `https://fwshomo.afip.gov.ar/wsct/CTService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsct/CTService?wsdl` [V] | `/ws/documentacion/manuales/Manual_Desarrollador_WSCT_v1.6.4.pdf` | Vigente | 22 |
| `wsseg` | Pólizas de Seguros de Caución (RG 2668) | `wsseg` | `https://wswhomo.afip.gov.ar/wsseg/service.asmx?WSDL` [V] | `https://servicios1.afip.gov.ar/wsseg/service.asmx?WSDL` [V] | `/ws/documentacion/manuales/WSSEG-ManualParaElDesarrollador_ARCA.pdf` (V0.9); RG 5616: `/fe/ayuda/documentos/wsseg-RG-2668.pdf` | Vigente | 11 |
| `wscdc` | Constatación: verifica que un comprobante recibido esté autorizado por ARCA (CAE, CAEA o CAI) | No indicado | Manual: `https://wswhomo.afip.gob.ar/WSCDC/service.asmx?WSDL` [V]; también responde en `wswhomo.afip.gov.ar` [V] | Manual: `https://servicios1.arca.gob.ar/WSCDC/service.asmx?WSDL` **[X]**, porque el certificado TLS es de `servicios1.afip.gov.ar`. `https://servicios1.afip.gov.ar/WSCDC/service.asmx?WSDL` [V] | `/ws/WSCDCV1/WSCDC-manual-desarrollador-v4.pdf` (V0.4) | Vigente | 6 |
| `wsfe` | Factura electrónica V0, A y B (RG 4291) | NO VERIFICADO | `https://wswhomo.afip.gov.ar/wsfe/service.asmx?WSDL` [V]. **URL no documentada**: se dedujo del patrón y respondió 200 | `https://servicios1.afip.gov.ar/wsfe/service.asmx?WSDL` [V] (mismo caveat) | Sin manual enlazado | "Reemplazado por wsfev1 desde el 1-jul-2011" (ws-factura-electronica.asp), aunque el WSDL sigue publicado | 7 |
| `wsfecred` | Factura de Crédito Electrónica MiPyMEs: cuenta corriente, aceptación o rechazo, obligado a recibir | `wsfecred` | `https://fwshomo.afip.gov.ar/wsfecred/FECredService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsfecred/FECredService?wsdl` [V] | `https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECRED_v2.0.3.pdf` | Vigente; no figura en `/ws/` | 21 |
| `wsfecredagente` | FCE MiPyMEs para agentes de depósito colectivo | `wsfecredagente` | `https://fwshomo.afip.gov.ar/wsfecredagente/FECredAgenteService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsfecredagente/FECredAgenteService?wsdl` [M] | `https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECREDAGENTE-1.1.0.pdf` | Sin indicación | 8 |
| `wsfecredsca` | FCE MiPyMEs, Sistema de Circulación Abierta | `wsfecredsca` | `https://fwshomo.afip.gov.ar/wsfecredsca/FECredSCAService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsfecredsca/FECredSCAService` [M] | `https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECREDSCA-1.3.0.pdf` | Sin indicación | 3 |

### 3.2 Padrón (consultas de contribuyentes)

Todos son SOAP con WSAA. Usan el mismo host y el mismo "dialecto" (ver sección 4.1).

| Servicio | Qué hace | WSAA service | Homologación (WSDL) | Producción (WSDL) | Manual | Estado | Ops |
|---|---|---|---|---|---|---|---|
| `ws_sr_constancia_inscripcion` | Datos de la constancia de inscripción de un CUIT: nombre o razón social, domicilio fiscal, impuestos (IVA, monotributo), actividades, caracterizaciones | `ws_sr_constancia_inscripcion` | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5?WSDL` [V] (el manual 4.1 da `awshomo.arca.gob.ar`, que también respondió 200, aunque una vez falló en el primer intento) | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5?WSDL` [V]; `https://aws.arca.gob.ar/...` [V] | `/ws/WSCI/manual_ws_sr_ws_constancia_inscripcion.pdf` (V4.1, marzo 2026) | Vigente | 5 |
| `ws_sr_padron_a5` | Nombre anterior del mismo servicio. El manual de constancia, v2.0 del 27/11/17, registra el "Cambio de nombre del WS de ws_sr_padron_a5 a ws_sr_constancia_inscripcion" | `ws_sr_padron_a5` (es el nombre histórico; que WSAA lo siga aceptando está **NO VERIFICADO**) | Mismo endpoint `personaServiceA5` | Mismo | Sin manual propio | **Deprecado**, "reemplazado por ws_sr_constancia_inscripcion" (catalogo.asp) | — |
| `ws_sr_padron_a13` | Datos de identificación y domicilios fiscal y legal; además busca CUITs a partir de un DNI | `ws_sr_padron_a13` | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA13?WSDL` [V] | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA13?WSDL` [V] | `/ws/ws-padron-a13/manual-ws-sr-padron-a13-v1.4.pdf` (V1.4, 14/08/2026) | Vigente | 4 |
| `ws_sr_padron_a4` | Situación tributaria: impuestos y regímenes en los que está inscripto | `ws_sr_padron_a4` | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA4?WSDL` [V] | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA4?WSDL` [V] | `/ws/ws_sr_padron_a4/manual_ws_sr_padron_a4_v1.3.pdf`; datos de prueba: `/ws/ws_sr_padron_a4/datos-prueba-padron-a4.txt` | Vigente | 2 |
| `ws_sr_padron_a10` | Datos resumidos del contribuyente ("versión mínima") | `ws_sr_padron_a10` | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA10?WSDL` [V]. Ojo: el `soap:address` del WSDL dice `http://`, no `https://` | `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA10?WSDL` [V] | `/ws/ws_sr_padron_a10/manual_ws_sr_padron_a10_v1.2.pdf` | Vigente | 2 |
| `ws_sr_padron_a100` | Tablas de parámetros del padrón (SUPA): provincias, formas jurídicas, etc. | `ws_sr_padron_a100` | `https://awshomo.afip.gov.ar/sr-parametros/webservices/parameterServiceA100?WSDL` [V] | `https://aws.afip.gov.ar/sr-parametros/webservices/parameterServiceA100?WSDL` [V] | `/ws/ws_sr_padron_a100/manual_ws_sr_padron_a100_v2.1.pdf` | Vigente | 2 |
| Padrón Nivel 3 y Nivel 10 | — | — | — | — | — | **Deprecados**, "reemplazados por WS A4 y WS A10" (catalogo.asp) | — |

### 3.3 Otros servicios de negocio del catálogo general

Todos son SOAP con WSAA, salvo donde se indica.

| Servicio | Qué hace | WSAA service | Homologación | Producción | Manual | Estado | Ops |
|---|---|---|---|---|---|---|---|
| `wsagr` (AGR Reproweb) | Consultas al registro AGR/Reproweb (RG 4035). Operaciones: `Consulta`, `ConsultaHistorica`, `ConsultaRectificada`, `ConsultaCondRet`, `ConsultaCodResp`, `ConsultaCantCuit`, `ConsultaObs`, `dummy`. El propósito de negocio más allá del nombre está **NO VERIFICADO** | No indicado | `https://wswhomo.afip.gov.ar/wsagr/wsagr.asmx?WSDL` [V]. La otra URL del manual, `wswhomo.afip.gov.ar/wsagr.asmx?WSDL`, da 404 | Manual: `https://servicios1.afip.gov.ar/wsagr.asmx?WSDL` **[X] 404**. `https://servicios1.afip.gov.ar/wsagr/wsagr.asmx?WSDL` [V], pero esa URL se dedujo y no está documentada | `/ws/agrREPROWEB/manual_desarrollador_wsagr.pdf` (V1.0) | Sin indicación | 8 |
| Automatización Res. Revocación A.P.E. (Obras Sociales) | El organismo A.P.E. envía resoluciones de revocación para retener deudas de obras sociales (Decreto 213/04) | — | Sin URL publicada | — | Sin manual | **NO VERIFICADO** | — |
| `wscpe` (Carta de Porte Electrónica) | Carta de porte ferroviaria y automotor, granos y derivados | `wscpe` (según el manual, el servicio se da de alta como "wscpe" en Autogestión de certificados) | `https://cpea-ws-qaext.afip.gob.ar/wscpe/services/soap?wsdl` [V] | `https://cpea-ws.afip.gob.ar/wscpe/services/soap?wsdl` [V] | `/ws/documentos/manual-wscpe.pdf` (V2.2.1) | Vigente | 75 |
| `wscta` (Certificados DNRPA) | La DNRPA consulta, rechaza o acepta Certificados de Transferencia de Automotores (F.381) | No indicado | Manual: `https://fwshomo.afip.gov.ar/wscta/services/CertificadoDNRPAService?wsdl` **[X] 404** | No publicada | `/ws/WSCTA/WSCTA-ManualParaElDesarrollador.pdf` | **NO VERIFICADO** (homologación no responde) | — |
| `ws_sr_...`: ver 3.2 | | | | | | | |
| `wscec` (Economía del Conocimiento) | Consulta comprobantes de beneficiarios de la Ley de Economía del Conocimiento | No indicado | `https://fwshomo.afip.gov.ar/wscec/CECService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wscec/CECService?wsdl` [V] | `/ws/wscec/manual-wscec.pdf` | Sin indicación | 5 |
| `wsapoc` (Contribuyentes apócrifos) | Consulta la base de facturas y contribuyentes apócrifos | `wsapoc` | `https://eapoc-ws-qaext.afip.gob.ar/Service.asmx?WSDL` [V] | `https://eapoc-ws.afip.gob.ar/service.asmx?WSDL` [V] | `/ws/wsapoc/ManualUsuario-1.0.9.pdf` | Sin indicación | 4 |
| `sud_contrataciones` | Un organismo verifica si un proveedor del Estado tiene deuda | `sud_restricciones`. El manual de contrataciones da el **mismo ID y el mismo endpoint** que restricciones | `https://sud-ws.cloudhomo.afip.gob.ar/sud_restricciones?wsdl` **[X]**: certificado no confiable para Windows (`SEC_E_UNTRUSTED_ROOT`) | `https://sud-ws.cloud.afip.gob.ar/sud_restricciones?wsdl` [V] | `/ws/SudContrataciones/manual_sud_contrataciones.pdf` (V1.0) | Sin indicación | 3 (compartidas) |
| `sud_restricciones` | Consulta de deuda por CUIT ("reservado a entidades bancarias") | `sud_restricciones` | Igual que la fila anterior | Igual | `/ws/SudRestricciones/manual_sud_restricciones_1.3.pdf` | Sin indicación | 3 |
| `veconsumerws` (WSCCOMU) | Lee las comunicaciones de la Ventanilla Electrónica del contribuyente | `veconsumerws` | `https://stable-middleware-tecno-ext.afip.gob.ar/ve-ws/services/veconsumer?wsdl` [V] | `https://infraestructura.afip.gob.ar/ve-ws/services/veconsumer?wsdl` [V] | `/ws/WSCComu/vecuwsconcomunicaciones.pdf` | Sin indicación | 5 |
| WSCREATEVEP | Creación de VEPs para entidades externas | `setipagob2b_createvep` | `https://awshomo.afip.gov.ar/setiws/services/externalvepreceptorinterop?wsdl` [V] | `https://aws.afip.gov.ar/setiws/services/externalvepreceptorinterop?wsdl` [V] | `/ws/WSCREATEVEP/ManualParaElDesarrolladorDelCreateVEPwebService.pdf` | **Deprecado**, "reemplazado por setiws-pago-api" | 4 |
| SETIWS-PAGO-API | **REST/JSON**: organismos crean VEPs, los envían a entidades de pago y los consultan (`GET /dummy`, `POST /api/v1/veps`, `GET /api/v1/veps`) | **No usa WSAA**: `Authorization: Bearer <JWT>` de "WSAUTH" más el header `WSAA-AUTH-PROXY-REPRESENTADO`. El ID que da el manual se extrae como "seti-setipago-api" (el texto tiene ligaduras rotas) | `https://seti-setipago-api-qaext.arca.gob.ar/`; OpenAPI en `/v3/api-docs` [V] (JSON `openapi`) | `https://setiws-pago-api.arca.gob.ar/` (`/dummy` [V]) | `/ws/SETIWS-PAGO-API/Manual_para_el_desarrollador_setiws-pago-api.pdf` | Vigente | — |
| `wsjaza` (JAZA Service) | Informe de operaciones en puntos de explotación de juegos de azar | No indicado | `https://fwshomo.afip.gov.ar/wsjaza/JAZAService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsjaza/JAZAService?wsdl` [V] | `/juegosdeazar/documentos/Web_Service_JAZA_20240122_v1.0.5.pdf` | Sin indicación | 11 |
| `wslum` | Liquidación Mensual Única de Lechería | No indicado | `https://fwshomo.afip.gov.ar/wslum/LumService?wsdl` [V] (el manual da `fwshomo.arca.gob.ar`) | `https://serviciosjava.afip.gob.ar/wslum/LumService?wsdl` [V] | `/ws/wslum/manual_wslum1.4.pdf` | Sin indicación | 10 |
| `wslca` | Liquidación de compra de caña de azúcar | `wslca` ("el token debe solicitarse para el servicio wslca") | `https://fwshomo.afip.gov.ar/wslca/services/soap?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wslca/services/soap?wsdl` [V] | `/ws/WSLCA/manual_wslca.pdf` | Sin indicación | 14 |
| `wsltv` | Liquidación de tabaco verde | No indicado | `https://fwshomo.afip.gov.ar/wsltv/LtvService?wsdl` [V] (el manual da `fwshomo.arca.gov.ar`, que **no resuelve DNS**) | `https://serviciosjava.afip.gob.ar/wsltv/LtvService?wsdl` [V] | `/ws/tabaco/manual-wsltv-1.4.pdf` | Sin indicación | 15 |
| `wslpg` | Liquidación primaria electrónica de granos | No indicado | `https://fwshomo.afip.gov.ar/wslpg/LpgService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wslpg/LpgService?wsdl` [V] | `/ws/WSLiquiGranos/manual-wslpg.pdf` (V1.25) | Sin indicación | 48 |
| `wslsp` | Liquidación del sector pecuario (incluye avícola) | `wslsp` ("el token debe solicitarse para el servicio wslsp") | `https://fwshomo.afip.gov.ar/wslsp/LspService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wslsp/LspService?wsdl` [V] | `/ws/WSLSP/manual_wslsp_2.0.6.pdf` | Sin indicación | 23 |
| `miargentina-ws` (Mi Argentina WS) | Datos de vida laboral para la app Mi Argentina | `miargentina-ws` | `https://webserviceshomoext.afip.gob.ar/miargentina-ws/servicios.asmx?wsdl` [V] | `https://wswss.afip.gob.ar/miargentina-ws/servicios.asmx?wsdl` [V] | `/ws/Mi-argentina/MiArgentina-Webservice-Manual-del-Desarrollador-v2.2.pdf` | Sin indicación | 2 |
| `wsseg` | Ver 3.1 | | | | | | |
| Presentación de DDJJ (`uploadPresentacionService`) | Automatiza la presentación de declaraciones juradas | Por perfil: `presentacionprocessor` (organismo), `djprocessorcontribuyente`, `djprocessorcontribuyente_cf` | Manual: `https://awshomo.arca.gov.ar/...` **[X] DNS**. `https://awshomo.afip.gov.ar/setiws/webservices/uploadPresentacionService?wsdl` [V]; importa un WSDL padre con `upload`, `consulta` y `dummy` | `https://aws.afip.gov.ar/setiws/webservices/uploadPresentacionService?wsdl` [V] | `/ws/wsddjj/WSPresentaciondeDDJJManualparaelDesarrollador.pdf` | Sin indicación | 3 |
| `wsrgiva` (Régimen Percepción IVA) | Plataformas digitales consultan la situación fiscal de los sujetos del régimen de la RG 5319/2023 | No indicado | `https://fwshomo.afip.gov.ar/wsrgiva/services/RegimenPercepcionIVAService?wsdl` [V] (el manual da `fwshomo.arca.gov.ar`, **[X] DNS**) | `https://serviciosjava.afip.gob.ar/wsrgiva/services/RegimenPercepcionIVAService?wsdl` [V] | `/ws/documentacion/manuales/manualdesarrolladorWSRGIVA.pdf` | Sin indicación | 2 |
| `wstabaco` (Régimen Tabacalero) | Tabaco en hebras: CATHEs, desnaturalización, cambios de titular | `wstabaco` | `https://fwshomo.afip.gov.ar/wstabaco/TabacoService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wstabaco/TabacoService?wsdl` [V] | `/ws/WSTABACO/Manual_Desarrollador_WSTABACO_v1_0.pdf` | Sin indicación | 29 |
| `wsicdb` | Registro de beneficios fiscales en el impuesto a los créditos y débitos bancarios | No indicado | `https://fwshomo.afip.gov.ar/wsicdb/IcdbService?wsdl` [V] (el manual da `arca.gov.ar`, **[X] DNS**) | `https://serviciosjava.afip.gob.ar/wsicdb/IcdbService?wsdl` [V] | `/ws/registroICBD/manual_wsicdb1.2.pdf` | Sin indicación | 8 |
| `wsremharina` | Remito electrónico de harinas de trigo y subproductos | `wsremharina` | `https://fwshomo.afip.gov.ar/wsremharina/RemHarinaService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsremharina/RemHarinaService?wsdl` [V] | `/ws/remitoHTSDMT/Manual_Desarrollador_WSREMHARINA_v2.9.pdf` | Sin indicación | 29 |
| `wsremazucar` | Remito electrónico de azúcar, alcohol y subproductos | `wsremazucar` | `https://fwshomo.afip.gov.ar/wsremazucar/RemAzucarService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsremazucar/RemAzucarService?wsdl` [V] | `/ws/remitoElecAzucar/Manual-DesarrolladorWSREMAZUCAR%20v2_0_9.pdf` | Sin indicación | 27 |
| `wsremcarne` | Remito electrónico cárnico | `wsremcarne` | `https://fwshomo.afip.gov.ar/wsremcarne/RemCarneService?wsdl` [V] | `https://serviciosjava.afip.gob.ar/wsremcarne/RemCarneService?wsdl` [V] | `/ws/remitoElecCarnico/Manual_Desarrollador_WSREMCARNE_v3_6.pdf` | Sin indicación | 29 |
| `sire-ws` (Sistema Integral de Retenciones Electrónicas) | El agente de retención emite y anula el certificado F2005 (IVA) | `sire-ws` | `https://ws-aplicativos-reca.homo.afip.gob.ar/sire/ws/v1/c2005/2005?wsdl` [V] | `https://ws-aplicativos-reca.afip.gob.ar/sire/ws/v1/c2005/2005?wsdl` [V] | `/ws/sistemaIntegralRetenElect/SOAP-SIRE-IVA-Manualparaeldesarrollador_V1_0_0.pdf` | Sin indicación | 3 |
| TRABAJO_F931 (SSF931) | Organismos consultan remuneraciones declaradas en el F931 | Requiere TA de WSAA; el ID no está indicado | Solo aparece `POST /WebService/F931.asmx`, **sin host** | — | `/ws/TRABAJO_F931/TrabajoF931-ManualParaElDesarrollador.pdf` | **NO VERIFICADO** | — |
| `wssv` (Seguimiento Vehicular) | Prestadores OLS informan posición y estado de precintos electrónicos | No indicado | `https://wswhomo.afip.gov.ar/wssv/service.asmx?WSDL` [V] (el manual da `wswhomo.arca.gov.ar`, **[X] DNS**) | Manual: `https://wsw.arca.gob.ar/wssv/service.asmx` **[X]**, certificado TLS de otro nombre; `wsw.afip.gov.ar` da conexión reseteada | `/ws/WSSV/WSSV-ManualParaElDesarrollador.pdf` | Producción **NO VERIFICADA** | 7 |

### 3.4 Aduana (DIA)

Todos son SOAP ASMX con WSAA. El acceso está restringido por tipo de agente aduanero (OTEN, PSAD, DEPO, etc., según catalogo.asp) y los manuales no indican el service ID. La producción corre en `servicios3.arca.gob.ar`, que sí funcionó con TLS.

| Servicio | Qué hace | Homologación | Producción | Manual | Ops |
|---|---|---|---|---|---|
| `WDiaUtiDES` (Actualización/Consulta PEMA) | Prestadores PEMA actualizan dispositivos DES | `https://testdia.afip.gov.ar/dia/ws/WDiaUtiDES/WDiaUtiDES.asmx?WSDL` [V] | `https://servicios3.arca.gob.ar/Dia/Ws/WDiaUtiDES/WDiaUtiDES.asmx?WSDL` [V] | `/ws/WDIAUTIDES/ManualDesarrollador-WdiaUtiDEs.pdf` | 8 |
| `WGesINV` | El INV aprueba o deniega despachos de vitivinicultura | Manual: `https://testdia.homo.afip.gob.ar/...` **[X]** (sin conexión) | `https://servicios3.arca.gob.ar/Dia/Ws/WGesINV/WGesINV.asmx?WSDL` [V] | `/ws/WGESINV/wgesinv-ManualParaElDesarrollador.pdf` | 7 |
| `wgesTabRef` (Consulta de Tablas de Referencia) | Tablas de referencia MARIA. El catálogo dice: "A ser reemplazado por el wGesTabRef" | `https://testdia.afip.gob.ar/Dia/ws/wgesTabRef/wgesTabRef.asmx?WSDL` [V] | `https://servicios3.arca.gob.ar/Dia/ws/wgesTabRef/wgesTabRef.asmx?WSDL` [V] | `/ws/documentos/Manual_del_Desarrollador_wgestabref.pdf` (V1.1) | 13 |
| `wConsDepFiel` | Estados de legajos de Depositario Fiel | `https://testdia.afip.gov.ar/Dia/Ws/wConsDepFiel/wConsDepFiel.asmx?WSDL` [V] | `https://servicios3.arca.gob.ar/Dia/Ws/wConsDepFiel/wConsDepFiel.asmx?WSDL` [V] | `/ws/documentos/Manual-Desarrollador-wConsDepFiel.pdf` (V1.7) | 3 |
| `wgestiendaslibres` | Control de stock en tiendas libres | Sin URL en el manual | — | `/ws/documentos/ManualDesa-wgestiendaslibres.pdf` | — |
| `wgesprecintosdepfis` (WSCES, Coraza Electrónica de Seguridad) | Precintos de depositario fiscal. El catálogo marca su RG 3871/16 como "Derogada" | `https://testdia.afip.gob.ar/Dia/Ws/wgesprecintosdepfis/wgesprecintosdepfis.asmx?WSDL` [V] | `https://servicios3.arca.gob.ar/Dia/Ws/wgesprecintosdepfis/wgesprecintosdepfis.asmx?WSDL` [V] | `/ws/WSCES/ManualDesa-wgesprecintosdepfis.pdf` | 8 |
| `wDigDepFiel` | Digitalización de legajos | `https://testdia.afip.gov.ar/Dia/Ws/wDigDepFiel/wDigDepFiel.asmx?WSDL` [V] | `https://servicios3.arca.gob.ar/Dia/Ws/wDigDepFiel/wDigDepFiel.asmx?WSDL` [V] | `/ws/documentos/ManualDesaDigDepFiel.pdf` | 3 |
| `WutiGOPDeclaraciones` (Grandes Operadores, salidas GOP) | Declaraciones domiciliarias de grandes operadores | Sin URL completa en el manual | `https://servicios3.arca.gob.ar` (solo el host) | `/USAduaneros/documentos/WutiGOPDeclaraciones_NuevoEsquema_Manual-Usuario_.pdf` | — |
| `wdepMovimientos` | Salidas de zona primaria y movimientos de terminales y depositarios | `https://testdia.afip.gov.ar/dia/ws/wdepMovimientos/wdepMovimientos.asmx?WSDL` [V] | `https://servicios3.arca.gob.ar/dia/ws/wdepMovimientos/wdepMovimientos.asmx?WSDL` [V] | `/ws/WDEPMOVIMIENTOS/wdepmovimientos-ManualParaElDesarrollador.pdf` | 6 |
| `wEnysa` | Recibe de Chile los eventos de entrada y salida de vehículos | `https://testdia.afip.gov.ar/DIA/WS/wEnysa/wEnysa.asmx?WSDL` [V] | `https://servicios3.arca.gob.ar/DIA/WS/wEnysa/wEnysa.asmx?WSDL` [V] | `/ws/wEnysa/wEnysa-ManualDesarrollador.pdf` | 6 |
| `WSSV` | Ver 3.3 | | | | |

## 4. Lo que aprendimos de los endpoints reales

### 4.1 Hay tres "dialectos" SOAP

ArcaSim tiene que imitar los tres, porque el cliente los distingue. Se verificó llamando a homologación el 2026-10-01.

| Dialecto | Servicios | Autenticación en el body | Error de autenticación | Header de respuesta |
|---|---|---|---|---|
| **.NET ASMX** (`wswhomo` / `servicios1`) | wsfev1, wsfexv1, wsbfev1, wsseg, wscdc, wsagr, wssv | `<Auth><Token/><Sign/><Cuit/></Auth>` | **HTTP 200** con el error *dentro* del resultado (`Errors/Err` o `FEXErr`) | `soap:Header/FEHeaderInfo` con `ambiente`, `fecha` e `id` (versión) |
| **Java** (`fwshomo` / `serviciosjava`) | wsmtxca, wsct, wslpg, wsfecred, remitos, etc. | `<authRequest><token/><sign/><cuitRepresentada/></authRequest>` (wsmtxca) | El manual de wsmtxca dice `soapenv:Fault` con `faultcode soapenv:Client`, por ejemplo "Token vencido ..." | — |
| **Padrón** (`awshomo` / `aws`) | ws_sr_* | `token`, `sign`, `cuitRepresentada` sueltos, sin wrapper | **HTTP 500** con `soap:Fault`, `faultcode soap:Server` | — |

Respuestas reales observadas (homologación, 2026-10-01):

- **Padrón A5 y A13, `dummy`:** `<return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return>`, HTTP 200.
- **Padrón A5 y A13 con `token` = "abc":** `faultstring` "Token malformado", HTTP 500.
- **Padrón A5 sin `token` ni `sign`:** `faultstring` "Falta token y/o sign." con `<detail><ns1:SRValidationException/></detail>`, HTTP 500.
- **Padrón A5 con un token XML bien formado pero firma falsa:** `faultstring` "No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token>", HTTP 500.
- **wscdc, `ComprobanteDummy`:** HTTP 200, header `FEHeaderInfo` con `<ambiente>Homologacion-Ext - srt</ambiente>` y `<id>3.1.1.0</id>`; body `AppServer`/`DbServer`/`AuthServer` = OK.
- **wscdc con token falso:** HTTP 200 con `<Errors><Err><Code>600</Code><Msg>ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</Msg></Err></Errors><Events><Evt><Code>0</Code></Evt></Events>`.
- **wsfexv1 con token falso:** HTTP 200 con `<FEXErr><ErrCode>1000</ErrCode><ErrMsg>Usuario no autorizado a realizar esta operacion. ValidacionDeToken: ...</ErrMsg></FEXErr>`. Además trae `FEXEvents` con `EventCode` 103 y un aviso de mantenimiento. `FEHeaderInfo` con `<id>5.0.1.0</id>`.
- **wsmtxca, `dummy`:** HTTP 200 con `appserver`, `authserver` y `dbserver` directamente bajo `ns1:dummyResponse`, sin `<return>`.
- **wsmtxca, `consultarTiposComprobante` con token falso:** HTTP 200 **sin `Content-Type`** y con el body de texto plano `BL<número> <fecha hora> 500`, no SOAP. Parece un gateway o WAF delante del servicio; **NO VERIFICADO** qué lo dispara. No contradice el Fault del manual, pero muestra que un cliente real puede recibir algo que no es SOAP.

### 4.2 Hostnames: los manuales nuevos ponen dominios que no funcionan

Varios manuales de 2025-2026 reemplazaron `afip` por `arca` en las URLs ("Modificación links WSDL de afip a arca", historial del manual de constancia 4.1). Probado el 2026-10-01:

- `aws.arca.gob.ar` y `awshomo.arca.gob.ar` (padrón) responden. `awshomo.arca.gob.ar` falló una vez al primer intento.
- `servicios1.arca.gob.ar` resuelve, pero su certificado es para `servicios1.afip.gov.ar`, así que TLS falla (`SEC_E_WRONG_PRINCIPAL`). Esto afecta a **wscdc en producción, según su propio manual**.
- `*.arca.gov.ar` (`fwshomo.arca.gov.ar`, `awshomo.arca.gov.ar`, `wswhomo.arca.gov.ar`) **no resuelve en DNS**. Es la URL que traen los manuales de wsrgiva, wsltv, wsicdb, wssv y DDJJ.
- `servicios3.arca.gob.ar` (aduana) y `*.arca.gob.ar` de SETIWS sí funcionan.
- Todos los hosts `*.afip.gov.ar` y `*.afip.gob.ar` probados funcionan.

Para ArcaSim, la URL base tiene que ser configurable por servicio. "Cambiar solo URLs" significa cambiar hacia los hosts que de verdad funcionan, no necesariamente hacia los del manual.

La página https://www.afip.gob.ar/ws/documentacion/servicios-migrados.asp lista 31 hosts como "Migrado", entre ellos `wswhomo`, `awshomo`, `fwshomo`, `wsaahomo` y `servicioscf`. No explica a qué se migró.

### 4.3 Trampa de namespace en wsmtxca

Los ejemplos del manual usan `xmlns:ser="http://impl.service.wsmtxca.afip.gob.ar/service/"`, pero el WSDL real declara `targetNamespace="http://impl.service.wsmtxca.afip.gov.ar/service/"` (`.gov.ar`). El simulador tiene que seguir al WSDL, y conviene decidir y documentar si tolera el namespace del manual.

## 5. Detalle: servicios que una app de facturación o punto de venta usaría además de wsfev1

### 5.1 Padrón: `ws_sr_constancia_inscripcion` (ex `ws_sr_padron_a5`)

- Fuente: manual V4.1, `https://www.afip.gob.ar/ws/WSCI/manual_ws_sr_ws_constancia_inscripcion.pdf`.
- WSDL: `wsdl/ws_sr_constancia_inscripcion-homologacion.wsdl`, con namespace `http://a5.soap.ws.server.puc.sr/` y servicio `PersonaServiceA5`.

**Operaciones** (WSDL):

| Operación | Entrada | Qué devuelve |
|---|---|---|
| `dummy` | — | `appserver`, `authserver`, `dbserver` (OK/ERROR). No requiere token |
| `getPersona_v2` | `token`, `sign`, `cuitRepresentada`, `idPersona` | `personaReturn` con `metadata`, `datosGenerales`, `datosRegimenGeneral`, `datosMonotributo`, `errorConstancia`, `errorRegimenGeneral` y `errorMonotributo` |
| `getPersonaList_v2` | lo mismo, con `idPersona` repetido **hasta 250 veces** | `personaListReturn` con `metadata` y un `persona` por cada clave pedida |
| `getPersona` / `getPersonaList` | igual que v2 | Se mantienen "para conservar la compatibilidad". El manual recomienda v2, "que incluye todas las actividades del monotributista y las caracterizaciones vigentes" |

**Campos de respuesta** (sección 4 del manual):

- `cuitRepresentada` "debe coincidir con alguna de las CUITs listadas en la sección relations del token".
- `datosGenerales`: `idPersona`, `tipoPersona` (FISICA/JURIDICA), `tipoClave` (CUIT/CUIL/CDI), `estadoClave` (ACTIVO/INACTIVO), `nombre`, `apellido`, `razonSocial`, `esSucesion`, `mesCierre`, `fechaContratoSocial`, `dependencia`, `domicilioFiscal` y `caracterizacion` (lista).
- `domicilioFiscal`: `tipoDomicilio`, `direccion`, `localidad`, `codPostal`, `idProvincia`, `descripcionProvincia`, `tipoDatoAdicional` y `datoAdicional`.
- `datosRegimenGeneral`: `impuesto[]`, `regimen[]`, `actividad[]` y `categoriaAutonomo`.
- `datosMonotributo`: `impuesto[]`, `actividadMonotributista`, `categoriaMonotributo`, `componenteDeSociedad` y `actividad[]`.
- `impuesto`: `idImpuesto`, `descripcionImpuesto`, `estadoImpuesto` (por ejemplo `AC`), `motivo` y `periodo`. En los ejemplos del manual, IVA es `idImpuesto` 30 y MONOTRIBUTO es 20.
- `caracterizacion`: `idCaracterizacion`, `descripcionCaracterizacion`, `periodo` y `fechaSolicitud`. Este último campo es nuevo desde el 11/2/2026 (catalogo.asp). La caracterización 639, "GANANCIAS SIMPLIFICADA LEY 27.779", marca la adhesión al régimen de DJ simplificada de Ganancias.

**Condición frente al IVA.** El servicio **no devuelve un campo "condición IVA"**. Hay que deducirla de qué `impuesto` aparece activo y en qué bloque: IVA en `datosRegimenGeneral` o monotributo en `datosMonotributo`. La regla exacta, por ejemplo para exentos o no alcanzados, **no está en el manual (NO VERIFICADO)** y la tiene que definir el cliente. ArcaSim debería devolver datos coherentes con lo que acepta `CondicionIVAReceptorId` de wsfev1.

**Comportamiento de error:**

- Los errores de autenticación son SOAP Fault con HTTP 500. Están verificados en vivo; ver 4.1.
- Los mensajes de la tabla 5.3 del manual, como "La clave ingresada no es una CUIT", "La CUIT ... fue limitada en los términos de la RG AFIP 3832/16", "Domicilio Incompleto" y "Nombre erróneo", salen en `errorConstancia/error` dentro de una respuesta normal. Eso dice el tipo `errorConstancia` del manual. `errorRegimenGeneral/mensaje` y `errorMonotributo/mensaje` tienen textos fijos: "No cumple con las condiciones para enviar datos del regimen general" y "... monotributo".
- **CUIT inexistente en `getPersonaList_v2`:** sale un `persona` que contiene solo `<errorConstancia><error>No existe persona con ese Id</error><idPersona>12345678901</idPersona></errorConstancia>` (ejemplo del manual).
- Para el mismo caso en `getPersona_v2`, el formato (fault o `errorConstancia`) está **NO VERIFICADO**: el manual no lo muestra y no se pudo probar sin un certificado de homologación.

**Respuesta de ejemplo** (manual, sección 3.2.3, recortada):

```xml
<ns2:getPersona_v2Response xmlns:ns2="http://a5.soap.ws.server.puc.sr/">
  <personaReturn>
    <datosGenerales>
      <apellido>SKIOGEK OGIUPE</apellido>
      <caracterizacion>
        <descripcionCaracterizacion>GANANCIAS SIMPLIFICADA LEY 27.779</descripcionCaracterizacion>
        <fechaSolicitud>20260220</fechaSolicitud>
        <idCaracterizacion>639</idCaracterizacion>
        <periodo>20250101</periodo>
      </caracterizacion>
      <domicilioFiscal>
        <codPostal>7007</codPostal>
        <datoAdicional>BARRIO ANTONIO CARLOS</datoAdicional>
        <descripcionProvincia>BUENOS AIRES</descripcionProvincia>
        <direccion>SAN MANUEL 9</direccion>
        <idProvincia>1</idProvincia>
        <localidad>SAN MANUEL</localidad>
        <tipoDatoAdicional>BARRIO</tipoDatoAdicional>
        <tipoDomicilio>FISCAL</tipoDomicilio>
      </domicilioFiscal>
      <esSucesion>NO</esSucesion>
      <estadoClave>ACTIVO</estadoClave>
      <idPersona>20164755100</idPersona>
      <mesCierre>12</mesCierre>
      <nombre>OGIEK KPSGR</nombre>
      <tipoClave>CUIT</tipoClave>
      <tipoPersona>FISICA</tipoPersona>
    </datosGenerales>
    <datosRegimenGeneral>
      <actividad>
        <descripcionActividad>CULTIVO DE CEREALES N.C.P., EXCEPTO LOS DE USO FORRAJERO</descripcionActividad>
        <idActividad>11119</idActividad><nomenclador>883</nomenclador><orden>1</orden><periodo>201311</periodo>
      </actividad>
      <impuesto>
        <descripcionImpuesto>IVA</descripcionImpuesto><estadoImpuesto>AC</estadoImpuesto>
        <idImpuesto>30</idImpuesto><motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo><periodo>199911</periodo>
      </impuesto>
    </datosRegimenGeneral>
    <metadata>
      <fechaHora>2026-02-26T15:52:37.653-03:00</fechaHora>
      <servidor>qa-sr-padron-ws.cloudhomo.afip.gob.ar</servidor>
    </metadata>
  </personaReturn>
</ns2:getPersona_v2Response>
```

En el XML real los nombres de elemento salen en **orden alfabético**, y `metadata` aparece al final aunque el esquema del manual la ponga primero.

### 5.2 Padrón: `ws_sr_padron_a13`

- Fuente: manual V1.4, `https://www.afip.gob.ar/ws/ws-padron-a13/manual-ws-sr-padron-a13-v1.4.pdf`.
- WSDL: `wsdl/ws_sr_padron_a13-homologacion.wsdl`, con namespace `http://a13.soap.ws.server.puc.sr/`.

| Operación | Entrada | Devuelve |
|---|---|---|
| `dummy` | — | `appserver`, `authserver` y `dbserver` |
| `getPersona` | `token`, `sign`, `cuitRepresentada`, `idPersona` | `personaReturn` con `metadata` y `persona` |
| `getPersonaV2` | lo mismo | Igual que `getPersona`, pero "permitiendo consultar la información aún cuando la clave se encuentre en estado INACTIVA" |
| `getIdPersonaListByDocumento` | `token`, `sign`, `cuitRepresentada`, `documento` | `idPersonaListReturn` con `idPersona[]` (todas las CUIT/CUIL de ese DNI) y `metadata` |

- **`persona`:** `idPersona`, `tipoPersona`, `tipoClave`, `estadoClave`, `nombre`, `apellido`, `razonSocial`, `tipoDocumento` (LC, LE, CI, DNI, PAS, etc.; tabla 5.1), `numeroDocumento`, `mesCierre`, `fechaInscripcion`, `fechaContratoSocial`, `formaJuridica`, `fechaFallecimiento`, `fechaNacimiento`, `idActividadPrincipal`, `descripcionActividadPrincipal`, `periodoActividadPrincipal`, `domicilio[]` (con `tipoDomicilio` FISCAL o LEGAL/REAL, `estadoDomicilio`, `calle`, `numero`, `direccion`, `localidad`, `codigoPostal`, `idProvincia` y `descripcionProvincia`) y `claveInactivaAsociada[]`.
- **A13 no trae impuestos**, así que no sirve para la condición frente al IVA. Su valor para una app de punto de venta es **DNI → CUIT** y el domicilio.
- **Mensajes de error** (tabla 5.3): "El Id de la persona no es valido", "La Clave (CUIT/CUIL) consultada es inexistente", "La clave (CUIT/CUIL) consultada se encuentra INACTIVA", "No autorizado, par token/sign invalido.", "Debe enviar la CUIT representada", "Este token no le permite actuar en representacion de la CUIT " + cuitRepresentada, y "Falta token y/o sign.". El manual no dice el transporte. Por lo observado en A5 y A13 (4.1), son `soap:Fault` con HTTP 500; para los mensajes de negocio esto es **inferencia, NO VERIFICADO**.
- Los valores de `formaJuridica` y `tipoDatoAdicional` vienen de `ws_sr_padron_a100` (`getParameterCollectionByName`, colecciones `SUPA.TIPO_EMPRESA_JURIDICA` y `SUPA.TIPO_DATO_ADICIONAL_DOMICILIO`).

**Respuesta de ejemplo** (manual, sección 3.2.3, recortada):

```xml
<ns2:getPersonaResponse xmlns:ns2="http://a13.soap.ws.server.puc.sr/">
  <personaReturn>
    <metadata><fechaHora>2018-11-28T14:55:19.023-03:00</fechaHora><servidor>127.0.0.1</servidor></metadata>
    <persona>
      <apellido>INTENTAR</apellido>
      <domicilio>
        <calle>AV LOS INCAS</calle><codigoPostal>5881</codigoPostal>
        <descripcionProvincia>SAN LUIS</descripcionProvincia><direccion>AV LOS INCAS 4137</direccion>
        <estadoDomicilio>CONFIRMADO</estadoDomicilio><idProvincia>11</idProvincia>
        <localidad>MERLO</localidad><numero>4137</numero><tipoDomicilio>FISCAL</tipoDomicilio>
      </domicilio>
      <estadoClave>ACTIVO</estadoClave>
      <idActividadPrincipal>551023</idActividadPrincipal>
      <idPersona>27015942210</idPersona>
      <nombre>JAZMIN</nombre>
      <numeroDocumento>1594221</numeroDocumento>
      <tipoClave>CUIT</tipoClave><tipoDocumento>LC</tipoDocumento><tipoPersona>FISICA</tipoPersona>
    </persona>
  </personaReturn>
</ns2:getPersonaResponse>
```

### 5.3 `wsmtxca`: factura con detalle de ítems

- Fuente: manual V0.25.8 (380 páginas), `https://www.afip.gob.ar/fe/ayuda/documentos/wsmtxca-RG-2904.pdf`.
- WSDL: `wsdl/wsmtxca-homologacion.wsdl`.

**Operaciones (27).** El manual (pág. 13) las agrupa así:

- **Solo CAE:** `autorizarComprobante`, `autorizarAjusteIVA` y `consultarPuntosVentaCAE`.
- **Solo CAEA:** `solicitarCAEA`, `informarComprobanteCAEA`, `informarAjusteIVACAEA`, `consultarPuntosVentaCAEA`, `informarCAEANoUtilizado`, `informarCAEANoUtilizadoPtoVta`, `consultarPtosVtaCAEANoInformados`, `consultarCAEA` y `consultarCAEAEntreFechas`.
- **Ambos:** `consultarUltimoComprobanteAutorizado`, `consultarComprobante`, `consultarTiposComprobante`, `consultarTiposDocumento`, `consultarAlicuotasIVA`, `consultarCondicionesIVA` (condición IVA **del ítem**), `consultarCondicionesIVAReceptor`, `consultarMonedas`, `consultarCotizacionMoneda`, `consultarUnidadesMedida`, `consultarPuntosVenta`, `consultarTiposTributo`, `consultarTiposDatosAdicionales`, `consultarActividadesVigentes` y `dummy`.

El manual aclara: "Un contribuyente sólo necesita implementar un cliente para los métodos del WS correspondientes a la RG por la cual esté alcanzado".

**Diferencias con wsfev1.** El lado de wsfev1 sale del WSDL `wsfev1-homologacion.wsdl` de esta misma carpeta.

| Aspecto | wsfev1 | wsmtxca |
|---|---|---|
| Comprobantes | A, B, C y M sin ítems (ws-factura-electronica.asp) | A y B con ítems. `codigoTipoComprobante` puede ser 1, 2, 3, 6, 7, 8, 51, 52 o 53, y además 201 a 208 de FCE MiPyMEs (validación 100, pág. 31) |
| Lote | `FECAESolicitar` recibe `FeCabReq` (`CantReg`, `PtoVta`, `CbteTipo`) más una lista de `FeDetReq`, y un detalle puede cubrir un rango con `CbteDesde`/`CbteHasta` | `autorizarComprobante` recibe **un** `comprobanteCAERequest` por llamada |
| Ítems | No hay | `arrayItems/item`: `unidadesMtx`, `codigoMtx` (código de producto, por ejemplo GTIN), `codigo`, `descripcion`, `cantidad`, `codigoUnidadMedida`, `precioUnitario`, `importeBonificacion`, `codigoCondicionIVA`, `importeIVA` e `importeItem` |
| IVA | Lista `Iva/AlicIva` en el detalle | `arraySubtotalesIVA/subtotalIVA` (`codigo`, `importe`), más la condición IVA por ítem |
| Autenticación | `Auth{Token, Sign, Cuit}` | `authRequest{token, sign, cuitRepresentada}` |
| Errores de autenticación y de estructura | Dentro de `Errors` en la respuesta (ASMX) | `soapenv:Fault` "excepcional" (manual, pág. 3). Además, en vivo se observó una respuesta no SOAP: ver 4.1 |
| Errores de negocio | `Errors/Err{Code, Msg}` y `Observaciones/Obs` | `arrayErrores/codigoDescripcion{codigo, descripcion}`, `arrayObservaciones` y `evento{codigo, descripcion}` |
| Resultado | `Resultado` A (aprobado), R (rechazado) o P (parcial) (manual wsfev1 V4.7) | `resultado` A (aprobado), O (observado) o R (rechazado) (pág. 23) |
| Stack y host | ASMX en `wswhomo` / `servicios1` | Java en `fwshomo` / `serviciosjava` |
| Extras | — | `autorizarAjusteIVA` e `informarAjusteIVACAEA`, `arrayCompradores`, `arrayDatosAdicionales{t, c1..c6}` y validaciones del emisor previas (códigos 10000 a 10003: CUIT activa, actividad, domicilio y alta en IVA) |
| Reintentos | `FECompUltimoAutorizado` / `FECompConsultar` | `consultarComprobante` / `consultarUltimoComprobanteAutorizado`. Reenviar un comprobante ya aceptado se rechaza "indicando un error de correlatividad" (pág. 7) |

### 5.4 `wsfexv1`: factura de exportación

- Fuente: manual V3.1.x, `https://www.afip.gob.ar/ws/documentacion/manuales/WSFEX-Manualparaeldesarrollador_V3.1.1_ARCA.pdf`.
- WSDL: `wsdl/wsfexv1-homologacion.wsdl`, con namespace `http://ar.gov.afip.dif.fexv1/`.
- WSAA: `service` = **`wsfex`**, no "wsfexv1" (manual 1.3).

**Operaciones (19):**

- Autorización y consulta: `FEXAuthorize`, `FEXGetCMP`, `FEXGetLast_CMP`, `FEXGetLast_ID` y `FEXCheck_Permiso` (verifica un permiso de embarque o país contra las bases aduaneras).
- Parámetros: `FEXGetPARAM_Cbte_Tipo`, `FEXGetPARAM_Tipo_Expo`, `FEXGetPARAM_Incoterms`, `FEXGetPARAM_Idiomas`, `FEXGetPARAM_UMed`, `FEXGetPARAM_DST_pais`, `FEXGetPARAM_DST_CUIT`, `FEXGetPARAM_MON`, `FEXGetPARAM_MON_CON_COTIZACION`, `FEXGetPARAM_Ctz`, `FEXGetPARAM_PtoVenta`, `FEXGetPARAM_Opcionales` y `FEXGetPARAM_Actividades`.
- `FEXDummy`.

**Diferencias con wsfev1:**

- **Comprobantes:** 19 (Factura de Exportación "E"), 20 (Nota de Débito) y 21 (Nota de Crédito por operaciones con el exterior), según el manual, pág. ~22.
- **Un comprobante por llamada, con `Id` de requerimiento propio:** reenviar el mismo `Id` devuelve el CAE ya otorgado con `<Reproceso>S</Reproceso>`; si el `Id` es nuevo, viene `N`. `FEXGetLast_ID` devuelve el último `Id` recibido (manual 1.5). **Es idempotencia explícita y ArcaSim debe implementarla.**
- **`ClsFEXRequest`** (WSDL): `Id`, `Fecha_cbte`, `Cbte_Tipo`, `Punto_vta`, `Cbte_nro`, `Tipo_expo`, `Permiso_existente`, `Permisos/Permiso{Id_permiso, Dst_merc}`, `Dst_cmp`, `Cliente`, `Cuit_pais_cliente`, `Domicilio_cliente`, `Id_impositivo`, `Moneda_Id`, `Moneda_ctz`, `CanMisMonExt`, `Obs_comerciales`, `Imp_total`, `Obs`, `Cmps_asoc`, `Forma_pago`, `Incoterms`, `Incoterms_Ds`, `Idioma_cbte`, `Items/Item{Pro_codigo, Pro_ds, Pro_qty, Pro_umed, Pro_precio_uni, Pro_bonificacion, Pro_total_item}`, `Opcionales`, `Fecha_pago` y `Actividades`.
  - Lleva ítems, a diferencia de wsfev1.
  - No discrimina IVA.
  - `Incoterms` es obligatorio solo si `Cbte_Tipo` = 19 y `Tipo_expo` = 1 (historial 1.0).
- **Respuesta `ClsFEXOutAuthorize`:** `Id`, `Cuit`, `Cbte_tipo`, `Punto_vta`, `Cbte_nro`, `Cae`, `Fch_venc_Cae`, `Fch_cbte`, `Resultado`, `Reproceso` y `Motivos_Obs`.
- **Errores:** un único `FEXErr{ErrCode, ErrMsg}`, en vez de la lista `Errors` de wsfev1; los eventos van en `FEXEvents{EventCode, EventMsg}`. "Para errores internos de infraestructura, los errores se devuelven en la misma estructura (FEXErr)" (manual 1.6). Verificado en vivo (4.1).
- **Header `FEHeaderInfo`:** `ambiente`, `fecha` e `id` (manual 1.4; también observado en vivo).

### 5.5 `wscdc`: constatación de comprobantes

- Fuente: manual V0.4, `https://www.afip.gob.ar/ws/WSCDCV1/WSCDC-manual-desarrollador-v4.pdf`.
- WSDL: `wsdl/wscdc-homologacion.wsdl`, con namespace `http://servicios1.afip.gob.ar/wscdc/`.
- WSAA: el manual no dice el service ID (**NO VERIFICADO**).

| Operación | Qué hace |
|---|---|
| `ComprobanteConstatar` | Recibe `Auth{Token, Sign, Cuit}` y `CmpReq{CbteModo, CuitEmisor, PtoVta, CbteTipo, CbteNro, CbteFch (yyyymmdd), ImpTotal, CodAutorizacion, DocTipoReceptor?, DocNroReceptor?, Opcionales?}`. Devuelve `CmpResp` (eco), `Resultado` A o R, `Observaciones`, `FchProceso`, `Events` y `Errors` |
| `ComprobantesModalidadConsultar` | Modalidades válidas para `CbteModo`. Las validaciones del manual hablan de CAE, CAEA y CAI |
| `ComprobantesTipoConsultar` | Tipos de comprobante |
| `DocumentosTipoConsultar` | Tipos de documento |
| `OpcionalesTipoConsultar` | Tipos de opcionales |
| `ComprobanteDummy` | `AppServer`, `DbServer` y `AuthServer` |

Reglas que el simulador puede reproducir con los datos que ya genera al emitir con wsfev1:

- "Los comprobantes pueden ser constatados por el emisor, por el receptor, o por cualquiera que tenga acceso al ws".
- Validaciones de `Auth`: 600 ("No se corresponden token y firma"), 601 ("CUIT representada no incluida en token") y 602 ("CUIT representada no se encuentra activa").
- Validaciones de formato (códigos 1 a 8, por ejemplo `PtoVta` entre 1 y 99998). Si fallan, devuelven `Resultado` R con `Errors`.
- Validaciones funcionales (100 a 108). Si fallan, devuelven `Resultado` R con **`Observaciones`**, no `Errors`:
  - 100: que el código de autorización exista.
  - 101: fecha desde 20130101.
  - 102 a 105: que la CUIT, el tipo, el punto de venta y el número coincidan con lo registrado bajo ese código.
  - 106 y 107: CAEA y fecha.
  - 108: rango de fechas del CAI.
- Errores de infraestructura: 500, 501, 502 y 503, en la misma estructura `Errors` (manual 1.3).

## 6. WSDLs guardados

En `D:\Proyectos\arcasim\docs\arca\wsdl\`. Todos se descargaron el 2026-10-01 con HTTP 200 `text/xml` y se validó que parsean como XML con raíz `definitions`:

| Archivo | Origen |
|---|---|
| `ws_sr_constancia_inscripcion-homologacion.wsdl` | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5?WSDL`. **También es el de `ws_sr_padron_a5`**: mismo endpoint, así que no se duplicó |
| `ws_sr_padron_a13-homologacion.wsdl` | `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA13?WSDL` |
| `ws_sr_padron_a4-homologacion.wsdl` | `.../personaServiceA4?WSDL` (extra) |
| `ws_sr_padron_a10-homologacion.wsdl` | `.../personaServiceA10?WSDL` (extra) |
| `ws_sr_padron_a100-homologacion.wsdl` | `https://awshomo.afip.gov.ar/sr-parametros/webservices/parameterServiceA100?WSDL` (extra) |
| `wsmtxca-homologacion.wsdl` | `https://fwshomo.afip.gov.ar/wsmtxca/services/MTXCAService?wsdl` |
| `wsfexv1-homologacion.wsdl` | `https://wswhomo.afip.gov.ar/wsfexv1/service.asmx?WSDL` |
| `wscdc-homologacion.wsdl` | `https://wswhomo.afip.gov.ar/WSCDC/service.asmx?WSDL` |
| `wsfecred-homologacion.wsdl` | `https://fwshomo.afip.gov.ar/wsfecred/FECredService?wsdl` (extra) |

Los archivos `wsaa-*.wsdl` y `wsfev1-*.wsdl` que ya estaban en la carpeta son de los colegas y no se tocaron.

## 7. Alcance recomendado para ArcaSim

**Criterio:** simular lo que una app de facturación o punto de venta llama en la práctica, en el orden en que lo llama, y nada que exija acuerdos especiales con ARCA o que pertenezca a un sector puntual.

### Primero, junto con WSAA y wsfev1 (que ya documentan los colegas)

1. **`ws_sr_constancia_inscripcion`**, con todas sus operaciones: `dummy`, `getPersona_v2`, `getPersonaList_v2` y las legacy `getPersona` / `getPersonaList`.
   - Es lo que hace un punto de venta al cargar un cliente por CUIT: razón social, domicilio y si es responsable inscripto o monotributista.
   - Sin esto, la app de prueba no puede completar el `CondicionIVAReceptorId` que hoy exige wsfev1 (RG 5616).
   - Es barato: 5 operaciones con la misma forma.
   - El simulador tiene que aceptar **los dos** service IDs (`ws_sr_constancia_inscripcion` y `ws_sr_padron_a5`) para el mismo endpoint, si se confirma que WSAA real los sigue emitiendo.
2. **`ws_sr_padron_a13`**: `dummy`, `getPersona`, `getPersonaV2` y `getIdPersonaListByDocumento`. Comparte stack, dialecto y base de contribuyentes ficticios con el anterior. Además resuelve el caso de un consumidor final que da su DNI.
3. **Una sola base de contribuyentes ficticios** para padrón, wsfev1 y wscdc, para que lo que dice el padrón sea coherente con lo que wsfev1 acepta o rechaza. También hay que imitar el dialecto padrón de errores: `soap:Fault` con HTTP 500, con los `faultstring` reales de la sección 4.1.

### Segundo

4. **`wscdc`**: 6 operaciones, chico. Cierra el ciclo: un comprobante emitido contra el simulador se puede constatar contra el simulador, con las validaciones 100 a 105 sobre datos que ArcaSim ya guarda. Sirve también para apps que *reciben* facturas.
5. **`wsfexv1`**: 19 operaciones, mismo dialecto ASMX que wsfev1, así que se reusa casi toda la infraestructura. Lo nuevo es la idempotencia por `Id`/`Reproceso` y las tablas de parámetros (incoterms, países, idiomas). Vale si el público objetivo incluye exportadores de servicios, que hoy son muchos contractors en Argentina.
6. **`wsmtxca`**: solo si hay demanda de factura con detalle de ítems. Es el más caro del grupo:
   - 27 operaciones y 380 páginas de validaciones.
   - Otro dialecto: Java, con `Fault` para errores de autenticación y `arrayErrores` para los de negocio.
   - La trampa de namespace de 4.3.
   
   Conviene empezar por el subconjunto CAE (`autorizarComprobante`, `consultarUltimoComprobanteAutorizado`, `consultarComprobante`, los `consultar*` de parámetros y `dummy`) y dejar CAEA y los ajustes de IVA para después.

### Tercero, opcional y barato si ya existe lo anterior

- **`ws_sr_padron_a100`** (`getParameterCollectionByName`), para servir provincias y formas jurídicas consistentes con A13. A4 y A10 cuestan poco con el mismo motor.
- **`wsfecred.consultarObligadoRecepcion`** y **`consultarMontoObligadoRecepcion`**: una app de facturación a grandes empresas las consulta para decidir si debe emitir Factura de Crédito MiPyME. El resto de wsfecred (cuentas corrientes, aceptación y rechazo) es flujo financiero, no de punto de venta.
- **`wsct`**, **`wsbfev1`** y **`wsseg`**: son de facturación, pero de nicho (turismo, bienes de capital, seguros de caución).

### No simular

- **Deprecados:** `wsfe`, `ws_sr_padron_a5` como servicio aparte (es un alias), Padrón N3/N10 y WSCREATEVEP. Nadie debería desarrollar contra ellos.
- **Agro y sectoriales:** `wslpg`, `wscpe` (75 operaciones), `wslsp`, `wslum`, `wslca`, `wsltv`, `wstabaco`, `wsremharina`, `wsremcarne`, `wsremazucar` y `wsagr`. Son de dominio específico y enormes, y ninguna app de facturación general los usa.
- **Aduana (DIA)**, los 10 servicios de la sección 3.4 más `wssv`: restringidos por tipo de agente aduanero.
- **De organismos, bancos o regímenes especiales:** `sud_*`, TRABAJO_F931, Mi Argentina, A.P.E., SETIWS-PAGO-API (además es REST con otro sistema de autenticación, WSAUTH), `wsddjj`, `veconsumerws`, `wsrgiva`, `wsicdb`, `wscec`, `wsapoc`, `sire-ws`, JAZA y `wscta`. El catálogo advierte que requieren "autorizaciones y acuerdos especiales", y simularlos no ayuda a desarrollar una app de facturación.

### Implicancias de diseño que salen del relevamiento

- La URL base tiene que ser **configurable por servicio** (4.2). Hay manuales oficiales con hosts que no resuelven o con TLS inválido.
- El simulador necesita tres "dialectos" de sobre y error (4.1), incluido `FEHeaderInfo` en los ASMX. Los clientes reales pueden depender del código HTTP: 200 con error adentro, o 500 con Fault.
- Un TA de WSAA vale para **un** service ID, y los IDs no siempre coinciden con el nombre del servicio: `wsfev1` usa `wsfe`, `wsfexv1` usa `wsfex` y `wsbfev1` usa `wsbfe`. ArcaSim debe validar que el TA presentado sea del service ID correcto.

## 8. Pendientes NO VERIFICADOS

- Service ID de WSAA de: `wsmtxca`, `wscdc`, `wsct`, `wscec`, `wsjaza`, `wslpg`, `wsltv`, `wslum`, `wsicdb`, `wsrgiva`, `wsagr`, `wssv`, `wscta`, TRABAJO_F931 y los servicios de Aduana. Sus manuales no lo dicen. Se puede confirmar en WSASS con un certificado de homologación.
- Si WSAA sigue emitiendo TA para `ws_sr_padron_a5`.
- El formato de error de `getPersona_v2` cuando el CUIT no existe.
- El transporte de los errores de negocio del padrón (Fault o campo de la respuesta). Solo se verificaron los de autenticación.
- El origen de la respuesta `BL<n> ... 500` de wsmtxca.
- La producción de `wssv`, `wscta` (homologación da 404), TRABAJO_F931 (sin host) y Automatización A.P.E. (sin documentación).
- El estado real de `wsbfe` frente a `wsbfev1`, y de `wsctg`.
