/**
 * WP-VW-W3E (W3E-WEB-a, Q2) — the QR print page: single (A4 / A5) or the label sheet (4 / 8 on A4). The sheet shows a decoy
 * until "Print"; then the codes are asked (the Web server draws the SVG, no-store; the CRM writes the printed event with the
 * paper and the copies), the browser prints, and after printing the codes leave the page again (the decoy comes back).
 * The label names come from the bulk page (sessionStorage "vq-print-meta": id → name / parent), else the print answer.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('vq-print-root');
    if (!root || !window.VisitQrCore) { return; }
    const C = window.VisitQrCore;
    const L = window.VisitQrL10n || {};
    const api = root.getAttribute('data-api-base') || '/CRM/VisitQrCodes/api';
    const decoy = root.getAttribute('data-decoy') || '';
    const accountId = root.getAttribute('data-account-id') || '';
    const ids = (root.getAttribute('data-ids') || '').split(',').filter(Boolean);
    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => String(t == null ? '' : t).replace(/\{(\d+)\}/g, (m, i) => (a[+i] == null ? '' : String(a[+i])));
    const lang = () => ((document.documentElement && document.documentElement.lang) || 'tr').trim() || 'tr';
    const today = () => {
        try { return new Intl.DateTimeFormat(/^en(-|$)/i.test(lang()) ? 'en-GB' : lang(), { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date()); } catch (e) { return new Date().toISOString().slice(0, 10); }
    };
    const meta = (() => { try { return JSON.parse(window.sessionStorage.getItem('vq-print-meta') || '{}') || {}; } catch (e) { return {}; } })();
    const brandText = (() => {
        const t = el('vq-brand-template');
        const n = t && t.content ? t.content.cloneNode(true) : null;
        if (!n) { return ''; }
        const text = (n.textContent || '').replace(/\s+/g, ' ').trim();
        const img = n.querySelector && n.querySelector('img');
        return text || (img && img.getAttribute('alt')) || '';
    })();

    const singleId = accountId || (ids.length === 1 ? ids[0] : '');
    const labelIds = ids.length ? ids : (accountId ? [accountId] : []);
    const s = { mode: root.getAttribute('data-mode') === 'labels' ? 'labels' : 'single', single: 'a4', labels: 'labels8', version: null, busy: false };

    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    const decoyImg = cls => '<img class="vq-decoy-img ' + (cls || '') + '" src="' + esc(decoy) + '" alt="" />';
    function fail(text) { el('vq-print-error-text').textContent = text || ''; show('vq-print-error', !!text); }

    function labelHtml(item) {
        return '<div class="vq-sticker">'
            + '<div class="vq-l-parent"><bdi>' + esc(item.parent || '') + '</bdi></div>'
            + '<div class="vq-l-name"><bdi>' + esc(item.name || '—') + '</bdi></div>'
            + '<div class="vq-l-qr">' + (item.svg || decoyImg('vq-decoy')) + '</div>'
            + '<div class="vq-l-text">' + esc(L.StickerText) + '</div>'
            + '<div class="vq-l-foot">' + esc([brandText, item.version ? fmt(L.LabelFooter, item.version, today()) : today()].filter(Boolean).join(' · ')) + '</div>'
            + '</div>';
    }

    function renderLabels(items) {
        const grid = C.labelGrid(s.labels);
        const pages = [];
        for (let i = 0; i < items.length; i += grid.perPage) { pages.push(items.slice(i, i + grid.perPage)); }
        el('vq-label-pages').innerHTML = pages.map(p => '<div class="vq-label-sheet vq-rows-' + grid.rows + '">' + p.map(labelHtml).join('') + '</div>').join('');
        el('vq-cut-text').textContent = fmt(L.CutHint, grid.perPage);
    }

    const previewItems = () => labelIds.map(id => ({ id: id, name: (meta[id] && meta[id].name) || null, parent: (meta[id] && meta[id].parent) || null }));

    function render() {
        const single = s.mode === 'single';
        el('vq-mode-single').classList.toggle('vq-on', single);
        el('vq-mode-labels').classList.toggle('vq-on', !single);
        show('vq-paper-single', single);
        show('vq-paper-labels', !single);
        // U-2: the paper is a select in the form field standard (the same values as before: a4 / a5, labels4 / labels8)
        el('vq-paper-single-select').value = s.single;
        el('vq-paper-labels-select').value = s.labels;
        el('vq-print-hint').textContent = single ? (L.HintSingle || '') : (L.HintLabels || '');
        show('vq-single', single);
        show('vq-labels', !single);
        const sheet = el('vq-single');
        sheet.classList.toggle('vq-sheet-a4', s.single === 'a4');
        sheet.classList.toggle('vq-sheet-a5', s.single === 'a5');
        el('vq-s-qr').innerHTML = decoyImg('vq-decoy');
        el('vq-s-foot').textContent = s.version ? fmt(L.StickerFooter, s.version, today()) : '';
        if (!single) { renderLabels(previewItems()); }
        el('vq-print-go').disabled = s.busy || (single ? !singleId : labelIds.length === 0);
        if (!single && labelIds.length === 0) { fail(L.NoSelection || ''); }
    }

    const call = (method, path, body) => window.fetch(api + path, {
        method: method,
        credentials: 'same-origin',
        cache: 'no-store',
        headers: Object.assign({ Accept: 'application/json' }, body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    }).then(r => r.text().then(t => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));

    /** After the print dialog: the codes leave the page (the decoy comes back). */
    function clearCodes() { render(); }

    function print() {
        if (s.busy) { return; }
        fail(''); show('vq-print-skipped', false);
        const copies = C.copiesOf(el('vq-copies').value);
        el('vq-copies').value = copies;
        s.busy = true; render();
        const request = s.mode === 'single'
            ? call('GET', '/accounts/' + encodeURIComponent(singleId) + '/print?paper=' + s.single + '&copies=' + copies)
            : call('POST', '/bulk-print', { accountIds: labelIds, paper: s.labels, copies: copies });
        request.then(r => {
            s.busy = false; render();
            if (!r.ok || !r.body || r.body.data == null) { fail(L[C.errorKey(r)] || L.Err_generic || ''); return; }
            if (s.mode === 'single') {
                const d = r.body.data;
                el('vq-s-qr').innerHTML = d.svg || '';
                if (!el('vq-s-name').textContent.replace('—', '').trim() && d.accountName) { el('vq-s-name').innerHTML = '<bdi>' + esc(d.accountName) + '</bdi>'; }
                el('vq-s-foot').textContent = fmt(L.StickerFooter, d.version, today());
            } else {
                const rows = Array.isArray(r.body.data) ? r.body.data : [];
                if (!rows.length) { fail(L.NothingToPrint || ''); return; }
                const byId = {};
                rows.forEach(x => { byId[String(x.accountId).toLowerCase()] = x; });
                const items = labelIds.map(id => byId[String(id).toLowerCase()]).filter(Boolean).map(x => ({
                    name: (meta[x.accountId] && meta[x.accountId].name) || x.accountName, parent: (meta[x.accountId] && meta[x.accountId].parent) || null,
                    version: x.version, svg: x.svg
                }));
                renderLabels(items);
                const skipped = labelIds.length - items.length;
                if (skipped > 0) { el('vq-print-skipped-text').textContent = fmt(L.SkippedNote, skipped); show('vq-print-skipped', true); }
            }
            window.setTimeout(() => window.print(), 50);
        });
    }

    el('vq-mode-single').addEventListener('click', () => { if (singleId) { s.mode = 'single'; fail(''); render(); } });
    el('vq-mode-labels').addEventListener('click', () => { s.mode = 'labels'; fail(''); render(); });
    ['vq-paper-single-select', 'vq-paper-labels-select'].forEach(id => el(id).addEventListener('change', e => {
        const p = e.target.value;
        if (C.SINGLE_PAPERS.indexOf(p) > -1) { s.single = p; } else if (C.LABEL_PAPERS.indexOf(p) > -1) { s.labels = p; }
        render();
    }));
    el('vq-print-go').addEventListener('click', print);
    window.addEventListener('afterprint', clearCodes);

    render();
    // the single sticker's version for the preview footer (the state read — never the payload)
    if (singleId) {
        call('GET', '/accounts/' + encodeURIComponent(singleId)).then(r => {
            if (r.ok && r.body && r.body.data && r.body.data.status === 'active') { s.version = r.body.data.version; render(); }
        });
    }
}(window, document));
