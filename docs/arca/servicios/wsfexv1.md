# wsfexv1

**Factura Electrónica de Exportación (RG 2758).** Autoriza comprobantes clase E: 19 Factura de Exportación, 20 Nota de Débito y 21 Nota de Crédito por operaciones con el exterior. Devuelve un CAE por comprobante. A diferencia de wsfev1 lleva ítems, no discrimina IVA, y tiene **idempotencia explícita**: cada pedido trae un `Id` propio y reenviar el mismo `Id` devuelve lo ya otorgado con `Reproceso`=`S`.

Relevamiento hecho el 2026-10-02. Fuentes y marcas:

| Marca | Fuente |
|---|---|
| **[MAN]** | "Factura de Exportación Versión 3.1.0 – R.G. N° 2.758 – Manuales para el desarrollador", ARCA-SDG SIT, "versión correspondiente al 18 de Agosto de 2025", 59 páginas. El historial llega a la 3.1.1 (18-08-2025). URL: `https://www.afip.gob.ar/ws/documentacion/manuales/WSFEX-Manualparaeldesarrollador_V3.1.1_ARCA.pdf` (bajado el 2026-10-02, HTTP 200, 1 864 290 bytes). "pág. N" = página física del PDF, que coincide con el pie "Página N de 59". |
| **[WSDL]** | `docs/arca/wsdl/wsfexv1-homologacion.wsdl` (bajado el 2026-10-01). El de producción (`https://servicios1.afip.gov.ar/wsfexv1/service.asmx?WSDL`, bajado el 2026-10-02) es idéntico salvo `soap:address` y `soap12:address`. El esquema está embebido: no hay XSD que bajar, así que no hay carpeta `wsdl/wsfexv1/`. |
| **[VIVO]** | Llamadas propias sin credenciales a homologación y producción el 2026-10-02. |
| **[TAB]** | "Tablas del sistema" genéricas de factura electrónica: `https://www.afip.gob.ar/fe/ayuda/tablas.asp` (incoterms, idiomas, unidades de medida, monedas, países). **No son del manual de wsfexv1**; que coincidan con lo que devuelven los `FEXGetPARAM_*` es NO VERIFICADO. |

Cuando WSDL y manual no coinciden, manda el WSDL. Hay varias diferencias de nombres que rompen clientes (ver cada operación).

## Contrato

| Aspecto | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX. Errores de negocio y de autenticación con HTTP 200 dentro de `FEXErr` | [WSDL] [VIVO] |
| Endpoint homologación | `https://wswhomo.afip.gov.ar/wsfexv1/service.asmx` (`soap:address`; el manual dice lo mismo, pág. 10) | [WSDL] [MAN] |
| Endpoint producción | `https://servicios1.afip.gov.ar/wsfexv1/service.asmx` | [WSDL] [MAN pág. 10] |
| Namespace | `http://ar.gov.afip.dif.fexv1/` (**`fexv1` en minúsculas**, a diferencia de `FEV1` de wsfev1). `elementFormDefault="qualified"` | [WSDL] |
| Archivo WSDL | `docs/arca/wsdl/wsfexv1-homologacion.wsdl` (66 373 bytes; producción 66 379) | — |
| WSAA service id | **`wsfex`** (no "wsfexv1"): "debe enviar el tag service con el valor "wsfex" y [...] la duración del mismo es de 12 hs" | [MAN pág. 7] |
| SOAPAction | `http://ar.gov.afip.dif.fexv1/<Operación>` (19) | [WSDL] |
| SOAP 1.1 / 1.2 | Ambos (`ServiceSoap`, `ServiceSoap12`, misma URL). En vivo, `FEXDummy` por SOAP 1.2 devuelve 200 `application/soap+xml; charset=utf-8` con envelope 1.2 y el mismo `FEHeaderInfo` | [WSDL] [VIVO] |
| Header de respuesta | `soap:Header/FEHeaderInfo{ambiente, fecha, id}` en todas las respuestas, incluso en los `soap:Fault`. Vivo: homologación `Homologacion-Ext - srext`, producción `Produccion - sre05`, `id` = `5.0.1.0`. El manual (pág. 7-8) muestra `Homologacion - Clo`, `Produccion - Pto` e `id` `1.0.3.0` (viejos) | [VIVO] [MAN] |
| Fin de la respuesta | Algunas respuestas terminan en `</soap:Envelope>\r\n` y otras sin CRLF (ej. `FEXGetLast_ID` sí, `FEXDummy` no) | [VIVO] |
| Operaciones | 19 | [WSDL] |

## Autenticación

`Auth` (`ClsFEXAuthRequest`): `Token` (string, 0..1), `Sign` (string, 0..1), `Cuit` (long, 1..1, "Cuit contribuyente (Representado o Emisora)"). Excepción: en `FEXGetLast_CMP` el `Auth` es de tipo `ClsFEX_LastCMP` y **lleva además `Pto_venta` y `Cbte_Tipo` adentro** (así lo dicen el WSDL y el manual, pág. 36).

Todos los errores de autenticación salen con **HTTP 200**, código **1000** en `FEXErr`, y **sin el bloque de resultado** (`FEXResultAuth`, `FEXResultGet`, etc. no aparecen). Respuestas reales ([VIVO] 2026-10-02):

| Caso | `ErrMsg` literal (código 1000) |
|---|---|
| Sin `Auth`, o `Auth` sin namespace, o elemento raíz con otro nombre (el `FEXGetPARAM_DST_Pais` del manual) | `Usuario no autorizado a realizar esta operacion. ` (con espacio final) |
| `Token`=`abc` | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc` |
| `Token` y `Sign` vacíos | `Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: No validó la firma digital. ` |
| Token base64 de un `<sso>` bien formado, vencido o futuro, con firma falsa | Igual que el anterior: la firma se verifica antes que las fechas |

Junto al error viene `FEXEvents`. En homologación: `<EventCode>103</EventCode><EventMsg>IMPORTANTE: Por motivos de mantenimiento, el servicio estara fuera de linea el domingo 14 de junio desde las 21:00 hs durante aproximadamente 3 horas. Agradecemos tu comprension y pedimos disculpas por las molestias ocasionadas.</EventMsg>` (aviso viejo que quedó). En producción: `<EventCode>0</EventCode><EventMsg>Ok</EventMsg>`.

Respuesta completa observada (homologación, `FEXAuthorize` con token falso). Notar que **no** viene `FEXResultAuth`:

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.fexv1/"><ambiente>Homologacion-Ext - srext</ambiente><fecha>2026-10-02T15:11:28.6588757-03:00</fecha><id>5.0.1.0</id></FEHeaderInfo></soap:Header><soap:Body><FEXAuthorizeResponse xmlns="http://ar.gov.afip.dif.fexv1/"><FEXAuthorizeResult><FEXErr><ErrCode>1000</ErrCode><ErrMsg>Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</ErrMsg></FEXErr><FEXEvents><EventCode>103</EventCode><EventMsg>IMPORTANTE: Por motivos de mantenimiento, el servicio estara fuera de linea el domingo 14 de junio desde las 21:00 hs durante aproximadamente 3 horas. Agradecemos tu comprension y pedimos disculpas por las molestias ocasionadas.</EventMsg></FEXEvents></FEXAuthorizeResult></FEXAuthorizeResponse></soap:Body></soap:Envelope>
```

Fallas de protocolo ([VIVO]):

- `SOAPAction` inexistente: **HTTP 500**, `soap:Fault`, `faultcode` `soap:Client`, `faultstring` `System.Web.Services.Protocols.SoapException: Server did not recognize the value of HTTP Header SOAPAction: http://ar.gov.afip.dif.fexv1/FEXNoExiste.` seguido del stack .NET. Este fault **no** trae `FEHeaderInfo`.
- Valor no numérico en un `long` (`<Cuit>abc</Cuit>`): **HTTP 500**, `soap:Fault` `soap:Client`, `Server was unable to read request. ---> System.InvalidOperationException: There is an error in XML document (1, 220). ---> System.FormatException: Input string was not in a correct format.` + stack. Este fault **sí** trae `FEHeaderInfo`.
- El ruteo es por `SOAPAction`: con el nombre de elemento raíz equivocado responde la operación del `SOAPAction`, pero no lee `Auth`.

El manual también lista 1001 "Cuit solicitante no se encuentra entre sus representados" (CUIT fuera del token) y, para `FEXAuthorize`, 1002 "Debe ser un valor numerico mayor a 0." (CUIT) y 1014 "Debe ser un valor numerico mayor o igual a 0." (`Id`). No se observaron en vivo.

## Operaciones

Orden = orden del esquema ([WSDL]). Respuesta: `<{Op}Response xmlns="http://ar.gov.afip.dif.fexv1/"><{Op}Result>` con el bloque de datos, `FEXErr` y `FEXEvents`. Las 16 operaciones de consulta tienen errores 1000 y 1001 (manual) además de los de infraestructura.

### FEXAuthorize

Autoriza un comprobante y devuelve el CAE ([MAN pág. 11-30]). "Ante cualquier anomalía se retorna un código de error cancelando la ejecución del WS" (pág. 16): o sea, `FEXErr` con **un solo** error.

Request:

Elemento raíz: `FEXAuthorize`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |
| `Cmp` | `ClsFEXRequest` | 0..1 | Comprobante (`ClsFEXRequest`) |
| `Cmp/Id` | `long` | 1..1 | Identificador del requerimiento, N15, elegido por el cliente. Clave de idempotencia (1014: ≥ 0) |
| `Cmp/Fecha_cbte` | `string` | 0..1 | Fecha `yyyymmdd`. Nula o entre N-5 y N+5 (1500). Manual: N |
| `Cmp/Cbte_Tipo` | `short` | 1..1 | 19, 20 o 21 (1530) |
| `Cmp/Punto_vta` | `int` | 1..1 | 1 a 99998, tipo FEEWS (1510) |
| `Cmp/Cbte_nro` | `long` | 1..1 | 1 a 99999999; el próximo a autorizar (1520, 1535) |
| `Cmp/Tipo_expo` | `short` | 1..1 | 1 bienes, 2 servicios, 4 otros (1540) |
| `Cmp/Permiso_existente` | `string` | 0..1 | `S`, `N` o vacío (1550, 1720) |
| `Cmp/Permisos` | `ArrayOfPermiso` | 0..1 | Permisos de embarque. Ver matriz de 1720 |
| `Cmp/Permisos/Permiso` | `Permiso` | 0..n nil | Un permiso |
| `Cmp/Permisos/Permiso/Id_permiso` | `string` | 0..1 | Despacho, C16, formato `99999AAXX999999A` (1740) |
| `Cmp/Permisos/Permiso/Dst_merc` | `int` | 1..1 | País de destino de la mercadería, N3 (1750) |
| `Cmp/Dst_cmp` | `short` | 1..1 | País de destino del comprobante (1560). Manual: Double(N3); WSDL: short |
| `Cmp/Cliente` | `string` | 0..1 | Comprador, C200, obligatorio (1650, 1651) |
| `Cmp/Cuit_pais_cliente` | `long` | 1..1 | CUIT del país destino o del contribuyente, N11 (1570). Al menos uno entre este e `Id_impositivo` (1580) |
| `Cmp/Domicilio_cliente` | `string` | 0..1 | C300, obligatorio (1660, 1661) |
| `Cmp/Id_impositivo` | `string` | 0..1 | Clave tributaria del comprador, C50 |
| `Cmp/Moneda_Id` | `string` | 0..1 | Código de moneda, C3 (1590) |
| `Cmp/Moneda_ctz` | `decimal` | 0..1 | Cotización, N4,6, > 0 (1600). 1 si PES (1601). Obligatoria salvo `CanMisMonExt`=S (1602) |
| `Cmp/CanMisMonExt` | `string` | 0..1 | `S` o `N`: se cancela en la misma moneda extranjera (1603, 1605). Agregado en 3.0.0 |
| `Cmp/Obs_comerciales` | `string` | 0..1 | C4000 (1665) |
| `Cmp/Imp_total` | `decimal` | 1..1 | N13,2, ≥ 0, = suma de `Pro_total_item` (1610) |
| `Cmp/Obs` | `string` | 0..1 | C1000 (1665) |
| `Cmp/Cmps_asoc` | `ArrayOfCmp_asoc` | 0..1 | Comprobantes asociados (ver tabla de asociables) |
| `Cmp/Cmps_asoc/Cmp_asoc` | `Cmp_asoc` | 0..n nil | Un comprobante asociado |
| `Cmp/Cmps_asoc/Cmp_asoc/Cbte_tipo` | `short` | 1..1 | Tipo del asociado: 19, 20, 21, 88, 89, 91, 993, 994 (1680) |
| `Cmp/Cmps_asoc/Cmp_asoc/Cbte_punto_vta` | `int` | 1..1 | 1 a 99998 (1690) |
| `Cmp/Cmps_asoc/Cmp_asoc/Cbte_nro` | `long` | 1..1 | 1 a 999999999 (1700) |
| `Cmp/Cmps_asoc/Cmp_asoc/Cbte_cuit` | `long` | 1..1 | CUIT que emitió el asociado, N11 (2031) |
| `Cmp/Forma_pago` | `string` | 0..1 | C50; obligatorio si `Cbte_Tipo`=19 (1620) |
| `Cmp/Incoterms` | `string` | 0..1 | C3; obligatorio si 19 y Tipo_expo 1 (1640) |
| `Cmp/Incoterms_Ds` | `string` | 0..1 | C20; exige `Incoterms` (1641, 1642) |
| `Cmp/Idioma_cbte` | `short` | 1..1 | 1, 2 o 3 (1630) |
| `Cmp/Items` | `ArrayOfItem` | 0..1 | Ítems: 1 a 9999 (1666) |
| `Cmp/Items/Item` | `Item` | 0..n nil | Un ítem |
| `Cmp/Items/Item/Pro_codigo` | `string` | 0..1 | C50 (1760) |
| `Cmp/Items/Item/Pro_ds` | `string` | 0..1 | C4000, obligatorio (1770) |
| `Cmp/Items/Item/Pro_qty` | `decimal` | 1..1 | N12,6, > 0 (1780, 1813) |
| `Cmp/Items/Item/Pro_umed` | `int` | 1..1 | Unidad de medida (1790). 0, 97, 99 sin cantidad ni precio (1775) |
| `Cmp/Items/Item/Pro_precio_uni` | `decimal` | 1..1 | N12,6, ≥ 0 (1800, 1814) |
| `Cmp/Items/Item/Pro_bonificacion` | `decimal` | 1..1 | N12,6, ≥ 0 y ≤ precio × cantidad (1811, 1812, 1817) |
| `Cmp/Items/Item/Pro_total_item` | `decimal` | 1..1 | N13,2 (1810, 1815, 1816) |
| `Cmp/Opcionales` | `ArrayOfOpcional` | 0..1 | Opcionales (exportación simplificada: 2401, 2402) |
| `Cmp/Opcionales/Opcional` | `Opcional` | 0..n nil | Un opcional |
| `Cmp/Opcionales/Opcional/Id` | `string` | 0..1 | C4, de `FEXGetPARAM_Opcionales` (2003, 2004) |
| `Cmp/Opcionales/Opcional/Valor` | `string` | 0..1 | C250 (2005-2008) |
| `Cmp/Fecha_pago` | `string` | 0..1 | `yyyymmdd` (1671-1674). Agregado en 1.7.0 |
| `Cmp/Actividades` | `ArrayOfActividad` | 0..1 | Actividades (RG 5264/2022) |
| `Cmp/Actividades/Actividad` | `Actividad` | 0..n nil | Una actividad |
| `Cmp/Actividades/Actividad/Id` | `long` | 1..1 | Código de actividad, Long(6), de `FEXGetPARAM_Actividades` (2100-2113) |


