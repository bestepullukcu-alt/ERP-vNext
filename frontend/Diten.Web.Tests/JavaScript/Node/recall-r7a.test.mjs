// WP-0177-R7a — Vaka Detayı's pure parts, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Recall/case-view.js      (which button: key + status + four-eyes CT-R1)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/case-decision.js  (the signed decisions: prepare → MOD-0022 dialog → command, pack §8.8)
//   frontend/Diten.Web/Resources/Views/Recall/Recall.tr.resx       (the refusal sentences)
// RecallR7aNodeTests.cs runs this file from `dotnet test`.
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

const context = vm.createContext({ window: {}, console });
const { RecallCaseView: view, RecallCaseDecision: decision } = vm.runInContext(
    `${js('case-view.js')}\n${js('case-decision.js')}\n;({ RecallCaseView, RecallCaseDecision })`, context);
const plain = (x) => JSON.parse(JSON.stringify(x));

const ALL = ['RECALL_SCOPE_MARK', 'RECALL_SCOPE_SUBMIT', 'RECALL_SCOPE_RETURN', 'RECALL_SCOPE_APPROVE', 'RECALL_SCOPE_REVISE', 'RECALL_QUARANTINE_REQUEST',
    'RECALL_SEGREGATION_CONFIRM', 'RECALL_CUSTOMER_NOTIFY', 'RECALL_CONSUMPTION_DECLARE', 'RECALL_RETURN_LINK', 'RECALL_EFFECTIVENESS_START',
    'RECALL_EFFECTIVENESS_COMPLETE', 'RECALL_CASE_CLOSE', 'RECALL_CASE_CANCEL'];
const keys = (...codes) => (code) => codes.includes(code);
const QA1 = '11111111-1111-4111-8111-111111111111';
const QA2 = '22222222-2222-4222-8222-222222222222';

// button → (its action code, a case in which the service accepts it)
const BUTTONS = {
    mark: ['RECALL_SCOPE_MARK', { status: 'Draft' }],
    submit: ['RECALL_SCOPE_SUBMIT', { status: 'Draft' }],
    returnScope: ['RECALL_SCOPE_RETURN', { status: 'PendingScopeApproval' }],
    approve: ['RECALL_SCOPE_APPROVE', { status: 'PendingScopeApproval', scopeSubmittedBy: { userId: QA1 } }],
    revise: ['RECALL_SCOPE_REVISE', { status: 'Active', approvedScope: { revision: 1 } }],
    quarantine: ['RECALL_QUARANTINE_REQUEST', { status: 'Active' }],
    segregate: ['RECALL_SEGREGATION_CONFIRM', { status: 'Active' }],
    notify: ['RECALL_CUSTOMER_NOTIFY', { status: 'EffectivenessCheck' }],
    consume: ['RECALL_CONSUMPTION_DECLARE', { status: 'Active' }],
    link: ['RECALL_RETURN_LINK', { status: 'Active' }],
    effStart: ['RECALL_EFFECTIVENESS_START', { status: 'Active' }],
    effComplete: ['RECALL_EFFECTIVENESS_COMPLETE', { status: 'EffectivenessCheck' }],
    close: ['RECALL_CASE_CLOSE', { status: 'EffectivenessCheck', effectiveness: { totals: [] }, effectivenessCompletedBy: { userId: QA1 } }],
    cancel: ['RECALL_CASE_CANCEL', { status: 'PendingScopeApproval' }]
};

test('every button needs its own action key (pack §11.3): with it shown, without it — every other key held — absent', () => {
    for (const [button, [code, c]] of Object.entries(BUTTONS)) {
        assert.equal(view.offers(c, keys(...ALL), QA2)[button], true, `${button} with ${code}`);
        assert.equal(view.offers(c, keys(...ALL.filter((k) => k !== code)), QA2)[button], false, `${button} without ${code}`);
    }
});

