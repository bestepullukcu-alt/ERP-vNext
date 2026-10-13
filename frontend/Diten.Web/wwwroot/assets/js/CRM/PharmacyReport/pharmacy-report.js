/**
 * WP-VW-W4 (W4-WEB-b, §2.5) — the pharmacy visit report (mockup 03-rapor, pharmacy steps). Every rule lives in
 * report-pharmacy-core.js (pure, tested in Node); every text comes from window.PharmacyReportL10n (7 languages), the BRD
 * option labels (the pre-fill's optionSets) or MDM / CRM (SKU, competitor and doctor names). Dates / numbers in the
 * APPLICATION language through VisitPlanningFormat; every data name isolated (bidi) for RTL. Calls go to the same-origin
 * proxy /CRM/PharmacyReport/api/*.
 *   Steps: General (met role, note) · Stock and shelf (a stock level per SKU, near expiry / return issue, shelf position,
 *   material / stand, the shelf PHOTO) · Order and sales (the pre-order is PASSIVE — "soon", W6 —; a TREND per SKU is
 *   required, boxes / month optional) · Competitor and recommendation (MDM competitors or a proposal, advantages,
 *   recommendation level, linked doctors, staff training and requests) · Safety (adverse event) · Summary (the missing list).
 *   Draft / submit / the 60-minute re-submit; after it the reasoned correction (amend).
 */
