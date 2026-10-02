# wsagr

Web service de AGR (Reproweb), RG 4035. Según el manual, el cliente manda una CUIT o un lote de CUITs y un período (`mm/aaaa`), y el servicio devuelve la **calificación** de cada CUIT para ese período (manual §2.2, pág. 6). La calificación es un código de respuesta (`Rsp`) cuyo significado se obtiene con `ConsultaCodResp`. El ejemplo de esa operación (manual §2.6.5, pág. 25-26) muestra de qué se trata: los códigos son condiciones de **retención de IVA** que el consultante tiene que aplicarle a cada proveedor: `0` "RG 2226", `1` "Retención General vigente RG 2854, ...", `2` "Retención Sustitutiva 100% RG 2854 (AFIP)", `3` "CREDITO FISCAL NO COMPUTABLE", `4` "Retención Sustitutiva 100% RG 2854 (AFIP) - Irregularidades en la cadena de comercialización del proveedor", `5` "RETENCION SUSTITUTIVA 100% R.G. 1575 AFIP - HABILITADO FACTURA \"M\"". El error 501 habla de "Inscripción en el régimen, autorización de acceso a AGR" (manual §2.2.3, pág. 8). El manual no explica qué quieren decir las siglas "AGR" ni "Reproweb", y tampoco dice quién está obligado a consultar. Fuera de eso, el propósito queda claro: consultar en lote qué régimen de retención de IVA corresponde a cada proveedor en un mes dado.

Manual: "WSAGR – SOAP WebService para Reproweb – RG 4035 – Manuales para el desarrollador", **V1.0, 28/04/2017**, AFIP-SDG SIT, 32 páginas. URL: `https://www.afip.gob.ar/ws/agrREPROWEB/manual_desarrollador_wsagr.pdf`. El changelog (§3, pág. 32) está vacío.

El manual es flojo y se contradice con el WSDL en casi todos los nombres de elementos de respuesta (ver "Errores" y "No verificado"). **El simulador sigue al WSDL y a las capturas del 2026-10-02.**

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **.NET ASMX**. Lo indican el endpoint `.asmx`, el prefijo de esquema `s:`, el `wsdl:documentation` por operación, los dos bindings (`WSAgrSoap` y `WSAgrSoap12`) y las respuestas observadas: `soap:Envelope` con `xmlns:xsi`/`xmlns:xsd` declarados, el elemento de respuesta con namespace por default (`<dummyResponse xmlns="http://servicios1.afip.gob.ar/wsagr/">`) y el header HTTP `MicrosoftOfficeWebServer: 5.0_Pub`. A diferencia de wsfev1/wscdc **no hay `soap:Header`** (no hay `FEHeaderInfo`) |
| Endpoint homologación | `https://wswhomo.afip.gov.ar/wsagr/wsagr.asmx` (`soap:address` y `soap12:address` del WSDL). El manual (§1.5, pág. 5) es inconsistente: para ver el WSDL da la URL correcta, `https://wswhomo.afip.gov.ar/wsagr/wsagr.asmx?WSDL`, pero para llamar al servicio da `https://wswhomo.afip.gov.ar/wsagr.asmx`, y los `?op=` de cada método también usan esa base. Según catalogo.md, `wswhomo.afip.gov.ar/wsagr.asmx?WSDL` da 404 |
| Endpoint producción | `https://servicios1.afip.gov.ar/wsagr/wsagr.asmx` (`soap:address` del WSDL de producción). El manual da `https://servicios1.afip.gov.ar/wsagr` para llamar y `https://servicios1.afip.gov.ar/wsagr.asmx?WSDL` para el WSDL; según catalogo.md, esa URL de WSDL da 404 |
| targetNamespace | `http://servicios1.afip.gob.ar/wsagr/` (ojo: `gob.ar` en el namespace, `gov.ar` en el host) |
| service / ports / bindings / portType | service `WSAgr`; ports `WSAgrSoap` (binding `tns:WSAgrSoap`, SOAP 1.1) y `WSAgrSoap12` (binding `tns:WSAgrSoap12`, SOAP 1.2); portType `WSAgrSoap` |
| WSDL guardado | `docs/arca/wsdl/wsagr-homologacion.wsdl`. Autocontenido: no tiene `wsdl:import`, `xsd:import` ni `xsd:include`. El de producción es idéntico salvo los dos `address` (diff del 2026-10-02) |
| WSAA service id | No indicado en el manual: **NO VERIFICADO**. El manual sólo dice que se complementa con el documento del WSAA (§1.2, pág. 4). Es razonable suponer `wsagr`, pero no está confirmado; ver catalogo.md §8 |
| SOAPAction | targetNamespace + nombre de la operación, p. ej. `http://servicios1.afip.gob.ar/wsagr/Consulta`; para dummy, `http://servicios1.afip.gob.ar/wsagr/dummy`. Igual en los dos bindings (en SOAP 1.2 va como parámetro `action` del `Content-Type`) |
| Versión SOAP | 1.1 y 1.2. Captura del 2026-10-02: `dummy` responde HTTP 200 por las dos; por 1.2 responde con `Content-Type: application/soap+xml; charset=utf-8` y envelope `http://www.w3.org/2003/05/soap-envelope`; por 1.1, `text/xml; charset=utf-8` |
| Estilo | document/literal, wrapped (un elemento por operación) |
| elementFormDefault | `qualified`: **todos** los elementos (también `Auth`, `Token`, `Periodo`, `Cuit`) van en el namespace `http://servicios1.afip.gob.ar/wsagr/` |
| Operaciones | 8: `Consulta`, `ConsultaHistorica`, `ConsultaRectificada`, `ConsultaCondRet`, `ConsultaCodResp`, `ConsultaCantCuit`, `ConsultaObs`, `dummy` |

