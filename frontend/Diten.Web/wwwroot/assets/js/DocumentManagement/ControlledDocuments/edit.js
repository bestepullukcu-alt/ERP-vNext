/**
 * MOD-0029-FU01 - Edit controlled document metadata (Compact). Loads current metadata into the shared form,
 * disables identity/file fields, and submits a metadata-only update via the same-origin proxy.
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const ctx = window.ControlledDocumentContext || {};
    const id = ctx.documentId;
    const form = document.getElementById('controlledDocumentForm');
    if (!id || !form) return;

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const toast = (m, k) => window.showToast?.(m, k);
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed, CONFLICT: L.ReasonConflict, PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound, STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable
    }[code] || code);

    // Identity + file fields are immutable in edit mode (metadata-only update).
    ['fldCollectionInstanceId', 'fldCompanyId', 'fldDocumentType', 'fldFile'].forEach((f) => {
        const el = document.getElementById(f);
        if (el) el.disabled = true;
    });

    (async () => {
        const res = await fetch(`/DocumentManagementControlledDocuments/detail/${id}`, { credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        const d = json.data || json.Data;
        if (!d) return;
        document.getElementById('fldTitle').value = d.title || '';
        document.getElementById('fldDescription').value = d.description || '';
        document.getElementById('fldTags').value = (d.tags || []).join(', ');
        if (d.effectiveDate) document.getElementById('fldEffectiveDate').value = String(d.effectiveDate).slice(0, 10);
        if (d.reviewDate) document.getElementById('fldReviewDate').value = String(d.reviewDate).slice(0, 10);
        if (d.expiryDate) document.getElementById('fldExpiryDate').value = String(d.expiryDate).slice(0, 10);
    })();

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const payload = {
            title: document.getElementById('fldTitle')?.value?.trim(),
            description: document.getElementById('fldDescription')?.value?.trim() || null,
            tags: (document.getElementById('fldTags')?.value || '').split(',').map((t) => t.trim()).filter(Boolean),
            effectiveDate: document.getElementById('fldEffectiveDate')?.value || null,
            reviewDate: document.getElementById('fldReviewDate')?.value || null,
            expiryDate: document.getElementById('fldExpiryDate')?.value || null
        };
        const fd = new FormData();
        fd.append('payloadJson', JSON.stringify(payload));
        fd.append('__RequestVerificationToken', token());
        const res = await fetch(`/DocumentManagementControlledDocuments/edit/${id}`, { method: 'POST', body: fd, credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (res.ok && json.isSuccessful !== false) {
            toast(L.RecordSaved || 'Saved', 'success');
            setTimeout(() => { window.location.href = `/DocumentManagementControlledDocuments/Details/${id}`; }, 600);
        } else {
            const corr = json.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
            toast(`${reasonText(json.reason_code) || L.ErrorOccurred}${corr}`, 'error');
        }
    });
})();
