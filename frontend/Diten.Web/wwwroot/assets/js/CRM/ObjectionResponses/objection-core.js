/**
 * WP-VW-W3 (W3-WEB-c) — the objection library's and the step presentation notes' PURE rules (no DOM, no fetch, no
 * clock). Browser: window.ObjectionCore. Node: module.exports (the Web tests).
 *
 * Library (/CRM/ObjectionResponses):
 *   actions(dto)                         the buttons a record offers: CRM's abilities AND the status that allows them
 *   decisionBody(decision, comment)      { ok, body } | { ok:false, error }; "return" needs a note (K-A1)
 *   categoryTone(code)                   a Bootstrap label tone per category CODE (labels come from BRD, colours from here)
 *   claimOptions(claims, productId)      the claim picker: approved, live, of the selected product only
 *   toggleClaim(selected, id)            add / remove; never more than MAX_CLAIMS
 *   editBody(form, version)              the POST / PUT body (a half-written draft is valid, K-A3)
 *   counter(text, max)                   "n / max" + over
 *   pendingLine(dto, names)              "Onay bekliyor · who, when" facts of the open round
 *   timeline(dto, names)                 the history, newest first (created, submitted, decided, retired)
 *   returnNote(dto)                      the last "returned to draft" decision (its note), if the draft came back
 *   personName(id, names, unknown)       a user id → its display name
 *
 * Step presentation notes (Knowledge Path Studio, "Sunum notları" tab):
 *   presentationBody(fields)             ALWAYS the complete object (every key; an object replaces the stored notes)
 *   validatePresentation(fields)         the CRM limits (key ≤ 500, talk ≤ 4000, target 10–3600, transition ≤ 500, ≤ 10)
 *   notesFilled(presentation)            key message + talk track written
 *   completeness(items)                  { filled, total, missing[] } over the path's content steps
 *   targetHint(targetSeconds, minutes)   the effective target: the step's own, else the estimate (MINUTES × 60)
 *   objectionOptions(rows, productId)    approved answers of the path's product (K-A4)
 *   splitLinked(ids, rows, productId)    linked ids → usable / unusable (an unusable one is dropped on save)
 *   toggleObjection(selected, id)        add / remove; never more than MAX_OBJECTIONS
 *
 *   errorText(errors, L) · fmt(template, …)
 */
