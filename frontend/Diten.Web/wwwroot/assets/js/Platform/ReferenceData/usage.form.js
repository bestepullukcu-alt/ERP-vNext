/**
 * Business Reference Data - Usage Dependency Create/Edit form (proxy-profile, No-ViewModel).
 * Submits via window.ReferenceDataApi to the same-origin proxy; redirects to the set-scoped list.
 */
'use strict';

(function () {
    const form = document.getElementById('usageForm');
    if (!form) return;

    const api = window.ReferenceDataApi;
    const L = window.L10n || {};
    const setCode = form.dataset.setCode || '';
    const mode = form.dataset.mode || 'create';
    const usageId = form.dataset.usageId || '';
    const listUrl = `/Platform/ReferenceData/Usage/${encodeURIComponent(setCode)}`;
    let scopeKeysByScopeType = {};
    let setScopeType = '';

    const statusEl = document.getElementById('rd-usage-form-status');
    const get = (id) => document.getElementById(id);

    const setStatus = (message, level) => {
        if (!statusEl) return;
        if (!message) { statusEl.className = 'alert d-none mb-3'; statusEl.textContent = ''; return; }
        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'info';
        statusEl.className = `alert alert-${css} mb-3`;
        statusEl.textContent = message;
    };

    const toIsoOrNull = (localValue) => {
        if (!localValue) return null;
        const d = new Date(localValue);
        return Number.isNaN(d.getTime()) ? null : d.toISOString();
    };
    const toLocalInput = (value) => {
        if (!value) return '';
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return '';
        const pad = (n) => String(n).padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };

    const toggleConditionalFields = () => {
        const resolution = get('usageResolution')?.value || 'latest';
        const pinWrap = get('usageVersionPinWrap');
        const asOfWrap = get('usageAsOfWrap');
        if (pinWrap) pinWrap.classList.toggle('d-none', resolution !== 'pinned');
        if (asOfWrap) asOfWrap.classList.toggle('d-none', resolution !== 'as-of');
    };

    const appendOptions = (selectId, items) => {
        const select = get(selectId);
        if (!select) return;
        items.forEach((item) => {
            const value = item.value ?? item.Value;
            if (value == null || value === '') return;
            if (select.querySelector(`option[value="${CSS.escape(String(value))}"]`)) return;
            const opt = document.createElement('option');
            opt.value = value;
            opt.textContent = item.label ?? item.Label ?? value;
            select.appendChild(opt);
        });
    };
    const resetOptions = (selectId) => {
        const select = get(selectId);
        if (!select) return;
        const placeholder = select.querySelector('option[value=""]')?.cloneNode(true);
        select.replaceChildren();
        if (placeholder) select.appendChild(placeholder);
    };
    const ensureOption = (selectId, value, label) => {
        const select = get(selectId);
        if (!select || value == null || value === '') return;
        if (!select.querySelector(`option[value="${CSS.escape(String(value))}"]`)) {
            const opt = document.createElement('option');
            opt.value = value;
            opt.textContent = label || value;
            select.appendChild(opt);
        }
    };
    const setSelect = (selectId, value) => {
        const select = get(selectId);
        if (!select) return;
        select.value = value ?? '';
        if (window.jQuery) jQuery(select).trigger('change');
    };
    const normalizeScopeType = (value) => String(value || '').trim().toLowerCase();
    const normalizeScopeKeysByType = (data) => {
        const raw = data?.scopeKeysByScopeType || data?.ScopeKeysByScopeType || {};
        const normalized = {};
        Object.keys(raw || {}).forEach((key) => {
            normalized[normalizeScopeType(key)] = Array.isArray(raw[key]) ? raw[key] : [];
        });
        const legacyKeys = data?.scopeKeys || data?.ScopeKeys || [];
        const type = data?.setScopeType || data?.SetScopeType || '';
        if (legacyKeys.length && type && !normalized[normalizeScopeType(type)]) {
            normalized[normalizeScopeType(type)] = legacyKeys;
        }
        return normalized;
    };
    const populateScopeKeys = (selectedValue) => {
        const scopeType = normalizeScopeType(selectedValue || get('usageScopeType')?.value || setScopeType);
        const select = get('usageScopeKey');
        resetOptions('usageScopeKey');
        const keys = scopeType === 'global' ? [] : (scopeKeysByScopeType[scopeType] || []);
        appendOptions('usageScopeKey', keys.map((k) => ({ value: k, label: k })));
        if (select) {
            select.disabled = !scopeType || scopeType === 'global';
            if (select.disabled) select.value = '';
            if (window.jQuery) jQuery(select).trigger('change');
        }
    };

    const loadOptions = async () => {
        if (typeof api?.getUsageFormOptions !== 'function') return;
        try {
            const data = await api.getUsageFormOptions(setCode);
            appendOptions('usageConsumerModule', data?.consumerModules || data?.ConsumerModules || []);
            appendOptions('usageScopeType', data?.scopeTypes || data?.ScopeTypes || []);
            setScopeType = data?.setScopeType || data?.SetScopeType || '';
            scopeKeysByScopeType = normalizeScopeKeysByType(data);
            if (setScopeType && !get('usageScopeType')?.value) {
                setSelect('usageScopeType', setScopeType);
            }
            populateScopeKeys(setScopeType);
        } catch (error) {
            if (error?.isHandled) return;
            console.warn('[Usage Form] Failed to load form options.', error);
        }
    };

    const readPayload = () => {
        const resolution = get('usageResolution')?.value || 'latest';
        const versionPinRaw = get('usageVersionPin')?.value;
        const asOfRaw = get('usageAsOf')?.value;
        return {
            set_code: setCode,
            consumer_module: (get('usageConsumerModule')?.value || '').trim(),
            consumer_name: (get('usageConsumerName')?.value || '').trim(),
            consumer_endpoint: (get('usageConsumerEndpoint')?.value || '').trim() || null,
            scope_type: (get('usageScopeType')?.value || '').trim() || null,
            scope_key: (get('usageScopeKey')?.value || '').trim() || null,
            resolution_mode: resolution,
            version_pin: versionPinRaw ? Number(versionPinRaw) : null,
            as_of_date: toIsoOrNull(asOfRaw),
            criticality: get('usageCriticality')?.value || 'medium',
            notes: (get('usageNotes')?.value || '').trim() || null
        };
    };

    const prefill = (item) => {
        if (!item) return;
        const module = item.consumerModule || item.ConsumerModule || '';
        const scopeType = item.scopeType || item.ScopeType || '';
        const scopeKey = item.scopeKey || item.ScopeKey || '';
        // Saved values may no longer be in the current option set — keep them selectable.
        ensureOption('usageConsumerModule', module, module);
        ensureOption('usageScopeType', scopeType, scopeType);
        populateScopeKeys(scopeType);
        ensureOption('usageScopeKey', scopeKey, scopeKey);
        setSelect('usageConsumerModule', module);
        setSelect('usageScopeType', scopeType);
        setSelect('usageScopeKey', scopeKey);
        get('usageConsumerName').value = item.consumerName || item.ConsumerName || '';
        get('usageConsumerEndpoint').value = item.consumerEndpoint || item.ConsumerEndpoint || '';
        setSelect('usageResolution', (item.resolutionMode || item.ResolutionMode || 'latest').toLowerCase());
        const pin = item.versionPin || item.VersionPin;
        if (pin) get('usageVersionPin').value = pin;
        const asOf = item.asOfDate || item.AsOfDate;
        if (asOf) get('usageAsOf').value = toLocalInput(asOf);
        setSelect('usageCriticality', (item.criticality || item.Criticality || 'medium').toLowerCase());
        get('usageNotes').value = item.notes || item.Notes || '';
        toggleConditionalFields();
    };

    const loadForEdit = async () => {
        if (mode !== 'edit' || !usageId || typeof api?.getUsageRegistrations !== 'function') return;
        try {
            const data = await api.getUsageRegistrations(setCode);
            const items = data?.items || data?.Items || [];
            const item = items.find((x) => String(x.usageRegistrationId || x.UsageRegistrationId) === String(usageId));
            if (item) prefill(item);
            else setStatus(L.ErrorOccurred || 'Record not found.', 'error');
        } catch (error) {
            if (error?.isHandled) return;
            setStatus(error?.message || L.ErrorOccurred || '', 'error');
        }
    };

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        setStatus('', null);
        const payload = readPayload();

        if (!payload.consumer_module || !payload.consumer_name) {
            form.classList.add('was-validated');
            return;
        }
        if (payload.resolution_mode === 'pinned' && !payload.version_pin) {
            setStatus(L.UsageVersionPinRequired || 'Pinned version is required.', 'error');
            return;
        }
        if (payload.resolution_mode === 'as-of' && !payload.as_of_date) {
            setStatus(L.UsageAsOfRequired || 'As-of date is required.', 'error');
            return;
        }

        const submitBtn = document.querySelector('button[type="submit"][form="usageForm"]');
        if (submitBtn) submitBtn.disabled = true;
        try {
            await api.registerUsage(payload);
            window.showToast?.(mode === 'edit' ? (L.RecordUpdated || L.RecordSaved) : (L.RecordCreated || L.RecordSaved), 'success');
            setTimeout(() => { window.location.href = listUrl; }, 500);
        } catch (error) {
            if (submitBtn) submitBtn.disabled = false;
            if (error?.isHandled) return;
            setStatus(error?.message || L.ErrorOccurred || '', 'error');
        }
    });

    const initSelect2 = () => {
        if (!window.jQuery || !jQuery.fn.select2) return;
        jQuery('#usageForm .select2').each(function () {
            const $this = jQuery(this);
            if ($this.hasClass('select2-hidden-accessible')) return;
            $this.wrap('<div class="position-relative"></div>').select2({ dropdownParent: $this.parent() });
        });
        jQuery('#usageResolution').on('change', toggleConditionalFields);
    };

    get('usageResolution')?.addEventListener('change', toggleConditionalFields);
    get('usageScopeType')?.addEventListener('change', (event) => populateScopeKeys(event.target.value));

    document.addEventListener('DOMContentLoaded', async () => {
        await loadOptions();
        initSelect2();
        toggleConditionalFields();
        await loadForEdit();
    });
})();