### Qué dice el manual de SOAP 1.2

No lo menciona en el texto. Pero casi todos los **ejemplos** de request y response (§2.2.4 en adelante) usan el envelope SOAP 1.2 (`xmlns:soap="http://www.w3.org/2003/05/soap-envelope"`, con `<soap:Header/>` vacío en el request), mientras que los "esquemas" de cada método y el ejemplo de Fault (§1.3.2) usan SOAP 1.1 (`http://schemas.xmlsoap.org/soap/envelope/`). O sea: el manual da por hecho que funcionan los dos, sin decirlo. El WSDL lo confirma con dos bindings y el dummy por 1.2 se observó funcionando el 2026-10-02.

Ojo con los ejemplos del manual: declaran `xmlns:wsr="http://servicios1.afip.gob.ar/wsagr/"` pero usan el prefijo `wsag:` (XML mal formado, salvo en §2.7.5, §2.8.5 y §2.9 que declaran `wsag`), escriben `wsag:auth` en minúscula (el WSDL dice `Auth`) y el dummy lo llaman `AGRDummy`. Copiados tal cual, fallan.

## Autenticación

Elemento `Auth` (tipo `tns:Auth`), **primer hijo** del elemento de la operación, en todas las operaciones salvo `dummy`:

```xml
<Auth>
  <Token>string</Token>   <!-- minOccurs=0 -->
  <Sign>string</Sign>     <!-- minOccurs=0 -->
  <Cuit>long</Cuit>       <!-- minOccurs=1, nillable -->
</Auth>
```

- Nombres exactos con mayúscula inicial: `Auth`, `Token`, `Sign`, `Cuit`. Todos en el namespace del servicio (qualified).
- `Cuit` es `s:long` (11 dígitos, "CUIT contribuyente (representado o Emisora)", manual §2.2.1, pág. 7).
- En el WSDL `Auth` es `minOccurs="0"` en todas las operaciones, y `Token`/`Sign` también son opcionales: un request sin `Auth` es válido contra el esquema y llega a la lógica de negocio (ver la falla 500 abajo).

### Falla documentada

El manual (§2.2.3, pág. 8; se repite el 501 en cada método) lista, como "Validaciones excluyentes" sobre `Auth`:

| Campo | Código | Descripción (manual) |
|---|---|---|
| Cuit | 501 | "Verificación de datos registrales, Inscripción en el régimen, autorización de acceso a AGR." |
| Cuit | 101 | "El campo CUIT es obligatorio" |
| Sign | 116 | (sin descripción en el manual) |
| — | 115 | (sin descripción en el manual) |
| — | 117 | (sin descripción en el manual) |

El ejemplo de "error a nivel general" del manual (pág. 10) muestra el 117: `<Code>117</Code><Msg>La CUIT ingresada está mal formada</Msg>` (con CUIT de auth `00000000001`).

Todas vuelven **dentro de la respuesta**, con HTTP 200, no como Fault.

### Falla observada (homologación, 2026-10-02, operación `Consulta`, SOAP 1.1)

