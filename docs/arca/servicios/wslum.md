# wslum

Liquidación Mensual Única de Lechería (`LumService`). Con este servicio, el comprador de leche cruda (la industria láctea u "operador comercial", que es la CUIT representada) emite cada mes la liquidación al productor tambero y obtiene un **CAE**. La misma operación `generarLiquidacion` sirve para los ajustes: notas de crédito y débito (tipos 43-48) con un bloque `ajuste` que apunta al CAE original o a una factura en papel con CAI. **No hay una operación de ajuste aparte**, aunque el §1.2 del manual diga "Ajustar una liquidación" como si fuera otro método. También se puede consultar una liquidación por CAE o por número de comprobante, pedir el último número por punto de venta y leer cuatro tablas de parámetros.

Manual: "Lechería - Liquidación Mensual Única — Web Service LumService", **versión 1.4 del 03/03/2017** (82 págs.), en `https://www.afip.gob.ar/ws/wslum/manual_wslum1.4.pdf`. El PDF tiene fecha de 2017, pero ya trae URLs, namespaces y mails reescritos a "arca" (ver Contrato). Las citas usan las secciones del manual y la página impresa del índice.

Es parte de una familia de liquidaciones sectoriales Java con el mismo esqueleto: `wslsp`, `wslum`, `wslca` y `wsltv`. Todas tienen `auth` dentro del request, `generarLiquidacion` (que devuelve el CAE), consulta por número de comprobante, último número por punto de venta, errores en `respuesta/errores/error` y un `pdf` en base64. wslum se aparta del resto en tres cosas: no tiene `metadata` en las respuestas, no tiene un método de ajuste propio, y en las consultas el PDF se pide con un flag. Esta ficha no depende de las otras.

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | **Java JAX-WS** (stack Metro/RI). Lo observado en la captura del 2026-10-02: `<?xml version='1.0' encoding='UTF-8'?><S:Envelope ...>` sin `Header`, el body es `ns2:DummyResp` con `<respuesta>` y `appserver`, `authserver` y `dbserver` = `OK` en mayúsculas, con `Content-Type: text/xml;charset=utf-8` y `Transfer-Encoding: chunked`. El Fault de ejemplo del manual (§2.4) es de JAX-WS RI y Woodstox |
| Endpoint de homologación | `https://fwshomo.afip.gov.ar:443/wslum/LumService` (`soap:address` del WSDL). **El manual (§2.2, p. 6) da `https://fwshomo.arca.gob.ar/wslum/LumService`**: ese host no se probó y queda NO VERIFICADO. catalogo.md §3.3 confirmó que responde el WSDL en `fwshomo.afip.gov.ar` |
| Endpoint de producción | `https://serviciosjava.afip.gob.ar:443/wslum/LumService` (`soap:address` del WSDL de producción). El manual da dos variantes distintas: `https://serviciosjava.arca.gov.ar/wslum/LumService` para conectarse y `https://serviciosjava.arca.gob.ar/wslum/LumService?wsdl` para ver el WSDL. Ninguna de las dos se probó. Los hosts `*.arca.gov.ar` que se probaron no resuelven en DNS (catalogo.md §4.2) |
| targetNamespace | `http://serviciosjava.afip.gob.ar/wslum/`. **El manual usa en todos los ejemplos `http://serviciosjava.arca.gob.ar/wslum/`** (37 veces), que **no** es el del WSDL. La respuesta real (2026-10-02) usa `afip`. Un cliente armado copiando los ejemplos del manual manda el namespace equivocado. El simulador sigue al WSDL |
| service / port / binding / portType | `LumService` / `LumEndPoint` / `wslumSOAP` / `LumPortType` |
| WSDL guardado | `docs/arca/wsdl/wslum-homologacion.wsdl` (1175 líneas, 58 396 bytes; autocontenido: sin `wsdl:import`, `xsd:import` ni `xsd:include`). El de producción es idéntico salvo el `soap:address` (diff del 2026-10-02). Los 20 `wsdl:message` están todos en uso |
| WSAA service id | **No indicado en el manual: NO VERIFICADO.** El §2.3 habla de "la información obtenida del WSAA" pero no da el nombre del servicio. "wslum" solo aparece en URLs y namespaces. Por la convención de wslsp y wslca, que sí lo dicen ("el token debe solicitarse para el servicio wslsp/wslca"), lo más probable es `wslum`, pero es una inferencia. catalogo.md §8 lo tiene como pendiente |
| SOAPAction | targetNamespace + nombre de la operación, por ejemplo `http://serviciosjava.afip.gob.ar/wslum/generarLiquidacion`. Verificado en las 10 operaciones |
| Versión SOAP | Solo SOAP 1.1 (un único binding `soap:`), estilo document/literal y una sola part `parameters` por mensaje |
| elementFormDefault | No está declarado, así que vale `unqualified`. Solo el elemento raíz del Body lleva namespace; `auth`, `solicitud` y todos sus hijos van **sin** namespace (confirmado en la respuesta real: `<ns2:DummyResp ...><respuesta>`) |
| Operaciones | **10**. Ninguna declara `wsdl:fault` |
| `dummy` | El mensaje de entrada no tiene parts y el Body va vacío. La respuesta es `DummyResp/respuesta/{appserver,authserver,dbserver}` (el esquema del manual escribe `dummyResp` en minúscula; el WSDL y la captura dicen `DummyResp`) |

