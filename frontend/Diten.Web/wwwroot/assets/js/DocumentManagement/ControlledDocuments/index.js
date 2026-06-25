/**
 * MOD-0029-FU01 - Controlled Documents library (TenantShell). Golden Compact DataTable: DtDefaults.create
 * toolbar (search, export, colVis, inline filter button, colReorder) + Add New (route to Create). Consumes the
 * FU01 backend exclusively through the same-origin proxy (/DocumentManagementControlledDocuments/list -> Gateway).
 * All UI text comes from window.L10n (no hardcoded EN/TR).
 */
'use strict';

const ControlledDocumentsList = (function () {
    let dt;
    let L = window.L10n || {};
    const tableEl = document.getElementById('dt-controlleddocuments');
    const filterCollapseId = 'inlineFilterCollapse';
    const filterHostId = 'inlineFilterHost';
    const canCreate = !!window.ControlledDocumentsPerms?.canCreate;
    const createUrl = window.ControlledDocumentsPerms?.createUrl || '/DocumentManagementControlledDocuments/Create';
    const baseOrder = [[6, 'desc']];
    let appliedFilters = { type: [], status: [] };

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const text = (v, fallback) => (v === null || v === undefined || v === '' ? (fallback || '-') : String(v));
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => String(i).trim().toUpperCase()).filter(Boolean)));
        const s = (typeof v === 'string' ? v.trim() : '');
        return s ? [s.toUpperCase()] : [];
    };

    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        if (Number.isNaN(d.getTime())) return String(v).slice(0, 10);
        const locale = window.CurrentLanguage || undefined;
        const datePart = new Intl.DateTimeFormat(locale, { month: 'short', day: '2-digit', year: '2-digit' }).format(d);
        const timePart = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', hour12: true }).format(d);
        return `<span class="d-inline-flex flex-column lh-sm"><span>${datePart}</span><small class="text-muted">${timePart}</small></span>`;
    };

    const typeLabel = (t) => {
        const map = {
            SOP: L.TypeSop, WORK_INSTRUCTION: L.TypeWorkInstruction, POLICY: L.TypePolicy,
            FORM: L.TypeForm, TEMPLATE: L.TypeTemplate, OTHER: L.TypeOther
        };
        return `<span class="badge bg-label-secondary">${text(map[String(t || '').toUpperCase()], t)}</span>`;
    };
    const statusBadge = (s) => {
        const v = String(s || '').toUpperCase();
        if (v === 'ACTIVE') return `<span class="badge bg-label-success">${text(L.StatusActive, 'Active')}</span>`;
        if (v === 'ARCHIVED') return `<span class="badge bg-label-secondary">${text(L.StatusArchived, 'Archived')}</span>`;
        return `<span class="badge bg-label-secondary">${text(L.Unknown, s)}</span>`;
    };

    const matches = (row) => {
        const t = normalizeArray(appliedFilters.type);
        const s = normalizeArray(appliedFilters.status);
        if (t.length && !t.includes(String(row.documentType || '').toUpperCase())) return false;
        if (s.length && !s.includes(String(row.status || '').toUpperCase())) return false;
        return true;
    };
    const getAppliedFilterCount = () => [appliedFilters.type, appliedFilters.status].filter((x) => normalizeArray(x).length > 0).length;

    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };
    const initSelect2 = (id, placeholder) => {
        if (!window.jQuery || !$.fn.select2) return;
        const $s = $('#' + id);
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        $s.select2({
            dropdownParent: $(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            containerCssClass: 'dt-inline-filter-multi',
            selectionCssClass: 'form-select form-select-sm',
            placeholder: $s.data('placeholder') || placeholder || '',
            minimumResultsForSearch: Infinity,
            width: 'element',
            closeOnSelect: false
        });
    };

    const setupFilters = (api) => {
        initSelect2('filterType', L.DocumentType);
        initSelect2('filterStatus', L.Status);
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { type: normalizeArray($('#filterType').val() || []), status: normalizeArray($('#filterStatus').val() || []) };
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            const c = document.getElementById(filterCollapseId);
            if (c) bootstrap.Collapse.getOrCreateInstance(c, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            appliedFilters = { type: [], status: [] };
            $('#filterType').val(null).trigger('change');
            $('#filterStatus').val(null).trigger('change');
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
        });
    };

    const init = () => {
        if (!tableEl || !window.jQuery || !window.jQuery.fn.DataTable || !window.DtDefaults || !window.DitenDataTable) {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
            return;
        }
        syncL10n();

        if (window.jQuery?.fn?.dataTable?.ext?.search && tableEl.dataset.bound !== '1') {
            tableEl.dataset.bound = '1';
            $.fn.dataTable.ext.search.push((settings, _d, dataIndex, rowData) => {
                if (settings.nTable !== tableEl) return true;
                const row = rowData || dt?.row(dataIndex)?.data?.() || null;
                return row ? matches(row) : true;
            });
        }

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => {
                    const c = document.getElementById(filterCollapseId);
                    if (c) bootstrap.Collapse.getOrCreateInstance(c, { toggle: false }).toggle();
                }
            }
        };

        dt = new DataTable(tableEl, window.DtDefaults.create({
            ajax: {
                url: '/DocumentManagementControlledDocuments/list',
                type: 'GET',
                xhrFields: { withCredentials: true },
                dataSrc: function (json) {
                    if (json && json.isSuccessful === false) {
                        window.showToast?.(text(L.ErrorOccurred, 'Error'), 'error');
                        return [];
                    }
                    return (json && (json.data || json.Data)) || [];
                }
            },
            language: { emptyTable: text(L.EmptyList, '') },
            order: baseOrder,
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            columns: [
                { data: 'id', orderable: false, searchable: false, className: 'control', render: () => '' },
                { data: 'title', render: (d) => `<span class="fw-medium text-heading">${text(d)}</span>` },
                { data: 'documentType', className: 'all', render: (d, type) => (type === 'display' ? typeLabel(d) : d) },
                { data: 'collectionPath' },
                { data: 'currentVersionNumber', render: (d) => `v${text(d, '1')}` },
                { data: 'status', render: (d, type) => (type === 'display' ? statusBadge(d) : d) },
                { data: 'createdAt', render: (d, type) => (type === 'display' ? formatDate(d) : d) },
                {
                    data: 'id', orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all',
                    render: (id) => window.DitenDataTable.renderActions([
                        { key: 'details', className: 'btn-text-secondary', icon: 'bx bx-show', text: text(L.ViewDetails, 'Details'), attrs: { 'data-id': id } },
                        { key: 'edit', icon: 'bx bx-edit', text: text(L.EditMetadata, 'Edit'), attrs: { 'data-id': id } }
                    ])
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                canCreate ? (L.AddDocument || 'Add Document') : null,
                canCreate ? { href: createUrl, title: L.AddDocument, 'data-bs-toggle': 'tooltip' } : null,
                extraButtons,
                { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }
            ),
            initComplete: function () {
                document.getElementById('skeleton-loader')?.classList.add('d-none');
                mountInlineFilter();
                setupFilters(this.api());
                document.querySelector('.add-new')?.addEventListener('click', (e) => { e.preventDefault(); window.location.href = createUrl; });
            },
            drawCallback: function () {
                document.getElementById('skeleton-loader')?.classList.add('d-none');
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        }));

        window.DitenDataTable.bindActionDispatcher({
            tableEl,
            dt,
            onRowAction: {
                details: ({ id }) => { if (id) window.location.href = `/DocumentManagementControlledDocuments/Details/${id}`; },
                edit: ({ id }) => { if (id) window.location.href = `/DocumentManagementControlledDocuments/Edit/${id}`; }
            }
        });
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ControlledDocumentsList.init());
