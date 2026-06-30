/**
 * MOD-0029-FU01 - Controlled Documents Explorer (TenantShell).
 * 3-panel UI over active, company-instantiated Documentation Structures. Browser calls same-origin MVC proxy
 * only; no direct service port, no browser-supplied tenant header, and no folder-structure mutation.
 */
'use strict';

const ControlledDocumentsList = (function () {
    let L = window.L10n || {};
    const BASE = '/DocumentManagementControlledDocuments';

    let companies = [];
    let structures = [];
    let folders = [];
    let selectedCompanyId = '';
    let selectedStructureId = '';
    let selectedFolderId = '';
    let selectedFolderPath = '';
    let moveModal = null;
    let moveItem = null;
    let moveModalMode = 'move';
    let searchTimer;

    const $id = (id) => document.getElementById(id);
    const text = (v, fallback) => (v === null || v === undefined || v === '' ? (fallback || '-') : String(v));
    const html = (v) => text(v, '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const upper = (v) => String(v || '').toUpperCase();
    const unwrap = (payload) => payload?.data ?? payload?.Data ?? payload;
    const unwrapList = (payload) => {
        const data = unwrap(payload);
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.items)) return data.items;
        if (Array.isArray(data?.Items)) return data.Items;
        if (Array.isArray(data?.results)) return data.results;
        if (Array.isArray(data?.Results)) return data.Results;
        return [];
    };
    const get = (obj, camel, pascal) => obj?.[camel] ?? obj?.[pascal];
    const itemId = (item) => get(item, 'id', 'Id');
    const folderIdOf = (item) => get(item, 'collectionInstanceId', 'CollectionInstanceId') ?? itemId(item);
    const currentVersionIdOf = (item) => get(item, 'currentVersionId', 'CurrentVersionId');
    const currentVersionNumberOf = (item) => get(item, 'currentVersionNumber', 'CurrentVersionNumber') ?? get(item, 'currentVersion', 'CurrentVersion');
    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        if (Number.isNaN(d.getTime())) return html(String(v).slice(0, 10));
        const locale = window.CurrentLanguage || undefined;
        return html(new Intl.DateTimeFormat(locale, { month: 'short', day: '2-digit', year: 'numeric' }).format(d));
    };

    const typeLabel = (value, kind) => {
        const v = upper(value || kind);
        const map = {
            SOP: L.TypeSop,
            WORK_INSTRUCTION: L.TypeWorkInstruction,
            POLICY: L.TypePolicy,
            FORM: L.TypeForm,
            TEMPLATE: L.TypeTemplate,
            OTHER: L.TypeOther
        };
        return `<span class="badge bg-label-secondary">${html(map[v] || value || kind)}</span>`;
    };

    const resultTypeLabel = (kind) => {
        const k = upper(kind);
        if (k === 'FOLDER') return L.FolderResult;
        if (k === 'TEMPLATE') return L.TemplateResult;
        return L.DocumentResult;
    };

    const statusBadge = (value) => {
        const v = upper(value);
        const label = v === 'ACTIVE' ? L.StatusActive : v === 'ARCHIVED' ? L.StatusArchived : (L.Unknown || value);
        const tone = v === 'ACTIVE' ? 'success' : 'secondary';
        return `<span class="badge bg-label-${tone}">${html(label)}</span>`;
    };

    const handleErr = (json) => {
        const map = {
            VALIDATION_FAILED: L.ReasonValidationFailed,
            CONFLICT: L.ReasonConflict,
            PERM_DENIED: L.ReasonPermDenied,
            NOT_FOUND_NON_LEAKAGE: L.ReasonNotFound,
            STORAGE_UNAVAILABLE: L.ReasonStorageUnavailable,
            FEATURE_DISABLED: L.ReasonFeatureDisabled
        };
        const corr = json?.correlation_id ? ` (${L.CorrelationId}: ${json.correlation_id})` : '';
        window.showToast?.(`${text(map[json?.reason_code] || json?.reason_code || L.ErrorOccurred, '')}${corr}`, 'error');
    };

    const fetchJson = async (url, options) => {
        const res = await fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
        const json = await res.json().catch(() => ({}));
        if (!res.ok || json?.isSuccessful === false) {
            throw Object.assign(new Error('request_failed'), { payload: json, status: res.status });
        }
        return json;
    };

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const postForm = async (url, fields) => {
        const fd = new FormData();
        Object.entries(fields || {}).forEach(([k, v]) => fd.append(k, v ?? ''));
        fd.append('__RequestVerificationToken', token());
        const r = await fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' });
        const j = await r.json().catch(() => ({}));
        return { ok: r.ok && j.isSuccessful !== false, json: j };
    };

    const setSelectDisabled = (select, disabled) => {
        if (!select) return;
        select.disabled = disabled;
        select.toggleAttribute('disabled', disabled);
        if (window.jQuery?.fn?.select2) {
            const $select = window.jQuery(select);
            $select.prop('disabled', disabled).trigger('change.select2');
            $select.next('.select2-container')
                .toggleClass('select2-container--disabled', disabled)
                .attr('aria-disabled', disabled ? 'true' : 'false');
        }
    };

    const initSelect2 = () => {
        if (!window.jQuery?.fn?.select2) return;
        const jq = window.jQuery;
        ['#explorerCompanySelect', '#explorerStructureSelect'].forEach((sel) => {
            const $select = jq(sel);
            if (!$select.length) return;
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: jq(document.body),
                width: '100%',
                allowClear: true,
                placeholder: $select.data('placeholder') || ''
            });
        });
    };

    const rowKind = (row) => upper(row?.itemKind || row?.resultType || row?.ResultType || 'DOCUMENT');

    const mergeFolderContents = (payload) => {
        const data = unwrap(payload) || {};
        const docs = data.documents || data.Documents || [];
        const templates = data.templates || data.Templates || [];
        return [
            ...docs.map((d) => Object.assign({ itemKind: 'DOCUMENT' }, d)),
            ...templates.map((t) => Object.assign({ itemKind: 'TEMPLATE', documentType: 'TEMPLATE' }, t))
        ];
    };

    const reloadTable = () => {
        if (typeof renderFolderContents === 'function') renderFolderContents();
    };

    const folderNameOf = (folder) => get(folder, 'name', 'Name')
        || String(get(folder, 'fullPath', 'FullPath') || '').split('/').filter(Boolean).pop()
        || folderIdOf(folder);

    const folderPathOf = (folder) => get(folder, 'fullPath', 'FullPath') || folderNameOf(folder) || '';

    const isDirectChildFolder = (folder, parent) => {
        if (!folder || !parent || String(folderIdOf(folder)) === String(folderIdOf(parent))) return false;

        const parentCanonical = String(get(parent, 'canonicalId', 'CanonicalId') || '');
        const folderParentCanonical = String(get(folder, 'parentCanonicalId', 'ParentCanonicalId') || '');
        if (parentCanonical && folderParentCanonical && folderParentCanonical === parentCanonical) return true;

        const parentId = String(folderIdOf(parent) || '');
        const folderParentId = String(get(folder, 'parentCollectionInstanceId', 'ParentCollectionInstanceId') || '');
        if (parentId && folderParentId && folderParentId === parentId) return true;

        const parentPath = String(folderPathOf(parent) || '').replace(/\/+$/g, '');
        const folderPath = String(folderPathOf(folder) || '').replace(/\/+$/g, '');
        if (!parentPath || !folderPath || !folderPath.startsWith(`${parentPath}/`)) return false;

        const remainder = folderPath.slice(parentPath.length + 1);
        return !!remainder && !remainder.includes('/');
    };

    const getChildFolders = (folderId) => {
        const parent = folders.find((f) => String(folderIdOf(f)) === String(folderId));
        if (!parent) return [];
        return folders
            .filter((folder) => isDirectChildFolder(folder, parent))
            .sort((a, b) => Number(get(a, 'displayOrder', 'DisplayOrder') || 0) - Number(get(b, 'displayOrder', 'DisplayOrder') || 0)
                || String(folderPathOf(a)).localeCompare(String(folderPathOf(b))));
    };

    const structureIdOf = (structure) => get(structure, 'activeStructureId', 'ActiveStructureId')
        || get(structure, 'rootCollectionInstanceId', 'RootCollectionInstanceId')
        || itemId(structure);

    const deriveStructuresFromFolders = async (companyId) => {
        const json = await fetchJson(`${BASE}/collection-instances?companyId=${encodeURIComponent(companyId)}`);
        const activeFolders = unwrapList(json)
            .filter((f) => upper(get(f, 'instanceStatus', 'InstanceStatus')) === 'ACTIVE');

        const grouped = new Map();
        activeFolders.forEach((folder) => {
            const baselineId = get(folder, 'baselineReleaseId', 'BaselineReleaseId') || '__company_structure__';
            if (!grouped.has(baselineId)) grouped.set(baselineId, []);
            grouped.get(baselineId).push(folder);
        });

        return Array.from(grouped.entries()).map(([baselineId, group]) => {
            const sorted = group.slice().sort((a, b) => {
                const pathA = String(get(a, 'fullPath', 'FullPath') || get(a, 'name', 'Name') || '');
                const pathB = String(get(b, 'fullPath', 'FullPath') || get(b, 'name', 'Name') || '');
                const depthA = pathA.split('/').filter(Boolean).length;
                const depthB = pathB.split('/').filter(Boolean).length;
                return depthA - depthB
                    || Number(get(a, 'displayOrder', 'DisplayOrder') || 0) - Number(get(b, 'displayOrder', 'DisplayOrder') || 0)
                    || pathA.localeCompare(pathB);
            });
            const root = sorted[0];
            const rootId = folderIdOf(root);
            return {
                activeStructureId: rootId,
                rootCollectionInstanceId: rootId,
                baselineReleaseId: baselineId === '__company_structure__' ? '' : baselineId,
                displayName: get(root, 'name', 'Name') || get(root, 'fullPath', 'FullPath') || rootId,
                status: 'ACTIVE',
                folderCount: group.length,
                derivedFromCollectionInstances: true
            };
        });
    };

    const loadCompanies = async () => {
        const select = $id('explorerCompanySelect');
        if (!select) return;
        try {
            const json = await fetchJson(`${BASE}/legal-entities`);
            companies = unwrapList(json);
            select.innerHTML = '<option value=""></option>';
            companies.forEach((company) => {
                const id = get(company, 'legalEntityId', 'LegalEntityId') || itemId(company);
                if (!id) return;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = get(company, 'displayName', 'DisplayName') || get(company, 'legalName', 'LegalName') || get(company, 'name', 'Name') || id;
                select.appendChild(opt);
            });
            if (companies.length === 1) {
                select.value = get(companies[0], 'legalEntityId', 'LegalEntityId') || itemId(companies[0]) || '';
                if (window.jQuery?.fn?.select2) window.jQuery(select).trigger('change');
            }
        } catch (err) {
            handleErr(err.payload || {});
        }
    };

    const loadStructures = async (companyId) => {
        const select = $id('explorerStructureSelect');
        if (!select) return;
        structures = [];
        folders = [];
        selectedStructureId = '';
        selectedFolderId = '';
        selectedFolderPath = '';
        select.innerHTML = '<option value=""></option>';
        setSelectDisabled(select, true);
        renderTree();
        setExplorerEnabled(false);
        updateExplorerWorkspace();
        if (!companyId) return;
        try {
            const json = await fetchJson(`${BASE}/documentation-structures?companyId=${encodeURIComponent(companyId)}`);
            structures = unwrapList(json).filter((x) => upper(get(x, 'status', 'Status')) === 'ACTIVE' || upper(get(x, 'status', 'Status')) === 'PROVISIONED');
            if (!structures.length) {
                structures = await deriveStructuresFromFolders(companyId);
            }
            structures.forEach((structure) => {
                const id = structureIdOf(structure);
                if (!id) return;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = get(structure, 'displayName', 'DisplayName') || id;
                select.appendChild(opt);
            });
            setSelectDisabled(select, false);
            if (structures.length === 1) {
                select.value = structureIdOf(structures[0]) || '';
                if (window.jQuery?.fn?.select2) window.jQuery(select).trigger('change.select2');
                await onStructureSelected(select.value);
            } else if (!structures.length) {
                const opt = document.createElement('option');
                opt.value = '';
                opt.textContent = L.NoStructures || '';
                select.appendChild(opt);
                showTreeEmpty(L.NoStructures);
                updateExplorerWorkspace();
            }
        } catch (err) {
            setSelectDisabled(select, false);
            updateExplorerWorkspace();
            handleErr(err.payload || {});
        }
    };

    const loadFolders = async (companyId, structureId) => {
        folders = [];
        if (!companyId || !structureId) {
            renderTree();
            return;
        }
        const json = await fetchJson(`${BASE}/collection-instances?companyId=${encodeURIComponent(companyId)}`);
        const structure = structures.find((s) => String(structureIdOf(s)) === String(structureId));
        const baselineId = get(structure, 'baselineReleaseId', 'BaselineReleaseId');
        const all = unwrapList(json);
        folders = all
            .filter((f) => upper(get(f, 'instanceStatus', 'InstanceStatus')) === 'ACTIVE')
            .filter((f) => !baselineId || String(get(f, 'baselineReleaseId', 'BaselineReleaseId')) === String(baselineId))
            .sort((a, b) => Number(get(a, 'displayOrder', 'DisplayOrder') || 0) - Number(get(b, 'displayOrder', 'DisplayOrder') || 0)
                || String(get(a, 'fullPath', 'FullPath') || '').localeCompare(String(get(b, 'fullPath', 'FullPath') || '')));
        renderTree();
        const rootId = get(structure, 'rootCollectionInstanceId', 'RootCollectionInstanceId') || structureId;
        selectFolder(rootId || folderIdOf(folders[0]), { reload: true });
    };

    const showTreeEmpty = (message) => {
        const empty = $id('folderTreeEmpty');
        const tree = $id('folderTree');
        if (empty) {
            empty.textContent = text(message, '');
            empty.classList.remove('d-none');
        }
        if (tree) tree.innerHTML = '';
        const count = $id('folderTreeCount');
        if (count) count.textContent = '0';
    };

    const renderTree = () => {
        const host = $id('folderTree');
        if (!host) return;
        const count = $id('folderTreeCount');
        if (count) count.textContent = String(folders.length);
        if (!folders.length) {
            showTreeEmpty(selectedStructureId ? L.NoStructures : L.NoFolderSelected);
            return;
        }
        $id('folderTreeEmpty')?.classList.add('d-none');
        if (window.jQuery?.fn?.jstree) {
            const jq = window.jQuery;
            if (jq(host).jstree(true)) jq(host).jstree('destroy');
            const ids = new Set(folders.map((f) => String(get(f, 'canonicalId', 'CanonicalId'))));
            const byCanonical = {};
            folders.forEach((f) => { byCanonical[String(get(f, 'canonicalId', 'CanonicalId'))] = f; });
            // Start the tree COMPACT: with the full structure (many top-level domains × deep sub-folders) a fully
            // expanded tree is unusable and overflows the panel. Open only the path to the currently selected folder
            // so it stays visible; everything else is collapsed and expandable via the jstree chevrons.
            const openCanonicals = new Set();
            let cursor = folders.find((f) => String(folderIdOf(f)) === String(selectedFolderId));
            while (cursor) {
                const pc = get(cursor, 'parentCanonicalId', 'ParentCanonicalId');
                if (!pc || !ids.has(String(pc)) || openCanonicals.has(String(pc))) break;
                openCanonicals.add(String(pc));
                cursor = byCanonical[String(pc)];
            }
            const data = folders.map((folder) => {
                const canonicalId = String(get(folder, 'canonicalId', 'CanonicalId') || folderIdOf(folder));
                const parentRaw = get(folder, 'parentCanonicalId', 'ParentCanonicalId');
                const parentFolder = parentRaw && ids.has(String(parentRaw)) ? byCanonical[String(parentRaw)] : null;
                return {
                    id: String(folderIdOf(folder)),
                    parent: parentFolder ? String(folderIdOf(parentFolder)) : '#',
                    text: html(get(folder, 'name', 'Name') || canonicalId),
                    icon: 'icon-base bx bx-folder text-warning',
                    state: { opened: openCanonicals.has(canonicalId), selected: String(folderIdOf(folder)) === String(selectedFolderId) },
                    a_attr: { title: get(folder, 'fullPath', 'FullPath') || get(folder, 'name', 'Name') || '' }
                };
            });
            const theme = jq('html').attr('data-bs-theme') === 'dark' ? 'default-dark' : 'default';
            jq(host).jstree({
                core: { themes: { name: theme, dots: true }, data, multiple: false },
                plugins: ['types', 'wholerow', 'search'],
                types: { default: { icon: 'icon-base bx bx-folder text-warning' } }
            });
            jq(host).off('activate_node.jstree').on('activate_node.jstree', (_event, node) => selectFolder(node?.node?.id, { reload: true }));
            return;
        }

        host.innerHTML = folders.map((folder) => {
            const id = folderIdOf(folder);
            const path = get(folder, 'fullPath', 'FullPath') || get(folder, 'name', 'Name') || '';
            const depth = Math.max(0, String(path).split('/').filter(Boolean).length - 1);
            const active = String(id) === String(selectedFolderId);
            return `<button type="button" class="list-group-item list-group-item-action d-flex align-items-center gap-2 ${active ? 'active' : ''}" data-folder-id="${html(id)}" style="padding-left:${0.75 + depth * 1.1}rem">
                <i class="icon-base bx bx-folder"></i><span class="text-truncate">${html(get(folder, 'name', 'Name') || path)}</span>
            </button>`;
        }).join('');
        host.querySelectorAll('[data-folder-id]').forEach((btn) => btn.addEventListener('click', () => selectFolder(btn.dataset.folderId, { reload: true })));
    };

    const setExplorerEnabled = (enabled) => {
        ['explorerSearchInput', 'explorerSearchScope', 'btnExplorerSearchReset'].forEach((id) => {
            const el = $id(id);
            if (el) el.disabled = !enabled;
        });
    };

    const updateExplorerWorkspace = () => {
        const ready = !!selectedCompanyId && !!selectedStructureId;
        $id('explorerWorkspace')?.classList.toggle('d-none', !ready);
    };

    const selectFolder = (folderId, options) => {
        if (!folderId) return;
        const folder = folders.find((f) => String(folderIdOf(f)) === String(folderId));
        if (!folder) return;
        selectedFolderId = String(folderIdOf(folder));
        selectedFolderPath = get(folder, 'fullPath', 'FullPath') || get(folder, 'name', 'Name') || '';
        const pathEl = $id('selectedFolderPath');
        if (pathEl) pathEl.textContent = selectedFolderPath;
        setExplorerEnabled(true);
        updateExplorerWorkspace();
        renderTree();
        if (options?.reload) {
            if (isSearchActive()) runSearch();
            else reloadTable();
        }
    };

    const onStructureSelected = async (structureId) => {
        selectedStructureId = structureId || '';
        selectedFolderId = '';
        selectedFolderPath = '';
        clearDetails();
        updateExplorerWorkspace();
        if (!selectedStructureId) {
            setExplorerEnabled(false);
            renderTree();
            reloadTable();
            return;
        }
        try {
            await loadFolders(selectedCompanyId, selectedStructureId);
        } catch (err) {
            handleErr(err.payload || {});
        }
    };

    const isSearchActive = () => !!($id('explorerSearchInput')?.value || '').trim();
    const setSearchMode = (active) => {
        $id('searchResultsPanel')?.classList.toggle('d-none', !active);
        $id('folderContentsPanel')?.classList.toggle('d-none', active);
    };

    const runSearch = async () => {
        const query = ($id('explorerSearchInput')?.value || '').trim();
        if (!query) {
            setSearchMode(false);
            reloadTable();
            return;
        }
        if (!selectedCompanyId || !selectedStructureId) return;
        setSearchMode(true);
        const scope = $id('explorerSearchScope')?.value || 'structure';
        const qs = new URLSearchParams({
            companyId: selectedCompanyId,
            activeStructureId: selectedStructureId,
            includeTemplates: 'true',
            scope,
            query
        });
        if (selectedFolderId && scope !== 'structure') qs.set('collectionInstanceId', selectedFolderId);
        try {
            const json = await fetchJson(`${BASE}/search?${qs.toString()}`);
            renderSearchResults(unwrapList(json));
        } catch (err) {
            handleErr(err.payload || {});
            renderSearchResults([]);
        }
    };

    const renderSearchResults = (items) => {
        const list = $id('searchResultsList');
        const empty = $id('searchResultsEmpty');
        const count = $id('searchResultsCount');
        if (count) count.textContent = String(items.length);
        if (!list || !empty) return;
        empty.classList.toggle('d-none', items.length > 0);
        list.innerHTML = items.map((item) => {
            const kind = upper(get(item, 'resultType', 'ResultType'));
            const id = itemId(item);
            const icon = kind === 'FOLDER' ? 'bx-folder text-warning' : kind === 'TEMPLATE' ? 'bx-copy-alt text-info' : 'bx-file text-primary';
            const path = get(item, 'fullPath', 'FullPath') || '';
            const title = get(item, 'name', 'Name') || get(item, 'title', 'Title') || id;
            if (kind === 'FOLDER') {
                return `<div class="list-group-item list-group-item-action explorer-content-row explorer-folder-row" data-result-kind="${html(kind)}" data-result-id="${html(id)}" data-folder-id="${html(id)}" role="button" tabindex="0">
                    <div class="d-flex align-items-start justify-content-between gap-3">
                        <div class="min-w-0">
                            <div class="d-flex align-items-center gap-2">
                                <i class="icon-base bx ${icon}"></i>
                                <span class="fw-medium text-heading text-truncate">${html(title)}</span>
                            </div>
                            <small class="text-muted d-block text-truncate">${html(path)}</small>
                        </div>
                        <div class="d-inline-flex align-items-center gap-2 flex-shrink-0">
                            <span class="badge bg-label-secondary">${html(resultTypeLabel(kind))}</span>
                            <i class="icon-base bx bx-chevron-right text-muted"></i>
                        </div>
                    </div>
                </div>`;
            }
            const documentType = get(item, 'documentType', 'DocumentType') || (kind === 'TEMPLATE' ? 'TEMPLATE' : resultTypeLabel(kind));
            const status = get(item, 'status', 'Status');
            const targetId = get(item, 'documentId', 'DocumentId') || get(item, 'templateId', 'TemplateId') || id;
            return itemRowHtml({
                id: targetId,
                itemKind: kind === 'TEMPLATE' ? 'TEMPLATE' : 'DOCUMENT',
                title,
                documentType,
                status: status || 'ACTIVE',
                collectionPath: path,
                collectionInstanceId: get(item, 'collectionInstanceId', 'CollectionInstanceId'),
                currentVersionId: get(item, 'currentVersionId', 'CurrentVersionId'),
                currentVersionNumber: get(item, 'currentVersionNumber', 'CurrentVersionNumber'),
                isFavorite: get(item, 'isFavorite', 'IsFavorite')
            });
        }).join('');
    };

    const clearDetails = () => {
        // Details are handled by the dedicated Details page; the explorer no longer owns a side panel.
    };

    const renderDetailActions = (kind, detail, permissions) => {
        const id = itemId(detail);
        const currentVersionId = currentVersionIdOf(detail);
        const canDownload = permissions?.canDownload ?? permissions?.CanDownload ?? true;
        const canShare = permissions?.canShare ?? permissions?.CanShare ?? true;
        const canVersion = permissions?.canUploadNewVersion ?? permissions?.CanUploadNewVersion ?? true;
        const actions = [];
        if (currentVersionId) {
            actions.push(`<button type="button" class="btn btn-sm btn-label-secondary" data-detail-action="preview"><i class="icon-base bx bx-show me-1"></i>${html(L.Preview)}</button>`);
            actions.push(`<button type="button" class="btn btn-sm btn-label-secondary" data-detail-action="download" ${canDownload ? '' : 'disabled'}><i class="icon-base bx bx-download me-1"></i>${html(L.Download)}</button>`);
        }
        if (kind === 'DOCUMENT') {
            actions.push(`<a class="btn btn-sm btn-label-secondary" href="${BASE}/Edit/${html(id)}"><i class="icon-base bx bx-edit me-1"></i>${html(L.EditMetadata)}</a>`);
            actions.push(`<a class="btn btn-sm btn-label-secondary ${canVersion ? '' : 'disabled'}" href="${BASE}/VersionHistory/${html(id)}"><i class="icon-base bx bx-upload me-1"></i>${html(L.UploadNewVersion)}</a>`);
        }
        actions.push(`<button type="button" class="btn btn-sm btn-label-secondary" data-detail-action="copy"><i class="icon-base bx bx-copy me-1"></i>${html(L.CopyToFolder)}</button>`);
        actions.push(`<a class="btn btn-sm btn-label-secondary ${canShare ? '' : 'disabled'}" href="${BASE}/Share/${html(id)}"><i class="icon-base bx bx-share-alt me-1"></i>${html(L.ShareDocument)}</a>`);
        const host = $id('detailActions');
        if (!host) return;
        host.innerHTML = actions.join('');
        host.querySelector('[data-detail-action="preview"]')?.addEventListener('click', () => openPreview(kind, id, currentVersionId));
        host.querySelector('[data-detail-action="download"]')?.addEventListener('click', () => openDownload(kind, id, currentVersionId));
        host.querySelector('[data-detail-action="copy"]')?.addEventListener('click', () => openFolderModal(id, Object.assign({ itemKind: kind }, detail), 'copy'));
    };

    const renderVersions = (versions, kind, itemIdValue) => {
        const host = $id('detailVersions');
        if (!host) return;
        if (!versions.length) {
            host.innerHTML = `<div class="text-muted small py-2">${html(L.NotAvailable)}</div>`;
            return;
        }
        host.innerHTML = versions.slice(0, 5).map((version) => {
            const versionId = itemId(version);
            return `<div class="list-group-item px-0">
                <div class="d-flex align-items-center justify-content-between gap-2">
                    <div>
                        <span class="fw-medium">${html(L.Version)} ${html(get(version, 'versionNumber', 'VersionNumber'))}</span>
                        <small class="d-block text-muted">${formatDate(get(version, 'uploadedAt', 'UploadedAt'))}</small>
                    </div>
                    <button type="button" class="btn btn-sm btn-icon btn-label-secondary" data-version-download="${html(versionId)}" title="${html(L.Download)}">
                        <i class="icon-base bx bx-download"></i>
                    </button>
                </div>
            </div>`;
        }).join('');
        host.querySelectorAll('[data-version-download]').forEach((btn) => btn.addEventListener('click', () => openDownload(kind, itemIdValue, btn.dataset.versionDownload)));
    };

    const openDetails = async (kind, id) => {
        if (!id) return;
        window.location.href = `${BASE}/Details/${id}`;
    };

    const openVersionHistory = (id) => {
        if (!id) return;
        window.location.href = `${BASE}/VersionHistory/${id}`;
    };

    const openPreview = (kind, id, versionId) => {
        if (!id || !versionId) {
            window.showToast?.(L.PreviewUnavailable, 'warning');
            return;
        }
        const url = upper(kind) === 'TEMPLATE' ? `${BASE}/templates/preview/${id}/${versionId}` : `${BASE}/preview/${id}/${versionId}`;
        window.open(url, '_blank');
    };

    const openDownload = (kind, id, versionId) => {
        if (!id || !versionId) return;
        const url = upper(kind) === 'TEMPLATE' ? `${BASE}/templates/download/${id}/${versionId}` : `${BASE}/download/${id}/${versionId}`;
        window.open(url, '_blank');
    };

    const applyFavoriteState = (id, isFavorite) => {
        let updated = false;
        document.querySelectorAll('.explorer-item-row').forEach((rowEl) => {
            if (String(rowEl.dataset.itemId || '') !== String(id || '')) return;
            try {
                const row = JSON.parse(rowEl.dataset.json || '{}');
                row.isFavorite = isFavorite;
                row.IsFavorite = isFavorite;
                rowEl.outerHTML = itemRowHtml(row);
                updated = true;
            } catch (_) {
                updated = false;
            }
        });
        return updated;
    };

    const toggleFavorite = async (id) => {
        if (!id) return;
        const { ok, json } = await postForm(`${BASE}/favorite/${id}`, {});
        if (ok) {
            const result = json?.data || json?.Data || {};
            const isFavorite = !!(result.isFavorite ?? result.IsFavorite);
            if (!applyFavoriteState(id, isFavorite)) reloadTable();
            window.showToast?.(text(isFavorite ? L.Favorited : L.Unfavorited, ''), 'success');
        } else {
            handleErr(json);
        }
    };

    const confirmDelete = (id, row) => {
        if (!id || rowKind(row) !== 'DOCUMENT') return;
        const run = async () => {
            const { ok, json } = await postForm(`${BASE}/delete/${id}`, {});
            if (ok) {
                reloadTable();
                window.showToast?.(text(L.DeleteSuccess, ''), 'success');
            } else {
                handleErr(json);
            }
        };
        if (window.showConfirm) window.showConfirm(text(L.DeleteConfirm, ''), run, { entityName: get(row, 'title', 'Title') || '', type: 'danger', confirmButtonText: L.Delete });
    };

    const openFolderModal = async (id, row, mode) => {
        if (!id) return;
        moveItem = { id, row, kind: rowKind(row) };
        moveModalMode = mode === 'copy' ? 'copy' : 'move';
        const select = $id('moveTargetFolder');
        const modalEl = $id('moveDocumentModal');
        const titleEl = $id('moveDocumentModalTitle');
        const confirmBtn = $id('btnMoveConfirm');
        if (!select || !modalEl) return;
        if (titleEl) titleEl.textContent = text(moveModalMode === 'copy' ? L.CopyToFolder : L.MoveToFolder, '');
        if (confirmBtn) confirmBtn.textContent = text(moveModalMode === 'copy' ? L.Copy : L.Move, '');
        select.innerHTML = `<option value="">${html(L.SelectTargetFolder)}</option>`;
        folders.forEach((folder) => {
            const idValue = folderIdOf(folder);
            if (moveModalMode === 'move' && String(idValue) === String(folderIdOf(row))) return;
            const opt = document.createElement('option');
            opt.value = idValue;
            opt.textContent = get(folder, 'fullPath', 'FullPath') || get(folder, 'name', 'Name') || idValue;
            select.appendChild(opt);
        });
        moveModal = moveModal || (window.bootstrap ? new bootstrap.Modal(modalEl) : null);
        moveModal?.show();
    };

    const submitFolderOp = async () => {
        const target = $id('moveTargetFolder')?.value;
        if (!moveItem?.id || !target) {
            window.showToast?.(text(L.ReasonValidationFailed, ''), 'error');
            return;
        }
        const isTemplate = moveItem.kind === 'TEMPLATE';
        if (moveModalMode === 'move' && isTemplate) {
            window.showToast?.(text(L.FolderOperationsDeferred, ''), 'warning');
            return;
        }
        const url = isTemplate ? `${BASE}/templates/copy/${moveItem.id}` : (moveModalMode === 'copy' ? `${BASE}/copy/${moveItem.id}` : `${BASE}/move/${moveItem.id}`);
        const { ok, json } = await postForm(url, { targetCollectionInstanceId: target });
        if (ok) {
            moveModal?.hide();
            reloadTable();
            window.showToast?.(text(moveModalMode === 'copy' ? L.RecordSaved : L.MoveSuccess, ''), 'success');
        } else {
            handleErr(json);
        }
    };

    // Compact list row builders (subfolders + documents + templates), aligned with the search-results surface.
    const folderRowHtml = (f) => {
        const id = folderIdOf(f);
        return `<div class="list-group-item list-group-item-action explorer-content-row explorer-folder-row" data-folder-id="${html(id)}" role="button" tabindex="0">
            <div class="d-flex align-items-start justify-content-between gap-3">
                <div class="min-w-0">
                    <div class="d-flex align-items-center gap-2">
                        <i class="icon-base bx bx-folder text-warning"></i>
                        <span class="fw-medium text-heading text-truncate">${html(folderNameOf(f))}</span>
                    </div>
                    <small class="text-muted d-block text-truncate">${html(folderPathOf(f))}</small>
                </div>
                <div class="d-inline-flex align-items-center gap-2 flex-shrink-0">
                    <span class="badge bg-label-secondary">${html(L.FolderResult)}</span>
                    <i class="icon-base bx bx-chevron-right text-muted"></i>
                </div>
            </div>
        </div>`;
    };

    const itemRowHtml = (row) => {
        const kind = rowKind(row);
        const id = itemId(row);
        const icon = kind === 'TEMPLATE' ? 'bx-copy-alt text-info' : 'bx-file text-primary';
        const isFav = !!get(row, 'isFavorite', 'IsFavorite');
        const isFavoriteDocument = kind === 'DOCUMENT' && isFav;
        const favoriteLabel = html(L.Favorite || 'Favorite');
        const favoriteMarker = isFavoriteDocument
            ? `<span class="badge bg-label-warning explorer-favorite-badge"><i class="icon-base bx bxs-star"></i>${favoriteLabel}</span>`
            : '';
        const hasVersion = !!currentVersionIdOf(row);
        const rowJson = html(JSON.stringify(row));
        const attrs = { 'data-id': id, 'data-json': rowJson };
        const title = get(row, 'title', 'Title') || get(row, 'name', 'Name') || id;
        const path = get(row, 'collectionPath', 'CollectionPath') || selectedFolderPath;
        const version = currentVersionNumberOf(row);
        const modifiedAt = formatDate(get(row, 'updatedAt', 'UpdatedAt') || get(row, 'createdAt', 'CreatedAt'));
        const actions = [];
        actions.push({ key: 'details', className: 'btn-text-secondary', icon: 'bx bx-info-circle', text: text(L.ViewDetails, ''), attrs });
        if (kind === 'DOCUMENT') {
            actions.push({
                key: 'favorite',
                className: isFav ? 'text-warning fw-medium' : '',
                icon: isFav ? 'bx bxs-star text-warning' : 'bx bx-star',
                text: text(isFav ? L.Unfavorite : L.Favorite, ''),
                attrs: Object.assign({}, attrs, {
                    'aria-label': text(isFav ? L.Unfavorite : L.Favorite, ''),
                    title: text(isFav ? L.Unfavorite : L.Favorite, '')
                })
            });
        }
        if (hasVersion) {
            actions.push({ key: 'preview', icon: 'bx bx-show', text: text(L.Preview, ''), attrs });
            actions.push({ key: 'download', icon: 'bx bx-download', text: text(L.Download, ''), attrs });
        }
        actions.push({ key: 'versions', icon: 'bx bx-history', text: text(L.VersionHistory, ''), attrs });
        actions.push({ key: 'copy', icon: 'bx bx-copy', text: text(L.CopyToFolder, ''), attrs });
        if (kind === 'DOCUMENT') {
            actions.push({ key: 'move', icon: 'bx bx-move', text: text(L.MoveToFolder, ''), attrs });
            actions.push({ key: 'share', icon: 'bx bx-share-alt', text: text(L.ShareDocument, ''), attrs });
            actions.push({ key: 'edit', icon: 'bx bx-edit', text: text(L.EditMetadata, ''), attrs });
            actions.push({ key: 'delete', className: 'text-danger', icon: 'bx bx-trash', text: text(L.Delete, ''), attrs });
        }
        const meta = [
            path ? html(path) : '',
            modifiedAt !== '-' ? html(modifiedAt) : '',
            version ? `v${html(version)}` : ''
        ].filter(Boolean).join(' <span class="mx-1 text-muted">·</span> ');
        return `<div class="list-group-item list-group-item-action explorer-content-row explorer-item-row ${isFavoriteDocument ? 'is-favorite' : ''}" data-item-id="${html(id)}" data-item-kind="${html(kind)}" data-json="${rowJson}">
            <div class="d-flex align-items-start justify-content-between gap-3">
                <div class="min-w-0 explorer-open" role="button" tabindex="0">
                    <div class="d-flex align-items-center gap-2">
                        <i class="icon-base bx ${icon}"></i>
                        <span class="fw-medium text-heading text-truncate">${html(title)}</span>
                        ${favoriteMarker}
                    </div>
                    <small class="text-muted d-block text-truncate">${meta}</small>
                </div>
                <div class="d-inline-flex align-items-center gap-2 flex-shrink-0">
                    ${typeLabel(get(row, 'documentType', 'DocumentType'), kind)}
                    ${statusBadge(get(row, 'status', 'Status'))}
                    <span class="cell-fit">${window.DitenDataTable.renderActions(actions)}</span>
                </div>
            </div>
        </div>`;
    };

    const renderFolderContents = async () => {
        $id('skeleton-loader')?.classList.add('d-none');
        const body = $id('folderContentsList');
        const emptyEl = $id('folderContentsEmpty');
        if (!body) return;
        if (!selectedFolderId) { body.innerHTML = ''; emptyEl?.classList.remove('d-none'); return; }

        // Subfolders = direct children of the selected node (read-only navigation; no tree mutation).
        const subfolders = getChildFolders(selectedFolderId);

        let contents = [];
        try {
            const json = await fetchJson(`${BASE}/folder-documents?collectionInstanceId=${encodeURIComponent(selectedFolderId)}`);
            contents = mergeFolderContents(json);
        } catch (err) { handleErr(err.payload || {}); }

        const rows = [...subfolders.map(folderRowHtml), ...contents.map(itemRowHtml)];
        body.innerHTML = rows.join('');
        emptyEl?.classList.toggle('d-none', rows.length > 0);
    };

    const initDataTable = () => {
        $id('skeleton-loader')?.classList.add('d-none');
        if (!window.DitenDataTable) return;

        const rowActions = {
            details: ({ id, row }) => openDetails(rowKind(row), id),
            preview: ({ id, row }) => openPreview(rowKind(row), id, currentVersionIdOf(row)),
            download: ({ id, row }) => openDownload(rowKind(row), id, currentVersionIdOf(row)),
            versions: ({ id }) => openVersionHistory(id),
            edit: ({ id }) => { if (id) window.location.href = `${BASE}/Edit/${id}`; },
            share: ({ id }) => { if (id) window.location.href = `${BASE}/Share/${id}`; },
            favorite: ({ id }) => toggleFavorite(id),
            move: ({ id, row }) => openFolderModal(id, row, 'move'),
            copy: ({ id, row }) => openFolderModal(id, row, 'copy'),
            delete: ({ id, row }) => confirmDelete(id, row)
        };

        const bindExplorerList = (list) => {
            if (!list) return;
            // Row navigation: folder -> drill in; document/template name -> dedicated details page.
            list.addEventListener('click', (e) => {
                if (e.target.closest('[data-row-action], .dropdown-menu, .dropdown-toggle')) return;
                const folderRow = e.target.closest('.explorer-folder-row');
                if (folderRow) { selectFolder(folderRow.dataset.folderId, { reload: true }); return; }
                const open = e.target.closest('.explorer-open');
                const itemRow = open?.closest('.explorer-item-row');
                if (itemRow) openDetails(itemRow.dataset.itemKind, itemRow.dataset.itemId);
            });

            window.DitenDataTable.bindActionDispatcher({
                tableEl: list,
                onRowAction: rowActions
            });
        };

        bindExplorerList($id('folderContentsList'));
        bindExplorerList($id('searchResultsList'));

        renderFolderContents();
    };

    const bindExplorerEvents = () => {
        const companySelect = $id('explorerCompanySelect');
        const structureSelect = $id('explorerStructureSelect');
        if (window.jQuery?.fn?.select2) {
            window.jQuery(companySelect).on('change.controlled-documents', function () {
                selectedCompanyId = this.value || '';
                loadStructures(selectedCompanyId);
            });
            window.jQuery(structureSelect).on('change.controlled-documents', function () {
                onStructureSelected(this.value || '');
            });
        } else {
            companySelect?.addEventListener('change', (e) => {
                selectedCompanyId = e.target.value || '';
                loadStructures(selectedCompanyId);
            });
            structureSelect?.addEventListener('change', (e) => onStructureSelected(e.target.value || ''));
        }
        $id('explorerSearchInput')?.addEventListener('input', () => {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(runSearch, 250);
        });
        $id('explorerSearchScope')?.addEventListener('change', () => { if (isSearchActive()) runSearch(); });
        $id('btnExplorerSearchReset')?.addEventListener('click', () => {
            const input = $id('explorerSearchInput');
            if (input) input.value = '';
            setSearchMode(false);
            reloadTable();
        });
        $id('btnMoveConfirm')?.addEventListener('click', submitFolderOp);
        $id('btnExplorerFolderOperations')?.addEventListener('click', () => window.showToast?.(L.FolderOperationsDeferred, 'info'));
    };

    const init = async () => {
        syncL10n();
        initSelect2();
        bindExplorerEvents();
        initDataTable();
        clearDetails();
        setExplorerEnabled(false);
        updateExplorerWorkspace();
        await loadCompanies();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ControlledDocumentsList.init());

