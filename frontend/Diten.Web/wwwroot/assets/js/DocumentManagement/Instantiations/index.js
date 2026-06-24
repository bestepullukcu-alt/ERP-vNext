/**
 * MOD-0028-FU05 - Structure Baseline Instantiation Wizard.
 * All calls use the same-origin MVC proxy; the browser never calls service ports directly.
 */
'use strict';

const DocumentationInstantiations = (function () {
    let L = window.L10n || {};
    let lastDryRun;
    let lastOperation;
    let publishedReleases = [];
    let legalEntityNames = {};
    let baselineTreeApi = null;
    let planTreeApi = null;
    let instancesTreeApi = null;
    let flowCorrelationId = crypto?.randomUUID ? crypto.randomUUID() : String(Date.now());

    const $ = (id) => document.getElementById(id);
    const text = (v, fallback) => (v === null || v === undefined || v === '' ? (fallback || '-') : String(v));
    const html = (v, fallback) => text(v, fallback)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    const csrf = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const isGuid = (v) => /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(v || '').trim());

    const badgeTone = (status) => {
        const s = String(status || '').toUpperCase();
        return (s === 'CREATED' || s === 'SUCCEEDED' || s === 'COMPLETED' || s === 'ACTIVE' || s === 'PROVISIONED') ? 'success'
            : (s === 'FAILED' || s === 'BLOCKED') ? 'danger'
            : (s === 'SKIPPED') ? 'warning' : 'secondary';
    };

    // Shared "empty list" illustration (folder icon + heading + subtitle), matching the designer.
    const emptyStateMarkup = (heading, subtitle) =>
        `<div class="text-center py-5">`
        + `<i class="bx bx-folder-open mb-3" style="font-size:3rem;line-height:1;color:var(--bs-secondary-color,#a7acb2);"></i>`
        + `<h6 class="mb-2">${html(heading)}</h6>`
        + `<p class="text-muted mb-0 mx-auto" style="max-width:520px;">${html(subtitle)}</p>`
        + `</div>`;

    const formData = () => {
        const data = new FormData();
        const selectionMode = document.querySelector('input[name="selectionMode"]:checked')?.value || 'FULL_TREE';
        const selectedCanonicalIds = getSelectedCanonicalIds();
        data.append('__RequestVerificationToken', csrf());
        data.append('baselineReleaseId', $('baselineReleaseId')?.value || '');
        data.append('companyId', $('companyId')?.value || '');
        data.append('plantId', $('plantId')?.value || '');
        data.append('businessUnitId', $('businessUnitId')?.value || '');
        data.append('instanceToken', $('instanceToken')?.value || '');
        data.append('correlationId', flowCorrelationId);
        data.append('selectionMode', selectionMode);
        selectedCanonicalIds.forEach((id) => data.append('selectedCanonicalIds', id));
        data.append('includeDescendants', $('includeDescendants')?.value || 'true');
        data.append('includeRequiredAncestors', $('includeRequiredAncestors')?.value || 'true');
        return data;
    };

    const api = async (url, options, behavior) => {
        const response = await fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
        const payload = await response.json().catch(() => ({}));
        if (!response.ok || payload?.isSuccessful === false) {
            const errors = Array.isArray(payload?.errors) ? payload.errors : [];
            const message = errors[0] || payload?.reason_code || L.ErrorOccurred || 'Error';
            const detail = payload?.correlation_id ? `${message} (${L.CorrelationId}: ${payload.correlation_id})` : message;
            if (!behavior?.suppressToast) {
                window.showToast?.(detail, 'error');
            }
            throw Object.assign(new Error(message), { payload, status: response.status });
        }
        return payload;
    };

    const setText = (id, value) => {
        const el = $(id);
        if (el) el.textContent = text(value, '0');
    };

    const setCorrelation = (value) => {
        flowCorrelationId = value || flowCorrelationId;
        const el = $('flowCorrelation');
        if (el) el.textContent = `${L.CorrelationId || 'Correlation'}: ${flowCorrelationId}`;
    };

    const validateInputs = () => {
        if (!$('baselineReleaseId')?.value) {
            window.showToast?.(L.MissingSelection || 'Select a published release.', 'warning');
            return false;
        }
        if (!isGuid($('companyId')?.value)) {
            window.showToast?.(L.InvalidCompany || 'Enter a valid company/legal entity id.', 'warning');
            return false;
        }
        for (const id of ['plantId', 'businessUnitId']) {
            const value = $(id)?.value;
            if (value && !isGuid(value)) {
                window.showToast?.(L.InvalidCompany || 'Enter a valid GUID.', 'warning');
                return false;
            }
        }
        if (getSelectionMode() === 'SELECTED_BRANCHES' && getSelectedCanonicalIds().length === 0) {
            window.showToast?.(L.SelectAtLeastOneBranch || 'Select at least one branch.', 'warning');
            return false;
        }
        return true;
    };

    const getSelectionMode = () => document.querySelector('input[name="selectionMode"]:checked')?.value || 'FULL_TREE';

    const getSelectedCanonicalIds = () => {
        // With jsTree, get_top_checked() returns the top-most checked nodes — i.e. the selected
        // branch roots. The backend includes descendants (includeDescendants=true) and de-dupes.
        if (baselineTreeApi && typeof baselineTreeApi.get_top_checked === 'function') {
            return baselineTreeApi.get_top_checked().filter(Boolean).map(String);
        }
        return Array.from(document.querySelectorAll('#baselineTreeSelection input[type="checkbox"]:checked'))
            .map((x) => x.value)
            .filter(Boolean);
    };

    const currentRelease = () => {
        const id = $('baselineReleaseId')?.value;
        return publishedReleases.find((x) => String(x.id || x.Id) === String(id));
    };

    // Renders the PUBLISHED baseline tree as a jsTree, matching the FU04 Manual Designer's
    // definition tree (folder icons, expand/collapse, cascading checkboxes, search).
    const renderBaselineTree = () => {
        const host = $('baselineTreeSelection');
        if (!host) return;
        const release = currentRelease();
        const definitions = release?.definitions || release?.Definitions || [];

        const jq = window.jQuery;
        const hasJstree = jq && jq.fn && jq.fn.jstree;
        if (hasJstree && jq(host).jstree(true)) {
            jq(host).jstree('destroy');
        }
        baselineTreeApi = null;

        if (!definitions.length) {
            host.innerHTML = `<div class="text-muted small p-2">${text(L.NoPublishedRelease)}</div>`;
            syncSelectedCount();
            return;
        }

        if (!hasJstree) {
            renderBaselineTreeFallback(host, definitions);
            return;
        }

        host.innerHTML = '';
        const data = definitions.map((node) => {
            const canonicalId = node.canonicalId || node.CanonicalId;
            const parentRaw = node.parentCanonicalId || node.ParentCanonicalId || '';
            const name = node.name || node.Name || canonicalId;
            const path = node.fullPath || node.FullPath || name;
            return {
                id: String(canonicalId),
                parent: parentRaw ? String(parentRaw) : '#',
                text: html(name),
                type: 'folder',
                state: { opened: true },
                a_attr: { title: path }
            };
        });

        const $host = jq(host);
        const theme = jq('html').attr('data-bs-theme') === 'dark' ? 'default-dark' : 'default';
        $host.jstree({
            core: { themes: { name: theme }, data, multiple: true, check_callback: true },
            plugins: ['types', 'checkbox', 'wholerow', 'search'],
            types: {
                default: { icon: 'icon-base bx bx-folder text-warning' },
                folder: { icon: 'icon-base bx bx-folder text-warning' }
            },
            checkbox: { cascade: 'down+up+undetermined', three_state: true, whole_node: false, tie_selection: false },
            search: { show_only_matches: true, show_only_matches_children: true, close_opened_onclear: false }
        });
        baselineTreeApi = $host.jstree(true);
        $host.off('check_node.jstree uncheck_node.jstree ready.jstree')
            .on('check_node.jstree uncheck_node.jstree', () => {
                lastDryRun = null;
                if ($('btnExecute')) $('btnExecute').disabled = true;
                syncSelectedCount();
            })
            .on('ready.jstree', syncSelectedCount);
        syncSelectedCount();
    };

    // Plain nested-checkbox fallback if the jsTree vendor lib is unavailable.
    const renderBaselineTreeFallback = (host, definitions) => {
        const byParent = definitions.reduce((acc, item) => {
            const parent = item.parentCanonicalId || item.ParentCanonicalId || '';
            acc[parent] = acc[parent] || [];
            acc[parent].push(item);
            return acc;
        }, {});
        Object.keys(byParent).forEach((key) => byParent[key].sort((a, b) =>
            Number(a.displayOrder || a.DisplayOrder || 0) - Number(b.displayOrder || b.DisplayOrder || 0) ||
            String(a.fullPath || a.FullPath || '').localeCompare(String(b.fullPath || b.FullPath || ''))));

        const renderNodes = (parent, depth) => (byParent[parent || ''] || []).map((node) => {
            const canonicalId = node.canonicalId || node.CanonicalId;
            const name = node.name || node.Name || canonicalId;
            const path = node.fullPath || node.FullPath || name;
            const checkboxId = `branch-${crypto?.randomUUID ? crypto.randomUUID() : Math.random().toString(36).slice(2)}`;
            return `<div class="form-check py-1" style="margin-left:${depth * 1.1}rem">
                <input class="form-check-input branch-checkbox" type="checkbox" value="${html(canonicalId)}" id="${checkboxId}">
                <label class="form-check-label small" for="${checkboxId}">
                    <span class="fw-medium">${html(name)}</span>
                    <span class="text-muted d-block">${html(path)}</span>
                </label>
            </div>${renderNodes(canonicalId, depth + 1)}`;
        }).join('');

        host.innerHTML = renderNodes('', 0);
        host.querySelectorAll('.branch-checkbox').forEach((box) => box.addEventListener('change', () => {
            lastDryRun = null;
            if ($('btnExecute')) $('btnExecute').disabled = true;
            syncSelectedCount();
        }));
        syncSelectedCount();
    };

    const syncSelectedCount = () => {
        const count = getSelectedCanonicalIds().length;
        const el = $('selectedBranchCount');
        if (el) el.textContent = `${count} ${L.SelectedBranches || 'Selected branches'}`;
    };

    const syncSelectionMode = () => {
        const selected = getSelectionMode() === 'SELECTED_BRANCHES';
        $('selectedBranchesSection')?.classList.toggle('d-none', !selected);
        lastDryRun = null;
        if ($('btnExecute')) $('btnExecute').disabled = true;
        // Build/refresh the jsTree while the section is visible so wholerow widths lay out correctly.
        if (selected) renderBaselineTree();
    };

    const renderMessages = (result) => {
        const host = $('diagnosticMessages');
        if (!host) return;
        const diagnostics = result?.diagnostics;
        const warnings = diagnostics?.warnings || [];
        const errors = diagnostics?.errors || [];
        const bits = [];
        warnings.forEach((x) => bits.push(`<div class="alert alert-warning py-2 mb-2">${text(x)}</div>`));
        errors.forEach((x) => bits.push(`<div class="alert alert-danger py-2 mb-2">${text(x)}</div>`));
        if (diagnostics) {
            bits.push(`<div class="d-flex flex-wrap gap-2">
                <span class="badge bg-label-info">${L.IncludedScope || 'Scope'}: ${diagnostics.includedCanonicalIds?.length || 0}</span>
                <span class="badge bg-label-secondary">${L.IncludedAncestors || 'Ancestors'}: ${diagnostics.includedAncestors?.length || 0}</span>
                <span class="badge bg-label-secondary">${L.IncludedDescendants || 'Descendants'}: ${diagnostics.includedDescendants?.length || 0}</span>
                <span class="badge bg-label-dark">${L.ExcludedNodes || 'Excluded'}: ${diagnostics.excludedCanonicalIdsCount || 0}</span>
                <span class="badge bg-label-primary">${L.NodesToCreate || 'Create'}: ${diagnostics.nodesToCreate || 0}</span>
                <span class="badge bg-label-secondary">${L.NodesToSkip || 'Skip'}: ${diagnostics.nodesToSkip || 0}</span>
                <span class="badge bg-label-danger">${L.Conflicts || 'Conflicts'}: ${diagnostics.conflicts || 0}</span>
            </div>`);
            (diagnostics.blockedSelections || []).forEach((x) => bits.push(`<div class="alert alert-danger py-2 mb-2">${text(x)}</div>`));
        }
        host.innerHTML = bits.join('');
    };

    // Maps a per-node outcome status to a human-friendly label, colour tone and folder icon
    // so the plan/result is readable as "which folder will be created/skipped/failed".
    const planStatusMeta = (status) => {
        const s = String(status || '').toUpperCase();
        if (s === 'WOULD_CREATE') return { tone: 'success', label: L.WillBeCreated || 'Will be created', icon: 'icon-base bx bx-folder-plus text-success' };
        if (s === 'CREATED') return { tone: 'success', label: L.Created || 'Created', icon: 'icon-base bx bx-check-circle text-success' };
        if (s === 'SKIPPED') return { tone: 'warning', label: L.AlreadyExists || 'Already exists', icon: 'icon-base bx bx-folder text-warning' };
        if (s === 'FAILED') return { tone: 'danger', label: L.Failed || 'Failed', icon: 'icon-base bx bx-x-circle text-danger' };
        if (s === 'BLOCKED') return { tone: 'danger', label: L.Blocked || 'Blocked', icon: 'icon-base bx bx-block text-danger' };
        return { tone: 'secondary', label: text(status), icon: 'icon-base bx bx-folder text-muted' };
    };

    // Renders the dry-run / execute plan as a tree of real folder names (resolved from the
    // selected release definitions) with a per-node status badge, instead of raw canonical ids.
    const renderPlanTree = (result) => {
        const host = $('planTree');
        if (!host) return;
        const jq = window.jQuery;
        const hasJstree = jq && jq.fn && jq.fn.jstree;
        if (hasJstree && jq(host).jstree(true)) {
            jq(host).jstree('destroy');
        }
        planTreeApi = null;

        const outcomes = result?.outcomes || result?.diagnostics?.outcomes || [];
        if (!outcomes.length) {
            host.innerHTML = emptyStateMarkup(L.EmptyPlanHeading || 'No preview yet', L.EmptyPlanText || L.EmptyOutcomes || '');
            return;
        }

        // Resolve names/paths/parents from the selected release definitions.
        const release = currentRelease();
        const defs = release?.definitions || release?.Definitions || [];
        const defById = {};
        defs.forEach((d) => { defById[String(d.canonicalId || d.CanonicalId)] = d; });
        const inPlan = new Set(outcomes.map((o) => String(o.canonicalId || o.CanonicalId)));

        if (!hasJstree) {
            renderPlanTreeFallback(host, outcomes, defById);
            return;
        }

        const data = outcomes.map((o) => {
            const canonicalId = String(o.canonicalId || o.CanonicalId);
            const def = defById[canonicalId];
            const name = def ? (def.name || def.Name || canonicalId) : canonicalId;
            const path = def ? (def.fullPath || def.FullPath || name) : name;
            const parentRaw = def ? (def.parentCanonicalId || def.ParentCanonicalId || '') : '';
            const parent = (parentRaw && inPlan.has(String(parentRaw))) ? String(parentRaw) : '#';
            const meta = planStatusMeta(o.status || o.Status);
            const message = o.message || o.Message || '';
            const reason = o.reasonCode || o.ReasonCode || '';
            // Status badge per node; for problems (failed/blocked) add a solid red badge with the
            // reason/message so the issue is visible inline without opening a separate table.
            let badges = `<span class="badge bg-label-${meta.tone} ms-1">${html(meta.label)}</span>`;
            if (meta.tone === 'danger') {
                const detail = message || reason;
                if (detail) badges += ` <span class="badge bg-danger ms-1">${html(detail)}</span>`;
            }
            return {
                id: canonicalId,
                parent,
                text: `${html(name)} ${badges}`,
                icon: meta.icon,
                state: { opened: true },
                a_attr: { title: message ? `${path} — ${message}` : path }
            };
        });

        host.innerHTML = '';
        const $host = jq(host);
        const theme = jq('html').attr('data-bs-theme') === 'dark' ? 'default-dark' : 'default';
        $host.jstree({
            core: { themes: { name: theme, dots: true }, data, check_callback: false },
            plugins: ['types', 'wholerow', 'search'],
            types: { default: { icon: 'icon-base bx bx-folder text-muted' } },
            search: { show_only_matches: true, show_only_matches_children: true, close_opened_onclear: false }
        });
        planTreeApi = $host.jstree(true);
    };

    // Plain nested-list fallback if the jsTree vendor lib is unavailable.
    const renderPlanTreeFallback = (host, outcomes, defById) => {
        const byParent = {};
        const inPlan = new Set(outcomes.map((o) => String(o.canonicalId || o.CanonicalId)));
        outcomes.forEach((o) => {
            const canonicalId = String(o.canonicalId || o.CanonicalId);
            const def = defById[canonicalId];
            const parentRaw = def ? (def.parentCanonicalId || def.ParentCanonicalId || '') : '';
            const parent = (parentRaw && inPlan.has(String(parentRaw))) ? String(parentRaw) : '';
            (byParent[parent] = byParent[parent] || []).push(o);
        });
        const renderNodes = (parent, depth) => (byParent[parent || ''] || []).map((o) => {
            const canonicalId = String(o.canonicalId || o.CanonicalId);
            const def = defById[canonicalId];
            const name = def ? (def.name || def.Name || canonicalId) : canonicalId;
            const meta = planStatusMeta(o.status || o.Status);
            return `<div class="py-1" style="margin-left:${depth * 1.1}rem">
                <i class="${meta.icon} me-1"></i><span class="fw-medium small">${html(name)}</span>
                <span class="badge bg-label-${meta.tone} ms-1">${html(meta.label)}</span>
            </div>${renderNodes(canonicalId, depth + 1)}`;
        }).join('');
        host.innerHTML = renderNodes('', 0);
    };

    const renderResult = (result) => {
        lastOperation = result;
        setCorrelation(result?.correlationId || result?.correlation_id);
        setText('countCreated', result?.created || 0);
        setText('countSkipped', result?.skipped || 0);
        setText('countFailed', result?.failed || 0);
        setText('countTotal', result?.total || 0);
        // #operationStatus is itself the badge — update its tone class + text in place
        // (don't inject a nested badge inside it).
        const statusEl = $('operationStatus');
        if (statusEl) {
            statusEl.className = `badge bg-label-${badgeTone(result?.status || 'READY')}`;
            statusEl.textContent = text(result?.status || 'READY');
        }
        renderMessages(result);
        const outcomes = result?.outcomes || result?.diagnostics?.outcomes || [];
        renderPlanTree(result);
        const failedRetryable = outcomes.some((x) => String(x.status || '').toUpperCase() === 'FAILED' && x.retryable);
        if ($('btnRetry')) $('btnRetry').disabled = !failedRetryable;
    };

    const companyLabel = (companyId) => legalEntityNames[String(companyId)] || companyId;
    const baselineLabel = (baselineId) => {
        const r = publishedReleases.find((x) => String(x.id || x.Id) === String(baselineId));
        return r ? (r.baselineReleaseId || r.BaselineReleaseId || '') : '';
    };

    const canArchive = () => !!(window.FU05Permissions && window.FU05Permissions.canArchive);

    // Posts a soft status change (archive/restore) for an instance node + its sub-tree, then refreshes.
    const changeInstanceStatus = (id, action, confirmText, successText, confirmButtonText, confirmType) => {
        if (!id) return;
        const run = async () => {
            const fd = new FormData();
            fd.append('__RequestVerificationToken', csrf());
            try {
                const payload = await api(`/DocumentManagementInstantiations/instances/${id}/${action}`, { method: 'POST', body: fd });
                const count = payload?.data?.affectedCount ?? payload?.data?.AffectedCount ?? 0;
                window.showToast?.(successText.replace('{0}', count), 'success');
                renderInstancesTree();
            } catch (_) { /* api() already surfaced the error toast */ }
        };
        if (typeof window.showConfirm === 'function') {
            window.showConfirm(confirmText, run, { type: confirmType, confirmButtonText });
        } else if (window.confirm(confirmText)) {
            run();
        }
    };

    // Soft-archives a company instance node and its whole sub-tree (server cascades by canonical id).
    const archiveInstance = (id, name) => changeInstanceStatus(
        id, 'archive',
        (L.ArchiveConfirm || 'Archive “{0}” and all of its sub-folders?').replace('{0}', name || ''),
        L.ArchiveSuccess || 'Archived {0} folder(s).',
        L.Archive || 'Archive', 'danger');

    // Restores (un-archives) a node + its sub-tree and re-activates required ancestors.
    const restoreInstance = (id, name) => changeInstanceStatus(
        id, 'restore',
        (L.RestoreConfirm || 'Restore “{0}” and its sub-folders to active?').replace('{0}', name || ''),
        L.RestoreSuccess || 'Restored {0} folder(s).',
        L.Restore || 'Restore', 'info');

    // Renders the company's instantiated structures grouped by company → as a folder tree
    // (instead of a flat GUID table), matching the rest of the wizard.
    const renderInstancesTree = async () => {
        const host = $('instancesTree');
        if (!host) return;
        const jq = window.jQuery;
        const hasJstree = jq && jq.fn && jq.fn.jstree;
        if (hasJstree && jq(host).jstree(true)) {
            jq(host).jstree('destroy');
        }
        instancesTreeApi = null;

        let items = [];
        try {
            const payload = await api('/DocumentManagementInstantiations/instances', undefined, { suppressToast: true });
            items = payload?.data || payload?.Data || [];
        } catch (_) {
            host.innerHTML = emptyStateMarkup(L.EmptyInstancesHeading || 'No instances yet', L.ErrorOccurred || '');
            return;
        }
        if (!items.length) {
            host.innerHTML = emptyStateMarkup(L.EmptyInstancesHeading || 'No instances yet', L.EmptyInstances || '');
            return;
        }
        if (!hasJstree) {
            host.innerHTML = `<div class="text-muted small p-2">${html(items.length)} ${html(L.FoldersLabel || 'folders')}</div>`;
            return;
        }

        // Group by company + baseline + token; each group is a parent-complete instance subtree.
        const groups = {};
        items.forEach((it) => {
            const companyId = String(it.companyId || it.CompanyId || '');
            const baselineId = String(it.baselineReleaseId || it.BaselineReleaseId || '');
            const token = String(it.instanceToken || it.InstanceToken || '');
            const key = `${companyId}|${baselineId}|${token}`;
            (groups[key] = groups[key] || { companyId, baselineId, token, items: [] }).items.push(it);
        });

        const data = [];
        Object.keys(groups).forEach((key) => {
            const g = groups[key];
            const groupId = `grp:${key}`;
            const baseline = baselineLabel(g.baselineId);
            const parts = [`${g.items.length} ${L.FoldersLabel || 'folders'}`];
            if (baseline) parts.unshift(baseline);
            if (g.token) parts.push(g.token);
            data.push({
                id: groupId,
                parent: '#',
                text: `<span class="fw-medium">${html(companyLabel(g.companyId))}</span> <span class="text-muted small">· ${html(parts.join(' · '))}</span>`,
                icon: 'icon-base bx bx-buildings text-primary',
                state: { opened: true },
                a_attr: { title: `${g.companyId}` }
            });
            const inGroup = new Set(g.items.map((x) => String(x.canonicalId || x.CanonicalId)));
            g.items.forEach((it) => {
                const canonicalId = String(it.canonicalId || it.CanonicalId);
                const parentRaw = it.parentCanonicalId || it.ParentCanonicalId || '';
                const parent = (parentRaw && inGroup.has(String(parentRaw))) ? `${groupId}::${parentRaw}` : groupId;
                const name = it.name || it.Name || canonicalId;
                const path = it.fullPath || it.FullPath || name;
                const status = it.instanceStatus || it.InstanceStatus || '';
                const id = it.id || it.Id;
                data.push({
                    id: `${groupId}::${canonicalId}`,
                    parent,
                    text: `${html(name)} <span class="badge bg-label-${badgeTone(status)} ms-1">${html(String(status).toLowerCase())}</span>`,
                    icon: 'icon-base bx bx-folder text-warning',
                    state: { opened: true },
                    a_attr: { title: `${path} — ${L.ViewDetails || 'View details'}` },
                    data: { detailId: id, name, status: String(status).toUpperCase() }
                });
            });
        });

        host.innerHTML = '';
        const $host = jq(host);
        const theme = jq('html').attr('data-bs-theme') === 'dark' ? 'default-dark' : 'default';
        const plugins = ['types', 'wholerow', 'search'];
        if (canArchive()) plugins.push('contextmenu');
        $host.jstree({
            core: { themes: { name: theme, dots: true }, data, check_callback: false },
            plugins,
            types: { default: { icon: 'icon-base bx bx-folder text-muted' } },
            search: { show_only_matches: true, show_only_matches_children: true, close_opened_onclear: false },
            contextmenu: {
                items: (node) => {
                    const detailId = node?.data?.detailId;
                    if (!detailId || !canArchive()) return {};
                    const isArchived = String(node?.data?.status || '').toUpperCase() === 'ARCHIVED';
                    return isArchived
                        ? {
                            restore: {
                                label: L.Restore || 'Restore',
                                icon: false,
                                action: () => restoreInstance(detailId, node?.data?.name)
                            }
                        }
                        : {
                            archive: {
                                label: L.Archive || 'Archive',
                                icon: false,
                                action: () => archiveInstance(detailId, node?.data?.name)
                            }
                        };
                }
            }
        });
        instancesTreeApi = $host.jstree(true);
        // Left-click a folder node to open its instance detail (group nodes carry no detailId).
        // Skip right-click: the contextmenu plugin also "activates" the node, which must not navigate.
        $host.off('activate_node.jstree').on('activate_node.jstree', (event, node) => {
            const orig = node?.event;
            if (orig && (orig.type === 'contextmenu' || orig.which === 3 || orig.button === 2)) return;
            const detailId = node?.node?.data?.detailId;
            if (detailId) window.location.href = `/DocumentManagementInstantiations/Details/${detailId}`;
        });
    };

    const loadPrerequisites = async () => {
        const payload = await api('/DocumentManagementInstantiations/prerequisites');
        const data = payload?.data || {};
        publishedReleases = data.publishedReleases || [];
        const select = $('baselineReleaseId');
        if (select) {
            select.innerHTML = '';
            publishedReleases.forEach((release) => {
                const opt = document.createElement('option');
                opt.value = release.id;
                opt.textContent = `${release.baselineReleaseId || release.id} - ${release.baselineVersion || ''}`;
                select.appendChild(opt);
            });
            select.addEventListener('change', () => {
                lastDryRun = null;
                if ($('btnExecute')) $('btnExecute').disabled = true;
                renderBaselineTree();
            });
            renderBaselineTree();
        }

        const alert = $('preconditionAlert');
        if (alert) {
            // Only surface actionable preconditions. The MOD-0220 company-validation mode
            // (local-smoke vs fail-closed) is internal plumbing and is intentionally not shown.
            const messages = [];
            if (!data.hasPublishedRelease) messages.push(L.NoPublishedRelease || 'No published release is available.');
            alert.textContent = messages.filter(Boolean).join(' ');
            alert.classList.toggle('d-none', messages.length === 0);
        }
    };

    // Company/LegalEntity Select2 source: tenant-scoped referenceable legal entities (MDM / MOD-0220),
    // fetched through the same-origin proxy. Plant/Business Unit stay free-entry Select2 (tags) for now.
    const loadLegalEntities = async () => {
        const select = $('companyId');
        if (!select) return;
        try {
            const payload = await api('/DocumentManagementInstantiations/legal-entities', undefined, { suppressToast: true });
            const list = payload?.data || payload?.Data || [];
            select.innerHTML = '<option></option>';
            list.forEach((le) => {
                const id = le.legalEntityId || le.LegalEntityId || le.id;
                if (!id) return;
                const label = le.displayName || le.DisplayName || le.legalName || le.LegalName || id;
                legalEntityNames[String(id)] = label; // GUID -> friendly name for the instances tree
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = label;
                select.appendChild(opt);
            });
        } catch (_) {
            window.showToast?.(L.LegalEntitiesUnavailable || 'Legal entity lookup is unavailable. You can enter a company GUID manually.', 'warning');
        }
    };

    const initScopeSelects = () => {
        const jq = window.jQuery;
        if (!jq || !jq.fn || !jq.fn.select2) return;
        jq('#companyId').select2({
            dropdownParent: jq(document.body),
            width: '100%',
            tags: true,
            allowClear: true,
            placeholder: jq('#companyId').data('placeholder') || ''
        });
        ['#plantId', '#businessUnitId'].forEach((sel) => {
            jq(sel).select2({
                dropdownParent: jq(document.body),
                width: '100%',
                tags: true,
                allowClear: true,
                placeholder: jq(sel).data('placeholder') || ''
            });
        });
    };

    const dryRun = async () => {
        if (!validateInputs()) return;
        const payload = await api('/DocumentManagementInstantiations/dry-run', { method: 'POST', body: formData() });
        lastDryRun = payload?.data;
        renderResult(lastDryRun);
        if ($('btnExecute')) $('btnExecute').disabled = Boolean(lastDryRun?.diagnostics?.blocked);
        window.showToast?.(L.DryRunComplete || 'Dry-run complete.', 'success');
    };

    const execute = async () => {
        if (!validateInputs() || !lastDryRun || lastDryRun?.diagnostics?.blocked) {
            window.showToast?.(L.DryRunRequired || 'Run a successful dry-run first.', 'warning');
            return;
        }
        const payload = await api('/DocumentManagementInstantiations/execute', { method: 'POST', body: formData() });
        renderResult(payload?.data);
        renderInstancesTree();
        window.showToast?.(L.ExecuteComplete || 'Instantiation complete.', 'success');
    };

    const retry = async () => {
        if (!lastOperation?.operationId) return;
        const fd = new FormData();
        fd.append('__RequestVerificationToken', csrf());
        const failed = (lastOperation.outcomes || []).filter((x) => String(x.status || '').toUpperCase() === 'FAILED' && x.retryable);
        failed.forEach((x) => fd.append('nodeKeys', x.nodeKey));
        try {
            const payload = await api(`/DocumentManagementInstantiations/operations/${lastOperation.operationId}/retry`, { method: 'POST', body: fd });
            renderResult(payload?.data);
            renderInstancesTree();
        } catch (err) {
            if (err?.status === 404 || err?.status === 405 || err?.payload?.reason_code === 'RETRY_UNAVAILABLE') {
                window.showToast?.(L.RetryUnavailable || 'Retry is not available.', 'warning');
            }
        }
    };

    const init = async () => {
        setCorrelation(flowCorrelationId);
        await loadLegalEntities();
        initScopeSelects();
        $('btnDryRun')?.addEventListener('click', dryRun);
        $('btnExecute')?.addEventListener('click', execute);
        $('btnRetry')?.addEventListener('click', retry);
        document.querySelectorAll('input[name="selectionMode"]').forEach((x) => x.addEventListener('change', syncSelectionMode));
        const treeSearch = $('baselineTreeSearch');
        if (treeSearch) {
            let searchTimer;
            treeSearch.addEventListener('input', (event) => {
                const value = event.target.value;
                clearTimeout(searchTimer);
                searchTimer = setTimeout(() => baselineTreeApi?.search(value), 200);
            });
        }
        const planSearch = $('planTreeSearch');
        if (planSearch) {
            let planSearchTimer;
            planSearch.addEventListener('input', (event) => {
                const value = event.target.value;
                clearTimeout(planSearchTimer);
                planSearchTimer = setTimeout(() => planTreeApi?.search(value), 200);
            });
        }
        const instancesSearch = $('instancesTreeSearch');
        if (instancesSearch) {
            let instSearchTimer;
            instancesSearch.addEventListener('input', (event) => {
                const value = event.target.value;
                clearTimeout(instSearchTimer);
                instSearchTimer = setTimeout(() => instancesTreeApi?.search(value), 200);
            });
        }
        renderPlanTree({}); // show the empty-state until the first dry-run/execute
        await loadPrerequisites().catch(() => {});
        syncSelectionMode();
        renderInstancesTree();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => DocumentationInstantiations.init());
