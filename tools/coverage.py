#!/usr/bin/env python3
"""Writes docs/cobertura.md: which of the WSFEv1 manual's validation codes ArcaSim implements.

A code counts as implemented when the source asks the catalog for it: a call
to For/WithMessage with that number, or a row of RuleCodes. The list of codes
comes from the same codigos.json the simulator answers with, so the report
cannot drift from what the service does.

    python tools/coverage.py
"""

from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CATALOG = ROOT / "src" / "ArcaSim.Application" / "Wsfe" / "Data" / "codigos.json"
SOURCE = ROOT / "src" / "ArcaSim.Application" / "Wsfe"
TARGET = ROOT / "docs" / "cobertura.md"

CALL = re.compile(r"(?:(?:For|WithMessage)\([^,()]+, |Fail\(|error = )(\d+)")
RULE = re.compile(r"\[Rule\.\w+\] = \((\d+|null), (\d+|null)\)")


def referenced() -> tuple[set[int], dict[str, set[int]]]:
    anywhere: set[int] = set()
    by_method: dict[str, set[int]] = defaultdict(set)
    for path in SOURCE.glob("*.cs"):
        text = path.read_text(encoding="utf-8")
        anywhere.update(int(code) for code in CALL.findall(text))
        for cae, caea in RULE.findall(text):
            if cae != "null":
                by_method["FECAESolicitar"].add(int(cae))
            if caea != "null":
                by_method["FECAEARegInformativo"].add(int(caea))
    return anywhere, by_method


def main() -> None:
    codes = json.loads(CATALOG.read_text(encoding="utf-8"))["codes"]
    anywhere, by_method = referenced()

    tables: dict[str, list[dict]] = defaultdict(list)
    for entry in codes:
        tables[entry["method"]].append(entry)

    def implemented(entry: dict) -> bool:
        return entry["code"] in anywhere or entry["code"] in by_method[entry["method"]]

    lines = [
        "# Cobertura de las validaciones de WSFEv1",
        "",
        "Generado por `tools/coverage.py` a partir del código: un código cuenta como implementado cuando ArcaSim",
        "lo puede devolver. La lista sale de las tablas del manual v4.7 ([docs/arca/wsfev1-codigos.md](arca/wsfev1-codigos.md)).",
        "",
        "Lo que falta se concentra en lo que depende de padrones reales de ARCA (receptores apócrifos, CBU,",
        "remitos, categorías de monotributo), en Factura de Crédito Electrónica MiPyME y en los datos opcionales",
        "por resolución general. Ver [docs/arca/wsfev1.md §8.3](arca/wsfev1.md).",
        "",
        "| Método | Implementados | Total |",
        "|---|---:|---:|",
    ]
    total_done = total = 0
    for method, entries in tables.items():
        unique = {e["code"]: e for e in entries}.values()
        done = sum(1 for e in unique if implemented(e))
        total_done += done
        total += len(unique)
        lines.append(f"| {method} | {done} | {len(unique)} |")
    lines.append(f"| **Total** | **{total_done}** | **{total}** |")

    for method, entries in tables.items():
        lines += ["", f"## {method}", "", "| Código | Tipo | Campo | ArcaSim |", "|---:|---|---|:---:|"]
        seen = set()
        for e in entries:
            if e["code"] in seen:
                continue
            seen.add(e["code"])
            field = e["field"].replace("|", "\\|").replace("<", "&lt;").replace(">", "&gt;")
            lines.append(f"| {e['code']} | {e['kind']} | {field} | {'sí' if implemented(e) else '—'} |")

    TARGET.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{total_done} of {total} codes -> {TARGET.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
