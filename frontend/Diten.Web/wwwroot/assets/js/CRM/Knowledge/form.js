(function (window, document) {
    'use strict';
    const form = document.getElementById('knowledgeForm');
    if (!form) return;

    const jq = window.jQuery;
    const L = window.KnowledgeL10n || {};

    // External References — OPTIONAL. Golden Slim DataTable (v2) over a client-side collection + offcanvas editor,
    // mirrored into hidden inputs for MVC model binding. Mirrors the Consent form pattern.
    (function externalReferences() {
        const host = document.getElementById('externalReferencesHost');
        const tableEl = document.getElementById('dt-external-refs');
        if (!host || !tableEl || typeof DataTable === 'undefined') return;
        const esc = v => String(v ?? '').replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c]));

        let seq = 0;
        let refs = [];
        try { refs = JSON.parse(document.getElementById('externalRefSeed')?.textContent || '[]') || []; } catch { refs = []; }
        refs = (refs || []).filter(r => r && (r.sourceSystem || r.externalId)).map(r => Object.assign({ _id: ++seq }, r));

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
                { targets: 4, render: v => v ? `<span class="badge bg-label-primary">${esc(L.Yes || 'Yes')}</span>` : `<span class="text-muted">${esc(L.No || 'No')}</span>` },
                { targets: 5, orderable: false, searchable: false, className: 'cell-fit all text-end', render: (v, t, row) => removeBtn(row) }
            ],
            language: { emptyTable: L.NoExternalReferences || 'No external references' },
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
            refs = refs.filter(r => r._id !== Number(btn.dataset.refId));
            refresh();
        });

        const ss = document.getElementById('erSourceSystem');
        const canvasEl = document.getElementById('externalRefCanvas');
        // The offcanvas is rendered inside <form id="knowledgeForm">; move it to <body> so it behaves like the Consent
        // form (Enter in its search no longer submits the form, and the Select2 dropdown is free of the form's context).
        if (canvasEl && canvasEl.parentElement !== document.body) document.body.appendChild(canvasEl);
        const initSourceSystem = () => {
            if (!(jq && jq.fn && jq.fn.select2 && ss)) return;
            if (jq(ss).hasClass('select2-hidden-accessible')) return; // already initialized
            jq(ss).select2({
                tags: true, width: '100%', allowClear: true,
                placeholder: ss.dataset.placeholder || '',
                dropdownParent: canvasEl ? jq(canvasEl) : undefined,
                createTag: params => { const t = (params.term || '').trim(); return t ? { id: t, text: t } : null; }
            });
        };
        // select2 built while the offcanvas is hidden renders an EMPTY dropdown. Initialize it only once the offcanvas is
        // actually shown (visible), never at page load.
        canvasEl?.addEventListener('shown.bs.offcanvas', initSourceSystem);
        const getVal = id => (document.getElementById(id)?.value || '').trim();
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
        const from = document.getElementById('EffectiveFrom')?.value;
        const to = document.getElementById('EffectiveTo')?.value;
        if (from && to && new Date(to) < new Date(from)) {
            event.preventDefault();
            window.showToast?.(window.KnowledgeL10n?.EffectiveToBeforeFrom || 'Effective end cannot be earlier than start.', 'error');
        }
    });

    // Init reference pickers (Select2) and date fields (flatpickr, day/month/year display, Y-m-d value).
    const initWidgets = () => {
        document.querySelectorAll('.flatpickr-date').forEach(el => {
            if (typeof el.flatpickr === 'function') {
                el.flatpickr({ monthSelectorType: 'static', dateFormat: 'Y-m-d', altInput: true, altFormat: 'd/m/Y', allowInput: true });
            }
        });
        if (window.jQuery && window.jQuery.fn.select2) {
            window.jQuery('.select2:not(:disabled)').each(function () {
                const $s = window.jQuery(this);
                $s.wrap('<div class="position-relative"></div>').select2({ dropdownParent: $s.parent(), width: '100%' });
            });
        }
    };
    // Subject -> Topic cascade. Topic options come from a JSON island holding every active topic (each tagged with its
    // subject) plus any preserved archived/invalid topic. Changing the subject rebuilds the topic list to that subject.
    const setupCascade = () => {
        const subjectEl = document.getElementById('SubjectId');
        const topicEl = document.getElementById('TopicId');
        if (!subjectEl || !topicEl) return;

        let topicData = [];
        try { topicData = JSON.parse(document.getElementById('knowledgeTopicOptions')?.textContent || '[]'); }
        catch (e) { topicData = []; }

        const archivedLabel = topicEl.dataset.archivedLabel || '';
        const placeholder = topicEl.dataset.placeholder || '';
        const reinit = el => {
            if (!(window.jQuery && window.jQuery.fn.select2)) return;
            const $el = window.jQuery(el);
            if ($el.data('select2')) $el.select2('destroy');
            $el.select2({ dropdownParent: $el.parent(), width: '100%' });
        };

        // select2 fires 'change' through jQuery's event system, which native addEventListener does NOT catch. Bind via
        // jQuery when present (it is — select2 needs it); fall back to native for the no-select2 case.
        const onSubjectChange = () => {
            const subjectId = subjectEl.value;
            topicEl.innerHTML = '';
            const empty = document.createElement('option');
            empty.value = '';
            empty.textContent = placeholder;
            topicEl.appendChild(empty);
            topicData.filter(t => t.group === subjectId).forEach(t => {
                const o = document.createElement('option');
                o.value = t.value;
                o.textContent = t.label + (t.isInactive && archivedLabel ? ' (' + archivedLabel + ')' : '');
                if (t.group) o.setAttribute('data-subject', t.group);
                topicEl.appendChild(o);
            });
            topicEl.value = '';
            reinit(topicEl);
        };
        if (window.jQuery) window.jQuery(subjectEl).on('change', onSubjectChange);
        else subjectEl.addEventListener('change', onSubjectChange);
    };

    // AC-UI-3 — Subject -> ConceptType -> ConceptNode chain (MOD-0162-FU03).
    // ConceptType is a narrowing control only: it carries no name attribute and is never posted. The single persisted
    // value stays KnowledgeContent.ConceptNodeId, so the FU02 form contract is untouched.
    // Rebuilds fire only on a real user change, never on load — otherwise opening an existing record would wipe a
    // saved (possibly archived) node before the user touched anything.
    const setupConceptCascade = () => {
        const subjectEl = document.getElementById('SubjectId');
        const typeEl = document.getElementById('ConceptTypeIdFilter');
        const nodeEl = document.getElementById('ConceptNodeId');
        if (!subjectEl || !typeEl || !nodeEl) return;

        const parseIsland = id => {
            try { return JSON.parse(document.getElementById(id)?.textContent || '[]'); }
            catch (e) { return []; }
        };
        const typeData = parseIsland('knowledgeConceptTypeOptions');
        const nodeData = parseIsland('knowledgeConceptNodeOptions');

        const reinit = el => {
            if (!(window.jQuery && window.jQuery.fn.select2)) return;
            const $el = window.jQuery(el);
            if ($el.data('select2')) $el.select2('destroy');
            $el.select2({ dropdownParent: $el.parent(), width: '100%' });
        };
        const rebuild = (el, rows, groupAttr) => {
            const archivedLabel = el.dataset.archivedLabel || '';
            el.innerHTML = '';
            const empty = document.createElement('option');
            empty.value = '';
            empty.textContent = el.dataset.placeholder || '';
            el.appendChild(empty);
            rows.forEach(r => {
                const o = document.createElement('option');
                o.value = r.value;
                o.textContent = r.label + (r.isInactive && archivedLabel ? ' (' + archivedLabel + ')' : '');
                if (r.group) o.setAttribute(groupAttr, r.group);
                el.appendChild(o);
            });
            el.value = '';
            reinit(el);
        };

        // A new subject invalidates both the type filter and the node: neither can stay bound to the old subject.
        const onSubjectChange = () => {
            rebuild(typeEl, typeData.filter(t => t.group === subjectEl.value), 'data-subject');
            rebuild(nodeEl, [], 'data-concept-type');
        };
        // A new type narrows the node list. An archived node is never offered here — LoadOptionsAsync already dropped
        // it server-side; only an already-saved one survives, and only until the user deliberately changes the type.
        const onTypeChange = () => rebuild(nodeEl, nodeData.filter(n => n.group === typeEl.value), 'data-concept-type');

        if (window.jQuery) {
            window.jQuery(subjectEl).on('change', onSubjectChange);
            window.jQuery(typeEl).on('change', onTypeChange);
        } else {
            subjectEl.addEventListener('change', onSubjectChange);
            typeEl.addEventListener('change', onTypeChange);
        }
    };

    // Content Pointers: show one source at a time (Document Reference / URL / Body / Asset). Switching clears the others
    // so exactly one pointer is submitted.
    const setupContentSource = () => {
        const radios = Array.from(document.querySelectorAll('.content-source-radio'));
        const fields = Array.from(document.querySelectorAll('.content-pointer-field'));
        if (!radios.length || !fields.length) return;

        const show = source => fields.forEach(f => { f.style.display = f.dataset.source === source ? '' : 'none'; });
        const clearOthers = source => fields.forEach(f => {
            if (f.dataset.source === source) return;
            f.querySelectorAll('input, textarea').forEach(i => { i.value = ''; });
            f.querySelectorAll('select').forEach(s => {
                s.value = '';
                if (window.jQuery && window.jQuery(s).data('select2')) window.jQuery(s).val('').trigger('change.select2');
            });
        });

        const has = id => { const el = document.getElementById(id); return !!(el && el.value && el.value.trim() !== ''); };
        let initial = 'document';
        if (has('FileRef')) initial = 'document';
        else if (has('Url')) initial = 'url';
        else if (has('ContentBodyRef')) initial = 'body';
        else if (has('ContentAssetRef')) initial = 'asset';

        radios.forEach(r => {
            r.checked = r.value === initial;
            r.addEventListener('change', () => { if (r.checked) { show(r.value); clearOthers(r.value); } });
        });
        show(initial);
    };

    // Document Reference picker refresh: when the user returns from creating a document in Document Management (opened in
    // another tab), re-pull the options so the new document is selectable without a full page reload.
    const setupDocumentRefresh = () => {
        const sel = document.getElementById('FileRef');
        if (!sel || !sel.hasAttribute('data-document-picker')) return;
        let busy = false;
        const refresh = async () => {
            if (busy) return;
            busy = true;
            try {
                const res = await fetch('/CRM/Knowledge/api/document-options', {
                    credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (!res.ok) return;
                const opts = await res.json();
                if (!Array.isArray(opts)) return;
                const current = sel.value;
                sel.innerHTML = '';
                const empty = document.createElement('option');
                empty.value = '';
                empty.textContent = sel.dataset.placeholder || '';
                sel.appendChild(empty);
                let hasCurrent = false;
                opts.forEach(o => {
                    const op = document.createElement('option');
                    op.value = o.value;
                    op.textContent = o.label;
                    sel.appendChild(op);
                    if (o.value === current) hasCurrent = true;
                });
                if (current && !hasCurrent) {
                    const op = document.createElement('option'); op.value = current; op.textContent = current; sel.appendChild(op);
                }
                sel.value = current;
                if (window.jQuery && window.jQuery.fn.select2) {
                    const $s = window.jQuery(sel);
                    if ($s.data('select2')) $s.select2('destroy');
                    $s.select2({ dropdownParent: $s.parent(), width: '100%' });
                }
            } catch (e) { /* non-fatal */ }
            finally { busy = false; }
        };
        window.addEventListener('focus', refresh);
    };

    const boot = () => { initWidgets(); setupCascade(); setupConceptCascade(); setupContentSource(); setupDocumentRefresh(); };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();
})(window, document);
