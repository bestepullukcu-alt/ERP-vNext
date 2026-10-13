/**
 * MOD-0176 F5 — the bands on other screens (pack §8.3, §11.2; mockup "ex"): the movement form (MOD-0173 /StockMovements — outbound FEFO band,
 * goods-receipt minimum-remaining band) and the lot forms (MOD-0174 Yeni Lot + the quick lot — date suggestion / override). The hooks in the
 * posting and createLot transactions decide; a band only says beforehand what the server will do, and turns the server's refusals into
 * words. Two parts:
 *   ExpiryBandRules — pure decisions (no DOM, measured by a Node test against THIS file): the CT-F1 resubmission, the outbound state,
 *                     the receipt state, the date suggestion;
 *   ExpiryBands     — the DOM of the three bands (Views/Shared/Expiry/_*Band.cshtml), reading /Expiry/band/context and writing
 *                     /Expiry/api/date-overrides; the MOD-0022 dialog (window.ESign.open) signs an override when the company asks.
 */
'use strict';

var ExpiryBandRules = (function () {
    const MIN_REASON = 10;
    const day = (wire) => (wire ? Math.floor(Date.parse(`${String(wire).slice(0, 10)}T00:00:00Z`) / 86400000) : null);
    const wire = (n) => new Date(n * 86400000).toISOString().slice(0, 10);

    /**
     * CT-F1 (pack §8.3.2): a goods receipt into AVAILABLE refused 422 LOT_BELOW_MIN_REMAINING_SHELF_LIFE whose details name
     * requiredToStockStatus = QUARANTINE (the company's "Otomatik karantina") is sent AGAIN — the same movement into QUARANTINE, with a
     * NEW idempotency key — and the reader is told. Without that detail ("Reddet") nothing is resent: only the refusal is shown.
     * The decision is the server's detail alone (never the policy, never a guess). K3 sabotages (c) and (d).
     */
    const receiptRetry = (status, json, sentStatus) => {
        const error = json?.error || {};
        if (status !== 422 || error.code !== 'LOT_BELOW_MIN_REMAINING_SHELF_LIFE') return { resubmit: false };
        const required = error.details?.requiredToStockStatus;
        if (required === 'QUARANTINE' && sentStatus === 'AVAILABLE') return { resubmit: true, toStockStatus: 'QUARANTINE', details: error.details };
        return { resubmit: false, refused: true, details: error.details || {} };
    };

    /**
     * Outbound (KF-B): the issued lot against the server's FEFO order (getFefoOrder). Equal expiry = compliant; no order / no lot = nothing to
     * say. Mode unknown (the reader may not read the policy) → "reason" (the server asks or not — its 422 is the authority).
     */
    const outbound = (order, lotId, mode) => {
        const first = order?.eligible?.[0];
        if (!first || !lotId) return { state: 'none' };
        const issued = (order.eligible || []).find((e) => e.lotId === lotId);
        if (first.lotId === lotId || (issued && issued.expiryDate && issued.expiryDate === first.expiryDate)) return { state: 'ok', first };
        return { state: mode === 'Block' ? 'block' : mode === 'Warn' ? 'warn' : 'reason', first };
    };

    /** The server's outbound refusal → the band (422 FEFO_BYPASS_REASON_REQUIRED → reason; 422 FEFO_ORDER_VIOLATION → block). */
    const outboundRefusal = (status, json) => {
        const error = json?.error || {};
        if (status !== 422) return null;
        if (error.code === 'FEFO_BYPASS_REASON_REQUIRED') return { state: 'reason', details: error.details || {} };
        if (error.code === 'FEFO_ORDER_VIOLATION') return { state: 'block', details: error.details || {} };
        return null;
    };

    /** A reason the server would take (10–1000) — the band's own check before the post; the server repeats it. */
    const reasonOk = (text) => String(text || '').trim().length >= MIN_REASON;

    /**
     * Goods receipt (KF-C): remaining = expiry − posting date (days); below the SKU's minimum → warn / quar / reject by the company mode
     * (unknown mode → "below"); no minimum (null — "Tanımsız") or no expiry → the rule is not applied.
     */
    const receipt = ({ expiryDate, postingDate, minRemaining, mode }) => {
        const expiry = day(expiryDate);
        const posting = day(postingDate);
        if (expiry === null || posting === null) return { state: 'none' };
        if (minRemaining === null || minRemaining === undefined) return { state: 'undefined', remaining: expiry - posting };
        const remaining = expiry - posting;
        if (remaining >= Number(minRemaining)) return { state: 'ok', remaining, min: Number(minRemaining) };
        const state = mode === 'Warn' ? 'warn' : mode === 'AutoQuarantine' ? 'quar' : mode === 'Reject' ? 'reject' : 'below';
        return { state, remaining, min: Number(minRemaining) };
    };

    /** KF-D: suggested expiry = manufacturing + shelfLifeDays, retest = manufacturing + retestDays (none when either is absent). */
    const suggestion = (manufacturingDate, shelfLifeDays, retestDays) => {
        const m = day(manufacturingDate);
        if (m === null) return { expiry: null, retest: null };
        return {
            expiry: shelfLifeDays === null || shelfLifeDays === undefined ? null : wire(m + Number(shelfLifeDays)),
            retest: retestDays === null || retestDays === undefined ? null : wire(m + Number(retestDays))
        };
    };

    /** Differs (ASSUMPTION-0176-07): a suggestion exists and the entered date is another day — an empty entry while one is suggested differs. */
    const differs = (suggested, entered) => ({
        expiry: !!suggested.expiry && (entered.expiry || '') !== suggested.expiry,
        retest: !!suggested.retest && (entered.retest || '') !== suggested.retest
    });

    return { MIN_REASON, receiptRetry, outbound, outboundRefusal, reasonOk, receipt, suggestion, differs };
})();

