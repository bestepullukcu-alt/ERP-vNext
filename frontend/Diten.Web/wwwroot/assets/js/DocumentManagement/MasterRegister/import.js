/**
 * WP-DM-DCP005-REGISTER-IMPORT-UI-01 - Document Master Register CSV import wizard (preview -> confirm -> commit).
 * Same-origin proxy only (/DocumentManagement/MasterRegister/api/import/*). The file never leaves this browser
 * tab as anything but base64 inside the proxy call; nothing is written to the register until the person reviews
 * the preview and explicitly confirms the commit (BL-367 window.showConfirm — no raw Swal/DitenModal).
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const fileInput = document.getElementById('registerImportFile');
    const fileInfo = document.getElementById('registerImportFileInfo');
    const btnPreview = document.getElementById('btnRegisterImportPreview');
    const previewSpinner = document.getElementById('registerImportPreviewSpinner');
    const summaryCard = document.getElementById('registerImportSummaryCard');
    const summaryBadge = document.getElementById('registerImportSummaryBadge');
    const summaryCounts = document.getElementById('registerImportSummaryCounts');
    const summaryFindings = document.getElementById('registerImportSummaryFindings');
    const alreadyAppliedAlert = document.getElementById('registerImportAlreadyAppliedAlert');
    const commitCard = document.getElementById('registerImportCommitCard');
    const btnCommit = document.getElementById('btnRegisterImportCommit');
    const commitSpinner = document.getElementById('registerImportCommitSpinner');
    const historyTableEl = document.getElementById('dt-register-import-history');

    if (!fileInput || !btnPreview) { return; }

    const t = (k, fallback) => (L[k] || fallback || k);
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const humanSize = (bytes) => {
        if (bytes === null || bytes === undefined) { return ''; }
        const u = ['B', 'KB', 'MB', 'GB']; let i = 0; let n = bytes;
        while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
        return `${i === 0 ? n : n.toFixed(1)} ${u[i]}`;
    };
    const antiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    // Commit is allowed only after a valid preview FOR THE CURRENT FILE. The server re-checks the hash
    // independently (contract §2) — this is a UX guard, not the boundary.
    let previewedSignature = null;
    let previewedHash = null;
    let previewedTotalRows = 0;

    const fileSignature = (f) => (f ? `${f.name}|${f.size}|${f.lastModified}` : null);

    const setActiveStep = (n) => {
        document.querySelectorAll('#registerImportSteps .qms-step').forEach((el) => {
            const s = Number(el.dataset.step);
            el.classList.toggle('active', s === n);
            el.classList.toggle('done', s < n);
        });
    };

    const setCommitEnabled = (enabled) => {
        commitCard?.classList.toggle('d-none', !enabled);
        if (btnCommit) { btnCommit.disabled = !enabled; }
    };

    const mapReason = (rc) => ({
        IMPORT_CONTENT_CHANGED: t('ReasonContentChanged'),
        IMPORT_ALREADY_APPLIED: t('ReasonAlreadyApplied'),
        VALIDATION_FAILED: t('ReasonValidationFailed')
    }[rc] || t('ErrorOccurred', 'An error occurred.'));

    const countCol = (labelKey, value, cls) =>
        `<div class="col"><div class="fw-bold fs-5 ${cls || ''}">${value ?? 0}</div><small class="text-muted">${t(labelKey)}</small></div>`;

    const renderLifecycleDistribution = (dist) => {
        const entries = Object.entries(dist || {});
        if (!entries.length) { return ''; }
        const chips = entries.map(([status, count]) =>
            `<span class="badge bg-label-secondary me-1 mb-1">${esc(status)}: ${count}</span>`).join('');
        return `<div class="mb-2"><strong>${t('PreviewLifecycleDistribution')}</strong><div class="mt-1">${chips}</div></div>`;
    };

    const renderErrorsList = (titleKey, items) => {
        if (!items || !items.length) { return ''; }
        const lis = items.map((x) => `<li>${esc(x)}</li>`).join('');
        return `<div class="mb-2"><strong>${t(titleKey)}</strong><ul class="mb-0">${lis}</ul></div>`;
    };

    const renderFileInfo = (file) => {
        if (!fileInfo) { return; }
        if (!file) { fileInfo.classList.add('d-none'); fileInfo.textContent = ''; return; }
        fileInfo.classList.remove('d-none');
        fileInfo.innerHTML = `<i class="icon-base bx bx-file me-1"></i>${esc(file.name)} · ${humanSize(file.size)}`;
    };

    const showPreview = (data) => {
        summaryCard.classList.remove('d-none');
        const hasErrors = Array.isArray(data.errors) && data.errors.length > 0;
        summaryBadge.className = hasErrors ? 'badge bg-label-warning' : 'badge bg-label-success';
        summaryBadge.textContent = hasErrors ? t('PreviewErrors') : t('Step2Preview');

        if (data.alreadyImported) {
            alreadyAppliedAlert.classList.remove('d-none');
            const when = data.alreadyImportedAt ? new Date(data.alreadyImportedAt).toLocaleString() : '';
            alreadyAppliedAlert.textContent = t('AlreadyImportedWarning')
                .replace('{0}', when).replace('{1}', data.alreadyImportedBy || '');
        } else {
            alreadyAppliedAlert.classList.add('d-none');
            alreadyAppliedAlert.textContent = '';
        }

        summaryCounts.innerHTML =
            countCol('PreviewTotalRows', data.totalRows) +
            countCol('PreviewCreated', data.created, 'text-success') +
            countCol('PreviewUpdated', data.updated, 'text-primary') +
            countCol('PreviewUnchanged', data.unchanged) +
            countCol('PreviewBlocked', data.blocked, 'text-warning') +
            countCol('PreviewCitableYes', data.citableByQualityDecisionYes, 'text-success') +
            countCol('PreviewCitableNo', data.citableByQualityDecisionNo);

        summaryFindings.innerHTML =
            renderLifecycleDistribution(data.lifecycleDistribution) +
            renderErrorsList('PreviewMissingColumns', data.missingColumns) +
            renderErrorsList('PreviewErrors', data.errors);

        setActiveStep(hasErrors ? 2 : 3);
    };

    const showControlledFailure = (resp) => {
        summaryCard.classList.remove('d-none');
        summaryBadge.className = 'badge bg-label-danger';
        summaryBadge.textContent = t('PreviewFailed');
        summaryCounts.innerHTML = '';
        alreadyAppliedAlert.classList.add('d-none');
        const errors = Array.isArray(resp?.errors) ? resp.errors : [];
        summaryFindings.innerHTML =
            `<div class="alert alert-danger mb-3">${esc(mapReason(resp?.reason_code || resp?.reasonCode))}</div>` +
            renderErrorsList('PreviewErrors', errors);
        setActiveStep(2);
    };

    const postForm = async (url, formData) => {
        const token = antiForgeryToken();
        if (token && !formData.has('__RequestVerificationToken')) {
            formData.append('__RequestVerificationToken', token);
        }
        let res;
        try {
            res = await fetch(url, {
                method: 'POST', credentials: 'same-origin',
                headers: { RequestVerificationToken: token }, body: formData
            });
        } catch (e) {
            return { ok: false, status: 0, json: null };
        }
        let json = null;
        try { json = await res.json(); } catch (e) { /* non-json */ }
        return { ok: res.ok, status: res.status, json };
    };

    btnPreview.addEventListener('click', async () => {
        const file = fileInput.files?.[0];
        if (!file) { window.showToast?.(t('ImportFileRequired'), 'error'); return; }
        if (!file.name.toLowerCase().endsWith('.csv')) { window.showToast?.(t('ImportInvalidFileType'), 'error'); return; }

        btnPreview.disabled = true; previewSpinner.classList.remove('d-none');
        setCommitEnabled(false);
        previewedSignature = null; previewedHash = null;
        try {
            const fd = new FormData();
            fd.append('file', file);
            const { json } = await postForm('/DocumentManagement/MasterRegister/api/import/dry-run', fd);
            if (json && json.isSuccessful && json.data) {
                showPreview(json.data);
                previewedSignature = fileSignature(file);
                previewedHash = json.data.contentHash;
                previewedTotalRows = json.data.totalRows || 0;
                const hasErrors = Array.isArray(json.data.errors) && json.data.errors.length > 0;
                setCommitEnabled(!hasErrors);
            } else {
                showControlledFailure(json || {});
                setCommitEnabled(false);
            }
        } catch (e) {
            console.error('[MasterRegisterImport] preview failed', e);
            window.showToast?.(t('PreviewFailed'), 'error');
        } finally {
            btnPreview.disabled = false; previewSpinner.classList.add('d-none');
        }
    });

    const doCommit = async () => {
        const file = fileInput.files?.[0];
        if (!file || fileSignature(file) !== previewedSignature || !previewedHash) {
            setCommitEnabled(false);
            window.showToast?.(t('PreviewFailed'), 'error');
            return;
        }

        btnCommit.disabled = true; commitSpinner.classList.remove('d-none');
        try {
            const fd = new FormData();
            fd.append('file', file);
            fd.append('expectedContentHash', previewedHash);
            const { json } = await postForm('/DocumentManagement/MasterRegister/api/import/commit', fd);
            if (json && json.isSuccessful) {
                window.showToast?.(t('CommitSucceeded'), 'success');
                setCommitEnabled(false);
                previewedSignature = null; previewedHash = null;
                historyTable?.ajax.reload();
            } else {
                showControlledFailure(json || {});
                btnCommit.disabled = false;
            }
        } catch (e) {
            console.error('[MasterRegisterImport] commit failed', e);
            window.showToast?.(t('CommitFailed'), 'error');
            btnCommit.disabled = false;
        } finally {
            commitSpinner.classList.add('d-none');
        }
    };

    btnCommit?.addEventListener('click', () => {
        const confirmMsg = t('CommitConfirmMessage').replace('{0}', String(previewedTotalRows));
        if (typeof window.showConfirm === 'function') {
            window.showConfirm(confirmMsg, doCommit, { type: 'warning', confirmButtonText: t('CommitImport') });
        } else if (window.confirm(confirmMsg)) {
            doCommit();
        }
    });

    fileInput.addEventListener('change', () => {
        previewedSignature = null; previewedHash = null; previewedTotalRows = 0;
        setCommitEnabled(false);
        renderFileInfo(fileInput.files?.[0]);
        setActiveStep(1);
        summaryCard.classList.add('d-none');
    });

    // ── Import history (read-only DataTable) ──────────────────────────────
    let historyTable = null;
    if (historyTableEl && window.DitenDataTable && window.DtDefaults) {
        historyTable = window.DitenDataTable.createCrudTable({
            tableEl: historyTableEl,
            ajax: {
                url: '/DocumentManagement/MasterRegister/api/import/history',
                type: 'GET',
                dataSrc: (json) => Array.isArray(json?.data) ? json.data : []
            },
            actions: {},
            config: {
                stateSave: false,
                language: { emptyTable: t('HistoryEmpty'), zeroRecords: t('HistoryEmpty') },
                order: [[2, 'desc']],
                columns: [
                    { data: 'fileName', name: 'fileName' },
                    { data: 'actor', name: 'actor' },
                    { data: 'appliedAt', name: 'appliedAt' },
                    { data: 'totalRows', name: 'counts' }
                ],
                columnDefs: [
                    { targets: 0, render: (data) => esc(data) },
                    { targets: 1, render: (data) => esc(data) },
                    { targets: 2, render: (data) => data ? new Date(data).toLocaleString() : '' },
                    {
                        targets: 3, orderable: false,
                        render: (data, type, full) => `${full.created ?? 0} / ${full.updated ?? 0} / ${full.blocked ?? 0}`
                    }
                ]
            }
        });
    }
})();
