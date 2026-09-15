'use strict';

/*
 * MOD-0357 S12 (pack §23) — the meeting report screen. Same-origin proxy only
 * (`/Meetings/api/report`, `/Meetings/api/report/export`), the SAME network seam every other Meetings screen
 * already uses — never a service port from the browser.
 *
 * UAS-001 — every content region below (`#mrTiles`, `#mrMeetingsCard`, `#mrDecisionsCard`, `#mrActionsCard`)
 * starts `hidden` in the markup and is revealed ONLY after a successful report response. A 403 shows exactly
 * one sentence (`#mrNoAccess`) and nothing else — no tile, no table header, no skeleton.
 *
 * ⚠ EXPOSED AS `window.MeetingReportScreen`, NOT LEFT INSIDE THE `DOMContentLoaded` LISTENER — jsdom's
 * `document.readyState` is already `'complete'` by the time a test loads this script, so that listener never
 * fires there (the same measured fact `work-report-layout.test.js` records for its own module). Every function
 * a test needs to drive is a property on the returned object and is called directly, never through the dead
 * event.
 */
(function (global) {
    const t = (key) => global.MeetingReportL10n?.t?.(key) ?? key;

    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    const LIFECYCLE_KEYS = {
        Open: 'LifecycleOpen', Planned: 'LifecyclePlanned', InProgress: 'LifecycleInProgress',
        Waiting: 'LifecycleWaiting', PendingReview: 'LifecyclePendingReview',
        Done: 'LifecycleDone', Cancelled: 'LifecycleCancelled'
    };
    const lifecycleLabel = (value) => t(LIFECYCLE_KEYS[value] || value);

    let actionsTable = null;
    let lastReport = null;
    let lastQuery = null;

    const getAuthHeaders = () => global.DitenDataTable?.getAuthHeaders?.() || {};

    const buildQuery = () => {
        const from = byId('mrFrom').value;
        const to = byId('mrTo').value;
        const meetingTypeId = byId('mrMeetingType').value;
        const organizerUserId = byId('mrOrganizer').value;

        const params = new URLSearchParams();
        // flatpickr gives a plain calendar day; the API's `to` is EXCLUSIVE, so the picked end day must still
        // be included — the same "add one day" rule the work report's own filter relies on its API doing
        // server-side is done here explicitly since this screen sends bare dates, not instants.
        params.set('from', new Date(`${from}T00:00:00Z`).toISOString());
        const toExclusive = new Date(`${to}T00:00:00Z`);
        toExclusive.setUTCDate(toExclusive.getUTCDate() + 1);
        params.set('to', toExclusive.toISOString());
        if (meetingTypeId) { params.set('meetingTypeId', meetingTypeId); }
        if (organizerUserId) { params.set('organizerUserId', organizerUserId); }
        return params;
    };

    const setStatus = (message) => { const el = byId('mrStatus'); if (el) { el.textContent = message; } };

    const showNoAccess = () => {
        const el = byId('mrNoAccess');
        el.textContent = t('ErrorNoAccess');
        el.classList.remove('d-none');
        ['mrTiles', 'mrMeetingsCard', 'mrDecisionsCard', 'mrActionsCard'].forEach((id) => { byId(id).hidden = true; });
    };

    const hideNoAccess = () => { byId('mrNoAccess').classList.add('d-none'); };

    const renderTiles = (totals) => {
        byId('mrTileMeetingCount').textContent = totals.meetingCount;
        byId('mrTileAttendanceRate').textContent =
            totals.attendanceRatePercent === null || totals.attendanceRatePercent === undefined
                ? '–' : `${totals.attendanceRatePercent}%`;
        byId('mrTileDecisionCount').textContent = totals.decisionCount;
        byId('mrTileOpenActions').textContent = totals.openActionCount;
        byId('mrTileOverdueActions').textContent = totals.overdueActionCount;
        byId('mrTiles').hidden = false;
    };

    const renderMeetings = (meetings) => {
        const body = byId('mrMeetingsBody');
        body.innerHTML = meetings.map((m) => `<tr>
            <td>${esc(m.title)}</td>
            <td>${esc(m.meetingTypeName)}</td>
            <td>${esc(new Date(m.startAt).toLocaleString(global.CurrentLanguage || undefined))}</td>
            <td>${esc(m.organizerUserId)}</td>
            <td>${m.attendanceRatePercent === null || m.attendanceRatePercent === undefined ? '–' : esc(m.attendanceRatePercent) + '%'}
                <span class="text-muted small">(${esc(m.respondedCount)}/${esc(m.attendeeCount)})</span></td>
        </tr>`).join('');
        byId('mrMeetingsEmpty').hidden = meetings.length > 0;
        byId('mrMeetingsCard').hidden = false;
    };

    const renderDecisions = (decisions) => {
        const body = byId('mrDecisionsBody');
        body.innerHTML = decisions.map((d) => `<tr>
            <td>${esc(d.meetingTitle)}</td>
            <td>${esc(d.code)}</td>
            <td>${esc(d.text)}</td>
        </tr>`).join('');
        byId('mrDecisionsEmpty').hidden = decisions.length > 0;
        byId('mrDecisionsCard').hidden = false;
    };

    const renderActions = (actions) => {
        const skeleton = byId('mrActionsSkeleton');
        if (skeleton) { skeleton.hidden = true; }
        byId('mrActionsEmpty').hidden = actions.length > 0;
        byId('mrActionsCard').hidden = false;

        const jq = global.jQuery;
        if (!jq?.fn?.DataTable) { return; }

        const rows = actions.map((a) => [
            (a.isOverdue ? `<span class="badge bg-label-danger me-1">${esc(t('BadgeOverdue'))}</span>` : '') + esc(a.title),
            esc(lifecycleLabel(a.lifecycle)),
            a.dueAt ? esc(new Date(a.dueAt).toLocaleDateString(global.CurrentLanguage || undefined)) : '–',
            esc(a.originMeetingTitle),
            esc(a.currentMeetingTitle)
        ]);

        if (actionsTable) {
            actionsTable.clear();
            actionsTable.rows.add(rows);
            actionsTable.draw();
            return;
        }

        actionsTable = jq('#mrActionsTable').DataTable({
            data: rows,
            columns: [{ title: t('ColActionTitle') }, { title: t('ColActionLifecycle') },
                { title: t('ColActionDueAt') }, { title: t('ColActionOrigin') }, { title: t('ColActionCurrent') }],
            paging: true,
            searching: true,
            ordering: true
        });
    };

    const renderScope = (scopeApplied) => {
        const el = byId('mrScopeBadge');
        if (!el) { return; }
        el.textContent = scopeApplied === 'tenant' ? t('ScopeTenant') : t('ScopeScoped');
        byId('mrScope').hidden = false;
    };

    const setExportEnabled = (enabled) => {
        const toggle = document.querySelector('[data-mr-export-toggle]');
        if (toggle) { toggle.disabled = !enabled; }
    };

    const loadReport = async () => {
        hideNoAccess();
        setStatus(t('Loading'));
        lastQuery = buildQuery();

        let response;
        try {
            response = await global.fetch(`/Meetings/api/report?${lastQuery.toString()}`, {
                headers: { Accept: 'application/json', ...getAuthHeaders() },
                credentials: 'same-origin'
            });
        } catch (_) {
            setStatus(t('ErrorOccurred'));
            return;
        }

        if (response.status === 403) {
            setStatus('');
            showNoAccess();
            setExportEnabled(false);
            return;
        }

        if (!response.ok) {
            setStatus(t('ErrorOccurred'));
            return;
        }

        const payload = await response.json().catch(() => null);
        const data = payload?.data;
        if (!data) {
            setStatus(t('ErrorOccurred'));
            return;
        }

        lastReport = data;
        setStatus('');
        renderTiles(data.totals);
        renderMeetings(data.meetings);
        renderDecisions(data.decisions);
        renderActions(data.actions);
        renderScope(data.scopeApplied);
        setExportEnabled(true);
    };

    // ── export ────────────────────────────────────────────────────────────────────────────────────────────

    const downloadExport = async (dataset, format) => {
        if (!lastQuery) { return; }
        const params = new URLSearchParams(lastQuery);
        params.set('dataset', dataset);
        params.set('format', format);
        params.set('locale', global.CurrentLanguage || 'en');

        let response;
        try {
            response = await global.fetch(`/Meetings/api/report/export?${params.toString()}`, {
                headers: getAuthHeaders(),
                credentials: 'same-origin'
            });
        } catch (_) {
            setStatus(t('ErrorOccurred'));
            return;
        }

        if (!response.ok) {
            const body = await response.json().catch(() => null);
            const reasonCode = body?.reason_code ?? body?.reasonCode;
            if (response.status === 503 && reasonCode === 'DATA_EXPORT_AUDIT_NOT_RECORDED') {
                setStatus(t('ExportAuditNotRecorded'));
            } else if (reasonCode === 'MEETING_REPORT_EXPORT_TOO_LARGE') {
                setStatus(t('ExportTooLarge'));
            } else {
                setStatus(t('ErrorOccurred'));
            }
            return;
        }

        const disposition = response.headers.get('Content-Disposition') || '';
        const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition);
        const fileName = match ? decodeURIComponent(match[1].replace(/"/g, '')) : `${dataset}.${format}`;
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        try {
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
        } finally {
            URL.revokeObjectURL(url);
        }
    };

    // ── wiring ────────────────────────────────────────────────────────────────────────────────────────────

    const initPickers = () => {
        if (global.flatpickr) {
            ['mrFrom', 'mrTo'].forEach((id) => global.flatpickr(byId(id), { dateFormat: 'Y-m-d' }));
        }

        const jq = global.jQuery;
        if (jq?.fn?.select2) {
            jq('#mrMeetingType, #mrOrganizer').select2({ width: '100%' });
        }
    };

    const loadLookups = async () => {
        if (!global.MeetingsApi) { return; }
        const [typesResult, attendeesResult] = await Promise.all([
            global.MeetingsApi.lookupTypes(), global.MeetingsApi.lookupAttendees()
        ]);
        if (typesResult.ok) {
            (typesResult.data || []).forEach((type) => {
                byId('mrMeetingType').append(new Option(type.name, type.id));
            });
        }
        if (attendeesResult.ok) {
            (attendeesResult.data?.people || []).forEach((p) => {
                byId('mrOrganizer').append(new Option(p.displayName || p.userId, p.userId));
            });
        }
        global.jQuery?.('#mrMeetingType, #mrOrganizer').trigger('change.select2');
    };

    const bindEvents = () => {
        document.getElementById('reportFilterForm')?.addEventListener('submit', (e) => {
            e.preventDefault();
            loadReport();
        });

        document.querySelectorAll('[data-mr-export]').forEach((button) => {
            button.addEventListener('click', () => {
                downloadExport(button.getAttribute('data-mr-export'), button.getAttribute('data-mr-export-format'));
            });
        });
    };

    const boot = async () => {
        if (!byId('mrTiles')) { return; } // this script is loaded only on the report page itself.
        initPickers();
        await loadLookups();
        bindEvents();
        await loadReport();
    };

    const screen = {
        boot, loadReport, downloadExport, renderTiles, renderMeetings, renderDecisions, renderActions,
        renderScope, showNoAccess, hideNoAccess, buildQuery, lifecycleLabel,
        get lastReport() { return lastReport; }
    };

    global.MeetingReportScreen = screen;
    document.addEventListener('DOMContentLoaded', () => screen.boot());
})(typeof window !== 'undefined' ? window : globalThis);
