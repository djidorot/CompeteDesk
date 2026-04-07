(() => {
  const DEFAULT_FILTERS = {
    entity: "all",
    category: "",
    priority: "",
    status: ""
  };

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

  function getFilters(panel) {
    return {
      entity: panel.dataset.entity || DEFAULT_FILTERS.entity,
      category: panel.dataset.category || DEFAULT_FILTERS.category,
      priority: panel.dataset.priority || DEFAULT_FILTERS.priority,
      status: panel.dataset.status || DEFAULT_FILTERS.status
    };
  }

  function setFilters(panel, next) {
    panel.dataset.entity = next.entity || DEFAULT_FILTERS.entity;
    panel.dataset.category = next.category || DEFAULT_FILTERS.category;
    panel.dataset.priority = next.priority || DEFAULT_FILTERS.priority;
    panel.dataset.status = next.status || DEFAULT_FILTERS.status;
  }

  function renderSelect(label, key, options, selectedValue, allLabel) {
    const optionHtml = [`<option value="">${escapeHtml(allLabel)}</option>`]
      .concat((options || []).map((value) => {
        const selected = value === selectedValue ? ' selected' : '';
        return `<option value="${escapeHtml(value)}"${selected}>${escapeHtml(value)}</option>`;
      }))
      .join("");

    return `
      <label class="cd-searchPanel__filter">
        <span>${escapeHtml(label)}</span>
        <select data-search-filter="${escapeHtml(key)}">${optionHtml}</select>
      </label>
    `;
  }

  function renderSection(title, items) {
    if (!items || !items.length) return "";

    return `
      <section class="cd-searchPanel__section">
        <div class="cd-searchPanel__sectionTitle">${escapeHtml(title)}</div>
        <div class="cd-searchPanel__cards">
          ${items.map((item) => `
            <a class="cd-searchPanel__card" href="${escapeHtml(item.url || '#')}">
              <div class="cd-searchPanel__cardType">${escapeHtml(item.entityType || "item")}</div>
              <div class="cd-searchPanel__cardTitle">${escapeHtml(item.title || "Untitled")}</div>
              <div class="cd-searchPanel__cardSubtitle">${escapeHtml(item.subtitle || "")}</div>
              ${item.meta ? `<div class="cd-searchPanel__cardMeta">${escapeHtml(item.meta)}</div>` : ""}
            </a>
          `).join("")}
        </div>
      </section>
    `;
  }

  function renderResults(panel, q, data) {
    const filters = getFilters(panel);
    const categories = Array.isArray(data.categories) ? data.categories : [];
    const priorities = Array.isArray(data.priorityOptions) ? data.priorityOptions : ["High", "Medium", "Low"];
    const statuses = Array.isArray(data.statusOptions) ? data.statusOptions : ["Active", "Archived"];
    const canSearchUsers = Boolean(data.canSearchUsers);

    panel.hidden = false;
    panel.dataset.open = "1";
    panel.innerHTML = `
      <div class="cd-searchPanel__hdr">
        <div>
          <div class="cd-searchPanel__eyebrow">Global search</div>
          <div class="cd-searchPanel__title">${escapeHtml(q)}</div>
        </div>
        <div class="cd-searchPanel__actions">
          <button type="button" class="cd-searchPanel__btn" data-cd-ai-close>Close</button>
        </div>
      </div>

      <div class="cd-searchPanel__filtersRow">
        ${renderSelect("Type", "entity", ["all", "strategies", "workspaces"].concat(canSearchUsers ? ["users"] : []), filters.entity, "All")}
        ${renderSelect("Category", "category", categories, filters.category, "All categories")}
        ${renderSelect("Priority", "priority", priorities, filters.priority, "All priorities")}
        ${renderSelect("Status", "status", statuses, filters.status, "All statuses")}
      </div>

      <div class="cd-searchPanel__summary">
        <span>${escapeHtml(String(data.totalCount || 0))} result(s)</span>
        <span>Strategies: ${escapeHtml(String(data.strategyCount || 0))}</span>
        <span>Workspaces: ${escapeHtml(String(data.workspaceCount || 0))}</span>
        ${canSearchUsers ? `<span>Users: ${escapeHtml(String(data.userCount || 0))}</span>` : ""}
      </div>

      <div class="cd-searchPanel__body">
        ${(data.totalCount || 0) === 0
          ? `<div class="cd-searchPanel__empty">No matches found. Try a shorter query or adjust the filters.</div>`
          : `${renderSection("Strategies", data.strategies)}${renderSection("Workspaces", data.workspaces)}${canSearchUsers ? renderSection("Users", data.users) : ""}`}
      </div>
    `;
  }

  function renderLoading(panel, q) {
    panel.hidden = false;
    panel.dataset.open = "1";
    panel.innerHTML = `
      <div class="cd-searchPanel__hdr">
        <div>
          <div class="cd-searchPanel__eyebrow">Global search</div>
          <div class="cd-searchPanel__title">${escapeHtml(q)}</div>
        </div>
        <div class="cd-searchPanel__actions">
          <button type="button" class="cd-searchPanel__btn" data-cd-ai-close>Close</button>
        </div>
      </div>
      <div class="cd-searchPanel__loading">
        <span class="cd-searchPanel__spinner"></span>
        Searching your workspace…
      </div>
    `;
  }

  async function runSearch(input, panel) {
    const q = (input.value || "").trim();
    if (!q) {
      closePanel(panel);
      return;
    }

    const filters = getFilters(panel);
    const url = input.dataset.globalSearchUrl || "/api/search/global";
    const params = new URLSearchParams({ q });
    if (filters.entity) params.set("entity", filters.entity);
    if (filters.category) params.set("category", filters.category);
    if (filters.priority) params.set("priority", filters.priority);
    if (filters.status) params.set("status", filters.status);

    renderLoading(panel, q);
    input.setAttribute("aria-busy", "true");

    try {
      const res = await fetch(`${url}?${params.toString()}`, {
        method: "GET",
        headers: { Accept: "application/json" }
      });

      const text = await res.text();
      if (!res.ok) {
        let msg = text || `Request failed (${res.status}).`;
        try {
          const json = JSON.parse(text);
          if (json && json.error) msg = json.error;
        } catch {}
        panel.innerHTML = `<div class="cd-searchPanel__empty">${escapeHtml(msg)}</div>`;
        return;
      }

      const data = JSON.parse(text);
      renderResults(panel, q, data);
    } catch (err) {
      panel.innerHTML = `<div class="cd-searchPanel__empty">${escapeHtml(err && err.message ? err.message : String(err))}</div>`;
    } finally {
      input.removeAttribute("aria-busy");
    }
  }

  document.addEventListener("DOMContentLoaded", () => {
    const input = document.getElementById("cdTopbarAiSearch");
    const panel = document.getElementById("cdTopbarAiSearchPanel");
    if (!input || !panel) return;

    setFilters(panel, DEFAULT_FILTERS);

    let debounceHandle = 0;
    const scheduleSearch = () => {
      window.clearTimeout(debounceHandle);
      debounceHandle = window.setTimeout(() => runSearch(input, panel), 220);
    };

    input.addEventListener("input", scheduleSearch);
    input.addEventListener("focus", () => {
      if ((input.value || "").trim()) {
        scheduleSearch();
      }
    });
    input.addEventListener("keydown", (e) => {
      if (e.key === "Enter") {
        e.preventDefault();
        runSearch(input, panel);
      } else if (e.key === "Escape") {
        closePanel(panel);
      }
    });

    panel.addEventListener("click", (e) => {
      const closeBtn = e.target && e.target.closest ? e.target.closest("[data-cd-ai-close]") : null;
      if (closeBtn) {
        closePanel(panel);
      }
    });

    panel.addEventListener("change", (e) => {
      const target = e.target;
      if (!target || !target.matches || !target.matches("[data-search-filter]")) return;
      const filters = getFilters(panel);
      filters[target.dataset.searchFilter] = target.value || "";
      setFilters(panel, filters);
      runSearch(input, panel);
    });

    document.addEventListener("click", (e) => {
      if (panel.hidden) return;
      const withinSearch = input.closest(".cd-topbar__search");
      if (!withinSearch) return;
      if (withinSearch.contains(e.target)) return;
      closePanel(panel);
    });
  });
})();
