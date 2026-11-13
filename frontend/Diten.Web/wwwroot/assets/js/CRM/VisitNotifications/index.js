/**
 * WP-VW-W5 · W5-WEB — the sent visit e-mails (mockup 06-epostalar) on the Golden Compact list standard.
 *
 * THE LIST IS THE SHARED COMPONENT (data_mode: server): Save View, column state, the inline filter, paging, the skeleton,
 * the export and the quick view's open / close come from DitenDataTable.createList. The factory sends start / length /
 * draw + each applied filter by its key (period, from, to, event, status, resourceId) to the same-origin proxy, which turns
 * it into the CRM's own query (the CRM orders newest first: no column sorts, no orderBy, no search).
 *
 * THE QUICK VIEW IS THE E-MAIL PREVIEW (06): the header (From · To · Cc · Date), the band, the title / intro, the rows, the
 * sections, the note, the button and the KVKK footer — the content from the e-mail's template variables (the Platform
 * template's words live in the Platform; this is the page's rendering of the same fields). The recipients are ROLES and
 * the rep's name — NEVER an e-mail address: the CRM stores none, the proxy passes none, and an address typed into a free
 * text is masked (VisitNotificationsCore.maskEmails). Every rule is VisitNotificationsCore's (notifications-core.js,
 * Node-tested); every word comes from window.L10n (7 languages); every data name is isolated (bidi) for RTL. Read only.
 */
'use strict';

