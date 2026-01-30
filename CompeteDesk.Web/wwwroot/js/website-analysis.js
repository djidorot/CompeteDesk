(() => {
  "use strict";

  const form = document.querySelector('[data-wa-form]');
  if (!form) return;

  const btn = form.querySelector('[data-wa-submit]');

  form.addEventListener('submit', () => {
    if (!btn) return;
    btn.disabled = true;
    btn.textContent = 'Analyzing...';
  });
})();
