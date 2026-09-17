/**
 * MOD-0165-FU03 (WP-FREQ-B) Visit Frequency / Call-Cycle Policy — Create/Edit offcanvas editor (frontend only).
 *
 * ONE offcanvas, two modes: "New Policy" (empty) and row "Edit" (GET → fill). The FREQ-A console (index.js) owns the
 * list, the toolbar "New" button and the row "Edit" action, and it is NOT touched — this file only listens (capture
 * phase, so it runs before the console shows the offcanvas) to learn which mode/id the user asked for, then populates
 * and submits the editor when the offcanvas opens.
 *
 * CONTRACT-DRIVEN end to end. targetType / frequencyType / periodType / source / status / PRIORITY BANDS all come from
 * the FU03 /contract endpoint — nothing is a hardcoded vocabulary here. The priority band radio-cards render from
 * contract.vocabulary.priorityBands (code + authored weight, "smaller wins"), labelled through L10n; the mockup's
 * inverted "larger wins" numbers are never used. The ONLY structural rule embedded here is the FrequencyType×PeriodType
 * allow-map (a validation rule the WP enumerates, mirroring the backend), never a vocabulary list.
 *
 * PICKERS show NAMES, never GUIDs: each target/context entity is chosen from an existing sibling list, proxied
 * same-origin (the browser never sees a service URL or a bearer token). The submitted payload maps one-to-one onto the
 * CrmService Create/Update request (TenantId is never sent; PolicyCode + TargetType/TargetId are immutable on edit).
 */
