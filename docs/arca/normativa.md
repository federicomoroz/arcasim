# Normativa ARCA para ArcaSim: qué tiene que validar WSFEv1 y desde cuándo

Estado de la investigación: **1 de octubre de 2026**.
Alcance: el marco normativo y los cambios recientes que definen cómo se arma un comprobante y qué valida WSFEv1. El detalle de los métodos SOAP, los WSDL y el WSAA están en otros documentos del repo.

## Convenciones de este documento

| Marca | Significado |
|---|---|
| **VIGENTE** | Rige hoy (01/10/2026) según la fuente primaria citada. |
| **ANUNCIADO** | Publicado con fecha futura (norma o manual), todavía no rige en producción. |
| **POSTERGADO** | Tuvo una fecha que no se cumplió y se movió. |
| **NO VERIFICADO** | Viene de una fuente secundaria (foro, consultora, prensa) y no se encontró en una fuente oficial. |
| **INFERENCIA** | Conclusión propia a partir de fuentes oficiales; no está escrita literalmente. |

Fuentes primarias usadas (en orden de autoridad): Boletín Oficial (boletinoficial.gob.ar), textos actualizados de argentina.gob.ar/normativa e Infoleg, publicaciones de ARCA (arca.gob.ar / afip.gob.ar / servicioscf.afip.gob.ar) y los manuales del desarrollador de WSFEv1, que se descargaron y se leyeron completos (v4.7 y v4.8).

---

## 0. Resumen para el simulador

1. **Hay dos manuales vigentes en paralelo.** Producción: **v4.7** ("Revisión correspondiente al 1 de Septiembre de 2026"), enlazado en https://www.arca.gob.ar/fe/ayuda/webservice.asp. Homologación externa: **v4.8** ("Revisión correspondiente al 1 de Diciembre de 2026"), enlazado en https://www.afip.gob.ar/ws/documentacion/homologacion-externa.asp y publicado en https://www.arca.gob.ar/fe/ayuda/documentos/wsfev1-RG-4291.pdf. El único cambio funcional de la v4.8 es que **`CondicionIVAReceptorId` pasa a ser obligatorio el 01/12/2026** y las observaciones 10245 (CAE) y 825 (CAEA) quedan en desuso.
2. **`CondicionIVAReceptorId`** (RG 5616/2024, art. 2). Hoy se puede omitir: el comprobante sale con la observación **10245** (CAE) o **825** (CAEA). Desde el **01/12/2026** se rechaza con **10246** (CAE) o **826** (CAEA). Si el valor no existe, ya se rechaza hoy (10242 en CAE, 823 en CAEA). Si el valor no corresponde a la clase de comprobante, CAE rechaza (10243) y CAEA solo observa (824).
3. **Consumidor final:** hay que identificarlo cuando la operación es **igual o superior a $10.000.000** (RG 1415, Anexo II, Ap. A, Tít. II, inc. d), texto según RG 5700/2025 y RG 5866/2026). El manual no da la cifra: dice "monto en pesos resultante según RG4444" y lo calcula como `ImpTotal × cotización`.
4. **Ventana de `CbteFch`** (RG 4291, art. 9; manual, validación 10016). Concepto 1 (productos): N±5 días y sin salirse del mes. Conceptos 2 y 3: N±10 días. FCE: de N-5 a N+1. N es la fecha en que se envía el pedido.
5. **Clase M eliminada** (RG 5762/2025, vigente desde el 01/12/2025). Los códigos 51-54 siguen existiendo, ahora como clase **"A con leyenda OPERACIÓN SUJETA A RETENCIÓN"** (`ClaseCmp` = `ALEY`).
6. **El CAEA queda solo para contingencias** (RG 5782/2025, vigente desde el **01/08/2026** según RG 5852/2026). Además, `CbteFchHsGen` es obligatorio en los puntos de venta CAEA de contingencia.
7. **Transparencia fiscal (Ley 27.743 / RG 5614):** solo afecta la representación gráfica. **WSFEv1 no tiene ningún campo para eso.**
8. **QR (RG 4892/2020):** es un JSON v1 codificado en Base64. La especificación oficial hoy usa la URL `https://www.arca.gob.ar/fe/qr/` y su propio ejemplo todavía muestra `https://www.afip.gob.ar/fe/qr/?p=`.

---

## 1. Mapa normativo

| Norma | Publicación (BO) | Qué define para ArcaSim | Estado | Fuente |
|---|---|---|---|---|
| RG 1415/2003 | 2003 (texto actualizado) | Qué comprobantes existen, letras A/B/C/E, datos obligatorios, identificación del consumidor final, plazos de emisión, puntos de venta (art. 47) | VIGENTE (con modificaciones) | https://www.argentina.gob.ar/normativa/nacional/norma-81316/actualizacion |
| RG 4291/2018 | 03/08/2018 | Régimen de emisión electrónica (R.E.C.E.), WebService como modalidad, lotes, ventana de fechas, puntos de venta, CAE | VIGENTE | https://www.boletinoficial.gob.ar/detalleAviso/primera/189308/20180803 ; texto actualizado: https://www.argentina.gob.ar/normativa/nacional/resoluci%C3%B3n-4291-2018-313088/actualizacion |
| RG 4290/2018 | 03/08/2018 | Contingencias (Título III) | VIGENTE (modificada por RG 5785) | https://www.argentina.gob.ar/normativa/nacional/resoluci%C3%B3n-4290-2018-313087/texto |
| RG 4367/2018 | 20/12/2018 | Factura de Crédito Electrónica MiPyME | VIGENTE | https://www.boletinoficial.gob.ar/detalleAviso/primera/198475/20181220 |
| RG 4444/2019 | 28/03/2019 | Montos de lote y de identificación del consumidor final (el manual la sigue citando) | VIGENTE en lo que no fue reemplazado | Citada en el texto actualizado de RG 4291, art. 8 |
| RG 4892/2020 | 24/12/2020 | Código QR obligatorio | VIGENTE | https://www.boletinoficial.gob.ar/detalleAviso/primera/239173/20201224 |
| Ley 27.743, Título VII | 08/07/2024 | Régimen de Transparencia Fiscal al Consumidor | VIGENTE | https://www.boletinoficial.gob.ar/detalleAviso/primera/310191/20240708 |
| RG 5614/2024 | 13/12/2024 | Reglamenta la transparencia fiscal en los comprobantes | VIGENTE (obligatoria para todos desde el 01/04/2025) | https://www.consejosalta.org.ar/wp-content/uploads/ARCA-5614.pdf (copia del BO) |
| RG 5616/2024 | 18/12/2024 | Condición frente al IVA del receptor; cotización en moneda extranjera | VIGENTE (el rechazo por falta del dato está ANUNCIADO para el 01/12/2026) | https://www.boletinoficial.gob.ar/detalleAviso/primera/318374/20241218 |
| RG 5700/2025 | 27/05/2025 | Umbral de identificación del consumidor final: $10.000.000 | VIGENTE desde el 29/05/2025 (el inc. d fue reescrito después por RG 5866) | https://www.consejosalta.org.ar/wp-content/uploads/ARCA-5700.pdf (copia del BO) |
| RG 5762/2025 | 25/09/2025 | Elimina la clase M; crea la clase "A con leyenda OPERACIÓN SUJETA A RETENCIÓN" | VIGENTE desde el 01/12/2025 | https://www.argentina.gob.ar/normativa/nacional/norma-417981/texto |
| RG 5782/2025 | 30/10/2025 | CAEA solo para contingencias (reemplaza a RG 2926) | VIGENTE desde el 01/08/2026 | https://www.argentina.gob.ar/normativa/nacional/resoluci%C3%B3n-5782-2025-419496/texto |
| RG 5785/2025 | 03/11/2025 | CAEA como primera opción de contingencia para WebService | VIGENTE desde el 01/08/2026 | https://www.boletinoficial.gob.ar/detalleAviso/primera/333886/20251103 |
| RG 5852/2026 | 29/05/2026 | Pasa la vigencia de RG 5782 y 5785 del 01/06/2026 al 01/08/2026 | VIGENTE | https://www.boletinoficial.gob.ar/detalleAviso/primera/342594/20260529 |
| RG 5824/2026 | 13/02/2026 (según prensa) | Nuevos obligados y liquidación mensual | **ABROGADA** por RG 5866 (art. 3) antes de entrar en vigencia | Ver RG 5866 |
| RG 5866/2026 | 29/06/2026 | Modifica RG 1415 y RG 4291: entidades financieras y seguros, seguros de caución por WSFEv1, PV vinculados a sistema y condición IVA, nuevo texto del inc. d) de consumidor final | VIGENTE desde el 01/07/2026, con cronograma escalonado hasta el 01/03/2027 | https://www.boletinoficial.gob.ar/detalleAviso/primera/343656/20260629 |
| Ley 27.440, Título I | 2018 | Crea la FCE MiPyME | VIGENTE | https://servicios.infoleg.gob.ar/infolegInternet/anexos/310000-314999/310084/norma.htm |

---

## 2. RG 4291: régimen de emisión electrónica (base de WSFEv1)

