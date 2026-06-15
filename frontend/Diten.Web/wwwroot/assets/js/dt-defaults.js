/**
 * MOD-0013: Merkezi DataTable Konfigürasyonu (Sneat 2.x Layout API)
 * Referans: _Reference/Theme/full-version/assets/js/app-user-list.js
 */
'use strict';

window.DtDefaults = (function () {
    var L = function () { return window.L10n || {}; };
    var _authRefreshInFlight = null;
    var _buttonRadiusResizeBound = false;
    var _buttonRadiusResizeTimer = null;
    var _responsiveModalTitleBound = false;

    function isPlatformContext() {
        var host = window.location.hostname.toLowerCase();
        var path = window.location.pathname.toLowerCase();
        return host.startsWith('admin.') || path.startsWith('/platform/');
    }

    function getTenantId() {
        if (isPlatformContext()) {
            return null;
        }

        var user = window.CurrentUser || {};
        return user.tenantId || '00000000-0000-0000-0000-000000000001';
    }

    function redirectToLogin() {
        var returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
        var isAdminHost = window.location.hostname.toLowerCase().startsWith('admin.');
        var isPlatformRoute = window.location.pathname.toLowerCase().startsWith('/platform/');
        var loginPath = (isAdminHost || isPlatformRoute) ? '/platform/login' : '/account/login';
        window.location.href = loginPath + '?returnUrl=' + returnUrl;
    }

    function refreshTokenAndReload(ajaxSettings) {
        if (_authRefreshInFlight) {
            return _authRefreshInFlight.then(function (result) {
                if (result.success) {
                    if (ajaxSettings) {
                        retryAjax(ajaxSettings);
                    } else {
                        window.location.reload();
                    }
                } else if (result.reauthRequired) {
                    redirectToLogin();
                } else if (ajaxSettings) {
                    retryAjax(ajaxSettings);
                }
            });
        }

        var promiseResolve;
        _authRefreshInFlight = new Promise(function (resolve) {
            promiseResolve = resolve;
        });

        fetch('/account/refresh', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json'
            }
        })
            .then(function (res) {
                var contentType = res.headers.get('content-type') || '';
                var isJson = contentType.indexOf('application/json') !== -1;
                if (res.ok) {
                    return res.json().then(function (data) {
                        return { success: true, data: data };
                    });
                } else if (isJson) {
                    return res.json().then(function (data) {
                        return { success: false, reauthRequired: data.reauthRequired !== false, status: res.status };
                    });
                } else {
                    return { success: false, reauthRequired: res.status < 500, status: res.status };
                }
            })
            .catch(function () {
                return { success: false, reauthRequired: false, status: 0 };
            })
            .then(function (result) {
                _authRefreshInFlight = null;
                if (result.success) {
                    if (result.data && result.data.user) {
                        window.CurrentUser = result.data.user;
                    }
                    promiseResolve({ success: true, reauthRequired: false });
                    if (ajaxSettings) {
                        retryAjax(ajaxSettings);
                    } else {
                        window.location.reload();
                    }
                } else {
                    promiseResolve({ success: false, reauthRequired: result.reauthRequired });
                    if (result.reauthRequired) {
                        redirectToLogin();
                    } else if (ajaxSettings) {
                        retryAjax(ajaxSettings);
                    }
                }
            });

        return _authRefreshInFlight;
    }

    function retryAjax(settings) {
        if (!settings) return;
        if (settings._retried) return;
        settings._retried = true;
        $.ajax(settings);
    }

    /**
     * Ortak Responsive Renderer (Modal içi tablo oluşturucu).
     */
    function shouldSkipResponsiveColumn(api, col) {
        if (!col || col.title === '') return true;

        var settings = api.settings()[0];
        var column = settings && settings.aoColumns ? settings.aoColumns[col.columnIndex] : null;
        var columnName = String(column?.sName || '').toLowerCase();
        var columnClass = String(column?.sClass || '').toLowerCase();
        var title = String(col.title || '').toLowerCase();
        var data = String(col.data || '').toLowerCase();

        return columnName === 'control' ||
            columnName === 'checkbox' ||
            columnName === 'action' ||
            columnClass.includes('control') ||
            columnClass.includes('dt-checkboxes-cell') ||
            title.includes('<input') ||
            data.includes('dt-checkboxes') ||
            data.includes('type="checkbox"') ||
            data.includes("type='checkbox'");
    }

    function responsiveRenderer(api, rowIdx, columns) {
        var data = $.map(columns, function (col, i) {
            return !shouldSkipResponsiveColumn(api, col)
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

    function normalizeResponsiveModalTitle(modalEl) {
        var modal = modalEl && modalEl.classList && modalEl.classList.contains('dtr-bs-modal')
            ? modalEl
            : document.querySelector('.modal.dtr-bs-modal');

        if (!modal) return;

        modal.querySelectorAll('h4.modal-title').forEach(function (title) {
            var replacement = document.createElement('h5');

            Array.prototype.forEach.call(title.attributes, function (attr) {
                replacement.setAttribute(attr.name, attr.value);
            });

            while (title.firstChild) {
                replacement.appendChild(title.firstChild);
            }

            title.replaceWith(replacement);
        });
    }

    function bindResponsiveModalTitleFix() {
        if (_responsiveModalTitleBound) return;
        _responsiveModalTitleBound = true;

        document.addEventListener('show.bs.modal', function (event) {
            if (!event.target || !event.target.classList.contains('dtr-bs-modal')) return;
            normalizeResponsiveModalTitle(event.target);
            window.setTimeout(function () { normalizeResponsiveModalTitle(event.target); }, 0);
        }, true);

        document.addEventListener('shown.bs.modal', function (event) {
            if (!event.target || !event.target.classList.contains('dtr-bs-modal')) return;
            normalizeResponsiveModalTitle(event.target);
        }, true);
    }

    /**
     * Sneat 2.x Layout API — orijinal 'app-user-list.js' ile %100 uyumlu.
     */
    function buildLayout() {
        var l = L();
        return {
            topStart: {
                rowClass: 'row my-0 justify-content-between',
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
                rowClass: 'row justify-content-between',
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
                        return L().Details || 'Details';
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
        $('.dt-layout-end').removeClass('justify-content-between').addClass('d-flex gap-md-4 justify-content-md-between justify-content-center gap-4 flex-wrap mt-0');
        $('.dt-layout-start').addClass('mt-0');

        refreshButtonGroupRadii();
        bindButtonRadiusResizeRefresh();

        // Ensure dot z-index is protected
        $('.dt-colvis-btn').css('z-index', '4');

        $('.dt-layout-table').removeClass('row mt-2');
        $('.dt-layout-full').removeClass('col-md col-12');
        $('table.dataTable').addClass('table-hover');
    }

    function bindButtonRadiusResizeRefresh() {
        if (_buttonRadiusResizeBound) return;
        _buttonRadiusResizeBound = true;

        var scheduleRefresh = function () {
            window.clearTimeout(_buttonRadiusResizeTimer);
            _buttonRadiusResizeTimer = window.setTimeout(refreshButtonGroupRadii, 120);
        };

        window.addEventListener('resize', scheduleRefresh);

        if (window.matchMedia) {
            var mdQuery = window.matchMedia('(min-width: 768px)');
            if (mdQuery.addEventListener) {
                mdQuery.addEventListener('change', scheduleRefresh);
            } else if (mdQuery.addListener) {
                mdQuery.addListener(scheduleRefresh);
            }
        }
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

            $container.removeClass('d-flex gap-1 gap-2 gap-3 gap-4'); // Remove any JS-injected gaps

            var $allBtns = $container.children('button.btn');
            var $visibleBtns = $allBtns.filter(':visible').filter(function () {
                return !this.hidden;
            });

            // Mark groups for responsive/layout styling (e.g., keep primary action controlled)
            var hasAddNew = $container.find('.add-new').length > 0;
            $container.toggleClass('dt-buttons-primary', hasAddNew);
            $container.toggleClass('dt-buttons-actions', !hasAddNew);

            if ($container.hasClass('dt-buttons-actions')) {
                if ($visibleBtns.length > 1) {
                    $container.addClass('btn-group');
                } else {
                    $container.removeClass('btn-group');
                }

                $allBtns.each(function () {
                    this.style.removeProperty('border-radius');
                    this.style.removeProperty('border-top-left-radius');
                    this.style.removeProperty('border-bottom-left-radius');
                    this.style.removeProperty('border-top-right-radius');
                    this.style.removeProperty('border-bottom-right-radius');
                    this.style.removeProperty('border-left');
                    this.style.setProperty('margin-left', '0', 'important');
                    this.style.setProperty('position', 'relative', 'important');
                });

                if ($visibleBtns.length > 1) {
                    var isDarkActions = document.documentElement.getAttribute('data-bs-theme') === 'dark';
                    var actionBorderColor = isDarkActions ? 'rgba(255, 255, 255, 0.15)' : 'rgba(0, 0, 0, 0.1)';

                    $visibleBtns.each(function (index) {
                        this.style.setProperty('border-radius', '0', 'important');

                        if (index === 0) {
                            this.style.setProperty('border-top-left-radius', '0.375rem', 'important');
                            this.style.setProperty('border-bottom-left-radius', '0.375rem', 'important');
                        } else {
                            this.style.setProperty('border-top-left-radius', '0', 'important');
                            this.style.setProperty('border-bottom-left-radius', '0', 'important');
                            this.style.setProperty('border-left', '1px solid ' + actionBorderColor, 'important');
                        }

                        if (index === $visibleBtns.length - 1) {
                            this.style.setProperty('border-top-right-radius', '0.375rem', 'important');
                            this.style.setProperty('border-bottom-right-radius', '0.375rem', 'important');
                        } else {
                            this.style.setProperty('border-top-right-radius', '0', 'important');
                            this.style.setProperty('border-bottom-right-radius', '0', 'important');
                        }
                    });
                } else if ($visibleBtns.length === 1) {
                    $visibleBtns[0].style.setProperty('border-radius', '0.375rem', 'important');
                }

                return;
            }

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

            if (window.matchMedia('(min-width: 768px)').matches) {
                $container.find('.dt-filter-btn:visible').each(function () {
                    this.style.setProperty('border-top-left-radius', '0', 'important');
                    this.style.setProperty('border-bottom-left-radius', '0', 'important');
                });
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
            merged.ajax.xhrFields = $.extend(true, {}, merged.ajax.xhrFields, { withCredentials: true });
            merged.ajax.error = merged.ajax.error || function (xhr, textStatus, errorThrown) {
                try { $('#skeleton-loader').fadeOut(200); } catch (e) { }

                var status = xhr && xhr.status ? xhr.status : 0;
                var url = merged.ajax && merged.ajax.url ? merged.ajax.url : '(unknown url)';
                var responseText = xhr && xhr.responseText ? xhr.responseText : '';

                // eslint-disable-next-line no-console
                console.error('[DtDefaults] Ajax error', { status: status, url: url, textStatus: textStatus, errorThrown: errorThrown, responseText: responseText });

                if (status === 401) {
                    if (this._retried) {
                        return;
                    }
                    refreshTokenAndReload(this);
                    return;
                }

                if (status === 403) {
                    if (window.showToast) {
                        window.showToast('Permission denied.', 'error');
                    }
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

    function customizePrintWindow(win, titleText) {
        var doc = win.document;
        var safeTitle = titleText || document.title || 'Report';
        var printedAt = new Date().toLocaleString();
        var rootStyles = window.getComputedStyle(document.documentElement);
        var primaryColor = (rootStyles.getPropertyValue('--bs-primary') || '').trim() || '#696cff';
        var primaryRgb = (rootStyles.getPropertyValue('--bs-primary-rgb') || '').trim() || '105, 108, 255';
        var moduleName = safeTitle.split(' - ')[0].trim() || safeTitle;

        doc.title = safeTitle;

        var style = doc.createElement('style');
        style.type = 'text/css';
        style.textContent = [
            '@page { size: auto; margin: 16mm; }',
            'html, body { background: #f4f6f8 !important; color: #17202a !important; font-family: "Public Sans", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif !important; }',
            'body { margin: 0 !important; padding: 24px !important; }',
            '.print-shell { max-width: 1100px; margin: 0 auto; background: #ffffff; border: 1px solid #e9edf3; border-radius: 16px; padding: 22px 28px; box-shadow: 0 18px 50px rgba(15, 23, 42, 0.08); }',
            '.print-header { display: block; margin-bottom: 16px; padding-bottom: 12px; border-bottom: 1px solid #e9edf3; }',
            '.print-kicker { font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: ' + primaryColor + '; margin-bottom: 6px; }',
            '.print-title { margin: 0; font-size: 28px; line-height: 1.15; font-weight: 700; color: #111827; }',
            '.print-meta { margin: 10px 0 0; padding-top: 10px; border-top: 1px solid #e9edf3; font-size: 13px; color: #6b7280; }',
            'table { width: 100% !important; border-collapse: collapse !important; margin: 0 !important; }',
            'thead th { background: #f8fafc !important; color: #334155 !important; font-size: 12px !important; font-weight: 700 !important; text-transform: uppercase; letter-spacing: 0.04em; border-bottom: 1px solid #dbe3ee !important; padding: 12px 14px !important; }',
            'tbody td { padding: 12px 14px !important; border-bottom: 1px solid #e9edf3 !important; color: #17202a !important; vertical-align: middle !important; }',
            'tbody tr:nth-child(even) td { background: #fcfdfd !important; }',
            '.dt-print-view h1 { display: none !important; }',
            '.dt-print-view table.dataTable thead th, .dt-print-view table.dataTable tbody td { box-shadow: none !important; }',
            '.dt-print-view table.dataTable tbody tr.selected td { background: #fff7ed !important; }',
            '@media print { html, body { background: #fff !important; } body { padding: 0 !important; } .print-shell { max-width: none; border: 0; border-radius: 0; box-shadow: none; padding: 0; } .print-header { margin-bottom: 12px; } }'
        ].join('\n');

        doc.head.appendChild(style);

        var body = doc.body;
        var table = body.querySelector('table');
        var shell = doc.createElement('div');
        shell.className = 'print-shell';

        var header = doc.createElement('div');
        header.className = 'print-header';
        header.innerHTML =
            '<div>' +
            '  <div class="print-kicker">' + moduleName + '</div>' +
            '  <h1 class="print-title">' + safeTitle + '</h1>' +
            '  <p class="print-meta">Generated ' + printedAt + '</p>' +
            '</div>';

        shell.appendChild(header);

        if (table) {
            shell.appendChild(table);
        }

        body.innerHTML = '';
        body.appendChild(shell);
    }

    /**
     * Standard export buttons + optional extras.
     */
    function exportButtons(addNewText, addNewAttr, extraButtons, options) {
        var l = L();
        options = options || {};

        // MOD-0021: Dynamically determine exportable columns if not provided
        var exportColumns = Array.isArray(options.exportColumns) ? options.exportColumns : [];
        if (exportColumns.length === 0) {
            // Default: Columns 2 to N-1 (skips control/checkbox and actions)
            $('.dataTable thead th').each(function(idx) {
               if (idx > 1) exportColumns.push(idx);
            });
            if (exportColumns.length > 0) exportColumns.pop(); // Remove last (actions)
        }
        
        var colvisColumns = Array.isArray(options.colvisColumns) ? options.colvisColumns : exportColumns;
        var showAllColumns = Array.isArray(options.showAllColumns) ? options.showAllColumns : colvisColumns;

        var exportOptions = buildExportOptions(exportColumns);

        var exportBtn = {
            extend: 'collection',
            className: 'btn btn-label-secondary dropdown-toggle dt-export-collection-btn',
            text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-cog icon-sm"></i> <span class="d-none d-sm-inline-block">' + (l.Action || 'Action') + '</span></span>',
            buttons: [
                {
                    extend: 'print',
                    text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>' + (l.Print || 'Print') + '</span>',
                    className: 'dropdown-item',
                    exportOptions: exportOptions,
                    title: document.title || 'Report',
                    autoPrint: false,
                    customize: function (win) {
                        customizePrintWindow(win, document.title || 'Report');
                    }
                },
                { extend: 'csv', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>CSV</span>', className: 'dropdown-item', exportOptions: exportOptions },
                { extend: 'excel', text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>', className: 'dropdown-item', exportOptions: exportOptions },
                { extend: 'pdf', text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-2"></i>' + (l.PDF || 'PDF') + '</span>', className: 'dropdown-item', exportOptions: exportOptions },
                { extend: 'copy', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-copy me-2"></i>' + (l.Copy || 'Copy') + '</span>', className: 'dropdown-item', exportOptions: exportOptions }
            ]
        };

        if (extraButtons && extraButtons.importBtn) {
            var importAction = extraButtons.importBtn.action;
            var importTitle = (extraButtons.importBtn.attr && extraButtons.importBtn.attr.title) || l.Import || 'Import';
            exportBtn.buttons.push({ text: '<hr class="my-0">', className: 'dropdown-item p-0 pe-none bg-transparent border-0', action: function() {} });
            exportBtn.buttons.push({
                text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-import me-2"></i>' + importTitle + '</span>',
                className: 'dropdown-item',
                action: importAction || function() {}
            });
        }

        var colvisBtn = {
            extend: 'colvis',
            text: '<i class="icon-base bx bx-show icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-colvis-btn position-relative d-none d-md-inline-flex',
            attr: {
                title: l.ColumnVisibility || 'Column Visibility',
                'data-bs-toggle': 'tooltip',
                'data-colvis-columns': Array.isArray(colvisColumns) ? colvisColumns.join(',') : ''
            },
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
        var group2 = [];
        
        if (!options.skipColVis) {
            group2.push(colvisBtn);
        }

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
            var managedColumns = ($colvisBtn.attr('data-colvis-columns') || '')
                .split(',')
                .map(function (value) { return parseInt(value, 10); })
                .filter(function (value) { return Number.isInteger(value); });

            var hiddenCount = managedColumns.filter(function (idx) {
                try {
                    return !api.column(idx).visible();
                } catch (e) {
                    return false;
                }
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

    bindResponsiveModalTitleFix();

    return {
        create: create,
        exportButtons: exportButtons,
        responsiveRenderer: responsiveRenderer,
        updateVisualState: updateVisualState,
        refreshButtonGroupRadii: refreshButtonGroupRadii,
        handleUnauthorized: refreshTokenAndReload
    };
})();
