/**
 * WP-VW-W3E · the manager's visit evidence flags (mockup "W3E Ek Kanitlar" M1–M6) — W3E-FLAGS-STD (K-STD): the Golden
 * Compact list standard.
 *
 * THE VISITS LIST IS THE SHARED COMPONENT (data_mode: server): Save View, column state, the inline filter, paging, the
 * skeleton, the export and the quick view's open / close come from DitenDataTable.createList. The factory sends start /
 * length / draw + each applied filter by its key (period, from, to, resourceId, flags, accountId, scope) to the same-origin
 * proxy, which turns it into the CRM's own window / page query (the CRM orders by the start, newest first, so no column is
 * sortable and no orderBy is ever sent). No ext.search, no serverSide, no ajax.data here — the factory owns them.
 *
 * THE REP SUMMARY is the second table (rep-summary.js, data_mode client: the summary read's bounded rep set, no page size
 * in the URL). It and the summary card are ONE read that follows the list's APPLIED filter state (C.summaryQuery) — one
 * state, so Save View keeps all three. A rep row filters the visits to that rep through the standard Apply.
 *
 * A custom range over 92 days is never sent: the Apply guard stops it (the standard warning toast) before the factory
 * reads the filter; the proxy refuses it too. Every rule of a row is VisitEvidenceFlagsCore's (flags-core.js, Node-tested);
 * every word comes from window.L10n (7 languages); every data name is isolated (bidi) for RTL. Read only: nothing writes.
 */
'use strict';

