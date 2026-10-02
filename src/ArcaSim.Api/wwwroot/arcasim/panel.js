// ArcaSim's panel: a thin page over /arcasim/api. Every action reloads what it changed.
const api = (path, options = {}) =>
  fetch(`/arcasim/api${path}`, {
    headers: { "Content-Type": "application/json" },
    ...options,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  }).then(async (response) => {
    if (!response.ok) {
      const text = await response.text();
      let message = text;
      try { message = JSON.parse(text).error ?? text; } catch { /* plain text */ }
      throw new Error(message || `HTTP ${response.status}`);
    }
    const type = response.headers.get("Content-Type") ?? "";
    return type.includes("json") ? response.json() : response;
  });

const $ = (selector) => document.querySelector(selector);
const form = (element) => Object.fromEntries(new FormData(element));

function toast(message, error = false) {
  const box = $("#toast");
  box.textContent = message;
  box.className = error ? "toast error" : "toast";
  box.hidden = false;
  clearTimeout(box.timer);
  box.timer = setTimeout(() => (box.hidden = true), 3500);
}

const run = (action) => async (event) => {
  event?.preventDefault();
  try {
    await action(event);
  } catch (error) {
    toast(error.message, true);
  }
};

function fill(table, rows) {
  const body = $(`${table} tbody`);
  body.replaceChildren(...rows.map((cells) => {
    const tr = document.createElement("tr");
    for (const cell of cells) {
      const td = document.createElement("td");
      td.textContent = cell ?? "";
      tr.append(td);
    }
    return tr;
  }));
}

const vatNames = {
  ResponsableInscripto: "RI",
  Monotributo: "Monotributo",
  Exento: "Exento",
  ConsumidorFinal: "Consumidor Final",
};

async function loadStatus() {
  const status = await api("/status");
  $("#now").textContent = new Date(status.clock.now).toLocaleString("es-AR", { timeZone: "America/Argentina/Buenos_Aires", hour12: false });
  $("#frozen").hidden = !status.clock.frozen;
  const settings = $("#settings");
  settings.environment.value = status.environment;
  settings.manual.value = status.followsCalendar ? "calendar" : status.manualVersion === "4.8" ? "V4_8" : "V4_7";
  settings.threshold.value = status.finalConsumerIdentificationThreshold;
  settings.replay.checked = status.replayWindowEnabled;
  settings.open.checked = status.openAccess;
  $("#chaos-state").textContent = Object.keys(status.chaos).length === 0
    ? "Sin fallas activas."
    : Object.entries(status.chaos).map(([service, c]) =>
        `${service}: ${c.down ? "caído" : "arriba"}, demora ${c.delayMilliseconds} ms` +
        `${c.dropNextResponse ? ", corta la próxima respuesta" : ""}` +
        `${c.pendingForcedRejections ? `, ${c.pendingForcedRejections} rechazo(s) forzado(s)` : ""}`).join("\n");
}

async function loadTaxpayers() {
  const taxpayers = await api("/taxpayers");
  fill("#taxpayers", taxpayers.map((t) => [
    t.cuit,
    t.active ? t.name : `${t.name} (inactivo)`,
    vatNames[t.vatCondition] ?? t.vatCondition,
    t.pointsOfSale.map((p) => `${p.number} ${p.kind === "WebServiceCae" ? "CAE" : p.kind === "WebServiceCaea" ? "CAEA" : "otro"}${p.blocked ? " (bloqueado)" : ""}`).join(", "),
  ]));
}

async function loadAuthorizations() {
  const authorizations = await api("/authorizations");
  fill("#authorizations", authorizations.map((a) => [a.clientCuit, a.alias, a.representedCuit, a.service]));
}

