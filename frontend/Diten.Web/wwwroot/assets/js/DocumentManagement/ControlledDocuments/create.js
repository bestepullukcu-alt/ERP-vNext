/**
 * MOD-0029-FU01 - Create controlled document / template (Compact route page). Posts multipart (file + payload
 * JSON) to the same-origin proxy. All text from window.L10n; controlled reason_code/correlation_id surfaced.
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const params = new URLSearchParams(window.location.search);
    const isTemplate = (params.get('kind') || '').toLowerCase() === 'template';
    const form = document.getElementById('controlledDocumentForm');
    if (!form) return;
    const companySelect = document.getElementById('fldCompanyId');
    const folderSelect = document.getElementById('fldCollectionInstanceId');

    if (isTemplate) {
        document.getElementById('createHeading')?.replaceChildren(document.createTextNode(L.AddTemplate || 'Add Template'));
        document.querySelectorAll('[data-doc-only]').forEach((el) => el.classList.add('d-none'));
    }

    const unwrapList = (payload) => {
        const data = payload?.data || payload?.Data || payload;
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.items)) return data.items;
        if (Array.isArray(data?.Items)) return data.Items;
        return [];
    };

    const optionText = (item, fallback) =>
        item?.displayName || item?.DisplayName ||
        item?.legalName || item?.LegalName ||
        item?.name || item?.Name ||
        item?.fullPath || item?.FullPath ||
        fallback;

    const optionId = (item) =>
        item?.legalEntityId || item?.LegalEntityId ||
        item?.id || item?.Id;

    const folderDepth = (item) => {
        const path = String(item?.fullPath || item?.FullPath || '');
        if (!path) return 0;
        return Math.max(0, path.split('/').filter(Boolean).length - 1);
    };

    const initSelect2 = () => {
        if (!window.jQuery?.fn?.select2) return;
        const jq = window.jQuery;
        jq(companySelect).select2({
            dropdownParent: jq(document.body),
            width: '100%',
            allowClear: true,
            placeholder: jq(companySelect).data('placeholder') || ''
        });
        jq(folderSelect).select2({
            dropdownParent: jq(document.body),
            width: '100%',
            allowClear: true,
            placeholder: jq(folderSelect).data('placeholder') || '',
            templateResult: (state) => {
                if (!state.id) return state.text;
                const depth = Number(state.element?.dataset?.depth || 0);
                return jq('<span>').css('padding-left', `${depth * 14}px`).text(state.text);
            }
        });
    };

    const setSelectLoading = (select, isLoading) => {
        if (!select) return;
        select.disabled = isLoading || select.dataset.locked === 'true';
        if (window.jQuery?.fn?.select2) {
            window.jQuery(select).trigger('change.select2');
        }
    };

    const loadLegalEntities = async () => {
        if (!companySelect) return;
        try {
            const res = await fetch('/DocumentManagementControlledDocuments/legal-entities', { credentials: 'same-origin' });
            const json = await res.json().catch(() => ({}));
            if (!res.ok || json.isSuccessful === false) throw new Error('legal-entities');
            companySelect.innerHTML = '<option value=""></option>';
            unwrapList(json).forEach((item) => {
                const id = optionId(item);
                if (!id) return;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = optionText(item, id);
                companySelect.appendChild(opt);
            });
        } catch (_) {
            toast(L.ErrorOccurred || 'Error', 'error');
        }
    };

    const clearFolders = () => {
        if (!folderSelect) return;
        folderSelect.innerHTML = '<option value=""></option>';
        folderSelect.value = '';
        folderSelect.disabled = true;
        if (window.jQuery?.fn?.select2) {
            window.jQuery(folderSelect).val('').trigger('change');
        }
    };

    const loadFoldersForCompany = async (companyId) => {
        clearFolders();
        if (!companyId || !folderSelect) return;
        setSelectLoading(folderSelect, true);
        try {
            const res = await fetch(`/DocumentManagementControlledDocuments/collection-instances?companyId=${encodeURIComponent(companyId)}`, { credentials: 'same-origin' });
            const json = await res.json().catch(() => ({}));
            if (!res.ok || json.isSuccessful === false) throw new Error('collection-instances');
            const folders = unwrapList(json).slice().sort((a, b) => {
                const left = String(a.fullPath || a.FullPath || a.name || a.Name || '');
                const right = String(b.fullPath || b.FullPath || b.name || b.Name || '');
                return left.localeCompare(right);
            });
            folderSelect.innerHTML = '<option value=""></option>';
            folders.forEach((item) => {
                const id = item.id || item.Id;
                if (!id) return;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = optionText(item, id);
                opt.dataset.depth = String(folderDepth(item));
                folderSelect.appendChild(opt);
            });
            folderSelect.disabled = false;
        } catch (_) {
            toast(L.ErrorOccurred || 'Error', 'error');
        } finally {
            if (window.jQuery?.fn?.select2) {
                window.jQuery(folderSelect).trigger('change.select2');
            }
        }
    };

    const initLookups = async () => {
        initSelect2();
        await loadLegalEntities();
        if (window.jQuery?.fn?.select2) {
            window.jQuery(companySelect).trigger('change.select2');
        }
        companySelect?.addEventListener('change', (event) => {
            loadFoldersForCompany(event.target.value);
        });
    };

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const toast = (msg, kind) => window.showToast?.(msg, kind);
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed, CONFLICT: L.ReasonConflict, PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound, STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable,
        FEATURE_DISABLED: L.ReasonFeatureDisabled
    }[code] || code);

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const fileEl = document.getElementById('fldFile');
        const file = fileEl?.files?.[0];
        const title = document.getElementById('fldTitle')?.value?.trim();
        const companyId = companySelect?.value?.trim();
        const collectionInstanceId = folderSelect?.value?.trim();
        if (!title) { toast(L.TitleRequired || 'Title required', 'error'); return; }
        if (!companyId || !collectionInstanceId) { toast(L.ReasonValidationFailed || 'Validation failed', 'error'); return; }
        if (!file) { toast(L.FileRequired || 'File required', 'error'); return; }

        const tags = (document.getElementById('fldTags')?.value || '')
            .split(',').map((t) => t.trim()).filter(Boolean);

        const payload = isTemplate
            ? {
                companyId,
                collectionInstanceId,
                title, description: document.getElementById('fldDescription')?.value?.trim() || null, tags,
                flags: { reusable: true, shareable: true, copyableOnAdopt: false, referenceOnly: false }
            }
            : {
                collectionInstanceId,
                companyId,
                title, documentType: document.getElementById('fldDocumentType')?.value,
                description: document.getElementById('fldDescription')?.value?.trim() || null, tags,
                controlled: document.getElementById('fldControlled')?.checked ?? true,
                effectiveDate: document.getElementById('fldEffectiveDate')?.value || null,
                reviewDate: document.getElementById('fldReviewDate')?.value || null,
                expiryDate: document.getElementById('fldExpiryDate')?.value || null
            };

        const fd = new FormData();
        fd.append('file', file);
        fd.append('payloadJson', JSON.stringify(payload));
        fd.append('__RequestVerificationToken', token());

        try {
            const url = isTemplate ? '/DocumentManagementControlledDocuments/templates/create' : '/DocumentManagementControlledDocuments/create';
            const res = await fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' });
            const json = await res.json().catch(() => ({}));
            if (res.ok && json.isSuccessful !== false) {
                toast(isTemplate ? (L.RecordSaved || 'Saved') : (L.DocumentCreated || 'Document created'), 'success');
                setTimeout(() => { window.location.href = '/DocumentManagementControlledDocuments'; }, 600);
            } else {
                const corr = json.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
                toast(`${reasonText(json.reason_code) || L.ErrorOccurred}${corr}`, 'error');
            }
        } catch (_) {
            toast(L.ErrorOccurred || 'Error', 'error');
        }
    });

    initLookups();
})();
