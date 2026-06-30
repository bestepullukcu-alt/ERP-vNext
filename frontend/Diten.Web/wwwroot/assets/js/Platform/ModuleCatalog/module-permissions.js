/**
 * Module Catalog Details / Permissions tab (MC-5).
 * Read-only: aggregates the module's page RequiredPermissions + action PermissionKeys
 * (from the existing page/action descriptor proxies) into one list.
 */
'use strict';

(function () {
    const root = document.getElementById('module-permissions-root');
    if (!root) return;

    const moduleCode = root.dataset.moduleCode || '';
    const labelPage = root.dataset.labelPage || 'Page';
    const labelAction = root.dataset.labelAction || 'Action';

    const tabButton = document.getElementById('module-permissions-tab-button');
    const loadingEl = document.getElementById('module-permissions-loading');
    const errorEl = document.getElementById('module-permissions-error');
    const emptyEl = document.getElementById('module-permissions-empty');
    const tableWrap = document.getElementById('module-permissions-table-wrap');
    const tbody = document.getElementById('module-permissions-tbody');

    let loaded = false;

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));

    const show = (el) => el?.classList.remove('d-none');
    const hide = (el) => el?.classList.add('d-none');

    const unwrapList = (payload) => {
        if (Array.isArray(payload)) return payload;
        const data = payload?.data ?? payload?.Data;
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.items ?? data?.Items)) return data.items ?? data.Items;
        return [];
    };

    const fetchJson = async (url) => {
        const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!response.ok) throw new Error(`Request failed: ${response.status}`);
        return response.json();
    };

    const collectRows = async () => {
        const pagesPayload = await fetchJson(`/Platform/ModuleCatalog/api/${encodeURIComponent(moduleCode)}/pages`);
        const pages = unwrapList(pagesPayload);
        const rows = [];

        // Per-page action fetches run in parallel; one page's failure must not drop the rest.
        const actionFetches = pages.map(async (page) => {
            const pageId = page.id || page.Id;
            const pageName = page.displayName || page.DisplayName || page.pageCode || page.PageCode || '';
            const pagePermission = page.requiredPermission || page.RequiredPermission;
            if (pagePermission) {
                rows.push({ key: pagePermission, source: pageName, type: labelPage });
            }
            if (!pageId) return;
            try {
                const actionsPayload = await fetchJson(`/Platform/ModuleCatalog/api/pages/${encodeURIComponent(pageId)}/actions`);
                unwrapList(actionsPayload).forEach((action) => {
                    const key = action.permissionKey || action.PermissionKey;
                    if (!key) return;
                    const actionName = action.displayName || action.DisplayName || action.actionCode || action.ActionCode || '';
                    rows.push({ key, source: `${pageName} › ${actionName}`, type: labelAction });
                });
            } catch (error) {
                console.warn('[ModulePermissions] action load failed for page', pageId, error);
            }
        });

        await Promise.all(actionFetches);
        return rows;
    };

    const render = (rows) => {
        if (!rows.length) {
            show(emptyEl);
            return;
        }
        rows.sort((a, b) => a.key.localeCompare(b.key) || a.type.localeCompare(b.type));
        tbody.innerHTML = rows.map((row) => `
            <tr>
                <td class="ps-3"><code>${escapeHtml(row.key)}</code></td>
                <td>${escapeHtml(row.source)}</td>
                <td class="pe-3"><span class="badge bg-label-secondary">${escapeHtml(row.type)}</span></td>
            </tr>`).join('');
        show(tableWrap);
    };

    const load = async () => {
        if (loaded) return;
        loaded = true;
        hide(errorEl); hide(emptyEl); hide(tableWrap);
        show(loadingEl);
        try {
            render(await collectRows());
        } catch (error) {
            console.error('[ModulePermissions] load failed', error);
            loaded = false; // allow a retry on next tab open
            show(errorEl);
        } finally {
            hide(loadingEl);
        }
    };

    // Lazy-load when the Permissions tab is first shown.
    if (tabButton) {
        tabButton.addEventListener('shown.bs.tab', load);
        // If the tab is already active on load (deep link), load immediately.
        if (tabButton.classList.contains('active')) load();
    } else {
        load();
    }
})();
