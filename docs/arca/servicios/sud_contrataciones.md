# sud_contrataciones

Un organismo verifica si un proveedor del Estado tiene deuda (catálogo). **Es el mismo servicio que `sud_restricciones`**: mismo ID de WSAA, mismo endpoint y mismo WSDL. Todo el contrato está en [sud_restricciones.md](sud_restricciones.md).

## Contrato

| Campo | Valor |
|---|---|
| Dialecto | Java, Spring-WS, SOAP 1.1 |
| Endpoint homologación | `https://sud-ws.cloudhomo.afip.gob.ar/sud_restricciones` |
| Endpoint producción | `https://sud-ws.cloud.afip.gob.ar/sud_restricciones` |
| Namespace | `http://afip.gob.ar/ws/sud_restricciones` |
| Archivo guardado | `wsdl/sud_restricciones-produccion.wsdl` |
| WSAA service id | `sud_restricciones` (manual, sección 2.4) |
| Manual | `https://www.afip.gob.ar/ws/SudContrataciones/manual_sud_contrataciones.pdf`, versión 1.1 del 11/10/2018, titulado "Consulta servicio de deuda sud_restricciones" |

## Autenticación

Igual que `sud_restricciones`: `token`, `sign`, `cuit` y `cuitRepresentado` sueltos dentro del request.

## Operaciones

Las 3 del WSDL compartido: `dummy`, `tieneDeuda` y `tieneDeudaV2`. El manual de contrataciones solo documenta `dummy` y `tieneDeuda`, pero le asigna la respuesta con `deudas{deuda{impuesto, periodoFiscal}}` que en el WSDL actual pertenece a `tieneDeudaV2`. Seguir al WSDL.

## Errores

Ver `sud_restricciones.md`.

## Comportamiento a simular

Un único servicio simulado atiende a los dos nombres. No hay que registrar un service id `sud_contrataciones` en el WSAA simulado: ningún manual lo menciona.

## No verificado

- Si existe un service id `sud_contrataciones` en WSAA (ningún manual lo nombra).
- Si el alcance de contrataciones también está acotado a los impuestos 301 y 351 (lo dice solo el manual 1.3 de restricciones).
