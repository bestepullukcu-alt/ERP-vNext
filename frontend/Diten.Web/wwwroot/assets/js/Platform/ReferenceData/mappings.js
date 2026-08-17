'use strict';

(function () {
    const root = document.getElementById('rd-mappings-page');
    if (!root) return;

    const setCode = root.dataset.setCode;
    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || { can: () => true, apply: (el, _cap, stateAllowed) => { if (el) el.disabled = stateAllowed === false; return stateAllowed !== false; }, guard: () => true };

    const body = document.getElementById('rd-mappings-body');
    const publishedBody = document.getElementById('rd-mappings-published-body');
    const statusEl = document.getElementById('rd-mappings-status');
    const emptyEl = document.getElementById('rd-mappings-empty');
    const errorEl = document.getElementById('rd-mappings-error');
    const cardEl = document.getElementById('rd-mappings-card');
    const workspaceEl = document.getElementById('rd-mappings-workspace');
    const getAddBtn = () => document.getElementById('rd-mappings-add');
    const refreshBtn = document.getElementById('rd-mappings-refresh');
    const publishedCardEl = document.getElementById('rd-mappings-published-card');

    let currentSet = null;
    let draftVersion = null;
    let mappings = [];
    let valueOptions = [];
    let draftDt = null;
    let publishedDt = null;

    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'ReferenceDataMappings_' + setCode };
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    let appliedFilters = { sourceValue: [], key: '', target: '' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';

    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalize(String(i))).filter(Boolean)));
        const s = normalize(v);
        return s ? [s] : [];
    };

    // Like normalizeArray but preserves the original casing of each value. The source
    // filter stores the actual <option> values (e.g. "USD") so they round-trip back into
    // select2 as selected; matching against rows is done case-insensitively at filter time.
    const dedupeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => String(i).trim()).filter(Boolean)));
        const s = String(v ?? '').trim();
        return s ? [s] : [];
    };

    const getAppliedFilterCount = () => {
        let count = 0;
        if (appliedFilters.sourceValue.length > 0) count++;
        if (appliedFilters.key) count++;
        if (appliedFilters.target) count++;
        return count;
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!btn || !collapseEl || btn.dataset.bound === '1') return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    const syncMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = $select.data('placeholder') || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((i) => String(i.text || '').trim()).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (tt('Reset', 'Reset')) + '" title="' + (tt('Reset', 'Reset')) + '">&times;</span>');
            $clearBtn.on('mousedown', (e) => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clearBtn);
        }
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterSourceValue').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
            $s.on('change.select2-summary', function () { syncMultiSelectSummary($s); });
            requestAnimationFrame(() => syncMultiSelectSummary($s));
        });
    };

    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || draftTableEl?.dataset.mappingsFilterBound === '1') return;
        draftTableEl.dataset.mappingsFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== draftTableEl) return true;
            const row = rowData || draftDt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;

            const matchesSource = !appliedFilters.sourceValue.length || appliedFilters.sourceValue.some((v) => normalize(v) === normalize(row.sourceValueCode));
            const matchesKey = !appliedFilters.key || normalize(row.mappingKey).includes(normalize(appliedFilters.key));
            const matchesTarget = !appliedFilters.target || normalize(row.targetCode).includes(normalize(appliedFilters.target)) || normalize(row.targetLabel).includes(normalize(appliedFilters.target));

            return matchesSource && matchesKey && matchesTarget;
        });
    };

    const saveDefaultView = async (normalizedView) => {
        if (!personalizationClient?.saveView) return null;
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: 'Default',
            isDefault: true,
            viewDefinition: normalizedView,
            visibility: 'private'
        };
        const existingId = defaultViewRecord?.id || defaultViewRecord?.Id || null;
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const savedRecord = savedResponse?.data || savedResponse?.Data || savedResponse;
        defaultViewRecord = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find((x) => x.isDefault === true || x.IsDefault === true) || items[0] || null) : null;
            if (defaultViewRecord) {
                const defRaw = defaultViewRecord.viewDefinition || defaultViewRecord.ViewDefinition;
                let def = {};
                if (typeof defRaw === 'string') {
                    try { def = JSON.parse(defRaw); } catch (e) {}
                } else if (defRaw && typeof defRaw === 'object') {
                    def = defRaw;
                }
                
                defaultViewState = {
                    filters: {
                        sourceValue: Array.isArray(def?.filters?.sourceValue) ? def.filters.sourceValue : [],
                        key: String(def?.filters?.key || '').trim(),
                        target: String(def?.filters?.target || '').trim()
                    },
                    search: String(def?.search || '').trim(),
                    order: Array.isArray(def?.order) ? def.order : [[1, 'asc']]
                };
                
                appliedFilters = Object.assign({}, defaultViewState.filters);
            }
        } catch (error) {
            console.error('[ReferenceDataMappings SaveView] Failed to load default view.', error);
        }
    };

    const getCurrentView = (api) => {
        const wrapper = api.table().container();
        const searchInput = wrapper?.querySelector('.dt-search input');
        const searchVal = searchInput ? searchInput.value.trim() : api.search();
        return {
            filters: Object.assign({}, appliedFilters),
            search: searchVal,
            order: api.order()
        };
    };

    const serializeView = (v) => JSON.stringify({
        filters: {
            sourceValue: Array.isArray(v?.filters?.sourceValue) ? v.filters.sourceValue.slice().sort() : [],
            key: String(v?.filters?.key || '').trim(),
            target: String(v?.filters?.target || '').trim()
        },
        search: String(v?.search || '').trim(),
        order: Array.isArray(v?.order) ? v.order : [[1, 'asc']]
    });

    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: { sourceValue: [], key: '', target: '' },
            search: '',
            order: [[1, 'asc']]
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const syncFilterControls = (values) => {
        const $sourceSelect = $('#filterSourceValue');
        if ($sourceSelect.length && window.jQuery && $.fn.select2) {
            $sourceSelect.val(dedupeArray(values.sourceValue)).trigger('change');
        }
        const keyInput = document.getElementById('filterKey');
        if (keyInput) keyInput.value = values.key || '';
        const targetInput = document.getElementById('filterTarget');
        if (targetInput) targetInput.value = values.target || '';
    };

    const syncSearchInput = (api, v) => {
        try {
            const el = api.table().container().querySelector('.dt-search input');
            if (el) el.value = v || '';
        } catch (_e) {}
    };

    const applySavedTableState = (api, view) => {
        if (!api || !view) return;
        appliedFilters = {
            sourceValue: Array.isArray(view.filters?.sourceValue) ? view.filters.sourceValue : [],
            key: String(view.filters?.key || '').trim(),
            target: String(view.filters?.target || '').trim()
        };
        syncFilterControls(appliedFilters);
        api.search(view.search || '');
        syncSearchInput(api, view.search || '');
        if (Array.isArray(view.order)) api.order(view.order);
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const show = (el, on) => el && el.classList.toggle('d-none', !on);
    const normalize = (value) => String(value || '').trim().toLowerCase();
    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const tt = (key, fallback) => {
        const value = (window.L10n || {})[key];
        return typeof value === 'string' && value.trim() ? value : fallback;
    };
    const noDraftReason = tt('NoDraftReason', 'An active draft version is required.');
    const retiredSetReason = permissions.retiredSetReason || 'This reference data set is retired. Changes are disabled.';
    const isRetiredSet = (setInfo) => (typeof permissions.isRetiredSet === 'function'
        ? permissions.isRetiredSet(setInfo)
        : normalize(setInfo?.status || setInfo?.Status) === 'retired');
    const applySetGate = (setInfo) => {
        const retired = isRetiredSet(setInfo);
        if (typeof permissions.setGlobalBlock === 'function') {
            permissions.setGlobalBlock(retired, retiredSetReason);
        }
        if (retired) {
            setStatus(retiredSetReason, 'info');
        }
        return retired;
    };

    const setDraftActions = (enabled, reason) => {
        permissions.apply(getAddBtn(), 'canUpdateVersion', enabled, reason || noDraftReason);
    };

    const renderEmptyState = ({ icon, title, description, actionsHtml }) => {
        show(workspaceEl, false);
        setDraftActions(false, noDraftReason);
        if (body) body.innerHTML = '';
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
        mappings = [];
        valueOptions = [];
        const workspaceHref = `/Platform/ReferenceData/Sets/${setId}`;
        const openWorkspaceBtn = `<a class="btn btn-primary" href="${workspaceHref}"><i class="bx bx-folder-open me-1"></i>${escapeHtml(tt('OpenSetWorkspace', 'Open Set Workspace'))}</a>`;

        if (isRetiredSet(currentSet)) {
            renderEmptyState({
                icon: 'bx-archive',
                title: escapeHtml(tt('NoDraftTitle', 'No editable draft version')),
                description: escapeHtml(tt('NoDraftRetired', 'This set is retired; new drafts cannot be created.')),
                actionsHtml: openWorkspaceBtn
            });
            return;
        }

        const hasPublished = !!(currentSet?.publishedVersionId || currentSet?.PublishedVersionId);
        const hint = hasPublished
            ? tt('NoDraftHintFromPublished', 'You can create a new draft from the published version.')
            : tt('NoDraftHintNoVersions', 'This set has no versions yet. Create the first draft.');
        const description = `${escapeHtml(tt('NoDraftMappingsDescription', 'Mappings can only be edited on a draft version.'))}<br><span class="fw-medium text-heading">${escapeHtml(setCode)}</span> &mdash; ${escapeHtml(hint)}`;

        if (!hasPublished) {
            show(publishedCardEl, false);
        }

        renderEmptyState({
            icon: 'bx-git-branch',
            title: escapeHtml(tt('NoDraftTitle', 'No editable draft version')),
            description,
            actionsHtml: openWorkspaceBtn
        });
    };

    const setStatus = (message, level) => {
        if (!statusEl) return;
        if (!message) {
            statusEl.className = 'alert alert-info d-none mb-3';
            statusEl.textContent = '';
            return;
        }

        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'info';
        statusEl.className = `alert alert-${css} mb-3`;
        statusEl.textContent = message;
    };

    const resolveSet = async () => {
        const data = await api.getSets(`?search=${encodeURIComponent(setCode)}&status=&scope_type=&page=1&page_size=100&sort=-createdAt`);
        const items = data?.items || data?.Items || [];
        const candidate = items.find((x) => normalize(x.setCode || x.SetCode) === normalize(setCode)) || null;
        if (!candidate) return null;
        return api.getSet(candidate.setId || candidate.SetId);
    };

    const sourceOptionsHtml = (selected) => {
        const options = [`<option value="">${escapeHtml(tt('SelectSourceValue', 'Select source value'))}</option>`];
        valueOptions.forEach((valueCode) => {
            const isSelected = normalize(valueCode) === normalize(selected) ? 'selected' : '';
            options.push(`<option value="${valueCode}" ${isSelected}>${valueCode}</option>`);
        });
        return options.join('');
    };
    // ─── Offcanvas Create/Edit mapping helpers ────────────────────────────────
    const getOcMappingInstance = () => {
        const el = document.getElementById('offcanvasMappingCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const resetMappingForm = () => {
        const form = document.getElementById('formMapping');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        form.querySelectorAll('.invalid-feedback').forEach((el) => { el.textContent = ''; });
        
        document.getElementById('mappingIndex').value = '';
        document.getElementById('mappingKeyInput').value = '';
        document.getElementById('mappingTargetInput').value = '';
        document.getElementById('mappingLabelInput').value = '';
        
        const sourceSelect = document.getElementById('mappingSourceSelect');
        if (sourceSelect) {
            sourceSelect.innerHTML = `<option value="">${escapeHtml(tt('SelectSourceValue', 'Select source value'))}</option>`;
            valueOptions.forEach((code) => {
                sourceSelect.insertAdjacentHTML('beforeend', `<option value="${escapeHtml(code)}">${escapeHtml(code)}</option>`);
            });
            sourceSelect.value = '';
        }
        document.getElementById('formMappingAlert').classList.add('d-none');
    };

    const openCreateMapping = () => {
        resetMappingForm();
        document.getElementById('offcanvasMappingCreateEditLabel').textContent = tt('AddMapping', 'Add Mapping');
        getOcMappingInstance()?.show();
    };

    const openEditMapping = (index) => {
        resetMappingForm();
        document.getElementById('offcanvasMappingCreateEditLabel').textContent = tt('Edit', 'Edit');
        
        const data = mappings[index];
        if (!data) return;

        document.getElementById('mappingIndex').value = String(index);
        document.getElementById('mappingKeyInput').value = data.mappingKey || '';
        document.getElementById('mappingSourceSelect').value = data.sourceValueCode || '';
        document.getElementById('mappingTargetInput').value = data.targetCode || '';
        document.getElementById('mappingLabelInput').value = data.targetLabel || '';

        getOcMappingInstance()?.show();
    };

    const submitMappingForm = () => {
        const form = document.getElementById('formMapping');
        if (!form) return;

        form.classList.add('was-validated');
        const keyInput = document.getElementById('mappingKeyInput');
        const sourceSelect = document.getElementById('mappingSourceSelect');
        const targetInput = document.getElementById('mappingTargetInput');
        const labelInput = document.getElementById('mappingLabelInput');

        const keyVal = keyInput.value.trim();
        const sourceVal = sourceSelect.value.trim();
        const targetVal = targetInput.value.trim();
        const labelVal = labelInput.value.trim() || null;

        if (!keyVal || !sourceVal || !targetVal) {
            return;
        }

        const indexVal = document.getElementById('mappingIndex').value;
        const isEdit = indexVal !== '';
        const editIndex = isEdit ? Number(indexVal) : -1;

        // Duplicate check
        const isDuplicate = mappings.some((row, idx) => {
            if (isEdit && idx === editIndex) return false;
            return normalize(row.mappingKey) === normalize(keyVal) && normalize(row.sourceValueCode) === normalize(sourceVal);
        });

        if (isDuplicate) {
            const alertEl = document.getElementById('formMappingAlert');
            alertEl.textContent = tt('DuplicateMappingKeySource', 'Duplicate mapping for key/source: {0} / {1}')
                .replace('{0}', keyVal)
                .replace('{1}', sourceVal);
            alertEl.classList.remove('d-none');
            return;
        }

        const newRow = {
            mappingKey: keyVal,
            sourceValueCode: sourceVal,
            targetCode: targetVal,
            targetLabel: labelVal
        };

        if (isEdit) {
            mappings[editIndex] = newRow;
        } else {
            mappings.push(newRow);
        }

        getOcMappingInstance()?.hide();
        renderDraftRows();
        save().catch((error) => {
            if (error?.isHandled) return;
            setStatus(error?.message || tt('SaveMappingsFailed', 'Failed to save mappings.'), 'error');
        });
    };
    const ensureDraftDataTable = () => {
        const draftTableEl = document.getElementById('dt-mappings-draft');
        if (!draftTableEl || draftDt) return draftDt;

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: tt('Filter', 'Filter'), 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (tt('SaveView', 'Save View')) + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: tt('SaveView', 'Save View'), 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || draftDt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(tt('RecordSaved', 'Saved successfully.'), 'success');
                    } catch (error) {
                        console.error('[ReferenceDataMappings SaveView] Failed to save default view.', error);
                        window.showToast?.(tt('ErrorOccurred', 'An error occurred.'), 'error');
                    }
                }
            }
        };

        draftDt = new DataTable(draftTableEl, window.DtDefaults.create({
            stateSave: false,
            data: mappings,
            order: [[1, 'asc']],
            colReorder: { columns: ':gt(0):not(:last-child)' },
            buttons: window.DtDefaults.exportButtons(
                tt('AddMapping', 'Add Mapping'),
                {
                    href: '#',
                    id: 'rd-mappings-add',
                    title: tt('AddMapping', 'Add Mapping'),
                    'aria-label': tt('AddMapping', 'Add Mapping')
                },
                extraButtons,
                { exportColumns: [1, 2, 3, 4], colvisColumns: [1, 2, 3, 4] }
            ),
            columns: [
                { data: null, name: 'control', title: '' },
                { data: 'mappingKey', name: 'mappingKey', title: tt('MappingKeyColumn', 'Key') },
                { data: 'sourceValueCode', name: 'sourceValueCode', title: tt('MappingSourceColumn', 'Source') },
                { data: 'targetCode', name: 'targetCode', title: tt('MappingTargetColumn', 'Target') },
                { data: 'targetLabel', name: 'targetLabel', title: tt('MappingLabelColumn', 'Label') },
                { data: null, name: 'action', title: tt('ActionColumn', 'Action') }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1, render: (data) => escapeHtml(data || '-') },
                { targets: 2, render: (data) => escapeHtml(data || '-') },
                { targets: 3, render: (data) => escapeHtml(data || '-') },
                { targets: 4, render: (data) => escapeHtml(data || '-') },
                {
                    targets: -1,
                    title: tt('ActionColumn', 'Action'),
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row, meta) => {
                        const readOnly = typeof permissions.isBlocked === 'function' && permissions.isBlocked();
                        if (readOnly) return '';

                        return window.DitenDataTable?.renderActions?.([
                            {
                                className: 'rd-map-remove text-danger me-1',
                                icon: 'bx bx-trash',
                                attrs: { 'data-index': meta.row }
                            },
                            {
                                className: 'rd-map-edit',
                                text: tt('Edit', 'Edit'),
                                icon: 'bx bx-edit',
                                attrs: { 'data-index': meta.row }
                            }
                        ]) || '';
                    }
                }
            ],
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            },
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterA11y();
                initSelect2Filters();
                registerTableFilters();

                // Apply saved view state (already loaded before table creation)
                applySavedTableState(this.api(), defaultViewState || {
                    filters: { sourceValue: [], key: '', target: '' },
                    search: '',
                    order: [[1, 'asc']]
                });

                setTimeout(() => { saveFilterArmed = true; }, 0);

                getAddBtn()?.addEventListener('click', (event) => {
                    event.preventDefault();
                    if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
                    if (!draftVersion) {
                        setStatus(noDraftReason, 'error');
                        return;
                    }
                    openCreateMapping();
                });
                setDraftActions(!!draftVersion, noDraftReason);
            }
        }));

        draftDt.on('search.dt order.dt draw.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(draftDt));
        });

        return draftDt;
    };

    const ensurePublishedDataTable = () => {
        const pubTableEl = document.getElementById('dt-mappings-published');
        if (!pubTableEl || publishedDt) return publishedDt;

        publishedDt = new DataTable(pubTableEl, window.DtDefaults.create({
            stateSave: false,
            data: [],
            order: [[1, 'asc']],
            colReorder: { columns: ':gt(0)' },
            columns: [
                { data: null, name: 'control', title: '' },
                { data: 'mappingKey', name: 'mappingKey', title: tt('MappingKeyColumn', 'Key') },
                { data: 'sourceValueCode', name: 'sourceValueCode', title: tt('MappingSourceColumn', 'Source') },
                { data: 'targetCode', name: 'targetCode', title: tt('MappingTargetColumn', 'Target') },
                { data: 'targetLabel', name: 'targetLabel', title: tt('MappingLabelColumn', 'Label') }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1, render: (data) => escapeHtml(data || '-') },
                { targets: 2, render: (data) => escapeHtml(data || '-') },
                { targets: 3, render: (data) => escapeHtml(data || '-') },
                { targets: 4, render: (data) => escapeHtml(data || '-') }
            ]
            // No drawCallback: the published snapshot has no filter/colvis buttons, and
            // DtDefaults.updateVisualState targets the GLOBAL `.dt-filter-btn` / `.dt-colvis-btn`,
            // so calling it here would wipe the draft table's restored filter badge/state.
        }));

        return publishedDt;
    };



    const renderDraftRows = () => {
        if (!window.DataTable || !window.DtDefaults) return;
        const readOnly = typeof permissions.isBlocked === 'function' && permissions.isBlocked();

        const api = ensureDraftDataTable();
        api?.clear();
        api?.rows.add(mappings);
        api?.draw();
    };

    const renderPublishedSnapshot = async () => {
        if (!window.DataTable || !window.DtDefaults) return;
        try {
            const published = await api.getValues(setCode, '?include_deprecated=true&include_attributes=false&include_mappings=true');
            const rows = (published?.mappings || published?.Mappings || []).map((x) => ({
                mappingKey: x.mappingKey || x.MappingKey || '',
                sourceValueCode: x.sourceValueCode || x.SourceValueCode || '',
                targetCode: x.targetCode || x.TargetCode || '',
                targetLabel: x.targetLabel || x.TargetLabel || ''
            }));

            const api = ensurePublishedDataTable();
            api?.clear();
            api?.rows.add(rows);
            api?.draw();
        } catch (_error) {
            const api = ensurePublishedDataTable();
            api?.clear();
            api?.draw();
        }
    };

    const validateRows = () => {
        const dedup = new Set();
        for (const row of mappings) {
            if (!row.mappingKey) return tt('MappingKeyRequired', 'Mapping key is required.');
            if (!row.sourceValueCode) return tt('MappingSourceRequired', 'Source value is required.');
            if (!row.targetCode) return tt('MappingTargetRequired', 'Target code is required.');
            if (!valueOptions.some((x) => normalize(x) === normalize(row.sourceValueCode))) {
                return tt('MappingSourceNotPresent', 'Source value {0} is not present in draft values.').replace('{0}', row.sourceValueCode);
            }

            const dedupKey = `${normalize(row.mappingKey)}|${normalize(row.sourceValueCode)}`;
            if (dedup.has(dedupKey)) {
                return tt('DuplicateMappingKeySource', 'Duplicate mapping for key/source: {0} / {1}')
                    .replace('{0}', row.mappingKey)
                    .replace('{1}', row.sourceValueCode);
            }
            dedup.add(dedupKey);
        }

        return null;
    };

    const save = async () => {
        if (!draftVersion) {
            setStatus(noDraftReason, 'error');
            return;
        }

        const validationError = validateRows();
        if (validationError) {
            setStatus(validationError, 'error');
            return;
        }

        const draftVersionId = draftVersion?.versionId || draftVersion?.VersionId;
        const token = draftVersion?.concurrencyToken || draftVersion?.ConcurrencyToken;
        const payload = {
            expected_concurrency_token: token,
            mappings: mappings.map((row) => ({
                mapping_key: row.mappingKey,
                source_value_code: row.sourceValueCode,
                target_code: row.targetCode,
                target_label: row.targetLabel
            }))
        };

        await api.replaceVersionMappings(draftVersionId, payload);
        await load();
        setStatus(tt('MappingsSaved', 'Draft mappings saved successfully.'), 'success');
    };

    const load = async () => {
        setStatus(null);
        show(errorEl, false);
        show(emptyEl, false);
        show(workspaceEl, true);
        show(publishedCardEl, true);
        setDraftActions(false, noDraftReason);

        currentSet = await resolveSet();
        if (typeof permissions.clearGlobalBlock === 'function') {
            permissions.clearGlobalBlock();
        }
        if (!currentSet) {
            show(workspaceEl, false);
            emptyEl.textContent = tt('SetNotFoundDescription', 'The requested reference data set was not found:') + ' ' + setCode;
            show(emptyEl, true);
            setDraftActions(false, 'Set must be loaded before editing mappings.');
            return;
        }
        const retired = applySetGate(currentSet);

        const draftVersionId = currentSet.activeDraftVersionId || currentSet.ActiveDraftVersionId;
        const setId = currentSet.setId || currentSet.SetId;
        if (!draftVersionId) {
            renderNoDraftState(setId);
            await renderPublishedSnapshot();
            return;
        }

        const [version, valuePayload, mappingsPayload] = await Promise.all([
            api.getVersion(draftVersionId),
            api.getVersionValues(draftVersionId),
            api.getVersionMappings(draftVersionId)
        ]);

        draftVersion = version;
        valueOptions = (valuePayload?.items || valuePayload?.Items || []).map((x) => x.code || x.Code).filter(Boolean);

        const filterSourceSelect = document.getElementById('filterSourceValue');
        if (filterSourceSelect) {
            const currentSelected = $(filterSourceSelect).val() || [];
            filterSourceSelect.innerHTML = '';
            valueOptions.forEach((code) => {
                const isSelected = currentSelected.includes(code) ? 'selected' : '';
                filterSourceSelect.insertAdjacentHTML('beforeend', `<option value="${escapeHtml(code)}" ${isSelected}>${escapeHtml(code)}</option>`);
            });
            if (window.jQuery && $.fn.select2) {
                $(filterSourceSelect).trigger('change');
            }
        }

        mappings = (mappingsPayload?.items || mappingsPayload?.Items || []).map((x) => ({
            mappingKey: x.mappingKey || x.MappingKey || '',
            sourceValueCode: x.sourceValueCode || x.SourceValueCode || '',
            targetCode: x.targetCode || x.TargetCode || '',
            targetLabel: x.targetLabel || x.TargetLabel || null
        }));

        // Load saved view state BEFORE creating/drawing the DataTable
        // (Golden Compact pattern: load view → create table → apply in initComplete)
        await loadDefaultView();

        renderDraftRows();
        permissions.apply(getAddBtn(), 'canUpdateVersion', true);
        await renderPublishedSnapshot();
        setStatus(retired ? retiredSetReason : null);
    };



    const draftTableEl = document.getElementById('dt-mappings-draft');
    draftTableEl?.addEventListener('click', (event) => {
        const removeBtn = event.target.closest('.rd-map-remove');
        const editBtn = event.target.closest('.rd-map-edit');
        if (!removeBtn && !editBtn) return;
        if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
        
        if (removeBtn) {
            const index = Number(removeBtn.getAttribute('data-index'));
            if (!Number.isFinite(index)) return;

            const mapping = mappings[index];
            const entityName = `${mapping?.mappingKey || ''} (${mapping?.sourceValueCode || ''} → ${mapping?.targetCode || ''})`;

            if (typeof window.showConfirm === 'function') {
                window.showConfirm(
                    tt('AreYouSure', 'Are you sure?'),
                    async () => {
                        mappings.splice(index, 1);
                        renderDraftRows();
                        try {
                            await save();
                        } catch (error) {
                            if (error?.isHandled) return;
                            setStatus(error?.message || tt('SaveMappingsFailed', 'Failed to save mappings.'), 'error');
                        }
                    },
                    {
                        entityName: entityName,
                        type: 'danger',
                        confirmButtonText: tt('Delete', 'Delete')
                    }
                );
            } else {
                mappings.splice(index, 1);
                renderDraftRows();
                save().catch((error) => {
                    if (error?.isHandled) return;
                    setStatus(error?.message || tt('SaveMappingsFailed', 'Failed to save mappings.'), 'error');
                });
            }
        } else if (editBtn) {
            const index = Number(editBtn.getAttribute('data-index'));
            if (!Number.isFinite(index)) return;
            openEditMapping(index);
        }
    });

    document.getElementById('btnSaveMapping')?.addEventListener('click', submitMappingForm);

    document.getElementById('btnFilterApply')?.addEventListener('click', () => {
        const sourceSelect = document.getElementById('filterSourceValue');
        const keyInput = document.getElementById('filterKey');
        const targetInput = document.getElementById('filterTarget');

        appliedFilters.sourceValue = dedupeArray($(sourceSelect).val());
        appliedFilters.key = keyInput ? keyInput.value.trim() : '';
        appliedFilters.target = targetInput ? targetInput.value.trim() : '';

        draftDt?.draw();
        window.DtDefaults.updateVisualState(draftDt, getAppliedFilterCount());
    });

    document.getElementById('btnFilterReset')?.addEventListener('click', () => {
        const sourceSelect = document.getElementById('filterSourceValue');
        if (sourceSelect && window.jQuery && $.fn.select2) {
            $(sourceSelect).val(null).trigger('change');
        }

        const keyInput = document.getElementById('filterKey');
        if (keyInput) keyInput.value = '';

        const targetInput = document.getElementById('filterTarget');
        if (targetInput) targetInput.value = '';

        appliedFilters = { sourceValue: [], key: '', target: '' };
        draftDt?.draw();
        window.DtDefaults.updateVisualState(draftDt, 0);
    });

    // Handle column adjustment on tab show
    const tabEl = document.querySelectorAll('button[data-bs-toggle="tab"]');
    tabEl.forEach((el) => {
        el.addEventListener('shown.bs.tab', () => {
            try { draftDt?.columns.adjust(); } catch (_error) {}
            try { publishedDt?.columns.adjust(); } catch (_error) {}
        });
    });



    const handleLoadError = (error) => {
        if (error?.isHandled) return;
        show(workspaceEl, false);
        const prefix = tt('LoadFailed', 'Could not load the workspace');
        errorEl.textContent = `${prefix}: ${error?.message || 'request_failed'}`;
        show(errorEl, true);
    };

    refreshBtn?.addEventListener('click', () => {
        load().catch(handleLoadError);
    });

    load().catch(handleLoadError);
})();
