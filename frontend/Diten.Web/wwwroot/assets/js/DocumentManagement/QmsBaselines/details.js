/**
 * MOD-0028-FU03 - QMS Baseline detail + nested definition tree + publish.
 * Same-origin proxy only. The nested tree is built from the backend's parentCanonicalId/displayOrder;
 * it is never flattened and folder names are shown as atomic values (names may contain '/').
 */
'use strict';

(function () {
    let L = window.L10n || {};
    const root = document.getElementById('qmsBaselineRoot');
    if (!root) return;

    const baselineId = root.dataset.baselineId;
    const canPublish = root.dataset.canPublish === '1';
    const skeleton = document.getElementById('qmsBaselineSkeleton');
    const errorBox = document.getElementById('qmsBaselineError');
    const content = document.getElementById('qmsBaselineContent');
    const statusBadge = document.getElementById('qmsStatusBadge');
    const btnPublish = document.getElementById('btnPublish');
    const publishSpinner = document.getElementById('publishSpinner');
    const treeEl = document.getElementById('qmsDefinitionTree');
    const treeError = document.getElementById('qmsTreeError');

    const t = (k, fallback) => (L[k] || fallback || k);
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const setText = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = (v === null || v === undefined || v === '') ? '-' : String(v); };
    const setHtml = (id, v) => { const el = document.getElementById(id); if (el) el.innerHTML = (v === null || v === undefined || v === '') ? '-' : String(v); };
    // Two-line stacked date/time (project standard, cf. list page): "Jun 18, 26" + muted "05:04 PM".
    const fmtDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        if (Number.isNaN(d.getTime())) return String(v);
        const locale = window.CurrentLanguage || undefined;
        const datePart = new Intl.DateTimeFormat(locale, { month: 'short', day: '2-digit', year: '2-digit' }).format(d);
        const timePart = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', hour12: true }).format(d);
        return `<span class="d-inline-flex flex-column lh-sm"><span>${datePart}</span><small class="text-muted">${timePart}</small></span>`;
    };
    const antiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const mapReason = (rc) => ({
        VALIDATION_FAILED: t('ReasonValidationFailed'),
        CONFLICT: t('ReasonConflict'),
        PERM_DENIED: t('ReasonPermDenied'),
        NOT_FOUND_NON_LEAKAGE: t('ReasonNotFound')
    }[rc] || t('ErrorOccurred'));

    const showError = (message, corr) => {
        skeleton?.classList.add('d-none');
        errorBox.classList.remove('d-none');
        errorBox.innerHTML = esc(message) + (corr ? `<div class="small mt-2">${t('CorrelationId')}: <code>${esc(corr)}</code></div>` : '');
    };

    const getJson = async (url) => {
        const res = await fetch(url, { method: 'GET', credentials: 'same-origin' });
        let json = null;
        try { json = await res.json(); } catch (e) { /* ignore */ }
        return { ok: res.ok, status: res.status, json };
    };

    const renderStatusBadge = (status) => {
        const s = String(status || '').toUpperCase();
        statusBadge.className = 'badge ' + (s === 'PUBLISHED' ? 'bg-label-success' : s === 'DRAFT' ? 'bg-label-warning' : 'bg-label-secondary');
        statusBadge.textContent = s === 'PUBLISHED' ? t('StatusPublished') : s === 'DRAFT' ? t('StatusDraft') : t('Unknown');
    };

    const renderDetail = (d) => {
        setText('md-baselineReleaseId', d.baselineReleaseId);
        setText('md-version', d.baselineVersion);
        setText('md-definitionCount', d.definitionCount);
        setText('md-snapshotHash', d.snapshotHash);
        setHtml('md-createdAt', fmtDate(d.createdAt));
        setHtml('md-publishedAt', fmtDate(d.publishedAt));
        renderStatusBadge(d.status);
        const subtitle = document.getElementById('qmsBaselineSubtitle');
        if (subtitle) subtitle.textContent = d.baselineReleaseId || '';
        if (btnPublish && canPublish && String(d.status).toUpperCase() === 'DRAFT') {
            btnPublish.classList.remove('d-none');
        }
        skeleton?.classList.add('d-none');
        content?.classList.remove('d-none');
    };

    // Node binding metadata keyed by the (controlled) jsTree node id, so selection lookup never depends on how
    // jsTree preserves custom `data` across versions. Rebuilt on every renderTree().
    const nodeMetaById = new Map();

    // Build nested tree from a flat list (parentCanonicalId + displayOrder), preserving order, never flattening.
    const buildTree = (items) => {
        const byParent = new Map();
        items.forEach((it) => {
            const key = it.parentCanonicalId || '__root__';
            if (!byParent.has(key)) byParent.set(key, []);
            byParent.get(key).push(it);
        });
        byParent.forEach((arr) => arr.sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0)));
        return byParent;
    };

    // jsTree node from the flat definition (parentCanonicalId hierarchy). Folder name is atomic (may contain '/'),
    // shown verbatim and escaped. Extra metadata is surfaced via the node tooltip (a_attr.title).
    const toJstreeNode = (node, byParent) => {
        const children = byParent.get(node.canonicalId) || [];
        const titleBits = [];
        if (node.fullPath) titleBits.push(`${t('FullPath')}: ${node.fullPath}`);
        if (node.requiredByScope) titleBits.push(node.requiredByScope);
        if (node.defaultClassificationLevel) titleBits.push(node.defaultClassificationLevel);
        // Controlled, DOM-safe node id; canonicalId is unique per node within a baseline.
        const meta = { id: node.id, canonicalId: node.canonicalId, fullPath: node.fullPath, name: node.name };
        const nodeId = node.canonicalId ? `qnode-${node.canonicalId}` : undefined;
        if (nodeId) nodeMetaById.set(nodeId, meta);
        return {
            id: nodeId,
            text: `<span class="fw-medium">${esc(node.name || '')}</span>`,
            type: 'folder', // every QMS definition node is a documentation folder
            state: { opened: true },
            a_attr: { title: titleBits.join('  ·  ') },
            // Carried so node selection can look up Related Corporate Templates by binding (read-only).
            data: meta,
            children: children.map((c) => toJstreeNode(c, byParent))
        };
    };

    const $tree = () => window.jQuery(treeEl);
    const treeApi = () => (window.jQuery && window.jQuery.fn.jstree ? $tree().jstree(true) : null);

    const setupTreeToolbar = () => {
        // Drive the jsTree instance (initialized later by renderTree) via its public API.
        document.getElementById('btnExpandAll')?.addEventListener('click', () => treeApi()?.open_all());
        document.getElementById('btnCollapseAll')?.addEventListener('click', () => treeApi()?.close_all());
        const search = document.getElementById('qmsTreeSearch');
        let debounce;
        search?.addEventListener('input', () => {
            clearTimeout(debounce);
            debounce = setTimeout(() => treeApi()?.search(search.value || ''), 200);
        });
    };

    const emptyStateHtml = () =>
        `<div class="text-center py-5">` +
        `<i class="bx bx-folder-open mb-3" style="font-size:3rem;line-height:1;color:var(--bs-secondary-color,#a7acb2);"></i>` +
        `<h6 class="mb-2">${esc(t('NoDefinitionsHeading'))}</h6>` +
        `<p class="text-muted mb-0 mx-auto" style="max-width:520px;">${esc(t('NoDefinitions'))}</p>` +
        `</div>`;

    // When the tree has no definitions: hide the tree toolbar (search + expand/collapse) and disable Publish
    // with an explanatory tooltip (a disabled <button> needs pointer-events re-enabled for the tooltip to show).
    const applyTreeEmptyConstraints = (isEmpty) => {
        document.getElementById('qmsTreeToolbar')?.classList.toggle('d-none', isEmpty);
        if (!btnPublish) return;
        const bs = window.bootstrap;
        if (isEmpty) {
            btnPublish.disabled = true;
            btnPublish.style.pointerEvents = 'auto';
            btnPublish.setAttribute('data-bs-toggle', 'tooltip');
            btnPublish.setAttribute('data-bs-placement', 'top');
            btnPublish.setAttribute('title', t('PublishNeedsDefinitions'));
            bs?.Tooltip?.getOrCreateInstance(btnPublish);
        } else {
            bs?.Tooltip?.getInstance(btnPublish)?.dispose();
            btnPublish.disabled = false;
            btnPublish.style.pointerEvents = '';
            btnPublish.removeAttribute('data-bs-toggle');
            btnPublish.removeAttribute('title');
        }
    };

    const renderTree = (items) => {
        nodeMetaById.clear();
        destroyRelatedDt?.(); // drop any stale DataTable instance before the tree is rebuilt
        relatedShow?.('select'); // reset the related panel to its "pick a folder" state on (re)render
        if (!window.jQuery || !window.jQuery.fn.jstree) {
            treeEl.innerHTML = emptyStateHtml();
            return;
        }
        const $t = $tree();
        if ($t.jstree(true)) $t.jstree('destroy');
        if (!items || !items.length) {
            treeEl.innerHTML = emptyStateHtml();
            applyTreeEmptyConstraints(true);
            return;
        }
        const byParent = buildTree(items);
        const roots = byParent.get('__root__') || [];
        const data = roots.map((r) => toJstreeNode(r, byParent));
        const theme = window.jQuery('html').attr('data-bs-theme') === 'dark' ? 'default-dark' : 'default';
        $t.jstree({
            core: { themes: { name: theme, dots: true, responsive: true }, data: data, multiple: false },
            plugins: ['types', 'search', 'wholerow'],
            types: {
                default: { icon: 'icon-base bx bx-folder text-warning' },
                folder: { icon: 'icon-base bx bx-folder text-warning' },
                file: { icon: 'icon-base bx bx-file text-muted' }
            },
            search: { show_only_matches: true, show_only_matches_children: true, close_opened_onclear: false }
        });
        // Read-only: selecting a node loads its Related Corporate Templates (no tree mutation).
        // Resolve metadata via the controlled node id first, then fall back to jsTree's preserved data.
        $t.off('select_node.jstree').on('select_node.jstree', (e, sel) => {
            const meta = nodeMetaById.get(sel?.node?.id) || sel?.node?.data || sel?.node?.original?.data || {};
            loadRelatedTemplates(meta);
        });
        applyTreeEmptyConstraints(false);
    };

    // ── MOD-0029-FU02A: Related Corporate Templates for the selected definition node (read-only) ──
    const relatedEls = {
        states: document.getElementById('relatedTplStates'),
        select: document.getElementById('relatedTplSelect'),
        loading: document.getElementById('relatedTplLoading'),
        error: document.getElementById('relatedTplError'),
        empty: document.getElementById('relatedTplEmpty'),
        wrap: document.getElementById('relatedTplTableWrap'),
        table: document.getElementById('relatedTemplatesTable'),
        body: document.getElementById('relatedTplBody'),
        path: document.getElementById('relatedTplPath')
    };
    let relatedReqSeq = 0;
    let relatedDt = null;

    const destroyRelatedDt = () => {
        if (relatedDt) { try { relatedDt.destroy(); } catch (e) { /* ignore */ } relatedDt = null; }
    };

    // The table lives in its own card-datatable surface; the message states share the card-body. Show one or
    // the other (golden compact list surface for results, centered message otherwise).
    const relatedShow = (which) => {
        ['select', 'loading', 'error', 'empty'].forEach((k) => relatedEls[k]?.classList.add('d-none'));
        if (which === 'wrap') {
            relatedEls.states?.classList.add('d-none');
            relatedEls.wrap?.classList.remove('d-none');
        } else {
            relatedEls.wrap?.classList.add('d-none');
            relatedEls.states?.classList.remove('d-none');
            relatedEls[which]?.classList.remove('d-none');
        }
    };

    const masterBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const color = { DRAFT: 'warning', PUBLISHED: 'success', DEPRECATED: 'danger', ARCHIVED: 'secondary' }[s] || 'secondary';
        const label = { DRAFT: t('StatusDraft'), PUBLISHED: t('StatusPublished'), DEPRECATED: t('StatusDeprecated'), ARCHIVED: t('StatusArchived') }[s] || t('Unknown');
        return `<span class="badge bg-label-${color}">${esc(label)}</span>`;
    };
    const variantLabel = (v) => {
        const s = String(v || '').toUpperCase();
        if (s === 'LOCKED') return esc(t('VariantLocked'));
        if (s === 'ALLOWED') return esc(t('VariantAllowed'));
        return s ? esc(s) : '-';
    };

    // Standard golden-compact action cell (full-size btn btn-icon + icon-md), dispatched via DitenDataTable.
    const renderRelatedActions = (r) => {
        const actions = [{ key: 'view', icon: 'bx bx-show', attrs: { 'data-id': r.id, title: t('ViewDetails'), 'aria-label': t('ViewDetails') } }];
        return window.DitenDataTable?.renderActions
            ? window.DitenDataTable.renderActions(actions)
            : `<a href="/DocumentManagementTemplateMasters/Details/${esc(r.id)}" class="btn btn-icon"><i class="bx bx-show icon-md"></i></a>`;
    };

    // Bind the row-action dispatcher once (self-guards); the <table> element persists across DataTable re-inits.
    if (relatedEls.table && window.DitenDataTable?.bindActionDispatcher) {
        window.DitenDataTable.bindActionDispatcher({
            tableEl: relatedEls.table,
            getTable: () => relatedDt,
            onRowAction: {
                view: ({ id }) => { if (id) window.location.href = `/DocumentManagementTemplateMasters/Details/${id}`; }
            }
        });
    }

    // Golden Compact DataTable v2: build rows (with control + actions columns), then init via DtDefaults.
    const renderRelatedRows = (rows) => {
        destroyRelatedDt();
        relatedEls.body.innerHTML = rows.map((r) => {
            const owner = r.ownerCompanyId || r.ownerUserId || '-';
            return `<tr>
                <td></td>
                <td class="fw-medium">${esc(r.masterCode)}</td>
                <td>${esc(r.templateName)}</td>
                <td class="text-nowrap" data-order="${esc(r.currentMasterVersion)}">v${esc(r.currentMasterVersion)}</td>
                <td>${masterBadge(r.status)}</td>
                <td>${esc(r.classification) || '-'}</td>
                <td>${variantLabel(r.variantPolicy)}</td>
                <td class="text-truncate" style="max-width:160px" title="${esc(owner)}">${esc(owner)}</td>
                <td>${renderRelatedActions(r)}</td>
            </tr>`;
        }).join('');
        relatedShow('wrap');

        if (relatedEls.table && window.DataTable && window.DtDefaults?.create && window.DtDefaults?.exportButtons) {
            relatedDt = new DataTable(relatedEls.table, window.DtDefaults.create({
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                paging: true,
                lengthChange: true,
                searching: true,
                info: true,
                order: [[1, 'asc']],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 2, render: () => '' },
                    { targets: -1, orderable: false, searchable: false, className: 'cell-fit all' },
                    { responsivePriority: 1, targets: 1 }
                ],
                buttons: window.DtDefaults.exportButtons(
                    null,
                    {},
                    {},
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
                ),
                drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), 0); },
                initComplete: function () { window.DtDefaults?.updateVisualState?.(this.api(), 0); }
            }));
        }
    };

    const loadRelatedTemplates = async (node) => {
        const collectionDefinitionId = node?.id || '';
        const canonicalId = node?.canonicalId || '';
        destroyRelatedDt();
        if (!collectionDefinitionId && !canonicalId) {
            relatedEls.path?.classList.add('d-none');
            relatedShow('select');
            return;
        }
        if (relatedEls.path) {
            const label = node?.fullPath || node?.name || '';
            relatedEls.path.textContent = label;
            relatedEls.path.classList.toggle('d-none', !label);
        }
        const seq = ++relatedReqSeq;
        relatedShow('loading');
        const qs = new URLSearchParams();
        if (collectionDefinitionId) qs.set('collectionDefinitionId', collectionDefinitionId);
        if (canonicalId) qs.set('canonicalId', canonicalId);
        const { ok, json } = await getJson(`/DocumentManagementQmsBaselines/related-template-masters?${qs.toString()}`);
        if (seq !== relatedReqSeq) return; // superseded by a newer selection
        if (!ok || !json || json.isSuccessful === false) {
            relatedEls.error.textContent = mapReason(json?.reason_code || json?.reasonCode);
            relatedShow('error');
            return;
        }
        const rows = json.data || json.Data || [];
        if (!rows.length) { relatedShow('empty'); return; }
        renderRelatedRows(rows);
    };

    const loadDefinitions = async () => {
        const { json } = await getJson(`/DocumentManagementQmsBaselines/definitions/${baselineId}`);
        if (json && json.isSuccessful) {
            renderTree(json.data || json.Data || []);
        } else {
            treeError.classList.remove('d-none');
            treeError.textContent = mapReason(json?.reason_code || json?.reasonCode);
        }
    };

    const load = async () => {
        if (!baselineId) { showError(t('NotFound')); return; }
        const { status, json } = await getJson(`/DocumentManagementQmsBaselines/detail/${baselineId}`);
        if (json && json.isSuccessful && json.data) {
            renderDetail(json.data);
            await loadDefinitions();
        } else if (status === 404) {
            showError(t('NotFound'), json?.correlation_id);
        } else if (status === 403) {
            showError(t('AccessDenied'), json?.correlation_id);
        } else {
            showError(mapReason(json?.reason_code || json?.reasonCode), json?.correlation_id || json?.correlationId);
        }
    };

    btnPublish?.addEventListener('click', () => {
        const doPublish = async () => {
            btnPublish.disabled = true; publishSpinner.classList.remove('d-none');
            try {
                const token = antiForgeryToken();
                const fd = new FormData();
                fd.append('expectedVersion', '0'); // server still guards with the loaded version
                if (token) fd.append('__RequestVerificationToken', token);
                const res = await fetch(`/DocumentManagementQmsBaselines/publish/${baselineId}`, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token },
                    body: fd
                });
                let json = null; try { json = await res.json(); } catch (e) { /* ignore */ }
                if (json && json.isSuccessful) {
                    window.showToast?.(t('PublishSuccess'), 'success');
                    btnPublish.classList.add('d-none');
                    await load();
                } else {
                    window.showToast?.(mapReason(json?.reason_code || json?.reasonCode), 'error');
                    btnPublish.disabled = false;
                }
            } catch (e) {
                console.error('[QmsBaselines] publish failed', e);
                window.showToast?.(t('ErrorOccurred'), 'error');
                btnPublish.disabled = false;
            } finally {
                publishSpinner.classList.add('d-none');
            }
        };
        if (typeof window.showConfirm === 'function') {
            window.showConfirm(t('PublishConfirm'), doPublish, { type: 'warning', confirmButtonText: t('Publish') });
        } else if (window.confirm(t('PublishConfirm'))) {
            doPublish();
        }
    });

    setupTreeToolbar();
    document.addEventListener('DOMContentLoaded', load);
})();
