/**
 * Legal Entities DataTables Initialization
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

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/legal-entities',
                type: 'GET',
                dataSrc: function (json) { return json.data || json; },
                headers: { 'X-Tenant-Id': '00000000-0000-0000-0000-000000000001' }
            },
            columns: [
                { data: 'id' },
                { data: 'id' },
                { data: 'title' },
                { data: 'taxNumber' },
                { data: 'taxOffice' },
                { data: 'email' },
                { data: 'phone' },
                { data: 'companyType' },
                { data: 'isActive' },
                { data: 'action' }
            ],
            columnDefs: [
                {
                    // For Responsive
                    className: 'control',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 2,
                    targets: 0,
                    render: function () {
                        return '';
                    }
                },
                {
                    // For Checkboxes
                    targets: 1,
                    orderable: false,
                    searchable: false,
                    responsivePriority: 3,
                    checkboxes: {
                        selectAllRender: '<input type="checkbox" class="form-check-input">'
                    },
                    render: function () {
                        return '<input type="checkbox" class="dt-checkboxes form-check-input">';
                    }
                },
                {
                    targets: 5, // Email
                    render: function (data) {
                        return data ? '<a href="mailto:' + data + '">' + data + '</a>' : '-';
                    }
                },
                {
                    // Status
                    targets: 8,
                    render: function (data) {
                        var key = String(data);
                        const status = statusObj[key] || { title: L.Unknown || 'Unknown', class: 'bg-label-primary' };
                        return (
                            '<span class="badge ' +
                            status.class +
                            '" text-capitalized>' +
                            status.title +
                            '</span>'
                        );
                    }
                },
                {
                    // Actions
                    targets: -1,
                    title: L.Actions || 'Actions',
                    searchable: false,
                    orderable: false,
                    render: function (data, type, full) {
                        return (
                            '<div class="d-flex align-items-center">' +
                            '<a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>' +
                            '<a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>' +
                            '<div class="dropdown-menu dropdown-menu-end m-0">' +
                            '<a href="/LegalEntities/Details/' + full['id'] + '" class="dropdown-item">' + (L.ViewDetails || 'View') + '</a>' +
                            '<a href="/LegalEntities/Edit/' + full['id'] + '" class="dropdown-item">' + (L.Edit || 'Edit') + '</a>' +
                            '</div>' +
                            '</div>'
                        );
                    }
                }
            ],
            order: [[2, 'asc']],
            buttons: [
                {
                    extend: 'collection',
                    className: 'btn btn-label-secondary dropdown-toggle',
                    text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-export icon-sm"></i> <span class="d-none d-sm-inline-block">' + (L.Export || 'Export') + '</span></span>',
                    buttons: [
                        {
                            extend: 'print',
                            text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>' + (L.Print || 'Print') + '</span>',
                            className: 'dropdown-item'
                        },
                        {
                            extend: 'csv',
                            text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>CSV</span>',
                            className: 'dropdown-item'
                        },
                        {
                            extend: 'excel',
                            text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>',
                            className: 'dropdown-item'
                        }
                    ]
                },
                {
                    text: '<i class="icon-base bx bx-import icon-sm"></i>',
                    className: 'btn btn-icon btn-label-secondary',
                    attr: {
                        title: L.Import || 'Import',
                        'data-bs-toggle': 'tooltip'
                    },
                    action: function () {
                        if (window.showToast) window.showToast(L.ComingSoon || 'Coming soon', 'info');
                    }
                },
                {
                    text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                    className: 'btn btn-icon btn-label-secondary',
                    attr: {
                        title: L.Filter || 'Filter',
                        'data-bs-toggle': 'offcanvas',
                        'data-bs-target': '#offcanvasFilter'
                    }
                },
                {
                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">' + (L.AddNewCompany || 'Add New Company') + '</span>',
                    className: 'add-new btn btn-primary',
                    attr: {
                        onclick: "window.location.href='/LegalEntities/Create'"
                    }
                }
            ],
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: function (row) {
                            const data = row.data();
                            return 'Details of ' + data.title;
                        }
                    }),
                    type: 'column',
                    renderer: function (api, rowIdx, columns) {
                        const data = columns
                            .map(function (col) {
                                return col.title !== ''
                                    ? `<tr data-dt-row="${col.rowIndex}" data-dt-column="${col.columnIndex}">
                      <td>${col.title}:</td>
                      <td>${col.data}</td>
                    </tr>`
                                    : '';
                            })
                            .join('');

                        if (data) {
                            const div = document.createElement('div');
                            div.classList.add('table-responsive');
                            const table = document.createElement('table');
                            div.appendChild(table);
                            table.classList.add('table');
                            const tbody = document.createElement('tbody');
                            tbody.innerHTML = data;
                            table.appendChild(tbody);
                            return div;
                        }
                        return false;
                    }
                }
            },
            initComplete: function () {
                const api = this.api();
                setupFilters(api);
            }
        }));
    };

    /**
     * Setup Filters (Internal)
     */
    const setupFilters = (api) => {
        // Helper function to create a select dropdown and append options
        const createSelectFilter = (columnIndex, containerClass, selectId, defaultOptionText) => {
            const column = api.column(columnIndex);
            const container = document.querySelector(containerClass);
            if (!container) return;

            const select = document.createElement('select');
            select.id = selectId;
            select.className = 'form-select text-capitalize';
            select.innerHTML = '<option value="">' + defaultOptionText + '</option>';
            container.appendChild(select);

            // Populate options based on unique column data
            const uniqueData = Array.from(new Set(column.data().toArray())).sort();
            uniqueData.forEach(d => {
                if (!d) return; // Skip empty
                const option = document.createElement('option');
                option.value = d;
                option.textContent = d;
                option.className = 'text-capitalize';
                select.appendChild(option);
            });
        };

        // 1. Type filter
        createSelectFilter(7, '.user_plan', 'UserPlan', L.CompanyType || 'Select Type');

        // 2. Status filter
        const statusFilter = document.createElement('select');
        statusFilter.id = 'FilterTransaction';
        statusFilter.className = 'form-select text-capitalize';
        statusFilter.innerHTML = '<option value="">' + (L.SelectStatus || 'Select Status') + '</option>';
        const statusContainer = document.querySelector('.user_status');

        if (statusContainer) {
            statusContainer.appendChild(statusFilter);
            const statusColumn = api.column(8);
            const uniqueStatusData = Array.from(new Set(statusColumn.data().toArray())).sort();
            uniqueStatusData.forEach(d => {
                const option = document.createElement('option');
                var key = String(d);
                const statusObjRef = statusObj[key] || { title: L.Unknown || 'Unknown' };
                option.value = statusObjRef.title;
                option.textContent = statusObjRef.title;
                option.className = 'text-capitalize';
                statusFilter.appendChild(option);
            });
        }

        // 3. Apply button logic
        const btnApply = document.getElementById('btnFilterApply');
        if (btnApply) {
            btnApply.addEventListener('click', () => {
                const typeVal = document.getElementById('UserPlan')?.value;
                const statusVal = document.getElementById('FilterTransaction')?.value;

                const typeSearch = typeVal ? '^' + typeVal + '$' : '';
                const statusSearch = statusVal ? '^' + statusVal + '$' : '';

                api.column(7).search(typeSearch, true, false);
                api.column(8).search(statusSearch, true, false);

                api.draw();

                // Close offcanvas after applying
                const offcanvas = document.getElementById('offcanvasFilter');
                const bootstrapOffcanvas = bootstrap.Offcanvas.getInstance(offcanvas);
                if (bootstrapOffcanvas) {
                    bootstrapOffcanvas.hide();
                }
            });
        }

        // 4. Reset logic
        const btnReset = document.getElementById('btnFilterReset');
        if (btnReset) {
            btnReset.addEventListener('click', () => {
                const typeSelect = document.getElementById('UserPlan');
                const statusSelect = document.getElementById('FilterTransaction');
                if (typeSelect) typeSelect.value = '';
                if (statusSelect) statusSelect.value = '';
                api.columns().search('').draw();
            });
        }
    };

    /**
     * Handle UI Events
     */
    const handleEvents = () => {
        if (!dtTableEl) return;

        dtTableEl.addEventListener('click', function (e) {
            if (e.target.closest('.delete-record')) {
                let tr = e.target.closest('tr');
                if (tr.classList.contains('child')) {
                    tr = tr.previousElementSibling;
                }
                const row = dt.row(tr);
                const data = row.data();

                if (window.showConfirm) {
                    window.showConfirm('DeleteConfirmation', function () {
                        fetch(apiUrl + '/api/legal-entities/' + data.id, {
                            method: 'DELETE',
                            headers: { 'X-Tenant-Id': '00000000-0000-0000-0000-000000000001' }
                        })
                            .then(res => {
                                if (res.ok) {
                                    row.remove().draw();
                                    if (window.showToast) window.showToast('RecordDeleted', 'success');
                                } else {
                                    if (window.showToast) window.showToast('ErrorOccurred', 'error');
                                }
                            })
                            .catch(err => {
                                console.error('Delete error:', err);
                                if (window.showToast) window.showToast('Error deleting record', 'error');
                            });
                    }, data.title);
                }
            }
        });

        // Search input styling fix
        const searchFilterInput = document.querySelector('.dt-search input');
        if (searchFilterInput) {
            searchFilterInput.classList.add('form-control');
        }
    };

    // Public API
    return {
        init: function () {
            initDataTable();
            handleEvents();
        }
    };
})();

// DOM Ready initialization
document.addEventListener('DOMContentLoaded', function () {
    LegalEntitiesList.init();
});
