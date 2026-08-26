'use strict';

/*
 * DCP-005 slice 2 — the controlled-document list screen: versions, import, search.
 *
 * Shaped after the taxonomy import wizard (assets/js/DocumentManagement/QmsBaselines/import.js), including the
 * one behaviour worth copying above all: A CHANGED FILE INVALIDATES THE DRY RUN. Without it a person can
 * validate one file and commit another, which is precisely the mistake the server's 409 exists to catch after
 * the fact — better to make it unreachable.
 */
(function (global) {
    const L = (() => {
        const el = document.getElementById('doclist-l10n');
        if (!el) { return {}; }
        try {
            const raw = JSON.parse(el.textContent || '{}');
            const out = {};
            // Payload keys are camelCase; every reader in this module normalises the same way.
            Object.keys(raw).forEach((k) => { out[k.charAt(0).toUpperCase() + k.slice(1)] = raw[k]; });
            return out;
        } catch (error) {
            console.error('[DocumentList] localization payload could not be parsed.', error);
            return {};
        }
    })();

    const t = (key) => L[key] || key;
    const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

    const fileInput = document.getElementById('docListFile');
    const sourceInput = document.getElementById('docListSourceKey');
    const versionInput = document.getElementById('docListVersion');
    const btnDryRun = document.getElementById('btnDocListDryRun');
    const btnImport = document.getElementById('btnDocListImport');
    const dryRunSpinner = document.getElementById('docListDryRunSpinner');
    const importSpinner = document.getElementById('docListImportSpinner');
    const summary = document.getElementById('docListSummary');
    const versionsBody = document.getElementById('docListVersions');
    const searchInput = document.getElementById('docListSearch');
    const resultsBody = document.getElementById('docListResults');

    if (!fileInput || !versionsBody) { return; }

    /*
     * The dry run is valid for ONE file. Name + size + last-modified is the same signature the precedent uses:
     * enough to catch "I picked a different file", cheap enough to compute on every change.
     */
    let validatedSignature = null;
    /*
     * What the dry run said about these exact bytes. Kept so the commit's own refusal can be spoken in the
     * reader's language: the server's sentence is English by design (a service holding seven translations of a
     * rule is a second place for the rule to live), and the STABLE CODE is what crosses.
     */
    let knownAsVersion = null;
    const signatureOf = (file) => (file ? `${file.name}|${file.size}|${file.lastModified}` : null);

    const setImportEnabled = (enabled) => { btnImport.disabled = !enabled; };

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const post = async (url, formData) => {
        const res = await fetch(url, {
            method: 'POST',
            credentials: 'include',
            headers: { RequestVerificationToken: token() },
            body: formData
        });
        let json = null;
        try { json = await res.json(); } catch { /* a non-JSON body is reported by status alone */ }
        return { status: res.status, json };
    };

    const buildForm = (file, includeVersion) => {
        const fd = new FormData();
        fd.append('file', file);
        fd.append('sourceKey', sourceInput.value.trim());
        if (includeVersion) { fd.append('listVersion', versionInput.value.trim()); }
        fd.append('__RequestVerificationToken', token());
        return fd;
    };

    const renderSummary = (data) => {
        const blocked = data.blockedCount ?? 0;
        const unread = data.unreadColumns || [];
        const missing = data.missingColumns || [];
        const already = data.alreadyImportedAsVersion;

        summary.innerHTML = `
            <ul class="list-unstyled mb-2">
                <li><strong>${data.entryCount}</strong> ${esc(t('DocListEntries'))}</li>
                <li><strong>${data.linkableCount}</strong> ${esc(t('DocListLinkable'))}</li>
                <li><strong>${blocked}</strong> ${esc(t('DocListBlocked'))}</li>
                <li>${esc(t('DocListUnreadColumns'))}: ${unread.length ? esc(unread.join(', ')) : esc(t('DocListNone'))}</li>
                <li>${esc(t('DocListMissingColumns'))}: ${missing.length ? esc(missing.join(', ')) : esc(t('DocListNone'))}</li>
            </ul>` + (already
            /*
             * ⚠ INFORMATION, NOT AN ERROR. The reader already has the state they asked for; telling them it
             * "failed" would send them looking for a problem that does not exist.
             */
            ? `<p class="alert alert-info mb-0" role="note"><i class="bx bx-info-circle me-1" aria-hidden="true"></i>${
                esc(t('DocListAlreadyImported')).replace('{0}', esc(already))}</p>`
            : '');
    };

    const loadVersions = async () => {
        const res = await fetch('/Tasks/api/document-list/versions', { credentials: 'include' });
        const json = await res.json().catch(() => null);
        const rows = json?.data || [];
        versionsBody.innerHTML = rows.length
            ? rows.map((v) => `<tr>
                <td class="fw-medium text-heading">${esc(v.listVersion)}</td>
                <td>${esc(v.fileName)}</td>
                <td>${esc(new Date(v.importedAt).toLocaleString(global.CurrentLanguage || undefined))}</td>
                <td class="text-end">${v.entryCount}</td>
                <td class="text-end">${v.linkableCount}</td>
                <td><button type="button" class="btn btn-sm btn-label-secondary js-copy-hash"
                        data-hash="${esc(v.contentHash)}" title="${esc(t('DocListCopyHash'))}"
                        aria-label="${esc(t('DocListCopyHash'))}"><code>${esc(v.contentHash.slice(0, 8))}</code></button></td>
              </tr>`).join('')
            : `<tr><td colspan="6" class="text-muted">${esc(t('DocListNoVersions'))}</td></tr>`;
    };

    const runSearch = async (term) => {
        const url = `/Tasks/api/document-list/search?term=${encodeURIComponent(term || '')}&limit=50`;
        const res = await fetch(url, { credentials: 'include' });
        const json = await res.json().catch(() => null);
        const rows = json?.data || [];

        resultsBody.innerHTML = rows.length
            ? rows.map((d) => {
                /*
                 * ⚠ A BLOCKED ROW IS SHOWN, NOT HIDDEN — and its unselectability is carried by MORE THAN COLOUR.
                 * `aria-disabled` states it, the reason is TEXT in the row, and the muted class is only the
                 * third signal. A row that reads as "cannot be chosen" only to someone who can see the grey is
                 * a row a screen-reader user would try to choose.
                 */
                const blocked = !d.linkableInErp;
                return `<tr class="${blocked ? 'text-muted' : ''}"${blocked ? ' aria-disabled="true"' : ''}>
                    <td class="fw-medium">${esc(d.documentCode)}</td>
                    <td>${esc(d.title)}${blocked
                        ? `<div class="small"><span class="badge bg-label-warning">${esc(t('DocListNotLinkable'))}</span>
                           <span class="ms-1">${esc(d.linkBlockedReason || '')}</span></div>`
                        : ''}</td>
                    <td>${esc(d.documentVersion || '-')}</td>
                    <td>${esc(d.status || '-')}</td>
                    <td>${esc(d.gqmsDomain || '-')}</td>
                </tr>`;
            }).join('')
            : `<tr><td colspan="5" class="text-muted">${esc(t('DocListNoResults'))}</td></tr>`;
    };

    btnDryRun.addEventListener('click', async () => {
        const file = fileInput.files?.[0];
        if (!file) { global.showToast?.(t('DocListFileRequired'), 'error'); return; }

        btnDryRun.disabled = true;
        dryRunSpinner.classList.remove('d-none');
        setImportEnabled(false);
        validatedSignature = null;
        try {
            const { json } = await post('/Tasks/DocumentList/dry-run', buildForm(file, false));
            if (json?.isSuccessful && json.data) {
                renderSummary(json.data);
                validatedSignature = signatureOf(file);
                knownAsVersion = json.data.alreadyImportedAsVersion || null;
                // A file that will not parse must not become importable, however tempting the button is.
                setImportEnabled((json.data.errors || []).length === 0);
            } else {
                summary.innerHTML = `<p class="alert alert-danger mb-0">${esc((json?.errors || [t('ErrorOccurred')])[0])}</p>`;
            }
        } catch (error) {
            console.error('[DocumentList] dry-run failed', error);
            global.showToast?.(t('ErrorOccurred'), 'error');
        } finally {
            btnDryRun.disabled = false;
            dryRunSpinner.classList.add('d-none');
        }
    });

    btnImport.addEventListener('click', async () => {
        const file = fileInput.files?.[0];
        // Re-guarded here as well as on change: the button is the last place this can still be stopped.
        if (!file || signatureOf(file) !== validatedSignature) {
            setImportEnabled(false);
            global.showToast?.(t('DocListFileChanged'), 'error');
            return;
        }
        if (!versionInput.value.trim()) { global.showToast?.(t('DocListVersionRequired'), 'error'); return; }

        btnImport.disabled = true;
        importSpinner.classList.remove('d-none');
        try {
            const { status, json } = await post('/Tasks/DocumentList/import', buildForm(file, true));
            if (json?.isSuccessful) {
                global.showToast?.(t('DocListImported'), 'success');
                await loadVersions();
            } else if (status === 409 || json?.reason_code === 'DOCUMENT_LIST_ALREADY_IMPORTED') {
                /*
                 * Already stored: the reader has what they asked for. Said as INFORMATION, in place — and in
                 * their own language, from the stable code rather than from the service's English sentence.
                 * The server's words are the fallback, so an unmapped refusal is visible rather than silent.
                 */
                const named = knownAsVersion
                    ? t('DocListAlreadyImported').replace('{0}', knownAsVersion)
                    : ((json?.errors || [])[0] || t('DocListAlreadyImported'));
                summary.innerHTML = `<p class="alert alert-info mb-0" role="note"><i class="bx bx-info-circle me-1" aria-hidden="true"></i>${
                    esc(named)}</p>`;
                await loadVersions();
            } else {
                global.showToast?.((json?.errors || [t('ErrorOccurred')])[0], 'error');
                btnImport.disabled = false;
            }
        } catch (error) {
            console.error('[DocumentList] import failed', error);
            global.showToast?.(t('ErrorOccurred'), 'error');
            btnImport.disabled = false;
        } finally {
            importSpinner.classList.add('d-none');
        }
    });

    /*
     * ⚠ THE LOCK. A new file means the summary on screen describes something else, so the import is closed
     * until it is validated again — see the module comment.
     */
    fileInput.addEventListener('change', () => {
        validatedSignature = null;
        setImportEnabled(false);
        const file = fileInput.files?.[0];
        document.getElementById('docListFileInfo').textContent = file
            ? `${file.name} · ${Math.round(file.size / 1024)} KB`
            : '';
        summary.innerHTML = `<p class="text-muted mb-0">${esc(t('DocListDryRunFirst'))}</p>`;
    });

    versionsBody.addEventListener('click', (event) => {
        const btn = event.target.closest('.js-copy-hash');
        if (btn) { global.navigator?.clipboard?.writeText(btn.getAttribute('data-hash') || ''); }
    });

    let searchTimer = null;
    searchInput?.addEventListener('input', () => {
        global.clearTimeout(searchTimer);
        searchTimer = global.setTimeout(() => void runSearch(searchInput.value), 250);
    });

    void loadVersions();
    void runSearch('');
})(typeof window !== 'undefined' ? window : globalThis);
