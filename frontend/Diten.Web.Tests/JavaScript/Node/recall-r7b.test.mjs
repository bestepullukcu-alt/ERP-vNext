// WP-0177-R7b — the pure parts of Tatbikat Planlayıcı, Ayarlar and Kanıt Paketi, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Recall/drill-view.js     (server clock, step order, cancel reason, the two-step result signature)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/settings-form.js  (reason ≥ 10, prepare → MOD-0022 when the policy asks → save)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/evidence-view.js  (subject, section order, export → download → SHA-256 comparison)
// RecallR7bNodeTests.cs runs this file from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const js = (name) => readFileSync(path.join(repo, `frontend/Diten.Web/wwwroot/assets/js/Recall/${name}`), 'utf8');

// The browser's clock is POISONED: Date.now() and new Date() (no argument) answer a time 3 days off. Anything that reads them shows a wrong clock.
const RealDate = Date;
class PoisonedDate extends RealDate {
    constructor(...args) { super(...(args.length === 0 ? [RealDate.parse('2026-10-18T23:59:00Z')] : args)); }
    static now() { return RealDate.parse('2026-10-18T23:59:00Z'); }
}
const context = vm.createContext({ window: {}, console, Date: PoisonedDate, Uint8Array, Number, Math, String, JSON, Object, Array, encodeURIComponent });
const { RecallDrillView: drill, RecallSettingsForm: settings, RecallEvidenceView: evidence } = vm.runInContext(
    `${js('drill-view.js')}\n${js('settings-form.js')}\n${js('evidence-view.js')}\n;({ RecallDrillView, RecallSettingsForm, RecallEvidenceView })`, context);
const plain = (x) => JSON.parse(JSON.stringify(x));
const RUN = '0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f';
const LOT = '11111111-1111-4111-8111-111111111111';

/** A recording deps: write → the scripted answers in order; sign → the scripted signature. */
const recorder = (answers = [], signature = { signatureId: 'sig-1' }) => {
    const calls = [];
    return {
        calls,
        newKey: () => `r7-key-${calls.length + 1}`,
        write: async (p, body, options) => { calls.push({ kind: 'write', path: p, body: plain(body), ifMatch: options?.ifMatch, language: options?.language }); return answers.shift() || { ok: true, status: 200, json: {} }; },
        sign: async (subject) => { calls.push({ kind: 'sign', subject: plain(subject) }); return signature; }
    };
};

// ── drills ───────────────────────────────────────────────────────────────────────────────────────────────────────

test('the running clock is the server\'s: serverNowUtc − startedAtUtc + the monotonic time since the answer; the browser clock is never read', () => {
    const run = { status: 'Running', startedAtUtc: '2026-10-15T08:00:00Z', targetHours: 4, nextStep: 'Scope', steps: [{ step: 'Trace' }] };
    const c = plain(drill.clock(run, '2026-10-15T09:42:30Z', 0));
    assert.deepEqual([c.minutes, c.text, c.over, c.remaining, c.stopped], [102, '01:42', false, 138, false]);
    assert.equal(drill.clock(run, '2026-10-15T09:42:30Z', 60000).text, '01:43'); // one monotonic minute later
    assert.equal(drill.clock(run, '2026-10-15T12:30:00Z', 0).over, true);        // past the 4 h target
    assert.equal(drill.clock(run, null, 0), null);                               // no server time → no guess
    const stopped = plain(drill.clock({ ...run, durationMinutes: 222, nextStep: null }, '2026-10-20T00:00:00Z', 999999));
    assert.deepEqual([stopped.minutes, stopped.text, stopped.stopped, stopped.over], [222, '03:42', true, false]); // the server's duration
    const cancelled = plain(drill.clock({ ...run, status: 'Cancelled', endedAtUtc: '2026-10-15T08:25:00Z' }, '2026-10-20T00:00:00Z', 999999));
    assert.deepEqual([cancelled.minutes, cancelled.stopped], [25, true]);           // a cancelled run stands at its end, not at the server's now
});

