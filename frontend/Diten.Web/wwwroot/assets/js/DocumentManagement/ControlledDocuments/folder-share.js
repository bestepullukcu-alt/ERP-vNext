/**
 * MOD-0029-FU01 - Folder/branch share wizard: Select branch + target + mode -> Dry-run preview -> Execute.
 * Execute stays disabled until a successful dry-run for the current selection. Same-origin proxy only.
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const toast = (m, k) => window.showToast?.(m, k);
    const esc = (v) => (v === null || v === undefined ? '-' : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed, CONFLICT: L.ReasonConflict, PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound, STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable, FEATURE_DISABLED: L.ReasonFeatureDisabled
    }[code] || code);

    let lastSelectionKey = null;

    const buildPayload = () => ({
        sourceBranchCollectionInstanceId: document.getElementById('fsBranch')?.value?.trim(),
        targetCompanyId: document.getElementById('fsTarget')?.value?.trim(),
        includeTemplates: document.getElementById('fsIncludeTemplates')?.checked ?? true,
        shareMode: document.getElementById('fsShareMode')?.value || 'REFERENCE'
    });
    const selectionKey = (p) => `${p.sourceBranchCollectionInstanceId}|${p.targetCompanyId}|${p.includeTemplates}|${p.shareMode}`;

    const handleError = (json) => {
        const corr = json?.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
        toast(`${reasonText(json?.reason_code) || L.ErrorOccurred}${corr}`, 'error');
    };

    const renderResult = (d, isExecute) => {
        document.getElementById('fsResultCard')?.classList.remove('d-none');
        document.getElementById('fsResultTitle').textContent = isExecute ? L.Execute : L.DryRun;
        document.getElementById('fsCounts').innerHTML =
            `<div class="col-auto"><span class="badge bg-label-primary">${esc(L.FoldersIncluded)}: ${esc(d.foldersIncluded)}</span></div>` +
            `<div class="col-auto"><span class="badge bg-label-success">${esc(L.TemplatesIncluded)}: ${esc(d.templatesIncluded)}</span></div>` +
            `<div class="col-auto"><span class="badge bg-label-warning">${esc(L.TemplatesSkipped)}: ${esc(d.templatesSkipped)}</span></div>`;
        document.getElementById('fsOutcomes').innerHTML = (d.outcomes || []).map((o) => {
            const cls = { SHARED: 'success', COPIED: 'info', SKIPPED: 'warning', FAILED: 'danger' }[String(o.status).toUpperCase()] || 'secondary';
            return `<tr><td>${esc(o.itemType)}</td><td>${esc(o.message || o.itemKey)}</td><td><span class="badge bg-label-${cls}">${esc(o.status)}</span></td></tr>`;
        }).join('') || `<tr><td colspan="3" class="text-center text-muted">${esc(L.EmptyList)}</td></tr>`;
    };

    const post = async (url, payload) => {
        const fd = new FormData();
        fd.append('payloadJson', JSON.stringify(payload));
        fd.append('__RequestVerificationToken', token());
        const res = await fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        return { ok: res.ok && json.isSuccessful !== false, json };
    };

    document.getElementById('btnDryRun')?.addEventListener('click', async () => {
        const payload = buildPayload();
        if (!payload.sourceBranchCollectionInstanceId || !payload.targetCompanyId) { toast(L.ReasonValidationFailed || 'Invalid', 'error'); return; }
        const { ok, json } = await post('/DocumentManagementControlledDocuments/folder-shares/dry-run', payload);
        if (ok) {
            renderResult(json.data || json.Data, false);
            lastSelectionKey = selectionKey(payload);
            document.getElementById('btnExecute').disabled = false;
        } else {
            document.getElementById('btnExecute').disabled = true;
            handleError(json);
        }
    });

    document.getElementById('btnExecute')?.addEventListener('click', async () => {
        const payload = buildPayload();
        if (selectionKey(payload) !== lastSelectionKey) {
            document.getElementById('btnExecute').disabled = true;
            toast(L.DryRun || 'Run dry-run first', 'error');
            return;
        }
        const { ok, json } = await post('/DocumentManagementControlledDocuments/folder-shares/execute', payload);
        if (ok) { renderResult(json.data || json.Data, true); toast(L.ShareSuccess || 'Shared', 'success'); }
        else { handleError(json); }
    });

    // Any selection change re-arms the dry-run gate (execute disabled until a fresh valid dry-run).
    ['fsBranch', 'fsTarget', 'fsShareMode', 'fsIncludeTemplates'].forEach((idAttr) => {
        document.getElementById(idAttr)?.addEventListener('change', () => { document.getElementById('btnExecute').disabled = true; });
    });
})();
