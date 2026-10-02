# ws_sr_padron_a100

Consulta a parámetros de padrón (Alcance 100): recibe el nombre de una tabla de parámetros de SUPA (provincias, formas jurídicas, tipos de domicilio, etc.) y devuelve todos sus registros.

Fuentes y convenciones:

- Manual vigente: `https://www.afip.gob.ar/ws/ws_sr_padron_a100/manual_ws_sr_padron_a100_v2.1.pdf`, versión 2.1 del 20/12/23. Se descargó el 2026-10-02 (HTTP 200, `application/pdf`). "PDF p.N" es la página del archivo; el número impreso ("Pág. N de 16") va 1 atrás.
- Manual anterior, para comparar: `https://www.afip.gob.ar/ws/ws_sr_padron_a100/manual_ws_sr_padron_a100_v1.1.pdf`, v1.1 del 24/07/17 (HTTP 200 el 2026-10-02; no está enlazado desde el catálogo). Las URLs `_v1.0`, `_v1.2` y `_v2.0` dieron 404.
- WSDL: `docs/arca/wsdl/ws_sr_padron_a100-homologacion.wsdl` (bajado el 2026-10-01). Esquema inline: **no hay `xsd:import`, `xsd:include` ni `wsdl:import`**, así que no hubo XSD para bajar.
- WSDL de producción pedido el 2026-10-02 a `https://aws.afip.gov.ar/sr-parametros/webservices/parameterServiceA100?WSDL`: es idéntico al de homologación salvo el host.
- Llamadas reales sin credenciales contra homologación: 2026-10-02, alrededor de las 18:08 GMT.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | **Padrón** (Apache CXF, JAX-WS, document/literal wrapped). Ver `catalogo.md` 4.1. Corre en otra aplicación (`sr-parametros`, no `sr-padron`) | WSDL; faults observados |
| Endpoint homologación | `https://awshomo.afip.gov.ar/sr-parametros/webservices/parameterServiceA100` | `soap:address`; manual 2.3, PDF p.5 |
| Endpoint producción | `https://aws.afip.gov.ar/sr-parametros/webservices/parameterServiceA100` | `soap:address` de producción; manual PDF p.5 |
| Hosts alternativos | `aws.arca.gob.ar` y `awshomo.arca.gob.ar` responden el WSDL con HTTP 200 (2026-10-02) | Prueba en vivo |
| WSDL | El manual lo escribe `?wsdl` en minúscula, y funciona (HTTP 200 `text/xml`, 2026-10-02) | Manual; prueba |
| Servicio / puerto | `ParameterServiceA100`, puerto `ParameterServiceA100Port`, binding `ParameterServiceA100SoapBinding` | WSDL |
| Namespace | `http://a100.soap.ws.server.pucParam.sr/` (ojo: `pucParam`, no `puc`) | WSDL |
| `elementFormDefault` | `unqualified` (el esquema además lleva `version="1.0"` y no declara `attributeFormDefault`) | WSDL |
| WSAA service id | `ws_sr_padron_a100` | Manual 2.4, PDF p.5 |
| SOAPAction | `soapAction=""` | WSDL |
| SOAP | Solo SOAP 1.1 | WSDL |
| Faults declarados | **Ninguno**. A diferencia de A4, A5, A10 y A13, el WSDL de A100 no declara `SRValidationException`, y eso cambia el Fault real (ver Autenticación) | WSDL; prueba en vivo |
| Content-Type de respuesta | `text/xml;charset=UTF-8`, sin `<?xml?>`, sin header SOAP | Prueba en vivo |

Operaciones del `portType` `ParameterServiceA100`: **2** (`dummy`, `getParameterCollectionByName`).

## Autenticación

- `token`, `sign` y `cuitRepresentada` van sueltos dentro del wrapper, sin `<Auth>`. Salen del TA de WSAA para el service id `ws_sr_padron_a100` (manual 2.2 y 2.4, PDF p.5).
- `cuitRepresentada`: "Esta CUIT debe ser la que solicitó el token enviado, o estar presente en el atributo 'relations' del mismo" (manual 3.2.1, PDF p.8). Es más permisivo en la redacción que A4, A10 y A13, que piden que esté en `relations`.
- `dummy` no requiere token.
- Fallas de autenticación: **HTTP 500** con `soap:Fault`, `faultcode` `soap:Server`.

