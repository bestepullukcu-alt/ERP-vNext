/**
 * MOD-0029-FU01 - Controlled document Details (golden compact read-only detail page).
 * Loads document metadata into Identity / Classification / Lifecycle cards (dl.row > dt/dd).
 * Version history lives on a dedicated page (/VersionHistory/{id}); this surface is metadata-only.
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const ctx = window.ControlledDocumentContext || {};
    const id = ctx.documentId;
    if (!id) return;

    const $ = (s) => document.getElementById(s);
    const toast = (m, k) => window.showToast?.(m, k);
    const esc = (v) => (v === null || v === undefined || v === '' ? '-' : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed, CONFLICT: L.ReasonConflict, PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound, STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable
    }[code] || code);

    // dl.row > dt/dd pair (golden compact detail card body).
    const pair = (label, value) =>
        `<dt class="col-12 fw-medium text-heading mb-1">${esc(label)}</dt><dd class="col-12 mb-4">${value}</dd>`;

    const docTypeLabel = (type) => ({
        SOP: L.TypeSop, WORK_INSTRUCTION: L.TypeWorkInstruction, POLICY: L.TypePolicy,
        FORM: L.TypeForm, TEMPLATE: L.TypeTemplate, OTHER: L.TypeOther
    }[String(type || '').toUpperCase()] || type);

    const statusBadge = (status) => {
        const active = String(status || '').toUpperCase() === 'ACTIVE';
        return `<span class="badge bg-label-${active ? 'success' : 'secondary'}">${esc(active ? L.StatusActive : L.StatusArchived)}</span>`;
    };

    const renderCards = (d, isTemplate) => {
        const titleEl = $('detailTitle');
        if (titleEl) titleEl.textContent = d.title || L.ViewDetails;

        $('detailIdentity').innerHTML =
            pair(L.Title, esc(d.title)) +
            pair(L.DocumentType, esc(isTemplate ? docTypeLabel('TEMPLATE') : docTypeLabel(d.documentType))) +
            pair(L.FolderPath, esc(d.collectionPath)) +
            pair(L.CurrentVersion, 'v' + esc(d.currentVersionNumber)) +
            pair(L.Status, statusBadge(d.status));

        $('detailClassification').innerHTML =
            pair(L.Description, esc(d.description)) +
            pair(L.Tags, esc((d.tags || []).join(', '))) +
            pair(L.Controlled, (!isTemplate && d.controlled)
                ? '<i class="icon-base bx bx-check text-success"></i>'
                : '<i class="icon-base bx bx-x text-muted"></i>');

        $('detailLifecycle').innerHTML =
            pair(L.EffectiveDate, esc(d.effectiveDate ? String(d.effectiveDate).slice(0, 10) : '')) +
            pair(L.ReviewDate, esc(d.reviewDate ? String(d.reviewDate).slice(0, 10) : '')) +
            pair(L.ExpiryDate, esc(d.expiryDate ? String(d.expiryDate).slice(0, 10) : ''));
    };

    const renderMasterRegisterMissing = () => {
        const state = $('masterRegisterState');
        if (!state) return;
        state.className = 'alert alert-secondary mt-3 mb-0';
        state.id = 'missing_link_message';
        state.textContent = L.NoLinkedMasterRegisterLegacyHint;
        $('masterRegisterDetails')?.classList.add('d-none');
        if ($('masterRegisterActions')) $('masterRegisterActions').innerHTML = '';
    };

    const loadMasterRegister = async (isTemplate) => {
        if (isTemplate || !ctx.canViewMasterRegister || !$('masterRegisterCard')) return;
        const state = $('masterRegisterState');
        try {
            const res = await fetch(`/DocumentManagementControlledDocuments/master-register/${encodeURIComponent(id)}`, {
                credentials: 'same-origin'
            });
            const json = await res.json().catch(() => ({}));
            if (res.status === 404) {
                renderMasterRegisterMissing();
                return;
            }
            if (res.status === 403) {
                state.className = 'alert alert-warning mt-3 mb-0';
                state.textContent = L.ReverseLookupForbidden;
                return;
            }
            if (!res.ok || json.isSuccessful === false) {
                state.className = 'alert alert-danger mt-3 mb-0';
                state.textContent = L.ReverseLookupFailed;
                return;
            }

            const mr = json.data || json.Data;
            if (!mr?.masterRegisterEntryId) {
                renderMasterRegisterMissing();
                return;
            }

            const compatibility = String(mr.linkCompatibilityStatus || '').toUpperCase();
            const compatible = compatibility === 'COMPATIBLE';
            state.className = `alert alert-${compatible ? 'success' : 'warning'} mt-3 mb-0`;
            state.textContent = compatible ? L.GovernedRegistrationComplete : L.ReadinessFailClosedDueToLink;

            const details = $('masterRegisterDetails');
            details.innerHTML =
                pair(L.MasterRegisterEntry, esc(mr.documentTitle)) +
                pair(L.MasterRegisterEntryId, esc(mr.masterRegisterEntryId)) +
                pair(L.DocumentScope, esc(mr.documentScope)) +
                pair(L.ScopeOwner, esc(mr.scopeOwnerId || mr.ownerCompanyId || mr.corporateOwnerId)) +
                pair(L.DocumentClass, esc(mr.documentClass)) +
                pair(L.DocumentType, esc(mr.documentType)) +
                pair(L.MasterRegisterStatus, esc(mr.registerStatus)) +
                pair(L.LifecycleStatus, esc(mr.lifecycleStatus)) +
                pair(L.MasterRegisterLinkStatus, esc(mr.linkCompatibilityStatus)) +
                pair(L.LinkedAt, esc(mr.linkedAt));
            details.classList.remove('d-none');

            if (compatible) {
                $('masterRegisterActions').innerHTML =
                    `<a class="btn btn-primary" id="open_master_register_route" href="/DocumentManagementMasterRegister/Details/${encodeURIComponent(mr.masterRegisterEntryId)}">
                        <i class="icon-base bx bx-link-external me-1"></i>${esc(L.OpenMasterRegister)}
                    </a>`;
            }
        } catch (_) {
            state.className = 'alert alert-danger mt-3 mb-0';
            state.textContent = L.ReverseLookupFailed;
        }
    };

    const load = async () => {
        // Try the controlled-document detail first; when the id belongs to a folder-attached template (e.g. a
        // MOD-0029-FU03 variant-linked template), that lookup 404s, so fall back to the template detail endpoint.
        let res = await fetch(`/DocumentManagementControlledDocuments/detail/${id}`, { credentials: 'same-origin' });
        let json = await res.json().catch(() => ({}));
        let isTemplate = false;

        if (!res.ok || json.isSuccessful === false) {
            const tres = await fetch(`/DocumentManagementControlledDocuments/templates/detail/${id}`, { credentials: 'same-origin' });
            const tjson = await tres.json().catch(() => ({}));
            if (tres.ok && tjson.isSuccessful !== false && (tjson.data || tjson.Data)) {
                json = tjson;
                isTemplate = true;
            } else {
                const corr = json?.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
                toast(`${reasonText(json?.reason_code) || L.ErrorOccurred}${corr}`, 'error');
                return;
            }
        }

        const d = json.data || json.Data;
        if (!d) return;
        renderCards(d, isTemplate);
        await loadMasterRegister(isTemplate);
    };

    load();
})();
