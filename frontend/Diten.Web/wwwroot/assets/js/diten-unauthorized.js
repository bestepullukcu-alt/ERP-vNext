/**
 * MOD-0029-FU04C — shared unauthorized (401/403 / PERM_DENIED) UX helper for AJAX/action requests.
 * Navigation/download 403s are turned into the friendly Not Authorized page by the MVC proxy; this helper covers
 * the fetch/AJAX path so a denied action shows a Sneat/Bootstrap toast instead of a raw JSON envelope.
 *
 *   window.DitenUnauthorized.isUnauthorized(res, json)  → boolean
 *   window.DitenUnauthorized.handle(res, json)          → shows toast + returns true if it was a 401/403/PERM_DENIED
 *   window.DitenUnauthorized.show(message?, correlationId?)
 */
(function () {
    'use strict';

    var l10n = function () { return window.L10n || {}; };
    var text = function (key, fallback) { var v = l10n()[key]; return (typeof v === 'string' && v) ? v : fallback; };

    function statusOf(res, json) {
        if (res && typeof res.status === 'number') return res.status;
        if (json && (json.statusCode || json.StatusCode)) return Number(json.statusCode || json.StatusCode);
        return 0;
    }
    function reasonOf(json) {
        return json && (json.reason_code || json.reasonCode || json.ReasonCode || '');
    }
    function correlationOf(json) {
        return json && (json.correlation_id || json.correlationId || json.CorrelationId || '');
    }

    function isUnauthorized(res, json) {
        var status = statusOf(res, json);
        return status === 401 || status === 403 || String(reasonOf(json)).toUpperCase() === 'PERM_DENIED';
    }

    function show(message, correlationId) {
        var msg = message || text('NotAuthorizedActionMessage', 'You are not authorized to perform this action.');
        var corr = correlationId ? ' (' + text('CorrelationId', 'Correlation ID') + ': ' + correlationId + ')' : '';
        if (typeof window.showToast === 'function') window.showToast(msg + corr, 'error');
        else if (window.Swal && typeof window.Swal.fire === 'function') window.Swal.fire({ icon: 'error', title: msg, text: corr });
        else window.alert(msg + corr);
    }

    window.DitenUnauthorized = {
        isUnauthorized: isUnauthorized,
        // Returns true (and shows the toast) when the response is a 401/403/PERM_DENIED; false otherwise so the
        // caller can fall back to its normal error handling.
        handle: function (res, json) {
            if (!isUnauthorized(res, json)) return false;
            show(null, correlationOf(json));
            return true;
        },
        show: show
    };
})();
