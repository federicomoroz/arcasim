# wDigDepFiel

"Digitalización Depositario Fiel" (RG 2570). Lo usan los PSAD (prestadores de servicios de archivo y digitalización) y los despachantes que se auto-archivan para informar dos hitos del legajo de una declaración aduanera: que el PSAD **recibió y aceptó** la documentación (`AvisoRecepAcept`, solo PSAD) y que la documentación **quedó digitalizada** (`AvisoDigit`). Después del aviso de digitalización el legajo pasa a `DIGI` y el declarante recibe un aviso por e-ventanilla (manual, "Funcionalidad").

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/documentos/ManualDesaDigDepFiel.pdf`, versión 1.6 del 01/07/2014. Descargado el 2026-10-02 (HTTP 200). Las primeras 9 páginas no tienen número extraíble: se citan secciones; desde la 10 se citan páginas impresas.
- WSDL de homologación: `docs/arca/wsdl/wDigDepFiel-homologacion.wsdl`, de `https://testdia.afip.gov.ar/Dia/Ws/wDigDepFiel/wDigDepFiel.asmx?WSDL` (2026-10-02). Esquema inline, sin XSD aparte.
- WSDL de producción: `docs/arca/wsdl/wDigDepFiel/wDigDepFiel-produccion.wsdl`. **Difiere**: a `AvisoRecepAcept` le falta el último parámetro, `hashing`.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:15 (-03:00). **Ojo**: los dos métodos de negocio crean estado; solo se llamaron con token inválido, que corta antes de tocar datos.
- Catálogo: tipos de agente `PSAD` y `DESP`, RG 3069/11. README: `https://www.afip.gob.ar/ws/documentos/READMEDF.txt`. El README da como producción un host viejo, `servicios1.afip.gov.ar`, que el 2026-10-02 respondió **HTTP 503** "Service Unavailable" para todas las rutas DIA probadas: la producción vigente es `servicios3.arca.gob.ar`.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA", variante con `Recibo` reducido: `codError` + `descError` en minúscula, sin `DescAdicErr` | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/Dia/Ws/wDigDepFiel/wDigDepFiel.asmx` | `soap:address`; manual "Especificaciones del servicio" |
| Endpoint producción | `https://servicios3.arca.gob.ar/Dia/Ws/wDigDepFiel/wDigDepFiel.asmx` | WSDL de producción; manual |
| Namespace | `ar.gov.afip.dia.serviciosWeb.wDigDepFiel` (`serviciosWeb` con W mayúscula; los ejemplos del manual usan `serviciosweb`) | WSDL |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `wDigDepFielSoap` (1.1) y `wDigDepFielSoap12` (1.2) | WSDL |
| SOAPAction | `ar.gov.afip.dia.serviciosWeb.wDigDepFiel/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | `wDigDepFiel` (homologación y producción) | `https://www.afip.gob.ar/ws/documentos/READMEDF.txt` |
| Operaciones | 3: `AvisoRecepAcept`, `AvisoDigit`, `Dummy` | WSDL |

## Autenticación

El parámetro se llama `autentica` (minúscula) y `Autenticacion` es **plano**, con este orden: `Cuit`, `TipoAgente`, `Rol` (0..1), `Token`, `Sign` (1..1, nillable). Los demás parámetros van **sueltos** en el wrapper, sin objeto contenedor. El manual pide `TipoAgente` `DESP` o `PSAD` y `UsuRol` `EXTE` ("Propiedades de la estructura Autenticacion"); en el WSDL es `Rol`.

Tabla del manual ("Errores / descripción, autenticación del usuario"): la estándar de la DIA (7004, 7005, 7006, 7007, 7008, 7013, 7014, 6005, 6006, 6003).

Observado con `AvisoRecepAcept` (`nroLegajo`=`X`, `fechaHoraAcept`=`2026-10-02T10:00:00`; homologación, 2026-10-02):

| Caso | HTTP | Respuesta |
|---|---|---|
| `Token`=`abc` | 200 | `<codError>7004</codError><descError>ID MWE : 73329203</descError>` |
| `Token` y `Sign` vacíos | 200 | `<codError>7007</codError><descError>Debe ingresar el Token y Firma.</descError>` |
| Base64 de `<sso><id/></sso>` | 200 | `7004` / `ID MWE : 73329204` |
| `sso` 2.0 bien formado, `Sign` falso | 200 | `7006` / `Debe ingresar la Firma.` |
| Sin `autentica` | 500 | `soap:Fault` `soap:Server` "El servidor no puede procesar la solicitud. ---&gt; Referencia a objeto no establecida como instancia de un objeto." |

Como en `wgesTabRef` (mismo estilo de autenticación plana), un token que no es Base64 da **7004** y no 7008, y el texto lleva espacio antes de los dos puntos (`ID MWE : n`).

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><AvisoRecepAceptResponse xmlns="ar.gov.afip.dia.serviciosWeb.wDigDepFiel"><AvisoRecepAceptResult><codError>7007</codError><descError>Debe ingresar el Token y Firma.</descError></AvisoRecepAceptResult></AvisoRecepAceptResponse></soap:Body></soap:Envelope>
```

## Operaciones

| Operación | Propósito | Entrada (tras `autentica`, parámetros sueltos) | Salida | Estado |
|---|---|---|---|---|
| `AvisoRecepAcept` | El PSAD informa que recibió y acepta digitalizar | `nroLegajo`, `cuitDeclarante`, `cuitPSAD`, `cuitIE`, `codigo`, `fechaHoraAcept` (dateTime 1..1), `ticket`, `sigea`, `nroGuia`, `fechaGeneracion` (dateTime 1..1), `indLugarFisico`, `idEnvio`, `fechaDespacho` (dateTime 1..1), `cantFojas` (int 1..1), `nroReferencia`, `hashing` (solo homologación) | `AvisoRecepAceptResult` (`Recibo` 1..1): `codError`, `descError` | Crea. Clave: `nroLegajo` + `codigo` (+ `ticket` para `001`) |
| `AvisoDigit` | Informa que la documentación quedó digitalizada | `nroLegajo`, `cuitDeclarante`, `cuitPSAD`, `cuitIE`, `cuitATA`, `codigo`, `url`, `familias/Familia[]` (`codigo`, `cantidad`), `ticket`, `hashing`, `cantidadTotal` (int 1..1), `sigea`, `nroReferencia` | `AvisoDigitResult`: `codError`, `descError` | Crea; el legajo pasa a `DIGI`. Misma clave |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` | — |

