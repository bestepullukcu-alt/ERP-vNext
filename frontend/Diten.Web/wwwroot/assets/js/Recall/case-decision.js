/**
 * MOD-0177 R7a — the three signed decisions of the case (pack §8.8; the MOD-0176 F5 expiry-decision.js pattern). NO DOM here — the dialog
 * (case-detail.js) draws, this file decides the calls, so their ORDER is measured by a Node test against THIS file:
 *   step 1  prepare — the decision's own door (prepare-approval / prepare-completion / prepare-closure; each door asks for ITS key and puts
 *           its own action in), If-Match = the case version; the server stores the PendingAction (15 min) and says whether the company's
 *           MOD-0022 policy asks for a signature;
 *   step 2  when it asks (anything but an explicit `signatureRequired: false` — fail-closed): the ONE MOD-0022 dialog (window.ESign.open)
 *           signs the prepared subject; a cancelled dialog sends nothing;
 *   then    the command (approve / effectiveness/complete / close) with the same fields and the signatureId, If-Match = the PREPARED version.
 * There is no path to a command without a prepared decision. The refusals (RECALL_SOD_VIOLATION, RECALL_SIGNATURE_*, RECALL_SCOPE_CHANGED,
 * RECALL_PENDING_ACTION_MISMATCH …) come back as the server's reply; the dialog words them.
 */
'use strict';

var RecallCaseDecision = (function () {
    const MIN = 10;
    const KINDS = {
        approve: { prepare: 'prepare-approval', commit: 'approve', actionCode: 'recall.scope.approve' },
        complete: { prepare: 'prepare-completion', commit: 'effectiveness/complete', actionCode: 'recall.effectiveness.complete' },
        close: { prepare: 'prepare-closure', commit: 'close', actionCode: 'recall.case.close' }
    };
    const etag = (version) => `W/"${version}"`;
    const door = (caseId, verb) => `/Recall/cmd/cases/${encodeURIComponent(caseId)}/${verb}`;
    const text = (value) => String(value ?? '').trim();

    /** Step 1 fields: approve — note (optional); complete — note ≥ 10; close — summary ≥ 10 (+ acceptance ≥ 10 while an open quantity remains). */
    const validate = (kind, input) => {
        const errors = {};
        if (!KINDS[kind]) errors.kind = 'Recall.Errors.GENERIC';
        if (kind === 'complete' && text(input.note).length < MIN) errors.note = 'Recall.Common.ReasonMin';
        if (kind === 'close' && text(input.summary).length < MIN) errors.summary = 'Recall.Common.ReasonMin';
        if (kind === 'close' && input.openRemains && text(input.acceptance).length < MIN) errors.acceptance = 'Recall.Common.ReasonMin';
        return errors;
    };

    /** The decision's fields — the SAME in the prepare and in the command (the server compares them). */
    const fields = (kind, input) => {
        if (kind === 'approve') return text(input.note) ? { note: text(input.note) } : {};
        if (kind === 'complete') return { note: text(input.note) };
        const body = { summary: text(input.summary) };
        if (input.openRemains) body.openQuantityAcceptance = text(input.acceptance);
        return body;
    };

    const needsSignature = (prepared) => prepared?.signatureRequired !== false;

    /** deps: { write(path, body, { ifMatch }) → { ok, status, json }, newKey(), sign({ module, subjectType, subjectId, actionCode, meaningCode }) }. */
    const prepare = async (deps, kind, input) => {
        const errors = validate(kind, input);
        if (Object.keys(errors).length > 0) return { ok: false, step: 'validate', errors };
        const key = deps.newKey();
        const reply = await deps.write(door(input.caseId, KINDS[kind].prepare), { idempotencyKey: key, ...fields(kind, input) }, { ifMatch: etag(input.version) });
        return reply.ok
            ? { ok: true, step: 'prepare', prepared: reply.json, signatureRequired: needsSignature(reply.json) }
            : { ok: false, step: 'prepare', reply };
    };

    /** Step 2 + the command, ONLY on a prepared decision (its version is the If-Match). */
    const signAndCommit = async (deps, kind, prepared, input) => {
        if (!prepared || prepared.version === undefined || prepared.version === null) return { ok: false, step: 'prepare', missing: true };
        let signatureId;
        if (needsSignature(prepared)) {
            const subject = prepared.subject || {};
            const signature = await deps.sign({
                module: subject.module || 'recall',
                subjectType: subject.subjectType || 'recall.case',
                subjectId: subject.subjectId || input.caseId,
                actionCode: prepared.actionCode || KINDS[kind].actionCode,
                meaningCode: prepared.meaningCode
            });
            if (!signature?.signatureId) return { ok: false, step: 'sign', cancelled: true };
            signatureId = signature.signatureId;
        }

        const key = deps.newKey();
        const body = { idempotencyKey: key, ...fields(kind, input) };
        if (signatureId) body.signatureId = signatureId;
        const reply = await deps.write(door(input.caseId, KINDS[kind].commit), body, { ifMatch: etag(prepared.version) });
        return reply.ok ? { ok: true, step: 'commit', detail: reply.json, signatureId } : { ok: false, step: 'commit', reply };
    };

    /** The whole decision in one call (prepare → sign when asked → command) — what the dialog does across its two steps. */
    const decide = async (deps, kind, input) => {
        const first = await prepare(deps, kind, input);
        if (!first.ok) return first;
        return signAndCommit(deps, kind, first.prepared, input);
    };

    /** The unsigned case commands (each through its own door; If-Match where the record is versioned). */
    const command = async (deps, caseId, verb, body, version) => {
        const key = deps.newKey();
        const options = version === undefined || version === null ? {} : { ifMatch: etag(version) };
        const reply = await deps.write(door(caseId, verb), { idempotencyKey: key, ...body }, options);
        return reply.ok ? { ok: true, detail: reply.json } : { ok: false, reply };
    };

    return { MIN, KINDS, validate, fields, needsSignature, prepare, signAndCommit, decide, command, door, etag };
})();

if (typeof window !== 'undefined') window.RecallCaseDecision = RecallCaseDecision;
