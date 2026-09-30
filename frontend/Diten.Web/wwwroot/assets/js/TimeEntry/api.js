/*
 * MOD-0280-FU01 T2a — the one door from the browser to Time Entry: /TimeEntry/api/* on the web tier, which proxies to
 * the gateway (/api/v1/time-entry/*) with the session's token. The browser never sees a token, a tenant id or a
 * service port. Every answer comes back as { ok, status, data, reasonCode } — the reason code is what the page turns
 * into a sentence (TimeEntryCore.failureMessage).
 */
(function (root) {
    'use strict';

    var BASE = '/TimeEntry/api/';

    function readReasonCode(body) {
        if (!body || typeof body !== 'object') {
            return null;
        }
        return body.reason_code || body.reasonCode || null;
    }

    function request(method, path, body) {
        var options = {
            method: method,
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' }
        };
        if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }

        return root.fetch(BASE + path, options).then(function (response) {
            return response.text().then(function (text) {
                var parsed = null;
                try { parsed = text ? JSON.parse(text) : null; } catch (error) { parsed = null; }
                return {
                    ok: response.ok,
                    status: response.status,
                    data: parsed && Object.prototype.hasOwnProperty.call(parsed, 'data') ? parsed.data : parsed,
                    reasonCode: readReasonCode(parsed)
                };
            });
        }, function () {
            return { ok: false, status: 0, data: null, reasonCode: null };
        });
    }

    var enc = encodeURIComponent;

    root.TimeEntryApi = {
        getWeek: function (weekKey) { return request('GET', 'weeks/' + enc(weekKey)); },
        saveEntries: function (weekKey, expectedVersion, entries) {
            return request('PUT', 'weeks/' + enc(weekKey) + '/entries', { expectedVersion: expectedVersion, entries: entries });
        },
        submit: function (weekKey, expectedVersion) {
            return request('POST', 'weeks/' + enc(weekKey) + '/submit', { expectedVersion: expectedVersion });
        },
        withdraw: function (weekKey, expectedVersion) {
            return request('POST', 'weeks/' + enc(weekKey) + '/withdraw', { expectedVersion: expectedVersion });
        },
        requestCorrection: function (weekKey, reason) {
            return request('POST', 'weeks/' + enc(weekKey) + '/corrections', { reason: reason });
        },
        discardCorrection: function (weekKey) {
            return request('DELETE', 'weeks/' + enc(weekKey) + '/corrections/draft');
        },
        planFillIn: function (weekKey) { return request('GET', 'weeks/' + enc(weekKey) + '/plan-fill-in'); },
        acceptSuggestion: function (weekKey, id, expectedVersion) {
            return request('POST', 'weeks/' + enc(weekKey) + '/suggestions/' + enc(id) + '/accept',
                { expectedVersion: expectedVersion, taskItemId: null, categoryCode: null });
        },
        dismissSuggestion: function (weekKey, id) {
            return request('POST', 'weeks/' + enc(weekKey) + '/suggestions/' + enc(id) + '/dismiss', {});
        },
        categories: function () { return request('GET', 'categories'); },
        taskOptions: function (search) {
            return request('GET', 'task-options' + (search ? '?search=' + enc(search) : ''));
        },
        /** The chip's shared read when the chip is on the page (one request per page load, v3 M5); else our own. */
        timer: function () {
            return root.DitenTimerShared ? root.DitenTimerShared.read(false) : request('GET', 'timer');
        },
        stopTimer: function () { return request('POST', 'timer/stop', {}); }
    };
})(typeof window !== 'undefined' ? window : globalThis);
