# wsicdb

Registro de Beneficios Fiscales en el Impuesto sobre los Créditos y Débitos en Cuentas Bancarias y Otras (Ley 25.413, "impuesto al cheque"). Es un servicio **de consulta para bancos**: el banco (la CUIT del token) consulta qué cuentas (CUIT + CBU) de sus clientes tienen un beneficio de exención o reducción vigente, sus cambios de estado por fecha, el catálogo de beneficios y estados, y los entes públicos exentos por el artículo 2 de la ley. El manual habla de "CUIT del banco" y de "las cuentas de sus clientes".

Fuentes:

- Manual "Web Service IcdbService" 1.2 (21/12/2016): `https://www.afip.gob.ar/ws/registroICBD/manual_wsicdb1.2.pdf`.
- WSDL de homologación y producción, descargados el 2026-10-02 (iguales salvo el `soap:address`).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, JAX-WS RI (Metro). La página `GET` del endpoint dice "Implementation class: service.IcdbServiceImpl" |
| SOAP | 1.1, document/literal |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsicdb/IcdbService` (`:443` en el WSDL). El manual da `https://fwshomo.arca.gov.ar/...`, que **no resuelve DNS** |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsicdb/IcdbService` (`:443` en el WSDL). El manual da `serviciosjava.arca.gov.ar`, que tampoco resuelve |
| WSDL | `?wsdl` |
| targetNamespace | `http://serviciosjava.afip.gob.ar/wsicdb/` (esquema inline sin `elementFormDefault`: hijos sin namespace) |
| portType / binding / service | `IcdbPortType` / `wsicdbSOAP` / `IcdbService`, port `IcdbEndPoint` |
| SOAPAction | `http://serviciosjava.afip.gob.ar/wsicdb/<operación>` |
| Archivo guardado | `wsdl/wsicdb-homologacion.wsdl` (sin imports) |
| WSAA service id | **No indicado** en el manual. Probablemente `wsicdb`: **NO VERIFICADO** |
| Versión | El servicio no expone versión |

**Trampa de namespace.** Todos los ejemplos del manual usan `http://serviciosjava.arca.gob.ar/wsicdb/` (con `arca`). El WSDL real usa `http://serviciosjava.afip.gob.ar/wsicdb/`. Seguir al WSDL.

**Mayúsculas.** Los elementos raíz empiezan con mayúscula (`ConsultarEstadosReq`, `DummyResp`). El esquema de dummy del manual escribe `dummyResp` en minúscula; el ejemplo y el WSDL dicen `DummyResp`.

**`dummy` sin cuerpo**: `dummyRequest` no tiene partes; se despacha por SOAPAction, igual que wsjaza.

## Autenticación

Primer hijo de cada request, sin namespace:

```xml
<auth>
  <token>...</token>
  <sign>...</sign>
  <cuit>11111111111</cuit>
</auth>
```

Ojo: acá el campo se llama `cuit` (tipo `CUIT`, long entre 10000000000 y 99999999999), no `cuitRepresentada`. Es la CUIT del banco.

El manual (2.4) dice que los errores "excepcionales" van como `S:Fault`, con un ejemplo de XML mal formado:

```xml
<S:Fault xmlns:ns4="http://www.w3.org/2003/05/soap-envelope">
  <faultcode>S:Client</faultcode>
  <faultstring>Couldn't create SOAP message due to exception: XML reader error: com.ctc.wstx.exc.WstxEOFException: Unexpected EOF; was expecting a close tag for element &lt;soapenv:Envelope>
 at [row,col {unknown-source}]: [2,3]</faultstring>
</S:Fault>
```

No da el texto de los errores de token. **En vivo** (homologación, 2026-10-02), `consultarEstados` con `token` = `abc` devolvió la respuesta del WAF de `fwshomo`, no SOAP:

```
HTTP/1.0 200 OK
Connection: Keep-Alive
Content-Length: 39

BL5434356245880 2026-10-02 15:18:22 500
```

Es decir, el backend respondió con un Fault (HTTP 500) y el F5 lo reemplazó. El texto del Fault de autenticación está **NO VERIFICADO**. Ver [wsjaza.md](wsjaza.md) para el análisis del `BL... 500`.

## Operaciones

8 operaciones en `IcdbPortType`. Todas responden `<X>Resp/respuesta{...; errores{error*{codigo, descripcion}}?}`.