Respuestas observadas en homologación el 2026-10-02 (cuerpo crudo completo):

| Caso enviado | HTTP | Cuerpo |
|---|---|---|
| Sin elementos `token` ni `sign` | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Falta token y/o sign.</faultstring></soap:Fault></soap:Body></soap:Envelope>` (**sin `detail`**: en los servicios de persona viene `<detail><ns1:SRValidationException/>`) |
| `token`=`abc`, o `token` y `sign` vacíos | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Token malformado</faultstring></soap:Fault></soap:Body></soap:Envelope>` |
| `token` = Base64 de un XML `sso` 2.0 bien formado; `sign` falso | 500 | `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token></faultstring></soap:Fault></soap:Body></soap:Envelope>` |

Headers de las respuestas 500: `Content-Type: text/xml;charset=UTF-8`, `Connection: close`, más headers de seguridad y cookies del balanceador.

## Operaciones

Orden de elementos: el WSDL declara `parameter` como `attributeList*`, `description`, `id`, y `parameterAttribute` como `name`, `value`. Las dos secuencias son alfabéticas, y el ejemplo del manual sale en ese orden.

### dummy

Propósito: verifica la disponibilidad de la aplicación, la autenticación y la base de datos (manual 3.1, PDF p.6). Sin credenciales.

Request `<a100:dummy/>`. Response: `dummyResponse` → `return` → `appserver`, `authserver`, `dbserver` (xs:string 0..1, `OK`/`ERROR`).

Ejemplo del manual (PDF p.7), request:

```xml
 <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
 xmlns:a100="http://a100.soap.ws.server.pucParam.sr/">
   <soapenv:Header/>
   <soapenv:Body>
      <a100:dummy/>
   </soapenv:Body>
 </soapenv:Envelope>
```