Response:

Elemento raíz: `FEXAuthorizeResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXAuthorizeResult` | `FEXResponseAuthorize` | 0..1 | Resultado |
| `FEXAuthorizeResult/FEXResultAuth` | `ClsFEXOutAuthorize` | 0..1 | Resultado de la autorización (no viene si falla el token) |
| `FEXAuthorizeResult/FEXResultAuth/Id` | `long` | 1..1 | Eco del `Id` |
| `FEXAuthorizeResult/FEXResultAuth/Cuit` | `long` | 1..1 | CUIT, N11 |
| `FEXAuthorizeResult/FEXResultAuth/Cbte_tipo` | `short` | 1..1 | Tipo (`Cbte_tipo` con t minúscula en el WSDL) |
| `FEXAuthorizeResult/FEXResultAuth/Punto_vta` | `int` | 1..1 | Punto de venta, N5 |
| `FEXAuthorizeResult/FEXResultAuth/Cbte_nro` | `long` | 1..1 | Número, N8 |
| `FEXAuthorizeResult/FEXResultAuth/Cae` | `string` | 0..1 | CAE, C14 |
| `FEXAuthorizeResult/FEXResultAuth/Fch_venc_Cae` | `string` | 0..1 | Vencimiento del CAE, C8 `yyyymmdd` |
| `FEXAuthorizeResult/FEXResultAuth/Fch_cbte` | `string` | 0..1 | Fecha del comprobante, C8 |
| `FEXAuthorizeResult/FEXResultAuth/Resultado` | `string` | 0..1 | Resultado, C1 |
| `FEXAuthorizeResult/FEXResultAuth/Reproceso` | `string` | 0..1 | `S` si el `Id` ya estaba aprobado, `N` si se procesó ahora |
| `FEXAuthorizeResult/FEXResultAuth/Motivos_Obs` | `string` | 0..1 | Observaciones o motivo de rechazo, C40 (códigos 14-17 y 21) |
| `FEXAuthorizeResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXAuthorizeResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXAuthorizeResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXAuthorizeResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXAuthorizeResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXAuthorizeResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


Diferencias manual / WSDL:

- Respuesta: el WSDL usa **`Cbte_tipo`** (t minúscula) y el orden `Id, Cuit, Cbte_tipo, Punto_vta, Cbte_nro, Cae, Fch_venc_Cae, Fch_cbte, Resultado, Reproceso, Motivos_Obs`. El manual escribe `Cbte_Tipo` y pone `Cae` después de `Cuit`.
- `Dst_cmp` es `short` en el WSDL; el manual dice Double(N3).
- El manual marca `Fecha_cbte` como no obligatorio (si no viene, toma la fecha de envío: validación 1500 "Nulo, o comprendido entre N-5 y N+5").
- Errata del manual: el cuadro de `Cmp` tiene un `Id_permisos` donde el WSDL dice `Id_permiso`.
- `Pro_qty`, `Pro_precio_uni` y `Pro_bonificacion` son opcionales en el manual pero `1..1` (decimal no anulable) en el WSDL. Si no vienen, el deserializador .NET los deja en 0, que es justo lo que piden 1775 para las unidades 0, 97 y 99. Lo mismo con `Cuit_pais_cliente` y `Dst_merc`.

Ejemplo del manual (pág. 28-29, request 3, con bonificación por ítem). Es el único con envelope completo y namespace bien puesto:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.fexv1/">
  <soapenv:Header/>
  <soapenv:Body>
    <ar:FEXAuthorize>
      <ar:Auth><ar:Token>Un String</ar:Token><ar:Sign>Un String</ar:Sign><ar:Cuit>66666666666</ar:Cuit></ar:Auth>
      <ar:Cmp>
        <ar:Id>4502</ar:Id><ar:Fecha_cbte>20110102</ar:Fecha_cbte><ar:Cbte_Tipo>19</ar:Cbte_Tipo>
        <ar:Punto_vta>99998</ar:Punto_vta><ar:Cbte_nro>2</ar:Cbte_nro><ar:Tipo_expo>1</ar:Tipo_expo>
        <ar:Permiso_existente>N</ar:Permiso_existente><ar:Dst_cmp>220</ar:Dst_cmp>
        <ar:Cliente>Denominacion del Cliente</ar:Cliente><ar:Cuit_pais_cliente>55000000050</ar:Cuit_pais_cliente>
        <ar:Domicilio_cliente>Domicilio del Cliente</ar:Domicilio_cliente><ar:Id_impositivo>Id9999/99</ar:Id_impositivo>
        <ar:Moneda_Id>PES</ar:Moneda_Id><ar:Moneda_ctz>1</ar:Moneda_ctz>
        <ar:Obs_comerciales>Texto libre 1</ar:Obs_comerciales><ar:Imp_total>1209.08</ar:Imp_total><ar:Obs>Texto libre 2</ar:Obs>
        <ar:Forma_pago>Efectivo</ar:Forma_pago><ar:Incoterms>FOB</ar:Incoterms><ar:Incoterms_Ds>Descripción Incoter</ar:Incoterms_Ds>
        <ar:Idioma_cbte>1</ar:Idioma_cbte>
        <ar:Items>
          <ar:Item>
            <ar:Pro_codigo>Cod0001</ar:Pro_codigo><ar:Pro_ds>Descripcion del Producto</ar:Pro_ds>
            <ar:Pro_qty>100.555444</ar:Pro_qty><ar:Pro_umed>7</ar:Pro_umed><ar:Pro_precio_uni>12.123456</ar:Pro_precio_uni>
            <ar:Pro_bonificacion>10</ar:Pro_bonificacion><ar:Pro_total_item>1209.08</ar:Pro_total_item>
          </ar:Item>
        </ar:Items>
      </ar:Cmp>
    </ar:FEXAuthorize>
  </soapenv:Body>
</soapenv:Envelope>
```

Cuenta de control: 100.555444 × 12.123456 − 10 = 1209.0795… → 1209.08 (validación 1815).

El request 4 (pág. 29-30) es igual con un segundo ítem de descuento global: `Pro_umed`=99, sin cantidad ni precio, `Pro_total_item`=-12.09, e `Imp_total`=1196.99. Los requests 1 y 2 (pág. 26-28) son `ClsFEXRequest` sueltos, sin envelope, con erratas (`<Incoterms>/Incoterms>`, `Id_permiso` vacío con `Permiso_existente`=S). El manual **no trae ningún ejemplo de respuesta** de `FEXAuthorize`.

### FEXGetCMP

Devuelve un comprobante ya autorizado ([MAN pág. 30-34]).

Request:

Elemento raíz: `FEXGetCMP`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |
| `Cmp` | `ClsFEXGetCMP` | 0..1 | Clave del comprobante (`ClsFEXGetCMP`) |
| `Cmp/Cbte_tipo` | `short` | 1..1 | Tipo, N3 (**`Cbte_tipo`**; el manual escribe `Cbte_Tipo`) |
| `Cmp/Punto_vta` | `int` | 1..1 | Punto de venta, N5 |
| `Cmp/Cbte_nro` | `long` | 1..1 | Número, N8 |


Response:

