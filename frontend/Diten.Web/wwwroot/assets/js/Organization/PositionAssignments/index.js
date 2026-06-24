'use strict';

// Position Assignments (platform-admin, Slim pattern). Backend list endpoint returns a plain array
// (Response<IReadOnlyList<T>>), so the DataTable is client-side (serverSide:false): a single ajax fetch
// loads the whole list, with paging/sort/search handled in the browser. Create/edit happen in an in-page
// offcanvas; delete is a row action. Position + user options come from lookups (positions = Response<T>
// array, users = PaginatedResult). Unlike Organization Units there is NO GetById and NO archive endpoint,
// so edit populates the form directly from the clicked row object.
const PositionAssignmentsList = (function () {
    let dt;
    let editingId = null;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-position-assignments');
    const endpoint = '/PositionAssignments/api';
    const positionsEndpoint = '/PositionAssignments/api/positions';
    const usersEndpoint = '/PositionAssignments/api/users';
    const saveViewColumnIndexes = [1, 2, 3, 4];
    const baseOrder = [[1, 'asc']];

    // Loaded reference data + id→label maps used by the table renderers and offcanvas selects.
    let assignmentsData = [];
    let positionsData = [];
    let usersData = [];
    const positionMap = {};
    const userMap = {};

    const loadL10n = () => {
        const node = document.getElementById('position-assignments-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[PositionAssignments] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const toDateString = (value) => (value ? String(value).slice(0, 10) : '');

    const positionLabel = (position) => {
        const id = position.id || position.Id;
        const code = position.code || position.Code || '';
        const name = position.name || position.Name || '';
        return { id, text: code ? `${code} — ${name}` : name };
    };

    const userLabel = (user) => {
        const id = user.id || user.Id;
        const email = user.email || user.Email || '';
        const first = normalizeString(user.firstName || user.FirstName);
        const last = normalizeString(user.lastName || user.LastName);
        const fullName = `${first} ${last}`.trim();
        return { id, text: fullName ? `${fullName} (${email})` : email };
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
    };

    const rowActionHandlers = {
        edit: ({ row }) => {
            if (row) openEditOffcanvas(row);
        },
        delete: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = (row && positionMap[row.positionId || row.PositionId]) || '';
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

    const fetchAssignments = () => fetch(`${endpoint}`, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList);

    // Position lookup is best-effort: a failure resolves to [] so the table still renders.
    const fetchPositions = () => fetch(positionsEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList)
        .catch(() => []);

    // User lookup returns a PaginatedResult ({ items, totalCount, ... }), NOT the Response<T> envelope.
    const fetchUsers = () => fetch(usersEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then((payload) => payload?.items || payload?.Items || [])
        .catch(() => []);

    const rebuildMaps = () => {
        Object.keys(positionMap).forEach((k) => delete positionMap[k]);
        Object.keys(userMap).forEach((k) => delete userMap[k]);
        positionsData.forEach((p) => { const { id, text } = positionLabel(p); if (id) positionMap[id] = text; });
        usersData.forEach((u) => { const { id, text } = userLabel(u); if (id) userMap[id] = text; });
    };

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[PositionAssignments] DataTable element or DtDefaults not found.');
            return;
        }

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                Promise.all([fetchAssignments(), fetchPositions(), fetchUsers()])
                    .then(([assignments, positions, users]) => {
                        assignmentsData = assignments || [];
                        positionsData = positions || [];
                        usersData = users || [];
                        rebuildMaps();
                        callback({ data: assignmentsData });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                {
                    data: 'positionId', name: 'position',
                    render: (value) => escapeHtml(positionMap[value] || value || '-')
                },
                {
                    data: 'userId', name: 'user',
                    render: (value) => escapeHtml(userMap[value] || value || '-')
                },
                { data: 'effectiveFrom', name: 'effectiveFrom', render: (value) => escapeHtml(toDateString(value) || '-') },
                { data: 'effectiveTo', name: 'effectiveTo', render: (value) => escapeHtml(value ? toDateString(value) : '-') },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (value, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);

                        const actions = [
                            { key: 'edit', icon: 'bx bx-edit', className: 'js-edit-item', text: L.Edit || '', attrs: { 'data-id': id, 'data-json': rowJson } },
                            { key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete || '', attrs: { 'data-id': id, 'data-json': rowJson } }
                        ];

                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNew || '', {}, {}, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                window.DtDefaults.updateVisualState(api, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), 0);
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

    // ─── Create/Edit offcanvas (Slim pattern) ────────────────────────────────
    const getOcInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#formPositionAssignment input[name="__RequestVerificationToken"]')?.value || '';

    const populatePositionSelect = (selectedId) => {
        const select = document.getElementById('paPosition');
        if (!select) return;
        select.innerHTML = `<option value="">${escapeHtml(L.SelectPosition || '')}</option>`;
        positionsData.forEach((p) => {
            const { id, text } = positionLabel(p);
            if (!id) return;
            const option = document.createElement('option');
            option.value = id;
            option.textContent = text;
            if (selectedId && id === selectedId) option.selected = true;
            select.appendChild(option);
        });
    };

    const populateUserSelect = (selectedId) => {
        const select = document.getElementById('paUser');
        if (!select) return;
        select.innerHTML = `<option value="">${escapeHtml(L.SelectUser || '')}</option>`;
        usersData.forEach((u) => {
            const { id, text } = userLabel(u);
            if (!id) return;
            const option = document.createElement('option');
            option.value = id;
            option.textContent = text;
            if (selectedId && id === selectedId) option.selected = true;
            select.appendChild(option);
        });
    };

    const clearFormErrors = () => {
        const alertEl = document.getElementById('formPositionAssignmentAlert');
        if (alertEl) { alertEl.classList.add('d-none'); alertEl.innerHTML = ''; }
    };

    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formPositionAssignmentAlert');
        if (!alertEl) return;
        const list = (Array.isArray(errors) ? errors : [errors]).filter(Boolean);
        alertEl.innerHTML = list.length
            ? list.map((message) => `<div>${escapeHtml(message)}</div>`).join('')
            : escapeHtml(L.RequiredField || '');
        alertEl.classList.remove('d-none');
    };

    const resetForm = () => {
        const form = document.getElementById('formPositionAssignment');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('paItemId').value = '';
        document.getElementById('paEffectiveFrom').value = '';
        document.getElementById('paEffectiveTo').value = '';
        clearFormErrors();
    };

    const openCreateOffcanvas = () => {
        const oc = getOcInstance();
        if (!oc) {
            console.error('[PositionAssignments] #offcanvasCreateEdit not found. Rebuild + restart the app so the partial renders.');
            return;
        }
        editingId = null;
        resetForm();
        populatePositionSelect('');
        populateUserSelect('');
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSavePositionAssignment');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        oc.show();
    };

    // No GetById endpoint: populate the form directly from the clicked row object.
    const openEditOffcanvas = (row) => {
        if (!row) return;
        editingId = row.id || row.Id || null;
        resetForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.Edit || '';
        const saveBtn = document.getElementById('btnSavePositionAssignment');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';

        document.getElementById('paItemId').value = row.id || row.Id || '';
        populatePositionSelect(row.positionId || row.PositionId || '');
        populateUserSelect(row.userId || row.UserId || '');
        document.getElementById('paEffectiveFrom').value = toDateString(row.effectiveFrom || row.EffectiveFrom);
        document.getElementById('paEffectiveTo').value = toDateString(row.effectiveTo || row.EffectiveTo);

        getOcInstance()?.show();
    };

    const submitForm = async () => {
        const form = document.getElementById('formPositionAssignment');
        if (!form) return;
        clearFormErrors();
        form.classList.add('was-validated');

        const positionId = normalizeString(document.getElementById('paPosition')?.value);
        const userId = normalizeString(document.getElementById('paUser')?.value);
        const effectiveFromValue = normalizeString(document.getElementById('paEffectiveFrom')?.value);
        const effectiveToValue = normalizeString(document.getElementById('paEffectiveTo')?.value);

        if (!form.checkValidity() || !positionId || !userId || !effectiveFromValue) {
            showFormErrors([L.RequiredField || '']);
            return;
        }

        const isEdit = !!editingId;
        const payload = {
            positionId: positionId,
            userId: userId,
            effectiveFrom: new Date(effectiveFromValue).toISOString(),
            effectiveTo: effectiveToValue ? new Date(effectiveToValue).toISOString() : null
        };
        const url = isEdit ? `${endpoint}/${encodeURIComponent(editingId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';

        const saveBtn = document.getElementById('btnSavePositionAssignment');
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
                reloadWithSuccessToast(isEdit ? 'RecordUpdated' : 'RecordCreated', positionMap[positionId] || '');
                return;
            }
            let errors = [];
            try {
                const json = await res.json();
                errors = (json.errors || json.Errors || []);
            } catch { /* non-JSON response */ }
            showFormErrors(errors.length ? errors : [L.ErrorOccurred || '']);
        } catch (error) {
            console.error('[PositionAssignments] Form submit failed.', error);
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
        document.getElementById('btnSavePositionAssignment')?.addEventListener('click', submitForm);
    };

    const init = () => {
        loadL10n();
        bindOffcanvas();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => PositionAssignmentsList.init());
