/**
 * MOD-0176 F4 — the shared helpers of the expiry pages (pack §11.2). No expiry rule lives here: the server decides (FEFO-BUNDLE 1.1.0 —
 * severities, horizons, the FEFO order). This file holds the vocabulary reader, formatting, the same-origin calls (/Expiry/** —
 * antiforgery + Idempotency-Key on writes), the badges, the warehouse picker and the ONE "İstisna aç" modal (openException).
 * Buttons are shown by action code (can) — never by a permission key.
 */
'use strict';

window.ExpiryUi = (function () {
    const actions = (() => {
        try { return JSON.parse(document.getElementById('expiry-actions')?.textContent || '{}'); } catch (error) { return {}; }
    })();
    const L = () => window.ExpiryL10n || {};
    const t = (key, ...args) => String(L()[key] || '').replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`);
    const has = (key) => !!L()[key];
    const can = (code) => actions[code] === true;
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const lang = document.documentElement.lang || undefined;
    const numberFormat = new Intl.NumberFormat(lang, { maximumFractionDigits: 6 });
    const qty = (value) => {
        const n = Number(value);
        return Number.isFinite(n) ? numberFormat.format(n) : String(value ?? '');
    };
    const date = (wire) => {
        if (!wire) return '';
        const d = new Date(String(wire).slice(0, 10) + 'T00:00:00Z');
        return Number.isNaN(d.getTime()) ? String(wire) : d.toLocaleDateString(lang, { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'UTC' });
    };
    const byId = (id) => document.getElementById(id);
    const newKey = () => 'f4-' + (window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`).replace(/[^A-Za-z0-9-]/g, '');
    const antiforgery = () => document.querySelector('#ex-antiforgery input[name="__RequestVerificationToken"]')?.value || '';

    const call = async (method, url, body, options) => {
        const headers = { Accept: 'application/json' };
        if (method !== 'GET') {
            headers['Content-Type'] = 'application/json';
            headers.RequestVerificationToken = antiforgery();
        }
        if (options?.idempotencyKey) headers['Idempotency-Key'] = options.idempotencyKey;
        if (options?.ifMatch) headers['If-Match'] = options.ifMatch; // F5: the version of a case / the policy
        try {
            const res = await fetch(url, { method, credentials: 'same-origin', headers, body: body === undefined ? undefined : JSON.stringify(body) });
            const json = await res.json().catch(() => null);
            return { ok: res.ok, status: res.status, json };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const get = (url) => call('GET', url);
    const codeOf = (reply) => reply?.json?.error?.code || null;
    const errorText = (reply) => {
        const code = codeOf(reply);
        if (code && has(`Expiry.Errors.${code}`)) return t(`Expiry.Errors.${code}`);
        if (reply?.status === 0 || reply?.status >= 500) return t('Expiry.Errors.SERVICE_UNAVAILABLE');
        if (reply?.status === 401 || reply?.status === 403) return t('Expiry.Errors.FORBIDDEN');
        return t('Expiry.Errors.GENERIC');
    };
    const showError = (box, reply) => {
        if (!box) return;
        box.textContent = typeof reply === 'string' ? reply : errorText(reply);
        box.classList.remove('d-none');
    };
    const hideError = (box) => box?.classList.add('d-none');

    // ── display ─────────────────────────────────────────────────────────────────────────────────────────────────
    const SEVERITY = { Expired: 'bg-label-secondary', Critical: 'bg-label-warning', Approaching: 'bg-label-info', Ok: 'bg-label-success' };
    const severityBadge = (severity) => severity ? `<span class="badge ${SEVERITY[severity] || 'bg-label-secondary'}">${esc(t('Expiry.Severity.' + severity) || severity)}</span>` : '';
    const REASON = { EXPIRED: 'bg-label-secondary', RETEST_DUE: 'bg-label-danger', BLOCKED: 'bg-label-danger', HELD: 'bg-label-danger', QUARANTINED: 'bg-label-primary', BELOW_MIN_REMAINING: 'bg-label-primary' };
    const reasonBadge = (reason) => `<span class="badge ${REASON[reason] || 'bg-label-secondary'}">${esc(t('Expiry.Reason.' + reason) || reason)}</span>`;
    // an expired lot is told once: the severity badge already says "Süresi doldu", so its EXPIRED reason is not repeated
    const statusCell = (row) => [severityBadge(row.severity)].concat((row.heldReasons || []).filter((r) => !(r === 'EXPIRED' && row.severity === 'Expired')).map(reasonBadge)).join(' ');
    const unit = (onHand) => onHand?.baseUomId || t('Expiry.Common.NoUnit');
    const amount = (value, onHand) => `<span dir="ltr">${esc(qty(value))} <small class="text-muted">${esc(unit(onHand))}</small></span>`;
    const productCell = (row) => `<div class="d-flex flex-column"><span class="fw-medium text-heading">${esc(row.itemLabel || row.item?.itemId || '')}</span>`
        + `<small class="text-muted">${esc(row.skuLabel || '')}</small></div>`;
    const lotUrl = (lotId) => `/Lots/Details?${new URLSearchParams({ lotId })}`;
    const lotLink = (row) => row?.lotId
        ? `<a class="font-monospace" href="${lotUrl(row.lotId)}" title="${esc(t('Expiry.Action.LotDetail'))}">${esc(row.lotNumber || row.lotId)}</a>`
        : '<span class="text-muted">—</span>';
    const orderUrl = (row) => `/Expiry/FefoOrder?${new URLSearchParams({
        warehouseId: row.warehouse?.id || '', itemId: row.item?.itemId || '', skuId: row.item?.skuId || '', skuLevel: row.item?.skuLevel || ''
    })}`;
    const shelfBar = (row) => {
        if (row.remainingShelfLifePct == null) return '<span class="text-muted">—</span>';
        const pct = Math.max(0, Math.min(100, Number(row.remainingShelfLifePct)));
        const tone = row.severity === 'Critical' ? 'bg-warning' : row.severity === 'Approaching' ? 'bg-info' : 'bg-success';
        return `<div class="d-flex align-items-center gap-2" style="min-width:90px"><div class="progress flex-grow-1" style="height:6px">`
            + `<div class="progress-bar ${tone}" role="progressbar" style="width:${pct}%" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100"></div></div>`
            + `<small dir="ltr">${esc(qty(pct))}%</small></div>`;
    };

    // ── F5: exception cases (pack §4.3, §8.4; mockup XT / XS / RES) ────────────────────────────────────────────────
    const TYPE = { FEFO_BYPASS: 'bg-label-primary', BELOW_MIN_REMAINING: 'bg-label-warning', EXPIRY_DATE_OVERRIDE: 'bg-label-info', EXPIRED_ON_HAND: 'bg-label-danger' };
    const STATUS = { Open: 'bg-label-primary', InReview: 'bg-label-info', Accepted: 'bg-label-success', Fixed: 'bg-label-success', Closed: 'bg-label-secondary', Cancelled: 'bg-label-secondary' };
    const typeBadge = (type) => `<span class="badge ${TYPE[type] || 'bg-label-secondary'}">${esc(t('Expiry.Type.' + type) || type)}</span>`;
    const caseStatusBadge = (status) => `<span class="badge ${STATUS[status] || 'bg-label-secondary'}">${esc(t('Expiry.Status.' + status) || status)}</span>`;
    const resolutionText = (resolution) => (resolution ? t('Expiry.Resolution.' + resolution) || resolution : '');
    const dateTime = (wire) => {
        if (!wire) return '';
        const d = new Date(wire);
        return Number.isNaN(d.getTime()) ? String(wire)
            : d.toLocaleString(lang, { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    };
    /** "2g 4s" / "3s 20dk" (mockup SLA text). */
    const duration = (ms) => {
        const minutes = Math.max(0, Math.round(Math.abs(ms) / 60000));
        const days = Math.floor(minutes / 1440);
        const hours = Math.floor((minutes % 1440) / 60);
        return days > 0 ? t('Expiry.Sla.DaysHours', days, hours) : t('Expiry.Sla.HoursMinutes', hours, minutes % 60);
    };
    /** The SLA of an ACTIVE case: late (red, "… önce doldu"), due soon (< 6 h, yellow) — the server's late / dueSoon flags decide the colour. */
    const slaCell = (row) => {
        if (!['Open', 'InReview'].includes(row.status)) return '<span class="text-muted">—</span>';
        const left = new Date(row.dueAtUtc).getTime() - Date.now();
        if (row.late) return `<span class="badge bg-label-danger me-1">${esc(t('Expiry.Sla.Late'))}</span><small class="text-danger">${esc(t('Expiry.Sla.Ago', duration(left)))}</small>`;
        return `<small class="${row.dueSoon ? 'text-warning fw-medium' : 'text-muted'}">${esc(t('Expiry.Sla.Left', duration(left)))}</small>`;
    };
    /** A person: the name the Web resolved (FU-0175-Q5-02), else the short id + "bilinmeyen kullanıcı" — never a guessed name. */
    const person = (ref) => (ref?.name ? ref.name : ref?.userId ? `${String(ref.userId).slice(0, 8)} · ${t('Expiry.Common.UnknownUser')}` : '');
    const write = (method, path, body, options) => call(method, `/Expiry/api/${path}`, body, options);

    const loadWarehouses = async (select) => {
        if (!select) return;
        const reply = await get('/Expiry/lookups/warehouses');
        if (!reply.ok) return;
        (reply.json?.items || []).forEach((o) => select.add(new Option(o.text || o.value, o.value)));
    };

    /** The row actions of the lists (pack §11.3): İstisna aç, FEFO sırasını gör, Karantinaya al (MOD-0175 link). */
    const rowActions = (row, quarantineUrl) => {
        const items = [];
        if (can('EXPIRY_FEFO_ORDER_VIEW')) items.push(`<a class="btn btn-sm btn-icon btn-text-secondary" href="${orderUrl(row)}" title="${esc(t('Expiry.Action.ViewOrder'))}" aria-label="${esc(t('Expiry.Action.ViewOrder'))}"><i class="bx bx-sort-up"></i></a>`);
        if (can('QUARANTINE_LINK')) items.push(`<a class="btn btn-sm btn-icon btn-text-secondary" href="${quarantineUrl}" title="${esc(t('Expiry.Action.Quarantine'))}" aria-label="${esc(t('Expiry.Action.Quarantine'))}"><i class="bx bx-shield-quarter"></i></a>`);
        if (can('EXPIRY_EXCEPTION_OPEN')) items.push(`<button type="button" class="btn btn-sm btn-icon btn-text-secondary" data-ex-open='${esc(JSON.stringify({ lotId: row.lotId, lotNumber: row.lotNumber, warehouseId: row.warehouse?.id, warehouse: row.warehouseLabel, product: row.itemLabel }))}' title="${esc(t('Expiry.Action.OpenException'))}" aria-label="${esc(t('Expiry.Action.OpenException'))}"><i class="bx bx-error-alt"></i></button>`);
        return `<div class="d-flex justify-content-end gap-1">${items.join('')}</div>`;
    };

    // ── "İstisna aç" (openException) ─────────────────────────────────────────────────────────────────────────────
    let subject = null;
    const modalEl = () => byId('ex-open-modal');
    // F5 pick mode (the inbox has no row): warehouse → the SKUs with lot stock there (/Expiry/lookups/skus) → the SKU's lots (/Lots/form/lots)
    let pickSkus = [];
    const resetSelect = (select, disabled) => {
        if (!select) return;
        select.innerHTML = '<option value=""></option>';
        select.disabled = disabled;
    };
    const pickWarehouse = async () => {
        const warehouse = byId('ex-open-warehouse').value;
        resetSelect(byId('ex-open-sku'), true);
        resetSelect(byId('ex-open-lot'), true);
        pickSkus = [];
        if (!warehouse) return;
        const reply = await get(`/Expiry/lookups/skus?warehouseId=${encodeURIComponent(warehouse)}`);
        pickSkus = reply.ok ? reply.json?.items || [] : [];
        pickSkus.forEach((s, i) => byId('ex-open-sku').add(new Option(s.text || s.skuId, String(i))));
        byId('ex-open-sku').disabled = pickSkus.length === 0;
    };
    const pickSku = async () => {
        const sku = pickSkus[Number(byId('ex-open-sku').value)];
        resetSelect(byId('ex-open-lot'), true);
        if (!sku || byId('ex-open-sku').value === '') return;
        const reply = await get(`/Lots/form/lots?${new URLSearchParams({ sku: `${sku.skuLevel}:${sku.skuId}` })}`);
        (reply.ok ? reply.json?.items || [] : []).forEach((o) => byId('ex-open-lot').add(new Option(o.text || o.lotNumber || o.value, o.value)));
        byId('ex-open-lot').disabled = byId('ex-open-lot').options.length <= 1;
    };
    const openModal = async (data) => {
        const el = modalEl();
        if (!el || !window.bootstrap) return;
        subject = data || {};
        const pick = !subject.lotId;
        byId('ex-open-pick')?.classList.toggle('d-none', !pick);
        byId('ex-open-subject').textContent = pick ? t('Expiry.Modal.PickHint') : [subject.lotNumber, subject.product, subject.warehouse].filter(Boolean).join(' · ');
        byId('ex-open-description').value = '';
        el.querySelectorAll('[data-err]').forEach((e) => { e.textContent = ''; });
        hideError(byId('ex-open-error'));
        if (pick && byId('ex-open-warehouse').options.length <= 1) await loadWarehouses(byId('ex-open-warehouse'));
        window.bootstrap.Modal.getOrCreateInstance(el).show();
    };
    const submitModal = async () => {
        const description = byId('ex-open-description').value.trim();
        const pick = !subject.lotId;
        const lotId = pick ? byId('ex-open-lot')?.value : subject.lotId;
        const warehouseId = pick ? byId('ex-open-warehouse')?.value : subject.warehouseId;
        let invalid = false;
        if (pick && !lotId) { modalEl().querySelector('[data-err="lot"]').textContent = t('Expiry.Modal.LotRequired'); invalid = true; }
        if (description.length < 10) { modalEl().querySelector('[data-err="description"]').textContent = t('Expiry.Modal.DescriptionMin'); invalid = true; }
        if (invalid) return;
        const key = newKey();
        const body = { idempotencyKey: key, type: byId('ex-open-type').value, lotId, description };
        if (warehouseId) body.warehouseId = warehouseId;
        const reply = await call('POST', '/Expiry/api/exceptions', body, { idempotencyKey: key });
        if (!reply.ok) {
            showError(byId('ex-open-error'), reply);
            return;
        }
        window.bootstrap.Modal.getOrCreateInstance(modalEl()).hide();
        window.showToast?.(t('Expiry.Modal.Opened', reply.json?.number || ''), 'success');
        // mockup: the new case opens on its detail page (where the reader may read cases)
        if (reply.json?.exceptionId && can('EXPIRY_EXCEPTIONS_VIEW')) {
            window.location.href = `/Expiry/Exceptions/Details/${encodeURIComponent(reply.json.exceptionId)}`;
        }
    };
    document.addEventListener('change', (event) => {
        if (event.target?.id === 'ex-open-warehouse') pickWarehouse();
        if (event.target?.id === 'ex-open-sku') pickSku();
    });
    document.addEventListener('click', (event) => {
        const trigger = event.target.closest('[data-ex-open]');
        if (trigger) {
            event.preventDefault();
            try { openModal(JSON.parse(trigger.getAttribute('data-ex-open'))); } catch (error) { /* malformed row */ }
        }
    });
    document.addEventListener('DOMContentLoaded', () => byId('ex-open-submit')?.addEventListener('click', submitModal));

    return {
        t, has, can, esc, qty, date, byId, get, showError, hideError, severityBadge, reasonBadge, statusCell, amount, unit, productCell, lotLink, orderUrl, shelfBar, loadWarehouses, rowActions,
        // F5
        call, write, newKey, codeOf, errorText, typeBadge, caseStatusBadge, resolutionText, dateTime, duration, slaCell, person, openModal
    };
})();
