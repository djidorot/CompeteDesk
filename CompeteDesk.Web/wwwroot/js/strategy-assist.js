(() => {
  "use strict";

  const root = document.querySelector("[data-strategy-assist]");
  if (!root) return;

  const strategyId = root.getAttribute("data-strategy-id");
  const tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
  const statusEl = root.querySelector("[data-assist-status]");
  const outEl = root.querySelector("[data-assist-output]");
  const goalEl = root.querySelector("[data-assist-goal]");
  const historyLink = root.querySelector("[data-assist-history]");

  const btns = {
    swot: root.querySelector("[data-assist-swot]"),
    study: root.querySelector("[data-assist-study]"),
    quiz: root.querySelector("[data-assist-quiz]"),
    improve: root.querySelector("[data-assist-improve]"),
  };

  const token = () => (tokenInput ? tokenInput.value : "");

  const setStatus = (text, isErr = false) => {
    if (!statusEl) return;
    statusEl.textContent = text || "";
    statusEl.classList.toggle("text-danger", !!isErr);
  };

  const esc = (s) =>
    String(s ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const renderList = (items) => {
    if (!Array.isArray(items) || items.length === 0) return '<div class="cd-muted">—</div>';
    return `<ul class="cd-assist__list">${items.map((x) => `<li>${esc(x)}</li>`).join("")}</ul>`;
  };

  const renderKvList = (items, keys) => {
    if (!Array.isArray(items) || items.length === 0) return '<div class="cd-muted">—</div>';
    return `<ol class="cd-assist__list">${items
      .map((x) => {
        const obj = x && typeof x === "object" ? x : { value: x };
        const lines = keys
          .map((k) => {
            const v = obj[k];
            if (!v) return "";
            const label = k.replace(/([A-Z])/g, " $1").trim();
            return `<div><strong>${esc(label)}:</strong> ${esc(v)}</div>`;
          })
          .filter(Boolean)
          .join("");
        return `<li>${lines || esc(JSON.stringify(obj))}</li>`;
      })
      .join("")}</ol>`;
  };

  const section = (title, bodyHtml) => `
    <div class="cd-assist__section">
      <h3>${esc(title)}</h3>
      <div>${bodyHtml}</div>
    </div>
  `;

  const render = (kind, json) => {
    if (!outEl) return;
    outEl.hidden = false;

    // Basic structured rendering by kind; fallback to raw.
    let html = "";

    if (kind === "Swot") {
      html = [
        section("Strengths", renderList(json.strengths)),
        section("Weaknesses", renderList(json.weaknesses)),
        section("Opportunities", renderList(json.opportunities)),
        section("Threats", renderList(json.threats)),
        section("Next steps", renderKvList(json.nextSteps, ["title", "detail"])),
      ].join("");
      if (json.notes) html = section("Notes", `<div>${esc(json.notes)}</div>`) + html;
    } else if (kind === "StudySummary") {
      const pill = json.oneLine ? `<span class="cd-assist__pill">${esc(json.oneLine)}</span>` : "";
      html = [
        pill ? section("One-line", pill) : "",
        section("Key ideas", renderList(json.keyIdeas)),
        section("When to use", renderList(json.whenToUse)),
        section("When NOT to use", renderList(json.whenNotToUse)),
        section("Examples", renderKvList(json.examples, ["scenario", "whatToDo"])),
        section("Quick checklist", renderList(json.quickChecklist)),
      ].filter(Boolean).join("");
    } else if (kind === "Quiz") {
      const qs = Array.isArray(json.questions) ? json.questions : [];
      html = section(
        json.title || "Quiz",
        qs.length
          ? `<ol class="cd-assist__list">${qs
              .map((q) => {
                const choices = Array.isArray(q.choices) && q.choices.length
                  ? `<div class="cd-muted" style="margin-top:6px">${q.choices.map((c) => `• ${esc(c)}`).join("<br/>")}</div>`
                  : "";
                const ans = q.answer ? `<div style="margin-top:6px"><strong>Answer:</strong> ${esc(q.answer)}</div>` : "";
                const exp = q.explanation ? `<div class="cd-muted" style="margin-top:4px">${esc(q.explanation)}</div>` : "";
                const t = q.type ? `<span class="cd-assist__pill" style="margin-left:6px">${esc(q.type)}</span>` : "";
                return `<li><div><strong>${esc(q.prompt || "")}</strong>${t}</div>${choices}${ans}${exp}</li>`;
              })
              .join("")}</ol>`
          : '<div class="cd-muted">—</div>'
      );
    } else if (kind === "Improvements") {
      html = [
        section("Improvements", renderKvList(json.improvements, ["title", "why", "how"])),
        section("Risks & mitigations", renderKvList(json.risks, ["risk", "mitigation"])),
        section("Metrics", renderKvList(json.metrics, ["name", "target", "why"])),
        section("Focus areas", renderList(json.focusAreas)),
      ].join("");
    } else {
      html = section("Result", `<pre style="white-space:pre-wrap; margin:0">${esc(JSON.stringify(json, null, 2))}</pre>`);
    }

    outEl.innerHTML = html;
  };

  const post = async (url, payload) => {
    const res = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": token(),
      },
      body: JSON.stringify(payload || {}),
    });

    const text = await res.text();
    let json;
    try { json = JSON.parse(text); } catch { json = { raw: text }; }
    if (!res.ok) throw new Error(json?.error || json?.message || "Request failed.");
    return json;
  };

  const setBusy = (busy) => {
    Object.values(btns).forEach((b) => {
      if (b) b.disabled = !!busy;
    });
  };

  const run = async (kind, endpoint, feature) => {
    setBusy(true);
    setStatus(`Generating ${kind}…`);
    if (outEl) outEl.hidden = true;

    try {
      const json = await post(endpoint, { goal: goalEl ? goalEl.value : "" });
      const outputRaw = json.outputJson || "{}";
      const parsed = JSON.parse(outputRaw);
      render(kind, parsed);
      setStatus(`Done. Saved to AI History (Trace #${json.traceId}).`);

      if (historyLink) {
        historyLink.href = `/AiHistory?feature=${encodeURIComponent(feature)}`;
        historyLink.hidden = false;
      }
    } catch (err) {
      setStatus(err?.message || "AI assist failed.", true);
    } finally {
      setBusy(false);
    }
  };

  btns.swot?.addEventListener("click", () => run("Swot", `/AiAssist/StrategySwot/${encodeURIComponent(strategyId)}`, "Strategy.Assist.SWOT"));
  btns.study?.addEventListener("click", () => run("StudySummary", `/AiAssist/StrategyStudySummary/${encodeURIComponent(strategyId)}`, "Strategy.Assist.StudySummary"));
  btns.quiz?.addEventListener("click", () => run("Quiz", `/AiAssist/StrategyQuiz/${encodeURIComponent(strategyId)}`, "Strategy.Assist.Quiz"));
  btns.improve?.addEventListener("click", () => run("Improvements", `/AiAssist/StrategyImprovements/${encodeURIComponent(strategyId)}`, "Strategy.Assist.Improvements"));
})();
