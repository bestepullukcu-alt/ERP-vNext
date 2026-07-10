'use strict';

// ─────────────────────────────────────────────────────────────────────────────
// DitenTree — reusable hierarchy component.
//
// Builds an expand/collapse tree from a FLAT list (idField → parentField) and
// renders per-node row actions, live search, and optional drag-to-reparent.
// The consuming page owns all behaviour via callbacks; this component owns only
// the rendering, expand/collapse state, filtering and drag mechanics.
//
// All styles live in backbone-custom.css under the `.diten-tree*` namespace —
// there is no inline CSS here. Markup chrome comes from Views/Shared/_Tree.cshtml.
//
// Usage:
//   const tree = DitenTree.create('#myTreeHost', {
//     data, idField, parentField,
//     label:   (node, level) => ({ title, code, subtitle, statusHtml, iconLevel }),
//     actions: [{ key, icon, title, variant, visible(node), handler(node) }],
//     onAdd:   () => {},                 // root "add" button (shown when addLabel set)
//     addLabel:'Yeni Birim',
//     drag: { enabled, canDrop(drag, target|null), onDrop(drag, target|null) },
//     expandDepth: 1,
//     l10n: { expandAll, collapseAll, searchPlaceholder, empty, emptyHint }
//   });
// ─────────────────────────────────────────────────────────────────────────────
window.DitenTree = (function () {
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    const svg = (paths) =>
        `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${paths}</svg>`;

    const ICONS = {
        chevron: '<path d="m6 9 6 6 6-6"/>',
        expand: '<path d="m7 13 5 5 5-5M7 6l5 5 5-5"/>',
        collapse: '<path d="m7 11 5-5 5 5M7 18l5-5 5 5"/>',
        search: '<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>',
        node: '<circle cx="12" cy="12" r="3"/><path d="M12 2v7M12 15v7M2 12h7M15 12h7"/>'
    };

    function resolve(elOrSelector) {
        return typeof elOrSelector === 'string' ? document.querySelector(elOrSelector) : elOrSelector;
    }

    function create(hostArg, options) {
        const host = resolve(hostArg);
        if (!host) {
            console.error('[DitenTree] host element not found.', hostArg);
            return null;
        }

        const opts = Object.assign({
            data: [],
            idField: 'id',
            parentField: 'parentId',
            label: (node) => ({ title: String(node.name ?? node.id ?? ''), code: '', subtitle: '', statusHtml: '', iconLevel: 0 }),
            actions: [],
            onAdd: null,
            addLabel: '',
            drag: null,
            expandDepth: 1,
            l10n: {}
        }, options || {});

        const L = Object.assign({
            expandAll: 'Expand all', collapseAll: 'Collapse all',
            searchPlaceholder: 'Search…', empty: 'No records', emptyHint: ''
        }, opts.l10n || {});

        // The partial wraps host + toolbar + empty-state in [data-diten-tree-wrap].
        const wrap = host.closest('[data-diten-tree-wrap]') || host.parentElement;
        const searchInput = wrap?.querySelector('[data-tree-search]');
        const btnExpandAll = wrap?.querySelector('[data-tree-expand-all]');
        const btnCollapseAll = wrap?.querySelector('[data-tree-collapse-all]');
        const addBtn = wrap?.querySelector('[data-tree-add]');
        const emptyBox = wrap?.querySelector('[data-tree-empty]');

        let data = Array.isArray(opts.data) ? opts.data.slice() : [];
        const collapsed = new Set();   // node ids whose children are hidden (ignored while filtering)
        let filter = '';
        let selectedId = null;
        let dragId = null;             // id of the node currently being dragged

        const idOf = (n) => n[opts.idField] ?? n.id ?? n.Id;
        const parentOf = (n) => n[opts.parentField] ?? null;

        // ── hierarchy index ──────────────────────────────────────────────────
        function index() {
            const known = new Set(data.map(idOf));
            const kids = new Map();
            const byId = new Map();
            const roots = [];
            data.forEach((n) => byId.set(idOf(n), n));
            data.forEach((n) => {
                const pid = parentOf(n);
                if (pid != null && known.has(pid)) {
                    if (!kids.has(pid)) kids.set(pid, []);
                    kids.get(pid).push(n);
                } else {
                    roots.push(n);
                }
            });
            return { kids, byId, roots };
        }

        // ids of a node and everything beneath it — used so a node can't be dropped into its own subtree.
        function subtreeIds(rootId, kids) {
            const out = new Set([rootId]);
            const stack = [rootId];
            while (stack.length) {
                const cur = stack.pop();
                (kids.get(cur) || []).forEach((c) => {
                    const cid = idOf(c);
                    if (!out.has(cid)) { out.add(cid); stack.push(cid); }
                });
            }
            return out;
        }

        // ── filtering: a node stays visible if it or any descendant matches ──
        const matches = (n) => {
            if (!filter) return true;
            const l = opts.label(n) || {};
            return `${l.title || ''} ${l.code || ''} ${l.subtitle || ''}`.toLowerCase().includes(filter);
        };
        function markVisible(node, kids) {
            const childList = kids.get(idOf(node)) || [];
            const visKids = childList.filter((c) => markVisible(c, kids));
            const vis = matches(node) || visKids.length > 0;
            node.__vis = vis;
            node.__visKids = visKids;
            return vis;
        }

        function highlight(text) {
            const raw = String(text ?? '');
            if (!filter) return esc(raw);
            const i = raw.toLowerCase().indexOf(filter);
            if (i < 0) return esc(raw);
            return esc(raw.slice(0, i)) + '<mark>' + esc(raw.slice(i, i + filter.length)) + '</mark>' + esc(raw.slice(i + filter.length));
        }

        // ── node markup ──────────────────────────────────────────────────────
        function nodeHtml(node, level) {
            const id = idOf(node);
            const l = opts.label(node, level) || {};
            const kids = node.__visKids || [];
            const hasKids = kids.length > 0;
            const isCollapsed = collapsed.has(id) && !filter;
            const draggable = opts.drag?.enabled && (typeof opts.drag.canDrag !== 'function' || opts.drag.canDrag(node));

            const twisty = hasKids
                ? `<button type="button" class="diten-tree-twisty" data-tree-toggle="${esc(id)}" aria-label="${esc(hasKids ? L.expandAll : '')}">${svg(ICONS.chevron)}</button>`
                : `<span class="diten-tree-twisty is-leaf">${svg(ICONS.chevron)}</span>`;

            const iconLevel = Number.isInteger(l.iconLevel) ? l.iconLevel : Math.min(level, 3);
            const nodeIcon = `<span class="diten-tree-icon lvl-${Math.min(iconLevel, 3)}">${svg(l.icon || ICONS.node)}</span>`;

            const code = l.code ? `<span class="diten-tree-code">${esc(l.code)}</span>` : '';
            const count = hasKids ? `<span class="diten-tree-count">${kids.length}</span>` : '';
            const subtitle = l.subtitle ? `<div class="diten-tree-sub">${esc(l.subtitle)}</div>` : '';
            const status = l.statusHtml || '';

            const actionsHtml = (opts.actions || [])
                .filter((a) => typeof a.visible !== 'function' || a.visible(node))
                .map((a) => `<button type="button" class="diten-tree-act${a.variant ? ' act-' + a.variant : ''}" data-tree-act="${esc(a.key)}" title="${esc(a.title || '')}" aria-label="${esc(a.title || '')}">${svg(a.icon || ICONS.node)}</button>`)
                .join('');

            const childHtml = hasKids && !isCollapsed
                ? `<ul>${kids.map((c) => nodeHtml(c, level + 1)).join('')}</ul>` : '';

            return `
                <li class="diten-tree-node${isCollapsed ? ' is-collapsed' : ''}" data-tree-id="${esc(id)}">
                    <div class="diten-tree-row${selectedId === id ? ' is-selected' : ''}${filter && matches(node) ? ' is-hit' : ''}"
                         data-tree-row="${esc(id)}" tabindex="0"${draggable ? ' draggable="true"' : ''}>
                        ${twisty}
                        ${nodeIcon}
                        <div class="diten-tree-main">
                            <div class="diten-tree-title"><span class="diten-tree-name">${highlight(l.title)}</span>${code}${count}</div>
                            ${subtitle}
                        </div>
                        ${status}
                        ${actionsHtml ? `<div class="diten-tree-actions">${actionsHtml}</div>` : ''}
                    </div>
                    ${childHtml}
                </li>`;
        }

        function render() {
            const { kids, roots } = index();
            roots.forEach((r) => markVisible(r, kids));
            const visRoots = roots.filter((r) => r.__vis);

            if (!visRoots.length) {
                host.innerHTML = '';
                emptyBox?.classList.remove('d-none');
                return;
            }
            emptyBox?.classList.add('d-none');
            host.innerHTML = `<ul class="diten-tree-root">${visRoots.map((r) => nodeHtml(r, 0)).join('')}</ul>`;
        }

        // ── expand / collapse ────────────────────────────────────────────────
        function applyDefaultCollapse() {
            collapsed.clear();
            const depth = Number.isInteger(opts.expandDepth) ? opts.expandDepth : 1;
            const { kids, roots } = index();
            const walk = (node, level) => {
                const id = idOf(node);
                if ((kids.get(id) || []).length && level >= depth) collapsed.add(id);
                (kids.get(id) || []).forEach((c) => walk(c, level + 1));
            };
            roots.forEach((r) => walk(r, 0));
        }
        function expandAll() { collapsed.clear(); render(); }
        function collapseAll() {
            const { kids } = index();
            kids.forEach((_children, parentId) => collapsed.add(parentId));
            render();
        }

        // ── drag-to-reparent ─────────────────────────────────────────────────
        function clearDropHints() {
            host.querySelectorAll('.is-drop-target').forEach((el) => el.classList.remove('is-drop-target'));
            host.classList.remove('is-drop-root');
        }

        // Returns { ok:true } or { ok:false, reason }. Guards the own-subtree case
        // internally, then defers domain rules to opts.drag.canDrop.
        function evaluateDrop(dragNode, targetNode, kids) {
            if (!dragNode) return { ok: false };
            const targetId = targetNode ? idOf(targetNode) : null;
            // no-op: dropping onto current parent, or onto itself
            if (targetId === idOf(dragNode)) return { ok: false };
            if ((parentOf(dragNode) ?? null) === (targetId ?? null)) return { ok: false };
            if (targetNode && subtreeIds(idOf(dragNode), kids).has(targetId)) {
                return { ok: false, reason: 'cycle' };
            }
            if (typeof opts.drag?.canDrop === 'function') {
                const verdict = opts.drag.canDrop(dragNode, targetNode);
                if (verdict !== true) return { ok: false, reason: typeof verdict === 'string' ? verdict : 'blocked' };
            }
            return { ok: true };
        }

        function bindDrag() {
            host.addEventListener('dragstart', (e) => {
                const row = e.target.closest?.('[data-tree-row]');
                if (!row || row.getAttribute('draggable') !== 'true') return;
                dragId = row.getAttribute('data-tree-row');
                row.classList.add('is-dragging');
                try { e.dataTransfer.setData('text/plain', dragId); e.dataTransfer.effectAllowed = 'move'; } catch { /* noop */ }
            });
            host.addEventListener('dragend', () => {
                host.querySelector('.is-dragging')?.classList.remove('is-dragging');
                clearDropHints();
                dragId = null;
            });
            host.addEventListener('dragover', (e) => {
                if (dragId == null) return;
                const { kids, byId } = index();
                const dragNode = byId.get(coerceId(dragId, byId));
                const row = e.target.closest?.('[data-tree-row]');
                clearDropHints();
                if (row) {
                    const targetNode = byId.get(coerceId(row.getAttribute('data-tree-row'), byId));
                    if (evaluateDrop(dragNode, targetNode, kids).ok) {
                        e.preventDefault();
                        row.classList.add('is-drop-target');
                    }
                } else {
                    // empty area → make root
                    if (evaluateDrop(dragNode, null, kids).ok) {
                        e.preventDefault();
                        host.classList.add('is-drop-root');
                    }
                }
            });
            host.addEventListener('drop', async (e) => {
                if (dragId == null) return;
                e.preventDefault();
                const { kids, byId } = index();
                const dragNode = byId.get(coerceId(dragId, byId));
                const row = e.target.closest?.('[data-tree-row]');
                const targetNode = row ? byId.get(coerceId(row.getAttribute('data-tree-row'), byId)) : null;
                clearDropHints();
                const verdict = evaluateDrop(dragNode, targetNode, kids);
                const captured = dragId;
                dragId = null;
                host.querySelector('.is-dragging')?.classList.remove('is-dragging');
                if (!verdict.ok) {
                    if (verdict.reason && typeof opts.drag?.onReject === 'function') opts.drag.onReject(verdict.reason, dragNode, targetNode);
                    return;
                }
                if (typeof opts.drag?.onDrop === 'function') {
                    await opts.drag.onDrop(dragNode, targetNode);
                }
                void captured;
            });
        }

        // list ids may be strings or GUIDs; map lookup key must match idOf's type
        function coerceId(raw, byId) {
            if (byId.has(raw)) return raw;
            for (const key of byId.keys()) { if (String(key) === String(raw)) return key; }
            return raw;
        }

        // ── events ───────────────────────────────────────────────────────────
        host.addEventListener('click', (e) => {
            const toggle = e.target.closest('[data-tree-toggle]');
            if (toggle) {
                const id = coerceId(toggle.getAttribute('data-tree-toggle'), index().byId);
                collapsed.has(id) ? collapsed.delete(id) : collapsed.add(id);
                render();
                return;
            }
            const actBtn = e.target.closest('[data-tree-act]');
            if (actBtn) {
                e.stopPropagation();
                const rowEl = actBtn.closest('[data-tree-row]');
                const { byId } = index();
                const node = byId.get(coerceId(rowEl?.getAttribute('data-tree-row'), byId));
                const action = (opts.actions || []).find((a) => a.key === actBtn.getAttribute('data-tree-act'));
                if (node && action && typeof action.handler === 'function') action.handler(node);
                return;
            }
            const row = e.target.closest('[data-tree-row]');
            if (row) { selectedId = coerceId(row.getAttribute('data-tree-row'), index().byId); render(); }
        });

        // keyboard: Enter/Space toggles; ←/→ collapse/expand the focused node
        host.addEventListener('keydown', (e) => {
            const row = e.target.closest?.('[data-tree-row]');
            if (!row) return;
            const id = coerceId(row.getAttribute('data-tree-row'), index().byId);
            if (e.key === 'Enter' || e.key === ' ') {
                if ((index().kids.get(id) || []).length) { e.preventDefault(); collapsed.has(id) ? collapsed.delete(id) : collapsed.add(id); render(); refocus(id); }
            } else if (e.key === 'ArrowLeft') { if (!collapsed.has(id)) { collapsed.add(id); render(); refocus(id); } }
            else if (e.key === 'ArrowRight') { if (collapsed.has(id)) { collapsed.delete(id); render(); refocus(id); } }
        });
        function refocus(id) { host.querySelector(`[data-tree-row="${CSS.escape(String(id))}"]`)?.focus(); }

        // ── toolbar wiring ───────────────────────────────────────────────────
        if (searchInput) {
            searchInput.placeholder = L.searchPlaceholder;
            let t;
            searchInput.addEventListener('input', (e) => {
                clearTimeout(t);
                t = setTimeout(() => { filter = e.target.value.trim().toLowerCase(); render(); }, 120);
            });
        }
        btnExpandAll?.addEventListener('click', expandAll);
        btnCollapseAll?.addEventListener('click', collapseAll);
        if (btnExpandAll) btnExpandAll.querySelector('[data-tree-label]') && (btnExpandAll.querySelector('[data-tree-label]').textContent = L.expandAll);
        if (btnCollapseAll) btnCollapseAll.querySelector('[data-tree-label]') && (btnCollapseAll.querySelector('[data-tree-label]').textContent = L.collapseAll);

        if (addBtn) {
            if (opts.addLabel && typeof opts.onAdd === 'function') {
                const labelSpan = addBtn.querySelector('[data-tree-add-label]');
                if (labelSpan) labelSpan.textContent = opts.addLabel;
                addBtn.classList.remove('d-none');
                addBtn.addEventListener('click', () => opts.onAdd());
            } else {
                addBtn.classList.add('d-none');
            }
        }
        if (emptyBox) {
            const emptyTitle = emptyBox.querySelector('[data-tree-empty-title]');
            const emptyHint = emptyBox.querySelector('[data-tree-empty-hint]');
            if (emptyTitle) emptyTitle.textContent = L.empty;
            if (emptyHint && L.emptyHint) emptyHint.textContent = L.emptyHint;
        }

        if (opts.drag?.enabled) bindDrag();

        // ── init ─────────────────────────────────────────────────────────────
        applyDefaultCollapse();
        render();

        // ── public API ───────────────────────────────────────────────────────
        return {
            setData(next) { data = Array.isArray(next) ? next.slice() : []; applyDefaultCollapse(); render(); },
            refresh() { render(); },
            expandAll, collapseAll,
            setFilter(text) { filter = String(text || '').trim().toLowerCase(); if (searchInput) searchInput.value = text || ''; render(); },
            getSelectedId() { return selectedId; },
            destroy() { host.innerHTML = ''; }
        };
    }

    return { create };
})();
