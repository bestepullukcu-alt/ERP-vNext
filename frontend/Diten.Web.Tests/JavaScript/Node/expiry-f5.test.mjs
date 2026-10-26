// WP-0176-F5 — the decision drawer's calls and the bands' decisions, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Expiry/expiry-decision.js  (prepare → MOD-0022 sign when asked → resolve → close)
//   frontend/Diten.Web/wwwroot/assets/js/Expiry/expiry-bands.js     (CT-F1 resubmission, outbound / receipt states, date suggestion)
// ExpiryF5NodeTests.cs runs this file from `dotnet test` (the CI frontend gate). What this does NOT run is the DOM (the drawer, the
// band boxes); those are the Razor tests and the screenshots of the evidence.
//   node --test frontend/Diten.Web.Tests/JavaScript/Node/expiry-f5.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const js = (name) => readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Expiry', name), 'utf8');
// no `window` / `document`: the files define their pure parts and skip the DOM parts
const context = vm.createContext({});
const decision = vm.runInContext(`${js('expiry-decision.js')}\n;ExpiryDecision`, context);
const bands = vm.runInContext(`${js('expiry-bands.js')}\n;ExpiryBandRules`, context);

/** A fake same-origin API: records every call, answers by path. */
const api = (answers) => {
    const calls = [];
    let n = 0;
    return {
        calls,
        newKey: () => `key-${++n}`,
        write: async (method, p, body, options) => {
            calls.push({ method, path: p, body, options });
            const verb = p.split('/').pop();
            return answers[verb] ? answers[verb](body) : { ok: false, status: 404, json: null };
        }
    };
};
const prepared = (signatureRequired) => ({
    ok: true, status: 200,
    json: { exceptionId: 'c1', resolution: 'AcceptedWithReason', signatureRequired, actionCode: 'expiry.exception.accept', meaningCode: 'APPROVE',
        visualMode: 'savedAccepted', subject: { module: 'expiry', subjectType: 'expiry.exception', subjectId: 'c1' }, expiresAtUtc: '2026-10-13T12:15:00Z', version: 4 }
});
const input = { exceptionId: 'c1', version: 3, resolution: 'AcceptedWithReason', reason: 'Müşteri talebi: uzun raf ömürlü lot istendi' };

test('drawer end to end: prepare → the MOD-0022 signature (test double) → resolve → close, with the versions and keys', async () => {
    const fake = api({
        'pending-resolution': () => prepared(true),
        resolve: () => ({ ok: true, status: 200, json: { status: 'Accepted', version: 5 } }),
        close: () => ({ ok: true, status: 200, json: { status: 'Closed', version: 6 } })
    });
    const signed = [];
    const deps = { ...fake, sign: async (subject) => { signed.push(subject); return { signatureId: 'sig-1', signatureNumber: 'ES-1' }; } };

    const result = await decision.decide(deps, input);
    assert.equal(result.ok, true);
    assert.equal(result.signatureId, 'sig-1');
    assert.deepEqual(fake.calls.map((c) => c.path), ['exceptions/c1/pending-resolution', 'exceptions/c1/resolve']);
    assert.equal(fake.calls[0].options.ifMatch, 'W/"3"');                // the case version
    assert.equal(fake.calls[1].options.ifMatch, 'W/"4"');                // the prepared version
    assert.equal(fake.calls[0].options.idempotencyKey, fake.calls[0].body.idempotencyKey);
    assert.notEqual(fake.calls[0].body.idempotencyKey, fake.calls[1].body.idempotencyKey);
    assert.equal(fake.calls[1].body.signatureId, 'sig-1');
    assert.equal(fake.calls[1].body.reason, input.reason);
    assert.deepEqual(JSON.parse(JSON.stringify(signed)), [{ module: 'expiry', subjectType: 'expiry.exception', subjectId: 'c1', actionCode: 'expiry.exception.accept' }]);

    const closed = await decision.close(fake, { exceptionId: 'c1', version: 5 });
    assert.equal(closed.ok, true);
    assert.equal(fake.calls[2].path, 'exceptions/c1/close');
    assert.equal(fake.calls[2].options.ifMatch, 'W/"5"');
});

test('K3 sabotage (e): the signature step is skipped ONLY when the server says signatureRequired: false', async () => {
    for (const flag of [true, undefined, null]) assert.equal(decision.needsSignature({ signatureRequired: flag }), true, String(flag));
    assert.equal(decision.needsSignature({ signatureRequired: false }), false);

    const fake = api({ 'pending-resolution': () => prepared(false), resolve: () => ({ ok: true, status: 200, json: { version: 5 } }) });
    let asked = 0;
    const result = await decision.decide({ ...fake, sign: async () => { asked++; return { signatureId: 'x' }; } }, input);
    assert.equal(result.ok, true);
    assert.equal(asked, 0);
    assert.equal('signatureId' in fake.calls[1].body, false);
});

test('a closed signature dialog sends nothing; the SoD refusal is the server\'s answer', async () => {
    const fake = api({ 'pending-resolution': () => prepared(true), resolve: () => ({ ok: true, status: 200, json: {} }) });
    const cancelled = await decision.decide({ ...fake, sign: async () => null }, input);
    assert.equal(cancelled.ok, false);
    assert.equal(cancelled.cancelled, true);
    assert.deepEqual(fake.calls.map((c) => c.path), ['exceptions/c1/pending-resolution']);

    const sod = api({ 'pending-resolution': () => ({ ok: false, status: 409, json: { error: { code: 'EXPIRY_SOD_VIOLATION' } } }) });
    const refused = await decision.decide({ ...sod, sign: async () => ({ signatureId: 'x' }) }, input);
    assert.equal(refused.ok, false);
    assert.equal(refused.step, 'prepare');
    assert.equal(refused.reply.json.error.code, 'EXPIRY_SOD_VIOLATION');
});

