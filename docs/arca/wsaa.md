# WSAA: referencia para ArcaSim

Referencia del **Web Service de Autenticación y Autorización (WSAA)** de ARCA (ex AFIP), escrita para construir un simulador que se comporte igual que el servicio real. Relevada el 2026-10-01.

## 0. Fuentes y cómo leer este documento

Cada dato lleva una etiqueta de procedencia:

| Etiqueta | Significado |
|---|---|
| **[SPEC]** | *Especificación Técnica del WebService de Autenticación y Autorización* v1.2.2 (PDF del 2025-07-15). https://www.afip.gob.ar/ws/WSAA/Especificacion_Tecnica_WSAA_1.2.2.pdf |
| **[MANUAL]** | *WSAA Manual del Desarrollador*, publicación 20.2.19 (PDF del 2025-07-15). https://www.afip.gob.ar/ws/WSAA/WSAAmanualDev.pdf |
| **[WSDL]** | WSDL oficiales: `docs/arca/wsdl/wsaa-homologacion.wsdl` y `wsaa-produccion.wsdl`. El de homologación es **idéntico byte a byte** al servido en vivo por https://wsaahomo.afip.gov.ar/ws/services/LoginCms?wsdl (comparado el 2026-10-01). |
| **[WEB-WSAA]** | https://www.afip.gob.ar/ws/documentacion/wsaa.asp |
| **[WEB-ARQ]** | https://www.afip.gob.ar/ws/documentacion/arquitectura-general.asp |
| **[WEB-CERT]** | https://www.afip.gob.ar/ws/documentacion/certificados.asp |
| **[WSASS]** | *Manual del Usuario del WSASS*, publicación 19.10.1. https://www.afip.gob.ar/ws/WSASS/WSASS_manual.pdf |
| **[CERT-PROD]** | *Generación de Certificados para Producción*. https://www.afip.gob.ar/ws/WSAA/WSAA.ObtenerCertificado.pdf y https://www.afip.gob.ar/ws/WSAA/wsaa_obtener_certificado_produccion.pdf |
| **[EJ-OFICIAL]** | Clientes de ejemplo oficiales: https://www.afip.gob.ar/ws/WSAA/ejemplos/dev-wsaa-cliente-dotnet-cs.zip y https://www.afip.gob.ar/ws/WSAA/ejemplos/wsaa_client_java.tgz |
| **[WSFE-MANUAL]** | *Manual para el desarrollador, Facturación RG 4291, Proyecto FE v4.7* (revisión del 1 de septiembre de 2026). https://www.afip.gob.ar/ws/documentacion/manuales/manual-desarrollador-ARCA-COMPG.pdf |
| **[OBSERVADO]** | Pedidos hechos por mí el **2026-10-01, cerca de las 22:40 ART**, contra **homologación** (`wsaahomo.afip.gov.ar` y `wswhomo.afip.gov.ar`), usando CMS inválidos o firmados con certificados autofirmados. Son respuestas reales del servidor. No toqué producción salvo un GET del WSDL. |
| **[CAPTURA-OSS]** | Respuestas reales de WSAA guardadas en repos públicos de GitHub. No las publica ARCA: sirven para confirmar formato, no como norma. |
| **[OSS]** | Foros y clientes open source (pyafipws, AfipSDK). Se usan solo donde la documentación oficial no alcanza, y siempre se aclara. |

**Pérdida de guiones en los PDF.** Al extraer texto de la SPEC desaparecen los guiones: aparece `20011231T12:00:0003:00` en lugar de `2001-12-31T12:00:00-03:00`, y `UTF8` en lugar de `UTF-8`. Los ejemplos de abajo llevan los guiones restituidos. Donde la reconstrucción no es obvia, se indica.

---

## 1. Endpoints y operación SOAP

### 1.1 URLs

| Ambiente | Endpoint | WSDL | Fuente |
|---|---|---|---|
| Homologación (testing) | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms?wsdl` | [WEB-WSAA], [WSDL] |
| Producción | `https://wsaa.afip.gov.ar/ws/services/LoginCms` | `https://wsaa.afip.gov.ar/ws/services/LoginCms?wsdl` | [WEB-WSAA], [WSDL] |

Hostnames alternativos:

- El FAQ 10.1 del [MANUAL] menciona `https://wsaahomo.arca.gov.ar/...` y `https://wsaa.arca.gov.ar/...`. El 2026-10-01 **ninguno de los dos resolvía por DNS** [OBSERVADO].
- `https://wsaa.arca.gob.ar/ws/services/LoginCms?WSDL` y `https://wsaahomo.afip.gob.ar/ws/services/LoginCms?WSDL` sí responden 200 [OBSERVADO].
- El certificado TLS de producción cubre `wsaa.afip.gov.ar`, `wsaa.afip.gob.ar` y `wsaa.arca.gob.ar`. El de homologación cubre `*.afip.gob.ar` y `*.afip.gov.ar`. Ambos los emite Sectigo y negocian TLS 1.2 [OBSERVADO].

Un GET al endpoint sin `?wsdl` devuelve la página por defecto de Axis: `<h1>LoginCms</h1><p>Hi there, this is an AXIS service!</p>...` [OBSERVADO].

### 1.2 Contrato del WSDL [WSDL]

Es Apache Axis 1.4 (`<!--WSDL created by Apache Axis version: 1.4 Built on Apr 22, 2006 (06:55:48 PDT)-->`), document/literal.

| Elemento | Valor |
|---|---|
| `targetNamespace` del WSDL / namespace `impl` | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` (homo) · `https://wsaa.afip.gov.ar/ws/services/LoginCms` (prod) |
| Namespace de los elementos del mensaje (`tns1`) | `http://wsaa.view.sua.dvadac.desein.afip.gov` (**igual en ambos ambientes**) |
| Servicio / puerto | `LoginCMSService` / `LoginCms` |
| portType / binding | `LoginCMS` / `LoginCmsSoapBinding` |
| Operación | `loginCms` (única), `soapAction=""` |
| Request | `tns1:loginCms` → secuencia con `in0` (`xsd:string`): el CMS en Base64 |
| Response | `tns1:loginCmsResponse` → secuencia con `loginCmsReturn` (`xsd:string`): el XML del TA como **string** |
| Fault | mensaje `LoginFault`, parte `impl:fault` de tipo `impl:LoginFault` (secuencia vacía) |

Los WSDL de homologación y producción difieren solo en el namespace `impl` y en la `soap:address`. También cambia el orden en que se declaran los `wsdl:message`, que no tiene efecto.

Como `elementFormDefault="qualified"`, `in0` y `loginCmsReturn` quedan en el namespace `tns1`.

### 1.3 Request de ejemplo (SOAP 1.1)

