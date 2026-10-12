/**
 * MOD-0173-FU02 L-1 Location tree + detail (LOC2) — a custom component, NOT a DataTable (pack §1/§11).
 *
 * TREE (WAI-ARIA tree pattern): role="tree" / "treeitem" / "group", aria-level, aria-expanded on nodes that can have
 * children, aria-selected on the chosen one, one roving tabindex. Keyboard: ↑ ↓ move, → expand / first child,
 * ← collapse / parent, Home / End, Enter / Space select. Indentation uses the logical margin-inline-start, so the
 * Arabic (RTL) shell mirrors it without a second rule.
 *
 * LAZY (LOC-08): the company's Sites first; the children of ONE node when it is expanded (/LocationTree/children).
 * Above 500 children the node does not draw them: it hands over to the L-2 list filtered by that parent. Archived
 * nodes appear only with "show archived". While a node loads, its group shows rows shaped like the rows to come.
 *
 * DETAIL: fields, status, breadcrumb, stock presence (yes / no — K-10, never a quantity) + "View in stock list",
 * children, change history (K-11). Actions by permission — absent, not disabled, when not allowed (UAS-001 §6):
 *   newLocation (toolbar / empty state) · edit · addChild · activate / deactivate / archive · bulkBins (Zone /
 *   StorageLocation) — inventory.locations.manage, through the shared form.js / bulk-bins.js;
 *   locationHistory — inventory.locations.read; viewStock — inventory.stock.read.
 *
 * Same-origin MVC proxies only (/LocationTree/children, /Locations/…): never 5067, /api/locations or the Gateway.
 */
'use strict';

