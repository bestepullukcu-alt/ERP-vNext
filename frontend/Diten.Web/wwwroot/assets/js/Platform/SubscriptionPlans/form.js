'use strict';

(function () {
    const l10n = () => window.L10n || {};
    const availabilityValues = ['Included', 'AddOn', 'Preview', 'NotAvailable'];
    const availabilityClass = {
        Included: 'border-success',
        AddOn: 'border-warning',
        Preview: 'border-info',
        NotAvailable: 'border-light'
    };

    const state = {
        features: [],
        categories: [],
        modules: [],
        selectedModuleKeys: new Set(),
        mappings: new Map(),
        disabled: false
    };

    const els = {
        form: document.getElementById('subscriptionPlanForm'),
        entitlementJson: document.getElementById('sp-entitlement-mappings-json'),
        error: document.getElementById('sp-entitlements-error'),
        loading: document.getElementById('sp-entitlements-loading'),
        empty: document.getElementById('sp-entitlements-empty'),
        list: document.getElementById('sp-entitlements-list'),
        search: document.getElementById('sp-feature-search'),
        category: document.getElementById('sp-feature-category'),
        availability: document.getElementById('sp-feature-availability'),
        includedModuleKeys: document.getElementById('sp-included-module-keys'),
        moduleLoading: document.getElementById('sp-modules-loading'),
        moduleEmpty: document.getElementById('sp-modules-empty'),
        moduleSelect: document.getElementById('subscription-plan-module-select'),
        trialToggle: document.getElementById('is-trial-plan'),
        trialDays: document.getElementById('trial-duration-days'),
        quotasJson: document.getElementById('sp-default-quotas-json'),
        quotaInputs: Array.from(document.querySelectorAll('.sp-quota-input'))
    };

    const safe = (value) => (value === null || value === undefined ? '' : String(value));
    const setVisible = (el, visible) => el && el.classList.toggle('d-none', !visible);

    const escapeHtml = (value) => safe(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');

    const label = (key) => l10n()[key] || {
        Included: 'Included',
        AddOn: 'Add-on',
        Preview: 'Preview',
        NotAvailable: 'Not available',
        AllCategories: 'All categories',
        AllFeatures: 'All features',
        Archived: 'Archived',
        GatewayError: 'The request could not be completed.',
        NoFeaturesAvailable: 'No active subscription features found.'
    }[key] || key;

    const normalizeModuleKey = (value) => safe(value).trim().toUpperCase();

    const parseEnvelope = async (response) => {
        const text = await response.text();
        if (!text) return null;
        try {
            const json = JSON.parse(text);
            if (!response.ok || json.isSuccessful === false || json.IsSuccessful === false) {
                const errors = json.errors || json.Errors || [];
                throw new Error(errors.length ? errors.join(' ') : label('GatewayError'));
            }

            return json.data || json.Data || json;
        } catch (error) {
            if (error instanceof SyntaxError) {
                throw new Error(label('GatewayError'));
            }

            throw error;
        }
    };

    const api = async (url) => {
        const response = await fetch(url, { credentials: 'same-origin' });
        return parseEnvelope(response);
    };

    const categoryName = (categoryId) => {
        const category = state.categories.find((item) => safe(item.id || item.Id) === safe(categoryId));
        return category ? safe(category.displayName || category.DisplayName) : '';
    };

    const mappingFor = (featureId) => state.mappings.get(safe(featureId)) || {
        availabilityStatus: 'NotAvailable',
        rowVersion: null
    };

    const currentRows = () => Array.from(els.list ? els.list.querySelectorAll('.sp-feature-status') : []);

    const serializeMappings = () => {
        if (!els.entitlementJson) return;
        if (state.disabled) {
            els.entitlementJson.value = '';
            return;
        }

        const mappings = state.features.map((feature) => {
            const featureId = safe(feature.id || feature.Id);
            const mapping = mappingFor(featureId);
            return {
                featureDefinitionId: featureId,
                availabilityStatus: safe(mapping.availabilityStatus || mapping.AvailabilityStatus || 'NotAvailable'),
                effectiveFromUtc: null,
                effectiveToUtc: null,
                rowVersion: mapping.rowVersion || mapping.RowVersion || null
            };
        });

        els.entitlementJson.value = JSON.stringify(mappings);
    };

    const syncVisibleRowsToState = () => {
        currentRows().forEach((select) => {
            const featureId = safe(select.getAttribute('data-feature-id'));
            const existing = mappingFor(featureId);
            state.mappings.set(featureId, {
                featureDefinitionId: featureId,
                availabilityStatus: select.value || 'NotAvailable',
                effectiveFromUtc: null,
                effectiveToUtc: null,
                rowVersion: existing.rowVersion || existing.RowVersion || null
            });
        });
    };

    const filteredFeatures = () => {
        const search = safe(els.search && els.search.value).trim().toLowerCase();
        const categoryId = safe(els.category && els.category.value);
        const availability = safe(els.availability && els.availability.value);

        return state.features.filter((feature) => {
            const id = feature.id || feature.Id;
            const mapping = mappingFor(id);
            const featureCategoryId = safe(feature.categoryId || feature.CategoryId);
            const haystack = [
                feature.featureCode || feature.FeatureCode,
                feature.featureSlug || feature.FeatureSlug,
                feature.displayName || feature.DisplayName,
                feature.description || feature.Description
            ].map(safe).join(' ').toLowerCase();

            return (!search || haystack.includes(search))
                && (!categoryId || featureCategoryId === categoryId)
                && (!availability || safe(mapping.availabilityStatus || mapping.AvailabilityStatus) === availability);
        });
    };

    const render = () => {
        if (!els.list) return;
        const items = filteredFeatures();
        setVisible(els.empty, state.features.length === 0);

        if (items.length === 0) {
            els.list.innerHTML = state.features.length === 0 ? '' : `<div class="text-muted small">${escapeHtml(label('NoFeaturesAvailable'))}</div>`;
            serializeMappings();
            return;
        }

        els.list.innerHTML = items.map((feature) => {
            const id = feature.id || feature.Id;
            const status = feature.status || feature.Status;
            const archived = status === 'Archived';
            const mapping = mappingFor(id);
            const selected = safe(mapping.availabilityStatus || mapping.AvailabilityStatus || 'NotAvailable');
            const rowVersion = safe(mapping.rowVersion || mapping.RowVersion);
            const disabled = state.disabled || archived;
            const category = categoryName(feature.categoryId || feature.CategoryId);

            return `
                <div class="border rounded p-3 ${availabilityClass[selected] || 'border-light'}">
                    <div class="d-flex flex-column flex-lg-row align-items-lg-center justify-content-between gap-3">
                        <div class="min-w-0">
                            <div class="d-flex align-items-center flex-wrap gap-2 mb-1">
                                <span class="fw-semibold text-truncate">${escapeHtml(feature.displayName || feature.DisplayName)}</span>
                                ${category ? `<span class="badge bg-label-secondary">${escapeHtml(category)}</span>` : ''}
                                ${archived ? `<span class="badge bg-label-danger">${escapeHtml(label('Archived'))}</span>` : ''}
                            </div>
                            <small class="text-muted">${escapeHtml(feature.featureCode || feature.FeatureCode)} · ${escapeHtml(feature.featureSlug || feature.FeatureSlug)}</small>
                        </div>
                        <select class="form-select form-select-sm sp-feature-status" data-feature-id="${escapeHtml(id)}" data-row-version="${escapeHtml(rowVersion)}" ${disabled ? 'disabled' : ''}>
                            ${availabilityValues.map((item) => `<option value="${item}" ${item === selected ? 'selected' : ''}>${escapeHtml(label(item))}</option>`).join('')}
                        </select>
                    </div>
                </div>`;
        }).join('');

        serializeMappings();
    };

    const parseSelectedModuleKeys = () => {
        state.selectedModuleKeys = new Set(safe(els.includedModuleKeys?.value)
            .split(/[\r\n,]+/)
            .map(normalizeModuleKey)
            .filter(Boolean));
    };

    const serializeModuleKeys = () => {
        if (!els.includedModuleKeys) return;
        els.includedModuleKeys.value = Array.from(state.selectedModuleKeys).sort((a, b) => a.localeCompare(b)).join('\n');
    };

    const serializeQuotas = () => {
        if (!els.quotasJson) return;
        const quotas = {};
        els.quotaInputs.forEach((input) => {
            const key = input.getAttribute('data-quota-key');
            const raw = safe(input.value).trim();
            if (!key || raw.length === 0) return;
            const value = Number(raw);
            if (Number.isFinite(value) && value >= 0) {
                quotas[key] = value;
            }
        });

        els.quotasJson.value = Object.keys(quotas).length === 0 ? '' : JSON.stringify(quotas);
    };

    const renderModules = () => {
        if (!els.moduleSelect) return;
        const items = state.modules;
        setVisible(els.moduleEmpty, state.modules.length === 0);

        if (items.length === 0) {
            els.moduleSelect.innerHTML = '';
            serializeModuleKeys();
            return;
        }

        const groups = items.reduce((acc, module) => {
            const domain = safe(module.domain || module.Domain || 'Other') || 'Other';
            if (!acc.has(domain)) acc.set(domain, []);
            acc.get(domain).push(module);
            return acc;
        }, new Map());

        els.moduleSelect.innerHTML = Array.from(groups.entries())
            .sort(([a], [b]) => a.localeCompare(b))
            .map(([domain, modules]) => {
                const options = modules
                    .sort((a, b) => safe(a.moduleCode || a.ModuleCode).localeCompare(safe(b.moduleCode || b.ModuleCode)))
                    .map((module) => {
                        const moduleCode = normalizeModuleKey(module.moduleCode || module.ModuleCode);
                        const displayName = module.displayName || module.DisplayName || module.moduleName || module.ModuleName || moduleCode;
                        const service = module.service || module.Service;
                        const text = service ? `${displayName} (${moduleCode}) - ${service}` : `${displayName} (${moduleCode})`;
                        return `<option value="${escapeHtml(moduleCode)}" ${state.selectedModuleKeys.has(moduleCode) ? 'selected' : ''}>${escapeHtml(text)}</option>`;
                    }).join('');
                return `<optgroup label="${escapeHtml(domain)}">${options}</optgroup>`;
            }).join('');

        if (window.jQuery?.fn?.select2) {
            const $select = window.jQuery(els.moduleSelect);
            if ($select.hasClass('select2-hidden-accessible')) {
                $select.trigger('change.select2');
            } else {
                $select.wrap('<div class="position-relative"></div>').select2({
                    dropdownParent: $select.parent(),
                    placeholder: els.moduleSelect.dataset.placeholder || label('AllModules'),
                    width: '100%'
                });
            }

            $select.off('change.subscriptionPlanModules').on('change.subscriptionPlanModules', syncSelectedModulesFromSelect);
        }

        syncSelectedModulesFromSelect();
    };

    const syncSelectedModulesFromSelect = () => {
        if (!els.moduleSelect) return;
        state.selectedModuleKeys = new Set(Array.from(els.moduleSelect.selectedOptions || [])
            .map((option) => normalizeModuleKey(option.value))
            .filter(Boolean));
        serializeModuleKeys();
    };

    const refreshCategoryOptions = () => {
        if (!els.category) return;
        const current = els.category.value;
        els.category.innerHTML = `<option value="">${escapeHtml(label('AllCategories'))}</option>`
            + state.categories.map((category) => {
                const id = category.id || category.Id;
                const name = category.displayName || category.DisplayName;
                return `<option value="${escapeHtml(id)}">${escapeHtml(name)}</option>`;
            }).join('');
        els.category.value = current;
    };

    const loadEntitlements = async () => {
        if (!els.form || !els.list) return;
        const planId = safe(els.form.getAttribute('data-plan-id'));
        state.disabled = safe(els.form.getAttribute('data-plan-active')) === 'false';
        parseSelectedModuleKeys();

        setVisible(els.error, false);
        setVisible(els.loading, true);
        setVisible(els.moduleLoading, true);

        try {
            const [categoryData, featureData, mappingData, moduleData] = await Promise.all([
                api('/Platform/SubscriptionPlans/api/features/categories?status=Active').catch(() => []),
                api('/Platform/SubscriptionPlans/api/features/catalog?page=1&pageSize=500&sort=sortOrder'),
                planId ? api(`/Platform/SubscriptionPlans/api/${planId}/features`).catch(() => []) : Promise.resolve([]),
                api('/Platform/SubscriptionPlans/api/module-catalog?page=1&pageSize=200&sort=sortOrder').catch(() => null)
            ]);

            state.categories = Array.isArray(categoryData) ? categoryData : [];
            const featurePayload = featureData || {};
            state.mappings = new Map((Array.isArray(mappingData) ? mappingData : []).map((mapping) => [
                safe(mapping.featureDefinitionId || mapping.FeatureDefinitionId),
                mapping
            ]));
            const allFeatures = featurePayload.items || featurePayload.Items || (Array.isArray(featurePayload) ? featurePayload : []);
            state.features = allFeatures.filter((feature) => {
                const id = safe(feature.id || feature.Id);
                const status = feature.status || feature.Status;
                return status === 'Active' || state.mappings.has(id);
            });
            const modulePayload = moduleData || {};
            state.modules = modulePayload.items || modulePayload.Items || (Array.isArray(modulePayload) ? modulePayload : []);

            refreshCategoryOptions();
            render();
            renderModules();
        } catch (error) {
            if (els.error) {
                els.error.textContent = error.message || label('GatewayError');
            }
            setVisible(els.error, true);
        } finally {
            setVisible(els.loading, false);
            setVisible(els.moduleLoading, false);
        }
    };

    const applyTrialToggle = () => {
        if (!els.trialToggle || !els.trialDays) return;
        const enabled = !!els.trialToggle.checked;
        els.trialDays.disabled = !enabled;
        if (!enabled) {
            els.trialDays.value = '';
        }
    };

    if (els.trialToggle) {
        els.trialToggle.addEventListener('change', applyTrialToggle);
        applyTrialToggle();
    }

    [els.search, els.category, els.availability].forEach((el) => {
        el && el.addEventListener(el === els.search ? 'input' : 'change', render);
    });

    els.moduleSelect && els.moduleSelect.addEventListener('change', syncSelectedModulesFromSelect);

    els.list && els.list.addEventListener('change', (event) => {
        if (event.target && event.target.classList.contains('sp-feature-status')) {
            syncVisibleRowsToState();
            serializeMappings();
        }
    });

    const forms = document.querySelectorAll('.needs-validation');
    Array.from(forms).forEach((form) => {
        form.addEventListener('submit', (event) => {
            syncVisibleRowsToState();
            syncSelectedModulesFromSelect();
            serializeMappings();
            serializeModuleKeys();
            serializeQuotas();
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            form.classList.add('was-validated');
        }, false);
    });

    els.quotaInputs.forEach((input) => input.addEventListener('input', serializeQuotas));
    serializeQuotas();

    loadEntitlements();
})();