test('the step button always sends the run\'s own nextStep with its version; the panel offers by key and status', async () => {
    const deps = recorder();
    await drill.step(deps, { runId: RUN, status: 'Running', nextStep: 'Scope', version: 5 });
    assert.deepEqual(deps.calls.map((c) => [c.path, c.ifMatch]), [[`/Recall/cmd/drill-runs/${RUN}/steps/Scope`, 'W/"5"']]);
    const none = await drill.step(deps, { runId: RUN, status: 'Running', nextStep: null, version: 6 });
    assert.equal(none.ok, false);
    assert.equal(deps.calls.length, 1);
    assert.deepEqual(plain(drill.steps({ status: 'Running', nextStep: 'Scope', steps: [{ step: 'Trace', completedAtUtc: 'x' }] })).map((s) => [s.step, s.done, s.current]),
        [['Trace', true, false], ['Scope', false, true], ['CustomerList', false, false]]);

    const can = (...codes) => (code) => codes.includes(code);
    const running = { status: 'Running', nextStep: 'Trace', steps: [] };
    const finished = { status: 'Running', nextStep: null, steps: [{ step: 'Trace' }, { step: 'Scope' }, { step: 'CustomerList' }] };
    assert.deepEqual(plain(drill.offers(running, can('RECALL_DRILL_RUN', 'RECALL_DRILL_SIGN'), true)), { planSave: false, start: false, step: true, cancel: true, sign: false });
    assert.deepEqual(plain(drill.offers(finished, can('RECALL_DRILL_RUN', 'RECALL_DRILL_SIGN'), true)), { planSave: false, start: false, step: false, cancel: true, sign: true });
    assert.deepEqual(plain(drill.offers(finished, can(), true)), { planSave: false, start: false, step: false, cancel: false, sign: false });
    assert.equal(drill.offers(null, can('RECALL_DRILL_PLAN_SAVE'), false).planSave, true);
    assert.equal(drill.offers(null, can('RECALL_DRILL_RUN'), false).start, true);
    assert.equal(drill.offers({ ...finished, status: 'Completed' }, can('RECALL_DRILL_RUN', 'RECALL_DRILL_SIGN'), false).sign, false);
});

test('"Sistem seçsin" sends no lot (the server picks); "Ben seçeyim" needs one', async () => {
    const deps = recorder();
    await drill.start(deps, 'plan-1', 'Auto', LOT);
    assert.deepEqual(deps.calls[0].body, { idempotencyKey: 'r7-key-1', planId: 'plan-1', selectionMode: 'Auto' });
    assert.equal((await drill.start(deps, 'plan-1', 'Manual', '')).ok, false);
    assert.equal(deps.calls.length, 1);
    await drill.start(deps, 'plan-1', 'Manual', LOT);
    assert.deepEqual(deps.calls[1].body, { idempotencyKey: 'r7-key-2', planId: 'plan-1', selectionMode: 'Manual', lotId: LOT });
});

test('"Tatbikatı iptal et" needs a reason of at least 10 characters — without it nothing is sent', async () => {
    const deps = recorder();
    const run = { runId: RUN, status: 'Running', version: 7 };
    assert.deepEqual(plain(await drill.cancel(deps, run, 'kisa')), { ok: false, step: 'validate', errors: { reason: 'Recall.Common.ReasonMin' } });
    assert.equal(deps.calls.length, 0);
    await drill.cancel(deps, run, '  Ekip toplantisi nedeniyle durduruldu  ');
    assert.deepEqual([deps.calls[0].path, deps.calls[0].ifMatch, deps.calls[0].body.reason], [`/Recall/cmd/drill-runs/${RUN}/cancel`, 'W/"7"', 'Ekip toplantisi nedeniyle durduruldu']);
});

test('the result is two steps: prepare → the MOD-0022 dialog (unless the policy says no) → result with the signatureId and the PREPARED version', async () => {
    const run = { runId: RUN, status: 'Running', nextStep: null, version: 8 };
    const prepared = { signatureRequired: true, actionCode: 'recall.drill.result', meaningCode: 'APPROVE', version: 9, subject: { module: 'recall', subjectType: 'recall.drill-run', subjectId: RUN } };
    const deps = recorder([{ ok: true, json: prepared }, { ok: true, json: { status: 'Completed' } }]);
    const note = 'Tatbikat hedef surede tamamlandi';
    assert.equal((await drill.prepareResult(deps, run, 'kisa')).ok, false);
    const first = await drill.prepareResult(deps, run, note);
    assert.equal(first.signatureRequired, true);
    const done = await drill.signResult(deps, run, first.prepared, note);
    assert.equal(done.ok, true);
    assert.deepEqual(deps.calls.map((c) => c.kind === 'sign' ? ['sign', c.subject.actionCode, c.subject.subjectType, c.subject.subjectId] : [c.path, c.ifMatch]), [
        [`/Recall/cmd/drill-runs/${RUN}/prepare-result`, 'W/"8"'],
        ['sign', 'recall.drill.result', 'recall.drill-run', RUN],
        [`/Recall/cmd/drill-runs/${RUN}/result`, 'W/"9"']]);
    assert.deepEqual(deps.calls[2].body, { idempotencyKey: 'r7-key-3', resultNote: note, signatureId: 'sig-1' });

    const cancelled = recorder([], null);
    assert.equal((await drill.signResult(cancelled, run, prepared, note)).cancelled, true);
    assert.deepEqual(cancelled.calls.map((c) => c.kind), ['sign']);                  // a closed dialog sends nothing
    const unsigned = recorder();
    await drill.signResult(unsigned, run, { ...prepared, signatureRequired: false }, note);
    assert.deepEqual(unsigned.calls.map((c) => c.kind), ['write']);                  // policy says no → no dialog
    assert.equal(drill.needsSignature({ version: 1 }), true);                        // fail-closed
    assert.equal((await drill.signResult(recorder(), run, null, note)).missing, true); // never a result without a prepare
});

