# sire-ws

SIRE (Sistema Integral de Retenciones Electrónicas), certificado **F2005 de retención de IVA**. El agente de retención emite desde sus sistemas el certificado de cada retención de IVA (impuesto 216) que practica, y lo puede anular. ARCA devuelve el número de certificado y un código de seguridad para imprimirlo desde el micrositio SIRE. Lo usa cualquier agente de retención con certificado asociado al servicio "SIRE".

Fuentes:

- Manual "Certificado de retención electrónica", versión 1.0.3 (19/10/2020), aunque el nombre del archivo diga V1_0_0: `https://www.afip.gob.ar/ws/sistemaIntegralRetenElect/SOAP-SIRE-IVA-Manualparaeldesarrollador_V1_0_0.pdf`.
- WSDL y XSD de homologación, descargados el 2026-10-02. El de producción es igual salvo las URLs absolutas (el `schemaLocation` del XSD y el `soap:address` apuntan al host de cada ambiente).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java. Las respuestas están armadas con plantillas de texto: llevan saltos de línea y espacios **dentro** de los valores (ver abajo) |
| SOAP | 1.1, document/literal |
| Endpoint homologación | `https://ws-aplicativos-reca.homo.afip.gob.ar/sire/ws/v1/c2005/2005` |
| Endpoint producción | `https://ws-aplicativos-reca.afip.gob.ar/sire/ws/v1/c2005/2005` |
| WSDL | `?wsdl`. Importa `C2005_schema.xsd` con URL **absoluta** del mismo host: `https://ws-aplicativos-reca.homo.afip.gob.ar/sire/ws/v1/c2005/C2005_schema.xsd` |
| targetNamespace | `http://www.afip.gob.ar/sire/c2005/` (XSD con `elementFormDefault="unqualified"`, `version="1.0"`) |
| portType / binding / service | `C2005Soap` / `C2005SoapServiceSoapBinding` / `C2005SoapService`, port `C2005SoapPort` |
| SOAPAction | `""` en las tres operaciones |
| Header de respuesta | `tns:HeaderInfo{ambiente, fecha, vCore, v2005}`. El binding solo lo declara en la salida de `dummy`, pero en vivo también viene en los Fault, y el manual lo muestra en `emitir` y `anular` |
| Archivos guardados | `wsdl/sire-ws-homologacion.wsdl` y `wsdl/sire-ws/C2005_schema.xsd` |
| WSAA service id | `sire-ws` (manual 1.2 y 2.2). El certificado se asocia al WS de negocio "SIRE" |
| Versión | Header en vivo (2026-10-02): `vCore` 2020.4, `v2005` 2020.7, `ambiente` "homologacion - 207" |

## Autenticación

Los tres campos van **sueltos** como primeros hijos del elemento de operación, sin wrapper:

```xml
<c20:anular xmlns:c20="http://www.afip.gob.ar/sire/c2005/">
  <token>...</token>
  <sign>...</sign>
  <cuitAgente>20111111112</cuitAgente>
  <certificadoAnulacion>...</certificadoAnulacion>
</c20:anular>
```

`cuitAgente` (long): "Cuit agente de retención". No se llama `cuitRepresentada`.

Respuestas reales con token falso (homologación, 2026-10-02). **HTTP 500**, `Content-Type: text/xml; charset=UTF-8`, `Connection: close`, `faultcode` `soapenv:Server` (aunque el manual diga que los errores de negocio usan `soapenv:Client`):

| Caso | `faultstring` | `detail` |
|---|---|---|
| `token` = `abc` | `CANT_PARSE_TOKEN` | `CANT_PARSE_TOKEN` |
| `token` = TA bien formado en base64, `sign` falso | `CANT_VERIFY_SIGN` | `No se pudo verificar la firma por error general` |

Respuesta exacta del primer caso, **con el espacio en blanco tal cual** (saltos LF; los valores quedan rodeados de salto de línea e indentación):

```xml
<soapenv:Envelope xmlns:tns="http://www.afip.gob.ar/sire/c2005/" xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Header>
            <tns:HeaderInfo>
              <ambiente>
                homologacion - 207
              </ambiente>
              <fecha>
                2026-10-02T15:20:35.878-03:00
              </fecha>
              <vCore>
                2020.4
              </vCore>
              <v2005>
                2020.7
              </v2005>
            </tns:HeaderInfo>
          </soapenv:Header>
          <soapenv:Body>
            <soapenv:Fault>
        <faultcode>
          soapenv:Server
        </faultcode>
        <faultstring>
          CANT_PARSE_TOKEN
        </faultstring>
        <faultactor>
          
        </faultactor>
        <detail>
          CANT_PARSE_TOKEN

        </detail>
      </soapenv:Fault>
          </soapenv:Body>
        </soapenv:Envelope>
```

