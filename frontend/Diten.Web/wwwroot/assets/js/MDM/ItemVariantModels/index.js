'use strict';

const ItemVariantModelsList = (function () {
    let dt;
    let itemTypes = [];
    let attributeRows = [];
    let L = window.L10n || {};

    const dtTableEl = document.querySelector('.datatables-item-variant-models');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        return parts.length === 2 ? parts.pop().split(';').shift() : null;
    };

    const getTenantId = () => {
        try {
            return JSON.parse(localStorage.getItem('user') || '{}').tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (error) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getAuthHeaders = (includeJsonContentType = false) => {
        const token = getCookie('access_token');
        const headers = {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
        if (includeJsonContentType) headers['Content-Type'] = 'application/json';
        return headers;
    };

    const populateOffcanvas = (data) => {
        if (!data) {
            return;
        }

        document.getElementById('oc-title').innerText = data.name || '-';
        document.getElementById('oc-subtitle').innerText = data.itemType || '-';
        document.getElementById('oc-code').innerText = data.code || '-';
        document.getElementById('oc-name').innerText = data.name || '-';
        document.getElementById('oc-itemType').innerText = data.itemType || '-';
        document.getElementById('oc-description').innerText = data.description || '-';
        document.getElementById('oc-attributes').innerText = Array.isArray(data.attributes) ? data.attributes.map((attribute) => attribute.name).join(', ') || '-' : '-';
        document.getElementById('oc-btn-edit').onclick = () => {
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).hide();
            openEditor(data);
        };
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
        }
    };

    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        btn.addEventListener('click', (event) => {
            event.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false });
            collapseEl.classList.contains('show') ? instance.hide() : instance.show();
        });
    };

    const loadLookups = async () => {
        const response = await fetch(`${apiUrl}/api/item-types`, { headers: getAuthHeaders() });
        itemTypes = response.ok ? ((await response.json()).data || []) : [];
        const markup = itemTypes.map((option) => `<option value="${option.id}">${option.name}</option>`).join('');
        document.getElementById('filterItemType').innerHTML = `<option value=""></option>${markup}`;
        document.getElementById('VariantModelItemTypeId').innerHTML = `<option value=""></option>${markup}`;

        if (window.jQuery && $.fn.select2) {
            $('#filterItemType, #filterStatus').select2({ dropdownParent: $('#inlineFilterCollapse'), selectionCssClass: 'form-select form-select-sm', allowClear: true });
            $('#VariantModelItemTypeId').select2({ dropdownParent: $('#offcanvasVariantModelEditor'), allowClear: true });
        }
    };

    const renderAttributeEditor = () => {
        const host = document.getElementById('variantAttributeEditor');
        host.innerHTML = '';
        if (!attributeRows.length) {
            host.innerHTML = `<div class="text-muted">${L.NoAttributesDefined}</div>`;
            return;
        }

        attributeRows.forEach((attribute, index) => {
            const row = document.createElement('div');
            row.className = 'border rounded p-3';
            row.innerHTML = `
                <div class="row g-3">
                    <div class="col-md-4">
                        <label class="form-label">${L.Code}</label>
                        <input type="text" class="form-control js-attribute-code" data-index="${index}" value="${attribute.code || ''}">
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">${L.Name}</label>
                        <input type="text" class="form-control js-attribute-name" data-index="${index}" value="${attribute.name || ''}">
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">${L.Type}</label>
                        <input type="text" class="form-control js-attribute-type" data-index="${index}" value="${attribute.dataType || L.TextDataType}">
                    </div>
                    <div class="col-md-4">
                        <div class="form-check form-switch">
                            <input type="checkbox" class="form-check-input js-attribute-required" data-index="${index}" ${attribute.isRequired ? 'checked' : ''}>
                            <label class="form-check-label">${L.Required}</label>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="form-check form-switch">
                            <input type="checkbox" class="form-check-input js-attribute-axis" data-index="${index}" ${attribute.isVariantAxis ? 'checked' : ''}>
                            <label class="form-check-label">${L.VariantAxis}</label>
                        </div>
                    </div>
                    <div class="col-md-4 d-flex align-items-end justify-content-end">
                        <button type="button" class="btn btn-sm btn-label-danger js-remove-attribute" data-index="${index}">${L.Remove}</button>
                    </div>
                </div>`;
            host.appendChild(row);
        });
    };

    const collectAttributes = () => {
        attributeRows = Array.from(document.querySelectorAll('.js-attribute-code')).map((input) => {
            const index = input.dataset.index;
            return {
                code: input.value.trim(),
                name: document.querySelector(`.js-attribute-name[data-index="${index}"]`)?.value?.trim() || '',
                dataType: document.querySelector(`.js-attribute-type[data-index="${index}"]`)?.value?.trim() || L.TextDataType,
                isRequired: !!document.querySelector(`.js-attribute-required[data-index="${index}"]`)?.checked,
                isVariantAxis: !!document.querySelector(`.js-attribute-axis[data-index="${index}"]`)?.checked,
                sortOrder: Number(index) + 1
            };
        }).filter((attribute) => attribute.code || attribute.name);
    };

    const resetEditor = () => {
        document.getElementById('VariantModelId').value = '';
        document.getElementById('VariantModelCode').value = '';
        document.getElementById('VariantModelName').value = '';
        document.getElementById('VariantModelDescription').value = '';
        document.getElementById('VariantModelItemTypeId').value = '';
        document.getElementById('VariantModelIsActive').checked = true;
        attributeRows = [];
        renderAttributeEditor();
        $('#VariantModelItemTypeId').trigger('change');
        document.getElementById('variantModelEditorTitle').innerText = L.AddNewItemVariantModels;
    };

    const openEditor = (data) => {
        resetEditor();
        if (data) {
            document.getElementById('VariantModelId').value = data.id;
            document.getElementById('VariantModelCode').value = data.code || '';
            document.getElementById('VariantModelName').value = data.name || '';
            document.getElementById('VariantModelDescription').value = data.description || '';
            document.getElementById('VariantModelItemTypeId').value = data.itemTypeId || '';
            document.getElementById('VariantModelIsActive').checked = data.isActive !== false;
            attributeRows = Array.isArray(data.attributes) ? data.attributes.map((attribute) => ({
                attributeDefinitionId: attribute.attributeDefinitionId,
                code: attribute.code,
                name: attribute.name,
                dataType: attribute.dataType,
                isRequired: attribute.isRequired,
                isVariantAxis: attribute.isVariantAxis,
                sortOrder: attribute.sortOrder
            })) : [];
            renderAttributeEditor();
            $('#VariantModelItemTypeId').trigger('change');
            document.getElementById('variantModelEditorTitle').innerText = L.Edit;
        }

        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasVariantModelEditor')).show();
    };

    const submitEditor = async (event) => {
        event.preventDefault();
        collectAttributes();

        const id = document.getElementById('VariantModelId').value;
        const payload = {
            code: document.getElementById('VariantModelCode').value.trim(),
            name: document.getElementById('VariantModelName').value.trim(),
            description: document.getElementById('VariantModelDescription').value.trim(),
            itemTypeId: document.getElementById('VariantModelItemTypeId').value,
            isActive: document.getElementById('VariantModelIsActive').checked,
            attributes: attributeRows
        };

        const response = await fetch(`${apiUrl}/api/item-variant-models${id ? `/${id}` : ''}`, {
            method: id ? 'PUT' : 'POST',
            headers: getAuthHeaders(true),
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }

        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasVariantModelEditor')).hide();
        dt.ajax.reload(() => window.showToast?.(L.RecordSaved, 'success'), false);
    };

    const applyFilters = () => {
        const itemType = document.getElementById('filterItemType').value || '';
        const status = document.getElementById('filterStatus').value || '';
        dt.column('itemType:name').search(itemType);
        dt.column('isActive:name').search(status);
        dt.draw();
        window.DtDefaults.updateVisualState(dt, [itemType, status].filter(Boolean).length);
    };

    const getSelectedIds = () => Array.from(dtTableEl.querySelectorAll('.dt-checkboxes:checked')).map((checkbox) => checkbox.value);

    const updateBulkBar = () => {
        const ids = getSelectedIds();
        document.getElementById('bulkActionBar').classList.toggle('d-none', ids.length === 0);
        document.getElementById('bulkSelectedCount').textContent = ids.length;
    };

    const clearSelection = () => {
        dtTableEl.querySelectorAll('.dt-checkboxes').forEach((checkbox) => {
            checkbox.checked = false;
            checkbox.closest('tr')?.classList.remove('selected');
        });
        updateBulkBar();
    };

    const initDataTable = async () => {
        await loadLookups();
        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/item-variant-models',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'code', name: 'code' },
                { data: 'name', name: 'name' },
                { data: 'itemTypeId', name: 'itemType' },
                { data: 'attributes', name: 'attributes' },
                { data: 'isActive', name: 'isActive' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', orderable: false, searchable: false, render: () => '' },
                { targets: 1, className: 'dt-checkboxes-cell cell-fit', orderable: false, searchable: false, render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                { targets: 4, render: (data, type, full) => full.itemType || '' },
                { targets: 5, render: (data) => Array.isArray(data) ? data.map((attribute) => attribute.name).join(', ') : '-' },
                {
                    targets: 6,
                    render: (data, type) => {
                        const status = data ? { title: L.Active, class: 'bg-label-success' } : { title: L.Passive, class: 'bg-label-secondary' };
                        return type === 'display' ? `<span class="badge ${status.class}">${status.title}</span>` : status.title;
                    }
                },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit',
                    render: () => `
                        <div class="d-flex align-items-center">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon edit-record text-primary me-1"><i class="bx bx-edit-alt icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon js-quick-view text-secondary" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview"><i class="bx bx-show icon-md"></i></a>
                        </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewItemVariantModels,
                { id: 'btnAddVariantModel', onclick: 'return false;' },
                {
                    importBtn: {
                        text: '<i class="icon-base bx bx-import icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary',
                        attr: { title: L.Import },
                        action: function () { window.showToast?.(L.ComingSoon, 'warning'); }
                    },
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn'
                    }
                },
                {
                    exportColumns: [2, 3, 4, 5, 6],
                    colvisColumns: [2, 3, 4, 5, 6]
                }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), [document.getElementById('filterItemType').value, document.getElementById('filterStatus').value].filter(Boolean).length);
            }
        }));
    };

    const bindEvents = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', applyFilters);
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            $('#filterItemType, #filterStatus').val('').trigger('change');
            applyFilters();
        });

        document.addEventListener('click', (event) => {
            if (event.target.closest('#btnAddVariantModel')) {
                event.preventDefault();
                openEditor(null);
            }
        });

        document.getElementById('btnAddVariantAttribute')?.addEventListener('click', () => {
            attributeRows.push({ code: '', name: '', dataType: L.TextDataType, isRequired: false, isVariantAxis: false, sortOrder: attributeRows.length + 1 });
            renderAttributeEditor();
        });

        document.getElementById('variantAttributeEditor')?.addEventListener('click', (event) => {
            const removeBtn = event.target.closest('.js-remove-attribute');
            if (!removeBtn) return;
            attributeRows = attributeRows.filter((_, index) => index !== Number(removeBtn.dataset.index));
            renderAttributeEditor();
        });

        document.getElementById('variantModelEditorForm')?.addEventListener('submit', submitEditor);
        document.getElementById('btnClearSelection')?.addEventListener('click', clearSelection);
        document.getElementById('btnBulkDelete')?.addEventListener('click', async () => {
            const ids = getSelectedIds();
            if (!ids.length) return;
            const response = await fetch(`${apiUrl}/api/item-variant-models/bulk`, {
                method: 'DELETE',
                headers: getAuthHeaders(true),
                body: JSON.stringify({ ids })
            });
            if (response.ok) {
                clearSelection();
                dt.ajax.reload(() => window.showToast?.((L.BulkDeleteSuccess || '').replace('{0}', ids.length), 'success'), false);
            } else {
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        });

        dtTableEl?.addEventListener('click', async (event) => {
            const quickViewBtn = event.target.closest('.js-quick-view');
            if (quickViewBtn) {
                let rowEl = quickViewBtn.closest('tr');
                if (rowEl.classList.contains('child')) rowEl = rowEl.previousElementSibling;
                populateOffcanvas(dt.row(rowEl).data());
            }

            const editBtn = event.target.closest('.edit-record');
            if (editBtn) {
                let rowEl = editBtn.closest('tr');
                if (rowEl.classList.contains('child')) rowEl = rowEl.previousElementSibling;
                openEditor(dt.row(rowEl).data());
            }

            const deleteBtn = event.target.closest('.delete-record');
            if (deleteBtn) {
                let rowEl = deleteBtn.closest('tr');
                if (rowEl.classList.contains('child')) rowEl = rowEl.previousElementSibling;
                const data = dt.row(rowEl).data();
                const response = await fetch(`${apiUrl}/api/item-variant-models/${data.id}`, { method: 'DELETE', headers: getAuthHeaders() });
                if (response.ok) {
                    dt.ajax.reload(() => window.showToast?.(L.RecordDeleted, 'success'), false);
                } else {
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            $(this).closest('tr').toggleClass('selected', this.checked);
            updateBulkBar();
        });

        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const checked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach((checkbox) => {
                checkbox.checked = checked;
                checkbox.closest('tr')?.classList.toggle('selected', checked);
            });
            updateBulkBar();
        });
    };

    return {
        init: async function () {
            await initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ItemVariantModelsList.init());
