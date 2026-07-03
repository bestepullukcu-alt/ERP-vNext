/**
 * MOD-0029-FU01 - Controlled document version history (golden compact detail page).
 *  • Header resolves to "folder path / document title"; breadcrumb keeps the page identity.
 *  • Version history rendered as a Golden Compact DataTable v2 styled table.
 *  • "Upload new version" opens a golden slim-create offcanvas with client-side content-change detection.
 *
 * "Is this really a new version?" — the SHA-256 fingerprint is the deterministic answer:
 *  • Before upload, the chosen file is hashed in the browser (crypto.subtle) and compared to the current
 *    active version. Identical content is flagged and only accepted if the user explicitly forces it.
 *  • Each history row shows its checksum and a per-version verdict (changed / same-as-previous / type-changed).
 *  • The backend re-checks and rejects an identical re-upload with 409 NO_CONTENT_CHANGE (authoritative).
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const ctx = window.ControlledDocumentContext || {};
    const id = ctx.documentId;
    if (!id) return;

    const $ = (s) => document.getElementById(s);
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const toast = (m, k) => window.showToast?.(m, k);
    const esc = (v) => (v === null || v === undefined ? '-' : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
    const reasonText = (code) => ({
        VALIDATION_FAILED: L.ReasonValidationFailed, CONFLICT: L.ReasonConflict, PERM_DENIED: L.ReasonPermDenied,
        NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound, STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable,
        FEATURE_DISABLED: L.ReasonFeatureDisabled, NO_CONTENT_CHANGE: L.ReasonNoContentChange
    }[code] || code);
    const handleError = (json) => {
        const corr = json?.correlation_id ? ` (${L.CorrelationId || 'Correlation'}: ${json.correlation_id})` : '';
        toast(`${reasonText(json?.reason_code) || L.ErrorOccurred}${corr}`, 'error');
    };

    const humanSize = (bytes) => {
        const n = Number(bytes);
        if (!Number.isFinite(n) || n <= 0) return '-';
        const units = ['B', 'KB', 'MB', 'GB'];
        let i = 0, v = n;
        while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
        return `${v.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
    };

    // SHA-256 hex (lowercase) of a File — matches the backend/storage checksum format exactly.
    const sha256Hex = async (file) => {
        const buf = await file.arrayBuffer();
        const digest = await crypto.subtle.digest('SHA-256', buf);
        return Array.from(new Uint8Array(digest)).map((b) => b.toString(16).padStart(2, '0')).join('');
    };

    const checksumCell = (checksum, verdictBadge) => {
        const short = checksum ? esc(String(checksum).slice(0, 12)) : '-';
        return `<code class="small" title="${esc(checksum)}">${short}</code>${verdictBadge ? ' ' + verdictBadge : ''}`;
    };

    const renderVersionActions = (version) => {
        const actions = [
            {
                key: 'preview',
                className: 'btn-text-secondary me-1',
                icon: 'bx bx-show',
                attrs: {
                    'data-id': version.id,
                    title: L.Preview,
                    'aria-label': L.Preview
                }
            },
            {
                key: 'download',
                icon: 'bx bx-download',
                text: L.Download,
                attrs: {
                    'data-id': version.id,
                    title: L.Download,
                    'aria-label': L.Download
                }
            }
        ];

        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(actions) : '';
    };

    // Active version fingerprint, captured from loadVersions for the upload-time comparison.
    let active = { checksum: '', mediaType: '', fileName: '' };
    let dt = null;

    const loadDetail = async () => {
        const res = await fetch(`/DocumentManagementControlledDocuments/detail/${id}`, { credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (!res.ok || json.isSuccessful === false) { handleError(json); return; }
        const d = json.data || json.Data;
        if (!d) return;
        const folderPath = String(d.collectionPath || '').trim();
        const title = String(d.title || L.VersionHistory || '').trim();
        const header = folderPath && title ? `${folderPath} / ${title}` : (title || folderPath || '');
        const titleEl = $('detailTitle');
        if (titleEl) titleEl.textContent = header;
    };

    const createVersionHistoryTable = () => {
        const table = document.getElementById('versionHistoryTable');
        if (!table) return null;
        if (!window.DataTable || !window.DtDefaults?.create || !window.DtDefaults?.exportButtons) {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
            return null;
        }

        const config = {
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            paging: true,
            lengthChange: true,
            searching: true,
            info: true,
            order: [[2, 'desc']],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit' },
                { responsivePriority: 1, targets: 3 },
                { responsivePriority: 2, targets: 9 },
                { orderable: false, targets: [5, 9] }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.UploadNewVersion,
                { href: '#offcanvasUploadVersion' },
                {},
                { exportColumns: [2, 3, 4, 5, 6, 7, 8], colvisColumns: [2, 3, 4, 5, 6, 7, 8] }
            ),
            drawCallback: function () {
                window.DtDefaults?.updateVisualState?.(this.api(), 0);
            },
            initComplete: function () {
                window.DtDefaults?.updateVisualState?.(this.api(), 0);
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    const offcanvasEl = $('offcanvasUploadVersion');
                    if (!offcanvasEl || !window.bootstrap?.Offcanvas) return;
                    window.bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl).show();
                });
            }
        };

        const api = new DataTable(table, window.DtDefaults.create(config));
        bindVersionTableHandlers(table);
        return api;
    };

    const bindVersionTableHandlers = (table) => {
        if (!table || table.dataset.versionHistoryBound === '1') return;
        table.dataset.versionHistoryBound = '1';

        if (window.DitenDataTable?.bindActionDispatcher) {
            window.DitenDataTable.bindActionDispatcher({
                tableEl: table,
                getTable: () => dt,
                onRowAction: {
                    preview: ({ id: versionId }) => {
                        if (!versionId) return;
                        window.open(`/DocumentManagementControlledDocuments/preview/${id}/${versionId}`, '_blank', 'noopener');
                    },
                    download: ({ id: versionId }) => {
                        if (!versionId) return;
                        window.location.assign(`/DocumentManagementControlledDocuments/download/${id}/${versionId}`);
                    }
                }
            });
        }

        table.addEventListener('change', (event) => {
            const target = event.target;
            if (!(target instanceof HTMLInputElement)) return;

            if (target.classList.contains('dt-checkboxes-select-all')) {
                const checked = target.checked;
                table.querySelectorAll('tbody .dt-checkboxes').forEach((checkbox) => {
                    checkbox.checked = checked;
                    checkbox.closest('tr')?.classList.toggle('selected', checked);
                });
                target.indeterminate = false;
                return;
            }

            if (target.classList.contains('dt-checkboxes')) {
                target.closest('tr')?.classList.toggle('selected', target.checked);
                const header = table.querySelector('.dt-checkboxes-select-all');
                const boxes = Array.from(table.querySelectorAll('tbody .dt-checkboxes'));
                const selected = boxes.filter((checkbox) => checkbox.checked).length;
                if (header) {
                    header.checked = boxes.length > 0 && selected === boxes.length;
                    header.indeterminate = selected > 0 && selected < boxes.length;
                }
            }
        });
    };

    const loadVersions = async () => {
        const res = await fetch(`/DocumentManagementControlledDocuments/versions/${id}`, { credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        const body = $('versionHistoryBody');
        if (!res.ok || json.isSuccessful === false) { handleError(json); return; }
        // Versions arrive newest-first (descending version number).
        const versions = json.data || json.Data || [];

        const activeRow = versions.find((v) => String(v.versionStatus).toUpperCase() === 'ACTIVE') || versions[0];
        if (activeRow) {
            active = { checksum: activeRow.file?.checksum || '', mediaType: activeRow.file?.mediaType || '', fileName: activeRow.file?.fileName || '' };
        }

        if (dt) { dt.destroy(); dt = null; }

        if (!versions.length) {
            body.innerHTML = '';
            dt = createVersionHistoryTable();
            return;
        }

        body.innerHTML = versions.map((v, i) => {
            const isActive = String(v.versionStatus).toUpperCase() === 'ACTIVE';
            const statusBadge = `<span class="badge bg-label-${isActive ? 'success' : 'secondary'}">${esc(isActive ? L.VersionStatusActive : L.VersionStatusSuperseded)}</span>`;

            // Per-version verdict: compare this version's checksum to the one it superseded (next, since list is desc).
            const prev = versions[i + 1];
            let verdict = '';
            if (prev && v.file?.checksum && prev.file?.checksum) {
                if (v.file.checksum === prev.file.checksum) {
                    verdict = `<span class="badge bg-label-warning" title="${esc(L.IdenticalToActiveWarning)}">${esc(L.SameAsPrevious)}</span>`;
                } else if (v.file?.mediaType && prev.file?.mediaType && v.file.mediaType !== prev.file.mediaType) {
                    verdict = `<span class="badge bg-label-info">${esc(L.FileTypeChanged)}</span>`;
                } else {
                    verdict = `<span class="badge bg-label-success">${esc(L.ContentChanged)}</span>`;
                }
            }

            return `<tr>
                <td></td>
                <td class="cell-fit"><input type="checkbox" class="dt-checkboxes form-check-input" value="${esc(v.id)}"></td>
                <td class="text-nowrap" data-order="${esc(v.versionNumber)}">v${esc(v.versionNumber)} ${statusBadge}</td>
                <td class="text-truncate" style="max-width:200px" title="${esc(v.file?.fileName)}">${esc(v.file?.fileName)}</td>
                <td class="text-nowrap" data-order="${esc(v.file?.byteSize || 0)}">${esc(humanSize(v.file?.byteSize))}</td>
                <td class="text-nowrap">${checksumCell(v.file?.checksum, verdict)}</td>
                <td>${esc(v.uploadedBy)}</td>
                <td class="text-nowrap" data-order="${esc(v.uploadedAt || '')}">${esc((v.uploadedAt || '').slice(0, 10))}</td>
                <td>${esc(v.changeSummary)}</td>
                <td class="cell-fit text-end text-nowrap">${renderVersionActions(v)}</td>
            </tr>`;
        }).join('');

        dt = createVersionHistoryTable();
    };

    // ── upload offcanvas: live content-change detection ──────────────────────
    const verdictBox = () => $('versionIntegrityVerdict');
    const forcePanel = () => $('versionForcePanel');
    const allowBox = () => $('versionAllowUnchanged');

    const resetUpload = () => {
        if ($('versionFile')) $('versionFile').value = '';
        if ($('versionChangeSummary')) $('versionChangeSummary').value = '';
        $('versionFile')?.classList.remove('is-invalid');
        $('versionChangeSummary')?.classList.remove('is-invalid');
        verdictBox().className = 'd-none';
        verdictBox().innerHTML = '';
        forcePanel().classList.add('d-none');
        if (allowBox()) allowBox().checked = false;
    };

    const showVerdict = (kind, text) => {
        const cls = { ok: 'alert-success', warn: 'alert-warning', info: 'alert-info' }[kind] || 'alert-secondary';
        verdictBox().className = `alert ${cls} py-2 px-3 mb-0 small`;
        verdictBox().innerHTML = text;
    };

    const evaluateFile = async () => {
        const file = $('versionFile')?.files?.[0];
        verdictBox().className = 'd-none';
        verdictBox().innerHTML = '';
        forcePanel().classList.add('d-none');
        if (allowBox()) allowBox().checked = false;
        if (!file) return;
        $('versionFile')?.classList.remove('is-invalid');
        if (!window.crypto?.subtle) return; // graceful: backend still enforces NO_CONTENT_CHANGE
        showVerdict('info', `<i class="icon-base bx bx-loader bx-spin me-1"></i>${esc(L.Computing)}`);
        let hash = '';
        try { hash = await sha256Hex(file); } catch { verdictBox().className = 'd-none'; return; }

        const typeChanged = active.fileName && file.type && active.mediaType && file.type !== active.mediaType;
        if (active.checksum && hash === active.checksum) {
            showVerdict('warn', `<i class="icon-base bx bx-error-circle me-1"></i>${esc(L.IdenticalToActiveWarning)}`);
            forcePanel().classList.remove('d-none');
        } else {
            const extra = typeChanged ? ` · <span class="text-info">${esc(L.FileTypeChanged)}</span>` : '';
            showVerdict('ok', `<i class="icon-base bx bx-check-circle me-1"></i>${esc(L.ContentChanged)}${extra}`);
        }
    };

    $('versionFile')?.addEventListener('change', evaluateFile);
    $('offcanvasUploadVersion')?.addEventListener('hidden.bs.offcanvas', resetUpload);

    $('btnSubmitVersion')?.addEventListener('click', async () => {
        const file = $('versionFile')?.files?.[0];
        const summary = $('versionChangeSummary')?.value?.trim();
        let invalid = false;
        if (!file) { $('versionFile')?.classList.add('is-invalid'); invalid = true; }
        if (!summary) { $('versionChangeSummary')?.classList.add('is-invalid'); invalid = true; }
        if (invalid) {
            toast(!summary ? L.ChangeSummaryRequired : (L.FileRequired || 'File required'), 'error');
            return;
        }

        const identicalShown = !forcePanel().classList.contains('d-none');
        if (identicalShown && !allowBox()?.checked) { toast(L.IdenticalToActiveWarning, 'error'); return; }

        const fd = new FormData();
        fd.append('file', file);
        fd.append('changeSummary', summary);
        fd.append('allowUnchanged', allowBox()?.checked ? 'true' : 'false');
        fd.append('__RequestVerificationToken', token());
        const res = await fetch(`/DocumentManagementControlledDocuments/upload-version/${id}`, { method: 'POST', body: fd, credentials: 'same-origin' });
        const json = await res.json().catch(() => ({}));
        if (res.ok && json.isSuccessful !== false) {
            toast(L.VersionUploaded || 'Version uploaded', 'success');
            const oc = $('offcanvasUploadVersion');
            if (window.bootstrap && oc) { window.bootstrap.Offcanvas.getInstance(oc)?.hide(); }
            loadVersions(); loadDetail();
        } else { handleError(json); }
    });

    loadDetail();
    loadVersions();
})();
