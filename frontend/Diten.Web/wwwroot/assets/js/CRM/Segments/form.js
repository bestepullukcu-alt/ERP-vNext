/**
 * MOD-0167-FU02 Segments — Create/Edit page: the EMBEDDED criteria BLOCK editor, the live reach rail and the EMBEDDED
 * manual membership sub-editor. All three live inside this one Compact page; none is a second golden-reference surface.
 *
 * BLOCK MODEL (WP-SEG-A). The rule is authored as two FIXED layers, never a free tree:
 *   - a BLOCK groups conditions that combine with EVERY (all) or ANY (one) — the block's own toggle;
 *   - the blocks themselves ALWAYS combine with AND ALSO.
 * There is no nesting control, no move control and no NOT toggle. "is none of" is the `not-in` operator, so negation
 * lives in the operator, not in a separate switch. This maps to the EXACT stored tree the runtime persists:
 *   MatchMode = all  ·  one GROUP node per block (groupOperator every=and / any=or)  ·  one PREDICATE node per condition.
 * The posted payload (buildNodes) is byte-identical to the old tree editor's — the runtime still assigns the real
 * NodeIds and remaps the parents. Depth is 2 by construction, so the depth limit can never be breached here.
 *
 * CATALOG-DRIVEN end to end. The attribute list, its presentation DOMAIN (SEG-B — the optgroup a criterion renders
 * under), the operators allowed for each attribute, the value type/arity, the required/optional parameters AND (P1a)
 * where a legitimate value comes from all arrive from /attribute-catalog at runtime. There is NO hardcoded attribute,
 * operator, domain or value list in this file — only presentation LABELS (a code → business-language phrase), which are
 * translation, not a second source of truth. The operator business language: is any of=in, is none of=not-in,
 * is at least=gte (+ the rest), all rendered from the catalog's own operator set.
 *
 * P1a — the value control follows the attribute's declared value source, with a SOURCE BADGE beside it:
 *   reference-set  -> Select2 over the tenant's PUBLISHED MOD-0048 values (empty when unpublished, never a local list)
 *   enum           -> the closed value list the catalog itself carries
 *   entity-picker  -> the aggregate's existing selector (account / territory model+node / MDM product / brand)
 *   free-text      -> by value type: date picker, number input, bool toggle, plain text
 * Every one still ACCEPTS A TYPED VALUE (Select2 tags / free text), because the value source is a hint about what is
 * offered, never a restriction on what the runtime takes.
 *
 * REACH (SEG-C). A debounced POST to /preview answers "who would this rule reach right now?" — a total, a per-condition
 * funnel ("N match this alone"), a bounded member sample and an activation checklist. It PERSISTS NOTHING and membership
 * is never stored; the sample carries only a display name. All traffic goes through the same-origin MVC proxy: the
 * browser never builds a bearer token or touches a cookie.
 */
