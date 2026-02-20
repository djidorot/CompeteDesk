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

    // Guard: if Bootstrap JS isn't available, don't break the page.
    if (!window.bootstrap || !window.bootstrap.Modal) return;

    const bsModal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });

    // Business Profile updates should immediately refresh the analysis.
    // Always save + generate from this modal.
    const generateAfterSave = true;

    const setSaveButtonMode = () => {
        if (saveBtn) saveBtn.textContent = 'Save + Generate';
    };

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

    const setSavingText = (text) => {
        if (!savingBtn) return;
        savingBtn.textContent = text || 'Saving…';
    };

    const openModal = () => {
        showError('');
        bsModal.show();
    };

    // Auto-open if profile is missing
    if (needsProfile) {
        setSaveButtonMode();
        openModal();
    }

    // Edit button
    if (editBtn) {
        editBtn.addEventListener('click', (e) => {
            e.preventDefault();
            setSaveButtonMode();
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
                    setSaveButtonMode();
                    openModal();
                }
            });
        }
    }

    // Save profile via fetch, optionally triggering Generate.
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
        setSavingText('Saving…');
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

            if (generateAfterSave) {
                // Keep the modal open and show progress so the user knows we're working.
                setSavingText('Generating…');

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
                    const txt = await genRes.text();
                    // Friendly default, append server text if present.
                    showError('Saved, but could not generate analysis. Configure OpenAI (OpenAI:ApiKey) then click Generate.' + (txt ? `\n${txt}` : ''));
                    bsModal.show();
                    return;
                }
            }

            bsModal.hide();
            window.location.reload();
        } catch (err) {
            showError(err?.message || 'Save failed.');
        } finally {
            setSavingText('Saving…');
            setSaving(false);
        }
    });
})();
