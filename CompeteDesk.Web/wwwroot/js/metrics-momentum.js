(() => {
  "use strict";

  const qs = (s, r = document) => r.querySelector(s);
  const qsa = (s, r = document) => Array.from(r.querySelectorAll(s));

  const modal = qs("[data-km-config-modal]");
  if (!modal) return;

  const openBtn = qs("[data-open-km-config]");

  const open = () => {
    modal.hidden = false;
    // Focus first input for accessibility
    const firstInput = qs("input, button, select, textarea", modal);
    firstInput?.focus();
  };

  const close = () => {
    modal.hidden = true;
  };

  openBtn?.addEventListener("click", (e) => {
    e.preventDefault();
    open();
  });

  qsa("[data-close]", modal).forEach((el) =>
    el.addEventListener("click", (e) => {
      e.preventDefault();
      close();
    })
  );

  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") close();
  });
})();
