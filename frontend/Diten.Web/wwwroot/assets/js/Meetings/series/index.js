'use strict';

/*
 * S11 — Meeting Series DataTable Index script, mirroring Meetings/types/index.js's own shape (Golden Reference
 * Compact contract markers: id="skeleton-loader", data-dt-standard="v2"). No pagination server-side and no
 * inline filter — a tenant's recurring-series catalogue is small by nature (a handful of governed cadences),
 * the same posture MeetingTypes' own list already takes for its own catalogue.
 */
const MeetingSeriesList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-meeting-series');
    let L = {};

    /*
     * MEASURED (S7's own lesson, applied here before it bites twice): Platform serializes enums as their
     * INTEGER ordinal — MeetingSeriesFrequency { Weekly: 0, Monthly: 1, Quarterly: 2, Yearly: 3 }.
     */
    const FREQUENCY = { WEEKLY: 0, MONTHLY: 1, QUARTERLY: 2, YEARLY: 3 };

    const syncL10n = () => {
        const t = (key) => window.MeetingSeriesL10n?.t?.(key) ?? key;
        const st = (key) => window.MeetingsL10n?.t?.(key) ?? key;
        L = {
            AddNew: t('formTitleCreate'), Actions: t('actions'),
            Edit: st('edit'), Yes: st('yes'), No: st('no'), Delete: st('delete'), AreYouSure: st('areYouSure'),
            ErrorOccurred: st('errorOccurred'),
            LastGeneratedAtNever: t('lastGeneratedAtNever'),
            DeleteConfirmTitle: t('deleteConfirmTitle'), DeleteConfirmText: t('deleteConfirmText'),
            FrequencyLabels: [t('frequencyWeekly'), t('frequencyMonthly'), t('frequencyQuarterly'), t('frequencyYearly')]
        };
    };

    const frequencyLabelFor = (value) => L.FrequencyLabels[value] ?? value;

    const boolBadge = (value) => value
        ? `<span class="badge bg-label-success">${L.Yes}</span>`
        : `<span class="badge bg-label-secondary">${L.No}</span>`;

    const formatLastGenerated = (value) => value
        ? new Date(value).toLocaleString(window.CurrentLanguage || undefined)
        : L.LastGeneratedAtNever;

    const deleteSeries = async (id) => {
        window.showConfirm(L.DeleteConfirmTitle, async () => {
            const result = await window.MeetingsApi.seriesDelete(id);
            if (!result.ok) {
                window.DitenModal?.error?.({ title: L.ErrorOccurred, message: window.MeetingsApi.failureMessage(result) });
                return;
            }
            window.DitenModal?.success?.({ timer: 1200 });
            dt.ajax.reload();
        }, { type: 'danger', subtext: L.DeleteConfirmText, confirmButtonText: L.Delete });
    };

    const rowActionHandlers = {
        edit: ({ id }) => { if (id) { window.location.href = `/Meetings/Series/${id}/Edit`; } },
        delete: ({ id }) => { if (id) { void deleteSeries(id); } }
    };

    const initDataTable = async () => {
        if (!dtTableEl) { return; }
        syncL10n();

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: '/Meetings/api/series',
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'name', name: 'name' },
                    { data: 'meetingTypeName', name: 'meetingTypeName' },
                    { data: 'frequency', name: 'frequency' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'lastGeneratedAt', name: 'lastGeneratedAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 2, render: (data) => data ?? '-' },
                    { targets: 3, render: (data) => frequencyLabelFor(data) },
                    { targets: 4, render: (data) => boolBadge(data) },
                    { targets: 5, render: (data) => formatLastGenerated(data) },
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
                buttons: window.DtDefaults.exportButtons(L.AddNew, { href: '/Meetings/Series/Create' }),
                initComplete: function () {
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/Meetings/Series/Create';
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

document.addEventListener('DOMContentLoaded', () => MeetingSeriesList.init());