const VisitNotificationsList = (function () {
    let list = null;
    let quickToken = 0;

    const dtTableEl = document.querySelector('.datatables-visitnotifications');
    const root = document.getElementById('vn-root');
    const endpoint = '/CRM/VisitNotifications/api';
    const C = window.VisitNotificationsCore;
    const F = window.VisitPlanningFormat;
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const byId = (id) => document.getElementById(id);
    const canManage = !!root && root.getAttribute('data-can-manage') === 'true';
    const $ = window.jQuery;

    // control 0 · date 1 · event 2 · institution / doctor 3 · rep 4 · recipients 5 · status 6 · actions 7
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6];
    const EXPORT_COLUMNS = [1, 2, 4, 5, 6];                  // the institution / doctor is read per row on screen only

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const esc = (s) => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L()[key] || '', ...args);
    const culture = () => F.dateCulture();
    const dtf = (o, utc) => { try { return new Intl.DateTimeFormat(culture(), Object.assign(utc ? { timeZone: 'UTC' } : {}, o)); } catch (e) { return new Intl.DateTimeFormat('en-GB', o); } };
    const dayText = (iso) => (iso ? dtf({ day: 'numeric', month: 'short', year: 'numeric' }, true).format(new Date(String(iso).slice(0, 10) + 'T00:00:00Z')) : '');
    const momentText = (iso) => (iso ? dtf({ day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(iso)) : '');
    const timeText = (iso) => (iso ? dtf({ hour: '2-digit', minute: '2-digit' }).format(new Date(iso)) : '');
    const today = () => new Date(Date.now()).toISOString().slice(0, 10);
    const valueOf = (id) => { const n = byId(id); return n ? String(n.value || '') : ''; };
    const onChange = (id, fn) => { const n = byId(id); if (!n) { return; } if ($) { $(n).on('change', fn); } else { n.addEventListener('change', fn); } };
    const dates = { day: dayText, moment: momentText };

    const call = (path) => fetch(endpoint + path, { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
        .then((r) => r.text().then((t) => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));
    const data = (r) => (r && r.ok && r.body ? r.body.data : null);

    // ── the cells ────────────────────────────────────────────────────────────────────────────────────────────
    const eventHtml = (code) => {
        const e = C.eventOf(code);
        if (!e) { return '<span class="text-muted">' + esc(code || '—') + '</span>'; }
        return '<span class="d-inline-flex align-items-center gap-2 text-nowrap"><i class="bx ' + e.icon + ' text-' + e.tone + ' fs-5"></i><span class="text-heading">' + esc(L()['Event_' + e.key] || e.key) + '</span></span>';
    };
    const statusHtml = (status, notes) => {
        const s = C.statusOf(status, notes);
        // W5-FIX2 (F-6) — "sent" means handed over to the e-mail service, not delivered to the inbox: the badge says so on hover.
        const tip = L()['Explain_' + String(s.key).replace(/^St_/, '')] || '';
        return '<span class="badge bg-label-' + s.tone + ' d-inline-flex align-items-center gap-1 text-nowrap"' + (tip ? ' title="' + esc(tip) + '"' : '') + '><i class="bx ' + s.icon + '"></i>' + esc(L()[s.key] || s.code || '') + '</span>';
    };
    const targetHtml = (row) => {
        const t = row.target || {};
        const doctor = t.doctorName || '';
        const inst = t.institutionName || '';
        if (!doctor && !inst) { return '<span class="text-muted">—</span>'; }
        return '<div class="d-flex flex-column"><span class="text-heading">' + F.bidi(doctor || inst) + '</span>' + (doctor && inst ? '<small class="text-muted">' + F.bidi(inst) + '</small>' : '') + '</div>';
    };
    const recipientsCell = (row) => {
        const r = C.recipientsOf(row);
        if (r.none) { return '<span class="text-muted">' + esc(L().RcpNone) + '</span>'; }
        const to = r.to.map((x) => esc(L()[x.key] || ''));
        const cc = r.cc.map((x) => esc(T('CcSuffix', L()[x.key] || '')));
        return '<div class="d-flex flex-column"><span class="text-nowrap">' + to.concat(cc).join(' · ') + '</span>'
            + (r.pending.length ? '<small class="text-muted">' + esc(T('RcpPending', r.pending.map((x) => L()[x.key] || '').join(', '))) + '</small>' : '') + '</div>';
    };
    const dateCell = (row) => {
        const at = row.sentAt || row.createdAt;
        return '<div class="d-flex flex-column"><span class="fw-medium text-heading text-nowrap">' + esc(dayText(at)) + ' ' + esc(timeText(at)) + '</span>'
            + (row.visitDate ? '<small class="text-muted">' + esc(T('VisitDayLine', dayText(row.visitDate))) + '</small>' : '') + '</div>';
    };

    // ── the filter ───────────────────────────────────────────────────────────────────────────────────────────
    const reps = {};
    const filterFields = [
        { id: 'filterPeriod', key: 'period', kind: 'single' },
        { id: 'vn-from', key: 'from', kind: 'text' },
        { id: 'vn-to', key: 'to', kind: 'text' },
        { id: 'filterEvent', key: 'event', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' },
        { id: 'filterRep', key: 'resourceId', kind: 'single' }
    ];

    const monthText = () => dtf({ month: 'long', year: 'numeric' }, true).format(new Date(today() + 'T00:00:00Z'));
    const loadLookupOptions = async ({ fillSelect }) => {
        fillSelect(byId('filterPeriod'), [{ value: 'last7', text: L().PeriodLast7 }, { value: 'month', text: monthText() }, { value: 'custom', text: L().PeriodCustom }],
            { showAllText: L().PeriodLast30 });
        fillSelect(byId('filterEvent'), C.EVENTS.map((e) => ({ value: e.key, text: L()['Event_' + e.key] || e.key })), { showAllText: L().EventAll });
        fillSelect(byId('filterStatus'), C.STATUS_CODES.map((s) => ({ value: s, text: L()[C.STATUSES[s].key] || s })), { showAllText: L().StatusAll });
        fillSelect(byId('filterRep'), [], { showAllText: L().RepAll });
    };

    // the rep options come from the rows the list has shown (a value the options lack is kept as its own option)
    const refillReps = () => {
        const el = byId('filterRep');
        if (!el) { return; }
        const current = $ ? $(el).val() : el.value;
        el.innerHTML = '';
        const add = (value, text) => { const o = document.createElement('option'); o.value = value; o.textContent = text; el.appendChild(o); };
        add('', L().RepAll || '');
        Object.keys(reps).sort((a, b) => String(reps[a]).localeCompare(String(reps[b]), culture())).forEach((id) => add(id, F.isolate(reps[id])));
        if (current && !reps[current]) { add(current, current); }
        if ($) { $(el).val(current || '').trigger('change.select2'); } else { el.value = current || ''; }
        if (Object.keys(reps).length > 1) { byId('filterRepChip')?.classList.remove('d-none'); }
    };

    const onListResponse = (json) => {
        const items = (json && json.data && Array.isArray(json.data.items)) ? json.data.items : [];
        let added = false;
        items.forEach((row) => {
            if (row && row.resourceId && !reps[row.resourceId]) { reps[row.resourceId] = row.resourceName || row.resourceId; added = true; }
        });
        if (added) { refillReps(); }
    };

    // the custom range is shown only for "custom"; a preset clears it (a stale range never travels with a preset)
    const syncRange = () => {
        const custom = valueOf('filterPeriod') === 'custom';
        ['filterRangeFrom', 'filterRangeTo'].forEach((id) => byId(id)?.classList.toggle('d-none', !custom));
        if (!custom) {
            ['vn-from', 'vn-to'].forEach((id) => { const n = byId(id); if (n && n.value) { if (n._flatpickr) { n._flatpickr.clear(); } n.value = ''; } });
        }
    };
    const markRange = (bad) => ['vn-from', 'vn-to'].forEach((id) => {
        const n = byId(id);
        if (!n) { return; }
        n.classList.toggle('is-invalid', !!bad);
        n._flatpickr?.altInput?.classList.toggle('is-invalid', !!bad);
    });
    const enhanceRange = () => {
        const altFormat = L().DateFormat || 'Y-m-d';
        ['vn-from', 'vn-to'].forEach((id) => {
            const n = byId(id);
            if (!n || typeof n.flatpickr !== 'function' || n._flatpickr) { return; }
            n.flatpickr({ monthSelectorType: 'static', dateFormat: 'Y-m-d', altInput: true, altFormat: altFormat, allowInput: true });
            window.DitenDateField?.guardAgainstSilentMisparse?.(n._flatpickr, 'altFormat');
            const icon = n.closest ? n.closest('.diten-field')?.querySelector('.diten-field-icon') : null;
            icon?.addEventListener('click', () => n._flatpickr?.open());
        });
    };
    const syncRangeField = (event) => {
        const n = event && event.target;
        const fp = n && n._flatpickr;
        if (!fp) { return; }
        if (n.value) { fp.setDate(n.value, false); } else if (fp.selectedDates && fp.selectedDates.length) { fp.clear(false); }
    };
    /** A custom range the proxy would refuse is never sent: the capture-phase guard runs before the factory's Apply. */
    const guardApply = (event) => {
        const period = valueOf('filterPeriod');
        if (period !== 'custom') { syncRange(); }
        const bad = C.customRangeError(period, valueOf('vn-from'), valueOf('vn-to'));
        markRange(bad);
        if (!bad) { return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        window.showToast?.(L()[bad] || '', 'warning');
    };

    // ── the empty state ──────────────────────────────────────────────────────────────────────────────────────
    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) { return; }
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = '<div class="vn-empty"><i class="bx bx-envelope"></i><strong>' + esc(L().EmptyTitle) + '</strong><span class="text-muted">' + esc(L().EmptyText) + '</span></div>'; }
    }

    // ── the quick view: the e-mail preview (06) ─────────────────────────────────────────────────────────────
    const who = (r) => (r.key === 'Rcp_manager' || r.key === 'Rcp_upper_manager') && r.name
        ? esc(L()[r.key] || '') + ' <span class="vn-mu">(' + filled({ key: 'ManagerOf', args: [r.name] }) + ')</span>'
        : esc(L()[r.key] || '');
    const head = (k, v) => '<span class="vn-mu">' + esc(k) + '</span><span class="vn-hv">' + v + '</span>';
    /** The template's {n} slots are DATA: each goes in escaped and isolated (bidi), the words around it escaped. */
    const filled = (t) => {
        const text = t.text != null ? String(t.text) : String(L()[t.key] || '');
        const parts = text.split(/(\{\d+\})/);
        return parts.map((p) => { const m = /^\{(\d+)\}$/.exec(p); return m ? F.bidi((t.args || [])[Number(m[1])] == null ? '' : t.args[Number(m[1])]) : esc(p); }).join('');
    };

    const previewHtml = (d) => {
        const row = d.row || {};
        const key = C.eventKey(row.eventCode);
        const t = C.template(key, d.variables || {}, dates);
        const r = C.recipientsOf(d);
        const s = C.statusOf(row.status, row.notes);
        const at = row.sentAt || row.createdAt;
        // W5-FIX2 (F-6) — the preview band words "sent" like the badge: handed over to the e-mail service.
        const explain = L()['Explain_' + String(s.key).replace(/^St_/, '')] || '';
        const rows = t.rows.map((x) => '<tr><td class="vn-k">' + esc(L()[x.key] || '') + '</td><td class="vn-v">' + F.bidi(x.value) + '</td></tr>').join('');
        const sections = t.sections.map((x) => '<div class="vn-sec"><div class="vn-sec-title">' + esc(L()[x.key] || '') + '</div><ul class="vn-items">'
            + (x.itemKeys || []).map((k) => '<li>' + esc(L()[k] || '') + '</li>').join('')
            + (x.items || []).map((v) => '<li>' + F.bidi(v) + '</li>').join('') + '</ul></div>').join('');
        const cta = t.cta.url ? '<a class="btn btn-primary btn-sm align-self-start" href="' + esc(t.cta.url) + '" target="_blank" rel="noopener">' + esc(L()[t.cta.key] || '') + '</a>' : '';
        const rep = (d.variables && d.variables.repName) || row.resourceName || '';
        return '<div class="vn-status d-flex flex-column gap-1 px-4 py-3 border-bottom">' + statusHtml(row.status, row.notes)
            + (explain ? '<small class="text-muted">' + esc(explain) + '</small>' : '')
            + (row.attempts > 1 ? '<small class="text-muted">' + esc(T('Attempts', F.number(row.attempts, 0))) + '</small>' : '') + '</div>'
            + '<div class="vn-mail-head px-4 py-3 border-bottom"><div class="vn-subject">' + filled(Object.assign({}, t.subject, { text: C.withoutEmptySegments(L()[t.subject.key], t.subject.args) })) + '</div><div class="vn-head-grid">'
            + head(L().MailFrom, esc(L().MailFromValue))
            + head(L().MailTo, r.none ? esc(L().RcpNone) : r.to.map(who).join(', ') || '—')
            + (r.cc.length ? head(L().MailCc, r.cc.map(who).join(', ')) : '')
            + head(L().MailDate, esc(momentText(at)))
            + '</div>' + (r.pending.length ? '<small class="text-muted d-block mt-1">' + esc(T('RcpPending', r.pending.map((x) => L()[x.key] || '').join(', '))) + '</small>' : '') + '</div>'
            + '<div class="vn-canvas p-3"><div class="vn-mail">'
            + '<div class="vn-mail-brand"><span class="vn-logo"><i class="bx bx-envelope"></i></span><span class="vn-kind">' + esc(L()[t.kindKey] || '') + '</span></div>'
            + '<div class="vn-band vn-band-' + t.band.tone + '"><i class="bx ' + t.band.icon + '"></i><span>' + esc(L()[t.band.key] || '') + '</span></div>'
            + '<div class="vn-mail-body"><div class="vn-title-block"><div class="vn-title">' + filled(t.title) + '</div><div class="vn-intro">' + filled(t.intro) + '</div></div>'
            + (rows ? '<table class="vn-kv">' + rows + '</table>' : '') + sections
            + (t.noteKey ? '<div class="vn-note"><i class="bx bx-info-circle"></i> ' + esc(L()[t.noteKey] || '') + '</div>' : '') + cta + '</div>'
            + '<div class="vn-foot">' + filled({ key: 'FooterKvkk', args: [rep] }) + '</div>'
            + '</div><small class="text-muted d-block mt-2">' + esc(L().PreviewNote) + '</small></div>';
    };

    const renderQuickView = (row, d) => {
        const sub = byId('oc-subtitle');
        if (sub) { sub.innerHTML = esc(L()['Event_' + C.eventKey(row.eventCode)] || row.eventCode || '') + ' · ' + F.bidi(row.resourceName || row.resourceId || ''); }
        const body = byId('oc-details-list');
        if (body) { body.innerHTML = previewHtml(d || { row: row, recipients: row.recipients, resolved: row.resolved, variables: { repName: row.resourceName || '', doctorName: (row.target || {}).doctorName || '', institutionName: (row.target || {}).institutionName || '', visitDate: row.visitDate || '' } }); }
        const open = byId('oc-btn-edit');
        const url = d && d.variables ? C.safeUrl(d.variables.reportUrl) : '';
        if (open) {
            open.textContent = L()[C.eventKey(row.eventCode) === 'overdue' ? 'CtaReviewRecord' : 'CtaOpenReport'] || '';
            open.setAttribute('href', url || '#');
            open.classList.toggle('d-none', !url);
        }
    };

    /** The preview draws the row at once, then the e-mail's detail (its variables) when it arrives. */
    const populateQuickView = (row) => {
        if (!row) { return; }
        const token = ++quickToken;
        renderQuickView(row, null);
        call('/list/' + encodeURIComponent(row.id)).then((res) => {
            const d = data(res);
            if (d && token === quickToken) { renderQuickView(d.row || row, d); }
        });
    };

    const initDataTable = async () => {
        if (!dtTableEl || !C || !F || !window.DitenDataTable) return;

        onChange('filterPeriod', syncRange);
        byId('btnFilterApply')?.addEventListener('click', guardApply, true);
        enhanceRange();
        onChange('vn-from', syncRangeField);
        onChange('vn-to', syncRangeField);
        document.addEventListener('diten:tab-change', (event) => {
            if (event.detail && event.detail.key === 'list') {
                try { list?.dt?.columns?.adjust?.(); list?.dt?.responsive?.recalc?.(); } catch (e) { /* not drawn yet */ }
            }
            if (event.detail && event.detail.key === 'settings') { window.VisitNotificationsSettings?.load?.(); }
        });

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: endpoint + '/list', type: 'GET' },
            // BL-452: CSV/Excel = every matching e-mail with the visible columns and the filters, not the page on screen
            export: { mode: 'server', url: endpoint + '/list/export', fileName: 'visit-notifications' },
            onResponse: onListResponse,
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: { addNewText: '', exportColumns: EXPORT_COLUMNS, colvisColumns: DATA_COLUMNS },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'CRM', pageKey: 'VisitNotifications', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [] },
            config: {
                order: [],
                // the CRM has no text search over the e-mails: the toolbar search is not drawn
                searching: false,
                drawCallback: drawEmptyState,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'createdAt', name: 'createdAt' },
                    { data: 'eventCode', name: 'eventCode' },
                    { data: 'target', name: 'target' },
                    { data: 'resourceName', name: 'resourceName' },
                    { data: 'recipients', name: 'recipients' },
                    { data: 'status', name: 'status' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => (type === 'display' ? dateCell(full) : (full.sentAt || full.createdAt || '')) },
                    { targets: 2, responsivePriority: 2, render: (d, type, full) => (type === 'display' ? eventHtml(full.eventCode) : (L()['Event_' + C.eventKey(full.eventCode)] || full.eventCode || '')) },
                    { targets: 3, responsivePriority: 4, render: (d, type, full) => (type === 'display' ? targetHtml(full) : ((full.target || {}).doctorName || (full.target || {}).institutionName || '')) },
                    { targets: 4, responsivePriority: 5, render: (d, type, full) => (type === 'display' ? '<span class="text-nowrap">' + F.bidi(full.resourceName || full.resourceId || '') + '</span>' : (full.resourceName || full.resourceId || '')) },
                    { targets: 5, responsivePriority: 6, render: (d, type, full) => (type === 'display' ? recipientsCell(full) : (C.recipientsOf(full).none ? L().RcpNone : C.roleKeys(C.recipientsOf(full)).map((k) => L()[k] || k).join(', '))) },
                    { targets: 6, responsivePriority: 3, render: (d, type, full) => (type === 'display' ? statusHtml(full.status, full.notes) : (L()[C.statusOf(full.status, full.notes).key] || full.status || '')) },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        className: 'cell-fit all',
                        render: (d, type, full) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L().QuickView, 'aria-label': L().QuickView } }
                        ])
                    }
                ]
            }
        });
        if (canManage && window.location && /(^|[?&])tab=settings(&|$)/.test(window.location.search || '')) {
            const tab = byId('vn-tab-settings');
            if (tab) { window.DitenTabs?.activate(tab); }
        }
    };

    return {
        init: function () {
            initDataTable();
        },
        // for the tests: the preview of one detail as the drawer draws it
        previewHtml: previewHtml
    };
})();

document.addEventListener('DOMContentLoaded', () => VisitNotificationsList.init());
