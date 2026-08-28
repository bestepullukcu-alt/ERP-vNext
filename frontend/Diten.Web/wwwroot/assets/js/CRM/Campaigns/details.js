(function (window, document) {
    'use strict';
    const page = window.CampaignPage;
    const root = document.getElementById('campaignDetailsPage');
    // MOD-0165 FU10 - the manual targeting surface exists only for a manually targeted campaign. Details.cshtml does
    // not render the table, the offcanvas or the snapshot panel in segment mode, so this script has nothing to bind
    // and returns before touching a DOM that is deliberately absent. Nothing about the FU04 behaviour below changed.
    if (root && root.dataset.targetingMode === 'segment') return;
    if (!page || !root) return;
    const L = window.CampaignL10n || {};
    const campaignId = page.campaignId;
    const vocabulary = page.contract?.vocabulary || {};
    const esc = value => String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const value = id => document.getElementById(id)?.value?.trim() || '';
    const csv = id => value(id).split(',').map(x => x.trim()).filter(Boolean);
    const nullable = text => text || null;
    const dateOrNull = text => text ? new Date(text).toISOString() : null;
    const date = text => text ? new Date(text).toLocaleString() : '—';
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [body.message || L.ErrorState]).join(' · ')), { status:response.status, body });
        return body.data;
    };
    const showError = (id, message) => { const host = document.getElementById(id); if (host) { host.textContent = message; host.classList.remove('d-none'); } };
    const hideError = id => document.getElementById(id)?.classList.add('d-none');
    /**
     * Fills a select from a contract vocabulary.
     *
     * `labelPrefix` opts the list into localized labels: the vocabulary carries machine values (low / medium / high),
     * while the RESX carries the words a reader expects, keyed as <prefix><value>. Without it the option text would be
     * the raw token, which is fine for a filter chip and wrong for a field an author has to choose from.
     */
    const setOptions = (id, items, blank = true, labelPrefix = null) => {
        const select = document.getElementById(id); if (!select) return;
        const label = x => (labelPrefix && L[labelPrefix + x]) || x;
        select.innerHTML = (blank ? `<option value="">${esc(L.Filter || 'All')}</option>` : '') + (items || []).filter(x => x !== 'campaign-target').map(x => `<option value="${esc(x)}">${esc(label(x))}</option>`).join('');
    };
    const reasonBadges = reasons => (reasons || []).map(x => `<span class="badge bg-label-${x === 'consent_filter_not_applied' ? 'warning' : 'secondary'} me-1 mb-1">${esc(x)}</span>`).join('') || '—';
    const consentBadge = row => {
        const evaluation = row.consentEvaluation;
        const reasons = [...(row.reasonCodes || []), ...(evaluation?.reasonCodes || [])];
        if (reasons.includes('consent_filter_not_applied') || evaluation?.filterApplied === false) return `<span class="badge bg-label-warning">${esc(L.ConsentFilterNotAppliedWarning)}</span>`;
        if (reasons.includes('consent_evaluation_not_applicable')) return `<span class="badge bg-label-secondary">${esc(L.NotApplicable)}</span>`;
        const state = String(evaluation?.decision || evaluation?.eligibilityStatus || '').toLowerCase();
        if (state.includes('allow') || state.includes('eligible')) return `<span class="badge bg-label-success">${esc(L.Allowed)}</span>`;
        if (state.includes('block') || state.includes('denied')) return `<span class="badge bg-label-danger">${esc(L.Blocked)}</span>`;
        if (state) return `<span class="badge bg-label-warning">${esc(L.Unknown)}</span>`;
        return '—';
    };

    let targetTable = null;

    // Every data column except the responsive control (0) and the actions column (24). Both the export and the
    // column-visibility list use it, so a column can never be hidden with no way to bring it back.
    const EXPORTABLE_COLUMNS = [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23];

    /**
     * Golden-slim inline filter: the bar is authored above the grid and moved under the toolbar at init, so it opens
     * from the toolbar's filter button instead of permanently occupying space above the table.
     */
    const mountTargetFilter = () => {
        const host = document.getElementById('targetFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row')
            || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) toolbarRow.insertAdjacentElement('afterend', host);
    };

    const toggleTargetFilter = () => {
        const collapseEl = document.getElementById('targetFilterCollapse');
        if (collapseEl) window.bootstrap?.Collapse.getOrCreateInstance(collapseEl, { toggle:false }).toggle();
    };

    const bindTargetFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById('targetFilterCollapse');
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded','true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded','false'));
    };
    const targetActions = row => {
        if (!page.canManageTargets || row.isArchived || page.isArchived) return '—';
        return `<div class="dropdown"><button class="btn btn-sm btn-icon" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded"></i></button><div class="dropdown-menu dropdown-menu-end"><button class="dropdown-item js-edit-target" data-id="${esc(row.campaignTargetId)}"><i class="bx bx-edit me-2"></i>${esc(L.EditTarget)}</button><button class="dropdown-item text-warning js-archive-target" data-id="${esc(row.campaignTargetId)}"><i class="bx bx-archive-in me-2"></i>${esc(L.ArchiveTarget)}</button></div></div>`;
    };

    const targetQuery = () => {
        const params = new URLSearchParams();
        const map = { targetType:'targetTypeFilter', targetStatus:'targetStatusFilter', targetSource:'targetSourceFilter', snapshotBatchId:'snapshotBatchFilter' };
        Object.entries(map).forEach(([key,id]) => { const v = value(id); if (v) params.set(key,v); });
        params.set('includeArchived', document.getElementById('targetIncludeArchived')?.checked ? 'true' : 'false');
        return params.toString();
    };

    const loadTargets = async () => {
        const tableEl = document.getElementById('dt-campaign-targets'); if (!tableEl) return;
        document.getElementById('targetsSkeleton')?.classList.remove('d-none');
        try {
            const data = await envelope(await fetch(`/CRM/Campaigns/api/${campaignId}/targets?${targetQuery()}`, { credentials:'same-origin' }));
            const rows = data?.items || [];
            if (targetTable) { targetTable.clear(); targetTable.rows.add(rows).draw(); return; }
            const config = {
                // FU11 - scrollX is off on purpose. The eight columns that stay on the row fit without it, and the
                // other seventeen are reachable in the child row; keeping both a horizontal scrollbar and Responsive
                // makes the two fight over the same width.
                data:rows, searching:false, stateSave:false, scrollX:false, colReorder:{ columns:':gt(0):not(:last-child)' },
                order:[[23,'desc']],
                columns:[{data:null},{data:'campaignTargetId'},{data:'targetType'},{data:'targetId'},{data:'targetDisplayName'},{data:'targetStatus'},{data:'targetSource'},{data:'sourceReferenceType'},{data:'sourceReferenceId'},{data:'snapshotBatchId'},{data:'priorityLevel'},{data:'selectionReason'},{data:'reasonCodes'},{data:'exclusionReason'},{data:'consentEvaluation'},{data:'consentEvaluation'},{data:'consentEvaluation'},{data:'consentEvaluation'},{data:'consentEvaluation'},{data:'consentEvaluation'},{data:'effectiveFrom'},{data:'effectiveTo'},{data:'isArchived'},{data:'updatedAt'},{data:null}],
                columnDefs:[
                    {targets:0,className:'control',orderable:false,render:()=>''},
                    // FU11 round-2 - golden slim. FIVE columns carry the row: type, target, status, priority, actions.
                    // Everything else is HIDDEN, not removed: it stays declared, stays exportable, and comes back from
                    // the column-visibility control. That distinction is the whole point - the six consent-provenance
                    // columns are FU04's answer to "why is this target in, or out?", and a narrower default view is no
                    // reason to stop being able to answer it.
                    {targets:[1,3,6,7,8,9,11,12,13,14,15,16,17,18,19,20,21,22,23],visible:false},
                    {targets:[1,2,3,4,6,7,8,9,11,13],render:v=>esc(v || '—')},
                    {targets:5,render:v=>`<span class="badge bg-label-${v === 'excluded' ? 'danger' : 'success'}">${esc(v)}</span>`},
                    // FU11 - the priority BAND. Rows written before FU11 carry an integer and the server derives their
                    // band on read, so an old row shows a band here too rather than a blank cell.
                    {targets:10,render:v=>{
                        const band=String(v||'');
                        if(!band) return '—';
                        const tone=band==='high'?'danger':(band==='medium'?'warning':'secondary');
                        return `<span class="badge bg-label-${tone}">${esc(L['PriorityLevel_'+band]||band)}</span>`;
                    }},
                    {targets:12,render:v=>reasonBadges(v)},
                    {targets:14,render:(v,t,row)=>consentBadge(row)},
                    {targets:15,render:v=>esc(v?.eligibilityStatus || '—')},
                    {targets:16,render:v=>date(v?.evaluatedAt)},
                    {targets:17,render:v=>esc(v?.matchedConsentId || '—')},
                    {targets:18,render:v=>esc((v?.matchedPreferenceIds || []).join(', ') || '—')},
                    {targets:19,render:v=>esc(v?.evaluatorVersion || '—')},
                    {targets:[20,21,23],render:v=>date(v)},
                    {targets:22,render:v=>`<span class="badge bg-label-${v ? 'warning':'success'}">${esc(v ? L.Yes:L.No)}</span>`},
                    {targets:24,orderable:false,render:(v,t,row)=>targetActions(row)}
                ],
                language:{ emptyTable:L.EmptyState, processing:L.Loading },
                // Golden-slim toolbar: Action/export collection, column visibility, the filter toggle, and - only when
                // the actor may actually write - the primary "create target" action. Building it here rather than in
                // the page header is what puts the button where every other slim grid keeps it.
                buttons: window.DtDefaults?.exportButtons
                    ? window.DtDefaults.exportButtons(
                        page.canManageTargets && !page.isArchived ? L.CreateTarget : null,
                        {},
                        { filterBtn: {
                            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                            attr: { title: L.Filter, 'aria-controls': 'targetFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                            action: () => toggleTargetFilter()
                        } },
                        { exportColumns: EXPORTABLE_COLUMNS, colvisColumns: EXPORTABLE_COLUMNS })
                    : undefined,
                initComplete: function () {
                    mountTargetFilter();
                    bindTargetFilterA11y();
                    initFilterChips();
                    // Bound after DataTables renders the button; an addNewAttr onclick would fire at init instead.
                    document.querySelector('.add-new')?.addEventListener('click', event => {
                        event.preventDefault();
                        resetTargetForm();
                        window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('targetCanvas')).show();
                    });
                }
            };
            targetTable = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
            targetTable.on('column-reorder.dt columns-reordered.dt', () => tableEl.dispatchEvent(new CustomEvent('campaign-targets:columns-reordered')));
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        finally { document.getElementById('targetsSkeleton')?.classList.add('d-none'); }
    };

    // ---------------------------------------------------------------- FU11 target picker
    //
    // The GUID field became a picker. It mirrors the segment editor's subject picker exactly - same endpoints, same
    // Select2 ajax shape, same server-side paging - so accounts and contacts are searched where they live instead of
    // being downloaded in full. Neither master is written: the picker hands over an id, and the text it showed is kept
    // as a display label.
    const $ = window.jQuery;
    const pickerTypes = { account:'accounts', contact:'contacts' };

    const pickerLabel = (item, kind) => kind === 'contact'
        ? (item.displayName || [item.firstName, item.lastName].filter(Boolean).join(' ') || item.id)
        : (item.accountName || item.accountCode || item.id);

    const destroyPicker = () => {
        const el = document.getElementById('targetId');
        if ($ && el && $(el).data('select2')) $(el).select2('destroy');
        if (el) el.innerHTML = '';
    };

    /**
     * Rebuilds the picker for the chosen target type. A preserved option is injected first so that editing a target
     * whose account was archived - or simply is not on the first page - still shows what the row actually points at
     * instead of silently emptying the field.
     */
    const buildPicker = (preserved) => {
        const el = document.getElementById('targetId');
        const kind = value('targetType');
        if (!el) return;
        destroyPicker();

        if (preserved?.id) {
            const option = document.createElement('option');
            option.value = preserved.id;
            option.textContent = preserved.text || preserved.id;
            option.selected = true;
            el.appendChild(option);
        }

        const route = pickerTypes[kind];
        if (!$ || !route) return; // an unpickable type is left as an empty select rather than a fake free-text box

        $(el).select2({
            dropdownParent: $('#targetCanvas'),
            width: '100%',
            allowClear: true,
            placeholder: kind === 'contact' ? (L.SelectContact || '') : (L.SelectAccount || ''),
            minimumInputLength: 0,
            ajax: {
                url: `/CRM/Campaigns/api/${route}`,
                dataType: 'json',
                delay: 250,
                data: params => ({ search: params.term || '', page: params.page || 1, pageSize: 25 }),
                processResults: (payload, params) => {
                    const data = payload?.data || {};
                    const items = data.items || [];
                    const page = params.page || 1;
                    const pageSize = data.pageSize || 25;
                    return {
                        results: items.map(item => ({ id: item.id, text: pickerLabel(item, kind) })),
                        pagination: { more: page * pageSize < (data.total || 0) }
                    };
                }
            }
        });

        // The label travels with the row for audit. It is a snapshot of what the author saw, never a source of truth.
        $(el).off('change.fu11').on('change.fu11', () => {
            const chosen = $(el).select2('data')?.[0];
            const hidden = document.getElementById('targetDisplayName');
            if (hidden) hidden.value = chosen?.text || '';
        });
    };

    const resetTargetForm = () => {
        document.getElementById('targetForm')?.reset(); hideError('targetValidation');
        document.getElementById('campaignTargetId').value = '';
        document.getElementById('targetDisplayName').value = '';
        document.getElementById('targetCanvasTitle').textContent = L.CreateTarget;
        document.getElementById('targetType').disabled = false;
        buildPicker(null);
    };

    const populateTarget = row => {
        resetTargetForm(); document.getElementById('targetCanvasTitle').textContent = L.EditTarget;
        const values = { campaignTargetId:row.campaignTargetId, targetType:row.targetType, targetStatus:row.targetStatus, priorityLevel:row.priorityLevel, notes:row.notes, targetDisplayName:row.targetDisplayName };
        Object.entries(values).forEach(([id,v]) => { const el=document.getElementById(id); if(el) el.value=v??''; });
        // TargetType and TargetId are immutable on the aggregate - a different target is a different record.
        buildPicker({ id: row.targetId, text: row.targetDisplayName || row.targetId });
        document.getElementById('targetType').disabled = true;
        if ($) $('#targetId').prop('disabled', true);
        window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('targetCanvas')).show();
    };

    // FU11 - targetSource, selectionReason, reasonCodes, effectiveFrom/To, sourceReference*, exclusionReason and
    // externalReferences are no longer sent. The server fills the first four from what it knows and leaves the rest
    // null; omitting a field on update means "keep what the target already says", so nothing is erased by an edit.
    const targetPayload = editing => {
        const common = { targetDisplayName:nullable(value('targetDisplayName')), targetStatus:nullable(value('targetStatus')), priorityLevel:nullable(value('priorityLevel')), notes:nullable(value('notes')) };
        return editing ? common : { targetType:value('targetType'), targetId:value('targetId'), ...common };
    };

    const validateTarget = () => {
        const editing = !!value('campaignTargetId');
        if (!editing && (!value('targetType') || !value('targetId'))) return L.ValidationRequired;
        return null;
    };

    const saveTarget = async () => {
        hideError('targetValidation'); const invalid = validateTarget(); if (invalid) { showError('targetValidation',invalid); return; }
        const id=value('campaignTargetId');
        try {
            const payload=targetPayload(!!id); const url=id?`/CRM/Campaigns/api/${campaignId}/targets/${id}`:`/CRM/Campaigns/api/${campaignId}/targets`;
            await envelope(await fetch(url,{method:id?'PUT':'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)}));
            window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('targetCanvas')).hide();
            window.showToast?.(L.Save,'success'); await loadTargets();
        } catch(error) { showError('targetValidation',error.message); window.showToast?.(error.message,'error'); }
    };

    const snapshotRow = () => {
        const options=(vocabulary.targetTypes||[]).filter(x=>x!=='campaign-target').map(x=>`<option value="${esc(x)}">${esc(x)}</option>`).join('');
        const row=document.createElement('div'); row.className='snapshot-row border rounded p-2 mb-2';
        row.innerHTML=`<div class="row g-2"><div class="col-6"><select class="form-select form-select-sm snapshot-target-type">${options}</select></div><div class="col-6"><input class="form-control form-control-sm snapshot-target-id" placeholder="${esc(L.TargetId)}" /></div><div class="col-6"><input class="form-control form-control-sm snapshot-target-name" placeholder="${esc(L.CampaignName)}" /></div><div class="col-3"><select class="form-select form-select-sm snapshot-priority-level"><option value=""></option>${(vocabulary.targetPriorityLevels||[]).map(x=>`<option value="${esc(x)}">${esc(L['PriorityLevel_'+x]||x)}</option>`).join('')}</select></div><div class="col-3"><button type="button" class="btn btn-sm btn-label-danger remove-snapshot-row"><i class="bx bx-x"></i></button></div><div class="col-6"><input class="form-control form-control-sm snapshot-source-type" placeholder="SourceReferenceType" /></div><div class="col-6"><input class="form-control form-control-sm snapshot-source-id" placeholder="SourceReferenceId" /></div></div>`;
        document.getElementById('snapshotRows')?.appendChild(row);
    };

    const collectSnapshotItems = () => {
        const raw=value('snapshotJson'); if(raw) return JSON.parse(raw);
        return [...document.querySelectorAll('.snapshot-row')].map(row=>({targetType:row.querySelector('.snapshot-target-type').value,targetId:row.querySelector('.snapshot-target-id').value,targetDisplayName:nullable(row.querySelector('.snapshot-target-name').value.trim()),priorityLevel:nullable(row.querySelector('.snapshot-priority-level').value),sourceReferenceType:nullable(row.querySelector('.snapshot-source-type').value.trim()),sourceReferenceId:nullable(row.querySelector('.snapshot-source-id').value.trim())})).filter(x=>x.targetId);
    };

    const submitSnapshot = async () => {
        hideError('snapshotValidation'); let items;
        try { items=collectSnapshotItems(); } catch { showError('snapshotValidation',L.ValidationRequired); return; }
        const apply=document.getElementById('applyConsentFilter').checked;
        if (!items.length || !value('snapshotSourceType') || !value('snapshotSelectionReason') || (apply && (!value('consentChannel') || !value('consentPurpose')))) { showError('snapshotValidation',L.ValidationRequired); return; }
        const payload={sourceType:value('snapshotSourceType'),targetItems:items,selectionReason:value('snapshotSelectionReason'),applyConsentFilter:apply,sourceReferenceType:nullable(value('snapshotSourceReferenceType')),sourceReferenceId:nullable(value('snapshotSourceReferenceId')),consentChannel:nullable(value('consentChannel')),consentPurpose:nullable(value('consentPurpose')),effectiveAt:dateOrNull(value('snapshotEffectiveAt')),reasonCodes:csv('snapshotReasonCodes')};
        try {
            const result=await envelope(await fetch(`/CRM/Campaigns/api/${campaignId}/snapshot`,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)}));
            const host=document.getElementById('snapshotResult'); host.innerHTML=`<strong>${esc(L.SnapshotCreated)}</strong><br>${esc(L.SnapshotBatchId)}: <code>${esc(result.snapshotBatchId)}</code><br>${esc(L.CreatedCount)}: ${esc(result.createdCount)} · ${esc(L.ReconciledCount)}: ${esc(result.reconciledCount)} · ${esc(L.ExcludedCount)}: ${esc(result.excludedCount)}`; host.classList.remove('d-none');
            window.showToast?.(L.SnapshotCreated,'success'); await loadTargets();
        } catch(error) { const message=error.status===409?`${L.DifferentSourceConflict}: ${error.message}`:error.message; showError('snapshotValidation',message); window.showToast?.(message,'error'); }
    };

    setOptions('targetTypeFilter',vocabulary.targetTypes); setOptions('targetStatusFilter',vocabulary.targetStatuses,true,'TargetStatus_'); setOptions('targetSourceFilter',vocabulary.targetSources);
    // FU11 - the canvas offers only the target types that HAVE a picker, and only the statuses a human may set. Both
    // lists come from the contract (authorableTargetStatuses, targetPriorityLevels) so nothing is hardcoded here; the
    // API still accepts every type and status, the restriction is this screen's.
    setOptions('targetType',(vocabulary.targetTypes||[]).filter(x=>Object.prototype.hasOwnProperty.call(pickerTypes,x)),false);
    setOptions('targetStatus',vocabulary.authorableTargetStatuses||[],true,'TargetStatus_');
    setOptions('priorityLevel',vocabulary.targetPriorityLevels||[],true,'PriorityLevel_');
    document.getElementById('targetType')?.addEventListener('change',()=>buildPicker(null));

    /**
     * Golden slim filter chips. Select2 runs AFTER setOptions has filled the lists, because Select2 reads the <option>
     * list once and replacing innerHTML underneath a live widget leaves it showing a list that no longer exists.
     */
    function initFilterChips() {
        if (!$ || !$.fn.select2) return;
        $('#targetTypeFilter, #targetStatusFilter, #targetSourceFilter').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: true
            });
        });
    }

    document.getElementById('applyTargetFilters')?.addEventListener('click',loadTargets);
    // Reset clears the bar and reloads, so "reset" means the same thing here as everywhere else: show me everything.
    document.getElementById('resetTargetFilters')?.addEventListener('click',()=>{
        ['targetTypeFilter','targetStatusFilter','targetSourceFilter'].forEach(id=>{
            const el=document.getElementById(id); if(!el) return;
            el.value='';
            if ($ && $(el).hasClass('select2-hidden-accessible')) $(el).trigger('change.select2');
        });
        const batch=document.getElementById('snapshotBatchFilter'); if(batch) batch.value='';
        const archived=document.getElementById('targetIncludeArchived'); if(archived) archived.checked=true;
        loadTargets();
    });
    document.getElementById('saveTarget')?.addEventListener('click',saveTarget);
    document.getElementById('addSnapshotRow')?.addEventListener('click',snapshotRow);
    document.getElementById('submitSnapshot')?.addEventListener('click',submitSnapshot);
    document.getElementById('applyConsentFilter')?.addEventListener('change',event=>document.getElementById('consentNotAppliedWarning')?.classList.toggle('d-none',event.target.checked));
    document.addEventListener('click',async event=>{
        event.target.closest('.remove-snapshot-row')?.closest('.snapshot-row')?.remove();
        const edit=event.target.closest('.js-edit-target'); if(edit){try{populateTarget(await envelope(await fetch(`/CRM/Campaigns/api/${campaignId}/targets/${edit.dataset.id}`,{credentials:'same-origin'})));}catch(error){window.showToast?.(error.message,'error');}}
        const archive=event.target.closest('.js-archive-target'); if(archive) window.showConfirm?.(L.ArchiveTargetConfirm,async()=>{try{await envelope(await fetch(`/CRM/Campaigns/api/${campaignId}/targets/${archive.dataset.id}/archive`,{method:'POST',credentials:'same-origin'}));window.showToast?.(L.RecordArchived,'success');await loadTargets();}catch(error){window.showToast?.(error.message,'error');}},{type:'warning',confirmButtonText:L.ArchiveTarget});
    });
    document.getElementById('archiveCampaign')?.addEventListener('click',()=>window.showConfirm?.(L.ArchiveCampaignConfirm,async()=>{try{await envelope(await fetch(`/CRM/Campaigns/api/${campaignId}/archive`,{method:'POST',credentials:'same-origin'}));window.showToast?.(L.RecordArchived,'success');window.location.reload();}catch(error){window.showToast?.(error.message,'error');}},{type:'warning',confirmButtonText:L.ArchiveCampaign}));
    snapshotRow(); loadTargets();
})(window, document);
