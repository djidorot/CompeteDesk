(function () {
    "use strict";
    const qs = (sel, root) => (root || document).querySelector(sel);
    const qsa = (sel, root) => Array.from((root || document).querySelectorAll(sel));

    const getAntiForgeryToken = () => {
        const tokenForm = qs('[data-war-ai-token]');
        const input = tokenForm ? qs('input[name="__RequestVerificationToken"]', tokenForm) : null;
        return input ? input.value : null;
    };

    const postJson = async (url, payload) => {
        const token = getAntiForgeryToken();
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'RequestVerificationToken': token } : {})
            },
            body: JSON.stringify(payload || {})
        });

        const text = await res.text();
        let data = null;
        try { data = text ? JSON.parse(text) : null; } catch { data = { error: text || 'Invalid response.' }; }

        if (!res.ok) {
            const msg = (data && data.error) ? data.error : `Request failed (${res.status})`;
            throw new Error(msg);
        }
        return data;
    };

    const ensureModal = () => {
        const modalEl = qs('#warAiModal');
        if (!modalEl || !window.bootstrap) return null;
        return window.bootstrap.Modal.getOrCreateInstance(modalEl);
    };

    const setModal = (title, html) => {
        const titleEl = qs('#warAiModalTitle');
        const bodyEl = qs('#warAiModalBody');
        if (titleEl) titleEl.textContent = title || 'AI';
        if (bodyEl) bodyEl.innerHTML = html || '';
    };

    const escapeHtml = (s) => (s ?? '').toString()
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');

    const renderList = (items) => {
        if (!items || !items.length) return '<div class="cd-muted">—</div>';
        return `<ul class="mb-0">${items.map(x => `<li>${escapeHtml(x)}</li>`).join('')}</ul>`;
    };

    // ----------------------------
    // Intel: AI Brief (multi-select)
    // ----------------------------
    const intelChecks = qsa('[data-war-intel-select]');
    const briefBtn = qs('[data-war-ai-brief-btn]');
    if (intelChecks.length && briefBtn) {
        const updateBtn = () => {
            const selected = intelChecks.filter(x => x.checked).length;
            briefBtn.disabled = selected === 0;
            briefBtn.textContent = selected > 0 ? `AI Brief (${selected})` : 'AI Brief';
        };

        intelChecks.forEach(cb => cb.addEventListener('change', updateBtn));
        updateBtn();

        briefBtn.addEventListener('click', async () => {
            const ids = intelChecks.filter(x => x.checked).map(x => parseInt(x.value, 10)).filter(Number.isFinite);
            if (!ids.length) return;

            const modal = ensureModal();
            setModal('AI Intel Brief', '<div class="cd-muted">Generating brief…</div>');
            if (modal) modal.show();

            try {
                const data = await postJson('/WarRoomAi/IntelBrief', { intelIds: ids });
                if (data && data.error) throw new Error(data.error);

                const html = `
                    <div class="cd-war-ai">
                        <div class="cd-pill mb-2">Overall confidence: ${escapeHtml(data.overallConfidence1to5 ?? '—')}/5</div>
                        <h6 class="mt-0">${escapeHtml(data.title || 'Intel Brief')}</h6>
                        <div class="cd-v">${escapeHtml(data.executiveSummary || '')}</div>

                        <h6>Key signals</h6>
                        ${renderList(data.keySignals)}

                        <h6>Contradictions / conflicts</h6>
                        ${renderList(data.contradictionsOrConflicts)}

                        <h6>Missing intel questions</h6>
                        ${renderList(data.missingIntelQuestions)}

                        <h6>Recommended next moves</h6>
                        ${renderList(data.recommendedNextMoves)}
                    </div>
                `;
                setModal('AI Intel Brief', html);
            } catch (e) {
                setModal('AI Intel Brief', `<div class="alert alert-danger mb-0">${escapeHtml(e.message || 'Failed')}</div>`);
            }
        });
    }

    // ----------------------------
    // Plans: AI Red-Team
    // ----------------------------
    const redTeamBtn = qs('[data-war-ai-redteam-btn]');
    if (redTeamBtn) {
        redTeamBtn.addEventListener('click', async () => {
            const planId = parseInt(redTeamBtn.getAttribute('data-plan-id') || '0', 10);
            if (!Number.isFinite(planId) || planId <= 0) return;

            const modal = ensureModal();
            setModal('AI Red-Team', '<div class="cd-muted">Analyzing plan…</div>');
            if (modal) modal.show();

            try {
                const data = await postJson('/WarRoomAi/RedTeamPlan', { planId });
                if (data && data.error) throw new Error(data.error);

                const html = `
                    <div class="cd-war-ai">
                        <div class="cd-pill mb-2">Confidence in plan: ${escapeHtml(data.confidenceInPlan1to5 ?? '—')}/5</div>
                        <h6 class="mt-0">Summary</h6>
                        <div class="cd-v">${escapeHtml(data.summary || '')}</div>

                        <h6>Critical gaps</h6>
                        ${renderList(data.criticalGaps)}

                        <h6>Assumption risks</h6>
                        ${renderList(data.assumptionRisks)}

                        <h6>Likely opponent responses</h6>
                        ${renderList(data.likelyOpponentResponses)}

                        <h6>Recommended adjustments</h6>
                        ${renderList(data.recommendedAdjustments)}

                        <h6>Alternative approaches</h6>
                        ${renderList(data.alternativeApproaches)}

                        <h6>Verification steps</h6>
                        ${renderList(data.verificationSteps)}
                    </div>
                `;
                setModal('AI Red-Team', html);
            } catch (e) {
                setModal('AI Red-Team', `<div class="alert alert-danger mb-0">${escapeHtml(e.message || 'Failed')}</div>`);
            }
        });
    }
})();
