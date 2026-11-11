// WP-0177-R7c — the recall bands on 0173 / 0174 / 0175 and the R7c links, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Recall/recall-bands.js  (RecallBandRules + the RecallBands DOM part over a tiny fake document)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/case-view.js     ("Müşteri iadesi gir" → the MOD-0173 form, pre-filled)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/cases-view.js    (O-R7b-1: a finished drill row → its Kanıt Paketi, a running one → the planner)
// RecallR7cNodeTests.cs runs this file from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const js = (name) => readFileSync(path.join(repo, `frontend/Diten.Web/wwwroot/assets/js/Recall/${name}`), 'utf8');
const plain = (x) => JSON.parse(JSON.stringify(x));
const LOT = '11111111-1111-4111-8111-111111111111';
const CASE = '22222222-2222-4222-8222-222222222222';
const ALL = { status: true, trace: true, cases: true };

const pure = vm.createContext({ window: {}, console, URLSearchParams });
const { RecallBandRules: R, RecallCaseView: caseView, RecallCasesView: casesView } = vm.runInContext(
    `${js('recall-bands.js')}\n${js('case-view.js')}\n${js('cases-view.js')}\n;({ RecallBandRules, RecallCaseView, RecallCasesView })`, pure);

const own = { lotId: LOT, underRecall: true, cases: [{ caseId: CASE, number: 'RC-2026-0004', status: 'Active', sameCompany: true }] };
const other = { lotId: LOT, underRecall: true, cases: [{ number: 'RC-2026-0009', status: 'Active', sameCompany: false }] };
const free = { lotId: LOT, underRecall: false, cases: [] };

test('lot badge: own case linked with recall.cases.read, ANOTHER company only the number (CT-R7); "İzle" only with recall.trace.run; nothing when not under recall', () => {
    assert.deepEqual(plain(R.badge(own, ALL)), { cases: [{ number: 'RC-2026-0004', href: `/Recall/Cases/Details/${CASE}` }], traceHref: `/Recall/Trace?lot=${LOT}` });
    assert.deepEqual(plain(R.badge(other, ALL)), { cases: [{ number: 'RC-2026-0009', href: null }], traceHref: `/Recall/Trace?lot=${LOT}` });
    // another company's case even with a caseId smuggled in: still no link
    assert.equal(R.badge({ ...other, cases: [{ ...other.cases[0], caseId: CASE }] }, ALL).cases[0].href, null);
    assert.deepEqual(plain(R.badge(own, { status: true, trace: false, cases: false })), { cases: [{ number: 'RC-2026-0004', href: null }], traceHref: null });
    assert.equal(R.badge(free, ALL), null);
    assert.equal(R.badge(null, ALL), null);
});

test('exit band: only the blocked types (GOODS_ISSUE_SALES, GOODS_ISSUE_PROD, WAREHOUSE_TRANSFER) on a lot under recall', () => {
    assert.deepEqual(plain(R.EXIT_TYPES), ['GOODS_ISSUE_SALES', 'GOODS_ISSUE_PROD', 'WAREHOUSE_TRANSFER']);
    for (const type of R.EXIT_TYPES) assert.deepEqual(plain(R.exit(own, type)), { cases: [{ number: 'RC-2026-0004', href: null }] }, type);
    for (const type of ['SCRAP', 'WRITE_OFF', 'COUNT_LOSS', 'LOCATION_TRANSFER', 'OPENING_BALANCE', 'COUNT_GAIN', 'CUSTOMER_RETURN']) assert.equal(R.exit(own, type), null, type);
    assert.equal(R.exit(free, 'WAREHOUSE_TRANSFER'), null);
    assert.equal(R.sourceLot(`wh|loc|AVAILABLE|${LOT}`), LOT);
    assert.equal(R.sourceLot('wh|loc|AVAILABLE|LOT-TYPED-7'), null); // a typed lot has no recall status to read
    // the server's refusal → the same band with its case number
    assert.deepEqual(plain(R.refusal(422, { error: { code: 'LOT_UNDER_RECALL', details: { recallCaseNumber: 'RC-2026-0009' } } })), { cases: [{ number: 'RC-2026-0009', href: null }] });
    assert.equal(R.refusal(422, { error: { code: 'INSUFFICIENT_STOCK' } }), null);
});

test('CT-R9 release warning: only "Release" on a lot under recall, and it says it does not block; quarantine source and /LotTrace link', () => {
    assert.deepEqual(plain(R.release(own, ['Release'])), { cases: [{ number: 'RC-2026-0004', href: null }], blocks: false });
    assert.deepEqual(plain(R.release(own, ['Block', 'Release'])).blocks, false);
    assert.equal(R.release(own, ['Block']), null);
    assert.equal(R.release(free, ['Release']), null);
    const hold = { source: 'Recall', sourceDocument: { module: 'MOD-0177', type: 'RECALL_CASE', id: CASE, number: 'RC-2026-0004' } };
    assert.deepEqual(plain(R.holdSource(hold, ALL)), { number: 'RC-2026-0004', href: `/Recall/Cases/Details/${CASE}` });
    assert.deepEqual(plain(R.holdSource(hold, { cases: false })), { number: 'RC-2026-0004', href: null });
    assert.equal(R.holdSource({ source: 'Manual', sourceDocument: { number: 'X' } }, ALL), null);
    assert.equal(R.fullTrace(LOT, { trace: true }), `/Recall/Trace?lot=${LOT}`);
    assert.equal(R.fullTrace(LOT, { trace: false }), null);
});

