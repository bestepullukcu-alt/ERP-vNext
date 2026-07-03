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
        if (Array.isArray(data?.results)) return data.results;
        if (Array.isArray(data?.Results)) return data.Results;
        return [];
    };

    const upper = (value) => String(value || '').toUpperCase();
    const get = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];

    const optionText = (item, fallback) =>
        get(item, 'displayName', 'DisplayName') ||
        get(item, 'legalName', 'LegalName') ||
        get(item, 'fullPath', 'FullPath') ||
        get(item, 'name', 'Name') ||
        fallback;

    const optionId = (item) =>
        get(item, 'legalEntityId', 'LegalEntityId') ||
        get(item, 'collectionInstanceId', 'CollectionInstanceId') ||
        get(item, 'id', 'Id');

    const structureId = (item) =>
        get(item, 'activeStructureId', 'ActiveStructureId') ||
        get(item, 'rootCollectionInstanceId', 'RootCollectionInstanceId') ||
        get(item, 'id', 'Id');

    const folderDepth = (item) => {
        const path = String(get(item, 'fullPath', 'FullPath') || '');
        if (!path) return 0;
        return Math.max(0, path.split('/').filter(Boolean).length - 1);
    };

    const isActiveFolder = (item) => upper(get(item, 'instanceStatus', 'InstanceStatus')) === 'ACTIVE';
    const isActiveStructure = (item) => {
        const status = upper(get(item, 'status', 'Status'));
        return status === 'ACTIVE' || status === 'PROVISIONED';
    };

    const deriveStructuresFromFolders = (folders) => {
        const grouped = new Map();
        folders.filter(isActiveFolder).forEach((folder) => {
            const baselineId = get(folder, 'baselineReleaseId', 'BaselineReleaseId') || '__company_structure__';
            if (!grouped.has(baselineId)) grouped.set(baselineId, []);
            grouped.get(baselineId).push(folder);
        });

        return Array.from(grouped.entries()).map(([baselineId, group]) => {
            const root = group.slice().sort((a, b) => {
                const pathA = String(get(a, 'fullPath', 'FullPath') || get(a, 'name', 'Name') || '');
                const pathB = String(get(b, 'fullPath', 'FullPath') || get(b, 'name', 'Name') || '');
                const depthA = pathA.split('/').filter(Boolean).length;
                const depthB = pathB.split('/').filter(Boolean).length;
                return depthA - depthB
                    || Number(get(a, 'displayOrder', 'DisplayOrder') || 0) - Number(get(b, 'displayOrder', 'DisplayOrder') || 0)
                    || pathA.localeCompare(pathB);
            })[0];
            return {
                activeStructureId: optionId(root),
                rootCollectionInstanceId: optionId(root),
                baselineReleaseId: baselineId === '__company_structure__' ? '' : baselineId,
                displayName: optionText(root, optionId(root)),
                status: 'ACTIVE'
            };
        });
    };

    const fetchLookup = async (url, reason) => {
        const res = await fetch(url, { credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (!res.ok || json.isSuccessful === false) throw new Error(reason);
        return json;
    };

    const setSelectDisabled = (select, disabled) => {
        if (!select) return;
        const jq = window.jQuery;
        select.disabled = disabled;
        select.toggleAttribute('disabled', disabled);
        if (jq?.fn?.select2) {
            const $select = jq(select);
            $select.prop('disabled', disabled).trigger('change');
            const $container = $select.next('.select2-container');
            $container
                .toggleClass('select2-container--disabled', disabled)
                .attr('aria-disabled', disabled ? 'true' : 'false');
        }
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
        setSelectDisabled(folderSelect, true);
    };

    const setSelectLoading = (select, isLoading) => {
        if (!select) return;
        setSelectDisabled(select, isLoading || select.dataset.locked === 'true');
    };

    const loadLegalEntities = async () => {
        if (!companySelect) return;
        try {
            const json = await fetchLookup('/DocumentManagementControlledDocuments/legal-entities', 'legal-entities');
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
        setSelectDisabled(folderSelect, true);
        if (window.jQuery?.fn?.select2) {
            window.jQuery(folderSelect).val('').trigger('change');
        }
    };

    const loadFoldersForCompany = async (companyId) => {
        clearFolders();
        if (!companyId || !folderSelect) return;
        setSelectLoading(folderSelect, true);
        try {
            const [structureJson, folderJson] = await Promise.all([
                fetchLookup(`/DocumentManagementControlledDocuments/documentation-structures?companyId=${encodeURIComponent(companyId)}`, 'documentation-structures'),
                fetchLookup(`/DocumentManagementControlledDocuments/collection-instances?companyId=${encodeURIComponent(companyId)}`, 'collection-instances')
            ]);
            const allFolders = unwrapList(folderJson);
            let structures = unwrapList(structureJson).filter(isActiveStructure);
            if (!structures.length) structures = deriveStructuresFromFolders(allFolders);
            const baselineIds = new Set(structures
                .map((item) => get(item, 'baselineReleaseId', 'BaselineReleaseId'))
                .filter(Boolean)
                .map(String));
            const rootIds = new Set(structures.map(structureId).filter(Boolean).map(String));
            const rootPaths = allFolders
                .filter((item) => rootIds.has(String(optionId(item))))
                .map((item) => String(get(item, 'fullPath', 'FullPath') || get(item, 'name', 'Name') || ''))
                .filter(Boolean);
            const folders = allFolders
                .filter(isActiveFolder)
                .filter((item) => {
                    const baselineId = get(item, 'baselineReleaseId', 'BaselineReleaseId');
                    if (baselineIds.size) return baselineIds.has(String(baselineId));
                    if (!rootIds.size) return true;
                    const path = String(get(item, 'fullPath', 'FullPath') || get(item, 'name', 'Name') || '');
                    return rootIds.has(String(optionId(item))) || rootPaths.some((rootPath) => path === rootPath || path.startsWith(`${rootPath}/`));
                })
                .sort((a, b) =>
                    Number(get(a, 'displayOrder', 'DisplayOrder') || 0) - Number(get(b, 'displayOrder', 'DisplayOrder') || 0)
                    || String(get(a, 'fullPath', 'FullPath') || get(a, 'name', 'Name') || '')
                        .localeCompare(String(get(b, 'fullPath', 'FullPath') || get(b, 'name', 'Name') || '')));
            folderSelect.innerHTML = '<option value=""></option>';
            folders.forEach((item) => {
                const id = optionId(item);
                if (!id) return;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = optionText(item, id);
                opt.dataset.depth = String(folderDepth(item));
                folderSelect.appendChild(opt);
            });
            setSelectDisabled(folderSelect, false);
            if (!folders.length) {
                const opt = document.createElement('option');
                opt.value = '';
                opt.textContent = L.NoStructures || '';
                folderSelect.appendChild(opt);
            }
        } catch (_) {
            setSelectDisabled(folderSelect, false);
            toast(L.ErrorOccurred || 'Error', 'error');
        } finally {
            if (window.jQuery?.fn?.select2) {
                window.jQuery(folderSelect).trigger('change.select2');
            }
        }
    };

    const initLookups = async () => {
        initSelect2();
        const handleCompanyChange = (value) => {
            loadFoldersForCompany(value);
        };
        if (window.jQuery?.fn?.select2) {
            window.jQuery(companySelect)
                .off('change.controlled-documents')
                .on('change.controlled-documents', function () {
                    handleCompanyChange(this.value);
                });
        } else {
            companySelect?.addEventListener('change', (event) => {
                handleCompanyChange(event.target.value);
            });
        }
        await loadLegalEntities();
        if (window.jQuery?.fn?.select2) {
            window.jQuery(companySelect).trigger('change');
        }
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