Tomado del capítulo 9 del [MANUAL]:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsaa="http://wsaa.view.sua.dvadac.desein.afip.gov">
  <soapenv:Header/>
  <soapenv:Body>
    <wsaa:loginCms>
      <wsaa:in0>MIIG2AYJKoZIhvcNAQcCoIIGyTCC...</wsaa:in0>
    </wsaa:loginCms>
  </soapenv:Body>
</soapenv:Envelope>
```

El [MANUAL] lo envía con `Content-Type: text/xml;charset=UTF-8` y `SOAPAction:urn:LoginCms`.

Comportamiento de transporte observado [OBSERVADO]:

- **El header `SOAPAction` es obligatorio en SOAP 1.1.** Sin él, cualquier cuerpo (aun uno válido) devuelve el fault `ns1:Client.NoSOAPAction` / `no SOAPAction header!` (ver 6.3). El valor no se valida: `""` y `urn:LoginCms` funcionan igual.
- **SOAP 1.2 también se acepta.** Con `Content-Type: application/soap+xml` y sobre `http://www.w3.org/2003/05/soap-envelope`, la respuesta llega en SOAP 1.2 con `Content-Type: application/soap+xml;charset=UTF-8`. El ejemplo PHP oficial del [MANUAL] usa `'soap_version' => SOAP_1_2`.
- **El namespace del elemento no se controla.** Un `<loginCms xmlns="urn:wrong"><in0>abc</in0></loginCms>` se procesó y devolvió `cms.bad.base64`, no un error de despacho.
- Si falta `in0`, la respuesta es `soapenv:Server.userException` / `javax.ejb.EJBException: java.lang.NullPointerException`.
- Un `in0` vacío devuelve `cms.bad.base64`.
- Un XML malformado devuelve `soapenv:Server.userException` con el texto de la `SAXParseException`.
- Todos los faults salen con **HTTP 500**.
- El balanceador agrega cookies (`f5avr..._session_`, `TS01b14f84`). Son irrelevantes para el protocolo.

### 1.4 Response exitosa (envoltorio)

`loginCmsReturn` trae el XML del TA **escapado como texto**, no como XML anidado. Esta es una captura real de homologación de 2018 [CAPTURA-OSS: `unagisoftware/afip-invoices`, `spec/support/responses/login_response.xml`]:

```xml
<?xml version="1.0" encoding="UTF-8"?><soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><soapenv:Body><loginCmsResponse xmlns="http://wsaa.view.sua.dvadac.desein.afip.gov"><loginCmsReturn>&lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot; standalone=&quot;yes&quot;?&gt;
&lt;loginTicketResponse version=&quot;1&quot;&gt;
    &lt;header&gt;
        &lt;source&gt;CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239&lt;/source&gt;
        ...
&lt;/loginTicketResponse&gt;
</loginCmsReturn></loginCmsResponse></soapenv:Body></soapenv:Envelope>
```

No pude obtener un TA real porque hace falta un certificado emitido por ARCA, así que **el código HTTP de una respuesta exitosa no lo verifiqué**. Lo esperable en Axis es 200.

---

## 2. TRA (Ticket de Requerimiento de Acceso): `LoginTicketRequest.xml`

### 2.1 Esquema [SPEC]

```xml
<xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <xsd:element name="loginTicketRequest" type="loginTicketRequest"/>
  <xsd:complexType name="loginTicketRequest">
    <xsd:sequence>
      <xsd:element name="header"  type="headerType"  minOccurs="1" maxOccurs="1"/>
      <xsd:element name="service" type="serviceType" minOccurs="1" maxOccurs="1"/>
    </xsd:sequence>
    <xsd:attribute name="version" type="xsd:decimal" use="optional" default="1.0"/>
  </xsd:complexType>
  <xsd:complexType name="headerType">
    <xsd:sequence>
      <xsd:element name="source"         type="xsd:string"      minOccurs="0" maxOccurs="1"/>
      <xsd:element name="destination"    type="xsd:string"      minOccurs="0" maxOccurs="1"/>
      <xsd:element name="uniqueId"       type="xsd:unsignedInt" minOccurs="1" maxOccurs="1"/>
      <xsd:element name="generationTime" type="xsd:dateTime"    minOccurs="1" maxOccurs="1"/>
      <xsd:element name="expirationTime" type="xsd:dateTime"    minOccurs="1" maxOccurs="1"/>
    </xsd:sequence>
  </xsd:complexType>
  <xsd:simpleType name="serviceType">
    <xsd:restriction base="xsd:string">
      <xsd:pattern value="[a-z,A-Z][a-z,A-Z,\-,_,0-9]*"/>   <!-- reconstruido: el PDF muestra "[az,AZ][az,AZ,\,_,09]*" -->
      <xsd:minLength value="3"/>
      <xsd:maxLength value="32"/>
    </xsd:restriction>
  </xsd:simpleType>
</xsd:schema>
```

Notas:

- **Sin namespace.** El TRA no declara namespace, y el XML distingue mayúsculas de minúsculas (FAQ 10.10 del [MANUAL]).
- **Largo de `service`: dos fuentes en conflicto.** El XSD fija 3 a 32 caracteres, pero el FAQ 10.10 del [MANUAL] da como causa de `xml.bad` "Que el ID de servicio exceda **35** caracteres". **No verificado** cuál aplica el servidor.
- **El patrón de `service` está reconstruido** a partir de la extracción del PDF. Las comas dentro de la clase de caracteres son literales del XSD original.

### 2.2 Campos [SPEC]

| Campo | Oblig. | Semántica y reglas |
|---|---|---|
| `@version` | no | Decimal, por defecto `1.0`. Una versión no soportada da `xml.version.notSupported`. Qué valores acepta además de `1.0` está **sin verificar**. |
| `header/source` | no | DN del certificado con el que WSAA verifica la firma. Si falta, se usa el primer certificado firmante del CMS. Si está, tiene que coincidir con uno de los certificados firmantes del CMS; si no, da `xml.source.invalid`. |
| `header/destination` | no | DN del WSAA: `cn=wsaa,o=afip,c=ar,serialNumber=CUIT 33693450239` en producción y `cn=wsaahomo,o=afip,c=ar,serialNumber=CUIT 33693450239` en homologación. Si no coincide, da `xml.destination.invalid`. |
| `header/uniqueId` | sí | Entero de 32 bits sin signo que, junto con `generationTime`, identifica el requerimiento. |
| `header/generationTime` | sí | `xsd:dateTime`. "La tolerancia de aceptación será de hasta 24 horas previas al requerimiento de acceso." |
| `header/expirationTime` | sí | `xsd:dateTime`. "La tolerancia de aceptación será de hasta 24 horas posteriores al requerimiento de acceso." |
| `service` | sí | Id del WSN (`wsfe`, `ws_sr_constancia_inscripcion`, ...). |

