/**
 * WP-VW-W4 (W4-WEB-c) — the MDM competitor products pages (mockup "W4 Rakip Urunler" R1–R7 + DESIGN-GAP G1–G6), one
 * script for the separate pages, chosen by #cp-root data-mode:
 *   records / proposals   R1 — the two tabs (filters, the table, the reps' proposals with similar records + their decision state)
 *   create / edit         R2 — identity, competition (our products, indications from the CRM), the country table (+ history, new
 *                         period), the rep's card preview; 409 → "Yenile"
 *   complete              R4 — the same form filled from a proposal; it saves the DECISION ("Kararı kaydet" / "Onaya gönder")
 *   details               R3 — identity, competition, countries + history, change log, report usage (CRM, G2), merge trace,
 *                         "Pazar verisi · yakında"; edit · deactivate · merge
 *   merge / proposal-merge   R5 — 3 steps; a direct merge of two active records ends with "Birleştir", a proposal's with
 *                         "Onaya gönder"; "I read the effect" is required for both
 *   proposal              G1 — the proposal, its decision (draft · in review · returned · applied), Onaya gönder · Geri çek ·
 *                         Onayla · Taslağa geri gönder (a note is required); SoD: the preparer never decides
 * The rules live in competitor-core.js (window.CompetitorCore). Every call is the same-origin proxy (data-api-base).
 */