Fuente: texto original en el BO (https://www.boletinoficial.gob.ar/detalleAviso/primera/189308/20180803) y texto actualizado (https://www.argentina.gob.ar/normativa/nacional/resoluci%C3%B3n-4291-2018-313088/actualizacion).

| Art. | Regla | Impacto en el simulador |
|---|---|---|
| 6 | La autorización se pide por: a) aplicativo RECE (disponible hasta el 31/12/2018), **b) "WebService"**, c) "Comprobantes en línea". Monotributistas y exentos en IVA solo pueden usar b) o c). | WSFEv1 es la modalidad b). |
| 8 a) | Clase A: **un registro por comprobante**, sea cual sea el importe. | `CbteDesde` = `CbteHasta` para A (validación 10012). |
| 8 b) | Clases B y C a consumidores finales: si hay que identificar al receptor según RG 1415, un registro por comprobante. Si no, **un registro por lote**, con la suma de los importes (texto según RG 4444/2019, art. 10). | Habilita los lotes B (`CbteDesde` < `CbteHasta`) con `DocTipo` = 99 y `DocNro` = 0 (validaciones 10014 y 10015). |
| 8 c) | Las notas de crédito y débito solo se emiten con los códigos 02, 03, 07, 08, 12, 13, 52 y 53. | Tabla de tipos de comprobante. |
| 9 | Con fecha en el comprobante, la transferencia a ARCA puede hacerse **dentro de los 5 días corridos anteriores o posteriores** a esa fecha. Si la transferencia es anterior a la fecha del comprobante, las dos tienen que caer en el **mismo mes calendario**. Para **prestaciones de servicios, 10 días corridos**. Si no se manda fecha, la fecha de emisión es la de otorgamiento del CAE. | Ventana de `CbteFch`: ver la sección 10. |
| 11 | Cada solicitud sale de un **punto de venta específico** para el régimen, distinto de los de Controlador Fiscal, de RG 100 y de "Comprobantes en línea". Se habilita en "Administración de Puntos de Venta y Domicilios" (RG 1415, art. 47). La numeración es **correlativa por punto de venta**. | Validaciones 10005 ("El punto de venta informado debe estar dado de alta y ser del tipo RECE") y 10016 (correlatividad). |
| 12 | ARCA autoriza o rechaza. Los comprobantes no tienen efectos fiscales frente a terceros hasta que se otorga el CAE. Con WebService se da **un CAE por cada registro** de la solicitud. Los rechazos se informan con códigos y mensajes. | Un CAE por registro (en un lote B, un CAE para todo el rango). |
| 14 | El comprobante tiene que mostrar: a) el CAE, b) el código del tipo de comprobante, c) si corresponde, el código de la leyenda de crédito fiscal no computable, d) los demás datos del Anexo II, Ap. A de RG 1415. | Representación gráfica (fuera del WS). |
| 15 (último párrafo, agregado por RG 5614, art. 5) | La representación gráfica tiene que incluir los datos de transparencia fiscal. | Ver la sección 7. |
| 16 | Si el sistema no funciona, se aplica el Título III de RG 4290 (contingencia). | Ver la sección 9. |
| Anexo II (modificado por RG 5866, art. 2) | Sujetos obligados especiales: medicina prepaga, educación privada, **seguros de caución** (punto 5). Datos "Adicionales por RG": código 10 / 10.11 (educación y prepagas), **29.01 (Póliza) y 29.02 (Endoso)** (seguros de caución). | En WSFEv1 son los `Opcionales` con Id **2901 y 2902** (manual v4.7, validaciones 10273 a 10282). |

---

## 3. RG 1415: clases de comprobante, datos obligatorios y consumidor final

Fuente: texto actualizado, https://www.argentina.gob.ar/normativa/nacional/norma-81316/actualizacion

### 3.1 Quién emite cada letra

| Letra | Quién la emite y a quién | Artículo |
|---|---|---|
| **A** | Responsable inscripto (RI) → otros RI o **monotributistas**. Si el receptor es monotributista, el comprobante lleva la leyenda: "El crédito fiscal discriminado en el presente comprobante, sólo podrá ser computado a efectos del Régimen de Sostenimiento e Inclusión Fiscal para Pequeños Contribuyentes de la Ley Nº 27.618". Texto según RG 5003/2021, vigente desde el 01/07/2021. | Art. 15 a) |
| **B** | RI → exentos, no responsables, consumidores finales, sujetos que deben recibir trato de consumidor final, **sujetos no categorizados** | Art. 15 b) |
| **C** | **Exentos o no responsables** en IVA y **monotributistas** (texto según RG 5198/2022, vigente desde el 01/06/2022) | Art. 16 |
| **E** | Exportaciones, incluidas las del área aduanera especial (en WSFEX, no en WSFEv1) | Art. 17 |
| **A con leyenda "OPERACIÓN SUJETA A RETENCIÓN"** | RI que no superan los controles para la clase A. **Reemplaza a la clase M** (RG 5762/2025, arts. 5, 10, 11, 28 y 31) | Ver la sección 4 |
| **A con leyenda "PAGO EN CBU INFORMADA"** | RI que cumplen los requisitos pero no acreditan solvencia patrimonial y eligen esta opción | RG 5762, arts. 5, 20 y 21 |

En WSFEv1 las letras se traducen a códigos (manual v4.7/v4.8, validación 10007):

| Clase | `CbteTipo` |
|---|---|
| A | 01, 02, 03, 04, 05, 34, 39, 60, 63, 201, 202, 203 |
| B | 06, 07, 08, 09, 10, 35, 40, 61, 64, 206, 207, 208 |
| C | 11, 12, 13, 15, 211, 212, 213 |
| A con leyenda "operación sujeta a retención" (ex M) | 51, 52, 53, 54 |
| Bienes usados | 49 |

### 3.2 Identificación del consumidor final

**Umbral VIGENTE: $10.000.000** (igual o superior).

Texto VIGENTE del inc. d), Título II, Apartado A, Anexo II de RG 1415, según RG 5866/2026, art. 1 inc. f), vigente desde el 01/07/2026:

> "d) Cuando se trate de un sujeto que revista el carácter de consumidor final en el impuesto al valor agregado:
> 1. Leyenda "A CONSUMIDOR FINAL".
> 2. Si el importe de la operación es igual o superior a PESOS DIEZ MILLONES ($10.000.000.-): Documento Nacional de Identidad (DNI), Código Único de Identificación Laboral (CUIL) o Clave de Identificación (CDI), y en el supuesto de extranjeros, documento o cédula de identidad del país de origen o pasaporte.
> Los datos de apellido, nombre y domicilio del comprador, locatario o prestatario, podrán informarse o completarse con las letras "NR" de "No Requerido" y/o con ceros, cuando el sistema de facturación o controlador fiscal lo requiera.
> Deberá identificarse en el comprobante, al adquirente, locatario o prestatario, con su Clave Única de Identificación Tributaria (CUIT), sin considerar el importe previsto anteriormente, en caso de que el responsable lo requiera a los fines de poder computar la correspondiente deducción en su declaración jurada del impuesto a las ganancias."

Fuentes: texto actualizado de RG 1415 (enlace de arriba) y RG 5866 en el BO (https://www.boletinoficial.gob.ar/detalleAviso/primera/343656/20260629).

Historia del umbral:

| Período | Umbral | Norma | Fuente |
|---|---|---|---|
| Hasta el 28/05/2025 | $208.644 (efectivo) / $417.288 (otros medios de pago). Algunos medios citan $250.000 / $400.000; la ficha de Tributum que acompaña la copia de RG 5700 da $208.644 / $417.288. | RG 1415 según modificaciones anteriores | https://www.consejosalta.org.ar/wp-content/uploads/ARCA-5700.pdf |
| Desde el 29/05/2025 | **$10.000.000**, un solo umbral para todos los medios de pago. Datos: CUIT, CUIL, CDI o DNI (extranjeros: documento del país de origen o pasaporte). | RG 5700/2025, arts. 1 y 4 (BO 27/05/2025) | misma |
| Desde el 01/07/2026 | **$10.000.000**. El texto pasa a listar DNI, CUIL o CDI y suma la identificación con CUIT, sin importar el monto, cuando el receptor la pide para deducir en Ganancias. | RG 5866/2026, art. 1 inc. f) y art. 4 | BO RG 5866 |

Cómo lo valida WSFEv1 (manual v4.8, validaciones 10014 y 10015; changelog 2.11, 2.14 y 2.17):

- Lotes B (`CbteDesde` ≠ `CbteHasta`): `ImpTotal / (CbteHasta − CbteDesde + 1)` tiene que ser **menor** que el "monto en pesos resultante según RG4444", con `DocTipo` = 99 y `DocNro` = 0.
- B individual por debajo del umbral: si `DocTipo` = 99, `DocNro` = 0. Si `DocTipo` es 80, 86 u 87, el número tiene que estar en el padrón, salvo `DocNro` = 23000000000 (No Categorizado).
- B individual **"con montos superiores"** al umbral: `DocTipo` distinto de 99 y `DocNro` obligatorio.
- El monto se calcula como `importe total × cotización` (si `MonId` = PES, la cotización es 1).
- **NO VERIFICADO**: que el servicio use hoy exactamente $10.000.000. El manual no dice la cifra. **INFERENCIA**: debería ser $10.000.000, porque RG 4291 art. 8 b) ata el lote a "si no se requiere la identificación … conforme … RG 1415".
- **Ambigüedad del límite exacto**: la norma dice "igual o superior". El manual dice "<" para los lotes y "superiores" para los individuales. Con exactamente $10.000.000, la norma exige identificar. El simulador debería exigirlo (`>=`) y dejar ese límite configurable.

