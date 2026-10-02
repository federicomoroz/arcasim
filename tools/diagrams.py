#!/usr/bin/env python3
"""Generates the animated diagrams in docs/diagrams/ (Spanish and English).

Each diagram is one self-contained HTML page: an SVG that plays its steps in a
loop with CSS animations only, so it opens offline, respects
prefers-reduced-motion, and can be recorded frame by frame into a GIF
(tools/record.py) by pausing every animation at a given time.

    python tools/diagrams.py
"""

from __future__ import annotations

from dataclasses import dataclass, field
from html import escape
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "docs" / "diagrams"

W, H = 1200, 750
STEP = 2.8  # seconds per step


@dataclass
class Node:
    id: str
    x: int
    y: int
    w: int
    h: int
    title: dict
    sub: dict = field(default_factory=lambda: {"es": "", "en": ""})
    group: bool = False  # a container drawn behind its children


@dataclass
class Packet:
    path: list  # [(x, y), ...] walked at constant speed during the step
    label: dict
    color: str = "accent"


@dataclass
class Badge:
    x: int
    y: int
    text: dict
    color: str = "accent"
    until: int | None = None  # last step it stays on (default: only its own)
    anchor: str = "middle"


@dataclass
class Cut:
    x: int
    y: int
    until: int | None = None


@dataclass
class Step:
    title: dict
    body: dict
    active: list = field(default_factory=list)
    packets: list = field(default_factory=list)
    badges: list = field(default_factory=list)
    cuts: list = field(default_factory=list)


@dataclass
class Diagram:
    slug: str
    kicker: dict
    nodes: list
    wires: list  # list of point lists
    steps: list
    foot: dict


COLORS = {
    "accent": "#ffb648",
    "copper": "#c07a45",
    "ok": "#6fd08c",
    "stop": "#ec6a5e",
    "data": "#4dd0e1",
}


def pct(value: float) -> str:
    return f"{max(0.0, min(100.0, value)):.3f}%"


def window(step: int, total: int, until: int | None = None) -> tuple[float, float]:
    start = step / total * 100
    end = ((until if until is not None else step) + 1) / total * 100
    return start, end


def show_keyframes(name: str, start: float, end: float) -> str:
    fade = 0.6
    frames = [f"0% {{ opacity: 0; }}"]
    if start > 0:
        frames.append(f"{pct(start - 0.01)} {{ opacity: 0; }}")
    frames.append(f"{pct(start + fade)} {{ opacity: 1; }}")
    if end < 100:
        frames.append(f"{pct(end - fade)} {{ opacity: 1; }}")
        frames.append(f"{pct(end)} {{ opacity: 0; }}")
        frames.append("100% { opacity: 0; }")
    else:
        frames.append(f"{pct(99.2)} {{ opacity: 1; }}")
        frames.append("100% { opacity: 0; }")
    return f"@keyframes {name} {{ {' '.join(frames)} }}"


