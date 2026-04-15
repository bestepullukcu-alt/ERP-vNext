'use strict';

const ProductsList = (function () {
    let dt;
    let L = window.L10n || {};

    const dtTableEl = document.querySelector('.datatables-products');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const lookupsPayloadEl = document.getElementById('products-lookups');

    const readLookups = () => {
        if (!lookupsPayloadEl) {
            return { categories: [], lifecycleStates: [] };
        }

        try {
            return JSON.parse(lookupsPayloadEl.textContent || '{}');
        } catch (error) {
            console.error('[Products] lookup payload parse error', error);
            return { categories: [], lifecycleStates: [] };
        }
    };

    const lookups = readLookups();

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        return parts.length === 2 ? parts.pop().split(';').shift() : null;
    };

    const getTenantId = () => {
        try {
            return JSON.parse(localStorage.getItem('user') || '{}').tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (error) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getAuthHeaders = (includeJsonContentType = false) => {
        const token = getCookie('access_token');
        const headers = {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };

        if (includeJsonContentType) {
            headers['Content-Type'] = 'application/json';
        }

        return headers;
    };

    const getProductTypeText = (typeCode) => {
        switch ((typeCode || '').toUpperCase()) {
            case 'FINISHED_PRODUCT': return L.ProductTypeFinished || 'Finished Product';
            case 'SEMI_FINISHED_PRODUCT': return L.ProductTypeSemiFinished || 'Semi-Finished Product';
            case 'SERVICE': return L.ProductTypeService || 'Service';
            case 'TECHNOLOGY': return L.ProductTypeTechnology || 'Technology';
            default: return L.ProductTypeUnknown || 'Unknown';
        }
    };

    const getCapabilitiesText = (row) => {
        const capabilities = [];
        if (row.isSaleable) capabilities.push(L.CapabilitySaleable || 'Saleable');
        if (row.isPurchasable) capabilities.push(L.CapabilityPurchasable || 'Purchasable');
        if (row.isManufacturable) capabilities.push(L.CapabilityManufacturable || 'Manufacturable');
        return capabilities.length ? capabilities.join(', ') : '-';
    };

    const escapeRegex = (value) => String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

    const setMultiColumnSearch = (api, columnName, values) => {
        const selected = Array.isArray(values) ? values.filter(Boolean) : [];
        if (!selected.length) {
            api.column(`${columnName}:name`).search('');
            return;
        }

        const pattern = `^(${selected.map((x) => escapeRegex(x)).join('|')})$`;
        api.column(`${columnName}:name`).search(pattern, true, false);
    };

    const getSelectedIds = () => Array.from(dtTableEl.querySelectorAll('.dt-checkboxes:checked')).map((checkbox) => checkbox.value);

    const updateBulkBar = () => {
        const count = getSelectedIds().length;
        document.getElementById('bulkActionBar')?.classList.toggle('d-none', count === 0);
        const countEl = document.getElementById('bulkSelectedCount');
        if (countEl) {
            countEl.textContent = count;
        }
    };

    const clearSelection = () => {
        dtTableEl.querySelectorAll('.dt-checkboxes').forEach((checkbox) => {
            checkbox.checked = false;
            checkbox.closest('tr')?.classList.remove('selected');
        });
        const selectAll = dtTableEl.querySelector('.dt-checkboxes-select-all');
        if (selectAll) selectAll.checked = false;
        updateBulkBar();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
        }
    };

    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!btn || !collapseEl || btn.dataset.bound) {
            return;
        }

        btn.dataset.bound = '1';
        btn.addEventListener('click', (event) => {
            event.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false });
            collapseEl.classList.contains('show') ? instance.hide() : instance.show();
        });
    };

    const buildFilterOptions = () => {
        const productTypes = [
            { code: 'FINISHED_PRODUCT', text: L.ProductTypeFinished || 'Finished Product' },
            { code: 'SEMI_FINISHED_PRODUCT', text: L.ProductTypeSemiFinished || 'Semi-Finished Product' },
            { code: 'SERVICE', text: L.ProductTypeService || 'Service' },
            { code: 'TECHNOLOGY', text: L.ProductTypeTechnology || 'Technology' }
        ];

        const typeEl = document.getElementById('filterProductType');
        const categoryEl = document.getElementById('filterCategory');
        const lifecycleEl = document.getElementById('filterLifecycleState');

        if (typeEl) {
            typeEl.innerHTML = productTypes.map((option) => `<option value="${option.code}">${option.text}</option>`).join('');
        }

        if (categoryEl) {
            categoryEl.innerHTML = (lookups.categories || [])
                .map((option) => `<option value="${option.name}">${option.name}</option>`)
                .join('');
        }

        if (lifecycleEl) {
            lifecycleEl.innerHTML = (lookups.lifecycleStates || [])
                .map((option) => `<option value="${option.name}">${option.name}</option>`)
                .join('');
        }

        if (window.jQuery && $.fn.select2) {
            const $dropdownParent = $(document.body);
            $('#filterProductType, #filterCategory, #filterLifecycleState').select2({
                dropdownParent: $dropdownParent,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                allowClear: true,
                placeholder: ''
            });
        }
    };

    const applyFilters = () => {
        const typeValues = $('#filterProductType').val() || [];
        const categoryValues = $('#filterCategory').val() || [];
        const lifecycleValues = $('#filterLifecycleState').val() || [];

        setMultiColumnSearch(dt, 'productType', typeValues);
        setMultiColumnSearch(dt, 'category', categoryValues);
        setMultiColumnSearch(dt, 'lifecycle', lifecycleValues);

        dt.draw();
        const count = [typeValues, categoryValues, lifecycleValues].reduce((sum, arr) => sum + (Array.isArray(arr) ? arr.length : 0), 0);
        window.DtDefaults?.updateVisualState?.(dt, count);
    };

    const resetFilters = () => {
        $('#filterProductType, #filterCategory, #filterLifecycleState').val(null).trigger('change');
        applyFilters();
    };

    const populateOffcanvas = (data) => {
        if (!data) return;

        document.getElementById('oc-title').innerText = data.name || '-';
        document.getElementById('oc-subtitle').innerText = data.shortName || '-';
        document.getElementById('oc-code').innerText = data.code || '-';
        document.getElementById('oc-type').innerText = getProductTypeText(data.productTypeCode);
        document.getElementById('oc-category').innerText = data.category || '-';
        document.getElementById('oc-lifecycle').innerText = data.lifecycleState || '-';
        document.getElementById('oc-capabilities').innerText = getCapabilitiesText(data);
        document.getElementById('oc-btn-edit').href = `/Products/Edit/${data.id}`;
    };

    const initDataTable = () => {
        if (!dtTableEl) return;

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: `${apiUrl}/api/products`,
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
                { data: 'name', name: 'name' },
                { data: 'productTypeCode', name: 'productType' },
                { data: 'category', name: 'category' },
                { data: 'lifecycleState', name: 'lifecycle' },
                { data: null, name: 'capabilities' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', orderable: false, searchable: false, render: () => '' },
                {
                    targets: 1,
                    className: 'dt-checkboxes-cell cell-fit',
                    orderable: false,
                    searchable: false,
                    render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">`
                },
                {
                    targets: 4,
                    render: (data) => getProductTypeText(data)
                },
                {
                    targets: 7,
                    orderable: false,
                    render: (data, type, row) => type === 'display' ? `<span class="text-muted">${getCapabilitiesText(row)}</span>` : getCapabilitiesText(row)
                },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit text-end',
                    render: (data, type, row) => `
                        <div class="d-flex align-items-center justify-content-end">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview">${L.Details}</a>
                                <a href="/Products/Details/${row.id}" class="dropdown-item">${L.Details}</a>
                                <a href="/Products/Edit/${row.id}" class="dropdown-item">${L.Edit}</a>
                            </div>
                        </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewProduct,
                { onclick: "window.location.href='/Products/Create'" },
                {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn'
                    }
                },
                {
                    exportColumns: [2, 3, 4, 5, 6, 7],
                    colvisColumns: [2, 3, 4, 5, 6, 7]
                }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                buildFilterOptions();
            },
            drawCallback: function () {
                window.DtDefaults?.updateVisualState?.(this.api(), 0);
            }
        }));
    };

    const bindEvents = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', applyFilters);
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            resetFilters();
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', clearSelection);

        document.getElementById('btnBulkDelete')?.addEventListener('click', () => {
            const ids = getSelectedIds();
            if (!ids.length) return;

            window.showConfirm?.(L.BulkDeleteConfirm || L.AreYouSure, async () => {
                const response = await fetch(`${apiUrl}/api/products/bulk`, {
                    method: 'DELETE',
                    headers: getAuthHeaders(true),
                    body: JSON.stringify({ ids })
                });

                if (response.ok) {
                    clearSelection();
                    dt.ajax.reload(() => {
                        const message = (L.BulkDeleteSuccess || '').replace('{0}', ids.length);
                        window.showToast?.(message || L.RecordDeleted, 'success');
                    }, false);
                } else {
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            });
        });

        dtTableEl?.addEventListener('click', (event) => {
            const tr = event.target.closest('tr');
            if (!tr) return;
            const rowEl = tr.classList.contains('child') ? tr.previousElementSibling : tr;
            const data = dt.row(rowEl).data();

            if (event.target.closest('.js-quick-view')) {
                populateOffcanvas(data);
            }

            if (event.target.closest('.delete-record')) {
                window.showConfirm?.(L.AreYouSure, async () => {
                    const response = await fetch(`${apiUrl}/api/products/${data.id}`, {
                        method: 'DELETE',
                        headers: getAuthHeaders()
                    });

                    if (response.ok) {
                        dt.ajax.reload(() => window.showToast?.(L.RecordDeleted, 'success'), false);
                    } else {
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                });
            }
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            $(this).closest('tr').toggleClass('selected', this.checked);
            updateBulkBar();
        });

        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const checked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach((checkbox) => {
                checkbox.checked = checked;
                checkbox.closest('tr')?.classList.toggle('selected', checked);
            });
            updateBulkBar();
        });
    };

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
        }
    };

    return {
        init: () => {
            syncL10n();
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ProductsList.init());