const VisitEvidenceFlagsList = (function () {
    let list = null;
    let repSummary = null;
    let quickToken = 0;

    const dtTableEl = document.querySelector('.datatables-visitevidenceflags');
    const repsTableEl = document.querySelector('.datatables-vf-reps');
    const flagsRoot = document.getElementById('vf-root');
    const endpoint = '/CRM/VisitEvidenceFlags/api';
    const C = window.VisitEvidenceFlagsCore;
    const F = window.VisitPlanningFormat;
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const byId = (id) => document.getElementById(id);
    const attr = (name) => (flagsRoot ? flagsRoot.getAttribute(name) : null);
    const perms = { readAll: attr('data-can-read-all') === 'true', photos: attr('data-can-see-photos') === 'true' };
    const visitUrl = attr('data-visit-url') || '/CRM/VisitReport';
    const exceptionUrl = attr('data-exception-url') || '/CRM/VisitEvidenceSettings';
    const $ = window.jQuery;

    // control 0 · start 1 · rep 2 · institution / doctor 3 · kind 4 · flags 5 · duration 6 · exception 7 · actions 8
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6, 7];

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const esc = (s) => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L()[key] || '', ...args);
    const culture = () => F.dateCulture();
    const dtf = (o) => { try { return new Intl.DateTimeFormat(culture(), Object.assign({ timeZone: 'UTC' }, o)); } catch (e) { return new Intl.DateTimeFormat('en-GB', o); } };
    const dayText = (iso) => (iso ? dtf({ day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(iso)) : '—');
    const timeText = (iso) => (iso ? new Intl.DateTimeFormat(culture(), { hour: '2-digit', minute: '2-digit' }).format(new Date(iso)) : '—');
    const startDay = (iso) => (iso ? new Intl.DateTimeFormat(culture(), { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(iso)) : '—');
    const rangeText = (from, to, short) => {
        if (!from || !to) { return ''; }
        const f = dtf(short ? { day: 'numeric', month: 'short' } : { day: 'numeric', month: 'short', year: 'numeric' }); // short: the period option "8–14 Eki" (the mockup)
        try { return f.formatRange(new Date(from + 'T00:00:00Z'), new Date(to + 'T00:00:00Z')); } catch (e) { return f.format(new Date(from + 'T00:00:00Z')) + ' – ' + f.format(new Date(to + 'T00:00:00Z')); }
    };
    const monthText = (today) => dtf({ month: 'long', year: 'numeric' }).format(new Date(today + 'T00:00:00Z'));
    const num = (n) => F.isolate(F.number(n, 0));
    const pct = (n) => { let v; try { v = new Intl.NumberFormat(culture(), { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(n); } catch (e) { v = String(n); } return T('Percent', v); }; // %9,0 (the mockup)
    const valueText = (v) => (v ? T(v.key, ...v.args.map((a) => F.number(a, v.digits || 0))) : '');
    const durationText = (s) => (s == null ? '—' : valueText(C.duration(s)));
    const today = () => new Date(Date.now()).toISOString().slice(0, 10);
    const valueOf = (id) => { const n = byId(id); return n ? String(n.value || '') : ''; };
    const setValue = (id, value) => { const n = byId(id); if (!n) { return; } if ($) { $(n).val(value).trigger('change'); } else { n.value = value; } };
    const onChange = (id, fn) => { const n = byId(id); if (!n) { return; } if ($) { $(n).on('change', fn); } else { n.addEventListener('change', fn); } };

    const call = (path) => fetch(endpoint + path, { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
        .then((r) => r.text().then((t) => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));
    const data = (r) => (r && r.ok && r.body ? r.body.data : null);

    // ── the cells ────────────────────────────────────────────────────────────────────────────────────────────
    const chipHtml = (c) => {
        const cat = C.CATS[c.cat] || C.CATS.exc;
        return '<span class="vf-chip vf-tone-' + cat.tone + (c.flag ? '' : ' vf-chip-info') + '"><i class="bx ' + cat.icon + '"></i>' + esc(L()[c.key] || c.code) + (c.value ? ' · ' + esc(valueText(c.value)) : '') + '</span>';
    };
    const targetHtml = (t) => {
        const target = t || {};
        const doctor = target.type === 'contact';
        const inst = doctor ? (target.accountName || '—') : (target.name || target.accountName || '—');
        return '<div class="d-flex flex-column"><span class="text-heading">' + F.bidi(inst) + '</span>' + (doctor && target.name ? '<small class="text-muted">' + F.bidi(target.name) + '</small>' : '') + '</div>';
    };
    // the mockup's avatars: one stable tone per rep (primary · info · warning)
    const AV_TONES = ['primary', 'info', 'warning'];
    const avatar = (name, id) => {
        const key = String(id || name || '');
        let h = 0; for (let i = 0; i < key.length; i++) { h = (h * 31 + key.charCodeAt(i)) >>> 0; }
        return '<span class="vf-avatar vf-av-' + AV_TONES[h % AV_TONES.length] + '">' + esc(C.initials(name)) + '</span>';
    };
    const repHtml = (name, id) => '<div class="d-flex align-items-center gap-2">' + avatar(name, id) + '<span class="text-nowrap">' + F.bidi(name || id) + '</span></div>';

    // ── the filter (one state: the list's applied filters) ──────────────────────────────────────────────────
    const accounts = {};
    const accountInfo = {};
    let reps = [];

    const filterFields = [
        { id: 'filterPeriod', key: 'period', kind: 'single' },
        { id: 'vf-from', key: 'from', kind: 'text' },
        { id: 'vf-to', key: 'to', kind: 'text' },
        { id: 'filterRep', key: 'resourceId', kind: 'single' },
        { id: 'filterFlags', key: 'flags', kind: 'multi' },
        { id: 'filterAccount', key: 'accountId', kind: 'single' },
        { id: 'filterScope', key: 'scope', kind: 'text' }
    ];

    const periodOptions = () => {
        const r = C.rangeOf('', today());
        return {
            showAllText: T('PeriodLast7', rangeText(r.from, r.to, true)),   // the baseline "" = the last 7 days
            items: [{ value: 'last30', text: L().PeriodLast30 }, { value: 'month', text: monthText(today()) }, { value: 'custom', text: L().PeriodCustom }]
        };
    };

    // refill a select keeping its staged value (a value the new options lack is kept as its own option)
    const refill = (id, items, showAllText) => {
        const el = byId(id);
        if (!el) { return; }
        const current = $ ? $(el).val() : el.value;
        el.innerHTML = '';
        const add = (value, text) => { const o = document.createElement('option'); o.value = value; o.textContent = text; el.appendChild(o); };
        add('', showAllText || '');
        items.forEach((i) => add(i.value, i.text));
        if (current && !items.some((i) => i.value === current)) { add(current, current); }
        if ($) { $(el).val(current || '').trigger('change.select2'); } else { el.value = current || ''; }
    };

    const repShowAll = () => T(perms.readAll ? 'RepAllTenant' : 'RepAllTeam', F.number(reps.length, 0));
    const refillReps = () => refill('filterRep', reps.map((x) => ({ value: x.resourceId, text: F.isolate(x.repName || x.resourceId) })), repShowAll());
    const refillAccounts = () => {
        const options = C.accountOptions(Object.keys(accounts).map((id) => Object.assign({ id: id, name: accounts[id] }, accountInfo[id] || {})),
            (a, b) => String(a).localeCompare(String(b), culture()));
        // the name isolated as a whole (G-6): "75.YIL" never turns into "YIL.75" in Arabic; the city / code after it (G-7)
        refill('filterAccount', options.map((o) => ({ value: o.id, text: o.suffix ? F.isolate(o.name) + ' · ' + F.isolate(o.suffix) : F.isolate(o.text) })), L().AccountAll);
    };

    const loadLookupOptions = async ({ fillSelect }) => {
        const p = periodOptions();
        fillSelect(byId('filterPeriod'), p.items, { showAllText: p.showAllText });
        fillSelect(byId('filterRep'), [], { showAllText: repShowAll() });
        // only the REAL flags: "QR not scanned" is not a flag and is never offered (D-6)
        fillSelect(byId('filterFlags'), C.FLAGS.map((f) => ({ value: f.code, text: L()['Flag_' + f.code] || f.code })));
        fillSelect(byId('filterAccount'), [], { showAllText: L().AccountAll });
    };

    /**
     * W3E-FIX (G-7) — institutions with the same name in the filter ("100.YIL ECZANESİ" × 3): their city / code is read
     * ONCE per id through the page's proxy (only for the same-name ones), then the options carry it.
     */
    const tellApart = () => {
        const ask = C.sameNameIds(Object.keys(accounts).map((id) => ({ id: id, name: accounts[id] }))).filter((id) => !accountInfo[id]);
        if (!ask.length) { return Promise.resolve(); }
        ask.forEach((id) => { accountInfo[id] = {}; });
        return call('/account-labels?ids=' + encodeURIComponent(ask.join(','))).then((res) => {
            const d = data(res);
            (Array.isArray(d) ? d : []).forEach((x) => { if (x && x.accountId) { accountInfo[x.accountId] = { city: x.city || null, code: x.code || null }; } });
            refillAccounts();
        });
    };

    // the custom range is shown only for "custom"; a preset clears it (a stale range never travels with a preset)
    const syncRange = () => {
        const custom = valueOf('filterPeriod') === 'custom';
        byId('filterRange')?.classList.toggle('d-none', !custom);
        if (!custom) {
            ['vf-from', 'vf-to'].forEach((id) => { const n = byId(id); if (n && n.value) { if (n._flatpickr) { n._flatpickr.clear(); } n.value = ''; } });
        }
    };
    // "only flagged" is a switch over the hidden scope value ("" = only flagged, the baseline; "all" = every visit)
    const syncSwitchFromScope = () => { const sw = byId('vf-only'); if (sw) { sw.checked = valueOf('filterScope') !== 'all'; } };
    const syncScopeFromSwitch = () => { const n = byId('filterScope'); const sw = byId('vf-only'); if (n && sw) { n.value = sw.checked ? '' : 'all'; } };

    const markRange = (bad) => ['vf-from', 'vf-to'].forEach((id) => byId(id)?.classList.toggle('is-invalid', !!bad));

    /**
     * The 92-day rule on Apply: a custom range that the CRM would refuse is never sent. Registered in the CAPTURE phase on
     * the Apply button, so it runs before the factory's own Apply (which reads the staged filter and redraws): a bad range
     * stops there with the standard warning toast, the panel stays open, nothing is requested.
     */
    const guardApply = (event) => {
        syncScopeFromSwitch();
        const period = valueOf('filterPeriod');
        if (period !== 'custom') { syncRange(); }
        const bad = C.customRangeError(period, valueOf('vf-from'), valueOf('vf-to'));
        markRange(bad);
        if (!bad) { return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        window.showToast?.(T(bad, C.MAX_RANGE_DAYS), 'warning');
    };

    // ── the summary card + the rep summary (one read of the applied filter state) ──────────────────────────
    /**
     * The list's APPLIED filter state, read from the request the list last sent (DataTables' ajax.params(): the factory's
     * own query — start, length, draw and each applied filter by its key). Not handle.appliedFilters: the factory copies
     * that getter with Object.assign, so the handle keeps the state of the first moment only (measured live, 2026-10-15).
     */
    const appliedFilters = () => {
        const params = list && list.dt && list.dt.ajax && typeof list.dt.ajax.params === 'function' ? list.dt.ajax.params() : null;
        if (typeof params !== 'string') { return null; }
        const q = new URLSearchParams(params);
        return { period: q.get('period') || '', from: q.get('from') || '', to: q.get('to') || '', resourceId: q.get('resourceId') || '',
            flags: q.getAll('flags'), accountId: q.get('accountId') || '', scope: q.get('scope') || '' };
    };

    const renderSummary = (s) => {
        const set = (id, t) => { const n = byId(id); if (n) { n.textContent = t; } };
        if (!s) { return; }
        set('vf-sum-started', F.number(s.started, 0));
        const period = (appliedFilters() || {}).period || '';
        set('vf-sum-range', (period === '' || period === 'last7' ? (L().PeriodLast7Short || '') + ' · ' : period === 'last30' ? (L().PeriodLast30 || '') + ' · ' : '') + rangeText(s.from, s.to));
        set('vf-sum-flagged', F.number(s.flagged, 0));
        set('vf-sum-rate', pct(C.repRate(s.started, s.flagged)));
        const b = C.summaryBars(s.byFlag, 6);
        const bar = (label, count, width) => '<div class="vf-bar-row"><span class="vf-bar-label">' + esc(label) + '</span><span class="vf-bar"><span style="width:' + width + '%"></span></span><span class="vf-bar-count">' + num(count) + '</span></div>';
        const bars = byId('vf-bars');
        if (bars) {
            bars.innerHTML = b.bars.map((x) => bar(L()[x.key] || x.code, x.count, x.pct)).join('')
                + (b.other ? bar(T('SumOther', F.number(b.other.types, 0)), b.other.count, b.other.pct) : '')
                + (!b.bars.length ? '<span class="vf-sub">' + esc(L().SumNoFlags) + '</span>' : '');
        }
    };

    // the summary read answers the card AND the rep table (one request); the rep set refills the rep filter
    const summaryDataSrc = (d) => {
        renderSummary(d);
        reps = Array.isArray(d.reps) ? d.reps : [];
        refillReps();
        return reps;
    };

    /** The summary read follows the APPLIED filter state: a new state → one new read (the card + the rep table). */
    const syncSummary = () => {
        const applied = appliedFilters();
        if (applied && repSummary) { repSummary.sync(C.summaryQuery(applied)); }
    };

    /** M3 — a rep row filters the visits to that rep through the standard Apply (Save View follows), then the visits tab. */
    const filterToRep = (resourceId) => {
        if (!resourceId) { return; }
        const el = byId('filterRep');
        if (el && !Array.prototype.some.call(el.options || [], (o) => o.value === resourceId)) {
            const o = document.createElement('option'); o.value = resourceId; o.textContent = resourceId; el.appendChild(o);
        }
        setValue('filterRep', resourceId);
        byId('btnFilterApply')?.click();
        const tab = byId('vf-tab-visits');
        if (tab && window.bootstrap?.Tab) { window.bootstrap.Tab.getOrCreateInstance(tab).show(); }
    };

    // every list answer: the institutions it names feed the institution filter, then the summary follows the state
    const onListResponse = (json) => {
        const items = (json && json.data && Array.isArray(json.data.items)) ? json.data.items : [];
        let added = false;
        items.forEach((row) => {
            const t = row.target || {};
            if (t.accountId && (t.accountName || t.name) && !accounts[t.accountId]) { accounts[t.accountId] = t.accountName || t.name; added = true; }
        });
        if (added) { refillAccounts(); tellApart(); }
        setTimeout(syncSummary, 0);
    };

    // ── M4 — the empty state on the standard table ─────────────────────────────────────────────────────────
    const emptyHtml = () => '<div class="vf-empty"><i class="bx bx-check-shield"></i><strong>'
        + esc(((appliedFilters() || {}).scope === 'all') ? L().EmptyAllTitle : L().EmptyTitle) + '</strong><span class="vf-sub">' + esc(L().EmptyText) + '</span></div>';
    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) { return; }
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = emptyHtml(); }
    }

    // ── M2 — the quick view ─────────────────────────────────────────────────────────────────────────────────
    const kv = (k, v) => '<div class="vf-kv"><span class="vf-k">' + esc(k) + '</span><span class="vf-v">' + v + '</span></div>';
    const section = (icon, title, inner) => '<div class="vf-sec"><div class="vf-sec-title"><i class="bx ' + icon + '"></i> ' + esc(title) + '</div>' + inner + '</div>';

    const renderQuickView = (row, d) => {
        const l = L();
        const loc = row.location || {};
        const t = row.target || {};
        const sub = byId('oc-subtitle');
        if (sub) { sub.innerHTML = F.bidi(row.repName || row.resourceId) + ' · ' + esc(startDay(row.startedAt) + ' ' + timeText(row.startedAt)) + ' · ' + esc(l['Kind_' + row.kind] || row.kind); }
        const target = byId('oc-target');
        if (target) { target.innerHTML = t.type === 'contact' ? F.bidi(t.accountName || '—') + ' · ' + F.bidi(t.name || '') : F.bidi(t.name || t.accountName || '—'); }
        const remote = row.kind === 'remote';
        const distance = remote || loc.distanceMeters == null ? (loc.status === 'account_unknown' ? esc(l.LocNotMeasured) : '—') : esc(valueText(C.distance(loc.distanceMeters)));
        const accuracy = remote || loc.accuracyMeters == null ? '—' : esc(T('Accuracy', F.number(loc.accuracyMeters, 0)));
        const qr = row.qr;
        const dev = C.device(row);
        const exception = d ? d.exception : null;
        const qrRes = esc(l[C.qrResult(qr, row.kind, exception)] || '') + (qr && qr.status === 'ok' && qr.matchedVia === 'parent' ? ' <span class="vf-mu">· ' + esc(l.QrParent) + '</span>' : '');
        let photo;
        if (!row.hasPhoto) { photo = '<span class="vf-mu">' + esc(l.PhotoNone) + '</span>'; }
        else if (perms.photos) {
            photo = '<img class="vf-photo" alt="' + esc(l.PhotoAlt) + '" src="' + esc(endpoint + '/flags/' + encodeURIComponent(row.executionId) + '/photo') + '" />'
                + (d && d.photoCapturedAt ? '<span class="vf-sub">' + esc(T('PhotoCaption', timeText(d.photoCapturedAt))) + '</span>' : '');
        } else { photo = '<div class="vf-photo-none"><i class="bx bx-image"></i> ' + esc(l.PhotoNoPermission) + '</div>'; }
        const slides = d && d.slideCount ? esc(T('SlidesValue', valueText(C.durationExact(d.slideSeconds)), F.number(d.slideCount, 0))) : '—';
        const body = byId('oc-details-list');
        if (body) {
            body.innerHTML = '<div class="vf-chips vf-chips-row">' + C.chipsOf(row).map(chipHtml).join('') + '</div>'
                + section('bx-map-pin', l.SecLocation, kv(l.LocDistance, distance) + kv(l.LocAccuracy, accuracy) + kv(l.LocRadius, remote ? '—' : esc(T('UnitMeters', F.number(loc.radiusMeters, 0)))))
                + section('bx-qr', l.SecQr, kv(l.QrResult, qrRes)
                    + kv(l.QrInstitution, qr && qr.accountName ? F.bidi(qr.accountName) : '—')
                    + kv(l.QrVersion, qr && qr.version ? esc(T('QrVersionValue', F.number(qr.version, 0))) : '—')
                    + kv(l.QrTime, qr && qr.scannedAt ? esc(timeText(qr.scannedAt)) : '—'))
                + section('bx-mobile-alt', l.SecDevice, kv(l.DevPlatform, dev.platformKey ? esc(l[dev.platformKey] || '') + (dev.platformExtra ? ' · ' + F.bidi(dev.platformExtra) : '') : '—')
                    + kv(l.DevApp, dev.appKey ? esc(l[dev.appKey] || '') : dev.appVersion ? F.bidi(dev.appVersion) : '—')
                    + kv(l.DevVerdict, esc(l[dev.verdictKey] || '')))
                + section('bx-time-five', l.SecDuration, kv(l.DurVisit, esc(durationText(row.durationSeconds))) + kv(l.DurSlides, slides))
                + section('bx-image', l.SecPhoto, '<div class="d-flex flex-column gap-1">' + photo + '</div>')
                + (exception ? section('bx-shield-quarter', l.SecException, kv(l.ExRule, esc((exception.rules || []).map((k) => l['XRule_' + k] || k).join(', ')))
                    + kv(l.ExRange, esc(rangeText(exception.startDate, exception.endDate)))
                    + (exception.approvedAt ? kv(l.ExApproved, esc(dayText(exception.approvedAt))) : '')
                    + '<a class="vf-link" href="' + esc(exceptionUrl + '?exception=' + encodeURIComponent(exception.id)) + '">' + esc(l.ExOpen) + '</a>') : '');
        }
        const open = byId('oc-btn-edit');
        if (open) { open.setAttribute('href', visitUrl + '?plannedVisitId=' + encodeURIComponent(row.plannedVisitId || '')); }
    };

    /** The quick view draws the row at once, then the visit's detail (slides, exception, photo time) when it arrives. */
    const populateQuickView = (row) => {
        if (!row) { return; }
        const token = ++quickToken;
        renderQuickView(row, null);
        call('/flags/' + encodeURIComponent(row.executionId)).then((res) => {
            const d = data(res);
            if (d && token === quickToken) { renderQuickView(d.row || row, d); }
        });
    };

    const rowActionHandlers = {
        open: ({ row }) => { if (row && row.plannedVisitId) { window.location.assign(visitUrl + '?plannedVisitId=' + encodeURIComponent(row.plannedVisitId)); } }
    };

    const initDataTable = async () => {
        if (!dtTableEl || !C || !F || !window.DitenDataTable) return;

        onChange('filterPeriod', syncRange);
        onChange('filterScope', syncSwitchFromScope);
        byId('vf-only')?.addEventListener('change', syncScopeFromSwitch);
        byId('btnFilterApply')?.addEventListener('click', guardApply, true);
        // the custom range is the shared date field: flatpickr in the page's language, the calendar icon OPENS it
        if (window.DitenDateField && byId('filterRange')) { window.DitenDateField.enhance(byId('filterRange')); }
        repSummary = window.VisitEvidenceFlagsRepSummary?.create({
            tableEl: repsTableEl,
            endpoint: endpoint,
            onSummary: summaryDataSrc,
            onRep: filterToRep,
            cells: {
                esc: esc,
                num: num,
                rep: repHtml,
                rate: (started, flagged) => esc(pct(C.repRate(started, flagged))),
                rateValue: (started, flagged) => C.repRate(started, flagged),
                topFlag: (code) => chipHtml(Object.assign(C.chipsOf({ flags: [code] })[0], { value: null }))
            }
        }) || null;

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: endpoint + '/flags', type: 'GET' },
            // BL-452: CSV/Excel = every matching visit with the visible columns and the filters, not the page on screen.
            export: { mode: 'server', url: endpoint + '/flags/export', fileName: 'visit-evidence-flags' },
            onResponse: onListResponse,
            actions: { onRowAction: rowActionHandlers },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: { addNewText: '', exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'CRM', pageKey: 'VisitEvidenceFlags', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [] },
            config: {
                order: [],
                // the CRM has no text search over the visits: the toolbar search is not drawn (nothing could honour it)
                searching: false,
                drawCallback: drawEmptyState,
                columns: [
                    { data: 'executionId', name: 'control' },
                    { data: 'startedAt', name: 'startedAt' },
                    { data: 'repName', name: 'repName' },
                    { data: 'target', name: 'target' },
                    { data: 'kind', name: 'kind' },
                    { data: 'flags', name: 'flags' },
                    { data: 'durationSeconds', name: 'durationSeconds' },
                    { data: 'exceptionId', name: 'exceptionId' },
                    { data: 'executionId', name: 'action' }
                ],
                columnDefs: [
                    // the CRM orders by the start, newest first: no column sorts (the factory then never sends orderBy)
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1,
                        render: (d, type, full) => (type === 'display'
                            ? '<div class="d-flex flex-column"><span class="fw-medium text-heading text-nowrap">' + esc(startDay(full.startedAt)) + '</span><small class="text-muted">' + esc(timeText(full.startedAt)) + '</small></div>'
                            : (full.startedAt || ''))
                    },
                    { targets: 2, render: (d, type, full) => (type === 'display' ? repHtml(full.repName, full.resourceId) : (full.repName || full.resourceId || '')) },
                    { targets: 3, render: (d, type, full) => (type === 'display' ? targetHtml(full.target) : ((full.target || {}).accountName || (full.target || {}).name || '')) },
                    { targets: 4, render: (d, type, full) => '<span class="d-inline-flex align-items-center gap-1 text-nowrap"><i class="bx ' + (full.kind === 'remote' ? 'bx-video' : 'bx-user') + ' text-muted"></i>' + esc(L()['Kind_' + full.kind] || full.kind || '') + '</span>' },
                    { targets: 5, render: (d, type, full) => '<div class="vf-chips">' + C.chipsOf(full).map(chipHtml).join('') + '</div>' },
                    { targets: 6, className: 'text-nowrap', render: (d, type, full) => esc(durationText(full.durationSeconds)) },
                    {
                        targets: 7,
                        render: (d, type, full) => (full.exceptionId
                            ? '<a class="vf-link" href="' + esc(exceptionUrl + '?exception=' + encodeURIComponent(full.exceptionId)) + '">' + esc(L().ExceptionLink) + '</a>'
                            : '<span class="text-muted">—</span>')
                    },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        className: 'cell-fit all',
                        render: (d, type, full) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.executionId, title: L().QuickView, 'aria-label': L().QuickView } },
                            { key: 'open', icon: 'bx bx-right-arrow-alt', text: L().OpenVisit, visible: !!full.plannedVisitId, attrs: { 'data-id': full.plannedVisitId } }
                        ])
                    }
                ]
            }
        });
        syncSummary();
    };

    return {
        init: function () {
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => VisitEvidenceFlagsList.init());
