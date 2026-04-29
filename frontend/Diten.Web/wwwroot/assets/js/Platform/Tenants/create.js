/**
 * Tenant Core Create Page Script
 * Diten ERP vNext - Platform/Tenants
 */
'use strict';

const TenantCreate = (function () {
    const form = document.getElementById('formTenantCreate');
    const apiUrl = window.API?.platform || window.ApiBaseUrl || 'http://localhost:5000';
    let L = window.L10n || {};

    const syncL10n = () => { L = window.L10n || {}; };
    const getAuthHeaders = () => ({ 'Content-Type': 'application/json' });

    const read = (name) => {
        const element = form?.elements[name];
        return typeof element?.value === 'string' ? element.value.trim() : '';
    };

    const readBool = (name) => form?.elements[name]?.checked === true;

    const setIfValue = (payload, key, value) => {
        if (value !== null && value !== undefined && value !== '') {
            payload[key] = value;
        }
    };

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

    const showErrors = (problem) => {
        const messages = [];
        const errors = problem?.errors || problem?.Errors || {};

        Object.keys(errors).forEach((key) => {
            const fieldMessages = Array.isArray(errors[key]) ? errors[key] : [String(errors[key])];
            messages.push(...fieldMessages);
            const messageElement = document.querySelector(`[data-valmsg-for="${key}"]`);
            if (messageElement) messageElement.textContent = fieldMessages.join(' ');
            findInputForProperty(key)?.classList.add('is-invalid');
        });

        if (problem?.detail) messages.unshift(problem.detail);
        if (problem?.title && messages.length === 0) messages.push(problem.title);

        const summary = document.getElementById('formErrorSummary');
        if (summary) {
            summary.textContent = [...new Set(messages)].join(' ');
            summary.classList.toggle('d-none', messages.length === 0);
        }

        window.showToast?.(messages[0] || L.ValidationFailed || 'ValidationFailed', 'error');
    };

    const buildPayload = () => {
        const payload = {
            name: read('name'),
            domain: read('domain')
        };

        setIfValue(payload, 'slug', read('slug'));
        setIfValue(payload, 'displayName', read('displayName'));
        const tenantType = read('tenantType');
        if (tenantType) payload.tenantType = Number(tenantType);
        setIfValue(payload, 'legalName', read('legalName'));
        setIfValue(payload, 'taxNumber', read('taxNumber'));
        setIfValue(payload, 'country', read('country').toUpperCase());
        setIfValue(payload, 'industry', read('industry'));
        setIfValue(payload, 'contactPerson', read('contactPerson'));
        setIfValue(payload, 'contactEmail', read('contactEmail').toLowerCase());
        setIfValue(payload, 'contactPhone', read('contactPhone'));
        setIfValue(payload, 'defaultTimezone', read('defaultTimezone'));
        setIfValue(payload, 'defaultLanguage', read('defaultLanguage'));
        setIfValue(payload, 'defaultCurrency', read('defaultCurrency').toUpperCase());

        const initialAdmin = {
            firstName: read('initialAdmin.firstName'),
            lastName: read('initialAdmin.lastName'),
            email: read('initialAdmin.email').toLowerCase(),
            phone: read('initialAdmin.phone'),
            mfaRequired: readBool('initialAdmin.mfaRequired'),
            emailVerificationRequired: readBool('initialAdmin.emailVerificationRequired'),
            sendInvitationEmail: readBool('initialAdmin.sendInvitationEmail')
        };

        if (initialAdmin.firstName || initialAdmin.lastName || initialAdmin.email || initialAdmin.phone) {
            payload.initialAdmin = initialAdmin;
        }

        return payload;
    };

    const bindSlugSuggestion = () => {
        const name = document.getElementById('tenantName');
        const slug = document.getElementById('tenantSlug');
        if (!name || !slug) return;

        name.addEventListener('input', () => {
            if (slug.dataset.touched === '1') return;
            slug.value = name.value
                .trim()
                .toLowerCase()
                .replace(/[^a-z0-9\s-]/g, '')
                .replace(/\s+/g, '-')
                .replace(/-+/g, '-');
        });

        slug.addEventListener('input', () => { slug.dataset.touched = '1'; });
    };

    const bindSubmit = () => {
        if (!form) return;
        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            clearErrors();

            const submitButton = document.querySelector('[form="formTenantCreate"]');
            const originalHtml = submitButton?.innerHTML;
            if (submitButton) {
                submitButton.disabled = true;
                submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>';
            }

            try {
                const response = await fetch(`${apiUrl}/api/admin/tenants`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: getAuthHeaders(),
                    body: JSON.stringify(buildPayload())
                });

                if (response.status === 401 || response.status === 403) {
                    window.DtDefaults?.handleUnauthorized?.();
                    return;
                }

                const json = await response.json().catch(() => null);
                if (!response.ok) {
                    showErrors(json || { detail: response.statusText });
                    return;
                }

                window.showToast?.(L.TenantCreated || 'Tenant created and provisioning started.', 'success');
                window.location.href = '/Platform/Tenants';
            } catch (error) {
                window.showToast?.(error.message || L.ErrorOccurred || 'ErrorOccurred', 'error');
            } finally {
                if (submitButton) {
                    submitButton.disabled = false;
                    submitButton.innerHTML = originalHtml;
                }
            }
        });
    };

    const loadLookups = async () => {
        try {
            const [countries, currencies, timezones] = await Promise.all([
                fetch(`${apiUrl}/api/lookups/countries`).then(r => r.json()),
                fetch(`${apiUrl}/api/lookups/currencies`).then(r => r.json()),
                fetch(`${apiUrl}/api/lookups/timezones`).then(r => r.json())
            ]);

            const countrySelect = document.getElementById('tenantCountry');
            const currencySelect = document.getElementById('defaultCurrency');
            const timezoneSelect = document.getElementById('defaultTimezone');

            if (countrySelect) {
                countries.forEach(c => {
                    const opt = new Option(`${c.name} (${c.code})`, c.code);
                    countrySelect.add(opt);
                });
            }

            if (currencySelect) {
                currencies.forEach(c => {
                    const opt = new Option(`${c.code} — ${c.name}`, c.code);
                    currencySelect.add(opt);
                });
            }

            if (timezoneSelect) {
                timezones.forEach(t => {
                    const opt = new Option(t.name, t.id);
                    timezoneSelect.add(opt);
                });
            }
        } catch (err) {
            console.error('[TenantCreate] Lookups yüklenemedi:', err);
        }
    };

    return {
        init: () => {
            syncL10n();
            bindSlugSuggestion();
            bindSubmit();
            loadLookups();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantCreate.init());
