# wsfecredagente

Web service de **Factura de Crédito Electrónica (FCE) MiPyMEs para Agentes de Depósito Colectivo** (`FECredAgenteService`). Lo usa un solo actor: el Agente de Depósito Colectivo, es decir, una entidad "registrada ante la CNV para recibir depósitos colectivos de valores negociables" (manual §1.5, pág. 13). Con este servicio el agente:

- da de alta, consulta y da de baja las **cuentas** que tiene abiertas a nombre de cada CUIT (los vendedores después eligen una de esas cuentas en `wsfecred`);
- consulta las **facturas que los vendedores le informaron** desde `wsfecred` (`informarFacturaAgtDptoCltv`) y las **acepta o rechaza**;
- obtiene la tabla de **motivos de rechazo** (manual §1.1.1, pág. 4, y §1.5, pág. 13).

Marco normativo: Ley 27.440 (Título I) y RG 4367/2018, ver `docs/arca/normativa.md` §8. El manual de este servicio no cita ninguna RG (§2.4.1, pág. 17, se titula "según la RG de aplicación" pero no la nombra). El manual de `wsfecred` cita la Ley 27.440, art. 4, en los errores 6004 y 6005 (pág. 31).

Este servicio no tiene sentido solo: las facturas que consulta nacen en `wsfecred` (ver "Comportamiento a simular"). El documento de `wsfecred` lo hace otro agente; acá se cita su manual sólo para la relación.

### Fuentes

| Id | Fuente | URL | Uso |
|---|---|---|---|
| **[MAN]** | "Factura Electrónica – Web Service Agentes de Depósito Colectivo – Manual para el desarrollador", **versión 1.1.0**, 56 págs. Changelog (pág. 56): 1.0.0-beta1 21/01/2021 "Versión Inicial Beta"; 1.0.0 06/04/2021 "Versión Inicial"; 1.1.0 01/11/2021 "Ajustes Internos sin Impacto Funcional" | `https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECREDAGENTE-1.1.0.pdf` | Leído completo. Las tablas se verificaron sobre la imagen de cada página porque el texto extraído sale desordenado |
| **[WSDL]** | WSDL de homologación (`docs/arca/wsdl/wsfecredagente-homologacion.wsdl`) | `https://fwshomo.afip.gov.ar/wsfecredagente/FECredAgenteService?wsdl` | Estructura. **Manda sobre el manual.** El de producción (`https://serviciosjava.afip.gob.ar/wsfecredagente/FECredAgenteService?wsdl`) es idéntico salvo `soap:address` (comparado el 2026-10-02) |
| **[VIVO]** | Llamadas propias sin autenticación válida, 2026-10-02, homologación (y `dummy` en producción) | `scratchpad\vivo\agente-*` (req/hdr/body); resumen en `agente-sca-NOTAS.txt` | Forma real de respuestas y errores |
| **[MAN-FECRED]** | Manual de `wsfecred` v2.0.3, 86 págs. | `https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Manual_Desarrollador_WSFECRED_v2.0.3.pdf` | Sólo para la relación con `wsfecred` |

Marcas: **[MAN]**, **[WSDL]**, **[VIVO]**: verificado. **[INFERIDO]**: deducido, sin texto oficial. **NO VERIFICADO**: no se pudo comprobar. "pág. N" es la página física del PDF.

Fecha de relevamiento: **2026-10-02**.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Spring-WS** (Spring Boot). Lo muestran el header de respuesta `info` con namespace `http://headers.springbootws.factu.fisca.afip.gob.ar/xml`, el envelope con prefijo `soap:`, el elemento de respuesta con prefijo `ns2:`, `Content-Type: text/xml;charset=UTF-8` sin declaración `<?xml?>`, y los errores de negocio y de autenticación dentro del resultado con HTTP 200 [VIVO]. **No es** el dialecto Java JAX-WS de `wsfecred` (ver tabla de diferencias abajo) |
| Endpoint homologación | `https://fwshomo.afip.gov.ar/wsfecredagente/FECredAgenteService/` (`soap:address`, **con** barra final) [WSDL]. El manual da la URL **sin** barra (§2.1, pág. 14). Las dos responden igual [VIVO] |
| Endpoint producción | `https://serviciosjava.afip.gob.ar/wsfecredagente/FECredAgenteService` (`soap:address` del WSDL de producción y manual §2.1, pág. 14). `dummy` respondió OK el 2026-10-02 [VIVO] |
| WSDL | `<endpoint>?wsdl`. Archivo guardado: `docs/arca/wsdl/wsfecredagente-homologacion.wsdl`. Todo el esquema está inline, sin `xsd:import` |
| `targetNamespace` | `http://ar.gob.afip.wsfecredagente/FECredAgenteService/` (el mismo para el esquema y el WSDL) |
| `elementFormDefault` | **No está declarado → `unqualified`**. Sólo el elemento raíz del Body (`ser:xxxRequest`) lleva namespace. Todos sus hijos (`autenticacion`, `token`, `nroPagina`, etc.) van **sin namespace**. Si se los califica, el request viola el XSD y vuelve un Fault (enmascarado como `BL...500`) [VIVO `agente-qualified`]. Las respuestas siguen la misma regla: `<ns2:xxxResponse xmlns:ns2="...">` y los hijos sin prefijo ni namespace por default [VIVO] |
| Service id WSAA | **`wsfecredagente`**. Fuente: manual §2.3.1, pág. 16: "debe enviar el tag service con el valor "wsfecredagente"". Coincide con `catalogo.md` §3.1. El certificado se asocia al servicio "Web Service de Agentes de Depósito Colectivo" (misma página) |
| `SOAPAction` | `http://ar.gob.afip.wsfecredagente/FECredAgenteService/<operación>`. El binding declara 7: `dummy`, `consultarFacturasInformadas`, `confirmarFacturasInformadas`, `consultarCuentasAgente`, `altaCuentasAgente`, `bajaCuentasAgente`, `obtenerMotivosRechazo`. **`obtenerCuitsEmisores` está en el `portType` pero no en el binding** (no tiene `soapAction`) [WSDL]. Cómo enruta el servidor: ver "Enrutamiento" abajo |
| SOAP 1.1 / 1.2 | Sólo SOAP 1.1: un binding (`FECredAgenteServiceSOAP`, `soap:binding style="document"`, `use="literal"`), un port. Un request SOAP 1.2 (`application/soap+xml`) a `dummy` devolvió `BL<n> <fecha> 500` [VIVO `agente-dummy-soap12`] |
| Header de respuesta | `<soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente/><fecha/><id/></info></soap:Header>` en toda respuesta no-fault [VIVO]. Ver tabla abajo |
| Nombres de elementos | Request `<op>Request`, response `<op>Response` con un único hijo `resultado` [WSDL]. Excepción: `dummy` no tiene elemento de request (el mensaje `dummyRequest` no tiene `part`) y la respuesta es `dummyResponse` |
| Transporte | HTTP/1.1, `Transfer-Encoding: chunked`, cookies del balanceador F5 (`f5avr..._session_`, `TS01761d9e`), `Strict-Transport-Security`, `X-Frame-Options: sameorigin`, `X-Content-Type-Options: nosniff` [VIVO]. Las cookies no hacen falta para operar |

### Header `info`

| Ambiente | `ambiente` | `fecha` | `id` | Fuente |
|---|---|---|---|---|
| Homologación | `homologacion-externa - FI1` | `2026-10-02T15:07:36` (hora local, **sin** milisegundos ni zona) | `wsfecredagente 1.1.1 2021-11-01T19:10:21.225Z` | [VIVO] |
| Producción | `produccion - SJ6` | `2026-10-02T15:15:22` | `wsfecredagente 1.1.1 2021-11-01T19:10:21.225Z` | [VIVO] |
| Manual | `Testing - vii` / `Produccion - bus` | `2018-06-22T17:49:06.970-03:00` | `fecred-agente-ws 1.0.0 2020-09-14T20:52:30.332Z` | [MAN] §1.2, págs. 6-7 |

El manual pone el header con `xmlns="https://ar.gob.afip.wsfecredagente/FECredAgenteService/"`. **El real usa `http://headers.springbootws.factu.fisca.afip.gob.ar/xml`** [VIVO]. Manda el real.

### Enrutamiento [VIVO, 2026-10-02]

| Request | Resultado |
|---|---|
| `SOAPAction` correcto + elemento correcto | Procesa la operación |
| `SOAPAction` sin comillas (`SOAPAction: http://...`) | Igual que con comillas |
| Sin header `SOAPAction` o con `SOAPAction: ""` | Enruta por el elemento raíz del Body (`obtenerMotivosRechazoRequest` → `obtenerMotivosRechazo`) |
| `SOAPAction` de **otra** operación (p. ej. `consultarCuentasAgente`) con body de `obtenerMotivosRechazoRequest` | `BL<n> <fecha> 500` (Fault) |
| `SOAPAction` inexistente (`.../XXX`) | `BL<n> <fecha> 500` |
| `SOAPAction` `.../dummy` con body de `obtenerMotivosRechazoRequest` | **Responde el dummy** (ignora el body) |
| Body vacío o `<ser:dummy/>`, con o sin `SOAPAction` | Responde el dummy |
| `GET` al endpoint (sin `?wsdl`) | **HTTP 200 con un `dummyResponse` completo** |
| XML mal formado | `BL<n> <fecha> 500` |
| `obtenerCuitsEmisoresRequest`, con su `SOAPAction`, sin `SOAPAction` o con `""` | `BL<n> <fecha> 500` en los tres casos: **la operación no está implementada en homologación** |

`BL<n> <fecha> 500` es la respuesta del balanceador F5 cuando el backend devuelve HTTP 500 (un SOAP Fault): HTTP/1.0 200, sin `Content-Type`, `Content-Length: 39`, cuerpo de texto plano como `BL6255295889570 2026-10-02 15:14:56 500` [VIVO; interpretación del brief común, INFERIDO con evidencia fuerte]. El Fault real que hay detrás **no se puede ver** desde afuera.

### Diferencias con `wsfecred` (para no mezclar dialectos)

