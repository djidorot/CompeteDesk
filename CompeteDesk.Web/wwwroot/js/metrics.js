(() => {
  "use strict";

  const qs = (s, r = document) => r.querySelector(s);
  const qsa = (s, r = document) => Array.from(r.querySelectorAll(s));

  // Range dropdown
  const range = qs("[data-m-range]");
  if (range) {
    const btn = qs(".cd-mselect__btn", range);
    const menu = qs(".cd-mselect__menu", range);

    const close = () => {
      range.classList.remove("is-open");
      if (btn) btn.setAttribute("aria-expanded", "false");
    };

    btn?.addEventListener("click", (e) => {
      e.preventDefault();
      const open = !range.classList.contains("is-open");
      if (open) {
        range.classList.add("is-open");
        btn.setAttribute("aria-expanded", "true");
      } else {
        close();
      }
    });

    document.addEventListener("click", (e) => {
      if (!range.contains(e.target)) close();
    });

    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape") close();
    });

    // Custom range modal
    const modal = qs("[data-custom-modal]");
    const openCustom = qs("[data-open-custom]", range);
    const openDate = qs("[data-m-date]");
    const openModal = () => {
      if (!modal) return;
      modal.hidden = false;
      close();
    };

    openCustom?.addEventListener("click", (e) => {
      e.preventDefault();
      openModal();
    });
    openDate?.addEventListener("click", (e) => {
      // Treat date pill as "open custom" for now
      e.preventDefault();
      openModal();
    });

    if (modal) {
      qsa("[data-close]", modal).forEach((el) =>
        el.addEventListener("click", (e) => {
          e.preventDefault();
          modal.hidden = true;
        })
      );

      document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") modal.hidden = true;
      });
    }
  }
})();
