/**
 * Platform / Tenant Registry
 */
'use strict';

const TenantRegistryPage = (function () {
    const dtTableEl = document.querySelector('.datatables-tenants');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    let dt;

    const showError = (message) => {
        if (window.showToast) {
            window.showToast(message, 'error');
            return;
        }
        alert(message);
    };

    const showSuccess = (message) => {
        if (window.showToast) {
            window.showToast(message, 'success');
            return;
        }
        alert(message);
    };

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

    const formatDate = (value) => {
        if (!value) return '-';
        try {
            return new Date(value).toLocaleString();
        } catch (e) {
            return value;
        }
    };

    const unwrap = (payload) => {
        if (!payload) return null;
        if (Object.prototype.hasOwnProperty.call(payload, 'data')) return payload.data;
        return payload;
    };

    const statusBadge = (status) => {
        const map = {
            Active: 'bg-label-success',
            Provisioning: 'bg-label-info',
            Suspended: 'bg-label-warning',
            Deactivated: 'bg-label-danger'
        };
        const css = map[status] || 'bg-label-secondary';
        return `<span class="badge ${css}">${escapeHtml(status || 'Unknown')}</span>`;
    };

    const envBadge = (environment) => {
        const map = {
            Production: 'bg-label-success',
            Staging: 'bg-label-warning',
            Development: 'bg-label-info'
        };
        const css = map[environment] || 'bg-label-secondary';
        return `<span class="badge ${css}">${escapeHtml(environment || '-')}</span>`;
    };

    const updateKpis = (stats) => {
        if (!stats) return;
        document.getElementById('kpi-total').innerText = String(stats.total || 0);
        document.getElementById('kpi-active').innerText = String(stats.active || 0);
        document.getElementById('kpi-provisioning').innerText = String(stats.provisioning || 0);
        document.getElementById('kpi-suspended').innerText = String(stats.suspended || 0);
    };

    const loadStats = async () => {
        try {
            const response = await fetch(apiUrl + '/api/admin/tenants/stats', {
                credentials: 'include'
            });
            if (!response.ok) return;
            const json = await response.json();
            updateKpis(unwrap(json));
        } catch (e) {
            // no-op: list load still works
        }
    };

    const populateDetails = (data) => {
        if (!data) return;

        const safe = (v) => (v === null || v === undefined || v === '' ? '-' : String(v));

        document.getElementById('oc-tenant-title').innerText = safe(data.displayName || data.name);
        document.getElementById('oc-tenant-subtitle').innerText = `${safe(data.code)} / ${safe(data.provisioningStatus)}`;
        document.getElementById('oc-tenant-status').outerHTML = statusBadge(data.status).replace('<span', '<span id="oc-tenant-status"');
        document.getElementById('oc-tenant-environment').outerHTML = envBadge(data.environment).replace('<span', '<span id="oc-tenant-environment"');

        document.getElementById('oc-tenant-id').innerText = safe(data.id);
        document.getElementById('oc-tenant-code').innerText = safe(data.code);
        document.getElementById('oc-tenant-slug').innerText = safe(data.provisioningStatus);
        document.getElementById('oc-tenant-display-name').innerText = safe(data.displayName || data.name);
        document.getElementById('oc-tenant-domain').innerText = safe(data.domain);
        document.getElementById('oc-tenant-region').innerText = safe(data.region);
        document.getElementById('oc-tenant-tier').innerText = safe(data.tier);
        document.getElementById('oc-tenant-created').innerText = formatDate(data.createdAt);
        document.getElementById('oc-tenant-created-by').innerText = safe(data.createdBy);
    };

    const loadTenantDetail = async (id) => {
        const response = await fetch(apiUrl + '/api/admin/tenants/' + encodeURIComponent(id), {
            credentials: 'include'
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || ('HTTP ' + response.status));
        }

        const json = await response.json();
        return unwrap(json);
    };

    const changeLifecycle = async (tenantId, action) => {
        const response = await fetch(`${apiUrl}/api/admin/tenants/${encodeURIComponent(tenantId)}/${action}`, {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({})
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || ('HTTP ' + response.status));
        }

        return unwrap(await response.json());
    };

    const initDataTable = () => {
        if (!dtTableEl) return;

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/admin/tenants',
                type: 'GET',
                xhrFields: { withCredentials: true },
                dataSrc: function (json) {
                    const data = unwrap(json) || {};
                    const rows = Array.isArray(data.items) ? data.items : [];
                    loadStats();
                    return rows;
                },
                error: function (xhr) {
                    if (xhr.status === 401 || xhr.status === 403) {
                        window.location.href = '/platform/login?returnUrl=' + encodeURIComponent(window.location.pathname);
                        return;
                    }

                    if (xhr.status === 502) {
                        showError('Gateway or Platform service is unavailable. Please verify ports 5000 and 5057.');
                        return;
                    }

                    showError('Tenant registry request failed (HTTP ' + (xhr.status || 0) + ').');
                }
            },
            stateSave: true,
            order: [[8, 'desc']],
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'id', name: 'identity' },
                { data: 'code', name: 'code_slug' },
                { data: 'displayName', name: 'display_name' },
                { data: 'status', name: 'status' },
                { data: 'region', name: 'region' },
                { data: 'environment', name: 'environment' },
                { data: 'createdAt', name: 'created_meta' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                {
                    targets: 1,
                    searchable: false,
                    orderable: false,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data)}">`
                },
                {
                    targets: 2,
                    render: function (data, type, full) {
                        const shortId = (full.id || '').toString().slice(0, 8);
                        return `<div><span class="fw-medium text-heading">${escapeHtml(shortId)}</span><br/><small class="text-muted">${escapeHtml(full.id)}</small></div>`;
                    }
                },
                {
                    targets: 3,
                    render: function (data, type, full) {
                        return `<div><span class="fw-medium text-primary">${escapeHtml(full.code)}</span><br/><small class="text-muted">${escapeHtml(full.provisioningStatus || '')}</small></div>`;
                    }
                },
                {
                    targets: 4,
                    render: function (data, type, full) {
                        return `<div><span class="fw-medium text-heading">${escapeHtml(full.displayName || full.name)}</span><br/><small class="text-muted">${escapeHtml(full.domain || '-')}</small></div>`;
                    }
                },
                {
                    targets: 5,
                    render: function (data) {
                        return statusBadge(data);
                    }
                },
                {
                    targets: 7,
                    render: function (data) {
                        return envBadge(data);
                    }
                },
                {
                    targets: 8,
                    render: function (data, type, full) {
                        const createdBy = escapeHtml(full.createdBy || 'system');
                        return `<div><span class="fw-medium">${escapeHtml(formatDate(full.createdAt))}</span><br/><small class="text-muted">by ${createdBy}</small></div>`;
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit text-end',
                    render: function (data, type, full) {
                        const suspendDisabled = full.status === 'Suspended' || full.status === 'Deactivated' ? 'disabled' : '';
                        const reactivateDisabled = full.status === 'Active' || full.status === 'Deactivated' ? 'disabled' : '';

                        return `<div class="d-flex align-items-center justify-content-end">
                            <a href="javascript:void(0);" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="javascript:void(0);" class="dropdown-item js-tenant-preview" data-id="${escapeHtml(full.id)}" data-bs-toggle="offcanvas" data-bs-target="#offcanvasTenantDetails">View Snapshot</a>
                                <a href="javascript:void(0);" class="dropdown-item js-tenant-suspend ${suspendDisabled}" data-id="${escapeHtml(full.id)}">Suspend</a>
                                <a href="javascript:void(0);" class="dropdown-item js-tenant-reactivate ${reactivateDisabled}" data-id="${escapeHtml(full.id)}">Reactivate</a>
                            </div>
                        </div>`;
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                'Register Tenant',
                { id: 'btnOpenCreateTenant' },
                null,
                {
                    exportColumns: [2, 3, 4, 5, 6, 7, 8],
                    colvisColumns: [2, 3, 4, 5, 6, 7, 8]
                }
            )
        }));
    };

    const openCreatePanel = () => {
        const panel = document.getElementById('offcanvasAddTenant');
        if (!panel) return;
        bootstrap.Offcanvas.getOrCreateInstance(panel).show();
    };

    const bindEvents = () => {
        document.addEventListener('click', async function (event) {
            const createBtn = event.target.closest('#btnOpenCreateTenant');
            if (createBtn) {
                event.preventDefault();
                openCreatePanel();
                return;
            }

            const previewBtn = event.target.closest('.js-tenant-preview');
            if (previewBtn) {
                const tenantId = previewBtn.getAttribute('data-id');
                if (!tenantId) return;
                try {
                    const detail = await loadTenantDetail(tenantId);
                    populateDetails(detail);
                } catch (e) {
                    showError('Tenant detail load failed.');
                }
                return;
            }

            const suspendBtn = event.target.closest('.js-tenant-suspend:not(.disabled)');
            if (suspendBtn) {
                event.preventDefault();
                const tenantId = suspendBtn.getAttribute('data-id');
                if (!tenantId) return;

                try {
                    await changeLifecycle(tenantId, 'suspend');
                    dt.ajax.reload(null, false);
                    loadStats();
                    showSuccess('Tenant suspended.');
                } catch (e) {
                    showError('Suspend tenant failed.');
                }
                return;
            }

            const reactivateBtn = event.target.closest('.js-tenant-reactivate:not(.disabled)');
            if (reactivateBtn) {
                event.preventDefault();
                const tenantId = reactivateBtn.getAttribute('data-id');
                if (!tenantId) return;

                try {
                    await changeLifecycle(tenantId, 'reactivate');
                    dt.ajax.reload(null, false);
                    loadStats();
                    showSuccess('Tenant reactivated.');
                } catch (e) {
                    showError('Reactivate tenant failed.');
                }
            }
        });

        const nameEl = document.getElementById('tenantName');
        const subdomainEl = document.getElementById('tenantSubdomain');
        if (nameEl && subdomainEl) {
            nameEl.addEventListener('input', function () {
                if (subdomainEl.dataset.touched === '1') return;
                subdomainEl.value = nameEl.value.trim().toLowerCase().replace(/\s+/g, '-');
            });
            subdomainEl.addEventListener('input', function () {
                subdomainEl.dataset.touched = '1';
            });
        }

        const form = document.getElementById('formAddTenant');
        if (!form) return;

        form.addEventListener('submit', async function (e) {
            e.preventDefault();

            const submitBtn = form.querySelector('button[type="submit"]');
            const originalText = submitBtn.innerHTML;
            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Creating...';

            const formData = new FormData(form);
            const payload = {
                name: (formData.get('name') || '').toString().trim(),
                domain: (formData.get('domain') || '').toString().trim(),
                subdomain: (formData.get('subdomain') || '').toString().trim(),
                displayName: (formData.get('displayName') || '').toString().trim(),
                tier: (formData.get('tier') || 'Standard').toString(),
                region: (formData.get('region') || 'US').toString(),
                environment: (formData.get('environment') || 'Production').toString()
            };

            try {
                const response = await fetch(apiUrl + '/api/admin/tenants', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        ...getAuthHeaders()
                    },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) {
                    if (response.status === 401 || response.status === 403) {
                        window.location.href = '/platform/login?returnUrl=' + encodeURIComponent(window.location.pathname);
                        return;
                    }

                    const errText = await response.text();
                    throw new Error(errText || ('HTTP ' + response.status));
                }

                bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasAddTenant'))?.hide();
                form.reset();
                if (subdomainEl) {
                    subdomainEl.dataset.touched = '0';
                }
                dt.ajax.reload(null, false);
                loadStats();
                showSuccess('Tenant created and provisioning started.');
            } catch (error) {
                showError('Create tenant failed: ' + (error.message || 'Unknown error'));
            } finally {
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalText;
            }
        });
    };

    const init = () => {
        if (!dtTableEl) return;
        initDataTable();
        bindEvents();
        loadStats();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', TenantRegistryPage.init);
