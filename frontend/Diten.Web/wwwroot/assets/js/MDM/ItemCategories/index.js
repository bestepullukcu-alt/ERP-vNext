'use strict';

const ItemCategoriesList = (function () {
    let dt;
    let itemTypes = [];
    let categoryOptions = [];
    let L = window.L10n || {};

    const dtTableEl = document.querySelector('.datatables-item-categories');
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
        document.getElementById('oc-parentCategory').innerText = data.parentCategory || '-';
        document.getElementById('oc-description').innerText = data.description || '-';
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
        const [itemTypesResponse, categoriesResponse] = await Promise.all([
            fetch(`${apiUrl}/api/item-types`, { headers: getAuthHeaders() }),
            fetch(`${apiUrl}/api/item-categories`, { headers: getAuthHeaders() })
        ]);

        itemTypes = itemTypesResponse.ok ? ((await itemTypesResponse.json()).data || []) : [];
        categoryOptions = categoriesResponse.ok ? ((await categoriesResponse.json()).data || []) : [];

        const itemTypeMarkup = itemTypes.map((option) => `<option value="${option.id}">${option.name}</option>`).join('');
        document.getElementById('filterItemType').innerHTML = `<option value=""></option>${itemTypeMarkup}`;
        document.getElementById('CategoryItemTypeId').innerHTML = `<option value=""></option>${itemTypeMarkup}`;

        const parentMarkup = categoryOptions.map((option) => `<option value="${option.id}" data-item-type-id="${option.itemTypeId}">${option.name}</option>`).join('');
        document.getElementById('ParentCategoryId').innerHTML = `<option value=""></option>${parentMarkup}`;

        if (window.jQuery && $.fn.select2) {
            $('#filterItemType, #filterStatus').select2({ dropdownParent: $('#inlineFilterCollapse'), selectionCssClass: 'form-select form-select-sm', allowClear: true });
            $('#CategoryItemTypeId, #ParentCategoryId').select2({ dropdownParent: $('#offcanvasCategoryEditor'), allowClear: true });
        }
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

    const resetEditor = () => {
        document.getElementById('CategoryId').value = '';
        document.getElementById('CategoryCode').value = '';
        document.getElementById('CategoryName').value = '';
        document.getElementById('CategoryDescription').value = '';
        document.getElementById('CategoryItemTypeId').value = '';
        document.getElementById('ParentCategoryId').value = '';
        document.getElementById('CategoryIsActive').checked = true;
        $('#CategoryItemTypeId, #ParentCategoryId').trigger('change');
        document.getElementById('categoryEditorTitle').innerText = L.AddNewItemCategories;
    };

    const openEditor = (data) => {
        resetEditor();
        if (data) {
            document.getElementById('CategoryId').value = data.id;
            document.getElementById('CategoryCode').value = data.code || '';
            document.getElementById('CategoryName').value = data.name || '';
            document.getElementById('CategoryDescription').value = data.description || '';
            document.getElementById('CategoryItemTypeId').value = data.itemTypeId || '';
            document.getElementById('ParentCategoryId').value = data.parentCategoryId || '';
            document.getElementById('CategoryIsActive').checked = data.isActive !== false;
            $('#CategoryItemTypeId, #ParentCategoryId').trigger('change');
            document.getElementById('categoryEditorTitle').innerText = L.Edit;
        }

        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCategoryEditor')).show();
    };

    const submitEditor = async (event) => {
        event.preventDefault();
        const id = document.getElementById('CategoryId').value;
        const payload = {
            code: document.getElementById('CategoryCode').value.trim(),
            name: document.getElementById('CategoryName').value.trim(),
            description: document.getElementById('CategoryDescription').value.trim(),
            itemTypeId: document.getElementById('CategoryItemTypeId').value,
            parentCategoryId: document.getElementById('ParentCategoryId').value || null,
            isActive: document.getElementById('CategoryIsActive').checked
        };

        const response = await fetch(`${apiUrl}/api/item-categories${id ? `/${id}` : ''}`, {
            method: id ? 'PUT' : 'POST',
            headers: getAuthHeaders(true),
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }

        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCategoryEditor')).hide();
        dt.ajax.reload(() => window.showToast?.(L.RecordSaved, 'success'), false);
    };

    const initDataTable = async () => {
        await loadLookups();
        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/item-categories',
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
                { data: 'parentCategory', name: 'parentCategory' },
                { data: 'isActive', name: 'isActive' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', orderable: false, searchable: false, render: () => '' },
                { targets: 1, className: 'dt-checkboxes-cell cell-fit', orderable: false, searchable: false, render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                { targets: 4, render: (data, type, full) => full.itemType || '' },
                { targets: 5, render: (data) => data || '-' },
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
                    render: (data, type, full) => `
                        <div class="d-flex align-items-center">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon edit-record text-primary me-1"><i class="bx bx-edit-alt icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon js-quick-view text-secondary" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview"><i class="bx bx-show icon-md"></i></a>
                        </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewItemCategories,
                { id: 'btnAddCategory', onclick: 'return false;' },
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
            if (event.target.closest('#btnAddCategory')) {
                event.preventDefault();
                openEditor(null);
            }
        });

        document.getElementById('categoryEditorForm')?.addEventListener('submit', submitEditor);
        document.getElementById('btnClearSelection')?.addEventListener('click', clearSelection);
        document.getElementById('btnBulkDelete')?.addEventListener('click', async () => {
            const ids = getSelectedIds();
            if (!ids.length) return;
            const response = await fetch(`${apiUrl}/api/item-categories/bulk`, {
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
                const response = await fetch(`${apiUrl}/api/item-categories/${data.id}`, { method: 'DELETE', headers: getAuthHeaders() });
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

document.addEventListener('DOMContentLoaded', () => ItemCategoriesList.init());