(function (window, document) {
    'use strict';

    const C = window.ReportPharmacyCore;
    const F = window.VisitPlanningFormat;
    const L = window.PharmacyReportL10n || {};
    const root = document.getElementById('ph-root');
    if (!C || !F || !root) { return; }

    const base = root.getAttribute('data-api-base') || '/CRM/PharmacyReport/api';
    const planId = root.getAttribute('data-planned-visit-id') || '';
    const perms = {
        record: root.getAttribute('data-can-record') === 'true',
        amend: root.getAttribute('data-can-amend') === 'true',
        gskus: root.getAttribute('data-can-read-gskus') === 'true',
        contacts: root.getAttribute('data-can-read-contacts') === 'true'
    };
    const lang = (document.documentElement && document.documentElement.lang) || 'tr';

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L[key] || '', ...args);
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    const setText = (id, t) => { const n = el(id); if (n) { n.textContent = t; } };
    const toast = (msg, type) => { if (window.showToast) { window.showToast(msg, type || 'success'); } };
    const short = id => String(id || '').slice(0, 8);
    const hm = iso => { try { return new Intl.DateTimeFormat(F.dateCulture(), { hour: '2-digit', minute: '2-digit' }).format(new Date(iso)); } catch (e) { return '—'; } };
    const visitMinutes = p => (p && p.startedAt && p.finishedAt ? Math.max(0, Math.round((Date.parse(p.finishedAt) - Date.parse(p.startedAt)) / 60000)) : null);
    /** "11:31 – 11:52 · 21 dk" (the mockup's server-time line). */
    const visitTimes = p => (p && p.startedAt ? hm(p.startedAt) + (p.finishedAt ? ' – ' + hm(p.finishedAt) : '') + (visitMinutes(p) != null ? ' · ' + T('Minutes', F.number(visitMinutes(p), 0)) : '') : '—');

    // a multipart body (the shelf photo) goes as it is: the browser writes its boundary
    const isForm = b => typeof window.FormData === 'function' && b instanceof window.FormData;
    const send = (method, path, body, headers) => fetch(base + path, {
        method: method,
        credentials: 'same-origin',
        headers: Object.assign({ Accept: 'application/json' }, body && !isForm(body) ? { 'Content-Type': 'application/json' } : {}, headers || {}),
        body: body ? (isForm(body) ? body : JSON.stringify(body)) : undefined
    }).then(r => r.text().then(t => {
        let b = null;
        try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; }
        return { ok: r.ok, status: r.status, body: b };
    })).catch(() => ({ ok: false, status: 0, body: null }));
    const data = r => (r && r.ok && r.body ? (r.body.data !== undefined ? r.body.data : r.body) : null);
    const errText = r => L[C.errorKey(r)] || L.Err_generic || '';

    // ── state ────────────────────────────────────────────────────────────────────────────────────────────────
    const state = {
        prefill: null, body: null, version: 0, reportId: null, status: null, step: 'general', mode: 'draft',
        editing: false, dirty: false, savedAt: null, serverMissing: null, tried: false,
        gskus: [], gskuNames: {}, gskuError: null, gskuResults: [], contactNames: {}, contactResults: [],
        proposalOpen: false, proposalErrors: {}, fixReason: '', photoUrl: null, busy: false
    };

    const options = set => C.optionsOf(state.prefill, set);
    const optLabel = (set, code) => { const o = options(set).find(x => x.code === code); return o ? C.optionLabel(o, lang) : (code || ''); };
    const gskuName = id => state.gskuNames[id] || short(id);
    const ctx = () => ({ competitorsListed: C.competitorsListed(state.prefill) });
    const missingNow = () => state.serverMissing || C.missing(state.body, ctx());
    const writable = () => perms.record && (state.mode === 'draft' || state.editing);
    const fixing = () => state.mode === 'fix';

    // ── load ─────────────────────────────────────────────────────────────────────────────────────────────────
    function load() {
        return send('GET', '/' + encodeURIComponent(planId) + '/prefill').then(r => {
            const d = data(r);
            if (!d) { setText('ph-load-error', errText(r)); show('ph-load-error', true); show('ph-main', false); return; }
            if (d.reportKind && d.reportKind !== C.KIND) { setText('ph-load-error', L.NotPharmacy || ''); show('ph-load-error', true); show('ph-main', false); return; }
            show('ph-load-error', false);
            show('ph-main', true);
            state.prefill = d;
            state.body = C.draftOf(d);
            state.version = d.version || 0;
            state.reportId = d.visitReportId || null;
            state.status = d.reportStatus || null;
            state.mode = C.modeOf(d) === 'resubmit' ? 'sent' : 'draft';
            if (state.body.pharmacy.shelf.photoEvidenceId) { state.photoUrl = base + '/' + encodeURIComponent(planId) + '/shelf-photo'; }
            render();
            return loadGskus();
        });
    }

    function loadGskus() {
        if (!perms.gskus) { state.gskuError = L.GskusNoPermission || ''; render(); return Promise.resolve(); }
        return send('GET', '/lookups/gskus').then(r => {
            const d = data(r);
            if (!Array.isArray(d)) { state.gskuError = errText(r); render(); return; }
            state.gskus = d;
            d.forEach(g => { state.gskuNames[g.id] = (g.productName ? g.productName + ' · ' : '') + (g.code || short(g.id)) + (g.pack ? ' (' + g.pack + ')' : ''); });
            if (writable()) {
                const before = JSON.stringify(state.body.pharmacy);
                state.body.pharmacy = C.seedRows(state.body.pharmacy, d, C.productIdsOf(state.prefill));
                if (JSON.stringify(state.body.pharmacy) !== before) { state.dirty = true; }
            }
            render();
        });
    }

    // ── render ───────────────────────────────────────────────────────────────────────────────────────────────
    const seg = (act, items, current, extra, disabled) => '<div class="ph-seg" role="group">' + items.map(o =>
        '<button type="button" class="ph-seg-btn' + (o.code === current ? ' active' : '') + '" data-act="' + act + '" data-val="' + esc(o.code) + '"'
        + (extra || '') + ' aria-pressed="' + (o.code === current) + '"' + (disabled ? ' disabled' : '') + '>' + esc(o.label) + '</button>').join('') + '</div>';
    const brdSeg = (act, set, current, extra) => {
        const opts = options(set).map(o => ({ code: o.code, label: C.optionLabel(o, lang) }));
        return opts.length ? seg(act, opts, current, extra, !writable()) : '<span class="ph-sub">' + esc(L.OptionsUnavailable) + '</span>';
    };
    const req = '<span class="ph-req">*</span>';
    const dis = () => (writable() ? '' : ' disabled');

    function render() {
        if (!state.prefill) { return; }
        renderHead();
        renderSteps();
        C.STEPS.forEach(s => show('ph-pane-' + s, s === state.step));
        ({ general: renderGeneral, stock: renderStock, order: renderOrder, market: renderMarket, safety: renderSafety, summary: renderSummary })[state.step]();
        renderBar();
    }

    function renderHead() {
        const p = state.prefill;
        // the visit day ("13 Eki · Pazartesi"); the account name is not in the pre-fill (see the report's open items)
        setText('ph-sub', p.startedAt ? F.dayMonth(p.startedAt) + ' · ' + F.weekdayLong(p.startedAt) : '');
        const miss = missingNow().length;
        const chip = el('ph-chip-valid');
        chip.className = 'ph-chip ' + (miss ? 'ph-tone-danger' : 'ph-tone-success');
        chip.innerHTML = '<i class="bx ' + (miss ? 'bx-error-circle' : 'bx-check-circle') + '"></i> ' + esc(miss ? T('MissingCount', miss) : L.ReadyToSend);
        setText('ph-chip-saved', state.savedAt ? T('SavedAt', F.dateTime(state.savedAt)) : (state.dirty ? L.Unsaved : (state.reportId ? L.DraftStored : L.NotSaved)));
        // the sent banner + the 60-minute rule
        const sent = state.mode === 'sent' && !state.editing;
        show('ph-sent', sent);
        if (sent) {
            setText('ph-sent-text', state.status === 'amended' ? L.SentAmended : L.SentText);
            show('ph-unlock', perms.record || perms.amend);
        }
        show('ph-fix', fixing());
        show('ph-readonly', !perms.record);
        const fe = el('ph-fix-err');
        if (fe) { const k = state.tried && fixing() ? C.reasonError(state.fixReason) : null; fe.textContent = k ? (L[k] || '') : ''; fe.classList.toggle('d-none', !k); }
        setText('ph-fix-count', F.ratio(String(state.fixReason || '').trim().length, C.MAX_REASON));
    }

    function renderSteps() {
        // the mockup's marks: a complete step = ✓ "tamam"; a step with missing answers = its number + "n eksik" (red);
        // the summary carries the total
        const all = missingNow();
        const counts = C.missingByStep(all);
        el('ph-steps').innerHTML = C.STEPS.map((s, i) => {
            const n = s === 'summary' ? all.length : counts[s];
            const cur = s === state.step;
            const done = !n && !cur && s !== 'summary';
            return '<button type="button" role="tab" class="ph-step' + (cur ? ' active' : '') + (n ? ' ph-step-miss' : '') + (done ? ' ph-step-done' : '') + '" data-act="step" data-step="' + s + '" aria-selected="' + cur + '">'
                + '<span class="ph-step-n">' + (done ? '<i class="bx bx-check"></i>' : String(i + 1)) + '</span>'
                + '<span class="ph-col"><span class="ph-step-l">' + esc(L['Step_' + s]) + '</span><span class="ph-step-s">' + esc(n ? T('StepMissing', n) : (done ? (L.StepOk || '') : '')) + '</span></span></button>';
        }).join('');
    }

    function renderGeneral() {
        const g = state.body.general;
        const p = state.prefill;
        const kindKnown = g.visitKindCode === 'in_person' || g.visitKindCode === 'remote';
        el('ph-general').innerHTML = '<div class="ph-grid">'
            + '<div class="ph-field"><span class="ph-label">' + esc(L.Outcome) + '</span><span class="ph-done"><i class="bx bx-check"></i> ' + esc(L.OutcomeDone) + '</span><span class="ph-sub">' + esc(L.OutcomeHint) + '</span></div>'
            + '<div class="ph-field"><span class="ph-label">' + esc(L.ActualTimes) + '</span><span class="ph-locked"><i class="bx bx-lock-alt"></i> ' + esc(visitTimes(p)) + '</span><span class="ph-sub">' + esc(L.ServerTime) + '</span></div>'
            // the visit type: pre-filled from the W3 execution (DESIGN-GAP-W4-report §2 "Genel: görüşülen, ziyaret türü")
            + '<div class="ph-field"><span class="ph-label">' + esc(L.VisitKind) + ' ' + req + '</span>'
            + seg('kind', [{ code: 'in_person', label: L.Kind_in_person }, { code: 'remote', label: L.Kind_remote }], g.visitKindCode, '', !writable())
            + (kindKnown || !state.tried ? '' : '<span class="ph-err">' + esc(L.Miss_visitKindCode) + '</span>') + '</div>'
            + '<div class="ph-field"><span class="ph-label">' + esc(L.MetRole) + ' ' + req + '</span>' + brdSeg('met', C.SETS.metRole, g.metRoleCode) + '</div>'
            + '</div>'
            + '<label class="ph-field"><span class="ph-label">' + esc(L.GeneralNote) + '</span><textarea class="ph-input ph-textarea" rows="3" data-act="note" maxlength="' + C.MAX_NOTE + '"' + dis() + '>' + esc(g.generalNote) + '</textarea></label>';
    }

    function gskuPicker() {
        if (!writable()) { return ''; }
        if (state.gskuError) { return '<div class="ph-alert ph-alert-info"><i class="bx bx-info-circle"></i> ' + esc(state.gskuError) + '</div>'; }
        return '<div class="ph-picker"><input type="search" class="ph-input" id="ph-gsku-q" data-act="gsku-q" placeholder="' + esc(L.GskuSearch) + '" aria-label="' + esc(L.GskuSearch) + '" autocomplete="off" />'
            + '<div class="ph-results">' + state.gskuResults.map(g => '<button type="button" class="ph-result" data-act="gsku-add" data-id="' + esc(g.id) + '"><i class="bx bx-plus"></i> ' + F.bidi(gskuName(g.id)) + '</button>').join('') + '</div></div>';
    }

    function renderStock() {
        const p = state.body.pharmacy;
        const rows = p.stock.map(r => '<div class="ph-line">'
            + '<span class="ph-line-name">' + F.bidi(gskuName(r.gskuId)) + '</span>'
            + brdSeg('stock', C.SETS.stockLevel, r.levelCode, ' data-gsku="' + esc(r.gskuId) + '"')
            + '<label class="ph-check"><input type="checkbox" data-act="flag" data-flag="nearExpiry" data-gsku="' + esc(r.gskuId) + '"' + (r.nearExpiry ? ' checked' : '') + dis() + '> ' + esc(L.NearExpiry) + '</label>'
            + '<label class="ph-check"><input type="checkbox" data-act="flag" data-flag="returnIssue" data-gsku="' + esc(r.gskuId) + '"' + (r.returnIssue ? ' checked' : '') + dis() + '> ' + esc(L.ReturnIssue) + '</label>'
            + (writable() ? '<button type="button" class="ph-x" data-act="gsku-rm" data-id="' + esc(r.gskuId) + '" aria-label="' + esc(L.RemoveRow) + '"><i class="bx bx-x"></i></button>' : '')
            + (state.tried && !r.levelCode ? '<span class="ph-err">' + esc(L.ErrStockLevel) + '</span>' : '')
            + '</div>').join('');
        const shelf = p.shelf;
        el('ph-stock').innerHTML = '<div class="ph-h">' + esc(L.StockTitle) + ' ' + req + '</div>'
            + (rows || '<div class="ph-empty">' + esc(L.NoSkus) + '</div>') + gskuPicker()
            + '<div class="ph-h">' + esc(L.ShelfTitle) + '</div>'
            + '<div class="ph-grid"><div class="ph-col ph-gap-s">' + brdSeg('shelf', C.SETS.shelf, shelf.positionCode)
            + '<label class="ph-check"><input type="checkbox" data-act="stand"' + (shelf.materialStand ? ' checked' : '') + dis() + '> ' + esc(L.MaterialStand) + '</label></div>'
            + '<div class="ph-photo' + (state.photoUrl ? ' has-photo' : '') + '">'
            + (state.photoUrl ? '<img src="' + esc(state.photoUrl) + '" alt="' + esc(L.ShelfPhoto) + '" />' : '<i class="bx bx-camera"></i><span>' + esc(L.ShelfPhoto) + '</span>')
            + (writable() ? '<button type="button" class="ph-btn ph-btn-o ph-btn-sm" data-act="photo"' + (state.busy ? ' disabled' : '') + '><i class="bx bx-camera"></i> ' + esc(state.photoUrl ? L.PhotoReplace : L.PhotoTake) + '</button>' : '')
            + '<span class="ph-sub">' + esc(L.PhotoHint) + '</span></div></div>';
    }

    function renderOrder() {
        const p = state.body.pharmacy;
        const rows = p.sales.map(r => '<div class="ph-line">'
            + '<span class="ph-line-name">' + F.bidi(gskuName(r.gskuId)) + '</span>'
            + '<input type="number" class="ph-input ph-input-num" min="0" max="' + C.MAX_BOXES + '" step="1" inputmode="numeric" data-act="boxes" data-gsku="' + esc(r.gskuId) + '" value="' + (r.boxesPerMonth == null ? '' : esc(r.boxesPerMonth)) + '" aria-label="' + esc(L.BoxesPerMonth) + '"' + dis() + '>'
            + '<span class="ph-sub">' + esc(L.BoxesPerMonth) + '</span>'
            + brdSeg('trend', C.SETS.trend, r.trendCode, ' data-gsku="' + esc(r.gskuId) + '"')
            + (state.tried && !r.trendCode ? '<span class="ph-err">' + esc(L.ErrTrend) + '</span>' : '')
            + '</div>').join('');
        el('ph-order').innerHTML = '<div class="ph-h">' + esc(L.OrderTitle) + '</div>'
            + '<div class="ph-order-box"><span class="ph-grow">' + esc(L.NoOrder) + '</span>'
            // W6: the pre-order is NOT built yet — a passive button with its "soon" badge, never a link
            + '<button type="button" class="ph-btn ph-btn-p" id="ph-order-btn" disabled aria-disabled="true" title="' + esc(L.OrderSoonHint) + '"><i class="bx bx-cart-add"></i> ' + esc(L.CreateOrder) + '</button>'
            + '<span class="ph-pill ph-tone-secondary">' + esc(L.Soon) + '</span></div>'
            + '<div class="ph-h">' + esc(L.SalesTitle) + ' ' + req + '</div><span class="ph-sub">' + esc(L.SalesHint) + '</span>'
            + (rows || '<div class="ph-empty">' + esc(L.NoSkus) + '</div>');
    }

    function competitorCard(c, i) {
        const item = C.competitorOptions(state.prefill).find(x => x.id === c.competitorProductId) || null;
        const detail = item ? [
            (item.activeIngredients || []).join(', '), [item.formCode, item.strength].filter(Boolean).join(' '),
            item.country && item.country.boxPrice != null ? F.number(item.country.boxPrice, 2) + ' ' + (item.country.currencyCode || '') : '',
            item.country && item.country.reimbursementStatusCode ? item.country.reimbursementStatusCode : ''
        ].filter(Boolean).join(' · ') : '';
        const adv = options(C.SETS.advantage).map(o => {
            const plus = c.advantageCodes.indexOf(o.code) > -1;
            const minus = c.disadvantageCodes.indexOf(o.code) > -1;
            return '<button type="button" class="ph-adv' + (plus ? ' plus' : minus ? ' minus' : '') + '" data-act="adv" data-idx="' + i + '" data-code="' + esc(o.code) + '"' + dis() + '>'
                + (plus ? '+ ' : minus ? '− ' : '') + esc(C.optionLabel(o, lang)) + '</button>';
        }).join('');
        return '<div class="ph-comp"><div class="ph-row ph-between"><strong class="ph-strong">' + F.bidi(c.nameSnapshot || short(c.competitorProductId)) + '</strong>'
            + '<span class="ph-row">' + (c.company ? '<span class="ph-sub">' + F.bidi(c.company) + '</span>' : '')
            + (c.proposed ? '<span class="ph-pill ph-tone-warning">' + esc(L.ProposedBadge) + '</span>' : '')
            + (writable() ? '<button type="button" class="ph-x" data-act="comp-rm" data-idx="' + i + '" aria-label="' + esc(L.RemoveRow) + '"><i class="bx bx-x"></i></button>' : '') + '</span></div>'
            + (detail ? '<span class="ph-sub">' + F.bidi(detail) + '</span>' : '')
            + (adv ? '<span class="ph-sub">' + esc(L.AdvantageHint) + '</span><div class="ph-chips">' + adv + '</div>' : '') + '</div>';
    }

    function renderMarket() {
        const p = state.body.pharmacy;
        const opts = C.competitorOptions(state.prefill);
        const chosen = {};
        p.competitors.forEach(c => { if (c.competitorProductId) { chosen[c.competitorProductId] = true; } });
        const listed = C.competitorsListed(state.prefill);
        const unavailable = state.prefill.competitorsUnavailable;
        const e = state.proposalErrors;
        const rec = p.recommendation;
        const tr = p.training;
        el('ph-market').innerHTML = '<div class="ph-h">' + esc(L.CompetitorTitle) + (listed ? ' ' + req : '') + '</div>'
            + (unavailable ? '<div class="ph-alert ph-alert-info"><i class="bx bx-info-circle"></i> ' + esc(L.CompetitorsUnavailable) + '</div>' : '')
            + '<div class="ph-chips">' + opts.map(o => '<button type="button" class="ph-chip-btn' + (chosen[o.id] ? ' active' : '') + '" data-act="comp" data-id="' + esc(o.id) + '" aria-pressed="' + !!chosen[o.id] + '"' + dis() + '>' + F.bidi(o.name) + '</button>').join('')
            + '<button type="button" class="ph-chip-btn ph-chip-none' + (p.noCompetitor ? ' active' : '') + '" data-act="no-comp" aria-pressed="' + p.noCompetitor + '"' + dis() + '>' + esc(L.NoCompetitor) + '</button></div>'
            + p.competitors.map(competitorCard).join('')
            + (writable() ? (state.proposalOpen
                ? '<div class="ph-proposal"><label class="ph-field"><span class="ph-label">' + esc(L.ProposalName) + ' ' + req + '</span><input class="ph-input" id="ph-prop-name" maxlength="' + C.MAX_NAME + '" />' + (e.name ? '<span class="ph-err">' + esc(L[e.name]) + '</span>' : '') + '</label>'
                  + '<label class="ph-field"><span class="ph-label">' + esc(L.ProposalCompany) + ' ' + req + '</span><input class="ph-input" id="ph-prop-company" maxlength="' + C.MAX_NAME + '" />' + (e.company ? '<span class="ph-err">' + esc(L[e.company]) + '</span>' : '') + '</label>'
                  + '<button type="button" class="ph-btn ph-btn-p" data-act="prop-add">' + esc(L.ProposalAdd) + '</button><span class="ph-sub ph-span">' + esc(L.ProposalHint) + '</span></div>'
                : '<button type="button" class="ph-link" data-act="prop-open"><i class="bx bx-plus"></i> ' + esc(L.ProposalOpen) + '</button>') : '')
            + (state.tried && listed && !p.competitors.length && !p.noCompetitor ? '<span class="ph-err">' + esc(L.ErrCompetitors) + '</span>' : '')
            + '<div class="ph-h">' + esc(L.RecommendationTitle) + '</div>' + brdSeg('rec', C.SETS.recommendation, rec.levelCode)
            + '<span class="ph-label">' + esc(L.LinkedDoctors) + '</span><div class="ph-chips">'
            + rec.linkedContactIds.map(id => '<span class="ph-chip-btn active">' + F.bidi(state.contactNames[id] || short(id))
                + (writable() ? ' <button type="button" class="ph-chip-x" data-act="contact-rm" data-id="' + esc(id) + '" aria-label="' + esc(L.RemoveRow) + '"><i class="bx bx-x"></i></button>' : '') + '</span>').join('')
            + (rec.linkedContactIds.length ? '' : '<span class="ph-sub">' + esc(L.NoLinked) + '</span>') + '</div>'
            + (writable() && perms.contacts ? '<div class="ph-picker"><input type="search" class="ph-input" id="ph-contact-q" data-act="contact-q" placeholder="' + esc(L.DoctorSearch) + '" aria-label="' + esc(L.DoctorSearch) + '" autocomplete="off" /><div class="ph-results">'
                + state.contactResults.map(c => '<button type="button" class="ph-result" data-act="contact-add" data-id="' + esc(c.id) + '" data-name="' + esc(c.name) + '"><i class="bx bx-plus"></i> ' + F.bidi(c.name || short(c.id)) + '</button>').join('') + '</div></div>' : '')
            + '<div class="ph-h">' + esc(L.TrainingTitle) + '</div>'
            + '<label class="ph-check"><input type="checkbox" data-act="trained"' + (tr.given ? ' checked' : '') + dis() + '> ' + esc(L.TrainingGiven) + '</label>'
            + tr.requests.map((r, i) => '<div class="ph-line"><select class="ph-input ph-grow" data-act="req-type" data-idx="' + i + '" aria-label="' + esc(L.RequestType) + '"' + dis() + '><option value="">' + esc(L.RequestType) + '</option>'
                + options(C.SETS.request).map(o => '<option value="' + esc(o.code) + '"' + (o.code === r.typeCode ? ' selected' : '') + '>' + esc(C.optionLabel(o, lang)) + '</option>').join('') + '</select>'
                + '<input type="date" class="ph-input" data-act="req-date" data-idx="' + i + '" value="' + esc(r.dueDate || '') + '" aria-label="' + esc(L.RequestDate) + '"' + dis() + '>'
                + (writable() ? '<button type="button" class="ph-x" data-act="req-rm" data-idx="' + i + '" aria-label="' + esc(L.RemoveRow) + '"><i class="bx bx-x"></i></button>' : '') + '</div>').join('')
            + (writable() && tr.requests.length < C.MAX_REQUESTS ? '<button type="button" class="ph-btn ph-btn-dash" data-act="req-add"><i class="bx bx-plus"></i> ' + esc(L.RequestAdd) + '</button>' : '');
    }

    function renderSafety() {
        const s = state.body.safety;
        const yes = s.adverseEvent === true;
        el('ph-safety').innerHTML = '<div class="ph-q' + (yes ? ' ph-q-yes' : '') + '"><div class="ph-row ph-between ph-wrap"><span class="ph-q-l">' + esc(L.AeQuestion) + ' ' + req + '</span>'
            + seg('ae', [{ code: 'yes', label: L.AnswerYes }, { code: 'no', label: L.AnswerNo }], s.adverseEvent == null ? null : (yes ? 'yes' : 'no'), '', !writable()) + '</div>'
            + (yes ? '<div class="ph-alert ph-alert-danger"><i class="bx bx-error"></i> ' + esc(L.AeWarning) + '</div>'
                + '<label class="ph-field"><span class="ph-label">' + esc(L.AeNote) + ' ' + req + '</span><textarea class="ph-input ph-textarea" rows="3" data-act="ae-note" maxlength="' + C.MAX_NOTE + '"' + dis() + '>' + esc(s.adverseEventNote || '') + '</textarea>'
                + (state.tried && !String(s.adverseEventNote || '').trim() ? '<span class="ph-err">' + esc(L.ErrAeNote) + '</span>' : '') + '</label>' : '')
            + (state.tried && s.adverseEvent == null ? '<span class="ph-err">' + esc(L.ErrAe) + '</span>' : '') + '</div>';
    }

    function missingText(m) {
        const name = m.productId ? gskuName(m.productId) : '';
        return T(C.missingKey(m), name) || T('Miss_generic', m.field);
    }

    function renderSummary() {
        const b = state.body;
        const p = b.pharmacy;
        const miss = missingNow();
        const trends = {};
        p.sales.forEach(r => { if (r.trendCode) { trends[r.trendCode] = (trends[r.trendCode] || 0) + 1; } });
        const chips = [
            L.MetRole + ': ' + (b.general.metRoleCode ? optLabel(C.SETS.metRole, b.general.metRoleCode) : '—'),
            T('SumStock', p.stock.filter(r => r.levelCode).length, p.stock.length),
            T('SumSales', p.sales.filter(r => r.trendCode).length, p.sales.length),
            p.noCompetitor ? L.NoCompetitor : T('SumCompetitors', p.competitors.length),
            L.ShelfPhoto + ': ' + (p.shelf.photoEvidenceId ? L.AnswerYes : L.AnswerNo)
        ];
        const kind = b.general.visitKindCode === 'in_person' ? L.Kind_in_person : b.general.visitKindCode === 'remote' ? L.Kind_remote : '';
        el('ph-summary').innerHTML = '<span class="ph-sub">' + esc([kind, visitTimes(state.prefill)].filter(Boolean).join(' · ')) + '</span><div class="ph-chips">' + chips.map(c => '<span class="ph-pill ph-tone-secondary">' + F.bidi(c) + '</span>').join('')
            + Object.keys(trends).map(k => '<span class="ph-pill ph-tone-info">' + F.bidi(optLabel(C.SETS.trend, k) + ' × ' + trends[k]) + '</span>').join('') + '</div>'
            + '<div class="ph-pill ' + (b.safety.adverseEvent === true ? 'ph-tone-danger' : b.safety.adverseEvent === false ? 'ph-tone-success' : 'ph-tone-warning') + ' ph-self">'
            + esc(b.safety.adverseEvent === true ? L.SumAeYes : b.safety.adverseEvent === false ? L.SumAeNo : L.SumAeOpen) + '</div>'
            + (miss.length
                ? '<div class="ph-h">' + esc(T('MissingCount', miss.length)) + '</div><ul class="ph-miss">' + miss.map(m => '<li><button type="button" class="ph-link" data-act="step" data-step="' + C.stepOf(m) + '">'
                    + '<i class="bx bx-error-circle"></i> ' + F.bidi(missingText(m)) + '</button></li>').join('') + '</ul>'
                : '<div class="ph-alert ph-alert-success"><i class="bx bx-check-circle"></i> ' + esc(L.ReadyToSend) + '</div>');
    }

    function renderBar() {
        const i = C.STEPS.indexOf(state.step);
        const last = i === C.STEPS.length - 1;
        show('ph-prev', i > 0);
        setText('ph-step-label', T('StepOf', i + 1, C.STEPS.length));
        show('ph-next', !last);
        if (!last) { setText('ph-next-label', T('NextStep', L['Step_' + C.STEPS[i + 1]] || '')); }
        show('ph-draft', writable() && !fixing() && state.mode === 'draft');
        show('ph-submit', last && writable());
        const submit = el('ph-submit');
        if (submit) {
            submit.classList.toggle('ph-faded', missingNow().length > 0);
            submit.disabled = state.busy;
            setText('ph-submit-label', fixing() ? L.SendFix : L.SubmitButton);
        }
        const draft = el('ph-draft');
        if (draft) { draft.disabled = state.busy; }
    }

    // ── actions ──────────────────────────────────────────────────────────────────────────────────────────────
    const changed = () => { state.dirty = true; state.serverMissing = null; };
    const row = (list, gsku) => list.find(r => r.gskuId === gsku);

    function goStep(step) {
        if (C.STEPS.indexOf(step) < 0) { return; }
        const save = state.dirty && writable() && state.mode === 'draft' ? saveDraft(true) : Promise.resolve();
        state.step = step;
        render();
        return save;
    }

    const ifMatch = () => (state.version ? { 'If-Match': '"' + state.version + '"' } : {});

    function saveDraft(quiet) {
        if (!writable() || state.mode !== 'draft' || state.busy) { return Promise.resolve(); }
        state.busy = true;
        return send('PUT', '/draft', C.requestBody(planId, state.body, state.version), ifMatch()).then(r => {
            state.busy = false;
            const d = data(r);
            if (!r.ok || !d) { showError(errText(r)); render(); return false; }
            hideError();
            state.version = d.version;
            state.reportId = d.visitReportId || state.reportId;
            state.status = d.reportStatus || state.status;
            state.dirty = false;
            state.savedAt = new Date().toISOString();
            if (!quiet) { toast(L.DraftSaved); }
            render();
            return true;
        });
    }

    function submit() {
        state.tried = true;
        if (fixing()) { return amend(); }
        const local = C.missing(state.body, ctx());
        if (local.length) { state.step = 'summary'; render(); toast(T('MissingCount', local.length), 'warning'); return Promise.resolve(); }
        state.busy = true;
        render();
        return send('POST', '/submit', C.requestBody(planId, state.body, state.version), ifMatch()).then(r => {
            state.busy = false;
            if (r.ok) {
                const d = data(r) || {};
                hideError();
                state.version = d.version || state.version;
                state.reportId = d.visitReportId || state.reportId;
                state.status = d.reportStatus || 'submitted';
                state.mode = 'sent';
                state.editing = false;
                state.dirty = false;
                state.serverMissing = null;
                toast((d.warnings && d.warnings.length) ? L.SubmittedWithWarnings : L.Submitted);
                render();
                return;
            }
            const sm = C.serverMissing(r);
            if (sm) { state.serverMissing = sm; state.step = 'summary'; render(); return; }
            if (C.isAmendRefusal(r)) { state.mode = 'fix'; state.editing = true; showError(L.WindowClosed); render(); return; }
            showError(errText(r));
            render();
        });
    }

    function amend() {
        if (!perms.amend) { showError(L.Err_permission_denied); return Promise.resolve(); }
        const k = C.reasonError(state.fixReason);
        if (k) { render(); return Promise.resolve(); }
        state.busy = true;
        render();
        return send('POST', '/' + encodeURIComponent(state.reportId) + '/amend', C.amendBody(state.fixReason, planId, state.body, state.version)).then(r => {
            state.busy = false;
            if (!r.ok) { showError(errText(r)); render(); return; }
            const d = data(r) || {};
            hideError();
            state.version = d.version || state.version;
            state.status = d.reportStatus || 'amended';
            state.mode = 'sent';
            state.editing = false;
            state.fixReason = '';
            state.tried = false;
            toast(L.Amended);
            render();
        });
    }

    function uploadPhoto(file) {
        if (!file || !writable()) { return Promise.resolve(); }
        if (['image/jpeg', 'image/png'].indexOf(file.type) < 0) { showError(L.Err_visit_photo_invalid); return Promise.resolve(); }
        const form = new window.FormData();
        form.append('file', file, file.name || 'shelf.jpg');
        state.busy = true;
        render();
        return send('POST', '/' + encodeURIComponent(planId) + '/shelf-photo', form).then(r => {
            state.busy = false;
            const d = data(r);
            if (!r.ok || !d || !d.photoEvidenceId) { showError(errText(r)); render(); return; }
            hideError();
            state.body.pharmacy.shelf.photoEvidenceId = d.photoEvidenceId;
            state.photoUrl = window.URL && window.URL.createObjectURL ? window.URL.createObjectURL(file) : null;
            changed();
            toast(L.PhotoSaved);
            render();
            return state.mode === 'draft' ? saveDraft(true) : undefined;
        });
    }

    let gskuTimer = null;
    function searchGskus(q) {
        clearTimeout(gskuTimer);
        const text = String(q || '').trim();
        if (text.length < 2) { state.gskuResults = []; render(); return; }
        gskuTimer = setTimeout(() => send('GET', '/lookups/gskus?q=' + encodeURIComponent(text)).then(r => {
            const d = data(r);
            const have = {};
            state.body.pharmacy.stock.forEach(x => { have[x.gskuId] = true; });
            state.gskuResults = Array.isArray(d) ? d.filter(g => !have[g.id]).slice(0, 10) : [];
            (Array.isArray(d) ? d : []).forEach(g => { state.gskuNames[g.id] = (g.productName ? g.productName + ' · ' : '') + (g.code || short(g.id)) + (g.pack ? ' (' + g.pack + ')' : ''); });
            renderStock();
            const input = el('ph-gsku-q'); if (input) { input.value = text; if (input.focus) { input.focus(); } }
        }), 250);
    }

    let contactTimer = null;
    function searchContacts(q) {
        clearTimeout(contactTimer);
        const text = String(q || '').trim();
        if (text.length < 2) { state.contactResults = []; return; }
        contactTimer = setTimeout(() => send('GET', '/lookups/contacts?q=' + encodeURIComponent(text)).then(r => {
            const d = data(r);
            const have = state.body.pharmacy.recommendation.linkedContactIds;
            state.contactResults = Array.isArray(d) ? d.filter(c => have.indexOf(c.id) < 0) : [];
            renderMarket();
            const input = el('ph-contact-q'); if (input) { input.value = text; if (input.focus) { input.focus(); } }
        }), 250);
    }

    const errorBox = () => el('ph-error');
    function showError(text) { const n = errorBox(); if (n) { setText('ph-error-text', text || L.Err_generic || ''); n.classList.remove('d-none'); } }
    function hideError() { const n = errorBox(); if (n) { n.classList.add('d-none'); } }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    const actOf = e => (e && e.target && e.target.closest ? e.target.closest('[data-act]') : null);

    root.addEventListener('click', e => {
        const t = actOf(e);
        if (!t || t.disabled) { return; }
        const act = t.getAttribute('data-act');
        const val = t.getAttribute('data-val');
        const gsku = t.getAttribute('data-gsku');
        const idx = Number(t.getAttribute('data-idx'));
        const b = state.body;
        const p = b && b.pharmacy;
        if (act === 'step') { goStep(t.getAttribute('data-step')); return; }
        if (!writable()) { return; }
        switch (act) {
            case 'kind': b.general.visitKindCode = val; break;
            case 'met': b.general.metRoleCode = val; break;
            case 'stock': { const r = row(p.stock, gsku); if (r) { r.levelCode = r.levelCode === val ? null : val; } break; }
            case 'trend': { const r = row(p.sales, gsku); if (r) { r.trendCode = r.trendCode === val ? null : val; } break; }
            case 'shelf': p.shelf.positionCode = p.shelf.positionCode === val ? null : val; break;
            case 'photo': { const input = el('ph-photo-input'); if (input && input.click) { input.click(); } return; }
            case 'gsku-add': b.pharmacy = C.addGsku(p, t.getAttribute('data-id')); state.gskuResults = []; break;
            case 'gsku-rm': b.pharmacy = C.removeGsku(p, t.getAttribute('data-id')); break;
            case 'comp': { const item = C.competitorOptions(state.prefill).find(x => x.id === t.getAttribute('data-id')); if (item) { b.pharmacy = C.toggleCompetitor(p, item); } break; }
            case 'no-comp': b.pharmacy = C.setNoCompetitor(p, !p.noCompetitor); break;
            case 'comp-rm': p.competitors.splice(idx, 1); break;
            case 'adv': if (p.competitors[idx]) { p.competitors[idx] = C.cycleAdvantage(p.competitors[idx], t.getAttribute('data-code')); } break;
            case 'prop-open': state.proposalOpen = true; state.proposalErrors = {}; render(); return;
            case 'prop-add': {
                const name = (el('ph-prop-name') || {}).value;
                const company = (el('ph-prop-company') || {}).value;
                state.proposalErrors = C.proposalErrors(name, company);
                if (state.proposalErrors.name || state.proposalErrors.company) { render(); return; }
                b.pharmacy = C.addProposal(p, name, company);
                state.proposalOpen = false;
                break;
            }
            case 'rec': p.recommendation.levelCode = p.recommendation.levelCode === val ? null : val; break;
            case 'contact-add': {
                const id = t.getAttribute('data-id');
                if (id && p.recommendation.linkedContactIds.indexOf(id) < 0) { p.recommendation.linkedContactIds.push(id); state.contactNames[id] = t.getAttribute('data-name') || ''; }
                state.contactResults = [];
                break;
            }
            case 'contact-rm': p.recommendation.linkedContactIds = p.recommendation.linkedContactIds.filter(x => x !== t.getAttribute('data-id')); break;
            case 'req-add': p.training.requests.push({ typeCode: null, dueDate: null, note: null }); break;
            case 'req-rm': p.training.requests.splice(idx, 1); break;
            case 'ae': b.safety.adverseEvent = val === 'yes'; if (val !== 'yes') { b.safety.adverseEventNote = null; } break;
            default: return;
        }
        changed();
        render();
    });

    root.addEventListener('change', e => {
        const t = actOf(e);
        if (!t || !writable()) { return; }
        const act = t.getAttribute('data-act');
        const p = state.body.pharmacy;
        const idx = Number(t.getAttribute('data-idx'));
        switch (act) {
            case 'flag': { const r = row(p.stock, t.getAttribute('data-gsku')); if (r) { r[t.getAttribute('data-flag')] = !!t.checked; } break; }
            case 'stand': p.shelf.materialStand = !!t.checked; break;
            case 'trained': p.training.given = !!t.checked; break;
            case 'req-type': if (p.training.requests[idx]) { p.training.requests[idx].typeCode = t.value || null; } break;
            case 'req-date': if (p.training.requests[idx]) { p.training.requests[idx].dueDate = t.value || null; } break;
            default: return;
        }
        changed();
        render();
    });

    root.addEventListener('input', e => {
        const t = actOf(e);
        if (!t) { return; }
        const act = t.getAttribute('data-act');
        if (act === 'gsku-q') { searchGskus(t.value); return; }
        if (act === 'contact-q') { searchContacts(t.value); return; }
        if (act === 'fix-why') { state.fixReason = t.value || ''; renderHead(); return; }
        if (!writable()) { return; }
        const b = state.body;
        if (act === 'note') { b.general.generalNote = t.value || ''; }
        else if (act === 'ae-note') { b.safety.adverseEventNote = t.value || null; }
        else if (act === 'boxes') {
            const r = row(b.pharmacy.sales, t.getAttribute('data-gsku'));
            const v = C.boxesOf(t.value);
            if (r) { r.boxesPerMonth = v.value; }
            if (t.classList) { t.classList.toggle('is-invalid', !!v.error); }
        } else { return; }
        changed();
        renderHead();
        renderSteps();
    });

    const on = (id, ev, fn) => { const n = el(id); if (n) { n.addEventListener(ev, fn); } };
    on('ph-prev', 'click', () => goStep(C.STEPS[C.STEPS.indexOf(state.step) - 1]));
    on('ph-next', 'click', () => goStep(C.STEPS[C.STEPS.indexOf(state.step) + 1]));
    on('ph-draft', 'click', () => saveDraft(false));
    on('ph-submit', 'click', () => submit());
    on('ph-unlock', 'click', () => { state.editing = true; state.mode = 'sent'; state.step = 'general'; render(); });
    on('ph-photo-input', 'change', e => { const f = e && e.target && e.target.files ? e.target.files[0] : null; uploadPhoto(f); if (e && e.target) { e.target.value = ''; } });
    on('ph-retry', 'click', () => load());

    // the in-page API the smoke drives (no network of its own)
    window.PharmacyReportPage = { state: state, submit: submit, saveDraft: saveDraft, goStep: goStep, uploadPhoto: uploadPhoto };

    load();
}(window, document));