### 3.3 Plazos de emisión y entrega (art. 13)

Cuadro sustituido por RG 5866/2026, art. 1 inc. a), vigente desde el 01/07/2026:

| Operación | Fecha límite de emisión | Plazo de entrega |
|---|---|---|
| Compraventa de cosas muebles | Último día del mes en que se entregó la cosa o se la puso a disposición, lo que pase primero | 10 días corridos desde la emisión |
| Locaciones de obra | Día en que termina la prestación o se cobra el precio (total o parcial), lo que pase primero | — |
| Servicios continuos | Último día de cada mes, salvo que antes se haya cobrado o terminado | — |
| Locaciones de cosas | Vencimiento del pago de cada período | — |
| Anticipos que fijan precio | Día en que se cobra el anticipo | — |
| Servicios públicos (agua, luz, gas, teléfono, internet, etc.) | Primer vencimiento | Hasta el primer vencimiento |
| Directores, síndicos y cargos equivalentes | Día de la asignación individual | 10 días corridos |

Con consumidores finales, la entrega es en el momento de la operación (art. 13, último párrafo).

**INFERENCIA:** estos plazos son de derecho sustantivo y **WSFEv1 no los valida**. Lo que el servicio controla es la ventana de `CbteFch` contra la fecha del pedido (sección 10).

### 3.4 Puntos de venta (art. 47, texto de RG 5866)

- El punto de venta se informa con al menos **3 días hábiles** de anticipación al inicio de las operaciones. La baja se informa dentro de los **5 días hábiles**. **"Cuando se ingrese la baja de un punto de venta, el mismo no podrá volver a utilizarse."**
- "Los puntos de venta … deberán estar vinculados al sistema de facturación mediante el cual se emiten y a la condición ante el impuesto al valor agregado". Los de exportación (E) llevan un código específico.
- Se gestionan en "Administración de Puntos de Venta y Domicilios" con Clave Fiscal nivel 3. Ahí también se **vincula una actividad económica** al punto de venta.

Fuente: BO RG 5866, art. 1 inc. d).

---

## 4. RG 5762/2025: fin de la clase M

Fuentes: texto original (https://www.argentina.gob.ar/normativa/nacional/norma-417981/texto) y novedad de ARCA del 25/09/2025 (https://servicioscf.afip.gob.ar/publico/sitio/contenido/novedad/ver.aspx?id=5179).

- **Vigencia: 01/12/2025** (art. 32). Firmada el 24/09/2025, publicada en el BO el 25/09/2025.
- La clase M se reemplaza por la clase **"A" con leyenda "OPERACIÓN SUJETA A RETENCIÓN"**. Quien tenía habilitada la M pasa automáticamente a la nueva clase (novedad ARCA).
- "La emisión de estos comprobantes **mantendrá la codificación actualmente utilizada**, bajo la nueva denominación y **continuará con la correlatividad de la numeración** respecto de aquellos generados como clase "M" … salvo que se habilite un nuevo punto de venta" (novedad ARCA; art. 28).
- Toda referencia a la clase M en otras normas (salvo RG 3561) se lee como la nueva clase (art. 31).
- El receptor RI tiene que retener el 100 % del IVA y el 6 % de Ganancias (arts. 12 y 13). Eso no lo valida WSFEv1.
- Estos comprobantes **no se pueden emitir con Controlador Fiscal** (art. 11).

En WSFEv1:

- Changelog **v4.1 (01/12/2025)**: "Adecuaciones para el reemplazo de comprobantes clase M". Se modificó `FEParamGetCondicionIvaReceptor` y las descripciones de los códigos 814, 815, 1444, 1516, 10000, 10007, 10012, 10013, 10017, 10061, 10063, 10149, 10217, 10234 y 10244.
- `ClaseCmp` de `FEParamGetCondicionIvaReceptor` acepta **A, ALEY, B, C o 49**. Cualquier otro valor da el error **10244**.
- La validación 10000 tiene la sub-razón 09: "LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR COMPROBANTES CLASE A CON LEYENDA 'OPERACIÓN SUJETA A RETENCIÓN'".
- **NO VERIFICADO**: el texto exacto que devuelve hoy `FEParamGetTiposCbte` para los códigos 51 a 54. El manual no trae la tabla y no se pudo consultar el servicio.

---

## 5. RG 5616/2024: condición frente al IVA del receptor (`CondicionIVAReceptorId`)

### 5.1 Qué dice la norma

RG 5616/2024, firmada el 17/12/2024 y publicada en el BO el **18/12/2024** (https://www.boletinoficial.gob.ar/detalleAviso/primera/318374/20241218):

- **Art. 2, segundo párrafo:** "En los comprobantes electrónicos a emitir en los términos de la citada resolución general [4291] se deberá identificar la condición ante el impuesto al valor agregado del cliente (comprador, locatario o prestatario) con relación a la operación que se documenta."
- **Art. 2, primer párrafo:** si el pedido indica que el comprobante en moneda extranjera **se cancela en la misma moneda**, "el sistema automáticamente consignará el tipo de cambio vendedor divisa que informa el Banco de la Nación Argentina al cierre de sus operaciones, correspondiente al día hábil cambiario anterior a la fecha consignada en el comprobante o a la fecha de solicitud cuando se consigne una fecha posterior". De ahí salen `CanMisMonExt` y el cambio en la validación de `MonCotiz`.
- **Art. 5:** vigencia desde la publicación. Para WebService: "el 15 de enero de 2025 se publicarán los manuales … y se habilitará el servicio de homologación externa. La nueva versión, será de uso obligatorio a partir del **15 de abril de 2025**".

### 5.2 Cronología de la obligatoriedad (con conflictos de fuentes)

| Fecha | Hecho | Estado | Fuente |
|---|---|---|---|
| 18/12/2024 | Se publica RG 5616 | — | BO |
| 15/01/2025 | Manuales y homologación externa (según la norma) | Cumplido | RG 5616, art. 5 a) |
| ~06/02/2025 | Desarrolladores ven el **rechazo 10242 en homologación** por falta del campo, mientras producción sigue igual | NO VERIFICADO (foro) | https://groups.google.com/g/pyafipws/c/Ts7_jHioBwc |
| 17/03/2025 | **Manual v4.0**: se agregan `CondicionIVAReceptorId`, `CanMisMonExt` y `FEParamGetCondicionIvaReceptor` | Oficial | Manual v4.7/v4.8, historial |
| 06/04/2025 | El campo se puede enviar **de forma opcional** en producción | Oficial | Manual, changelog v4.0: "A partir del 6 de abril de 2025 podrá enviarse de forma opcional el campo … hasta tanto entre en vigencia su obligatoriedad … en cuyo momento pasará a rechazar la emisión de comprobantes sin este dato." |
| 15/04/2025 | Obligatorio según RG 5616, art. 5 a) | **POSTERGADO** en los hechos | BO |
| 15/04/2025 | Evento del WS (citado en un foro como código 39): "…se mantendra como un dato no excluyente hasta el 30/06/2025, inclusive. A partir del 1/07/2025 se rechazaran las solicitudes de emision de comprobantes sin este dato." | **POSTERGADO** (no se cumplió el rechazo del 01/07/2025) | https://groups.google.com/g/pyafipws/c/3STFCXNOW_o/m/z4aDS3cQBgAJ |
| sin fecha | Evento/mensaje citado por AfipSDK: "se mantendra como un dato no excluyente hasta el 30/11/2026 … A partir del 01/12/2026 se rechazaran las solicitudes de emision" | NO VERIFICADO literalmente (secundaria); coincide con el manual v4.8 | https://afipsdk.com/blog/factura-electronica-solucion-a-error-10242/ |
| 11/08/2026 | Una consultora afirma que la **v4.7** hacía el campo "estrictamente mandatorio" desde el **01/09/2026** y daba de baja 10245 y 825 | **CONTRADICHO** por la fuente oficial: el changelog de la v4.7 (01/09/2026) no menciona `CondicionIVAReceptorId`, y la v4.7 sigue listando 10245 como observación | https://www.signature.ar/novedades/actualizaci%C3%B3n-arca:-lanzamiento-del-wsfev1-v4.7-y-obligatoriedad-del-iva-receptor (secundaria) vs. manual v4.7 oficial |
| 08/09/2026 a 11/09/2026 | Prensa: ARCA manda comunicaciones a los contribuyentes; rechazo desde el 01/12/2026 | Coincide con el manual oficial | https://siap.blogdelcontador.com.ar/novedades/arca-rechazara-facturas-sin-condicion-iva-receptor-diciembre-2026/ ; https://www.iprofesional.com/impuestos/464294-arca-cambia-las-facturas-electronicas-y-hay-plazo-hasta-noviembre-para-evitar-rechazos |
| **01/12/2026** | **Manual v4.8**: "Será obligatorio el campo Condición Frente al IVA del receptor, atento a la entrada en vigencia reglamentada por la Resolución General N°5616. Por tal motivo los códigos de error 10245 para CAE y 825 para CAEA quedaran en desuso." | **ANUNCIADO** (oficial) | https://www.arca.gob.ar/fe/ayuda/documentos/wsfev1-RG-4291.pdf |