| Tema | `wsfecred` (Java JAX-WS) | `wsfecredagente` (Spring-WS) |
|---|---|---|
| Bloque de autenticación | `authRequest` (`token`, `sign`, `cuitRepresentada`) | **`autenticacion`** (`token`, `sign`, `cuitRepresentada`) [WSDL] |
| Error de autenticación | SOAP Fault (el F5 lo muestra como `BL...500`) [VIVO, brief común] | **HTTP 200** con `resultado/errores/codigoDescripcion` (505, 506, 503, 504...) [VIVO] |
| Request que viola el XSD | — | Fault → `BL...500` [VIVO] |
| Header de respuesta | `info xmlns="https://ar.gob.afip.wsfecred/FECredService/"`, `ambiente` `Testing - FI1`, `id` `WS-2.1.6` | `info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"` |
| Envelope de respuesta | `<?xml ...?>` + prefijo `S:` | Sin declaración XML, prefijo `soap:` |
| `dummy` con `<x:dummy/>` en el Body | `BL...500` (sólo anda con Body vacío) | Anda con Body vacío, con `<x:dummy/>`, sin `SOAPAction` y hasta con `GET` |
| Contenedor del resultado | `xxxReturn` (p. ej. `dummyReturn`) | `resultado` |
| Nombres de arrays | `arrayErrores`, `arrayObservaciones`, `arrayErroresFormato` | `errores`, `observaciones`, `erroresFormato` |

## Autenticación

Todas las operaciones salvo `dummy` llevan como primer hijo `autenticacion` de tipo `AutenticacionType` [WSDL]:

| Campo | Tipo XSD | Ocurrencia | Manual (§2.3, pág. 15) |
|---|---|---|---|
| `token` | `xsd:string` | 1..1 | S. "Token devuelto por el WSAA" |
| `sign` | `xsd:string` | 1..1 | S. "Signature devuelta por el WSAA" |
| `cuitRepresentada` | `CuitSimpleType` (`xsd:long`, `minExclusive` 9999999999, `maxInclusive` 99999999999: 11 dígitos) | 1..1 | S. "CUIT del Agente de depósito colectivo" |

- El manual dice: "Se validará en todos los casos que la CUIT solicitante se encuentre entre sus representados. El Token y el Sign remitidos deberán ser válidos y no estar vencidos" (§2.3.1, pág. 15). La longitud de `token` y `sign` no se especifica (pág. 55, aclaración 1).
- El ejemplo del manual (§2.3, pág. 14) declara `xmlns:ser="http://ar.gob.afip.wsfecredagente/FECredService/"`. Es una errata: el namespace real es `.../FECredAgenteService/`.
- `autenticacion` es obligatorio en el XSD. **Sin `autenticacion`** el request no valida contra el esquema y vuelve `BL...500` [VIVO `agente-noauth`]. Lo mismo con un `cuitRepresentada` fuera de rango (p. ej. `123`) [VIVO `agente-badcuit`]. O sea: **el código 502 no se vio**; un bloque que no cumple el XSD termina en Fault.

Códigos de autenticación (tabla de §2.3.1, pág. 16; todos con "Campo/Grupo" `<autenticacion>`): 501 a 516, texto completo en "Validaciones y errores".

### Respuestas reales de falla [VIVO, 2026-10-02]

Todas con **HTTP 200**, header `info` y el resultado de la operación con los campos obligatorios en vacío o cero y el error en `errores`:

| Caso | `errores` devuelto |
|---|---|
| `token` = `abc`, `sign` = `abc` | `505` `El Token no se corresponde con la Firma. {token = [abc]firma = [abc]}` **y** `506` `El Token no cumple con el Esquema (XSD) de Autenticacion. {abc}` |
| `token` y `sign` vacíos (`<token></token>`) | `503` `El Formato del Token es inválido. {}` **y** `504` `El Formato de la Firma es inválida. {}` |
| Token XML bien formado en base64 (`<sso>` con `service="wsfecred"`) + firma falsa | Sólo `505` `El Token no se corresponde con la Firma. {token = [<base64 completo>]firma = [<base64 completo>]}`. El servicio equivocado del token no llegó a validarse (no salió 508) |

Detalles de forma:
- El texto real es **el del manual + `. {detalle}`**. En 506 el real dice `Autenticacion` sin tilde y el manual `Autenticación`. En 503 y 504 el real conserva las tildes.
- Entre `[abc]` y `firma` no hay espacio: `{token = [abc]firma = [abc]}`.
- Se acumulan varios `codigoDescripcion` en el mismo `errores`.
- El servidor valida la autenticación **antes** que las validaciones de formato 2xxx: con token inválido + rango de fechas invertido + `nroPagina` 0, o con token inválido + `nroPagina` -1 + CUIT con dígito verificador mal, sólo volvieron 505 y 506 [VIVO `agente-consfact-badrange`, `agente-conscuentas-badcuitdv`].

Forma por operación con token `abc` [VIVO]:

```xml
<!-- obtenerMotivosRechazo: sin <parametros> -->
<ns2:obtenerMotivosRechazoResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><errores><codigoDescripcion><codigo>505</codigo><descripcion>El Token no se corresponde con la Firma. {token = [abc]firma = [abc]}</descripcion></codigoDescripcion><codigoDescripcion><codigo>506</codigo><descripcion>El Token no cumple con el Esquema (XSD) de Autenticacion. {abc}</descripcion></codigoDescripcion></errores></resultado></ns2:obtenerMotivosRechazoResponse>

<!-- consultarFacturasInformadas: lista vacía, página 0, hayMas N -->
<ns2:consultarFacturasInformadasResponse xmlns:ns2="..."><resultado><facturasInformadas/><nroPagina>0</nroPagina><hayMas>N</hayMas><errores>...505...506...</errores></resultado></ns2:consultarFacturasInformadasResponse>

<!-- consultarCuentasAgente: igual -->
<ns2:consultarCuentasAgenteResponse xmlns:ns2="..."><resultado><cuentasAgente/><nroPagina>0</nroPagina><hayMas>N</hayMas><errores>...</errores></resultado></ns2:consultarCuentasAgenteResponse>

<!-- confirmarFacturasInformadas, altaCuentasAgente, bajaCuentasAgente: errores primero, resultados vacío al final -->
<ns2:confirmarFacturasInformadasResponse xmlns:ns2="..."><resultado><errores>...</errores><resultados/></resultado></ns2:confirmarFacturasInformadasResponse>
```

Nota: en la respuesta de `consultarFacturasInformadas` el `nroPagina` vuelve `0` aunque se pidió `1`.

Lo que **no** se pudo ver (hace falta certificado): 507 a 516, 4008 y 4009, y si un Token vencido o de otra CUIT da 509 o 510-515.

## Operaciones

El `portType` `FECredAgenteServicePortType` tiene **8 operaciones** [WSDL], en este orden: `dummy`, `consultarFacturasInformadas`, `confirmarFacturasInformadas`, `consultarCuentasAgente`, `altaCuentasAgente`, `bajaCuentasAgente`, `obtenerMotivosRechazo`, `obtenerCuitsEmisores`. El manual documenta 7: no menciona `obtenerCuitsEmisores` (§2.4.1, pág. 17). El binding tampoco la tiene.

Convenciones de las tablas: "XSD" es `tipo minOccurs..maxOccurs` del WSDL; "Man." es la columna "Oblig" del manual (S/N). Cuando difieren, manda el XSD y la diferencia se anota.

Forma de todos los requests (hijos **sin** namespace, ver "Contrato"):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://ar.gob.afip.wsfecredagente/FECredAgenteService/">
  <soapenv:Header/>
  <soapenv:Body>
    <ser:obtenerMotivosRechazoRequest>
      <autenticacion>
        <token>PD94bWwg...</token>
        <sign>Bv7fM...</sign>
        <cuitRepresentada>30000000007</cuitRepresentada>
      </autenticacion>
    </ser:obtenerMotivosRechazoRequest>
  </soapenv:Body>
</soapenv:Envelope>
```

Forma de todas las respuestas [VIVO]: una sola línea, sin `<?xml?>`.

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Header><info xmlns="http://headers.springbootws.factu.fisca.afip.gob.ar/xml"><ambiente>homologacion-externa - FI1</ambiente><fecha>2026-10-02T15:08:59</fecha><id>wsfecredagente 1.1.1 2021-11-01T19:10:21.225Z</id></info></soap:Header><soap:Body><ns2:xxxResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado>...</resultado></ns2:xxxResponse></soap:Body></soap:Envelope>
```

En los ejemplos de abajo se muestra sólo el contenido del Body.

### Tipos compartidos

**Tipos simples** [WSDL; descripciones del manual §3.1, págs. 40-41]:

| Tipo | Base y restricción XSD | Manual |
|---|---|---|
| `CuitSimpleType` | `long`, `minExclusive` 9999999999, `maxInclusive` 99999999999 | "Longitud 11." |
| `ResultadoSimpleType` | `string`, enum `A`, `O`, `R` | "Resultado final de una operación. Si figura observada entonces el campo "Observaciones" tendra observaciones resultantes de la operación." A: Aceptado, O: Observada, R: Rechazado |
| `NumeroComprobanteSimpleType` | `long`, 1 a 99999999 | "desde 1 hasta 99999999" |
| `PuntoVentaSimpleType` | `int`, 1 a 99999 | "Identificador del Punto de Venta", 1 a 99999 |
| `Texto250SimpleType` | `string`, `minLength` 3, `maxLength` 250 | "Texto hasta 250 caracteres." (el manual no menciona el mínimo de 3) |
| `SiNoSimpleType` | `string`, `length` 1, enum `S`, `N` | S: Si, N: No |
| `ImporteSimpleType` | `decimal`, -9999999999999.99 a 9999999999999.99, `totalDigits` 15, `fractionDigits` 2 | "Total de dígitos 15 (13 enteros y 2 decimales)" |
| `DecimalSimpleType` | `decimal`, 0 a 999999999999.999999, `totalDigits` 18, `fractionDigits` 6 | — (no lo usa ningún elemento) |
| `TipoFechasFacturasSimpleType` | `string`, enum `Disponible`, `Consultada`, `Confirmada` | Disponible: "Fecha en la que el Vendedor la puso a Disposición"; Consultada: "Fecha en la que el Agente la Consulto"; Confirmada: "Fecha en la que el Agente la Confirmo". La documentación del XSD dice que filtran sobre `fechaDisponible`, `fechaConsultadaAgente` y `fechaConfirmadaAgente` |
| `TipoFechasCuentasSimpleType` | `string`, enum `Alta`, `Baja` | Alta: "Fecha en la que el Agente la dio de Alta"; Baja: "Fecha en la que el Agente la la dio de Baja" |
| `EstadoCuentaAgenteSimpleType` | `string`, enum `A`, `B` | "Estado de la Cuenta asociable a Facturas de Crédito declarada por el Agente de Deposito Colectivo": A: Activa, B: Inactiva |
| `EstadoInformeSimpleType` | `string`, enum `D`, `P`, `A`, `R` | "Estado de la Factura de Crédito Informada por el Vendedor": D: Disponible "el Emisor indicó el envió al agente"; P: Pendiente "el agente ya la consulto y esta a la espera de ser confirmada"; A: Aceptada "el agente ya la acepto mediante la operación confirmación"; R: Rechazada "el agente ya la rechazo mediante la operación de confirmación" |

