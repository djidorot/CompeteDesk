(() => {
    "use strict";

    const form = document.querySelector('form[action="/Dashboard/GenerateBusinessAnalysis"]');
    if (!form) return;

    const btn = form.querySelector('button[type="submit"]');
    const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    const wsInput = form.querySelector('input[name="workspaceId"]');
    const reportHost = document.querySelector('[data-cd-biz-report]');

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

    const refreshReport = async (workspaceId) => {
        if (!reportHost || !workspaceId) return;
        const res = await fetch(`/BusinessAnalysis/LatestReport?workspaceId=${encodeURIComponent(workspaceId)}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!res.ok) throw new Error(`Could not refresh analysis (${res.status})`);
        reportHost.innerHTML = await res.text();
    };

    form.addEventListener('submit', async (e) => {
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

            await refreshReport(workspaceId);
        } catch (err) {
            showInlineError(err?.message || 'Generate failed.');
        } finally {
            setBusy(false);
        }
    });
})();
