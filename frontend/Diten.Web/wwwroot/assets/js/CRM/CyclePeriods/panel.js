/**
 * WP-CYC-UI-1 — the Cycle Period create / edit panel (right-side offcanvas, mockup screen 02).
 *
 * The panel decides NOTHING about rules. It asks the server:
 *  - /api/panel/state      which fields the period's status leaves editable, which lifecycle actions are offered
 *                          (CyclePeriodScreenRules.FieldStates / Actions);
 *  - /api/panel/check      the live warnings: end after start (E4), sequence taken (E5), active overlap in the same
 *                          scope (linked), other-level overlap (info) — plus the first free sequence;
 *  - /api/code-suggestion  the suggested code (K-2) — a suggestion only, read-only once saved;
 *  - /api/working-days     the live day + working-day count with the calendar's status.
 * The runtime enforces every rule again on save; its refusal is shown verbatim.
 *
 * Exactly ONE scope reference is posted: the hidden blocks are cleared, because a hidden-but-populated field would be
 * refused as an ambiguous scope. Esc closes the panel and focus stays inside it (Bootstrap offcanvas); the scope
 * radios move with the arrow keys (native radio group).
 */
(function (window, document) {
    'use strict';

    const S = window.CyclePeriodsShared;
    const panelEl = document.getElementById('cyclePeriodPanel');
    if (!S || !panelEl) return;

    const L = S.L;
    const $ = id => document.getElementById(id);
    const form = $('cyclePeriodPanelForm');
    const el = {
        title: $('cyclePeriodPanelTitle'), status: $('panelStatusBadge'), errors: $('panelErrors'),
        activeNotice: $('panelActiveNotice'), closedNotice: $('panelClosedNotice'), closeAndReopen: $('btnCloseAndReopen'),
        id: $('pCyclePeriodId'), version: $('pExpectedVersion'),
        year: $('pYear'), sequence: $('pSequence'), sequenceHint: $('pSequenceHint'),
        name: $('pName'), nameHint: $('pNameHint'), nameSuggestion: $('pNameSuggestion'), nameUse: $('pNameUseSuggestion'),
        code: $('pCode'), codeHint: $('pCodeHint'), description: $('pDescription'),
        templates: $('pDateTemplates'), month: $('pMonthTemplate'), start: $('pStart'), end: $('pEnd'), summary: $('pDateSummary'),
        scopeLocked: $('pScopeLockedHint'), country: $('pCountry'), legalEntity: $('pLegalEntity'),
        buCountry: $('pBuCountry'), businessUnit: $('pBusinessUnit'), buHint: $('pBusinessUnitHint'),
        checks: $('panelChecks'), save: $('btnPanelSave')
    };
    const offcanvas = () => window.bootstrap?.Offcanvas.getOrCreateInstance(panelEl);

    let state = { isNew: true, period: null, fields: null, actions: null };
    let touched = { code: false, sequence: false, name: false };
    let blocking = false;
    let savedHandlers = [];
    const debounce = (fn, ms) => { let t = null; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

    // ── scope ────────────────────────────────────────────────────────────────────────────────────────────────
    const scopeType = () => form.querySelector('input[name="pScopeType"]:checked')?.value || 'tenant';
    const setScopeType = value => {
        const radio = form.querySelector(`input[name="pScopeType"][value="${CSS.escape(value || 'tenant')}"]`);
        if (radio) radio.checked = true;
        applyScope();
    };
    /** Shows the one reference block the level needs and CLEARS the others (the browser would post them). */
    const applyScope = () => {
        const current = scopeType();
        form.querySelectorAll('.p-scope-ref').forEach(block => {
            const active = block.dataset.scopeRef === current;
            block.classList.toggle('d-none', !active);
            if (!active) block.querySelectorAll('select, input').forEach(f => { f.value = ''; });
        });
    };
    const scopeRefParams = () => {
        const t = scopeType();
        return {
            scopeType: t,
            countryScope: t === 'country' ? (el.country.value || null) : null,
            legalEntityId: t === 'legal-entity' ? (el.legalEntity.value || null) : null,
            businessUnitId: t === 'business-unit' ? (el.businessUnit.value || null) : null,
            businessUnitCountryContext: t === 'business-unit' ? (el.buCountry.value || null) : null
        };
    };
    /** The working calendar's country for the live count: the period's own country, or a unit's country context. */
    const calendarCountry = () => {
        const t = scopeType();
        return t === 'country' ? el.country.value : t === 'business-unit' ? el.buCountry.value : '';
    };

    // ── field states (server-decided) ────────────────────────────────────────────────────────────────────────
    const applyFieldStates = () => {
        const f = state.fields || {};
        const lock = (selector, editable) => form.querySelectorAll(selector).forEach(x => {
            if (x.tagName === 'INPUT' && x.type !== 'radio' || x.tagName === 'TEXTAREA') {
                x.readOnly = !editable;
                x.setAttribute('aria-readonly', String(!editable));
            } else {
                x.disabled = !editable;
            }
        });
        lock('[data-field="code"]', !!f.code);
        lock('[data-field="name"]', !!f.name);
        lock('[data-field="description"]', !!f.description);
        lock('[data-field="year"]', !!f.year);
        lock('[data-field="sequence"]', !!f.sequence);
        lock('[data-field="dates"]', !!f.dates);
        lock('[data-field="scope"]', !!f.scope);
        el.scopeLocked.classList.toggle('d-none', !!f.scope || state.isNew);
        el.activeNotice.classList.toggle('d-none', !f.offerCloseAndReopen);
        el.closeAndReopen.classList.toggle('d-none', !(state.actions && state.actions.close));
        el.closedNotice.classList.toggle('d-none', !f.readOnly);
        el.save.classList.toggle('d-none', !!f.readOnly);
        el.codeHint.textContent = state.isNew ? (L().CodeSuggestedHint || '') : (L().CodeImmutableHint || '');
    };

    // ── suggestions (new periods only) ───────────────────────────────────────────────────────────────────────
    const nameSuggestion = () => {
        const y = el.year.value, n = el.sequence.value;
        return y && n ? String(L().NameSuggestionPattern || '{0} · {1}').replace('{0}', y).replace('{1}', n) : '';
    };
    const refreshNameHint = () => {
        const suggestion = nameSuggestion();
        if (!touched.name && state.isNew) {
            el.name.value = suggestion;
            el.nameHint.classList.add('d-none');
            return;
        }
        const show = state.isNew && !!suggestion && el.name.value !== suggestion;
        el.nameHint.classList.toggle('d-none', !show);
        el.nameSuggestion.textContent = show ? `${L().Suggestion || ''}: ${suggestion} ·` : '';
    };
    const refreshCodeSuggestion = debounce(async () => {
        if (!state.isNew || !el.year.value) return;
        const p = scopeRefParams();
        const q = new URLSearchParams({ scopeType: p.scopeType, year: el.year.value });
        if (p.countryScope) q.set('countryScope', p.countryScope);
        if (p.legalEntityId) q.set('legalEntityId', p.legalEntityId);
        if (p.businessUnitId) q.set('businessUnitId', p.businessUnitId);
        if (p.scopeType !== 'tenant' && !p.countryScope && !p.legalEntityId && !p.businessUnitId) return;
        try {
            const s = await S.getJson(`/code-suggestion?${q.toString()}`);
            if (!s) return;
            if (!touched.code) el.code.value = s.suggestedCode || '';
            if (!touched.sequence && s.nextSequenceInYear) el.sequence.value = s.nextSequenceInYear;
            refreshNameHint();
            runChecks();
        } catch (e) { /* the suggestion is optional: the author can always type a code */ }
    }, 250);

    // ── dates ────────────────────────────────────────────────────────────────────────────────────────────────
    const templateRange = (key, year) => {
        const r = { q1: [1, 3], q2: [4, 6], q3: [7, 9], q4: [10, 12], h1: [1, 6], h2: [7, 12] }[key];
        if (!r || !year) return null;
        const start = new Date(Date.UTC(year, r[0] - 1, 1));
        const end = new Date(Date.UTC(year, r[1], 0));
        return [S.isoDay(start), S.isoDay(end)];
    };
    const fillMonthTemplates = () => {
        const year = Number(el.year.value) || new Date().getUTCFullYear();
        const head = `<option value="">${S.esc(L().TemplateMonth || '')}</option>`;
        el.month.innerHTML = head + Array.from({ length: 12 }, (_, i) =>
            `<option value="${i + 1}">${S.esc(S.monthName(year, i + 1))}</option>`).join('');
    };
    const markTemplate = () => {
        const year = Number(el.year.value);
        el.templates.querySelectorAll('[data-template]').forEach(btn => {
            const r = templateRange(btn.dataset.template, year);
            const on = !!r && r[0] === el.start.value && r[1] === el.end.value;
            btn.classList.toggle('btn-primary', on);
            btn.classList.toggle('btn-outline-secondary', !on);
            btn.setAttribute('aria-pressed', String(on));
        });
    };
    const refreshWorkingDays = debounce(async () => {
        const from = el.start.value, to = el.end.value;
        el.summary.innerHTML = '';
        if (!from || !to || to <= from) return;
        const q = new URLSearchParams({ from, to });
        const country = calendarCountry();
        if (country) q.set('country', country);
        try {
            const r = await S.getJson(`/working-days?${q.toString()}`);
            const calendar = {
                resolved: [L().CalendarResolved, 'success'],
                no_country: [L().CalendarNoCountry, 'secondary'],
                calendar_forbidden: [L().CalendarForbidden, 'warning'],
                calendar_unresolved: [L().CalendarUnresolved, 'warning']
            }[r.resolution] || [L().CalendarUnresolved, 'warning'];
            el.summary.innerHTML =
                `<span><strong>${S.esc(S.number(r.days))}</strong> ${S.esc(L().DaysUnit || '')}</span>`
                + (r.workingDays !== null && r.workingDays !== undefined
                    ? `<span class="text-muted">|</span><span><strong>${S.esc(S.number(r.workingDays))}</strong> ${S.esc(L().WorkingDaysUnit || '')}</span>`
                    : '')
                + ` ${S.badge((r.country ? r.country + ' · ' : '') + (calendar[0] || ''), calendar[1])}`;
        } catch (e) {
            el.summary.innerHTML = S.badge(L().CalendarUnresolved || '', 'warning');
        }
    }, 300);

    // ── live checks ──────────────────────────────────────────────────────────────────────────────────────────
    const warningText = code => ({
        end_not_after_start: L().CheckEndNotAfterStart,
        sequence_taken: L().CheckSequenceTaken,
        active_overlap: L().CheckActiveOverlap,
        other_level_overlap: L().CheckOtherLevelOverlap
    }[code] || code);
    const toneClass = tone => tone === 'error' ? 'danger' : tone === 'warning' ? 'warning' : 'info';
    const periodLink = p => `<a href="/CRM/CyclePeriods/Details/${encodeURIComponent(p.cyclePeriodId)}">${S.esc(p.cycleCode)}</a>`
        + ` <span class="text-muted">(${S.esc(S.day(p.startDate))} – ${S.esc(S.day(p.endDate))})</span>`;
    const renderChecks = result => {
        const warnings = result?.warnings || [];
        blocking = warnings.some(w => w.tone === 'error');
        if (result?.nextSequence && state.fields?.sequence) {
            el.sequenceHint.textContent = String(L().SequenceSuggestion || '{0}').replace('{0}', S.number(result.nextSequence));
        } else {
            el.sequenceHint.textContent = '';
        }
        if (!warnings.length) {
            el.checks.innerHTML = `<div class="alert alert-success py-2 mb-0 small"><i class="icon-base bx bx-check me-1"></i>${S.esc(L().CheckClear || '')}</div>`;
            return;
        }
        el.checks.innerHTML = warnings.map(w =>
            `<div class="alert alert-${toneClass(w.tone)} py-2 mb-0 small" ${w.tone === 'error' ? 'role="alert"' : ''}>`
            + `<div class="fw-medium">${S.esc(warningText(w.code))}</div>`
            + (w.periods?.length ? `<div class="mt-1 d-flex flex-column gap-1">${w.periods.map(periodLink).join('')}</div>` : '')
            + '</div>').join('');
    };
    const runChecks = debounce(async () => {
        if (state.fields?.readOnly) { el.checks.innerHTML = ''; blocking = false; return; }
        const p = scopeRefParams();
        // E4 is decided locally too, so the message appears before any round trip.
        if (el.start.value && el.end.value && el.end.value <= el.start.value) {
            renderChecks({ warnings: [{ code: 'end_not_after_start', tone: 'error', periods: [] }] });
        }
        try {
            renderChecks(await S.sendJson('POST', '/panel/check', {
                cyclePeriodId: el.id.value || null,
                year: el.year.value ? Number(el.year.value) : null,
                sequenceInYear: el.sequence.value ? Number(el.sequence.value) : null,
                startDate: el.start.value || null,
                endDate: el.end.value || null,
                scopeType: p.scopeType,
                countryScope: p.countryScope,
                legalEntityId: p.legalEntityId,
                businessUnitId: p.businessUnitId
            }));
        } catch (e) { /* checks are advisory; the save still meets every rule in the runtime */ }
    }, 300);

    // ── business-unit list (country first) ──────────────────────────────────────────────────────────────────
    const refreshBusinessUnits = async () => {
        const country = el.buCountry.value;
        const previous = el.businessUnit.value;
        const q = new URLSearchParams();
        if (country) q.set('country', country);
        if (el.start.value) q.set('startDate', el.start.value);
        if (el.end.value) q.set('endDate', el.end.value);
        try {
            const data = await S.getJson(`/scope-options?${q.toString()}`);
            const options = Array.isArray(data?.businessUnits) ? data.businessUnits : [];
            const keeps = previous && options.some(o => o.value === previous);
            el.businessUnit.innerHTML = `<option value="">${S.esc(L().SelectPlaceholder || '')}</option>`
                + (previous && !keeps ? `<option value="${S.esc(previous)}">${S.esc(previous)}</option>` : '')
                + options.map(o => `<option value="${S.esc(o.value)}" title="${S.esc(o.hint || '')}">${S.esc(o.label)}</option>`).join('');
            el.businessUnit.value = previous || '';
            el.buHint.textContent = !data?.businessUnitReady ? (L().BusinessUnitNoPlan || '')
                : data.businessUnitFromTerritory ? (L().BusinessUnitFromTerritory || '') : (L().BusinessUnitFromVocabulary || '');
        } catch (e) { /* a failed lookup leaves the list as it was: emptying it would read as "there are none" */ }
    };

    // ── open ─────────────────────────────────────────────────────────────────────────────────────────────────
    const reset = () => {
        form.reset();
        el.errors.classList.add('d-none');
        el.errors.textContent = '';
        el.checks.innerHTML = '';
        el.summary.innerHTML = '';
        el.sequenceHint.textContent = '';
        touched = { code: false, sequence: false, name: false };
        blocking = false;
    };
    const setHeader = () => {
        const status = state.period?.cycleStatus || 'draft';
        el.title.textContent = state.isNew
            ? (L().FormTitleCreate || '')
            : `${L().FormTitleEdit || ''} · ${state.period.cycleCode}`;
        el.status.className = `badge bg-label-${S.statusTone(status)} mt-1`;
        el.status.textContent = S.statusLabel(status);
        el.save.textContent = state.isNew ? (L().SaveAsDraft || '') : (L().Save || '');
    };
    const afterOpen = () => {
        applyFieldStates();
        fillMonthTemplates();
        markTemplate();
        refreshNameHint();
        refreshWorkingDays();
        runChecks();
        offcanvas()?.show();
    };

    /** Opens the panel for a NEW period. <prefill> may carry scopeType / country / legalEntityId / businessUnitId /
     *  businessUnitCountryContext / year (used by "close and open a new period"). */
    const openCreate = async prefill => {
        reset();
        try {
            const s = await S.getJson('/panel/state');
            state = { isNew: true, period: null, fields: s.fields, actions: s.actions };
        } catch (e) { window.showToast?.(e.message || L().ErrorOccurred, 'error'); return; }
        const p = prefill || {};
        el.id.value = '';
        el.version.value = '';
        el.year.value = p.year || new Date().getUTCFullYear();
        setScopeType(p.scopeType || 'tenant');
        if (p.countryScope) el.country.value = p.countryScope;
        if (p.legalEntityId) el.legalEntity.value = p.legalEntityId;
        if (p.businessUnitCountryContext) el.buCountry.value = p.businessUnitCountryContext;
        if (p.businessUnitId) {
            if (![...el.businessUnit.options].some(o => o.value === p.businessUnitId)) {
                el.businessUnit.insertAdjacentHTML('beforeend', `<option value="${S.esc(p.businessUnitId)}">${S.esc(p.businessUnitId)}</option>`);
            }
            el.businessUnit.value = p.businessUnitId;
        }
        setHeader();
        refreshCodeSuggestion();
        afterOpen();
    };

    const openEdit = async id => {
        reset();
        try {
            const s = await S.getJson(`/panel/state?cyclePeriodId=${encodeURIComponent(id)}`);
            state = { isNew: false, period: s.period, fields: s.fields, actions: s.actions };
        } catch (e) { window.showToast?.(e.message || L().ErrorOccurred, 'error'); return; }
        const p = state.period;
        el.id.value = p.cyclePeriodId;
        el.version.value = p.version;
        el.code.value = p.cycleCode || '';
        el.name.value = p.cycleName || '';
        el.year.value = p.year;
        el.sequence.value = p.sequenceInYear;
        el.description.value = p.description || '';
        el.start.value = String(p.startDate || '').slice(0, 10);
        el.end.value = String(p.endDate || '').slice(0, 10);
        touched = { code: true, sequence: true, name: true };
        setScopeType(p.scopeType || 'tenant');
        el.country.value = p.countryScope || '';
        el.legalEntity.value = p.legalEntityId || '';
        el.buCountry.value = p.businessUnitCountryContext || '';
        if (p.businessUnitId && ![...el.businessUnit.options].some(o => o.value === p.businessUnitId)) {
            el.businessUnit.insertAdjacentHTML('beforeend', `<option value="${S.esc(p.businessUnitId)}">${S.esc(p.businessUnitId)}</option>`);
        }
        el.businessUnit.value = p.businessUnitId || '';
        setHeader();
        afterOpen();
    };

    // ── save ─────────────────────────────────────────────────────────────────────────────────────────────────
    const showErrors = message => {
        el.errors.textContent = message;
        el.errors.classList.remove('d-none');
        el.errors.scrollIntoView({ block: 'nearest' });
    };
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (state.fields?.readOnly) return;
        if (!form.checkValidity()) { form.reportValidity(); return; }
        if (blocking) { showErrors(L().FormValidationError || ''); return; }
        const p = scopeRefParams();
        const body = {
            cycleCode: el.code.value.trim(),
            cycleName: el.name.value.trim(),
            year: Number(el.year.value),
            sequenceInYear: Number(el.sequence.value),
            startDate: `${el.start.value}T00:00:00Z`,
            endDate: `${el.end.value}T00:00:00Z`,
            scopeType: p.scopeType,
            countryScope: p.countryScope,
            legalEntityId: p.legalEntityId,
            businessUnitId: p.businessUnitId,
            businessUnitCountryContext: p.businessUnitCountryContext,
            description: el.description.value.trim() || null,
            expectedVersion: el.version.value ? Number(el.version.value) : null
        };
        el.save.disabled = true;
        try {
            const result = state.isNew
                ? await S.sendJson('POST', '/periods', body)
                : await S.sendJson('PUT', `/periods/${encodeURIComponent(el.id.value)}`, body);
            const id = state.isNew ? result : el.id.value;
            offcanvas()?.hide();
            window.showToast?.(state.isNew ? (L().RecordCreated || '') : (L().RecordUpdated || ''), 'success');
            savedHandlers.forEach(h => { try { h(id, state.isNew); } catch (e) { console.error(e); } });
        } catch (e) {
            showErrors(e.message || L().ErrorOccurred || '');
        } finally {
            el.save.disabled = false;
        }
    });

    // ── close and open a new period (K-3) ────────────────────────────────────────────────────────────────────
    el.closeAndReopen.addEventListener('click', () => {
        const p = state.period;
        if (!p) return;
        window.showConfirm?.(L().CloseAndReopenConfirm, async () => {
            try {
                await S.sendJson('POST', `/periods/${encodeURIComponent(p.cyclePeriodId)}/close`);
                window.showToast?.(L().RecordClosed || '', 'success');
                savedHandlers.forEach(h => { try { h(p.cyclePeriodId, false); } catch (e) { console.error(e); } });
                offcanvas()?.hide();
                // Same address, same year; the code and sequence are suggested afresh for the new period.
                setTimeout(() => openCreate({
                    scopeType: p.scopeType, countryScope: p.countryScope, legalEntityId: p.legalEntityId,
                    businessUnitId: p.businessUnitId, businessUnitCountryContext: p.businessUnitCountryContext, year: p.year
                }), 350);
            } catch (e) { showErrors(e.message || L().ErrorOccurred || ''); }
        }, { entityName: p.cycleName, type: 'warning', confirmButtonText: L().CloseCyclePeriod });
    });

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    el.code.addEventListener('input', () => { touched.code = true; });
    el.sequence.addEventListener('input', () => { touched.sequence = true; refreshNameHint(); runChecks(); });
    el.name.addEventListener('input', () => { touched.name = true; refreshNameHint(); });
    el.nameUse.addEventListener('click', () => { touched.name = false; touched.sequence = false; refreshNameHint(); refreshCodeSuggestion(); });
    el.year.addEventListener('input', () => { fillMonthTemplates(); markTemplate(); refreshNameHint(); refreshCodeSuggestion(); runChecks(); });
    el.templates.addEventListener('click', event => {
        const btn = event.target.closest('[data-template]');
        if (!btn || btn.disabled) return;
        const r = templateRange(btn.dataset.template, Number(el.year.value));
        if (!r) return;
        [el.start.value, el.end.value] = r;
        markTemplate(); refreshWorkingDays(); runChecks();
    });
    el.month.addEventListener('change', () => {
        const m = Number(el.month.value), y = Number(el.year.value);
        if (!m || !y) return;
        el.start.value = S.isoDay(new Date(Date.UTC(y, m - 1, 1)));
        el.end.value = S.isoDay(new Date(Date.UTC(y, m, 0)));
        el.month.value = '';
        markTemplate(); refreshWorkingDays(); runChecks();
    });
    [el.start, el.end].forEach(x => x.addEventListener('change', () => { markTemplate(); refreshWorkingDays(); runChecks(); }));
    form.querySelectorAll('input[name="pScopeType"]').forEach(r => r.addEventListener('change', () => {
        applyScope(); refreshCodeSuggestion(); refreshWorkingDays(); runChecks();
    }));
    [el.country, el.legalEntity, el.businessUnit].forEach(x => x.addEventListener('change', () => {
        refreshCodeSuggestion(); refreshWorkingDays(); runChecks();
    }));
    el.buCountry.addEventListener('change', () => { void refreshBusinessUnits(); refreshWorkingDays(); });

    window.CyclePeriodPanel = {
        openCreate,
        openEdit,
        onSaved: handler => { if (typeof handler === 'function') savedHandlers.push(handler); }
    };
})(window, document);
