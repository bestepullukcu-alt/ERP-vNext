/**
 * Legal Entities DataTables Initialization (Refactored Module Pattern)
 */

'use strict';

const LegalEntitiesList = (function () {
    // Constants & Variables
    let dt;
    const dtTableEl = document.querySelector('.datatables-legal-entities');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const L = window.L10n || {};

    const statusObj = {
        true: { title: L.Active || 'Active', class: 'bg-label-success' },
        false: { title: L.Passive || 'Passive', class: 'bg-label-secondary' }
    };

    /**
     * Initialize DataTable
     */
    const initDataTable = () => {
        if (!dtTableEl) return;

        // Uzantı butonları (Merkezi butona ek olarak)
        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import || 'Import', 'data-bs-toggle': 'tooltip' },
                action: function () {
                    if (window.showToast) window.showToast(L.ComingSoon || 'Coming soon', 'info');
                }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter || 'Filter', 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#offcanvasFilter' }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/legal-entities',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: { 'X-Tenant-Id': '00000000-0000-0000-0000-000000000001' }
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'title', name: 'title' },
                { data: 'taxNumber', name: 'taxNumber' },
                { data: 'taxOffice', name: 'taxOffice' },
                { data: 'email', name: 'email' },
                { data: 'phone', name: 'phone' },
                { data: 'companyType', name: 'companyType' },
                { data: 'isActive', name: 'isActive' },
                { data: 'action', name: 'action' }
            ],
            preXhr: function () {
                // Veri yüklenmeye başladığında skeleton'ı geri getir
                $('#skeleton-loader').fadeIn(100);
            },
            columnDefs: [
                { className: 'control', searchable: false, orderable: false, responsivePriority: 2, targets: 0, render: () => '' },
                {
                    targets: 1, orderable: false, searchable: false, responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: function (data) {
                        return '<input type="checkbox" class="dt-checkboxes form-check-input" value="' + data + '">';
                    }
                },
                { targets: 5, render: (data) => data ? `<a href="mailto:${data}">${data}</a>` : '-' },
                {
                    targets: 8,
                    render: (data) => {
                        const status = statusObj[String(data)] || { title: L.Unknown || 'Unknown', class: 'bg-label-primary' };
                        return `<span class="badge ${status.class}" text-capitalized>${status.title}</span>`;
                    }
                },
                {
                    targets: -1, title: L.Actions || 'Actions', searchable: false, orderable: false,
                    className: 'cell-fit',
                    render: (data, type, full) => `
            <div class="d-flex align-items-center">
              <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
              <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
              <div class="dropdown-menu dropdown-menu-end m-0">
                <a href="/LegalEntities/Details/${full['id']}" class="dropdown-item">${L.ViewDetails || 'View'}</a>
                <a href="/LegalEntities/Edit/${full['id']}" class="dropdown-item">${L.Edit || 'Edit'}</a>
              </div>
            </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewCompany || 'Add New Company', {
                onclick: "window.location.href='/LegalEntities/Create'"
            }, extraButtons),
            initComplete: function () {
                setupFilters(this.api());
            },
            drawCallback: function () {
                // Her çizimde (arama, sayfalama vb.) görsel durumları güncelle
                const count = [$('#UserPlan').val(), $('#FilterTransaction').val()].filter(Boolean).length;
                window.DtDefaults.updateVisualState(this.api(), count);
            }
        }));

        // Sütun görünürlüğü değiştiğinde görsel durumları tetikle
        dt.on('column-visibility.dt', function () {
            const count = [$('#UserPlan').val(), $('#FilterTransaction').val()].filter(Boolean).length;
            window.DtDefaults.updateVisualState(dt, count);
        });
    };

    /**
     * Setup Filters
     */
    const setupFilters = (api) => {
        const createSelectFilter = (columnSelector, containerClass, selectId, defaultOptionText) => {
            const container = document.querySelector(containerClass);
            if (!container) return;
            const select = document.createElement('select');
            select.id = selectId;
            select.className = 'form-select select2 text-capitalize';
            select.innerHTML = `<option value="">${defaultOptionText}</option>`;
            container.appendChild(select);

            const column = api.column(columnSelector);
            Array.from(new Set(column.data().toArray())).sort().forEach(d => {
                if (!d) return;
                const option = document.createElement('option');
                option.value = d;
                option.textContent = d;
                select.appendChild(option);
            });

            // Initialize Select2
            $(select).select2({
                placeholder: defaultOptionText,
                dropdownParent: $('#offcanvasFilter'),
                minimumResultsForSearch: 5
            });
        };

        // Company Type Filter
        createSelectFilter('companyType:name', '.user_plan', 'UserPlan', L.CompanyType || 'Select Type');

        // Status filter (special handling for statusObj)
        const statusContainer = document.querySelector('.user_status');
        if (statusContainer) {
            const selectId = 'FilterTransaction';
            const select = document.createElement('select');
            select.id = selectId;
            select.className = 'form-select select2 text-capitalize';
            select.innerHTML = `<option value="">${L.SelectStatus || 'Select Status'}</option>`;
            statusContainer.appendChild(select);

            const statusColumn = api.column('isActive:name');
            Array.from(new Set(statusColumn.data().toArray())).sort().forEach(d => {
                const status = statusObj[String(d)] || { title: L.Unknown || 'Unknown' };
                const option = document.createElement('option');
                option.value = status.title;
                option.textContent = status.title;
                select.appendChild(option);
            });

            // Initialize Select2
            $(select).select2({
                placeholder: L.SelectStatus || 'Select Status',
                dropdownParent: $('#offcanvasFilter'),
                minimumResultsForSearch: Infinity // No search for status
            });
        }

        // Restore state values to UI
        const state = api.state.loaded();
        let initialFilterCount = 0;

        if (state) {
            const companyTypeCol = api.column('companyType:name').index();
            const statusCol = api.column('isActive:name').index();

            if (companyTypeCol !== undefined && state.columns[companyTypeCol].search.search) {
                const val = state.columns[companyTypeCol].search.search.replace(/\^|\$/g, '').replace(/\\/g, '');
                $('#UserPlan').val(val).trigger('change');
                if (val) initialFilterCount++;
            }
            if (statusCol !== undefined && state.columns[statusCol].search.search) {
                const val = state.columns[statusCol].search.search.replace(/\^|\$/g, '').replace(/\\/g, '');
                $('#FilterTransaction').val(val).trigger('change');
                if (val) initialFilterCount++;
            }
        }

        // Initial visual sync
        window.DtDefaults.updateVisualState(api, initialFilterCount);

        // Buttons
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            const companyType = $('#UserPlan').val();
            const status = $('#FilterTransaction').val();

            api.column('companyType:name').search(companyType ? `^${companyType}$` : '', true, false);
            api.column('isActive:name').search(status ? `^${status}$` : '', true, false);
            api.draw();

            let count = [companyType, status].filter(Boolean).length;
            window.DtDefaults.updateVisualState(api, count);

            bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasFilter'))?.hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', () => {
            $('#UserPlan, #FilterTransaction').val('').trigger('change');
            api.state.clear(); // Clear saved state
            api.columns().search('').draw();
            window.DtDefaults.updateVisualState(api, 0);
        });
    };

    // =================== Checkbox Selection Management ===================

    /**
     * Get all selected IDs from checked checkboxes
     */
    const getSelectedIds = () => {
        const ids = [];
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            ids.push(cb.value);
        });
        return ids;
    };

    /**
     * Update Bulk Action Bar visibility and selected count
     */
    const updateBulkBar = () => {
        const ids = getSelectedIds();
        const bar = document.getElementById('bulkActionBar');
        const countEl = document.getElementById('bulkSelectedCount');
        if (!bar || !countEl) return;

        if (ids.length > 0) {
            bar.classList.remove('d-none');
            countEl.textContent = ids.length;
        } else {
            bar.classList.add('d-none');
            countEl.textContent = '0';
        }

        // Sync header checkbox
        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) {
            const totalVisible = dtTableEl.querySelectorAll('tbody .dt-checkboxes').length;
            headerCb.checked = ids.length > 0 && ids.length === totalVisible;
            headerCb.indeterminate = ids.length > 0 && ids.length < totalVisible;
        }
    };

    /**
     * Clear all checkboxes
     */
    const clearSelection = () => {
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            cb.checked = false;
            cb.closest('tr')?.classList.remove('selected');
        });
        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) { headerCb.checked = false; headerCb.indeterminate = false; }
        updateBulkBar();
    };

    // =================== Event Handlers ===================

    /**
     * Handle UI Events
     */
    const handleEvents = () => {
        if (!dtTableEl) return;

        // --- Single row delete ---
        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (deleteBtn) {
                let tr = deleteBtn.closest('tr');
                if (tr.classList.contains('child')) tr = tr.previousElementSibling;
                const row = dt.row(tr);
                const data = row.data();

                window.showConfirm?.('DeleteConfirmation', () => {
                    fetch(`${apiUrl}/api/legal-entities/${data.id}`, { method: 'DELETE', headers: { 'X-Tenant-Id': '00000000-0000-0000-0000-000000000001' } })
                        .then(res => {
                            if (res.ok) {
                                row.remove().draw();
                                window.showToast?.('RecordDeleted', 'success');
                            } else window.showToast?.('ErrorOccurred', 'error');
                        })
                        .catch(() => window.showToast?.('Error deleting record', 'error'));
                }, data.title);
            }
        });

        // --- Row checkbox change ---
        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            const tr = $(this).closest('tr');
            if (this.checked) tr.addClass('selected'); else tr.removeClass('selected');
            updateBulkBar();
        });

        // --- Header "Select All" checkbox ---
        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const isChecked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach(cb => {
                cb.checked = isChecked;
                const tr = cb.closest('tr');
                if (isChecked) tr?.classList.add('selected'); else tr?.classList.remove('selected');
            });
            updateBulkBar();
        });

        // --- Clear selection button ---
        document.getElementById('btnClearSelection')?.addEventListener('click', () => {
            clearSelection();
        });

        // --- Bulk Delete button ---
        document.getElementById('btnBulkDelete')?.addEventListener('click', () => {
            const ids = getSelectedIds();
            if (ids.length === 0) return;

            const confirmMsg = (L.BulkDeleteConfirm || 'Are you sure you want to delete {0} selected records?').replace('{0}', ids.length);

            Swal.fire({
                title: L.AreYouSure || window.L10n?.AreYouSure || 'Are you sure?',
                text: confirmMsg,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: L.BulkDelete || 'Bulk Delete',
                cancelButtonText: L.Cancel || 'Cancel',
                customClass: {
                    confirmButton: 'btn btn-danger me-3',
                    cancelButton: 'btn btn-label-secondary'
                },
                buttonsStyling: false
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch(`${apiUrl}/api/legal-entities/bulk`, {
                        method: 'DELETE',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Tenant-Id': '00000000-0000-0000-0000-000000000001'
                        },
                        body: JSON.stringify({ ids: ids })
                    })
                        .then(res => {
                            if (res.ok) {
                                return res.json();
                            }
                            throw new Error('Bulk delete failed');
                        })
                        .then(data => {
                            window.showToast?.((L.BulkDeleteSuccess || '{0} records deleted successfully').replace('{0}', data.deletedCount), 'success');
                            clearSelection();
                            dt.ajax.reload();
                        })
                        .catch(() => window.showToast?.('ErrorOccurred', 'error'));
                }
            });
        });
    };

    return { init: () => { initDataTable(); handleEvents(); } };
})();

document.addEventListener('DOMContentLoaded', () => LegalEntitiesList.init());
