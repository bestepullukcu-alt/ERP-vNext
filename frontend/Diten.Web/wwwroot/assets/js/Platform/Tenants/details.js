/**
 * Tenant Core Details Page Script
 * Diten ERP vNext - Platform/Tenants
 */
'use strict';

const TenantDetails = (function () {
    const root = document.getElementById('tenantDetailsRoot');
    const tenantId = root?.getAttribute('data-tenant-id');
    const apiBase = '/Platform/Tenants/api';
    let L = window.L10n || {};
    let currentBranding = {
        logoDataUrl: null,
        faviconDataUrl: null
    };
    let recentActivities = [];
    let recentActivityExpanded = false;
    let adminUsersDt;
    let adminUsersLoaded = false;
    let commercialLoaded = false;
    let commercialSubscription = null;
    let commercialSubscriptionDt;
    let activeSubscriptionPlans = [];
    let subscriptionAppliedFilters = { status: [], cancelAtPeriodEnd: '' };
    let moduleEntitlementsLoaded = false;
    let moduleEntitlementsDt;
    let availableModules = [];
    let moduleEntitlementAppliedFilters = { source: [], access: [] };
    let quotaGovernanceLoaded = false;

    const syncL10n = () => { L = window.L10n || {}; };
    const getAuthHeaders = () => ({});
    const unwrap = (payload) => payload?.data ?? payload?.Data ?? payload ?? null;
    const subscriptionFilterCollapseId = 'inlineFilterCollapse';
    const moduleEntitlementFilterCollapseId = 'moduleEntitlementFilterCollapse';

    const extractErrorMessage = (payload, fallback) => {
        if (!payload) return fallback;
        if (typeof payload === 'string') {
            try {
                return extractErrorMessage(JSON.parse(payload), fallback);
            } catch (error) {
                return payload.trim() || fallback;
            }
        }

        const errors = payload.errors || payload.Errors;
        if (Array.isArray(errors) && errors.length > 0) return errors.filter(Boolean).join('\n');
        if (errors && typeof errors === 'object') {
            const messages = Object.values(errors).flat().filter(Boolean);
            if (messages.length > 0) return messages.join('\n');
        }

        return payload.message || payload.Message || payload.title || payload.Title || fallback;
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
            const date = new Date(value);
            if (Number.isNaN(date.getTime())) return value;
            const parts = new Intl.DateTimeFormat('en-GB', {
                day: '2-digit',
                month: 'short',
                year: 'numeric',
                hour: 'numeric',
                minute: '2-digit',
                second: '2-digit',
                hour12: true
            }).formatToParts(date).reduce((acc, part) => {
                acc[part.type] = part.value;
                return acc;
            }, {});
            return `${parts.day} ${parts.month} ${parts.year} ${parts.hour}:${parts.minute}:${parts.second} ${String(parts.dayPeriod || '').toUpperCase()}`.trim();
        } catch (error) { return value; }
    };

    const formatRelativeTime = (value) => {
        if (!value) return '-';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return formatDate(value);

        const seconds = Math.round((date.getTime() - Date.now()) / 1000);
        const ranges = [
            { unit: 'year', seconds: 31536000 },
            { unit: 'month', seconds: 2592000 },
            { unit: 'week', seconds: 604800 },
            { unit: 'day', seconds: 86400 },
            { unit: 'hour', seconds: 3600 },
            { unit: 'minute', seconds: 60 }
        ];

        const range = ranges.find((item) => Math.abs(seconds) >= item.seconds);
        if (!range) return 'just now';

        try {
            return new Intl.RelativeTimeFormat('en', { numeric: 'auto' }).format(
                Math.round(seconds / range.seconds),
                range.unit
            );
        } catch (error) {
            return formatDate(value);
        }
    };

    const statusBadgeClass = (status) => ({
        Active: 'bg-label-success',
        Provisioning: 'bg-label-info',
        Suspended: 'bg-label-warning',
        Deactivated: 'bg-label-danger'
    }[status] || 'bg-label-secondary');

    const subscriptionBadgeClass = (status) => ({
        Active: 'bg-label-success',
        PendingProvisioning: 'bg-label-info',
        Trialing: 'bg-label-info',
        PastDue: 'bg-label-warning',
        TrialExpired: 'bg-label-warning',
        Suspended: 'bg-label-warning',
        Cancelled: 'bg-label-danger',
        Expired: 'bg-label-secondary'
    }[status] || 'bg-label-secondary');

    const tenantTypeBadgeClass = (tenantType) => ({
        Customer: 'bg-label-primary',
        Demo: 'bg-label-info',
        Internal: 'bg-label-dark',
        Trial: 'bg-label-warning',
        Paid: 'bg-label-success'
    }[tenantType] || 'bg-label-secondary');

    const badgeMarkup = (value, className) =>
        `<span class="badge ${escapeHtml(className || 'bg-label-secondary')}">${escapeHtml(value || '-')}</span>`;

    const formatActor = (value) => {
        const actor = String(value || '').trim();
        if (!actor) return 'system';

        const currentActor = window.TenantCurrentActor || {};
        const currentActorId = String(currentActor.id || '').toLowerCase();
        if (currentActorId && actor.toLowerCase() === currentActorId && currentActor.display) {
            return currentActor.display;
        }

        return actor;
    };

    const syncLifecycleActions = (status) => {
        const suspendButton = document.getElementById('btnSuspendTenant');
        const reactivateButton = document.getElementById('btnReactivateTenant');
        const normalized = String(status || '').toLowerCase();

        suspendButton?.classList.toggle('d-none', normalized !== 'active');
        reactivateButton?.classList.toggle('d-none', normalized !== 'suspended');
    };

    const fetchJson = async (url, options) => {
        const headers = {
            Accept: 'application/json',
            ...getAuthHeaders(),
            ...(options?.headers || {})
        };
        const response = await fetch(url, {
            credentials: 'same-origin',
            ...(options || {}),
            headers
        });

        const contentType = response.headers.get('content-type') || '';
        const redirectedToLogin = response.redirected && /\/(account|platform)\/login/i.test(response.url || '');
        if (response.status === 401 || redirectedToLogin) {
            window.DtDefaults?.handleUnauthorized?.();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            throw authError;
        }

        if (response.status === 403) {
            throw new Error(L.PermissionDenied || 'Permission denied.');
        }

        const text = await response.text();
        let payload = null;
        if (text) {
            try {
                payload = JSON.parse(text);
            } catch (error) {
                payload = text;
            }
        }

        if (!response.ok) {
            throw new Error(extractErrorMessage(payload, L.ErrorOccurred || 'Error occurred.'));
        }

        if (response.status === 204) return null;
        if (!contentType.toLowerCase().includes('application/json')) {
            throw new Error('Unexpected non-JSON response.');
        }

        const isSuccessful = payload?.isSuccessful ?? payload?.IsSuccessful ?? payload?.succeeded ?? payload?.Succeeded ?? payload?.success ?? payload?.Success;
        if (isSuccessful === false) {
            throw new Error(extractErrorMessage(payload, L.ErrorOccurred || 'Error occurred.'));
        }

        return unwrap(payload);
    };

    const imageMarkup = (src, alt, className) => `<img src="${escapeHtml(src)}" alt="${escapeHtml(alt)}" class="${escapeHtml(className)}" />`;

    const renderBranding = (detail) => {
        currentBranding = {
            logoDataUrl: detail.logoDataUrl || detail.LogoDataUrl || null,
            faviconDataUrl: detail.faviconDataUrl || detail.FaviconDataUrl || null
        };

        const logoAvatar = document.getElementById('tenantLogoAvatar');
        if (logoAvatar) {
            logoAvatar.innerHTML = currentBranding.logoDataUrl
                ? imageMarkup(currentBranding.logoDataUrl, detail.displayName || detail.name || 'Tenant logo', 'rounded-circle')
                : '<i class="bx bx-buildings fs-3"></i>';
        }

        const faviconPreview = document.getElementById('tenantFaviconPreview');
        if (faviconPreview) {
            faviconPreview.innerHTML = currentBranding.faviconDataUrl
                ? imageMarkup(currentBranding.faviconDataUrl, 'Fav icon', 'rounded')
                : '<i class="bx bx-image"></i>';
        }
    };

    const renderDefinitionList = (elementId, rows) => {
        const element = document.getElementById(elementId);
        if (!element) return;
        element.innerHTML = rows.map((row) => {
            const label = Array.isArray(row) ? row[0] : row.label;
            const value = Array.isArray(row) ? row[1] : row.value;
            const html = Array.isArray(row) ? null : row.html;
            return `<dt class="col-5 mb-2">${escapeHtml(label)}</dt><dd class="col-7 mb-2 text-break">${html || escapeHtml(value || '-')}</dd>`;
        }).join('');
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

    const timelinePointClass = (eventType) => {
        const normalized = String(eventType || '').toLowerCase();
        if (normalized.includes('created')) return 'timeline-point-primary';
        if (normalized.includes('started') || normalized.includes('updated')) return 'timeline-point-info';
        if (normalized.includes('completed') || normalized.includes('reactivated')) return 'timeline-point-success';
        if (normalized.includes('suspended') || normalized.includes('queued')) return 'timeline-point-warning';
        if (normalized.includes('deleted') || normalized.includes('failed')) return 'timeline-point-danger';
        return 'timeline-point-secondary';
    };

    const renderRecentActivityTimeline = (elementId, activities, emptyText) => {
        const element = document.getElementById(elementId);
        if (!element) return;

        if (!activities || activities.length === 0) {
            element.innerHTML = `<div class="text-muted">${escapeHtml(emptyText || '-')}</div>`;
            return;
        }

        element.innerHTML = `<ul class="timeline timeline-outline mb-0">
            ${activities.map((activity) => {
            const actor = formatActor(activity.actor || 'system');
            const message = activity.message || '';
            const happenedAt = activity.at || activity.At;
            return `<li class="timeline-item timeline-item-transparent border-dashed">
                    <span class="timeline-point ${timelinePointClass(activity.eventType)}"></span>
                    <div class="timeline-event">
                        <div class="timeline-header mb-3">
                            <h6 class="mb-0">${escapeHtml(activity.eventType || '-')}</h6>
                            <small class="text-body-secondary" title="${escapeHtml(formatDate(happenedAt))}">${escapeHtml(formatRelativeTime(happenedAt))}</small>
                        </div>
                        <p class="mb-2">${escapeHtml(message || '-')}</p>
                        <div class="d-flex align-items-center mb-2">
                            <div class="badge bg-label-primary d-flex align-items-center">
                                <i class="icon-base bx bx-user-circle me-2"></i>
                                <span>${escapeHtml(actor)}</span>
                            </div>
                        </div>
                    </div>
                </li>`;
        }).join('')}
        </ul>`;
    };

    const renderOverviewRecentActivity = () => {
        const visibleActivities = recentActivityExpanded
            ? recentActivities
            : recentActivities.slice(0, 5);
        renderRecentActivityTimeline('overviewRecentActivity', visibleActivities, L.DtEmptyTable);

        const viewAllButton = document.getElementById('btnViewAllRecentActivity');
        if (!viewAllButton) return;

        viewAllButton.classList.toggle('d-none', recentActivities.length <= 5);
        viewAllButton.innerHTML = recentActivityExpanded
            ? 'Show less <i class="icon-base bx bx-arrow-to-top"></i>'
            : 'View all <i class="icon-base bx bx-arrow-to-right"></i>';
        viewAllButton.setAttribute('aria-expanded', recentActivityExpanded ? 'true' : 'false');
    };

    const loadOverview = async () => {
        const detail = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}`);

        document.getElementById('detailsTitle').innerText = detail.displayName || detail.name || '-';
        document.getElementById('detailDisplayName').innerText = detail.displayName || detail.name || '-';
        document.getElementById('detailCodeSlug').innerText = `${detail.code || '-'} / ${detail.slug || '-'}`;
        renderBranding(detail);
        const statusEl = document.getElementById('detailStatus');
        statusEl.className = `badge ${statusBadgeClass(detail.status)}`;
        statusEl.innerText = detail.status || '-';
        syncLifecycleActions(detail.status);
        document.getElementById('detailProvisioning').innerText = detail.provisioningStatus || '-';
        document.getElementById('detailDomain').innerText = detail.domain || '-';
        document.getElementById('detailTenantType').innerHTML = badgeMarkup(detail.tenantType, tenantTypeBadgeClass(detail.tenantType));
        document.getElementById('detailPlan').innerText = [detail.planName, detail.planCode].filter(Boolean).join(' / ') || detail.plan || '-';
        const subscriptionEl = document.getElementById('detailSubscriptionStatus');
        if (subscriptionEl) {
            const normalizedStatus = normalizeSubscriptionStatus(detail.subscriptionStatus);
            subscriptionEl.className = `badge ${subscriptionBadgeClass(normalizedStatus.key)}`;
            subscriptionEl.innerText = normalizedStatus.label;
        }
        document.getElementById('detailCountry').innerText = detail.country || '-';

        renderDefinitionList('contactInformationList', [
            [L.LegalName || 'Legal Name', detail.legalName],
            [L.TaxNumber || 'Tax Number', detail.taxNumber],
            [L.Industry || 'Industry', detail.industry],
            [L.ContactPerson || 'Contact Person', detail.contactPerson],
            [L.ContactEmail || 'Contact Email', detail.contactEmail],
            [L.ContactPhone || 'Contact Phone', detail.contactPhone]
        ]);

        renderDefinitionList('basicInformationList', [
            [L.Domain || 'Domain', detail.domain],
            { label: L.TenantType || 'Tenant Type', html: badgeMarkup(detail.tenantType, tenantTypeBadgeClass(detail.tenantType)) },
            [L.SubscriptionPlan || 'Subscription Plan', [detail.planName, detail.planCode].filter(Boolean).join(' / ') || detail.plan],
            { label: L.SubscriptionStatus || 'Subscription Status', html: (() => {
                const normalizedStatus = normalizeSubscriptionStatus(detail.subscriptionStatus);
                return badgeMarkup(normalizedStatus.label, subscriptionBadgeClass(normalizedStatus.key));
            })() },
            [L.Country || 'Country', detail.country],
            [L.DefaultTimezone || 'Timezone', detail.defaultTimezone],
            [L.DefaultLanguage || 'Language', detail.defaultLanguage],
            [L.DefaultCurrency || 'Currency', detail.defaultCurrency],
            [L.TrialStartDate || 'Trial Start Date', formatDate(detail.trialStartDateUtc)],
            [L.TrialEndDate || 'Trial End Date', formatDate(detail.trialEndDateUtc)],
            [L.Created || 'Created', formatDate(detail.createdAt)],
            ['Created By', formatActor(detail.createdBy)]
        ]);

        recentActivities = detail.recentActivity || [];
        renderOverviewRecentActivity();

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

        loadOverviewModules().catch(() => {
            renderListGroup('overviewEnabledModules', [], L.ErrorOccurred || 'Error occurred.');
        });

        return detail;
    };

    const moduleRows = (data) => (data.entitlements || [])
        .filter((item) => item.enabled)
        .slice(0, 6)
        .map((item) => ({
            title: item.moduleName || item.moduleKey,
            subtitle: item.moduleKey,
            badge: item.source
        }));

    const loadOverviewModules = async () => {
        const data = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/modules`);
        renderListGroup('overviewEnabledModules', moduleRows(data), L.DtEmptyTable);
    };

    const showCommercialError = (message) => {
        const element = document.getElementById('commercialSubscriptionError');
        if (!element) return;
        element.textContent = message || '';
        element.classList.toggle('d-none', !message);
    };

    const setCommercialLoading = (loading) => {
        document.getElementById('commercialSubscriptionLoading')?.classList.toggle('d-none', !loading);
    };

    const renderCommercialState = (state) => {
        document.getElementById('commercialSubscriptionEmpty')?.classList.toggle('d-none', state !== 'empty');
        document.getElementById('commercialSubscriptionContent')?.classList.toggle('d-none', state !== 'content');
    };

    const quotaLabel = (key, fallback) => ({
        'users.max': L.TenantCommercialQuotaUsers || 'Users',
        'storage.gb.max': L.TenantCommercialQuotaStorage || 'Storage',
        'api.calls.per.month': L.TenantCommercialQuotaApiCallsThisMonth || 'API Calls This Month',
        'modules.max': L.TenantCommercialQuotaEnabledModules || 'Enabled Modules'
    }[String(key || '').toLowerCase()] || fallback || L.Quotas || 'Quota');

    const quotaStatusLabel = (status) => ({
        healthy: L.TenantCommercialQuotaStatusHealthy || 'Healthy',
        warning: L.TenantCommercialQuotaStatusWarning || 'Warning',
        atLimit: L.TenantCommercialQuotaStatusAtLimit || 'At limit',
        overLimit: L.TenantCommercialQuotaStatusOverLimit || 'Over limit',
        configurationMissing: L.TenantCommercialQuotaStatusConfigurationMissing || 'Configuration missing',
        subscriptionInactive: L.TenantCommercialQuotaStatusSubscriptionInactive || 'Subscription inactive'
    }[status] || L.TenantCommercialQuotaStatusHealthy || 'Healthy');

    const quotaStatusBadgeClass = (status) => ({
        healthy: 'bg-label-success',
        warning: 'bg-label-warning',
        atLimit: 'bg-label-danger',
        overLimit: 'bg-label-danger',
        configurationMissing: 'bg-label-secondary',
        subscriptionInactive: 'bg-label-warning'
    }[status] || 'bg-label-secondary');

    const quotaProgressClass = (status) => ({
        healthy: 'bg-success',
        warning: 'bg-warning',
        atLimit: 'bg-danger',
        overLimit: 'bg-danger',
        configurationMissing: 'bg-secondary',
        subscriptionInactive: 'bg-warning'
    }[status] || 'bg-secondary');

    const formatNumber = (value, fractionDigits) => {
        const number = Number(value);
        if (!Number.isFinite(number)) return '-';
        return new Intl.NumberFormat(undefined, {
            maximumFractionDigits: fractionDigits,
            minimumFractionDigits: 0
        }).format(number);
    };

    const formatQuotaValue = (item, value) => {
        const key = String(item.quotaKey || item.QuotaKey || '').toLowerCase();
        if (key === 'storage.gb.max') return `${formatNumber(value, 2)} GB`;
        return formatNumber(value, 0);
    };

    const safePercent = (value) => {
        const number = Number(value);
        if (!Number.isFinite(number)) return 0;
        return Math.max(0, Math.min(100, number));
    };

    const setQuotaState = (state, message) => {
        const loading = document.getElementById('tenantQuotaLoading');
        const empty = document.getElementById('tenantQuotaEmpty');
        const error = document.getElementById('tenantQuotaError');
        const unauthorized = document.getElementById('tenantQuotaUnauthorized');
        const rows = document.getElementById('tenantQuotaRows');

        loading?.classList.toggle('d-none', state !== 'loading');
        empty?.classList.toggle('d-none', state !== 'empty');
        unauthorized?.classList.toggle('d-none', state !== 'unauthorized');
        rows?.classList.toggle('d-none', state !== 'ready');
        if (error) {
            error.textContent = state === 'error' ? (message || L.TenantCommercialQuotaError || 'Quota status could not be loaded.') : '';
            error.classList.toggle('d-none', state !== 'error');
        }
    };

    const renderQuotaRows = (items) => {
        const rows = document.getElementById('tenantQuotaRows');
        if (!rows) return;

        rows.innerHTML = items.map((item) => {
            const key = item.quotaKey || item.QuotaKey;
            const status = item.status || item.Status || 'healthy';
            const currentValue = item.currentValue ?? item.CurrentValue;
            const limitValue = item.limitValue ?? item.LimitValue;
            const usagePercent = item.usagePercent ?? item.UsagePercent;
            const percent = safePercent(usagePercent);
            const hasLimit = Number(limitValue) > 0;
            const source = item.overrideSource || item.OverrideSource || item.source || item.Source || '';
            const periodStart = item.periodStart || item.PeriodStart;
            const periodEnd = item.periodEnd || item.PeriodEnd;
            const isPeriodBased = String(key || '').toLowerCase() === 'api.calls.per.month';
            const statusText = hasLimit ? quotaStatusLabel(status) : quotaStatusLabel('configurationMissing');
            const normalizedStatus = hasLimit ? status : 'configurationMissing';
            const periodMarkup = isPeriodBased
                ? `<div class="small text-muted mt-2">${escapeHtml(L.PeriodStart || 'Period Start')}: ${escapeHtml(formatSubscriptionTableDate(periodStart))} · ${escapeHtml(L.PeriodEnd || 'Period End')}: ${escapeHtml(formatSubscriptionTableDate(periodEnd))}</div>`
                : '';
            const noteMarkup = item.apiCallsMvpNote || item.ApiCallsMvpNote
                ? `<div class="small text-muted mt-2">${escapeHtml(L.TenantCommercialQuotaApiCallsMvpNote || '')}</div>`
                : '';
            const sourceMarkup = source
                ? `<div class="small text-muted mt-2">${escapeHtml(L.Source || 'Source')}: ${escapeHtml(source)}</div>`
                : '';

            return `<div class="col-12 col-md-6">
                <div class="border rounded p-3 h-100">
                    <div class="d-flex align-items-start justify-content-between gap-3 mb-3">
                        <div>
                            <div class="fw-medium">${escapeHtml(quotaLabel(key, item.displayLabel || item.DisplayLabel))}</div>
                            <small class="text-muted">${escapeHtml(formatQuotaValue(item, currentValue))} / ${escapeHtml(hasLimit ? formatQuotaValue(item, limitValue) : '-')}</small>
                        </div>
                        <span class="badge ${escapeHtml(quotaStatusBadgeClass(normalizedStatus))}">${escapeHtml(statusText)}</span>
                    </div>
                    <div class="d-flex align-items-center justify-content-between small text-muted mb-1">
                        <span>${escapeHtml(formatNumber(usagePercent, 1))}%</span>
                        <span>${escapeHtml(L.Status || 'Status')}: ${escapeHtml(statusText)}</span>
                    </div>
                    <div class="progress" style="height: 8px;" role="progressbar" aria-valuenow="${escapeHtml(percent)}" aria-valuemin="0" aria-valuemax="100">
                        <div class="progress-bar ${escapeHtml(quotaProgressClass(normalizedStatus))}" style="width: ${escapeHtml(percent)}%;"></div>
                    </div>
                    ${periodMarkup}
                    ${sourceMarkup}
                    ${noteMarkup}
                </div>
            </div>`;
        }).join('');
    };

    const readQuotaPayload = async (response) => {
        const text = await response.text();
        if (!text) return null;
        try {
            return JSON.parse(text);
        } catch (error) {
            return null;
        }
    };

    const loadTenantQuotaGovernance = async () => {
        setQuotaState('loading');
        try {
            const response = await fetch(`/Platform/Tenants/${encodeURIComponent(tenantId)}/QuotaStatus`, {
                credentials: 'same-origin',
                headers: { Accept: 'application/json' }
            });
            const payload = await readQuotaPayload(response);
            const state = payload?.state || payload?.State;
            const message = payload?.message || payload?.Message;
            const items = payload?.items || payload?.Items || [];
            const responseTenantId = String(payload?.tenantId || payload?.TenantId || '').toLowerCase();

            if (!response.ok) {
                if (response.status === 401 || response.status === 403 || state === 'unauthorized') {
                    setQuotaState('unauthorized');
                    return;
                }

                setQuotaState('error', message || L.TenantCommercialQuotaError || 'Quota status could not be loaded.');
                return;
            }

            if (responseTenantId && responseTenantId !== String(tenantId || '').toLowerCase()) {
                setQuotaState('error', L.TenantCommercialQuotaError || 'Quota status could not be loaded.');
                return;
            }

            if (!Array.isArray(items) || items.length === 0 || state === 'empty') {
                setQuotaState('empty');
                return;
            }

            renderQuotaRows(items);
            document.getElementById('tenantQuotaLoadedAt').textContent = formatSubscriptionTableDate(payload.loadedAtUtc || payload.LoadedAtUtc);
            setQuotaState('ready');
        } catch (error) {
            setQuotaState('error', L.TenantCommercialQuotaError || 'Quota status could not be loaded.');
        }
    };

    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';
    const normalizeArray = (value) => Array.isArray(value) ? value.map(String).filter(Boolean).sort() : [];

    const normalizeSubscriptionStatus = (status) => {
        const raw = normalizeString(String(status || ''));
        const normalizedRaw = raw.startsWith('SubscriptionStatus') ? raw.replace('SubscriptionStatus', '') : raw;
        const key = ({
            0: 'PendingProvisioning',
            1: 'Trialing',
            2: 'Active',
            3: 'PastDue',
            4: 'Cancelled',
            5: 'Expired',
            6: 'Suspended',
            7: 'TrialExpired'
        })[normalizedRaw] || normalizedRaw;

        const fallbackLabels = {
            PendingProvisioning: 'Pending Provisioning',
            Trialing: 'Trialing',
            Active: L.Active || 'Active',
            PastDue: 'Past Due',
            Cancelled: 'Cancelled',
            Expired: 'Expired',
            Suspended: 'Suspended',
            TrialExpired: 'Trial Expired'
        };
        const localized = ({
            PendingProvisioning: L.SubscriptionStatusPendingProvisioning,
            Trialing: L.SubscriptionStatusTrialing,
            Active: L.SubscriptionStatusActive || L.Active,
            PastDue: L.SubscriptionStatusPastDue,
            Cancelled: L.SubscriptionStatusCancelled,
            Expired: L.SubscriptionStatusExpired,
            Suspended: L.SubscriptionStatusSuspended,
            TrialExpired: L.SubscriptionStatusTrialExpired
        })[key];
        const label = localized && !String(localized).startsWith('SubscriptionStatus')
            ? localized
            : (fallbackLabels[key] || key || '-');

        return { key, label };
    };

    const formatSubscriptionTableDate = (value) => {
        if (!value) return '-';
        try {
            const date = new Date(value);
            if (Number.isNaN(date.getTime())) return value;
            const parts = new Intl.DateTimeFormat('en-GB', {
                day: '2-digit',
                month: 'short',
                year: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                hour12: false
            }).formatToParts(date).reduce((acc, part) => {
                acc[part.type] = part.value;
                return acc;
            }, {});
            return `${parts.day} ${parts.month} ${parts.year},${parts.hour}:${parts.minute}`;
        } catch (error) {
            return value;
        }
    };

    const subscriptionTimelinePointClass = (status) => {
        const normalized = normalizeSubscriptionStatus(status).key.toLowerCase();
        if (normalized === 'active') return 'timeline-point-success';
        if (normalized === 'trialing' || normalized === 'pendingprovisioning') return 'timeline-point-info';
        if (normalized === 'pastdue' || normalized === 'suspended' || normalized === 'trialexpired') return 'timeline-point-warning';
        if (normalized === 'cancelled' || normalized === 'expired') return 'timeline-point-danger';
        return 'timeline-point-secondary';
    };

    const mountSubscriptionInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        if (!host) return;
        const filterBtn = document.querySelector('.dt-current-subscription-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const syncSubscriptionMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;

        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');

        if (!$summary.length) $summary = $('<span class="dt-inline-filter-multi__summary"></span>').prependTo($selection);
        if (!$actions.length) $actions = $('<span class="dt-inline-filter-multi__actions"></span>').appendTo($selection);
        if (!$count.length) $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>').appendTo($actions);
        if (!$arrow.length) $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>').appendTo($selection);

        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((item) => normalizeString(item.text)).filter(Boolean);

        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();

        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (event) => {
                event.preventDefault();
                event.stopPropagation();
                $select.val(null).trigger('change');
            });
            $actions.append($clearBtn);
        }
    };

    const initSubscriptionFilterControls = () => {
        if (!window.jQuery || !$.fn.select2) return;

        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            const isMultiple = $select.prop('multiple');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: isMultiple ? 'dt-inline-filter-multi' : undefined,
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: !isMultiple,
                allowClear: !isMultiple
            });

            if (isMultiple) {
                $select.off('change.select2-summary').on('change.select2-summary', () => syncSubscriptionMultiSelectSummary($select));
                requestAnimationFrame(() => syncSubscriptionMultiSelectSummary($select));
            }
        });
    };

    const bindSubscriptionInlineFilterToggle = () => {
        const button = document.querySelector('.dt-current-subscription-filter-btn');
        const collapseEl = document.getElementById(subscriptionFilterCollapseId);
        if (!button || !collapseEl || button.dataset.inlineFilterBound) return;
        button.dataset.inlineFilterBound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => button.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => button.setAttribute('aria-expanded', 'false'));
        button.addEventListener('click', (event) => {
            event.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false });
            if (collapseEl.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    const getStagedSubscriptionFilters = () => ({
        status: $('#subscriptionFilterStatus').val() || [],
        cancelAtPeriodEnd: document.getElementById('subscriptionFilterCancelAtPeriodEnd')?.value || ''
    });

    const getAppliedSubscriptionFilterCount = () =>
        normalizeArray(subscriptionAppliedFilters.status).length + (normalizeString(subscriptionAppliedFilters.cancelAtPeriodEnd) ? 1 : 0);

    const applySubscriptionFilterValues = (api, filters) => {
        const statusRegex = normalizeArray(filters.status).join('|');
        const cancelValue = normalizeString(filters.cancelAtPeriodEnd);
        api.column('status:name').search(statusRegex, true, false);
        api.column('cancelAtPeriodEnd:name').search(cancelValue ? `^${cancelValue}$` : '', true, false);
    };

    const bindSubscriptionFilters = (api) => {
        const applyButton = document.getElementById('btnSubscriptionFilterApply');
        const resetButton = document.getElementById('btnSubscriptionFilterReset');

        if (applyButton && !applyButton.dataset.bound) {
            applyButton.dataset.bound = '1';
            applyButton.addEventListener('click', () => {
                subscriptionAppliedFilters = getStagedSubscriptionFilters();
                applySubscriptionFilterValues(api, subscriptionAppliedFilters);
                api.draw();
                window.DtDefaults?.updateVisualState?.(api, getAppliedSubscriptionFilterCount());
                bootstrap.Collapse.getInstance(document.getElementById(subscriptionFilterCollapseId))?.hide();
            });
        }

        if (resetButton && !resetButton.dataset.bound) {
            resetButton.dataset.bound = '1';
            resetButton.addEventListener('click', (event) => {
                event.preventDefault();
                subscriptionAppliedFilters = { status: [], cancelAtPeriodEnd: '' };
                $('#subscriptionFilterStatus').val(null).trigger('change');
                $('#subscriptionFilterCancelAtPeriodEnd').val('').trigger('change');
                applySubscriptionFilterValues(api, subscriptionAppliedFilters);
                api.search('');
                api.draw();
                window.DtDefaults?.updateVisualState?.(api, 0);
            });
        }
    };

    const renderCommercialSubscriptionActions = (subscription) => {
        const status = normalizeSubscriptionStatus(subscription?.status).key;
        const actions = [
            { key: 'activate-subscription', visible: ['PendingProvisioning', 'Trialing'].includes(status), icon: 'bx bx-check-circle', text: L.Activate || 'Activate' },
            { key: 'renew-subscription', visible: status === 'Active', icon: 'bx bx-refresh', text: L.Renew || 'Renew' },
            { key: 'cancel-subscription', visible: ['Active', 'Trialing'].includes(status), buttonClass: 'text-danger', icon: 'bx bx-x-circle', text: L.Cancel || 'Cancel' },
            { key: 'suspend-subscription', visible: status === 'Active', icon: 'bx bx-pause-circle', text: L.Suspend || 'Suspend' },
            { key: 'reactivate-subscription', visible: status === 'Suspended', icon: 'bx bx-play-circle', text: L.Reactivate || 'Reactivate' }
        ];
        const visibleActions = actions.filter((action) => action.visible !== false);
        if (!visibleActions.length) return '<span class="text-muted">-</span>';
        return window.DitenDataTable?.renderActions?.(actions) || visibleActions.map((action) =>
            `<button type="button" class="btn btn-sm btn-icon btn-label-secondary me-1" data-action-key="${escapeHtml(action.key)}"><i class="icon-base ${escapeHtml(action.icon)}"></i></button>`
        ).join('');
    };

    const initCommercialSubscriptionTable = () => {
        const table = document.getElementById('dtCurrentSubscription');
        if (!table || commercialSubscriptionDt) return false;

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn dt-current-subscription-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-label': L.Filter,
                    'aria-controls': subscriptionFilterCollapseId,
                    'aria-expanded': 'false',
                    'data-bs-toggle': 'tooltip'
                }
            }
        };

        commercialSubscriptionDt = new DataTable(table, window.DtDefaults.create({
            data: [],
            paging: true,
            searching: true,
            info: true,
            lengthChange: false,
            order: [],
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            buttons: window.DtDefaults.exportButtons(L.AssignPlan || 'Assign Plan', {
                id: 'btnAssignSubscriptionToolbar',
                title: L.AssignPlan || 'Assign Plan',
                'aria-label': L.AssignPlan || 'Assign Plan'
            }, extraButtons, {
                exportColumns: [2, 3, 4, 5, 6, 7, 8],
                colvisColumns: [2, 3, 4, 5, 6, 7, 8],
                showAllColumns: [2, 3, 4, 5, 6, 7, 8]
            }),
            columns: [
                { data: null, name: 'control', defaultContent: '' },
                { data: 'id', name: 'select' },
                { data: 'planName', name: 'plan' },
                { data: 'status', name: 'status' },
                { data: 'cancelAtPeriodEnd', name: 'cancelAtPeriodEnd' },
                { data: 'trialStartDateUtc', name: 'trialStart' },
                { data: 'trialEndDateUtc', name: 'trialEnd' },
                { data: 'currentPeriodStartUtc', name: 'periodStart' },
                { data: 'currentPeriodEndUtc', name: 'periodEnd' },
                { data: null, name: 'actions' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, searchable: false, orderable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data || '')}">` },
                { targets: [4, 7], visible: false },
                {
                    targets: 2,
                    responsivePriority: 2,
                    render: (data, type, full) => {
                        const name = full.planName || '-';
                        const code = full.planCode || '';
                        return `<div><span class="fw-medium text-heading">${escapeHtml(name)}</span>${code ? `<br><small class="text-muted">${escapeHtml(code)}</small>` : ''}</div>`;
                    }
                },
                {
                    targets: 3,
                    render: (data, type) => {
                        const status = normalizeSubscriptionStatus(data);
                        return type === 'display' ? badgeMarkup(status.label, subscriptionBadgeClass(status.key)) : status.key;
                    }
                },
                {
                    targets: 4,
                    render: (data, type) => {
                        const value = data === true || String(data).toLowerCase() === 'true';
                        if (type !== 'display') return value ? 'true' : 'false';
                        return value ? badgeMarkup(L.Yes || 'Yes', 'bg-label-warning') : badgeMarkup(L.No || 'No', 'bg-label-secondary');
                    }
                },
                { targets: [5, 6, 7, 8], render: (data) => data ? `<span>${escapeHtml(formatSubscriptionTableDate(data))}</span>` : '<span class="text-muted">-</span>' },
                { targets: -1, searchable: false, orderable: false, className: 'cell-fit text-end pe-3 all', render: (data, type, full) => renderCommercialSubscriptionActions(full) }
            ],
            initComplete: function () {
                const api = this.api();
                const actionHandlers = {
                    'activate-subscription': () => openSubscriptionActionModal('activate'),
                    'renew-subscription': () => openSubscriptionActionModal('renew'),
                    'cancel-subscription': () => openSubscriptionActionModal('cancel'),
                    'suspend-subscription': () => openSubscriptionActionModal('suspend'),
                    'reactivate-subscription': () => confirmReactivateSubscription()
                };

                window.DitenDataTable?.bindActionDispatcher?.({
                    tableEl: table,
                    getTable: () => commercialSubscriptionDt,
                    onRowAction: actionHandlers
                });
                window.DitenDataTable?.bindBulkSelection?.(table, commercialSubscriptionDt, {});
                mountSubscriptionInlineFilter();
                initSubscriptionFilterControls();
                bindSubscriptionInlineFilterToggle();
                bindSubscriptionFilters(api);
                document.getElementById('btnAssignSubscriptionToolbar')?.addEventListener('click', () => {
                    openSubscriptionActionModal('assign').catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
                });
                if (!window.DitenDataTable?.bindActionDispatcher) {
                    table.addEventListener('click', (event) => {
                        const trigger = event.target.closest('[data-action-key]');
                        if (!trigger) return;
                        const handler = actionHandlers[trigger.getAttribute('data-action-key')];
                        if (handler) handler();
                    });
                }

                api.on('draw.dt search.dt column-visibility.dt column-reorder.dt columns-reordered.dt order.dt', () => {
                    window.DtDefaults?.updateVisualState?.(api, getAppliedSubscriptionFilterCount());
                });
            }
        }));

        return true;
    };

    const mountModuleEntitlementInlineFilter = () => {
        const host = document.getElementById('moduleEntitlementFilterHost');
        const filterBtn = document.querySelector('.dt-module-entitlements-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (!host || !toolbarRow || host.parentElement === toolbarRow.parentElement) return;
        const wrapper = document.createElement('div');
        wrapper.className = 'row px-3 my-0';
        const col = document.createElement('div');
        col.className = 'col-12';
        wrapper.appendChild(col);
        col.appendChild(host);
        toolbarRow.insertAdjacentElement('afterend', wrapper);
    };

    const initModuleEntitlementFilterControls = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#moduleEntitlementFilterHost select.select2').each(function () {
            const $select = $(this);
            const isMultiple = $select.prop('multiple');
            if ($select.data('select2')) return;
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                containerCssClass: isMultiple ? 'dt-inline-filter-multi' : undefined,
                width: 'element',
                closeOnSelect: !isMultiple,
                allowClear: !isMultiple
            });
            if (isMultiple) {
                $select.off('change.module-entitlement-summary').on('change.module-entitlement-summary', () => syncSubscriptionMultiSelectSummary($select));
                requestAnimationFrame(() => syncSubscriptionMultiSelectSummary($select));
            }
        });
    };

    const bindModuleEntitlementInlineFilterToggle = () => {
        const button = document.querySelector('.dt-module-entitlements-filter-btn');
        const collapseEl = document.getElementById(moduleEntitlementFilterCollapseId);
        if (!button || !collapseEl) return;
        collapseEl.addEventListener('shown.bs.collapse', () => button.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => button.setAttribute('aria-expanded', 'false'));
        button.addEventListener('click', () => bootstrap.Collapse.getOrCreateInstance(collapseEl).toggle());
    };

    const getStagedModuleEntitlementFilters = () => ({
        source: $('#moduleEntitlementFilterSource').val() || [],
        access: $('#moduleEntitlementFilterAccess').val() || []
    });

    const getAppliedModuleEntitlementFilterCount = () =>
        moduleEntitlementAppliedFilters.source.length + moduleEntitlementAppliedFilters.access.length;

    const applyModuleEntitlementFilterValues = (api, filters) => {
        api.column('source:name').search(normalizeArray(filters.source).join('|'), true, false);
        api.column('effectiveAccess:name').search(normalizeArray(filters.access).join('|'), true, false);
        api.draw();
    };

    const bindModuleEntitlementFilters = (api) => {
        document.getElementById('btnModuleEntitlementFilterApply')?.addEventListener('click', () => {
            moduleEntitlementAppliedFilters = getStagedModuleEntitlementFilters();
            applyModuleEntitlementFilterValues(api, moduleEntitlementAppliedFilters);
            window.DtDefaults?.updateVisualState?.(api, getAppliedModuleEntitlementFilterCount());
        });

        document.getElementById('btnModuleEntitlementFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            moduleEntitlementAppliedFilters = { source: [], access: [] };
            $('#moduleEntitlementFilterSource').val(null).trigger('change');
            $('#moduleEntitlementFilterAccess').val(null).trigger('change');
            api.search('');
            api.columns().search('');
            api.colReorder?.order?.(Array.from({ length: 12 }, (_, i) => i), true);
            api.order([]);
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, 0);
        });
    };

    const moduleEntitlementAccessBadgeClass = (access) => ({
        Active: 'bg-label-success',
        EnabledByOverride: 'bg-label-success',
        SystemLocked: 'bg-label-info',
        BlockedByOverride: 'bg-label-danger',
        Expired: 'bg-label-warning',
        NoAccess: 'bg-label-secondary'
    }[access] || 'bg-label-secondary');

    const localizeEntitlementSource = (source) => ({
        Plan: L.SourcePlan,
        ManualOverride: L.SourceManualOverride,
        Addon: L.SourceAddon,
        Trial: L.SourceTrial,
        System: L.SourceSystem
    }[source] || source || '-');

    const localizeEffectiveAccess = (access) => ({
        Active: L.AccessActive,
        NoAccess: L.AccessNoAccess,
        Expired: L.AccessExpired,
        BlockedByOverride: L.AccessBlockedByOverride,
        EnabledByOverride: L.AccessEnabledByOverride,
        SystemLocked: L.AccessSystemLocked
    }[access] || access || '-');

    const renderModuleEntitlementActions = (row) => {
        const isProjection = row.isProjectionRow === true;
        const id = row.physicalEntitlementId;
        const source = row.displaySource;
        const actions = [
            { key: 'disable-module-entitlement', visible: isProjection || id, buttonClass: 'text-danger', icon: 'bx bx-block', text: L.Disable || 'Disable' },
            { key: 'enable-module-entitlement', visible: !isProjection && id && row.isEnabled === false, icon: 'bx bx-check-circle', text: L.Enable || 'Enable' },
            { key: 'edit-module-entitlement-expiry', visible: !isProjection && id, icon: 'bx bx-calendar-edit', text: L.EditExpiry || 'Edit Expiry' },
            { key: 'remove-module-entitlement-override', visible: !isProjection && id && source === 'ManualOverride', buttonClass: 'text-danger', icon: 'bx bx-trash', text: L.RemoveManualOverride || 'Remove Override' }
        ];
        const visibleActions = actions.filter((action) => action.visible !== false);
        if (!visibleActions.length) return '<span class="text-muted">-</span>';
        return window.DitenDataTable?.renderActions?.(actions) || visibleActions.map((action) =>
            `<button type="button" class="btn btn-sm btn-icon btn-label-secondary me-1" data-action-key="${escapeHtml(action.key)}"><i class="icon-base ${escapeHtml(action.icon)}"></i></button>`
        ).join('');
    };

    const initModuleEntitlementsTable = () => {
        const table = document.getElementById('dtModuleEntitlements');
        if (!table || moduleEntitlementsDt) return false;

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn dt-module-entitlements-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-label': L.Filter,
                    'aria-controls': moduleEntitlementFilterCollapseId,
                    'aria-expanded': 'false',
                    'data-bs-toggle': 'tooltip'
                }
            }
        };

        moduleEntitlementsDt = new DataTable(table, window.DtDefaults.create({
            ajax: (_data, callback) => {
                fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/module-entitlements`)
                    .then(rows => callback({ data: Array.isArray(rows) ? rows : [] }))
                    .catch(error => {
                        callback({ data: [] });
                        if (!error.authHandled) window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                    });
            },
            paging: true,
            searching: true,
            info: true,
            lengthChange: false,
            order: [[2, 'asc']],
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            buttons: window.DtDefaults.exportButtons(L.AddModuleEntitlement || 'Add Module Entitlement', {
                id: 'btnAddModuleEntitlementToolbar',
                title: L.AddModuleEntitlement || 'Add Module Entitlement',
                'aria-label': L.AddModuleEntitlement || 'Add Module Entitlement'
            }, extraButtons, {
                exportColumns: [2, 3, 4, 5, 6, 7, 8, 9, 10],
                colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9, 10],
                showAllColumns: [2, 3, 4, 5, 6, 7, 8, 9, 10]
            }),
            columns: [
                { data: null, name: 'control', defaultContent: '' },
                { data: 'physicalEntitlementId', name: 'select' },
                { data: 'moduleCode', name: 'moduleCode' },
                { data: 'moduleName', name: 'moduleName' },
                { data: 'displaySource', name: 'source' },
                { data: 'effectiveAccess', name: 'status' },
                { data: 'isEnabled', name: 'enabled' },
                { data: 'expiryDateUtc', name: 'expiryDate' },
                { data: 'effectiveAccess', name: 'effectiveAccess' },
                { data: 'reason', name: 'reason' },
                { data: 'lastUpdatedAtUtc', name: 'lastUpdated' },
                { data: null, name: 'actions' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, searchable: false, orderable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => data ? `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data)}">` : '' },
                { targets: [7, 9, 10], visible: false },
                { targets: 4, render: (data) => badgeMarkup(localizeEntitlementSource(data), data === 'Plan' ? 'bg-label-info' : 'bg-label-primary') },
                { targets: 5, render: (data) => badgeMarkup(localizeEffectiveAccess(data), moduleEntitlementAccessBadgeClass(data)) },
                { targets: 6, className: 'text-center', render: (data) => data === true ? '<i class="icon-base bx bx-check text-success"></i>' : '<i class="icon-base bx bx-x text-danger"></i>' },
                { targets: [7, 10], render: (data) => data ? `<span>${escapeHtml(formatSubscriptionTableDate(data))}</span>` : '<span class="text-muted">-</span>' },
                { targets: -1, searchable: false, orderable: false, className: 'cell-fit text-end pe-3 all', render: (data, type, full) => renderModuleEntitlementActions(full) }
            ],
            initComplete: function () {
                const api = this.api();
                const actionHandlers = {
                    'disable-module-entitlement': (ctx) => disableModuleEntitlement(ctx.row),
                    'enable-module-entitlement': (ctx) => enableModuleEntitlement(ctx.row),
                    'edit-module-entitlement-expiry': (ctx) => openModuleEntitlementExpiryEditor(ctx.row),
                    'remove-module-entitlement-override': (ctx) => removeModuleEntitlementOverride(ctx.row)
                };
                window.DitenDataTable?.bindActionDispatcher?.({
                    tableEl: table,
                    getTable: () => moduleEntitlementsDt,
                    onRowAction: actionHandlers
                });
                window.DitenDataTable?.bindBulkSelection?.(table, moduleEntitlementsDt, {});
                mountModuleEntitlementInlineFilter();
                initModuleEntitlementFilterControls();
                bindModuleEntitlementInlineFilterToggle();
                bindModuleEntitlementFilters(api);
                document.getElementById('btnAddModuleEntitlementToolbar')?.addEventListener('click', openModuleEntitlementOffcanvas);
                api.on('draw.dt search.dt column-visibility.dt column-reorder.dt columns-reordered.dt order.dt', () => {
                    window.DtDefaults?.updateVisualState?.(api, getAppliedModuleEntitlementFilterCount());
                });
            }
        }));

        return true;
    };

    const loadModuleEntitlements = async () => {
        initModuleEntitlementsTable();
        moduleEntitlementsDt?.ajax.reload(null, false);
    };

    const renderCommercialSubscription = (subscription) => {
        commercialSubscription = subscription;
        initCommercialSubscriptionTable();
        commercialSubscriptionDt?.clear().rows.add([subscription]).draw();

        const status = normalizeSubscriptionStatus(subscription.status).key;
        document.getElementById('btnReactivateSubscription')?.classList.toggle('d-none', status !== 'Suspended');
    };

    const renderCommercialHistory = (rows) => {
        const container = document.getElementById('commercialSubscriptionHistoryTimeline');
        if (!container) return;
        if (!rows || rows.length === 0) {
            container.innerHTML = `<div class="text-center text-muted py-4">${escapeHtml(L.DtEmptyTable || '-')}</div>`;
            return;
        }

        container.innerHTML = `<ul class="timeline timeline-outline mb-0">
            ${rows.map((row) => {
                const status = normalizeSubscriptionStatus(row.status);
                const planText = [row.planName, row.planCode].filter(Boolean).join(' / ') || '-';
                const changedAt = row.changedAtUtc || row.changedAt || row.createdAt;
                return `<li class="timeline-item timeline-item-transparent border-dashed">
                    <span class="timeline-point ${subscriptionTimelinePointClass(status.key)}"></span>
                    <div class="timeline-event">
                        <div class="timeline-header mb-3">
                            <h6 class="mb-0">${badgeMarkup(status.label, subscriptionBadgeClass(status.key))} <span class="ms-2">${escapeHtml(planText)}</span></h6>
                            <small class="text-body-secondary" title="${escapeHtml(formatDate(changedAt))}">${escapeHtml(formatSubscriptionTableDate(changedAt))}</small>
                        </div>
                        <div class="row g-2 mb-3">
                            <div class="col-12 col-sm-6">
                                <small class="text-muted d-block">${escapeHtml(L.PeriodStart || 'Period Start')}</small>
                                <span>${escapeHtml(formatSubscriptionTableDate(row.currentPeriodStartUtc))}</span>
                            </div>
                            <div class="col-12 col-sm-6">
                                <small class="text-muted d-block">${escapeHtml(L.PeriodEnd || 'Period End')}</small>
                                <span>${escapeHtml(formatSubscriptionTableDate(row.currentPeriodEndUtc))}</span>
                            </div>
                        </div>
                        <p class="mb-2"><strong>${escapeHtml(L.Reason || 'Reason')}:</strong> ${escapeHtml(row.reason || row.action || '-')}</p>
                        <div class="d-flex align-items-center">
                            <div class="badge bg-label-secondary d-flex align-items-center">
                                <i class="icon-base bx bx-user-circle me-2"></i>
                                <span>${escapeHtml(formatActor(row.changedBy || 'system'))}</span>
                            </div>
                        </div>
                    </div>
                </li>`;
            }).join('')}
        </ul>`;
    };

    const loadCommercialSubscription = async () => {
        setCommercialLoading(true);
        showCommercialError('');
        try {
            const subscription = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/subscription`);
            renderCommercialSubscription(subscription);
            renderCommercialState('content');
        } catch (error) {
            if (error.authHandled) return;
            if (String(error.message || '').includes('404')) {
                commercialSubscription = null;
                commercialSubscriptionDt?.clear().draw();
                renderCommercialState('empty');
            } else {
                renderCommercialState('empty');
                showCommercialError(L.ErrorOccurred || 'Error occurred.');
            }
        } finally {
            setCommercialLoading(false);
        }

        try {
            const history = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/subscription/history`);
            renderCommercialHistory(Array.isArray(history) ? history : []);
        } catch (error) {
            renderCommercialHistory([]);
        }
    };

    const loadActiveSubscriptionPlans = async () => {
        if (activeSubscriptionPlans.length > 0) return activeSubscriptionPlans;
        activeSubscriptionPlans = await fetchJson(`${apiBase}/subscription-plans/active`) || [];
        return activeSubscriptionPlans;
    };

    const loadAvailableModules = async () => {
        if (availableModules.length > 0) return availableModules;
        availableModules = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/module-entitlements/available-modules`) || [];
        return availableModules;
    };

    const populateModuleEntitlementModules = async (selectedCode) => {
        const select = document.getElementById('moduleEntitlementModule');
        if (!select) return;
        const modules = await loadAvailableModules();
        select.innerHTML = modules.map((module) => {
            const code = module.moduleCode || module.ModuleCode || '';
            const name = module.displayName || module.moduleName || module.DisplayName || module.ModuleName || code;
            return `<option value="${escapeHtml(code)}" ${code === selectedCode ? 'selected' : ''}>${escapeHtml([name, code].filter(Boolean).join(' / '))}</option>`;
        }).join('');
    };

    const openModuleEntitlementOffcanvas = async () => {
        const subStatus = normalizeSubscriptionStatus(commercialSubscription?.status).key;
        if (subStatus !== 'Active') {
            window.showToast?.(L.ActivateSubscriptionFirst || 'Activate the tenant subscription before adding module entitlements.', 'warning');
            return;
        }

        const form = document.getElementById('moduleEntitlementForm');
        if (!form) return;
        form.reset();
        form.classList.remove('was-validated');
        document.getElementById('moduleEntitlementId').value = '';
        document.getElementById('moduleEntitlementRowVersion').value = '';
        document.getElementById('moduleEntitlementEnabled').checked = true;
        await populateModuleEntitlementModules();
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasModuleEntitlement')).show();
    };

    const readModuleEntitlementPayload = () => ({
        moduleCode: document.getElementById('moduleEntitlementModule')?.value || '',
        source: document.getElementById('moduleEntitlementSource')?.value || 'Addon',
        isEnabled: document.getElementById('moduleEntitlementEnabled')?.checked === true,
        expiryDateUtc: document.getElementById('moduleEntitlementExpiry')?.value
            ? new Date(`${document.getElementById('moduleEntitlementExpiry').value}T23:59:59Z`).toISOString()
            : null,
        reason: normalizeString(document.getElementById('moduleEntitlementReason')?.value)
    });

    const saveModuleEntitlement = async () => {
        const form = document.getElementById('moduleEntitlementForm');
        if (!form) return;
        form.classList.add('was-validated');

        const payload = readModuleEntitlementPayload();
        const reason = document.getElementById('moduleEntitlementReason');
        const reasonRequired = payload.source === 'ManualOverride' || payload.isEnabled === false;
        reason?.toggleAttribute('required', reasonRequired);
        if (!form.checkValidity()) return;

        await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/module-entitlements`, {
            method: 'POST',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasModuleEntitlement'))?.hide();
        window.showToast?.(L.RecordSaved || 'Record saved.', 'success');
        moduleEntitlementsDt?.ajax.reload(null, false);
    };

    const disableModuleEntitlement = (row) => {
        const run = async (reason) => {
            await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/module-entitlements/disable`, {
                method: 'POST',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    moduleCode: row.moduleCode,
                    physicalEntitlementId: row.physicalEntitlementId,
                    reason: reason || row.reason || L.ManualOverrideReason || 'Manual override',
                    rowVersion: row.rowVersion || null
                })
            });
            moduleEntitlementsDt?.ajax.reload(() => window.showToast?.(L.RecordSaved || 'Record saved.', 'success'), false);
        };

        window.showConfirm?.(L.AreYouSure || 'Are you sure?', run, {
            entityName: row.moduleName || row.moduleCode,
            type: 'danger',
            confirmButtonText: L.Disable || 'Disable',
            showInput: true,
            inputPlaceholder: L.ReasonRequired || 'Reason is required.'
        });
    };

    const enableModuleEntitlement = async (row) => {
        if (!row?.physicalEntitlementId) return;
        await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/module-entitlements/${encodeURIComponent(row.physicalEntitlementId)}/enable`, {
            method: 'POST',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify(row.rowVersion || null)
        });
        moduleEntitlementsDt?.ajax.reload(() => window.showToast?.(L.RecordSaved || 'Record saved.', 'success'), false);
    };

    const openModuleEntitlementExpiryEditor = (row) => {
        const current = row.expiryDateUtc ? new Date(row.expiryDateUtc).toISOString().slice(0, 10) : '';
        const nextValue = window.prompt(L.ExpiryDate || 'Expiry Date', current);
        if (nextValue === null || !row?.physicalEntitlementId) return;
        fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/module-entitlements/${encodeURIComponent(row.physicalEntitlementId)}/expiry`, {
            method: 'PATCH',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify({
                expiryDateUtc: nextValue ? new Date(`${nextValue}T23:59:59Z`).toISOString() : null,
                reason: row.reason || null,
                rowVersion: row.rowVersion || null
            })
        }).then(() => {
            moduleEntitlementsDt?.ajax.reload(() => window.showToast?.(L.RecordSaved || 'Record saved.', 'success'), false);
        }).catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
    };

    const removeModuleEntitlementOverride = (row) => {
        if (!row?.physicalEntitlementId) return;
        const run = async () => {
            await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/module-entitlements/${encodeURIComponent(row.physicalEntitlementId)}/manual-override`, {
                method: 'DELETE',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify({ rowVersion: row.rowVersion || null })
            });
            moduleEntitlementsDt?.ajax.reload(() => window.showToast?.(L.RecordDeleted || 'Record deleted.', 'success'), false);
        };
        window.showConfirm?.(L.AreYouSure || 'Are you sure?', run, {
            entityName: row.moduleName || row.moduleCode,
            type: 'danger',
            confirmButtonText: L.Delete || 'Delete'
        });
    };

    const openSubscriptionActionModal = async (action) => {
        const modalEl = document.getElementById('subscriptionActionModal');
        const form = document.getElementById('subscriptionActionForm');
        if (!modalEl || !form) return;

        form.classList.remove('was-validated');
        document.getElementById('subscriptionActionType').value = action;
        document.getElementById('subscriptionActionTitle').innerText = ({
            assign: L.AssignPlan || 'Assign Plan',
            activate: L.Activate || 'Activate',
            renew: L.Renew || 'Renew',
            cancel: L.Cancel || 'Cancel',
            suspend: L.Suspend || 'Suspend',
            reactivate: L.Reactivate || 'Reactivate'
        })[action] || action;

        const isAssign = action === 'assign';
        const needsReason = ['cancel', 'suspend'].includes(action);
        const needsPeriod = ['activate', 'renew'].includes(action);
        document.getElementById('subscriptionPlanFields')?.classList.toggle('d-none', !isAssign);
        document.getElementById('subscriptionReasonField')?.classList.toggle('d-none', !needsReason);
        document.getElementById('subscriptionPeriodField')?.classList.toggle('d-none', !needsPeriod);
        document.getElementById('subscriptionCancelAtPeriodEndWrap')?.classList.toggle('d-none', action !== 'cancel');
        document.getElementById('subscriptionReason')?.toggleAttribute('required', needsReason);
        document.getElementById('subscriptionActionPeriodEnd')?.toggleAttribute('required', needsPeriod);
        document.getElementById('subscriptionPeriodEnd')?.toggleAttribute('required', isAssign && document.getElementById('subscriptionIsTrial')?.checked !== true);

        if (isAssign) {
            const plans = await loadActiveSubscriptionPlans();
            const select = document.getElementById('subscriptionPlanSelect');
            if (select) {
                select.innerHTML = `<option value="">${escapeHtml(L.SelectPlan || 'Select plan')}</option>${plans.map((plan) =>
                    `<option value="${escapeHtml(plan.id)}" data-trial="${plan.isTrialPlan === true}" data-days="${escapeHtml(plan.trialDurationDays || '')}">${escapeHtml([plan.name, plan.code].filter(Boolean).join(' / '))}</option>`
                ).join('')}`;
            }
        }

        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    };

    const dateInputToIso = (id) => {
        const value = document.getElementById(id)?.value;
        return value ? new Date(`${value}T00:00:00Z`).toISOString() : null;
    };

    const confirmReactivateSubscription = () => {
        if (!commercialSubscription?.id) return;
        const planName = [commercialSubscription.planName, commercialSubscription.planCode].filter(Boolean).join(' / ') || L.CurrentSubscription || 'Current Subscription';
        const run = async () => {
            try {
                await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/commercial/subscription/${encodeURIComponent(commercialSubscription.id)}/reactivate`, {
                    method: 'POST',
                    headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        reason: '',
                        rowVersion: commercialSubscription.rowVersion
                    })
                });
                window.showToast?.(L.RecordSaved || 'Record saved.', 'success');
                await loadCommercialSubscription();
                await loadOverview();
            } catch (error) {
                window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error');
            }
        };

        if (window.showConfirm) {
            window.showConfirm(L.AreYouSure || 'Are you sure?', run, {
                entityName: planName,
                type: 'danger',
                confirmButtonText: L.Reactivate || 'Reactivate'
            });
            return;
        }

        run();
    };

    const submitSubscriptionAction = async () => {
        const form = document.getElementById('subscriptionActionForm');
        if (!form) return;
        const action = document.getElementById('subscriptionActionType')?.value;

        form.classList.add('was-validated');
        if (action === 'assign' && !document.getElementById('subscriptionPlanSelect')?.value) return;
        if (action === 'assign' && document.getElementById('subscriptionIsTrial')?.checked !== true && !document.getElementById('subscriptionPeriodEnd')?.value) return;
        if (['cancel', 'suspend'].includes(action) && !document.getElementById('subscriptionReason')?.value.trim()) return;
        if (['activate', 'renew'].includes(action) && !document.getElementById('subscriptionActionPeriodEnd')?.value) return;

        let url = `${apiBase}/${encodeURIComponent(tenantId)}/commercial/subscription`;
        let payload;
        if (action === 'assign') {
            const isTrial = document.getElementById('subscriptionIsTrial')?.checked === true;
            payload = {
                planId: document.getElementById('subscriptionPlanSelect').value,
                isTrial,
                trialEndDateUtc: isTrial ? dateInputToIso('subscriptionTrialEnd') : null,
                currentPeriodStartUtc: isTrial ? null : new Date().toISOString(),
                currentPeriodEndUtc: isTrial ? null : dateInputToIso('subscriptionPeriodEnd'),
                source: 'platform-admin'
            };
        } else {
            if (!commercialSubscription?.id) return;
            url = `${url}/${encodeURIComponent(commercialSubscription.id)}/${action}`;
            if (action === 'activate') {
                payload = {
                    currentPeriodStartUtc: new Date().toISOString(),
                    currentPeriodEndUtc: dateInputToIso('subscriptionActionPeriodEnd'),
                    rowVersion: commercialSubscription.rowVersion
                };
            } else if (action === 'renew') {
                payload = {
                    newPeriodEndUtc: dateInputToIso('subscriptionActionPeriodEnd'),
                    rowVersion: commercialSubscription.rowVersion
                };
            } else if (action === 'cancel') {
                payload = {
                    cancellationReason: document.getElementById('subscriptionReason').value.trim(),
                    cancelAtPeriodEnd: document.getElementById('subscriptionCancelAtPeriodEnd')?.checked === true,
                    rowVersion: commercialSubscription.rowVersion
                };
            } else if (action === 'suspend') {
                payload = {
                    reason: document.getElementById('subscriptionReason').value.trim(),
                    rowVersion: commercialSubscription.rowVersion
                };
            } else {
                payload = {
                    reason: '',
                    rowVersion: commercialSubscription.rowVersion
                };
            }
        }

        await fetchJson(url, {
            method: 'POST',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        bootstrap.Modal.getInstance(document.getElementById('subscriptionActionModal'))?.hide();
        window.showToast?.(L.RecordSaved || 'Record saved.', 'success');
        await loadCommercialSubscription();
        await loadOverview();
    };

    const loadUsers = async () => {
        const data = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/users/summary`);
        const element = document.getElementById('usersSummary');
        if (!element) return;
        const cards = [
            ['Total Users', data.totalUsers, 'bx-group', 'bg-label-primary'],
            ['Active Users', data.activeUsers, 'bx-user-check', 'bg-label-success'],
            ['Pending Invitations', data.pendingInvitations, 'bx-envelope', 'bg-label-warning']
        ];
        element.innerHTML = cards.map(([label, value, icon, color]) => `<div class="col-12 col-md-4">
            <div class="d-flex align-items-center border rounded p-3 h-100">
                <span class="badge ${color} me-3"><i class="icon-base bx ${icon}"></i></span>
                <div>
                    <div class="h5 mb-0">${escapeHtml(value ?? 0)}</div>
                    <small class="text-muted">${escapeHtml(label)}</small>
                </div>
            </div>
        </div>`).join('');
    };

    const adminUserStatusBadge = (status) => ({
        Active: 'bg-label-success',
        Invited: 'bg-label-info',
        PendingInvitation: 'bg-label-warning',
        Disabled: 'bg-label-secondary'
    }[status] || 'bg-label-secondary');

    const renderAdminUserActions = (user) => {
        const rowJson = JSON.stringify(user);
        return window.DitenDataTable?.renderActions?.([
            {
                key: 'delete-admin-user',
                buttonClass: 'text-danger me-1',
                icon: 'bx bx-trash',
                attrs: {
                    'data-id': user.id,
                    'data-json': rowJson,
                    'data-name': user.name || user.email
                }
            },
            {
                key: 'edit-admin-user',
                icon: 'bx bx-edit',
                text: L.Edit || 'Edit',
                attrs: {
                    'data-id': user.id,
                    'data-json': rowJson
                }
            },
            {
                key: 'invite-admin-user',
                visible: user.status !== 'Active',
                icon: 'bx bx-send',
                text: 'Invite',
                attrs: {
                    'data-id': user.id,
                    'data-name': user.name || user.email
                }
            }
        ]) || '';
    };

    const initAdminUsersTable = () => {
        const table = document.getElementById('dtTenantAdminUsers');
        if (!table) return false;
        if (adminUsersDt) return false;

        adminUsersDt = new DataTable(table, window.DtDefaults.create({
            ajax: {
                url: `${apiBase}/${encodeURIComponent(tenantId)}/admin-users`,
                type: 'GET',
                headers: getAuthHeaders(),
                dataSrc: (json) => Array.isArray(unwrap(json)) ? unwrap(json) : []
            },
            order: [[0, 'asc']],
            columns: [
                { data: 'name', name: 'name' },
                { data: 'email', name: 'email' },
                { data: 'status', name: 'status' },
                { data: null, name: 'actions' }
            ],
            columnDefs: [
                {
                    targets: 0,
                    render: (data, type, full) => `<span class="fw-medium text-heading">${escapeHtml(full.name || '-')}</span>`
                },
                {
                    targets: 1,
                    render: (data) => `<span class="text-body">${escapeHtml(data || '-')}</span>`
                },
                {
                    targets: 2,
                    render: (data) => `<span class="badge ${adminUserStatusBadge(data)}">${escapeHtml(data || '-')}</span>`
                },
                {
                    targets: 3,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit text-end',
                    render: (data, type, full) => renderAdminUserActions(full)
                }
            ],
            initComplete: function () {
                const tableEl = document.getElementById('dtTenantAdminUsers');
                window.DitenDataTable?.bindActionDispatcher?.({
                    tableEl,
                    getTable: () => adminUsersDt,
                    onRowAction: {
                        'edit-admin-user': ({ row }) => {
                            if (!row) {
                                window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                                return;
                            }
                            openAdminUserModal(row);
                        },
                        'invite-admin-user': ({ id }) => {
                            if (!id) return;
                            inviteAdminUser(id).catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
                        },
                        'delete-admin-user': ({ id, row }) => {
                            if (!id) return;
                            deleteAdminUser(id, row?.name || row?.email).catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
                        }
                    }
                });
            }
        }));

        return true;
    };

    const reloadAdminUsers = async () => {
        const initialized = initAdminUsersTable();
        if (!initialized) {
            adminUsersDt?.ajax?.reload(null, false);
        }
        await loadUsers();
    };

    const openAdminUserModal = (user) => {
        const modalEl = document.getElementById('adminUserModal');
        const form = document.getElementById('adminUserForm');
        if (!modalEl || !form) return;

        form.classList.remove('was-validated');
        document.getElementById('adminUserModalTitle').innerText = user ? 'Edit Admin User' : 'Add Admin User';
        document.getElementById('adminUserId').value = user?.id || '';
        document.getElementById('adminUserName').value = user?.name || '';
        document.getElementById('adminUserEmail').value = user?.email || '';
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    };

    const saveAdminUser = async () => {
        const form = document.getElementById('adminUserForm');
        if (!form) return;
        form.classList.add('was-validated');
        if (!form.checkValidity()) return;

        const id = document.getElementById('adminUserId').value;
        const payload = {
            name: document.getElementById('adminUserName').value.trim(),
            email: document.getElementById('adminUserEmail').value.trim()
        };

        await fetchJson(
            id
                ? `${apiBase}/${encodeURIComponent(tenantId)}/admin-users/${encodeURIComponent(id)}`
                : `${apiBase}/${encodeURIComponent(tenantId)}/admin-users`,
            {
                method: id ? 'PUT' : 'POST',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

        bootstrap.Modal.getInstance(document.getElementById('adminUserModal'))?.hide();
        window.showToast?.(L.RecordSaved || 'Record saved.', 'success');
        await reloadAdminUsers();
    };

    const inviteAdminUser = async (id) => {
        await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/admin-users/${encodeURIComponent(id)}/invite`, {
            method: 'POST',
            headers: getAuthHeaders()
        });
        window.showToast?.('Invitation queued.', 'success');
        await reloadAdminUsers();
        await loadTenantQuotaGovernance();
    };

    const deleteAdminUser = async (id, name) => {
        const run = async () => {
            await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/admin-users/${encodeURIComponent(id)}`, {
                method: 'DELETE',
                headers: getAuthHeaders()
            });
            window.showToast?.(L.RecordDeleted || 'Record deleted.', 'success');
            await reloadAdminUsers();
            await loadTenantQuotaGovernance();
        };

        if (window.showConfirm) {
            window.showConfirm(L.AreYouSure || 'Are you sure?', run, { entityName: name, type: 'danger', confirmButtonText: L.Delete || 'Delete' });
        } else if (window.confirm(`Delete ${name || 'admin user'}?`)) {
            await run();
        }
    };

    const formatBool = (value) => value === true ? (L.Enabled || 'Enabled') : (L.Disabled || 'Disabled');

    const loginSecurityForm = () => document.getElementById('tenantLoginSecurityForm');
    const readBool = (name) => loginSecurityForm()?.elements[name]?.checked === true;
    const readNumber = (name) => {
        const value = loginSecurityForm()?.elements[name]?.value;
        return value === '' || value === null || value === undefined ? null : Number(value);
    };

    const setLoginSecurityLoading = (loading) => {
        const button = document.getElementById('btnSaveLoginSecurity');
        if (!button) return;
        button.disabled = loading;
        if (loading) {
            button.dataset.originalHtml = button.innerHTML;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>';
        } else if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
            delete button.dataset.originalHtml;
        }
    };

    const showLoginSecurityError = (message) => {
        const summary = document.getElementById('loginSecurityErrorSummary');
        if (!summary) return;
        summary.textContent = message || '';
        summary.classList.toggle('d-none', !message);
    };

    const populateLoginSecurityForm = (data) => {
        const form = loginSecurityForm();
        if (!form || !data) return;
        form.elements.emailLoginEnabled.checked = data.emailLoginEnabled === true;
        form.elements.phoneLoginEnabled.checked = data.phoneLoginEnabled === true;
        form.elements.twoFactorEnabled.checked = data.twoFactorEnabled === true;
        form.elements.mfaRequired.checked = data.mfaRequired === true;
        form.elements.passwordMinLength.value = data.passwordMinLength ?? 10;
        form.elements.passwordExpirationDays.value = data.passwordExpirationDays ?? '';
        form.elements.passwordRequireUppercase.checked = data.passwordRequireUppercase === true;
        form.elements.passwordRequireSpecialChar.checked = data.passwordRequireSpecialChar === true;
        form.elements.sessionTimeoutMinutes.value = data.sessionTimeoutMinutes ?? 60;
        form.elements.maxFailedLoginAttempts.value = data.maxFailedLoginAttempts ?? 5;
        form.elements.lockoutDurationMinutes.value = data.lockoutDurationMinutes ?? 15;
        form.elements.refreshTokenLifetimeDays.value = data.refreshTokenLifetimeDays ?? 14;
    };

    const buildLoginSecurityPayload = () => ({
        twoFactorEnabled: readBool('twoFactorEnabled'),
        mfaRequired: readBool('mfaRequired'),
        emailLoginEnabled: readBool('emailLoginEnabled'),
        phoneLoginEnabled: readBool('phoneLoginEnabled'),
        passwordMinLength: readNumber('passwordMinLength'),
        passwordRequireUppercase: readBool('passwordRequireUppercase'),
        passwordRequireLowercase: true,
        passwordRequireDigit: true,
        passwordRequireSpecialChar: readBool('passwordRequireSpecialChar'),
        passwordExpirationDays: readNumber('passwordExpirationDays'),
        sessionTimeoutMinutes: readNumber('sessionTimeoutMinutes'),
        refreshTokenLifetimeDays: readNumber('refreshTokenLifetimeDays'),
        maxFailedLoginAttempts: readNumber('maxFailedLoginAttempts'),
        lockoutDurationMinutes: readNumber('lockoutDurationMinutes'),
        ipWhitelistEnabled: false,
        allowedIps: [],
        allowedCountries: [],
        loginAuditRetentionDays: 90
    });

    const validateLoginSecurityPayload = (payload) => {
        if (!payload.emailLoginEnabled && !payload.phoneLoginEnabled) {
            return L.AtLeastOneLoginMethod || 'At least one login method must be enabled.';
        }
        if (payload.mfaRequired && (!payload.twoFactorEnabled || !payload.emailLoginEnabled)) {
            return L.MfaRequiresEmailLogin || 'MFA Required needs Two-Factor Authentication and Email Login enabled.';
        }
        return null;
    };

    const loadLoginSecurity = async () => {
        const data = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/login-settings`);
        populateLoginSecurityForm(data);
    };

    const saveLoginSecurity = async () => {
        const form = loginSecurityForm();
        if (!form) return;

        form.classList.add('was-validated');
        showLoginSecurityError('');
        if (!form.checkValidity()) return;

        const payload = buildLoginSecurityPayload();
        const validationError = validateLoginSecurityPayload(payload);
        if (validationError) {
            showLoginSecurityError(validationError);
            window.showToast?.(validationError, 'error');
            return;
        }

        setLoginSecurityLoading(true);
        try {
            const saved = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/login-settings`, {
                method: 'PUT',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            populateLoginSecurityForm(saved);
            window.showToast?.(L.LoginSecuritySaved || L.RecordSaved || 'Record saved.', 'success');
        } finally {
            setLoginSecurityLoading(false);
        }
    };

    const loadSettings = async () => {
        const data = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/settings`);
        renderDefinitionList('settingsSummary', [
            [L.Region || 'Region', data.region],
            [L.DefaultLanguage || 'Language', data.language],
            [L.DefaultTimezone || 'Timezone', data.timezone],
            [L.DefaultCurrency || 'Currency', data.currency],
            ['Environment', data.environment]
        ]);
    };

    const changeLifecycle = async (action, reason) => {
        await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/${action}`, {
            method: 'POST',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: reason || '' })
        });
        await loadOverview();
    };

    const readImageFile = (file, maxBytes) => new Promise((resolve, reject) => {
        if (!file) {
            resolve(null);
            return;
        }

        if (!file.type.startsWith('image/')) {
            reject(new Error('Only image files are supported.'));
            return;
        }

        if (file.size > maxBytes) {
            reject(new Error('Selected image is too large.'));
            return;
        }

        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(new Error('Image could not be read.'));
        reader.readAsDataURL(file);
    });

    const saveBranding = async (payload) => {
        const detail = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/branding`, {
            method: 'PUT',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        renderBranding(detail);
        window.showToast?.(L.RecordSaved || 'Record saved.', 'success');
    };

    const bindBranding = () => {
        document.getElementById('tenantLogoInput')?.addEventListener('change', async (event) => {
            try {
                const dataUrl = await readImageFile(event.target.files?.[0] || null, 1024 * 1024);
                const logoAvatar = document.getElementById('tenantLogoAvatar');
                if (logoAvatar && dataUrl) {
                    logoAvatar.innerHTML = imageMarkup(dataUrl, 'Tenant logo preview', 'rounded-circle');
                }
            } catch (error) {
                event.target.value = '';
                window.showToast?.(error.message || L.ErrorOccurred || 'Error occurred.', 'error');
            }
        });

        document.getElementById('tenantFaviconInput')?.addEventListener('change', async (event) => {
            try {
                const dataUrl = await readImageFile(event.target.files?.[0] || null, 256 * 1024);
                const faviconPreview = document.getElementById('tenantFaviconPreview');
                if (faviconPreview && dataUrl) {
                    faviconPreview.innerHTML = imageMarkup(dataUrl, 'Fav icon preview', 'rounded');
                }
            } catch (error) {
                event.target.value = '';
                window.showToast?.(error.message || L.ErrorOccurred || 'Error occurred.', 'error');
            }
        });

        document.getElementById('btnSaveBranding')?.addEventListener('click', async () => {
            const button = document.getElementById('btnSaveBranding');
            const originalHtml = button?.innerHTML;
            try {
                if (button) {
                    button.disabled = true;
                    button.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Upload';
                }

                const logoFile = document.getElementById('tenantLogoInput')?.files?.[0] || null;
                const faviconFile = document.getElementById('tenantFaviconInput')?.files?.[0] || null;
                const logoDataUrl = logoFile ? await readImageFile(logoFile, 1024 * 1024) : currentBranding.logoDataUrl;
                const faviconDataUrl = faviconFile ? await readImageFile(faviconFile, 256 * 1024) : currentBranding.faviconDataUrl;
                await saveBranding({ logoDataUrl, faviconDataUrl });
            } catch (error) {
                window.showToast?.(error.message || L.ErrorOccurred || 'Error occurred.', 'error');
            } finally {
                if (button) {
                    button.disabled = false;
                    button.innerHTML = originalHtml;
                }
            }
        });

        document.getElementById('btnClearBranding')?.addEventListener('click', async () => {
            try {
                await saveBranding({ logoDataUrl: null, faviconDataUrl: null });
                const logoInput = document.getElementById('tenantLogoInput');
                const faviconInput = document.getElementById('tenantFaviconInput');
                if (logoInput) logoInput.value = '';
                if (faviconInput) faviconInput.value = '';
            } catch (error) {
                window.showToast?.(error.message || L.ErrorOccurred || 'Error occurred.', 'error');
            }
        });
    };

    const bindLifecycle = () => {
        document.getElementById('btnSuspendTenant')?.addEventListener('click', () => {
            window.showConfirm?.('AreYouSure', async (reason) => {
                try {
                    await changeLifecycle('suspend', reason);
                    window.showToast?.(L.TenantSuspended || 'Tenant suspended.', 'success');
                } catch (error) {
                    window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error');
                }
            }, { type: 'warning', confirmButtonText: L.Suspend, showInput: true, inputPlaceholder: L.SuspendReason });
        });

        document.getElementById('btnReactivateTenant')?.addEventListener('click', () => {
            window.showConfirm?.('AreYouSure', async () => {
                try {
                    await changeLifecycle('reactivate', '');
                    window.showToast?.(L.TenantReactivated || 'Tenant reactivated.', 'success');
                } catch (error) {
                    window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error');
                }
            }, { type: 'success', confirmButtonText: L.Reactivate });
        });
    };

    const bindTabs = () => {
        document.querySelector('[data-bs-target="#tabAccess"]')?.addEventListener('shown.bs.tab', () => {
            if (adminUsersLoaded) return;
            adminUsersLoaded = true;
            reloadAdminUsers().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error'));
        }, { once: true });
        document.querySelector('[data-bs-target="#tabLoginSecurity"]')?.addEventListener('shown.bs.tab', () => loadLoginSecurity().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error')), { once: true });
        document.querySelector('[data-bs-target="#tabCommercial"]')?.addEventListener('shown.bs.tab', () => {
            if (commercialLoaded) return;
            commercialLoaded = true;
            loadCommercialSubscription().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error'));
        }, { once: true });
        document.querySelector('[data-bs-target="#tabModuleEntitlements"]')?.addEventListener('shown.bs.tab', () => {
            if (moduleEntitlementsLoaded) return;
            moduleEntitlementsLoaded = true;
            loadModuleEntitlements().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error'));
        }, { once: true });
        document.querySelector('[data-bs-target="#tabTenantQuotaGovernance"]')?.addEventListener('shown.bs.tab', () => {
            if (quotaGovernanceLoaded) return;
            quotaGovernanceLoaded = true;
            loadTenantQuotaGovernance();
        }, { once: true });
        document.querySelector('[data-bs-target="#tabSystemMonitoring"]')?.addEventListener('shown.bs.tab', () => loadSettings().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error')), { once: true });
    };

    const bindRecentActivity = () => {
        document.getElementById('btnViewAllRecentActivity')?.addEventListener('click', () => {
            recentActivityExpanded = !recentActivityExpanded;
            renderOverviewRecentActivity();
        });
    };

    const bindAdminUsers = () => {
        document.getElementById('btnAddAdminUser')?.addEventListener('click', () => openAdminUserModal(null));
        document.getElementById('adminUserForm')?.addEventListener('submit', (event) => {
            event.preventDefault();
            saveAdminUser().catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
        });
    };

    const bindLoginSecurity = () => {
        loginSecurityForm()?.addEventListener('submit', (event) => {
            event.preventDefault();
            saveLoginSecurity().catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
        });

        loginSecurityForm()?.elements.mfaRequired?.addEventListener('change', (event) => {
            if (!event.target.checked) return;
            const form = loginSecurityForm();
            if (!form) return;
            form.elements.twoFactorEnabled.checked = true;
            form.elements.emailLoginEnabled.checked = true;
        });
    };

    const bindCommercialSubscription = () => {
        document.getElementById('btnAssignSubscriptionEmpty')?.addEventListener('click', () => {
            openSubscriptionActionModal('assign').catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
        });
        document.getElementById('btnReactivateSubscription')?.addEventListener('click', () => confirmReactivateSubscription());
        document.getElementById('subscriptionActionForm')?.addEventListener('submit', (event) => {
            event.preventDefault();
            submitSubscriptionAction().catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
        });

        document.getElementById('subscriptionPlanSelect')?.addEventListener('change', (event) => {
            const option = event.target.selectedOptions?.[0];
            const isTrial = option?.getAttribute('data-trial') === 'true';
            document.getElementById('subscriptionIsTrial').checked = isTrial;
            document.getElementById('subscriptionPeriodEnd')?.toggleAttribute('required', !isTrial);
        });

        document.getElementById('subscriptionIsTrial')?.addEventListener('change', (event) => {
            document.getElementById('subscriptionPeriodEnd')?.toggleAttribute('required', event.target.checked !== true);
        });
        document.getElementById('btnAddModuleEntitlement')?.addEventListener('click', () => {
            openModuleEntitlementOffcanvas().catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
        });
        document.getElementById('moduleEntitlementForm')?.addEventListener('submit', (event) => {
            event.preventDefault();
            saveModuleEntitlement().catch((error) => window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error'));
        });
        document.getElementById('moduleEntitlementSource')?.addEventListener('change', () => {
            const payload = readModuleEntitlementPayload();
            const reason = document.getElementById('moduleEntitlementReason');
            reason?.toggleAttribute('required', payload.source === 'ManualOverride' || payload.isEnabled === false);
        });
        document.getElementById('moduleEntitlementEnabled')?.addEventListener('change', () => {
            const payload = readModuleEntitlementPayload();
            const reason = document.getElementById('moduleEntitlementReason');
            reason?.toggleAttribute('required', payload.source === 'ManualOverride' || payload.isEnabled === false);
        });
    };

    const bindSubscriptionHistory = () => {
        const toggleButton = document.getElementById('btnToggleSubscriptionHistory');
        const collapseEl = document.getElementById('subscriptionHistoryCollapse');
        if (!toggleButton || !collapseEl) return;

        collapseEl.addEventListener('shown.bs.collapse', () => {
            toggleButton.setAttribute('aria-expanded', 'true');
            const icon = toggleButton.querySelector('i');
            if (icon) icon.className = 'icon-base bx bx-chevron-up';
        });
        collapseEl.addEventListener('hidden.bs.collapse', () => {
            toggleButton.setAttribute('aria-expanded', 'false');
            const icon = toggleButton.querySelector('i');
            if (icon) icon.className = 'icon-base bx bx-chevron-down';
        });
    };

    return {
        init: () => {
            syncL10n();
            if (!tenantId) return;
            loadOverview().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error'));
            bindLifecycle();
            bindBranding();
            bindTabs();
            bindRecentActivity();
            bindAdminUsers();
            bindLoginSecurity();
            bindCommercialSubscription();
            bindSubscriptionHistory();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantDetails.init());