El FAQ 10.5 del [MANUAL] recomienda **omitir `source` y `destination`** "para evitar inconvenientes en el futuro si cambian los DN de los certificados".

### 2.3 Ventanas de tiempo

Las reglas siguen la tabla de errores de la [SPEC] (ver 6.1). `now` es la hora del servidor WSAA:

| Condición | Error |
|---|---|
| `generationTime > now` (en el futuro) | `xml.generationTime.invalid` |
| `generationTime < now - 24h` | `xml.generationTime.invalid` |
| `expirationTime < now` | `xml.expirationTime.expired` |
| `expirationTime > now + 24h`. Ver la ambigüedad abajo. | `xml.expirationTime.invalid` |
| Fecha con formato inválido o imposible (31 de febrero, hora 25:00) | `xml.bad` (FAQ 10.10 del [MANUAL]) |

**Ambigüedad.** La tabla de la [SPEC] dice "El tiempo de expiración **del documento** es superior a 24 horas". Puede leerse como `expirationTime - now > 24h` o como `expirationTime - generationTime > 24h`. **No verificado.**

**Tolerancia de reloj.** No hay un valor documentado. La [SPEC] y el [MANUAL] solo piden sincronizar por NTP (sugieren `time.afip.gov.ar`) y tener la zona horaria en GMT-3. Por eso los clientes oficiales dejan margen hacia atrás:

| Cliente oficial | `generationTime` | `expirationTime` | `uniqueId` |
|---|---|---|---|
| C# y PowerShell [EJ-OFICIAL], [MANUAL] | `now - 10 min` | `now + 10 min` | contador o `yyMMddHHMM` |
| PHP [MANUAL] | `now - 60 s` | `now + 60 s` | `date('U')` |
| Java [EJ-OFICIAL] | `now` | `now + 1 h` (`TicketTime=3600000`) | epoch en segundos |

El FAQ 10.9 del [MANUAL] sugiere directamente "tomen el valor de fecha del sistema y le resten algunos minutos".

**Formatos de fecha que aparecen en la documentación oficial:**

- `2019-09-26T10:09:20`, sin zona. Lo generan los clientes C# y PowerShell con `ToString("s")`, en hora local. **No verificado** cómo interpreta el servidor un `dateTime` sin offset; lo probable es la hora local del servidor, GMT-3.
- `2018-03-21T12:57:42` (FAQ 10.9) y `2018-01-29T13:52:57.467-03:00` (FAQ 10.10), con milisegundos y offset.
- `date('c')` de PHP: `2019-11-04T21:40:11+00:00`.

### 2.4 Ejemplos

Ejemplo de la [SPEC], con los guiones restituidos:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<loginTicketRequest version="1.0">
  <header>
    <source>cn=srv1,ou=facturacion,o=empresa s.a.,c=ar,serialNumber=CUIT 30123456789</source>
    <destination>cn=wsaa,o=afip,c=ar,serialNumber=CUIT 33693450239</destination>
    <uniqueId>4325399</uniqueId>
    <generationTime>2001-12-31T12:00:00-03:00</generationTime>
    <expirationTime>2001-12-31T12:10:00-03:00</expirationTime>
  </header>
  <service>wsfe</service>
</loginTicketRequest>
```

TRA mínimo que el capítulo 5 del [MANUAL] firma y envía con éxito. No tiene declaración XML, ni `version`, ni zona horaria:

```xml
<loginTicketRequest>
  <header>
    <uniqueId>190926</uniqueId>
    <generationTime>2019-09-26T10:09:20</generationTime>
    <expirationTime>2019-09-26T10:29:20</expirationTime>
  </header>
  <service>ws_sr_constancia_inscripcion</service>
</loginTicketRequest>
```

---

## 3. Firma del TRA (CMS / PKCS#7)

### 3.1 Requisitos

| Aspecto | Requisito | Fuente |
|---|---|---|
| Tipo | CMS `SignedData` que **contiene** el TRA. El contenido va adjunto (encapsulado, `eContentType = id-data`). | [SPEC]; `-nodetach` en el [MANUAL]; `gen.generate(data, true, ...)` en Java y `new SignedCms(contentInfo)` en C# [EJ-OFICIAL] |
| Certificado | El certificado firmante tiene que viajar dentro del CMS. El ejemplo C# usa `X509IncludeOption.EndCertOnly`. | [SPEC] (paso 2: "un CMS que contenga el TRA, su firma electrónica y el certificado X.509"), [EJ-OFICIAL] |
| Algoritmo | La [SPEC] dice "firma electrónica utilizando **SHA1+RSA**". **SHA-256 también se acepta**: el CMS de ejemplo del [MANUAL], usado con éxito, declara digest SHA-256 (OID `2.16.840.1.101.3.4.2.1`; lo decodifiqué del Base64 publicado en el capítulo 5). El ejemplo Java oficial usa `DIGEST_SHA1`. | [SPEC], [MANUAL], [EJ-OFICIAL] |
| Codificación | El CMS en DER se codifica en Base64 y va en `in0`. El [MANUAL] pega el cuerpo de un PEM `-----BEGIN CMS-----` (Base64 con saltos de línea, sin las líneas BEGIN/END). | [SPEC], [MANUAL] |
| Atributos firmados | `openssl cms -sign` agrega `smimeCapabilities` y el CMS se acepta igual. No hay requisitos documentados sobre atributos firmados. | [MANUAL] |

Otros algoritmos (SHA-384, SHA-512, RSA-PSS, ECDSA) **no están verificados**. Ante un algoritmo no soportado, la [SPEC] asigna `cms.sign.invalid` ("Firma inválida o algoritmo no soportado").

Firma con OpenSSL, del capítulo 5 del [MANUAL]:

```
openssl cms -sign -in MiLoginTicketRequest.xml -out MiLoginTicketRequest.xml.cms \
  -signer MiCertificado2019.pem -inkey MiPrivada2019.key -nodetach -outform PEM
```

### 3.2 Requisitos del certificado del cliente (CEE)

Según la [SPEC]:

1. X.509v3 emitido por una autoridad certificante reconocida por ARCA.
2. DN conforme a RFC 2253 y al perfil mínimo de la ONTI.
3. Campos obligatorios:
   - `commonName`: nombre del servicio o aplicación.
   - `serialNumber` (OID 2.5.4.5): `"CUIT <11 dígitos>"`, por ejemplo `CUIT 20123456780`.
   - `organizationName`.
   - `countryName` (ISO 3166).

CSR documentado en el [WSASS] y en [CERT-PROD]:

```
openssl genrsa -out MiClavePrivada.key 2048
openssl req -new -key MiClavePrivada.key \
  -subj "/C=AR/O=Empresa/CN=Sistema/serialNumber=CUIT nnnnnnnnnnn" -out MiPedidoCSR.csr
