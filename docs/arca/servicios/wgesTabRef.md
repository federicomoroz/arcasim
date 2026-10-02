# wgesTabRef

"Consulta de Tablas de Referencia" del SIM (sistema MARIA). Devuelve el contenido de las tablas de códigos que necesitan los demás web services aduaneros: aduanas, lugares operativos, países, monedas, embalajes, estados, etc. Todos los manuales DIA remiten a él para validar códigos (por ejemplo `wConsDepFiel` pide `DFCOD_DESC`, `DFEST_DESC` y `DFPEDDOC_DESC` con `ListaDescripcion`). Lo usa cualquier agente aduanero que consuma los servicios DIA. El catálogo lo anota como "A ser reemplazado por el wGesTabRef".

Fuentes y convenciones:

- Manual: `https://www.afip.gob.ar/ws/documentos/Manual_del_Desarrollador_wgestabref.pdf`, fechado "Junio del 2025", historial hasta la versión 2.5. Descargado el 2026-10-02 (HTTP 200). El PDF no tiene números de página extraíbles: se citan secciones.
- WSDL de homologación: `docs/arca/wsdl/wgesTabRef-homologacion.wsdl`, de `https://testdia.afip.gov.ar/Dia/ws/wgesTabRef/wgesTabRef.asmx?WSDL` (2026-10-02). Con el host `testdia.afip.gob.ar` (el del manual) el WSDL sale idéntico salvo el `soap:address`, que repite el host pedido: el servidor arma la dirección con el host de la request. Esquema inline, sin XSD aparte.
- WSDL de producción: `docs/arca/wsdl/wgesTabRef/wgesTabRef-produccion.wsdl` (`https://servicios3.arca.gob.ar/Dia/ws/wgesTabRef/wgesTabRef.asmx?WSDL`). **Difiere**: tiene 11 operaciones; le faltan `DocumentosVigentes` y `ListaDatoComplementario`.
- Llamadas sin credenciales: 2026-10-02, 15:08-15:15 (-03:00).
- Catálogo: tipos de agente habilitados `DEPO`, `DESP`, `IEOC`, `IMEX`, `OTEN`, `PSAD`, `SCIN`, `SICO`, `USUD`. README: `https://www.afip.gob.ar/ws/documentos/README.txt` (endpoints `testdia.afip.gov.ar/dia/ws/wGesTabRef/wGesTabRef.asmx` y `Servicios1.afip.gov.ar/...`). El README da como producción un host viejo, `servicios1.afip.gov.ar`, que el 2026-10-02 respondió **HTTP 503** "Service Unavailable" para todas las rutas DIA probadas: la producción vigente es `servicios3.arca.gob.ar`.

**Aviso importante: el manual y el servicio desplegado no coinciden.** El manual 2.5 describe otro contrato: namespace `Ar.Gob.Afip.Dga.wgestabref`, autenticación `argWSAutenticacionEmpresa` (`Token`, `Sign`, `CuitEmpresaConectada`, `TipoAgente`, `Rol`), parámetro `argNombreTabla` y respuestas con `ListaErrores/DetalleError` + `Server` + `TimeStamp` (el estilo "diav2" de `wgestiendaslibres`). En la URL que el mismo manual da (`testdia.afip.gob.ar/Dia/ws/wgesTabRef/wgesTabRef.asmx`) corre el contrato **viejo**, el que se documenta abajo. Se buscó el contrato nuevo en `https://wsaduhomoext.afip.gob.ar/diav2/wgestabref/wgestabref.asmx` y en `https://webservicesadu.afip.gob.ar/diav2/wgestabref/wgestabref.asmx`: los dos dieron **404** el 2026-10-02. ArcaSim debería replicar lo desplegado y dejar el contrato del manual como una variante futura.

## Contrato

