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

    form.addEventListener('submit', async (e) => {
        // dashboard-business-analysis.js will cancel and open modal if profile is missing
        if (e.defaultPrevented) return;

        e.preventDefault();

        const token = tokenInput ? tokenInput.value : '';
        const workspaceId = wsInput ? wsInput.value : '';
        if (!workspaceId) return;

        try {
            setBusy(true);

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
            alert(err?.message || 'Generate failed.');
        } finally {
            setBusy(false);
        }
    });
})();
