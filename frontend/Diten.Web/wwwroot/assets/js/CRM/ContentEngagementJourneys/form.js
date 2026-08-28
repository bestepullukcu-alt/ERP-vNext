/**
 * MOD-0162-FU05 ContentEngagementJourney form — journey fields + EMBEDDED stage sub-editor + per-stage
 * BranchCondition repeater (S2/S5). Stages are the journey's sub-resource: they are created/updated/archived through
 * /CRM/ContentEngagementJourneys/api/journeys/{id}/stages. AdvancementRule, FallbackStageId and the branch conditions
 * are authorable DATA ONLY — nothing here (or on the server) evaluates them. The KnowledgePath picker is read-only:
 * it lists published + effective FU04 paths and never writes to a path.
 */
(function (window, document) {
    'use strict';
    const L = window.ContentEngagementJourneysL10n || window.L10n || {};
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
    const topicOptions = readJson('contentEngagementJourneyTopicOptions') || [];
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

    // ─── Stage sub-editor (Edit page only) ─────────────────────────────────────
    const editor = document.getElementById('stageEditor');
    if (!editor) return;
    const journeyId = editor.dataset.journeyId;
    const endpoint = editor.dataset.endpoint;
    const vocab = readJson('contentEngagementJourneyVocab')
        || { stageTypes: [], advancementRules: [], pathVersionPinPolicies: [], maxStagesPerJourney: 0, maxBranchConditionsPerStage: 0 };
    const maxBranches = Number(editor.dataset.maxBranches || vocab.maxBranchConditionsPerStage || 20);

    const stageList = document.getElementById('stageList');
    const stageEmpty = document.getElementById('stageEmpty');
    const branchList = document.getElementById('branchList');
    let stages = [];
    let paths = [];
    let canvas = null;

    const optionHtml = (arr, sel) => (arr || []).map(v => `<option value="${esc(v)}"${v === sel ? ' selected' : ''}>${esc(v)}</option>`).join('');
    const optionalHtml = (arr, sel) => `<option value="">${esc(L.SelectOption || '')}</option>` + optionHtml(arr, sel);

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error((body.errors || [L.ErrorState]).join(' · '));
        return body.data;
    };

    // Only published + effective FU04 paths may be bound (the server enforces the same rule).
    const loadRefData = async () => {
        try {
            const query = 'status=published&effectiveAt=' + encodeURIComponent(new Date().toISOString()) + '&includeArchived=false';
            paths = (await envelope(await fetch(`${endpoint}/knowledge-paths?${query}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || [];
        } catch (e) { paths = []; }
    };

    const loadStages = async () => {
        try {
            stages = (await envelope(await fetch(`${endpoint}/journeys/${journeyId}/stages?includeArchived=false`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || [];
        } catch (e) { stages = []; }
        renderStages();
    };

    const renderStages = () => {
        stageEmpty.classList.toggle('d-none', stages.length > 0);
        stageList.innerHTML = stages.map(s => {
            const resCls = s.pathResolutionStatus === 'unresolved' ? 'bg-label-danger' : s.pathResolutionStatus === 'resolved-latest' ? 'bg-label-info' : 'bg-label-success';
            const pathLabel = s.resolvedPathName || s.pathCode || '';
            return `<div class="border rounded p-2 d-flex justify-content-between align-items-center">
                <div>
                    <span class="badge bg-label-secondary me-2">#${esc(s.stageOrder)}</span>
                    <span class="fw-medium">${esc(s.stageName)}</span>
                    <span class="text-muted ms-2">${esc(s.stageCode)}${s.stageType ? ' · ' + esc(s.stageType) : ''}</span>
                    <span class="text-muted ms-2">→ ${esc(pathLabel)}${s.resolvedPathVersion ? ' @ ' + esc(s.resolvedPathVersion) : ''}</span>
                    ${s.isRequired ? `<span class="badge bg-label-primary ms-2">${esc(L.IsRequired || 'required')}</span>` : ''}
                    ${s.repeatable ? `<span class="badge bg-label-info ms-2">${esc(L.Repeatable || 'repeatable')}</span>` : ''}
                    ${s.pathUsageCountInJourney > 1 ? `<span class="badge bg-label-secondary ms-2">${esc(L.Repeated || 'repeated')}</span>` : ''}
                    <span class="badge ${resCls} ms-2">${esc(s.pathResolutionStatus)}</span>
                    ${s.advancementRule ? `<span class="badge bg-label-secondary ms-2">${esc(s.advancementRule)}</span>` : ''}
                    ${s.branchConditions && s.branchConditions.length ? `<span class="badge bg-label-secondary ms-2">${esc(L.BranchConditions || 'branch')}: ${s.branchConditions.length}</span>` : ''}
                </div>
                <div class="d-flex gap-1">
                    <button type="button" class="btn btn-sm btn-icon btn-label-secondary js-stage-edit" data-id="${esc(s.stageId)}"><i class="bx bx-edit"></i></button>
                    <button type="button" class="btn btn-sm btn-icon btn-label-warning js-stage-archive" data-id="${esc(s.stageId)}"><i class="bx bx-archive-in"></i></button>
                </div>
            </div>`;
        }).join('');
    };

    // ── Branch repeater (data only — never evaluated) ──
    const addBranchRow = (b) => {
        if (branchList.querySelectorAll('.branch-row').length >= maxBranches) {
            toast(L.ErrorState || '', 'warning');
            return;
        }
        const row = document.createElement('div');
        row.className = 'branch-row border rounded p-2';
        const targetOpts = `<option value="">${esc(L.SelectOption || '')}</option>` +
            stages.map(s => `<option value="${esc(s.stageId)}"${b && b.targetStageId === s.stageId ? ' selected' : ''}>#${esc(s.stageOrder)} ${esc(s.stageCode)}</option>`).join('');
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
        targetStageId: row.querySelector('.js-branch-target').value || null
    })).filter(b => b.conditionCode);

    const pathMeta = pathId => {
        const p = paths.find(x => (x.pathId || x.id) === pathId);
        if (!p) return '';
        const steps = p.activeStepCount ?? p.requiredStepCount ?? null;
        return `${p.pathCode} @ ${p.pathVersion}${steps === null ? '' : ' · ' + steps + ' ' + (L.StepCount || 'steps')}`;
    };

    // ── Canvas open (add / edit) ──
    const openCanvas = (stage) => {
        document.getElementById('stageCanvasError').classList.add('d-none');
        document.getElementById('stageEditId').value = stage ? stage.stageId : '';
        document.getElementById('stageCanvasLabel').textContent = stage ? (L.EditJourney || 'Edit') : (L.AddStage || 'Add Stage');
        document.getElementById('stageOrder').value = stage ? stage.stageOrder : (stages.length ? (Math.max(...stages.map(s => s.stageOrder)) + 10) : 10);
        document.getElementById('stageCode').value = stage ? stage.stageCode : '';
        document.getElementById('stageName').value = stage ? stage.stageName : '';
        document.getElementById('stageObjective').value = stage ? stage.stageObjective : '';
        document.getElementById('stageType').innerHTML = optionalHtml(vocab.stageTypes, stage ? stage.stageType : '');
        document.getElementById('stagePathPin').innerHTML = optionHtml(vocab.pathVersionPinPolicies, stage ? stage.pathVersionPinPolicy : 'pinned');
        document.getElementById('stageAdvancementRule').innerHTML = optionalHtml(vocab.advancementRules, stage ? stage.advancementRule : '');
        document.getElementById('stageKnowledgePathId').innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
            paths.map(p => {
                const id = p.pathId || p.id;
                const selected = stage && stage.recommendedKnowledgePathId === id ? ' selected' : '';
                return `<option value="${esc(id)}"${selected}>${esc(p.pathCode)} — ${esc(p.pathName)} (@ ${esc(p.pathVersion)})</option>`;
            }).join('');
        // A pinned stage whose path is no longer listed keeps its value visible instead of silently resetting.
        if (stage && stage.recommendedKnowledgePathId && !paths.some(p => (p.pathId || p.id) === stage.recommendedKnowledgePathId)) {
            const opt = document.createElement('option');
            opt.value = stage.recommendedKnowledgePathId;
            opt.textContent = `${stage.pathCode} (${L.Unresolved || 'unresolved'})`;
            opt.selected = true;
            document.getElementById('stageKnowledgePathId').appendChild(opt);
        }
        document.getElementById('stagePathMeta').textContent = stage ? pathMeta(stage.recommendedKnowledgePathId) : '';
        document.getElementById('stageFallback').innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
            stages.filter(s => !stage || s.stageId !== stage.stageId)
                .map(s => `<option value="${esc(s.stageId)}"${stage && stage.fallbackStageId === s.stageId ? ' selected' : ''}>#${esc(s.stageOrder)} ${esc(s.stageCode)}</option>`).join('');
        document.getElementById('stageMinVisit').value = stage && stage.minVisitNumber ? stage.minVisitNumber : '';
        document.getElementById('stageMaxVisit').value = stage && stage.maxVisitNumber ? stage.maxVisitNumber : '';
        document.getElementById('stageNotes').value = stage && stage.notes ? stage.notes : '';
        document.getElementById('stageRequired').checked = stage ? !!stage.isRequired : false;
        document.getElementById('stageRepeatable').checked = stage ? !!stage.repeatable : false;
        branchList.innerHTML = '';
        (stage && stage.branchConditions ? stage.branchConditions : []).forEach(addBranchRow);
        canvas = window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('stageCanvas'));
        canvas.show();
    };

    document.getElementById('stageKnowledgePathId')?.addEventListener('change', e => {
        document.getElementById('stagePathMeta').textContent = pathMeta(e.target.value);
    });

    const saveStage = async () => {
        const editId = document.getElementById('stageEditId').value;
        const minVisit = document.getElementById('stageMinVisit').value;
        const maxVisit = document.getElementById('stageMaxVisit').value;
        const payload = {
            stageOrder: parseInt(document.getElementById('stageOrder').value, 10),
            stageCode: document.getElementById('stageCode').value.trim(),
            stageName: document.getElementById('stageName').value.trim(),
            stageObjective: document.getElementById('stageObjective').value.trim(),
            recommendedKnowledgePathId: document.getElementById('stageKnowledgePathId').value,
            isRequired: document.getElementById('stageRequired').checked,
            repeatable: document.getElementById('stageRepeatable').checked,
            stageType: document.getElementById('stageType').value || null,
            pathVersionPinPolicy: document.getElementById('stagePathPin').value,
            minVisitNumber: minVisit ? parseInt(minVisit, 10) : null,
            maxVisitNumber: maxVisit ? parseInt(maxVisit, 10) : null,
            advancementRule: document.getElementById('stageAdvancementRule').value || null,
            fallbackStageId: document.getElementById('stageFallback').value || null,
            notes: document.getElementById('stageNotes').value.trim() || null,
            branchConditions: readBranches()
        };
        const url = editId ? `${endpoint}/journeys/${journeyId}/stages/${editId}` : `${endpoint}/journeys/${journeyId}/stages`;
        const method = editId ? 'PUT' : 'POST';
        try {
            await envelope(await fetch(url, { method, credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify(payload) }));
            canvas?.hide();
            toast(editId ? (L.RecordUpdated || 'Updated') : (L.RecordCreated || 'Created'), 'success');
            await loadStages();
        } catch (e) {
            const err = document.getElementById('stageCanvasError');
            err.textContent = e.message || L.ErrorState; err.classList.remove('d-none');
        }
    };

    document.getElementById('btnAddStage')?.addEventListener('click', () => openCanvas(null));
    document.getElementById('stageSaveBtn')?.addEventListener('click', saveStage);
    stageList?.addEventListener('click', e => {
        const edit = e.target.closest('.js-stage-edit');
        if (edit) { const s = stages.find(x => x.stageId === edit.dataset.id); if (s) openCanvas(s); return; }
        const arc = e.target.closest('.js-stage-archive');
        if (!arc) return;
        window.showConfirm?.(L.ArchiveStageConfirm || L.AreYouSure, async () => {
            try {
                await envelope(await fetch(`${endpoint}/journeys/${journeyId}/stages/${arc.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                toast(L.RecordArchived || 'Archived', 'success');
                await loadStages();
            } catch (err) { toast(err.message || L.ErrorState, 'error'); }
        }, { type: 'warning' });
    });

    function readJson(id) {
        const el = document.getElementById(id);
        if (!el) return null;
        try { return JSON.parse(el.textContent || 'null'); } catch (e) { return null; }
    }

    (async () => { await loadRefData(); await loadStages(); })();
})(window, document);
