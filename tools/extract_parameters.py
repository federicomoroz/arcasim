#!/usr/bin/env python3
"""Turns the parameter tables of docs/arca/wsfev1.md (section 7) into data.

Writes src/ArcaSim.Application/Wsfe/Data/parametros.json, which the
FEParamGet* operations answer with and the validations read. The rows, their
order and their texts (including the literal "NULL") are the ones ARCA's
homologación returned in 2021; the country table comes from ARCA's published
spreadsheet in docs/arca/tablas/.

    python tools/extract_parameters.py
"""

from __future__ import annotations

import json
import re
from pathlib import Path

import openpyxl

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "docs" / "arca" / "wsfev1.md"
COUNTRIES = ROOT / "docs" / "arca" / "tablas" / "TABLA-PAISES-V.0-28072022.xlsx"
TARGET = ROOT / "src" / "ArcaSim.Application" / "Wsfe" / "Data" / "parametros.json"

# Voucher classes per code 10007 (wsfev1.md §4.4). ALEY is "A con leyenda", the old M.
CLASSES = {
    "A": [1, 2, 3, 4, 5, 34, 39, 60, 63, 201, 202, 203],
    "B": [6, 7, 8, 9, 10, 35, 40, 64, 61, 206, 207, 208],
    "C": [11, 12, 13, 15, 211, 212, 213],
    "ALEY": [51, 52, 53, 54],
    "49": [49],
}
KINDS = {
    "Invoice": [1, 6, 11, 49, 51, 201, 206, 211],
    "DebitNote": [2, 7, 12, 52, 202, 207, 212],
    "CreditNote": [3, 8, 13, 53, 203, 208, 213],
    "Receipt": [4, 9, 15, 54],
}
FCE = [201, 202, 203, 206, 207, 208, 211, 212, 213]

# Percentages behind each VAT rate id, for code 10051 (wsfev1.md §7.4).
VAT_RATES = {3: 0, 4: 0.105, 5: 0.21, 6: 0.27, 8: 0.05, 9: 0.025}


def section(text: str, number: str) -> str:
    start = text.index(f"### {number} ")
    end = text.index("\n### ", start + 1)
    return text[start:end]


def first_table(block: str) -> list[list[str]]:
    rows, started = [], False
    for line in block.splitlines():
        if line.startswith("|"):
            started = True
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if set("".join(cells)) <= set("-: "):
                continue
            rows.append(cells)
        elif started:
            break
    return rows[1:]


def plain(cell: str) -> str:
    return re.sub(r"^\*\*(.*)\*\*$", r"\1", cell.strip("`"))


def dated(rows: list[list[str]], id_kind) -> list[dict]:
    return [{"id": id_kind(plain(r[0])), "desc": r[1], "from": r[2], "to": r[3]} for r in rows]


def main() -> None:
    text = SOURCE.read_text(encoding="utf-8")
    class_of = {code: name for name, codes in CLASSES.items() for code in codes}
    kind_of = {code: name for name, codes in KINDS.items() for code in codes}

    voucher_types = dated(first_table(section(text, "7.1")), int)
    for row in voucher_types:
        row["class"] = class_of[row["id"]]
        row["kind"] = kind_of.get(row["id"], "Other")
        row["fce"] = row["id"] in FCE

    vat = dated(first_table(section(text, "7.4")), str)
    for row in vat:
        row["rate"] = VAT_RATES[int(row["id"])]

    conditions = []
    for r in first_table(section(text, "7.8")):
        classes = [name for name, mark in zip(["A", "B", "C", "49"], r[2:6]) if mark == "X"]
        conditions.append({"id": int(r[0]), "desc": r[1], "classes": classes})

    sheet = openpyxl.load_workbook(COUNTRIES, read_only=True).worksheets[0]
    countries = [
        {"id": int(code), "desc": str(desc).strip()}
        for code, desc, *_ in list(sheet.iter_rows(values_only=True))[1:]
        if code is not None
    ]

    data = {
        "voucherTypes": voucher_types,
        "documentTypes": dated(first_table(section(text, "7.2")), int),
        "concepts": dated(first_table(section(text, "7.3")), int),
        "vatRates": vat,
        "currencies": dated(first_table(section(text, "7.5")), str),
        "taxes": dated(first_table(section(text, "7.6")), int),
        "optionals": dated(first_table(section(text, "7.7")), str),
        "receiverVatConditions": conditions,
        "countries": countries,
    }
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(", ".join(f"{k} {len(v)}" for k, v in data.items()), "->", TARGET.relative_to(ROOT))


if __name__ == "__main__":
    main()
