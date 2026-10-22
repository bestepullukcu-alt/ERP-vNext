/**
 * MOD-0176 F5 — the decision of an exception case (pack §8.4, §8.6, KF-E; mockup "İstisna kararı" drawer). NO DOM here — the drawer
 * (exception-detail.js) draws, this file decides the calls, so the order of the calls is measured by a Node test against THIS file:
 *   step 1  prepareResolution (resolution + reason ≥ 10, If-Match = the case version) — the server names the MOD-0022 action and says
 *           whether the company's e-sign policy asks for a signature;
 *   step 2  when it asks (anything but an explicit `signatureRequired: false` — fail-closed): the ONE MOD-0022 signature dialog
 *           (window.ESign.open, never a second dialog) signs the prepared decision; a cancelled dialog sends nothing;
 *   then    resolveException with the signature id (If-Match = the prepared version);
 *   later   closeException ("İstisnayı kapat", no signature) for Accepted / Fixed.
 * The SoD refusal (409 EXPIRY_SOD_VIOLATION, CT-F10) and the signature refusals come back as the server's reply; the drawer words them.
 */
'use strict';

var ExpiryDecision = (function () {
    const MIN_REASON = 10;
    const RESOLUTIONS = ['AcceptedWithReason', 'Fixed', 'Cancelled'];
    const etag = (version) => `W/"${version}"`;
    const casePath = (id, verb) => `exceptions/${encodeURIComponent(id)}/${verb}`;

    /** Step 1 of the drawer: a resolution and a reason of at least 10 characters (the error keys are the drawer's). */
    const validate = (resolution, reason) => {
        const errors = {};
        if (!RESOLUTIONS.includes(resolution)) errors.resolution = 'Expiry.Drawer.ResolutionRequired';
        if (String(reason || '').trim().length < MIN_REASON) errors.reason = 'Expiry.Drawer.ReasonMin';
        return errors;
    };

    /** KF-E: the server's prepareResolution decides; only an explicit false skips the signature step (K3 sabotage (e)). */
    const needsSignature = (prepared) => prepared?.signatureRequired !== false;

    /** deps: { write(method, path, body, { idempotencyKey, ifMatch }) → { ok, status, json }, newKey() }. */
    const prepare = async (deps, { exceptionId, version, resolution, reason }) => {
        const errors = validate(resolution, reason);
        if (Object.keys(errors).length > 0) return { ok: false, step: 'validate', errors };
        const key = deps.newKey();
        const reply = await deps.write('POST', casePath(exceptionId, 'pending-resolution'),
            { idempotencyKey: key, resolution, reason: String(reason).trim() }, { idempotencyKey: key, ifMatch: etag(version) });
        return reply.ok ? { ok: true, step: 'prepare', prepared: reply.json, signatureRequired: needsSignature(reply.json) } : { ok: false, step: 'prepare', reply };
    };

    /**
     * Step 2 + resolve. deps also: sign({ module, subjectType, subjectId, actionCode }) → { signatureId } | null (the MOD-0022 dialog).
     * Returns { ok, step, detail?, signatureId?, cancelled?, reply? }.
     */
    const signAndResolve = async (deps, prepared, { exceptionId, resolution, reason }) => {
        let signatureId;
        if (needsSignature(prepared)) {
            const subject = prepared.subject || {};
            const signature = await deps.sign({
                module: subject.module || 'expiry',
                subjectType: subject.subjectType || 'expiry.exception',
                subjectId: subject.subjectId || exceptionId,
                actionCode: prepared.actionCode
            });
            if (!signature?.signatureId) return { ok: false, step: 'sign', cancelled: true };
            signatureId = signature.signatureId;
        }

        const key = deps.newKey();
        const body = { idempotencyKey: key, resolution, reason: String(reason).trim() };
        if (signatureId) body.signatureId = signatureId;
        const reply = await deps.write('POST', casePath(exceptionId, 'resolve'), body, { idempotencyKey: key, ifMatch: etag(prepared.version) });
        return reply.ok ? { ok: true, step: 'resolve', detail: reply.json, signatureId } : { ok: false, step: 'resolve', reply };
    };

    /** The whole decision in one call (prepare → sign when asked → resolve) — what the drawer does across its two steps. */
    const decide = async (deps, input) => {
        const first = await prepare(deps, input);
        if (!first.ok) return first;
        return signAndResolve(deps, first.prepared, input);
    };

    /** "İstisnayı kapat" — Accepted / Fixed only (the server refuses the rest with 409 EXPIRY_INVALID_TRANSITION). */
    const close = async (deps, { exceptionId, version, note }) => {
        const key = deps.newKey();
        const body = { idempotencyKey: key };
        if (note) body.note = note;
        const reply = await deps.write('POST', casePath(exceptionId, 'close'), body, { idempotencyKey: key, ifMatch: etag(version) });
        return reply.ok ? { ok: true, detail: reply.json } : { ok: false, reply };
    };

    /** "İncelemeye al" (take) and "Yorum ekle" (comment) — the other two writes of the detail page. */
    const take = async (deps, { exceptionId, version }) => {
        const key = deps.newKey();
        const reply = await deps.write('POST', casePath(exceptionId, 'take'), { idempotencyKey: key }, { idempotencyKey: key, ifMatch: etag(version) });
        return reply.ok ? { ok: true, detail: reply.json } : { ok: false, reply };
    };

    const comment = async (deps, { exceptionId, text }) => {
        const key = deps.newKey();
        const reply = await deps.write('POST', casePath(exceptionId, 'comments'), { idempotencyKey: key, text: String(text).trim() }, { idempotencyKey: key });
        return reply.ok ? { ok: true, event: reply.json } : { ok: false, reply };
    };

    /** What the detail page offers for a status (the keys decide whether a button is drawn at all — UAS-001). */
    const offers = (status) => ({
        take: status === 'Open',
        decide: status === 'Open' || status === 'InReview',
        close: status === 'Accepted' || status === 'Fixed'
    });

    return { MIN_REASON, RESOLUTIONS, validate, needsSignature, prepare, signAndResolve, decide, close, take, comment, offers };
})();

if (typeof window !== 'undefined') window.ExpiryDecision = ExpiryDecision;