(function (window, document) {
    'use strict';

    const editor = document.getElementById('criteriaEditor');
    const hidden = document.getElementById('CriteriaJson');
    if (!editor || !hidden) return;

    const L = window.SegmentsL10n || window.L10n || {};
    const endpoint = editor.dataset.endpoint || '/CRM/Segments/api';
    const segmentId = editor.dataset.segmentId || '';
    const isFrozen = editor.dataset.frozen === 'true';
    const maxNodes = parseInt(editor.dataset.maxNodes || '100', 10);
    const maxChildren = parseInt(editor.dataset.maxChildren || '20', 10);

    const listEl = document.getElementById('criteriaList');
    const emptyEl = document.getElementById('criteriaEmpty');
    const templatesEl = document.getElementById('criteriaTemplates');
    const readbackEl = document.getElementById('criteriaReadback');
    const readbackTextEl = document.getElementById('criteriaReadbackText');
    const storedRuleEl = document.getElementById('storedRule');
    const storedRuleJsonEl = document.getElementById('storedRuleJson');
    const subjectTypeEl = document.getElementById('SubjectType');
    const segmentTypeEl = document.getElementById('SegmentType');
    const matchModeEl = document.getElementById('MatchMode');

    let bootstrap = {};
    try { bootstrap = JSON.parse(document.getElementById('segmentFormBootstrap')?.textContent || '{}'); }
    catch (e) { bootstrap = {}; }
    const availablePickers = new Set(bootstrap.availablePickers || []);

    let catalog = null;
    let contract = null;
    let blocks = [];              // the authored block model — the single in-memory source of truth
    let lastConditionCounts = {}; // nodeId -> { count, capExceeded } from the most recent /preview

    // One cache per option source, so re-rendering never re-fetches a list.
    const optionCache = new Map();

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const uid = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };
    const getJson = path => fetch(`${endpoint}${path}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(envelope);

    // Subject is now a pill toggle and segment type a radio-card group (both post their value through a checked radio;
    // for an immutable subject on edit the disabled-but-checked pill still answers here, and a hidden input posts it).
    const checkedValue = name => document.querySelector(`input[name="${name}"]:checked`)?.value;
    const currentSubjectType = () => (checkedValue('SubjectType') || subjectTypeEl?.value || editor.dataset.subjectType || 'contact').trim();
    const currentSegmentType = () => (checkedValue('SegmentType') || segmentTypeEl?.value || 'dynamic').trim();

    // ---------------------------------------------------------------- presentation labels (translation only)

    // Operator code -> business-language L10n key. The SET of operators is still the catalog's; this only translates.
    const OPERATOR_LABEL_KEY = {
        'eq': 'OpIs', 'ne': 'OpIsNot', 'in': 'OpIsAnyOf', 'not-in': 'OpIsNoneOf', 'contains': 'OpContains',
        'gt': 'OpMoreThan', 'gte': 'OpAtLeast', 'lt': 'OpLessThan', 'lte': 'OpAtMost', 'between': 'OpBetween',
        'is-null': 'OpIsEmpty', 'is-not-null': 'OpIsSet'
    };
    const operatorLabel = op => L[OPERATOR_LABEL_KEY[op]] || op;

    // Presentation domain (SEG-B) -> optgroup label + stable render order.
    const DOMAIN_LABEL_KEY = {
        'doctor-profile': 'DomainDoctorProfile', 'consent': 'DomainConsent', 'workplace': 'DomainWorkplace',
        'commercial': 'DomainCommercial', 'activity': 'DomainActivity', 'institution': 'DomainInstitution'
    };
    const DOMAIN_ORDER = ['doctor-profile', 'consent', 'workplace', 'commercial', 'activity', 'institution'];
    const domainLabel = d => L[DOMAIN_LABEL_KEY[d]] || d || (L.DomainOther || 'Other');

    // Human label for an attribute code: the last dotted segment, dashes to spaces, capitalised. Derived from the code
    // (never a hardcoded list); the optgroup already carries the domain context.
    const attributeLabel = code => {
        const raw = String(code || '').split('.').pop().replace(/-/g, ' ').trim();
        return raw ? raw.charAt(0).toUpperCase() + raw.slice(1) : code;
    };

    const entityLabel = kind => attributeLabel(kind);

    /** The value-source badge: where a legitimate value comes from. Derived from the catalog's declared kind. */
    const sourceBadge = definition => {
        const src = definition?.valueSource || { kind: 'free-text' };
        switch (src.kind) {
            case 'reference-set': return { text: L.SourceReferenceSet || 'Reference set', tone: 'info' };
            case 'enum': return { text: L.SourceCatalogList || 'Catalog list', tone: 'info' };
            case 'entity-picker': return { text: `${L.SourcePicker || 'Picker'} · ${entityLabel(src.entityKind)}`, tone: 'primary' };
            default: return { text: L.SourceFreeText || 'Free text', tone: 'secondary' };
        }
    };

    /**
     * Shows only the sections the chosen segment type can actually use, mirroring what the runtime already enforces:
     *   static  -> membership IS the manual list, so the criteria block editor and the reach rail are hidden
     *   dynamic -> the rule decides everything, so a manual row is refused with a 400
     *   hybrid  -> both.
     */
    const applySegmentTypeVisibility = () => {
        const type = currentSegmentType();
        document.getElementById('criteriaSection')?.classList.toggle('d-none', type === 'static');
        document.getElementById('manualMembershipSection')?.classList.toggle('d-none', type === 'dynamic');
        // The reach rail previews a rule, so a static segment (no rule) does not get one.
        document.getElementById('reachSection')?.classList.toggle('d-none', type === 'static');
    };

    const attributeFor = code => (catalog?.attributes || []).find(a => a.attributeCode === code) || null;
    const applicableAttributes = () => (catalog?.attributes || [])
        .filter(a => (a.subjectTypes || []).includes(currentSubjectType()));

    // ---------------------------------------------------------------- option sources (P1a)

    /** Published MOD-0048 values for one set. An unpublished set yields an EMPTY list — never a local fallback. */
    const loadReferenceValues = async setCode => {
        const key = `set:${setCode}`;
        if (optionCache.has(key)) return optionCache.get(key);
        let options = [];
        try {
            const data = await getJson(`/reference-values/${encodeURIComponent(setCode)}`);
            const items = data?.items || data || [];
            options = items
                .filter(x => x.isActive !== false && (x.value || x.valueCode))
                .map(x => ({ value: x.value || x.valueCode, text: x.text || x.displayName || x.value || x.valueCode }));
        } catch (e) {
            options = [];
        }
        optionCache.set(key, options);
        return options;
    };

    /** The existing selector for an id-valued attribute. Nothing new is opened here. */
    const loadEntityOptions = async (entityKind, context) => {
        const key = `entity:${entityKind}:${context || ''}`;
        if (optionCache.has(key)) return optionCache.get(key);

        const readers = {
            'global-product': () => getJson('/global-products?pageSize=200'),
            'account': () => getJson('/accounts?pageSize=200'),
            'territory-model': () => getJson('/territory-models?pageSize=200'),
            'territory-node': () => context ? getJson(`/territory-models/${context}/nodes`) : Promise.resolve([]),
            'mdm-product': () => getJson('/mdm-products?pageSize=200'),
            'mdm-brand': () => getJson('/mdm-brands?pageSize=200')
        };

        let options = [];
        try {
            const data = await (readers[entityKind] ? readers[entityKind]() : Promise.resolve([]));
            const items = Array.isArray(data) ? data : (data?.items || data?.nodes || []);
            options = items
                .map(x => ({
                    value: x.id || x.value || x.accountId || x.territoryModelId || x.territoryNodeId
                        || x.globalProductId || x.productId || x.brandId,
                    // SEG-E: PascalCase eşleri de düşülür — endpoint PascalCase döndürürse text=id (GUID)
                    // fallback'ine düşmesin, dropdown gerçek isim göstersin. Value/GUID gönderimi DEĞİŞMEZ.
                    text: x.name || x.Name || x.text || x.accountName || x.AccountName
                        || x.modelName || x.ModelName || x.territoryName || x.TerritoryName
                        || x.nodeName || x.NodeName || x.globalProductName || x.GlobalProductName
                        || x.productName || x.ProductName || x.brandName || x.BrandName
                        || x.code || x.id
                }))
                .filter(x => x.value);
        } catch (e) {
            options = [];
        }
        optionCache.set(key, options);
        return options;
    };

    const pickerAvailable = entityKind => availablePickers.has(entityKind);

    // ---------------------------------------------------------------- block model

    const allConditions = () => blocks.flatMap(b => b.conditions);
    const totalNodeCount = () => blocks.length + allConditions().length;
    const findBlock = id => blocks.find(b => b.blockId === id);
    const findCondition = id => allConditions().find(c => c.nodeId === id);

    /** Re-shapes a condition around a newly chosen attribute: operator, value type and parameter set all come from the
     *  catalog, so an attribute change can never leave an operator the runtime would reject. */
    const applyAttribute = (condition, attributeCode) => {
        const definition = attributeFor(attributeCode);
        if (!definition) return;
        condition.attributeCode = definition.attributeCode;
        condition.valueType = definition.valueType;
        condition.operator = definition.operators.includes(condition.operator) ? condition.operator : definition.operators[0];
        condition.values = [];
        condition.valueLabels = {}; // SEG-E: eski etiketler (UI-only) sıfırlanır; payload'a girmez
        const kept = {};
        (definition.requiredParameters || []).concat(definition.optionalParameters || []).forEach(p => {
            kept[p] = (condition.parameters || {})[p] || '';
        });
        condition.parameters = kept;
    };

    const makeCondition = () => {
        const condition = { nodeId: uid(), attributeCode: null, operator: null, values: [], valueType: null, parameters: {} };
        const first = applicableAttributes()[0];
        if (first) applyAttribute(condition, first.attributeCode);
        return condition;
    };

    const makeBlock = () => ({ blockId: uid(), match: 'and', conditions: [makeCondition()] });

    const addBlock = () => {
        if (isFrozen) return false;
        if (totalNodeCount() + 2 > maxNodes) {
            window.showToast?.(`${L.LimitReached || 'Limit'} (${maxNodes})`, 'error');
            return false;
        }
        if (!applicableAttributes()[0]) {
            window.showToast?.(L.SegmentContractUnavailable || 'No attribute available', 'error');
            return false;
        }
        blocks.push(makeBlock());
        return true;
    };

    const addCondition = blockId => {
        if (isFrozen) return false;
        const block = findBlock(blockId);
        if (!block) return false;
        if (block.conditions.length >= maxChildren) {
            window.showToast?.(`${L.LimitReached || 'Limit'} (${maxChildren})`, 'error');
            return false;
        }
        if (totalNodeCount() + 1 > maxNodes) {
            window.showToast?.(`${L.LimitReached || 'Limit'} (${maxNodes})`, 'error');
            return false;
        }
        block.conditions.push(makeCondition());
        return true;
    };

    const removeCondition = (blockId, nodeId) => {
        const block = findBlock(blockId);
        if (!block) return;
        block.conditions = block.conditions.filter(c => c.nodeId !== nodeId);
        // A block with no conditions is a 400 nobody meant to author, so it goes with its last condition.
        if (block.conditions.length === 0) removeBlock(blockId);
    };

    const removeBlock = blockId => { blocks = blocks.filter(b => b.blockId !== blockId); };

    /** Any condition pointing at an attribute the current subject type cannot use is re-pointed at the first one that
     *  applies (subject type is immutable after create, so this only ever runs while authoring a new segment). */
    const normalizeForSubject = () => {
        const applicable = new Set(applicableAttributes().map(a => a.attributeCode));
        const first = applicableAttributes()[0];
        allConditions().forEach(c => {
            if (!applicable.has(c.attributeCode) && first) applyAttribute(c, first.attributeCode);
        });
    };

    // ---------------------------------------------------------------- block -> stored tree (payload UNCHANGED)

    const buildNodes = () => {
        const nodes = [];
        blocks.forEach((block, bi) => {
            nodes.push({
                nodeId: block.blockId, parentNodeId: null, nodeKind: 'group',
                groupOperator: block.match, attributeCode: null, operator: null, values: [],
                valueType: null, parameters: {}, negate: false, sortOrder: bi, label: null
            });
            block.conditions.forEach((c, ci) => {
                nodes.push({
                    nodeId: c.nodeId, parentNodeId: block.blockId, nodeKind: 'predicate',
                    groupOperator: null, attributeCode: c.attributeCode || null, operator: c.operator || null,
                    values: (c.values || []).filter(v => String(v ?? '').trim() !== ''),
                    valueType: c.valueType || null, parameters: c.parameters || {}, negate: false,
                    sortOrder: ci, label: null
                });
            });
        });
        return nodes;
    };

    const syncHidden = () => {
        // A static segment must POST an EMPTY tree: the runtime refuses one that carries criteria, and hiding the
        // section does not stop a hidden input from submitting. The in-memory blocks are left intact, so switching back
        // to hybrid restores the rule instead of silently destroying the author's work. MatchMode is fixed to all.
        if (matchModeEl) matchModeEl.value = 'all';
        if (currentSegmentType() === 'static') {
            hidden.value = '[]';
        } else {
            hidden.value = JSON.stringify(buildNodes());
        }
        if (storedRuleJsonEl) storedRuleJsonEl.textContent = JSON.stringify(JSON.parse(hidden.value || '[]'), null, 2);
        storedRuleEl?.classList.toggle('d-none', blocks.length === 0 || currentSegmentType() === 'static');
    };

    // ---------------------------------------------------------------- value controls (P1a)

    const isMultiValue = operator => operator === 'in' || operator === 'not-in';

    const arityOf = operator => {
        switch (operator) {
            case 'is-null':
            case 'is-not-null': return { min: 0, max: 0 };
            case 'between': return { min: 2, max: 2 };
            case 'in':
            case 'not-in': return { min: 1, max: catalog?.maxValuesPerInOperator || 50 };
            default: return { min: 1, max: 1 };
        }
    };

    const controlFor = condition => {
        const definition = attributeFor(condition.attributeCode);
        const source = definition?.valueSource || { kind: 'free-text' };
        const multi = isMultiValue(condition.operator);

        // WP-SEG-A3: catalog list values render as toggle CHIPS + a free-text input (the mockup look). The value source
        // is unchanged — reference-set values still load from the tenant's PUBLISHED MOD-0048 set through the proxy, and
        // enum values are still the catalog's own closed list. Chips accept a typed value too (the free-text field), so
        // the source stays a hint, never a restriction.
        if (source.kind === 'reference-set') {
            return { control: 'chips', multi, async: () => loadReferenceValues(source.referenceSetCode) };
        }
        if (source.kind === 'enum') {
            return { control: 'chips', multi, options: (source.allowedValues || []).map(v => ({ value: v, text: v })) };
        }
        if (source.kind === 'entity-picker') {
            if (!pickerAvailable(source.entityKind)) {
                return { control: 'text', multi: false, disabledReason: L.PickerUnavailable };
            }
            if (source.entityKind === 'territory-node') {
                return { control: 'cascade-select', multi, entityKind: 'territory-node' };
            }
            return { control: 'select', multi, async: () => loadEntityOptions(source.entityKind), taggable: true };
        }

        switch (condition.valueType) {
            case 'date': return { control: 'date', multi: false };
            case 'number': return { control: 'number', multi: false };
            case 'bool': return { control: 'bool', multi: false };
            default: return { control: 'text', multi };
        }
    };

    const valueSlotCount = condition => {
        const arity = arityOf(condition.operator);
        if (arity.max === 0) return 0;
        if (isMultiValue(condition.operator)) return 1;
        return arity.min === 2 ? 2 : 1;
    };

    /** One toggle chip. Selected chips carry a check; the click handler writes straight to condition.values, so the
     *  posted payload is identical to the old Select2 selection. */
    const chipHtml = (nodeId, value, text, on) =>
        `<button type="button" class="seg-chip js-value-chip${on ? ' is-on' : ''}" data-node="${esc(nodeId)}" data-value="${esc(value)}">${on ? '<span class="seg-chip-check">✓</span>' : ''}${esc(text)}</button>`;

    /** One SINGLE-SELECT parameter chip (WP-SEG-H). Same look as a value chip; the click handler writes the chosen
     *  value straight to condition.parameters[name], so the posted payload is byte-identical to the old select. */
    const paramChipHtml = (nodeId, name, value, text, on) =>
        `<button type="button" class="seg-chip js-param-chip${on ? ' is-on' : ''}" data-node="${esc(nodeId)}" data-param="${esc(name)}" data-value="${esc(value)}">${on ? '<span class="seg-chip-check">✓</span>' : ''}${esc(text)}</button>`;

    const renderValueControl = (condition, index, spec) => {
        const values = condition.values || [];
        const single = values[index] ?? '';

        // Boolean → two toggle chips (the catalog's declared bool). Single-valued, so picking one replaces the other.
        if (spec.control === 'bool') {
            const isTrue = String(single) === 'true';
            return `<div class="seg-chip-wrap">
                        ${chipHtml(condition.nodeId, 'true', L.BoolTrue || 'Yes', isTrue)}
                        ${chipHtml(condition.nodeId, 'false', L.BoolFalse || 'No', String(single) === 'false')}
                    </div>`;
        }

        // Catalog list (reference-set / enum) → chips + free-text. The chips are filled in hydrateControls, because a
        // reference-set loads its published values asynchronously through the same-origin proxy.
        if (spec.control === 'chips') {
            return `<div class="seg-chip-wrap js-chips" data-node="${esc(condition.nodeId)}"></div>`;
        }

        if (spec.control === 'date') {
            return `<input type="text" class="seg-value-input flatpickr-date js-node-value"
                        data-node="${esc(condition.nodeId)}" data-index="${index}" value="${esc(single)}"
                        placeholder="YYYY-MM-DD" autocomplete="off" />`;
        }

        if (spec.control === 'number') {
            return `<input type="number" step="any" class="seg-value-input seg-value-input-number js-node-value"
                        data-node="${esc(condition.nodeId)}" data-index="${index}" value="${esc(single)}" />`;
        }

        // Entity picker (accounts / territory nodes / MDM products…) keeps Select2 — these lists can run to hundreds of
        // rows, so chips would not scale. It stays catalog-driven and taggable, exactly as before.
        if (spec.control === 'select' || spec.control === 'cascade-select') {
            const seeded = spec.multi ? values : [single];
            const seedOptions = seeded.filter(v => String(v ?? '').trim() !== '')
                .map(v => `<option value="${esc(v)}" selected>${esc(v)}</option>`).join('');
            const cascade = spec.control === 'cascade-select'
                ? `<select class="form-select form-select-sm mb-1 js-node-cascade" data-node="${esc(condition.nodeId)}">
                       <option value="">${esc(L.SelectTerritoryModel || '')}</option>
                   </select>`
                : '';
            return cascade + `<select class="form-select form-select-sm js-node-select"
                        data-node="${esc(condition.nodeId)}" data-index="${index}"
                        data-taggable="${spec.taggable ? '1' : '0'}"
                        ${spec.multi ? 'multiple="multiple"' : ''}>
                        ${spec.multi ? '' : `<option value="">${esc(L.SelectOption || '')}</option>`}
                        ${seedOptions}
                    </select>`;
        }

        const disabledNote = spec.disabledReason
            ? `<small class="text-warning d-block">${esc(spec.disabledReason)}</small>` : '';
        if (spec.multi) {
            return `<input type="text" class="seg-value-input js-node-multitext"
                        data-node="${esc(condition.nodeId)}" value="${esc(values.join(', '))}"
                        placeholder="${esc(L.CommaSeparated || '')}" />${disabledNote}`;
        }
        return `<input type="text" class="seg-value-input js-node-value"
                    data-node="${esc(condition.nodeId)}" data-index="${index}" value="${esc(single)}" />${disabledNote}`;
    };

    /** Parameters get their OWN row under the three main controls, so they never squeeze the value field. */
    const parameterFields = condition => {
        const definition = attributeFor(condition.attributeCode);
        if (!definition) return '';
        // Only REQUIRED parameters get a field. Optional parameters (the niche ones the mockup never shows, e.g.
        // concept.affinity maxDepth or a subjectId hint) are hidden in Phase-1: their values stay seeded (empty) in
        // the model by applyAttribute, so the posted payload is unchanged — the bare input is simply not rendered.
        const required = definition.requiredParameters || [];
        if (required.length === 0) return '';

        // WP-SEG-G/H: a parameter can declare a value-source (parameterValueSources[name]) exactly as the main value
        // does. When it is a closed enum (e.g. consent.eligibility channel/purpose, from ConsentChannel.All/
        // ConsentPurpose.All), render the SAME value-box chip look as the eligibility main value — "Pick one · Catalog
        // list" (source badge from valueSource.kind) + SINGLE-SELECT chips + "or type a value" free-text — instead of a
        // bare box; otherwise keep the plain input. Either way the chosen value is written to the SAME
        // condition.parameters[name] slot (payload shape unchanged) and a value outside the list is still preserved as a
        // selected chip, so the source stays a hint, never a restriction.
        const paramSources = definition.parameterValueSources || {};
        const fields = required.map(name => {
            const value = (condition.parameters || {})[name] || '';
            const src = paramSources[name];
            let control;
            if (src && src.kind === 'enum') {
                // allowedValues are the catalog's own closed list (synchronous), so the chips are filled here directly
                // (no async hydrate). A free-typed value outside the list survives as a selected chip.
                const opts = (src.allowedValues || []).map(String);
                const selected = String(value);
                const extras = selected.trim() !== '' && !opts.includes(selected) ? [selected] : [];
                const chips = opts.concat(extras)
                    .map(o => paramChipHtml(condition.nodeId, name, o, o, o === selected)).join('');
                const free = `<input type="text" class="seg-freetext js-param-freetext"
                            data-node="${esc(condition.nodeId)}" data-param="${esc(name)}"
                            placeholder="${esc(L.OrTypeValue || '')}"${isFrozen ? ' disabled' : ''} />`;
                control = `<div class="seg-value-box">
                            <div class="seg-value-prompt-row">
                                <span class="seg-value-prompt">${esc(L.ValuePick || 'Pick one')}</span>
                                <span class="seg-source-badge">${esc(sourceBadge({ valueSource: src }).text)}</span>
                            </div>
                            <div class="seg-chip-wrap">${chips}${free}</div>
                        </div>`;
            } else {
                control = `<input type="text" class="seg-value-input js-node-param"
                            data-node="${esc(condition.nodeId)}" data-param="${esc(name)}" value="${esc(value)}" />`;
            }
            return `<div class="seg-param">
                        <label class="seg-param-label">${esc(name)} <span class="seg-req">*</span></label>
                        ${control}
                    </div>`;
        }).join('');

        return `<div class="seg-param-row">${fields}</div>`;
    };

    // ---------------------------------------------------------------- rendering

    /** The attribute <select> as SEG-B optgroups: one per presentation domain, in a stable order, humanised labels. */
    const attributeOptions = selectedCode => {
        const byDomain = new Map();
        applicableAttributes().forEach(a => {
            const domain = a.domain || 'other';
            if (!byDomain.has(domain)) byDomain.set(domain, []);
            byDomain.get(domain).push(a);
        });
        const domains = [...DOMAIN_ORDER.filter(d => byDomain.has(d)), ...[...byDomain.keys()].filter(d => !DOMAIN_ORDER.includes(d))];
        return domains.map(domain => {
            const opts = byDomain.get(domain)
                .sort((a, b) => attributeLabel(a.attributeCode).localeCompare(attributeLabel(b.attributeCode)))
                .map(a => `<option value="${esc(a.attributeCode)}"${a.attributeCode === selectedCode ? ' selected' : ''}>${esc(attributeLabel(a.attributeCode))}</option>`)
                .join('');
            return `<optgroup label="${esc(domainLabel(domain))}">${opts}</optgroup>`;
        }).join('');
    };

    const renderCondition = (condition, block, index) => {
        const definition = attributeFor(condition.attributeCode);
        const operators = (definition?.operators || []).map(op =>
            `<option value="${esc(op)}"${op === condition.operator ? ' selected' : ''}>${esc(operatorLabel(op))}</option>`).join('');

        const spec = controlFor(condition);
        const slots = valueSlotCount(condition);
        const badge = sourceBadge(definition);

        // Lead word mirrors the mockup: the first condition reads "WHERE", the rest read "AND" / "OR" from the block's
        // own match mode.
        const lead = index === 0
            ? (L.ReadsWhere || 'where')
            : (block.match === 'or' ? (L.JoinerOr || 'or') : (L.JoinerAnd || 'and'));

        const isListLike = spec.control === 'chips' || spec.control === 'select' || spec.control === 'cascade-select';
        const prompt = spec.control === 'chips' ? (L.ValuePick || 'Pick one or more')
            : spec.control === 'bool' ? (L.ValueSet || 'Set to')
                : (L.ValueLabel || 'Value');

        let valueBox;
        if (slots === 0) {
            valueBox = `<div class="seg-value-box seg-value-inline"><span class="seg-value-prompt">${esc(L.NoValueNeeded || '—')}</span></div>`;
        } else if (slots === 2) {
            valueBox = `<div class="seg-value-box"><div class="seg-value-inline">
                    ${Array.from({ length: 2 }, (_, i) => `<span class="seg-value-prompt">${esc(i === 0 ? (L.From || 'From') : (L.To || 'To'))}</span>${renderValueControl(condition, i, spec)}`).join('')}
                </div></div>`;
        } else if (isListLike) {
            valueBox = `<div class="seg-value-box">
                    <div class="seg-value-prompt-row">
                        <span class="seg-value-prompt">${esc(prompt)}</span>
                        <span class="seg-source-badge">${esc(badge.text)}</span>
                    </div>
                    ${renderValueControl(condition, 0, spec)}
                </div>`;
        } else {
            valueBox = `<div class="seg-value-box seg-value-inline">
                    <span class="seg-value-prompt">${esc(prompt)}</span>
                    ${renderValueControl(condition, 0, spec)}
                </div>`;
        }

        const removeBtn = isFrozen ? '' : `<button type="button" class="seg-remove-cond js-remove-condition"
                    data-block="${esc(block.blockId)}" data-node="${esc(condition.nodeId)}" title="${esc(L.Remove || 'Remove')}">✕</button>`;

        return `
        <div class="seg-cond">
            <div class="seg-cond-top">
                <span class="seg-cond-lead">${esc(lead)}</span>
                <select class="seg-cond-attr js-node-attribute" data-node="${esc(condition.nodeId)}">${attributeOptions(condition.attributeCode)}</select>
                <select class="seg-cond-op js-node-operator" data-node="${esc(condition.nodeId)}">${operators}</select>
                ${removeBtn}
            </div>
            <div class="seg-cond-body">
                ${valueBox}
                ${parameterFields(condition)}
                <div class="seg-cond-foot">
                    <span class="seg-readback">${esc(conditionText(condition))}</span>
                    <span class="seg-reach-chip js-cond-count-row seg-hidden" data-node="${esc(condition.nodeId)}"></span>
                </div>
            </div>
        </div>`;
    };

    const renderBlock = (block, index) => {
        // "Include people where [every|any] condition below is true" — a segmented toggle. The stored value is unchanged
        // (every=and, any=or); the change handler still reads it off the checked radio's data-block/value.
        const toggleName = `blockmatch-${block.blockId}`;
        const isEvery = block.match === 'and';
        const matchToggle = `
            <span class="seg-toggle" role="group" aria-label="${esc(L.BlockMatchLabel || 'match')}">
                <input type="radio" class="btn-check js-block-match" name="${esc(toggleName)}" id="bm-and-${esc(block.blockId)}"
                    data-block="${esc(block.blockId)}" value="and" autocomplete="off"${isEvery ? ' checked' : ''}>
                <label class="seg-toggle-btn" for="bm-and-${esc(block.blockId)}">${esc(L.BlockToggleEvery || 'every')}</label>
                <input type="radio" class="btn-check js-block-match" name="${esc(toggleName)}" id="bm-or-${esc(block.blockId)}"
                    data-block="${esc(block.blockId)}" value="or" autocomplete="off"${!isEvery ? ' checked' : ''}>
                <label class="seg-toggle-btn" for="bm-or-${esc(block.blockId)}">${esc(L.BlockToggleAny || 'any')}</label>
            </span>`;

        const conditions = block.conditions.map((c, i) => renderCondition(c, block, i)).join('');

        // Blocks always combine with AND ALSO; the joiner only shows between blocks (mockup g.showJoiner = gi > 0).
        const andAlso = index > 0
            ? `<div class="seg-joiner">
                   <span class="seg-joiner-line seg-joiner-line-short"></span>
                   <span class="seg-joiner-pill">${esc(L.AndAlso || 'AND ALSO')}</span>
                   <span class="seg-joiner-line"></span>
               </div>`
            : '';

        // Remove-block only when there is more than one block (mockup g.canDelete = groups.length > 1).
        const canDelete = blocks.length > 1;
        const removeBlockBtn = (isFrozen || !canDelete) ? '' : `<button type="button" class="seg-remove-block js-remove-block"
                    data-block="${esc(block.blockId)}" title="${esc(L.RemoveBlock || 'Remove block')}">${esc(L.RemoveBlock || 'Remove block')}</button>`;

        const addCond = isFrozen ? '' : `<button type="button" class="seg-add-cond js-add-condition" data-block="${esc(block.blockId)}">+ ${esc(L.AddCondition || 'Add condition')}</button>`;

        return `${andAlso}
        <div class="seg-block">
            <div class="seg-block-head">
                <span class="seg-block-head-text">${esc(L.BlockHeadLead || 'Include people where')}</span>
                ${matchToggle}
                <span class="seg-block-head-text seg-block-head-grow">${esc(L.BlockHeadTail || 'condition below is true')}</span>
                ${removeBlockBtn}
            </div>
            <div class="seg-block-body">
                ${conditions}
                ${addCond}
            </div>
        </div>`;
    };

    const render = () => {
        if (!listEl) return;

        const hasBlocks = blocks.length > 0;
        emptyEl?.classList.toggle('d-none', hasBlocks);
        listEl.innerHTML = blocks.map((b, i) => renderBlock(b, i)).join('');

        if (isFrozen) {
            listEl.querySelectorAll('select, input, button').forEach(el => { el.disabled = true; });
        }

        refreshDerived();
        void hydrateControls();
        applyConditionCounts();
    };

    /** Fills the async option lists and upgrades the selects to Select2 after a render pass. */
    const hydrateControls = async () => {
        if (window.flatpickr) {
            listEl.querySelectorAll('.flatpickr-date').forEach(el => {
                if (!el._flatpickr) window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true });
            });
        }

        const selects = Array.from(listEl.querySelectorAll('.js-node-select'));
        for (const el of selects) {
            const condition = findCondition(el.dataset.node);
            if (!condition) continue;
            const spec = controlFor(condition);

            let options = spec.options || [];
            if (spec.async) {
                options = await spec.async();
            } else if (spec.control === 'cascade-select') {
                const modelId = el.previousElementSibling?.value || '';
                options = modelId ? await loadEntityOptions('territory-node', modelId) : [];
            }

            const chosen = spec.multi ? (condition.values || []) : [(condition.values || [])[Number(el.dataset.index) || 0] ?? ''];
            const known = new Set(options.map(o => String(o.value)));
            const head = spec.multi ? '' : `<option value="">${esc(L.SelectOption || '')}</option>`;
            const extras = chosen.filter(v => String(v ?? '').trim() !== '' && !known.has(String(v)))
                .map(v => `<option value="${esc(v)}" selected>${esc(v)}</option>`).join('');
            el.innerHTML = head + extras + options.map(o =>
                `<option value="${esc(o.value)}"${chosen.includes(String(o.value)) ? ' selected' : ''}>${esc(o.text)}</option>`).join('');

            // SEG-E: seçili picker GUID'lerinin etiketi option listesinden çözülür (edit restore dahil), reads-as
            // GUID yerine isim göstersin. valueLabels salt UI-state — buildNodes payload'ına GİRMEZ.
            condition.valueLabels = condition.valueLabels || {};
            options.forEach(o => { if (chosen.includes(String(o.value))) condition.valueLabels[String(o.value)] = o.text; });
            const readbackSpan = el.closest('.seg-cond')?.querySelector('.seg-readback');
            if (readbackSpan) readbackSpan.textContent = conditionText(condition);

            if (window.jQuery?.fn?.select2 && !isFrozen) {
                const $el = window.jQuery(el);
                if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
                $el.select2({
                    dropdownParent: window.jQuery(document.body),
                    dropdownCssClass: 'segment-criteria-dropdown',
                    width: '100%',
                    tags: el.dataset.taggable === '1',
                    placeholder: L.SelectOption || '',
                    allowClear: !el.multiple
                });
                $el.off('change.segment').on('change.segment', () => writeSelectValue(el));
            }
        }

        for (const el of Array.from(listEl.querySelectorAll('.js-node-cascade'))) {
            const models = await loadEntityOptions('territory-model');
            const current = el.value;
            el.innerHTML = `<option value="">${esc(L.SelectTerritoryModel || '')}</option>`
                + models.map(m => `<option value="${esc(m.value)}"${m.value === current ? ' selected' : ''}>${esc(m.text)}</option>`).join('');
        }

        // Catalog-list chips (reference-set / enum). The options are the SAME published values as before (loaded through
        // the proxy for a reference set, or the enum's own closed list); only the control is chips instead of a select.
        // A value the author free-typed but the list does not carry is still shown as a selected chip, so nothing is lost.
        for (const wrap of Array.from(listEl.querySelectorAll('.js-chips'))) {
            const condition = findCondition(wrap.dataset.node);
            if (!condition) continue;
            const spec = controlFor(condition);
            let options = spec.options || [];
            if (spec.async) { try { options = await spec.async(); } catch (e) { options = []; } }
            const values = (condition.values || []).map(v => String(v));
            const known = new Set(options.map(o => String(o.value)));
            const extras = values.filter(v => v.trim() !== '' && !known.has(v)).map(v => ({ value: v, text: v }));
            const chips = options.concat(extras)
                .map(o => chipHtml(condition.nodeId, o.value, o.text, values.includes(String(o.value)))).join('');
            const free = `<input type="text" class="seg-freetext js-value-freetext" data-node="${esc(condition.nodeId)}" placeholder="${esc(L.OrTypeValue || '')}"${isFrozen ? ' disabled' : ''} />`;
            wrap.innerHTML = chips + free;
            if (isFrozen) wrap.querySelectorAll('button').forEach(b => { b.disabled = true; });
        }

        // SEG-E: picker etiketleri asenkron çözüldükten sonra üst reads-as cümlesi de isimlerle tazelenir.
        updateReadback();
    };

    const writeSelectValue = el => {
        const condition = findCondition(el.dataset.node);
        if (!condition) return;
        if (el.multiple) {
            condition.values = Array.from(el.selectedOptions).map(o => o.value).filter(v => String(v).trim() !== '');
        } else {
            condition.values = condition.values || [];
            condition.values[Number(el.dataset.index) || 0] = el.value;
        }
        // SEG-E: seçilen option'ın etiketi UI-state olarak saklanır (value→label); reads-as GUID yerine isim
        // gösterir. Salt görünüm — buildNodes value=GUID gönderir, valueLabels payload'a girmez.
        condition.valueLabels = condition.valueLabels || {};
        Array.from(el.selectedOptions).forEach(o => {
            if (String(o.value).trim() !== '') condition.valueLabels[String(o.value)] = o.text;
        });
        const readbackSpan = el.closest('.seg-cond')?.querySelector('.seg-readback');
        if (readbackSpan) readbackSpan.textContent = conditionText(condition);
        refreshDerived();
    };

    // ---------------------------------------------------------------- read-back sentence

    const conditionText = condition => {
        const attr = attributeLabel(condition.attributeCode);
        const op = operatorLabel(condition.operator);
        const arity = arityOf(condition.operator);
        if (arity.max === 0) return `${attr} ${op}`;
        // SEG-E: entity-picker value'ları GUID; varsa saklı etiket (valueLabels) gösterilir, yoksa value.
        // Reference-set/enum'da value=isim ve valueLabels boş olduğundan davranış AYNEN korunur.
        const labels = condition.valueLabels || {};
        const values = (condition.values || [])
            .filter(v => String(v ?? '').trim() !== '')
            .map(v => labels[String(v)] ?? v);
        const valueText = values.length ? values.join(', ') : (L.AnyValuePlaceholder || '…');
        return `${attr} ${op} ${valueText}`;
    };

    const blockText = block => {
        const joiner = block.match === 'or' ? ` ${L.JoinerOr || 'or'} ` : ` ${L.JoinerAnd || 'and'} `;
        const parts = block.conditions.map(conditionText);
        return parts.length > 1 ? `(${parts.join(joiner)})` : (parts[0] || '');
    };

    const updateReadback = () => {
        if (!readbackEl || !readbackTextEl) return;
        if (blocks.length === 0 || currentSegmentType() === 'static') {
            readbackEl.classList.add('d-none');
            return;
        }
        const subjectNoun = currentSubjectType() === 'account'
            ? (L.SubjectNounAccount || 'Accounts')
            : (L.SubjectNounContact || 'Contacts');
        const sentence = blocks.map(blockText).filter(Boolean).join(` ${L.AndAlso || 'AND ALSO'} `);
        readbackTextEl.textContent = `${subjectNoun} ${L.ReadsWhere || 'where'} ${sentence}`;
        readbackEl.classList.remove('d-none');
    };

    // ---------------------------------------------------------------- live reach rail (SEG-C)

    const reachSection = document.getElementById('reachSection');
    const reachIdle = document.getElementById('reachIdle');
    const reachContent = document.getElementById('reachContent');
    const reachError = document.getElementById('reachError');
    const reachCap = document.getElementById('reachCap');
    const reachTotalEl = document.getElementById('reachTotal');
    const reachFunnel = document.getElementById('reachFunnel');
    const reachSample = document.getElementById('reachSample');
    const reachChecklistItems = document.getElementById('reachChecklistItems');
    let previewTimer = null;
    let previewSeq = 0;

    const predicateNodes = () => buildNodes().filter(n => n.nodeKind === 'predicate' && n.attributeCode && n.operator);

    /** A predicate is "ready" only when it carries enough values for its operator's arity and every required
     *  parameter — firing /preview on an incomplete predicate would just earn a 400 and a scary banner. */
    const predicateComplete = node => {
        const definition = attributeFor(node.attributeCode);
        if (!definition || !node.operator) return false;
        const arity = arityOf(node.operator);
        const values = (node.values || []).filter(v => String(v ?? '').trim() !== '');
        if (arity.max > 0 && values.length < arity.min) return false;
        return !(definition.requiredParameters || []).some(p => !String((node.parameters || {})[p] ?? '').trim());
    };

    const readyForPreview = () => {
        const preds = predicateNodes();
        return preds.length > 0 && preds.every(predicateComplete);
    };

    const schedulePreview = () => {
        if (!reachSection || currentSegmentType() === 'static') return;
        if (previewTimer) clearTimeout(previewTimer);
        previewTimer = setTimeout(runPreview, 500);
    };

    const showReachState = state => {
        reachIdle?.classList.toggle('d-none', state !== 'idle');
        reachContent?.classList.toggle('d-none', state !== 'ready');
        reachError?.classList.toggle('d-none', state !== 'error' && state !== 'cap');
    };

    const runPreview = async () => {
        if (!reachSection || currentSegmentType() === 'static') return;
        const nodes = buildNodes();
        if (!readyForPreview()) {
            showReachState('idle');
            lastConditionCounts = {};
            applyConditionCounts();
            updateChecklist(null);
            return;
        }

        const seq = ++previewSeq;
        const body = { subjectType: currentSubjectType(), matchMode: 'all', criteria: nodes, effectiveAt: null };
        try {
            const response = await fetch(`${endpoint}/preview`, {
                method: 'POST', credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            const payload = await response.json().catch(() => ({}));
            if (seq !== previewSeq) return; // a newer edit already fired; drop this stale answer

            if (response.status === 422) {
                showReachState('cap');
                if (reachError) {
                    reachError.classList.remove('d-none');
                    reachError.textContent = L.ReachTooWide || (payload.errors || []).join(' · ') || 'Too wide';
                }
                return;
            }
            if (!response.ok) {
                showReachState('error');
                if (reachError) reachError.textContent = (payload.errors || [L.ErrorState]).join(' · ');
                return;
            }
            renderReach(payload.data || {});
        } catch (e) {
            if (seq !== previewSeq) return;
            showReachState('error');
            if (reachError) reachError.textContent = L.ErrorState || 'Error';
        }
    };

    const renderReach = data => {
        showReachState('ready');
        const counts = data.conditionCounts || [];
        lastConditionCounts = {};
        counts.forEach(c => { lastConditionCounts[c.nodeId] = { count: c.count, capExceeded: c.capExceeded }; });

        if (reachTotalEl) reachTotalEl.textContent = String(data.totalCount ?? 0);
        reachCap?.classList.add('d-none');

        // Funnel: each condition counted alone, longest bar = the widest single condition.
        const max = Math.max(1, ...counts.map(c => c.count || 0));
        if (reachFunnel) {
            reachFunnel.innerHTML = counts.map(c => {
                const pct = Math.round(((c.count || 0) / max) * 100);
                const label = attributeLabel(c.attributeCode);
                const value = c.capExceeded ? `${c.count}+` : String(c.count ?? 0);
                return `<div>
                    <div class="seg-funnel-row-head">
                        <span class="seg-funnel-label">${esc(label)}</span>
                        <span class="seg-funnel-value">${esc(value)} ${esc(L.MatchThisAlone || 'match this alone')}</span>
                    </div>
                    <div class="seg-funnel-track"><div class="seg-funnel-fill" style="width:${pct}%"></div></div>
                </div>`;
            }).join('');
        }

        if (reachSample) {
            const members = data.sampleMembers || [];
            reachSample.innerHTML = members.length
                ? members.map(m => {
                    const name = m.displayName || m.subjectId || '';
                    const initials = String(name).trim().split(/\s+/).map(w => w.charAt(0)).slice(0, 2).join('').toUpperCase() || '?';
                    const meta = m.subjectSecondaryLabel || '';
                    return `<div class="seg-sample">
                       <span class="seg-avatar">${esc(initials)}</span>
                       <span class="seg-sample-body">
                           <span class="seg-sample-name">${esc(name)}</span>
                           ${meta ? `<span class="seg-sample-meta">${esc(meta)}</span>` : ''}
                       </span>
                   </div>`;
                }).join('')
                : `<div class="seg-reach-idle">${esc(L.ReachNoSample || '—')}</div>`;
        }

        applyConditionCounts();
        updateChecklist(data);
    };

    /** Patches each condition's "N match this alone" line without a full re-render (keeps Select2 focus intact). */
    const applyConditionCounts = () => {
        listEl?.querySelectorAll('.js-cond-count-row').forEach(row => {
            const info = lastConditionCounts[row.dataset.node];
            if (!info) { row.textContent = ''; row.classList.add('seg-hidden'); return; }
            const value = info.capExceeded ? `${info.count}+` : String(info.count);
            row.textContent = `${value} ${L.MatchThisAlone || 'match this alone'}`;
            row.classList.remove('seg-hidden');
        });
    };

    const updateChecklist = data => {
        if (!reachChecklistItems) return;
        const nameEl = document.getElementById('SegmentName');
        const effFromEl = document.getElementById('EffectiveFrom');
        const items = [
            { ok: !!(nameEl?.value || '').trim(), text: L.ChecklistName || 'Name set' },
            { ok: predicateNodes().length > 0, text: L.ChecklistRule || 'At least one condition' },
            { ok: (data?.totalCount ?? 0) > 0, text: L.ChecklistReach || 'Reaches at least one member' },
            { ok: !!(effFromEl?.value || '').trim(), text: L.ChecklistEffective || 'Effective-from set' }
        ];
        reachChecklistItems.innerHTML = items.map(i =>
            `<li class="seg-check ${i.ok ? 'is-ok' : 'is-pending'}">
                <span class="seg-check-mark">${i.ok ? '✓' : ''}</span>
                <span class="seg-check-text">${esc(i.text)}</span>
            </li>`).join('');
    };

    // ---------------------------------------------------------------- empty-state templates

    // A template is a STARTING POINT: it references attribute codes the catalog must declare for the current subject
    // type (checked below), and it leaves the values for the author to pick from the catalog. It never hardcodes a
    // value. A template whose attributes are not applicable is simply not offered.
    const TEMPLATES = [
        { key: 'tplSpecialty', labelKey: 'TplSpecialty', specs: [{ attributeCode: 'contact.specialty', operator: 'in' }] },
        { key: 'tplAccountType', labelKey: 'TplAccountType', specs: [{ attributeCode: 'account.type', operator: 'in' }] },
        { key: 'tplConsentReachable', labelKey: 'TplConsentReachable', specs: [{ attributeCode: 'consent.eligibility', operator: 'eq' }] },
        { key: 'tplHasCoverage', labelKey: 'TplHasCoverage', specs: [{ attributeCode: 'territory.has-coverage', operator: 'eq', values: ['true'] }] }
    ];

    const templateApplies = tpl => {
        const applicable = new Set(applicableAttributes().map(a => a.attributeCode));
        return tpl.specs.every(s => applicable.has(s.attributeCode));
    };

    const renderTemplates = () => {
        if (!templatesEl) return;
        const available = TEMPLATES.filter(templateApplies);
        templatesEl.innerHTML = available.map(tpl =>
            `<button type="button" class="seg-recipe js-template" data-template="${esc(tpl.key)}">${esc(L[tpl.labelKey] || tpl.key)}</button>`).join('');
        templatesEl.classList.toggle('d-none', available.length === 0);
    };

    const applyTemplate = key => {
        if (isFrozen) return;
        const tpl = TEMPLATES.find(t => t.key === key);
        if (!tpl || !templateApplies(tpl)) return;
        const block = { blockId: uid(), match: 'and', conditions: [] };
        tpl.specs.forEach(spec => {
            const condition = { nodeId: uid(), attributeCode: null, operator: null, values: [], valueType: null, parameters: {} };
            applyAttribute(condition, spec.attributeCode);
            if (spec.operator && attributeFor(spec.attributeCode)?.operators.includes(spec.operator)) {
                condition.operator = spec.operator;
            }
            if (spec.values) condition.values = spec.values.slice();
            block.conditions.push(condition);
        });
        if (block.conditions.length) { blocks.push(block); render(); }
    };

    // ---------------------------------------------------------------- one place that fans a model change out

    const refreshDerived = () => {
        syncHidden();
        updateReadback();
        schedulePreview();
    };

    // ---------------------------------------------------------------- events

    document.addEventListener('change', event => {
        const attribute = event.target.closest('.js-node-attribute');
        if (attribute) {
            const condition = findCondition(attribute.dataset.node);
            if (condition) { applyAttribute(condition, attribute.value); render(); }
            return;
        }

        const operator = event.target.closest('.js-node-operator');
        if (operator) {
            const condition = findCondition(operator.dataset.node);
            if (condition) { condition.operator = operator.value; condition.values = []; render(); }
            return;
        }

        const blockMatch = event.target.closest('.js-block-match');
        if (blockMatch) {
            const block = findBlock(blockMatch.dataset.block);
            if (block) { block.match = blockMatch.value; refreshDerived(); }
            return;
        }

        const cascade = event.target.closest('.js-node-cascade');
        if (cascade) { void hydrateControls(); return; }

        const select = event.target.closest('.js-node-select');
        if (select) { writeSelectValue(select); return; }

        const value = event.target.closest('.js-node-value');
        if (value) {
            const condition = findCondition(value.dataset.node);
            if (condition) {
                condition.values = condition.values || [];
                condition.values[Number(value.dataset.index) || 0] = value.value;
                refreshDerived();
            }
            return;
        }

        const multiText = event.target.closest('.js-node-multitext');
        if (multiText) {
            const condition = findCondition(multiText.dataset.node);
            if (condition) {
                condition.values = multiText.value.split(',').map(v => v.trim()).filter(Boolean);
                refreshDerived();
            }
            return;
        }

        const parameter = event.target.closest('.js-node-param');
        if (parameter) {
            const condition = findCondition(parameter.dataset.node);
            if (condition) { condition.parameters[parameter.dataset.param] = parameter.value; refreshDerived(); }
            return;
        }

        // The applicable attribute set depends on the subject type; the reach depends on both, and on the name/date.
        // Subject is a pill toggle and segment type a radio-card group, so react to a change on either radio group.
        if (event.target.name === 'SubjectType' || event.target.name === 'SegmentType') {
            editor.dataset.subjectType = currentSubjectType();
            applySegmentTypeVisibility();
            normalizeForSubject();
            renderTemplates();
            refreshMemberAvailability();
            render();
        }
    });

    document.addEventListener('input', event => {
        const value = event.target.closest('.js-node-value');
        if (value && value.type === 'text') {
            const condition = findCondition(value.dataset.node);
            if (condition) {
                condition.values = condition.values || [];
                condition.values[Number(value.dataset.index) || 0] = value.value;
                refreshDerived();
            }
            return;
        }
        // A name / effective-from edit changes the activation checklist (but not the reach itself).
        if (event.target.id === 'SegmentName' || event.target.id === 'EffectiveFrom') {
            updateChecklist(null);
        }
    });

    // Free-text entry on a catalog-list chip field: Enter adds the typed value (mirrors the mockup's "type + Enter"),
    // exactly as Select2 tagging did — the value source stays a hint, never a hard restriction.
    document.addEventListener('keydown', event => {
        const free = event.target.closest && event.target.closest('.js-value-freetext');
        if (!free || event.key !== 'Enter' || isFrozen) return;
        event.preventDefault();
        const condition = findCondition(free.dataset.node);
        if (!condition) return;
        const v = (free.value || '').trim();
        if (!v) return;
        condition.values = condition.values || [];
        if (isMultiValue(condition.operator)) {
            if (!condition.values.map(String).includes(v)) condition.values.push(v);
        } else {
            condition.values = [v];
        }
        free.value = '';
        render();
    });

    // WP-SEG-H: free-text on an enum PARAMETER chip field: Enter sets the typed value (single-select, mirrors the main
    // value's "type + Enter"). Written to condition.parameters[name], so the source stays a hint and the payload shape
    // is unchanged.
    document.addEventListener('keydown', event => {
        const free = event.target.closest && event.target.closest('.js-param-freetext');
        if (!free || event.key !== 'Enter' || isFrozen) return;
        event.preventDefault();
        const condition = findCondition(free.dataset.node);
        if (!condition) return;
        const v = (free.value || '').trim();
        if (!v) return;
        condition.parameters = condition.parameters || {};
        condition.parameters[free.dataset.param] = v;
        free.value = '';
        render();
    });

    document.addEventListener('click', event => {
        if (event.target.closest('#btnAddBlock')) { event.preventDefault(); if (addBlock()) render(); return; }

        // A value chip toggles a catalog value in condition.values. Multi-value operators (in / not-in) toggle
        // membership; single-value operators (eq, bool…) replace. Either way the write path is the same as the old
        // Select2, so buildNodes / the posted payload are unchanged.
        const valueChip = event.target.closest('.js-value-chip');
        if (valueChip) {
            event.preventDefault();
            if (isFrozen) return;
            const condition = findCondition(valueChip.dataset.node);
            if (condition) {
                const v = valueChip.dataset.value;
                condition.values = condition.values || [];
                if (isMultiValue(condition.operator)) {
                    const at = condition.values.map(String).indexOf(String(v));
                    if (at >= 0) condition.values.splice(at, 1); else condition.values.push(v);
                } else {
                    condition.values = [v];
                }
                render();
            }
            return;
        }

        // WP-SEG-H: a parameter chip is SINGLE-SELECT. Clicking a chip writes its value to condition.parameters[name];
        // clicking the already-selected chip clears it. The write path is condition.parameters[name] exactly as the old
        // select's change handler, so buildNodes / the posted payload are unchanged.
        const paramChip = event.target.closest('.js-param-chip');
        if (paramChip) {
            event.preventDefault();
            if (isFrozen) return;
            const condition = findCondition(paramChip.dataset.node);
            if (condition) {
                const name = paramChip.dataset.param;
                const v = paramChip.dataset.value;
                condition.parameters = condition.parameters || {};
                condition.parameters[name] = String(condition.parameters[name] ?? '') === String(v) ? '' : v;
                render();
            }
            return;
        }

        // "Preview sample of 50" re-runs the same SEG-C /preview the rail already debounces — it fabricates nothing.
        if (event.target.closest('#btnPreviewSample')) { event.preventDefault(); runPreview(); return; }

        const addCond = event.target.closest('.js-add-condition');
        if (addCond) { event.preventDefault(); if (addCondition(addCond.dataset.block)) render(); return; }

        const template = event.target.closest('.js-template');
        if (template) { event.preventDefault(); applyTemplate(template.dataset.template); return; }

        const removeCond = event.target.closest('.js-remove-condition');
        if (removeCond) { event.preventDefault(); removeCondition(removeCond.dataset.block, removeCond.dataset.node); render(); return; }

        const removeBlk = event.target.closest('.js-remove-block');
        if (removeBlk) { event.preventDefault(); removeBlock(removeBlk.dataset.block); render(); return; }

        const newVersion = event.target.closest('#btnNewVersionFromForm');
        if (newVersion) {
            event.preventDefault();
            fetch(`${endpoint}/segments/${newVersion.dataset.id}/new-version`, { method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' } })
                .then(envelope)
                .then(id => { if (id) window.location.href = `/CRM/Segments/Edit/${id}`; })
                .catch(error => window.showToast?.(error.message || L.ErrorState, 'error'));
        }
    });

    // ---------------------------------------------------------------- manual membership sub-editor

    const memberEditor = document.getElementById('memberEditor');
    const memberListEl = document.getElementById('memberList');
    const memberEmptyEl = document.getElementById('memberEmpty');
    let members = [];

    const refreshMemberAvailability = () => {
        if (!memberEditor) return;
        const addBtn = document.getElementById('btnAddMember');
        if (addBtn) addBtn.disabled = currentSegmentType() === 'dynamic';
    };

    const renderMembers = () => {
        if (!memberListEl) return;
        memberEmptyEl?.classList.toggle('d-none', members.length > 0);
        memberListEl.innerHTML = members.map(m => `
            <div class="seg-member ${m.isArchived ? 'is-archived' : ''}">
                <div>
                    <div class="d-flex align-items-center gap-2 mb-1">
                        <span class="seg-member-badge ${m.membershipMode === 'manual-include' ? 'is-include' : 'is-exclude'}">${esc(m.membershipMode)}</span>
                        <span class="seg-member-name">${esc(m.subjectDisplayName || m.subjectId)}</span>
                        ${m.isArchived ? `<span class="seg-member-badge">${esc(L.Archived || 'archived')}</span>` : ''}
                    </div>
                    <div class="seg-member-meta">${esc(m.selectionReason)}</div>
                    ${(m.subjectSecondaryLabel || '') ? `<div class="seg-member-meta">${esc(m.subjectSecondaryLabel)}</div>` : ''}
                </div>
                ${m.isArchived ? '' : `
                <div class="seg-member-actions">
                    <button type="button" class="seg-icon-btn js-edit-member" data-id="${esc(m.targetCustomerId)}"><i class="bx bx-edit"></i></button>
                    <button type="button" class="seg-icon-btn js-archive-member" data-id="${esc(m.targetCustomerId)}"><i class="bx bx-archive-in"></i></button>
                </div>`}
            </div>`).join('');
    };

    const loadMembers = async () => {
        if (!memberEditor || !segmentId) return;
        try {
            const data = await getJson(`/segments/${segmentId}/targets?includeArchived=true`);
            members = data?.items || [];
            renderMembers();
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
        }
    };

    const memberCanvas = () => window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('memberCanvas'));

    const setupSubjectPicker = (member) => {
        const block = document.getElementById('memberPickerBlock');
        const picker = document.getElementById('memberSubjectPicker');
        const label = document.getElementById('memberPickerLabel');
        const hint = document.getElementById('memberPickerHint');
        if (!block || !picker) return;

        const isContact = currentSubjectType() === 'contact';
        const entityKind = isContact ? 'contact' : 'account';

        if (!pickerAvailable(entityKind)) {
            block.classList.add('d-none');
            return;
        }

        block.classList.remove('d-none');
        if (label) label.textContent = isContact ? (L.SelectContact || '') : (L.SelectAccount || '');
        if (hint) hint.textContent = L.SubjectPickerHelp || '';

        const $picker = window.jQuery ? window.jQuery(picker) : null;
        if ($picker?.hasClass('select2-hidden-accessible')) {
            $picker.select2('destroy');
        }
        picker.innerHTML = '';

        if (member) {
            picker.disabled = true;
            return;
        }
        picker.disabled = false;

        if (!$picker || !window.jQuery.fn.select2) {
            return;
        }

        $picker.select2({
            dropdownParent: window.jQuery('#memberCanvas'),
            width: '100%',
            placeholder: isContact ? (L.SelectContact || '') : (L.SelectAccount || ''),
            allowClear: true,
            minimumInputLength: 0,
            ajax: {
                url: `${endpoint}/${entityKind}s`,
                dataType: 'json',
                delay: 250,
                data: params => ({ search: params.term || '', page: params.page || 1, pageSize: 25 }),
                processResults: (payload, params) => {
                    const data = payload?.data || {};
                    const items = data.items || [];
                    const page = params.page || 1;
                    const pageSize = data.pageSize || 25;
                    return {
                        results: items.map(item => ({
                            id: item.id,
                            text: isContact
                                ? (item.displayName
                                    || [item.firstName, item.lastName].filter(Boolean).join(' ')
                                    || item.id)
                                : (item.accountName || item.accountCode || item.id)
                        })),
                        pagination: { more: page * pageSize < (data.total || 0) }
                    };
                }
            }
        });

        $picker.off('select2:select.segment').on('select2:select.segment', event => {
            const chosen = event.params?.data;
            if (!chosen) return;
            document.getElementById('memberSubjectId').value = chosen.id || '';
            document.getElementById('memberDisplayName').value = chosen.text || '';
        });
    };

    const openMember = (member) => {
        document.getElementById('memberCanvasError')?.classList.add('d-none');
        document.getElementById('memberEditId').value = member?.targetCustomerId || '';
        document.getElementById('memberSubjectId').value = member?.subjectId || '';
        document.getElementById('memberSubjectId').disabled = !!member;
        document.getElementById('memberDisplayName').value = member?.subjectDisplayName || '';
        document.getElementById('memberReason').value = member?.selectionReason || '';
        document.getElementById('memberNotes').value = member?.notes || '';
        document.getElementById('memberEffectiveFrom').value = (member?.effectiveFrom || new Date().toISOString()).slice(0, 10);
        document.getElementById('memberEffectiveTo').value = member?.effectiveTo ? String(member.effectiveTo).slice(0, 10) : '';

        const modeEl = document.getElementById('memberMode');
        modeEl.innerHTML = (contract?.vocabularies?.membershipModes || []).map(m =>
            `<option value="${esc(m)}"${member?.membershipMode === m ? ' selected' : ''}>${esc(m)}</option>`).join('');

        setupSubjectPicker(member);
        memberCanvas()?.show();
    };

    const saveMember = async () => {
        const errorEl = document.getElementById('memberCanvasError');
        const id = document.getElementById('memberEditId').value;
        const mode = document.getElementById('memberMode').value;
        const payload = {
            subjectType: currentSubjectType(),
            subjectId: document.getElementById('memberSubjectId').value.trim(),
            membershipMode: mode,
            selectionReason: document.getElementById('memberReason').value.trim(),
            reasonCodes: [mode === 'manual-include' ? 'manual_include' : 'manual_exclude'],
            effectiveFrom: document.getElementById('memberEffectiveFrom').value,
            effectiveTo: document.getElementById('memberEffectiveTo').value || null,
            subjectDisplayName: document.getElementById('memberDisplayName').value.trim() || null,
            notes: document.getElementById('memberNotes').value.trim() || null
        };

        try {
            const url = id ? `${endpoint}/segments/${segmentId}/targets/${id}` : `${endpoint}/segments/${segmentId}/targets`;
            const response = await fetch(url, {
                method: id ? 'PUT' : 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            await envelope(response);
            memberCanvas()?.hide();
            window.showToast?.(id ? (L.RecordUpdated || '') : (L.RecordCreated || ''), 'success');
            await loadMembers();
        } catch (error) {
            if (errorEl) { errorEl.textContent = error.message || L.ErrorState; errorEl.classList.remove('d-none'); }
        }
    };

    document.addEventListener('click', event => {
        if (event.target.closest('#btnAddMember')) { event.preventDefault(); openMember(null); return; }
        if (event.target.closest('#memberSaveBtn')) { event.preventDefault(); void saveMember(); return; }

        const edit = event.target.closest('.js-edit-member');
        if (edit) {
            event.preventDefault();
            openMember(members.find(m => m.targetCustomerId === edit.dataset.id));
            return;
        }

        const archive = event.target.closest('.js-archive-member');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.AreYouSure, async () => {
            try {
                await fetch(`${endpoint}/segments/${segmentId}/targets/${archive.dataset.id}/archive`, {
                    method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
                }).then(r => r.ok ? null : envelope(r));
                window.showToast?.(L.RecordArchived || '', 'success');
                await loadMembers();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { type: 'warning' });
    });

    // ---------------------------------------------------------------- load: stored tree -> blocks

    const toCondition = node => ({
        nodeId: node.nodeId || uid(),
        attributeCode: node.attributeCode || null,
        operator: node.operator || null,
        values: node.values || [],
        valueType: node.valueType || null,
        parameters: node.parameters || {}
    });

    const nodesToBlocks = loaded => {
        const bySort = arr => arr.slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
        const result = [];
        const placed = new Set();

        // A legacy tree could carry root-level predicates or nesting deeper than two; the block model is exactly two
        // layers, so those are folded flat rather than lost. Fresh segments authored here are always well-formed.
        const rootGroups = bySort(loaded.filter(n => n.nodeKind === 'group' && !n.parentNodeId));
        rootGroups.forEach(group => {
            const conds = bySort(loaded.filter(n => n.nodeKind === 'predicate' && n.parentNodeId === group.nodeId)).map(toCondition);
            conds.forEach(c => placed.add(c.nodeId));
            result.push({
                blockId: group.nodeId || uid(),
                match: group.groupOperator === 'or' ? 'or' : 'and',
                conditions: conds.length ? conds : [makeCondition()]
            });
        });

        const rootPreds = bySort(loaded.filter(n => n.nodeKind === 'predicate' && !n.parentNodeId)).map(toCondition);
        if (rootPreds.length) {
            rootPreds.forEach(c => placed.add(c.nodeId));
            result.unshift({ blockId: uid(), match: 'and', conditions: rootPreds });
        }

        const leftover = bySort(loaded.filter(n => n.nodeKind === 'predicate' && !placed.has(n.nodeId))).map(toCondition);
        if (leftover.length) {
            if (result.length) result[result.length - 1].conditions.push(...leftover);
            else result.push({ blockId: uid(), match: 'and', conditions: leftover });
        }

        return result;
    };

    // ---------------------------------------------------------------- bootstrap

    const init = async () => {
        applySegmentTypeVisibility();
        if (isFrozen) { const b = document.getElementById('btnAddBlock'); if (b) b.disabled = true; }

        try {
            [catalog, contract] = await Promise.all([getJson('/attribute-catalog'), getJson('/contract')]);
        } catch (error) {
            window.showToast?.(error.message || L.SegmentContractUnavailable, 'error');
            return;
        }

        let loaded = [];
        try { loaded = JSON.parse(hidden.value || '[]') || []; }
        catch (error) { loaded = []; }
        blocks = nodesToBlocks(loaded);

        renderTemplates();
        render();
        refreshMemberAvailability();
        await loadMembers();

        if (window.flatpickr) {
            document.querySelectorAll('.flatpickr-date').forEach(el => {
                if (!el._flatpickr) window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true });
            });
        }
    };

    void init();
})(window, document);
