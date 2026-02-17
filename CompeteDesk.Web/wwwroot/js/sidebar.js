
(() => {
  "use strict";

  const body = document.body;

  const openBtn = document.querySelector("[data-cd-sidebar-open]");
  const collapseBtn = document.querySelector("[data-cd-sidebar-collapse]");
  const backdrop = document.querySelector("[data-cd-sidebar-backdrop]");

  const isMobile = () => window.matchMedia("(max-width: 767.98px)").matches;

  const syncCollapseBtn = () => {
    if (!collapseBtn) return;

    const collapsed = body.classList.contains("cd-sidebar-collapsed");
    collapseBtn.setAttribute("aria-label", collapsed ? "Expand sidebar" : "Collapse sidebar");
    collapseBtn.setAttribute("title", collapsed ? "Expand" : "Collapse");
  };

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
      // On mobile, the sidebar is an off-canvas drawer.
      // Treat the collapse button as a "close" action.
      if (isMobile()) {
        body.classList.remove("cd-sidebar-open");
        return;
      }

      body.classList.toggle("cd-sidebar-collapsed");

      syncCollapseBtn();

      // Persist preference (desktop only)
      try {
        localStorage.setItem(
          "cd.sidebar.collapsed",
          body.classList.contains("cd-sidebar-collapsed") ? "1" : "0"
        );
      } catch { }
    });
  }

  // Restore collapse state
  try {
    if (!isMobile() && localStorage.getItem("cd.sidebar.collapsed") === "1") {
      body.classList.add("cd-sidebar-collapsed");
    }
  } catch { }

  syncCollapseBtn();

  // Mobile should never start in the "collapsed rail" state
  const normalizeMobileState = () => {
    if (isMobile()) {
      body.classList.remove("cd-sidebar-collapsed");
      // Also close the drawer when switching to mobile
      body.classList.remove("cd-sidebar-open");
    }

    // Ensure labels reflect current state after viewport changes.
    syncCollapseBtn();
  };

  normalizeMobileState();
  window.addEventListener("resize", normalizeMobileState);

  // Close on ESC (mobile)
  window.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      body.classList.remove("cd-sidebar-open");
    }
  });
})();
