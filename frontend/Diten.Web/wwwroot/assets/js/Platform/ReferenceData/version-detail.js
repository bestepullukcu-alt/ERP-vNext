'use strict';

(function () {
    const root = document.getElementById('rd-version-page');
    if (!root) return;

    const versionId = root.dataset.versionId;
    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || { can: () => true, apply: (el, _cap, stateAllowed) => { if (el) el.disabled = stateAllowed === false; return stateAllowed !== false; }, guard: () => true };
    const L = window.L10n || {};

    const tableBody = document.getElementById('rd-values-tbody');
    const modeBadge = document.getElementById('rd-version-mode');
    const blockersEl = document.getElementById('rd-validation-blockers');
    const versionJson = document.getElementById('rd-version-json');
    const badgeHost = document.getElementById('rd-version-badges');
    const inlineAlert = document.getElementById('rd-inline-alert');
    const emptyDraftState = document.getElementById('rd-empty-draft-state');
    const openPublishedFallbackBtn = document.getElementById('rd-btn-open-published-fallback');
    const seedFromPublishedBtn = document.getElementById('rd-btn-seed-draft-from-published');
    const openSetButton = document.getElementById('rd-btn-open-set');
    const retryButton = document.getElementById('rd-btn-retry-load');

    let version = null;
    let values = [];
    let lastValidation = null;
    let fatalLoadError = null;
    let currentSetId = null;
    let publishedVersionId = null;
    let currentSet = null;

    const showInlineAlert = (message) => {
        if (!inlineAlert) return;
        const value = String(message || '').trim();
        if (!value) {
            inlineAlert.classList.add('d-none');
            inlineAlert.textContent = '';
            return;
        }
        inlineAlert.textContent = value;
        inlineAlert.classList.remove('d-none');
    };

    const syncWorkspaceButton = () => {
        if (!openSetButton) return;
        if (!currentSetId) {
            openSetButton.disabled = true;
            return;
        }

        openSetButton.disabled = false;
    };

    const syncEmptyDraftState = () => {
        if (!emptyDraftState) return;
        const editableDraft = isEditableDraft();
        const hasValues = values.length > 0;
        const show = editableDraft && !hasValues;
        emptyDraftState.classList.toggle('d-none', !show);
        if (openPublishedFallbackBtn) {
            openPublishedFallbackBtn.disabled = !publishedVersionId;
        }
        if (seedFromPublishedBtn) {
            seedFromPublishedBtn.disabled = !publishedVersionId;
        }
    };

    const showError = (error, fallback) => {
        if (error?.isHandled) return;
        let message = error?.message || fallback || 'request_failed';
        const normalized = String(message).toLowerCase();
        if (normalized.includes('validation_blockers')) {
            message = L.ValidationBlockers || 'Draft has validation blockers.';
        } else if (normalized.includes('draft_required')) {
            message = 'This action is only available for editable draft versions.';
        } else if (normalized.includes('approval_required')) {
            message = 'Approval is required before publish.';
        } else if (normalized.includes('override_reason_required')) {
            message = 'Override reason is required for this action.';
        }
        showInlineAlert(message);
        if (window.showToast) {
            window.showToast(message, 'error');
            return;
        }
        console.error(error);
    };
    const isNotFoundError = (error) => {
        const message = String(error?.message || '').toLowerCase();
        return message.includes('not_found') || message.includes('not found');
    };

    const text = (value) => value == null || value === '' ? '-' : String(value);
    const badge = (label, value, css) => `<span class="badge ${css || 'bg-label-secondary'} me-1">${label}: ${text(value)}</span>`;

    const normalizeStatus = (status) => String(status || '').toLowerCase();
    const retiredSetReason = permissions.retiredSetReason || 'This reference data set is retired. Changes are disabled.';
    const isRetiredSet = (setInfo) => (typeof permissions.isRetiredSet === 'function'
        ? permissions.isRetiredSet(setInfo)
        : normalizeStatus(setInfo?.status || setInfo?.Status) === 'retired');
    const applySetGate = () => {
        const retired = isRetiredSet(currentSet);
        if (typeof permissions.setGlobalBlock === 'function') {
            permissions.setGlobalBlock(retired, retiredSetReason);
        }
        if (retired) {
            showInlineAlert(retiredSetReason);
        }
        return retired;
    };
    const isPublishedVersion = () => normalizeStatus(version?.status || version?.Status) === 'published';
    const effectiveImmutable = () => isPublishedVersion() || !!(version?.isImmutable ?? version?.IsImmutable);
    const effectiveEditable = () => !(typeof permissions.isBlocked === 'function' && permissions.isBlocked()) && !isPublishedVersion() && !!(version?.isEditable ?? version?.IsEditable ?? true);
    const isEditableDraft = () => normalizeStatus(version?.status || version?.Status) === 'draft' && !effectiveImmutable() && effectiveEditable();
    const normalizeState = (value) => String(value || '').toLowerCase();

    const setButtonState = (id, enabled) => {
        const button = document.getElementById(id);
        if (button) button.disabled = !enabled;
    };

    const navigate = (url) => {
        if (typeof window.__rdNavigate === 'function') {
            window.__rdNavigate(url);
            return;
        }
        window.location.href = url;
    };

    const openPublishReview = (capability) => {
        if (!permissions.guard(capability || 'canSubmitVersion', showInlineAlert)) return;
        if (!versionId || !isEditableDraft()) {
            showInlineAlert('Publish readiness review is available only for editable draft versions.');
            return;
        }

        navigate(`/Platform/ReferenceData/PublishReview/${encodeURIComponent(versionId)}`);
    };

    // Opening the review page to approve/publish only needs a non-published draft version (editable or already
    // submitted). Per-action gating happens inside the review page; entry requires approve OR publish capability.
    const openReviewForApproval = () => {
        if (!permissions.can('canApproveVersion') && !permissions.can('canPublishVersion')) {
            showInlineAlert(typeof permissions.reason === 'function' ? permissions.reason() : 'You do not have permission to perform this action.');
            return;
        }
        if (!versionId || normalizeStatus(version?.status || version?.Status) !== 'draft' || effectiveImmutable()) {
            showInlineAlert('Publish readiness review is available for draft versions awaiting publish.');
            return;
        }

        navigate(`/Platform/ReferenceData/PublishReview/${encodeURIComponent(versionId)}`);
    };

    const parseAttributes = (input) => {
        const raw = (input || '').trim();
        if (!raw) return null;
        const pairs = raw.split(';');
        const result = {};
        pairs.forEach((pair) => {
            const [k, ...rest] = pair.split('=');
            const key = (k || '').trim();
            if (!key) return;
            result[key] = rest.join('=').trim();
        });
        return Object.keys(result).length > 0 ? result : null;
    };

    const attributesToText = (attributes) => {
        if (!attributes || typeof attributes !== 'object') return '';
        return Object.keys(attributes)
            .map((key) => `${key}=${attributes[key]}`)
            .join('; ');
    };

    const redrawSortOrder = () => {
        values.forEach((item, index) => {
            item.sortOrder = (index + 1) * 10;
        });
    };

    const renderValidationBlockers = (validationRun) => {
        const blockers = validationRun?.publishBlockers || validationRun?.PublishBlockers || [];
        if (!blockersEl) return;
        if (!blockers.length) {
            blockersEl.classList.add('d-none');
            blockersEl.textContent = '';
            return;
        }

        const prefix = L.ValidationBlockers || 'Publish blockers';
        blockersEl.textContent = `${prefix}: ${blockers.join(', ')}`;
        blockersEl.classList.remove('d-none');
    };

    const updateActionStates = () => {
        if (fatalLoadError) {
            setButtonState('rd-btn-add-value', false);
            setButtonState('rd-btn-save-values', false);
            setButtonState('rd-btn-validate', false);
            setButtonState('rd-btn-submit', false);
            setButtonState('rd-btn-publish', false);
            return;
        }

        const editable = isEditableDraft();
        permissions.apply(document.getElementById('rd-btn-add-value'), 'canUpdateVersion', editable, 'An editable draft is required.');
        permissions.apply(document.getElementById('rd-btn-save-values'), 'canUpdateVersion', editable, 'An editable draft is required.');
        permissions.apply(document.getElementById('rd-btn-validate'), 'canValidateVersion', editable, 'An editable draft is required.');
        permissions.apply(document.getElementById('rd-btn-submit'), 'canSubmitVersion', editable, 'An editable draft is required.');
        // The Review Publish Readiness button opens the governance review page, which stays relevant after the
        // version is submitted (IsEditable flips to false). Gate it on the version being a non-published draft
        // plus approve OR publish capability, rather than on editability, so approvers are not locked out.
        const canEnterReview = permissions.can('canApproveVersion') || permissions.can('canPublishVersion');
        const blocked = typeof permissions.isBlocked === 'function' && permissions.isBlocked();
        const reviewableDraft = normalizeStatus(version?.status || version?.Status) === 'draft' && !effectiveImmutable();
        const publishBtnEl = document.getElementById('rd-btn-publish');
        if (publishBtnEl) {
            const enabled = canEnterReview && reviewableDraft && !blocked;
            publishBtnEl.disabled = !enabled;
            publishBtnEl.setAttribute('aria-disabled', enabled ? 'false' : 'true');
            if (enabled) {
                publishBtnEl.removeAttribute('title');
            } else {
                publishBtnEl.setAttribute('title', !canEnterReview
                    ? (typeof permissions.reason === 'function' ? permissions.reason() : 'You do not have permission to perform this action.')
                    : 'A draft version awaiting publish is required.');
            }
        }
        permissions.apply(seedFromPublishedBtn, 'canUpdateVersion', !!publishedVersionId && editable, 'A published version and editable draft are required.');
        syncEmptyDraftState();
    };

    const renderRows = () => {
        if (!tableBody) return;
        const editable = isEditableDraft();
        const noDataText = L.NoRecords || 'No records found.';
        if (!values.length) {
            const emptyText = isEditableDraft()
                ? 'No values in this draft yet.'
                : noDataText;
            tableBody.innerHTML = `<tr><td colspan="8" class="text-center text-muted py-4">${emptyText}</td></tr>`;
            return;
        }

        tableBody.innerHTML = values.map((item, index) => {
            const statusChecked = item.isActive !== false ? 'checked' : '';
            const attrsText = attributesToText(item.attributes || item.Attributes);
            const code = text(item.code || item.Code);
            const label = text(item.label || item.Label);
            const description = text(item.description || item.Description);
            const sortOrder = Number(item.sortOrder ?? item.SortOrder ?? ((index + 1) * 10));
            const actions = editable
                ? `<button type="button" class="btn btn-sm btn-label-danger rd-remove-row" data-index="${index}"><i class="bx bx-trash"></i></button>`
                : '<span class="text-muted">-</span>';

            if (!editable) {
                return `<tr>
                    <td class="text-nowrap">
                        <button type="button" class="btn btn-sm btn-icon btn-label-secondary rd-move-up" data-index="${index}" disabled><i class="bx bx-chevron-up"></i></button>
                        <button type="button" class="btn btn-sm btn-icon btn-label-secondary rd-move-down" data-index="${index}" disabled><i class="bx bx-chevron-down"></i></button>
                    </td>
                    <td>${code}</td>
                    <td>${label}</td>
                    <td>${description}</td>
                    <td><span class="badge ${item.isActive !== false ? 'bg-label-success' : 'bg-label-secondary'}">${item.isActive !== false ? (L.Active || 'Active') : (L.Passive || 'Passive')}</span></td>
                    <td>${sortOrder}</td>
                    <td>${text(attrsText)}</td>
                    <td class="text-end">${actions}</td>
                </tr>`;
            }

            return `<tr>
                <td class="text-nowrap">
                    <button type="button" class="btn btn-sm btn-icon btn-label-secondary rd-move-up" data-index="${index}" ${index === 0 ? 'disabled' : ''}><i class="bx bx-chevron-up"></i></button>
                    <button type="button" class="btn btn-sm btn-icon btn-label-secondary rd-move-down" data-index="${index}" ${index === values.length - 1 ? 'disabled' : ''}><i class="bx bx-chevron-down"></i></button>
                </td>
                <td><input type="text" class="form-control form-control-sm rd-input-code" data-index="${index}" value="${code === '-' ? '' : code}" maxlength="128" /></td>
                <td><input type="text" class="form-control form-control-sm rd-input-label" data-index="${index}" value="${label === '-' ? '' : label}" maxlength="256" /></td>
                <td><input type="text" class="form-control form-control-sm rd-input-description" data-index="${index}" value="${description === '-' ? '' : description}" maxlength="2000" /></td>
                <td>
                    <div class="form-check form-switch mb-0">
                        <input class="form-check-input rd-input-active" type="checkbox" data-index="${index}" ${statusChecked} />
                    </div>
                </td>
                <td><input type="number" min="0" class="form-control form-control-sm rd-input-sort" data-index="${index}" value="${sortOrder}" /></td>
                <td><input type="text" class="form-control form-control-sm rd-input-attrs" data-index="${index}" value="${attrsText}" placeholder="key=value; key2=value2" /></td>
                <td class="text-end">${actions}</td>
            </tr>`;
        }).join('');
    };

    const syncFromInputs = () => {
        if (!isEditableDraft()) return;
        values = values.map((item, index) => {
            const code = document.querySelector(`.rd-input-code[data-index="${index}"]`)?.value ?? item.code;
            const label = document.querySelector(`.rd-input-label[data-index="${index}"]`)?.value ?? item.label;
            const description = document.querySelector(`.rd-input-description[data-index="${index}"]`)?.value ?? item.description;
            const isActive = !!document.querySelector(`.rd-input-active[data-index="${index}"]`)?.checked;
            const sortOrderRaw = document.querySelector(`.rd-input-sort[data-index="${index}"]`)?.value;
            const sortOrder = Number(sortOrderRaw ?? item.sortOrder ?? ((index + 1) * 10));
            const attrsRaw = document.querySelector(`.rd-input-attrs[data-index="${index}"]`)?.value;
            return {
                code: (code || '').trim(),
                label: (label || '').trim(),
                description: (description || '').trim(),
                isActive,
                sortOrder: Number.isFinite(sortOrder) ? Math.max(0, sortOrder) : 0,
                parentValueCode: item.parentValueCode || item.ParentValueCode || null,
                attributes: parseAttributes(attrsRaw)
            };
        });
        values.sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
    };

    const renderHeader = () => {
        if (!version) return;
        const status = normalizeStatus(version.status || version.Status);
        const immutable = effectiveImmutable();
        const approvalState = isPublishedVersion() && normalizeState(version.approvalState || version.ApprovalState) === 'notstarted'
            ? 'Approved'
            : version.approvalState || version.ApprovalState;
        const immutableCss = immutable ? 'bg-label-success' : 'bg-label-warning';
        badgeHost.innerHTML = `<div class="col-12">${badge(L.StatusText || 'Status', version.status || version.Status, status === 'published' ? 'bg-label-success' : 'bg-label-warning')}${badge(L.Approve || 'Approval', approvalState, 'bg-label-info')}${badge(L.Immutable || 'Immutable', immutable, immutableCss)}${badge(L.Version || 'Version', version.versionNumber || version.VersionNumber, 'bg-label-primary')}</div>`;

        const retiredBlocked = typeof permissions.isBlocked === 'function' && permissions.isBlocked();
        const editable = isEditableDraft();
        const retiredBadge = retiredBlocked ? badge('Set', 'Retired', 'bg-label-secondary') : '';
        if (modeBadge) {
            modeBadge.textContent = retiredBlocked ? 'Read-only (Retired set)' : editable ? (L.DraftEditable || 'Editable Draft') : (L.ReadOnlyPublished || 'Read-only (Published)');
            modeBadge.className = `badge ${editable ? 'bg-label-success' : 'bg-label-secondary'}`;
        }
        badgeHost.innerHTML += retiredBadge;

        currentSetId = version?.setId || version?.SetId || null;
        syncWorkspaceButton();
        updateActionStates();
    };

    const load = async () => {
        fatalLoadError = null;
        showInlineAlert(null);

        let v;
        try {
            v = await api.getVersion(versionId);
        } catch (error) {
            if (isNotFoundError(error)) {
                fatalLoadError = 'This version could not be found. It may have been deleted or belongs to another tenant.';
                currentSetId = null;
                syncWorkspaceButton();
                showInlineAlert(fatalLoadError);
                if (badgeHost) {
                    badgeHost.innerHTML = '<div class="col-12"><span class="badge bg-label-danger">Version not found</span></div>';
                }
                if (modeBadge) {
                    modeBadge.textContent = 'Unavailable';
                    modeBadge.className = 'badge bg-label-danger';
                }
                if (tableBody) {
                    tableBody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">Version not found. Return to the set list and select a valid draft version.</td></tr>';
                }
                publishedVersionId = null;
                syncEmptyDraftState();
                if (versionJson) {
                    versionJson.textContent = JSON.stringify({ error: 'version_not_found', versionId }, null, 2);
                }
                renderValidationBlockers(null);
                updateActionStates();
                return;
            }
            throw error;
        }

        let valuePayload = null;
        try {
            valuePayload = await api.getVersionValues(versionId);
        } catch (error) {
            if (isNotFoundError(error)) {
                valuePayload = { items: [] };
            } else {
                throw error;
            }
        }

        version = v;
        currentSetId = version?.setId || version?.SetId || null;
        publishedVersionId = null;
        currentSet = null;
        if (currentSetId) {
            try {
                currentSet = await api.getSet(currentSetId);
                publishedVersionId = currentSet?.publishedVersionId || currentSet?.PublishedVersionId || null;
                applySetGate();
            } catch (_error) {
                publishedVersionId = null;
                currentSet = null;
                if (typeof permissions.clearGlobalBlock === 'function') {
                    permissions.clearGlobalBlock();
                }
            }
        }
        values = (valuePayload?.items || valuePayload?.Items || []).map((item) => ({
            code: item.code || item.Code || '',
            label: item.label || item.Label || '',
            description: item.description || item.Description || '',
            isActive: item.isActive ?? item.IsActive ?? true,
            sortOrder: item.sortOrder ?? item.SortOrder ?? 0,
            attributes: item.attributes || item.Attributes || null,
            parentValueCode: item.parentValueCode || item.ParentValueCode || null
        }));

        versionJson.textContent = JSON.stringify({ version: v, values }, null, 2);
        renderHeader();
        renderRows();
        lastValidation = null;
        renderValidationBlockers(null);
        updateActionStates();
    };

    const addValue = () => {
        if (!permissions.guard('canUpdateVersion', showInlineAlert)) return;
        syncFromInputs();
        const nextSort = values.length ? Math.max(...values.map(x => x.sortOrder || 0)) + 10 : 10;
        values.push({
            code: '',
            label: '',
            description: '',
            isActive: true,
            sortOrder: nextSort,
            parentValueCode: null,
            attributes: null
        });
        renderRows();
    };

    const saveValues = async () => {
        if (!permissions.guard('canUpdateVersion', showInlineAlert)) return;
        syncFromInputs();
        const duplicateCodes = new Set();
        const seenCodes = new Set();
        values.forEach((item) => {
            const code = String(item.code || '').trim().toLowerCase();
            if (!code) return;
            if (seenCodes.has(code)) duplicateCodes.add(code);
            seenCodes.add(code);
        });

        const missingCode = values.some((item) => !String(item.code || '').trim());
        const missingLabel = values.some((item) => !String(item.label || '').trim());
        if (missingCode || missingLabel) {
            throw new Error('Each value must contain both code and label.');
        }
        if (duplicateCodes.size > 0) {
            throw new Error(`Duplicate value code(s): ${Array.from(duplicateCodes).join(', ')}`);
        }

        const payload = {
            expected_concurrency_token: version?.concurrencyToken || version?.ConcurrencyToken,
            values: values.map((item) => ({
                code: item.code,
                label: item.label,
                description: item.description || null,
                is_active: item.isActive !== false,
                sort_order: Number(item.sortOrder || 0),
                parent_value_code: item.parentValueCode || null,
                attributes: item.attributes || null
            }))
        };

        await api.replaceVersionValues(versionId, payload);
        window.showToast?.(L.RecordSaved || L.Save || 'Saved', 'success');
        await load();
    };

    const moveRow = (index, delta) => {
        syncFromInputs();
        const targetIndex = index + delta;
        if (targetIndex < 0 || targetIndex >= values.length) return;
        const [item] = values.splice(index, 1);
        values.splice(targetIndex, 0, item);
        redrawSortOrder();
        renderRows();
    };

    tableBody?.addEventListener('click', (event) => {
        const removeBtn = event.target.closest('.rd-remove-row');
        if (removeBtn) {
            if (!permissions.guard('canUpdateVersion', showInlineAlert)) return;
            const index = Number(removeBtn.dataset.index);
            if (Number.isFinite(index)) {
                syncFromInputs();
                values.splice(index, 1);
                redrawSortOrder();
                renderRows();
            }
            return;
        }

        const moveUpBtn = event.target.closest('.rd-move-up');
        if (moveUpBtn) {
            if (!permissions.guard('canUpdateVersion', showInlineAlert)) return;
            const index = Number(moveUpBtn.dataset.index);
            if (Number.isFinite(index)) moveRow(index, -1);
            return;
        }

        const moveDownBtn = event.target.closest('.rd-move-down');
        if (moveDownBtn) {
            if (!permissions.guard('canUpdateVersion', showInlineAlert)) return;
            const index = Number(moveDownBtn.dataset.index);
            if (Number.isFinite(index)) moveRow(index, 1);
        }
    });

    document.getElementById('rd-btn-add-value')?.addEventListener('click', addValue);
    document.getElementById('rd-btn-save-values')?.addEventListener('click', async () => {
        try {
            await saveValues();
        } catch (error) {
            showError(error);
        }
    });

    document.getElementById('rd-btn-validate')?.addEventListener('click', async () => {
        if (!permissions.guard('canValidateVersion', showInlineAlert)) return;
        try {
            lastValidation = await api.validateVersion(versionId);
            renderValidationBlockers(lastValidation);
            updateActionStates();
            window.showToast?.(L.Validate || 'Validated', 'success');
        } catch (error) {
            showError(error);
        }
    });

    const submitForApprovalButton = document.getElementById('rd-btn-submit');
    const reviewPublishReadinessButton = document.getElementById('rd-btn-publish');
    if (submitForApprovalButton) submitForApprovalButton.onclick = () => openPublishReview('canSubmitVersion');
    if (reviewPublishReadinessButton) reviewPublishReadinessButton.onclick = () => openReviewForApproval();

    retryButton?.addEventListener('click', async () => {
        try {
            await load();
        } catch (error) {
            showError(error);
        }
    });

    openSetButton?.addEventListener('click', () => {
        if (!currentSetId) return;
        navigate(`/Platform/ReferenceData/Sets/${currentSetId}`);
    });

    openPublishedFallbackBtn?.addEventListener('click', () => {
        if (!publishedVersionId) return;
        navigate(`/Platform/ReferenceData/Versions/${publishedVersionId}`);
    });

    seedFromPublishedBtn?.addEventListener('click', async () => {
        if (!permissions.guard('canUpdateVersion', showInlineAlert)) return;
        if (!publishedVersionId || !isEditableDraft()) return;
        try {
            const publishedValuesPayload = await api.getVersionValues(publishedVersionId);
            const publishedValues = (publishedValuesPayload?.items || publishedValuesPayload?.Items || []).map((item) => ({
                code: item.code || item.Code || '',
                label: item.label || item.Label || '',
                description: item.description || item.Description || null,
                is_active: item.isActive ?? item.IsActive ?? true,
                sort_order: item.sortOrder ?? item.SortOrder ?? 0,
                parent_value_code: item.parentValueCode || item.ParentValueCode || null,
                attributes: item.attributes || item.Attributes || null
            }));

            await api.replaceVersionValues(versionId, {
                expected_concurrency_token: version?.concurrencyToken || version?.ConcurrencyToken,
                values: publishedValues
            });

            window.showToast?.('Draft seeded from published version.', 'success');
            await load();
        } catch (error) {
            showError(error, 'Failed to seed draft from published version.');
        }
    });

    load().catch((error) => showError(error));
})();
