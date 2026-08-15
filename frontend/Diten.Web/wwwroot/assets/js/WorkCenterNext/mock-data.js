'use strict';

/*
 * WorkCenterNext mock catalog facade.
 * Canonical truth lives in fixtures/*. This file only supplies collection/list
 * presentation fields required by the existing WorkCenterNext shell.
 */
(function (global) {
    // The day the SHOWCASE fixtures are authored against. Their due dates are written relative to it, so the demo
    // catalogue only reads correctly when measured from here. It is NOT the clock for real work — see referenceDate.
    const SHOWCASE_TODAY_ISO = '2026-07-24';
    const SHOWCASE_TODAY = new Date(SHOWCASE_TODAY_ISO + 'T09:00:00+03:00');
    /*
     * The clock, split by provenance.
     *
     * Real items must be measured from the REAL today. Measuring them from the fixture reference day is what made
     * every real due date read two days optimistic — an item due in 4 days showed "6g kaldı" — with the error
     * growing by one day every day, and an already-late item reported as merely due soon.
     *
     * `nowProvider` is the injection seam: a test pins "now" so it asserts a fixed answer instead of starting to
     * fail tomorrow. Production never sets it and reads the wall clock.
     */
    let nowProvider = () => new Date();
    const setNowProvider = (provider) => {
        nowProvider = typeof provider === 'function' ? provider : () => new Date();
    };
    const localIsoDate = (date) => {
        const pad = (value) => String(value).padStart(2, '0');
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    };
    const referenceDate = (provenance) => (provenance === 'fixture' ? SHOWCASE_TODAY : nowProvider());
    /*
     * Showcase-only identities. The real session user comes from the SERVER (window.CurrentUser, rendered by the
     * tenant shell from the JWT claims) — see sessionUser. Handing these to a real user showed them someone else's
     * name and job title in the scope selector.
     */
    const CURRENT_USER = { id: 'USR-OWN', name: 'Selin Aras', title: 'Operasyon PMO Lideri' };
    const DELEGATORS = [
        { id: 'USR-103', name: 'Deniz Koç', title: 'Finans Kontrolörü' },
        { id: 'USR-104', name: 'Aylin Ersoy', title: 'Satınalma Lideri' }
    ];
    /*
     * The signed-in user, from the claims the shell already serialized — no new endpoint, no invented data.
     * `title` is deliberately null: there is NO source for a position/title on the client today, and an absent
     * title renders as nothing at all, which is the honest answer. Do not substitute a role, an email or a
     * placeholder here — that is exactly the habit this slice removes.
     */
    const sessionUser = () => {
        const claims = global.CurrentUser || {};
        const name = [claims.firstName, claims.lastName].filter(Boolean).join(' ').trim()
            || claims.email
            || '';
        return { id: claims.id || null, name, title: null };
    };
    const MEETINGS = [
        { id: 'MTG-1001', title: 'Haftalık Operasyon Toplantısı', start: '14:00', end: '15:00', location: 'Teams', owner: 'Selin Aras' }
    ];
    const NOTES = [
        { id: 'NOTE-1001', text: 'Q3 bütçe revizyonları için departman onaylarını topla.', ageKey: 'TimeToday', converted: false }
    ];
    const TYPE_ICON = { approval: 'bx-check-shield', task: 'bx-task', review: 'bx-search-alt', issue: 'bx-error-circle', exception: 'bx-error-alt' };
    /*
     * Friendly module name for a provider code, from the resx (7 languages).
     *
     * This used to be a hardcoded Turkish map, and moduleLabel is called for REAL items too — so the "Görevler"
     * chip on genuine work was single-language presentation data invented on the client. The key is derived from
     * the code (master-data → ModuleMasterData) so adding a provider means adding one resx entry, not editing JS.
     *
     * An unmapped code renders as the raw code and warns once: a new provider shows up as a visible, explained
     * gap instead of silently borrowing someone else's name.
     */
    const reportedMissingModuleCodes = new Set();
    const moduleResourceKey = (code) => 'Module' + String(code).split(/[-_]/)
        .filter(Boolean)
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
        .join('');
    const moduleLabel = (code) => {
        if (!code) { return ''; }
        const key = moduleResourceKey(code);
        const resolved = global.WCN?.t?.(key);
        if (!resolved || resolved === key) {
            if (!reportedMissingModuleCodes.has(code)) {
                reportedMissingModuleCodes.add(code);
                console.warn(
                    `[WorkCenterNext] No module name for provider code "${code}" — rendering the raw code. `
                    + `Add "${key}" to the WorkCenterNext resx (7 languages).`);
            }
            return code;
        }
        return resolved;
    };
    /*
     * Curation for the DEVELOPMENT showcase catalog only: which demo fixtures are "in the catalogue" and which are
     * parked. It is an allowlist of FIXTURE IDS, so it can only ever be applied to fixtures — a real work item has
     * a GUID that is by definition absent here, and gating real items on it hides every one of them.
     * See toPresentation: provenance decides whether this list is consulted at all.
     */
    const VISIBLE_CATALOG_IDS = new Set([
        'INBOX-TASK-01', 'INBOX-APPROVAL-01', 'INBOX-REVIEW-OPTIONAL-MEETING',
        'INBOX-REVIEW-REQUIRED-MEETING', 'INBOX-ISSUE-01', 'INBOX-EXCEPTION-01',
        // İşlerim showcase (real, varied) replaces the placeholder WC-TASK-* variants.
        'ISLERIM-WORK-ACTIVE', 'ISLERIM-WORK-ISSUE', 'ISLERIM-WORK-BLOCKED',
        'ISLERIM-WORK-DELEGATED', 'ISLERIM-WORK-SNOOZED', 'ISLERIM-WORK-WAITING',
        'ISLERIM-WORK-REVIEW-MEETING', 'ISLERIM-WORK-PLANNED',
        // Havuz showcase — group-queue (claim) + offered (accept/decline).
        'HAVUZ-CLAIM-01', 'HAVUZ-CLAIM-02', 'HAVUZ-OFFER-01',
        // Geçmiş showcase — real Done/Cancelled archive (replaces WC-TASK-DONE placeholder).
        'GECMIS-TASK-DONE-01', 'GECMIS-APPROVAL-DONE-01', 'GECMIS-TASK-CANCELLED-01',
        'WC-TASK-DONE'   // legacy Geçmiş placeholder (kept until showcase locked)
    ]);
    const clone = (value) => (typeof global.structuredClone === 'function')
        ? global.structuredClone(value)
        : JSON.parse(JSON.stringify(value));
    /*
     * Resolve a contract label to text.
     *   { kind: 'resource', key, args? } → looked up in the WorkCenterNext resx
     *   { kind: 'display',  text, locale } → already final; used for content a user typed
     *
     * A resource key with no resx entry falls back to the key itself, which renders as visible gibberish
     * ("WorkAggregation_Title_Task"). That fallback is now announced, once per key, so the next provider to
     * introduce a label without a translation finds out immediately instead of shipping it.
     */
    const reportedMissingLabelKeys = new Set();
    const resolveLabel = (label) => {
        if (!label) { return ''; }
        if (label.kind === 'resource') {
            // WC-1b DEC-3 — a backend label carries NAMED args ({objectType}/{objectId}); render them through
            // the named-token helper so the title never shows literal placeholders. Mock labels have no args
            // and fall through to the plain lookup unchanged.
            const resolved = (label.args && global.WCN?.tn)
                ? global.WCN.tn(label.key, label.args)
                : global.WCN?.t?.(label.key);

            if (!resolved || resolved === label.key) {
                if (!reportedMissingLabelKeys.has(label.key)) {
                    reportedMissingLabelKeys.add(label.key);
                    console.warn(
                        `[WorkCenterNext] Missing resource label "${label.key}" — rendering the raw key. `
                        + 'Add it to the WorkCenterNext resx (7 languages), or have the provider send '
                        + '{ kind: "display", text, locale } if the text is user-entered and needs no translation.');
                }
                return label.key;
            }

            return resolved;
        }
        return label.text || '';
    };
    const personName = (person) => {
        if (!person) { return ''; }
        if (person.displayName) { return person.displayName; }
        if (person.isCurrentUser) { return global.WCN?.t?.('PersonSelf') || ''; }
        // An id is not a name: showing the GUID would be worse than admitting the name is unknown.
        return global.WCN?.t?.('PersonNameUnavailable') || '';
    };

    /*
     * SHOWCASE ONLY (WC-2). The SLA state of REAL work is decided by the server, through IWorkingTimeCalculator,
     * and arrives as `slaState` on the projection.
     *
     * This used to decide it for everything, which inverted the surface's own law — the browser renders
     * decisions, it does not make them — and left the working calendar (BL: Calendar) with nothing on the server
     * to arrive at. It survives because the showcase catalogue has no server behind it: its fixtures are authored
     * against a fixed reference day and must keep reading correctly. Its `<= 2` is a DEMO threshold; the real one
     * is a server policy (WorkAggregation:Sla:DueSoonWithinWorkingDays) and is not mirrored here on purpose —
     * two copies of a threshold is how the copies start disagreeing.
     */
    const computeShowcaseSla = (dueAt, closedAt) => {
        if (!dueAt) { return { state: 'no-sla', diffDays: null }; }
        const due = new Date(`${dueAt}T00:00:00`);
        /*
         * BL-046, in the showcase's own terms. The catalogue plays the part of a server, so it has to answer this
         * question the way one does: FINISHED work is measured from the day it closed, not from the demo's today.
         * Without this, a History fixture whose own activity log says it was closed on time would still be
         * painted late — the demo contradicting itself where the real surface no longer does.
         */
        const reference = closedAt ? new Date(`${dateOnly(closedAt)}T00:00:00`) : referenceDate('fixture');
        const base = new Date(reference.getFullYear(), reference.getMonth(), reference.getDate());
        const diffDays = Math.round((due - base) / 86400000);
        return { state: diffDays < 0 ? 'overdue' : diffDays <= 2 ? 'due-soon' : 'on-track', diffDays };
    };

    /*
     * How many days away a deadline is, measured AT RENDER TIME from the absolute due date.
     *
     * Only the WORDING uses this — "3 gün kaldı". The STATE is the server's. That split is the cure for the
     * frozen-count defect this project already shipped once (`ago`): a day count computed on the server is a lie
     * the moment the tab outlives it, so the count is derived late here from the absolute date the projection
     * already carries, and the count never decides anything.
     */
    const daysUntil = (dueAt, provenance) => {
        if (!dueAt) { return null; }
        const due = new Date(`${dueAt}T00:00:00`);
        const reference = referenceDate(provenance);
        const base = new Date(reference.getFullYear(), reference.getMonth(), reference.getDate());
        return Math.round((due - base) / 86400000);
    };

    /*
     * The DAY part of an absolute value, whether it arrived as a date ('2026-07-20') or as a full instant
     * ('2026-07-20T16:30:00+00:00'). Deadlines are a whole-day question — "closed two days late" — so the clock
     * time is dropped rather than allowed to turn a two-day overrun into 1.8.
     */
    const dateOnly = (value) => {
        const match = /^(\d{4}-\d{2}-\d{2})/.exec(String(value || ''));
        return match ? match[1] : null;
    };

    /*
     * BL-046 — how late FINISHED work was, measured between its own two absolute dates and nothing else.
     *
     * The sign follows daysUntil deliberately (negative = past the deadline), so the label's arithmetic reads the
     * same whichever branch produced the number.
     *
     * This is the half that makes the badge stop lying. `daysUntil` is right for live work — an open deadline
     * genuinely IS a day nearer tomorrow — and catastrophic for closed work: History read "11 days late" one
     * morning and "12 days late" the next about a task nobody had touched. Today is not part of this answer.
     */
    const daysLateAtClose = (dueAt, closedAt) => {
        const due = dateOnly(dueAt);
        const closed = dateOnly(closedAt);
        if (!due || !closed) { return null; }
        const dueDay = new Date(`${due}T00:00:00`);
        const closedDay = new Date(`${closed}T00:00:00`);
        if (Number.isNaN(dueDay.getTime()) || Number.isNaN(closedDay.getTime())) { return null; }
        return Math.round((dueDay - closedDay) / 86400000);
    };
    const tabFor = (item) => {
        if (['Done', 'Cancelled'].includes(item.normalizedStatus)) { return 'history'; }
        if (item.admissionState === 'pendingClaim' || item.admissionState === 'pendingOffer') { return 'havuz'; }
        if (item.admissionState === 'pendingAcceptance') { return 'inbox'; }
        // Act-directly intents (approval/review/issue/exception) awaiting the viewer's
        // first decision live in the Inbox even though they are 'admitted' (no accept
        // gate) — they are resolved on the spot (approve/signoff/resolve), not owned work.
        if (['approval', 'review', 'issue', 'exception'].includes(item.workIntent) && item.normalizedStatus === 'Pending') { return 'inbox'; }
        return 'islerim';
    };
    const segmentFor = (item) => {
        if (item.normalizedStatus === 'Waiting') { return 'bekleyen'; }
        if (item.taskLifecycle === 'Planned' || (item.personal?.plannedDate && item.executionState === 'notStarted')) { return 'planli'; }
        return 'aktif';
    };
    const actionForPresentation = (action) => ({
        key: action.code,
        code: action.code,
        labelKey: action.label?.kind === 'resource' ? action.label.key : null,
        displayLabel: resolveLabel(action.label),
        semanticType: action.semanticType || action.code,
        /*
         * Whether this action destroys or calls off work. The engine says "destructive" (and "elevated" for
         * raised-risk); this file only ever tested for "danger", a value no provider emits — so cancel arrived
         * marked destructive and was styled and treated as an ordinary next step. The shell uses this to keep a
         * destructive action from ever LEADING a row.
         */
        destructive: ['danger', 'destructive'].includes(action.riskLevel),
        kind: ['danger', 'destructive'].includes(action.riskLevel) ? 'danger'
            : ['approve', 'complete', 'resolve', 'signoff', 'submitReview'].includes(action.code) ? 'success'
                : action.code === 'requestInfo' ? 'warning'
                    : action.code === 'accept' || action.code === 'claim' || action.code === 'start' || action.code === 'resume' ? 'primary'
                        : 'secondary',
        primary: false,
        enabled: action.enabled,
        disabled: action.enabled === false,
        disabledReasonKey: action.disabledReason?.kind === 'resource' ? action.disabledReason.key : null,
        disabledReason: resolveLabel(action.disabledReason),
        confirm: action.requiresConfirmation,
        reason: action.requiresReason,
        evidence: action.requiresEvidence,
        bulk: action.supportsBulk,
        /*
         * `plan` ALWAYS wants a date picker, on every provenance — derived from the CODE rather than trusted from
         * the wire, the same way `kind` and `role` above are. The engine's WorkItemActionDto carries no `input`
         * field at all, so a real `plan` action would otherwise never open the picker; only a raw fixture that
         * happened to set `input: 'date'` would, and none ever did. This is what actually wires the picker up.
         */
        input: action.input || (action.code === 'plan' ? 'date' : null),
        /*
         * Where the action HAPPENS: 'inline' acts here, 'deeplink' sends the reader to the source. Carried
         * through because getActions needs it — a closed item may still offer "open in source" while offering
         * nothing to press here (BL-038). Left unresolved (null, not 'inline') so getActions can apply the
         * item-level `actionDepth` default itself, exactly the way the contract resolves it.
         */
        depth: action.depth || null,
        role: ['reject', 'return', 'declineMeeting'].includes(action.code) ? 'reject'
            : ['approve', 'accept', 'claim', 'complete', 'resolve', 'signoff', 'start', 'resume', 'acceptMeeting', 'submitReview'].includes(action.code) ? 'accept'
                : null
    });
    const allFixtureGroups = () => {
        const fixtures = global.WorkCenterNextFixtures || {};
        const migrationAdapter = global.WorkCenterNextMigrationAdapter?.adaptLegacyFixture;
        const adaptedMigration = (fixtures.migration || []).map((fixture) => migrationAdapter?.(fixture)).filter(Boolean);
        return [
            ...(fixtures.inboxShowcase || []),
            ...(fixtures.islerimShowcase || []),
            ...(fixtures.havuzShowcase || []),
            ...(fixtures.gecmisShowcase || []),
            ...(fixtures.canonical || []),
            ...(fixtures.edgeCases || []),
            ...(fixtures.enterpriseStrategy || []),
            ...(fixtures.documentation || []),
            ...adaptedMigration
        ];
    };
    /*
     * Map a contract-shaped work item to the shape the shell renders.
     *
     * `options.provenance` says where the item came from: 'fixture' for the Development showcase catalog, 'api'
     * for the real projection. It defaults to 'api' ON PURPOSE — the failure mode of guessing wrong in that
     * direction is a parked demo fixture appearing, whereas guessing 'fixture' hides genuine work, which is
     * exactly the bug this argument fixes.
     */
    const toPresentation = (fixture, options) => {
        const provenance = (options && options.provenance) || 'api';
        // Re-projecting an already-presented item must not RE-STAMP its origin. The default above is right for a
        // raw contract item but wrong for one that already knows what it is, and a silent 'fixture' → 'api' slide
        // turns the showcase catalogue's own curation off for that item. Callers pass provenance explicitly; this
        // says so out loud if one ever stops.
        if (fixture && fixture.provenance && fixture.provenance !== provenance) {
            console.warn(
                `[WorkCenterNext] Work item "${fixture.id}" is being re-projected as provenance="${provenance}" `
                + `but it was "${fixture.provenance}". Pass { provenance } explicitly at the call site — an item `
                + 'that changes origin also changes which guards apply to it.');
        }
        const item = clone(fixture);
        const showcaseSla = provenance === 'fixture'
            ? computeShowcaseSla(item.dueAt, isTerminal(item) ? item.closedAt : null)
            : null;
        item.itemType = item.workIntent;
        item.lifecycle = item.taskLifecycle;
        item.status = item.normalizedStatus === 'InProgress' ? 'In Progress' : item.normalizedStatus;
        item.nativeStatusText = resolveLabel(item.nativeStatus?.label);
        item.titleText = resolveLabel(item.title);
        item.summaryText = resolveLabel(item.summary);
        item.title = item.titleText;
        item.summary = item.summaryText;
        item.sourceModule = moduleLabel(item.source?.providerCode);
        item.sourceModuleName = moduleLabel(item.source?.sourceSystem) || item.sourceModule;
        item.sourceModuleId = item.source?.moduleId || null;
        item.sourceType = item.source?.objectType || '';
        item.sourceId = item.source?.objectId || '';
        item.sourceObjectType = item.sourceType;
        item.deepLink = item.source?.deepLink || null;
        item.typeIcon = TYPE_ICON[item.workIntent] || 'bx-circle';
        item.accepted = item.admissionState === 'admitted';
        item.claimed = item.ownershipState === 'owned';
        item.startedOnce = item.executionState === 'active' || item.executionState === 'paused';
        /*
         * WHICH SEAT THE READER IS IN — derived here, BEFORE the person objects are flattened to names.
         *
         * The detail page showed a role chip with no text on every real task, because `viewerRole` is a fixture
         * field and the projection has never carried one. It does carry `isCurrentUser` on assignee/requester —
         * the one thing the server can state for certain — so the role is read from that. A fixture that
         * declares its own role keeps it; when neither person is the caller nothing is claimed and the chip
         * does not render at all.
         */
        if (!item.viewerRole) {
            if (item.assignee?.isCurrentUser) { item.viewerRole = 'Owner'; }
            else if (item.requester?.isCurrentUser) { item.viewerRole = 'Creator'; }
        }
        // A person is { id, displayName } — fixtures carry the name, the real projection cannot yet resolve it
        // (no user-directory seam in Platform), so fall back to "Me" for the caller and to a plain
        // name-unavailable label for anyone else. Never render a raw user GUID.
        item.requester = personName(item.requester);
        item.assignee = personName(item.assignee);
        item.scope = item.delegationContext ? 'onBehalf' : 'mine';
        item.delegator = item.delegationContext?.displayName || null;
        // A group is shown only when something actually NAMES one. The projection carries no pool identity — a
        // real item says only assignmentMode:"groupQueue", never WHICH queue — so deriving a name from that flag
        // meant labelling genuine CFO-pool work "Operasyon Kuyruğu", a queue that does not exist. Nothing is
        // synthesized any more: buildGroupSelector then renders nothing, and the Havuz tab stops asserting a team
        // it cannot know. Giving the provider a pool-identity field is WC-3 contract work (BL-031 a/b), NOT this
        // slice. A showcase fixture may still declare its own `group` and keep it; none does today.
        /*
         * WHICH queue this work waits in. It now comes from the projection (WC-3 / BL-031): `pool.label` is the
         * position joined to its organization unit, resolved server-side.
         *
         * Still NOTHING is synthesized. A pool whose position could not be read arrives with an id and no label,
         * and an unlabelled queue shows no group — the same silence that replaced the fabricated "Operasyon
         * Kuyruğu", which named a team that does not exist for every pooled item. A showcase fixture may still
         * declare its own `group` and keep it.
         */
        item.group = provenance === 'fixture'
            ? (item.group || item.pool?.label?.text || null)
            : (item.pool?.label?.text || null);
        item.isUnread = item.personal?.seen === false;
        item.pinned = !!item.personal?.pinned;
        item.snoozedUntil = item.personal?.snoozedUntil || null;
        // A REAL item's plan date arrives on the wire (item.plannedDate, normalized by adaptProjection) and must
        // WIN here — this used to overwrite it unconditionally with `personal?.plannedDate`, which real work
        // never carries, silently discarding a plan the moment it left the write path: stored on the server,
        // invisible on every screen that reads it back. Fixtures still set theirs via `personal`.
        item.plannedDate = item.plannedDate || item.personal?.plannedDate || null;
        // Two different questions, two different fields. `waitingOn` is WHO (a typed identity, so a name can be
        // rendered); `reason` is WHY, in the holder's own words. The reason text used to be sent inside
        // waitingOn, where this line reads `.displayName` off a string — so the sentence the user typed was on
        // the wire and rendered as nothing at all.
        item.waitingOn = item.waitingContext?.waitingOn?.displayName || null;
        item.waitingReason = resolveLabel(item.waitingContext?.reason) || null;
        /*
         * WC-1 — the personal NOTES, a list now and stored on the server. `personal.notes` is what the projection
         * emits (id · text · createdAt), and the array is normalised here so every reader downstream can map over
         * it without asking whether the container arrived: a task nobody has written on carries no `personal` at
         * all, deliberately, so an unguarded `.notes.map` would throw on the ordinary case.
         *
         * The single `personal.note` string this replaced was never on the wire — nothing wrote it and no fixture
         * declared it. It read a field that only the browser's own unsaved assignment ever set.
         */
        item.notes = Array.isArray(item.personal?.notes) ? item.personal.notes : [];
        /*
         * WC-2. The state comes from the PROJECTION for real work and is never re-derived here; a real item whose
         * provider said nothing reads `no-sla`, which is the honest answer — "this provider does not track
         * deadlines" — rather than a number this file invented.
         *
         * The showcase catalogue keeps its own answer because it has no server behind it.
         */
        item.slaState = showcaseSla ? (item.slaState || showcaseSla.state) : (item.slaState || 'no-sla');
        // Absent for every provider that cannot say when its work closed (MOD-0023 today) — null, never invented.
        item.closedAt = item.closedAt || null;
        /*
         * Derived late, for the LABEL only — see daysUntil. EXCEPT once the work is closed (BL-046): a finished
         * item is measured between its deadline and its closing day, so the count is a fact about that task
         * rather than a fact about today, and it reads the same tomorrow.
         *
         * A closed item whose provider sent no closing instant falls back to the live count. That keeps sorting
         * sensible, and the label never prints it — slaLabel says "closed late" without a number rather than
         * quoting one that would drift, which is the original defect wearing a new word.
         */
        const frozenDiff = isTerminal(item) ? daysLateAtClose(item.dueAt, item.closedAt) : null;
        item.slaDiffDays = frozenDiff === null ? daysUntil(item.dueAt, provenance) : frozenDiff;
        item.actions = item.actions.map((candidate) => {
            const mapped = actionForPresentation(candidate);
            mapped.primary = candidate.code === item.primaryActionCode;
            return mapped;
        });
        item.tab = tabFor(item);
        item.dismissed = false;
        // Showcase curation applies to showcase fixtures ONLY. A real projection item is visible because the
        // backend already decided the actor may see it — re-filtering it here against a list of demo ids removed
        // every genuinely created task from the surface.
        item.provenance = provenance;   // real vs showcase — decides whether ACTIONS hit the server
        item.catalogVisible = provenance === 'fixture' ? VISIBLE_CATALOG_IDS.has(item.id) : true;
        if (item.catalogVisible === false) {
            // NEVER filter silently: this exact hidden exclusion turned a working backend into an invisible one
            // and cost hours of diagnosis.
            console.warn(
                `[WorkCenterNext] Work item "${item.id}" hidden by the showcase catalog filter `
                + `(sourceModule="${item.sourceModule || item.source?.providerCode || 'unknown'}", `
                + `provenance="${provenance}"): its id is not in VISIBLE_CATALOG_IDS. `
                + 'Real projection items must never reach this branch — if this is one, its provenance is wrong.');
        }
        // WC-1b DEC-2 — the projection's additive `escalation` object (unmodelled by the contract) folds onto the
        // boolean signal the shell already renders as the "Eskale" chip. No contract/backend change.
        item.escalated = !!(item.escalated || item.escalation?.escalated);
        item.reviewRequired = item.taskLifecycle === 'PendingReview';
        item.checklist = item.checklist ? {
            ...item.checklist,
            items: (item.checklist.items || []).map((entry) => ({
                ...entry,
                text: resolveLabel(entry.label) || entry.text || '',
                done: entry.completed === true || entry.done === true,
                /*
                 * The level arrives as TWO booleans and the screen needs ONE name. Derived here, beside `text`
                 * and `done`, rather than at each of the four places that ask: the write path has to send a
                 * level back, and a second derivation is how the chip and the request start disagreeing.
                 *
                 * `blocking` wins — an item that stops completion is not merely expected.
                 */
                requirement: entry.blocking ? 'Blocking' : (entry.required ? 'Required' : 'Optional'),
                // A template item's words are the template's; the server refuses to reword one. The projection
                // says so through the label's KIND, which is the only place that fact is carried.
                templateOwned: entry.label?.kind === 'resource' || !!entry.label?.key,
                /*
                 * The SERVER'S answer to "may this reader change this row", carried through unchanged.
                 *
                 * Not re-derived here from an author id, and the projection deliberately does not send one: the
                 * rule then exists once, on the side that enforces it, rather than twice with a chance to
                 * disagree. Defaulted true so a provider that has no concept of authorship keeps behaving as it
                 * did — a missing field must not silently take controls away.
                 */
                editable: entry.editable !== false
            }))
        } : null;
        item.subtasks = item.subtasks || null;
        item.dependencies = item.dependencies ? item.dependencies.map((entry) => ({
            ...entry,
            title: resolveLabel(entry.title) || entry.title || ''
        })) : null;
        // A blocker's label is a contract label like any other. Left unresolved it reached the banner as an
        // object and rendered as "[object Object]" — the same failure the dependency titles above already had.
        item.blockedState = item.blockedState
            ? {
                ...item.blockedState,
                blockers: (item.blockedState.blockers || []).map((blocker) => ({
                    ...blocker,
                    labelText: resolveLabel(blocker.label) || ''
                }))
            }
            : item.blockedState;
        item.attachments = item.attachments ? item.attachments.map((entry) => ({
            ...entry,
            name: resolveLabel(entry.label) || entry.name || entry.id,
            size: entry.version ? `v${entry.version}` : ''
        })) : null;
        /*
         * Activity carries an ABSOLUTE `at`; "3 days ago" is computed where it is rendered.
         *
         * It used to be derived here, once, into a day count. That is the frozen-date class of bug: a projection
         * held in memory while a tab stays open — or served from a cache — keeps saying "today" tomorrow. The
         * server does not send a day count either, for the same reason.
         *
         * Parsed once here (fixtures write "2026-07-24 09:10", not ISO) and left as a timestamp.
         */
        item.activity = (item.activity || []).map((entry) => {
            const stamp = (value) => {
                const parsed = value ? new Date(String(value).replace(' ', 'T')) : null;
                return (parsed && !isNaN(parsed)) ? parsed.getTime() : null;
            };
            // `editedAt` gets the SAME treatment as `at`, and for the same reason: it is an absolute instant on
            // the wire and the words beside it ("edited", plus the date on hover) are derived where they render.
            return { ...entry, atMs: stamp(entry.at), editedAtMs: stamp(entry.editedAt) };
        });
        item.stages = item.processStages || null;
        item.timesheet = item.workItemCapabilities.includes('timeTracking')
            // A running timer needs a real start anchor, else the live tick renders
            // `Date.now() - null` (epoch millis) as a nonsense elapsed value.
            ? {
                running: item.timerState === 'running',
                startedAt: item.timerState === 'running' ? Date.now() - (37 * 60000) : null,
                loggedMinutes: item.loggedMinutes || 0
            }
            : null;
        item._fixture = fixture;
        return item;
    };
    /*
     * WC-1b DEC-1 — FIXTURE SOURCE vs PRESENTATION MAPPER.
     * The mapper (toPresentation + tabFor/segmentFor/computeBlocked/getActions/resolveLabel) is NOT
     * mock-specific: the real API path maps canonical work items through exactly the same code.
     *
     * `computeShowcaseSla` is the one part that is NOT shared (WC-2): a real item's slaState is decided by the
     * server and only read here, so the two paths deliberately diverge at that one point. Only the fixture
     * SOURCE below is showcase data, and it is reachable ONLY when the server says so.
     *
     * The switch is decided SERVER-side (IWebHostEnvironment → data-wcn-fixtures on #wcnApp) and re-read on each
     * call, so production has no client-reachable path to fixture data — a hand-typed query string alone does
     * nothing because the attribute is only emitted in Development.
     */
    const showcaseFixturesEnabled = () => {
        const host = global.document?.getElementById('wcnApp');
        return host?.dataset?.wcnFixtures === 'showcase';
    };
    const buildItems = () => (showcaseFixturesEnabled()
        // Showcase fixtures declare their provenance so the curated allowlist applies to them alone.
        ? allFixtureGroups().map((fixture) => toPresentation(fixture, { provenance: 'fixture' }))
        : []);
    /*
     * Is this work finished? Terminal means Done or Cancelled, read from EITHER the normalized status or the task
     * lifecycle, because the two can disagree on the wire and "finished" is the stronger claim.
     *
     * Exported so app.js reads the same definition instead of keeping a second copy: two definitions of "closed"
     * would drift, and the tab routing, the read-only rules and the action filter below all depend on it agreeing.
     */
    const isTerminal = (item) => ['Done', 'Cancelled'].includes(item?.normalizedStatus)
        || item?.lifecycle === 'Done' || item?.lifecycle === 'Cancelled';

    /*
     * The actions a surface may offer for an item.
     *
     * This is the ONE place both surfaces read from — app.js's `itemActions` wraps it, and the list rows, the
     * table, the bulk bar and the detail rail all go through that — so a rule written here reaches every surface
     * at once and no surface can forget it.
     *
     * BL-038: a CLOSED item offers no INLINE action. History is meant to be read-only, and until now that held
     * only because TaskWorkItemProvider happens to send an empty action set for terminal work — the surface
     * itself would happily render a disabled button if one ever arrived. This makes the rule the surface's own.
     *
     * `deeplink` actions survive deliberately: opening the SOURCE record of a finished task is a legitimate
     * thing to want, and the depth axis already means exactly "acts here / goes elsewhere". The default is
     * `inline` (resolved against the item, then the action, the same order the contract uses), so an action that
     * declares no depth is filtered.
     *
     * Why here and NOT in fixture-contract.js: validateItems DROPS an item that fails validation
     * (work-items-api.js), so a contract rule would make a mis-projected task VANISH from History. A lost task is
     * worse than a leaked disabled button — and this codebase has already lost real items that way once, to
     * `catalogVisible`.
     */
    const getActions = (item) => {
        const actions = clone(item?.actions || []);
        if (!isTerminal(item)) { return actions; }
        const itemDepth = item?.actionDepth || 'inline';
        return actions.filter((action) => (action.depth || itemDepth) === 'deeplink');
    };
    const buildTriggers = () => (showcaseFixturesEnabled()
        ? clone(global.WorkCenterNextFixtures?.triggerOnly || [])
        : []);

    /*
     * `onBehalfOf`, `status`, the old `computeSla` and `computeBlocked` used to be exported here and were read from
     * nowhere (0 references in app.js and in the tests). They are gone rather than kept "just in case": a mock
     * export nobody consumes is a standing invitation to consume it.
     *
     * The three getters below are evaluated per access, not frozen at load, because each answers "what is real
     * right now" — the showcase flag is a runtime attribute and the clock moves.
     */
    global.WorkCenterNextData = {
        // Snooze bounds and the calendar's today-highlight are about the USER's clock, so outside the showcase
        // they must read the real day, not the day the fixtures were written against.
        get todayIso() {
            return showcaseFixturesEnabled() ? SHOWCASE_TODAY_ISO : localIsoDate(nowProvider());
        },
        // Showcase keeps its demo persona so the catalogue stays coherent; real sessions get the real user, with
        // no title, because there is no source for one.
        get currentUser() {
            return showcaseFixturesEnabled() ? CURRENT_USER : sessionUser();
        },
        /*
         * Delegation scopes. There is NO delegation data for a real session — Platform exposes no "who delegated
         * to me" seam yet — so outside the showcase this is empty and the scope selector collapses to "Kendim".
         * The code path is intentionally left intact: the day a real provider lands, it fills this array and the
         * selector comes back on its own. Listing the showcase's people to a real user offered them delegation
         * from colleagues who do not exist.
         */
        get delegators() {
            return showcaseFixturesEnabled() ? DELEGATORS : [];
        },
        tabFor,
        segmentFor,
        // One definition of "closed", shared with app.js — see isTerminal for why it must not be duplicated.
        isTerminal,
        // Exposed so the RENDER can measure "how long ago" against the same clock the rest of the surface uses:
        // the showcase's frozen date for fixtures, the real one for real work.
        referenceDate,
        getActions,
        setNowProvider,
        toPresentation,
        buildItems,
        buildTriggers,
        showcaseFixturesEnabled,
        buildMeetings: () => (showcaseFixturesEnabled() ? clone(MEETINGS) : []),
        buildNotes: () => (showcaseFixturesEnabled() ? clone(NOTES) : []),
        resolveLabel
    };
})(typeof window !== 'undefined' ? window : globalThis);
