/*
 * MOD-0280-FU01 T2a (pack §21.2 U1) — the top-bar timer chip.
 *
 * It exists ONLY WHILE A TIMER RUNS: the task (or category) and the elapsed time, a link to the task in the Task
 * Center, and Stop. The timer switched off for the person's legal entity → the chip is not drawn at all (the element is
 * removed). A failed read (network, a restarting service) only HIDES it: the next time the tab becomes visible it asks
 * again, so a passing error never takes the chip away for the rest of the page.
 *
 * Elapsed time is drawn from the SERVER's start instant (the browser never sends an instant — pack §8.2); the one-second
 * tick only redraws the number. The server is asked once when the page opens and again when the tab becomes visible —
 * no polling.
 *
 * WHAT IT COSTS (v3 M5). Every tenant page carries this chip, and for most people the timer is off. So:
 *   · "the timer is off" is remembered for ten minutes in this tab's sessionStorage, per person — the pages opened in
 *     that time send no request at all;
 *   · the timer read is SHARED (window.DitenTimerShared): My Timesheet asks for the same answer, and one page load sends
 *     ONE request for both. That is why this file is loaded without `defer` — it must define the shared read before the
 *     page's own scripts run.
 * Storage is best-effort (try/catch): blocked storage only means the request is sent.
 *
 * The morning notice ("your timer ran until midnight") is shown at most once a day per person in this browser.
 */
(function (root) {
    'use strict';

    var doc = root.document;
    var API = '/TimeEntry/api/';
    var NOTICE_PREFIX = 'diten.timeEntry.midnightNotice.';
    var OFF_PREFIX = 'diten.timeEntry.timerOff.';
    var OFF_REMEMBER_MS = 10 * 60 * 1000;

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
                return {
                    ok: response.ok,
                    status: response.status,
                    data: parsed && parsed.data !== undefined ? parsed.data : parsed,
                    reasonCode: parsed && (parsed.reason_code || parsed.reasonCode) || null
                };
            });
        }, function () { return { ok: false, status: 0, data: null, reasonCode: null }; });
    }

    // ── the shared read ────────────────────────────────────────────────────────────────────────────────────────

    function userId() {
        var node = doc.getElementById('timeEntryTimerChip');
        return node && node.getAttribute('data-user') || 'anonymous';
    }

    function rememberedOff() {
        try {
            var until = Number(root.sessionStorage.getItem(OFF_PREFIX + userId()));
            return until > Date.now();
        } catch (error) {
            return false;
        }
    }

    function rememberOff(off) {
        try {
            if (off) { root.sessionStorage.setItem(OFF_PREFIX + userId(), String(Date.now() + OFF_REMEMBER_MS)); }
            else { root.sessionStorage.removeItem(OFF_PREFIX + userId()); }
        } catch (error) { /* the next page simply asks again */ }
    }

    var pending = null;

    /**
     * The person's timer, asked once per page load. `force` asks again (the tab came back, a Stop needs the truth).
     * A remembered "off" answers without a request.
     */
    function readTimer(force) {
        if (!force && pending) {
            return pending;
        }
        if (!force && rememberedOff()) {
            pending = Promise.resolve({
                ok: true, status: 200, remembered: true,
                data: { timerEnabled: false, disabledReason: null, running: null, closedAtMidnightYesterday: [] }
            });
            return pending;
        }
        pending = request('GET', 'timer').then(function (result) {
            if (result.ok && result.data) { rememberOff(!result.data.timerEnabled); }
            return result;
        });
        return pending;
    }

    root.DitenTimerShared = { read: readTimer, stop: function () { return request('POST', 'timer/stop', {}); } };

    // ── the chip ───────────────────────────────────────────────────────────────────────────────────────────────

    /** 3725 s → "1:02:05"; under an hour → "2:05". */
    function formatElapsed(seconds) {
        var total = Math.max(0, Math.floor(seconds));
        var hours = Math.floor(total / 3600);
        var minutes = Math.floor((total % 3600) / 60);
        var secs = total % 60;
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        return hours > 0 ? hours + ':' + pad(minutes) + ':' + pad(secs) : minutes + ':' + pad(secs);
    }

    function stopTicker() {
        if (ticker) { root.clearInterval(ticker); ticker = null; }
    }

    function remove() {
        stopTicker();
        if (chip && chip.parentNode) { chip.parentNode.removeChild(chip); }
        chip = null;
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

    /** At most once a day per person: remembers the latest closed day it has told the person about. */
    function midnightNotice(closed) {
        if (!closed || !closed.length) { return false; }
        var day = closed.map(function (s) { return s.localDate; }).sort().pop();
        var key = NOTICE_PREFIX + userId();
        var seen = null;
        try { seen = root.localStorage.getItem(key); } catch (error) { seen = null; }
        if (seen === day) { return false; }
        try { root.localStorage.setItem(key, day); } catch (error) { /* shown again tomorrow at worst */ }
        if (typeof root.showToast === 'function') {
            root.showToast(t('MidnightNotice', { count: closed.length }), 'warning');
        }
        return true;
    }

    function apply(result) {
        if (!chip) { return result; }
        if (!result.ok || !result.data) {
            // CT acceptance: a LASTING refusal (401/403/404) removes the chip — asking again on every tab switch only logs
            // warnings; a passing failure (network, 5xx) hides it and the next visibility asks again.
            if (result.status === 401 || result.status === 403 || result.status === 404) { remove(); } else { hide(); }
            return result;
        }
        if (!result.data.timerEnabled) {
            // Off for the person's legal entity: the chip is not drawn — not even hidden.
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
    }

    function refresh(force) {
        // A chip no longer in the page (the page replaced it) asks nothing.
        if (!chip || !doc.documentElement.contains(chip)) { return Promise.resolve(null); }
        return readTimer(force).then(apply);
    }

    function stop() {
        var stopButton = chip && chip.querySelector('[data-timer-stop]');
        if (stopButton) { stopButton.disabled = true; }
        return root.DitenTimerShared.stop().then(function (result) {
            if (stopButton) { stopButton.disabled = false; }
            if (result.ok) {
                hide();
                if (typeof root.showToast === 'function') { root.showToast(t('TimerStopped'), 'success'); }
                return result;
            }
            // Refused — most often because the timer already stopped elsewhere (another tab, a task transition). Ask
            // for the truth instead of leaving a ghost chip: a stopped timer hides it, a running one keeps it.
            if (result.reasonCode !== 'TIMER_NOT_RUNNING' && typeof root.showToast === 'function') {
                root.showToast(t('TimerStopFailed'), 'error');
            }
            return refresh(true).then(function () { return result; });
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
            if (doc.visibilityState === 'visible') { refresh(true); }
        });
        return refresh(false);
    }

    // CT acceptance: from outside, refresh() always ASKS AGAIN (a caller refreshing after a Start must not get the
    // answer cached at page load); the page-load sharing stays internal.
    root.DitenTimerChip = { init: init, refresh: function () { return refresh(true); }, stop: stop, formatElapsed: formatElapsed };

    if (doc.readyState === 'loading') {
        doc.addEventListener('DOMContentLoaded', init);
    } else if (!root.__timerChipNoAutoInit) {
        init();
    }
})(window);
