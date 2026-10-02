#!/usr/bin/env bash
# Una factura B de punta a punta contra ArcaSim, sin ninguna librería: openssl
# para el certificado y la firma, curl para los dos web services.
# Contra ARCA es lo mismo con sus URL y el certificado de WSASS.
#
#   ARCASIM=http://localhost:7080 CUIT=20111111112 bash docs/ejemplos/curl.sh
set -euo pipefail
# Git Bash en Windows convertiría "/CN=..." en una ruta.
export MSYS_NO_PATHCONV=1

ARCASIM=${ARCASIM:-http://localhost:7080}
CUIT=${CUIT:-20111111112}
WSAA="$ARCASIM/ws/services/LoginCms"
WSFE="$ARCASIM/wsfev1/service.asmx"
cd "$(mktemp -d)"

# 1. Un certificado con el DN que pide ARCA. Con acceso abierto, ArcaSim acepta
#    uno autofirmado; contra ARCA va el que emite WSASS para esa clave.
openssl req -x509 -newkey rsa:2048 -nodes -days 365 -keyout key.pem -out cert.pem \
  -subj "/CN=mi-aplicacion/serialNumber=CUIT $CUIT" 2>/dev/null

# 2. El pedido de acceso (TRA), firmado como CMS con el certificado adentro.
now=$(date +%s)
gen=$(date -d "@$((now - 600))" +%Y-%m-%dT%H:%M:%S%:z)
exp=$(date -d "@$((now + 600))" +%Y-%m-%dT%H:%M:%S%:z)
cat > tra.xml <<XML
<?xml version="1.0" encoding="UTF-8"?>
<loginTicketRequest version="1.0">
  <header><uniqueId>$now</uniqueId><generationTime>$gen</generationTime><expirationTime>$exp</expirationTime></header>
  <service>wsfe</service>
</loginTicketRequest>
XML
cms=$(openssl cms -sign -in tra.xml -signer cert.pem -inkey key.pem -nodetach -outform DER | base64 | tr -d '\n')

# 3. loginCms: el ticket de acceso viene escapado dentro de loginCmsReturn.
ticket=$(curl -s "$WSAA" -H 'Content-Type: text/xml; charset=utf-8' -H 'SOAPAction: ""' --data-binary \
  "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:wsaa=\"http://wsaa.view.sua.dvadac.desein.afip.gov\"><soapenv:Body><wsaa:loginCms><wsaa:in0>$cms</wsaa:in0></wsaa:loginCms></soapenv:Body></soapenv:Envelope>")
token=$(printf '%s' "$ticket" | sed -n 's/.*&lt;token&gt;\(.*\)&lt;\/token&gt;.*/\1/p')
sign=$(printf '%s' "$ticket" | sed -n 's/.*&lt;sign&gt;\(.*\)&lt;\/sign&gt;.*/\1/p')
if [ -z "$token" ]; then
  echo "WSAA respondió:"; echo "$ticket"
  # Como ARCA, WSAA no da otro ticket mientras el anterior sigue vigente: una aplicación
  # guarda el ticket 12 horas. Para correr este ejemplo seguido, apagar la ventana:
  #   curl -X PUT $ARCASIM/arcasim/api/settings -H 'Content-Type: application/json' -d '{"replayWindowEnabled":false}'
  case "$ticket" in *coe.alreadyAuthenticated*) echo "Ya hay un ticket vigente para este certificado (ver el comentario en el script).";; esac
  exit 1
fi
auth="<ar:Auth><ar:Token>$token</ar:Token><ar:Sign>$sign</ar:Sign><ar:Cuit>$CUIT</ar:Cuit></ar:Auth>"

wsfe() { # operación, contenido
  curl -s "$WSFE" -H 'Content-Type: text/xml; charset=utf-8' -H "SOAPAction: \"http://ar.gov.afip.dif.FEV1/$1\"" --data-binary \
    "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ar=\"http://ar.gov.afip.dif.FEV1/\"><soapenv:Body><ar:$1>$2</ar:$1></soapenv:Body></soapenv:Envelope>"
}

# 4. El último número autorizado para el punto de venta 1, tipo 6 (Factura B).
last=$(wsfe FECompUltimoAutorizado "$auth<ar:PtoVta>1</ar:PtoVta><ar:CbteTipo>6</ar:CbteTipo>" | sed -n 's/.*<CbteNro>\([0-9]*\)<\/CbteNro>.*/\1/p')
next=$((last + 1))

# 5. El CAE del siguiente: $ 1.210 a consumidor final, con 21 % de IVA.
wsfe FECAESolicitar "$auth<ar:FeCAEReq>
  <ar:FeCabReq><ar:CantReg>1</ar:CantReg><ar:PtoVta>1</ar:PtoVta><ar:CbteTipo>6</ar:CbteTipo></ar:FeCabReq>
  <ar:FeDetReq><ar:FECAEDetRequest>
    <ar:Concepto>1</ar:Concepto><ar:DocTipo>99</ar:DocTipo><ar:DocNro>0</ar:DocNro>
    <ar:CbteDesde>$next</ar:CbteDesde><ar:CbteHasta>$next</ar:CbteHasta>
    <ar:ImpTotal>1210</ar:ImpTotal><ar:ImpTotConc>0</ar:ImpTotConc><ar:ImpNeto>1000</ar:ImpNeto>
    <ar:ImpOpEx>0</ar:ImpOpEx><ar:ImpTrib>0</ar:ImpTrib><ar:ImpIVA>210</ar:ImpIVA>
    <ar:MonId>PES</ar:MonId><ar:MonCotiz>1</ar:MonCotiz><ar:CondicionIVAReceptorId>5</ar:CondicionIVAReceptorId>
    <ar:Iva><ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>1000</ar:BaseImp><ar:Importe>210</ar:Importe></ar:AlicIva></ar:Iva>
  </ar:FECAEDetRequest></ar:FeDetReq></ar:FeCAEReq>" | sed -n 's/.*\(<FECAEDetResponse>.*<\/FECAEDetResponse>\).*/\1/p'
