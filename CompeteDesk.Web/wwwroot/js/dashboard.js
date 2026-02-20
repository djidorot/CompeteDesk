(() => {
  'use strict';

  const qs = (sel, root = document) => root.querySelector(sel);
  const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));

  // ---------------------------------------------------------------------------
  // Dynamic Summary refresh
  // ---------------------------------------------------------------------------
  const summaryRoot = qs('#overview-summary');

  async function refreshSummary() {
    if (!summaryRoot) return;
    const workspaceId = summaryRoot.getAttribute('data-workspace-id') || '';

    try {
      const url = workspaceId ? `/Dashboard/Summary?workspaceId=${encodeURIComponent(workspaceId)}` : '/Dashboard/Summary';
      const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      if (!res.ok) return;
      const data = await res.json();
      if (!data || !Array.isArray(data.items)) return;

      data.items.forEach(it => {
        const card = qs(`[data-summary-title="${CSS.escape(it.title)}"]`, summaryRoot);
        if (!card) return;

        const countEl = qs('[data-summary-count]', card);
        if (countEl) countEl.textContent = String(it.count ?? 0);

        let badgeEl = qs('[data-summary-badge]', card);
        if (it.badge && String(it.badge).trim().length) {
          if (!badgeEl) {
            const top = qs('.cd-overview-top', card);
            badgeEl = document.createElement('span');
            badgeEl.className = 'cd-overview-badge';
            badgeEl.setAttribute('data-summary-badge', '');
            if (top) top.appendChild(badgeEl);
          }
          if (badgeEl) badgeEl.textContent = it.badge;
        } else if (badgeEl) {
          badgeEl.remove();
        }
      });
    } catch {
      // no-op
    }
  }

  // Refresh on load + when returning via back/forward cache
  refreshSummary();
  window.addEventListener('pageshow', refreshSummary);

  // Mark done button toggles checkbox + styling
  qsa('[data-cd-done]').forEach(btn => {
    btn.addEventListener('click', () => {
      const item = btn.closest('.cd-action-item');
      if (!item) return;
      const cb = qs('[data-cd-action-check]', item);
      if (cb) cb.checked = true;
      item.classList.add('is-done');
    });
  });

  // Checkbox toggles done style
  qsa('[data-cd-action-check]').forEach(cb => {
    cb.addEventListener('change', () => {
      const item = cb.closest('.cd-action-item');
      if (!item) return;
      item.classList.toggle('is-done', cb.checked);
    });
  });

  // Simple filter for high-impact actions
  qsa('[data-cd-filter]').forEach(btn => {
    btn.addEventListener('click', () => {
      const mode = btn.getAttribute('data-cd-filter');
      const list = qs('#cdTodayActions');
      if (!list) return;

      qsa('[data-cd-filter]').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');

      qsa('.cd-action-item', list).forEach(item => {
        const impact = (item.getAttribute('data-impact') || '').toLowerCase();
        const show = (mode === 'all') || (mode === 'high' && impact === 'high');
        item.style.display = show ? '' : 'none';
      });
    });
  });

  // Reschedule (UI-only for now)
  qsa('[data-cd-reschedule]').forEach(btn => {
    btn.addEventListener('click', () => {
      const item = btn.closest('.cd-action-item');
      if (!item) return;
      item.classList.remove('is-done');
      const cb = qs('[data-cd-action-check]', item);
      if (cb) cb.checked = false;
      // lightweight hint
      btn.textContent = 'Rescheduled';
      setTimeout(() => btn.textContent = 'Reschedule', 900);
    });
  });
})();