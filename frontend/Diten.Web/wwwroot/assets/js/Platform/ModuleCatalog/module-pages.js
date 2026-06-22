/**
 * Module Catalog Details / Pages tab.
 */
'use strict';

const ModuleCatalogPages = (function () {
    const tableEl = document.getElementById('dt-module-pages');
    const skeletonEl = document.getElementById('module-pages-skeleton-loader');
    const moduleCode = tableEl?.dataset.moduleCode || '';
    const endpoint = `/Platform/ModuleCatalog/api/${encodeURIComponent(moduleCode)}/pages`;
    const offcanvasEl = document.getElementById('modulePageOffcanvas');
    const form = document.getElementById('modulePageForm');
    const offcanvasTitle = document.getElementById('modulePageOffcanvasTitle');
    const fields = {
        id: document.getElementById('modulePageId'),
        moduleCode: document.getElementById('modulePageModuleCode'),
        pageCode: document.getElementById('modulePagePageCode'),
        displayName: document.getElementById('modulePageDisplayName'),
        routePath: document.getElementById('modulePageRoutePath'),
        requiredPermission: document.getElementById('modulePageRequiredPermission'),
        parentPageCode: document.getElementById('modulePageParentPageCode'),
        pageType: document.getElementById('modulePagePageType'),
        status: document.getElementById('modulePageStatus'),
        sortOrder: document.getElementById('modulePageSortOrder'),
        isNavigationVisible: document.getElementById('modulePageIsNavigationVisible'),
        description: document.getElementById('modulePageDescription')
    };
    const preview = document.querySelector('#modulePageCodePreview code');
    const L = window.L10n || {};
    const saveButton = document.getElementById('btnSaveModulePage');
    let dt;
    let offcanvas;
    let isTableInitialized = false;
    let hasAttemptedSubmit = false;
    let requiredPermissionManuallyEdited = false;

    const normalizePageCode = (value) => (value || '')
        .toUpperCase()
        .replace(/[\s-]+/g, '_')
        .replace(/[^A-Z0-9_]/g, '_')
        .replace(/_+/g, '_')
        .replace(/^_|_$/g, '');

    const normalizeRoutePath = (value) => {
        const route = (value || '').trim().replace(/\/{2,}/g, '/');
        if (!route) return '';
        return route.length > 1 ? route.replace(/\/+$/g, '') : route;
    };

    const normalizePermission = (value) => (value || '')
        .trim()
        .toLowerCase()
        .replace(/\s+/g, '')
        .replace(/\.+/g, '.')
        .replace(/^\.+|\.+$/g, '');

    const normalizePermissionSegment = (value) => (value || '')
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-|-$/g, '');

    const actionByPageType = {
        List: 'view',
        Details: 'view',
        Create: 'create',
        Edit: 'edit',
        Report: 'view',
        Dashboard: 'view',
        Wizard: 'execute',
        Custom: 'view'
    };

    const isValidRoutePath = (rawValue, routePath) => {
        const raw = (rawValue || '').trim();
        return raw.startsWith('/')
            && !/\s/.test(raw)
            && /^\/[A-Z][A-Za-z0-9-]*(?:\/(?:[A-Z][A-Za-z0-9-]*|\{[A-Za-z0-9_]+\}|:[A-Za-z0-9_]+))*$/.test(routePath);
    };

    const isValidPageCode = (pageCode) => pageCode.length >= 3
        && pageCode.length <= 100
        && /[A-Z]/.test(pageCode)
        && /^[A-Z0-9]+(?:_[A-Z0-9]+)*$/.test(pageCode);

    const isValidPermission = (permission) => {
        if (!permission) return true;
        return permission.length >= 3
            && permission.length <= 200
            && /^[a-z][a-z0-9-]*\.[a-z][a-z0-9-]*\.[a-z][a-z0-9-]*$/.test(permission);
    };

    const setGeneratedFieldState = (field, isValid) => {
        if (!field) return;
        field.classList.toggle('is-invalid', hasAttemptedSubmit && !isValid);
    };

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const initTooltips = () => {
        if (!window.bootstrap?.Tooltip) return;
        tableEl?.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((el) => {
            bootstrap.Tooltip.getOrCreateInstance(el);
        });
    };

    const statusBadge = (status) => {
        const cls = {
            Active: 'bg-label-success',
            Inactive: 'bg-label-warning',
            Deprecated: 'bg-label-danger',
            Draft: 'bg-label-secondary'
        }[status] || 'bg-label-secondary';
        const label = L[`Status${status}`] || status || '-';
        return `<span class="badge ${cls}">${label}</span>`;
    };

    const pageTypeLabel = (pageType) => L[`PageType${pageType}`] || pageType || '-';

    const pageTypeBadge = (pageType) => {
        const cls = {
            List: 'bg-label-primary',
            Details: 'bg-label-info',
            Create: 'bg-label-success',
            Edit: 'bg-label-warning',
            Report: 'bg-label-secondary',
            Wizard: 'bg-label-dark',
            Dashboard: 'bg-label-info',
            Custom: 'bg-label-secondary'
        }[pageType] || 'bg-label-secondary';

        return `<span class="badge ${cls}">${escapeHtml(pageTypeLabel(pageType))}</span>`;
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
        const data = json?.data;
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

    const resetForm = () => {
        form?.reset();
        fields.id.value = '';
        fields.moduleCode.value = moduleCode;
        fields.status.value = 'Draft';
        fields.pageType.value = 'List';
        fields.sortOrder.value = '0';
        fields.parentPageCode.value = '';
        fields.isNavigationVisible.checked = true;
        requiredPermissionManuallyEdited = false;
        hasAttemptedSubmit = false;
        Object.values(fields).forEach((field) => field?.setCustomValidity?.(''));
        form?.classList.remove('was-validated');
        syncGeneratedFields();
    };

    const updatePreview = () => {
        const normalized = normalizePageCode(fields.displayName?.value);
        if (fields.pageCode && fields.pageCode.value !== normalized) {
            fields.pageCode.value = normalized;
        }
        setGeneratedFieldState(fields.pageCode, isValidPageCode(normalized));
        if (preview) {
            preview.textContent = normalized || '-';
        }
    };

    const getRouteSegments = () => {
        const routePath = normalizeRoutePath(fields.routePath?.value);
        const parts = routePath.split('/').filter(Boolean);
        return {
            domain: parts[0] || '',
            module: parts[1] || ''
        };
    };

    const generatePermission = () => {
        // First segment is ALWAYS moduleCode — the entitlement bridge maps Permission.Module to it.
        // Resource segment comes from the route's 2nd segment, else pageCode, else displayName.
        const moduleSeg = normalizePermissionSegment(moduleCode);
        const resourceSeg = normalizePermissionSegment(getRouteSegments().module)
            || normalizePermissionSegment(fields.pageCode?.value)
            || normalizePermissionSegment(fields.displayName?.value);
        const action = actionByPageType[fields.pageType?.value] || 'view';
        return normalizePermission([moduleSeg, resourceSeg, action].filter(Boolean).join('.'));
    };

    const updatePermission = () => {
        if (!fields.requiredPermission || requiredPermissionManuallyEdited) return;
        const permission = generatePermission();
        fields.requiredPermission.value = permission;
        setGeneratedFieldState(fields.requiredPermission, isValidPermission(permission));
    };

    const syncGeneratedFields = () => {
        updatePreview();
        updatePermission();
    };

    const buildPayload = () => ({
        moduleCode,
        pageCode: normalizePageCode(fields.pageCode.value),
        displayName: fields.displayName.value.trim(),
        routePath: normalizeRoutePath(fields.routePath.value),
        requiredPermission: normalizePermission(fields.requiredPermission.value) || null,
        parentPageCode: normalizePageCode(fields.parentPageCode.value) || null,
        pageType: fields.pageType.value,
        status: fields.status.value,
        sortOrder: Number.parseInt(fields.sortOrder.value || '0', 10),
        isNavigationVisible: fields.isNavigationVisible.checked,
        description: fields.description.value.trim() || null
    });

    const showError = async (response) => {
        let message = L.ErrorOccurred || '';
        try {
            const payload = await response.json();
            message = payload?.errors?.join(' ') || payload?.message || message;
        } catch { }
        window.showToast?.(message, 'error');
    };

    const openCreate = () => {
        resetForm();
        offcanvasTitle.textContent = L.AddPage || '';
        offcanvas?.show();
    };

    const openEdit = async (id) => {
        const response = await fetch(`/Platform/ModuleCatalog/api/pages/${encodeURIComponent(id)}`);
        if (!response.ok) {
            await showError(response);
            return;
        }

        const payload = await response.json();
        const data = payload?.data || {};
        fields.id.value = data.id || data.Id || '';
        fields.pageCode.value = data.pageCode || data.PageCode || '';
        fields.displayName.value = data.displayName || data.DisplayName || '';
        fields.routePath.value = normalizeRoutePath(data.routePath || data.RoutePath || '');
        fields.pageType.value = data.pageType || data.PageType || 'List';
        fields.status.value = data.status || data.Status || 'Draft';
        fields.sortOrder.value = data.sortOrder ?? data.SortOrder ?? 0;
        fields.parentPageCode.value = data.parentPageCode || data.ParentPageCode || '';
        fields.isNavigationVisible.checked = (data.isNavigationVisible ?? data.IsNavigationVisible ?? true) === true;
        fields.requiredPermission.value = data.requiredPermission || data.RequiredPermission || '';
        requiredPermissionManuallyEdited = Boolean(fields.requiredPermission.value);
        fields.description.value = data.description || data.Description || '';
        syncGeneratedFields();
        offcanvasTitle.textContent = L.EditPage || '';
        offcanvas?.show();
    };

    const save = async (event) => {
        event.preventDefault();
        if (!form) return;

        syncGeneratedFields();
        const rawRoutePath = fields.routePath.value;
        const routePath = normalizeRoutePath(fields.routePath.value);
        fields.routePath.value = routePath;
        fields.routePath.setCustomValidity(isValidRoutePath(rawRoutePath, routePath) ? '' : (L.InvalidRouteFormat || L.InvalidRoutePath || ''));

        const generatedFieldsValid = isValidPageCode(fields.pageCode.value) && isValidPermission(fields.requiredPermission.value);
        if (!form.checkValidity() || !generatedFieldsValid) {
            hasAttemptedSubmit = true;
            syncGeneratedFields();
            form.classList.add('was-validated');
            return;
        }

        if (saveButton) {
            saveButton.disabled = true;
            saveButton.dataset.originalText = saveButton.innerHTML;
            saveButton.innerHTML = `<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>${L.Save || ''}`;
        }

        try {
            const id = fields.id.value;
            const response = await fetch(id ? `/Platform/ModuleCatalog/api/pages/${encodeURIComponent(id)}` : endpoint, {
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
            window.showToast?.(L.PageSaved || '', 'success');
        } catch (error) {
            console.error('[ModuleCatalogPages] Save failed.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
        } finally {
            if (saveButton) {
                saveButton.disabled = false;
                saveButton.innerHTML = saveButton.dataset.originalText || (L.Save || '');
            }
        }
    };

    const updateRowStatus = (button, active) => {
        const rowApi = dt?.row?.(jQuery(button).closest('tr'));
        const row = rowApi?.data?.();
        if (!row) return;

        row.status = active ? 'Active' : 'Inactive';
        row.Status = row.status;
        rowApi.data(row).invalidate().draw(false);
        initTooltips();
        hideSkeleton();
    };

    const setActive = async (button, active) => {
        const id = button?.dataset?.id;
        if (!id) return;

        button.disabled = true;
        try {
            const response = await fetch(`/Platform/ModuleCatalog/api/pages/${encodeURIComponent(id)}/${active ? 'activate' : 'deactivate'}`, {
                method: 'POST'
            });
            if (!response.ok) {
                await showError(response);
                return;
            }
            updateRowStatus(button, active);
        } catch (error) {
            console.error('[ModuleCatalogPages] Status update failed.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
        } finally {
            button.disabled = false;
            hideSkeleton();
        }
    };

    const remove = (row) => {
        const entityName = row.pageCode || row.PageCode;
        window.showConfirm?.(L.AreYouSure, async () => {
            try {
                const response = await fetch(`/Platform/ModuleCatalog/api/pages/${encodeURIComponent(row.id || row.Id)}`, {
                    method: 'DELETE'
                });
                if (!response.ok) {
                    await showError(response);
                    return;
                }
                reload();
                window.showToast?.(L.RecordDeleted || L.Deleted || '', 'success');
            } catch (error) {
                console.error('[ModuleCatalogPages] Delete failed.', error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName, type: 'delete', confirmButtonText: L.Delete });
    };

    const mountToolbarAddButton = () => {
        const wrapper = tableEl?.closest('.dt-container') || tableEl?.closest('.card') || document;
        const toolbarEnd = wrapper.querySelector('.dt-layout-row:first-child .dt-layout-end') || wrapper.querySelector('.dt-layout-end');
        if (!toolbarEnd || toolbarEnd.querySelector('.module-page-add-btn')) return;

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-primary add-new module-page-add-btn';
        button.innerHTML = `<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">${L.AddPage || ''}</span>`;
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
            // undefined (promise-chain form) lets reload run clean. Behavior is otherwise identical.
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
                                placeholder: L.ModulePagesSearchPlaceholder || '',
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
                searchPlaceholder: L.ModulePagesSearchPlaceholder || '',
                info: L.ModulePagesDtInfo || '',
                infoEmpty: L.ModulePagesDtInfoEmpty || '',
                infoFiltered: L.ModulePagesDtInfoFiltered || ''
            },
            columns: [
                { data: 'pageCode', render: (data) => `<code>${escapeHtml(data || '-')}</code>` },
                { data: 'displayName', className: 'all', render: (data) => escapeHtml(data || '-') },
                { data: 'routePath', render: (data) => `<code>${escapeHtml(data || '-')}</code>` },
                { data: 'pageType', render: pageTypeBadge },
                { data: 'status', render: statusBadge },
                { data: 'sortOrder', className: 'text-end', defaultContent: '0' },
                { data: 'isNavigationVisible', className: 'text-center', render: (data) => data ? '<i class="bx bx-check text-success fs-4"></i>' : '<i class="bx bx-x text-danger fs-4"></i>' },
                {
                    data: null,
                    orderable: false,
                    searchable: false,
                    className: 'cell-fit all text-end',
                    render: (data, type, row) => {
                        const id = row.id || row.Id;
                        const status = row.status || row.Status;
                        const rowJson = JSON.stringify(row);
                        const actions = [
                            {
                                key: 'view',
                                className: 'js-quick-view',
                                text: L.Details || '',
                                icon: 'bx bx-show',
                                attrs: {
                                    'data-id': id,
                                    'data-json': rowJson,
                                    'aria-label': L.Details || ''
                                }
                            },
                            {
                                key: 'edit',
                                text: L.Edit || '',
                                attrs: { 'data-id': id, 'data-json': rowJson, 'aria-label': L.Edit || '' }
                            }
                        ];

                        if (status === 'Active') {
                            actions.push({
                                key: 'deactivate',
                                className: 'text-warning',
                                text: L.Deactivate || '',
                                attrs: { 'data-id': id, 'data-json': rowJson, 'aria-label': L.Deactivate || '' }
                            });
                        } else {
                            actions.push({
                                key: 'activate',
                                className: 'text-success',
                                text: L.Activate || '',
                                attrs: { 'data-id': id, 'data-json': rowJson, 'aria-label': L.Activate || '' }
                            });
                        }

                        actions.push({
                            key: 'delete',
                            className: 'text-danger',
                            text: L.Delete || '',
                            attrs: { 'data-id': id, 'data-json': rowJson, 'aria-label': L.Delete || '' }
                        });

                        return window.DitenDataTable?.renderActions?.(actions) || '';
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
            console.error('[ModuleCatalogPages] DataTable init failed.', error);
            hideSkeleton();
            window.showToast?.(L.ErrorOccurred || '', 'error');
            return;
        }

        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl,
            dt,
            onRowAction: {
                view: ({ id }) => {
                    if (id) window.location.href = `/Platform/ModuleCatalog/PageDetails/${encodeURIComponent(id)}`;
                },
                edit: ({ id }) => openEdit(id),
                activate: ({ trigger }) => setActive(trigger, true),
                deactivate: ({ trigger }) => setActive(trigger, false),
                delete: ({ row }) => {
                    if (row) remove(row);
                }
            }
        });
    };

    const bindEvents = () => {
        document.querySelector('[data-bs-target="#module-pages-tab"]')?.addEventListener('shown.bs.tab', initDataTable);
        fields.displayName?.addEventListener('input', syncGeneratedFields);
        fields.routePath?.addEventListener('input', () => {
            fields.routePath.setCustomValidity('');
            updatePermission();
        });
        fields.routePath?.addEventListener('blur', () => {
            fields.routePath.value = normalizeRoutePath(fields.routePath.value);
            updatePermission();
        });
        fields.pageType?.addEventListener('change', updatePermission);
        fields.requiredPermission?.addEventListener('input', () => {
            requiredPermissionManuallyEdited = true;
            fields.requiredPermission.setCustomValidity('');
        });
        fields.requiredPermission?.addEventListener('blur', () => {
            fields.requiredPermission.value = normalizePermission(fields.requiredPermission.value);
            setGeneratedFieldState(fields.requiredPermission, isValidPermission(fields.requiredPermission.value));
        });
        form?.addEventListener('submit', save);
    };

    const init = () => {
        if (!tableEl || !offcanvasEl) return;
        offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl);
        bindEvents();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ModuleCatalogPages.init());
