'use strict';

/**
 * Subscription Features — Feature Compact full-page form (Create / Edit).
 * No-ViewModel: data is loaded and submitted via AJAX through the same-origin proxy.
 */
(function () {
    const l10n = () => window.L10n || {};
    const formEl = document.getElementById('sf-feature-page-form');
    if (!formEl) return;

    const state = { plans: [], categories: [], mappings: [], feature: null };
    const featureId = (formEl.getAttribute('data-feature-id') || '').trim();
    const isEdit = !!featureId;

    const els = {
        alert: document.getElementById('sf-page-alert'),
        save: document.getElementById('sf-page-save'),
        id: document.getElementById('sf-feature-id'),
        rowVersion: document.getElementById('sf-row-version'),
        code: document.getElementById('sf-feature-code'),
        slug: document.getElementById('sf-feature-slug'),
        name: document.getElementById('sf-display-name'),
        description: document.getElementById('sf-description'),
        status: document.getElementById('sf-editor-status'),
        category: document.getElementById('sf-editor-category'),
        sort: document.getElementById('sf-sort-order'),
        flag: document.getElementById('sf-flag-key'),
        core: document.getElementById('sf-core-feature'),
        planMappings: document.getElementById('sf-plan-mappings'),
        planMappingState: document.getElementById('sf-plan-mapping-state')
    };

    const safe = (v) => (v === null || v === undefined ? '' : String(v));
    const escapeHtml = (v) => safe(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const setVisible = (el, visible) => { if (el) el.classList.toggle('d-none', !visible); };
    const availabilityLabel = (s) => l10n()[safe(s)] || safe(s);

    const parseEnvelope = async (response) => {
        const text = await response.text();
        if (!text) return {};
        try { return JSON.parse(text); } catch { return { errors: [text] }; }
    };
    const api = async (url, options) => {
        const response = await fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
        const json = await parseEnvelope(response);
        if (!response.ok || json.isSuccessful === false || json.IsSuccessful === false) {
            const errors = json.errors || json.Errors || [];
            const message = errors.length ? errors.join(' ') : response.status === 403 ? l10n().PermissionDenied : l10n().GatewayError;
            const error = new Error(message || 'request_failed');
            error.status = response.status;
            error.errors = errors;
            throw error;
        }
        return json.data || json.Data || json;
    };

    const clearInvalid = () => {
        formEl.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        formEl.querySelectorAll('.invalid-feedback').forEach((el) => { el.textContent = ''; });
    };
    const setInvalid = (input, message) => {
        if (!input) return;
        input.classList.add('is-invalid');
        const fb = input.closest('.col-12, .col-md-6')?.querySelector('.invalid-feedback') || input.parentElement?.querySelector('.invalid-feedback');
        if (fb) fb.textContent = message || '';
    };

    const featureMappings = (id) => state.mappings.filter((x) => safe(x.featureDefinitionId || x.FeatureDefinitionId) === safe(id));

    // Toggle the leading bullet marker based on availability (anything other
    // than "NotAvailable" is considered selected). Mirrors SubscriptionPlans.
    const applyRowSelectedMarker = (select) => {
        const row = select.closest('.sp-entitlement-row');
        if (row) row.classList.toggle('is-selected', (select.value || 'NotAvailable') !== 'NotAvailable');
    };

    // Per-row availability selects → Select2 (body-mounted, sm, no search),
    // exact config copied from SubscriptionPlans form.js initRowSelect2().
    const initRowSelect2 = () => {
        if (!window.jQuery || !$.fn.select2 || !els.planMappings) return;
        const $body = $(document.body);
        $(els.planMappings).find('.sf-plan-status').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element'
            });
            $s.off('change.sfPlan').on('change.sfPlan', function () { applyRowSelectedMarker(this); });
        });
    };

    const renderPlanMappings = () => {
        if (!els.planMappings) return;
        if (state.plans.length === 0) {
            els.planMappings.innerHTML = `<div class="text-muted small">${escapeHtml(l10n().NoPlans || '-')}</div>`;
            if (els.planMappingState) els.planMappingState.textContent = '';
            return;
        }
        const archived = els.status.value === 'Archived';
        if (els.planMappingState) els.planMappingState.textContent = archived ? (l10n().CannotMapArchivedTooltip || '') : '';
        const mappings = state.feature ? featureMappings(state.feature.id || state.feature.Id) : [];
        els.planMappings.innerHTML = `<div class="list-group sp-entitlement-list">${state.plans.map((plan) => {
            const planId = plan.id || plan.Id;
            const mapping = mappings.find((x) => safe(x.subscriptionPlanId || x.SubscriptionPlanId) === safe(planId));
            const status = mapping ? safe(mapping.availabilityStatus || mapping.AvailabilityStatus) : 'NotAvailable';
            const planActive = (plan.isActive ?? plan.IsActive) !== false;
            const rowDisabled = archived || !planActive;
            return `
            <div class="list-group-item sp-entitlement-row selectable-row${status !== 'NotAvailable' ? ' is-selected' : ''}${planActive ? '' : ' opacity-75'}">
                <div class="sp-entitlement-row__info">
                    <div class="d-flex align-items-center flex-wrap gap-2">
                        <span class="fw-medium text-truncate">${escapeHtml(plan.name || plan.Name)}</span>
                        ${planActive ? '' : `<span class="badge bg-label-secondary">${escapeHtml(l10n().Inactive || 'Passive')}</span>`}
                    </div>
                    <small class="text-muted">${escapeHtml(plan.code || plan.Code)}</small>
                </div>
                <div class="sp-entitlement-status">
                    <select class="form-select form-select-sm sf-plan-status" data-plan-id="${escapeHtml(planId)}" data-row-version="${escapeHtml(mapping ? (mapping.rowVersion || mapping.RowVersion || '') : '')}" ${rowDisabled ? 'disabled' : ''}>
                        ${['Included', 'AddOn', 'Preview', 'NotAvailable'].map((item) => `<option value="${item}" ${item === status ? 'selected' : ''}>${escapeHtml(availabilityLabel(item))}</option>`).join('')}
                    </select>
                </div>
            </div>`;
        }).join('')}</div>`;
        initRowSelect2();
    };

    const fillCategoryOptions = () => {
        if (!els.category) return;
        const options = [`<option value="">${escapeHtml(l10n().AllCategories || '-')}</option>`]
            .concat(state.categories.map((c) => `<option value="${escapeHtml(c.id || c.Id)}">${escapeHtml(c.displayName || c.DisplayName)}</option>`));
        els.category.innerHTML = options.join('');
    };

    const initSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        $('#sf-editor-status, #sf-editor-category').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({ width: '100%' });
        });
    };

    const fillFeature = (feature) => {
        if (els.id) els.id.value = safe(feature.id || feature.Id);
        if (els.rowVersion) els.rowVersion.value = safe(feature.rowVersion || feature.RowVersion);
        if (els.code) els.code.value = safe(feature.featureCode || feature.FeatureCode);
        if (els.slug) els.slug.value = safe(feature.featureSlug || feature.FeatureSlug);
        if (els.name) els.name.value = safe(feature.displayName || feature.DisplayName);
        if (els.description) els.description.value = safe(feature.description || feature.Description);
        if (els.status) $('#sf-editor-status').val(safe(feature.status || feature.Status) || 'Draft').trigger('change');
        if (els.category) $('#sf-editor-category').val(safe(feature.categoryId || feature.CategoryId)).trigger('change');
        if (els.sort) els.sort.value = feature.sortOrder ?? feature.SortOrder ?? '';
        if (els.flag) els.flag.value = safe(feature.optionalFeatureFlagKey || feature.OptionalFeatureFlagKey);
        if (els.core) els.core.checked = !!(feature.isCoreFeature || feature.IsCoreFeature);
    };

    const validate = () => {
        clearInvalid();
        let valid = true;
        [[els.code], [els.slug], [els.name], [els.status]].forEach(([input]) => {
            if (!input || !input.value.trim()) { setInvalid(input, l10n().RequiredField); valid = false; }
        });
        if (els.status.value === 'Active' && els.category && !els.category.value) {
            setInvalid(els.category, l10n().CategoryRequiredForActive);
            valid = false;
        }
        return valid;
    };

    const buildPayload = () => ({
        featureCode: els.code.value.trim(),
        featureSlug: els.slug.value.trim(),
        displayName: els.name.value.trim(),
        description: els.description.value.trim() || null,
        categoryId: els.category.value || null,
        status: els.status.value,
        isCoreFeature: !!els.core.checked,
        sortOrder: els.sort.value === '' ? null : Number(els.sort.value),
        optionalFeatureFlagKey: els.flag.value.trim() || null,
        rowVersion: els.rowVersion.value || null
    });

    const saveMappings = async (id, isArchived) => {
        if (isArchived || !els.planMappings) return;
        const selects = Array.from(els.planMappings.querySelectorAll('.sf-plan-status')).filter((s) => !s.disabled);
        await Promise.all(selects.map((select) => api(`/Platform/SubscriptionFeatures/api/plans/${select.getAttribute('data-plan-id')}/features`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ mappings: [{ featureDefinitionId: id, availabilityStatus: select.value, effectiveFromUtc: null, effectiveToUtc: null, rowVersion: select.getAttribute('data-row-version') || null }] })
        })));
    };

    const showError = (messages) => {
        if (!els.alert) return;
        els.alert.textContent = Array.isArray(messages) ? messages.join(' ') : (messages || l10n().GatewayError);
        setVisible(els.alert, true);
    };

    const save = async () => {
        if (!validate()) return;
        setVisible(els.alert, false);
        const payload = buildPayload();
        els.save.disabled = true;
        try {
            let id = featureId;
            if (isEdit) {
                await api(`/Platform/SubscriptionFeatures/api/${featureId}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
            } else {
                id = await api('/Platform/SubscriptionFeatures/api', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
            }
            await saveMappings(id, payload.status === 'Archived');
            try { sessionStorage.setItem('sf-toast', isEdit ? 'updated' : 'created'); } catch (e) { }
            window.location.href = '/Platform/SubscriptionFeatures';
        } catch (error) {
            const messages = error.errors && error.errors.length ? error.errors : [error.message];
            messages.forEach((m) => {
                if (/FeatureCode/i.test(m)) setInvalid(els.code, m);
                else if (/FeatureSlug/i.test(m)) setInvalid(els.slug, m);
                else if (/CategoryId|category/i.test(m)) setInvalid(els.category, m);
            });
            showError(messages);
            els.save.disabled = false;
        }
    };

    const init = async () => {
        try {
            const categories = await api('/Platform/SubscriptionFeatures/api/categories').catch(() => []);
            state.categories = Array.isArray(categories) ? categories : [];
            fillCategoryOptions();

            // Mirror ALL subscription plans (active + inactive) so the list reflects
            // whatever exists in Subscription Plans (add → appears, delete → gone).
            // Inactive plans are shown but locked (backend blocks mapping them).
            // The error is surfaced — not swallowed — so an empty list never hides a real failure.
            const plansRes = await api('/Platform/SubscriptionFeatures/api/plans?page=1&pageSize=500&sort=sortOrder');
            state.plans = (plansRes && (plansRes.items || plansRes.Items)) || (Array.isArray(plansRes) ? plansRes : []);

            if (isEdit) {
                state.feature = await api(`/Platform/SubscriptionFeatures/api/${featureId}`);
                const mappings = await api(`/Platform/SubscriptionFeatures/api/${featureId}/plan-mappings`).catch(() => []);
                state.mappings = Array.isArray(mappings) ? mappings : [];
            }

            initSelect2();
            if (isEdit) fillFeature(state.feature);
            renderPlanMappings();
        } catch (error) {
            showError(error.errors && error.errors.length ? error.errors : [error.message]);
            initSelect2();
        }
    };

    els.save && els.save.addEventListener('click', save);
    els.status && els.status.addEventListener('change', renderPlanMappings);

    // Run init even if the DOM is already parsed (script may load after DOMContentLoaded).
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
