# Cobertura de las validaciones de WSFEv1

Generado por `tools/coverage.py` a partir del código: un código cuenta como implementado cuando ArcaSim
lo puede devolver. La lista sale de las tablas del manual v4.7 ([docs/arca/wsfev1-codigos.md](arca/wsfev1-codigos.md)).

Lo que falta se concentra en lo que depende de padrones reales de ARCA (receptores apócrifos, CBU,
remitos, categorías de monotributo), en Factura de Crédito Electrónica MiPyME y en los datos opcionales
por resolución general. Ver [docs/arca/wsfev1.md §8.3](arca/wsfev1.md).

| Método | Implementados | Total |
|---|---:|---:|
| FECAESolicitar | 69 | 240 |
| FECAEARegInformativo | 79 | 212 |
| FECAEASolicitar | 8 | 17 |
| FECAEAConsultar | 2 | 2 |
| FECAEASinMovimientoInformar | 8 | 9 |
| FECAEASinMovimientoConsultar | 3 | 4 |
| FECompUltimoAutorizado | 3 | 3 |
| FECompConsultar | 4 | 4 |
| FEParamGetCotizacion | 3 | 3 |
| FEParamGetCondicionIvaReceptor | 1 | 1 |
| **Total** | **180** | **495** |

## FECAESolicitar

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 10000 | Rechazo | &lt;Cuit&gt; | sí |
| 10001 | Rechazo | &lt;CantReg&gt; | sí |
| 10002 | Rechazo | &lt;CantReg&gt; | sí |
| 10003 | Rechazo | Cantidad de registros incluidos | sí |
| 10004 | Rechazo | &lt;PtoVta&gt; | — |
| 10005 | Rechazo | &lt;PtoVta&gt; | — |
| 10006 | Rechazo | &lt;CbteTipo&gt; | sí |
| 10007 | Rechazo | &lt;CbteTipo&gt; | sí |
| 10008 | Rechazo | &lt;CbteDesde&gt; | sí |
| 10010 | Rechazo | &lt;CbteHasta&gt; | sí |
| 10011 | Rechazo | &lt;CbteHasta&gt; | sí |
| 10012 | Rechazo | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | sí |
| 10013 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; | sí |
| 10014 | Rechazo | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | sí |
| 10015 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | sí |
| 10016 | Rechazo | &lt;CbteDesde&gt; / &lt;CbteFch&gt; | sí |
| 10018 | Rechazo | &lt;AlicIVA&gt; | sí |
| 10019 | Rechazo | &lt;AlicIVA&gt;&lt;id&gt; | sí |
| 10020 | Rechazo | &lt;AlicIVA&gt;&lt;BaseImp&gt; | sí |
| 10021 | Rechazo | &lt;AlicIVA&gt;&lt;Importe&gt; | sí |
| 10022 | Rechazo | &lt;AlicIVA&gt;&lt;id&gt; | sí |
| 10023 | Rechazo | &lt;ImpIVA&gt; / &lt;AlicIVA&gt;&lt;importe&gt; | sí |
| 10024 | Rechazo | &lt;Tributo&gt; | sí |
| 10025 | Rechazo | &lt;Tributo&gt;&lt;id&gt; | sí |
| 10026 | Rechazo | &lt;Tributo&gt;&lt;BaseImp&gt; | sí |
| 10027 | Rechazo | &lt;Tributo&gt;&lt;Alic&gt; | sí |
| 10028 | Rechazo | &lt;Tributo&gt;&lt;importe&gt; | sí |
| 10029 | Rechazo | &lt;ImpTrib&gt; / &lt;Tributo&gt;&lt;importe&gt; | sí |
| 10030 | Rechazo | &lt;concepto&gt; | sí |
| 10031 | Rechazo | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | sí |
| 10032 | Rechazo | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; | sí |
| 10033 | Rechazo | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | sí |
| 10035 | Rechazo | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | sí |
| 10036 | Rechazo | &lt;FchVtoPago&gt; | sí |
| 10037 | Rechazo | &lt;MonId&gt; | sí |
| 10038 | Rechazo | &lt;MonCotiz&gt; | sí |
| 10039 | Rechazo | &lt;MonId&gt; / &lt;MonCotiz&gt; | sí |
| 10040 | Rechazo | &lt;CbtesAsoc&gt; / &lt;CbteTipo&gt; | sí |
| 10042 | Rechazo | &lt;Tirbuto&gt;&lt;Id&gt; / &lt;Tirbuto&gt;&lt;Desc&gt; | sí |
| 10043 | Rechazo | &lt;ImpTotConc&gt; | sí |
| 10044 | Rechazo | &lt;ImpOpEx&gt; | sí |
| 10045 | Rechazo | &lt;ImpNeto&gt; | sí |
| 10046 | Rechazo | &lt;ImpTrib&gt; | sí |
| 10047 | Rechazo | &lt;ImpIVA&gt; | sí |
| 10048 | Rechazo | &lt;ImpTotConc&gt; / &lt;ImpOpEx&gt; / &lt;ImpNeto&gt; / &lt;ImpTrib&gt; / &lt;ImpIVA&gt; / &lt;ImpTotal&gt; | sí |
| 10049 | Rechazo | &lt;FchServDesde&gt; / &lt;FchServHasta&gt; / &lt;FchVtoPago&gt; | sí |
| 10051 | Rechazo | &lt;AlicIVA&gt; | sí |
| 10052 | Rechazo | &lt;Opcionales&gt; | — |
| 10053 | Rechazo | &lt;Opcional&gt; | — |
| 10054 | Rechazo | &lt;Opcional&gt; | — |
| 10055 | Rechazo | &lt;Opcional&gt; | — |
| 10056 | Rechazo | Importes en general | sí |
| 10057 | Rechazo | &lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10058 | Rechazo | &lt;CbteAsoc&gt;&lt;PtoVta&gt; | sí |
| 10059 | Rechazo | &lt;CbteAsoc&gt;&lt;Nro&gt; | sí |
| 10060 | Rechazo | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |
| 10061 | Rechazo | &lt;ImpNeto&gt; / &lt;AlicIVA&gt;&lt;BaseImp&gt; | sí |
| 10062 | Rechazo | &lt;CbtesAsoc&gt;&lt;CbteAsoc&gt; | — |
| 10064 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10065 | Rechazo | &lt;ImpTotal&gt; | sí |
| 10066 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10067 | Rechazo | &lt;ImpTrib&gt; &lt;DocTipo&gt;&lt;DocNro&gt; | sí |
| 10068 | Rechazo | &lt;Opcionales&gt;&lt;CbteTipo&gt; | — |
| 10069 | Rechazo | &lt;DocNro&gt; | sí |
| 10070 | Rechazo | &lt;ImpNeto&gt;/ &lt;Iva&gt; | sí |
| 10071 | Rechazo | &lt;Iva&gt; | sí |
| 10075 | Rechazo | &lt;CbteTipo&gt;/&lt;AlicIVA&gt; | — |
| 10076 | Rechazo | &lt;Opcionales&gt;&lt;CbteTipo&gt;/&lt;DocTipo&gt; | — |
| 10077 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10078 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | — |
| 10079 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10080 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10081 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | — |
| 10082 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;CbteTipo&gt; | — |
| 10083 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10084 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | — |
| 10085 | Rechazo | &lt;concepto&gt; | sí |
| 10086 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | — |
| 10088 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10089 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10090 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10091 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10092 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10093 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10094 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10095 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10096 | Rechazo | &lt;PtoVta&gt; / &lt;CbteTipo&gt; | — |
| 10097 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10098 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10099 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10110 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10111 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10112 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt; | — |
| 10113 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10114 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10115 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10116 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10117 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10118 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10119 | Rechazo | &lt;MonId&gt;/&lt;MonCotiz&gt; | sí |
| 10120 | Rechazo | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |
| 10121 | Rechazo | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |
| 10122 | Rechazo | &lt;DocTipo&gt; / &lt;DocNro&gt; &lt;CbteAsoc&gt;&lt;Cuit&gt; | — |
| 10123 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10124 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10125 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10126 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10127 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10128 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10129 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10130 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10131 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; &lt;Auth&gt;&lt;Cuit&gt; | — |
| 10132 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10133 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt; | — |
| 10134 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Compradores&gt; | — |
| 10135 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocTipo&gt; | — |
| 10136 | Rechazo | &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;/ &lt;Compradores&gt; | — |
| 10137 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocTipo&gt; | — |
| 10138 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | — |
| 10139 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | — |
| 10140 | Rechazo | &lt;Auth&gt;/&lt;Cuit&gt; &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | — |
| 10141 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt; | — |
| 10142 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | — |
| 10143 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | — |
| 10144 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | — |
| 10145 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt; | — |
| 10146 | Rechazo | &lt;FECAEDetRequest&gt;&lt;DocTipo&gt; &lt;FECAEDetRequest&gt;&lt;DocNro&gt; &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocTipo&gt; &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | — |
| 10147 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;Porcentaje&gt; | — |
| 10148 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | — |
| 10149 | Rechazo | &lt;Compradores&gt;/&lt;Comprador&gt;/&lt;DocNro&gt; | — |
| 10150 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Concepto&gt;/ &lt;Compradores&gt; | — |
| 10151 | Rechazo | &lt;CbteAsoc&gt;&lt;Cuit&gt; | — |
| 10152 | Rechazo | &lt;CbteFch&gt;/&lt;Concepto&gt; | sí |
| 10153 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt; | — |
| 10154 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt;&lt;PtoVta&gt;&lt;Nro&gt;&lt;Cuit&gt; | — |
| 10155 | Rechazo | &lt;Auth&gt;&lt;Cuit&gt; &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Cuit&gt; | — |
| 10156 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10157 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10158 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | — |
| 10159 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FeDetReq&gt;/&lt;CbteFch&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | — |
| 10160 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | — |
| 10161 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt; | — |
| 10162 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt; | — |
| 10163 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | — |
| 10164 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; / &lt;FECAEDetRequest&gt;&lt;CbteFch&gt; | — |
| 10165 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10166 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10167 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10168 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10169 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10170 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10171 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10172 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10173 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10174 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10175 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | — |
| 10176 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 10177 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 10178 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 10180 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 10181 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/&lt;MonId&gt; &lt;CbteAsoc&gt; | — |
| 10183 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt;/ &lt;CbteAsoc&gt; | — |
| 10184 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/&lt;MonId&gt; &lt;CbteAsoc&gt;/ &lt;FeCabReq&gt;&lt;ImpTotal&gt; | — |
| 10186 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | — |
| 10187 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | — |
| 10189 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10190 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10192 | Rechazo | &lt;Auth&gt;&lt;Cuit&gt;/ &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocNro&gt;/ &lt;FECAEDetRequest&gt;&lt;ImpTotal&gt; / &lt;FECAEDetRequest&gt;&lt;MonCotiz&gt; / Tope | — |
| 10193 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | — |
| 10194 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Compradores&gt; | — |
| 10195 | Rechazo | &lt;DocTipo&gt;/ &lt;DocNro&gt;/ | — |
| 10196 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt; | — |
| 10197 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt; | sí |
| 10198 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt; | sí |
| 10199 | Rechazo | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | — |
| 10203 | Rechazo | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 10204 | Rechazo | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | — |
| 10205 | Rechazo | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 10206 | Rechazo | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 10207 | Rechazo | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 10208 | Rechazo | &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 10210 | Rechazo | &lt;FECAEDetRequest&gt;&lt;CbteFch&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 10211 | Rechazo | &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 10212 | Rechazo | &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 10213 | Rechazo | &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 10214 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10215 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10216 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 10218 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt; | — |
| 10219 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 10220 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 10221 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 10222 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 10223 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 10224 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; / &lt;Concepto&gt; | — |
| 10225 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10226 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10227 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10228 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10229 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10230 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 10231 | Rechazo | &lt;FECAEDetRequest&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc | — |
| 10232 | Rechazo | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |
| 10239 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;CanMisMonExt&gt; | sí |
| 10240 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;CanMisMonExt&gt; | — |
| 10241 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;CanMisMonExt&gt; | sí |
| 10242 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 10243 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 10246 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 10247 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | — |
| 10248 | Rechazo | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | — |
| 10251 | Rechazo | &lt;Cuit&gt; / &lt;FeCabReq&gt;&lt;CbteTipo&gt; | — |
| 10270 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | — |
| 10271 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | — |
| 10272 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;CondicionIVAReceptorId&gt; | — |
| 10273 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt; | — |
| 10274 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10275 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt; | — |
| 10276 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10277 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10278 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10279 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10280 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10281 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 10282 | Rechazo | &lt;Cuit&gt; / &lt;PtoVta&gt; | — |
| 10284 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | — |
| 10017 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; | sí |
| 10041 | Observacion | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |
| 10063 | Observacion | DocTipo / DocNro | sí |
| 10188 | Observacion | &lt;Auth&gt;&lt;Cuit&gt;/ &lt;DocTipo&gt;/ &lt;DocNro&gt;/ | — |
| 10209 | Observacion | &lt;FECAEDetRequest&gt;&lt;Tributos&gt;&lt;Id&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEDetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 10217 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; | sí |
| 10234 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 10235 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEDetRequest&gt; / ImpTotal | — |
| 10236 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEDetRequest&gt; / ImpTotal | — |
| 10237 | Observacion | &lt;FECAEDetRequest&gt; / ImpTotal / &lt;CbteAsoc&gt; | — |
| 10238 | Observacion | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | — |
| 10245 | Observacion | &lt;FECAEDetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 10249 | Observacion | &lt;FECAEDetRequest&gt; / &lt;DocNro&gt; | — |
| 10283 | Observacion | &lt;DocTipo&gt; / &lt;DocNro&gt; / &lt;Tributo&gt; | sí |

