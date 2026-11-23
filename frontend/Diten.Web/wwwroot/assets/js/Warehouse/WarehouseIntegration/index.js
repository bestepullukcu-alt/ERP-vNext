/**
 * MOD-0178 DL-7 — Entegrasyon İzleme (WAREHOUSEINTEGRATION) — the four cards are drawn by the SERVER (Index.cshtml); this script only shows every
 * <time data-wh-time datetime="…"> in the reader's own local time (the server writes UTC). No fetch, no loading state, no write — not a DataTable
 * (pack §11.1 r23).
 */
'use strict';

const WarehouseIntegrationCards = (function () {
    /** The local text of an RFC 3339 instant, or null when it is not one (the server's UTC text then stays). */
    const local = (value) => {
        if (!value) return null;
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? null : date.toLocaleString(window.CurrentLanguage || undefined);
    };

    const localize = (root) => {
        (root || document).querySelectorAll('time[data-wh-time]').forEach((el) => {
            const text = local(el.getAttribute('datetime'));
            if (text) el.textContent = text;
        });
    };

    return { local, localize };
})();

window.WarehouseIntegrationCards = WarehouseIntegrationCards;
document.addEventListener('DOMContentLoaded', () => WarehouseIntegrationCards.localize(document));
