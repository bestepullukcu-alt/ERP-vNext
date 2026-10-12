/**
 * WP-VP-4D — the doctor panel's "Period view" tab (mockup "05 Haftalar" panel, K-7 addendum), on the 4C panel skeleton.
 *
 *   Opened with page.emit('doctor-panel:open', { contactId, accountId }) (the Weeks tab: a doctor row, a visit).
 *   - Head: name · specialty · institution; segment badges (information only, K-4); "no frequency" when unknown.
 *   - Frequency target / done / remaining — the 3D period status (GET sessions/{id}/targets).
 *   - The next visit's products: its week, the chips, their sources, the visit time (preview content[]); "Change products"
 *     opens the Products tab for this doctor (targets.js, 'request:pick-products'); none ⇒ "The visit time cannot be
 *     calculated. Pick products".
 *   - Over the period · product history: per week with a visit of the doctor — Presented (a visit already behind us; its
 *     planned products until reports carry what was told, SB-3c) / Planned (approved or this draft week) / Projected
 *     (a later draft week) + the chips.
 *   - "Next content" is NOT shown: content progression waits for SB-3c.
 *   The Products tab (and the footer) is a plan writer's only.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('visit-planning-details');
    const page = window.VisitPlanningPage || null;
    const panel = document.getElementById('vp-doctor-panel');
    if (!root || !page || !panel) return;

    const L = window.L10n || {};
    const VPF = window.VisitPlanningFormat; // WP-VP-4H — dates / numbers in the application's language
    const base = '/CRM/VisitPlanning/api';
    const sessionId = root.dataset.sessionId;
    const canGenerate = root.dataset.canGenerate === 'true';
    const readOnly = root.dataset.readOnly === 'true';

    const el = id => document.getElementById(id);
    const esc = s => { const d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };
    const bidi = VPF.bidi; // WP-VP-4I (7) — data names isolated (<bdi>) for right-to-left pages
    const fmt = (tpl, ...args) => args.reduce((t, a, i) => t.split('{' + i + '}').join(String(a)), String(tpl || ''));
    const getJson = url => fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
        .then(r => (r.ok ? r.json() : null)).catch(() => null);
    const localDate = v => new Date(v + 'T00:00:00');
    const ymd = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    const mondayYmd = v => { const d = localDate(v); d.setDate(d.getDate() - ((d.getDay() + 6) % 7)); return ymd(d); };
    const weekTitle = w => fmt(L.WeekNumberLabel || '{0}', w.isoWeek);
    const weekRange = w => VPF.workRange(w.from || w.weekStart, w.to || w.weekStart);

    // the plan's targets (names + 3D status) and the specialty labels — read once, refreshed with the plan
    let doctors = {}, accounts = {}, specLabels = {};
    const loadTargets = () => getJson(base + '/sessions/' + encodeURIComponent(sessionId) + '/targets').then(b => {
        const d = b && b.data; if (!d) return;
        doctors = {}; accounts = {};
        (d.doctors || []).forEach(x => { if (x && x.contactId) doctors[x.contactId] = x; });
        (d.accounts || []).concat(d.pharmacies || []).forEach(a => { if (a && a.accountId) accounts[a.accountId] = a; });
    });
    const loadLabels = () => getJson(base + '/reference-labels').then(b => { specLabels = (b && b.data && b.data.specialties) || {}; });
    const specLabel = code => code ? (specLabels[code] || specLabels[String(code).toLowerCase()] || code) : '';

    const SOURCE_LABEL = { play: 'LegendPlay', 'rep-pick': 'LegendRepPick', 'last-visit': 'LegendLastVisit', portfolio: 'LegendPortfolio' };
    const chip = c => {
        const promo = c.role !== 'non-promo';
        return '<span class="badge ' + (promo ? 'bg-label-primary' : 'bg-transparent border text-body') + '" title="' + esc(c.productCode || '') + '">' + bidi(VPF.productLabel(c)) + '</span>';
    };

    // WP-VP-4H (5) — the product history covers EVERY week of the period; a week's state: done (a completed report, 4G
    // reportStatus) · approved (written; a visit behind us without the field) · draft (the first draft week) · projected
    // (a later draft week) · none ("—"). The chips carry the matching prefix: Presented / Planned / Projected.
    const DONE_REPORT = ['completed', 'done', 'reported'];
    const historyState = (slot, firstDraft) => {
        if (!slot) return 'none';
        if (slot.reportStatus && DONE_REPORT.indexOf(String(slot.reportStatus).toLowerCase()) > -1) return 'done';
        if (slot.isFixed || slot.plannedDate < ymd(new Date())) return 'approved';
        return (slot.weekStart || mondayYmd(slot.plannedDate)) === firstDraft ? 'draft' : 'projected';
    };
    const HISTORY_STATE_LABEL = { done: 'HistoryStateDone', approved: 'HistoryStateApproved', draft: 'HistoryStateDraft', projected: 'HistoryStateProjected' };
    const HISTORY_LABEL = { done: 'HistoryPresented', approved: 'HistoryPlanned', draft: 'HistoryPlanned', projected: 'HistoryProjected' };
    const HISTORY_TONE = { done: 'success', approved: 'primary', draft: 'label-primary', projected: 'label-secondary' };

    let current = null; // { contactId, accountId }
    const render = () => {
        const host = el('vp-dp-tab-period'); if (!host || !current) return;
        const p = page.state.preview || {};
        const slots = (p.scheduled || []).filter(s => s.contactId === current.contactId)
            .sort((a, b) => String(a.plannedDate).localeCompare(String(b.plannedDate)));
        const doctor = doctors[current.contactId] || null;
        const status = (doctor && doctor.status) || {};
        const name = (doctor && doctor.displayName) || (slots[0] && slots[0].contactDisplayName) || '—';
        const specialty = specLabel((doctor && doctor.specialty) || (slots[0] && slots[0].contactSpecialty) || '');
        const accountId = current.accountId || (doctor && doctor.accountId) || (slots[0] && slots[0].accountId);
        const accountName = accountId && accounts[accountId] ? accounts[accountId].accountName : '';
        el('vp-dp-title').textContent = name;
        el('vp-dp-sub').textContent = [specialty, accountName].filter(Boolean).join(' · ') || '—';

        const hasFrequency = status.requiredVisitCount != null && status.frequencyStatus !== 'unknown';
        const parts = [];
        parts.push('<div class="d-flex flex-wrap gap-1 mb-3">' +
            (status.segmentBadges || []).map(sname => '<span class="badge bg-label-info" title="' + esc(L.SegmentBadgeHint || '') + '">' + bidi(sname) + '</span>').join('') +
            (hasFrequency ? '' : '<span class="badge bg-label-secondary">' + esc(L.FrequencyNone || '') + '</span>') + '</div>');

        // three boxes: frequency target ("dönemde 3" / "dönemde 1 (varsayılan)", F4-2) / done / remaining (3D)
        parts.push('<div class="row g-2 text-center mb-4">' + [
            [L.FrequencyTarget, hasFrequency ? fmt(L.FrequencyPerPeriod || '{0}', status.requiredVisitCount) : (L.FrequencyDefaultOne || '')],
            [L.DoneLabel, status.done != null ? status.done : '—'],
            [L.RemainingLabel, status.remaining != null ? status.remaining : '—']
        ].map(x => '<div class="col-4"><div class="border rounded p-2 h-100"><div class="small text-muted">' + esc(x[0] || '') + '</div><div class="fw-semibold">' + esc(x[1]) + '</div></div></div>').join('') + '</div>');

        // the next visit's products: week + range, chips (names), the source, "1 promo + 1 reminder + report ≈ 22 min"
        const today = ymd(new Date());
        const next = slots.find(s => s.plannedDate >= today) || null;
        const content = (p.content || []).find(c => c.contactId === current.contactId) || null;
        const products = (content && content.products) || [];
        const nextWeek = next ? page.week(next.weekStart || mondayYmd(next.plannedDate)) : null;
        const editable = canGenerate && !readOnly && !page.isLegacy() && !!nextWeek && (nextWeek.status === 'draft' || nextWeek.status === 'empty');
        const promoCount = products.filter(x => x.role !== 'non-promo').length;
        parts.push('<div class="border rounded p-3 mb-2"><div class="d-flex justify-content-between align-items-start gap-2 mb-2"><div><div class="text-uppercase small text-muted">' + esc(L.NextVisitProducts || '') + '</div>' +
            '<div class="small fw-medium">' + esc(nextWeek ? weekTitle(nextWeek) + ' · ' + weekRange(nextWeek) : '—') + '</div></div>' +
            (editable ? '<button type="button" class="btn btn-sm btn-label-primary" id="vp-dp-change-products">' + esc(products.length ? (L.EditProducts || '') : (L.PickProducts || '')) + '</button>' : '') + '</div>' +
            (products.length
                ? '<div class="d-flex flex-wrap gap-1 mb-2">' + products.map(chip).join(' ') + '</div>' +
                  '<div class="small text-muted">' + esc(fmt(L.SourceLine || '{0}', Array.from(new Set(products.map(x => L[SOURCE_LABEL[x.source]] || x.source))).join(', '))) + '</div>' +
                  (content && content.durationMinutes ? '<div class="small">' + esc(fmt(L.DurationLine || '{0} {1} {2}', promoCount, products.length - promoCount, content.durationMinutes)) + '</div>' : '')
                : '<div class="small text-warning"><span class="badge bg-label-warning me-1">' + esc(L.NoProductBadge || '') + '</span>' + esc(L.NoProductsNoTime || '') + '</div>') +
            '</div>');
        // "Next content: soon" — content progression waits for SB-3c
        parts.push('<div class="small text-muted mb-4"><i class="bx bx-time-five me-1"></i>' + esc(L.NextContentSoon || '') + '</div>');

        // over the period · product history — every week of the period, a past week faded
        const firstDraft = (page.weeks().find(w => w.status === 'draft') || {}).weekStart;
        const weeks = page.weeks();
        parts.push('<div class="text-uppercase small text-muted mb-2">' + esc(L.ProductHistoryTitle || '') + '</div>' +
            (weeks.length ? '<ul class="list-unstyled mb-0">' + weeks.map(w => {
                const slot = slots.find(s => (s.weekStart || mondayYmd(s.plannedDate)) === w.weekStart) || null;
                const state = historyState(slot, firstDraft);
                const items = slot ? (slot.contentItems || []).slice().sort((a, b) => (a.order || 0) - (b.order || 0)) : [];
                return '<li class="border-bottom py-2 vp-dp-week' + (w.status === 'past' ? ' opacity-50' : '') + '" data-ws="' + esc(w.weekStart) + '"><div class="d-flex justify-content-between small gap-2"><span class="fw-medium">' + esc(weekTitle(w) + ' · ' + weekRange(w)) + '</span>' +
                    (state === 'none' ? '<span class="text-muted">—</span>' : '<span class="badge bg-' + HISTORY_TONE[state] + '">' + esc(L[HISTORY_STATE_LABEL[state]] || state) + '</span>') + '</div>' +
                    (items.length ? '<div class="d-flex flex-wrap gap-1 mt-1 align-items-center"><span class="small text-muted">' + esc((L[HISTORY_LABEL[state]] || '') + ':') + '</span>' + items.map(chip).join(' ') + '</div>' : '') + '</li>';
            }).join('') + '</ul>' : '<div class="small text-muted">' + esc(L.NoVisitsInPeriod || '') + '</div>'));

        host.innerHTML = parts.join('');
    };

    const showTab = id => { const b = el(id); if (b && window.bootstrap) window.bootstrap.Tab.getOrCreateInstance(b).show(); };
    const open = d => {
        if (!d || !d.contactId) return;
        current = { contactId: d.contactId, accountId: d.accountId || null };
        render();
        showTab('vp-dp-tab-period-btn');
        Promise.all([Object.keys(doctors).length ? null : loadTargets(), Object.keys(specLabels).length ? null : loadLabels()]).then(render);
        if (window.bootstrap) window.bootstrap.Offcanvas.getOrCreateInstance(panel).show();
    };

    // The Products tab is a plan writer's only; the footer (Cancel / Done) belongs to it.
    if (!canGenerate) el('vp-dp-tab-products-item')?.classList.add('d-none');
    panel.addEventListener('shown.bs.tab', e => {
        el('vp-dp-footer')?.classList.toggle('d-none', !(e.target && e.target.id === 'vp-dp-tab-products-btn'));
    });
    panel.addEventListener('click', e => {
        if (!e.target.closest('#vp-dp-change-products') || !current) return;
        const doctor = doctors[current.contactId] || {};
        page.emit('request:pick-products', {
            contactId: current.contactId, accountId: current.accountId || doctor.accountId || null,
            accountContactLinkId: doctor.accountContactLinkId || null, name: doctor.displayName || el('vp-dp-title').textContent,
            specialty: specLabel(doctor.specialty || '')
        });
    });
    page.on('doctor-panel:open', open);
    page.on('session', () => { loadTargets().then(render); });
    page.on('preview', () => render());
})(window, document);
