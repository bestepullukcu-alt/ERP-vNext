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

    const syncL10n = () => { L = window.L10n || {}; };
    const getAuthHeaders = () => ({});
    const unwrap = (payload) => payload?.data ?? payload?.Data ?? payload ?? null;

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
        Trialing: 'bg-label-info',
        TrialExpired: 'bg-label-warning',
        Suspended: 'bg-label-warning',
        Cancelled: 'bg-label-danger'
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
        const response = await fetch(url, Object.assign({
            credentials: 'same-origin',
            headers: getAuthHeaders()
        }, options || {}));

        if (response.status === 401 || response.status === 403) {
            window.DtDefaults?.handleUnauthorized?.();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            throw authError;
        }

        if (!response.ok) throw new Error(await response.text());
        if (response.status === 204) return null;

        const text = await response.text();
        return text ? unwrap(JSON.parse(text)) : null;
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
            subscriptionEl.className = `badge ${subscriptionBadgeClass(detail.subscriptionStatus)}`;
            subscriptionEl.innerText = detail.subscriptionStatus || '-';
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
            { label: L.SubscriptionStatus || 'Subscription Status', html: badgeMarkup(detail.subscriptionStatus, subscriptionBadgeClass(detail.subscriptionStatus)) },
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

    const loadModules = async () => {
        const data = await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/modules`);
        document.getElementById('modulesSummary').innerHTML = `<div class="table-responsive">
            <table class="table">
                <thead><tr><th>${escapeHtml(L.Modules || 'Module')}</th><th>${escapeHtml(L.Status || 'Status')}</th><th>Source</th></tr></thead>
                <tbody>${(data.entitlements || []).map((item) => `<tr><td>${escapeHtml(item.moduleName)}</td><td>${item.enabled ? L.Active : L.Passive}</td><td>${escapeHtml(item.source)}</td></tr>`).join('')}</tbody>
            </table>
        </div>`;
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
    };

    const deleteAdminUser = async (id, name) => {
        const run = async () => {
            await fetchJson(`${apiBase}/${encodeURIComponent(tenantId)}/admin-users/${encodeURIComponent(id)}`, {
                method: 'DELETE',
                headers: getAuthHeaders()
            });
            window.showToast?.(L.RecordDeleted || 'Record deleted.', 'success');
            await reloadAdminUsers();
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
        document.querySelector('[data-bs-target="#tabAccess"]')?.addEventListener('shown.bs.tab', () => {
            if (adminUsersLoaded) return;
            adminUsersLoaded = true;
            reloadAdminUsers().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error'));
        }, { once: true });
        document.querySelector('[data-bs-target="#tabLoginSecurity"]')?.addEventListener('shown.bs.tab', () => loadLoginSecurity().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error')), { once: true });
        document.querySelector('[data-bs-target="#tabCommercial"]')?.addEventListener('shown.bs.tab', () => loadModules().catch(() => window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error')), { once: true });
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
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantDetails.init());