Formatos generales (manual pág. 55): `date` es `AAAA-MM-DD` sin huso horario; el separador decimal es el punto.

**Tipos complejos** [WSDL; manual §3.3, págs. 45-54]:

`AutenticacionType`: ver "Autenticación".

`CodigoDescripcionType` (se usa en `evento` y dentro de `errores`/`observaciones`):

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `codigo` | `long` 1..1 | S, **`short`** | "Indentificador del tipo que representa" |
| `descripcion` | `string` 1..1 | S | "Descripción del código" |

El manual dice `short` (pág. 45); el WSDL dice `long`. Manda el WSDL. En `wsfecredsca` el mismo tipo es `short`.

`CodigoDescripcionStringType` (dentro de `erroresFormato` y en una rama de `parametros`): `codigo` `string` 1..1, `descripcion` `string` 1..1 (manual pág. 45).

| Array | Elemento hijo | Ocurrencia |
|---|---|---|
| `ArrayCodigosDescripcionesType` | `codigoDescripcion` (`CodigoDescripcionType`) | 1..unbounded |
| `ArrayCodigosDescripcionesStringType` | `codigoDescripcionString` (`CodigoDescripcionStringType`) | 1..unbounded |
| `ArrayCodigosType` | `codigo` (`long`) | 1..unbounded |
| `ArrayCodigosStringType` | `codigo` (`string`) | 1..unbounded |
| `ArrayTexto250SimpleType` | `texto` (`Texto250SimpleType`) | 1..unbounded (no lo usa ninguna operación) |
| `ArrayFacturasInformadasType` | `facturaInformada` (`FacturaInformadaType`) | **0**..unbounded |
| `ArrayFacturasAConfirmarType` | `factura` (`FacturaAConfirmarType`) | 1..unbounded |
| `ArrayResultadosConfirmacionFacturasType` | `resultado` (`ConfirmacionFacturaResultadoType`) | 0..unbounded |
| `ArrayCuentasAgenteType` | `cuenta` (`CuentaAgenteType`) | 0..unbounded |
| `ArrayCuentasAgenteAOperarType` | `cuenta` (`CuentaAgenteAOperarType`) | 1..unbounded |
| `ArrayResultadosCuentaAgenteType` | `resultado` (`ResultadoOperacionCuentaAgenteType`) | 0..unbounded |

Los arrays de errores tienen `minOccurs="1"` en su hijo: si no hay errores, el elemento `errores` no se emite (no se emite vacío). Los arrays de datos con `minOccurs="0"` se emiten vacíos (`<facturasInformadas/>`, `<cuentasAgente/>`, `<resultados/>`) [VIVO].

`ArrayParametrosType`: `xsd:choice` 1..1 entre `parametrosTipoCodigosDescripciones` (`ArrayCodigosDescripcionesType`), `parametrosTipoCodigosDescripcionesString` (`ArrayCodigosDescripcionesStringType`), `parametrosTipoCodigos` (`ArrayCodigosType`) y `parametrosTipoCodigosString` (`ArrayCodigosStringType`) [WSDL]. El manual (§3.2, pág. 42) sólo nombra las dos primeras.

`IdComprobanteType` (identifica una factura de crédito):

| Campo | XSD | Man. | Descripción (pág. 48) |
|---|---|---|---|
| `cuitEmisor` | `CuitSimpleType` 1..1 | S | "CUIT Emisor del Comprobante" |
| `tipoCmp` | `short` 1..1 | S | "Tipo Comprobante" |
| `ptoVta` | `PuntoVentaSimpleType` 1..1 | S | "Punnto de Venta" |
| `nroCmp` | `NumeroComprobanteSimpleType` 1..1 | S | "Numero de Comprobante" |

En `wsfecred` el tipo homónimo usa `CUITEmisor` y `codTipoCmp` (otros nombres) [WSDL de wsfecred]. No mezclar.

`CuentaAgenteType` (respuestas) y `CuentaAgenteAOperarType` (requests de alta y baja). Tienen la misma estructura:

| Campo | XSD | Man. (págs. 48-49) | Descripción |
|---|---|---|---|
| `cuitTitular` | `CuitSimpleType` **1..1** | **N** | "Cuit del Titular de la Cuenta" |
| `cuentaId` | `xsd:string` 1..1 (sin restricción) | S | "Identificador de la Cuenta" |
| `denominacion` | `Texto250SimpleType` **0..1** | **S** | "Denominacion de la Cuenta" |

La obligatoriedad del manual está invertida respecto del XSD; manda el XSD. En `wsfecred` el identificador de cuenta (`idCuenta`) es `IdCuentaAgenteSimpleType`: string de 3 a 20 caracteres [WSDL de wsfecred]. Acá `cuentaId` no tiene límite en el XSD; el error 2005 "ID Cuenta Inválido" sugiere que el servidor lo valida aparte [INFERIDO]. Qué formato exige: NO VERIFICADO.

`FiltroFechasFacturasType` y `FiltroFechasCuentasType`:

| Campo | XSD | Man. (págs. 52-53) | Descripción |
|---|---|---|---|
| `tipo` | `TipoFechasFacturasSimpleType` o `TipoFechasCuentasSimpleType` 1..1 | S | Qué fecha se filtra |
| `desde` | `xsd:date` 1..1 | S | "Inicio del Rango de Fechas" |
| `hasta` | `xsd:date` 1..1 | S | "Fin del Rango de Fechas" |

Un `tipo` fuera del enum viola el XSD y vuelve `BL...500` [VIVO `agente-conscuentas-badenum`, con `Disponible` en el filtro de cuentas].

`evento` es siempre un `CodigoDescripcionType` suelto (0..1), con `codigo` y `descripcion` como hijos directos [WSDL]. El manual (§1.4, pág. 12) lo dibuja como `<evento><codigoDescripcion>...</codigoDescripcion></evento>` y dice "es del tipo ArrayCodigoDescripcionType". Manda el WSDL.

### 1. dummy

Propósito: "Metodo dummy." [WSDL]; "Permite verificar el funcionamiento del presente Servicio" (manual §2.4.5, pág. 20). **No lleva autenticación.**

Request: ninguno. El mensaje `dummyRequest` no tiene `part` y el esquema no declara un elemento `dummy`. El manual manda el Body vacío (pág. 20). El servidor real acepta Body vacío, `<ser:dummy/>`, cualquier body con `SOAPAction` de `dummy`, y hasta un `GET` [VIVO].

Response `dummyResponse` → `resultado` (`DummyReturnType`):

| Campo | XSD | Man. (pág. 21) |
|---|---|---|
| `appserver` | `string` 1..1 | S, "Servidor de aplicaciones" |
| `authserver` | `string` 1..1 | S, "Servidor de autenticacion" |
| `dbserver` | `string` 1..1 | S, "Servidor de base de datos" |

(En la tabla del manual las descripciones están corridas una fila; acá se ordenan según el nombre.)

Ejemplo real [VIVO, homologación, 2026-10-02]:

```xml
<ns2:dummyResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></resultado></ns2:dummyResponse>
```

El ejemplo del manual (pág. 22) omite `<resultado>` y pone `appserver` directo bajo `dummyResponse`: errata. Valores distintos de `OK`: NO VERIFICADO.

### 2. consultarFacturasInformadas

Propósito: "consultar aquellas facturas de crédito informadas por los Emisores en el Servicio de Factura de Crédito (FECRED). Facturas cuyo saldo negociable haya sido aceptado por el comprador y se haya asociado a una cuenta del agente de depósito colectivo. Estas facturas disponibles, pasarán a un estado de Pendiente de Confirmación del Agente." (manual §2.4.7, pág. 25).

Request `consultarFacturasInformadasRequest` (`ConsultarFacturasInformadasAgenteRequestType`):

| Campo | XSD | Man. (pág. 25) | Descripción |
|---|---|---|---|
| `autenticacion` | `AutenticacionType` 1..1 | S | |
| `estadoInforme` | `EstadoInformeSimpleType` 0..1 | N | "El estado en el que se encuentra la factura de cerdito" (`D`, `P`, `A`, `R`) |
| `nroPagina` | `xsd:short` 1..1 | S | "Numero de página para obtener más resultados de una misma búsqueda" |
| `cuitEmisor` | `CuitSimpleType` 0..1 | N | "CUIT del Vendedor que Informo la Factura" |
| `filtroFechas` | `FiltroFechasFacturasType` 1..1 | S | "Fecha desde y hasta que indica el rango a consultar." |

La tabla del manual llama al último campo `filtroFecha` y le pone tipo `FiltroFechasCuentaType`; el diagrama de la misma página dice `filtroFechas` / `FiltroFechasFacturasType`, igual que el WSDL. Manda el WSDL.

Response `consultarFacturasInformadasResponse` → `resultado` (`ConsultarFacturasInformadasAgenteReturnType`), en este orden:

| Campo | XSD | Man. (págs. 26-27) | Descripción |
|---|---|---|---|
| `facturasInformadas` | `ArrayFacturasInformadasType` 1..1 | S | "Conjunto de Facturas Informadas resultantes de la Búsqueda" |
| `nroPagina` | `short` 1..1 | S | "Numero de Pagina Devuelto" |
| `hayMas` | `SiNoSimpleType` 1..1 | S | "Indica si existen más resultados posteriores a los devueltos en esta página" |
| `evento` | `CodigoDescripcionType` 0..1 | N | "Anuncios informativos del sistema." |
| `observaciones` | `ArrayCodigosDescripcionesType` 0..1 | **no figura** | — (está en el WSDL y no en el manual) |
| `errores` | `ArrayCodigosDescripcionesType` 0..1 | N | "Si el requerimiento fue rechazado, detalla el o los motivos que dieron origen al rechazo." |
| `erroresFormato` | `ArrayCodigosDescripcionesStringType` 0..1 | N | errores de formato (tags inválidos, sin cerrar, tipos) |

`FacturaInformadaType` (cada `facturaInformada`):

