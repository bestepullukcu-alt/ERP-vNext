/**
 * MOD-0013: Merkezi DataTable Konfigürasyonu (Sneat 2.x Layout API)
 * Referans: _Reference/Theme/full-version/assets/js/app-user-list.js
 */
'use strict';

window.DtDefaults = (function () {
    var L = function () { return window.L10n || {}; };

    /**
     * Ortak Responsive Renderer (Modal içi tablo oluşturucu).
     */
    function responsiveRenderer(api, rowIdx, columns) {
        var data = $.map(columns, function (col, i) {
            return col.title !== '' // titlesız kolonları (checkbox vb) sakla
                ? '<tr data-dt-row="' +
                col.rowIndex +
                '" data-dt-column="' +
                col.columnIndex +
                '">' +
                '<td>' +
                col.title +
                ':' +
                '</td> ' +
                '<td>' +
                col.data +
                '</td>' +
                '</tr>'
                : '';
        }).join('');

        return data ? $('<table class="table"/><tbody/>').append(data) : false;
    }

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
        order: [[2, 'desc']],
        language: {
            sLengthMenu: '_MENU_',
            search: '',
            searchPlaceholder: '',
            paginate: {
                next: '<i class="icon-base bx bx-chevron-right icon-18px"></i>',
                previous: '<i class="icon-base bx bx-chevron-left icon-18px"></i>'
            },
            processing: '<div class="sk-fold sk-primary mx-auto"><div class="sk-fold-cube"></div><div class="sk-fold-cube"></div><div class="sk-fold-cube"></div><div class="sk-fold-cube"></div></div>'
        },
        responsive: {
            details: {
                display: DataTable.Responsive.display.modal({
                    header: function (row) {
                        var data = row.data();
                        return 'Details of ' + (data.title || data.name || '');
                    }
                }),
                type: 'column',
                renderer: responsiveRenderer
            }
        }
    };

    /**
     * Sneat class düzeltmeleri — drawCallback ile daha stabil.
     */
    function applySneatClassFixes() {
        $('.dt-buttons .btn').removeClass('btn-secondary');
        $('.dt-search .form-control').removeClass('form-control-sm');
        $('.dt-length .form-select').removeClass('form-select-sm').addClass('ms-0');
        $('.dt-length').addClass('mb-md-6 mb-0');
        $('.dt-search').addClass('mb-md-6 mb-2');
        $('.dt-layout-end').removeClass('justify-content-between').addClass('d-flex gap-md-4 justify-content-md-between justify-content-center gap-4 flex-wrap mt-0');
        $('.dt-layout-start').addClass('mt-0');
        $('.dt-buttons').addClass('d-flex gap-4 mb-md-0 mb-6');
        $('.dt-layout-table').removeClass('row mt-2');
        $('.dt-layout-full').removeClass('col-md col-12').addClass('table-responsive');
    }

    /**
     * Merge user config with base defaults.
     */
    function create(userConfig) {
        var merged = $.extend(true, {}, baseConfig, userConfig);
        var l = L();
        merged.language.searchPlaceholder = merged.language.searchPlaceholder || l.Search || 'Search...';

        if (!merged.layout) {
            merged.layout = buildLayout(merged.buttons);
            delete merged.buttons;
        }

        // Auto-hide skeleton + apply class fixes
        var originalInitComplete = merged.initComplete;
        merged.initComplete = function (settings, json) {
            $('#skeleton-loader').fadeOut(300);
            applySneatClassFixes();
            if (typeof originalInitComplete === 'function') {
                originalInitComplete.call(this, settings, json);
            }
        };

        // Redraw durumunda class fixleri tazele
        var originalDrawCallback = merged.drawCallback;
        merged.drawCallback = function (settings) {
            applySneatClassFixes();
            if (typeof originalDrawCallback === 'function') {
                originalDrawCallback.call(this, settings);
            }
        };

        return merged;
    }

    /**
     * Ortak export ayarları (HTML temizleme ve kolon seçimi).
     */
    var commonExportOptions = {
        columns: [2, 3, 4, 5, 6, 7, 8],
        format: {
            body: function (inner) {
                if (!inner || inner.length <= 0) return inner;
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
     * Standard export buttons + optional extras.
     */
    function exportButtons(addNewText, addNewAttr, extraButtons) {
        var l = L();
        var btns = [
            {
                extend: 'collection',
                className: 'btn btn-label-secondary dropdown-toggle',
                text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-export icon-sm"></i> <span class="d-none d-sm-inline-block">' + (l.Export || 'Export') + '</span></span>',
                buttons: [
                    { extend: 'print', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>' + (l.Print || 'Print') + '</span>', className: 'dropdown-item', exportOptions: commonExportOptions },
                    { extend: 'csv', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>CSV</span>', className: 'dropdown-item', exportOptions: commonExportOptions },
                    { extend: 'excel', text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>', className: 'dropdown-item', exportOptions: commonExportOptions },
                    { extend: 'pdf', text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-2"></i>' + (l.PDF || 'PDF') + '</span>', className: 'dropdown-item', exportOptions: commonExportOptions },
                    { extend: 'copy', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-copy me-2"></i>' + (l.Copy || 'Copy') + '</span>', className: 'dropdown-item', exportOptions: commonExportOptions }
                ]
            }
        ];

        if (extraButtons && Array.isArray(extraButtons)) {
            btns = btns.concat(extraButtons);
        }

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
        exportButtons: exportButtons,
        responsiveRenderer: responsiveRenderer
    };
})();