Todas con **HTTP 200**, `Content-Type: text/xml; charset=utf-8`, sin `soap:Header`:

| Caso | Body |
|---|---|
| `Token`/`Sign` = "abc", Cuit 20111111112 | `<ConsultaResponse xmlns="http://servicios1.afip.gob.ar/wsagr/"><Respuesta><Err><Code>501</Code><Msg>Error de token: ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</Msg></Err></Respuesta></ConsultaResponse>` |
| `Token`/`Sign` vacíos | `...<Respuesta><Err><Code>115</Code><Msg>El campo Token no fue ingresado</Msg></Err></Respuesta>...` |
| Sin `Auth` (`<w:Consulta/>` vacío) | `...<Respuesta><Err><Code>500</Code><Msg>Error interno de la aplicacion: Error inesperado: Object reference not set to an instance of an object.</Msg></Err></Respuesta>...` |

Notas:

- El 501 que en el manual es "datos registrales / inscripción / autorización AGR" también se usa para el token inválido. El texto `ValidacionDeToken: Error al verificar hash: VerificacionDeHash: ...` es el mismo que wscdc devuelve con código 600 (catalogo.md §4.1): misma librería de validación .NET.
- El 115 es "token no ingresado": coincide con el hueco sin descripción del manual.
- El request del caso "abc" traía `Periodo` = `202601` (formato inválido según el manual) y aun así volvió 501: **la autenticación se valida antes que el período**. El caso de token vacío no traía `Periodo` ni `Cuit` a consultar y volvió 115: también se valida antes.
- Sin `Auth`, el servicio no valida: explota con una `NullReferenceException` y la envuelve como 500. El simulador debería imitarlo (no inventar un código "más lógico").

### Request mínimo válido contra el esquema (SOAP 1.1)

```http
POST /wsagr/wsagr.asmx HTTP/1.1
Host: wswhomo.afip.gov.ar
Content-Type: text/xml; charset=utf-8
SOAPAction: "http://servicios1.afip.gob.ar/wsagr/Consulta"
```

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:w="http://servicios1.afip.gob.ar/wsagr/">
  <soap:Body>
    <w:Consulta>
      <w:Auth>
        <w:Token>PD94...</w:Token>
        <w:Sign>tYft0...</w:Sign>
        <w:Cuit>20111111112</w:Cuit>
      </w:Auth>
      <w:Periodo>10/2026</w:Periodo>
      <w:Cuit>20222222223</w:Cuit>
      <w:Cuit>27333333334</w:Cuit>
    </w:Consulta>
  </soap:Body>
