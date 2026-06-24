'use strict';

(function () {
    const l10n = () => window.L10n || {};
    const availabilityValues = ['Included', 'AddOn', 'Preview', 'NotAvailable'];

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
        trialWrapper: document.getElementById('trial-duration-wrapper'),
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

    // Toggle the reusable `.is-selected` row marker (leading bullet) based on
    // the row's current availability — anything other than "NotAvailable" is
    // considered selected/active.
    const applyRowSelectedMarker = (select) => {
        const row = select.closest('.sp-entitlement-row');
        if (row) {
            row.classList.toggle('is-selected', (select.value || 'NotAvailable') !== 'NotAvailable');
        }
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
            els.list.innerHTML = state.features.length === 0 ? '' : `<div class="text-muted small py-2">${escapeHtml(label('NoFeaturesAvailable'))}</div>`;
            serializeMappings();
            initRowSelect2();
            return;
        }

        els.list.innerHTML = `<div class="list-group sp-entitlement-list">${items.map((feature) => {
            const id = feature.id || feature.Id;
            const status = feature.status || feature.Status;
            const archived = status === 'Archived';
            const mapping = mappingFor(id);
            const selected = safe(mapping.availabilityStatus || mapping.AvailabilityStatus || 'NotAvailable');
            const rowVersion = safe(mapping.rowVersion || mapping.RowVersion);
            const disabled = state.disabled || archived;
            const category = categoryName(feature.categoryId || feature.CategoryId);

            return `
                <div class="list-group-item sp-entitlement-row selectable-row${selected !== 'NotAvailable' ? ' is-selected' : ''}${archived ? ' opacity-75' : ''}">
                    <div class="sp-entitlement-row__info">
                        <div class="d-flex align-items-center flex-wrap gap-2">
                            <span class="fw-medium text-truncate">${escapeHtml(feature.displayName || feature.DisplayName)}</span>
                            ${category ? `<span class="badge bg-label-secondary">${escapeHtml(category)}</span>` : ''}
                            ${archived ? `<span class="badge bg-label-danger">${escapeHtml(label('Archived'))}</span>` : ''}
                        </div>
                        <small class="text-muted">${escapeHtml(feature.featureCode || feature.FeatureCode)} · ${escapeHtml(feature.featureSlug || feature.FeatureSlug)}</small>
                    </div>
                    <div class="sp-entitlement-status">
                        <select class="form-select form-select-sm sp-feature-status" data-feature-id="${escapeHtml(id)}" data-row-version="${escapeHtml(rowVersion)}" ${disabled ? 'disabled' : ''}>
                            ${availabilityValues.map((item) => `<option value="${item}" ${item === selected ? 'selected' : ''}>${escapeHtml(label(item))}</option>`).join('')}
                        </select>
                    </div>
                </div>`;
        }).join('')}</div>`;

        serializeMappings();
        initRowSelect2();
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

    // Initialize the static enum dropdowns (category/availability filters + currency)
    // as Select2, matching the project form convention. The module multi-select keeps
    // its own dedicated init in renderModules and is excluded here.
    // IMPORTANT: scope to `select.select2` only. Select2 generates a sibling
    // `<span class="select2 select2-container">` for every widget (including the
    // per-row availability selects), so a bare `.select2` selector would re-run
    // select2() on those spans and produce a broken "No results found" widget.
    const initStaticSelect2 = () => {
        if (!window.jQuery?.fn?.select2) return;
        window.jQuery('select.select2').not('#subscription-plan-module-select').each(function () {
            const $el = window.jQuery(this);
            if ($el.hasClass('select2-hidden-accessible')) {
                $el.trigger('change.select2');
                return;
            }
            $el.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $el.parent(),
                width: '100%'
            });
        });
    };

    // Convert the per-row availability selects into Select2 using the exact
    // GoldenReferenceCompact inline-filter single-select config (#filterPriority):
    // body-mounted dropdown so it is never clipped by the entitlements card,
    // sm selection box, no search box for this short enum list, width:'element'.
    const initRowSelect2 = () => {
        if (!window.jQuery?.fn?.select2 || !els.list) return;
        const $body = window.jQuery(document.body);
        window.jQuery(els.list).find('.sp-feature-status').each(function () {
            const $s = window.jQuery(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element'
            });
            $s.off('change.spStatus').on('change.spStatus', () => {
                applyRowSelectedMarker(this);
                syncVisibleRowsToState();
                serializeMappings();
            });
        });
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
            initStaticSelect2();
        }
    };

    const applyTrialToggle = () => {
        if (!els.trialToggle || !els.trialDays) return;
        const enabled = !!els.trialToggle.checked;
        setVisible(els.trialWrapper, enabled);
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
            applyRowSelectedMarker(event.target);
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