(function () {
    'use strict';

    const root = document.getElementById('cp-root');
    if (!root) { return; }

    const C = window.CompetitorCore;
    const L = window.CompetitorL10n || {};
    const base = root.getAttribute('data-api-base') || '';
    const pageBase = root.getAttribute('data-base') || '/MasterDataManagement/CompetitorProducts';
    const mode = root.getAttribute('data-mode') || 'records';
    const recordId = root.getAttribute('data-id') || '';
    const proposalId = root.getAttribute('data-proposal-id') || '';
    const actor = root.getAttribute('data-actor') || '';
    const flag = n => root.getAttribute('data-' + n) === 'true';
    const perms = {
        manage: flag('can-manage'), products: flag('can-read-products'), forms: flag('can-read-forms'),
        indications: flag('can-read-indications'), usage: flag('can-read-usage'), people: flag('can-read-people')
    };
    const lang = String((document.documentElement && document.documentElement.lang) || 'tr').toLowerCase().split('-')[0];
    const el = id => document.getElementById(id);
    const esc = v => String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fmt = C.fmt;
    const toast = (m, t) => { if (m && window.showToast) { window.showToast(m, t || 'success'); } };
    const go = url => { window.location.href = url; };

    const state = {
        loading: true, list: null, filters: { productId: '', company: '', ingredient: '', market: '', reimbursement: '', status: '', q: '' }, page: 1,
        proposals: null, record: null, proposal: null, products: [], forms: null, markets: null, reimbursement: [], people: {}, usage: null,
        indications: {}, form: null, original: null, version: null, conflict: false, errors: {}, histOpen: {}, preview: 'TR', addOpen: false,
        merge: { step: 1, q: '', candidates: [], targetId: null, target: null, moveMarketCodes: [], ack: false }, modal: null, dirty: false, busy: false
    };

    // ── the API ──────────────────────────────────────────────────────────────────────────────────────────────
    async function api(method, path, body) {
        const init = { method: method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (body !== undefined) { init.headers['Content-Type'] = 'application/json'; init.body = JSON.stringify(body); }
        try {
            const res = await fetch(base + path, init);
            const text = await res.text();
            let json = null;
            try { json = text ? JSON.parse(text) : null; } catch (e) { json = null; }
            return { ok: res.ok, status: res.status, data: json ? (json.data !== undefined ? json.data : json.Data) : null, errors: (json && (json.errors || json.Errors)) || [] };
        } catch (e) {
            return { ok: false, status: 0, data: null, errors: [] };
        }
    }

    const items = d => (Array.isArray(d) ? d : (d && Array.isArray(d.items) ? d.items : []));
    const errText = r => {
        if (r && r.status === 409 && (r.errors || []).some(e => e === 'concurrency_conflict')) { return L.Err_concurrency_conflict || ''; }
        const t = C.errorText(r && r.errors, L);
        if (t && t !== L.ActionFailed) { return t; }
        if (r && (r.status === 502 || r.status === 503)) { return L.Err_service_unavailable || L.ActionFailed || ''; }
        if (r && r.status === 403) { return L.Err_forbidden || L.ActionFailed || ''; }
        return L.ActionFailed || '';
    };
    const alertBox = text => {
        const box = el('cp-alert');
        if (!box) { return; }
        box.textContent = text || '';
        box.classList.toggle('d-none', !text);
    };

    // ── words ────────────────────────────────────────────────────────────────────────────────────────────────
    const date = v => { if (!v) { return ''; } const d = new Date(v); return isNaN(d.getTime()) ? '' : d.toLocaleDateString(lang, { day: '2-digit', month: '2-digit', year: 'numeric' }); };
    const dateTime = v => { if (!v) { return ''; } const d = new Date(v); return isNaN(d.getTime()) ? '' : d.toLocaleString(lang, { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }); };
    const money = (v, cur) => {
        if (v === null || v === undefined || v === '') { return '—'; }
        const tail = cur ? ' ' + cur : '';
        try { return new Intl.NumberFormat(lang, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(v)) + tail; } catch (e) { return String(v) + tail; }
    };
    let regionNames = null;
    try { regionNames = new Intl.DisplayNames([lang], { type: 'region' }); } catch (e) { regionNames = null; }
    const countryName = c => {
        const m = (state.markets || []).find(x => x.code === c);
        if (regionNames) { try { const n = regionNames.of(c); if (n && n !== c) { return n; } } catch (e) { /* not a region */ } }
        return (m && m.name) || c || '';
    };
    const who = id => (id ? state.people[id] || (L.UserShort ? fmt(L.UserShort, String(id).slice(0, 8)) : String(id).slice(0, 8)) : '—');
    const statusLabel = s => L['Status_' + s] || s || '';
    const statusChip = s => '<span class="badge bg-label-' + C.statusTone(s) + '">' + esc(statusLabel(s)) + '</span>';
    const reimbLabel = c => { const hit = state.reimbursement.find(x => x.code === c); return hit ? hit.label : (c || '—'); };
    const REIMB_TONES = { reimbursed: 'success', reimbursed_conditional: 'warning', not_reimbursed: 'danger', unknown: 'secondary' };
    const reimbChip = c => '<span class="badge bg-label-' + (REIMB_TONES[c] || 'secondary') + '">' + esc(reimbLabel(c)) + '</span>';
    const formLabel = c => (c ? L['Form_' + String(c).replace(/-/g, '_')] || c : '');
    const productName = id => { const p = state.products.find(x => x.id === id); return p ? (p.globalProductName || p.canonicalCode) : (L.UnknownProduct || '—'); };
    const initials = n => String(n || '').split(/\s+/).filter(Boolean).map(z => z[0]).join('').slice(0, 2).toUpperCase();
    const decisionLabel = a => L['Decision_' + a] || a || '';
    const fieldLabel = f => {
        const k = String(f || '');
        if (k.indexOf('countryPrices.') === 0) { return fmt(L.Field_countryPrices || '{0}', k.slice('countryPrices.'.length)); } // a country row (one per market)
        return L['Field_' + k.replace(/\./g, '_')] || k;
    };
    const optionLabelOf = v => (lang !== 'en' && v.attributes && v.attributes['label_' + lang]) || v.displayName || v.valueCode;

    // ── lookups ──────────────────────────────────────────────────────────────────────────────────────────────
    async function loadLookups() {
        const jobs = [];
        if (perms.products) { jobs.push(api('GET', '/products?pageSize=100&pageNumber=1').then(r => { state.products = r.ok ? items(r.data) : []; })); }
        jobs.push(api('GET', '/reference-data/mdm-reimbursement-status/values').then(r => {
            state.reimbursement = r.ok ? items(r.data).filter(v => v.isActive !== false).map(v => ({ code: v.valueCode, label: optionLabelOf(v) })) : [];
        }));
        if (perms.people) {
            jobs.push(api('GET', '/people').then(r => { ((r.ok && r.data && r.data.people) || []).forEach(p => { state.people[p.userId] = p.displayName || p.userId; }); }));
        }
        await Promise.all(jobs);
    }

    async function loadFormLookups() {
        const jobs = [];
        if (perms.forms) { jobs.push(api('GET', '/forms').then(r => { state.forms = r.ok && r.data ? (r.data.dosageForms || (r.data.vocabularies && r.data.vocabularies.dosageForms) || null) : null; })); }
        jobs.push(api('GET', '/reference-data/market/values').then(r => {
            state.markets = r.ok ? items(r.data).filter(v => v.isActive !== false).map(v => ({ code: v.valueCode, name: v.displayName })) : null;
        }));
        await Promise.all(jobs);
    }

    async function loadIndications(productIds) {
        if (!perms.indications) { return; }
        await Promise.all((productIds || []).filter(id => !state.indications[id]).map(id => api('GET', '/indications/' + encodeURIComponent(id)).then(r => {
            const d = r.ok ? r.data : null;
            const seen = {};
            const list = [];
            items(d).forEach(x => { if (!seen[x.indicationNodeId]) { seen[x.indicationNodeId] = true; list.push({ id: x.indicationNodeId, name: x.indicationName }); } });
            state.indications[id] = { items: list, reason: d && d.reason };
        })));
    }

    async function loadUsage(ids) {
        if (!perms.usage || !ids.length) { state.usage = null; return; }
        const r = await api('GET', '/usage?months=3&competitorIds=' + encodeURIComponent(ids.slice(0, 100).join(',')));
        state.usage = r.ok ? r.data : null;
    }

    // ── load per page ────────────────────────────────────────────────────────────────────────────────────────
    async function load() {
        await loadLookups();
        if (mode === 'records') { await loadList(); }
        else if (mode === 'proposals') { await loadProposals(); }
        else if (mode === 'create') { await loadFormLookups(); state.form = emptyForm(); state.original = null; }
        else if (mode === 'edit') { await loadFormLookups(); await loadRecord(); }
        else if (mode === 'complete') { await loadFormLookups(); await loadProposal(); fillFromProposal(); await loadUsage([proposalId]); }
        else if (mode === 'details') { await loadRecord(); await loadUsage([recordId]); if (state.record) { await loadIndications(state.record.competesWithGlobalProductIds); } }
        else if (mode === 'merge') { await loadRecord(); await searchCandidates(); await loadUsage([recordId]); }
        else if (mode === 'proposal-merge') { await loadProposal(); await searchCandidates(); await loadUsage([proposalId]); if (state.merge.targetId) { await loadTarget(); } }
        else if (mode === 'proposal') { await loadProposal(); await loadUsage([proposalId]); }
        if (state.form) { await loadIndications(state.form.competesWithGlobalProductIds); }
        state.loading = false;
        render();
    }

    async function loadList() {
        if (perms.manage && state.proposals === null) {
            const p = await api('GET', '/proposals');
            state.proposals = p.ok ? items(p.data) : [];
        }
        const f = state.filters;
        const q = ['page=' + state.page, 'pageSize=25'];
        Object.keys(f).forEach(k => { if (f[k]) { q.push(encodeURIComponent(k === 'productId' ? 'productId' : k) + '=' + encodeURIComponent(f[k])); } });
        const r = await api('GET', '/records?' + q.join('&'));
        if (!r.ok) { alertBox(errText(r)); state.list = { items: [], total: 0, page: 1, pageSize: 25 }; return; }
        state.list = r.data || { items: [], total: 0 };
        await loadUsage(items(state.list).map(x => x.id));
    }

    async function loadProposals() {
        const p = await api('GET', '/proposals');
        if (!p.ok) { alertBox(errText(p)); }
        state.proposals = p.ok ? items(p.data) : [];
        await loadUsage(state.proposals.map(x => x.proposal.id));
    }

    async function loadRecord() {
        const r = await api('GET', '/records/' + encodeURIComponent(recordId));
        if (!r.ok || !r.data) { alertBox(errText(r)); return; }
        state.record = r.data;
        state.version = r.data.version;
        if (mode === 'edit') {
            state.form = formOf(r.data);
            state.original = formOf(r.data);
        }
    }

    async function loadProposal() {
        const r = await api('GET', '/proposals/' + encodeURIComponent(proposalId));
        if (!r.ok || !r.data) { alertBox(errText(r)); return; }
        state.proposal = r.data;
    }

    const emptyForm = () => ({ name: '', company: '', aliases: [], activeIngredients: [], formCode: '', strength: '', atcCode: '', competesWithGlobalProductIds: [], indicationNodeIds: [], countryPrices: [] });
    const formOf = r => ({
        name: r.name || '', company: r.company || '', aliases: (r.aliases || []).slice(), activeIngredients: (r.activeIngredients || []).slice(),
        formCode: r.formCode || '', strength: r.strength || '', atcCode: r.atcCode || '',
        competesWithGlobalProductIds: (r.competesWithGlobalProductIds || []).slice(), indicationNodeIds: (r.indicationNodeIds || []).slice(),
        countryPrices: (r.countryPrices || []).map(c => ({ marketCode: c.marketCode, boxPrice: c.boxPrice, currencyCode: c.currencyCode, reimbursementStatusCode: c.reimbursementStatusCode, validFrom: String(c.validFrom || '').slice(0, 10), source: c.source || '' }))
    });

    function fillFromProposal() {
        const p = state.proposal;
        if (!p) { state.form = emptyForm(); return; }
        const d = p.decision;
        if (d && d.action === 'complete' && d.complete) { state.form = formOf(d.complete); }
        else { state.form = Object.assign(emptyForm(), { name: p.proposal.name, company: p.proposal.company || '', competesWithGlobalProductIds: (p.proposal.competesWithGlobalProductIds || []).slice() }); }
        state.version = d ? d.version : null;
        state.original = null;
    }

    // ── render ───────────────────────────────────────────────────────────────────────────────────────────────
    function render() {
        if (state.loading) { return; }
        let html = '';
        if (mode === 'records' || mode === 'proposals') { html = listHtml(); }
        else if (mode === 'create' || mode === 'edit' || mode === 'complete') { html = formHtml(); }
        else if (mode === 'details') { html = detailsHtml(); }
        else if (mode === 'merge' || mode === 'proposal-merge') { html = mergeHtml(); }
        else if (mode === 'proposal') { html = proposalHtml(); }
        el('cp-body').innerHTML = html;
        renderModal();
    }

    const crumb = parts => '<div class="cp-crumb">' + parts.map((p, i) => (p.href ? '<a href="' + esc(p.href) + '">' + esc(p.text) + '</a>' : '<span>' + esc(p.text) + '</span>') + (i < parts.length - 1 ? ' / ' : '')).join('') + '</div>';
    const card = (title, inner, extra, head) => '<section class="card cp-card' + (extra ? ' ' + extra : '') + '">' + (title ? '<div class="card-header cp-card-h"><h5 class="mb-0">' + esc(title) + '</h5>' + (head || '') + '</div>' : '') + '<div class="card-body">' + inner + '</div></section>';
    const readOnlyChip = () => (perms.manage ? '' : '<span class="badge bg-label-secondary cp-ro"><i class="bx bx-lock-alt"></i>' + esc(L.ReadOnly || '') + '</span>');

    // ── R1 list ──────────────────────────────────────────────────────────────────────────────────────────────
    function listHtml() {
        const pending = (state.proposals || []).filter(p => !p.decision || p.decision.status !== 'applied').length;
        const head = '<div class="cp-head"><h4 class="cp-title">' + '<span class="cp-muted">' + esc(L.Crumb || '') + ' /</span> ' + esc(L.Title || '') + ' ' + readOnlyChip() + '</h4>'
            + (perms.manage ? '<a class="btn btn-primary" href="' + esc(pageBase + '/Create') + '" data-act="new"><i class="bx bx-plus"></i>' + esc(L.NewButton || '') + '</a>' : '') + '</div>';
        const tabs = '<div class="cp-tabs" role="tablist"><a role="tab" class="cp-tab' + (mode === 'records' ? ' active' : '') + '" href="' + esc(pageBase) + '"><i class="bx bx-target-lock"></i>' + esc(L.TabRecords || '') + '</a>'
            + (perms.manage ? '<a role="tab" class="cp-tab' + (mode === 'proposals' ? ' active' : '') + '" href="' + esc(pageBase + '?tab=proposals') + '"><i class="bx bx-message-square-add"></i>' + esc(L.TabProposals || '')
                + '<span class="cp-count">' + pending + '</span></a>' : '') + '</div>';
        return head + tabs + (mode === 'proposals' ? proposalsHtml() : recordsHtml());
    }

    function recordsHtml() {
        const f = state.filters;
        const sel = (k, label, opts) => '<label class="cp-field"><span>' + esc(label) + '</span><select class="form-select" data-filter="' + k + '"><option value="">' + esc(L.AllOption || '') + '</option>'
            + opts.map(o => '<option value="' + esc(o.v) + '"' + (f[k] === o.v ? ' selected' : '') + '>' + esc(o.t) + '</option>').join('') + '</select></label>';
        const txt = (k, label) => '<label class="cp-field"><span>' + esc(label) + '</span><input class="form-control" data-filter="' + k + '" value="' + esc(f[k]) + '"></label>';
        const marketsSeen = [];
        items(state.list).forEach(r => (r.countryPrices || []).forEach(c => { if (marketsSeen.indexOf(c.marketCode) < 0) { marketsSeen.push(c.marketCode); } }));
        if (f.market && marketsSeen.indexOf(f.market) < 0) { marketsSeen.push(f.market); }
        const filters = '<div class="cp-filters">'
            + (perms.products ? sel('productId', L.FilterOurs || '', state.products.map(p => ({ v: p.id, t: p.globalProductName || p.canonicalCode }))) : '')
            + txt('company', L.FilterCompany || '') + txt('ingredient', L.FilterIngredient || '')
            + sel('market', L.FilterCountry || '', marketsSeen.map(c => ({ v: c, t: countryName(c) + ' (' + c + ')' })))
            + sel('reimbursement', L.FilterReimbursement || '', state.reimbursement.map(x => ({ v: x.code, t: x.label })))
            + sel('status', L.FilterStatus || '', [{ v: 'active', t: statusLabel('active') }, { v: 'inactive', t: statusLabel('inactive') }]) + '</div>'
            + '<div class="cp-toolbar"><div class="cp-search"><i class="bx bx-search"></i><input class="form-control" data-filter="q" placeholder="' + esc(L.SearchPlaceholder || '') + '" value="' + esc(f.q) + '"></div>'
            + '<button type="button" class="btn btn-label-secondary" data-act="export"><i class="bx bx-export"></i>' + esc(L.ExportButton || '') + '</button></div>';
        const rows = items(state.list);
        const anyFilter = Object.keys(f).some(k => f[k]);
        let table;
        if (!rows.length && !anyFilter) {
            table = '<div class="cp-empty"><span class="cp-empty-ic"><i class="bx bx-target-lock"></i></span><h5>' + esc(L.EmptyTitle || '') + '</h5><p>' + esc(L.EmptyText || '') + '</p>'
                + (perms.manage ? '<a class="btn btn-primary" href="' + esc(pageBase + '/Create') + '"><i class="bx bx-plus"></i>' + esc(L.NewButton || '') + '</a>' : '') + '</div>';
        } else if (!rows.length) {
            table = '<div class="cp-noresult">' + esc(L.NoResults || '') + '</div>';
        } else {
            table = '<div class="table-responsive"><table class="table cp-table"><thead><tr>' + ['ColName', 'ColCompany', 'ColIngredient', 'ColForm', 'ColOurs', 'ColCountries', 'ColUsage', 'ColStatus', 'ColLast', 'ColActions']
                .map(k => '<th>' + esc(L[k] || '') + '</th>').join('') + '</tr></thead><tbody>' + rows.map(rowHtml).join('') + '</tbody></table></div>';
        }
        const list = state.list || {};
        const from = rows.length ? ((list.page || 1) - 1) * (list.pageSize || 25) + 1 : 0;
        const to = from ? from + rows.length - 1 : 0;
        const pages = Math.max(1, Math.ceil((list.total || 0) / (list.pageSize || 25)));
        const footer = '<div class="cp-footer"><span>' + esc(fmt(L.FooterText || '{0}–{1} / {2}', from, to, list.total || 0)) + '</span><div class="cp-pager">'
            + '<button type="button" class="btn btn-sm btn-label-secondary" data-act="page" data-page="' + Math.max(1, state.page - 1) + '"' + (state.page <= 1 ? ' disabled' : '') + ' aria-label="' + esc(L.PrevPage || '') + '"><i class="bx bx-chevrons-left cp-flip"></i></button>'
            + '<span class="cp-page">' + state.page + ' / ' + pages + '</span>'
            + '<button type="button" class="btn btn-sm btn-label-secondary" data-act="page" data-page="' + Math.min(pages, state.page + 1) + '"' + (state.page >= pages ? ' disabled' : '') + ' aria-label="' + esc(L.NextPage || '') + '"><i class="bx bx-chevrons-right cp-flip"></i></button></div></div>';
        return card(L.FiltersTitle || '', filters + table + footer, 'cp-list');
    }

    function usageCell(id) {
        if (!perms.usage) { return '<span class="cp-muted" title="' + esc(L.UsageNoAccess || '') + '">—</span>'; }
        const u = C.usageOf(state.usage, id);
        return esc(C.countText(u ? u.reportCount : 0));
    }

    function rowHtml(r) {
        const priced = (r.countryPrices || []).filter(c => c.boxPrice !== null && c.boxPrice !== undefined).length;
        const last = (r.changeLog || [])[0];
        const actions = C.recordActions(r, perms);
        return '<tr><td><div class="cp-namecell"><span class="cp-avatar">' + esc(initials(r.name)) + '</span><div><a class="cp-name" href="' + esc(pageBase + '/' + r.id) + '">' + esc(r.name) + '</a>'
            + ((r.aliases || []).length ? '<div class="cp-small cp-muted">' + esc(L.Aka || '') + ' ' + esc(r.aliases.join(', ')) + '</div>' : '') + '</div></div></td>'
            + '<td>' + esc(r.company) + '</td><td>' + esc((r.activeIngredients || []).join(' + ')) + '</td>'
            + '<td>' + esc(formLabel(r.formCode)) + ' <strong>' + esc(r.strength || '') + '</strong></td>'
            + '<td>' + (r.competesWithGlobalProductIds || []).map(id => '<span class="badge bg-label-primary cp-chip">' + esc(productName(id)) + '</span>').join(' ') + '</td>'
            + '<td><div class="cp-ctry">' + (r.countryPrices || []).map(c => '<span class="cp-cc' + (c.boxPrice === null || c.boxPrice === undefined ? ' unpriced' : '') + '" title="' + esc(countryName(c.marketCode)) + '">' + esc(c.marketCode) + '</span>').join('') + '</div>'
            + '<div class="cp-small cp-muted">' + esc(fmt(L.PricedText || '{0}/{1}', priced, (r.countryPrices || []).length)) + '</div></td>'
            + '<td class="cp-strong">' + usageCell(r.id) + '</td><td>' + statusChip(r.status) + '</td>'
            + '<td><div>' + esc(who(r.updatedBy || r.createdBy)) + '</div><div class="cp-small cp-muted">' + esc(date(last ? last.at : (r.updatedAt || r.createdAt))) + '</div></td>'
            + '<td class="cp-acts"><a class="btn btn-icon btn-sm btn-text-secondary" href="' + esc(pageBase + '/' + r.id) + '" title="' + esc(L.ViewDetails || '') + '"><i class="bx bx-show"></i></a>'
            + (actions.indexOf('edit') > -1 ? '<a class="btn btn-icon btn-sm btn-text-secondary" href="' + esc(pageBase + '/' + r.id + '/Edit') + '" title="' + esc(L.EditButton || '') + '"><i class="bx bx-edit"></i></a>' : '') + '</td></tr>';
    }

    // ── R1 proposals ─────────────────────────────────────────────────────────────────────────────────────────
    function proposalsHtml() {
        const list = (state.proposals || []);
        const body = list.length ? list.map(proposalCard).join('') : '<div class="cp-empty cp-empty-sm"><i class="bx bx-check-double cp-okc"></i><span>' + esc(L.NoProposals || '') + '</span></div>';
        return card(L.ProposalsTitle || '', '<p class="cp-muted cp-small">' + esc(L.ProposalsText || '') + '</p>' + body, 'cp-list');
    }

    function stateChip(s) {
        const map = {
            waiting: ['secondary', L.State_waiting], draft: ['info', fmt(L.State_draft || '{0}', decisionLabel(s.action))],
            in_review: ['warning', fmt(L.State_in_review || '{0} {1}', who(s.by), dateTime(s.at))], applied: ['success', fmt(L.State_applied || '{0} {1}', who(s.by), dateTime(s.at))],
            returned: ['danger', s.reason === 'sod' ? (L.State_returned_sod || '') : s.reason === 'withdrawn' ? (L.State_returned_withdrawn || '') : fmt(L.State_returned || '{0}', s.note || '—')]
        };
        const m = map[s.key] || map.waiting;
        return '<span class="badge bg-label-' + m[0] + ' cp-state" data-state="' + esc(s.key) + '">' + esc(m[1] || '') + '</span>';
    }

    function proposalCard(row) {
        const p = row.proposal;
        const s = C.decisionState(row.decision);
        const u = C.usageOf(state.usage, p.id);
        const sim = (row.similar || []).map(m => '<span class="cp-sim' + (C.similarityTone(m.score) === 'likely' ? ' likely' : '') + '">' + esc(p.name) + ' <i class="bx bx-transfer-alt cp-flip"></i> <strong>' + esc(m.name) + '</strong> <span class="cp-muted">('
            + esc(statusLabel(m.status)) + ', %' + esc(m.score) + ')</span></span>').join('');
        const canPrepare = C.decisionActions(row.decision, perms, actor).indexOf('prepare') > -1;
        const pref = pageBase + '/Proposals/' + p.id;
        return '<div class="cp-prop" data-proposal="' + esc(p.id) + '"><div class="cp-prop-grid">'
            + '<div><span class="cp-k">' + esc(L.PropName || '') + '</span><span><strong>' + esc(p.name) + '</strong> ' + statusChip('proposed') + '</span></div>'
            + '<div><span class="cp-k">' + esc(L.PropCompany || '') + '</span><span>' + esc(p.company || '—') + '</span></div>'
            + '<div><span class="cp-k">' + esc(L.PropBy || '') + '</span><span>' + esc(who(p.proposedBy)) + '</span><span class="cp-small cp-muted">' + esc(date(p.proposedAt)) + '</span></div>'
            + '<div><span class="cp-k">' + esc(L.PropProduct || '') + '</span><span class="badge bg-label-primary cp-chip">' + esc(p.proposedForProductId ? productName(p.proposedForProductId) : '—') + '</span></div>'
            + '<div><span class="cp-k">' + esc(L.PropUsage || '') + '</span><span class="cp-strong">' + esc(perms.usage ? fmt(L.InReports || '{0}', C.countText(u ? u.reportCount : 0)) : '—') + '</span></div></div>'
            + '<div class="cp-prop-foot"><div class="cp-sims"><i class="bx bx-git-compare cp-muted"></i><span class="cp-muted">' + esc(L.SimilarRecords || '') + '</span>' + (sim || '<span>' + esc(L.NoSimilar || '') + '</span>') + '</div>'
            + '<div class="cp-prop-state">' + stateChip(s)
            + (canPrepare ? '<a class="btn btn-sm btn-label-success" href="' + esc(pref + '/Complete') + '"><i class="bx bx-check-circle"></i>' + esc(L.ActComplete || '') + '</a>'
                + '<a class="btn btn-sm btn-label-primary" href="' + esc(pref + '/Merge') + '"><i class="bx bx-git-merge"></i>' + esc(L.ActMerge || '') + '</a>'
                + '<button type="button" class="btn btn-sm btn-label-danger" data-act="reject-open" data-id="' + esc(p.id) + '"><i class="bx bx-x-circle"></i>' + esc(L.ActReject || '') + '</button>' : '')
            + '<a class="btn btn-sm btn-label-secondary" href="' + esc(pref) + '">' + esc(L.OpenDecision || '') + '</a></div></div>'
            + '</div>';
    }

    // ── R2 / R4 form ─────────────────────────────────────────────────────────────────────────────────────────
    function formHtml() {
        const f = state.form;
        if (!f) { return ''; }
        const e = state.errors;
        const isComplete = mode === 'complete';
        const title = mode === 'create' ? L.FormNewTitle : isComplete ? L.FormCompleteTitle : L.FormEditTitle;
        const head = '<div class="cp-head"><div><h4 class="cp-title"><a class="cp-muted" href="' + esc(pageBase) + '">' + esc(L.CrumbFull || '') + ' /</a> ' + esc(title || '') + '</h4>'
            + '<span class="cp-muted">' + esc(isComplete ? (L.FormCompleteSub || '') : mode === 'edit' && state.record ? state.record.name : (L.FormNewSub || '')) + '</span></div><div class="cp-btns">'
            + '<button type="button" class="btn btn-label-secondary" data-act="cancel">' + esc(L.CancelButton || '') + '</button>'
            + (isComplete ? '<button type="button" class="btn btn-outline-primary" data-act="decision-save" data-kind="complete"' + (state.busy ? ' disabled' : '') + '>' + esc(L.SaveDecision || '') + '</button>'
                + '<button type="button" class="btn btn-primary" data-act="decision-send" data-kind="complete"' + (state.busy ? ' disabled' : '') + '><i class="bx bx-send cp-flip"></i>' + esc(L.SendForApproval || '') + '</button>'
                : '<button type="button" class="btn btn-primary" data-act="save"' + (state.busy ? ' disabled' : '') + '><i class="bx bx-save"></i>' + esc(L.SaveButton || '') + '</button>') + '</div></div>';
        let banners = '';
        if (isComplete && state.proposal) {
            const p = state.proposal.proposal;
            const u = C.usageOf(state.usage, p.id);
            banners += '<div class="cp-banner cp-info"><span class="cp-banner-ic"><i class="bx bx-message-square-add"></i></span><div><strong>' + esc(fmt(L.ProposalBanner || '{0} {1} {2}', who(p.proposedBy), date(p.proposedAt), perms.usage ? C.countText(u ? u.reportCount : 0) : '—'))
                + '</strong><div class="cp-small">' + esc(fmt(L.ProposalBannerText || '{0} {1}', p.name, p.company || '—')) + '</div></div></div>';
        }
        if (state.conflict) { banners += conflictBanner(); }
        if (Object.keys(e).length) { banners += '<div class="cp-banner cp-danger"><i class="bx bx-error-circle"></i><strong>' + esc(L.FormErrors || '') + '</strong></div>'; }
        const chips = (key, list, merged) => '<div class="cp-tags">' + list.map((v, i) => '<span class="cp-tag' + (key === 'activeIngredients' ? ' p' : '') + '">' + (merged && merged.indexOf(String(v).toLowerCase()) > -1 ? '<i class="bx bx-git-merge cp-infoc" title="' + esc(L.FromMerge || '') + '"></i>' : '')
            + esc(v) + '<button type="button" data-act="tag-del" data-key="' + key + '" data-i="' + i + '" aria-label="' + esc(L.RemoveLabel || '') + '"><i class="bx bx-x"></i></button></span>').join('')
            + '<input data-tag="' + key + '" placeholder="' + esc(key === 'aliases' ? (L.AliasPlaceholder || '') : (L.IngredientPlaceholder || '')) + '"></div>';
        const mergedNames = ((state.record && state.record.mergedFrom) || []).map(m => String(m.sourceName || '').toLowerCase());
        const forms = state.forms;
        const identity = '<div class="cp-grid2">'
            + '<label class="cp-field"><span>' + esc(L.FieldName || '') + ' <i class="cp-req">*</i></span><input class="form-control" data-form="name" maxlength="200" value="' + esc(f.name) + '"><span class="cp-help"><span>' + esc(L.NameHelp || '') + '</span><span>' + esc(f.name.length) + '/200</span></span>'
            + (e.name ? '<span class="cp-err">' + esc(L.ErrNameRequired || '') + '</span>' : '') + '</label>'
            + '<label class="cp-field"><span>' + esc(L.FieldCompany || '') + ' <i class="cp-req">*</i></span><input class="form-control" data-form="company" maxlength="200" list="cp-companies" value="' + esc(f.company) + '">'
            + '<datalist id="cp-companies">' + companyOptions() + '</datalist>' + (e.company ? '<span class="cp-err">' + esc(L.ErrCompanyRequired || '') + '</span>' : '') + '</label>'
            + '<div class="cp-field cp-span"><span>' + esc(L.FieldAliases || '') + '</span>' + chips('aliases', f.aliases, mergedNames) + '<span class="cp-help">' + esc(L.AliasHelp || '') + '</span></div>'
            + '<div class="cp-field cp-span"><span>' + esc(L.FieldIngredients || '') + ' <i class="cp-req">*</i></span>' + chips('activeIngredients', f.activeIngredients) + '<span class="cp-help">' + esc(L.IngredientHelp || '') + '</span>'
            + (e.ingredients ? '<span class="cp-err">' + esc(L.ErrIngredientsRequired || '') + '</span>' : '') + '</div>'
            + '<label class="cp-field"><span>' + esc(L.FieldForm || '') + '</span><select class="form-select" data-form="formCode"><option value="">' + esc(L.ChooseOption || '') + '</option>'
            + (forms || (f.formCode ? [f.formCode] : [])).map(c => '<option value="' + esc(c) + '"' + (f.formCode === c ? ' selected' : '') + '>' + esc(formLabel(c)) + '</option>').join('') + '</select>'
            + (forms ? '' : '<span class="cp-help">' + esc(L.FormsUnavailable || '') + '</span>') + '</label>'
            + '<div class="cp-grid2 cp-tight"><label class="cp-field"><span>' + esc(L.FieldStrength || '') + '</span><input class="form-control" data-form="strength" maxlength="60" value="' + esc(f.strength) + '" placeholder="5/160 mg"></label>'
            + '<label class="cp-field"><span>' + esc(L.FieldAtc || '') + '</span><input class="form-control" data-form="atcCode" maxlength="16" value="' + esc(f.atcCode) + '" placeholder="C01EB15"></label></div></div>';
        const competition = '<div class="cp-field"><span>' + esc(L.FieldOurs || '') + ' <i class="cp-req">*</i></span><div class="cp-chips">'
            + (perms.products ? state.products.map(p => { const on = f.competesWithGlobalProductIds.indexOf(p.id) > -1; return '<button type="button" class="cp-toggle' + (on ? ' on' : '') + '" data-act="toggle-product" data-id="' + esc(p.id) + '">' + (on ? '<i class="bx bx-check"></i>' : '') + esc(p.globalProductName || p.canonicalCode) + '</button>'; }).join('')
                : '<span class="cp-help">' + esc(L.ProductsNoAccess || '') + '</span>') + '</div><span class="cp-help">' + esc(L.OursHelp || '') + '</span>'
            + (e.products ? '<span class="cp-err">' + esc(L.ErrProductsRequired || '') + '</span>' : '') + '</div>'
            + '<div class="cp-field"><span>' + esc(L.FieldIndications || '') + ' <span class="cp-muted">' + esc(L.Optional || '') + '</span></span>' + indicationChips(f) + '<span class="cp-help">' + esc(L.IndicationHelp || '') + '</span></div>';
        return head + banners + '<div class="cp-split"><div class="cp-main">'
            + card(L.SectionIdentity || '', identity) + card(L.SectionCompetition || '', competition) + countryCard(f) + '</div><div class="cp-side">' + previewCard(f) + '</div></div>';
    }

    function companyOptions() {
        const seen = {};
        return items(state.list).concat(state.record ? [state.record] : []).map(r => r.company).filter(c => c && !seen[c] && (seen[c] = true)).map(c => '<option value="' + esc(c) + '"></option>').join('');
    }

    function indicationChips(f) {
        if (!perms.indications) { return '<span class="cp-help">' + esc(L.IndicationsNoAccess || '') + '</span>'; }
        if (!f.competesWithGlobalProductIds.length) { return '<span class="cp-help">' + esc(L.IndicationsPickProduct || '') + '</span>'; }
        const all = [];
        f.competesWithGlobalProductIds.forEach(id => ((state.indications[id] && state.indications[id].items) || []).forEach(x => { if (!all.some(y => y.id === x.id)) { all.push(x); } }));
        (f.indicationNodeIds || []).filter(id => !all.some(x => x.id === id)).forEach(id => all.push({ id: id, name: L.UnknownIndication || id }));
        if (!all.length) { return '<span class="cp-help cp-disabled">' + esc(L.NoIndications || '') + '</span>'; }
        return '<div class="cp-chips">' + all.map(x => { const on = f.indicationNodeIds.indexOf(x.id) > -1; return '<button type="button" class="cp-pill' + (on ? ' on' : '') + '" data-act="toggle-indication" data-id="' + esc(x.id) + '">' + (on ? '<i class="bx bx-check"></i>' : '') + esc(x.name) + '</button>'; }).join('') + '</div>';
    }

    const original = c => ((state.original && state.original.countryPrices) || []).find(r => r.marketCode === c) || null;
    const historyOf = c => (((state.record && state.record.countryPriceHistory) || []).filter(h => h.marketCode === c)).sort((a, b) => String(b.validTo).localeCompare(String(a.validTo)));

    function countryCard(f) {
        const used = f.countryPrices.map(r => r.marketCode);
        const marketOpts = (state.markets || []).filter(m => used.indexOf(m.code) < 0).map(m => ({ code: m.code, name: countryName(m.code) })).sort((a, b) => a.name.localeCompare(b.name, lang));
        const add = '<div class="cp-add"><button type="button" class="btn btn-sm btn-label-primary" data-act="add-open"><i class="bx bx-plus"></i>' + esc(L.AddCountry || '') + '<i class="bx bx-chevron-down"></i></button>'
            + (state.addOpen ? '<div class="cp-add-menu">' + (state.markets ? '<input class="form-control form-control-sm" data-local="market-q" placeholder="' + esc(L.CountrySearch || '') + '" value="' + esc(state.marketQ || '') + '">'
                + '<div class="cp-add-list">' + marketOpts.filter(m => !state.marketQ || (m.name + ' ' + m.code).toLowerCase().indexOf(String(state.marketQ).toLowerCase()) > -1).slice(0, 40)
                    .map(m => '<button type="button" data-act="add-country" data-code="' + esc(m.code) + '"><span class="cp-cc">' + esc(m.code) + '</span>' + esc(m.name) + '</button>').join('') + '</div>'
                : '<div class="cp-small cp-muted">' + esc(L.MarketsUnavailable || '') + '</div><div class="cp-inline"><input class="form-control form-control-sm" maxlength="2" data-local="market-code" placeholder="TR"><button type="button" class="btn btn-sm btn-primary" data-act="add-country-code">' + esc(L.AddButton || '') + '</button></div>')
                + '</div>' : '') + '</div>';
        const rows = f.countryPrices.length ? f.countryPrices.map((r, i) => {
            const hist = historyOf(r.marketCode);
            const period = C.pricePeriod(original(r.marketCode), r);
            const open = !!state.histOpen[r.marketCode];
            return '<div class="cp-crow"><div class="cp-crow-h"><span class="cp-cc">' + esc(r.marketCode) + '</span><strong>' + esc(countryName(r.marketCode)) + '</strong><span class="cp-muted">· ' + esc(r.currencyCode || '—') + '</span><span class="cp-grow"></span>'
                + (hist.length ? '<button type="button" class="cp-link" data-act="hist" data-code="' + esc(r.marketCode) + '"><i class="bx bx-history"></i>' + esc(fmt(L.HistoryCount || '{0}', hist.length)) + '<i class="bx ' + (open ? 'bx-chevron-up' : 'bx-chevron-down') + '"></i></button>' : '<span class="cp-small cp-muted">' + esc(L.NoHistory || '') + '</span>')
                + '<button type="button" class="btn btn-icon btn-sm btn-text-danger" data-act="row-del" data-i="' + i + '" aria-label="' + esc(L.RemoveRow || '') + '"><i class="bx bx-trash"></i></button></div>'
                + '<div class="cp-crow-b"><label class="cp-field"><span>' + esc(L.BoxPrice || '') + '</span><div class="input-group"><input class="form-control cp-num" inputmode="decimal" data-row="' + i + '" data-k="boxPrice" value="' + esc(r.boxPrice == null ? '' : r.boxPrice) + '" placeholder="0,00">'
                + '<input class="form-control cp-cur" maxlength="3" data-row="' + i + '" data-k="currencyCode" value="' + esc(r.currencyCode || '') + '" aria-label="' + esc(L.CurrencyLabel || '') + '"></div></label>'
                + '<label class="cp-field"><span>' + esc(L.Reimbursement || '') + '</span><select class="form-select" data-row="' + i + '" data-k="reimbursementStatusCode">' + (state.reimbursement.length ? state.reimbursement : [{ code: 'unknown', label: 'unknown' }])
                    .map(x => '<option value="' + esc(x.code) + '"' + (r.reimbursementStatusCode === x.code ? ' selected' : '') + '>' + esc(x.label) + '</option>').join('') + '</select></label>'
                + '<label class="cp-field"><span>' + esc(L.ValidFrom || '') + '</span><input type="date" class="form-control" data-row="' + i + '" data-k="validFrom" value="' + esc(r.validFrom || '') + '"></label>'
                + '<label class="cp-field"><span>' + esc(L.Source || '') + '</span><input class="form-control" maxlength="200" data-row="' + i + '" data-k="source" value="' + esc(r.source || '') + '" placeholder="' + esc(L.SourcePlaceholder || '') + '"></label></div>'
                + (period ? '<div class="cp-period' + (period.dateMustMove ? ' warn' : '') + '"><i class="bx bx-time-five"></i>' + esc(fmt(L.NewPeriod || '{0} {1} {2}', money(period.from, ''), money(period.to, ''), r.currencyCode || '')) + (period.dateMustMove ? ' ' + esc(L.NewPeriodDate || '') : '') + '</div>' : '')
                + (open ? historyHtml(hist, r.currencyCode) : '') + '</div>';
        }).join('') : '<div class="cp-noctry"><i class="bx bx-world"></i><strong>' + esc(L.NoCountries || '') + '</strong><span>' + esc(L.NoCountriesText || '') + '</span></div>';
        const err = state.errors.markets ? '<span class="cp-err">' + esc(L['ErrMarkets_' + state.errors.markets] || '') + '</span>' : '';
        return card(L.SectionCountries || '', '<p class="cp-muted cp-small">' + esc(L.CountriesHelp || '') + '</p>' + rows + err, 'cp-countries', add);
    }

    function historyHtml(hist, cur) {
        return '<div class="cp-hist"><div class="cp-k"><i class="bx bx-lock-alt"></i>' + esc(L.PreviousPeriods || '') + '</div><table class="table table-sm mb-0"><thead><tr><th>' + esc(L.PeriodLabel || '') + '</th><th>' + esc(L.BoxPrice || '') + '</th><th>' + esc(L.Reimbursement || '') + '</th></tr></thead><tbody>'
            + hist.map(h => '<tr><td>' + esc(date(h.validFrom)) + ' – ' + esc(date(h.validTo)) + '</td><td class="cp-strong">' + esc(money(h.boxPrice, h.currencyCode || cur)) + '</td><td>' + esc(reimbLabel(h.reimbursementStatusCode)) + '</td></tr>').join('') + '</tbody></table></div>';
    }

    function previewCard(f) {
        const codes = f.countryPrices.map(r => r.marketCode);
        if (codes.length && codes.indexOf(state.preview) < 0) { state.preview = codes[0]; }
        const row = f.countryPrices.find(r => r.marketCode === state.preview);
        const ind = [];
        f.competesWithGlobalProductIds.forEach(id => ((state.indications[id] && state.indications[id].items) || []).forEach(x => { if (f.indicationNodeIds.indexOf(x.id) > -1 && ind.indexOf(x.name) < 0) { ind.push(x.name); } }));
        return card(L.PreviewTitle || '', '<span class="cp-small cp-muted">' + esc(L.PreviewSub || '') + '</span>'
            + '<select class="form-select" data-local="preview">' + (codes.length ? codes : ['TR']).map(c => '<option value="' + esc(c) + '"' + (c === state.preview ? ' selected' : '') + '>' + esc(fmt(L.PreviewAs || '{0}', countryName(c))) + '</option>').join('') + '</select>'
            + '<div class="cp-repcard"><div class="cp-repcard-h"><span class="cp-rep-ic"><i class="bx bx-target-lock"></i></span><div class="cp-grow"><strong>' + esc(f.name || '—') + '</strong><div class="cp-small cp-muted">' + esc(f.company || '—') + '</div></div><span class="badge bg-label-secondary">' + esc(L.CompetitorTag || '') + '</span></div>'
            + '<dl class="cp-dl"><dt>' + esc(L.FieldIngredients || '') + '</dt><dd>' + esc(f.activeIngredients.join(' + ') || '—') + '</dd><dt>' + esc(L.FormStrength || '') + '</dt><dd>' + esc([formLabel(f.formCode), f.strength].filter(Boolean).join(' · ') || '—') + '</dd>'
            + '<dt>' + esc(L.OurProduct || '') + '</dt><dd>' + esc(f.competesWithGlobalProductIds.map(productName).join(', ') || '—') + '</dd></dl>'
            + (row && row.boxPrice !== null && row.boxPrice !== '' ? '<div class="cp-price"><span class="cp-k">' + esc(fmt(L.PriceIn || '{0}', countryName(row.marketCode))) + '</span><span class="cp-price-v">' + esc(money(row.boxPrice, row.currencyCode)) + '</span>'
                + '<span class="cp-small cp-muted">' + esc(fmt(L.Since || '{0}', date(row.validFrom))) + '</span>' + reimbChip(row.reimbursementStatusCode) + '</div>'
                : '<div class="cp-price cp-muted">' + esc(fmt(L.NoPriceFor || '{0}', countryName(state.preview))) + '</div>')
            + (ind.length ? '<div class="cp-small cp-muted">' + esc(L.ShownIndications || '') + ' <span class="cp-strong">' + esc(ind.join(', ')) + '</span></div>' : '') + '</div>', 'cp-preview');
    }

    function conflictBanner() {
        return '<div class="cp-banner cp-danger" id="cp-conflict"><span class="cp-banner-ic"><i class="bx bx-error"></i></span><div class="cp-grow"><strong>' + esc(L.ConflictTitle || '') + '</strong><div class="cp-small">' + esc(L.ConflictText || '') + '</div></div>'
            + '<button type="button" class="btn btn-danger btn-sm" data-act="refresh"><i class="bx bx-refresh"></i>' + esc(L.RefreshButton || '') + '</button></div>';
    }

    // ── R3 details ───────────────────────────────────────────────────────────────────────────────────────────
    function detailsHtml() {
        const r = state.record;
        if (!r) { return ''; }
        const actions = C.recordActions(r, perms);
        const last = (r.changeLog || [])[0];
        const head = '<div class="cp-head"><div class="cp-dhead"><span class="cp-avatar lg">' + esc(initials(r.name)) + '</span><div><h4 class="cp-title"><a class="cp-muted" href="' + esc(pageBase) + '">' + esc(L.Title || '') + ' /</a> ' + esc(r.name) + ' ' + statusChip(r.status) + ' ' + readOnlyChip() + '</h4>'
            + '<span class="cp-muted">' + esc(fmt(L.DetailSub || '{0} {1} {2}', r.company, who(last ? last.by : r.updatedBy || r.createdBy), date(last ? last.at : r.updatedAt || r.createdAt))) + '</span></div></div><div class="cp-btns">'
            + (actions.indexOf('merge') > -1 ? '<a class="btn btn-outline-secondary" href="' + esc(pageBase + '/' + r.id + '/Merge') + '"><i class="bx bx-git-merge"></i>' + esc(L.MergeOther || '') + '</a>' : '')
            + (actions.indexOf('deactivate') > -1 ? '<button type="button" class="btn btn-label-danger" data-act="deactivate-open"><i class="bx bx-block"></i>' + esc(L.DeactivateButton || '') + '</button>' : '')
            + (actions.indexOf('edit') > -1 ? '<a class="btn btn-primary" href="' + esc(pageBase + '/' + r.id + '/Edit') + '"><i class="bx bx-edit"></i>' + esc(L.EditButton || '') + '</a>' : '') + '</div></div>';
        const merged = r.status === 'merged' && r.survivor ? '<div class="cp-banner cp-info"><i class="bx bx-git-merge"></i><span>' + esc(L.MergedInto || '') + ' <a href="' + esc(pageBase + '/' + r.survivor.id) + '">' + esc(r.survivor.name) + '</a></span></div>' : '';
        const kv = (k, v) => '<div class="cp-kv"><span class="cp-k">' + esc(k) + '</span><span class="cp-v">' + v + '</span></div>';
        const mergedNames = (r.mergedFrom || []).map(m => String(m.sourceName || '').toLowerCase());
        const identity = '<div class="cp-grid3">' + kv(L.FieldName || '', '<strong>' + esc(r.name) + '</strong>') + kv(L.FieldCompany || '', esc(r.company))
            + kv(L.FieldAliases || '', (r.aliases || []).map(a => '<span class="cp-tag">' + (mergedNames.indexOf(a.toLowerCase()) > -1 ? '<i class="bx bx-git-merge cp-infoc"></i>' : '') + esc(a) + '</span>').join(' ') || '—')
            + kv(L.FieldIngredients || '', esc((r.activeIngredients || []).join(' + ') || '—')) + kv(L.FormStrength || '', esc([formLabel(r.formCode), r.strength].filter(Boolean).join(' · ') || '—')) + kv(L.FieldAtc || '', esc(r.atcCode || '—')) + '</div>';
        const competition = '<div class="cp-grid3">' + kv(L.FieldOurs || '', (r.competesWithGlobalProductIds || []).map(id => '<span class="badge bg-label-primary cp-chip">' + esc(productName(id)) + '</span>').join(' ') || '—')
            + kv(L.FieldIndications || '', indicationNames(r)) + '</div>';
        const countries = (r.countryPrices || []).length ? '<div class="table-responsive"><table class="table cp-table"><thead><tr><th>' + esc(L.CountryLabel || '') + '</th><th>' + esc(L.BoxPrice || '') + '</th><th>' + esc(L.Reimbursement || '') + '</th><th>' + esc(L.ValidFrom || '') + '</th><th>' + esc(L.Source || '') + '</th><th></th></tr></thead><tbody>'
            + r.countryPrices.map(c => {
                const hist = historyOf(c.marketCode);
                const open = !!state.histOpen[c.marketCode];
                return '<tr><td><span class="cp-cc">' + esc(c.marketCode) + '</span> ' + esc(countryName(c.marketCode)) + '</td><td class="cp-strong">' + esc(money(c.boxPrice, c.currencyCode)) + '</td><td>' + reimbChip(c.reimbursementStatusCode) + '</td><td>' + esc(date(c.validFrom)) + '</td><td>' + esc(c.source || '') + '</td>'
                    + '<td>' + (hist.length ? '<button type="button" class="cp-link" data-act="hist" data-code="' + esc(c.marketCode) + '">' + esc(fmt(L.HistoryCount || '{0}', hist.length)) + '<i class="bx ' + (open ? 'bx-chevron-up' : 'bx-chevron-down') + '"></i></button>' : '<span class="cp-small cp-muted">' + esc(L.NoHistory || '') + '</span>') + '</td></tr>'
                    + (open ? '<tr class="cp-histrow"><td colspan="6">' + historyHtml(hist, c.currencyCode) + '</td></tr>' : '');
            }).join('') + '</tbody></table></div>' : '<div class="cp-small cp-muted">' + esc(L.NoCountries || '') + '</div>';
        const timeline = (r.changeLog || []).length ? '<ul class="cp-timeline">' + r.changeLog.slice(0, 50).map(c => '<li><div class="cp-tl-h"><strong>' + esc(fieldLabel(c.field)) + '</strong><span class="cp-small cp-muted">' + esc(dateTime(c.at)) + '</span></div>'
            + (c.oldValue || c.newValue ? '<div class="cp-small">' + esc(changeValue(c, c.oldValue) || '—') + ' → ' + esc(changeValue(c, c.newValue) || '—') + '</div>' : '') + '<div class="cp-small cp-muted">' + esc(who(c.by)) + '</div></li>').join('') + '</ul>' : '<div class="cp-small cp-muted">—</div>';
        return head + merged + '<div class="cp-split"><div class="cp-main">' + card(L.SectionIdentity || '', identity) + card(L.SectionCompetition || '', competition) + card(L.SectionCountries || '', countries)
            + card(L.SectionHistory || '', timeline) + '</div><div class="cp-side">' + usageCard(r.id) + mergeTraceCard(r) + '<div class="cp-soon"><i class="bx bx-line-chart"></i><div><strong>' + esc(L.MarketSoon || '') + '</strong><div class="cp-small cp-muted">' + esc(L.MarketSoonText || '') + '</div></div></div></div></div>';
    }

    /** A status change names the statuses in the page language; any other value is shown as stored. */
    const changeValue = (c, v) => (c.field === 'status' && v ? statusLabel(v) : v);

    /** The record's indications by name (CRM Concept Graph, G3); without the CRM read only their number. */
    function indicationNames(r) {
        const ids = r.indicationNodeIds || [];
        if (!ids.length) { return '—'; }
        const all = [];
        (r.competesWithGlobalProductIds || []).forEach(id => ((state.indications[id] && state.indications[id].items) || []).forEach(x => all.push(x)));
        const named = ids.map(id => all.find(x => x.id === id)).filter(Boolean);
        return named.length ? named.map(x => '<span class="cp-pill on">' + esc(x.name) + '</span>').join(' ')
            + (named.length < ids.length ? ' <span class="cp-small cp-muted">' + esc(fmt(L.IndicationCount || '{0}', ids.length - named.length)) + '</span>' : '')
            : esc(fmt(L.IndicationCount || '{0}', ids.length));
    }

    function usageCard(id) {
        if (!perms.usage) { return card(L.SectionUsage || '', '<div class="cp-small cp-muted">' + esc(L.UsageNoAccess || '') + '</div>'); }
        const u = C.usageOf(state.usage, id);
        if (!u) { return card(L.SectionUsage || '', '<div class="cp-small cp-muted">' + esc(state.usage ? (L.UsageNone || '') : (L.UsageUnavailable || '')) + '</div>', '', '<span class="cp-small cp-muted">' + esc(L.LastThreeMonths || '') + '</span>'); }
        const bars = (rows, label, tone) => {
            const max = Math.max.apply(null, rows.map(x => Number(x.count) || 0).concat([1]));
            return rows.map(x => { const n = Number(x.count); const w = isNaN(n) ? 4 : Math.max(4, Math.round(n / max * 100)); return '<div class="cp-bar"><div class="cp-bar-h"><span class="cp-strong">' + esc(label(x)) + '</span><span>' + esc(C.countText(x.count)) + '</span></div><div class="cp-bar-t"><div class="cp-bar-f ' + tone + '" style="width:' + w + '%"></div></div></div>'; }).join('');
        };
        return card(L.SectionUsage || '', '<div class="cp-usage"><div class="cp-stat"><span class="cp-stat-ic p"><i class="bx bx-user-pin"></i></span><div><span class="cp-stat-v">' + esc(C.countText(u.doctorCount)) + '</span><span class="cp-small">' + esc(L.DoctorsLabel || '') + '</span></div></div>'
            + '<div class="cp-stat"><span class="cp-stat-ic i"><i class="bx bx-file"></i></span><div><span class="cp-stat-v">' + esc(C.countText(u.reportCount)) + '</span><span class="cp-small">' + esc(L.ReportsLabel || '') + '</span></div></div></div>'
            + '<div class="cp-k">' + esc(L.ByProduct || '') + '</div>' + bars(u.byProduct || [], x => productName(x.productId), 'p')
            + '<div class="cp-k">' + esc(L.ByCountry || '') + '</div>' + bars(u.byCountry || [], x => (x.country === 'unknown' ? (L.UnknownCountry || '') : countryName(String(x.country).toUpperCase())), 'i')
            + '<div class="cp-lock"><i class="bx bx-lock-alt"></i>' + esc(L.UsagePrivacy || '') + '</div>', '', '<span class="cp-small cp-muted">' + esc(L.LastThreeMonths || '') + '</span>');
    }

    function mergeTraceCard(r) {
        const list = r.mergedFrom || [];
        return card(L.SectionMergeTrace || '', list.length ? list.map(m => '<div class="cp-trace"><span class="cp-trace-ic"><i class="bx bx-git-merge"></i></span><div><div class="cp-strong">"' + esc(m.sourceName) + '"' + (m.sourceCompany ? ' <span class="cp-muted">· ' + esc(m.sourceCompany) + '</span>' : '') + '</div>'
            + '<div class="cp-small cp-muted">' + esc(fmt(L.TraceLine || '{0} {1}', m.proposedBy ? who(m.proposedBy) : '—', date(m.mergedAt))) + '</div></div></div>').join('') : '<div class="cp-small cp-muted">' + esc(L.NoMergeTrace || '') + '</div>');
    }

    // ── R5 merge ─────────────────────────────────────────────────────────────────────────────────────────────
    const mergeSource = () => (mode === 'merge' ? state.record : (state.proposal && state.proposal.proposal));

    async function searchCandidates() {
        const src = mergeSource();
        const id = mode === 'merge' ? recordId : proposalId;
        const r = await api('GET', '/merge-candidates?sourceId=' + encodeURIComponent(id) + (state.merge.q ? '&q=' + encodeURIComponent(state.merge.q) : ''));
        state.merge.candidates = r.ok ? items(r.data) : [];
        if (!r.ok) { alertBox(errText(r)); }
        if (!state.merge.q && src) { state.merge.q = src.name; }
        if (mode === 'proposal-merge' && state.proposal && state.proposal.decision && state.proposal.decision.action === 'merge' && !state.merge.targetId) {
            state.merge.targetId = state.proposal.decision.targetId;
            state.merge.moveMarketCodes = (state.proposal.decision.moveMarketCodes || []).slice();
        }
    }

    async function loadTarget() {
        const m = state.merge;
        if (!m.targetId) { m.target = null; return; }
        const r = await api('GET', '/records/' + encodeURIComponent(m.targetId));
        m.target = r.ok ? r.data : null;
    }

    function mergeHtml() {
        const src = mergeSource();
        if (!src) { return ''; }
        const m = state.merge;
        const proposalMode = mode === 'proposal-merge';
        const u = C.usageOf(state.usage, src.id);
        const head = '<div class="cp-head"><h4 class="cp-title"><a class="cp-muted" href="' + esc(proposalMode ? pageBase + '?tab=proposals' : pageBase + '/' + src.id) + '">' + esc(proposalMode ? (L.CrumbProposals || '') : (L.Title || '') + ' / ' + src.name) + ' /</a> ' + esc(L.MergeTitle || '') + '</h4></div>';
        const banner = '<div class="card cp-card cp-mergesrc"><span class="cp-k">' + esc(L.MergeSource || '') + '</span><strong>' + esc(src.name) + '</strong>' + statusChip(src.status)
            + '<span class="cp-small">' + esc(proposalMode ? fmt(L.MergeSourceLine || '{0} {1} {2}', src.company || '—', who(src.proposedBy), date(src.proposedAt)) : (src.company || '')) + '</span></div>';
        const stepsDef = [['bx-search', L.MergeStepTarget || '', L.MergeStepTargetSub || ''], ['bx-git-compare', L.MergeStepCompare || '', L.MergeStepCompareSub || ''], ['bx-check-shield', L.MergeStepEffect || '', L.MergeStepEffectSub || '']];
        const steps = '<div class="cp-steps">' + stepsDef.map((s, i) => { const n = i + 1; const st = n === m.step ? 'active' : n < m.step ? 'done' : 'todo';
            return '<div class="cp-mstep ' + st + '" data-step="' + n + '"><span class="cp-mstep-ic"><i class="bx ' + (st === 'done' ? 'bx-check' : s[0]) + '"></i></span><div><div class="cp-strong" title="' + esc(fmt(L.StepN || '{0}/3', n)) + '">' + esc(s[1]) + '</div><div class="cp-small cp-muted">' + esc(s[2]) + '</div></div>'
                + (n < 3 ? '<i class="bx bx-chevron-right cp-flip cp-muted"></i>' : '') + '</div>'; }).join('') + '</div>';
        let body = '';
        if (m.step === 1) {
            body = '<div class="cp-mbody"><label class="cp-field"><span>' + esc(L.SearchSurvivor || '') + '</span><div class="cp-search"><i class="bx bx-search"></i><input class="form-control" data-local="merge-q" value="' + esc(m.q) + '"></div></label>'
                + '<div class="cp-k">' + esc(L.SimilarBySimilarity || '') + '</div>'
                + (m.candidates.length ? m.candidates.map(c => '<button type="button" class="cp-cand' + (m.targetId === c.id ? ' on' : '') + '" data-act="pick-target" data-id="' + esc(c.id) + '"><span class="cp-radio"></span><span class="cp-grow"><strong>' + esc(c.name) + '</strong> ' + statusChip(c.status)
                    + '<span class="cp-small cp-muted d-block">' + esc(c.company || '') + '</span></span><span class="cp-simbar"><span class="cp-simbar-t"><span class="cp-simbar-f' + (C.similarityTone(c.score) === 'likely' ? ' likely' : '') + '" style="width:' + Math.max(2, Number(c.score) || 0) + '%"></span></span><strong>%' + esc(c.score) + '</strong></span></button>').join('')
                    : '<div class="cp-small cp-muted">' + esc(L.NoCandidates || '') + '</div>') + '</div>'
                + '<div class="cp-mfoot"><button type="button" class="btn btn-label-secondary" data-act="cancel">' + esc(L.CancelButton || '') + '</button><button type="button" class="btn btn-primary" data-act="merge-next"' + (C.mergeCanAdvance(1, m) ? '' : ' disabled') + '>' + esc(L.NextButton || '') + '<i class="bx bx-chevron-right cp-flip"></i></button></div>';
        } else if (m.step === 2) {
            const t = m.target || {};
            const diff = C.mergeDiffRows(src, t, fieldLabel).map(d => (d.field === 'formCode' ? Object.assign(d, { a: formLabel(d.a), b: formLabel(d.b) }) : d));
            const markets = C.mergeMarketRows(src, t);
            body = '<div class="cp-mbody"><span>' + esc(L.CompareHelp || '') + '</span><div class="table-responsive"><table class="table cp-table cp-cmp"><thead><tr><th>' + esc(L.FieldColumn || '') + '</th><th><i class="bx bx-log-out-circle cp-wac"></i>' + esc(fmt(L.WillMerge || '{0}', src.name)) + '</th><th><i class="bx bx-shield-quarter cp-okc"></i>' + esc(fmt(L.Survivor || '{0}', t.name || '')) + '</th></tr></thead><tbody>'
                + diff.map(d => '<tr class="' + (d.diff ? 'diff' : '') + '"><td class="cp-strong">' + esc(d.label) + (d.diff ? ' <span class="badge bg-label-warning">' + esc(L.DifferentTag || '') + '</span>' : '') + '</td><td>' + esc(d.a || '—') + '</td><td class="cp-strong">' + esc(d.b || '—') + '</td></tr>').join('') + '</tbody></table></div>'
                + '<div><h6 class="mb-1">' + esc(L.CountryRows || '') + '</h6><span class="cp-small cp-muted">' + esc(L.CountryRowsHelp || '') + '</span></div>'
                + (markets.length ? '<div class="table-responsive"><table class="table cp-table cp-cmp"><thead><tr><th>' + esc(L.CountryLabel || '') + '</th><th>' + esc(L.WillMergeShort || '') + '</th><th>' + esc(L.SurvivorShort || '') + '</th><th>' + esc(L.MoveColumn || '') + '</th></tr></thead><tbody>'
                    + markets.map(x => '<tr class="' + (x.movable ? 'diff' : '') + '"><td><span class="cp-cc">' + esc(x.code) + '</span> ' + esc(countryName(x.code)) + '</td><td>' + esc(x.source ? money(x.source.boxPrice, x.source.currencyCode) + ' · ' + reimbLabel(x.source.reimbursementStatusCode) : (L.NoneLabel || '—')) + '</td>'
                        + '<td class="cp-strong">' + esc(x.target ? money(x.target.boxPrice, x.target.currencyCode) + ' · ' + reimbLabel(x.target.reimbursementStatusCode) : (L.NoneLabel || '—')) + '</td><td>'
                        + (x.movable ? '<label class="cp-check"><input type="checkbox" data-act-change="move" data-code="' + esc(x.code) + '"' + (m.moveMarketCodes.indexOf(x.code) > -1 ? ' checked' : '') + '>' + esc(L.MoveLabel || '') + '</label>' : '<span class="cp-small cp-muted"><i class="bx bx-lock-alt"></i>' + esc(x.target ? (L.KeptLabel || '') : (L.NoneLabel || '')) + '</span>') + '</td></tr>').join('') + '</tbody></table></div>'
                    : '<div class="cp-small cp-muted">' + esc(L.NoCountries || '') + '</div>') + '</div>'
                + '<div class="cp-mfoot"><button type="button" class="btn btn-label-secondary" data-act="merge-back"><i class="bx bx-chevron-left cp-flip"></i>' + esc(L.BackButton || '') + '</button><button type="button" class="btn btn-primary" data-act="merge-next"' + (C.mergeCanAdvance(2, m) ? '' : ' disabled') + '>' + esc(L.NextButton || '') + '<i class="bx bx-chevron-right cp-flip"></i></button></div>';
        } else {
            const t = m.target || {};
            const moved = m.moveMarketCodes.map(countryName).join(', ');
            const usage = perms.usage ? C.countText(u ? u.reportCount : 0) : '—';
            const effect = (ic, text) => '<div class="cp-effect"><span class="cp-effect-ic"><i class="bx ' + ic + '"></i></span><span>' + esc(text) + '</span></div>';
            const enabled = C.mergeSubmitEnabled(m);
            body = '<div class="cp-mbody"><h6 class="mb-0">' + esc(L.EffectTitle || '') + '</h6>'
                + effect('bx-link', fmt(L.EffectReports || '{0} {1}', usage, t.name || '')) + effect('bx-purchase-tag', fmt(L.EffectAlias || '{0}', src.name))
                + effect('bx-world', moved ? fmt(L.EffectMove || '{0}', moved) : (L.EffectNoMove || '')) + effect('bx-git-merge', proposalMode ? (L.EffectStatusProposal || '') : (L.EffectStatusRecord || ''))
                + (proposalMode ? effect('bx-check-shield', L.EffectApproval || '') : '')
                + '<div class="cp-banner cp-danger cp-tight"><i class="bx bx-error"></i><strong>' + esc(L.Irreversible || '') + '</strong></div>'
                + '<label class="cp-check cp-ack"><input type="checkbox" data-act-change="ack"' + (m.ack ? ' checked' : '') + '>' + esc(L.AckText || '') + '</label></div>'
                + '<div class="cp-mfoot"><button type="button" class="btn btn-label-secondary" data-act="merge-back"><i class="bx bx-chevron-left cp-flip"></i>' + esc(L.BackButton || '') + '</button><div class="cp-btns">'
                + '<button type="button" class="btn btn-outline-secondary" data-act="cancel">' + esc(L.CancelButton || '') + '</button>'
                + (proposalMode ? '<button type="button" class="btn btn-outline-primary" data-act="decision-save" data-kind="merge"' + (enabled && !state.busy ? '' : ' disabled') + '>' + esc(L.SaveDecision || '') + '</button>'
                    + '<button type="button" class="btn btn-primary" data-act="decision-send" data-kind="merge"' + (enabled && !state.busy ? '' : ' disabled') + '><i class="bx bx-send cp-flip"></i>' + esc(L.SendForApproval || '') + '</button>'
                    : '<button type="button" class="btn btn-danger" data-act="merge-do"' + (enabled && !state.busy ? '' : ' disabled') + '><i class="bx bx-git-merge"></i>' + esc(L.MergeButton || '') + '</button>') + '</div></div>';
        }
        return head + banner + '<div class="card cp-card cp-merge">' + steps + body + '</div>';
    }

    // ── G1 the proposal + its decision ───────────────────────────────────────────────────────────────────────
    function proposalHtml() {
        const pr = state.proposal;
        if (!pr) { return ''; }
        const p = pr.proposal;
        const d = pr.decision;
        const s = C.decisionState(d);
        const acts = C.decisionActions(d, perms, actor);
        const u = C.usageOf(state.usage, p.id);
        const pref = pageBase + '/Proposals/' + p.id;
        const head = '<div class="cp-head"><h4 class="cp-title"><a class="cp-muted" href="' + esc(pageBase + '?tab=proposals') + '">' + esc(L.CrumbProposals || '') + ' /</a> ' + esc(p.name) + ' ' + statusChip(p.status) + '</h4></div>';
        const applied = p.status !== 'proposed' ? '<div class="cp-banner cp-ok"><i class="bx bx-check-circle"></i><span>' + esc(fmt(L.AppliedLine || '{0} {1} {2}', decisionLabel(d && d.action), who(d && d.appliedBy), dateTime(d && d.appliedAt)))
            + (p.status === 'merged' && p.survivor ? ' → <a href="' + esc(pageBase + '/' + p.survivor.id) + '">' + esc(p.survivor.name) + '</a>' : p.status === 'active' ? ' → <a href="' + esc(pageBase + '/' + p.id) + '">' + esc(p.name) + '</a>' : '') + '</span></div>' : '';
        const facts = '<div class="cp-prop-grid"><div><span class="cp-k">' + esc(L.PropCompany || '') + '</span><span>' + esc(p.company || '—') + '</span></div>'
            + '<div><span class="cp-k">' + esc(L.PropBy || '') + '</span><span>' + esc(who(p.proposedBy)) + '</span><span class="cp-small cp-muted">' + esc(date(p.proposedAt)) + '</span></div>'
            + '<div><span class="cp-k">' + esc(L.PropProduct || '') + '</span><span>' + esc(p.proposedForProductId ? productName(p.proposedForProductId) : '—') + '</span></div>'
            + '<div><span class="cp-k">' + esc(L.PropUsage || '') + '</span><span class="cp-strong">' + esc(perms.usage ? fmt(L.InReports || '{0}', C.countText(u ? u.reportCount : 0)) : '—') + '</span></div></div>'
            + '<div class="cp-sims"><i class="bx bx-git-compare cp-muted"></i><span class="cp-muted">' + esc(L.SimilarRecords || '') + '</span>' + ((pr.similar || []).map(m => '<a class="cp-sim' + (C.similarityTone(m.score) === 'likely' ? ' likely' : '') + '" href="' + esc(pageBase + '/' + m.id) + '"><strong>' + esc(m.name) + '</strong> <span class="cp-muted">(' + esc(statusLabel(m.status)) + ', %' + esc(m.score) + ')</span></a>').join('') || '<span>' + esc(L.NoSimilar || '') + '</span>') + '</div>';
        const summary = d ? decisionSummary(d) : '<div class="cp-small cp-muted">' + esc(L.NoDecision || '') + '</div>';
        const buttons = [];
        if (acts.indexOf('prepare') > -1 && p.status === 'proposed') {
            buttons.push('<a class="btn btn-sm btn-label-success" href="' + esc(pref + '/Complete') + '"><i class="bx bx-check-circle"></i>' + esc(L.ActComplete || '') + '</a>',
                '<a class="btn btn-sm btn-label-primary" href="' + esc(pref + '/Merge') + '"><i class="bx bx-git-merge"></i>' + esc(L.ActMerge || '') + '</a>',
                '<button type="button" class="btn btn-sm btn-label-danger" data-act="reject-open" data-id="' + esc(p.id) + '"><i class="bx bx-x-circle"></i>' + esc(L.ActReject || '') + '</button>');
        }
        if (acts.indexOf('submit') > -1) { buttons.push('<button type="button" class="btn btn-sm btn-primary" data-act="submit"><i class="bx bx-send cp-flip"></i>' + esc(L.SendForApproval || '') + '</button>'); }
        if (acts.indexOf('withdraw') > -1) { buttons.push('<button type="button" class="btn btn-sm btn-outline-warning" data-act="withdraw"><i class="bx bx-undo"></i>' + esc(L.WithdrawButton || '') + '</button>'); }
        if (acts.indexOf('approve') > -1) { buttons.push('<button type="button" class="btn btn-sm btn-success" data-act="approve"><i class="bx bx-check"></i>' + esc(L.ApproveButton || '') + '</button>'); }
        if (acts.indexOf('return') > -1) { buttons.push('<button type="button" class="btn btn-sm btn-warning" data-act="return-open"><i class="bx bx-undo"></i>' + esc(L.ReturnToDraft || '') + '</button>'); }
        const sod = C.sodNote(d, perms, actor) ? '<div class="cp-sod"><i class="bx bx-user-x"></i>' + esc(L.SodNote || '') + '</div>' : '';
        const band = '<div class="cp-band" data-state="' + esc(s.key) + '">' + stateChip(s) + '<span class="cp-grow"></span><span class="cp-btns">' + buttons.join('') + '</span></div>';
        const history = d ? historyList(d) : '';
        return head + applied + '<div class="cp-split"><div class="cp-main">' + card(L.SectionProposal || '', facts) + card(L.SectionDecision || '', band + sod + summary) + '</div><div class="cp-side">' + (history ? card(L.SectionDecisionHistory || '', history) : '') + '</div></div>';
    }

    function decisionSummary(d) {
        const kv = (k, v) => '<div class="cp-kv"><span class="cp-k">' + esc(k) + '</span><span class="cp-v">' + v + '</span></div>';
        let inner = kv(L.DecisionKind || '', '<strong>' + esc(decisionLabel(d.action)) + '</strong>') + kv(L.PreparedBy || '', esc(who(d.updatedBy || d.createdBy)) + ' <span class="cp-small cp-muted">' + esc(dateTime(d.updatedAt)) + '</span>');
        if (d.action === 'complete' && d.complete) {
            const c = d.complete;
            inner += kv(L.FieldName || '', esc(c.name)) + kv(L.FieldCompany || '', esc(c.company)) + kv(L.FieldIngredients || '', esc((c.activeIngredients || []).join(' + ') || '—'))
                + kv(L.FormStrength || '', esc([formLabel(c.formCode), c.strength].filter(Boolean).join(' · ') || '—')) + kv(L.FieldOurs || '', esc((c.competesWithGlobalProductIds || []).map(productName).join(', ') || '—'))
                + kv(L.SectionCountries || '', esc((c.countryPrices || []).map(r => r.marketCode + ' ' + money(r.boxPrice, r.currencyCode) + ' · ' + reimbLabel(r.reimbursementStatusCode)).join(' | ') || '—'));
        } else if (d.action === 'merge') {
            inner += kv(L.MergeTarget || '', d.target ? '<a href="' + esc(pageBase + '/' + d.target.id) + '">' + esc(d.target.name) + '</a> ' + statusChip(d.target.status) : '—')
                + kv(L.MovedCountries || '', esc((d.moveMarketCodes || []).map(countryName).join(', ') || (L.NoneLabel || '—'))) + '<div class="cp-small cp-muted">' + esc(L.MergeEffectShort || '') + '</div>';
        } else {
            inner += kv(L.RejectReason || '', esc(d.reason || '—'));
        }
        return '<div class="cp-grid2">' + inner + '</div>';
    }

    function historyList(d) {
        const ev = [];
        (d.reviewRounds || []).forEach(r => {
            ev.push({ at: r.submittedAt, text: fmt(L.HistSubmitted || '{0} {1}', r.roundNo, who(r.submittedBy)) });
            if (r.closedAt) { ev.push({ at: r.closedAt, text: fmt(L['HistRound_' + String(r.outcome || 'closed').replace(/-/g, '_')] || L.HistRound_closed || '{0}', who(r.completedBy)) + (r.reasonCode && /_sod$/.test(r.reasonCode) ? ' · ' + (L.State_returned_sod || '') : '') }); }
        });
        (d.decisions || []).forEach(x => ev.push({ at: x.at, text: fmt(L['HistDecision_' + x.outcome] || '{0}', who(x.by)) + (x.comment ? ' · “' + x.comment + '”' : '') }));
        ev.sort((a, b) => String(b.at).localeCompare(String(a.at)));
        return ev.length ? '<ul class="cp-timeline">' + ev.map(e => '<li><div class="cp-small">' + esc(e.text) + '</div><div class="cp-small cp-muted">' + esc(dateTime(e.at)) + '</div></li>').join('') + '</ul>' : '';
    }

    // ── modals ───────────────────────────────────────────────────────────────────────────────────────────────
    function renderModal() {
        const box = el('cp-modal');
        if (!box) { return; }
        const m = state.modal;
        box.classList.toggle('d-none', !m);
        if (!m) { box.innerHTML = ''; return; }
        let inner = '';
        if (m.kind === 'reject') {
            const p = ((state.proposals || []).find(x => x.proposal.id === m.id) || state.proposal || {}).proposal || {};
            inner = '<h5>' + esc(fmt(L.RejectTitle || '{0}', p.name || '')) + '</h5><span class="cp-small cp-muted">' + esc(who(p.proposedBy)) + ' · ' + esc(date(p.proposedAt)) + '</span>'
                + '<label class="cp-field"><span>' + esc(L.RejectReason || '') + ' <i class="cp-req">*</i></span><textarea class="form-control" rows="4" maxlength="' + C.MAX_REASON + '" data-local="reason" placeholder="' + esc(L.RejectPlaceholder || '') + '">' + esc(m.text || '') + '</textarea>'
                + '<span class="cp-help">' + (m.error ? '<span class="cp-err">' + esc(m.error === 'too_long' ? (L.ErrTooLong || '') : (L.ErrReasonRequired || '')) + '</span>' : '<span></span>') + '<span>' + esc(String(m.text || '').length) + '/' + C.MAX_REASON + '</span></span></label>'
                + '<div class="cp-note"><i class="bx bx-info-circle cp-infoc"></i>' + esc(L.RejectInfo || '') + '</div>'
                + '<div class="cp-modal-f"><button type="button" class="btn btn-label-secondary" data-act="modal-close">' + esc(L.CancelButton || '') + '</button><button type="button" class="btn btn-outline-danger" data-act="decision-save" data-kind="reject">' + esc(L.SaveDecision || '') + '</button>'
                + '<button type="button" class="btn btn-danger" data-act="decision-send" data-kind="reject"><i class="bx bx-send cp-flip"></i>' + esc(L.SendForApproval || '') + '</button></div>';
        } else if (m.kind === 'return') {
            inner = '<h5>' + esc(L.ReturnTitle || '') + '</h5><label class="cp-field"><span>' + esc(L.ReturnNote || '') + ' <i class="cp-req">*</i></span><textarea class="form-control" rows="4" maxlength="' + C.MAX_NOTE + '" data-local="note">' + esc(m.text || '') + '</textarea>'
                + (m.error ? '<span class="cp-err">' + esc(L.ErrNoteRequired || '') + '</span>' : '') + '</label>'
                + '<div class="cp-modal-f"><button type="button" class="btn btn-label-secondary" data-act="modal-close">' + esc(L.CancelButton || '') + '</button><button type="button" class="btn btn-warning" data-act="return-do">' + esc(L.ReturnToDraft || '') + '</button></div>';
        } else if (m.kind === 'deactivate') {
            inner = '<h5>' + esc(fmt(L.DeactivateTitle || '{0}', state.record ? state.record.name : '')) + '</h5><p>' + esc(L.DeactivateText || '') + '</p>'
                + '<div class="cp-modal-f"><button type="button" class="btn btn-label-secondary" data-act="modal-close">' + esc(L.CancelButton || '') + '</button><button type="button" class="btn btn-danger" data-act="deactivate-do"><i class="bx bx-block"></i>' + esc(L.DeactivateButton || '') + '</button></div>';
        } else if (m.kind === 'leave') {
            inner = '<h5>' + esc(L.LeaveTitle || '') + '</h5><p>' + esc(L.LeaveText || '') + '</p><div class="cp-modal-f"><button type="button" class="btn btn-label-secondary" data-act="modal-close">' + esc(L.StayButton || '') + '</button><button type="button" class="btn btn-danger" data-act="leave-do">' + esc(L.LeaveButton || '') + '</button></div>';
        }
        box.innerHTML = '<div class="cp-modal" role="dialog" aria-modal="true">' + inner + '</div>';
    }

    // ── actions ──────────────────────────────────────────────────────────────────────────────────────────────
    async function saveRecord() {
        const v = C.validate(state.form);
        state.errors = v.errors;
        if (!v.ok) { render(); return; }
        state.busy = true; render();
        const body = C.writeBody(state.form, mode === 'edit' ? state.version : null);
        const r = mode === 'edit' ? await api('PUT', '/records/' + encodeURIComponent(recordId), body) : await api('POST', '/records', body);
        state.busy = false;
        if (r.ok) {
            state.dirty = false;
            toast(L.Saved || '');
            go(pageBase + '/' + (mode === 'edit' ? recordId : r.data));
            return;
        }
        if (r.status === 409 && (r.errors || []).indexOf('concurrency_conflict') > -1) { state.conflict = true; render(); return; }
        if ((r.errors || []).indexOf('competitor_duplicate') > -1) { state.errors = Object.assign({}, state.errors, { name: 'duplicate' }); }
        alertBox(errText(r));
        render();
    }

    async function saveDecision(kind, send) {
        const pid = proposalId || (state.modal && state.modal.id);
        let input;
        if (kind === 'complete') { input = { form: state.form }; }
        else if (kind === 'merge') { input = { targetId: state.merge.targetId, moveMarketCodes: state.merge.moveMarketCodes, ack: state.merge.ack }; }
        else { input = { reason: state.modal && state.modal.text }; }
        const version = proposalVersion(pid);
        const b = C.decisionBody(kind, input, version);
        if (!b.ok) {
            if (kind === 'complete') { state.errors = b.errors || {}; render(); }
            else if (kind === 'reject') { state.modal.error = b.error === 'reason_too_long' ? 'too_long' : 'required'; render(); }
            return;
        }
        state.busy = true; render();
        const r = await api('PUT', '/proposals/' + encodeURIComponent(pid) + '/decision', b.body);
        if (!r.ok) {
            state.busy = false;
            if (r.status === 409 && (r.errors || []).indexOf('concurrency_conflict') > -1) { state.conflict = true; }
            alertBox(errText(r)); render(); return;
        }
        if (send) {
            const s = await api('POST', '/proposals/' + encodeURIComponent(pid) + '/decision/submit');
            if (!s.ok) {
                // the draft is saved; the approval could not start (e.g. 409 workflow_template_missing) — say so and stay
                state.busy = false;
                alertBox(errText(s));
                toast(L.DecisionSaved || '', 'info');
                if (mode === 'proposals') { state.modal = null; state.proposals = null; await loadProposals(); }
                render();
                return;
            }
        }
        state.busy = false;
        state.dirty = false;
        state.modal = null;
        toast(send ? (L.Sent || '') : (L.DecisionSaved || ''));
        go(pageBase + '/Proposals/' + pid);
    }

    function proposalVersion(pid) {
        const row = state.proposal && state.proposal.proposal.id === pid ? state.proposal : (state.proposals || []).find(x => x.proposal.id === pid);
        return row && row.decision ? row.decision.version : null;
    }

    async function decisionCall(path, body) {
        state.busy = true;
        const r = await api('POST', '/proposals/' + encodeURIComponent(proposalId) + '/decision/' + path, body);
        state.busy = false;
        if (!r.ok) { alertBox(errText(r)); render(); return false; }
        state.modal = null;
        alertBox('');
        await loadProposal();
        render();
        return true;
    }

    async function doMerge() {
        const b = C.mergeBody(state.merge, state.version);
        if (!b.ok) { return; }
        state.busy = true; render();
        const r = await api('POST', '/records/' + encodeURIComponent(recordId) + '/merge', b.body);
        state.busy = false;
        if (r.ok) { toast(L.Merged || ''); go(pageBase + '/' + state.merge.targetId); return; }
        if (r.status === 409 && (r.errors || []).indexOf('concurrency_conflict') > -1) { alertBox(L.Err_concurrency_conflict || ''); }
        else { alertBox(errText(r)); }
        render();
    }

    async function deactivate() {
        const r = await api('POST', '/records/' + encodeURIComponent(recordId) + '/deactivate', { expectedVersion: state.version });
        state.modal = null;
        if (!r.ok) { alertBox(errText(r)); render(); return; }
        toast(L.Deactivated || '');
        await loadRecord();
        render();
    }

    let filterTimer = null;
    function setFilter(k, v, immediate) {
        state.filters[k] = v;
        state.page = 1;
        if (filterTimer) { clearTimeout(filterTimer); }
        const run = async () => { await loadList(); render(); };
        if (immediate) { run(); } else { filterTimer = setTimeout(run, 350); }
    }

    function exportCsv() {
        const rows = items(state.list);
        const head = ['ColName', 'ColCompany', 'ColIngredient', 'ColForm', 'ColOurs', 'ColCountries', 'ColStatus'].map(k => L[k] || k);
        const lines = [head].concat(rows.map(r => [r.name, r.company, (r.activeIngredients || []).join(' + '), [formLabel(r.formCode), r.strength].filter(Boolean).join(' '),
            (r.competesWithGlobalProductIds || []).map(productName).join(' | '), (r.countryPrices || []).map(c => c.marketCode).join(' '), statusLabel(r.status)]));
        const csv = lines.map(l => l.map(x => '"' + String(x == null ? '' : x).replace(/"/g, '""') + '"').join(';')).join('\r\n');
        try {
            const a = document.createElement('a');
            a.href = 'data:text/csv;charset=utf-8,' + encodeURIComponent('﻿' + csv);
            a.download = 'competitor-products.csv';
            a.click();
        } catch (e) { /* no DOM download in the smoke */ }
        state.lastExport = csv;
    }

    const leave = url => { if (state.dirty) { state.modal = { kind: 'leave', url: url }; render(); } else { go(url); } };
    const backUrl = () => (mode === 'complete' || mode === 'proposal-merge' ? pageBase + '/Proposals/' + proposalId : mode === 'edit' || mode === 'merge' ? pageBase + '/' + recordId : pageBase);

    root.addEventListener('click', async e => {
        const t = e.target && e.target.closest ? e.target.closest('[data-act]') : null;
        if (!t) { return; }
        const act = t.getAttribute('data-act');
        const f = state.form;
        switch (act) {
            case 'export': exportCsv(); break;
            case 'page': state.page = parseInt(t.getAttribute('data-page'), 10) || 1; await loadList(); render(); break;
            case 'cancel': leave(backUrl()); break;
            case 'save': await saveRecord(); break;
            case 'refresh': state.conflict = false; state.errors = {}; await loadRecord(); render(); break;
            case 'tag-del': { const k = t.getAttribute('data-key'); f[k].splice(parseInt(t.getAttribute('data-i'), 10), 1); state.dirty = true; render(); break; }
            case 'toggle-product': {
                const id = t.getAttribute('data-id');
                const at = f.competesWithGlobalProductIds.indexOf(id);
                if (at > -1) { f.competesWithGlobalProductIds.splice(at, 1); } else { f.competesWithGlobalProductIds.push(id); await loadIndications([id]); }
                state.dirty = true; render(); break;
            }
            case 'toggle-indication': { const id = t.getAttribute('data-id'); const at = f.indicationNodeIds.indexOf(id); if (at > -1) { f.indicationNodeIds.splice(at, 1); } else { f.indicationNodeIds.push(id); } state.dirty = true; render(); break; }
            case 'add-open': state.addOpen = !state.addOpen; render(); break;
            case 'add-country': case 'add-country-code': {
                const code = act === 'add-country' ? t.getAttribute('data-code') : String(state.marketCode || '').toUpperCase();
                const rows = C.addMarket(f.countryPrices, code, defaultCurrency(code), new Date(Date.now()).toISOString().slice(0, 10));
                if (rows) { f.countryPrices = rows; state.addOpen = false; state.preview = String(code).toUpperCase(); state.dirty = true; state.errors.markets = undefined; delete state.errors.markets; }
                else { state.errors = Object.assign({}, state.errors, { markets: 'duplicate' }); }
                render(); break;
            }
            case 'row-del': f.countryPrices.splice(parseInt(t.getAttribute('data-i'), 10), 1); state.dirty = true; render(); break;
            case 'hist': { const c = t.getAttribute('data-code'); state.histOpen[c] = !state.histOpen[c]; render(); break; }
            case 'decision-save': await saveDecision(t.getAttribute('data-kind'), false); break;
            case 'decision-send': await saveDecision(t.getAttribute('data-kind'), true); break;
            case 'reject-open': state.modal = { kind: 'reject', id: t.getAttribute('data-id'), text: '' }; render(); break;
            case 'modal-close': state.modal = null; render(); break;
            case 'submit': await decisionCall('submit'); break;
            case 'withdraw': await decisionCall('withdraw'); break;
            case 'approve': {
                const b = C.reviewBody('approve', null);
                if (b.ok && C.decisionActions(state.proposal.decision, perms, actor).indexOf('approve') > -1) { await decisionCall('approve-reject', b.body); }
                break;
            }
            case 'return-open': state.modal = { kind: 'return', text: '' }; render(); break;
            case 'return-do': {
                const b = C.reviewBody('return', state.modal && state.modal.text);
                if (!b.ok) { state.modal.error = b.error; render(); break; }
                await decisionCall('approve-reject', b.body);
                break;
            }
            case 'deactivate-open': state.modal = { kind: 'deactivate' }; render(); break;
            case 'deactivate-do': await deactivate(); break;
            case 'leave-do': { const url = state.modal.url; state.dirty = false; state.modal = null; go(url); break; }
            case 'pick-target': state.merge.targetId = t.getAttribute('data-id'); state.merge.moveMarketCodes = []; state.merge.ack = false; render(); break;
            case 'merge-next': {
                const m = state.merge;
                if (!C.mergeCanAdvance(m.step, m)) { break; }
                if (m.step === 1) { await loadTarget(); }
                m.step = Math.min(3, m.step + 1);
                render(); break;
            }
            case 'merge-back': state.merge.step = Math.max(1, state.merge.step - 1); state.merge.ack = false; render(); break;
            case 'merge-do': await doMerge(); break;
        }
    });

    const CURRENCIES = { TR: 'TRY', AE: 'AED', AZ: 'AZN', SA: 'SAR', US: 'USD', GB: 'GBP', RU: 'RUB', KZ: 'KZT', GE: 'GEL', UA: 'UAH', CN: 'CNY', EG: 'EGP' };
    const EURO = ['DE', 'FR', 'IT', 'ES', 'NL', 'BE', 'AT', 'PT', 'IE', 'FI', 'GR', 'SK', 'SI', 'EE', 'LV', 'LT', 'LU', 'MT', 'CY', 'HR'];
    const defaultCurrency = c => { const k = String(c || '').toUpperCase(); return CURRENCIES[k] || (EURO.indexOf(k) > -1 ? 'EUR' : ''); };

    root.addEventListener('input', e => {
        const t = e.target;
        if (!t || !t.getAttribute) { return; }
        const local = t.getAttribute('data-local');
        if (local === 'reason' || local === 'note') { state.modal.text = t.value; return; } // validated on the button (no re-draw while typing)
        if (local === 'market-q') { state.marketQ = t.value; return; }
        if (local === 'market-code') { state.marketCode = t.value; return; }
        if (local === 'merge-q') { state.merge.q = t.value; return; }
        const filter = t.getAttribute('data-filter');
        if (filter && t.tagName !== 'SELECT') { setFilter(filter, t.value, false); return; }
        const k = t.getAttribute('data-form');
        if (k && state.form) { state.form[k] = t.value; state.dirty = true; return; }
        const row = t.getAttribute('data-row');
        if (row !== null && row !== undefined && state.form) { state.form.countryPrices[parseInt(row, 10)][t.getAttribute('data-k')] = t.value; state.dirty = true; }
    });

    root.addEventListener('change', async e => {
        const t = e.target;
        if (!t || !t.getAttribute) { return; }
        const filter = t.getAttribute('data-filter');
        if (filter) { setFilter(filter, t.value, true); return; }
        const local = t.getAttribute('data-local');
        if (local === 'preview') { state.preview = t.value; render(); return; }
        if (local === 'market-q') { render(); return; }
        if (local === 'merge-q') { await searchCandidates(); render(); return; }
        const act = t.getAttribute('data-act-change');
        if (act === 'ack') { state.merge.ack = !!t.checked; render(); return; }
        if (act === 'move') {
            const c = t.getAttribute('data-code');
            const list = state.merge.moveMarketCodes;
            const at = list.indexOf(c);
            if (t.checked && at < 0) { list.push(c); } else if (!t.checked && at > -1) { list.splice(at, 1); }
            render(); return;
        }
        const k = t.getAttribute('data-form');
        if (k && state.form) { state.form[k] = t.value; state.dirty = true; render(); return; }
        const row = t.getAttribute('data-row');
        if (row !== null && row !== undefined && state.form) {
            const r = state.form.countryPrices[parseInt(row, 10)];
            const key = t.getAttribute('data-k');
            r[key] = key === 'currencyCode' ? String(t.value || '').toUpperCase() : t.value;
            state.dirty = true; render();
        }
    });

    root.addEventListener('keydown', e => {
        const t = e.target;
        const key = t && t.getAttribute ? t.getAttribute('data-tag') : null;
        if (!key || (e.key !== 'Enter' && e.key !== ',')) { return; }
        if (e.preventDefault) { e.preventDefault(); }
        state.form[key] = C.addTag(state.form[key], t.value, key === 'activeIngredients' ? C.MAX_INGREDIENTS : C.MAX_ALIASES);
        state.dirty = true;
        render();
    });

    if (window.addEventListener) {
        window.addEventListener('beforeunload', ev => { if (state.dirty) { ev.preventDefault(); ev.returnValue = ''; } });
    }

    load();
}());