test('the plan form: name, frequency (custom days 1–730), scope, next date, owner', async () => {
    const ok = { name: 'Ceyrek tatbikat', frequency: 'Quarterly', scopeKind: 'RandomFinishedGoodLot', nextDueOn: '2026-12-01', ownerUserId: LOT };
    assert.deepEqual(plain(drill.validatePlan(ok)), {});
    assert.deepEqual(Object.keys(plain(drill.validatePlan({ ...ok, frequency: 'CustomDays', customDays: 0 }))), ['customDays']);
    const deps = recorder();
    await drill.savePlan(deps, { planId: 'p-1', version: 2 }, ok);
    assert.deepEqual([deps.calls[0].path, deps.calls[0].ifMatch], ['/Recall/cmd/drill-plans/p-1', 'W/"2"']);
    await drill.savePlan(deps, null, { ...ok, frequency: 'CustomDays', customDays: 45 });
    assert.deepEqual([deps.calls[1].path, deps.calls[1].body.customDays], ['/Recall/cmd/drill-plans', 45]);
});

// ── settings ─────────────────────────────────────────────────────────────────────────────────────────────────────

const SETTINGS = { drillTargetHours: 4, drillFrequencyWarningDays: 180, effectivenessTargetPercent: { 1: 100, 2: 95, 3: 90 }, traceMaxDepth: 20, traceMaxNodes: 5000, version: 3 };

test('a settings change needs a reason of at least 10 characters and a real change — otherwise nothing is sent', async () => {
    const deps = recorder();
    const form = { ...plain(settings.read(SETTINGS)), drillFrequencyWarningDays: '120' };
    assert.deepEqual(plain((await settings.prepare(deps, SETTINGS, { ...form, reason: 'kisa' })).errors), { reason: 'Recall.Common.ReasonMin' });
    assert.deepEqual(plain((await settings.prepare(deps, SETTINGS, { ...plain(settings.read(SETTINGS)), reason: 'Ic denetim bulgusu IA-2026-07' })).errors), { form: 'Recall.Errors.RECALL_NO_CHANGES' });
    assert.deepEqual(Object.keys(plain(settings.validate({ ...form, drillTargetHours: '0', e2: '101', reason: 'Ic denetim bulgusu IA-2026-07' }))), ['drillTargetHours', 'e2']);
    assert.equal(deps.calls.length, 0);
    assert.deepEqual(plain(settings.changed(SETTINGS, form)), ['drillFrequencyWarningDays']);
});

test('"Kaydet": prepare (rs-v1) → MOD-0022 when the policy asks → updateSettings with the SAME proposal, the trace limits unchanged', async () => {
    const form = { ...plain(settings.read(SETTINGS)), drillFrequencyWarningDays: '120', e3: '92.5', reason: 'Ic denetim bulgusu IA-2026-07' };
    const prepared = { signatureRequired: true, actionCode: 'recall.settings.update', meaningCode: 'APPROVE', version: 3, subject: { module: 'recall', subjectType: 'recall.settings-change', subjectId: 'chg-1' } };
    const deps = recorder([{ ok: true, json: prepared }, { ok: true, json: { version: 4 } }]);
    const first = await settings.prepare(deps, SETTINGS, form);
    const saved = await settings.save(deps, SETTINGS, first.prepared, form);
    assert.equal(saved.ok, true);
    assert.deepEqual(deps.calls.map((c) => c.kind === 'sign' ? ['sign', c.subject.subjectType, c.subject.subjectId] : [c.path, c.ifMatch]),
        [['/Recall/cmd/settings/prepare', 'W/"3"'], ['sign', 'recall.settings-change', 'chg-1'], ['/Recall/cmd/settings', 'W/"3"']]);
    const { idempotencyKey: k1, ...p1 } = deps.calls[0].body;
    const { idempotencyKey: k2, signatureId, ...p2 } = deps.calls[2].body;
    assert.deepEqual(p1, p2);                                             // the same proposal the server fingerprinted
    assert.deepEqual([p2.traceMaxDepth, p2.traceMaxNodes, p2.drillFrequencyWarningDays, p2.effectivenessTargetPercent['3'], signatureId], [20, 5000, 120, 92.5, 'sig-1']);

    const unsigned = recorder([{ ok: true, json: { ...prepared, signatureRequired: false } }]);
    const again = await settings.prepare(unsigned, SETTINGS, form);
    await settings.save(unsigned, SETTINGS, again.prepared, form);
    assert.deepEqual(unsigned.calls.map((c) => c.kind), ['write', 'write']);
    assert.equal('signatureId' in unsigned.calls[1].body, false);
    const closed = recorder([{ ok: true, json: prepared }], null);
    const third = await settings.prepare(closed, SETTINGS, form);
    assert.equal((await settings.save(closed, SETTINGS, third.prepared, form)).cancelled, true);
    assert.deepEqual(closed.calls.map((c) => c.kind), ['write', 'sign']);  // no save after a closed dialog
});

