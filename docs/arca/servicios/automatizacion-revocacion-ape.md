# Automatización Res. Revocación A.P.E. (Obras Sociales)

Web service por el cual la **A.P.E.** ("organismo A.P.E. del Ministerio de Salud", según el catálogo) envía automáticamente a ARCA las **Resoluciones de Revocación** (RR) que emite para cobrar deudas de las Obras Sociales con ese organismo. ARCA procesa las RR en el sistema S.T.E. (Sistema de Transferencias Externas), que retiene los fondos adeudados de la distribución previsional de cada obra social y se los transfiere a la A.P.E. mediante Notas de Transferencia enviadas al Banco Nación. Norma: Decreto 213/04.

Lo usa un único consumidor: la A.P.E.

Fuente única: la entrada del catálogo `https://www.afip.gob.ar/ws/documentacion/catalogo.asp` (consultada el 2026-10-02), que solo trae el texto descriptivo y un enlace al Decreto 213/04 (`http://biblioteca.afip.gob.ar/dcp/DEC_C_000213_2004_02_19`). **No enlaza manual, WSDL ni URL.** Una búsqueda web del 2026-10-02 tampoco encontró documentación técnica.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | NO VERIFICADO (el catálogo lo presenta como un WS SOAP más, con WSAA) |
| Endpoint homologación | NO VERIFICADO |
| Endpoint producción | NO VERIFICADO |
| Namespace | NO VERIFICADO |
| Archivo de definición | Ninguno: no hay WSDL ni manual publicados |
| WSAA service id | NO VERIFICADO |
| Versión | — |

## Autenticación

NO VERIFICADO. Por la arquitectura común del catálogo (sección 1), sería un TA de WSAA, pero ni el campo ni su forma están documentados.

## Operaciones

NO VERIFICADO. Del texto del catálogo solo se deduce un flujo: **enviar una Resolución de Revocación** (alta) y, quizás, consultar su procesamiento. Ningún nombre de operación, elemento ni tipo está publicado.

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| — | Envío de RR (deducido del catálogo) | NO VERIFICADO | NO VERIFICADO | Crearía una RR a procesar por S.T.E. |

## Errores

NO VERIFICADO.

## Comportamiento a simular

**No simular** hasta conseguir documentación: no hay contrato que replicar y el único cliente es un organismo puntual. Si ArcaSim necesita listarlo, que responda como servicio "no implementado" y lo documente así.

## No verificado

Todo el contrato: endpoints, WSDL, service id, operaciones, errores, y si el servicio sigue vigente.
