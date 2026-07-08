'use strict';

(function () {
    const page = document.getElementById('hcm-employee-draft-page');
    if (!page) return;

    const apiBase = page.getAttribute('data-api-base') || '/HCM/Employees/drafts/api';
    const legalEntityApiBase = '/HCM/Employees/reference-api/legal-entities';
    const canCreateDraft = page.getAttribute('data-can-create-draft') === 'true';
    const L = window.L10n || {};
    const state = {
        draftSessionId: '',
        version: null,
        etag: '',
        reviewState: 'not_reviewed',
        referenceSummary: null
    };

    const byId = (id) => document.getElementById(id);
    const controls = {
        start: byId('hcm-start-draft'),
        reload: byId('hcm-reload-draft'),
        save: byId('hcm-save-draft'),
        validate: byId('hcm-validate-references'),
        review: byId('hcm-review-draft'),
        error: byId('hcm-error-state'),
        success: byId('hcm-success-state'),
        permission: byId('hcm-permission-state'),
        referenceResults: byId('hcm-reference-results'),
        reviewBlockers: byId('hcm-review-blockers'),
        legalEntityStatus: byId('hcm-legal-entity-status')
    };

    const fields = {
        personId: byId('hcm-person-id'),
        legalName: byId('hcm-legal-name'),
        sensitivityLevel: byId('hcm-sensitivity-level'),
        workerType: byId('hcm-worker-type'),
        employmentType: byId('hcm-employment-type'),
        hireDate: byId('hcm-hire-date'),
        organizationUnitId: byId('hcm-organization-unit-id'),
        positionId: byId('hcm-position-id'),
        legalEntityId: byId('hcm-legal-entity-id'),
        legalEntityPicker: byId('hcm-legal-entity-picker'),
        legalEntityOptions: byId('hcm-legal-entity-options')
    };
    let legalEntitySearchTimer = null;

    const asString = (value) => value === null || value === undefined ? '' : String(value).trim();
    const randomId = (prefix) => {
        if (window.crypto?.randomUUID) return `${prefix}-${window.crypto.randomUUID()}`;
        return `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
    };
    const isGuid = (value) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(asString(value));

    const unwrap = (payload) => {
        if (payload?.data !== undefined) return payload.data;
        if (payload?.Data !== undefined) return payload.Data;
        return payload;
    };
    const firstValue = (source, keys, fallback) => {
        if (!source) return fallback;
        for (const key of keys) {
            if (source[key] !== undefined && source[key] !== null) return source[key];
        }
        return fallback;
    };
    const text = (key, fallback) => {
        const value = L[key];
        return value && value !== key ? value : fallback;
    };

    const showState = (element, message) => {
        if (!element) return;
        element.textContent = message || '';
        element.classList.toggle('d-none', !message);
    };

    const clearMessages = () => {
        showState(controls.error, '');
        showState(controls.success, '');
    };

    const setBusy = (isBusy) => {
        page.setAttribute('aria-busy', isBusy ? 'true' : 'false');
        [controls.start, controls.reload, controls.save, controls.validate, controls.review]
            .filter(Boolean)
            .forEach((button) => {
                if (button.dataset.locked === 'true') return;
                button.disabled = isBusy || button.dataset.enabled !== 'true';
            });
    };

    const setEnabled = (button, enabled) => {
        if (!button) return;
        button.dataset.enabled = enabled ? 'true' : 'false';
        button.disabled = !enabled;
    };

    const updateButtons = () => {
        const hasDraft = Boolean(state.draftSessionId);
        const allowed = canCreateDraft;
        setEnabled(controls.start, allowed);
        setEnabled(controls.reload, allowed && hasDraft);
        setEnabled(controls.save, allowed && hasDraft);
        setEnabled(controls.validate, allowed && hasDraft);
        setEnabled(controls.review, allowed && hasDraft);
    };

    const updateStatePanel = () => {
        byId('hcm-state-session').textContent = state.draftSessionId || text('NotStarted', 'Not started');
        byId('hcm-state-version').textContent = state.version === null || state.version === undefined ? '-' : String(state.version);
        byId('hcm-state-etag').textContent = state.etag || '-';
        byId('hcm-state-review').textContent = state.reviewState || 'not_reviewed';
        updateButtons();
    };

    const classifyStatus = (status) => {
        if (status === 401) return text('AuthenticationRequired', 'Authentication is required.');
        if (status === 403) return text('PermissionDenied', 'Permission denied.');
        if (status === 404) return text('DraftNotFound', 'Draft or reference was not found.');
        if (status === 409) return text('DraftChangedReload', 'The draft has changed. Reload the draft and try again.');
        if (status === 422 || status === 400) return text('ValidationFailed', 'Validation failed.');
        if (status === 502 || status === 503 || status === 504) return text('DependencyUnavailable', 'A dependency is unavailable.');
        return text('RequestFailed', 'Request failed.');
    };

    const extractErrorMessage = (payload, fallback) => {
        if (!payload) return fallback;
        if (Array.isArray(payload.errors) && payload.errors.length) return payload.errors.join('; ');
        if (payload.errors && typeof payload.errors === 'object') {
            const messages = Object.values(payload.errors).flat().filter((item) => typeof item === 'string' && item.trim());
            if (messages.length) return messages.join('; ');
        }
        if (Array.isArray(payload.Errors) && payload.Errors.length) return payload.Errors.join('; ');
        return payload.message || payload.Message || payload.detail || payload.Detail || payload.title || payload.Title || fallback;
    };

    const requestJson = async (path, options) => {
        const init = Object.assign({
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                Accept: 'application/json',
                'X-Correlation-Id': randomId('hcm-draft')
            }
        }, options || {});

        if (init.body && !init.headers['Content-Type']) {
            init.headers['Content-Type'] = 'application/json';
        }

        const response = await fetch(`${apiBase}${path}`, init);
        const text = await response.text();
        let payload = null;
        if (text) {
            try {
                payload = JSON.parse(text);
            } catch (_error) {
                payload = { message: text };
            }
        }

        if (!response.ok) {
            const error = new Error(extractErrorMessage(payload, classifyStatus(response.status)));
            error.status = response.status;
            error.payload = payload;
            throw error;
        }

        return payload;
    };

    const requestLegalEntityJson = async (legalEntityId) => {
        const response = await fetch(`${legalEntityApiBase}/${encodeURIComponent(legalEntityId)}/lookup-validation`, {
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                Accept: 'application/json',
                'X-Correlation-Id': randomId('hcm-legal-entity')
            }
        });
        const responseText = await response.text();
        let payload = null;
        if (responseText) {
            try {
                payload = JSON.parse(responseText);
            } catch (_error) {
                payload = { message: responseText };
            }
        }

        if (!response.ok) {
            const error = new Error(extractErrorMessage(payload, classifyStatus(response.status)));
            error.status = response.status;
            error.payload = payload;
            throw error;
        }

        return payload;
    };

    const requestLegalEntitySearchJson = async (query) => {
        const params = new URLSearchParams({
            query: asString(query),
            referenceable: 'true',
            page: '1',
            pageSize: '20'
        });
        const response = await fetch(`${legalEntityApiBase}?${params.toString()}`, {
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                Accept: 'application/json',
                'X-Correlation-Id': randomId('hcm-legal-entity')
            }
        });
        const responseText = await response.text();
        let payload = null;
        if (responseText) {
            try {
                payload = JSON.parse(responseText);
            } catch (_error) {
                payload = { message: responseText };
            }
        }

        if (!response.ok) {
            const error = new Error(extractErrorMessage(payload, classifyStatus(response.status)));
            error.status = response.status;
            error.payload = payload;
            throw error;
        }

        return payload;
    };

    const normalizeLegalEntity = (payload) => {
        const data = unwrap(payload) || {};
        const legalEntityId = asString(firstValue(data, ['legalEntityId', 'LegalEntityId'], ''));
        const code = asString(firstValue(data, ['code', 'Code'], ''));
        const displayName = asString(firstValue(data, ['displayName', 'DisplayName'], ''));
        const legalName = asString(firstValue(data, ['legalName', 'LegalName'], ''));
        const lifecycleState = asString(firstValue(data, ['lifecycleState', 'LifecycleState'], ''));
        const referenceable = data.referenceable === true || data.Referenceable === true;
        if (!legalEntityId) return null;
        return {
            legalEntityId,
            code,
            displayName,
            legalName,
            lifecycleState,
            referenceable
        };
    };

    const normalizeLegalEntitySearch = (payload) => {
        const data = unwrap(payload) || {};
        const items = Array.isArray(data.items) ? data.items : Array.isArray(data.Items) ? data.Items : [];
        return items.map((item) => normalizeLegalEntity(item)).filter(Boolean);
    };

    const formatLegalEntityText = (entity) => {
        const name = entity.displayName || entity.legalName || entity.legalEntityId;
        return entity.code ? `${name} (${entity.code})` : name;
    };

    const setLegalEntityStatus = (message, state) => {
        if (!controls.legalEntityStatus) return;
        controls.legalEntityStatus.textContent = message || '';
        controls.legalEntityStatus.classList.toggle('text-danger', state === 'error');
        controls.legalEntityStatus.classList.toggle('text-success', state === 'success');
        controls.legalEntityStatus.classList.toggle('text-muted', state !== 'error' && state !== 'success');
    };

    const validateLegalEntityField = async () => {
        const id = asString(fields.legalEntityId?.value);
        if (!fields.legalEntityId || !fields.legalEntityPicker || !id) {
            setLegalEntityStatus('', 'empty');
            return null;
        }

        if (!isGuid(id)) {
            setLegalEntityStatus(text('LegalEntityInvalidId', 'Enter a valid LegalEntityId to resolve it from Legal Entity.'), 'error');
            return null;
        }

        setLegalEntityStatus(text('LegalEntityResolving', 'Resolving Legal Entity reference...'), 'loading');
        fields.legalEntityId.setAttribute('aria-busy', 'true');
        try {
            const entity = normalizeLegalEntity(await requestLegalEntityJson(id));
            if (!entity || !entity.referenceable) {
                setLegalEntityStatus(text('LegalEntityNotReferenceable', 'Legal Entity is not active/referenceable.'), 'error');
                return null;
            }

            const name = formatLegalEntityText(entity);
            const lifecycle = entity.lifecycleState ? ` (${entity.lifecycleState})` : '';
            fields.legalEntityPicker.value = name;
            fields.legalEntityPicker.dataset.selectedId = id;
            fields.legalEntityPicker.dataset.selectedLabel = name;
            setLegalEntityStatus(`${text('LegalEntityLinked', 'Linked Legal Entity')}: ${name}${lifecycle}`, 'success');
            return entity;
        } catch (error) {
            setLegalEntityStatus(error.message || text('LegalEntityLookupFailed', 'Legal Entity could not be resolved.'), 'error');
            return null;
        } finally {
            fields.legalEntityId.removeAttribute('aria-busy');
        }
    };

    const renderLegalEntityOptions = (items) => {
        if (!fields.legalEntityOptions) return;
        fields.legalEntityOptions.innerHTML = items.map((entity) => {
            const label = formatLegalEntityText(entity);
            return `<option value="${escapeHtml(label)}" data-id="${escapeHtml(entity.legalEntityId)}"></option>`;
        }).join('');
    };

    const searchLegalEntities = async () => {
        const query = asString(fields.legalEntityPicker?.value);
        if (!fields.legalEntityPicker || query.length < 2) {
            renderLegalEntityOptions([]);
            if (fields.legalEntityId) fields.legalEntityId.value = '';
            setLegalEntityStatus('', 'empty');
            return;
        }

        if (isGuid(query)) {
            fields.legalEntityId.value = query;
            await validateLegalEntityField();
            return;
        }

        setLegalEntityStatus(text('LegalEntitySearching', 'Searching Legal Entities...'), 'loading');
        try {
            const items = normalizeLegalEntitySearch(await requestLegalEntitySearchJson(query));
            renderLegalEntityOptions(items);
            setLegalEntityStatus(
                items.length ? text('LegalEntitySelectMatch', 'Select a Legal Entity from the list.') : text('LegalEntityNoMatches', 'No active Legal Entities matched.'),
                items.length ? 'muted' : 'error');
        } catch (error) {
            renderLegalEntityOptions([]);
            setLegalEntityStatus(error.message || text('LegalEntityLookupFailed', 'Legal Entity could not be resolved.'), 'error');
        }
    };

    const selectLegalEntityOption = async () => {
        const selectedText = asString(fields.legalEntityPicker?.value);
        const option = fields.legalEntityOptions
            ? Array.from(fields.legalEntityOptions.options).find((item) => item.value === selectedText)
            : null;

        if (!option?.dataset?.id &&
            fields.legalEntityPicker?.dataset?.selectedId &&
            selectedText === fields.legalEntityPicker.dataset.selectedLabel) {
            fields.legalEntityId.value = fields.legalEntityPicker.dataset.selectedId;
            return;
        }

        if (!option?.dataset?.id && isGuid(selectedText)) {
            fields.legalEntityId.value = selectedText;
            await validateLegalEntityField();
            return;
        }

        if (!option?.dataset?.id) {
            if (fields.legalEntityId) fields.legalEntityId.value = '';
            if (selectedText) setLegalEntityStatus(text('LegalEntitySelectMatch', 'Select a Legal Entity from the list.'), 'error');
            return;
        }

        fields.legalEntityId.value = option.dataset.id;
        await validateLegalEntityField();
    };

    const queueLegalEntitySearch = () => {
        if (legalEntitySearchTimer) window.clearTimeout(legalEntitySearchTimer);
        legalEntitySearchTimer = window.setTimeout(searchLegalEntities, 250);
    };

    const collectPayload = () => ({
        person_id: asString(fields.personId?.value),
        legal_name: asString(fields.legalName?.value),
        worker_type: asString(fields.workerType?.value),
        employment_type: asString(fields.employmentType?.value),
        hire_date: asString(fields.hireDate?.value),
        organization_unit_id: asString(fields.organizationUnitId?.value),
        position_id: asString(fields.positionId?.value),
        legal_entity_id: asString(fields.legalEntityId?.value),
        sensitivity_level: asString(fields.sensitivityLevel?.value)
    });

    const collectReferenceRequest = () => ({
        personId: asString(fields.personId?.value),
        organizationUnitId: asString(fields.organizationUnitId?.value),
        positionId: asString(fields.positionId?.value),
        legalEntityId: asString(fields.legalEntityId?.value),
        idempotencyKey: randomId('validate')
    });

    const applyDraft = (draft) => {
        const data = unwrap(draft) || {};
        state.draftSessionId = asString(firstValue(data, ['draftSessionId', 'DraftSessionId'], state.draftSessionId));
        state.version = firstValue(data, ['version', 'Version'], state.version);
        state.etag = asString(firstValue(data, ['etag', 'eTag', 'ETag'], state.etag));
        state.reviewState = asString(firstValue(data, ['reviewState', 'ReviewState'], state.reviewState || 'not_reviewed'));
        state.referenceSummary = firstValue(data, ['referenceValidationSummary', 'ReferenceValidationSummary', 'validationSummary', 'ValidationSummary'], state.referenceSummary);
        updateStatePanel();
        renderReferenceSummary(state.referenceSummary);
    };

    const startDraft = async () => {
        clearMessages();
        setBusy(true);
        try {
            const payload = {
                sourceContext: 'hcm-create-employee-draft',
                clientReference: randomId('client'),
                idempotencyKey: randomId('create')
            };
            const result = await requestJson('', {
                method: 'POST',
                body: JSON.stringify(payload)
            });
            applyDraft(result);
            showState(controls.success, text('DraftStarted', 'Draft started.'));
        } catch (error) {
            showState(controls.error, error.message);
        } finally {
            setBusy(false);
            updateButtons();
        }
    };

    const saveDraft = async () => {
        if (!state.draftSessionId) return;
        clearMessages();
        setBusy(true);
        try {
            const result = await requestJson(`/${encodeURIComponent(state.draftSessionId)}`, {
                method: 'PATCH',
                headers: {
                    Accept: 'application/json',
                    'Content-Type': 'application/json',
                    'If-Match': state.etag || '',
                    'X-Correlation-Id': randomId('hcm-draft')
                },
                body: JSON.stringify({
                    stepCode: 'employee_draft',
                    payloadSchemaVersion: 'employee-create-wizard.v1',
                    stepPayload: collectPayload(),
                    clientValidationState: {},
                    idempotencyKey: randomId('save')
                })
            });
            applyDraft(result);
            showState(controls.success, text('DraftSaved', 'Draft saved.'));
        } catch (error) {
            showState(controls.error, error.message);
        } finally {
            setBusy(false);
            updateButtons();
        }
    };

    const reloadDraft = async () => {
        if (!state.draftSessionId) return;
        clearMessages();
        setBusy(true);
        try {
            const result = await requestJson(`/${encodeURIComponent(state.draftSessionId)}`);
            applyDraft(result);
            showState(controls.success, text('DraftReloaded', 'Draft reloaded.'));
        } catch (error) {
            showState(controls.error, error.message);
        } finally {
            setBusy(false);
            updateButtons();
        }
    };

    const validateReferences = async () => {
        if (!state.draftSessionId) return;
        clearMessages();
        setBusy(true);
        try {
            const result = await requestJson(`/${encodeURIComponent(state.draftSessionId)}/validate-references`, {
                method: 'POST',
                headers: {
                    Accept: 'application/json',
                    'Content-Type': 'application/json',
                    'If-Match': state.etag || '',
                    'X-Correlation-Id': randomId('hcm-draft')
                },
                body: JSON.stringify(collectReferenceRequest())
            });
            state.referenceSummary = unwrap(result);
            renderReferenceSummary(state.referenceSummary);
            await reloadDraft();
        } catch (error) {
            showState(controls.error, error.message);
        } finally {
            setBusy(false);
            updateButtons();
        }
    };

    const reviewDraft = async () => {
        if (!state.draftSessionId) return;
        clearMessages();
        setBusy(true);
        try {
            const result = await requestJson(`/${encodeURIComponent(state.draftSessionId)}/review`, {
                method: 'POST',
                headers: {
                    Accept: 'application/json',
                    'Content-Type': 'application/json',
                    'If-Match': state.etag || '',
                    'X-Correlation-Id': randomId('hcm-draft')
                },
                body: JSON.stringify({
                    idempotencyKey: randomId('review'),
                    referenceValidationAcknowledged: true,
                    duplicateWarningAcknowledged: true,
                    etag: state.etag
                })
            });
            const data = unwrap(result) || {};
            state.reviewState = asString(firstValue(data, ['reviewState', 'ReviewState'], state.reviewState));
            state.version = firstValue(data, ['version', 'Version'], state.version);
            state.etag = asString(firstValue(data, ['etag', 'eTag', 'ETag'], state.etag));
            state.referenceSummary = firstValue(data, ['referenceValidationSummary', 'ReferenceValidationSummary'], state.referenceSummary);
            renderReviewBlockers(firstValue(data, ['blockingReasons', 'BlockingReasons'], []));
            renderReferenceSummary(state.referenceSummary);
            updateStatePanel();
            showState(
                controls.success,
                state.reviewState === 'reviewed'
                    ? text('DraftReviewed', 'Draft reviewed.')
                    : text('DraftReviewHasBlockers', 'Draft review has blockers.')
            );
        } catch (error) {
            showState(controls.error, error.message);
        } finally {
            setBusy(false);
            updateButtons();
        }
    };

    const renderReferenceSummary = (summary) => {
        if (!controls.referenceResults) return;
        const data = summary || {};
        const results = Array.isArray(data.results) ? data.results : Array.isArray(data.Results) ? data.Results : [];

        if (!results.length) {
            controls.referenceResults.innerHTML = `<div class="list-group-item text-muted">${escapeHtml(text('ReferencesNotValidated', 'References have not been validated.'))}</div>`;
            return;
        }

        controls.referenceResults.innerHTML = results.map((item) => {
            const referenceType = asString(item.referenceType || item.ReferenceType);
            const referenceId = asString(item.referenceId || item.ReferenceId);
            const status = asString(item.status || item.Status);
            const reason = asString(item.reasonCode || item.ReasonCode);
            const valid = item.isReferenceable === true || item.IsReferenceable === true;
            const badge = valid ? 'bg-label-success' : 'bg-label-danger';
            const label = valid ? text('Valid', 'Valid') : (reason || status || text('Blocked', 'Blocked'));
            return `
                <div class="list-group-item d-flex justify-content-between align-items-start gap-3">
                    <div class="text-break">
                        <div class="fw-medium">${escapeHtml(referenceType || text('ReferenceFallback', 'reference'))}</div>
                        <small class="text-muted">${escapeHtml(referenceId || '-')}</small>
                    </div>
                    <span class="badge ${badge}">${escapeHtml(label)}</span>
                </div>`;
        }).join('');
    };

    const renderReviewBlockers = (blockingReasons) => {
        if (!controls.reviewBlockers) return;
        const reasons = Array.isArray(blockingReasons) ? blockingReasons.filter(Boolean) : [];
        if (!reasons.length) {
            showState(controls.reviewBlockers, '');
            return;
        }

        showState(controls.reviewBlockers, `${text('ReviewBlockersPrefix', 'Review blockers')}: ${reasons.join(', ')}`);
    };

    const escapeHtml = (value) => asString(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');

    const wireEvents = () => {
        controls.start?.addEventListener('click', startDraft);
        controls.reload?.addEventListener('click', reloadDraft);
        controls.save?.addEventListener('click', saveDraft);
        controls.validate?.addEventListener('click', validateReferences);
        controls.review?.addEventListener('click', reviewDraft);

        fields.personId?.addEventListener('personreference:selected', (event) => {
            const personId = asString(event.detail?.person?.personId);
            if (personId && fields.personId) fields.personId.value = personId;
        });
        fields.legalEntityPicker?.addEventListener('input', queueLegalEntitySearch);
        fields.legalEntityPicker?.addEventListener('change', selectLegalEntityOption);
        fields.legalEntityPicker?.addEventListener('blur', selectLegalEntityOption);
    };

    const init = () => {
        if (!canCreateDraft) {
            showState(controls.permission, text('PermissionDeniedWorkspace', 'Permission denied. The draft workspace is read-only for this session.'));
        }

        window.PersonReferencePicker?.init(fields.personId, {
            referenceable: true,
            status: 'Active',
            placeholder: text('SearchSameTenantPerson', 'Search same-tenant person'),
            labels: {
                placeholder: text('SearchSameTenantPerson', 'Search same-tenant person')
            }
        });

        wireEvents();
        updateStatePanel();
        renderReferenceSummary(null);
    };

    window.HcmEmployeeDraftWizard = {
        _test: {
            classifyStatus,
            collectPayload,
            collectReferenceRequest,
            extractErrorMessage,
            firstValue,
            normalizeLegalEntity,
            normalizeLegalEntitySearch,
            unwrap
        }
    };

    init();
})();