async function loadVouchers() {
  const vouchers = await api("/vouchers?limit=100");
  fill("#vouchers", vouchers.map((v) => [
    new Date(v.processedAt).toLocaleString("es-AR", { timeZone: "America/Argentina/Buenos_Aires", hour12: false }),
    v.cuit,
    v.pointOfSale,
    v.voucherType,
    v.from === v.to ? v.from : `${v.from}-${v.to}`,
    v.date,
    `${v.currency} ${v.total}`,
    `${v.receiver.docTipo} ${v.receiver.docNro}`,
    `${v.emissionType} ${v.authorizationCode}`,
    v.observations.map((o) => o.code).join(", "),
  ]));
}

const reloadAll = () => Promise.all([loadStatus(), loadTaxpayers(), loadAuthorizations(), loadVouchers()]);

$("#settings").addEventListener("submit", run(async (event) => {
  const values = form(event.target);
  await api("/settings", {
    method: "PUT",
    body: {
      environment: values.environment,
      followCalendar: values.manual === "calendar",
      manualVersion: values.manual === "calendar" ? null : values.manual,
      finalConsumerIdentificationThreshold: Number(values.threshold),
      replayWindowEnabled: event.target.replay.checked,
      openAccess: event.target.open.checked,
    },
  });
  await loadStatus();
  toast("Simulación actualizada.");
}));

document.querySelectorAll("[data-advance]").forEach((button) =>
  button.addEventListener("click", run(async () => {
    await api("/clock", { method: "POST", body: { advanceMinutes: Number(button.dataset.advance) } });
    await loadStatus();
  })));

$("#clock-reset").addEventListener("click", run(async () => {
  await api("/clock", { method: "DELETE" });
  await loadStatus();
}));

$("#freeze").addEventListener("submit", run(async (event) => {
  const at = form(event.target).at;
  if (!at) return;
  await api("/clock", { method: "POST", body: { freezeAt: `${at.length === 16 ? `${at}:00` : at}-03:00` } });
  await loadStatus();
}));

$("#taxpayer").addEventListener("submit", run(async (event) => {
  const values = form(event.target);
  const points = values.point ? [{ number: Number(values.point), kind: values.kind }] : [];
  await api(`/taxpayers/${values.cuit}`, {
    method: "PUT",
    body: { name: values.name, vatCondition: values.vatCondition, active: true, pointsOfSale: points },
  });
  await loadTaxpayers();
  toast(`Contribuyente ${values.cuit} guardado.`);
}));

$("#certificate").addEventListener("submit", run(async (event) => {
  const values = form(event.target);
  const body = {
    cuit: Number(values.cuit),
    alias: values.alias,
    password: values.password,
    csr: values.csr || null,
    services: values.services.split(/[\s,]+/).filter(Boolean),
  };
  const result = await api("/certificates", { method: "POST", body });
  const out = $("#certificate-out");
  if (result instanceof Response) {
    const blob = await result.blob();
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = `arcasim-${values.cuit}-${values.alias}.pfx`;
    link.click();
    out.hidden = true;
    toast("Certificado emitido: se descargó el PFX.");
  } else {
    out.textContent = result.certificate;
    out.hidden = false;
    toast("CSR firmado.");
  }
  await loadAuthorizations();
}));

$("#chaos").addEventListener("submit", run(async (event) => {
  const values = form(event.target);
  await api(`/chaos/${values.service}`, {
    method: "PUT",
    body: {
      down: event.target.down.checked,
      delayMilliseconds: Number(values.delay || 0),
      dropNextResponse: event.target.drop.checked,
      forceRejection: values.reject ? Number(values.reject) : null,
    },
  });
  event.target.reject.value = "";
  await loadStatus();
  toast("Fallas aplicadas.");
}));

$("#rate").addEventListener("submit", run(async (event) => {
  const values = form(event.target);
  await api("/rates", { method: "PUT", body: { currency: values.currency, day: values.day, rate: Number(values.rate) } });
  toast(`Cotización de ${values.currency} cargada.`);
}));

$("#refresh").addEventListener("click", run(loadVouchers));

$("#reset").addEventListener("click", run(async () => {
  if (!confirm("Se borran contribuyentes, autorizaciones, comprobantes y fallas. ¿Seguir?")) return;
  await api("/reset", { method: "POST" });
  await reloadAll();
  toast("ArcaSim quedó vacío.");
}));

