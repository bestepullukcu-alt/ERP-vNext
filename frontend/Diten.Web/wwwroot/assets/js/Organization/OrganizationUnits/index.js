'use strict';

// MOD-0288 — Organization Units (platform-admin, Slim pattern). Backend list endpoints return a plain
// array (Response<IReadOnlyList<T>>), so the DataTable is client-side (serverSide:false): a single ajax
// fetch loads the whole list, with paging/sort/search handled in the browser. Create/edit happen in an
// in-page offcanvas; archive + delete are row actions. Legal-entity options come from an MDM lookup that
// degrades to a manual GUID input when MDM is unreachable.
const OrganizationUnitsList = (function () {
    let dt;
    let editingId = null;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-organization-units');
    const endpoint = '/OrganizationUnits/api';
    const legalEntitiesEndpoint = '/OrganizationUnits/api/legal-entities';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { archived: '' };

    // Loaded reference data + id→label maps used by the table renderers and offcanvas selects.
    let orgUnitsData = [];
    let legalEntitiesData = [];
    let legalEntityLookupAvailable = false;
    const orgUnitMap = {};
    const legalEntityMap = {};

    const loadL10n = () => {
        const node = document.getElementById('organization-units-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[OrganizationUnits] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');

    const archivedBadge = (value) => value
        ? `<span class="badge bg-label-secondary">${escapeHtml(L.StatusArchived || 'Archived')}</span>`
        : `<span class="badge bg-label-success">${escapeHtml(L.StatusActive || 'Active')}</span>`;

    const legalEntityLabel = (entity) => {
        const id = entity.legalEntityId || entity.id || entity.Id || entity.LegalEntityId;
        const name = entity.displayName || entity.DisplayName || entity.legalName || entity.LegalName || '';
        const code = entity.code || entity.Code || '';
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

    const fetchOrgUnits = () => fetch(`${endpoint}`, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList);

    // Legal-entity lookup is best-effort: a failure (e.g. MDM offline) resolves to [] so the table still
    // renders and the offcanvas falls back to a manual GUID input.
    const fetchLegalEntities = () => fetch(legalEntitiesEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then((payload) => { legalEntityLookupAvailable = true; return unwrapList(payload); })
        .catch(() => { legalEntityLookupAvailable = false; return []; });

    const rebuildMaps = () => {
        Object.keys(orgUnitMap).forEach((k) => delete orgUnitMap[k]);
        Object.keys(legalEntityMap).forEach((k) => delete legalEntityMap[k]);
        orgUnitsData.forEach((u) => { orgUnitMap[u.id || u.Id] = u.name || u.Name || ''; });
        legalEntitiesData.forEach((e) => { const { id, text } = legalEntityLabel(e); if (id) legalEntityMap[id] = text; });
    };

    const applyClientFilter = (rows) => {
        if (appliedFilters.archived === '') return rows;
        const wantArchived = appliedFilters.archived === 'true';
        return rows.filter((r) => Boolean(r.isArchived ?? r.IsArchived) === wantArchived);
    };

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[OrganizationUnits] DataTable element or DtDefaults not found.');
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
                Promise.all([fetchOrgUnits(), fetchLegalEntities()])
                    .then(([units, legalEntities]) => {
                        orgUnitsData = units || [];
                        legalEntitiesData = legalEntities || [];
                        rebuildMaps();
                        callback({ data: applyClientFilter(orgUnitsData) });
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
                    data: 'legalEntityId', name: 'legalEntity',
                    render: (value) => escapeHtml(legalEntityMap[value] || value || '-')
                },
                {
                    data: 'parentOrganizationUnitId', name: 'parent',
                    render: (value) => escapeHtml(value ? (orgUnitMap[value] || value) : '-')
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
        document.querySelector('#formOrganizationUnit input[name="__RequestVerificationToken"]')?.value || '';

    const normalizeCode = (value) => (value || '')
        .toUpperCase()
        .replace(/[^A-Z0-9]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-+|-+$/g, '')
        .slice(0, 80)
        .replace(/-+$/g, '');

    const updateCodePreview = () => {
        const input = document.getElementById('orgUnitCode');
        const preview = document.getElementById('orgUnitCodePreview');
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

    const populateLegalEntitySelect = (selectedId) => {
        const select = document.getElementById('orgUnitLegalEntity');
        const manual = document.getElementById('orgUnitLegalEntityManual');
        const hint = document.getElementById('orgUnitLegalEntityHint');
        if (!select) return;

        if (legalEntityLookupAvailable && legalEntitiesData.length > 0) {
            // Dropdown mode.
            select.classList.remove('d-none');
            manual?.classList.add('d-none');
            hint?.classList.add('d-none');
            if (manual) manual.value = '';
            select.innerHTML = `<option value="">${escapeHtml(L.SelectLegalEntity || '')}</option>`;
            legalEntitiesData.forEach((e) => {
                const { id, text } = legalEntityLabel(e);
                if (!id) return;
                const option = document.createElement('option');
                option.value = id;
                option.textContent = text;
                if (selectedId && id === selectedId) option.selected = true;
                select.appendChild(option);
            });
            select.required = true;
        } else {
            // Manual GUID fallback (MDM lookup unavailable).
            select.classList.add('d-none');
            select.required = false;
            manual?.classList.remove('d-none');
            hint?.classList.remove('d-none');
            if (manual) manual.value = selectedId || '';
        }
    };

    const populateParentSelect = (selectedId, excludeId) => {
        const select = document.getElementById('orgUnitParent');
        if (!select) return;
        select.innerHTML = `<option value="">${escapeHtml(L.NoParent || '')}</option>`;
        orgUnitsData
            .filter((u) => (u.id || u.Id) !== excludeId)
            .forEach((u) => {
                const id = u.id || u.Id;
                const option = document.createElement('option');
                option.value = id;
                option.textContent = `${u.code || u.Code || ''} — ${u.name || u.Name || ''}`;
                if (selectedId && id === selectedId) option.selected = true;
                select.appendChild(option);
            });
    };

    const readLegalEntityId = () => {
        const select = document.getElementById('orgUnitLegalEntity');
        const manual = document.getElementById('orgUnitLegalEntityManual');
        if (select && !select.classList.contains('d-none')) return normalizeString(select.value);
        return normalizeString(manual?.value);
    };

    const clearFormErrors = () => {
        const alertEl = document.getElementById('formOrganizationUnitAlert');
        if (alertEl) { alertEl.classList.add('d-none'); alertEl.innerHTML = ''; }
    };

    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formOrganizationUnitAlert');
        if (!alertEl) return;
        const list = (Array.isArray(errors) ? errors : [errors]).filter(Boolean);
        alertEl.innerHTML = list.length
            ? list.map((message) => `<div>${escapeHtml(message)}</div>`).join('')
            : escapeHtml(L.RequiredField || '');
        alertEl.classList.remove('d-none');
    };

    const resetForm = () => {
        const form = document.getElementById('formOrganizationUnit');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('orgUnitItemId').value = '';
        document.getElementById('orgUnitCode').value = '';
        document.getElementById('orgUnitName').value = '';
        document.getElementById('orgUnitCodePreview')?.classList.add('d-none');
        clearFormErrors();
    };

    const openCreateOffcanvas = () => {
        const oc = getOcInstance();
        if (!oc) {
            console.error('[OrganizationUnits] #offcanvasCreateEdit not found. Rebuild + restart the app so the partial renders.');
            return;
        }
        editingId = null;
        resetForm();
        populateLegalEntitySelect('');
        populateParentSelect('', null);
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSaveOrgUnit');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        oc.show();
    };

    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.Edit || '';
        const saveBtn = document.getElementById('btnSaveOrgUnit');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';

        try {
            const res = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { headers: getAuthHeaders() });
            if (!res.ok) throw new Error('Failed to load item.');
            const payload = await res.json();
            const d = payload.data || payload.Data || {};
            document.getElementById('orgUnitItemId').value = d.id || d.Id || '';
            document.getElementById('orgUnitCode').value = d.code || d.Code || '';
            document.getElementById('orgUnitName').value = d.name || d.Name || '';
            populateLegalEntitySelect(d.legalEntityId || d.LegalEntityId || '');
            populateParentSelect(d.parentOrganizationUnitId || d.ParentOrganizationUnitId || '', d.id || d.Id);
        } catch (error) {
            console.error('[OrganizationUnits] Failed to load item for edit.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
            return;
        }
        getOcInstance()?.show();
    };

    const submitForm = async () => {
        const form = document.getElementById('formOrganizationUnit');
        if (!form) return;
        clearFormErrors();
        form.classList.add('was-validated');

        const legalEntityId = readLegalEntityId();
        if (!form.checkValidity() || !legalEntityId) {
            showFormErrors([L.RequiredField || '']);
            return;
        }

        const isEdit = !!editingId;
        const parentValue = normalizeString(document.getElementById('orgUnitParent')?.value);
        const payload = {
            code: normalizeCode(document.getElementById('orgUnitCode').value),
            name: normalizeString(document.getElementById('orgUnitName').value),
            legalEntityId: legalEntityId,
            parentOrganizationUnitId: parentValue || null
        };
        const url = isEdit ? `${endpoint}/${encodeURIComponent(editingId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';

        const saveBtn = document.getElementById('btnSaveOrgUnit');
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
            console.error('[OrganizationUnits] Form submit failed.', error);
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
        document.getElementById('btnSaveOrgUnit')?.addEventListener('click', submitForm);
        document.getElementById('orgUnitCode')?.addEventListener('input', function () {
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

document.addEventListener('DOMContentLoaded', () => OrganizationUnitsList.init());