Elemento raíz: `FEXGetCMPResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetCMPResult` | `FEXGetCMPResponse` | 0..1 | Resultado |
| `FEXGetCMPResult/FEXResultGet` | `ClsFEXGetCMPR` | 0..1 | Comprobante autorizado (`ClsFEXGetCMPR`): los datos enviados más CAE |
| `FEXGetCMPResult/FEXResultGet/Id` | `long` | 1..1 | Eco del `Id` del requerimiento, N15 |
| `FEXGetCMPResult/FEXResultGet/Fecha_cbte` | `string` | 0..1 | Fecha `yyyymmdd` |
| `FEXGetCMPResult/FEXResultGet/Cbte_tipo` | `short` | 1..1 | Tipo de comprobante, N2 (19, 20, 21) |
| `FEXGetCMPResult/FEXResultGet/Punto_vta` | `int` | 1..1 | Punto de venta, N5 |
| `FEXGetCMPResult/FEXResultGet/Cbte_nro` | `long` | 1..1 | Número, N8 |
| `FEXGetCMPResult/FEXResultGet/Tipo_expo` | `short` | 1..1 | 1 bienes, 2 servicios, 4 otros (1540) |
| `FEXGetCMPResult/FEXResultGet/Permiso_existente` | `string` | 0..1 | `S`, `N` o vacío (1550, 1720) |
| `FEXGetCMPResult/FEXResultGet/Permisos` | `ArrayOfPermiso` | 0..1 | Permisos de embarque. Ver matriz de 1720 |
| `FEXGetCMPResult/FEXResultGet/Permisos/Permiso` | `Permiso` | 0..n nil | Un permiso |
| `FEXGetCMPResult/FEXResultGet/Permisos/Permiso/Id_permiso` | `string` | 0..1 | Despacho, C16, formato `99999AAXX999999A` (1740) |
| `FEXGetCMPResult/FEXResultGet/Permisos/Permiso/Dst_merc` | `int` | 1..1 | País de destino de la mercadería, N3 (1750) |
| `FEXGetCMPResult/FEXResultGet/Dst_cmp` | `short` | 1..1 | País de destino del comprobante (1560). Manual: Double(N3); WSDL: short |
| `FEXGetCMPResult/FEXResultGet/Cliente` | `string` | 0..1 | Comprador, C200, obligatorio (1650, 1651) |
| `FEXGetCMPResult/FEXResultGet/Cuit_pais_cliente` | `long` | 1..1 | CUIT del país destino o del contribuyente, N11 (1570). Al menos uno entre este e `Id_impositivo` (1580) |
| `FEXGetCMPResult/FEXResultGet/Domicilio_cliente` | `string` | 0..1 | C300, obligatorio (1660, 1661) |
| `FEXGetCMPResult/FEXResultGet/Id_impositivo` | `string` | 0..1 | Clave tributaria del comprador, C50 |
| `FEXGetCMPResult/FEXResultGet/Moneda_Id` | `string` | 0..1 | Código de moneda, C3 (1590) |
| `FEXGetCMPResult/FEXResultGet/Moneda_ctz` | `decimal` | 1..1 | Cotización, N4,6, > 0 (1600). 1 si PES (1601). Obligatoria salvo `CanMisMonExt`=S (1602) |
| `FEXGetCMPResult/FEXResultGet/CanMisMonExt` | `string` | 0..1 | `S` o `N`: se cancela en la misma moneda extranjera (1603, 1605). Agregado en 3.0.0 |
| `FEXGetCMPResult/FEXResultGet/Obs_comerciales` | `string` | 0..1 | C4000 (1665) |
| `FEXGetCMPResult/FEXResultGet/Imp_total` | `decimal` | 1..1 | N13,2, ≥ 0, = suma de `Pro_total_item` (1610) |
| `FEXGetCMPResult/FEXResultGet/Obs` | `string` | 0..1 | C1000 (1665) |
| `FEXGetCMPResult/FEXResultGet/Cmps_asoc` | `ArrayOfCmp_asoc` | 0..1 | Comprobantes asociados (ver tabla de asociables) |
| `FEXGetCMPResult/FEXResultGet/Cmps_asoc/Cmp_asoc` | `Cmp_asoc` | 0..n nil | Un comprobante asociado |
| `FEXGetCMPResult/FEXResultGet/Cmps_asoc/Cmp_asoc/Cbte_tipo` | `short` | 1..1 | Tipo del asociado: 19, 20, 21, 88, 89, 91, 993, 994 (1680) |
| `FEXGetCMPResult/FEXResultGet/Cmps_asoc/Cmp_asoc/Cbte_punto_vta` | `int` | 1..1 | 1 a 99998 (1690) |
| `FEXGetCMPResult/FEXResultGet/Cmps_asoc/Cmp_asoc/Cbte_nro` | `long` | 1..1 | 1 a 999999999 (1700) |
| `FEXGetCMPResult/FEXResultGet/Cmps_asoc/Cmp_asoc/Cbte_cuit` | `long` | 1..1 | CUIT que emitió el asociado, N11 (2031) |
| `FEXGetCMPResult/FEXResultGet/Forma_pago` | `string` | 0..1 | C50; obligatorio si `Cbte_Tipo`=19 (1620) |
| `FEXGetCMPResult/FEXResultGet/Incoterms` | `string` | 0..1 | C3; obligatorio si 19 y Tipo_expo 1 (1640) |
| `FEXGetCMPResult/FEXResultGet/Incoterms_Ds` | `string` | 0..1 | C20; exige `Incoterms` (1641, 1642) |
| `FEXGetCMPResult/FEXResultGet/Idioma_cbte` | `short` | 1..1 | 1, 2 o 3 (1630) |
| `FEXGetCMPResult/FEXResultGet/Items` | `ArrayOfItem` | 0..1 | Ítems: 1 a 9999 (1666) |
| `FEXGetCMPResult/FEXResultGet/Items/Item` | `Item` | 0..n nil | Un ítem |
| `FEXGetCMPResult/FEXResultGet/Items/Item/Pro_codigo` | `string` | 0..1 | C50 (1760) |
| `FEXGetCMPResult/FEXResultGet/Items/Item/Pro_ds` | `string` | 0..1 | C4000, obligatorio (1770) |
| `FEXGetCMPResult/FEXResultGet/Items/Item/Pro_qty` | `decimal` | 1..1 | N12,6, > 0 (1780, 1813) |
| `FEXGetCMPResult/FEXResultGet/Items/Item/Pro_umed` | `int` | 1..1 | Unidad de medida (1790). 0, 97, 99 sin cantidad ni precio (1775) |
| `FEXGetCMPResult/FEXResultGet/Items/Item/Pro_precio_uni` | `decimal` | 1..1 | N12,6, ≥ 0 (1800, 1814) |
| `FEXGetCMPResult/FEXResultGet/Items/Item/Pro_bonificacion` | `decimal` | 1..1 | N12,6, ≥ 0 y ≤ precio × cantidad (1811, 1812, 1817) |
| `FEXGetCMPResult/FEXResultGet/Items/Item/Pro_total_item` | `decimal` | 1..1 | N13,2 (1810, 1815, 1816) |
| `FEXGetCMPResult/FEXResultGet/Fecha_cbte_cae` | `string` | 0..1 | Fecha de otorgamiento del CAE (interpretación por el nombre; el manual no lo describe) |
| `FEXGetCMPResult/FEXResultGet/Fch_venc_Cae` | `string` | 0..1 | Vencimiento del CAE, C8 `yyyymmdd` |
| `FEXGetCMPResult/FEXResultGet/Cae` | `string` | 0..1 | CAE, C14 |
| `FEXGetCMPResult/FEXResultGet/Resultado` | `string` | 0..1 | Resultado, C1 |
| `FEXGetCMPResult/FEXResultGet/Motivos_Obs` | `string` | 0..1 | Observaciones o motivo de rechazo, C40 (códigos 14-17 y 21) |
| `FEXGetCMPResult/FEXResultGet/Opcionales` | `ArrayOfOpcional` | 0..1 | Opcionales (exportación simplificada: 2401, 2402) |
| `FEXGetCMPResult/FEXResultGet/Opcionales/Opcional` | `Opcional` | 0..n nil | Un opcional |
| `FEXGetCMPResult/FEXResultGet/Opcionales/Opcional/Id` | `string` | 0..1 | C4, de `FEXGetPARAM_Opcionales` (2003, 2004) |
| `FEXGetCMPResult/FEXResultGet/Opcionales/Opcional/Valor` | `string` | 0..1 | C250 (2005-2008) |
| `FEXGetCMPResult/FEXResultGet/Fecha_pago` | `string` | 0..1 | `yyyymmdd` (1671-1674). Agregado en 1.7.0 |
| `FEXGetCMPResult/FEXResultGet/Actividades` | `ArrayOfActividad` | 0..1 | Actividades (RG 5264/2022) |
| `FEXGetCMPResult/FEXResultGet/Actividades/Actividad` | `Actividad` | 0..n nil | Una actividad |
| `FEXGetCMPResult/FEXResultGet/Actividades/Actividad/Id` | `long` | 1..1 | Código de actividad, Long(6), de `FEXGetPARAM_Actividades` (2100-2113) |
| `FEXGetCMPResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetCMPResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetCMPResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetCMPResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetCMPResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetCMPResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


Trampas:

- El request del manual usa `<Cbte_Tipo>`; **el WSDL dice `Cbte_tipo`**. El deserializador .NET distingue mayúsculas: con `Cbte_Tipo` el valor no se lee y queda en 0 (comportamiento de .NET con elementos desconocidos, verificado en wsfev1; en wsfexv1 NO VERIFICADO con token válido).
- En el WSDL `Opcionales` va **después** de `Motivos_Obs`; el manual lo pone antes de `Fecha_cbte_cae`.
- El manual muestra `Permisos/Permiso xsi:nil="true"` y `Cmps_asoc/Cmp_asoc xsi:nil="true"` en el esquema de respuesta.
- `Moneda_ctz` es `1..1` en la respuesta (en el request es `0..1`).

Error propio: 1020 "Comprobante inexistente" (pág. 34).

### FEXGetLast_ID

Devuelve "el último id de requerimiento para la cuit enviada" ([MAN pág. 34-36]); en 1.5 lo describe como "el último `<Id>` (el máximo) recibido".

Request:

Elemento raíz: `FEXGetLast_ID`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetLast_IDResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetLast_IDResult` | `FEXResponse_LastID` | 0..1 | Resultado |
| `FEXGetLast_IDResult/FEXResultGet` | `ClsFEXResponse_LastID` | 0..1 | Resultado |
| `FEXGetLast_IDResult/FEXResultGet/Id` | `long` | 1..1 | Último `Id` (el máximo) recibido para la CUIT |
| `FEXGetLast_IDResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetLast_IDResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetLast_IDResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetLast_IDResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetLast_IDResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetLast_IDResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


### FEXGetLast_CMP

Último `Cbte_nro` autorizado para un punto de venta y tipo ([MAN pág. 36-37]). Los parámetros van **dentro de `Auth`**.

Request:

Elemento raíz: `FEXGetLast_CMP`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEX_LastCMP` | 0..1 | Credenciales **más** los parámetros de la consulta (`ClsFEX_LastCMP`) |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |
| `Auth/Pto_venta` | `int` | 1..1 | Punto de venta a consultar (1607 si no es FEEWS) |
| `Auth/Cbte_Tipo` | `short` | 1..1 | Tipo de comprobante: 19, 20 o 21 (1606) |


Response:

Elemento raíz: `FEXGetLast_CMPResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetLast_CMPResult` | `FEXResponseLast_CMP` | 0..1 | Resultado |
| `FEXGetLast_CMPResult/FEXResult_LastCMP` | `ClsFEX_LastCMP_Response` | 0..1 | Último comprobante |
| `FEXGetLast_CMPResult/FEXResult_LastCMP/Cbte_nro` | `long` | 1..1 | Número del último autorizado, N8 |
| `FEXGetLast_CMPResult/FEXResult_LastCMP/Cbte_fecha` | `string` | 0..1 | Fecha del último, C8 `yyyymmdd` |
| `FEXGetLast_CMPResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetLast_CMPResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetLast_CMPResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetLast_CMPResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetLast_CMPResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetLast_CMPResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


Errores propios (versión 3.1.0): 1606 "Campo Cbte_Tipo no se corresponde con alguno de los comprobantes habilitados. Recuerde que los valores son 19, 20 o 21" y 1607 "Campo Pto_venta no es valido o no esta dado de alta como punto de venta de 'Comprobantes de Exportación - Web Services'". Qué devuelve cuando todavía no hay comprobantes (¿`Cbte_nro`=0?): NO VERIFICADO.

### FEXGetPARAM_Cbte_Tipo

Tipos de comprobante ([MAN pág. 39-40]).

Request:

Elemento raíz: `FEXGetPARAM_Cbte_Tipo`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_Cbte_TipoResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_Cbte_TipoResult` | `FEXResponse_Cbte_Tipo` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_Cbte_TipoResult/FEXResultGet` | `ArrayOfClsFEXResponse_Cbte_Tipo` | 0..1 | Lista de valores |
| `FEXGetPARAM_Cbte_TipoResult/FEXResultGet/ClsFEXResponse_Cbte_Tipo` | `ClsFEXResponse_Cbte_Tipo` | 0..n nil | Un tipo |
| `FEXGetPARAM_Cbte_TipoResult/FEXResultGet/ClsFEXResponse_Cbte_Tipo/Cbte_Id` | `short` | 1..1 | Código, N2 |
| `FEXGetPARAM_Cbte_TipoResult/FEXResultGet/ClsFEXResponse_Cbte_Tipo/Cbte_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_Cbte_TipoResult/FEXResultGet/ClsFEXResponse_Cbte_Tipo/Cbte_vig_desde` | `string` | 0..1 | Vigencia desde, C8 |
| `FEXGetPARAM_Cbte_TipoResult/FEXResultGet/ClsFEXResponse_Cbte_Tipo/Cbte_vig_hasta` | `string` | 0..1 | Vigencia hasta, C8 |
| `FEXGetPARAM_Cbte_TipoResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_Cbte_TipoResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_Cbte_TipoResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_Cbte_TipoResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_Cbte_TipoResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_Cbte_TipoResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


### FEXGetPARAM_Tipo_Expo

Tipos de exportación ([MAN pág. 40-42]).

Request:

Elemento raíz: `FEXGetPARAM_Tipo_Expo`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_Tipo_ExpoResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_Tipo_ExpoResult` | `FEXResponse_Tex` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_Tipo_ExpoResult/FEXResultGet` | `ArrayOfClsFEXResponse_Tex` | 0..1 | Lista de valores |
| `FEXGetPARAM_Tipo_ExpoResult/FEXResultGet/ClsFEXResponse_Tex` | `ClsFEXResponse_Tex` | 0..n nil | Un tipo de exportación |
| `FEXGetPARAM_Tipo_ExpoResult/FEXResultGet/ClsFEXResponse_Tex/Tex_Id` | `short` | 1..1 | Código, N2 |
| `FEXGetPARAM_Tipo_ExpoResult/FEXResultGet/ClsFEXResponse_Tex/Tex_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_Tipo_ExpoResult/FEXResultGet/ClsFEXResponse_Tex/Tex_vig_desde` | `string` | 0..1 | Vigencia desde, C8 |
| `FEXGetPARAM_Tipo_ExpoResult/FEXResultGet/ClsFEXResponse_Tex/Tex_vig_hasta` | `string` | 0..1 | Vigencia hasta, C8 |
| `FEXGetPARAM_Tipo_ExpoResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_Tipo_ExpoResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_Tipo_ExpoResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_Tipo_ExpoResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_Tipo_ExpoResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_Tipo_ExpoResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


### FEXGetPARAM_Incoterms

Incoterms ([MAN pág. 46-48]).

Request:

Elemento raíz: `FEXGetPARAM_Incoterms`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_IncotermsResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_IncotermsResult` | `FEXResponse_Inc` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_IncotermsResult/FEXResultGet` | `ArrayOfClsFEXResponse_Inc` | 0..1 | Lista de valores |
| `FEXGetPARAM_IncotermsResult/FEXResultGet/ClsFEXResponse_Inc` | `ClsFEXResponse_Inc` | 0..n nil | Un incoterm |
| `FEXGetPARAM_IncotermsResult/FEXResultGet/ClsFEXResponse_Inc/Inc_Id` | `string` | 0..1 | Código, C3 |
| `FEXGetPARAM_IncotermsResult/FEXResultGet/ClsFEXResponse_Inc/Inc_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_IncotermsResult/FEXResultGet/ClsFEXResponse_Inc/Inc_vig_desde` | `string` | 0..1 | Vigencia desde, C8 |
| `FEXGetPARAM_IncotermsResult/FEXResultGet/ClsFEXResponse_Inc/Inc_vig_hasta` | `string` | 0..1 | Vigencia hasta, C8 |
| `FEXGetPARAM_IncotermsResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_IncotermsResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_IncotermsResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_IncotermsResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_IncotermsResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_IncotermsResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


