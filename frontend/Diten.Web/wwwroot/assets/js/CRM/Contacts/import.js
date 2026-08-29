/**
 * MOD-0150 Contact Import/Export Task 2 — Contacts import workspace (upload → dry-run preview → apply).
 * All traffic goes through the same-origin MVC proxy, which calls the Gateway server-side; the CRM service is never
 * called directly. Everything rendered here comes from the server's PII-safe preview (masked labels, value-free
 * messages) — this script never reconstructs a name, phone number or e-mail.
 */
'use strict';

(function () {
    const L = (() => {
        const el = document.getElementById('contacts-import-l10n');
        if (!el) return {};
        try {
            return JSON.parse(el.textContent || '{}');
        } catch (error) {
            console.error('[ContactsImport] Localization payload could not be parsed.', error);
            return {};
        }
    })();

    const fileInput = document.getElementById('import-file');
    const strictInput = document.getElementById('import-strict');
    const validateBtn = document.getElementById('import-validate');
    const applyBtn = document.getElementById('import-apply');
    const previewCard = document.getElementById('import-preview-card');
    const previewTitle = document.getElementById('import-preview-title');
    const summaryEl = document.getElementById('import-summary');
    const rowsEl = document.getElementById('import-rows');
    const blockedEl = document.getElementById('import-blocked');
    const messagesEl = document.getElementById('import-file-messages');
    const filterEl = document.getElementById('import-filter');

    if (!fileInput || !validateBtn) return;

    let lastRows = [];

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    const badgeClass = (status) => {
        switch (status) {
            case 'create': return 'bg-label-success';
            case 'update': return 'bg-label-info';
            case 'end': return 'bg-label-warning';
            case 'error': return 'bg-label-danger';
            case 'conflict': return 'bg-label-danger';
            case 'skipped_dependency': return 'bg-label-secondary';
            default: return 'bg-label-secondary';
        }
    };

    const validateLabel = validateBtn.textContent.trim();

    const setBusy = (busy, text) => {
        validateBtn.disabled = busy;
        applyBtn.disabled = busy || !applyBtn.dataset.ready;
        validateBtn.innerHTML = busy
            ? `<span class="spinner-border spinner-border-sm me-2" role="status"></span>${escapeHtml(text || '')}`
            : `<i class="icon-base bx bx-search-alt me-1"></i>${escapeHtml(validateLabel)}`;
    };

    const renderSummary = (summary) => {
        const cards = [
            { key: 'creates', label: L.summaryCreates, cls: 'text-success' },
            { key: 'updates', label: L.summaryUpdates, cls: 'text-info' },
            { key: 'ends', label: L.summaryEnds, cls: 'text-warning' },
            { key: 'skips', label: L.summarySkips, cls: 'text-muted' },
            { key: 'errors', label: L.summaryErrors, cls: 'text-danger' },
            { key: 'warnings', label: L.summaryWarnings, cls: 'text-warning' },
            { key: 'conflicts', label: L.summaryConflicts, cls: 'text-danger' }
        ];

        summaryEl.innerHTML = cards.map((c) => `
            <div class="col-6 col-md-3 col-xl">
                <div class="border rounded-3 p-2 text-center h-100">
                    <div class="fs-4 fw-semibold ${c.cls}">${escapeHtml(summary?.[c.key] ?? 0)}</div>
                    <div class="small text-muted">${escapeHtml(c.label || c.key)}</div>
                </div>
            </div>`).join('');
    };

    const renderRows = () => {
        const filter = filterEl?.value || '';
        const rows = filter
            ? lastRows.filter((r) => (r.status || '').indexOf(filter) === 0)
            : lastRows;

        if (!rows.length) {
            rowsEl.innerHTML = `<tr><td colspan="8" class="text-center text-muted py-3">${escapeHtml(L.noRows || '-')}</td></tr>`;
            return;
        }

        rowsEl.innerHTML = rows.map((r) => `
            <tr>
                <td>${escapeHtml(r.sheet)}</td>
                <td>${escapeHtml(r.rowNumber)}</td>
                <td>${escapeHtml(r.operation || '-')}</td>
                <td>${escapeHtml(r.entityType)}</td>
                <td>${escapeHtml(r.displayLabel || r.resolvedKey || '-')}</td>
                <td><span class="badge ${badgeClass(r.status)}">${escapeHtml(r.status)}</span></td>
                <td class="small">${escapeHtml((r.changedFields || []).join(', ') || '-')}</td>
                <td class="small">${escapeHtml(r.message)}</td>
            </tr>`).join('');
    };

    const renderFileMessages = (preview) => {
        const errors = preview.fileErrors || [];
        const warnings = preview.fileWarnings || [];
        const blocks = [];
        errors.forEach((e) => blocks.push(`<div class="alert alert-danger py-2 px-3">${escapeHtml(e)}</div>`));
        warnings.forEach((w) => blocks.push(`<div class="alert alert-warning py-2 px-3">${escapeHtml(w)}</div>`));
        messagesEl.innerHTML = blocks.join('');
    };

    const render = (preview, applied) => {
        lastRows = preview.rows || [];
        previewCard.classList.remove('d-none');
        previewTitle.textContent = applied ? (L.importResultTitle || '') : (L.importPreviewTitle || '');
        renderFileMessages(preview);
        renderSummary(preview.summary);
        renderRows();

        if (preview.blockedReason) {
            blockedEl.textContent = preview.blockedReason;
            blockedEl.classList.remove('d-none');
        } else {
            blockedEl.classList.add('d-none');
        }

        const canApply = !!preview.canApply && !applied;
        applyBtn.disabled = !canApply;
        if (canApply) {
            applyBtn.dataset.ready = '1';
        } else {
            delete applyBtn.dataset.ready;
        }

        previewCard.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    };

    const post = async (url) => {
        const file = fileInput.files && fileInput.files[0];
        if (!file) {
            window.showToast?.(L.importSelectFile || '', 'warning');
            return null;
        }

        if (!/\.xlsx$/i.test(file.name)) {
            window.showToast?.(L.importOnlyXlsx || '', 'warning');
            return null;
        }

        const form = new FormData();
        form.append('file', file);
        form.append('strictMode', strictInput?.checked ? 'true' : 'false');

        const response = await fetch(url, { method: 'POST', body: form, credentials: 'same-origin' });
        const payload = await response.json().catch(() => null);

        if (!response.ok) {
            const message = payload?.errors?.[0] || L.errorOccurred || '';
            window.showToast?.(message, 'error');
            return null;
        }

        return payload?.data ?? payload?.Data ?? null;
    };

    validateBtn.addEventListener('click', async () => {
        setBusy(true, L.importValidating);
        try {
            const preview = await post(L.previewUrl);
            if (preview) render(preview, false);
        } catch (error) {
            console.error('[ContactsImport] Validation failed.', error);
            window.showToast?.(L.errorOccurred || '', 'error');
        } finally {
            setBusy(false);
        }
    });

    applyBtn.addEventListener('click', () => {
        // The confirmation spells out that ending a link keeps history — the user is approving a write, not a preview.
        window.showConfirm?.(L.apply || '', async () => {
            setBusy(true, L.importApplying);
            try {
                const result = await post(L.applyUrl);
                if (result) {
                    render(result, true);
                    if (result.applied) {
                        window.showToast?.(L.importApplySuccess || '', 'success');
                    } else if (result.blockedReason) {
                        window.showToast?.(result.blockedReason, 'warning');
                    }
                }
            } catch (error) {
                console.error('[ContactsImport] Apply failed.', error);
                window.showToast?.(L.errorOccurred || '', 'error');
            } finally {
                setBusy(false);
            }
        }, {
            type: 'warning',
            width: '480px',
            subtext: L.importApplyConfirm || '',
            confirmButtonText: L.apply || '',
            cancelButtonText: L.cancel || ''
        });
    });

    filterEl?.addEventListener('change', renderRows);

    // A new file invalidates the previous preview: applying it would write a plan the user never saw.
    fileInput.addEventListener('change', () => {
        applyBtn.disabled = true;
        delete applyBtn.dataset.ready;
        previewCard.classList.add('d-none');
    });
})();