## Autenticación

Va en el hijo `auth` (tipo `Auth`), que es el primer hijo 1..1 del elemento raíz de cada request:

```xml
<auth>
  <token>xsd:string</token>  <!-- 1..1 -->
  <sign>xsd:string</sign>    <!-- 1..1 -->
  <cuit>Cuit</cuit>          <!-- 1..1: xsd:long entre 10000000000 y 99999999999 (11 dígitos) -->
</auth>
```

- Se manda en las **9 operaciones que no son `dummy`**. Según §4.2.1, `<cuit>` es la "CUIT Representada / Comprador": la industria que liquida, no el tambero.
- `token` y `sign` "tienen longitud variable según la respuesta del WSAA" (§4.2).
- **Falla documentada:** ninguna en concreto. El manual no da un texto para token inválido o vencido. Las pistas que hay:
  - Los errores "excepcionales" van en `S:Fault` (§2.4).
  - La tabla de negocio (§4.1) tiene 2040 "La cuit representada, no se encuentra activa o es inexistente." y 2036 "La cuit representada, no registra una actividad válida en RUCA para emitir una liquidación." Esas son validaciones de la CUIT, no del token.
  - **NO VERIFICADO** si una falla del token vuelve como Fault o en `errores`.
- **Falla observada (homologación, 2026-10-02).** Se probó `ConsultarProvinciasReq` con token y sign "abc", con token y sign vacíos, y sin `auth`. Las tres respuestas fueron iguales: `HTTP/1.0 200 OK`, sin `Content-Type`, con `Connection: Keep-Alive`, `Content-Length: 39` y un body de texto plano, por ejemplo `BL5681214363165 2026-10-02 15:08:28 500`. No es SOAP. Es lo mismo que pasa en todos los servicios de fwshomo. La hipótesis, NO VERIFICADA, es que un WAF/F5 reemplaza cualquier HTTP 500 (o sea, cualquier Fault). **El Fault real no se pudo ver.**
- Request mínimo válido según el esquema (SOAPAction `"http://serviciosjava.afip.gob.ar/wslum/consultarUltimoNroComprobantePorPtoVta"`, `Content-Type: text/xml; charset=utf-8`):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:wsl="http://serviciosjava.afip.gob.ar/wslum/">
  <soapenv:Header/>
  <soapenv:Body>
    <wsl:ConsultarUltimoNroComprobantePorPtoVtaReq>
      <auth><token>...</token><sign>...</sign><cuit>20111111112</cuit></auth>
      <solicitud><puntoVenta>1</puntoVenta><tipoComprobante>27</tipoComprobante></solicitud>
    </wsl:ConsultarUltimoNroComprobantePorPtoVtaReq>
  </soapenv:Body>
