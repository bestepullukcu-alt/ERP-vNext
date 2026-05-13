/**
 * Platform Administrators — DataTables Index Script
 * Diten ERP vNext | Platform/Administrators
 */
'use strict';

const AdministratorsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let editingId = null;
    let suspendModalRowVersion = null;
    let L = window.L10n || {};

    const dtTableEl = document.querySelector('.datatables-administrators');
    const endpoint = '/Platform/Administrators/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'Administrators' };
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 10;
    const baseOrder = [[2, 'asc']];
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
    let appliedFilters = { status: [], actorType: [], role: [], invitationStatus: [] };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[char]));
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const normalizeArray = (value) => {
        const raw = Array.isArray(value) ? value : (typeof value === 'string' ? value.split(',') : []);
        return [...new Set(raw.map((item) => normalizeString(String(item))).filter(Boolean))].sort((a, b) => a.localeCompare(b));
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], actorType: [], role: [], invitationStatus: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            actorType: normalizeArray(source.actorType),
            role: normalizeArray(source.role),
            invitationStatus: normalizeArray(source.invitationStatus)
        };
    };
    const getAuthHeaders = (includeJson = false) => {
        const headers = { 'X-Requested-With': 'XMLHttpRequest' };
        if (includeJson) headers['Content-Type'] = 'application/json';
        return headers;
    };
    const getAntiForgeryToken = () =>
        document.querySelector('#formAdministrators input[name="__RequestVerificationToken"]')?.value || '';
    const syncL10n = () => { L = window.L10n || {}; };

    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount ? normalized : null;
    };
    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((columnIndex, position) => {
                if (typeof colVis[columnIndex] === 'boolean') normalized[columnIndex] = colVis[columnIndex];
                else if (typeof colVis[position] === 'boolean') normalized[columnIndex] = colVis[position];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((columnIndex) => {
                if (typeof colVis[columnIndex] === 'boolean') normalized[columnIndex] = colVis[columnIndex];
            });
        }
        return Object.keys(normalized).length ? normalized : null;
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((acc, columnIndex) => {
        acc[columnIndex] = defaultVisibleColumnIndexes.includes(columnIndex);
        return acc;
    }, {});
    const captureColVis = (api) => {
        const result = {};
        saveViewColumnIndexes.forEach((columnIndex) => {
            try { result[columnIndex] = !!api.column(columnIndex).visible(); } catch (error) { }
        });
        return result;
    };
    const captureColOrder = (api) => {
        try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (error) { return null; }
    };
    const applyColVis = (api, colVis) => {
        const normalized = normalizeColVis(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((columnIndex) => {
            if (typeof normalized[columnIndex] === 'boolean') {
                try { api.column(columnIndex).visible(normalized[columnIndex], false); } catch (error) { }
            }
        });
    };
    const applyColOrder = (api, order) => {
        const normalized = normalizeColOrder(order);
        if (normalized && typeof api?.colReorder?.order === 'function') api.colReorder.order(normalized, true);
    };
    const getSearchVal = (api) => {
        try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (error) { return ''; }
    };
    const syncSearchInput = (api, value) => {
        try {
            const input = api.table().container().querySelector('.dt-search input');
            if (input) input.value = value || '';
        } catch (error) { }
    };
    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (view) => JSON.stringify({
        filters: Object.keys(view?.filters || {}).sort().reduce((acc, key) => {
            acc[key] = normalizeFilterValue(view.filters[key]);
            return acc;
        }, {}),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getSavedViewId = (savedView) => savedView?.id || savedView?.Id || savedView?._id || null;
    const getSavedViewName = (savedView) => savedView?.viewName || savedView?.ViewName || '';
    const isSavedViewDefault = (savedView) => savedView?.isDefault === true || savedView?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
    const getSavedViewDef = (savedView) => {
        const raw = savedView?.viewDefinition ?? savedView?.ViewDefinition ?? {};
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (error) { return {}; }
        }
        return raw || {};
    };
    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDef(savedView);
        return {
            filters: normalizeFilters(definition.filters),
            search: normalizeString(definition.search),
            colVis: normalizeColVis(definition.colVis),
            columnOrder: normalizeColOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });
    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[Administrators SaveView] Failed to load saved views.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalizedView = normalizeViewState(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalizedView,
            isDefault: true,
            visibility: 'private'
        };
        const existingId = getSavedViewId(defaultViewRecord);
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object'
            ? savedRecord
            : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };
    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const statusMap = () => ({
        Active: ['bg-label-success', L.Active],
        Suspended: ['bg-label-warning', L.Suspended],
        Disabled: ['bg-label-secondary', L.Disabled]
    });
    const invitationMap = () => ({
        PendingInvitation: ['bg-label-info', L.PendingInvitation],
        Invited: ['bg-label-primary', L.Invited],
        Accepted: ['bg-label-success', L.Accepted],
        Expired: ['bg-label-danger', L.Expired]
    });
    const actorTypeMap = () => ({ PlatformAdmin: L.PlatformAdmin, PartnerAdmin: L.PartnerAdmin });
    const roleMap = () => ({
        SuperAdmin: L.RoleSuperAdmin,
        BillingAdmin: L.RoleBillingAdmin,
        SupportAdmin: L.RoleSupportAdmin,
        ReadOnly: L.RoleReadOnly
    });
    const badge = (value, map) => {
        const item = map[value] || ['bg-label-secondary', value || L.Unknown || '-'];
        return `<span class="badge ${item[0]}">${escapeHtml(item[1] || value || '-')}</span>`;
    };
    const renderRoles = (roles) => {
        const items = Array.isArray(roles) ? roles : [];
        if (!items.length) return '-';
        return items.map((role) => `<span class="badge bg-label-primary me-1">${escapeHtml(roleMap()[role] || role)}</span>`).join('');
    };
    const updateKpis = (stats) => {
        if (!stats) return;
        document.getElementById('kpi-total').innerText = String(stats.total ?? stats.Total ?? 0);
        document.getElementById('kpi-active').innerText = String(stats.active ?? stats.Active ?? 0);
        document.getElementById('kpi-suspended').innerText = String(stats.suspended ?? stats.Suspended ?? 0);
        document.getElementById('kpi-pending').innerText = String(stats.pendingInvitation ?? stats.PendingInvitation ?? 0);
    };
    const loadStats = async () => {
        try {
            const response = await fetch(`${endpoint}/stats`, { headers: getAuthHeaders() });
            if (!response.ok) return;
            const payload = await response.json();
            updateKpis(payload.data || payload.Data);
        } catch (error) {
            console.warn('[Administrators KPIs] Failed to load stats.', error);
        }
    };
    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);
        loadStats();
    };
    const showDevSetupLink = (setupUrl, emailSent) => {
        if (!setupUrl) return;

        const intro = emailSent === false
            ? 'Email could not be sent. Use this development setup link:'
            : 'Development setup link:';

        if (window.Swal) {
            window.Swal.fire({
                icon: 'info',
                title: 'Setup link',
                html: `<p class="mb-2">${escapeHtml(intro)}</p><input class="form-control" readonly value="${escapeHtml(setupUrl)}">`,
                confirmButtonText: L.Ok || 'OK',
                customClass: { confirmButton: 'btn btn-primary' },
                buttonsStyling: false
            });
            return;
        }

        window.showToast?.(setupUrl, 'info');
    };

    const buildQuery = (data) => {
        const params = new URLSearchParams();
        params.set('page', String(Math.floor((data.start || 0) / (data.length || 20)) + 1));
        params.set('pageSize', String(data.length || 20));
        const order = Array.isArray(data.order) ? data.order[0] : null;
        const orderColumn = order ? data.columns?.[order.column] : null;
        const sortName = orderColumn?.name || orderColumn?.data || 'displayName';
        params.set('sort', `${sortName}:${order?.dir || 'asc'}`);
        const search = data.search?.value || '';
        if (search) params.set('search', search);
        Object.entries(appliedFilters).forEach(([key, value]) => {
            if (Array.isArray(value) && value.length) params.set(key, value.join(','));
        });
        return params.toString();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
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
    const syncMultiSelectSummary = ($select) => {
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
    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: false
            });
            $select.off('change.select2-summary').on('change.select2-summary', function () { syncMultiSelectSummary($select); });
            requestAnimationFrame(() => syncMultiSelectSummary($select));
        });
    };
    const syncFilterControls = (filters) => {
        const values = normalizeFilters(filters);
        $('#filterStatus').val(values.status).trigger('change');
        $('#filterActorType').val(values.actorType).trigger('change');
        $('#filterRole').val(values.role).trigger('change');
        $('#filterInvitationStatus').val(values.invitationStatus).trigger('change');
    };
    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => Array.isArray(value) ? value.length > 0 : value !== '').length;
    const applySavedTableState = (api, state) => {
        if (!api || !state) return;
        const normalizedState = normalizeViewState(state);
        appliedFilters = normalizedState.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(api, normalizedState.columnOrder);
        applyColVis(api, normalizedState.colVis);
        api.search(normalizedState.search);
        syncSearchInput(api, normalizedState.search);
        api.order(normalizedState.order);
        try { api.columns.adjust(); } catch (error) { }
        try { api.responsive?.recalc?.(); } catch (error) { }
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };
    const bindFilters = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: normalizeArray($('#filterStatus').val() || []),
                actorType: normalizeArray($('#filterActorType').val() || []),
                role: normalizeArray($('#filterRole').val() || []),
                invitationStatus: normalizeArray($('#filterInvitationStatus').val() || [])
            };
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed && dt) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            if (dt) applySavedTableState(dt, getResetBaselineState());
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed && dt) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const getOcCreateEditInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };
    const getOcDetailsInstance = () => {
        const el = document.getElementById('offcanvasDetailsPreview');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };
    const setPartnerScopeVisibility = () => {
        const isPartner = document.getElementById('administratorActorType')?.value === 'PartnerAdmin';
        document.querySelectorAll('.js-partner-scope').forEach((el) => el.classList.toggle('d-none', !isPartner));
        document.getElementById('administratorPartnerId')?.toggleAttribute('required', isPartner);
        document.getElementById('administratorAllowedTenantIds')?.toggleAttribute('required', isPartner);
    };
    const triggerFormTrackerUpdate = () => {
        const form = document.getElementById('formAdministrators');
        if (!form) return;
        form.querySelectorAll('input, select, textarea').forEach((el) => {
            el.dispatchEvent(new Event('input', { bubbles: true }));
        });
    };
    const resetCreateEditForm = () => {
        const form = document.getElementById('formAdministrators');
        if (!form) return;
        form.reset();
        form.classList.remove('was-validated');
        document.getElementById('administratorId').value = '';
        document.getElementById('administratorVersion').value = '0';
        document.getElementById('administratorStatus').value = 'Active';
        document.getElementById('administratorUserName').value = '';
        document.getElementById('administratorActorType').value = 'PlatformAdmin';
        document.getElementById('roleReadOnly').checked = true;
        document.getElementById('formAdministratorsAlert').classList.add('d-none');
        setPartnerScopeVisibility();
        triggerFormTrackerUpdate();
    };
    const openCreateOffcanvas = () => {
        editingId = null;
        resetCreateEditForm();
        document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleCreate || L.AddNew || '';
        document.getElementById('createRecommendedBadge')?.classList.remove('d-none');
        document.getElementById('btnSaveAdministrator').textContent = L.Save || '';
        
        // Ensure email input is unlocked and button is hidden
        const emailInput = document.getElementById('administratorEmail');
        if (emailInput) {
            emailInput.removeAttribute('disabled');
            emailInput.style.borderColor = ''; // Reset custom border color
            emailInput.classList.add('rounded-end'); // Beautiful rounded corner on the right when button is hidden
        }
        const btnUnlockEmail = document.getElementById('btnUnlockEmail');
        if (btnUnlockEmail) {
            btnUnlockEmail.classList.add('d-none');
            btnUnlockEmail.innerHTML = '<i class="bx bx-lock-alt"></i>';
            btnUnlockEmail.classList.remove('btn-outline-primary', 'btn-outline-warning');
            btnUnlockEmail.classList.add('btn-outline-secondary');
            btnUnlockEmail.style.borderColor = '';
        }

        // Enable select for ActorType
        const actorTypeSelect = document.getElementById('administratorActorType');
        if (actorTypeSelect) {
            actorTypeSelect.removeAttribute('disabled');
        }

        // Hide Status cards & Password management section
        document.getElementById('administratorEditStatusSection')?.classList.add('d-none');
        document.querySelector('.js-password-management')?.classList.add('d-none');

        getOcCreateEditInstance()?.show();
    };
    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetCreateEditForm();
        document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleEdit || L.Edit || '';
        document.getElementById('createRecommendedBadge')?.classList.add('d-none');
        document.getElementById('btnSaveAdministrator').textContent = L.Update || L.Save || '';

        // Compute Display Name border color to match perfectly
        const displayNameInput = document.getElementById('administratorDisplayName');
        let matchedBorderColor = '#d9dee3'; // fallback standard border color
        if (displayNameInput) {
            matchedBorderColor = window.getComputedStyle(displayNameInput).borderColor || '#d9dee3';
        }

        // Lock email and show unlock button
        const emailInput = document.getElementById('administratorEmail');
        if (emailInput) {
            emailInput.setAttribute('disabled', 'disabled');
            emailInput.style.borderColor = matchedBorderColor; // Perfect match with Display Name
            emailInput.classList.remove('rounded-end'); // Flat right side when button is shown
        }
        const btnUnlockEmail = document.getElementById('btnUnlockEmail');
        if (btnUnlockEmail) {
            btnUnlockEmail.classList.remove('d-none');
            btnUnlockEmail.innerHTML = '<i class="bx bx-lock-alt"></i>';
            btnUnlockEmail.classList.remove('btn-outline-primary', 'btn-outline-warning');
            btnUnlockEmail.classList.add('btn-outline-secondary');
            btnUnlockEmail.style.borderColor = matchedBorderColor; // Perfect matching border with standard input
        }

        try {
            const response = await fetch(`/Platform/Administrators/get/${encodeURIComponent(id)}`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            const json = await response.json();
            if (!json.success || !json.data) throw new Error('Failed to load administrator.');
            const data = json.data;
            document.getElementById('administratorId').value = data.id || '';
            document.getElementById('administratorEmail').value = data.email || '';
            document.getElementById('administratorUserName').value = data.userName || '';
            document.getElementById('administratorDisplayName').value = data.displayName || '';
            document.getElementById('administratorActorType').value = data.actorType || 'PlatformAdmin';

            // Set actor type select dropdown to disabled
            const actorTypeSelect = document.getElementById('administratorActorType');
            if (actorTypeSelect) {
                actorTypeSelect.setAttribute('disabled', 'disabled');
            }

            document.getElementById('administratorPartnerId').value = data.partnerId || '';
            document.getElementById('administratorAllowedTenantIds').value = Array.isArray(data.allowedTenantIds) ? data.allowedTenantIds.join('\n') : '';
            document.getElementById('administratorStatus').value = data.status || 'Active';
            document.getElementById('administratorVersion').value = String(data.version || 0);
            document.querySelectorAll('#administratorRolesGroup input[name="Roles"]').forEach((input) => {
                input.checked = Array.isArray(data.roles) && data.roles.includes(input.value);
            });
            setPartnerScopeVisibility();

            // Status and Invitation badges in edit mode
            document.getElementById('administratorEditStatusSection')?.classList.remove('d-none');
            const statusBadge = document.getElementById('administratorEditStatusBadge');
            if (statusBadge) {
                const mapData = statusMap()[data.status] || ['bg-label-secondary', data.status || '-'];
                statusBadge.className = `badge ${mapData[0]}`;
                statusBadge.innerText = mapData[1];
            }

            const invitationBadge = document.getElementById('administratorEditInvitationBadge');
            if (invitationBadge) {
                const mapData = invitationMap()[data.invitationStatus] || ['bg-label-secondary', data.invitationStatus || '-'];
                invitationBadge.className = `badge ${mapData[0]}`;
                invitationBadge.innerText = mapData[1];
            }

            const pwdMgmt = document.querySelector('.js-password-management');
            if (pwdMgmt) {
                pwdMgmt.classList.remove('d-none');
                const wrapperResend = document.getElementById('wrapperResendInvite');
                wrapperResend?.classList.remove('d-none');
            }

            triggerFormTrackerUpdate();
            getOcCreateEditInstance()?.show();
        } catch (error) {
            console.error('[Administrators] Failed to load administrator for edit.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
        }
    };
    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formAdministratorsAlert');
        if (!alertEl) return;
        alertEl.innerHTML = Array.isArray(errors) ? errors.map((error) => `<div>${escapeHtml(error)}</div>`).join('') : escapeHtml(errors || L.FormValidationError || '');
        alertEl.classList.remove('d-none');
    };
    const submitCreateEditForm = async () => {
        const form = document.getElementById('formAdministrators');
        if (!form) return;
        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showFormErrors([L.FormValidationError || '']);
            return;
        }
        if (!form.querySelector('input[name="Roles"]:checked')) {
            showFormErrors([L.FormValidationError || '']);
            return;
        }
        const isPartner = document.getElementById('administratorActorType')?.value === 'PartnerAdmin';
        if (isPartner && (!document.getElementById('administratorPartnerId')?.value || !document.getElementById('administratorAllowedTenantIds')?.value)) {
            showFormErrors([L.PartnerScopeRequired || '']);
            return;
        }
        // Temporarily enable disabled fields so they are submitted in FormData
        const disabledFields = form.querySelectorAll('input:disabled, select:disabled, textarea:disabled');
        disabledFields.forEach(el => el.removeAttribute('disabled'));

        const formData = new FormData(form);

        // Restore their disabled states immediately
        disabledFields.forEach(el => el.setAttribute('disabled', 'disabled'));
        const url = editingId ? `/Platform/Administrators/edit/${encodeURIComponent(editingId)}` : '/Platform/Administrators/create';
        const saveBtn = document.getElementById('btnSaveAdministrator');
        if (saveBtn) saveBtn.disabled = true;
        try {
            const response = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken(),
                    ...getAuthHeaders()
                },
                body: formData
            });
            const json = await response.json();
            if (json.success) {
                getOcCreateEditInstance()?.hide();
                showDevSetupLink(json.devSetupUrl, json.emailSent);
                reloadWithSuccessToast(editingId ? 'RecordUpdated' : 'RecordCreated');
            } else {
                showFormErrors(json.errors);
            }
        } catch (error) {
            console.error('[Administrators] Form submit failed.', error);
            showFormErrors([L.ErrorOccurred || '']);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    const populateDetailsOffcanvas = (data) => {
        if (!data) return;
        document.getElementById('oc-title').innerText = data.displayName || '-';
        document.getElementById('oc-subtitle').innerText = data.email || '-';
        document.getElementById('oc-email').innerText = data.email || '-';
        document.getElementById('oc-username').innerText = data.userName || '-';
        document.getElementById('oc-actor-type').innerText = actorTypeMap()[data.actorType] || data.actorType || '-';
        document.getElementById('oc-roles').innerHTML = renderRoles(data.roles);
        document.getElementById('oc-invitation-status').innerHTML = badge(data.invitationStatus, invitationMap());
        document.getElementById('oc-partner-id').innerText = data.partnerId || '-';
        document.getElementById('oc-tenant-scope').innerText = Array.isArray(data.allowedTenantIds) && data.allowedTenantIds.length ? data.allowedTenantIds.join('\n') : '-';
        const isPartnerAdmin = data.actorType === 'PartnerAdmin' || data.actorType === 2;
        document.querySelectorAll('#offcanvasDetailsPreview .js-partner-scope').forEach((el) => el.classList.toggle('d-none', !isPartnerAdmin));
        document.getElementById('oc-audit').innerText = `${data.createdBy || '-'} / ${data.updatedBy || '-'}`;
        const statusEl = document.getElementById('oc-status');
        statusEl.className = `badge ${(statusMap()[data.status] || ['bg-label-secondary'])[0]}`;
        statusEl.innerText = (statusMap()[data.status] || [null, data.status || '-'])[1] || '-';
        document.getElementById('oc-btn-edit').dataset.editId = data.id || '';
    };

    const isRowProtected = (row) => {
        if (!row) return false;
        const id = row.id || row.Id;
        const currentUserId = window.CurrentUser?.id;
        const emailLower = String(row.email || row.Email || '').toLowerCase();
        return id === currentUserId || emailLower === 'admin@diten.com';
    };

    const updateBulkDeleteButtonVisibility = () => {
        if (!dtTableEl) return;
        const anyProtectedSelected = !!dtTableEl.querySelector('.dt-checkboxes:checked[data-protected="true"]');
        const deleteBtn = document.querySelector('[data-bulk-action="delete"]');
        if (deleteBtn) {
            deleteBtn.classList.toggle('d-none', anyProtectedSelected);
        }
    };

    const selectedItems = () => {
        const ids = window.DitenDataTable.getSelectedIds(dtTableEl, bulkOptions.checkboxSelector);
        return ids.map((id) => {
            let rowData = null;
            dt.rows().every(function () {
                const row = this.data();
                if (String(row?.id || row?.Id) === String(id)) rowData = row;
            });
            return rowData;
        }).filter((row) => row && !isRowProtected(row))
          .map((row) => ({ id: row.id || row.Id, version: Number(row.version || row.Version || 0) }));
    };

    const getSuspendModalInstance = () => {
        const el = document.getElementById('suspendModal');
        return el ? bootstrap.Modal.getOrCreateInstance(el) : null;
    };

    const rowActionHandlers = {
        quickView: ({ row }) => {
            populateDetailsOffcanvas(row);
            getOcDetailsInstance()?.show();
        },
        edit: ({ id }) => openEditOffcanvas(id),
        suspend: ({ row, id }) => {
            const modalEl = document.getElementById('suspendModal');
            if (!modalEl) return;

            const form = document.getElementById('suspendForm');
            if (form) {
                form.reset();
                form.classList.remove('was-validated');
            }

            document.getElementById('suspendAdminId').value = id;
            suspendModalRowVersion = row?.version || row?.Version || 0;

            getSuspendModalInstance()?.show();
        },
        reactivate: ({ row, id }) => changeStatus(id, 'reactivate', row?.version || row?.Version, ''),
        resendInvite: ({ row, id }) => postVersionAction(id, 'resend-invite', row?.version || row?.Version, 'ResendInviteSuccess'),
        delete: ({ row, id }) => deleteOne(id, row)
    };

    const postVersionAction = async (id, action, version, successKey) => {
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, {
                method: 'POST',
                headers: getAuthHeaders(true),
                body: JSON.stringify({ version: Number(version || 0) })
            });
            if (!response.ok) throw new Error(`${action} failed.`);
            const json = await response.json().catch(() => null);
            const result = json?.data || json?.Data;
            showDevSetupLink(result?.setupUrl || result?.SetupUrl, result?.emailSent ?? result?.EmailSent);
            reloadWithSuccessToast(successKey);
        } catch (error) {
            console.error(error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
        }
    };
    const changeStatus = async (id, action, version, reason) => {
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, {
                method: 'POST',
                headers: getAuthHeaders(true),
                body: JSON.stringify({ version: Number(version || 0), reason })
            });
            if (!response.ok) throw new Error(await readErrorMessage(response, `${action} failed.`));
            reloadWithSuccessToast('RecordUpdated');
        } catch (error) {
            console.error(error);
            window.showToast?.(error?.message || L.ErrorOccurred || '', 'error');
        }
    };
    const readErrorMessage = async (response, fallback) => {
        try {
            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('application/json')) {
                const json = await response.json();
                if (Array.isArray(json?.errors) && json.errors.length) return localizeServerError(json.errors[0]);
                if (json?.errors && typeof json.errors === 'object') {
                    const first = Object.values(json.errors).flat().find(Boolean);
                    if (first) return localizeServerError(first);
                }
                if (json?.message) return localizeServerError(json.message);
                if (json?.detail) return localizeServerError(json.detail);
                if (json?.title) return localizeServerError(json.title);
            }

            const text = await response.text();
            return localizeServerError(text || fallback || L.ErrorOccurred || '');
        } catch {
            return fallback || L.ErrorOccurred || '';
        }
    };
    const localizeServerError = (message) => {
        const text = String(message || '').trim();
        if (!text) return text;

        if (/You cannot perform this action on your own account/i.test(text)) return L.AdminSelfActionDenied || text;
        if (/You cannot remove your own administrative role/i.test(text)) return L.AdminSelfRoleDowngradeDenied || text;
        if (/At least one active Super Admin must remain in the system/i.test(text)) return L.AdminLastSuperAdminDenied || text;
        if (/The system seed administrator cannot be deleted/i.test(text)) return L.AdminSeedDeleteDenied || text;
        if (/The system seed administrator cannot be suspended/i.test(text)) return L.AdminSeedSuspendDenied || text;

        return text;
    };
    const deleteOne = (id, row) => {
        if (!id) return;
        const entityName = row?.displayName || row?.email || '';
        window.showConfirm?.(L.AreYouSure, async () => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                    method: 'DELETE',
                    headers: getAuthHeaders(true),
                    body: JSON.stringify({ version: Number(row?.version || row?.Version || 0) })
                });
                if (!response.ok) throw new Error(await readErrorMessage(response, L.ErrorOccurred));
                reloadWithSuccessToast('RecordDeleted', entityName);
            } catch (error) {
                console.error(error);
                window.showToast?.(error?.message || L.ErrorOccurred || '', 'error');
            }
        }, { entityName, type: 'delete', confirmButtonText: L.Delete });
    };
    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: {
            delete: () => {
                const items = selectedItems();
                if (!items.length) return;
                const confirmText = (L.BulkDeleteConfirm || '').replace('{0}', items.length);
                window.showConfirm?.(confirmText, async () => {
                    try {
                        const response = await fetch(`${endpoint}/bulk`, {
                            method: 'DELETE',
                            headers: getAuthHeaders(true),
                            body: JSON.stringify(items)
                        });
                        if (!response.ok) throw new Error(await readErrorMessage(response, L.ErrorOccurred));
                        reloadWithSuccessToast('BulkDeleteSuccess', String(items.length));
                    } catch (error) {
                        console.error(error);
                        window.showToast?.(error?.message || L.ErrorOccurred || '', 'error');
                    }
                }, { entityName: String(items.length), type: 'delete', confirmButtonText: L.Delete });
            }
        }
    };

    const initDataTable = async () => {
        if (!dtTableEl || !window.DtDefaults) return;
        const savedState = await loadDefaultView();
        if (savedState?.filters) appliedFilters = normalizeFilters(Object.assign({}, appliedFilters, savedState.filters));
        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: () => window.showToast?.(L.ComingSoon, 'warning')
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + escapeHtml(L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary dt-save-filter-btn d-none',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (event, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[Administrators SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            stateSave: false,
            order: savedState?.order || baseOrder,
            search: { search: savedState?.search || '' },
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
                { data: 'id', name: 'checkbox' },
                { data: 'displayName', name: 'displayName', render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { data: 'userName', name: 'userName', render: escapeHtml },
                { data: 'email', name: 'email', render: escapeHtml },
                { data: 'actorType', name: 'actorType', render: (data) => escapeHtml(actorTypeMap()[data] || data) },
                { data: 'roles', name: 'role', render: renderRoles },
                { data: 'invitationStatus', name: 'invitationStatus', render: (data) => badge(data, invitationMap()) },
                { data: 'status', name: 'status', render: (data) => badge(data, statusMap()) },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                {
                    targets: 1,
                    orderable: false,
                    searchable: false,
                    responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data, type, row) => {
                        const currentUserId = window.CurrentUser?.id;
                        const emailLower = String(row?.email || row?.Email || '').toLowerCase();
                        const isProtected = (row?.id || row?.Id) === currentUserId || emailLower === 'admin@diten.com';
                        return `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data)}" ${isProtected ? 'data-protected="true"' : ''}>`;
                    }
                },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);
                        const currentUserId = window.CurrentUser?.id;
                        const emailLower = String(row.email || row.Email || '').toLowerCase();
                        const isProtected = id === currentUserId || emailLower === 'admin@diten.com';

                        const actions = [
                            { key: 'quickView', className: 'js-quick-view', text: L.QuickView, icon: 'bx bx-show', attrs: { 'data-id': id, 'data-json': rowJson } },
                            { key: 'edit', className: 'js-edit-item', text: L.Edit, icon: '', attrs: { 'data-id': id, 'data-json': rowJson } }
                        ];

                        if (row.status === 'Suspended') {
                            actions.push({ key: 'reactivate', className: 'text-success', text: L.Reactivate, icon: '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        } else {
                            if (isProtected) {
                                actions.push({
                                    key: 'suspend',
                                    className: 'text-muted opacity-50 pe-none',
                                    text: L.Suspend,
                                    icon: '',
                                    attrs: { 'style': 'pointer-events: none; opacity: 0.5;', 'title': L.ProtectedAccount || 'Protected Account' }
                                });
                            } else {
                                actions.push({ key: 'suspend', className: 'text-warning', text: L.Suspend, icon: '', attrs: { 'data-id': id, 'data-json': rowJson } });
                            }
                        }

                        if (!isProtected) {
                            actions.push({ key: 'resendInvite', className: '', text: L.ResendInvite, icon: '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        }

                        if (isProtected) {
                            actions.push({
                                key: 'delete',
                                className: 'delete-record text-muted opacity-50 pe-none',
                                text: L.Delete,
                                icon: '',
                                attrs: { 'style': 'pointer-events: none; opacity: 0.5;', 'title': L.ProtectedAccount || 'Protected Account' }
                            });
                        } else {
                            actions.push({ key: 'delete', className: 'delete-record text-danger', text: L.Delete, icon: '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        }
                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNew || '', {}, extraButtons, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                initSelect2Filters();
                applySavedTableState(api, savedState || { filters: appliedFilters });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                loadStats();
            }
        });

        dt = new DataTable(dtTableEl, dtConfig);
        window.DitenDataTable?.bindBulkSelection?.(dtTableEl, dt, bulkOptions);
        window.DitenDataTable?.bindActionDispatcher?.({ tableEl: dtTableEl, dt, onRowAction: rowActionHandlers });

        $(dtTableEl).on('change', '.dt-checkboxes, .dt-checkboxes-select-all', () => {
            setTimeout(updateBulkDeleteButtonVisibility, 0);
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', () => {
            setTimeout(updateBulkDeleteButtonVisibility, 0);
        });

        dt.on('draw.dt', () => {
            setTimeout(updateBulkDeleteButtonVisibility, 0);
        });

        $(dtTableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const bindEvents = () => {
        bindFilters();
        document.addEventListener('click', (event) => {
            const addNewBtn = event.target.closest('.add-new');
            if (addNewBtn) {
                event.preventDefault();
                openCreateOffcanvas();
                return;
            }

            const quickViewBtn = event.target.closest('.js-quick-view');
            if (!quickViewBtn || !quickViewBtn.closest('.datatables-administrators')) return;
            event.preventDefault();
            event.stopPropagation();
            let rowEl = quickViewBtn.closest('tr');
            if (rowEl?.classList.contains('child')) rowEl = rowEl.previousElementSibling;
            const row = rowEl ? dt?.row(rowEl).data() : null;
            if (!row) return;
            populateDetailsOffcanvas(row);
            getOcDetailsInstance()?.show();
        });
        document.getElementById('administratorActorType')?.addEventListener('change', setPartnerScopeVisibility);
        document.getElementById('btnSaveAdministrator')?.addEventListener('click', submitCreateEditForm);
        document.getElementById('oc-btn-edit')?.addEventListener('click', () => {
            const id = document.getElementById('oc-btn-edit')?.dataset.editId;
            if (id) openEditOffcanvas(id);
        });

        // Email unlock button
        const btnUnlockEmail = document.getElementById('btnUnlockEmail');
        if (btnUnlockEmail) {
            btnUnlockEmail.addEventListener('click', () => {
                const emailInput = document.getElementById('administratorEmail');
                if (emailInput) {
                    if (emailInput.hasAttribute('disabled')) {
                        emailInput.removeAttribute('disabled');
                        emailInput.style.borderColor = ''; // Let focus/active styling handle the border color
                        emailInput.focus();
                        btnUnlockEmail.innerHTML = '<i class="bx bx-lock-open-alt"></i>';
                        btnUnlockEmail.classList.remove('btn-outline-secondary');
                        btnUnlockEmail.classList.add('btn-outline-primary');
                        btnUnlockEmail.style.borderColor = ''; // Let active class handle it
                    } else {
                        emailInput.setAttribute('disabled', 'disabled');
                        
                        // Dynamically compute the active input's border color
                        const displayNameInput = document.getElementById('administratorDisplayName');
                        let matchedBorderColor = '#d9dee3';
                        if (displayNameInput) {
                            matchedBorderColor = window.getComputedStyle(displayNameInput).borderColor || '#d9dee3';
                        }
                        
                        emailInput.style.borderColor = matchedBorderColor;
                        btnUnlockEmail.innerHTML = '<i class="bx bx-lock-alt"></i>';
                        btnUnlockEmail.classList.remove('btn-outline-primary');
                        btnUnlockEmail.classList.add('btn-outline-secondary');
                        btnUnlockEmail.style.borderColor = matchedBorderColor; // Perfect matching border with standard inputs
                    }
                }
            });
        }

        // Resend Invite button
        const btnResendInvite = document.getElementById('btnResendInvite');
        if (btnResendInvite) {
            btnResendInvite.addEventListener('click', async () => {
                if (!editingId) return;
                btnResendInvite.disabled = true;
                try {
                    const version = document.getElementById('administratorVersion').value;
                    const response = await fetch(`/Platform/Administrators/api/${encodeURIComponent(editingId)}/resend-invite`, {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': getAntiForgeryToken(),
                            ...getAuthHeaders()
                        },
                        body: JSON.stringify({ version: parseInt(version || '0') })
                    });
                    const json = await response.json();
                    if (response.ok && (json.success || json.statusCode === 200 || json.statusCode === 204)) {
                        const result = json.data || json.Data;
                        showDevSetupLink(result?.setupUrl || result?.SetupUrl, result?.emailSent ?? result?.EmailSent);
                        reloadWithSuccessToast('ResendInviteSuccess');
                        getOcCreateEditInstance()?.hide();
                    } else {
                        const errorMsg = Array.isArray(json.errors) ? json.errors[0] : (json.message || L.ErrorOccurred);
                        window.showToast?.(errorMsg, 'error');
                    }
                } catch (error) {
                    console.error('[Administrators] Resend invite failed.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                } finally {
                    btnResendInvite.disabled = false;
                }
            });
        }

        const suspendForm = document.getElementById('suspendForm');
        suspendForm?.addEventListener('submit', async (event) => {
            event.preventDefault();
            event.stopPropagation();

            if (!suspendForm.checkValidity()) {
                suspendForm.classList.add('was-validated');
                return;
            }

            const id = document.getElementById('suspendAdminId').value;
            const reason = normalizeString(document.getElementById('suspendReason').value);
            if (!reason) return;

            const btn = document.getElementById('btnConfirmSuspend');
            const spinner = btn?.querySelector('.spinner-border');
            if (btn) btn.disabled = true;
            if (spinner) spinner.classList.remove('d-none');

            try {
                await changeStatus(id, 'suspend', suspendModalRowVersion, reason);
                getSuspendModalInstance()?.hide();
            } finally {
                if (btn) btn.disabled = false;
                if (spinner) spinner.classList.add('d-none');
            }
        });
    };

    const init = async () => {
        syncL10n();
        bindEvents();
        loadStats();
        await initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => AdministratorsList.init());
