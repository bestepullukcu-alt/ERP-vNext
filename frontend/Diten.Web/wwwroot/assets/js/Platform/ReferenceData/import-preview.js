'use strict';

(function () {
    const root = document.getElementById('rd-import-page');
    if (!root) return;

    const form = document.getElementById('rd-import-form');
    const setSelect = document.getElementById('rd-import-set-code');
    const versionSelect = document.getElementById('rd-import-version-id');
    const fileInput = document.getElementById('rd-import-file');
    const formatSelect = document.getElementById('rd-import-format');
    const previewIdEl = document.getElementById('rd-import-preview-id');
    const commitBtn = document.getElementById('rd-import-commit');
    const exportBtn = document.getElementById('rd-import-export-errors');
    const bodyEl = document.getElementById('rd-import-preview-body');
    const resultEl = document.getElementById('rd-import-result');
    const statusEl = document.getElementById('rd-import-status');
    const emptyEl = document.getElementById('rd-import-empty');
    const previewBtn = form?.querySelector('button[type="submit"]');
    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || { can: () => true, apply: (el, _cap, stateAllowed) => { if (el) el.disabled = stateAllowed === false; return stateAllowed !== false; }, guard: () => true };

    let previewId = null;
    let lastPreview = null;
    let setDraftMap = new Map();

    const show = (el, on) => el && el.classList.toggle('d-none', !on);
    const text = (value) => value == null || String(value).trim() === '' ? '-' : String(value);
    const noDraftReason = 'An active draft version is required for import preview.';
    const normalize = (value) => String(value || '').trim().toLowerCase();
    const retiredSetReason = permissions.retiredSetReason || 'This reference data set is retired. Changes are disabled.';

    const setImportActions = (enabled, reason) => {
        permissions.apply(previewBtn, 'canImportPreview', enabled, reason || noDraftReason);
        if (fileInput) fileInput.disabled = !enabled;
        if (formatSelect) formatSelect.disabled = !enabled;
    };

    const setStatus = (message, level) => {
        if (!statusEl) return;
        if (!message) {
            statusEl.className = 'alert alert-info d-none mb-3';
            statusEl.textContent = '';
            return;
        }

        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'info';
        statusEl.className = `alert alert-${css} mb-3`;
        statusEl.textContent = message;
    };

    const toBase64 = (file) => new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => {
            const value = String(reader.result || '');
            const idx = value.indexOf(',');
            resolve(idx >= 0 ? value.slice(idx + 1) : value);
        };
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });

    const resetPreview = () => {
        previewId = null;
        lastPreview = null;
        previewIdEl.textContent = '-';
        permissions.apply(commitBtn, 'canImportCommit', false, 'A valid preview is required.');
        exportBtn.disabled = true;
        bodyEl.innerHTML = '';
        resultEl.textContent = '{}';
    };

    const renderDraftOptions = () => {
        const selectedSet = setSelect.value;
        const draft = setDraftMap.get(selectedSet);
        versionSelect.innerHTML = '';
        if (typeof permissions.clearGlobalBlock === 'function') {
            permissions.clearGlobalBlock();
        }

        if (!draft) {
            versionSelect.innerHTML = '<option value="">No active draft</option>';
            versionSelect.disabled = true;
            show(emptyEl, true);
            emptyEl.textContent = 'Selected set has no active draft. Create/open draft in Set Detail first.';
            setImportActions(false, noDraftReason);
            return;
        }

        versionSelect.innerHTML = `<option value="${draft.versionId}">${draft.setCode} / v${draft.versionNumber} (${draft.status})</option>`;
        if (draft.isRetired) {
            if (typeof permissions.setGlobalBlock === 'function') {
                permissions.setGlobalBlock(true, retiredSetReason);
            }
            versionSelect.disabled = true;
            show(emptyEl, true);
            emptyEl.textContent = retiredSetReason;
            setImportActions(false, retiredSetReason);
            permissions.apply(commitBtn, 'canImportCommit', false, retiredSetReason);
            setStatus(retiredSetReason, 'info');
            return;
        }

        versionSelect.disabled = false;
        show(emptyEl, false);
        setImportActions(true);
    };

    const loadDraftTargets = async () => {
        setStatus(null);
        show(emptyEl, false);
        resetPreview();

        const setsData = await api.getSets('?search=&status=&scope_type=&page=1&page_size=200&sort=-createdAt');
        const sets = setsData?.items || setsData?.Items || [];
        setDraftMap = new Map();

        for (const set of sets) {
            const setId = set.setId || set.SetId;
            const setCode = set.setCode || set.SetCode;
            if (!setId || !setCode) continue;

            try {
                const detail = await api.getSet(setId);
                const draftVersionId = detail.activeDraftVersionId || detail.ActiveDraftVersionId;
                if (!draftVersionId) continue;
                const version = await api.getVersion(draftVersionId);
                const isRetired = typeof permissions.isRetiredSet === 'function'
                    ? permissions.isRetiredSet(detail)
                    : normalize(detail?.status || detail?.Status) === 'retired';
                setDraftMap.set(setCode, {
                    versionId: draftVersionId,
                    versionNumber: version.versionNumber || version.VersionNumber || '?',
                    status: version.status || version.Status || 'Draft',
                    setCode,
                    isRetired
                });
            } catch (_error) {
                // Best effort. Skip broken set rows.
            }
        }

        const codes = Array.from(setDraftMap.keys()).sort((a, b) => a.localeCompare(b));
        setSelect.innerHTML = '';
        if (!codes.length) {
            setSelect.innerHTML = '<option value="">No sets with active draft</option>';
            setSelect.disabled = true;
            versionSelect.innerHTML = '<option value="">No active draft</option>';
            versionSelect.disabled = true;
            show(emptyEl, true);
            emptyEl.textContent = 'No reference-data set has an active draft version for import.';
            setImportActions(false, noDraftReason);
            return;
        }

        setSelect.disabled = false;
        setSelect.innerHTML = codes.map((code) => `<option value="${code}">${code}</option>`).join('');
        renderDraftOptions();
        setStatus('Select set, format and file, then run preview.', 'info');
    };

    const renderPreview = (data) => {
        const rows = data.rows || data.Rows || [];
        const blockingErrorCount = Number(data.blockingErrorCount ?? data.BlockingErrorCount ?? 0);
        previewId = data.previewId || data.PreviewId || null;
        lastPreview = data;

        previewIdEl.textContent = previewId || '-';
        permissions.apply(commitBtn, 'canImportCommit', !!previewId && blockingErrorCount <= 0, blockingErrorCount > 0 ? 'Resolve preview blockers before commit.' : 'A valid preview is required.');
        exportBtn.disabled = !previewId;

        if (!rows.length) {
            bodyEl.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Preview returned no rows.</td></tr>';
            return;
        }

        bodyEl.innerHTML = rows.map((row) => {
            const issues = row.issues || row.Issues || [];
            const issueHtml = !issues.length
                ? '-'
                : issues.map((issue) => {
                    const code = issue.ruleCode || issue.RuleCode || 'RULE';
                    const msg = issue.message || issue.Message || '';
                    const blocking = issue.isBlocking ?? issue.IsBlocking;
                    return `<div class="small ${blocking ? 'text-danger' : 'text-warning'}">${code}: ${msg}</div>`;
                }).join('');

            return `<tr>
                <td>${text(row.rowNumber || row.RowNumber)}</td>
                <td>${text(row.valueCode || row.ValueCode)}</td>
                <td>${text(row.operation || row.Operation)}</td>
                <td>${(row.isValid ?? row.IsValid) ? '<span class="badge bg-label-success">Yes</span>' : '<span class="badge bg-label-danger">No</span>'}</td>
                <td>${issueHtml}</td>
            </tr>`;
        }).join('');
    };

    setSelect?.addEventListener('change', () => {
        renderDraftOptions();
        resetPreview();
    });

    form?.addEventListener('submit', async (e) => {
        e.preventDefault();
        if (!permissions.guard('canImportPreview', (message) => setStatus(message, 'error'))) return;
        const file = fileInput.files?.[0];
        const draftVersionId = versionSelect.value;
        if (!file || !draftVersionId) {
            setStatus('Draft version and file are required.', 'error');
            return;
        }

        try {
            setStatus('Preview is running...', 'info');
            const payload = {
                target_draft_version_id: draftVersionId,
                file_name: file.name,
                format: formatSelect.value,
                content_base64: await toBase64(file)
            };

            const data = await api.previewImport(payload);
            renderPreview(data);
            const blocking = Number(data.blockingErrorCount ?? data.BlockingErrorCount ?? 0);
            if (blocking > 0) {
                setStatus(`Preview completed with ${blocking} blocking issue(s). Fix input and rerun preview.`, 'warning');
            } else {
                setStatus('Preview completed. Commit is enabled.', 'success');
            }
        } catch (error) {
            if (error?.isHandled) return;
            setStatus(error?.message || 'Preview failed.', 'error');
            bodyEl.innerHTML = `<tr><td colspan="5" class="text-danger">${error?.message || 'request_failed'}</td></tr>`;
        }
    });

    commitBtn?.addEventListener('click', async () => {
        if (!permissions.guard('canImportCommit', (message) => setStatus(message, 'error'))) return;
        if (!previewId) return;
        const key = `imp-${Date.now()}`;
        try {
            setStatus('Committing import preview...', 'info');
            const result = await api.commitImport(previewId, key);
            resultEl.textContent = JSON.stringify(result, null, 2);
            setStatus('Import commit completed successfully.', 'success');
            commitBtn.disabled = true;
        } catch (error) {
            if (error?.isHandled) return;
            setStatus(error?.message || 'Commit failed.', 'error');
        }
    });

    exportBtn?.addEventListener('click', () => {
        if (!lastPreview) return;
        const report = lastPreview.errorReport || lastPreview.ErrorReport;
        if (!report) return;
        const payload = {
            file_name: report.fileName || report.FileName || 'import-errors.json',
            content_type: report.contentType || report.ContentType || 'application/json',
            rows: report.rows || report.Rows || []
        };

        const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = payload.file_name;
        anchor.click();
        URL.revokeObjectURL(url);
    });

    setImportActions(false, noDraftReason);
    permissions.apply(commitBtn, 'canImportCommit', false, 'A valid preview is required.');

    loadDraftTargets().catch((error) => {
        if (error?.isHandled) return;
        setStatus(error?.message || 'Failed to load import draft targets.', 'error');
    });
})();