```

- La clave es RSA de **2048 bits**, según ambas guías. Que el servidor rechace otros tamaños **no está verificado**.
- Después del texto `CUIT` va un espacio y los 11 dígitos sin guiones.

### 3.3 Autoridades certificantes (firma del CMS, no TLS)

Inspeccioné las cadenas oficiales publicadas en [WEB-CERT]:

| Ambiente | AC emisora de los certificados de cliente | Raíz | Archivo |
|---|---|---|---|
| Homologación | `CN=Computadores Test, O=AFIP, C=AR` (2022-07-07 a 2030-07-07, RSA 2048, sha512WithRSA) | `CN=AC Raiz Test, O=AFIP, C=AR` (2014 a 2034) | `/ws/WSASS/Cadena_de_certificacion_homo_2022_2034.zip` |
| Producción | `CN=Computadores, O=AFIP, C=AR` (2024-01-03 a 2035-01-03) | `CN=AFIP Root CA 2, O=AFIP, C=AR` (2015 a 2035) | `/ws/documentacion/certificados/Cadena_de_certificacion_prod_2024_2035.zip` |

- **Un certificado de un ambiente falla en el otro.** Con un certificado de homologación en producción, o al revés, el FAQ 10.2 del [MANUAL] da `cms.cert.untrusted` ("Certificado no emitido por AC de confianza"). El FAQ 10.3 atribuye el mismo caso a "Computador no autorizado a acceder a los servicios de AFIP" (`coe.notAuthorized`). **Las dos fuentes se contradicen y no está verificado** cuál ocurre.
- **Así se ve un certificado emitido por WSASS** (ejemplo del [WSASS], decodificado): emisor `CN=Computadores, O=AFIP, C=AR` (la AC de homologación de entonces), sujeto `CN=Cert_glarriera_1, SERIALNUMBER=CUIT 20190178154`, firma sha512WithRSA, **vigencia de 2 años** (2016-09-30 a 2018-09-30).
- **El DN no se puede cambiar.** El [WSASS] dice: "El DN del certificado creado tiene el formato `SERIALNUMBER=CUIT nnnnnnnnnnn, CN=xxxxx` [...] donde xxxxx es el alias". Del CSR solo se aprovecha la clave pública.

### 3.4 Asociación certificado → servicio (autorización)

| Ambiente | Herramienta | Qué hace | Fuente |
|---|---|---|---|
| Homologación | **WSASS** (Autoservicio de Acceso a APIs de Homologación), al que se adhiere con clave fiscal de **persona física** desde el Administrador de Relaciones | Crea el DN y su certificado ("Nuevo Certificado", alias + CSR). Luego, en "Crear autorización a servicio", vincula **alias + CUIT representada + servicio**. Una autorización se puede eliminar, pero un certificado no se borra nunca. | [WEB-WSAA], [WEB-CERT], [WSASS] |
| Producción | "Administración de Certificados Digitales" (alias + CSR → CRT) y "Administrador de Relaciones de Clave Fiscal" ("Nueva Relación" → WSN → representante, o "delegación") | Equivalente. | [WEB-WSAA], [CERT-PROD], https://www.afip.gob.ar/ws/WSAA/ADMINREL.DelegarWS.pdf |

Modelo de datos que se desprende del [WSASS]: **DN/alias** (con su CUIT) → N certificados; **autorización** = (alias, CUIT representada, servicio, CUIT de quien autoriza).

Las CUIT representadas aparecen dentro del token como `<relation key="CUIT" reltype="4"/>` (ver 4.3). El [MANUAL] señala que las relaciones quedan **congeladas en el token** cuando se emite. AfipSDK lo confirma en la práctica: una delegación creada después exige pedir un TA nuevo [OSS: https://afipsdk.com/blog/solucion-a-no-aparecio-cuit-en-lista-de-relaciones/].

---

## 4. TA (Ticket de Acceso): `LoginTicketResponse.xml`

### 4.1 Esquema [SPEC]

```xml
<xsd:element name="loginTicketResponse" type="loginTicketResponse"/>
<xsd:complexType name="loginTicketResponse">
  <xsd:sequence>
    <xsd:element name="header"      type="headerType"      minOccurs="1" maxOccurs="1"/>
    <xsd:element name="credentials" type="credentialsType" minOccurs="1" maxOccurs="1"/>
  </xsd:sequence>
  <xsd:attribute name="version" type="xsd:decimal" use="optional" default="1.0"/>
</xsd:complexType>
<xsd:complexType name="headerType">
  <xsd:sequence>
    <xsd:element name="source"         type="xsd:string"      minOccurs="1" maxOccurs="1"/>
    <xsd:element name="destination"    type="xsd:string"      minOccurs="1" maxOccurs="1"/>
    <xsd:element name="uniqueId"       type="xsd:unsignedInt" minOccurs="1" maxOccurs="1"/>
    <xsd:element name="generationTime" type="xsd:dateTime"    minOccurs="1" maxOccurs="1"/>
    <xsd:element name="expirationTime" type="xsd:dateTime"    minOccurs="1" maxOccurs="1"/>
  </xsd:sequence>
</xsd:complexType>
<xsd:complexType name="credentialsType">
  <xsd:sequence>
    <xsd:element name="token" type="xsd:string" minOccurs="1" maxOccurs="1"/>
    <xsd:element name="sign"  type="xsd:string" minOccurs="1" maxOccurs="1"/>
  </xsd:sequence>
</xsd:complexType>
```

| Campo | Contenido |
|---|---|
| `source` | DN del WSAA que emitió el TA |
| `destination` | DN del certificado del cliente autenticado |
| `uniqueId` | uint32 |
| `generationTime` / `expirationTime` | Vigencia del TA |
| `token` / `sign` | Se envían al WSN. "El formato interno de estas cadenas puede diferir de un servicio a otro y su información contenida es interpretada por el WSN." |

### 4.2 Formato real (lo que hay que reproducir byte a byte)

Esto sale de las capturas de homologación de 2015, 2016 y 2018 [CAPTURA-OSS: `aabcehmt/fe`, `ezewer/fact_electronica`, `unagisoftware/afip-invoices`] y de los ejemplos de 2019 del [MANUAL]:

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<loginTicketResponse version="1">
    <header>
        <source>CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239</source>
        <destination>SERIALNUMBER=CUIT 20376793126, CN=batman</destination>
        <uniqueId>1861493915</uniqueId>
        <generationTime>2018-04-06T15:09:37.563-03:00</generationTime>
        <expirationTime>2018-04-07T03:09:37.563-03:00</expirationTime>
    </header>
    <credentials>
        <token>PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiIHN0YW5kYWxvbmU9InllcyI/Pgo8c3NvIHZlcnNpb249IjIuMCI+...</token>
        <sign>lOc0Zqwik+3aR5iIgbWQPvG/uNr8aIlUKGnMH7RRvGKyOetAqFdJFe42a1IdhnVhoP...=</sign>
    </credentials>
</loginTicketResponse>
```

