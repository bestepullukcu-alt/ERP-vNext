/**
 * MOD-0013: Merkezi DataTable Konfigürasyonu (Sneat 2.x Layout API)
 * Referans: _Reference/Theme/full-version/assets/js/app-user-list.js
 */
'use strict';

window.DtDefaults = (function () {
    var L = function () { return window.L10n || {}; };
    var _authRefreshInFlight = null;

    function getCookie(name) {
        var value = '; ' + document.cookie;
        var parts = value.split('; ' + name + '=');
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    function setCookie(name, value, expiryOrDays) {
        var expires = '';
        if (typeof expiryOrDays === 'string') {
            expires = 'expires=' + new Date(expiryOrDays).toUTCString();
        } else if (typeof expiryOrDays === 'number') {
            var date = new Date();
            date.setTime(date.getTime() + (expiryOrDays * 24 * 60 * 60 * 1000));
            expires = 'expires=' + date.toUTCString();
        }
        document.cookie = name + '=' + value + ';' + expires + ';path=/;SameSite=Strict';
    }

    function clearCookie(name) {
        document.cookie = name + '=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/;SameSite=Strict';
    }

    function getTenantId() {
        try {
            var user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (e) {
            return '00000000-0000-0000-0000-000000000001';
        }
    }

    function redirectToLogin() {
        try {
            clearCookie('access_token');
            clearCookie('refresh_token');
        } catch (e) { }
        var returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
        window.location.href = '/account/login?returnUrl=' + returnUrl;
    }

    function refreshTokenAndReload() {
        if (_authRefreshInFlight) return _authRefreshInFlight;

        var apiBaseUrl = window.ApiBaseUrl || '';
        var accessToken = getCookie('access_token');
        var refreshToken = getCookie('refresh_token');

        if (!apiBaseUrl || !accessToken || !refreshToken) {
            redirectToLogin();
            return null;
        }

        _authRefreshInFlight = fetch(apiBaseUrl + '/api/auth/refresh-token', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Tenant-Id': getTenantId()
            },
            body: JSON.stringify({ accessToken: accessToken, refreshToken: refreshToken })
        })
            .then(function (res) {
                if (!res.ok) throw new Error('refresh-failed');
                return res.json();
            })
            .then(function (data) {
                // Auth service currently returns `expiresAt` as refresh-token expiry.
                // Keep access_token cookie long-lived so we can refresh seamlessly when the JWT itself expires.
                setCookie('access_token', data.accessToken, data.expiresAt);
                setCookie('refresh_token', data.refreshToken, data.expiresAt);
                try { localStorage.setItem('user', JSON.stringify(data.user || {})); } catch (e) { }
                window.location.reload();
            })
            .catch(function () {
                redirectToLogin();
            })
            .finally(function () {
                _authRefreshInFlight = null;
            });

        return _authRefreshInFlight;
    }

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
                rowClass: 'row px-3 my-0 justify-content-between',
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
                rowClass: 'row px-3 justify-content-between',
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
        $('.dt-buttons .btn').removeClass('btn-secondary').addClass('shadow-none');
        $('.dt-search .form-control').removeClass('form-control-sm').addClass('shadow-none');
        $('.dt-length .form-select').removeClass('form-select-sm').addClass('ms-0');
        $('.dt-length').addClass('mb-md-6 mb-0');
        $('.dt-search').addClass('mb-md-6 mb-2');
        $('.dt-layout-end').removeClass('justify-content-between').addClass('d-flex gap-md-4 justify-content-md-between justify-content-center gap-4 flex-wrap mt-0');
        $('.dt-layout-start').addClass('mt-0');

        refreshButtonGroupRadii();

        // Ensure dot z-index is protected
        $('.dt-colvis-btn').css('z-index', '4');

        $('.dt-layout-table').removeClass('row mt-2');
        $('.dt-layout-full').removeClass('col-md col-12');
        $('table.dataTable').addClass('table-hover');
    }

    /**
     * Fix button-group border radius/dividers with respect to *visible* buttons.
     * Important for cases where a button exists in DOM but is hidden (e.g., Save Filter).
     */
    function refreshButtonGroupRadii() {
        $('.dt-buttons').each(function () {
            var $container = $(this);
            // MOD-0014: Remove any DataTables-generated internal wraps
            // NOTE: Do not unwrap `.dt-button-collection` — it is the live dropdown container when a Buttons collection is open.
            $container.find('> .btn-group').each(function () {
                $(this).contents().unwrap();
            });

            $container.addClass('mb-md-0 mb-6');
            $container.removeClass('d-flex gap-1 gap-2 gap-3 gap-4'); // Remove any JS-injected gaps

            var $allBtns = $container.children('button.btn');
            var $visibleBtns = $allBtns.filter(':visible').filter(function () {
                return !this.classList.contains('d-none') && !this.hidden;
            });

            // Mark groups for responsive/layout styling (e.g., keep primary action controlled)
            var hasAddNew = $container.find('.add-new').length > 0;
            $container.toggleClass('dt-buttons-primary', hasAddNew);
            $container.toggleClass('dt-buttons-actions', !hasAddNew);

            // Reset all buttons first (including hidden ones), so stale radii won't remain.
            $allBtns.each(function () {
                this.style.setProperty('border-radius', '0', 'important');
                this.style.setProperty('border-top-left-radius', '0', 'important');
                this.style.setProperty('border-bottom-left-radius', '0', 'important');
                this.style.setProperty('border-top-right-radius', '0', 'important');
                this.style.setProperty('border-bottom-right-radius', '0', 'important');
                this.style.setProperty('border-left', '0', 'important');
                this.style.setProperty('margin-left', '0', 'important');
                this.style.setProperty('position', 'relative', 'important');
            });

            if ($visibleBtns.length > 1) {
                $container.addClass('btn-group');

                var isDark = document.documentElement.getAttribute('data-bs-theme') === 'dark';
                var borderColor = isDark ? 'rgba(255, 255, 255, 0.15)' : 'rgba(0, 0, 0, 0.1)';

                $visibleBtns.each(function (index) {
                    if (index === 0) {
                        this.style.setProperty('border-top-left-radius', '0.375rem', 'important');
                        this.style.setProperty('border-bottom-left-radius', '0.375rem', 'important');
                    } else {
                        this.style.setProperty('border-left', '1px solid ' + borderColor, 'important');
                    }

                    if (index === $visibleBtns.length - 1) {
                        this.style.setProperty('border-top-right-radius', '0.375rem', 'important');
                        this.style.setProperty('border-bottom-right-radius', '0.375rem', 'important');
                    }
                });
            } else {
                $container.removeClass('btn-group');
                if ($visibleBtns.length === 1) {
                    // Single visible button should keep rounded corners
                    $visibleBtns[0].style.setProperty('border-radius', '0.375rem', 'important');
                }
            }
        });
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

        // Centralized Ajax error handler (helps diagnose DataTables "Ajax error" tn/7 quickly)
        // Note: DataTables treats `ajax: { ... }` as an $.ajax config object.
        if (merged.ajax && typeof merged.ajax === 'object') {
            merged.ajax.error = merged.ajax.error || function (xhr, textStatus, errorThrown) {
                try { $('#skeleton-loader').fadeOut(200); } catch (e) { }

                var status = xhr && xhr.status ? xhr.status : 0;
                var url = merged.ajax && merged.ajax.url ? merged.ajax.url : '(unknown url)';
                var responseText = xhr && xhr.responseText ? xhr.responseText : '';

                // eslint-disable-next-line no-console
                console.error('[DtDefaults] Ajax error', { status: status, url: url, textStatus: textStatus, errorThrown: errorThrown, responseText: responseText });

                if (status === 401) {
                    refreshTokenAndReload();
                    return;
                }

                if (window.showToast) {
                    var msg = 'DataTables Ajax error (HTTP ' + status + ')';
                    window.showToast(msg, 'error');
                }
            };
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
            $('#skeleton-loader').fadeOut(200);
            applySneatClassFixes();
            if (typeof originalDrawCallback === 'function') {
                originalDrawCallback.call(this, settings);
            }
        };

        return merged;
    }

    var defaultExportColumns = [2, 3, 4, 5, 6, 7, 8];

    /**
     * Ortak export ayarları (HTML temizleme ve kolon seçimi).
     */
    var commonExportOptionsBase = {
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

    function buildExportOptions(columns) {
        return $.extend(true, {}, commonExportOptionsBase, { columns: columns });
    }

    /**
     * Standard export buttons + optional extras.
     */
    function exportButtons(addNewText, addNewAttr, extraButtons, options) {
        var l = L();
        options = options || {};

        var exportColumns = Array.isArray(options.exportColumns) ? options.exportColumns : defaultExportColumns;
        var colvisColumns = Array.isArray(options.colvisColumns) ? options.colvisColumns : exportColumns;
        var showAllColumns = Array.isArray(options.showAllColumns) ? options.showAllColumns : colvisColumns;

        var exportOptions = buildExportOptions(exportColumns);

        var exportBtn = {
            extend: 'collection',
            className: 'btn btn-label-secondary dropdown-toggle dt-export-collection-btn',
            text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-export icon-sm"></i> <span class="d-none d-sm-inline-block">' + (l.Export || 'Export') + '</span></span>',
            buttons: [
                { extend: 'print', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>' + (l.Print || 'Print') + '</span>', className: 'dropdown-item', exportOptions: exportOptions },
                { extend: 'csv', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>CSV</span>', className: 'dropdown-item', exportOptions: exportOptions },
                { extend: 'excel', text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>', className: 'dropdown-item', exportOptions: exportOptions },
                { extend: 'pdf', text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-2"></i>' + (l.PDF || 'PDF') + '</span>', className: 'dropdown-item', exportOptions: exportOptions },
                { extend: 'copy', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-copy me-2"></i>' + (l.Copy || 'Copy') + '</span>', className: 'dropdown-item', exportOptions: exportOptions }
            ]
        };

        var colvisBtn = {
            extend: 'colvis',
            text: '<i class="icon-base bx bx-show icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-colvis-btn position-relative',
            attr: { title: l.ColumnVisibility || 'Column Visibility', 'data-bs-toggle': 'tooltip' },
            columns: colvisColumns, // Exclude Index 0 (Control), 1 (Checkbox), (Actions) based on module
            postfixButtons: [
                {
                    extend: 'colvisGroup',
                    text: l.ShowAll || 'Tümünü Göster',
                    show: showAllColumns,
                    className: 'btn btn-outline-primary mt-2 w-100'
                }
            ]
        };

        var group1 = [exportBtn];
        if (extraButtons && extraButtons.importBtn) group1.push(extraButtons.importBtn);

        var group2 = [colvisBtn];
        if (extraButtons && extraButtons.filterBtn) group2.push(extraButtons.filterBtn);
        if (extraButtons && extraButtons.saveFilterBtn) group2.push(extraButtons.saveFilterBtn);

        var features = [
            { buttons: group1 },
            { buttons: group2 }
        ];

        // Extra array buttons (usually custom actions)
        if (Array.isArray(extraButtons)) {
            features.push({ buttons: extraButtons });
        }

        // Add New button stays in its own group as a primary action
        if (addNewText) {
            features.push({
                buttons: [{
                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">' + addNewText + '</span>',
                    className: 'add-new btn btn-primary',
                    attr: addNewAttr || {}
                }]
            });
        }

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
                $filterBtn.append('<span class="badge badge-center rounded-pill bg-primary position-absolute top-0 end-0 translate-middle">' + filterCount + '</span>');
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
                $colvisBtn.append('<span class="badge badge-center rounded-pill bg-primary position-absolute top-0 end-0 translate-middle">' + hiddenCount + '</span>');
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
        updateVisualState: updateVisualState,
        refreshButtonGroupRadii: refreshButtonGroupRadii
    };
})();
