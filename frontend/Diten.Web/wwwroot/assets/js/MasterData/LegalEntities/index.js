'use strict';

// MOD-0220 — Legal Entities (tenant shell, Master Data, Slim pattern). Backend list returns a plain array
// (Response<IReadOnlyList<T>>), so the DataTable is client-side (serverSide:false): one ajax fetch loads
// the list, paging/sort/search run in the browser. Create happens in an in-page offcanvas (POST → Draft);
// lifecycle (activate/archive) + delete are status-aware row actions.
const LegalEntitiesList = (function () {
    let dt;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-legal-entities');
    const endpoint = '/LegalEntities/api';
    const saveViewColumnIndexes = [1, 2, 3, 4];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { status: '' };

    let rowsData = [];

    const loadL10n = () => {
        const node = document.getElementById('legal-entities-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[LegalEntities] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const statusOf = (row) => String(row.lifecycleState ?? row.LifecycleState ?? '').toUpperCase();

    const statusBadge = (status) => {
        const s = String(status || '').toUpperCase();
        if (s === 'ACTIVE') return `<span class="badge bg-label-success">${escapeHtml(L.StatusActive || 'Active')}</span>`;
        if (s === 'ARCHIVED') return `<span class="badge bg-label-secondary text-muted">${escapeHtml(L.StatusArchived || 'Archived')}</span>`;
        return `<span class="badge bg-label-secondary">${escapeHtml(L.StatusDraft || 'Draft')}</span>`;
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
    };

    const patchLifecycle = (id, action, confirmKey, toastKey, name) => {
        window.showConfirm?.(L[confirmKey] || L.AreYouSure, async () => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, { method: 'PATCH', headers: getAuthHeaders() });
                if (!response.ok) throw new Error(`${action} failed.`);
                reloadWithSuccessToast(toastKey, name);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName: name, type: action === 'archive' ? 'warning' : 'primary', confirmButtonText: action === 'archive' ? L.Archive : L.Activate });
    };

    const rowActionHandlers = {
        activate: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (rowId) patchLifecycle(rowId, 'activate', 'ActivateConfirm', 'RecordActivated', row?.legalName || row?.LegalName || '');
        },
        archive: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (rowId) patchLifecycle(rowId, 'archive', 'ArchiveConfirm', 'RecordArchived', row?.legalName || row?.LegalName || '');
        },
        delete: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = row?.legalName || row?.LegalName || row?.code || row?.Code || '';
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

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    // Backend list rows expose `legalEntityId` (not `id`); normalize to `id` so the shared action
    // dispatcher and renderers behave like the other slim screens.
    const normalizeRow = (row) => ({ ...row, id: row.id || row.Id || row.legalEntityId || row.LegalEntityId });

    const applyClientFilter = (rows) => {
        if (appliedFilters.status === '') return rows;
        return rows.filter((r) => statusOf(r) === appliedFilters.status);
    };

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[LegalEntities] DataTable element or DtDefaults not found.');
            return;
        }

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                fetch(`${endpoint}`, { headers: getAuthHeaders() })
                    .then((response) => response.ok ? response.json() : Promise.reject(response))
                    .then((payload) => {
                        rowsData = unwrapList(payload).map(normalizeRow);
                        callback({ data: applyClientFilter(rowsData) });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'code', name: 'code', render: (value) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(value)}</span>` },
                { data: 'legalName', name: 'legalName', render: escapeHtml },
                { data: 'displayName', name: 'displayName', render: (value) => escapeHtml(value || '-') },
                { data: 'lifecycleState', name: 'lifecycleState', render: (value) => statusBadge(value) },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (value, type, row) => {
                        const id = row.id;
                        const rowJson = JSON.stringify(row);
                        const status = statusOf(row);

                        const actions = [];
                        if (status === 'DRAFT') {
                            actions.push({ key: 'activate', icon: 'bx bx-check-circle', className: 'text-success', text: L.Activate || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                            actions.push({ key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        } else if (status === 'ACTIVE') {
                            actions.push({ key: 'archive', icon: 'bx bx-archive-in', className: 'text-warning', text: L.Archive || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        }

                        return actions.length && window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '<span class="text-muted">—</span>';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNew || '', {}, { filterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                initSelect2Filters();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        });

        if (L.Showing) {
            dtConfig.language = dtConfig.language || {};
            dtConfig.language.info = `${L.Showing} _START_ - _END_ / _TOTAL_`;
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
            appliedFilters = { status: document.getElementById('filterStatus')?.value || '' };
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            appliedFilters = { status: '' };
            const element = document.getElementById('filterStatus');
            if (element) {
                element.value = '';
                if (window.jQuery?.fn?.select2) $(element).val('').trigger('change');
            }
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => value !== '').length;

    // ─── Create offcanvas (Slim pattern) ─────────────────────────────────────
    const getOcInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#formLegalEntity input[name="__RequestVerificationToken"]')?.value || '';

    const clearFormErrors = () => {
        const alertEl = document.getElementById('formLegalEntityAlert');
        if (alertEl) { alertEl.classList.add('d-none'); alertEl.innerHTML = ''; }
    };

    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formLegalEntityAlert');
        if (!alertEl) return;
        const list = (Array.isArray(errors) ? errors : [errors]).filter(Boolean);
        alertEl.innerHTML = list.length
            ? list.map((message) => `<div>${escapeHtml(message)}</div>`).join('')
            : escapeHtml(L.RequiredField || '');
        alertEl.classList.remove('d-none');
    };

    const resetForm = () => {
        const form = document.getElementById('formLegalEntity');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('legalEntityCode').value = '';
        document.getElementById('legalEntityLegalName').value = '';
        document.getElementById('legalEntityDisplayName').value = '';
        clearFormErrors();
    };

    const openCreateOffcanvas = () => {
        const oc = getOcInstance();
        if (!oc) {
            console.error('[LegalEntities] #offcanvasCreateEdit not found. Rebuild + restart the app so the partial renders.');
            return;
        }
        resetForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSaveLegalEntity');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        oc.show();
    };

    const submitForm = async () => {
        const form = document.getElementById('formLegalEntity');
        if (!form) return;
        clearFormErrors();
        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showFormErrors([L.RequiredField || '']);
            return;
        }

        const payload = {
            code: normalizeString(document.getElementById('legalEntityCode').value),
            legalName: normalizeString(document.getElementById('legalEntityLegalName').value),
            displayName: normalizeString(document.getElementById('legalEntityDisplayName').value) || null
        };

        const saveBtn = document.getElementById('btnSaveLegalEntity');
        if (saveBtn) saveBtn.disabled = true;
        try {
            const res = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken(),
                    ...getAuthHeaders()
                },
                body: JSON.stringify(payload)
            });
            if (res.ok) {
                getOcInstance()?.hide();
                reloadWithSuccessToast('RecordCreated', payload.legalName);
                return;
            }
            let errors = [];
            try {
                const json = await res.json();
                errors = (json.errors || json.Errors || []);
            } catch { /* non-JSON response */ }
            showFormErrors(errors.length ? errors : [L.ErrorOccurred || '']);
        } catch (error) {
            console.error('[LegalEntities] Form submit failed.', error);
            showFormErrors([L.ErrorOccurred || '']);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    const bindOffcanvas = () => {
        document.addEventListener('click', (event) => {
            if (event.target.closest('.add-new')) {
                event.preventDefault();
                openCreateOffcanvas();
            }
        });
        document.getElementById('btnSaveLegalEntity')?.addEventListener('click', submitForm);
    };

    const init = () => {
        loadL10n();
        bindFilters();
        bindOffcanvas();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => LegalEntitiesList.init());
