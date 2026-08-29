/**
 * MOD-0029-FU37B — scope-aware controlled-document registration.
 * Browser transport remains multipart FormData; file bytes never become base64 or browser-persisted state.
 */
'use strict';

(function () {
    const form = document.getElementById('controlledDocumentRegistrationForm');
    if (!form) return;

    const config = window.ControlledDocumentRegistrationConfig || {};
    const l10nNode = document.getElementById('controlledDocumentRegistrationL10n');
    const L = l10nNode ? JSON.parse(l10nNode.textContent || '{}') : {};
    const endpoint = '/DocumentManagement/MasterRegister/api/controlled-document-registrations';
    const completedStatus = 'COMPLETED';
    const retryableStatuses = new Set(['CONTENTSTORED', 'DOCUMENTCREATED', 'REGISTERCREATED', 'LINKED', 'COMPENSATIONPENDING', 'FAILED']);
    const idempotencyKey = window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    let operationId = null;
    let busy = false;
    let governedLookupsReady = false;
    let companyNodes = [];
    let corporateNodes = [];

    const el = (id) => document.getElementById(id);
    const value = (id) => el(id)?.value?.trim() || '';
    const optional = (id) => value(id) || null;
    const currentScope = () => value('registrationDocumentScope') || 'Company';
    const isCorporate = () => currentScope() === 'Corporate';
    const token = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const unwrap = (payload) => payload?.data ?? payload?.Data ?? payload;
    const operationFrom = (payload) => {
        const data = unwrap(payload);
        return data?.operation ?? data?.Operation ?? data;
    };
    const pick = (item, ...keys) => keys.map((key) => item?.[key]).find((candidate) => candidate !== undefined && candidate !== null);
    const upper = (input) => String(input || '').replace(/[^a-z0-9]/gi, '').toUpperCase();
    const toast = (message, kind) => window.showToast?.(message, kind);

    const unwrapList = (payload) => {
        const data = unwrap(payload);
        if (Array.isArray(data)) return data;
        return data?.items ?? data?.Items ?? data?.results ?? data?.Results ?? [];
    };

    const requestJson = async (url, options) => {
        const response = await fetch(url, { credentials: 'same-origin', ...options });
        const json = await response.json().catch(() => ({}));
        if (!response.ok || json?.isSuccessful === false || json?.IsSuccessful === false) {
            const error = new Error(
                pick(json, 'failureDetail', 'FailureDetail', 'message', 'Message', 'reason_code', 'reasonCode')
                || json?.errors?.[0]
                || L.Error);
            error.payload = json;
            error.status = response.status;
            throw error;
        }
        return json;
    };

    const resetSelect = (select, placeholder, disabled = true) => {
        if (!select) return;
        select.replaceChildren(new Option(placeholder || L.SelectOption || '', ''));
        select.disabled = disabled;
        if (window.jQuery?.fn?.select2 && window.jQuery(select).hasClass('select2-hidden-accessible')) {
            window.jQuery(select).val(null).trigger('change.select2');
        }
    };

    const addOptions = (select, items, getId, getText, placeholder) => {
        if (!select) return;
        select.replaceChildren(new Option(placeholder || L.SelectOption || '', ''));
        items.forEach((item) => {
            const id = getId(item);
            if (!id) return;
            select.add(new Option(getText(item) || id, id));
        });
        select.disabled = false;
    };

    const initSelect2 = () => {
        if (!window.jQuery?.fn?.select2) return;
        [
            'registrationDocumentScope', 'registrationLanguage', 'registrationRetentionClass',
            'registrationOwnerFunction', 'registrationOwnerCompany', 'registrationCorporateOwner',
            'registrationProcessUser', 'registrationAuthorUser', 'registrationCompany', 'registrationCollectionInstance', 'registrationFolder',
            'registrationParent', 'registrationVariantType', 'registrationCountry', 'registrationSite'
        ].forEach((id) => {
            const select = el(id);
            if (!select || window.jQuery(select).hasClass('select2-hidden-accessible')) return;
            window.jQuery(select).select2({
                width: '100%',
                placeholder: select.options[0]?.text || L.SelectOption || '',
                allowClear: id !== 'registrationDocumentScope'
            });
        });
    };

    const nodeId = (item) => pick(item, 'collectionInstanceId', 'CollectionInstanceId', 'id', 'Id');
    const nodeName = (item) => pick(item, 'fullPath', 'FullPath', 'displayName', 'DisplayName', 'name', 'Name');
    const nodeOwner = (item) => pick(item, 'corporateOwnerId', 'CorporateOwnerId', 'scopeOwnerId', 'ScopeOwnerId');
    const nodeBaseline = (item) => pick(item, 'baselineReleaseId', 'BaselineReleaseId');
    const nodeParent = (item) => pick(item, 'parentCanonicalId', 'ParentCanonicalId');
    const nodePath = (item) => pick(item, 'fullPath', 'FullPath') || '';

    const rootsFrom = (items) => items.filter((item) => !nodeParent(item));
    const branchFrom = (items, root) => {
        const prefix = `${nodePath(root)}/`;
        return items.filter((item) =>
            nodeId(item) === nodeId(root)
            || nodePath(item) === nodePath(root)
            || nodePath(item).startsWith(prefix));
    };

    const loadGovernedLookups = async () => {
        const language = el('registrationLanguage');
        const retention = el('registrationRetentionClass');
        const retentionSet = retention?.dataset?.referenceSet || '';
        governedLookupsReady = false;
        resetSelect(language, L.SelectOption, true);
        resetSelect(retention, L.SelectOption, true);
        try {
            const [languagePayload, retentionPayload] = await Promise.all([
                requestJson('/DocumentManagement/MasterRegister/api/governed-languages'),
                requestJson(`/DocumentManagement/MasterRegister/api/reference-data/${encodeURIComponent(retentionSet)}`)
            ]);
            addOptions(
                language,
                unwrapList(languagePayload),
                (item) => pick(item, 'value', 'Value', 'code', 'Code'),
                (item) => pick(item, 'name', 'Name', 'label', 'Label', 'value', 'Value'));
            addOptions(
                retention,
                unwrapList(retentionPayload),
                (item) => pick(item, 'code', 'Code', 'valueCode', 'ValueCode', 'value', 'Value', 'id', 'Id'),
                (item) => pick(item, 'displayName', 'DisplayName', 'label', 'Label', 'name', 'Name', 'code', 'Code'));
            governedLookupsReady = language.options.length > 1 && retention.options.length > 1;
            if (!governedLookupsReady) throw new Error(L.GovernedLookupUnavailable);
        } catch (error) {
            resetSelect(language, L.GovernedLookupUnavailable, true);
            resetSelect(retention, L.GovernedLookupUnavailable, true);
            toast(L.GovernedLookupUnavailable || error.message || L.Error, 'error');
        } finally {
            setBusy(false);
            initSelect2();
        }
    };

    const loadSharedLookups = async () => {
        const ownerCompany = el('registrationOwnerCompany');
        const company = el('registrationCompany');
        const processUser = el('registrationProcessUser');
        const authorUser = el('registrationAuthorUser');
        const ownerFunction = el('registrationOwnerFunction');
        const [companiesResult, usersResult, functionsResult] = await Promise.allSettled([
            requestJson('/DocumentManagement/MasterRegister/api/legal-entities'),
            requestJson('/DocumentManagement/MasterRegister/api/users'),
            requestJson(`/DocumentManagement/MasterRegister/api/reference-data/${encodeURIComponent(ownerFunction?.dataset?.referenceSet || '')}`)
        ]);

        if (companiesResult.status === 'fulfilled') {
            const companies = unwrapList(companiesResult.value);
            const companyId = (item) => pick(item, 'legalEntityId', 'LegalEntityId', 'id', 'Id');
            const companyName = (item) => pick(item, 'legalName', 'LegalName', 'displayName', 'DisplayName', 'name', 'Name');
            addOptions(ownerCompany, companies, companyId, companyName);
            addOptions(company, companies, companyId, companyName);
        }
        if (usersResult.status === 'fulfilled') {
            addOptions(
                processUser,
                unwrapList(usersResult.value),
                (item) => pick(item, 'id', 'Id', 'userId', 'UserId'),
                (item) => pick(item, 'fullName', 'FullName', 'displayName', 'DisplayName', 'email', 'Email'));
            addOptions(
                authorUser,
                unwrapList(usersResult.value),
                (item) => pick(item, 'id', 'Id', 'userId', 'UserId'),
                (item) => pick(item, 'fullName', 'FullName', 'displayName', 'DisplayName', 'email', 'Email'));
        }
        if (functionsResult.status === 'fulfilled') {
            addOptions(
                ownerFunction,
                unwrapList(functionsResult.value),
                (item) => pick(item, 'code', 'Code', 'value', 'Value', 'id', 'Id'),
                (item) => pick(item, 'displayName', 'DisplayName', 'label', 'Label', 'name', 'Name', 'code', 'Code'));
        }
        initSelect2();
    };

    const loadCompanyInstances = async (companyId) => {
        const instance = el('registrationCollectionInstance');
        resetSelect(instance, L.SelectCompanyCollectionInstance, true);
        resetSelect(el('registrationFolder'), L.SelectCompanyFolder, true);
        companyNodes = [];
        if (!companyId) return;
        try {
            companyNodes = unwrapList(await requestJson(
                `/DocumentManagement/MasterRegister/api/collection-instances?companyId=${encodeURIComponent(companyId)}`));
            const roots = rootsFrom(companyNodes);
            addOptions(instance, roots.length ? roots : companyNodes, nodeId, nodeName, L.SelectCompanyCollectionInstance);
        } catch (error) {
            toast(error.status === 403 ? L.CompanyAccessDenied : L.ScopeMismatch, 'error');
        }
    };

    const loadCorporateNodes = async () => {
        const ownerSelect = el('registrationCorporateOwner');
        resetSelect(ownerSelect, L.SelectCorporateOwner, true);
        resetSelect(el('registrationCollectionInstance'), L.SelectCorporateCollectionInstance, true);
        resetSelect(el('registrationFolder'), L.SelectCorporateFolder, true);
        el('registrationCorporateInstanceRequired')?.classList.add('d-none');
        corporateNodes = [];
        try {
            corporateNodes = unwrapList(await requestJson(
                '/DocumentManagement/MasterRegister/api/corporate-collection-instances'));
            const owners = new Map();
            corporateNodes.forEach((item) => {
                const ownerId = nodeOwner(item);
                if (!ownerId || owners.has(ownerId)) return;
                const root = rootsFrom(corporateNodes.filter((candidate) => nodeOwner(candidate) === ownerId))[0];
                owners.set(ownerId, root ? `${nodeName(root)} · ${ownerId}` : ownerId);
            });
            addOptions(
                ownerSelect,
                [...owners].map(([id, name]) => ({ id, name })),
                (item) => item.id,
                (item) => item.name,
                L.SelectCorporateOwner);
            if (!owners.size) {
                resetSelect(ownerSelect, L.NoCorporateCollectionInstancesAvailable, true);
                el('registrationCorporateInstanceRequired')?.classList.remove('d-none');
            }
        } catch (error) {
            el('registrationCorporateInstanceRequired')?.classList.remove('d-none');
            toast(error.status === 403 ? L.CorporateAccessDenied : L.ScopeMismatch, 'error');
        }
    };

    const loadCorporateInstances = (ownerId) => {
        const select = el('registrationCollectionInstance');
        resetSelect(select, L.SelectCorporateCollectionInstance, true);
        resetSelect(el('registrationFolder'), L.SelectCorporateFolder, true);
        if (!ownerId) return;
        const ownerNodes = corporateNodes.filter((item) => nodeOwner(item) === ownerId);
        const roots = rootsFrom(ownerNodes);
        addOptions(select, roots.length ? roots : ownerNodes, nodeId, nodeName, L.SelectCorporateCollectionInstance);
        el('registrationCorporateInstanceRequired')?.classList.toggle('d-none', select.options.length > 1);
    };

    const loadFoldersForInstance = (instanceId) => {
        const folder = el('registrationFolder');
        const nodes = isCorporate() ? corporateNodes : companyNodes;
        const root = nodes.find((item) => String(nodeId(item)) === instanceId);
        resetSelect(folder, isCorporate() ? L.SelectCorporateFolder : L.SelectCompanyFolder, true);
        if (!root) return;
        addOptions(
            folder,
            branchFrom(nodes, root),
            nodeId,
            nodeName,
            isCorporate() ? L.SelectCorporateFolder : L.SelectCompanyFolder);
    };

    const setConditionalField = (node, active) => {
        node.classList.toggle('d-none', !active);
        node.querySelectorAll('input,select,textarea').forEach((input) => {
            input.disabled = !active;
            if (!active && input.id !== 'registrationDocumentScope') input.value = '';
        });
    };

    const bindSelectChange = (id, handler) => {
        const select = el(id);
        if (!select) return;
        if (window.jQuery) {
            window.jQuery(select)
                .off('change.controlled-document-registration')
                .on('change.controlled-document-registration', function () {
                    handler(this.value);
                });
            return;
        }
        select.addEventListener('change', (event) => handler(event.target.value));
    };

    const applyScope = async () => {
        const corporate = isCorporate();
        document.querySelectorAll('[data-company-field]').forEach((node) => setConditionalField(node, !corporate));
        document.querySelectorAll('[data-corporate-field]').forEach((node) => setConditionalField(node, corporate));
        resetSelect(el('registrationCollectionInstance'), corporate ? L.SelectCorporateCollectionInstance : L.SelectCompanyCollectionInstance, true);
        resetSelect(el('registrationFolder'), corporate ? L.SelectCorporateFolder : L.SelectCompanyFolder, true);
        el('registrationCollectionInstanceLabel').innerHTML = `${corporate ? L.CorporateCollectionInstance : L.CompanyCollectionInstance} <span class="text-danger">*</span>`;
        el('registrationFolderLabel').innerHTML = `${corporate ? L.CorporateFolder : L.CompanyFolder} <span class="text-danger">*</span>`;
        el('registrationCorporateInstanceRequired')?.classList.add('d-none');
        if (corporate) await loadCorporateNodes();
        initSelect2();
    };

    function setBusy(nextBusy) {
        busy = nextBusy;
        const submit = el('registrationSubmit');
        if (submit) {
            submit.disabled = busy || !config.canCreate || !governedLookupsReady;
            submit.querySelector('.spinner-border')?.classList.toggle('d-none', !busy);
            submit.querySelector('.bx-file')?.classList.toggle('d-none', busy);
        }
        el('registrationRetry')?.toggleAttribute('disabled', busy);
    }

    const showOperation = (operation) => {
        if (!operation) return false;
        operationId = pick(operation, 'operationId', 'OperationId') || operationId;
        const status = pick(operation, 'status', 'Status') || '-';
        const normalized = upper(status);
        const correlationId = pick(operation, 'correlationId', 'CorrelationId');
        const failure = pick(operation, 'failureDetail', 'FailureDetail', 'failureReasonCode', 'FailureReasonCode');
        el('registrationOperationCard')?.classList.remove('d-none');
        const badge = el('registrationStatus');
        if (badge) {
            badge.textContent = status;
            badge.className = `badge ${normalized === completedStatus ? 'bg-label-success' : normalized === 'FAILED' ? 'bg-label-danger' : 'bg-label-warning'}`;
        }
        el('registrationOperationMeta').textContent = [
            operationId ? `${L.OperationId}: ${operationId}` : '',
            correlationId ? `${L.CorrelationId}: ${correlationId}` : ''
        ].filter(Boolean).join(' · ');
        const failureNode = el('registrationFailure');
        if (failureNode) {
            failureNode.textContent = failure || '';
            failureNode.classList.toggle('d-none', !failure);
        }
        el('registrationRetry')?.classList.toggle(
            'd-none',
            !(config.canRetry && operationId && retryableStatuses.has(normalized)));
        if (operationId) el('registrationDocumentScope').disabled = true;
        return normalized === completedStatus;
    };

    const isRecord = () => value('registrationKind') === 'Record';
    const isVariant = () => value('registrationKind') === 'Variant';

    const buildPayload = () => {
        const corporate = isCorporate();
        const record = isRecord();
        const variant = isVariant();
        const folderId = value('registrationFolder');
        const payload = {
            kind: value('registrationKind') || 'ControlledDocument',
            // Manual code applies to records only; controlled documents are engine-allocated so it is left null.
            recordCode: record ? (value('registrationRecordCode').trim() || null) : null,
            documentScope: currentScope(),
            idempotencyKey,
            documentTitle: value('registrationTitle'),
            documentClass: value('registrationClass'),
            criticality: value('registrationCriticality'),
            documentType: value('registrationType'),
            description: optional('registrationDescription'),
            tags: value('registrationTags').split(',').map((tag) => tag.trim()).filter(Boolean),
            governingLanguage: value('registrationLanguage'),
            governingLanguageId: value('registrationLanguage'),
            ownerFunction: optional('registrationOwnerFunction'),
            processOwnerRole: optional('registrationProcessRole'),
            processOwnerUserId: optional('registrationProcessUser'),
            authorUserId: optional('registrationAuthorUser'),
            // A record has no review cycle (no periodic-review lifecycle applies to it).
            reviewCycleMonths: (!record && value('registrationReviewCycle')) ? Number(value('registrationReviewCycle')) : null,
            retentionClass: value('registrationRetentionClass'),
            retentionClassId: value('registrationRetentionClass'),
            // Current MOD-0028 runtime represents every folder as a CollectionInstance node.
            collectionInstanceId: folderId,
            folderId
        };
        if (corporate) {
            payload.corporateOwnerId = value('registrationCorporateOwner');
        } else {
            payload.companyId = value('registrationCompany');
            payload.ownerCompanyId = value('registrationOwnerCompany');
        }
        if (variant) {
            payload.parentRegisterEntryId = value('registrationParent') || null;
            payload.variantType = value('registrationVariantType') || 'Translation';
            // The variant's language is its governing language (already selected above).
            payload.languageCode = value('registrationLanguage') || null;
            payload.countryCode = value('registrationCountry').trim() || null;
            payload.siteCode = value('registrationSite').trim() || null;
        }
        return payload;
    };

    const displayRequestError = (error) => {
        // 409 is overloaded (idempotency scope conflict vs duplicate title) — disambiguate on the reason code.
        const reason = upper(pick(error.payload || {}, 'reason_code', 'reasonCode'));
        if (reason === 'DUPLICATEDOCUMENTTITLE') return L.DuplicateDocumentTitle || error.message || L.Error;
        if (reason === 'DUPLICATERECORDCODE') return L.DuplicateRecordCode || error.message || L.Error;
        if (reason === 'VARIANTCONTENTUNCHANGED') return L.VariantContentUnchanged || error.message || L.Error;
        if (reason === 'VARIANTPARENTNOTFOUND') return L.VariantParentNotFound || error.message || L.Error;
        if (error.status === 409) return L.IdempotencyScopeConflict;
        if (error.status === 403) return isCorporate() ? L.CorporateAccessDenied : L.CompanyAccessDenied;
        if (error.status === 404) return L.ScopeMismatch;
        return error.message || L.Error;
    };

    const completeOrWarn = (operation, retryStarted) => {
        if (showOperation(operation)) {
            toast(L.RegistrationCompleted, 'success');
            const masterRegisterId = pick(operation, 'masterRegisterEntryId', 'MasterRegisterEntryId');
            if (masterRegisterId) {
                setTimeout(() => {
                    window.location.href = `/DocumentManagementMasterRegister/Details/${encodeURIComponent(masterRegisterId)}`;
                }, 700);
            }
            return;
        }
        toast(retryStarted ? L.RegistrationRetryStarted : L.RegistrationIncomplete, 'warning');
    };

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        form.classList.add('was-validated');
        if (!config.canCreate) {
            el('registrationPermissionAlert')?.classList.remove('d-none');
            return;
        }
        if (!governedLookupsReady) {
            toast(L.GovernedLookupUnavailable, 'error');
            return;
        }
        if (!form.checkValidity()) return;
        const file = el('registrationFile')?.files?.[0];
        if (!file) return;

        const body = new FormData();
        body.append('initialFile', file);
        body.append('payloadJson', JSON.stringify(buildPayload()));
        body.append('__RequestVerificationToken', token());
        setBusy(true);
        try {
            completeOrWarn(operationFrom(await requestJson(endpoint, { method: 'POST', body })), false);
        } catch (error) {
            showOperation(operationFrom(error.payload));
            toast(displayRequestError(error), 'error');
        } finally {
            setBusy(false);
        }
    });

    el('registrationRetry')?.addEventListener('click', async () => {
        if (!config.canRetry || !operationId) return;
        const body = new FormData();
        body.append('__RequestVerificationToken', token());
        setBusy(true);
        try {
            completeOrWarn(
                operationFrom(await requestJson(`${endpoint}/${encodeURIComponent(operationId)}/retry`, { method: 'POST', body })),
                true);
        } catch (error) {
            showOperation(operationFrom(error.payload));
            toast(displayRequestError(error), 'error');
        } finally {
            setBusy(false);
        }
    });

    // Variant parent picker: list controlled documents (records/variants excluded) so one can be chosen as the parent.
    const loadParents = async () => {
        const select = el('registrationParent');
        if (!select || select.dataset.loaded === '1') return;
        try {
            const payload = await requestJson('/DocumentManagement/MasterRegister/api/list');
            const items = unwrapList(payload).filter((r) => (pick(r, 'documentKind', 'DocumentKind')) === 'ControlledDocument');
            addOptions(
                select, items,
                (i) => pick(i, 'id', 'Id'),
                (i) => pick(i, 'documentTitle', 'DocumentTitle'));
            select.dataset.loaded = '1';
            initSelect2();
        } catch (error) {
            toast(L.GovernedLookupUnavailable || error.message || L.Error, 'error');
        }
    };

    // Country / Site are governed single-selects fed from business reference data (set code from data-reference-set).
    const loadReferenceSelect = async (id) => {
        const select = el(id);
        const setCode = select?.dataset?.referenceSet;
        if (!select || !setCode || select.dataset.loaded === '1') return;
        try {
            const payload = await requestJson(`/DocumentManagement/MasterRegister/api/reference-data/${encodeURIComponent(setCode)}`);
            addOptions(
                select, unwrapList(payload),
                (i) => pick(i, 'code', 'Code', 'value', 'Value', 'id', 'Id'),
                (i) => pick(i, 'displayName', 'DisplayName', 'label', 'Label', 'name', 'Name', 'code', 'Code'));
            select.dataset.loaded = '1';
            initSelect2();
        } catch {
            // Empty / unpublished set → the select simply keeps only its placeholder.
        }
    };

    // Parent preview — a file-style card with the parent's metadata plus View (inline) and Download actions.
    const showParentPreview = async () => {
        const id = value('registrationParent');
        const box = el('registrationParentPreview');
        if (!box) return;
        const view = el('parentPreviewView');
        const dl = el('parentPreviewDownload');
        view?.classList.add('d-none');
        dl?.classList.add('d-none');
        if (!id) { box.classList.add('d-none'); return; }
        try {
            const d = unwrap(await requestJson(`/DocumentManagement/MasterRegister/api/detail/${encodeURIComponent(id)}`)) || {};
            el('parentPreviewTitle').textContent = pick(d, 'documentTitle', 'DocumentTitle') || '';
            const ver = pick(d, 'currentVersionNumber', 'CurrentVersionNumber');
            el('parentPreviewMeta').textContent = [
                pick(d, 'documentClass', 'DocumentClass'),
                pick(d, 'documentType', 'DocumentType'),
                ver ? `v${ver}` : ''
            ].filter(Boolean).join(' · ');
            el('parentPreviewFolder').textContent = pick(d, 'governingLanguage', 'GoverningLanguage') || '';
            box.classList.remove('d-none');

            // Resolve the parent's current file version so it can be viewed / downloaded.
            const docRef = pick(d, 'controlledDocumentId', 'ControlledDocumentId');
            if (docRef && view && dl) {
                const cd = unwrap(await requestJson(`/DocumentManagementControlledDocuments/detail/${encodeURIComponent(docRef)}`)) || {};
                const versionRef = pick(cd, 'currentVersionId', 'CurrentVersionId');
                if (versionRef) {
                    view.href = `/DocumentManagementControlledDocuments/preview/${docRef}/${versionRef}`;
                    dl.href = `/DocumentManagementControlledDocuments/download/${docRef}/${versionRef}`;
                    view.classList.remove('d-none');
                    dl.classList.remove('d-none');
                }
            }
        } catch {
            box.classList.add('d-none');
        }
    };

    // Record has no periodic-review lifecycle — hide the review-cycle field when the record kind is chosen.
    // Records may carry an optional manual code (controlled documents are engine-allocated), so reveal that field.
    // Variant reveals the parent/locale section, requires a parent, and loads the governed locale lookups.
    const applyKind = () => {
        const record = isRecord();
        const variant = isVariant();
        el('registrationReviewCycleField')?.classList.toggle('d-none', record);
        el('registrationRecordCodeField')?.classList.toggle('d-none', !record);
        el('registrationVariantSection')?.classList.toggle('d-none', !variant);
        const parent = el('registrationParent');
        if (parent) parent.required = variant;
        if (variant) {
            void loadParents();
            void loadReferenceSelect('registrationCountry');
            void loadReferenceSelect('registrationSite');
        }
    };

    bindSelectChange('registrationDocumentScope', applyScope);
    bindSelectChange('registrationKind', applyKind);
    bindSelectChange('registrationParent', showParentPreview);
    bindSelectChange('registrationCompany', loadCompanyInstances);
    bindSelectChange('registrationCorporateOwner', loadCorporateInstances);
    bindSelectChange('registrationCollectionInstance', loadFoldersForInstance);

    if (!config.canCreate) el('registrationPermissionAlert')?.classList.remove('d-none');
    setBusy(false);
    applyKind();
    applyScope()
        .then(() => Promise.all([loadSharedLookups(), loadGovernedLookups()]))
        .catch(() => toast(L.Error, 'error'));
})();