def move_keyframes(name: str, start: float, end: float, path: list) -> str:
    """Walks the path during the first 55% of the step window and fades out on arrival: the box's glow and badges keep the state."""
    span = end - start
    travel = span * 0.55
    lengths = [((b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2) ** 0.5 for a, b in zip(path, path[1:])]
    total = sum(lengths) or 1
    frames = [f"0% {{ opacity: 0; transform: translate({path[0][0]}px, {path[0][1]}px); }}"]
    if start > 0:
        frames.append(f"{pct(start - 0.01)} {{ opacity: 0; transform: translate({path[0][0]}px, {path[0][1]}px); }}")
    frames.append(f"{pct(start + 0.4)} {{ opacity: 1; transform: translate({path[0][0]}px, {path[0][1]}px); }}")
    walked = 0.0
    for (x, y), length in zip(path[1:], lengths):
        walked += length
        frames.append(f"{pct(start + 0.4 + travel * walked / total)} {{ opacity: 1; transform: translate({x}px, {y}px); }}")
    last = path[-1]
    arrived = start + 0.4 + travel
    frames.append(f"{pct(arrived + span * 0.06)} {{ opacity: 1; transform: translate({last[0]}px, {last[1]}px); }}")
    frames.append(f"{pct(arrived + span * 0.14)} {{ opacity: 0; transform: translate({last[0]}px, {last[1]}px); }}")
    frames.append(f"100% {{ opacity: 0; transform: translate({last[0]}px, {last[1]}px); }}")
    return f"@keyframes {name} {{ {' '.join(frames)} }}"


def text_lines(x: float, y: float, text: str, cls: str, gap: int = 18, anchor: str = "middle") -> str:
    lines = text.split("\n")
    return "".join(
        f'<text x="{x}" y="{y + i * gap}" class="{cls}" text-anchor="{anchor}">{escape(line)}</text>' for i, line in enumerate(lines)
    )


def wrap(text: str, width: int) -> list[str]:
    words, lines, line = text.split(), [], ""
    for word in words:
        if line and len(line) + 1 + len(word) > width:
            lines.append(line)
            line = word
        else:
            line = f"{line} {word}".strip()
    if line:
        lines.append(line)
    return lines


def render(d: Diagram, lang: str) -> str:
    total = len(d.steps)
    cycle = total * STEP
    css: list[str] = []
    svg: list[str] = []

    for wire in d.wires:
        points = " ".join(f"{x},{y}" for x, y in wire)
        svg.append(f'<polyline points="{points}" class="wire"/>')
        svg.append(f'<polyline points="{points}" class="current"/>')

    for node in d.nodes:
        cls = "group" if node.group else "box"
        svg.append(f'<rect x="{node.x}" y="{node.y}" width="{node.w}" height="{node.h}" rx="8" class="{cls}"/>')
        svg.append(f'<rect id="glow-{node.id}" x="{node.x}" y="{node.y}" width="{node.w}" height="{node.h}" rx="8" class="glow"/>')
        if node.group:
            svg.append(text_lines(node.x + 16, node.y + 26, node.title[lang], "group-title", anchor="start"))
            if node.sub[lang]:
                svg.append(text_lines(node.x + node.w - 16, node.y + 26, node.sub[lang], "sub", anchor="end"))
        else:
            sub_lines = node.sub[lang].split("\n") if node.sub[lang] else []
            block = 22 + 17 * len(sub_lines)
            top = node.y + node.h / 2 - block / 2 + 15
            svg.append(text_lines(node.x + node.w / 2, top, node.title[lang], "title"))
            if sub_lines:
                svg.append(text_lines(node.x + node.w / 2, top + 22, node.sub[lang], "sub", gap=17))

    for node in d.nodes:
        steps = [i for i, s in enumerate(d.steps) if node.id in s.active]
        if not steps:
            continue
        frames = ["0% { opacity: 0; }"]
        for i in steps:
            start, end = window(i, total)
            frames.append(f"{pct(start - 0.01)} {{ opacity: 0; }} {pct(start + 0.6)} {{ opacity: 1; }}")
            frames.append(f"{pct(end - 0.6)} {{ opacity: 1; }} {pct(end)} {{ opacity: 0; }}")
        frames.append("100% { opacity: 0; }")
        css.append(f"@keyframes g-{node.id} {{ {' '.join(frames)} }}")
        css.append(f"#glow-{node.id} {{ animation: g-{node.id} {cycle}s linear infinite; }}")

    serial = 0
    for i, step in enumerate(d.steps):
        start, end = window(i, total)
        for packet in step.packets:
            serial += 1
            name = f"p{serial}"
            color = COLORS[packet.color]
            label = packet.label[lang]
            width = max(36, 8.2 * len(label) + 22)
            css.append(move_keyframes(name, start, end, packet.path))
            svg.append(
                f'<g class="packet" style="animation: {name} {cycle}s linear infinite;">'
                f'<rect x="{-width / 2}" y="-13" width="{width}" height="26" rx="13" fill="#171c22" stroke="{color}" stroke-width="1.6"/>'
                f'<circle cx="{-width / 2 + 13}" cy="0" r="4" fill="{color}"/>'
                f'<text x="{-width / 2 + 23}" y="4.5" class="packet-text" fill="{color}">{escape(label)}</text></g>'
            )
        for badge in step.badges:
            serial += 1
            name = f"b{serial}"
            b_start, b_end = window(i, total, badge.until)
            color = COLORS[badge.color]
            css.append(show_keyframes(name, b_start, b_end))
            lines = badge.text[lang].split("\n")
            width = max(8.0 * max(len(l) for l in lines) + 26, 40)
            height = 14 + 18 * len(lines)
            left = badge.x - width / 2 if badge.anchor == "middle" else badge.x
            svg.append(
                f'<g style="animation: {name} {cycle}s linear infinite; opacity: 0;">'
                f'<rect x="{left}" y="{badge.y - 15}" width="{width}" height="{height}" rx="6" fill="#171c22" stroke="{color}" stroke-width="1.2"/>'
                + "".join(
                    f'<text x="{left + width / 2}" y="{badge.y + 1 + k * 18}" class="badge-text" fill="{color}" text-anchor="middle">{escape(l)}</text>'
                    for k, l in enumerate(lines)
                )
                + "</g>"
            )
        for cut in step.cuts:
            serial += 1
            name = f"c{serial}"
            c_start, c_end = window(i, total, cut.until)
            css.append(show_keyframes(name, c_start, c_end))
            svg.append(
                f'<g style="animation: {name} {cycle}s linear infinite; opacity: 0;" class="cut">'
                f'<circle cx="{cut.x}" cy="{cut.y}" r="15" fill="#171c22" stroke="#ec6a5e" stroke-width="2"/>'
                f'<path d="M{cut.x - 6},{cut.y - 6} L{cut.x + 6},{cut.y + 6} M{cut.x + 6},{cut.y - 6} L{cut.x - 6},{cut.y + 6}" stroke="#ec6a5e" stroke-width="2.4"/></g>'
            )

    # Step header: number, title and body, one set per step.
    head: list[str] = []
    for i, step in enumerate(d.steps):
        serial += 1
        name = f"h{serial}"
        start, end = window(i, total)
        css.append(show_keyframes(name, start, end))
        body = "".join(
            f'<text x="160" y="{170 + k * 27}" class="body">{escape(line)}</text>' for k, line in enumerate(wrap(step.body[lang], 82))
        )
        head.append(
            f'<g style="animation: {name} {cycle}s linear infinite; opacity: 0;">'
            f'<rect x="56" y="98" width="76" height="76" rx="10" class="num-box"/>'
            f'<text x="94" y="149" class="num" text-anchor="middle">{i + 1}</text>'
            f'<text x="160" y="134" class="step-title">{escape(step.title[lang])}</text>{body}</g>'
        )
        dot_x = W / 2 - (total - 1) * 11 + i * 22
        css.append(f"@keyframes d{serial} {{ 0% {{ fill: #4a5461; }} {pct(start)} {{ fill: #4a5461; }} {pct(start + 0.1)} {{ fill: #ffb648; }} {pct(end)} {{ fill: #ffb648; }} {pct(end + 0.1)} {{ fill: #4a5461; }} 100% {{ fill: #4a5461; }} }}")
        svg.append(f'<circle cx="{dot_x}" cy="{H - 104}" r="4" style="animation: d{serial} {cycle}s linear infinite;"/>')

    foot = "".join(
        f'<text x="56" y="{H - 52 + k * 24}" class="foot">{escape(line)}</text>' for k, line in enumerate(wrap(d.foot[lang], 120))
    )
    title = d.kicker[lang]
    lang_attr = "es" if lang == "es" else "en"
    return f"""<!DOCTYPE html>
<html lang="{lang_attr}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>ArcaSim · {escape(title)}</title>
<style>
  :root {{ color-scheme: dark; }}
  html, body {{ margin: 0; background: #171c22; }}
  body {{ display: flex; align-items: center; justify-content: center; min-height: 100vh; }}
  svg {{ width: 100%; max-width: {W}px; height: auto; display: block; font-family: "Archivo", "Segoe UI", system-ui, sans-serif; }}
  .frame {{ fill: #171c22; }}
  .kicker {{ fill: #ebe6dc; font-size: 22px; font-weight: 800; letter-spacing: 0.14em; }}
  .meta {{ fill: #848d98; font: 15px Consolas, "JetBrains Mono", monospace; }}
  .rule {{ stroke: #c07a45; stroke-width: 2; opacity: 0.55; }}
  .num-box {{ fill: rgba(255, 182, 72, 0.10); stroke: #ffb648; stroke-width: 2; }}
  .num {{ fill: #ffb648; font-size: 38px; font-weight: 800; }}
  .step-title {{ fill: #ebe6dc; font-size: 34px; font-weight: 800; }}
  .body {{ fill: #a3abb5; font-size: 19px; }}
  .box {{ fill: #262d36; stroke: #4a5461; stroke-width: 1.2; }}
  .group {{ fill: #1f252d; stroke: #6e4a31; stroke-width: 1.4; stroke-dasharray: 6 5; }}
  .glow {{ fill: rgba(255, 182, 72, 0.07); stroke: #ffb648; stroke-width: 2; opacity: 0; }}
  .title {{ fill: #ebe6dc; font-size: 18px; font-weight: 700; }}
  .group-title {{ fill: #c07a45; font-size: 14px; font-weight: 800; letter-spacing: 0.12em; }}
  .sub {{ fill: #848d98; font: 13.5px Consolas, "JetBrains Mono", monospace; }}
  .wire {{ fill: none; stroke: #4a5461; stroke-width: 1.6; }}
  .current {{ fill: none; stroke: #c07a45; stroke-width: 1.6; stroke-dasharray: 3 9; animation: flow 0.8s linear infinite; opacity: 0.8; }}
  @keyframes flow {{ to {{ stroke-dashoffset: -12; }} }}
  .packet-text {{ font: 700 13px Consolas, "JetBrains Mono", monospace; }}
  .badge-text {{ font: 600 13.5px Consolas, "JetBrains Mono", monospace; }}
  .dot {{ fill: #4a5461; }}
  .foot {{ fill: #a3abb5; font-size: 17px; }}
  .foot-band {{ fill: #1f252d; }}
  .foot-edge {{ fill: #c07a45; }}
  {chr(10).join("  " + rule for rule in css)}
  @media (prefers-reduced-motion: reduce) {{
    .current {{ animation: none; }}
  }}
</style>
</head>
<body>
<svg viewBox="0 0 {W} {H}" role="img" aria-label="{escape(title)}">
  <rect width="{W}" height="{H}" class="frame"/>
  <rect x="56" y="38" width="10" height="10" fill="#ffb648"/>
  <text x="78" y="50" class="kicker">{escape(title.upper())}</text>
  <text x="{W - 56}" y="50" class="meta" text-anchor="end">ArcaSim · .NET 8 · SOAP · PostgreSQL</text>
  <line x1="56" y1="74" x2="{W - 56}" y2="74" class="rule"/>
  {"".join(head)}
  {"".join(svg)}
  <rect x="0" y="{H - 84}" width="{W}" height="84" class="foot-band"/>
  <rect x="0" y="{H - 84}" width="5" height="84" class="foot-edge"/>
  {foot}
</svg>
</body>
</html>
"""


def t(es: str, en: str) -> dict:
    return {"es": es, "en": en}


# 1. Swapping ArcaSim for ARCA ---------------------------------------------------------------

module = Diagram(
    slug="modulo",
    kicker=t("Cambiar ArcaSim por ARCA", "Swapping ArcaSim for ARCA"),
    nodes=[
        Node("app", 50, 330, 230, 130, t("Tu aplicación", "Your application"), t("Arca.Client\ncertificado + CUIT", "Arca.Client\ncertificate + CUIT")),
        Node("arcasim", 380, 262, 380, 296, t("ARCASIM", "ARCASIM"), t("desarrollo y pruebas", "development and tests"), group=True),
        Node("wsaa", 400, 302, 160, 80, t("WSAA", "WSAA"), t("loginCms", "loginCms")),
        Node("wsfe", 580, 302, 160, 80, t("WSFEv1", "WSFEv1"), t("22 operaciones", "22 operations")),
        Node("panel", 400, 432, 340, 106, t("Panel /arcasim/", "Panel /arcasim/"), t("contribuyentes · reloj · fallas", "taxpayers · clock · failures")),
        Node("arca", 880, 330, 270, 130, t("ARCA", "ARCA"), t("homologación\nproducción", "homologación\nproduction")),
    ],
    wires=[
        [(280, 405), (480, 405), (480, 382)],
        [(480, 405), (660, 405), (660, 382)],
        [(165, 460), (165, 606), (1015, 606), (1015, 460)],
    ],
    steps=[
        Step(t("El ticket de acceso", "The access ticket"),
             t("La aplicación firma el pedido con su certificado, como lo haría con ARCA. ArcaSim lo valida en el mismo orden y entrega un ticket por 12 horas.",
               "The application signs the request with its certificate, as it would for ARCA. ArcaSim checks it in the same order and hands out a ticket for 12 hours."),
             active=["app", "wsaa"],
             packets=[Packet([(280, 405), (480, 405), (480, 382)], t("TRA firmado", "signed TRA"))],
             badges=[Badge(165, 492, t("ticket · 12 h", "ticket · 12 h"), "ok")]),
        Step(t("El CAE", "The CAE"),
             t("Mismas operaciones, mismos códigos de error y los mismos textos que devuelve ARCA. El comprobante queda numerado y se puede consultar después.",
               "Same operations, same error codes and the same texts ARCA answers with. The voucher gets its number and can be looked up later."),
             active=["app", "wsfe"],
             packets=[Packet([(280, 405), (660, 405), (660, 382)], t("Factura B", "Invoice B"))],
             badges=[Badge(165, 492, t("A · CAE 71263951827464", "A · CAE 71263951827464"), "ok")]),
        Step(t("Las fallas, a pedido", "Failures on demand"),
             t("Lo que con ARCA real es difícil de provocar: el servicio caído, una demora, un rechazo con el código que se elija o la respuesta que no llega.",
               "What is hard to cause against the real ARCA: the service down, a delay, a rejection with a chosen code, or an answer that never arrives."),
             active=["panel", "wsfe"],
             badges=[Badge(570, 590, t("caído · demora · rechazo 10016 · respuesta cortada", "down · delay · rejection 10016 · dropped answer"), "stop")]),
        Step(t("El reloj de ArcaSim", "ArcaSim's clock"),
             t("Detenerlo o adelantarlo: vencer el ticket, salirse del rango de fechas o cruzar el 01/12/2026, cuando la condición frente al IVA pasa a ser obligatoria.",
               "Freeze it or move it ahead: expire the ticket, leave the date range or cross 01/12/2026, when the receiver's VAT condition becomes mandatory."),
             active=["panel", "wsaa", "wsfe"],
             badges=[Badge(570, 590, t("+13 h: ticket vencido · 01/12/2026: rechazo 10246", "+13 h: expired ticket · 01/12/2026: rejection 10246"), "data")]),
        Step(t("A producción, la misma aplicación", "To production, the same application"),
             t("Se cambian las dos direcciones y el certificado por el de ARCA. El código no se toca: las llamadas son las mismas.",
               "The two addresses and the certificate change to ARCA's. The code stays as it is: the calls are the same."),
             active=["app", "arca"],
             packets=[Packet([(165, 460), (165, 606), (1015, 606), (1015, 460)], t("mismas llamadas", "same calls"), "ok")],
             badges=[Badge(165, 300, t("2 URL + certificado", "2 URLs + certificate"), "accent")]),
    ],
    foot=t("La aplicación usa su cliente real de ARCA desde el primer día. Pasar de ArcaSim a homologación o a producción es configuración, no código.",
           "The application uses its real ARCA client from day one. Moving from ArcaSim to homologación or production is configuration, not code."),
)

# 2. When the answer gets lost ---------------------------------------------------------------

recovery = Diagram(
    slug="recuperacion",
    kicker=t("Si la respuesta se pierde", "When the answer gets lost"),
    nodes=[
        Node("app", 60, 330, 230, 130, t("Facturación", "Billing"), t("Arca.Client", "Arca.Client")),
        Node("wsfe", 480, 330, 240, 130, t("WSFEv1", "WSFEv1"), t("ArcaSim o ARCA", "ArcaSim or ARCA")),
        Node("store", 900, 330, 240, 130, t("Comprobantes", "Vouchers"), t("autorizados", "authorized")),
    ],
    wires=[
        [(290, 395), (480, 395)],
        [(720, 395), (900, 395)],
    ],
    steps=[
        Step(t("El próximo número", "The next number"),
             t("La numeración es correlativa por punto de venta y tipo: antes de pedir el CAE, el cliente pregunta cuál fue el último.",
               "Numbering runs per point of sale and type: before asking for the CAE, the client asks which was the last one."),
             active=["app", "wsfe"],
             packets=[Packet([(290, 395), (480, 395)], t("último?", "last?"), "data")],
             badges=[Badge(175, 492, t("último 41 → próximo 42", "last 41 → next 42"), "data")]),
        Step(t("Pide el CAE del 42", "Asks for the CAE of 42"),
             t("El comprobante 42 sale con sus importes, su receptor y su condición frente al IVA.",
               "Voucher 42 goes out with its amounts, its receiver and its VAT condition."),
             active=["app", "wsfe", "store"],
             packets=[Packet([(290, 395), (480, 395)], t("42", "42")),
                      Packet([(720, 395), (900, 395)], t("42 · CAE", "42 · CAE"), "ok")]),
        Step(t("La respuesta no llega", "The answer never arrives"),
             t("ARCA otorgó el CAE y guardó el 42, pero la conexión se cortó antes de la respuesta. El cliente no sabe si se autorizó.",
               "ARCA granted the CAE and stored 42, but the connection dropped before the answer. The client does not know whether it went through."),
             active=["store"],
             cuts=[Cut(385, 395)],
             badges=[Badge(385, 470, t("conexión cortada", "connection dropped"), "stop"),
                     Badge(1020, 492, t("42 autorizado", "42 authorized"), "ok")]),
        Step(t("Reenviar a ciegas, no", "No blind retry"),
             t("ARCA no es idempotente: el mismo pedido otra vez devuelve 10016, porque el 42 ya figura como emitido.",
               "ARCA is not idempotent: the same request again answers 10016, because 42 is already issued."),
             active=["wsfe"],
             packets=[Packet([(290, 395), (480, 395)], t("42 otra vez", "42 again"), "stop")],
             badges=[Badge(600, 492, t("10016: no es el próximo", "10016: not the next one"), "stop")]),
        Step(t("Consultar antes de reintentar", "Ask before retrying"),
             t("Arca.Client consulta el 42 con FECompConsultar, como indica el manual de ARCA para los errores de comunicación.",
               "Arca.Client looks up 42 with FECompConsultar, as ARCA's manual prescribes for communication errors."),
             active=["app", "wsfe", "store"],
             packets=[Packet([(290, 395), (480, 395)], t("FECompConsultar 42", "FECompConsultar 42"), "data"),
                      Packet([(720, 395), (900, 395)], t("42?", "42?"), "data")]),
        Step(t("El CAE, recuperado", "The CAE, recovered"),
             t("El 42 existe con los mismos importes y el mismo receptor: la factura queda aprobada con su CAE, sin duplicar nada.",
               "42 exists with the same amounts and receiver: the invoice is approved with its CAE, nothing duplicated."),
             active=["store", "wsfe", "app"],
             packets=[Packet([(900, 395), (720, 395), (480, 395), (290, 395)], t("CAE del 42", "CAE of 42"), "ok")],
             badges=[Badge(175, 492, t("aprobada · recuperada", "approved · recovered"), "ok")]),
    ],
    foot=t("Con ARCA real esta situación casi no se puede provocar. ArcaSim la produce a pedido: otorga el CAE y corta la conexión antes de responder.",
           "With the real ARCA this is almost impossible to cause. ArcaSim does it on demand: it grants the CAE and drops the connection before answering."),
)

DIAGRAMS = [module, recovery]


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for diagram in DIAGRAMS:
        for lang in ("es", "en"):
            suffix = "" if lang == "es" else "_en"
            path = OUT / f"{diagram.slug}{suffix}.html"
            path.write_text(render(diagram, lang), encoding="utf-8", newline="\n")
            print(path.relative_to(OUT.parent.parent))


if __name__ == "__main__":
    main()
