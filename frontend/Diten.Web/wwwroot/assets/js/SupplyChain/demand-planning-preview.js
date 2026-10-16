/* MOD-0188 development preview. No network request or persisted effect is possible here. */
(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) module.exports = api;
    if (root && root.document) {
        root.DemandPlanningPreview = api;
        const host = root.document.getElementById('demandPlanningPreview');
        const app = root.document.getElementById('demandPlanningFixtureApp');
        const translations = root.document.getElementById('demandPlanningTranslations');
        if (host && app && translations) {
            api.mount(app, JSON.parse(translations.textContent), {
                canEdit: host.dataset.canEdit === 'true',
                canReview: host.dataset.canReview === 'true',
                canConsume: host.dataset.canConsume === 'true',
                canAudit: host.dataset.canAudit === 'true'
            });
        }
    }
})(typeof window !== 'undefined' ? window : null, function () {
    'use strict';

    const previewActor = 'fixture-reviewer';
    const isoWeek = (number) => {
        const start = new Date(Date.UTC(2026, 9, 5 + (number - 1) * 7));
        const end = new Date(start);
        end.setUTCDate(start.getUTCDate() + 6);
        return { number, start: start.toISOString().slice(0, 10), end: end.toISOString().slice(0, 10) };
    };
    const weeks = (withGaps) => Array.from({ length: 52 }, (_, index) => {
        const week = isoWeek(index + 1);
        return { ...week, kind: withGaps && index === 4 ? 'Missing' :
            withGaps && index === 5 ? 'Unknown' : 'Known',
        quantity: withGaps && (index === 4 || index === 5) ? null : index === 2 ? 0 : 12 + index,
        origin: 'Manual' };
    });
    function fixture() {
        return {
            companies: [
                { id: 'fixture-le-a', name: 'Fixture LegalEntity A' },
                { id: 'fixture-le-b', name: 'Fixture LegalEntity B' }
            ],
            cycles: [
                { id: 'fixture-cycle-a', companyId: 'fixture-le-a', label: '2026-10-05 · ISO · 52' },
                { id: 'fixture-cycle-b', companyId: 'fixture-le-b', label: '2026-10-12 · ISO · 52' }
            ],
            revisions: [
                { id: 'fixture-draft', cycleId: 'fixture-cycle-a', state: 'Draft',
                    preparedBy: previewActor, changedBySession: false,
                    series: [
                        { id: 'sku-a:wh-a', label: 'SKU A · Warehouse A', unit: 'EA', weeks: weeks(true) },
                        { id: 'sku-b:wh-a', label: 'SKU B · Warehouse A', unit: 'EA', weeks: weeks(false) }
                    ], changes: [] },
                { id: 'fixture-review', cycleId: 'fixture-cycle-a', state: 'InReview',
                    preparedBy: 'fixture-planner', changedBySession: false,
                    series: [{ id: 'sku-c:wh-a', label: 'SKU C · Warehouse A', unit: 'EA', weeks: weeks(false) }],
                    changes: [] },
                { id: 'fixture-published', cycleId: 'fixture-cycle-a', state: 'Published',
                    preparedBy: 'fixture-planner', changedBySession: false,
                    series: [{ id: 'sku-d:wh-a', label: 'SKU D · Warehouse A', unit: 'EA', weeks: weeks(false) }],
                    changes: [] },
                { id: 'fixture-superseded', cycleId: 'fixture-cycle-a', state: 'Superseded',
                    preparedBy: 'fixture-planner', changedBySession: false,
                    series: [{ id: 'sku-old:wh-a', label: 'SKU Old · Warehouse A', unit: 'EA', weeks: weeks(false) }],
                    changes: [] },
                { id: 'fixture-invalidated', cycleId: 'fixture-cycle-a', state: 'Invalidated',
                    preparedBy: 'fixture-planner', changedBySession: false,
                    series: [{ id: 'sku-e:wh-a', label: 'SKU E · Warehouse A', unit: 'EA', weeks: weeks(false) }],
                    changes: [] },
                { id: 'fixture-other-company', cycleId: 'fixture-cycle-b', state: 'Draft',
                    preparedBy: 'fixture-planner', changedBySession: false,
                    series: [{ id: 'sku-f:wh-b', label: 'SKU F · Warehouse B', unit: 'EA', weeks: weeks(false) }],
                    changes: [] }
            ]
        };
    }
    const node = (tag, cls, value) => {
        const item = document.createElement(tag);
        if (cls) item.className = cls;
        if (value !== undefined) item.textContent = value;
        return item;
    };
    const add = (parent, ...children) => { children.forEach(child => parent.appendChild(child)); return parent; };
    const button = (label, action, disabled) => {
        const result = node('button', 'btn btn-outline-primary btn-sm', label);
        result.type = 'button';
        result.disabled = !!disabled;
        result.addEventListener('click', action);
        return result;
    };
    const select = (label, options, value, changed, testId) => {
        const group = node('label', 'dp-field');
        group.appendChild(node('span', 'form-label', label));
        const input = node('select', 'form-select');
        input.dataset.testid = testId;
        options.forEach(option => {
            const item = node('option', '', option.label);
            item.value = option.id;
            item.selected = option.id === value;
            input.appendChild(item);
        });
        input.addEventListener('change', () => changed(input.value));
        group.appendChild(input);
        return group;
    };
    function mount(host, text, permissions, data = fixture()) {
        const state = { companyId: '', cycleId: '', revisionId: '', seriesId: '', offset: 0,
            allWeeks: false, editing: null, message: '' };
        const t = key => text[key] || key;
        const allowedCycles = () => data.cycles.filter(item => item.companyId === state.companyId);
        const allowedRevisions = () => data.revisions.filter(item =>
            item.cycleId === state.cycleId &&
            (item.state === 'Invalidated' ? (permissions.canConsume || permissions.canAudit) :
                (item.state === 'Published' || item.state === 'Superseded') ?
                    permissions.canConsume : true));
        const revision = () => allowedRevisions().find(item => item.id === state.revisionId);
        const series = () => revision()?.series.find(item => item.id === state.seriesId);
        const setCompany = id => {
            state.companyId = data.companies.some(item => item.id === id) ? id : '';
            state.cycleId = allowedCycles()[0]?.id || '';
            state.revisionId = allowedRevisions()[0]?.id || '';
            state.seriesId = revision()?.series[0]?.id || '';
            state.offset = 0; state.allWeeks = false; state.editing = null; state.message = '';
            render();
        };
        const setCycle = id => {
            state.cycleId = allowedCycles().some(item => item.id === id) ? id : '';
            state.revisionId = allowedRevisions()[0]?.id || '';
            state.seriesId = revision()?.series[0]?.id || '';
            state.offset = 0; state.allWeeks = false; state.editing = null;
            render();
        };
        const setRevision = id => {
            state.revisionId = allowedRevisions().some(item => item.id === id) ? id : '';
            state.seriesId = revision()?.series[0]?.id || '';
            state.offset = 0; state.allWeeks = false; state.editing = null;
            render();
        };
        const saveWeek = (weekNumber, kind, rawQuantity, reason) => {
            const current = revision();
            const selected = series();
            if (!permissions.canEdit || !current || current.state !== 'Draft') return false;
            if (!reason || !reason.trim()) { state.message = t('ReasonRequired'); render(); return false; }
            const quantity = rawQuantity === '' ? null : Number(rawQuantity);
            if (kind === 'Known' && (quantity === null || !Number.isFinite(quantity) || quantity < 0)) {
                state.message = t('InvalidQuantity'); render(); return false;
            }
            const target = selected?.weeks.find(item => item.number === weekNumber);
            if (!target) return false;
            const old = target.kind === 'Known' ? String(target.quantity) : target.kind;
            target.kind = kind;
            target.quantity = kind === 'Known' ? quantity : null;
            current.changedBySession = true;
            current.changes.push({ weekNumber, old, next: kind === 'Known' ? String(quantity) : kind,
                reason: reason.trim(), actor: previewActor });
            state.message = t('SavedLocally');
            state.editing = null;
            render();
            return true;
        };
        const transition = (action, reason) => {
            const current = revision();
            if (!current) return false;
            if (!reason || !reason.trim()) { state.message = t('ReasonRequired'); render(); return false; }
            if (action === 'submit') {
                if (!permissions.canEdit || current.state !== 'Draft') return false;
                if (current.series.some(item => item.weeks.length !== 52 ||
                    item.weeks.some(week => week.kind !== 'Known' || week.quantity === null))) {
                    state.message = t('IncompleteWeeks'); render(); return false;
                }
                current.state = 'InReview';
            } else {
                if (!permissions.canReview || current.state !== 'InReview' ||
                    current.preparedBy === previewActor || current.changedBySession) return false;
                current.state = action === 'approve' ? 'Approved' : 'Draft';
            }
            current.changes.push({ action, reason: reason.trim(), actor: previewActor });
            state.message = t('SavedLocally');
            render();
            return true;
        };
        function render() {
            host.replaceChildren();
            const heading = node('div', 'dp-heading');
            add(heading, node('h1', 'h4 mb-1', t('Title')),
                node('p', 'text-muted mb-0', t('PreviewNotice')));
            host.appendChild(heading);
            if (!data.companies.length) { host.appendChild(node('p', 'alert alert-warning', t('NoCompanyData'))); return; }
            const filters = node('div', 'dp-filters card card-body');
            filters.appendChild(select(t('Company'),
                [{ id: '', label: '—' }, ...data.companies.map(item => ({ id: item.id, label: item.name }))],
                state.companyId, setCompany, 'company'));
            if (state.companyId) {
                filters.appendChild(select(t('Cycle'), allowedCycles().map(item => ({
                    id: item.id, label: item.label })), state.cycleId, setCycle, 'cycle'));
                filters.appendChild(select(t('Revision'), allowedRevisions().map(item => ({
                    id: item.id, label: item.id + ' · ' + t(item.state === 'Approved' ? 'ApprovedState' : item.state)
                })), state.revisionId, setRevision, 'revision'));
            }
            host.appendChild(filters);
            if (!state.companyId || !revision()) {
                host.appendChild(node('p', 'alert alert-info mt-3', t('NoScope')));
                return;
            }
            const current = revision();
            const card = node('section', 'card mt-3');
            const body = node('div', 'card-body');
            add(body, node('h2', 'h5', t('RevisionHistory')),
                node('p', 'dp-state', t('State') + ': ' +
                    t(current.state === 'Approved' ? 'ApprovedState' : current.state)));
            if (current.state === 'Invalidated') {
                body.appendChild(node('p', 'alert alert-danger', t('InvalidatedWarning')));
                if (!permissions.canAudit) {
                    body.appendChild(node('p', 'text-muted', t('NoAuditPermission')));
                    card.appendChild(body); host.appendChild(card); return;
                }
            }
            if (current.state === 'Superseded') {
                body.appendChild(node('p', 'alert alert-info', t('SupersededReadOnly')));
                card.appendChild(body); host.appendChild(card); return;
            }
            body.appendChild(select(t('Series'), current.series.map(item => ({
                id: item.id, label: item.label })), state.seriesId, id => {
                    state.seriesId = id; state.offset = 0; state.editing = null; render();
                }, 'series'));
            const selected = series();
            if (!selected) { card.appendChild(body); host.appendChild(card); return; }
            const toolbar = node('div', 'dp-toolbar');
            add(toolbar,
                button(t('PreviousWeeks'), () => { state.offset = Math.max(0, state.offset - 13); render(); },
                    state.allWeeks || state.offset === 0),
                node('span', 'dp-window', t('WeekWindow') + ': ' +
                    (state.allWeeks ? '1–52' : (state.offset + 1) + '–' + Math.min(52, state.offset + 13))),
                button(t('NextWeeks'), () => { state.offset = Math.min(39, state.offset + 13); render(); },
                    state.allWeeks || state.offset >= 39),
                button(state.allWeeks ? t('FirstThirteen') : t('ShowAllWeeks'), () => {
                    state.allWeeks = !state.allWeeks; state.offset = 0; render();
                }));
            body.appendChild(toolbar);
            const scroll = node('div', 'dp-table-scroll');
            const table = node('table', 'table table-sm dp-weeks');
            const head = node('thead');
            const header = node('tr');
            [t('Week'), t('Quantity'), t('Origin'), ''].forEach(value =>
                header.appendChild(node('th', '', value)));
            head.appendChild(header); table.appendChild(head);
            const tbody = node('tbody');
            const visible = state.allWeeks ? selected.weeks : selected.weeks.slice(state.offset, state.offset + 13);
            visible.forEach(week => {
                const row = node('tr');
                const value = week.kind === 'Known' ?
                    (week.quantity === 0 ? '0 · ' + t('KnownZero') : String(week.quantity)) :
                    t(week.kind);
                add(row,
                    node('th', '', String(week.number) + ' · ' + week.start + ' – ' + week.end),
                    node('td', '', value + (week.kind === 'Known' ? ' ' + selected.unit : '')),
                    node('td', '', t(week.origin)));
                const action = node('td');
                if (permissions.canEdit && current.state === 'Draft') {
                    action.appendChild(button(t('EditWeek'), () => {
                        state.editing = week.number; state.message = ''; render();
                    }));
                }
                row.appendChild(action); tbody.appendChild(row);
            });
            table.appendChild(tbody); scroll.appendChild(table); body.appendChild(scroll);
            if (state.editing !== null && current.state === 'Draft' && permissions.canEdit) {
                const week = selected.weeks.find(item => item.number === state.editing);
                const form = node('form', 'dp-edit border rounded p-3');
                form.addEventListener('submit', event => {
                    event.preventDefault();
                    saveWeek(week.number, form.elements.namedItem('kind').value,
                        form.elements.namedItem('quantity').value,
                        form.elements.namedItem('reason').value);
                });
                form.appendChild(node('h3', 'h6', t('EditWeek') + ' ' + week.number));
                const kind = node('select', 'form-select');
                kind.name = 'kind';
                ['Known', 'Missing', 'Unknown'].forEach(value => {
                    const option = node('option', '', value === 'Known' ? t('Quantity') : t(value));
                    option.value = value; option.selected = week.kind === value; kind.appendChild(option);
                });
                const quantity = node('input', 'form-control');
                quantity.name = 'quantity'; quantity.type = 'number'; quantity.min = '0';
                quantity.step = 'any'; quantity.value = week.quantity === null ? '' : String(week.quantity);
                const reason = node('input', 'form-control');
                reason.name = 'reason'; reason.required = true; reason.maxLength = 500;
                reason.placeholder = t('Reason');
                const save = node('button', 'btn btn-primary', t('SavePreview'));
                save.type = 'submit';
                add(form, kind, quantity, reason, save); body.appendChild(form);
            }
            if ((current.state === 'Draft' && permissions.canEdit) ||
                (current.state === 'InReview' && permissions.canReview)) {
                const actions = node('div', 'dp-actions');
                const reason = node('input', 'form-control');
                reason.type = 'text'; reason.placeholder = t('ReviewReason');
                reason.setAttribute('aria-label', t('ReviewReason'));
                actions.appendChild(reason);
                if (current.state === 'Draft') actions.appendChild(button(t('SubmitReview'),
                    () => transition('submit', reason.value)));
                else {
                    const blocked = current.preparedBy === previewActor || current.changedBySession;
                    actions.appendChild(button(t('Approve'), () => transition('approve', reason.value), blocked));
                    actions.appendChild(button(t('Reject'), () => transition('reject', reason.value), blocked));
                }
                body.appendChild(actions);
            }
            const history = node('div', 'dp-history');
            history.appendChild(node('h3', 'h6', t('Changes')));
            if (!current.changes.length) history.appendChild(node('p', 'text-muted', t('NoChanges')));
            else current.changes.forEach(item => history.appendChild(node('p', 'mb-1',
                (item.action || (t('Week') + ' ' + item.weekNumber + ': ' + item.old + ' → ' + item.next)) +
                ' · ' + item.reason)));
            body.appendChild(history);
            if (state.message) body.appendChild(node('p', 'alert alert-info mt-3', state.message));
            card.appendChild(body); host.appendChild(card);
        }
        render();
        return { state, data, setCompany, setCycle, setRevision, saveWeek, transition, render };
    }
    return { fixture, mount };
});