Reglas del manual ("Funcionalidad" y pp.11-18):

- `nroLegajo`: 16 caracteres, declaración detallada `AABBBCCCCDDDDDDE` o número de referencia `AAAABBCCCCCCCCCD`.
- `codigo`: `000` carpeta completa, `001` documentación adicional, `002` rectificativa B total, `003` rectificativa B parcial, `004` post-libramiento, `100` manifiesto general de carga de importación (PDF), `101` manifiesto general (físico).
- `ticket`: obligatorio para `001`, prohibido en los demás. Cuatro dígitos de año (≥ 2009) + secuencia con ceros a la izquierda.
- `sigea`: prohibido para `000`, `001`, `100`, `101`; obligatorio en los demás; 31 o 36 posiciones.
- `indLugarFisico`: `R1`-`R4` (recepción) o `A1` (archivo).
- `hashing`: SHA-1 en 40 caracteres. En `AvisoRecepAcept` es obligatorio para `100` y prohibido para los demás; en `AvisoDigit` es obligatorio.
- `familias` en `AvisoDigit`: `000`/`002`/`003` informan las cinco (`01` a `05`), `001` una sola, `004`/`100` ninguna, `101` solo `01` y `02`.
- Fechas nulas: el manual las representa como la mínima `01/01/01 00:00` (los `dateTime` son 1..1 en el WSDL).

## Errores

`codError` (int) + `descError`, HTTP 200. Éxito: `0` "OK Procesado".

| Código | `AvisoRecepAcept` (p.13-14) | `AvisoDigit` (p.18-19) |
|---|---|---|
| 0 | OK Procesado | OK procesado |
| 1 | PSAD incorrecto | Declarante incorrecto |
| 2 | Error Atributo/Parametro: "xxxxx" Obligatorio | Igual |
| 3 | ... Formato Incorrecto | PSAD incorrecto |
| 4 | Error Parametro: "codigo" Valor incorrecto | ... Formato Incorrecto |
| 5 | Error Parametro: "ticket" Valor incorrecto | ... Valor incorrecto |
| 6 | ... Prohibido | Legajo incorrecto |
| 7 | ... rango entre "xxxx" y "xxxx" | CUIT I/E Incorrecto |
| 8-11 | — | CUIT ATA incorrecto / ticket obligatorio / "familia" no debe informarse / Prohibido |
| 101 | Nro de Legajo inválido | Igual |
| 102 | Estado del Legajo inválido (para `000` debe estar en `ENDO`) | Estado del Legajo inválido (`ENDO` o `PSAD`) |
| 103 | Transmisión no autorizada por esta VIA | Igual |
| 104 | Transmisión no autorizada. Legajo no digitalizado (para `001`, la carpeta `000` en `ENDO` o superior) | Igual (para `001`, la `000` en `DIGI`) |
| 105 | Documento inexistente o en estado inválido | Igual |
| 106 | Declarante inválido para Legajo | Igual |
| 107 | Declarante PSAD inválido | PSAD inválido |
| 108 | El Legajo no se corresponde con el importador/exportador informado | Igual |
| 109 | TICKET (Dupla Legajo-Ticket inexistente) | Control: el declarante/PSAD tiene que estar autorizado a usar WSE; texto probable "Transmisión no autorizada por esta VIA. Legajo:..." |
| 111 | Legajo Duplicado | Igual |
| 112 | Error no contemplado | Igual |
| 113 | Legajo y Ticket existen duplicados | Dupla Legajo(XXX)-Ticket(XXX) inexistente |

Los mismos números significan cosas distintas en cada método. La tabla de `AvisoDigit` está desalineada en el PDF; el emparejamiento de 103-113 sale del orden de los controles.

## Comportamiento a simular

- Estado del legajo compartido con `wConsDepFiel`: `ENDO` → (`AvisoRecepAcept`) → legajo con PSAD asociado → (`AvisoDigit`) → `DIGI`. `ListaEstado` de `wConsDepFiel` debería reflejar cada paso, y `PndListaEndo` dejar de listar el legajo cuando sale de `ENDO` (inferido; **NO VERIFICADO**).
- Repetir un aviso para el mismo legajo y código → 111 "Legajo Duplicado".
- `001` (documentación adicional) depende de que exista la `000`: en `AvisoRecepAcept` en `ENDO` o superior; en `AvisoDigit` ya en `DIGI`.
- El CUIT de conexión tiene que ser el `cuitPSAD` (o el declarante si no hay PSAD): errores 1 y 3.
- Plazo: cinco días hábiles para digitalizar desde la recepción (manual "Funcionalidad"); `wConsDepFiel` expone `FechaVtoPSAD` y `FechaVtoDIGI`.

## No verificado

- Respuestas autenticadas.
- Si producción rechaza `hashing` en `AvisoRecepAcept` (su WSDL no lo tiene).
- Estado exacto del legajo después de `AvisoRecepAcept` (el manual habla de "PSAD asociado" pero no da el código).
