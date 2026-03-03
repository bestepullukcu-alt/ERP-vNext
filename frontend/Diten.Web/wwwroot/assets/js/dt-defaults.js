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
    function buildLayout() {
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
        stateSave: true,
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
        $('.dt-buttons').addClass('mb-md-0 mb-6'); // Removed d-flex gap-4 to keep btn-groups intact
        $('.dt-layout-table').removeClass('row mt-2');
        $('.dt-layout-full').removeClass('col-md col-12'); // table-responsive class'ı sayfa içindeki div'de mevcut, mükerrerliği önlemek için buradan kaldırıldı.
        $('table.dataTable').addClass('table-hover');
    }

    /**
     * Merge user config with base defaults.
     */
    function create(userConfig) {
        var merged = $.extend(true, {}, baseConfig, userConfig);
        var l = L();
        merged.language.searchPlaceholder = merged.language.searchPlaceholder || l.Search || 'Search...';

        // DataTables i18n mapping
        if (l.DtNoRecords) merged.language.zeroRecords = l.DtNoRecords;
        if (l.DtInfo) merged.language.info = l.DtInfo;
        if (l.DtInfoEmpty) merged.language.infoEmpty = l.DtInfoEmpty;
        if (l.DtInfoFiltered) merged.language.infoFiltered = l.DtInfoFiltered;
        if (l.DtZeroRecords) merged.language.zeroRecords = l.DtZeroRecords;
        if (l.DtEmptyTable) merged.language.emptyTable = l.DtEmptyTable;

        if (!merged.layout) {
            merged.layout = buildLayout(); // Don't pass buttons yet

            var btns = merged.buttons || exportButtons();

            if (Array.isArray(btns) && btns.length > 0 && btns[0].buttons !== undefined) {
                // If it is an array of layout features like [{buttons: [...]}, ...]
                merged.layout.topEnd.features = merged.layout.topEnd.features.concat(btns);
            } else if (btns) {
                // Standard single button array
                merged.layout.topEnd.features.push({ buttons: btns });
            }

            delete merged.buttons;
        }

        // AJAX isteği başladığında skeleton'ı göster (Eğer varsa)
        var originalPreXhr = merged.preXhr;
        merged.preXhr = function (settings, data) {
            $('#skeleton-loader').fadeIn(100);
            if (typeof originalPreXhr === 'function') {
                originalPreXhr.call(this, settings, data);
            }
        };

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

        var exportBtn = {
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
        };

        var colvisBtn = {
            extend: 'colvis',
            text: '<i class="icon-base bx bx-show icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-colvis-btn position-relative',
            attr: { title: 'Column Visibility', 'data-bs-toggle': 'tooltip' },
            columns: [2, 3, 4, 5, 6, 7, 8] // Exclude Index 0 (Control), 1 (Checkbox), 9 (Actions)
        };

        var group1 = [exportBtn];
        var group2 = [colvisBtn];
        var group3 = [];

        if (extraButtons) {
            if (extraButtons.importBtn) group1.push(extraButtons.importBtn);
            if (extraButtons.filterBtn) group2.push(extraButtons.filterBtn);
            if (Array.isArray(extraButtons)) group3 = group3.concat(extraButtons);
        }

        var addNewBtnGroup = [];
        if (addNewText) {
            addNewBtnGroup.push({
                text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">' + addNewText + '</span>',
                className: 'add-new btn btn-primary',
                attr: addNewAttr || {}
            });
        }

        var features = [
            { buttons: group1 },
            { buttons: group2 }
        ];

        if (group3.length > 0) features.push({ buttons: group3 });
        if (addNewBtnGroup.length > 0) features.push({ buttons: addNewBtnGroup });

        return features;
    }

    /**
     * UI: Update Visual States (Filter, ColVis, Search)
     * Verilen DataTable API'sine göre butonları ve kutuları görsel olarak günceller.
     */
    function updateVisualState(api, filterCount) {
        // 1. Filter Button Sync
        var $filterBtn = $('.dt-filter-btn');
        if ($filterBtn.length && filterCount !== undefined) {
            $filterBtn.find('.badge').remove();
            if (filterCount > 0) {
                $filterBtn.removeClass('btn-label-secondary').addClass('btn-label-primary');
                $filterBtn.append('<span class="badge badge-center rounded-pill bg-primary position-absolute top-0 start-100 translate-middle">' + filterCount + '</span>');
            } else {
                $filterBtn.removeClass('btn-label-primary').addClass('btn-label-secondary');
            }
        }

        // 2. ColVis Button Sync (Gizlenen kolon varsa işaretle)
        var $colvisBtn = $('.dt-colvis-btn');
        if ($colvisBtn.length) {
            // Varsayılan gizli kolonlar dışındakileri saymak daha doğru olur ama basitçe herhangi bir gizli kolon varsa gösterelim
            var hiddenCount = api.columns().flatten().filter(function (idx) {
                return !api.column(idx).visible();
            }).length;

            $colvisBtn.find('.badge').remove();
            if (hiddenCount > 0) {
                $colvisBtn.addClass('btn-label-primary').removeClass('btn-label-secondary');
                $colvisBtn.append('<span class="badge badge-dot bg-primary position-absolute top-0 start-100 translate-middle"></span>');
            } else {
                $colvisBtn.removeClass('btn-label-primary').addClass('btn-label-secondary');
            }
        }

        // 3. Search Box Sync
        var $searchWrapper = $('.dt-search');
        var $searchInput = $searchWrapper.find('input');
        if ($searchInput.length) {
            if (api.search()) {
                $searchInput.addClass('border-primary bg-label-primary');
            } else {
                $searchInput.removeClass('border-primary bg-label-primary');
            }
        }
    }

    return {
        create: create,
        exportButtons: exportButtons,
        responsiveRenderer: responsiveRenderer,
        updateVisualState: updateVisualState
    };
})();
