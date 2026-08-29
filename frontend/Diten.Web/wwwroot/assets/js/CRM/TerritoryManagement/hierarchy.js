'use strict';

(function () {
    const tableEl = document.getElementById('dt-territoryhierarchy');
    const payloadEl = document.getElementById('territory-hierarchy-data');
    if (!tableEl || !payloadEl) return;

    let payload;
    try {
        payload = JSON.parse(payloadEl.textContent || '{}');
    } catch (error) {
        console.error('[TerritoryHierarchy] Page data could not be parsed.', error);
        return;
    }

    const labels = payload.labels || {};
    const nodes = Array.isArray(payload.nodes) ? payload.nodes : [];
    const canManageNode = payload.canManageNode === true;
    const modelId = payload.modelId || '';
    const apiUrl = window.API?.crm ?? window.ApiBaseUrl;
    const authHeaders = (json = false) => window.DitenDataTable?.getAuthHeaders?.(json) || {};
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'TerritoryHierarchy' };
    let appliedFilters = { level: [], status: [] };
    let savedViewRecord = null;
    let savedViewState = null;
    let editingId = null;
    const createNodeOffcanvasEl = document.getElementById('offcanvasCreateNode');
    const createNodeOffcanvas = createNodeOffcanvasEl
        ? bootstrap.Offcanvas.getOrCreateInstance(createNodeOffcanvasEl)
        : null;

    const escapeHtml = (value) => {
        const element = document.createElement('span');
        element.textContent = value ?? '';
        return element.innerHTML;
    };

    const formatDate = (value) => {
        if (!value) return '—';
        const text = String(value);
        return text.length >= 10 ? text.slice(0, 10) : text;
    };

    const statusBadgeClass = (status) => ({
        draft: 'bg-label-secondary',
        active: 'bg-label-success',
        superseded: 'bg-label-warning',
        retired: 'bg-label-dark',
        inactive: 'bg-label-secondary',
        expired: 'bg-label-danger',
        archived: 'bg-label-dark'
    })[String(status || '').toLowerCase()] || 'bg-label-primary';

    const executeLifecycle = (endpoint, label, entityName, type) => {
        window.showConfirm?.(label, async () => {
            try {
                const response = await fetch(`${apiUrl}${endpoint}`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: authHeaders(true),
                    body: JSON.stringify({
                        reason: label,
                        correlationId: `ui-territory-life-${crypto.randomUUID?.() || Date.now()}`
                    })
                });
                if (!response.ok) {
                    let message = labels.gatewayError || '';
                    const body = await response.json();
                    message = body?.errors?.join(', ') || body?.Errors?.join(', ') || message;
                    window.showToast?.(message, 'error');
                    return;
                }
                window.location.reload();
            } catch (error) {
                console.error('[TerritoryHierarchy] Lifecycle action failed.', error);
                window.showToast?.(labels.gatewayError || '', 'error');
            }
        }, {
            entityName: entityName || payload.modelName || String(modelId),
            type: type || 'info',
            subtext: labels.lifecycleConfirmation || '',
            confirmButtonText: label
        });
    };

    const populateSelect = (id, options, includeBlank = true) => {
        const select = document.getElementById(id);
        if (!select) return;
        select.replaceChildren();
        if (includeBlank) select.add(new Option('', ''));
        (Array.isArray(options) ? options : []).forEach((option) => {
            select.add(new Option(option.text || option.value || '', option.value || ''));
        });
    };

    const normalizeArray = (value) => Array.from(new Set(
        (Array.isArray(value) ? value : (value ? [value] : []))
            .map((item) => String(item).trim())
            .filter(Boolean)
    ));

    const syncMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;

        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        if (!$summary.length) $summary = $('<span class="dt-inline-filter-multi__summary"></span>').prependTo($selection);
        if (!$actions.length) $actions = $('<span class="dt-inline-filter-multi__actions"></span>').appendTo($selection);
        if (!$count.length) $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>').appendTo($actions);
        if (!$selection.find('.select2-selection__arrow').length) {
            $selection.append('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>');
        }

        const values = normalizeArray($select.val());
        const placeholder = String($select.data('placeholder') || '');
        const selectedTexts = ($select.select2('data') || []).map((item) => String(item.text || '')).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', values.length > 0);
        $count.toggleClass('d-none', values.length === 0).text(String(values.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (values.length) {
            $('<span class="dt-multi-clear-btn" role="button" title="Reset">&times;</span>')
                .on('mousedown', (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    $select.val(null).trigger('change');
                })
                .appendTo($actions);
        }
    };

    const initFilterSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        $('#filterTerritoryLevel, #filterStatus').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $select.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
            $select.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary($select));
            requestAnimationFrame(() => syncMultiSelectSummary($select));
        });
    };

    const getViewState = (api) => ({
        filters: appliedFilters,
        search: api.search(),
        colVis: [1, 2, 3, 4, 5, 6, 7].reduce((result, index) => {
            result[index] = api.column(index).visible();
            return result;
        }, {}),
        columnOrder: api.colReorder?.order?.() || null
    });

    const getSavedViewDefinition = (record) => {
        const raw = record?.viewDefinition ?? record?.ViewDefinition ?? {};
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (_error) { return {}; }
        }
        return raw || {};
    };

    const loadSavedView = async () => {
        if (!personalizationClient?.getViews) return;
        try {
            const response = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const views = Array.isArray(response) ? response : (response?.data || response?.Data || []);
            savedViewRecord = views.find((view) => view.isDefault === true || view.IsDefault === true) || views[0] || null;
            savedViewState = savedViewRecord ? getSavedViewDefinition(savedViewRecord) : null;
        } catch (error) {
            if (!error?.authHandled) console.error('[TerritoryHierarchy SaveView] Load failed.', error);
        }
    };

    const saveCurrentView = async (api) => {
        if (!personalizationClient?.saveView) return;
        const viewDefinition = getViewState(api);
        const request = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (savedViewRecord?.viewName || savedViewRecord?.ViewName || labels.saveView || 'Default').trim(),
            viewDefinition,
            isDefault: true,
            visibility: 'private'
        };
        const id = savedViewRecord?.id || savedViewRecord?.Id;
        const response = id
            ? await personalizationClient.updateView(id, request)
            : await personalizationClient.saveView(request);
        savedViewRecord = response?.data || response?.Data || response || request;
        savedViewState = viewDefinition;
        document.querySelector('.dt-save-filter-btn')?.classList.add('d-none');
        window.showToast?.(labels.recordSaved || labels.saveView || '', 'success');
    };

    const getAppliedFilterCount = () =>
        [appliedFilters.level, appliedFilters.status].filter((value) => normalizeArray(value).length > 0).length;

    const toggleInlineFilter = () => {
        const collapse = document.getElementById('inlineFilterCollapse');
        if (!collapse) return;
        bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).toggle();
    };

    const bindInlineFilterToggle = () => {
        const button = document.querySelector('.dt-filter-btn');
        const collapse = document.getElementById('inlineFilterCollapse');
        if (!button || !collapse || button.dataset.hierarchyFilterBound === '1') return;
        button.dataset.hierarchyFilterBound = '1';
        button.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            toggleInlineFilter();
        });
        collapse.addEventListener('shown.bs.collapse', () => button.setAttribute('aria-expanded', 'true'));
        collapse.addEventListener('hidden.bs.collapse', () => button.setAttribute('aria-expanded', 'false'));
    };

    const createDefaultTerritoryCode = () => {
        const now = new Date();
        const pad = (value) => String(value).padStart(2, '0');
        const country = String(payload.countryScope || 'TR').trim().toUpperCase().replace(/[^A-Z0-9]+/g, '-');
        return `${country}-TN-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
    };

    const today = () => {
        const date = new Date();
        const offset = date.getTimezoneOffset();
        return new Date(date.getTime() - offset * 60000).toISOString().slice(0, 10);
    };

    const initCreateNodeSelects = () => {
        populateSelect('tnTerritoryLevel', payload.levelOptions);
        populateSelect('tnParentTerritoryId', payload.parentOptions);
        populateSelect('tnAnchorAccountId', payload.anchorAccountOptions);
        populateSelect('tnPlanningCenterType', payload.planningCenterTypeOptions);
        if (!window.jQuery || !$.fn.select2) return;
        ['tnTerritoryLevel', 'tnParentTerritoryId', 'tnAnchorAccountId', 'tnPlanningCenterType'].forEach((id) => {
            const $select = $(`#${id}`);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $('#offcanvasCreateNode'),
                allowClear: true,
                width: '100%'
            });
        });
    };

    const toggleMicrozoneSection = () => {
        const isMicrozone = String(document.getElementById('tnTerritoryLevel')?.value || '').toLowerCase() === 'microzone';
        document.getElementById('tnMicrozoneSection')?.classList.toggle('d-none', !isMicrozone);
        if (!isMicrozone) {
            ['tnAnchorAccountId', 'tnPlanningCenterType'].forEach((id) => {
                const select = document.getElementById(id);
                if (select) select.value = '';
            });
            if (window.jQuery) $('#tnAnchorAccountId, #tnPlanningCenterType').trigger('change');
            const notes = document.getElementById('tnClusterNotes');
            if (notes) notes.value = '';
        }
    };

    const resetNodeForm = () => {
        const form = document.getElementById('formTerritoryNode');
        form?.reset();
        form?.classList.remove('was-validated');
        document.getElementById('formTerritoryNodeAlert')?.classList.add('d-none');
        initCreateNodeSelects();
        ['tnNodeId', 'tnCountryCode', 'tnDivisionCode', 'tnRegionCode', 'tnAreaCode', 'tnZoneCode', 'tnMicroZoneCode']
            .forEach((id) => { const input = document.getElementById(id); if (input) input.value = ''; });
    };

    const openCreateNode = () => {
        editingId = null;
        resetNodeForm();
        document.getElementById('offcanvasCreateNodeLabel').textContent = labels.createNode || '';
        document.getElementById('btnSaveTerritoryNode').textContent = labels.createNode || '';
        document.getElementById('tnTerritoryCode').value = createDefaultTerritoryCode();
        document.getElementById('tnEffectiveFrom').value = today();
        document.getElementById('tnSortOrder').value = '0';
        toggleMicrozoneSection();
        createNodeOffcanvas?.show();
    };

    const setSelectValue = (id, value) => {
        const select = document.getElementById(id);
        if (select) select.value = value || '';
        if (window.jQuery) $(`#${id}`).val(value || null).trigger('change');
    };

    const openEditNode = (node) => {
        if (!node?.id) return;
        editingId = String(node.id);
        resetNodeForm();
        document.getElementById('offcanvasCreateNodeLabel').textContent = labels.edit || '';
        document.getElementById('btnSaveTerritoryNode').textContent = labels.update || labels.edit || '';
        document.getElementById('tnNodeId').value = editingId;
        document.getElementById('tnTerritoryCode').value = node.territoryCode || '';
        document.getElementById('tnName').value = node.name || '';
        setSelectValue('tnTerritoryLevel', node.territoryLevel);
        setSelectValue('tnParentTerritoryId', node.parentTerritoryId);
        document.getElementById('tnSortOrder').value = node.sortOrder ?? 0;
        document.getElementById('tnEffectiveFrom').value = formatDate(node.effectiveFrom) === '—' ? '' : formatDate(node.effectiveFrom);
        document.getElementById('tnEffectiveTo').value = formatDate(node.effectiveTo) === '—' ? '' : formatDate(node.effectiveTo);
        ['CountryCode', 'DivisionCode', 'RegionCode', 'AreaCode', 'ZoneCode', 'MicroZoneCode'].forEach((name) => {
            document.getElementById(`tn${name}`).value = node[name.charAt(0).toLowerCase() + name.slice(1)] || '';
        });
        setSelectValue('tnAnchorAccountId', node.microZoneProfile?.anchorAccountId);
        setSelectValue('tnPlanningCenterType', node.microZoneProfile?.planningCenterType);
        document.getElementById('tnClusterNotes').value = node.microZoneProfile?.clusterNotes || '';
        toggleMicrozoneSection();
        createNodeOffcanvas?.show();
    };

    const submitCreateNode = async () => {
        const form = document.getElementById('formTerritoryNode');
        if (!form) return;
        form.classList.add('was-validated');
        if (!form.checkValidity()) return;

        const saveButton = document.getElementById('btnSaveTerritoryNode');
        const alert = document.getElementById('formTerritoryNodeAlert');
        saveButton.disabled = true;
        alert.classList.add('d-none');
        try {
            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            const endpoint = editingId
                ? `/CRM/TerritoryManagement/Models/${encodeURIComponent(modelId)}/Nodes/${encodeURIComponent(editingId)}/EditJson`
                : `/CRM/TerritoryManagement/Models/${encodeURIComponent(modelId)}/Nodes/CreateJson`;
            const response = await fetch(endpoint, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token },
                body: new FormData(form)
            });
            const result = await response.json();
            if (result.success) {
                createNodeOffcanvas?.hide();
                window.location.reload();
                return;
            }
            alert.innerHTML = (result.errors || []).map((error) => `<div>${escapeHtml(error)}</div>`).join('');
            alert.classList.remove('d-none');
        } catch (error) {
            console.error('[TerritoryHierarchy] Node create failed.', error);
            alert.textContent = String(error?.message || error);
            alert.classList.remove('d-none');
        } finally {
            saveButton.disabled = false;
        }
    };

    const initialize = async () => {
        await loadSavedView();
        const savedFilters = savedViewState?.filters || {};
        appliedFilters = {
            level: normalizeArray(savedFilters.level),
            status: normalizeArray(savedFilters.status)
        };

    const table = new DataTable(
        tableEl,
        window.DtDefaults.create({
            data: nodes,
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            ordering: false,
            columns: [
                { data: 'id', name: 'control' },
                { data: 'territoryCode', name: 'territoryCode' },
                { data: 'name', name: 'name' },
                { data: 'territoryLevel', name: 'territoryLevel' },
                { data: 'status', name: 'status' },
                { data: 'effectiveFrom', name: 'effectiveFrom' },
                { data: 'effectiveTo', name: 'effectiveTo' },
                { data: 'sortOrder', name: 'sortOrder' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                {
                    targets: 0,
                    className: 'control',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 2,
                    render: () => ''
                },
                {
                    targets: 1,
                    render: (data, type, row) => {
                        if (type !== 'display') return data ?? '';
                        const depth = Math.max(0, Number(row.depth) || 0);
                        const branch = depth > 0 ? '<i class="bx bx-subdirectory-right text-muted me-1"></i>' : '';
                        return `<span class="d-inline-block" style="padding-left:${depth * 24}px">${branch}<span class="fw-medium text-heading">${escapeHtml(data)}</span></span>`;
                    }
                },
                {
                    targets: 3,
                    render: (data, type) => type === 'display'
                        ? `<span class="badge bg-label-info">${escapeHtml(data || '—')}</span>`
                        : (data ?? '')
                },
                {
                    targets: 4,
                    render: (data, type) => type === 'display'
                        ? `<span class="badge ${statusBadgeClass(data)}">${escapeHtml(data || '—')}</span>`
                        : (data ?? '')
                },
                { targets: 5, render: (data, type) => type === 'display' ? formatDate(data) : (data ?? '') },
                { targets: 6, render: (data, type) => type === 'display' ? formatDate(data) : (data ?? '') },
                {
                    targets: -1,
                    title: labels.actions || '',
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end',
                    render: (id, _type, row) => canManageNode
                        ? window.DitenDataTable.renderActions([{
                            key: 'edit',
                            className: 'js-edit-node',
                            icon: 'bx bx-edit',
                            text: labels.edit || '',
                            attrs: { 'data-id': id, 'data-json': JSON.stringify(row) }
                        }, ...(String(row.storedStatus || row.status).toLowerCase() === 'draft' ? [{
                            key: 'deleteDraftNode',
                            className: 'js-delete-draft-node',
                            icon: 'bx bx-trash',
                            text: labels.deleteDraftNode || '',
                            attrs: { 'data-id': id }
                        }] : [])])
                        : ''
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                canManageNode ? (labels.createNode || '') : null,
                {},
                {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                        attr: { title: labels.filter || '', 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
                        action: () => {}
                    },
                    saveFilterBtn: {
                        text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(labels.saveView || '')}</span>`,
                        className: 'btn btn-label-primary d-none dt-save-filter-btn',
                        attr: { title: labels.saveView || '' },
                        action: async (_event, api) => {
                            try {
                                await saveCurrentView(api || table);
                            } catch (error) {
                                if (!error?.authHandled) console.error('[TerritoryHierarchy SaveView] Save failed.', error);
                            }
                        }
                    }
                },
                { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
            ),
            language: payload.nodesUnavailable
                ? { emptyTable: labels.gatewayError || '' }
                : { emptyTable: labels.noNodes || '' },
            initComplete: function () {
                const api = this.api();
                const host = document.getElementById('inlineFilterHost');
                const filterButton = document.querySelector('.dt-filter-btn');
                const toolbar = filterButton?.closest('.dt-layout-row')
                    || filterButton?.closest('.row')
                    || filterButton?.closest('.dt-layout-end')?.parentElement;
                if (host && toolbar) {
                    toolbar.insertAdjacentElement('afterend', host);
                    host.classList.remove('px-6');
                    host.classList.add('px-3');
                }
                bindInlineFilterToggle();
                const distinct = (property) => Array.from(new Set(nodes.map((node) => node[property]).filter(Boolean))).sort();
                populateSelect('filterTerritoryLevel', distinct('territoryLevel').map((value) => ({ value, text: value })), false);
                populateSelect('filterStatus', distinct('status').map((value) => ({ value, text: value })), false);
                initFilterSelect2();
                $('#filterTerritoryLevel').val(appliedFilters.level).trigger('change');
                $('#filterStatus').val(appliedFilters.status).trigger('change');
                if (savedViewState?.search) api.search(savedViewState.search);
                Object.entries(savedViewState?.colVis || {}).forEach(([index, visible]) => api.column(Number(index)).visible(!!visible, false));
                if (Array.isArray(savedViewState?.columnOrder)) api.colReorder?.order?.(savedViewState.columnOrder, true);
                api.draw(false);
                if (canManageNode) {
                    document.querySelector('.add-new')?.addEventListener('click', (event) => {
                        event.preventDefault();
                        openCreateNode();
                    });
                }
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        })
    );

    table.on('column-visibility.dt column-reorder.dt columns-reordered.dt', function () {
        window.DtDefaults.updateVisualState(table, getAppliedFilterCount());
        document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
    });
    table.on('search.dt', function () {
        document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
    });

    $.fn.dataTable.ext.search.push((settings, _data, dataIndex, rowData) => {
        if (settings.nTable !== tableEl) return true;
        const row = rowData || table.row(dataIndex).data();
        return (!appliedFilters.level.length || appliedFilters.level.includes(row.territoryLevel))
            && (!appliedFilters.status.length || appliedFilters.status.includes(row.status));
    });

    document.getElementById('btnFilterApply')?.addEventListener('click', () => {
        appliedFilters = {
            level: normalizeArray($('#filterTerritoryLevel').val()),
            status: normalizeArray($('#filterStatus').val())
        };
        table.draw();
        document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
        bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse')).hide();
    });
    document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
        event.preventDefault();
        appliedFilters = { level: [], status: [] };
        $('#filterTerritoryLevel, #filterStatus').val(null).trigger('change');
        table.search('');
        table.draw();
        document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
    });

    document.addEventListener('click', (event) => {
        const lifecycle = event.target.closest('.js-model-lifecycle');
        if (lifecycle) {
            event.preventDefault();
            const action = lifecycle.dataset.action;
            const label = labels[action === 'delete-draft' ? 'deleteDraft' : action] || action;
            const lifecycleType = action === 'activate' ? 'success' : (action === 'delete-draft' ? 'danger' : 'warning');
            executeLifecycle(
                `/api/crm/territory-models/${encodeURIComponent(modelId)}/${action}`,
                label,
                payload.modelName,
                lifecycleType
            );
            return;
        }
        const deleteNode = event.target.closest('.js-delete-draft-node');
        if (deleteNode) {
            event.preventDefault();
            const node = nodes.find((item) => String(item.id) === String(deleteNode.dataset.id));
            executeLifecycle(
                `/api/crm/territory-models/${encodeURIComponent(modelId)}/nodes/${encodeURIComponent(deleteNode.dataset.id)}/delete-draft`,
                labels.deleteDraftNode || '',
                [node?.territoryCode, node?.name].filter(Boolean).join(' — ') || String(deleteNode.dataset.id),
                'danger'
            );
            return;
        }
        const trigger = event.target.closest('.js-edit-node');
        if (!trigger) return;
        event.preventDefault();
        let node = null;
        try { node = JSON.parse(trigger.getAttribute('data-json') || 'null'); } catch (_error) { }
        if (!node) node = nodes.find((item) => String(item.id) === String(trigger.dataset.id));
        openEditNode(node);
    });

    document.getElementById('tnTerritoryLevel')?.addEventListener('change', toggleMicrozoneSection);
    document.getElementById('btnSaveTerritoryNode')?.addEventListener('click', submitCreateNode);
    };

    initialize();
})();