Conclusión: la fuente más autorizada es el **manual v4.8 oficial**, que fija la obligatoriedad efectiva en el **01/12/2026**. No se encontró una resolución general que posponga formalmente la fecha del 15/04/2025. Las prórrogas se comunicaron por eventos del WS y por el manual. Un artículo de gosocket menciona una "RG 5894/2026" junto con la v4.8, pero según otras fuentes esa RG trata sobre granos (RG 4310 / SISA), no sobre esto. Tomarlo como **NO VERIFICADO**.

### 5.3 Valores permitidos según la clase de comprobante

Tabla del manual v4.8, sección "Condición Frente al IVA del receptor", leída por posición en el PDF (pág. 202):

| Id | Descripción | A / ALEY | B | C | 49 (Bienes usados) |
|---|---|:-:|:-:|:-:|:-:|
| 1 | IVA Responsable Inscripto | X | | X | |
| 4 | IVA Sujeto Exento | | X | X | |
| 5 | Consumidor Final | | X | X | X |
| 6 | Responsable Monotributo | X | | X | |
| 7 | Sujeto No Categorizado | | X | X | |
| 8 | Proveedor del Exterior | | X | X | |
| 9 | Cliente del Exterior | | X | X | |
| 10 | IVA Liberado – Ley N° 19.640 | | X | X | |
| 13 | Monotributista Social | X | | X | |
| 15 | IVA No Alcanzado | | X | X | |
| 16 | Monotributo Trabajador Independiente Promovido | X | | X | |

Estructura de `FEParamGetCondicionIvaReceptor`: request con `Auth` y `ClaseCmp` opcional (A, ALEY, B, C o 49; si se omite, devuelve todas las combinaciones). Response con `ResultGet/CondicionIvaReceptor` y los campos `Id` int(3), `Desc` string(250) y `Cmp_Clase` string(5). Error 10244 si `ClaseCmp` no es válido.

Ojo: en el ejemplo de request del manual el elemento aparece como `ar:FEParamGetCondicionFrenteIvaReceptor`, mientras que la operación y el response se llaman `FEParamGetCondicionIvaReceptor`. **Hay que confirmarlo contra el WSDL.**

Regla especial (manual v4.5, validación 10272): si `DocTipo` = 31 (Fondo Común de Inversiones CNV, solo entidades financieras), `CondicionIVAReceptorId` tiene que ser **15**.

### 5.4 Códigos de error y observación

Del manual v4.7 (producción) y v4.8 (homologación). El cuerpo de las validaciones es igual en las dos; solo cambia el changelog.

| Código | Modo | Tipo | Texto (resumido) | Desde el 01/12/2026 |
|---|---|---|---|---|
| 10242 | CAE | Rechazo | Condición IVA del receptor no es un valor permitido | Sigue |
| 10243 | CAE | Rechazo | No es válida para la clase de comprobante informada | Sigue |
| 10245 | CAE | **Observación** | "resultará obligatorio conforme lo reglamentado por la RG 5616" | **En desuso** |
| 10246 | CAE | Rechazo | "es obligatorio conforme a lo reglamentado por la RG 5616" | Pasa a dispararse cuando falta el campo |
| 823 | CAEA | Rechazo | Valor no permitido | Sigue |
| 824 | CAEA | Observación | No es válida para la clase de comprobante | Sigue |
| 825 | CAEA | **Observación** | "resultará obligatorio…" | **En desuso** |
| 826 | CAEA | Rechazo | "es obligatorio…" | Pasa a dispararse cuando falta el campo |

Definición del campo (manual): `CondicionIVAReceptorId` Int(2), obligatorio "N". Texto: "Si el valor informado no es valido, para CAE rechazará y en CAEA observará. Si el valor no existe rechazará en ambos casos."

**Comportamiento sugerido para ArcaSim:** un "reloj normativo" configurable. Con fecha de proceso anterior al 01/12/2026, si falta el campo, CAE aprueba con Obs 10245 y CAEA con Obs 825. Desde el 01/12/2026, rechazo 10246 o 826.

### 5.5 Moneda extranjera (también viene de RG 5616)

- `CanMisMonExt` String(1), valores `S` o `N`. Si `MonId` = PES, no se informa o va `N` (10239 a 10241; CAEA 820 a 822).
- `MonCotiz`: si el pago es en la misma moneda extranjera, la cotización tiene que coincidir con la registrada por ARCA para el día hábil anterior a `CbteFch` (o a la fecha actual si `CbteFch` es posterior). Si no, se puede omitir (changelog v4.0, códigos 10038 y 726).
- Validación 10240: si se informa `MonCotiz`, "no podra superar en 1 a la cotizacion oficial".
- 10119 (todas las monedas distintas de PES): el tipo de cambio no puede ser menor al 2 % ni mayor al 400 % del orientativo de ARCA (v3.3, 04/01/2024).
- `FEParamGetCotizacion` acepta una fecha opcional desde la v4.0 (error 12002).

---

## 6. RG 4892/2020: código QR

### 6.1 Norma

