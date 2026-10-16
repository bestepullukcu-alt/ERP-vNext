/**
 * MOD-0177 R7c — the recall bands on other modules' screens (pack §11.1 "Diğer Ekranlar", §11.2; the MOD-0176 expiry-bands.js pattern). The
 * server decides — the exit block is INVENTORY's 422 LOT_UNDER_RECALL, a release stays allowed (CT-R9); a band only says beforehand what the
 * reader will meet and turns the refusal into the same sentence. Two parts:
 *   RecallBandRules — pure decisions (no DOM; measured by a Node test against THIS file): which band shows, with which case numbers and links;
 *   RecallBands     — the DOM: reads /Recall/band/lots/{lotId}/status (getLotRecallStatus, recall.lots.status.read) and draws into the
 *                     partials of Views/Shared/Recall. A failed read draws NOTHING (the page keeps working).
 * CT-R7: a case of ANOTHER company of the tenant shows its number only — never a link (the service gives it no caseId either).
 */
'use strict';

var RecallBandRules = (function () {
    /** INVENTORY 1.4.0 LOT_UNDER_RECALL: the outbound types the exit block refuses (KR-C). Every other type is NOT blocked. */
    const EXIT_TYPES = ['GOODS_ISSUE_SALES', 'GOODS_ISSUE_PROD', 'WAREHOUSE_TRANSFER'];
    const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

    const isLot = (lotId) => GUID.test(String(lotId || ''));

    /** The cases a status names: number + a link ONLY for a case of the reader's company AND a reader with recall.cases.read. */
    const cases = (status, perms) => (status?.cases || []).filter((c) => c && c.number).map((c) => ({
        number: c.number,
        href: c.sameCompany === true && isLot(c.caseId) && perms?.cases ? `/Recall/Cases/Details/${encodeURIComponent(c.caseId)}` : null
    }));

    /** 0174 lot detail: "Açık geri çağırma: RC-…" + "İzle" (→ /Recall/Trace?lot=…, only with recall.trace.run). */
    const badge = (status, perms) => {
        if (!status?.underRecall) return null;
        const list = cases(status, perms);
        if (list.length === 0) return null;
        return { cases: list, traceHref: perms?.trace && isLot(status.lotId) ? `/Recall/Trace?lot=${encodeURIComponent(status.lotId)}` : null };
    };

    /** 0173 movement form: red band only for a blocked type on a lot under recall. */
    const exit = (status, movementType) => {
        if (!status?.underRecall || !EXIT_TYPES.includes(movementType)) return null;
        const list = cases(status, null);
        return list.length === 0 ? null : { cases: list };
    };

    /** The server's own refusal (422 LOT_UNDER_RECALL; details.recallCaseNumber) → the same band. */
    const refusal = (status, json) => {
        const error = json?.error || {};
        if (status !== 422 || error.code !== 'LOT_UNDER_RECALL') return null;
        const number = error.details?.recallCaseNumber;
        return { cases: number ? [{ number, href: null }] : [] };
    };

    /** 0175 decision (CT-R9): a yellow WARNING when a release to AVAILABLE is chosen for a lot under recall — never a block. */
    const release = (status, decisions) => {
        if (!status?.underRecall || !(decisions || []).includes('Release')) return null;
        const list = cases(status, null);
        return list.length === 0 ? null : { cases: list, blocks: false };
    };

    /** 0175 record: "Kaynak: Geri çağırma RC-…" from the hold's own sourceDocument; the link only with recall.cases.read. */
    const holdSource = (hold, perms) => {
        const doc = hold?.sourceDocument;
        if (!doc || !(hold.source === 'Recall' || doc.type === 'RECALL_CASE')) return null;
        const number = doc.number || doc.text;
        if (!number) return null;
        return { number, href: perms?.cases && isLot(doc.id) ? `/Recall/Cases/Details/${encodeURIComponent(doc.id)}` : null };
    };

    /** CT-R15: /LotTrace's "Tam izleme (geri çağırma)" — only with recall.trace.run. */
    const fullTrace = (lotId, perms) => (perms?.trace && isLot(lotId) ? `/Recall/Trace?lot=${encodeURIComponent(lotId)}` : null);

    /** The lot of a movement-form source row (warehouseId|locationId|stockStatus|lotId) — a REGISTERED lot only. */
    const sourceLot = (key) => {
        const parts = String(key || '').split('|');
        return parts.length === 4 && isLot(parts[3]) ? parts[3] : null;
    };

    return { EXIT_TYPES, isLot, cases, badge, exit, refusal, release, holdSource, fullTrace, sourceLot };
})();

if (typeof window !== 'undefined') window.RecallBandRules = RecallBandRules;