Detalles que difieren de la SPEC o que la SPEC no menciona:

- **`version="1"`** en las respuestas reales; la SPEC muestra `1.0`. Lleva `standalone="yes"`, indentación de 4 espacios y un salto de línea final.
- **El `source` sale en mayúsculas** y con `", "` como separador: `CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239`. Para producción la SPEC da `cn=wsaa,o=afip,c=ar,serialNumber=CUIT 33693450239`; en el formato real sería `CN=wsaa, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239`. Esto es **inferido, no verificado en vivo**.
- **El `destination` es el DN del certificado del cliente** al estilo de `X500Principal.toString()` de Java: RDN en orden RFC 2253 (el último codificado va primero), `", "` como separador y `SERIALNUMBER` en mayúsculas. Ejemplos: `SERIALNUMBER=CUIT 20190178154, CN=glarriera20190903` ([MANUAL], certificado de WSASS) y `C=ar, O=nim, SERIALNUMBER=CUIT 20319944762, CN=aabcehmt` ([CAPTURA-OSS], 2015). La regla es una **inferencia** que cuadra con las capturas.
- **Fechas con milisegundos y offset `-03:00`.**
- **`expirationTime` = `generationTime` + exactamente 12 h.** Coincide en todos los ejemplos. La [SPEC] lo dice así: "El tiempo de validez de los TA emitidos es de 12 horas desde la fecha de emisión". [WEB-ARQ] agrega "(actualmente, 12 horas)". El `expirationTime` pedido en el TRA **no** acota la vida del TA: un TRA de 20 minutos recibe un TA de 12 horas ([MANUAL], capítulos 5 y 6).
- **`uniqueId` del TA ≠ `uniqueId` del TRA.** Parece un uint32 aleatorio: en la [SPEC] el TRA lleva 4325399 y el TA 383953094.
- **`sign`** = 128 bytes en Base64 (172 caracteres) en la captura de 2018, compatible con una firma RSA-1024 sobre el token. Es opaco: **el algoritmo real no está documentado**.

### 4.3 Contenido del `token`

El token es Base64 de un XML `sso`. Lo documenta el capítulo 6.3 del [MANUAL] y coincide con las capturas:

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<sso version="2.0">
    <id src="CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239" dst="CN=wsfe, O=AFIP, C=AR" unique_id="3485452165" gen_time="1523038117" exp_time="1523081377"/>
    <operation type="login" value="granted">
        <login entity="33693450239" service="wsfe" uid="SERIALNUMBER=CUIT 20376793126, CN=batman" authmethod="cms" regmethod="22">
            <relations>
                <relation key="20376793126" reltype="4"/>
            </relations>
        </login>
    </operation>
