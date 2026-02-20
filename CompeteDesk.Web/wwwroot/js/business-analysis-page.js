(() => {
    "use strict";

    const form = document.querySelector('form[action="/Dashboard/GenerateBusinessAnalysis"]');
    if (!form) return;

    const btn = form.querySelector('button[type="submit"]');
    const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    const wsInput = form.querySelector('input[name="workspaceId"]');

    const setBusy = (busy) => {
        if (!btn) return;
        btn.disabled = busy;
        btn.textContent = busy ? 'Generating…' : 'Generate';
    };

    const showInlineError = (msg) => {
        const root = document.querySelector('[data-cd-biz-root]');
        if (!root) return;

        let host = root.querySelector('[data-cd-biz-page-error]');
        if (!host) {
            host = document.createElement('div');
            host.setAttribute('data-cd-biz-page-error', '');
            host.className = 'alert alert-danger shadow-sm mb-3 d-none';
            // Place near the top of the page content.
            root.insertBefore(host, root.firstElementChild?.nextSibling || root.firstChild);
        }

        if (!msg) {
            host.classList.add('d-none');
            host.textContent = '';
            return;
        }

        host.textContent = msg;
        host.classList.remove('d-none');
    };

    const maybeRecoverFromFetchFailure = (err) => {
        // In some cases the server may finish generating but the browser connection drops,
        // resulting in a generic "Failed to fetch" error. If that happens, attempt a single
        // reload to pick up the generated report.
        const msg = (err?.message || '').toLowerCase();
        if (!msg.includes('failed to fetch')) return false;

        const key = 'cd_biz_gen_retry';
        if (sessionStorage.getItem(key) === '1') return false;
        sessionStorage.setItem(key, '1');

        showInlineError('Generation may have completed, but the connection was interrupted. Refreshing to check…');
        window.setTimeout(() => window.location.reload(), 900);
        return true;
    };

    form.addEventListener('submit', async (e) => {
        // dashboard-business-analysis.js will cancel and open modal if profile is missing
        if (e.defaultPrevented) return;

        e.preventDefault();

        const token = tokenInput ? tokenInput.value : '';
        const workspaceId = wsInput ? wsInput.value : '';
        if (!workspaceId) return;

        try {
            setBusy(true);
            showInlineError('');

            const res = await fetch('/Dashboard/GenerateBusinessAnalysis', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: new URLSearchParams({ workspaceId })
            });

            if (!res.ok) {
                const txt = await res.text();
                throw new Error(txt || `Generate failed (${res.status})`);
            }

            window.location.reload();
        } catch (err) {
            if (maybeRecoverFromFetchFailure(err)) return;
            showInlineError(err?.message || 'Generate failed.');
        } finally {
            setBusy(false);
        }
    });
})();
