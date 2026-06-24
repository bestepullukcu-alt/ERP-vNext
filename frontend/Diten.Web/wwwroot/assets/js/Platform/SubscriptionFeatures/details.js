'use strict';

/** Subscription Features — Feature Details (read-only, AJAX-loaded). */
(function () {
    const l10n = () => window.L10n || {};
    const root = document.querySelector('.subscription-feature-details');
    if (!root) return;
    const featureId = (root.getAttribute('data-feature-id') || '').trim();
    if (!featureId) return;

    const safe = (v) => (v === null || v === undefined ? '' : String(v));
    const dash = (v) => (safe(v).trim() === '' ? '-' : safe(v));
    const set = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = dash(value); };
    const availabilityLabel = (s) => l10n()[safe(s)] || safe(s);

    const parseEnvelope = async (response) => {
        const text = await response.text();
        if (!text) return {};
        try { return JSON.parse(text); } catch { return { errors: [text] }; }
    };
    const api = async (url) => {
        const response = await fetch(url, { credentials: 'same-origin' });
        const json = await parseEnvelope(response);
        if (!response.ok || json.isSuccessful === false || json.IsSuccessful === false) {
            const errors = json.errors || json.Errors || [];
            throw new Error(errors.length ? errors.join(' ') : (l10n().GatewayError || 'error'));
        }
        return json.data || json.Data || json;
    };

    const statusBadge = (status) => {
        const map = { Active: 'bg-label-success', Inactive: 'bg-label-secondary', Draft: 'bg-label-info', Deprecated: 'bg-label-warning', Archived: 'bg-label-dark' };
        const label = l10n()[safe(status)] || safe(status);
        return `<span class="badge ${map[safe(status)] || 'bg-label-secondary'}">${label}</span>`;
    };

    const render = (feature, categories, mappings) => {
        document.getElementById('sfd-title').textContent = safe(feature.displayName || feature.DisplayName) || '-';
        set('sfd-code', feature.featureCode || feature.FeatureCode);
        set('sfd-slug', feature.featureSlug || feature.FeatureSlug);
        set('sfd-name', feature.displayName || feature.DisplayName);
        set('sfd-description', feature.description || feature.Description);
        set('sfd-sort', feature.sortOrder ?? feature.SortOrder);
        set('sfd-flag', feature.optionalFeatureFlagKey || feature.OptionalFeatureFlagKey);

        const statusEl = document.getElementById('sfd-status');
        if (statusEl) statusEl.innerHTML = statusBadge(feature.status || feature.Status);

        const coreEl = document.getElementById('sfd-core');
        if (coreEl) coreEl.innerHTML = (feature.isCoreFeature || feature.IsCoreFeature)
            ? `<span class="badge bg-label-info">${l10n().IsCoreFeature || 'Core'}</span>`
            : '<span class="text-muted">-</span>';

        const catId = safe(feature.categoryId || feature.CategoryId);
        const category = categories.find((c) => safe(c.id || c.Id) === catId);
        set('sfd-category', category ? (category.displayName || category.DisplayName) : '-');

        const plansEl = document.getElementById('sfd-plans');
        if (plansEl) {
            const included = mappings.filter((m) => ['Included', 'AddOn', 'Preview'].includes(safe(m.availabilityStatus || m.AvailabilityStatus)));
            plansEl.innerHTML = included.length
                ? included.map((m) => `<span class="badge bg-label-primary">${safe(m.subscriptionPlanName || m.SubscriptionPlanName)} (${availabilityLabel(m.availabilityStatus || m.AvailabilityStatus)})</span>`).join('')
                : `<span class="text-muted">${l10n().NoPlans || '-'}</span>`;
        }
    };

    const showError = (message) => {
        const el = document.getElementById('sfd-alert');
        if (!el) return;
        el.textContent = message || l10n().GatewayError;
        el.classList.remove('d-none');
    };

    const load = async () => {
        try {
            const [feature, categories, mappings] = await Promise.all([
                api(`/Platform/SubscriptionFeatures/api/${featureId}`),
                api('/Platform/SubscriptionFeatures/api/categories').catch(() => []),
                api(`/Platform/SubscriptionFeatures/api/${featureId}/plan-mappings`).catch(() => [])
            ]);
            render(feature, Array.isArray(categories) ? categories : [], Array.isArray(mappings) ? mappings : []);
        } catch (error) {
            showError(error.message);
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', load);
    } else {
        load();
    }
})();
