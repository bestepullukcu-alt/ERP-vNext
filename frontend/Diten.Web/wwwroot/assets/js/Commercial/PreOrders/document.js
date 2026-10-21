/**
 * WP-VW-W6 · W6-WEB-a — E: the depot document of one order. The button group chooses the copy (depot ↔ pharmacy); the
 * viewer and the download are the same W6-BE-c2 endpoint (…/document?copy=…; &download=true = an attachment with the
 * CRM's file name). The trail is the order's transmission attempts. A copy that cannot be produced (the CRM's refusal,
 * e.g. the order is outside the caller's scope) is said in words and the viewer stays empty.
 */
'use strict';

const PreOrderDocument = (function () {
    const U = window.PreOrdersUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('po-root');
    const endpoint = '/Commercial/PreOrders/api';

    /** The PDF url of a copy (the twin of PreOrdersController.DocumentPdf). */
    const pdfUrl = (id, copy, download) => endpoint + '/orders/' + encodeURIComponent(id) + '/document?copy=' + encodeURIComponent(copy === 'pharmacy' ? 'pharmacy' : 'depot') + (download ? '&download=true' : '');

    const alertBox = (text) => { const a = byId('po-alert'); a.textContent = text || ''; a.classList.toggle('d-none', !text); };

    const show = async (id, copy) => {
        // the PDF is read once through fetch so a refusal is said in words (an iframe would draw the error body)
        const r = await window.fetch(pdfUrl(id, copy, false), { credentials: 'same-origin' }).catch(() => null);
        const frame = byId('po-pdf');
        if (!r || !r.ok) {
            let body = null;
            try { body = r ? await r.json() : null; } catch (e) { body = null; }
            alertBox(U.errorText({ ok: false, status: r ? r.status : 0, body: body }));
            frame.removeAttribute('src');
            return;
        }
        alertBox('');
        const blob = await r.blob();
        if (frame.dataset.url) { URL.revokeObjectURL(frame.dataset.url); }
        const url = URL.createObjectURL(blob);
        frame.dataset.url = url;
        frame.src = url;
        byId('po-download').setAttribute('href', pdfUrl(id, copy, true));
    };

    const init = async () => {
        if (!root || !U) return;
        const id = root.getAttribute('data-order-id');
        const r = await U.request('GET', endpoint + '/orders/' + encodeURIComponent(id));
        const o = U.data(r);
        if (!o) { alertBox(U.errorText(r)); return; }
        byId('po-title').innerHTML = U.Th('DocumentTitleOrder', o.code);
        byId('po-trail').innerHTML = U.trailHtml(o);
        document.querySelectorAll('input[name="po-copy"]').forEach((n) => n.addEventListener('change', () => { if (n.checked) { show(id, n.value); } }));
        show(id, 'depot');
    };

    return { init: init, pdfUrl: pdfUrl };
})();

if (typeof document !== 'undefined') { document.addEventListener('DOMContentLoaded', () => PreOrderDocument.init()); }
