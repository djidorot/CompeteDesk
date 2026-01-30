(() => {
  "use strict";

  const form = document.querySelector("[data-strategy-ai-form]");
  if (!form) return;

  const strategyId = form.getAttribute("data-strategy-id");
  const statusEl = form.querySelector("[data-ai-status]");
  const btnGenerate = form.querySelector("[data-ai-generate]");
  const btnCreateActions = form.querySelector("[data-ai-create-actions]");
  const resultWrap = document.querySelector("[data-ai-result]");
  const sectionsEl = document.querySelector("[data-ai-sections]");

  const getVal = (sel) => {
    const el = form.querySelector(sel);
    return el ? (el.value || "").trim() : "";
  };

  const antiForgeryToken = () => {
    const input = form.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : "";
  };

  const setStatus = (text, isError = false) => {
    if (!statusEl) return;
    statusEl.textContent = text || "";
    statusEl.classList.toggle("text-danger", !!isError);
  };

  const safeText = (v) => {
    if (v === null || v === undefined) return "";
    if (typeof v === "string") return v;
    try { return JSON.stringify(v); } catch { return String(v); }
  };

  const renderList = (items) => {
    if (!Array.isArray(items) || items.length === 0) return "<div class=\"cd-muted\">—</div>";
    const lis = items.map((x) => `<li>${escapeHtml(safeText(x))}</li>`).join("");
    return `<ul class=\"cd-ai__list\">${lis}</ul>`;
  };

  const escapeHtml = (s) =>
    String(s)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const section = (title, bodyHtml) => {
    return `
      <div class=\"cd-ai__section\">
        <h3 class=\"cd-ai__sectionTitle\">${escapeHtml(title)}</h3>
        <div>${bodyHtml}</div>
      </div>
    `;
  };

  const renderPlaybook = (pb) => {
    if (!pb || typeof pb !== "object") return;

    const oneLine = pb.oneLineSummary ? `<div class=\"cd-ai__pill\">${escapeHtml(pb.oneLineSummary)}</div>` : "";

    const battlefield = pb.battlefield && typeof pb.battlefield === "object" ? pb.battlefield : null;
    const battlefieldHtml = battlefield
      ? `
        <div class=\"cd-ai__grid2\">
          <div class=\"cd-ai__kv\"><div class=\"cd-ai__kvKey\">Market</div><div class=\"cd-ai__kvVal\">${escapeHtml(battlefield.market || "")}</div></div>
          <div class=\"cd-ai__kv\"><div class=\"cd-ai__kvKey\">Enemy</div><div class=\"cd-ai__kvVal\">${escapeHtml(battlefield.enemy || "")}</div></div>
          <div class=\"cd-ai__kv\"><div class=\"cd-ai__kvKey\">Likely move</div><div class=\"cd-ai__kvVal\">${escapeHtml(battlefield.theirLikelyMove || "")}</div></div>
          <div class=\"cd-ai__kv\"><div class=\"cd-ai__kvKey\">Our edge</div><div class=\"cd-ai__kvVal\">${escapeHtml(battlefield.ourEdge || "")}</div></div>
        </div>
      `
      : "<div class=\"cd-muted\">—</div>";

    const exec = Array.isArray(pb.executionPlan) ? pb.executionPlan : [];
    const execHtml = exec.length
      ? `<ol class=\"cd-ai__list\">${exec
          .map((x) => {
            const t = escapeHtml(x.title || "");
            const d = escapeHtml(x.detail || "");
            const meta = [x.ownerRole, x.timeframe, x.successMetric].filter(Boolean).map(escapeHtml).join(" • ");
            return `<li><strong>${t}</strong>${meta ? ` <span class=\"cd-muted\">(${meta})</span>` : ""}<div class=\"cd-muted\" style=\"margin-top:4px\">${d}</div></li>`;
          })
          .join("")}</ol>`
      : "<div class=\"cd-muted\">—</div>";

    const counter = Array.isArray(pb.counterMoves) ? pb.counterMoves : [];
    const counterHtml = counter.length
      ? `<ol class=\"cd-ai__list\">${counter
          .map((x) => {
            const em = escapeHtml(x.enemyMove || "");
            const or = escapeHtml(x.ourResponse || "");
            const sig = escapeHtml(x.signalToWatch || "");
            return `<li><div><strong>Enemy:</strong> ${em}</div><div><strong>Our response:</strong> ${or}</div>${sig ? `<div class=\"cd-muted\"><strong>Signal:</strong> ${sig}</div>` : ""}</li>`;
          })
          .join("")}</ol>`
      : "<div class=\"cd-muted\">—</div>";

    const risks = Array.isArray(pb.risks) ? pb.risks : [];
    const risksHtml = risks.length
      ? `<ol class=\"cd-ai__list\">${risks
          .map((x) => {
            const r = escapeHtml(x.risk || "");
            const m = escapeHtml(x.mitigation || "");
            const sev = escapeHtml(x.severity || "");
            return `<li><div><strong>${r}</strong> ${sev ? `<span class=\"cd-ai__pill\">${sev}</span>` : ""}</div><div class=\"cd-muted\">${m}</div></li>`;
          })
          .join("")}</ol>`
      : "<div class=\"cd-muted\">—</div>";

    const kpis = Array.isArray(pb.kpis) ? pb.kpis : [];
    const kpisHtml = kpis.length
      ? `<ol class=\"cd-ai__list\">${kpis
          .map((x) => {
            const n = escapeHtml(x.name || "");
            const t = escapeHtml(x.target || "");
            const w = escapeHtml(x.why || "");
            return `<li><strong>${n}</strong> — ${t}<div class=\"cd-muted\">${w}</div></li>`;
          })
          .join("")}</ol>`
      : "<div class=\"cd-muted\">—</div>";

    sectionsEl.innerHTML = [
      section("Summary", `${oneLine}${pb.strategicAim ? `<div style=\"margin-top:8px\"><strong>Aim:</strong> ${escapeHtml(pb.strategicAim)}</div>` : ""}`),
      section("Battlefield", battlefieldHtml),
      section("Principle fit", `
        <div><strong>Why:</strong> ${escapeHtml(pb.principleFit?.whyThisStrategy || "")}</div>
        <div style=\"margin-top:6px\"><strong>When not to use:</strong> ${escapeHtml(pb.principleFit?.whenNotToUse || "")}</div>
        <div style=\"margin-top:10px\"><strong>Assumptions to validate:</strong></div>
        ${renderList(pb.principleFit?.assumptionsToValidate || [])}
      `),
      section("Execution plan", execHtml),
      section("Counter-moves", counterHtml),
      section("Quick wins", renderList(pb.quickWins || [])),
      section("Risks & mitigations", risksHtml),
      section("KPIs", kpisHtml),
      section("Open questions", renderList(pb.questions || [])),
    ].join("");

    resultWrap.hidden = false;
    btnCreateActions.disabled = !(Array.isArray(pb.recommendedActions) && pb.recommendedActions.length);
  };

  const tryLoadInitial = () => {
    const el = document.querySelector("[data-ai-initial]");
    if (!el) return;
    const raw = (el.textContent || "").trim();
    if (!raw) return;
    try {
      const pb = JSON.parse(raw);
      renderPlaybook(pb);
      setStatus("Loaded last AI playbook.");
    } catch {
      // ignore
    }
  };

  const postJson = async (url, payload) => {
    const res = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": antiForgeryToken(),
      },
      body: JSON.stringify(payload),
    });

    const text = await res.text();
    let json;
    try { json = JSON.parse(text); } catch { json = { ok: res.ok, raw: text }; }

    if (!res.ok) {
      const msg = json?.error || json?.message || json?.raw || "Request failed.";
      throw new Error(msg);
    }

    return json;
  };

  form.addEventListener("submit", async (e) => {
    e.preventDefault();

    btnGenerate.disabled = true;
    btnCreateActions.disabled = true;
    setStatus("Generating playbook…");

    const payload = {
      marketOrArena: getVal("[data-ai-market]"),
      objective: getVal("[data-ai-objective]"),
      competitor: getVal("[data-ai-competitor]"),
      ourPosition: getVal("[data-ai-position]"),
      constraints: getVal("[data-ai-constraints]"),
      timeHorizon: getVal("[data-ai-time]"),
      ethicalLine: getVal("[data-ai-ethics]"),
      successDefinition: getVal("[data-ai-success]"),
    };

    try {
      const json = await postJson(`/Strategies/GenerateAiPlaybook/${encodeURIComponent(strategyId)}`, payload);
      const aiJson = json.aiJson || "";
      const pb = JSON.parse(aiJson);
      renderPlaybook(pb);
      setStatus(json.summary || "Playbook generated.");
    } catch (err) {
      setStatus(err?.message || "AI generation failed.", true);
    } finally {
      btnGenerate.disabled = false;
    }
  });

  btnCreateActions?.addEventListener("click", async () => {
    btnCreateActions.disabled = true;
    setStatus("Creating Action Items…");

    try {
      const json = await postJson(`/Strategies/CreateActionsFromAi/${encodeURIComponent(strategyId)}`, {});
      setStatus(`Created ${json.created} action item(s). You can view them in Actions.`);
    } catch (err) {
      setStatus(err?.message || "Failed to create action items.", true);
      btnCreateActions.disabled = false;
    }
  });

  tryLoadInitial();
})();
