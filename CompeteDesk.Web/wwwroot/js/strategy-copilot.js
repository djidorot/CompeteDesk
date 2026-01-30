(() => {
  "use strict";

  const form = document.querySelector("[data-copilot-form]");
  if (!form) return;

  const statusEl = form.querySelector("[data-copilot-status]");
  const btn = form.querySelector("[data-copilot-generate]");
  const wsSel = form.querySelector("[data-copilot-workspace]");

  const resultCard = document.querySelector("[data-copilot-result]");
  const sectionsEl = document.querySelector("[data-copilot-sections]");
  const confEl = document.querySelector("[data-copilot-confidence]");

  const token = () => {
    const input = form.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : "";
  };

  const setStatus = (text, isError = false) => {
    if (!statusEl) return;
    statusEl.textContent = text || "";
    statusEl.classList.toggle("text-danger", !!isError);
  };

  const escapeHtml = (s) =>
    String(s ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const checkedIds = (selector) => {
    return Array.from(form.querySelectorAll(selector))
      .filter((x) => x.checked)
      .map((x) => parseInt(x.value, 10))
      .filter((n) => Number.isFinite(n) && n > 0);
  };

  const getVal = (selector) => {
    const el = form.querySelector(selector);
    return el ? (el.value || "").trim() : "";
  };

  const renderList = (items) => {
    if (!Array.isArray(items) || items.length === 0) return '<div class="cd-muted">—</div>';
    return `<ul class="cd-ai__list">${items.map((x) => `<li>${escapeHtml(x)}</li>`).join("")}</ul>`;
  };

  const section = (title, bodyHtml) => {
    return `
      <div class="cd-ai__section">
        <h3 class="cd-ai__sectionTitle">${escapeHtml(title)}</h3>
        <div>${bodyHtml}</div>
      </div>
    `;
  };

  const renderHypotheses = (items) => {
    if (!Array.isArray(items) || items.length === 0) return '<div class="cd-muted">—</div>';
    return items
      .map((h) => {
        const risks = renderList(h.keyRisks || []);
        const exps = renderList(h.quickExperiments || []);
        return `
          <div class="cd-copilot__hypo">
            <div class="cd-copilot__hypoTitle">${escapeHtml(h.title || "")}</div>
            <div class="cd-copilot__hypoGrid">
              <div><div class="cd-k">Unlocks</div><div>${escapeHtml(h.whoItUnlocks || "")}</div></div>
              <div><div class="cd-k">Value leap</div><div>${escapeHtml(h.valueLeap || "")}</div></div>
              <div><div class="cd-k">How to win</div><div>${escapeHtml(h.howToWin || "")}</div></div>
            </div>
            <div class="cd-copilot__hypoSub">
              <div><div class="cd-k">Key risks</div>${risks}</div>
              <div><div class="cd-k">Quick experiments</div>${exps}</div>
            </div>
          </div>
        `;
      })
      .join("");
  };

  const render = (json) => {
    if (!json || typeof json !== "object") return;

    const conf = json.overallConfidence1to5;
    if (confEl) {
      confEl.textContent = Number.isFinite(conf) ? `Confidence ${conf}/5` : "";
      confEl.hidden = !confEl.textContent;
    }

    const narrative = json.strategicNarrative || {};
    const narrativeHtml = `
      <div class="cd-copilot__narrative">
        <div><strong>Situation:</strong> ${escapeHtml(narrative.situation || "")}</div>
        <div style="margin-top:6px"><strong>Insight:</strong> ${escapeHtml(narrative.insight || "")}</div>
        <div style="margin-top:6px"><strong>Choice:</strong> ${escapeHtml(narrative.choice || "")}</div>
        <div style="margin-top:10px"><strong>Moves:</strong></div>
        ${renderList(narrative.moves || [])}
        <div style="margin-top:10px"><strong>Success signals:</strong></div>
        ${renderList(narrative.successSignals || [])}
      </div>
    `;

    const errc = json.errcRecommendations || {};
    const errcHtml = `
      <div class="cd-copilot__errcOut">
        <div><div class="cd-k">Eliminate</div>${renderList(errc.eliminate || [])}</div>
        <div><div class="cd-k">Reduce</div>${renderList(errc.reduce || [])}</div>
        <div><div class="cd-k">Raise</div>${renderList(errc.raise || [])}</div>
        <div><div class="cd-k">Create</div>${renderList(errc.create || [])}</div>
      </div>
    `;

    sectionsEl.innerHTML = [
      json.headline ? section("Headline", `<div class="cd-copilot__headline">${escapeHtml(json.headline)}</div>`) : "",
      section("Blue Ocean hypotheses", renderHypotheses(json.blueOceanHypotheses || [])),
      section("Strategic narrative", narrativeHtml),
      section("ERRC recommendations", errcHtml),
      section("Strategy canvas deltas", renderList(json.strategyCanvasDeltas || [])),
      section("Noncustomer targets", renderList(json.noncustomerTargets || [])),
      section("Next 90 days", renderList(json.next90Days || [])),
    ]
      .filter(Boolean)
      .join("");

    resultCard.hidden = false;
    resultCard.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  const postJson = async (url, payload) => {
    const res = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": token(),
      },
      body: JSON.stringify(payload),
    });

    const text = await res.text();
    let json;
    try {
      json = JSON.parse(text);
    } catch {
      json = { ok: res.ok, raw: text };
    }

    if (!res.ok) {
      const msg = json?.error || json?.message || json?.raw || "Request failed.";
      throw new Error(msg);
    }

    return json;
  };

  wsSel?.addEventListener("change", () => {
    const id = wsSel.value;
    const u = new URL(window.location.href);
    u.searchParams.set("workspaceId", id);
    window.location.href = u.toString();
  });

  form.addEventListener("submit", async (e) => {
    e.preventDefault();

    btn.disabled = true;
    setStatus("Generating…");

    const payload = {
      workspaceId: wsSel ? parseInt(wsSel.value, 10) : null,
      intelIds: checkedIds("[data-copilot-intel]"),
      strategyIds: checkedIds("[data-copilot-strategy]"),
      goal: getVal("[data-copilot-goal]"),
      marketScope: getVal("[data-copilot-scope]"),
      constraints: getVal("[data-copilot-constraints]"),
      strategyCanvasText: getVal("[data-copilot-canvas]"),
      errcEliminate: getVal("[data-copilot-errc-eliminate]"),
      errcReduce: getVal("[data-copilot-errc-reduce]"),
      errcRaise: getVal("[data-copilot-errc-raise]"),
      errcCreate: getVal("[data-copilot-errc-create]"),
    };

    try {
      const json = await postJson("/StrategyCopilot/Generate", payload);
      render(json);
      setStatus("Done.");
    } catch (err) {
      setStatus(err?.message || "AI generation failed.", true);
    } finally {
      btn.disabled = false;
    }
  });
})();