</soapenv:Envelope>
```

Respuesta real de `dummy` (homologación, 2026-10-02, HTTP 200):

```xml
<?xml version='1.0' encoding='UTF-8'?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:DummyResp xmlns:ns2="http://serviciosjava.afip.gob.ar/wslum/"><respuesta><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></respuesta></ns2:DummyResp></S:Body></S:Envelope>
```

## Operaciones

Todos los elementos son `<Op>Req` y `<Op>Resp` con la primera letra en mayúscula, y cada respuesta trae un único hijo `respuesta`. Las liquidaciones vuelven en `LiquidacionDetalleRespuesta`, que tiene `liquidacion` (`LiquidacionDetalle`) y `errores`.

| operación | propósito | entrada principal | salida principal | crea estado / consulta estado |
|---|---|---|---|---|
| `dummy` | Estado de app, auth y base | Body vacío | `respuesta/{appserver,authserver,dbserver}` | — |
| `consultarProvincias` | Provincias | `auth` | `provincia*` (`CodigoDescripcion`) | Consulta tabla de parámetros |
| `consultarLocalidadesPorProvincia` | Localidades de una provincia | `solicitud/codProvincia` (0..99) | `localidad*` | Consulta tabla de parámetros |
| `consultarPuntosVenta` | Puntos de venta habilitados para el WS | `auth` | `puntoVenta*` (código y domicilio) | Consulta tabla de parámetros (por CUIT) |
| `consultarBonificacionesPenalizaciones` | Conceptos de bonificación, penalización y débito comercial | `auth` | `tipo*{codigo,descripcion,subtipo*{codigo,descripcion,valor,signo}}` | Consulta tabla de parámetros. Las restricciones del campo `resultado` por concepto están en §3.1.1, p. 55 |
| `consultarOtrosImpuestos` | Tipos de "otros impuestos" | `auth` | `otroImpuesto*` | Consulta tabla de parámetros |
| `generarLiquidacion` | Alta de la liquidación mensual **o de un ajuste** (NC/ND) | `solicitud`: `liquidacion{periodo,fechaComprobante,puntoVenta,tipoComprobante,nroComprobante,...,alicuotaIVA?,ajuste?,condicionVenta+}`, `tambero{cuit,iibb?}`, `tambo{nroTamboInterno,nroRenspa,ubicacionTambo,...}`, `balanceLitrosPorcentajesSolidos?`, `conceptosBasicosMercadoInterno?`, `conceptosBasicosMercadoExterno?`, `bonificacionPenalizacion*`, `otroImpuesto*`, `remito*` | `liquidacion/encabezado{cae,tipoComprobante,nroComprobante,fechaComprobante,periodo,fechaVencimiento,fecha,puntoVenta,cuitComprador,...}`, `ajuste?`, `tambero`, `tambo`, totales, `remito*`, `pdf` | **Crea** una liquidación o un ajuste y devuelve el **CAE**. Se lee con `consultarLiquidacionPorCae` (por CAE) o con `consultarLiquidacionPorNroComprobante` (por CUIT comprador + pto vta + tipo + nro) |
| `consultarLiquidacionPorNroComprobante` | Detalle de una liquidación | `solicitud/{cuitComprador,puntoVenta,tipoComprobante,nroComprobante,pdf (boolean)}` | `LiquidacionDetalleRespuesta` | **Lee estado** por CUIT comprador + pto vta + tipo + nro. `pdf=true` agrega el PDF |
| `consultarLiquidacionPorCae` | Detalle de una liquidación | `solicitud/{cae (NroCAE, 14 dígitos), pdf (boolean)}` | `LiquidacionDetalleRespuesta` | **Lee estado** por CAE |
| `consultarUltimoNroComprobantePorPtoVta` | Último número autorizado | `solicitud/{puntoVenta,tipoComprobante}` | `nroComprobante` (`NroComprobante`, 1..1) | **Lee estado**: el máximo número emitido para CUIT + pto vta + tipo |

No hay operaciones para consultar tipos de comprobante, condiciones de venta ni períodos: los valores están fijos en el WSDL (ver Comportamiento a simular).

## Errores

**Estructura.** Los errores van **dentro de la respuesta**, con HTTP 200, en `respuesta/errores/error` (`Errores`, con `error` 0..n de tipo `CodigoDescripcion`, `codigo` y `descripcion` `xsd:string`). En las liquidaciones, `errores` es hermano de `liquidacion`:

```xml
<respuesta>
  <liquidacion>...</liquidacion>
  <errores>
    <error><codigo>550</codigo><descripcion>Error al generar el archivo pdf.</descripcion></error>
  </errores>
