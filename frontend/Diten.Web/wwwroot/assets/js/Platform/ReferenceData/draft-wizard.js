'use strict';

(function () {
    const root = document.getElementById('rd-draft-wizard-page');
    if (!root) return;

    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || { can: () => true, apply: (el, _cap, stateAllowed) => { if (el) el.disabled = stateAllowed === false; return stateAllowed !== false; }, guard: () => true };
    const L = window.L10n || {};
    const tt = (key, fallback) => (L && L[key]) || fallback;

    const setId = root.dataset.setId;
    const initialMode = String(root.dataset.mode || '').toLowerCase();

    const byId = (id) => document.getElementById(id);
    const alertEl = byId('rd-wizard-alert');
    const finalStatusEl = byId('rd-wizard-final-status');

    let currentSet = null;
    let draftVersion = null;
    let publishedVersion = null;
    let values = [];
    let stepper = null;
    // Action sequencing state for the Reference Values step:
    //   Add Value → Save Draft Values → Run Validation → Submit / Publish.
    // `dirty` = there are unsaved value edits; `validationOk` = the last
    // validation ran clean against the currently saved values.
    let dirty = false;
    let validationOk = false;

    const showMessage = (el, message, level) => {
        if (!el) return;
        const normalized = String(message || '').trim();
        if (!normalized) {
            el.className = 'alert alert-info d-none';
            el.textContent = '';
            return;
        }

        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : level === 'warning' ? 'warning' : 'info';
        el.className = `alert alert-${css}`;
        el.textContent = normalized;
    };

    const navigate = (url) => {
        if (typeof window.__rdNavigate === 'function') {
            window.__rdNavigate(url);
            return;
        }
        window.location.href = url;
    };

    const retiredSetReason = permissions.retiredSetReason || tt('NoDraftRetired', 'This reference data set is retired. Changes are disabled.');
    const normalize = (value) => String(value || '').trim().toLowerCase();
    const isRetiredSet = (setInfo) => (typeof permissions.isRetiredSet === 'function'
        ? permissions.isRetiredSet(setInfo)
        : normalize(setInfo?.status || setInfo?.Status) === 'retired');
    const applySetGate = () => {
        const retired = isRetiredSet(currentSet);
        if (typeof permissions.setGlobalBlock === 'function') {
            permissions.setGlobalBlock(retired, retiredSetReason);
        }
        if (retired) {
            showMessage(alertEl, retiredSetReason, 'warning');
        }
        return retired;
    };

    const publishReviewUrl = () => {
        const id = draftVersion?.versionId || draftVersion?.VersionId || root.dataset.versionId;
        return id ? `/Platform/ReferenceData/PublishReview/${encodeURIComponent(id)}` : null;
    };

    const strategy = () => document.querySelector('input[name="rd-strategy"]:checked')?.value || 'empty';

    const draftRequiredReason = tt('NoDraftReason', 'A draft version is required.');
    const draftNotEditableReason = tt('DraftNotEditable', 'This draft is no longer editable because it has been submitted. Create a new draft to change values.');
    const addValueFirstReason = tt('AddValueFirst', 'Add at least one value first.');
    const saveValuesFirstReason = tt('SaveValuesFirst', 'Save the draft values first.');
    const runValidationFirstReason = tt('RunValidationFirst', 'Run validation with no blockers first.');
    const noUnsavedChangesReason = tt('NoUnsavedChanges', 'No unsaved changes to save.');

    // Mark the draft as having unsaved edits. Any value change invalidates the
    // previous validation result, so Submit/Publish re-lock until the user saves
    // and re-validates.
    const markDirty = () => {
        dirty = true;
        validationOk = false;
        showMessage(alertEl, null);
        refreshActionStates();
    };

    // A draft can only receive value edits while it is still an editable Draft
    // (mirrors the backend guard in ReplaceBusinessReferenceDataVersionValues:
    // Status == Draft && IsEditable && !IsImmutable). Once submitted for
    // approval the backend flips IsEditable to false, so the authoring controls
    // must lock to avoid the raw "draft_required_for_value_edit" error.
    const isDraftEditable = () => {
        if (!draftVersion) return false;
        const status = normalize(draftVersion.status || draftVersion.Status);
        const editable = draftVersion.isEditable ?? draftVersion.IsEditable;
        const immutable = draftVersion.isImmutable ?? draftVersion.IsImmutable;
        return status === 'draft' && editable !== false && immutable !== true;
    };

    // Sequential gating of the Reference Values actions. Each step unlocks the
    // next; changing values re-locks everything downstream.
    const refreshActionStates = () => {
        const editable = isDraftEditable();
        const hasValues = values.length > 0;
        const editReason = !draftVersion ? draftRequiredReason : draftNotEditableReason;

        // 1. Add Value — needs an editable draft.
        permissions.apply(byId('rd-wizard-add-value'), 'canUpdateVersion', editable, editReason);

        // 2. Save Draft Values — editable draft with unsaved value rows.
        permissions.apply(
            byId('rd-wizard-save-values'),
            'canUpdateVersion',
            editable && hasValues && dirty,
            !editable ? editReason : !hasValues ? addValueFirstReason : noUnsavedChangesReason);

        // 3. Run Validation — values must be saved (no pending edits).
        permissions.apply(
            byId('rd-wizard-validate'),
            'canValidateVersion',
            !!draftVersion && hasValues && !dirty,
            !draftVersion ? draftRequiredReason : !hasValues ? addValueFirstReason : saveValuesFirstReason);

        // 4. Submit for Approval — needs a clean validation on the saved draft.
        permissions.apply(
            byId('rd-wizard-submit'),
            'canSubmitVersion',
            !!draftVersion && validationOk && !dirty,
            !draftVersion ? draftRequiredReason : runValidationFirstReason);

        // 5. Publish Readiness — same readiness gate as Submit.
        permissions.apply(
            byId('rd-wizard-publish'),
            'canPublishVersion',
            !!draftVersion && validationOk && !dirty,
            !draftVersion ? draftRequiredReason : runValidationFirstReason);
    };

    const summarize = () => {
        const normalizedCodes = new Set();
        let duplicates = 0;
        let requiredFailures = 0;
        values.forEach((v) => {
            const code = String(v.code || '').trim().toLowerCase();
            const label = String(v.label || '').trim();
            if (!code || !label) requiredFailures += 1;
            if (code && normalizedCodes.has(code)) duplicates += 1;
            if (code) normalizedCodes.add(code);
        });

        byId('rd-wizard-review-values').textContent = String(values.length);
        byId('rd-wizard-review-duplicates').textContent = String(duplicates);
        byId('rd-wizard-review-required').textContent = String(requiredFailures);
    };

    const renderEnrichment = async () => {
        const versionId = draftVersion?.versionId || draftVersion?.VersionId;
        if (!versionId) return;

        const [defsPayload, mappingsPayload] = await Promise.all([
            api.getVersionAttributeDefinitions(versionId).catch(() => ({ items: [] })),
            api.getVersionMappings(versionId).catch(() => ({ items: [] }))
        ]);

        const defsCount = (defsPayload?.items || defsPayload?.Items || []).length;
        const mappingCount = (mappingsPayload?.items || mappingsPayload?.Items || []).length;
        const hierarchyCount = values.filter((x) => x.parentValueCode).length;

        // Surfaced as KPI cards on the Review step — show the raw counts only.
        const setCount = (id, n) => { const el = byId(id); if (el) el.textContent = String(n); };
        setCount('rd-wizard-enrich-hierarchy', hierarchyCount);
        setCount('rd-wizard-enrich-attributes', defsCount);
        setCount('rd-wizard-enrich-mappings', mappingCount);
    };

    const syncValuesFromInputs = () => {
        values = values.map((item, index) => ({
            code: byId(`rd-wizard-code-${index}`)?.value?.trim() || '',
            label: byId(`rd-wizard-label-${index}`)?.value?.trim() || '',
            description: byId(`rd-wizard-description-${index}`)?.value?.trim() || '',
            isActive: !!byId(`rd-wizard-active-${index}`)?.checked,
            sortOrder: Number(byId(`rd-wizard-sort-${index}`)?.value || 0),
            parentValueCode: item.parentValueCode || null,
            attributes: item.attributes || null
        }));
    };

    const renderValues = () => {
        const body = byId('rd-wizard-values-body');
        const emptyState = byId('rd-wizard-values-empty');
        if (!body) return;
        const readOnly = (typeof permissions.isBlocked === 'function' && permissions.isBlocked()) || !isDraftEditable();
        const disabled = readOnly ? 'disabled' : '';

        if (!values.length) {
            body.innerHTML = `<tr><td colspan="6" class="text-center text-muted py-4">${tt('NoValuesInDraft', 'No values in this draft yet.')}</td></tr>`;
            emptyState?.classList.remove('d-none');
            summarize();
            return;
        }

        emptyState?.classList.add('d-none');

        body.innerHTML = values.map((v, index) => `
            <tr>
                <td><input class="form-control form-control-sm" id="rd-wizard-code-${index}" value="${v.code || ''}" ${disabled} /></td>
                <td><input class="form-control form-control-sm" id="rd-wizard-label-${index}" value="${v.label || ''}" ${disabled} /></td>
                <td><input class="form-control form-control-sm" id="rd-wizard-description-${index}" value="${v.description || ''}" ${disabled} /></td>
                <td><input type="checkbox" class="form-check-input" id="rd-wizard-active-${index}" ${v.isActive !== false ? 'checked' : ''} ${disabled} /></td>
                <td><input type="number" class="form-control form-control-sm" id="rd-wizard-sort-${index}" value="${Number(v.sortOrder || 0)}" ${disabled} /></td>
                <td><button class="btn btn-icon text-danger rd-wizard-remove-value" data-index="${index}" type="button" title="${tt('RemoveAction', 'Remove')}" aria-label="${tt('RemoveAction', 'Remove')}" ${disabled}><i class="bx bx-trash icon-md"></i></button></td>
            </tr>
        `).join('');

        summarize();
    };

    const loadValues = async () => {
        const versionId = draftVersion?.versionId || draftVersion?.VersionId;
        if (!versionId) {
            values = [];
            // Freshly loaded data is the saved baseline: no unsaved edits, and it
            // still needs validating before submit/publish unlock.
            dirty = false;
            validationOk = false;
            renderValues();
            refreshActionStates();
            return;
        }

        const payload = await api.getVersionValues(versionId);
        values = (payload?.items || payload?.Items || []).map((x) => ({
            code: x.code || x.Code || '',
            label: x.label || x.Label || '',
            description: x.description || x.Description || '',
            isActive: x.isActive ?? x.IsActive ?? true,
            sortOrder: x.sortOrder ?? x.SortOrder ?? 0,
            parentValueCode: x.parentValueCode || x.ParentValueCode || null,
            attributes: x.attributes || x.Attributes || null
        }));
        dirty = false;
        validationOk = false;
        renderValues();
        refreshActionStates();
    };

    const saveValues = async () => {
        if (!permissions.guard('canUpdateVersion', (message) => showMessage(alertEl, message, 'warning'))) return;
        const versionId = draftVersion?.versionId || draftVersion?.VersionId;
        if (!versionId) throw new Error('draft_required');
        if (!isDraftEditable()) throw new Error(draftNotEditableReason);

        syncValuesFromInputs();

        // Client-side guard: the draft needs at least one value, and every row
        // needs a code AND a display name. The backend rejects empty/incomplete
        // payloads, so stop here with a clear message rather than letting the
        // request fail with an opaque validation error.
        if (!values.length || values.some((x) => !String(x.code ?? '').trim() || !String(x.label ?? '').trim())) {
            throw new Error(tt('ValueCodeLabelRequired', 'Add at least one value, with a code and a display name for each.'));
        }

        // Coerce every field to the exact type the API contract expects so the
        // `values` collection always binds (sort_order = integer, attributes =
        // string->string map or null); a type mismatch here surfaces as an
        // opaque model-binding error ("...is not valid for 'Values'").
        const normalizeAttributes = (attrs) => {
            if (!attrs || typeof attrs !== 'object') return null;
            const out = {};
            Object.keys(attrs).forEach((key) => {
                const v = attrs[key];
                if (v !== null && v !== undefined) out[String(key)] = String(v);
            });
            return Object.keys(out).length ? out : null;
        };
        const toInt = (n) => { const v = Math.trunc(Number(n)); return Number.isFinite(v) ? v : 0; };

        const payload = {
            expected_concurrency_token: draftVersion?.concurrencyToken || draftVersion?.ConcurrencyToken,
            values: values.map((x) => ({
                code: String(x.code ?? '').trim(),
                label: String(x.label ?? '').trim(),
                description: (x.description && String(x.description).trim()) || null,
                is_active: x.isActive !== false,
                sort_order: toInt(x.sortOrder),
                parent_value_code: x.parentValueCode || null,
                attributes: normalizeAttributes(x.attributes)
            }))
        };

        try {
            await api.replaceVersionValues(versionId, payload);
        } catch (error) {
            // Backstop: if the draft was locked between gating and submit,
            // surface the human-readable reason instead of the raw error code.
            if (String(error?.message || '').includes('draft_required_for_value_edit')) {
                throw new Error(draftNotEditableReason);
            }
            throw error;
        }
        const reloaded = await api.getVersion(versionId);
        draftVersion = reloaded;
        await loadValues();
        // Keep the always-visible KPI cards (hierarchy/attributes/mappings) in
        // sync after each save.
        await renderEnrichment().catch(() => { });
        showMessage(alertEl, tt('RecordSaved', 'Draft values saved.'), 'success');
    };

    const resolveSetContext = async () => {
        currentSet = await api.getSet(setId);
        const retired = applySetGate();

        const activeDraftId = currentSet.activeDraftVersionId || currentSet.ActiveDraftVersionId;
        const publishedId = currentSet.publishedVersionId || currentSet.PublishedVersionId;

        draftVersion = activeDraftId ? await api.getVersion(activeDraftId).catch(() => null) : null;
        publishedVersion = publishedId ? await api.getVersion(publishedId).catch(() => null) : null;

        byId('rd-wizard-open-set').href = `/Platform/ReferenceData/Sets/${setId}`;
        byId('rd-wizard-set-code').textContent = currentSet.setCode || currentSet.SetCode || '-';
        byId('rd-wizard-set-name').textContent = currentSet.name || currentSet.Name || '-';
        byId('rd-wizard-set-scope').textContent = currentSet.scopeType || currentSet.ScopeType || '-';
        byId('rd-wizard-set-status').textContent = currentSet.status || currentSet.Status || '-';
        byId('rd-wizard-source-version').textContent = publishedVersion ? `v${publishedVersion.versionNumber || publishedVersion.VersionNumber}` : '-';
        byId('rd-wizard-draft-version').textContent = draftVersion ? `v${draftVersion.versionNumber || draftVersion.VersionNumber}` : '-';
        permissions.apply(byId('rd-wizard-start'), 'canCreateVersion', !retired, retired ? retiredSetReason : draftRequiredReason);
        refreshActionStates();

        // Resuming a draft that has already been submitted (no longer editable):
        // tell the user up front why the authoring controls are locked, instead
        // of letting them hit the backend rejection on save.
        if (draftVersion && !isDraftEditable() && !retired) {
            showMessage(alertEl, draftNotEditableReason, 'warning');
        }
    };

    const createDraft = async (sourceVersionId) => {
        if (!permissions.guard('canCreateVersion', (message) => showMessage(alertEl, message, 'warning'))) return;
        const created = await api.createVersion(setId, sourceVersionId ? { source_version_id: sourceVersionId } : {});
        const createdId = created?.versionId || created?.VersionId;
        if (!createdId) {
            throw new Error('draft_creation_failed');
        }

        draftVersion = await api.getVersion(createdId);
        byId('rd-wizard-draft-version').textContent = `v${draftVersion.versionNumber || draftVersion.VersionNumber}`;
    };

    const executeStrategy = async () => {
        await resolveSetContext();
        if (applySetGate()) {
            if (draftVersion) {
                await loadValues();
                await renderEnrichment();
            }
            return;
        }

        const mode = strategy();
        const publishedId = publishedVersion?.versionId || publishedVersion?.VersionId;
        if (mode === 'resume') {
            if (!draftVersion) {
                showMessage(alertEl, tt('NoActiveDraftCreating', 'No active draft exists. Creating an empty draft now.'), 'warning');
                await createDraft(null);
            }
        } else if (mode === 'copy') {
            if (!draftVersion) {
                if (!publishedId) throw new Error('published_required_for_copy');
                await createDraft(publishedId);
            }
        } else if (mode === 'import') {
            // Just provision the draft. The actual upload is driven from the
            // Set Basics step (download a template, fill it, then upload), so we
            // no longer auto-open the file picker here.
            if (!draftVersion) {
                await createDraft(publishedId || null);
            }
        } else {
            if (!draftVersion) {
                await createDraft(null);
            }
        }

        await loadValues();
        await renderEnrichment();
        toggleImportPanel();
        refreshActionStates();
    };

    // ---- Import-from-file support (Set Basics step) ----
    const toggleImportPanel = () => {
        byId('rd-wizard-import-panel')?.classList.toggle('d-none', strategy() !== 'import');
    };

    const runImport = async () => {
        if (!draftVersion) {
            await createDraft(publishedVersion?.versionId || publishedVersion?.VersionId || null);
        }
        if (!draftVersion) return;
        if (!permissions.guard('canImportPreview', (message) => showMessage(alertEl, message, 'warning'))) return;

        const fileInput = document.createElement('input');
        fileInput.type = 'file';
        fileInput.accept = '.json,.csv,.xlsx,text/csv,application/json,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
        fileInput.onchange = async () => {
            try {
                const file = fileInput.files?.[0];
                if (!file) return;
                const buffer = await file.arrayBuffer();
                const bytes = new Uint8Array(buffer);
                let binary = '';
                bytes.forEach((b) => { binary += String.fromCharCode(b); });
                const base64 = btoa(binary);
                const lower = file.name.toLowerCase();
                const format = lower.endsWith('.csv') ? 'csv' : lower.endsWith('.xlsx') ? 'xlsx' : 'json';
                const preview = await api.previewImport({
                    target_draft_version_id: draftVersion.versionId || draftVersion.VersionId,
                    file_name: file.name,
                    format,
                    content_base64: base64
                });

                const previewId = preview?.previewId || preview?.PreviewId;
                if (previewId) {
                    if (!permissions.guard('canImportCommit', (message) => showMessage(alertEl, message, 'warning'))) return;
                    await api.commitImport(previewId, `idem-${Date.now()}`);
                    await loadValues();
                    await renderEnrichment().catch(() => { });
                    showMessage(alertEl, tt('ImportCommitted', 'Import committed into draft.'), 'success');
                }
            } catch (error) {
                showMessage(alertEl, error?.message || tt('ImportFailed', 'Import failed.'), 'error');
            }
        };
        fileInput.click();
    };

    const downloadFile = (filename, content, mime) => {
        const blob = new Blob([content], { type: mime });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };

    // Template columns/fields mirror the backend import parsers (csv-v1 / json-v1).
    const csvImportTemplate = 'value_code,display_name,description,parent_value_code,replacement_value_code,is_deprecated,sort_order,attributes_json\n'
        + 'US,United States,Country code for the United States,,,false,10,"{""iso3"":""USA""}"\n'
        + 'TR,Türkiye,Country code for Türkiye,,,false,20,"{""iso3"":""TUR""}"\n';

    const jsonImportTemplate = JSON.stringify([
        { value_code: 'US', display_name: 'United States', description: 'Country code for the United States', parent_value_code: null, replacement_value_code: null, is_deprecated: false, sort_order: 10, attributes: { iso3: 'USA' } },
        { value_code: 'TR', display_name: 'Türkiye', description: 'Country code for Türkiye', parent_value_code: null, replacement_value_code: null, is_deprecated: false, sort_order: 20, attributes: { iso3: 'TUR' } }
    ], null, 2);

    // Same data as the CSV/JSON templates, laid out as worksheet rows.
    const xlsxTemplateRows = [
        ['value_code', 'display_name', 'description', 'parent_value_code', 'replacement_value_code', 'is_deprecated', 'sort_order', 'attributes_json'],
        ['US', 'United States', 'Country code for the United States', '', '', 'false', '10', '{"iso3":"USA"}'],
        ['TR', 'Türkiye', 'Country code for Türkiye', '', '', 'false', '20', '{"iso3":"TUR"}']
    ];

    // Build a minimal .xlsx (Office Open XML) entirely in the browser: a STORED
    // (uncompressed) zip of the required XML parts with inline-string cells.
    const buildXlsxBlob = (rows) => {
        const enc = new TextEncoder();
        const xmlEscape = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const colLetter = (i) => {
            let s = '';
            let n = i;
            do { s = String.fromCharCode(65 + (n % 26)) + s; n = Math.floor(n / 26) - 1; } while (n >= 0);
            return s;
        };
        const sheetRows = rows.map((cells, r) => {
            const cellXml = cells.map((val, c) =>
                `<c r="${colLetter(c)}${r + 1}" t="inlineStr"><is><t xml:space="preserve">${xmlEscape(val)}</t></is></c>`).join('');
            return `<row r="${r + 1}">${cellXml}</row>`;
        }).join('');

        const parts = [
            { name: '[Content_Types].xml', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>' },
            { name: '_rels/.rels', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>' },
            { name: 'xl/workbook.xml', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Template" sheetId="1" r:id="rId1"/></sheets></workbook>' },
            { name: 'xl/_rels/workbook.xml.rels', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>' },
            { name: 'xl/worksheets/sheet1.xml', text: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${sheetRows}</sheetData></worksheet>` }
        ].map((p) => ({ name: p.name, bytes: enc.encode(p.text) }));

        // CRC-32
        const crcTable = (() => {
            const t = new Uint32Array(256);
            for (let n = 0; n < 256; n++) {
                let c = n;
                for (let k = 0; k < 8; k++) c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1);
                t[n] = c >>> 0;
            }
            return t;
        })();
        const crc32 = (b) => {
            let c = 0xFFFFFFFF;
            for (let i = 0; i < b.length; i++) c = crcTable[(c ^ b[i]) & 0xFF] ^ (c >>> 8);
            return (c ^ 0xFFFFFFFF) >>> 0;
        };
        const u16 = (n) => [n & 0xFF, (n >>> 8) & 0xFF];
        const u32 = (n) => [n & 0xFF, (n >>> 8) & 0xFF, (n >>> 16) & 0xFF, (n >>> 24) & 0xFF];

        const chunks = [];
        const central = [];
        let offset = 0;
        parts.forEach((p) => {
            const nameBytes = enc.encode(p.name);
            const crc = crc32(p.bytes);
            const size = p.bytes.length;
            const local = [].concat(u32(0x04034b50), u16(20), u16(0), u16(0), u16(0), u16(0x21), u32(crc), u32(size), u32(size), u16(nameBytes.length), u16(0));
            chunks.push(new Uint8Array(local), nameBytes, p.bytes);
            central.push(new Uint8Array([].concat(u32(0x02014b50), u16(20), u16(20), u16(0), u16(0), u16(0), u16(0x21), u32(crc), u32(size), u32(size), u16(nameBytes.length), u16(0), u16(0), u16(0), u16(0), u32(0), u32(offset))));
            central.push(nameBytes);
            offset += local.length + nameBytes.length + size;
        });
        const centralStart = offset;
        let centralSize = 0;
        central.forEach((c) => { chunks.push(c); centralSize += c.length; });
        chunks.push(new Uint8Array([].concat(u32(0x06054b50), u16(0), u16(0), u16(parts.length), u16(parts.length), u32(centralSize), u32(centralStart), u16(0))));

        return new Blob(chunks, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    };

    byId('rd-wizard-tpl-csv')?.addEventListener('click', () => downloadFile('reference-data-template.csv', '﻿' + csvImportTemplate, 'text/csv;charset=utf-8'));
    byId('rd-wizard-tpl-json')?.addEventListener('click', () => downloadFile('reference-data-template.json', jsonImportTemplate, 'application/json'));
    byId('rd-wizard-tpl-xlsx')?.addEventListener('click', () => downloadFile('reference-data-template.xlsx', buildXlsxBlob(xlsxTemplateRows), 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'));
    byId('rd-wizard-import-file')?.addEventListener('click', () => runImport());

    const validate = async () => {
        if (!permissions.guard('canValidateVersion', (message) => showMessage(alertEl, message, 'warning'))) return;
        const versionId = draftVersion?.versionId || draftVersion?.VersionId;
        if (!versionId) throw new Error('draft_required');

        const result = await api.validateVersion(versionId);
        const blockers = result?.publishBlockers || result?.PublishBlockers || [];
        const blockingErrorCount = result?.blockingErrorCount || result?.BlockingErrorCount || 0;
        showMessage(alertEl, blockers.length > 0
            ? `${tt('ValidationBlockers', 'Validation blockers')}: ${blockers.join(', ')}`
            : `${tt('ValidationPassed', 'Validation passed.')} ${tt('BlockingErrorsLabel', 'Blocking errors')}: ${blockingErrorCount}.`, blockers.length > 0 ? 'warning' : 'success');

        // Clean validation against the saved draft unlocks Submit / Publish.
        validationOk = blockers.length === 0 && !dirty;
        refreshActionStates();
    };

    const withFinalAction = async (fn, successMessage) => {
        try {
            await fn();
            showMessage(finalStatusEl, successMessage, 'success');
            try {
                await resolveSetContext();
                await loadValues();
            } catch (_refreshError) {
                showMessage(finalStatusEl, `${successMessage} ${tt('RefreshFailedReload', 'Refresh failed, please reload page.')}`, 'warning');
            }
        } catch (error) {
            if (error?.isHandled) return;
            showMessage(finalStatusEl, error?.message || tt('ActionFailed', 'Action failed.'), 'error');
        }
    };

    // ---- bs-stepper wiring ----
    const goNext = () => stepper?.next();
    const goPrev = () => stepper?.previous();

    byId('rd-wizard-start')?.addEventListener('click', async () => {
        if (strategy() !== 'resume' && !permissions.guard('canCreateVersion', (message) => showMessage(alertEl, message, 'warning'))) return;
        try {
            await executeStrategy();
            goNext();
        } catch (error) {
            showMessage(alertEl, error?.message || tt('CouldNotInitStrategy', 'Could not initialize draft strategy.'), 'error');
        }
    });

    ['rd-wizard-prev-2', 'rd-wizard-prev-3']
        .forEach((id) => byId(id)?.addEventListener('click', goPrev));

    byId('rd-wizard-next-2')?.addEventListener('click', goNext);

    byId('rd-wizard-add-value')?.addEventListener('click', () => {
        if (!permissions.guard('canUpdateVersion', (message) => showMessage(alertEl, message, 'warning'))) return;
        // Capture what the user has already typed into the existing rows before
        // re-rendering, otherwise those edits are lost when the table redraws.
        syncValuesFromInputs();
        values.push({ code: '', label: '', description: '', isActive: true, sortOrder: (values.length + 1) * 10, parentValueCode: null, attributes: null });
        renderValues();
        markDirty();
    });

    byId('rd-wizard-values-body')?.addEventListener('click', (event) => {
        const removeBtn = event.target.closest('.rd-wizard-remove-value');
        if (!removeBtn) return;
        if (!permissions.guard('canUpdateVersion', (message) => showMessage(alertEl, message, 'warning'))) return;
        syncValuesFromInputs();
        const index = Number(removeBtn.dataset.index);
        if (!Number.isFinite(index)) return;
        values.splice(index, 1);
        renderValues();
        markDirty();
    });

    // Editing any cell marks the draft dirty (re-locks validation/submit/publish).
    ['input', 'change'].forEach((evt) => byId('rd-wizard-values-body')?.addEventListener(evt, (event) => {
        if (event.target?.closest?.('.rd-wizard-remove-value')) return;
        if (!dirty) markDirty();
    }));

    byId('rd-wizard-save-values')?.addEventListener('click', async () => {
        try {
            await saveValues();
        } catch (error) {
            showMessage(alertEl, error?.message || tt('CouldNotSaveValues', 'Could not save draft values.'), 'error');
        }
    });

    byId('rd-wizard-validate')?.addEventListener('click', () => validate().catch((error) => showMessage(alertEl, error?.message || tt('ValidationFailed', 'Validation failed.'), 'error')));

    byId('rd-wizard-submit')?.addEventListener('click', () => {
        if (!permissions.guard('canSubmitVersion', (message) => showMessage(finalStatusEl, message, 'warning'))) return;
        const target = publishReviewUrl();
        if (!target) {
            showMessage(finalStatusEl, tt('DraftRequiredForSubmit', 'Draft version is required before submit for approval.'), 'warning');
            return;
        }

        navigate(target);
    });

    byId('rd-wizard-publish')?.addEventListener('click', () => {
        if (!permissions.guard('canPublishVersion', (message) => showMessage(finalStatusEl, message, 'warning'))) return;
        const target = publishReviewUrl();
        if (!target) {
            showMessage(finalStatusEl, tt('DraftRequiredForPublish', 'Draft version is required before publish readiness review.'), 'warning');
            return;
        }

        navigate(target);
    });

    const initStepper = () => {
        const el = byId('rd-wizard-stepper');
        if (!el || !window.Stepper) return;
        stepper = new window.Stepper(el, { linear: false, animation: false });
        el.addEventListener('shown.bs-stepper', async (event) => {
            const to = event.detail?.to ?? 0;
            if (to >= 2 && !draftVersion) {
                showMessage(alertEl, tt('StartWizardFirst', 'Start the wizard to create a draft before authoring.'), 'warning');
            }
            // Set Basics step — show the import template/upload panel only when
            // the chosen strategy is "Import from File".
            if (to === 1) { toggleImportPanel(); }
            // Reference Values is now the final, consolidated step (index 2):
            // refresh the always-visible KPI cards when landing on it.
            if (to === 2) {
                summarize();
                try { await renderEnrichment(); } catch (_e) { /* non-blocking */ }
            }
            refreshActionStates();
        });
    };

    initStepper();

    resolveSetContext()
        .then(async () => {
            if (initialMode === 'resume' || initialMode === 'copy' || initialMode === 'import') {
                const radio = byId(`rd-strategy-${initialMode}`);
                if (radio) radio.checked = true;
                await executeStrategy();
                stepper?.to(1);
            }
        })
        .catch((error) => {
            showMessage(alertEl, error?.message || tt('CouldNotLoadSetContext', 'Could not load set context.'), 'error');
        });
})();
