'use strict';

/*
 * MOD-0357 S3 — DataTables Index script (Golden Reference Compact contract markers: id="skeleton-loader",
 * data-dt-standard="v2"). Deliberately simpler than DevEnablement/GoldenReferenceCompact/index.js: no saved-view
 * personalization or column-reorder persistence — this WP's own field list does not ask for either, and porting
 * them would be scope this slice does not need.
 */
const MeetingsList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-meetings');
    let L = window.L10n || {};
    let organizerNamesById = {};
    let appliedFilters = { segment: '', meetingType: [], organizer: [], iAmAttendee: false, hasLinkedTasks: false, from: '', to: '' };

    const syncL10n = () => {
        // Payload keys are camelCase (MVC's Json.Serialize camelCases the C# property names) — MeetingsL10n.t()
        // takes the camelCase form; this was measured live and fixed here (every PascalCase call below was
        // silently falling back to the raw key, per MeetingsL10n's own missing-key console warning).
        const t = (key) => window.MeetingsL10n?.t?.(key) ?? key;
        L = {
            AddNew: t('addNew'), Actions: t('actions'), Edit: t('edit'), Details: t('details'),
            Yes: t('yes'), No: t('no'), Filter: t('filter'), Apply: t('apply'), Reset: t('reset'),
            ShowAll: t('showAll'), MeetingType: t('meetingType'), Organizer: t('organizer'),
            StatusScheduled: t('statusScheduled'), StatusCancelled: t('statusCancelled'), StatusCompleted: t('statusCompleted'),
            ErrorOccurred: t('errorOccurred'),
            // BL-390 — an organizer id this tenant's eligible-people list does not resolve (deleted/test
            // identity) must never render as the raw GUID; the same label everywhere it could otherwise leak.
            UnknownUser: t('unknownUser')
        };
    };

    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const formatDateTime = (v) => {
        if (!v) { return '-'; }
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString(window.CurrentLanguage || undefined);
    };

    // Platform serializes MeetingLifecycle as its integer ordinal (Scheduled=0, Cancelled=1, Completed=2), not
    // its C# name — measured live; see the matching fix/comment in form.js's statusLabelFor.
    const statusLabel = (lifecycle) => ([
        { text: L.StatusScheduled, cls: 'bg-label-info' },
        { text: L.StatusCancelled, cls: 'bg-label-secondary' },
        { text: L.StatusCompleted, cls: 'bg-label-success' }
    ][lifecycle] || { text: lifecycle, cls: 'bg-label-primary' });

    const boolBadge = (value) => value
        ? `<span class="badge bg-label-success">${L.Yes}</span>`
        : `<span class="badge bg-label-secondary">${L.No}</span>`;

    // ── Filters (client-side — the list loads ALL rows in one call, same as the golden reference) ──────────

    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.meetingsFilterBound === '1') { return; }
        dtTableEl.dataset.meetingsFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) { return true; }
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) { return true; }

            const now = new Date();
            if (appliedFilters.segment === 'upcoming' && new Date(row.startAt) < now) { return false; }
            if (appliedFilters.segment === 'past' && new Date(row.startAt) >= now) { return false; }
            if (appliedFilters.meetingType.length && !appliedFilters.meetingType.includes(row.meetingTypeId)) { return false; }
            if (appliedFilters.organizer.length && !appliedFilters.organizer.includes(row.organizerUserId)) { return false; }
            if (appliedFilters.iAmAttendee && !row.iAmAttendee) { return false; }
            if (appliedFilters.hasLinkedTasks && !row.hasLinkedTasks) { return false; }
            if (appliedFilters.from && new Date(row.startAt) < new Date(appliedFilters.from)) { return false; }
            if (appliedFilters.to && new Date(row.startAt) > new Date(appliedFilters.to)) { return false; }
            return true;
        });
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) { return; }
        $('#filterSegment, #filterMeetingType, #filterOrganizer').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) { $s.select2('destroy'); }
            $s.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                placeholder: $s.data('placeholder') || '',
                width: 'element',
                minimumResultsForSearch: Infinity,
                closeOnSelect: $s.attr('multiple') === undefined
            });
        });
    };

    const populateFilterOptions = (rows, types) => {
        const $type = $('#filterMeetingType');
        $type.empty();
        types.forEach((t) => $type.append(new Option(t.name, t.id)));

        const organizerIds = Array.from(new Set(rows.map((r) => r.organizerUserId)));
        const $organizer = $('#filterOrganizer');
        $organizer.empty();
        organizerIds.forEach((id) => $organizer.append(new Option(organizerNamesById[id] || L.UnknownUser, id)));
    };

    const bindFilterButtons = (api) => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                segment: document.getElementById('filterSegment')?.value || '',
                meetingType: $('#filterMeetingType').val() || [],
                organizer: $('#filterOrganizer').val() || [],
                iAmAttendee: document.getElementById('filterIAmAttendee')?.checked || false,
                hasLinkedTasks: document.getElementById('filterHasLinkedTasks')?.checked || false,
                from: document.getElementById('filterFromDate')?.value || '',
                to: document.getElementById('filterToDate')?.value || ''
            };
            api.draw();
            const collapseEl = document.getElementById('inlineFilterCollapse');
            if (collapseEl) { bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide(); }
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            appliedFilters = { segment: '', meetingType: [], organizer: [], iAmAttendee: false, hasLinkedTasks: false, from: '', to: '' };
            $('#filterSegment, #filterMeetingType, #filterOrganizer').val(appliedFilters.segment).trigger('change');
            document.getElementById('filterIAmAttendee').checked = false;
            document.getElementById('filterHasLinkedTasks').checked = false;
            document.getElementById('filterFromDate').value = '';
            document.getElementById('filterToDate').value = '';
            api.draw();
        });
    };

    // Golden reference contract: the inline filter host is moved next to the toolbar and re-padded (px-6 → px-3)
    // so it lines up with the table below it instead of the card's own outer padding.
    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) { return; }
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    // Quick View delegation is handled by DitenDataTable, equivalent to closest('.js-quick-view').
    const rowActionHandlers = {
        details: ({ id }) => { if (id) { window.location.href = `/Meetings/${id}`; } },
        edit: ({ id }) => { if (id) { window.location.href = `/Meetings/${id}/Edit`; } }
    };

    const loadLookupsThenInit = async () => {
        const [typesResult, attendeesResult] = await Promise.all([
            window.MeetingsApi.lookupTypes(),
            window.MeetingsApi.lookupAttendees()
        ]);
        const types = typesResult.ok ? (typesResult.data || []) : [];
        organizerNamesById = {};
        if (attendeesResult.ok) {
            (attendeesResult.data?.people || []).forEach((p) => { organizerNamesById[p.userId] = p.displayName || L.UnknownUser; });
        }
        return types;
    };

    const initDataTable = async () => {
        if (!dtTableEl) { return; }
        syncL10n();
        const types = await loadLookupsThenInit();

        // DitenDataTable wraps the DataTables v2 constructor and shared defaults:
        // new DataTable(...)
        // window.DtDefaults.create(...)
        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: '/Meetings/api/list?pageSize=1000',
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'title', name: 'title' },
                    { data: 'meetingTypeId', name: 'meetingType' },
                    { data: 'startAt', name: 'startAt' },
                    { data: 'endAt', name: 'endAt' },
                    { data: 'organizerUserId', name: 'organizer' },
                    { data: 'iAmAttendee', name: 'iAmAttendee' },
                    { data: 'lifecycle', name: 'lifecycle' },
                    { data: 'hasLinkedTasks', name: 'hasLinkedTasks' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        targets: 2,
                        render: (data) => {
                            const type = types.find((t) => t.id === data);
                            return type ? type.name : '-';
                        }
                    },
                    { targets: 3, render: (data) => formatDateTime(data) },
                    { targets: 4, render: (data) => formatDateTime(data) },
                    { targets: 5, render: (data) => organizerNamesById[data] || L.UnknownUser },
                    { targets: 6, render: (data) => boolBadge(data) },
                    {
                        targets: 7,
                        render: (data, type) => {
                            const s = statusLabel(data);
                            return type === 'display' ? `<span class="badge ${s.cls}">${s.text}</span>` : s.text;
                        }
                    },
                    { targets: 8, render: (data) => boolBadge(data) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => window.DitenDataTable.renderActions([
                            { key: 'details', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L.Details } },
                            { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id } }
                        ])
                    }
                ],
                buttons: window.DtDefaults.exportButtons(L.AddNew, { href: '/Meetings/Create' }, {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                        attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
                        action: () => toggleInlineFilter()
                    }
                }),
                initComplete: function () {
                    const api = this.api();
                    populateFilterOptions(dt.rows().data().toArray(), types);
                    initSelect2Filters();
                    registerTableFilters();
                    mountInlineFilter();
                    bindFilterButtons(api);
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/Meetings/Create';
                    });
                }
            }
        });
    };

    return {
        init: function () {
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => MeetingsList.init());