Respuesta real observada el 2026-10-02 (HTTP 200, 292 bytes):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><ns2:dummyResponse xmlns:ns2="http://a100.soap.ws.server.pucParam.sr/"><return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return></ns2:dummyResponse></soap:Body></soap:Envelope>
```

### getParameterCollectionByName

Propósito: "Devuelve todos los registros de la tabla de parámetros solicitada" (manual 3.2, PDF p.8). No tiene filtros ni paginación: siempre devuelve la tabla completa.

Request (`getParameterCollectionByName`; sin `minOccurs`, todos 1..1):

| Elemento | Tipo WSDL | Occurs | Significado y límites |
|---|---|---|---|
| `token` | xs:string | 1..1 | Token del TA |
| `sign` | xs:string | 1..1 | Firma del TA |
| `cuitRepresentada` | xs:long | 1..1 | "CUIT que solicita la información de la AFIP" (PDF p.14) |
| `collectionName` | xs:string | 1..1 | Nombre de la tabla; valores válidos en el anexo 5.1 (ver Tablas). Distingue el prefijo (`SUPA.`, `PUC_PARAM.`); si distingue mayúsculas está NO VERIFICADO |

Response: `getParameterCollectionByNameResponse` → `parameterCollectionReturn` (0..1):

| Elemento | Tipo WSDL | Occurs | Significado |
|---|---|---|---|
| `metadata` | tns:metadata | 0..1 | `fechaHora` (xs:dateTime) y `servidor` (xs:string). El manual los marca 1..1 (PDF p.14) |
| `parameterCollection` | tns:parameterCollection | 0..1 | La tabla |

`parameterCollection`:

| Elemento | Tipo WSDL | Occurs | Significado |
|---|---|---|---|
| `name` | xs:string | 0..1 | Nombre de la colección (el mismo `collectionName` pedido) |
| `parameterList` | tns:parameter, nillable | 0..* | Un registro por clave posible |

`parameter` (cada `parameterList`):

| Elemento | Tipo WSDL | Occurs | Significado |
|---|---|---|---|
| `attributeList` | tns:parameterAttribute, nillable | 0..* | Atributos del registro, "1 o más ocurrencias" (PDF p.9). El manual marca 1..1 en la tabla de tipos (PDF p.14), pero el WSDL y el ejemplo lo repiten |
| `description` | xs:string | 0..1 | "Descripción del parámetro correspondiente a la clave id" |
| `id` | xs:string | 0..1 | "Valor de la clave id". Para cada colección, el anexo dice a qué atributo es igual |

`parameterAttribute`: `name` (xs:string 0..1, nombre del atributo, ej. `COD_PROVINCIA`) y `value` (xs:string 0..1). Todos los valores viajan como string, aunque sean numéricos.

Ejemplo del manual, request (PDF p.10; token y sign unidos en una línea, el PDF los corta):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:a100="http://a100.soap.ws.server.pucParam.sr/">
  <soapenv:Header/>
  <soapenv:Body>
     <a100:getParameterCollectionByName>
<token>PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiIHN0YW5kYWxvbmU9InllcyI/Pgo8c3NvIHZlcnNpb249IjIuMCI+CiAgICA8aWQgdW5pcXVlX2lkPSI0NjUzNXRpb24+Cjwvc3NvPgoK</token>
<sign>hUolb9l7fHp66pEEFsoPDixIMEbWOF1oFnZHXNAy5Kh1w85xrwBIdftpCRSG6RO4JpvC/F4w2pS11OfJXu36Ft3pfcIK+GE83zkyiTM=</sign>
       <cuitRepresentada>20111111112</cuitRepresentada>
       <collectionName>SUPA.TIPO_TELEFONO</collectionName>
     </a100:getParameterCollectionByName>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta del ejemplo (PDF p.11-12). Se copia tal cual, con sus errores: pide `SUPA.TIPO_TELEFONO` y responde `SUPA.E_PROVINCIA`; escribe `<decription>` en lugar de `<description>`, que es lo que dice el WSDL; y en el segundo registro le falta el `<` de `value>RIO NEGRO</value>` y el cierre `</attributeList>`:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <ns2:getParameterCollectionByNameResponse
xmlns:ns2="http://a100.soap.ws.server.pucParam.sr/">
       <parameterCollectionReturn>
          <metadata>
            <fechaHora>2017-05-05T10:54:45.034-03:00</fechaHora>
            <servidor>awshomo.afip.gov.ar</servidor>
          </metadata>
          <parameterCollection>
            <name>SUPA.E_PROVINCIA</name>
            <parameterList>
               <attributeList>
                 <name>NOMBRE_PROVINCIA</name>
                 <value>MISIONES</value>
               </attributeList>
               <attributeList>
                 <name>COD_PROVINCIA</name>
                 <value>19</value>
               </attributeList>
               <attributeList>
                 <name>CODIGO_SIM_PROVINCIA</name>
                 <value>MI</value>
               </attributeList>
               <decription>MISIONES</decription>
               <id>19</id>
            </parameterList>
            <parameterList>
               <attributeList>
                 <name>NOMBRE_PROVINCIA</name>
                   value>RIO NEGRO</value>
               <attributeList>
                 <name>COD_PROVINCIA</name>
                 <value>22</value>
               </attributeList>
               <attributeList>
                 <name>CODIGO_SIM_PROVINCIA</name>
                 <value>RN</value>
               </attributeList>
               <decription>RIO NEGRO</decription>
               <id>22</id>
            </parameterList>
            <parameterList>.... </parameterList>
            ... Similar con todas las provincias
            <parameterList>.... </parameterList>
          </parameterCollection>
       </parameterCollectionReturn>
     </ns2:getParameterCollectionByNameResponse>
  </soap:Body>
</soap:Envelope>
```

Lo que se desprende del ejemplo:

- El orden de los `attributeList` dentro de un registro **no es alfabético** (`NOMBRE_PROVINCIA`, `COD_PROVINCIA`, `CODIGO_SIM_PROVINCIA`). Parece el orden de columnas de la tabla. El orden de los `parameterList` (19 antes que 22) tampoco permite deducir una regla. Ambos son **NO VERIFICADOS**.
- `description` repite un atributo (para provincias, `NOMBRE_PROVINCIA`).
- El ejemplo trae `metadata` antes que `parameterCollection`, en orden alfabético, igual que el WSDL.