Un cliente que compare `faultstring` sin recortar espacios no va a encontrar "CANT_PARSE_TOKEN". ArcaSim tiene que reproducir el espacio en blanco.

Headers de las respuestas: `X-XSS-Protection: 1; mode=block`, `X-Frame-Options: deny`, `X-Content-Type-Options: nosniff`, un `Set-Cookie: HttpOnly;Secure` vacío (sin nombre de cookie) y cookies F5 (`f5avraaaaaaaaaaaaaaaa_session_`, `TS01527c7e`). Este host **no** tiene el WAF que reemplaza los 500 por `BL...`: los Fault llegan completos.

## Operaciones

3 operaciones en `C2005Soap`. `emitir` y `anular` declaran el fault `Exception{message?}`, pero los Fault reales traen `detail` como **texto**, no ese elemento.

| Operación | Propósito | Entrada | Salida | Estado |
|---|---|---|---|---|
| `dummy` | Infraestructura, sin auth | `c20:dummy` vacío | `dummyResponse{appserver, authserver, dbserver}` (nillable) | — |
| `emitir` | Emite un certificado F2005 | `token`, `sign`, `cuitAgente`, `certificado{...}` | `emitirResponse{certificadoNro, codigoSeguridad}` | **Crea** un certificado. Clave: `certificadoNro` |
| `anular` | Anula un certificado ya emitido | `token`, `sign`, `cuitAgente`, `certificadoAnulacion{version, codigoTrazabilidad?, impuesto, numeroCertificado, motivoAnulacion}` | `anularResponse{certificadoNro, codigoSeguridad}`: "los datos del certificado que anula al certificado informado" (es decir, el número del certificado de anulación, no del original) | **Cambia estado**: el certificado `numeroCertificado` queda anulado y se genera otro |

**`certificado`** (orden del XSD; obligatoriedad y reglas del manual):

| Campo | Tipo | Regla |
|---|---|---|
| `version` | int | Fijo `100` |
| `codigoTrazabilidad` | string? (36) | Texto libre |
| `impuesto` | int | Fijo `216` |
| `regimen` | int | Debe existir en la tabla REGIMEN del impuesto 216 (la tabla no está en el manual) |
| `fechaRetencion` | dateTime | Desde el 01/12/2019 |
| `condicion` | int? | Tabla CONDICIÓN: 1 INSCRIPTO, 2 NO INSCRIPTO. Obligatoria para los regímenes de REGIMEN_CONDICIÓN (régimen 831, condiciones 1 y 2) |
| `imposibilidadRetencion` | boolean | false = retención efectuada, true = no efectuada |
| `motivoNoRetencion` | string? (30) | Obligatorio si `imposibilidadRetencion` = true |
| `importeRetencion` | double (11,2) | El importe; en notas de crédito o ajustes, el importe de la retención |
| `importeBaseCalculo` | double (11,2) | Base de cálculo |
| `regimenExclusion` | boolean | true = régimen excluido |
| `porcentajeExclusion` | double? | 50 o 100. Obligatorio si `regimenExclusion` = true |
| `fechaPublicacion` | dateTime? | Obligatorio si `regimenExclusion` = true (publicación del certificado de exclusión) |
| `tipoComprobante` | int | Tabla TIPO_COMPROBANTE |
| `fechaComprobante` | dateTime | ≤ `fechaRetencion`; igual a `fechaRetencion` si el tipo es 3, 19 o 20 |
| `numeroComprobante` | string? (16) | Obligatorio para tipos 1, 2, 3, 4, 5, 6, 9, 11, 17, 18, 19 y 20; con el formato de la tabla |
| `coe`, `coeOriginal`, `cae` | string? (12, 12, 14) | "Se deberá completar con espacios hasta que se incorporen los regímenes correspondientes" |
| `importeComprobante` | double (11,2) | Importe del comprobante |
| `motivoEmisionNotaCredito` | string? (30) | Obligatorio si tipo = 3 |
| `cuitRetenido` | long | CUIT, CUIL o CDI existente |
| `numeroCertificadoOriginal` | string? (25) | Obligatorio si tipo 3, 19 o 20 |
| `fechaRetencionCertificadoOriginal` | dateTime? | Obligatorio si tipo 3, 19 o 20 |
| `importeCertificadoOriginal` | double? (11,2) | Obligatorio si tipo 3, 19 o 20 |
| `motivoAnulacion` | int? | **No** se envía en `emitir` |

Tablas del manual:

