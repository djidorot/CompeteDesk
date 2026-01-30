(() => {
  'use strict';

  const modalEl = document.getElementById('cdBusinessSetupModal');
  if (!modalEl) return;

  const shouldOpen = modalEl.getAttribute('data-cd-autoshow') === '1';
  const form = modalEl.querySelector('form');
  const submitBtn = modalEl.querySelector('[data-cd-biz-submit]');

  if (form && submitBtn)
  {
    form.addEventListener('submit', () => {
      submitBtn.disabled = true;
      submitBtn.textContent = 'Generating…';
    });
  }

  if (shouldOpen && window.bootstrap && window.bootstrap.Modal)
  {
    const m = new window.bootstrap.Modal(modalEl, {
      backdrop: 'static',
      keyboard: false
    });
    m.show();
  }
})();
