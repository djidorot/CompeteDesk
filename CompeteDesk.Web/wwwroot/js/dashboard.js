(() => {
  'use strict';

  const qs = (sel, root = document) => root.querySelector(sel);
  const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));

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