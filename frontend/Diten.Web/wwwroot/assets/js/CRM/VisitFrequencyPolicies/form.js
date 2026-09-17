/**
 * MOD-0165-FU03 (WP-FREQ-F2) Visit Frequency / Call-Cycle Policy — Create/Edit editor as a SEPARATE Golden Compact page
 * (the FREQ-B offcanvas is retired). ONE body, two modes read from the form dataset: Create (empty) and Edit
 * (GET → fill). The FREQ-A console (index.js) navigates here ("New Policy" → /Create, row "Edit" → /Edit/{id}); this
 * file owns the editor only.
 *
 * FIELD LOGIC IS REUSED unchanged from the offcanvas editor: targetType / frequencyType / periodType / source / the
 * PRIORITY BANDS all come from the FU03 /contract endpoint — nothing is a hardcoded vocabulary. The band radio-cards
 * render from contract.vocabulary.priorityBands (code + authored weight, "smaller wins"), labelled + described through
 * L10n. The ONLY structural rule embedded here is the FrequencyType×PeriodType allow-map (a validation rule the WP
 * enumerates, mirroring the backend), never a vocabulary list. PICKERS show NAMES, never GUIDs. The submitted payload
 * maps one-to-one onto the CrmService Create/Update request (TenantId is never sent; PolicyCode + TargetType/TargetId
 * are immutable on edit) — the payload contract is byte-for-byte the FREQ-B contract.
 *
 * NEW for F2 (page-only presentation): the right-hand sticky panel — a lifecycle status selector, a live "what will this
 * policy do?" summary and a "before you save" checklist — the collapsible "where does it apply?" scope block with a chip
 * summary + clear, brand→product narrowing, and two save buttons (activate / draft) plus edit-mode inactivate/archive.
 * Save redirects to the list; archive uses the dedicated archive endpoint.
 */
