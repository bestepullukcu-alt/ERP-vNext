/**
 * MOD-0029-FU01 - Edit controlled document metadata (Compact). Loads current metadata into the shared form,
 * shows the immutable identity (company / folder / type) and the current version read-only, and submits a
 * metadata-only update via the same-origin proxy. Identity + file fields are immutable in edit mode.
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const ctx = window.ControlledDocumentContext || {};
    const id = ctx.documentId;
    const form = document.getElementById('controlledDocumentForm');
    if (!id || !form) return;

    const $ = (s) => document.getElementById(s);
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const toast = (m, k) => window.showToast?.(m, k);
    const esc = (v) => (v === null || v === undefined ? '-' : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed, CONFLICT: L.ReasonConflict, PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound, STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable
    }[code] || code);

    const get = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
    const unwrapList = (payload) => {
        const data = payload?.data || payload?.Data || payload;
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.items)) return data.items;
        if (Array.isArray(data?.Items)) return data.Items;
        return [];
    };

    const humanSize = (bytes) => {
        const n = Number(bytes);
        if (!Number.isFinite(n) || n <= 0) return '-';
        const units = ['B', 'KB', 'MB', 'GB'];
        let i = 0, v = n;
        while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
        return `${v.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
    };

    // Identity + file fields are immutable in edit mode (metadata-only update).
    ['fldCollectionInstanceId', 'fldCompanyId', 'fldDocumentType', 'fldFile'].forEach((f) => {
        const el = $(f);
        if (el) el.disabled = true;
    });

    // Inject a single selected option into an immutable select so its current value is shown.
    const setImmutableOption = (select, value, text) => {
        if (!select || !value) return;
        select.innerHTML = '';
        const opt = document.createElement('option');
        opt.value = value;
        opt.textContent = text || value;
        opt.selected = true;
        select.appendChild(opt);
        select.value = value;
    };

    // Resolve the company display name from the legal-entities lookup (detail only carries the GUID).
    const companyName = async (companyId) => {
        if (!companyId) return null;
        try {
            const res = await fetch('/DocumentManagementControlledDocuments/legal-entities', { credentials: 'same-origin' });
            const json = await res.json().catch(() => ({}));
            const match = unwrapList(json).find((x) =>
                String(get(x, 'legalEntityId', 'LegalEntityId') ?? get(x, 'id', 'Id')) === String(companyId));
            return match
                ? (get(match, 'displayName', 'DisplayName') || get(match, 'legalName', 'LegalName') || get(match, 'name', 'Name'))
                : null;
        } catch (_) { return null; }
    };

    // Replace the (immutable) file upload card body with the current version, read-only.
    const renderCurrentVersionCard = async (currentVersionNumber) => {
        const fileEl = $('fldFile');
        const host = fileEl?.closest('.col-12') || fileEl?.parentElement;
        if (!host) return;
        let info = '';
        try {
            const res = await fetch(`/DocumentManagementControlledDocuments/versions/${id}`, { credentials: 'same-origin' });
            const json = await res.json().catch(() => ({}));
            const versions = unwrapList(json);
            const activeRow = versions.find((v) => String(get(v, 'versionStatus', 'VersionStatus')).toUpperCase() === 'ACTIVE') || versions[0];
            if (activeRow) {
                const file = get(activeRow, 'file', 'File') || {};
                info = `<div class="d-flex flex-column gap-1">
                    <span class="fw-medium">v${esc(get(activeRow, 'versionNumber', 'VersionNumber'))} · ${esc(get(file, 'fileName', 'FileName'))}</span>
                    <small class="text-muted">${esc(humanSize(get(file, 'byteSize', 'ByteSize')))}</small>
                </div>`;
            }
        } catch (_) { /* fall through to a neutral note */ }
        if (!info) {
            info = `<span class="text-muted">v${esc(currentVersionNumber ?? '-')}</span>`;
        }
        host.innerHTML = `
            <label class="form-label fw-medium">${esc(L.CurrentVersion)}</label>
            ${info}
            <a href="/DocumentManagementControlledDocuments/VersionHistory/${id}" class="btn btn-sm btn-label-secondary mt-3">
                <i class="icon-base bx bx-history me-1"></i>${esc(L.VersionHistory)}
            </a>`;
    };

    (async () => {
        const res = await fetch(`/DocumentManagementControlledDocuments/detail/${id}`, { credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        const d = json.data || json.Data;
        if (!d) { toast(reasonText(json.reason_code) || L.ErrorOccurred, 'error'); return; }

        $('fldTitle').value = d.title || '';
        $('fldDescription').value = d.description || '';
        $('fldTags').value = (d.tags || []).join(', ');
        if (d.effectiveDate) $('fldEffectiveDate').value = String(d.effectiveDate).slice(0, 10);
        if (d.reviewDate) $('fldReviewDate').value = String(d.reviewDate).slice(0, 10);
        if (d.expiryDate) $('fldExpiryDate').value = String(d.expiryDate).slice(0, 10);

        // Document type (static options) — select the current value.
        if (d.documentType && $('fldDocumentType')) $('fldDocumentType').value = d.documentType;

        // Immutable identity — show the current company + folder as selected (read-only).
        setImmutableOption($('fldCollectionInstanceId'), d.collectionInstanceId, d.collectionPath);
        setImmutableOption($('fldCompanyId'), d.companyId, await companyName(d.companyId) || d.companyId);

        // Controlled switch reflects current value.
        const ctrl = $('fldControlled');
        if (ctrl) ctrl.checked = !!d.controlled;

        // Current version read-only (replaces the immutable file upload field).
        renderCurrentVersionCard(d.currentVersionNumber);
    })();

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const payload = {
            title: $('fldTitle')?.value?.trim(),
            description: $('fldDescription')?.value?.trim() || null,
            tags: ($('fldTags')?.value || '').split(',').map((t) => t.trim()).filter(Boolean),
            effectiveDate: $('fldEffectiveDate')?.value || null,
            reviewDate: $('fldReviewDate')?.value || null,
            expiryDate: $('fldExpiryDate')?.value || null
        };
        const fd = new FormData();
        fd.append('payloadJson', JSON.stringify(payload));
        fd.append('__RequestVerificationToken', token());
        const res = await fetch(`/DocumentManagementControlledDocuments/edit/${id}`, { method: 'POST', body: fd, credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (res.ok && json.isSuccessful !== false) {
            toast(L.RecordSaved || 'Saved', 'success');
            setTimeout(() => { window.location.href = `/DocumentManagementControlledDocuments/VersionHistory/${id}`; }, 600);
        } else {
            const corr = json.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
            toast(`${reasonText(json.reason_code) || L.ErrorOccurred}${corr}`, 'error');
        }
    });
})();