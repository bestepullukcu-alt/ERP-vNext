/**
 * Tenant Core Details Page Script
 * Diten ERP vNext - Platform/Tenants
 */
'use strict';

const TenantDetails = (function () {
    const root = document.getElementById('tenantDetailsRoot');
    const tenantId = root?.getAttribute('data-tenant-id');
    const apiUrl = window.API?.platform || window.ApiBaseUrl || 'http://localhost:5000';
    let L = window.L10n || {};

    const syncL10n = () => { L = window.L10n || {}; };
    const getAuthHeaders = () => ({});
    const unwrap = (payload) => payload?.data ?? payload ?? null;

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
        try { return new Date(value).toLocaleString(); } catch (error) { return value; }
    };

    const statusBadgeClass = (status) => ({
        Active: 'bg-label-success',
        Provisioning: 'bg-label-info',
        Suspended: 'bg-label-warning',
        Deactivated: 'bg-label-danger'
    }[status] || 'bg-label-secondary');

    const fetchJson = async (url, options) => {
        const response = await fetch(url, Object.assign({
            credentials: 'include',
            headers: getAuthHeaders()
        }, options || {}));

        if (response.status === 401 || response.status === 403) {
            window.DtDefaults?.handleUnauthorized?.();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            throw authError;
        }

        if (!response.ok) throw new Error(await response.text());
        return unwrap(await response.json());
    };

    const renderDefinitionList = (elementId, rows) => {
        const element = document.getElementById(elementId);
        if (!element) return;
        element.innerHTML = rows.map(([label, value]) =>
            `<dt class="col-5 mb-2">${escapeHtml(label)}</dt><dd class="col-7 mb-2 text-break">${escapeHtml(value || '-')}</dd>`
        ).join('');
    };

    const renderListGroup = (elementId, rows, emptyText) => {
        const element = document.getElementById(elementId);
        if (!element) return;
        if (!rows || rows.length === 0) {
            element.innerHTML = `<div class="text-muted">${escapeHtml(emptyText || '-')}</div>`;
            return;
        }

        element.innerHTML = rows.map((row) =>
            `<div class="list-group-item px-0">
                <div class="d-flex justify-content-between gap-3">
                    <div>
                        <div class="fw-medium">${escapeHtml(row.title)}</div>
                        <small class="text-muted">${escapeHtml(row.subtitle || '')}</small>
                    </div>
                    <span class="badge bg-label-secondary align-self-start">${escapeHtml(row.badge || '')}</span>
                </div>
            </div>`
        ).join('');
    };

    const loadOverview = async () => {
        const detail = await fetchJson(`${apiUrl}/api/admin/tenants/${encodeURIComponent(tenantId)}`);

        document.getElementById('detailsTitle').innerText = detail.displayName || detail.name || '-';
        document.getElementById('detailDisplayName').innerText = detail.displayName || detail.name || '-';
        document.getElementById('detailCodeSlug').innerText = `${detail.code || '-'} / ${detail.slug || '-'}`;
        const statusEl = document.getElementById('detailStatus');
        statusEl.className = `badge ${statusBadgeClass(detail.status)}`;
        statusEl.innerText = detail.status || '-';
        document.getElementById('detailProvisioning').innerText = detail.provisioningStatus || '-';
        document.getElementById('detailDomain').innerText = detail.domain || '-';
        document.getElementById('detailTenantType').innerText = detail.tenantType || '-';
        document.getElementById('detailCountry').innerText = detail.country || '-';

        renderDefinitionList('legalContactList', [
            [L.LegalName || 'Legal Name', detail.legalName],
            [L.TaxNumber || 'Tax Number', detail.taxNumber],
            [L.Industry || 'Industry', detail.industry],
            [L.ContactPerson || 'Contact Person', detail.contactPerson],
            [L.ContactEmail || 'Contact Email', detail.contactEmail],
            [L.ContactPhone || 'Contact Phone', detail.contactPhone]
        ]);

        renderDefinitionList('localeDefaultsList', [
            [L.DefaultTimezone || 'Timezone', detail.defaultTimezone],
            [L.DefaultLanguage || 'Language', detail.defaultLanguage],
            [L.DefaultCurrency || 'Currency', detail.defaultCurrency],
            [L.Created || 'Created', formatDate(detail.createdAt)],
            ['Created By', detail.createdBy]
        ]);

        renderListGroup('provisioningSteps', (detail.provisioningSteps || []).map((step) => ({
            title: step.label,
            subtitle: step.detail || formatDate(step.completedAt || step.createdAt),
            badge: step.status
        })), L.DtEmptyTable);

        renderListGroup('activityTimeline', (detail.recentActivity || []).map((activity) => ({
            title: activity.eventType,
            subtitle: `${activity.message || ''} ${formatDate(activity.at)}`,
            badge: activity.actor || 'system'
        })), L.DtEmptyTable);

        return detail;
    };

    const loadModules = async () => {
        const data = await fetchJson(`${apiUrl}/api/admin/tenants/${encodeURIComponent(tenantId)}/modules`);
        document.getElementById('modulesSummary').innerHTML = `<div class="table-responsive">
            <table class="table">
                <thead><tr><th>${escapeHtml(L.Modules || 'Module')}</th><th>${escapeHtml(L.Status || 'Status')}</th><th>Source</th></tr></thead>
                <tbody>${(data.entitlements || []).map((item) => `<tr><td>${escapeHtml(item.moduleName)}</td><td>${item.enabled ? L.Active : L.Passive}</td><td>${escapeHtml(item.source)}</td></tr>`).join('')}</tbody>
            </table>
        </div>`;
    };

    const loadUsers = async () => {
        const data = await fetchJson(`${apiUrl}/api/admin/tenants/${encodeURIComponent(tenantId)}/users/summary`);
        renderDefinitionList('usersSummary', [
            ['Total Users', data.totalUsers],
            ['Active Users', data.activeUsers],
            ['Pending Invitations', data.pendingInvitations],
            ['Invitation Policy', data.invitationPolicy]
        ]);
    };

    const loadSettings = async () => {
        const data = await fetchJson(`${apiUrl}/api/admin/tenants/${encodeURIComponent(tenantId)}/settings`);
        renderDefinitionList('settingsSummary', [
            [L.Region || 'Region', data.region],
            [L.DefaultLanguage || 'Language', data.language],
            [L.DefaultTimezone || 'Timezone', data.timezone],
            [L.DefaultCurrency || 'Currency', data.currency],
            ['Environment', data.environment]
        ]);
    };

    const changeLifecycle = async (action, reason) => {
        await fetchJson(`${apiUrl}/api/admin/tenants/${encodeURIComponent(tenantId)}/${action}`, {
            method: 'POST',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: reason || '' })
        });
        await loadOverview();
    };

    const bindLifecycle = () => {
        document.getElementById('btnSuspendTenant')?.addEventListener('click', () => {
            window.showConfirm?.('AreYouSure', async (reason) => {
                try {
                    await changeLifecycle('suspend', reason);
                    window.showToast?.(L.TenantSuspended || 'Tenant suspended.', 'success');
                } catch (error) {
                    window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                }
            }, { type: 'warning', confirmButtonText: L.Suspend, showInput: true, inputPlaceholder: L.SuspendReason });
        });

        document.getElementById('btnReactivateTenant')?.addEventListener('click', () => {
            window.showConfirm?.('AreYouSure', async () => {
                try {
                    await changeLifecycle('reactivate', '');
                    window.showToast?.(L.TenantReactivated || 'Tenant reactivated.', 'success');
                } catch (error) {
                    window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                }
            }, { type: 'success', confirmButtonText: L.Reactivate });
        });
    };

    const bindTabs = () => {
        document.querySelector('[data-bs-target="#tabModules"]')?.addEventListener('shown.bs.tab', () => loadModules().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error')), { once: true });
        document.querySelector('[data-bs-target="#tabUsers"]')?.addEventListener('shown.bs.tab', () => loadUsers().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error')), { once: true });
        document.querySelector('[data-bs-target="#tabSettings"]')?.addEventListener('shown.bs.tab', () => loadSettings().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error')), { once: true });
    };

    return {
        init: () => {
            syncL10n();
            if (!tenantId) return;
            loadOverview().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error'));
            bindLifecycle();
            bindTabs();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantDetails.init());