// ── evidence ─────────────────────────────────────────────────────────────────────────────────────────────────────

test('the subject: exactly a case, a drill run or a period of at most 366 days (end ≥ start)', () => {
    assert.deepEqual(plain(evidence.subject({ kind: 'Case', id: RUN })), { ok: true, params: { caseId: RUN } });
    assert.deepEqual(plain(evidence.subject({ kind: 'DrillRun', id: RUN })), { ok: true, params: { runId: RUN } });
    assert.deepEqual(plain(evidence.subject({ kind: 'Period', from: '2026-01-01', to: '2026-12-31' })), { ok: true, params: { from: '2026-01-01', to: '2026-12-31' } });
    assert.equal(evidence.subject({ kind: 'Period', from: '2026-01-01', to: '2027-01-02' }).error, 'Recall.Errors.RECALL_EVIDENCE_RANGE_INVALID');
    assert.equal(evidence.subject({ kind: 'Period', from: '2026-03-01', to: '2026-02-01' }).error, 'Recall.Errors.RECALL_EVIDENCE_RANGE_INVALID');
    assert.equal(evidence.subject({ kind: 'Case', id: 'not-a-guid' }).ok, false);
    assert.deepEqual(plain(evidence.sections({ sections: { '7_signatures': { applicable: true }, '1_summary': { applicable: true } } })).map((s) => [s.key, s.section !== null]),
        [['1_summary', true], ['2_trace', false], ['3_quarantine', false], ['4_customers', false], ['5_returnsAndEffectiveness', false], ['6_timeline', false], ['7_signatures', true]]);
});

test('"PDF indir": export → download THE stored file → its SHA-256 is compared with the logged one (a mismatch is never "verified")', async () => {
    const bytes = new Uint8Array(Buffer.from('%PDF-1.7 recall evidence pack'));
    const sha = createHash('sha256').update(bytes).digest('hex');
    const make = (logged) => {
        const calls = [];
        return {
            calls,
            newKey: () => 'r7-key-9',
            write: async (p, body, options) => { calls.push({ p, body: plain(body), language: options.language }); return { ok: true, json: { exportId: 'e-1', sha256: logged, fileName: 'RC_kanit.pdf' } }; },
            download: async (url) => { calls.push({ url }); return { ok: true, bytes }; },
            sha256: async (b) => createHash('sha256').update(b).digest('hex')
        };
    };
    const good = make(sha);
    const r = await evidence.exportFile(good, { kind: 'Case', id: RUN }, 'Pdf', 'tr');
    assert.deepEqual([r.ok, r.verified, r.computed], [true, true, sha]);
    assert.deepEqual(good.calls, [
        { p: '/Recall/cmd/evidence/exports', body: { idempotencyKey: 'r7-key-9', format: 'Pdf', caseId: RUN }, language: 'tr' },
        { url: '/Recall/export/evidence/e-1' }]);
    const bad = await evidence.exportFile(make('0'.repeat(64)), { kind: 'Case', id: RUN }, 'Csv', 'en');
    assert.deepEqual([bad.ok, bad.verified], [true, false]);
    const refused = make(sha);
    assert.equal((await evidence.exportFile(refused, { kind: 'Period', from: '2026-01-01' }, 'Pdf', 'en')).ok, false);
    assert.equal(refused.calls.length, 0);
    assert.equal(evidence.hex(new Uint8Array([0, 15, 255]).buffer), '000fff');
});