</soap:Envelope>
```

Las CUITs a consultar van como `Cuit` **repetidos directamente bajo `Consulta`**, sin contenedor (WSDL). El manual dibuja en algunos esquemas un contenedor `<cuits><cuit>` (§2.3.2, §2.5.2) que el WSDL no tiene.

## Operaciones

Ninguna operación "crea" un objeto con id propio. Lo único con estado es el **historial de consultas**: cada `Consulta` deja registradas, por consultante (`Auth/Cuit`), las calificaciones que devolvió (con `RTran` y `FTran`), y las demás consultas leen ese historial.

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `Consulta` | Consulta puntual: calificación de una CUIT o lote para un período (§2.2) | `Auth`, `Periodo` (`mm/aaaa`), `Cuit` 0..n | `ConsultaResponse/Respuesta` (`ResConsulta`): `Det/DetalleCuits*`, `Err`, `Evt` | **Crea** un registro por CUIT consultada (consultante + CUIT + período, con `RTran` y `FTran`). Lo leen `ConsultaHistorica`, `ConsultaRectificada` y `ConsultaCondRet` con la clave consultante + CUIT + período |
| `ConsultaHistorica` | Busca en lo ya consultado **por el mismo cliente**, por CUIT, por período o ambos; al menos uno (§2.3) | `Auth`, `Periodo` 0..1, `Cuit` 0..n | `ConsultaHistoricaResponse/Respuesta` (`ResConsultaHistorica`), misma forma que `Consulta` | Consulta estado. Con sólo CUITs devuelve todos los períodos con calificación. Si nunca se consultó, **respuesta vacía sin error** (nota del manual §2.3.3) |
| `ConsultaRectificada` | CUITs consultadas por el mismo contribuyente para el período que **cambiaron de calificación** (§2.4) | `Auth`, `Periodo` | `ConsultaRectificadaResponse/Respuesta` (`ResConsultaRectificada`), misma forma | Consulta estado. "El método informa error si no existen registros" (§2.4); el código no se indica, el candidato es 103 "No se encontraron registros para dicha consulta" (§1.3.1): **NO VERIFICADO** |
| `ConsultaCondRet` | "Consultar condiciones de retención que ya hayan sido consultado por otros": como la histórica, pero sobre CUITs que consultó cualquiera; CUIT y período obligatorios (§2.5) | `Auth`, `Periodo`, `Cuit` 1..n | `ConsultaCondRetResponse/Respuesta` (`ResConsultaCondRet`), misma forma | Consulta estado (historial global, no sólo el propio). Si nunca se consultó, respuesta vacía sin error |
| `ConsultaCodResp` | Tabla de códigos de respuesta (`Rsp`) y su descripción (§2.6) | `Auth` | `ConsultaCodRespResponse/Respuesta` (`ResConsultaCodResp`): `Err`, `Evt`, `Rsp/WsResp*` (`Code`, `Msg`, ambos string) | Consulta tabla de parámetros |
| `ConsultaCantCuit` | Máximo de CUITs por request en `Consulta` (§2.8) | `Auth` | `ConsultaCantCuitResponse/Respuesta` (`ResCantCuit`): `Err` 0..1, `Cantidad` (int, obligatorio) | Consulta parámetro. Valor del ejemplo del manual: 100 |
| `ConsultaObs` | Tabla de códigos de observación (`CodObs`) (§2.7) | `Auth` | `ConsultaObsResponse/Respuesta` (`ResConsultaObs`): `Err`, `Evt`, `Obs/WsObs*` (`Code`, `Msg`, string) | Consulta tabla de parámetros. Ejemplo del manual: `10` "factura M" |
| `dummy` | Estado de los servidores (§2.9) | `dummy` (vacío; el manual lo llama `AGRDummy`) | `dummyResponse/Respuesta`: `AppServer`, `DbServer`, `AuthServer` | Sin estado. Observado: los tres `OK` |

### Forma de la respuesta por operación (WSDL)

Todas las respuestas tienen un solo hijo, **`Respuesta`** (minOccurs 0), y adentro:

```
ResConsulta / ResConsultaHistorica / ResConsultaRectificada / ResConsultaCondRet:
  Det        ArrayOfDetalleCuits  0..1
    DetalleCuits  0..n, nillable
      Cuit    long    1, nillable
      Pdo     string  0..1   período, "mm/aaaa"
      Rsp     string  0..1   código de respuesta (ver ConsultaCodResp)
      RTran   string  0..1   código de transacción
      FTran   string  0..1   fecha de la consulta
      CodObs  string  0..1   código de observación (ver ConsultaObs)
      Err     WsErrors 0..1  error particular de esa CUIT
      Evt     WsEvents 0..1
  Err        WsErrors  0..1   error general
  Evt        WsEvents  0..1

ResConsultaCodResp:  Err 0..1, Evt 0..1, Rsp (ArrayOfWsResp: WsResp* {Code string, Msg string}) 0..1
ResConsultaObs:      Err 0..1, Evt 0..1, Obs (ArrayOfWsObs: WsObs* {Code string, Msg string}) 0..1
ResCantCuit:         Err 0..1, Cantidad int 1
DummyResponse:       AppServer, DbServer, AuthServer (string, 0..1)

WsErrors / WsEvents: Code int (1), Msg string (0..1)
```

Orden de hijos: el WSDL pone `Det, Err, Evt` en las consultas, pero `Err, Evt, Rsp` / `Err, Evt, Obs` / `Err, Cantidad` en las paramétricas. El simulador tiene que respetar ese orden.

Ejemplo de respuesta exitosa armado sobre el WSDL, con los valores del ejemplo del manual (pág. 9-10):

```xml
<ConsultaResponse xmlns="http://servicios1.afip.gob.ar/wsagr/">
  <Respuesta>
    <Det>
      <DetalleCuits>
        <Cuit>27942330007</Cuit>
        <Pdo>10/2015</Pdo>
        <Rsp>3</Rsp>
        <RTran>396548827831086429172</RTran>
        <FTran>06/10/2015</FTran>
        <CodObs />
      </DetalleCuits>
    </Det>
  </Respuesta>
