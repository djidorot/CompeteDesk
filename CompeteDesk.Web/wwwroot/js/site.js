// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Generic async pagination (progressive enhancement)
// Usage:
//  - Wrap the paged section with: <div data-async-pager data-target-url="...optional"> ... </div>
//  - Pager links include ?partial=1 (added by _Pager.cshtml when Async=true)
//  - Controller returns PartialView when partial=1.
(function () {
  function closest(el, sel) {
    while (el && el !== document) {
      if (el.matches && el.matches(sel)) return el;
      el = el.parentNode;
    }
    return null;
  }

  async function fetchAndSwap(container, url) {
    const overlay = container.querySelector('[data-loading-overlay]');
    const body = container.querySelector('[data-async-body]') || container;
    if (overlay) overlay.classList.add('show');

    try {
      const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      if (!res.ok) throw new Error('Request failed');
      const html = await res.text();
      body.innerHTML = html;
      // Preserve scroll position near top of container for table paging.
      container.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) {
      // Fallback: navigate normally.
      window.location.href = url;
    } finally {
      if (overlay) overlay.classList.remove('show');
    }
  }

  document.addEventListener('click', function (e) {
    const a = closest(e.target, 'a.page-link');
    if (!a) return;
    const container = closest(a, '[data-async-pager]');
    if (!container) return;
    const href = a.getAttribute('href');
    if (!href) return;
    // Only intercept links that request partial rendering.
    if (href.indexOf('partial=1') === -1) return;
    e.preventDefault();
    fetchAndSwap(container, href);
  });
})();

