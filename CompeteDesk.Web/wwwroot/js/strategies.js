(function () {
  "use strict";

  // Small UX niceties for the Strategies page.
  // - Pressing Enter in the search box already submits the form (browser default)
  // - Here we auto-focus the search box if a query is present.

  const q = document.getElementById("q");
  if (q && q.value) {
    try { q.focus({ preventScroll: true }); } catch { q.focus(); }
    q.setSelectionRange(q.value.length, q.value.length);
  }
})();
