/**
 * MOD-0013: Merkezi DataTable Konfigürasyonu (Sneat 2.x Layout API)
 * Referans: _Reference/Theme/full-version/assets/js/app-user-list.js
 */
'use strict';

window.DtDefaults = (function () {
    var L = function () { return window.L10n || {}; };

    /*
     * BL-047b — the DataTable chrome ("Showing 1 to 9 of 9 entries", the pager, the empty-table sentence) in the
     * reader's language, on EVERY table, from ONE place.
     *
     * The six strings have been in SharedResource in all seven languages the whole time. What was missing was
     * the delivery path: BL-047 seeded them onto `window.L10n` from the WorkCenterNext payload alone, so every
     * OTHER screen — each with its own page and its own l10n payload — went on rendering the vendor's English.
     * A Turkish /Tasks/FieldDefinitions read "No data available in table" on 2026-08-10.
     *
     * WHY HERE, and not a partial each management page includes. "Every page must remember to include it" IS the
     * defect: WorkCenterNext remembered, the field-definition screen did not, and this repository has 61 files
     * that build a DataTable. A per-page fix leaves the next screen to be born English — which is exactly why
     * the recurrence screen in this same slice could not be built before this landed. One consumer reads one
     * payload; a page that wants its own wording still wins, below.
     *
     * Read LATE and cached, never at load time: this file is a <script> in the layout and the payload is another
     * tag beside it. Reading on first use means the order of two tags cannot silently take the translations away.
     */
    var SHARED_L10N_ELEMENT_ID = 'datatable-l10n';
    var _sharedL10n = null;
    var sharedL10n = function () {
        if (_sharedL10n) { return _sharedL10n; }
        var el = document.getElementById(SHARED_L10N_ELEMENT_ID);
        if (!el) { return {}; }   // not cached: the tag may simply not have been parsed yet
        try {
            _sharedL10n = JSON.parse(el.textContent || '{}') || {};
        } catch (e) {
            // A broken payload costs the page its translations, never its table.
            console.error('[DtDefaults] Shared DataTable localization payload could not be parsed.', e);
            _sharedL10n = {};
        }
        return _sharedL10n;
    };

    /*
     * One key, from the page's own dictionary first and the shared payload second.
     *
     * The precedence is BL-047's and is kept deliberately: a screen that chose its own sentence keeps it. The
     * fallback is per KEY, not per dictionary — a page that overrides one string must not lose the other five.
     */
    var dtText = function (key) {
        var own = L()[key];
        return own || sharedL10n()[key];
    };

    var _authRefreshInFlight = null;
    var _buttonRadiusResizeBound = false;
    var _buttonRadiusResizeTimer = null;
    var _buttonRadiusSettleTimer = null;
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

    /*
     * ══ ONE RELOAD PER PAGE AFTER A SESSION REFRESH (CT-SHELL-FIX1, item 6) ═══════════════════════════════════════════
     *
     * A caller with no request to retry (the library's error channel below, and every page that calls
     * `handleUnauthorized()` bare) gets a page reload once the session refresh succeeds. If the API keeps refusing the
     * token while /account/refresh keeps succeeding — the JWT 7.x ↔ 8.16 mismatch of 2026-07 did exactly that — every
     * reload ends in another 401, another refresh and another reload: the page refreshes forever.
     *
     * So the page remembers, for a short while, that it has already reloaded for this reason (sessionStorage, keyed by
     * path; every access inside try/catch). First time: reload. Second time: the sign-in page decides. Third time — the
     * sign-in page sent the reader straight back, because the web tier still accepts the cookie — nothing navigates any
     * more; `onStop` (the list's own failure state, when the caller has one) is all that happens. Without a usable
     * storage the page cannot count, so it never reloads blind: it goes to the sign-in page.
     */
    var AUTH_RELOAD_GUARD_PREFIX = 'dt-defaults.auth-reload:';
    var AUTH_RELOAD_GUARD_MS = 60000;

    function authReloadDecision() {
        var key = AUTH_RELOAD_GUARD_PREFIX + window.location.pathname;
        var now = Date.now();
        var previous = null;
        try {
            var raw = window.sessionStorage.getItem(key);
            previous = raw ? JSON.parse(raw) : null;
        } catch (e) {
            return 'login';
        }
        if (!previous || typeof previous.at !== 'number' || now - previous.at > AUTH_RELOAD_GUARD_MS) {
            previous = null;
        }
        var count = previous ? (Number(previous.count) || 1) + 1 : 1;
        try {
            window.sessionStorage.setItem(key, JSON.stringify({ at: previous ? previous.at : now, count: count }));
        } catch (e) {
            return 'login';
        }
        return count === 1 ? 'reload' : count === 2 ? 'login' : 'stop';
    }

    // Several callers can be waiting on the same refresh (two lists on one page, a list and a fetch). The page navigates
    // once: the first decision to reload or to sign in is the page's; the others are not counted against it.
    var _navigationPending = false;
    function reloadAfterRefresh(onStop) {
        if (_navigationPending) { return; }
        var decision = authReloadDecision();
        if (decision === 'reload') {
            _navigationPending = true;
            window.location.reload();
        } else if (decision === 'login') {
            _navigationPending = true;
            redirectToLogin();
        } else if (typeof onStop === 'function') {
            onStop();
        }
    }

    function refreshTokenAndReload(ajaxSettings, onStop) {
        if (_authRefreshInFlight) {
            return _authRefreshInFlight.then(function (result) {
                if (result.success) {
                    if (ajaxSettings) {
                        retryAjax(ajaxSettings);
                    } else {
                        reloadAfterRefresh(onStop);
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
                var isJson = window.DitenHttp.isJsonMediaType(res.headers.get('content-type'));
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
                        reloadAfterRefresh(onStop);
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

    /*
     * ══ BL-515 — A LIST THAT COULD NOT BE LOADED SAYS SO, INSIDE THE TABLE, IN THE READER'S LANGUAGE ══════════════
     *
     * MEASURED in the vendored DataTables (_fnBuildAjax): when the `ajax` option is an object carrying `error`, that
     * function REPLACES the library's own — no `xhr` event, no `_fnLog`, and the processing indicator stays on. Every
     * list built through create() gets such an `error` below, so those lists never showed the vendor's alert box: they
     * showed an English toast (the HTTP status, or a fixed permission sentence) over an empty table. A list
     * whose `ajax` is a URL STRING (or one built without create()) goes through the library's own handler instead,
     * and that one ends in `errMode`, whose default is a browser alert: "DataTables warning: table id=… - Ajax error".
     *
     * Now both paths end here. 401 keeps today's rule (refresh the session, then retry or go to the login page);
     * 403 says the reader may not see this list; anything else says the list could not be loaded and offers a retry.
     * The words come from the shared DataTable payload (_DataTableL10n.cshtml → SharedResource), English only as the
     * last fallback. The table's own body carries the message, so it sits where the rows would have been.
     *
     * The failure is TABLE STATE, not a one-off paint (CT-SHELL-FIX1, items 2 and 4): it is recorded on the table's
     * settings, every later draw (a sort, a keystroke in the search box, a page length, a column shown) paints it again
     * instead of the library's "no records", the processing indicator is taken down even mid-draw, and the first answer
     * that loads clears it. A CANCELLED request (item 3) is not a failure and draws nothing.
     *
     * ⚠ No `global.` and no bare `document` here: the cell is built from the table's own ownerDocument, which is the
     * browser's document in a page and the sandbox's in a test (tests/dt-defaults-runs-in-a-browser.test.js).
     */
    var LOAD_ERROR_FALLBACK = {
        DtLoadFailed: 'The list could not be loaded.',
        DtLoadForbidden: 'You do not have permission to see this list.',
        DtRetry: 'Retry'
    };
    // Each key read by name, so the payload's key list and what this file reads stay provably equal
    // (tests/datatable-language-one-delivery-path.test.js).
    var loadErrorText = function (key) {
        var own = key === 'DtLoadFailed' ? dtText('DtLoadFailed')
            : key === 'DtLoadForbidden' ? dtText('DtLoadForbidden')
                : dtText('DtRetry');
        return own || LOAD_ERROR_FALLBACK[key];
    };
    // The library as the rest of this file reaches it (the free `DataTable`), `window.DataTable` as the fallback.
    var dataTableLibrary = function () {
        return (typeof DataTable !== 'undefined' && DataTable) || window.DataTable || null;
    };

    /*
     * ⚠ REVEALING THE TABLE IS ONE ACT, AND EVERY EXIT HAS TO PERFORM IT (2026-09-23, owner report).
     *
     * MEASURED: the shaped placeholder hides the table through a CSS sibling rule, so what reveals the table
     * is REMOVING the element — hiding it is not enough. Only `initComplete` removed it, and DataTables does
     * not call `initComplete` when the first ajax FAILS. With the golden reference pages' service down, the
     * placeholder stayed in the DOM, the rule kept matching, and the page showed nothing at all. Before the
     * shaped placeholder existed the same failure left an empty table on screen — so the change turned
     * "service down" into "page down".
     *
     * Now the draw path, the ajax error path and the in-table failure state perform the same act. A list that still
     * carries the old hidden block keeps falling through to the fade, exactly as before.
     */
    function revealTable() {
        // `document`, never `global`: the browser has no `global` (only Node does — which is why vitest stayed green
        // while every list page threw ReferenceError inside initComplete, 2026-09-23 14:16–15:00).
        var shaped = document.querySelector('#skeleton-loader[data-table-skeleton]');
        if (shaped) {
            shaped.remove();
            return true;
        }
        try { $('#skeleton-loader').fadeOut(200); } catch (e) { }
        return false;
    }

    /*
     * A request that was CANCELLED is not a failure (CT-SHELL-FIX1, item 3). DataTables aborts the request still in
     * flight when a page reloads the list (vendored `__reload`: `xhr.abort()` before `_fnBuildAjax`), and jQuery 3.7.1
     * reports that with textStatus "abort" ("canceled" when it was stopped before being sent). The next request is
     * already on its way: drawing "could not be loaded" and taking the indicator down would lie about both.
     *
     * Leaving the page is the other cancellation. The browser drops the requests in flight and jQuery reports each as
     * status 0 / "error" — by itself the same answer as a gateway that cannot be reached, which IS a failure and must
     * keep saying so (that is why this is not the library's blunt `readyState !== 4`: it would also silence the
     * unreachable service). So the leaving page is remembered (beforeunload / pagehide) and forgotten when it is shown
     * again (pageshow — the back/forward cache restores the same page), and only a status-0 answer is set aside then.
     */
    var _pageIsLeaving = false;
    function watchPageLeaving() {
        try {
            window.addEventListener('beforeunload', function () { _pageIsLeaving = true; });
            window.addEventListener('pagehide', function () { _pageIsLeaving = true; });
            window.addEventListener('pageshow', function (event) {
                _pageIsLeaving = false;
                // Only a page brought back from the back/forward cache had navigated away; the first pageshow of a
                // load must not forget a reload this page has just asked for.
                if (event && event.persisted) { _navigationPending = false; }
            });
        } catch (e) { }
    }

    function isCancelledRequest(xhr, textStatus) {
        if (textStatus === 'abort' || textStatus === 'canceled') { return true; }
        return _pageIsLeaving && !(xhr && xhr.status);
    }

    // A 200 answer whose body carries `error` / `sError` — the library's own definition of a server-reported failure.
    function serverReportedError(json) {
        return !!(json && typeof json === 'object' && (json.error || json.sError));
    }

    function tableApiFor(settings) {
        var library = dataTableLibrary();
        if (!settings || !library || typeof library.Api !== 'function') { return null; }
        try { return new library.Api(settings); } catch (e) { return null; }
    }

    /*
     * MEASURED in the vendored DataTables 2.1.8 (`_fnProcessingDisplay`): a request to HIDE the indicator is ignored while
     * `settings.bDrawing` is set — and the server-side branch of `_fnDraw` sets it, sends the request through
     * `_fnAjaxUpdate` and returns, leaving the end of the draw to the answer. A failed answer never finishes that draw,
     * so `processing(false)` alone could never take a server-side list's indicator down (CT-SHELL-FIX1, item 2). The
     * library's own end-of-draw order is applied first (bSorted, bFiltered, bDrawing = false), then the indicator goes.
     */
    function stopProcessing(api, settings) {
        if (settings.bDrawing) {
            settings.bSorted = false;
            settings.bFiltered = false;
            settings.bDrawing = false;
        }
        try { api.processing(false); } catch (e) { }
    }

    /*
     * The failure outlives the draw that showed it (CT-SHELL-FIX1, item 4). Written outside the library's state, the row
     * was replaced by `_emptyRow` — "no records" — the moment a client-side list redrew for a sort, a search keystroke,
     * a page length or a column. Until a load succeeds every draw paints the failure again; the first answer that
     * carries data (the library's `xhr` event with a body and no `error`) clears it, before that answer is drawn.
     * The two listeners are the library's own events, bound through its API, once per table.
     */
    function keepLoadErrorAcrossDraws(api, settings) {
        if (settings._dtLoadErrorWatched) { return; }
        settings._dtLoadErrorWatched = true;
        api.on('draw.dtLoadError', function () {
            if (settings._dtLoadError) { paintLoadError(api, settings); }
        });
        api.on('xhr.dtLoadError', function (_event, _settings, json) {
            if (json && !serverReportedError(json)) { settings._dtLoadError = null; }
        });
    }

    function renderLoadError(settings, status) {
        var api = tableApiFor(settings);
        if (!api) { return false; }
        settings._dtLoadError = { status: status === 403 ? 403 : (status || 0) };
        keepLoadErrorAcrossDraws(api, settings);
        stopProcessing(api, settings);
        revealTable();
        return paintLoadError(api, settings);
    }

    // Whether a table (its API or its settings) is showing the load failure — for a page that keeps its own notices
    // (an "empty" banner must not say "no records" beside "could not be loaded").
    function hasLoadError(target) {
        var settings = target && typeof target.settings === 'function' ? target.settings()[0] : target;
        return !!(settings && settings._dtLoadError);
    }

    function paintLoadError(api, settings) {
        var state = settings._dtLoadError;
        var body = null;
        try { body = api.table().body(); } catch (e) { body = null; }
        if (!state || !body || !body.ownerDocument) { return false; }

        var forbidden = state.status === 403;
        var columns = 1;
        try { columns = Math.max(1, api.columns(':visible').count()); } catch (e) { }
        var doc = body.ownerDocument;
        var row = doc.createElement('tr');
        var cell = doc.createElement('td');
        cell.colSpan = columns;
        cell.className = 'dt-empty';
        var box = doc.createElement('div');
        box.className = 'd-flex flex-column align-items-center gap-2 py-4 text-center';
        box.setAttribute('role', 'alert');
        box.setAttribute('data-dt-load-error', forbidden ? 'forbidden' : 'failed');
        var text = doc.createElement('span');
        text.className = 'text-body-secondary';
        text.textContent = loadErrorText(forbidden ? 'DtLoadForbidden' : 'DtLoadFailed');
        box.appendChild(text);
        if (!forbidden) {
            var retry = doc.createElement('button');
            retry.type = 'button';
            retry.className = 'btn btn-sm btn-label-primary';
            retry.setAttribute('data-dt-retry', 'true');
            retry.textContent = loadErrorText('DtRetry');
            retry.addEventListener('click', function () { api.ajax.reload(null, false); });
            box.appendChild(retry);
        }
        cell.appendChild(box);
        row.appendChild(cell);
        while (body.firstChild) { body.removeChild(body.firstChild); }
        body.appendChild(row);
        return true;
    }

    /*
     * The library's warning channel: written to the console, never an alert box.
     *
     * An ajax or JSON failure that reached it (tn 7 / tn 1 — a table built without create()) is shown in the table like
     * any other. A 200 answer whose body says `error` reaches it too — the vendored `_fnBuildAjax` reports `json.error`
     * through `_fnLog(oSettings, 0, error)` with no technical note — and is a failed load as well (CT decision
     * 2026-10-04, item 7: the server's error must not stay invisible). It is recognised by the answer itself
     * (`jqXHR.responseJSON`), not by the wording. Every other warning (tn 3 re-initialisation, tn 4 unknown parameter,
     * tn 18 column count …) is a developer's message: the console only, no English box for the reader.
     */
    function logInsteadOfAlert(settings, techNote, message) {
        // eslint-disable-next-line no-console
        console.error('[DtDefaults] DataTables warning', { techNote: techNote, message: message });
        var xhr = settings && settings.jqXHR;
        if (techNote === 1 || techNote === 7) {
            var status = xhr && xhr.status ? xhr.status : 0;
            if (status === 401) {
                refreshTokenAndReload(null, function () { renderLoadError(settings, status); });
                return;
            }
            renderLoadError(settings, status);
            return;
        }
        if (techNote === undefined && serverReportedError(xhr && xhr.responseJSON)) {
            renderLoadError(settings, 0);
        }
    }

    function installErrMode() {
        var library = dataTableLibrary();
        var ext = (library && library.ext)
            || (window.jQuery && window.jQuery.fn && window.jQuery.fn.dataTable && window.jQuery.fn.dataTable.ext);
        if (ext) { ext.errMode = logInsteadOfAlert; }
    }

    watchPageLeaving();

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

        // A table rendered inside a freshly-shown tab/collapse can report a not-yet-settled
        // layout on this synchronous pass — a responsive button (e.g. colvis is `d-md-inline-flex`)
        // may momentarily test as not-visible, so the joined button group resolves to the wrong
        // radii and the bad inline `!important` styles override the correct CSS until the next
        // resize. Re-run after the next paint and once more after layout fully settles, mirroring
        // what the resize handler already does. Idempotent + debounced, so no pile-up.
        window.requestAnimationFrame(refreshButtonGroupRadii);
        window.clearTimeout(_buttonRadiusSettleTimer);
        _buttonRadiusSettleTimer = window.setTimeout(refreshButtonGroupRadii, 200);

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

            // A container inside a hidden tab-pane reports 0 visible buttons. Skip it so we
            // don't strip the radii of buttons that are merely off-screen (e.g. a sibling
            // sub-tab DataTable); they keep their rounding and are re-evaluated once visible.
            if ($visibleBtns.length === 0) {
                return;
            }

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
        installErrMode();
        var merged = $.extend(true, {}, baseConfig, userConfig);
        var l = L();
        // A URL string is the same request as `{ url }`; as an object it gets the error handling below (BL-515).
        if (typeof merged.ajax === 'string') { merged.ajax = { url: merged.ajax }; }
        /*
         * The table this config is building — needed by the ajax error callback, whose `this` is jQuery's request
         * settings, not the table's. It must be in hand BEFORE the first request (CT-SHELL-FIX1, item 1).
         *
         * MEASURED in the vendored DataTables 2.1.8: `_fnInitialise` fires `preInit` and then `_fnReDraw` → `_fnDraw`,
         * whose FIRST act is the `aoPreDrawCallback` list (= `preDrawCallback`). Only after that does its server-side
         * branch call `_fnAjaxUpdate` (the request) and RETURN — `aoDrawCallback` (= `drawCallback`) is not reached
         * until the answer is drawn. So a server-side list whose first request failed had no settings here: nothing
         * was drawn and the indicator spun forever. `preInit` runs first too, but it is an EVENT on the table node,
         * and create() returns a config before any table exists; `preDrawCallback` is the earliest init option that
         * sees the settings on the client-side and the server-side path alike. drawCallback / initComplete still
         * refresh it.
         */
        var tableSettings = null;
        var originalPreDrawCallback = merged.preDrawCallback;
        merged.preDrawCallback = function (settings) {
            tableSettings = settings || tableSettings;
            // A page's own preDrawCallback keeps its say — returning false still cancels the draw.
            return typeof originalPreDrawCallback === 'function'
                ? originalPreDrawCallback.apply(this, arguments)
                : undefined;
        };

        /*
         * ── ONE LOADING LANGUAGE AT A TIME (owner report, 2026-09-21) ────────────────────────────────────
         *
         * The product had already chosen the skeleton: `backbone-custom.css` carries a "NO NEW SKELETON
         * LANGUAGE" warning and 226 views ship placeholder markup. What it did not have was a working way to
         * SHOW one on a list — so every list opened with the theme's `sk-fold` cube instead, which is what the
         * owner saw and asked about.
         *
         * ⚠ AND THE FIRST FIX FOR IT DID NOT RUN. MEASURED in the vendored DataTables: `preXhr` is an EVENT,
         * not an init option — the options registered as callbacks are drawCallback, initComplete,
         * preDrawCallback, rowCallback and the state ones. Setting `merged.preXhr` therefore called nothing,
         * on any page, and the tests that drove that function directly were green the whole time. So the
         * mechanism moved out of JavaScript: `_TableSkeleton.cshtml` renders the placeholder VISIBLE, a CSS
         * sibling rule keeps the table (and the cube inside it) out of the page while that element is there,
         * and the block below removes the element once the table has drawn. Nothing has to fire.
         */
        merged.language.searchPlaceholder = merged.language.searchPlaceholder || l.Search || sharedL10n().Search || 'Search...';

        // DataTables i18n mapping. Each slot left undefined is a slot DataTables fills with its own English —
        // which is precisely what a Turkish page was showing before these had a delivery path (BL-047b).
        var noRecords = dtText('DtNoRecords');
        var zeroRecords = dtText('DtZeroRecords');
        var info = dtText('DtInfo');
        var infoEmpty = dtText('DtInfoEmpty');
        var infoFiltered = dtText('DtInfoFiltered');
        var emptyTable = dtText('DtEmptyTable');
        /*
         * ⚠ AND THE THIRD VOICE GOES QUIET. Left unset, DataTables writes its own English "Loading..." into the
         * body while the skeleton is drawn above it — an untranslated sentence on every list in the product.
         * The skeleton already says "this is loading", so the row says nothing rather than saying it in the
         * wrong language. No new resource key: there is nothing to translate.
         */
        merged.language.loadingRecords = merged.language.loadingRecords || '';
        if (noRecords) merged.language.zeroRecords = noRecords;
        if (info) merged.language.info = info;
        if (infoEmpty) merged.language.infoEmpty = infoEmpty;
        if (infoFiltered) merged.language.infoFiltered = infoFiltered;
        if (zeroRecords) merged.language.zeroRecords = zeroRecords;
        if (emptyTable) merged.language.emptyTable = emptyTable;

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

        // Revealing the table on every exit: see revealTable() above (the draw path, the error path, the failure state).

        // Centralized Ajax error handler (helps diagnose DataTables "Ajax error" tn/7 quickly)
        // Note: DataTables treats `ajax: { ... }` as an $.ajax config object.
        if (merged.ajax && typeof merged.ajax === 'object') {
            merged.ajax.xhrFields = $.extend(true, {}, merged.ajax.xhrFields, { withCredentials: true });
            // A page that brings its own handler keeps it — but the placeholder is removed on failure regardless,
            // or that page would show a placeholder forever when its service is down.
            var pageAjaxError = typeof merged.ajax.error === 'function' ? merged.ajax.error : null;
            var defaultAjaxError = function (xhr, textStatus, errorThrown) {
                var status = xhr && xhr.status ? xhr.status : 0;
                var url = merged.ajax && merged.ajax.url ? merged.ajax.url : '(unknown url)';

                // eslint-disable-next-line no-console
                console.error('[DtDefaults] Ajax error', { status: status, url: url, textStatus: textStatus, errorThrown: errorThrown });

                if (status === 401) {
                    if (this._retried) {
                        return;
                    }
                    refreshTokenAndReload(this);
                    return;
                }

                renderLoadError(tableSettings, status);
            };
            // A page's own handler still decides first. Returning `false` hands the failure back to the component —
            // the way a page keeps only what is its own (a controlled-failure sentence) and gives up the rest (BL-515).
            // A CANCELLED request is no failure at all (item 3): neither the page's handler nor the component hears of
            // it, nothing is revealed, drawn or switched off — the request that replaced it is still running.
            merged.ajax.error = pageAjaxError
                ? function (xhr, textStatus, errorThrown) {
                    if (isCancelledRequest(xhr, textStatus)) { return undefined; }
                    revealTable();
                    var handled = pageAjaxError.call(this, xhr, textStatus, errorThrown);
                    return handled === false ? defaultAjaxError.call(this, xhr, textStatus, errorThrown) : handled;
                }
                : function (xhr, textStatus, errorThrown) {
                    if (isCancelledRequest(xhr, textStatus)) { return undefined; }
                    revealTable();
                    return defaultAjaxError.call(this, xhr, textStatus, errorThrown);
                };
        }

        // Auto-hide skeleton + apply class fixes
        var originalInitComplete = merged.initComplete;
        merged.initComplete = function (settings, json) {
            tableSettings = settings || tableSettings;
            /*
             * ⚠ REMOVED, NOT HIDDEN. The CSS keeps the table out of the page for as long as this element is its
             * sibling, so taking the element away IS what reveals the table — one act, no second class to get
             * out of step with. A list still carrying the old hidden block falls through to the fadeOut below,
             * exactly as before.
             */
            if (revealTable()) {
                /*
                 * A table that was not rendered has no column widths worth keeping. DataTables recomputes on
                 * demand, so ask it once, now that the table is actually on screen.
                 */
                try {
                    var api = new DataTable.Api(settings);
                    api.columns.adjust();
                    if (api.responsive && typeof api.responsive.recalc === 'function') { api.responsive.recalc(); }
                } catch (e) { }
            }
            applySneatClassFixes();
            if (typeof originalInitComplete === 'function') {
                originalInitComplete.call(this, settings, json);
            }
        };

        // Redraw durumunda class fixleri tazele
        var originalDrawCallback = merged.drawCallback;
        merged.drawCallback = function (settings) {
            tableSettings = settings || tableSettings;
            /*
             * ⚠ NOT A REVEAL EXIT. DataTables draws the table ONCE, EMPTY, before the ajax request is answered
             * (measured on 2.1.8: first drawCallback fires with the response still pending). Revealing here took
             * the shaped placeholder away at init and every list opened on the three dots instead of its shape —
             * the owner saw it the same afternoon it shipped (2026-09-23). The answer arrives through
             * initComplete (success) or a failure (ajax.error, or renderLoadError for the library's own error
             * channel and a 200 answer that says `error`); those are the only exits.
             */
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
        var allowed = Array.isArray(columns) ? columns.slice() : [];
        return $.extend(true, {}, commonExportOptionsBase, { columns: exportableColumns(allowed) });
    }

    /*
     * K16 (Kullanıcılar testi, 2026-09-23): a column the reader HID still came out in CSV/Excel/PDF/print, because
     * the export was a fixed index list. The file must be what the screen shows: the page's allowed columns AND
     * only those currently visible. DataTables detaches a hidden column's header cell from the document, so a
     * header node that is not connected is a hidden column — no API round-trip, and no dependence on CSS
     * visibility (a table inside a collapsed tab would otherwise export nothing). A call with no node (older
     * Buttons builds, unit harnesses) falls back to the index rule alone.
     */
    function exportableColumns(allowed) {
        return function (idx, data, node) {
            if (allowed.length && allowed.indexOf(idx) < 0) return false;
            if (node && typeof node.isConnected === 'boolean') return node.isConnected;
            return true;
        };
    }

    /*
     * BL-452 package 2 — THE CONTROLLED COPY (owner decision 2026-09-24). A printed or PDF'd list says what it is and
     * where it came from: the screen, the tenant, the applied filters and search, the sort, the row count, who made it
     * and when (with the time zone) — and every page says "uncontrolled copy" with its page number (GxP "uncontrolled
     * when printed"; Veeva and MasterControl stamp creator, date, source and the uncontrolled mark on every printout).
     *
     * ONE data object (`buildControlledCopy`) feeds BOTH outputs: the pdfmake doc-definition and the print window's
     * DOM are two renderings of the same lines, so the PDF and the printout cannot tell different stories.
     *
     * Rows: a list that hands `controlledCopy.rows` (the factory, on a server-mode list with `export: { mode: 'server' }`)
     * gives every matching row from the service; everything else prints what DataTables holds, exactly as before.
     * Words: SharedResource through `window.L10n` (the tenant shell's l10n bridge), English only as the last fallback.
     */
    var CONTROLLED_COPY_FALLBACK = {
        ControlledCopyReport: 'Report',
        ControlledCopyTenant: 'Company',
        ControlledCopyFilters: 'Filters',
        ControlledCopySearch: 'Search',
        ControlledCopySort: 'Sort',
        ControlledCopySortAsc: 'ascending',
        ControlledCopySortDesc: 'descending',
        ControlledCopyRows: 'Rows',
        ControlledCopyCreatedBy: 'Created by',
        ControlledCopyCreatedAt: 'Created at',
        ControlledCopyNone: 'None',
        ControlledCopyUncontrolled: 'This printout is an uncontrolled copy — {0}',
        ControlledCopyPage: 'Page {0} of {1}'
    };
    var _lastControlledCopy = null;

    function ccText(key) { return L()[key] || CONTROLLED_COPY_FALLBACK[key]; }
    function fillTemplate(template, a, b) { return String(template).split('{0}').join(String(a)).split('{1}').join(b === undefined ? '' : String(b)); }
    function cellText(value) { return value === null || value === undefined ? '' : String(value); }

    // "25.09.2026 14:05 GMT+3 (Europe/Istanbul)" — the reader's culture, and the zone said out loud.
    function controlledCopyStamp(date) {
        var zone = '';
        try { zone = Intl.DateTimeFormat().resolvedOptions().timeZone || ''; } catch (e) { zone = ''; }
        var text;
        try {
            text = new Intl.DateTimeFormat(window.CurrentLanguage || undefined, {
                year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', timeZoneName: 'short'
            }).format(date);
        } catch (e) {
            text = date.toISOString();
        }
        return zone ? text + ' (' + zone + ')' : text;
    }

    // The screen's name is the page title without the product suffix ("Kullanıcılar - Di10" → "Kullanıcılar").
    function screenTitle() {
        var raw = String(document.title || '').replace(/\s+-\s+[^-]*$/, '').trim();
        return raw || ccText('ControlledCopyReport');
    }

    // The tenant shell's brand carries the tenant's display name (TenantBrand view component).
    function tenantName() {
        var el = document.querySelector('[data-tenant-name]');
        return el ? String(el.getAttribute('data-tenant-name') || '').trim() : '';
    }

    // The sorted columns by their header text and direction, read from the table as the reader sees it.
    function describeSort(dt) {
        var order;
        try { order = dt.order() || []; } catch (e) { return []; }
        var list = Array.isArray(order[0]) || (order[0] && typeof order[0] === 'object') ? order : (order.length ? [order] : []);
        return list.map(function (entry) {
            var idx = Array.isArray(entry) ? entry[0] : (entry.idx !== undefined ? entry.idx : entry.column);
            var dir = Array.isArray(entry) ? entry[1] : entry.dir;
            var header = null;
            try { header = dt.column(idx).header(); } catch (e) { header = null; }
            var label = header ? String(header.textContent || '').trim() : '';
            if (!label) return '';
            return label + ' (' + ccText(String(dir).toLowerCase() === 'desc' ? 'ControlledCopySortDesc' : 'ControlledCopySortAsc') + ')';
        }).filter(Boolean);
    }

    /**
     * The controlled copy as data: { title, tenant, lines: [{ key, label, value }], header, body, rowCount, createdBy,
     * createdAt, uncontrolled, pageTemplate, dir }. `input` = { header, body, filters: [{ label, values }], search, sort }.
     */
    function buildControlledCopy(input) {
        input = input || {};
        var none = ccText('ControlledCopyNone');
        var stamp = controlledCopyStamp(input.now || new Date());
        var header = (input.header || []).map(cellText);
        var width = header.length;
        var body = (input.body || []).map(function (row) {
            var cells = (row || []).map(cellText);
            if (!width) return cells;
            while (cells.length < width) cells.push('');
            return cells.slice(0, width);
        });
        var filters = (input.filters || []).filter(function (f) { return f && f.label && f.values && f.values.length; });
        var sort = Array.isArray(input.sort) ? input.sort : [];
        var tenant = input.tenant !== undefined ? input.tenant : tenantName();
        var createdBy = input.createdBy !== undefined ? input.createdBy : ((window.CurrentUser && window.CurrentUser.email) || '');
        var search = cellText(input.search).trim();
        return {
            title: input.title || screenTitle(),
            tenant: tenant,
            lines: [
                { key: 'tenant', label: ccText('ControlledCopyTenant'), value: tenant || '—' },
                { key: 'filters', label: ccText('ControlledCopyFilters'), value: filters.length ? filters.map(function (f) { return f.label + ': ' + f.values.join(', '); }).join('; ') : none },
                { key: 'search', label: ccText('ControlledCopySearch'), value: search || none },
                { key: 'sort', label: ccText('ControlledCopySort'), value: sort.length ? sort.join(', ') : none },
                { key: 'rows', label: ccText('ControlledCopyRows'), value: String(body.length) },
                { key: 'createdBy', label: ccText('ControlledCopyCreatedBy'), value: createdBy || '—' },
                { key: 'createdAt', label: ccText('ControlledCopyCreatedAt'), value: stamp }
            ],
            header: header,
            body: body,
            rowCount: body.length,
            createdBy: createdBy,
            createdAt: stamp,
            uncontrolled: fillTemplate(ccText('ControlledCopyUncontrolled'), stamp),
            pageTemplate: ccText('ControlledCopyPage'),
            dir: document.documentElement.getAttribute('dir') === 'rtl' ? 'rtl' : 'ltr'
        };
    }

    // The pdfmake rendering of the copy. The footer is pdfmake's per-page callback: uncontrolled mark + page x/y.
    function controlledCopyDocDefinition(copy) {
        var content = [
            { text: copy.tenant || '', style: 'ccKicker' },
            { text: copy.title, style: 'ccTitle' },
            {
                table: { widths: ['auto', '*'], body: copy.lines.map(function (line) { return [{ text: line.label, style: 'ccLabel' }, { text: line.value, style: 'ccValue' }]; }) },
                layout: 'noBorders',
                margin: [0, 6, 0, 12]
            }
        ];
        if (copy.header.length) {
            content.push({
                table: {
                    headerRows: 1,
                    widths: copy.header.map(function () { return '*'; }),
                    body: [copy.header.map(function (h) { return { text: h, style: 'ccHead' }; })].concat(copy.body)
                },
                layout: 'lightHorizontalLines'
            });
        }
        return {
            pageSize: 'A4',
            pageOrientation: copy.header.length > 5 ? 'landscape' : 'portrait',
            pageMargins: [32, 40, 32, 44],
            info: { title: copy.title, author: copy.createdBy, subject: copy.uncontrolled },
            content: content,
            footer: function (currentPage, pageCount) {
                return {
                    margin: [32, 12, 32, 0],
                    columns: [
                        { text: copy.uncontrolled, style: 'ccFooter' },
                        { text: fillTemplate(copy.pageTemplate, currentPage, pageCount), style: 'ccFooter', alignment: 'right', width: 'auto' }
                    ]
                };
            },
            defaultStyle: { fontSize: 8 },
            styles: {
                ccKicker: { fontSize: 8, bold: true, color: '#696cff' },
                ccTitle: { fontSize: 16, bold: true, color: '#111827', margin: [0, 2, 0, 0] },
                ccLabel: { bold: true, color: '#334155' },
                ccValue: { color: '#17202a' },
                ccHead: { bold: true, color: '#334155', fillColor: '#f8fafc' },
                ccFooter: { fontSize: 7, color: '#6b7280' }
            }
        };
    }

    function cssString(text) { return '"' + String(text).replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/[\r\n]+/g, ' ') + '"'; }

    // "Sayfa {0} / {1}" → "Sayfa " counter(page) " / " counter(pages) — the page number in the reader's words.
    function pageCounterContent(template) {
        return String(template).split(/(\{0\}|\{1\})/).filter(function (part) { return part !== ''; }).map(function (part) {
            if (part === '{0}') return 'counter(page)';
            if (part === '{1}') return 'counter(pages)';
            return cssString(part);
        }).join(' ');
    }

    // The print window's rendering of the copy. Stays in the window's own <style> (FG-003: no inline style).
    function writeControlledPrint(win, copy) {
        var doc = win.document;
        doc.open();
        doc.write('<!DOCTYPE html><html><head><meta charset="utf-8"><title></title></head><body></body></html>');
        doc.close();
        doc.documentElement.setAttribute('lang', window.CurrentLanguage || '');
        doc.documentElement.setAttribute('dir', copy.dir);
        doc.title = copy.title;

        var rootStyles = window.getComputedStyle(document.documentElement);
        var primaryColor = (rootStyles.getPropertyValue('--bs-primary') || '').trim() || '#696cff';
        var style = doc.createElement('style');
        style.textContent = [
            '@page { size: auto; margin: 16mm 16mm 20mm; @bottom-left { content: ' + cssString(copy.uncontrolled) + '; font-size: 9px; color: #6b7280; } @bottom-right { content: ' + pageCounterContent(copy.pageTemplate) + '; font-size: 9px; color: #6b7280; } }',
            'html, body { background: #f4f6f8; color: #17202a; font-family: "Public Sans", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }',
            'body { margin: 0; padding: 24px; }',
            '.print-shell { max-width: 1100px; margin: 0 auto; background: #ffffff; border: 1px solid #e9edf3; border-radius: 16px; padding: 22px 28px; box-shadow: 0 18px 50px rgba(15, 23, 42, 0.08); }',
            '.print-header { margin-bottom: 16px; padding-bottom: 12px; border-bottom: 1px solid #e9edf3; }',
            '.print-kicker { font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: ' + primaryColor + '; margin-bottom: 6px; }',
            '.print-title { margin: 0; font-size: 28px; line-height: 1.15; font-weight: 700; color: #111827; }',
            '.print-meta { display: grid; grid-template-columns: max-content 1fr; gap: 4px 16px; margin: 10px 0 0; padding-top: 10px; border-top: 1px solid #e9edf3; font-size: 13px; }',
            '.print-meta dt { font-weight: 600; color: #334155; }',
            '.print-meta dd { margin: 0; color: #4b5563; }',
            'table { width: 100%; border-collapse: collapse; margin: 0; }',
            'thead th { background: #f8fafc; color: #334155; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; border-bottom: 1px solid #dbe3ee; padding: 12px 14px; text-align: start; }',
            'tbody td { padding: 12px 14px; border-bottom: 1px solid #e9edf3; color: #17202a; vertical-align: middle; }',
            'tbody tr:nth-child(even) td { background: #fcfdfd; }',
            '.print-footer { margin: 16px 0 0; font-size: 12px; color: #6b7280; }',
            '@media print { html, body { background: #fff; } body { padding: 0; } .print-shell { max-width: none; border: 0; border-radius: 0; box-shadow: none; padding: 0; } thead { display: table-header-group; } }'
        ].join('\n');
        doc.head.appendChild(style);

        var el = function (tag, className, text) {
            var node = doc.createElement(tag);
            if (className) node.className = className;
            if (text !== undefined) node.textContent = text;
            return node;
        };
        var shell = el('div', 'print-shell');
        var header = el('header', 'print-header');
        header.appendChild(el('div', 'print-kicker', copy.tenant || ''));
        header.appendChild(el('h1', 'print-title', copy.title));
        var meta = el('dl', 'print-meta');
        copy.lines.forEach(function (line) {
            var term = el('dt', null, line.label);
            var value = el('dd', null, line.value);
            term.setAttribute('data-line', line.key);
            value.setAttribute('data-line', line.key);
            meta.appendChild(term);
            meta.appendChild(value);
        });
        header.appendChild(meta);
        shell.appendChild(header);

        if (copy.header.length) {
            var table = el('table', 'print-table');
            var headRow = el('tr');
            copy.header.forEach(function (h) { headRow.appendChild(el('th', null, h)); });
            var thead = el('thead');
            thead.appendChild(headRow);
            var tbody = el('tbody');
            copy.body.forEach(function (row) {
                var tr = el('tr');
                row.forEach(function (cell) { tr.appendChild(el('td', null, cell)); });
                tbody.appendChild(tr);
            });
            table.appendChild(thead);
            table.appendChild(tbody);
            shell.appendChild(table);
        }
        shell.appendChild(el('p', 'print-footer', copy.uncontrolled));
        doc.body.appendChild(shell);
    }

    /*
     * Decision C (vendored pdfmake 0.2.15 ships Roboto only: 0 of 256 Arabic and 0 of 20 992 CJK code points, measured
     * 2026-09-25). A Chinese or Arabic PDF from pdfmake would print blank boxes, so in those two languages the PDF entry
     * opens the print window and the browser saves the PDF with its own fonts. No font is embedded (bundle size); a
     * server-side PDF is BL-452 package 4's note.
     */
    function pdfNeedsBrowserPrint(lang) {
        var code = String(lang || '').toLowerCase().split('-')[0];
        return code === 'zh' || code === 'ar';
    }

    function controlledCopyFileName(title) {
        var stem = String(title || '').toLowerCase().replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-+|-+$/g, '') || 'report';
        var now = new Date();
        var pad = function (n) { return String(n).padStart(2, '0'); };
        return stem + '-' + now.getUTCFullYear() + pad(now.getUTCMonth() + 1) + pad(now.getUTCDate()) + '-' + pad(now.getUTCHours()) + pad(now.getUTCMinutes()) + '.pdf';
    }

    /**
     * Print or PDF, one path. `source` = { rows() → Promise<{ header, body } | null>, meta(dt) → { filters, search } }:
     * `rows` present = the server's rows (null = refused, the provider already told the reader); absent = the rows
     * DataTables holds, through the same exportOptions the buttons always used.
     */
    async function runControlledCopy(kind, dt, exportOptions, source) {
        source = source || {};
        var viaPrint = kind === 'print' || pdfNeedsBrowserPrint(window.CurrentLanguage);
        // Opened NOW, inside the click: a window opened after an await is a popup the browser blocks.
        var win = viaPrint ? window.open('', '_blank') : null;
        if (viaPrint && !win) { window.showToast?.('ErrorOccurred', 'error'); return null; }
        var rows = null;
        try {
            rows = typeof source.rows === 'function' ? await source.rows() : dt.buttons.exportData(exportOptions);
        } catch (error) {
            console.error('[DtDefaults] Controlled copy rows failed.', error);
            window.showToast?.('ErrorOccurred', 'error');
            rows = null;
        }
        if (!rows) {
            if (win) { try { win.close(); } catch (e) { } }
            return null;
        }
        var meta = (typeof source.meta === 'function' ? source.meta(dt) : null) || {};
        var copy = buildControlledCopy({
            header: rows.header,
            body: rows.body,
            filters: meta.filters,
            search: meta.search !== undefined ? meta.search : dt.search(),
            sort: describeSort(dt)
        });
        var result = { requested: kind, output: viaPrint ? 'print' : 'pdf', copy: copy, docDefinition: null, window: win };
        if (viaPrint) {
            writeControlledPrint(win, copy);
            try { win.focus(); if (kind === 'pdf') win.print(); } catch (e) { }
        } else {
            var pdfMake = window.pdfMake;
            if (!pdfMake || typeof pdfMake.createPdf !== 'function') {
                console.error('[DtDefaults] pdfmake is not loaded — the PDF cannot be built.');
                window.showToast?.('ErrorOccurred', 'error');
                return null;
            }
            result.docDefinition = controlledCopyDocDefinition(copy);
            pdfMake.createPdf(result.docDefinition).download(controlledCopyFileName(copy.title));
        }
        _lastControlledCopy = result;
        return result;
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

        /*
         * BL-452 package 1 — THE FILE IS THE SCREEN. On a server-mode list DataTables holds only the page on screen, so
         * its own csv/excel buttons would write those 10 rows and call it the list. A list that declares a server export
         * hands `options.serverExport(format)`: CSV and Excel then ask the service for every matching row (visible
         * columns, applied filter, search and order) instead. Without it — every client-mode page, every page that never
         * heard of this — the two buttons are exactly the DataTables buttons they always were.
         *
         * BL-452 package 2 — PDF and print are the CONTROLLED COPY on every list (runControlledCopy): the header block and
         * the uncontrolled-copy footer always; the rows from `options.controlledCopy.rows` when the list hands it (the
         * factory on a server export — every matching row from the service), else the rows DataTables holds. Copy is
         * unchanged.
         */
        var serverExport = typeof options.serverExport === 'function' ? options.serverExport : null;
        var controlledCopy = options.controlledCopy || {};
        var csvText = '<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>CSV</span>';
        var excelText = '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>';
        var csvBtn = serverExport
            ? { text: csvText, className: 'dropdown-item dt-server-export', attr: { 'data-export-format': 'csv' }, action: function () { serverExport('csv'); } }
            : { extend: 'csv', text: csvText, className: 'dropdown-item', exportOptions: exportOptions };
        var excelBtn = serverExport
            ? { text: excelText, className: 'dropdown-item dt-server-export', attr: { 'data-export-format': 'xlsx' }, action: function () { serverExport('xlsx'); } }
            : { extend: 'excel', text: excelText, className: 'dropdown-item', exportOptions: exportOptions };

        /*
         * BL-452 (standard 1) — the file is a right of its own: a page passing `exportPermitted: false` (the reader lacks
         * its {module}.export key) gets no Print / CSV / Excel / PDF entry at all — not a disabled one, not one that
         * answers 403 — and no Copy either (owner, 2026-09-25: Copy puts the table's rows on the clipboard, which is the data
         * leaving the screen just like a CSV). With nothing left in it, the Action button itself is not drawn (unless the page
         * adds its own module items to that menu). Omitted = permitted: every page that never heard of this keeps its menu.
         */
        var exportPermitted = options.exportPermitted !== false;

        var exportBtn = {
            extend: 'collection',
            className: 'btn btn-label-secondary dropdown-toggle dt-export-collection-btn',
            text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-cog icon-sm"></i> <span class="d-none d-sm-inline-block">' + (l.Action || 'Action') + '</span></span>',
            buttons: [
                {
                    text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>' + (l.Print || 'Print') + '</span>',
                    className: 'dropdown-item dt-controlled-copy',
                    attr: { 'data-export-format': 'print' },
                    action: function (e, dt) { return runControlledCopy('print', dt, exportOptions, controlledCopy); }
                },
                csvBtn,
                excelBtn,
                {
                    text: '<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-2"></i>' + (l.PDF || 'PDF') + '</span>',
                    className: 'dropdown-item dt-controlled-copy',
                    attr: { 'data-export-format': 'pdf' },
                    action: function (e, dt) { return runControlledCopy('pdf', dt, exportOptions, controlledCopy); }
                },
                { extend: 'copy', text: '<span class="d-flex align-items-center"><i class="icon-base bx bx-copy me-2"></i>' + (l.Copy || 'Copy') + '</span>', className: 'dropdown-item', exportOptions: exportOptions }
            ].filter(function () { return exportPermitted; })
        };

        // Module-supplied entries for the Action dropdown (e.g. MOD-0150 Contacts template download / server-side
        // export). Additive and opt-in: a module that passes no `collectionBtns` gets exactly the dropdown it had
        // before. They sit with Import, below the client-side print/CSV/Excel/PDF/copy group.
        var moduleItems = (extraButtons && Array.isArray(extraButtons.collectionBtns))
            ? extraButtons.collectionBtns.filter(Boolean)
            : [];
        var importBtn = extraButtons && extraButtons.importBtn;

        if ((moduleItems.length || importBtn) && exportBtn.buttons.length) {
            exportBtn.buttons.push({ text: '<hr class="my-0">', className: 'dropdown-item p-0 pe-none bg-transparent border-0', action: function() {} });
        }

        moduleItems.forEach(function (item) {
            exportBtn.buttons.push({
                text: '<span class="d-flex align-items-center"><i class="icon-base bx ' + (item.icon || 'bx-file') + ' me-2"></i>' + (item.text || '') + '</span>',
                className: 'dropdown-item' + (item.className ? ' ' + item.className : ''),
                action: item.action || function() {}
            });
        });

        if (importBtn) {
            var importAction = importBtn.action;
            var importTitle = (importBtn.attr && importBtn.attr.title) || l.Import || 'Import';
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

        // BL-452 — an Action menu with no entry is not drawn (a reader without the export right, no module items).
        var group1 = exportBtn.buttons.length ? [exportBtn] : [];
        var group2 = [];
        
        if (!options.skipColVis) {
            group2.push(colvisBtn);
        }

        if (extraButtons && extraButtons.filterBtn) group2.push(extraButtons.filterBtn);
        if (extraButtons && extraButtons.saveFilterBtn) group2.push(extraButtons.saveFilterBtn);

        var features = group1.length ? [{ buttons: group1 }] : [];

        // Only emit the secondary group when it actually has buttons. An empty group (e.g. a
        // table with skipColVis and no filter/save button, like Admin Users) would otherwise
        // render an empty .dt-buttons container that adds a phantom gap between the Action
        // dropdown and the Add New button via the toolbar's column-gap.
        if (group2.length) {
            features.push({ buttons: group2 });
        }

        // Extra array buttons (usually custom actions)
        if (Array.isArray(extraButtons)) {
            features.push({ buttons: extraButtons });
        }

        // Add New button stays in its own group as a primary action
        if (addNewText) {
            features.push({
                buttons: [{
                    text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-plus icon-sm"></i><span class="d-none d-sm-inline-block">' + addNewText + '</span></span>',
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
        // Scope to THIS table's container. On pages with multiple DataTables (e.g. a Details
        // page with several sub-tab tables) a global selector would paint one table's badges
        // and active states onto every other table's filter/colvis/search controls.
        var $scope;
        try {
            $scope = $(api.table().container());
        } catch (e) {
            $scope = $(document);
        }
        if (!$scope.length) $scope = $(document);

        // 1. Filter Button Sync
        var $filterBtn = $scope.find('.dt-filter-btn');
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
        var $colvisBtn = $scope.find('.dt-colvis-btn');
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
        var $searchWrapper = $scope.find('.dt-search');
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

    // Tables built without create() (a page calling `new DataTable` directly) are covered from the moment this file
    // loads; create() installs it again in case the library arrived after this file.
    installErrMode();

    return {
        create: create,
        renderLoadError: renderLoadError,
        hasLoadError: hasLoadError,
        exportButtons: exportButtons,
        controlledCopy: {
            build: buildControlledCopy,
            docDefinition: controlledCopyDocDefinition,
            writePrint: writeControlledPrint,
            pdfNeedsBrowserPrint: pdfNeedsBrowserPrint,
            run: runControlledCopy,
            get last() { return _lastControlledCopy; }
        },
        responsiveRenderer: responsiveRenderer,
        updateVisualState: updateVisualState,
        refreshButtonGroupRadii: refreshButtonGroupRadii,
        handleUnauthorized: refreshTokenAndReload
    };
})();