RG 4892/2020, BO **24/12/2020** (https://www.boletinoficial.gob.ar/detalleAviso/primera/239173/20201224):

- **Art. 1:** obligación de incorporar un QR "que codificará los datos indicados en el micrositio 'Factura Electrónica' (https://www.afip.gob.ar/fe/qr)". No exime de mostrar los datos exigidos.
- **Art. 3:** en "Comprobantes en línea" y "Facturador Móvil", ARCA agrega el QR por su cuenta. **Con WebService, lo arma el emisor.**
- **Art. 5:** para WebService fue obligatorio de forma escalonada según la facturación de 2020. RI con más de $10M: desde el 01/03/2021. Más de $2M: 01/04/2021. Más de $500K: 01/05/2021. Resto de RI, exentos y monotributistas: **01/06/2021**. **VIGENTE para todos.**

### 6.2 Especificación técnica

Fuente: PDF oficial https://www.afip.gob.ar/fe/qr/documentos/QRespecificaciones.pdf. La página HTML `especificaciones.asp` da 404 al 01/10/2026.

- Texto a codificar: `{URL}?p={DATOS_CMP_BASE64}`.
- `{URL}` = **`https://www.arca.gob.ar/fe/qr/`** según el texto vigente del PDF. El **ejemplo del mismo PDF** usa `https://www.afip.gob.ar/fe/qr/?p=...`. Las dos URL responden HTTP 302 a `.../fe/qr/conceptos-generales.asp` cuando se piden sin navegador (prueba del 01/10/2026).
- `{DATOS_CMP_BASE64}` = JSON con los datos del comprobante en Base64 (el ejemplo oficial usa Base64 estándar con padding `=`). **NO VERIFICADO**: si el lector oficial acepta Base64URL.
- JSON, versión 1:

| Campo | Tipo | Obligatorio | Descripción | Ejemplo |
|---|---|---|---|---|
| `ver` | Numérico, 1 dígito | Sí | Versión del formato | `1` |
| `fecha` | full-date (RFC 3339) | Sí | Fecha de emisión | `"2020-10-13"` |
| `cuit` | Numérico, 11 dígitos | Sí | CUIT del emisor | `30000000007` |
| `ptoVta` | Numérico, hasta 5 dígitos | Sí | Punto de venta | `10` |
| `tipoCmp` | Numérico, hasta 3 dígitos | Sí | Tipo de comprobante | `1` |
| `nroCmp` | Numérico, hasta 8 dígitos | Sí | Número de comprobante | `94` |
| `importe` | Decimal, hasta 13 enteros y 2 decimales | Sí | Importe total, en la moneda del comprobante | `12100` |
| `moneda` | 3 caracteres | Sí | Moneda (tabla del sistema) | `"DOL"` |
| `ctz` | Decimal, hasta 13 enteros y 6 decimales | Sí | Cotización en pesos (1 si es PES) | `65` |
| `tipoDocRec` | Numérico, hasta 2 dígitos | De corresponder | Tipo de documento del receptor | `80` |
| `nroDocRec` | Numérico, hasta 20 dígitos | De corresponder | Número de documento del receptor | `20000000001` |
| `tipoCodAut` | string | Sí | `"A"` = CAEA, `"E"` = CAE | `"E"` |
| `codAut` | Numérico, 14 dígitos | Sí | CAE o CAEA | `70417054367476` |

Ejemplo oficial:

```json
{"ver":1,"fecha":"2020-10-13","cuit":30000000007,"ptoVta":10,"tipoCmp":1,"nroCmp":94,"importe":12100,"moneda":"DOL","ctz":65,"tipoDocRec":80,"nroDocRec":20000000001,"tipoCodAut":"E","codAut":70417054367476}
```

Texto codificado oficial:

```
https://www.afip.gob.ar/fe/qr/?p=eyJ2ZXIiOjEsImZlY2hhIjoiMjAyMC0xMC0xMyIsImN1aXQiOjMwMDAwMDAwMDA3LCJwdG9WdGEiOjEwLCJ0aXBvQ21wIjoxLCJucm9DbXAiOjk0LCJpbXBvcnRlIjoxMjEwMCwibW9uZWRhIjoiRE9MIiwiY3R6Ijo2NSwidGlwb0RvY1JlYyI6ODAsIm5yb0RvY1JlYyI6MjAwMDAwMDAwMDEsInRpcG9Db2RBdXQiOiJFIiwiY29kQXV0Ijo3MDQxNzA1NDM2NzQ3Nn0=
```

Para ArcaSim: el QR no pasa por WSFEv1, es responsabilidad del cliente. Si el simulador ofrece una página `/fe/qr/?p=` de verificación, tiene que decodificar este JSON y compararlo con lo autorizado.

---

## 7. Ley 27.743 y RG 5614/2024: transparencia fiscal al consumidor

- **Ley 27.743**, Título VII, publicada en el BO el 08/07/2024 (https://www.boletinoficial.gob.ar/detalleAviso/primera/310191/20240708). Sustituye el primer párrafo del art. 39 de la Ley de IVA. Hay que discriminar el IVA y los demás impuestos nacionales indirectos en las ventas a consumidores finales.
- **RG 5614/2024**, firmada el 12/12/2024 y publicada en el BO el **13/12/2024** (copia: https://www.consejosalta.org.ar/wp-content/uploads/ARCA-5614.pdf):
  - Art. 2, punto 5 (RG 1415, Anexo II, Ap. A, Tít. IV, inc. a): un RI que vende a exentos, no alcanzados o **consumidores finales** tiene que discriminar el impuesto que recae sobre la operación.
  - Art. 2, punto 7 (RG 1415, Anexo II, Ap. B, inc. g): **en el espacio inferior izquierdo** va el título **"Régimen de Transparencia Fiscal al Consumidor (Ley 27.743)"**, debajo el dato **"IVA Contenido"** y debajo **"Otros Impuestos Nacionales Indirectos"**, cada uno con su valor.
  - Art. 2, punto 4: hay que consignar los "Otros Impuestos Nacionales Indirectos" para las operaciones del art. 99 de la Ley 27.743 (así lo cita la RG).
  - Art. 5: agrega a RG 4291 art. 15 la obligación de mostrar esos datos en la representación gráfica (imagen o impresión). Si se usa el modelo de RG 3561, va en "OTRAS LEYENDAS" con el título "TRANSPARENCIA FISCAL".
  - Art. 6, cronograma: **empresas grandes** (RG 4367, art. 2) desde el **01/01/2025**. El resto puede desde el 01/01/2025 y **está obligado desde el 01/04/2025**. "Comprobantes en línea" y "Facturador Móvil" lo traen desde el 01/01/2025.
- Novedad de ARCA del 01/04/2025 (https://servicioscf.afip.gob.ar/publico/sitio/contenido/novedad/ver.aspx?id=4709): "bajo la leyenda 'Régimen de Transparencia Fiscal al Consumidor Ley 27.743', debe consignarse el impuesto al valor agregado (IVA) en una línea y los impuestos internos -bajo el nombre de 'Otros impuestos nacionales indirectos'- en otra."

**Impacto en WSFEv1:** **ninguno.** Los manuales v4.7 y v4.8 no mencionan "transparencia", "27.743" ni "5614", y no hay ningún campo nuevo.

**INFERENCIA:** el servicio ya pide el desglose de IVA en las facturas B. La validación 10070 dice: "Si el importe neto es mayor a cero, es obligatorio informar el array de iva". Por eso el "IVA Contenido" se puede calcular desde `Iva/AlicIva`. Los "Otros impuestos nacionales indirectos" (impuestos internos) van en `Tributos`, con el código que corresponda de `FEParamGetTiposTributos`.

**NO VERIFICADO:** si los monotributistas (clase C) tienen alguna obligación de mostrar transparencia fiscal. No se encontró una fuente oficial que lo aclare.

---

## 8. Factura de Crédito Electrónica MiPyME (Ley 27.440, RG 4367)

### 8.1 Cuándo aplica

- **Ley 27.440, art. 1:** "En todas las operaciones comerciales en las que una Micro, Pequeña o Mediana Empresa esté obligada a emitir comprobantes electrónicos originales (factura o recibo) **a una empresa grande** … se deberá emitir 'Facturas de Crédito Electrónicas MiPyMEs' … en reemplazo de los mencionados comprobantes. El régimen … será **optativo** en las operaciones comerciales entre Micro, Pequeñas o Medianas Empresas." (https://servicios.infoleg.gob.ar/infolegInternet/anexos/310000-314999/310084/norma.htm)
- Nota de Infoleg sobre el art. 1: por Res. SEPyME 219/2025 (BO 28/10/2025), **hasta el 31/10/2026** el régimen no aplica a los comprobantes cedidos según el art. 1618 del CCyC antes de ser cancelados, rechazados o aceptados. Vigente desde el 01/11/2025.
- **Monto mínimo VIGENTE: $5.549.862, desde el 14/04/2026** (Resolución 1/2026). Fuente oficial: https://servicioscf.afip.gob.ar/facturadecreditoelectronica/ y https://servicioscf.afip.gob.ar/facturadecreditoelectronica/conceptos/caracteristicas.asp ("El importe de la operación debe ser igual o superior a $5.549.862").
- Requisitos (misma página): las dos partes tienen el **Domicilio Fiscal Electrónico** constituido y el emisor registra una **CBU**.
- RG 4367/2018 (BO 20/12/2018): https://www.boletinoficial.gob.ar/detalleAviso/primera/198475/20181220
- Desde la RG 5866, las entidades financieras no pueden emitir FCE (validación 10251, manual v4.4).

### 8.2 Comprobantes 201 a 213

| Clase | Factura | Nota de débito | Nota de crédito |
|---|---|---|---|
| A | 201 | 202 | 203 |
| B | 206 | 207 | 208 |
| C | 211 | 212 | 213 |

Fuente: manual, validaciones 10007, 10157, 10172 y 10173.

### 8.3 Lo que valida WSFEv1 (manual v4.7/v4.8)

- **1 comprobante por request** (texto junto a 10001).
- Receptor: `DocTipo` = 80, con DFE habilitado (10161). Tiene que estar caracterizado como **GRANDE o haber optado por PyME**, con una actividad alcanzada (10180). No se acepta 23000000000 (10178).
- Emisor: validación 10000, sub-razón 10 ("NO SE ENCUENTRA REGISTRADA COMO PYME SEGÚN EL REGIMEN FCE") y sub-razón 11 (DFE).
- `CbteFch` de N-5 a N+1. En notas de débito y crédito, hasta 5 días antes y no anterior al comprobante asociado (sección 10).
- `FchVtoPago` es obligatorio en las facturas 201, 206 y 211 (10163) y tiene que ser ≥ la fecha más tardía entre `CbteFch` y hoy (10164).
- `Opcionales`:
  - **2101** = CBU de 22 dígitos (10165). Obligatorio en las facturas (10168).
  - **2102** = alias de 6 a 20 caracteres (10166).
  - **22** = anulación, `S` o `N` (10167). Obligatorio en notas de débito y crédito (10173) y prohibido en facturas (10171).
  - **27** = `SCA` (Sistema de Circulación Abierta) o `ADC` (Agente de Depósito Colectivo) (10214 y 10215). Obligatorio en facturas (10216).
  - Las notas de débito y crédito **no** informan CBU, alias ni 27 (10172).
  - Los comprobantes que no son FCE no pueden usar 2101, 2102, 22 ni 27 (10169).
  - El 23 (referencia comercial) dejó de ser exclusivo de FCE en la v2.19.
- Notas de débito y crédito FCE: siempre asocian **una** factura del mismo tipo de clase (201, 206 o 211) o un remito (10156 y 10157), con su fecha (10158 a 10160). Moneda igual a la del asociado o PES (10181).
- Observación 10188: si por la categorización del emisor y del receptor correspondía una FCE y se emitió una factura común (1, 4, 6, 9, 11, 15), el comprobante igual se aprueba pero lleva la observación.
- La aceptación, el rechazo y la transmisión **no** pasan por WSFEv1: se hacen en el "Registro de Facturas de Crédito Electrónicas MiPyME". FAQ del emisor: https://servicioscf.afip.gob.ar/facturadecreditoelectronica/preguntasFrecuentes/emisor-factura.asp
- **Para homologación**, ARCA publica un listado de 1201 CUIT de "empresas grandes" de prueba: https://servicioscf.afip.gob.ar/facturadecreditoelectronica/documentos/Pruebas-homologacion-WS-FCE.xlsx (columnas: CUIT, DENOMINACION, ACTIVIDAD PRINCIPAL, inicio desde). Le sirve a ArcaSim como padrón semilla de receptores FCE.

---

## 9. CAE y CAEA; tipos de punto de venta

### 9.1 CAE (modalidad principal)

- RG 4291, art. 12: un CAE por registro. Métodos WSFEv1: `FECAESolicitar` (manual, sección 2.4.1).
- Desde el 01/08/2026 el CAE es **la modalidad general obligatoria** (RG 5782 y su considerando).

### 9.2 CAEA (solo contingencia desde el 01/08/2026)

RG 5782/2025, firmada el 28/10/2025 y publicada el 30/10/2025, **vigente desde el 01/08/2026** por RG 5852/2026, art. 1. Abroga RG 2926 (art. 7).

| Art. | Regla |
|---|---|
| 1 | Procedimiento **excepcional para contingencias** (Título III de RG 4290). El CAEA reemplaza al CAE. |
| 2 | Se pide por WebService ("RG N° 4.291" para la mayoría; los sujetos de RG 2904 usan WSMTXCA). |
| 3 | Comprobantes alcanzados: facturas, recibos y notas de crédito y débito clase A, "A PAGO EN CBU INFORMADA", "A OPERACIÓN SUJETA A RETENCIÓN", B y C. |
| 4 | **Un CAEA por contribuyente y por quincena**: del 1 al 15 y del 16 al último día del mes. Se pide desde los **5 días corridos anteriores** al inicio de la quincena o **dentro** de ella. |
| 5 | Puntos de venta específicos para CAEA, **asociados al mismo domicilio de la modalidad principal y a la misma condición de IVA**. |
| 6 | Se informa cada punto de venta con **fecha y hora de generación** de cada comprobante. También se informan los PV no usados. Plazo: desde el día siguiente al inicio de la quincena **hasta el octavo día corrido** después de que termina. Antes, con RG 2926 art. 11, eran 5 días. |

- RG 5785/2025 (BO 03/11/2025, vigente desde el 01/08/2026) modifica RG 4290. El CAEA es **la primera opción de contingencia** cuando se emite por WebService (art. 16 a). Solo se usa "en condiciones de excepcionalidad" (art. 18). Si el contribuyente no rinde los CAEA o no presenta el F. 2051, ARCA puede negarle el CAEA (art. 25).
- RG 5852/2026, art. 3: desde el 01/06/2026 no hace falta adherirse para usar el CAEA en contingencia, y se rechazan los pedidos nuevos de CAEA como método principal.

En WSFEv1 (manual v4.6, 01/08/2026, y v4.7/v4.8):

- Todos los PV CAEA pasan a ser de **Contingencia**. Tienen que compartir domicilio con un PV CAE o con un Controlador Fiscal de nueva generación.
- `FECAEASolicitar`: la validación **15016** es excluyente ("según la RG 5782/2025 el régimen de CAEA se aplica exclusivamente a situaciones de contingencia … solo se permitirá su uso en domicilios que cuenten con al menos un punto de venta activo bajo la modalidad CAE o Controlador Fiscal de Nueva Tecnología"). Observaciones 15014 (incumplimiento de rendición: debe 2 quincenas consecutivas o 4 alternadas; devuelve "periodo;orden;puntoVenta"), 15015 (DFE), **15017** (PV CAEA sin domicilio vinculado) y **15018** (recordatorio de RG 5782 y de la fecha y hora de generación).
- `FECAEARegInformativo`: **`CbteFchHsGen`** String(14), formato `yyyymmddhhmiss`. Obligatorio en los PV de contingencia CAEA (1440) y con formato validado (1441).
- El changelog de la v4.6 lista como nuevos solo 15016 y 15017, pero el cuerpo del manual también tiene 15018.

### 9.3 Tipos de punto de venta que el servicio reconoce

- 10004 / 11000: `PtoVta` entre 1 y 99998. Desde la v2.12 (01/10/2018) el punto de venta tiene 5 dígitos.
- 10005: "El punto de venta informado debe estar dado de alta y ser del tipo **RECE**".
- 11002: "Debe ser un punto de venta habilitado en este WS".
- 1444 (CAEA): para A, B y "A con leyenda", los PV válidos son "CAEA - Fact. Elect. (RECE) - RI IVA" y "CAEA - Fact. Elect. (RECE) - RI IVA - Contingencias".
- `FEParamGetPtosVenta` devuelve `Nro` (int 5), `EmisionTipo` ("CAE" o "CAEA"), `Bloqueado` (S/N) y `FchBaja`.
- Seguros de caución (RG 5866 y manual v4.7): PV del tipo **SEGWS** (10273 a 10282). Además, `wsseg` (RG 2668) sigue disponible hasta el 31/12/2026 (RG 5866, art. 4).

---

## 10. Ventanas de fecha (`CbteFch` y otras)

| Caso | Regla del servicio (manual v4.7/v4.8) | Base normativa |
|---|---|---|
| Concepto 1 (Productos) | `CbteFch` nulo o en el rango **N-5 a N+5**, con N = fecha de envío del pedido. "La misma no podrá exceder el mes de presentación" (10016). Si es posterior a N, tiene que ser del **mismo mes** (10152). | RG 4291, art. 9 (5 días corridos; mismo mes calendario si la transferencia es anterior) |
| Conceptos 2 y 3 (Servicios / Productos y Servicios) | Nulo o **N-10 a N+10** (10016). `FchServDesde`, `FchServHasta` y `FchVtoPago` obligatorios, formato `yyyymmdd`. `FchServHasta` ≥ `FchServDesde`. `FchVtoPago` ≥ fecha del comprobante. | RG 4291, art. 9 (10 días corridos) |
| FCE factura | **N-5 a N+1**. Si es posterior a N, del mismo mes (10152). | Manual |
| FCE nota de débito o crédito | Hasta 5 días antes y ≥ la fecha del comprobante asociado | Manual |
| Todos | `CbteFch` ≥ la fecha del último comprobante emitido para ese tipo y punto de venta (10016) | Correlatividad, RG 4291 art. 11 |
| Sin `CbteFch` | Se toma la fecha de proceso | RG 4291, art. 9, último párrafo |
| Lote (`CbteDesde` < `CbteHasta`) | Solo B por debajo del umbral de identificación. `CantReg` entre 1 y 9998 (10001), con el máximo real que devuelve `FECompTotXRequest`. | RG 4291, art. 8 b) |

Para el simulador, N tiene que ser **la fecha de proceso del simulador**, configurable para poder probar los bordes de mes.

**NO VERIFICADO:** con qué zona horaria y a qué hora de corte ARCA calcula N. Se asume la hora de Argentina (UTC-3).

---

## 11. Historial de versiones del manual WSFEv1 (RG 4291)

Fuente: "Historial de modificaciones" de los PDF oficiales v4.7 (https://www.arca.gob.ar/ws/documentacion/manuales/manual-desarrollador-ARCA-COMPG.pdf) y v4.8 (https://www.arca.gob.ar/fe/ayuda/documentos/wsfev1-RG-4291.pdf). Todas las ediciones son de "SDG SIT/DIF".

Nota de método: en el PDF la tabla tiene la versión en una celda y la descripción en otra. Las filas se reconstruyeron por la posición vertical del texto. La etiqueta de cada versión queda una línea debajo del primer renglón de su descripción, y la regla se cumple en todas las páginas. Algunas fechas están fuera de orden en el original (la 2.5 dice 01-04-2014).

| Ver. | Fecha | Cambios |
|---|---|---|
| 0.1 | 08/09/2010 | Versión inicial. |
| 1.1 | 18/03/2011 | URL de homologación y producción. "Operaciones según la RG" (métodos CAE vs CAEA). Se acepta receptor no categorizado. Correcciones de errores y validaciones en `FECAESolicitar` y `FECAEARegInformativo`. Lógica de `Opcionales`. Anexo 1 de códigos. |
| 2.0 | 15/04/2011 | Comprobantes **C** (RG 3067/2011), Anexo 2. El código 1413 pasa de excluyente a no excluyente. |
| 2.1 | 22/07/2013 | **Bienes usados**: 10000 (empadronamiento), alta 10075 a 10085, modificaciones en 10007, 10012, 10015, 10043 a 10048 y 10068. |
| 2.2 | 03/10/2014 | Bienes usados: los `Opcionales` 51, 52 y 53 pasan a 91, 92 y 93. |
| 2.3 | 09/10/2014 | Alícuotas de la Ley 26.982. |
| 2.4 | 22/10/2014 | RG 3668 por `Opcionales`: alta 10086 a 10095. |
| 2.5 | 01/04/2014 (sic) | Comprobantes C para **exentos** en IVA: alta 10096. |
| 2.6 | 01/07/2015 | Comprobantes **M** y opcionales de RG 3749: modificaciones en 10066, 1100 y en los códigos de M; alta 10097 a 10099 y 10110 a 10115. Validaciones 1427 y 1428. Canales de atención. |
| 2.7 | 01/01/2016 | 10017 pasa a no excluyente. RG 3779: alta 10116 a 10118. |
| 2.8 | 01/09/2016 | `<Observaciones>` en `FECAEASolicitarResult`. Alta 15100, 1429 a 1431. Baja 1428 y 15013. |
| 2.9 | 13/03/2017 | Modificaciones en 10040, 806 y 807. Alta 10119 a 10122 y 808 a 811 (comprobantes asociados 88 y 991). RG 4004-E: alta 10123 a 10132. |
| 2.10 | 09/08/2017 | Estructura **`Compradores`** (receptores múltiples, 10133 a 10150). Alta 11002, 10151 y 1432. |
| 2.11 | 01/08/2018 | **C por CAEA** (RG 4291). El CAEA se puede pedir dentro de la quincena. Validaciones de B > $10.000 (RG 4291) en 1417 a 1422, 10014 y 10015. Recibos A, B, C y M. Altas 1433 a 1445, 10152, 10071, 15015 y 15014. Bajas 1430, 15100 y 15007. |
| 2.12 | 01/10/2018 | **Punto de venta de 4 a 5 dígitos.** `FEParamGetPtosVenta.Nro` pasa de short a int. Nuevo header en los responses. Modificaciones en 1206, 1300, 10004, 10101 y 11000. 10119 pasa de 50 % a 20 %. |
| 2.13 Beta 1 | 01/11/2018 | **Factura de Crédito** (FCE). Alta 10153 a 10185. Modificaciones en códigos CAE y CAEA. |
| 2.13 Beta 2 | 16/01/2019 | Aclaraciones sobre `CbteFch`, `FchVtoPago` y `CbtesAsoc.CbteFch`. Bajas 10000 orden 12, 10179 y 10182. Modificaciones en 10000 orden 4, 10016, 10040, 10184 y 10185. Alta 10186 a 10188. |
| 2.13 | 11/03/2019 | Referencia comercial (FCE) en `Opcionales`. Alta 10189 a 10192. Modificaciones en 10154, 10169 y 10170. |
| 2.14 | 01/05/2019 | Alta 10193, baja 10185. Validaciones de B > $10.000 según **RG 4444/2019** (monto = `ImpTotal × cotización`). |
| 2.15 | 01/07/2019 | Alta 10194 (CAE). CAEA: modificaciones en 10003, 700, 711, 807 y 808; alta 812, 813 y 1446 a 1488. |
| 2.16 | 01/11/2019 | Alta 10195 (CAE) y 1489 (CAEA). |
| 2.17 | 26/02/2020 | Topes de facturación según RG 4444/2019 (1415, 1417 a 1419, 1422, 10014 y 10015). |
| 2.18 Beta 1 | 01/05/2020 | **RG 4540**: estructura **`PeriodoAsoc`**. Alta 10196 a 10199, 10203 a 10213 y 1490 a 1504. |
| 2.18 | 18/06/2020 | RG 4540: 10209 y 1500 pasan de rechazo a observación. |
| 2.19 | 19/10/2020 | El opcional 23 (referencia comercial) se abre a todos los comprobantes. RG 3668 en CAEA (1505 a 1512). Baja 10087 y 10093. 10195 pasa a **rechazar** si el receptor es apócrifo. |
| 2.20 | 29/01/2021 | FCE: opcional 27 (SCA/ADC). Alta 10214 a 10216 y 1513 a 1515. |
| 2.21 | 01/06/2021 | **Ley 27.618** (monotributistas que reciben A): alta 10217 y 1516. |
| 2.22 | 07/09/2021 | Ajustes en 10177 y 1474. |
| 3.0 Beta 1 | 15/10/2022 | RG 5259 (remitos cárnicos) y RG 5264 (harineros): método **`FEParamGetActividades`**, estructura **`Actividades`**, alta 10218 a 10232 y 16000 a 16014. Cárnicos: solo se observa entre el 15/11/2022 y el 14/12/2022 y se rechaza desde el 15/12/2022. Harina: se observa entre el 01/02/2023 y el 28/02/2023 y se rechaza desde el 01/03/2023. |
| 3.0 | 01/12/2022 | Versión final de lo anterior. |
| 3.1 | 01/01/2023 | 10119: el tipo de cambio va del 20 % al 200 % del oficial. |
| 3.2 | 12/12/2023 | 10119: hasta el 400 %. |
| 3.3 | 04/01/2024 | 10119: del 2 % al 400 %. **Alertas y monitoreo**: alta 10234 a 10236 (CAE) y 814 a 817 (CAEA). |
| 3.4 | 17/05/2024 | Alertas: alta 10237 y 10238 (CAE), 818 y 819 (CAEA). |
| **4.0** | **17/03/2025** | Campos nuevos **`CanMisMonExt`** y **`CondicionIVAReceptorId`**. Alta 10239 a 10243, 10245 y 10246 (CAE) y 820 a 826 (CAEA). Nueva regla de `MonCotiz` (10038 y 726). `FEParamGetCotizacion` acepta fecha opcional (12002). Método nuevo **`FEParamGetCondicionIvaReceptor`** (10244). Campo opcional desde el 06/04/2025. |
| **4.1** | **01/12/2025** | **Reemplazo de la clase M** (RG 5762). Se modifica `FEParamGetCondicionIvaReceptor` y cambian las descripciones de 814, 815, 1444, 1516, 10000, 10007, 10012, 10013, 10017, 10061, 10063, 10149, 10217, 10234 y 10244. |
| 4.2 | 01/02/2026 | Alertas: alta 10247, 10248 y 10249 (CAE) y 827 a 829 (CAEA). Son CUIT receptora inactiva, "sujeto no confiable" en Seguridad Social y receptor fallecido. |
| 4.3 | 01/06/2026 | Opcionales de los tipos **63 y 64**: alta 1517 a 1526 (CAEA). Modificaciones en 10068, 700, 807, 1426 y 15000. 15001 queda en desuso. |
| 4.4 | 01/07/2026 | Entidades financieras y seguros (**RG 5866**): alta 10251 (no pueden emitir FCE). |
| 4.5 | 02/07/2026 | `DocTipo` **31** (Fondo Común de Inversiones CNV): alta 10270 a 10272. |
| 4.6 | 01/08/2026 | **RG 5782**: los PV CAEA pasan a contingencia y se vinculan a un domicilio CAE o CF. `CbteFchHsGen` obligatorio. Modificación de 15014 y alta 15016 y 15017. |
| **4.7** | **01/09/2026** | **Seguros de caución** por WSFEv1 (opcionales 2901 y 2902, PV SEGWS): alta 10273 a 10282 y modificación de 10054. B a **Sujeto No Categorizado** exige la percepción 13 (RG 2126): alta 10283 y 1527, modificación de 10067 y 1425. Alertas: alta 10284 y 1528. **Versión de producción hoy.** |
| **4.8** | **01/12/2026** | **`CondicionIVAReceptorId` obligatorio** (RG 5616). 10245 y 825 quedan en desuso. **ANUNCIADO; publicado como manual de homologación externa.** |

---

## 12. Ambiente de homologación

### 12.1 Cómo se obtiene un certificado de prueba (WSASS)

Fuentes: https://www.afip.gob.ar/ws/documentacion/certificados.asp, "WSASS: Cómo adherirse al servicio" (publicación 19.9.25, https://www.afip.gob.ar/ws/WSASS/WSASS_como_adherirse.pdf) y "Manual del Usuario del WSASS" (publicación 19.10.1, https://www.afip.gob.ar/ws/WSASS/WSASS_manual.pdf).

1. Entrar a arca.gob.ar con **Clave Fiscal de persona física** (no de persona jurídica), **nivel 2 o superior**. "El servicio WSASS no es delegable."
2. Mis Servicios → "Administrador de Relaciones de Clave Fiscal" → "Adherir servicio" → "Servicios interactivos" de ARCA → **WSASS** → "Continuar".
3. Cerrar la sesión y volver a entrar. WSASS aparece en Mis Servicios.
4. Generar la clave y el CSR con OpenSSL:
   ```
   openssl genrsa -out MiClavePrivada.key 2048
   openssl req -new -key MiClavePrivada.key -subj "/C=AR/O=Empresa/CN=Sistema/serialNumber=CUIT nnnnnnnnnnn" -out MiPedidoCSR.csr
   ```
   En `serialNumber` va "CUIT", un espacio y los 11 dígitos.
5. WSASS → "Nuevo Certificado": alias (nombre simbólico del DN), CUIT (la del usuario conectado, no se puede editar) y el CSR en formato PKCS#10. Devuelve un X.509 en PEM. El DN queda como `SERIALNUMBER=CUIT nnnnnnnnnnn, CN=<alias>` "por diseño y no puede modificarse". **"Los certificados creados no pueden ser eliminados, ni aún después de su fecha de expiración."**
6. Opcional, PFX: `openssl pkcs12 -export -inkey MiClavePrivada.key -in MiCertificado.pem -out MiCertificado.pfx`
7. **Asociarlo a wsfev1:** WSASS → "Crear Autorización a Servicio". Se eligen el alias, la **CUIT representada** (física o jurídica) y el servicio (wsfev1). Para representar a otra CUIT se usa el mismo formulario.
8. Las cadenas de certificación de homologación se publican para 2014-2024 y **2022-2034**.

Producción (para comparar): los certificados se gestionan en "Administración de Certificados Digitales" y la asociación al servicio en "Administrador de Relaciones de Clave Fiscal". Guías: https://www.afip.gob.ar/ws/WSAA/wsaa_obtener_certificado_produccion.pdf y https://www.afip.gob.ar/ws/WSAA/wsaa_asociar_certificado_a_wsn_produccion.pdf. Cadena de producción vigente: 2024-2035 (desde el 03/01/2024).

### 12.2 URLs y contactos (manual v4.7/v4.8, sección "Dirección URL" y "Canales de Atención")

| | Homologación | Producción |
|---|---|---|
| WSFEv1 | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx` (`?WSDL`) | `https://servicios1.afip.gov.ar/wsfev1/service.asmx` (`?WSDL`) |
| WSAA (LoginCms) | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` | `https://wsaa.afip.gov.ar/ws/services/LoginCms` |

Las URL de WSAA salen de https://www.afip.gob.ar/ws/documentacion/wsaa.asp.

Contactos: certificados y accesos de testing en http://www.arca.gob.ar/ws/ y soporte-ws-testing@arca.gob.ar. Funcional de WSFEv1: wsfev1@arca.gov.ar. Producción: sri@arca.gov.ar. Normativa: facturaelectronica@arca.gov.ar.

TLS: ARCA anuncia que deja de aceptar TLS 1.0 y 1.1 y exige TLS 1.2. Las fechas por host figuran como "Próximamente" (https://www.afip.gob.ar/ws/documentacion/cronograma-TLS.asp).

### 12.3 Diferencias conocidas entre homologación y producción

No hay un documento oficial que las liste. Todo lo que sigue es **NO VERIFICADO** y viene de foros de desarrolladores.

| Tema | Comportamiento reportado | Fuente |
|---|---|---|
| Puntos de venta | Reportes en conflicto. En 2018: "solo se pueden usar los mismos números de PV creados en producción". En 2023 y 2024: `FEParamGetPtosVenta` devuelve **602 "Sin Resultados"** y "En Homologacion no hay puntos de venta, debes colocar cualquier valor". | https://groups.google.com/g/pyafipws/c/oGQFQqDq8sI (2018) ; https://groups.google.com/g/pyafipws/c/PqKowNnwdkw (2023-2024) |
| Datos de padrón | "Homologación no tiene coherencia en los datos": el ambiente es funcional y no refleja padrones reales. | mismo hilo de 2023-2024 |
| Adelanto de cambios | Homologación activa las validaciones nuevas antes que producción. Ejemplo: en febrero de 2025 ya exigía `CondicionIVAReceptorId` (10242) y producción no. Hoy publica el manual v4.8 mientras producción usa la v4.7. | https://groups.google.com/g/pyafipws/c/Ts7_jHioBwc ; https://www.afip.gob.ar/ws/documentacion/homologacion-externa.asp (oficial) |
| FCE | ARCA publica un listado de "empresas grandes" para probar FCE en homologación (oficial, ver la sección 8.3). | oficial |

Consecuencia para ArcaSim: conviene un **perfil "homologación"** permisivo con los PV y los padrones, y un **perfil "producción"** estricto, que exija PV dados de alta con tipo RECE o CAEA y CUIT existentes.

---

## 13. Cronología de cambios 2024-2026

| Fecha | Cambio | Estado al 01/10/2026 |
|---|---|---|
| 04/01/2024 | Manual 3.3: 10119 entre el 2 % y el 400 %; alertas 10234 a 10236 | VIGENTE |
| 17/05/2024 | Manual 3.4: alertas 10237 y 10238 | VIGENTE |
| 13/12/2024 | RG 5614, transparencia fiscal (gráfica). Obligatoria para todos desde el 01/04/2025 | VIGENTE |
| 18/12/2024 | RG 5616: condición IVA del receptor y cotización en moneda extranjera | VIGENTE (obligatoriedad efectiva ANUNCIADA para el 01/12/2026) |
| 17/03/2025 | Manual 4.0: `CondicionIVAReceptorId`, `CanMisMonExt`, `FEParamGetCondicionIvaReceptor` | VIGENTE |
| 06/04/2025 | Producción acepta `CondicionIVAReceptorId` como opcional | VIGENTE |
| 01/07/2025 | Rechazo anunciado por evento del WS | POSTERGADO |
| 29/05/2025 | RG 5700: consumidor final con umbral de $10.000.000 | VIGENTE |
| 01/12/2025 | RG 5762 y manual 4.1: fin de la clase M; nace ALEY | VIGENTE |
| 01/02/2026 | Manual 4.2: alertas 10247 a 10249 | VIGENTE |
| 13/02/2026 | RG 5824, régimen de comprobantes | ABROGADA por RG 5866 antes de regir |
| 14/04/2026 | FCE: monto mínimo de $5.549.862 (Res. 1/2026) | VIGENTE |
| 01/06/2026 | Manual 4.3: opcionales de los tipos 63 y 64 en CAEA | VIGENTE |
| 01/06/2026 → 01/08/2026 | RG 5782 y 5785 (CAEA solo contingencia), postergadas por RG 5852 | VIGENTE desde el 01/08/2026 |
| 01/07/2026 | RG 5866: nuevos obligados, PV vinculados, nuevo texto de consumidor final. Manual 4.4 y 4.5 | VIGENTE, con cronograma hasta el 01/03/2027 |
| 01/08/2026 | Manual 4.6: PV CAEA de contingencia, `CbteFchHsGen` obligatorio | VIGENTE |
| 01/09/2026 | Manual 4.7: seguros de caución, B a No Categorizado con percepción 13 | VIGENTE |
| 01/10/2026 | RG 5866: entidades financieras (leasing y comercio exterior) e inciso j) | VIGENTE desde hoy |
| 31/10/2026 | Fin de la excepción FCE para facturas cedidas (Res. SEPyME 219/2025) | ANUNCIADO |
| 30/11/2026 | Entidades financieras exceptuadas de informar percepciones (RG 5866, art. 4) | ANUNCIADO (fin de la excepción) |
| **01/12/2026** | **Manual 4.8: `CondicionIVAReceptorId` obligatorio (rechazo 10246 y 826)**. RG 5866: préstamos y liquidaciones de tarjetas | ANUNCIADO |
| 31/12/2026 | Último día de `wsseg` (RG 2668) para seguros de caución | ANUNCIADO |
| 01/03/2027 | RG 5866: coaseguros y liquidaciones a comercios | ANUNCIADO |

---

## 14. Conflictos entre fuentes

1. **Obligatoriedad de `CondicionIVAReceptorId`.** RG 5616 dice 15/04/2025. Un evento del WS anunció el 01/07/2025. Una consultora dijo 01/09/2026. El **manual oficial v4.8 dice 01/12/2026**. Vale el manual v4.8: es oficial, es el más reciente y coincide con la prensa especializada de septiembre de 2026. El changelog oficial de la v4.7 contradice el dato del 01/09/2026.
2. **URL del QR.** El PDF oficial define `https://www.arca.gob.ar/fe/qr/` y su ejemplo usa `https://www.afip.gob.ar/fe/qr/?p=`. RG 4892 cita `https://www.afip.gob.ar/fe/qr`. Las dos responden igual. Recomendación: el simulador acepta las dos y genera la de ARCA, que es la del texto vigente del PDF.
3. **Umbral anterior a RG 5700.** Unos medios citan $250.000 / $400.000 y otros $208.644 / $417.288. Es historia y no cambia nada hoy: el valor vigente es $10.000.000 en todas las fuentes.
4. **Puntos de venta en homologación.** Hay reportes de 2018 y de 2023-2024 que se contradicen (sección 12.3). Vale el más reciente, pero no hay fuente oficial.
5. **Códigos nuevos de CAEA en la v4.6.** El changelog lista 15016 y 15017, y el cuerpo del manual también trae 15018.
6. **Nombre del elemento en el request de `FEParamGetCondicionIvaReceptor`.** El manual muestra `FEParamGetCondicionFrenteIvaReceptor` en el ejemplo. Hay que confirmarlo en el WSDL.

---

## 15. Preguntas abiertas / no verificado

1. **Monto exacto que usa hoy WSFEv1** para 10014 y 10015 (debería ser $10.000.000) y si la comparación es `>=` o `>`. El manual solo dice "según RG4444".
2. **Comportamiento exacto en homologación** hoy: si ya rechaza la falta de `CondicionIVAReceptorId` con 10246 (manual v4.8) o con 10242 (reporte de 2025), y si acepta cualquier PV.
3. **Texto de los eventos (`Events/Evt`)** que devuelve producción hoy sobre RG 5616. Solo hay citas secundarias.
4. **Descripciones actuales de `FEParamGetTiposCbte`** para 51 a 54 tras RG 5762. No se pudo consultar el servicio en vivo.
5. **Zona horaria y hora de corte** para calcular N en las ventanas de `CbteFch`.
6. **Base64 vs Base64URL** en el QR, y si el validador de ARCA tolera la falta de padding.
7. **Transparencia fiscal para monotributistas (clase C):** si está alcanzada. No se encontró una aclaración oficial.
8. **Resolución que pospone la RG 5616.** No se encontró. Se mencionó una "RG 5894/2026" que parece no estar relacionada.
9. **Plazo de aceptación tácita de la FCE** y operación del Registro FCE: no son de WSFEv1 y no se investigaron a fondo.
10. **Validez del CAE (`CAEFchVto`)** y del token del WSAA: quedan para los documentos técnicos de WSFEv1 y WSAA.