if (typeof window !== 'undefined' && typeof document !== 'undefined') window.RecallBands = (function () {
    const R = RecallBandRules;
    const config = (() => { try { return JSON.parse(document.getElementById('recall-band-config')?.textContent || '{}'); } catch (error) { return {}; } })();
    const perms = config.perms || {};
    const L = config.l10n || {};
    const t = (key, ...args) => String(L[key] || '').replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const numbers = (list) => list.map((c) => (c.href ? `<a href="${esc(c.href)}" class="alert-link">${esc(c.number)}</a>` : esc(c.number))).join(', ');
    const plain = (list) => list.map((c) => c.number).join(', ');
    /** A sentence with {0} = the case numbers (already HTML): the sentence's own text escaped, the numbers kept. */
    const fill = (key, html) => t(key, '\u0000').split('\u0000').map(esc).join(html);
    const hide = (box) => { if (box) { box.classList.add('d-none'); box.innerHTML = ''; } };

    const cache = new Map();
    /** getLotRecallStatus through the Recall door; null when not readable (no key, unknown lot, service down) — then no band. */
    const status = (lotId) => {
        if (!perms.status || !R.isLot(lotId)) return Promise.resolve(null);
        if (!cache.has(lotId)) {
            cache.set(lotId, fetch(`/Recall/band/lots/${encodeURIComponent(lotId)}/status`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
                .then((res) => (res.ok ? res.json() : null)).catch(() => null));
        }
        return cache.get(lotId);
    };

    const drawBadge = async (box) => {
        const band = R.badge(await status(box.dataset.lotId), perms);
        if (!band) { hide(box); return; }
        box.innerHTML = `<span class="badge bg-label-danger d-inline-flex flex-wrap align-items-center gap-1 text-wrap" data-recall-badge><i class="bx bx-error-alt"></i>${fill('Recall.Band.Badge', numbers(band.cases))}</span>`
            + (band.traceHref ? ` <a class="btn btn-xs btn-label-danger" href="${esc(band.traceHref)}" data-recall-trace><i class="bx bx-git-branch me-1"></i>${esc(t('Recall.Band.Trace'))}</a>` : '');
        box.classList.remove('d-none');
    };

    const drawBand = (box, band, tone, key) => {
        if (!box) return;
        if (!band) { hide(box); return; }
        box.className = `alert alert-${tone} d-flex gap-2 py-2 px-3 small mt-2 mb-0`;
        box.innerHTML = `<i class="bx ${tone === 'danger' ? 'bx-block' : 'bx-error'} mt-1"></i><div>${fill(key, `<strong>${numbers(band.cases)}</strong>`)}</div>`;
    };

    // ── 0173 movement form ───────────────────────────────────────────────────────────────────────────────────────
    const exitBox = () => document.querySelector('[data-recall-exit-band]');
    const movement = async () => {
        const box = exitBox();
        if (!box) return;
        const type = document.getElementById('movementType')?.value || '';
        const lot = R.sourceLot(document.getElementById('source')?.value);
        const band = R.EXIT_TYPES.includes(type) && lot ? R.exit(await status(lot), type) : null;
        drawBand(box, band, 'danger', 'Recall.Band.Exit');
        box.dataset.recallState = band ? 'exit' : 'none';
    };
    /** form.js hands the server's refusal over: the band shows it, and the form's alert gets THE SAME sentence. */
    const exitRefused = (statusCode, json) => {
        const band = R.refusal(statusCode, json);
        if (!band) return null;
        drawBand(exitBox(), band, 'danger', 'Recall.Band.Exit');
        return t('Recall.Band.Exit', plain(band.cases));
    };

    // ── 0175 decision (CT-R9) ────────────────────────────────────────────────────────────────────────────────────
    let decisionLot = null;
    const decisionBox = () => document.querySelector('[data-recall-release-band]');
    const decisions = () => {
        const form = document.getElementById('qr-decide-form');
        if (!form) return [];
        const partial = document.getElementById('qr-partial')?.checked;
        return partial
            ? [...form.querySelectorAll('[data-line-decision]')].map((s) => s.value)
            : [form.querySelector('input[name="qr-decision"]:checked')?.value].filter(Boolean);
    };
    const decision = async () => {
        const box = decisionBox();
        if (!box) return;
        const band = decisionLot ? R.release(await status(decisionLot), decisions()) : null;
        drawBand(box, band, 'warning', 'Recall.Band.Release');
    };
    const decisionFor = (lotId) => { decisionLot = R.isLot(lotId) ? lotId : null; decision(); };

    // ── 0175 record ──────────────────────────────────────────────────────────────────────────────────────────────
    const holdSource = (box, hold) => {
        const target = box || document.querySelector('[data-recall-hold-source]');
        if (!target) return;
        const band = R.holdSource(hold, perms);
        if (!band) { hide(target); return; }
        target.innerHTML = `<span class="badge bg-label-danger text-wrap"><i class="bx bx-error-alt me-1"></i>${fill('Recall.Band.HoldSource', band.href ? `<a href="${esc(band.href)}" class="text-danger fw-medium">${esc(band.number)}</a>` : esc(band.number))}</span>`;
        target.classList.remove('d-none');
    };

    // ── 0174 /LotTrace (CT-R15) ──────────────────────────────────────────────────────────────────────────────────
    const lotTrace = () => {
        const link = document.querySelector('[data-recall-lottrace]');
        if (!link) return;
        const lot = document.getElementById('lotTraceLot')?.value || link.dataset.lotId;
        const href = R.fullTrace(lot, perms);
        link.classList.toggle('d-none', !href);
        if (href) link.setAttribute('href', href);
    };

    const init = () => {
        document.querySelectorAll('[data-recall-lot-badge]').forEach(drawBadge);
        lotTrace();
        const jq = window.jQuery;
        ['movementType', 'source'].forEach((id) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener('change', movement);
            if (jq) jq(el).on('change', movement);
        });
        const picker = document.getElementById('lotTraceLot');
        if (picker) { picker.addEventListener('change', lotTrace); if (jq) jq(picker).on('change', lotTrace); }
        document.getElementById('qr-decide-form')?.addEventListener('change', decision);
        document.getElementById('qr-decide-form')?.addEventListener('input', decision);
        movement();
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();

    return { status, movement, exitRefused, decisionFor, decision, holdSource, lotTrace };
})();
