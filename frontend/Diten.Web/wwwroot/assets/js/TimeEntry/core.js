/*
 * MOD-0280-FU01 T2a — My Timesheet, the page's PURE half (no DOM, no network).
 *
 * WHAT LIVES HERE AND WHAT DOES NOT. Every business rule of a timesheet is the server's: the 15-minute step, the
 * 660-minute flag, the 960-minute refusal, the edit window, which days are in the future, what a suggestion is worth.
 * This file only TRANSLATES: a typed "1,5" into minutes (a format conversion, snapped to the step the server will
 * accept so the person is told before they save), the week payload into rows × days, the rows back into the save
 * body, and a server reason code into a sentence. Nothing here decides whether a value is allowed.
 *
 * Loaded as a plain script (window.TimeEntryCore); the tests load this same file into a vm, never a copy.
 */
(function (root) {
    'use strict';

    var STEP_MINUTES = 15;
    var DAY_MS = 24 * 60 * 60 * 1000;

    /** The sources the person types (a save carries their whole set) and the captured ones (only a correction). */
    var PERSON_TYPED = ['Manual', 'Plan'];
    var CAPTURED = ['Timer', 'Meeting'];

    // ── Duration input ─────────────────────────────────────────────────────────────────────────────────────────

    /**
     * Reads what a person types into a cell. Accepted forms (pack §21.2 U5):
     *   "1:30"          hours:minutes
     *   "1,5" / "1.5"   decimal hours (a bare number is hours, as in SAP CAT2 and Tempo)
     *   "90dk" / "90m"  minutes ("dk", "dak", "m", "min")
     *   ""              clears the cell
     * The result is snapped to the nearest quarter hour (a tie goes up) and says whether it was, so the page can tell
     * the person — never silently.
     *
     * Only an explicit "" / "0" clears a cell. A typed value that would snap to ZERO ("7dk", "0:05") is refused
     * (`tooSmall`), so a cell is never emptied by a rounding the person did not see. A bare number is hours; from 10 up
     * the page says how it was read (`bareHours`: "15" → 15:00), because it may have been meant as minutes.
     */
    function parseDuration(input) {
        var text = String(input === null || input === undefined ? '' : input).trim().toLowerCase().replace(/\s+/g, '');
        if (text === '' || text === '0') {
            return { ok: true, minutes: 0, typed: 0, rounded: false, empty: true };
        }

        var typed = null;
        var bare = false;
        var match;
        if ((match = /^(\d{1,2}):([0-5]\d)$/.exec(text))) {
            typed = Number(match[1]) * 60 + Number(match[2]);
        } else if ((match = /^(\d{1,4})(dk|dak|m|min)$/.exec(text))) {
            typed = Number(match[1]);
        } else if ((match = /^(\d{1,2})(?:[.,](\d{1,2}))?(h|sa|s)?$/.exec(text))) {
            typed = Math.round(Number(match[1] + '.' + (match[2] || '0')) * 60);
            bare = !match[3];
        }

        if (typed === null || !isFinite(typed)) {
            return { ok: false, invalid: true };
        }

        var minutes = Math.round(typed / STEP_MINUTES) * STEP_MINUTES;
        if (typed > 0 && minutes === 0) {
            return { ok: false, tooSmall: true, typed: typed };
        }
        return {
            ok: true, minutes: minutes, typed: typed, rounded: minutes !== typed, empty: minutes === 0,
            bareHours: bare && typed >= 600
        };
    }

    /** 90 → "1:30"; 0 → "". Durations are shown as h:mm everywhere on the page, never as a decimal. */
    function formatMinutes(minutes) {
        var value = Number(minutes) || 0;
        if (value <= 0) {
            return '';
        }
        var hours = Math.floor(value / 60);
        var rest = value % 60;
        return hours + ':' + (rest < 10 ? '0' : '') + rest;
    }

    // ── ISO 8601 weeks (Monday start; the week-year rule: 2027-01-01 belongs to 2026-W53) ──────────────────────

    function toUtc(isoDate) {
        var parts = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(isoDate || ''));
        return parts ? Date.UTC(Number(parts[1]), Number(parts[2]) - 1, Number(parts[3])) : NaN;
    }

    function toIsoDate(utcMs) {
        var date = new Date(utcMs);
        var month = date.getUTCMonth() + 1;
        var day = date.getUTCDate();
        return date.getUTCFullYear() + '-' + (month < 10 ? '0' : '') + month + '-' + (day < 10 ? '0' : '') + day;
    }

    function mondayOfUtc(utcMs) {
        var dayOfWeek = (new Date(utcMs).getUTCDay() + 6) % 7;
        return utcMs - dayOfWeek * DAY_MS;
    }

    function firstMondayOfWeekYear(year) {
        return mondayOfUtc(Date.UTC(year, 0, 4));
    }

    function weekKeyOfDate(isoDate) {
        var utc = toUtc(isoDate);
        if (isNaN(utc)) {
            return null;
        }
        var monday = mondayOfUtc(utc);
        var thursday = monday + 3 * DAY_MS;
        var year = new Date(thursday).getUTCFullYear();
        var week = 1 + Math.round((monday - firstMondayOfWeekYear(year)) / (7 * DAY_MS));
        return year + '-W' + (week < 10 ? '0' : '') + week;
    }

    function isWeekKey(value) {
        return /^\d{4}-W(0[1-9]|[1-4]\d|5[0-3])$/.test(String(value || ''));
    }

    function mondayOfWeekKey(weekKey) {
        var match = /^(\d{4})-W(\d{2})$/.exec(String(weekKey || ''));
        if (!match) {
            return null;
        }
        return toIsoDate(firstMondayOfWeekYear(Number(match[1])) + (Number(match[2]) - 1) * 7 * DAY_MS);
    }

    function shiftWeek(weekKey, weeks) {
        var monday = mondayOfWeekKey(weekKey);
        return monday ? weekKeyOfDate(toIsoDate(toUtc(monday) + weeks * 7 * DAY_MS)) : null;
    }

    /** The browser's own date, only as a first guess before the server says what "today" is in the tenant zone. */
    function browserToday(now) {
        var date = now || new Date();
        var month = date.getMonth() + 1;
        var day = date.getDate();
        return date.getFullYear() + '-' + (month < 10 ? '0' : '') + month + '-' + (day < 10 ? '0' : '') + day;
    }

    // ── The grid model ─────────────────────────────────────────────────────────────────────────────────────────

    function rowKeyOf(source, taskItemId, categoryCode, sourceRef) {
        return [source, taskItemId || '', categoryCode || '', sourceRef || ''].join('|');
    }

    function isCaptured(source) {
        return CAPTURED.indexOf(source) !== -1;
    }

    function isPersonTyped(source) {
        return PERSON_TYPED.indexOf(source) !== -1;
    }

    /**
     * Turns the week payload into rows × days. A row is one (source, task-or-category, meeting) — so a timer row and a
     * typed row of the same task stay apart, each with its own source badge. Every cell remembers what the server
     * said, so the save body can tell a touched captured cell from an untouched one.
     */
    function buildRows(payload) {
        var rows = [];
        var byKey = {};
        (payload && payload.entries || []).forEach(function (entry) {
            var key = rowKeyOf(entry.source, entry.taskItemId, entry.categoryCode, entry.sourceRef);
            var row = byKey[key];
            if (!row) {
                row = {
                    key: key,
                    source: entry.source,
                    taskItemId: entry.taskItemId || null,
                    categoryCode: entry.categoryCode || null,
                    sourceRef: entry.sourceRef || null,
                    taskTitle: entry.taskTitle || null,
                    cells: {}
                };
                byKey[key] = row;
                rows.push(row);
            }
            row.cells[entry.localDate] = {
                entryId: entry.id,
                minutes: entry.durationMinutes,
                note: entry.note || '',
                originalMinutes: entry.durationMinutes,
                originalNote: entry.note || '',
                capturedMinutes: entry.capturedMinutes === undefined ? null : entry.capturedMinutes,
                editedFromTimer: !!entry.editedFromTimer,
                outsideWorkingMinutes: entry.outsideWorkingMinutes || 0,
                minutesConflict: !!entry.minutesConflict
            };
        });
        return rows;
    }

    /** A new, empty, person-typed row. It exists on the page only until it holds minutes (the server has no rows). */
    function newRow(source, taskItemId, categoryCode, taskTitle) {
        return {
            key: rowKeyOf(source, taskItemId, categoryCode, null),
            source: source,
            taskItemId: taskItemId || null,
            categoryCode: categoryCode || null,
            sourceRef: null,
            taskTitle: taskTitle || null,
            cells: {}
        };
    }

    function sameTarget(a, b) {
        return (a.taskItemId || null) === (b.taskItemId || null) && (a.categoryCode || null) === (b.categoryCode || null);
    }

    /** Sets a cell's minutes (and note, when given). Returns the cell. */
    function setCell(row, date, minutes, note) {
        var cell = row.cells[date];
        if (!cell) {
            cell = { entryId: null, minutes: 0, note: '', originalMinutes: null, originalNote: '', capturedMinutes: null };
            row.cells[date] = cell;
        }
        cell.minutes = minutes;
        if (note !== undefined) {
            cell.note = note;
        }
        return cell;
    }

    function dayTotals(rows, dates) {
        var totals = {};
        dates.forEach(function (date) { totals[date] = 0; });
        rows.forEach(function (row) {
            Object.keys(row.cells).forEach(function (date) {
                if (totals[date] !== undefined) {
                    totals[date] += Number(row.cells[date].minutes) || 0;
                }
            });
        });
        return totals;
    }

    function rowTotal(row) {
        return Object.keys(row.cells).reduce(function (sum, date) { return sum + (Number(row.cells[date].minutes) || 0); }, 0);
    }

    /**
     * Days up to today whose recorded time is below their target — two server figures compared, never a ratio (Z-4).
     * The recorded figure is the SAVED one, so the indicator describes the week as it stands on the server.
     */
    function missingDays(payload) {
        return (payload && payload.days || []).filter(function (day) {
            return !day.isFuture && day.targetMinutes > 0 && day.recordedMinutes < day.targetMinutes;
        }).map(function (day) { return day.date; });
    }

    /**
     * The save body. Person-typed rows (Manual, Plan) are the person's WHOLE set — every cell with minutes, a cell left
     * out is removed. A captured row (Timer, Meeting) is sent only where the person changed its minutes or its note —
     * an untouched captured cell is NEVER in the body, so a save can never re-state (or "correct" to itself) what the
     * timer measured. Every row names its source.
     */
    function buildSaveEntries(rows) {
        var entries = [];
        rows.forEach(function (row) {
            Object.keys(row.cells).sort().forEach(function (date) {
                var cell = row.cells[date];
                var minutes = Number(cell.minutes) || 0;
                var note = (cell.note || '').trim();
                if (isPersonTyped(row.source)) {
                    if (minutes > 0) {
                        entries.push(entryBody(row, date, minutes, note));
                    }
                    return;
                }
                if (isCaptured(row.source) && cell.entryId) {
                    var touched = minutes !== cell.originalMinutes || note !== (cell.originalNote || '').trim();
                    if (touched) {
                        entries.push(entryBody(row, date, minutes, note));
                    }
                }
            });
        });
        return entries;
    }

    function entryBody(row, date, minutes, note) {
        return {
            localDate: date,
            taskItemId: row.taskItemId,
            categoryCode: row.taskItemId ? null : row.categoryCode,
            durationMinutes: minutes,
            note: note === '' ? null : note,
            source: row.source,
            sourceRef: row.source === 'Meeting' ? row.sourceRef : null
        };
    }

    function isDirty(rows) {
        return rows.some(function (row) {
            return Object.keys(row.cells).some(function (date) {
                var cell = row.cells[date];
                return (Number(cell.minutes) || 0) !== (cell.originalMinutes || 0)
                    || (cell.note || '').trim() !== (cell.originalNote || '').trim();
            });
        });
    }

    /**
     * "Copy previous week" (U5): the previous week's TARGETS become empty Manual rows — no minutes, ever. A target that
     * already has a person-typed row here is left alone (never overwritten), a task the person can no longer read
     * (no title) and a category that is no longer active are skipped. Returns the rows actually added.
     */
    function copyRowsFromWeek(currentRows, previousPayload, activeCategoryCodes) {
        var active = activeCategoryCodes || [];
        var added = [];
        buildRows(previousPayload).forEach(function (previous) {
            if (previous.taskItemId && !previous.taskTitle) {
                return;
            }
            if (!previous.taskItemId && active.indexOf(previous.categoryCode) === -1) {
                return;
            }
            var exists = currentRows.concat(added).some(function (row) {
                return isPersonTyped(row.source) && sameTarget(row, previous);
            });
            if (!exists) {
                added.push(newRow('Manual', previous.taskItemId, previous.taskItemId ? null : previous.categoryCode, previous.taskTitle));
            }
        });
        added.forEach(function (row) { currentRows.push(row); });
        return added;
    }

    /**
     * Plan fill-in ghost values (D9) placed on the grid: only on a cell with no minutes yet, never replacing anything
     * the person has. Returns { rowKey|date: {minutes, taskItemId, taskTitle, date} }.
     */
    function planGhosts(rows, planRows) {
        var ghosts = {};
        (planRows || []).forEach(function (plan) {
            var occupied = rows.some(function (row) {
                return row.taskItemId === plan.taskItemId && row.cells[plan.localDate] && Number(row.cells[plan.localDate].minutes) > 0;
            });
            if (!occupied) {
                ghosts[plan.taskItemId + '|' + plan.localDate] = {
                    minutes: plan.durationMinutes, taskItemId: plan.taskItemId, taskTitle: plan.taskTitle || null, date: plan.localDate
                };
            }
        });
        return ghosts;
    }

    /**
     * Accepting a plan ghost. Manual and Plan rows of one task share a cell on the server (one typed row per task and
     * day), so the minutes go into the task's EXISTING typed row — Manual first, then Plan — and a Plan row is created only
     * when the task has neither. Never a second row for the same task and day.
     */
    function acceptPlanGhost(rows, ghost) {
        var typed = function (source) {
            return rows.filter(function (r) { return r.source === source && r.taskItemId === ghost.taskItemId; })[0];
        };
        var row = typed('Manual') || typed('Plan');
        if (!row) {
            row = newRow('Plan', ghost.taskItemId, null, ghost.taskTitle);
            rows.push(row);
        }
        setCell(row, ghost.date, ghost.minutes);
        return row;
    }

    /** A person-typed row with no minutes on any day: it lives only on the page until it holds time (the server keeps
     * no empty rows). The page marks it, keeps it across a reload of the same week, and warns before it is left. */
    function isPendingRow(row) {
        return isPersonTyped(row.source) && !Object.keys(row.cells).some(function (date) {
            var cell = row.cells[date];
            return (Number(cell.minutes) || 0) > 0 || cell.entryId;
        });
    }

    function pendingRows(rows) {
        return rows.filter(isPendingRow);
    }

    /** Puts the page's pending rows back after a reload: each one whose target has no typed row in the fresh list. */
    function mergePending(freshRows, pending) {
        (pending || []).forEach(function (row) {
            var covered = freshRows.some(function (r) { return isPersonTyped(r.source) && sameTarget(r, row); });
            if (!covered) {
                freshRows.push(newRow(row.source, row.taskItemId, row.taskItemId ? null : row.categoryCode, row.taskTitle));
            }
        });
        return freshRows;
    }

    /**
     * Is the week showing a rejection RIGHT NOW? Only when the last rejection is newer than the last submission (or there
     * was no submission since). Rejected → resubmitted → withdrawn is a plain draft again, not "rejected".
     */
    function isRejectedNow(payload) {
        if (!payload || payload.status !== 'Draft' || !payload.lastRejectedAtUtc) {
            return false;
        }
        if (!payload.submittedAtUtc) {
            return true;
        }
        return Date.parse(payload.lastRejectedAtUtc) > Date.parse(payload.submittedAtUtc);
    }

    /** Meeting suggestions still waiting for the person, grouped by day (they are drawn INSIDE their day, U3). */
    function openSuggestionsByDay(payload) {
        var byDay = {};
        (payload && payload.suggestions || []).forEach(function (suggestion) {
            if (suggestion.state !== 'Open' && suggestion.state !== 'open') {
                return;
            }
            (byDay[suggestion.localDate] = byDay[suggestion.localDate] || []).push(suggestion);
        });
        return byDay;
    }

    // ── Keyboard: where an arrow key goes ──────────────────────────────────────────────────────────────────────

    /**
     * The next cell for an arrow key. In a right-to-left page (ar) the week runs right to left, so the LEFT arrow
     * moves to the NEXT day — the arrow follows what the eye sees, not the array index.
     */
    function nextCell(position, key, size, isRtl) {
        var row = position.row;
        var col = position.col;
        var forward = isRtl ? 'ArrowLeft' : 'ArrowRight';
        var backward = isRtl ? 'ArrowRight' : 'ArrowLeft';
        if (key === forward) { col = Math.min(size.cols - 1, col + 1); }
        else if (key === backward) { col = Math.max(0, col - 1); }
        else if (key === 'ArrowDown' || key === 'Enter') { row = Math.min(size.rows - 1, row + 1); }
        else if (key === 'ArrowUp') { row = Math.max(0, row - 1); }
        return { row: row, col: col };
    }

    // ── Reason codes → sentences (the Password error-code bridge pattern) ─────────────────────────────────────

    /**
     * EVERY code Platform's TimeEntryReasonCodes declares, plus MOD-0023's reject-comment code, mapped to a message key
     * of this page's l10n payload. A guard test reads TimeEntryModels.cs and fails when a code is missing here, mapped
     * twice, or its key is missing from any of the seven resx files.
     */
    var REASON_CODE_MESSAGE_KEYS = {
        TIME_ENTRY_STEP_INVALID: 'ErrStepInvalid',
        TIMESHEET_TIMER_DRAFTS_UNAVAILABLE: 'ErrTimerDraftsUnavailable',
        TIME_ENTRY_FUTURE_DATE: 'ErrFutureDate',
        TIME_ENTRY_DATE_OUTSIDE_WEEK: 'ErrDateOutsideWeek',
        TIME_ENTRY_TARGET_INVALID: 'ErrTargetInvalid',
        TIME_ENTRY_DUPLICATE_ROW: 'ErrDuplicateRow',
        TIME_ENTRY_NOTE_TOO_LONG: 'ErrNoteTooLong',
        TIME_ENTRY_CATEGORY_INACTIVE: 'ErrCategoryInactive',
        TIME_ENTRY_DAY_IMPLAUSIBLE: 'ErrDayImplausible',
        TIMESHEET_WEEK_KEY_INVALID: 'ErrWeekKeyInvalid',
        TIMESHEET_WEEK_NOT_FOUND: 'ErrWeekNotFound',
        TIMESHEET_WEEK_NOT_OPEN: 'ErrWeekNotOpen',
        TIMESHEET_WEEK_OUTSIDE_EDIT_WINDOW: 'ErrWeekOutsideEditWindow',
        TIMESHEET_EMPTY_WEEK: 'ErrEmptyWeek',
        TIMESHEET_NO_APPROVER: 'ErrNoApprover',
        TIMESHEET_APPROVAL_START_FAILED: 'ErrApprovalStartFailed',
        TIMESHEET_WITHDRAW_TOO_LATE: 'ErrWithdrawTooLate',
        TIMESHEET_CORRECTION_REASON_REQUIRED: 'ErrCorrectionReasonRequired',
        TIMESHEET_CORRECTION_ALREADY_OPEN: 'ErrCorrectionAlreadyOpen',
        TIMESHEET_CORRECTION_NOT_ALLOWED: 'ErrCorrectionNotAllowed',
        TIMESHEET_CORRECTION_DRAFT_NOT_FOUND: 'ErrCorrectionDraftNotFound',
        TIMESHEET_REOPEN_REASON_REQUIRED: 'ErrReopenReasonRequired',
        TIMESHEET_REOPEN_NOT_NEEDED: 'ErrReopenNotNeeded',
        TIMESHEET_CONCURRENCY_CONFLICT: 'ErrConcurrencyConflict',
        TIMESHEET_SELF_DECISION_REFUSED: 'ErrSelfDecisionRefused',
        TIMESHEET_APPROVAL_INSTANCE_CLOSED: 'ErrApprovalInstanceClosed',
        TIMESHEET_REOPEN_OWN_WEEK: 'ErrReopenOwnWeek',
        TIMESHEET_PERSON_NOT_FOUND: 'ErrPersonNotFound',
        TIMER_SWITCH_CONCURRENCY_CONFLICT: 'ErrTimerSwitchConcurrencyConflict',
        TIMESHEET_APPROVAL_NOT_FOUND: 'ErrApprovalNotFound',
        WORK_CATEGORY_NOT_FOUND: 'ErrCategoryNotFound',
        WORK_CATEGORY_CODE_INVALID: 'ErrCategoryCodeInvalid',
        WORK_CATEGORY_CODE_RESERVED: 'ErrCategoryCodeReserved',
        WORK_CATEGORY_CODE_DUPLICATE: 'ErrCategoryCodeDuplicate',
        WORK_CATEGORY_CODE_IMMUTABLE: 'ErrCategoryCodeImmutable',
        WORK_CATEGORY_LABEL_REQUIRED: 'ErrCategoryLabelRequired',
        WORK_CATEGORY_DESCRIPTION_TOO_LONG: 'ErrCategoryDescriptionTooLong',
        WORK_CATEGORY_SORT_ORDER_INVALID: 'ErrCategorySortOrderInvalid',
        WORK_CATEGORY_CONCURRENCY_CONFLICT: 'ErrCategoryConcurrencyConflict',
        TIME_ENTRY_SETTINGS_POSITION_NOT_FOUND: 'ErrSettingsPositionNotFound',
        TIME_ENTRY_SETTINGS_CONCURRENCY_CONFLICT: 'ErrSettingsConcurrencyConflict',
        TIME_ENTRY_LEGAL_ENTITY_REQUIRED: 'ErrLegalEntityRequired',
        TIMER_SWITCH_REASON_REQUIRED: 'ErrTimerSwitchReasonRequired',
        TIMER_DISABLED_FOR_LEGAL_ENTITY: 'ErrTimerDisabledForLegalEntity',
        TIMER_TASK_NOT_HELD: 'ErrTimerTaskNotHeld',
        TIMER_TASK_NOT_IN_PROGRESS: 'ErrTimerTaskNotInProgress',
        TIMER_NOT_RUNNING: 'ErrTimerNotRunning',
        TIMER_CONCURRENCY_CONFLICT: 'ErrTimerConcurrencyConflict',
        TIMER_UNDO_EXPIRED: 'ErrTimerUndoExpired',
        TIME_SUGGESTION_NOT_FOUND: 'ErrSuggestionNotFound',
        TIME_SUGGESTION_ALREADY_DECIDED: 'ErrSuggestionAlreadyDecided',
        TIME_SUGGESTION_WITHDRAWN: 'ErrSuggestionWithdrawn',
        TIME_ENTRY_SOURCE_INVALID: 'ErrSourceInvalid',
        TIME_ENTRY_SOURCE_REQUIRED: 'ErrSourceRequired',
        TIME_ENTRY_CAPTURED_ROW_NOT_FOUND: 'ErrCapturedRowNotFound',
        WORKFLOW_REJECT_COMMENT_REQUIRED: 'ErrRejectCommentRequired'
    };

    /** A failed call → the sentence to show: its code's own, else the generic one. Never the code, never "403". */
    function failureMessage(result, t) {
        var key = result && result.reasonCode ? REASON_CODE_MESSAGE_KEYS[result.reasonCode] : null;
        if (!key && result && result.status === 403) {
            key = 'ErrForbidden';
        }
        return t(key || 'ErrGeneric');
    }

    /** A code the page shows as a state (not-editable reason, finalization block) → its sentence, or null. */
    function codeMessage(code, t) {
        var key = code ? REASON_CODE_MESSAGE_KEYS[code] : null;
        return key ? t(key) : null;
    }

    root.TimeEntryCore = {
        STEP_MINUTES: STEP_MINUTES,
        parseDuration: parseDuration,
        formatMinutes: formatMinutes,
        weekKeyOfDate: weekKeyOfDate,
        isWeekKey: isWeekKey,
        mondayOfWeekKey: mondayOfWeekKey,
        shiftWeek: shiftWeek,
        browserToday: browserToday,
        addDays: function (isoDate, days) { return toIsoDate(toUtc(isoDate) + days * DAY_MS); },
        rowKeyOf: rowKeyOf,
        isCaptured: isCaptured,
        isPersonTyped: isPersonTyped,
        buildRows: buildRows,
        newRow: newRow,
        setCell: setCell,
        dayTotals: dayTotals,
        rowTotal: rowTotal,
        missingDays: missingDays,
        buildSaveEntries: buildSaveEntries,
        isDirty: isDirty,
        copyRowsFromWeek: copyRowsFromWeek,
        planGhosts: planGhosts,
        acceptPlanGhost: acceptPlanGhost,
        isPendingRow: isPendingRow,
        pendingRows: pendingRows,
        mergePending: mergePending,
        isRejectedNow: isRejectedNow,
        openSuggestionsByDay: openSuggestionsByDay,
        nextCell: nextCell,
        REASON_CODE_MESSAGE_KEYS: REASON_CODE_MESSAGE_KEYS,
        failureMessage: failureMessage,
        codeMessage: codeMessage
    };
})(typeof window !== 'undefined' ? window : globalThis);