const LocationTree = (function () {
    const ui = window.LocationUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const esc = (value) => ui.esc(value);
    const treeEl = byId('locationTree');

    const canManage = () => ui.can('inventory.locations.manage') && !!byId('formLocation');
    const canSeeStock = () => ui.can('inventory.stock.read');

    /** id → { data, level, parentId, children: null | id[], expanded, tooMany, total } */
    const nodes = new Map();
    let rootIds = [];
    let selectedId = null;
    let includeArchived = false;
    let current = null; // the selected node's detail payload
    let historyPage = 1;

    const dateFormat = new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: 'short', timeStyle: 'short' });
    const formatDate = (value) => {
        const date = value ? new Date(value) : null;
        return date && !Number.isNaN(date.getTime()) ? dateFormat.format(date) : String(value || '');
    };

    const childrenUrl = (parentId) => {
        const query = new URLSearchParams({ includeArchived: String(includeArchived) });
        if (parentId) query.set('parentId', parentId);
        return `/LocationTree/children?${query.toString()}`;
    };
    const listUrl = (node) => `/Locations?${new URLSearchParams({ parentId: node.data.id, parentLabel: node.data.code || '' }).toString()}`;

    // ── tree rendering ──────────────────────────────────────────────────────────────────────────────────────
    const itemOf = (id) => treeEl?.querySelector(`li[role="treeitem"][data-id="${CSS.escape(id)}"]`);

    const rowHtml = (node) => {
        const d = node.data;
        const expandable = d.canHaveChildren && !(node.children && node.children.length === 0 && !node.tooMany);
        const toggle = expandable
            ? `<span class="loc-tree-toggle d-inline-flex" data-toggle="1" aria-hidden="true"><i class="bx ${node.expanded ? 'bx-chevron-down' : 'bx-chevron-right'}"></i></span>`
            : '<span class="d-inline-block" style="width: 1em;" aria-hidden="true"></span>';
        const muted = d.status !== 'Active' ? ' opacity-75' : '';
        return `<div class="loc-tree-row d-flex align-items-center gap-2 rounded px-2 py-1${muted}" style="margin-inline-start: ${(node.level * 1.25).toFixed(2)}rem; cursor: pointer;">`
            + toggle
            + `<i class="${ui.typeIcon[d.type] || 'bx bx-map'} text-muted" aria-hidden="true"></i>`
            + `<span class="fw-medium font-monospace">${esc(d.code)}</span>`
            + `<span class="text-muted small text-truncate">${esc(d.name)}</span>`
            + (d.status !== 'Active' ? ` ${ui.statusBadge(d.status)}` : '')
            + '</div>';
    };

    const createItem = (node) => {
        const li = document.createElement('li');
        li.setAttribute('role', 'treeitem');
        li.dataset.id = node.data.id;
        li.setAttribute('aria-level', String(node.level + 1));
        li.setAttribute('aria-selected', String(node.data.id === selectedId));
        li.setAttribute('aria-label', `${node.data.code} ${node.data.name} — ${ui.typeText(node.data.type)}, ${ui.statusText(node.data.status)}`);
        li.tabIndex = -1;
        if (node.data.canHaveChildren) li.setAttribute('aria-expanded', String(!!node.expanded));
        li.innerHTML = rowHtml(node);
        return li;
    };

    const refreshItem = (id) => {
        const li = itemOf(id);
        const node = nodes.get(id);
        if (!li || !node) return;
        const group = li.querySelector(':scope > ul[role="group"]');
        li.innerHTML = rowHtml(node);
        if (group) li.appendChild(group);
        const expandable = node.data.canHaveChildren && !(node.children && node.children.length === 0 && !node.tooMany);
        if (expandable) li.setAttribute('aria-expanded', String(!!node.expanded));
        else li.removeAttribute('aria-expanded');
        markSelected();
    };

    const skeletonRows = (level) => {
        const pad = ((level + 1) * 1.25).toFixed(2);
        return [70, 60, 50].map((w) => `<li role="none" aria-hidden="true"><div class="shimmer skeleton-row" style="width: ${w}%; margin-inline-start: ${pad}rem;"></div></li>`).join('');
    };

    const addNodes = (items, parentId, level) => items.map((item) => {
        const existing = nodes.get(item.id);
        nodes.set(item.id, {
            data: item,
            level,
            parentId,
            children: existing?.children ?? null,
            expanded: false,
            tooMany: false,
            total: 0
        });
        return item.id;
    });

    const renderRoots = () => {
        treeEl.innerHTML = '';
        rootIds.forEach((id) => treeEl.appendChild(createItem(nodes.get(id))));
        const empty = rootIds.length === 0;
        byId('locationTreeEmpty')?.classList.toggle('d-none', !empty);
        treeEl.classList.toggle('d-none', empty);
        const first = treeEl.querySelector('li[role="treeitem"]');
        if (first && !treeEl.querySelector('li[role="treeitem"][tabindex="0"]')) first.tabIndex = 0;
        markSelected();
    };

    const treeError = (message) => {
        const box = byId('locationTreeError');
        if (!box) return;
        box.innerHTML = message ? ui.withReference(message.message, message.correlationId) : '';
        box.classList.toggle('d-none', !message);
    };

    const loadRoots = async () => {
        const reply = await ui.call(childrenUrl(null));
        byId('locationTreeSkeleton')?.classList.add('d-none');
        if (!reply.ok) {
            treeError(reply);
            treeEl.classList.add('d-none');
            return false;
        }
        treeError(null);
        nodes.clear();
        rootIds = addNodes(reply.items || [], null, 0);
        renderRoots();
        return true;
    };

    const expand = async (id) => {
        const node = nodes.get(id);
        const li = itemOf(id);
        if (!node || !li || !node.data.canHaveChildren) return;
        let group = li.querySelector(':scope > ul[role="group"]');
        if (!group) {
            group = document.createElement('ul');
            group.setAttribute('role', 'group');
            group.className = 'list-unstyled mb-0';
            li.appendChild(group);
        }
        node.expanded = true;
        group.classList.remove('d-none');

        if (node.children === null) {
            group.innerHTML = skeletonRows(node.level);
            refreshItem(id);
            const reply = await ui.call(childrenUrl(id));
            if (!reply.ok) {
                group.innerHTML = '';
                node.expanded = false;
                treeError(reply);
                refreshItem(id);
                return;
            }
            node.tooMany = !!reply.tooMany;
            node.total = reply.total || 0;
            node.children = node.tooMany ? [] : addNodes(reply.items || [], id, node.level + 1);
            group.innerHTML = '';
            if (node.tooMany) {
                // > 500 children: the tree hands over to the list (LOC-08).
                const li2 = document.createElement('li');
                li2.setAttribute('role', 'treeitem');
                li2.setAttribute('aria-level', String(node.level + 2));
                li2.dataset.href = listUrl(node);
                li2.tabIndex = -1;
                li2.innerHTML = `<div class="small text-muted px-2 py-1" style="margin-inline-start: ${((node.level + 1) * 1.25).toFixed(2)}rem;">`
                    + `${esc(ui.format(L().TooManyChildren, node.total))} <a href="${esc(listUrl(node))}">${esc(L().OpenInList || '')}</a></div>`;
                group.appendChild(li2);
            } else {
                node.children.forEach((childId) => group.appendChild(createItem(nodes.get(childId))));
            }
            if (current?.location?.locationId === id) renderChildrenInfo(current.location);
        }
        refreshItem(id);
    };

    const collapse = (id) => {
        const node = nodes.get(id);
        const li = itemOf(id);
        if (!node || !li) return;
        node.expanded = false;
        li.querySelector(':scope > ul[role="group"]')?.classList.add('d-none');
        refreshItem(id);
    };

    const toggle = (id) => (nodes.get(id)?.expanded ? collapse(id) : expand(id));

    const markSelected = () => {
        treeEl?.querySelectorAll('li[role="treeitem"]').forEach((li) => {
            const on = li.dataset.id === selectedId;
            li.setAttribute('aria-selected', String(on));
            li.querySelector(':scope > .loc-tree-row')?.classList.toggle('bg-label-primary', on);
        });
    };

    const visibleItems = () => Array.from(treeEl.querySelectorAll('li[role="treeitem"]'))
        .filter((li) => !li.closest('ul[role="group"].d-none'));

    const focusItem = (li) => {
        if (!li) return;
        treeEl.querySelectorAll('li[role="treeitem"][tabindex="0"]').forEach((el) => { el.tabIndex = -1; });
        li.tabIndex = 0;
        li.focus();
    };

    // ── selection + detail ──────────────────────────────────────────────────────────────────────────────────
    const detailState = (which) => {
        byId('locationDetailHint')?.classList.toggle('d-none', which !== 'hint');
        byId('locationDetailSkeleton')?.classList.toggle('d-none', which !== 'loading');
        byId('locationDetailBody')?.classList.toggle('d-none', which !== 'body');
        byId('locationDetailError')?.classList.toggle('d-none', which !== 'error');
    };

    const select = async (id) => {
        if (!id) return;
        selectedId = id;
        markSelected();
        await loadDetail(id);
    };

    const yesNo = (value) => value ? `<span class="badge bg-label-warning">${esc(L().StockYes)}</span>` : `<span class="badge bg-label-success">${esc(L().StockNo)}</span>`;

    const detailActions = (d) => {
        const manage = canManage();
        const archived = d.status === 'Archived';
        return [
            { key: 'edit', visible: manage && !archived, icon: 'bx bx-edit', text: L().ActionEdit, css: 'btn-label-primary' },
            { key: 'addChild', visible: manage && !archived && d.type !== 'Bin', icon: 'bx bx-plus', text: L().ActionAddChild, css: 'btn-label-primary' },
            { key: 'bulkBins', visible: manage && d.status === 'Active' && (d.type === 'Zone' || d.type === 'StorageLocation'), icon: 'bx bx-grid', text: L().ActionBulkBins, css: 'btn-label-primary' },
            { key: 'activate', visible: manage && d.status === 'Inactive', icon: 'bx bx-play-circle', text: L().ActionActivate, css: 'btn-label-success' },
            { key: 'deactivate', visible: manage && d.status === 'Active', icon: 'bx bx-pause-circle', text: L().ActionDeactivate, css: 'btn-label-warning' },
            { key: 'archive', visible: manage && !archived, icon: 'bx bx-archive', text: L().ActionArchive, css: 'btn-label-danger' },
            { key: 'locationHistory', visible: true, icon: 'bx bx-history', text: L().ActionHistory, css: 'btn-label-secondary' },
            { key: 'viewStock', visible: canSeeStock(), icon: 'bx bx-package', text: L().ActionViewStock, css: 'btn-label-secondary' }
        ].filter((action) => action.visible);
    };

    const fillDetail = (payload) => {
        const d = payload.location || {};
        byId('ldIcon').className = `${ui.typeIcon[d.type] || 'bx bx-map'} fs-4`;
        byId('ldName').textContent = d.name || '-';
        byId('ldCode').textContent = d.code || '-';
        byId('ldTypeBadge').innerHTML = ui.typeBadge(d.type);
        byId('ldStatusBadge').innerHTML = ui.statusBadge(d.status);

        const steps = payload.path || [];
        byId('ldPath').innerHTML = steps.map((step, i) => i === steps.length - 1
            ? `<li class="breadcrumb-item active font-monospace" aria-current="page">${esc(step.code || step.id)}</li>`
            : `<li class="breadcrumb-item font-monospace"><a href="javascript:void(0);" data-select-node="${esc(step.id)}">${esc(step.code || step.id)}</a></li>`).join('');

        byId('ldActions').innerHTML = detailActions(d).map((a) =>
            `<button type="button" class="btn btn-sm ${a.css}" data-location-action="${a.key}" title="${esc(a.text)}"><i class="${a.icon} me-1" aria-hidden="true"></i>${esc(a.text)}</button>`).join('');

        const notes = [];
        if (d.status === 'Inactive') notes.push(L().InactiveNote);
        if (d.status === 'Archived') notes.push(L().ArchivedNote);
        if (d.operatingMode === 'external-integrated') notes.push(L().ExternalNote);
        const note = byId('ldNote');
        note.innerHTML = notes.map((n) => `<div>${esc(n || '')}</div>`).join('');
        note.classList.toggle('d-none', !notes.length);

        byId('ldFieldCode').textContent = d.code || '-';
        byId('ldFieldName').textContent = d.name || '-';
        byId('ldFieldType').textContent = ui.typeText(d.type) || '-';
        const stockable = d.type === 'Bin' || d.type === 'StorageLocation';
        byId('ldStockableLabel').classList.toggle('d-none', !stockable);
        byId('ldFieldStockable').classList.toggle('d-none', !stockable);
        byId('ldFieldStockable').textContent = d.isStockable ? (L().StockableYes || '') : (L().StockableNo || '');
        const hasMode = d.type === 'Site' || d.type === 'Warehouse';
        byId('ldModeLabel').classList.toggle('d-none', !hasMode);
        byId('ldFieldMode').classList.toggle('d-none', !hasMode);
        byId('ldFieldMode').textContent = ui.modeText(d.operatingMode) || '-';
        byId('ldFieldOrgUnit').textContent = d.orgUnitRef || '-';
        byId('ldFieldCostCenter').textContent = d.costCenterCode || '-';
        resolveOrgUnit(d.orgUnitRef);

        const presence = payload.presence;
        byId('ldStockSelf').innerHTML = presence ? yesNo(presence.hasStock) : '-';
        byId('ldStockBelow').innerHTML = presence ? yesNo(presence.subtreeHasStock) : '-';
        byId('ldStockUnknown').classList.toggle('d-none', !!presence);

        renderChildrenInfo(d);
    };

    // "Child locations": the count once the node's children are loaded (they load on expand), and the list link.
    const renderChildrenInfo = (d) => {
        const node = nodes.get(d.locationId);
        const children = byId('ldChildren');
        if (!children) return;
        if (d.type === 'Bin') {
            children.textContent = L().NoChildrenForBin || '';
            return;
        }
        const count = node?.tooMany ? node.total : node?.children ? node.children.length : null;
        children.innerHTML = (count === null ? '' : `<span class="me-2">${esc(ui.format(L().ChildrenCount, count))}</span>`)
            + `<a href="${esc(`/Locations?${new URLSearchParams({ parentId: d.locationId, parentLabel: d.code || '' }).toString()}`)}">${esc(L().ShowInList || '')}</a>`;
    };

    // The org-unit NAME for the panel (the stored value is the unit id — a soft link, LOC-11).
    let orgUnitNames = null;
    const resolveOrgUnit = async (id) => {
        if (!id) return;
        if (!orgUnitNames) {
            const reply = await ui.call('/Locations/orgunits');
            orgUnitNames = new Map((reply.ok ? reply.items || [] : []).map((item) => [item.value, item.text]));
        }
        if (orgUnitNames.has(id) && current?.location?.orgUnitRef === id) byId('ldFieldOrgUnit').textContent = orgUnitNames.get(id);
    };

    const fieldText = (field) => (field && L()['HistField_' + field]) || field || '';
    const actionText = (action) => (action && L()['HistAction_' + action]) || action || '';
    const valueText = (field, value) => {
        if (value === null || value === undefined || value === '') return '—';
        if (field === 'status') return ui.statusText(value);
        if (field === 'isStockable') return value === 'true' ? (L().StockableYes || value) : value === 'false' ? (L().StockableNo || value) : value;
        if (field === 'operatingMode') return ui.modeText(value) || value;
        if (field === 'parentId') return nodes.get(value)?.data?.code || value;
        return value;
    };

    const loadHistory = async (id, page) => {
        const reply = await ui.call(`/Locations/history/${encodeURIComponent(id)}?page=${page}`);
        const body = byId('ldHistory');
        if (page === 1) body.innerHTML = '';
        if (!reply.ok) {
            body.insertAdjacentHTML('beforeend', `<tr><td colspan="4" class="text-danger small">${ui.withReference(reply.message, reply.correlationId)}</td></tr>`);
            return;
        }
        (reply.items || []).forEach((change) => {
            const what = change.field ? `${esc(actionText(change.action))} · ${esc(fieldText(change.field))}` : esc(actionText(change.action));
            const delta = change.field
                ? `<span class="text-muted">${esc(valueText(change.field, change.oldValue))}</span> → <span class="fw-medium">${esc(valueText(change.field, change.newValue))}</span>`
                : '';
            body.insertAdjacentHTML('beforeend', `<tr><td class="text-nowrap small">${esc(formatDate(change.occurredAt))}</td>`
                + `<td class="small font-monospace text-truncate" style="max-width: 9rem;" title="${esc(change.actorUserId)}">${esc(change.actorUserId)}</td>`
                + `<td class="small">${what}</td><td class="small">${delta}</td></tr>`);
        });
        const shown = body.querySelectorAll('tr').length;
        byId('ldHistoryEmpty').classList.toggle('d-none', shown > 0);
        byId('ldHistoryMore').classList.toggle('d-none', shown >= (reply.total || 0));
        historyPage = page;
    };

    const loadDetail = async (id) => {
        detailState('loading');
        const reply = await ui.call(`/Locations/node/${encodeURIComponent(id)}`);
        if (selectedId !== id) return; // a later selection won
        if (!reply.ok || !reply.location) {
            const box = byId('locationDetailError');
            box.innerHTML = ui.withReference(reply.message, reply.correlationId);
            detailState('error');
            return;
        }
        current = reply;
        fillDetail(reply);
        detailState('body');
        await loadHistory(id, 1);
    };

    // ── reveal (deep link, after a write) ───────────────────────────────────────────────────────────────────
    const reveal = async (id) => {
        const reply = await ui.call(`/Locations/node/${encodeURIComponent(id)}`);
        if (reply.ok && Array.isArray(reply.path)) {
            for (const step of reply.path.slice(0, -1)) {
                if (nodes.has(step.id) && !nodes.get(step.id).expanded) await expand(step.id);
            }
        }
        if (nodes.has(id)) {
            const li = itemOf(id);
            li?.scrollIntoView({ block: 'nearest' });
            focusItem(li);
        }
        await select(id);
    };

    const refresh = async (selectId) => {
        const expanded = Array.from(nodes.values()).filter((n) => n.expanded).sort((a, b) => a.level - b.level).map((n) => n.data.id);
        if (!(await loadRoots())) return;
        for (const id of expanded) {
            if (nodes.has(id)) await expand(id);
        }
        const target = selectId || selectedId;
        if (target) await reveal(target);
    };

    // ── actions ─────────────────────────────────────────────────────────────────────────────────────────────
    const actionHandlers = {
        newLocation: () => window.LocationForm?.open({ mode: 'create', siteOnly: true, onSaved: (loc) => refresh(loc?.locationId) }),
        edit: (d) => window.LocationForm?.open({ mode: 'edit', id: d.locationId, onSaved: () => refresh(d.locationId) }),
        addChild: (d) => window.LocationForm?.open({
            mode: 'child',
            parent: { id: d.locationId, type: d.type, code: d.code, name: d.name },
            onSaved: async (loc) => {
                const node = nodes.get(d.locationId);
                if (node) { node.children = null; node.expanded = true; }
                await refresh(loc?.locationId || d.locationId);
            }
        }),
        activate: (d) => window.LocationForm?.changeStatus({ id: d.locationId }, 'Active', { onDone: () => refresh(d.locationId) }),
        deactivate: (d) => window.LocationForm?.changeStatus({ id: d.locationId }, 'Inactive', { onDone: () => refresh(d.locationId) }),
        archive: (d) => window.LocationForm?.changeStatus({ id: d.locationId }, 'Archived', { onDone: () => refresh(d.locationId) }),
        bulkBins: (d) => window.LocationBulkBins?.open({
            target: { id: d.locationId, code: d.code, name: d.name, type: d.type },
            onDone: () => {
                const node = nodes.get(d.locationId);
                if (node) node.children = null;
                refresh(d.locationId);
            }
        }),
        locationHistory: (d) => {
            byId('history')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            loadHistory(d.locationId, 1);
        },
        viewStock: (d) => { window.location.href = ui.stockListUrl(d.locationId); }
    };

    const onAction = (event) => {
        const trigger = event.target.closest('[data-location-action]');
        if (!trigger) return;
        const key = trigger.getAttribute('data-location-action');
        const handler = actionHandlers[key];
        if (!handler) return;
        event.preventDefault();
        if (key === 'newLocation') { handler(); return; }
        if (current?.location) handler(current.location);
    };

    // ── keyboard (WAI-ARIA tree) ────────────────────────────────────────────────────────────────────────────
    const onKey = async (event) => {
        const li = event.target.closest('li[role="treeitem"]');
        if (!li) return;
        const items = visibleItems();
        const index = items.indexOf(li);
        const id = li.dataset.id;
        const node = id ? nodes.get(id) : null;
        switch (event.key) {
            case 'ArrowDown': event.preventDefault(); focusItem(items[index + 1]); break;
            case 'ArrowUp': event.preventDefault(); focusItem(items[index - 1]); break;
            case 'Home': event.preventDefault(); focusItem(items[0]); break;
            case 'End': event.preventDefault(); focusItem(items[items.length - 1]); break;
            case 'ArrowRight':
                event.preventDefault();
                if (node?.data.canHaveChildren && !node.expanded) await expand(id);
                else if (node?.expanded) focusItem(li.querySelector(':scope > ul[role="group"] > li[role="treeitem"]'));
                break;
            case 'ArrowLeft':
                event.preventDefault();
                if (node?.expanded) collapse(id);
                else focusItem(li.parentElement?.closest('li[role="treeitem"]'));
                break;
            case 'Enter':
            case ' ':
                event.preventDefault();
                if (li.dataset.href) window.location.href = li.dataset.href;
                else if (id) select(id);
                break;
            default:
                break;
        }
    };

    const onClick = async (event) => {
        const li = event.target.closest('li[role="treeitem"]');
        if (!li || !li.dataset.id || event.target.closest('a')) return;
        focusItem(li);
        if (event.target.closest('[data-toggle]')) { await toggle(li.dataset.id); return; }
        await select(li.dataset.id);
        const node = nodes.get(li.dataset.id);
        if (node?.data.canHaveChildren && !node.expanded) await expand(li.dataset.id);
    };

    const init = async () => {
        if (!treeEl) return; // no company yet: nothing is drawn and nothing is asked (AC-14)
        treeEl.addEventListener('keydown', onKey);
        treeEl.addEventListener('click', onClick);
        document.addEventListener('click', onAction);
        byId('locationDetail')?.addEventListener('click', (event) => {
            const step = event.target.closest('[data-select-node]');
            if (!step) return;
            event.preventDefault();
            reveal(step.getAttribute('data-select-node'));
        });
        byId('ldHistoryMore')?.addEventListener('click', () => current?.location && loadHistory(current.location.locationId, historyPage + 1));
        byId('locationShowArchived')?.addEventListener('change', (event) => {
            includeArchived = !!event.target.checked;
            refresh(selectedId);
        });

        if (!(await loadRoots())) return;
        // /LocationTree?locationId=… (L-2 "History", the form after a write): open the tree down to that node.
        const params = new URLSearchParams(window.location.search);
        const deepLink = params.get('locationId');
        if (deepLink) {
            await reveal(deepLink);
            if (window.location.hash === '#history') byId('history')?.scrollIntoView({ block: 'start' });
        } else if (rootIds.length === 1) {
            await expand(rootIds[0]);
        }
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => LocationTree.init());
