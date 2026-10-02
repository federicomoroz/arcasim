# ArcaSim: diseño

Un doble de los web services de ARCA para desarrollo y pruebas. Una aplicación que factura usa su cliente real de ARCA (WSAA + WSFEv1 y, más adelante, padrón y constatación) apuntado a ArcaSim mientras se desarrolla, en los tests y en las demos. En producción, el mismo cliente apunta a ARCA: se cambian las direcciones y el certificado en la configuración, no el código.

ArcaSim no es para producción ni emite comprobantes válidos. No tiene relación con ARCA.

Este diseño sale del estudio de la API pública de ARCA en [`docs/arca/`](arca/): [WSAA](arca/wsaa.md), [WSFEv1](arca/wsfev1.md) y [sus 496 validaciones](arca/wsfev1-codigos.md), el [catálogo de servicios](arca/catalogo.md) y la [normativa vigente](arca/normativa.md). Los WSDL oficiales están en [`docs/arca/wsdl/`](arca/wsdl/).

## 1. Qué significa "funciona exactamente como ARCA"

Un cliente no tiene que notar la diferencia. En concreto:

| Nivel | Qué tiene que coincidir | Cómo se verifica |
|---|---|---|
| Contrato | Las mismas rutas, los mismos WSDL (con la dirección del simulador), operaciones, namespaces y `SOAPAction`. SOAP 1.1 y 1.2. | Un cliente generado de los WSDL oficiales (`dotnet-svcutil`) corre contra ArcaSim en los tests. |
| Forma de la respuesta | Byte a byte donde importa: una sola línea en WSFEv1, `<CAE />`, el texto `NULL` en fechas vacías, el header `FEHeaderInfo`, los faults Axis de WSAA con HTTP 500, HTTP 400 vacío ante XML roto. | Tests "golden" contra las respuestas reales capturadas en el estudio. |
| Errores | Los mismos códigos, en el mismo orden, con los textos **reales** (no los del manual cuando difieren, incluidas las faltas de tildes). | Tabla de códigos con el estado de cada uno: implementado, no implementado o no verificable. |
| Comportamiento | Numeración correlativa por CUIT, punto de venta y tipo; sin idempotencia (reenviar da 10016); un lote se corta en el primer rechazo; TA de 12 h; ventana anti-repetición. | Tests de escenarios. |

Lo que **no se puede** igualar se dice en la documentación de ArcaSim:
- **Tokens:** los TA de ArcaSim solo sirven contra ArcaSim, porque la firma real la pone ARCA.
- **Padrón:** las consultas al padrón real se reemplazan por contribuyentes ficticios.
- **Certificado TLS:** no puede ser el de `*.afip.gov.ar`.
- **Detalles sin documentar:** lo que ARCA no publica (por ejemplo, el orden de algunos errores del TRA) queda marcado como no verificado hasta poder compararlo con homologación.

## 2. Arquitectura

- **.NET 8, ASP.NET Core.** Una capa SOAP propia y chica en lugar de CoreWCF: hay que reproducir tres "dialectos" distintos de ARCA (ASMX de .NET en WSFEv1, Axis en WSAA, Java en el padrón) con sus rarezas, y un framework SOAP genérico las normaliza.
- **Un módulo por servicio de ARCA**, cada uno con su ruta real: `/ws/services/LoginCms` (WSAA), `/wsfev1/service.asmx` (WSFEv1), y más adelante `/sr-padron/webservices/...` (padrón) y `/WSCDC/service.asmx` (constatación).
- **Reglas como datos.** Las validaciones de `FECAESolicitar` son una tabla de reglas con su código, si es error u observación, si rechaza, y desde qué fecha rige. Así una resolución nueva es una fila nueva, y el informe de cobertura sale de la misma tabla.
- **Perfiles.** Ambiente (`homologacion` | `produccion`: textos de `FEHeaderInfo`, `source` del TA, ventana anti-repetición de 10 o 2 min) y versión del manual (`4.7` | `4.8`: si `CondicionIVAReceptorId` falta, observa 10245 o rechaza 10246). Por defecto, la regla que rige en la fecha simulada.
- **Reloj inyectable.** Para probar vencimientos del TA, la ventana de fechas de `CbteFch`, las quincenas del CAEA y el 01/12/2026 sin esperar.
- **Persistencia detrás de puertos:** PostgreSQL para uso compartido y un proveedor en memoria para los tests de las aplicaciones (arranca en milisegundos y se descarta).

## 3. Lo que agrega ArcaSim (no existe en ARCA)

