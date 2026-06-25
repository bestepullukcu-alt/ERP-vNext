/**
 * MOD-0029-FU01 - Controlled document details: metadata cards + version history + upload new version + share.
 * Consumes the same-origin proxy only. All text from window.L10n.
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const ctx = window.ControlledDocumentContext || {};
    const id = ctx.documentId;
    if (!id) return;

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const toast = (m, k) => window.showToast?.(m, k);
    const esc = (v) => (v === null || v === undefined ? '-' : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed, CONFLICT: L.ReasonConflict, PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound, STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable,
        FEATURE_DISABLED: L.ReasonFeatureDisabled
    }[code] || code);
    const row = (label, value) => `<div class="row mb-2"><div class="col-sm-4 text-muted">${esc(label)}</div><div class="col-sm-8">${value}</div></div>`;
    const handleError = (json) => {
        const corr = json?.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
        toast(`${reasonText(json?.reason_code) || L.ErrorOccurred}${corr}`, 'error');
    };

    const loadDetail = async () => {
        const res = await fetch(`/DocumentManagementControlledDocuments/detail/${id}`, { credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (!res.ok || json.isSuccessful === false) { handleError(json); return; }
        const d = json.data || json.Data;
        if (!d) return;
        document.getElementById('detailTitle').textContent = d.title || L.ViewDetails;
        document.getElementById('detailFolderPath').textContent = d.collectionPath || '';
        document.getElementById('detailIdentity').innerHTML =
            row(L.Title, esc(d.title)) + row(L.DocumentType, esc(d.documentType)) + row(L.FolderPath, esc(d.collectionPath)) + row(L.CurrentVersion, 'v' + esc(d.currentVersionNumber));
        document.getElementById('detailClassification').innerHTML =
            row(L.Description, esc(d.description)) + row(L.Tags, esc((d.tags || []).join(', '))) + row(L.Controlled, d.controlled ? '✔' : '✘');
        document.getElementById('detailLifecycle').innerHTML =
            row(L.EffectiveDate, esc(d.effectiveDate)) + row(L.ReviewDate, esc(d.reviewDate)) + row(L.ExpiryDate, esc(d.expiryDate));
    };

    const loadVersions = async () => {
        const res = await fetch(`/DocumentManagementControlledDocuments/versions/${id}`, { credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        const body = document.getElementById('versionHistoryBody');
        if (!res.ok || json.isSuccessful === false) { handleError(json); return; }
        const versions = json.data || json.Data || [];
        body.innerHTML = versions.map((v) => {
            const active = String(v.versionStatus).toUpperCase() === 'ACTIVE';
            const badge = `<span class="badge bg-label-${active ? 'success' : 'secondary'}">${esc(active ? L.VersionStatusActive : L.VersionStatusSuperseded)}</span>`;
            const dl = `<a class="btn btn-sm btn-text-secondary" href="/DocumentManagementControlledDocuments/download/${id}/${v.id}"><i class="bx bx-download"></i> ${esc(L.Download)}</a>`;
            return `<tr><td>v${esc(v.versionNumber)} ${badge}</td><td>${esc(v.file?.mediaType)}</td><td>${esc(v.uploadedBy)}</td><td>${esc((v.uploadedAt || '').slice(0, 10))}</td><td>${esc(v.changeSummary)}</td><td class="text-end">${dl}</td></tr>`;
        }).join('') || `<tr><td colspan="6" class="text-center text-muted">${esc(L.EmptyList)}</td></tr>`;
    };

    document.getElementById('btnUploadVersion')?.addEventListener('click', () => {
        document.getElementById('uploadVersionPanel')?.classList.toggle('d-none');
    });

    document.getElementById('btnSubmitVersion')?.addEventListener('click', async () => {
        const file = document.getElementById('versionFile')?.files?.[0];
        if (!file) { toast(L.FileRequired || 'File required', 'error'); return; }
        const fd = new FormData();
        fd.append('file', file);
        fd.append('changeSummary', document.getElementById('versionChangeSummary')?.value || '');
        fd.append('__RequestVerificationToken', token());
        const res = await fetch(`/DocumentManagementControlledDocuments/upload-version/${id}`, { method: 'POST', body: fd, credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (res.ok && json.isSuccessful !== false) {
            toast(L.VersionUploaded || 'Version uploaded', 'success');
            document.getElementById('uploadVersionPanel')?.classList.add('d-none');
            document.getElementById('versionFile').value = '';
            loadVersions(); loadDetail();
        } else { handleError(json); }
    });

    document.getElementById('btnShareDocument')?.addEventListener('click', async () => {
        const target = document.getElementById('shareTargetCompany')?.value?.trim();
        if (!target) { toast(L.ReasonValidationFailed || 'Invalid target', 'error'); return; }
        const fd = new FormData();
        fd.append('targetCompanyId', target);
        fd.append('shareMode', document.getElementById('shareMode')?.value || 'REFERENCE');
        fd.append('__RequestVerificationToken', token());
        const res = await fetch(`/DocumentManagementControlledDocuments/share/${id}`, { method: 'POST', body: fd, credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (res.ok && json.isSuccessful !== false) { toast(L.ShareSuccess || 'Shared', 'success'); }
        else { handleError(json); }
    });

    const actions = document.getElementById('detailActions');
    if (actions) {
        actions.innerHTML = `<a class="btn btn-label-secondary" href="/DocumentManagementControlledDocuments/Edit/${id}"><i class="bx bx-edit me-1"></i>${esc(L.EditMetadata)}</a>`;
    }

    loadDetail();
    loadVersions();
})();