El manual dice `Inc_Id` String(C3) en la tabla y `short` en el esquema; el WSDL dice `string`.

### FEXGetPARAM_Idiomas

Idiomas del comprobante ([MAN pág. 43-45]).

Request:

Elemento raíz: `FEXGetPARAM_Idiomas`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_IdiomasResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_IdiomasResult` | `FEXResponse_Idi` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_IdiomasResult/FEXResultGet` | `ArrayOfClsFEXResponse_Idi` | 0..1 | Lista de valores |
| `FEXGetPARAM_IdiomasResult/FEXResultGet/ClsFEXResponse_Idi` | `ClsFEXResponse_Idi` | 0..n nil | Un idioma |
| `FEXGetPARAM_IdiomasResult/FEXResultGet/ClsFEXResponse_Idi/Idi_Id` | `short` | 1..1 | Código, N2 |
| `FEXGetPARAM_IdiomasResult/FEXResultGet/ClsFEXResponse_Idi/Idi_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_IdiomasResult/FEXResultGet/ClsFEXResponse_Idi/Idi_vig_desde` | `string` | 0..1 | Vigencia desde, C8 |
| `FEXGetPARAM_IdiomasResult/FEXResultGet/ClsFEXResponse_Idi/Idi_vig_hasta` | `string` | 0..1 | Vigencia hasta, C8 |
| `FEXGetPARAM_IdiomasResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_IdiomasResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_IdiomasResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_IdiomasResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_IdiomasResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_IdiomasResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


El esquema del manual escribe `idi_Ds`, `idi_vig_desde`; el WSDL, `Idi_Ds`, `Idi_vig_desde`.

### FEXGetPARAM_UMed

Unidades de medida ([MAN pág. 42-43]). El manual nombra el request `FEXGetPARAM_Umed`; en el WSDL es **`FEXGetPARAM_UMed`**.

Request:

Elemento raíz: `FEXGetPARAM_UMed`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_UMedResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_UMedResult` | `FEXResponse_Umed` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_UMedResult/FEXResultGet` | `ArrayOfClsFEXResponse_UMed` | 0..1 | Lista de valores |
| `FEXGetPARAM_UMedResult/FEXResultGet/ClsFEXResponse_UMed` | `ClsFEXResponse_UMed` | 0..n nil | Una unidad |
| `FEXGetPARAM_UMedResult/FEXResultGet/ClsFEXResponse_UMed/Umed_Id` | `short` | 1..1 | Código, N2 |
| `FEXGetPARAM_UMedResult/FEXResultGet/ClsFEXResponse_UMed/Umed_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_UMedResult/FEXResultGet/ClsFEXResponse_UMed/Umed_vig_desde` | `string` | 0..1 | Vigencia desde, C8 |
| `FEXGetPARAM_UMedResult/FEXResultGet/ClsFEXResponse_UMed/Umed_vig_hasta` | `string` | 0..1 | Vigencia hasta, C8 |
| `FEXGetPARAM_UMedResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_UMedResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_UMedResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_UMedResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_UMedResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_UMedResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


### FEXGetPARAM_DST_pais

Países de destino ([MAN pág. 45-46]). El manual nombra el request `FEXGetPARAM_DST_Pais`; el WSDL, **`FEXGetPARAM_DST_pais`**. Con el nombre del manual el servicio responde (rutea por `SOAPAction`) pero no lee `Auth`: error 1000 "Usuario no autorizado a realizar esta operacion. " ([VIVO]).

Request:

Elemento raíz: `FEXGetPARAM_DST_pais`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_DST_paisResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_DST_paisResult` | `FEXResponse_DST_pais` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_DST_paisResult/FEXResultGet` | `ArrayOfClsFEXResponse_DST_pais` | 0..1 | Lista de valores |
| `FEXGetPARAM_DST_paisResult/FEXResultGet/ClsFEXResponse_DST_pais` | `ClsFEXResponse_DST_pais` | 0..n nil | Un país |
| `FEXGetPARAM_DST_paisResult/FEXResultGet/ClsFEXResponse_DST_pais/DST_Codigo` | `string` | 0..1 | Código de país, C3 |
| `FEXGetPARAM_DST_paisResult/FEXResultGet/ClsFEXResponse_DST_pais/DST_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_DST_paisResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_DST_paisResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_DST_paisResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_DST_paisResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_DST_paisResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_DST_paisResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


### FEXGetPARAM_DST_CUIT

CUIT genéricas de países (para `Cuit_pais_cliente`) ([MAN pág. 48-49]).

Request:

Elemento raíz: `FEXGetPARAM_DST_CUIT`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_DST_CUITResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_DST_CUITResult` | `FEXResponse_DST_cuit` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_DST_CUITResult/FEXResultGet` | `ArrayOfClsFEXResponse_DST_cuit` | 0..1 | Lista de valores |
| `FEXGetPARAM_DST_CUITResult/FEXResultGet/ClsFEXResponse_DST_cuit` | `ClsFEXResponse_DST_cuit` | 0..n nil | Una CUIT de país |
| `FEXGetPARAM_DST_CUITResult/FEXResultGet/ClsFEXResponse_DST_cuit/DST_CUIT` | `long` | 1..1 | CUIT de país (long en el WSDL, C11 en el manual) |
| `FEXGetPARAM_DST_CUITResult/FEXResultGet/ClsFEXResponse_DST_cuit/DST_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_DST_CUITResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_DST_CUITResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_DST_CUITResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_DST_CUITResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_DST_CUITResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_DST_CUITResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


`DST_CUIT` es `long` en el WSDL; el manual dice String(C11).

### FEXGetPARAM_MON

Monedas ([MAN pág. 37-39]).

Request:

Elemento raíz: `FEXGetPARAM_MON`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_MONResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_MONResult` | `FEXResponse_Mon` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_MONResult/FEXResultGet` | `ArrayOfClsFEXResponse_Mon` | 0..1 | Lista de valores |
| `FEXGetPARAM_MONResult/FEXResultGet/ClsFEXResponse_Mon` | `ClsFEXResponse_Mon` | 0..n nil | Una moneda |
| `FEXGetPARAM_MONResult/FEXResultGet/ClsFEXResponse_Mon/Mon_Id` | `string` | 0..1 | Código de moneda, C3 |
| `FEXGetPARAM_MONResult/FEXResultGet/ClsFEXResponse_Mon/Mon_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_MONResult/FEXResultGet/ClsFEXResponse_Mon/Mon_vig_desde` | `string` | 0..1 | Vigencia desde, C8 |
| `FEXGetPARAM_MONResult/FEXResultGet/ClsFEXResponse_Mon/Mon_vig_hasta` | `string` | 0..1 | Vigencia hasta, C8 |
| `FEXGetPARAM_MONResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_MONResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_MONResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_MONResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_MONResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_MONResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


### FEXGetPARAM_MON_CON_COTIZACION

Monedas con cotización aduanera a una fecha ([MAN pág. 57-58]). Es la lista a usar para comprobantes de servicios (validación 1590).

Request:

Elemento raíz: `FEXGetPARAM_MON_CON_COTIZACION`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |
| `Fecha_CTZ` | `string` | 0..1 | Día hábil a consultar, `yyyymmdd`, obligatorio (2054). El manual lo llama `Fecha` |


Response:

Elemento raíz: `FEXGetPARAM_MON_CON_COTIZACIONResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_MON_CON_COTIZACIONResult` | `FEXResponse_Mon_CON_COTIZACION` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXResultGet` | `ArrayOfClsFEXResponse_Mon_CON_Cotizacion` | 0..1 | Lista de valores |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXResultGet/ClsFEXResponse_Mon_CON_Cotizacion` | `ClsFEXResponse_Mon_CON_Cotizacion` | 0..n nil | Una moneda con cotización |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXResultGet/ClsFEXResponse_Mon_CON_Cotizacion/Mon_Id` | `string` | 0..1 | Código de moneda, C3 |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXResultGet/ClsFEXResponse_Mon_CON_Cotizacion/Mon_Ds` | `string` | 0..1 | Descripción, C250 |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXResultGet/ClsFEXResponse_Mon_CON_Cotizacion/Mon_ctz` | `string` | 0..1 | Cotización (string en el WSDL; Decimal N12,6 en el manual) |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXResultGet/ClsFEXResponse_Mon_CON_Cotizacion/Fecha_ctz` | `string` | 0..1 | Fecha de la cotización, C8 |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_MON_CON_COTIZACIONResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


Trampas: el manual llama al parámetro `<Fecha>` (yyyymmdd); **el WSDL lo llama `Fecha_CTZ`**, que es el nombre que usa el error 2054 ("El campo Fecha_CTZ es de integración obligatoria y debe tener el siguiente formato: YYYYMMDD"). En la respuesta el WSDL agrega `Mon_Ds`, tipa `Mon_ctz` como **string** y llama `Fecha_ctz` a la fecha; el manual muestra `Mon_Id, Mon_ctz (decimal), Mon_fecha`.

### FEXGetPARAM_Ctz

Cotización de una moneda ([MAN pág. 49-51]). "Retorna la última cotización de la base de datos aduanera de la moneda ingresada. Este valor es orientativo."

Request:

Elemento raíz: `FEXGetPARAM_Ctz`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |
| `Mon_id` | `string` | 0..1 | Moneda a cotizar, obligatorio |
| `FchCotiz` | `string` | 0..1 | Fecha a consultar, opcional, formato `YYYY-MM-DD` (1003) |


Response:

Elemento raíz: `FEXGetPARAM_CtzResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_CtzResult` | `FEXResponse_Ctz` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_CtzResult/FEXResultGet` | `ClsFEXResponse_Ctz` | 0..1 | Cotización |
| `FEXGetPARAM_CtzResult/FEXResultGet/Mon_ctz` | `decimal` | 1..1 | Cotización, N12,6 ("orientativo") |
| `FEXGetPARAM_CtzResult/FEXResultGet/Mon_fecha` | `string` | 0..1 | Fecha de la cotización, C8 |
| `FEXGetPARAM_CtzResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_CtzResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_CtzResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_CtzResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_CtzResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_CtzResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


`FchCotiz` es opcional (agregado en 3.0.0). Ojo con el formato: el error 1003 pide **`YYYY-MM-DD`** (con guiones), distinto del `yyyymmdd` del resto del servicio.

### FEXGetPARAM_PtoVenta

Puntos de venta "Comprobantes de Exportación - Web Services" del emisor ([MAN pág. 51-52]).

Request:

Elemento raíz: `FEXGetPARAM_PtoVenta`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_PtoVentaResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_PtoVentaResult` | `FEXResponse_PtoVenta` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_PtoVentaResult/FEXResultGet` | `ArrayOfClsFEXResponse_PtoVenta` | 0..1 | Lista de valores |
| `FEXGetPARAM_PtoVentaResult/FEXResultGet/ClsFEXResponse_PtoVenta` | `ClsFEXResponse_PtoVenta` | 0..n nil | Un punto de venta |
| `FEXGetPARAM_PtoVentaResult/FEXResultGet/ClsFEXResponse_PtoVenta/Pve_Nro` | `int` | 1..1 | Número (el manual dice N4; el resto del servicio admite 5) |
| `FEXGetPARAM_PtoVentaResult/FEXResultGet/ClsFEXResponse_PtoVenta/Pve_Bloqueado` | `string` | 0..1 | `S` o `N` |
| `FEXGetPARAM_PtoVentaResult/FEXResultGet/ClsFEXResponse_PtoVenta/Pve_FchBaja` | `string` | 0..1 | Fecha de baja, C8, si corresponde |
| `FEXGetPARAM_PtoVentaResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_PtoVentaResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_PtoVentaResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_PtoVentaResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_PtoVentaResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_PtoVentaResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


### FEXGetPARAM_Opcionales

Tipos de datos opcionales ([MAN pág. 52-54]).

Request:

Elemento raíz: `FEXGetPARAM_Opcionales`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_OpcionalesResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_OpcionalesResult` | `FEXResponse_Opc` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_OpcionalesResult/FEXResultGet` | `ArrayOfClsFEXResponse_Opc` | 0..1 | Lista de valores |
| `FEXGetPARAM_OpcionalesResult/FEXResultGet/ClsFEXResponse_Opc` | `ClsFEXResponse_Opc` | 0..n nil | Un opcional |
| `FEXGetPARAM_OpcionalesResult/FEXResultGet/ClsFEXResponse_Opc/Opc_Id` | `short` | 1..1 | Código (short en el WSDL) |
| `FEXGetPARAM_OpcionalesResult/FEXResultGet/ClsFEXResponse_Opc/Opc_Ds` | `string` | 0..1 | Descripción |
| `FEXGetPARAM_OpcionalesResult/FEXResultGet/ClsFEXResponse_Opc/Opc_vig_desde` | `string` | 0..1 | Vigencia desde |
| `FEXGetPARAM_OpcionalesResult/FEXResultGet/ClsFEXResponse_Opc/Opc_vig_hasta` | `string` | 0..1 | Vigencia hasta |
| `FEXGetPARAM_OpcionalesResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_OpcionalesResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_OpcionalesResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_OpcionalesResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_OpcionalesResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_OpcionalesResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


El manual muestra `Opc_Id` string y en la tabla los llama `Id`, `Desc`, `FchDesde`, `FchHasta`; el WSDL usa `Opc_Id` **short**, `Opc_Ds`, `Opc_vig_desde`, `Opc_vig_hasta`.

### FEXGetPARAM_Actividades

Actividades vigentes del emisor ([MAN pág. 58-59]; agregado en 2.0.0 por RG 5264/2022).

Request:

Elemento raíz: `FEXGetPARAM_Actividades`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |


Response:

Elemento raíz: `FEXGetPARAM_ActividadesResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXGetPARAM_ActividadesResult` | `FEXResponse_Actividades` | 0..1 | Contenedor del resultado |
| `FEXGetPARAM_ActividadesResult/FEXResultGet` | `ArrayOfClsFEXResponse_ActividadTipo` | 0..1 | Lista de valores |
| `FEXGetPARAM_ActividadesResult/FEXResultGet/ClsFEXResponse_ActividadTipo` | `ClsFEXResponse_ActividadTipo` | 0..n nil | Una actividad |
| `FEXGetPARAM_ActividadesResult/FEXResultGet/ClsFEXResponse_ActividadTipo/Id` | `long` | 1..1 | Código de actividad |
| `FEXGetPARAM_ActividadesResult/FEXResultGet/ClsFEXResponse_ActividadTipo/Orden` | `short` | 1..1 | Orden de la actividad |
| `FEXGetPARAM_ActividadesResult/FEXResultGet/ClsFEXResponse_ActividadTipo/Desc` | `string` | 0..1 | Descripción, C180 |
| `FEXGetPARAM_ActividadesResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXGetPARAM_ActividadesResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXGetPARAM_ActividadesResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXGetPARAM_ActividadesResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXGetPARAM_ActividadesResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXGetPARAM_ActividadesResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


El manual dice `Id` string en el esquema y Long en la tabla; el WSDL, `long`. `Desc` String(180).

### FEXCheck_Permiso

Verifica que un permiso de embarque con un país de destino exista en las bases aduaneras ([MAN pág. 54-55]).

Request:

Elemento raíz: `FEXCheck_Permiso`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `Auth` | `ClsFEXAuthRequest` | 0..1 | Credenciales: Token, Sign, Cuit |
| `Auth/Token` | `string` | 0..1 | Token del TA de WSAA (service `wsfex`) |
| `Auth/Sign` | `string` | 0..1 | Firma del TA |
| `Auth/Cuit` | `long` | 1..1 | CUIT representada o emisora (N11). 1002 si no es > 0 |
| `ID_Permiso` | `string` | 0..1 | Código de permiso de embarque, obligatorio |
| `Dst_merc` | `int` | 1..1 | País de destino, obligatorio |


Response:

Elemento raíz: `FEXCheck_PermisoResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXCheck_PermisoResult` | `FEXResponse_CheckPermiso` | 0..1 | Contenedor del resultado |
| `FEXCheck_PermisoResult/FEXResultGet` | `ClsFEXResponse_CheckPermiso` | 0..1 | Resultado |
| `FEXCheck_PermisoResult/FEXResultGet/Status` | `string` | 0..1 | `OK` existe, `NO` no existe (C2) |
| `FEXCheck_PermisoResult/FEXErr` | `ClsFEXErr` | 0..1 | Error: un solo código. "0 – OK" si no hubo error (manual) |
| `FEXCheck_PermisoResult/FEXErr/ErrCode` | `int` | 1..1 | Código |
| `FEXCheck_PermisoResult/FEXErr/ErrMsg` | `string` | 0..1 | Mensaje |
| `FEXCheck_PermisoResult/FEXEvents` | `ClsFEXEvents` | 0..1 | Evento informativo |
| `FEXCheck_PermisoResult/FEXEvents/EventCode` | `int` | 1..1 | Código (único) |
| `FEXCheck_PermisoResult/FEXEvents/EventMsg` | `string` | 0..1 | Mensaje |


`Status` = `OK` si existe, `NO` si no (String(C2)). Error 1810 si falta un campo o el país no existe.

### FEXDummy

Ping de infraestructura, sin `Auth` ([MAN pág. 55-56]).

Request: `<ar:FEXDummy/>`.

Response:

Elemento raíz: `FEXDummyResponse`

| Elemento (ruta) | Tipo | Ocurr. | Significado y límites |
|---|---|---|---|
| `FEXDummyResult` | `DummyResponse` | 0..1 | Resultado |
| `FEXDummyResult/AppServer` | `string` | 0..1 | `OK` |
| `FEXDummyResult/DbServer` | `string` | 0..1 | `OK` |
| `FEXDummyResult/AuthServer` | `string` | 0..1 | `OK` |