(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.ObjectionCore = api; }
})(typeof window !== 'undefined' ? window : null, function () {
    'use strict';

    const MAX_CLAIMS = 10;
    const MAX_QUESTION = 500;
    const MAX_ANSWER = 4000;
    const MAX_OBJECTIONS = 10;
    const MAX_KEY_MESSAGE = 500;
    const MAX_TALK_TRACK = 4000;
    const MAX_TRANSITION = 500;
    const MIN_TARGET = 10;
    const MAX_TARGET = 3600;

    const STATUSES = ['draft', 'in_review', 'approved', 'retired'];
    const ACTIONS = ['edit', 'submit', 'withdraw', 'approve', 'return', 'retire', 'copy'];

    // ---------------- library ----------------

    // An ability alone never draws a button: the status must allow the action too (a stale read cannot offer a
    // withdraw on an approved record). The approve / return pair is the reviewer's, on the record page only — never in
    // the editor (K-A2).
    const ALLOWED = {
        edit: ['draft'],
        submit: ['draft'],
        withdraw: ['in_review'],
        approve: ['in_review'],
        return: ['in_review'],
        retire: ['draft', 'approved'],
        copy: ['draft', 'approved', 'retired']
    };
    const ABILITY = {
        edit: 'canEdit', submit: 'canSubmit', withdraw: 'canWithdraw', approve: 'canDecide', return: 'canDecide',
        retire: 'canRetire', copy: 'canCopy'
    };

    const actions = dto => {
        const status = dto && dto.status;
        const abilities = (dto && dto.abilities) || {};
        return ACTIONS.filter(a => abilities[ABILITY[a]] === true && ALLOWED[a].indexOf(status) > -1);
    };

    const trimOrNull = v => {
        const s = v == null ? '' : String(v).trim();
        return s.length ? s : null;
    };

    const decisionBody = (decision, comment) => {
        if (decision !== 'approve' && decision !== 'return') return { ok: false, error: 'decision_invalid' };
        const note = trimOrNull(comment);
        if (decision === 'return' && !note) return { ok: false, error: 'comment_required' };
        return { ok: true, body: { decision, comment: note } };
    };

    const TONES = {
        price: 'warning',
        reimbursement: 'info',
        efficacy: 'success',
        side_effect: 'danger',
        dosage: 'primary',
        access_stock: 'secondary',
        habit_competitor: 'primary',
        insufficient_evidence: 'dark',
        other: 'secondary'
    };
    const categoryTone = code => TONES[code] || 'secondary';

    const sameId = (a, b) => a != null && b != null && String(a).toLowerCase() === String(b).toLowerCase();

    const claimOptions = (claims, productId) => (claims || []).filter(c => c
        && String(c.status || '').toLowerCase() === 'approved'
        && c.isArchived !== true
        && productId && sameId(c.productId, productId));

    const toggle = (selected, id, max) => {
        const list = (selected || []).slice();
        const at = list.findIndex(x => sameId(x, id));
        if (at > -1) { list.splice(at, 1); return { selected: list, refused: false }; }
        if (list.length >= max) return { selected: list, refused: true };
        list.push(id);
        return { selected: list, refused: false };
    };
    const toggleClaim = (selected, id) => toggle(selected, id, MAX_CLAIMS);

    const editBody = (form, version) => {
        const f = form || {};
        const body = {
            productId: f.productId || null,
            categoryCode: trimOrNull(f.categoryCode),
            question: trimOrNull(f.question),
            answer: trimOrNull(f.answer),
            claimIds: (f.claimIds || []).slice(0, MAX_CLAIMS)
        };
        if (version != null) body.expectedVersion = version;
        return body;
    };

    const counter = (text, max) => {
        const n = text == null ? 0 : String(text).length;
        return { text: `${n} / ${max}`, over: n > max };
    };

    const personName = (id, names, unknown) => {
        if (!id) return unknown || '';
        const map = names || {};
        const key = Object.keys(map).find(k => sameId(k, id));
        return key ? map[key] : (unknown || '');
    };

    const openRound = dto => {
        const rounds = (dto && dto.reviewRounds) || [];
        return rounds.filter(r => !r.closedAt).sort((a, b) => b.roundNo - a.roundNo)[0] || null;
    };

    const pendingLine = (dto, names, unknown) => {
        if (!dto || dto.status !== 'in_review') return null;
        const round = openRound(dto);
        const by = dto.submittedBy || (round && round.submittedBy);
        const at = dto.submittedAt || (round && round.submittedAt);
        return { by: personName(by, names, unknown), at: at || null };
    };

    const returnNote = dto => {
        if (!dto || dto.status !== 'draft') return null;
        const decisions = ((dto && dto.decisions) || []).slice().sort((a, b) => String(b.at).localeCompare(String(a.at)));
        const last = decisions[0];
        const rounds = (dto.reviewRounds || []).slice().sort((a, b) => b.roundNo - a.roundNo);
        const lastRound = rounds[0];
        if (lastRound && lastRound.reasonCode && (!last || last.roundNo !== lastRound.roundNo)) {
            return { kind: 'reason', reasonCode: lastRound.reasonCode, at: lastRound.closedAt || null };
        }
        if (last && String(last.outcome).toLowerCase() !== 'approve' && String(last.outcome).toLowerCase() !== 'approved') {
            return { kind: 'returned', comment: last.comment || null, by: last.by || null, at: last.at || null, reasonCode: lastRound && lastRound.roundNo === last.roundNo ? lastRound.reasonCode || null : null };
        }
        return null;
    };

    const OUTCOME = o => {
        const v = String(o || '').toLowerCase();
        if (v === 'approve' || v === 'approved') return 'approved';
        if (v === 'return' || v === 'returned' || v === 'reject' || v === 'rejected') return 'returned';
        if (v === 'withdrawn' || v === 'cancelled' || v === 'canceled') return 'withdrawn';
        if (v === 'timeout' || v === 'timed_out' || v === 'expired') return 'timeout';
        return v || 'returned';
    };

    const timeline = (dto, names, unknown) => {
        if (!dto) return [];
        const events = [];
        const name = id => personName(id, names, unknown);
        if (dto.createdAt) events.push({ kind: 'created', at: dto.createdAt, by: name(dto.createdBy) });
        (dto.reviewRounds || []).forEach(r => {
            events.push({ kind: 'submitted', at: r.submittedAt, by: name(r.submittedBy), round: r.roundNo });
            const decided = (dto.decisions || []).some(d => d.roundNo === r.roundNo);
            if (r.closedAt && !decided) {
                const kind = r.reasonCode ? 'returned' : OUTCOME(r.outcome);
                events.push({ kind, at: r.closedAt, by: name(r.completedBy), round: r.roundNo, reasonCode: r.reasonCode || null });
            }
        });
        (dto.decisions || []).forEach(d => {
            const round = (dto.reviewRounds || []).find(r => r.roundNo === d.roundNo);
            events.push({ kind: OUTCOME(d.outcome), at: d.at, by: name(d.by), round: d.roundNo, comment: d.comment || null, reasonCode: round && round.reasonCode || null });
        });
        if (dto.retiredAt) events.push({ kind: 'retired', at: dto.retiredAt, by: name(dto.retiredBy) });
        return events.filter(e => e.at).sort((a, b) => String(b.at).localeCompare(String(a.at)));
    };

    // ---------------- step presentation notes ----------------

    const intOrNull = v => {
        if (v == null || String(v).trim() === '') return null;
        const n = Number(v);
        return Number.isFinite(n) ? Math.trunc(n) : null;
    };

    /** The complete presentation object — every key, always. A partial object would wipe the omitted notes. */
    const presentationBody = fields => {
        const f = fields || {};
        return {
            keyMessage: trimOrNull(f.keyMessage),
            talkTrack: trimOrNull(f.talkTrack),
            targetSeconds: intOrNull(f.targetSeconds),
            transition: trimOrNull(f.transition),
            objectionResponseIds: Array.from(new Set((f.objectionResponseIds || []).map(String))).slice(0, MAX_OBJECTIONS)
        };
    };

    const length = v => (v == null ? 0 : String(v).trim().length);

    const validatePresentation = fields => {
        const f = fields || {};
        const errors = [];
        if (length(f.keyMessage) > MAX_KEY_MESSAGE) errors.push('keyMessage');
        if (length(f.talkTrack) > MAX_TALK_TRACK) errors.push('talkTrack');
        if (length(f.transition) > MAX_TRANSITION) errors.push('transition');
        const raw = f.targetSeconds;
        if (raw != null && String(raw).trim() !== '') {
            const n = Number(raw);
            if (!Number.isInteger(n) || n < MIN_TARGET || n > MAX_TARGET) errors.push('targetSeconds');
        }
        if ((f.objectionResponseIds || []).length > MAX_OBJECTIONS) errors.push('objectionResponseIds');
        return errors;
    };

    const notesFilled = p => !!(p && length(p.keyMessage) > 0 && length(p.talkTrack) > 0);

    const completeness = items => {
        const steps = (items || []).filter(i => i && i.kind === 'content');
        const missing = steps.filter(i => !notesFilled(i.presentation));
        return { filled: steps.length - missing.length, total: steps.length, missing };
    };

    const targetHint = (targetSeconds, estimatedMinutes) => {
        const own = intOrNull(targetSeconds);
        if (own != null && own > 0) return { seconds: own, source: 'target' };
        const minutes = intOrNull(estimatedMinutes);
        if (minutes != null && minutes > 0) return { seconds: minutes * 60, source: 'estimate' };
        return { seconds: null, source: null };
    };

    const usableObjection = (row, productId) => !!(row
        && String(row.status || '').toLowerCase() === 'approved'
        && productId && sameId(row.productId, productId));

    const objectionOptions = (rows, productId) => (rows || []).filter(r => usableObjection(r, productId));

    const splitLinked = (ids, rows, productId) => {
        const usable = [];
        const unusable = [];
        (ids || []).forEach(id => {
            const row = (rows || []).find(r => sameId(r.id, id));
            if (row && usableObjection(row, productId)) usable.push(String(id));
            else unusable.push({ id: String(id), row: row || null });
        });
        return { usable, unusable };
    };

    const toggleObjection = (selected, id) => toggle(selected, id, MAX_OBJECTIONS);

    // ---------------- shared ----------------

    const errorText = (errors, L) => {
        const labels = L || {};
        const list = Array.isArray(errors) ? errors : [];
        const code = list.find(e => typeof e === 'string' && /^[a-z0-9_]+$/.test(e) && labels['Err_' + e]);
        return code ? labels['Err_' + code] : (labels.ActionFailed || list[0] || '');
    };

    function fmt(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template == null ? '' : template).replace(/\{(\d+)\}/g, (m, i) => (args[i] == null ? '' : String(args[i])));
    }

    return {
        MAX_CLAIMS, MAX_QUESTION, MAX_ANSWER, MAX_OBJECTIONS, MAX_KEY_MESSAGE, MAX_TALK_TRACK, MAX_TRANSITION,
        MIN_TARGET, MAX_TARGET, STATUSES, ACTIONS,
        actions, decisionBody, categoryTone, claimOptions, toggleClaim, editBody, counter, personName,
        pendingLine, returnNote, timeline,
        presentationBody, validatePresentation, notesFilled, completeness, targetHint,
        objectionOptions, splitLinked, toggleObjection,
        errorText, fmt
    };
});
