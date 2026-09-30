/**
 * WP-CL-FE-2 — claim × country coverage matrix (mockup scenario 2) over the FE-1 v2 proxy:
 *  - columns = COUNTRY_CODES order from lookups/countries (ICU display names + content languages), header summary
 *  - rows = the current record of each claim code (api/v2/claims/coverage), sub line with evidence / usage counts from
 *    the list read (includeCounts; "—" when a counter is unavailable)
 *  - cells: state pill (theme colours) + version + note (closed reason, expiring, core / evidence changed)
 *  - cell actions (crm.claim.manage): open country version (FE-4 route; core only, disabled until the core is
 *    approved), mark as not to be opened (single-choice reason), reopen, edit / view a version
 * There is no country restriction (D1) and no licence number (D3).
 */
(function (window, document) {
    'use strict';
    const table = document.getElementById('coverageTable');
    if (!table) return;

    const api = '/CRM/Claims/api/v2';
    const $ = window.jQuery;
    const L = window.ClaimL10n || window.L10n || {};
    const flags = (() => { try { return JSON.parse(document.getElementById('claim-page-flags')?.textContent || '{}'); } catch (e) { return {}; } })();
    const canManage = flags.canManage === true;
    const STATES = ['approved', 'in-review', 'draft', 'review-required', 'expiring', 'closed', 'not-opened'];
    const ICONS = {
        'approved': 'bx-check-circle', 'in-review': 'bx-time-five', 'draft': 'bx-edit-alt', 'review-required': 'bx-revision',
        'expiring': 'bx-alarm-exclamation', 'closed': 'bx-block', 'not-opened': 'bx-plus-circle', 'not-applicable': 'bx-minus'
    };

    const state = { countries: [], rows: [], counts: {}, reasons: [], action: null };

    const byId = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const fmt = (template, ...args) => String(template ?? '').replace(/\{(\d+)\}/g, (_, i) => args[Number(i)] ?? '');
    const stateLabel = s => L['State_' + s] || s;
    const reasonLabel = code => L['Reason_' + code] || state.reasons.find(r => r.code === code)?.name || code || '';

    const readError = async response => {
        const body = await response.json().catch(() => ({}));
        const errors = Array.isArray(body.errors) ? body.errors.filter(e => typeof e === 'string') : [];
        const code = errors.length > 1 && /^[a-z_]+$/.test(errors[0]) ? errors[0] : null;
        const message = (code && L['Err_' + code]) || (errors.length > 1 ? errors.slice(1).join(' · ') : errors[0]) || L.ErrorState;
        return Object.assign(new Error(message), { status: response.status, code });
    };
    const request = async (method, url, body) => {
        const init = { method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (body !== undefined) { init.headers['Content-Type'] = 'application/json'; init.body = JSON.stringify(body); }
        const response = await fetch(url, init);
        if (!response.ok) throw await readError(response);
        if (response.status === 204) return null;
        const json = await response.json().catch(() => ({}));
        return json && Object.prototype.hasOwnProperty.call(json, 'data') ? json.data : json;
    };
    const tryGet = async url => { try { return await request('GET', url); } catch (e) { return null; } };

    // ─── cell model ─────────────────────────────────────────────────────────────
    const cellState = cell => cell.state === 'approved' && cell.isExpiring ? 'expiring' : cell.state;
    const cellNotes = (row, cell) => {
        const notes = [];
        if (cell.state === 'closed') notes.push({ text: fmt(L.ClosedTooltip, reasonLabel(cell.closureReasonCode)) });
        else if (cell.state === 'not-opened') notes.push({ text: L.NotOpenedTooltip });
        if (cell.state === 'review-required') {
            const coreChanged = cell.boundCoreVersion && row.coreVersion && cell.boundCoreVersion !== row.coreVersion;
            notes.push({ text: coreChanged ? L.CellCoreChangedNote : L.CellEvidenceChangedNote, tone: 'is-warning' });
        }
        if (cell.isExpiring || cell.evidenceExpiring) notes.push({ text: L.CellExpiringNote, tone: 'is-danger' });
        if (cell.boundCoreVersion && row.kind === 'core') notes.push({ text: fmt(L.CoreVersionLine, cell.boundCoreVersion) });
        return notes;
    };

    const cellActions = (row, cell, country) => {
        if (!canManage || cell.state === 'not-applicable') return '';
        const items = [];
        if (cell.versionId) {
            items.push(`<li><a class="dropdown-item" href="/CRM/Claims/CountryVersions/${encodeURIComponent(cell.versionId)}/Edit"><i class="bx bx-edit-alt me-2"></i>${esc(L.ActionEditVersion)}</a></li>`);
        }
        if (cell.state === 'not-opened') {
            if (row.kind === 'core') {
                const approved = row.coreStatus === 'approved';
                items.push(approved
                    ? `<li><a class="dropdown-item" href="/CRM/Claims/${encodeURIComponent(row.claimId)}/Countries/${encodeURIComponent(cell.countryCode)}/Create"><i class="bx bx-plus-circle me-2"></i>${esc(L.ActionOpenCountryVersion)}</a></li>`
                    : `<li><span class="dropdown-item disabled" aria-disabled="true" title="${esc(L.CoreNotApprovedTooltip)}"><i class="bx bx-plus-circle me-2"></i>${esc(L.ActionOpenCountryVersion)}<small class="d-block text-muted">${esc(L.CoreNotApprovedTooltip)}</small></span></li>`);
            }
            items.push(`<li><button type="button" class="dropdown-item js-cov-close" data-claim="${esc(row.claimId)}" data-country="${esc(cell.countryCode)}"><i class="bx bx-block me-2"></i>${esc(L.ActionMarkNotOpened)}</button></li>`);
        }
        if (cell.state === 'closed') {
            items.push(`<li><button type="button" class="dropdown-item js-cov-reopen" data-claim="${esc(row.claimId)}" data-country="${esc(cell.countryCode)}"><i class="bx bx-lock-open-alt me-2"></i>${esc(L.ActionReopen)}</button></li>`);
        }
        if (!items.length) return '';
        const label = fmt(L.CellActionsLabel, `${row.claimCode} · ${country?.name || cell.countryCode}`);
        return `<div class="dropdown cov-cell-menu"><button type="button" class="btn btn-sm btn-icon btn-text-secondary rounded-pill" data-bs-toggle="dropdown" data-bs-popper-config='{"strategy":"fixed"}' aria-expanded="false" aria-label="${esc(label)}" title="${esc(label)}"><i class="bx bx-dots-vertical-rounded"></i></button>`
            + `<ul class="dropdown-menu dropdown-menu-end">${items.join('')}</ul></div>`;
    };

    const cellHtml = (row, cell, country) => {
        const s = cellState(cell);
        const label = s === 'not-applicable' ? '—' : stateLabel(s);
        const notes = cellNotes(row, cell).map(n => `<span class="cov-cell-note ${n.tone || ''}">${esc(n.text)}</span>`).join('');
        return `<div class="d-flex justify-content-between align-items-start gap-1"><div class="cov-cell">`
            + `<span class="claim-chip state-${esc(s)}" title="${esc((country?.name || cell.countryCode) + ': ' + label)}"><i class="bx ${ICONS[s] || 'bx-circle'}"></i>${esc(label)}</span>`
            + (cell.version ? `<span class="cov-cell-version">v${esc(cell.version)}</span>` : '')
            + notes + `</div>${cellActions(row, cell, country)}</div>`;
    };

    // ─── filters ────────────────────────────────────────────────────────────────
    const fill = (id, options, allLabel) => {
        const el = byId(id);
        if (!el) return;
        el.innerHTML = `<option value="">${esc(allLabel)}</option>` + options.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    const loadFilterOptions = () => {
        const products = Array.from(new Map(state.rows.filter(r => r.productId || r.productDisplay)
            .map(r => [norm(r.productId || r.productDisplay), norm(r.productDisplay) || norm(r.productId)])).entries())
            .map(([value, text]) => ({ value, text })).sort((a, b) => a.text.localeCompare(b.text));
        fill('covProduct', products, L.FilterProduct);
        fill('covKind', [{ value: 'core', text: L.KindCore }, { value: 'local', text: L.KindLocal }], L.FilterKind);
        fill('covStatus', STATES.map(s => ({ value: s, text: stateLabel(s) })), L.FilterStatus);
    };
    const filters = () => ({
        search: norm(byId('covSearch')?.value).toLowerCase(),
        product: byId('covProduct')?.value || '',
        kind: byId('covKind')?.value || '',
        status: byId('covStatus')?.value || ''
    });
    const matches = (row, f) => {
        if (f.search && !`${row.claimCode} ${row.claimName} ${row.productDisplay || ''}`.toLowerCase().includes(f.search)) return false;
        if (f.product && norm(row.productId || row.productDisplay) !== f.product) return false;
        if (f.kind && row.kind !== f.kind) return false;
        if (f.status && row.coreStatus !== f.status && !(row.cells || []).some(c => cellState(c) === f.status)) return false;
        return true;
    };

    // ─── render ─────────────────────────────────────────────────────────────────
    const columns = () => state.countries.length
        ? state.countries
        : Array.from(new Set(state.rows.flatMap(r => (r.cells || []).map(c => c.countryCode)))).map(code => ({ code, name: code, languages: [] }));

    const render = () => {
        const f = filters();
        const rows = state.rows.filter(r => matches(r, f));
        const cols = columns();
        byId('coverageLoading')?.classList.add('d-none');
        byId('coverageEmpty')?.classList.toggle('d-none', state.rows.length > 0);
        byId('coverageCard')?.classList.toggle('d-none', state.rows.length === 0);
        byId('coverageNoMatch')?.classList.toggle('d-none', state.rows.length === 0 || rows.length > 0);
        table.classList.toggle('d-none', rows.length === 0);

        const head = cols.map(c => {
            const states = rows.map(r => (r.cells || []).find(x => x.countryCode === c.code)).filter(Boolean).map(cellState);
            const ok = states.filter(s => s === 'approved' || s === 'expiring').length;
            const open = states.filter(s => ['review-required', 'expiring', 'in-review', 'draft', 'not-opened'].includes(s)).length;
            const langs = (c.languageDetails && c.languageDetails.length ? c.languageDetails.map(l => `${l.nativeName} (${l.code})`) : (c.languages || [])).join(', ');
            return `<th scope="col"><span class="text-heading">${esc(c.name)} <span class="text-muted">(${esc(c.code)})</span></span>`
                + (langs ? `<span class="cov-col-langs">${esc(langs)}</span>` : '')
                + `<span class="cov-col-summary">${esc(fmt(L.ColumnSummary, ok, open))}</span></th>`;
        }).join('');
        byId('coverageHead').innerHTML = `<tr><th scope="col" class="cov-claim-col">${esc(L.ColClaim)}</th>${head}</tr>`;

        byId('coverageBody').innerHTML = rows.map(row => {
            const counts = state.counts[row.claimId] || {};
            const sub = fmt(L.RowSubline, row.productDisplay || '—',
                counts.evidenceCount == null ? '—' : counts.evidenceCount, counts.usageCount == null ? '—' : counts.usageCount);
            const kind = row.kind === 'local'
                ? `<span class="badge rounded-pill claim-kind-local ms-1">${esc(L.KindLocal)}</span>`
                : `<span class="badge rounded-pill claim-kind-core ms-1">${esc(L.KindCore)}</span>`;
            const code = canManage
                ? `<a href="/CRM/Claims/Edit/${encodeURIComponent(row.claimId)}" class="fw-medium">${esc(row.claimCode)}</a>`
                : `<span class="fw-medium">${esc(row.claimCode)}</span>`;
            const cells = cols.map(c => {
                const cell = (row.cells || []).find(x => x.countryCode === c.code);
                return `<td>${cell ? cellHtml(row, cell, c) : '<span class="text-muted">—</span>'}</td>`;
            }).join('');
            return `<tr><th scope="row" class="cov-claim-col">${code}${kind}`
                + ` <span class="claim-chip state-${esc(row.coreStatus)} ms-1">${esc(stateLabel(row.coreStatus))}</span>`
                + `<span class="d-block text-heading">${esc(row.claimName)}</span><span class="cov-row-sub">${esc(sub)}</span></th>${cells}</tr>`;
        }).join('');
    };

    const renderLegend = () => {
        const host = byId('coverageLegend');
        if (!host) return;
        host.insertAdjacentHTML('beforeend', STATES.concat(['not-applicable']).map(s =>
            `<span class="claim-chip state-${s}"><i class="bx ${ICONS[s]}"></i>${esc(s === 'not-applicable' ? L['State_not-applicable'] : stateLabel(s))}</span>`).join(''));
    };

    // ─── load ───────────────────────────────────────────────────────────────────
    const load = async () => {
        const [coverage, countries, reasons, list] = await Promise.all([
            request('GET', `${api}/claims/coverage`),
            tryGet(`${api}/lookups/countries`),
            tryGet(`${api}/lookups/closure-reasons`),
            tryGet(`${api}/claims?includeArchived=false&includeCounts=true`)
        ]);
        state.countries = Array.isArray(countries) ? countries.map(c => ({ ...c, code: norm(c.code).toUpperCase() })) : [];
        if (!state.countries.length && Array.isArray(coverage?.countries)) {
            state.countries = coverage.countries.map(c => ({ code: norm(c.countryCode).toUpperCase(), name: c.displayName || c.countryCode, languages: [] }));
        }
        state.rows = Array.isArray(coverage?.rows) ? coverage.rows : [];
        state.reasons = Array.isArray(reasons) ? reasons : [];
        state.counts = {};
        (list?.items || []).forEach(i => { state.counts[i.claimId] = { evidenceCount: i.evidenceCount, usageCount: i.usageCount }; });
    };
    const reload = async () => {
        try { await load(); render(); }
        catch (error) {
            const el = byId('coverageError');
            if (el) { el.textContent = error.message || L.ErrorState; el.classList.remove('d-none'); }
            byId('coverageLoading')?.classList.add('d-none');
        }
    };

    // ─── close / reopen ─────────────────────────────────────────────────────────
    const modalAlert = (id, message) => { const el = byId(id); if (el) { el.textContent = message || ''; el.classList.toggle('d-none', !message); } };
    const countryName = code => state.countries.find(c => c.code === code)?.name || code;
    const openClose = (claimId, countryCode) => {
        state.action = { claimId, countryCode };
        const row = state.rows.find(r => r.claimId === claimId);
        byId('coverageCloseTitle').textContent = fmt(L.CloseModalTitle, `${row?.claimCode || ''} · ${countryName(countryCode)}`);
        const select = byId('coverageCloseReason');
        select.innerHTML = '<option value=""></option>' + state.reasons.map(r => `<option value="${esc(r.code)}">${esc(reasonLabel(r.code))}</option>`).join('');
        modalAlert('coverageCloseAlert', state.reasons.length ? '' : L.Err_reference_set_missing);
        const modal = byId('coverageCloseModal');
        if ($ && $.fn.select2) {
            const $s = $(select);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({ width: '100%', placeholder: $s.data('placeholder') || '', allowClear: false, minimumResultsForSearch: Infinity, dropdownParent: $(modal) });
        }
        window.bootstrap?.Modal.getOrCreateInstance(modal).show();
    };
    const confirmClose = async () => {
        const reasonCode = byId('coverageCloseReason')?.value;
        if (!reasonCode) { modalAlert('coverageCloseAlert', L.Err_required); return; }
        try {
            await request('POST', `${api}/claims/${encodeURIComponent(state.action.claimId)}/country-closures`,
                { countryCode: state.action.countryCode, reasonCode });
            window.bootstrap?.Modal.getOrCreateInstance(byId('coverageCloseModal')).hide();
            window.showToast?.(L.ToastClosed, 'success');
            await reload();
        } catch (error) { modalAlert('coverageCloseAlert', error.message); }
    };
    const openReopen = (claimId, countryCode) => {
        state.action = { claimId, countryCode };
        const row = state.rows.find(r => r.claimId === claimId);
        byId('coverageReopenTitle').textContent = fmt(L.ReopenModalTitle, `${row?.claimCode || ''} · ${countryName(countryCode)}`);
        byId('coverageReopenNote').value = '';
        modalAlert('coverageReopenAlert', '');
        window.bootstrap?.Modal.getOrCreateInstance(byId('coverageReopenModal')).show();
    };
    const confirmReopen = async () => {
        try {
            await request('POST', `${api}/claims/${encodeURIComponent(state.action.claimId)}/country-closures/${encodeURIComponent(state.action.countryCode)}/reopen`,
                { note: norm(byId('coverageReopenNote')?.value) || null });
            window.bootstrap?.Modal.getOrCreateInstance(byId('coverageReopenModal')).hide();
            window.showToast?.(L.ToastReopened, 'success');
            await reload();
        } catch (error) { modalAlert('coverageReopenAlert', error.message); }
    };

    // ─── events + init ──────────────────────────────────────────────────────────
    const clearFilters = () => {
        ['covSearch', 'covProduct', 'covKind', 'covStatus'].forEach(id => { const el = byId(id); if (el) el.value = ''; });
        render();
    };
    let searchTimer = null;
    byId('covSearch')?.addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(render, 200); });
    ['covProduct', 'covKind', 'covStatus'].forEach(id => byId(id)?.addEventListener('change', render));
    byId('covClear')?.addEventListener('click', clearFilters);
    byId('covClearEmpty')?.addEventListener('click', clearFilters);
    byId('coverageCloseConfirm')?.addEventListener('click', () => void confirmClose());
    byId('coverageReopenConfirm')?.addEventListener('click', () => void confirmReopen());
    document.addEventListener('click', event => {
        const close = event.target.closest('.js-cov-close');
        if (close) { event.preventDefault(); openClose(close.dataset.claim, close.dataset.country); return; }
        const reopen = event.target.closest('.js-cov-reopen');
        if (reopen) { event.preventDefault(); openReopen(reopen.dataset.claim, reopen.dataset.country); }
    });

    renderLegend();
    reload().then(loadFilterOptions);
})(window, document);
