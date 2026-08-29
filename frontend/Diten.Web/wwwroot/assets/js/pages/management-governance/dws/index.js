(function () {
    'use strict';

    const root = document.getElementById('dws-workspace');
    if (!root) return;

    const localizationSource = document.getElementById('dws-l10n');
    let localizedValues = window.DwsL10n || {};
    if (localizationSource) {
        try {
            localizedValues = JSON.parse(localizationSource.textContent || '{}');
        } catch {
            localizedValues = window.DwsL10n || {};
        }
    }
    const L = Object.fromEntries(Object.entries(localizedValues).map(([key, value]) => [
        key.charAt(0).toUpperCase() + key.slice(1), value
    ]));
    const enabled = root.dataset.gatewayEnabled === 'true';
    const endpoint = '/management-governance/delivery-execution/structures/api';
    const byId = (id) => document.getElementById(id);
    const value = (object, key) => object?.[key] ?? object?.[key.charAt(0).toUpperCase() + key.slice(1)];
    const state = byId('dws-state');
    let currentNodes = [];

    const setState = (kind, title, detail, correlationId) => {
        const styles = {
            loading: 'alert-info', empty: 'alert-secondary', success: 'alert-success',
            forbidden: 'alert-danger', missing: 'alert-warning', conflict: 'alert-warning',
            unavailable: 'alert-danger', offline: 'alert-danger'
        };
        state.className = `alert ${styles[kind] || 'alert-secondary'} d-flex align-items-start gap-2`;
        byId('dws-state-title').textContent = title;
        byId('dws-state-detail').textContent = correlationId
            ? `${detail} (${L.CorrelationId}: ${correlationId})`
            : detail;
    };

    const clearWorkspace = () => {
        byId('dws-tree').replaceChildren();
        ['dws-node-code', 'dws-node-title', 'dws-node-description', 'dws-node-order']
            .forEach((id) => { byId(id).textContent = '—'; });
        currentNodes = [];
    };

    const responseState = (status, payload) => {
        const correlation = value(payload, 'correlation_id') || value(payload, 'correlationId');
        if (status === 401) return ['forbidden', L.Unauthorized, L.Unauthorized, correlation];
        if (status === 403) return ['forbidden', L.Forbidden, L.Forbidden, correlation];
        if (status === 404) return ['missing', L.NotFound, L.NotFound, correlation];
        if (status === 409) return ['conflict', L.Conflict, L.Conflict, correlation];
        if (status === 503) return ['unavailable', L.Unavailable, L.Unavailable, correlation];
        return ['offline', L.Offline, L.Offline, correlation];
    };

    const getJson = async (path) => {
        if (!enabled) {
            setState('unavailable', L.Unavailable, L.Unavailable);
            clearWorkspace();
            return null;
        }

        try {
            const response = await fetch(`${endpoint}/${path}`, {
                method: 'GET', credentials: 'same-origin',
                headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) {
                setState(...responseState(response.status, payload));
                clearWorkspace();
                return null;
            }
            return value(payload, 'data') ?? payload;
        } catch {
            setState('offline', L.Offline, L.Offline);
            clearWorkspace();
            return null;
        }
    };

    const nodeValue = (node, key) => value(node, key);
    const selectNode = (element, node) => {
        root.querySelectorAll('[role="treeitem"]').forEach((item) => item.setAttribute('aria-selected', 'false'));
        element.setAttribute('aria-selected', 'true');
        byId('dws-node-code').textContent = nodeValue(node, 'code') || '—';
        byId('dws-node-title').textContent = nodeValue(node, 'title') || '—';
        byId('dws-node-description').textContent = nodeValue(node, 'description') || '—';
        byId('dws-node-order').textContent = String(nodeValue(node, 'siblingOrder') ?? '—');
    };

    const visibleItems = () => [...root.querySelectorAll('[role="treeitem"]')]
        .filter((item) => item.offsetParent !== null || getComputedStyle(item).display !== 'none');

    const focusRelative = (element, offset) => {
        const items = visibleItems();
        const next = items[Math.max(0, Math.min(items.length - 1, items.indexOf(element) + offset))];
        if (next) next.focus();
    };

    const buildNode = (node, childrenByParent) => {
        const logicalId = String(nodeValue(node, 'logicalNodeId'));
        const children = childrenByParent.get(logicalId) || [];
        const wrapper = document.createElement('div');
        const item = document.createElement('div');
        item.className = 'dws-tree-item d-flex align-items-center gap-1';
        item.setAttribute('role', 'treeitem');
        item.setAttribute('tabindex', currentNodes[0] === node ? '0' : '-1');
        item.setAttribute('aria-selected', 'false');
        item.dataset.nodeId = logicalId;

        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'dws-tree-toggle';
        toggle.tabIndex = -1;
        toggle.setAttribute('aria-hidden', 'true');
        const icon = document.createElement('i');
        icon.className = children.length ? 'bx bx-chevron-down' : 'bx bx-minus';
        toggle.append(icon);
        item.append(toggle);

        const label = document.createElement('span');
        label.textContent = `${nodeValue(node, 'code')} · ${nodeValue(node, 'title')}`;
        item.append(label);
        wrapper.append(item);

        let group = null;
        if (children.length) {
            item.setAttribute('aria-expanded', 'true');
            group = document.createElement('div');
            group.className = 'dws-tree-children';
            group.setAttribute('role', 'group');
            children.forEach((child) => group.append(buildNode(child, childrenByParent)));
            wrapper.append(group);
            toggle.addEventListener('click', () => {
                const expanded = item.getAttribute('aria-expanded') === 'true';
                item.setAttribute('aria-expanded', String(!expanded));
                group.hidden = expanded;
                icon.className = expanded ? 'bx bx-chevron-right' : 'bx bx-chevron-down';
            });
        }

        item.addEventListener('click', () => selectNode(item, node));
        item.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowDown') { event.preventDefault(); focusRelative(item, 1); }
            if (event.key === 'ArrowUp') { event.preventDefault(); focusRelative(item, -1); }
            if (event.key === 'Home') { event.preventDefault(); visibleItems()[0]?.focus(); }
            if (event.key === 'End') { event.preventDefault(); visibleItems().at(-1)?.focus(); }
            if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); selectNode(item, node); }
            if (event.key === 'ArrowLeft' && group && !group.hidden) { event.preventDefault(); toggle.click(); }
            if (event.key === 'ArrowRight' && group && group.hidden) { event.preventDefault(); toggle.click(); }
        });
        return wrapper;
    };

    const renderTree = (tree) => {
        const nodes = value(tree, 'nodes') || [];
        currentNodes = nodes;
        const container = byId('dws-tree');
        container.replaceChildren();
        const byParent = new Map();
        nodes.forEach((node) => {
            const parent = nodeValue(node, 'parentLogicalNodeId');
            const key = parent ? String(parent) : '';
            if (!byParent.has(key)) byParent.set(key, []);
            byParent.get(key).push(node);
        });
        byParent.forEach((items) => items.sort((a, b) =>
            Number(nodeValue(a, 'siblingOrder')) - Number(nodeValue(b, 'siblingOrder')) ||
            String(nodeValue(a, 'code')).localeCompare(String(nodeValue(b, 'code')))));
        (byParent.get('') || []).forEach((node) => container.append(buildNode(node, byParent)));

        const revision = value(tree, 'revisionNumber');
        const sealed = Boolean(value(tree, 'isSealed'));
        byId('dws-revision-badge').textContent = `${L.RevisionNumber || ''} ${revision} · ${sealed ? L.Sealed : L.Working}`;
        if (!nodes.length) setState('empty', L.Empty, L.Empty);
    };

    const structureId = () => byId('dws-structure-id').value.trim();
    const revisionSuffix = () => {
        const revision = byId('dws-revision-number').value;
        return revision ? `?revisionNumber=${encodeURIComponent(revision)}` : '';
    };

    byId('dws-load').addEventListener('click', async () => {
        const id = structureId();
        if (!id) { setState('empty', L.Empty, L.Empty); return; }
        setState('loading', L.Loading, L.Loading);
        const tree = await getJson(`${encodeURIComponent(id)}/tree${revisionSuffix()}`);
        if (!tree) return;
        renderTree(tree);
        setState('success', L.NodeSelected, value(value(tree, 'metadata'), 'name') || L.NodeSelected);
    });

    byId('dws-validate')?.addEventListener('click', async () => {
        const id = structureId();
        if (!id) { setState('empty', L.Empty, L.Empty); return; }
        const result = await getJson(`${encodeURIComponent(id)}/validation${revisionSuffix()}`);
        if (!result) return;
        const valid = Boolean(value(result, 'isValid'));
        const issues = value(result, 'issues') || [];
        byId('dws-validation-result').textContent = valid
            ? L.ValidationValid
            : `${L.ValidationInvalid}: ${issues.map((issue) => value(issue, 'code')).join(', ')}`;
    });

    byId('dws-compare')?.addEventListener('click', async () => {
        const id = structureId();
        const left = byId('dws-compare-left').value;
        const right = byId('dws-compare-right').value;
        if (!id || !left || !right) { setState('empty', L.Empty, L.Empty); return; }
        const result = await getJson(`${encodeURIComponent(id)}/revision-comparison?left=${encodeURIComponent(left)}&right=${encodeURIComponent(right)}`);
        if (!result) return;
        const nodes = value(result, 'nodes') || [];
        const dependencies = value(result, 'dependencies') || [];
        byId('dws-comparison-result').textContent = nodes.length || dependencies.length
            ? `${L.Nodes}: ${nodes.length} · ${L.Dependencies}: ${dependencies.length}`
            : L.ComparisonEmpty;
    });

    if (!enabled) {
        setState('unavailable', L.Unavailable, L.Unavailable);
        root.querySelectorAll('button[data-permission]').forEach((button) => { button.disabled = true; });
    }
})();