test('a button shows only in the statuses the service accepts it (pack §8.7)', () => {
    const can = keys(...ALL);
    const statuses = ['Draft', 'PendingScopeApproval', 'Active', 'EffectivenessCheck', 'Closed', 'Cancelled'];
    const shown = (button) => statuses.filter((s) => view.offers({ ...BUTTONS[button][1], status: s }, can, QA2)[button]);
    assert.deepEqual(shown('submit'), ['Draft']);
    assert.deepEqual(shown('returnScope'), ['PendingScopeApproval']);
    assert.deepEqual(shown('approve'), ['PendingScopeApproval']);
    assert.deepEqual(shown('cancel'), ['Draft', 'PendingScopeApproval']);
    assert.deepEqual(shown('notify'), ['Active', 'EffectivenessCheck']);
    assert.deepEqual(shown('effStart'), ['Active']);
    assert.deepEqual(shown('close'), ['EffectivenessCheck']);
    // a pending scope revision is approved on an Active case (CT-R5); a completed check is not completed twice; no close before completion
    assert.equal(view.offers({ status: 'Active', pendingRevision: { revision: 2 }, scopeSubmittedBy: { userId: QA1 } }, can, QA2).approve, true);
    assert.equal(view.offers({ status: 'EffectivenessCheck', effectiveness: { totals: [] } }, can, QA2).effComplete, false);
    assert.equal(view.offers({ status: 'EffectivenessCheck' }, can, QA2).close, false);
    assert.equal(view.offers({ status: 'Active', approvedScope: {}, pendingRevision: { revision: 2 } }, can, QA2).revise, false);
});

test('four-eyes on the screen (CT-R1): the scope submitter is never offered "Kapsamı onayla", the completer never "Vakayı kapat"', () => {
    const can = keys(...ALL);
    const pending = { status: 'PendingScopeApproval', scopeSubmittedBy: { userId: QA1 } };
    assert.deepEqual([view.offers(pending, can, QA1).approve, view.offers(pending, can, QA1).approveSod], [false, true]);
    assert.deepEqual([view.offers(pending, can, QA1.toUpperCase()).approve], [false]); // ids compared case-insensitively
    assert.deepEqual([view.offers(pending, can, QA2).approve, view.offers(pending, can, QA2).approveSod], [true, false]);
    const revision = { status: 'Active', pendingRevision: { revision: 2 }, scopeSubmittedBy: { userId: QA2 } };
    assert.equal(view.offers(revision, can, QA2).approve, false); // the reviser submitted the revision
    const completed = { status: 'EffectivenessCheck', effectiveness: { totals: [] }, effectivenessCompletedBy: { userId: QA1 } };
    assert.deepEqual([view.offers(completed, can, QA1).close, view.offers(completed, can, QA1).closeSod], [false, true]);
    assert.equal(view.offers(completed, can, QA2).close, true);
    assert.equal(tr[view.errorKey('RECALL_SOD_VIOLATION', 'approve')], 'Kapsamı gönderen kişi onaylayamaz.');
    assert.equal(tr[view.errorKey('RECALL_SOD_VIOLATION', 'close')], 'Etkinlik kontrolünü tamamlayan kişi vakayı kapatamaz.');
    for (const code of ['RECALL_SIGNATURE_MISMATCH', 'RECALL_SIGNATURE_REQUIRED', 'RECALL_PENDING_ACTION_MISMATCH', 'RECALL_SCOPE_CHANGED', 'LOT_UNDER_RECALL']) {
        assert.ok(tr[view.errorKey(code, 'approve')], `a sentence for ${code}`);
    }
});

test('the scope is drawn as the mockup\'s tree: every item under its parent, depth-first, each once', () => {
    const items = [
        { nodeKey: 'Shipment:fg:S1', parentKey: 'Lot:fg' }, { nodeKey: 'Lot:rm', isRoot: true }, { nodeKey: 'Lot:fg', parentKey: 'Lot:rm' },
        { nodeKey: 'Stock:fg:W1', parentKey: 'Lot:fg' }, { nodeKey: 'Stock:rm:W1', parentKey: 'Lot:rm' }, { nodeKey: 'Manual:x', parentKey: 'Lot:gone' }
    ];
    const order = plain(view.treeOrder(items)).map((r) => `${r.depth}:${r.item.nodeKey}`);
    assert.deepEqual(order, ['0:Lot:rm', '1:Lot:fg', '2:Shipment:fg:S1', '2:Stock:fg:W1', '1:Stock:rm:W1', '0:Manual:x']);
    const cyclic = plain(view.treeOrder([{ nodeKey: 'A', parentKey: 'B' }, { nodeKey: 'B', parentKey: 'A' }]));
    assert.equal(cyclic.length, 2);
});

