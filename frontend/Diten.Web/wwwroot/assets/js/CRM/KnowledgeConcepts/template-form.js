/**
 * SCMM-10-UI-refine (MOD-0162) Chain Template — Golden Compact full-page create/edit (Not 5), replacing the Slim
 * offcanvas. Ports the SCMM-10 branched builder onto a route-based page and submits through the same-origin
 * concept-chain-templates proxy.
 *
 * SCMM-10-MOD-C (template-level Moderator/ForWhom): Moderator and ForWhom moved OFF the step and ONTO the template
 * (Identity & Classification). Moderator = a single content-moderator-role published value (store ValueCode →
 * ModeratorRoleType; who presents the chain). ForWhom = an AudienceProfile multi-select (store id[] →
 * ForWhomAudienceProfileIds; the chain's target audience). The step-level AllowedRoleRefs / AudienceDimensionRefs are
 * gone (backend WP-A removed them). Branches are a Tasks-checklist compose-then-add builder (AUD-UI-6 pattern):
 * `.diten-checkitem` step rows + a compose-row (Concept Type + Min/Max + Add).
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;
    const base = '/CRM/KnowledgeConcepts/api';
    const MODERATOR_SET_CODE = 'content-moderator-role';
    const headers = { Accept: 'application/json' };
    const jsonHeaders = { Accept: 'application/json', 'Content-Type': 'application/json' };

    let L = {};
    try { L = JSON.parse(document.getElementById('concept-template-l10n')?.textContent || '{}'); } catch (e) { L = {}; }

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const toDateInput = v => v ? new Date(v).toISOString().slice(0, 10) : '';
    const fromDateInput = v => v ? new Date(`${v}T00:00:00Z`).toISOString() : null;
    const todayInput = () => new Date().toISOString().slice(0, 10);
    const setVal = (id, v) => { const el = document.getElementById(id); if (el) el.value = v == null ? '' : String(v); };
    const val = id => norm(document.getElementById(id)?.value);
    const showAlert = msg => { const el = document.getElementById('conceptTemplateFormAlert'); if (!el) return; el.textContent = msg || ''; el.classList.toggle('d-none', !msg); };

    const envelope = async res => {
        const body = await res.json().catch(() => ({}));
        if (!res.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { handled: true });
        return body.data;
    };
    const getJson = async path => envelope(await fetch(`${base}${path}`, { credentials: 'same-origin', headers }));

    // ─── reference data ─────────────────────────────────────────────────────────
    let types = [];             // { conceptTypeId, subjectId, conceptTypeCode, conceptTypeName, isArchived }
    let audienceOptions = [];   // { value: audienceProfileId, text: "code — name" }
    let moderatorOptions = [];  // { value: ValueCode, text: label }  (content-moderator-role published values)
    const typeNameById = {};
    const typeCodeById = {};       // conceptTypeId → conceptTypeCode (SCMM-10-MOD-D2 step-card code badge)
    const typeNameOnlyById = {};   // conceptTypeId → conceptTypeName (title = name only)
    const subjectLabelById = {};
    const labelType = id => typeNameById[id] || id || '';
    const codeOf = id => typeCodeById[id] || '';
    const nameOf = id => typeNameOnlyById[id] || labelType(id);
    const moderatorLabel = code => moderatorOptions.find(o => String(o.value) === String(code))?.text || code || '';
    const typeOptionsFor = subjectId => types
        .filter(t => String(t.subjectId) === String(subjectId) && !t.isArchived)
        .map(t => ({ value: t.conceptTypeId, text: `${t.conceptTypeCode} — ${t.conceptTypeName}` }));

    // A stored value no longer offered (an archived subject, a retired status) is kept so the form never silently
    // drops it; its label falls back to a resolver, then to the raw value.
    const fillSelect = (id, options, withEmpty, current, currentLabel) => {
        const el = document.getElementById(id);
        if (!el) return;
        const list = (options || []).slice();
        if (current && !list.some(o => String(o.value) === String(current))) list.unshift({ value: current, text: currentLabel || current });
        el.innerHTML = (withEmpty ? '<option value=""></option>' : '') + list.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    // ForWhom multi-select: mark selected audience-profile ids, preserving any stored ref that is no longer offered.
    const fillForWhom = selectedIds => {
        const el = document.getElementById('tplForWhom');
        if (!el) return;
        const sel = (selectedIds || []).map(String);
        const known = audienceOptions.map(o =>
            `<option value="${esc(o.value)}"${sel.includes(String(o.value)) ? ' selected' : ''}>${esc(o.text)}</option>`);
        const extra = sel.filter(v => !audienceOptions.some(o => String(o.value) === v))
            .map(v => `<option value="${esc(v)}" selected>${esc(v)}</option>`);
        el.innerHTML = known.concat(extra).join('');
    };
    const initSelect2 = (sel) => { if ($ && $.fn.select2) $(sel).each(function () { if (!$(this).hasClass('select2-hidden-accessible')) $(this).select2({ width: '100%', placeholder: this.getAttribute('data-placeholder') || '' }); }); };

    // ─── builder model + spine ──────────────────────────────────────────────────
    let branches = [];         // [{ name, steps:[{ conceptTypeId, min, max }] }]
    let templateReadOnly = false;
    // SCMM-10-MOD-D/E: one branch shown per page (Bootstrap .pagination); a branch's steps are a vertical row list
    // (no step paging — the list flows down, never a horizontal scroll). branchPage = the visible branch.
    let branchPage = 0;
    let sortable = null;   // SortableJS instance over the visible branch's step list
    const spineFromBranches = () => {
        const seen = new Set(); const out = [];
        branches.forEach(b => b.steps.forEach(s => { const id = String(s.conceptTypeId || ''); if (id && !seen.has(id)) { seen.add(id); out.push(id); } }));
        return out;
    };
    const bumpVersion = v => {
        const m = /^v?(\d+)(?:\.(\d+))?$/i.exec(norm(v));
        if (!m) return norm(v) ? `${norm(v)}-2` : 'v2';
        const major = parseInt(m[1], 10);
        return m[2] != null ? `v${major}.${parseInt(m[2], 10) + 1}` : `v${major + 1}`;
    };

    const cardinality = s => `${s.min == null || s.min === '' ? 1 : s.min}–${s.max == null || s.max === '' ? '∞' : s.max}`;

    // ─── Branches builder (SCMM-10-MOD-E: Tasks-checklist vertical step rows) ─────────────────────────────────────
    // The visible branch (one per page) lists its steps as full-width `card border shadow-none` rows: drag grip
    // (SortableJS handle) + ↑↓ + order # + name + Concept-Type code badge + min–max chip + Details (collapse → Min/Max
    // only) + remove. Steps stay refs-free {conceptTypeId, min, max}; Moderator/ForWhom remain template-level and are
    // not touched. No horizontal scroll (the list flows down).
    const stepRow = (bi, si, s, lastIndex, ro) => {
        const detId = `stepDet-${bi}-${si}`;
        const handle = ro ? '' : `<i class="bx bx-grid-vertical text-muted js-step-handle flex-shrink-0" role="button" aria-label="${esc(L.MoveUp || '')}"></i>`;
        // SCMM-10-MOD-F: TaskCenter checklist button look — text buttons (btn-text-*) with icon-base/icon-sm glyphs.
        const up = `<button type="button" class="btn btn-icon btn-text-secondary js-step-move" data-b="${bi}" data-s="${si}" data-delta="-1" title="${esc(L.MoveUp || '')}" aria-label="${esc(L.MoveUp || '')}" ${ro || si === 0 ? 'disabled' : ''}><i class="icon-base bx bx-chevron-up icon-sm"></i></button>`;
        const down = `<button type="button" class="btn btn-icon btn-text-secondary js-step-move" data-b="${bi}" data-s="${si}" data-delta="1" title="${esc(L.MoveDown || '')}" aria-label="${esc(L.MoveDown || '')}" ${ro || si === lastIndex ? 'disabled' : ''}><i class="icon-base bx bx-chevron-down icon-sm"></i></button>`;
        const moves = ro ? '' : `<span class="d-flex flex-column flex-shrink-0">${up}${down}</span>`;
        const details = `<button type="button" class="btn btn-icon btn-text-secondary flex-shrink-0" data-bs-toggle="collapse" data-bs-target="#${detId}" aria-expanded="false" aria-controls="${detId}" title="${esc(L.Details || '')}" aria-label="${esc(L.Details || '')}"><i class="icon-base bx bx-slider-alt icon-sm"></i></button>`;
        const rm = ro ? '' : `<button type="button" class="btn btn-icon btn-text-danger js-step-remove flex-shrink-0" data-b="${bi}" data-s="${si}" title="${esc(L.RemoveStep || '')}" aria-label="${esc(L.RemoveStep || '')}"><i class="icon-base bx bx-trash icon-sm"></i></button>`;
        const minmax = ro ? '' : `<div class="collapse" id="${detId}">
                    <div class="d-flex gap-2 align-items-end mt-2">
                        <div><label class="form-label small mb-0">${esc(L.MinSelection || 'Min')}</label><input type="number" min="0" step="1" class="form-control form-control-sm js-step-min" data-b="${bi}" data-s="${si}" value="${esc(String(s.min == null || s.min === '' ? 1 : s.min))}" style="width:5.5rem"></div>
                        <div><label class="form-label small mb-0">${esc(L.MaxSelection || 'Max')}</label><input type="number" min="1" step="1" class="form-control form-control-sm js-step-max" data-b="${bi}" data-s="${si}" value="${s.max == null || s.max === '' ? '' : esc(String(s.max))}" style="width:5.5rem"></div>
                    </div>
                </div>`;
        return `<div class="card border shadow-none js-step-row" data-b="${bi}" data-s="${si}" data-ct="${esc(String(s.conceptTypeId))}">
                <div class="card-body p-2">
                    <div class="d-flex align-items-center gap-2 flex-wrap">
                        ${handle}${moves}
                        <span class="badge bg-label-secondary rounded-pill flex-shrink-0">${si + 1}</span>
                        <span class="fw-medium text-truncate flex-grow-1" title="${esc(nameOf(s.conceptTypeId))}">${esc(nameOf(s.conceptTypeId))}</span>
                        <span class="badge bg-label-primary flex-shrink-0">${esc(codeOf(s.conceptTypeId))}</span>
                        <span class="badge bg-label-secondary flex-shrink-0 js-step-chip" data-chip-b="${bi}" data-chip-s="${si}">${esc(cardinality(s))}</span>
                        ${details}${rm}
                    </div>
                    ${minmax}
                </div>
            </div>`;
    };
    // A Bootstrap .pagination prev/·info·/next (existing classes only) — used for the branch pager.
    const pager = (kindClass, page, pages, label, bi) => {
        if (pages <= 1) return '';
        const b = bi == null ? '' : ` data-b="${bi}"`;
        const prev = `<li class="page-item ${page <= 0 ? 'disabled' : ''}"><button type="button" class="page-link ${kindClass}"${b} data-page="${page - 1}" aria-label="${esc(L.Prev || 'Previous')}"><i class="bx bx-chevron-left"></i></button></li>`;
        const info = `<li class="page-item disabled"><span class="page-link">${esc(label)}</span></li>`;
        const next = `<li class="page-item ${page >= pages - 1 ? 'disabled' : ''}"><button type="button" class="page-link ${kindClass}"${b} data-page="${page + 1}" aria-label="${esc(L.Next || 'Next')}"><i class="bx bx-chevron-right"></i></button></li>`;
        return `<nav class="mt-2"><ul class="pagination pagination-sm mb-0 justify-content-center">${prev}${info}${next}</ul></nav>`;
    };
    // The add-step row (bottom of the branch): Concept Type + Min/Max + Add step (the compose model, Tasks add-row feel).
    const addStepRow = (bi, opts) => `<div class="card border shadow-none">
                <div class="card-body p-2 d-flex gap-2 align-items-end flex-wrap">
                    <div class="flex-grow-1">
                        <label class="form-label small mb-0">${esc(L.AddStep || '')}</label>
                        <select class="form-select form-select-sm js-branch-type-picker" data-b="${bi}" data-placeholder="${esc(L.ConceptType || L.SelectOption || '')}">
                            <option value="">${esc(L.SelectOption || '')}</option>
                            ${opts.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('')}
                        </select>
                    </div>
                    <div><label class="form-label small mb-0">${esc(L.MinSelection || 'Min')}</label><input type="number" min="0" step="1" value="1" class="form-control form-control-sm js-compose-min" data-b="${bi}" aria-label="${esc(L.MinSelection || 'Min')}" style="width:5.5rem"></div>
                    <div><label class="form-label small mb-0">${esc(L.MaxSelection || 'Max')}</label><input type="number" min="1" step="1" class="form-control form-control-sm js-compose-max" data-b="${bi}" aria-label="${esc(L.MaxSelection || 'Max')}" style="width:5.5rem"></div>
                    <button type="button" class="btn btn-label-primary btn-sm js-branch-add-step" data-b="${bi}"><i class="bx bx-plus me-1"></i>${esc(L.AddToSequence || '')}</button>
                </div>
            </div>`;
    // SortableJS drag reorder over the visible branch's step list (repo idiom: window.Sortable + handle + onEnd). ↑↓
    // buttons do the same, so keyboard/AT users are covered when drag is unavailable.
    const wireSortable = () => {
        if (sortable) { try { sortable.destroy(); } catch (e) { /* already gone */ } sortable = null; }
        if (templateReadOnly || !window.Sortable) return;
        const list = document.getElementById('tplStepList');
        if (!list) return;
        sortable = window.Sortable.create(list, {
            handle: '.js-step-handle',
            animation: 150,
            onEnd: () => {
                const bi = branchPage;
                if (!branches[bi]) return;
                const order = Array.from(list.querySelectorAll('.js-step-row')).map(el => el.dataset.ct);
                branches[bi].steps.sort((a, b) => order.indexOf(String(a.conceptTypeId)) - order.indexOf(String(b.conceptTypeId)));
                renderBranches();
            }
        });
    };
    const renderBranches = () => {
        const host = document.getElementById('tplBranches');
        const empty = document.getElementById('tplBranchesEmpty');
        if (!host) return;
        const subjectId = val('tplSubjectId');
        const ro = templateReadOnly;
        const total = branches.length;
        empty?.classList.toggle('d-none', total > 0);
        setVal('tplOrderedConceptTypes', spineFromBranches().join(','));
        if (total === 0) { host.innerHTML = ''; wireSortable(); return; }

        branchPage = Math.min(Math.max(0, branchPage), total - 1);
        const bi = branchPage;                                       // one branch per page
        const b = branches[bi];
        const lastIndex = b.steps.length - 1;
        const opts = typeOptionsFor(subjectId).filter(o => !b.steps.some(s => String(s.conceptTypeId) === String(o.value)));
        const rows = b.steps.map((s, si) => stepRow(bi, si, s, lastIndex, ro)).join('');
        const listInner = rows || `<div class="text-muted small">${esc(L.BranchStepsEmpty || '')}</div>`;

        host.innerHTML = `
            <div class="card border shadow-none">
                <div class="card-body p-3">
                    <div class="d-flex align-items-center gap-2 mb-2">
                        <span class="badge bg-primary rounded-pill flex-shrink-0">${bi + 1}</span>
                        <input type="text" class="form-control form-control-sm js-branch-name flex-grow-1" data-b="${bi}" value="${esc(b.name || '')}" placeholder="${esc(L.BranchNamePlaceholder || '')}" ${ro ? 'disabled' : ''}>
                        <span class="badge bg-label-secondary flex-shrink-0">${b.steps.length} ${esc(L.Steps || '')}</span>
                        <button type="button" class="btn btn-icon btn-text-danger js-branch-remove flex-shrink-0" data-b="${bi}" title="${esc(L.RemoveBranch || '')}" ${ro ? 'disabled' : ''}><i class="icon-base bx bx-trash icon-sm"></i></button>
                    </div>
                    <div id="tplStepList" class="d-flex flex-column gap-2 mb-2">${listInner}</div>
                    ${ro ? '' : addStepRow(bi, opts)}
                </div>
            </div>
            ${pager('js-branch-page', branchPage, total, `${L.BranchLabel || 'Branch'} ${branchPage + 1} / ${total}`, null)}`;
        wireSortable();
    };

    // ─── submit ─────────────────────────────────────────────────────────────────
    const submit = async () => {
        const id = val('templateFormId');
        const error = document.getElementById('tplSequenceError');
        const spine = spineFromBranches();
        if (spine.length < 2) {
            if (error) { error.textContent = L.SequenceMinTwo || ''; error.classList.remove('d-none'); }
            throw Object.assign(new Error(L.SequenceMinTwo || ''), { handled: true });
        }
        error?.classList.add('d-none');

        const branchPayload = branches.filter(b => b.steps.length > 0).map((b, i) => ({
            branchCode: `BR${i + 1}`,
            branchName: norm(b.name) || null,
            sortOrder: i,
            steps: b.steps.map(s => ({
                conceptTypeId: String(s.conceptTypeId),
                minSelection: Number.isFinite(Number(s.min)) ? Number(s.min) : 1,
                maxSelection: (s.max === '' || s.max == null) ? null : Number(s.max)
            }))
        }));

        // SCMM-10-MOD-C: template-level Moderator (single ValueCode, blank = unspecified) + ForWhom (audience ids).
        const moderator = val('tplModeratorRoleType');
        const forWhom = $ ? ($('#tplForWhom').val() || []) : [];

        const payload = {
            chainName: val('tplChainName'),
            orderedConceptTypes: spine,
            branches: branchPayload,
            moderatorRoleType: moderator || null,
            forWhomAudienceProfileIds: forWhom,
            effectiveFrom: fromDateInput(val('tplEffectiveFrom')),
            description: val('tplDescription') || null,
            status: val('tplStatus') || null,
            chainVersion: val('tplChainVersion') || null,
            effectiveTo: fromDateInput(val('tplEffectiveTo'))
        };
        if (!id) { payload.subjectId = val('tplSubjectId'); payload.chainCode = val('tplChainCode'); }

        await envelope(await fetch(id ? `${base}/concept-chain-templates/${id}` : `${base}/concept-chain-templates`, {
            method: id ? 'PUT' : 'POST', credentials: 'same-origin', headers: jsonHeaders, body: JSON.stringify(payload)
        }));
        window.showToast?.(id ? (L.RecordUpdated || '') : (L.RecordCreated || ''), 'success');
        window.location.href = '/CRM/KnowledgeConcepts';
    };

    const setIdentityDisabled = disabled => {
        ['tplModeratorRoleType', 'tplForWhom'].forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.disabled = !!disabled;
            if ($ && $(el).hasClass('select2-hidden-accessible')) $(el).trigger('change.select2');
        });
    };

    const startNewVersion = () => {
        templateReadOnly = false;
        setVal('templateFormId', '');
        setVal('tplStatus', 'draft'); if ($) $('#tplStatus').trigger('change');
        setVal('tplEffectiveFrom', todayInput());
        setVal('tplEffectiveTo', '');
        setVal('tplChainVersion', bumpVersion(val('tplChainVersion')));
        document.getElementById('tplChainCode').readOnly = false;
        document.getElementById('tplChainCodeHint')?.classList.remove('d-none');
        document.getElementById('conceptTemplateFrozenNote')?.classList.add('d-none');
        document.getElementById('btnTplNewVersion')?.classList.add('d-none');
        document.getElementById('btnSaveConceptTemplate')?.classList.remove('d-none');
        setIdentityDisabled(false);
        document.getElementById('btnTplAddBranch')?.removeAttribute('disabled');
        renderBranches();
    };

    // ─── load ───────────────────────────────────────────────────────────────────
    const loadModeratorOptions = async () => {
        try {
            const data = await getJson(`/reference-data/${encodeURIComponent(MODERATOR_SET_CODE)}/values`);
            moderatorOptions = (data?.items || []).map(v => {
                const code = norm(v.code || v.valueCode || v.value);
                return {
                    value: code,
                    text: norm(v.label || v.displayName || v.text) || code,
                    isDeprecated: v.isDeprecated === true || v.isActive === false,
                    sortOrder: Number(v.sortOrder || 0)
                };
            }).filter(o => o.value).sort((a, b) => a.sortOrder - b.sortOrder || a.text.localeCompare(b.text));
        } catch { moderatorOptions = []; }
    };
    const loadRefs = async () => {
        // Subjects come back with archived ones so a disabled edit field still resolves a friendly label; the create
        // dropdown offers only live subjects. Audience profiles feed the ForWhom picker; moderator roles the Moderator.
        const [subs, tps, auds, , contract] = await Promise.all([
            getJson('/subjects?includeArchived=true').catch(() => ({ items: [] })),
            getJson('/concept-types?includeArchived=true').catch(() => ({ items: [] })),
            getJson('/audience-profiles?includeArchived=false').catch(() => ({ items: [] })),
            loadModeratorOptions(),
            getJson('/contract').catch(() => null)
        ]);
        types = (tps?.items || []).map(t => ({ conceptTypeId: t.conceptTypeId, subjectId: t.subjectId, conceptTypeCode: t.conceptTypeCode, conceptTypeName: t.conceptTypeName, isArchived: t.isArchived }));
        types.forEach(t => {
            typeNameById[t.conceptTypeId] = `${t.conceptTypeCode} — ${t.conceptTypeName}`;
            typeCodeById[t.conceptTypeId] = t.conceptTypeCode;
            typeNameOnlyById[t.conceptTypeId] = t.conceptTypeName;
        });
        audienceOptions = (auds?.items || []).filter(a => !a.isArchived).map(a => ({ value: a.audienceProfileId, text: `${a.profileCode} — ${a.profileName}` }));
        (subs?.items || []).forEach(s => { subjectLabelById[s.subjectId] = `${s.subjectCode} — ${s.subjectName}`; });
        const subjectOptions = (subs?.items || []).filter(s => !s.isArchived).map(s => ({ value: s.subjectId, text: subjectLabelById[s.subjectId] }));
        // "archived" is a lifecycle action, never a status the editor sets (a save carrying it is a backend 400).
        const statuses = (contract?.vocabularies?.chainStatuses || ['draft', 'review', 'approved', 'published', 'inactive', 'archived'])
            .filter(v => v !== 'archived').map(v => ({ value: v, text: v }));
        return { subjectOptions, statuses };
    };

    const suggestCode = () => `CHN-${new Date().getUTCFullYear()}-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;

    const init = async () => {
        if (window.flatpickr) document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));
        const editId = document.getElementById('templateEditHeader')?.getAttribute('data-template-id');
        let refs;
        try { refs = await loadRefs(); }
        catch (e) { showAlert(L.ErrorState); return; }

        const row = editId ? await getJson(`/concept-chain-templates/${editId}`).catch(() => null) : null;
        if (editId && !row) { showAlert(L.ErrorState); document.getElementById('btnSaveConceptTemplate')?.setAttribute('disabled', 'disabled'); return; }
        fillSelect('tplSubjectId', refs.subjectOptions, true, row?.subjectId, subjectLabelById[row?.subjectId]);
        fillSelect('tplStatus', refs.statuses, false, row?.status, row?.status);
        // SCMM-10-MOD-C: Moderator (single, blank = unspecified) + ForWhom (audience-profile multi).
        fillSelect('tplModeratorRoleType', moderatorOptions, true, row?.moderatorRoleType, moderatorLabel(row?.moderatorRoleType));
        fillForWhom(row?.forWhomAudienceProfileIds);
        initSelect2('#tplSubjectId,#tplStatus,#tplModeratorRoleType,#tplForWhom');

        setVal('templateFormId', row?.conceptChainTemplateId || '');
        setVal('tplSubjectId', row?.subjectId || ''); if ($) $('#tplSubjectId').trigger('change');
        setVal('tplChainCode', row ? row.chainCode : suggestCode());
        setVal('tplChainName', row?.chainName || '');
        setVal('tplDescription', row?.description || '');
        setVal('tplChainVersion', row?.chainVersion || '');
        setVal('tplModeratorRoleType', row?.moderatorRoleType || ''); if ($) $('#tplModeratorRoleType').trigger('change.select2');
        if ($) $('#tplForWhom').trigger('change.select2');
        setVal('tplStatus', row?.status || 'draft'); if ($) $('#tplStatus').trigger('change');
        setVal('tplEffectiveFrom', row ? toDateInput(row.effectiveFrom) : todayInput());
        setVal('tplEffectiveTo', toDateInput(row?.effectiveTo));

        branches = (row?.branches || []).map(b => ({
            name: b.branchName || '',
            steps: (b.steps || []).map(s => ({
                conceptTypeId: s.conceptTypeId,
                min: s.minSelection ?? 1,
                max: s.maxSelection ?? null
            }))
        }));
        if (!row && branches.length === 0) branches = [{ name: '', steps: [] }];

        const frozen = norm(row?.status) === 'published';
        templateReadOnly = frozen;
        document.getElementById('conceptTemplateFrozenNote')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnTplNewVersion')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnSaveConceptTemplate')?.classList.toggle('d-none', frozen);
        document.getElementById('btnTplAddBranch').disabled = frozen;
        // SCMM-10-MOD-C (D-f): a published template's Moderator/ForWhom freeze with the rest (edit → new version).
        setIdentityDisabled(frozen);

        // Subject + code are stable across versions (update contract carries neither); the code hint hides on edit.
        document.getElementById('tplSubjectId').disabled = !!row;
        if ($) $('#tplSubjectId').trigger('change.select2');
        document.getElementById('tplChainCode').readOnly = !!row;
        document.getElementById('tplChainCodeHint')?.classList.toggle('d-none', !!row);
        const crumb = document.getElementById('tplEditCrumb');
        if (crumb && row) crumb.textContent = row.chainCode || (L.EditTemplate || '');

        renderBranches();
    };

    // ─── event wiring ─────────────────────────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', () => {
        void init();

        // Subject change (create) rebuilds the builder — types are subject-scoped. Moderator/ForWhom are template-level
        // and are deliberately left untouched.
        const subj = document.getElementById('tplSubjectId');
        const onSubjectChange = () => { if (templateReadOnly) return; branches = [{ name: '', steps: [] }]; branchPage = 0; renderBranches(); };
        subj?.addEventListener('change', () => { if (!subj.disabled) onSubjectChange(); });
        if ($) $(subj).on('change', () => { if (!subj.disabled) onSubjectChange(); });

        // Structural builder actions.
        document.addEventListener('click', event => {
            if (event.target.closest('#btnTplAddBranch')) { event.preventDefault(); if (templateReadOnly) return; branches.push({ name: '', steps: [] }); branchPage = branches.length - 1; renderBranches(); return; }
            if (event.target.closest('#btnTplNewVersion')) { event.preventDefault(); startNewVersion(); return; }
            const br = event.target.closest('.js-branch-remove');
            if (br) { event.preventDefault(); if (templateReadOnly) return; branches.splice(Number(br.dataset.b), 1); renderBranches(); return; }
            // SCMM-10-MOD-D/E: branch pager (Bootstrap .page-link; a .disabled .page-link swallows the click).
            const bPage = event.target.closest('.js-branch-page');
            if (bPage) { event.preventDefault(); branchPage = Number(bPage.dataset.page); renderBranches(); return; }
            const addStep = event.target.closest('.js-branch-add-step');
            if (addStep) {
                event.preventDefault(); if (templateReadOnly) return;
                const bi = Number(addStep.dataset.b);
                const picker = document.querySelector(`.js-branch-type-picker[data-b="${bi}"]`);
                const v = norm(picker?.value);
                if (!v || branches[bi].steps.some(s => String(s.conceptTypeId) === v)) return;
                const minEl = document.querySelector(`.js-compose-min[data-b="${bi}"]`);
                const maxEl = document.querySelector(`.js-compose-max[data-b="${bi}"]`);
                const minRaw = norm(minEl?.value);
                const maxRaw = norm(maxEl?.value);
                branches[bi].steps.push({
                    conceptTypeId: v,
                    min: minRaw === '' ? 1 : Math.max(0, Number(minRaw)),
                    max: maxRaw === '' ? null : Math.max(1, Number(maxRaw))
                });
                renderBranches();
                return;
            }
            const mv = event.target.closest('.js-step-move');
            if (mv) {
                event.preventDefault(); if (templateReadOnly) return;
                const bi = Number(mv.dataset.b), si = Number(mv.dataset.s), target = si + Number(mv.dataset.delta);
                const steps = branches[bi].steps;
                if (target < 0 || target >= steps.length) return;
                const [it] = steps.splice(si, 1); steps.splice(target, 0, it); renderBranches();
                return;
            }
            const rm = event.target.closest('.js-step-remove');
            if (rm) { event.preventDefault(); if (templateReadOnly) return; branches[Number(rm.dataset.b)].steps.splice(Number(rm.dataset.s), 1); renderBranches(); return; }
        });

        // branch-name + a step row's Details Min/Max update the model WITHOUT re-render (keeps the collapse open and the
        // input focused); a Min/Max change also refreshes that row's cardinality chip in place. The add-step compose
        // Min/Max are read at Add time, so they need no per-keystroke sync.
        document.getElementById('tplBranches')?.addEventListener('input', event => {
            const el = event.target;
            if (!el?.dataset || el.dataset.b == null) return;
            const bi = Number(el.dataset.b);
            if (!branches[bi]) return;
            if (el.classList.contains('js-branch-name')) { branches[bi].name = el.value; return; }
            if (el.dataset.s == null) return;
            const s = branches[bi].steps[Number(el.dataset.s)];
            if (!s) return;
            if (el.classList.contains('js-step-min')) s.min = el.value === '' ? 1 : Math.max(0, Number(el.value));
            else if (el.classList.contains('js-step-max')) s.max = el.value === '' ? null : Math.max(1, Number(el.value));
            else return;
            const chip = document.querySelector(`.js-step-chip[data-chip-b="${bi}"][data-chip-s="${el.dataset.s}"]`);
            if (chip) chip.textContent = cardinality(s);
        });

        // Save (JS submit; the button is a form submit but we own the flow).
        const formEl = document.getElementById('conceptTemplateForm');
        formEl?.addEventListener('submit', async event => {
            event.preventDefault();
            if (!formEl.checkValidity()) { formEl.reportValidity(); return; }
            try { await submit(); }
            catch (err) { if (!err?.handled) showAlert(err.message || L.ErrorState); }
        });
    });
})(window, document);