| Campo | XSD | Man. (págs. 50-51) | Descripción del manual |
|---|---|---|---|
| `idFactura` | `IdComprobanteType` 1..1 | S | "Identificador de la Factura de Credito obtenida de la Consulta" |
| `cuitComprador` | `CuitSimpleType` 1..1 | S | "Cuit del Comprador de la Factura de Crédito" |
| `razonSocialComprador` | `Texto250SimpleType` 1..1 | S | "Razón Social del Comprador de la Factura de Crédito" |
| `fechaEmision` | `xsd:date` 1..1 | S | "Fecha en la que se Informó la Factura de Crédito" (sic; ver nota) |
| `fechaVencimientoPago` | `xsd:date` 1..1 | S | "Fecha de Vencimiento de Pago de la Factura" |
| `saldoNegociable` | `ImporteSimpleType` 1..1 | S | "Saldo aceptador por el Comprador de la Factura de Crédito" |
| `codMoneda` | `xsd:string` 1..1 | S | "El Codigo de Moneda Asociado a la Factura de Crédito (PES, DOL, etc)" |
| `cuentaAgente` | `CuentaAgenteType` 1..1 | S | "Cuenta del Agente de Deposito Colectivo asociada a la Factura de crédito" |
| `estadoInforme` | `EstadoInformeSimpleType` 1..1 | S | "Estado de la Factura de Crédito" |
| `fechaHoraDisponible` | `xsd:dateTime` 1..1 | S | "Fecha y Hora del instante en que el Vendedor Informo la Factura de crédito" |
| `fechaHoraLecturaAgente` | `xsd:dateTime` 0..1 | N | "...en que el Agente de Deposito Colectivo Consulto la Factura de crédito" |
| `fechaHoraConfirmacionAgente` | `xsd:dateTime` 0..1 | N | "...en que el Agente de Deposito Colectivo Confirmo la Factura de Crédito" |

Nota sobre `fechaEmision`: el nombre dice emisión y la descripción dice "Fecha en la que se Informó". La fecha en que se informó ya está en `fechaHoraDisponible`, así que lo razonable es devolver la fecha de emisión del comprobante [INFERIDO]. NO VERIFICADO con datos reales.

Formato de `xsd:dateTime` en la respuesta (con o sin milisegundos, con o sin zona): NO VERIFICADO. El header `info` usa `aaaa-mm-ddThh:mm:ss` sin zona [VIVO]; es una pista, no una prueba.

Validaciones propias (§2.4.7.3, pág. 27): **4022** `<factura>` "Ocurrio un error inesperado intentando consultar las Solicitudes de Informe". Además aplican las comunes: 2002 (CUIT), 2003 (rango de fechas), 2004 (página), 4008, 4009, 4001-4007.

Ejemplo de request [derivado del WSDL; el manual no trae ejemplos]:

```xml
<ser:consultarFacturasInformadasRequest>
  <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
  <estadoInforme>D</estadoInforme>
  <nroPagina>1</nroPagina>
  <filtroFechas><tipo>Disponible</tipo><desde>2026-09-01</desde><hasta>2026-09-30</hasta></filtroFechas>
</ser:consultarFacturasInformadasRequest>
```

Ejemplo de respuesta con datos [derivado del WSDL, valores ficticios, formato de `dateTime` NO VERIFICADO]:

```xml
<ns2:consultarFacturasInformadasResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><facturasInformadas><facturaInformada><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idFactura><cuitComprador>30500010912</cuitComprador><razonSocialComprador>EMPRESA GRANDE SA</razonSocialComprador><fechaEmision>2026-08-01</fechaEmision><fechaVencimientoPago>2026-10-30</fechaVencimientoPago><saldoNegociable>121000.00</saldoNegociable><codMoneda>PES</codMoneda><cuentaAgente><cuitTitular>20111111112</cuitTitular><cuentaId>0001234</cuentaId><denominacion>CUENTA COMITENTE</denominacion></cuentaAgente><estadoInforme>P</estadoInforme><fechaHoraDisponible>2026-09-10T11:20:00</fechaHoraDisponible><fechaHoraLecturaAgente>2026-10-02T15:30:00</fechaHoraLecturaAgente></facturaInformada></facturasInformadas><nroPagina>1</nroPagina><hayMas>N</hayMas></resultado></ns2:consultarFacturasInformadasResponse>
```

(La factura vuelve en `P` porque la consulta la pasa de `D` a `P`; ver "Comportamiento a simular". Si el `estadoInforme` devuelto es el anterior o el posterior a la consulta: NO VERIFICADO.)

### 3. confirmarFacturasInformadas

Propósito: "aceptar o rechazar, un conjunto de facturas de crédito informadas por los Emisores en el Servicio de Factura de Crédito Electrónica (FECRED)" (manual §2.4.8, pág. 28).

Request `confirmarFacturasInformadasRequest` (`ConfirmarFacturaInformadaRequestType`):

| Campo | XSD | Man. (pág. 28) | Descripción |
|---|---|---|---|
| `autenticacion` | `AutenticacionType` 1..1 | S | |
| `facturas` | `ArrayFacturasAConfirmarType` 1..1 → `factura` 1..unbounded | S | "Conjunto de Facturas de Crédito Informadas por los Vendedores a Rechazar o Aceptar" |

`FacturaAConfirmarType`:

| Campo | XSD | Man. (pág. 49) | Descripción |
|---|---|---|---|
| `idFactura` | `IdComprobanteType` 1..1 | S | "Identificador de la Factura de Credito a Confirmar" |
| `aceptada` | `SiNoSimpleType` 1..1 | S | "Confirmacion a Realizar (Aceptar o Rechazar)" |
| `codRechazo` | **`xsd:short`** 0..1 | N, **`Texto250SimpleType`** | "Uno de los que se pueden obtener del sistema, en caso de decidir rechazar la factura" |

La tabla del manual dice `Texto250SimpleType` para `codRechazo`; el diagrama de la misma página y el WSDL dicen `short`. Manda el WSDL: es un código de `obtenerMotivosRechazo`.

Response `confirmarFacturasInformadasResponse` → `resultado` (`ConfirmarFacturaInformadaAgenteReturnType`), **en este orden**:

| Campo | XSD | Man. (págs. 29-30) | Descripción |
|---|---|---|---|
| `evento` | `CodigoDescripcionType` 0..1 | N | anuncio informativo |
| `errores` | `ArrayCodigosDescripcionesType` 0..1 | N | errores del lote |
| `erroresFormato` | `ArrayCodigosDescripcionesStringType` 0..1 | N | errores de formato |
| `resultados` | `ArrayResultadosConfirmacionFacturasType` 1..1 → `resultado` 0..unbounded | S (el manual lo llama `resultado` y lo pone primero) | "Conjunto de Resultados de Confirmación de Facturas de Crédito Enviadas" |

`ConfirmacionFacturaResultadoType` (cada `resultados/resultado`):

| Campo | XSD | Man. (pág. 46) | Descripción |
|---|---|---|---|
| `idFactura` | `IdComprobanteType` 1..1 | S | "Identificador de la Factura de Crédito" |
| `resultado` | `ResultadoSimpleType` 1..1 | S | "Resultado de la Confirmación" (`A`/`O`/`R`) |
| `errores` | `ArrayCodigosDescripcionesType` **0..1** | **S** | "Listado de Errores de Autenticación o Negocio asociados de la operación" |

Validaciones propias (§2.4.8.3, pág. 30):

| Código | Grupo | Texto |
|---|---|---|
| 4017 | `<facturas>` | "Ocurrió un error inesperado intentando confirmar el lote de Solicitudes de Informe Enviado" |
| 4018 | `<factura>` | "Ocurrió un error intentando Confirmar la Solicitud de Informe" |
| 4019 | `<factura>` | "No se encontró ninguna Solicitud de Informe al Agente con los Datos del Comprobante Enviados que se encuentre Pendiente de Confirmar" |
| 4020 | `<factura>` | "La Solicitud de Informe ya fue Aceptada por el Agente" |
| 4021 | `<factura>` | "La Solicitud de Informe debe ser Consultada por el Agente antes de ser enviada para Confirmar" |

Además: 2006 (`codRechazo` inexistente o inválido), 2008 (error validando la confirmación de una factura), 2009 (demasiados elementos) y las comunes.

Por el "Campo/Grupo", 4017 va al `errores` del lote y 4018-4021 al `errores` de cada `resultado` (con `resultado` = `R`) [INFERIDO]. NO VERIFICADO.

Ejemplo de request [derivado del WSDL]:

```xml
<ser:confirmarFacturasInformadasRequest>
  <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
  <facturas>
    <factura>
      <idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idFactura>
      <aceptada>S</aceptada>
    </factura>
    <factura>
      <idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>16</nroCmp></idFactura>
      <aceptada>N</aceptada>
      <codRechazo>1</codRechazo>
    </factura>
  </facturas>
</ser:confirmarFacturasInformadasRequest>
```

Ejemplo de respuesta [derivado del WSDL; la segunda factura muestra el caso de una que ya estaba aceptada]:

```xml
<ns2:confirmarFacturasInformadasResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><resultados><resultado><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>15</nroCmp></idFactura><resultado>A</resultado></resultado><resultado><idFactura><cuitEmisor>20111111112</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>1</ptoVta><nroCmp>16</nroCmp></idFactura><resultado>R</resultado><errores><codigoDescripcion><codigo>4020</codigo><descripcion>La Solicitud de Informe ya fue Aceptada por el Agente</descripcion></codigoDescripcion></errores></resultado></resultados></resultado></ns2:confirmarFacturasInformadasResponse>
```

Ejemplo real con token inválido: ver "Autenticación" (`<errores>…</errores><resultados/>`).

### 4. consultarCuentasAgente

Propósito: "consultar las cuentas que ha dado de alta o baja" (manual §2.4.9, pág. 31).

Request `consultarCuentasAgenteRequest` (`ConsultarCuentasAgenteRequestType`):

| Campo | XSD | Man. (pág. 31) | Descripción |
|---|---|---|---|
| `autenticacion` | `AutenticacionType` 1..1 | S | |
| `estadoCuenta` | `EstadoCuentaAgenteSimpleType` 0..1 | N | "El estado en el que se encuentra la cuenta declarada" (`A`/`B`) |
| `nroPagina` | `xsd:short` 1..1 | S | página |
| `cuitTitular` | `CuitSimpleType` 0..1 | N | El manual lo llama **`cuitEmisor`** ("Cuit del Vendedor que Informo la Factura"), en la tabla y en el diagrama. El WSDL dice `cuitTitular`. Manda el WSDL |
| `filtroFechas` | `FiltroFechasCuentasType` 1..1 | S | rango por fecha de `Alta` o `Baja` (el manual escribe `filtroFecha` / `FiltroFechasCuentaType`) |

