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
    const base = '/CRM/VisitPlanning/api';
    const sessionId = root.dataset.sessionId;
    const canGenerate = root.dataset.canGenerate === 'true';
    const readOnly = root.dataset.readOnly === 'true';

    const el = id => document.getElementById(id);
    const esc = s => { const d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };
    const fmt = (tpl, ...args) => args.reduce((t, a, i) => t.split('{' + i + '}').join(String(a)), String(tpl || ''));
    const getJson = url => fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
        .then(r => (r.ok ? r.json() : null)).catch(() => null);
    const localDate = v => new Date(v + 'T00:00:00');
    const ymd = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    const mondayYmd = v => { const d = localDate(v); d.setDate(d.getDate() - ((d.getDay() + 6) % 7)); return ymd(d); };
    const dm = d => d.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
    const weekTitle = w => fmt(L.WeekNumberLabel || '{0}', w.isoWeek);
    const weekRange = w => { const f = localDate(w.from || w.weekStart), t = localDate(w.to || w.weekStart); return isNaN(f) || isNaN(t) ? '' : dm(f) + ' – ' + dm(t); };

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
        return '<span class="badge rounded-pill ' + (promo ? 'bg-label-primary' : 'bg-transparent border text-body') + '">' + esc(c.productCode || '—') + '</span>';
    };

    // Product history label per week of the doctor's visits.
    const HISTORY_LABEL = { presented: 'HistoryPresented', planned: 'HistoryPlanned', projected: 'HistoryProjected' };
    const historyState = (slot, firstDraft) => {
        if (slot.plannedDate < ymd(new Date())) return 'presented';
        if (slot.isFixed) return 'planned';
        return (slot.weekStart || mondayYmd(slot.plannedDate)) === firstDraft ? 'planned' : 'projected';
    };

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
            (status.segmentBadges || []).map(sname => '<span class="badge bg-label-info" title="' + esc(L.SegmentBadgeHint || '') + '">' + esc(sname) + '</span>').join('') +
            (hasFrequency ? '' : '<span class="badge bg-label-secondary">' + esc(L.FrequencyNone || '') + '</span>') + '</div>');

        // target / done / remaining (3D)
        parts.push('<div class="row g-2 text-center mb-4">' + [
            [L.FrequencyTarget, hasFrequency ? fmt(L.FrequencyPerPeriod || '{0}', status.requiredVisitCount) : '—'],
            [L.DoneLabel, status.done != null ? status.done : '—'],
            [L.RemainingLabel, status.remaining != null ? status.remaining : '—']
        ].map(x => '<div class="col-4"><div class="border rounded p-2"><div class="small text-muted">' + esc(x[0] || '') + '</div><div class="fw-semibold">' + esc(x[1]) + '</div></div></div>').join('') + '</div>');

        // the next visit's products (+ sources + time)
        const today = ymd(new Date());
        const next = slots.find(s => s.plannedDate >= today) || null;
        const content = (p.content || []).find(c => c.contactId === current.contactId) || null;
        const products = (content && content.products) || [];
        const nextWeek = next ? page.week(next.weekStart || mondayYmd(next.plannedDate)) : null;
        const editable = canGenerate && !readOnly && !page.isLegacy() && !!nextWeek && (nextWeek.status === 'draft' || nextWeek.status === 'empty');
        parts.push('<div class="border rounded p-3 mb-4"><div class="d-flex justify-content-between align-items-center mb-2"><span class="text-uppercase small fw-semibold text-muted">' + esc(L.NextVisitProducts || '') + '</span>' +
            '<span class="small text-muted">' + esc(nextWeek ? weekTitle(nextWeek) + ' · ' + weekRange(nextWeek) : '—') + '</span></div>' +
            (products.length
                ? '<div class="d-flex flex-wrap gap-1 mb-2">' + products.map(chip).join(' ') + '</div>' +
                  '<div class="small text-muted">' + esc(fmt(L.SourceLine || '{0}', Array.from(new Set(products.map(x => L[SOURCE_LABEL[x.source]] || x.source))).join(', '))) +
                  (content && content.durationMinutes ? ' · ' + esc(fmt(L.ApproxMinutes || '{0}', content.durationMinutes)) : '') + '</div>'
                : '<div class="small text-warning"><span class="badge bg-label-warning me-1">' + esc(L.NoProductBadge || '') + '</span>' + esc(L.NoProductsNoTime || '') + '</div>') +
            (editable ? '<button type="button" class="btn btn-sm btn-label-primary mt-2" id="vp-dp-change-products">' + esc(products.length ? (L.EditProducts || '') : (L.PickProducts || '')) + '</button>' : '') +
            '</div>');

        // over the period · product history
        const firstDraft = (page.weeks().find(w => w.status === 'draft') || {}).weekStart;
        parts.push('<div class="text-uppercase small fw-semibold text-muted mb-2">' + esc(L.ProductHistoryTitle || '') + '</div>' +
            (slots.length ? '<ul class="list-unstyled mb-0">' + slots.map(s => {
                const w = page.week(s.weekStart || mondayYmd(s.plannedDate));
                const state = historyState(s, firstDraft);
                const items = (s.contentItems || []).slice().sort((a, b) => (a.order || 0) - (b.order || 0));
                return '<li class="border-bottom py-2"><div class="d-flex justify-content-between small"><span class="fw-medium">' + esc(w ? weekTitle(w) + ' · ' + weekRange(w) : s.plannedDate) + '</span>' +
                    '<span class="badge bg-label-' + (state === 'presented' ? 'secondary' : state === 'planned' ? 'success' : 'info') + '">' + esc(L[HISTORY_LABEL[state]] || state) + '</span></div>' +
                    '<div class="d-flex flex-wrap gap-1 mt-1">' + (items.length ? items.map(chip).join(' ') : '<span class="small text-muted">' + esc(L.NoProductBadge || '') + '</span>') + '</div></li>';
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