if (typeof window !== 'undefined') window.ExpiryBandRules = ExpiryBandRules;

if (typeof window !== 'undefined' && typeof document !== 'undefined') window.ExpiryBands = (function () {
    const R = ExpiryBandRules;
    const byId = (id) => document.getElementById(id);
    const L = (() => { try { return JSON.parse(byId('expiry-band-l10n')?.textContent || '{}'); } catch (error) { return {}; } })();
    const t = (key, ...args) => String(L[key] || '').replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const lang = document.documentElement.lang || undefined;
    const date = (w) => (w ? new Date(`${String(w).slice(0, 10)}T00:00:00Z`).toLocaleDateString(lang, { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'UTC' }) : '');
    const newKey = () => (window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`);
    const antiforgery = () => document.querySelector('#ex-antiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const getJson = async (url) => {
        try {
            const res = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            return { ok: res.ok, status: res.status, json: await res.json().catch(() => null) };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const postJson = async (url, body, key) => {
        try {
            const res = await fetch(url, {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json', RequestVerificationToken: antiforgery(), 'Idempotency-Key': key },
                body: JSON.stringify(body)
            });
            return { ok: res.ok, status: res.status, json: await res.json().catch(() => null) };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const cache = new Map();
    const context = async ({ itemId, sku, warehouseId }) => {
        const [level, skuId] = String(sku || '').split(':');
        if (!skuId) return null;
        const key = [itemId, sku, warehouseId].join('|');
        if (!cache.has(key)) {
            const query = new URLSearchParams({ skuId, skuLevel: level });
            if (itemId) query.set('itemId', itemId);
            if (warehouseId) query.set('warehouseId', warehouseId);
            cache.set(key, getJson(`/Expiry/band/context?${query}`).then((r) => (r.ok ? r.json : null)));
        }
        return cache.get(key);
    };
    const TONE = { ok: 'success', warn: 'warning', reason: 'warning', block: 'danger', quar: 'primary', reject: 'danger', below: 'warning', undefined: 'secondary' };
    const draw = (box, state, title, text) => {
        if (!box) return;
        if (!state || state === 'none') { box.classList.add('d-none'); box.innerHTML = ''; return; }
        box.className = `alert alert-${TONE[state] || 'secondary'} d-flex gap-2 mb-3`;
        box.setAttribute('data-ex-band-state', state);
        box.innerHTML = `<i class="bx ${state === 'ok' ? 'bx-check-circle' : state === 'block' || state === 'reject' ? 'bx-block' : state === 'quar' ? 'bx-shield-quarter' : 'bx-error-circle'} mt-1"></i>`
            + `<div><div class="fw-medium">${esc(title)}</div>${text ? `<div class="small">${esc(text)}</div>` : ''}</div>`;
    };

    // ── the movement form (MOD-0173) ─────────────────────────────────────────────────────────────────────────────
    const movement = (() => {
        let outboundState = 'none';
        const box = () => byId('ex-band-movement');
        const receiptBox = () => byId('ex-band-receipt');
        const reasonBox = () => byId('ex-band-reason-group');
        const reasonField = () => byId('ex-band-reason');
        const submit = () => document.querySelector('#movement-form button[type="submit"], #btnMovementSubmit');
        const setOutbound = (state, first) => {
            outboundState = state;
            const lot = first ? t('Expiry.Band.FefoFirst', first.lotNumber || '', date(first.expiryDate)) : '';
            const text = { ok: t('Expiry.Band.FefoOkText'), warn: t('Expiry.Band.FefoWarnText'), reason: t('Expiry.Band.FefoReasonText'), block: t('Expiry.Band.FefoBlockText') }[state] || '';
            draw(box(), state, state === 'ok' ? t('Expiry.Band.FefoOkTitle') : lot, text);
            reasonBox()?.classList.toggle('d-none', !['reason', 'warn'].includes(state));
            if (submit()) submit().disabled = state === 'block';
        };

        /** Outbound: the source row's lot against the FEFO order of its warehouse. */
        const onSource = async ({ direction, itemId, sku, warehouseId, lotId }) => {
            if (direction !== 'out' || !lotId || !warehouseId) { setOutbound('none'); return; }
            const ctx = await context({ itemId, sku, warehouseId });
            const result = R.outbound(ctx?.order, lotId, ctx?.policy?.fefoOutboundMode);
            setOutbound(result.state, result.first);
        };

        /** Goods receipt: the picked lot's expiry against the SKU's minimum remaining shelf life. */
        const onLot = async ({ direction, movementType, itemId, sku, expiryDate, postingDate }) => {
            if (direction !== 'in' || !['GOODS_RECEIPT_PO', 'GOODS_RECEIPT_PROD', 'CUSTOMER_RETURN'].includes(movementType) || !expiryDate) { draw(receiptBox(), 'none'); return; }
            const ctx = await context({ itemId, sku });
            const result = R.receipt({ expiryDate, postingDate, minRemaining: ctx?.sku?.minRemainingShelfLifeDays, mode: ctx?.policy?.belowMinRemainingMode });
            if (result.state === 'undefined') { draw(receiptBox(), 'undefined', t('Expiry.Band.MinUndefined'), ''); return; }
            if (result.state === 'none') { draw(receiptBox(), 'none'); return; }
            const title = result.state === 'ok' ? t('Expiry.Band.MinOkTitle', result.remaining, result.min) : t('Expiry.Band.MinBelowTitle', result.remaining, result.min);
            const text = { ok: t('Expiry.Band.MinOkText'), warn: t('Expiry.Band.MinWarnText'), quar: t('Expiry.Band.MinQuarText'), reject: t('Expiry.Band.MinRejectText'), below: t('Expiry.Band.MinBelowText') }[result.state];
            draw(receiptBox(), result.state, title, text);
        };

        /** The post body gains the bypass reason only when the band asked for one and it was written. */
        const extend = (body) => {
            const text = reasonField()?.value?.trim();
            if (text && !reasonBox()?.classList.contains('d-none')) body.fefoBypassReason = text;
            return body;
        };

        /** A refusal of the post: FEFO → the band; CT-F1 → { resubmit } (the form resends with a new key); else null (the form's own alert). */
        const refused = (status, json, body) => {
            const fefo = R.outboundRefusal(status, json);
            if (fefo) {
                const d = fefo.details;
                setOutbound(fefo.state, { lotNumber: d.lotNumber, expiryDate: d.expiryDate });
                if (fefo.state === 'reason') reasonField()?.focus();
                return { handled: true };
            }
            const retry = R.receiptRetry(status, json, body?.toStockStatus);
            if (retry.resubmit) {
                draw(receiptBox(), 'quar', t('Expiry.Band.ResubmitTitle', retry.details?.remainingDays ?? '', retry.details?.requiredDays ?? ''), t('Expiry.Band.ResubmitText'));
                return { handled: true, resubmit: true, toStockStatus: retry.toStockStatus };
            }
            if (retry.refused) {
                draw(receiptBox(), 'reject', t('Expiry.Band.MinBelowTitle', retry.details?.remainingDays ?? '', retry.details?.requiredDays ?? ''), t('Expiry.Band.MinRejectText'));
                return { handled: false };
            }
            return null;
        };

        const reasonValid = () => reasonBox()?.classList.contains('d-none') || outboundState !== 'reason' || R.reasonOk(reasonField()?.value);

        return { onSource, onLot, extend, refused, reasonValid, newKey };
    })();

    // ── the lot forms (MOD-0174 Yeni Lot + the quick lot) ───────────────────────────────────────────────────────────
    const lot = (() => {
        const forms = new Map();

        /** ids: { root, manufacturing, expiry, retest, band, reasonGroup, reason, note, use }; getSku(): { itemId, sku } */
        const attach = (name, ids, getSku) => {
            const f = { ids, getSku, suggested: { expiry: null, retest: null }, mode: null };
            forms.set(name, f);
            const refresh = async () => {
                const s = getSku();
                const manufacturing = byId(ids.manufacturing)?.value;
                const ctx = s?.sku ? await context({ itemId: s.itemId, sku: s.sku }) : null;
                f.mode = ctx?.policy?.dateOverrideMode || null;
                f.suggested = R.suggestion(manufacturing, ctx?.sku?.shelfLifeDays, ctx?.sku?.retestDays);
                const entered = { expiry: byId(ids.expiry)?.value || '', retest: byId(ids.retest)?.value || '' };
                const diff = R.differs(f.suggested, entered);
                const band = byId(ids.band);
                byId(ids.use)?.classList.toggle('d-none', !(f.suggested.expiry || f.suggested.retest) || !(diff.expiry || diff.retest));
                if (!f.suggested.expiry && !f.suggested.retest) { draw(band, 'none'); byId(ids.reasonGroup)?.classList.add('d-none'); return; }
                const text = [f.suggested.expiry ? t('Expiry.Band.SuggestExpiry', date(f.suggested.expiry), ctx?.sku?.shelfLifeDays) : null,
                    f.suggested.retest ? t('Expiry.Band.SuggestRetest', date(f.suggested.retest), ctx?.sku?.retestDays) : t('Expiry.Band.NoRetest')].filter(Boolean).join(' · ');
                draw(band, diff.expiry || diff.retest ? 'warn' : 'ok', diff.expiry || diff.retest ? t('Expiry.Band.DiffersTitle') : t('Expiry.Band.SuggestTitle'), text);
                [ids.expiry, ids.retest].forEach((id, i) => byId(id)?.classList.toggle('border-warning', [diff.expiry, diff.retest][i]));
                byId(ids.reasonGroup)?.classList.toggle('d-none', !(diff.expiry || diff.retest));
                const note = byId(ids.note);
                if (note) note.textContent = f.mode === 'ReasonAndSignature' ? t('Expiry.Band.NoteSign') : f.mode === 'Reason' ? t('Expiry.Band.NoteReason') : t('Expiry.Band.NoteServer');
            };
            [ids.manufacturing, ids.expiry, ids.retest].forEach((id) => byId(id)?.addEventListener('change', refresh));
            byId(ids.use)?.addEventListener('click', () => {
                if (f.suggested.expiry && byId(ids.expiry)) byId(ids.expiry).value = f.suggested.expiry;
                if (f.suggested.retest && byId(ids.retest)) byId(ids.retest).value = f.suggested.retest;
                refresh();
            });
            f.refresh = refresh;
            return refresh;
        };

        /**
         * Before createLot: no difference → nothing; a difference → reason ≥ 10 → createDateOverride → (signatureRequired) the MOD-0022 dialog →
         * { ok, dateOverride: { overrideId, signatureId? } }. A refusal / a cancelled dialog → { ok: false, message }.
         */
        const beforeSave = async (name, lotNumber) => {
            const f = forms.get(name);
            if (!f) return { ok: true };
            await f.refresh?.();
            const ids = f.ids;
            const entered = { expiry: byId(ids.expiry)?.value || '', retest: byId(ids.retest)?.value || '' };
            const diff = R.differs(f.suggested, entered);
            if (!diff.expiry && !diff.retest) return { ok: true };
            const reason = byId(ids.reason)?.value?.trim() || '';
            if (!R.reasonOk(reason)) return { ok: false, field: 'reason', message: t('Expiry.Band.ReasonMin') };
            const s = f.getSku();
            const [level, skuId] = String(s.sku).split(':');
            const key = newKey();
            const body = { idempotencyKey: key, itemId: s.itemId, skuId, skuLevel: level, lotNumber, manufacturingDate: byId(ids.manufacturing)?.value, reason };
            if (entered.expiry) body.expiryDate = entered.expiry;
            if (entered.retest) body.retestDate = entered.retest;
            const reply = await postJson('/Expiry/api/date-overrides', body, key);
            if (!reply.ok) return { ok: false, message: t(`Expiry.Band.Errors.${reply.json?.error?.code}`) || t('Expiry.Band.OverrideFailed') };
            const prepared = reply.json;
            if (prepared.signatureRequired === false) return { ok: true, dateOverride: { overrideId: prepared.overrideId } };
            if (!window.ESign?.open) return { ok: false, message: t('Expiry.Band.SignUnavailable') };
            const signature = await window.ESign.open({ module: 'expiry', subjectType: 'expiry.date-override', subjectId: prepared.overrideId, actionCode: 'expiry.lot-date.override' });
            if (!signature?.signatureId) return { ok: false, cancelled: true, message: t('Expiry.Band.SignCancelled') };
            return { ok: true, dateOverride: { overrideId: prepared.overrideId, signatureId: signature.signatureId } };
        };

        /** A LOT_DATE_OVERRIDE_* refusal of createLot in the band's words (null for any other code). */
        const refusal = (json) => {
            const code = json?.error?.code;
            return code && String(code).startsWith('LOT_DATE_OVERRIDE') ? t(`Expiry.Band.Errors.${code}`) || t('Expiry.Band.OverrideFailed') : null;
        };

        return { attach, beforeSave, refusal };
    })();

    return { movement, lot, t };
})();