Response `consultarCuentasAgenteResponse` → `resultado` (`ConsultarCuentasAgenteReturnType`):

| Campo | XSD | Man. (págs. 32-33) | Descripción |
|---|---|---|---|
| `cuentasAgente` | `ArrayCuentasAgenteType` 1..1 → `cuenta` 0..unbounded (`CuentaAgenteType`) | S (la tabla dice `ArrayFacturasAConfirmarType`, errata; el diagrama dice `ArrayCuentasAgenteType`) | "Conjunto de Cuentas de los Vendedores en el Agente de Deposito Colectivo" |
| `nroPagina` | `short` 1..1 | S | página devuelta |
| `hayMas` | `SiNoSimpleType` 1..1 | S | hay más páginas |
| `evento` | 0..1 | N | |
| `observaciones` | `ArrayCodigosDescripcionesType` 0..1 | **no figura** | |
| `errores` | 0..1 | N | |
| `erroresFormato` | 0..1 | N | |

Cada `cuenta` sólo trae `cuitTitular`, `cuentaId` y `denominacion`. **La respuesta no dice el estado (A/B) ni las fechas de alta y baja** de cada cuenta: el cliente lo sabe por el filtro que pidió.

Validaciones propias (§2.4.9.3, pág. 33): **4016** `<cuentas>` "Ocurrio un error inesperado intentando consultar las Cuentas del Agente". Más las comunes.

Ejemplo de request [derivado del WSDL]:

```xml
<ser:consultarCuentasAgenteRequest>
  <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
  <estadoCuenta>A</estadoCuenta>
  <nroPagina>1</nroPagina>
  <filtroFechas><tipo>Alta</tipo><desde>2026-01-01</desde><hasta>2026-12-31</hasta></filtroFechas>
</ser:consultarCuentasAgenteRequest>
```

Respuesta con datos [derivado del WSDL, valores ficticios]:

```xml
<ns2:consultarCuentasAgenteResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><cuentasAgente><cuenta><cuitTitular>20111111112</cuitTitular><cuentaId>0001234</cuentaId><denominacion>CUENTA COMITENTE</denominacion></cuenta></cuentasAgente><nroPagina>1</nroPagina><hayMas>N</hayMas></resultado></ns2:consultarCuentasAgenteResponse>
```

### 5. altaCuentasAgente

Propósito: "dar de Alta cuentas que podrán ser asociadas a facturas de crédito por parte del Emisor en el Servicio de Factura de Crédito Electrónica (FECRED)" (manual §2.4.10, pág. 34).

Request `altaCuentasAgenteRequest` (`OperacionCuentasAgenteRequestType`):

| Campo | XSD | Man. (pág. 34) | Descripción |
|---|---|---|---|
| `autenticacion` | `AutenticacionType` 1..1 | S | |
| `cuentas` | `ArrayCuentasAgenteAOperarType` 1..1 → `cuenta` 1..unbounded (`CuentaAgenteAOperarType`) | S | "Conjunto de Cuentas a dar de Alta por parte del Agente de Deposito Colectivo" |

Response `altaCuentasAgenteResponse` → `resultado` (`OperacionCuentaAgenteReturnType`), en este orden:

| Campo | XSD | Man. (págs. 35-36) | Descripción |
|---|---|---|---|
| `evento` | 0..1 | N | |
| `errores` | 0..1 | N | errores del lote |
| `erroresFormato` | 0..1 | N | |
| `resultados` | `ArrayResultadosCuentaAgenteType` 1..1 → `resultado` 0..unbounded | S | "Conjunto de Resultados del Alta de las cuentas asociables a facturas de crédito" |

`ResultadoOperacionCuentaAgenteType` (cada `resultados/resultado`, manual pág. 54):

| Campo | XSD | Man. | Descripción |
|---|---|---|---|
| `cuentaAgente` | `CuentaAgenteType` 1..1 | S | "Cuenta Agente Resultante del Alta exitosa" |
| `resultado` | `ResultadoSimpleType` 1..1 | S | "Resultado de la Operacion" |
| `errores` | `ArrayCodigosDescripcionesType` 0..1 | N | motivos del rechazo |

Validaciones propias (§2.4.10.3, pág. 36): **4010** `<cuentas>` "Ocurrio un error inesperado intentando dar de Alta el lote de Cuentas Enviado"; **4012** `<cuenta>` "Ocurrio un error intentando dar de Alta la Cuenta Enviada". Además 2002 (CUIT del titular), 2005 (`cuentaId`), 2007 (validación de la cuenta), 2009 (demasiadas cuentas) y las comunes.

El manual **no tiene un código para "la cuenta ya existe"**. Qué devuelve un alta duplicada: NO VERIFICADO (ver "Comportamiento a simular").

Ejemplo de request [derivado del WSDL]:

```xml
<ser:altaCuentasAgenteRequest>
  <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
  <cuentas>
    <cuenta><cuitTitular>20111111112</cuitTitular><cuentaId>0001234</cuentaId><denominacion>CUENTA COMITENTE</denominacion></cuenta>
  </cuentas>
</ser:altaCuentasAgenteRequest>
```

Respuesta [derivado del WSDL]:

```xml
<ns2:altaCuentasAgenteResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><resultados><resultado><cuentaAgente><cuitTitular>20111111112</cuitTitular><cuentaId>0001234</cuentaId><denominacion>CUENTA COMITENTE</denominacion></cuentaAgente><resultado>A</resultado></resultado></resultados></resultado></ns2:altaCuentasAgenteResponse>
```

### 6. bajaCuentasAgente

Propósito: "dar de Baja cuentas que ya se hayan declarado mediante el alta y estén activas" (manual §2.4.11, pág. 37).

Request `bajaCuentasAgenteRequest`: mismo tipo que el alta (`OperacionCuentasAgenteRequestType`: `autenticacion` + `cuentas/cuenta` 1..unbounded). `denominacion` es opcional en el XSD.

Response `bajaCuentasAgenteResponse`: mismo tipo que el alta (`OperacionCuentaAgenteReturnType`: `evento`, `errores`, `erroresFormato`, `resultados/resultado`) (manual págs. 38-39).

Validaciones propias (§2.4.11.3, pág. 39):

| Código | Grupo | Texto |
|---|---|---|
| 4011 | `<cuentaAgente>` | "Ocurrio un error inesperado intentando dar de Baja el lote de Cuentas Enviado" |
| 4013 | `<cuentaAgente>` | "Ocurrio un error intentando dar de Baja la Cuenta Enviada" |
| 4014 | `<cuentaAgente>` | "No se encontro una Cuenta Activa asociada a los datos Enviados" |
| 4015 | `<cuitTitular>` | "El Titular con el que intenta efectuar la operación no esta asociado a la cuenta indicada" |

Ejemplo de request [derivado del WSDL]:

```xml
<ser:bajaCuentasAgenteRequest>
  <autenticacion><token>…</token><sign>…</sign><cuitRepresentada>30000000007</cuitRepresentada></autenticacion>
  <cuentas>
    <cuenta><cuitTitular>20111111112</cuitTitular><cuentaId>0001234</cuentaId></cuenta>
  </cuentas>
</ser:bajaCuentasAgenteRequest>
```

Respuesta cuando la cuenta ya no está activa [derivado del WSDL y del texto de 4014]:

```xml
<ns2:bajaCuentasAgenteResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><resultados><resultado><cuentaAgente><cuitTitular>20111111112</cuitTitular><cuentaId>0001234</cuentaId></cuentaAgente><resultado>R</resultado><errores><codigoDescripcion><codigo>4014</codigo><descripcion>No se encontro una Cuenta Activa asociada a los datos Enviados</descripcion></codigoDescripcion></errores></resultado></resultados></resultado></ns2:bajaCuentasAgenteResponse>
```

### 7. obtenerMotivosRechazo

Propósito: "obtener los diferentes motivos de rechazo para emplear en el método para Confirmar Facturas Informadas, en el caso que el agente desee rechazarla" (manual §2.4.6, pág. 23).

Request `obtenerMotivosRechazoRequest` (`ConsultaParametricaRequestType`): sólo `autenticacion` 1..1 ("únicamente se envían los campos involucrados en la autenticación", pág. 23).

Response `obtenerMotivosRechazoResponse` → `resultado` (`ConsultaParametricaReturnType`):

| Campo | XSD | Man. (págs. 24, 46-47) | Descripción |
|---|---|---|---|
| `parametros` | `ArrayParametrosType` **0..1** | N en la tabla; **1..1** en el diagrama de pág. 46 | "Conjunto de Resultados a una consulta Parametrica" |
| `evento` | `CodigoDescripcionType` 0..1 | N | |
| `errores` | `ArrayCodigosDescripcionesType` 0..1 | N | |
| `erroresFormato` | `ArrayCodigosDescripcionesStringType` 0..1 | N | |

Con error de autenticación `parametros` no se emite [VIVO]. Manda el WSDL (0..1).

El manual (pág. 24) dice que las consultas paramétricas devuelven "un Array de Parametros Clave-Valor (String, String) o bien un Array de Parametros Codigo-Descripcion (Short, String)". Como `codRechazo` es `short`, lo esperable es `parametros/parametrosTipoCodigosDescripciones/codigoDescripcion` [INFERIDO]. Qué rama del `choice` usa y qué motivos devuelve: **NO VERIFICADO** (el manual no trae la tabla).

Validaciones propias: ninguna en el manual. Aplican las comunes.

Ejemplo de respuesta [derivado del WSDL; rama del `choice` INFERIDA; el motivo es ficticio]:

```xml
<ns2:obtenerMotivosRechazoResponse xmlns:ns2="http://ar.gob.afip.wsfecredagente/FECredAgenteService/"><resultado><parametros><parametrosTipoCodigosDescripciones><codigoDescripcion><codigo>1</codigo><descripcion>(motivo ficticio)</descripcion></codigoDescripcion></parametrosTipoCodigosDescripciones></parametros></resultado></ns2:obtenerMotivosRechazoResponse>
```

### 8. obtenerCuitsEmisores

Está en el `portType` con los mismos tipos que `obtenerMotivosRechazo` (`obtenerCuitsEmisoresRequest` / `obtenerCuitsEmisoresResponse` = `ConsultaParametricaRequestType` / `ConsultaParametricaResponseType`) [WSDL]. Pero:

- **no está en el binding** (no tiene `soapAction`);
- **no está en el manual**;
- en homologación responde `BL<n> <fecha> 500` con su `SOAPAction`, sin `SOAPAction` y con `SOAPAction: ""` [VIVO `agente-cuitsemisores-*`]. Con el mismo token inválido, las demás operaciones devuelven 505/506; o sea, ni siquiera llega a validar la autenticación.

Propósito: NO VERIFICADO. Por el nombre, sería la lista de CUITs de vendedores que le informaron facturas al agente (sirve para el filtro `cuitEmisor`) [INFERIDO]. Para ArcaSim: publicarla en el WSDL igual que el real y responder un Fault (ver "Comportamiento a simular").

## Validaciones y errores

Hay tres familias, excluyentes entre sí (manual §1.3, págs. 8-10):

1. **Errores excepcionales**: el manual (pág. 8) los muestra como `<fault xmlns="https://ar.gob.afip.wsfecredagente/FECredAgenteService/"><codigo>1</codigo><descripcion>Ocurrio un error intentando procesar el Request</descripcion></fault>` dentro del Body, e incluye "casos de incumplimiento con la estructura del WSDL del servicio". En la realidad, todo request que viola el XSD, todo XML mal formado y toda acción inválida vuelve `BL<n> <fecha> 500`: o sea, el backend responde un Fault HTTP 500 y el F5 lo tapa [VIVO]. El contenido real de ese Fault: NO VERIFICADO.
2. **Errores de formato**: van en `erroresFormato/codigoDescripcionString` (código como **string**). "de no superar alguna de las validaciones de formato, el WS devolverá el erroresFormato y no continuará con las validaciones de negocio, por lo cual no existirá el elemento errores, son excluyentes entre si" (pág. 10). Ejemplo del manual (pág. 9): `<erroresFormato><codigoDescripcionString><codigo>2001</codigo><descripcion>Ocurrió un error intentando validar el pedido</descripcion></codigoDescripcionString></erroresFormato>`.
3. **Errores de negocio o autenticación**: van en `errores/codigoDescripcion` (código numérico). Ejemplo del manual (pág. 10): `<errores><codigoDescripcion><codigo>501</codigo><descripcion>Ocurrió un error intentando autenticar el pedido</descripcion></codigoDescripcion></errores>`.

Orden observado [VIVO]: XSD (Fault) → autenticación (`errores` 50x) → formato 2xxx (no se llegó a ver ninguno porque la autenticación falla antes). El orden entre 2xxx y 40xx sale del texto de pág. 10.

Los eventos (`evento`) son "eventos programados" (§1.4, pág. 12). No hay ningún código de evento documentado.

Tabla completa (todos los códigos del manual). "Efecto": **fault** = SOAP Fault HTTP 500 (en la realidad, `BL...500`); **rechaza (formato)** = `erroresFormato`, sin `errores`; **rechaza** = `errores` del resultado; **rechaza ítem** = `errores` dentro del `resultado` de una factura o cuenta.

| Código | Texto / condición | Efecto | Dónde | Página |
|---|---|---|---|---|
| 1 | "Ocurrió un error intentando procesar el Request" | fault (ejemplo de error excepcional de pág. 8) | todas, Request | 18, 8 |
| 2 | "Ocurrió un error intentando parsear el Soap Request" | fault [INFERIDO: el manual de wsfecredsca marca 1 y 2 como "devueltos como SoapFaults"] | todas, Request | 18 |
| 1001 | "El Request no Cumple con el Esquema asociado al WSDL" | fault [VIVO: violar el XSD da `BL...500`; el código 1001 en sí no se pudo ver] | todas, Request | 18 |
| 2001 | "Ocurrió un error intentando validar el pedido" | rechaza (formato) | todas, Request | 18, 9 |
| 2002 | "Formato CUIT, CUIL o CDI inválido" | rechaza (formato) | todas, cualquier CUIT (`cuitRepresentada`, `cuitEmisor`, `cuitTitular`) | 18 |
| 2003 | "Rango de Fechas inválido (*)" | rechaza (formato) | `consultarFacturasInformadas`, `consultarCuentasAgente`: `filtroFechas/desde`, `hasta` | 18 |
| 2004 | "Número de Página inválido" | rechaza (formato) | consultas: `nroPagina` | 18 |
| 2005 | "ID Cuenta Inválido" | rechaza (formato) | `altaCuentasAgente`, `bajaCuentasAgente`: `cuentaId` | 18 |
| 2006 | "Código Rechazo Inexistente o Inválido" | rechaza (formato) | `confirmarFacturasInformadas`: `codRechazo` | 18 |
| 2007 | "Ocurrió un error intentando validar el pedido para la cuenta" | rechaza (formato) | el manual pone grupo `<codRechazo>`; por el texto es de alta/baja de cuentas [INFERIDO] | 18 |
| 2008 | "Ocurrió un error intentando validar el pedido de confirmación para la factura" | rechaza (formato) | `confirmarFacturasInformadas`, grupo `<codRechazo>` | 18 |
| 2009 | "Supera la cantidad de elementos que pueden procesarse (*)" | rechaza (formato) | operaciones con lotes, grupo `<elementos>` (`facturas/factura`, `cuentas/cuenta`) | 18 |
| 501 | "Ocurrió un error intentando autenticar el pedido" | rechaza | todas menos `dummy`, `<autenticacion>` | 16, 10 |
| 502 | "La Sección de Autenticación del Request no cumple con el Esquema (XSD) de Autenticación" | rechaza (en la realidad, un bloque fuera del XSD da fault, ver "Autenticación") | `<autenticacion>` | 16 |
| 503 | "El Formato del Token es inválido" | rechaza. Real: `El Formato del Token es inválido. {}` con token vacío [VIVO] | `<autenticacion>/token` | 16 |
| 504 | "El Formato de la Firma es inválida" | rechaza. Real: `El Formato de la Firma es inválida. {}` con sign vacío [VIVO] | `<autenticacion>/sign` | 16 |
| 505 | "El Token no se corresponde con la Firma" | rechaza. Real: `El Token no se corresponde con la Firma. {token = [<token>]firma = [<sign>]}` [VIVO] | `<autenticacion>` | 16 |
| 506 | "El Token no cumple con el Esquema (XSD) de Autenticación" | rechaza. Real: `El Token no cumple con el Esquema (XSD) de Autenticacion. {<token>}` [VIVO] | `<autenticacion>/token` | 16 |
| 507 | "El Formato del Servicio asociado a Token es inválido" | rechaza | `<autenticacion>` | 16 |
| 508 | "El Servicio asociado a Token difiere del especificado para el Sistema" | rechaza (token pedido para otro service id) | `<autenticacion>` | 16 |
| 509 | "El Token se encuentra Expirado" | rechaza | `<autenticacion>` | 16 |
| 510 | "La CUIT, CUIL o CDI en el Token es Nula, esta Vacia o tiene un Formato inválido" | rechaza | `<autenticacion>` | 16 |
| 511 | "La CUIT, CUIL o CDI no pudo ser encontrada" | rechaza | `<autenticacion>` | 16 |
| 512 | "La CUIL o CDI no esta activa" | rechaza | `<autenticacion>` | 16 |
| 513 | "La CUIT no esta activa" | rechaza | `<autenticacion>` | 16 |
| 514 | "La CUIT no tiene un domicilio activo" | rechaza | `<autenticacion>` | 16 |
| 515 | "La CUIT no tiene una actividad activa" | rechaza | `<autenticacion>` | 16 |
| 516 | "El Servicio de Autenticación no se encuentra Operativo" | rechaza | `<autenticacion>` | 16 |
| 4008 | "Ocurrió un error intentando realizar las validaciones generales para la operacion" | rechaza | todas, "Error Generico de Autorizacion" | 18 |
| 4009 | "La CUIT del Agente no se encuentra en los Registros de AFIP" | rechaza | todas, "Error Agente no Habilitado para operar con el Servicio u Operacion" (`cuitRepresentada` no es agente) | 18-19 |
| 4001 | "Ocurrió un error intentando ejecutar la operación" | rechaza (error interno; se informa con un código `[xxxyyyzzz-cuitRepresentada-fechaHora-xyz]`) | todas | 19 |
| 4002 | "Ocurrió un error intentando ejecutar la operación" | ídem | todas | 19 |
| 4003 | "Ocurrió un error intentando ejecutar la operación" | ídem | todas | 19 |
| 4004 | "Ocurrió un error intentando ejecutar la operación" | ídem | todas | 19 |
| 4005 | "Ocurrió un error intentando ejecutar la operación" | ídem | todas | 19 |
| 4006 | "Ocurrió un error intentando ejecutar la operación" | ídem | todas | 19 |
| 4007 | "Ocurrió un error intentando ejecutar la operación" | ídem | todas | 19 |
| 4010 | "Ocurrio un error inesperado intentando dar de Alta el lote de Cuentas Enviado" | rechaza (lote) | `altaCuentasAgente`, `<cuentas>` | 36 |
| 4011 | "Ocurrio un error inesperado intentando dar de Baja el lote de Cuentas Enviado" | rechaza (lote) | `bajaCuentasAgente`, `<cuentaAgente>` | 39 |
| 4012 | "Ocurrio un error intentando dar de Alta la Cuenta Enviada" | rechaza ítem | `altaCuentasAgente`, `<cuenta>` | 36 |
| 4013 | "Ocurrio un error intentando dar de Baja la Cuenta Enviada" | rechaza ítem | `bajaCuentasAgente`, `<cuentaAgente>` | 39 |
| 4014 | "No se encontro una Cuenta Activa asociada a los datos Enviados" | rechaza ítem | `bajaCuentasAgente`, `<cuentaAgente>` | 39 |
| 4015 | "El Titular con el que intenta efectuar la operación no esta asociado a la cuenta indicada" | rechaza ítem | `bajaCuentasAgente`, `<cuitTitular>` | 39 |
| 4016 | "Ocurrio un error inesperado intentando consultar las Cuentas del Agente" | rechaza | `consultarCuentasAgente`, `<cuentas>` | 33 |
| 4017 | "Ocurrió un error inesperado intentando confirmar el lote de Solicitudes de Informe Enviado" | rechaza (lote) | `confirmarFacturasInformadas`, `<facturas>` | 30 |
| 4018 | "Ocurrió un error intentando Confirmar la Solicitud de Informe" | rechaza ítem | `confirmarFacturasInformadas`, `<factura>` | 30 |
| 4019 | "No se encontró ninguna Solicitud de Informe al Agente con los Datos del Comprobante Enviados que se encuentre Pendiente de Confirmar" | rechaza ítem | `confirmarFacturasInformadas`, `<factura>` | 30 |
| 4020 | "La Solicitud de Informe ya fue Aceptada por el Agente" | rechaza ítem | `confirmarFacturasInformadas`, `<factura>` | 30 |
| 4021 | "La Solicitud de Informe debe ser Consultada por el Agente antes de ser enviada para Confirmar" | rechaza ítem | `confirmarFacturasInformadas`, `<factura>` | 30 |
| 4022 | "Ocurrio un error inesperado intentando consultar las Solicitudes de Informe" | rechaza | `consultarFacturasInformadas`, `<factura>` | 27 |

