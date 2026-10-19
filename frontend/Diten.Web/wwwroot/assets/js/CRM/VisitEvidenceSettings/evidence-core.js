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
 *   WP-VW-W3L (W3-WEB-e) — the scope layers (tenant › legal entity › business unit, K-L1 / K-L2):
 *   scopeId / parseSource / scopeNames / scopeName / sourceOf   a scope's id; a field source → its label (the NAME)
 *   scopeTree / scopeMeta / addOptions   the tree of GET scopes, its meta line, what "add" may still offer
 *   layerOverrides / layerEffective / layerChanges / layerFieldErrors / layerPutBody   a layer's form: null = from above;
 *                                        the PUT body carries EVERY field (an inherited one as null, never "")
 *   chainNotes / appliedLayers / historyScope / historyQuery   why a layer was skipped for a rep, the rep's applied
 *                                        layers, a history row's scope and the history filter
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
        // W3E-FIX (G-5) — the QR code as the rep will meet it: a row on the start window (required / optional; NONE when off)
        // + the rule sentence; device verification "flag" → the web line the rep sees on the start window (S7 — never blocks).
        const qrMode = d.qrMode === 'required' || d.qrMode === 'optional' ? d.qrMode : null;
        if (qrMode) { lines.push({ icon: 'bx-qr', tone: qrMode === 'required' ? 'warning' : 'info', key: 'D_qrMode_' + qrMode, args: [] }); }
        const deviceWeb = d.antiFraudMode === 'flag';
        return {
            photoKey: photo ? 'PvRequired' : 'PvOptional',
            locationKey: loc ? 'PvRequired' : 'PvInfo',
            locationTone: loc ? 'warning' : 'info',
            locationTextKey: loc ? 'PvLocationRequired' : 'PvLocationInfo',
            locationArgs: [d.radiusMeters],
            sign: !!d.doctorConfirmation,
            startDisabled: photo || loc || qrMode === 'required',
            qr: qrMode ? { modeKey: qrMode === 'required' ? 'PvRequired' : 'PvOptional', tone: qrMode === 'required' ? 'warning' : 'info' } : null,
            deviceWeb: deviceWeb,
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
        from: h.kind === 'created' || h.kind === 'archived' ? null : h.oldValue,
        to: h.kind === 'created' || h.kind === 'archived' ? null : h.newValue,
        // WP-VW-W3L — a layer's side that does not override the field is "from above" (null on the wire); the row's scope
        fromInherited: isLayerChange(h) && h.oldValue == null,
        toInherited: isLayerChange(h) && h.newValue == null,
        scope: historyScope(h)
    }));
    const isLayerChange = h => !!h && h.kind !== 'created' && h.kind !== 'archived' && !!h.scope && h.scope !== 'tenant';

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
    const repRows = (reps, policy, exceptions, layers) => {
        const p = policy || {};
        const active = (exceptions || []).filter(x => x && x.status === 'approved' && x.phase === 'active');
        return (reps || []).map(r => {
            const cells = {};
            REP_COLUMNS.forEach(f => { cells[f] = { value: p[f], fromException: false }; });
            // WP-VW-W3L — the rep's layers (the roster row's chain): the legal entity's overrides, then the business unit's
            // (K-L1), each only where it overrides; an approved exception still relaxes on top
            const chain = r.scopeChain || null;
            const ids = chain ? [chain.legalEntity ? scopeId('legal-entity', chain.legalEntity) : null, chain.businessUnit ? scopeId('business-unit', chain.businessUnit) : null] : [];
            ids.forEach(id => {
                const ov = id && layers ? layers[id] : null;
                if (ov) { REP_COLUMNS.forEach(f => { if (!inherited(ov[f])) { cells[f] = { value: ov[f], fromException: false }; } }); }
            });
            active.filter(x => x.resourceId === r.resourceId).forEach(x => (x.rules || []).forEach(rule => {
                const map = RULE_FIELD[rule];
                if (!map || !cells[map.field] || (map.only && cells[map.field].value !== map.only)) { return; }
                cells[map.field] = { value: map.value, fromException: true };
            }));
            return { resourceId: r.resourceId, name: r.displayName || r.resourceId, cells: cells };
        });
    };

    // ── WP-VW-W3L (W3-WEB-e) — the scope layers ─────────────────────────────────────────────────────────────
    /** The two layers a manager adds under the tenant (K-L1: the business unit wins over the legal entity). */
    const LAYER_TYPES = ['legal-entity', 'business-unit'];

    /** A legal entity is a Guid (compared case-insensitively, "D"), a business unit its code. */
    const normRef = (type, ref) => (ref == null ? '' : type === 'legal-entity' ? String(ref).trim().toLowerCase() : String(ref).trim());

    /** The page's id of a scope: "tenant" | "legal-entity:<id>" | "business-unit:<code>" (the CRM's field source spelling). */
    const scopeId = (type, ref) => (type === 'tenant' || !type ? 'tenant' : type + ':' + normRef(type, ref));

    /** A field source of the CRM (tenant | legal-entity:<id> | business-unit:<code> | exception) → { type, ref }. */
    const parseSource = source => {
        const s = String(source || 'tenant');
        if (s === 'tenant') { return { type: 'tenant', ref: null }; }
        if (s === 'exception' || s.indexOf('exception:') === 0) { return { type: 'exception', ref: null }; }
        const i = s.indexOf(':');
        const type = i > 0 ? s.slice(0, i) : s;
        return LAYER_TYPES.indexOf(type) > -1 ? { type: type, ref: normRef(type, s.slice(i + 1)) } : { type: 'tenant', ref: null };
    };

    /**
     * The names of the scopes the page can show: the MDM legal entities ({ ref: id, name }) and the published business-unit
     * values ({ ref: code, name }) → { scopeId: name }. Never the code where a name is known.
     */
    const scopeNames = (legalEntities, businessUnits) => {
        const out = {};
        (legalEntities || []).forEach(x => { if (x && x.ref && x.name) { out[scopeId('legal-entity', x.ref)] = x.name; } });
        (businessUnits || []).forEach(x => { if (x && x.ref && x.name) { out[scopeId('business-unit', x.ref)] = x.name; } });
        return out;
    };

    /** A scope's name: the tenant's label; a layer's name from the lists; when no list knows it, its code (a legal entity
     *  Guid shortened) — the screen never invents a name. */
    const scopeName = (type, ref, names, labels) => {
        if (type === 'tenant' || !type) { return (labels && labels.TenantDefault) || ''; }
        const known = names && names[scopeId(type, ref)];
        if (known) { return known; }
        const r = String(ref || '');
        return type === 'legal-entity' && r.length > 12 ? r.slice(0, 8) + '…' : r;
    };

    /** The tone of a scope (the mockup: tenant secondary, legal entity info, business unit primary, exception warning). */
    const SCOPE_TONE = { tenant: 'secondary', 'legal-entity': 'info', 'business-unit': 'primary', exception: 'warning' };
    const SCOPE_ICON = { tenant: 'bx-buildings', 'legal-entity': 'bx-briefcase', 'business-unit': 'bx-sitemap' };

    /**
     * Where a field's value comes from, as a label: { key, args, tone } — "Kiracı" | "Tüzel kişilik: <name>" |
     * "İş birimi: <name>" | "İstisna · <date>'e kadar" (the caller gives the end day). The NAME, never the code.
     */
    const sourceOf = (source, names, labels) => {
        const s = parseSource(source);
        if (s.type === 'exception') { return { type: s.type, key: 'SourceExceptionUntil', args: [], tone: SCOPE_TONE.exception }; }
        if (s.type === 'tenant') { return { type: s.type, key: 'SourceTenant', args: [], tone: SCOPE_TONE.tenant }; }
        return {
            type: s.type, ref: s.ref, tone: SCOPE_TONE[s.type],
            key: s.type === 'legal-entity' ? 'SourceLegalEntity' : 'SourceBusinessUnit',
            args: [scopeName(s.type, s.ref, names, labels)]
        };
    };

    /**
     * The scope tree (mockup C1) from GET scopes: the tenant first, then the legal entities, then the business units (each
     * by name). A business unit is not under a legal entity: the CRM takes it from the rep's territory assignment, whatever
     * their legal entity (K-L4). → [{ id, type, ref, name, overridden, repCount, repCountNote, version, updatedAt,
     * updatedBy, isDefault, depth }]
     */
    const scopeTree = (list, names, labels) => {
        const items = (list && list.items) || [];
        const node = x => ({
            id: scopeId(x.scopeType, x.scopeRef), type: x.scopeType || 'tenant', ref: x.scopeType === 'tenant' ? null : normRef(x.scopeType, x.scopeRef),
            name: scopeName(x.scopeType, x.scopeRef, names, labels), overridden: x.overriddenFields || 0,
            repCount: typeof x.repCount === 'number' ? x.repCount : 0, repCountNote: x.repCountNote || null,
            version: x.version || 0, updatedAt: x.updatedAt || null, updatedBy: x.updatedBy || null, isDefault: !!x.isDefault,
            depth: x.scopeType === 'tenant' ? 0 : 1
        });
        const byName = (a, b) => String(a.name).localeCompare(String(b.name));
        const tenant = items.filter(x => x.scopeType === 'tenant').map(node);
        const le = items.filter(x => x.scopeType === 'legal-entity').map(node).sort(byName);
        const bu = items.filter(x => x.scopeType === 'business-unit').map(node).sort(byName);
        return (tenant.length ? tenant : [node({ scopeType: 'tenant', isDefault: true })]).concat(le, bu);
    };

    /** A tree row's meta line as keys: the tenant "Tüm alanlar dolu", a layer "N alan farklı" / "Değişiklik yok"; the reps
     *  "N temsilci" ("en az N" when some seats could not be read). */
    const scopeMeta = n => ({
        fields: n.type === 'tenant' ? { key: 'AllFields', args: [] } : n.overridden ? { key: 'Diff', args: [n.overridden] } : { key: 'NoDiff', args: [] },
        reps: { key: n.repCountNote === 'platform-unavailable' ? 'RepsAtLeast' : 'Reps', args: [n.repCount] }
    });

    /** The options "add a legal entity / business unit" offers: the list's entries without a layer yet, by name. */
    const addOptions = (type, options, tree) => {
        const taken = (tree || []).filter(n => n.type === type).map(n => n.id);
        return (options || []).filter(o => o && o.ref && taken.indexOf(scopeId(type, o.ref)) < 0)
            .map(o => ({ id: scopeId(type, o.ref), ref: normRef(type, o.ref), name: o.name || o.ref }))
            .sort((a, b) => String(a.name).localeCompare(String(b.name)));
    };

    const inherited = v => v === null || v === undefined || v === '';

    /** A layer's overrides (GET scopes/{type}/{ref} fields, the wire strings) → { field: typed value | null } (null = from above). */
    const layerOverrides = detail => {
        const out = {};
        FIELDS.forEach(f => { out[f] = null; });
        ((detail && detail.fields) || []).forEach(x => {
            if (x && FIELDS.indexOf(x.field) > -1) { out[x.field] = x.value == null ? null : typed(x.field, x.value); }
        });
        return out;
    };

    /** The values a rep in the scope starts with: the base (the tenant policy) with the scope's overrides on top. */
    const layerEffective = (base, overrides) => {
        const out = draftOf(base);
        FIELDS.forEach(f => { if (!inherited((overrides || {})[f])) { out[f] = overrides[f]; } });
        return out;
    };

    /** The fields the layer overrides (its "N alan farklı"). */
    const overriddenCount = overrides => FIELDS.filter(f => !inherited((overrides || {})[f])).length;

    /** The changed fields of a layer: { field, from: { value, inherited }, to: { value, inherited } } — an inherited side
     *  carries the value it inherits (the confirm says "Üstten: X" / "Üst ayara döner (X)"). */
    const layerChanges = (original, draft, base) => FIELDS
        .filter(f => {
            const a = (original || {})[f], b = (draft || {})[f];
            return inherited(a) !== inherited(b) || (!inherited(a) && !same(a, b));
        })
        .map(f => {
            const side = v => (inherited(v) ? { value: (base || {})[f], inherited: true } : { value: v, inherited: false });
            return { field: f, from: side((original || {})[f]), to: side((draft || {})[f]) };
        });

    /** A layer's number bounds: only the fields it overrides (an inherited value is the scope above's to keep). */
    const layerFieldErrors = (overrides, base) => {
        const all = fieldErrors(layerEffective(base, overrides));
        const out = {};
        Object.keys(all).forEach(f => { if (!inherited((overrides || {})[f])) { out[f] = all[f]; } });
        return out;
    };

    /**
     * The layer PUT body (VisitEvidencePolicyLayerInput): EVERY field, an inherited one as null (the PUT replaces the
     * layer's overrides — a field left out or sent as "" is not "from above"); numbers as numbers, switches as booleans, a
     * choice only as one of its wire values ("block" never leaves the page).
     */
    const layerPutBody = (overrides, version) => {
        const o = overrides || {};
        const body = {};
        FIELDS.forEach(f => {
            const v = o[f];
            if (inherited(v)) { body[f] = null; return; }
            if (KIND[f] === 'num') { body[f] = Number(v); return; }
            if (KIND[f] === 'switch') { body[f] = v === true || v === 'true'; return; }
            body[f] = canSelect(f, v) ? v : null;
        });
        body.expectedVersion = typeof version === 'number' && version > 0 ? version : null;
        return body;
    };

    /** Why a layer was skipped for this rep (the CRM's ScopeChain notes) → the note keys, legal entity first. */
    const CHAIN_NOTES = {
        'no-seat': 'ChainNoSeat', 'platform-unavailable': 'ChainPlatformDown', 'no-territory': 'ChainNoTerritory',
        'no-business-unit': 'ChainNoBusinessUnit', 'bu-ambiguous': 'ChainBuAmbiguous'
    };
    const chainNotes = chain => {
        if (!chain) { return []; }
        const out = [];
        const take = n => { const k = CHAIN_NOTES[String(n || '').trim()]; if (k && out.indexOf(k) < 0) { out.push(k); } };
        if (chain.legalEntityNote || chain.businessUnitNote) { take(chain.legalEntityNote); take(chain.businessUnitNote); }
        else { String(chain.note || '').split(',').forEach(take); }
        return out;
    };

    /** The layers that apply to a rep today (their chain against the tenant's layers): legal entity, then business unit. */
    const appliedLayers = (chain, tree) => {
        const out = [];
        if (!chain) { return out; }
        const find = (type, ref) => (tree || []).find(n => n.type === type && n.id === scopeId(type, ref));
        if (chain.legalEntity) { const le = find('legal-entity', chain.legalEntity); if (le) { out.push(le); } }
        if (chain.businessUnit) { const bu = find('business-unit', chain.businessUnit); if (bu) { out.push(bu); } }
        return out;
    };

    /** A history row's scope (the CRM writes scope + scopeRef) → { type, ref, id }. */
    const historyScope = item => {
        const type = item && LAYER_TYPES.indexOf(item.scope) > -1 ? item.scope : 'tenant';
        return { type: type, ref: type === 'tenant' ? null : normRef(type, item.scopeRef), id: scopeId(type, item && item.scopeRef) };
    };

    /** The history read's scope filter for the proxy: "" (every scope) | "tenant" | "<type>:<ref>" → query parameters. */
    const historyQuery = filter => {
        if (!filter) { return {}; }
        if (filter === 'tenant') { return { scope: 'tenant' }; }
        const i = filter.indexOf(':');
        return i > 0 ? { scope: filter.slice(0, i), scopeRef: filter.slice(i + 1) } : {};
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
        NEW_FIELDS, EXTRA, canSelect, NEW_RULES, REP_COLUMNS, repRows,
        LAYER_TYPES, SCOPE_TONE, SCOPE_ICON, CHAIN_NOTES, normRef, scopeId, parseSource, scopeNames, scopeName, sourceOf, scopeTree,
        scopeMeta, addOptions, layerOverrides, layerEffective, overriddenCount, layerChanges, layerFieldErrors, layerPutBody,
        chainNotes, appliedLayers, historyScope, historyQuery
    };
}));
