'use strict';

// Positions (platform-admin, Slim pattern). Backend list endpoints return a plain array
// (Response<IReadOnlyList<T>>), so the DataTable is client-side (serverSide:false): a single ajax
// fetch loads the whole list, with paging/sort/search handled in the browser. Create/edit happen in an
// in-page offcanvas; archive + delete are row actions. Org-unit options come from the Platform org-units
// list (always reachable); Reports-To options come from the positions list itself. On edit, the manager
// chain is fetched and rendered read-only at the bottom of the offcanvas.
const PositionsList = (function () {
    let dt;
    let editingId = null;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-positions');
    const endpoint = '/Positions/api';
    const orgUnitsEndpoint = '/Positions/api/org-units';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { archived: '' };

    // Loaded reference data + id→label maps used by the table renderers and offcanvas selects.
    let positionsData = [];
    let orgUnitsData = [];
    const orgUnitMap = {};
    const positionMap = {};

    const loadL10n = () => {
        const node = document.getElementById('positions-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[Positions] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');

    const archivedBadge = (value) => value
        ? `<span class="badge bg-label-secondary">${escapeHtml(L.StatusArchived || 'Archived')}</span>`
        : `<span class="badge bg-label-success">${escapeHtml(L.StatusActive || 'Active')}</span>`;

    const orgUnitLabel = (unit) => {
        const id = unit.id || unit.Id;
        const name = unit.name || unit.Name || '';
        const code = unit.code || unit.Code || '';
        return { id, text: code ? `${code} — ${name}` : name };
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
    };

    const rowActionHandlers = {
        edit: ({ id, row }) => {
            const editId = id || row?.id || row?.Id;
            if (editId) openEditOffcanvas(String(editId));
        },
        archive: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = row?.name || row?.Name || row?.code || row?.Code || '';
            window.showConfirm?.(L.ArchiveConfirm || L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(rowId)}/archive`, { method: 'POST', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Archive failed.');
                    reloadWithSuccessToast('RecordArchived', entityName);
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }, { entityName, type: 'warning', confirmButtonText: L.Archive });
        },
        delete: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = row?.name || row?.Name || row?.code || row?.Code || '';
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(rowId)}`, { method: 'DELETE', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted', entityName);
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }, { entityName, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    const fetchPositions = () => fetch(`${endpoint}`, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList);

    // Org-units lookup feeds the required OrgUnit select. Platform org-units is always reachable, but a
    // failure resolves to [] so the table still renders.
    const fetchOrgUnits = () => fetch(orgUnitsEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList)
        .catch(() => []);

    const rebuildMaps = () => {
        Object.keys(orgUnitMap).forEach((k) => delete orgUnitMap[k]);
        Object.keys(positionMap).forEach((k) => delete positionMap[k]);
        orgUnitsData.forEach((u) => { const { id, text } = orgUnitLabel(u); if (id) orgUnitMap[id] = text; });
        positionsData.forEach((p) => { positionMap[p.id || p.Id] = p.name || p.Name || ''; });
    };

    const applyClientFilter = (rows) => {
        if (appliedFilters.archived === '') return rows;
        const wantArchived = appliedFilters.archived === 'true';
        return rows.filter((r) => Boolean(r.isArchived ?? r.IsArchived) === wantArchived);
    };

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[Positions] DataTable element or DtDefaults not found.');
            return;
        }

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                Promise.all([fetchPositions(), fetchOrgUnits()])
                    .then(([positions, orgUnits]) => {
                        positionsData = positions || [];
                        orgUnitsData = orgUnits || [];
                        rebuildMaps();
                        callback({ data: applyClientFilter(positionsData) });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'code', name: 'code', render: (value) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(value)}</span>` },
                { data: 'name', name: 'name', render: escapeHtml },
                {
                    data: 'organizationUnitId', name: 'orgUnit',
                    render: (value) => escapeHtml(orgUnitMap[value] || value || '-')
                },
                {
                    data: 'reportsToPositionId', name: 'reportsTo',
                    render: (value) => escapeHtml(value ? (positionMap[value] || value) : '-')
                },
                { data: 'isArchived', name: 'isArchived', render: (value) => archivedBadge(value) },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (value, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);
                        const isArchived = Boolean(row.isArchived ?? row.IsArchived);

                        const actions = [
                            { key: 'edit', icon: 'bx bx-edit', className: 'js-edit-item', text: L.Edit || '', attrs: { 'data-id': id, 'data-json': rowJson } }
                        ];
                        if (!isArchived) {
                            actions.push({ key: 'archive', icon: 'bx bx-archive-in', className: 'text-warning', text: L.Archive || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        }
                        actions.push({ key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete || '', attrs: { 'data-id': id, 'data-json': rowJson } });

                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNew || '', {}, { filterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                initSelect2Filters();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        });

        if (L.Showing) {
            dtConfig.language = dtConfig.language || {};
            dtConfig.language.info = `${L.Showing} _START_ - _END_ / _TOTAL_`;
        }

        dt = new DataTable(dtTableEl, dtConfig);

        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl: dtTableEl,
            dt,
            onRowAction: rowActionHandlers
        });
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: true,
                allowClear: true
            });
        });
    };

    const bindFilters = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { archived: document.getElementById('filterArchived')?.value || '' };
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            appliedFilters = { archived: '' };
            const element = document.getElementById('filterArchived');
            if (element) {
                element.value = '';
                if (window.jQuery?.fn?.select2) $(element).val('').trigger('change');
            }
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => value !== '').length;

    // ─── Create/Edit offcanvas (Slim pattern) ────────────────────────────────
    const getOcInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#formPosition input[name="__RequestVerificationToken"]')?.value || '';

    const normalizeCode = (value) => (value || '')
        .toUpperCase()
        .replace(/[^A-Z0-9]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-+|-+$/g, '')
        .slice(0, 80)
        .replace(/-+$/g, '');

    const updateCodePreview = () => {
        const input = document.getElementById('positionCode');
        const preview = document.getElementById('positionCodePreview');
        const span = preview?.querySelector('span');
        if (!input || !preview || !span) return;
        const normalized = normalizeCode(input.value);
        if (normalized && normalized !== input.value) {
            span.textContent = normalized;
            preview.classList.remove('d-none');
        } else {
            preview.classList.add('d-none');
        }
    };

    const populateOrgUnitSelect = (selectedId) => {
        const select = document.getElementById('positionOrgUnit');
        if (!select) return;
        select.innerHTML = `<option value="">${escapeHtml(L.SelectOrgUnit || '')}</option>`;
        orgUnitsData.forEach((u) => {
            const { id, text } = orgUnitLabel(u);
            if (!id) return;
            const option = document.createElement('option');
            option.value = id;
            option.textContent = text;
            if (selectedId && id === selectedId) option.selected = true;
            select.appendChild(option);
        });
    };

    const populateReportsToSelect = (selectedId, excludeId) => {
        const select = document.getElementById('positionReportsTo');
        if (!select) return;
        select.innerHTML = `<option value="">${escapeHtml(L.NoReportsTo || '')}</option>`;
        positionsData
            .filter((p) => (p.id || p.Id) !== excludeId)
            .forEach((p) => {
                const id = p.id || p.Id;
                const option = document.createElement('option');
                option.value = id;
                option.textContent = `${p.code || p.Code || ''} — ${p.name || p.Name || ''}`;
                if (selectedId && id === selectedId) option.selected = true;
                select.appendChild(option);
            });
    };

    // Render the manager chain (read-only) into the offcanvas. Hidden on create.
    const renderManagerChain = (chain) => {
        const section = document.getElementById('positionManagerChainSection');
        const host = document.getElementById('positionManagerChain');
        if (!section || !host) return;
        const nodes = (Array.isArray(chain) ? chain : [])
            .slice()
            .sort((a, b) => (a.depth ?? a.Depth ?? 0) - (b.depth ?? b.Depth ?? 0));
        if (!nodes.length) {
            host.innerHTML = `<span class="text-muted">-</span>`;
            section.classList.remove('d-none');
            return;
        }
        const items = nodes.map((n) => {
            const code = n.positionCode || n.PositionCode || '';
            const name = n.positionName || n.PositionName || '';
            return `<li>${escapeHtml(code ? `${code} — ${name}` : name)}</li>`;
        }).join('');
        host.innerHTML = `<ol class="mb-0 ps-3">${items}</ol>`;
        section.classList.remove('d-none');
    };

    const hideManagerChain = () => {
        const section = document.getElementById('positionManagerChainSection');
        const host = document.getElementById('positionManagerChain');
        if (host) host.innerHTML = '';
        section?.classList.add('d-none');
    };

    const readOrgUnitId = () => normalizeString(document.getElementById('positionOrgUnit')?.value);

    const clearFormErrors = () => {
        const alertEl = document.getElementById('formPositionAlert');
        if (alertEl) { alertEl.classList.add('d-none'); alertEl.innerHTML = ''; }
    };

    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formPositionAlert');
        if (!alertEl) return;
        const list = (Array.isArray(errors) ? errors : [errors]).filter(Boolean);
        alertEl.innerHTML = list.length
            ? list.map((message) => `<div>${escapeHtml(message)}</div>`).join('')
            : escapeHtml(L.RequiredField || '');
        alertEl.classList.remove('d-none');
    };

    const resetForm = () => {
        const form = document.getElementById('formPosition');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('positionItemId').value = '';
        document.getElementById('positionCode').value = '';
        document.getElementById('positionName').value = '';
        document.getElementById('positionCodePreview')?.classList.add('d-none');
        hideManagerChain();
        clearFormErrors();
    };

    const openCreateOffcanvas = () => {
        const oc = getOcInstance();
        if (!oc) {
            console.error('[Positions] #offcanvasCreateEdit not found. Rebuild + restart the app so the partial renders.');
            return;
        }
        editingId = null;
        resetForm();
        populateOrgUnitSelect('');
        populateReportsToSelect('', null);
        hideManagerChain();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSavePosition');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        oc.show();
    };

    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.Edit || '';
        const saveBtn = document.getElementById('btnSavePosition');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';

        try {
            const res = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { headers: getAuthHeaders() });
            if (!res.ok) throw new Error('Failed to load item.');
            const payload = await res.json();
            const d = payload.data || payload.Data || {};
            document.getElementById('positionItemId').value = d.id || d.Id || '';
            document.getElementById('positionCode').value = d.code || d.Code || '';
            document.getElementById('positionName').value = d.name || d.Name || '';
            populateOrgUnitSelect(d.organizationUnitId || d.OrganizationUnitId || '');
            populateReportsToSelect(d.reportsToPositionId || d.ReportsToPositionId || '', d.id || d.Id);
        } catch (error) {
            console.error('[Positions] Failed to load item for edit.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
            return;
        }

        // Manager chain is best-effort; a failure leaves the section hidden.
        try {
            const chainRes = await fetch(`${endpoint}/${encodeURIComponent(id)}/manager-chain`, { headers: getAuthHeaders() });
            if (chainRes.ok) {
                const chainPayload = await chainRes.json();
                const chainData = chainPayload.data || chainPayload.Data || {};
                renderManagerChain(chainData.chain || chainData.Chain || []);
            } else {
                hideManagerChain();
            }
        } catch (error) {
            console.error('[Positions] Failed to load manager chain.', error);
            hideManagerChain();
        }

        getOcInstance()?.show();
    };

    const submitForm = async () => {
        const form = document.getElementById('formPosition');
        if (!form) return;
        clearFormErrors();
        form.classList.add('was-validated');

        const organizationUnitId = readOrgUnitId();
        if (!form.checkValidity() || !organizationUnitId) {
            showFormErrors([L.RequiredField || '']);
            return;
        }

        const isEdit = !!editingId;
        const reportsToValue = normalizeString(document.getElementById('positionReportsTo')?.value);
        const payload = {
            code: normalizeCode(document.getElementById('positionCode').value),
            name: normalizeString(document.getElementById('positionName').value),
            organizationUnitId: organizationUnitId,
            reportsToPositionId: reportsToValue || null
        };
        const url = isEdit ? `${endpoint}/${encodeURIComponent(editingId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';

        const saveBtn = document.getElementById('btnSavePosition');
        if (saveBtn) saveBtn.disabled = true;
        try {
            const res = await fetch(url, {
                method,
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken(),
                    ...getAuthHeaders()
                },
                body: JSON.stringify(payload)
            });
            if (res.ok) {
                getOcInstance()?.hide();
                reloadWithSuccessToast(isEdit ? 'RecordUpdated' : 'RecordCreated', payload.name);
                return;
            }
            let errors = [];
            try {
                const json = await res.json();
                errors = (json.errors || json.Errors || []);
            } catch { /* non-JSON response */ }
            showFormErrors(errors.length ? errors : [L.ErrorOccurred || '']);
        } catch (error) {
            console.error('[Positions] Form submit failed.', error);
            showFormErrors([L.ErrorOccurred || '']);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    const bindOffcanvas = () => {
        // Delegated: the Add New button is rendered by DataTables into the toolbar, so bind at document level.
        document.addEventListener('click', (event) => {
            if (event.target.closest('.add-new')) {
                event.preventDefault();
                openCreateOffcanvas();
            }
        });
        document.getElementById('btnSavePosition')?.addEventListener('click', submitForm);
        document.getElementById('positionCode')?.addEventListener('input', function () {
            this.value = this.value.toUpperCase();
            updateCodePreview();
        });
    };

    const init = () => {
        loadL10n();
        bindFilters();
        bindOffcanvas();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => PositionsList.init());