## Validaciones y errores

El manual de A100 **no trae tabla de errores**.

| Código / texto | Condición | Dónde aparece | Fuente |
|---|---|---|---|
| `Falta token y/o sign.` | Falta el elemento `token` o `sign` | `soap:Fault` `soap:Server`, **sin `detail`**; HTTP 500 | En vivo 2026-10-02 |
| `Token malformado` | Token vacío o que no decodifica | `soap:Fault` `soap:Server` sin `detail`; HTTP 500 | En vivo 2026-10-02 |
| `No se pudo verificar que &lt;sign> contenga una firma valida de &lt;token>` | Firma inválida | `soap:Fault` `soap:Server` sin `detail`; HTTP 500 | En vivo 2026-10-02 |
| Errores de capa SOAP (`Unmarshalling Error`, operación no reconocida, wrapper inesperado, `VersionMismatch`) | XML o sobre inválidos | `soap:Fault` `soap:Client`; HTTP 500. Observados en A4, mismo stack CXF; textos en `ws_sr_padron_a4.md` | Inferencia para A100 |
| — | `collectionName` inexistente | **NO VERIFICADO**: puede ser Fault, colección vacía o `parameterCollection` ausente | — |
| — | `cuitRepresentada` que no es la del token ni está en `relations` | **NO VERIFICADO** | — |

## Tablas y datos

### 5.1 Valores de `collectionName` (manual v2.1, PDF p.15-17)

El anexo explica: "se debe invocar al WS de nombre ws_sr_padron_a100, con los valores definidos en la columna "CollectionName" [...] En respuesta [...] se obtiene en cada caso, una coleccion de elementos cuyo atributo "id" los identifica unívocamente dentro de la colección" (PDF p.15).

Copiado completo. **El PDF v2.1 está roto en los cortes de página**: en la PDF p.15 la fila "Dependencia AFIP" queda sin `CollectionName`, y entre las PDF p.16 y p.17 faltan filas, porque la p.17 empieza a mitad de una fila de "Monotributo según F.184 TO". Se probó con `pdftotext` y con `pypdf` y en el PDF no hay más texto.

Las celdas angostas del PDF parten los nombres en dos líneas (`SUPA.E_ORGANISMO_INFORMANT` / `E`, `SUPA.A_PROVINCIA_SOCIEDAD_O` / `RG`, `SUPA.TIPO_EMPRESA_JURI_SUBG` / `RUPO`). En la tabla van unidos. La unión de `SUPA.A_PROVINCIA_SOCIEDAD_ORG` es la menos segura, porque el `RG` cae en la línea de la descripción.

