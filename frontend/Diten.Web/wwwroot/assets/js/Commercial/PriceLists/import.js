/**
 * WP-VW-W6 · W6-WEB-b — the price list import: ① the file + head + columns (multipart to the CRM's staging area) → ② the
 * preview: each count on its own (matched · unmatched · Retired · duplicate · invalid price) and the difference, the rows
 * by status → convert (only the matched rows become the draft; once) → ③ the draft, sent for publishing (four eyes).
 * The preview is resumable: ?id= opens step 2 of a staged import.
 */
'use strict';

const PriceListImport = (function () {
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('pr-import-root');
    const endpoint = PU.api + '/imports';
    const ROW_STATUSES = ['', 'matched', 'unmatched', 'retired', 'duplicate', 'invalid-price', 'removed'];
    let current = null;

    const alertBox = (t) => { const a = byId('pr-alert'); a.textContent = t || ''; a.classList.toggle('d-none', !t); };
    const step = (n) => {
        [1, 2, 3].forEach((i) => byId('pr-step-' + i).classList.toggle('d-none', i !== n));
        document.querySelectorAll('#pr-steps li').forEach((li) => li.classList.toggle('active', Number(li.getAttribute('data-step')) <= n));
    };

    /** Step 2's numbers as chips (each count on its own; the difference only when a list was compared). */
    const countsHtml = (s) => s.counts.map((c) => '<span class="badge bg-label-' + (c.count ? c.tone : 'secondary') + ' pr-count" data-count="' + c.key + '">' + U.esc(U.T(c.key, c.count)) + '</span>').join('');
    const diffHtml = (s) => (s.compared ? s.diff.map((d) => '<span class="badge bg-label-info pr-count" data-count="' + d.key + '">' + U.esc(U.T(d.key + 'Count', d.count)) + '</span>').join('') : '<small class="text-muted">' + U.esc(L().NoCompared) + '</small>');

    const rowsTable = (rows) => '<div class="table-responsive"><table class="table table-sm align-middle mb-0"><thead><tr>'
        + ['ColRowNo', 'ColSourceCode', 'ColSku', 'ColDepotPrice', 'ColPharmacistPrice', 'ColPublicPrice', 'ColVat', 'ColRowStatus'].map((h) => '<th>' + U.esc(L()[h]) + '</th>').join('')
        + '</tr></thead><tbody>' + ((rows || []).map((r) => '<tr><td>' + U.esc(r.rowNo) + '</td><td>' + U.bidi(r.sourceCode || r.sourceGtin || '') + '</td><td>' + U.bidi(r.skuName || r.skuCode || '—') + '</td>'
            + [r.depotPrice, r.pharmacistPurchasePrice, r.publicPrice].map((v) => '<td>' + U.esc(PU.num(v)) + '</td>').join('') + '<td>' + U.esc(PU.num(r.vatRate, 1)) + '</td>'
            + '<td><span class="badge bg-label-' + (r.rowStatus === 'matched' ? 'success' : 'warning') + '">' + U.esc(L()['Row_' + String(r.rowStatus).replace(/-/g, '_')] || r.rowStatus) + '</span>'
            + (r.diffKind ? ' <small class="text-muted">' + U.esc(L()['Diff_' + r.diffKind] || r.diffKind) + '</small>' : '') + '</td></tr>').join('')
            || '<tr><td colspan="8" class="text-muted">' + U.esc(L().NoRows) + '</td></tr>') + '</tbody></table></div>';

    const showPreview = async (id) => {
        const status = byId('pr-row-status').value;
        const r = await U.request('GET', endpoint + '/' + encodeURIComponent(id) + '?page=1&pageSize=100' + (status ? '&rowStatus=' + encodeURIComponent(status) : ''));
        const d = U.data(r);
        if (!d) { alertBox(U.errorText(r)); return; }
        current = d.import;
        const s = K.importSummary(current);
        if (s.converted) { showConverted(s.converted); return; }
        byId('pr-counts').innerHTML = '<span class="badge bg-label-primary">' + U.esc(U.T('Imp_total', s.total)) + '</span>' + countsHtml(s);
        byId('pr-diff-counts').innerHTML = diffHtml(s);
        byId('pr-rows').innerHTML = rowsTable(d.rows);
        byId('pr-rows-more').textContent = d.total > (d.rows || []).length ? U.T('ItemsShown', (d.rows || []).length, d.total) : '';
        byId('pr-convert').disabled = !s.canConvert;
        byId('pr-import-discard').disabled = s.discarded;
        step(2);
    };

    const showConverted = (listId) => {
        byId('pr-converted-text').textContent = L().ConvertedText || '';
        byId('pr-open-draft').setAttribute('href', '/Commercial/Pricing/PriceLists/Details/' + encodeURIComponent(listId));
        byId('pr-submit-draft').classList.toggle('d-none', root.getAttribute('data-can-submit') !== 'true');
        byId('pr-submit-draft').onclick = async () => {
            const l = U.data(await U.request('GET', PU.api + '/price-lists/' + encodeURIComponent(listId)));
            const r = await U.request('POST', PU.api + '/price-lists/' + encodeURIComponent(listId) + '/submit', { expectedVersion: l ? l.version : null });
            if (!r.ok) { alertBox(U.errorText(r)); return; }
            window.location.href = '/Commercial/Pricing/PriceLists/Details/' + encodeURIComponent(listId);
        };
        step(3);
    };

    const upload = async (e) => {
        e.preventDefault();
        const file = byId('pr-file').files[0];
        const currency = byId('pr-currency').value.trim().toUpperCase();
        if (!byId('pr-name').value.trim() || !byId('pr-company').value || !/^[A-Z]{3}$/.test(currency) || !byId('pr-from').value || !file) { alertBox(L().ErrImportHead); return; }
        if (file.size > 5 * 1024 * 1024) { alertBox(window.CommercialRules?.error('price_list_import_invalid') || L().ErrImportHead); return; }
        const form = new FormData();
        form.append('file', file);
        form.append('name', byId('pr-name').value.trim());
        form.append('legalEntityId', byId('pr-company').value);
        form.append('currency', currency);
        form.append('kind', byId('pr-kind').value);
        form.append('validFrom', byId('pr-from').value);
        if (byId('pr-to').value) form.append('validTo', byId('pr-to').value);
        [['sku', 'skuCodeColumn'], ['depot', 'depotPriceColumn'], ['pharmacist', 'pharmacistPurchasePriceColumn'], ['public', 'publicPriceColumn'], ['vat', 'vatRateColumn']]
            .forEach(([id, name]) => { const v = byId('pr-col-' + id).value.trim(); if (v) form.append(name, v); });
        byId('pr-upload').disabled = true;
        const headers = Object.assign({}, (window.DitenDataTable?.getAuthHeaders?.(false)) || {});
        delete headers['Content-Type'];
        const r = await fetch(endpoint, { method: 'POST', credentials: 'same-origin', headers: headers, body: form })
            .then((x) => x.text().then((t) => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (err) { b = null; } return { ok: x.ok, status: x.status, body: b }; }))
            .catch(() => ({ ok: false, status: 0, body: null }));
        byId('pr-upload').disabled = false;
        if (!r.ok) { alertBox(U.errorText(r)); return; }
        alertBox('');
        const imp = U.data(r);
        if (window.history && window.history.replaceState) window.history.replaceState(null, '', '/Commercial/Pricing/PriceLists/Import?id=' + encodeURIComponent(imp.id));
        showPreview(imp.id);
    };

    const init = async () => {
        if (!root || !U || !K || !PU) return;
        const altFormat = L().DateFormat || 'Y-m-d';
        ['pr-from', 'pr-to'].forEach((id) => { const n = byId(id); if (n && typeof n.flatpickr === 'function') { n.flatpickr({ dateFormat: 'Y-m-d', altInput: true, altFormat: altFormat, allowInput: true }); } });
        await PU.fillCompanies(byId('pr-company'), null);
        byId('pr-row-status').innerHTML = ROW_STATUSES.map((s) => '<option value="' + s + '">' + U.esc(s ? (L()['Row_' + s.replace(/-/g, '_')] || s) : L().RowStatusAll) + '</option>').join('');
        byId('pr-row-status').addEventListener('change', () => current && showPreview(current.id));
        byId('pr-step-1').addEventListener('submit', upload);
        byId('pr-convert').addEventListener('click', async () => {
            if (!current) return;
            byId('pr-convert').disabled = true;
            const r = await U.request('POST', endpoint + '/' + encodeURIComponent(current.id) + '/convert', {});
            const list = U.data(r);
            if (!r.ok || !list) { alertBox(U.errorText(r)); byId('pr-convert').disabled = false; return; }
            showConverted(list.id);
        });
        byId('pr-import-discard').addEventListener('click', () => {
            if (!current) return;
            U.confirm(L().DiscardImport, L().DiscardImportConfirm, L().DiscardImport, 'danger', async () => {
                const r = await U.request('POST', endpoint + '/' + encodeURIComponent(current.id) + '/discard', {});
                if (!r.ok) { alertBox(U.errorText(r)); return; }
                window.location.href = '/Commercial/Pricing/PriceLists';
            });
        });
        const id = root.getAttribute('data-id');
        if (id) { showPreview(id); } else { step(1); }
    };

    return { init: init, countsHtml: countsHtml };
})();

if (typeof document !== 'undefined') { document.addEventListener('DOMContentLoaded', () => PriceListImport.init()); }
