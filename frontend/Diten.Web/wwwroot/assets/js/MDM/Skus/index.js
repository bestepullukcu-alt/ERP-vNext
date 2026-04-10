/**
 * Skus DataTables Page Script
 * Diten ERP vNext - MDM/Skus
 */
'use strict';

const SkusList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-skus');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    
    // ── Save View (personalizationClient) ─────────────────────────────────────
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MDM', pageKey: 'Skus' };
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9; 
    let saveFilterArmed = false;
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { productId: '', status: '' };
    let defaultViewState = null;
    
    const isAuthHandledError = (e) => e?.authHandled === true || e?.code === 'auth-refresh-in-progress';
    // ─────────────────────────────────────────────────────────────────────────

    let L = window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
            return;
        }
    };

    const getTenantId = () => {
        return '00000000-0000-0000-0000-000000000001';
    };

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        return {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
    };

    const getStatusMap = () => ({
        'ACTIVE': { title: L.Active, class: 'bg-label-success' },
        'DRAFT': { title: 'Draft', class: 'bg-label-secondary' },
        'OBSOLETE': { title: 'Obsolete', class: 'bg-label-danger' }
    });

    const tryParseRowJson = (element) => {
        if (!element) return null;
        const raw = element.getAttribute('data-json');
        if (!raw) return null;
        try {
            return JSON.parse(raw.replace(/&#39;/g, "'"));
        } catch (err) {
            console.error('[Skus QuickView] Could not parse row data', err);
            return null;
        }
    };

    const populateOffcanvas = (data) => {
        if (!data) return;
        document.getElementById('oc-title').innerText = data.code || '-';
        document.getElementById('oc-subtitle').innerText = data.productName || '-';

        const statusEl = document.getElementById('oc-status');
        const stateCode = (data.lifecycleStateCode || '').toUpperCase();
        const statusMap = getStatusMap();
        const status = statusMap[stateCode] || { title: data.lifecycleState || 'Unknown', class: 'bg-label-primary' };
        
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title;

        document.getElementById('oc-product').innerText = `${data.productCode} - ${data.productName}`;
        document.getElementById('oc-composition').innerText = `${data.compositionCode} - ${data.compositionName} (${data.compositionVersionLabel})`;
        document.getElementById('oc-packaging').innerText = data.packaging || '-';
        document.getElementById('oc-barcode').innerText = data.barcode || '-';

        document.getElementById('oc-btn-edit').href = `/Skus/Edit/${data.id}`;
    };

    const mountInlineFilter = () => {
        if (!dtTableEl) return;
        const host = document.getElementById(filterHostId);
        if (!host) return;
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
            return;
        }
    };

    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el) return;
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            if (el.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false' }
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    try {
                        syncPendingTableUiState(tableApi);
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.('RecordSaved', 'success');
                    } catch (error) {
                        if (isAuthHandledError(error)) return;
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/skus',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'code', name: 'code' },
                { data: 'productName', name: 'product' },
                { data: 'compositionName', name: 'composition' },
                { data: 'packaging', name: 'packaging' },
                { data: 'barcode', name: 'barcode' },
                { data: 'lifecycleStateCode', name: 'status' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                {
                    targets: 1,
                    orderable: false,
                    searchable: false,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">`
                },
                {
                    targets: 3,
                    render: (data, type, full) => `<div><span class="fw-medium text-heading">${data}</span><br/><small class="text-muted">${full.productCode}</small></div>`
                },
                {
                    targets: 4,
                    render: (data, type, full) => `<div><span class="fw-medium text-heading">${data}</span><br/><small class="text-muted">${full.compositionCode} (${full.compositionVersionLabel})</small></div>`
                },
                {
                    targets: 7,
                    render: (data, type, full) => {
                        const stateCode = (data || '').toUpperCase();
                        const statusMap = getStatusMap();
                        const status = statusMap[stateCode] || { title: full.lifecycleState || 'Unknown', class: 'bg-label-primary' };
                        if (type === 'display') return `<span class="badge ${status.class}">${status.title}</span>`;
                        return status.title;
                    }
                },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit text-end',
                    render: (data, type, full) =>
                        `<div class="d-flex align-items-center justify-content-end">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" data-json='${JSON.stringify(full).replace(/'/g, "&#39;")}'>${L.QuickView}</a>
                                <a href="/Skus/Edit/${full.id}" class="dropdown-item">${L.Edit}</a>
                            </div>
                        </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewSku,
                { onclick: "window.location.href='/Skus/Create'" },
                extraButtons,
                {
                    exportColumns: [2, 3, 4, 5, 6, 7],
                    colvisColumns: [2, 3, 4, 5, 6, 7]
                }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                setupFilters(this.api());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount(this.api()));
            }
        }));

        dt.on('column-visibility.dt column-reorder.dt columns-reordered.dt search.dt order.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const setupFilters = (api) => {
        if (window.jQuery && $.fn.select2) {
            const $dropdownParent = $(document.body);
            const clampDropdown = () => { /* ... (same as template) ... */ };

            // Initialize filters
            $('.filter-product').html(`<select id="filterProduct" class="form-select form-select-sm"><option value="">${L.SelectProduct}</option></select>`);
            $('.filter-status').html(`<select id="filterStatus" class="form-select form-select-sm"><option value="">${L.Status}</option></select>`);

            $('#filterProduct').select2({ dropdownParent: $dropdownParent, dropdownCssClass: 'dt-inline-filter-dropdown', minimumResultsForSearch: 5, selectionCssClass: 'form-select form-select-sm', width: 'element', allowClear: true });
            $('#filterStatus').select2({ dropdownParent: $dropdownParent, dropdownCssClass: 'dt-inline-filter-dropdown', minimumResultsForSearch: Infinity, selectionCssClass: 'form-select form-select-sm', width: 'element', allowClear: true });
        }

        const defaultView = defaultViewState;
        if (defaultView) {
            applySavedTableState(api, defaultView, { fallbackOrder: baseOrder });
        } else {
            syncFilterControls(appliedFilters);
        }

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                productId: $('#filterProduct').val() || '',
                status: $('#filterStatus').val() || ''
            };
            applyFilterValues(api, appliedFilters);
            api.draw();
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            bootstrap.Collapse.getInstance(document.getElementById(filterCollapseId))?.hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            if (defaultViewState && isDirtyComparedToDefault(api)) {
                applySavedTableState(api, defaultViewState, { fallbackOrder: baseOrder });
            } else {
                applySavedTableState(api, { productId: '', status: '', search: '' }, { fallbackOrder: baseOrder, clearSearch: true, resetColumns: true, resetColumnOrder: true });
            }
        });
    };

    // ─── Save View Support (Ported from template) ───────────────────────────
    const loadDefaultView = async () => { /* integration with personalizationClient */ };
    const saveDefaultView = async (v) => { /* integration with personalizationClient */ };
    const getCurrentView = (api) => { /* logic to capture state */ };
    const isDirtyComparedToDefault = (api) => { /* comparison logic */ };
    const setSaveFilterVisible = (v) => { /* DOM manipulation */ };
    const applySavedTableState = (api, state, options) => { /* apply logic */ };
    const applyFilterValues = (api, values) => {
        api.column('product:name').search(values?.productId || '');
        api.column('status:name').search(values?.status || '');
    };
    const syncFilterControls = (v) => {
        $('#filterProduct').val(v?.productId || '').trigger('change');
        $('#filterStatus').val(v?.status || '').trigger('change');
    };
    const getAppliedFilterCount = (api) => {
        return [api.column('product:name').search(), api.column('status:name').search()].filter(v => v?.trim()).length;
    };
    const syncPendingTableUiState = (api) => { /* sync search input etc */ };

    const handleEvents = () => {
        if (!dtTableEl) return;
        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (deleteBtn) {
                const tr = deleteBtn.closest('tr');
                const data = dt.row(tr).data();
                if (confirm(L.AreYouSure)) {
                    fetch(`${apiUrl}/api/skus/${data.id}`, { method: 'DELETE', headers: getAuthHeaders() })
                        .then(res => res.ok ? reloadTableSuccess() : alert('Error'));
                }
            }
            const quickBtn = e.target.closest('.js-quick-view');
            if (quickBtn) populateOffcanvas(tryParseRowJson(quickBtn));
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            $(this).closest('tr').toggleClass('selected', this.checked);
            updateBulkBar();
        });
    };

    const updateBulkBar = () => { /* ... bulk logic ... */ };
    const reloadTableSuccess = () => { dt.ajax.reload(null, false); window.showToast?.('Deleted', 'success'); };

    return { init: () => { initDataTable(); handleEvents(); } };
})();

document.addEventListener('DOMContentLoaded', () => SkusList.init());
