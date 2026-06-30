/**
 * Tenant Security Settings — self-service (tenant shell).
 * The tenant manages its OWN login/security settings; there is no tenant id in the path. Reads and the
 * single save go through the same-origin proxy (/TenantSecurity/api/login-settings), which forwards the
 * HttpOnly bearer token and the tenant claim to the gateway. The FULL loaded DTO is kept in memory so the
 * PUT payload includes every request field (form inputs override loaded values) — unshown fields round-trip
 * unchanged and server validation passes. Client-side validation mirrors the server rules for UX only;
 * the backend is authoritative.
 */
'use strict';

const TenantSecuritySettings = (function () {
    const root = document.getElementById('tenantSecurityRoot');
    const form = document.getElementById('formTenantSecurity');
    const saveButton = document.getElementById('btnSaveTenantSecurity');
    const baseUrl = '/TenantSecurity/api/login-settings';

    let L = {};
    let loadedDto = null; // full DTO from the last successful GET/PUT

    const loadL10n = () => {
        try {
            const el = document.getElementById('tenant-security-l10n');
            L = el ? JSON.parse(el.textContent) : {};
        } catch (_) { L = {}; }
    };

    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.()
        || window.DtDefaults?.getAuthHeaders?.()
        || {};

    const unwrap = (json) => {
        if (json?.data?.data !== undefined) return json.data.data;
        return json?.data ?? json?.Data ?? null;
    };

    const isAuthHandledError = (error) => error?.authHandled === true;

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
        document.querySelectorAll('[data-valmsg-for]').forEach((el) => { el.textContent = ''; });
        form?.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
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
            const messageElement = document.querySelector(`[data-valmsg-for="${key}"]`)
                || document.querySelector(`[data-valmsg-for="${key.charAt(0).toUpperCase() + key.slice(1)}"]`);
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
            const json = await response.json().catch(() => null);
            if (json?.redirectUrl) {
                window.location.href = json.redirectUrl;
            } else {
                window.DtDefaults?.handleUnauthorized?.();
            }
            const authError = new Error('auth-redirect-in-progress');
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
        saveButton.disabled = loading;
        if (loading) {
            saveButton.dataset.originalHtml = saveButton.innerHTML;
            saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>';
        } else if (saveButton.dataset.originalHtml) {
            saveButton.innerHTML = saveButton.dataset.originalHtml;
            delete saveButton.dataset.originalHtml;
        }
    };

    const populateForm = (data) => {
        if (!form || !data) return;
        form.elements.twoFactorEnabled.checked = data.twoFactorEnabled === true;
        form.elements.mfaRequired.checked = data.mfaRequired === true;
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
        form.elements.allowedIps.value = joinList(data.allowedIps);
        form.elements.allowedCountries.value = joinList(data.allowedCountries);
        form.elements.loginAuditRetentionDays.value = data.loginAuditRetentionDays ?? 90;
        updateIpWhitelistState();
    };

    // Build the full request payload: start from every request field of the loaded DTO so that
    // unshown/unchanged fields round-trip, then override with the current form input values.
    const buildPayload = () => {
        const dto = loadedDto || {};
        const allowedIps = splitList(form?.elements.allowedIps?.value);
        const allowedCountries = splitList(form?.elements.allowedCountries?.value)
            .map((c) => c.toUpperCase());

        return {
            twoFactorEnabled: readBool('twoFactorEnabled'),
            mfaRequired: readBool('mfaRequired'),
            emailLoginEnabled: readBool('emailLoginEnabled'),
            phoneLoginEnabled: readBool('phoneLoginEnabled'),
            passwordMinLength: readNumber('passwordMinLength') ?? dto.passwordMinLength,
            passwordRequireUppercase: readBool('passwordRequireUppercase'),
            passwordRequireLowercase: readBool('passwordRequireLowercase'),
            passwordRequireDigit: readBool('passwordRequireDigit'),
            passwordRequireSpecialChar: readBool('passwordRequireSpecialChar'),
            passwordExpirationDays: readNumber('passwordExpirationDays'),
            sessionTimeoutMinutes: readNumber('sessionTimeoutMinutes') ?? dto.sessionTimeoutMinutes,
            refreshTokenLifetimeDays: readNumber('refreshTokenLifetimeDays') ?? dto.refreshTokenLifetimeDays,
            maxFailedLoginAttempts: readNumber('maxFailedLoginAttempts') ?? dto.maxFailedLoginAttempts,
            lockoutDurationMinutes: readNumber('lockoutDurationMinutes') ?? dto.lockoutDurationMinutes,
            ipWhitelistEnabled: readBool('ipWhitelistEnabled'),
            allowedIps: allowedIps.length ? allowedIps : null,
            allowedCountries: allowedCountries.length ? allowedCountries : null,
            loginAuditRetentionDays: readNumber('loginAuditRetentionDays') ?? dto.loginAuditRetentionDays
        };
    };

    const ipRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$|^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$/;
    const countryRegex = /^[A-Z]{2}$/;

    const validatePayload = (payload) => {
        const errors = {};
        const addError = (field, msg) => {
            if (!errors[field]) errors[field] = [];
            errors[field].push(msg);
        };

        // Rule 1 — mfaRequired ⇒ twoFactorEnabled AND emailLoginEnabled
        if (payload.mfaRequired && (!payload.twoFactorEnabled || !payload.emailLoginEnabled)) {
            addError('MfaRequired', L.MfaRequires2FA || 'When MFA is required, two-factor and email login must both be enabled.');
        }

        // Rule 2 — at least one of email/phone login enabled
        if (!payload.emailLoginEnabled && !payload.phoneLoginEnabled) {
            addError('EmailLoginEnabled', L.AtLeastOneLoginMethod || 'At least one login method must be enabled.');
        }

        // Rule 3 — IP whitelist enabled requires non-empty allowedIps
        if (payload.ipWhitelistEnabled && (!payload.allowedIps || payload.allowedIps.length === 0)) {
            addError('AllowedIps', L.IpWhitelistEmpty || 'At least one allowed IP is required when IP whitelist is enabled.');
        }

        // Rule 4 — allowedIps items valid IPv4/IPv6
        (payload.allowedIps || []).forEach((ip) => {
            if (!ipRegex.test(ip)) addError('AllowedIps', (L.InvalidIp || 'Invalid IP address: ') + ip);
        });

        // Rule 5 — allowedCountries items ISO alpha-2
        (payload.allowedCountries || []).forEach((code) => {
            if (!countryRegex.test(code)) addError('AllowedCountries', (L.InvalidCountry || 'Invalid country code: ') + code);
        });

        return { isValid: Object.keys(errors).length === 0, errors };
    };

    const load = async () => {
        clearErrors();
        setLoading(true);
        try {
            const data = await fetchJson(baseUrl);
            if (data) {
                loadedDto = data;
                populateForm(data);
            }
        } catch (error) {
            if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred, 'error');
        } finally {
            setLoading(false);
        }
    };

    const save = async () => {
        clearErrors();
        const payload = buildPayload();
        const validation = validatePayload(payload);
        if (!validation.isValid) {
            showErrors(validation);
            return;
        }

        setLoading(true);
        try {
            const data = await fetchJson(baseUrl, {
                method: 'PUT',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            if (data) {
                loadedDto = data;
                populateForm(data);
            }
            window.showToast?.(L.Saved, 'success');
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
        const ipInput = form?.elements.allowedIps;
        if (ipInput) ipInput.disabled = !enabled;
    };

    const bindEvents = () => {
        // mfaRequired ⇒ twoFactorEnabled AND emailLoginEnabled (UX convenience auto-enable)
        form?.elements.mfaRequired?.addEventListener('change', (e) => {
            if (e.target.checked) {
                let changed = false;
                if (form.elements.twoFactorEnabled && !form.elements.twoFactorEnabled.checked) {
                    form.elements.twoFactorEnabled.checked = true; changed = true;
                }
                if (form.elements.emailLoginEnabled && !form.elements.emailLoginEnabled.checked) {
                    form.elements.emailLoginEnabled.checked = true; changed = true;
                }
                if (changed) window.showToast?.(L.TwoFactorAutoEnabled || '', 'info');
            }
        });

        form?.elements.twoFactorEnabled?.addEventListener('change', (e) => {
            if (!e.target.checked && form.elements.mfaRequired?.checked) {
                form.elements.mfaRequired.checked = false;
                window.showToast?.(L.MfaAutoDisabled || '', 'warning');
            }
        });

        form?.elements.ipWhitelistEnabled?.addEventListener('change', updateIpWhitelistState);

        form?.addEventListener('submit', (event) => {
            event.preventDefault();
            save();
        });

        form?.addEventListener('input', (event) => {
            event.target?.classList?.remove('is-invalid');
        });
    };

    return {
        init: () => {
            loadL10n();
            if (!root || !form) return;
            bindEvents();
            load();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantSecuritySettings.init());
