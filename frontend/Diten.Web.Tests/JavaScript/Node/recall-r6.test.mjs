// WP-0177-R6 — the recall pages' pure parts, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Recall/trace-view.js  (tree + table from ONE node list, the CT-R8 count)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/cases-view.js  (a drill row: "Tatbikat", no class / level — D-25, never a link in R6)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/dashboard.js   (the cards print exactly the getDashboard numbers, the frequency band)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/recall-ui.js   (RecallFormat.duration)
//   frontend/Diten.Web/Resources/Views/Recall/Recall.tr.resx    (the words)
// RecallR6NodeTests.cs runs this file from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const read = (p) => readFileSync(path.join(repo, p), 'utf8');
const js = (name) => read(`frontend/Diten.Web/wwwroot/assets/js/Recall/${name}`);
const decodeXml = (s) => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');
const tr = Object.fromEntries([...read('frontend/Diten.Web/Resources/Views/Recall/Recall.tr.resx')
    .matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)].map((m) => [m[1], decodeXml(m[2])]));
const t = (key, ...args) => String(tr[key] ?? '').replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`);

// a page with no DOM work: the IIFEs that draw return early or only register a listener
const context = vm.createContext({
    window: {},
    document: { getElementById: () => null, documentElement: { lang: 'tr' }, addEventListener: () => {}, querySelector: () => null },
    Intl, console
});
vm.runInContext(js('recall-ui.js'), context);
const views = vm.runInContext(`${js('trace-view.js')}\n${js('cases-view.js')}\n${js('dashboard.js')}\n;({ RecallTraceView, RecallCasesView, RecallDashboardView, RecallFormat })`, context);
const plain = (x) => JSON.parse(JSON.stringify(x));
const fmt = { t, qty: (v) => String(v), date: (v) => String(v).slice(0, 10), duration: (m) => views.RecallFormat.duration(m, t),
    percent: (p) => (p == null ? '—' : `%${String(p).replace('.', ',')}`) };

// ── a trace as the service sends it for company A (rule-10 order) ───────────────────────────────────────────────────────
const trace = {
    rootLotId: 'fg', durationMs: 1800, truncated: false,
    nodes: [
        { nodeKey: 'Lot:rm', level: 1, side: 'Backward', kind: 'RawMaterialLot', lot: { lotId: 'rm', lotNumber: 'LKT-25K004' }, itemLabel: 'Amoksisilin trihidrat' },
        { nodeKey: 'Input:pk', parentKey: 'Lot:fg', level: 1, side: 'Backward', kind: 'Input', untracked: true, itemLabel: 'Blister folyo' },
        { nodeKey: 'Lot:fg', parentKey: 'Lot:rm', level: 0, side: 'Root', kind: 'FinishedGoodLot', lot: { lotId: 'fg', lotNumber: 'AMX-2512' }, itemLabel: 'Amoksisilin 500mg' },
        { nodeKey: 'Stock:fg:D01', parentKey: 'Lot:fg', level: 1, kind: 'Stock', lot: { lotId: 'fg', lotNumber: 'AMX-2512' }, warehouseLabel: 'D01 İstanbul', stockStatus: 'AVAILABLE', quantity: 450, baseUomId: 'EA' },
        { nodeKey: 'Shipment:fg:SHP-1', parentKey: 'Lot:fg', level: 1, kind: 'Shipment', lot: { lotId: 'fg', lotNumber: 'AMX-2512' }, quantity: 400, baseUomId: 'EA', customerRef: 'CUST-100/ADDR-2',
            movement: { transactionNumber: 'HRK-1', sourceDocumentId: 'SHP-1', postingDate: '2026-07-30T00:00:00Z' }, shipment: { read: 'Ok', status: 'Delivered' } },
        { nodeKey: 'Shipment:fg:SHP-2', parentKey: 'Lot:fg', level: 1, kind: 'Shipment', lot: { lotId: 'fg', lotNumber: 'AMX-2512' }, quantity: 250, baseUomId: 'EA',
            movement: { transactionNumber: 'HRK-2', sourceDocumentId: 'SHP-2', postingDate: '2026-10-09T00:00:00Z' }, shipment: { read: 'Unavailable' } },
        { nodeKey: 'Gap:LOTLESS:tx9', parentKey: 'Lot:fg', level: 1, kind: 'Gap', gapCode: 'LOTLESS_OUTBOUND', text: 'Outbound HRK-9 of a lot-controlled SKU has no lot', quantity: 40, baseUomId: 'EA' }
    ],
    gaps: [{ code: 'LOTLESS_OUTBOUND', nodeKey: 'Gap:LOTLESS:tx9', text: '…' }, { code: 'OTHER_COMPANY_ACTIVITY', nodeKey: 'Gap:OTHER', text: '…', count: 3 }],
    summary: { finishedGoodLots: 1, otherCompanyActivityCount: 3 }
};

test('tree and table show the SAME nodes — the table in the service order, the tree depth-first under the root', () => {
    const table = plain(views.RecallTraceView.tableRows(trace, t));
    const tree = plain(views.RecallTraceView.treeRows(trace, t));
    assert.deepEqual(table.map((r) => r.key), trace.nodes.map((n) => n.nodeKey));
    assert.deepEqual([...tree.map((r) => r.key)].sort(), [...table.map((r) => r.key)].sort());
    assert.deepEqual(tree.map((r) => [r.key, r.depth]), [
        ['Lot:rm', 0], ['Lot:fg', 1], ['Input:pk', 2], ['Stock:fg:D01', 2], ['Shipment:fg:SHP-1', 2], ['Shipment:fg:SHP-2', 2], ['Gap:LOTLESS:tx9', 2]]);
    for (const r of table) {
        const twin = tree.find((x) => x.key === r.key);
        assert.deepEqual([twin.title, twin.kindText, twin.place, twin.ref], [r.title, r.kindText, r.place, r.ref], r.key);
    }
});

test('a gap is a red row, "Takip dışı" is a badge (not a gap), an unreadable shipment says so, the customer is the shipToReference', () => {
    const rows = plain(views.RecallTraceView.tableRows(trace, t));
    const gap = rows.find((r) => r.key === 'Gap:LOTLESS:tx9');
    assert.equal(gap.gap, true);
    assert.equal(gap.title, tr['Recall.Gap.LOTLESS_OUTBOUND']);
    const input = rows.find((r) => r.key === 'Input:pk');
    assert.equal(input.gap, false);
    assert.deepEqual(input.badges.map((b) => b.key), ['Recall.Badge.Untracked']);
    assert.equal(rows.find((r) => r.key === 'Shipment:fg:SHP-1').place, t('Recall.Shipment.Customer', 'CUST-100/ADDR-2'));
    assert.deepEqual(rows.find((r) => r.key === 'Shipment:fg:SHP-2').badges.map((b) => b.key), ['Recall.Shipment.Unreadable']);
    assert.equal(rows.find((r) => r.key === 'Shipment:fg:SHP-2').place, tr['Recall.Shipment.UnknownCustomer']);
});

test('CT-R8: another company is ONE count (the band), never a row; it is not counted as a gap of this company', () => {
    const s = plain(views.RecallTraceView.summary(trace));
    assert.deepEqual(s, { finishedGoodLots: 1, stockLines: 1, shipments: 2, gaps: 1, otherCompany: 3, truncated: false, seconds: 1.8 });
    assert.equal(views.RecallTraceView.tableRows(trace, t).length, trace.nodes.length); // nothing added
});

test('a drill row is not a case: "Tatbikat", class and level "—" (D-25), its result and duration, never a link', () => {
    const drill = plain(views.RecallCasesView.cells({ rowKind: 'Drill', number: 'RC-2026-D011', status: 'Completed', passed: true, durationMinutes: 102,
        lot: { lotNumber: 'PRC-2502' }, itemLabel: 'Parasetamol', startedAtUtc: '2026-06-05T09:00:00Z', owner: { userId: 'u', name: 'Charlie Brown' } }, fmt));
    assert.deepEqual([drill.type, drill.class, drill.level, drill.progress, drill.clickable],
        [tr['Recall.Type.Drill'], '—', '—', `${tr['Recall.Drill.Passed']} · ${t('Recall.Duration.HoursMinutes', 1, 42)}`, false]);
    assert.equal(drill.status, tr['Recall.DrillStatus.Completed']);
    const real = plain(views.RecallCasesView.cells({ rowKind: 'Case', number: 'RC-2026-0004', status: 'Active', class: 2, level: 'Pharmacy',
        rootLot: { lotNumber: 'LKT-25K004' }, openedAtUtc: '2026-10-06T09:12:00Z', owner: { userId: 'u' }, progress: [{ returned: 180, shipped: 1150, baseUomId: 'EA' }] }, fmt));
    assert.deepEqual([real.type, real.class, real.level, real.progress, real.clickable],
        [tr['Recall.Type.Real'], t('Recall.Class.N', 2), tr['Recall.Level.Pharmacy'], '180 / 1150 EA', false]);
});

test('the panel cards print exactly the getDashboard numbers; the band shows only when the server says so', () => {
    const panel = {
        openCases: { total: 3, byClass: { 1: 1, 2: 2, 3: 0 } },
        lastDrill: { runId: 'r', number: 'RC-2026-D011', durationMinutes: 300, targetHours: 4, onTarget: false },
        lotCoverage: { windowDays: 90, withLot: 1522, total: 1524, percent: 99.9 },
        traceGaps: { windowDays: 90, count: 2, movements: [{ number: 'HRK-2026-08877' }, { number: 'HRK-2026-08902' }] },
        returnedOpenCases: [{ number: 'RC-2026-0004', skuId: 's', baseUomId: 'EA', quantity: 280 }, { number: 'RC-2026-0003', skuId: 's', baseUomId: 'EA', quantity: 60 }]
    };
    const c = plain(views.RecallDashboardView.cards(panel, t, fmt));
    assert.equal(c.open.value, '3');
    assert.equal(c.open.sub, t('Recall.Kpi.OpenSub', 1, 2, 0));
    assert.deepEqual([c.lastDrill.value, c.lastDrill.badge], [t('Recall.Duration.HoursMinutes', 5, 0), 'OverTarget']);
    assert.equal(c.coverage.value, '%99,9');
    assert.equal(c.coverage.sub, t('Recall.Kpi.CoverageSub', 90, '1522', '1524'));
    assert.deepEqual([c.gaps.value, c.gaps.sub], ['2', 'HRK-2026-08877, HRK-2026-08902']);
    assert.deepEqual([c.returned.value, c.returned.sub], ['340 EA', 'RC-2026-0004: 280 EA · RC-2026-0003: 60 EA']); // one SKU + unit → one total

    assert.equal(views.RecallDashboardView.band({ warning: false, warningDays: 180 }, t, fmt), null);
    assert.deepEqual(plain(views.RecallDashboardView.band({ warning: true, warningDays: 120, daysSinceLastCompleted: 129, lastCompletedAtUtc: '2026-06-05T10:00:00Z' }, t, fmt)),
        { title: t('Recall.Freq.Band', 120), detail: t('Recall.Freq.Detail', '2026-06-05', 129) });
    // quantities of two units are never summed
    assert.deepEqual(plain(views.RecallDashboardView.sumByUnit([{ skuId: 's', baseUomId: 'EA', quantity: 1 }, { skuId: 's', baseUomId: 'BOX', quantity: 2 }])),
        [{ baseUomId: 'EA', quantity: 1 }, { baseUomId: 'BOX', quantity: 2 }]);
});
