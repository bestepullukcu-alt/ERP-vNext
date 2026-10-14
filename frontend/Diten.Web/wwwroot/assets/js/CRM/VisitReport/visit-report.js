/**
 * WP-VW-W4 (W4-WEB-a) — the visit report wizard v2 (mockup 03-rapor + DESIGN-GAP-W4-report §1): general → one step per
 * product (A presentation + interest · B market + competitor · C requests + next step) → follow-up + safety → the doctor
 * profile card → summary + submit; the draft saves itself; after the submit 60 minutes of free correction, then a
 * reasoned amendment (old → new). The rules live in report-core.js (window.VisitReportCore); this file reads, draws and
 * wires. Every call goes through the same-origin proxy (data-api-base); the server is the authority (the completeness,
 * the class / opportunity, the 60 minutes) — the page only previews.
 */
(function () {
    'use strict';

    const root = document.getElementById('vr-root');
    if (!root) { return; }

    const C = window.VisitReportCore;
    const L = window.VisitReportL10n || {};
    const base = root.getAttribute('data-api-base') || '';
    const workspaceUrl = root.getAttribute('data-workspace-url') || '/CRM/VisitWorkspace';
    const pharmacyUrl = root.getAttribute('data-pharmacy-url') || '/CRM/PharmacyReport';
    const plannedVisitId = root.getAttribute('data-planned-visit-id') || '';
    const flag = name => root.getAttribute('data-' + name) === 'true';
    const perms = {
        record: flag('can-record'), amend: flag('can-amend'), visit: flag('can-read-visit'), execution: flag('can-read-execution'),
        insights: flag('can-read-insights'), lookup: flag('can-lookup-competitors'), propose: flag('can-propose-competitors'),
        gskus: flag('can-read-gskus'), products: flag('can-pick-products')
    };
    const lang = String((document.documentElement && document.documentElement.lang) || 'tr').toLowerCase().split('-')[0];

    const el = id => document.getElementById(id);
    const esc = v => String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fmt = C.fmt;
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    const toast = (m, t) => { if (m && window.showToast) { window.showToast(m, t || 'info'); } };
    const S = C.SETS;

    const state = {
        phase: 'loading', prefill: null, body: null, ctx: null, version: 0, reportId: null, status: null, submittedAt: null,
        amendments: [], visit: null, execution: null, presentation: null, names: {}, gskus: {}, reimbursement: [], insight: null,
        step: 0, mode: 'edit', reason: '', sent: false, saving: false, savedAt: null, dirty: false, timer: null,
        serverMissing: null, proposal: {}, history: false, addProduct: '', productOptions: []
    };

    // ── the API ──────────────────────────────────────────────────────────────────────────────────────────────
    async function api(method, path, body) {
        const options = { method: method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        try {
            const res = await fetch(base + path, options);
            const text = await res.text();
            let json = null;
            try { json = text ? JSON.parse(text) : null; } catch (e) { json = null; }
            return { ok: res.ok, status: res.status, data: json ? (json.data !== undefined ? json.data : json.Data) : null, errors: (json && (json.errors || json.Errors)) || [] };
        } catch (e) {
            return { ok: false, status: 0, data: null, errors: [] };
        }
    }

    const items = d => (Array.isArray(d) ? d : (d && Array.isArray(d.items) ? d.items : []));

    // ── load ─────────────────────────────────────────────────────────────────────────────────────────────────
    async function load() {
        if (!plannedVisitId) { return notice(L.NoVisitTitle, L.NoVisitText); }
        const pre = await api('GET', '/prefill/' + encodeURIComponent(plannedVisitId));
        if (!pre.ok || !pre.data) { return notice(pre.status === 404 ? L.NotFoundTitle : L.LoadFailed, pre.status === 404 ? L.NotFoundText : C.errorText(pre.errors, L)); }
        const p = pre.data;
        // CT (W4 K13 I1): a pharmacy visit opens the pharmacy report page (W4-WEB-b) — every entry (workspace, presentation)
        // keeps pointing here, the wizard forwards; the notice stays only for a page without that route.
        if (p.reportKind === 'pharmacy') {
            if (window.location && typeof window.location.replace === 'function') { window.location.replace(C.pharmacyReportUrl(pharmacyUrl, plannedVisitId)); return; }
            return notice(L.PharmacyTitle, L.PharmacyText);
        }
        state.prefill = p;
        state.body = C.bodyOf(p);
        state.ctx = C.contextOf(p);
        state.version = p.version || 0;
        state.reportId = p.visitReportId || null;
        state.status = p.reportStatus || null;
        state.mode = state.status === 'submitted' || state.status === 'amended' ? 'ro' : 'edit';

        const jobs = [];
        if (perms.visit) { jobs.push(api('GET', '/visits/' + encodeURIComponent(plannedVisitId)).then(r => { if (r.ok) { state.visit = r.data; } })); }
        if (perms.execution) { jobs.push(api('GET', '/execution/by-plan/' + encodeURIComponent(plannedVisitId)).then(r => { if (r.ok) { state.execution = r.data; } })); }
        jobs.push(api('GET', '/presentation/' + encodeURIComponent(plannedVisitId) + '?lang=' + encodeURIComponent(lang)).then(r => { if (r.ok) { state.presentation = r.data; } }));
        jobs.push(api('GET', '/reference-data/' + S.reimbursement + '/values').then(r => {
            state.reimbursement = r.ok ? items(r.data).map(v => ({ code: v.valueCode || v.code, label: C.optionLabel({ code: v.valueCode || v.code, label: v.displayName, labels: v.attributes }, lang) })) : [];
        }));
        if (state.reportId) { jobs.push(loadReport()); }
        if (perms.insights && p.contactId) { jobs.push(api('GET', '/doctors/' + encodeURIComponent(p.contactId) + '/profile').then(r => { if (r.ok) { state.insight = r.data; } })); }
        if (perms.products) { jobs.push(api('GET', '/products?pageSize=100&pageNumber=1').then(r => { state.productOptions = r.ok ? items(r.data) : []; })); }
        await Promise.all(jobs);
        names();
        if (perms.gskus) { await Promise.all(state.body.productSections.map(s => loadGskus(s.productId))); }
        prefillNote();
        state.phase = 'ready';
        show('vr-loading', false);
        render();
    }

    async function loadReport() {
        const r = await api('GET', '/reports/' + encodeURIComponent(state.reportId));
        if (r.ok && r.data) {
            state.submittedAt = r.data.submittedAt || null;
            state.amendments = r.data.amendments || [];
            state.status = r.data.reportStatus || state.status;
            state.version = r.data.version != null ? r.data.version : state.version;
        }
    }

    async function loadGskus(productId) {
        const code = productCode(productId);
        const r = await api('GET', '/gskus?pageSize=100&pageNumber=1' + (code ? '&search=' + encodeURIComponent(code) : ''));
        state.gskus[productId] = r.ok ? C.gskuOptions(items(r.data), productId) : [];
    }

    /** The product names: the plan's snapshot, the presentation's slides, the product picker. */
    function names() {
        const out = {};
        ((state.visit && state.visit.contentItems) || []).forEach(c => { out[c.productId] = c.productName || c.productCode || null; });
        ((state.presentation && state.presentation.slides) || []).forEach(s => { if (!out[s.productId]) { out[s.productId] = s.productName || s.productCode || null; } });
        (state.productOptions || []).forEach(o => { if (!out[o.id]) { out[o.id] = o.globalProductName || o.canonicalCode; } });
        state.names = Object.assign(out, state.names);
    }

    function productCode(productId) {
        const c = ((state.visit && state.visit.contentItems) || []).find(x => x.productId === productId);
        const o = (state.productOptions || []).find(x => x.id === productId);
        return (c && c.productCode) || (o && o.canonicalCode) || '';
    }

    /** "Genel değerlendirme" starts from the presentation's note (W3) when the rep has not written one. */
    function prefillNote() {
        const g = state.body.general;
        state.noteFromPresentation = false;
        if (!state.reportId && !g.generalNote && state.execution && state.execution.note) {
            g.generalNote = state.execution.note;
            state.noteFromPresentation = true;
        }
    }

    function notice(title, text) {
        state.phase = 'notice';
        show('vr-loading', false);
        const n = el('vr-notice');
        n.innerHTML = '<strong>' + esc(title || '') + '</strong> ' + esc(text || '') + ' <a href="' + esc(workspaceUrl) + '">' + esc(L.BackToWorkspace || '') + '</a>';
        show('vr-notice', true);
    }

    // ── derived ──────────────────────────────────────────────────────────────────────────────────────────────
    const editable = () => state.mode === 'edit' || state.mode === 'unlocked' || state.mode === 'fix';
    const nameOf = id => state.names[id] || L.UnknownProduct || '—';
    const opts = set => C.options(state.prefill, set, lang);
    const label = (set, code) => { const o = opts(set).find(x => x.code === code); return o ? o.label : (code || ''); };
    const missingRows = () => C.missing(state.body, state.ctx, { prefill: state.prefill, amend: state.mode === 'fix', reason: state.reason });
    const stepList = () => C.steps(state.prefill.reportKind, state.body, state.names);
    const curStep = () => { const s = stepList(); return s[Math.min(state.step, s.length - 1)]; };
    const sectionIndex = productId => state.body.productSections.findIndex(s => s.productId === productId);
    const fieldText = row => (row.productId ? nameOf(row.productId) + ' · ' : '') + (L['Field_' + row.field.replace(/\./g, '_')] || row.field);
    const stepTitle = s => s.key === 'general' ? L.StepGeneral : s.key === 'safety' ? L.StepSafety : s.key === 'doctor' ? L.StepDoctor
        : s.key === 'summary' ? L.StepSummary : (s.name || nameOf(s.productId));

    // ── render ───────────────────────────────────────────────────────────────────────────────────────────────
    function render() {
        if (state.phase !== 'ready') { return; }
        renderHead();
        renderChips();
        renderBanner();
        if (state.sent) {
            el('vr-steps').innerHTML = '';
            el('vr-body').innerHTML = '';
            show('vr-bar', false);
            return;
        }
        renderSteps();
        renderBody();
        renderBar();
    }

    /** The light refresh while typing: the counts, never the field being typed into. */
    function renderLight() {
        renderChips();
        renderSteps();
        renderBar();
        const warn = el('vr-total-warn');
        if (warn) { warn.innerHTML = totalWarning(curStep().productId); }
    }

    function renderHead() {
        const v = state.visit || {};
        el('vr-title').textContent = L.TitleDoctor || '';
        el('vr-sub').textContent = [v.targetDisplayName || v.contactDisplayName, v.accountDisplayName, v.plannedDate].filter(Boolean).join(' · ');
    }

    function renderChips() {
        const out = [];
        const v = state.visit || {};
        const hours = C.hoursLeft(v.reportDeadline, Date.now());
        if (hours !== null && state.mode === 'edit') {
            out.push('<span class="vr-chip ' + (hours <= 6 ? 'vr-da' : 'vr-wa') + '"><i class="bx bx-time-five"></i>' + esc(fmt(L.DeadlineChip || '{0}', hours)) + '</span>');
        }
        const miss = missingRows();
        out.push('<span class="vr-chip ' + (miss.length ? 'vr-da' : 'vr-ok') + '" id="vr-valid"><i class="bx ' + (miss.length ? 'bx-error-circle' : 'bx-check-circle') + '"></i>'
            + esc(miss.length ? fmt(L.MissingChip || '{0}', miss.length) : (L.ReadyChip || '')) + '</span>');
        if (state.mode === 'edit' && perms.record) {
            out.push('<span class="vr-save"><i class="bx bx-cloud-upload"></i>' + esc(state.saving ? (L.Saving || '') : state.savedAt ? fmt(L.AutoSaved || '{0}', state.savedAt) : (L.AutoDraft || '')) + '</span>');
        }
        el('vr-chips').innerHTML = out.join('');
    }

    function renderBanner() {
        const box = el('vr-banner');
        if (state.sent) {
            const w = C.editWindow(state.submittedAt, Date.now());
            box.innerHTML = '<div class="vr-card vr-sent"><div class="vr-sent-head"><span class="vr-sent-ic"><i class="bx bx-check-circle"></i></span><div><div class="vr-sent-title">'
                + esc(L.SentTitle || '') + '</div><div class="vr-small">' + esc(fmt(L.SentText || '{0}', w.minutesLeft)) + '</div></div></div>'
                + warningsHtml() + '<div class="vr-actions"><a class="btn btn-primary" href="' + esc(workspaceUrl) + '">' + esc(L.BackToCalendar || '') + '</a>'
                + '<button type="button" class="btn btn-outline-secondary" data-act="view-report">' + esc(L.ViewReport || '') + '</button></div></div>';
            return;
        }
        if (state.mode === 'ro') {
            const w = C.editWindow(state.submittedAt, Date.now());
            const canFix = !w.open && perms.amend;
            const canUnlock = w.open && perms.record;
            box.innerHTML = '<div class="vr-ro"><span><i class="bx bx-check-circle"></i><strong>' + esc(L.SubmittedTitle || '') + '</strong> '
                + esc(w.open ? fmt(L.EditableFor || '{0}', w.minutesLeft) : (L.WindowClosed || '')) + '</span>'
                + (canUnlock ? '<button type="button" class="btn btn-primary btn-sm" data-act="unlock">' + esc(fmt(L.UnlockButton || '{0}', w.minutesLeft)) + '</button>' : '')
                + (canFix ? '<button type="button" class="btn btn-warning btn-sm" data-act="fix">' + esc(L.FixButton || '') + '</button>' : '') + '</div>';
            return;
        }
        if (state.mode === 'fix') {
            box.innerHTML = '<div class="vr-card vr-fix"><div class="vr-h6">' + esc(L.FixTitle || '') + '</div><div class="vr-small">' + esc(L.FixText || '') + '</div>'
                + '<textarea class="form-control" rows="2" id="vr-reason" data-reason="1" maxlength="500" placeholder="' + esc(L.FixReasonPlaceholder || '') + '">' + esc(state.reason) + '</textarea></div>';
            return;
        }
        box.innerHTML = '';
    }

    function warningsHtml() {
        const w = state.warnings || [];
        if (!w.length) { return ''; }
        return '<ul class="vr-warnings">' + w.map(x => '<li>' + esc(x.indexOf('competitor_proposal_pending:') === 0
            ? fmt(L.WarnProposalPending || '{0}', x.slice('competitor_proposal_pending:'.length)) : (L.WarnSideEffect || x)) + '</li>').join('') + '</ul>';
    }

    function renderSteps() {
        const list = stepList();
        const counts = C.missingByStep(missingRows(), state.prefill.reportKind);
        const total = missingRows().length;
        const cur = Math.min(state.step, list.length - 1);
        el('vr-steps').innerHTML = list.map((s, i) => {
            const n = counts[s.key] || 0;
            const on = i === cur, done = i < cur && !n;
            const sub = s.key === 'summary' ? (total ? fmt(L.StepMissing || '{0}', total) : (L.StepReady || '')) : n ? fmt(L.StepMissing || '{0}', n)
                : i < cur ? (L.StepDone || '') : s.productId ? (s.unplanned ? (L.UnplannedTag || '') : (L.StepProduct || '')) : '';
            return '<button type="button" role="tab" class="vr-step' + (on ? ' active' : '') + (n ? ' has-missing' : '') + '" aria-selected="' + on + '" data-act="step" data-step="' + i + '">'
                + '<span class="vr-step-no' + (done ? ' done' : '') + '">' + (done ? '✓' : (i + 1)) + '</span><span class="vr-step-text"><span class="vr-step-l">' + esc(stepTitle(s))
                + '</span><span class="vr-step-s">' + esc(sub) + '</span></span></button>';
        }).join('');
    }

    function renderBar() {
        const list = stepList();
        const cur = Math.min(state.step, list.length - 1);
        const last = cur === list.length - 1;
        const miss = missingRows().length;
        const canSend = state.mode === 'fix' ? perms.amend : perms.record;
        const out = [];
        if (cur > 0) { out.push('<button type="button" class="btn btn-outline-secondary" data-act="prev"><i class="bx bx-chevron-left vr-flip"></i>' + esc(L.BackButton || '') + '</button>'); }
        out.push('<span class="vr-bar-l">' + esc(fmt(L.StepOf || '{0} / {1}', cur + 1, list.length)) + '</span>');
        // the phone (mockup): "Taslak" / "İleri" — the step name only where there is room
        if (state.mode === 'edit' && perms.record) { out.push('<button type="button" class="btn btn-label-secondary" data-act="save-draft"><span class="vr-long">' + esc(L.SaveDraft || '') + '</span><span class="vr-short">' + esc(L.SaveDraftShort || '') + '</span></button>'); }
        if (!last) { out.push('<button type="button" class="btn btn-primary" data-act="next"><span class="vr-long">' + esc(fmt(L.NextTo || '{0}', stepTitle(list[cur + 1]))) + '</span><span class="vr-short">' + esc(L.NextShort || '') + '</span><i class="bx bx-chevron-right vr-flip"></i></button>'); }
        if (last && editable() && canSend) {
            out.push('<button type="button" class="btn btn-primary vr-submit' + (miss ? ' vr-dim' : '') + '" data-act="submit"' + (state.saving ? ' disabled' : '') + '><i class="bx bx-send vr-flip"></i>'
                + esc(state.mode === 'fix' ? (L.SubmitFix || '') : (L.Submit || '')) + '</button>');
        }
        el('vr-bar').innerHTML = out.join('');
        show('vr-bar', true);
    }

    function renderBody() {
        const s = curStep();
        let html;
        if (s.key === 'general') { html = generalHtml(); }
        else if (s.key === 'safety') { html = safetyHtml(); }
        else if (s.key === 'doctor') { html = doctorHtml(); }
        else if (s.key === 'summary') { html = summaryHtml(); }
        else if (s.productId) { html = productHtml(s.productId); }
        else if (typeof C.pharmacyHtml === 'function') { html = C.pharmacyHtml(s, state, L); } // WEB-b
        else { html = ''; }
        el('vr-body').innerHTML = '<fieldset class="vr-fieldset"' + (editable() ? '' : ' disabled') + '>' + html + '</fieldset>';
    }

    // ── small builders ───────────────────────────────────────────────────────────────────────────────────────
    const req = () => ' <span class="vr-req">*</span>';
    const fromTag = text => '<span class="vr-tag vr-ok"><i class="bx bx-check"></i>' + esc(text) + '</span>';
    const fieldLabel = (text, required, extra) => '<span class="vr-label">' + esc(text) + (required ? req() : '') + (extra || '') + '</span>';

    /** A segmented single choice: data-act=set (a string) / bool (true | false). */
    function seg(path, current, choices, extraClass) {
        return '<div class="vr-seg' + (extraClass ? ' ' + extraClass : '') + '" role="radiogroup">' + choices.map(c => {
            const on = current === c.value;
            const isBool = typeof c.value === 'boolean';
            return '<button type="button" role="radio" aria-checked="' + on + '" class="vr-seg-btn' + (on ? ' active' : '') + (c.tone ? ' vr-t-' + c.tone : '') + '" data-act="' + (isBool ? 'bool' : 'set')
                + '" data-path="' + esc(path) + '" data-value="' + esc(String(c.value)) + '">' + (c.icon ? '<i class="bx ' + c.icon + '"></i>' : '') + esc(c.label) + '</button>';
        }).join('') + '</div>';
    }

    const setChoices = set => opts(set).map(o => ({ value: o.code, label: o.label }));
    const noneButton = (path, on, text) => '<button type="button" class="vr-none' + (on ? ' active' : '') + '" data-act="toggle" data-path="' + esc(path) + '">' + esc(text) + '</button>';
    const card = (inner, extra) => '<div class="vr-card' + (extra ? ' ' + extra : '') + '">' + inner + '</div>';
    const unavailable = set => opts(set).length ? '' : '<div class="vr-small vr-muted">' + esc(L.ListUnavailable || '') + '</div>';

    // ── Genel ────────────────────────────────────────────────────────────────────────────────────────────────
    function generalHtml() {
        const g = state.body.general;
        const p = state.prefill;
        const span = C.minutesBetween(p.startedAt, p.finishedAt);
        const time = p.startedAt ? clock(p.startedAt) + ' – ' + (p.finishedAt ? clock(p.finishedAt) : '…') + (span !== null ? ' · ' + fmt(L.MinutesShort || '{0}', span) : '') : (L.NoExecution || '');
        const co = ((state.execution && state.execution.coVisitors) || []).map(c => c.displayName).filter(Boolean);
        const coText = co.length ? co.join(', ') : (g.coVisitorUserIds || []).length ? fmt(L.CoVisitorCount || '{0}', g.coVisitorUserIds.length) : (L.NoCoVisitor || '');
        const doctor = (state.visit && (state.visit.contactDisplayName || state.visit.targetDisplayName)) || L.PlannedDoctor || '';
        return card('<div class="vr-h5">' + esc(L.StepGeneral || '') + '</div><div class="vr-grid">'
            + '<div class="vr-field">' + fieldLabel(L.OutcomeLabel || '') + '<span class="vr-done"><i class="bx bx-check"></i>' + esc(L.OutcomeDone || '') + '</span><span class="vr-small vr-muted">' + esc(L.OutcomeHint || '') + '</span></div>'
            + '<div class="vr-field">' + fieldLabel(L.ActualTime || '') + '<span class="vr-locked"><i class="bx bx-lock-alt"></i>' + esc(time) + '</span><span class="vr-small vr-muted">' + esc(L.ServerTime || '') + '</span></div>'
            + '<div class="vr-field">' + fieldLabel(L.VisitKind || '', true) + seg('general.visitKindCode', g.visitKindCode, [{ value: 'in_person', label: L.Kind_in_person || '' }, { value: 'remote', label: L.Kind_remote || '' }]) + '</div>'
            + '<div class="vr-field">' + fieldLabel(L.JointVisit || '') + '<span class="vr-plain"><i class="bx bx-group"></i>' + esc(coText) + '</span></div>'
            + '<div class="vr-field">' + fieldLabel(L.MetPerson || '', true) + '<label class="vr-check"><input type="checkbox" data-act="met"' + (g.metContactId ? ' checked' : '') + '>' + esc(doctor) + '</label></div>'
            + '</div><label class="vr-field">' + fieldLabel(L.GeneralNote || '', false, state.noteFromPresentation ? fromTag(L.FromPresentation || '') : '')
            + '<textarea class="form-control" rows="3" maxlength="' + C.MAX_GENERAL_NOTE + '" data-path="general.generalNote">' + esc(g.generalNote || '') + '</textarea></label>');
    }

    const clock = iso => { const d = new Date(iso); return isNaN(d.getTime()) ? '' : String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0'); };

    // ── a product ────────────────────────────────────────────────────────────────────────────────────────────
    function productHtml(productId) {
        const i = sectionIndex(productId);
        const s = state.body.productSections[i];
        const counts = C.groupCounts(missingRows(), productId);
        const chip = n => '<span class="vr-tag ' + (n ? 'vr-da' : 'vr-ok') + '">' + esc(n ? fmt(L.StepMissing || '{0}', n) : (L.GroupDone || '')) + '</span>';
        const mini = ['a', 'b', 'c'].map(k => '<span class="vr-mini ' + (counts[k] ? 'vr-da' : 'vr-ok') + '"><i class="bx ' + (counts[k] ? 'bx-error-circle' : 'bx-check-circle') + '"></i>'
            + k.toUpperCase() + (counts[k] ? ' · ' + counts[k] : '') + '</span>').join('');
        const role = roleOf(productId);
        return card('<span class="vr-pname">' + esc(nameOf(productId)) + '</span>' + (role ? '<span class="vr-role">' + esc(role) + '</span>' : '')
                + (s.unplanned ? '<span class="vr-tag vr-wa">' + esc(L.UnplannedTag || '') + '</span>' : '') + '<span class="vr-grow"></span>' + mini, 'vr-phead')
            + '<div class="vr-phone-only vr-swipe"><i class="bx bx-chevrons-left"></i>' + esc(L.SwipeHint || '') + '<i class="bx bx-chevrons-right"></i></div>'
            + '<div class="vr-groups">'
            + groupA(s, i, chip(counts.a)) + groupB(s, i, chip(counts.b)) + groupC(s, i, chip(counts.c)) + '</div>'
            + (i === state.body.productSections.length - 1 ? addProductHtml() : '');
    }

    /** Under the LAST product (the mockup): "unplanned product presented" — a Global Product not in the plan opens a new step. */
    function addProductHtml() {
        const extra = (state.productOptions || []).filter(o => !state.body.productSections.some(s => s.productId === o.id));
        if (!perms.products || !editable() || !extra.length) { return ''; }
        return '<div class="vr-inline"><select class="form-select vr-w-auto" id="vr-add-product" data-local="addProduct">' + extra.map(o => '<option value="' + esc(o.id) + '"'
            + (state.addProduct === o.id ? ' selected' : '') + '>' + esc(o.globalProductName || o.canonicalCode) + '</option>').join('') + '</select><button type="button" class="vr-dashed vr-dashed-p" data-act="add-product"><i class="bx bx-plus"></i>'
            + esc(L.AddUnplanned || '') + '</button></div>';
    }

    function roleOf(productId) {
        const c = ((state.visit && state.visit.contentItems) || []).find(x => x.productId === productId);
        if (!c || !c.role) { return ''; }
        return L['Role_' + String(c.role).replace(/-/g, '_')] || '';
    }

    const groupHead = (letter, title, chip) => '<div class="vr-ghead"><span class="vr-gtitle"><span class="vr-letter">' + letter + '</span>' + esc(title) + '</span>' + chip + '</div>';

    function stepTitles(productId) {
        const c = ((state.visit && state.visit.contentItems) || []).find(x => x.productId === productId);
        const map = {};
        ((c && c.steps) || []).forEach(st => { map[st.stepId] = st.title || st.contentCode || ''; });
        ((state.presentation && state.presentation.slides) || []).filter(x => x.productId === productId).forEach(x => { if (!map[x.stepId]) { map[x.stepId] = x.title || ''; } });
        return map;
    }

    function groupA(s, i, chip) {
        const titles = stepTitles(s.productId);
        const steps = (s.presentedSteps || []).map(p => '<li>' + esc(titles[p.stepId] || L.UnknownStep || '') + ' <span class="vr-muted">· ' + esc(fmt(L.SecondsShort || '{0}', p.seconds)) + '</span></li>').join('');
        const plan = s.matchedPlan === true ? '<span class="vr-tag vr-ok">' + esc(L.MatchedPlan || '') + '</span>' : s.matchedPlan === false ? '<span class="vr-tag vr-wa">' + esc(L.Deviation || '') + '</span>' : '';
        const prev = previousSection(s.productId);
        const prevStage = prev && prev.adoptionStageCode;
        const prefilled = initialSection(s.productId);
        const interestFromE4 = prefilled && prefilled.interestCode && prefilled.interestCode === s.interestCode && !state.reportId;
        const stages = opts(S.adoptionStage);
        return '<div class="vr-card vr-group">' + groupHead('A', L.GroupA || '', chip)
            + '<div class="vr-grid">'
            + '<div class="vr-field">' + fieldLabel(L.PresentedSteps || '', false, plan) + (steps ? '<ul class="vr-steps-shown">' + steps + '</ul>' : '<span class="vr-small vr-muted">' + esc(L.NoStepsShown || '') + '</span>') + '</div>'
            + '<label class="vr-field">' + fieldLabel(L.Minutes || '', true, prefilled && prefilled.minutes ? fromTag(L.FromPresentation || '') : '')
            + '<input type="number" min="' + C.MIN_MINUTES + '" max="' + C.MAX_MINUTES + '" class="form-control" data-path="productSections.' + i + '.minutes" data-type="number" value="' + esc(s.minutes == null ? '' : s.minutes) + '">'
            + '<span class="vr-small vr-muted">' + esc(fmt(L.MinutesHint || '{0}', prefilled && prefilled.minutes ? fmt(L.MinutesShort || '{0}', prefilled.minutes) : '—')) + '</span></label>'
            + '</div><div class="vr-grid">'
            + '<div class="vr-field">' + fieldLabel(L.VisitSuccess || '', true, '<span class="vr-muted vr-small">· ' + esc(L.VisitSuccessHint || '') + '</span>') + seg('productSections.' + i + '.visitSuccessCode', s.visitSuccessCode, setChoices(S.visitSuccess)) + unavailable(S.visitSuccess) + '</div>'
            + '<div class="vr-field">' + fieldLabel(L.Commitment || '', true) + seg('productSections.' + i + '.commitmentCode', s.commitmentCode, setChoices(S.commitment), 'vr-wrap') + unavailable(S.commitment) + '</div>'
            + '<div class="vr-field">' + fieldLabel(L.Interest || '', true, interestFromE4 ? fromTag(L.FromPresentation || '') : '')
            + seg('productSections.' + i + '.interestCode', s.interestCode, setChoices(S.interest).map(c => Object.assign(c, { icon: c.value === 'interested' ? 'bx-like' : c.value === 'not_interested' ? 'bx-dislike' : 'bx-minus-circle', tone: c.value === 'interested' ? 'ok' : c.value === 'not_interested' ? 'da' : '' }))) + '</div>'
            + '</div><div class="vr-field">' + fieldLabel(L.AdoptionStage || '', true)
            + '<div class="vr-adopt">' + stages.map(o => '<button type="button" class="vr-adopt-btn' + (s.adoptionStageCode === o.code ? ' active' : '') + (prevStage === o.code ? ' prev' : '') + '" data-act="set" data-path="productSections.' + i + '.adoptionStageCode" data-value="' + esc(o.code) + '">' + esc(o.label) + '</button>').join('') + '</div>'
            + unavailable(S.adoptionStage)
            + '<div class="vr-prevline"><span>' + esc(fmt(L.PreviousVisit || '{0}', prevStage ? label(S.adoptionStage, prevStage) : '—')) + '</span>'
            + (prevStage ? '<button type="button" class="vr-same" data-act="set" data-path="productSections.' + i + '.adoptionStageCode" data-value="' + esc(prevStage) + '">' + esc(L.SameButton || '') + '</button>' : '') + '</div></div>'
            + '</div>';
    }

    function previousSection(productId) {
        const prev = state.prefill.previous;
        return prev ? (prev.productSections || []).find(x => x.productId === productId) || null : null;
    }

    function initialSection(productId) {
        return ((state.prefill.body && state.prefill.body.productSections) || []).find(x => x.productId === productId) || null;
    }

    function indicationRows(productId) {
        const r = (state.prefill.indicationRows || []).find(x => x.productId === productId);
        return r || { items: [], reason: 'no_concept_graph' };
    }

    function totalWarning(productId) {
        if (!productId) { return ''; }
        const hit = C.totalRule(state.body, state.prefill, state.ctx).find(r => r.productId === productId);
        return hit ? '<div class="vr-alert vr-da"><i class="bx bx-error"></i>' + esc(fmt(L.TotalExceeded || '{0} {1}', hit.sum, hit.total)) + '</div>' : '';
    }

    function groupB(s, i, chip) {
        const stored = C.storedPotential(state.prefill, s.productId);
        const asked = C.potentialAsked(s, state.ctx);
        const rows = indicationRows(s.productId);
        const pot = s.potential || { rows: [] };
        const total = C.doctorTotal(state.body, state.prefill);
        const indications = C.hasIndications(state.ctx, s.productId);
        let potential = '';
        if (!indications) {
            // W4-FIX-b (F-5): no approved indication list ⇒ no question, no "no patients" button, no star — a note only
            potential = '<div class="vr-small vr-muted" data-f5="no-indications"><i class="bx bx-info-circle"></i> ' + esc(L.NoIndicationList || '') + '</div>';
        } else if (asked) {
            potential = (rows.items.length ? rows.items.map((r, k) => {
                const at = (pot.rows || []).findIndex(x => x.indicationNodeId === r.indicationNodeId && (x.profileNodeId || null) === (r.profileNodeId || null));
                const on = at > -1;
                const before = stored ? (stored.indicationRows || []).find(x => x.indicationNodeId === r.indicationNodeId && (x.profileNodeId || null) === (r.profileNodeId || null)) : null;
                return '<div class="vr-ind' + (on ? ' on' : '') + '"><label class="vr-check"><input type="checkbox" data-act="ind" data-p="' + i + '" data-k="' + k + '"' + (on ? ' checked' : '') + '>'
                    + '<span class="vr-ind-text"><span class="vr-ind-prof">' + esc(r.profileName || r.indicationName) + '</span>' + (r.profileName ? '<span class="vr-small vr-muted">' + esc(r.indicationName) + '</span>' : '') + '</span></label>'
                    + (on ? '<div class="vr-ind-num"><input type="number" min="0" class="form-control vr-num" data-path="productSections.' + i + '.potential.rows.' + at + '.monthlyPatients" data-type="number" value="' + esc(pot.rows[at].monthlyPatients == null ? '' : pot.rows[at].monthlyPatients)
                        + '" placeholder="' + esc(before ? before.monthlyPatients : '') + '"><span class="vr-small">' + esc(L.PatientsPerMonth || '') + '</span>'
                        + (before ? '<span class="vr-small vr-muted">' + esc(fmt(L.PreviousValue || '{0}', before.monthlyPatients)) + '</span><button type="button" class="vr-same" data-act="ind-same" data-p="' + i + '" data-k="' + k + '">' + esc(L.SameButton || '') + '</button>' : '')
                        + '</div>' : '') + '</div>';
            }).join('') : '<div class="vr-small vr-muted">' + esc(L.NoIndicationList || '') + '</div>')
                + noneButton('productSections.' + i + '.potential.noPatientsInIndications', pot.noPatientsInIndications === true, L.NoPatients || '');
        } else {
            const when = stored && stored.asOf ? String(stored.asOf).slice(0, 10) : '—';
            potential = '<div class="vr-last"><span>' + esc(fmt(L.LastPotential || '{0} {1}', stored && stored.indicationTotal != null ? stored.indicationTotal : '—', when)) + '</span>'
                + (editable() ? '<button type="button" class="vr-same" data-act="pot-update" data-p="' + i + '">' + esc(L.UpdateNumbers || '') + '</button>' : '') + '</div>';
        }
        const pv = C.preview(s, state.prefill, state.ctx);
        const prevShare = stored && stored.shareBucketCode;
        const comp = competitorHtml(s, i);
        return '<div class="vr-card vr-group">' + groupHead('B', L.GroupB || '', chip)
            + '<div class="vr-field">' + fieldLabel(L.IndicationPatients || '', asked) + (indications ? '<span class="vr-small vr-muted">' + esc(asked ? (L.IndicationHint || '') : (L.ThreeMonthHint || '')) + '</span>' : '') + potential + '</div>'
            + '<div class="vr-small vr-muted">' + esc(fmt(L.DoctorTotalLine || '{0}', total == null ? '—' : total)) + '</div>'
            + '<div id="vr-total-warn">' + totalWarning(s.productId) + '</div>'
            + '<div class="vr-field">' + fieldLabel(L.ShareBucket || '', true) + seg('productSections.' + i + '.shareBucketCode', s.shareBucketCode, setChoices(S.shareBucket), 'vr-wrap') + unavailable(S.shareBucket)
            + '<div class="vr-prevline"><span>' + esc(fmt(L.PreviousValue || '{0}', prevShare ? label(S.shareBucket, prevShare) : '—')) + '</span>'
            + (prevShare ? '<button type="button" class="vr-same" data-act="set" data-path="productSections.' + i + '.shareBucketCode" data-value="' + esc(prevShare) + '">' + esc(L.SameButton || '') + '</button>' : '') + '</div></div>'
            + '<div class="vr-calc"><span class="vr-calc-k">' + esc(L.ComputedLabel || '') + '</span><div class="vr-calc-row"><span>' + esc(L.PotentialLabel || '') + ': <strong>'
            + esc(pv.potentialClass ? (L['Class_' + pv.potentialClass] || pv.potentialClass) + (pv.total != null ? ' · ' + fmt(L.PatientsCount || '{0}', pv.total) : '') : '—') + '</strong></span>'
            + (pv.threshold ? '<span class="vr-small">' + esc(fmt(L.ThresholdLine || '{0} {1}', pv.threshold.lowBelow, pv.threshold.highAbove)) + '</span>' : '') + '</div>'
            + (pv.opportunity ? '<span class="vr-opp vr-opp-' + esc(pv.opportunity) + '">' + esc(L['Opp_' + pv.opportunity] || pv.opportunity) + '</span>' : '')
            + '<span class="vr-small vr-muted">' + esc(L.PreviewNote || '') + '</span></div>'
            + comp + '</div>';
    }

    function competitorOptions(productId) {
        const c = (state.prefill.competitors || []).find(x => x.productId === productId);
        return c || { available: false, items: [] };
    }

    function competitorHtml(s, i) {
        const list = competitorOptions(s.productId);
        const chosen = s.competitors || [];
        const chips = list.items.map(o => {
            const on = chosen.some(c => c.competitorProductId === o.id);
            return '<button type="button" class="vr-chipbtn' + (on ? ' active' : '') + '" data-act="comp" data-p="' + i + '" data-id="' + esc(o.id) + '">' + (on ? '<i class="bx bx-check"></i>' : '') + esc(o.name) + '</button>';
        }).join('') + chosen.filter(c => !list.items.some(o => o.id === c.competitorProductId)).map(c => '<button type="button" class="vr-chipbtn active" data-act="comp-rm" data-p="' + i + '" data-c="'
            + chosen.indexOf(c) + '"><i class="bx bx-check"></i>' + esc(c.nameSnapshot) + '</button>').join('');
        const cards = chosen.map((c, k) => compCard(c, k, i, list.items.find(o => o.id === c.competitorProductId))).join('');
        const p = state.proposal[i] || { open: false, name: '', company: '', hits: [] };
        const adds = editable() ? '<button type="button" class="vr-link" data-act="prop-open" data-p="' + i + '"><i class="bx bx-plus"></i>' + esc(L.NotInList || '') + '</button>'
            + (p.open ? '<div class="vr-prop"><label class="vr-field">' + fieldLabel(L.CompetitorName || '') + '<input class="form-control" id="vr-prop-name-' + i + '" data-local="prop" data-p="' + i + '" data-k="name" value="' + esc(p.name) + '" maxlength="200"></label>'
                + '<label class="vr-field">' + fieldLabel(L.CompetitorCompany || '') + '<input class="form-control" id="vr-prop-company-' + i + '" data-local="prop" data-p="' + i + '" data-k="company" value="' + esc(p.company) + '" maxlength="200"></label>'
                + '<button type="button" class="btn btn-primary" data-act="prop-add" data-p="' + i + '">' + esc(L.AddButton || '') + '</button>'
                + (p.hits && p.hits.length ? '<div class="vr-hits"><span class="vr-small">' + esc(L.DidYouMean || '') + '</span>' + p.hits.map(h => '<button type="button" class="vr-chipbtn" data-act="prop-pick" data-p="' + i + '" data-id="' + esc(h.id) + '">' + esc(h.name)
                    + (h.company ? ' · ' + esc(h.company) : '') + '</button>').join('') + '</div>' : '') + '</div>' : '') : '';
        const unavailableNote = !list.available ? '<div class="vr-small vr-muted">' + esc(L.CompetitorsUnavailable || '') + '</div>' : '';
        return '<div class="vr-field">' + fieldLabel(L.Competitors || '', list.available) + unavailableNote
            + '<div class="vr-chips-row">' + chips + (chosen.length ? '' : noneButton('productSections.' + i + '.noCompetitor', !!s.noCompetitor, L.NoCompetitor || '')) + '</div>'
            + cards + adds + '</div>';
    }

    function compCard(c, k, i, option) {
        const o = option || {};
        const country = o.country || null;
        const reimb = country && country.reimbursementStatusCode ? ((state.reimbursement.find(r => r.code === country.reimbursementStatusCode) || {}).label || country.reimbursementStatusCode) : '—';
        const price = country && country.boxPrice != null ? fmtPrice(country.boxPrice, country.currencyCode) : '—';
        const known = !!option;
        const advs = opts(S.advantage).map(a => {
            const v = C.advantageOf(c, a.code);
            return '<button type="button" class="vr-adv' + (v === '+' ? ' plus' : v === '-' ? ' minus' : '') + '" data-act="adv" data-p="' + i + '" data-c="' + k + '" data-code="' + esc(a.code) + '">'
                + (v === '+' ? '+ ' : v === '-' ? '− ' : '') + esc(a.label) + '</button>';
        }).join('');
        return '<div class="vr-comp"><div class="vr-comp-head"><span class="vr-comp-n">' + esc(c.nameSnapshot || o.name || '') + '</span><span class="vr-muted vr-small">' + esc(c.company || o.company || '—') + '</span></div>'
            + (known ? '<div class="vr-comp-grid"><span><span class="vr-muted">' + esc(L.ActiveIngredient || '') + '</span><br>' + esc((o.activeIngredients || []).join(', ') || '—') + '</span>'
                + '<span><span class="vr-muted">' + esc(L.FormStrength || '') + '</span><br>' + esc([formText(o.formCode), o.strength].filter(Boolean).join(' · ') || '—') + '</span>'
                + '<span><span class="vr-muted">' + esc(L.BoxPrice || '') + '</span><br>' + esc(price) + '</span>'
                + '<span><span class="vr-muted">' + esc(L.ReimbursementLabel || '') + '</span><br>' + esc(reimb) + '</span></div>' : '')
            + (c.proposed ? '<span class="vr-tag vr-wa">' + esc(c.competitorProductId ? (L.ProposalPending || '') : (L.ProposalOnSubmit || '')) + '</span>' : '')
            + '<span class="vr-small vr-muted">' + esc(L.AdvantageHint || '') + '</span><div class="vr-chips-row">' + advs + '</div></div>';
    }

    const formText = code => (code ? (L['Form_' + String(code).replace(/-/g, '_')] || String(code).replace(/-/g, ' ')) : '');

    function fmtPrice(value, currency) {
        try { return new Intl.NumberFormat(lang, { style: 'currency', currency: currency || 'TRY', maximumFractionDigits: 2 }).format(Number(value)); }
        catch (e) { return String(value) + ' ' + (currency || ''); }
    }

    function groupC(s, i, chip) {
        const cats = opts(S.objection);
        const types = opts(S.requestType);
        const prefilled = initialSection(s.productId) || {};
        const objs = (s.objections || []).map((o, k) => {
            const lib = C.libraryAnswers(state.presentation, s.productId, o.categoryCode);
            return '<div class="vr-row"><div class="vr-inline"><select class="form-select" data-path="productSections.' + i + '.objections.' + k + '.categoryCode"><option value="">' + esc(L.SelectCategory || '') + '</option>'
                + cats.map(c => '<option value="' + esc(c.code) + '"' + (o.categoryCode === c.code ? ' selected' : '') + '>' + esc(c.label) + '</option>').join('') + '</select>'
                + '<button type="button" class="vr-icon" aria-label="' + esc(L.RemoveLabel || '') + '" data-act="row-rm" data-path="productSections.' + i + '.objections" data-k="' + k + '"><i class="bx bx-trash"></i></button></div>'
                + (lib.length ? '<label class="vr-field">' + fieldLabel(L.LibraryAnswer || '') + '<select class="form-select" data-path="productSections.' + i + '.objections.' + k + '.objectionResponseId"><option value="">' + esc(L.NoLibraryAnswer || '') + '</option>'
                    + lib.map(a => '<option value="' + esc(a.id) + '"' + (o.objectionResponseId === a.id ? ' selected' : '') + '>' + esc(a.question) + '</option>').join('') + '</select></label>' : '')
                + '<input class="form-control" placeholder="' + esc(L.NoteLabel || '') + '" maxlength="2000" data-path="productSections.' + i + '.objections.' + k + '.note" value="' + esc(o.note || '') + '">'
                + '<div class="vr-inline vr-small">' + esc(L.Resolved || '') + req() + seg('productSections.' + i + '.objections.' + k + '.resolved', o.resolved, C.RESOLUTIONS.map(r => ({ value: r, label: L['Resolved_' + r] || r }))) + '</div></div>';
        }).join('');
        const reqs = (s.requests || []).map((r, k) => '<div class="vr-row"><div class="vr-inline"><select class="form-select" data-path="productSections.' + i + '.requests.' + k + '.typeCode"><option value="">' + esc(L.SelectRequest || '') + '</option>'
            + types.map(t => '<option value="' + esc(t.code) + '"' + (r.typeCode === t.code ? ' selected' : '') + '>' + esc(t.label) + '</option>').join('') + '</select>'
            + '<input type="date" class="form-control vr-w-auto" data-path="productSections.' + i + '.requests.' + k + '.dueDate" value="' + esc(r.dueDate || '') + '">'
            + '<button type="button" class="vr-icon" aria-label="' + esc(L.RemoveLabel || '') + '" data-act="row-rm" data-path="productSections.' + i + '.requests" data-k="' + k + '"><i class="bx bx-trash"></i></button></div>'
            + '<input class="form-control" placeholder="' + esc(L.NoteLabel || '') + '" maxlength="2000" data-path="productSections.' + i + '.requests.' + k + '.note" value="' + esc(r.note || '') + '"></div>').join('');
        const list = (title, key, none, rowsHtml, addText, noneText, e4) => '<div class="vr-field">' + fieldLabel(title, true, e4 ? fromTag(L.FromPresentation || '') : '') + rowsHtml
            + '<div class="vr-inline">' + (editable() ? '<button type="button" class="vr-dashed" data-act="row-add" data-path="productSections.' + i + '.' + key + '"><i class="bx bx-plus"></i>' + esc(addText) + '</button>' : '')
            + ((s[key] || []).length ? '' : noneButton('productSections.' + i + '.' + none, !!s[none], noneText)) + '</div></div>';
        const gskus = state.gskus[s.productId] || [];
        const sampleValue = s.sample ? s.sample.gskuId : s.noSample ? 'none' : '';
        const signed = state.prefill.sampleSigned;
        const sample = '<div class="vr-field">' + fieldLabel(L.SampleLabel || '', true) + '<select class="form-select" data-act-change="sample" data-p="' + i + '"><option value="">' + esc(L.Choose || '') + '</option><option value="none"' + (sampleValue === 'none' ? ' selected' : '') + '>' + esc(L.NoSample || '') + '</option>'
            + gskus.map(g => '<option value="' + esc(g.id) + '"' + (sampleValue === g.id ? ' selected' : '') + '>' + esc(g.label) + '</option>').join('')
            + (s.sample && !gskus.some(g => g.id === s.sample.gskuId) ? '<option value="' + esc(s.sample.gskuId) + '" selected>' + esc(L.SampleRecorded || '') + '</option>' : '') + '</select>'
            + (!gskus.length ? '<span class="vr-small vr-muted">' + esc(L.NoGskus || '') + '</span>' : '')
            + (s.sample ? '<div class="vr-grid vr-grid-sm"><label class="vr-field">' + fieldLabel(L.QuantityLabel || '', true) + '<input type="number" min="1" max="' + C.MAX_SAMPLE + '" class="form-control" data-path="productSections.' + i + '.sample.quantity" data-type="number" value="' + esc(s.sample.quantity == null ? '' : s.sample.quantity) + '"></label>'
                + '<label class="vr-field">' + fieldLabel(L.Lot || '') + '<input class="form-control" maxlength="40" data-path="productSections.' + i + '.sample.lot" value="' + esc(s.sample.lot || '') + '" placeholder="L2609A"></label></div>'
                + '<span class="vr-small ' + (signed ? 'vr-okt' : 'vr-muted') + '"><i class="bx bx-pen"></i>' + esc(signed ? (L.SampleSigned || '') : (L.SampleNotSigned || '')) + '</span>' : '') + '</div>';
        const key = C.suggestionKey(s.commitmentCode, s.adoptionStageCode) || s.nextMessageSuggestion;
        const sug = C.suggestionText(key, L, code => label(S.adoptionStage, code));
        const next = '<label class="vr-field">' + fieldLabel(L.NextMessage || '', true) + '<input class="form-control" maxlength="' + C.MAX_NEXT_MESSAGE + '" data-path="productSections.' + i + '.nextMessage" value="' + esc(s.nextMessage || '') + '" placeholder="' + esc(L.NextMessagePlaceholder || '') + '">'
            + (sug ? '<span class="vr-prevline"><span>' + esc(fmt(L.Suggestion || '{0}', sug)) + '</span>' + (editable() ? '<button type="button" class="vr-same" data-act="use-sug" data-p="' + i + '">' + esc(L.UseSuggestion || '') + '</button>' : '') + '</span>' : '') + '</label>';
        return '<div class="vr-card vr-group">' + groupHead('C', L.GroupC || '', chip)
            + list(L.ObjectionsLabel || '', 'objections', 'noObjection', objs, L.AddObjection || '', L.NoObjection || '', (prefilled.objections || []).length > 0 && !state.reportId)
            + list(L.Requests || '', 'requests', 'noRequest', reqs, L.AddRequest || '', L.NoRequest || '', (prefilled.requests || []).length > 0 && !state.reportId)
            + sample + next + '</div>';
    }

    // ── follow-up + safety ───────────────────────────────────────────────────────────────────────────────────
    function safetyHtml() {
        const f = state.body.followUp;
        const sa = state.body.safety;
        const q = (key, text, yesText, tone) => {
            const v = sa[key];
            return '<div class="vr-q' + (v === null || v === undefined ? ' open' : '') + '"><div class="vr-q-head"><span class="vr-q-t">' + esc(text) + req() + '</span>'
                + seg('safety.' + key, v, [{ value: true, label: L.YesOption || '' }, { value: false, label: L.NoOption || '' }]) + '</div>'
                + (v === true ? '<div class="vr-alert vr-' + tone + '"><i class="bx ' + (tone === 'da' ? 'bx-error' : 'bx-info-circle') + '"></i>' + esc(yesText) + '</div>' : '') + '</div>';
        };
        return card('<div class="vr-h5">' + esc(L.StepSafety || '') + '</div>'
            + '<div class="vr-grid"><label class="vr-check"><input type="checkbox" data-path="followUp.required" data-type="bool"' + (f.required ? ' checked' : '') + '>' + esc(L.FollowUpRequired || '') + '</label>'
            + (f.required ? '<label class="vr-field">' + fieldLabel(L.FollowUpDate || '') + '<input type="date" class="form-control" data-path="followUp.suggestedDate" value="' + esc(f.suggestedDate || '') + '"></label>' : '') + '</div>'
            + (f.required ? '<label class="vr-field">' + fieldLabel(L.FollowUpNote || '') + '<input class="form-control" maxlength="2000" data-path="followUp.note" value="' + esc(f.note || '') + '"></label>' : '')
            + q('adverseEvent', L.AdverseEventQ || '', L.AdverseEventYes || '', 'da')
            + (sa.adverseEvent === true ? '<label class="vr-field">' + fieldLabel(L.AdverseEventNote || '', true) + '<textarea class="form-control" rows="2" maxlength="2000" data-path="safety.adverseEventNote">' + esc(sa.adverseEventNote || '') + '</textarea></label>' : '')
            + q('offLabelRequest', L.OffLabelQ || '', L.OffLabelYes || '', 'in'));
    }

    // ── the doctor profile card ──────────────────────────────────────────────────────────────────────────────
    function doctorHtml() {
        const d = state.body.doctor || {};
        const profile = state.prefill.profile || {};
        const tiles = [
            ['adoptionTypologyCode', S.typology, L.Typology, 'bx-trending-up'],
            ['communicationStyleCode', S.style, L.CommunicationStyle, 'bx-conversation'],
            ['kolLevelCode', S.kol, L.KolLevel, 'bx-network-chart']
        ];
        let changes = 0;
        const tileHtml = tiles.map(([key, set, title, icon]) => {
            const recorded = profile[key] || null;
            const cur = d[key] || recorded;
            const changed = !!d[key] && d[key] !== recorded;
            if (changed) { changes++; }
            return '<div class="vr-tile' + (changed ? ' changed' : '') + '"><div class="vr-tile-head"><i class="bx ' + icon + '"></i><span>' + esc(title || '') + '</span>'
                + (changed ? '<span class="vr-tag vr-wa">' + esc(L.ChangedTag || '') + '</span>' : '') + '</div><div class="vr-chips-row">'
                + opts(set).map(o => '<button type="button" class="vr-chipbtn' + (cur === o.code ? ' active' : '') + '" data-act="prof" data-key="' + key + '" data-value="' + esc(o.code) + '">' + esc(o.label)
                    + (recorded === o.code ? '<span class="vr-on-record">· ' + esc(L.OnRecord || '') + '</span>' : '') + '</button>').join('') + '</div>' + unavailable(set) + '</div>';
        }).join('');
        const due = state.ctx.totalDue;
        const updating = due || state.updateTotal;
        const total = '<div class="vr-tile"><div class="vr-tile-head"><i class="bx bx-group"></i><span>' + esc(L.TotalPatients || '') + (due ? req() : '') + '</span></div>'
            + (updating ? '<input type="number" min="0" max="100000" class="form-control vr-num" data-path="doctor.totalMonthlyPatients" data-type="number" value="' + esc(d.totalMonthlyPatients == null ? '' : d.totalMonthlyPatients)
                + '" placeholder="' + esc(profile.totalMonthlyPatients == null ? '' : profile.totalMonthlyPatients) + '">'
                : '<div class="vr-last"><span>' + esc(fmt(L.LastTotal || '{0} {1}', profile.totalMonthlyPatients == null ? '—' : profile.totalMonthlyPatients, profile.totalPatientsAsOf ? String(profile.totalPatientsAsOf).slice(0, 10) : '—')) + '</span>'
                    + (editable() ? '<button type="button" class="vr-same" data-act="total-update">' + esc(L.UpdateNumbers || '') + '</button>' : '') + '</div>')
            + '<span class="vr-small vr-muted">' + esc(L.TotalHint || '') + '</span>' + (C.totalRule(state.body, state.prefill, state.ctx).some(r => !r.asked) ? '<div class="vr-alert vr-da"><i class="bx bx-error"></i>' + esc(L.TotalBelowProducts || '') + '</div>' : '') + '</div>';
        const ins = state.insight || null;
        const avail = ins ? (ins.availability || []).map(a => (L['Day_' + String(a.weekday || '').toLowerCase()] || a.weekday) + ' ' + (a.preferredVisitStartTime || a.startTime) + '–' + (a.preferredVisitEndTime || a.endTime)).join(' · ') : '';
        const channel = ins ? ((ins.preferences || []).filter(p => /channel/i.test(p.preferenceType || '')).sort((a, b) => (a.priority || 0) - (b.priority || 0))[0] || null) : null;
        const read = perms.insights ? '<div class="vr-tile"><div class="vr-tile-head"><i class="bx bx-calendar"></i><span>' + esc(L.BestTime || '') + '</span></div><span>' + esc(avail || '—') + '</span>'
            + '<div class="vr-tile-head"><i class="bx bx-message-rounded-dots"></i><span>' + esc(L.PreferredChannel || '') + '</span></div><span>' + esc(channel ? (channel.channel || channel.preferenceValue) : '—') + '</span>'
            + '<span class="vr-small vr-muted">' + esc(L.ReadOnlyHere || '') + '</span></div>' : '';
        const initials = String((state.visit && (state.visit.contactDisplayName || state.visit.targetDisplayName)) || '').replace(/^(Prof\.|Doç\.|Uzm\.|Dr\.)\s*/g, '').replace(/^(Dr\.)\s*/, '')
            .split(/\s+/).filter(Boolean).map(z => z[0]).join('').slice(0, 2).toUpperCase();
        const history = ins && (ins.history || []).length ? '<button type="button" class="vr-link" data-act="history"><i class="bx bx-history"></i>' + esc(state.history ? (L.HideHistory || '') : (L.ShowHistory || '')) + '</button>'
            + (state.history ? '<ul class="vr-history">' + ins.history.slice(0, 20).map(h => '<li><strong>' + esc(L['Hist_' + h.field] || h.field) + '</strong>: ' + esc(h.oldValue || '—') + ' → ' + esc(h.newValue || '—')
                + ' <span class="vr-muted">· ' + esc(String(h.at || '').slice(0, 10)) + ' · ' + esc(String(h.source || '').indexOf('report:') === 0 ? (L.SourceReport || '') : (L.SourceManual || '')) + '</span></li>').join('') + '</ul>' : '') : '';
        return card('<div class="vr-prof-head"><span class="vr-avatar">' + esc(initials || '—') + '</span><div class="vr-grow"><div class="vr-h5">' + esc(L.StepDoctor || '') + '</div><div class="vr-small vr-muted">' + esc(L.ProfileHint || '') + '</div></div>'
            + '<span class="vr-tag ' + (changes ? 'vr-wa' : 'vr-se') + '">' + esc(changes ? fmt(L.ChangeCount || '{0}', changes) : (L.NoChange || '')) + '</span></div>'
            + '<div class="vr-tiles">' + tileHtml + total + read + '</div>' + history);
    }

    // ── summary ──────────────────────────────────────────────────────────────────────────────────────────────
    function summaryHtml() {
        const b = state.body;
        const p = state.prefill;
        const miss = missingRows();
        const span = C.minutesBetween(p.startedAt, p.finishedAt);
        const gen = [b.general.visitKindCode ? L['Kind_' + b.general.visitKindCode] : null, p.startedAt ? clock(p.startedAt) + '–' + clock(p.finishedAt) + (span !== null ? ' (' + fmt(L.MinutesShort || '{0}', span) + ')' : '') : null,
            ((state.execution && state.execution.coVisitors) || []).length ? fmt(L.JointWith || '{0}', state.execution.coVisitors.map(c => c.displayName).join(', ')) : null].filter(Boolean).join(' · ');
        const list = stepList();
        const rows = b.productSections.map(s => {
            const tags = [];
            if (s.visitSuccessCode) { tags.push(['vr-wa', label(S.visitSuccess, s.visitSuccessCode)]); }
            if (s.interestCode) { tags.push([s.interestCode === 'interested' ? 'vr-ok' : s.interestCode === 'not_interested' ? 'vr-da' : 'vr-se', label(S.interest, s.interestCode)]); }
            if (s.adoptionStageCode) { tags.push(['vr-p', label(S.adoptionStage, s.adoptionStageCode)]); }
            if ((s.competitors || []).length) { tags.push(['vr-se', fmt(L.CompetitorTag || '{0}', s.competitors.map(c => c.nameSnapshot).join(', '))]); }
            if (s.sample) { tags.push(['vr-in', fmt(L.SampleTag || '{0}', s.sample.quantity || 1)]); }
            const n = miss.filter(m => m.productId === s.productId).length;
            const at = list.findIndex(x => x.productId === s.productId);
            return '<div class="vr-sumrow"><div class="vr-grow"><div class="vr-sum-n">' + esc(nameOf(s.productId)) + '</div><div class="vr-chips-row">' + tags.map(t => '<span class="vr-tag ' + t[0] + '">' + esc(t[1]) + '</span>').join('') + '</div></div>'
                + '<span class="vr-tag ' + (n ? 'vr-da' : 'vr-ok') + '">' + esc(n ? fmt(L.StepMissing || '{0}', n) : (L.GroupDone || '')) + '</span>'
                + '<button type="button" class="btn btn-sm btn-outline-primary" data-act="step" data-step="' + at + '">' + esc(L.EditButton || '') + '</button></div>';
        }).join('');
        const safe = [['AdverseEventShort', b.safety.adverseEvent], ['OffLabelShort', b.safety.offLabelRequest]].map(([k, v]) => '<span class="vr-tag ' + (v === null || v === undefined ? 'vr-da' : 'vr-se') + '">'
            + esc(fmt(L[k] || '{0}', v === true ? L.YesOption : v === false ? L.NoOption : L.NoAnswer)) + '</span>').join('');
        const missList = miss.length ? '<div class="vr-misslist"><div class="vr-h6">' + esc(fmt(L.MissingTitle || '{0}', miss.length)) + '</div><ul>' + miss.map(m => '<li><button type="button" class="vr-link" data-act="goto" data-key="'
            + esc(C.stepOf(m, p.reportKind)) + '">' + esc(fieldText(m)) + '</button></li>').join('') + '</ul></div>'
            : '<div class="vr-alert vr-ok"><i class="bx bx-check-circle"></i>' + esc(L.ReadyText || '') + '</div>';
        const server = state.serverMissing && state.serverMissing.length ? '<div class="vr-alert vr-da"><i class="bx bx-error"></i>' + esc(fmt(L.ServerMissing || '{0}', state.serverMissing.length)) + '</div>' : '';
        return card('<div class="vr-h5">' + esc(L.SummaryTitle || '') + '</div><div class="vr-small">' + esc(gen) + '</div>' + rows + '<div class="vr-chips-row">' + safe + '</div>' + server + missList)
            + amendmentsHtml();
    }

    function amendmentsHtml() {
        const rows = C.changesOf(state.amendments);
        if (!rows.length) { return ''; }
        return card('<div class="vr-h6">' + esc(L.AmendmentsTitle || '') + '</div>' + rows.map(a => '<div class="vr-amend"><div class="vr-small"><strong>' + esc(String(a.at || '').slice(0, 16).replace('T', ' ')) + '</strong> · ' + esc(a.reason || '') + '</div><ul>'
            + a.changes.map(c => '<li><code>' + esc(c.path) + '</code>: ' + esc(c.old) + ' → ' + esc(c.new) + '</li>').join('') + '</ul></div>').join(''));
    }

    // ── state changes ────────────────────────────────────────────────────────────────────────────────────────
    function setPath(path, value) {
        const parts = path.split('.');
        let o = state.body;
        for (let i = 0; i < parts.length - 1; i++) {
            const k = parts[i];
            if (o[k] === null || o[k] === undefined) { o[k] = /^\d+$/.test(parts[i + 1]) ? [] : {}; }
            o = o[k];
        }
        o[parts[parts.length - 1]] = value;
    }

    function getPath(path) {
        return path.split('.').reduce((o, k) => (o == null ? undefined : o[k]), state.body);
    }

    function changed(full) {
        state.serverMissing = null;
        if (full) { render(); } else { renderLight(); }
        schedule();
    }

    /** The draft saves itself 1.5 s after the last change (only an unsubmitted report; a 409 reads it again). */
    function schedule() {
        if (state.mode !== 'edit' || !perms.record) { return; }
        state.dirty = true;
        if (state.timer) { clearTimeout(state.timer); }
        state.timer = setTimeout(saveDraft, 1500);
    }

    async function saveDraft() {
        state.timer = null;
        if (state.mode !== 'edit' || !perms.record || state.saving) { return; }
        state.saving = true;
        renderChips();
        const r = await api('PUT', '/draft', C.requestBody(plannedVisitId, state.body, state.version));
        state.saving = false;
        if (r.ok && r.data) {
            state.version = r.data.version;
            state.reportId = r.data.visitReportId || state.reportId;
            state.dirty = false;
            state.savedAt = clock(new Date(Date.now()).toISOString());
        } else if (r.status === 409) {
            toast(L.DraftConflict || '', 'warning');
            await reload();
            return;
        } else {
            toast(C.errorText(r.errors, L), 'error');
        }
        renderChips();
    }

    async function reload() {
        const step = state.step;
        state.phase = 'loading';
        await load();
        state.step = step;
        render();
    }

    async function submit() {
        if (state.saving) { return; }
        const miss = missingRows();
        if (miss.length) {
            toast(fmt(L.MissingToast || '{0}', miss.length, miss.slice(0, 3).map(fieldText).join(', ')), 'warning');
            goTo(C.stepOf(miss[0], state.prefill.reportKind));
            return;
        }
        if (state.timer) { clearTimeout(state.timer); state.timer = null; }
        state.saving = true;
        renderBar();
        const r = state.mode === 'fix'
            ? await api('POST', '/reports/' + encodeURIComponent(state.reportId) + '/amend', C.amendBody(state.reason, plannedVisitId, state.body, state.version))
            : await api('POST', '/submit', C.requestBody(plannedVisitId, state.body, state.version));
        state.saving = false;
        if (r.ok && r.data) {
            state.version = r.data.version;
            state.reportId = r.data.visitReportId || state.reportId;
            state.status = r.data.reportStatus || 'submitted';
            state.warnings = r.data.warnings || [];
            const wasFix = state.mode === 'fix';
            await loadReport();
            if (!state.submittedAt) { state.submittedAt = new Date(Date.now()).toISOString(); }
            state.mode = 'ro';
            state.reason = '';
            state.sent = !wasFix;
            toast(wasFix ? (L.FixSaved || '') : (L.SubmitSaved || ''), 'success');
            render();
            return;
        }
        if (r.status === 400 && (r.errors || []).indexOf('report_incomplete') > -1) {
            state.serverMissing = (r.data && r.data.missing) || [];
            toast(fmt(L.ServerMissing || '{0}', state.serverMissing.length), 'warning');
            goTo('summary');
            return;
        }
        if (r.status === 409 && (r.errors || []).indexOf('visit_report_concurrency_conflict') > -1) {
            toast(L.DraftConflict || '', 'warning');
            await reload();
            return;
        }
        toast(C.errorText(r.errors, L), 'error');
        renderBar();
    }

    function goTo(key) {
        const at = stepList().findIndex(s => s.key === key);
        state.step = at > -1 ? at : stepList().length - 1;
        render();
        if (window.scrollTo) { try { window.scrollTo(0, 0); } catch (e) { /* jsdom */ } }
    }

    async function propose(i) {
        const s = state.body.productSections[i];
        const p = state.proposal[i] || {};
        const name = String(p.name || '').trim(), company = String(p.company || '').trim();
        if (!name || !company) { toast(L.ProposalNeedsBoth || '', 'warning'); return; }
        let answer = null;
        if (perms.propose) {
            const r = await api('POST', '/competitors/proposals', { name: name, company: company, productId: s.productId, reportId: state.reportId || null });
            answer = r.ok ? r.data : null;
            if (!r.ok) { toast(L.ProposalLater || '', 'warning'); }
        }
        C.addProposal(s, name, company, answer);
        state.proposal[i] = { open: false, name: '', company: '', hits: [] };
        changed(true);
    }

    async function searchProposal(i) {
        const s = state.body.productSections[i];
        const p = state.proposal[i];
        if (!perms.lookup || !p || String(p.name || '').trim().length < 3) { if (p) { p.hits = []; } return; }
        const r = await api('GET', '/competitors/lookup?productId=' + encodeURIComponent(s.productId) + '&q=' + encodeURIComponent(p.name.trim()));
        p.hits = r.ok ? items(r.data).filter(h => !(s.competitors || []).some(c => c.competitorProductId === h.id)).slice(0, 5) : [];
        renderBody();
    }

    // ── events ───────────────────────────────────────────────────────────────────────────────────────────────
    root.addEventListener('click', e => {
        const t = e.target && e.target.closest ? e.target.closest('[data-act]') : null;
        if (!t) { return; }
        const act = t.getAttribute('data-act');
        const path = t.getAttribute('data-path');
        const i = parseInt(t.getAttribute('data-p'), 10);
        const s = isNaN(i) ? null : state.body.productSections[i];
        switch (act) {
            case 'step': state.step = parseInt(t.getAttribute('data-step'), 10) || 0; render(); break;
            case 'prev': state.step = Math.max(0, state.step - 1); render(); break;
            case 'next': state.step = Math.min(stepList().length - 1, state.step + 1); render(); break;
            case 'goto': goTo(t.getAttribute('data-key')); break;
            case 'save-draft': saveDraft(); break;
            case 'submit': submit(); break;
            case 'set': if (!editable()) { return; } setPath(path, t.getAttribute('data-value')); changed(true); break;
            case 'bool': if (!editable()) { return; } setPath(path, t.getAttribute('data-value') === 'true'); changed(true); break;
            case 'toggle': {
                if (!editable()) { return; }
                const on = !getPath(path);
                setPath(path, on);
                if (/\.potential\.noPatientsInIndications$/.test(path)) {
                    // "sees no patients in these indications" and rows exclude each other (the server's shape rule)
                    const sec = state.body.productSections[parseInt(path.split('.')[1], 10)];
                    sec.potential.rows = on ? [] : (sec.potential.rows || []);
                    if (!on) { sec.potential.noPatientsInIndications = null; }
                }
                changed(true);
                break;
            }
            case 'met': break; // handled on change
            case 'ind': break; // handled on change
            case 'ind-same': {
                const k = parseInt(t.getAttribute('data-k'), 10);
                const row = indicationRows(s.productId).items[k];
                const stored = C.storedPotential(state.prefill, s.productId);
                const before = stored && (stored.indicationRows || []).find(x => x.indicationNodeId === row.indicationNodeId && (x.profileNodeId || null) === (row.profileNodeId || null));
                const at = (s.potential.rows || []).findIndex(x => x.indicationNodeId === row.indicationNodeId && (x.profileNodeId || null) === (row.profileNodeId || null));
                if (before && at > -1) { s.potential.rows[at].monthlyPatients = before.monthlyPatients; changed(true); }
                break;
            }
            case 'pot-update': {
                const stored = C.storedPotential(state.prefill, s.productId);
                s.potential = { rows: copyRows(stored), noPatientsInIndications: stored && stored.noPatientsInIndications ? true : null, updated: true };
                changed(true);
                break;
            }
            case 'total-update': state.updateTotal = true; render(); break;
            case 'comp': {
                const option = competitorOptions(s.productId).items.find(o => o.id === t.getAttribute('data-id'));
                if (option && editable()) { C.toggleCompetitor(s, option); changed(true); }
                break;
            }
            case 'comp-rm': if (editable()) { s.competitors.splice(parseInt(t.getAttribute('data-c'), 10), 1); changed(true); } break;
            case 'adv': if (editable()) { C.cycleAdvantage(s.competitors[parseInt(t.getAttribute('data-c'), 10)], t.getAttribute('data-code')); changed(true); } break;
            case 'prop-open': state.proposal[i] = Object.assign({ name: '', company: '', hits: [] }, state.proposal[i] || {}, { open: !(state.proposal[i] && state.proposal[i].open) }); render(); break;
            case 'prop-add': propose(i); break;
            case 'prop-pick': {
                const hit = (state.proposal[i].hits || []).find(h => h.id === t.getAttribute('data-id'));
                if (hit) { C.toggleCompetitor(s, hit); state.proposal[i] = { open: false, name: '', company: '', hits: [] }; changed(true); }
                break;
            }
            case 'row-add': {
                const list = getPath(path) || [];
                list.push(/objections$/.test(path) ? { categoryCode: null, objectionResponseId: null, note: null, resolved: null } : { typeCode: null, dueDate: null, note: null });
                setPath(path, list);
                setPath(path.replace(/objections$/, 'noObjection').replace(/requests$/, 'noRequest'), false);
                changed(true);
                break;
            }
            case 'row-rm': { const list = getPath(path) || []; list.splice(parseInt(t.getAttribute('data-k'), 10), 1); changed(true); break; }
            case 'use-sug': {
                const key = C.suggestionKey(s.commitmentCode, s.adoptionStageCode) || s.nextMessageSuggestion;
                s.nextMessage = C.suggestionText(key, L, code => label(S.adoptionStage, code));
                changed(true);
                break;
            }
            case 'prof': {
                const key = t.getAttribute('data-key');
                const v = t.getAttribute('data-value');
                state.body.doctor = state.body.doctor || {};
                const recorded = (state.prefill.profile || {})[key] || null;
                state.body.doctor[key] = (state.body.doctor[key] || recorded) === v ? null : v; // a second tap withdraws the suggestion
                changed(true);
                break;
            }
            case 'history': state.history = !state.history; render(); break;
            case 'add-product': addProduct(); break;
            case 'unlock': state.mode = 'unlocked'; render(); break;
            case 'fix': state.mode = 'fix'; state.step = stepList().length - 1; render(); break;
            case 'view-report': state.sent = false; render(); break;
        }
    });

    function copyRows(stored) {
        return stored ? (stored.indicationRows || []).map(r => ({ indicationNodeId: r.indicationNodeId, profileNodeId: r.profileNodeId || null, monthlyPatients: r.monthlyPatients })) : [];
    }

    async function addProduct() {
        const id = state.addProduct || ((el('vr-add-product') || {}).value);
        if (!id || state.body.productSections.some(s => s.productId === id)) { return; }
        state.body.productSections.push(C.emptySection(id, true));
        names();
        if (perms.gskus) { await loadGskus(id); }
        toast(fmt(L.UnplannedAdded || '{0}', nameOf(id)), 'success');
        state.step = stepList().findIndex(s => s.productId === id);
        changed(true);
    }

    function valueOf(t) {
        const type = t.getAttribute('data-type');
        if (type === 'number') { const n = t.value === '' ? null : Number(t.value); return n === null || isNaN(n) ? null : n; }
        if (type === 'bool') { return !!t.checked; }
        return t.value === '' ? null : t.value;
    }

    root.addEventListener('input', e => {
        const t = e.target;
        if (!t || !t.getAttribute) { return; }
        if (t.getAttribute('data-reason')) { state.reason = t.value; renderLight(); return; }
        if (t.getAttribute('data-local') === 'prop') {
            const i = parseInt(t.getAttribute('data-p'), 10);
            state.proposal[i] = state.proposal[i] || { open: true, name: '', company: '', hits: [] };
            state.proposal[i][t.getAttribute('data-k')] = t.value;
            return;
        }
        const path = t.getAttribute('data-path');
        if (!path || !editable() || t.tagName === 'SELECT' || t.type === 'checkbox') { return; }
        setPath(path, valueOf(t));
        changed(false);
    });

    root.addEventListener('change', e => {
        const t = e.target;
        if (!t || !t.getAttribute) { return; }
        if (t.getAttribute('data-local') === 'addProduct') { state.addProduct = t.value; return; }
        if (t.getAttribute('data-local') === 'prop') {
            const i = parseInt(t.getAttribute('data-p'), 10);
            if (t.getAttribute('data-k') === 'name') { searchProposal(i); }
            return;
        }
        if (!editable()) { return; }
        const act = t.getAttribute('data-act') || t.getAttribute('data-act-change');
        if (act === 'met') {
            state.body.general.metContactId = t.checked ? state.prefill.contactId : null;
            changed(true);
            return;
        }
        if (act === 'ind') {
            const i = parseInt(t.getAttribute('data-p'), 10), k = parseInt(t.getAttribute('data-k'), 10);
            const s = state.body.productSections[i];
            const row = indicationRows(s.productId).items[k];
            s.potential = s.potential || { rows: [], noPatientsInIndications: null, updated: false };
            const at = s.potential.rows.findIndex(x => x.indicationNodeId === row.indicationNodeId && (x.profileNodeId || null) === (row.profileNodeId || null));
            if (at > -1) { s.potential.rows.splice(at, 1); }
            else { s.potential.rows.push({ indicationNodeId: row.indicationNodeId, profileNodeId: row.profileNodeId || null, monthlyPatients: null }); s.potential.noPatientsInIndications = null; }
            changed(true);
            return;
        }
        if (act === 'sample') {
            const s = state.body.productSections[parseInt(t.getAttribute('data-p'), 10)];
            if (t.value === 'none') { s.sample = null; s.noSample = true; }
            else if (t.value) { s.sample = { gskuId: t.value, quantity: (s.sample && s.sample.quantity) || 1, lot: (s.sample && s.sample.lot) || null }; s.noSample = false; }
            else { s.sample = null; s.noSample = false; }
            changed(true);
            return;
        }
        const path = t.getAttribute('data-path');
        if (!path) { return; }
        setPath(path, valueOf(t));
        changed(true);
    });

    load();
}());