</ConsultaResponse>
```

## Errores

Tres canales:

1. **Error de negocio o de infraestructura**: bloque `Err` (`Code` int, `Msg` string) con HTTP 200. Puede estar en dos niveles:
   - **General**: `Respuesta/Err`. Es lo que vuelve con errores de auth, período, cantidad o duplicados. En ese caso no hay `Det` (capturas del 2026-10-02 y ejemplo del manual, pág. 10).
   - **Particular, por CUIT**: `Respuesta/Det/DetalleCuits/Err`, junto a las CUITs que salieron bien. El manual (§2.2.2) dice que primero se evalúa todo lo general (CUIT consultante, período, cantidad, duplicados) y, si pasa, cada CUIT por separado. Ejemplo del manual: `<DetalleCuits><Cuit>...</Cuit><Pdo>10/2015</Pdo><Err><Code>108</Code><Msg>El CUIT es invalido o inexistente</Msg></Err></DetalleCuits>`.
2. **Eventos**: `Evt` (`Code`, `Msg`), mismo lugar que `Err`. El manual no lista ninguno.
3. **Errores "excepcionales"** (request que no cumple el WSDL: tipo de dato, tags mal cerrados, nombres u orden incorrectos): **SOAP Fault** estándar de ASMX (§1.3.2, pág. 4). Ejemplo del manual:

   ```xml
   <soap:Fault>
     <faultcode>soap:Client</faultcode>
     <faultstring>Server was unable to read request. ---> There is an error in XML document (11, 37). ---> Input string was not in a correct format.</faultstring>
     <detail/>
   </soap:Fault>
   ```

   El HTTP status del Fault no está documentado ni se observó para wsagr; ASMX devuelve HTTP 500 en los Faults (**NO VERIFICADO** para este servicio).

### Códigos

Infraestructura (manual §1.3.1, pág. 4):

| Code | Causa |
|---|---|
| 112 | Error al conectar con la base de datos |
| 103 | No se encontraron registros para dicha consulta |
| 500 | Error interno de la aplicación |

Autenticación: 101, 115, 116, 117, 501 (ver "Autenticación").

Validaciones de negocio (manual §2.2.3, §2.3.4, §2.4.4, §2.5.4):

| Code | Campo | Validación | Métodos |
|---|---|---|---|
| 113 | Periodo | Formato `mm/aaaa`, con cero adelante (ej. `09/2015`) | Consulta, Historica, Rectificada, CondRet |
| 107 | Periodo | Sólo se puede consultar el mes corriente, o el mes siguiente si el día es mayor que 15 | Consulta, Historica, Rectificada (no figura en CondRet) |
| 118 | Periodo | "Es obligatorio el ingreso de un periodo (formato MM/AAAA)" | CondRet |
| 102 | Cuit | La cantidad de CUITs supera el máximo (ver `ConsultaCantCuit`) | Consulta, Historica, CondRet |
| 104 | Cuit | CUITs duplicadas en la lista | Consulta, Historica, CondRet |
| 108 | Cuit | La CUIT no corresponde a un contribuyente; debe estar activa en el Sistema Registral. Mensaje del ejemplo: "El CUIT es invalido o inexistente" | Consulta, Historica, CondRet (error particular por CUIT) |
| 111 | Cuit | Debe ingresar como mínimo una CUIT | Consulta, CondRet |

Mensajes textuales conocidos: sólo los de las capturas (501, 115, 500) y los de los ejemplos del manual (117, 108). Para el resto el manual no da el `Msg` exacto.

## Comportamiento a simular

- **Período**: string `mm/aaaa`. Validar formato (113) y ventana (107: mes corriente; mes siguiente sólo desde el día 16). La regla de la ventana aplica también a `ConsultaHistorica` y `ConsultaRectificada` según el manual, lo cual es raro para una consulta de historial (¿no se puede pedir el historial de un mes viejo?): **NO VERIFICADO** que el servicio real lo haga.
- **Orden de validación** (manual §2.2.2 + capturas): 1) `Auth` (sin `Auth` → 500 NullReference; token vacío → 115; token inválido → 501); 2) generales: período, cantidad (102), duplicados (104), mínimo una CUIT (111); 3) por CUIT (108) dentro de `DetalleCuits`.
- **Lote**: máximo de CUITs por request = lo que devuelva `ConsultaCantCuit` (100 en el ejemplo del manual). Ese valor debe ser configurable en el simulador.
- **Respuesta por CUIT**: `Cuit`, `Pdo`, `Rsp` (código de `ConsultaCodResp`, 0 a 5 en el ejemplo), `RTran` (en los ejemplos, 21 dígitos: `396548827831086429172`), `FTran` (fecha de la consulta, `dd/mm/aaaa` en los ejemplos: `06/10/2015`), `CodObs` (vacío o un código de `ConsultaObs`). El formato de `RTran` no está especificado: el simulador puede generar 21 dígitos al azar, único por consulta.
- **Historial**: guardar cada `Consulta` exitosa por (CUIT consultante, CUIT consultada, período). Reconsultar la misma CUIT y período: el manual no dice si genera un `RTran` nuevo o devuelve el mismo (**NO VERIFICADO**); lo natural es registro nuevo.
- `ConsultaHistorica` y `ConsultaCondRet` sin coincidencias devuelven **respuesta vacía sin error** (manual §2.3.3 y §2.5). Forma exacta de "vacía" (¿`<Respuesta/>`, `<Respuesta><Det/></Respuesta>`?): **NO VERIFICADO**.
- `ConsultaRectificada` sin coincidencias devuelve **error** (manual §2.4).
- Para que `ConsultaRectificada` tenga sentido, el simulador necesita poder cambiar la calificación de una CUIT para un período después de consultada (p. ej. un endpoint de administración).
- **Sin `soap:Header`** en las respuestas, ni siquiera en dummy (captura 2026-10-02).
- **SOAP 1.1 y 1.2**: aceptar los dos, y responder en la misma versión con el `Content-Type` que corresponde.
- Respuestas observadas con `<?xml version="1.0" encoding="utf-8"?>` al principio y el elemento de respuesta con namespace por default (sin prefijo).
- No hay PDFs, paginación ni ids asignados.

## No verificado

- WSAA service id (el manual no lo dice; probablemente `wsagr`).
- Qué significan las siglas AGR y Reproweb, y quién está obligado a consultar el servicio.
- Las descripciones de los códigos 115, 116 y 117 (el manual las deja vacías; 115 y 117 se conocen por la captura y el ejemplo).
- El texto de los `Msg` de 101, 102, 103, 104, 107, 111, 112, 113 y 118.
- El código de error de `ConsultaRectificada` cuando no hay registros (¿103?).
- La forma exacta de la respuesta "vacía" de `ConsultaHistorica` y `ConsultaCondRet`.
- Si la ventana de período (107) se aplica de verdad a `ConsultaHistorica` y `ConsultaRectificada`.
- Si reconsultar la misma CUIT y período genera un `RTran` nuevo.
- La lista completa de códigos `Rsp` y `CodObs` en el servicio real (el manual muestra 0-5 y 10 como ejemplo).
- El valor real de `ConsultaCantCuit` en homologación y producción (el manual dice 100 en el ejemplo).
- HTTP status y texto del Fault ante SOAPAction inválido o XML mal formado (lo usual en ASMX es HTTP 500 con `soap:Client`; no se probó).
- Diferencias manual vs WSDL que el simulador resuelve a favor del WSDL:
  - Elemento de respuesta: el manual alterna entre `ConsultaResult`/`ConsultaHistoricaResult`/... y `Respuesta`; el WSDL y las capturas dicen **`Respuesta`**.
  - `ConsultaCodResp` y `ConsultaObs`: el manual pone `ConsultaCodRespResult/Respuesta/...` o `ConsultaObsResult/...`; el WSDL, `Respuesta/{Err,Evt,Rsp|Obs}` directo.
  - `ConsultaCantCuit`: el manual devuelve `<ConsultaCantCuitResult>100</ConsultaCantCuitResult>`; el WSDL, `<Respuesta><Cantidad>100</Cantidad></Respuesta>` (con `Err` opcional antes).
  - Dummy: el manual dice `AGRDummy`/`AGRDummyResponse`; el WSDL y la captura, `dummy`/`dummyResponse`.
  - Auth: el manual escribe `auth` en minúscula en los ejemplos y `Auth` en los esquemas; el WSDL dice `Auth`.
  - CUITs: el manual dibuja `<cuits><cuit>` en algunos esquemas y `periodo` en minúscula; el WSDL, `Cuit` repetido sin contenedor y `Periodo`.
  - URLs: las de llamada del manual (`/wsagr.asmx` en la raíz del host, y `https://servicios1.afip.gov.ar/wsagr` en producción) no son las del WSDL (`/wsagr/wsagr.asmx`).
