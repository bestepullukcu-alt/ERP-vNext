/**
 * WP-KP-UI-2 — the legacy-path wizard (K4 / D-KP-6; replaces the KP-UI-1 bind modal). Four steps:
 *  1. chain + country + language → POST bind-chain (the path's identity locks once bound — said on the step);
 *  2. map every old step onto a chain step (branch › step) → step update with `arrangement`; unmapped steps are warned;
 *  3. claim SUGGESTIONS from the step contents' claim references: each one is accepted or rejected by the user. "Next"
 *     stays closed until every suggestion is decided; only accepted ones are POSTed (nothing is bound automatically);
 *  4. summary + optional "Send for approval" (submit-review).
 * A path that is already bound (a wizard closed half-way) reopens on step 2. All traffic: /CRM/KnowledgePaths/api.
 */
(function (window, document) {
    'use strict';
    const S = window.KpStudio;
    const modalEl = document.getElementById('kpWizardModal');
    if (!S || !modalEl) return;
    const { t, esc, api } = S;
    const $ = id => document.getElementById(id);

    const ACCEPT = 'accept';
    const REJECT = 'reject';
    let state = null;

    // ---------------- step frame ----------------

    const NEXT_LABELS = { 1: 'WizardBindNext', 2: 'WizardMapNext', 3: 'WizardClaimsNext', 4: 'WizardFinish' };

    const showStep = step => {
        state.step = step;
        modalEl.querySelectorAll('[data-wizard-step]').forEach(n => n.classList.toggle('d-none', n.dataset.wizardStep !== String(step)));
        modalEl.querySelectorAll('[data-wizard-label]').forEach(n => {
            const current = n.dataset.wizardLabel === String(step);
            n.classList.toggle('bg-label-primary', current);
            n.classList.toggle('bg-label-secondary', !current);
            if (current) n.setAttribute('aria-current', 'step'); else n.removeAttribute('aria-current');
        });
        $('kpWizardNext').textContent = t(NEXT_LABELS[step]);
        $('kpWizardSubmit').classList.toggle('d-none', step !== 4 || !state.canSubmit);
        S.hideAlert($('kpWizardAlert'));
        refreshNext();
    };

    /** A suggestion is decided when it was accepted / rejected — or is already on the path (nothing to decide). */
    const isDecided = s => s.onPath || state.decisions[s.claimId] === ACCEPT || state.decisions[s.claimId] === REJECT;
    const allDecided = () => state.suggestions.every(isDecided);

    const refreshNext = () => {
        const next = $('kpWizardNext');
        if (state.busy) { next.disabled = true; return; }
        if (state.step === 1) {
            next.disabled = !($('kpWizardChain').value && state.country?.value && state.language?.value);
        } else if (state.step === 3) {
            // K4 gate: no "Next" until every suggestion has a decision.
            next.disabled = !allDecided();
            const open = state.suggestions.filter(s => !isDecided(s)).length;
            $('kpWizardUndecided').textContent = open > 0 ? t('WizardUndecided', open) : '';
        } else {
            next.disabled = false;
        }
    };

    const fail = error => { S.showAlert($('kpWizardAlert'), error); state.busy = false; refreshNext(); };

    // ---------------- step 1: chain + country + language ----------------

    const loadChainStep = async () => {
        const [chainList, countryList] = await Promise.all([S.chains(state.path.subjectId), S.countries()]);
        state.chains = chainList;
        S.fillOptions($('kpWizardChain'), chainList.map(c => ({ value: c.id, text: `${c.name} · ${t('VersionShort', c.version)}` })));
        $('kpWizardNoChain').classList.toggle('d-none', chainList.length > 0);
        $('kpWizardChain').onchange = refreshNext;
        // Fresh selects so a reopened wizard never stacks listeners.
        const country = $('kpWizardCountry').cloneNode(false); $('kpWizardCountry').replaceWith(country);
        const language = $('kpWizardLanguage').cloneNode(false); $('kpWizardLanguage').replaceWith(language);
        state.country = country;
        state.language = language;
        state.countryList = countryList;
        S.wireCountryLanguage(country, language, countryList, refreshNext);
    };

    const bind = async () => {
        await api.post(`/paths/${state.path.pathId}/bind-chain`, {
            chainTemplateId: $('kpWizardChain').value, countryCode: state.country.value, languageCode: state.language.value
        });
        const chain = state.chains.find(c => c.id === $('kpWizardChain').value);
        const country = state.countryList.find(c => c.code === state.country.value);
        state.summary.chain = chain ? `${chain.name} · ${t('VersionShort', chain.version)}` : '';
        state.summary.context = [country?.name, (country?.languageDetails || []).find(l => l.code === state.language.value)?.nativeName].filter(Boolean).join(' · ');
        state.changed = true;
    };

    // ---------------- step 2: map old steps onto chain steps ----------------

    const slotValue = (branchCode, chainStepId) => branchCode && chainStepId ? `${branchCode}|${chainStepId}` : '';

    const renderMapping = () => {
        const m = state.mapping;
        const options = `<option value="">${esc(t('WizardNoMatch'))}</option>` + m.branches.map(b =>
            `<optgroup label="${esc(b.name)}">${b.slots.map(s => `<option value="${esc(slotValue(b.code, s.chainStepId))}">${esc(`${b.name} › ${s.name}`)}</option>`).join('')}</optgroup>`).join('');
        $('kpWizardMap').innerHTML = m.steps.length === 0
            ? `<li class="list-group-item small text-muted">${esc(t('EmptySlot'))}</li>`
            : m.steps.map((s, i) => `
                <li class="list-group-item d-flex flex-wrap align-items-center gap-2">
                    <label class="flex-grow-1 min-w-0 small" for="kpWizardMap${i}">
                        <span class="fw-medium text-heading">${esc(s.title || '')}</span>
                        ${s.contentTitle && s.contentTitle !== s.title ? `<span class="text-muted d-block">${esc(s.contentTitle)}</span>` : ''}
                    </label>
                    <select class="form-select form-select-sm w-auto js-wizard-map" id="kpWizardMap${i}" data-step-id="${esc(s.stepId)}">${options}</select>
                </li>`).join('');
        m.steps.forEach((s, i) => { $(`kpWizardMap${i}`).value = slotValue(s.branchCode, s.chainStepId); });
        $('kpWizardMap').querySelectorAll('.js-wizard-map').forEach(sel => sel.addEventListener('change', renderUnmapped));
        renderUnmapped();
    };

    const renderUnmapped = () => {
        const unmapped = Array.from($('kpWizardMap').querySelectorAll('.js-wizard-map')).filter(sel => !sel.value).length;
        const warn = $('kpWizardUnmapped');
        warn.textContent = unmapped > 0 ? t('WizardUnmapped', unmapped) : '';
        warn.classList.toggle('d-none', unmapped === 0);
    };

    const loadMapping = async () => {
        state.mapping = (await api.get(`/studio/paths/${state.path.pathId}/mapping`)).data;
        renderMapping();
    };

    const saveMapping = async () => {
        const positions = {};
        let mapped = 0;
        for (const sel of $('kpWizardMap').querySelectorAll('.js-wizard-map')) {
            const step = state.mapping.steps.find(s => s.stepId === sel.dataset.stepId);
            if (!step || !sel.value) continue;
            const [branchCode, chainStepId] = sel.value.split('|');
            const position = positions[sel.value] = (positions[sel.value] ?? -1) + 1;
            mapped++;
            if (step.branchCode === branchCode && step.chainStepId === chainStepId) continue;
            await api.put(`/paths/${state.path.pathId}/steps/${step.stepId}`, Object.assign({}, step.body, { arrangement: { chainStepId, branchCode, position } }));
            state.changed = true;
        }
        state.positions = positions;
        state.summary.mapped = mapped;
        state.summary.total = state.mapping.steps.length;
    };

    // ---------------- step 3: claim suggestions (accept / reject each) ----------------

    const renderSuggestions = () => {
        const host = $('kpWizardClaims');
        if (state.suggestionsDisabled) {
            host.innerHTML = `<li class="list-group-item small text-muted">${esc(state.suggestionsDisabled)}</li>`;
            return;
        }
        host.innerHTML = state.suggestions.length === 0
            ? `<li class="list-group-item small text-muted">${esc(t('WizardNoSuggestions'))}</li>`
            : state.suggestions.map(s => {
                const decision = state.decisions[s.claimId];
                const placeable = !!(s.branchCode && s.chainStepId);
                const controls = s.onPath
                    ? `<span class="badge bg-label-secondary">${esc(t('WizardOnPath'))}</span>`
                    : `<div class="btn-group btn-group-sm" role="group" aria-label="${esc(s.code || '')}">
                           <button type="button" class="btn ${decision === ACCEPT ? 'btn-success' : 'btn-label-success'} js-wizard-decide" data-id="${esc(s.claimId)}" data-decision="${ACCEPT}" aria-pressed="${decision === ACCEPT}" ${placeable ? '' : 'disabled'}>${esc(t('WizardAccept'))}</button>
                           <button type="button" class="btn ${decision === REJECT ? 'btn-danger' : 'btn-label-danger'} js-wizard-decide" data-id="${esc(s.claimId)}" data-decision="${REJECT}" aria-pressed="${decision === REJECT}">${esc(t('WizardReject'))}</button>
                       </div>`;
                return `
                    <li class="list-group-item d-flex flex-wrap align-items-start gap-2">
                        <div class="flex-grow-1 min-w-0 small">
                            <div class="fw-medium text-heading">${esc([s.code, s.name].filter(Boolean).join(' · '))}</div>
                            ${s.text ? `<div class="text-break">“${esc(s.text)}”</div>` : ''}
                            ${s.qualifier ? `<div class="text-muted text-break">${esc(s.qualifier)}</div>` : ''}
                            <div class="${s.usable ? 'text-muted' : 'kp-reason'}">${esc([s.usable ? null : s.reasonLabel, s.versionLabel].filter(Boolean).join(' · '))}</div>
                            <div class="text-muted">${esc(t('WizardFromStep', s.fromStep || ''))}</div>
                            ${placeable || s.onPath ? '' : `<div class="kp-reason">${esc(t('WizardNeedsMapping'))}</div>`}
                        </div>
                        ${controls}
                    </li>`;
            }).join('');
        refreshNext();
    };

    const loadSuggestions = async () => {
        const data = (await api.get(`/studio/paths/${state.path.pathId}/claim-suggestions`)).data;
        state.suggestionsDisabled = data.disabled ? (data.reasonLabel || t('ClaimOptionsUnavailable')) : null;
        state.suggestions = data.disabled ? [] : (data.suggestions || []);
        state.decisions = {};
        renderSuggestions();
    };

    const applyDecisions = async () => {
        let accepted = 0;
        for (const s of state.suggestions.filter(x => !x.onPath && state.decisions[x.claimId] === ACCEPT)) {
            const key = slotValue(s.branchCode, s.chainStepId);
            const position = state.positions[key] = (state.positions[key] ?? -1) + 1;
            await api.post(`/paths/${state.path.pathId}/claims`, { claimId: s.claimId, arrangement: { chainStepId: s.chainStepId, branchCode: s.branchCode, position } });
            accepted++;
            state.changed = true;
        }
        state.summary.accepted = accepted;
        state.summary.rejected = state.suggestions.filter(x => state.decisions[x.claimId] === REJECT).length;
    };

    // ---------------- step 4: summary ----------------

    const renderSummary = () => {
        const sm = state.summary;
        const row = (label, value) => `<dt class="col-sm-5">${esc(label)}</dt><dd class="col-sm-7">${esc(value ?? '—')}</dd>`;
        $('kpWizardSummary').innerHTML = [
            row(t('FieldChain'), sm.chain || t('WizardChainKept')),
            row(t('WizardSummaryContext'), sm.context || t('WizardChainKept')),
            row(t('WizardSummaryMapped'), t('WizardMappedCount', sm.mapped ?? 0, sm.total ?? 0)),
            row(t('WizardSummaryClaims'), t('WizardClaimsCount', sm.accepted ?? 0, sm.rejected ?? 0))
        ].join('');
    };

    // ---------------- flow ----------------

    const next = async () => {
        state.busy = true;
        refreshNext();
        S.hideAlert($('kpWizardAlert'));
        try {
            if (state.step === 1) { await bind(); await loadMapping(); state.busy = false; showStep(2); return; }
            if (state.step === 2) { await saveMapping(); await loadSuggestions(); state.busy = false; showStep(3); return; }
            if (state.step === 3) {
                if (!allDecided()) { state.busy = false; refreshNext(); return; }
                await applyDecisions(); renderSummary(); state.busy = false; showStep(4); return;
            }
            close();
        } catch (error) {
            fail(error);
        }
    };

    const submit = async () => {
        state.busy = true;
        refreshNext();
        try {
            await api.post(`/paths/${state.path.pathId}/submit-review`);
            window.showToast?.(t('SubmitDone'), 'success');
            state.changed = true;
            close();
        } catch (error) {
            fail(error);
        }
    };

    const close = () => window.bootstrap?.Modal.getOrCreateInstance(modalEl).hide();

    const open = async (path, onDone) => {
        // The wizard is offered only to path managers (bind needs manage), so "Send for approval" is theirs too; CRM decides.
        state = { path, onDone, step: 1, busy: false, decisions: {}, suggestions: [], summary: {}, positions: {}, changed: false, canSubmit: true };
        $('kpWizardPath').textContent = [path.code, path.name].filter(Boolean).join(' · ');
        window.bootstrap?.Modal.getOrCreateInstance(modalEl).show();
        try {
            const mapping = (await api.get(`/studio/paths/${path.pathId}/mapping`)).data;
            if (mapping.bound) {
                // Already bound (a wizard closed half-way): continue with the mapping.
                state.mapping = mapping;
                renderMapping();
                showStep(2);
            } else {
                showStep(1);
                await loadChainStep();
                refreshNext();
            }
        } catch (error) {
            showStep(1);
            fail(error);
        }
    };

    $('kpWizardNext').addEventListener('click', () => void next());
    $('kpWizardSubmit').addEventListener('click', () => void submit());
    $('kpWizardClaims').addEventListener('click', event => {
        const button = event.target.closest('.js-wizard-decide');
        if (!button || button.disabled) return;
        state.decisions[button.dataset.id] = button.dataset.decision;
        renderSuggestions();
        $('kpWizardClaims').querySelector(`.js-wizard-decide[data-id="${CSS.escape(button.dataset.id)}"][data-decision="${button.dataset.decision}"]`)?.focus();
    });
    modalEl.addEventListener('hidden.bs.modal', () => { if (state?.changed) state.onDone?.(); });

    window.KpLegacyWizard = Object.freeze({ open, allDecided: () => !!state && allDecided() });
})(window, document);
