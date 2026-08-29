(function (window, document) {
    'use strict';
    const form = document.getElementById('preferenceForm');
    if (!form) return;
    const L = window.ConsentPreferenceL10n || {};

    // Golden Compact form init — Select2 on .select2 (mirrors consent-form.js).
    const jq = window.jQuery;
    if (jq && jq.fn && jq.fn.select2) {
        jq('#preferenceForm .select2').each(function () {
            const $this = jq(this);
            $this.wrap('<div class="position-relative"></div>').select2({ dropdownParent: $this.parent() });
        });
    }

    // Dependent subject picker (Create only). contact/account resolve by name via the Gateway proxy; every type
    // (and any no-match) still accepts a raw GUID via Select2 tags — the GUID fallback.
    const subjectSel = document.getElementById('SubjectId');
    const typeSel = document.getElementById('SubjectType');
    if (jq && jq.fn && jq.fn.select2 && subjectSel && subjectSel.tagName === 'SELECT' && typeSel) {
        const pickerUrl = subjectSel.dataset.pickerUrl;
        const placeholder = subjectSel.dataset.placeholder || '';
        const PICKER_TYPES = ['contact', 'account'];
        const $subject = jq(subjectSel);
        const buildPicker = () => {
            const type = typeSel.value;
            if ($subject.hasClass('select2-hidden-accessible')) $subject.select2('destroy');
            $subject.empty();
            const opts = {
                width: '100%', allowClear: true, placeholder,
                tags: true, // a raw GUID is always accepted (fallback for no match)
                createTag: params => { const term = (params.term || '').trim(); return term ? { id: term, text: term } : null; }
            };
            if (PICKER_TYPES.includes(type)) {
                opts.minimumInputLength = 0;
                opts.ajax = {
                    delay: 250,
                    url: pickerUrl,
                    dataType: 'json',
                    data: params => ({ subjectType: type, search: params.term || '' }),
                    processResults: data => ({ results: (data && data.results) || [] })
                };
            }
            $subject.select2(opts);
            $subject.prop('disabled', !type).trigger('change.select2');
        };
        jq(typeSel).on('change', buildPicker);
        buildPicker();
    }

    // Preference Value as true/false for boolean-restriction types (do-not-contact / do-not-visit): swap the free-text
    // input for a select. The text input remains the posting field (kept populated so the hidden-but-required control
    // never blocks submit); the select is unnamed and only drives it.
    const BOOLEAN_PREF_TYPES = ['do-not-contact', 'do-not-visit'];
    const prefTypeSel = document.getElementById('PreferenceType');
    const prefValueInput = document.getElementById('PreferenceValue');
    const prefValueBool = document.getElementById('preferenceValueBool');
    const prefValueBoolHelp = document.getElementById('preferenceValueBoolHelp');
    if (prefTypeSel && prefValueInput && prefValueBool) {
        const applyValueMode = resetOnBool => {
            const isBool = BOOLEAN_PREF_TYPES.includes((prefTypeSel.value || '').toLowerCase());
            if (isBool) {
                let current = (prefValueInput.value || '').trim().toLowerCase();
                if (resetOnBool || (current !== 'true' && current !== 'false')) current = 'true';
                prefValueBool.value = current;
                prefValueInput.value = current;
                prefValueInput.classList.add('d-none');
                prefValueBool.classList.remove('d-none');
                prefValueBoolHelp?.classList.remove('d-none');
            } else {
                prefValueInput.classList.remove('d-none');
                prefValueBool.classList.add('d-none');
                prefValueBoolHelp?.classList.add('d-none');
            }
        };
        prefValueBool.addEventListener('change', () => { prefValueInput.value = prefValueBool.value; });
        // Select2 raises 'change' on the underlying select; on Edit the type is disabled so only the initial pass runs.
        jq ? jq(prefTypeSel).on('change', () => applyValueMode(true)) : prefTypeSel.addEventListener('change', () => applyValueMode(true));
        applyValueMode(false); // initial load preserves any existing value
    }

    // External references — OPTIONAL. Golden Slim DataTable (v2) over a client-side collection + offcanvas editor.
    // Same pattern as consent-form.js: zero references is valid — nothing renders into #externalReferencesHost, so the
    // form posts an empty collection and the record saves fine. Rows are held in `refs` and mirrored into hidden inputs.
    (function externalReferences() {
        const host = document.getElementById('externalReferencesHost');
        const tableEl = document.getElementById('dt-external-refs');
        if (!host || !tableEl || typeof DataTable === 'undefined') return;
        const esc = v => String(v ?? '').replace(/[&<>'"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[c]));

        let seq = 0;
        let refs = [];
        try { refs = JSON.parse(document.getElementById('externalRefSeed')?.textContent || '[]') || []; } catch { refs = []; }
        refs = (refs || []).filter(r => r && (r.sourceSystem || r.externalId)).map(r => Object.assign({ _id: ++seq }, r));

        // Mirror the collection into contiguous hidden inputs (index order is irrelevant to the backend).
        const rebuildHidden = () => {
            host.innerHTML = refs.map((r, i) => `
                <input type="hidden" name="ExternalReferences[${i}].SourceSystem" value="${esc(r.sourceSystem)}" />
                <input type="hidden" name="ExternalReferences[${i}].ExternalId" value="${esc(r.externalId)}" />
                <input type="hidden" name="ExternalReferences[${i}].ExternalCode" value="${esc(r.externalCode || '')}" />
                <input type="hidden" name="ExternalReferences[${i}].ExternalName" value="${esc(r.externalName || '')}" />
                <input type="hidden" name="ExternalReferences[${i}].IsPrimary" value="${r.isPrimary ? 'true' : 'false'}" />`).join('');
        };

        const removeBtn = row => window.DitenDataTable?.renderActions
            ? window.DitenDataTable.renderActions([{
                className: 'js-remove-ref text-danger', icon: 'bx bx-trash', text: L.Remove || 'Remove',
                attrs: { 'data-ref-id': row._id, 'title': L.Remove || 'Remove' }
            }])
            : `<button type="button" class="btn btn-icon btn-sm js-remove-ref text-danger" data-ref-id="${row._id}"><i class="bx bx-trash"></i></button>`;
        const config = {
            data: refs, stateSave: false, searching: true, processing: false, order: [[0, 'asc']],
            columns: [
                { data: 'sourceSystem' }, { data: 'externalId' }, { data: 'externalCode' }, { data: 'externalName' },
                { data: 'isPrimary' }, { data: null }
            ],
            columnDefs: [
                { targets: [2, 3], render: v => esc(v || '—') },
                { targets: 4, render: v => v ? `<span class="badge bg-label-primary">${esc(L.Yes)}</span>` : `<span class="text-muted">${esc(L.No)}</span>` },
                { targets: 5, orderable: false, searchable: false, className: 'cell-fit all text-end', render: (v, t, row) => removeBtn(row) }
            ],
            language: { emptyTable: L.NoExternalReferences },
            // "Add Reference" lives in the DataTable toolbar (Golden Slim add-new slot) and opens the offcanvas.
            buttons: window.DtDefaults
                ? window.DtDefaults.exportButtons(
                    L.AddReference || 'Add Reference',
                    { 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#externalRefCanvas' },
                    {},
                    { exportColumns: [0, 1, 2, 3, 4], colvisColumns: [0, 1, 2, 3, 4] })
                : []
        };
        const table = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
        const refresh = () => { table.clear(); table.rows.add(refs).draw(); rebuildHidden(); };

        tableEl.addEventListener('click', event => {
            const btn = event.target.closest('.js-remove-ref');
            if (!btn) return;
            const id = Number(btn.dataset.refId);
            refs = refs.filter(r => r._id !== id);
            refresh();
        });

        // Offcanvas: SourceSystem is a searchable Select2 (suggestions + tags — a custom value is still allowed).
        const ss = document.getElementById('erSourceSystem');
        if (jq && jq.fn && jq.fn.select2 && ss) {
            jq(ss).select2({
                tags: true, width: '100%', allowClear: true,
                placeholder: ss.dataset.placeholder || '',
                dropdownParent: jq('#externalRefCanvas'),
                createTag: params => { const t = (params.term || '').trim(); return t ? { id: t, text: t } : null; }
            });
        }
        const getVal = id => (document.getElementById(id)?.value || '').trim();
        const canvasEl = document.getElementById('externalRefCanvas');
        document.getElementById('erSaveBtn')?.addEventListener('click', () => {
            const sourceSystem = getVal('erSourceSystem');
            const externalId = getVal('erExternalId');
            if (!sourceSystem || !externalId) { window.showToast?.(L.ValidationRequired || 'Source System and External ID are required.', 'error'); return; }
            refs.push({
                _id: ++seq, sourceSystem, externalId,
                externalCode: getVal('erExternalCode'),
                externalName: getVal('erExternalName'),
                isPrimary: document.getElementById('erIsPrimary')?.checked || false
            });
            refresh();
            ['erExternalId', 'erExternalCode', 'erExternalName'].forEach(id => { const el = document.getElementById(id); if (el) el.value = ''; });
            const primary = document.getElementById('erIsPrimary'); if (primary) primary.checked = false;
            if (jq && ss) jq(ss).val(null).trigger('change');
            if (canvasEl) window.bootstrap?.Offcanvas.getOrCreateInstance(canvasEl)?.hide();
        });

        rebuildHidden();
    })();

    form.addEventListener('submit', event => {
        const priority = Number(document.getElementById('Priority')?.value);
        if (!(priority >= 1)) {
            event.preventDefault();
            window.showToast?.(L.ValidationRequired || 'Priority must be 1 or greater.', 'error');
            return;
        }
        const start = document.getElementById('EffectiveFrom')?.value;
        const end = document.getElementById('EffectiveTo')?.value;
        if (start && end && new Date(end) < new Date(start)) {
            event.preventDefault();
            window.showToast?.(L.EffectiveToBeforeFrom || 'Effective To cannot be earlier than Effective From.', 'error');
        }
    });
})(window, document);
