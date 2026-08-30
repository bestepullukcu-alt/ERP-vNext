'use strict';

window.PpmCrud = (function () {
    const html = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    })[character]);

    const lowerFirstKeys = (value) => {
        if (Array.isArray(value)) return value.map(lowerFirstKeys);
        if (!value || typeof value !== 'object') return value;
        return Object.keys(value).reduce((result, key) => {
            result[key.charAt(0).toLowerCase() + key.slice(1)] = lowerFirstKeys(value[key]);
            return result;
        }, {});
    };

    const normalizeL10nKeys = (source) => {
        if (Array.isArray(source)) return source.map(normalizeL10nKeys);
        if (!source || typeof source !== 'object') return source;
        return Object.keys(source).reduce((result, key) => {
            const value = normalizeL10nKeys(source[key]);
            const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
            result[key] = value;
            result[pascalKey] = value;
            return result;
        }, {});
    };

    const parseL10n = () => {
        const node = document.getElementById('ppm-l10n');
        if (!node) return {};
        try {
            return normalizeL10nKeys(JSON.parse(node.textContent || '{}'));
        } catch (_) {
            return {};
        }
    };

    const request = async (url, options = {}) => {
        let response;
        try {
            response = await fetch(url, {
            credentials: 'same-origin',
            ...options,
            headers: {
                Accept: 'application/json',
                ...(options.body ? { 'Content-Type': 'application/json' } : {}),
                ...(options.headers || {})
            }
            });
        } catch (_) {
            const error = new Error();
            error.status = 0;
            error.isOffline = navigator.onLine === false;
            throw error;
        }
        if (response.status === 401) {
            window.DtDefaults?.handleUnauthorized?.();
            const error = new Error();
            error.status = 401;
            error.authHandled = true;
            throw error;
        }
        const raw = await response.text();
        let payload = {};
        if (raw) {
            try { payload = lowerFirstKeys(JSON.parse(raw)); } catch (_) {
                const error = new Error();
                error.status = 502;
                throw error;
            }
        }
        if (!response.ok) {
            const error = new Error();
            error.status = response.status;
            error.payload = payload;
            throw error;
        }
        return payload;
    };

    const lifecycleClass = (state) => ({
        Draft: 'bg-label-secondary', Proposed: 'bg-label-info', Planned: 'bg-label-info',
        Active: 'bg-label-success', OnHold: 'bg-label-warning',
        UnderAnalysis: 'bg-label-warning', Completed: 'bg-label-primary',
        Closed: 'bg-label-dark', Withdrawn: 'bg-label-danger',
        Archived: 'bg-label-dark', Cancelled: 'bg-label-danger'
    })[state] || 'bg-label-secondary';

    const mount = async (config) => {
        const L = parseL10n();
        window.L10n = Object.assign(window.L10n || {}, L);
        const table = document.querySelector('.datatables-ppm');
        if (!table || !window.DitenDataTable || !window.DtDefaults) return;

        const baseUrl = config.endpoint || `/ppm/${config.resource}/api`;
        const antiForgery = () => document.querySelector('#formPpm input[name="__RequestVerificationToken"]')?.value || '';
        const state = { dt: null, editId: null, rows: new Map(), lifecycle: [], referenceability: '', lookupBlocked: false };
        const form = document.getElementById('formPpm');
        const alert = document.getElementById('ppm-table-alert');
        const formAlert = document.getElementById('ppm-form-alert');
        const createCanvas = bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit'));
        const detailsCanvas = bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview'));
        const personalization = window.personalizationClient;
        const personalizationContext = { moduleKey: 'PPM', pageKey: config.resource };
        let defaultViewRecord = null;
        let defaultViewState = null;
        const stateLabel = (value) => L.States?.[value] || value;
        const titleOf = (row) => row?.[config.titleProperty || 'name'] || row?.name || row?.title || '';

        const setTableError = (message) => {
            alert.textContent = message || L.Error || L.ErrorOccurred || L.NotAvailable || '';
            alert.classList.remove('d-none');
        };
        const clearTableError = () => alert.classList.add('d-none');
        const settleTableLoading = (failed = false) => {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
            table.querySelectorAll('[data-ppm-loading-row]').forEach((row) => row.remove());
            Array.from(table.tBodies).forEach((body) => body.classList.toggle('d-none', failed));
            table.closest('.card')?.querySelectorAll('.dt-processing').forEach((element) => {
                element.classList.remove('show');
                element.setAttribute('style', 'display:none');
            });
            try { state.dt?.processing?.(false); } catch (_) { }
        };
        const showFormError = (error) => {
            const messages = error?.payload?.errors || [error?.message || L.ErrorOccurred || ''];
            formAlert.replaceChildren(...messages.map((message) => {
                const line = document.createElement('div');
                line.textContent = message;
                return line;
            }));
            formAlert.classList.remove('d-none');
        };
        const statusMessage = (error) => {
            if (error?.isOffline || error?.status === 0) return `${L.Error || L.ErrorOccurred || ''} (0)`;
            return ({
                401: L.Unauthorized,
                403: `${L.Error || L.ErrorOccurred || ''} (403)`,
                404: `${L.Error || L.ErrorOccurred || ''} (404)`,
                409: `${L.Error || L.ErrorOccurred || ''} (409)`,
                503: `${L.Error || L.ErrorOccurred || ''} (503)`
            })[error?.status] || L.ErrorOccurred || L.Error || L.NotAvailable || '';
        };
        const showFailure = (error) => {
            if (error?.authHandled) return;
            const message = statusMessage(error);
            window.showToast?.(message, 'error');
        };
        const setLookupBlocked = (error = null, showError = true) => {
            state.lookupBlocked = !!error;
            const selectors = config.hasPortfolio ? '#ppmPortfolioId' : config.hasInvestmentCaseParent ? '#ppmInvestmentCaseId' : '';
            if (selectors) $(selectors).val('').trigger('change').prop('disabled', state.lookupBlocked);
            const save = document.getElementById('btnSavePpm');
            if (save) save.disabled = state.lookupBlocked;
            if (!error) {
                formAlert.replaceChildren();
                formAlert.classList.add('d-none');
            } else if (showError) {
                if (!error.authHandled) showFormError({ message: statusMessage(error), status: error.status });
            }
        };

        const initializeSelect2 = () => {
            if (!window.jQuery || !$.fn.select2) return;
            const $lifecycle = $('#filterLifecycle');
            $lifecycle.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                closeOnSelect: false
            });
            const syncLifecycleSummary = () => {
                const $selection = $lifecycle.next('.select2-container').find('.select2-selection--multiple');
                const $rendered = $selection.find('.select2-selection__rendered');
                if (!$selection.length || !$rendered.length) return;
                let $summary = $selection.find('.dt-inline-filter-multi__summary');
                let $count = $selection.find('.dt-inline-filter-multi__count');
                if (!$summary.length) {
                    $summary = $('<span class="dt-inline-filter-multi__summary"></span>');
                    $selection.prepend($summary);
                }
                if (!$count.length) {
                    $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>');
                    $selection.append($count);
                }
                const count = ($lifecycle.val() || []).length;
                $summary.text($lifecycle.data('placeholder') || '');
                $rendered.attr('title', ($lifecycle.select2('data') || []).map((item) => item.text).join(', '));
                $count.toggleClass('d-none', count === 0).text(String(count));
                $selection.closest('.select2-container').toggleClass('dt-inline-filter-multi--has-value', count > 0);
            };
            $lifecycle.on('change.select2-summary', syncLifecycleSummary);
            requestAnimationFrame(syncLifecycleSummary);
            $('#filterReferenceability').select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                width: 'element',
                allowClear: true
            });
            $('#offcanvasCreateEdit .select2-offcanvas').each(function () {
                $(this).select2({ dropdownParent: $('#offcanvasCreateEdit'), width: '100%', allowClear: true });
            });
        };

        const unwrap = (payload) => lowerFirstKeys(payload?.data ?? payload?.Data ?? payload ?? []);

        if (config.hasInvestmentCaseParent) {
            try {
                const rows = unwrap(await request('/ppm/investment-cases/api'));
                config.investmentCases = new Map(rows.map((row) => [row.id, row]));
            } catch (error) {
                config.investmentCases = new Map();
                setLookupBlocked(error);
            }
        }

        const parentText = (row) => {
            if (config.hasPortfolio) {
                const parent = config.portfolios?.get(row.portfolioId);
                return parent ? `${parent.code} — ${titleOf(parent)}` : (config.hideRawParentId ? (L.NotAvailable || '-') : (row.portfolioId || L.NotAvailable || '-'));
            }
            if (config.hasInvestmentCaseParent) {
                const parent = config.investmentCases?.get(row.investmentCaseId);
                return parent ? `${parent.code} — ${titleOf(parent)}` : (L.NotAvailable || '-');
            }
            if (config.hasProjectParent) {
                const source = row.parentType === 'Initiative' ? config.initiatives : config.programs;
                const parent = source?.get(row.parentId);
                return parent ? `${row.parentType}: ${parent.code} — ${parent.name}` : `${row.parentType || ''}: ${row.parentId || '-'}`;
            }
            return '';
        };

        const rowActions = (row) => {
            const dataJson = html(JSON.stringify(row));
            const actions = [
                {
                    className: 'js-quick-view me-1', icon: 'bx bx-show',
                    attrs: { 'data-json': dataJson, title: L.ViewDetails }
                },
                {
                    className: 'js-ppm-edit', icon: 'bx bx-edit', text: L.Edit,
                    attrs: { 'data-id': row.id }
                }
            ];
            if (config.workspaceUrl) {
                actions.splice(1, 0, {
                    className: 'js-open-workspace', icon: 'bx bx-folder-open', text: L.OpenWorkspace,
                    attrs: { 'data-id': row.id }
                });
            }
            (config.transitions[row.lifecycleState] || []).forEach((target) => actions.push({
                className: 'js-ppm-lifecycle',
                icon: target === 'Cancelled' || target === 'Archived' ? 'bx bx-x-circle' : 'bx bx-transfer',
                text: stateLabel(target),
                attrs: { 'data-id': row.id, 'data-target': target }
            }));
            actions.push({
                className: 'js-ppm-delete text-danger', icon: 'bx bx-trash', text: L.Delete,
                attrs: { 'data-id': row.id, 'data-version': row.version, 'data-name': html(titleOf(row)) }
            });
            return window.DitenDataTable.renderActions(actions);
        };

        const parentColumn = config.hasPortfolio || config.hasProjectParent || config.hasInvestmentCaseParent;
        const columns = [
            { data: 'id' }, { data: 'id' }, { data: 'code' }, { data: config.titleProperty || 'name' },
            ...(parentColumn ? [{ data: null }] : []),
            { data: 'lifecycleState' }, ...(config.showReferenceability === false ? [] : [{ data: 'isReferenceable' }]), { data: 'id' }
        ];
        const lifecycleIndex = parentColumn ? 5 : 4;
        const referenceabilityIndex = config.showReferenceability === false ? -1 : lifecycleIndex + 1;
        const actionIndex = lifecycleIndex + (config.showReferenceability === false ? 1 : 2);

        const factoryState = () => ({
            filters: { lifecycle: [], referenceability: '' }, search: '',
            colVis: columns.map(() => true), columnOrder: columns.map((_column, index) => index), order: [[2, 'asc']]
        });
        const captureView = () => ({
            filters: { lifecycle: [...state.lifecycle], referenceability: state.referenceability },
            search: state.dt.search(), colVis: state.dt.columns().visible().toArray(),
            columnOrder: state.dt.colReorder?.order?.() || columns.map((_column, index) => index), order: state.dt.order()
        });
        const applyView = (view) => {
            const next = view || factoryState();
            state.lifecycle = Array.isArray(next.filters?.lifecycle) ? next.filters.lifecycle : [];
            state.referenceability = next.filters?.referenceability || '';
            $('#filterLifecycle').val(state.lifecycle).trigger('change');
            $('#filterReferenceability').val(state.referenceability).trigger('change');
            state.dt.search(next.search || '');
            (next.colVis || []).forEach((visible, index) => state.dt.column(index).visible(visible, false));
            if (next.columnOrder?.length) state.dt.colReorder?.order?.(next.columnOrder, true);
            state.dt.order(next.order?.length ? next.order : [[2, 'asc']]).draw();
        };
        const setSaveVisible = (visible) => {
            document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
            window.DtDefaults?.refreshButtonGroupRadii?.();
        };
        if (personalization?.getViews) {
            try {
                const response = await personalization.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
                const views = Array.isArray(response) ? response : (response?.data || response?.Data || []);
                defaultViewRecord = views.find((view) => view.isDefault || view.IsDefault) || views[0] || null;
                defaultViewState = defaultViewRecord?.viewDefinition || defaultViewRecord?.ViewDefinition || null;
            } catch (error) {
                if (!error?.authHandled) showFailure(error);
            }
        }

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter },
                action: () => bootstrap.Collapse.getOrCreateInstance(
                    document.getElementById('inlineFilterCollapse'), { toggle: false }).toggle()
            },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${html(L.SaveView)}</span>`,
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView },
                action: async () => {
                    if (!personalization?.saveView) {
                        showFailure(new Error(L.ErrorOccurred));
                        return;
                    }
                    const definition = captureView();
                    const payload = {
                        moduleKey: personalizationContext.moduleKey, pageKey: personalizationContext.pageKey,
                        viewName: L.SaveView, viewDefinition: definition, isDefault: true, visibility: 'private'
                    };
                    const id = defaultViewRecord?.id || defaultViewRecord?.Id;
                    defaultViewRecord = id && personalization.updateView
                        ? await personalization.updateView(id, payload)
                        : await personalization.saveView(payload);
                    defaultViewState = definition;
                    setSaveVisible(false);
                    window.showToast?.(L.RecordSaved, 'success');
                }
            }
        };

        if (config.hasPortfolio) {
            try {
                const rows = unwrap(await request('/ppm/portfolios/api'));
                config.portfolios = new Map(rows.map((row) => [row.id, row]));
            } catch (error) {
                config.portfolios = new Map();
                setLookupBlocked(error);
            }
        }
        if (config.hasProjectParent) {
            try {
                const [initiativesPayload, programsPayload] = await Promise.all([
                    request('/ppm/initiatives/api'), request('/ppm/programs/api')
                ]);
                config.initiatives = new Map(unwrap(initiativesPayload).map((row) => [row.id, row]));
                config.programs = new Map(unwrap(programsPayload).map((row) => [row.id, row]));
            } catch (_) {
                config.initiatives = new Map();
                config.programs = new Map();
            }
        }

        state.dt = window.DitenDataTable.createCrudTable({
            tableEl: table,
            bulk: {
                bulkBarSelector: '#bulkActionBar', bulkCountSelector: '#bulkSelectedCount',
                checkboxSelector: '.dt-checkboxes', clearSelectionSelector: '#btnClearSelection',
                selectAllSelector: '.dt-checkboxes-select-all'
            },
            ajax: { url: baseUrl, type: 'GET', headers: config.headers || {}, dataSrc: (payload) => {
                const rows = unwrap(payload);
                const list = Array.isArray(rows) ? rows : [];
                state.rows = new Map(list.map((row) => [row.id, row]));
                clearTableError();
                settleTableLoading(false);
                return list;
            }, error: (xhr) => {
                settleTableLoading(true);
                if (xhr.status === 401) window.DtDefaults?.handleUnauthorized?.();
                else setTableError(statusMessage({ status: xhr.status || 0, isOffline: xhr.status === 0 }));
            } },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns,
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                    { targets: 1, className: 'dt-checkboxes-cell cell-fit', searchable: false, orderable: false,
                        render: (value) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${html(value)}">` },
                    { targets: 2, render: (value) => `<span class="fw-medium text-heading">${html(value)}</span>` },
                    ...(parentColumn ? [{ targets: 4, render: (_value, _type, row) => html(parentText(row)) }] : []),
                    { targets: lifecycleIndex, render: (value) => `<span class="badge ${lifecycleClass(value)}">${html(stateLabel(value))}</span>` },
                    ...(config.showReferenceability === false ? [] : [{ targets: referenceabilityIndex, render: (value) => `<span class="badge ${value ? 'bg-label-success' : 'bg-label-secondary'}">${html(value ? L.Referenceable : L.NotReferenceable)}</span>` }]),
                    { targets: actionIndex, className: 'cell-fit all', searchable: false, orderable: false,
                        render: (_value, _type, row) => rowActions(row) }
                ],
                buttons: window.DtDefaults.exportButtons(L.AddNew, {}, extraButtons, {
                    exportColumns: Array.from({ length: actionIndex - 2 }, (_, i) => i + 2),
                    colvisColumns: Array.from({ length: actionIndex - 2 }, (_, i) => i + 2)
                }),
                language: { emptyTable: L.Empty, zeroRecords: L.Empty },
                order: [[2, 'asc']]
            }
        });
        state.dt.on('xhr.dt', (_event, _settings, json, xhr) => {
            const failed = json == null || (xhr && xhr.status >= 400);
            settleTableLoading(failed);
            if (failed) {
                setTableError(`${L.Error || L.ErrorOccurred || L.NotAvailable || ''} (${xhr?.status || 503})`);
            }
        });
        state.dt.on('error.dt', (_event, _settings, _techNote, message) => {
            settleTableLoading(true);
            setTableError(L.Error || L.ErrorOccurred || L.NotAvailable || '');
        });

        const markDirty = () => setSaveVisible(true);
        state.dt.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', markDirty);

        $.fn.dataTable.ext.search.push((settings, rowData, dataIndex) => {
            if (settings.nTable !== table) return true;
            const row = state.dt.row(dataIndex).data();
            if (!row) return true;
            const lifecycleMatch = !state.lifecycle.length || state.lifecycle.includes(row.lifecycleState);
            const referenceMatch = config.showReferenceability === false || state.referenceability === '' || String(!!row.isReferenceable) === state.referenceability;
            return lifecycleMatch && referenceMatch;
        });

        initializeSelect2();
        applyView(defaultViewState);
        document.querySelectorAll('#filterLifecycle option, #ppmLifecycleState option').forEach((option) => {
            option.textContent = stateLabel(option.value);
        });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            state.lifecycle = $('#filterLifecycle').val() || [];
            state.referenceability = document.getElementById('filterReferenceability')?.value || '';
            state.dt.draw();
            markDirty();
            bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', async (event) => {
            event.preventDefault();
            const id = defaultViewRecord?.id || defaultViewRecord?.Id;
            try {
                if (id && personalization?.deleteView) {
                    await personalization.deleteView(id, personalizationContext.moduleKey, personalizationContext.pageKey);
                }
                defaultViewRecord = null;
                defaultViewState = null;
                applyView(factoryState());
                setSaveVisible(false);
            } catch (error) { showFailure(error); }
        });

        const resetForm = () => {
            form.reset();
            form.classList.remove('was-validated');
            formAlert.classList.add('d-none');
            state.editId = null;
            document.getElementById('ppmId').value = '';
            document.getElementById('ppmVersion').value = '1';
            $('#ppmLifecycleState').val(config.defaultLifecycle).trigger('change');
            $('#ppmPortfolioId').val('').trigger('change');
            $('#ppmParentType').val('').trigger('change');
            $('#ppmParentId').val('').trigger('change').prop('disabled', true);
            $('#ppmInvestmentCaseId').val('').trigger('change');
            $('#ppmPortfolioId, #ppmInvestmentCaseId').prop('disabled', state.lookupBlocked);
        };

        const populateSelect = (selector, rows, selected) => {
            const select = document.querySelector(selector);
            if (!select) return;
            const first = select.options[0]?.cloneNode(true);
            select.replaceChildren(...(first ? [first] : []));
            rows.forEach((row) => {
                const option = document.createElement('option');
                option.value = row.id;
                option.textContent = `${row.code} — ${titleOf(row)}`;
                option.selected = row.id === selected;
                select.appendChild(option);
            });
            $(select).trigger('change');
        };

        async function refreshLookups(current) {
            setLookupBlocked(new Error(L.Loading || L.NotAvailable || ''), false);
            if (config.hasPortfolio) {
                const rows = unwrap(await request('/ppm/portfolios/api'));
                config.portfolios = new Map(rows.map((row) => [row.id, row]));
                populateSelect('#ppmPortfolioId', rows.filter((row) => row.isReferenceable), current?.portfolioId);
            }
            if (config.hasProjectParent) {
                const type = current?.parentType || document.getElementById('ppmParentType')?.value;
                if (!type) return;
                const resource = type === 'Initiative' ? 'initiatives' : 'programs';
                const rows = unwrap(await request(`/ppm/${resource}/api`));
                config[type === 'Initiative' ? 'initiatives' : 'programs'] = new Map(rows.map((row) => [row.id, row]));
                populateSelect('#ppmParentId', rows.filter((row) => row.isReferenceable), current?.parentId);
                $('#ppmParentId').prop('disabled', false);
            }
            if (config.hasInvestmentCaseParent) {
                const rows = unwrap(await request('/ppm/investment-cases/api'));
                config.investmentCases = new Map(rows.map((row) => [row.id, row]));
                populateSelect('#ppmInvestmentCaseId', rows.filter((row) => row.isReferenceable !== false), current?.investmentCaseId);
            }
            setLookupBlocked(null);
        }

        async function openCreate() {
            resetForm();
            document.getElementById('offcanvasCreateEditLabel').textContent = L.AddNew;
            try { await refreshLookups(null); } catch (error) { showFormError(error); }
            createCanvas.show();
        }

        async function openEdit(id) {
            resetForm();
            try {
                const row = unwrap(await request(`${baseUrl}/${id}`));
                state.editId = id;
                document.getElementById('offcanvasCreateEditLabel').textContent = L.EditTitle;
                document.getElementById('ppmId').value = row.id;
                document.getElementById('ppmVersion').value = row.version;
                document.getElementById('ppmCode').value = row.code || '';
                document.getElementById('ppmName').value = titleOf(row);
                document.getElementById('ppmDescription').value = row.description || '';
                if (config.hasPlanningDates) {
                    document.getElementById('ppmPlannedStartDate').value = row.plannedStartDate || '';
                    document.getElementById('ppmPlannedEndDate').value = row.plannedEndDate || '';
                }
                if (config.hasBenefitTarget) {
                    document.getElementById('ppmTargetDescription').value = row.targetDescription || '';
                    document.getElementById('ppmTargetDate').value = row.targetDate || '';
                }
                $('#ppmLifecycleState').val(row.lifecycleState).trigger('change');
                $('#ppmParentType').val(row.parentType || '').trigger('change');
                await refreshLookups(row);
                if (config.immutableParent) {
                    $('#ppmPortfolioId, #ppmInvestmentCaseId').prop('disabled', true);
                }
                createCanvas.show();
            } catch (error) { showFailure(error); }
        }

        if (table.dataset.ppmAddNewBound !== 'true') {
            table.dataset.ppmAddNewBound = 'true';
            document.addEventListener('click', (event) => {
                const addButton = event.target.closest('.add-new');
                if (!addButton) return;

                const dataTableRoot = addButton.closest('.dt-container');
                if (!dataTableRoot || !dataTableRoot.contains(table)) return;

                event.preventDefault();
                event.stopPropagation();
                void openCreate();
            });
        }

        const formPayload = () => ({
            id: state.editId,
            code: document.getElementById('ppmCode').value.trim(),
            [config.titleProperty || 'name']: document.getElementById('ppmName').value.trim(),
            description: document.getElementById('ppmDescription').value.trim() || null,
            ...(config.hasPortfolio ? { portfolioId: document.getElementById('ppmPortfolioId').value || null } : {}),
            ...(config.hasProjectParent ? {
                parentType: document.getElementById('ppmParentType').value,
                parentId: document.getElementById('ppmParentId').value
            } : {}),
            ...(config.hasInvestmentCaseParent ? { investmentCaseId: document.getElementById('ppmInvestmentCaseId').value } : {}),
            ...(config.hasPlanningDates ? {
                plannedStartDate: document.getElementById('ppmPlannedStartDate').value || null,
                plannedEndDate: document.getElementById('ppmPlannedEndDate').value || null
            } : {}),
            ...(config.hasBenefitTarget ? {
                targetDescription: document.getElementById('ppmTargetDescription').value.trim(),
                targetDate: document.getElementById('ppmTargetDate').value || null
            } : {}),
            lifecycleState: document.getElementById('ppmLifecycleState').value,
            ...(config.showVisibilityPolicy === false ? {} : { visibilityPolicyKey: null }),
            expectedVersion: Number(document.getElementById('ppmVersion').value || 1)
        });

        document.getElementById('btnSavePpm')?.addEventListener('click', async () => {
            form.classList.add('was-validated');
            formAlert.classList.add('d-none');
            if (state.lookupBlocked || !form.checkValidity()) return;
            const button = document.getElementById('btnSavePpm');
            button.disabled = true;
            try {
                await request(state.editId ? `${baseUrl}/${state.editId}` : baseUrl, {
                    method: state.editId ? 'PUT' : 'POST',
                    headers: { RequestVerificationToken: antiForgery() },
                    body: JSON.stringify(formPayload())
                });
                createCanvas.hide();
                state.dt.ajax.reload(() => window.showToast?.(L.RecordSaved, 'success'), false);
            } catch (error) { showFormError(error); } finally { button.disabled = state.lookupBlocked; }
        });

        document.getElementById('ppmParentType')?.addEventListener('change', async () => {
            $('#ppmParentId').val('').trigger('change').prop('disabled', true);
            try { await refreshLookups(null); } catch (error) { showFormError(error); }
        });

        document.addEventListener('click', async (event) => {
            const view = event.target.closest('.js-quick-view');
            const workspace = event.target.closest('.js-open-workspace');
            const edit = event.target.closest('.js-ppm-edit');
            const transition = event.target.closest('.js-ppm-lifecycle');
            const remove = event.target.closest('.js-ppm-delete');
            if (!view && !workspace && !edit && !transition && !remove) return;
            event.preventDefault();
            if (workspace) {
                window.location.assign(config.workspaceUrl(workspace.dataset.id));
            } else if (view) {
                const row = JSON.parse(view.dataset.json || '{}');
                document.getElementById('oc-title').textContent = titleOf(row) || '-';
                document.getElementById('oc-subtitle').textContent = row.code || '-';
                document.getElementById('oc-code').textContent = row.code || '-';
                document.getElementById('oc-name').textContent = titleOf(row) || '-';
                document.getElementById('oc-description').textContent = row.description || '-';
                document.getElementById('oc-lifecycle').textContent = stateLabel(row.lifecycleState) || '-';
                document.getElementById('oc-lifecycle').className = `badge mt-1 ${lifecycleClass(row.lifecycleState)}`;
                const referenceability = document.getElementById('oc-referenceability');
                if (referenceability) referenceability.textContent = row.isReferenceable ? L.Referenceable : L.NotReferenceable;
                document.getElementById('oc-parent') && (document.getElementById('oc-parent').textContent = parentText(row));
                document.getElementById('oc-planned-start') && (document.getElementById('oc-planned-start').textContent = row.plannedStartDate || '-');
                document.getElementById('oc-planned-end') && (document.getElementById('oc-planned-end').textContent = row.plannedEndDate || '-');
                document.getElementById('oc-target-description') && (document.getElementById('oc-target-description').textContent = row.targetDescription || '-');
                document.getElementById('oc-target-date') && (document.getElementById('oc-target-date').textContent = row.targetDate || '-');
                document.getElementById('oc-btn-edit').dataset.id = row.id;
                const workspaceLink = document.getElementById('oc-open-workspace');
                if (workspaceLink && config.workspaceUrl) workspaceLink.href = config.workspaceUrl(row.id);
                detailsCanvas.show();
            } else if (edit) {
                await openEdit(edit.dataset.id);
            } else {
                const id = transition?.dataset.id || remove?.dataset.id;
                const target = transition?.dataset.target;
                const confirmation = target === 'Archived' ? L.ArchiveConfirm
                    : target === 'Completed' ? L.CompleteConfirm
                        : target === 'Closed' ? L.CloseConfirm
                            : target === 'Withdrawn' ? L.WithdrawConfirm
                                : target === 'Cancelled' ? L.CancelConfirm : L.AreYouSure;
                window.showConfirm?.(confirmation || L.AreYouSure, async () => {
                    try {
                        if (transition) {
                            const row = state.rows.get(id);
                            await request(`${baseUrl}/${id}/lifecycle`, {
                                method: 'POST',
                                headers: { RequestVerificationToken: antiForgery() },
                                body: JSON.stringify({ id, targetState: target, expectedVersion: row?.version })
                            });
                        } else {
                            await request(`${baseUrl}/${id}?expectedVersion=${remove.dataset.version}`, {
                                method: 'DELETE', headers: { RequestVerificationToken: antiForgery() }
                            });
                        }
                        state.dt.ajax.reload(() => window.showToast?.(transition ? L.RecordSaved : L.RecordDeleted, 'success'), false);
                    } catch (error) { showFailure(error); }
                }, {
                    entityName: remove?.dataset.name || stateLabel(target) || '',
                    confirmButtonText: target ? stateLabel(target) : L.Delete,
                    type: 'danger'
                });
            }
        });

        document.getElementById('oc-btn-edit')?.addEventListener('click', async (event) => {
            detailsCanvas.hide();
            await openEdit(event.currentTarget.dataset.id);
        });
    };

    return { mount };
})();
