'use strict';

(function () {
    const endpoint = '/Platform/AuditRetention/api';
    let L = window.L10n || {};
    let dt = null;
    let loadedPolicies = [];
    let loadedPolicy = null;
    let loadedPolicyId = '';
    let hasLoadedPolicies = false;
    const categoryLabels = new Map();

    const tableEl = document.getElementById('dt-audit-retention');
    const form = document.getElementById('auditRetentionForm');
    const saveButton = document.getElementById('auditRetentionSave');
    const saving = document.getElementById('auditRetentionSaving');
    const errorEl = document.getElementById('audit-retention-error');
    const permissionEl = document.getElementById('audit-retention-permission');
    const editorEl = document.getElementById('auditRetentionEditor');
    const editor = editorEl && window.bootstrap ? new bootstrap.Offcanvas(editorEl) : null;

    const fields = {
        policyId: document.getElementById('retentionPolicyId'),
        category: document.getElementById('retentionCategory'),
        categoryDisplay: document.getElementById('retentionCategoryDisplay'),
        planTierCode: document.getElementById('retentionPlanTier'),
        status: document.getElementById('retentionStatus'),
        minimumRetentionDays: document.getElementById('minimumRetentionDays'),
        defaultRetentionDays: document.getElementById('defaultRetentionDays'),
        maximumRetentionDays: document.getElementById('maximumRetentionDays'),
        hotStorageDays: document.getElementById('hotStorageDays'),
        allowTenantOverride: document.getElementById('allowTenantOverride')
    };

    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    function syncL10n() {
        L = window.L10n || {};
    }

    function showError(message) {
        if (!errorEl) return;
        errorEl.textContent = message || L.AuditRetentionSaveError || '';
        errorEl.classList.remove('d-none');
    }

    function clearError() {
        errorEl?.classList.add('d-none');
        if (errorEl) errorEl.textContent = '';
    }

    function setPermissionVisible(visible) {
        permissionEl?.classList.toggle('d-none', !visible);
    }

    function setPermissionMessage(status) {
        if (!permissionEl) return;
        permissionEl.textContent = status === 401
            ? L.AuditRetentionLoginRequired || ''
            : L.AuditRetentionForbidden || L.AuditRetentionUnauthorized || '';
        setPermissionVisible(true);
    }

    function setSaving(isSaving) {
        saveButton?.toggleAttribute('disabled', isSaving || !loadedPolicy);
        saving?.classList.toggle('d-none', !isSaving);
    }

    function getValue(item, key) {
        return item?.[key] ?? item?.[key.charAt(0).toUpperCase() + key.slice(1)];
    }

    function getPolicyId(policy) {
        return String(getValue(policy, 'policyId') || getValue(policy, 'id') || '');
    }

    function toInt(input) {
        const value = Number.parseInt(input?.value || '', 10);
        return Number.isInteger(value) ? value : 0;
    }

    function normalizePayload(payload) {
        return payload?.data || payload?.Data || payload || [];
    }

    function normalizePolicies(payload) {
        const data = normalizePayload(payload);
        return Array.isArray(data) ? data : [];
    }

    function categoryLabel(category) {
        return categoryLabels.get(String(category || '')) || L.CommonUnknown || '';
    }

    function normalizeLookupRows(payload) {
        const data = normalizePayload(payload);
        return Array.isArray(data) ? data : [];
    }

    async function loadCategoryLabels() {
        try {
            const response = await fetch(`${endpoint}/lookups/categories`, {
                method: 'GET',
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });

            if (!response.ok) return;
            categoryLabels.clear();
            normalizeLookupRows(await response.json()).forEach((item) => {
                const code = item?.code ?? item?.Code ?? item?.value ?? item?.Value ?? '';
                const name = item?.name ?? item?.Name ?? item?.text ?? item?.Text ?? code;
                if (code) categoryLabels.set(String(code), String(name || code));
            });
        } catch (error) {
            console.error('[AuditRetention] Category lookup load failed.', error);
        }
    }

    function statusText(policy) {
        return getValue(policy, 'isActive') ? L.AuditRetentionActive || '' : L.AuditRetentionInactive || '';
    }

    function statusBadge(data, type, row) {
        const active = Boolean(getValue(row, 'isActive'));
        const text = active ? L.AuditRetentionActive || '' : L.AuditRetentionInactive || '';
        if (type !== 'display') return text;
        return `<span class="badge ${active ? 'bg-label-success' : 'bg-label-secondary'}">${escapeHtml(text)}</span>`;
    }

    function booleanBadge(data, type) {
        const enabled = Boolean(data);
        const text = enabled ? L.AuditRetentionYes || '' : L.AuditRetentionNo || '';
        if (type !== 'display') return text;
        return `<span class="badge ${enabled ? 'bg-label-primary' : 'bg-label-secondary'}">${escapeHtml(text)}</span>`;
    }

    function renderDays(data, type) {
        const value = Number.parseInt(data ?? '', 10);
        if (!Number.isInteger(value)) return type === 'display' ? '-' : 0;
        if (type !== 'display') return value;
        return `<span class="fw-medium text-heading">${value.toLocaleString()}</span> <span class="text-muted">${escapeHtml(L.AuditRetentionDaysShort || '')}</span>`;
    }

    function renderActions(row) {
        const id = getPolicyId(row);
        if (!id) return '';
        const actions = [
            {
                key: 'edit',
                className: 'js-edit-policy',
                text: L.AuditRetentionEdit || L.Edit || '',
                icon: 'bx bx-edit',
                attrs: { 'data-id': id }
            }
        ];

        return window.DitenDataTable
            ? window.DitenDataTable.renderActions(actions)
            : `<button type="button" class="btn btn-sm btn-icon js-edit-policy" data-id="${escapeHtml(id)}" title="${escapeHtml(L.AuditRetentionEdit || L.Edit || '')}"><i class="bx bx-edit"></i></button>`;
    }

    function setFieldError(name, message) {
        const field = fields[name];
        const feedback = document.querySelector(`[data-validation-for="${name}"]`);
        field?.classList.add('is-invalid');
        if (feedback) feedback.textContent = message || '';
    }

    function clearFieldErrors() {
        Object.values(fields).forEach((field) => field?.classList?.remove('is-invalid'));
        document.querySelectorAll('[data-validation-for]').forEach((el) => { el.textContent = ''; });
    }

    function normalizePolicyKey(policy) {
        return {
            id: getPolicyId(policy),
            category: String(getValue(policy, 'category') || ''),
            planTierCode: String(getValue(policy, 'planTierCode') || '')
        };
    }

    function findPolicyById(id) {
        const expected = String(id || '');
        return loadedPolicies.find((policy) => getPolicyId(policy) === expected) || null;
    }

    function populatePolicy(policy) {
        if (!policy) return;
        const key = normalizePolicyKey(policy);
        loadedPolicy = policy;
        loadedPolicyId = key.id;

        fields.policyId.value = key.id;
        fields.category.value = key.category;
        fields.categoryDisplay.value = categoryLabel(key.category);
        fields.planTierCode.value = key.planTierCode;
        fields.status.value = statusText(policy);
        fields.minimumRetentionDays.value = getValue(policy, 'minimumRetentionDays') || '';
        fields.defaultRetentionDays.value = getValue(policy, 'defaultRetentionDays') || '';
        fields.maximumRetentionDays.value = getValue(policy, 'maximumRetentionDays') || '';
        fields.hotStorageDays.value = getValue(policy, 'hotStorageDays') || '';
        fields.allowTenantOverride.checked = Boolean(getValue(policy, 'allowTenantOverride'));
        setSaving(false);
    }

    function collectPayload() {
        return {
            policyId: loadedPolicyId,
            category: fields.category?.value || '',
            planTierCode: (fields.planTierCode?.value || '').trim(),
            minimumRetentionDays: toInt(fields.minimumRetentionDays),
            defaultRetentionDays: toInt(fields.defaultRetentionDays),
            maximumRetentionDays: toInt(fields.maximumRetentionDays),
            hotStorageDays: toInt(fields.hotStorageDays),
            allowTenantOverride: Boolean(fields.allowTenantOverride?.checked)
        };
    }

    function validate() {
        clearFieldErrors();
        clearError();
        let valid = true;
        const payload = collectPayload();

        if (!hasLoadedPolicies) {
            showError(L.AuditRetentionLoadRequired || '');
            return null;
        }

        if (!loadedPolicy || !loadedPolicyId) {
            showError(L.AuditRetentionExistingPolicyRequired || '');
            return null;
        }

        const currentPolicy = findPolicyById(loadedPolicyId);
        if (!currentPolicy) {
            loadedPolicy = null;
            loadedPolicyId = '';
            setSaving(false);
            showError(L.AuditRetentionLoadedPolicyMismatch || '');
            return null;
        }

        if (!payload.category) {
            setFieldError('category', L.AuditRetentionRequired || '');
            valid = false;
        }

        if (!payload.planTierCode) {
            setFieldError('planTierCode', L.AuditRetentionRequired || '');
            valid = false;
        }

        ['minimumRetentionDays', 'defaultRetentionDays', 'maximumRetentionDays', 'hotStorageDays'].forEach((name) => {
            if (payload[name] <= 0) {
                setFieldError(name, L.AuditRetentionZeroError || '');
                valid = false;
            }
        });

        if (payload.maximumRetentionDays > 0 && payload.minimumRetentionDays > 0 && payload.maximumRetentionDays < payload.minimumRetentionDays) {
            setFieldError('maximumRetentionDays', L.AuditRetentionCeilingError || '');
            valid = false;
        }

        if (payload.defaultRetentionDays > 0
            && payload.minimumRetentionDays > 0
            && payload.defaultRetentionDays < payload.minimumRetentionDays) {
            setFieldError('defaultRetentionDays', L.AuditRetentionFloorError || '');
            valid = false;
        }

        if (payload.defaultRetentionDays > 0
            && payload.maximumRetentionDays > 0
            && payload.defaultRetentionDays > payload.maximumRetentionDays) {
            setFieldError('defaultRetentionDays', L.AuditRetentionCeilingError || '');
            valid = false;
        }

        if (payload.hotStorageDays > 0
            && payload.defaultRetentionDays > 0
            && payload.hotStorageDays > payload.defaultRetentionDays) {
            setFieldError('hotStorageDays', L.AuditRetentionHotStorageError || '');
            valid = false;
        }

        return valid ? payload : null;
    }

    async function readError(response) {
        try {
            const payload = await response.json();
            const errors = payload.errors || payload.Errors || [];
            if (errors.length) return errors.join(' ');
            return payload.detail || payload.Detail || L.AuditRetentionSaveError || '';
        } catch {
            return L.AuditRetentionSaveError || '';
        }
    }

    function showModal(icon, message) {
        if (window.Swal) {
            return window.Swal.fire({
                icon,
                title: message,
                confirmButtonText: L.CommonOk || '',
                customClass: { confirmButton: 'btn btn-primary' },
                buttonsStyling: false
            });
        }

        window.showToast?.(message, icon === 'success' ? 'success' : 'error');
        return Promise.resolve();
    }

    async function loadPolicies() {
        setSaving(true);
        setPermissionVisible(false);
        clearError();

        try {
            const response = await fetch(endpoint, {
                method: 'GET',
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });

            if (response.status === 401 || response.status === 403) {
                setPermissionMessage(response.status);
                return [];
            }

            if (!response.ok) {
                showError(await readError(response));
                return [];
            }

            loadedPolicies = normalizePolicies(await response.json());
            hasLoadedPolicies = true;
            if (!loadedPolicies.length) showError(L.AuditRetentionNoPolicies || '');
            return loadedPolicies;
        } catch (error) {
            console.error('[AuditRetention] Load failed.', error);
            showError(L.AuditRetentionLoadError || '');
            return [];
        } finally {
            setSaving(false);
        }
    }

    function openEditor(policy) {
        if (!policy || !editor) return;
        clearFieldErrors();
        clearError();
        setPermissionVisible(false);
        populatePolicy(policy);
        editor.show();
    }

    async function saveRetention(event) {
        event.preventDefault();
        setPermissionVisible(false);
        const payload = validate();
        if (!payload) return;

        setSaving(true);
        try {
            const response = await fetch(endpoint, {
                method: 'PUT',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(payload)
            });

            if (response.status === 401 || response.status === 403) {
                setPermissionMessage(response.status);
                return;
            }

            if (!response.ok) {
                showError(await readError(response));
                await showModal('error', L.AuditRetentionSaveError || '');
                return;
            }

            clearError();
            await showModal('success', L.AuditRetentionSaveSuccess || '');
            editor?.hide();
            await reloadTable();
        } catch (error) {
            console.error('[AuditRetention] Save failed.', error);
            showError(L.AuditRetentionSaveError || '');
            await showModal('error', L.AuditRetentionSaveError || '');
        } finally {
            setSaving(false);
        }
    }

    async function reloadTable() {
        if (!dt) return;
        const policies = await loadPolicies();
        dt.clear();
        dt.rows.add(policies);
        dt.draw(false);
        window.DtDefaults?.updateVisualState?.(dt, 0);
    }

    async function initDataTable() {
        if (!tableEl || typeof DataTable === 'undefined' || !window.DtDefaults) return;
        await loadCategoryLabels();
        const policies = await loadPolicies();

        const buttons = [
            {
                extend: 'colvis',
                text: '<i class="icon-base bx bx-show icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-colvis-btn position-relative d-none d-md-inline-flex',
                attr: {
                    title: L.ColumnVisibility || '',
                    'data-bs-toggle': 'tooltip',
                    'data-colvis-columns': '2,3,4,5,6,7,8'
                },
                columns: [2, 3, 4, 5, 6, 7, 8],
                postfixButtons: [
                    {
                        extend: 'colvisGroup',
                        text: L.ShowAll || '',
                        show: [2, 3, 4, 5, 6, 7, 8],
                        className: 'btn btn-outline-primary mt-2 w-100'
                    }
                ]
            },
            {
                text: '<i class="icon-base bx bx-refresh icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.AuditRetentionRefresh || L.Refresh || '', 'data-bs-toggle': 'tooltip' },
                action: () => reloadTable()
            }
        ];

        const config = window.DtDefaults.create({
            data: policies,
            processing: true,
            serverSide: false,
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            order: [[2, 'asc']],
            buttons,
            columns: [
                { data: null, name: 'control' },
                { data: null, name: 'policyId', render: (data, type, row) => `<code>${escapeHtml(getPolicyId(row))}</code>` },
                { data: 'category', name: 'category', render: (data, type) => type === 'display' ? `<span class="fw-medium text-heading">${escapeHtml(categoryLabel(data))}</span>` : categoryLabel(data) },
                { data: 'planTierCode', name: 'planTierCode', render: escapeHtml },
                { data: 'defaultRetentionDays', name: 'defaultRetentionDays', className: 'text-end', render: renderDays },
                { data: 'maximumRetentionDays', name: 'maximumRetentionDays', className: 'text-end', render: renderDays },
                { data: 'hotStorageDays', name: 'hotStorageDays', className: 'text-end', render: renderDays },
                { data: 'allowTenantOverride', name: 'allowTenantOverride', render: booleanBadge },
                { data: null, name: 'status', render: statusBadge },
                { data: null, name: 'action', orderable: false, searchable: false, className: 'text-end', render: (data, type, row) => renderActions(row) }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, visible: false, searchable: false },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, title: L.AuditRetentionActions || L.Actions || '', searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            initComplete: function () {
                window.DtDefaults.updateVisualState(this.api(), 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), 0);
            }
        });

        dt = new DataTable(tableEl, config);

        dt.on('column-visibility.dt column-reorder.dt columns-reordered.dt search.dt order.dt', function () {
            window.DtDefaults.updateVisualState(dt, 0);
        });

        tableEl.addEventListener('click', function (event) {
            const editButton = event.target.closest('.js-edit-policy');
            if (editButton) {
                event.preventDefault();
                event.stopPropagation();
                openEditor(findPolicyById(editButton.getAttribute('data-id')));
                return;
            }

        });
    }

    form?.addEventListener('submit', saveRetention);
    editorEl?.addEventListener('hidden.bs.offcanvas', () => {
        clearFieldErrors();
        clearError();
        loadedPolicy = null;
        loadedPolicyId = '';
        setSaving(false);
    });

    document.addEventListener('DOMContentLoaded', () => {
        syncL10n();
        initDataTable();
    });
})();
