// WP-MVP1-FIX-LIVE2 — the screen findings of the live round, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Recall/trace-view.js  (finding 6: a new case opens its Vaka Detayı)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/case-view.js   (finding 4: the stored seals by name; 7: no customer → no bind; 8: the return link carries the texts)
// RecallLive2NodeTests (RecallLive2WebTests.cs) runs this file from `dotnet test`.
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

const pure = vm.createContext({ window: {}, console, URLSearchParams });
const { RecallTraceView: traceView, RecallCaseView: caseView } = vm.runInContext(
    `${js('trace-view.js')}\n${js('case-view.js')}\n;({ RecallTraceView, RecallCaseView })`, pure);

test('finding 6: "Taslak vaka aç" → the new case\'s Vaka Detayı; nothing but a case id makes a link', () => {
    assert.equal(traceView.caseHref(CASE), `/Recall/Cases/Details/${CASE}`);
    for (const bad of [undefined, null, '', 'RC-2026-0001', '../x', `${CASE}/../../x`]) assert.equal(traceView.caseHref(bad), null, String(bad));
});

test('finding 4: the scope tab shows the STORED seals rc-v1 signs, by name, then the live digest; a changed scope is flagged', () => {
    const c = { submittedScope: { revision: 1, sha256: 'a'.repeat(64) }, approvedScope: { revision: 1, sha256: 'a'.repeat(64) } };
    const same = caseView.seals(c, { sha256: 'a'.repeat(64) });
    assert.deepEqual(plain(same.lines.map((l) => [l.kind, l.key, l.hash])), [
        ['submitted', 'Recall.Scope.SubmittedSeal', 'a'.repeat(64)],
        ['approved', 'Recall.Scope.ApprovedSeal', 'a'.repeat(64)],
        ['current', 'Recall.Scope.Seal', 'a'.repeat(64)]
    ]);
    assert.equal(same.differs, false);
    assert.equal(caseView.seals(c, { sha256: 'b'.repeat(64) }).differs, true);
    // a revision under approval is newer than the approved one: compared with the submitted seal
    const revised = { submittedScope: { revision: 2, sha256: 'c'.repeat(64) }, approvedScope: { revision: 1, sha256: 'a'.repeat(64) } };
    assert.equal(caseView.seals(revised, { sha256: 'c'.repeat(64) }).differs, false);
    // a Draft has no stored seal: only the live digest, nothing flagged
    assert.deepEqual(plain(caseView.seals({}, { sha256: 'd'.repeat(64) })), { lines: [{ kind: 'current', key: 'Recall.Scope.Seal', hash: 'd'.repeat(64) }], differs: false });
});

test('finding 7: a return links only to a customer of the scope — none → not linkable', () => {
    assert.equal(caseView.linkable([]), false);
    assert.equal(caseView.linkable(undefined), false);
    assert.equal(caseView.linkable([{ customerRef: 'C-1' }]), true);
});

test('finding 8: "Müşteri iadesi gir" carries the product / SKU texts the case shows — never an id as a name', () => {
    const c = { status: 'Active', rootLot: { lotId: LOT }, item: { itemId: 'i-1', skuId: 's-1', skuLevel: 'Lsku' }, itemLabel: 'GP-0042 · Aspirin 100 mg', skuLabel: 'LS-0042' };
    const href = new URL(caseView.customerReturnHref(c), 'http://x');
    assert.equal(href.pathname, '/StockMovements/Create');
    assert.equal(href.searchParams.get('itemText'), 'GP-0042 · Aspirin 100 mg');
    assert.equal(href.searchParams.get('skuText'), 'LS-0042');
    assert.equal(href.searchParams.get('itemId'), 'i-1');
    // a label that is only the id (the product master could not name it) is not sent as a text
    const bare = new URL(caseView.customerReturnHref({ ...c, itemLabel: 'i-1', skuLabel: null }), 'http://x');
    assert.equal(bare.searchParams.has('itemText'), false);
    assert.equal(bare.searchParams.has('skuText'), false);
});
