/**
 * MOD-0167-FU04 Strategy Template form — the four EMBEDDED binding repeaters.
 *
 *  - "who"          : segment bindings      (MOD-0167 FU02, read-only picker)
 *  - "how often"    : frequency intent      (policy reference | declared intent | none — NEVER writes a policy)
 *  - "what"         : product lines → SKU % (MDM global product + gsku pickers, live total display)
 *  - "which story"  : content bindings      (MOD-0162 knowledge path / engagement journey, published only)
 *
 * Every option list comes from the CONTRACT (bootstrap payload) or from an EXISTING list endpoint through the
 * same-origin proxy. There is no hardcoded status, mode, frequency or product list anywhere in this file, and a picker
 * the actor may not browse is DISABLED with a stated reason — it never degrades into a free-text GUID box.
 *
 * The live percentage total is a DISPLAY only. It never blocks the save and it never normalises a number: the runtime
 * decides, and it refuses anything that is not exactly 100.00 (showing the computed total back).
 */
(function (window, document) {
    'use strict';
    const form = document.getElementById('strategyTemplateForm');
    if (!form) return;

    const L = window.StrategyTemplatesL10n || window.L10n || {};
    const bootstrapEl = document.getElementById('strategyTemplateFormBootstrap');
    let cfg = {};
    try { cfg = JSON.parse(bootstrapEl?.textContent || '{}'); }
    catch (error) { console.error('[StrategyTemplates] Form bootstrap could not be parsed.', error); }

    const endpoint = cfg.endpoint || '/CRM/StrategyTemplates/api';
    const frozen = cfg.areBindingsFrozen === true;
    const pickers = Array.isArray(cfg.availablePickers) ? cfg.availablePickers : [];
    const can = name => pickers.indexOf(name) >= 0;
    const total100 = Number(cfg.requiredAllocationTotal ?? 100);

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    // Trim-or-empty, matching index.js. It was USED by the right-panel checklist/recipe below but never defined here, so
    // updateSidePanel threw a ReferenceError before it could paint any check icon — which is why the BÖLÜMLER glyphs
    // never showed. Defining it lets updateSidePanel run and setCheck stamp each row's bx-* icon.
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const el = id => document.getElementById(id);
    const parse = (json, fallback) => { try { const v = JSON.parse(json || ''); return v ?? fallback; } catch (e) { return fallback; } };
    const round2 = n => Math.round((Number(n) || 0) * 100) / 100;

    // ----- state, seeded from the hidden inputs the server rendered -----
    const state = {
        segments: parse(el('SegmentBindingsJson')?.value, []) || [],
        frequency: parse(el('FrequencyIntentJson')?.value, null) || { mode: 'none' },
        products: parse(el('ProductLinesJson')?.value, []) || [],
        contents: parse(el('ContentBindingsJson')?.value, []) || []
    };

    // ----- option sources: every one is an EXISTING endpoint, proxied same-origin -----
    const options = { segment: [], policy: [], path: [], journey: [], product: [], gsku: [] };

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    const load = async (url, map) => {
        try {
            const data = await envelope(await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } }));
            const items = data?.items || data?.Items || [];
            return items.map(map).filter(o => o.id);
        } catch (error) {
            // A picker that cannot be read stays empty and says so; it never invents options.
            console.warn('[StrategyTemplates] Picker load failed:', url, error);
            return [];
        }
    };

    const loadOptions = async () => {
        const jobs = [];
        if (can('segment')) {
            jobs.push(load(`${endpoint}/segments?includeArchived=false`, r => ({
                id: r.segmentId || r.id,
                text: `${r.segmentCode || ''} — ${r.segmentName || ''}`.trim(),
                // WP-ST-EDIT-G — code + name split out so the display row can show the name with the SEG code beneath it.
                code: r.segmentCode || '',
                name: r.segmentName || '',
                subjectType: r.subjectType,
                archived: r.isArchived === true
            })).then(x => { options.segment = x.filter(o => !o.archived); }));
        }
        if (can('frequency-policy')) {
            jobs.push(load(`${endpoint}/visit-frequency-policies`, r => ({
                id: r.policyId || r.id,
                text: `${r.policyCode || ''} — ${r.policyName || ''}`.trim(),
                status: r.status
            })).then(x => { options.policy = x.filter(o => o.status === 'active'); }));
        }
        if (can('knowledge-path')) {
            jobs.push(load(`${endpoint}/knowledge-paths`, r => ({
                id: r.pathId || r.id,
                text: `${r.pathCode || ''} — ${r.pathName || ''}`.trim(),
                status: r.pathStatus
            })).then(x => { options.path = x.filter(o => o.status === 'published'); }));
        }
        if (can('content-engagement-journey')) {
            jobs.push(load(`${endpoint}/content-engagement-journeys`, r => ({
                id: r.journeyId || r.id,
                text: `${r.journeyCode || ''} — ${r.journeyName || ''}`.trim(),
                status: r.journeyStatus
            })).then(x => { options.journey = x.filter(o => o.status === 'published'); }));
        }
        if (can('global-product')) {
            jobs.push(load(`${endpoint}/global-products?pageSize=200`, r => ({
                id: r.id,
                text: `${r.canonicalCode || ''} — ${r.globalProductName || ''}`.trim()
            })).then(x => { options.product = x; }));
        }
        if (can('gsku')) {
            jobs.push(load(`${endpoint}/gskus?pageSize=200`, r => ({
                id: r.id,
                text: r.gskuCanonicalCode || r.canonicalCode || String(r.id)
            })).then(x => { options.gsku = x; }));
        }
        await Promise.all(jobs);
    };

    // ---------------- SubjectType — DERIVED, not chosen (WP-ST-EDIT-C) ----------------
    // The manual "Özne tipi" select is gone. On EDIT the type is immutable — it keeps the server value. On CREATE it is
    // derived from the FIRST bound segment: empty (so the picker offers every segment) until one is chosen, then locked
    // to that segment's subjectType so every further segment must be the same type (homogeneous; the backend also
    // refuses a mismatch). The value is mirrored into the hidden #SubjectType, which is what actually posts.
    const isEdit = !!cfg.templateId;
    const activeSubjectType = () => {
        if (isEdit) return cfg.subjectType || '';
        for (const b of state.segments) {
            if (!b.segmentId) continue;
            const hit = (options.segment || []).find(o => o.id === b.segmentId);
            if (hit && hit.subjectType) return hit.subjectType;
        }
        return '';
    };
    const syncSubjectType = () => {
        if (isEdit) return;                       // immutable after create
        const input = el('SubjectType');
        if (input) input.value = activeSubjectType();
    };
    // Segment candidates honour the active type: everything while it is empty, same-type only once it is set.
    const segmentOptionsFor = () => {
        const type = activeSubjectType();
        const list = options.segment || [];
        return type ? list.filter(o => o.subjectType === type) : list;
    };

    /// A select bound to a picker. When the picker is unavailable the control is DISABLED with a reason — never a
    /// free-text GUID field, because a hand-typed id is an unverified promise.
    const pickerSelect = (kind, value, allowed, unavailableText, listOverride) => {
        if (!allowed) {
            return `<select class="form-select form-select-sm" data-kind="${kind}" disabled>
                        <option>${esc(unavailableText || L.PickerUnavailable || '')}</option>
                    </select>
                    <small class="text-muted">${esc(unavailableText || L.PickerUnavailable || '')}</small>`;
        }
        const list = listOverride || options[kind] || [];
        const known = list.some(o => o.id === value);
        const head = `<option value="">${esc(L.SelectOption || '—')}</option>`;
        const kept = !known && value ? `<option value="${esc(value)}" selected>${esc(value)}</option>` : '';
        const body = list.map(o => `<option value="${esc(o.id)}"${o.id === value ? ' selected' : ''}>${esc(o.text)}</option>`).join('');
        return `<select class="form-select form-select-sm" data-kind="${kind}"${frozen ? ' disabled' : ''}>${head}${kept}${body}</select>`;
    };

    const vocabSelect = (values, value, extraClass) => {
        const list = Array.isArray(values) ? values : [];
        return `<select class="form-select form-select-sm ${extraClass || ''}"${frozen ? ' disabled' : ''}>`
            + list.map(v => `<option value="${esc(v)}"${v === value ? ' selected' : ''}>${esc(v)}</option>`).join('')
            + '</select>';
    };

    // ---------------- "who" ----------------
    // WP-ST-EDIT-G — the segment section follows the mockup: each bound segment is a DISPLAY row (name + type badge +
    // SEG code — no member count, the backend segment list carries none) with a segmented role toggle, and adding is
    // done through an inline picker ("+ Segment ekle"). The picker honours the homogeneous filter (segmentOptionsFor),
    // the SubjectType is still derived from the first bound segment, and SegmentBindingsJson keeps its shape
    // (segmentId / bindingRole / sortOrder — sortOrder is now an automatic index, no longer an editable input).
    let segPickerOpen = false;

    const segmentById = id => (options.segment || []).find(o => o.id === id);
    const segTypeLabel = t => t === 'account' ? (L.SegTypeAccount || '') : t === 'contact' ? (L.SegTypeContact || '') : '';
    const segTypeBadgeClass = t => t === 'account' ? 'bg-label-warning' : 'bg-label-primary';
    const roleLabel = r => L['BindingRole_' + r] || r;

    const renderSegmentSummary = () => {
        const span = el('segmentSummary');
        if (!span) return;
        const n = state.segments.length;
        if (n === 0) { span.classList.add('d-none'); span.textContent = ''; return; }
        const type = activeSubjectType();
        const typeWord = type === 'contact' ? (L.SubjectTypeContact || '') : type === 'account' ? (L.SubjectTypeAccount || '') : '';
        span.textContent = typeWord
            ? (L.SegmentSummaryTpl || '{n} · {type}').replace('{n}', String(n)).replace('{type}', typeWord)
            : `${n} ${L.StatSegments || ''}`.trim();
        span.classList.remove('d-none');
    };

    const renderSegmentPicker = () => {
        const panel = el('segmentPicker');
        if (!panel) return;
        if (!segPickerOpen) { panel.classList.add('d-none'); panel.innerHTML = ''; return; }
        panel.classList.remove('d-none');
        if (!can('segment')) {
            panel.innerHTML = `<div class="st-segment-picker-note">${esc(L.PickerUnavailable || '')}</div>`;
            return;
        }
        const chosen = new Set(state.segments.map(s => s.segmentId));
        const pool = segmentOptionsFor().filter(o => !chosen.has(o.id));
        if (pool.length === 0) {
            panel.innerHTML = `<div class="st-segment-picker-note">${esc(L.SegmentPickerEmpty || '')}</div>`;
            return;
        }
        panel.innerHTML = pool.map(o => `
            <button type="button" class="st-segment-choice js-seg-choice" data-id="${esc(o.id)}">
                <span class="st-segment-choice-name">${esc(o.name || o.text)}</span>
                <span class="badge ${segTypeBadgeClass(o.subjectType)} st-seg-type">${esc(segTypeLabel(o.subjectType))}</span>
                <span class="st-segment-choice-code">${esc(o.code || '')}</span>
            </button>`).join('');
    };

    const updateAddSegBtnLabel = () => {
        const btn = el('btnAddSegmentBinding');
        if (!btn) return;
        btn.innerHTML = segPickerOpen
            ? `<i class="bx bx-x me-1"></i>${esc(L.SegmentPickerClose || '')}`
            : `<i class="bx bx-plus me-1"></i>${esc(L.AddSegmentBinding || '')}`;
    };

    const renderSegments = () => {
        const host = el('segmentBindingList');
        const empty = el('segmentBindingEmpty');
        if (!host) return;
        const roles = cfg.bindingRoles || [];
        host.innerHTML = state.segments.map((b, i) => {
            const opt = segmentById(b.segmentId);
            const name = opt ? (opt.name || opt.text) : (b.segmentId || '');
            const code = opt ? (opt.code || '') : '';
            const type = (opt && opt.subjectType) || activeSubjectType();
            const roleBtns = roles.map(r => `
                <button type="button" class="st-seg-role js-role-btn${b.bindingRole === r ? ' is-active' : ''}" data-role="${esc(r)}"${frozen ? ' disabled' : ''}>${esc(roleLabel(r))}</button>`).join('');
            return `
            <div class="st-seg-row" data-row="segment" data-index="${i}">
                <div class="st-seg-main">
                    <div class="st-seg-name">${esc(name)}</div>
                    <div class="st-seg-meta">${esc(code)}</div>
                </div>
                <span class="badge ${segTypeBadgeClass(type)} st-seg-type">${esc(segTypeLabel(type))}</span>
                <input type="hidden" class="js-role" value="${esc(b.bindingRole || '')}" />
                <div class="st-seg-roles" role="group">${roleBtns}</div>
                <button type="button" class="btn btn-sm btn-label-danger js-remove"${frozen ? ' disabled' : ''}>
                    <i class="bx bx-trash"></i> ${esc(L.Remove || '')}
                </button>
            </div>`;
        }).join('');
        empty?.classList.toggle('d-none', state.segments.length > 0);
        renderSegmentSummary();
        renderSegmentPicker();
        // Keep the hidden #SubjectType consistent with the current segment selection (no-op on edit).
        syncSubjectType();
    };

    // ---------------- "how often" ----------------

    const renderFrequency = () => {
        const modeEl = el('frequencyMode');
        if (!modeEl) return;
        const modes = cfg.frequencyIntentModes || [];
        modeEl.innerHTML = modes.map(m => `<option value="${esc(m)}"${m === state.frequency.mode ? ' selected' : ''}>${esc(m)}</option>`).join('');
        modeEl.disabled = frozen;

        const policyBlock = el('frequencyPolicyBlock');
        const policySelect = el('frequencyPolicyId');
        if (policySelect) {
            const list = options.policy;
            const value = state.frequency.visitFrequencyPolicyId || '';
            const known = list.some(o => o.id === value);
            policySelect.innerHTML = `<option value="">${esc(L.SelectOption || '—')}</option>`
                + (!known && value ? `<option value="${esc(value)}" selected>${esc(value)}</option>` : '')
                + list.map(o => `<option value="${esc(o.id)}"${o.id === value ? ' selected' : ''}>${esc(o.text)}</option>`).join('');
            policySelect.disabled = frozen || !can('frequency-policy');
        }

        const typeEl = el('frequencyType');
        if (typeEl) {
            // MOD-0165's own vocabulary, republished by the contract. Never a copy kept in this file.
            typeEl.innerHTML = (cfg.frequencyTypes || []).map(v => `<option value="${esc(v)}"${v === state.frequency.frequencyType ? ' selected' : ''}>${esc(v)}</option>`).join('');
            typeEl.disabled = frozen;
        }
        const periodEl = el('periodType');
        if (periodEl) {
            periodEl.innerHTML = (cfg.frequencyPeriodTypes || []).map(v => `<option value="${esc(v)}"${v === state.frequency.periodType ? ' selected' : ''}>${esc(v)}</option>`).join('');
            periodEl.disabled = frozen;
        }
        const countEl = el('requiredVisitCount');
        if (countEl) { countEl.value = state.frequency.requiredVisitCount ?? ''; countEl.disabled = frozen; }
        const noteEl = el('intentNote');
        if (noteEl) { noteEl.value = state.frequency.intentNote ?? ''; noteEl.disabled = frozen; }

        const mode = state.frequency.mode || 'none';
        policyBlock?.classList.toggle('d-none', mode !== 'policy-reference');
        document.querySelectorAll('.frequency-declared').forEach(node =>
            node.classList.toggle('d-none', mode !== 'declared-intent'));
    };

    const readFrequency = () => {
        const mode = el('frequencyMode')?.value || 'none';
        const note = el('intentNote')?.value?.trim() || null;
        if (mode === 'policy-reference') {
            return {
                mode,
                visitFrequencyPolicyId: el('frequencyPolicyId')?.value || null,
                frequencyType: null, requiredVisitCount: null, periodType: null, intentNote: note
            };
        }
        if (mode === 'declared-intent') {
            const raw = el('requiredVisitCount')?.value;
            return {
                mode,
                visitFrequencyPolicyId: null,
                frequencyType: el('frequencyType')?.value || null,
                requiredVisitCount: raw === '' || raw == null ? null : Number(raw),
                periodType: el('periodType')?.value || null,
                intentNote: note
            };
        }
        // 'none' is an ANSWER: it carries neither a policy nor a rhythm, and the runtime rejects a smuggled one.
        return { mode: 'none', visitFrequencyPolicyId: null, frequencyType: null, requiredVisitCount: null, periodType: null, intentNote: note };
    };

    // ---------------- "what" ----------------

    const lineTotal = line => round2((line.skuAllocations || []).reduce((sum, a) => sum + (Number(a.percentage) || 0), 0));

    const renderProducts = () => {
        const host = el('productLineList');
        const empty = el('productLineEmpty');
        if (!host) return;
        host.innerHTML = state.products.map((line, i) => {
            const allocated = (line.skuAllocationMode || 'product-only') === 'sku-allocated';
            const total = lineTotal(line);
            const ok = total === total100;
            const rows = (line.skuAllocations || []).map((a, j) => `
                <tr data-row="sku" data-index="${i}" data-sub="${j}">
                    <td>${pickerSelect('gsku', a.gskuId, can('gsku'), L.GskuPickerUnavailable)}</td>
                    <td style="width:9rem">
                        <input type="number" step="0.01" min="0.01" max="100" class="form-control form-control-sm js-percentage" value="${esc(a.percentage ?? '')}"${frozen ? ' disabled' : ''} />
                    </td>
                    <td class="text-end" style="width:6rem">
                        <button type="button" class="btn btn-sm btn-label-danger js-remove-sku"${frozen ? ' disabled' : ''}><i class="bx bx-trash"></i></button>
                    </td>
                </tr>`).join('');
            return `
            <div class="border rounded p-3" data-row="product" data-index="${i}">
                <div class="row g-2 align-items-end mb-2">
                    <div class="col-12 col-md-5">
                        <label class="form-label small mb-1">${esc(L.GlobalProduct || '')} <span class="text-danger">*</span></label>
                        ${pickerSelect('product', line.globalProductId, can('global-product'))}
                    </div>
                    <div class="col-6 col-md-3">
                        <label class="form-label small mb-1">${esc(L.SkuAllocationMode || '')}</label>
                        ${vocabSelect(cfg.skuAllocationModes || [], line.skuAllocationMode || 'product-only', 'js-mode')}
                    </div>
                    <div class="col-3 col-md-2">
                        <label class="form-label small mb-1">${esc(L.LineWeightPercentage || '')}</label>
                        <input type="number" step="0.01" min="0.01" max="100" class="form-control form-control-sm js-weight" value="${esc(line.lineWeightPercentage ?? '')}"${frozen ? ' disabled' : ''} />
                    </div>
                    <div class="col-3 col-md-2 text-end">
                        <button type="button" class="btn btn-sm btn-label-danger js-remove"${frozen ? ' disabled' : ''}><i class="bx bx-trash"></i></button>
                    </div>
                </div>
                <div class="${allocated ? '' : 'd-none'}">
                    <table class="table table-sm mb-2">
                        <thead><tr>
                            <th>${esc(L.Gsku || '')}</th>
                            <th>${esc(L.Percentage || '')}</th>
                            <th></th>
                        </tr></thead>
                        <tbody>${rows}</tbody>
                    </table>
                    <div class="d-flex justify-content-between align-items-center">
                        <button type="button" class="btn btn-sm btn-label-primary js-add-sku"${frozen ? ' disabled' : ''}>
                            <i class="bx bx-plus"></i> ${esc(L.AddSkuAllocation || '')}
                        </button>
                        <span class="badge ${ok ? 'bg-label-success' : 'bg-label-danger'}">
                            ${esc(L.TotalPercentage || '')}: ${total.toFixed(2)}
                        </span>
                    </div>
                </div>
            </div>`;
        }).join('');
        empty?.classList.toggle('d-none', state.products.length > 0);
    };

    // ---------------- "which story" ----------------

    const contentOptionsFor = type => type === 'content-engagement-journey' ? options.journey : options.path;

    const renderContents = () => {
        const host = el('contentBindingList');
        const empty = el('contentBindingEmpty');
        if (!host) return;
        host.innerHTML = state.contents.map((c, i) => {
            const type = c.contentRefType || (cfg.contentRefTypes || [])[0] || 'knowledge-path';
            const list = contentOptionsFor(type);
            const known = list.some(o => o.id === c.contentRefId);
            const allowed = type === 'content-engagement-journey' ? can('content-engagement-journey') : can('knowledge-path');
            const select = allowed
                ? `<select class="form-select form-select-sm js-content-ref"${frozen ? ' disabled' : ''}>
                        <option value="">${esc(L.SelectOption || '—')}</option>
                        ${!known && c.contentRefId ? `<option value="${esc(c.contentRefId)}" selected>${esc(c.contentRefId)}</option>` : ''}
                        ${list.map(o => `<option value="${esc(o.id)}"${o.id === c.contentRefId ? ' selected' : ''}>${esc(o.text)}</option>`).join('')}
                   </select>`
                : `<select class="form-select form-select-sm js-content-ref" disabled><option>${esc(L.PickerUnavailable || '')}</option></select>`;
            return `
            <div class="border rounded p-3" data-row="content" data-index="${i}">
                <div class="row g-2 align-items-end">
                    <div class="col-12 col-md-3">
                        <label class="form-label small mb-1">${esc(L.ContentRefType || '')} <span class="text-danger">*</span></label>
                        ${vocabSelect(cfg.contentRefTypes || [], type, 'js-content-type')}
                    </div>
                    <div class="col-12 col-md-5">
                        <label class="form-label small mb-1">${esc(L.ContentRef || '')} <span class="text-danger">*</span></label>
                        ${select}
                    </div>
                    <div class="col-6 col-md-2">
                        <label class="form-label small mb-1">${esc(L.SortOrder || '')}</label>
                        <input type="number" class="form-control form-control-sm js-sort" value="${esc(c.sortOrder ?? i * 10)}"${frozen ? ' disabled' : ''} />
                    </div>
                    <div class="col-6 col-md-2 text-end">
                        <button type="button" class="btn btn-sm btn-label-danger js-remove"${frozen ? ' disabled' : ''}><i class="bx bx-trash"></i></button>
                    </div>
                </div>
            </div>`;
        }).join('');
        empty?.classList.toggle('d-none', state.contents.length > 0);
    };

    const renderAll = () => { renderSegments(); renderFrequency(); renderProducts(); renderContents(); };

    // ---------------- events ----------------

    const limitReached = (count, max) => {
        if (!max || count < max) return false;
        window.showToast?.(L.LimitReached || '', 'warning');
        return true;
    };

    // WP-ST-EDIT-G — "+ Segment ekle" no longer pushes an empty row; it toggles the inline picker. A segment is added
    // (with its id already resolved) only when the author clicks an entry in that picker.
    const addSegBtn = el('btnAddSegmentBinding');
    if (addSegBtn && frozen) addSegBtn.disabled = true;
    addSegBtn?.addEventListener('click', () => {
        if (frozen) return;
        segPickerOpen = !segPickerOpen;
        updateAddSegBtnLabel();
        renderSegmentPicker();
    });

    // Picking a segment from the pool adds it with a sensible default role (first = primary, rest = secondary) and its
    // automatic sortOrder; the picker then closes. The homogeneous filter already kept the pool to the active type.
    el('segmentPicker')?.addEventListener('click', event => {
        const choice = event.target.closest('.js-seg-choice');
        if (!choice || frozen) return;
        if (limitReached(state.segments.length, cfg.maxSegmentBindings)) return;
        const id = choice.dataset.id;
        if (!id) return;
        state.segments.push({
            segmentId: id,
            bindingRole: state.segments.length ? 'secondary' : 'primary',
            sortOrder: state.segments.length * 10,
            notes: null
        });
        segPickerOpen = false;
        updateAddSegBtnLabel();
        renderSegments();
        setTimeout(updateSidePanel, 0);
    });

    // The segmented role toggle: a button click sets bindingRole directly (buttons fire no 'change'), then re-renders so
    // the active state and the hidden .js-role input (read back by sync/submit) stay in step.
    el('segmentBindingList')?.addEventListener('click', event => {
        const rbtn = event.target.closest('.js-role-btn');
        if (!rbtn || frozen) return;
        const row = rbtn.closest('[data-row="segment"]');
        if (!row) return;
        const i = Number(row.dataset.index);
        if (!state.segments[i]) return;
        state.segments[i].bindingRole = rbtn.dataset.role || null;
        renderSegments();
        setTimeout(updateSidePanel, 0);
    });

    el('btnAddProductLine')?.addEventListener('click', () => {
        if (frozen || limitReached(state.products.length, cfg.maxProductLines)) return;
        state.products.push({
            globalProductId: '', skuAllocationMode: 'product-only', lineWeightPercentage: null,
            skuAllocations: [], sortOrder: state.products.length * 10, notes: null
        });
        renderProducts();
    });

    el('btnAddContentBinding')?.addEventListener('click', () => {
        if (frozen || limitReached(state.contents.length, cfg.maxContentBindings)) return;
        state.contents.push({
            contentRefType: (cfg.contentRefTypes || [])[0] || 'knowledge-path',
            contentRefId: '', sortOrder: state.contents.length * 10, notes: null
        });
        renderContents();
    });

    el('frequencyMode')?.addEventListener('change', () => { state.frequency = readFrequency(); renderFrequency(); });

    document.addEventListener('click', event => {
        const removeSku = event.target.closest('.js-remove-sku');
        if (removeSku) {
            const row = removeSku.closest('[data-row="sku"]');
            state.products[Number(row.dataset.index)].skuAllocations.splice(Number(row.dataset.sub), 1);
            renderProducts();
            return;
        }

        const addSku = event.target.closest('.js-add-sku');
        if (addSku) {
            const line = state.products[Number(addSku.closest('[data-row="product"]').dataset.index)];
            line.skuAllocations = line.skuAllocations || [];
            if (limitReached(line.skuAllocations.length, cfg.maxSkuAllocationsPerLine)) return;
            line.skuAllocations.push({ gskuId: '', percentage: null, sortOrder: line.skuAllocations.length * 10 });
            renderProducts();
            return;
        }

        const remove = event.target.closest('.js-remove');
        if (!remove) return;
        const row = remove.closest('[data-row]');
        if (!row) return;
        const index = Number(row.dataset.index);
        if (row.dataset.row === 'segment') { state.segments.splice(index, 1); renderSegments(); }
        else if (row.dataset.row === 'product') { state.products.splice(index, 1); renderProducts(); }
        else if (row.dataset.row === 'content') { state.contents.splice(index, 1); renderContents(); }
    });

    /// Reads the DOM back into state. Called on every change so the live total and the hidden JSON always agree with
    /// what the author can see.
    const sync = () => {
        document.querySelectorAll('[data-row="segment"]').forEach(row => {
            const i = Number(row.dataset.index);
            const binding = state.segments[i];
            if (!binding) return;
            // WP-ST-EDIT-G — segmentId is fixed by the picker (the row is display-only), so it is NOT re-read here (an
            // absent dropdown would wipe it). Only the role (from the hidden .js-role input the toggle drives) and the
            // automatic index-based sortOrder are written back — SegmentBindingsJson keeps its shape.
            binding.bindingRole = row.querySelector('.js-role')?.value || null;
            binding.sortOrder = i * 10;
        });

        document.querySelectorAll('[data-row="product"]').forEach(row => {
            const i = Number(row.dataset.index);
            const line = state.products[i];
            if (!line) return;
            line.globalProductId = row.querySelector('[data-kind="product"]')?.value || '';
            line.skuAllocationMode = row.querySelector('.js-mode')?.value || 'product-only';
            const weight = row.querySelector('.js-weight')?.value;
            line.lineWeightPercentage = weight === '' || weight == null ? null : Number(weight);
            row.querySelectorAll('[data-row="sku"]').forEach(sub => {
                const allocation = line.skuAllocations[Number(sub.dataset.sub)];
                if (!allocation) return;
                allocation.gskuId = sub.querySelector('[data-kind="gsku"]')?.value || '';
                const raw = sub.querySelector('.js-percentage')?.value;
                // Stored exactly as typed — the client never normalises or redistributes a share.
                allocation.percentage = raw === '' || raw == null ? null : Number(raw);
            });
            if (line.skuAllocationMode !== 'sku-allocated') line.skuAllocations = [];
        });

        document.querySelectorAll('[data-row="content"]').forEach(row => {
            const i = Number(row.dataset.index);
            const binding = state.contents[i];
            if (!binding) return;
            binding.contentRefType = row.querySelector('.js-content-type')?.value || binding.contentRefType;
            binding.contentRefId = row.querySelector('.js-content-ref')?.value || '';
            binding.sortOrder = Number(row.querySelector('.js-sort')?.value || 0);
        });

        state.frequency = readFrequency();
        // The derived SubjectType is part of the payload — keep the hidden field current on every read-back (and so at
        // submit, since sync() runs first there too). No-op on edit, where the type is immutable.
        syncSubjectType();
    };

    form.addEventListener('change', event => {
        if (!event.target.closest('#segmentBindingList, #productLineList, #contentBindingList, #frequencyEditor')) return;
        sync();
        // A mode or a content type change reshapes its row, so those two re-render; a percentage only refreshes totals.
        if (event.target.classList.contains('js-mode') || event.target.classList.contains('js-content-type')) {
            renderProducts();
            renderContents();
        } else if (event.target.classList.contains('js-percentage')) {
            renderProducts();
        } else if (event.target.matches('[data-kind="segment"]')) {
            // Choosing the first segment derives the SubjectType; re-render so the remaining rows lock to that type.
            renderSegments();
        }
    });

    form.addEventListener('input', event => {
        if (event.target.classList.contains('js-percentage')) {
            sync();
            const row = event.target.closest('[data-row="product"]');
            const badge = row?.querySelector('.badge');
            if (!badge) return;
            const total = lineTotal(state.products[Number(row.dataset.index)]);
            badge.textContent = `${L.TotalPercentage || ''}: ${total.toFixed(2)}`;
            badge.classList.toggle('bg-label-success', total === total100);
            badge.classList.toggle('bg-label-danger', total !== total100);
        }
    });

    form.addEventListener('submit', () => {
        sync();
        // A frozen play posts NO binding list: an omitted list means "leave it alone", which is what lets a live play
        // be renamed without a 409.
        el('SegmentBindingsJson').value = frozen ? '' : JSON.stringify(state.segments);
        el('FrequencyIntentJson').value = frozen ? '' : JSON.stringify(state.frequency);
        el('ProductLinesJson').value = frozen ? '' : JSON.stringify(state.products);
        el('ContentBindingsJson').value = frozen ? '' : JSON.stringify(state.contents);
    });

    el('btnNewVersionFromForm')?.addEventListener('click', async () => {
        const id = el('btnNewVersionFromForm').dataset.id;
        try {
            const created = await envelope(await fetch(`${endpoint}/templates/${id}/new-version`, {
                method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
            }));
            window.location.href = created ? `/CRM/StrategyTemplates/Edit/${created}` : '/CRM/StrategyTemplates';
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
        }
    });

    // ---------------- WP-ST-EDIT-A scope cascade ("KAPSAM — NEREDE"), mirroring the Campaign form ----------------
    // The play's address is EDITABLE: ScopeType picks the level, and exactly ONE reference block (country / legal-entity
    // / business-unit) is shown and posted. The option feeds come from the same-origin scope-options proxy — there is no
    // cycle-period binding here, so unlike the Campaign form nothing reloads a period picker.
    const scopeSection = el('strategyTemplateScopeSection');
    // WP-ST-EDIT-E — scopeTypeEl is now a HIDDEN input (the level is chosen with the segmented buttons below). Its
    // `.value` still carries the posted ScopeType, so the cascade, single-reference clear and submit are unchanged.
    const scopeTypeEl = el('scopeType');
    const scopeTypeButtonsEl = el('scopeTypeButtons');
    const countryEl = el('countryScope');
    const legalEntityEl = el('legalEntityId');
    const businessUnitEl = el('businessUnitId');
    const buFilterCountryEl = el('buFilterCountry');
    const resolvedScopeEl = el('resolvedScope');
    const scopeOptionsUrl = scopeSection?.dataset.scopeOptionsUrl || `${endpoint}/scope-options`;
    let scopeOptions = null;

    // Fills a scope select, keeping the current value selectable even when the feed no longer offers it (e.g. a
    // territory-narrowed business-unit list on edit) — otherwise the round trip would silently drop the reference.
    const fillScope = (node, items, placeholder) => {
        if (!node) return;
        const current = node.dataset.selected || node.value || '';
        const list = items || [];
        const known = list.some(i => String(i.value) === String(current));
        const head = `<option value="">${esc(placeholder || '')}</option>`;
        const kept = current && !known ? `<option value="${esc(current)}" selected>${esc(current)}</option>` : '';
        node.innerHTML = head + kept
            + list.map(i => `<option value="${esc(i.value)}"${String(i.value) === String(current) ? ' selected' : ''}>${esc(i.label)}</option>`).join('');
        if (current) node.value = current;
    };

    // Says WHY a list is empty: an unpublished set, an unreachable dependency and "no territory plan matches" are three
    // different situations, and an author who cannot tell them apart has no way to act.
    const setScopeNote = (level, key) => {
        const note = scopeSection?.querySelector(`[data-scope-note="${level}"]`);
        if (!note) return;
        const text = key ? (L[key] || '') : '';
        note.textContent = text;
        note.classList.toggle('d-none', !text);
    };

    // Single-reference invariant: only the SELECTED level keeps its reference; the others are cleared so the payload can
    // never carry two addresses (the runtime refuses a second one too, but the UI never sends it).
    const clearUnselectedScopeRefs = () => {
        const level = (scopeTypeEl?.value || '').trim();
        if (level !== 'country' && countryEl) countryEl.value = '';
        if (level !== 'legal-entity' && legalEntityEl) legalEntityEl.value = '';
        if (level !== 'business-unit' && businessUnitEl) businessUnitEl.value = '';
    };

    // WP-ST-EDIT-E — the ÇÖZÜMLENEN KAPSAM box is ALWAYS painted (a bordered bg-body box, styled in strategy-create.css).
    // It shows a mono uppercase label plus the resolved breadcrumb; when nothing resolves yet the value is a danger-toned
    // "henüz çözümlenmedi", so the author can see the box is waiting on a reference rather than seeing it disappear.
    const renderResolvedScope = () => {
        if (!resolvedScopeEl) return;
        const level = (scopeTypeEl?.value || '').trim();
        const typeLabel = level ? (L['ScopeType_' + level] || level) : '';
        let refLabel = '';
        if (level === 'country') refLabel = countryEl?.selectedOptions?.[0]?.textContent?.trim() || '';
        else if (level === 'legal-entity') refLabel = legalEntityEl?.selectedOptions?.[0]?.textContent?.trim() || '';
        else if (level === 'business-unit') {
            // WP-ST-EDIT-H — the business-unit address resolves as "{country filter} / {business unit}" (e.g.
            // "Belarus / Beta"). Each part is included only when it actually has a value — the country filter's
            // "show all" head and the BU "—" head carry visible placeholder text but an empty value, so guard on
            // value (not text) and join only the filled ones.
            const buCountry = (buFilterCountryEl?.value || '').trim() ? (buFilterCountryEl.selectedOptions?.[0]?.textContent?.trim() || '') : '';
            const bu = (businessUnitEl?.value || '').trim() ? (businessUnitEl.selectedOptions?.[0]?.textContent?.trim() || '') : '';
            refLabel = [buCountry, bu].filter(Boolean).join(' / ');
        }
        const value = refLabel ? `${typeLabel} — ${refLabel}` : typeLabel;
        const label = `<span class="st-resolved-scope-label">${esc(L.ResolvedScope || '')}</span>`;
        const valueHtml = value
            ? `<span class="st-resolved-scope-value">${esc(value)}</span>`
            : `<span class="st-resolved-scope-value is-empty">${esc(L.ResolvedScopeNone || '')}</span>`;
        resolvedScopeEl.innerHTML = label + valueHtml;
    };

    // Only the block belonging to the selected level is shown: the address is discriminated, never combined.
    const applyScopeType = () => {
        const level = (scopeTypeEl?.value || '').trim();
        scopeSection?.querySelectorAll('[data-scope-block]').forEach(block => {
            block.classList.toggle('d-none', block.dataset.scopeBlock !== level);
        });
        clearUnselectedScopeRefs();
        renderResolvedScope();
    };

    // WP-ST-EDIT-E — build the segmented ScopeType buttons from the SAME feed the old dropdown used (scopeOptions
    // .scopeTypes). The current level is kept selected; on first paint it defaults to 'tenant' (or the first offered
    // level). Clicking a button sets the hidden #scopeType and calls applyScopeType — the cascade never changes.
    const renderScopeTypeButtons = () => {
        if (!scopeTypeButtonsEl) return;
        const types = (scopeOptions?.scopeTypes || []);
        let current = (scopeTypeEl?.value || scopeTypeEl?.dataset.selected || '').trim();
        if (!current || types.indexOf(current) < 0) current = types.indexOf('tenant') >= 0 ? 'tenant' : (types[0] || 'tenant');
        if (scopeTypeEl) scopeTypeEl.value = current;
        // WP-ST-EDIT-H — the buttons are Task Center .choice-box style cards (no btn-outline-primary fill): the
        // st-scope-type class now owns the base bg-body + subtle border and the active primary-subtle wash (see
        // strategy-create.css). The level logic, hidden #scopeType input and applyScopeType cascade are unchanged.
        scopeTypeButtonsEl.innerHTML = types.map(t => {
            const active = t === current;
            return `<button type="button" class="st-scope-type${active ? ' active' : ''}" data-scope-type="${esc(t)}" aria-pressed="${active ? 'true' : 'false'}">
                        <span class="st-scope-type-title">${esc(L['ScopeType_' + t] || t)}</span>
                        <span class="st-scope-type-sub">${esc(L['ScopeTypeSub_' + t] || '')}</span>
                    </button>`;
        }).join('');
    };

    const renderScopeOptions = () => {
        if (!scopeOptions) return;
        renderScopeTypeButtons();

        fillScope(countryEl, scopeOptions.countries, L.SelectOption);
        setScopeNote('country', scopeOptions.countrySetPublished ? null : 'ReferenceSetUnpublished');

        fillScope(legalEntityEl, scopeOptions.legalEntities, L.SelectOption);
        setScopeNote('legal-entity', scopeOptions.legalEntityLookupAvailable ? null : 'DependencyUnavailable');

        fillScope(buFilterCountryEl, scopeOptions.countries, L.ShowAll);
        fillScope(businessUnitEl, scopeOptions.businessUnits, L.SelectOption);
        setScopeNote('business-unit',
            !scopeOptions.businessUnitSetPublished ? 'ReferenceSetUnpublished'
                : (scopeOptions.businessUnitFromTerritory ? null : 'NoTerritoryPlanMatches'));

        applyScopeType();
    };

    const loadScopeOptions = async () => {
        if (!scopeSection) return;
        const params = new URLSearchParams();
        const country = (buFilterCountryEl?.value || '').trim();
        if (country) params.set('country', country);
        try {
            const response = await fetch(`${scopeOptionsUrl}?${params.toString()}`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            });
            if (!response.ok) return;
            scopeOptions = (await response.json().catch(() => ({})))?.data || null;
            renderScopeOptions();
        } catch (error) {
            // A selector that cannot load its options leaves what is already selected alone (graceful, like Campaign).
            console.warn('[StrategyTemplates] Scope options could not be loaded.', error);
        }
    };

    // WP-ST-EDIT-E — a hidden input fires no 'change' on a programmatic set, so the segmented buttons drive the level:
    // set the hidden value, move the active class, then run the existing applyScopeType (blocks + single-reference clear).
    scopeTypeButtonsEl?.addEventListener('click', event => {
        const btn = event.target.closest('.st-scope-type');
        if (!btn || btn.disabled) return;
        const level = btn.dataset.scopeType || '';
        if (scopeTypeEl) scopeTypeEl.value = level;
        scopeTypeButtonsEl.querySelectorAll('.st-scope-type').forEach(b => {
            const on = b === btn;
            b.classList.toggle('active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        applyScopeType();
    });
    countryEl?.addEventListener('change', renderResolvedScope);
    legalEntityEl?.addEventListener('change', renderResolvedScope);
    businessUnitEl?.addEventListener('change', renderResolvedScope);
    // The country filter narrows the business-unit candidates; it is not the play's scope and is never posted.
    buFilterCountryEl?.addEventListener('change', () => { void loadScopeOptions(); });
    // Belt-and-braces before the metadata/binding sync runs: only the selected level's reference reaches the payload.
    form.addEventListener('submit', clearUnselectedScopeRefs);

    // ---------------- WP-ST-EDIT-B right sticky panel (live summary + BÖLÜMLER checklist + YAŞAM DÖNGÜSÜ) --------------
    // READ-ONLY over the form: every value is DERIVED from the real form state (state.* + the live DOM). It never writes
    // a binding, never touches buildPayload / the scope cascade / submit, and the two save buttons submit the existing
    // MVC form through form="strategyTemplateForm". New version / Archive reuse the existing template endpoints.
    const panelEl = () => document.getElementById('stChecklist');

    // The scope "NEREDE" phrase, read the same way renderResolvedScope reads it (type label + selected reference name).
    const scopePhrase = () => {
        const level = (scopeTypeEl?.value || '').trim();
        if (!level) return '';
        const typeLabel = L['ScopeType_' + level] || level;
        let refLabel = '';
        if (level === 'country') refLabel = countryEl?.selectedOptions?.[0]?.textContent?.trim() || '';
        else if (level === 'legal-entity') refLabel = legalEntityEl?.selectedOptions?.[0]?.textContent?.trim() || '';
        else if (level === 'business-unit') refLabel = businessUnitEl?.selectedOptions?.[0]?.textContent?.trim() || '';
        return refLabel ? `${typeLabel} — ${refLabel}` : typeLabel;
    };

    const subjectWord = () => {
        const st = el('SubjectType')?.value || cfg.subjectType || '';
        if (st === 'contact') return L.SubjectTypeContact || st;
        if (st === 'account') return L.SubjectTypeAccount || st;
        return st;
    };

    const weightSum = () => round2(state.products.reduce((s, l) => s + (Number(l.lineWeightPercentage) || 0), 0));
    const skuLinesConsistent = () => state.products.every(l =>
        (l.skuAllocationMode || 'product-only') !== 'sku-allocated' || lineTotal(l) === total100);

    const frequencyPhrase = () => {
        const f = state.frequency || { mode: 'none' };
        if (f.mode === 'policy-reference') {
            const hit = (options.policy || []).find(o => o.id === f.visitFrequencyPolicyId);
            return hit ? hit.text : (L.FrequencyPolicyRef || L.FrequencyPolicy || '');
        }
        if (f.mode === 'declared-intent') {
            if (f.frequencyType && Number(f.requiredVisitCount) > 0 && f.periodType) {
                return (L.VisitsPerPeriodTpl || '{count} / {period}')
                    .replace('{count}', String(f.requiredVisitCount))
                    .replace('{period}', String(f.periodType));
            }
            return L.FrequencyDeclared || '';
        }
        return L.FrequencyNone || (L.RecipeNone || '—');
    };

    const setRecipe = (id, value) => { const n = el(id); if (n) n.textContent = value || (L.RecipeNone || '—'); };

    const setCheck = (key, state3, detail) => {
        const row = document.querySelector(`.st-check[data-check="${key}"]`);
        if (!row) return;
        row.classList.remove('is-ok', 'is-warn', 'is-neutral');
        row.classList.add(state3 === 'ok' ? 'is-ok' : state3 === 'warn' ? 'is-warn' : 'is-neutral');
        const icon = row.querySelector('.st-check-icon');
        if (icon) {
            icon.classList.remove('bx-check-circle', 'bx-error-circle', 'bx-minus-circle');
            icon.classList.add(state3 === 'ok' ? 'bx-check-circle' : state3 === 'warn' ? 'bx-error-circle' : 'bx-minus-circle');
        }
        const d = row.querySelector('.st-check-detail');
        if (d) d.textContent = detail || '';
    };

    function updateSidePanel() {
        if (!panelEl()) return;

        const nSeg = state.segments.length;
        const nProd = state.products.length;
        const nContent = state.contents.length;
        const wsum = weightSum();
        const skuOk = skuLinesConsistent();
        const code = norm(el('TemplateCode')?.value);
        const name = norm(el('TemplateName')?.value);

        // ── recipe (BU OYUN NE YAPACAK) ──
        setRecipe('stRecipeWhere', scopePhrase());
        setRecipe('stRecipeWho', nSeg > 0 ? `${nSeg} ${L.StatSegments || ''} · ${subjectWord()}`.trim() : '');
        setRecipe('stRecipeHowOften', frequencyPhrase());
        setRecipe('stRecipeWhat', nProd > 0 ? `${nProd} ${L.StatProductLines || ''} · Σ${wsum}%` : '');
        setRecipe('stRecipeStory', nContent > 0 ? `${nContent} ${L.StatContents || ''}` : '');

        const sentenceBox = el('stSummarySentence');
        if (sentenceBox) {
            if (name) {
                sentenceBox.textContent = (L.SummarySentenceTpl || '"{name}" — {who} · {what} · {howoften}')
                    .replace('{name}', name)
                    .replace('{who}', nSeg > 0 ? `${nSeg} ${L.StatSegments || ''} (${subjectWord()})` : subjectWord())
                    .replace('{what}', nProd > 0 ? `${nProd} ${L.StatProductLines || ''}` : '—')
                    .replace('{howoften}', frequencyPhrase());
            } else {
                sentenceBox.textContent = '';
            }
        }

        // ── stat tiles ──
        const setStat = (id, v, off) => { const n = el(id); if (n) { n.textContent = v; n.classList.toggle('is-off', !!off); } };
        setStat('stStatSegments', String(nSeg));
        setStat('stStatProducts', String(nProd));
        setStat('stStatWeight', `${wsum}`, nProd > 0 && wsum !== total100);
        setStat('stStatContents', String(nContent));

        // ── checklist (BÖLÜMLER) ──
        setCheck('identity', code && name ? 'ok' : 'warn', code && name ? name : (L.ChkIdentityEmptyDesc || ''));
        setCheck('scope', 'ok', scopePhrase() || (L.ChkScopeTenantDesc || ''));
        setCheck('segments', nSeg > 0 ? 'ok' : 'warn',
            nSeg > 0 ? (L.ChkSegmentsCountDesc || '{n}').replace('{n}', String(nSeg)) : (L.ChkSegmentsEmptyDesc || ''));

        const fmode = (state.frequency || {}).mode || 'none';
        if (fmode === 'none') setCheck('frequency', 'neutral', L.ChkFrequencyNoneDesc || '');
        else if (fmode === 'policy-reference') {
            const has = !!norm(state.frequency.visitFrequencyPolicyId);
            setCheck('frequency', has ? 'ok' : 'warn', has ? (L.ChkFrequencyPolicyDesc || '') : (L.ChkFrequencyIncompleteDesc || ''));
        } else {
            const ok = !!state.frequency.frequencyType && Number(state.frequency.requiredVisitCount) > 0 && !!state.frequency.periodType;
            setCheck('frequency', ok ? 'ok' : 'warn', ok ? (L.ChkFrequencyDeclaredDesc || '') : (L.ChkFrequencyIncompleteDesc || ''));
        }

        if (nProd === 0) setCheck('products', 'warn', L.ChkProductsEmptyDesc || '');
        else if (wsum !== total100 || !skuOk) setCheck('products', 'warn', L.ChkProductsWeightOffDesc || '');
        else setCheck('products', 'ok', (L.ChkProductsOkDesc || '{n}').replace('{n}', String(nProd)));

        if (nContent === 0) setCheck('content', 'neutral', L.ChkContentEmptyDesc || '');
        else setCheck('content', 'ok', (L.ChkContentCountDesc || '{n}').replace('{n}', String(nContent)));

        if (nProd === 0) setCheck('mdm', 'neutral', L.ChkMdmNeutralDesc || '');
        else if (state.products.every(l => norm(l.globalProductId))) setCheck('mdm', 'ok', L.ChkMdmOkDesc || '');
        else setCheck('mdm', 'warn', L.ChkMdmMissingDesc || '');

        // ── ready counter + bar (over the 6 content sections; a neutral/optional row is not a blocker) ──
        const navKeys = ['identity', 'scope', 'segments', 'frequency', 'products', 'content'];
        const done = navKeys.filter(k => !document.querySelector(`.st-check[data-check="${k}"]`)?.classList.contains('is-warn')).length;
        const total = navKeys.length;
        const pill = el('stReadyPill');
        if (pill) {
            pill.textContent = (L.ReadyCountTpl || '{done}/{total}').replace('{done}', String(done)).replace('{total}', String(total));
            pill.classList.toggle('is-ready', done === total);
        }
        const bar = el('stReadyBar');
        if (bar) {
            bar.style.width = `${Math.round((done / total) * 100)}%`;
            bar.classList.toggle('is-ready', done === total);
        }
    }

    // Live updates: field edits bubble to the form; add/remove clicks mutate state then re-render, so a microtask-delayed
    // refresh picks up the new state. Neither path touches buildPayload / the binding builders.
    form.addEventListener('change', updateSidePanel);
    form.addEventListener('input', updateSidePanel);
    document.addEventListener('click', event => {
        if (event.target.closest('#btnAddSegmentBinding, #btnAddProductLine, #btnAddContentBinding, .js-remove, .js-remove-sku, .js-add-sku')) {
            setTimeout(updateSidePanel, 0);
        }
    });

    // YAŞAM DÖNGÜSÜ — New version / Archive reuse the existing template endpoints (no new backend), exactly as details.js.
    el('btnPanelNewVersion')?.addEventListener('click', async () => {
        const id = el('btnPanelNewVersion').dataset.id;
        const run = async () => {
            try {
                const created = await envelope(await fetch(`${endpoint}/templates/${id}/new-version`, {
                    method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
                }));
                window.location.href = created ? `/CRM/StrategyTemplates/Edit/${created}` : '/CRM/StrategyTemplates';
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        };
        if (window.showConfirm) window.showConfirm(L.NewVersionConfirm, run, { type: 'question', confirmButtonText: L.NewVersion });
        else run();
    });
    el('btnPanelArchive')?.addEventListener('click', () => {
        const id = el('btnPanelArchive').dataset.id;
        const run = async () => {
            try {
                await envelope(await fetch(`${endpoint}/templates/${id}/archive`, {
                    method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
                }));
                window.showToast?.(L.RecordArchived, 'success');
                setTimeout(() => { window.location.href = '/CRM/StrategyTemplates'; }, 350);
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        };
        if (window.showConfirm) window.showConfirm(L.ArchiveStrategyTemplateConfirm, run, { type: 'warning', confirmButtonText: L.ArchiveStrategyTemplate });
        else run();
    });

    const init = async () => {
        renderAll();
        await loadScopeOptions();
        await loadOptions();
        renderAll();
        if (window.flatpickr) {
            document.querySelectorAll('.flatpickr-date').forEach(node => window.flatpickr(node, { dateFormat: 'Y-m-d' }));
        }
        updateSidePanel();
    };

    init();
})(window, document);
