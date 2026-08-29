/**
 * MOD-0162-FU04 KnowledgePath form — path fields + EMBEDDED step sub-editor + per-step BranchCondition repeater (D2/D3/D7).
 * Steps are the path's sub-resource: they are created/updated/archived through /CRM/KnowledgePaths/api/paths/{id}/steps.
 * The branch conditions are authorable data only — they are never evaluated here.
 */
(function (window, document) {
    'use strict';
    const L = window.KnowledgePathsL10n || window.L10n || {};
    const $ = window.jQuery;

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const toast = (m, t) => window.showToast?.(m, t || 'info');

    // Flatpickr on the date inputs.
    if (window.flatpickr) {
        document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));
    }
    if ($ && $.fn.select2) {
        $('.select2').each(function () {
            const $s = $(this);
            if (!$s.hasClass('select2-hidden-accessible')) $s.select2({ width: '100%', dropdownParent: $(document.body) });
        });
    }

    // Topic cascade: narrow the topic list to the selected subject.
    const topicOptions = readJson('knowledgePathTopicOptions') || [];
    const subjectSelect = document.getElementById('SubjectId');
    const topicSelect = document.getElementById('TopicId');
    if (subjectSelect && topicSelect) {
        subjectSelect.addEventListener('change', () => {
            const subject = subjectSelect.value;
            const current = topicSelect.value;
            const opts = topicOptions.filter(o => o.group === subject || o.value === current);
            topicSelect.innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
                opts.map(o => `<option value="${esc(o.value)}"${o.value === current ? ' selected' : ''}>${esc(o.label)}${o.isInactive ? ' (' + esc(L.Archived || '') + ')' : ''}</option>`).join('');
            if ($ && $.fn.select2) $(topicSelect).trigger('change');
        });
    }

    // ─── Step sub-editor (Edit page only) ──────────────────────────────────────
    const editor = document.getElementById('stepEditor');
    if (!editor) return;
    const pathId = editor.dataset.pathId;
    const endpoint = editor.dataset.endpoint;
    const vocab = readJson('knowledgePathVocab') || { stepTypes: [], completionRules: [], versionPinPolicies: [] };

    const stepList = document.getElementById('stepList');
    const stepEmpty = document.getElementById('stepEmpty');
    const branchList = document.getElementById('branchList');
    let steps = [];
    let contents = [];
    let nodes = [];
    let canvas = null;

    const optionHtml = (arr, sel) => arr.map(v => `<option value="${esc(v)}"${v === sel ? ' selected' : ''}>${esc(v)}</option>`).join('');

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error((body.errors || [L.ErrorState]).join(' · '));
        return body.data;
    };

    const loadRefData = async () => {
        try { contents = (await envelope(await fetch(`${endpoint}/contents?includeArchived=false`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || []; } catch (e) { contents = []; }
        const subject = editor.dataset.subjectId;
        try { nodes = (await envelope(await fetch(`${endpoint}/concept-nodes?includeArchived=false${subject ? '&subjectId=' + subject : ''}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || []; } catch (e) { nodes = []; }
    };

    const loadSteps = async () => {
        try {
            steps = (await envelope(await fetch(`${endpoint}/paths/${pathId}/steps?includeArchived=false`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || [];
        } catch (e) { steps = []; }
        renderSteps();
    };

    const renderSteps = () => {
        stepEmpty.classList.toggle('d-none', steps.length > 0);
        stepList.innerHTML = steps.map(s => {
            const resCls = s.contentResolutionStatus === 'unresolved' ? 'bg-label-danger' : s.contentResolutionStatus === 'resolved-latest' ? 'bg-label-info' : 'bg-label-success';
            return `<div class="border rounded p-2 d-flex justify-content-between align-items-center">
                <div>
                    <span class="badge bg-label-secondary me-2">#${esc(s.stepOrder)}</span>
                    <span class="fw-medium">${esc(s.stepTitle)}</span>
                    <span class="text-muted ms-2">${esc(s.stepCode)} · ${esc(s.stepType)}</span>
                    ${s.isRequired ? `<span class="badge bg-label-primary ms-2">${esc(L.IsRequired || 'required')}</span>` : ''}
                    <span class="badge ${resCls} ms-2">${esc(s.contentResolutionStatus)}</span>
                    ${s.branchConditions && s.branchConditions.length ? `<span class="badge bg-label-secondary ms-2">${esc(L.BranchConditions || 'branch')}: ${s.branchConditions.length}</span>` : ''}
                </div>
                <div class="d-flex gap-1">
                    <button type="button" class="btn btn-sm btn-icon btn-label-secondary js-step-edit" data-id="${esc(s.stepId)}"><i class="bx bx-edit"></i></button>
                    <button type="button" class="btn btn-sm btn-icon btn-label-warning js-step-archive" data-id="${esc(s.stepId)}"><i class="bx bx-archive-in"></i></button>
                </div>
            </div>`;
        }).join('');
    };

    // ── Branch repeater ──
    const addBranchRow = (b) => {
        const row = document.createElement('div');
        row.className = 'branch-row border rounded p-2';
        const targetOpts = `<option value="">${esc(L.SelectOption || '')}</option>` +
            steps.map(s => `<option value="${esc(s.stepId)}"${b && b.targetStepId === s.stepId ? ' selected' : ''}>#${esc(s.stepOrder)} ${esc(s.stepCode)}</option>`).join('');
        row.innerHTML = `<div class="row g-2 align-items-end">
            <div class="col-4"><input type="text" class="form-control form-control-sm js-branch-code" placeholder="${esc(L.ConditionCode || 'code')}" value="${b ? esc(b.conditionCode) : ''}" /></div>
            <div class="col-4"><input type="text" class="form-control form-control-sm js-branch-desc" placeholder="${esc(L.Description || 'description')}" value="${b && b.description ? esc(b.description) : ''}" /></div>
            <div class="col-3"><select class="form-select form-select-sm js-branch-target">${targetOpts}</select></div>
            <div class="col-1 text-end"><button type="button" class="btn btn-sm btn-icon btn-label-danger js-branch-remove"><i class="bx bx-x"></i></button></div>
        </div>`;
        branchList.appendChild(row);
    };
    document.getElementById('btnAddBranch')?.addEventListener('click', () => addBranchRow(null));
    branchList?.addEventListener('click', e => { const b = e.target.closest('.js-branch-remove'); if (b) b.closest('.branch-row').remove(); });

    const readBranches = () => Array.from(branchList.querySelectorAll('.branch-row')).map(row => ({
        conditionCode: row.querySelector('.js-branch-code').value.trim(),
        description: row.querySelector('.js-branch-desc').value.trim() || null,
        targetStepId: row.querySelector('.js-branch-target').value || null
    })).filter(b => b.conditionCode);

    // ── Canvas open (add / edit) ──
    const openCanvas = (step) => {
        document.getElementById('stepCanvasError').classList.add('d-none');
        document.getElementById('stepEditId').value = step ? step.stepId : '';
        document.getElementById('stepCanvasLabel').textContent = step ? (L.EditPath || 'Edit') : (L.AddStep || 'Add Step');
        document.getElementById('stepOrder').value = step ? step.stepOrder : (steps.length ? (Math.max(...steps.map(s => s.stepOrder)) + 10) : 10);
        document.getElementById('stepCode').value = step ? step.stepCode : '';
        document.getElementById('stepTitle').value = step ? step.stepTitle : '';
        document.getElementById('stepType').innerHTML = optionHtml(vocab.stepTypes, step ? step.stepType : '');
        document.getElementById('stepVersionPin').innerHTML = optionHtml(vocab.versionPinPolicies, step ? step.versionPinPolicy : 'pinned');
        document.getElementById('stepCompletionRule').innerHTML = optionHtml(vocab.completionRules, step ? step.completionRule : 'none');
        document.getElementById('stepContentId').innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
            contents.map(c => `<option value="${esc(c.contentId || c.id)}"${step && step.contentId === (c.contentId || c.id) ? ' selected' : ''}>${esc(c.contentCode)} — ${esc(c.contentTitle)} (${esc(c.contentType)})</option>`).join('');
        document.getElementById('stepConceptNode').innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
            nodes.map(n => `<option value="${esc(n.conceptNodeId || n.id)}"${step && step.conceptNodeId === (n.conceptNodeId || n.id) ? ' selected' : ''}>${esc(n.conceptNodeCode)} — ${esc(n.conceptNodeName)}</option>`).join('');
        document.getElementById('stepPrerequisite').innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
            steps.filter(s => !step || s.stepId !== step.stepId).map(s => `<option value="${esc(s.stepId)}"${step && step.prerequisiteStepId === s.stepId ? ' selected' : ''}>#${esc(s.stepOrder)} ${esc(s.stepCode)}</option>`).join('');
        document.getElementById('stepDuration').value = step && step.estimatedDurationMinutes ? step.estimatedDurationMinutes : '';
        document.getElementById('stepNotes').value = step && step.notes ? step.notes : '';
        document.getElementById('stepRequired').checked = step ? !!step.isRequired : false;
        branchList.innerHTML = '';
        (step && step.branchConditions ? step.branchConditions : []).forEach(addBranchRow);
        canvas = window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('stepCanvas'));
        canvas.show();
    };

    const saveStep = async () => {
        const editId = document.getElementById('stepEditId').value;
        const payload = {
            stepOrder: parseInt(document.getElementById('stepOrder').value, 10),
            stepCode: document.getElementById('stepCode').value.trim(),
            stepTitle: document.getElementById('stepTitle').value.trim(),
            stepType: document.getElementById('stepType').value,
            contentId: document.getElementById('stepContentId').value,
            isRequired: document.getElementById('stepRequired').checked,
            versionPinPolicy: document.getElementById('stepVersionPin').value,
            completionRule: document.getElementById('stepCompletionRule').value,
            prerequisiteStepId: document.getElementById('stepPrerequisite').value || null,
            conceptNodeId: document.getElementById('stepConceptNode').value || null,
            estimatedDurationMinutes: document.getElementById('stepDuration').value ? parseInt(document.getElementById('stepDuration').value, 10) : null,
            notes: document.getElementById('stepNotes').value.trim() || null,
            branchConditions: readBranches()
        };
        const url = editId ? `${endpoint}/paths/${pathId}/steps/${editId}` : `${endpoint}/paths/${pathId}/steps`;
        const method = editId ? 'PUT' : 'POST';
        try {
            await envelope(await fetch(url, { method, credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify(payload) }));
            canvas?.hide();
            toast(editId ? (L.RecordUpdated || 'Updated') : (L.RecordCreated || 'Created'), 'success');
            await loadSteps();
        } catch (e) {
            const err = document.getElementById('stepCanvasError');
            err.textContent = e.message || L.ErrorState; err.classList.remove('d-none');
        }
    };

    document.getElementById('btnAddStep')?.addEventListener('click', () => openCanvas(null));
    document.getElementById('stepSaveBtn')?.addEventListener('click', saveStep);
    stepList?.addEventListener('click', e => {
        const edit = e.target.closest('.js-step-edit');
        if (edit) { const s = steps.find(x => x.stepId === edit.dataset.id); if (s) openCanvas(s); return; }
        const arc = e.target.closest('.js-step-archive');
        if (!arc) return;
        window.showConfirm?.(L.ArchiveStepConfirm || L.AreYouSure, async () => {
            try {
                await envelope(await fetch(`${endpoint}/paths/${pathId}/steps/${arc.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                toast(L.RecordArchived || 'Archived', 'success');
                await loadSteps();
            } catch (err) { toast(err.message || L.ErrorState, 'error'); }
        }, { type: 'warning' });
    });

    function readJson(id) {
        const el = document.getElementById(id);
        if (!el) return null;
        try { return JSON.parse(el.textContent || 'null'); } catch (e) { return null; }
    }

    (async () => { await loadRefData(); await loadSteps(); })();
})(window, document);