// ── Explorer single-card layout: collapse/expand panels + Windows-like drag-resize ──────────────────
(function () {
    'use strict';
    const STORE_KEY = 'cd-explorer-tree-width';
    const DEFAULT_WIDTH = 280;
    const MIN_WIDTH = 180;
    const MAX_WIDTH = 520;

    const init = () => {
        const shell = document.getElementById('explorerShell');
        if (!shell || shell.dataset.layoutBound === '1') return;
        shell.dataset.layoutBound = '1';

        const treePanel = document.getElementById('folderTreePanel');
        const treeExpand = document.getElementById('folderTreeExpandBtn');
        const treeCollapse = document.getElementById('folderTreeCollapseBtn');
        const resizer = document.getElementById('explorerResizer');

        // Restore persisted folder-tree width.
        const saved = parseInt(localStorage.getItem(STORE_KEY) || '', 10);
        if (treePanel && Number.isFinite(saved) && saved >= MIN_WIDTH && saved <= MAX_WIDTH) {
            treePanel.style.width = saved + 'px';
        }

        const setTreeCollapsed = (collapsed) => {
            treePanel?.classList.toggle('is-collapsed', collapsed);
            resizer?.classList.toggle('is-hidden', collapsed);
            treeExpand?.classList.toggle('d-none', !collapsed);
        };

        treeCollapse?.addEventListener('click', () => setTreeCollapsed(true));
        treeExpand?.addEventListener('click', () => setTreeCollapsed(false));

        // Drag-resize the folder-tree width via the vertical handle.
        if (resizer && treePanel) {
            let startX = 0;
            let startWidth = 0;
            const onMove = (e) => {
                const clientX = e.touches ? e.touches[0].clientX : e.clientX;
                let w = startWidth + (clientX - startX);
                w = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, w));
                treePanel.style.width = w + 'px';
            };
            const onUp = () => {
                resizer.classList.remove('is-dragging');
                document.body.style.userSelect = '';
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup', onUp);
                document.removeEventListener('touchmove', onMove);
                document.removeEventListener('touchend', onUp);
                const w = parseInt(treePanel.style.width || '', 10);
                if (Number.isFinite(w)) { try { localStorage.setItem(STORE_KEY, String(w)); } catch (_) { } }
            };
            const onDown = (e) => {
                startX = e.touches ? e.touches[0].clientX : e.clientX;
                startWidth = treePanel.getBoundingClientRect().width;
                resizer.classList.add('is-dragging');
                document.body.style.userSelect = 'none';
                document.addEventListener('mousemove', onMove);
                document.addEventListener('mouseup', onUp);
                document.addEventListener('touchmove', onMove, { passive: true });
                document.addEventListener('touchend', onUp);
                e.preventDefault();
            };
            resizer.addEventListener('mousedown', onDown);
            resizer.addEventListener('touchstart', onDown, { passive: false });
            // Double-click resets to the default width.
            resizer.addEventListener('dblclick', () => { treePanel.style.width = DEFAULT_WIDTH + 'px'; try { localStorage.setItem(STORE_KEY, String(DEFAULT_WIDTH)); } catch (_) { } });
            // Keyboard resize (left/right arrows) for accessibility.
            resizer.addEventListener('keydown', (e) => {
                if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
                const cur = treePanel.getBoundingClientRect().width;
                const next = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, cur + (e.key === 'ArrowRight' ? 16 : -16)));
                treePanel.style.width = next + 'px';
                try { localStorage.setItem(STORE_KEY, String(Math.round(next))); } catch (_) { }
                e.preventDefault();
            });
        }
    };

    // The workspace starts hidden (d-none) and is revealed once a structure loads; bind as soon as the DOM is
    // ready (idempotent guard handles re-entry).
    document.addEventListener('DOMContentLoaded', init);
    document.addEventListener('click', (e) => { if (e.target.closest('#explorerShell')) init(); }, true);
})();