## FECAEARegInformativo

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 10000 | Rechazo | &lt;Auth&gt;&lt;Cuit&gt; | sí |
| 10001 | Rechazo | &lt;CantReg&gt; | sí |
| 10002 | Rechazo | &lt;CantReg&gt; | sí |
| 10003 | Rechazo | Cantidad de registros incluidos | sí |
| 700 | Rechazo | CbteTipo | sí |
| 1300 | Rechazo | PtoVta | — |
| 701 | Rechazo | PtoVta | — |
| 702 | Rechazo | CbteFch | sí |
| 703 | Rechazo | CbteDesde / CbteHasta / PtoVta / CbteTipo | sí |
| 704 | Rechazo | CbteFch / PtoVta / CbteTipo | sí |
| 705 | Rechazo | CAEA | sí |
| 1414 | Rechazo | Fecha de envío de la solicitud | — |
| 709 | Rechazo | CAEA / PtoVta | — |
| 1401 | Rechazo | MonId | sí |
| 713 | Rechazo | Concepto | sí |
| 715 | Rechazo | ImpIVA / Iva / AlicIva | sí |
| 717 | Rechazo | &lt;ImpTotConc&gt; | sí |
| 718 | Rechazo | &lt;ImpOpEx&gt; | sí |
| 719 | Rechazo | &lt;ImpNeto&gt; | sí |
| 723 | Rechazo | &lt;ImpTrib&gt; | sí |
| 1407 | Rechazo | &lt;ImpIVA&gt; | sí |
| 726 | Rechazo | &lt;MonCotiz&gt; | sí |
| 780 | Rechazo | CAEA | sí |
| 781 | Rechazo | PtoVta / CbteFch | — |
| 782 | Rechazo | CAEA | sí |
| 783 | Rechazo | CbteFch | sí |
| 784 | Rechazo | CbteDesde / CbteHasta | sí |
| 1416 | Rechazo | &lt;CbteHasta&gt; / &lt;CbteDesde&gt; | sí |
| 1415 | Rechazo | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | sí |
| 1417 | Rechazo | DocTipo / DocNro / CbteDesde / CbteHasta | sí |
| 1418 | Rechazo | DocTipo / DocNro / CbteDesde / CbteHasta | sí |
| 1419 | Rechazo | DocTipo / DocNro / CbteDesde / CbteHasta | sí |
| 1422 | Rechazo | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | sí |
| 711 | Rechazo | &lt;CbteTipo&gt; / &lt;CbteDesde&gt; / &lt;CbteHasta&gt; | sí |
| 1403 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; | sí |
| 1409 | Rechazo | &lt;ImpTotal&gt; | sí |
| 1404 | Rechazo | &lt;DocTipo&gt; / &lt;DocNro&gt; | sí |
| 1405 | Rechazo | &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 1421 | Rechazo | &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 788 | Rechazo | DocTipo / DocNro | sí |
| 1423 | Rechazo | &lt;ImpTrib&gt; / &lt;Tributos&gt; / &lt;Tributo&gt; | sí |
| 1426 | Rechazo | &lt;Opcionales&gt;&lt;CbteTipo&gt; | — |
| 1432 | Rechazo | &lt;Compradores&gt; | sí |
| 1433 | Rechazo | &lt;CbteTipo&gt;/ &lt;CbteDesde&gt;/ &lt;CbteHasta&gt; | sí |
| 1434 | Rechazo | &lt;CbteTipo&gt;/ &lt;ImpTotConc&gt; | sí |
| 1435 | Rechazo | &lt;CbteTipo&gt;/ &lt;ImpOpEx&gt; | sí |
| 1436 | Rechazo | &lt;CbteTipo&gt;/ &lt;ImpNeto&gt; | — |
| 1437 | Rechazo | &lt;CbteTipo&gt;/ &lt;ImpTrib&gt; | — |
| 1438 | Rechazo | &lt;CbteTipo&gt;/ &lt;ImpIVA&gt; | sí |
| 1439 | Rechazo | &lt;CbteTipo&gt;/ &lt;ImpTotal&gt;/ &lt;ImpNeto&gt; / &lt;ImpTrib&gt; / | sí |
| 1440 | Rechazo | &lt;CbteFchHsGen&gt; | sí |
| 1441 | Rechazo | &lt;CbteFchHsGen&gt; | sí |
| 1443 | Rechazo | &lt;Iva&gt; | sí |
| 1444 | Rechazo | &lt;PtoVta&gt; /&lt;CbteTipo&gt; | — |
| 1445 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt; / &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 1446 | Rechazo | &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 1450 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;CbtesAsoc&gt; | — |
| 1451 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt;&lt;PtoVta&gt;&lt;Nro&gt;&lt;Cuit&gt; | — |
| 1452 | Rechazo | &lt;Auth&gt;&lt;Cuit&gt; &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Cuit&gt; | — |
| 1453 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 1454 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 1455 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | — |
| 1456 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FeDetReq&gt;/&lt;CbteFch&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | — |
| 1457 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | — |
| 1458 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt; | — |
| 1459 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt; | — |
| 1460 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | — |
| 1461 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; / &lt;FECAEDetRequest&gt;&lt;CbteFch&gt; | — |
| 1462 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1463 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1464 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1465 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1466 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1467 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1468 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1469 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1470 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1471 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1472 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FchVtoPago&gt; | — |
| 1474 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 1475 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 1476 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 1477 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;MonId&gt; &lt;CbteAsoc&gt; | — |
| 1478 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocTipo&gt;&lt;DocNro&gt;/ &lt;CbteAsoc&gt; | — |
| 1479 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;MonId&gt; &lt;CbteAsoc&gt;/ &lt;FeCabReq&gt;&lt;ImpTotal&gt; | — |
| 1480 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | — |
| 1481 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | — |
| 1482 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1483 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1486 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt;/ | — |
| 1487 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | sí |
| 1488 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;CbteAsoc&gt; / &lt;Tipo&gt; / &lt;PtoVta&gt; / &lt;Nro&gt; / &lt;Cuit&gt; / &lt;CbteFch&gt; | — |
| 1490 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt; | — |
| 1491 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt; | sí |
| 1492 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt; | sí |
| 1493 | Rechazo | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | — |
| 1494 | Rechazo | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 1495 | Rechazo | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt; | — |
| 1496 | Rechazo | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 1497 | Rechazo | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 1498 | Rechazo | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 1499 | Rechazo | &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 1502 | Rechazo | &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 1503 | Rechazo | &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 1504 | Rechazo | &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 1505 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/&lt;CbteTipo&gt; | — |
| 1506 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 1507 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 1508 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 1509 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 1510 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 1511 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 1512 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;/ &lt;Opcionales&gt;&lt;Valor&gt; | — |
| 1513 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1514 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1515 | Rechazo | &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1517 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt; | — |
| 1518 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1519 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1520 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1521 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1522 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1523 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1524 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1525 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1526 | Rechazo | &lt;FECAEADetRequest&gt;/ &lt;Opcionales&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 820 | Rechazo | &lt;FECAEADetRequest&gt; / &lt;CanMisMonExt&gt; | sí |
| 823 | Rechazo | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 826 | Rechazo | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 1528 | Rechazo | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | — |
| 708 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; | sí |
| 724 | Observacion | &lt;ImpTotConc&gt; / &lt;ImpOpEx&gt; / &lt;ImpNeto&gt; / &lt;ImpTrib&gt; / &lt;ImpIVA&gt; / &lt;ImpTotal&gt; | sí |
| 728 | Observacion | FchServHasta | — |
| 725 | Observacion | &lt;ImpIVA&gt; | sí |
| 1402 | Observacion | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | sí |
| 727 | Observacion | &lt;FchServDesde&gt; | — |
| 1420 | Observacion | &lt;CbteTipo&gt; / &lt;DocTipo&gt; / &lt;DocNro&gt; | sí |
| 1408 | Observacion | &lt;ImpNeto&gt; / &lt;AlicIva&gt; &lt;BaseImp&gt; | sí |
| 1411 | Observacion | FchVtoPago | sí |
| 729 | Observacion | FchVtoPago | — |
| 1412 | Observacion | &lt;FchServDesde&gt;/ &lt;FchServHasta&gt; | sí |
| 1406 | Observacion | &lt;ImpTrib&gt; | sí |
| 1424 | Observacion | CAEA / &lt;PtoVta&gt; | sí |
| 1425 | Observacion | &lt;ImpTrib&gt; &lt;DocTipo&gt;&lt;DocNro&gt; | sí |
| 1413 | Observacion | &lt;FchServDesde&gt;/ &lt;FchServHasta&gt;/ &lt;FchVtoPago&gt; | sí |
| 1427 | Observacion | &lt;ImpNeto&gt;/ &lt;Iva&gt;.&lt;AlicIva&gt; | sí |
| 1429 | Observacion | &lt;Auth&gt;&lt;Cuit&gt; / &lt;CbteTipo&gt; / &lt;CbteFch&gt; | — |
| 1431 | Observacion | &lt;Auth&gt;&lt;Cuit&gt; / &lt;CbteTipo&gt; / &lt;CbteFch&gt; | — |
| 1442 | Observacion | &lt;CbteFchHsGen&gt; | — |
| 1485 | Observacion | &lt;Auth&gt;&lt;Cuit&gt;/ &lt;FeCabReq&gt;&lt;CbteTipo&gt;/ &lt;FECAEDetRequest&gt;&lt;DocNro&gt;/ &lt;FECAEDetRequest&gt;&lt;ImpTotal&gt; / &lt;FECAEDetRequest&gt;&lt;MonCotiz&gt; / Tope | — |
| 1489 | Observacion | &lt;DocTipo&gt;/ &lt;DocNro&gt;/ | — |
| 1500 | Observacion | &lt;FECAEADetRequest&gt;&lt;Tributos&gt;&lt;Id&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchDesde&gt;/ &lt;FECAEADetRequest&gt;&lt;PeriodoAsoc&gt;&lt;FchHasta&gt; | — |
| 1501 | Observacion | &lt;FECAEADetRequest&gt;&lt;CbteFch&gt;/ &lt;FECAEADetRequest&gt;&lt;CbtesAsoc&gt;&lt;CbteFch&gt; | — |
| 1516 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; | sí |
| 814 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; | — |
| 815 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEADetRequest&gt;&lt;CbteFch&gt;/ | — |
| 816 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEADetRequest&gt; / ImpTotal | — |
| 817 | Observacion | &lt;CbteTipo&gt; / &lt;DocNro&gt; / &lt;FECAEADetRequest&gt; / ImpTotal | — |
| 818 | Observacion | &lt;FECAEADetRequest&gt; / ImpTotal / &lt;CbteAsoc&gt; | — |
| 819 | Observacion | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | — |
| 821 | Observacion | &lt;FECAEADetRequest&gt; / &lt;CanMisMonExt&gt; | — |
| 822 | Observacion | &lt;FECAEADetRequest&gt; / &lt;CanMisMonExt&gt; | sí |
| 824 | Observacion | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 825 | Observacion | &lt;FECAEADetRequest&gt; / &lt;CondicionIVAReceptorId&gt; | sí |
| 827 | Observacion | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | — |
| 828 | Observacion | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | — |
| 829 | Observacion | &lt;FECAEADetRequest&gt; / &lt;DocNro&gt; | — |
| 800 | Rechazo | CbtesAsoc | — |
| 802 | Rechazo | PtoVta | sí |
| 803 | Rechazo | Nro | sí |
| 804 | Rechazo | Tipo / PtoVta / Nro | — |
| 805 | Rechazo | Tipo | — |
| 807 | Rechazo | CbteTipo / CbtesAsoc | sí |
| 808 | Rechazo | &lt;CbteAsoc&gt;&lt;Cuit&gt; | — |
| 812 | Rechazo | CbteTipo / CbtesAsoc | — |
| 813 | Rechazo | &lt;Opcionales&gt;&lt;Id&gt; | — |
| 806 | Observacion | Tipo | — |
| 801 | Observacion | Tipo/ PtoVta / Nro | — |
| 809 | Observacion | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |
| 810 | Observacion | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |
| 811 | Observacion | &lt;DocTipo&gt; / &lt;DocNro&gt; &lt;CbteAsoc&gt;&lt;Cuit&gt; | — |
| 900 | Rechazo | Id | sí |
| 908 | Rechazo | Desc | sí |
| 907 | Rechazo | Importe | sí |
| 905 | Rechazo | BaseImp | sí |
| 906 | Rechazo | Alic | sí |
| 1000 | Rechazo | Id | sí |
| 1003 | Rechazo | Id | sí |
| 1008 | Rechazo | Importe | sí |
| 1009 | Rechazo | BaseImp | sí |
| 1006 | Observacion | Importe / AlicIva / BaseImp | sí |
| 1100 | Rechazo | Id | — |
| 1101 | Rechazo | Id | — |
| 1105 | Rechazo | Valor | — |
| 1103 | Rechazo | &lt;Opcionales&gt;&lt;Opcional&gt;&lt;Id&gt;&lt;Valor&gt; | — |
| 1104 | Rechazo | Valor | — |
| 1106 | Observacion | Valor | — |
| 16000 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt; | — |
| 16001 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 16002 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 16003 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 16004 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 16005 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; | — |
| 16006 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; / &lt;Concepto&gt; | — |
| 16007 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 16008 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 16009 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 16010 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 16011 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 16012 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc&gt;&lt;Tipo&gt; | — |
| 16013 | Rechazo | &lt;FeCAEARegInfReq&gt;&lt;Actividades&gt;&lt;Actividad&gt;&lt;Id&gt; /&lt;CbtesAsoc&gt;&lt;CbteAsoc | — |
| 16014 | Rechazo | &lt;CbteAsoc&gt;&lt;Tipo&gt; / &lt;CbteAsoc&gt;&lt;PtoVta&gt; / &lt;CbteAsoc&gt;&lt;Nro&gt; | — |

