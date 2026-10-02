#!/usr/bin/env python3
"""Turns the validation tables of docs/arca/wsfev1-codigos.md into data.

Writes src/ArcaSim.Application/Wsfe/Data/codigos.json: one entry per code and
method, with whether it rejects (EXCLUYENTE) or only observes, the manual's
text and, where one was seen in a real response, the literal message ARCA
sends (§5.1). The simulator answers with the literal when there is one and
with the manual's text otherwise.

    python tools/extract_codes.py
"""

from __future__ import annotations

import html
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "docs" / "arca" / "wsfev1-codigos.md"
TARGET = ROOT / "src" / "ArcaSim.Application" / "Wsfe" / "Data" / "codigos.json"

HEADER = re.compile(r"^#### (?P<method>\w+) · (?P<group>.+) · (?P<kind>NO EXCLUYENTES|EXCLUYENTES)")
ROW = re.compile(r"^\|\s*(?P<code>\d+)\s*\|(?P<field>[^|]*)\|(?P<text>.*)\|\s*$")

# Literal messages seen in real responses (wsfev1-codigos.md §5.1). The key is
# the code; the location tells whether ARCA sends it as an Err or an Obs.
LITERALS = {
    "10016:Err": "El numero o fecha del comprobante no se corresponde con el proximo a autorizar. Consultar metodo FECompUltimoAutorizado.",
    "10016:Obs:1": "Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N+5 siendo N la fecha de envio del pedido  de autorizacion para 1 - Productos",
    "10016:Obs:FCE-ND-NC": "Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N siendo N la fecha de envio del pedido de autorizacion para Facturas de Credito del tipo Nota de Debito o Nota de Credito",
    "10217": "El credito fiscal discriminado en el presente comprobante solo podra ser computado a efectos del Procedimiento permanente de transicion al Regimen General.",
    "602": "No existen datos en nuestros registros para los parametros ingresados.",
    "782": "El <CAEA> es obligatorio informarlo.",
    "15004": "El campo <Periodo> debe tener el formato AAAAMM, donde AAAA indica el año y MM el mes en números.",
    "1200": "El codigo de autorizacion debe ser del tipo CAEA",
}


def clean(text: str) -> str:
    text = re.sub(r"\*\*\[[^\]]*\]\*\*", "", text)
    text = text.replace("<br>", " ").replace("\\|", "|")
    text = html.unescape(text)
    return re.sub(r"\s+", " ", text).strip()


def main() -> None:
    entries: list[dict] = []
    method = group = kind = None
    for line in SOURCE.read_text(encoding="utf-8").splitlines():
        header = HEADER.match(line)
        if header:
            method, group = header["method"], clean(header["group"])
            kind = "Observacion" if header["kind"].startswith("NO") else "Rechazo"
            continue
        if line.startswith("## "):
            method = None
        row = ROW.match(line)
        if row and method:
            code = int(row["code"])
            entries.append({
                "method": method,
                "group": group,
                "code": code,
                "kind": kind,
                "field": clean(row["field"]),
                "manualText": clean(row["text"]),
            })

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(
        json.dumps({"literals": LITERALS, "codes": entries}, ensure_ascii=False, indent=1) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    methods = sorted({e["method"] for e in entries})
    print(f"{len(entries)} rows from {len(methods)} methods -> {TARGET.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