| Descripción parámetro | CollectionName | Comentario (id =) |
|---|---|---|
| Organismos Informantes | `SUPA.E_ORGANISMO_INFORMANTE` | atributo `CUIT_ORGANISMO` |
| Organismo de control | `SUPA.E_ORGANISMO_CONTROL` | atributo `CUIT_ORGANISMO` |
| Relación entre Provincia, Organismo de Control y forma jurídica | `SUPA.A_PROVINCIA_SOCIEDAD_ORG` | atributos `COD_PROVINCIA`, `COD_TIPO_EMPRESA_JURIDICA`, `COD_ORGANISMO` |
| Tipos de forma jurídicas | `SUPA.TIPO_EMPRESA_JURIDICA` | atributo `COD_TIPO_EMPRESA_JURIDICA` |
| Identificación particular de la Forma Jurídica | `SUPA.TIPO_EMPRESA_JURI_SUBGRUPO` | atributo `COD_SUBGRUPO_TIPO_EMPJURI` |
| Provincia | `SUPA.E_PROVINCIA` | atributo `COD_PROVINCIA` |
| Tipo de dato adicional del domicilio | `SUPA.TIPO_DATO_ADICIONAL_DOMICILIO` | atributo `COD_TIPO_DATO_ADICIONAL_DOM` |
| Dependencia AFIP | (cortado en el PDF) | (cortado en el PDF) |
| Tipo de clave (CUIT / CUIL / CDI) | `SUPA.TIPO_CLAVE_IDENTIFICACION` | atributo `COD_CLAVE_IDENTIFICACION` |
| Tipo de documento identificatorio | `SUPA.TIPO_DOCUMENTO` | atributo `COD_TIPO_DOCUMENTO` |
| Clasificación tipo de sexo (M / F / X) | `E_SEXO` (así, sin prefijo `SUPA.`) | atributo `COD_TIPO_SEXO` |
| Clasificación tipo de residencia | `SUPA.TIPO_RESIDENCIA` | atributo `COD_TIPO_RESIDENCIA` |
| Tipo de componente de sociedad | `SUPA.TIPO_COMPONENTE_SOCIEDAD` | atributo `COD_TIPO_COMPONENTE_SOCIEDAD` |
| Particularidades de los componentes de acuerdo al tipo societario | `SUPA.TIPO_RELACION_COMPONENTE_SOC` | atributo `COD_TIPO_COMPONENTE_SOCIEDAD` |
| Refleja las distintas relaciones que pueden darse entre las claves | `SUPA.TIPO_RELACION` | atributo `COD_RELACION` |
| Subtipos de relación que pueden darse entre las claves; se relaciona con el código relación de la tabla Tipo_Relación | `SUPA.TIPO_SUBTIPO_RELACION` | atributo `CODIGO_SUBTIPO_RELACION` |
| Tipo de email | `SUPA.TIPO_EMAIL` | atributo `COD_TIPO_EMAIL` |
| Tipo de domicilio | `SUPA.TIPO_DOMICILIO` | atributo `COD_TIPO_DOMICILIO` |
| Calle (Sólo CABA) | `PUC_PARAM.T_CALLE` | atributo `COD_CALLE` |
| Localidad | `PUC_PARAM.T_LOCALIDAD` | (sin comentario en v2.1; la v1.1 dice "d es igual al atributo `COD_LOCALIDAD`") |
| (filas perdidas en el corte de página) | — | — |
| ... Monotributo según F.184 TO | (cortado en el PDF) | atributo `COD_CATEGORIA_MONOTRIBUTO` |
| Tipología de Autónomos de Monotributo según TO F.184 | `SUPA.E_AUTONOMO_MONOTRIBUTO` | atributo `COD_AUTONOMO_MONOTRIBUTO` |
| Nómina de Regímenes vigentes y no vigentes | `SUPA.E_REGIMEN` | atributos `COD_IMPUESTO`, `COD_REGIMEN` |
| clasificaciones de regímenes | `SUPA.TIPO_REGIMEN` | atributo `COD_TIPO_REGIMEN` |
| Atributos y características de los contribuyentes | `SUPA.E_CARACTERIZACION` | atributo `COD_CARACTERIZACION` |
| Tipos de Documento válidos para el sistema | `SUPA.TIPO_ARCHIVO` | atributo `COD_TIPO_ARCHIVO` |
| Relaciones entre actividades y rubros de todos los nomencladores | `SUPA.A_ACTIVIDAD_RUBRO` | atributo `COD_ACTIVIDAD:COD_RUBRO:COD_NOMENCLADOR` (id compuesto con `:`) |
| Rubros de los nomencladores de actividades económica | `SUPA.E_RUBRO_ACTIVIDAD` | atributo `COD_RUBRO:COD_NOMENCLADOR` |
| Actividades ocupacionales no incluidas en nomenclador F.150. Esos códigos se utilizan en el momento de la inscripción del contribuyente | `SUPA.E_ACTIVIDAD_OCUPACIONAL` | atributo `COD_ACTIVIDAD_OCUPACIONAL` |

Colecciones que figuran en la v1.1 (PDF p.15 de ese manual) y **no aparecen** en lo legible de la v2.1. Pueden estar entre las filas perdidas o haber sido dadas de baja (NO VERIFICADO):

