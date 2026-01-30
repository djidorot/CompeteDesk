(() => {
    "use strict";

    const root = document.querySelector('[data-cd-biz-root]');
    if (!root) return;

    const needsProfile = (root.getAttribute('data-needs-profile') || '').toLowerCase() === 'true';

    const modalEl = document.getElementById('cdBizModal');
    const formEl = document.getElementById('cdBizProfileForm');
    const saveBtn = document.querySelector('[data-cd-biz-save]');
    const savingBtn = document.querySelector('[data-cd-biz-saving]');
    const errorEl = document.querySelector('[data-cd-biz-error]');
    const editBtn = document.querySelector('[data-cd-biz-edit]');
    const genBtn = document.querySelector('[data-cd-biz-generate]');

    if (!modalEl || !formEl || !saveBtn || !savingBtn) return;

    const bsModal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });

    const antiForgery = () => {
        const tokenInput = formEl.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    };

    const showError = (msg) => {
        if (!errorEl) return;
        if (!msg) {
            errorEl.classList.add('d-none');
            errorEl.textContent = '';
            return;
        }
        errorEl.textContent = msg;
        errorEl.classList.remove('d-none');
    };

    const setSaving = (isSaving) => {
        saveBtn.classList.toggle('d-none', isSaving);
        savingBtn.classList.toggle('d-none', !isSaving);
    };

    const openModal = () => {
        showError('');
        bsModal.show();
    };

    // Auto-open if profile is missing
    if (needsProfile) {
        openModal();
    }

    // Edit button
    if (editBtn) {
        editBtn.addEventListener('click', (e) => {
            e.preventDefault();
            openModal();
        });
    }

    // If user clicks Generate but profile missing, block and open modal.
    if (genBtn) {
        const genForm = genBtn.closest('form');
        if (genForm) {
            genForm.addEventListener('submit', (e) => {
                const currentNeeds = (root.getAttribute('data-needs-profile') || '').toLowerCase() === 'true';
                if (currentNeeds) {
                    e.preventDefault();
                    openModal();
                }
            });
        }
    }

    // Save profile via fetch then trigger Generate if user originally clicked Generate.
    saveBtn.addEventListener('click', async () => {
        showError('');

        const fd = new FormData(formEl);
        const workspaceId = fd.get('workspaceId');
        const businessType = (fd.get('businessType') || '').toString().trim();
        const country = (fd.get('country') || '').toString().trim();

        if (!businessType || !country) {
            showError('Business type and country are required.');
            return;
        }

        setSaving(true);
        try {
            const res = await fetch('/Dashboard/SetBusinessProfile', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': antiForgery()
                },
                body: new URLSearchParams({
                    workspaceId: workspaceId,
                    businessType: businessType,
                    country: country
                })
            });

            if (!res.ok) {
                const txt = await res.text();
                throw new Error(txt || `Save failed (${res.status})`);
            }

            // Mark profile as complete so Generate form can submit.
            root.setAttribute('data-needs-profile', 'false');

            bsModal.hide();

            // After saving, automatically generate analysis if none exists yet.
            // We do this by posting to Generate endpoint and then reloading.
            const genTokenInput = document.querySelector('form[action*="GenerateBusinessAnalysis"] input[name="__RequestVerificationToken"]');
            const genToken = genTokenInput ? genTokenInput.value : antiForgery();

            const genRes = await fetch('/Dashboard/GenerateBusinessAnalysis', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': genToken
                },
                body: new URLSearchParams({ workspaceId: workspaceId })
            });

            if (!genRes.ok) {
                // If OpenAI isn't configured, show a friendly error and don't reload.
                const txt = await genRes.text();
                showError('Saved, but could not generate analysis. Configure OpenAI (OpenAI:ApiKey) then click Generate.');
                bsModal.show();
                return;
            }

            window.location.reload();
        } catch (err) {
            showError(err?.message || 'Save failed.');
        } finally {
            setSaving(false);
        }
    });
})();