| Operación | Propósito | Entrada (además de `auth`) | Salida (`respuesta`) | Estado |
|---|---|---|---|---|
| `dummy` | Infraestructura | Body vacío; despacho por SOAPAction | `DummyResp/respuesta{appserver?, authserver?, dbserver?}` | — |
| `consultarNovedadesPorFecha` | Cambios de estado de las cuentas de los clientes del banco en una fecha | `solicitud{fecha}` | `registro*{cuit?, cbu?, codigoBeneficio?, codigoEstado?, fechaVigencia?, errores?}`, `errores?` | Consulta. Clave: CUIT + CBU + beneficio |
| `consultarEstadoCuentaPorFecha` | Estado de una cuenta (CUIT + CBU) a una fecha | `solicitud{cuitCliente, cbu (22 dígitos), fecha}` | Igual que la anterior | Consulta |
| `consultarBeneficios` | Catálogo de beneficios | — | `beneficio*{codigoDescripcion{codigo, descripcion}?, norma?, articulo?, inciso?}`, `errores?` | Consulta (tabla fija) |
| `consultarEstados` | Catálogo de estados de cuenta | — | `estado*{codigoDescripcion{codigo, descripcion}?}`, `errores?` | Consulta (tabla fija) |
| `consultarEnteExentoLey25413` | ¿Una CUIT es ente público exento por el art. 2? | `solicitud{cuitCliente}` | `ente{cuit?, razonSocial?}?`, `errores?` | Consulta |
| `consultarInscriptosRegistro` | Listado completo de CBU activas del banco | — | `registro*{cuit?, cbu?, codBeneficio?, fechaVigencia?}`, `errores?` | Consulta |
| `consultarEntesExentosLey25413` | Listado completo de entes exentos | — | `ente*{cuit?, razonSocial?}`, `errores?` | Consulta |

Valores del manual:

- **Estados** (ejemplo de `consultarEstados`): `AC` Autorizado, `BA` Baja, `EX` Excluido, `UI` Baja por Uso Indebido.
- **Beneficios**: tabla 5 del manual y ejemplo de `consultarBeneficios`, códigos 1 a 52 con descripción, norma (casi siempre "Anexo del Decreto N° 380/2001"), artículo (7° o 10°) e inciso. Ejemplo: `1` "Débitos y Créditos en cuenta corriente cuando se trate de Obra Sociales creadas o reconocidas por normas legales, nacionales o provinciales.", artículo 7°. Los incisos 47 y 48 se eliminaron (versiones 1.2), y 51 y 52 se agregaron por la RG 3900. El texto completo hay que copiarlo del PDF.
- Ojo con el nombre: en novedades y estado de cuenta el campo es `codigoBeneficio`; en inscriptos es `codBeneficio`.
- Fechas `AAAA-MM-DD` sin huso horario (manual, 4.2).
- El manual menciona `documentacionRequerida` en `BeneficioRespuesta`, pero **no está en el WSDL**.

Respuesta real de `dummy` (homologación, 2026-10-02), HTTP 200, `Content-Type: text/xml;charset=utf-8`:

```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:DummyResp xmlns:ns2="http://serviciosjava.afip.gob.ar/wsicdb/"><respuesta><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></respuesta></ns2:DummyResp></S:Body></S:Envelope>
```

## Errores

Dentro de `respuesta/errores/error{codigo, descripcion}` (y también por registro). `codigo` es string.

| Tipo | Código | Descripción | Operación |
|---|---|---|---|
| Formato | `cvc-type.3.1.3`, `cvc-complex-type.2.4.a`, ... | Mensajes del validador en inglés, por ejemplo "Invalid content was found starting with element 'fecha'. One of '{cbu}' is expected." | Rechazada |
| Interno | 500 | Error general de aplicación. | Rechazada |
| Interno | 550 | Error al generar el archivo pdf. | Aceptada |
| Interno | 700 | Error de sincronismo. | Rechazada |
| Interno | 800 | Servicio no disponible. | Rechazada |
| Negocio | 1000 | La CUIT ingresada es inválida o inexistente. | R |
| Negocio | 1001 | La cuitCliente ingresada, pertenece a un ente público Exento por Ley 25.413 art. 2. | R |
| Negocio | 1002 | La fecha a consultar no debe ser posterior al día actual. | R |
| Negocio | 1003 | El número de CBU es inválido. | R |
| Negocio | 1004 | No existen registros según los parámetros de búsqueda ingresados. | R |
| Excepcional | — | `S:Fault` (XML roto, autenticación). Desde Internet llega como `BL... 500` | — |

## Comportamiento a simular

- `dummy` por SOAPAction con body vacío.
- Faults detrás del WAF: `HTTP/1.0 200` + `BL<15 dígitos> <fecha hora> 500`.
- Datos: registro de cuentas por banco (CUIT del token) con `cuit`, `cbu`, `codigoBeneficio`, historial de estados (`AC`, `BA`, `EX`, `UI`) con fecha de vigencia; lista de entes exentos.
- `consultarNovedadesPorFecha`: devuelve los cambios con `fechaVigencia` = fecha pedida; 1004 si no hay; 1002 si la fecha es futura.
- `consultarEstadoCuentaPorFecha`: valida el CBU (dígitos verificadores; 1003), rechaza con 1001 si `cuitCliente` es ente exento, devuelve el estado vigente a esa fecha.
- `consultarInscriptosRegistro`: solo CBU activas (`AC`).
- Es un servicio de solo lectura: el registro lo cargan los contribuyentes por otro canal. Para pruebas, ArcaSim necesita un hook que cambie estados y genere novedades.

## No verificado

- El service id de WSAA.
- Los textos del Fault de autenticación (ocultos por el WAF).
- Si las novedades son solo del banco consultante o de todo el sistema (el manual dice "de sus clientes").
- Cuándo sale `errores` a nivel de `registro` en lugar de a nivel de `respuesta`.
- Qué devuelven `consultarEnteExentoLey25413` para una CUIT que no es ente (vacío o 1004).