</respuesta>
```

A diferencia de wslsp y wslca, **no hay `metadata`** en ninguna respuesta (el WSDL no lo define).

**Tipos de error (§2.4, p. 7-8):**

- **De formato:** son errores de esquema. El manual dice que vienen en `errores` con un código `cvc-*` y da dos ejemplos, en inglés: `cvc-type.3.1.3` "The value 'xxxxx' of element 'periodo' is not valid." y `cvc-complex-type.2.4.a` "Invalid content was found starting with element 'puntoVenta'. One of '{periodo}' is expected." Documentado, no observado.
- **Internos:** `500` "Error general de aplicación." (Rechazada), `550` "Error al generar el archivo pdf." (Aceptada: la respuesta llega sin `pdf` y con el error en `errores`), `700` "Error de sincronismo." (Rechazada) y `800` "Servicio no disponible." (Rechazada).
- **De negocio:** están en §4.1.
- **Excepcionales:** un `S:Fault`. Ejemplo del manual:

```xml
<S:Fault xmlns:ns4="http://www.w3.org/2003/05/soap-envelope">
  <faultcode>S:Client</faultcode>
  <faultstring>Couldn't create SOAP message due to exception: XML reader error: com.ctc.wstx.exc.WstxEOFException: Unexpected EOF; was expecting a close tag for element &lt;soapenv:Envelope>
 at [row,col {unknown-source}]: [2,3]</faultstring>