| Tema | Valor | Fuente |
|---|---|---|
| Dialecto | .NET ASMX "DIA", variante con resultado plano: `CodError` + `InfoAdicional` en la raíz del `Result`, sin `Recibo` | WSDL; prueba en vivo |
| Endpoint homologación | `https://testdia.afip.gov.ar/Dia/ws/wgesTabRef/wgesTabRef.asmx` (también responde `https://testdia.afip.gob.ar/...`, que es el del manual) | `soap:address` del WSDL; prueba en vivo |
| Endpoint producción | `https://servicios3.arca.gob.ar/Dia/ws/wgesTabRef/wgesTabRef.asmx` | WSDL de producción; manual |
| Namespace | `ar.gov.afip.dia.serviciosweb.wgesTabRef` | WSDL |
| `elementFormDefault` | `qualified` | WSDL |
| Bindings | `wgesTabRefSoap` (1.1) y `wgesTabRefSoap12` (1.2) | WSDL |
| SOAPAction | `ar.gov.afip.dia.serviciosweb.wgesTabRef/<Operación>` | WSDL |
| SOAP | 1.1 y 1.2; `Dummy` respondió por las dos | Prueba en vivo |
| WSAA service id | El README de esta entrada (`/ws/documentos/README.txt`, titulado "wGesTabRef") dice `WDiaUtiDES` para los dos ambientes. Parece un error de copia, pero es lo único publicado: **NO VERIFICADO** | README del catálogo |
| Operaciones | 13 en homologación, 11 en producción | WSDL |

## Autenticación

El parámetro se llama `Autentica` (no `argAutentica`) y el tipo `Autenticacion` es **plano**, con este orden:

```xml
<Autentica>
  <Cuit>...</Cuit>              <!-- 0..1 -->
  <TipoAgente>...</TipoAgente>  <!-- 0..1 -->
  <Rol>...</Rol>                <!-- 0..1 -->
  <Token>...</Token>            <!-- 1..1, nillable -->
  <Sign>...</Sign>              <!-- 1..1, nillable -->
</Autentica>
```

El manual (sección "Autenticación / autorización") describe la estructura nueva y da como valores de `TipoAgente`: `IMEX`, `IEOC`, `DESP`, `USUD`, `TRSP`; `Rol` = `EXTE`.

Errores de autenticación del manual ("Códigos y mensajes de error"): 6005, 6006, 6007 (Aduana invalida para el CUIT y el tipo de agente informados), 6008 (Lugar operativo invalido para el CUIT y la aduana informados), 6009, 7000, 7001 (No se encontro la empresa conectada en la lista de empresas del token), 7006 Debe ingresar la firma, 7007 Debe ingresar el token, 7008 con cuatro textos ("token invalido. El Token no se encuentra en formato base 64", "... no se encuentra bien conformado", "... no tiene un tipo de usuario definido", "... Fallo la autenticacion del token"), 7013, 7015-7017 (Cuit/Tipo Agente/Rol no informado) y 42034 (Falta el dato obligatorio ...). Ojo: esa tabla es del contrato nuevo.

Observado con `ListaDescripcion` e `IdReferencia`=`SIGLAS` (homologación, 2026-10-02). El error va en `CodError` e `InfoAdicional`, HTTP 200:

| Caso | HTTP | Respuesta |
|---|---|---|
| `Token`=`abc` | 200 | `<CodError>7004</CodError><InfoAdicional>ID MWE : 73329196</InfoAdicional><IdReferencia>SIGLAS</IdReferencia>` |
| `Token` y `Sign` vacíos | 200 | `<CodError>7007</CodError><InfoAdicional>Debe ingresar el Token y Firma.</InfoAdicional><IdReferencia>SIGLAS</IdReferencia>` |
| Base64 de `<sso><id/></sso>` | 200 | `7004` / `ID MWE : 73329197` |
| `sso` 2.0 bien formado, `Sign` falso | 200 | `7006` / `Debe ingresar la Firma.` |
| Sin `Autentica` | 500 | `soap:Fault` `soap:Server` "El servidor no puede procesar la solicitud. ---&gt; Referencia a objeto no establecida como instancia de un objeto." |

Diferencias con el resto de la DIA: un token que no es Base64 da **7004**, no 7008; el texto `ID MWE : n` lleva un espacio antes de los dos puntos; el `Result` repite el `IdReferencia` pedido.

```xml
<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><soap:Body><ListaDescripcionResponse xmlns="ar.gov.afip.dia.serviciosweb.wgesTabRef"><ListaDescripcionResult><CodError>7007</CodError><InfoAdicional>Debe ingresar el Token y Firma.</InfoAdicional><IdReferencia>SIGLAS</IdReferencia></ListaDescripcionResult></ListaDescripcionResponse></soap:Body></soap:Envelope>
```

## Operaciones

Todas son de consulta. Todos los `Result` extienden `Contenedor` (`CodError` int 1..1, `InfoAdicional`) y, salvo `DocumentosVigentes` y las dos `ListaTablasReferencia*`, repiten `IdReferencia`.

