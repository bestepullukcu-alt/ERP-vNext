'use strict';

/*
 * S8 — Meeting Types DataTable Index script, mirroring Meetings/index.js's own shape (Golden Reference
 * Compact contract markers: id="skeleton-loader", data-dt-standard="v2"). No pagination server-side — a
 * tenant's meeting-type catalogue is small by nature (a handful of governed review cadences), so the whole
 * list loads once, same posture Tasks/TaskTypes/index.js takes for its own catalogue.
 */
const MeetingTypesList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-meeting-types');
    let L = {};
    let appliedFilters = { isQualityRecord: '', requiresESignature: '', attendanceMandatory: '' };

    const syncL10n = () => {
        const t = (key) => window.MeetingTypesL10n?.t?.(key) ?? key;
        const st = (key) => window.MeetingsL10n?.t?.(key) ?? key;
        L = {
            AddNew: t('formTitleCreate'), Actions: t('actions'),
            Edit: st('edit'), Yes: st('yes'), No: st('no'), Filter: st('filter'), Apply: st('apply'),
            Reset: st('reset'), ShowAll: st('showAll'), Delete: st('delete'), AreYouSure: st('areYouSure'),
            ErrorOccurred: st('errorOccurred'),
            IsQualityRecord: t('isQualityRecord'), RequiresESignature: t('requiresESignature'),
            AttendanceMandatory: t('attendanceMandatory'),
            DeleteConfirmTitle: t('deleteConfirmTitle'), DeleteConfirmText: t('deleteConfirmText')
        };
    };

    const boolBadge = (value) => value
        ? `<span class="badge bg-label-success">${L.Yes}</span>`
        : `<span class="badge bg-label-secondary">${L.No}</span>`;

    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.meetingTypesFilterBound === '1') { return; }
        dtTableEl.dataset.meetingTypesFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) { return true; }
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) { return true; }

            if (appliedFilters.isQualityRecord && String(row.isQualityRecord) !== appliedFilters.isQualityRecord) { return false; }
            if (appliedFilters.requiresESignature && String(row.requiresESignature) !== appliedFilters.requiresESignature) { return false; }
            if (appliedFilters.attendanceMandatory && String(row.attendanceMandatory) !== appliedFilters.attendanceMandatory) { return false; }
            return true;
        });
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) { return; }
        $('#filterIsQualityRecord, #filterRequiresESignature, #filterAttendanceMandatory').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) { $s.select2('destroy'); }
            $s.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                placeholder: $s.data('placeholder') || '',
                width: 'element',
                minimumResultsForSearch: Infinity
            });
        });
    };

    const bindFilterButtons = (api) => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                isQualityRecord: document.getElementById('filterIsQualityRecord')?.value || '',
                requiresESignature: document.getElementById('filterRequiresESignature')?.value || '',
                attendanceMandatory: document.getElementById('filterAttendanceMandatory')?.value || ''
            };
            api.draw();
            const collapseEl = document.getElementById('inlineFilterCollapse');
            if (collapseEl) { bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide(); }
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            appliedFilters = { isQualityRecord: '', requiresESignature: '', attendanceMandatory: '' };
            $('#filterIsQualityRecord, #filterRequiresESignature, #filterAttendanceMandatory')
                .val('').trigger('change');
            api.draw();
        });
    };

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

    const deleteType = async (id) => {
        window.showConfirm(L.DeleteConfirmTitle, async () => {
            const result = await window.MeetingsApi.typesDelete(id);
            if (!result.ok) {
                window.DitenModal?.error?.({ title: L.ErrorOccurred, message: window.MeetingsApi.failureMessage(result) });
                return;
            }
            window.DitenModal?.success?.({ timer: 1200 });
            dt.ajax.reload();
        }, { type: 'danger', subtext: L.DeleteConfirmText, confirmButtonText: L.Delete });
    };

    const rowActionHandlers = {
        edit: ({ id }) => { if (id) { window.location.href = `/Meetings/MeetingTypes/${id}/Edit`; } },
        delete: ({ id }) => { if (id) { void deleteType(id); } }
    };

    const initDataTable = async () => {
        if (!dtTableEl) { return; }
        syncL10n();

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: '/Meetings/api/types',
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'name', name: 'name' },
                    { data: 'isQualityRecord', name: 'isQualityRecord' },
                    { data: 'requiresESignature', name: 'requiresESignature' },
                    { data: 'attendanceMandatory', name: 'attendanceMandatory' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 2, render: (data) => boolBadge(data) },
                    { targets: 3, render: (data) => boolBadge(data) },
                    { targets: 4, render: (data) => boolBadge(data) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => window.DitenDataTable.renderActions([
                            { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id } },
                            { key: 'delete', className: 'js-delete-item text-danger', icon: 'bx bx-trash', attrs: { 'data-id': full.id, title: L.Delete } }
                        ])
                    }
                ],
                buttons: window.DtDefaults.exportButtons(L.AddNew, { href: '/Meetings/MeetingTypes/Create' }, {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                        attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
                        action: () => toggleInlineFilter()
                    }
                }),
                initComplete: function () {
                    const api = this.api();
                    initSelect2Filters();
                    registerTableFilters();
                    mountInlineFilter();
                    bindFilterButtons(api);
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/Meetings/MeetingTypes/Create';
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

document.addEventListener('DOMContentLoaded', () => MeetingTypesList.init());