test('"Müşteri iadesi gir": the 0173 form pre-filled (CUSTOMER_RETURN, product, SKU, root lot, QUARANTINE) once the scope is approved', () => {
    const c = { status: 'Active', rootLot: { lotId: LOT, lotNumber: 'L-1' }, item: { itemId: 'i-1', skuId: 's-1', skuLevel: 'Lsku' } };
    assert.equal(caseView.customerReturnHref(c), `/StockMovements/Create?type=CUSTOMER_RETURN&itemId=i-1&sku=Lsku%3As-1&lotId=${LOT}&toStatus=QUARANTINE`);
    assert.ok(caseView.customerReturnHref({ ...c, status: 'EffectivenessCheck' }));
    for (const status of ['Draft', 'PendingScopeApproval', 'Closed', 'Cancelled']) assert.equal(caseView.customerReturnHref({ ...c, status }), null, status);
    assert.equal(caseView.customerReturnHref({ ...c, rootLot: { lotNumber: 'TYPED' } }), null);
});

test('O-R7b-1: a finished drill row opens its Kanıt Paketi, a running one the Tatbikat Planlayıcı — each only with that page\'s key', () => {
    const fmt = (canDrills, canEvidence) => ({ t: (k) => k, qty: String, date: String, duration: String, canDrills, canEvidence });
    const row = (status) => ({ rowKind: 'Drill', runId: 'r-1', number: 'RC-2026-D001', status, lot: { lotNumber: 'L' } });
    for (const status of ['Completed', 'Cancelled']) {
        assert.deepEqual(plain(casesView.drillLink(row(status), fmt(true, true))), { clickable: true, href: '/Recall/Evidence?drillRun=r-1' }, status);
        assert.equal(casesView.drillLink(row(status), fmt(true, false)).clickable, false);
    }
    assert.deepEqual(plain(casesView.drillLink(row('Running'), fmt(true, true))), { clickable: true, href: '/Recall/Drills?run=r-1' });
    assert.equal(casesView.drillLink(row('Running'), fmt(false, true)).clickable, false);
});

// ── the DOM part over a tiny fake document: the CT-R9 band never disables the decision ────────────────────────────
const fakeDom = (elements, statusJson) => {
    const make = (id, extra = {}) => {
        const classes = new Set(extra.classes || []);
        return {
            id, value: extra.value || '', checked: false, disabled: false, innerHTML: '', textContent: extra.text || '', dataset: { ...(extra.dataset || {}) },
            className: '',
            classList: { add: (c) => classes.add(c), remove: (c) => classes.delete(c), toggle: (c, on) => (on ? classes.add(c) : classes.delete(c)), contains: (c) => classes.has(c) },
            attributes: {}, setAttribute(k, v) { this.attributes[k] = v; }, addEventListener() {}, querySelector: () => null, querySelectorAll: () => []
        };
    };
    const byId = {};
    Object.entries(elements).forEach(([id, extra]) => { byId[id] = make(id, extra); });
    const band = make('band', { classes: ['d-none'] });
    const radio = { value: 'Release' };
    byId['qr-decide-form'].querySelector = (sel) => (sel.startsWith('input[name="qr-decision"]') ? radio : null);
    byId['qr-decide-form'].querySelectorAll = () => [];
    const document = {
        readyState: 'complete',
        getElementById: (id) => byId[id] || null,
        querySelector: (sel) => (sel === '[data-recall-release-band]' ? band : null),
        querySelectorAll: () => [],
        addEventListener() {}
    };
    const fetch = async () => ({ ok: true, json: async () => statusJson });
    return { document, fetch, band, byId };
};

test('the CT-R9 band shows on "Serbest bırak" for a recalled lot and NEVER disables the decision button', async () => {
    const dom = fakeDom({
        'recall-band-config': { text: JSON.stringify({ perms: ALL, l10n: { 'Recall.Band.Release': 'Bu lot geri çağırmada ({0}); serbest bırakmak çıkış engelini kaldırmaz.' } }) },
        'qr-decide-form': {}, 'qr-partial': {}, 'qr-primary': {}
    }, own);
    const ctx = vm.createContext({ window: {}, document: dom.document, fetch: dom.fetch, console, URLSearchParams });
    vm.runInContext(js('recall-bands.js'), ctx);
    ctx.window.RecallBands.decisionFor(LOT);
    await new Promise((r) => setTimeout(r, 10));
    await ctx.window.RecallBands.decision();
    assert.equal(dom.band.className.includes('alert-warning'), true, dom.band.className);
    assert.match(dom.band.innerHTML, /RC-2026-0004/);
    assert.equal(dom.byId['qr-primary'].disabled, false);
    for (const el of Object.values(dom.byId)) assert.equal(el.disabled, false, el.id);
});

test('a failed status read draws no band and throws nothing (the page keeps working); without recall.lots.status.read nothing is read at all', async () => {
    for (const [perms, fetchOk] of [[ALL, false], [{ ...ALL, status: false }, true]]) {
        const dom = fakeDom({ 'recall-band-config': { text: JSON.stringify({ perms, l10n: {} }) }, 'qr-decide-form': {}, 'qr-partial': {}, 'qr-primary': {} }, own);
        let calls = 0;
        const fetch = async () => { calls += 1; if (!fetchOk) throw new Error('network'); return { ok: true, json: async () => own }; };
        const ctx = vm.createContext({ window: {}, document: dom.document, fetch, console, URLSearchParams });
        vm.runInContext(js('recall-bands.js'), ctx);
        ctx.window.RecallBands.decisionFor(LOT);
        await new Promise((r) => setTimeout(r, 10));
        await ctx.window.RecallBands.decision();
        assert.equal(dom.band.innerHTML, '');
        assert.equal(dom.band.classList.contains('d-none'), true);
        if (!perms.status) assert.equal(calls, 0);
    }
});
