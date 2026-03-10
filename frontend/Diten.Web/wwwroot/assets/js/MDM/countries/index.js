/**
 * Countries Management Module
 * Diten ERP vNext - MDM Module
 */
'use strict';

const Countries = (function () {
    // Private Variables
    let dt_countries;
    const dt_table_el = $('.datatables-countries');
    const offcanvasDetailsEl = document.getElementById('offcanvasDetailsPreview');
    const offcanvasDetails = new bootstrap.Offcanvas(offcanvasDetailsEl);
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (e) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        const headers = {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
        // console.log('[Countries] Headers:', headers);
        return headers;
    };

    // Initializer
    const init = function () {
        if (dt_table_el.length) {
            initDataTable();
            initEvents();
        }
    };

    // DataTable Configuration
    const initDataTable = function () {
        dt_countries = dt_table_el.DataTable({
            processing: true,
            serverSide: false,
            ajax: {
                url: apiUrl + '/api/countries',
                type: 'GET',
                headers: getAuthHeaders(),
                dataSrc: function (json) {
                    if (json && json.data) return json.data;
                    if (Array.isArray(json)) return json;
                    console.error('[Countries] Unexpected API response:', json);
                    return [];
                }
            },
            columns: [
                { data: null, defaultContent: '' },
                { data: 'id' },
                { data: 'flagEmoji', defaultContent: '' },
                { data: 'name' },
                { data: 'iso2Code' },
                { data: 'phoneCode' },
                { data: 'capital' },
                { data: 'region' },
                { data: 'isActive' },
                { data: null, defaultContent: '' }
            ],
            columnDefs: [
                {
                    // Control column for responsive
                    targets: 0,
                    className: 'control',
                    orderable: false,
                    render: function () {
                        return '';
                    }
                },
                {
                    // Checkboxes
                    targets: 1,
                    orderable: false,
                    render: function (data, type, full) {
                        return '<input type="checkbox" class="dt-checkboxes form-check-input" value="' + data + '">';
                    },
                    checkboxes: {
                        selectAllRender: '<input type="checkbox" class="form-check-input">'
                    }
                },
                {
                    // Flag Emoji
                    targets: 2,
                    orderable: false,
                    className: 'text-center',
                    render: function (data, type, full) {
                        return '<span class="fs-3">' + (data || '🏳️') + '</span>';
                    }
                },
                {
                    // Country Name with Native Name
                    targets: 3,
                    render: function (data, type, full) {
                        return '<strong>' + data + '</strong><br><small class="text-muted">' + (full.nativeName || '') + '</small>';
                    }
                },
                {
                    // ISO2 Code with badge
                    targets: 4,
                    className: 'text-center',
                    render: function (data, type, full) {
                        return '<span class="badge bg-label-info">' + (data || '-') + '</span>';
                    }
                },
                {
                    // Phone Code
                    targets: 5,
                    className: 'text-center',
                    render: function (data, type, full) {
                        return data ? '+' + data : '-';
                    }
                },
                {
                    // Status Badge
                    targets: -2,
                    render: function (data, type, full) {
                        const status = data ? 'success' : 'secondary';
                        const text = data ? window.L10n.Active : window.L10n.Passive;
                        return '<span class="badge bg-label-' + status + '">' + text + '</span>';
                    }
                },
                {
                    // Actions
                    targets: -1,
                    title: window.L10n.Actions,
                    orderable: false,
                    render: function (data, type, full) {
                        return (
                            '<div class="d-inline-block text-nowrap">' +
                            '<button class="btn btn-sm btn-icon btn-view" data-json=\'' + JSON.stringify(full).replace(/'/g, "&#39;") + '\'><i class="bx bx-show"></i></button>' +
                            '<a href="/MDM/Countries/Edit/' + full.id + '" class="btn btn-sm btn-icon"><i class="bx bx-edit"></i></a>' +
                            '<button class="btn btn-sm btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded"></i></button>' +
                            '<div class="dropdown-menu dropdown-menu-end m-0">' +
                            '<a href="javascript:;" class="dropdown-item btn-delete" data-id="' + full.id + '">' + window.L10n.Delete + '</a>' +
                            '</div>' +
                            '</div>'
                        );
                    }
                }
            ],
            layout: {
                topStart: {
                    rowClass: 'row mx-1',
                    features: [
                        {
                            buttons: [
                                {
                                    extend: 'collection',
                                    className: 'btn btn-label-secondary dropdown-toggle mx-3',
                                    text: '<i class="bx bx-export me-1"></i>' + window.L10n.Export,
                                    buttons: ['print', 'csv', 'excel', 'pdf']
                                }
                            ]
                        }
                    ]
                },
                topEnd: {
                    features: [
                        {
                            search: {
                                placeholder: window.L10n.Search + '...'
                            }
                        },
                        {
                            buttons: [
                                {
                                    text: '<i class="bx bx-plus me-1"></i>' + window.L10n.AddNewCountries,
                                    className: 'add-new btn btn-primary',
                                    action: function () {
                                        window.location.href = '/MDM/Countries/Create';
                                    }
                                }
                            ]
                        }
                    ]
                }
            },
            language: {
                sLengthMenu: '_MENU_',
                search: '',
                searchPlaceholder: window.L10n.Search,
                info: window.L10n.DtInfo,
                infoEmpty: window.L10n.DtInfoEmpty,
                emptyTable: window.L10n.DtNoRecords,
                paginate: {
                    next: '<i class="bx bx-chevron-right"></i>',
                    previous: '<i class="bx bx-chevron-left"></i>'
                }
            },
            responsive: {
                details: {
                    type: 'column',
                    target: 0
                }
            },
            order: [[3, 'asc']]
        });
    };

    // Event Listeners
    const initEvents = function () {
        // Quick View (Offcanvas)
        dt_table_el.on('click', '.btn-view', function () {
            populateOffcanvas(this);
            offcanvasDetails.show();
        });

        // Delete Confirmation
        dt_table_el.on('click', '.btn-delete', function () {
            const id = $(this).data('id');
            const row = $(this).closest('tr');

            Swal.fire({
                title: window.L10n.AreYouSure,
                text: window.L10n.BulkDeleteConfirm,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: window.L10n.Delete,
                cancelButtonText: window.L10n.Cancel,
                customClass: {
                    confirmButton: 'btn btn-danger me-3',
                    cancelButton: 'btn btn-label-secondary'
                },
                buttonsStyling: false
            }).then(function (result) {
                if (result.value) {
                    deleteCountry(id, row);
                }
            });
        });

        // Bulk Delete
        $('#btnBulkDelete').on('click', function () {
            const selectedIds = [];
            $('.dt-checkboxes:checked').each(function () {
                selectedIds.push($(this).val());
            });

            if (selectedIds.length === 0) return;

            Swal.fire({
                title: window.L10n.AreYouSure,
                text: window.L10n.BulkDeleteConfirm,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: window.L10n.Delete,
                cancelButtonText: window.L10n.Cancel,
                customClass: {
                    confirmButton: 'btn btn-danger me-3',
                    cancelButton: 'btn btn-label-secondary'
                },
                buttonsStyling: false
            }).then(function (result) {
                if (result.value) {
                    bulkDeleteCountries(selectedIds);
                }
            });
        });

        // Clear Selection
        $('#btnClearSelection').on('click', function () {
            $('.dt-checkboxes').prop('checked', false);
            $('#bulkActionBar').addClass('d-none');
        });
    };

    // Delete Country
    const deleteCountry = function (id, row) {
        $.ajax({
            url: apiUrl + '/api/countries/' + id,
            type: 'DELETE',
            headers: getAuthHeaders(),
            success: function (response) {
                dt_countries.row(row).remove().draw();
                toastr.success(window.L10n.DeleteSuccess);
            },
            error: function (xhr) {
                toastr.error(window.L10n.DeleteError);
            }
        });
    };

    // Bulk Delete Countries
    const bulkDeleteCountries = function (ids) {
        $.ajax({
            url: apiUrl + '/api/countries/bulk-delete',
            type: 'POST',
            data: JSON.stringify({ ids: ids }),
            contentType: 'application/json',
            headers: getAuthHeaders(),
            success: function (response) {
                dt_countries.draw();
                $('#bulkActionBar').addClass('d-none');
                toastr.success(window.L10n.DeleteSuccess);
            },
            error: function (xhr) {
                toastr.error(window.L10n.DeleteError);
            }
        });
    };

    return {
        init: init
    };
})();

// Document Ready
$(function () {
    Countries.init();
});