</sso>
```

- `gen_time` y `exp_time` están en **epoch en segundos**.
- `exp_time` coincide con el `expirationTime` del TA.
- `gen_time` es **60 s anterior** al `generationTime` del TA, en las tres capturas y en el ejemplo del [MANUAL] (allí `exp - gen = 43260 s`). **Patrón observado, no documentado.**
- `dst` = `CN=<servicio>, O=AFIP, C=AR`. Visto solo para `wsfe`; para otros servicios es **inferencia**.
- `unique_id` del token ≠ `uniqueId` del header del TA.
- `relations` lista las CUIT que el certificado puede representar. El [MANUAL] muestra dos: `20190178154` y `30333333313`.
- El orden de los atributos varía entre capturas. Es serialización de Java, sin significado.

### 4.4 Reglas de reutilización

- **La regla.** "Los CEE que hayan obtenido TA para un determinado servicio, deberán utilizarlo mientras sea valido, antes de solicitar uno nuevo" [SPEC].
- **Ventana preventiva** (FAQ 10.6 del [MANUAL], citado textual): "ACTUALMENTE ESE LAPSO PREVENTIVO ES DE **10 MINUTOS EN EL WSAA DE TESTING Y 2 MINUTOS EN EL WSAA DE PRODUCCION**. TENER EN CUENTA QUE ESTOS VALORES PUEDEN SER MODIFICADOS DINAMICAMENTE Y SIN AVISO PREVIO. Si se solicitan mas de un TA [...] dentro del lapso de retencion, estando vigente el anterior, el servicio WSAA de ARCA puede rechazar el pedido devolviendo el error 'El CEE ya posee un TA valido para el acceso al WSN solicitado'." Ese error es `coe.alreadyAuthenticated`.
- **WSAA no devuelve el TA anterior.** Rechaza el pedido.
- **No verificado:**
  - si la clave de la ventana es (DN del certificado, servicio), (CUIT, servicio) o (certificado concreto, servicio);
  - si pasada la ventana se emite un TA nuevo y el anterior sigue siendo válido en el WSN (lo razonable es que sí: ambos firmados, cada uno con su `exp_time`).

---

## 5. Validaciones y su orden

Orden **observado** en homologación [OBSERVADO]. Un paso que falla corta el proceso:

1. **SOAP/HTTP.** Sin header `SOAPAction` → `Client.NoSOAPAction`. XML del envelope malformado → `Server.userException`.
2. **Base64 de `in0`** → `cms.bad.base64`. Un `in0` vacío también.
3. **Parseo del CMS** → `cms.bad`, por ejemplo con el Base64 de `hello`.
4. **Certificado firmante presente** → `cms.cert.notFound` (CMS generado con `-nocerts`).
5. **Verificación de la firma** → `cms.sign.invalid`. Un CMS *detached* (sin contenido) da este error aunque el certificado no sea de confianza, así que la firma se verifica **antes** que la confianza en la AC.
6. **Vigencia del certificado.**
   - `notBefore` en el futuro → `cms.cert.invalid`. Pasó con un certificado con `notBefore` en 2030, y también con uno recién creado en la máquina local (el reloj local iba unos segundos adelantado), lo que indica **que no hay tolerancia de reloj para `notBefore`**.
   - `notAfter` vencido → `cms.cert.expired`.
7. **Confianza en la AC** → `cms.cert.untrusted`. Un TRA que no es XML, firmado con un certificado autofirmado, devuelve `cms.cert.untrusted` y no `xml.bad`, así que **la AC se valida antes que el XML**. Lo mismo un TRA con fechas de 2020.
8. **No observable sin un certificado emitido por ARCA.** Los pasos que siguen tienen un orden **probable** que **no está verificado**: esquema del XML (`xml.bad`) → `version` → `source`/`destination` → `generationTime`/`expirationTime` → servicio existente (`wsn.notFound`) → autorización (`coe.notAuthorized`) → ventana de reutilización (`coe.alreadyAuthenticated`) → disponibilidad (`wsn.unavailable`).

Para los pasos 6 y 7 no se pudo establecer el orden relativo frente al paso 5 con certificados inválidos y firma inválida a la vez. Solo se sabe que el paso 5 corre antes que el 7.

---

## 6. Errores (SOAP faults)

### 6.1 Tabla oficial [SPEC]

La [SPEC] dice: "En caso de encontrarse algún error, el mensaje SOAP devolverá un 'SoapFault' conteniendo código y descripción del error producido. La descripción podrá contener adicionalmente detalles mas específicos del error (ej: el XML expiró hace 10 minutos)." Avisa también que podrán agregarse códigos nuevos.

| `faultcode` (sin prefijo) | Descripción según [SPEC] | Cuándo |
|---|---|---|
| `coe.notAuthorized` | CEE no autorizado a acceder los servicios de ARCA. No deberá solicitar nuevos TA hasta que no haya gestionado el acceso WSN correspondiente. | El certificado no está asociado al servicio pedido, se firmó con otro certificado o el `service` es incorrecto (FAQ 10.4 del [MANUAL]). |
| `coe.alreadyAuthenticated` | El CEE ha solicitado un ticket de acceso para el cual ya dispone de TA validos. No deberá solicitar nuevos TA mientras disponga de TA validos para ese WSN correspondiente. | Pedido repetido dentro de la ventana preventiva (ver 4.4). |
| `cms.bad` | El CMS no es valido | — |
| `cms.bad.base64` | No se puede decodificar el BASE64 | — |
| `cms.cert.notFound` | No se ha encontrado certificado de firma en el CMS | — |
| `cms.sign.invalid` | Firma inválida o algoritmo no soportado | — |
| `cms.cert.expired` | Certificado expirado | — |
| `cms.cert.invalid` | Certificado con fecha de generación posterior a la actual | — |
| `cms.cert.untrusted` | Certificado no emitido por AC de confianza | AC ajena, o certificado de otro ambiente (FAQ 10.2). |
| `xml.bad` | No se ha podido interpretar el XML contra el SCHEMA | Mayúsculas y minúsculas incorrectas, fecha mal formada o imposible, `service` demasiado largo, mensaje mal firmado (FAQ 10.10). |
| `xml.source.invalid` | El atributo 'source' no se corresponde con el DN del Certificado | — |
| `xml.destination.invalid` | El atributo 'destination' no se corresponde con el DN del WSAA | — |
| `xml.version.notSupported` | La versión del documento no es soportada | — |
| `xml.generationTime.invalid` | El tiempo de generación es posterior a la hora actual o posee más de 24 horas de antigüedad | — |
| `xml.expirationTime.expired` | El tiempo de expiración es inferior a la hora actual | — |
| `xml.expirationTime.invalid` | El tiempo de expiración del documento es superior a 24 horas | — |
| `wsn.unavailable` | El servicio al que se desea acceder se encuentra momentáneamente fuera de servicio | — |
| `wsn.notFound` | Servicio informado inexistente | — |
| `wsaa.unavailable` | El servicio de autenticación/autorización se encuentra momentáneamente fuera de servicio | — |
| `wsaa.internalError` | No se ha podido procesar el requerimiento | — |

**Reglas de reintento** [SPEC]:

- Con códigos distintos de `wsaa.*` y `wsn.unavailable`, el cliente no debe pedir TA nuevos hasta corregir la causa (relaciones, reloj, desarrollo).
- Con `wsaa.*` y `wsn.unavailable`, no debe pedir TA nuevos dentro de los **60 segundos** siguientes.
- La [SPEC] **no documenta** que el servidor aplique un castigo si el cliente no respeta esos 60 segundos.

### 6.2 Textos reales de `faultstring` (difieren de la SPEC)

| `faultcode` | `faultstring` real | Fuente |
|---|---|---|
| `ns1:cms.bad.base64` | `No se puede decodificar el BASE64` | [OBSERVADO] |
| `ns1:cms.bad` | `El CMS no es valido` | [OBSERVADO] |
| `ns1:cms.cert.notFound` | `No se ha encontrado certificado de firmador` (**≠ SPEC**) | [OBSERVADO] |
| `ns1:cms.sign.invalid` | `Firma inv&#xE1;lida o algoritmo no soportado` (la `á` sale como referencia numérica) | [OBSERVADO] |
| `ns1:cms.cert.expired` | `Certificado expirado` | [OBSERVADO] |
| `ns1:cms.cert.invalid` | `Certificado con fecha de generacion posterior a la actual` (**sin tilde**) | [OBSERVADO] |
| `ns1:cms.cert.untrusted` | `Certificado no emitido por AC de confianza` | [OBSERVADO] |
| `ns1:coe.alreadyAuthenticated` | `El CEE ya posee un TA valido para el acceso al WSN solicitado` (**≠ SPEC**) | [MANUAL] FAQ 10.6; [OSS] pyafipws 2013 (https://groups.google.com/g/pyafipws/c/faq4C4nWmSM) y AfipSDK (https://github.com/AfipSDK/afip.php/issues/16) |
| `ns1:coe.notAuthorized` | `Computador no autorizado a acceder al servicio` (FAQ 10.4); `Computador no autorizado a acceder al Servicio` (con mayúscula, pyafipws 2015); `Computador no autorizado a acceder a los servicios de AFIP` (FAQ 10.3, otro caso) | [MANUAL]; [OSS] https://groups.google.com/g/pyafipws/c/qZ2sJ-TOM_g |
| `ns1:xml.generationTime.invalid` | `generationTime posee formato o dato inválido (ej: en el futuro o más de 24 horas de antigüedad)` (**≠ SPEC**) | [OSS] 2019, https://www.phpcentral.com/pregunta/2062/error-ns1xmlgenerationtimeinvalid |
| Resto de los `xml.*`, `wsn.*`, `wsaa.*` | **No verificado.** Usar el texto de la SPEC. | — |

Aunque el código del FAQ 10.3 no figura en esa entrada del manual, el texto del FAQ 10.4 sí aparece junto al código `coe.notAuthorized` en los foros. Si el FAQ 10.3 corresponde al mismo código es **inferencia**.

### 6.3 Forma exacta del fault [OBSERVADO]

SOAP 1.1, HTTP 500, `Content-Type: text/xml;charset=utf-8`:

```xml
<?xml version="1.0" encoding="UTF-8"?><soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><soapenv:Body><soapenv:Fault><faultcode xmlns:ns1="http://xml.apache.org/axis/">ns1:cms.bad.base64</faultcode><faultstring>No se puede decodificar el BASE64</faultstring><detail><ns2:exceptionName xmlns:ns2="http://xml.apache.org/axis/">gov.afip.desein.dvadac.sua.view.wsaa.LoginFault</ns2:exceptionName><ns3:hostname xmlns:ns3="http://xml.apache.org/axis/">wsaaext0.homo.afip.gov.ar</ns3:hostname></detail></soapenv:Fault></soapenv:Body></soapenv:Envelope>
```

Estructura:

- El prefijo `ns1` está ligado a `http://xml.apache.org/axis/`, **no** a un namespace de AFIP. Por eso los clientes muestran `ns1:coe.alreadyAuthenticated`.
- El `detail` lleva `exceptionName` = `gov.afip.desein.dvadac.sua.view.wsaa.LoginFault` y `hostname` = nodo interno (`wsaaext0.homo.afip.gov.ar` en homologación; el de producción **no lo verifiqué**).
- El cuerpo va en una sola línea, sin indentación.

SOAP 1.2, HTTP 500, `Content-Type: application/soap+xml;charset=UTF-8`:

```xml
<?xml version="1.0" encoding="UTF-8"?><soapenv:Envelope xmlns:soapenv="http://www.w3.org/2003/05/soap-envelope" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><soapenv:Body><soapenv:Fault><soapenv:Code xmlns:ns1="http://xml.apache.org/axis/"><soapenv:Value>ns1:cms.bad</soapenv:Value></soapenv:Code><soapenv:Reason><soapenv:Text xml:lang="en">El CMS no es valido</soapenv:Text></soapenv:Reason><soapenv:Detail><ns2:exceptionName xmlns:ns2="http://xml.apache.org/axis/">gov.afip.desein.dvadac.sua.view.wsaa.LoginFault</ns2:exceptionName><ns3:hostname xmlns:ns3="http://xml.apache.org/axis/">wsaaext0.homo.afip.gov.ar</ns3:hostname></soapenv:Detail></soapenv:Fault></soapenv:Body></soapenv:Envelope>
```

Faults de Axis, ajenos a WSAA:

- **Sin `SOAPAction`.** `faultcode` = `ns1:Client.NoSOAPAction`, `faultstring` = `no SOAPAction header!`. El `detail` trae solo `hostname`, sin `exceptionName`, y la respuesta sale **indentada**.
- **Sin `in0`.** `faultcode` = `soapenv:Server.userException`, `faultstring` = `javax.ejb.EJBException: java.lang.NullPointerException`.
- **XML malformado.** `faultcode` = `soapenv:Server.userException`, `faultstring` = `org.xml.sax.SAXParseException; lineNumber: 1; columnNumber: 96; XML document structures must start and end within the same entity.`

---

## 7. Cómo consumen el TA los WSN (WSFEv1)

### 7.1 Elemento `Auth`

Según el WSDL `docs/arca/wsdl/wsfev1-homologacion.wsdl`, en el namespace `http://ar.gov.afip.dif.FEV1/`:

```xml
<s:complexType name="FEAuthRequest">
  <s:sequence>
    <s:element minOccurs="0" maxOccurs="1" name="Token" type="s:string"/>
    <s:element minOccurs="0" maxOccurs="1" name="Sign"  type="s:string"/>
    <s:element minOccurs="1" maxOccurs="1" name="Cuit"  type="s:long"/>
  </s:sequence>
</s:complexType>
```

- `Token` y `Sign` son los valores `token` y `sign` del TA, tal cual.
- `Cuit` es la "Cuit contribuyente (representado o Emisora)" [WSFE-MANUAL]. Tiene que figurar en las `relations` del token.
- Todas las operaciones salvo `FEDummy` llevan `Auth`. `FEDummy` responde sin autenticación: `<AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer>` [OBSERVADO].
- El TA se pide con `service` = `wsfe` y dura 12 horas [WSFE-MANUAL].

### 7.2 Errores de autenticación en WSFEv1

WSFEv1 no responde con un SOAP fault: devuelve **HTTP 200** con `Errors/Err` dentro del `...Result`.

Códigos oficiales según la tabla de errores de infraestructura del [WSFE-MANUAL]:

| Code | Causa según [WSFE-MANUAL] |
|---|---|
| 500 | Error interno de aplicación. |
| 501 | Error interno de base de datos. |
| 502 | Error interno de base de datos - Autorizador CAE / Régimen CAEA - Transacción Activa |
| 600 | No se corresponden token y firma. Usuario no autorizado a realizar esta operación |
| 601 | CUIT representada no incluida en token. |
| 602 | No existen datos en nuestros registros. |

Textos reales observados en `FECompUltimoAutorizado` contra `https://wswhomo.afip.gov.ar/wsfev1/service.asmx` [OBSERVADO]:

| Caso | Respuesta |
|---|---|
| Sin `Auth` | `<Err><Code>500</Code><Msg>Campo Auth no fue ingresado o esta mal formado.</Msg></Err>` |
| `Token` no decodificable o que no es XML | `<Code>600</Code><Msg>ValidacionDeToken: No valido token. Excepcion: CargarStringBase64Token: Excepción: CargarNodos: Error al cargar token XML. Excepcion: Data at the root level is invalid. Line 1, position 1.</Msg>` |
| Token `sso` bien formado pero vencido | `<Code>600</Code><Msg>ValidacionDeToken: No validaron las fechas del token. GenTime=1573830140, ExpTime=1573873400, NowUTC=1790905612</Msg>` |
| Token `sso` vigente con `Sign` que no corresponde | `<Code>600</Code><Msg>ValidacionDeToken: Error al verificar hash: </Msg>` (con un espacio final y nada más) |
| CUIT que no está en las relaciones | `(600) ValidacionDeToken: No aparecio CUIT en lista de relaciones` [OSS: https://afipsdk.com/blog/solucion-a-no-aparecio-cuit-en-lista-de-relaciones/]. El manual atribuye este caso al código 601, pero en la práctica aparece como 600. |

Orden observado en WSFEv1: parseo del token → fechas → hash/firma → (presumiblemente) CUIT en relaciones. Las fechas se validan antes que la firma.

En esas respuestas, `PtoVta`, `CbteTipo` y `CbteNro` vuelven en `0`. El header SOAP incluye `<FEHeaderInfo><ambiente>HomologacionExterno - srt</ambiente><fecha>...</fecha><id>7.0.0.53</id></FEHeaderInfo>`.

**No verificado:**

- si WSFEv1 rechaza un token emitido para otro servicio (`dst` ≠ `CN=wsfe, ...`) y con qué texto;
- si existe una tolerancia de reloj en la validación de fechas del token.

Otros WSN, como el padrón A5, usan otro formato de autenticación (`token`, `sign` y `cuitRepresentada` como parámetros sueltos), según el capítulo 6.4 del [MANUAL]. Quedan fuera de este documento.

---

## 8. Qué tiene que reproducir ArcaSim

### 8.1 Indispensable para que el cliente no note la diferencia

1. **Rutas y WSDL.**
   - Ruta `/ws/services/LoginCms`.
   - `?wsdl` (y `?WSDL`) devuelve el WSDL oficial con `soap:address` y el namespace `impl` del host del simulador. Lo más fiel es servir el archivo de `docs/arca/wsdl/` reescribiendo solo la URL.
   - El namespace `http://wsaa.view.sua.dvadac.desein.afip.gov` no se toca.
   - Un GET sin `?wsdl` devuelve la página de Axis.
2. **SOAP 1.1 y 1.2.**
   - Responder en la misma versión SOAP del request.
   - Exigir el header `SOAPAction` en SOAP 1.1 con cualquier valor.
   - No validar el namespace de `loginCms`.
3. **Faults idénticos.**
   - HTTP 500.
   - `faultcode` con `xmlns:ns1="http://xml.apache.org/axis/"`.
   - `detail` con `exceptionName` = `gov.afip.desein.dvadac.sua.view.wsaa.LoginFault` y `hostname`.
   - Los textos reales de la tabla 6.2, incluidas las rarezas (`firmador`, `generacion` sin tilde, `&#xE1;`).
   - El `hostname` debería ser configurable.
4. **Validación del CMS en el orden de la sección 5.**
   - Base64 → SignedData → certificado incluido → firma (SHA-1 y SHA-256 como mínimo) → `notBefore`/`notAfter` → cadena de confianza.
   - Un CMS *detached* da `cms.sign.invalid`.
5. **Validación del TRA.**
   - Esquema (sin namespace, sensible a mayúsculas).
   - `version`.
   - `source`/`destination`, incluido el DN propio del WSAA según el ambiente.
   - Ventanas de ±24 h.
   - `service` existente (`wsn.notFound`).
   - Autorización certificado ↔ servicio (`coe.notAuthorized`).
6. **Ventana anti-repetición** (`coe.alreadyAuthenticated`) configurable, con 10 min en homologación y 2 min en producción por defecto. Debe poder apagarse.
7. **TA con el formato real de la sección 4.2.**
   - `version="1"`, `standalone="yes"`, indentación de 4 espacios.
   - `source` según el ambiente.
   - `destination` = DN del certificado del cliente al estilo Java.
   - `uniqueId` aleatorio.
   - Milisegundos y `-03:00`.
   - Vida de 12 h.
   - Todo escapado como texto dentro de `loginCmsReturn`.
8. **Token `sso` 2.0 decodificable** como en 4.3, con `relations` según las autorizaciones configuradas y `gen_time` = generación - 60 s. Hay clientes que decodifican el token para leer `exp_time` o las relaciones.
9. **`sign` verificable por los WSN simulados.** Como el formato es opaco, puede ser cualquier firma propia del simulador (por ejemplo RSA sobre el token con una clave de ArcaSim). Los WSN simulados (WSFEv1) deben validarlo y devolver los errores 600 de la sección 7.2 con sus textos y su orden.
10. **Reloj inyectable.** Sin él no se pueden probar los vencimientos del TA, la ventana de 24 h ni `coe.alreadyAuthenticated` sin esperar.
11. **Administración en lugar de WSASS.** Una API o configuración que haga lo mismo:
    - alta de DN/alias por CUIT;
    - emisión de certificados a partir de un CSR, con el DN forzado a `SERIALNUMBER=CUIT n, CN=alias`;
    - creación y borrado de autorizaciones (alias, CUIT representada, servicio);
    - catálogo de servicios.

### 8.2 Decisiones que hay que tomar (no son imposibles)

- **Aceptar los certificados reales de homologación de ARCA.** Para verificar la firma de un CMS alcanza con la parte pública. Si el simulador confía, además de en su propia AC, en `CN=Computadores Test` / `CN=AC Raiz Test` (sección 3.3), el desarrollador puede usar **el mismo certificado de WSASS** contra ArcaSim y contra ARCA. Lo único que cambiaría sería la URL.
- **Certificado TLS.** ARCA usa certificados de Sectigo. ArcaSim no puede presentar uno válido para `*.afip.gov.ar`, así que el cliente tiene que confiar en el certificado TLS del simulador o usar HTTP en desarrollo. Esto agrega un cambio además de la URL y conviene documentarlo.

### 8.3 Imposible o sin sentido simular

| Qué | Por qué |
|---|---|
| Emitir certificados firmados por las AC reales de ARCA | No tenemos sus claves privadas. ArcaSim usa su propia AC. |
| Tokens que los WSN reales de ARCA acepten | El `sign` real lo firma ARCA y su algoritmo no es público. Los TA de ArcaSim solo sirven contra los WSN de ArcaSim. |
| La interfaz de WSASS y del Administrador de Relaciones con clave fiscal | Basta con su modelo de datos (8.1, punto 11). |
| Balanceo, cookies F5/TS, nodos `wsaaext0`, NTP de `time.afip.gov.ar` | No afectan el contrato. Como mucho, el `hostname` del fault es configurable. |
| Cambios "dinámicos y sin aviso" de la ventana anti-repetición | Se cubren con configuración. |
| Caídas reales (`wsaa.unavailable`, `wsn.unavailable`, `wsaa.internalError`) | No se simulan solas, pero conviene poder **inyectarlas** para probar los 60 s de espera del cliente. |

---

## 9. Pendientes y no verificado

1. El orden de las validaciones del XML del TRA y de la autorización (sección 5, paso 8). Requiere un certificado real de WSASS.
2. Los textos reales de `xml.bad`, `xml.source.invalid`, `xml.destination.invalid`, `xml.version.notSupported`, `xml.expirationTime.expired`, `xml.expirationTime.invalid`, `wsn.notFound`, `wsn.unavailable`, `wsaa.unavailable` y `wsaa.internalError`.
3. La semántica exacta de `xml.expirationTime.invalid`: si el límite de 24 h se mide desde `now` o desde `generationTime`.
4. Cómo se interpreta un `dateTime` sin offset en el TRA.
5. El límite real de largo de `service`: 32 según el XSD o 35 según el FAQ.
6. Los algoritmos aceptados además de SHA-1 y SHA-256 con RSA, y si se rechazan claves que no sean de 2048 bits.
7. Qué devuelve un certificado de un ambiente usado en el otro: `cms.cert.untrusted` según el FAQ 10.2 o `coe.notAuthorized` según el FAQ 10.3.
8. Cómo se identifica a un cliente en la ventana de `coe.alreadyAuthenticated`, y si el TA anterior sigue siendo válido después de emitir uno nuevo.
9. El `source` y el `hostname` reales en producción. No sondeé producción adrede.
10. El código HTTP de una respuesta exitosa de `loginCms` (lo esperable es 200).
11. En WSFEv1: si se valida el `dst` del token (que el servicio coincida), y si se usa 601 o 600 para una CUIT no relacionada (en la práctica se ve 600).
12. Si existe control de repetición sobre `uniqueId` + `generationTime`. La [SPEC] dice que "identifica el requerimiento", pero no documenta ningún error de duplicado.
