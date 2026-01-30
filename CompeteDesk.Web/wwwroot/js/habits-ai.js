// FILE: CompeteDesk/wwwroot/js/habits-ai.js
(() => {
    "use strict";

    const qs = (sel, root = document) => root.querySelector(sel);
    const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    const openBtn = qs('[data-habits-ai-open]');
    const modalEl = qs('#habitsAiModal');
    if (!openBtn || !modalEl) return;

    // Bootstrap modal is available in this app; fail gracefully if not.
    const modal = window.bootstrap?.Modal ? new window.bootstrap.Modal(modalEl) : null;

    const form = qs('[data-habits-ai-form]', modalEl);
    const statusEl = qs('[data-habits-ai-status]', modalEl);
    const resultsWrap = qs('[data-habits-ai-results]', modalEl);
    const listEl = qs('[data-habits-ai-list]', modalEl);
    const createBtn = qs('[data-habits-ai-create]', modalEl);
    const wsSel = qs('#aiWorkspace', modalEl);
    const stSel = qs('#aiStrategy', modalEl);
    const goalInput = qs('#aiGoal', modalEl);

    const tokenInput = qs('input[name="__RequestVerificationToken"]', form);
    const getToken = () => tokenInput?.value || "";

    let lastSuggestions = [];

    const setStatus = (msg, type = "") => {
        if (!statusEl) return;
        statusEl.className = `habits-ai-status ${type}`.trim();
        statusEl.textContent = msg || "";
    };

    const setBusy = (busy) => {
        const genBtn = qs('[data-habits-ai-generate]', form);
        if (genBtn) genBtn.disabled = !!busy;
        if (createBtn) createBtn.disabled = !!busy || qsa('input[type="checkbox"]:checked', listEl).length === 0;
        if (wsSel) wsSel.disabled = !!busy;
        if (stSel) stSel.disabled = !!busy;
        if (goalInput) goalInput.disabled = !!busy;
    };

    const renderSuggestions = (suggestions) => {
        lastSuggestions = Array.isArray(suggestions) ? suggestions : [];
        if (!listEl) return;

        listEl.innerHTML = "";

        if (lastSuggestions.length === 0) {
            resultsWrap.hidden = true;
            return;
        }

        resultsWrap.hidden = false;

        lastSuggestions.forEach((s, idx) => {
            const title = (s?.title || "").trim();
            const frequency = (s?.frequency || "Daily").trim();
            const target = Number.isFinite(s?.targetCount) ? s.targetCount : 1;
            const desc = (s?.description || "").trim();
            const cue = (s?.cue || "").trim();
            const routine = (s?.routine || "").trim();
            const reward = (s?.reward || "").trim();
            const rationale = (s?.rationale || "").trim();

            const card = document.createElement('div');
            card.className = 'habits-ai-item';

            const safe = (t) => (t || '').replace(/</g, '&lt;').replace(/>/g, '&gt;');

            card.innerHTML = `
                <label class="habits-ai-item__row">
                    <input type="checkbox" class="form-check-input" data-ai-pick value="${idx}" checked>
                    <div class="habits-ai-item__body">
                        <div class="habits-ai-item__top">
                            <div class="habits-ai-item__title">${safe(title || '(Untitled habit)')}</div>
                            <div class="habits-ai-item__meta">
                                <span class="habits-ai-pill">${safe(frequency)}</span>
                                <span class="cd-muted">Target: <strong>${safe(String(target || 1))}</strong></span>
                            </div>
                        </div>
                        ${desc ? `<div class="cd-muted">${safe(desc)}</div>` : ''}
                        ${(cue || routine || reward || rationale) ? `
                            <div class="habits-ai-item__details">
                                ${cue ? `<div><strong>Cue:</strong> ${safe(cue)}</div>` : ''}
                                ${routine ? `<div><strong>Routine:</strong> ${safe(routine)}</div>` : ''}
                                ${reward ? `<div><strong>Reward:</strong> ${safe(reward)}</div>` : ''}
                                ${rationale ? `<div><strong>Why:</strong> ${safe(rationale)}</div>` : ''}
                            </div>
                        ` : ''}
                    </div>
                </label>
            `;

            listEl.appendChild(card);
        });

        const syncCreateEnabled = () => {
            const picked = qsa('input[data-ai-pick]:checked', listEl).length;
            if (createBtn) createBtn.disabled = picked === 0;
        };

        qsa('input[data-ai-pick]', listEl).forEach(cb => {
            cb.addEventListener('change', syncCreateEnabled);
        });

        syncCreateEnabled();
    };

    openBtn.addEventListener('click', (e) => {
        e.preventDefault();
        setStatus('');
        renderSuggestions([]);
        if (modal) modal.show();
        else modalEl.classList.add('show');
    });

    form?.addEventListener('submit', async (e) => {
        e.preventDefault();
        setStatus('Generating suggestions…');
        setBusy(true);

        try
        {
            const fd = new FormData(form);
            const res = await fetch('/Habits/AiSuggest', {
                method: 'POST',
                headers: { 'RequestVerificationToken': getToken() },
                body: fd
            });

            const json = await res.json().catch(() => null);
            if (!res.ok || !json || json.ok !== true)
            {
                const err = json?.error || `Request failed (${res.status})`;
                setStatus(err, 'is-error');
                renderSuggestions([]);
                setBusy(false);
                return;
            }

            const suggestions = json?.data?.suggestions || [];
            setStatus(`Got ${suggestions.length} suggestion(s).`, 'is-ok');
            renderSuggestions(suggestions);
        }
        catch (err)
        {
            setStatus('Failed to generate suggestions.', 'is-error');
            renderSuggestions([]);
        }
        finally
        {
            setBusy(false);
        }
    });

    createBtn?.addEventListener('click', async () => {
        const workspaceId = parseInt(wsSel?.value || '0', 10);
        const strategyIdRaw = (stSel?.value || '').trim();
        const strategyId = strategyIdRaw ? parseInt(strategyIdRaw, 10) : null;

        const picks = qsa('input[data-ai-pick]:checked', listEl)
            .map(cb => parseInt(cb.value, 10))
            .filter(n => Number.isFinite(n));

        const suggestions = picks.map(i => lastSuggestions[i]).filter(Boolean);

        if (!workspaceId || suggestions.length === 0)
            return;

        setStatus('Creating habits…');
        setBusy(true);

        try
        {
            const res = await fetch('/Habits/AiCreate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getToken()
                },
                body: JSON.stringify({ workspaceId, strategyId, suggestions })
            });

            const json = await res.json().catch(() => null);
            if (!res.ok || !json || json.ok !== true)
            {
                const err = json?.error || `Request failed (${res.status})`;
                setStatus(err, 'is-error');
                setBusy(false);
                return;
            }

            const created = json.created ?? 0;
            setStatus(`Created ${created} habit(s).`, 'is-ok');

            // Reload back to habits list filtered by workspace.
            const url = new URL(window.location.href);
            url.searchParams.set('workspaceId', String(workspaceId));
            if (strategyId) url.searchParams.set('strategyId', String(strategyId));
            window.location.href = url.toString();
        }
        catch
        {
            setStatus('Failed to create habits.', 'is-error');
            setBusy(false);
        }
    });
})();
