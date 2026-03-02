/**
 * MOD-0013: Merkezi DataTable Konfigürasyonu (Sneat 2.x Layout API)
 * Referans: _Reference/Theme/full-version/assets/js/app-user-list.js
 *
 * Kullanım:
 *   var dt = $('#myTable').DataTable(window.DtDefaults.create({ columns: [...], ajax: {...} }));
 */
'use strict';

window.DtDefaults = (function () {
    var L = function () { return window.L10n || {}; };

    /**
     * Sneat 2.x Layout API — orijinal 'app-user-list.js' ile %100 uyumlu.
     */
    function buildLayout(userButtons) {
        var l = L();
        return {
            topStart: {
                rowClass: 'row mx-3 my-0 justify-content-between',
                features: [
                    {
                        pageLength: {
                            menu: [10, 25, 50, 100],
                            text: '_MENU_'
                        }
                    }
                ]
            },
            topEnd: {
                features: [
                    {
                        search: {
                            placeholder: l.Search || 'Search...',
                            text: '_INPUT_'
                        }
                    },
                    {
                        buttons: userButtons || exportButtons()
                    }
                ]
            },
            bottomStart: {
                rowClass: 'row mx-3 justify-content-between',
                features: ['info']
            },
            bottomEnd: {
                features: [
                    {
                        paging: {
                            firstLast: false
                        }
                    }
                ]
            }
        };
    }

    var baseConfig = {
        serverSide: false,
        processing: true,
        order: [[0, 'desc']],
        language: {
            sLengthMenu: '_MENU_',
            search: '',
            searchPlaceholder: '',
            paginate: {
                next: '<i class="icon-base bx bx-chevron-right icon-18px"></i>',
                previous: '<i class="icon-base bx bx-chevron-left icon-18px"></i>'
            },
            processing: '<div class="spinner-border spinner-border-sm text-primary" role="status"><span class="visually-hidden">Loading...</span></div>'
        }
    };

    /**
     * Sneat class düzeltmeleri — orijinal 'app-user-list.js' ile %100 uyumlu.
     */
    function applySneatClassFixes() {
        // İhracat butonlarındaki varsayılan btn-secondary sınıfını kaldır
        $('.dt-buttons > .btn-group > button').removeClass('btn-secondary');

        setTimeout(function () {

            const elementsToModify = [
                { selector: '.dt-buttons .btn', classToRemove: 'btn-secondary' },
                { selector: '.dt-search .form-control', classToRemove: 'form-control-sm' },
                { selector: '.dt-length .form-select', classToRemove: 'form-select-sm', classToAdd: 'ms-0' },
                { selector: '.dt-length', classToAdd: 'mb-md-6 mb-0' },
                { selector: '.dt-search', classToAdd: 'mb-md-6 mb-2' },
                {
                    selector: '.dt-layout-end',
                    classToRemove: 'justify-content-between',
                    classToAdd: 'd-flex gap-md-4 justify-content-md-between justify-content-center gap-4 flex-wrap mt-0'
                },
                { selector: '.dt-layout-start', classToAdd: 'mt-0' },
                { selector: '.dt-buttons', classToAdd: 'd-flex gap-4 mb-md-0 mb-6' },
                { selector: '.dt-layout-table', classToRemove: 'row mt-2' },
                { selector: '.dt-layout-full', classToRemove: 'col-md col-12', classToAdd: 'table-responsive' }
            ];

            elementsToModify.forEach(function ({ selector, classToRemove, classToAdd }) {
                document.querySelectorAll(selector).forEach(function (element) {
                    if (classToRemove) {
                        classToRemove.split(' ').forEach(function (className) { element.classList.remove(className); });
                    }
                    if (classToAdd) {
                        classToAdd.split(' ').forEach(function (className) { element.classList.add(className); });
                    }
                });
            });
        }, 100);
    }

    /**
     * Merge user config with base defaults + inject layout + skeleton hide.
     * @param {Object} userConfig - Page-specific overrides (columns, ajax, etc.)
     * @returns {Object} Merged DataTable config
     */
    function create(userConfig) {
        var merged = $.extend(true, {}, baseConfig, userConfig);

        // Dynamic L10n
        var l = L();
        merged.language.searchPlaceholder = merged.language.searchPlaceholder || l.Search || 'Search...';

        // Inject Sneat 2.x Layout (unless user provides custom layout)
        if (!merged.layout) {
            merged.layout = buildLayout(merged.buttons);
            delete merged.buttons; // layout handles buttons, no need for top-level
        }

        // Remove legacy dom string if accidentally passed
        delete merged.dom;

        // Auto-hide skeleton loader + apply Sneat class fixes
        var originalInitComplete = merged.initComplete;
        merged.initComplete = function (settings, json) {
            $('#skeleton-loader').fadeOut(300);
            applySneatClassFixes();
            if (typeof originalInitComplete === 'function') {
                originalInitComplete.call(this, settings, json);
            }
        };

        return merged;
    }

    /**
     * Ortak export ayarları (HTML temizleme ve kolon seçimi).
     */
    var commonExportOptions = {
        columns: [2, 3, 4, 5, 6, 7, 8], // Standart veri kolonları (Control, Checkbox ve Action hariç)
        format: {
            body: function (inner, coldex, rowdex) {
                if (inner.length <= 0) return inner;
                var el = $.parseHTML(inner);
                var result = '';
                $.each(el, function (index, item) {
                    if (item.classList !== undefined && item.classList.contains('user-name')) {
                        result = result + item.lastChild.firstChild.textContent;
                    } else if (item.innerText === undefined) {
                        result = result + item.textContent;
                    } else result = result + item.innerText;
                });
                return result;
            }
        }
    };

    /**
     * Standard export buttons (Sneat style — icon + text, dropdown).
     * @param {String} addNewText - "Add New" button text (optional)
     * @param {Object} addNewAttr - attributes for "Add New" button (optional)
     * @returns {Array} DataTable buttons config
     */
    function exportButtons(addNewText, addNewAttr) {
        var l = L();
        var btns = [
            {
                extend: 'collection',
                className: 'btn btn-label-secondary dropdown-toggle',
                text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-export icon-sm"></i> <span class="d-none d-sm-inline-block">' + (l.Export || 'Export') + '</span></span>',
                buttons: [
                    {
                        extend: 'print',
                        text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>' + (l.Print || 'Print') + '</span>',
                        className: 'dropdown-item',
                        exportOptions: commonExportOptions
                    },
                    {
                        extend: 'csv',
                        text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>CSV</span>',
                        className: 'dropdown-item',
                        exportOptions: commonExportOptions
                    },
                    {
                        extend: 'excel',
                        text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>',
                        className: 'dropdown-item',
                        exportOptions: commonExportOptions
                    },
                    {
                        extend: 'pdf',
                        text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-2"></i>' + (l.PDF || 'PDF') + '</span>',
                        className: 'dropdown-item',
                        exportOptions: commonExportOptions
                    },
                    {
                        extend: 'copy',
                        text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-copy me-2"></i>' + (l.Copy || 'Copy') + '</span>',
                        className: 'dropdown-item',
                        exportOptions: commonExportOptions
                    }
                ]
            }
        ];

        // Optional "Add New" button
        if (addNewText) {
            btns.push({
                text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">' + addNewText + '</span>',
                className: 'add-new btn btn-primary',
                attr: addNewAttr || {}
            });
        }

        return btns;
    }

    return {
        create: create,
        exportButtons: exportButtons
    };
})();
