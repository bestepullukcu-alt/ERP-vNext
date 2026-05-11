'use strict';

const ModuleCatalogAssignments = (function () {
    const root = document.getElementById('module-assignments-root');
    const moduleCode = root?.dataset.moduleCode || '';
    const L = window.L10n || {};
    const els = {
        loading: document.getElementById('module-assignments-loading'),
        error: document.getElementById('module-assignments-error'),
        retry: document.getElementById('module-assignments-retry'),
        planCount: document.getElementById('module-assignment-plan-count'),
        planDetail: document.getElementById('module-assignment-plan-detail'),
        tenantCount: document.getElementById('module-assignment-tenant-count'),
        tenantDetail: document.getElementById('module-assignment-tenant-detail'),
        enabledCount: document.getElementById('module-assignment-enabled-count'),
        enabledDetail: document.getElementById('module-assignment-enabled-detail'),
        manualCount: document.getElementById('module-assignment-manual-count'),
        manualDetail: document.getElementById('module-assignment-manual-detail'),
        planList: document.getElementById('module-assignment-plans-list'),
        planEmpty: document.getElementById('module-plans-empty'),
        planStatus: document.getElementById('module-plan-status-filter'),
        planSearch: document.getElementById('module-plan-search'),
        tenantList: document.getElementById('module-assignment-tenants-list'),
        tenantEmpty: document.getElementById('module-tenants-empty'),
        tenantDegraded: document.getElementById('module-tenants-degraded'),
        tenantSource: document.getElementById('module-tenant-source-filter'),
        tenantStatus: document.getElementById('module-tenant-status-filter'),
        tenantSearch: document.getElementById('module-tenant-search')
    };
    let loaded = false;
    let debounceTimer;

    const safe = (value) => (value === null || value === undefined || value === '' ? '-' : String(value));
    const escapeHtml = (value) => safe(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    const setVisible = (el, visible) => el?.classList.toggle('d-none', !visible);
    const getData = (payload) => payload?.data || payload?.Data || payload || {};
    const getItems = (payload) => {
        const data = getData(payload);
        return data.items || data.Items || [];
    };
    const getDependencies = (payload) => {
        const data = getData(payload);
        return data.dependencyStates || data.DependencyStates || [];
    };
    const isUnavailable = (value) => value === null || value === undefined;
    const normalizeCount = (value) => {
        const count = isUnavailable(value) ? 0 : Number(value);
        return Number.isFinite(count) ? count : 0;
    };
    const formatUnit = (count, singular, plural) => count === 1 ? singular : plural;
    const setSummaryCard = (countEl, detailEl, value, singular, plural) => {
        const count = normalizeCount(value);
        if (countEl) countEl.textContent = String(count);
        if (!detailEl) return;
        const unit = formatUnit(count, singular, plural);
        detailEl.textContent = isUnavailable(value)
            ? `${L.DataUnavailable || 'Data unavailable'} / ${count} ${unit}`
            : `${count} ${unit}`;
    };

    const badge = (value, map) => {
        const key = safe(value);
        const cls = map[key] || 'bg-label-secondary';
        const label = L[`Assignment${key}`] || L[`AssignmentStatus${key}`] || L[`AssignmentSource${key}`] || L[`Status${key}`] || key;
        return `<span class="badge ${cls}">${escapeHtml(label)}</span>`;
    };

    const statusBadge = (status) => badge(status, {
        Active: 'bg-label-success',
        Inactive: 'bg-label-warning',
        Enabled: 'bg-label-success',
        Disabled: 'bg-label-secondary',
        Suspended: 'bg-label-danger',
        Pending: 'bg-label-warning',
        Expired: 'bg-label-dark',
        Included: 'bg-label-primary'
    });

    const sourceBadge = (source) => badge(source, {
        Plan: 'bg-label-primary',
        Manual: 'bg-label-info',
        Trial: 'bg-label-warning',
        Override: 'bg-label-danger',
        System: 'bg-label-secondary'
    });

    const boolIcon = (value) => value
        ? '<i class="bx bx-check text-success fs-4"></i>'
        : '<i class="bx bx-x text-muted fs-4"></i>';

    const formatDate = (value) => {
        if (!value) return '-';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return '-';
        return date.toLocaleDateString();
    };

    const formatDateRange = (from, to) => {
        const start = formatDate(from);
        const end = formatDate(to);
        return start === '-' && end === '-' ? '-' : `${start} - ${end}`;
    };

    const interpolate = (template, value) => String(template || '').replace('{0}', value);
    const getNumber = (...values) => {
        const value = values.find((item) => item !== null && item !== undefined && item !== '');
        const number = Number(value);
        return Number.isFinite(number) ? number : null;
    };

    const metaItem = (label, value) => `<div class="col-12 col-md-6 col-xl-3">
        <div class="backbone-preview-label mb-1">${escapeHtml(label)}</div>
        <div class="small text-heading">${value}</div>
    </div>`;

    const fetchJson = async (url) => {
        const response = await fetch(url, { credentials: 'same-origin' });
        let payload = {};
        try {
            payload = await response.json();
        } catch { }
        if (!response.ok) {
            const message = payload?.errors?.join(' ') || payload?.Errors?.join(' ') || L.ErrorOccurred || 'Error occurred.';
            throw new Error(message);
        }
        return payload;
    };

    const setSummary = (overviewPayload) => {
        const data = getData(overviewPayload);
        setSummaryCard(
            els.planCount,
            els.planDetail,
            data.planAssignmentCount ?? data.PlanAssignmentCount,
            L.PlanUnitSingular || 'plan',
            L.PlanUnitPlural || 'plans');
        setSummaryCard(
            els.tenantCount,
            els.tenantDetail,
            data.tenantAssignmentCount ?? data.TenantAssignmentCount,
            L.AssignedTenantUnitSingular || 'assigned tenant',
            L.AssignedTenantUnitPlural || 'assigned tenants');
        setSummaryCard(
            els.enabledCount,
            els.enabledDetail,
            data.enabledTenantCount ?? data.EnabledTenantCount,
            L.EnabledTenantUnitSingular || 'enabled tenant',
            L.EnabledTenantUnitPlural || 'enabled tenants');
        setSummaryCard(
            els.manualCount,
            els.manualDetail,
            data.manualOverrideCount ?? data.ManualOverrideCount,
            L.OverrideUnitSingular || 'override',
            L.OverrideUnitPlural || 'overrides');
    };

    const renderPlans = (payload) => {
        const rows = getItems(payload);
        if (!els.planList) return;
        setVisible(els.planEmpty, rows.length === 0);
        els.planList.innerHTML = rows.map((row) => {
            const planCode = row.planCode || row.PlanCode;
            const planName = row.planName || row.PlanName;
            const planStatus = row.planStatus || row.PlanStatus;
            const isActive = String(planStatus).toLowerCase() === 'active';
            const displayStatus = isActive ? (L.AssignmentStatusEnabled || 'Enabled') : (L.AssignmentStatusDisabled || 'Disabled');
            const tenantReach = getNumber(row.tenantCount, row.TenantCount, row.assignedTenantCount, row.AssignedTenantCount);
            const description = tenantReach !== null
                ? interpolate(L.PlanEntitlementTenantReach || '{0} tenants will receive this module', tenantReach)
                : (isActive
                    ? (L.PlanEntitlementActiveDescription || 'Subscription plan entitlement grants this module')
                    : (L.PlanEntitlementInactiveDescription || 'Module is included but currently inactive'));
            const includedSince = formatDate(row.effectiveFromUtc || row.EffectiveFromUtc || row.lastUpdatedAtUtc || row.LastUpdatedAtUtc);
            return `<article class="list-group-item p-4">
                <div class="d-flex flex-column flex-md-row align-items-md-start justify-content-between gap-3 mb-3">
                    <div>
                        <div class="fw-semibold text-heading">${escapeHtml(planName)}</div>
                        <code class="small">${escapeHtml(planCode)}</code>
                    </div>
                    <span class="badge ${isActive ? 'bg-label-success' : 'bg-label-secondary'}">${escapeHtml(displayStatus)}</span>
                </div>
                <div class="text-body mb-3">${escapeHtml(description)}</div>
                <div class="d-flex flex-column flex-md-row gap-2 gap-md-4 small text-body-secondary">
                    <span><span class="text-heading fw-medium">${escapeHtml(L.AssignmentSource || 'Source')}:</span> ${escapeHtml(L.PlanEntitlementSource || 'Subscription Plan')}</span>
                    <span><span class="text-heading fw-medium">${escapeHtml(L.IncludedSince || 'Included since')}:</span> ${escapeHtml(includedSince)}</span>
                </div>
            </article>`;
        }).join('');
    };

    const getTenantGroupKey = (row) => {
        const tenantStatus = row.tenantStatus || row.TenantStatus;
        const assignmentStatus = row.assignmentStatus || row.AssignmentStatus;
        const source = row.assignmentSource || row.AssignmentSource;

        if (['Suspended', 'Disabled', 'Expired'].includes(tenantStatus) || ['Suspended', 'Disabled', 'Expired'].includes(assignmentStatus)) {
            return 'blocked';
        }

        if (['Manual', 'Override'].includes(source)) {
            return 'manual';
        }

        return 'plans';
    };

    const tenantGroupTitle = (key) => ({
        plans: L.FromSubscriptionPlans || 'From Subscription Plans',
        manual: L.ManualOverrides || 'Manual Overrides',
        blocked: L.BlockedSuspended || 'Blocked / Suspended'
    }[key] || key);

    const tenantSourceText = (row) => {
        const source = row.assignmentSource || row.AssignmentSource;
        const sourcePlan = row.sourcePlanCode || row.SourcePlanCode;
        const assignmentStatus = row.assignmentStatus || row.AssignmentStatus;
        const tenantStatus = row.tenantStatus || row.TenantStatus;

        if (tenantStatus === 'Suspended') return L.TenantSuspended || 'Tenant Suspended';
        if (source === 'Manual' && assignmentStatus === 'Disabled') return L.ManualDisabled || 'Manual Disabled';
        if (source === 'Manual' || source === 'Override') return L.ManualAssignment || 'Manual Assignment';
        return sourcePlan || source || '-';
    };

    const renderTenantGroup = (key, rows) => {
        if (!rows.length) return '';
        return `<section class="p-4 border-top">
            <div class="fw-semibold text-heading mb-3">${escapeHtml(tenantGroupTitle(key))}</div>
            <div class="d-grid gap-2">
                ${rows.map((row) => {
                    const tenantCode = row.tenantCode || row.TenantCode;
                    return `<article class="d-flex flex-column flex-lg-row align-items-lg-center justify-content-between gap-2 py-2">
                        <div class="min-w-0">
                            <div class="fw-medium text-heading">${escapeHtml(row.tenantName || row.TenantName)}</div>
                            <code class="small">${escapeHtml(tenantCode)}</code>
                        </div>
                        <div class="text-body-secondary small">${escapeHtml(tenantSourceText(row))}</div>
                        <div class="d-flex align-items-center gap-2">
                            ${statusBadge(row.assignmentStatus || row.AssignmentStatus)}
                            <button type="button"
                                    class="btn btn-icon btn-sm btn-label-secondary btn-tenant-assignment-detail"
                                    data-bs-toggle="tooltip"
                                    title="${escapeHtml(L.Details || 'Details')}"
                                    aria-label="${escapeHtml(L.Details || 'Details')}"
                                    data-tenant-code="${escapeHtml(tenantCode)}">
                                <i class="bx bx-show"></i>
                            </button>
                        </div>
                    </article>`;
                }).join('')}
            </div>
        </section>`;
    };

    const renderTenants = (payload) => {
        const rows = getItems(payload);
        const dependencies = getDependencies(payload);
        const degraded = dependencies.some((item) => (item.status || item.Status) === 'Unavailable');
        if (!els.tenantList) return;
        setVisible(els.tenantDegraded, degraded);
        setVisible(els.tenantEmpty, !degraded && rows.length === 0);
        if (degraded) {
            els.tenantList.innerHTML = '';
            return;
        }

        const groups = rows.reduce((acc, row) => {
            acc[getTenantGroupKey(row)].push(row);
            return acc;
        }, { plans: [], manual: [], blocked: [] });

        els.tenantList.innerHTML = [
            renderTenantGroup('plans', groups.plans),
            renderTenantGroup('manual', groups.manual),
            renderTenantGroup('blocked', groups.blocked)
        ].join('');
    };

    const buildPlanQuery = () => {
        const params = new URLSearchParams();
        if (els.planStatus?.value) params.set('status', els.planStatus.value);
        if (els.planSearch?.value?.trim()) params.set('search', els.planSearch.value.trim());
        return params.toString();
    };

    const buildTenantQuery = () => {
        const params = new URLSearchParams();
        if (els.tenantSource?.value) params.set('source', els.tenantSource.value);
        if (els.tenantStatus?.value) params.set('status', els.tenantStatus.value);
        if (els.tenantSearch?.value?.trim()) params.set('search', els.tenantSearch.value.trim());
        return params.toString();
    };

    const loadPlans = async () => {
        const query = buildPlanQuery();
        const payload = await fetchJson(`/Platform/ModuleCatalog/api/${encodeURIComponent(moduleCode)}/assignments/plans${query ? `?${query}` : ''}`);
        renderPlans(payload);
    };

    const loadTenants = async () => {
        const query = buildTenantQuery();
        const payload = await fetchJson(`/Platform/ModuleCatalog/api/${encodeURIComponent(moduleCode)}/assignments/tenants${query ? `?${query}` : ''}`);
        renderTenants(payload);
    };

    const load = async () => {
        if (!moduleCode) return;
        setVisible(els.loading, true);
        setVisible(els.error, false);
        try {
            const overview = await fetchJson(`/Platform/ModuleCatalog/api/${encodeURIComponent(moduleCode)}/assignments/overview`);
            setSummary(overview);
            await Promise.all([loadPlans(), loadTenants()]);
        } catch (error) {
            console.error('[ModuleCatalogAssignments] Load failed.', error);
            setVisible(els.error, true);
            window.showToast?.(error.message || (L.ErrorOccurred || 'Error occurred.'), 'error');
        } finally {
            setVisible(els.loading, false);
        }
    };

    const scheduleReload = () => {
        window.clearTimeout(debounceTimer);
        debounceTimer = window.setTimeout(() => {
            loadPlans().catch((error) => {
                console.error('[ModuleCatalogAssignments] Plan reload failed.', error);
                window.showToast?.(error.message || (L.ErrorOccurred || 'Error occurred.'), 'error');
            });
            loadTenants().catch((error) => {
                console.error('[ModuleCatalogAssignments] Tenant reload failed.', error);
                window.showToast?.(error.message || (L.ErrorOccurred || 'Error occurred.'), 'error');
            });
        }, 250);
    };

    const init = () => {
        if (!root) return;
        document.querySelector('[data-bs-target="#module-assignments-tab"]')?.addEventListener('shown.bs.tab', () => {
            if (loaded) return;
            loaded = true;
            load();
        });
        els.retry?.addEventListener('click', load);
        [els.planStatus, els.planSearch, els.tenantSource, els.tenantStatus, els.tenantSearch]
            .forEach((el) => el?.addEventListener('input', scheduleReload));
        [els.planStatus, els.tenantSource, els.tenantStatus]
            .forEach((el) => el?.addEventListener('change', scheduleReload));
        els.tenantList?.addEventListener('click', (event) => {
            const button = event.target.closest('.btn-tenant-assignment-detail');
            if (!button) return;
            window.showToast?.(L.TenantAssignmentsDegraded || 'Tenant assignment data is temporarily unavailable.', 'warning');
        });
    };

    return { init, load };
})();

document.addEventListener('DOMContentLoaded', () => ModuleCatalogAssignments.init());
