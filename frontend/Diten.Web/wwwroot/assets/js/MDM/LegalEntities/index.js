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
        const extraButtons = [
            {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import || 'Import', 'data-bs-toggle': 'tooltip' },
                action: function () {
                    if (window.showToast) window.showToast(L.ComingSoon || 'Coming soon', 'info');
                }
            },
            {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Filter || 'Filter', 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#offcanvasFilter' }
            }
        ];

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
                    checkboxes: { selectAllRender: '<input type="checkbox" class="form-check-input">' },
                    render: () => '<input type="checkbox" class="dt-checkboxes form-check-input">'
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
            }
        }));
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

        // Buttons
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            const companyType = $('#UserPlan').val();
            const status = $('#FilterTransaction').val();

            api.column('companyType:name').search(companyType ? `^${companyType}$` : '', true, false);
            api.column('isActive:name').search(status ? `^${status}$` : '', true, false);
            api.draw();

            bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasFilter'))?.hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', () => {
            $('#UserPlan, #FilterTransaction').val('').trigger('change');
            api.columns().search('').draw();
        });
    };

    /**
     * Handle UI Events
     */
    const handleEvents = () => {
        if (!dtTableEl) return;
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
    };

    return { init: () => { initDataTable(); handleEvents(); } };
})();

document.addEventListener('DOMContentLoaded', () => LegalEntitiesList.init());