Una API de administración bajo `/arcasim/` y un panel web:
- **Contribuyentes ficticios:** CUIT, razón social, condición frente al IVA y puntos de venta habilitados (tipo RECE o CAEA). Es la misma base para WSFEv1 y el padrón, así que consultar un CUIT y facturarle dan resultados coherentes.
- **Certificados y relaciones**, en lugar de WSASS: una autoridad certificante propia que emite certificados desde un CSR, con el DN que exige ARCA. Las relaciones entre alias, CUIT y servicio. Opcionalmente, confiar en la autoridad de homologación de ARCA, para usar el mismo certificado de prueba contra ArcaSim y contra ARCA.
- **Casos de prueba que con ARCA real son difíciles de provocar:**
  - el servicio caído;
  - una demora;
  - rechazar el próximo comprobante con un código dado;
  - "CAE otorgado y respuesta perdida" (para ejercitar `FECompConsultar` después de un timeout);
  - vencer los tokens.
- **Reloj:** fijarlo o adelantarlo.
- **Comprobantes emitidos:** buscarlos y ver qué se mandó y qué se respondió, y reiniciar todo.

## 4. Etapas

1. **WSAA + WSFEv1 completos.**
   - `loginCms`: validación del CMS en el orden real, TA y token `sso` con el formato real, ventana anti-repetición y todos los faults con sus textos reales.
   - Las 22 operaciones de WSFEv1, con `Auth` validado en el orden y con los textos reales.
   - `FECAESolicitar` con las validaciones del manual, empezando por las que tocan a cualquier factura: fechas, importes, clases A/B/C, receptor y consumidor final, IVA, moneda, condición IVA del receptor, numeración y lotes. Las de comprobantes asociados, FCE MiPyME y exportación entran después.
   - CAE de 14 dígitos con vencimiento a 10 días (regla inferida de todos los ejemplos, configurable). `FECompConsultar`, `FECompUltimoAutorizado`, `FECompTotXRequest`, las tablas de parámetros y el CAEA de contingencia.
   - Panel y API de administración.
2. **Padrón:** `ws_sr_constancia_inscripcion` y `ws_sr_padron_a13` sobre los contribuyentes ficticios.
3. **Constatación** (`wscdc`) sobre los comprobantes emitidos, y **exportación** (`wsfexv1`).

Fuera de alcance: los servicios deprecados, agro y remitos, aduana, y los servicios para organismos y bancos (ver [catálogo, sección 7](arca/catalogo.md)).

## 5. El "módulo" en las aplicaciones

El repo trae además **`Arca.Client`**, una librería .NET con el cliente real de WSAA y WSFEv1:
- firma el TRA con el certificado;
- guarda el TA hasta que vence;
- arma los comprobantes;
- distingue un error que conviene reintentar de un rechazo definitivo.

Facturación de Comanda la usa en lugar de su simulador interno, y cualquier otra aplicación puede hacer lo mismo. El cambio de ArcaSim a ARCA es su configuración:

```json
"Arca": {
  "WsaaUrl": "https://localhost:7443/ws/services/LoginCms",
  "WsfeUrl": "https://localhost:7443/wsfev1/service.asmx",
  "Certificate": "certs/arcasim-30000000007.pfx",
  "Cuit": "30000000007"
}
```

En homologación y producción solo cambian las dos URL y el certificado. Si el cliente confía en el certificado TLS de ArcaSim solo en desarrollo, ese ajuste también queda en la configuración del entorno.

## 6. Cómo se verifica la fidelidad

- **Contrato:** el cliente generado de los WSDL oficiales corre contra ArcaSim en los tests.
- **Respuestas reales:** las capturas del estudio (homologación, 2026-10-01, y respuestas grabadas de 2021) son fixtures; la respuesta de ArcaSim al mismo pedido tiene que coincidir.
- **Cobertura:** la tabla de 496 códigos genera un informe de cuáles están implementados.
- **Comparación con ARCA** (cuando haya un certificado de homologación): un script corre los mismos escenarios contra ArcaSim y contra homologación y compara las respuestas normalizadas. Cierra las preguntas abiertas del estudio, como el orden de los errores del TRA o si se compara con `>=` o `>` en el umbral de consumidor final.

## 7. Preguntas abiertas que el diseño deja configurables

- La regla del vencimiento del CAE (inferida: fecha del comprobante + 10 días).
- `RegXReq` en producción (250 en homologación en 2021).
- Si el umbral de $10.000.000 para identificar al consumidor final se compara con `>=` o `>`.
- La zona horaria con la que ARCA calcula "la fecha del pedido" para la ventana de `CbteFch`.
- Los textos reales de los códigos que nunca se capturaron. Mientras tanto se usa el texto del manual, marcado como tal.
