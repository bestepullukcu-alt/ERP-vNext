/**
 * MOD-0177 R7b — Tatbikat Planlayıcı, the PURE part (no DOM; the Node test runs it against THIS file). drills.js draws, this file decides:
 *   • the clock — from the SERVER only: serverNowUtc (the service's answer time, the list / run door adds it) − startedAtUtc, advanced by the
 *     monotonic time since that answer; the browser's wall clock (Date.now) is never read. A stopped run (all three steps done) shows the
 *     server's durationMinutes. No "+30 dk" (D-14: demo control, not built).
 *   • the steps — Trace → Scope → CustomerList; the button always sends the run's own nextStep (the server's order, RECALL_DRILL_STEP_ORDER);
 *   • which button shows — by action code + run status (UAS-001: absent, not disabled);
 *   • the calls — each through its own door (/Recall/cmd/drill-…): start (Sistem seçsin = Auto: the SERVER picks the lot; Ben seçeyim =
 *     Manual + lotId), step, cancel (reason ≥ 10; the run is KEPT as Cancelled — CT-R17, D-13), the result in two steps (prepare → the ONE
 *     MOD-0022 dialog when the policy asks → result with the signatureId; fail-closed like RecallCaseDecision), the plan form.
 */
'use strict';

var RecallDrillView = (function () {
    const MIN = 10;
    const STEPS = ['Trace', 'Scope', 'CustomerList'];
    const FREQUENCIES = ['Yearly1', 'Yearly2', 'Quarterly', 'CustomDays'];
    const SCOPES = ['RandomFinishedGoodLot', 'RawMaterialLot', 'SelectedLot'];
    const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    const DAY = /^\d{4}-\d{2}-\d{2}$/;
    const etag = (version) => `W/"${version}"`;
    const text = (value) => String(value ?? '').trim();
    const pad = (n) => String(n).padStart(2, '0');

    /**
     * The running panel's clock. serverNowUtc = the service's time when it answered; sinceAnswerMs = monotonic milliseconds since that answer
     * (performance.now() difference in the page). Returns null when the server gave no time (the page then shows "—", never a guess).
     */
    const clock = (run, serverNowUtc, sinceAnswerMs) => {
        if (!run) return null;
        const targetMinutes = Math.max(0, Number(run.targetHours) || 0) * 60;
        let minutes;
        const stopped = run.durationMinutes !== null && run.durationMinutes !== undefined;
        if (stopped) {
            minutes = Math.max(0, Math.round(Number(run.durationMinutes) || 0));
        } else {
            const now = Date.parse(serverNowUtc || '');
            const started = Date.parse(run.startedAtUtc || '');
            if (Number.isNaN(now) || Number.isNaN(started)) return null;
            minutes = Math.max(0, Math.floor((now + Math.max(0, Number(sinceAnswerMs) || 0) - started) / 60000));
        }

        return {
            minutes, text: `${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`, stopped, targetHours: Number(run.targetHours) || 0,
            over: targetMinutes > 0 && minutes > targetMinutes, remaining: Math.max(0, targetMinutes - minutes)
        };
    };

    /** The three steps in the server's order: done (with who / when), current (= the run's nextStep), waiting. */
    const steps = (run) => STEPS.map((step, i) => {
        const done = (run?.steps || []).find((s) => s.step === step) || null;
        return { step, n: i + 1, done: !!done, at: done?.completedAtUtc || null, by: done?.completedBy || null, current: run?.status === 'Running' && run?.nextStep === step };
    });

    /** All three steps done, the timer stopped, the result not yet signed. */
    const finished = (run) => run?.status === 'Running' && !run.nextStep && (run.steps || []).length >= STEPS.length;

    /** run (or null) + can(code) → which buttons show. A plan row's "Tatbikatı başlat" also needs no other run running (one per company). */
    const offers = (run, can, anyRunning) => ({
        planSave: can('RECALL_DRILL_PLAN_SAVE'),
        start: can('RECALL_DRILL_RUN') && !anyRunning,
        step: !!run && run.status === 'Running' && !!run.nextStep && can('RECALL_DRILL_RUN'),
        cancel: !!run && run.status === 'Running' && can('RECALL_DRILL_RUN'),
        sign: finished(run) && can('RECALL_DRILL_SIGN')
    });

    const validatePlan = (input) => {
        const errors = {};
        const name = text(input.name);
        if (name.length < 1 || name.length > 200) errors.name = 'Recall.Common.Required';
        if (!FREQUENCIES.includes(input.frequency)) errors.frequency = 'Recall.Common.Required';
        if (input.frequency === 'CustomDays') {
            const days = Number(input.customDays);
            if (!Number.isInteger(days) || days < 1 || days > 730) errors.customDays = 'Recall.Drills.CustomDaysRange';
        }
        if (!SCOPES.includes(input.scopeKind)) errors.scopeKind = 'Recall.Common.Required';
        if (!DAY.test(text(input.nextDueOn))) errors.nextDueOn = 'Recall.Common.Required';
        if (!GUID.test(text(input.ownerUserId))) errors.owner = 'Recall.Common.Required';
        return errors;
    };

    const planBody = (input) => {
        const body = { name: text(input.name), frequency: input.frequency, scopeKind: input.scopeKind, nextDueOn: text(input.nextDueOn), ownerUserId: text(input.ownerUserId), active: input.active !== false };
        if (input.frequency === 'CustomDays') body.customDays = Number(input.customDays);
        return body;
    };

    /** deps: { write(path, body, { ifMatch }) → { ok, status, json }, newKey(), sign(subject) → { signatureId } | null }. */
    const savePlan = async (deps, plan, input) => {
        const errors = validatePlan(input);
        if (Object.keys(errors).length > 0) return { ok: false, step: 'validate', errors };
        const body = { idempotencyKey: deps.newKey(), ...planBody(input) };
        const reply = plan
            ? await deps.write(`/Recall/cmd/drill-plans/${encodeURIComponent(plan.planId)}`, body, { ifMatch: etag(plan.version) })
            : await deps.write('/Recall/cmd/drill-plans', body, {});
        return reply.ok ? { ok: true, plan: reply.json } : { ok: false, step: 'save', reply };
    };

    /** "Sayacı başlat": Auto sends no lot (the server picks); Manual needs the chosen lot. */
    const start = async (deps, planId, mode, lotId) => {
        if (mode !== 'Auto' && mode !== 'Manual') return { ok: false, step: 'validate', errors: { mode: 'Recall.Common.Required' } };
        if (mode === 'Manual' && !GUID.test(text(lotId))) return { ok: false, step: 'validate', errors: { lot: 'Recall.Drills.LotRequired' } };
        const body = { idempotencyKey: deps.newKey(), planId, selectionMode: mode };
        if (mode === 'Manual') body.lotId = text(lotId);
        const reply = await deps.write('/Recall/cmd/drill-runs', body, {});
        return reply.ok ? { ok: true, run: reply.json } : { ok: false, step: 'start', reply };
    };

    /** The next step — ALWAYS the run's own nextStep (never a step the page chose), If-Match = the run version. */
    const step = async (deps, run) => {
        if (!run?.nextStep) return { ok: false, step: 'validate', errors: { step: 'Recall.Errors.RECALL_INVALID_TRANSITION' } };
        const reply = await deps.write(`/Recall/cmd/drill-runs/${encodeURIComponent(run.runId)}/steps/${encodeURIComponent(run.nextStep)}`,
            { idempotencyKey: deps.newKey() }, { ifMatch: etag(run.version) });
        return reply.ok ? { ok: true, run: reply.json } : { ok: false, step: 'step', reply };
    };

    /** "Tatbikatı iptal et" — a reason of at least 10 characters; the server keeps the run as Cancelled. */
    const cancel = async (deps, run, reason) => {
        if (text(reason).length < MIN) return { ok: false, step: 'validate', errors: { reason: 'Recall.Common.ReasonMin' } };
        const reply = await deps.write(`/Recall/cmd/drill-runs/${encodeURIComponent(run.runId)}/cancel`,
            { idempotencyKey: deps.newKey(), reason: text(reason) }, { ifMatch: etag(run.version) });
        return reply.ok ? { ok: true, run: reply.json } : { ok: false, step: 'cancel', reply };
    };

    const needsSignature = (prepared) => prepared?.signatureRequired !== false;

    /** "Sonucu imzala" step 1 — the result note (≥ 10) is prepared; the server says whether MOD-0022 asks for a signature. */
    const prepareResult = async (deps, run, note) => {
        if (text(note).length < MIN) return { ok: false, step: 'validate', errors: { note: 'Recall.Common.ReasonMin' } };
        const reply = await deps.write(`/Recall/cmd/drill-runs/${encodeURIComponent(run.runId)}/prepare-result`,
            { idempotencyKey: deps.newKey(), resultNote: text(note) }, { ifMatch: etag(run.version) });
        return reply.ok ? { ok: true, step: 'prepare', prepared: reply.json, signatureRequired: needsSignature(reply.json) } : { ok: false, step: 'prepare', reply };
    };

    /** Step 2 — ONLY on a prepared result: the MOD-0022 dialog when asked (cancelled → nothing is sent), then the result command. */
    const signResult = async (deps, run, prepared, note) => {
        if (!prepared || prepared.version === undefined || prepared.version === null) return { ok: false, step: 'prepare', missing: true };
        let signatureId;
        if (needsSignature(prepared)) {
            const subject = prepared.subject || {};
            const signature = await deps.sign({
                module: subject.module || 'recall', subjectType: subject.subjectType || 'recall.drill-run', subjectId: subject.subjectId || run.runId,
                actionCode: prepared.actionCode || 'recall.drill.result', meaningCode: prepared.meaningCode
            });
            if (!signature?.signatureId) return { ok: false, step: 'sign', cancelled: true };
            signatureId = signature.signatureId;
        }

        const body = { idempotencyKey: deps.newKey(), resultNote: text(note) };
        if (signatureId) body.signatureId = signatureId;
        const reply = await deps.write(`/Recall/cmd/drill-runs/${encodeURIComponent(run.runId)}/result`, body, { ifMatch: etag(prepared.version) });
        return reply.ok ? { ok: true, step: 'commit', run: reply.json, signatureId } : { ok: false, step: 'commit', reply };
    };

    return { MIN, STEPS, FREQUENCIES, SCOPES, clock, steps, finished, offers, validatePlan, planBody, savePlan, start, step, cancel, needsSignature, prepareResult, signResult, etag };
})();

if (typeof window !== 'undefined') window.RecallDrillView = RecallDrillView;