(*) "El sistema es parametrizado internamente para ajustar la carga recibida" (pág. 18): el rango máximo de fechas y la cantidad máxima de elementos son configurables del lado de ARCA. Sus valores: NO VERIFICADO.

"Rechaza ítem" con `resultado` = `R` en el ítem: INFERIDO del "Campo/Grupo" (`<factura>`, `<cuenta>`, `<cuentaAgente>`) contra `<facturas>`/`<cuentas>` para el lote.

El manual no define ningún código `observaciones` ni ningún resultado `O`, aunque el XSD tiene `observaciones` en las consultas y `O` en `ResultadoSimpleType`. Lista en `wsfecredagente-codigos.json` (50 entradas).

## Tablas y datos

- **Estados de la factura informada** (`EstadoInformeSimpleType`): `D` Disponible, `P` Pendiente, `A` Aceptada, `R` Rechazada (manual pág. 40; texto completo en "Tipos compartidos").
- **Estados de la cuenta** (`EstadoCuentaAgenteSimpleType`): `A` Activa, `B` Inactiva (pág. 40).
- **Tipos de fecha para filtrar facturas**: `Disponible`, `Consultada`, `Confirmada`. **Para cuentas**: `Alta`, `Baja` (pág. 41).
- **Resultado** (`ResultadoSimpleType`): `A` Aceptado, `O` Observada, `R` Rechazado (pág. 40).
- **Motivos de rechazo** (`obtenerMotivosRechazo`): el manual **no trae la tabla**. NO VERIFICADO. ArcaSim necesita una tabla configurable.
- **Tipos de comprobante** (`tipoCmp`): el manual no los lista. En la práctica son los de FCE de `wsfev1` (`wsfev1.md` §7.1): 201 Factura de Crédito electrónica MiPyMEs (FCE) A, 206 (FCE) B, 211 (FCE) C; y sus notas de débito y crédito 202/203, 207/208, 212/213. Como `informarFacturaAgtDptoCltv` informa "la factura de crédito" (manual de wsfecred §2.4.6, pág. 30), al agente sólo deberían llegar 201, 206 y 211 [INFERIDO].
- **Monedas** (`codMoneda`): "PES, DOL, etc" (pág. 50). Son los códigos de `FEParamGetTiposMonedas` (`wsfev1.md` §7.5) [INFERIDO].
- **Datos de prueba de homologación**: el manual no publica CUITs de agentes, cuentas ni facturas de prueba. Las llamadas de este relevamiento usaron `20111111112` sólo como CUIT sintácticamente válida.

## Comportamiento a simular

### Protocolo

1. Ruta `/wsfecredagente/FECredAgenteService` (con y sin barra final) y `?wsdl` sirviendo el WSDL con `soap:address` apuntando a ArcaSim. Publicar el WSDL tal cual, con `obtenerCuitsEmisores` en el `portType` y fuera del binding.
2. Sólo SOAP 1.1. Hijos sin namespace (`unqualified`). Validar el request contra el XSD: si falla, Fault HTTP 500.
3. Enrutamiento como en "Contrato → Enrutamiento": por `SOAPAction` si viene y no está vacío; si no, por el elemento raíz. `SOAPAction` de `dummy` gana sobre el body. `GET` al endpoint responde el dummy.
4. Respuesta: `Content-Type: text/xml;charset=UTF-8`, sin `<?xml?>`, una línea, `soap:` para el envelope, `ns2:` para el elemento de respuesta, hijos sin prefijo. Header `info` con namespace `http://headers.springbootws.factu.fisca.afip.gob.ar/xml`, `fecha` `aaaa-mm-ddThh:mm:ss` en hora de Argentina, `ambiente` propio (p. ej. `arcasim - local`) e `id` `wsfecredagente 1.1.1 2021-11-01T19:10:21.225Z` para que coincida con el real [recomendación].
5. **Modo F5** opcional: cuando el backend respondería un Fault, devolver en su lugar HTTP/1.0 200, sin `Content-Type`, `Content-Length: 39` y el cuerpo `BL<13 dígitos> <aaaa-mm-dd hh:mm:ss> 500`. Por defecto conviene emitir el Fault SOAP 1.1 estándar (`faultcode` `soap:Client` para errores del cliente) [recomendación; el Fault real no se puede ver].
6. `obtenerCuitsEmisores`: responder Fault (o `BL...500` en modo F5), igual que el real.
7. Errores de autenticación y negocio: HTTP 200 dentro del resultado, con los campos obligatorios de la respuesta igual presentes (`<facturasInformadas/><nroPagina>0</nroPagina><hayMas>N</hayMas>`, `<resultados/>`). Textos de autenticación con el sufijo real (`. {token = [..]firma = [..]}`, `. {<token>}`, `. {}`).

### Estado que hay que guardar

**Cuentas del agente** (las crea `altaCuentasAgente`):

| Dato | Origen |
|---|---|
| CUIT del agente | `cuitRepresentada` del alta |
| `cuentaId` | request |
| `cuitTitular` | request |
| `denominacion` | request (opcional) |
| estado `A`/`B` | `A` al alta, `B` a la baja |
| fecha de alta, fecha de baja | reloj del simulador (para `filtroFechas` `Alta`/`Baja`) |

La identidad de la cuenta es (agente, `cuentaId`) y el titular es un atributo: lo sugiere 4015, "El Titular con el que intenta efectuar la operación no esta asociado a la cuenta indicada" [INFERIDO].

**Solicitudes de informe** (una por factura informada a un agente; las crea `wsfecred.informarFacturaAgtDptoCltv`):

| Dato | Origen |
|---|---|
| `idFactura` (`cuitEmisor`, `tipoCmp`, `ptoVta`, `nroCmp`) | la factura de `wsfecred` |
| `cuitComprador`, `razonSocialComprador` | la factura y el padrón simulado |
| `fechaEmision`, `fechaVencimientoPago`, `codMoneda` | la factura (`wsfev1` / `wsfecred`) |
| `saldoNegociable` | saldo aceptado de la cuenta corriente en `wsfecred` |
| `cuentaAgente` | la `ctaAgente` que eligió el vendedor en `informarFacturaAgtDptoCltv` |
| `estadoInforme` | `D` al crearse |
| `fechaHoraDisponible` | instante de `informarFacturaAgtDptoCltv` |
| `fechaHoraLecturaAgente` | instante de la consulta que la pasa a `P` |
| `fechaHoraConfirmacionAgente` | instante de la confirmación |
| `codRechazo` | si el agente la rechazó |

### Relación con `wsfecred`

Lo que dicen los manuales:

1. La factura se emite en `wsfev1` como FCE con el opcional 27 = `ADC` ("AGENTE DE DEPOSITO COLECTIVO") o `SCA` (`wsfev1.md` §7.7). En `wsfecred` el vendedor puede cambiar la opción mientras la cuenta corriente esté "Modificable" (`modificarOpcionTransferencia`, [MAN-FECRED] §2.4.8, pág. 34; error 7000 si repite la misma).
2. El comprador acepta la factura (`aceptarFECred`) o ARCA la acepta tácitamente al vencer el plazo. Con opción ADC y saldo negociable positivo, la cuenta corriente queda "Aceptada" ([MAN-FECRED] §1.5.1, págs. 12-13).
3. El vendedor consulta las cuentas que los agentes dieron de alta a su nombre con `consultarCuentasEnAgtDptoCltv` ("sus Cuentas en Agentes de Depósito Colectivo que fueron informadas por ellos a la AFIP", [MAN-FECRED] §2.4.12, pág. 44). Esa respuesta trae `cuitAgente`, `razonSocialAgente`, `idCuenta` y `denominacion` [WSDL de wsfecred]. O sea: las cuentas de `altaCuentasAgente` con `cuitTitular` = vendedor y estado `A` son las que ve el vendedor, con `cuitAgente` = `cuitRepresentada` del agente e `idCuenta` = `cuentaId` [INFERIDO; la correspondencia campo a campo no está escrita].
4. El vendedor llama `informarFacturaAgtDptoCltv(idCtaCte, ctaAgente)` "para aquellas Facturas con opción de transferencia "Agente de Depósito Colectivo"", con "el saldo negociable resultante de la cuenta corriente vinculada aceptada por el comprador" ([MAN-FECRED] §2.4.6, pág. 30). La factura pasa al estado "Informada" y la cuenta corriente a "Informada" (`InformadaAgDpto`) ([MAN-FECRED] pág. 12-13). **Desde ese momento la factura está en `D` para el agente** (manual de wsfecredagente pág. 13: "Cuando los Vendedor las informen quedaran en un estado "Disponible"").
5. No se puede informar al agente: una factura con opción SCA ("quedarán aceptadas sin posibilidad de cambiar su estado", [MAN-FECRED] pág. 13, nota 1), una cancelada totalmente ("no podrán ser informadas al Agente", pág. 20), una rechazada (pág. 26).
6. Errores de `informarFacturaAgtDptoCltv` que dependen del estado en el agente ([MAN-FECRED] pág. 31): 6000 "La factura ya fue informada al agente de depósito colectivo"; 6001 "...ya fue informada al agente de depósito colectivo y se encuentra pendiente de confirmación de recepción"; 6007 "La factura se encuentra a la espera de la lectura del agente de depósito colectivo"; 6002 "Los datos de la cuenta en el agente no son válidos para la CUIT representada". La correspondencia con los estados del agente sería: `D` → 6007, `P` → 6001, `A` → 6000; 6002 si la cuenta no existe, no es del vendedor o está en `B` [INFERIDO].
7. El vendedor ve la respuesta del agente en `consultarFacturasAgtDptoCltv` ("Permite obtener si el Agente ha recibido efectivamente el informe, dicho de otra manera, exterioriza el acuse de recibo del Agente", [MAN-FECRED] §2.4.16, pág. 50), con `InfoAgtDptoCltvType` ([MAN-FECRED] págs. 79-80): `fechaInfo`, `ctaAgente`, `recibida` (S/N), `fechaLectura` ("Fecha en la que el Agte de Depósito Colectivo tomó la información de los sistemas de AFIP"), `fechaRecep`, `aceptada` (S/N), `motivoRechazo`, `idPagoAgtDptoCltv`, `CBUAgtDptoCltv`.

