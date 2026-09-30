/*
 * MOD-0280-FU01 T2a (pack §21.2 U1) — the top-bar timer chip.
 *
 * It exists ONLY WHILE A TIMER RUNS: the task (or category) and the elapsed time, a link to the task in the Task
 * Center, and Stop. No timer running, the timer switched off for the person's legal entity, or any failure → the chip
 * is not drawn at all (the element is removed, not left empty).
 *
 * Elapsed time is drawn from the SERVER's start instant (the browser never sends an instant — pack §8.2); the one-second
 * tick only redraws the number. The server is asked once when the page opens and again when the tab becomes visible —
 * no polling.
 *
 * The morning notice ("your timer ran until midnight") is shown at most once a day per person in this browser; the
 * remembering is best-effort (try/catch: private mode or blocked storage only means the notice may show again).
 */
(function (root) {
    'use strict';

    var doc = root.document;
    var API = '/TimeEntry/api/';
    var STORAGE_PREFIX = 'diten.timeEntry.midnightNotice.';

    var chip = null;
    var l10n = {};
    var running = null;
    var ticker = null;
    var categories = null;

    function t(key, values) {
        var text = l10n[key] || key;
        if (values) {
            Object.keys(values).forEach(function (name) { text = text.split('{' + name + '}').join(String(values[name])); });
        }
        return text;
    }

    function request(method, path, body) {
        var options = { method: method, credentials: 'same-origin', headers: { 'Accept': 'application/json' } };
        if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        return root.fetch(API + path, options).then(function (response) {
            return response.text().then(function (text) {
                var parsed = null;
                try { parsed = text ? JSON.parse(text) : null; } catch (error) { parsed = null; }
                return { ok: response.ok, status: response.status, data: parsed && parsed.data !== undefined ? parsed.data : parsed };
            });
        }, function () { return { ok: false, status: 0, data: null }; });
    }

    /** 3725 s → "1:02:05"; under an hour → "2:05". */
    function formatElapsed(seconds) {
        var total = Math.max(0, Math.floor(seconds));
        var hours = Math.floor(total / 3600);
        var minutes = Math.floor((total % 3600) / 60);
        var secs = total % 60;
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        return hours > 0 ? hours + ':' + pad(minutes) + ':' + pad(secs) : minutes + ':' + pad(secs);
    }

    function remove() {
        stopTicker();
        if (chip && chip.parentNode) { chip.parentNode.removeChild(chip); }
        chip = null;
    }

    function stopTicker() {
        if (ticker) { root.clearInterval(ticker); ticker = null; }
    }

    function hide() {
        stopTicker();
        running = null;
        if (chip) { chip.hidden = true; }
    }

    function categoryLabel(code) {
        var fromList = (categories || []).filter(function (c) { return c.code === code; })[0];
        if (fromList && fromList.labelText) { return fromList.labelText; }
        var labels = l10n.CategoryLabels || {};
        var key = fromList && fromList.labelResourceKey ? fromList.labelResourceKey : 'TimeEntry.Category.' + code;
        return labels[key] || code;
    }

    function titleOf(segment) {
        if (segment.taskItemId) { return segment.taskTitle || t('UnreadableTask'); }
        return categoryLabel(segment.categoryCode);
    }

    function draw() {
        if (!chip || !running) { return; }
        var started = Date.parse(running.startedAtUtc);
        var elapsed = isNaN(started) ? 0 : (Date.now() - started) / 1000;
        chip.querySelector('[data-timer-elapsed]').textContent = formatElapsed(elapsed);
    }

    function show(segment) {
        running = segment;
        var link = chip.querySelector('[data-timer-link]');
        link.setAttribute('href', segment.taskItemId
            ? '/WorkCenterNext/Details/' + encodeURIComponent(segment.taskItemId)
            : '/TimeEntry');
        link.setAttribute('title', t('OpenTask'));
        chip.querySelector('[data-timer-title]').textContent = titleOf(segment);
        chip.hidden = false;
        draw();
        stopTicker();
        ticker = root.setInterval(draw, 1000);
    }

    function userKey() {
        return STORAGE_PREFIX + (chip && chip.getAttribute('data-user') || 'anonymous');
    }

    /** At most once a day per person: remembers the latest closed day it has told the person about. */
    function midnightNotice(closed) {
        if (!closed || !closed.length) { return false; }
        var day = closed.map(function (s) { return s.localDate; }).sort().pop();
        var seen = null;
        try { seen = root.localStorage.getItem(userKey()); } catch (error) { seen = null; }
        if (seen === day) { return false; }
        try { root.localStorage.setItem(userKey(), day); } catch (error) { /* shown again tomorrow at worst */ }
        if (typeof root.showToast === 'function') {
            root.showToast(t('MidnightNotice', { count: closed.length }), 'warning');
        }
        return true;
    }

    function refresh() {
        // A chip no longer in the page (the page replaced it) asks nothing.
        if (!chip || !doc.documentElement.contains(chip)) { return Promise.resolve(null); }
        return request('GET', 'timer').then(function (result) {
            if (!chip) { return result; }
            if (!result.ok || !result.data || !result.data.timerEnabled) {
                // Off for the person's legal entity (or no answer at all): the chip is not drawn — not even hidden.
                remove();
                return result;
            }
            midnightNotice(result.data.closedAtMidnightYesterday);
            var segment = result.data.running;
            if (!segment) {
                hide();
                return result;
            }
            if (!segment.taskItemId && categories === null) {
                return request('GET', 'categories').then(function (list) {
                    categories = list.ok && Array.isArray(list.data) ? list.data : [];
                    if (chip) { show(segment); }
                    return result;
                });
            }
            show(segment);
            return result;
        });
    }

    function stop() {
        var stopButton = chip && chip.querySelector('[data-timer-stop]');
        if (stopButton) { stopButton.disabled = true; }
        return request('POST', 'timer/stop', {}).then(function (result) {
            if (stopButton) { stopButton.disabled = false; }
            if (result.ok) {
                hide();
                if (typeof root.showToast === 'function') { root.showToast(t('TimerStopped'), 'success'); }
            } else if (typeof root.showToast === 'function') {
                root.showToast(t('TimerStopFailed'), 'error');
            }
            return result;
        });
    }

    function init() {
        chip = doc.getElementById('timeEntryTimerChip');
        if (!chip) { return Promise.resolve(null); }
        try {
            l10n = JSON.parse(chip.getAttribute('data-l10n') || '{}');
        } catch (error) {
            l10n = {};
        }
        chip.querySelector('[data-timer-stop]').addEventListener('click', stop);
        doc.addEventListener('visibilitychange', function () {
            if (doc.visibilityState === 'visible') { refresh(); }
        });
        return refresh();
    }

    root.DitenTimerChip = { init: init, refresh: refresh, stop: stop, formatElapsed: formatElapsed };

    if (doc.readyState === 'loading') {
        doc.addEventListener('DOMContentLoaded', init);
    } else if (!root.__timerChipNoAutoInit) {
        init();
    }
})(window);