Respuesta real (homologación, 2026-10-02):

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Header><FEHeaderInfo xmlns="http://ar.gov.afip.dif.fexv1/"><ambiente>Homologacion-Ext - srext</ambiente><fecha>2026-10-02T15:11:13.5772956-03:00</fecha><id>5.0.1.0</id></FEHeaderInfo></soap:Header><soap:Body><FEXDummyResponse xmlns="http://ar.gov.afip.dif.fexv1/"><FEXDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></FEXDummyResult></FEXDummyResponse></soap:Body></soap:Envelope>
```

## Validaciones y errores

Cómo se reparten:

| Tipo | Dónde | Efecto |
|---|---|---|
| Infraestructura (500, 501, 502/505), autenticación (1000-1002) y validaciones de `FEXAuthorize` (1014, 1500-2113) | `FEXErr{ErrCode, ErrMsg}`: **un solo** error por respuesta | La solicitud se rechaza. Si en ese caso viene `FEXResultAuth` con `Resultado`=`R` o no viene: NO VERIFICADO (con error de token no viene) |
| Observaciones 14-17 | `FEXResultAuth/Motivos_Obs` (String(C40)) | El comprobante se aprueba con observación |
| Rechazo 21 (CUIT receptora apócrifa) | `FEXResultAuth/Motivos_Obs` | Rechaza |
| Mensajes no contemplados | `FEXErr` con código 1014 "incluyendo un texto que explica la causa exacta" (pág. 26) | Rechaza |

Sin error, `FEXErr` vuelve con `ErrCode` 0: el manual describe `FEXErr` como "Información del error producido (0 – OK)" y `FEXEvents` "(0 – OK)" (pág. 16-17). El `ErrMsg` exacto en ese caso: NO VERIFICADO.

La columna "Texto / condición" es la descripción del manual. Los mensajes reales sólo se conocen para 1000 (vivo). Para el resto, el texto del manual es la mejor aproximación y hay que marcarlo así.

Tabla completa del manual. También en `wsfexv1-codigos.json` (229 filas: 123 de `FEXAuthorize`, el resto repartido en las 17 operaciones con `Auth`).

### FEXAuthorize

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1000 | Usuario no autorizado a realizar esta operación | Error | FEXErr | Verificación de Token y Firma | 17 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | Error | FEXErr | Cuit solicitante se encuentra entre sus representados | 17 |
| 1002 | Debe ser un valor numerico mayor a 0. | Error | FEXErr | Cuit solicitante sea valida | 17 |
| 1014 | Debe ser un valor numerico mayor o igual a 0. | Error | FEXErr | Identificador del requerimiento sea válido. | 17 |
| 1500 | Nulo, o comprendido entre N-5 y N+5 siendo N la Fecha de envío. Para el caso de comprobantes de servicios de exportación, la fecha de emisión del comprobante no puede ser posterior al mes en curso según normativa vigente | Error | FEXErr | Fecha_cbte | 17 |
| 1510 | Valor comprendido entre 1 – 99998 y dado de alta como punto de venta “Comprobantes de Exportación - Web Services” (Código FEEWS) Consultar método: FEXGetPARAM_PtoVenta | Error | FEXErr | Punto_vta | 17 |
| 1520 | Comprendido entre 1 y 99999999. | Error | FEXErr | Cbte_nro | 17 |
| 1530 | Los posibles tipo de comprobantes son: 19, 20, 21 19 – Factura de Exportación “E” 20 – Nota de Débito por operaciones con el Exterior 21 – Nota de Crédito por operaciones con el Exterior Ver método FEXGetPARAM_Cbte_Tipo | Error | FEXErr | Cbte_Tipo | 17 |
| 1535 | Verifica que el comprobante ingresado corresponde en secuencia al próximo inmediato a autorizar. | Error | FEXErr | Fecha_cbte Punto_vta Cbte_nro Cbte_Tipo | 17 |
| 1540 | Deberá ser algunos de los valores permitidos. Valores Permitidos: 1, 2, 4 1= Exportación definitiva de bienes 2= Servicios 4= Otros Ver método FEXGetPARAM_Tipo_Expo | Error | FEXErr | Tipo_expo | 18 |
| 1550 | Valores posibles: S, N o “vacío” “S” si ya se dispone del despacho de exportación. “N” si aún no se dispone del despacho de exportación. “vacío” si el campo Cbte_Tipo es 20 ó 21 o si Cbte_Tipo es igual a 19 y el campo Tipo_expo es igual a 2 ó 4. | Error | FEXErr | Permiso_existente | 18 |
| 1560 | Obligatorio. Deberá ser algunos de los valores permitidos. Ver método FEXGetPARAM_DST_pais | Error | FEXErr | Dst_cmp | 18 |
| 1570 | Deberá ser algunos de los valores permitidos. Ver método FEXGetPARAM_DST_CUIT | Error | FEXErr | Cuit_pais_cliente | 18 |
| 1580 | Se deberá consignar al menos un campo. | Error | FEXErr | ID_impositivo / Cuit pais cliente | 18 |
| 1590 | Deberá ser algunos de los valores permitidos. Ver método FEXGetPARAM_MON. Para el caso de estar autorizando comprobantes de Servicio (Tipo_expo=2) se deben informar solo monedas que tengan cotización al cierre del día hábil anterior (Para este caso ver método FEXGetPARAM_MON_CON_COTIZACION). | Error | FEXErr | Moneda_Id | 18 |
| 1600 | Deberá ser mayor a 0, hasta 4 enteros y 6 decimales | Error | FEXErr | Moneda_ctz | 18 |
| 1601 | Moneda_ctz deberá ser igual a 1 cuando de indique Moneda_Id = PES | Error | FEXErr | Moneda_Id / Moneda_ctz | 18 |
| 1602 | El campo Moneda_ctz es obligatorio si no informa el campo CanMisMonExt con el valor S, y debe ser mayor a 0 | Error | FEXErr | Moneda_ctz | 18 |
| 1603 | Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacío. | Error | FEXErr | CanMisMonExt | 18 |
| 1604 | Si informa el campo MonCotiz, el mismo no podrá superar en 1 a la cotización oficial. Ver Método FEXGetPARAM_Ctz | Error | FEXErr | Moneda_Id / Moneda_ctz | 18 |
| 1605 | Si informa Tipo Cbte 19 y MonID = PES, o Tipo Cbte 20 y 21 el campo CanMisMonExt no debe informarse | Error | FEXErr | Moneda_Id / CanMisMonExt | 18 |
| 1610 | Deberá ser mayor igual a cero e igual a la suma de los campos Item.Pro_total_item Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 * cantidad de ítems ingresados | Error | FEXErr | Imp_total | 18 |
| 1620 | Obligatorio si el tipo de comprobantes es 19 | Error | FEXErr | Forma_pago | 18 |
| 1630 | Obligatorio. Deberá ser algunos de los valores permitidos. Valores posibles: 1, 2, 3 1: Español 2: Inglés 3: Portugués Ver método FEXGetPARAM_Idiomas | Error | FEXErr | Idioma_cbte | 18 |
| 1640 | Obligatorio en el caso que el tipo de comprobante sea igual a 19 y tipo de operación sea igual a 1 (Productos). Para el resto de los casos es opcional. Para obtener los valores permitidos consultar el método FEXGetPARAM_Incoterms | Error | FEXErr | Incoterms | 18 |
| 1641 | Si se ingresó un valor, el campo Incoterms no puede estar vacío. | Error | FEXErr | Incoterms_Ds | 18 |
| 1642 | Longitud máxima es de 20 caracteres. | Error | FEXErr | Incoterms_Ds | 18 |
| 1650 | Campo Obligatorio, no podrá estar vacío. | Error | FEXErr | Cliente | 18 |
| 1651 | Longitud máxima es de 200 caracteres | Error | FEXErr | Cliente | 18 |
| 1660 | Campo Obligatorio, no podrá estar vacío | Error | FEXErr | Domicilio_cliente | 18 |
| 1661 | Longitud máxima es de 300 caracteres | Error | FEXErr | Domicilio_cliente | 19 |
| 1665 | Opcionales. El campo Obs la longitud máxima es 1000 y para el campo Obs_comerciales la longitud máxima es 4000 | Error | FEXErr | Obs / Obs_comerciales | 19 |
| 1667 | Si Moneda_Id <> PES, el campo Moneda_ctz no puede ser superior en un 400% ni inferior al 2% de la cotización oficial (ver método FEXGetPARAM_Ctz) | Error | FEXErr | Moneda_Id / Moneda_ctz | 19 |
| 1668 | Si el tipo de exportación corresponde al concepto (Tipo_expo=1) Expor tación definitiva de bienes. Deberá estar registrado como exportador. | Error | FEXErr | Auth Cuit Cmp Tipo_expo | 19 |
| 2053 | Para el caso de Factura, si está autorizando un comprobante de Servi cio (Tipo_expo=2) y la moneda es diferente de PES, la cotización debe ser la del día hábil anterior (divisa vendedor) de la fecha de emisión de la factura. En caso que la solicitud sea con fecha anterior a la de la fac tura, deberá ser igual a la del día hábil anterior de la solicitud. Para el caso de Nota de Débito o Nota de Crédito de Servicio (Tipo_expo=2), la cotización informada debe ser la misma que la del comproban te asociado. | Error | FEXErr | Moneda_ctz / Moneda_ctz | 19 |
| 1671 | Si informa fecha de pago `<Cmp>``<Fecha_pago>` debe tener formato váli do YYYYMMDD. | Error | FEXErr | Cmp Fecha_pago | 19 |
| 1672 | Para comprobantes del tipo “19 - Facturas de Exportación” donde el tipo de exportación es “2 – Servicios / 4 - Otros” la fecha de pago `<Cmp>``<Fecha_pago>` es obligatoria. | Error | FEXErr | Cmp Cbte_Tipo / Cmp Tipo_expo / Cmp Fecha_pago | 19 |
| 1673 | Para comprobantes que no son tipo “19 - Facturas de Exportación” la fe cha de pago `<Cmp>``<Fecha_pago>` no debe informarse. | Error | FEXErr | Cmp Cbte_Tipo / Cmp Tipo_expo / Cmp Fecha_pago | 19 |
| 1674 | Para comprobantes del tipo “19 - Facturas de Exportación” donde el tipo de exportación es “2 – Servicios / 4 - Otros”, la fecha de pago debe ser igual o posterior a la fecha de emisión del comprobante. | Error | FEXErr | Cmp Cbte_Tipo / Cmp Tipo_expo / Cmp Fecha_pago / Cmp Fecha_cbte | 19 |
| 1670 | Si alguno de estos campos no está vació entonces ninguno de estos debe estar vacío. Es decir si se informó el tipo de comprobante (Cbte_tipo) entonces se deben informar los campos punto de venta y número de comprobante (Cbte_punto_vta / Cbte_nro) | Error | FEXErr | Cbte_tipo / Cbte pun to vta / Cbte_nro | 20 |
| 1680 | Los posibles tipo de comprobantes son: 19, 20, 21, 88, 89 19 – Factura de Exportación “E” 20 – Nota de Débito por operaciones con el Exterior 21 – Nota de Crédito por operaciones con el Exterior 88 -Remito Electrónico de Tabaco Acondicionado 89 - Resumen de Datos de Exportación de Tabaco Acondicionado 91 - Remito R 993 - Remito Electrónico Harinero - Automotor 994 - Remito Electrónico Harinero – Ferroviario Ver método FEXGetPARAM_Cbte_Tipo | Error | FEXErr | Cbte_tipo | 20 |
| 1690 | De informarse deberá estar comprendido entre 1 – 99998. | Error | FEXErr | Cbte_punto_vta | 20 |
| 1700 | De informarse podrá tomar los valores desde 1 hasta 999999999 | Error | FEXErr | Cbte_nro | 20 |
| 1749 | Si el tipo de comprobante asociado (Cbte_tipo) es igual a 19, 20 o 21 y el punto de venta informado es electrónico, el punto de venta deberá corresponder a alguno de los tipos de puntos de venta habilitados para Comprobantes de Exportación. Si se cumple, el tipo y número de comprobante informado deberá estar autorizado. | Error | FEXErr | Cbte_tipo / Cbte pun to vta / Cbte_nro | 20 |
| 1754 | No se puede informar más de 1 comprobante asociado, excepto que los mismos sean 88 u 89. | Error | FEXErr | Cmps_asoc | 20 |
| 1755 | No se pueden informar comprobantes asociados cuando el tipo de comprobante a autorizar es 19 (Factura E), excepto que los mismos sean del tipo 89, 88, 91, 993, 994 | Error | FEXErr | Cmps_asoc | 20 |
| 1818 | Si el tipo de comprobante asociado Cbte_tipo es igual 89, 88, 993, o 994, entonces éste deberá estar registrado. | Error | FEXErr | Cmps_asoc | 20 |
| 1819 | Si el tipo de comprobante asociado Cbte_tipo es igual a 89, 88, 993, o 994, y el emisor (Cbte_cuit) es distinto al emisor del comprobante que se solicita autorización (`<Cuit>`) entonces, el comprobante asociado deberá estar registrado como confirmado. | Error | FEXErr | Cmps_asoc | 20 |
| 1820 | Si envía Cmps_asoc, Cmp_asoc es obligatorio | Error | FEXErr | Cmps_asoc | 20 |
| 1821 | Si informa comprobante asociado y sus códigos (Cbte_tipo) son 89, 88, 993, o 994, entonces el receptor del comprobante a autorizar debe ser igual al receptor del comprobante asociado. | Error | FEXErr | Cmps_asoc | 20 |
| 1822 | Si informa comprobante asociados y sus códigos (Cbte_tipo) corresponden a 89, 88, 993, o 994, los mismos no deben encontrarse asociado a otro comprobante. | Error | FEXErr | Cmps_asoc | 20 |
| 1823 | Si informa comprobante asociados y sus códigos (Cbte_tipo) corresponden a 89, 88, 993, o 994, tanto el comprobante a autorizar como el comprobante asociado deben corresponder a comprobantes del tipo exportación. | Error | FEXErr | Cmps_asoc | 20 |
| 2040 | Si esta autorizando una N.D. de Servicio (Tipo_expo=2), no se puede asociar mas de una Factura como comprobante asociado (Cmp.Cmps_asoc). | Error | FEXErr | Cmps_asoc | 20 |
| 2041 | Si esta autorizando una N.D. de Servicio (Tipo_expo=2), el comprobante asociado no puede ser del tipo Nota de Debito (Cmp.Cmps_asoc). | Error | FEXErr | Cmps_asoc | 20 |
| 2042 | Si esta autorizando una N.D. de Servicio (Tipo_expo=2), el comprobante asociado no puede ser del tipo Nota de Credito (Cmp.Cmps_asoc). | Error | FEXErr | Cmps_asoc | 20 |
| 2043 | Si esta autorizando una N.C. de Servicio (Tipo_expo=2), no se puede asociar mas de una Factura como comprobante asociado (Cmp.Cmps_asoc). | Error | FEXErr | Cmps_asoc | 20 |
| 2044 | Si esta autorizando una N.C. de Servicio (Tipo_expo=2), no se puede asociar mas de una Nota de Debito como comprobante asociado (Cmp.Cmps_asoc). | Error | FEXErr | Cmps_asoc | 20 |
| 2045 | Si esta autorizando una N.C. de Servicio (Tipo_expo=2), el comprobante asociado no puede ser del tipo Nota de Credito (Cmp.Cmps_asoc). | Error | FEXErr | Cmps_asoc | 20 |
| 2046 | Si esta autorizando una N.C. de Servicio (Tipo_expo=2), no puede ingresar como comprobantes asociados una Factura mas una Nota de Debito (Cmp.Cmps_asoc). Ingrese uno y solo uno de los tipos de comprobantes. | Error | FEXErr | Cmps_asoc | 21 |
| 2047 | Si esta autorizando una N.D. o N.C. comprobante de Servicio (Tipo=2), el campo Cmp.Cmps_asoc es de caracter obligatorio. | Error | FEXErr | Cmps_asoc | 21 |
| 2048 | Si esta autorizando un comprobante de Servicio (Tipo_expo=2), el comprobante asociado debe ser electrónico y existir en las bases del organismo (ARCA). | Error | FEXErr | Cmps_asoc | 21 |
| 2049 | Si esta autorizando un comprobante de Servicio (Tipo_expo=2), el comprobante asociado tambien debe ser de Servicio. | Error | FEXErr | Cmps_asoc | 21 |
| 2050 | Si esta autorizando un comprobante de Servicio (Tipo_expo=2), el pais de destino debe ser el mismo que el comprobante asociado. | Error | FEXErr | Cmps_asoc | 21 |
| 2051 | Si esta autorizando un comprobante de Servicio (Tipo_expo=2), la moneda debe ser la misma que el comprobante asociado o emitirse en PESOS. | Error | FEXErr | Cmps_asoc | 21 |
| 2052 | Si esta autorizando un comprobante de Servicio (Tipo_expo=2), la fecha del comprobante debe ser igual o posterior a la fecha del comprobante asociado. | Error | FEXErr | Cmps_asoc | 21 |
| 2055 | Para comprobante de Servicio (Tipo_expo=2), la Nota de Debito asocia da que informa debe tener una FACTURA (Cbte_tipo=19) de servicio (Tipo_expo=2) asociada. | Error | FEXErr | Cmps_asoc | 21 |
| 1720 | - Obligatorio para Tipo_expo = 1 Cmp.Cbte_Tipo = 19 y Cmp.Permiso_existente = “S” - Enviado. El mismo no debe enviarse cuando Cmp.Permiso_existente = “N" - Obligatorio (tag Permisos), Si envía `<Permisos>`, `<Permiso>` es obligatorio. | Error | FEXErr | Permisos / Id_permiso | 21 |
| 1730 | Si se informó el campo Id_permiso deberá informase el campo Dst_merc, como así también si se informó el campo Dst_merc deberá informarse el campo Id_permiso. | Error | FEXErr | Id_permiso / Dst_merc | 22 |
| 1736 | No es posible informar estos campos con tipo expo = 2 ó 4. | Error | FEXErr | Tipo_expo / Permisos | 22 |
| 1740 | Deberá ser un permiso válido, formato 99999AAXX999999A (donde XX podrán ser números o letras). Ver método FEXCHECK_PERMISO. Importante: la combinación Id_permiso y Dst_merc no pueden repetirse dentro del array de `<Permisos>`. | Error | FEXErr | Id_permiso | 22 |
| 1750 | Para los posibles valores consultar método FEXGetPARAM_DST_pais. El destino de la mercadería debe corresponder a un país del permiso de embarque (código despacho) asignado al campo Id_permiso. Se puede validar la existencia de un permiso de embarque / destino de la mercadería mediante el método: FEXCHECK_PERMISO | Error | FEXErr | Dst_merc | 22 |
| 1760 | No podrá superar longitud de 50 caracteres | Error | FEXErr | Pro_codigo | 22 |
| 1770 | Campo obligatorio. No podrá exceder los 4000 caracteres de longitud. | Error | FEXErr | Pro_ds | 22 |
| 1775 | Si Pro_umed es igual a 0, 97 ó 99 deberán informar Item.Pro_qty, Item.Pro_precio_uni y Pro_bonificacion igual a 0 ó no informarse. | Error | FEXErr | Pro_qty / Pro_umed / Pro_precio_uni / Pro_bonificacion | 22 |
| 1780 | Es obligatorio si se informa el precio unitario (Pro_precio_uni) o si Pro_umed es distinto a 0, 97 y 99. De ingresarse valor deberá ser mayor a cero. | Error | FEXErr | Pro_qty | 22 |
| 1813 | Valor máximo permitido 12 enteros y 6 decimales. | Error | FEXErr | Pro_qty | 22 |
| 1790 | Valores posible Ver Método FEXGetPARAM_UMed | Error | FEXErr | Pro_umed | 22 |
| 1800 | Es obligatorio si se informa la cantidad (Pro_qty) o si Pro_umed es distinto a 0, 97 y 99. De ingresarse valor deberá ser mayor o igual a cero. | Error | FEXErr | Pro_precio_uni | 22 |
| 1814 | Valor máximo permitido 12 enteros y 6 decimales. | Error | FEXErr | Pro_precio_uni | 22 |
| 1810 | Obligatorio. Si Pro_umed es distinto a 97 y 99, el valor deber ser mayor o igual a 0. Si Pro_umed = 97, sin restricción, el valor puede ser menor, igual o mayor a cero. Si Pro_umed = 99, el valor debe ser menor a 0. | Error | FEXErr | Pro_total_item | 22 |
| 1815 | Si Pro_umed es distinto a 0, 97 ó 99 deberá ser igual a `<Pro_precio_uni>` * `<Pro_qty>` - Pro_bonificacion Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=0.01 | Error | FEXErr | Pro_total_item | 22 |
| 1816 | Valor máximo permitido 13 enteros y 2 decimales. | Error | FEXErr | Pro_total_item | 22 |
| 1811 | Si Pro_umed es distinto de 97, 99 y 0, entonces el valor informado para Pro_bonificacion debe ser mayor o igual a 0 | Error | FEXErr | Pro_bonificacion | 22 |
| 1812 | Si es mayor a 0 debe ser menor o igual a Pro_precio_uni * Pro_qty. | Error | FEXErr | Pro_bonificacion | 22 |
| 1817 | Valor máximo permitido 12 enteros y 6 decimales. | Error | FEXErr | Pro_bonificacion | 22 |
| 1666 | La estructura `<Items>` es inválida, ya sea porque no se ingresó, o bien porque posee 0 ítems, o bien porque supera los 9999 ítems. | Error | FEXErr | Items | 22 |
| 2001 | Si envía opcionales, opcional es obligatorio informarlo. | Error | FEXErr | Opcionales / Opcional | 23 |
| 2002 | Si envía opcionales, opcional es obligatorio y no debe estar vacío. | Error | FEXErr | Opcionales / Opcional | 23 |
| 2003 | El campo Id en Opcionales es obligatorio y debe ser alguno de los devueltos por el método FEXGetPARAM_Opcionales. | Error | FEXErr | Opcionales / Opcional Id | 23 |
| 2004 | El campo Id en Opcionales es obligatorio y no debe repetirse. | Error | FEXErr | Opcionales / Opcional Id | 23 |
| 2005 | Si envía opcionales con el identificador 2401, el campo valor es obligatorio informarlo. | Error | FEXErr | Opcionales / Opcional Id Opcional Valor | 23 |
| 2006 | Si envía opcionales con el identificador 2401, el campo valor debe contener el documento de exportación. Alfanumérico de 11 caracteres | Error | FEXErr | Opcionales / Opcional Id Opcional Valor | 23 |
| 2007 | Si envía opcionales con el identificador 2402, el campo valor es obligatorio informarlo. | Error | FEXErr | Opcionales / Opcional Id Opcional Valor | 23 |
| 2008 | Si envía opcionales con el identificador 2402, el campo valor debe representar al monto FOB. Se espera un numérico de 13 valores enteros y 2 decimales. Separador de decimales usar el punto. | Error | FEXErr | Opcionales / Opcional Id Opcional Valor | 23 |
| 2010 | Si informa opcionales para el régimen de exportación simplificada es obligatorio informar: - para “19 – Facturas” el documento de exportación simplificada y el monto FOB con valor mayor o igual a 0. - para “20 - Nota de Débito” y “21 – Nota de Crédito” solo informar el monto FOB. | Error | FEXErr | Cbte_Tipo / Opcionales / Opcional | 23 |
| 2011 | Si informa opcionales para el régimen de exportación simplificada, solo se encuentra habilitada para el tipo de exportación 1 – PRODUCTO. | Error | FEXErr | Tipo_expo / Opcionales | 23 |
| 2016 | Si envía opcionales para el régimen de exportación simplificada, solo se permite moneda en DOLARES, Moneda_Id = DOL. | Error | FEXErr | Opcionales / Moneda_Id | 23 |
| 2020 | Si envía opcionales para el régimen de exportación simplificada e informa el monto FOB, el mismo no puede superar el monto máximo habilitado a informar por comprobante. | Error | FEXErr | Opcionales Opcional Id Opcional Valor | 23 |
| 2021 | Si envía opcionales para el régimen de exportación simplificada e informa el monto FOB, el mismo no puede superar el monto total del comprobante. | Error | FEXErr | Opcionales Opcional Id Opcional Valor / Cmp Imp_total | 23 |
| 2022 | Si envía opcionales para el régimen de exportación simplificada e informa el monto FOB y el tipo de comprobante es Factura, el valor informado debe ser igual al monto FOB del Permiso de Embarque. | Error | FEXErr | Opcionales Opcional Id Opcional Valor / Cmp Cbte_Tipo | 23 |
| 2023 | Si envía opcionales para el régimen de exportación simplificada, informa el monto FOB y el tipo de comprobante es Nota de Crédito o Débito, el valor informado debe ser menor o igual al monto FOB original informado junto al Permiso de Embarque. | Error | FEXErr | Opcionales Opcional Id Opcional Valor / Cmp Cbte_Tipo | 23 |
| 2026 | Si envía opcionales para el régimen de exportación simplificada, evaluar que el exportador tenga saldo disponible acorde con el tipo de operación para el periodo identificado en la fecha del comprobante. | Error | FEXErr | Opcionales Opcional Id Opcional Valor / Cmp Fecha_cbte | 23 |
| 2027 | Si el comprobante es del tipo exportación simplificada, el emisor del comprobante debe encontrarse habilitado. | Error | FEXErr | Auth Cuit | 23 |
| 2028 | Si el comprobante es Nota de Crédito o Débito, de exportación simple, es obligatorio informar comprobantes asociados. | Error | FEXErr | Auth Cuit / Cmp Cbte_Tipo / Opcionales Opcional Id Opcional Valor / Cmps_asoc | 24 |
| 2029 | Si el comprobante es Nota de Crédito o Débito, de exportación simple, el comprobante asociado debe ser un comprobante de exportación simplificada Autorizado perteneciente al emisor. | Error | FEXErr | Auth Cuit / Cmp Cbte_Tipo / Opcionales Opcional Id Opcional Valor / Cmps_asoc | 24 |
| 2031 | Si el tipo de comprobante asociado Cbte_tipo es distinto a 88 u 89 e informa el cuit del emisor del comprobante asociado (Cbte_cuit), no puede ser distinto al emisor del comprobante que se solicita autorización (`<Cuit>`). | Error | FEXErr | Auth Cuit Cmp_asoc Cbte_tipo Cmp_asoc Cbte_cuit | 24 |
| 2032 | Si el comprobante es 20 - Nota de Débito o 21 - Nota de Crédito e intenta autorizar un comprobante del tipo exportación simplificada es obligatorio informar comprobante asociado del tipo exportación simplificada y datos opcionales del tipo exportación simplificada. | Error | FEXErr | Cmp Cbte_Tipo / Cmp_asoc Opcionales | 24 |
| 2056 | Si envía opcionales para el régimen de exportación simplificada no informar `<Permios>``<Permiso>` | Error | FEXErr | Opcionales Opcional Id Opcional Valor / Permisos Permiso | 24 |
| 2057 | Si el comprobante es 20 - Nota de Débito o 21 - Nota de Crédito e intenta autorizar un comprobante del tipo exportación simplificada no informar el documento de exportación simplificada | Error | FEXErr | Cmp Cbte_Tipo / Opcionales Opcional Id Opcional Valor | 24 |
| 2058 | Si el comprobante es 20 - Nota de Débito o 21 - Nota de Crédito e intenta autorizar un comprobante del tipo exportación simplificada es obligatorio informar el monto FOB. | Error | FEXErr | Cmp Cbte_Tipo / Opcionales Opcional Id Opcional Valor | 24 |
| 2059 | El documento de exportación simplificado informado no se encuentra registrado o tiene diferencias con el registrado. | Error | FEXErr | Opcionales Opcional Id Opcional Valor | 24 |
| 2060 | El documento de exportación simplificado informado no se encuentra registrado o tiene diferencias en el monto FOB. | Error | FEXErr | Opcionales Opcional Id Opcional Valor | 24 |
| 2061 | Según los datos del comprobante asociado, no se puede autorizar el comprobante actual, el saldo FOB es inferior a 0 o superior al disponible por comprobante. | Error | FEXErr | Auth Cuit / Cmp_asoc / Opcionales / Opcional Id Opcional Valor | 24 |
| 14 | Si el emisor es monotributista, e informa una factura o nota de débito, se valida que el importe total en pesos no exceda el importe límite establecido para la categoría máxima de Monotributo. Por tal motivo, quedarías excluido automáticamente teniendo que solicitar el alta de los tributos (impositivos y de los recursos de la seguridad social) en el régimen general de acuerdo con la actividad. | Observa | FEXResultAuth/Motivos_Obs | Cuit / Cbte_Tipo / Imp_total / Moneda_ctz | 26 |
| 15 | Si el emisor es monotributista, e informa una factura o nota de crédito, se valida que el importe total en pesos no exceda el importe límite establecido para la categoría de Monotributo. Tenerlo en cuenta para la próxima recategorización. | Observa | FEXResultAuth/Motivos_Obs | Cuit / Cbte_Tipo / Imp_total / Moneda_ctz | 26 |
| 16 | Si el comprobante fue emitido a un destino del tipo Zona Franca, es necesario que exista el receptor en los padrones de ARCA | Observa | FEXResultAuth/Motivos_Obs | Cuit / Punto_vta / Dst_cmp | 26 |
| 17 | El importe de la nota de crédito supera el monto del comprobante asociado que estás ajustando. Verificá los montos ingresados y de tratarse de un error, tenés que efectuar el ajuste o anulación de la operación según corresponda. | Observa | FEXResultAuth/Motivos_Obs | Cuit / Cbte_Tipo / Imp_total / Moneda_ctz | 26 |
| 21 | La CUIT receptora se encuentra inactiva por haber sido incluida en la consulta de facturas apócrifas. | Rechaza | FEXResultAuth/Motivos_Obs | Cuit | 26 |
| 1014 | Los mensajes de error no contemplados en este documento salen por código 1014 incluyendo un texto que explica la causa exacta del error. | Error | FEXErr | (otros) | 26 |


Faltan en el manual: **2107 y 2110**. El historial (2.0.0) los da de alta y la 2.0.1 dice que 2107 a 2113 "SOLO observan el comprobante" si la actividad es Harina y la fecha está entre el 01/02/2023 y el 28/02/2023, y rechazan desde el 01/03/2023; pero en la tabla de la pág. 25 sus filas están vacías. Textos de 2107 y 2110: NO VERIFICADO.

Matriz de permisos de embarque (pág. 21). Los incumplimientos salen todos como 1720 con su texto:

| Tipo_expo | Cbte_Tipo | Permiso_existente | `Permisos` (Id_permiso / Dst_merc) |
|---|---|---|---|
| 1 | 19 | N | Vacío |
| 1 | 19 | S | Obligatorio |
| 1 | 20 | Vacío | Opcional |
| 1 | 21 | Vacío | Opcional |
| 2 o 4 | 19 | Vacío | Vacío |
| 2 o 4 | 20 | Vacío | Vacío |
| 2 o 4 | 21 | Vacío | Vacío |

Comprobantes asociados permitidos (pág. 19):

| Cmp.Cbte_Tipo | Cmp_asoc.Cbte_tipo | Cantidad máxima | Validación |
|---|---|---|---|
| 20 o 21 | 19, 20 o 21 | 1 | Que exista el comprobante (sólo si el punto de venta es electrónico para Tipo_expo = 1 o 4). Para Tipo_expo = 2 debe existir y ser electrónico |
| 19, 20 o 21 | 89, 88, 91, 993, 994 | sin tope | Que exista el remito en los registros de la Administración |

### FEXGetCMP

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1020 | Comprobante inexistente | Error | FEXErr | — | 34 |


### FEXGetLast_ID

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1000 | Usuario no autorizado a realizar esta operación | Error | FEXErr | Verificación de Token y Firma | 36 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | Error | FEXErr | Cuit solicitante se encuentra entre sus representados | 36 |


### FEXGetLast_CMP

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1000 | Usuario no autorizado a realizar esta operación | Error | FEXErr | Verificación de Token y Firma | 37 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | Error | FEXErr | Cuit solicitante se encuentra entre sus representados | 37 |
| 1606 | Campo Cbte_Tipo no se corresponde con alguno de los comprobantes habilitados. Recuerde que los valores son 19, 20 o 21 | Error | FEXErr | Verificación del campo Cbte_Tipo | 37 |
| 1607 | Campo Pto_venta no es valido o no esta dado de alta como punto de venta de 'Comprobantes de Exportación - Web Services' | Error | FEXErr | Verificación del campo Pto_venta | 37 |


### FEXGetPARAM_Ctz

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1000 | Usuario no autorizado a realizar esta operación | Error | FEXErr | Verificación de Token y Firma | 51 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | Error | FEXErr | Cuit solicitante se encuentra entre sus representados | 51 |
| 1003 | De informar el campo, el mismo debe tener el sig. formato YYYY-MM-DD | Error | FEXErr | FchCotiz | 51 |


### FEXGetPARAM_MON_CON_COTIZACION

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1000 | Usuario no autorizado a realizar esta operación | Error | FEXErr | Verificación de Token y Firma | 58 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | Error | FEXErr | Cuit solicitante se encuentra entre sus representados | 58 |
| 2054 | El campo Fecha_CTZ es de integración obligatoria y debe tener el siguiente formato: YYYYMMDD | Error | FEXErr | Verificacion del campo Fecha_CTZ | 58 |


### FEXCheck_Permiso

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1000 | Usuario no autorizado a realizar esta operación | Error | FEXErr | Verificación de Token y Firma | 55 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | Error | FEXErr | Cuit solicitante se encuentra entre sus representados | 55 |
| 1810 | En caso de omisión de alguno de los campos de ingreso. En caso de no existir el país registrado en nuestras bases. | Error | FEXErr | Campo mandatario + Relación Permiso / Pais | 55 |


### Recuperadores sin errores propios

`FEXGetPARAM_MON`, `FEXGetPARAM_Cbte_Tipo`, `FEXGetPARAM_Tipo_Expo`, `FEXGetPARAM_UMed`, `FEXGetPARAM_Idiomas`, `FEXGetPARAM_DST_pais`, `FEXGetPARAM_Incoterms`, `FEXGetPARAM_DST_CUIT`, `FEXGetPARAM_PtoVenta`, `FEXGetPARAM_Opcionales` y `FEXGetPARAM_Actividades` tienen sólo los de infraestructura y estos dos (pág. 38-59):

| Código | Texto / condición (manual) | Efecto | Dónde aparece | Campo | Pág. |
|---:|---|---|---|---|---:|
| 500 | Error interno de aplicación. | Error | FEXErr | (infraestructura) | 9 |
| 501 | Error interno de base de datos. | Error | FEXErr | (infraestructura) | 9 |
| 502 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 505 | Error interno – Autorizador - Transacción Activa (el manual lo lista como "502/505") | Error | FEXErr | (infraestructura) | 9 |
| 1000 | Usuario no autorizado a realizar esta operación | Error | FEXErr | Verificación de Token y Firma | 39 |
| 1001 | Cuit solicitante no se encuentra entre sus representados | Error | FEXErr | Cuit solicitante se encuentra entre sus representados | 39 |


## Tablas y datos

**Del manual:**

| Tabla | Valores | Fuente |
|---|---|---|
| Tipos de comprobante | 19 Factura de Exportación "E"; 20 Nota de Débito por operaciones con el Exterior; 21 Nota de Crédito por operaciones con el Exterior | pág. 17 (1530) |
| Tipos de comprobante asociables | 19, 20, 21; 88 Remito Electrónico de Tabaco Acondicionado; 89 Resumen de Datos de Exportación de Tabaco Acondicionado; 91 Remito R; 993 Remito Electrónico Harinero - Automotor; 994 Remito Electrónico Harinero – Ferroviario | pág. 20 (1680) |
| Tipos de exportación | 1 Exportación definitiva de bienes; 2 Servicios; 4 Otros | pág. 14, 18 (1540) |
| Idiomas | 1 Español; 2 Inglés; 3 Portugués | pág. 14, 18 (1630) |
| `Permiso_existente` | `S`, `N` o vacío | pág. 18 (1550) |
| `CanMisMonExt` | `S` o `N` | pág. 14 (1603) |
| Opcionales | 2401 Documento de exportación simplificada (alfanumérico de 11); 2402 Monto FOB (13 enteros, 2 decimales, punto decimal). Sólo para exportación simplificada (RG Conjunta 4458/2019), Tipo_expo 1 y moneda `DOL` | pág. 15, 23 |
| Unidades especiales | 0, 97 y 99: sin cantidad ni precio. 97 admite total positivo o negativo; 99 (bonificación) exige total menor a 0 | pág. 22 (1775, 1810) |
| Formato de permiso | `99999AAXX999999A` (XX letras o números) | pág. 22 (1740) |

**Tablas genéricas [TAB]** (no son del manual; para sembrar los `FEXGetPARAM_*` hasta poder verificarlos):

Incoterms (`TABLA INCOTERMS V.0.1 26012011.xls`):

| Código | Descripción |
|---|---|
| EXW | Ex Works |
| FCA | Free Carrier |
| FAS | Free Alongside Ship |
| FOB | Free On Board |
| CFR | Cost and Freight |
| CIF | Cost, Insurance and Freight |
| CPT | Carriage Paid To |
| CIP | Carriage and Insurance Paid To |
| DAF | Delivered At Frontier |
| DES | Delivered Ex Ship |
| DEQ | Delivered Ex Quay |
| DDU | Delivered Duty Unpaid |
| DDP | Delivered Duty Paid |
| DAP | Delivered At Port |

Es la lista de Incoterms 2000 más DAP; faltan DAT/DPU de 2010/2020. Si el servicio real devuelve otra cosa: NO VERIFICADO.

Idiomas (`TABLAIDIOMASV.025082010.xls`): 1 Español, 2 Inglés, 3 Portugués (coincide con el manual).

Unidades de medida (`TABLA UNIDADES DE MEDIDA V.0 25082010.xls`):

| Código | Descripción | Código | Descripción |
|---|---|---|---|
| 00 | SIN DESCRIPCION | 27 | CENTIMETRO CUBICO |
| 01 | KILOGRAMO | 28 | UIACTANT |
| 02 | METROS | 29 | TONELADA |
| 03 | METRO CUADRADO | 30 | DECAMETRO CUBICO |
| 04 | METRO CUBICO | 31 | HECTOMETRO CUBICO |
| 05 | LITROS | 32 | KILOMETRO CUBICO |
| 06 | 1000 KILOWATT HORA | 33 | MICROGRAMO |
| 07 | UNIDAD | 34 | NANOGRAMO |
| 08 | PAR | 35 | PICOGRAMO |
| 09 | DOCENA | 36 | MUIACTANT |
| 10 | QUILATE | 37 | UIACTIG |
| 11 | MILLAR | 41 | MILIGRAMO |
| 12 | MEGA U. INTER. ACT. ANTIB | 47 | MILILITRO |
| 13 | UNIDAD INT. ACT. INMUNG | 48 | CURIE |
| 14 | GRAMO | 49 | MILICURIE |
| 15 | MILIMETRO | 50 | MICROCURIE |
| 16 | MILIMETRO CUBICO | 51 | U.INTER. ACT. HORMONAL |
| 17 | KILOMETRO | 52 | MEGA U. INTER. ACT. HOR. |
| 18 | HECTOLITRO | 53 | KILOGRAMO BASE |
| 19 | MEGA UNIDAD INT. ACT. INMUNG | 54 | GRUESA |
| 20 | CENTIMETRO | 55 | MUIACTIG |
| 21 | KILOGRAMO ACTIVO | 61 | KILOGRAMO BRUTO |
| 22 | GRAMO ACTIVO | 62 | PACK |
| 23 | GRAMO BASE | 63 | HORMA |
| 24 | UIACTHOR | 97 | SEÑAS/ANTICIPOS |
| 25 | JGO.PQT. MAZO NAIPES | 98 | OTRAS UNIDADES |
| 26 | MUIACTHOR | 99 | BONIFICACION |

Monedas: la tabla genérica (`TABLA MONEDAS V.0 25082010.xls`) es la misma que ya está en `wsfev1.md` §7.5; no se copia de nuevo. `Mon_Id` es String(C3) (`PES`, `DOL`, `012`...).

Países (`Dst_cmp`, `Dst_merc`): la tabla genérica está en `docs/arca/tablas/TABLA-PAISES-V.0-28072022.xlsx` (309 países, códigos de 3 dígitos, ej. 101 BURKINA FASO). Los ejemplos del manual usan 202, 203 y 220. Que `FEXGetPARAM_DST_pais` devuelva exactamente esa tabla: NO VERIFICADO.

CUIT de países (`FEXGetPARAM_DST_CUIT`): no hay tabla pública relevada. Los ejemplos del manual usan 50000000016 (con `Dst_cmp` 203) y 55000000050 (con 220). NO VERIFICADO.

**Datos de prueba del manual:** `Cuit` 66666666666, `Punto_vta` 2 y 99998, `Id` 1, 4502, 4503, permisos `09052EC01006154G` con destinos 202 y 203, `Cbte_cuit` asociado 33500606989.

## Comportamiento a simular

**Idempotencia por `Id` (manual 1.5, pág. 8-9).** Es la diferencia central con wsfev1:

- Cada `FEXAuthorize` trae `Cmp.Id` (long, N15), elegido por el cliente. "Es fundamental asegurarse de no repetir accidentalmente el `<Id>`"; el manual sugiere una secuencia o una representación numérica de fecha/hora.
- Si se corta la comunicación **a la ida**, el cliente reenvía la misma solicitud (mismo `Id`) y el servicio la procesa normalmente: `Reproceso`=`N`.
- Si se corta **a la vuelta**, el cliente reenvía la misma solicitud (mismo `Id`): "El sistema busca la solicitud recibida en su base de datos y, si la encuentra, retorna la respuesta con el campo `<Reproceso>` = "S". Si no la encuentra, la procesa normalmente".
- El servicio "almacena en su base de datos todas las solicitudes de CAE que fueron aprobadas". O sea: el `Id` sólo queda registrado si se aprobó. Un `Id` que fue rechazado se puede reusar: inferido de esa frase, NO VERIFICADO.
- Qué pasa si se reenvía un `Id` ya aprobado **con otros datos** (otro número, otro importe): NO VERIFICADO. La opción conservadora es devolver lo guardado con `Reproceso`=`S` sin mirar el resto.
- `FEXGetLast_ID` devuelve el máximo `Id` recibido "para la cuit enviada": el contador de `Id` es por CUIT. Si cuenta `Id` rechazados: NO VERIFICADO (el manual dice "recibido" en un lugar y "aprobadas" en otro).
- Validación 1014 sobre el `Id`: "Debe ser un valor numerico mayor o igual a 0."

**Numeración.** Correlativa por CUIT + `Punto_vta` + `Cbte_Tipo` (1535: "corresponde en secuencia al próximo inmediato a autorizar"). El primero es 1: inferido, el manual no lo dice. `FEXGetLast_CMP` devuelve `Cbte_nro` y `Cbte_fecha` del último. El punto de venta tiene que ser del tipo "Comprobantes de Exportación - Web Services" (código FEEWS), de 1 a 99998, y no bloqueado (`FEXGetPARAM_PtoVenta`).

**Fechas.** `Fecha_cbte` nula o entre N-5 y N+5 (N = fecha de envío); para servicios, no posterior al mes en curso (1500). `Fecha_pago` obligatoria para 19 con Tipo_expo 2 o 4, y no menor a `Fecha_cbte` (1672-1674); no se informa en 20 y 21.

**Importes.** `Imp_total` = suma de `Pro_total_item`, con error relativo ≤ 0,01 % o absoluto ≤ 0,01 × cantidad de ítems (1610). Por ítem (si `Pro_umed` no es 0, 97 ni 99): `Pro_total_item` = `Pro_precio_uni` × `Pro_qty` − `Pro_bonificacion`, con error relativo ≤ 0,01 % o absoluto ≤ 0,01 (1815). Redondeo Round Half Even "a 5 decimales" (historial 1.0) (pág. 56). Entre 1 y 9999 ítems (1666).

**Moneda y cotización.** `Moneda_ctz` = 1 si `Moneda_Id` = `PES` (1601). Si no es PES, entre el 2 % y el 400 % de la cotización oficial (1667) y no más de 1 por encima de ella (1604). Con `CanMisMonExt`=`S` y moneda del Banco Nación se puede omitir `Moneda_ctz` y el servicio usa la registrada; si se informa debe coincidir exactamente con la del día hábil cambiario anterior a la fecha del comprobante (historial 3.0.0). `CanMisMonExt` sólo para facturas (19) en moneda extranjera (1605). Para servicios (Tipo_expo 2), monedas de `FEXGetPARAM_MON_CON_COTIZACION` y cotización del día hábil anterior (1590, 2053).

**Respuesta aprobada.** `FEXResultAuth` con `Id`, `Cuit`, `Cbte_tipo`, `Punto_vta`, `Cbte_nro`, `Cae` (C14), `Fch_venc_Cae` (C8, yyyymmdd), `Fch_cbte`, `Resultado`, `Reproceso` (S/N) y `Motivos_Obs`. Valores de `Resultado`: el manual no los enumera; por analogía con el resto de factura electrónica, `A` aprobado y `R` rechazado. NO VERIFICADO (también si existe `O` o `P`). Cómo se calcula `Fch_venc_Cae`: NO VERIFICADO.

**Relación con wscdc.** Si wscdc constata comprobantes E es NO VERIFICADO; si se habilita, el mapeo de campos está en `wscdc.md`.

**Eventos.** Producción devuelve `EventCode` 0 / `Ok`; homologación arrastra el evento 103 de mantenimiento. El simulador puede devolver 0/`Ok` por defecto y permitir configurar un evento.

## No verificado

- Si con un error de validación de negocio (no de token) viene `FEXResultAuth` con `Resultado`=`R`, o sólo `FEXErr`.
- Los valores posibles de `Resultado` y el formato exacto de `Motivos_Obs` (C40) cuando hay observaciones 14-17.
- El `ErrMsg` real de cada código distinto de 1000.
- Textos de 2107 y 2110 (filas vacías en el manual).
- Reproceso con el mismo `Id` y datos distintos; si `FEXGetLast_ID` cuenta `Id` rechazados; qué devuelve antes del primer `Id`.
- Qué devuelve `FEXGetLast_CMP` sin comprobantes previos.
- Contenido real de las tablas de parámetros (incoterms, unidades, países, CUIT de países, monedas, opcionales, actividades) y si coinciden con las tablas genéricas.
- El cálculo de `Fch_venc_Cae`.
- Que `FEXGetCMP` con `Cbte_Tipo` (mayúscula, como el manual) lea 0.
- Si los ejemplos 1 y 2 del manual (sin envelope) reflejan algún cliente real o son sólo ilustrativos.
