/**
 * Tenant Core Create Page Script
 * Diten ERP vNext - Platform/Tenants
 */
'use strict';

const TenantCreate = (function () {
    const form = document.getElementById('formTenantCreate');
    const apiBase = '/Platform/Tenants/api';
    const isEditMode = form?.dataset.mode === 'edit';
    const tenantId = form?.dataset.tenantId || '';
    let L = window.L10n || {};
    let plans = [];
    let selectedPlan = null;
    let tenantLoaded = !isEditMode;

    const syncL10n = () => { L = window.L10n || {}; };
    const read = (name) => {
        const element = form?.elements[name];
        return typeof element?.value === 'string' ? element.value.trim() : '';
    };
    const readBool = (name) => form?.elements[name]?.checked === true;
    const unwrap = (payload) => payload?.data ?? payload?.Data ?? payload ?? null;

    const setIfValue = (payload, key, value) => {
        if (value !== null && value !== undefined && value !== '') {
            payload[key] = value;
        }
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

    const normalizeHostPart = (value) => value
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9\s-]/g, '')
        .replace(/\s+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-|-$/g, '');

    const clearErrors = () => {
        document.getElementById('formErrorSummary')?.classList.add('d-none');
        document.querySelectorAll('[data-valmsg-for]').forEach((element) => {
            element.textContent = '';
        });
        form?.querySelectorAll('.is-invalid').forEach((element) => element.classList.remove('is-invalid'));
    };

    const findInputForProperty = (propertyName) => {
        const normalized = propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
        return form?.querySelector(`[name="${normalized}"], [name="${propertyName}"], [name="initialAdmin.${normalized.split('.').pop()}"]`);
    };

    const friendlyMessages = (messages) => messages
        .filter(Boolean)
        .map((message) => String(message).replace(/^[A-Za-z0-9_.]+:\s*/, ''));

    const showErrors = (problem) => {
        const messages = [];
        const errors = problem?.errors || problem?.Errors || {};

        Object.keys(errors).forEach((key) => {
            const fieldMessages = friendlyMessages(Array.isArray(errors[key]) ? errors[key] : [String(errors[key])]);
            messages.push(...fieldMessages);
            const messageElement = document.querySelector(`[data-valmsg-for="${key}"]`);
            if (messageElement) messageElement.textContent = fieldMessages.join(' ');
            findInputForProperty(key)?.classList.add('is-invalid');
        });

        if (problem?.detail) messages.unshift(problem.detail);
        if (problem?.message && messages.length === 0) messages.push(problem.message);
        if (problem?.title && messages.length === 0) messages.push(problem.title);

        const summary = document.getElementById('formErrorSummary');
        if (summary) {
            summary.textContent = [...new Set(friendlyMessages(messages))].join(' ');
            summary.classList.toggle('d-none', messages.length === 0);
        }

        window.showToast?.(messages[0] || L.ValidationFailed || 'Validation failed.', 'error');
    };

    const validateClient = () => {
        const errors = {};
        if (!read('name')) errors.Name = [L.TenantNameRequired || 'Tenant name is required.'];
        if (!read('displayName')) errors.DisplayName = [L.DisplayNameRequired || 'Display name is required.'];
        if (!read('initialAdmin.email')) errors['InitialAdmin.Email'] = [L.AdminEmailRequired || 'Admin email is required.'];
        if (read('initialAdmin.email') && !form.elements['initialAdmin.email'].checkValidity()) errors['InitialAdmin.Email'] = [L.AdminEmailInvalid || 'Admin email must be valid.'];
        if (!read('subdomain')) errors.Subdomain = [L.SubdomainRequired || 'Subdomain is required.'];
        if (!isEditMode && !selectedPlan?.id) errors.PlanId = [L.PlanRequired || 'Select an active subscription plan.'];

        if (Object.keys(errors).length > 0) {
            showErrors({ errors });
            return false;
        }

        return true;
    };

    const buildPayload = () => {
        const payload = {
            name: read('name'),
            displayName: read('displayName'),
            domain: 'ditenteknoloji.com',
            subdomain: read('subdomain'),
            tenantType: Number(read('tenantType') || 0)
        };

        if (!isEditMode) {
            payload.planId = selectedPlan.id;
        }

        setIfValue(payload, 'slug', read('slug'));
        setIfValue(payload, 'country', read('country').toUpperCase());
        setIfValue(payload, 'defaultTimezone', read('defaultTimezone'));
        setIfValue(payload, 'defaultLanguage', read('defaultLanguage'));
        setIfValue(payload, 'defaultCurrency', read('defaultCurrency').toUpperCase());
        if (!isEditMode) {
            payload.initialAdmin = {
                firstName: '',
                lastName: '',
                email: read('initialAdmin.email').toLowerCase(),
                phone: '',
                mfaRequired: false,
                emailVerificationRequired: true,
                sendInvitationEmail: true
            };
        }

        return payload;
    };

    const setCreateEnabled = () => {
        const button = document.getElementById('btnCreateTenant');
        if (button) button.disabled = isEditMode ? !tenantLoaded : !selectedPlan?.id;
    };

    const formatAmount = (amount, currency) => {
        if (amount === null || amount === undefined || amount === '') return '-';
        const numericAmount = Number(amount);
        const normalizedCurrency = String(currency || '').trim().toUpperCase();
        if (!Number.isNaN(numericAmount) && /^[A-Z]{3}$/.test(normalizedCurrency)) {
            return new Intl.NumberFormat(undefined, {
                style: 'currency',
                currency: normalizedCurrency,
                minimumFractionDigits: 0,
                maximumFractionDigits: 2
            }).format(numericAmount);
        }

        return normalizedCurrency ? `${amount} ${normalizedCurrency}` : `${amount}`;
    };

    const formatPrice = (plan) => {
        const currency = plan.currency || '';
        const monthly = plan.priceMonthly ?? plan.PriceMonthly;
        const yearly = plan.priceYearly ?? plan.PriceYearly;
        return {
            monthly: monthly !== null && monthly !== undefined ? formatAmount(monthly, currency) : '-',
            yearly: yearly !== null && yearly !== undefined ? formatAmount(yearly, currency) : '-'
        };
    };

    const featureItems = (plan) => {
        const features = plan.includedFeatures || plan.IncludedFeatures || [];
        const modules = (plan.includedModuleKeys || plan.IncludedModuleKeys || []).map((item) => `${L.Modules || 'Modules'}: ${item}`);
        const quotas = plan.defaultQuotas || plan.DefaultQuotas || {};
        const quotaItems = Object.keys(quotas).map((key) => `${key}: ${quotas[key]}`);
        return features.concat(modules, quotaItems).slice(0, 7);
    };

    const renderPlans = () => {
        const grid = document.getElementById('planCardGrid');
        if (!grid) return;
        grid.innerHTML = plans.map((plan) => {
            const id = plan.id || plan.Id;
            const isSelected = selectedPlan && selectedPlan.id === id;
            const isTrial = plan.isTrialPlan ?? plan.IsTrialPlan;
            const isDefault = plan.isDefault ?? plan.IsDefault;
            const trialDays = plan.trialDurationDays ?? plan.TrialDurationDays;
            const prices = formatPrice(plan);
            const items = featureItems(plan);
            const monthlyLabel = String(L.Month || 'month').toLowerCase();
            const yearlyLabel = String(L.Year || 'year').toLowerCase();
            return `<div class="col-12 col-md-6 col-xl-3">
                <div class="card h-100 border shadow-sm ${isSelected ? 'border-primary' : ''}" data-plan-id="${escapeHtml(id)}" role="${isEditMode ? 'group' : 'button'}" tabindex="${isEditMode ? '-1' : '0'}">
                    <div class="card-body d-flex flex-column p-4">
                        <div class="d-flex align-items-start justify-content-between gap-3 mb-4">
                            <div class="min-w-0">
                                <div class="d-flex flex-wrap align-items-center gap-2 mb-2">
                                    <h5 class="mb-0 text-truncate">${escapeHtml(plan.name || plan.Name)}</h5>
                                    ${isTrial ? `<span class="badge bg-label-info">${escapeHtml(L.FreeTrial || 'Free')}</span>` : ''}
                                    ${isDefault ? `<span class="badge bg-label-primary">${escapeHtml(L.Default || 'Default')}</span>` : ''}
                                    ${isSelected ? `<span class="badge bg-label-success">${escapeHtml(L.Selected || 'Selected')}</span>` : ''}
                                </div>
                                <small class="text-uppercase text-muted">${escapeHtml(plan.code || plan.Code)}</small>
                            </div>
                            <div class="avatar avatar-sm flex-shrink-0">
                                <span class="avatar-initial rounded bg-label-${isSelected ? 'primary' : 'secondary'}">
                                    <i class="bx ${isSelected ? 'bx-check' : 'bx-layer'}"></i>
                                </span>
                            </div>
                        </div>
                        ${plan.description || plan.Description ? `<p class="text-muted mb-5">${escapeHtml(plan.description || plan.Description)}</p>` : `<p class="text-muted mb-5">&nbsp;</p>`}
                        <div class="mb-4">
                            <div class="d-flex align-items-baseline flex-wrap gap-1">
                                <span class="display-6 text-heading mb-0">${escapeHtml(prices.monthly)}</span>
                                <span class="text-muted">/${escapeHtml(monthlyLabel)}</span>
                            </div>
                            <div class="text-muted small">${escapeHtml(prices.yearly)}/${escapeHtml(yearlyLabel)}</div>
                        </div>
                        ${isTrial ? `<div class="mb-4"><small class="text-muted">${escapeHtml(L.TrialDuration || 'Trial Duration')}:</small> <span class="fw-semibold">${escapeHtml(trialDays || '-')} ${escapeHtml(L.Days || 'days')}</span></div>` : ''}
                        <div class="border-top pt-4 mt-auto">
                            <small class="text-muted d-block mb-3">${escapeHtml(L.Features || 'Features')}</small>
                            ${items.length
                                ? `<ul class="list-unstyled mb-0">${items.map((item) => `<li class="d-flex align-items-start gap-2 mb-2"><i class="bx bx-check text-success mt-1"></i><span>${escapeHtml(item)}</span></li>`).join('')}</ul>`
                                : `<div class="text-muted small">${escapeHtml(L.NoFeatureSummary || '-')}</div>`}
                        </div>
                    </div>
                </div>
            </div>`;
        }).join('');
    };

    const updateAccessPreview = () => {
        const status = document.getElementById('subscriptionStatusPreview');
        const text = document.getElementById('accessPreviewText');
        if (!status || !text) return;

        if (!selectedPlan) {
            status.textContent = '-';
            text.textContent = '-';
            return;
        }

        const isTrial = selectedPlan.isTrialPlan ?? selectedPlan.IsTrialPlan;
        const trialDays = selectedPlan.trialDurationDays ?? selectedPlan.TrialDurationDays;
        const planName = selectedPlan.name || selectedPlan.Name || '';
        const trialEndDate = new Date(Date.now() + Number(trialDays || 0) * 86400000).toLocaleDateString();
        status.textContent = isTrial ? 'Trialing access' : 'Active subscription';
        text.textContent = isTrial
            ? `${planName} starts as Trialing for ${trialDays} ${L.Days || 'days'} and ends on ${trialEndDate}.`
            : `${planName} starts as Active with ${L.NoTrialPeriod || 'no trial period'}.`;
    };

    const selectPlan = (planId) => {
        selectedPlan = plans.find((plan) => (plan.id || plan.Id) === planId) || null;
        const hidden = document.getElementById('planId');
        if (hidden) hidden.value = selectedPlan ? (selectedPlan.id || selectedPlan.Id) : '';
        renderPlans();
        updateAccessPreview();
        setCreateEnabled();
    };

    const loadPlans = async () => {
        const loadingBadge = document.getElementById('plansLoadingBadge');
        const errorBox = document.getElementById('plansError');
        const emptyBox = document.getElementById('plansEmpty');
        if (errorBox) errorBox.classList.add('d-none');
        if (emptyBox) emptyBox.classList.add('d-none');
        if (loadingBadge) loadingBadge.classList.remove('d-none');

        try {
            const response = await fetch(`${apiBase}/subscription-plans/active`, { credentials: 'same-origin' });
            const json = await response.json().catch(() => null);
            if (!response.ok) throw new Error(json?.message || response.statusText);

            plans = unwrap(json) || [];
            if (!Array.isArray(plans)) plans = [];
            if (emptyBox) emptyBox.classList.toggle('d-none', plans.length > 0);
            renderPlans();

            const defaultPlan = plans.find((plan) => String(plan.code || plan.Code).toUpperCase() === 'FREE' && (plan.isDefault ?? plan.IsDefault))
                || plans.find((plan) => String(plan.code || plan.Code).toUpperCase() === 'FREE')
                || plans.find((plan) => plan.isDefault ?? plan.IsDefault)
                || plans[0];
            if (defaultPlan) selectPlan(defaultPlan.id || defaultPlan.Id);
        } catch (error) {
            if (errorBox) {
                errorBox.textContent = L.PlanLoadError || 'Active subscription plans could not be loaded.';
                errorBox.classList.remove('d-none');
            }
            plans = [];
            selectedPlan = null;
            setCreateEnabled();
        } finally {
            if (loadingBadge) loadingBadge.classList.add('d-none');
        }
    };

    const bindPlanSelection = () => {
        document.getElementById('planCardGrid')?.addEventListener('click', (event) => {
            if (isEditMode) return;
            const card = event.target.closest('[data-plan-id]');
            if (!card) return;
            selectPlan(card.getAttribute('data-plan-id'));
        });

        document.getElementById('planCardGrid')?.addEventListener('keydown', (event) => {
            if (isEditMode) return;
            if (event.key !== 'Enter' && event.key !== ' ') return;
            const card = event.target.closest('[data-plan-id]');
            if (!card) return;
            event.preventDefault();
            selectPlan(card.getAttribute('data-plan-id'));
        });
    };

    const bindSlugAndDomainSuggestion = () => {
        const name = document.getElementById('tenantName');
        const displayName = document.getElementById('tenantDisplayName');
        const slug = document.getElementById('tenantSlug');
        const subdomain = document.getElementById('tenantSubdomain');
        const preview = document.getElementById('accessUrlPreview');

        const syncGenerated = () => {
            const source = displayName?.value || name?.value || '';
            const generated = normalizeHostPart(source);
            if (slug && slug.dataset.touched !== '1') slug.value = generated;
            if (subdomain && subdomain.dataset.touched !== '1') subdomain.value = generated;
            if (preview) preview.value = `${subdomain?.value || generated || ''}.ditenteknoloji.com`;
        };

        name?.addEventListener('input', syncGenerated);
        displayName?.addEventListener('input', syncGenerated);
        slug?.addEventListener('input', () => {
            slug.dataset.touched = '1';
            slug.value = normalizeHostPart(slug.value);
        });
        subdomain?.addEventListener('input', () => {
            subdomain.dataset.touched = '1';
            subdomain.value = normalizeHostPart(subdomain.value);
            if (preview) preview.value = `${subdomain.value}.ditenteknoloji.com`;
        });
    };

    const bindSubmit = () => {
        if (!form) return;
        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            clearErrors();
            if (!validateClient()) return;

            const submitButton = document.getElementById('btnCreateTenant');
            const originalHtml = submitButton?.innerHTML;
            if (submitButton) {
                submitButton.disabled = true;
                submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>';
            }

            try {
                const response = await fetch(isEditMode ? `${apiBase}/${encodeURIComponent(tenantId)}` : apiBase, {
                    method: isEditMode ? 'PUT' : 'POST',
                    credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(buildPayload())
                });

                if (response.status === 401) {
                    window.DtDefaults?.handleUnauthorized?.();
                    return;
                }

                if (response.status === 403) {
                    showErrors({ detail: L.PermissionDenied || 'Permission denied.' });
                    return;
                }

                const json = await response.json().catch(() => null);
                if (!response.ok) {
                    showErrors(json || { detail: response.statusText });
                    return;
                }

                window.showToast?.(isEditMode ? (L.RecordSaved || 'Record saved.') : (L.TenantCreated || 'Tenant created and provisioning started.'), 'success');
                const id = unwrap(json);
                const redirectId = isEditMode ? tenantId : id;
                window.location.href = isEditMode
                    ? '/Platform/Tenants'
                    : (redirectId ? `/Platform/Tenants/Details/${encodeURIComponent(redirectId)}` : '/Platform/Tenants');
            } catch (error) {
                window.showToast?.(error.message || L.ErrorOccurred || 'Error occurred.', 'error');
            } finally {
                if (submitButton) {
                    submitButton.disabled = !selectedPlan?.id;
                    submitButton.innerHTML = originalHtml;
                }
            }
        });
    };

    const loadLookups = async () => {
        try {
            const [countriesPayload, currenciesPayload, timezonesPayload] = await Promise.all([
                fetch(`${apiBase}/lookups/countries`).then(r => r.json()),
                fetch(`${apiBase}/lookups/currencies`).then(r => r.json()),
                fetch(`${apiBase}/lookups/timezones`).then(r => r.json())
            ]);
            const countries = unwrap(countriesPayload) || [];
            const currencies = unwrap(currenciesPayload) || [];
            const timezones = unwrap(timezonesPayload) || [];

            const countrySelect = document.getElementById('tenantCountry');
            const currencySelect = document.getElementById('defaultCurrency');
            const timezoneSelect = document.getElementById('defaultTimezone');

            countries.forEach(c => countrySelect?.add(new Option(`${c.name} (${c.code})`, c.code)));
            currencies.forEach(c => currencySelect?.add(new Option(`${c.code} - ${c.name}`, c.value || c.code)));
            [...timezones]
                .sort((a, b) => String(a.name || '').localeCompare(String(b.name || ''), undefined, { sensitivity: 'base' }))
                .forEach(t => timezoneSelect?.add(new Option(t.name, t.value || t.code || t.id)));

            initTimezoneSelect2();
        } catch (err) {
            console.error('[TenantCreate] Lookups could not be loaded:', err);
        }
    };

    const setValue = (name, value) => {
        const element = form?.elements[name];
        if (!element || value === null || value === undefined) return;
        element.value = value;
        element.dispatchEvent(new Event('change'));
    };

    const tenantTypeValue = (value) => {
        const normalized = String(value || '').toLowerCase();
        if (normalized === 'demo') return '1';
        if (normalized === 'internal') return '4';
        return '0';
    };

    const subdomainFromDomain = (domain) => {
        const value = String(domain || '').toLowerCase();
        const suffix = '.ditenteknoloji.com';
        if (value.endsWith(suffix)) return value.slice(0, -suffix.length);
        return '';
    };

    const loadTenantForEdit = async () => {
        if (!isEditMode || !tenantId) return;

        try {
            const response = await fetch(`${apiBase}/${encodeURIComponent(tenantId)}`, { credentials: 'same-origin' });
            const json = await response.json().catch(() => null);
            if (!response.ok) throw new Error(json?.message || response.statusText);

            const tenant = unwrap(json);
            setValue('name', tenant.name || tenant.Name);
            setValue('displayName', tenant.displayName || tenant.DisplayName);
            setValue('slug', tenant.slug || tenant.Slug);
            setValue('tenantType', tenantTypeValue(tenant.tenantType || tenant.TenantType));
            setValue('subdomain', subdomainFromDomain(tenant.domain || tenant.Domain));
            setValue('initialAdmin.email', tenant.contactEmail || tenant.ContactEmail);
            setValue('country', tenant.country || tenant.Country);
            setValue('defaultTimezone', tenant.defaultTimezone || tenant.DefaultTimezone);
            setValue('defaultLanguage', tenant.defaultLanguage || tenant.DefaultLanguage);
            setValue('defaultCurrency', tenant.defaultCurrency || tenant.DefaultCurrency);

            const planId = tenant.planId || tenant.PlanId;
            if (planId && plans.some((plan) => (plan.id || plan.Id) === planId)) {
                selectPlan(planId);
            } else {
                renderPlans();
                updateAccessPreview();
            }

            document.getElementById('tenantSlug')?.setAttribute('data-touched', '1');
            document.getElementById('tenantSubdomain')?.setAttribute('data-touched', '1');
            const preview = document.getElementById('accessUrlPreview');
            const subdomain = read('subdomain');
            if (preview) preview.value = `${subdomain || ''}.ditenteknoloji.com`;
            tenantLoaded = true;
            setCreateEnabled();
        } catch (error) {
            tenantLoaded = false;
            showErrors({ detail: error.message || L.ErrorOccurred || 'Error occurred.' });
            setCreateEnabled();
        }
    };

    const initTimezoneSelect2 = () => {
        if (!window.jQuery || !window.jQuery.fn?.select2) return;

        const $select = window.jQuery('#defaultTimezone');
        if (!$select.length) return;
        if ($select.hasClass('select2-hidden-accessible')) {
            $select.select2('destroy');
        }

        if (!$select.parent().hasClass('position-relative')) {
            $select.wrap('<div class="position-relative"></div>');
        }

        $select.select2({
            dropdownParent: $select.parent(),
            placeholder: $select.find('option:first').text(),
            allowClear: true,
            width: '100%'
        });
    };

    return {
        init: async () => {
            syncL10n();
            bindPlanSelection();
            bindSlugAndDomainSuggestion();
            bindSubmit();
            await Promise.all([loadPlans(), loadLookups()]);
            await loadTenantForEdit();
            updateAccessPreview();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantCreate.init());