(function (window, document) {
    'use strict';

    const FORM = document.getElementById('vfpEditorForm');
    if (!FORM) return;

    const endpoint = FORM.dataset.endpoint || '/CRM/VisitFrequencyPolicies/api';
    const LIST_URL = '/CRM/VisitFrequencyPolicies';
    const initialMode = (FORM.dataset.mode || 'create').toLowerCase() === 'edit' ? 'edit' : 'create';
    const initialId = (FORM.dataset.policyId || '').trim() || null;

    let L = {};
    try { L = JSON.parse(document.getElementById('vfp-editor-l10n')?.textContent || '{}'); } catch (e) { L = {}; }
    L = Object.assign({}, window.L10n || {}, L);
    const statusLabels = L.statusLabels || {};

    // ── small helpers ─────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const humanize = code => norm(code).split(/[-_\s]+/).filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
    const t = (key, fallback) => L[key] || fallback || key;
    const statusLabel = s => statusLabels[norm(s)] || humanize(s) || '—';

    // ── select2 (search) enhancement — the app-wide pattern (jQuery select2, dropdownParent, native-change bridge) ────
    // WP-FREQ-F9. select2 + jQuery are loaded app-wide by the tenant shell layout. This is PRESENTATION + SEARCH ONLY:
    // the underlying <select> keeps its id, data-role, value and selectedOptions, so buildPayload / currentTargetId /
    // cascade / validation read it byte-for-byte as before. select2 announces a choice the jQuery way ($(el).trigger
    // ('change')), which native addEventListener never hears — so a bridge re-dispatches a native BUBBLING change,
    // keeping BOTH the FORM-delegated change listener and the target picker's own change listener alive. Because select2
    // renders from a SNAPSHOT of the options, any re-fill (cascade model→node, brand→product, mode reset) must destroy +
    // re-init; a value-only change just tells select2 to re-read via the `change.select2` namespace.
    const jq = () => window.jQuery;
    const hasSelect2 = () => { const $ = jq(); return !!($ && $.fn && $.fn.select2); };
    const bindSelect2 = select => {
        if (!select || !hasSelect2()) return;
        const $ = jq();
        const $s = $(select);
        if ($s.hasClass('select2-hidden-accessible')) return; // already bound
        if (!(select.parentElement && select.parentElement.classList.contains('vfp-s2-wrap'))) {
            $s.wrap('<div class="vfp-s2-wrap position-relative"></div>');
        }
        $s.select2({ dropdownParent: $s.parent() });
        // Bridge: carry select2's jQuery-synthesised change across to a native bubbling change (namespaced so a
        // `change.select2` re-read never triggers it). A real DOM change already reached the native listeners, and
        // jQuery marks it with `originalEvent` — that guard stops the bridge echoing its own dispatch.
        $s.off('change.vfpBridge').on('change.vfpBridge', ev => {
            if (ev && ev.originalEvent) return;
            select.dispatchEvent(new Event('change', { bubbles: true }));
        });
    };
    const unbindSelect2 = select => {
        if (!select || !hasSelect2()) return;
        const $ = jq();
        const $s = $(select);
        if ($s.hasClass('select2-hidden-accessible')) { $s.off('change.vfpBridge'); $s.select2('destroy'); }
    };
    // destroy + re-init after the options (or the disabled state) were rebuilt — select2 snapshots them at init.
    const rebindSelect2 = select => { unbindSelect2(select); bindSelect2(select); };
    // value-only sync (no re-fill): tell select2 to re-read the underlying value WITHOUT firing the change bridge.
    const syncSelect2 = select => {
        const $ = jq();
        if (select && hasSelect2() && $(select).hasClass('select2-hidden-accessible')) $(select).trigger('change.select2');
    };

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [t('ErrorState', 'Error')]).join(' · ')), { status: response.status });
        return body.data;
    };
    const getJson = path => fetch(`${endpoint}${path}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(envelope);

    // ── FrequencyType × PeriodType allow-map (structural VALIDATION rule, mirrors the backend — not a vocabulary) ─────
    const ALLOWED_PERIODS = {
        weekly: ['week'],
        biweekly: ['week', 'custom'],
        monthly: ['month'],
        'cycle-based': ['cycle'],
        custom: ['day', 'week', 'month', 'quarter', 'cycle', 'campaign-period', 'custom']
    };

    // ── entity-picker readers (existing sibling lists, proxied) — value=id/code, text=name ──────────────────────────
    const READERS = {
        segment: () => getJson('/segments?pageSize=200'),
        account: () => getJson('/accounts?pageSize=200'),
        contact: () => getJson('/contacts?pageSize=200'),
        campaign: () => getJson('/campaigns?pageSize=200'),
        'concept-node': () => getJson('/concept-nodes?pageSize=200'),
        'audience-profile': () => getJson('/audience-profiles?pageSize=200'),
        brand: () => getJson('/mdm-brands?pageSize=200'),
        product: () => getJson('/mdm-products?pageSize=200'),
        'cycle-period': () => getJson('/cycle-periods'),
        'business-unit': () => getJson('/business-units'),
        'territory-model': () => getJson('/territory-models?pageSize=200'),
        'territory-node': ctx => (ctx ? getJson(`/territory-models/${ctx}/nodes`) : Promise.resolve([]))
    };

    const optionCache = new Map();
    const loadOptions = async (kind, ctx) => {
        const key = `${kind}:${ctx || ''}`;
        if (optionCache.has(key)) return optionCache.get(key);
        let options = [];
        try {
            const data = await (READERS[kind] ? READERS[kind](ctx) : Promise.resolve([]));
            const items = Array.isArray(data) ? data : (data?.items || data?.nodes || data?.values || []);
            options = items.map(mapOption).filter(o => o.value !== '' && o.value != null);
        } catch (e) { options = []; }
        optionCache.set(key, options);
        return options;
    };

    // Generous id/name fallbacks (endpoints differ in casing + field names) so the dropdown shows a NAME, never a GUID.
    const mapOption = x => ({
        value: x.id ?? x.value ?? x.valueCode ?? x.segmentId ?? x.accountId ?? x.contactId ?? x.campaignId
            ?? x.cyclePeriodId ?? x.conceptNodeId ?? x.audienceProfileId ?? x.territoryNodeId ?? x.nodeId
            ?? x.brandId ?? x.productId ?? x.globalProductId ?? x.territoryModelId ?? '',
        text: x.name || x.Name || x.text || x.displayName || x.DisplayName || x.label || x.Label
            || x.segmentName || x.SegmentName || x.accountName || x.AccountName || x.contactName || x.ContactName
            || x.fullName || x.FullName || x.campaignName || x.CampaignName || x.periodName || x.PeriodName
            || x.cyclePeriodName || x.conceptName || x.ConceptName || x.nodeName || x.NodeName
            || x.audienceProfileName || x.profileName || x.brandName || x.BrandName || x.productName || x.ProductName
            || x.modelName || x.ModelName || x.code || x.Code
            || String(x.id ?? x.value ?? x.valueCode ?? '')
    });

    // targetType → entity-picker kind; anything unmapped (account-contact-link) falls back to a manual id input.
    const TARGET_KIND = {
        segment: 'segment', account: 'account', contact: 'contact',
        'campaign-target': 'campaign', 'concept-node': 'concept-node', 'audience-profile': 'audience-profile'
    };

    // ── contract (loaded once) ─────────────────────────────────────────────────
    let contract = null;
    let vocab = null;
    let built = false;
    let currentMode = initialMode;
    let currentId = initialId;
    let loadedStatus = 'draft'; // the persisted status of the record being edited (for archived read-only detection)

    const loadContract = async () => {
        if (contract) return contract;
        contract = await getJson('/visit-frequency-policies/contract');
        vocab = contract?.vocabulary || {};
        return contract;
    };

    // ── option/vocab select fillers ────────────────────────────────────────────
    const fillSelect = (select, options, placeholder) => {
        if (!select) return;
        const head = placeholder != null ? `<option value="">${esc(placeholder)}</option>` : '';
        select.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    const fillVocabSelect = (select, codes, labeller, placeholder) =>
        fillSelect(select, (codes || []).map(c => ({ value: c, text: labeller(c) })), placeholder);

    // ── radio-card grids (target type + priority band) ─────────────────────────
    const renderCards = (host, items, name) => {
        host.innerHTML = (items || []).map(it => `
            <label class="vfp-radio-card">
                <input type="radio" class="vfp-radio-input" name="${esc(name)}" value="${esc(it.value)}">
                <span class="vfp-radio-title">${esc(it.title)}${it.weight != null ? `<span class="vfp-band-weight">${esc(it.weight)}</span>` : ''}</span>
                ${it.help ? `<span class="vfp-radio-help">${esc(it.help)}</span>` : ''}
            </label>`).join('');
    };
    // Target-type DISPLAY order (specificity, narrow → broad). This is a display-only transform: the vocabulary itself
    // still comes from /contract; unknown codes keep their contract order after the known ones. The first rendered chip
    // (most specific) and last chip (broadest) carry a small subtext.
    const TARGET_SPECIFICITY = ['account-contact-link', 'contact', 'account', 'campaign-target', 'concept-node', 'territory-node', 'audience-profile', 'segment'];
    const orderTargetTypes = codes => {
        const list = (codes || []).slice();
        const rank = c => { const i = TARGET_SPECIFICITY.indexOf(c); return i === -1 ? TARGET_SPECIFICITY.length : i; };
        return list.map((c, i) => ({ c, i })).sort((a, b) => (rank(a.c) - rank(b.c)) || (a.i - b.i)).map(x => x.c);
    };
    // compact flex-wrap chips for the target type (specificity order + most-specific/broadest subtext)
    const renderTargetChips = (host, codes) => {
        if (!host) return;
        const ordered = orderTargetTypes(codes);
        host.innerHTML = ordered.map((code, idx) => {
            const sub = idx === 0 ? t('MostSpecific', '') : (idx === ordered.length - 1 ? t('Broadest', '') : '');
            return `
            <label class="vfp-chip-card">
                <input type="radio" class="vfp-radio-input" name="vfpTargetType" value="${esc(code)}">
                <span class="vfp-chip-title">${esc(t(`TargetType_${code}`, humanize(code)))}</span>
                ${sub ? `<span class="vfp-chip-sub">${esc(sub)}</span>` : ''}
            </label>`;
        }).join('');
    };
    const checkedValue = name => FORM.querySelector(`input[name="${name}"]:checked`)?.value || '';
    const setChecked = (name, value) => {
        const input = FORM.querySelector(`input[name="${name}"][value="${(window.CSS && CSS.escape) ? CSS.escape(value) : value}"]`);
        if (input) input.checked = true;
    };

    // ── build the whole editor once (after contract is available) ──────────────
    const buildOnce = () => {
        if (built) return;

        // target type chips (compact, specificity-ordered, most-specific/broadest subtext)
        renderTargetChips(el('vfpTargetTypeCards'), vocab.targetTypes || []);

        // priority band cards — CONTRACT bands (code + weight); smaller wins. Sorted by weight ascending (strongest
        // first). The description is the F1 Band_{code}_Desc phrase.
        const bands = (vocab.priorityBands || []).slice().sort((a, b) => (a.value || 0) - (b.value || 0));
        renderCards(el('vfpBandCards'),
            bands.map(b => ({ value: b.value, title: t(`Band_${b.code}`, humanize(b.code)), help: t(`Band_${b.code}_Desc`, ''), weight: b.value })),
            'vfpPriority');

        // frequency + source vocab selects
        fillVocabSelect(el('vfpFrequencyType'), vocab.frequencyTypes, c => t(`Freq_${c}`, humanize(c)), t('SelectOption', '—'));
        fillVocabSelect(el('vfpSource'), vocab.sources, c => t(`Source_${c}`, humanize(c)), t('SelectOption', '—'));
        refreshPeriodOptions();

        built = true;
    };

    // period options depend on the chosen frequency type (allow-map)
    const refreshPeriodOptions = () => {
        const freq = norm(el('vfpFrequencyType')?.value);
        const all = vocab.periodTypes || [];
        const allowed = ALLOWED_PERIODS[freq] || all;
        const usable = all.filter(p => allowed.includes(p));
        const prev = norm(el('vfpPeriodType')?.value);
        fillVocabSelect(el('vfpPeriodType'), usable, c => t(`Period_${c}`, humanize(c)), t('SelectOption', '—'));
        if (usable.includes(prev)) el('vfpPeriodType').value = prev;
        else if (usable.length === 1) el('vfpPeriodType').value = usable[0];
        updateFreqSentence();
    };

    const updateFreqSentence = () => {
        const count = norm(el('vfpRequiredVisitCount')?.value);
        const period = norm(el('vfpPeriodType')?.value);
        const freq = norm(el('vfpFrequencyType')?.value);
        const box = el('vfpFreqSentence');
        const raw = el('vfpFreqRaw');
        if (raw) raw.textContent = (count && period) ? [freq, count, period].filter(Boolean).join(' · ') : '';
        if (!box) return;
        if (!count || !period) { box.textContent = t('FreqSentenceEmpty', '—'); return; }
        const visits = t('VisitsWord', '');
        box.textContent = `${count} ${visits} ${t('PerPeriod', '/')} ${t(`Period_${period}`, humanize(period))}`.replace(/\s+/g, ' ').trim();
    };

    // ── target picker (varies by target type) ──────────────────────────────────
    const renderTargetPicker = async (targetType, seedId, seedName) => {
        const host = el('vfpTargetPicker');
        const picked = el('vfpTargetPicked');
        if (!host) return;
        const kind = TARGET_KIND[targetType];

        if (targetType === 'territory-node') {
            // WP-FREQ-F7 — app field pattern (diten-field icon + form-select), matching /Tasks/Create. The two select
            // ids and data-role="targetId" (form.js's payload source) are unchanged.
            host.innerHTML = `
                <div class="diten-field mb-2"><i class="bx bx-sitemap diten-field-icon" aria-hidden="true"></i><select class="form-select select2" id="vfpTargetTerModel"><option value="">${esc(t('SelectTerritoryModel', 'Select model'))}</option></select></div>
                <div class="diten-field"><i class="bx bx-map-pin diten-field-icon" aria-hidden="true"></i><select class="form-select select2" id="vfpTargetTerNode" data-role="targetId"><option value="">${esc(t('SelectTerritoryNode', 'Select node'))}</option></select></div>`;
            const models = await loadOptions('territory-model');
            fillSelect(el('vfpTargetTerModel'), models, t('SelectTerritoryModel', 'Select model'));
            if (seedId) seedSelect(el('vfpTargetTerNode'), seedId, seedName);
            // WP-FREQ-F9 — search-enable both target selects AFTER options + seed are in place (cascade model→node
            // re-inits the node select in cascadeNodes). data-role="targetId" stays on the underlying <select>.
            bindSelect2(el('vfpTargetTerModel'));
            bindSelect2(el('vfpTargetTerNode'));
            return;
        }

        if (!kind) {
            // account-contact-link (no picker in the mockup) — a manual id keeps it authorable, contract-driven.
            host.innerHTML = `<div class="diten-field"><i class="bx bx-hash diten-field-icon" aria-hidden="true"></i><input type="text" class="form-control" data-role="targetId" placeholder="${esc(t('TargetIdManual', 'Enter id (GUID)'))}" value="${esc(seedId || '')}"></div>`;
            return;
        }

        host.innerHTML = `<div class="diten-field"><i class="bx bx-crosshair diten-field-icon" aria-hidden="true"></i><select class="form-select select2" data-role="targetId"><option value="">${esc(t('SelectOption', '—'))}</option></select></div>`;
        const control = host.querySelector('[data-role="targetId"]');
        const options = await loadOptions(kind);
        fillSelect(control, options, t('SelectOption', '—'));
        if (seedId) seedSelect(control, seedId, seedName);
        control.addEventListener('change', () => {
            const opt = control.selectedOptions[0];
            showPicked(picked, opt && opt.value ? opt.text : '');
            updatePanel();
        });
        // WP-FREQ-F9 — search-enable AFTER options + seed + the native change listener; select2's choice reaches that
        // listener through the native-change bridge, so showPicked/updatePanel fire exactly as before.
        bindSelect2(control);
        const cur = control.selectedOptions[0];
        showPicked(picked, cur && cur.value ? cur.text : '');
    };

    const seedSelect = (select, id, name) => {
        if (!select) return;
        const has = Array.from(select.options).some(o => o.value === String(id));
        if (!has) {
            const opt = document.createElement('option');
            opt.value = String(id);
            opt.textContent = name || String(id);
            select.appendChild(opt);
        }
        select.value = String(id);
    };
    // WP-FREQ-F7 — the picked target renders as the mockup "Hangi kayıt" chip (code badge + name + optional external
    // code + "Değiştir…"). Empty → the app select in #vfpTargetPicker is shown; picked → the select is hidden and the
    // chip is shown. The select stays in the DOM with its value, so [data-role="targetId"] / currentTargetId() and the
    // payload are unchanged; "Değiştir…" simply re-reveals it. isGuid is declared below and only used here at runtime.
    const showPicked = (host, name) => {
        if (!host) return;
        const picker = el('vfpTargetPicker');
        if (!name) {
            host.innerHTML = '';
            host.classList.remove('is-shown');
            picker?.classList.remove('vfp-hidden');
            return;
        }
        const type = norm(checkedValue('vfpTargetType'));
        const ctrl = FORM.querySelector('#vfpTargetPicker [data-role="targetId"]');
        const val = norm(ctrl?.value);
        const ext = (val && !isGuid(val) && val !== name) ? val : '';
        host.innerHTML = `
            <div class="vfp-picked-chip">
                ${type ? `<span class="vfp-picked-code">${esc(type)}</span>` : ''}
                <span class="vfp-picked-name">${esc(name)}</span>
                ${ext ? `<span class="vfp-picked-ext">${esc(ext)}</span>` : ''}
                <button type="button" class="vfp-picked-change">${esc(t('TargetChange', 'Change…'))}</button>
            </div>`;
        host.classList.add('is-shown');
        picker?.classList.add('vfp-hidden');
        const change = host.querySelector('.vfp-picked-change');
        if (change) change.addEventListener('click', () => {
            picker?.classList.remove('vfp-hidden');
            host.classList.remove('is-shown');
            host.innerHTML = '';
        });
    };

    const currentTargetId = () => norm(FORM.querySelector('#vfpTargetPicker [data-role="targetId"]')?.value);
    const currentTargetName = () => {
        const ctrl = FORM.querySelector('#vfpTargetPicker [data-role="targetId"]');
        if (!ctrl) return '';
        if (ctrl.tagName === 'SELECT') { const o = ctrl.selectedOptions[0]; return o && o.value ? o.text : ''; }
        return norm(ctrl.value);
    };

    // ── context selects (optional provenance / scope) ──────────────────────────
    const CONTEXT = [
        { id: 'vfpBusinessUnit', kind: 'business-unit', ph: 'ScopeAllBusinessUnits' },
        { id: 'vfpSegment', kind: 'segment', ph: 'ScopeSegmentIndependent' },
        { id: 'vfpCampaign', kind: 'campaign', ph: 'ScopeCampaignIndependent' },
        { id: 'vfpBrand', kind: 'brand', ph: 'ScopeAllBrands' },
        { id: 'vfpProduct', kind: 'product', ph: 'ScopeAllProducts' },
        { id: 'vfpCyclePeriod', kind: 'cycle-period', ph: 'ScopeAllCyclePeriods' }
    ];
    const loadContextSelects = async seeds => {
        seeds = seeds || {};
        for (const c of CONTEXT) {
            const select = el(c.id);
            if (!select) continue;
            const options = await loadOptions(c.kind);
            fillSelect(select, options, t(c.ph, t('ContextNone', '— none —')));
            const seed = seeds[c.id];
            if (seed) seedSelect(select, seed, seeds[`${c.id}_name`]);
            rebindSelect2(select); // WP-FREQ-F9 — search-enable after options + seed
        }
        // territory-node context = cascade model → node
        const modelSel = el('vfpTerritoryModel');
        const nodeSel = el('vfpTerritoryNode');
        if (modelSel) {
            const models = await loadOptions('territory-model');
            fillSelect(modelSel, models, t('SelectTerritoryModel', 'Select model'));
            rebindSelect2(modelSel); // WP-FREQ-F9
        }
        if (nodeSel && seeds.vfpTerritoryNode) seedSelect(nodeSel, seeds.vfpTerritoryNode, seeds.vfpTerritoryNode_name);
        if (nodeSel) rebindSelect2(nodeSel); // WP-FREQ-F9 — enhance the node select (may hold only its seed/placeholder)
        applyBrandProductNarrowing();
    };
    const cascadeNodes = async (modelSel, nodeSel) => {
        if (!modelSel || !nodeSel) return;
        const modelId = norm(modelSel.value);
        const options = modelId ? await loadOptions('territory-node', modelId) : [];
        fillSelect(nodeSel, options, t('SelectTerritoryNode', 'Select node'));
        rebindSelect2(nodeSel); // WP-FREQ-F9 — node options were re-filled; select2 must re-snapshot them
    };
    const onContextTerritoryModelChange = () => cascadeNodes(el('vfpTerritoryModel'), el('vfpTerritoryNode'));

    // brand → product narrowing (mockup: product disabled until a brand is picked, "önce marka seçin")
    const applyBrandProductNarrowing = () => {
        const brand = el('vfpBrand');
        const product = el('vfpProduct');
        const hint = el('vfpProductHint');
        if (!brand || !product) return;
        const hasBrand = !!norm(brand.value);
        product.disabled = !hasBrand;
        if (!hasBrand) { product.value = ''; }
        if (hint) hint.classList.toggle('vfp-hidden', hasBrand);
        // WP-FREQ-F9 — select2 snapshots the disabled state + value at init, so re-bind to reflect the toggle/clear.
        rebindSelect2(product);
    };

    // ── collapsible scope block (chip summary + clear) ─────────────────────────
    const SCOPE_FIELDS = [
        { id: 'vfpBusinessUnit', label: () => t('FieldBusinessUnit', 'Business unit') },
        { id: 'vfpTerritoryNode', label: () => t('FieldTerritory', 'Territory') },
        { id: 'vfpSegment', label: () => t('FieldSegment', 'Segment') },
        { id: 'vfpCampaign', label: () => t('FieldCampaign', 'Campaign') },
        { id: 'vfpBrand', label: () => t('FieldBrand', 'Brand') },
        { id: 'vfpProduct', label: () => t('FieldProduct', 'Product') },
        { id: 'vfpCyclePeriod', label: () => t('FieldCyclePeriod', 'Cycle period') }
    ];
    const selectedScope = () => SCOPE_FIELDS.filter(f => norm(el(f.id)?.value));
    const scopeText = f => { const s = el(f.id); const o = s?.selectedOptions?.[0]; return o && o.value ? o.text : ''; };
    const toggleScope = force => {
        const body = el('vfpScopeBody');
        const btn = el('vfpScopeToggle');
        if (!body || !btn) return;
        const show = force != null ? force : body.classList.contains('vfp-hidden');
        body.classList.toggle('vfp-hidden', !show);
        btn.setAttribute('aria-expanded', String(show));
        btn.classList.toggle('is-open', show);
    };
    const renderScopeChips = () => {
        const host = el('vfpScopeChips');
        const clear = el('vfpScopeClear');
        if (!host) return;
        const picked = selectedScope();
        host.innerHTML = picked.map(f => `<span class="vfp-chip">${esc(f.label())}: ${esc(scopeText(f))}</span>`).join('');
        if (clear) clear.classList.toggle('vfp-hidden', picked.length === 0);
    };
    const clearScope = () => {
        SCOPE_FIELDS.forEach(f => { const s = el(f.id); if (s) { s.value = ''; syncSelect2(s); } });
        const model = el('vfpTerritoryModel'); if (model) { model.value = ''; syncSelect2(model); }
        applyBrandProductNarrowing(); // re-binds product (disabled + cleared)
        renderScopeChips();
        updatePanel();
    };

    // ── lifecycle status selector (right panel) ────────────────────────────────
    const getSelectedStatus = () => norm(checkedValue('vfpLifecycle')) || norm(el('vfpStatus')?.value) || 'draft';
    const setLifecycle = status => {
        const s = norm(status) || 'draft';
        setChecked('vfpLifecycle', s);
        if (el('vfpStatus')) el('vfpStatus').value = s;
        updateFooter();
        updatePanel();
    };
    const configureLifecycleForMode = () => {
        // All four rows stay VISIBLE. create: only draft/active are selectable — inactive/archived shown disabled/faded
        // (a new record cannot be created inactive or archived). edit (non-archived): all four selectable.
        const rows = FORM.querySelectorAll('#vfpLifecycle .vfp-life-row');
        rows.forEach(r => {
            const s = r.dataset.status;
            const disabled = currentMode === 'create' && (s === 'inactive' || s === 'archived');
            r.classList.remove('vfp-hidden');
            r.classList.toggle('vfp-life-disabled', disabled);
            const input = r.querySelector('input');
            if (input) input.disabled = disabled;
        });
    };
    const updateFooter = () => {
        const selected = getSelectedStatus();
        const editable = !(currentMode === 'edit' && loadedStatus === 'archived');
        const showInactive = editable && currentMode === 'edit';
        const showArchive = editable && currentMode === 'edit';
        el('vfpSaveInactive')?.classList.toggle('vfp-hidden', !showInactive);
        el('vfpArchive')?.classList.toggle('vfp-hidden', !showArchive);
        // "armed" highlight follows the currently selected lifecycle (senkron with the radios).
        const arm = (btn, on) => btn && btn.classList.toggle('vfp-btn-armed', on);
        arm(el('vfpSaveActivate'), selected === 'active');
        arm(el('vfpSaveDraft'), selected === 'draft');
        arm(el('vfpSaveInactive'), selected === 'inactive');
        arm(el('vfpArchive'), selected === 'archived');
    };

    // ── live summary + checklist (right panel) ─────────────────────────────────
    const cadenceText = () => {
        const count = norm(el('vfpRequiredVisitCount')?.value);
        const period = norm(el('vfpPeriodType')?.value);
        if (!count || !period) return '';
        return `${count} ${t('VisitsWord', '')} ${t('PerPeriod', '/')} ${t(`Period_${period}`, humanize(period))}`.replace(/\s+/g, ' ').trim();
    };
    const validityText = () => {
        const from = norm(el('vfpEffectiveFrom')?.value);
        const to = norm(el('vfpEffectiveTo')?.value);
        if (!from) return '—';
        return `${from} → ${to || t('SummaryOpenEnded', '—')}`;
    };
    const weightText = () => {
        const p = norm(checkedValue('vfpPriority'));
        if (!p) return '—';
        const card = FORM.querySelector(`input[name="vfpPriority"][value="${(window.CSS && CSS.escape) ? CSS.escape(p) : p}"]`)?.closest('.vfp-radio-card');
        const title = card?.querySelector('.vfp-radio-title')?.childNodes?.[0]?.textContent;
        return norm(title) || p;
    };
    const targetSummary = () => {
        const type = checkedValue('vfpTargetType');
        if (!type) return '—';
        const label = t(`TargetType_${type}`, humanize(type));
        const name = currentTargetName();
        return name ? `${label} · ${name}` : label;
    };
    const scopeSummary = () => {
        const n = selectedScope().length;
        return n === 0 ? t('ScopeNoConstraint', 'no constraints') : `${n} ${t('ScopeConstraintUnit', 'constraints')}`;
    };

    const updateSummary = () => {
        const name = norm(el('vfpPolicyName')?.value) || t('SummaryEmptyName', 'This policy');
        const cadence = cadenceText();
        const nConstraints = selectedScope().length;
        const scopePhrase = nConstraints === 0 ? t('SummaryScopeAll', '') : scopeSummary();
        const sentenceBox = el('vfpSummarySentence');
        if (sentenceBox) {
            if (cadence) {
                const tpl = t('SummarySentenceTpl', '"{name}" — {cadence} — {scope}');
                sentenceBox.textContent = tpl.replace('{name}', name).replace('{cadence}', cadence).replace('{scope}', scopePhrase);
            } else {
                sentenceBox.textContent = '';
            }
        }
        const set = (id, v) => { const n = el(id); if (n) n.textContent = v || '—'; };
        set('vfpSumTarget', targetSummary());
        set('vfpSumFrequency', cadence);
        set('vfpSumScope', scopeSummary());
        set('vfpSumValidity', validityText());
        set('vfpSumWeight', weightText());
        set('vfpSumStatus', statusLabel(getSelectedStatus()));
    };

    const willLabel = status => ({
        draft: t('ChkWillDraft', 'Stays draft'),
        active: t('ChkWillActive', 'Will be published'),
        inactive: t('ChkWillInactive', 'Will be unpublished'),
        archived: t('ChkWillArchived', 'Will be archived')
    }[norm(status)] || statusLabel(status));

    const updateChecklist = () => {
        const host = el('vfpChecklist');
        const badge = el('vfpCheckBadge');
        if (!host) return;
        const freq = norm(el('vfpFrequencyType')?.value);
        const count = Number(el('vfpRequiredVisitCount')?.value);
        const period = norm(el('vfpPeriodType')?.value);
        const targetOk = !!checkedValue('vfpTargetType') && !!currentTargetId();
        const freqOk = !!freq && count > 0;
        const periodOk = !!period && (!(freq && ALLOWED_PERIODS[freq]) || ALLOWED_PERIODS[freq].includes(period));
        const weightOk = !!norm(checkedValue('vfpPriority'));
        const nScope = selectedScope().length;

        const items = [
            { ok: targetOk, label: t('ChkTarget', 'Target selected') },
            { ok: freqOk, label: t('ChkFrequency', 'Frequency valid') },
            { ok: periodOk, label: t('ChkPeriod', 'Period consistent') },
            { ok: true, neutral: true, label: nScope > 0 ? t('ChkScopeNarrowed', 'Scope narrowed') : t('ChkScopeNone', 'No scope constraint') },
            { ok: weightOk, label: t('ChkWeight', 'Conflict weight chosen') },
            { ok: true, neutral: true, label: willLabel(getSelectedStatus()) }
        ];
        host.innerHTML = items.map(it => {
            const cls = it.neutral ? 'is-neutral' : (it.ok ? 'is-ok' : 'is-warn');
            const icon = it.neutral ? 'bx-info-circle' : (it.ok ? 'bx-check' : 'bx-error-circle');
            return `<li class="vfp-check ${cls}"><i class="bx ${icon}"></i><span>${esc(it.label)}</span></li>`;
        }).join('');

        const warnCount = items.filter(it => !it.neutral && !it.ok).length;
        if (badge) {
            badge.textContent = warnCount === 0 ? t('ChecklistReady', 'ready') : `${warnCount} ${t('ChecklistWarnWord', 'warnings')}`;
            badge.classList.toggle('is-ready', warnCount === 0);
            badge.classList.toggle('is-warn', warnCount > 0);
        }
    };

    const updatePanel = () => {
        renderScopeChips();
        updateSummary();
        updateChecklist();
    };

    // ── validation ─────────────────────────────────────────────────────────────
    const setError = (id, message) => {
        const box = el(id);
        if (!box) return;
        box.textContent = message || '';
        box.classList.toggle('is-shown', !!message);
    };
    const clearErrors = () => FORM.querySelectorAll('.vfp-error').forEach(b => { b.textContent = ''; b.classList.remove('is-shown'); });
    const isGuid = v => /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(norm(v));

    const validate = () => {
        clearErrors();
        const errors = [];
        const req = t('Required', 'Required');

        const code = norm(el('vfpPolicyCode')?.value);
        const name = norm(el('vfpPolicyName')?.value);
        const targetType = checkedValue('vfpTargetType');
        const targetId = currentTargetId();
        const freq = norm(el('vfpFrequencyType')?.value);
        const count = Number(el('vfpRequiredVisitCount')?.value);
        const period = norm(el('vfpPeriodType')?.value);
        const priority = norm(checkedValue('vfpPriority'));
        const source = norm(el('vfpSource')?.value);
        const effFrom = norm(el('vfpEffectiveFrom')?.value);
        const effTo = norm(el('vfpEffectiveTo')?.value);
        const notes = norm(el('vfpNotes')?.value);
        const campaignId = norm(el('vfpCampaign')?.value);
        const segmentId = norm(el('vfpSegment')?.value);
        const cyclePeriodId = norm(el('vfpCyclePeriod')?.value);

        if (currentMode === 'create' && !code) { setError('vfpPolicyCodeError', req); errors.push('code'); }
        if (!name) { setError('vfpPolicyNameError', req); errors.push('name'); }
        if (!targetType) { setError('vfpTargetError', req); errors.push('targetType'); }
        else if (!targetId) { setError('vfpTargetError', t('TargetRequired', 'Pick a target')); errors.push('targetId'); }
        else if (targetType !== 'territory-node' && !TARGET_KIND[targetType] && !isGuid(targetId)) {
            setError('vfpTargetError', t('TargetIdInvalid', 'A valid id is required')); errors.push('targetId');
        }
        if (!freq) { setError('vfpFreqError', req); errors.push('freq'); }
        if (!(count > 0)) { setError('vfpCountError', t('CountPositive', 'Must be greater than zero')); errors.push('count'); }
        if (!period) { setError('vfpPeriodError', req); errors.push('period'); }
        else if (freq && ALLOWED_PERIODS[freq] && !ALLOWED_PERIODS[freq].includes(period)) {
            setError('vfpPeriodError', t('PeriodComboInvalid', 'This period is not valid for the chosen frequency'));
            errors.push('combo');
        }
        if (!priority) { setError('vfpPriorityError', t('BandRequired', 'Pick a priority band')); errors.push('priority'); }
        if (!source) { setError('vfpSourceError', req); errors.push('source'); }
        if (!effFrom) { setError('vfpEffectiveError', req); errors.push('effFrom'); }
        else if (effTo && effTo < effFrom) { setError('vfpEffectiveError', t('EffectiveOrder', 'End cannot be before start')); errors.push('effRange'); }

        // context/provenance requirements (mirror the backend)
        const needsCycle = freq === 'cycle-based' || period === 'cycle';
        if (needsCycle && !cyclePeriodId) { setError('vfpCyclePeriodError', t('CycleRequired', 'A cycle period is required for cycle-based frequency')); errors.push('cycle'); }
        if ((period === 'campaign-period' || source === 'campaign') && !campaignId) { setError('vfpCampaignError', t('CampaignRequired', 'A campaign is required')); errors.push('campaign'); }
        if (source === 'segmentation' && !segmentId) { setError('vfpSegmentError', t('SegmentRequired', 'A segment is required for segmentation source')); errors.push('segment'); }
        if (freq === 'custom' && !notes) { setError('vfpNotesError', t('NotesRequired', 'A custom frequency requires notes')); errors.push('notes'); }

        // opening the scope block so a hidden context error is visible
        if (['cycle', 'campaign', 'segment'].some(e => errors.includes(e))) toggleScope(true);

        return errors.length === 0;
    };

    // ── payload (one-to-one with the CrmService request; no TenantId; immutable fields omitted on update) ────────────
    const asGuid = v => { const s = norm(v); return s || null; };
    const asStr = v => { const s = norm(v); return s || null; };

    const buildPayload = () => {
        const base = {
            policyName: norm(el('vfpPolicyName')?.value),
            frequencyType: norm(el('vfpFrequencyType')?.value),
            requiredVisitCount: Number(el('vfpRequiredVisitCount')?.value) || 0,
            periodType: norm(el('vfpPeriodType')?.value),
            effectiveFrom: norm(el('vfpEffectiveFrom')?.value),
            priority: Number(checkedValue('vfpPriority')) || 0,
            source: norm(el('vfpSource')?.value),
            status: asStr(el('vfpStatus')?.value),
            description: asStr(el('vfpDescription')?.value),
            businessUnit: asStr(el('vfpBusinessUnit')?.value),
            territoryNodeId: asGuid(el('vfpTerritoryNode')?.value),
            campaignId: asGuid(el('vfpCampaign')?.value),
            segmentId: asGuid(el('vfpSegment')?.value),
            brandId: asGuid(el('vfpBrand')?.value),
            productId: asGuid(el('vfpProduct')?.value),
            cycleId: null,
            cyclePeriodId: asGuid(el('vfpCyclePeriod')?.value),
            effectiveTo: asStr(el('vfpEffectiveTo')?.value),
            notes: asStr(el('vfpNotes')?.value)
        };
        if (currentMode === 'create') {
            return Object.assign({
                policyCode: norm(el('vfpPolicyCode')?.value),
                targetType: checkedValue('vfpTargetType'),
                targetId: currentTargetId()
            }, base);
        }
        return base; // update: PolicyCode + TargetType/TargetId are immutable and never re-sent
    };

    // ── mode transitions ────────────────────────────────────────────────────────
    const setFormError = message => {
        const box = el('vfpFormError');
        if (!box) return;
        box.textContent = message || '';
        box.classList.toggle('is-shown', !!message);
    };
    const setTitle = text => { const h = el('vfpEditorTitle'); if (h) h.textContent = text; };
    const setCodeReadonly = ro => {
        const code = el('vfpPolicyCode');
        if (code) code.readOnly = ro;
        el('vfpTargetLockNote')?.classList.toggle('vfp-hidden', !ro);
        FORM.querySelectorAll('input[name="vfpTargetType"]').forEach(i => { i.disabled = ro; });
        el('vfpTargetPicker')?.querySelectorAll('select,input').forEach(i => { i.disabled = ro; });
        // WP-FREQ-F9 — select2 snapshots disabled at init, so re-bind the target selects to reflect the lock (edit mode).
        el('vfpTargetPicker')?.querySelectorAll('select').forEach(rebindSelect2);
        // WP-FREQ-F7 — the picked chip's "Değiştir…" must also lock when the target is immutable (edit mode).
        el('vfpTargetPicked')?.querySelectorAll('button').forEach(i => { i.disabled = ro; });
    };
    const suggestCode = () => `vfp-${new Date().getFullYear()}-${Math.random().toString(36).slice(2, 8)}`;

    const resetForCreate = async () => {
        currentMode = 'create';
        currentId = null;
        loadedStatus = 'draft';
        setTitle(t('NewPolicy', 'New Policy'));
        setFormError('');
        clearErrors();
        buildOnce();
        configureLifecycleForMode();
        setCodeReadonly(false);
        if (el('vfpPolicyCode')) el('vfpPolicyCode').value = suggestCode();
        if (el('vfpEffectiveFrom')) el('vfpEffectiveFrom').value = new Date().toISOString().slice(0, 10);
        // default target type = first card
        const firstTarget = (vocab.targetTypes || [])[0];
        if (firstTarget) { setChecked('vfpTargetType', firstTarget); await renderTargetPicker(firstTarget); }
        await loadContextSelects({});
        refreshPeriodOptions();
        showPicked(el('vfpTargetPicked'), '');
        setLifecycle((vocab.statuses || []).includes('draft') ? 'draft' : getSelectedStatus());
        updatePanel();
    };

    const loadForEdit = async id => {
        currentMode = 'edit';
        currentId = id;
        setFormError('');
        clearErrors();
        buildOnce();
        configureLifecycleForMode();
        let p;
        try { p = await getJson(`/visit-frequency-policies/${id}`); }
        catch (e) { setFormError(e.message || t('ErrorState', 'Error')); return; }
        if (!p) { setFormError(t('ErrorState', 'Error')); return; }

        loadedStatus = norm(p.status) || 'draft';
        setTitle(p.policyName || p.policyCode || t('Edit', 'Edit'));
        el('vfpPolicyCode').value = p.policyCode || '';
        el('vfpPolicyName').value = p.policyName || '';
        el('vfpDescription').value = p.description || '';
        el('vfpFrequencyType').value = p.frequencyType || '';
        refreshPeriodOptions();
        el('vfpRequiredVisitCount').value = p.requiredVisitCount ?? '';
        el('vfpPeriodType').value = p.periodType || '';
        el('vfpSource').value = p.source || '';
        el('vfpEffectiveFrom').value = (p.effectiveFrom || '').slice(0, 10);
        el('vfpEffectiveTo').value = (p.effectiveTo || '').slice(0, 10);
        el('vfpNotes').value = p.notes || '';
        setChecked('vfpPriority', String(p.priority));
        updateFreqSentence();

        setChecked('vfpTargetType', p.targetType);
        await renderTargetPicker(p.targetType, p.targetId, null);
        await loadContextSelects({
            vfpBusinessUnit: p.businessUnit,
            vfpSegment: p.segmentId, vfpCampaign: p.campaignId, vfpBrand: p.brandId,
            vfpProduct: p.productId, vfpCyclePeriod: p.cyclePeriodId,
            vfpTerritoryNode: p.territoryNodeId
        });
        setLifecycle(loadedStatus);
        // Lock code + target AFTER the pickers are (re)built so the freshly rendered target controls are disabled too.
        setCodeReadonly(true);
        // any scope constraint present → open the block so it is visible
        if (selectedScope().length > 0) toggleScope(true);
        applyReadOnlyIfArchived();
        updatePanel();
    };

    const applyReadOnlyIfArchived = () => {
        if (!(currentMode === 'edit' && loadedStatus === 'archived')) return;
        el('vfpArchivedNote')?.classList.remove('vfp-hidden');
        FORM.querySelectorAll('input, select, textarea, button').forEach(c => { c.disabled = true; });
        // WP-FREQ-F9 — re-bind the search selects so their select2 boxes render the disabled (read-only) state too.
        FORM.querySelectorAll('select.select2').forEach(rebindSelect2);
        el('vfpSaveActivate')?.classList.add('vfp-hidden');
        el('vfpSaveDraft')?.classList.add('vfp-hidden');
        el('vfpSaveInactive')?.classList.add('vfp-hidden');
        el('vfpArchive')?.classList.add('vfp-hidden');
    };

    // ── submit + archive ─────────────────────────────────────────────────────────
    let busy = false;
    const setBusy = on => {
        busy = on;
        ['vfpSaveActivate', 'vfpSaveDraft', 'vfpSaveInactive', 'vfpArchive'].forEach(id => { const b = el(id); if (b) b.disabled = on; });
    };
    const redirectToList = () => { window.location.href = LIST_URL; };

    const submit = async status => {
        if (busy) return;
        setLifecycle(status);
        if (!validate()) { setFormError(t('FixErrors', 'Please fix the highlighted fields.')); return; }
        setFormError('');
        setBusy(true);
        const payload = buildPayload();
        const isEdit = currentMode === 'edit';
        const path = isEdit ? `/visit-frequency-policies/${currentId}` : '/visit-frequency-policies';
        try {
            const response = await fetch(`${endpoint}${path}`, {
                method: isEdit ? 'PUT' : 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            await envelope(response);
            window.showToast?.(isEdit ? t('RecordUpdated', 'Saved') : t('RecordCreated', 'Created'), 'success');
            setTimeout(redirectToList, 350);
        } catch (e) {
            setFormError(e.message || t('ErrorState', 'Error'));
            setBusy(false);
        }
    };

    const archive = () => {
        if (busy || currentMode !== 'edit' || !currentId) return;
        const run = async () => {
            setBusy(true);
            try {
                await envelope(await fetch(`${endpoint}/visit-frequency-policies/${currentId}/archive`, {
                    method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
                }));
                window.showToast?.(t('RecordArchived', 'Archived'), 'success');
                setTimeout(redirectToList, 350);
            } catch (e) {
                setFormError(e.message || t('ErrorState', 'Error'));
                setBusy(false);
            }
        };
        if (window.showConfirm) {
            window.showConfirm(t('ArchiveConfirm', ''), run, { type: 'warning', confirmButtonText: t('ArchiveAction', 'Archive') });
        } else { run(); }
    };

    // ── wiring ────────────────────────────────────────────────────────────────────
    FORM.addEventListener('change', event => {
        if (event.target.id === 'vfpFrequencyType') { refreshPeriodOptions(); updatePanel(); return; }
        if (event.target.id === 'vfpPeriodType') { updateFreqSentence(); updatePanel(); return; }
        if (event.target.id === 'vfpTerritoryModel') { void onContextTerritoryModelChange(); return; }
        if (event.target.id === 'vfpTargetTerModel') { void cascadeNodes(el('vfpTargetTerModel'), el('vfpTargetTerNode')); return; }
        if (event.target.id === 'vfpBrand') { applyBrandProductNarrowing(); updatePanel(); return; }
        if (event.target.name === 'vfpTargetType') { void renderTargetPicker(event.target.value); showPicked(el('vfpTargetPicked'), ''); updatePanel(); return; }
        if (event.target.name === 'vfpLifecycle') { setLifecycle(event.target.value); return; }
        if (event.target.name === 'vfpPriority') { updatePanel(); return; }
        if (SCOPE_FIELDS.some(f => f.id === event.target.id) || event.target.id === 'vfpTerritoryNode') { updatePanel(); return; }
    });
    FORM.addEventListener('input', event => {
        if (event.target.id === 'vfpRequiredVisitCount') { updateFreqSentence(); updatePanel(); return; }
        if (event.target.id === 'vfpPolicyName') { updateSummary(); return; }
        if (event.target.id === 'vfpEffectiveFrom' || event.target.id === 'vfpEffectiveTo') { updateSummary(); return; }
        if (event.target.id === 'vfpNotes') { updateChecklist(); return; }
    });

    el('vfpScopeToggle')?.addEventListener('click', () => toggleScope());
    el('vfpScopeClear')?.addEventListener('click', () => clearScope());
    el('vfpSaveActivate')?.addEventListener('click', e => { e.preventDefault(); void submit('active'); });
    el('vfpSaveDraft')?.addEventListener('click', e => { e.preventDefault(); void submit('draft'); });
    el('vfpSaveInactive')?.addEventListener('click', e => { e.preventDefault(); void submit('inactive'); });
    el('vfpArchive')?.addEventListener('click', e => { e.preventDefault(); archive(); });
    FORM.addEventListener('submit', e => { e.preventDefault(); void submit(getSelectedStatus() === 'draft' ? 'draft' : 'active'); });

    // ── page init ───────────────────────────────────────────────────────────────
    const init = async () => {
        try {
            await loadContract();
            if (currentMode === 'edit' && currentId) await loadForEdit(currentId);
            else await resetForCreate();
        } catch (e) {
            setFormError(e.message || t('ContractUnavailable', 'The editor could not be prepared.'));
        }
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})(window, document);
