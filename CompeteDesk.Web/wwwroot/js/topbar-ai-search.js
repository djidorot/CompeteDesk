(() => {
  function escapeHtml(str) {
    return (str || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  function closePanel(panel) {
    panel.hidden = true;
    panel.innerHTML = "";
    panel.dataset.open = "0";
  }

  async function runAiSearch(input, panel) {
    const q = (input.value || "").trim();
    if (!q) return;

    const url = input.dataset.aiSearchUrl || "/api/ai-search";

    panel.hidden = false;
    panel.dataset.open = "1";
    panel.innerHTML = `
      <div class="cd-aiSearchPanel__hdr">
        <div class="cd-aiSearchPanel__title"><span class="cd-aiSearchPanel__spinner"></span>Gemini AI</div>
        <div class="cd-aiSearchPanel__actions">
          <button type="button" class="cd-aiSearchPanel__btn" data-cd-ai-close>Close</button>
        </div>
      </div>
      <div class="cd-aiSearchPanel__body cd-aiSearchPanel__muted">Thinking…</div>
    `;

    input.setAttribute("aria-busy", "true");
    input.disabled = true;

    try {
      const res = await fetch(`${url}?q=${encodeURIComponent(q)}`, {
        method: "GET",
        headers: { "Accept": "application/json" }
      });

      const text = await res.text();
      if (!res.ok) {
        panel.querySelector(".cd-aiSearchPanel__body").innerHTML =
          `<div class="cd-aiSearchPanel__muted">Request failed (${res.status}).</div><div class="cd-aiSearchPanel__muted">${escapeHtml(text)}</div>`;
        return;
      }

      const data = JSON.parse(text);

      // New (Gemini AI Overview) shape:
      // { topic, overview, keyAspects:[], examples:[], elapsedMs }
      // Back-compat (plain text): { answer, elapsedMs }
      const hasOverview = data && (data.topic || data.overview || (Array.isArray(data.keyAspects) && data.keyAspects.length) || (Array.isArray(data.examples) && data.examples.length));
      const answer = (!hasOverview && data && data.answer) ? String(data.answer) : "";
      const meta = [];
      if (data && typeof data.elapsedMs === "number") meta.push(`${data.elapsedMs}ms`);

      const bodyHtml = hasOverview
        ? (() => {
            const topic = escapeHtml(String(data.topic || q));
            const overview = escapeHtml(String(data.overview || ""));
            const keyAspects = Array.isArray(data.keyAspects) ? data.keyAspects.filter(Boolean).slice(0, 8) : [];
            const examples = Array.isArray(data.examples) ? data.examples.filter(Boolean).slice(0, 8) : [];

            const bullets = (items) => {
              if (!items || !items.length) return "";
              return `<ul class="cd-aiSearchPanel__list">${items.map(i => `<li>${escapeHtml(String(i))}</li>`).join("")}</ul>`;
            };

            return `
              <div class="cd-aiSearchPanel__topic">${topic}</div>
              ${overview ? `<div class="cd-aiSearchPanel__overview">${overview}</div>` : ""}
              ${keyAspects.length ? `<div class="cd-aiSearchPanel__sectionTitle">Key aspects</div>${bullets(keyAspects)}` : ""}
              ${examples.length ? `<div class="cd-aiSearchPanel__sectionTitle">Examples</div>${bullets(examples)}` : ""}
            `;
          })()
        : (escapeHtml(answer) || '<span class="cd-aiSearchPanel__muted">No answer returned.</span>');

      panel.innerHTML = `
        <div class="cd-aiSearchPanel__hdr">
          <div class="cd-aiSearchPanel__title">Gemini AI ${meta.length ? `• ${escapeHtml(meta.join(" • "))}` : ""}</div>
          <div class="cd-aiSearchPanel__actions">
            <button type="button" class="cd-aiSearchPanel__btn" data-cd-ai-copy>Copy</button>
            <button type="button" class="cd-aiSearchPanel__btn" data-cd-ai-close>Close</button>
          </div>
        </div>
        <div class="cd-aiSearchPanel__body">${bodyHtml}</div>
      `;

      const copyBtn = panel.querySelector("[data-cd-ai-copy]");
      if (copyBtn) {
        copyBtn.addEventListener("click", async () => {
          try {
            const copyText = hasOverview ? text : answer;
            await navigator.clipboard.writeText(copyText);
            copyBtn.textContent = "Copied";
            setTimeout(() => (copyBtn.textContent = "Copy"), 900);
          } catch {
            copyBtn.textContent = "Copy failed";
            setTimeout(() => (copyBtn.textContent = "Copy"), 900);
          }
        });
      }
    } catch (err) {
      panel.querySelector(".cd-aiSearchPanel__body").innerHTML =
        `<div class="cd-aiSearchPanel__muted">Error: ${escapeHtml(err && err.message ? err.message : String(err))}</div>`;
    } finally {
      input.disabled = false;
      input.removeAttribute("aria-busy");
      input.focus();
    }
  }

  document.addEventListener("DOMContentLoaded", () => {
    const input = document.getElementById("cdTopbarAiSearch");
    const panel = document.getElementById("cdTopbarAiSearchPanel");
    if (!input || !panel) return;

    // Enter triggers AI search
    input.addEventListener("keydown", (e) => {
      if (e.key === "Enter") {
        e.preventDefault();
        runAiSearch(input, panel);
      } else if (e.key === "Escape") {
        closePanel(panel);
      }
    });

    // Close buttons inside panel
    panel.addEventListener("click", (e) => {
      const btn = e.target && e.target.closest ? e.target.closest("[data-cd-ai-close]") : null;
      if (btn) closePanel(panel);
    });

    // Click outside closes panel
    document.addEventListener("click", (e) => {
      if (panel.hidden) return;
      const withinSearch = input.closest(".cd-topbar__search");
      if (!withinSearch) return;
      if (withinSearch.contains(e.target)) return;
      closePanel(panel);
    });
  });
})();