// a fake server: records every call; prepare answers the prepared decision at version 8
const fakeDeps = (policy = { signatureRequired: true }, signer = async () => ({ signatureId: 'SIG-77' })) => {
    const calls = [];
    let n = 0;
    return {
        calls,
        newKey: () => `r7-key-${++n}`,
        sign: async (subject) => { calls.push({ sign: subject }); return signer(subject); },
        write: async (p, body, options) => {
            calls.push({ path: p, body, ifMatch: options.ifMatch });
            if (p.includes('/prepare-')) {
                return { ok: true, status: 200, json: { caseId: 'c-1', action: 'x', ...policy, actionCode: 'recall.scope.approve', meaningCode: 'APPROVE',
                    subject: { module: 'recall', subjectType: 'recall.case', subjectId: 'c-1' }, version: 8 } };
            }
            return { ok: true, status: 200, json: { caseId: 'c-1', version: 9 } };
        }
    };
};

test('a signed decision is prepare → MOD-0022 signature → command, with the prepared version and the same fields (pack §8.8)', async () => {
    const deps = fakeDeps();
    const result = plain(await decision.decide(deps, 'approve', { caseId: 'c-1', version: 7, note: 'Kapsam kontrol edildi' }));
    assert.deepEqual([result.ok, result.step, result.signatureId], [true, 'commit', 'SIG-77']);
    const [prep, sign, commit] = plain(deps.calls);
    assert.deepEqual([prep.path, prep.ifMatch, prep.body.note], ['/Recall/cmd/cases/c-1/prepare-approval', 'W/"7"', 'Kapsam kontrol edildi']);
    assert.deepEqual(sign.sign, { module: 'recall', subjectType: 'recall.case', subjectId: 'c-1', actionCode: 'recall.scope.approve', meaningCode: 'APPROVE' });
    assert.deepEqual([commit.path, commit.ifMatch, commit.body.note, commit.body.signatureId], ['/Recall/cmd/cases/c-1/approve', 'W/"8"', 'Kapsam kontrol edildi', 'SIG-77']);
    assert.equal(deps.calls.length, 3);

    const close = fakeDeps();
    await decision.decide(close, 'close', { caseId: 'c-1', version: 3, summary: 'Geri cagirma tamamlandi', acceptance: 'Musteri beyani ile kabul', openRemains: true });
    const paths = plain(close.calls).map((c) => c.path || 'sign');
    assert.deepEqual(paths, ['/Recall/cmd/cases/c-1/prepare-closure', 'sign', '/Recall/cmd/cases/c-1/close']);
    assert.equal(plain(close.calls)[2].body.openQuantityAcceptance, 'Musteri beyani ile kabul');
});

test('no command without a prepared decision; no signature when the policy asks none; a cancelled dialog or a bad field sends nothing', async () => {
    const unsigned = fakeDeps({ signatureRequired: false });
    const result = plain(await decision.decide(unsigned, 'complete', { caseId: 'c-1', version: 4, note: 'Etkinlik degerlendirildi' }));
    assert.deepEqual([result.ok, result.signatureId], [true, undefined]);
    assert.deepEqual(plain(unsigned.calls).map((c) => c.path), ['/Recall/cmd/cases/c-1/prepare-completion', '/Recall/cmd/cases/c-1/effectiveness/complete']);
    assert.equal(plain(unsigned.calls)[1].body.signatureId, undefined);

    const cancelled = fakeDeps(undefined, async () => null);
    const refused = plain(await decision.decide(cancelled, 'approve', { caseId: 'c-1', version: 7 }));
    assert.deepEqual([refused.ok, refused.step, refused.cancelled], [false, 'sign', true]);
    assert.equal(plain(cancelled.calls).filter((c) => c.path?.endsWith('/approve')).length, 0);

    const short = fakeDeps();
    const invalid = plain(await decision.decide(short, 'complete', { caseId: 'c-1', version: 4, note: 'kisa' }));
    assert.deepEqual([invalid.ok, invalid.step, Object.keys(invalid.errors)], [false, 'validate', ['note']]);
    const noAcceptance = plain(await decision.decide(short, 'close', { caseId: 'c-1', version: 4, summary: 'Geri cagirma tamamlandi', openRemains: true }));
    assert.deepEqual(Object.keys(noAcceptance.errors), ['acceptance']);
    assert.equal(short.calls.length, 0);

    const direct = fakeDeps();
    const bare = plain(await decision.signAndCommit(direct, 'approve', null, { caseId: 'c-1', version: 7 }));
    assert.deepEqual([bare.ok, bare.missing, direct.calls.length], [false, true, 0]);
});
