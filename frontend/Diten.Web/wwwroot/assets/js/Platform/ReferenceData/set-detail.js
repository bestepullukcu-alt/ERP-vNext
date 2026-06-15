'use strict';

(function () {
    const root = document.getElementById('rd-set-detail-page');
    if (!root) return;

    const setId = root.dataset.setId;
    const api = window.ReferenceDataApi;
    const setNativeDisabled = (element, disabled, reason) => {
        if (!element) return;
        element.disabled = !!disabled;
        if (disabled) {
            element.setAttribute('title', reason || 'This action is unavailable for the current state.');
            element.setAttribute('aria-disabled', 'true');
            element.dataset.disabledReason = reason || 'This action is unavailable for the current state.';
        } else {
            element.removeAttribute('title');
            element.setAttribute('aria-disabled', 'false');
            delete element.dataset.disabledReason;
        }
    };
    const permissions = window.ReferenceDataPermissions || {
        can: () => true,
        reason: () => 'You do not have permission to perform this action.',
        apply: (el, _cap, stateAllowed, stateReason) => {
            const allowed = stateAllowed !== false;
            setNativeDisabled(el, !allowed, stateReason);
            return allowed;
        },
        guard: () => true,
        setDisabled: setNativeDisabled
    };
    const L = window.L10n || {};

    const alertEl = document.getElementById('rd-set-alert');
    const publishedFirstGuidanceEl = document.getElementById('rd-published-first-guidance');
    const draftValidationEl = document.getElementById('rd-draft-validation');

    let currentSet = null;
    let draftVersion = null;
    let publishedVersion = null;
    let draftValueCount = 0;
    let publishedValueCount = 0;
    let lastValidation = null;
    let hydrationFailed = false;

    const byId = (id) => document.getElementById(id);
    const text = (value) => value == null || value === '' ? '-' : String(value);
    const hydrationRegionIds = ['rd-set-actions', 'rd-set-hydrated-content', 'rd-workspace-links'];

    const showToast = (message, type) => {
        if (window.showToast) {
            window.showToast(message, type || 'info');
            return;
        }
        console.log(message);
    };

    const showAlert = (message, level) => {
        if (!alertEl) return;
        if (!message) {
            alertEl.className = 'alert alert-warning d-none';
            alertEl.textContent = '';
            return;
        }

        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'warning';
        alertEl.className = `alert alert-${css}`;
        alertEl.textContent = message;
    };

    const showPublishedFirstGuidance = (visible) => {
        if (!publishedFirstGuidanceEl) return;
        publishedFirstGuidanceEl.classList.toggle('d-none', !visible);
    };

    const setHydrationRegionsVisible = (visible) => {
        hydrationRegionIds.forEach((id) => byId(id)?.classList.toggle('d-none', !visible));
    };

    const navigate = (url) => {
        if (typeof window.__rdNavigate === 'function') {
            window.__rdNavigate(url);
            return;
        }
        window.location.href = url;
    };

    const showValidation = (message, level) => {
        if (!draftValidationEl) return;
        if (!message) {
            draftValidationEl.className = 'alert alert-info d-none mt-3 mb-0';
            draftValidationEl.textContent = '';
            return;
        }

        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'info';
        draftValidationEl.className = `alert alert-${css} mt-3 mb-0`;
        draftValidationEl.textContent = message;
    };

    const setInput = (id, value) => {
        const el = byId(id);
        if (el) el.value = value || '';
    };

    const setText = (id, value) => {
        const el = byId(id);
        if (el) el.textContent = text(value);
    };

    const formatDate = (raw) => {
        if (!raw) return '-';
        const date = new Date(raw);
        if (Number.isNaN(date.getTime())) return String(raw);
        // Compact, locale-stable format: "Jun 05, 26 12:35 PM" (no seconds).
        const datePart = new Intl.DateTimeFormat('en-US', { month: 'short', day: '2-digit', year: '2-digit' }).format(date);
        const timePart = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', hour12: true }).format(date);
        return `${datePart} ${timePart}`;
    };

    const normalize = (value) => String(value || '').toLowerCase();
    const unavailableReason = 'Set workspace is not loaded.';
    const noDraftReason = 'No active draft exists for this set.';
    const noPublishedReason = 'No published version exists for this set.';
    const editableDraftReason = 'An editable active draft is required.';
    const existingDraftReason = 'An active draft already exists.';
    const validationRequiredReason = 'Validate the draft before submitting for approval.';
    const retiredSetReason = 'Retired sets are read-only.';

    const getVersionId = (version) => version?.versionId || version?.VersionId || null;
    const getSetCode = () => currentSet?.setCode || currentSet?.SetCode || null;
    const getSetStatus = () => normalize(currentSet?.status || currentSet?.Status);
    const isRetiredSet = () => getSetStatus() === 'retired';
    const hasEditableDraft = () => {
        const status = normalize(draftVersion?.status || draftVersion?.Status);
        return !!getVersionId(draftVersion) && status === 'draft' && !!(draftVersion?.isEditable ?? draftVersion?.IsEditable);
    };

    const formatVersionLabel = (version, emptyText) => {
        if (!version) return emptyText;
        const number = version.versionNumber ?? version.VersionNumber;
        const status = version.status || version.Status || 'Unknown';
        return number == null || number === '' ? `Version - ${status}` : `Version ${number} - ${status}`;
    };

    const validationBlockers = () => lastValidation?.publishBlockers || lastValidation?.PublishBlockers || [];
    const canSubmitValidatedDraft = () => hasEditableDraft() && !!lastValidation && validationBlockers().length === 0;

    const setDisabled = (element, disabled, reason) => {
        if (!element) return;
        if (typeof permissions.setDisabled === 'function') {
            permissions.setDisabled(element, disabled, reason);
            return;
        }

        setNativeDisabled(element, disabled, reason || unavailableReason);
    };

    const setLinkState = (anchor, href, enabled, reason) => {
        if (!anchor) return;
        anchor.href = enabled ? href : '#';
        anchor.classList.toggle('disabled', !enabled);
        anchor.setAttribute('aria-disabled', enabled ? 'false' : 'true');
        if (enabled) {
            anchor.removeAttribute('title');
            delete anchor.dataset.disabledReason;
        } else {
            anchor.setAttribute('title', reason || unavailableReason);
            anchor.dataset.disabledReason = reason || unavailableReason;
        }
    };

    const disabledReason = (id) => byId(id)?.dataset?.disabledReason || unavailableReason;
    const notifyIfDisabled = (id) => {
        const element = byId(id);
        if (!element) return false;
        const disabled = !!element.disabled || element.getAttribute('aria-disabled') === 'true';
        if (disabled) {
            showAlert(disabledReason(id), 'warning');
            return true;
        }
        return false;
    };

    const isNotFoundError = (error) => {
        const message = String(error?.message || '').toLowerCase();
        return message.includes('not_found') || message.includes('not found');
    };
    const isActiveDraftExistsError = (error) => {
        const message = String(error?.message || '').toLowerCase();
        return message.includes('active_draft_exists') || message.includes('active draft');
    };

    const wireWorkspaceLinks = () => {
        const setCode = getSetCode();
        const draftId = getVersionId(draftVersion);
        const hydrated = !!currentSet && !hydrationFailed;
        const setContextReason = hydrated ? 'Set code is required.' : unavailableReason;

        setLinkState(
            byId('rd-link-version-editor'),
            draftId ? `/Platform/ReferenceData/Versions/${encodeURIComponent(draftId)}` : '#',
            hydrated && !!draftId && permissions.can('canUpdateVersion'),
            !hydrated ? unavailableReason : !draftId ? noDraftReason : permissions.reason?.() || 'You do not have permission to perform this action.'
        );
        setLinkState(byId('rd-link-hierarchy'), setCode ? `/Platform/ReferenceData/Hierarchy/${encodeURIComponent(setCode)}` : '#', hydrated && !!setCode, setContextReason);
        setLinkState(byId('rd-link-attributes'), setCode ? `/Platform/ReferenceData/Attributes/${encodeURIComponent(setCode)}` : '#', hydrated && !!setCode, setContextReason);
        setLinkState(byId('rd-link-mappings'), setCode ? `/Platform/ReferenceData/Mappings/${encodeURIComponent(setCode)}` : '#', hydrated && !!setCode, setContextReason);
    };

    const shouldDefaultToPublishedVersion = () => {
        const hasDraft = !!(draftVersion?.versionId || draftVersion?.VersionId);
        const hasPublished = !!(publishedVersion?.versionId || publishedVersion?.VersionId);
        return hasDraft && hasPublished && draftValueCount === 0 && publishedValueCount > 0;
    };

    const publishReviewUrl = () => {
        const draftId = draftVersion?.versionId || draftVersion?.VersionId;
        return draftId ? `/Platform/ReferenceData/PublishReview/${encodeURIComponent(draftId)}` : null;
    };

    const renderSet = () => {
        if (!currentSet || hydrationFailed) {
            setHydrationRegionsVisible(false);
            setInput('rd-set-code', '');
            setInput('rd-set-scope', '');
            setInput('rd-set-name', '');
            setInput('rd-set-description', '');
            setInput('rd-set-status', '');
            setText('rd-active-draft', hydrationFailed ? 'Unavailable' : 'Loading');
            setText('rd-published-version', hydrationFailed ? 'Unavailable' : 'Loading');
            setText('rd-set-row-version', '-');
            setText('rd-set-updated', '-');

            ['rd-btn-open-draft', 'rd-btn-open-published', 'rd-btn-create-from-published', 'rd-btn-validate-draft', 'rd-btn-submit-draft', 'rd-btn-publish-draft', 'rd-btn-create-version', 'rd-btn-save-set', 'rd-btn-start-draft-wizard', 'rd-btn-import-to-draft']
                .forEach((id) => setDisabled(byId(id), true, unavailableReason));
            wireWorkspaceLinks();
            return;
        }

        setHydrationRegionsVisible(true);
        setInput('rd-set-code', currentSet.setCode || currentSet.SetCode);
        setInput('rd-set-scope', currentSet.scopeType || currentSet.ScopeType);
        setInput('rd-set-name', currentSet.name || currentSet.Name);
        setInput('rd-set-description', currentSet.description || currentSet.Description || '');
        setInput('rd-set-status', currentSet.status || currentSet.Status);

        setText('rd-active-draft', formatVersionLabel(draftVersion, 'No active draft'));
        setText('rd-published-version', formatVersionLabel(publishedVersion, 'No published version'));
        setText('rd-set-row-version', currentSet.rowVersion || currentSet.RowVersion);
        setText('rd-set-updated', formatDate(currentSet.updatedAt || currentSet.UpdatedAt || currentSet.createdAt || currentSet.CreatedAt));

        const hasDraft = !!getVersionId(draftVersion);
        const hasPublished = !!getVersionId(publishedVersion);
        const editableDraft = hasEditableDraft();
        const retired = isRetiredSet();

        setDisabled(byId('rd-btn-open-draft'), !hasDraft, noDraftReason);
        setDisabled(byId('rd-btn-open-published'), !hasPublished, noPublishedReason);
        permissions.apply(
            byId('rd-btn-create-from-published'),
            'canCreateVersion',
            hasPublished && !hasDraft && !retired,
            retired ? retiredSetReason : !hasPublished ? noPublishedReason : existingDraftReason
        );

        const validateDraftButton = byId('rd-btn-validate-draft');
        const submitDraftButton = byId('rd-btn-submit-draft');
        const publishDraftButton = byId('rd-btn-publish-draft');
        const createDraftButton = byId('rd-btn-create-version');
        permissions.apply(validateDraftButton, 'canValidateVersion', editableDraft, editableDraftReason);
        permissions.apply(submitDraftButton, 'canSubmitVersion', canSubmitValidatedDraft(), editableDraft ? validationRequiredReason : editableDraftReason);
        // The publish-readiness review page hosts the whole governance flow (validate/submit/approve/publish),
        // so its entry button must stay reachable whenever a draft version exists — not only while the draft is
        // still editable. After submit the backend flips IsEditable to false, which previously disabled this
        // button for everyone and blocked approvers from opening the review screen. Per-action gating still
        // happens inside the review page; approvers enter via the approve OR publish capability.
        const canEnterReview = permissions.can('canApproveVersion') || permissions.can('canPublishVersion');
        const reviewReason = !canEnterReview
            ? (permissions.reason?.() || 'You do not have permission to perform this action.')
            : retired ? retiredSetReason
                : !hasDraft ? noDraftReason
                    : editableDraftReason;
        setDisabled(publishDraftButton, !(canEnterReview && hasDraft && !retired), reviewReason);
        permissions.apply(createDraftButton, 'canCreateVersion', !hasDraft && !retired, retired ? retiredSetReason : existingDraftReason);
        permissions.apply(byId('rd-btn-save-set'), 'canUpdateSet', !retired, retiredSetReason);
        permissions.apply(byId('rd-btn-start-draft-wizard'), 'canUpdateVersion', editableDraft, editableDraftReason);
        permissions.apply(byId('rd-btn-import-to-draft'), 'canImportPreview', editableDraft, editableDraftReason);

        const publishedFirst = shouldDefaultToPublishedVersion();
        if (publishedFirst) {
            showAlert('Draft is currently empty. Open the published version for inspection or add draft values before submit.', 'warning');
        }
        showPublishedFirstGuidance(publishedFirst);

        wireWorkspaceLinks();
    };

    const load = async () => {
        showAlert(null);
        showValidation(null);
        hydrationFailed = false;
        currentSet = null;
        draftVersion = null;
        publishedVersion = null;
        draftValueCount = 0;
        publishedValueCount = 0;
        lastValidation = null;
        renderSet();

        try {
            currentSet = await api.getSet(setId);
        } catch (error) {
            hydrationFailed = true;
            renderSet();
            throw error;
        }

        const activeDraftVersionId = currentSet.activeDraftVersionId || currentSet.ActiveDraftVersionId;
        const publishedVersionId = currentSet.publishedVersionId || currentSet.PublishedVersionId;

        const loadVersionIfExists = async (candidateId, holder, missingMessage) => {
            if (!candidateId) return;
            try {
                const loaded = await api.getVersion(candidateId);
                if (holder === 'draft') {
                    draftVersion = loaded;
                } else {
                    publishedVersion = loaded;
                }

                const versionId = loaded?.versionId || loaded?.VersionId;
                if (versionId) {
                    const valuesPayload = await api.getVersionValues(versionId);
                    const itemCount = (valuesPayload?.items || valuesPayload?.Items || []).length;
                    if (holder === 'draft') {
                        draftValueCount = itemCount;
                    } else {
                        publishedValueCount = itemCount;
                    }
                }
            } catch (error) {
                if (isNotFoundError(error)) {
                    showAlert(missingMessage, 'warning');
                    return;
                }
                throw error;
            }
        };

        try {
            await Promise.all([
                loadVersionIfExists(activeDraftVersionId, 'draft', 'Active draft reference is stale. Create a new draft version.'),
                loadVersionIfExists(publishedVersionId, 'published', 'Published version reference is stale.')
            ]);
        } catch (error) {
            hydrationFailed = true;
            renderSet();
            throw error;
        }
        renderSet();
    };

    const saveSet = async () => {
        if (!permissions.guard('canUpdateSet', showAlert)) return;
        if (!currentSet) return;

        const payload = {
            row_version: Number(currentSet.rowVersion || currentSet.RowVersion || 0),
            name: byId('rd-set-name')?.value?.trim(),
            description: byId('rd-set-description')?.value?.trim() || null,
            status: byId('rd-set-status')?.value || null
        };

        if (!payload.name) {
            showAlert(L.ErrorState || 'Set name is required.', 'error');
            return;
        }

        currentSet = await api.patchSet(setId, payload);
        showToast(L.RecordSaved || 'Record saved.', 'success');
        renderSet();
    };

    const ensureDraft = () => {
        if (!draftVersion) {
            showAlert('No active draft exists. Create a draft version first.', 'warning');
            return false;
        }

        const status = normalize(draftVersion.status || draftVersion.Status);
        const editable = draftVersion.isEditable ?? draftVersion.IsEditable;
        if (status !== 'draft' || !editable) {
            showAlert('Active draft is not editable in current governance state.', 'warning');
            return false;
        }

        return true;
    };

    const validateDraft = async () => {
        if (!permissions.guard('canValidateVersion', showAlert)) return;
        if (!ensureDraft()) return;

        const draftId = draftVersion.versionId || draftVersion.VersionId;
        lastValidation = await api.validateVersion(draftId);
        const blockers = lastValidation.publishBlockers || lastValidation.PublishBlockers || [];

        if (blockers.length > 0) {
            showValidation(`Validation blockers: ${blockers.join(', ')}`, 'error');
        } else {
            showValidation('Validation passed. Draft is ready for submit/publish checks.', 'success');
        }

        renderSet();
    };

    const openPublishReadiness = () => {
        // Navigating to the review page only requires a draft version to exist. The page itself handles the
        // editable-vs-submitted state (validate/submit for editable drafts, approve/publish once submitted),
        // so we intentionally do not require an editable draft here — that would lock approvers out post-submit.
        const target = publishReviewUrl();
        if (!target) {
            showAlert(noDraftReason, 'warning');
            return;
        }

        navigate(target);
    };

    byId('rd-btn-create-version')?.addEventListener('click', async () => {
        if (notifyIfDisabled('rd-btn-create-version')) return;
        if (!permissions.guard('canCreateVersion', showAlert)) return;
        try {
            if (draftVersion) {
                const draftId = draftVersion.versionId || draftVersion.VersionId;
                navigate(`/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(draftId)}`);
                return;
            }

            const created = await api.createVersion(setId, {});
            const versionId = created.versionId || created.VersionId;
            if (versionId) {
                navigate(`/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(versionId)}`);
            }
        } catch (error) {
            if (isActiveDraftExistsError(error)) {
                await load();
                const existingDraftId = draftVersion?.versionId || draftVersion?.VersionId;
                if (existingDraftId) {
                    navigate(`/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(existingDraftId)}`);
                    return;
                }
            }
            if (error?.isHandled) return;
            showAlert(error?.message || (L.ErrorState || 'Draft creation failed.'), 'error');
        }
    });

    byId('rd-btn-open-draft')?.addEventListener('click', () => {
        if (notifyIfDisabled('rd-btn-open-draft')) return;
        const draftId = getVersionId(draftVersion);
        if (draftId) {
            navigate(`/Platform/ReferenceData/Versions/${encodeURIComponent(draftId)}`);
        }
    });

    byId('rd-btn-open-published')?.addEventListener('click', () => {
        if (notifyIfDisabled('rd-btn-open-published')) return;
        const publishedId = getVersionId(publishedVersion);
        if (publishedId) {
            navigate(`/Platform/ReferenceData/Versions/${encodeURIComponent(publishedId)}`);
        }
    });

    byId('rd-btn-create-from-published')?.addEventListener('click', async () => {
        if (notifyIfDisabled('rd-btn-create-from-published')) return;
        if (!permissions.guard('canCreateVersion', showAlert)) return;
        try {
            const publishedId = getVersionId(publishedVersion);
            if (!publishedId) {
                showAlert('No published version exists for draft seeding.', 'warning');
                return;
            }

            if (draftVersion) {
                showAlert(existingDraftReason, 'warning');
                return;
            }

            const created = await api.createVersion(setId, { source_version_id: publishedId });
            const createdId = created?.versionId || created?.VersionId;
            if (createdId) {
                navigate(`/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(createdId)}`);
            }
        } catch (error) {
            if (error?.isHandled) return;
            showAlert(error?.message || 'Could not create draft from published version.', 'error');
        }
    });

    byId('rd-btn-start-draft-wizard')?.addEventListener('click', () => {
        if (notifyIfDisabled('rd-btn-start-draft-wizard')) return;
        if (!permissions.guard('canUpdateVersion', showAlert)) return;
        const draftId = getVersionId(draftVersion);
        if (!draftId) {
            showAlert(noDraftReason, 'warning');
            return;
        }
        navigate(`/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(draftId)}`);
    });

    byId('rd-btn-import-to-draft')?.addEventListener('click', () => {
        if (notifyIfDisabled('rd-btn-import-to-draft')) return;
        if (!permissions.guard('canImportPreview', showAlert)) return;
        const draftId = getVersionId(draftVersion);
        if (!draftId) {
            showAlert(noDraftReason, 'warning');
            return;
        }
        navigate(`/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=import&versionId=${encodeURIComponent(draftId)}`);
    });

    byId('rd-btn-save-set')?.addEventListener('click', async () => {
        if (notifyIfDisabled('rd-btn-save-set')) return;
        try {
            await saveSet();
        } catch (error) {
            if (error?.isHandled) return;
            showAlert(error?.message || (L.ErrorState || 'Could not save set metadata.'), 'error');
        }
    });

    byId('rd-btn-validate-draft')?.addEventListener('click', async () => {
        if (notifyIfDisabled('rd-btn-validate-draft')) return;
        try {
            await validateDraft();
        } catch (error) {
            if (error?.isHandled) return;
            showAlert(error?.message || 'Validation failed.', 'error');
        }
    });

    byId('rd-btn-submit-draft')?.addEventListener('click', async () => {
        if (notifyIfDisabled('rd-btn-submit-draft')) return;
        if (!permissions.guard('canSubmitVersion', showAlert)) return;
        try {
            openPublishReadiness();
        } catch (error) {
            if (error?.isHandled) return;
            showAlert(error?.message || 'Submit failed.', 'error');
        }
    });

    byId('rd-btn-publish-draft')?.addEventListener('click', async () => {
        if (notifyIfDisabled('rd-btn-publish-draft')) return;
        if (!permissions.can('canApproveVersion') && !permissions.can('canPublishVersion')) {
            showAlert(permissions.reason?.() || 'You do not have permission to perform this action.', 'warning');
            return;
        }
        try {
            openPublishReadiness();
        } catch (error) {
            if (error?.isHandled) return;
            showAlert(error?.message || 'Publish failed.', 'error');
        }
    });

    load().catch((error) => {
        if (error?.isHandled) return;
        hydrationFailed = true;
        renderSet();
        const message = isNotFoundError(error)
            ? 'Reference data set was not found or is not accessible for this tenant.'
            : 'Reference data set workspace is unavailable. Retry later or contact support with the server correlation trace.';
        showAlert(message, 'error');
    });
})();
