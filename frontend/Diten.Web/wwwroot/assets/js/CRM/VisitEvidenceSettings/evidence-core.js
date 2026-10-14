/**
 * WP-VW-W3 (W3-WEB-d) — the visit evidence settings page's PURE rules: no DOM, no fetch. Loaded before
 * visit-evidence-settings.js; the Web tests run these functions in Node. Texts are KEYS (the page resolves them through
 * window.VisitEvidenceSettingsL10n), never words.
 *
 *   FIELDS / GROUPS / RANGES / OPTIONS   the tenant policy's eight fields (wire names), the mockup's three groups
 *   draftOf(policy) / changes(a, b)      the editable copy of a policy; the changed fields (save confirm rows)
 *   fieldErrors(draft)                   the number bounds (the short-visit threshold only when duration consistency is on)
 *   putBody(draft, version)              the PUT body — an out-of-range threshold is NOT sent while duration is off
 *   valueKey(field, value)               a value → its label key (required / info, optional, allow_flagged / block, on / off)
 *   preview(draft)                       "the rep in this scope will see": photo / location chips + the sentence lines
 *   daysInclusive(a, b) / exceptionErrors(form, today)   the new exception's day counter and its checks (≤ 30 days,
 *                                        both ends included, no past start, end required, a reason ≤ 500, ≥ 1 rule)
 *   RULES / ruleState(rule, fields)      the three relaxable rules; a rule the rep already has is not offered
 *   chipOf(exception)                    the status / phase chip (draft, in review, planned, active, expired, cancelled,
 *                                        ended early)
 *   FILTERS / filterExceptions / tabBadge   the list's chips and counts; the tab badge = in review + active
 *   actionsOf(exception, perms, me)      the detail drawer's buttons by the CRM abilities (+ the SoD hint for one's own)
 *   timeline(exception)                  the detail's history (created, submitted, decisions, approved, cancelled …)
 *   historyRows(items) / effectiveRows(dto)   the change history and the effective-rules table
 *   EXTRA / canSelect / repRows / NEW_FIELDS   WP-VW-W3E (W3E-WEB-b) — the "additional evidence" fields (institution QR
 *                                        code off / optional / required, device verification off / flag; "block" is
 *                                        shown but NOT selectable until W3E-ATTEST), the reps' effective values
 *   errorKey(response)                   a CRM refusal → the user's text key (never a raw code)
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.VisitEvidenceCore = api; }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    // ── the policy ──────────────────────────────────────────────────────────────────────────────────────────
    const FIELDS = ['locationMode', 'radiusMeters', 'maxAccuracyMeters', 'accountWithoutLocation', 'photoMode',
        'doctorConfirmation', 'durationConsistency', 'shortVisitSeconds',
        'qrMode', 'antiFraudMode']; // WP-VW-W3E (W3E-WEB-b) — the "additional evidence" section (not a GROUP: its own card)

    /** WP-VW-W3E — the two fields that are new in the effective table / history ("new" badge, the mockup). */
    const NEW_FIELDS = ['qrMode', 'antiFraudMode'];

    /** The mockup's groups: location · photo · approval and duration. */
    const GROUPS = [
        { key: 'location', icon: 'bx-map-pin', fields: ['locationMode', 'radiusMeters', 'maxAccuracyMeters', 'accountWithoutLocation'] },
        { key: 'photo', icon: 'bx-camera', fields: ['photoMode'] },
        { key: 'approval', icon: 'bx-check-double', fields: ['doctorConfirmation', 'durationConsistency', 'shortVisitSeconds'] }
    ];

    /** The CRM bounds (VisitEvidencePolicyDefaults) and the tenant defaults. */
    const RANGES = {
        radiusMeters: { min: 50, max: 5000, unit: 'm', def: 300 },
        maxAccuracyMeters: { min: 10, max: 1000, unit: 'm', def: 150 },
        shortVisitSeconds: { min: 30, max: 1800, unit: 's', def: 120 }
    };

    /** The choice fields and their wire values (segmented: location, photo; radio: an institution without a location). */
    const OPTIONS = {
        locationMode: ['required', 'info'],
        photoMode: ['required', 'optional'],
        accountWithoutLocation: ['allow_flagged', 'block'],
        qrMode: ['off', 'optional', 'required'],
        antiFraudMode: ['off', 'flag']
    };

    /**
     * WP-VW-W3E (W3E-WEB-b, E1) — the "additional evidence" card: the QR code as three choice cards (each with its effect
     * sentence), the device verification as a segment whose "block" is drawn disabled with "soon" (W3E-ATTEST): the CRM
     * refuses it today, so it can never be chosen nor sent.
     */
    const EXTRA = {
        qrMode: { options: ['off', 'optional', 'required'] },
        antiFraudMode: { options: ['off', 'flag', 'block'], soon: ['block'] }
    };

    /** A value a user may pick: only the field's wire values (the soon ones are not). */
    const canSelect = (field, value) => (OPTIONS[field] || []).indexOf(value) > -1;

    const KIND = {
        locationMode: 'seg', photoMode: 'seg', accountWithoutLocation: 'radio',
        radiusMeters: 'num', maxAccuracyMeters: 'num', shortVisitSeconds: 'num',
        doctorConfirmation: 'switch', durationConsistency: 'switch',
        qrMode: 'extra', antiFraudMode: 'extra'
    };

    /** The editable copy (numbers as they are typed: strings are allowed while editing). */
    const draftOf = policy => {
        const p = policy || {};
        const out = {};
        FIELDS.forEach(f => { out[f] = p[f]; });
        return out;
    };

    const same = (a, b) => String(a) === String(b);

    /** The changed fields, in form order: [{ field, from, to }]. */
    const changes = (original, draft) => FIELDS
        .filter(f => !same((original || {})[f], (draft || {})[f]))
        .map(f => ({ field: f, from: (original || {})[f], to: (draft || {})[f] }));

    const isInt = v => /^\s*\d+\s*$/.test(String(v == null ? '' : v));

    /** { field: { min, max } } for every number out of its bounds; the threshold only counts while duration is on. */
    const fieldErrors = draft => {
        const d = draft || {};
        const out = {};
        Object.keys(RANGES).forEach(f => {
            if (f === 'shortVisitSeconds' && !d.durationConsistency) { return; }
            const r = RANGES[f];
            const n = Number(d[f]);
            if (!isInt(d[f]) || n < r.min || n > r.max) { out[f] = { min: r.min, max: r.max }; }
        });
        return out;
    };

    /**
     * The PUT body (VisitEvidencePolicyInput). Numbers as numbers; while duration consistency is OFF an out-of-range
     * threshold is left out (absent = the CRM keeps the stored value), so a value the user cannot see never fails the save.
     */
    const putBody = (draft, version) => {
        const d = draft || {};
        const body = {
            locationMode: d.locationMode,
            radiusMeters: Number(d.radiusMeters),
            maxAccuracyMeters: Number(d.maxAccuracyMeters),
            photoMode: d.photoMode,
            doctorConfirmation: !!d.doctorConfirmation,
            durationConsistency: !!d.durationConsistency,
            accountWithoutLocation: d.accountWithoutLocation,
            expectedVersion: typeof version === 'number' && version > 0 ? version : null
        };
        // WP-VW-W3E — the two new fields, only as one of their wire values ("block" never leaves the page)
        if (canSelect('qrMode', d.qrMode)) { body.qrMode = d.qrMode; }
        if (canSelect('antiFraudMode', d.antiFraudMode)) { body.antiFraudMode = d.antiFraudMode; }
        const r = RANGES.shortVisitSeconds;
        const n = Number(d.shortVisitSeconds);
        if (d.durationConsistency || (isInt(d.shortVisitSeconds) && n >= r.min && n <= r.max)) { body.shortVisitSeconds = n; }
        return body;
    };

    /** A value → { key, args } of its label (choices and switches) or { unit, value } of a number. */
    const valueKey = (field, value) => {
        if (KIND[field] === 'switch') { return { key: value ? 'ValueOn' : 'ValueOff', args: [] }; }
        if (KIND[field] === 'num') { return { key: RANGES[field].unit === 'm' ? 'UnitMeters' : 'UnitSeconds', args: [value] }; }
        return { key: 'Opt_' + field + '_' + value, args: [] };
    };

    /**
     * "The rep in this scope will see": the photo / location chips (required → warning, optional / info → info) and
     * the sentence lines, as keys + arguments (the mockup's preview, word for word in Turkish).
     */
    const preview = draft => {
        const d = draft || {};
        const photo = d.photoMode === 'required';
        const loc = d.locationMode === 'required';
        const lines = [{ icon: 'bx-play-circle', tone: 'primary', key: photo && loc ? 'PvOpenBoth' : loc ? 'PvOpenLocation' : photo ? 'PvOpenPhoto' : 'PvOpenNone', args: [d.radiusMeters] }];
        if (loc) { lines.push({ icon: 'bx-target-lock', tone: 'muted', key: 'PvAccuracy', args: [d.maxAccuracyMeters] }); }
        const block = d.accountWithoutLocation === 'block';
        lines.push({ icon: block ? 'bx-block' : 'bx-flag', tone: block ? 'danger' : 'warning', key: block ? 'PvNoLocationBlock' : 'PvNoLocationFlag', args: [] });
        if (d.durationConsistency) { lines.push({ icon: 'bx-timer', tone: 'muted', key: 'PvShort', args: [d.shortVisitSeconds] }); }
        return {
            photoKey: photo ? 'PvRequired' : 'PvOptional',
            locationKey: loc ? 'PvRequired' : 'PvInfo',
            locationTone: loc ? 'warning' : 'info',
            locationTextKey: loc ? 'PvLocationRequired' : 'PvLocationInfo',
            locationArgs: [d.radiusMeters],
            sign: !!d.doctorConfirmation,
            startDisabled: photo || loc,
            lines: lines
        };
    };

    // ── the time-boxed exceptions ───────────────────────────────────────────────────────────────────────────
    const RULES = ['location_info', 'photo_optional', 'account_without_location_allow', 'qr_optional']; // + W3E (E2)
    const MAX_DAYS = 30;
    const MAX_REASON = 500;

    /** The policy field a rule relaxes and the value it relaxes it to. */
    const RULE_FIELD = {
        location_info: { field: 'locationMode', value: 'info' },
        photo_optional: { field: 'photoMode', value: 'optional' },
        account_without_location_allow: { field: 'accountWithoutLocation', value: 'allow_flagged' },
        // WP-VW-W3E (W3E-WEB-b, E2) — the QR code: required → optional (an off / optional rep has nothing to relax)
        qr_optional: { field: 'qrMode', value: 'optional', only: 'required' }
    };

    /** The rules that are new this round (the "new" badge of the exception window). */
    const NEW_RULES = ['qr_optional'];

    const dayNo = iso => {
        const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(iso || ''));
        return m ? Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])) / 864e5 : null;
    };

    /** Days from a to b, BOTH ends included (13 → 13 = 1); 0 when either is missing or b < a. */
    const daysInclusive = (a, b) => {
        const x = dayNo(a), y = dayNo(b);
        return x == null || y == null || y < x ? 0 : y - x + 1;
    };

    /** The UTC day the CRM calls "today". */
    const todayUtc = nowMs => new Date(nowMs).toISOString().slice(0, 10);

    /**
     * The new exception's checks (the CRM's VisitEvidenceExceptionChecks, ahead of time):
     * { rep, rules, from, to, why } → { key, args } per failing field. No past start; an end is required (no open-ended
     * exception); at most 30 days both ends included; a reason ≤ 500.
     */
    const exceptionErrors = (form, today) => {
        const f = form || {};
        const out = {};
        if (!f.resourceId) { out.rep = { key: 'XErrRep', args: [] }; }
        if (!(f.rules || []).length) { out.rules = { key: 'XErrRules', args: [] }; }
        if (!f.startDate) { out.from = { key: 'XErrFrom', args: [] }; } else if (f.startDate < today) { out.from = { key: 'XErrPast', args: [] }; }
        const days = daysInclusive(f.startDate, f.endDate);
        if (!f.endDate) { out.to = { key: 'XErrToRequired', args: [] }; } else if (f.startDate && f.endDate < f.startDate) { out.to = { key: 'XErrToBefore', args: [] }; } else if (days > MAX_DAYS) { out.to = { key: 'XErrTooLong', args: [days] }; }
        const why = String(f.reason || '');
        if (!why.trim()) { out.why = { key: 'XErrWhy', args: [] }; } else if (why.length > MAX_REASON) { out.why = { key: 'XErrWhyLong', args: [MAX_REASON] }; }
        return out;
    };

    /**
     * A rule against the rep's effective rules (fields[] of GET effective): off without a rep; off when the rep already
     * has the relaxed value (nothing to relax), with where it comes from.
     */
    const ruleState = (rule, fields) => {
        if (!fields) { return { enabled: false, reason: 'XRuleNoRep', source: null }; }
        const map = RULE_FIELD[rule];
        const f = (fields || []).find(x => x.field === map.field);
        if (f && f.value === map.value) { return { enabled: false, reason: 'XRuleAlready', source: f.source }; }
        if (map.only && (!f || f.value !== map.only)) { return { enabled: false, reason: 'XRuleQrNotRequired', source: f ? f.source : null }; }
        return { enabled: true, reason: 'XRuleNow', source: f ? f.source : null, value: f ? f.value : null, field: map.field };
    };

    /** The status / phase chip of an exception: { key, tone }. */
    const chipOf = x => {
        const s = x && x.status;
        if (s === 'draft') { return { key: 'XSt_draft', tone: 'secondary' }; }
        if (s === 'in_review') { return { key: 'XSt_in_review', tone: 'warning' }; }
        if (s === 'cancelled') { return { key: 'XSt_cancelled', tone: 'danger' }; }
        if (s === 'ended_early') { return { key: 'XSt_ended_early', tone: 'muted' }; }
        const phase = x && x.phase;
        if (phase === 'active') { return { key: 'XSt_active', tone: 'success' }; }
        if (phase === 'planned') { return { key: 'XSt_planned', tone: 'info' }; }
        return { key: 'XSt_expired', tone: 'outline' };
    };

    /** The list's chips: the status / phase each one filters on. */
    const FILTERS = [
        { key: 'all' }, { key: 'active', phase: 'active' }, { key: 'planned', phase: 'planned' },
        { key: 'in_review', status: 'in_review' }, { key: 'draft', status: 'draft' }, { key: 'expired', phase: 'expired' },
        { key: 'cancelled', status: 'cancelled' }, { key: 'ended_early', status: 'ended_early' }
    ];

    const matches = (x, filter) => !filter || filter.key === 'all'
        || (filter.status ? x.status === filter.status : x.status === 'approved' && x.phase === filter.phase);

    const filterExceptions = (items, key) => {
        const filter = FILTERS.find(f => f.key === key) || FILTERS[0];
        return (items || []).filter(x => matches(x, filter));
    };

    /** A chip's count (from the CRM counts when present, else counted). */
    const filterCount = (list, key) => {
        const filter = FILTERS.find(f => f.key === key) || FILTERS[0];
        const items = (list && list.items) || [];
        if (filter.key === 'all') { return items.length; }
        if (filter.status && list && list.statusCounts && typeof list.statusCounts[filter.status] === 'number') { return list.statusCounts[filter.status]; }
        if (filter.phase && list && list.phaseCounts && typeof list.phaseCounts[filter.phase] === 'number') { return list.phaseCounts[filter.phase]; }
        return items.filter(x => matches(x, filter)).length;
    };

    /** The exceptions tab badge: waiting for approval + active today. */
    const tabBadge = list => filterCount(list, 'in_review') + filterCount(list, 'active');

    const samePerson = (a, b) => !!a && !!b && String(a).trim().toLowerCase() === String(b).trim().toLowerCase();

    /** The caller wrote / last edited / submitted it — the reviewer side of the SoD. */
    const isOwn = (x, me) => !!x && (samePerson(me, x.createdBy) || samePerson(me, x.updatedBy) || samePerson(me, x.submittedBy));

    /**
     * The detail drawer's buttons from the CRM abilities × the page keys. A record in review the caller wrote shows the
     * SoD hint and a DISABLED approve (the mockup); one they cannot decide on otherwise shows nothing to decide.
     * perms = { manage, read }.
     */
    const actionsOf = (x, perms, me) => {
        const a = (x && x.abilities) || {};
        const p = perms || {};
        const own = x && x.status === 'in_review' && isOwn(x, me);
        return {
            edit: !!(p.manage && a.canEdit),
            submit: !!(p.manage && a.canSubmit),
            withdraw: !!(p.manage && a.canWithdraw),
            decide: !!(a.canDecide && !own),
            sod: !!own,
            approveDisabled: !!own,
            cancel: !!(p.manage && a.canCancel),
            endEarly: !!(p.manage && a.canEndEarly)
        };
    };

    /** The detail's history, oldest first: { key, by, at, note }. */
    /**
     * WP-VW-W3-FIX (F3) — a Work Center machine reason code (WORKCENTER_APPROVE, …) is not a note: CRM no longer stores /
     * returns one; a value an older record still carries is dropped here. A real comment (a return note) stays.
     */
    const MACHINE_CODE = /^WORKCENTER_[A-Z_]+$/;
    const isMachineCode = v => typeof v === 'string' && MACHINE_CODE.test(v.trim());
    const note = v => (v == null || v === '' || isMachineCode(v) ? null : v);

    const timeline = x => {
        if (!x) { return []; }
        const out = [{ key: 'TlCreated', by: x.createdBy, at: x.createdAt, note: null }];
        (x.reviewRounds || []).forEach(r => {
            out.push({ key: 'TlSubmitted', by: r.submittedBy, at: r.submittedAt, note: null });
            const d = (x.decisions || []).find(dd => dd.roundNo === r.roundNo);
            if (d) { out.push({ key: d.outcome === 'approve' ? 'TlApproved' : 'TlReturned', by: d.by, at: d.at, note: note(d.comment) }); }
            else if (r.outcome === 'cancelled') { out.push({ key: 'TlWithdrawn', by: r.completedBy, at: r.closedAt, note: null }); }
            else if (r.outcome === 'timed-out') { out.push({ key: 'TlTimedOut', by: null, at: r.closedAt, note: null }); }
            if (r.reasonCode && /_sod$|_overlap$/.test(r.reasonCode)) { out.push({ key: 'TlRefused_' + r.reasonCode, by: null, at: r.closedAt, note: null }); }
        });
        if (x.status === 'cancelled' || x.status === 'ended_early') {
            out.push({ key: x.status === 'cancelled' ? 'TlCancelled' : 'TlEndedEarly', by: x.cancelledBy, at: x.cancelledAt, note: x.cancelReason || null });
        }
        return out.filter(e => e.at).sort((a, b) => (a.at < b.at ? -1 : a.at > b.at ? 1 : 0));
    };

    /** The change history rows: { at, by, fieldKey, from, to, kind } (a "created" row has no field). */
    const historyRows = items => (items || []).map(h => ({
        at: h.at, by: h.by, kind: h.kind,
        field: h.field || null,
        from: h.kind === 'created' ? null : h.oldValue,
        to: h.kind === 'created' ? null : h.newValue
    }));

    /** A history / effective value (a string on the wire) → its typed value for valueKey. */
    const typed = (field, raw) => {
        if (KIND[field] === 'switch') { return raw === true || raw === 'true'; }
        if (KIND[field] === 'num') { return Number(raw); }
        return raw;
    };

    /** The effective rules table in form order: { field, value, source, exceptionId }. */
    const effectiveRows = dto => FIELDS.map(f => {
        const row = ((dto && dto.fields) || []).find(x => x.field === f) || {};
        return { field: f, value: typed(f, row.value), source: row.source || 'tenant', exceptionId: row.exceptionId || null };
    });

    /**
     * WP-VW-W3E (W3E-WEB-b, E3) — the reps' effective values of the mockup's columns (location, photo, QR, device): the
     * tenant policy relaxed by each rep's APPROVED exception ACTIVE today (the list the page already reads), so one read
     * fills the table — the same rule as the CRM resolver. → [{ resourceId, name, cells: { field: { value, fromException } } }]
     */
    const REP_COLUMNS = ['locationMode', 'photoMode', 'qrMode', 'antiFraudMode'];
    const repRows = (reps, policy, exceptions) => {
        const p = policy || {};
        const active = (exceptions || []).filter(x => x && x.status === 'approved' && x.phase === 'active');
        return (reps || []).map(r => {
            const cells = {};
            REP_COLUMNS.forEach(f => { cells[f] = { value: p[f], fromException: false }; });
            active.filter(x => x.resourceId === r.resourceId).forEach(x => (x.rules || []).forEach(rule => {
                const map = RULE_FIELD[rule];
                if (!map || !cells[map.field] || (map.only && cells[map.field].value !== map.only)) { return; }
                cells[map.field] = { value: map.value, fromException: true };
            }));
            return { resourceId: r.resourceId, name: r.displayName || r.resourceId, cells: cells };
        });
    };

    // ── people (WP-VW-W3-FIX F5) ─────────────────────────────────────────────────────────────────────────────
    /** Adds a DTO's displayNames (id "D" → name; CRM resolves them) to the page's map; ids compare case-insensitively. */
    const mergeNames = (target, displayNames) => {
        const out = target || {};
        Object.keys(displayNames || {}).forEach(k => { if (displayNames[k]) { out[String(k).toLowerCase()] = displayNames[k]; } });
        return out;
    };

    /**
     * Who a user id is on the screen: the name CRM resolved (also for the session user), then the rep roster's name, then
     * "Siz" for the session user, the workflow actor label, else a short id (never a guessed name).
     */
    const personLabel = (id, people, repNames, me, labels) => {
        const T = labels || {};
        if (!id) { return '—'; }
        const key = String(id).toLowerCase();
        if (people && people[key]) { return people[key]; }
        if (repNames && repNames[id]) { return repNames[id]; }
        if (me && key === String(me).toLowerCase()) { return T.YouLabel || ''; }
        if (/^workflow:/.test(id)) { return T.WorkflowActor || ''; }
        return String(id).length > 12 ? String(id).slice(0, 8) + '…' : String(id);
    };

    // ── refusals ───────────────────────────────────────────────────────────────────────────────────────────
    const ERROR_CODES = [
        'visit_evidence_exception_invalid', 'visit_evidence_exception_overlap', 'visit_evidence_exception_rep_unknown',
        'visit_evidence_exception_not_editable', 'visit_evidence_exception_sod', 'visit_evidence_exception_not_found',
        'visit_evidence_policy_invalid', 'visit_execution_concurrency_conflict', 'workflow_template_missing',
        'workflow_unavailable', 'approval_forbidden', 'no_open_review', 'withdraw_not_possible', 'rejection_comment_required',
        'workflow_request_rejected', 'permission_denied'
    ];

    /** The first known code of a refusal → Err_{code}; 409 without a code = a version conflict; else the generic text. */
    const errorKey = response => {
        const errors = response && response.body && Array.isArray(response.body.errors) ? response.body.errors : [];
        const hit = errors.find(c => ERROR_CODES.indexOf(c) > -1);
        if (hit) { return 'Err_' + hit; }
        if (response && response.status === 409) { return 'Err_conflict'; }
        if (response && response.status === 403) { return 'Err_permission_denied'; }
        if (!response || response.status === 0 || response.status >= 500) { return 'Err_unavailable'; }
        return 'Err_generic';
    };

    return {
        FIELDS, GROUPS, RANGES, OPTIONS, KIND, draftOf, changes, fieldErrors, putBody, valueKey, preview,
        RULES, RULE_FIELD, MAX_DAYS, MAX_REASON, daysInclusive, todayUtc, exceptionErrors, ruleState, chipOf,
        FILTERS, filterExceptions, filterCount, tabBadge, isOwn, actionsOf, timeline, historyRows, typed, effectiveRows,
        ERROR_CODES, errorKey, isMachineCode, mergeNames, personLabel,
        NEW_FIELDS, EXTRA, canSelect, NEW_RULES, REP_COLUMNS, repRows
    };
}));
