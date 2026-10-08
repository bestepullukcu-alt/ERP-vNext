/**
 * WP-VP-4C — the Targets tab's product layer and selection summary (K-7, S-1..S-4), plugged into the 4B page skeleton.
 *
 *   - Products column: per doctor, the chips of the next visit's product list (preview `content[].products`; colour =
 *     role, icon = source, a warning on a promo product without approved content), else the plan's stored pick
 *     (session `selectedContacts[].products`); none ⇒ "no product" + "Pick products". A legend explains the chips.
 *   - Doctor panel, Products tab: suggested (play) products locked — role too (S-3); the rep's own picks with a role
 *     toggle; catalogue search (GET /CRM/VisitPlanning/api/products); the role limits "N / max" and the visit time
 *     VisitMinutes(p, n) from the period capacity; "Done" writes ONLY that doctor's products through the existing session
 *     update (every other doctor goes without `products` ⇒ null = keep, D9). S-1: unapproved next visits only.
 *   - Bulk apply: the same panel for the selected doctors; the picked products are ADDED to each doctor's own (union,
 *     S-2); doctors whose list then exceeds a limit are counted in the message (the extra waits for the next visit).
 *   - Summary: this week's time by products + its share of the week (preview weekCapacity), product distribution,
 *     doctors without a product.
 * The Targets "Save" (details.js) still sends no products. An approved / past week (or a legacy plan) is read-only.
 * details.js tells this file about the doctor table ('targets:doctors-drawn') and the local selection
 * ('targets:selection'); the session and the preview come from the page skeleton.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('visit-planning-details');
    const page = window.VisitPlanningPage || null;
    if (!root || !page) return;

    const L = window.L10n || {};
    const base = '/CRM/VisitPlanning/api';
    const capacityBase = '/CRM/CycleCapacities/api';
    const sessionId = root.dataset.sessionId;
    const canGenerate = root.dataset.canGenerate === 'true';
    const readOnly = root.dataset.readOnly === 'true';

    const ROLE_PROMO = 'promo', ROLE_NON_PROMO = 'non-promo';
    const SOURCE_PLAY = 'play', SOURCE_REP_PICK = 'rep-pick';
    const SOURCE_ICON = { play: 'bx-bulb', 'rep-pick': 'bx-user-check', 'last-visit': 'bx-history', portfolio: 'bx-briefcase' };
    const NO_CONTENT_WARNINGS = ['no_approved_content', 'ambiguous_journey'];
    const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
    const LOCKED_WEEK_STATUSES = ['approved', 'past'];

    const el = id => document.getElementById(id);
    const esc = s => { const d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };
    const bidi = s => window.VisitPlanningFormat.bidi(s); // WP-VP-4I (7) — data names isolated (<bdi>) for RTL
    const fmt = (tpl, ...args) => args.reduce((t, a, i) => t.split('{' + i + '}').join(String(a)), String(tpl || ''));
    const request = (url, options) => {
        options = options || {};
        options.credentials = 'same-origin';
        options.headers = Object.assign({ Accept: 'application/json', 'Content-Type': 'application/json' }, options.headers || {});
        return fetch(url, options).then(r => r.text().then(text => {
            let body = null; try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
            return { ok: r.ok, status: r.status, body };
        }));
    };
    const errorText = r => (r.body && r.body.errors && r.body.errors.length) ? r.body.errors.join(' · ') : (r.body && r.body.message) || ('HTTP ' + r.status);

    // ── the rule shared with details.js: an approved / past week (or a legacy plan) is read-only ──
    const targetsLocked = () => readOnly || page.isLegacy() || LOCKED_WEEK_STATUSES.indexOf(page.weekStatus(page.state.weekStart)) > -1;
    const canEdit = () => canGenerate && !targetsLocked();

    // ── pure product-list rules (the picker, the bulk apply and the chips use only these) ──
    const roleOf = item => (item && item.role === ROLE_NON_PROMO ? ROLE_NON_PROMO : ROLE_PROMO); // null role reads as promo (K-7d)
    const countRoles = items => items.reduce((c, it) => { if (roleOf(it) === ROLE_PROMO) c.promo++; else c.nonPromo++; return c; }, { promo: 0, nonPromo: 0 });
    // CycleCapacity.VisitMinutes(p, n) = p × PromoProductTime + n × NonPromoProductTime + the per-visit report minutes.
    const visitMinutes = (cap, p, n) => (cap && cap.promoMinutes != null) ? p * cap.promoMinutes + n * cap.nonPromoMinutes + cap.reportMinutes : null;
    const overLimit = (cap, counts) => !!(cap && cap.maxPromo != null && (counts.promo > cap.maxPromo || counts.nonPromo > cap.maxNonPromo));
    // S-2 — bulk apply ADDS: every product the doctor already has stays (with its own role); a picked product the doctor
    // does not have yet is appended. Never a replacement.
    const unionPicks = (existing, chosen) => {
        const merged = (existing || []).map(p => ({ productId: p.productId, productCode: p.productCode, productName: p.productName, role: roleOf(p) }));
        (chosen || []).forEach(p => { if (!merged.some(m => m.productId === p.productId)) merged.push({ productId: p.productId, productCode: p.productCode, productName: p.productName, role: roleOf(p) }); });
        return merged;
    };
    // S-3 — a suggested product (from the play) can neither be removed nor have its role changed here.
    const isLocked = item => !!(item && item.locked);
    const pickInput = p => ({ productId: p.productId, productCode: p.productCode || null, role: roleOf(p) });
    const contactInput = c => ({ contactId: c.contactId, accountId: c.accountId || null, accountContactLinkId: c.accountContactLinkId || null });

    // ── data: the stored picks (session), the next visit's list (preview), the period capacity, the local selection ──
    const savedContacts = () => (page.state.session && Array.isArray(page.state.session.selectedContacts)) ? page.state.session.selectedContacts : [];
    const savedPicks = cid => { const c = savedContacts().find(x => x.contactId === cid); return c && Array.isArray(c.products) ? c.products : []; };
    let nextVisit = {};   // contactId -> [{ productId, productCode, role, source, noContent }]
    let selection = { doctors: [], accountCount: 0, pharmacyCount: 0 };
    let capacity = null;  // { maxPromo, maxNonPromo, promoMinutes, nonPromoMinutes, reportMinutes } — null until read
    let capacityPeriod = null;

    const readNextVisits = p => {
        const map = {};
        ((p && p.content) || []).forEach(c => {
            if (!c || !c.contactId) return;
            const noContent = {};
            (c.items || []).forEach(it => {
                if (!it || !it.productId) return;
                const warned = (it.warnings || []).some(w => NO_CONTENT_WARNINGS.indexOf(w) > -1);
                if (warned || !it.journeyId || it.journeyId === EMPTY_GUID) noContent[it.productId] = true;
            });
            map[c.contactId] = (c.products || []).map(x => ({ productId: x.productId, productCode: x.productCode, productName: x.productName, role: roleOf(x), source: x.source || SOURCE_PLAY, noContent: !!noContent[x.productId] }));
        });
        return map;
    };
    // The doctor's chips: the next visit's list when the preview has it, else the stored pick (as "your pick").
    const chipItems = cid => {
        const nv = nextVisit[cid];
        if (nv && nv.length) return nv;
        return savedPicks(cid).map(p => ({ productId: p.productId, productCode: p.productCode, productName: p.productName, role: roleOf(p), source: SOURCE_REP_PICK, noContent: false }));
    };
    const playItems = cid => (nextVisit[cid] || []).filter(x => x.source === SOURCE_PLAY);

    // The period capacity's per-visit figures (max products per role, minutes per product, report minutes).
    // WP-VP-4D — first the plan's own visitModel (4E: preview / detail, no capacity read key needed); source "none" = the
    // period has no capacity ("—", nothing invented). Only without a visitModel (an older server) the Cycle Capacities
    // proxies are read; when they refuse the limit shows "—" and no time is invented.
    const visitModelOf = () => (page.state.preview && page.state.preview.visitModel) || (page.state.session && page.state.session.visitModel) || null;
    const loadCapacity = periodId => {
        const vm = visitModelOf();
        if (vm) {
            capacity = vm.source === 'none' ? null : {
                maxPromo: vm.maxPromo, maxNonPromo: vm.maxNonPromo, promoMinutes: vm.promoMinutes,
                nonPromoMinutes: vm.nonPromoMinutes, reportMinutes: vm.reportMinutes || 0
            };
            return Promise.resolve(capacity);
        }
        if (!periodId || periodId === capacityPeriod) return Promise.resolve(capacity);
        capacityPeriod = periodId;
        return request(capacityBase + '/capacities?cyclePeriodId=' + encodeURIComponent(periodId)).then(r => {
            const d = r.ok && r.body && (r.body.data !== undefined ? r.body.data : r.body);
            const items = Array.isArray(d) ? d : (d && Array.isArray(d.items) ? d.items : []);
            const row = items.find(x => !x.isArchived && x.cyclePeriodId === periodId) || items.find(x => !x.isArchived) || null;
            if (!row) return null;
            capacity = { maxPromo: row.maxPromoProducts, maxNonPromo: row.maxNonPromoProducts, promoMinutes: null, nonPromoMinutes: null, reportMinutes: 0 };
            return request(capacityBase + '/capacities/' + encodeURIComponent(row.cycleCapacityId)).then(dr => {
                const x = dr.ok && dr.body && (dr.body.data !== undefined ? dr.body.data : dr.body);
                if (!x) return capacity;
                capacity = {
                    maxPromo: x.maxPromoProducts, maxNonPromo: x.maxNonPromoProducts,
                    promoMinutes: x.promoProductTime, nonPromoMinutes: x.nonPromoProductTime,
                    reportMinutes: x.visitModel === 'typical' ? (x.reportMinutesPerVisit || 0) : (x.reportDuration || 0)
                };
                return capacity;
            });
        }).catch(() => capacity);
    };

    // ── chips ──
    const sourceLabel = src => ({ play: L.LegendPlay, 'rep-pick': L.LegendRepPick, 'last-visit': L.LegendLastVisit, portfolio: L.LegendPortfolio }[src] || '');
    const chipHtml = it => {
        const promo = roleOf(it) === ROLE_PROMO;
        const warn = promo && it.noContent; // a promo product without approved content (K-7f)
        const tip = [it.productName ? it.productCode : '', promo ? L.LegendPromo : L.LegendNonPromo, sourceLabel(it.source), warn ? L.NoApprovedContent : ''].filter(Boolean).join(' · ');
        return '<span class="badge rounded-pill ' + (promo ? 'bg-label-primary' : 'bg-transparent border text-body') + ' vp-pchip" data-role="' + esc(roleOf(it)) + '" data-source="' + esc(it.source) + '" title="' + esc(tip) + '">' +
            '<i class="bx ' + (SOURCE_ICON[it.source] || 'bx-package') + ' me-1" aria-hidden="true"></i>' + bidi(window.VisitPlanningFormat.productLabel(it)) +
            (warn ? '<i class="bx bx-error text-warning ms-1" aria-label="' + esc(L.NoApprovedContent || '') + '"></i>' : '') + '</span>';
    };
    const pickButton = (cell, label, icon) => '<button type="button" class="btn btn-sm btn-text-primary px-1 py-0 js-vp-pick" data-cid="' + esc(cell.dataset.cid) + '" title="' + esc(label) + '">' + (icon ? '<i class="bx ' + icon + '"></i>' : esc(label)) + '</button>';
    const paintCell = cell => {
        const items = chipItems(cell.dataset.cid);
        if (!items.length) {
            cell.innerHTML = '<span class="badge bg-label-warning">' + esc(L.NoProductBadge || '') + '</span>' + (canEdit() ? ' ' + pickButton(cell, L.PickProducts || '') : '');
            return;
        }
        cell.innerHTML = '<div class="d-flex flex-wrap align-items-center gap-1">' + items.map(chipHtml).join('') + (canEdit() ? pickButton(cell, L.EditProducts || '', 'bx-edit-alt') : '') + '</div>';
    };
    const paintCells = () => document.querySelectorAll('#dt-vp-contacts .vp-doc-picks').forEach(paintCell);

    // ── selection summary: time by products, distribution, doctors without products (from the preview) ──
    const hours = minutes => window.VisitPlanningFormat.hours(minutes, L.HoursFormat || '{0} h'); // WP-VP-4H — the app's language
    const renderSummary = () => {
        const host = el('vp-sum-extra'); if (!host) return;
        const p = page.state.preview;
        if (!p) { host.innerHTML = ''; return; }
        const parts = [];
        // WP-VP-4H (6) — the same reading as the header's "planned this week" card (VPF.weekLoad), the same format.
        const load = window.VisitPlanningFormat.weekLoad(p, page.state.weekStart);
        if (load.planned != null) {
            const pct = load.capacity > 0 ? Math.round(load.planned / load.capacity * 100) : null;
            parts.push('<div><i class="bx bx-time-five me-1"></i>' + esc(fmt(L.EstimatedWeek || '{0}', hours(load.planned))) + (pct != null ? ' · ' + esc(fmt(L.EstimatedWeekShare || '{0}', pct)) : '') + '</div>');
            if (load.capacity > 0 && load.planned > load.capacity) {
                parts.push('<div class="text-warning"><i class="bx bx-error me-1"></i>' + esc(fmt(L.EstimatedOver || '{0}', hours(load.planned - load.capacity))) + '</div>');
            }
        }
        const dist = (p.productDistribution || []).slice().sort((a, b) => b.doctorCount - a.doctorCount);
        if (dist.length) {
            parts.push('<div class="mt-2"><span class="opacity-75">' + esc(L.ProductDistribution || '') + ':</span> ' + dist.map(x => bidi(window.VisitPlanningFormat.productLabel(x)) + ' <strong>' + x.doctorCount + '</strong>').join(' · ') + '</div>');
        }
        if (p.doctorsWithoutProducts > 0) {
            parts.push('<div class="text-warning mt-1"><i class="bx bx-error me-1"></i>' + esc(fmt(L.DoctorsWithoutProducts || '{0}', p.doctorsWithoutProducts)) + '</div>');
        }
        host.innerHTML = parts.join('');
    };

    // ── bulk button ──
    const paintBulkButton = () => {
        const btn = el('vp-bulk-products'); if (!btn) return;
        const n = selection.doctors.length;
        btn.disabled = !canEdit() || n === 0;
        const label = el('vp-bulk-products-label'); if (label) label.textContent = fmt(L.BulkApplyProducts || '{0}', n);
    };

    // ── the picker (doctor panel, Products tab) ──
    let picker = null; // { mode: 'single' | 'bulk', doctor?, doctors?, locked: [], picks: [], results: [] }
    let searchTimer = null;
    const panel = () => el('vp-doctor-panel');
    const allItems = () => picker.locked.concat(picker.picks);

    const note = (tone, icon, text) => '<div class="alert alert-' + tone + ' py-2 small mb-0 d-flex gap-2" role="note"><i class="bx ' + icon + ' mt-1"></i><span>' + esc(text) + '</span></div>';
    const roleButtons = (item, idx) => {
        const locked = isLocked(item);
        const btn = (role, label) => '<button type="button" class="btn btn-sm py-0 px-2 ' + (roleOf(item) === role ? 'btn-primary' : 'btn-outline-secondary') + ' js-vp-role" data-idx="' + idx + '" data-role="' + role + '"' + (locked ? ' disabled' : '') + '>' + esc(label) + '</button>';
        return '<div class="btn-group btn-group-sm" role="group">' + btn(ROLE_PROMO, L.RolePromo || '') + btn(ROLE_NON_PROMO, L.RoleNonPromo || '') + '</div>';
    };
    const renderPicker = () => {
        if (!picker) return;
        const items = allItems();
        const counts = countRoles(items);
        const notes = [];
        if (picker.mode === 'bulk') notes.push(note('primary', 'bx-info-circle', fmt(L.ProductsBulkNote || '{0}', picker.doctors.length)));
        else if (picker.locked.length) notes.push(note('primary', 'bx-bulb', L.ProductsSuggestedNote || ''));
        if (picker.mode === 'single' && !items.length) notes.push(note('warning', 'bx-error', L.ProductsNoneNote || ''));
        const pf = page.state.preview && page.state.preview.portfolioStatus;
        if (pf !== 'defined') notes.push(note('secondary', 'bx-briefcase', L.PortfolioUndefinedNote || ''));
        el('vp-dp-notes').innerHTML = notes.join('');

        // the limit indicator comes from the capacity (never a constant); unknown ⇒ "—"
        const max = v => (capacity && capacity[v] != null ? capacity[v] : '—');
        el('vp-dp-limit').innerHTML = esc(window.VisitPlanningFormat.isolateRatios(fmt(L.ProductLimitLine || '{0} {1} {2} {3}', counts.promo, max('maxPromo'), counts.nonPromo, max('maxNonPromo'))));
        const minutes = picker.mode === 'single' ? visitMinutes(capacity, counts.promo, counts.nonPromo) : null;
        el('vp-dp-duration').textContent = picker.mode !== 'single' ? ''
            : (minutes != null ? fmt(L.DurationLine || '{0} {1} {2}', counts.promo, counts.nonPromo, minutes) : (L.DurationUnknown || ''));

        const warns = [];
        if (overLimit(capacity, counts)) warns.push(note('danger', 'bx-error-circle', fmt(L.LimitExceeded || '{0} {1}', capacity.maxPromo, capacity.maxNonPromo)));
        // the suggested products alone already fill a role: a product the rep adds in that role waits for the next visit
        const lockedCounts = countRoles(picker.locked);
        const pickCounts = countRoles(picker.picks);
        if (capacity && capacity.maxPromo != null && ((lockedCounts.promo >= capacity.maxPromo && pickCounts.promo > 0) || (lockedCounts.nonPromo >= capacity.maxNonPromo && pickCounts.nonPromo > 0))) {
            warns.push(note('warning', 'bx-package', L.PlayLimitFull || ''));
        }
        el('vp-dp-warn').innerHTML = warns.join('');

        el('vp-dp-selected').innerHTML = items.map((it, idx) => {
            const locked = isLocked(it);
            const noContent = roleOf(it) === ROLE_PROMO && it.noContent;
            return '<div class="border rounded p-2 d-flex align-items-center gap-2 vp-dp-item" data-idx="' + idx + '"' + (locked ? ' data-locked="1"' : '') + '>' +
                (locked ? '<i class="bx bx-lock-alt text-muted" title="' + esc(L.SuggestedLockHint || '') + '"></i>' : '<i class="bx ' + (SOURCE_ICON[it.source] || 'bx-user-check') + ' text-muted"></i>') +
                '<div class="flex-grow-1" style="min-width:0"><div class="fw-medium text-truncate">' + bidi(it.productName || it.productCode || '—') + '</div>' +
                (it.productName && it.productCode ? '<div class="text-muted small font-monospace">' + esc(it.productCode) + '</div>' : '') +
                (noContent ? '<div class="text-warning small"><i class="bx bx-error me-1"></i>' + esc(L.NoApprovedContent || '') + '</div>' : '') + '</div>' +
                roleButtons(it, idx) +
                (locked ? '' : '<button type="button" class="btn btn-sm btn-icon btn-text-danger js-vp-remove" data-idx="' + idx + '" title="' + esc(L.RemoveTarget || '') + '"><i class="bx bx-x"></i></button>') +
                '</div>';
        }).join('');

        renderResults();
        el('vp-dp-footnote').textContent = picker.mode === 'bulk' ? '' : (L.ProductsS1Note || '');
        el('vp-dp-done').textContent = picker.mode === 'bulk' ? fmt(L.ApplyToSelected || '{0}', picker.doctors.length) : (L.ProductsDone || '');
        el('vp-dp-done').disabled = !canEdit() || (picker.mode === 'bulk' && !picker.picks.length);
    };
    const renderResults = () => {
        const host = el('vp-dp-results'); if (!host || !picker) return;
        if (picker.unavailable) { host.innerHTML = '<div class="text-muted small py-2">' + esc(L.ProductsUnavailable || '') + '</div>'; return; }
        if (!picker.results.length) { host.innerHTML = '<div class="text-muted small py-2">' + esc(L.NoProductMatches || '') + '</div>'; return; }
        const taken = {}; allItems().forEach(x => { taken[x.productId] = true; });
        host.innerHTML = picker.results.map(r => '<button type="button" class="list-group-item list-group-item-action d-flex align-items-center gap-2 js-vp-add" data-pid="' + esc(r.productId) + '"' + (taken[r.productId] ? ' disabled' : '') + '>' +
            '<i class="bx ' + (taken[r.productId] ? 'bx-check text-success' : 'bx-plus') + '"></i><span class="flex-grow-1 text-truncate">' + bidi(r.productName || r.productCode || '—') + '</span>' +
            (r.productCode ? '<span class="text-muted font-monospace">' + esc(r.productCode) + '</span>' : '') + '</button>').join('');
    };
    const searchProducts = term => request(base + '/products?pageSize=100' + (term ? '&search=' + encodeURIComponent(term) : '')).then(r => {
        if (!picker) return;
        const d = r.ok && r.body;
        picker.unavailable = !d || d.disabled === true;
        picker.results = (!picker.unavailable && Array.isArray(d.options)) ? d.options : [];
        renderResults();
    }).catch(() => { if (picker) { picker.unavailable = true; picker.results = []; renderResults(); } });

    const openPicker = state => {
        if (!canEdit() || !panel() || !window.bootstrap) return;
        picker = Object.assign({ locked: [], picks: [], results: [], unavailable: false }, state);
        if (picker.mode === 'single') {
            const d = picker.doctor;
            el('vp-dp-title').textContent = d.name || '—';
            el('vp-dp-sub').textContent = [d.accountName, d.specialty].filter(Boolean).join(' · ') || '—';
        } else {
            el('vp-dp-title').textContent = fmt(L.ApplyToSelected || '{0}', picker.doctors.length);
            el('vp-dp-sub').textContent = picker.doctors.map(x => x.name).filter(Boolean).slice(0, 3).join(', ') + (picker.doctors.length > 3 ? '…' : '');
        }
        el('vp-dp-search').value = '';
        renderPicker();
        loadCapacity(page.state.session && page.state.session.cyclePeriodId).then(() => { if (picker) renderPicker(); });
        searchProducts('');
        // WP-VP-4D — the panel has two tabs now: the picker lives on "Products".
        const productsTab = el('vp-dp-tab-products-btn');
        if (productsTab) window.bootstrap.Tab.getOrCreateInstance(productsTab).show();
        window.bootstrap.Offcanvas.getOrCreateInstance(panel()).show();
    };
    const openSingle = cell => {
        const cid = cell.dataset.cid;
        const locked = playItems(cid).map(x => ({ productId: x.productId, productCode: x.productCode, productName: x.productName, role: x.role, source: SOURCE_PLAY, noContent: x.noContent, locked: true }));
        const lockedIds = locked.map(x => x.productId);
        const noContentOf = {}; (nextVisit[cid] || []).forEach(x => { noContentOf[x.productId] = x.noContent; });
        const picks = savedPicks(cid).filter(p => lockedIds.indexOf(p.productId) === -1)
            .map(p => ({ productId: p.productId, productCode: p.productCode, productName: p.productName, role: roleOf(p), source: SOURCE_REP_PICK, noContent: !!noContentOf[p.productId] }));
        openPicker({
            mode: 'single', locked, picks,
            doctor: { contactId: cid, accountId: cell.dataset.aid, accountContactLinkId: cell.dataset.lid || null, name: cell.dataset.name, specialty: cell.dataset.spec, accountName: accountNameOf(cell.dataset.aid) }
        });
    };
    const accountNameOf = aid => { const d = selection.doctors.find(x => x.accountId === aid); return d ? d.accountName : (el('vp-contacts-for') ? el('vp-contacts-for').textContent : ''); };

    // ── write: the existing session update; only the touched doctors carry `products` (null = keep for the rest) ──
    // The saved selection is the base (the tab's unsaved ticks are not written here); a touched doctor that is not in it
    // yet joins it, with its institution.
    const buildUpdate = changes => {
        const session = page.state.session;
        const contacts = savedContacts().map(contactInput);
        const accounts = (session.selectedAccountIds || []).slice();
        let accountsChanged = false;
        changes.forEach(ch => {
            let entry = contacts.find(c => c.contactId === ch.doctor.contactId && (c.accountId || null) === (ch.doctor.accountId || null))
                || contacts.find(c => c.contactId === ch.doctor.contactId);
            if (!entry) { entry = contactInput(ch.doctor); contacts.push(entry); }
            if (ch.doctor.accountId && accounts.indexOf(ch.doctor.accountId) === -1) { accounts.push(ch.doctor.accountId); accountsChanged = true; }
            entry.products = ch.products.map(pickInput);
        });
        return { selectedAccountIds: accountsChanged ? accounts : null, selectedPharmacyIds: null, selectedContacts: contacts, expectedVersion: session.version };
    };
    const save = (payload, message, extraWarning) => {
        const btn = el('vp-dp-done'); if (btn) btn.disabled = true;
        return request(base + '/sessions/' + encodeURIComponent(sessionId), { method: 'PUT', body: JSON.stringify(payload) }).then(r => {
            if (!r.ok) { window.showToast?.(errorText(r), 'error'); if (btn) btn.disabled = false; return; }
            window.showToast?.(message, 'success');
            if (extraWarning) window.showToast?.(extraWarning, 'warning');
            window.bootstrap?.Offcanvas.getInstance(panel())?.hide();
            picker = null;
            page.request('reload-plan');
        });
    };
    const done = () => {
        if (!picker || !canEdit() || !page.state.session) return;
        if (picker.mode === 'single') {
            // Only this doctor's own picks are written; the locked suggested products are the play's, never stored.
            save(buildUpdate([{ doctor: picker.doctor, products: picker.picks }]), L.ProductsSaved || '');
            return;
        }
        let over = 0;
        const changes = picker.doctors.map(d => {
            const merged = unionPicks(savedPicks(d.contactId), picker.picks);
            if (overLimit(capacity, countRoles(playItems(d.contactId).concat(merged)))) over++;
            return { doctor: d, products: merged };
        });
        save(buildUpdate(changes), L.ProductsSaved || '', over ? fmt(L.BulkLimitExceeded || '{0}', over) : null);
    };

    // ── events ──
    page.on('session', () => { paintCells(); paintBulkButton(); });
    page.on('preview', p => { nextVisit = readNextVisits(p); paintCells(); renderSummary(); paintBulkButton(); });
    page.on('week-change', () => { paintCells(); renderSummary(); paintBulkButton(); });
    page.on('targets:doctors-drawn', () => paintCells());
    page.on('targets:selection', s => { selection = s || selection; paintBulkButton(); });
    // WP-VP-4D — "Change products" from the doctor panel's period view opens this picker for that doctor.
    page.on('request:pick-products', d => {
        if (!d || !d.contactId) return;
        openSingle({ dataset: { cid: d.contactId, aid: d.accountId || '', lid: d.accountContactLinkId || '', name: d.name || '', spec: d.specialty || '' } });
    });

    document.addEventListener('click', e => {
        const pick = e.target.closest('#dt-vp-contacts .js-vp-pick');
        if (pick) { const cell = pick.closest('.vp-doc-picks'); if (cell) openSingle(cell); return; }
        if (e.target.closest('#vp-bulk-products')) {
            if (!selection.doctors.length) return;
            openPicker({ mode: 'bulk', doctors: selection.doctors.slice() });
            return;
        }
        if (!picker) return;
        const add = e.target.closest('#vp-dp-results .js-vp-add');
        if (add) {
            const r = picker.results.find(x => x.productId === add.dataset.pid);
            if (r && !allItems().some(x => x.productId === r.productId)) {
                picker.picks.push({ productId: r.productId, productCode: r.productCode, productName: r.productName, role: ROLE_PROMO, source: SOURCE_REP_PICK, noContent: false });
                renderPicker();
            }
            return;
        }
        const roleBtn = e.target.closest('#vp-dp-selected .js-vp-role');
        if (roleBtn) {
            const item = allItems()[Number(roleBtn.dataset.idx)];
            if (!item || isLocked(item)) return; // S-3: the suggested product's role is the play's
            item.role = roleBtn.dataset.role === ROLE_NON_PROMO ? ROLE_NON_PROMO : ROLE_PROMO;
            renderPicker();
            return;
        }
        const rm = e.target.closest('#vp-dp-selected .js-vp-remove');
        if (rm) {
            const item = allItems()[Number(rm.dataset.idx)];
            if (!item || isLocked(item)) return; // K-7b: removable only on the Planned Visit screen
            picker.picks = picker.picks.filter(x => x !== item);
            renderPicker();
            return;
        }
        if (e.target.closest('#vp-dp-done')) done();
    });
    el('vp-dp-search')?.addEventListener('input', function () {
        const term = (this.value || '').trim();
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => searchProducts(term), 250);
    });
    panel()?.addEventListener('hidden.bs.offcanvas', () => { picker = null; });
})(window, document);
