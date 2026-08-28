/**
 * MOD-0167-FU02 Segments — Create/Edit page: the EMBEDDED criteria tree editor and the EMBEDDED manual membership
 * sub-editor. Both live inside this one Compact page; neither is a second golden-reference surface.
 *
 * The editor is CATALOG-DRIVEN end to end. The attribute list, the operators allowed for each attribute, the value
 * type, the value arity, the required/optional parameters AND (P1a) where a legitimate value comes from all arrive
 * from /attribute-catalog at runtime. There is no hardcoded attribute, operator or value list in this file, on
 * purpose — a hardcoded copy is a second source of truth and it drifts silently until the UI offers a rule the
 * runtime refuses.
 *
 * P1a — the value control follows the attribute's declared value source:
 *   reference-set  -> Select2 over the tenant's PUBLISHED MOD-0048 values (empty when unpublished, never a local list)
 *   enum           -> the closed value list the catalog itself carries
 *   entity-picker  -> the aggregate's existing selector (account / territory model+node / MDM product / brand)
 *   free-text      -> by value type: date picker, number input, bool toggle, plain text
 * Every one of them still ACCEPTS A TYPED VALUE (Select2 tags), because the value source is a hint about what is
 * offered, never a restriction on what the runtime takes. `in` / `not-in` render as ONE multi-select that fills the
 * SAME values[] array — the persisted predicate shape is byte-identical to before.
 *
 * P1b — the tree is drawn as real nesting: each group is a bordered rail with its own operator badge and its own
 * "add inside" buttons, so the parent of a new node is decided by WHERE you click. The parent dropdown survives only
 * as a move-fallback. A new group is born with one predicate inside it, so an empty group cannot happen by accident.
 *
 * All traffic goes through the same-origin MVC proxy. The browser never builds a bearer token or touches a cookie.
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
    const maxDepth = parseInt(editor.dataset.maxDepth || '5', 10);

    const listEl = document.getElementById('criteriaList');
    const emptyEl = document.getElementById('criteriaEmpty');
    const subjectTypeEl = document.getElementById('SubjectType');
    const segmentTypeEl = document.getElementById('SegmentType');

    let bootstrap = {};
    try { bootstrap = JSON.parse(document.getElementById('segmentFormBootstrap')?.textContent || '{}'); }
    catch (e) { bootstrap = {}; }
    const availablePickers = new Set(bootstrap.availablePickers || []);

    let catalog = null;
    let contract = null;
    let nodes = [];

    // One cache per option source, so re-rendering the tree never re-fetches a list.
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

    const currentSubjectType = () => (subjectTypeEl?.value || editor.dataset.subjectType || 'contact').trim();
    const currentSegmentType = () => (segmentTypeEl?.value || 'dynamic').trim();

    /**
     * Shows only the sections the chosen segment type can actually use, mirroring what the runtime already enforces:
     *   static  -> membership IS the manual list, so the criteria tree is not just empty but forbidden
     *   dynamic -> the rule decides everything, so a manual row is refused with a 400
     *   hybrid  -> both, which is the entire point of hybrid
     * Hiding beats disabling here: an author never has to wonder why a section they can see does nothing.
     */
    const applySegmentTypeVisibility = () => {
        const type = currentSegmentType();
        document.getElementById('criteriaSection')?.classList.toggle('d-none', type === 'static');
        document.getElementById('manualMembershipSection')?.classList.toggle('d-none', type === 'dynamic');
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
                    text: x.name || x.text || x.accountName || x.modelName || x.territoryName || x.nodeName
                        || x.globalProductName || x.productName || x.brandName || x.code || x.id
                }))
                .filter(x => x.value);
        } catch (e) {
            options = [];
        }
        optionCache.set(key, options);
        return options;
    };

    const pickerAvailable = entityKind => availablePickers.has(entityKind);

    // ---------------------------------------------------------------- criteria model

    const childrenOf = parentId => nodes
        .filter(n => (n.parentNodeId || '') === (parentId || ''))
        .sort((a, b) => a.sortOrder - b.sortOrder);

    const depthOf = node => {
        let depth = 1;
        let cursor = node.parentNodeId;
        let guard = 0;
        while (cursor && guard++ < 16) {
            depth++;
            cursor = (nodes.find(n => n.nodeId === cursor) || {}).parentNodeId;
        }
        return depth;
    };

    const syncHidden = () => {
        // A static segment must POST an EMPTY tree: the runtime refuses one that carries criteria, and hiding the
        // section does not stop a hidden input from submitting. The in-memory tree is left intact, so switching back
        // to hybrid restores the rule instead of silently destroying the author's work.
        if (currentSegmentType() === 'static') {
            hidden.value = '[]';
            return;
        }

        // Exactly the shape the runtime persists. The value source changed how a value is CHOSEN, never how it is stored.
        hidden.value = JSON.stringify(nodes.map(n => ({
            nodeId: n.nodeId,
            parentNodeId: n.parentNodeId || null,
            nodeKind: n.nodeKind,
            groupOperator: n.nodeKind === 'group' ? (n.groupOperator || null) : null,
            attributeCode: n.nodeKind === 'predicate' ? (n.attributeCode || null) : null,
            operator: n.nodeKind === 'predicate' ? (n.operator || null) : null,
            values: n.nodeKind === 'predicate' ? (n.values || []).filter(v => String(v ?? '').trim() !== '') : [],
            valueType: n.nodeKind === 'predicate' ? (n.valueType || null) : null,
            parameters: n.parameters || {},
            negate: !!n.negate,
            sortOrder: n.sortOrder,
            label: n.label || null
        })));
    };

    const nextSortOrder = parentId => {
        const siblings = childrenOf(parentId);
        return siblings.length === 0 ? 0 : Math.max(...siblings.map(s => s.sortOrder)) + 1;
    };

    const makeNode = (kind, parentId) => ({
        nodeId: uid(),
        parentNodeId: parentId || null,
        nodeKind: kind,
        groupOperator: kind === 'group' ? (contract?.vocabularies?.groupOperators || ['and'])[0] : null,
        attributeCode: null,
        operator: null,
        values: [],
        valueType: null,
        parameters: {},
        negate: false,
        sortOrder: nextSortOrder(parentId),
        label: null
    });

    /** Re-shapes a predicate around a newly chosen attribute: operator, value type and parameter set all come from
     *  the catalog, so an attribute change can never leave an operator the runtime would reject. */
    const applyAttribute = (node, attributeCode) => {
        const definition = attributeFor(attributeCode);
        if (!definition) return;
        node.attributeCode = definition.attributeCode;
        node.valueType = definition.valueType;
        node.operator = definition.operators.includes(node.operator) ? node.operator : definition.operators[0];
        node.values = [];
        const kept = {};
        (definition.requiredParameters || []).concat(definition.optionalParameters || []).forEach(p => {
            kept[p] = (node.parameters || {})[p] || '';
        });
        node.parameters = kept;
    };

    const addNode = (kind, parentId) => {
        if (isFrozen) return null;
        if (nodes.length >= maxNodes) {
            window.showToast?.(`${L.LimitReached || 'Limit'} (${maxNodes})`, 'error');
            return null;
        }
        if (parentId && childrenOf(parentId).length >= maxChildren) {
            window.showToast?.(`${L.LimitReached || 'Limit'} (${maxChildren})`, 'error');
            return null;
        }

        const node = makeNode(kind, parentId);
        if (depthOf(node) > maxDepth) {
            window.showToast?.(`${L.LimitReached || 'Limit'} (${maxDepth})`, 'error');
            return null;
        }

        if (kind === 'predicate') {
            const first = applicableAttributes()[0];
            if (!first) {
                window.showToast?.(L.SegmentContractUnavailable || 'No attribute available', 'error');
                return null;
            }
            applyAttribute(node, first.attributeCode);
        }

        nodes.push(node);

        // A group is born with one predicate inside it: an empty group is a 400 nobody meant to author.
        if (kind === 'group' && depthOf(node) < maxDepth) {
            const child = makeNode('predicate', node.nodeId);
            const first = applicableAttributes()[0];
            if (first) {
                applyAttribute(child, first.attributeCode);
                nodes.push(child);
            }
        }

        return node;
    };

    const removeNode = nodeId => {
        // Removing a group takes its subtree with it: an orphaned child would be a parent reference the runtime rejects.
        const doomed = new Set([nodeId]);
        let grew = true;
        while (grew) {
            grew = false;
            nodes.forEach(n => {
                if (n.parentNodeId && doomed.has(n.parentNodeId) && !doomed.has(n.nodeId)) {
                    doomed.add(n.nodeId);
                    grew = true;
                }
            });
        }
        nodes = nodes.filter(n => !doomed.has(n.nodeId));
    };

    // ---------------------------------------------------------------- value controls (P1a)

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

    const isMultiValue = operator => operator === 'in' || operator === 'not-in';

    /**
     * Describes the control a predicate needs, without touching the DOM. Kept separate so the rendering stays dumb
     * and the DECISION (which is the interesting part) is readable in one place.
     */
    const controlFor = node => {
        const definition = attributeFor(node.attributeCode);
        const source = definition?.valueSource || { kind: 'free-text' };
        const multi = isMultiValue(node.operator);

        if (source.kind === 'reference-set') {
            return { control: 'select', multi, async: () => loadReferenceValues(source.referenceSetCode), taggable: true };
        }
        if (source.kind === 'enum') {
            return { control: 'select', multi, options: (source.allowedValues || []).map(v => ({ value: v, text: v })) };
        }
        if (source.kind === 'entity-picker') {
            if (!pickerAvailable(source.entityKind)) {
                // No permission to browse that master: a plain id field plus the reason, never an always-empty list.
                return { control: 'text', multi: false, disabledReason: L.PickerUnavailable };
            }
            if (source.entityKind === 'territory-node') {
                return { control: 'cascade-select', multi, entityKind: 'territory-node' };
            }
            return { control: 'select', multi, async: () => loadEntityOptions(source.entityKind), taggable: true };
        }

        // free-text: the value TYPE is the whole instruction.
        switch (node.valueType) {
            case 'date': return { control: 'date', multi: false };
            case 'number': return { control: 'number', multi: false };
            case 'bool': return { control: 'bool', multi: false };
            default: return { control: 'text', multi };
        }
    };

    const valueSlotCount = node => {
        const arity = arityOf(node.operator);
        if (arity.max === 0) return 0;
        if (isMultiValue(node.operator)) return 1;      // one multi-select control
        return arity.min === 2 ? 2 : 1;                  // between = two bounds
    };

    const renderValueControl = (node, index, spec) => {
        const values = node.values || [];
        const single = values[index] ?? '';

        if (spec.control === 'bool') {
            return `<select class="form-select form-select-sm js-node-value" data-node="${esc(node.nodeId)}" data-index="${index}">
                        <option value="true"${single === 'true' ? ' selected' : ''}>true</option>
                        <option value="false"${single === 'false' ? ' selected' : ''}>false</option>
                    </select>`;
        }

        if (spec.control === 'date') {
            return `<input type="text" class="form-control form-control-sm flatpickr-date js-node-value"
                        data-node="${esc(node.nodeId)}" data-index="${index}" value="${esc(single)}"
                        placeholder="YYYY-MM-DD" autocomplete="off" />`;
        }

        if (spec.control === 'number') {
            return `<input type="number" step="any" class="form-control form-control-sm js-node-value"
                        data-node="${esc(node.nodeId)}" data-index="${index}" value="${esc(single)}" />`;
        }

        if (spec.control === 'select' || spec.control === 'cascade-select') {
            // Options are filled asynchronously after the render pass; the current value is pre-seeded so an
            // already-authored criterion never loses its value while the list is still loading.
            const seeded = spec.multi ? values : [single];
            const seedOptions = seeded.filter(v => String(v ?? '').trim() !== '')
                .map(v => `<option value="${esc(v)}" selected>${esc(v)}</option>`).join('');
            const cascade = spec.control === 'cascade-select'
                ? `<select class="form-select form-select-sm mb-1 js-node-cascade" data-node="${esc(node.nodeId)}">
                       <option value="">${esc(L.SelectTerritoryModel || '')}</option>
                   </select>`
                : '';
            return cascade + `<select class="form-select form-select-sm js-node-select"
                        data-node="${esc(node.nodeId)}" data-index="${index}"
                        data-taggable="${spec.taggable ? '1' : '0'}"
                        ${spec.multi ? 'multiple="multiple"' : ''}>
                        ${spec.multi ? '' : `<option value="">${esc(L.SelectOption || '')}</option>`}
                        ${seedOptions}
                    </select>`;
        }

        // plain text (free text, or a picker the actor may not browse)
        const disabledNote = spec.disabledReason
            ? `<small class="text-warning d-block">${esc(spec.disabledReason)}</small>` : '';
        if (spec.multi) {
            return `<input type="text" class="form-control form-control-sm js-node-multitext"
                        data-node="${esc(node.nodeId)}" value="${esc(values.join(', '))}"
                        placeholder="${esc(L.CommaSeparated || '')}" />${disabledNote}`;
        }
        return `<input type="text" class="form-control form-control-sm js-node-value"
                    data-node="${esc(node.nodeId)}" data-index="${index}" value="${esc(single)}" />${disabledNote}`;
    };

    /** Parameters get their OWN row under the three main controls, so they never squeeze the value field. */
    const parameterFields = node => {
        const definition = attributeFor(node.attributeCode);
        if (!definition) return '';
        const required = definition.requiredParameters || [];
        const optional = definition.optionalParameters || [];
        if (required.length === 0 && optional.length === 0) return '';

        const fields = required.concat(optional).map(name => {
            const isRequired = required.includes(name);
            const value = (node.parameters || {})[name] || '';
            return `<div class="col-6 col-md-3">
                        <label class="form-label small mb-1">${esc(name)}${isRequired ? ' <span class="text-danger">*</span>' : ''}</label>
                        <input type="text" class="form-control form-control-sm js-node-param"
                            data-node="${esc(node.nodeId)}" data-param="${esc(name)}" value="${esc(value)}" />
                    </div>`;
        }).join('');

        return `<div class="col-12">
                    <div class="row g-2 align-items-end pt-1 mt-1 border-top">${fields}</div>
                </div>`;
    };

    // ---------------------------------------------------------------- tree rendering (P1b)

    const moveTargets = node => {
        const forbidden = new Set([node.nodeId]);
        let grew = true;
        while (grew) {
            grew = false;
            nodes.forEach(n => {
                if (n.parentNodeId && forbidden.has(n.parentNodeId) && !forbidden.has(n.nodeId)) {
                    forbidden.add(n.nodeId);
                    grew = true;
                }
            });
        }

        const options = [`<option value=""${node.parentNodeId ? '' : ' selected'}>${esc(L.RootLevel || 'Root')}</option>`];
        nodes.filter(n => n.nodeKind === 'group' && !forbidden.has(n.nodeId)).forEach(g => {
            const selected = (node.parentNodeId || '') === g.nodeId ? ' selected' : '';
            options.push(`<option value="${esc(g.nodeId)}"${selected}>${esc(g.groupOperator)} · ${esc(g.nodeId.slice(0, 6))}</option>`);
        });
        return options.join('');
    };

    const nodeToolbar = node => `
        <div class="d-flex align-items-center gap-3 flex-shrink-0">
            <div class="form-check form-switch mb-0 d-flex align-items-center gap-1">
                <input class="form-check-input mt-0 js-node-negate" type="checkbox" role="switch"
                    data-node="${esc(node.nodeId)}" ${node.negate ? 'checked' : ''} id="negate-${esc(node.nodeId)}" />
                <label class="form-check-label small mb-0" for="negate-${esc(node.nodeId)}">NOT</label>
            </div>
            <details class="position-relative">
                <summary class="btn btn-sm btn-label-secondary">${esc(L.Move || 'Move')}</summary>
                <select class="form-select form-select-sm mt-1 js-node-parent" data-node="${esc(node.nodeId)}"
                    style="min-width: 12rem">${moveTargets(node)}</select>
            </details>
            <button type="button" class="btn btn-sm btn-icon btn-label-danger js-remove-node" data-node="${esc(node.nodeId)}"
                title="${esc(L.Remove || 'Remove')}"><i class="bx bx-trash"></i></button>
        </div>`;

    const renderPredicate = node => {
        const definition = attributeFor(node.attributeCode);
        const attributes = applicableAttributes().map(a =>
            `<option value="${esc(a.attributeCode)}"${a.attributeCode === node.attributeCode ? ' selected' : ''}>${esc(a.attributeCode)}</option>`).join('');
        const operators = (definition?.operators || []).map(op =>
            `<option value="${esc(op)}"${op === node.operator ? ' selected' : ''}>${esc(op)}</option>`).join('');

        const spec = controlFor(node);
        const slots = valueSlotCount(node);

        // The value ALWAYS occupies one column of the same width (4 / 3 / 5 adds up to 12). A two-bound `between`
        // splits inside that column instead of adding a fourth one, which is what used to overflow the row.
        // The value column carries an invisible spacer label rather than a caption: it keeps the control on the same
        // baseline as Attribute and Operator without inventing an untranslated word (no new RESX key).
        const valueBody = slots === 0
            ? `<div class="form-control form-control-sm bg-transparent border-0 px-0 text-muted small">${esc(L.NoValueNeeded || '—')}</div>`
            : slots === 2
                ? `<div class="row g-2">
                       ${Array.from({ length: 2 }, (_, i) => `
                       <div class="col-6">
                           <span class="d-block small text-muted mb-1">${esc(i === 0 ? (L.From || 'From') : (L.To || 'To'))}</span>
                           ${renderValueControl(node, i, spec)}
                       </div>`).join('')}
                   </div>`
                : renderValueControl(node, 0, spec);

        // Plain utility classes rather than .card: this sits inside the page's own section card, and a nested theme
        // card would stack a third shadow. A light border plus shadow-sm is the whole treatment.
        return `
        <div class="segment-predicate border rounded-3 shadow-sm bg-body p-3">
            <div class="d-flex justify-content-between align-items-center gap-2 mb-3">
                <span class="badge bg-label-primary text-uppercase">${esc(L.PredicateNode || 'condition')}</span>
                ${nodeToolbar(node)}
            </div>
            <div class="row g-3 align-items-end">
                <div class="col-12 col-md-4">
                    <label class="form-label small mb-1">${esc(L.Attribute || 'Attribute')}</label>
                    <select class="form-select form-select-sm js-node-attribute" data-node="${esc(node.nodeId)}">${attributes}</select>
                </div>
                <div class="col-12 col-md-3">
                    <label class="form-label small mb-1">${esc(L.Operator || 'Operator')}</label>
                    <select class="form-select form-select-sm js-node-operator" data-node="${esc(node.nodeId)}">${operators}</select>
                </div>
                <div class="col-12 col-md-5">
                    <label class="form-label small mb-1 invisible d-none d-md-block" aria-hidden="true">&nbsp;</label>
                    ${valueBody}
                </div>
                ${parameterFields(node)}
            </div>
        </div>`;
    };

    const renderGroup = node => {
        const operators = (contract?.vocabularies?.groupOperators || []).map(op =>
            `<option value="${esc(op)}"${op === node.groupOperator ? ' selected' : ''}>${esc(op).toUpperCase()}</option>`).join('');
        const children = childrenOf(node.nodeId);
        const isNot = node.groupOperator === 'not';

        // The rail is what makes AND / OR visible: everything inside this border is combined by the operator above it.
        // The group is the CONTAINER, so it takes the border and skips the shadow: only the condition cards inside it
        // lift off the surface, which is what makes the nesting readable instead of noisy.
        return `
        <div class="segment-group border rounded-3 p-3">
            <div class="d-flex justify-content-between align-items-center gap-2 mb-3">
                <div class="d-flex align-items-center gap-2 flex-wrap">
                    <span class="badge bg-label-secondary text-uppercase">${esc(L.GroupNode || 'group')}</span>
                    <select class="form-select form-select-sm js-node-group-operator" data-node="${esc(node.nodeId)}"
                        style="width: 7.5rem">${operators}</select>
                    <small class="text-muted">${esc(isNot ? (L.NotGroupHelp || '') : (L.GroupHelp || ''))}</small>
                </div>
                ${nodeToolbar(node)}
            </div>

            <div class="segment-group-body ps-3 border-start border-2">
                ${children.length === 0
                    ? `<div class="alert alert-warning py-2 px-3 mb-0 small"><i class="bx bx-info-circle me-1"></i>${esc(L.EmptyGroupHelp || '')}</div>`
                    : children.map(renderNode).join('')}
                ${isFrozen ? '' : `
                <div class="d-flex gap-2 flex-wrap mt-3">
                    <button type="button" class="btn btn-sm btn-label-primary js-add-inside" data-parent="${esc(node.nodeId)}" data-kind="predicate">
                        <i class="bx bx-plus me-1"></i>${esc(L.AddPredicateInside || '')}</button>
                    <button type="button" class="btn btn-sm btn-label-secondary js-add-inside" data-parent="${esc(node.nodeId)}" data-kind="group">
                        <i class="bx bx-folder-plus me-1"></i>${esc(L.AddGroupInside || '')}</button>
                </div>`}
            </div>
        </div>`;
    };

    const renderNode = node => `<div class="mb-3">${node.nodeKind === 'group' ? renderGroup(node) : renderPredicate(node)}</div>`;

    const render = () => {
        if (!listEl) return;

        const roots = childrenOf(null);
        emptyEl?.classList.toggle('d-none', roots.length > 0);

        const rootCombinator = (document.getElementById('MatchMode')?.value || 'all') === 'any' ? 'OR' : 'AND';
        listEl.innerHTML = roots.length === 0 ? '' : `
            <div class="mb-2">
                <span class="badge bg-label-dark text-uppercase">${esc(rootCombinator)}</span>
                <span class="text-muted small">${esc(L.RootCombinatorHelp || '')}</span>
            </div>
            ${roots.map(renderNode).join('')}`;

        // Disabling every control is what makes the freeze visible instead of merely enforced server-side.
        if (isFrozen) {
            listEl.querySelectorAll('select, input, button').forEach(el => { el.disabled = true; });
        }

        syncHidden();
        void hydrateControls();
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
            const node = findNode(el.dataset.node);
            if (!node) continue;
            const spec = controlFor(node);

            let options = spec.options || [];
            if (spec.async) {
                options = await spec.async();
            } else if (spec.control === 'cascade-select') {
                const modelId = el.previousElementSibling?.value || '';
                options = modelId ? await loadEntityOptions('territory-node', modelId) : [];
            }

            const chosen = spec.multi ? (node.values || []) : [(node.values || [])[Number(el.dataset.index) || 0] ?? ''];
            const known = new Set(options.map(o => String(o.value)));
            const head = spec.multi ? '' : `<option value="">${esc(L.SelectOption || '')}</option>`;
            // An already-authored value that the set no longer publishes stays visible and selected rather than
            // silently disappearing from the rule.
            const extras = chosen.filter(v => String(v ?? '').trim() !== '' && !known.has(String(v)))
                .map(v => `<option value="${esc(v)}" selected>${esc(v)}</option>`).join('');
            el.innerHTML = head + extras + options.map(o =>
                `<option value="${esc(o.value)}"${chosen.includes(String(o.value)) ? ' selected' : ''}>${esc(o.text)}</option>`).join('');

            if (window.jQuery?.fn?.select2 && !isFrozen) {
                const $el = window.jQuery(el);
                if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
                $el.select2({
                    dropdownParent: window.jQuery(document.body),
                    // The panel is appended to <body>, so it needs its own class hook to be sized like the small
                    // control it belongs to; #criteriaList scoping cannot reach it.
                    dropdownCssClass: 'segment-criteria-dropdown',
                    width: '100%',
                    // tags:true keeps the free-text escape hatch: the list is what is OFFERED, not a restriction.
                    tags: el.dataset.taggable === '1',
                    placeholder: L.SelectOption || '',
                    allowClear: !el.multiple
                });
                $el.off('change.segment').on('change.segment', () => writeSelectValue(el));
            }
        }

        // Territory node needs its model first: fill the cascade heads.
        for (const el of Array.from(listEl.querySelectorAll('.js-node-cascade'))) {
            const models = await loadEntityOptions('territory-model');
            const current = el.value;
            el.innerHTML = `<option value="">${esc(L.SelectTerritoryModel || '')}</option>`
                + models.map(m => `<option value="${esc(m.value)}"${m.value === current ? ' selected' : ''}>${esc(m.text)}</option>`).join('');
        }
    };

    const writeSelectValue = el => {
        const node = findNode(el.dataset.node);
        if (!node) return;
        if (el.multiple) {
            node.values = Array.from(el.selectedOptions).map(o => o.value).filter(v => String(v).trim() !== '');
        } else {
            node.values = node.values || [];
            node.values[Number(el.dataset.index) || 0] = el.value;
        }
        syncHidden();
    };

    // ---------------------------------------------------------------- events

    const findNode = id => nodes.find(n => n.nodeId === id);

    document.addEventListener('change', event => {
        const attribute = event.target.closest('.js-node-attribute');
        if (attribute) {
            const node = findNode(attribute.dataset.node);
            if (node) { applyAttribute(node, attribute.value); render(); }
            return;
        }

        const operator = event.target.closest('.js-node-operator');
        if (operator) {
            const node = findNode(operator.dataset.node);
            // The value control depends on the operator (single / two bounds / multi), so this is a structural change.
            if (node) { node.operator = operator.value; node.values = []; render(); }
            return;
        }

        const groupOperator = event.target.closest('.js-node-group-operator');
        if (groupOperator) {
            const node = findNode(groupOperator.dataset.node);
            if (node) { node.groupOperator = groupOperator.value; render(); }
            return;
        }

        const parent = event.target.closest('.js-node-parent');
        if (parent) {
            const node = findNode(parent.dataset.node);
            if (!node) return;
            const target = parent.value || null;
            if (target && childrenOf(target).length >= maxChildren) {
                window.showToast?.(`${L.LimitReached || 'Limit'} (${maxChildren})`, 'error');
                render();
                return;
            }
            const previous = node.parentNodeId;
            node.parentNodeId = target;
            node.sortOrder = nextSortOrder(target);
            if (depthOf(node) > maxDepth) {
                window.showToast?.(`${L.LimitReached || 'Limit'} (${maxDepth})`, 'error');
                node.parentNodeId = previous;
            }
            render();
            return;
        }

        const cascade = event.target.closest('.js-node-cascade');
        if (cascade) { void hydrateControls(); return; }

        const select = event.target.closest('.js-node-select');
        if (select) { writeSelectValue(select); return; }

        const value = event.target.closest('.js-node-value');
        if (value) {
            const node = findNode(value.dataset.node);
            if (node) {
                node.values = node.values || [];
                node.values[Number(value.dataset.index) || 0] = value.value;
                syncHidden();
            }
            return;
        }

        const multiText = event.target.closest('.js-node-multitext');
        if (multiText) {
            const node = findNode(multiText.dataset.node);
            if (node) {
                node.values = multiText.value.split(',').map(v => v.trim()).filter(Boolean);
                syncHidden();
            }
            return;
        }

        const parameter = event.target.closest('.js-node-param');
        if (parameter) {
            const node = findNode(parameter.dataset.node);
            if (node) { node.parameters[parameter.dataset.param] = parameter.value; syncHidden(); }
            return;
        }

        const negate = event.target.closest('.js-node-negate');
        if (negate) {
            const node = findNode(negate.dataset.node);
            if (node) { node.negate = negate.checked; syncHidden(); }
            return;
        }

        // The applicable attribute set depends on the subject type, and the root badge on the match mode.
        if (event.target === subjectTypeEl || event.target === segmentTypeEl
            || event.target.id === 'MatchMode') {
            editor.dataset.subjectType = currentSubjectType();
            applySegmentTypeVisibility();
            refreshMemberAvailability();
            render();
        }
    });

    document.addEventListener('input', event => {
        const value = event.target.closest('.js-node-value');
        if (value && value.type === 'text') {
            const node = findNode(value.dataset.node);
            if (node) {
                node.values = node.values || [];
                node.values[Number(value.dataset.index) || 0] = value.value;
                syncHidden();
            }
        }
    });

    document.addEventListener('click', event => {
        if (event.target.closest('#btnAddGroup')) { event.preventDefault(); if (addNode('group', null)) render(); return; }
        if (event.target.closest('#btnAddPredicate')) { event.preventDefault(); if (addNode('predicate', null)) render(); return; }

        // P1b: the parent is WHERE you clicked, not something you pick from a dropdown afterwards.
        const inside = event.target.closest('.js-add-inside');
        if (inside) {
            event.preventDefault();
            if (addNode(inside.dataset.kind, inside.dataset.parent)) render();
            return;
        }

        const remove = event.target.closest('.js-remove-node');
        if (remove) { event.preventDefault(); removeNode(remove.dataset.node); render(); return; }

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
        // A dynamic segment refuses manual rows outright. The whole section is hidden for it
        // (applySegmentTypeVisibility), so this only keeps the button honest if the section is ever revealed.
        const addBtn = document.getElementById('btnAddMember');
        if (addBtn) addBtn.disabled = currentSegmentType() === 'dynamic';
    };

    const renderMembers = () => {
        if (!memberListEl) return;
        memberEmptyEl?.classList.toggle('d-none', members.length > 0);
        memberListEl.innerHTML = members.map(m => `
            <div class="border rounded p-3 d-flex justify-content-between align-items-start ${m.isArchived ? 'opacity-50' : ''}">
                <div>
                    <div class="d-flex align-items-center gap-2 mb-1">
                        <span class="badge bg-label-${m.membershipMode === 'manual-include' ? 'success' : 'danger'}">${esc(m.membershipMode)}</span>
                        <span class="fw-medium">${esc(m.subjectDisplayName || m.subjectId)}</span>
                        ${m.isArchived ? `<span class="badge bg-label-secondary">${esc(L.Archived || 'archived')}</span>` : ''}
                    </div>
                    <div class="text-muted small">${esc(m.selectionReason)}</div>
                    <div class="text-muted small">${esc(m.subjectId)}</div>
                </div>
                ${m.isArchived ? '' : `
                <div class="d-flex gap-2">
                    <button type="button" class="btn btn-sm btn-label-secondary js-edit-member" data-id="${esc(m.targetCustomerId)}"><i class="bx bx-edit"></i></button>
                    <button type="button" class="btn btn-sm btn-label-warning js-archive-member" data-id="${esc(m.targetCustomerId)}"><i class="bx bx-archive-in"></i></button>
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

    /**
     * The subject picker: choose an Account or a Contact from the list that already exists, instead of pasting a GUID.
     * Which list depends on the segment's own SubjectType, so a contact segment can never be handed an account.
     *
     * It is a CONVENIENCE, not a contract change. Selecting one fills the id and display-name fields below; the id
     * field stays authoritative and hand-typed ids keep working, and the runtime still stores the id exactly as given
     * without reading the referenced master (D-TC).
     *
     * dropdownParent is the offcanvas itself: a Select2 parented to <body> renders behind the offcanvas backdrop and
     * the list becomes unusable (the Working Calendar imports lesson).
     */
    const setupSubjectPicker = (member) => {
        const block = document.getElementById('memberPickerBlock');
        const picker = document.getElementById('memberSubjectPicker');
        const label = document.getElementById('memberPickerLabel');
        const hint = document.getElementById('memberPickerHint');
        if (!block || !picker) return;

        const isContact = currentSubjectType() === 'contact';
        const entityKind = isContact ? 'contact' : 'account';

        // No permission to browse that master: the picker is hidden and the raw id field carries the whole job.
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

        // Editing an existing row: the subject is immutable, so the picker is disabled exactly like the id field.
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

        // Modes come from the contract vocabulary: exactly two, and never a derived third.
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

    // ---------------------------------------------------------------- bootstrap

    const init = async () => {
        // Section visibility depends only on the select's own value, so it is settled BEFORE the contract call: a
        // catalog outage must not leave a static segment showing a criteria editor it can never save.
        applySegmentTypeVisibility();

        try {
            [catalog, contract] = await Promise.all([getJson('/attribute-catalog'), getJson('/contract')]);
        } catch (error) {
            window.showToast?.(error.message || L.SegmentContractUnavailable, 'error');
            return;
        }

        try { nodes = JSON.parse(hidden.value || '[]') || []; }
        catch (error) { nodes = []; }
        nodes = nodes.map(n => ({
            nodeId: n.nodeId || uid(),
            parentNodeId: n.parentNodeId || null,
            nodeKind: n.nodeKind,
            groupOperator: n.groupOperator || null,
            attributeCode: n.attributeCode || null,
            operator: n.operator || null,
            values: n.values || [],
            valueType: n.valueType || null,
            parameters: n.parameters || {},
            negate: !!n.negate,
            sortOrder: typeof n.sortOrder === 'number' ? n.sortOrder : 0,
            label: n.label || null
        }));

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
