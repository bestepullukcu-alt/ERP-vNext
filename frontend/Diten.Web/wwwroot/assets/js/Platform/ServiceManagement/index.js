'use strict';

const ServiceManagementList = (function () {
    let dt;
    let editingId = null;
    let L = window.L10n || {};
    const dtTableEl = document.querySelector('.datatables-service-management');
    const endpoint = '/Platform/ServiceManagement/api';
    const saveViewColumnIndexes = [1, 2, 3, 4];
    const baseOrder = [[3, 'asc']];
    let appliedFilters = { isActive: '' };

    const syncL10n = () => { L = window.L10n || {}; };
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');

    const activeBadge = (value) => value
        ? `<span class="badge bg-label-success">${escapeHtml(L.StatusActive || 'Active')}</span>`
        : `<span class="badge bg-label-secondary">${escapeHtml(L.StatusInactive || 'Inactive')}</span>`;

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
    };

    const rowActionHandlers = {
        edit: ({ id, row }) => {
            const editId = id || row?.id || row?.Id;
            if (editId) openEditOffcanvas(String(editId));
        },
        delete: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = row?.displayName || row?.DisplayName || row?.code || row?.Code || '';
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(rowId)}`, { method: 'DELETE', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted', entityName);
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }, { entityName, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const buildQuery = (data) => {
        const params = new URLSearchParams();
        params.set('page', String(Math.floor((data.start || 0) / (data.length || 20)) + 1));
        params.set('pageSize', String(data.length || 20));
        const order = Array.isArray(data.order) ? data.order[0] : null;
        const orderColumn = order ? data.columns?.[order.column] : null;
        const sortName = orderColumn?.name || orderColumn?.data || 'sortOrder';
        params.set('sort', `${sortName}:${order?.dir || 'asc'}`);
        const search = data.search?.value || '';
        if (search) params.set('search', search);
        Object.entries(appliedFilters).forEach(([key, value]) => {
            if (value !== '') params.set(key, value);
        });
        return params.toString();
    };

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[ServiceManagement] DataTable element or DtDefaults not found.');
            return;
        }

        const addNewAttr = {};
        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                fetch(`${endpoint}?${buildQuery(data)}`, { headers: getAuthHeaders() })
                    .then((response) => response.ok ? response.json() : Promise.reject(response))
                    .then((payload) => {
                        const result = payload.data || payload.Data || {};
                        callback({
                            data: result.items || result.Items || [],
                            recordsTotal: result.totalCount || result.TotalCount || 0,
                            recordsFiltered: result.totalCount || result.TotalCount || 0
                        });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'code', name: 'code', render: (data) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(data)}</span>` },
                { data: 'displayName', name: 'displayName', render: escapeHtml },
                { data: 'sortOrder', name: 'sortOrder', render: (data) => escapeHtml(data ?? 0) },
                { data: 'isActive', name: 'isActive', render: (data) => activeBadge(data) },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (data, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);

                        const actions = [
                            {
                                key: 'edit',
                                icon: 'bx bx-edit',
                                className: 'js-edit-item',
                                text: L.Edit || '',
                                attrs: { 'data-id': id, 'data-json': rowJson }
                            },
                            {
                                key: 'delete',
                                icon: 'bx bx-trash',
                                className: 'text-danger',
                                text: L.Delete || '',
                                attrs: { 'data-id': id, 'data-json': rowJson }
                            }
                        ];

                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewService || '', addNewAttr, { filterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                initSelect2Filters();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                // Add New is delegated at document level (see bindOffcanvas) so it works regardless of
                // when DataTables renders the toolbar button.
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        });

        if (window.L10n?.Showing) {
            dtConfig.language = dtConfig.language || {};
            dtConfig.language.info = `${window.L10n.Showing} _START_ - _END_ / _TOTAL_`;
        }

        dt = new DataTable(dtTableEl, dtConfig);

        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl: dtTableEl,
            dt,
            onRowAction: rowActionHandlers
        });
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: true,
                allowClear: true
            });
        });
    };

    const bindFilters = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                isActive: document.getElementById('filterActive')?.value || ''
            };
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            appliedFilters = { isActive: '' };
            const element = document.getElementById('filterActive');
            if (element) {
                element.value = '';
                if (window.jQuery?.fn?.select2) $(element).val('').trigger('change');
            }
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => value !== '').length;

    // ─── Create/Edit offcanvas (Slim pattern) ────────────────────────────────
    const getOcInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#formServiceManagement input[name="__RequestVerificationToken"]')?.value || '';

    const normalizeCode = (value) => (value || '')
        .toUpperCase()
        .replace(/[^A-Z0-9]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-+|-+$/g, '')
        .slice(0, 80)
        .replace(/-+$/g, '');

    const updateCodePreview = () => {
        const input = document.getElementById('serviceCode');
        const preview = document.getElementById('serviceCodePreview');
        const span = preview?.querySelector('span');
        if (!input || !preview || !span) return;
        const normalized = normalizeCode(input.value);
        if (normalized && normalized !== input.value) {
            span.textContent = normalized;
            preview.classList.remove('d-none');
        } else {
            preview.classList.add('d-none');
        }
    };

    const clearFormErrors = () => {
        const alertEl = document.getElementById('formServiceManagementAlert');
        if (alertEl) { alertEl.classList.add('d-none'); alertEl.innerHTML = ''; }
    };

    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formServiceManagementAlert');
        if (!alertEl) return;
        const list = (Array.isArray(errors) ? errors : [errors]).filter(Boolean);
        alertEl.innerHTML = list.length
            ? list.map((message) => `<div>${escapeHtml(message)}</div>`).join('')
            : escapeHtml(L.RequiredField || '');
        alertEl.classList.remove('d-none');
    };

    const resetForm = () => {
        const form = document.getElementById('formServiceManagement');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('serviceItemId').value = '';
        document.getElementById('serviceCode').value = '';
        document.getElementById('serviceDisplayName').value = '';
        document.getElementById('serviceDescription').value = '';
        document.getElementById('serviceSortOrder').value = '0';
        document.getElementById('serviceIsActive').checked = true;
        document.getElementById('serviceCodePreview')?.classList.add('d-none');
        clearFormErrors();
    };

    const openCreateOffcanvas = () => {
        const oc = getOcInstance();
        if (!oc) {
            console.error('[ServiceManagement] #offcanvasCreateEdit not found in DOM. Rebuild + restart the app so the _CreateEditOffcanvas partial renders.');
            return;
        }
        editingId = null;
        resetForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNewService || '';
        const saveBtn = document.getElementById('btnSaveService');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        oc.show();
    };

    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.Edit || '';
        const saveBtn = document.getElementById('btnSaveService');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';

        try {
            const res = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { headers: getAuthHeaders() });
            if (!res.ok) throw new Error('Failed to load item.');
            const payload = await res.json();
            const d = payload.data || payload.Data || {};
            document.getElementById('serviceItemId').value = d.id || d.Id || '';
            document.getElementById('serviceCode').value = d.code || d.Code || '';
            document.getElementById('serviceDisplayName').value = d.displayName || d.DisplayName || '';
            document.getElementById('serviceDescription').value = d.description || d.Description || '';
            document.getElementById('serviceSortOrder').value = d.sortOrder ?? d.SortOrder ?? 0;
            document.getElementById('serviceIsActive').checked = (d.isActive ?? d.IsActive) !== false;
        } catch (error) {
            console.error('[ServiceManagement] Failed to load item for edit.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
            return;
        }
        getOcInstance()?.show();
    };

    const localizeError = (code) => (code === 'SERVICE_CODE_IN_USE' ? (L.ServiceCodeInUse || code) : code);

    const submitForm = async () => {
        const form = document.getElementById('formServiceManagement');
        if (!form) return;
        clearFormErrors();
        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showFormErrors([L.RequiredField || '']);
            return;
        }

        const isEdit = !!editingId;
        const sortOrderRaw = document.getElementById('serviceSortOrder').value;
        const payload = {
            code: normalizeCode(document.getElementById('serviceCode').value),
            displayName: normalizeString(document.getElementById('serviceDisplayName').value),
            description: normalizeString(document.getElementById('serviceDescription').value) || null,
            sortOrder: sortOrderRaw === '' ? 0 : Number(sortOrderRaw),
            isActive: document.getElementById('serviceIsActive').checked
        };
        const url = isEdit ? `${endpoint}/${encodeURIComponent(editingId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';

        const saveBtn = document.getElementById('btnSaveService');
        if (saveBtn) saveBtn.disabled = true;
        try {
            const res = await fetch(url, {
                method,
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken(),
                    ...getAuthHeaders()
                },
                body: JSON.stringify(payload)
            });
            if (res.ok) {
                getOcInstance()?.hide();
                reloadWithSuccessToast(isEdit ? 'RecordUpdated' : 'RecordCreated', payload.displayName);
                return;
            }
            let errors = [];
            try {
                const json = await res.json();
                errors = (json.errors || json.Errors || []).map(localizeError);
            } catch { /* non-JSON response */ }
            showFormErrors(errors.length ? errors : [L.ErrorOccurred || '']);
        } catch (error) {
            console.error('[ServiceManagement] Form submit failed.', error);
            showFormErrors([L.ErrorOccurred || '']);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    const bindOffcanvas = () => {
        // Delegated: the Add New button is rendered by DataTables into the toolbar, so bind at document level.
        document.addEventListener('click', (event) => {
            if (event.target.closest('.add-new')) {
                event.preventDefault();
                openCreateOffcanvas();
            }
        });
        document.getElementById('btnSaveService')?.addEventListener('click', submitForm);
        document.getElementById('serviceCode')?.addEventListener('input', function () {
            this.value = this.value.toUpperCase();
            updateCodePreview();
        });
    };

    const init = () => {
        syncL10n();
        bindFilters();
        bindOffcanvas();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ServiceManagementList.init());
