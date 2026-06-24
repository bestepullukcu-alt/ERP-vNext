/**
 * MOD-0028-FU04 - Manual QMS tree designer.
 * Browser traffic stays same-origin; the MVC controller forwards to Gateway. Names are atomic display values
 * (a name may contain '/'); hierarchy always comes from parentCanonicalId.
 */
'use strict';

(function () {
    const root = document.getElementById('qmsDesignerRoot');
    if (!root) return;

    let L = window.L10n || {};
    let baseline = null;
    let definitions = [];
    let isDraft = false;
    const referenceDataCache = new Map();

    const baselineId = root.dataset.baselineId;
    const canCreate = root.dataset.canCreate === '1';
    const canEdit = root.dataset.canEdit === '1';
    const canMove = root.dataset.canMove === '1';
    const canDelete = root.dataset.canDelete === '1';
    const canValidate = root.dataset.canValidate === '1';
    const canPublish = root.dataset.canPublish === '1';
    const skeleton = document.getElementById('qmsDesignerSkeleton');
    const content = document.getElementById('qmsDesignerContent');
    const errorBox = document.getElementById('qmsDesignerError');
    const tree = document.getElementById('designerTree');
    const validationSummary = document.getElementById('validationSummary');
    const editorModalEl = document.getElementById('nodeEditorModal');
    const moveModalEl = document.getElementById('moveNodeModal');
    const editorModal = editorModalEl ? new bootstrap.Modal(editorModalEl) : null;
    const moveModal = moveModalEl ? new bootstrap.Modal(moveModalEl) : null;

    const t = (key, fallback) => L[key] || fallback || key;
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const antiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const text = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value === null || value === undefined || value === '' ? '-' : String(value); };
    // Compact single-line badge format: "Jun 18, 26 · 05:02 PM".
    const fmtDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        if (Number.isNaN(d.getTime())) return String(v);
        const locale = window.CurrentLanguage || undefined;
        const datePart = new Intl.DateTimeFormat(locale, { month: 'short', day: '2-digit', year: '2-digit' }).format(d);
        const timePart = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', hour12: true }).format(d);
        return `${datePart} · ${timePart}`;
    };

    const reasonText = (json) => {
        const code = json?.reason_code || json?.reasonCode;
        return {
            VALIDATION_FAILED: t('ReasonValidationFailed'),
            CONFLICT: t('ReasonConflict'),
            PERM_DENIED: t('ReasonPermDenied'),
            NOT_FOUND_NON_LEAKAGE: t('ReasonNotFound')
        }[code] || (json?.errors && json.errors[0]) || t('ErrorOccurred');
    };

    const request = async (url, options = {}) => {
        const token = antiForgeryToken();
        const headers = Object.assign({}, options.headers || {});
        if (token) headers.RequestVerificationToken = token;
        const response = await fetch(url, Object.assign({
            credentials: 'same-origin',
            headers
        }, options));
        let json = null;
        try { json = await response.json(); } catch (e) { /* ignore */ }
        return { response, json };
    };

    const unwrapReferenceItems = (json) => {
        const data = json?.data || json?.Data;
        const items = data?.items || data?.Items || [];
        return items
            .filter((item) => item?.isActive !== false && item?.IsActive !== false)
            .map((item) => {
                const code = item.code ?? item.Code;
                const label = item.label ?? item.Label ?? code;
                return {
                    id: code,
                    text: label
                };
            })
            .filter((item) => item.id);
    };

    const loadReferenceOptions = async (setCode) => {
        if (referenceDataCache.has(setCode)) {
            return referenceDataCache.get(setCode);
        }

        const result = await request(`/DocumentManagementQmsBaselines/reference-data/${encodeURIComponent(setCode)}`);
        if (!result.response.ok || !result.json?.isSuccessful) {
            throw new Error(reasonText(result.json));
        }

        const items = unwrapReferenceItems(result.json);
        referenceDataCache.set(setCode, items);
        return items;
    };

    const fillReferenceSelect = (select, items) => {
        const placeholder = select.dataset.placeholder || '';
        const current = select.value;
        const options = [`<option value="">${esc(placeholder)}</option>`]
            .concat(items.map((item) => `<option value="${esc(item.id)}">${esc(item.text)}</option>`));
        select.innerHTML = options.join('');
        select.value = current && items.some((item) => item.id === current) ? current : '';
    };

    const initReferenceSelects = async () => {
        const selects = Array.from(document.querySelectorAll('#nodeEditorForm select[data-reference-set]'));
        await Promise.all(selects.map(async (select) => {
            const items = await loadReferenceOptions(select.dataset.referenceSet);
            fillReferenceSelect(select, items);

            if (window.jQuery?.fn?.select2) {
                const $select = window.jQuery(select);
                if ($select.data('select2')) {
                    $select.select2('destroy');
                }

                $select.select2({
                    data: items,
                    dropdownParent: window.jQuery(editorModalEl || document.body),
                    placeholder: select.dataset.placeholder || '',
                    allowClear: true,
                    width: '100%'
                });
            }
        }));
    };

    const setReferenceValue = (id, value) => {
        const select = document.getElementById(id);
        if (!select) return;
        const normalized = value || '';
        if (normalized && !Array.from(select.options).some((option) => option.value === normalized)) {
            select.add(new Option(normalized, normalized, true, true));
        }

        if (window.jQuery?.fn?.select2) {
            window.jQuery(select).val(normalized).trigger('change');
            return;
        }

        select.value = normalized;
    };

    const postForm = async (url, formData) => {
        const token = antiForgeryToken();
        if (token && !formData.has('__RequestVerificationToken')) {
            formData.append('__RequestVerificationToken', token);
        }
        return request(url, { method: 'POST', body: formData });
    };

    const showError = (message, correlationId) => {
        skeleton?.classList.add('d-none');
        errorBox.classList.remove('d-none');
        errorBox.innerHTML = esc(message) + (correlationId ? `<div class="small mt-2">${t('CorrelationId')}: <code>${esc(correlationId)}</code></div>` : '');
    };

    const statusBadge = () => {
        const badge = document.getElementById('designerStatusBadge');
        if (!badge) return;
        const status = String(baseline?.status || '').toUpperCase();
        badge.className = 'badge ' + (status === 'PUBLISHED' ? 'bg-label-success' : status === 'DRAFT' ? 'bg-label-warning' : 'bg-label-secondary');
        badge.textContent = status === 'PUBLISHED' ? t('StatusPublished') : status === 'DRAFT' ? t('StatusDraft') : t('Unknown');
    };

    const byParent = () => {
        const map = new Map();
        definitions.forEach((node) => {
            const key = node.parentCanonicalId || '__root__';
            if (!map.has(key)) map.set(key, []);
            map.get(key).push(node);
        });
        map.forEach((items) => items.sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0) || String(a.name).localeCompare(String(b.name))));
        return map;
    };

    const findNode = (canonicalId) => definitions.find((d) => d.canonicalId === canonicalId);
    const boolValue = (value, fallback = false) => {
        if (value === true || value === false) return value;
        if (typeof value === 'string') {
            const normalized = value.trim().toLowerCase();
            if (normalized === 'true') return true;
            if (normalized === 'false') return false;
        }

        return fallback;
    };

    const allowsManualChildren = (node) =>
        boolValue(node?.allowsManualChildren ?? node?.AllowsManualChildren, true);

    const manualChildrenBlockedMessage = () =>
        t('ManualChildrenNotAllowed', 'Selected parent does not allow manual children.');

    const canMoveToParent = (node, parentCanonicalId) => {
        if (!node) return false;
        const normalizedParent = parentCanonicalId || '';
        const currentParent = node.parentCanonicalId || '';
        if (normalizedParent === currentParent) return true;
        if (!normalizedParent) return true;

        const parent = findNode(normalizedParent);
        return !!parent && allowsManualChildren(parent);
    };

    // ── jsTree (editable tree). Flat data keyed by the stable canonicalId; DnD persists via the
    //    move endpoint, single ops via right-click context menu, checkboxes for bulk delete. ──────
    const $tree = () => window.jQuery(tree);
    const treeApi = () => (window.jQuery && window.jQuery.fn.jstree ? $tree().jstree(true) : null);

    const buildJstreeData = () => definitions
        .filter((d) => !d.isDeleted)
        .map((d) => ({
            id: d.canonicalId,
            parent: d.parentCanonicalId || '#',
            text: esc(d.name || ''),
            type: 'folder',
            state: { opened: true },
            a_attr: { title: d.fullPath || '' }
        }));

    const nodeDepth = (node) => {
        let depth = 0;
        let p = node.parentCanonicalId;
        while (p) { const pn = findNode(p); if (!pn) break; depth++; p = pn.parentCanonicalId; }
        return depth;
    };

    const deleteNode = async (canonicalId, versionToken) => {
        const fd = new FormData();
        fd.append('versionToken', String(versionToken ?? 0));
        const result = await postForm(`/DocumentManagementQmsBaselines/definitions/${baselineId}/${encodeURIComponent(canonicalId)}/delete`, fd);
        return { ok: result.response.ok || result.json?.isSuccessful === true, json: result.json };
    };

    const confirmDeleteSingle = (node) => {
        if (!node) return;
        const run = async () => {
            const r = await deleteNode(node.canonicalId, node.versionToken);
            if (r.ok) { window.showToast?.(t('DeleteSuccess'), 'success'); await refresh(); }
            else window.showToast?.(reasonText(r.json), 'error');
        };
        if (typeof window.showConfirm === 'function') window.showConfirm(t('DeleteConfirm'), run, { type: 'danger', confirmButtonText: t('DeleteNode') });
        else if (window.confirm(t('DeleteConfirm'))) run();
    };

    const updateBulkButton = () => {
        const btn = document.getElementById('btnDeleteSelected');
        if (!btn) return;
        const api = treeApi();
        const count = api ? (api.get_checked() || []).length : 0;
        const countEl = document.getElementById('selectedCount');
        if (countEl) countEl.textContent = count ? `(${count})` : '';
        btn.classList.toggle('d-none', count === 0);
    };

    const bulkDelete = () => {
        const api = treeApi();
        if (!api) return;
        const nodes = (api.get_checked() || []).map(findNode).filter(Boolean);
        if (!nodes.length) return;
        nodes.sort((a, b) => nodeDepth(b) - nodeDepth(a)); // deepest first: single-delete is leaf-only
        const run = async () => {
            let failed = 0, lastErr = null;
            for (const n of nodes) {
                const r = await deleteNode(n.canonicalId, n.versionToken);
                if (!r.ok) { failed++; lastErr = r.json; }
            }
            await refresh();
            if (failed === 0) window.showToast?.(t('DeleteSuccess'), 'success');
            else window.showToast?.(reasonText(lastErr), 'error');
        };
        const msg = (t('BulkDeleteConfirm') || 'Delete {0} selected item(s)?').replace('{0}', nodes.length);
        if (typeof window.showConfirm === 'function') window.showConfirm(msg, run, { type: 'danger', confirmButtonText: t('DeleteNode') });
        else if (window.confirm(msg)) run();
    };

    const onMoveNode = async (event, data) => {
        const canonicalId = data.node.id;
        const node = findNode(canonicalId);
        if (!node) { await refresh(); return; }
        const parentCanonicalId = data.parent === '#' ? '' : data.parent;
        if (!canMoveToParent(node, parentCanonicalId)) {
            window.showToast?.(manualChildrenBlockedMessage(), 'warning');
            await refresh();
            return;
        }

        const fd = new FormData();
        fd.append('parentCanonicalId', parentCanonicalId);
        fd.append('displayOrder', String(data.position ?? 0));
        fd.append('versionToken', String(node.versionToken ?? 0));
        const result = await postForm(`/DocumentManagementQmsBaselines/definitions/${baselineId}/${encodeURIComponent(canonicalId)}/move`, fd);
        if (result.json?.isSuccessful) window.showToast?.(t('MoveSuccess'), 'success');
        else window.showToast?.(reasonText(result.json), 'error');
        await refresh(); // success: reflect server order/paths; failure: revert the visual move
    };

    const contextMenuItems = (jnode) => {
        if (!isDraft) return {};
        const node = findNode(jnode.id);
        const items = {};
        if (canCreate) items.addChild = { label: t('AddChild'), icon: false, _disabled: !allowsManualChildren(node), action: () => openEditor('create', null, jnode.id) };
        if (canEdit) items.edit = { label: t('EditNode'), icon: false, action: () => openEditor('edit', node, null) };
        if (canMove) items.move = { label: t('MoveNode'), icon: false, separator_after: canDelete, action: () => openMove(node) };
        if (canDelete) items.remove = { label: t('DeleteNode'), icon: false, action: () => confirmDeleteSingle(node) };
        return items;
    };

    const emptyStateHtml = () =>
        `<div class="text-center py-5">` +
        `<i class="bx bx-folder-open mb-3" style="font-size:3rem;line-height:1;color:var(--bs-secondary-color,#a7acb2);"></i>` +
        `<h6 class="mb-2">${esc(t('NoDefinitionsHeading'))}</h6>` +
        `<p class="text-muted mb-0 mx-auto" style="max-width:520px;">${esc(t('NoDefinitions'))}</p>` +
        `</div>`;

    // Empty tree: hide the filter input and disable Validate/Publish with an explanatory tooltip
    // (Add Root stays enabled — it is how the first node gets created). pointer-events is re-enabled
    // so the tooltip can show on a disabled <button>.
    const applyEmptyConstraints = (isEmpty) => {
        document.getElementById('designerTreeSearch')?.classList.toggle('d-none', isEmpty);
        const bs = window.bootstrap;
        [['btnValidateDraft', 'ValidateNeedsDefinitions'], ['btnDesignerPublish', 'PublishNeedsDefinitions']].forEach(([id, key]) => {
            const btn = document.getElementById(id);
            if (!btn) return;
            if (isEmpty) {
                btn.disabled = true;
                btn.style.pointerEvents = 'auto';
                btn.setAttribute('data-bs-toggle', 'tooltip');
                btn.setAttribute('data-bs-placement', 'top');
                btn.setAttribute('title', t(key));
                bs?.Tooltip?.getOrCreateInstance(btn);
            } else {
                bs?.Tooltip?.getInstance(btn)?.dispose();
                btn.disabled = false;
                btn.style.pointerEvents = '';
                btn.removeAttribute('data-bs-toggle');
                btn.removeAttribute('title');
            }
        });
    };

    const renderTree = () => {
        if (!tree) return;
        const active = definitions.filter((d) => !d.isDeleted);
        const isEmpty = !active.length;
        applyEmptyConstraints(isEmpty);
        if (treeApi()) $tree().jstree('destroy');
        updateBulkButton();
        if (isEmpty) {
            tree.innerHTML = `<div>${emptyStateHtml()}</div>`;
            return;
        }
        if (!window.jQuery || !window.jQuery.fn.jstree) {
            tree.innerHTML = `<div class="text-muted">${esc(t('NoDefinitions'))}</div>`;
            return;
        }
        tree.innerHTML = '';
        const theme = window.jQuery('html').attr('data-bs-theme') === 'dark' ? 'default-dark' : 'default';
        const plugins = ['types', 'checkbox', 'wholerow', 'search'];
        if (isDraft && canMove) plugins.push('dnd'); // drag-to-move only on editable DRAFT
        if (isDraft && (canCreate || canEdit || canMove || canDelete)) plugins.push('contextmenu');
        const $t = $tree();
        $t.jstree({
            core: {
                themes: { name: theme },
                data: buildJstreeData(),
                check_callback: (operation, node, parent) => {
                    if (operation !== 'move_node') return true;
                    const source = findNode(node?.id);
                    const parentCanonicalId = parent?.id === '#' ? '' : parent?.id;
                    return canMoveToParent(source, parentCanonicalId);
                },
                multiple: true
            },
            plugins: plugins,
            types: {
                default: { icon: 'icon-base bx bx-folder text-warning' },
                folder: { icon: 'icon-base bx bx-folder text-warning' }
            },
            checkbox: {
                cascade: 'down+up+undetermined',
                three_state: true,
                whole_node: false,
                tie_selection: false
            },
            search: { show_only_matches: true, show_only_matches_children: true, close_opened_onclear: false },
            contextmenu: { items: contextMenuItems }
        });
        $t.off('move_node.jstree').on('move_node.jstree', onMoveNode);
        $t.off('check_node.jstree uncheck_node.jstree changed.jstree ready.jstree').on('check_node.jstree uncheck_node.jstree changed.jstree ready.jstree', updateBulkButton);
    };

    const fillParentSelect = (select, currentId) => {
        if (!select) return;
        const options = [`<option value="">${esc(t('RootNode'))}</option>`];
        definitions
            .filter((node) => node.canonicalId !== currentId)
            .sort((a, b) => String(a.fullPath).localeCompare(String(b.fullPath)))
            .forEach((node) => {
                const disabled = allowsManualChildren(node) ? '' : ' disabled';
                options.push(`<option value="${esc(node.canonicalId)}"${disabled}>${esc(node.fullPath || node.name)}</option>`);
            });
        select.innerHTML = options.join('');
    };

    const openEditor = async (mode, node, parentCanonicalId) => {
        const form = document.getElementById('nodeEditorForm');
        form.reset();
        try {
            await initReferenceSelects();
        } catch (error) {
            window.showToast?.(error?.message || t('ErrorOccurred'), 'error');
        }
        document.getElementById('nodeEditorTitle').textContent =
            mode === 'edit' ? t('EditNode') : parentCanonicalId ? t('AddChild') : t('AddRoot');
        document.getElementById('nodeCanonicalId').value = node?.canonicalId || '';
        document.getElementById('nodeParentCanonicalId').value = parentCanonicalId || node?.parentCanonicalId || '';
        document.getElementById('nodeVersionToken').value = node?.versionToken || 0;
        document.getElementById('nodeName').value = node?.name || '';
        document.getElementById('nodeDisplayOrder').value = node?.displayOrder ?? 0;
        document.getElementById('nodePurposeScope').value = node?.purposeScope || '';
        document.getElementById('nodeRequiredByScope').value = node?.requiredByScope || '';
        setReferenceValue('nodeAllowedDocClass', node?.allowedDocClass);
        setReferenceValue('nodeClassification', node?.defaultClassificationLevel);
        setReferenceValue('nodeRetentionHint', node?.defaultRetentionHint);
        document.getElementById('nodeAllowsManualChildren').checked = allowsManualChildren(node);
        document.getElementById('nodeTemplatesAllowed').checked = node?.templatesAllowed ?? false;
        document.getElementById('nodeIsMandatory').checked = node?.isMandatory ?? false;
        document.getElementById('nodeIsProtected').checked = node?.isProtected ?? false;
        editorModal?.show();
    };

    const openMove = (node) => {
        document.getElementById('moveCanonicalId').value = node.canonicalId;
        document.getElementById('moveVersionToken').value = node.versionToken || 0;
        document.getElementById('moveDisplayOrder').value = node.displayOrder ?? 0;
        const select = document.getElementById('moveParentCanonicalId');
        fillParentSelect(select, node.canonicalId);
        select.value = node.parentCanonicalId || '';
        moveModal?.show();
    };

    const refresh = async () => {
        const detail = await request(`/DocumentManagementQmsBaselines/detail/${baselineId}`);
        if (!detail.json?.isSuccessful || !detail.json.data) {
            showError(reasonText(detail.json), detail.json?.correlation_id || detail.json?.correlationId);
            return;
        }
        baseline = detail.json.data;
        isDraft = String(baseline.status || '').toUpperCase() === 'DRAFT';
        text('designerBaselineKey', baseline.baselineReleaseId);
        text('designerVersion', baseline.baselineVersion);
        text('designerDefinitionCount', baseline.definitionCount);
        text('designerCreatedAt', fmtDate(baseline.createdAt));
        const subtitle = document.getElementById('qmsDesignerSubtitle');
        if (subtitle) subtitle.textContent = baseline.baselineReleaseId || '';
        statusBadge();
        document.getElementById('btnAddRoot')?.classList.toggle('d-none', !isDraft);
        document.getElementById('btnValidateDraft')?.classList.toggle('d-none', !isDraft);
        document.getElementById('btnDesignerPublish')?.classList.toggle('d-none', !(isDraft && canPublish));

        const defs = await request(`/DocumentManagementQmsBaselines/definitions/${baselineId}`);
        definitions = defs.json?.isSuccessful ? (defs.json.data || defs.json.Data || []) : [];
        renderTree();
        skeleton?.classList.add('d-none');
        content?.classList.remove('d-none');
    };

    const renderValidation = (data) => {
        const groups = [
            ['SummaryErrors', data.errors],
            ['SummaryWarnings', data.warnings],
            ['SummaryDuplicates', data.duplicateSiblingFindings],
            ['SummaryHierarchy', [...(data.orphanParentFindings || []), ...(data.invalidHierarchyFindings || [])]]
        ];
        const lines = groups
            .filter(([, items]) => items && items.length)
            .map(([label, items]) => `<div class="fw-medium">${esc(t(label))}</div><ul class="mb-2">${items.map((x) => `<li>${esc(x)}</li>`).join('')}</ul>`);
        validationSummary.className = `alert ${data.valid ? 'alert-success' : 'alert-warning'}`;
        validationSummary.innerHTML = data.valid ? esc(t('DraftTreeValid')) : lines.join('');
        validationSummary.classList.remove('d-none');
        window.showToast?.(data.valid ? t('DraftTreeValid') : t('DraftTreeInvalid'), data.valid ? 'success' : 'warning');
    };

    document.getElementById('btnAddRoot')?.addEventListener('click', () => openEditor('create', null, null));

    document.getElementById('btnDeleteSelected')?.addEventListener('click', bulkDelete);

    let searchDebounce;
    document.getElementById('designerTreeSearch')?.addEventListener('input', (event) => {
        const value = event.target.value || '';
        clearTimeout(searchDebounce);
        searchDebounce = setTimeout(() => treeApi()?.search(value), 200);
    });

    document.getElementById('nodeEditorForm')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const form = event.currentTarget;
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }
        const canonicalId = document.getElementById('nodeCanonicalId').value;
        const fd = new FormData(form);
        const url = canonicalId
            ? `/DocumentManagementQmsBaselines/definitions/${baselineId}/${encodeURIComponent(canonicalId)}/edit`
            : `/DocumentManagementQmsBaselines/definitions/${baselineId}`;
        const result = await postForm(url, fd);
        if (result.json?.isSuccessful) {
            editorModal?.hide();
            window.showToast?.(t('RecordSaved'), 'success');
            await refresh();
        } else {
            window.showToast?.(reasonText(result.json), 'error');
        }
    });

    document.getElementById('moveNodeForm')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const canonicalId = document.getElementById('moveCanonicalId').value;
        const node = findNode(canonicalId);
        const parentCanonicalId = document.getElementById('moveParentCanonicalId')?.value || '';
        if (!canMoveToParent(node, parentCanonicalId)) {
            window.showToast?.(manualChildrenBlockedMessage(), 'warning');
            return;
        }

        const fd = new FormData(event.currentTarget);
        const result = await postForm(`/DocumentManagementQmsBaselines/definitions/${baselineId}/${encodeURIComponent(canonicalId)}/move`, fd);
        if (result.json?.isSuccessful) {
            moveModal?.hide();
            window.showToast?.(t('MoveSuccess'), 'success');
            await refresh();
        } else {
            window.showToast?.(reasonText(result.json), 'error');
        }
    });

    document.getElementById('btnValidateDraft')?.addEventListener('click', async () => {
        if (!canValidate) return;
        const fd = new FormData();
        const result = await postForm(`/DocumentManagementQmsBaselines/validate/${baselineId}`, fd);
        if (result.json?.isSuccessful) {
            renderValidation(result.json.data);
        } else {
            window.showToast?.(reasonText(result.json), 'error');
        }
    });

    document.getElementById('btnDesignerPublish')?.addEventListener('click', () => {
        const doPublish = async () => {
            const fd = new FormData();
            fd.append('expectedVersion', baseline?.versionToken || 0);
            const result = await postForm(`/DocumentManagementQmsBaselines/publish/${baselineId}`, fd);
            if (result.json?.isSuccessful) {
                window.showToast?.(t('PublishSuccess'), 'success');
                await refresh();
            } else {
                window.showToast?.(reasonText(result.json), 'error');
            }
        };
        if (typeof window.showConfirm === 'function') {
            window.showConfirm(t('PublishConfirm'), doPublish, { type: 'warning', confirmButtonText: t('Publish') });
        } else if (window.confirm(t('PublishConfirm'))) {
            doPublish();
        }
    });

    document.addEventListener('DOMContentLoaded', refresh);
    document.addEventListener('DOMContentLoaded', () => {
        initReferenceSelects().catch((error) => {
            window.showToast?.(error?.message || t('ErrorOccurred'), 'error');
        });
    });
})();