Correspondencia propuesta entre los dos servicios [INFERIDO]:

| wsfecredagente | wsfecred (`infoAgtDptoCltv`) |
|---|---|
| `fechaHoraDisponible` | `fechaInfo` (sólo fecha) |
| `cuentaAgente` | `ctaAgente` |
| paso a `P` (`fechaHoraLecturaAgente`) | `fechaLectura` |
| confirmación (`fechaHoraConfirmacionAgente`) | `fechaRecep`, `recibida` = `S` |
| `aceptada` = `S` → `A` | `aceptada` = `S` |
| `aceptada` = `N` + `codRechazo` → `R` | `aceptada` = `N`, `motivoRechazo` = descripción del motivo |

`idPagoAgtDptoCltv` y `CBUAgtDptoCltv` no tienen origen en este servicio: ninguna operación de `wsfecredagente` los recibe. NO VERIFICADO de dónde salen.

### Transiciones de la solicitud de informe

| Estado actual | Evento | Resultado | Fuente |
|---|---|---|---|
| (no existe) | `wsfecred.informarFacturaAgtDptoCltv` OK | `D`, `fechaHoraDisponible` = ahora | [MAN] pág. 13 |
| `D` | aparece en una respuesta de `consultarFacturasInformadas` | `P`, `fechaHoraLecturaAgente` = ahora | [MAN] págs. 13 y 25 |
| `P` | `consultarFacturasInformadas` de nuevo | sigue `P` | [INFERIDO] |
| `P` | `confirmarFacturasInformadas` con `aceptada` = `S` | `A`, `fechaHoraConfirmacionAgente` = ahora, ítem `resultado` = `A` | [MAN] págs. 13, 28 y 40 |
| `P` | `confirmarFacturasInformadas` con `aceptada` = `N` y `codRechazo` válido | `R`, `fechaHoraConfirmacionAgente` = ahora | [MAN] págs. 28 y 40 |
| `D` | confirmar | error 4021, sin cambio | [MAN] pág. 30 |
| `A` | confirmar de nuevo (S o N) | error 4020, sin cambio | [MAN] pág. 30 |
| `R` | confirmar de nuevo | NO VERIFICADO. No hay código propio; 4019 ("...que se encuentre Pendiente de Confirmar") es el que mejor encaja [INFERIDO] | — |
| no existe para ese agente | confirmar | error 4019 | [MAN] pág. 30 |

**Idempotencia**: confirmar no es idempotente. La segunda confirmación de una factura aceptada devuelve 4020 en ese ítem [MAN]. Cada factura del lote se resuelve por separado [INFERIDO: `resultados` tiene un `resultado` por factura con su propio `errores`].

Casos abiertos que ArcaSim tiene que decidir y dejar configurables:

- **Qué facturas pasan a `P`.** El manual dice "Finalizada la consulta todas las facturas resultantes quedaran "Pendientes"" (pág. 13). Lo más seguro es pasar a `P` sólo las facturas en `D` **devueltas en la página** [INFERIDO]. Si pasaran todas las que cumplen el filtro, las páginas siguientes de un filtro `D` vendrían vacías. NO VERIFICADO.
- **Consultar sin filtro de estado** devuelve todas (D, P, A, R) que cumplan el resto de los filtros, y sólo las `D` cambian a `P` [INFERIDO].
- **`aceptada` = `N` sin `codRechazo`**, o `aceptada` = `S` con `codRechazo`: NO VERIFICADO. Lo más probable es 2006 [INFERIDO].
- **Rechazo del agente y nuevo informe**: si después de un `R` el vendedor puede volver a informar la factura (a otra cuenta o al mismo agente): NO VERIFICADO.

### Cuentas: alta, baja, duplicados

- Alta: crea la cuenta en estado `A` con fecha de alta. Resultado por cuenta en `resultados/resultado` con `cuentaAgente` (eco de lo enviado) y `resultado` `A` [MAN pág. 54: "Cuenta Agente Resultante del Alta exitosa"].
- Baja: sólo sobre cuentas activas: "cuentas que ya se hayan declarado mediante el alta y estén activas" (pág. 37). Pasa a `B` con fecha de baja. Si no hay cuenta activa con ese `cuentaId` → 4014. Si existe pero con otro titular → 4015.
- **Baja dos veces**: la segunda da 4014 (ya no está activa) [INFERIDO del texto].
- **Alta duplicada** (mismo `cuentaId` activo): el manual no tiene código específico. NO VERIFICADO; lo más cercano es 4012 en el ítem con `resultado` `R` [INFERIDO].
- **Alta de una cuenta dada de baja** (reactivar): NO VERIFICADO.
- **Baja de una cuenta con facturas informadas en `D` o `P`**: NO VERIFICADO.
- `consultarCuentasAgente` sin `estadoCuenta` devuelve activas e inactivas; el `filtroFechas` es obligatorio, así que una cuenta sin fecha de baja nunca sale con `tipo` = `Baja` [INFERIDO].

### Paginación

- `nroPagina` (`short`) obligatorio en el request; la respuesta devuelve `nroPagina` ("Numero de Pagina Devuelto") y `hayMas` `S`/`N` [MAN págs. 25-26, 31-32].
- La primera página es la 1 [INFERIDO: el manual de wsfecred, §2.4.9, pág. 36, describe el mismo mecanismo con "el número de página 1, 2, 3, etc., manteniendo los mismos filtros entre los consecutivos request hasta que el servicio le responda que "NO hay mas"" y dice que "El tamaño de página será manejado por el servicio de manera dinámica"].
- Tamaño de página: NO VERIFICADO. Configurable en ArcaSim.
- Página inválida (≤ 0, o mayor que la última): 2004 en `erroresFormato` [INFERIDO para el criterio exacto].
- Con error, el real devuelve `nroPagina` = `0` y `hayMas` = `N` [VIVO].

### Filtro de fechas

- `tipo` elige la marca de tiempo: facturas `Disponible` → `fechaHoraDisponible`, `Consultada` → `fechaHoraLecturaAgente`, `Confirmada` → `fechaHoraConfirmacionAgente`; cuentas `Alta`/`Baja` → fecha de alta/baja [MAN pág. 41; documentación del XSD].
- `desde` y `hasta` son fechas sin hora; tomarlas inclusivas [INFERIDO].
- `desde` > `hasta` o rango mayor al máximo configurado → 2003 [INFERIDO; el manual sólo dice "Rango de Fechas inválido (*)" y que el límite es parametrizado].
- Una factura sin la marca pedida (p. ej. `Confirmada` en una factura `D`) no entra en el filtro [INFERIDO].

### Sin datos

Con filtros válidos y nada para devolver: `<facturasInformadas/>` o `<cuentasAgente/>`, `hayMas` = `N` [WSDL: el array admite 0 elementos]. Si además viene una observación (en `wsfecred` es la 32767 "La búsqueda no ha arrojado resultados con los filtros indicados", [MAN-FECRED] págs. 45 y 51) y qué `nroPagina` se devuelve: NO VERIFICADO.

### Lotes

`confirmarFacturasInformadas`, `altaCuentasAgente` y `bajaCuentasAgente` aceptan 1..n elementos. Límite configurable (2009). Cada elemento se procesa por separado y tiene su `resultado`; los errores del lote entero (4010, 4011, 4017) van en `errores` del resultado general [INFERIDO]. Si un elemento falla, si los demás se procesan igual: NO VERIFICADO; lo coherente con `resultados` por ítem es que sí [INFERIDO].

### Autorización

- El token tiene que ser de `service` = `wsfecredagente` (508 si no).
- `cuitRepresentada` tiene que estar en las relaciones del token y ser un agente habilitado; si no es agente → 4009 [MAN págs. 15 y 18].
- Cada agente ve sólo sus cuentas y las facturas informadas a sus cuentas [INFERIDO].

## No verificado

1. Tabla real de motivos de rechazo y qué rama de `ArrayParametrosType` usa `obtenerMotivosRechazo`.
2. Propósito y forma de `obtenerCuitsEmisores` (no está implementada en homologación: responde Fault).
3. Contenido del SOAP Fault real detrás de `BL...500` (faultcode, faultstring, si incluye los códigos 1, 2 o 1001).
4. Si alguna vez aparecen 502 y 1001 dentro del resultado o siempre terminan en Fault.
5. Si la consulta pasa a `P` sólo las facturas de la página devuelta o todas las que cumplen el filtro; si `estadoInforme` se devuelve antes o después del cambio; si `fechaHoraLecturaAgente` se actualiza en consultas posteriores.
6. Qué pasa al confirmar una factura en `R`, al rechazar sin `codRechazo` y al aceptar con `codRechazo`.
7. Alta duplicada, reactivación de una cuenta dada de baja y baja de una cuenta con facturas en `D`/`P`.
8. Formato y largo que exige 2005 para `cuentaId` (en `wsfecred` `idCuenta` es de 3 a 20 caracteres).
9. Tamaño de página, número de la primera página, criterio de 2004 y `nroPagina` devuelto cuando no hay datos.
10. Rango máximo de fechas (2003) y cantidad máxima de elementos por lote (2009).
11. Si existe alguna observación (p. ej. 32767) cuando la búsqueda no trae resultados; cuándo se usa el resultado `O`.
12. Formato de `xsd:dateTime` en las respuestas (milisegundos, zona horaria).
13. Si `fechaEmision` es la fecha de emisión del comprobante o la del informe (el manual dice lo segundo).
14. Origen de `idPagoAgtDptoCltv` y `CBUAgtDptoCltv` que ve el vendedor en `wsfecred`, y la correspondencia exacta entre los estados del agente y los campos `recibida`/`aceptada`/`fechaRecep` de `wsfecred`.
15. Mapeo exacto de 6000/6001/6007 de `wsfecred` contra los estados `D`/`P`/`A`/`R`, y si una factura rechazada por el agente se puede volver a informar.
16. Códigos 507-516, 4008, 4009 y 4001-4007 en vivo (hace falta certificado de homologación con el servicio `wsfecredagente` asociado).
17. Textos reales de los errores 2xxx y 40xx (si llevan sufijo `. {...}` como los de autenticación).
18. Valores de `dummy` distintos de `OK`; eventos (`evento`) que publique ARCA.
