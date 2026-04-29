/**
 * Tenant Login & Security Settings Page Script
 * Diten ERP vNext - Platform/Tenants
 */
'use strict';

const TenantSecurity = (function () {
    const root = document.getElementById('tenantSecurityRoot');
    const form = document.getElementById('formTenantSecurity');
    const tenantSelect = document.getElementById('securityTenantSelect');
    const saveButton = document.getElementById('btnSaveTenantSecurity');
    const apiUrl = window.API?.platform || window.ApiBaseUrl;
    let tenants = [];
    let selectedTenantId = '';
    let L = window.L10n || {};
    let tagifyIps = null;
    let countryList = [];

    const syncL10n = () => { L = window.L10n || {}; };
    const unwrap = (payload) => payload?.data ?? payload ?? null;
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress';
    const getAuthHeaders = () => ({});

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

    const readBool = (name) => form?.elements[name]?.checked === true;
    const readNumber = (name) => {
        const value = form?.elements[name]?.value;
        return value === '' || value === null || value === undefined ? null : Number(value);
    };

    const splitList = (value) => String(value || '')
        .split(/[\n,;]/)
        .map((item) => item.trim())
        .filter(Boolean);

    const joinList = (values) => Array.isArray(values) ? values.join('\n') : '';

    const clearErrors = () => {
        document.getElementById('securityErrorSummary')?.classList.add('d-none');
        document.querySelectorAll('[data-valmsg-for]').forEach((element) => {
            element.textContent = '';
        });
        form?.querySelectorAll('.is-invalid').forEach((element) => element.classList.remove('is-invalid'));
    };

    const findInputForProperty = (propertyName) => {
        const key = String(propertyName || '').split('.').pop();
        if (!key) return null;
        const normalized = key.charAt(0).toLowerCase() + key.slice(1);
        return form?.querySelector(`[name="${normalized}"], [name="${key}"]`);
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

        const summary = document.getElementById('securityErrorSummary');
        if (summary) {
            summary.textContent = [...new Set(messages)].join(' ');
            summary.classList.toggle('d-none', messages.length === 0);
        }

        window.showToast?.(messages[0] || L.ValidationFailed, 'error');
    };

    const fetchJson = async (url, options) => {
        const response = await fetch(url, Object.assign({
            credentials: 'include',
            headers: getAuthHeaders()
        }, options || {}));

        if (response.status === 401) {
            window.DtDefaults?.handleUnauthorized?.();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            throw authError;
        }

        if (response.status === 403) {
            const forbiddenError = new Error('Forbidden');
            forbiddenError.status = 403;
            throw forbiddenError;
        }

        const json = await response.json().catch(() => null);
        if (!response.ok) {
            const error = new Error(response.statusText);
            error.problem = json;
            throw error;
        }

        return unwrap(json);
    };

    const setLoading = (loading) => {
        if (!saveButton) return;
        saveButton.disabled = loading || !selectedTenantId;
        if (loading) {
            saveButton.dataset.originalHtml = saveButton.innerHTML;
            saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>';
        } else if (saveButton.dataset.originalHtml) {
            saveButton.innerHTML = saveButton.dataset.originalHtml;
            delete saveButton.dataset.originalHtml;
        }
    };

    const populateTenantSelect = () => {
        if (!tenantSelect) return;
        const current = selectedTenantId || new URLSearchParams(window.location.search).get('tenantId') || '';
        tenantSelect.innerHTML = `<option value="">${escapeHtml(L.SelectTenant)}</option>` + tenants.map((tenant) =>
            `<option value="${escapeHtml(tenant.id)}">${escapeHtml(tenant.displayName || tenant.name)} (${escapeHtml(tenant.code)})</option>`
        ).join('');
        if (current) tenantSelect.value = current;
    };

    const updateSelectedTenantSummary = () => {
        const tenant = tenants.find((item) => item.id === selectedTenantId);
        document.getElementById('selectedTenantName').innerText = tenant ? (tenant.displayName || tenant.name || '-') : '-';
        document.getElementById('selectedTenantMeta').innerText = tenant ? `${tenant.code || '-'} / ${tenant.slug || '-'}` : '-';
    };

    const loadTenants = async () => {
        const data = await fetchJson(`${apiUrl}/api/admin/tenants?page=1&pageSize=200&sort=-createdAt`);
        if (!data) return;
        tenants = Array.isArray(data?.items) ? data.items : [];
        populateTenantSelect();

        const queryTenantId = new URLSearchParams(window.location.search).get('tenantId');
        if (queryTenantId && tenants.some((tenant) => tenant.id === queryTenantId)) {
            selectedTenantId = queryTenantId;
            tenantSelect.value = queryTenantId;
            await loadLoginSettings(queryTenantId);
        }
    };

    const populateLoginSettingsForm = (data) => {
        if (!form || !data) return;
        form.elements.twoFactorEnabled.checked = data.twoFactorEnabled === true;
        form.elements.mfaRequiredSetting.checked = data.mfaRequired === true;
        form.elements.emailLoginEnabled.checked = data.emailLoginEnabled === true;
        form.elements.phoneLoginEnabled.checked = data.phoneLoginEnabled === true;
        form.elements.passwordMinLength.value = data.passwordMinLength ?? 8;
        form.elements.passwordRequireUppercase.checked = data.passwordRequireUppercase === true;
        form.elements.passwordRequireLowercase.checked = data.passwordRequireLowercase === true;
        form.elements.passwordRequireDigit.checked = data.passwordRequireDigit === true;
        form.elements.passwordRequireSpecialChar.checked = data.passwordRequireSpecialChar === true;
        form.elements.passwordExpirationDays.value = data.passwordExpirationDays ?? '';
        form.elements.sessionTimeoutMinutes.value = data.sessionTimeoutMinutes ?? 60;
        form.elements.refreshTokenLifetimeDays.value = data.refreshTokenLifetimeDays ?? 7;
        form.elements.maxFailedLoginAttempts.value = data.maxFailedLoginAttempts ?? 5;
        form.elements.lockoutDurationMinutes.value = data.lockoutDurationMinutes ?? 15;
        form.elements.ipWhitelistEnabled.checked = data.ipWhitelistEnabled === true;
        
        // Tagify for IPs
        if (tagifyIps && typeof tagifyIps.addTags === 'function') {
            tagifyIps.removeAllTags();
            if (data.allowedIps && data.allowedIps.length > 0) {
                tagifyIps.addTags(data.allowedIps);
            }
        }

        // Select2 for Countries
        const countrySelect = $(form?.elements.allowedCountries);
        if (countrySelect.length && typeof countrySelect.select2 === 'function') {
            countrySelect.val(data.allowedCountries || []).trigger('change');
        }

        form.elements.loginAuditRetentionDays.value = data.loginAuditRetentionDays ?? 90;
        updateIpWhitelistState();
    };

    const loadLoginSettings = async (tenantId) => {
        clearErrors();
        selectedTenantId = tenantId || '';
        updateSelectedTenantSummary();
        setLoading(true);
        try {
            const data = await fetchJson(`${apiUrl}/api/admin/tenants/${encodeURIComponent(selectedTenantId)}/login-settings`);
            if (data) populateLoginSettingsForm(data);
        } catch (error) {
            if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred, 'error');
        } finally {
            setLoading(false);
        }
    };

    const buildLoginSettingsPayload = () => {
        const getIps = () => tagifyIps ? tagifyIps.value.map(t => t.value) : [];
        const getCountries = () => {
            const el = $(form?.elements.allowedCountries);
            return (typeof el.val === 'function') ? (el.val() || []) : [];
        };

        return {
            twoFactorEnabled: readBool('twoFactorEnabled'),
            mfaRequired: readBool('mfaRequired'),
            emailLoginEnabled: readBool('emailLoginEnabled'),
            phoneLoginEnabled: readBool('phoneLoginEnabled'),
            passwordMinLength: readNumber('passwordMinLength'),
            passwordRequireUppercase: readBool('passwordRequireUppercase'),
            passwordRequireLowercase: readBool('passwordRequireLowercase'),
            passwordRequireDigit: readBool('passwordRequireDigit'),
            passwordRequireSpecialChar: readBool('passwordRequireSpecialChar'),
            passwordExpirationDays: readNumber('passwordExpirationDays'),
            sessionTimeoutMinutes: readNumber('sessionTimeoutMinutes'),
            refreshTokenLifetimeDays: readNumber('refreshTokenLifetimeDays'),
            maxFailedLoginAttempts: readNumber('maxFailedLoginAttempts'),
            lockoutDurationMinutes: readNumber('lockoutDurationMinutes'),
            ipWhitelistEnabled: readBool('ipWhitelistEnabled'),
            allowedIps: getIps(),
            allowedCountries: getCountries(),
            loginAuditRetentionDays: readNumber('loginAuditRetentionDays')
        };
    };

    const validatePayload = (payload) => {
        const errors = {};
        const addError = (field, msg) => {
            if (!errors[field]) errors[field] = [];
            errors[field].push(msg);
        };

        // Rule 1 — MFA Required requires Two Factor
        if (payload.mfaRequired && !payload.twoFactorEnabled) {
            addError('MfaRequired', L.MfaRequires2FA || 'Two Factor Authentication must be enabled when MFA is required.');
        }

        // Rule 2 — At least one login method must be enabled
        if (!payload.emailLoginEnabled && !payload.phoneLoginEnabled) {
            addError('EmailLoginEnabled', L.AtLeastOneLoginMethod || 'At least one login method must be enabled.');
        }

        // Rule 3 — IP whitelist enabled requires allowed IPs
        if (payload.ipWhitelistEnabled && (!payload.allowedIps || payload.allowedIps.length === 0)) {
            addError('AllowedIps', L.IpWhitelistEmpty || 'At least one allowed IP address is required when IP whitelist is enabled.');
        }

        // Rule 4 — AllowedIps must contain valid IP addresses
        const ipRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$|^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$/;
        payload.allowedIps?.forEach(ip => {
            if (!ipRegex.test(ip)) addError('AllowedIps', (L.InvalidIp || 'Invalid IP address: ') + ip);
        });

        // Rule 5 — AllowedCountries must be ISO alpha-2
        const countryRegex = /^[A-Z]{2}$/;
        payload.allowedCountries?.forEach(code => {
            if (!countryRegex.test(code)) addError('AllowedCountries', (L.InvalidCountry || 'Invalid country code: ') + code + '. Use ISO alpha-2 format.');
        });

        return { isValid: Object.keys(errors).length === 0, errors };
    };

    const saveLoginSettings = async () => {
        if (!selectedTenantId) {
            window.showToast?.(L.SelectTenant, 'warning');
            return;
        }

        clearErrors();
        const payload = buildLoginSettingsPayload();
        const validation = validatePayload(payload);

        if (!validation.isValid) {
            showErrors(validation);
            return;
        }

        setLoading(true);
        try {
            const data = await fetchJson(`${apiUrl}/api/admin/tenants/${encodeURIComponent(selectedTenantId)}/login-settings`, {
                method: 'PUT',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            if (data) {
                populateLoginSettingsForm(data);
                window.showToast?.(L.LoginSecuritySaved, 'success');
            }
        } catch (error) {
            if (isAuthHandledError(error)) return;
            if (error.problem) {
                showErrors(error.problem);
                return;
            }
            window.showToast?.(L.ErrorOccurred, 'error');
        } finally {
            setLoading(false);
        }
    };

    const updateIpWhitelistState = () => {
        const enabled = readBool('ipWhitelistEnabled');
        if (tagifyIps && typeof tagifyIps.setReadOnly === 'function') {
            tagifyIps.setReadOnly(!enabled);
        }
    };

    const loadLookups = async () => {
        try {
            const data = await fetchJson(`${apiUrl}/api/lookups/countries`);
            if (!data) return;
            countryList = Array.isArray(data) ? data : [];
            const select = $(form?.elements.allowedCountries);
            if (select.length && typeof select.select2 === 'function') {
                select.empty();
                countryList.forEach(c => {
                    const option = new Option(`${c.name} (${c.code})`, c.code, false, false);
                    select.append(option);
                });
                select.trigger('change');
            }
        } catch (error) {
            console.error('Failed to load countries', error);
        }
    };

    const initPlugins = () => {
        // Tagify
        const ipInput = document.getElementById('allowedIps');
        if (ipInput && typeof Tagify === 'function') {
            tagifyIps = new Tagify(ipInput, {
                pattern: /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$|^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$/,
                delimiters: ",| ",
                maxTags: 50,
                dropdown: { enabled: 0 }
            });
        }

        // Select2
        const countrySelect = $(form?.elements.allowedCountries);
        if (countrySelect.length && typeof countrySelect.select2 === 'function') {
            countrySelect.select2({
                placeholder: L.SelectAllowedCountries || 'Select countries',
                allowClear: true
            });
        }
    };

    const bindEvents = () => {
        tenantSelect?.addEventListener('change', () => {
            const tenantId = tenantSelect.value;
            if (!tenantId) {
                selectedTenantId = '';
                updateSelectedTenantSummary();
                setLoading(false);
                return;
            }
            loadLoginSettings(tenantId);
        });

        form?.elements.mfaRequiredSetting?.addEventListener('change', (e) => {
            if (e.target.checked) {
                const twoFactor = form?.elements.twoFactorEnabled;
                if (twoFactor && !twoFactor.checked) {
                    twoFactor.checked = true;
                    window.showToast?.(L.TwoFactorAutoEnabled || 'Two Factor enabled automatically.', 'info');
                }
            }
        });

        form?.elements.twoFactorEnabled?.addEventListener('change', (e) => {
            if (!e.target.checked) {
                const mfa = form?.elements.mfaRequiredSetting;
                if (mfa && mfa.checked) {
                    mfa.checked = false;
                    window.showToast?.(L.MfaAutoDisabled || 'MFA disabled because Two Factor was turned off.', 'warning');
                }
            }
        });

        form?.elements.ipWhitelistEnabled?.addEventListener('change', () => {
            updateIpWhitelistState();
        });

        form?.addEventListener('submit', (event) => {
            event.preventDefault();
            saveLoginSettings();
        });

        form?.addEventListener('input', (event) => {
            event.target?.classList?.remove('is-invalid');
        });
    };

    return {
        init: () => {
            syncL10n();
            if (!root || !apiUrl) return;


            initPlugins();
            bindEvents();
            loadLookups();
            loadTenants().catch((error) => {
                if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred, 'error');
            });
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantSecurity.init());
