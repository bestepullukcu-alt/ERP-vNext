'use strict';

(function () {
    const root = document.getElementById('rd-publish-review-page');
    if (!root) return;

    const versionId = root.dataset.versionId;
    const api = window.ReferenceDataApi;
    const L = window.L10n || {};
    // Evidence integration (MOD-0031 / evidence-links endpoint) is not deployed yet.
    // While disabled we skip the network call entirely so the browser does not log a 404.
    // Flip data-rd-evidence-enabled="true" on the page root once the endpoint is available.
    const evidenceIntegrationEnabled = root.dataset.rdEvidenceEnabled === 'true';
    const summaryEl = document.getElementById('rd-validation-summary');
    const checksEl = document.getElementById('rd-readiness-checks');
    const alertEl = document.getElementById('rd-review-alert');
    const historyTimeline = document.getElementById('rd-history-timeline');
    const badgeHost = document.getElementById('rd-review-badges');
    const validateBtn = document.getElementById('rd-review-validate');
    const submitBtn = document.getElementById('rd-review-submit');
    const approveBtn = document.getElementById('rd-review-approve');
    const publishBtn = document.getElementById('rd-review-publish');
    const publishLabel = document.getElementById('rd-review-publish-label');
    const defaultPublishText = publishLabel ? publishLabel.textContent : 'Publish';
    const overrideToggle = document.getElementById('rd-review-override');
    const overrideReasonInput = document.getElementById('rd-review-override-reason');
    const evidenceSelect = document.getElementById('rd-review-evidence-link');
    const evidenceStatusEl = document.getElementById('rd-review-evidence-status');
    const permissions = window.ReferenceDataPermissions || {
        can: () => true,
        apply: (element, _capability, stateAllowed = true, stateReason) => {
            if (!element) return false;
            element.disabled = !stateAllowed;
            if (stateAllowed) {
                element.removeAttribute('title');
            } else {
                element.setAttribute('title', stateReason || 'This action is unavailable for the current state.');
            }
            return stateAllowed;
        },
        guard: (_capability, element) => !element || !element.disabled
    };

    let version = null;
    let setInfo = null;
    let validation = null;
    let history = [];
    let evidenceLinks = [];
    let selectedEvidence = null;
    let evidenceEvaluation = null;
    let evidenceIntegrationAvailable = true;

    const text = (value) => value == null || String(value).trim() === '' ? '-' : String(value);
    const lower = (value) => String(value || '').toLowerCase();
    const fmt = (value) => {
        if (!value) return '-';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value);
        // Compact, locale-stable format: "Jun 05, 26 12:35 PM" (no seconds).
        const datePart = new Intl.DateTimeFormat('en-US', { month: 'short', day: '2-digit', year: '2-digit' }).format(date);
        const timePart = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', hour12: true }).format(date);
        return `${datePart} ${timePart}`;
    };
    const showAlert = (message, level) => {
        if (!alertEl) return;
        if (!message) {
            alertEl.className = 'alert alert-info d-none mb-3';
            alertEl.textContent = '';
            return;
        }
        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'info';
        alertEl.className = `alert alert-${css} mb-3`;
        alertEl.textContent = message;
        // The alert sits at the top of a long page; also surface a toast so the user sees
        // feedback near where they clicked (governance actions are bottom-right).
        if (typeof window.showToast === 'function') {
            window.showToast(message, level === 'error' ? 'error' : level === 'success' ? 'success' : 'info');
        }
    };
    const showPermissionAlert = (message) => showAlert(message, 'error');
    // Map known backend reason codes to readable messages (toasts otherwise show the raw code).
    const errorMessages = {
        sod_submitter_cannot_approve: 'You cannot approve a version you submitted. A different reviewer must approve it (separation of duties).',
        validation_blockers: 'There are blocking validation issues. Resolve them or enable Publish Override.',
        approval_required: 'Approval is required before this version can be published.',
        evidence_required: 'Canonical evidence is required for this action.',
        draft_required: 'This action is only available on a draft version.',
        override_reason_required: 'An override reason is required when Publish Override is enabled.',
        rejection_reason_required: 'A rejection reason is required to reject a version.',
        concurrency_conflict: 'This version was changed in another session. Refresh the page and try again.',
        already_published_different_idempotency: 'This version is already published.',
        workflow_start_failed: 'The approval workflow could not be started. Please try again.'
    };
    const friendlyError = (code) => {
        if (!code) return 'Action failed.';
        const key = String(code).trim();
        return errorMessages[key] || key;
    };
    const getInput = (id) => document.getElementById(id)?.value?.trim() || '';
    const getChecked = (id) => !!document.getElementById(id)?.checked;
    const retiredSetReason = permissions.retiredSetReason || 'This reference data set is retired. Changes are disabled.';
    const isRetiredSet = (set) => (typeof permissions.isRetiredSet === 'function'
        ? permissions.isRetiredSet(set)
        : lower(set?.status || set?.Status) === 'retired');
    const isReviewBlocked = () => (typeof permissions.isBlocked === 'function' && permissions.isBlocked()) || isRetiredSet(setInfo);
    const getBlockReason = () => (typeof permissions.blockReason === 'function' && permissions.blockReason()) || retiredSetReason;
    const applySetGate = () => {
        const retired = isRetiredSet(setInfo);
        if (typeof permissions.setGlobalBlock === 'function') {
            permissions.setGlobalBlock(retired, retiredSetReason);
        }
        if (retired) {
            showAlert(retiredSetReason, 'info');
        }
        return retired;
    };

    const setButtonState = (button, enabled, reason) => {
        if (!button) return;
        button.disabled = !enabled;
        if (enabled) {
            button.removeAttribute('title');
        } else {
            button.setAttribute('title', reason || 'Unavailable for current state.');
        }
    };

    const setControlDisabled = (element, disabled, reason) => {
        if (typeof permissions.setDisabled === 'function') {
            permissions.setDisabled(element, disabled, reason);
            return;
        }

        setButtonState(element, !disabled, reason);
    };

    const disableGovernanceControls = (reason) => {
        [
            validateBtn,
            submitBtn,
            approveBtn,
            publishBtn,
            overrideToggle,
            overrideReasonInput,
            document.getElementById('rd-review-evidence-ref'),
            evidenceSelect,
            document.getElementById('rd-review-publish-mode'),
            document.getElementById('rd-review-publish-at'),
            document.getElementById('rd-review-rejection-reason'),
            ...document.querySelectorAll('input[name="rd-review-decision"]')
        ].forEach((element) => setControlDisabled(element, true, reason));

        if (overrideToggle) {
            overrideToggle.checked = false;
        }
    };

    const readEvidenceField = (source, camel, pascal, snake) => source?.[camel] ?? source?.[pascal] ?? source?.[snake] ?? null;

    const persistedEvidence = () => ({
        evidence_link_id: readEvidenceField(version, 'evidenceLinkId', 'EvidenceLinkId', 'evidence_link_id'),
        document_version_id: readEvidenceField(version, 'evidenceDocumentVersionId', 'EvidenceDocumentVersionId', 'evidence_document_version_id'),
        requirement_code: readEvidenceField(version, 'evidenceRequirementCode', 'EvidenceRequirementCode', 'evidence_requirement_code')
    });

    const selectedEvidencePayload = () => selectedEvidence ? {
        evidence_link_id: String(readEvidenceField(selectedEvidence, 'evidenceLinkId', 'EvidenceLinkId', 'evidence_link_id') || ''),
        document_version_id: String(readEvidenceField(selectedEvidence, 'documentVersionId', 'DocumentVersionId', 'document_version_id') || ''),
        requirement_code: String(readEvidenceField(selectedEvidence, 'requirementCode', 'RequirementCode', 'requirement_code') || '')
    } : null;

    const hasPayload = (payload) => !!payload
        && !!String(payload.evidence_link_id || '').trim()
        && !!String(payload.document_version_id || '').trim()
        && !!String(payload.requirement_code || '').trim();

    const hasPersistedCanonicalEvidence = () => hasPayload(persistedEvidence());
    const hasSelectedCanonicalEvidence = () => hasPayload(selectedEvidencePayload());
    const hasCanonicalEvidence = () => hasPersistedCanonicalEvidence() || hasSelectedCanonicalEvidence();

    // Canonical evidence is only enforced when the evidence integration is reachable AND
    // exposes at least one link for this version. When the integration is unavailable or
    // returns no data, the evidence gate is treated as optional so the governance flow is
    // not blocked by a dependency that cannot be satisfied. Mirrors the backend stub
    // adapter, which also stops blocking when no evidence can be attached.
    const isEvidenceRequired = () => evidenceIntegrationAvailable && evidenceLinks.length > 0;

    const canonicalEvidencePayload = () => {
        const selected = selectedEvidencePayload();
        if (hasPayload(selected)) return selected;
        const persisted = persistedEvidence();
        return hasPayload(persisted) ? persisted : {};
    };

    const formatEvidenceSummary = () => {
        const payload = persistedEvidence();
        if (!hasPayload(payload)) return '-';
        const decision = version?.evidenceDecisionCode || version?.EvidenceDecisionCode || 'pending';
        const reason = version?.evidenceReasonCode || version?.EvidenceReasonCode || 'not evaluated';
        return `${payload.requirement_code} / ${decision} / ${reason}`;
    };

    const renderEvidenceOptions = () => {
        if (!evidenceSelect) return;
        const persisted = persistedEvidence();
        const persistedId = persisted.evidence_link_id;
        evidenceSelect.innerHTML = '<option value="">Select canonical evidence</option>' + evidenceLinks.map((link) => {
            const id = readEvidenceField(link, 'evidenceLinkId', 'EvidenceLinkId', 'evidence_link_id');
            const req = readEvidenceField(link, 'requirementCode', 'RequirementCode', 'requirement_code') || 'requirement';
            const doc = readEvidenceField(link, 'documentVersionId', 'DocumentVersionId', 'document_version_id') || 'document';
            return `<option value="${text(id)}">${text(req)} / ${text(doc)}</option>`;
        }).join('');
        if (persistedId) {
            evidenceSelect.value = String(persistedId);
        }
        selectedEvidence = evidenceLinks.find((link) => String(readEvidenceField(link, 'evidenceLinkId', 'EvidenceLinkId', 'evidence_link_id')) === evidenceSelect.value) || null;
        // Refresh the select2 widget (if active) without firing the plain 'change' handler.
        if (window.jQuery && window.jQuery.fn && window.jQuery.fn.select2 && window.jQuery(evidenceSelect).data('select2')) {
            window.jQuery(evidenceSelect).trigger('change.select2');
        }
    };

    const renderEvidenceStatus = () => {
        if (!evidenceStatusEl) return;
        if (!evidenceIntegrationAvailable) {
            evidenceStatusEl.textContent = 'Evidence integration unavailable.';
            evidenceSelect && setControlDisabled(evidenceSelect, true, 'Evidence integration unavailable.');
            return;
        }
        const payload = canonicalEvidencePayload();
        const decision = version?.evidenceDecisionCode || version?.EvidenceDecisionCode || evidenceEvaluation?.overallStatus || evidenceEvaluation?.OverallStatus || evidenceEvaluation?.overall_status || 'not evaluated';
        const reason = version?.evidenceReasonCode || version?.EvidenceReasonCode || 'awaiting canonical validation';
        const verifiedAt = readEvidenceField(selectedEvidence, 'lastVerifiedAt', 'LastVerifiedAt', 'last_verified_at');
        const linkStatus = readEvidenceField(selectedEvidence, 'evidenceStatus', 'EvidenceStatus', 'evidence_status');
        const artifact = reason === 'MOD0028_ARTIFACT_USABLE'
            || (verifiedAt && String(linkStatus || '').toLowerCase() === 'linked')
            ? 'usable'
            : reason;
        const missing = evidenceEvaluation?.missingRequirements || evidenceEvaluation?.MissingRequirements || evidenceEvaluation?.missing_requirements || [];
        evidenceStatusEl.textContent = hasPayload(payload)
            ? `Requirement: ${payload.requirement_code}; evaluation: ${decision}; artifact: ${artifact}; missing: ${missing.length ? missing.join(', ') : 'none'}`
            : 'Missing canonical evidence link.';
    };

    const evaluateActionGate = () => {
        if (!version) return;

        if (isReviewBlocked()) {
            disableGovernanceControls(getBlockReason());
            return {
                canValidate: false,
                canSubmit: false,
                canApprove: false,
                canPublish: false
            };
        }

        const overrideAllowed = permissions.apply(
            overrideToggle,
            'canPublishOverride',
            true,
            'Publish Override requires approval for override actions.'
        );
        permissions.apply(
            overrideReasonInput,
            'canPublishOverride',
            overrideAllowed && getChecked('rd-review-override'),
            overrideAllowed ? 'Enable Publish Override to enter an override reason.' : 'Publish Override requires approval for override actions.'
        );
        if (!overrideAllowed && overrideToggle) {
            overrideToggle.checked = false;
        }

        const state = {
            status: lower(version.status || version.Status),
            governance: lower(version.businessReferenceDataGovernanceState || version.BusinessReferenceDataGovernanceState || version.governanceState || version.GovernanceState),
            approval: lower(version.businessReferenceDataApprovalState || version.BusinessReferenceDataApprovalState || version.approvalState || version.ApprovalState),
            editable: Boolean(version.isEditable ?? version.IsEditable),
            immutable: Boolean(version.isImmutable ?? version.IsImmutable),
            overrideAction: getChecked('rd-review-override'),
            overrideReason: getInput('rd-review-override-reason'),
            evidence: hasCanonicalEvidence() ? 'canonical' : '',
            persistedEvidence: hasPersistedCanonicalEvidence() ? 'canonical' : '',
            blockingCount: Number(validation?.blockingErrorCount ?? validation?.BlockingErrorCount ?? 0),
            publishMode: document.getElementById('rd-review-publish-mode')?.value || 'Immediate',
            publishAt: getInput('rd-review-publish-at')
        };
        state.overrideAction = state.overrideAction && overrideAllowed;

        const evidenceRequired = isEvidenceRequired();
        const hasEvidence = state.evidence.length > 0;
        const hasPublishEvidence = state.persistedEvidence.length > 0;
        const hasValidationBlocker = state.blockingCount > 0;
        const isDraft = state.status === 'draft';
        const isPublished = state.status === 'published' || state.immutable;
        const pendingApproval = state.approval === 'pending' || state.governance === 'submitted' || state.governance === 'inreview';
        const approved = state.approval === 'approved' || state.governance === 'approved';
        const requiresFutureDate = lower(state.publishMode) === 'futuredated';
        const futureDateReady = !requiresFutureDate || state.publishAt.length > 0;
        const evidenceReady = !evidenceRequired || hasEvidence || state.overrideAction;
        const publishEvidenceReady = !evidenceRequired || hasPublishEvidence || state.overrideAction;
        const approvalReady = approved || state.overrideAction;
        const validationReady = !hasValidationBlocker || state.overrideAction;
        const overrideReady = !state.overrideAction || state.overrideReason.length > 0;

        const canValidateState = isDraft && !isPublished;
        const canSubmitState = isDraft && !isPublished && evidenceReady && validationReady && overrideReady;
        const canApproveState = !isPublished && pendingApproval && evidenceReady && overrideReady;
        const canPublishState = !isPublished && isDraft && validationReady && approvalReady && publishEvidenceReady && futureDateReady && overrideReady;

        if (publishLabel) {
            publishLabel.textContent = state.overrideAction ? 'Publish Override' : defaultPublishText;
        }

        const validateStateReason = 'Validate is available only for editable draft versions.';
        const submitStateReason = !overrideReady ? 'Override reason is required for override actions.' : hasValidationBlocker ? 'Resolve blocking validation issues or use override.' : 'Submit requires draft and evidence readiness.';
        const approveStateReason = 'Approve is available after submit/in-review and requires evidence.';
        const publishStateReason = !overrideReady
            ? 'Override reason is required for Publish Override.'
            : requiresFutureDate && !futureDateReady
            ? 'Future-dated publish requires Publish At.'
            : 'Publish requires draft readiness, approval, and persisted canonical evidence.';

        const canValidate = permissions.apply(validateBtn, 'canValidateVersion', canValidateState, validateStateReason);
        const canSubmit = permissions.apply(submitBtn, 'canSubmitVersion', canSubmitState, submitStateReason);
        const canApprove = permissions.apply(approveBtn, 'canApproveVersion', canApproveState, approveStateReason);
        const canPublish = permissions.apply(publishBtn, 'canPublishVersion', canPublishState, publishStateReason);

        const hintEl = document.getElementById('rd-governance-hint');
        if (hintEl) {
            let hint = '';
            if (isPublished) {
                hint = 'This version is published and read-only.';
            } else if (!isDraft) {
                hint = `Validate and Submit apply only to draft versions (current status: ${text(version.status || version.Status)}). Use Approve / Reject for a version awaiting approval.`;
            } else if (!canValidate && !canSubmit) {
                hint = 'You do not have permission to perform these actions.';
            } else if (hasValidationBlocker && !state.overrideAction) {
                hint = 'Resolve the blocking validation issues, or enable Publish Override.';
            }
            hintEl.textContent = hint;
            hintEl.classList.toggle('d-none', hint.length === 0);
        }

        return {
            canValidate,
            canSubmit,
            canApprove,
            canPublish
        };
    };

    const setText = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.textContent = text(value);
    };

    const renderVersionHeader = () => {
        if (!version) return;
        const versionNumber = version.versionNumber || version.VersionNumber;
        const status = version.status || version.Status;
        const governance = version.businessReferenceDataGovernanceState || version.BusinessReferenceDataGovernanceState || version.governanceState || version.GovernanceState;
        const approval = version.businessReferenceDataApprovalState || version.BusinessReferenceDataApprovalState || version.approvalState || version.ApprovalState;
        const immutable = version.isImmutable || version.IsImmutable;
        const editable = version.isEditable || version.IsEditable;

        badgeHost.innerHTML = `
            <span class="badge bg-label-primary me-1">Version: ${text(versionNumber)}</span>
            <span class="badge ${lower(status) === 'published' ? 'bg-label-success' : 'bg-label-warning'} me-1">Status: ${text(status)}</span>
            <span class="badge bg-label-info me-1">Governance: ${text(governance)}</span>
            <span class="badge bg-label-secondary me-1">Approval: ${text(approval)}</span>
            <span class="badge ${immutable ? 'bg-label-success' : 'bg-label-warning'} me-1">Immutable: ${immutable ? 'Yes' : 'No'}</span>
            <span class="badge ${editable ? 'bg-label-success' : 'bg-label-secondary'}">Editable: ${editable ? 'Yes' : 'No'}</span>
        `;

        setText('rd-review-version', `v${text(versionNumber)} (${text(status)})`);
        setText('rd-review-set', `${text(setInfo?.setCode || setInfo?.SetCode)} / ${text(setInfo?.name || setInfo?.Name)}`);
        setText('rd-review-submitted', fmt(version.submittedAt || version.SubmittedAt));
        setText('rd-review-published', fmt(version.publishedAt || version.PublishedAt));
        setText('rd-review-evidence-current', formatEvidenceSummary());

        renderEvidenceStatus();
    };

    const historyEmptyMessage = () => L.NoVersionHistory || 'No version history available for this set.';
    const historyPointClass = (status) => {
        const s = lower(status);
        if (s === 'published') return 'timeline-point-success';
        if (s === 'draft') return 'timeline-point-warning';
        if (s === 'retired' || s === 'deprecated') return 'timeline-point-secondary';
        return 'timeline-point-info';
    };
    const historyBadgeCss = (status) => {
        const s = lower(status);
        if (s === 'published') return 'bg-label-success';
        if (s === 'draft') return 'bg-label-warning';
        return 'bg-label-secondary';
    };
    const renderHistory = () => {
        if (!historyTimeline) return;
        if (!history.length) {
            historyTimeline.innerHTML = `<div class="text-center text-muted py-4">${historyEmptyMessage()}</div>`;
            return;
        }
        const approvalLabel = L.ApprovalColumn || 'Approval';
        const publishedLabel = L.PublishedLabel || 'Published';
        const evidenceLabel = L.EvidenceLabel || 'Evidence';
        historyTimeline.innerHTML = `<ul class="timeline timeline-outline mb-0">
            ${history.map((item) => {
                const status = item.status || item.Status;
                const versionNumber = text(item.versionNumber || item.VersionNumber);
                const published = fmt(item.publishedAt || item.PublishedAt);
                const approval = text(item.businessReferenceDataApprovalState || item.BusinessReferenceDataApprovalState || item.approvalState || item.ApprovalState);
                const evidence = text(item.lastEvidenceRef || item.LastEvidenceRef);
                return `<li class="timeline-item timeline-item-transparent border-dashed">
                    <span class="timeline-point ${historyPointClass(status)}"></span>
                    <div class="timeline-event">
                        <div class="timeline-header mb-3">
                            <h6 class="mb-0"><span class="badge ${historyBadgeCss(status)}">${text(status)}</span><span class="ms-2">v${versionNumber}</span></h6>
                            <small class="text-body-secondary">${published}</small>
                        </div>
                        <div class="row g-2">
                            <div class="col-12 col-sm-6">
                                <small class="text-muted d-block">${approvalLabel}</small>
                                <span>${approval}</span>
                            </div>
                            <div class="col-12 col-sm-6">
                                <small class="text-muted d-block">${publishedLabel}</small>
                                <span>${published}</span>
                            </div>
                            <div class="col-12">
                                <small class="text-muted d-block">${evidenceLabel}</small>
                                <span>${evidence}</span>
                            </div>
                        </div>
                    </div>
                </li>`;
            }).join('')}
        </ul>`;
    };

    const renderReadiness = (usageSummary) => {
        const finalizedStatus = lower(version?.status || version?.Status);
        const alreadyPublished = finalizedStatus === 'published' || finalizedStatus === 'deprecated' || finalizedStatus === 'retired'
            || Boolean(version?.isImmutable ?? version?.IsImmutable);
        // Once a version is published (immutable), the draft-readiness validation — e.g. RDV-002 "version must be
        // in draft status" — no longer applies, so surfacing it as a publish blocker is misleading. Treat the
        // readiness panel as complete for finalized versions instead of showing phantom blockers.
        const rawBlockers = validation?.publishBlockers || validation?.PublishBlockers || [];
        const blockers = alreadyPublished ? [] : rawBlockers;
        const blockingCount = alreadyPublished ? 0 : Number(validation?.blockingErrorCount ?? validation?.BlockingErrorCount ?? 0);
        const warningCount = Number(validation?.warningCount ?? validation?.WarningCount ?? 0);
        const infoCount = Number(validation?.infoCount ?? validation?.InfoCount ?? 0);
        const approvalState = lower(version?.businessReferenceDataApprovalState || version?.BusinessReferenceDataApprovalState || version?.approvalState || version?.ApprovalState);
        const needsApproval = approvalState !== 'approved';
        const evidenceRequired = isEvidenceRequired();
        const requiresEvidence = evidenceRequired && !hasCanonicalEvidence();
        const persistedEvidenceMissing = evidenceRequired && !hasPersistedCanonicalEvidence();
        const overrideEnabled = getChecked('rd-review-override');
        const overrideReasonMissing = overrideEnabled && !getInput('rd-review-override-reason');
        const totalDeps = Number(usageSummary?.totalRegistrations ?? usageSummary?.TotalRegistrations ?? 0);
        const criticalDeps = Number(usageSummary?.criticalRegistrations ?? usageSummary?.CriticalRegistrations ?? 0);

        const evidenceChip = !evidenceRequired
            ? '<span class="badge bg-label-secondary">Evidence: Optional</span>'
            : requiresEvidence
                ? '<span class="badge bg-label-danger">Evidence: Missing</span>'
                : '<span class="badge bg-label-success">Evidence: Satisfied</span>';

        const stat = (value, label, css) =>
            `<div class="text-center"><div class="fw-bold fs-4 lh-1 text-${css}">${value}</div><div class="text-muted small">${label}</div></div>`;

        summaryEl.innerHTML = `
            <div class="d-flex flex-wrap align-items-center gap-4 mb-3">
                ${stat(blockingCount, 'Blocking', blockingCount > 0 ? 'danger' : 'success')}
                ${stat(warningCount, 'Warnings', warningCount > 0 ? 'warning' : 'body')}
                ${stat(infoCount, 'Info', 'info')}
                ${stat(criticalDeps, 'Critical deps', criticalDeps > 0 ? 'warning' : 'body')}
            </div>
            <div class="d-flex flex-wrap gap-2">
                <span class="badge ${needsApproval ? 'bg-label-warning' : 'bg-label-success'}">Approval: ${needsApproval ? 'Pending' : 'Approved'}</span>
                ${evidenceChip}
                <span class="badge ${overrideEnabled ? (overrideReasonMissing ? 'bg-label-danger' : 'bg-label-warning') : 'bg-label-secondary'}">Override: ${overrideEnabled ? (overrideReasonMissing ? 'Reason required' : 'Enabled') : 'Off'}</span>
            </div>
        `;

        const checks = [];
        const addCheck = (text, status) => checks.push({ text, status });
        if (typeof permissions.isBlocked === 'function' && permissions.isBlocked()) {
            addCheck(permissions.blockReason?.() || retiredSetReason, 'danger');
        }
        if (alreadyPublished) {
            addCheck('This version is already published and read-only; publish readiness checks no longer apply.', 'success');
        } else {
            addCheck(blockingCount > 0 ? `Validation has ${blockingCount} blocking issue(s).` : 'Validation blockers cleared.', blockingCount > 0 ? 'danger' : 'success');
        }
        addCheck(needsApproval ? 'Approval decision is still required before publish.' : 'Approval state is ready.', needsApproval ? 'warning' : 'success');
        if (!evidenceRequired) {
            addCheck('Canonical evidence integration unavailable or empty; evidence requirement skipped.', 'info');
        } else {
            addCheck(requiresEvidence ? 'Canonical evidence link is required by governance checks.' : 'Canonical evidence link is selected or persisted.', requiresEvidence ? 'danger' : 'success');
            if (persistedEvidenceMissing) addCheck('Publish requires evidence persisted by submit or approval validation.', 'warning');
        }
        if (overrideReasonMissing) addCheck('Publish Override requires an override reason.', 'danger');
        addCheck(totalDeps > 0 ? `Usage impact: ${totalDeps} downstream registration(s).` : 'No downstream usage registration found.', 'info');
        if (blockers.length) {
            blockers.forEach((b) => addCheck(`Publish blocker: ${b}`, 'danger'));
        }

        const checkIcon = {
            success: 'bx-check-circle text-success',
            warning: 'bx-error-circle text-warning',
            danger: 'bx-x-circle text-danger',
            info: 'bx-info-circle text-info'
        };
        checksEl.innerHTML = checks.map((c) => `
            <li class="list-group-item d-flex align-items-start gap-2 px-0 py-2 bg-transparent border-0 border-bottom">
                <i class="bx ${checkIcon[c.status] || checkIcon.info} fs-5 lh-1 mt-1"></i>
                <span class="small">${c.text}</span>
            </li>`).join('');

        // Reflect overall readiness as the tip-card (alert) variant + icon.
        const readinessAlert = document.getElementById('rd-readiness-alert');
        const readinessIcon = document.getElementById('rd-readiness-icon');
        if (readinessAlert) {
            const blocked = (typeof permissions.isBlocked === 'function' && permissions.isBlocked())
                || blockingCount > 0 || (evidenceRequired && requiresEvidence) || overrideReasonMissing || blockers.length > 0;
            const warn = needsApproval || warningCount > 0;
            const variant = blocked ? 'danger' : warn ? 'warning' : 'success';
            readinessAlert.className = `alert mb-4 alert-${variant}`;
            if (readinessIcon) {
                const iconName = blocked ? 'bx-error' : warn ? 'bx-error-circle' : 'bx-check-circle';
                readinessIcon.className = `icon-base bx ${iconName}`;
            }
        }
    };

    const renderImpact = (impactSummary) => {
        const setKpi = (id, value) => {
            const el = document.getElementById(id);
            if (el) el.textContent = value;
        };
        setKpi('rd-impact-total', Number(impactSummary?.totalRegistrations ?? impactSummary?.TotalRegistrations ?? 0));
        setKpi('rd-impact-critical', Number(impactSummary?.criticalRegistrations ?? impactSummary?.CriticalRegistrations ?? 0));
        setKpi('rd-impact-high', Number(impactSummary?.highRegistrations ?? impactSummary?.HighRegistrations ?? 0));
        setKpi('rd-impact-medium', Number(impactSummary?.mediumRegistrations ?? impactSummary?.MediumRegistrations ?? 0));
        setKpi('rd-impact-low', Number(impactSummary?.lowRegistrations ?? impactSummary?.LowRegistrations ?? 0));
    };

    const usageEmptyMessage = 'No usage registrations for this set.';
    const renderDependencies = (items) => {
        const body = document.getElementById('rd-dependencies-body');
        if (!body) return;
        if (!items || !items.length) {
            body.innerHTML = `<tr><td colspan="3" class="text-center text-muted py-3">${usageEmptyMessage}</td></tr>`;
            return;
        }
        body.innerHTML = items.map((it) => {
            const crit = lower(it.criticality || it.Criticality);
            const critCss = crit === 'critical' ? 'bg-label-danger'
                : crit === 'high' ? 'bg-label-warning'
                : crit === 'medium' ? 'bg-label-info'
                : 'bg-label-secondary';
            return `<tr>
                <td>${text(it.consumerModule || it.ConsumerModule)}</td>
                <td>${text(it.consumerName || it.ConsumerName)}</td>
                <td><span class="badge ${critCss}">${text(it.criticality || it.Criticality)}</span></td>
            </tr>`;
        }).join('');
    };

    const loadEvidenceLinks = async () => {
        evidenceLinks = [];
        selectedEvidence = null;
        evidenceEvaluation = null;
        evidenceIntegrationAvailable = true;
        if (!evidenceIntegrationEnabled) {
            evidenceIntegrationAvailable = false;
            renderEvidenceOptions();
            renderEvidenceStatus();
            return;
        }
        try {
            const query = new URLSearchParams();
            query.set('module_id', 'PSS-012');
            query.set('object_type', 'reference_data_version');
            query.set('object_id', versionId);
            query.set('object_version', String(version?.versionNumber || version?.VersionNumber || version?.version || version?.Version || '1'));
            query.set('limit', '50');
            const data = await api.getEvidenceLinks(`?${query.toString()}`);
            if (data == null) {
                evidenceIntegrationAvailable = false;
                renderEvidenceOptions();
                renderEvidenceStatus();
                return;
            }
            evidenceLinks = data?.items || data?.Items || [];
            renderEvidenceOptions();
            await evaluateSelectedEvidence();
        } catch (error) {
            console.warn('BusinessReferenceData publish review: evidence link refresh failed', error);
            evidenceIntegrationAvailable = false;
            renderEvidenceOptions();
            renderEvidenceStatus();
        }
    };

    const evaluateSelectedEvidence = async () => {
        if (!evidenceIntegrationEnabled) {
            renderEvidenceStatus();
            return;
        }
        const payload = canonicalEvidencePayload();
        if (!hasPayload(payload) || !api.evaluateEvidenceRequirements) {
            renderEvidenceStatus();
            return;
        }

        try {
            evidenceEvaluation = await api.evaluateEvidenceRequirements({
                tenant_id: version?.tenantId || version?.TenantId || null,
                source_module_id: 'PSS-012',
                source_object_type: 'reference_data_version',
                source_object_id: versionId,
                source_object_version: String(version?.versionNumber || version?.VersionNumber || '1'),
                requirement_codes: [payload.requirement_code],
                action_code: 'reference_data.submit',
                evaluation_context: {
                    evidence_link_id: payload.evidence_link_id,
                    document_version_id: payload.document_version_id
                }
            });
        } catch (error) {
            console.warn('BusinessReferenceData publish review: evidence requirement evaluation failed', error);
            evidenceEvaluation = null;
        }
        renderEvidenceStatus();
    };

    const refresh = async () => {
        showAlert(null);
        version = await api.getVersion(versionId);
        const setId = version.setId || version.SetId;
        setInfo = setId ? await api.getSet(setId) : null;
        const retired = applySetGate();
        await loadEvidenceLinks();

        let usagePayload = { impactSummary: null };
        if (setInfo?.setCode || setInfo?.SetCode) {
            try {
                usagePayload = await api.getUsageRegistrations(setInfo.setCode || setInfo.SetCode);
            } catch (error) {
                console.warn('BusinessReferenceData publish review: usage registration refresh failed', error);
                usagePayload = { impactSummary: null };
            }
        }

        const [historyPayload, validationPayload, usageResult] = await Promise.all([
            setId ? api.getSetVersions(setId) : Promise.resolve({ items: [] }),
            retired
                ? Promise.resolve({ blockingErrorCount: 0, warningCount: 0, infoCount: 0, publishBlockers: [] })
                : api.validateVersion(versionId),
            Promise.resolve(usagePayload)
        ]);

        history = historyPayload?.items || historyPayload?.Items || [];
        validation = validationPayload;

        renderVersionHeader();
        renderHistory();
        renderImpact(usageResult?.impactSummary || usageResult?.ImpactSummary);
        renderDependencies(usageResult?.items || usageResult?.Items || []);
        renderReadiness(usageResult?.impactSummary || usageResult?.ImpactSummary);
        evaluateActionGate();
    };

    const withAction = async (fn, successMessage) => {
        try {
            if (isReviewBlocked()) {
                showAlert(getBlockReason(), 'error');
                return;
            }

            await fn();
            showAlert(successMessage, 'success');
            try {
                await refresh();
            } catch (refreshError) {
                console.warn('BusinessReferenceData publish review: post-action refresh failed', refreshError);
                showAlert('Action succeeded, but the page could not refresh. Please click Refresh.', 'warning');
            }
        } catch (error) {
            if (error?.isHandled) return;
            showAlert(friendlyError(error?.message), 'error');
        }
    };

    validateBtn?.addEventListener('click', async () => {
        if (isReviewBlocked() || validateBtn.disabled || !permissions.guard('canValidateVersion', showPermissionAlert)) return;
        await withAction(async () => {
            validation = await api.validateVersion(versionId);
        }, 'Validation completed.');
    });

    submitBtn?.addEventListener('click', async () => {
        if (isReviewBlocked() || submitBtn.disabled || !permissions.guard('canSubmitVersion', showPermissionAlert)) return;
        await withAction(async () => {
            await api.submitVersion(versionId, {
                expected_concurrency_token: version?.concurrencyToken || version?.ConcurrencyToken || null,
                evidence_ref: null,
                ...canonicalEvidencePayload(),
                override_action: getChecked('rd-review-override'),
                override_reason: getInput('rd-review-override-reason') || null
            });
        }, 'Version submitted for review.');
    });

    approveBtn?.addEventListener('click', async () => {
        if (isReviewBlocked() || approveBtn.disabled || !permissions.guard('canApproveVersion', showPermissionAlert)) return;
        await withAction(async () => {
            const decision = document.querySelector('input[name="rd-review-decision"]:checked')?.value || 'approve';
            await api.approveVersion(versionId, {
                decision,
                expected_concurrency_token: version?.concurrencyToken || version?.ConcurrencyToken || null,
                rejection_reason: getInput('rd-review-rejection-reason') || null,
                evidence_ref: null,
                ...canonicalEvidencePayload(),
                override_action: getChecked('rd-review-override'),
                override_reason: getInput('rd-review-override-reason') || null
            });
        }, 'Approval decision submitted.');
    });

    publishBtn?.addEventListener('click', async () => {
        if (isReviewBlocked() || publishBtn.disabled || !permissions.guard('canPublishVersion', showPermissionAlert)) return;
        await withAction(async () => {
            const publishMode = document.getElementById('rd-review-publish-mode')?.value || 'Immediate';
            const publishAtRaw = getInput('rd-review-publish-at');
            const publishAt = publishAtRaw ? new Date(publishAtRaw).toISOString() : null;
            const key = `idem-${Date.now()}`;
            const overrideAction = getChecked('rd-review-override');
            const payload = {
                publish_mode: publishMode,
                publish_at: publishAt,
                expected_concurrency_token: version?.concurrencyToken || version?.ConcurrencyToken || null
            };

            if (overrideAction) {
                payload.override_reason = getInput('rd-review-override-reason') || null;
                await api.publishVersionOverride(versionId, payload, key);
                return;
            }

            await api.publishVersion(versionId, payload, key);
        }, 'Publish action completed.');
    });

    ['rd-review-evidence-ref', 'rd-review-override-reason', 'rd-review-publish-at', 'rd-review-publish-mode'].forEach((id) => {
        document.getElementById(id)?.addEventListener('input', evaluateActionGate);
        document.getElementById(id)?.addEventListener('change', evaluateActionGate);
    });
    evidenceSelect?.addEventListener('change', async () => {
        selectedEvidence = evidenceLinks.find((link) => String(readEvidenceField(link, 'evidenceLinkId', 'EvidenceLinkId', 'evidence_link_id')) === evidenceSelect.value) || null;
        await evaluateSelectedEvidence();
        evaluateActionGate();
        renderReadiness(null);
    });
    document.getElementById('rd-review-override')?.addEventListener('change', evaluateActionGate);

    refresh().catch((error) => {
        if (error?.isHandled) return;
        showAlert(error?.message || 'Could not load governance review workspace.', 'error');
    });
})();
