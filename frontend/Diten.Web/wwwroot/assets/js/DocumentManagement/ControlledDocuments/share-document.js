/**
 * MOD-0029-FU01 - Controlled document share page.
 * Uses the existing same-origin share proxy for approved company sharing. Link/email helpers share the internal
 * route only; access is still enforced by backend permissions.
 */
'use strict';

(function () {
    const ctx = window.ControlledDocumentShareContext || {};
    const L = window.L10n || {};
    const id = ctx.documentId;
    if (!id) return;

    const $ = (selector) => document.getElementById(selector);
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const text = (value, fallback) => value === null || value === undefined || value === '' ? (fallback || '-') : String(value);
    const toast = (message, kind) => window.showToast?.(message, kind);
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed,
        CONFLICT: L.ReasonConflict,
        PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound,
        STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable,
        FEATURE_DISABLED: L.ReasonFeatureDisabled
    }[code] || code);

    const handleError = (json) => {
        const corr = json?.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
        toast(`${reasonText(json?.reason_code) || L.ErrorOccurred}${corr}`, 'error');
    };

    const detailUrl = () => `${window.location.origin}/DocumentManagementControlledDocuments/VersionHistory/${id}`;

    const loadDetail = async () => {
        const response = await fetch(`/DocumentManagementControlledDocuments/detail/${id}`, { credentials: 'same-origin' });
        const json = await response.json().catch(() => ({}));
        if (!response.ok || json.isSuccessful === false) { handleError(json); return; }

        const data = json.data || json.Data || {};
        $('shareDocumentTitle').textContent = text(data.title || data.Title);
        $('shareDocumentPath').textContent = text(data.collectionPath || data.CollectionPath);
        $('shareDocumentVersion').textContent = data.currentVersionNumber || data.CurrentVersionNumber
            ? `v${data.currentVersionNumber || data.CurrentVersionNumber}`
            : '-';
        $('shareInternalLink').value = detailUrl();
    };

    const copyLink = async () => {
        const value = $('shareInternalLink')?.value || detailUrl();
        try {
            await navigator.clipboard.writeText(value);
            toast(L.LinkCopied, 'success');
        } catch (_) {
            $('shareInternalLink')?.select();
            document.execCommand('copy');
            toast(L.LinkCopied, 'success');
        }
    };

    const sendEmail = () => {
        const recipient = $('shareRecipientEmail')?.value?.trim() || '';
        const subject = encodeURIComponent(L.ShareDocument || '');
        const body = encodeURIComponent(`${$('shareDocumentTitle')?.textContent || ''}\n${detailUrl()}`);
        window.location.href = `mailto:${encodeURIComponent(recipient)}?subject=${subject}&body=${body}`;
    };

    const syncAccessMode = (source) => {
        const access = $('shareAccessLevel');
        const mode = $('shareMode');
        if (!access || !mode) return;
        if (source === access) mode.value = access.value;
        if (source === mode) access.value = mode.value;
    };

    const shareNow = async () => {
        const target = $('shareTargetCompany');
        const targetCompanyId = target?.value?.trim();
        if (!targetCompanyId) {
            target?.classList.add('is-invalid');
            toast(L.ReasonValidationFailed, 'error');
            return;
        }

        target.classList.remove('is-invalid');
        const fd = new FormData();
        fd.append('__RequestVerificationToken', token());
        fd.append('targetCompanyId', targetCompanyId);
        fd.append('shareMode', $('shareMode')?.value || 'REFERENCE');

        const response = await fetch(`/DocumentManagementControlledDocuments/share/${id}`, {
            method: 'POST',
            body: fd,
            credentials: 'same-origin'
        });
        const json = await response.json().catch(() => ({}));
        if (response.ok && json.isSuccessful !== false) {
            toast(L.ShareSuccess, 'success');
        } else {
            handleError(json);
        }
    };

    $('btnCopyShareLink')?.addEventListener('click', copyLink);
    $('btnSendShareEmail')?.addEventListener('click', sendEmail);
    $('shareAccessLevel')?.addEventListener('change', (event) => syncAccessMode(event.target));
    $('shareMode')?.addEventListener('change', (event) => syncAccessMode(event.target));
    $('btnShareDocumentNow')?.addEventListener('click', shareNow);

    loadDetail();
})();
