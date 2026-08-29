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
                subjectType: r.subjectType,
                archived: r.isArchived === true
            })).then(x => { options.segment = x.filter(o => !o.archived && (!cfg.subjectType || o.subjectType === cfg.subjectType)); }));
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

    /// A select bound to a picker. When the picker is unavailable the control is DISABLED with a reason — never a
    /// free-text GUID field, because a hand-typed id is an unverified promise.
    const pickerSelect = (kind, value, allowed, unavailableText) => {
        if (!allowed) {
            return `<select class="form-select form-select-sm" data-kind="${kind}" disabled>
                        <option>${esc(unavailableText || L.PickerUnavailable || '')}</option>
                    </select>
                    <small class="text-muted">${esc(unavailableText || L.PickerUnavailable || '')}</small>`;
        }
        const list = options[kind] || [];
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

    const renderSegments = () => {
        const host = el('segmentBindingList');
        const empty = el('segmentBindingEmpty');
        if (!host) return;
        host.innerHTML = state.segments.map((b, i) => `
            <div class="border rounded p-3" data-row="segment" data-index="${i}">
                <div class="row g-2 align-items-end">
                    <div class="col-12 col-md-5">
                        <label class="form-label small mb-1">${esc(L.Segment || '')} <span class="text-danger">*</span></label>
                        ${pickerSelect('segment', b.segmentId, can('segment'))}
                    </div>
                    <div class="col-6 col-md-3">
                        <label class="form-label small mb-1">${esc(L.BindingRole || '')}</label>
                        ${vocabSelect(['', ...(cfg.bindingRoles || [])], b.bindingRole || '', 'js-role')}
                    </div>
                    <div class="col-3 col-md-2">
                        <label class="form-label small mb-1">${esc(L.SortOrder || '')}</label>
                        <input type="number" class="form-control form-control-sm js-sort" value="${esc(b.sortOrder ?? i * 10)}"${frozen ? ' disabled' : ''} />
                    </div>
                    <div class="col-3 col-md-2 text-end">
                        <button type="button" class="btn btn-sm btn-label-danger js-remove"${frozen ? ' disabled' : ''}>
                            <i class="bx bx-trash"></i> ${esc(L.Remove || '')}
                        </button>
                    </div>
                </div>
            </div>`).join('');
        empty?.classList.toggle('d-none', state.segments.length > 0);
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

    el('btnAddSegmentBinding')?.addEventListener('click', () => {
        if (frozen || limitReached(state.segments.length, cfg.maxSegmentBindings)) return;
        state.segments.push({ segmentId: '', bindingRole: '', sortOrder: state.segments.length * 10, notes: null });
        renderSegments();
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
            binding.segmentId = row.querySelector('[data-kind="segment"]')?.value || '';
            binding.bindingRole = row.querySelector('.js-role')?.value || null;
            binding.sortOrder = Number(row.querySelector('.js-sort')?.value || 0);
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

    const init = async () => {
        renderAll();
        await loadOptions();
        renderAll();
        if (window.flatpickr) {
            document.querySelectorAll('.flatpickr-date').forEach(node => window.flatpickr(node, { dateFormat: 'Y-m-d' }));
        }
    };

    init();
})(window, document);
