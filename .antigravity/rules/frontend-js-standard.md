# GOLDEN RULE: JavaScript Module Pattern & DataTable v2 Standard

Diten ERP vNext projelerinde her modülün `index.js` dosyası aşağıdaki "Module Pattern" (IIFE) yapısında olmalı ve Global Scope'u asla kirletmemelidir.

## 🏗️ JS Mimari Kuralları
1. **Encapsulation:** Tüm değişkenler ve fonksiyonlar `const {{ModuleName}} = (function () { ... })();` bloğu içinde olmalıdır.
2. **DataTable v2 Layout:** Eski DOM (lfrtip) kullanımı yasaktır. Yeni `layout` API'si kullanılmalıdır.
3. **AJAX Gateway:** Tüm istekler `window.ApiBaseUrl` üzerinden ve `Helpers.ajaxCall` (veya merkezi wrapper) ile yapılmalıdır.
4. **L10n Bridge:** Metinler asla JS içinde hardcoded yazılmaz; `window.L10n` objesinden okunur.

## 📄 JavaScript Master Template

```javascript
/**
 * {{ModuleName}} Management Module
 */
'use strict';

const {{ModuleName}} = (function () {
    // Private Variables
    let dt_user;
    const dt_table_el = $('.datatables-{{ModuleNameLower}}');
    const offcanvasDetailsEl = document.getElementById('offcanvasDetailsPreview');
    const offcanvasDetails = new bootstrap.Offcanvas(offcanvasDetailsEl);

    // Initializer
    const init = function () {
        if (dt_table_el.length) {
            initDataTable();
            initEvents();
        }
    };

    // DataTable Configuration
    const initDataTable = function () {
        dt_user = dt_table_el.DataTable({
            processing: true,
            serverSide: true,
            ajax: {
                url: window.ApiBaseUrl + '/{{AreaName}}/api/v1/{{ModuleNameLower}}',
                type: 'GET',
                data: function (d) {
                    // Filter parameters
                    return d;
                }
            },
            columns: [
                { data: '' },
                { data: 'id' },
                {{JSColumns}},
                { data: 'isActive' },
                { data: '' }
            ],
            columnDefs: [
                {
                    // For Checkboxes
                    targets: 1,
                    orderable: false,
                    render: function () {
                        return '<input type="checkbox" class="dt-checkboxes form-check-input">';
                    },
                    checkboxes: {
                        selectAllRender: '<input type="checkbox" class="form-check-input">'
                    }
                },
                {
                    // Status Badge
                    targets: -2,
                    render: function (data, type, full) {
                        const status = data ? 'success' : 'secondary';
                        const text = data ? window.L10n.Active : window.L10n.Passive;
                        return `<span class="badge bg-label-${status}">${text}</span>`;
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
                            `<button class="btn btn-sm btn-icon btn-view" data-json='${JSON.stringify(full)}'><i class="bx bx-show"></i></button>` +
                            `<a href="/{{AreaName}}/{{ModuleName}}/Edit/${full.id}" class="btn btn-sm btn-icon"><i class="bx bx-edit"></i></a>` +
                            '<button class="btn btn-sm btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded"></i></button>' +
                            '<div class="dropdown-menu dropdown-menu-end m-0">' +
                            `<a href="javascript:;" class="dropdown-item btn-delete" data-id="${full.id}">${window.L10n.Delete}</a>` +
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
                                    text: '<i class="bx bx-plus me-1"></i>' + window.L10n.AddNew{{ModuleName}},
                                    className: 'add-new btn btn-primary',
                                    action: function () {
                                        window.location.href = '/{{AreaName}}/{{ModuleName}}/Create';
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
            }
        });
    };

    // Event Listeners
    const initEvents = function () {
        // Quick View (Offcanvas)
        dt_table_el.on('click', '.btn-view', function () {
            populateOffcanvas(this); // Defined in HTML template
            offcanvasDetails.show();
        });

        // Delete Confirmation
        dt_table_el.on('click', '.btn-delete', function () {
            const id = $(this).data('id');
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
                    // AJAX Call to Delete
                }
            });
        });
    };

    return {
        init: init
    };
})();

// Document Ready
$(function () {
    {{ModuleName}}.init();
});