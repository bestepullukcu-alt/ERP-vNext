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
                features: [
                    {
                        pageLength: {
                            menu: [10, 25, 50, 100],
                            text: '_MENU_'
                        }
                    },
                    {
                        search: {
                            placeholder: l.Search || 'Search...',
                            text: '_INPUT_'
                        }
                    }
                ]
            },
            topEnd: {
                features: []
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
        // 1. Basic Element Cleaning
        $('.dt-buttons .btn').removeClass('shadow-none');
        $('.dt-search .form-control').removeClass('form-control-sm').addClass('shadow-none');
        $('.dt-length .form-select').removeClass('form-select-sm').addClass('ms-0');

        // 2. Responsive Layout Containers
        // Sneat standard card padding: px-md-6 (24px) for desktop, px-4 (16px) for mobile.
        $('.dt-layout-row').first().attr('class', 'dt-layout-row row mx-0 px-md-6 px-4 py-2 align-items-center d-flex flex-row flex-wrap');

        // Start Cell (Now contains Length + Search)
        $('.dt-layout-start').first().attr('class', 'dt-layout-cell dt-layout-start col d-flex flex-row flex-wrap align-items-center justify-content-start p-0 gap-2');
        $('.dt-length').addClass('m-0');
        $('.dt-search').addClass('m-0 d-flex align-items-center');
        $('.dt-search input').addClass('form-control shadow-none').css('max-width', '150px'); // Ensure they fit on same line

        // End Cell (Now contains only Buttons)
        $('.dt-layout-end').first().attr('class', 'dt-layout-cell dt-layout-end col-12 col-md-auto d-flex justify-content-center justify-content-md-end align-items-center p-0 mt-2 mt-md-0');

        $('.dt-buttons').each(function () {
            var $container = $(this);
            // MOD-0014: Remove any DataTables-generated internal wraps
            $container.find('> .btn-group, > .dt-button-collection, > .dt-layout-cell').each(function () {
                $(this).contents().unwrap();
            });

            // Mobile: Take full width to drops correctly if needed, but here we handled it via dt-layout-end
            $container.addClass('m-0 d-flex justify-content-center justify-content-md-end');
            $container.removeClass('d-flex gap-1 gap-2 gap-3 gap-4'); // Remove any JS-injected gaps

            if ($container.children().length > 1) {
                $container.addClass('btn-group');

                // Nuclear Fix for Border Radius & Dividers using Inline Styles
                var $btns = $container.children('button.btn, a.btn');
                $btns.each(function (index) {
                    this.style.setProperty('border-radius', '0', 'important');
                    this.style.setProperty('margin-left', '0', 'important');
                    this.style.setProperty('position', 'relative', 'important');

                    if (index === 0) {
                        this.style.setProperty('border-top-left-radius', '0.375rem', 'important');
                        this.style.setProperty('border-bottom-left-radius', '0.375rem', 'important');
                    } else {
                        var isDark = document.documentElement.getAttribute('data-bs-theme') === 'dark';
                        var borderColor = isDark ? 'rgba(255, 255, 255, 0.15)' : 'rgba(0, 0, 0, 0.1)';
                        this.style.setProperty('border-left', '1px solid ' + borderColor, 'important');
                    }

                    if (index === $btns.length - 1) {
                        this.style.setProperty('border-top-right-radius', '0.375rem', 'important');
                        this.style.setProperty('border-bottom-right-radius', '0.375rem', 'important');
                    }
                });

            } else {
                $container.removeClass('btn-group');
            }
        });

        // Ensure dot z-index is protected
        $('.dt-colvis-btn').css('z-index', '4');

        $('.dt-layout-table').removeClass('row mt-2');
        $('.dt-layout-full').removeClass('col-md col-12');
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
        rows: function (idx, data, node) {
            // Tablo genelinde seçili satır var mı kontrol et
            var $table = $(node).closest('table');
            var hasSelected = $table.find('tbody tr.selected').length > 0;

            // Eğer seçim varsa sadece seçili olanları getir, yoksa hepsini (filtrelenmiş haliyle) getir
            if (hasSelected) {
                return $(node).hasClass('selected');
            }
            return true;
        },
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
            columns: [2, 3, 4, 5, 6, 7, 8], // Exclude Index 0 (Control), 1 (Checkbox), 9 (Actions)
            postfixButtons: [
                {
                    extend: 'colvisGroup',
                    text: l.ShowAll || 'Tümünü Göster',
                    show: [2, 3, 4, 5, 6, 7, 8],
                    className: 'btn btn-outline-primary mt-2 w-100'
                }
            ]
        };

        var allButtons = [exportBtn];
        if (extraButtons && extraButtons.importBtn) allButtons.push(extraButtons.importBtn);
        allButtons.push(colvisBtn);
        if (extraButtons && extraButtons.filterBtn) allButtons.push(extraButtons.filterBtn);

        if (addNewText) {
            allButtons.push({
                text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">' + addNewText + '</span>',
                className: 'add-new btn btn-primary',
                attr: addNewAttr || {}
            });
        }

        // Extra array buttons (usually custom actions)
        if (Array.isArray(extraButtons)) {
            allButtons = allButtons.concat(extraButtons);
        }

        return [{ buttons: allButtons }];
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