## FECAEASolicitar

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 15000 | Rechazo | &lt;Cuit&gt; | sí |
| 15001 | Rechazo | &lt;Cuit&gt; | — |
| 15003 | Rechazo | &lt;Cuit&gt; | sí |
| 15004 | Rechazo | &lt;Periodo&gt; | sí |
| 15005 | Rechazo | &lt;Orden&gt; | sí |
| 15006 | Rechazo | Fecha de envío | sí |
| 15008 | Rechazo | &lt;Periodo&gt; / &lt;Orden&gt; | sí |
| 15009 | Rechazo | &lt;Cuit&gt; | — |
| 15010 | Rechazo | &lt;Cuit&gt; | — |
| 15011 | Rechazo | &lt;Cuit&gt; | — |
| 15012 | Rechazo | &lt;Cuit&gt; | — |
| 15016 | Rechazo | &lt;Cuit&gt; | sí |
| 15014 | Observacion | &lt;Cuit&gt;/ &lt;Periodo&gt; / &lt;Orden&gt; | — |
| 15015 | Observacion | &lt;Cuit&gt; | — |
| 15017 | Observacion | &lt;Cuit&gt; | — |
| 15018 | Observacion | &lt;Cuit&gt; / CbteFchHsGen | sí |
| 1527 | Observacion | &lt;DocTipo&gt; / &lt;DocNro&gt; / &lt;Tributo&gt; | — |

