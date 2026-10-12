/**
 * WP-CYC-UI-1 — the Cycle Period details page.
 *  - Linked records come from /api/periods/{id}/usage (capacity, campaigns, planning sessions, planned visits by
 *    status). Every value reaches the DOM through esc() or textContent.
 *  - The calendar summary comes from /api/periods/{id}/calendar (one row per month, clipped at the period's edges);
 *    an unresolved / forbidden / country-less calendar is SAID, and no working-day figure is invented.
 *  - Lifecycle actions are the ones the server rendered (draft: Activate + Close; active: Close; closed: none), each
 *    behind a confirmation. The runtime's refusal (e.g. an active overlap on activation) is shown verbatim.
 *  - Edit opens the same right-side panel the list uses.
 */
(function (window, document) {
    'use strict';

    const S = window.CyclePeriodsShared;
    const root = document.getElementById('cyclePeriodDetails');
    if (!S || !root) return;

    const L = S.L;
    const esc = S.esc;
    const id = root.dataset.periodId;
    const status = root.dataset.status;
    const $ = x => document.getElementById(x);

    S.formatAll(root);

    const empty = text => `<p class="text-muted mb-0">${esc(text || '')}</p>`;

    const renderCapacity = capacity => {
        const host = $('usageCapacity');
        if (capacity) {
            host.innerHTML = `<div class="d-flex flex-wrap align-items-center justify-content-between gap-2">`
                + `<span>${capacity.isArchived ? S.badge(L().CapacityArchived || '', 'secondary') : S.badge(L().HasCapacity || '', 'success')}</span>`
                + `<a class="btn btn-sm btn-label-primary" href="/CRM/CycleCapacities/Details/${encodeURIComponent(capacity.cycleCapacityId)}">${esc(L().OpenCapacity || '')}</a></div>`;
            return;
        }
        host.innerHTML = `<div class="border border-warning border-dashed rounded p-3">`
            + `<div class="fw-medium">${esc(L().NoCapacityTitle || '')}</div>`
            + `<div class="text-muted small">${esc(L().NoCapacityHint || '')}</div>`
            + (status === 'closed'
                ? `<div class="text-muted small mt-2">${esc(L().NoCapacityClosed || '')}</div>`
                : `<a class="btn btn-sm btn-primary mt-2" href="/CRM/CycleCapacities/Index?cyclePeriodId=${encodeURIComponent(id)}&returnTo=cycleperiods">${esc(L().CreateCapacity || '')}</a>`)
            + '</div>';
    };

    const visitTone = s => ({ draft: 'secondary', planned: 'primary', confirmed: 'success', cancelled: 'danger', archived: 'dark' }[s] || 'info');
    const visitLabel = s => (L()[`VisitStatus_${s}`] || s);
    const renderVisits = visits => {
        const host = $('usageVisits');
        $('usageVisitTotal').textContent = S.number(visits?.total ?? 0);
        const entries = Object.entries(visits?.byStatus || {});
        if (!visits || !visits.total || !entries.length) { host.innerHTML = empty(L().NoPlannedVisits); return; }
        host.innerHTML = `<div class="cp-visit-bar mb-2" role="img" aria-label="${esc(L().PlannedVisits || '')}">`
            + entries.map(([s, n]) => `<span class="bg-${esc(visitTone(s))}" style="inline-size:${(n * 100 / visits.total).toFixed(2)}%"></span>`).join('')
            + '</div><ul class="list-unstyled mb-0 small">'
            + entries.map(([s, n]) => `<li class="d-flex justify-content-between"><span><span class="badge badge-dot bg-${esc(visitTone(s))} me-1"></span>${esc(visitLabel(s))}</span><span class="fw-medium">${esc(S.number(n))}</span></li>`).join('')
            + '</ul>';
    };

    const renderCampaigns = campaigns => {
        const host = $('usageCampaigns');
        $('usageCampaignTotal').textContent = S.number(campaigns.length);
        if (!campaigns.length) { host.innerHTML = empty(L().NoCampaigns); return; }
        host.innerHTML = '<ul class="list-unstyled mb-0 d-flex flex-column gap-2">'
            + campaigns.map(c => `<li class="d-flex justify-content-between gap-2"><span><span class="fw-medium text-heading">${esc(c.code)}</span> · ${esc(c.name)}</span>${S.badge(L()[`CampaignStatus_${c.status}`] || c.status, 'primary')}</li>`).join('')
            + '</ul>';
    };

    const renderSessions = sessions => {
        const host = $('usageSessions');
        if (!sessions.length) { host.innerHTML = empty(L().NoPlanningSessions); return; }
        host.innerHTML = '<ul class="list-unstyled mb-0 d-flex flex-column gap-2">'
            + sessions.map(s => `<li><div class="d-flex justify-content-between gap-2"><span class="fw-medium text-heading">${esc(S.day(s.name))}</span>`
                + `<span class="small">${esc(String(L().VisitCountPattern || '{0}').replace('{0}', S.number(s.committedVisitCount)))}</span></div>`
                + `<small class="text-muted">${esc(s.ownerDisplayName || '—')} · ${esc(L()[`SessionStatus_${s.status}`] || s.status)}</small></li>`).join('')
            + '</ul>';
    };

    const loadUsage = async () => {
        const column = $('usageColumn');
        try {
            const u = await S.getJson(`/periods/${encodeURIComponent(id)}/usage`);
            renderCapacity(u.capacity);
            renderVisits(u.plannedVisits);
            renderCampaigns(u.campaigns || []);
            renderSessions(u.planningSessions || []);
        } catch (e) {
            ['usageCapacity', 'usageVisits', 'usageCampaigns', 'usageSessions'].forEach(x => {
                $(x).innerHTML = `<p class="text-danger mb-0 small">${esc(L().UsageLoadFailed || '')}</p>`;
            });
        } finally {
            column?.setAttribute('aria-busy', 'false');
        }
    };

    const calendarBadge = (resolution, country) => {
        const map = {
            resolved: [L().CalendarResolved, 'success'],
            no_country: [L().CalendarNoCountry, 'secondary'],
            calendar_forbidden: [L().CalendarForbidden, 'warning'],
            calendar_unresolved: [L().CalendarUnresolved, 'warning']
        };
        const [text, tone] = map[resolution] || map.calendar_unresolved;
        const el = $('calendarStatusBadge');
        el.className = `badge bg-label-${tone}`;
        el.textContent = `${country ? country + ' · ' : ''}${text || ''}`;
    };
    const loadCalendar = async () => {
        const body = document.querySelector('#calendarSummaryTable tbody');
        try {
            const c = await S.getJson(`/periods/${encodeURIComponent(id)}/calendar`);
            calendarBadge(c.resolution, c.country);
            const total = (c.months || []).reduce((a, m) => a + (m.workingDays ?? 0), 0);
            const allResolved = (c.months || []).every(m => m.workingDays !== null && m.workingDays !== undefined);
            $('detailsWorkingDays').textContent = allResolved ? S.number(total) : '—';
            body.innerHTML = (c.months || []).map(m =>
                `<tr><td>${esc(S.monthName(m.year, m.month))}${m.partial ? ` <span class="badge bg-label-secondary" title="${esc(L().PartialMonthHint || '')}">${esc(L().PartialMonth || '')}</span>` : ''}</td>`
                + `<td class="text-end">${esc(S.number(m.days))}</td>`
                + `<td class="text-end fw-medium">${esc(S.number(m.workingDays))}</td>`
                + `<td class="text-end">${esc(S.number(m.nonWorkingDays))}</td></tr>`).join('');
        } catch (e) {
            calendarBadge('calendar_unresolved', null);
            body.innerHTML = `<tr><td colspan="4" class="text-danger small">${esc(L().CalendarLoadFailed || '')}</td></tr>`;
        }
    };

    const showActionError = message => {
        const el = $('detailsActionError');
        el.textContent = message;
        el.classList.remove('d-none');
    };
    const lifecycle = (action, message, confirmText, type, successKey) => btn => {
        window.showConfirm?.(message, async () => {
            try {
                await S.sendJson('POST', `/periods/${encodeURIComponent(btn.dataset.id)}/${action}`);
                window.showToast?.(L()[successKey] || '', 'success');
                window.location.reload();
            } catch (e) { showActionError(e.message || L().ErrorOccurred || ''); }
        }, { entityName: btn.dataset.name, type, confirmButtonText: confirmText });
    };
    const activate = lifecycle('activate', L().ActivateCyclePeriodConfirm, L().ActivateCyclePeriod, 'question', 'RecordActivated');
    const close = lifecycle('close', L().CloseCyclePeriodConfirm, L().CloseCyclePeriod, 'warning', 'RecordClosed');

    document.addEventListener('click', event => {
        const a = event.target.closest('.js-activate-period');
        if (a) { event.preventDefault(); activate(a); return; }
        const c = event.target.closest('.js-close-period');
        if (c) { event.preventDefault(); close(c); return; }
        const e = event.target.closest('.js-edit-period');
        if (e) { event.preventDefault(); window.CyclePeriodPanel?.openEdit(e.dataset.id); }
    });
    window.CyclePeriodPanel?.onSaved(() => window.location.reload());

    void loadUsage();
    void loadCalendar();
})(window, document);
