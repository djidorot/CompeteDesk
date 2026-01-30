
(() => {
  "use strict";

  const body = document.body;

  const openBtn = document.querySelector("[data-cd-sidebar-open]");
  const collapseBtn = document.querySelector("[data-cd-sidebar-collapse]");
  const backdrop = document.querySelector("[data-cd-sidebar-backdrop]");

  // Mobile open
  if (openBtn) {
    openBtn.addEventListener("click", () => {
      body.classList.add("cd-sidebar-open");
    });
  }

  // Backdrop close
  if (backdrop) {
    backdrop.addEventListener("click", () => {
      body.classList.remove("cd-sidebar-open");
    });
  }

  // Collapse (desktop)
  if (collapseBtn) {
    collapseBtn.addEventListener("click", () => {
      body.classList.toggle("cd-sidebar-collapsed");
      // Persist preference
      try {
        localStorage.setItem("cd.sidebar.collapsed", body.classList.contains("cd-sidebar-collapsed") ? "1" : "0");
      } catch { }
    });
  }

  // Restore collapse state
  try {
    if (localStorage.getItem("cd.sidebar.collapsed") === "1") {
      body.classList.add("cd-sidebar-collapsed");
    }
  } catch { }

  // Close on ESC (mobile)
  window.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      body.classList.remove("cd-sidebar-open");
    }
  });
})();
