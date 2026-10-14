/**
 * WP-VW-W3E (W3E-WEB-b) — the manager's visit evidence flags page (mockup "W3E Ek Kanitlar" M1–M6). Every rule lives in
 * flags-core.js (pure, tested in Node); every text comes from window.VisitEvidenceFlagsL10n (7 languages) or from the CRM
 * (rep, doctor and institution names). Dates / numbers in the APPLICATION language through VisitPlanningFormat; every data
 * name isolated (bidi) for RTL. Read only: no call writes. Calls go to the same-origin proxy /CRM/VisitEvidenceFlags/api/*.
 *   M1 the summary strip + the filters + the visits list; M2 the right drawer; M3 the rep summary (a row filters the list);
 *   M4 the empty state; M5 is the view's access notice (UAS-001); M6 the same page in Arabic (dir=rtl).
 */
(function (window, document) {
    'use strict';

    const C = window.VisitEvidenceFlagsCore;
    const F = window.VisitPlanningFormat;
    const L = window.VisitEvidenceFlagsL10n || {};
    const root = document.getElementById('vf-root');
    if (!C || !F || !root) { return; }

    const base = root.getAttribute('data-api-base') || '/CRM/VisitEvidenceFlags/api';
    const perms = {
        readAll: root.getAttribute('data-can-read-all') === 'true',
        photos: root.getAttribute('data-can-see-photos') === 'true'
    };
    const visitUrl = root.getAttribute('data-visit-url') || '/CRM/VisitReport';
    const exceptionUrl = root.getAttribute('data-exception-url') || '/CRM/VisitEvidenceSettings';

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L[key] || '', ...args);
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    const setText = (id, t) => { const n = el(id); if (n) { n.textContent = t; } };
    const drawer = () => (window.bootstrap && el('vf-drawer') ? window.bootstrap.Offcanvas.getOrCreateInstance(el('vf-drawer')) : null);
    const culture = () => F.dateCulture();
    const dtf = (o) => { try { return new Intl.DateTimeFormat(culture(), Object.assign({ timeZone: 'UTC' }, o)); } catch (e) { return new Intl.DateTimeFormat('en-GB', o); } };
    const dayText = iso => (iso ? dtf({ day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(iso)) : '—');
    const timeText = iso => (iso ? new Intl.DateTimeFormat(culture(), { hour: '2-digit', minute: '2-digit' }).format(new Date(iso)) : '—');
    const startDay = iso => (iso ? new Intl.DateTimeFormat(culture(), { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(iso)) : '—');
    const rangeText = (from, to, short) => {
        if (!from || !to) { return ''; }
        const f = dtf(short ? { day: 'numeric', month: 'short' } : { day: 'numeric', month: 'short', year: 'numeric' }); // short: the period option "8–14 Eki" (the mockup)
        try { return f.formatRange(new Date(from + 'T00:00:00Z'), new Date(to + 'T00:00:00Z')); } catch (e) { return f.format(new Date(from + 'T00:00:00Z')) + ' – ' + f.format(new Date(to + 'T00:00:00Z')); }
    };
    const monthText = today => dtf({ month: 'long', year: 'numeric' }).format(new Date(today + 'T00:00:00Z'));
    const num = n => F.isolate(F.number(n, 0));
    const pct = n => { let v; try { v = new Intl.NumberFormat(culture(), { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(n); } catch (e) { v = String(n); } return T('Percent', v); }; // %9,0 (the mockup)
    const valueText = v => (v ? T(v.key, ...v.args.map(a => F.number(a, 0))) : '');
    const durationText = s => (s == null ? '—' : valueText(C.duration(s)));
    const today = () => new Date(Date.now()).toISOString().slice(0, 10);

    const call = path => fetch(base + path, { method: 'GET', credentials: 'same-origin', headers: { Accept: 'application/json' } })
        .then(r => r.text().then(t => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));
    const data = r => (r && r.ok && r.body ? r.body.data : null);
    const errText = r => L[C.errorKey(r)] || L.Err_generic || '';

    // ── state ────────────────────────────────────────────────────────────────────────────────────────────────
    const state = {
        period: 'last7', custom: { from: '', to: '' }, resourceId: '', flags: [], accountId: '', onlyFlagged: true,
        page: 1, tab: 'visits', list: null, summary: null, accounts: {}, current: null, menuOpen: false
    };
    const range = () => C.rangeOf(state.period, today(), state.custom);
    const filters = () => { const r = range(); return { from: r.from, to: r.to, resourceId: state.resourceId, flags: state.flags, accountId: state.accountId, onlyFlagged: state.onlyFlagged }; };

    // ── load ─────────────────────────────────────────────────────────────────────────────────────────────────
    function load(withSummary) {
        const r = range();
        const bad = state.period === 'custom' ? C.rangeError(r.from, r.to) : null;
        const re = el('vf-range-error');
        if (re) { re.textContent = bad ? T(bad, C.MAX_RANGE_DAYS) : ''; re.classList.toggle('d-none', !bad); }
        if (bad) { return Promise.resolve(); } // a range over 92 days is never sent (the CRM refuses it too)
        show('vf-error', false);
        const jobs = [call('/flags' + C.listQuery(filters(), state.page)).then(res => {
            const d = data(res);
            if (!d) { showError(res); return; }
            state.list = d;
            (d.items || []).forEach(row => { const t = row.target || {}; if (t.accountId && (t.accountName || t.name)) { state.accounts[t.accountId] = t.accountName || t.name; } });
        })];
        if (withSummary !== false) {
            jobs.push(call('/summary' + C.summaryQuery(filters())).then(res => { const d = data(res); if (!d) { showError(res); return; } state.summary = d; }));
        }
        return Promise.all(jobs).then(render);
    }

    function showError(res) {
        const n = el('vf-error');
        if (n) { n.textContent = errText(res); n.classList.remove('d-none'); }
    }

    // ── render ───────────────────────────────────────────────────────────────────────────────────────────────
    function render() {
        renderFilters();
        renderSummary();
        renderRows();
        renderReps();
    }

    function renderFilters() {
        const periods = el('vf-period');
        if (periods) {
            const r = C.rangeOf('last7', today());
            periods.innerHTML = C.PERIODS.map(p => '<option value="' + p + '"' + (state.period === p ? ' selected' : '') + '>'
                + esc(p === 'last7' ? T('PeriodLast7', rangeText(r.from, r.to, true)) : p === 'last30' ? L.PeriodLast30 : p === 'month' ? monthText(today()) : L.PeriodCustom) + '</option>').join('');
        }
        show('vf-custom', state.period === 'custom');
        const reps = (state.summary && state.summary.reps) || [];
        const rep = el('vf-rep');
        if (rep) {
            rep.innerHTML = '<option value="">' + esc(T(perms.readAll ? 'RepAllTenant' : 'RepAllTeam', F.number(reps.length, 0))) + '</option>'
                + reps.map(x => '<option value="' + esc(x.resourceId) + '"' + (state.resourceId === x.resourceId ? ' selected' : '') + '>' + esc(F.isolate(x.repName || x.resourceId)) + '</option>').join('')
                + (state.resourceId && !reps.some(x => x.resourceId === state.resourceId) ? '<option value="' + esc(state.resourceId) + '" selected>' + esc(state.resourceId) + '</option>' : '');
        }
        setText('vf-flag-text', state.flags.length ? T('FlagSome', F.number(state.flags.length, 0)) : (L.FlagAll || ''));
        const menu = el('vf-flag-menu');
        if (menu) {
            // only the REAL flags: "QR not scanned" is not a flag and is never offered here (D-6)
            menu.innerHTML = C.FLAGS.map(f => '<label class="vf-check"><input type="checkbox" data-flagtype="' + f.code + '"' + (state.flags.indexOf(f.code) > -1 ? ' checked' : '') + '> '
                + '<i class="bx ' + C.CATS[f.cat].icon + ' vf-tone-text-' + C.CATS[f.cat].tone + '"></i> ' + esc(L['Flag_' + f.code]) + '</label>').join('');
            menu.classList.toggle('d-none', !state.menuOpen);
            const btn = el('vf-flag-btn'); if (btn) { btn.setAttribute('aria-expanded', String(state.menuOpen)); }
        }
        const acc = el('vf-account');
        if (acc) {
            const ids = Object.keys(state.accounts).sort((a, b) => String(state.accounts[a]).localeCompare(String(state.accounts[b]), culture()));
            acc.innerHTML = '<option value="">' + esc(L.AccountAll) + '</option>'
                + ids.map(id => '<option value="' + esc(id) + '"' + (state.accountId === id ? ' selected' : '') + '>' + esc(F.isolate(state.accounts[id])) + '</option>').join('');
        }
        const only = el('vf-only'); if (only) { only.checked = state.onlyFlagged; }
    }

    function renderSummary() {
        const s = state.summary;
        if (!s) { return; }
        setText('vf-sum-started', F.number(s.started, 0));
        const r = range();
        setText('vf-sum-range', (state.period === 'last7' ? (L.PeriodLast7Short || '') + ' · ' : state.period === 'last30' ? (L.PeriodLast30 || '') + ' · ' : '') + rangeText(r.from, r.to));
        setText('vf-sum-flagged', F.number(s.flagged, 0));
        setText('vf-sum-rate', pct(C.repRate(s.started, s.flagged)));
        const b = C.summaryBars(s.byFlag, 6);
        const bar = (label, count, width) => '<div class="vf-bar-row"><span class="vf-bar-label">' + esc(label) + '</span><span class="vf-bar"><span style="width:' + width + '%"></span></span><span class="vf-bar-count">' + num(count) + '</span></div>';
        el('vf-bars').innerHTML = b.bars.map(x => bar(L[x.key] || x.code, x.count, x.pct)).join('')
            + (b.other ? bar(T('SumOther', F.number(b.other.types, 0)), b.other.count, b.other.pct) : '')
            + (!b.bars.length ? '<span class="vf-sub">' + esc(L.SumNoFlags) + '</span>' : '');
    }

    const chipHtml = c => {
        const cat = C.CATS[c.cat] || C.CATS.exc;
        return '<span class="vf-chip vf-tone-' + cat.tone + (c.flag ? '' : ' vf-chip-info') + '"><i class="bx ' + cat.icon + '"></i>' + esc(L[c.key] || c.code) + (c.value ? ' · ' + esc(valueText(c.value)) : '') + '</span>';
    };

    const targetHtml = t => {
        const target = t || {};
        const doctor = target.type === 'contact';
        const inst = doctor ? (target.accountName || '—') : (target.name || target.accountName || '—');
        return '<div class="vf-col"><span class="vf-strong">' + F.bidi(inst) + '</span>' + (doctor && target.name ? '<span class="vf-sub">' + F.bidi(target.name) + '</span>' : '') + '</div>';
    };

    // the mockup's avatars: one stable tone per rep (primary · info · warning)
    const AV_TONES = ['primary', 'info', 'warning'];
    const avatar = (name, id) => {
        const key = String(id || name || '');
        let h = 0; for (let i = 0; i < key.length; i++) { h = (h * 31 + key.charCodeAt(i)) >>> 0; }
        return '<span class="vf-avatar vf-av-' + AV_TONES[h % AV_TONES.length] + '">' + esc(C.initials(name)) + '</span>';
    };

    function renderRows() {
        const list = state.list || { items: [], total: 0, page: 1 };
        const rows = list.items || [];
        el('vf-rows').innerHTML = rows.map(row => '<tr class="vf-click" data-open="' + esc(row.executionId) + '">'
            + '<td><div class="vf-col"><span class="vf-strong-n">' + esc(startDay(row.startedAt)) + '</span><span class="vf-sub">' + esc(timeText(row.startedAt)) + '</span></div></td>'
            + '<td><div class="vf-row">' + avatar(row.repName, row.resourceId) + '<span class="vf-nowrap">' + F.bidi(row.repName || row.resourceId) + '</span></div></td>'
            + '<td>' + targetHtml(row.target) + '</td>'
            + '<td><span class="vf-row vf-nowrap"><i class="bx ' + (row.kind === 'remote' ? 'bx-video' : 'bx-user') + ' vf-mu"></i> ' + esc(L['Kind_' + row.kind] || row.kind) + '</span></td>'
            + '<td><div class="vf-chips">' + C.chipsOf(row).map(chipHtml).join('') + '</div></td>'
            + '<td class="vf-nowrap">' + esc(durationText(row.durationSeconds)) + '</td>'
            + '<td>' + (row.exceptionId ? '<a class="vf-link" href="' + esc(exceptionUrl + '?exception=' + encodeURIComponent(row.exceptionId)) + '" data-stop="1">' + esc(L.ExceptionLink) + '</a>' : '<span class="vf-mu">—</span>') + '</td></tr>').join('');
        const empty = rows.length === 0;
        show('vf-empty', empty);
        show('vf-pager', !empty);
        const table = el('vf-rows').closest ? el('vf-rows').closest('table') : null;
        if (table) { table.classList.toggle('vf-table-empty', empty); }
        setText('vf-empty-title', state.onlyFlagged ? L.EmptyTitle : L.EmptyAllTitle);
        setText('vf-empty-text', L.EmptyText);
        const pr = C.pageRange(list.total, list.page || state.page, C.PAGE_SIZE);
        setText('vf-page-text', T(state.onlyFlagged ? 'PageFlagged' : 'PageAll', F.number(pr.total, 0), F.number(pr.from, 0), F.number(pr.to, 0)));
        const pages = C.pageCount(list.total, C.PAGE_SIZE);
        const current = list.page || state.page;
        const first = Math.max(1, Math.min(current - 3, pages - 6));
        const shown = []; for (let p = first; p <= Math.min(pages, first + 6); p++) { shown.push(p); }
        el('vf-pages').innerHTML = pages > 1 ? shown.map(p => '<button type="button" class="vf-page' + (p === current ? ' active' : '') + '" data-page="' + p + '"' + (p === current ? ' aria-current="page"' : '') + '>' + esc(F.number(p, 0)) + '</button>').join('') : '';
    }

    function renderReps() {
        const reps = (state.summary && state.summary.reps) || [];
        el('vf-rep-rows').innerHTML = reps.length
            ? reps.map(r => '<tr class="vf-click" data-rep="' + esc(r.resourceId) + '">'
                + '<td><div class="vf-row">' + avatar(r.repName, r.resourceId) + '<span class="vf-nowrap">' + F.bidi(r.repName || r.resourceId) + '</span></div></td>'
                + '<td>' + num(r.started) + '</td><td>' + num(r.flagged) + '</td><td>' + esc(pct(C.repRate(r.started, r.flagged))) + '</td>'
                + '<td>' + (r.topFlag ? chipHtml(Object.assign(C.chipsOf({ flags: [r.topFlag] })[0], { value: null })) : '<span class="vf-mu">—</span>') + '</td>'
                + '<td class="vf-chev"><i class="bx bx-chevron-right vf-flip vf-mu"></i></td></tr>').join('')
            : '<tr><td colspan="6" class="vf-mu vf-pad">' + esc(L.RepsEmpty) + '</td></tr>';
    }

    function setTab(tab) {
        state.tab = tab;
        document.querySelectorAll('.vf-tab').forEach(b => { const on = b.getAttribute('data-tab') === tab; b.classList.toggle('active', on); b.setAttribute('aria-selected', String(on)); });
        show('vf-pane-visits', tab === 'visits');
        show('vf-pane-reps', tab === 'reps');
    }

    // ── M2 — the drawer ──────────────────────────────────────────────────────────────────────────────────────
    function openDrawer(id) {
        call('/flags/' + encodeURIComponent(id)).then(res => {
            const d = data(res);
            if (!d) { showError(res); return; }
            state.current = d;
            renderDrawer();
            const dr = drawer(); if (dr) { dr.show(); }
        });
    }

    const kv = (k, v) => '<div class="vf-kv"><span class="vf-k">' + esc(k) + '</span><span class="vf-v">' + v + '</span></div>';
    const section = (icon, title, inner) => '<div class="vf-sec"><div class="vf-sec-title"><i class="bx ' + icon + '"></i> ' + esc(title) + '</div>' + inner + '</div>';

    function renderDrawer() {
        const d = state.current; if (!d) { return; }
        const row = d.row || {};
        const loc = row.location || {};
        const t = row.target || {};
        el('vf-drawer-sub').innerHTML = F.bidi(row.repName || row.resourceId) + ' · ' + esc(startDay(row.startedAt) + ' ' + timeText(row.startedAt)) + ' · ' + esc(L['Kind_' + row.kind] || row.kind);
        el('vf-drawer-target').innerHTML = t.type === 'contact' ? F.bidi(t.accountName || '—') + ' · ' + F.bidi(t.name || '') : F.bidi(t.name || t.accountName || '—');
        const remote = row.kind === 'remote';
        const distance = remote || loc.distanceMeters == null ? (loc.status === 'account_unknown' ? esc(L.LocNotMeasured) : '—') : esc(T('UnitMeters', F.number(loc.distanceMeters, 0)));
        const accuracy = remote || loc.accuracyMeters == null ? '—' : esc(T('Accuracy', F.number(loc.accuracyMeters, 0)));
        const qr = row.qr;
        const dev = C.device(row);
        const qrRes = esc(L[C.qrResult(qr, row.kind, d.exception)] || '') + (qr && qr.status === 'ok' && qr.matchedVia === 'parent' ? ' <span class="vf-mu">· ' + esc(L.QrParent) + '</span>' : '');
        let photo;
        if (!row.hasPhoto) { photo = '<span class="vf-mu">' + esc(L.PhotoNone) + '</span>'; }
        else if (perms.photos) {
            photo = '<img class="vf-photo" alt="' + esc(L.PhotoAlt) + '" src="' + esc(base + '/flags/' + encodeURIComponent(row.executionId) + '/photo') + '" />'
                + (d.photoCapturedAt ? '<span class="vf-sub">' + esc(T('PhotoCaption', timeText(d.photoCapturedAt))) + '</span>' : '');
        } else { photo = '<div class="vf-photo-none"><i class="bx bx-image"></i> ' + esc(L.PhotoNoPermission) + '</div>'; }
        const x = d.exception;
        el('vf-drawer-body').innerHTML = '<div class="vf-chips">' + C.chipsOf(row).map(chipHtml).join('') + '</div>'
            + section('bx-map-pin', L.SecLocation, kv(L.LocDistance, distance) + kv(L.LocAccuracy, accuracy) + kv(L.LocRadius, remote ? '—' : esc(T('UnitMeters', F.number(loc.radiusMeters, 0)))))
            + section('bx-qr', L.SecQr, kv(L.QrResult, qrRes)
                + kv(L.QrInstitution, qr && qr.accountName ? F.bidi(qr.accountName) : '—')
                + kv(L.QrVersion, qr && qr.version ? esc(T('QrVersionValue', F.number(qr.version, 0))) : '—')
                + kv(L.QrTime, qr && qr.scannedAt ? esc(timeText(qr.scannedAt)) : '—'))
            + section('bx-mobile-alt', L.SecDevice, kv(L.DevPlatform, dev.platformKey ? esc(L[dev.platformKey] || '') + (dev.platformExtra ? ' · ' + F.bidi(dev.platformExtra) : '') : '—')
                + kv(L.DevApp, dev.appKey ? esc(L[dev.appKey] || '') : dev.appVersion ? F.bidi(dev.appVersion) : '—')
                + kv(L.DevVerdict, esc(L[dev.verdictKey] || '')))
            + section('bx-time-five', L.SecDuration, kv(L.DurVisit, esc(durationText(row.durationSeconds)))
                + kv(L.DurSlides, d.slideCount ? esc(T('SlidesValue', valueText(C.durationExact(d.slideSeconds)), F.number(d.slideCount, 0))) : '—'))
            + section('bx-image', L.SecPhoto, '<div class="vf-col vf-gap-xs">' + photo + '</div>')
            + (x ? section('bx-shield-quarter', L.SecException, kv(L.ExRule, esc((x.rules || []).map(k => L['XRule_' + k] || k).join(', ')))
                + kv(L.ExRange, esc(rangeText(x.startDate, x.endDate)))
                + (x.approvedAt ? kv(L.ExApproved, esc(dayText(x.approvedAt))) : '')
                + '<a class="vf-link" href="' + esc(exceptionUrl + '?exception=' + encodeURIComponent(x.id)) + '">' + esc(L.ExOpen) + '</a>') : '');
        const open = el('vf-open-visit');
        if (open) { open.setAttribute('href', visitUrl + '?plannedVisitId=' + encodeURIComponent(row.plannedVisitId || '')); }
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    const on = (id, ev, fn) => { const n = el(id); if (n) { n.addEventListener(ev, fn); } };
    const refilter = () => { state.page = 1; load(true); };

    root.addEventListener('click', e => {
        const tab = e.target.closest('[data-tab]');
        if (tab) { setTab(tab.getAttribute('data-tab')); return; }
        if (e.target.closest('[data-stop]')) { return; } // the exception link opens its page, not the drawer
        const page = e.target.closest('[data-page]');
        if (page) { state.page = parseInt(page.getAttribute('data-page'), 10) || 1; load(false); return; }
        const rep = e.target.closest('[data-rep]');
        if (rep) { state.resourceId = rep.getAttribute('data-rep'); setTab('visits'); refilter(); return; } // M3: the row filters the list
        if (e.target.closest('#vf-flag-btn')) { state.menuOpen = !state.menuOpen; renderFilters(); return; }
        const row = e.target.closest('[data-open]');
        if (row) { openDrawer(row.getAttribute('data-open')); }
    });
    root.addEventListener('change', e => {
        const type = e.target.closest && e.target.closest('[data-flagtype]');
        if (type) {
            const code = type.getAttribute('data-flagtype');
            state.flags = type.checked ? state.flags.concat([code]) : state.flags.filter(c => c !== code);
            refilter();
        }
    });
    on('vf-period', 'change', e => {
        state.period = e.target.value;
        if (state.period === 'custom' && !state.custom.from) { const r = C.rangeOf('last30', today()); state.custom = { from: r.from, to: r.to }; el('vf-from').value = r.from; el('vf-to').value = r.to; }
        refilter();
    });
    on('vf-from', 'change', e => { state.custom.from = e.target.value; refilter(); });
    on('vf-to', 'change', e => { state.custom.to = e.target.value; refilter(); });
    on('vf-rep', 'change', e => { state.resourceId = e.target.value; refilter(); });
    on('vf-account', 'change', e => { state.accountId = e.target.value; state.page = 1; load(false); });
    on('vf-only', 'change', e => { state.onlyFlagged = !!e.target.checked; state.page = 1; load(false); });

    window.VisitEvidenceFlagsPage = { state: state, load: load, openDrawer: openDrawer, setTab: setTab };
    setTab('visits');
    load(true);
}(window, document));
