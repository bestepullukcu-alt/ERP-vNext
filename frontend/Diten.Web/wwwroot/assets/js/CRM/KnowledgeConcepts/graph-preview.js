/**
 * MOD-0162-FU03 Tab 5 — Graph Preview. READ-ONLY adjacency render; there is no DataTable and no write path here.
 *
 * It calls exactly three existing read endpoints, unchanged, through the same-origin proxy:
 *   subject   → GET /CRM/KnowledgeConcepts/api/concept-graph?subjectId=…&effectiveAt=…&includeArchived=…
 *   node      → GET /CRM/KnowledgeConcepts/api/concept-graph/by-node/{nodeId}?includeArchived=…
 *   content   → GET /CRM/KnowledgeConcepts/api/concept-graph/by-content/{contentId}?includeArchived=…
 *
 * BOUNDARY (FU01C / pack §6.1, AC-GRAPH-DEPTH): by-node is EXACTLY 1 hop and by-content EXACTLY 2 edge layers.
 * Those depths are fixed by the contract, so this module sends no depth/maxHops parameter, computes no transitive
 * closure, walks no second hop client-side, ranks nothing and recommends nothing. It renders what the endpoint
 * returned, in the order the endpoint returned it. Empty means empty — no graph is invented.
 */
(function (window, document) {
    'use strict';
    if (!document.getElementById('btnGraphLoad')) return;

    const base = '/CRM/KnowledgeConcepts/api';
    let L = window.ConceptL10n || window.L10n || {};

    const headers = { Accept: 'application/json' };
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const stamp = v => v ? new Date(v).toLocaleDateString() : '—';

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    const el = id => document.getElementById(id);
    const show = (node, visible) => node?.classList.toggle('d-none', !visible);

    // ─── Reference labels (read-only) ────────────────────────────────────────
    const subjectMap = {}, typeMap = {}, nodeMap = {}, contentMap = {};
    const nodeRows = [];

    const fillSelect = (id, options, placeholderFirst) => {
        const target = el(id);
        if (!target) return;
        const head = placeholderFirst ? `<option value=""></option>` : '';
        target.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    const initSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        jq('#tab-concept-graph select.graph-select2').each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({ placeholder: $s.data('placeholder') || '', width: '100%' });
        });
    };

    const loadReferences = async () => {
        const grab = async (path, map, idKey, codeKey, nameKey, sink) => {
            try {
                const data = await envelope(await fetch(`${base}/${path}`, { credentials: 'same-origin', headers }));
                (data?.items || []).forEach(x => {
                    if (!x[idKey]) return;
                    map[x[idKey]] = `${x[codeKey]} — ${x[nameKey]}`;
                    if (sink) sink.push(x);
                });
            } catch (e) { /* a picker stays empty; the read endpoints still answer for a hand-picked id */ }
        };
        await Promise.all([
            grab('subjects?includeArchived=true', subjectMap, 'subjectId', 'subjectCode', 'subjectName'),
            grab('concept-types?includeArchived=true', typeMap, 'conceptTypeId', 'conceptTypeCode', 'conceptTypeName'),
            grab('concept-nodes?includeArchived=true', nodeMap, 'conceptNodeId', 'conceptNodeCode', 'conceptNodeName', nodeRows),
            grab('contents?includeArchived=true', contentMap, 'contentId', 'contentCode', 'contentTitle')
        ]);

        fillSelect('graphSubjectId', Object.keys(subjectMap).map(k => ({ value: k, text: subjectMap[k] })), true);
        fillSelect('graphNodeId', nodeRows.map(n => ({ value: n.conceptNodeId, text: nodeMap[n.conceptNodeId] })), true);
        fillSelect('graphContentId', Object.keys(contentMap).map(k => ({ value: k, text: contentMap[k] })), true);
        initSelect2();
    };

    const labelSubject = id => subjectMap[id] || id || '—';
    const labelType = id => typeMap[id] || id || '';
    const labelNode = id => nodeMap[id] || id || '';

    // ─── Scope switching ─────────────────────────────────────────────────────
    // Each scope carries its own fixed-depth statement. The statement is part of the contract, not a hint.
    const DEPTH_NOTICE = {
        subject: () => L.GraphDepthNoticeSubject,
        node: () => L.GraphDepthNoticeNode,
        content: () => L.GraphDepthNoticeContent
    };
    const currentScope = () => norm(el('graphScope')?.value) || 'subject';
    const applyScope = () => {
        const scope = currentScope();
        document.querySelectorAll('#tab-concept-graph [data-graph-field]').forEach(field => {
            const kind = field.getAttribute('data-graph-field');
            // The subject picker and the effective-at filter belong to the subject scope only: by-node and by-content
            // resolve their own subject server-side and take no effectiveAt.
            show(field, kind === scope);
        });
        const text = el('graphDepthNoticeText');
        if (text) text.textContent = (DEPTH_NOTICE[scope] || DEPTH_NOTICE.subject)() || '';
    };

    // ─── Render ──────────────────────────────────────────────────────────────
    const conformanceBadge = ok => ok
        ? `<span class="badge bg-label-success">${esc(L.Conforming || '')}</span>`
        : `<span class="badge bg-label-warning" title="${esc(L.NonConformingNote || '')}">${esc(L.NonConforming || '')}</span>`;
    const statusBadge = v => `<span class="badge bg-label-${v === 'active' || v === 'published' ? 'success' : (v === 'archived' ? 'secondary' : 'primary')}">${esc(v || '—')}</span>`;

    const renderNodes = (nodes, edges) => {
        const host = el('graphNodes');
        if (!host) return;
        el('graphNodeCount').textContent = String(nodes.length);
        if (!nodes.length) { host.innerHTML = ''; return; }

        // Grouped by concept type purely for readability. Grouping is a display choice; it derives nothing.
        const groups = new Map();
        nodes.forEach(n => {
            const key = n.conceptTypeId || '';
            if (!groups.has(key)) groups.set(key, []);
            groups.get(key).push(n);
        });
        const incident = id => edges.some(e => e.fromConceptNodeId === id || e.toConceptNodeId === id);

        host.innerHTML = Array.from(groups.entries()).map(([typeId, list]) => `
            <div>
                <div class="text-muted small text-uppercase fw-semibold mb-2">${esc(labelType(typeId) || L.GraphUngroupedType || '')}</div>
                <div class="d-flex flex-column gap-2">
                    ${list.map(n => `
                        <div class="d-flex align-items-center justify-content-between gap-2 border rounded p-2">
                            <div class="min-w-0">
                                <div class="fw-medium text-heading text-truncate">${esc(n.conceptNodeName)}</div>
                                <div class="text-muted small text-truncate">${esc(n.conceptNodeCode)}${n.externalRefType ? ' · ' + esc(n.externalRefType) : ''}</div>
                                ${incident(n.conceptNodeId) ? '' : `<div class="text-muted small fst-italic">${esc(L.GraphSelfContained || '')}</div>`}
                            </div>
                            <div class="d-flex align-items-center gap-2 flex-shrink-0">
                                ${statusBadge(n.status)}
                                ${n.isArchived ? `<span class="badge bg-label-warning">${esc(L.Archived || '')}</span>` : ''}
                                <button type="button" class="btn btn-icon btn-sm btn-label-secondary js-graph-focus"
                                        data-id="${esc(n.conceptNodeId)}" title="${esc(L.GraphOpenNode || '')}"><i class="bx bx-target-lock"></i></button>
                            </div>
                        </div>`).join('')}
                </div>
            </div>`).join('');
    };

    const renderEdges = edges => {
        const host = el('graphEdges');
        if (!host) return;
        el('graphEdgeCount').textContent = String(edges.length);
        // The service already returns Priority → RelationshipCode order; it is echoed, never re-ranked.
        host.innerHTML = edges.map(e => `
            <div class="border rounded p-2">
                <div class="d-flex flex-wrap align-items-center gap-2">
                    <span class="fw-medium text-heading">${esc(labelNode(e.fromConceptNodeId))}</span>
                    <span class="text-muted d-inline-flex align-items-center gap-1">
                        <i class="bx bx-right-arrow-alt"></i>
                        <span class="badge bg-label-info">${esc(e.relationshipType)}</span>
                        ${e.direction === 'bidirectional' ? '<i class="bx bx-left-arrow-alt" title="bidirectional"></i>' : ''}
                    </span>
                    <span class="fw-medium text-heading">${esc(labelNode(e.toConceptNodeId))}</span>
                </div>
                <div class="d-flex flex-wrap align-items-center gap-2 mt-2">
                    <span class="text-muted small">${esc(e.relationshipCode)}</span>
                    <span class="text-muted small">${esc(L.Priority || '')}: ${esc(e.priority)}</span>
                    ${statusBadge(e.status)}
                    ${conformanceBadge(e.isTemplateConforming)}
                    ${e.isArchived ? `<span class="badge bg-label-warning">${esc(L.Archived || '')}</span>` : ''}
                    <span class="text-muted small ms-auto">${esc(stamp(e.effectiveFrom))}${e.effectiveTo ? ' → ' + esc(stamp(e.effectiveTo)) : ''}</span>
                </div>
            </div>`).join('');
    };

    const renderTemplates = templates => {
        const host = el('graphTemplates');
        if (!host) return;
        el('graphTemplateCount').textContent = String(templates.length);
        host.innerHTML = templates.map(t => `
            <div class="border rounded p-3">
                <div class="d-flex flex-wrap align-items-center gap-2 mb-2">
                    <span class="fw-medium text-heading">${esc(t.chainName)}</span>
                    <span class="text-muted small">${esc(t.chainCode)}</span>
                    ${t.chainVersion ? `<span class="badge bg-label-secondary">${esc(t.chainVersion)}</span>` : ''}
                    ${statusBadge(t.status)}
                    ${t.isArchived ? `<span class="badge bg-label-warning">${esc(L.Archived || '')}</span>` : ''}
                </div>
                <div class="d-flex flex-wrap align-items-center gap-1">
                    ${(t.orderedConceptTypes || []).map((id, i) =>
                        `${i ? '<i class="bx bx-chevron-right text-muted"></i>' : ''}<span class="badge bg-label-primary">${esc(labelType(id))}</span>`).join('')}
                </div>
            </div>`).join('');
    };

    const render = (graph, emptyText) => {
        const nodes = graph?.nodes || [];
        const edges = graph?.edges || [];
        const templates = graph?.templates || [];
        const isEmpty = !nodes.length && !edges.length && !templates.length;

        show(el('graphPreviewPlaceholder'), false);
        show(el('graphPreviewResult'), !isEmpty);
        show(el('graphPreviewEmpty'), isEmpty);
        if (isEmpty) {
            const text = el('graphPreviewEmptyText');
            if (text) text.textContent = emptyText || L.GraphEmpty || '';
            return;
        }
        renderNodes(nodes, edges);
        renderEdges(edges);
        renderTemplates(templates);
    };

    // ─── Load ────────────────────────────────────────────────────────────────
    const showError = message => {
        const host = el('graphPreviewError');
        if (host) { host.textContent = message || L.ErrorState; host.classList.remove('d-none'); }
    };
    const clearError = () => el('graphPreviewError')?.classList.add('d-none');

    const load = async () => {
        clearError();
        const scope = currentScope();
        const includeArchived = el('graphIncludeArchived')?.checked ? 'true' : 'false';
        let url = null;
        let emptyText = L.GraphEmpty;

        if (scope === 'node') {
            const nodeId = norm(el('graphNodeId')?.value);
            if (!nodeId) return;
            url = `${base}/concept-graph/by-node/${nodeId}?includeArchived=${includeArchived}`;
        } else if (scope === 'content') {
            const contentId = norm(el('graphContentId')?.value);
            if (!contentId) return;
            url = `${base}/concept-graph/by-content/${contentId}?includeArchived=${includeArchived}`;
            // by-content answers 200 with an empty graph when the content has no concept link at all — that is a
            // meaningful state of its own, not an error and not "no data for the subject".
            emptyText = L.GraphContentNoLinks;
        } else {
            const subjectId = norm(el('graphSubjectId')?.value);
            if (!subjectId) return;
            const at = norm(el('graphEffectiveAt')?.value);
            url = `${base}/concept-graph?subjectId=${encodeURIComponent(subjectId)}&includeArchived=${includeArchived}`
                + (at ? `&effectiveAt=${encodeURIComponent(new Date(`${at}T00:00:00Z`).toISOString())}` : '');
        }

        try {
            render(await envelope(await fetch(url, { credentials: 'same-origin', headers })), emptyText);
        } catch (error) {
            show(el('graphPreviewResult'), false);
            show(el('graphPreviewEmpty'), false);
            show(el('graphPreviewPlaceholder'), true);
            showError(error.message);
        }
    };

    // "Focus this node" re-reads the SAME 1-hop endpoint with a different anchor. It is navigation, not traversal:
    // no hop is accumulated and no path is remembered.
    document.addEventListener('click', event => {
        const focus = event.target.closest('.js-graph-focus');
        if (!focus) return;
        event.preventDefault();
        const scopeEl = el('graphScope');
        const nodeEl = el('graphNodeId');
        if (!scopeEl || !nodeEl) return;
        scopeEl.value = 'node';
        nodeEl.value = focus.dataset.id;
        if (window.jQuery) { window.jQuery(scopeEl).trigger('change.select2'); window.jQuery(nodeEl).trigger('change.select2'); }
        applyScope();
        void load();
    });

    el('btnGraphLoad')?.addEventListener('click', () => void load());
    const scopeEl = el('graphScope');
    scopeEl?.addEventListener('change', applyScope);
    if (window.jQuery) window.jQuery(scopeEl).on('change', applyScope);

    // The pickers are built the first time the tab is opened, not on page load: select2 measures a control inside a
    // hidden tab-pane as zero-width, and a tab the user never opens should not cost four reference reads.
    let referencesReady = false;
    const ensureReferences = async () => {
        if (referencesReady) return;
        referencesReady = true;
        L = window.ConceptL10n || window.L10n || {};
        applyScope();
        await loadReferences();
    };

    document.querySelectorAll('button[data-bs-toggle="tab"]').forEach(btn => {
        btn.addEventListener('shown.bs.tab', event => {
            if (event.target.getAttribute('data-bs-target') !== '#tab-concept-graph') return;
            void ensureReferences();
        });
    });

    applyScope();
})(window, document);