// The meter, ported from rate-guardian's dashboard: a half dial, green to 50, amber to 80, red after.
function gauge(percent) {
  const pct = Math.max(0, Math.min(100, percent));
  const cx = 120, cy = 110, r = 80;
  const point = (p) => {
    const a = ((-180 + p * 1.8) * Math.PI) / 180;
    return [cx + r * Math.cos(a), cy + r * Math.sin(a)];
  };
  const arc = (from, to, color) => {
    const [x1, y1] = point(from), [x2, y2] = point(to);
    return `<path d="M ${x1.toFixed(1)} ${y1.toFixed(1)} A ${r} ${r} 0 ${to - from > 50 ? 1 : 0} 1 ${x2.toFixed(1)} ${y2.toFixed(1)}" stroke="${color}" stroke-width="10" fill="none" stroke-linecap="round"/>`;
  };
  const color = pct < 50 ? "#3ccf91" : pct < 80 ? "#ffcf5c" : "#ff6b6b";
  const [nx, ny] = point(pct);
  return `<svg width="240" height="148" viewBox="0 0 240 148" role="img" aria-label="Saturación ${pct.toFixed(1)}%">
    <path d="M 40 110 A 80 80 0 0 1 200 110" stroke="#262b36" stroke-width="12" fill="none" stroke-linecap="round"/>
    ${arc(0, 50, "#1f6b4c")}${arc(50, 80, "#7a6324")}${arc(80, 100, "#7a2c2c")}
    <line x1="${cx}" y1="${cy}" x2="${nx.toFixed(1)}" y2="${ny.toFixed(1)}" stroke="${color}" stroke-width="2.5" stroke-linecap="round"/>
    <circle cx="${cx}" cy="${cy}" r="5" fill="${color}"/>
    <text x="120" y="140" fill="${color}" font-family="ui-monospace, monospace" font-size="14" text-anchor="middle">${pct.toFixed(1)}%</text>
  </svg>`;
}

async function loadTraffic() {
  const services = await api("/traffic");
  const names = { wsfe: "WSFEv1", wsaa: "WSAA" };
  const box = $("#meters");
  box.replaceChildren(...services.map((t) => {
    const el = document.createElement("div");
    el.className = "meter";
    const m = t.lastMinute;
    el.innerHTML = `<h3>${names[t.service] ?? t.service}</h3>${gauge(t.saturationPercent)}
      <dl>
        <dt>Pedidos (1 min)</dt><dd>${m.requests}</dd>
        <dt>Rechazados</dt><dd>${m.refused}</dd>
        <dt>Atendiendo / en cola</dt><dd>${t.inFlight} / ${t.queued}</dd>
        <dt>Promedio / p95</dt><dd>${m.averageMilliseconds} / ${m.p95Milliseconds} ms</dd>
      </dl>`;
    return el;
  }));
  const form = $("#traffic");
  const current = services.find((t) => t.service === form.service.value);
  if (current && !form.contains(document.activeElement)) {
    for (const key of ["requestsPerMinute", "capacity", "serviceTimeMilliseconds", "queueLimit"]) form[key].value = current.limits[key];
  }
}

$("#traffic").addEventListener("submit", run(async (event) => {
  const values = form(event.target);
  await api(`/traffic/${values.service}`, {
    method: "PUT",
    body: {
      requestsPerMinute: Number(values.requestsPerMinute),
      capacity: Number(values.capacity),
      serviceTimeMilliseconds: Number(values.serviceTimeMilliseconds),
      queueLimit: Number(values.queueLimit),
    },
  });
  await loadTraffic();
  toast("Límites de tráfico aplicados.");
}));

$("#traffic").service.addEventListener("change", () => loadTraffic().catch(() => {}));

run(reloadAll)();
run(loadTraffic)();
setInterval(() => loadStatus().catch(() => {}), 5000);
setInterval(() => loadTraffic().catch(() => {}), 1000);
