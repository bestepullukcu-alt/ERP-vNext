/**
 * MOD-0028-FU03 - QMS Baseline import wizard (dry-run -> review -> commit).
 * Same-origin proxy only (/DocumentManagementQmsBaselines/import/*). Workbook is governance metadata:
 * it is sent to the import API; never stored client-side, never written to disk, no physical folders.
 */
'use strict';

(function () {
    let L = window.L10n || {};
    const fileInput = document.getElementById('qmsWorkbookFile');
    const keyInput = document.getElementById('qmsSourceBaselineKey');
    const btnDryRun = document.getElementById('btnDryRun');
    const dryRunSpinner = document.getElementById('dryRunSpinner');
    const summaryCard = document.getElementById('qmsSummaryCard');
    const summaryBadge = document.getElementById('qmsSummaryBadge');
    const summaryCounts = document.getElementById('qmsSummaryCounts');
    const summaryFindings = document.getElementById('qmsSummaryFindings');
    const commitCard = document.getElementById('qmsCommitCard');
    const versionInput = document.getElementById('qmsBaselineVersion');
    const changeInput = document.getElementById('qmsChangeSummary');
    const btnCommit = document.getElementById('btnCommit');
    const commitSpinner = document.getElementById('commitSpinner');

    if (!fileInput || !btnDryRun) return;

    const fileInfo = document.getElementById('qmsFileInfo');

    // Commit is allowed only after a valid dry-run FOR THE CURRENT FILE.
    let validatedSignature = null;

    const setActiveStep = (n) => {
        document.querySelectorAll('#qmsSteps .qms-step').forEach((el) => {
            const s = Number(el.dataset.step);
            el.classList.toggle('active', s === n);
            el.classList.toggle('done', s < n);
        });
    };
    const humanSize = (bytes) => {
        if (bytes === null || bytes === undefined) return '';
        const u = ['B', 'KB', 'MB', 'GB']; let i = 0; let n = bytes;
        while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
        return `${i === 0 ? n : n.toFixed(1)} ${u[i]}`;
    };
    const renderFileInfo = (file) => {
        if (!fileInfo) return;
        if (!file) { fileInfo.classList.add('d-none'); fileInfo.textContent = ''; return; }
        fileInfo.classList.remove('d-none');
        fileInfo.innerHTML = `<i class="icon-base bx bx-spreadsheet me-1"></i>${escapeHtml(file.name)} · ${t('FileSize')}: ${escapeHtml(humanSize(file.size))}`;
    };

    const t = (k, fallback) => (L[k] || fallback || k);
    const escapeHtml = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fileSignature = (f) => (f ? `${f.name}|${f.size}|${f.lastModified}` : null);
    const antiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const mapReason = (rc) => ({
        VALIDATION_FAILED: t('ReasonValidationFailed'),
        CONFLICT: t('ReasonConflict'),
        PERM_DENIED: t('ReasonPermDenied'),
        NOT_FOUND_NON_LEAKAGE: t('ReasonNotFound')
    }[rc] || t('ErrorOccurred'));

    const setCommitEnabled = (enabled) => {
        if (commitCard) commitCard.classList.toggle('d-none', !enabled);
        if (btnCommit) btnCommit.disabled = !enabled;
    };

    const renderFindingsList = (titleKey, items) => {
        if (!items || !items.length) return '';
        const lis = items.map((x) => `<li>${escapeHtml(x)}</li>`).join('');
        return `<div class="mb-2"><strong>${t(titleKey)}</strong><ul class="mb-0">${lis}</ul></div>`;
    };

    const countCol = (labelKey, value, cls) =>
        `<div class="col"><div class="fw-bold fs-5 ${cls || ''}">${value ?? 0}</div><small class="text-muted">${t(labelKey)}</small></div>`;

    const showSummary = (data) => {
        summaryCard.classList.remove('d-none');
        summaryBadge.className = 'badge bg-label-success';
        summaryBadge.textContent = t('DryRunValid');
        summaryCounts.innerHTML =
            countCol('SummaryTotalRows', data.totalRows) +
            countCol('SummaryImported', data.importedDefinitionsCount, 'text-success') +
            countCol('SummarySkipped', data.skippedRows, 'text-warning');
        summaryFindings.innerHTML =
            renderFindingsList('SummaryWarnings', data.warnings);
        setActiveStep(3);
    };

    const showControlledFailure = (resp) => {
        summaryCard.classList.remove('d-none');
        summaryBadge.className = 'badge bg-label-danger';
        summaryBadge.textContent = t('DryRunInvalid');
        summaryCounts.innerHTML = '';
        const findings = Array.isArray(resp?.errors) ? resp.errors : [];
        const corr = resp?.correlation_id || resp?.correlationId;
        summaryFindings.innerHTML =
            `<div class="alert alert-danger mb-3">${escapeHtml(mapReason(resp?.reason_code || resp?.reasonCode))}</div>` +
            renderFindingsList('SummaryErrors', findings) +
            (corr ? `<div class="text-muted small mt-2">${t('CorrelationId')}: <code>${escapeHtml(corr)}</code></div>` : '');
        setActiveStep(2);
    };

    const validateInputs = () => {
        const file = fileInput.files?.[0];
        if (!file) { window.showToast?.(t('WorkbookRequired'), 'error'); return null; }
        if (!file.name.toLowerCase().endsWith('.xlsx')) { window.showToast?.(t('InvalidFileType'), 'error'); return null; }
        if (!keyInput.value.trim()) { window.showToast?.(t('SourceKeyRequired'), 'error'); return null; }
        return file;
    };

    const postForm = async (url, formData) => {
        // Send the antiforgery token as the default form field (and header) so validation works
        // regardless of whether a custom AntiforgeryOptions.HeaderName is configured.
        const token = antiForgeryToken();
        if (token && !formData.has('__RequestVerificationToken')) {
            formData.append('__RequestVerificationToken', token);
        }
        const res = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'RequestVerificationToken': token },
            body: formData
        });
        let json = null;
        try { json = await res.json(); } catch (e) { /* non-json */ }
        return { ok: res.ok, status: res.status, json };
    };

    btnDryRun.addEventListener('click', async () => {
        const file = validateInputs();
        if (!file) return;
        btnDryRun.disabled = true; dryRunSpinner.classList.remove('d-none');
        setCommitEnabled(false);
        validatedSignature = null;
        try {
            const fd = new FormData();
            fd.append('file', file);
            fd.append('sourceBaselineKey', keyInput.value.trim());
            const { json } = await postForm('/DocumentManagementQmsBaselines/import/dry-run', fd);
            if (json && json.isSuccessful && json.data) {
                showSummary(json.data);
                validatedSignature = fileSignature(file);
                setCommitEnabled(true);
            } else {
                showControlledFailure(json || {});
                setCommitEnabled(false);
            }
        } catch (e) {
            console.error('[QmsBaselines] dry-run failed', e);
            window.showToast?.(t('ErrorOccurred'), 'error');
        } finally {
            btnDryRun.disabled = false; dryRunSpinner.classList.add('d-none');
        }
    });

    btnCommit?.addEventListener('click', async () => {
        const file = fileInput.files?.[0];
        // Re-guard: commit only if the current file matches the validated dry-run.
        if (!file || fileSignature(file) !== validatedSignature) {
            setCommitEnabled(false);
            window.showToast?.(t('DryRunInvalid'), 'error');
            return;
        }
        if (!versionInput.value.trim()) { window.showToast?.(t('BaselineVersionRequired'), 'error'); return; }
        btnCommit.disabled = true; commitSpinner.classList.remove('d-none');
        try {
            const fd = new FormData();
            fd.append('file', file);
            fd.append('sourceBaselineKey', keyInput.value.trim());
            fd.append('baselineVersion', versionInput.value.trim());
            fd.append('changeSummary', changeInput.value.trim());
            const { json } = await postForm('/DocumentManagementQmsBaselines/import/commit', fd);
            if (json && json.isSuccessful) {
                window.showToast?.(t('CommitSuccess'), 'success');
                const id = json.data?.baselineReleaseId || json.data?.BaselineReleaseId;
                setTimeout(() => {
                    window.location.href = id
                        ? `/DocumentManagementQmsBaselines/Details/${id}`
                        : '/DocumentManagementQmsBaselines';
                }, 800);
            } else {
                showControlledFailure(json || {});
                btnCommit.disabled = false;
            }
        } catch (e) {
            console.error('[QmsBaselines] commit failed', e);
            window.showToast?.(t('ErrorOccurred'), 'error');
            btnCommit.disabled = false;
        } finally {
            commitSpinner.classList.add('d-none');
        }
    });

    // If the file changes after a dry-run, force a new dry-run before commit.
    fileInput.addEventListener('change', () => {
        validatedSignature = null;
        setCommitEnabled(false);
        renderFileInfo(fileInput.files?.[0]);
        setActiveStep(1);
    });
})();