test('step 1 needs a resolution and a reason of at least 10 characters, and calls nothing otherwise', async () => {
    const fake = api({});
    const short = await decision.prepare(fake, { ...input, reason: 'kısa' });
    assert.equal(short.ok, false);
    assert.equal(short.errors.reason, 'Expiry.Drawer.ReasonMin');
    const none = await decision.prepare(fake, { ...input, resolution: '' });
    assert.equal(none.errors.resolution, 'Expiry.Drawer.ResolutionRequired');
    assert.equal(fake.calls.length, 0);
    assert.deepEqual(JSON.parse(JSON.stringify(decision.offers('Open'))), { take: true, decide: true, close: false });
    assert.deepEqual(JSON.parse(JSON.stringify(decision.offers('Accepted'))), { take: false, decide: false, close: true });
});

const refusal = (details) => ({ error: { code: 'LOT_BELOW_MIN_REMAINING_SHELF_LIFE', details } });

test('K3 sabotage (c): CT-F1 — a 422 with requiredToStockStatus = QUARANTINE is resent INTO QUARANTINE (never AVAILABLE again)', () => {
    const retry = bands.receiptRetry(422, refusal({ requiredToStockStatus: 'QUARANTINE', remainingDays: 40, requiredDays: 180 }), 'AVAILABLE');
    assert.equal(retry.resubmit, true);
    assert.equal(retry.toStockStatus, 'QUARANTINE');
});

test('K3 sabotage (d): CT-F1 — "Reddet" (no requiredToStockStatus) is NOT resent; nor is a receipt already into QUARANTINE, nor another code', () => {
    const reject = bands.receiptRetry(422, refusal({ remainingDays: 40, requiredDays: 180 }), 'AVAILABLE');
    assert.equal(reject.resubmit, false);
    assert.equal(reject.refused, true);
    assert.equal(bands.receiptRetry(422, refusal({ requiredToStockStatus: 'QUARANTINE' }), 'QUARANTINE').resubmit, false);
    assert.equal(bands.receiptRetry(422, { error: { code: 'INSUFFICIENT_STOCK' } }, 'AVAILABLE').resubmit, false);
    assert.equal(bands.receiptRetry(400, refusal({ requiredToStockStatus: 'QUARANTINE' }), 'AVAILABLE').resubmit, false);
});

test('outbound band: first lot / equal expiry = ok; a later lot by the company mode; the server\'s 422s', () => {
    const order = { eligible: [{ lotId: 'A', lotNumber: 'A-1', expiryDate: '2027-01-01' }, { lotId: 'B', expiryDate: '2027-01-01' }, { lotId: 'C', expiryDate: '2027-06-01' }] };
    assert.equal(bands.outbound(order, 'A', 'RequireReason').state, 'ok');
    assert.equal(bands.outbound(order, 'B', 'RequireReason').state, 'ok');
    assert.equal(bands.outbound(order, 'C', 'RequireReason').state, 'reason');
    assert.equal(bands.outbound(order, 'C', 'Warn').state, 'warn');
    assert.equal(bands.outbound(order, 'C', 'Block').state, 'block');
    assert.equal(bands.outbound(order, 'C', undefined).state, 'reason');
    assert.equal(bands.outbound(null, 'C', 'Block').state, 'none');
    assert.equal(bands.outboundRefusal(422, { error: { code: 'FEFO_ORDER_VIOLATION', details: {} } }).state, 'block');
    assert.equal(bands.outboundRefusal(422, { error: { code: 'FEFO_BYPASS_REASON_REQUIRED', details: {} } }).state, 'reason');
    assert.equal(bands.outboundRefusal(422, { error: { code: 'LOT_EXPIRED' } }), null);
});

test('receipt band and the date suggestion (KF-C, KF-D)', () => {
    assert.deepEqual(JSON.parse(JSON.stringify(bands.receipt({ expiryDate: '2027-11-26', postingDate: '2026-10-12T10:00:00Z', minRemaining: 180, mode: 'AutoQuarantine' }))),
        { state: 'ok', remaining: 410, min: 180 });
    assert.equal(bands.receipt({ expiryDate: '2027-01-15', postingDate: '2026-10-12', minRemaining: 180, mode: 'AutoQuarantine' }).state, 'quar');
    assert.equal(bands.receipt({ expiryDate: '2027-01-15', postingDate: '2026-10-12', minRemaining: 180, mode: 'Reject' }).state, 'reject');
    assert.equal(bands.receipt({ expiryDate: '2027-01-15', postingDate: '2026-10-12', minRemaining: null, mode: 'Reject' }).state, 'undefined');
    const s = bands.suggestion('2026-10-01', 730, 365);
    assert.deepEqual(JSON.parse(JSON.stringify(s)), { expiry: '2028-09-30', retest: '2027-10-01' });
    assert.deepEqual(JSON.parse(JSON.stringify(bands.differs(s, { expiry: '2028-09-30', retest: '2027-10-01' }))), { expiry: false, retest: false });
    assert.deepEqual(JSON.parse(JSON.stringify(bands.differs(s, { expiry: '2028-12-31', retest: '' }))), { expiry: true, retest: true });
    assert.deepEqual(JSON.parse(JSON.stringify(bands.suggestion('', 730, 365))), { expiry: null, retest: null });
});