| Descripción | CollectionName | Comentario |
|---|---|---|
| Tipo de línea telefónica | `PUC_PARAM.T_TIPO_LINEA_TELEFONICA` | (sin comentario) |
| Tipo de telefono | `SUPA.TIPO_TELEFONO` (es el que usa el ejemplo de request) | id = `COD_TIPO_TELEFONO` |
| Actividad | `SUPA.E_ACTIVIDAD` | id = `COD_ACTIVIDAD` |

El nombre de las colecciones de impuestos, de dependencias y de categorías de monotributo **no se puede leer en ningún manual**. No se completa por convención.

### Atributos conocidos por colección

Solo los que aparecen en algún manual:

- `SUPA.E_PROVINCIA`: `NOMBRE_PROVINCIA`, `COD_PROVINCIA`, `CODIGO_SIM_PROVINCIA` (ejemplo, PDF p.11-12). Registros visibles: 19 MISIONES (`MI`), 22 RIO NEGRO (`RN`).
- `SUPA.TIPO_EMPRESA_JURIDICA`: `COD_TIPO_EMPRESA_JURIDICA` (id) y `DESC_TIPO_EMPRESA_JURIDICA`. Este último es el texto que A13 devuelve en `formaJuridica` (manual A13, PDF p.24).
- `SUPA.TIPO_DATO_ADICIONAL_DOMICILIO`: `COD_TIPO_DATO_ADICIONAL_DOM` (id) y `DESC_TIPO_DATO_ADICIONAL_DOM`, que es el texto que A13 devuelve en `tipoDatoAdicional` (manual A13, PDF p.24).
- El resto: solo el atributo id del anexo.

### Valores de provincias vistos en los manuales del padrón

No es la tabla completa, que **no está en ningún manual (NO VERIFICADO)**:

| COD_PROVINCIA | Nombre | Fuente |
|---|---|---|
| 0 | CIUDAD AUTONOMA BUENOS AIRES | ejemplos de A4 y A10 |
| 1 | BUENOS AIRES | ejemplos de A5 |
| 6 | JUJUY | ejemplo de A13 getPersonaV2 |
| 11 | SAN LUIS | ejemplos de A5 y A13 |
| 19 | MISIONES (`MI`) | ejemplo de A100 |
| 20 | NEUQUEN | ejemplos de A4 y A10 |
| 22 | RIO NEGRO (`RN`) | ejemplo de A100 |

Las tablas de valores de los otros manuales del padrón (TipoDocumento, TipoResidencia, TipoEstado, TipoRegimen, TipoTelefono, TipoRelacion, SubtipoRelación, TipoEMail, TipoDatoAdicional) están copiadas en `ws_sr_padron_a4.md`. Son los textos descriptivos de las colecciones SUPA equivalentes, pero sin sus códigos numéricos, salvo TipoRegimen 1-9.

## Comportamiento a simular

- Sin estado: cada `collectionName` devuelve siempre la misma lista completa.
- Fuente única de verdad: los valores que devuelvan A4, A5, A10 y A13 en `descripcionProvincia`, `formaJuridica`, `tipoDatoAdicional`, `tipoDocumento`, etc. tienen que existir en la colección A100 correspondiente.
- Serialización: `name` antes que `parameterList`; dentro de cada registro, `attributeList` repetido, después `description` y por último `id`. Todos los valores como string.
- Fault de autenticación **sin `detail`**: no copiar el `SRValidationException` de los servicios de persona.
- `collectionName` desconocido: comportamiento NO VERIFICADO; elegir uno y documentarlo como decisión del simulador.

## No verificado

- El contenido real de cada colección (códigos, descripciones y atributos), en particular la tabla completa de provincias.
- Los nombres de colección que el PDF v2.1 pierde en los cortes de página (dependencias, categorías de monotributo y lo que haya en el medio).
- Si `SUPA.TIPO_TELEFONO`, `PUC_PARAM.T_TIPO_LINEA_TELEFONICA` y `SUPA.E_ACTIVIDAD` siguen vigentes.
- Si `E_SEXO` va de verdad sin prefijo `SUPA.`.
- La respuesta a un `collectionName` inexistente o mal escrito, y si distingue mayúsculas.
- El orden de los `parameterList` y de los `attributeList`.
- Los textos de falla de TA vencido, TA de otro service id y `cuitRepresentada` no autorizada.