| Operación | Propósito | Entrada (tras `Autentica`) | Salida (además de `CodError`/`InfoAdicional`) |
|---|---|---|---|
| `ListaTablasReferencia` | Catálogo de tablas: nombre, descripción y método a usar | — | `TablasReferencia/TablaReferencia[]` |
| `ListaTablasReferenciaServicio` | El mismo catálogo filtrado por servicio | `IdServicio` | `TablasReferencia/TablaReferencia[]` |
| `ListaDescripcion` | Tabla código/descripción | `IdReferencia` | `Descripciones/Descripcion[]` |
| `ListaVigencias` | Tabla código/descripción/vigencia | `IdReferencia` | `Vigencias/Vigencia[]` |
| `ListaPaisesAduanas` | Código/descripción/vigencia/país o aduana | `IdReferencia` | `PaisesAduanas/PaisAduana[]` |
| `ListaEmpresas` | CUIT/razón social | `IdReferencia` | `Empresas/Empresa[]` |
| `ListaArancel` | Código/descripción/opción/vigencia | `IdReferencia` | `Opciones/Opcion[]` |
| `ListaLugaresOperativos` | Código/descripción/vigencia/aduana/lugar operativo | `IdReferencia` | `LugaresOperativos/LugarOperativo[]` |
| `ListaDescripcionDecodificacion` | Código/descripción/codificación | `IdReferencia` | `DescripcionesCodificaciones/DescripcionCodificacion[]` |
| `ListaDatoComplementario` | Código/descripción/vigencia/indicador/formato. **Solo homologación** | `IdReferencia` | `DatosComplementarios/DatoComplementario[]` |
| `DocumentosVigentes` | Código/descripción/vigencia con indicador. **Solo homologación** | — | `VigenciasIndicador/VigenciaIndicador[]` |
| `ConsultarFechaUltAct` | Fecha de última actualización de una tabla | `IdReferencia` | `Fecha` (dateTime 1..1) + `IdReferencia` |
| `Dummy` | Salud | — | `appserver`, `dbserver`, `authserver` |

El manual nombra los métodos de otra forma (`ListaDescripcionesDecodificacion`, `ListaTablaReferencia`) y les pasa `argNombreTabla`; el WSDL desplegado usa los nombres de la tabla de arriba e `IdReferencia`.

Ejemplo de datos del manual (`ListaVigencias`): `Codigo` `352` `KAZAJSTAN` con `FechaDesde` `19931004` y `FechaHasta` `30001231`. Las fechas de vigencia van como texto `AAAAMMDD`, y `30001231` significa "sin fin".

## Errores

| Campo | Significado |
|---|---|
| `CodError` | 0 = OK (ejemplos del manual). Otros: los de autenticación de arriba |
| `InfoAdicional` | Texto del error o `ID MWE : n` |

El manual no trae una tabla de errores de negocio propia del servicio (solo la de autenticación). Transporte: igual que el resto de la DIA en `testdia` (`SOAPAction` desconocida → 500 `soap:Client`; XML roto → 400 vacío).

## Comportamiento a simular

- Sin estado de negocio: es un servidor de catálogos. ArcaSim necesita fixtures por `IdReferencia` (por ejemplo `BUR_VIG`, `BUR_DESC`, `DFCOD_DESC`, `DFEST_DESC`, `DFPEDDOC_DESC`, `TIPPEMA_DESC`, `ETAPEMA_DESC`, `ESTCEL_DESC`, `ESTMON_DESC`, `TLTIPO_DESC`, `ORIGMERC_DESC`, que citan los otros manuales DIA) y la fecha de última actualización de cada una.
- Coherencia con los demás servicios: los códigos que el simulador acepte en `WDiaUtiDES`, `wgesprecintosdepfis`, `wConsDepFiel`, etc. deberían salir de estas mismas tablas.
- Perfiles distintos por ambiente: homologación con 13 operaciones y producción con 11.
- `IdReferencia` desconocido: **NO VERIFICADO** qué devuelve.

## No verificado

- WSAA service id real (el README dice `WDiaUtiDES`).
- Respuesta autenticada real, código de éxito real y error para `IdReferencia` inexistente.
- Si el contrato nuevo del manual (`Ar.Gob.Afip.Dga.wgestabref`) está desplegado en algún host.