</S:Fault>
```

  No hay `detail`. El status HTTP no está documentado (en JAX-WS es 500; en fwshomo eso termina en la página `BL... 500`).

**Códigos de negocio (§4.1, "Tabla 5", págs. 73-77): 62 códigos.** Al extraer el texto con `pdftotext -layout` las columnas salen corridas. Esta tabla se reconstruyó con la posición de las palabras en el PDF (PyMuPDF) y se controló contra el §1.3 (código 2074) y el historial v1.4 (1005 y 1006 son de ajustes). Todos son **R** (rechazada).

| Código | Descripción |
|---|---|
| 1000 | Solicitud incompleta: debe especificar el campo <ajuste>. |
| 1001 | Solicitud incompleta: para ajustes debe especificar uno y solo uno de los siguientes campos: <formularioPapel>, <caeAAjustar>. |
| 1003 | Si no es un ajuste, no debe enviar datos en la etiqueta <ajuste>. |
| 1005 | Para ajustes monetarios los siguientes campos debe ser nulos: balanceLitrosPorcentajesSolidos, conceptosBasicosMercadoInterno y conceptosBasicosMercadoExterno. |
| 1006 | Para ajustes físicos debe informar el campo balanceLitrosPorcentajesSolidos. |
| 2004 | No se puede ajustar la liquidación ya que no fue encontrada por los siguientes parámetros: su número de CAE, CUIT del productor, CUIT del comprador, período y número de RENSPA. |
| 2011 | Ajuste papel: Los datos de la factura ingresados son incongruentes. |
| 2016 | Error en balance de litros porcentaje de sólidos: El valor de Kg remitidos no puede ser igual al los Kg decomisados. |
| 2024 | Error en balance de litros porcentaje de sólidos: El valor decomisados debe ser menor a remitidos. |
| 2029 | Error en bonificación: Debe ingresar un valor del tipo Libre ('Libre', 'En Saneamiento'). |
| 2030 | Error en penalización: Debe ingresar un valor del tipo No Libre ('No Libre', 'En Saneamiento'). |
| 2032 | Error en bonificación o penalización: Debe ingresar un valor numérico de 3 dígitos. |
| 2033 | El siguiente código de bonificación/penalidad es inexistente: {código}. |
| 2034 | La cuit ingresada, no corresponde a un Operador Comercial. |
| 2036 | La cuit representada, no registra una actividad válida en RUCA para emitir una liquidación. |
| 2038 | La cuit ingresada, no registra un domicilio fiscal válido. |
| 2040 | La cuit representada, no se encuentra activa o es inexistente. |
| 2044 | La liquidación que intenta obtener no existe según los parámetros de búsqueda. |
| 2053 | Error concepto Básico, el total de kg de grasa debe ser igual a la suma de kg de producción más crecimiento de grasa. |
| 2054 | Error concepto Básico, el total de kg de proteínas debe ser igual a la suma de kg de producción más crecimiento de proteínas. |
| 2055 | Error, el período seleccionado para el tipo de liquidación que se intenta realizar, no es válido. |
| 2064 | ERROR en el tipo de liquidación, se está intentando hacer una liquidación de una clase(A,B,C) y no es la que corresponde según la situación de IVA del Adquiriente y del Productor tambero. |
| 2074 | N° de comprobante incorrecto para el tipo de comprobante y punto de venta ingresados. |
| 2076 | Error en la generación del comprobante/factura, hubo un error en la llamada al procedimiento solicitarAutorizacionDeComprobante. |
| 2077 | El N° de comprobante o la fecha no se corresponde con el proximo a autorizar. |
| 2078 | Error en la generación de la liquidación, la misma debe ser única por período, CUIT comprador, CUIT productor y número de RENSPA. |
| 2080 | El comprador no tiene un domicilio válido. |
| 2082 | La cuit representada, no tiene puntos de venta activos para emitir una liquidación. |
| 2084 | ERROR al calcular el porcentaje del balance de sólido útiles del mercado interno o externo. |
| 2085 | La cuit ingresada, no se encuentra inscripta en IVA. |
| 2086 | El punto de venta informado no es válido. |
| 2091 | El domicilio del tambo es un campo obligatorio. |
| 2094 | Debe completar el campo detalle si y sólo si codBonificacionPenalizacion es 41(Bonificación comercial - Otros) o 50 (Débito Comercial). |
| 2095 | Debe completar el campo detalle si y sólo si tipo impuesto es 9(Otras percepciiones) o 10 (Otros). |
| 2096 | La suma de kilogramos de sólidos útiles por concepto básico no puede ser mayor a la cantidad de kilogramos de sólidos útiles. |
| 2103 | La cuit tambero, no se encuentra activa o es inexistente. |
| 2104 | No se puede enviar conceptos de bonificaciones/penalizaciones repetidos a excepción de los conceptos con código 41(Bonificación comercial - Otros) y 50(Débito Comercial). |
| 2105 | No se puede enviar impuestos repetidos a excepción del código 10 (Otros). |
| 2106 | En conceptos de mercado interno/externo, sí se ingresó un valor de kg distinto de 0 entonces el precio no puede ser 0. |
| 2107 | La CUIT del adquirente se encuentra inactiva. |
| 2108 | Las bonificaciones/penalidades con código en el rango 1 hasta 19 requieren obligatoriamente el campo <resultado>. |
| 2109 | El siguiente código de tipo de otro impuesto es inexistente: {<tipo>} |
| 2110 | El código de provincia ingresado es inexistente. |
| 2111 | El campo resultado para el valor temperatura (TEMP) es incorrecto. |
| 2112 | El campo resultado para el valor crioscopía (CRIO) es incorrecto. |
| 2113 | El importe total neto de la liquidación no puede ser cero o menor a cero. |
| 2114 | La alícuota IVA no se corresponde con la situación del tambero. |
| 2115 | No debe informar la alícuota IVA para el tipo de comprobante que intenta generar. |
| 2116 | Actualmente no se encuentra inscripto en RUCA, dirijase al Ministerio de Agroindustria - Dirección Nacional de Matriculación y Fiscalización. |
| 2118 | Las coordenadas geográficas del tambo se encuentran fuera de la provincia indicada. |
| 2120 | La condición de venta debe ser del tipo CONTADO,CUENTA_CORRIENTE,CHEQUE u OTRA. |
| 2121 | La fecha de comprobante de la liquidación debe pertenecer al año y al mes de la liquidación. |
| 2122 | Para bonificaciones/penalizaciones con código igual a 41 debe informar el campo <importe> y no <porcentajeAAplicar>. |
| 2123 | Para bonificaciones/penalizaciones con código distinto a 41 debe informar el campo <porcentajeAAplicar> y no <importe>. |
| 2124 | El campo <descripcion> para una condición de venta debe indicarse para el tipo 'Otra'. |
| 2126 | La cuit del productor tambero y la del adquiriente no pueden ser iguales. |
| 2129 | La localidad no pertenece a la provincia seleccionada. |
| 2130 | La fecha de comprobante de la liquidación no debe tener más de 10 días de diferencia hacia atrás con la fecha de hoy. |
| 2131 | La fecha del comprobante no puede ser posterior a hoy. |
| 2132 | La fecha de comprobante ingresada, es anterior a la fecha de comprobante de una liquidación(activa) generada con anterioridad para la misma cuit, punto de venta y tipo de liquidacion. |
| 2134 | El código de bonificación/penalidad es inexistente para el ajuste monetario. |
| 2135 | En el ajuste monterio, no puede seleccionar una bonificación comercial y un débito comercial para el mismo documento. |

## Comportamiento a simular

**Numeración (§1.3, p. 4-5).**

- Un CAE autorizado se identifica por **`puntoVenta` + `tipoComprobante` + `nroComprobante`** (y la CUIT). El número arranca en 1 y sube de a uno, por separado para cada CUIT, punto de venta y tipo de comprobante.
- El cliente elige el número. Si la solicitud se rechaza, el número no se consume. El próximo número válido es `consultarUltimoNroComprobantePorPtoVta + 1`.
- Si la combinación de pto vta, tipo y número es incorrecta, el servicio responde el error **2074** (§1.3). Existe además el 2077 "El N° de comprobante o la fecha no se corresponde con el proximo a autorizar".
- Los ajustes usan **su propio tipo de comprobante** (nota de crédito o débito), así que su numeración va aparte de la liquidación original.
- Rangos (WSDL): `PuntoVenta` int 1..99999 y `NroComprobante` int 1..99999999.
- `TipoComprobante` es un `xsd:short` con **enumeración cerrada** (WSDL y §3.1, p. 54):
  - 27, 28 y 29: Liquidación Única Comercial Impositiva clase A, B y C.
  - 43 y 44: Nota de Crédito B y C.
  - 45, 46 y 47: Nota de Débito A, B y C.
  - 48: Nota de Crédito A.
- La respuesta de `consultarUltimo...` tipa `nroComprobante` como `NroComprobante` (mínimo 1, 1..1), así que según el esquema **no puede venir 0**. Qué devuelve cuando todavía no hay comprobantes está **NO VERIFICADO**. JAX-WS no valida la salida, así que podría venir 0, o podría venir solo `errores`.

**Id que asigna ARCA.** Viene en `liquidacion/encabezado/cae` (`xsd:long`). En las solicitudes el tipo es `NroCAE`: long entre 10000000000000 y 99999999999999, o sea **14 dígitos**. Ejemplos: `75521002437246` y `75511002412454`. El encabezado también trae:

- `fechaVencimiento` ("Fecha vencimiento de CAE", §3.2).
- `fecha` (la fecha de proceso, según los ejemplos).
- `cuitComprador`, `razonSocialComprador` y `situacionIVAComprador`.
- `nroComprobante`, `puntoVenta` y `tipoComprobante`. `puntoVenta` y `tipoComprobante` vuelven como `xsd:string` en la respuesta.

**Clave de negocio y unicidad.** Error 2078: la liquidación "debe ser única por **período, CUIT comprador, CUIT productor y número de RENSPA**". Es lo más parecido a una idempotencia que tiene el servicio. Si se reenvía la misma liquidación con otro número, se rechaza con 2078. Si se reenvía con el mismo número ya autorizado, se rechaza por numeración (2074 o 2077) **[INFERIDO]**: el manual no tiene un ejemplo de reenvío. La recuperación documentada para un timeout (§1.4) es consultar el último número y leer la liquidación.

**Ajustes (§2.6.4, historial v1.4, errores 1000-1006 y 2004).**

- Se hacen con `generarLiquidacion`, usando un tipo de NC o ND y el bloque `liquidacion/ajuste` (`LiquidacionAjustadaSolicitud`), que tiene:
  - `tipoAjuste`: `FISICO` o `MONETARIO`.
  - **Uno y solo uno** de `caeAAjustar` (`NroCAE`) o `formularioPapel{cai,fechaEmision,tipoComprobante,nroComprobante,puntoVenta}`, para ajustar una factura en papel con CAI (error 1001).
- Si el tipo de comprobante no es de ajuste, no se puede mandar `ajuste` (1003). El 1000 indica que falta `ajuste` cuando el tipo lo pide; que se dispare por el tipo de comprobante es una inferencia.
- En un ajuste monetario no van el balance de litros ni los conceptos básicos (1005). En uno físico el balance es obligatorio (1006).
- El original se busca por CAE, CUIT del productor, CUIT del comprador, período y RENSPA (2004).
- La respuesta repite el bloque `ajuste` (mismo tipo `LiquidacionAjustadaSolicitud`).
- El ejemplo del manual (§2.6.4.3, Solicitud 2) es una NC A (tipo 48) que ajusta una factura papel tipo 27.
- No hay estados documentados ("anulada", "ajustada") ni un límite de ajustes por liquidación. El 2132 menciona liquidaciones "activas", lo que sugiere que existe un estado inactivo **NO VERIFICADO**.

**Plazos y período (§4.2.1, errores 2121, 2130-2132).**

- `periodo` tiene formato `AAAA/MM` (patrón `((19|20|21)\d{2}[/\\/](0[1-9]|1[0-2]))`).
- Se puede liquidar dentro del período actual y "hasta el día 10 del mes siguiente".
- `fechaComprobante` tiene que caer en el año y mes del período (2121), no puede tener más de 10 días hacia atrás (2130), no puede ser futura (2131) y no puede ser anterior a la última liquidación activa del mismo pto vta y tipo (2132).

**IVA (§4.2.1).** `alicuotaIVA` vale 21 para el tipo 27, 0 o 21 para los tipos 45 y 48, y no se informa en el resto (2115).

**Cálculos.** Las fórmulas de los campos calculados de la respuesta están en §4.2.2 (Tabla 7, p. 76-78). Por ejemplo, `litrosNetosLiquidados` = remitidos − decomisados, `totalLiquidacion` = básico + bonificaciones − penalizaciones ..., y `totalNetoLiquidacion` = total − otros impuestos + IVA. El redondeo es Round Half Even a 2 decimales (§4.2). Un simulador de contrato puede devolver estos campos sin validar las cuentas.

**PDF (§2.5).**

- `pdf` (`xsd:base64Binary`) es "el mismo archivo que se imprime por la aplicación web". En los ejemplos lo genera JasperReports + iText.
- En las dos consultas se pide con `solicitud/pdf` (boolean, **obligatorio** en el esquema).
- `generarLiquidacion` no tiene flag, y el ejemplo de alta lo devuelve igual.
- Con el error 550 la operación se acepta y la respuesta llega sin `pdf` (ejemplo del §2.6.9.3).

**Formatos.**

- Fecha: `AAAA-MM-DD` sin huso horario (§4.2).
- `Latitud` y `Longitud`: `decimal(6)` dentro de la caja de Argentina (−55.06..−21.78 y −73.57..−53.65).
- `NroRenspa`: `\d{1,2}[.]\d{3}[.]\d{1}.\d{5}[/]\w{2}`.
- `NroRemito`: long entre 100000000 y 999999999999.
- `CodigoCondicionVenta`: enumeración 0..3. La v1.3 la renumeró; el error 2120 nombra CONTADO, CUENTA_CORRIENTE, CHEQUE y OTRA.

**Paginación:** no hay.

## No verificado

- El WSAA service id. El manual no lo da; se supone `wslum`.
- El formato real del error de autenticación. En fwshomo cualquier error llega como la página `BL<n> <fecha> 500` (capturas del 2026-10-02), y el manual no documenta ese caso.
- Si los hosts del manual (`fwshomo.arca.gob.ar`, `serviciosjava.arca.gov.ar`, `serviciosjava.arca.gob.ar`) existen y atienden el mismo servicio.
- Qué responde el servidor real si recibe el namespace `http://serviciosjava.arca.gob.ar/wslum/` de los ejemplos del manual. Lo esperable en JAX-WS es un Fault de operación desconocida, que en fwshomo se vería como `BL... 500`.
- Qué devuelve `consultarUltimoNroComprobantePorPtoVta` sin comprobantes previos.
- Qué error da reenviar un número ya autorizado (2074 o 2077).
- Si existen estados de liquidación (activa, anulada) y cómo se anula. No hay un método de anulación.
- La regla de `fechaVencimiento` del CAE.
- Si los errores `cvc-*` llegan de verdad en `errores` con HTTP 200.