## FECAEAConsultar

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 15004 | Rechazo | &lt;Periodo&gt; | sí |
| 15005 | Rechazo | &lt;Orden&gt; | sí |

## FECAEASinMovimientoInformar

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 1200 | Rechazo | &lt;CAEA&gt; | sí |
| 1201 | Rechazo | &lt;CUIT&gt; | sí |
| 1202 | Rechazo | &lt;CAEA&gt; / &lt;PtoVta&gt; | sí |
| 1203 | Rechazo | Fecha de envío de la solicitud | sí |
| 1204 | Rechazo | &lt;PtoVta&gt; | sí |
| 1205 | Rechazo | &lt;PtoVta&gt; | — |
| 1206 | Rechazo | &lt;PtoVta&gt; | sí |
| 1207 | Rechazo | &lt;CAEA&gt; | sí |
| 1209 | Rechazo | PtoVta | sí |

## FECAEASinMovimientoConsultar

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 10100 | Rechazo | CAEA | sí |
| 10101 | Rechazo | PtoVta | sí |
| 10102 | Rechazo | CAEA | sí |
| 10105 | Rechazo | CAEA / PtoVta | — |

## FECompUltimoAutorizado

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 11000 | Rechazo | &lt;PtoVta&gt; | sí |
| 11001 | Rechazo | &lt;CbteTipo&gt; | sí |
| 11002 | Rechazo | &lt;PtoVta&gt; | sí |

## FECompConsultar

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 10200 | Rechazo | PtoVta | sí |
| 10201 | Rechazo | CbteTipo | sí |
| 10104 | Rechazo | PtoVta | sí |
| 10202 | Rechazo | CbteNro | sí |

## FEParamGetCotizacion

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 12000 | Rechazo | MonId | sí |
| 12001 | Rechazo | MonId | sí |
| 12002 | Rechazo | FchCotiz | sí |

## FEParamGetCondicionIvaReceptor

| Código | Tipo | Campo | ArcaSim |
|---:|---|---|:---:|
| 10244 | Rechazo | Cmp_Clase | sí |