- **TIPO_COMPROBANTE**: 1 FACTURA, 2 RECIBO, 3 NOTA CREDITO, 4 NOTA DEBITO, 5 OTRO COMPROBANTE, 6 ORDEN DE PAGO, 9 ESCRITURA PUBLICA, 11 FACTURA (16 DÍGITOS), 17 LIQUIDACIÓN DE SERVICIOS PÚBLICOS - CLASE A, 18 ... CLASE B, 19 ... CLASE A – CON VALOR NEGATIVO, 20 ... CLASE B – CON VALOR NEGATIVO. Formato `99999-99999999` para 1 a 4 y 17 a 20; alfanumérico para 5, 6, 9 y 11 (la columna del PDF trae `X(16)`, `X(12)`, `X(16)`, `X(16)` en ese orden, pero está desalineada: **NO VERIFICADO** cuál corresponde a cuál).
- **MOTIVO_DE_ANULACION**: 1 CUIT ERRÓNEA DEL SUJETO RETENIDO, 2 FECHA DE LA RETENCIÓN ERRÓNEA, 3 RÉGIMEN INCORRECTO, 4 OTROS.
- Formato dateTime: `AAAA-MM-DDThh:mm:ss.SSSXXX`; separador decimal punto; los opcionales vacíos no se mandan (manual, 5.1).

Respuesta real de `dummy` (homologación, 2026-10-02), HTTP 200, `Content-Type: text/xml; charset=UTF-8`, `Content-Length: 790`:

```xml
<soapenv:Envelope xmlns:tns="http://www.afip.gob.ar/sire/c2005/" xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Header>
            <tns:HeaderInfo>
              <ambiente>
                homologacion - 207
              </ambiente>
              <fecha>
                2026-10-02T15:20:35.596-03:00
              </fecha>
              <vCore>
                2020.4
              </vCore>
              <v2005>
                2020.7
              </v2005>
            </tns:HeaderInfo>
          </soapenv:Header>
          <soapenv:Body>
            <tns:dummyResponse>
        <appserver>OK</appserver>
        <authserver>OK</authserver>
        <dbserver>OK</dbserver>
      </tns:dummyResponse>
          </soapenv:Body>
        </soapenv:Envelope>
```

(En el body del `dummy` los valores no tienen espacios extra; en el header sí.)

## Errores

Todo error es `soapenv:Fault` con HTTP 500, con `HeaderInfo` en el header, y `faultcode`, `faultstring`, `faultactor` (vacío) y `detail` (texto).

| `faultcode` | Cuándo (manual, 4.1 a 4.3) | Ejemplo |
|---|---|---|
| `soapenv:Client` | Error de negocio, por los datos enviados | "No existe persona con id xxxxxxxxxxx." (mismo texto en `faultstring` y `detail`) |
| `soapenv:Server` | Error de sistema | "Error de conexión a la base de datos" |
| `soapenv:Server` | Autenticación (en vivo) | `CANT_PARSE_TOKEN` / `CANT_PARSE_TOKEN`; `CANT_VERIFY_SIGN` / `No se pudo verificar la firma por error general` |

Los `faultstring` de autenticación son **constantes en mayúsculas** y el `detail`, el texto legible. El catálogo de constantes y textos de negocio está **NO VERIFICADO** (el manual solo da los dos ejemplos).

## Comportamiento a simular

- Respuestas con el mismo formato de plantilla: indentación y saltos LF dentro de los valores del header y del Fault.
- `dummy` sin auth.
- Auth: token no parseable → `CANT_PARSE_TOKEN`; firma inválida → `CANT_VERIFY_SIGN`; servirá para los demás casos (vencido, otro servicio, `cuitAgente` fuera de relations) con constantes del mismo estilo, marcadas como inventadas en ArcaSim hasta verificarlas.
- `emitir`: validar `version` 100, `impuesto` 216, régimen en catálogo configurable, fechas, obligatoriedades condicionales y formato de `numeroComprobante`; devolver `certificadoNro` correlativo y `codigoSeguridad` aleatorio. Errores como `soapenv:Client` con texto en `faultstring` y `detail`.
- `anular`: el certificado debe existir, ser del mismo agente y no estar anulado; `motivoAnulacion` 1 a 4; devuelve un **nuevo** `certificadoNro` (el de la anulación) con su código de seguridad.
- Par de estado: `emitir` → certificado vigente; `anular` sobre ese número → anulado; un segundo `anular` del mismo número debería fallar (**NO VERIFICADO** el texto).

## No verificado

- La tabla REGIMEN del impuesto 216 y los textos de cada error de negocio.
- El formato de `certificadoNro` y de `codigoSeguridad`.
- Si `emitir` es idempotente por `codigoTrazabilidad`.
- La alineación exacta de los formatos X(12) y X(16) de TIPO_COMPROBANTE.
- Por qué en vivo la autenticación usa `soapenv:Server` si el manual reserva `Server` para errores de sistema.