(function (window, document) {
    'use strict';

    const OFFCANVAS = document.getElementById('offcanvasCreateEdit');
    const FORM = document.getElementById('vfpEditorForm');
    if (!OFFCANVAS || !FORM) return;

    const endpoint = FORM.dataset.endpoint || '/CRM/VisitFrequencyPolicies/api';

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
    let currentMode = 'create';
    let currentId = null;

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
    const checkedValue = name => FORM.querySelector(`input[name="${name}"]:checked`)?.value || '';
    const setChecked = (name, value) => {
        const input = FORM.querySelector(`input[name="${name}"][value="${(window.CSS && CSS.escape) ? CSS.escape(value) : value}"]`);
        if (input) input.checked = true;
    };

    // ── build the whole editor once (after contract is available) ──────────────
    const buildOnce = () => {
        if (built) return;

        // target type cards
        renderCards(el('vfpTargetTypeCards'),
            (vocab.targetTypes || []).map(code => ({ value: code, title: t(`TargetType_${code}`, humanize(code)) })),
            'vfpTargetType');

        // priority band cards — CONTRACT bands (code + weight); smaller wins. Sorted by weight ascending (strongest first).
        const bands = (vocab.priorityBands || []).slice().sort((a, b) => (a.value || 0) - (b.value || 0));
        renderCards(el('vfpBandCards'),
            bands.map(b => ({ value: b.value, title: t(`Band_${b.code}`, humanize(b.code)), help: t(`BandHelp_${b.code}`, ''), weight: b.value })),
            'vfpPriority');

        // frequency + source + status vocab selects
        fillVocabSelect(el('vfpFrequencyType'), vocab.frequencyTypes, c => t(`Freq_${c}`, humanize(c)), t('SelectOption', '—'));
        fillVocabSelect(el('vfpSource'), vocab.sources, c => t(`Source_${c}`, humanize(c)), t('SelectOption', '—'));
        fillVocabSelect(el('vfpStatus'), vocab.statuses, statusLabel, null);
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
        const box = el('vfpFreqSentence');
        if (!box) return;
        if (!count || !period) { box.textContent = t('FreqSentenceEmpty', '—'); return; }
        box.textContent = `${count} ${t('PerPeriod', '/')} ${t(`Period_${period}`, humanize(period))}`;
    };

    // ── target picker (varies by target type) ──────────────────────────────────
    const renderTargetPicker = async (targetType, seedId, seedName) => {
        const host = el('vfpTargetPicker');
        const picked = el('vfpTargetPicked');
        if (!host) return;
        const kind = TARGET_KIND[targetType];

        if (targetType === 'territory-node') {
            host.innerHTML = `
                <select class="vfp-select mb-2" id="vfpTargetTerModel"><option value="">${esc(t('SelectTerritoryModel', 'Select model'))}</option></select>
                <select class="vfp-select" id="vfpTargetTerNode" data-role="targetId"><option value="">${esc(t('SelectTerritoryNode', 'Select node'))}</option></select>`;
            const models = await loadOptions('territory-model');
            fillSelect(el('vfpTargetTerModel'), models, t('SelectTerritoryModel', 'Select model'));
            // (edit restore of a territory-node target is best-effort: the node id is seeded so the payload is correct)
            if (seedId) seedSelect(el('vfpTargetTerNode'), seedId, seedName);
            return;
        }

        if (!kind) {
            // account-contact-link (no picker in the mockup) — a manual id keeps it authorable, contract-driven.
            host.innerHTML = `<input type="text" class="vfp-input" data-role="targetId" placeholder="${esc(t('TargetIdManual', 'Enter id (GUID)'))}" value="${esc(seedId || '')}">`;
            return;
        }

        host.innerHTML = `<select class="vfp-select" data-role="targetId"><option value="">${esc(t('SelectOption', '—'))}</option></select>`;
        const control = host.querySelector('[data-role="targetId"]');
        const options = await loadOptions(kind);
        fillSelect(control, options, t('SelectOption', '—'));
        if (seedId) seedSelect(control, seedId, seedName);
        control.addEventListener('change', () => {
            const opt = control.selectedOptions[0];
            showPicked(picked, opt && opt.value ? opt.text : '');
        });
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
    const showPicked = (host, name) => {
        if (!host) return;
        host.textContent = name ? `${t('Selected', 'Selected')}: ${name}` : '';
        host.classList.toggle('is-shown', !!name);
    };

    const currentTargetId = () => norm(FORM.querySelector('#vfpTargetPicker [data-role="targetId"]')?.value);

    // ── context selects (optional provenance) ──────────────────────────────────
    const CONTEXT = [
        { id: 'vfpBusinessUnit', kind: 'business-unit' },
        { id: 'vfpSegment', kind: 'segment' },
        { id: 'vfpCampaign', kind: 'campaign' },
        { id: 'vfpBrand', kind: 'brand' },
        { id: 'vfpProduct', kind: 'product' },
        { id: 'vfpCyclePeriod', kind: 'cycle-period' }
    ];
    const loadContextSelects = async seeds => {
        seeds = seeds || {};
        for (const c of CONTEXT) {
            const select = el(c.id);
            if (!select) continue;
            const options = await loadOptions(c.kind);
            fillSelect(select, options, t('ContextNone', '— none —'));
            const seed = seeds[c.id];
            if (seed) seedSelect(select, seed, seeds[`${c.id}_name`]);
        }
        // territory-node context = cascade model → node
        const modelSel = el('vfpTerritoryModel');
        const nodeSel = el('vfpTerritoryNode');
        if (modelSel) {
            const models = await loadOptions('territory-model');
            fillSelect(modelSel, models, t('SelectTerritoryModel', 'Select model'));
        }
        if (nodeSel && seeds.vfpTerritoryNode) seedSelect(nodeSel, seeds.vfpTerritoryNode, seeds.vfpTerritoryNode_name);
    };
    const cascadeNodes = async (modelSel, nodeSel) => {
        if (!modelSel || !nodeSel) return;
        const modelId = norm(modelSel.value);
        const options = modelId ? await loadOptions('territory-node', modelId) : [];
        fillSelect(nodeSel, options, t('SelectTerritoryNode', 'Select node'));
    };
    const onContextTerritoryModelChange = () => cascadeNodes(el('vfpTerritoryModel'), el('vfpTerritoryNode'));

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
    const setTitle = text => { const h = el('offcanvasCreateEditLabel'); if (h) h.textContent = text; };
    const setCodeReadonly = ro => {
        const code = el('vfpPolicyCode');
        if (code) code.readOnly = ro;
        el('vfpTargetLockNote')?.classList.toggle('vfp-hidden', !ro);
        // disable target type cards on edit (immutable)
        FORM.querySelectorAll('input[name="vfpTargetType"]').forEach(i => { i.disabled = ro; });
        el('vfpTargetPicker')?.querySelectorAll('select,input').forEach(i => { i.disabled = ro; });
    };
    const suggestCode = () => `vfp-${new Date().getFullYear()}-${Math.random().toString(36).slice(2, 8)}`;

    const resetForCreate = async () => {
        currentMode = 'create';
        currentId = null;
        setTitle(t('NewPolicy', 'New Policy'));
        setFormError('');
        clearErrors();
        FORM.reset();
        buildOnce();
        setCodeReadonly(false);
        if (el('vfpPolicyCode')) el('vfpPolicyCode').value = suggestCode();
        if (el('vfpEffectiveFrom')) el('vfpEffectiveFrom').value = new Date().toISOString().slice(0, 10);
        if (el('vfpStatus') && (vocab.statuses || []).includes('draft')) el('vfpStatus').value = 'draft';
        // default target type = first card
        const firstTarget = (vocab.targetTypes || [])[0];
        if (firstTarget) { setChecked('vfpTargetType', firstTarget); await renderTargetPicker(firstTarget); }
        await loadContextSelects({});
        refreshPeriodOptions();
        showPicked(el('vfpTargetPicked'), '');
    };

    const loadForEdit = async id => {
        currentMode = 'edit';
        currentId = id;
        setFormError('');
        clearErrors();
        buildOnce();
        let p;
        try { p = await getJson(`/visit-frequency-policies/${id}`); }
        catch (e) { setFormError(e.message || t('ErrorState', 'Error')); return; }
        if (!p) { setFormError(t('ErrorState', 'Error')); return; }

        setTitle(p.policyName || p.policyCode || t('Edit', 'Edit'));
        el('vfpPolicyCode').value = p.policyCode || '';
        el('vfpPolicyName').value = p.policyName || '';
        el('vfpDescription').value = p.description || '';
        el('vfpFrequencyType').value = p.frequencyType || '';
        refreshPeriodOptions();
        el('vfpRequiredVisitCount').value = p.requiredVisitCount ?? '';
        el('vfpPeriodType').value = p.periodType || '';
        el('vfpSource').value = p.source || '';
        if (el('vfpStatus')) el('vfpStatus').value = p.status || 'draft';
        el('vfpEffectiveFrom').value = (p.effectiveFrom || '').slice(0, 10);
        el('vfpEffectiveTo').value = (p.effectiveTo || '').slice(0, 10);
        el('vfpNotes').value = p.notes || '';
        setChecked('vfpPriority', String(p.priority));
        updateFreqSentence();
        toggleSourceNote();

        setChecked('vfpTargetType', p.targetType);
        await renderTargetPicker(p.targetType, p.targetId, null);
        await loadContextSelects({
            vfpBusinessUnit: p.businessUnit,
            vfpSegment: p.segmentId, vfpCampaign: p.campaignId, vfpBrand: p.brandId,
            vfpProduct: p.productId, vfpCyclePeriod: p.cyclePeriodId,
            vfpTerritoryNode: p.territoryNodeId
        });
        // Lock code + target AFTER the pickers are (re)built so the freshly rendered target controls are disabled too.
        setCodeReadonly(true);
    };

    const toggleSourceNote = () => {
        const note = el('vfpSourceNote');
        if (note) note.classList.toggle('vfp-hidden', false); // the "does not affect frequency" note always shows
    };

    // ── submit ───────────────────────────────────────────────────────────────────
    const submit = async () => {
        if (!validate()) { setFormError(t('FixErrors', 'Please fix the highlighted fields.')); return; }
        setFormError('');
        const btn = el('vfpSaveBtn');
        if (btn) btn.disabled = true;
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
            window.bootstrap?.Offcanvas.getOrCreateInstance(OFFCANVAS).hide();
            // FREQ-A owns the list; a reload is the simplest way to reflect the change without touching index.js.
            setTimeout(() => window.location.reload(), 350);
        } catch (e) {
            setFormError(e.message || t('ErrorState', 'Error'));
            if (btn) btn.disabled = false;
        }
    };

    // ── wiring ────────────────────────────────────────────────────────────────────
    // Capture phase so we learn the intended mode/id BEFORE the FREQ-A console shows the offcanvas.
    let pending = { mode: 'create', id: null };
    document.addEventListener('click', event => {
        const edit = event.target.closest('.js-vfp-edit');
        if (edit) { pending = { mode: 'edit', id: edit.dataset.id }; return; }
        const addNew = event.target.closest('.add-new, [data-vfp-new]');
        if (addNew) { pending = { mode: 'create', id: null }; }
    }, true);

    OFFCANVAS.addEventListener('show.bs.offcanvas', async () => {
        try {
            await loadContract();
            if (pending.mode === 'edit' && pending.id) await loadForEdit(pending.id);
            else await resetForCreate();
        } catch (e) {
            setFormError(e.message || t('ContractUnavailable', 'The editor could not be prepared.'));
        }
    });

    FORM.addEventListener('change', event => {
        if (event.target.id === 'vfpFrequencyType') { refreshPeriodOptions(); return; }
        if (event.target.id === 'vfpPeriodType') { updateFreqSentence(); return; }
        if (event.target.id === 'vfpTerritoryModel') { void onContextTerritoryModelChange(); return; }
        if (event.target.id === 'vfpTargetTerModel') { void cascadeNodes(el('vfpTargetTerModel'), el('vfpTargetTerNode')); return; }
        if (event.target.name === 'vfpTargetType') { void renderTargetPicker(event.target.value); showPicked(el('vfpTargetPicked'), ''); return; }
    });
    FORM.addEventListener('input', event => {
        if (event.target.id === 'vfpRequiredVisitCount') updateFreqSentence();
    });
    el('vfpSaveBtn')?.addEventListener('click', event => { event.preventDefault(); void submit(); });
    FORM.addEventListener('submit', event => { event.preventDefault(); void submit(); });
})(window, document);
