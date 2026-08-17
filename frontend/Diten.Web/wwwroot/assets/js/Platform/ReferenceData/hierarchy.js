'use strict';

(function () {
    const root = document.getElementById('rd-hierarchy-page');
    if (!root) return;

    const setCode = root.dataset.setCode;
    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || { can: () => true, apply: (el, _cap, stateAllowed) => { if (el) el.disabled = stateAllowed === false; return stateAllowed !== false; }, guard: () => true };

    const body = document.getElementById('rd-hierarchy-body');
    const hierarchyTableEl = document.getElementById('dt-reference-data-hierarchy');
    const tree = document.getElementById('rd-hierarchy-tree');
    const statusEl = document.getElementById('rd-hierarchy-status');
    const emptyEl = document.getElementById('rd-hierarchy-empty');
    const errorEl = document.getElementById('rd-hierarchy-error');
    const cardEl = document.getElementById('rd-hierarchy-card');
    const tableColEl = document.getElementById('rd-hierarchy-table-col');
    const previewColEl = document.getElementById('rd-hierarchy-preview-col');
    const previewCardEl = document.getElementById('rd-hierarchy-preview-card');
    const previewToggleBtn = document.getElementById('rd-hierarchy-preview-toggle');
    const previewRestoreBtn = document.getElementById('rd-hierarchy-preview-restore');
    const refreshBtn = document.getElementById('rd-hierarchy-refresh');

    let currentSet = null;
    let draftVersion = null;
    let values = [];
    let hierarchyDt = null;
    let previewCollapsed = false;
    let hierarchyDirty = false;

    const show = (el, on) => el && el.classList.toggle('d-none', !on);
    const normalize = (value) => String(value || '').trim().toLowerCase();
    const text = (value) => value == null || String(value).trim() === '' ? '-' : String(value);
    const tt = (key, fallback) => {
        const value = (window.L10n || {})[key];
        return typeof value === 'string' && value.trim() ? value : fallback;
    };
    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const escapeCss = (value) => window.CSS?.escape ? window.CSS.escape(String(value ?? '')) : String(value ?? '').replace(/"/g, '\\"');
    const noDraftReason = 'An active draft version is required.';
    const retiredSetReason = permissions.retiredSetReason || 'This reference data set is retired. Changes are disabled.';
    const isRetiredSet = (setInfo) => (typeof permissions.isRetiredSet === 'function'
        ? permissions.isRetiredSet(setInfo)
        : normalize(setInfo?.status || setInfo?.Status) === 'retired');
    const applySetGate = () => {
        const retired = isRetiredSet(currentSet);
        if (typeof permissions.setGlobalBlock === 'function') {
            permissions.setGlobalBlock(retired, retiredSetReason);
        }
        if (retired) {
            setStatus(retiredSetReason, 'info');
        }
        return retired;
    };

    const getSaveBtn = () => document.getElementById('rd-hierarchy-save');

    const setDraftActions = (enabled, reason) => {
        permissions.apply(getSaveBtn(), 'canUpdateVersion', enabled, reason || noDraftReason);
    };

    const setSaveVisible = (visible) => {
        const saveBtn = getSaveBtn();
        if (saveBtn) saveBtn.classList.toggle('d-none', !visible);
    };

    const normalizeSaveButton = () => {
        const saveBtn = getSaveBtn();
        saveBtn?.querySelector('.bx-plus')?.remove();
    };

    const syncPreviewToggle = () => {
        if (tableColEl) {
            tableColEl.classList.toggle('col-xl-9', !previewCollapsed);
            tableColEl.classList.toggle('col-xl-12', previewCollapsed);
        }
        if (previewColEl) {
            previewColEl.classList.toggle('d-none', previewCollapsed);
        }
        if (previewToggleBtn) {
            const label = previewCollapsed
                ? tt('ExpandPreview', 'Expand preview')
                : tt('CollapsePreview', 'Collapse preview');
            previewToggleBtn.setAttribute('aria-expanded', String(!previewCollapsed));
            previewToggleBtn.setAttribute('aria-label', label);
            previewToggleBtn.setAttribute('title', label);
        }
        previewRestoreBtn?.classList.toggle('d-none', !previewCollapsed);
        setTimeout(() => {
            try { hierarchyDt?.columns.adjust(); } catch (_error) { }
        }, 0);
    };

    const togglePreview = () => {
        previewCollapsed = !previewCollapsed;
        syncPreviewToggle();
    };

    const runSaveFromToolbar = async () => {
        const saveBtn = getSaveBtn();
        if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
        try {
            if (saveBtn) saveBtn.disabled = true;
            await save();
        } catch (error) {
            if (error?.isHandled) return;
            window.showToast?.(error?.message || tt('SaveFailed', 'Save failed.'), 'error');
        } finally {
            if (saveBtn) saveBtn.disabled = !draftVersion;
        }
    };

    const renderEmptyState = ({ icon, title, description, actionsHtml }) => {
        show(cardEl, false);
        show(previewCardEl, false);
        setSaveVisible(false);
        if (body) body.innerHTML = '';
        if (tree) tree.innerHTML = '';
        previewCollapsed = false;
        syncPreviewToggle();
        if (!emptyEl) return;
        emptyEl.innerHTML = `
            <div class="card">
                <div class="card-body text-center py-5">
                    <i class="bx ${icon} mb-3" style="font-size:3rem;line-height:1;color:var(--bs-secondary-color,#a7acb2);"></i>
                    <h5 class="mb-2">${title}</h5>
                    <p class="text-muted mb-4 mx-auto" style="max-width:560px;">${description}</p>
                    <div class="d-flex justify-content-center gap-2 flex-wrap">${actionsHtml || ''}</div>
                </div>
            </div>`;
        show(emptyEl, true);
    };

    const renderNoDraftState = (setId) => {
        draftVersion = null;
        values = [];
        const workspaceHref = `/Platform/ReferenceData/Sets/${setId}`;
        const openWorkspaceBtn = `<a class="btn btn-primary" href="${workspaceHref}"><i class="bx bx-folder-open me-1"></i>${escapeHtml(tt('OpenSetWorkspace', 'Open Set Workspace'))}</a>`;

        if (isRetiredSet(currentSet)) {
            renderEmptyState({
                icon: 'bx-archive',
                title: escapeHtml(tt('NoDraftTitle', 'No editable draft version')),
                description: escapeHtml(tt('NoDraftRetired', 'This set is retired; new drafts cannot be created.')),
                actionsHtml: openWorkspaceBtn
            });
            setDraftActions(false, noDraftReason);
            return;
        }

        const hasPublished = !!(currentSet?.publishedVersionId || currentSet?.PublishedVersionId);
        const hint = hasPublished
            ? tt('NoDraftHintFromPublished', 'You can create a new draft from the published version.')
            : tt('NoDraftHintNoVersions', 'This set has no versions yet. Create the first draft.');
        const description = `${escapeHtml(tt('NoDraftHierarchyDescription', 'Hierarchy can only be edited on a draft version.'))}<br><span class="fw-medium text-heading">${escapeHtml(setCode)}</span> &mdash; ${escapeHtml(hint)}`;

        renderEmptyState({
            icon: 'bx-git-branch',
            title: escapeHtml(tt('NoDraftTitle', 'No editable draft version')),
            description,
            actionsHtml: openWorkspaceBtn
        });
        setDraftActions(false, noDraftReason);
        setSaveVisible(false);
    };

    const setStatus = (message, level) => {
        if (!statusEl) return;
        if (!message) {
            statusEl.className = 'alert alert-info d-none';
            statusEl.textContent = '';
            return;
        }

        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : level === 'warning' ? 'warning' : 'info';
        statusEl.className = `alert alert-${css}`;
        statusEl.textContent = message;
    };

    const setHierarchyDirty = (dirty) => {
        hierarchyDirty = dirty;
        if (dirty) {
            setStatus(tt('HierarchyUnsavedWarning', 'You have unsaved hierarchy changes. Click Save Hierarchy to apply them.'), 'warning');
            return;
        }
        setStatus(null);
    };

    const renderNode = (node) => {
        const li = document.createElement('li');
        li.className = 'mb-1';
        li.textContent = `${node.displayName} (${node.valueCode})`;
        if (node.children.length > 0) {
            const ul = document.createElement('ul');
            ul.className = 'mt-1';
            node.children.forEach((child) => ul.appendChild(renderNode(child)));
            li.appendChild(ul);
        }
        return li;
    };

    const detectCycle = (items) => {
        const byCode = new Map(items.map((item) => [normalize(item.code), normalize(item.parentValueCode)]));
        const visiting = new Set();
        const visited = new Set();

        const visit = (code) => {
            if (!code || visited.has(code)) return false;
            if (visiting.has(code)) return true;
            visiting.add(code);
            const parent = byCode.get(code);
            if (parent && byCode.has(parent) && visit(parent)) return true;
            visiting.delete(code);
            visited.add(code);
            return false;
        };

        for (const code of byCode.keys()) {
            if (visit(code)) return true;
        }

        return false;
    };

    const buildTree = (items) => {
        const byCode = new Map(items.map((item) => [normalize(item.code), item]));
        const childrenMap = new Map();

        items.forEach((item) => {
            const parentKey = normalize(item.parentValueCode);
            const key = parentKey && byCode.has(parentKey) ? parentKey : '__root__';
            if (!childrenMap.has(key)) childrenMap.set(key, []);
            childrenMap.get(key).push(item);
        });

        const build = (parentKey) => {
            const children = childrenMap.get(parentKey) || [];
            return children
                .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0) || a.code.localeCompare(b.code))
                .map((child) => ({
                    valueCode: child.code,
                    displayName: child.label || child.code,
                    children: build(normalize(child.code))
                }));
        };

        return build('__root__');
    };

    const renderPreview = () => {
        if (!tree) return;
        tree.innerHTML = '';

        if (!values.length) {
            tree.innerHTML = '<li class="text-muted">No draft values available.</li>';
            return;
        }

        if (detectCycle(values)) {
            tree.innerHTML = '<li class="text-danger">Hierarchy cycle detected. Fix parent assignments before save.</li>';
            return;
        }

        const nodes = buildTree(values);
        if (!nodes.length) {
            tree.innerHTML = '<li class="text-muted">No root nodes. Check parent assignments.</li>';
            return;
        }

        nodes.forEach((node) => tree.appendChild(renderNode(node)));
    };

    const syncParentsFromInputs = () => {
        values = values.map((item) => {
            const selector = `.rd-parent-select[data-code="${escapeCss(item.code)}"]`;
            const select = hierarchyTableEl?.querySelector(selector);
            const parentValueCode = (select?.value || '').trim() || null;
            return select ? { ...item, parentValueCode } : item;
        });
    };

    const renderParentOptions = (row) => {
        const rowCode = normalize(row.code);
        const options = [`<option value="">${escapeHtml(tt('NoParentOption', '(No parent)'))}</option>`];
        values
            .filter((candidate) => normalize(candidate.code) !== rowCode)
            .sort((a, b) => String(a.code).localeCompare(String(b.code)))
            .forEach((candidate) => {
                const selected = normalize(candidate.code) === normalize(row.parentValueCode) ? 'selected' : '';
                const code = escapeHtml(candidate.code);
                const label = escapeHtml(candidate.label || candidate.code);
                options.push(`<option value="${code}" ${selected}>${code} - ${label}</option>`);
            });
        return options.join('');
    };

    const renderClearParentAction = (row, disabled) => {
        return window.DitenDataTable?.renderActions
            ? window.DitenDataTable.renderActions([{
                key: 'clearParent',
                className: `rd-parent-clear text-danger ${disabled ? 'disabled' : ''}`,
                icon: 'bx bx-x',
                attrs: {
                    'data-code': row.code,
                    title: tt('ClearParent', 'Clear Parent'),
                    'aria-label': tt('ClearParent', 'Clear Parent')
                }
            }])
            : `<button type="button" class="btn btn-sm btn-icon btn-text-danger rd-parent-clear" data-code="${escapeHtml(row.code)}" ${disabled ? 'disabled' : ''} title="${escapeHtml(tt('ClearParent', 'Clear Parent'))}" aria-label="${escapeHtml(tt('ClearParent', 'Clear Parent'))}"><i class="icon-base bx bx-x icon-sm"></i></button>`;
    };

    const clearParentForCode = (code) => {
        const select = hierarchyTableEl.querySelector(`.rd-parent-select[data-code="${escapeCss(code)}"]`);
        if (!select) return;
        select.value = '';
        values = values.map((item) => normalize(item.code) === normalize(code) ? { ...item, parentValueCode: null } : item);
        setHierarchyDirty(true);
        renderPreview();
    };

    const ensureHierarchyDataTable = () => {
        if (!hierarchyTableEl || hierarchyDt) return hierarchyDt;
        const L = window.L10n || {};

        hierarchyDt = new DataTable(hierarchyTableEl, window.DtDefaults.create({
            stateSave: false,
            data: values,
            order: [[1, 'asc']],
            colReorder: { columns: ':gt(0):not(:last-child)' },
            columns: [
                { data: null, name: 'control', title: '' },
                { data: 'code', name: 'code', title: L.ValueCodeColumn || tt('ValueCodeColumn', 'Value Code') },
                { data: 'label', name: 'label', title: L.DisplayNameColumn || tt('DisplayNameColumn', 'Display Name') },
                { data: null, name: 'parent', title: L.ParentColumn || tt('ParentColumn', 'Parent') },
                { data: null, name: 'action', title: L.Actions || tt('Actions', 'Actions') }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1, render: (data, type) => type === 'display' ? `<span class="fw-medium text-heading">${escapeHtml(data || '-')}</span>` : (data || '') },
                { targets: 2, responsivePriority: 3, render: (data) => escapeHtml(data || '-') },
                {
                    targets: 3,
                    orderable: false,
                    searchable: false,
                    render: (_data, _type, row) => {
                        const readOnly = typeof permissions.isBlocked === 'function' && permissions.isBlocked();
                        const disabled = readOnly ? 'disabled' : '';
                        return `<select class="form-select form-select-sm rd-parent-select" data-code="${escapeHtml(row.code)}" ${disabled}>${renderParentOptions(row)}</select>`;
                    }
                },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (_data, _type, row) => {
                        const readOnly = typeof permissions.isBlocked === 'function' && permissions.isBlocked();
                        return renderClearParentAction(row, readOnly);
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.SaveHierarchy || tt('SaveHierarchy', 'Save Hierarchy'),
                {
                    href: '#',
                    id: 'rd-hierarchy-save',
                    title: L.SaveHierarchy || tt('SaveHierarchy', 'Save Hierarchy'),
                    'aria-label': L.SaveHierarchy || tt('SaveHierarchy', 'Save Hierarchy')
                },
                {},
                { exportColumns: [1, 2, 3], colvisColumns: [1, 2, 3] }
            ),
            initComplete: function () {
                normalizeSaveButton();
                const saveButton = getSaveBtn();
                saveButton?.addEventListener('click', (event) => {
                    event.preventDefault();
                    runSaveFromToolbar();
                });
                setDraftActions(!!draftVersion, noDraftReason);
                setSaveVisible(!!draftVersion);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), 0);
            }
        }));

        return hierarchyDt;
    };

    const renderTable = () => {
        if (!hierarchyTableEl || !window.DataTable || !window.DtDefaults) return;
        const readOnly = typeof permissions.isBlocked === 'function' && permissions.isBlocked();

        if (!values.length) {
            const api = ensureHierarchyDataTable();
            api?.clear();
            api?.draw();
            permissions.apply(getSaveBtn(), 'canUpdateVersion', false, 'Draft values are required.');
            renderPreview();
            return;
        }

        const api = ensureHierarchyDataTable();
        api?.clear();
        api?.rows.add(values);
        api?.draw();

        permissions.apply(getSaveBtn(), 'canUpdateVersion', !readOnly);
        renderPreview();
    };

    const resolveSet = async () => {
        const data = await api.getSets(`?search=${encodeURIComponent(setCode)}&status=&scope_type=&page=1&page_size=100&sort=-createdAt`);
        const items = data?.items || data?.Items || [];
        const candidate = items.find((x) => normalize(x.setCode || x.SetCode) === normalize(setCode)) || null;
        if (!candidate) return null;
        return api.getSet(candidate.setId || candidate.SetId);
    };

    const load = async () => {
        setStatus(null);
        show(errorEl, false);
        show(emptyEl, false);
        show(cardEl, true);
        show(previewCardEl, true);
        hierarchyDirty = false;
        setDraftActions(false, noDraftReason);
        setSaveVisible(true);

        currentSet = await resolveSet();
        if (typeof permissions.clearGlobalBlock === 'function') {
            permissions.clearGlobalBlock();
        }
        if (!currentSet) {
            renderEmptyState({
                icon: 'bx-error-circle',
                title: escapeHtml(tt('SetNotFoundTitle', 'Set not found')),
                description: `${escapeHtml(tt('SetNotFoundDescription', 'The requested reference data set was not found:'))} <span class="fw-medium text-heading">${escapeHtml(setCode)}</span>`,
                actionsHtml: `<a class="btn btn-label-secondary" href="/Platform/ReferenceData"><i class="bx bx-arrow-back me-1"></i>${escapeHtml(tt('BackToSets', 'Back to Sets'))}</a>`
            });
            setDraftActions(false, 'Set must be loaded before editing hierarchy.');
            return;
        }
        const retired = applySetGate();

        const draftVersionId = currentSet.activeDraftVersionId || currentSet.ActiveDraftVersionId;
        const setId = currentSet.setId || currentSet.SetId;
        const crumb = document.getElementById('rd-hierarchy-crumb-set');
        if (crumb && setId) crumb.innerHTML = `<a href="/Platform/ReferenceData/Sets/${setId}">${escapeHtml(setCode)}</a>`;
        if (!draftVersionId) {
            renderNoDraftState(setId);
            return;
        }

        draftVersion = await api.getVersion(draftVersionId);
        const valuePayload = await api.getVersionValues(draftVersionId);
        values = (valuePayload?.items || valuePayload?.Items || []).map((item) => ({
            code: item.code || item.Code || '',
            label: item.label || item.Label || '',
            description: item.description || item.Description || '',
            isActive: item.isActive ?? item.IsActive ?? true,
            sortOrder: item.sortOrder ?? item.SortOrder ?? 0,
            parentValueCode: item.parentValueCode || item.ParentValueCode || null,
            attributes: item.attributes || item.Attributes || null
        }));

        renderTable();
        setStatus(retired ? retiredSetReason : null, 'info');
    };

    const validateDraftHierarchy = () => {
        const codes = new Set(values.map((item) => normalize(item.code)));
        for (const item of values) {
            const code = normalize(item.code);
            const parent = normalize(item.parentValueCode);
            if (!code) return 'Each value must have a code.';
            if (!parent) continue;
            if (code === parent) return `Value ${item.code} cannot be parent of itself.`;
            if (!codes.has(parent)) return `Parent ${item.parentValueCode} does not exist in current draft values.`;
        }

        if (detectCycle(values)) {
            return 'Hierarchy contains a cycle. Reparent values before saving.';
        }

        return null;
    };

    const save = async () => {
        if (!draftVersion) {
            setStatus(noDraftReason, 'error');
            return;
        }
        syncParentsFromInputs();
        const error = validateDraftHierarchy();
        if (error) {
            setStatus(error, 'error');
            return;
        }

        const draftVersionId = draftVersion?.versionId || draftVersion?.VersionId;
        const token = draftVersion?.concurrencyToken || draftVersion?.ConcurrencyToken;

        const payload = {
            expected_concurrency_token: token,
            values: values.map((item) => ({
                code: item.code,
                label: item.label,
                description: item.description || null,
                is_active: item.isActive !== false,
                sort_order: Number(item.sortOrder || 0),
                parent_value_code: item.parentValueCode || null,
                attributes: item.attributes || null
            }))
        };

        await api.replaceVersionValues(draftVersionId, payload);
        await load();
        hierarchyDirty = false;
        window.showToast?.(tt('HierarchySaved', 'Hierarchy assignments saved successfully.'), 'success');
    };

    hierarchyTableEl?.addEventListener('change', (event) => {
        const select = event.target.closest('.rd-parent-select');
        if (!select) return;
        const code = select.getAttribute('data-code');
        const parentValueCode = (select.value || '').trim() || null;
        values = values.map((item) => normalize(item.code) === normalize(code) ? { ...item, parentValueCode } : item);
        setHierarchyDirty(true);
        renderPreview();
    });

    hierarchyTableEl?.addEventListener('click', (event) => {
        const clearBtn = event.target.closest('.rd-parent-clear');
        if (!clearBtn) return;
        event.preventDefault();
        if (clearBtn.classList.contains('disabled')) return;
        if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
        const code = clearBtn.getAttribute('data-code');
        if (!code) return;

        if (window.showConfirm) {
            window.showConfirm(
                tt('ClearParentConfirm', 'Clear the parent assignment for this value?'),
                () => clearParentForCode(code),
                {
                    entityName: code,
                    type: 'danger',
                    confirmButtonText: tt('ClearParent', 'Clear Parent')
                }
            );
            return;
        }

        clearParentForCode(code);
    });

    previewToggleBtn?.addEventListener('click', togglePreview);
    previewRestoreBtn?.addEventListener('click', togglePreview);

    refreshBtn?.addEventListener('click', () => {
        load().catch((error) => {
            if (error?.isHandled) return;
            show(cardEl, false);
            errorEl.textContent = `Could not load hierarchy workspace: ${error?.message || 'request_failed'}`;
            show(errorEl, true);
        });
    });

    load().catch((error) => {
        if (error?.isHandled) return;
        show(cardEl, false);
        errorEl.textContent = `Could not load hierarchy workspace: ${error?.message || 'request_failed'}`;
        show(errorEl, true);
    });
})();
