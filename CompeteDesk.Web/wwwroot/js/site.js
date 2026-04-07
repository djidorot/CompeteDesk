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

// Responsive table wrapping (mobile):
// Automatically wraps Bootstrap tables with a .table-responsive container when not already wrapped.
(function () {
  function wrapTable(table) {
    // If already wrapped, skip.
    if (table.closest && table.closest('.table-responsive')) return;

    const wrapper = document.createElement('div');
    wrapper.className = 'table-responsive';
    table.parentNode.insertBefore(wrapper, table);
    wrapper.appendChild(table);
  }

  document.addEventListener('DOMContentLoaded', function () {
    // Only target main content tables to avoid side effects in navbar/sidebar.
    const scope = document.querySelector('.cd-main') || document.body;
    const tables = scope.querySelectorAll('table');
    tables.forEach(function (t) {
      // Respect explicit opt-out.
      if (t.hasAttribute('data-no-responsive-wrap')) return;
      wrapTable(t);
    });
  });
})();


// Persist primary UI state (workspace selection + page scroll)
(function () {
  const workspaceKey = 'cd.workspace.last';
  const scrollKey = 'cd.page.scroll:' + window.location.pathname;
  const params = new URLSearchParams(window.location.search);
  const explicitWorkspaceId = params.get('workspaceId');

  try {
    if (explicitWorkspaceId && /^\d+$/.test(explicitWorkspaceId) && Number(explicitWorkspaceId) > 0) {
      localStorage.setItem(workspaceKey, explicitWorkspaceId);
    }
  } catch { }

  function findWorkspaceInputs() {
    return Array.from(document.querySelectorAll('select[name="workspaceId"], input[name="workspaceId"]'));
  }

  document.addEventListener('DOMContentLoaded', function () {
    try {
      const savedWorkspaceId = localStorage.getItem(workspaceKey);
      if (savedWorkspaceId && (!explicitWorkspaceId || explicitWorkspaceId === '0')) {
        findWorkspaceInputs().forEach(function (input) {
          if (!input.value) {
            input.value = savedWorkspaceId;
          }
        });
      }
    } catch { }

    try {
      const savedScroll = sessionStorage.getItem(scrollKey);
      if (savedScroll !== null) {
        const top = parseInt(savedScroll, 10);
        if (!Number.isNaN(top)) {
          window.scrollTo({ top: top, left: 0, behavior: 'auto' });
        }
      }
    } catch { }
  });

  document.addEventListener('change', function (e) {
    const target = e.target;
    if (!(target instanceof HTMLSelectElement) && !(target instanceof HTMLInputElement)) return;
    if (target.name !== 'workspaceId') return;
    if (!target.value) return;
    try { localStorage.setItem(workspaceKey, target.value); } catch { }
  }, true);

  document.addEventListener('click', function (e) {
    const link = e.target && e.target.closest ? e.target.closest('a[href]') : null;
    if (!link) return;

    try { sessionStorage.setItem(scrollKey, String(window.scrollY || window.pageYOffset || 0)); } catch { }

    const href = link.getAttribute('href');
    if (!href || href.startsWith('#') || href.startsWith('mailto:') || href.startsWith('tel:')) return;

    try {
      const url = new URL(link.href, window.location.origin);
      const savedWorkspaceId = localStorage.getItem(workspaceKey);
      const eligiblePrefixes = ['/Dashboard', '/Strategies', '/Actions', '/Habits', '/Metrics', '/Recommendations', '/StudyPlanner', '/Exports', '/WebsiteAnalysis', '/BusinessAnalysis', '/WarRoom'];
      const isEligible = eligiblePrefixes.some(function (prefix) { return url.pathname.startsWith(prefix); });
      if (savedWorkspaceId && isEligible && !url.searchParams.has('workspaceId')) {
        url.searchParams.set('workspaceId', savedWorkspaceId);
        link.href = url.pathname + url.search + url.hash;
      }
    } catch { }
  }, true);

  window.addEventListener('beforeunload', function () {
    try { sessionStorage.setItem(scrollKey, String(window.scrollY || window.pageYOffset || 0)); } catch { }
  });
})();
