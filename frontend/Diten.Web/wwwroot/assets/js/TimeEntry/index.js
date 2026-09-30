/*
 * MOD-0280-FU01 T2a — My Timesheet (/TimeEntry), the page's DOM half.
 *
 * Pack §21.2: a rows × Mon–Sun grid with a daily target row and totals on a desktop, one day's list with a day switcher
 * on a phone (< 768 px, CSS decides which one shows); suggestions INSIDE their day; an Actions menu (copy previous week,
 * fill from plan, review suggestions); a secondary side panel (timer notices, "too short to count", the correction of a
 * captured row, the row notes); submit / withdraw / correction / rejected band; the approval history strip.
 *
 * NO BUSINESS RULE HERE (see core.js): the server says what a day's target is, which day is in the future, which day is
 * flagged, whether the week is editable, and refuses what it refuses — the page shows its reason code as a sentence.
 * No inline style anywhere (FG-003): state is a class or the `hidden` attribute.
 */
(function (root) {
    'use strict';

    var doc = root.document;
    var core = root.TimeEntryCore;
    var api = root.TimeEntryApi;

    var L = {};
    var state = null;

    // ── l10n ───────────────────────────────────────────────────────────────────────────────────────────────────

    function readL10n() {
        var node = doc.getElementById('time-entry-l10n');
        try { return node ? JSON.parse(node.textContent || '{}') : {}; } catch (error) { return {}; }
    }

    /** The limits a sentence may name ({step}, {maxRowHours}, {maxDayHours}, {noteMax}) — from the payload's Limits, never typed
     * into a translation (v3 L10). */
    function limitValues() {
        var limits = L.Limits || {};
        var hours = function (minutes) { return minutes ? String(Math.round(minutes / 6) / 10) : ''; };
        return {
            step: limits.StepMinutes || '',
            maxRowHours: hours(limits.MaxRowMinutes),
            maxDayHours: hours(limits.ImplausibleDayMinutes),
            noteMax: limits.NoteMaxLength || ''
        };
    }

    function t(key, values) {
        var text = Object.prototype.hasOwnProperty.call(L, key) && L[key] ? String(L[key]) : key;
        values = Object.assign(limitValues(), values || {});
        if (values) {
            Object.keys(values).forEach(function (name) {
                text = text.split('{' + name + '}').join(values[name] === null || values[name] === undefined ? '' : String(values[name]));
            });
        }
        return text;
    }

    function lang() {
        return (doc.documentElement.getAttribute('lang') || 'en');
    }

    function isRtl() {
        return (doc.documentElement.getAttribute('dir') || '').toLowerCase() === 'rtl';
    }

    function formatDate(isoDate, options) {
        try {
            return new Intl.DateTimeFormat(lang(), Object.assign({ timeZone: 'UTC' }, options))
                .format(new Date(isoDate + 'T00:00:00Z'));
        } catch (error) {
            return isoDate;
        }
    }

    function formatInstant(iso) {
        if (!iso) {
            return '';
        }
        try {
            return new Intl.DateTimeFormat(lang(), {
                dateStyle: 'medium', timeStyle: 'short', timeZone: (state && state.payload && state.payload.timeZoneId) || undefined
            }).format(new Date(iso));
        } catch (error) {
            return String(iso);
        }
    }

    // ── small DOM helpers ──────────────────────────────────────────────────────────────────────────────────────

    function el(tag, className, text) {
        var node = doc.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined && text !== null) { node.textContent = text; }
        return node;
    }

    function button(className, text, onClick, attrs) {
        var node = el('button', className, text);
        node.type = 'button';
        if (attrs) {
            Object.keys(attrs).forEach(function (name) { node.setAttribute(name, attrs[name]); });
        }
        if (onClick) { node.addEventListener('click', onClick); }
        return node;
    }

    function clear(node) {
        while (node && node.firstChild) { node.removeChild(node.firstChild); }
        return node;
    }

    function byId(id) {
        return doc.getElementById(id);
    }

    function toast(message, type) {
        if (typeof root.showToast === 'function') {
            root.showToast(message, type || 'success');
        }
    }

    function confirmThen(title, onYes) {
        if (typeof root.showConfirm === 'function') {
            root.showConfirm(title, onYes);
        } else {
            onYes();
        }
    }

    /**
     * The page's status line: VISIBLE (v3 M1) and aria-live, so a rounding, a refusal or a copy result is both seen and
     * read out. `kind` is info (default) or warning.
     */
    function announce(message, kind) {
        var line = byId('teNotice');
        if (!line) { return; }
        // The region itself always stays in the page (a live region that appears together with its text is often not
        // read out); an empty one takes no room (CSS :empty).
        line.textContent = message || '';
        line.className = 'time-entry-notice time-entry-notice-' + (kind || 'info');
    }

    // ── labels ─────────────────────────────────────────────────────────────────────────────────────────────────

    function categoryLabel(code) {
        var category = (state.categories || []).filter(function (c) { return c.code === code; })[0];
        if (category) {
            if (category.labelText) { return category.labelText; }
            var labels = L.CategoryLabels || {};
            if (category.labelResourceKey && labels[category.labelResourceKey]) { return labels[category.labelResourceKey]; }
        }
        var fallback = (L.CategoryLabels || {})['TimeEntry.Category.' + code];
        return fallback || code;
    }

    function targetLabel(taskItemId, categoryCode, taskTitle) {
        if (taskItemId) {
            return taskTitle || t('UnreadableTask');
        }
        return categoryLabel(categoryCode);
    }

    function rowLabel(row) {
        return targetLabel(row.taskItemId, row.categoryCode, row.taskTitle);
    }

    /** v3 M4 — a row without time is not saved; it says so on itself. */
    function pendingBadge() {
        var badge = el('span', 'badge time-entry-pending', t('PendingRow'));
        badge.setAttribute('title', t('PendingRowHint'));
        return badge;
    }

    function sourceBadge(source) {
        return el('span', 'badge time-entry-source time-entry-source-' + String(source).toLowerCase(), t('Source' + source));
    }

    // ── state ──────────────────────────────────────────────────────────────────────────────────────────────────

    function dates() {
        return (state.payload && state.payload.days || []).map(function (day) { return day.date; });
    }

    function dayOf(date) {
        return (state.payload.days || []).filter(function (day) { return day.date === date; })[0] || null;
    }

    function editable() {
        return !!(state.canUpdate && state.payload && state.payload.editable);
    }

    function cellEditable(row, date) {
        var day = dayOf(date);
        return editable() && core.isPersonTyped(row.source) && !!day && !day.isFuture;
    }

    function dirty() {
        return core.isDirty(state.rows);
    }

    function pending() {
        return core.pendingRows(state.rows);
    }

    /** What leaving the week would lose, as the one sentence to ask about — or null when nothing is lost (v3 M3/M4). */
    function leaveQuestion() {
        var unsaved = dirty();
        var empty = pending().length > 0;
        if (unsaved && empty) { return t('LeaveDirtyAndPendingConfirm'); }
        if (unsaved) { return t('DiscardChangesConfirm'); }
        if (empty) { return t('PendingRowsLeaveConfirm'); }
        return null;
    }

    function rowByKey(key) {
        return state.rows.filter(function (row) { return row.key === key; })[0] || null;
    }

    // ── loading ────────────────────────────────────────────────────────────────────────────────────────────────

    function setUrlWeek(weekKey, push) {
        if (!root.history || !root.history.pushState) { return; }
        var url = root.location.pathname + '?week=' + encodeURIComponent(weekKey);
        try {
            if (push) { root.history.pushState({ week: weekKey }, '', url); }
            else { root.history.replaceState({ week: weekKey }, '', url); }
        } catch (error) { /* a sandboxed frame — the page still works, the address just does not follow */ }
    }

    function load(weekKey, options) {
        var opts = options || {};
        state.loading = true;
        return api.getWeek(weekKey).then(function (result) {
            state.loading = false;
            if (!result.ok) {
                if (result.status === 403) {
                    renderAccessDenied();
                    return result;
                }
                renderLoadFailure(core.failureMessage(result, t));
                return result;
            }

            // The first load guessed the week from the browser's date; the server's "today" is the tenant's.
            if (opts.followServerToday && result.data && result.data.localToday) {
                var serverWeek = core.weekKeyOfDate(result.data.localToday);
                if (serverWeek && serverWeek !== weekKey) {
                    return load(serverWeek, { replaceUrl: true });
                }
            }

            var sameWeek = state.weekKey === (result.data.weekKey || weekKey);
            // CT acceptance: only an EDITABLE week keeps its pending rows — a submitted or approved week cannot be filled,
            // and rows it cannot take would keep the leave warnings firing with no way out.
            var keep = sameWeek && result.data && result.data.editable ? core.pendingRows(state.rows) : [];
            state.weekKey = result.data.weekKey || weekKey;
            state.payload = result.data;
            announce('');
            // v3 M4 — rows without time exist only on the page; a reload of the SAME week (after a save, an accept)
            // puts them back instead of dropping them without a word.
            state.rows = core.mergePending(core.buildRows(result.data), keep);
            state.ghosts = {};
            state.panel = null;
            state.review = false;
            if (!state.selectedDay || dates().indexOf(state.selectedDay) === -1) {
                state.selectedDay = dates().indexOf(result.data.localToday) !== -1 ? result.data.localToday : dates()[0];
            }
            if (opts.pushUrl) { setUrlWeek(state.weekKey, true); }
            else if (opts.replaceUrl) { setUrlWeek(state.weekKey, false); }
            render();
            return result;
        });
    }

    function reload() {
        return load(state.weekKey);
    }

    function goToWeek(weekKey) {
        var go = function () { load(weekKey, { pushUrl: true }); };
        var question = leaveQuestion();
        if (question) { confirmThen(question, go); } else { go(); }
    }

    /** Back/forward to another week. With something to lose, the address is put back first and the page asks; only a
     * "yes" moves on (the in-page confirmation — never a browser prompt). */
    function onPopState(event) {
        if (root.TimeEntryPage !== exported) { return; }
        var week = event.state && event.state.week;
        if (!week || !core.isWeekKey(week) || week === state.weekKey) { return; }
        var question = leaveQuestion();
        if (!question) {
            load(week);
            return;
        }
        setUrlWeek(state.weekKey, true);
        confirmThen(question, function () { load(week, { pushUrl: true }); });
    }

    /** Closing or leaving the page with something to lose: the browser's own leave warning (the only native one). */
    function onBeforeUnload(event) {
        if (root.TimeEntryPage !== exported) { return undefined; }
        // CT acceptance: the browser's own warning is for UNSAVED EDITS only. Rows without time are said by the in-page
        // leave-week question; the native prompt on every sidebar link, logout or language switch was noise.
        if (!state || !state.payload || !dirty()) { return undefined; }
        event.preventDefault();
        event.returnValue = '';
        return '';
    }

    // ── writes ─────────────────────────────────────────────────────────────────────────────────────────────────

    function fail(result) {
        toast(core.failureMessage(result, t), 'error');
        return result;
    }

    function save(options) {
        var opts = options || {};
        var entries = core.buildSaveEntries(state.rows);
        return api.saveEntries(state.weekKey, state.payload.version, entries).then(function (result) {
            if (!result.ok) { return fail(result); }
            announce('');
            if (!opts.quiet) { toast(t('Saved'), 'success'); }
            return opts.noReload ? result : reload().then(function () { return result; });
        });
    }

    function saveIfDirty() {
        if (!dirty()) { return Promise.resolve({ ok: true }); }
        return save({ quiet: true });
    }

    function submitWeek() {
        confirmThen(t('SubmitConfirm'), function () {
            saveIfDirty().then(function (saved) {
                if (!saved.ok) { return; }
                return api.submit(state.weekKey, state.payload.version).then(function (result) {
                    if (!result.ok) { return fail(result); }
                    toast(t('Submitted'), 'success');
                    return reload();
                });
            });
        });
    }

    function withdrawWeek() {
        confirmThen(t('WithdrawConfirm'), function () {
            api.withdraw(state.weekKey, state.payload.version).then(function (result) {
                if (!result.ok) { return fail(result); }
                toast(t('Withdrawn'), 'success');
                return reload();
            });
        });
    }

    function requestCorrection(reason) {
        var text = (reason || '').trim();
        var error = byId('teCorrectionError');
        if (!text) {
            if (error) { error.textContent = t('ErrCorrectionReasonRequired'); error.hidden = false; }
            return Promise.resolve({ ok: false });
        }
        return api.requestCorrection(state.weekKey, text).then(function (result) {
            if (!result.ok) {
                if (error) { error.textContent = core.failureMessage(result, t); error.hidden = false; }
                return result;
            }
            state.correctionOpen = false;
            toast(t('CorrectionStarted'), 'success');
            return reload();
        });
    }

    function discardCorrection() {
        confirmThen(t('DiscardCorrectionConfirm'), function () {
            api.discardCorrection(state.weekKey).then(function (result) {
                if (!result.ok) { return fail(result); }
                return reload();
            });
        });
    }

    function acceptSuggestion(suggestion) {
        return saveIfDirty().then(function (saved) {
            if (!saved.ok) { return saved; }
            return api.acceptSuggestion(state.weekKey, suggestion.id, state.payload.version).then(function (result) {
                if (!result.ok) { return fail(result); }
                return reload().then(function () { return result; });
            });
        });
    }

    function dismissSuggestion(suggestion) {
        return saveIfDirty().then(function (saved) {
            if (!saved.ok) { return saved; }
            return api.dismissSuggestion(state.weekKey, suggestion.id).then(function (result) {
                if (!result.ok) { return fail(result); }
                return reload();
            });
        });
    }

    // ── actions menu ───────────────────────────────────────────────────────────────────────────────────────────

    function ensureCategories() {
        if (state.categoriesLoaded) { return Promise.resolve(state.categories); }
        return api.categories().then(function (result) {
            state.categoriesLoaded = true;
            state.categories = result.ok && Array.isArray(result.data) ? result.data : [];
            return state.categories;
        });
    }

    function copyPreviousWeek() {
        var previousKey = core.shiftWeek(state.weekKey, -1);
        return Promise.all([api.getWeek(previousKey), ensureCategories()]).then(function (results) {
            var previous = results[0];
            if (!previous.ok) { return fail(previous); }
            var active = state.categories.filter(function (c) { return c.isActive !== false; }).map(function (c) { return c.code; });
            var added = core.copyRowsFromWeek(state.rows, previous.data, active);
            announce(added.length ? t('CopiedRows', { count: added.length }) : t('NothingToCopy'));
            render();
            return added;
        });
    }

    function fillFromPlan() {
        return api.planFillIn(state.weekKey).then(function (result) {
            if (!result.ok) { return fail(result); }
            state.ghosts = core.planGhosts(state.rows, result.data && result.data.rows);
            announce(Object.keys(state.ghosts).length ? t('PlanValuesShown') : t('NoPlanValues'));
            render();
            return result;
        });
    }

    function acceptGhost(ghostKey) {
        var ghost = state.ghosts[ghostKey];
        if (!ghost) { return; }
        core.acceptPlanGhost(state.rows, ghost);
        delete state.ghosts[ghostKey];
        render();
    }

    function openReview() {
        state.review = true;
        render();
    }

    /** "Review suggestions": the ticked plan values go into the draft and are saved; the ticked meetings are accepted
     * one after the other (each accept moves the week's version on). */
    function addSelectedSuggestions() {
        var form = byId('teReview');
        if (!form) { return Promise.resolve(); }
        var ticked = Array.prototype.slice.call(form.querySelectorAll('input[type=checkbox]:checked'));
        var ghostKeys = ticked.filter(function (box) { return box.getAttribute('data-kind') === 'plan'; })
            .map(function (box) { return box.value; });
        var meetingIds = ticked.filter(function (box) { return box.getAttribute('data-kind') === 'meeting'; })
            .map(function (box) { return box.value; });

        ghostKeys.forEach(function (key) {
            if (state.ghosts[key]) {
                core.acceptPlanGhost(state.rows, state.ghosts[key]);
                delete state.ghosts[key];
            }
        });

        var saveFailed = false;
        var chain = dirty() ? save({ quiet: true, noReload: true }).then(function (r) {
            if (!r.ok) {
                // v3 M3 — the save was refused: NO reload. The person's edits (and the plan values just taken) stay on
                // the page, and the page says so; the server's own reason was already shown.
                saveFailed = true;
                return r;
            }
            return reload();
        }) : Promise.resolve({ ok: true });

        meetingIds.forEach(function (id) {
            chain = chain.then(function (previous) {
                if (previous && previous.ok === false) { return previous; }
                return api.acceptSuggestion(state.weekKey, id, state.payload.version).then(function (result) {
                    if (!result.ok) { return fail(result); }
                    if (result.data && typeof result.data.weekVersion === 'number') { state.payload.version = result.data.weekVersion; }
                    return result;
                });
            });
        });

        return chain.then(function () {
            state.review = false;
            if (saveFailed) {
                render();
                announce(t('SaveFailedKept'), 'warning');
                return { ok: false };
            }
            return reload();
        });
    }

    // ── row add ────────────────────────────────────────────────────────────────────────────────────────────────

    function addTaskRow(option) {
        if (!option || !option.taskItemId) { return null; }
        var exists = state.rows.filter(function (row) {
            return row.source === 'Manual' && row.taskItemId === option.taskItemId;
        })[0];
        if (exists) { return exists; }
        var row = core.newRow('Manual', option.taskItemId, null, option.title);
        state.rows.push(row);
        state.picker = null;
        render();
        return row;
    }

    function addCategoryRow(code) {
        if (!code) { return null; }
        var exists = state.rows.filter(function (row) {
            return row.source === 'Manual' && !row.taskItemId && row.categoryCode === code;
        })[0];
        if (exists) { return exists; }
        var row = core.newRow('Manual', null, code, null);
        state.rows.push(row);
        state.picker = null;
        render();
        return row;
    }

    /** The task picker: the options come from the server (own open tasks + recent ones the person can still read, at
     * most 50, CT v2 decision 5). A native select — select2 dresses it when the shell has it; no new dialog type. */
    function openTaskPicker() {
        return api.taskOptions('').then(function (result) {
            if (!result.ok) { return fail(result); }
            state.picker = { kind: 'task', options: Array.isArray(result.data) ? result.data : [] };
            render();
            return result;
        });
    }

    function openCategoryPicker() {
        return ensureCategories().then(function () {
            state.picker = { kind: 'category', options: state.categories.filter(function (c) { return c.isActive !== false; }) };
            render();
        });
    }

    // ── cell input ─────────────────────────────────────────────────────────────────────────────────────────────

    /** Commits what the person typed into (row, date). Returns true when the value was taken. */
    function commitInput(input) {
        var row = rowByKey(input.getAttribute('data-row-key'));
        var date = input.getAttribute('data-date');
        if (!row || !date) { return false; }
        var parsed = core.parseDuration(input.value);
        var current = row.cells[date] ? Number(row.cells[date].minutes) || 0 : 0;
        if (!parsed.ok) {
            input.classList.add('is-invalid');
            input.setAttribute('aria-invalid', 'true');
            if (parsed.tooSmall) {
                // v3 M1 — a value that rounds to nothing never empties the cell: it keeps its value, and the page says why.
                input.value = core.formatMinutes(current);
                announce(t('NoticeTooSmall', { typed: String(parsed.typed) }), 'warning');
            } else {
                announce(t('InvalidDuration'), 'warning');
            }
            return false;
        }
        input.classList.remove('is-invalid');
        input.removeAttribute('aria-invalid');
        if (parsed.minutes !== current) {
            core.setCell(row, date, parsed.minutes);
        }
        if (parsed.bareHours) {
            announce(t('BareHoursRead', { typed: input.value.trim(), value: core.formatMinutes(parsed.minutes) }));
        } else if (parsed.rounded) {
            announce(t('RoundedTo', { value: core.formatMinutes(parsed.minutes) || '0:00' }));
        } else {
            announce('');
        }
        return true;
    }

    function focusCell(position) {
        var target = doc.querySelector('#teGrid [data-pos="' + position.row + ':' + position.col + '"]');
        if (target && typeof target.focus === 'function') {
            target.focus();
            if (typeof target.select === 'function') { target.select(); }
        }
        return target;
    }

    function onGridKeydown(event) {
        var input = event.target;
        if (!input || !input.hasAttribute || !input.hasAttribute('data-pos')) { return; }
        var key = event.key;
        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Enter'].indexOf(key) === -1) { return; }
        event.preventDefault();
        var parts = input.getAttribute('data-pos').split(':');
        var position = { row: Number(parts[0]), col: Number(parts[1]) };
        if (input.tagName === 'INPUT') {
            commitInput(input);
        }
        var size = { rows: state.gridRowCount || 1, cols: dates().length };
        var next = core.nextCell(position, key, size, isRtl());
        rerenderKeepingFocus(next);
    }

    /** Which cell or control holds the focus, as something that survives a rebuild of the grid. */
    function focusIdentity(node) {
        if (!node || !node.getAttribute) { return null; }
        var host = node.closest && node.closest('#teGrid') ? 'grid' : node.closest && node.closest('#teDayView') ? 'day' : null;
        if (node.hasAttribute('data-row-key') && node.hasAttribute('data-date')) {
            return { host: host, rowKey: node.getAttribute('data-row-key'), date: node.getAttribute('data-date') };
        }
        if (node.hasAttribute('data-day')) { return { host: host, day: node.getAttribute('data-day') }; }
        if (node.id) { return { id: node.id }; }
        return null;
    }

    function refocus(identity) {
        if (!identity) { return null; }
        var target = null;
        if (identity.id) {
            target = byId(identity.id);
        } else if (identity.rowKey) {
            var scope = identity.host === 'day' ? '#teDayView' : '#teGrid';
            target = Array.prototype.slice.call(doc.querySelectorAll(scope + ' [data-row-key][data-date]')).filter(function (n) {
                return n.getAttribute('data-row-key') === identity.rowKey && n.getAttribute('data-date') === identity.date;
            })[0] || null;
        } else if (identity.day) {
            target = doc.querySelector('#teDayView [data-day="' + identity.day + '"]');
        }
        if (target && typeof target.focus === 'function') {
            target.focus();
            // CT acceptance: select like focusCell does — typing after Tab REPLACES the value ("1:00" + "2" was "21:00").
            if (typeof target.select === 'function') { target.select(); }
        }
        return target;
    }

    /**
     * v3 M2 — after a cell's value is taken, the grid is rebuilt; the rebuild waits until the browser has moved the focus
     * (Tab, Shift+Tab, a click) and then puts it back on the SAME cell or control in the new DOM. Without this the person's
     * next cell vanishes under them.
     */
    var renderTimer = null;
    function renderKeepingFocus() {
        if (renderTimer) { root.clearTimeout(renderTimer); }
        renderTimer = root.setTimeout(function () {
            renderTimer = null;
            var identity = focusIdentity(doc.activeElement);
            render();
            refocus(identity);
        }, 0);
    }

    function rerenderKeepingFocus(position) {
        render();
        if (position) { focusCell(position); }
    }

    // ── render: header, banner, history ────────────────────────────────────────────────────────────────────────

    function statusKey(payload) {
        if (payload.status === 'Draft' && payload.correctionOfRevision) { return 'StatusCorrection'; }
        if (core.isRejectedNow(payload)) { return 'StatusRejected'; }
        return 'Status' + payload.status;
    }

    function renderHeader() {
        var payload = state.payload;
        var monday = payload.weekStartDate;
        var sunday = core.addDays(monday, 6);
        byId('teWeekLabel').textContent = t('WeekLabel', {
            week: payload.weekKey,
            from: formatDate(monday, { day: 'numeric', month: 'short' }),
            to: formatDate(sunday, { day: 'numeric', month: 'short', year: 'numeric' })
        });

        var pill = byId('teStatus');
        var key = statusKey(payload);
        pill.textContent = t(key);
        pill.className = 'badge rounded-pill time-entry-status time-entry-status-' + key.replace('Status', '').toLowerCase();
        pill.hidden = false;

        var missing = core.missingDays(payload);
        var missingNode = byId('teMissingDays');
        missingNode.textContent = missing.length ? t('MissingDays', { count: missing.length }) : '';
        missingNode.hidden = missing.length === 0;
        if (missing.length) {
            missingNode.setAttribute('title', missing.map(function (d) { return formatDate(d, { weekday: 'short', day: 'numeric' }); }).join(', '));
        }

        var actions = byId('teActionsMenu');
        if (actions) { actions.hidden = !editable(); }

        var prev = byId('tePrevWeek');
        var next = byId('teNextWeek');
        // The chevrons point where the week goes as the reader sees it — reversed in a right-to-left page.
        prev.querySelector('i').className = 'bx ' + (isRtl() ? 'bx-chevron-right' : 'bx-chevron-left');
        next.querySelector('i').className = 'bx ' + (isRtl() ? 'bx-chevron-left' : 'bx-chevron-right');
    }

    function renderBanner() {
        var payload = state.payload;
        var host = clear(byId('teBanner'));
        var add = function (kind, text, id) {
            var node = el('div', 'alert time-entry-banner time-entry-banner-' + kind, text);
            node.setAttribute('role', kind === 'rejected' || kind === 'blocked' ? 'alert' : 'status');
            if (id) { node.id = id; }
            host.appendChild(node);
            return node;
        };

        if (core.isRejectedNow(payload) && payload.lastRejectionReason) {
            add('rejected', t('RejectedBand', {
                name: payload.lastRejectedByDisplayName || t('UnknownPerson'),
                when: formatInstant(payload.lastRejectedAtUtc),
                reason: payload.lastRejectionReason
            }), 'teRejectedBand');
        }
        if (payload.status === 'Submitted') {
            add('submitted', payload.assignedApproverDisplayName
                ? t('WaitingApprovalOf', { name: payload.assignedApproverDisplayName })
                : t('WaitingApprovalUnnamed'), 'teWaitingBand');
        }
        if (payload.status === 'Approved') {
            add('approved', t('ApprovedBand'));
        }
        if (payload.correctionOfRevision) {
            add('correction', t('CorrectionBand', {
                revision: payload.correctionOfRevision, reason: payload.correctionReason || ''
            }));
        }
        if (payload.reopenActive) {
            add('reopened', t('ReopenedBand', { reason: payload.reopenReason || '' }));
        }
        if (payload.status === 'Draft' && !payload.editable && payload.notEditableReason) {
            add('blocked', core.codeMessage(payload.notEditableReason, t) || t('ErrGeneric'));
        }
        if (payload.finalizationBlockedReason) {
            add('blocked', core.codeMessage(payload.finalizationBlockedReason, t) || t('ErrGeneric'));
        }
    }

    function renderHistory() {
        var payload = state.payload;
        var host = clear(byId('teHistory'));
        var events = [];
        if (payload.submittedAtUtc) {
            events.push({ at: payload.submittedAtUtc, kind: 'submitted', text: t('HistorySubmitted', { name: payload.submittedByDisplayName || t('UnknownPerson') }) });
        }
        if (payload.lastRejectedAtUtc) {
            events.push({ at: payload.lastRejectedAtUtc, kind: 'rejected', text: t('HistoryRejected', { name: payload.lastRejectedByDisplayName || t('UnknownPerson') }) });
        }
        if (payload.approvedAtUtc) {
            events.push({ at: payload.approvedAtUtc, kind: 'approved', text: t('HistoryApproved', { name: payload.approvedByDisplayName || t('UnknownPerson') }) });
        }
        events.sort(function (a, b) { return String(a.at) < String(b.at) ? -1 : 1; });
        host.hidden = events.length === 0;
        if (!events.length) { return; }
        host.appendChild(el('span', 'time-entry-history-title', t('HistoryTitle')));
        var list = el('ol', 'time-entry-history-list');
        events.forEach(function (event) {
            var item = el('li', 'time-entry-history-item time-entry-history-' + event.kind);
            item.appendChild(el('span', 'time-entry-history-text', event.text));
            item.appendChild(el('time', 'time-entry-history-when', formatInstant(event.at)));
            list.appendChild(item);
        });
        host.appendChild(list);
    }

    // ── render: the grid (desktop) ─────────────────────────────────────────────────────────────────────────────

    function dayHeaderClass(day) {
        var classes = ['time-entry-day'];
        if (day.dayKind === 'weekend') { classes.push('time-entry-day-weekend'); }
        if (day.dayKind === 'holiday' || day.holidayName) { classes.push('time-entry-day-holiday'); }
        if (day.isHalfDay) { classes.push('time-entry-day-halfday'); }
        if (day.isFuture) { classes.push('time-entry-day-future'); }
        if (state.payload.localToday === day.date) { classes.push('time-entry-day-today'); }
        return classes.join(' ');
    }

    function cellNode(row, date, rowIndex, colIndex) {
        var cell = row.cells[date];
        var minutes = cell ? Number(cell.minutes) || 0 : 0;
        var td = el('td', dayHeaderClass(dayOf(date)) + ' time-entry-cell');

        if (cellEditable(row, date)) {
            var input = el('input', 'form-control form-control-sm time-entry-cell-input');
            input.type = 'text';
            input.inputMode = 'decimal';
            input.value = core.formatMinutes(minutes);
            input.setAttribute('data-row-key', row.key);
            input.setAttribute('data-date', date);
            input.setAttribute('data-pos', rowIndex + ':' + colIndex);
            input.setAttribute('aria-label', t('CellAria', { row: rowLabel(row), day: formatDate(date, { weekday: 'long', day: 'numeric', month: 'long' }) }));
            input.addEventListener('change', function () { if (commitInput(input)) { renderKeepingFocus(); } });
            td.appendChild(input);
        } else if (core.isCaptured(row.source) && cell) {
            var value = button('btn btn-sm time-entry-cell-captured', core.formatMinutes(minutes) || '0:00', function () {
                state.panel = { kind: 'correct', rowKey: row.key, date: date };
                render();
            }, { 'data-pos': rowIndex + ':' + colIndex, 'data-row-key': row.key, 'data-date': date });
            if (!editable()) { value.disabled = true; }
            td.appendChild(value);
        } else {
            td.appendChild(el('span', 'time-entry-cell-value', core.formatMinutes(minutes)));
        }

        if (cell && (cell.editedFromTimer || (cell.capturedMinutes !== null && cell.capturedMinutes !== undefined))) {
            td.appendChild(el('span', 'time-entry-mark time-entry-mark-edited', t('MarkEdited')));
        }
        if (cell && cell.outsideWorkingMinutes > 0) {
            td.appendChild(el('span', 'time-entry-mark time-entry-mark-outside', t('MarkOutsideHours', { value: core.formatMinutes(cell.outsideWorkingMinutes) })));
        }
        if (cell && cell.minutesConflict) {
            td.appendChild(el('span', 'time-entry-mark time-entry-mark-conflict', t('MarkMinutesConflict')));
        }

        var ghost = row.taskItemId && state.ghosts[row.taskItemId + '|' + date];
        if (ghost && minutes === 0 && editable()) {
            td.appendChild(ghostButton(row.taskItemId + '|' + date, ghost));
        }
        return td;
    }

    function ghostButton(ghostKey, ghost) {
        return button('btn btn-sm time-entry-ghost', core.formatMinutes(ghost.minutes), function () { acceptGhost(ghostKey); }, {
            'data-ghost': ghostKey, 'title': t('AcceptPlanValue'), 'aria-label': t('AcceptPlanValue')
        });
    }

    function rowHeader(row) {
        var th = el('th', 'time-entry-row-label');
        th.setAttribute('scope', 'row');
        var label = el('span', row.taskItemId && !row.taskTitle ? 'time-entry-row-title time-entry-row-title-unreadable' : 'time-entry-row-title', rowLabel(row));
        th.appendChild(label);
        th.appendChild(sourceBadge(row.source));
        if (core.isPendingRow(row)) { th.appendChild(pendingBadge()); }
        var hasNote = Object.keys(row.cells).some(function (d) { return (row.cells[d].note || '').trim() !== ''; });
        var note = button('btn btn-sm btn-icon time-entry-note-toggle' + (hasNote ? ' time-entry-note-present' : ''), null, function () {
            state.panel = { kind: 'note', rowKey: row.key };
            render();
        }, { 'aria-label': t('RowNote'), 'title': t('RowNote') });
        note.appendChild(el('i', 'bx bx-note'));
        th.appendChild(note);
        return th;
    }

    function renderGrid() {
        var host = clear(byId('teGrid'));
        var days = state.payload.days || [];
        var table = el('table', 'table time-entry-grid');
        var head = el('thead');
        var headRow = el('tr');
        headRow.appendChild(el('th', 'time-entry-row-label', t('ColumnWork')));
        days.forEach(function (day) {
            var th = el('th', dayHeaderClass(day));
            th.setAttribute('scope', 'col');
            th.appendChild(el('span', 'time-entry-day-name', formatDate(day.date, { weekday: 'short' })));
            th.appendChild(el('span', 'time-entry-day-date', formatDate(day.date, { day: 'numeric', month: 'short' })));
            if (day.holidayName) {
                th.appendChild(el('span', 'time-entry-day-holiday-name', day.holidayName));
            }
            headRow.appendChild(th);
        });
        headRow.appendChild(el('th', 'time-entry-total-col', t('ColumnTotal')));
        head.appendChild(headRow);
        table.appendChild(head);

        var body = el('tbody');
        var rowIndex = 0;
        state.rows.forEach(function (row) {
            var tr = el('tr', 'time-entry-row' + (core.isPendingRow(row) ? ' time-entry-row-pending' : ''));
            tr.setAttribute('data-row-key', row.key);
            tr.appendChild(rowHeader(row));
            days.forEach(function (day, colIndex) {
                tr.appendChild(cellNode(row, day.date, rowIndex, colIndex));
            });
            tr.appendChild(el('td', 'time-entry-total-col', core.formatMinutes(core.rowTotal(row))));
            body.appendChild(tr);
            rowIndex += 1;
        });
        state.gridRowCount = rowIndex;

        // Plan values for tasks that have no row yet: a grey row of their own, still one cell at a time.
        Object.keys(state.ghosts).forEach(function (ghostKey) {
            var ghost = state.ghosts[ghostKey];
            if (state.rows.some(function (row) { return row.taskItemId === ghost.taskItemId; })) { return; }
            var tr = el('tr', 'time-entry-row time-entry-row-ghost');
            var th = el('th', 'time-entry-row-label');
            th.appendChild(el('span', 'time-entry-row-title', targetLabel(ghost.taskItemId, null, ghost.taskTitle)));
            th.appendChild(sourceBadge('Plan'));
            tr.appendChild(th);
            days.forEach(function (day) {
                var td = el('td', dayHeaderClass(day) + ' time-entry-cell');
                if (day.date === ghost.date && editable()) { td.appendChild(ghostButton(ghostKey, ghost)); }
                tr.appendChild(td);
            });
            tr.appendChild(el('td', 'time-entry-total-col'));
            body.appendChild(tr);
        });

        // Meeting suggestions sit INSIDE their day (U3): one grey row per meeting, accepted or declined from the cell.
        var byDay = core.openSuggestionsByDay(state.payload);
        Object.keys(byDay).forEach(function (date) {
            byDay[date].forEach(function (suggestion) {
                var tr = el('tr', 'time-entry-row time-entry-row-suggestion');
                tr.setAttribute('data-suggestion-id', suggestion.id);
                var th = el('th', 'time-entry-row-label');
                th.appendChild(el('span', 'time-entry-row-title', suggestion.title));
                th.appendChild(sourceBadge('Meeting'));
                if (suggestion.minutesStatus === 'confirmed') {
                    th.appendChild(el('span', 'badge time-entry-mark time-entry-mark-confirmed', t('ConfirmedByMinutes')));
                }
                tr.appendChild(th);
                days.forEach(function (day) {
                    var td = el('td', dayHeaderClass(day) + ' time-entry-cell');
                    if (day.date === date) { td.appendChild(suggestionControls(suggestion)); }
                    tr.appendChild(td);
                });
                tr.appendChild(el('td', 'time-entry-total-col'));
                body.appendChild(tr);
            });
        });

        if (!state.rows.length && !Object.keys(state.ghosts).length && !Object.keys(byDay).length) {
            var empty = el('tr', 'time-entry-empty');
            var td = el('td', 'time-entry-empty-cell', t('EmptyWeek'));
            td.colSpan = days.length + 2;
            empty.appendChild(td);
            body.appendChild(empty);
        }
        table.appendChild(body);

        var totals = core.dayTotals(state.rows, dates());
        var foot = el('tfoot');
        var totalRow = el('tr', 'time-entry-totals');
        totalRow.appendChild(el('th', 'time-entry-row-label', t('RowTotal')));
        var weekTotal = 0;
        days.forEach(function (day) {
            var td = el('td', dayHeaderClass(day) + ' time-entry-day-total', core.formatMinutes(totals[day.date]) || '0:00');
            if (day.isFlagged) {
                td.classList.add('time-entry-day-flagged');
                td.appendChild(el('span', 'time-entry-mark time-entry-mark-flag', t('MarkFlaggedDay')));
            }
            td.setAttribute('data-date', day.date);
            weekTotal += totals[day.date];
            totalRow.appendChild(td);
        });
        totalRow.appendChild(el('td', 'time-entry-total-col', core.formatMinutes(weekTotal) || '0:00'));
        foot.appendChild(totalRow);

        var targetRow = el('tr', 'time-entry-targets');
        targetRow.appendChild(el('th', 'time-entry-row-label', t('RowTarget')));
        days.forEach(function (day) {
            var td = el('td', dayHeaderClass(day) + ' time-entry-day-target', day.targetMinutes > 0 ? core.formatMinutes(day.targetMinutes) : '—');
            td.setAttribute('data-date', day.date);
            targetRow.appendChild(td);
        });
        targetRow.appendChild(el('td', 'time-entry-total-col'));
        foot.appendChild(targetRow);
        table.appendChild(foot);

        var wrap = el('div', 'table-responsive time-entry-grid-wrap');
        wrap.appendChild(table);
        host.appendChild(wrap);
    }

    function suggestionControls(suggestion) {
        var box = el('div', 'time-entry-suggestion');
        box.appendChild(el('span', 'time-entry-ghost-value', core.formatMinutes(suggestion.proposedMinutes)));
        if (editable()) {
            box.appendChild(button('btn btn-sm btn-label-primary time-entry-suggestion-accept', t('Attended'), function () {
                acceptSuggestion(suggestion);
            }, { 'data-suggestion-accept': suggestion.id }));
            box.appendChild(button('btn btn-sm btn-label-secondary time-entry-suggestion-dismiss', t('DidNotAttend'), function () {
                dismissSuggestion(suggestion);
            }, { 'data-suggestion-dismiss': suggestion.id }));
        }
        return box;
    }

    // ── render: the day view (phone) ───────────────────────────────────────────────────────────────────────────

    function renderDayView() {
        var host = clear(byId('teDayView'));
        var switcher = el('div', 'time-entry-day-switcher');
        switcher.setAttribute('role', 'tablist');
        (state.payload.days || []).forEach(function (day) {
            var selected = day.date === state.selectedDay;
            var tab = button('btn btn-sm time-entry-day-tab' + (selected ? ' active' : '') + (day.isFuture ? ' time-entry-day-future' : ''),
                formatDate(day.date, { weekday: 'short', day: 'numeric' }), function () {
                    state.selectedDay = day.date;
                    render();
                }, { 'role': 'tab', 'aria-selected': selected ? 'true' : 'false', 'data-day': day.date });
            switcher.appendChild(tab);
        });
        host.appendChild(switcher);

        var date = state.selectedDay;
        var day = dayOf(date);
        if (!day) { return; }
        var list = el('ul', 'list-group time-entry-day-list');
        state.rows.forEach(function (row) {
            var item = el('li', 'list-group-item time-entry-day-item');
            var label = el('div', 'time-entry-day-item-label');
            label.appendChild(el('span', 'time-entry-row-title', rowLabel(row)));
            label.appendChild(sourceBadge(row.source));
            if (core.isPendingRow(row)) { label.appendChild(pendingBadge()); }
            item.appendChild(label);
            var cell = row.cells[date];
            if (cellEditable(row, date)) {
                var input = el('input', 'form-control form-control-sm time-entry-cell-input');
                input.type = 'text';
                input.inputMode = 'decimal';
                input.value = core.formatMinutes(cell ? cell.minutes : 0);
                input.setAttribute('data-row-key', row.key);
                input.setAttribute('data-date', date);
                input.setAttribute('aria-label', t('CellAria', { row: rowLabel(row), day: formatDate(date, { weekday: 'long', day: 'numeric', month: 'long' }) }));
                input.addEventListener('change', function () { if (commitInput(input)) { renderKeepingFocus(); } });
                item.appendChild(input);
            } else if (core.isCaptured(row.source) && cell) {
                var value = button('btn btn-sm time-entry-cell-captured', core.formatMinutes(cell.minutes) || '0:00', function () {
                    state.panel = { kind: 'correct', rowKey: row.key, date: date };
                    render();
                });
                if (!editable()) { value.disabled = true; }
                item.appendChild(value);
            } else {
                item.appendChild(el('span', 'time-entry-cell-value', core.formatMinutes(cell ? cell.minutes : 0)));
            }
            list.appendChild(item);
        });
        (core.openSuggestionsByDay(state.payload)[date] || []).forEach(function (suggestion) {
            var item = el('li', 'list-group-item time-entry-day-item time-entry-row-suggestion');
            var label = el('div', 'time-entry-day-item-label');
            label.appendChild(el('span', 'time-entry-row-title', suggestion.title));
            label.appendChild(sourceBadge('Meeting'));
            item.appendChild(label);
            item.appendChild(suggestionControls(suggestion));
            list.appendChild(item);
        });
        Object.keys(state.ghosts).forEach(function (ghostKey) {
            var ghost = state.ghosts[ghostKey];
            if (ghost.date !== date) { return; }
            var item = el('li', 'list-group-item time-entry-day-item time-entry-row-ghost');
            var label = el('div', 'time-entry-day-item-label');
            label.appendChild(el('span', 'time-entry-row-title', targetLabel(ghost.taskItemId, null, ghost.taskTitle)));
            label.appendChild(sourceBadge('Plan'));
            item.appendChild(label);
            if (editable()) { item.appendChild(ghostButton(ghostKey, ghost)); }
            list.appendChild(item);
        });
        if (!list.childNodes.length) {
            list.appendChild(el('li', 'list-group-item time-entry-empty-cell', t('EmptyDay')));
        }
        host.appendChild(list);

        var totals = core.dayTotals(state.rows, [date]);
        var summary = el('p', 'time-entry-day-summary', t('DaySummary', {
            recorded: core.formatMinutes(totals[date]) || '0:00',
            target: day.targetMinutes > 0 ? core.formatMinutes(day.targetMinutes) : '—'
        }));
        if (day.isFlagged) { summary.classList.add('time-entry-day-flagged'); }
        host.appendChild(summary);
    }

    // ── render: row add, review, actions ───────────────────────────────────────────────────────────────────────

    function renderRowAdd() {
        var host = clear(byId('teRowAdd'));
        host.hidden = !editable();
        if (!editable()) { return; }
        host.appendChild(button('btn btn-sm btn-label-primary', t('AddTaskRow'), openTaskPicker, { id: 'teAddTaskRow' }));
        host.appendChild(button('btn btn-sm btn-label-secondary', t('AddCategoryRow'), openCategoryPicker, { id: 'teAddCategoryRow' }));

        if (state.picker) {
            var picker = el('div', 'time-entry-picker');
            var select = el('select', 'form-select form-select-sm time-entry-picker-select');
            select.id = 'tePickerSelect';
            select.setAttribute('aria-label', state.picker.kind === 'task' ? t('PickTask') : t('PickCategory'));
            select.appendChild(el('option', null, state.picker.kind === 'task' ? t('PickTask') : t('PickCategory')));
            select.firstChild.value = '';
            state.picker.options.forEach(function (option) {
                var node = el('option', null, state.picker.kind === 'task' ? option.title : categoryLabel(option.code));
                node.value = state.picker.kind === 'task' ? option.taskItemId : option.code;
                select.appendChild(node);
            });
            if (!state.picker.options.length) {
                picker.appendChild(el('span', 'time-entry-picker-empty', state.picker.kind === 'task' ? t('NoTaskOptions') : t('NoCategoryOptions')));
            }
            var take = function () {
                if (!select.value) { return; }
                if (state.picker.kind === 'task') {
                    addTaskRow(state.picker.options.filter(function (o) { return o.taskItemId === select.value; })[0]);
                } else {
                    addCategoryRow(select.value);
                }
            };
            select.addEventListener('change', take);
            picker.appendChild(select);
            picker.appendChild(button('btn btn-sm btn-label-secondary', t('Cancel'), function () { state.picker = null; render(); }));
            host.appendChild(picker);
            enhanceTaskPicker(select, take);
        }
    }

    /** select2 is the shell's existing picker; when it is present, the task list searches on the server. */
    function enhanceTaskPicker(select, take) {
        var $ = root.jQuery;
        if (!$ || !$.fn || !$.fn.select2 || !state.picker || state.picker.kind !== 'task') { return; }
        var options = state.picker.options;
        $(select).select2({
            width: '100%',
            dropdownParent: $(select).parent(),
            ajax: {
                delay: 250,
                transport: function (params, success, failure) {
                    api.taskOptions(params.data && params.data.term || '').then(function (result) {
                        if (!result.ok) { failure(result); return; }
                        (result.data || []).forEach(function (o) {
                            if (!options.some(function (x) { return x.taskItemId === o.taskItemId; })) { options.push(o); }
                        });
                        success(result.data || []);
                    });
                },
                processResults: function (data) {
                    return { results: data.map(function (o) { return { id: o.taskItemId, text: o.title }; }) };
                }
            }
        }).on('select2:select', take);
    }

    function renderReview() {
        var host = clear(byId('teReview'));
        host.hidden = !state.review;
        if (!state.review) { return; }
        host.appendChild(el('h6', 'time-entry-review-title', t('ReviewSuggestions')));
        var list = el('ul', 'list-unstyled time-entry-review-list');
        var addItem = function (kind, value, text) {
            var item = el('li', 'form-check');
            var box = el('input', 'form-check-input');
            box.type = 'checkbox';
            box.value = value;
            box.id = 'teReview-' + kind + '-' + value.replace(/[^A-Za-z0-9]/g, '');
            box.setAttribute('data-kind', kind);
            var label = el('label', 'form-check-label', text);
            label.setAttribute('for', box.id);
            item.appendChild(box);
            item.appendChild(label);
            list.appendChild(item);
        };
        var byDay = core.openSuggestionsByDay(state.payload);
        Object.keys(byDay).sort().forEach(function (date) {
            byDay[date].forEach(function (s) {
                addItem('meeting', s.id, t('ReviewMeetingItem', {
                    day: formatDate(date, { weekday: 'short', day: 'numeric' }), title: s.title, value: core.formatMinutes(s.proposedMinutes)
                }));
            });
        });
        Object.keys(state.ghosts).sort().forEach(function (key) {
            var ghost = state.ghosts[key];
            addItem('plan', key, t('ReviewPlanItem', {
                day: formatDate(ghost.date, { weekday: 'short', day: 'numeric' }),
                title: targetLabel(ghost.taskItemId, null, ghost.taskTitle),
                value: core.formatMinutes(ghost.minutes)
            }));
        });
        if (!list.childNodes.length) {
            host.appendChild(el('p', 'time-entry-review-empty', t('NoSuggestions')));
        } else {
            host.appendChild(list);
        }
        var actions = el('div', 'time-entry-review-actions');
        if (list.childNodes.length) {
            actions.appendChild(button('btn btn-sm btn-primary', t('AddSelected'), addSelectedSuggestions, { id: 'teReviewAdd' }));
        }
        actions.appendChild(button('btn btn-sm btn-label-secondary', t('Close'), function () { state.review = false; render(); }));
        host.appendChild(actions);
    }

    function renderFooterActions() {
        var host = clear(byId('teFooterActions'));
        var payload = state.payload;
        if (!state.canUpdate) { return; }

        if (editable()) {
            var saveButton = button('btn btn-label-primary', t('SaveDraft'), function () { save(); }, { id: 'teSave' });
            saveButton.disabled = !dirty();
            host.appendChild(saveButton);
        }
        if (payload.status === 'Draft' && payload.editable) {
            host.appendChild(button('btn btn-primary', core.isRejectedNow(payload) ? t('Resubmit') : t('SubmitWeek'), submitWeek, { id: 'teSubmit' }));
        }
        if (payload.status === 'Draft' && payload.correctionOfRevision) {
            host.appendChild(button('btn btn-label-danger', t('DiscardCorrection'), discardCorrection, { id: 'teDiscardCorrection' }));
        }
        if (payload.status === 'Submitted') {
            host.appendChild(button('btn btn-label-secondary', t('Withdraw'), withdrawWeek, { id: 'teWithdraw' }));
        }
        if (payload.status === 'Approved' && !payload.correctionOfRevision) {
            host.appendChild(button('btn btn-label-secondary', t('RequestCorrection'), function () {
                state.correctionOpen = true;
                render();
                var field = byId('teCorrectionReason');
                if (field) { field.focus(); }
            }, { id: 'teRequestCorrection' }));
        }

        // The correction reason is a field ON the page (never a browser prompt): required, said inline.
        if (state.correctionOpen && payload.status === 'Approved') {
            var form = el('div', 'time-entry-correction-form');
            form.id = 'teCorrectionForm';
            var label = el('label', 'form-label', t('CorrectionReason'));
            label.setAttribute('for', 'teCorrectionReason');
            var field = el('textarea', 'form-control');
            field.id = 'teCorrectionReason';
            field.rows = 3;
            field.maxLength = 1000;
            var error = el('div', 'invalid-feedback time-entry-inline-error');
            error.id = 'teCorrectionError';
            error.hidden = true;
            form.appendChild(label);
            form.appendChild(field);
            form.appendChild(error);
            var row = el('div', 'time-entry-correction-actions');
            row.appendChild(button('btn btn-sm btn-primary', t('StartCorrection'), function () { requestCorrection(field.value); }, { id: 'teCorrectionSend' }));
            row.appendChild(button('btn btn-sm btn-label-secondary', t('Cancel'), function () { state.correctionOpen = false; render(); }));
            form.appendChild(row);
            host.appendChild(form);
        }
    }

    // ── render: side panel ─────────────────────────────────────────────────────────────────────────────────────

    function noticeList(title, items, id) {
        var section = el('section', 'time-entry-side-section');
        if (id) { section.id = id; }
        section.appendChild(el('h6', 'time-entry-side-title', title));
        var list = el('ul', 'list-unstyled time-entry-side-list');
        items.forEach(function (text) { list.appendChild(el('li', 'time-entry-side-item', text)); });
        section.appendChild(list);
        return section;
    }

    function renderSide() {
        var host = clear(byId('teSideBody'));
        var payload = state.payload;
        var itemLabel = function (x) {
            return formatDate(x.localDate, { weekday: 'short', day: 'numeric' }) + ' · ' + targetLabel(x.taskItemId, x.categoryCode, x.taskTitle);
        };

        var midnight = state.timer && state.timer.closedAtMidnightYesterday || [];
        if (midnight.length) {
            host.appendChild(noticeList(t('NoticeMidnightTitle'), midnight.map(function (s) {
                return itemLabel(s) + ' · ' + core.formatMinutes(Math.round(s.durationSeconds / 60));
            }), 'teNoticeMidnight'));
        }
        var outside = payload.timerOutsideOpenWeek || [];
        if (outside.length) {
            host.appendChild(noticeList(t('NoticeOutsideOpenWeekTitle'), outside.map(function (x) {
                return itemLabel(x) + ' · ' + core.formatMinutes(x.minutes);
            }), 'teNoticeOutside'));
        }
        var pending = payload.timerDraftPending || [];
        if (pending.length) {
            host.appendChild(noticeList(t('NoticeDraftPendingTitle'), pending.map(function (x) {
                return itemLabel(x) + ' · ' + core.formatMinutes(x.segmentMinutes);
            }), 'teNoticePending'));
        }
        var tooShort = payload.tooShortToCount || [];
        if (tooShort.length) {
            host.appendChild(noticeList(t('TooShortTitle'), tooShort.map(function (x) {
                return itemLabel(x) + ' · ' + t('Seconds', { value: x.seconds });
            }), 'teTooShort'));
        }

        if (state.panel && state.panel.kind === 'correct') { host.appendChild(correctionPanel()); }
        if (state.panel && state.panel.kind === 'note') { host.appendChild(notePanel()); }

        if (!host.childNodes.length) {
            host.appendChild(el('p', 'time-entry-side-empty', t('SideEmpty')));
        }
    }

    /** The correction of a captured (timer / meeting) cell: what was measured, what stands now, what it becomes. The
     * change stays on the page until the draft is saved; `source` goes with it (core.buildSaveEntries). */
    function correctionPanel() {
        var row = rowByKey(state.panel.rowKey);
        var date = state.panel.date;
        var cell = row && row.cells[date];
        var section = el('section', 'time-entry-side-section time-entry-correct-panel');
        section.id = 'teCorrectPanel';
        if (!cell) { return section; }
        section.appendChild(el('h6', 'time-entry-side-title', t('CorrectCapturedTitle')));
        section.appendChild(el('p', 'time-entry-side-item', rowLabel(row) + ' · ' + formatDate(date, { weekday: 'long', day: 'numeric', month: 'long' })));
        var measured = cell.capturedMinutes !== null && cell.capturedMinutes !== undefined ? cell.capturedMinutes : cell.originalMinutes;
        section.appendChild(el('p', 'time-entry-side-item', t('MeasuredValue', { value: core.formatMinutes(measured) || '0:00' })));
        if (cell.outsideWorkingMinutes > 0) {
            section.appendChild(el('p', 'time-entry-side-item', t('MarkOutsideHours', { value: core.formatMinutes(cell.outsideWorkingMinutes) })));
        }
        var label = el('label', 'form-label', t('CorrectedValue'));
        label.setAttribute('for', 'teCorrectValue');
        var input = el('input', 'form-control form-control-sm');
        input.id = 'teCorrectValue';
        input.type = 'text';
        input.value = core.formatMinutes(cell.minutes);
        var noteLabel = el('label', 'form-label', t('Note'));
        noteLabel.setAttribute('for', 'teCorrectNote');
        var note = el('textarea', 'form-control form-control-sm');
        note.id = 'teCorrectNote';
        note.rows = 2;
        note.maxLength = 500;
        note.value = cell.note || '';
        section.appendChild(label);
        section.appendChild(input);
        section.appendChild(noteLabel);
        section.appendChild(note);
        var actions = el('div', 'time-entry-side-actions');
        var apply = button('btn btn-sm btn-primary', t('Apply'), function () {
            var parsed = core.parseDuration(input.value);
            if (!parsed.ok || parsed.minutes === 0) {
                // A captured row cannot be corrected to nothing (the server keeps it; zero is not a step) — said as such,
                // not as a generic "quarter hours" refusal (v3 L10).
                input.classList.add('is-invalid');
                announce(!parsed.ok && !parsed.tooSmall ? t('InvalidDuration') : t('CapturedZeroNotAllowed'), 'warning');
                return;
            }
            core.setCell(row, date, parsed.minutes, note.value);
            if (parsed.rounded) { announce(t('RoundedTo', { value: core.formatMinutes(parsed.minutes) || '0:00' })); }
            state.panel = null;
            render();
        }, { id: 'teCorrectApply' });
        apply.disabled = !editable();
        actions.appendChild(apply);
        actions.appendChild(button('btn btn-sm btn-label-secondary', t('Close'), function () { state.panel = null; render(); }));
        section.appendChild(actions);
        return section;
    }

    /** A row's notes: one field per day that holds time (the server keeps a note per entry). */
    function notePanel() {
        var row = rowByKey(state.panel.rowKey);
        var section = el('section', 'time-entry-side-section time-entry-note-panel');
        section.id = 'teNotePanel';
        if (!row) { return section; }
        section.appendChild(el('h6', 'time-entry-side-title', t('RowNoteTitle', { row: rowLabel(row) })));
        var days = Object.keys(row.cells).filter(function (d) { return Number(row.cells[d].minutes) > 0; }).sort();
        if (!days.length) {
            section.appendChild(el('p', 'time-entry-side-item', t('NoteNeedsTime')));
        }
        var fields = [];
        days.forEach(function (date) {
            var id = 'teNote-' + date;
            var label = el('label', 'form-label', formatDate(date, { weekday: 'long', day: 'numeric', month: 'long' }));
            label.setAttribute('for', id);
            var field = el('textarea', 'form-control form-control-sm');
            field.id = id;
            field.rows = 2;
            field.maxLength = 500;
            field.value = row.cells[date].note || '';
            field.readOnly = !(editable() && (core.isPersonTyped(row.source) || core.isCaptured(row.source)));
            section.appendChild(label);
            section.appendChild(field);
            fields.push({ date: date, field: field });
        });
        var actions = el('div', 'time-entry-side-actions');
        if (editable() && days.length) {
            actions.appendChild(button('btn btn-sm btn-primary', t('Apply'), function () {
                fields.forEach(function (f) { row.cells[f.date].note = f.field.value; });
                state.panel = null;
                render();
            }, { id: 'teNoteApply' }));
        }
        actions.appendChild(button('btn btn-sm btn-label-secondary', t('Close'), function () { state.panel = null; render(); }));
        section.appendChild(actions);
        return section;
    }

    // ── render: whole page ─────────────────────────────────────────────────────────────────────────────────────

    function render() {
        if (!state.payload) { return; }
        byId('teLoading').hidden = true;
        byId('teContent').hidden = false;
        renderHeader();
        renderBanner();
        renderHistory();
        renderRowAdd();
        renderReview();
        renderGrid();
        renderDayView();
        renderFooterActions();
        renderSide();
    }

    function renderAccessDenied() {
        var page = byId('timeEntryPage');
        clear(page);
        var box = el('div', 'card diten-access-denied');
        box.setAttribute('role', 'note');
        box.appendChild(el('h5', null, t('AccessDeniedTitle')));
        box.appendChild(el('p', null, t('AccessDeniedMessage')));
        page.appendChild(box);
    }

    function renderLoadFailure(message) {
        byId('teLoading').hidden = false;
        byId('teLoading').textContent = message;
    }

    // ── boot ───────────────────────────────────────────────────────────────────────────────────────────────────

    function bindChrome() {
        byId('tePrevWeek').addEventListener('click', function () { goToWeek(core.shiftWeek(state.weekKey, -1)); });
        byId('teNextWeek').addEventListener('click', function () { goToWeek(core.shiftWeek(state.weekKey, 1)); });
        byId('teToday').addEventListener('click', function () {
            var today = state.payload && state.payload.localToday ? state.payload.localToday : core.browserToday();
            goToWeek(core.weekKeyOfDate(today));
        });
        var copy = byId('teCopyPrevious');
        if (copy) { copy.addEventListener('click', copyPreviousWeek); }
        var plan = byId('teFillFromPlan');
        if (plan) { plan.addEventListener('click', fillFromPlan); }
        var review = byId('teReviewSuggestions');
        if (review) { review.addEventListener('click', openReview); }
        var side = byId('teSideToggle');
        if (side) {
            side.addEventListener('click', function () {
                var panel = byId('teSide');
                var collapsed = panel.classList.toggle('time-entry-side-collapsed');
                side.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
            });
        }
        byId('teGrid').addEventListener('keydown', onGridKeydown);
        root.addEventListener('popstate', onPopState);
        root.addEventListener('beforeunload', onBeforeUnload);
    }

    function init() {
        var page = byId('timeEntryPage');
        if (!page) { return null; }
        L = readL10n();
        var requested = page.getAttribute('data-week');
        state = {
            weekKey: null,
            payload: null,
            rows: [],
            ghosts: {},
            categories: [],
            categoriesLoaded: false,
            canUpdate: page.getAttribute('data-can-update') === 'true',
            timer: null,
            panel: null,
            picker: null,
            review: false,
            correctionOpen: false,
            selectedDay: null,
            gridRowCount: 0
        };
        bindChrome();

        var explicit = core.isWeekKey(requested);
        var first = explicit ? requested : core.weekKeyOfDate(core.browserToday());
        var timer = api.timer().then(function (result) {
            state.timer = result.ok ? result.data : null;
        });
        return Promise.all([
            ensureCategories(),
            timer,
            load(first, { followServerToday: !explicit, replaceUrl: true })
        ]).then(function () {
            if (state.payload) { render(); }
            return state;
        });
    }

    var exported = {
        init: init,
        // Seams the tests drive; the page itself never calls these from outside.
        state: function () { return state; },
        save: save,
        copyPreviousWeek: copyPreviousWeek,
        fillFromPlan: fillFromPlan,
        addSelectedSuggestions: addSelectedSuggestions,
        addTaskRow: addTaskRow,
        addCategoryRow: addCategoryRow,
        requestCorrection: requestCorrection,
        goToWeek: goToWeek
    };
    root.TimeEntryPage = exported;

    if (doc && doc.readyState === 'loading') {
        doc.addEventListener('DOMContentLoaded', init);
    } else if (doc && doc.getElementById('timeEntryPage') && !root.__timeEntryNoAutoInit) {
        init();
    }
})(typeof window !== 'undefined' ? window : globalThis);
