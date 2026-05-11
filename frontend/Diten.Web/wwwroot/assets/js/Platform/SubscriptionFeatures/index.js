'use strict';

(function () {
    const l10n = () => window.L10n || {};
    const state = {
        features: [],
        categories: [],
        plans: [],
        mappings: [],
        editingFeature: null
    };

    const els = {
        error: document.getElementById('sf-error'),
        loading: document.getElementById('sf-loading'),
        empty: document.getElementById('sf-empty'),
        noResults: document.getElementById('sf-no-results'),
        grid: document.getElementById('sf-grid'),
        search: document.getElementById('sf-search'),
        status: document.getElementById('sf-status'),
        category: document.getElementById('sf-category'),
        reset: document.getElementById('sf-reset'),
        createFeature: document.getElementById('sf-create-feature'),
        createCategory: document.getElementById('sf-create-category'),
        featureEditor: document.getElementById('sf-feature-editor'),
        featureTitle: document.getElementById('sf-feature-editor-title'),
        featureForm: document.getElementById('sf-feature-form'),
        featureId: document.getElementById('sf-feature-id'),
        rowVersion: document.getElementById('sf-row-version'),
        featureCode: document.getElementById('sf-feature-code'),
        featureSlug: document.getElementById('sf-feature-slug'),
        displayName: document.getElementById('sf-display-name'),
        description: document.getElementById('sf-description'),
        editorStatus: document.getElementById('sf-editor-status'),
        editorCategory: document.getElementById('sf-editor-category'),
        sortOrder: document.getElementById('sf-sort-order'),
        flagKey: document.getElementById('sf-flag-key'),
        coreFeature: document.getElementById('sf-core-feature'),
        planMappings: document.getElementById('sf-plan-mappings'),
        planMappingState: document.getElementById('sf-plan-mapping-state'),
        saveFeature: document.getElementById('sf-save-feature'),
        categoryEditor: document.getElementById('sf-category-editor'),
        categoryForm: document.getElementById('sf-category-form'),
        categoryCode: document.getElementById('sf-category-code'),
        categoryName: document.getElementById('sf-category-name'),
        categoryDescription: document.getElementById('sf-category-description'),
        categorySort: document.getElementById('sf-category-sort'),
        categoryStatus: document.getElementById('sf-category-status'),
        saveCategory: document.getElementById('sf-save-category')
    };

    const featureOffcanvas = els.featureEditor && window.bootstrap ? new window.bootstrap.Offcanvas(els.featureEditor) : null;
    const categoryModal = els.categoryEditor && window.bootstrap ? new window.bootstrap.Modal(els.categoryEditor) : null;

    const setVisible = (el, visible) => {
        if (el) el.classList.toggle('d-none', !visible);
    };

    const safe = (value) => (value === null || value === undefined ? '' : String(value));
    const escapeHtml = (value) => safe(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const showError = (message) => {
        if (!els.error) return;
        els.error.textContent = message || l10n().ErrorOccurred || 'Error';
        setVisible(els.error, true);
    };

    const showToast = (message, type) => {
        if (window.showToast) {
            window.showToast(message, type || 'info');
            return;
        }

        if (type === 'error') {
            showError(message);
        }
    };

    const parseEnvelope = async (response) => {
        const text = await response.text();
        if (!text) return {};
        try {
            return JSON.parse(text);
        } catch {
            return { errors: [text] };
        }
    };

    const envelopeData = (json) => json.data || json.Data || json;
    const envelopeErrors = (json) => json.errors || json.Errors || [];

    const api = async (url, options) => {
        const response = await fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
        const json = await parseEnvelope(response);
        if (!response.ok || json.isSuccessful === false || json.IsSuccessful === false) {
            const errors = envelopeErrors(json);
            const message = errors.length ? errors.join(' ') : response.status === 403 ? l10n().PermissionDenied : l10n().GatewayError;
            const error = new Error(message || 'request_failed');
            error.status = response.status;
            error.errors = errors;
            throw error;
        }

        return envelopeData(json);
    };

    const categoryName = (id) => {
        const category = state.categories.find((x) => safe(x.id || x.Id) === safe(id));
        return category ? safe(category.displayName || category.DisplayName) : '-';
    };

    const statusBadge = (status) => {
        const normalized = safe(status);
        const map = {
            Active: 'bg-label-success',
            Inactive: 'bg-label-secondary',
            Draft: 'bg-label-info',
            Deprecated: 'bg-label-warning',
            Archived: 'bg-label-dark'
        };
        const label = l10n()[normalized] || normalized;
        return `<span class="badge ${map[normalized] || 'bg-label-secondary'}">${escapeHtml(label)}</span>`;
    };

    const availabilityLabel = (status) => l10n()[safe(status)] || safe(status);

    const featureMappings = (featureId) => state.mappings.filter((x) => safe(x.featureDefinitionId || x.FeatureDefinitionId) === safe(featureId));

    const includedPlanLabels = (featureId) => {
        return featureMappings(featureId)
            .filter((x) => ['Included', 'AddOn', 'Preview'].includes(safe(x.availabilityStatus || x.AvailabilityStatus)))
            .map((x) => {
                const planName = x.subscriptionPlanName || x.SubscriptionPlanName;
                const status = x.availabilityStatus || x.AvailabilityStatus;
                return `${safe(planName)} (${availabilityLabel(status)})`;
            });
    };

    const renderCard = (feature) => {
        const id = feature.id || feature.Id;
        const status = feature.status || feature.Status;
        const archived = status === 'Archived';
        const includedPlans = includedPlanLabels(id);
        const plansHtml = includedPlans.length
            ? includedPlans.slice(0, 4).map((x) => `<span class="badge bg-label-primary me-1 mb-1">${escapeHtml(x)}</span>`).join('')
            : `<span class="text-muted small">${escapeHtml(l10n().NoPlans || '-')}</span>`;

        return `
        <div class="col-12 col-xl-6">
            <div class="card h-100 border shadow-sm">
                <div class="card-body p-4 d-flex flex-column">
                    <div class="d-flex align-items-start justify-content-between gap-3 mb-3">
                        <div class="min-w-0">
                            <div class="d-flex align-items-center flex-wrap gap-2 mb-2">
                                <h5 class="mb-0 text-truncate">${escapeHtml(feature.displayName || feature.DisplayName)}</h5>
                                ${statusBadge(status)}
                                ${(feature.isCoreFeature || feature.IsCoreFeature) ? `<span class="badge bg-label-info">${escapeHtml(l10n().IsCoreFeature || 'Core')}</span>` : ''}
                            </div>
                            <div class="small text-muted">${escapeHtml(feature.featureCode || feature.FeatureCode)} · ${escapeHtml(feature.featureSlug || feature.FeatureSlug)}</div>
                        </div>
                        <div class="d-flex align-items-center gap-1 flex-shrink-0">
                            <button type="button" class="btn btn-icon btn-sm btn-label-secondary js-edit" data-id="${escapeHtml(id)}" title="${escapeHtml(l10n().Edit || 'Edit')}">
                                <i class="bx bx-edit"></i>
                            </button>
                            <button type="button" class="btn btn-icon btn-sm btn-label-secondary js-deactivate" data-id="${escapeHtml(id)}" data-row-version="${escapeHtml(feature.rowVersion || feature.RowVersion || '')}" ${archived ? 'disabled' : ''} title="${escapeHtml(archived ? l10n().CannotArchiveTooltip : l10n().Deactivate)}">
                                <i class="bx bx-block"></i>
                            </button>
                            <button type="button" class="btn btn-icon btn-sm btn-label-danger js-archive" data-id="${escapeHtml(id)}" data-row-version="${escapeHtml(feature.rowVersion || feature.RowVersion || '')}" ${archived ? 'disabled' : ''} title="${escapeHtml(archived ? l10n().CannotArchiveTooltip : l10n().Archive)}">
                                <i class="bx bx-archive-in"></i>
                            </button>
                        </div>
                    </div>
                    <p class="text-muted mb-4">${escapeHtml(feature.description || feature.Description || '') || '&nbsp;'}</p>
                    <div class="row g-3 mt-auto">
                        <div class="col-12 col-md-5">
                            <small class="text-muted d-block mb-1">${escapeHtml(l10n().Category || 'Category')}</small>
                            <span class="fw-semibold">${escapeHtml(categoryName(feature.categoryId || feature.CategoryId))}</span>
                        </div>
                        <div class="col-12 col-md-7">
                            <small class="text-muted d-block mb-1">${escapeHtml(l10n().IncludedPlans || 'Plans')}</small>
                            <div>${plansHtml}</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>`;
    };

    const refreshCategoryOptions = () => {
        const options = [`<option value="">${escapeHtml(l10n().AllCategories || 'All Categories')}</option>`]
            .concat(state.categories.map((category) => `<option value="${escapeHtml(category.id || category.Id)}">${escapeHtml(category.displayName || category.DisplayName)}</option>`));
        if (els.category) els.category.innerHTML = options.join('');
        if (els.editorCategory) els.editorCategory.innerHTML = options.join('');
    };

    const loadCategories = async () => {
        const data = await api('/Platform/SubscriptionFeatures/api/categories');
        state.categories = Array.isArray(data) ? data : [];
        refreshCategoryOptions();
    };

    const loadPlans = async () => {
        const data = await api('/Platform/SubscriptionFeatures/api/plans/active');
        state.plans = Array.isArray(data) ? data : [];
    };

    const loadFeatures = async () => {
        const params = new URLSearchParams();
        params.set('page', '1');
        params.set('pageSize', '200');
        params.set('sort', 'sortOrder');
        if (els.search && els.search.value.trim()) params.set('search', els.search.value.trim());
        if (els.status && els.status.value) params.set('status', els.status.value);
        if (els.category && els.category.value) params.set('categoryId', els.category.value);

        const data = await api(`/Platform/SubscriptionFeatures/api?${params.toString()}`);
        state.features = data.items || data.Items || [];
        const mappingLists = await Promise.all(state.features.map((feature) => {
            const id = feature.id || feature.Id;
            return api(`/Platform/SubscriptionFeatures/api/${id}/plan-mappings`).catch(() => []);
        }));
        state.mappings = mappingLists.flat();
    };

    const render = () => {
        if (!els.grid) return;
        const hasFilters = !!((els.search && els.search.value.trim()) || (els.status && els.status.value) || (els.category && els.category.value));
        setVisible(els.empty, state.features.length === 0 && !hasFilters);
        setVisible(els.noResults, state.features.length === 0 && hasFilters);
        els.grid.innerHTML = state.features.map(renderCard).join('');
    };

    const refresh = async () => {
        setVisible(els.error, false);
        setVisible(els.loading, true);
        setVisible(els.empty, false);
        setVisible(els.noResults, false);
        if (els.grid) els.grid.innerHTML = '';

        try {
            await Promise.all([loadCategories(), loadPlans()]);
            await loadFeatures();
            render();
        } catch (error) {
            showError(error.message || l10n().GatewayError);
        } finally {
            setVisible(els.loading, false);
        }
    };

    const clearInvalid = (form) => {
        if (!form) return;
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        form.querySelectorAll('.invalid-feedback').forEach((el) => { el.textContent = ''; });
    };

    const setInvalid = (input, message) => {
        if (!input) return;
        input.classList.add('is-invalid');
        const feedback = input.parentElement ? input.parentElement.querySelector('.invalid-feedback') : null;
        if (feedback) feedback.textContent = message;
    };

    const validateFeatureForm = () => {
        clearInvalid(els.featureForm);
        let valid = true;
        const required = [
            [els.featureCode, l10n().RequiredField],
            [els.featureSlug, l10n().RequiredField],
            [els.displayName, l10n().RequiredField],
            [els.editorStatus, l10n().RequiredField]
        ];

        required.forEach(([input, message]) => {
            if (!input || !input.value.trim()) {
                setInvalid(input, message);
                valid = false;
            }
        });

        if (els.editorStatus && els.editorStatus.value === 'Active' && els.editorCategory && !els.editorCategory.value) {
            setInvalid(els.editorCategory, l10n().CategoryRequiredForActive);
            valid = false;
        }

        return valid;
    };

    const renderPlanMappings = (feature) => {
        if (!els.planMappings || !els.planMappingState) return;
        if (state.plans.length === 0) {
            els.planMappings.innerHTML = `<div class="text-muted small">${escapeHtml(l10n().NoPlans || '-')}</div>`;
            els.planMappingState.textContent = '';
            return;
        }

        const archived = feature && (feature.status || feature.Status) === 'Archived';
        els.planMappingState.textContent = archived ? l10n().CannotMapArchivedTooltip || '' : '';
        const mappings = feature ? featureMappings(feature.id || feature.Id) : [];

        els.planMappings.innerHTML = state.plans.map((plan) => {
            const planId = plan.id || plan.Id;
            const mapping = mappings.find((x) => safe(x.subscriptionPlanId || x.SubscriptionPlanId) === safe(planId));
            const status = mapping ? safe(mapping.availabilityStatus || mapping.AvailabilityStatus) : 'NotAvailable';
            return `
            <div class="border rounded p-3">
                <div class="d-flex align-items-center justify-content-between gap-3">
                    <div class="min-w-0">
                        <div class="fw-semibold text-truncate">${escapeHtml(plan.name || plan.Name)}</div>
                        <small class="text-muted">${escapeHtml(plan.code || plan.Code)}</small>
                    </div>
                    <select class="form-select form-select-sm sf-plan-status" data-plan-id="${escapeHtml(planId)}" data-row-version="${escapeHtml(mapping ? (mapping.rowVersion || mapping.RowVersion || '') : '')}" ${archived ? 'disabled' : ''}>
                        ${['Included', 'AddOn', 'NotAvailable', 'Preview'].map((item) => `<option value="${item}" ${item === status ? 'selected' : ''}>${escapeHtml(availabilityLabel(item))}</option>`).join('')}
                    </select>
                </div>
            </div>`;
        }).join('');
    };

    const resetFeatureForm = () => {
        state.editingFeature = null;
        if (els.featureTitle) els.featureTitle.textContent = l10n().CreateFeature || 'Create Feature';
        if (els.featureForm) els.featureForm.reset();
        if (els.featureId) els.featureId.value = '';
        if (els.rowVersion) els.rowVersion.value = '';
        if (els.editorStatus) els.editorStatus.value = 'Draft';
        clearInvalid(els.featureForm);
        renderPlanMappings(null);
    };

    const openCreateFeature = () => {
        resetFeatureForm();
        featureOffcanvas && featureOffcanvas.show();
    };

    const openEditFeature = async (id) => {
        try {
            const feature = await api(`/Platform/SubscriptionFeatures/api/${id}`);
            state.editingFeature = feature;
            if (els.featureTitle) els.featureTitle.textContent = l10n().EditFeature || 'Edit Feature';
            if (els.featureId) els.featureId.value = feature.id || feature.Id || '';
            if (els.rowVersion) els.rowVersion.value = feature.rowVersion || feature.RowVersion || '';
            if (els.featureCode) els.featureCode.value = feature.featureCode || feature.FeatureCode || '';
            if (els.featureSlug) els.featureSlug.value = feature.featureSlug || feature.FeatureSlug || '';
            if (els.displayName) els.displayName.value = feature.displayName || feature.DisplayName || '';
            if (els.description) els.description.value = feature.description || feature.Description || '';
            if (els.editorStatus) els.editorStatus.value = feature.status || feature.Status || 'Draft';
            if (els.editorCategory) els.editorCategory.value = feature.categoryId || feature.CategoryId || '';
            if (els.sortOrder) els.sortOrder.value = feature.sortOrder ?? feature.SortOrder ?? '';
            if (els.flagKey) els.flagKey.value = feature.optionalFeatureFlagKey || feature.OptionalFeatureFlagKey || '';
            if (els.coreFeature) els.coreFeature.checked = !!(feature.isCoreFeature || feature.IsCoreFeature);
            clearInvalid(els.featureForm);
            renderPlanMappings(feature);
            featureOffcanvas && featureOffcanvas.show();
        } catch (error) {
            showError(error.message || l10n().GatewayError);
        }
    };

    const buildFeaturePayload = () => ({
        featureCode: els.featureCode.value.trim(),
        featureSlug: els.featureSlug.value.trim(),
        displayName: els.displayName.value.trim(),
        description: els.description.value.trim() || null,
        categoryId: els.editorCategory.value || null,
        status: els.editorStatus.value,
        isCoreFeature: !!els.coreFeature.checked,
        sortOrder: els.sortOrder.value === '' ? null : Number(els.sortOrder.value),
        optionalFeatureFlagKey: els.flagKey.value.trim() || null,
        rowVersion: els.rowVersion.value || null
    });

    const saveFeatureMappings = async (featureId, isArchived) => {
        if (isArchived || !els.planMappings) return;
        const selects = Array.from(els.planMappings.querySelectorAll('.sf-plan-status'));
        await Promise.all(selects.map((select) => {
            const planId = select.getAttribute('data-plan-id');
            const rowVersion = select.getAttribute('data-row-version') || null;
            return api(`/Platform/SubscriptionFeatures/api/plans/${planId}/features`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    mappings: [{
                        featureDefinitionId: featureId,
                        availabilityStatus: select.value,
                        effectiveFromUtc: null,
                        effectiveToUtc: null,
                        rowVersion
                    }]
                })
            });
        }));
    };

    const mapApiErrorsToFields = (error) => {
        const messages = error.errors && error.errors.length ? error.errors : [error.message];
        messages.forEach((message) => {
            if (/FeatureCode/i.test(message)) {
                setInvalid(els.featureCode, message);
            } else if (/FeatureSlug/i.test(message)) {
                setInvalid(els.featureSlug, message);
            } else if (/CategoryId|category/i.test(message)) {
                setInvalid(els.editorCategory, message);
            }
        });
        showError(messages.join(' '));
    };

    const saveFeature = async () => {
        if (!validateFeatureForm()) return;
        const payload = buildFeaturePayload();
        const id = els.featureId.value;
        els.saveFeature.disabled = true;

        try {
            let featureId = id;
            if (id) {
                await api(`/Platform/SubscriptionFeatures/api/${id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
            } else {
                const result = await api('/Platform/SubscriptionFeatures/api', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                featureId = result;
            }

            await saveFeatureMappings(featureId, payload.status === 'Archived');
            showToast(id ? l10n().RecordUpdated : l10n().RecordCreated, 'success');
            featureOffcanvas && featureOffcanvas.hide();
            await refresh();
        } catch (error) {
            mapApiErrorsToFields(error);
        } finally {
            els.saveFeature.disabled = false;
        }
    };

    const openCreateCategory = () => {
        if (els.categoryForm) els.categoryForm.reset();
        clearInvalid(els.categoryForm);
        if (els.categoryStatus) els.categoryStatus.value = 'Active';
        categoryModal && categoryModal.show();
    };

    const saveCategory = async () => {
        clearInvalid(els.categoryForm);
        let valid = true;
        if (!els.categoryCode.value.trim()) {
            setInvalid(els.categoryCode, l10n().RequiredField);
            valid = false;
        }
        if (!els.categoryName.value.trim()) {
            setInvalid(els.categoryName, l10n().RequiredField);
            valid = false;
        }
        if (!valid) return;

        els.saveCategory.disabled = true;
        try {
            await api('/Platform/SubscriptionFeatures/api/categories', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    categoryCode: els.categoryCode.value.trim(),
                    displayName: els.categoryName.value.trim(),
                    description: els.categoryDescription.value.trim() || null,
                    sortOrder: els.categorySort.value === '' ? null : Number(els.categorySort.value),
                    status: els.categoryStatus.value
                })
            });
            showToast(l10n().RecordCreated, 'success');
            categoryModal && categoryModal.hide();
            await refresh();
        } catch (error) {
            showError(error.message || l10n().GatewayError);
        } finally {
            els.saveCategory.disabled = false;
        }
    };

    const postStatusAction = async (id, rowVersion, action) => {
        await api(`/Platform/SubscriptionFeatures/api/${id}/${action}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ rowVersion: rowVersion || null })
        });
        showToast(l10n().RecordUpdated, 'success');
        await refresh();
    };

    const bindEvents = () => {
        els.createFeature && els.createFeature.addEventListener('click', openCreateFeature);
        els.createCategory && els.createCategory.addEventListener('click', openCreateCategory);
        els.saveFeature && els.saveFeature.addEventListener('click', saveFeature);
        els.saveCategory && els.saveCategory.addEventListener('click', saveCategory);
        els.reset && els.reset.addEventListener('click', () => {
            if (els.search) els.search.value = '';
            if (els.status) els.status.value = '';
            if (els.category) els.category.value = '';
            refresh();
        });

        let searchTimer = null;
        els.search && els.search.addEventListener('input', () => {
            window.clearTimeout(searchTimer);
            searchTimer = window.setTimeout(refresh, 250);
        });
        els.status && els.status.addEventListener('change', refresh);
        els.category && els.category.addEventListener('change', refresh);

        document.addEventListener('click', async (event) => {
            const edit = event.target.closest('.js-edit');
            if (edit) {
                event.preventDefault();
                openEditFeature(edit.getAttribute('data-id'));
                return;
            }

            const archive = event.target.closest('.js-archive');
            if (archive && !archive.disabled) {
                event.preventDefault();
                archive.disabled = true;
                try {
                    await postStatusAction(archive.getAttribute('data-id'), archive.getAttribute('data-row-version'), 'archive');
                } catch (error) {
                    showError(error.message || l10n().GatewayError);
                } finally {
                    archive.disabled = false;
                }
                return;
            }

            const deactivate = event.target.closest('.js-deactivate');
            if (deactivate && !deactivate.disabled) {
                event.preventDefault();
                deactivate.disabled = true;
                try {
                    await postStatusAction(deactivate.getAttribute('data-id'), deactivate.getAttribute('data-row-version'), 'deactivate');
                } catch (error) {
                    showError(error.message || l10n().GatewayError);
                } finally {
                    deactivate.disabled = false;
                }
            }
        });
    };

    bindEvents();
    refresh();
})();
