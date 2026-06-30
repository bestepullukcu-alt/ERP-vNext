/**
 * Module Catalog Page Details / Actions tab.
 * Client-side DataTable (serverSide:false) — fetches the flat action list once and
 * mirrors the sibling Pages table (module-pages.js / #dt-module-pages) for standards conformance.
 */
'use strict';

const ModulePageDetails = (function () {
    const tableEl = document.getElementById('dt-module-page-actions');
    const skeletonEl = document.getElementById('module-page-actions-skeleton-loader');
    const pageId = tableEl?.dataset.pageId || '';
    const moduleCode = tableEl?.dataset.moduleCode || '';
    const pageCode = tableEl?.dataset.pageCode || '';
    const isReadonly = (tableEl?.dataset.readonly || '').toLowerCase() === 'true';
    const endpoint = `/Platform/ModuleCatalog/api/pages/${encodeURIComponent(pageId)}/actions`;
    const offcanvasEl = document.getElementById('pageActionOffcanvas');
    const form = document.getElementById('pageActionForm');
    const offcanvasTitle = document.getElementById('pageActionOffcanvasTitle');
    const saveButton = document.getElementById('btnSavePageAction');
    const L = window.L10n || {};
    let dt;
    let offcanvas;
    let isTableInitialized = false;

    const fields = {
        id: document.getElementById('pageActionId'),
        actionCode: document.getElementById('pageActionCode'),
        displayName: document.getElementById('pageActionDisplayName'),
        permissionKey: document.getElementById('pageActionPermissionKey'),
        actionType: document.getElementById('pageActionType'),
        status: document.getElementById('pageActionStatus'),
        sortOrder: document.getElementById('pageActionSortOrder'),
        isDangerous: document.getElementById('pageActionIsDangerous'),
        isToolbarAction: document.getElementById('pageActionIsToolbarAction'),
        isRowAction: document.getElementById('pageActionIsRowAction'),
        description: document.getElementById('pageActionDescription')
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const normalizeActionCode = (value) => (value || '').toUpperCase().replace(/[^A-Z0-9]+/g, '_').replace(/_+/g, '_').replace(/^_|_$/g, '').slice(0, 80);
    const normalizePermission = (value) => (value || '').trim().toLowerCase().replace(/\s+/g, '').replace(/\.+/g, '.').replace(/^\.+|\.+$/g, '');
    const normalizePermissionSegment = (value) => (value || '')
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-|-$/g, '');

    const actionPermissionAliases = {
        ADD: 'create',
        CREATE: 'create',
        NEW: 'create',
        EDIT: 'edit',
        UPDATE: 'edit',
        VIEW: 'view',
        DETAILS: 'view',
        DETAIL: 'view',
        DELETE: 'delete',
        REMOVE: 'delete',
        ACTIVATE: 'activate',
        DEACTIVATE: 'deactivate',
        EXPORT: 'export',
        IMPORT: 'import',
        APPROVE: 'approve',
        REJECT: 'reject'
    };

    const statusBadge = (status) => {
        const cls = { Active: 'bg-label-success', Inactive: 'bg-label-warning', Deprecated: 'bg-label-danger', Draft: 'bg-label-secondary' }[status] || 'bg-label-secondary';
        return `<span class="badge ${cls}">${escapeHtml(L[`Status${status}`] || status || '-')}</span>`;
    };

    const actionTypeLabel = (actionType) => L[`ActionType${actionType}`] || actionType || '-';

    const permissionActionSegment = () => {
        const actionCode = normalizeActionCode(fields.actionCode?.value);
        if (!actionCode) return '';
        return actionPermissionAliases[actionCode] || normalizePermissionSegment(actionCode);
    };

    const generatePermissionKey = () => normalizePermission([
        normalizePermissionSegment(moduleCode),
        normalizePermissionSegment(pageCode),
        permissionActionSegment()
    ].filter(Boolean).join('.'));

    const syncPermissionKey = () => {
        if (!fields.permissionKey) return;
        fields.permissionKey.value = generatePermissionKey();
    };

    const initTooltips = () => {
        if (!window.bootstrap?.Tooltip) return;
        tableEl?.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((el) => {
            bootstrap.Tooltip.getOrCreateInstance(el);
        });
    };

    const hideSkeleton = () => {
        if (!skeletonEl) return;
        if (typeof jQuery !== 'undefined') {
            jQuery(skeletonEl).fadeOut(200);
            return;
        }
        skeletonEl.classList.add('d-none');
    };

    const unwrapRows = (json) => {
        const data = json?.data ?? json?.Data;
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.items)) return data.items;
        if (Array.isArray(data?.Items)) return data.Items;
        return [];
    };

    const reload = () => {
        if (dt) {
            dt.ajax.reload(() => hideSkeleton(), false);
        }
    };

    const showError = async (response) => {
        let message = L.ErrorOccurred || '';
        try {
            const payload = await response.json();
            message = payload?.errors?.join(' ') || payload?.message || message;
        } catch { }
        window.showToast?.(message, 'error');
    };

    const reset = () => {
        form?.reset();
        form?.classList.remove('was-validated');
        fields.id.value = '';
        fields.status.value = 'Draft';
        fields.actionType.value = 'Toolbar';
        fields.sortOrder.value = '0';
        fields.isToolbarAction.checked = true;
        fields.isRowAction.checked = false;
        fields.isDangerous.checked = false;
        syncPermissionKey();
        syncSelect2();
    };

    const openCreate = () => {
        reset();
        if (offcanvasTitle) offcanvasTitle.textContent = L.AddAction || '';
        offcanvas?.show();
    };

    const openEdit = (row) => {
        if (!row) return;
        fields.id.value = row.id || row.Id || '';
        fields.actionCode.value = row.actionCode || row.ActionCode || '';
        fields.displayName.value = row.displayName || row.DisplayName || '';
        fields.permissionKey.value = row.permissionKey || row.PermissionKey || '';
        fields.actionType.value = row.actionType || row.ActionType || 'Toolbar';
        fields.status.value = row.status || row.Status || 'Draft';
        fields.sortOrder.value = row.sortOrder ?? row.SortOrder ?? 0;
        fields.isDangerous.checked = (row.isDangerous ?? row.IsDangerous ?? false) === true;
        fields.isToolbarAction.checked = (row.isToolbarAction ?? row.IsToolbarAction ?? false) === true;
        fields.isRowAction.checked = (row.isRowAction ?? row.IsRowAction ?? false) === true;
        fields.description.value = row.description || row.Description || '';
        syncPermissionKey();
        if (offcanvasTitle) offcanvasTitle.textContent = L.EditAction || L.Edit || '';
        syncSelect2();
        offcanvas?.show();
    };

    const buildPayload = () => ({
        actionCode: normalizeActionCode(fields.actionCode.value),
        displayName: fields.displayName.value.trim(),
        permissionKey: normalizePermission(fields.permissionKey.value),
        actionType: fields.actionType.value,
        sortOrder: Number.parseInt(fields.sortOrder.value || '0', 10),
        isDangerous: fields.isDangerous.checked,
        isToolbarAction: fields.isToolbarAction.checked,
        isRowAction: fields.isRowAction.checked,
        status: fields.status.value,
        description: fields.description.value.trim() || null
    });

    const save = async (event) => {
        event.preventDefault();
        if (!form) return;
        fields.actionCode.value = normalizeActionCode(fields.actionCode.value);
        syncPermissionKey();
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }
        const id = fields.id.value;
        if (saveButton) {
            saveButton.disabled = true;
            saveButton.dataset.originalText = saveButton.innerHTML;
            saveButton.innerHTML = `<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>${L.Save || ''}`;
        }
        try {
            const response = await fetch(id ? `/Platform/ModuleCatalog/api/page-actions/${encodeURIComponent(id)}` : endpoint, {
                method: id ? 'PUT' : 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(buildPayload())
            });
            if (!response.ok) {
                await showError(response);
                return;
            }
            offcanvas?.hide();
            reload();
            window.showToast?.(L.ActionSaved || L.RecordSaved || '', 'success');
        } catch (error) {
            console.error('[ModulePageDetails] Save failed.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
        } finally {
            if (saveButton) {
                saveButton.disabled = false;
                saveButton.innerHTML = saveButton.dataset.originalText || (L.Save || '');
            }
        }
    };

    const remove = (row) => {
        const id = row.id || row.Id;
        const entityName = row.displayName || row.DisplayName || row.actionCode || row.ActionCode || '';
        window.showConfirm?.(L.AreYouSure, async () => {
            try {
                const response = await fetch(`/Platform/ModuleCatalog/api/page-actions/${encodeURIComponent(id)}`, { method: 'DELETE' });
                if (!response.ok) {
                    await showError(response);
                    return;
                }
                reload();
                window.showToast?.(L.RecordDeleted || L.Deleted || '', 'success');
            } catch (error) {
                console.error('[ModulePageDetails] Delete failed.', error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName, type: 'delete', confirmButtonText: L.Delete });
    };

    const syncSelect2 = () => {
        if (!window.jQuery?.fn?.select2) return;
        [fields.actionType, fields.status].forEach((field) => {
            if (field) $(field).trigger('change.select2');
        });
    };

    const initSelect2 = () => {
        if (!window.jQuery?.fn?.select2 || !form) return;
        $(form).find('select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select',
                minimumResultsForSearch: Infinity,
                width: 'element',
                placeholder: $select.data('placeholder') || ''
            });
        });
    };

    const mountToolbarAddButton = () => {
        // Read-only (code-owned) modules don't get an "Add Action" affordance.
        if (isReadonly) return;
        const wrapper = tableEl?.closest('.dt-container') || tableEl?.closest('.card') || document;
        const toolbarEnd = wrapper.querySelector('.dt-layout-row:first-child .dt-layout-end') || wrapper.querySelector('.dt-layout-end');
        if (!toolbarEnd || toolbarEnd.querySelector('.module-page-action-add-btn')) return;

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-primary add-new module-page-action-add-btn';
        button.innerHTML = `<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">${L.AddAction || ''}</span>`;
        button.addEventListener('click', (event) => {
            event.preventDefault();
            openCreate();
        });

        toolbarEnd.appendChild(button);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const initDataTable = () => {
        if (isTableInitialized || !tableEl) return;
        if (typeof DataTable === 'undefined') {
            hideSkeleton();
            window.showToast?.(L.ErrorOccurred || '', 'error');
            return;
        }
        isTableInitialized = true;

        const config = {
            // Non-async on purpose: an async ajax fn returns a Promise, which DataTables mistakes for a
            // jqXHR and calls .abort() on during ajax.reload() → "xhr.abort is not a function". Returning
            // undefined (promise-chain form) lets reload run clean.
            ajax: (data, callback) => {
                fetch(endpoint)
                    .then((response) => {
                        if (!response.ok) {
                            return Promise.resolve(showError(response)).then(() => {
                                hideSkeleton();
                                callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                            });
                        }

                        return response.json().then((payload) => {
                            const rows = unwrapRows(payload);
                            callback({ data: rows, recordsTotal: rows.length, recordsFiltered: rows.length });
                        });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        hideSkeleton();
                        callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                    });
            },
            stateSave: false,
            paging: true,
            searching: true,
            ordering: true,
            order: [[5, 'asc']],
            colReorder: { columns: ':not(:last-child)' },
            layout: {
                topStart: {
                    rowClass: 'row my-0 justify-content-between',
                    features: [
                        {
                            pageLength: {
                                menu: [10, 25, 50, 100],
                                text: '_MENU_'
                            }
                        }
                    ]
                },
                topEnd: {
                    features: [
                        {
                            search: {
                                placeholder: L.ModuleActionsSearchPlaceholder || '',
                                text: '_INPUT_'
                            }
                        }
                    ]
                },
                bottomStart: {
                    rowClass: 'row justify-content-between',
                    features: ['info']
                },
                bottomEnd: {
                    features: [
                        {
                            paging: {
                                firstLast: false
                            }
                        }
                    ]
                }
            },
            language: {
                search: '',
                searchPlaceholder: L.ModuleActionsSearchPlaceholder || '',
                info: L.ModuleActionsDtInfo || '',
                infoEmpty: L.ModuleActionsDtInfoEmpty || '',
                infoFiltered: L.ModuleActionsDtInfoFiltered || ''
            },
            columns: [
                { data: 'actionCode', render: (data, type, row) => `<code>${escapeHtml(data || row.ActionCode || '-')}</code>` },
                { data: 'displayName', className: 'all', render: (data, type, row) => escapeHtml(data || row.DisplayName || '-') },
                { data: 'permissionKey', render: (data, type, row) => `<code>${escapeHtml(data || row.PermissionKey || '-')}</code>` },
                { data: 'actionType', render: (data, type, row) => escapeHtml(actionTypeLabel(data || row.ActionType)) },
                { data: 'status', render: (data, type, row) => statusBadge(data || row.Status) },
                { data: 'sortOrder', className: 'cell-fit text-end', render: (data, type, row) => escapeHtml(data ?? row.SortOrder ?? 0) },
                {
                    data: null,
                    orderable: false,
                    searchable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);
                        // Code-owned (self-registered) modules: the actions table is view-only. The backend
                        // rejects action mutations with 409 MODULE_MANAGED_BY_CODE, so we render a lock
                        // indicator instead of edit/delete controls.
                        if (isReadonly) {
                            return `<span class="text-muted small d-inline-flex align-items-center" title="${escapeHtml(L.SelfRegisteredReadOnlyHint || '')}"><i class="bx bx-lock-alt"></i></span>`;
                        }
                        return window.DitenDataTable?.renderActions?.([
                            {
                                key: 'edit',
                                text: L.Edit || '',
                                attrs: { 'data-id': id, 'data-json': rowJson, 'aria-label': L.Edit || '' }
                            },
                            {
                                key: 'delete',
                                className: 'text-danger',
                                text: L.Delete || '',
                                attrs: { 'data-id': id, 'data-json': rowJson, 'aria-label': L.Delete || '' }
                            }
                        ]) || '';
                    }
                }
            ],
            initComplete: function () {
                hideSkeleton();
                mountToolbarAddButton();
                initTooltips();
            },
            drawCallback: function () {
                hideSkeleton();
                mountToolbarAddButton();
                initTooltips();
            }
        };

        try {
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
        } catch (error) {
            console.error('[ModulePageDetails] DataTable init failed.', error);
            hideSkeleton();
            window.showToast?.(L.ErrorOccurred || '', 'error');
            return;
        }

        // Read-only (code-owned) modules: no create/edit/delete wiring.
        if (!isReadonly) {
            window.DitenDataTable?.bindActionDispatcher?.({
                tableEl,
                dt,
                onRowAction: {
                    edit: ({ row }) => openEdit(row),
                    delete: ({ row }) => {
                        if (row) remove(row);
                    }
                }
            });
        }
    };

    const bindFormEvents = () => {
        // Read-only (code-owned) modules: no form wiring (Add button is also omitted).
        if (isReadonly) return;
        fields.displayName?.addEventListener('input', () => {
            if (!fields.actionCode.value) {
                fields.actionCode.value = normalizeActionCode(fields.displayName.value);
                syncPermissionKey();
            }
        });
        fields.actionCode?.addEventListener('input', () => {
            fields.actionCode.value = normalizeActionCode(fields.actionCode.value);
            syncPermissionKey();
        });
        form?.addEventListener('submit', save);
    };

    const init = () => {
        if (!tableEl || !offcanvasEl) return;
        offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl);
        initSelect2();
        bindFormEvents();
        document.querySelector('[data-bs-target="#page-actions-tab"]')?.addEventListener('shown.bs.tab', initDataTable);
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ModulePageDetails.init());
