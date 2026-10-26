/**
 * MOD-0177 R7b — Ayarlar, the PURE part (no DOM; the Node test runs it against THIS file). settings.js draws, this file decides:
 *   • the form ↔ RecallSettings (drill target hours, frequency warning days, the class effectiveness targets §4.14); the trace limits are not on
 *     the screen and travel back UNCHANGED (updateSettings is a full replacement);
 *   • a reason of at least 10 characters (CT-R16) — no reason, nothing is sent;
 *   • "Kaydet" in two steps: prepareSettingsChange (rs-v1) → the ONE MOD-0022 dialog when the company's policy row recall.settings.update asks
 *     (fail-closed: anything but an explicit signatureRequired false) → updateSettings with the same proposal (+ signatureId), If-Match = the
 *     settings version. The e-signature policy itself is NOT a setting here (D-10: MOD-0022's policy rows).
 */
'use strict';

var RecallSettingsForm = (function () {
    const MIN = 10;
    const CLASSES = ['1', '2', '3'];
    const etag = (version) => `W/"${version}"`;
    const text = (value) => String(value ?? '').trim();
    const int = (value) => (text(value) === '' ? NaN : Number(value));

    /** RecallSettings → the form's values. */
    const read = (settings) => ({
        drillTargetHours: settings?.drillTargetHours ?? '',
        drillFrequencyWarningDays: settings?.drillFrequencyWarningDays ?? '',
        e1: settings?.effectivenessTargetPercent?.['1'] ?? '',
        e2: settings?.effectivenessTargetPercent?.['2'] ?? '',
        e3: settings?.effectivenessTargetPercent?.['3'] ?? '',
        reason: ''
    });

    /** Field → error key (the same ranges as the contract: 1–72 h, 30–730 days, 0–100 %; reason ≥ 10). */
    const validate = (form) => {
        const errors = {};
        const hours = int(form.drillTargetHours);
        if (!Number.isInteger(hours) || hours < 1 || hours > 72) errors.drillTargetHours = 'Recall.Settings.RangeHours';
        const days = int(form.drillFrequencyWarningDays);
        if (!Number.isInteger(days) || days < 30 || days > 730) errors.drillFrequencyWarningDays = 'Recall.Settings.RangeDays';
        ['e1', 'e2', 'e3'].forEach((k) => {
            const p = int(form[k]);
            if (!Number.isFinite(p) || p < 0 || p > 100) errors[k] = 'Recall.Settings.RangePercent';
        });
        if (text(form.reason).length < MIN) errors.reason = 'Recall.Common.ReasonMin';
        return errors;
    };

    /** The proposal (prepare and save send the SAME one — the server fingerprints it). */
    const proposal = (settings, form) => ({
        drillTargetHours: Number(form.drillTargetHours),
        drillFrequencyWarningDays: Number(form.drillFrequencyWarningDays),
        effectivenessTargetPercent: { 1: Number(form.e1), 2: Number(form.e2), 3: Number(form.e3) },
        traceMaxDepth: settings.traceMaxDepth,
        traceMaxNodes: settings.traceMaxNodes,
        reason: text(form.reason)
    });

    /** The fields the form changes (empty → "Kaydet" has nothing to save). */
    const changed = (settings, form) => {
        const out = [];
        if (Number(form.drillTargetHours) !== Number(settings.drillTargetHours)) out.push('drillTargetHours');
        if (Number(form.drillFrequencyWarningDays) !== Number(settings.drillFrequencyWarningDays)) out.push('drillFrequencyWarningDays');
        CLASSES.forEach((c) => { if (Number(form['e' + c]) !== Number(settings.effectivenessTargetPercent?.[c])) out.push('effectivenessTargetPercent.' + c); });
        return out;
    };

    const needsSignature = (prepared) => prepared?.signatureRequired !== false;

    /** deps: { write(path, body, { ifMatch }) → { ok, status, json }, newKey(), sign(subject) }. Step 1 — validate + prepare. */
    const prepare = async (deps, settings, form) => {
        const errors = validate(form);
        if (Object.keys(errors).length > 0) return { ok: false, step: 'validate', errors };
        if (changed(settings, form).length === 0) return { ok: false, step: 'validate', errors: { form: 'Recall.Errors.RECALL_NO_CHANGES' } };
        const reply = await deps.write('/Recall/cmd/settings/prepare', { idempotencyKey: deps.newKey(), ...proposal(settings, form) }, { ifMatch: etag(settings.version) });
        return reply.ok ? { ok: true, step: 'prepare', prepared: reply.json, signatureRequired: needsSignature(reply.json) } : { ok: false, step: 'prepare', reply };
    };

    /** Step 2 — ONLY on a prepared change: the MOD-0022 dialog when asked, then updateSettings with the same proposal. */
    const save = async (deps, settings, prepared, form) => {
        if (!prepared) return { ok: false, step: 'prepare', missing: true };
        let signatureId;
        if (needsSignature(prepared)) {
            const subject = prepared.subject || {};
            const signature = await deps.sign({
                module: subject.module || 'recall', subjectType: subject.subjectType || 'recall.settings-change', subjectId: subject.subjectId,
                actionCode: prepared.actionCode || 'recall.settings.update', meaningCode: prepared.meaningCode
            });
            if (!signature?.signatureId) return { ok: false, step: 'sign', cancelled: true };
            signatureId = signature.signatureId;
        }

        const body = { idempotencyKey: deps.newKey(), ...proposal(settings, form) };
        if (signatureId) body.signatureId = signatureId;
        const reply = await deps.write('/Recall/cmd/settings', body, { ifMatch: etag(settings.version) });
        return reply.ok ? { ok: true, step: 'commit', settings: reply.json, signatureId } : { ok: false, step: 'commit', reply };
    };

    return { MIN, read, validate, proposal, changed, needsSignature, prepare, save, etag };
})();

if (typeof window !== 'undefined') window.RecallSettingsForm = RecallSettingsForm;
