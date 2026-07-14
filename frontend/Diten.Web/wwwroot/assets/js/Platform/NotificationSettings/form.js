/**
 * Notification Settings - Create/Edit form (MOD-0027-FU02).
 * No-ViewModel: loads/saves through the same-origin /Platform/NotificationSettings/api proxy.
 * Never collects raw secrets: only a CredentialSecretRef (MOD-0012 secret reference) is accepted.
 */
'use strict';

(function () {
    const apiBase = '/Platform/NotificationSettings/api';
    const form = document.getElementById('notificationSettingsForm');
    if (!form) return;

    const mode = form.dataset.mode || 'create';
    let targetTenantId = form.dataset.targetTenantId || '';
    const L = () => window.L10n || {};

    const errorSummary = document.getElementById('formErrorSummary');
    const unwrap = (payload) => payload?.data ?? payload?.Data ?? null;
    const errorsOf = (payload) => payload?.errors || payload?.Errors || [];

    const showSummary = (messages) => {
        if (!errorSummary) return;
        const list = (Array.isArray(messages) ? messages : [messages]).filter(Boolean);
        errorSummary.innerHTML = list.map((m) => `<div>${m}</div>`).join('');
        errorSummary.classList.toggle('d-none', list.length === 0);
    };
    const clearFieldErrors = () => {
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        form.querySelectorAll('.invalid-feedback').forEach((el) => { el.textContent = ''; });
    };
    const setFieldError = (fieldName, message) => {
        const feedback = form.querySelector(`[data-valmsg-for="${fieldName}"]`);
        if (feedback) {
            feedback.textContent = message;
            feedback.previousElementSibling?.classList?.add('is-invalid');
        }
    };
    const mapServerErrors = (messages) => {
        const fieldMap = [
            ['SenderEmail', /sender\s*email/i],
            ['ReplyToEmail', /reply/i],
            ['Host', /host/i],
            ['Port', /port/i],
            ['ApiBaseUrl', /api\s*base|url/i],
            ['CredentialSecretRef', /secret|credential|password|api\s*key/i],
            ['FallbackPolicy', /fallback/i],
            ['ProviderCode', /provider/i]
        ];
        const unmapped = [];
        (messages || []).forEach((message) => {
            const hit = fieldMap.find(([, pattern]) => pattern.test(message));
            if (hit) setFieldError(hit[0], message);
            else unmapped.push(message);
        });
        showSummary(unmapped);
    };

    const fillSelect = (select, options, selected) => {
        if (!select) return;
        select.innerHTML = '';
        options.forEach((item) => {
            if (!item?.value) return;
            const opt = document.createElement('option');
            opt.value = item.value;
            opt.textContent = item.name || item.code || item.value;
            select.appendChild(opt);
        });
        if (selected) select.value = selected;
    };
    const fetchLookup = async (key) => {
        const res = await fetch(`${apiBase}/lookups/${encodeURIComponent(key)}`, { credentials: 'same-origin' });
        if (!res.ok) throw new Error(`Lookup '${key}' failed (${res.status}).`);
        return unwrap(await res.json()) || [];
    };

    const syncProviderFields = () => {
        const provider = document.getElementById('providerCode')?.value || '';
        const isSmtp = provider === 'Smtp';
        const isApi = provider === 'SendGrid';
        document.querySelectorAll('.smtp-only').forEach((el) => el.classList.toggle('d-none', !isSmtp));
        document.querySelectorAll('.api-only').forEach((el) => el.classList.toggle('d-none', !isApi));
        document.getElementById('smtpHost')?.toggleAttribute('required', isSmtp);
        document.getElementById('smtpPort')?.toggleAttribute('required', isSmtp);
    };

    const currentTenantId = () =>
        mode === 'edit' ? targetTenantId : (document.getElementById('targetTenant')?.value || '');

    const loadResolved = async () => {
        const tenantId = currentTenantId();
        const resolvedError = document.getElementById('resolvedError');
        const resolvedResult = document.getElementById('resolvedResult');
        const resolvedEmpty = document.getElementById('resolvedEmptyState');
        resolvedError?.classList.add('d-none');
        if (!tenantId) {
            resolvedResult?.classList.add('d-none');
            resolvedEmpty?.classList.remove('d-none');
            return;
        }
        resolvedEmpty?.classList.add('d-none');
        try {
            const res = await fetch(`${apiBase}/${tenantId}/resolved`, { credentials: 'same-origin' });
            const body = await res.json().catch(() => null);
            if (!res.ok) {
                // Controlled state: no tenant settings AND no platform default -> resolver fails; never fake a fallback.
                resolvedResult?.classList.add('d-none');
                if (resolvedError) {
                    resolvedError.textContent = errorsOf(body).join(' ') || L().ResolvedUnavailable || '';
                    resolvedError.classList.remove('d-none');
                }
                return;
            }
            const dto = unwrap(body);
            document.getElementById('resolvedSource').innerText = dto?.isPlatformDefault
                ? (L().ResolvedFromPlatformDefault || 'Platform default')
                : (L().ResolvedFromTenant || 'Tenant-specific');
            document.getElementById('resolvedProvider').innerText = dto?.providerCode || '-';
            document.getElementById('resolvedSenderEmail').innerText = dto?.senderEmail || '-';
            document.getElementById('resolvedFallbackPolicy').innerText = dto?.fallbackPolicy || '-';
            resolvedResult?.classList.remove('d-none');
        } catch (error) {
            console.error('[NotificationSettings Resolved] Failed.', error);
            if (resolvedError) {
                resolvedError.textContent = L().ResolvedUnavailable || '';
                resolvedError.classList.remove('d-none');
            }
        }
    };

    const loadTenants = async () => {
        const select = document.getElementById('targetTenant');
        if (!select) return;
        try {
            const res = await fetch(`${apiBase}/tenants?page=1&pageSize=100`, { credentials: 'same-origin' });
            if (!res.ok) return;
            const payload = await res.json();
            const raw = payload?.data?.items || payload?.data || payload?.Data || [];
            const tenants = (Array.isArray(raw) ? raw : []).map((t) => ({
                value: t.id || t.Id,
                name: t.displayName || t.DisplayName || t.name || t.Name || t.id || t.Id
            }));
            // Edit mode: the target tenant is locked (disabled select) — still populate options and
            // pre-select the current tenant so its name shows instead of an empty field.
            fillSelect(select, tenants, mode === 'edit' ? targetTenantId : '');
            // Safety net: if the locked tenant is not within the first page of the list, ensure it is
            // still present and selected so the field is never blank.
            if (mode === 'edit' && targetTenantId && select.value !== targetTenantId) {
                const opt = document.createElement('option');
                opt.value = targetTenantId;
                opt.textContent = targetTenantId;
                opt.selected = true;
                select.appendChild(opt);
            }
        } catch (error) {
            console.error('[NotificationSettings Form] Tenant load failed.', error);
        }
    };

    const loadSettings = async () => {
        if (mode !== 'edit' || !targetTenantId) return;
        try {
            const res = await fetch(`${apiBase}/${targetTenantId}`, { credentials: 'same-origin' });
            const body = await res.json();
            if (!res.ok) {
                showSummary(errorsOf(body).join(' ') || L().ErrorOccurred);
                return;
            }
            const dto = unwrap(body);
            if (!dto) return;
            document.getElementById('providerCode').value = dto.providerCode || '';
            document.getElementById('senderEmail').value = dto.senderEmail || '';
            document.getElementById('senderName').value = dto.senderName || '';
            document.getElementById('replyToEmail').value = dto.replyToEmail || '';
            document.getElementById('smtpHost').value = dto.host || '';
            document.getElementById('smtpPort').value = dto.port ?? '';
            document.getElementById('useSsl').checked = dto.useSsl !== false;
            document.getElementById('apiBaseUrl').value = dto.apiBaseUrl || '';
            document.getElementById('credentialSecretRef').value = dto.credentialSecretRef || '';
            document.getElementById('isEnabled').checked = dto.isEnabled !== false;
            document.getElementById('fallbackPolicy').value = dto.fallbackPolicy || '';
            syncProviderFields();
        } catch (error) {
            console.error('[NotificationSettings Form] Load failed.', error);
            showSummary(L().ErrorOccurred);
        }
    };

    const submitForm = async (event) => {
        event.preventDefault();
        clearFieldErrors();
        showSummary([]);
        const tenantId = currentTenantId();
        if (!tenantId) {
            setFieldError('TargetTenant', L().ErrorOccurred || '');
            return;
        }
        const provider = document.getElementById('providerCode')?.value || '';
        const isSmtp = provider === 'Smtp';
        const portRaw = document.getElementById('smtpPort')?.value;
        const payload = {
            providerCode: provider,
            senderEmail: document.getElementById('senderEmail')?.value?.trim() || '',
            senderName: document.getElementById('senderName')?.value?.trim() || null,
            replyToEmail: document.getElementById('replyToEmail')?.value?.trim() || null,
            host: isSmtp ? (document.getElementById('smtpHost')?.value?.trim() || null) : null,
            port: isSmtp && portRaw ? Number(portRaw) : null,
            useSsl: !!document.getElementById('useSsl')?.checked,
            apiBaseUrl: document.getElementById('apiBaseUrl')?.value?.trim() || null,
            credentialSecretRef: document.getElementById('credentialSecretRef')?.value?.trim() || null,
            isEnabled: !!document.getElementById('isEnabled')?.checked,
            fallbackPolicy: document.getElementById('fallbackPolicy')?.value || ''
        };
        const saveButton = document.getElementById('btnSaveSettings');
        saveButton?.setAttribute('disabled', 'disabled');
        try {
            const res = await fetch(`${apiBase}/${tenantId}`, {
                method: 'PUT',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const body = res.status === 204 ? null : await res.json().catch(() => null);
            if (res.ok) {
                window.showToast?.(L().RecordSaved || '', 'success');
                window.location.href = '/Platform/NotificationSettings';
                return;
            }
            if (res.status === 409) {
                showSummary(errorsOf(body).join(' ') || L().ErrorOccurred);
                return;
            }
            mapServerErrors(errorsOf(body));
            if (!errorsOf(body).length) showSummary(L().ErrorOccurred);
        } catch (error) {
            console.error('[NotificationSettings Form] Save failed.', error);
            showSummary(L().ErrorOccurred);
        } finally {
            saveButton?.removeAttribute('disabled');
        }
    };

    const init = async () => {
        try {
            const [providers, policies] = await Promise.all([
                fetchLookup('messaging-providers'),
                fetchLookup('notification-fallback-policies')
            ]);
            fillSelect(document.getElementById('providerCode'), providers);
            fillSelect(document.getElementById('fallbackPolicy'), policies);
        } catch (error) {
            console.error('[NotificationSettings Form] Lookup load failed.', error);
            showSummary(L().ErrorOccurred);
        }
        await loadTenants();
        await loadSettings();
        syncProviderFields();
        document.getElementById('providerCode')?.addEventListener('change', syncProviderFields);
        document.getElementById('btnLoadResolved')?.addEventListener('click', loadResolved);
        form.addEventListener('submit', submitForm);
        if (mode === 'edit') void loadResolved();
        else document.getElementById('resolvedEmptyState')?.classList.remove('d-none');
    };

    document.addEventListener('DOMContentLoaded', init);
})();
