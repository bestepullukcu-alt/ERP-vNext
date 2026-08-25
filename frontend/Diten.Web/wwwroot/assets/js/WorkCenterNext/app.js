'use strict';

/*
 * WorkCenterNext — main app (frontend-only, mock-driven).
 *
 * Views (spec §4): List (default, grouped by SLA-state) · Split-detail ·
 * Table (multi-select + footer bulk bar) · Focus/Today.
 * Scope toggle: My Work / On-behalf-of.
 * 5 item types with type-specific action bars (spec §5).
 * Keyboard: j/k move, a accept/approve, r reject/return, Enter open, Esc clear.
 *
 * Renders everything from JS into #wcnApp so every label has a single source
 * (window.WCN.t / .tf → 7-language resx). No backend / API calls.
 */
(function (global) {
    const data = global.WorkCenterNextData;
    const t = (global.WCN && global.WCN.t) || ((k) => k);
    const tf = (global.WCN && global.WCN.tf) || ((k) => k);
    const SEEN_STORAGE_KEY = 'workcenter-next.seen-items';
    const readSeenIds = () => {
        try { return new Set(JSON.parse(global.sessionStorage?.getItem(SEEN_STORAGE_KEY) || '[]')); }
        catch (_) { return new Set(); }
    };
    const persistSeenIds = (ids) => {
        try { global.sessionStorage?.setItem(SEEN_STORAGE_KEY, JSON.stringify(Array.from(ids))); }
        catch (_) { /* Session persistence is best-effort in the frontend-only slice. */ }
    };
    const seenIds = readSeenIds();
    let workCenterDt = null;

    if (!data) {
        return;
    }

    // ── Static mappings (semantic colour per normalized dimension) ────────────
    const SLA_ORDER = ['overdue', 'due-soon', 'on-track', 'no-sla'];
    const SLA_KIND = { 'overdue': 'danger', 'due-soon': 'warning', 'on-track': 'success', 'no-sla': 'secondary' };
    const SLA_GROUP_KEY = { 'overdue': 'GroupOverdue', 'due-soon': 'GroupDueSoon', 'on-track': 'GroupOnTrack', 'no-sla': 'GroupNoDate' };
    // Keys are the CONTRACT's spelling (fixture-contract PRIORITIES = Low|Medium|High, the engine's own enum).
    // They used to be lowercase, matched nothing a provider emits, and that mismatch is why the column was hidden.
    const PRIORITY_KIND = { High: 'danger', Medium: 'warning', Low: 'secondary' };
    const PRIORITY_KEY = { High: 'PriorityHigh', Medium: 'PriorityMedium', Low: 'PriorityLow' };
    // Most-urgent-first, for sorting and for the filter's option order.
    const PRIORITY_ORDER = ['High', 'Medium', 'Low'];
    // Mirrors TaskCommentLimits.MaxTextLength. Checked here so an over-long comment is refused before a round
    // trip; the server refuses it too, because a client-side check is a courtesy and not a rule.
    const COMMENT_MAX_LENGTH = 2000;
    const STATUS_KIND = { 'Pending': 'primary', 'In Progress': 'info', 'Waiting': 'warning', 'Done': 'success', 'Cancelled': 'secondary' };
    const STATUS_KEY = { 'Pending': 'StatusPending', 'In Progress': 'StatusInProgress', 'Waiting': 'StatusWaiting', 'Done': 'StatusDone', 'Cancelled': 'StatusCancelled' };
    const TYPE_KEY = { approval: 'TypeApproval', task: 'TypeTask', review: 'TypeReview', issue: 'TypeIssue', exception: 'TypeException', meetingInvite: 'ChipMeetingInvite' };
    const TYPE_ICON_MAP = { approval: 'bx-check-shield', task: 'bx-task', review: 'bx-search-alt', issue: 'bx-error-circle', exception: 'bx-error-alt', meetingInvite: 'bx-calendar-event' };
    const SIGNAL_ICON = { blocked: 'bx-lock-alt', 'sla-risk': 'bx-time-five', escalated: 'bx-up-arrow-alt',
        snoozed: 'bx-moon' };
    const MODE_KEY = { direct: 'ModeDirect', approval: 'ModeApproval', groupQueue: 'ModeGroupQueue', offered: 'ModeOffered' };
    const SYSSTATE = {
        stale: { key: 'BannerStale', icon: 'bx-refresh', kind: 'warning' },
        sourceUnavailable: { key: 'BannerSourceUnavailable', icon: 'bx-wifi-off', kind: 'danger' },
        authorityEnded: { key: 'BannerAuthorityEnded', icon: 'bx-user-x', kind: 'danger' },
        reconciliationRequired: { key: 'BannerReconciliationRequired', icon: 'bx-error-alt', kind: 'danger' }
    };
    const ROLE_KEY = { Owner: 'RoleOwner', Approver: 'RoleApprover', Reviewer: 'RoleReviewer', Creator: 'RoleCreator' };

    // Axis law (spec v3): OWNERSHIP→tab · STATUS→segment · TYPE→chip.
    const TABS_PRIMARY = ['inbox', 'islerim'];
    const TABS_SECONDARY = ['havuz', 'history'];
    const TAB_KEY = { inbox: 'TabInbox', islerim: 'TabMine', havuz: 'TabPool', history: 'TabHistory' };
    const SEGMENTS = { islerim: ['aktif', 'bekleyen', 'planli'] };
    const SEGMENT_KEY = { aktif: 'SegActive', bekleyen: 'SegWaiting', planli: 'SegPlanned' };
    /*
     * ⚠ `snoozed` IS A SIGNAL THAT WORKS BACKWARDS, AND THAT IS DELIBERATE (BL-181).
     *
     * Every other chip here NARROWS what is on screen: turn on "SLA riski" and you see fewer rows. This one
     * REVEALS — a snoozed row is hidden by default, and the chip is the door back to it. "Ertelenmiş (3)" turned
     * on shows those three and nothing else.
     *
     * The asymmetry is the point, not an oversight: parking something is the reader's own decision to stop being
     * shown it, so the default has to be absence. A chip that merely narrowed would be a filter over rows the
     * reader had already asked not to see — which is to say, no snooze at all.
     *
     * ⚠ DO NOT "FIX" THIS FOR CONSISTENCY. Making it behave like its neighbours puts every parked item straight
     * back in the list and deletes the feature.
     */
    const SIGNALS = ['blocked', 'sla-risk', 'escalated', 'snoozed'];
    const SIGNAL_KEY = { blocked: 'SignalBlocked', 'sla-risk': 'SignalSlaRisk', escalated: 'SignalEscalated',
        snoozed: 'SignalSnoozed' };
    /*
     * WHERE PARKING APPLIES. `havuz` is work nobody holds yet — a personal overlay has nothing to hide there —
     * and `history` is finished: an item you snoozed and later completed must still appear in your own past.
     */
    const SNOOZE_TABS = ['inbox', 'islerim'];

    const state = {
        tab: 'inbox',
        agendaOpen: false,
        notesOpen: false,
        segment: 'aktif',        // meaningful within İşlerim
        view: 'list',
        viewsByTab: { inbox: 'list', islerim: 'list', havuz: 'list', history: 'list' },
        /*
         * 'mine' | 'all' | <delegator name> (N-way delegation) | 'team' (BL-023).
         *
         * 'team' joins the SAME control rather than becoming a fifth tab: the axis law fixes tab = OWNERSHIP,
         * and "my team" asks that same ownership question about somebody else — which is exactly what this
         * dropdown already does for delegation. SAP My Inbox has the same shape.
         */
        scope: 'mine',
        // Answered by the server at boot (org chart), never inferred from an empty list — see loadTeamAvailability.
        team: { hasTeam: false, memberCount: 0 },
        group: 'all',            // Havuz group-queue filter
        priorityFilter: 'all',
        modeFilter: 'all',
        moduleFilter: [],
        slaFilter: [],           // multi-select SLA state (empty = all) — replaces headings
        pinnedFilter: false,     // show only pinned
        typeFilter: new Set(),   // multi-select type chips (empty = all)
        signalFilter: new Set(), // multi-select signal chips (empty = all)
        filtersOpen: false,      // collapsible filter panel under the chip bar
        search: '',
        selectedId: null,
        tableSelected: new Set(),
        subtaskPanelId: null,
        // The row just added, so the reader can see WHERE it landed. Cleared after one paint — a permanent
        // highlight would become another status colour nobody declared.
        flashSubtaskId: null,
        /*
         * WHICH CAPPED LISTS THE READER HAS OPENED, by list key ('subtasks' | 'activity').
         *
         * Per LIST, not one flag: the two cards answer different questions, and opening the subtasks must not
         * silently unfold the feed underneath. Not persisted — an expansion is about this reading of this page.
         */
        expandedLists: {},
        /*
         * WHICH HALF OF THE FEED IS SHOWING — 'all' | 'comments'.
         *
         * Not persisted, for the same reason an expansion is not: it is about this reading of this page. The
         * detail view is a page load, so it starts at 'all' every time — a filter left on one task can never hide
         * another task's events from a reader who never asked for it.
         */
        activityFilter: 'all',
        /*
         * Which half of the detail page's CONTENT column is showing — 'general' | 'activity'.
         *
         * NOT persisted, by instruction and on merit: a task always opens on what it IS. Restoring a tab would
         * mean the same link shows two different first screens depending on who followed it last.
         *
         * `#etkinlik` in the URL opens straight onto the record — three lines now so the comment notification
         * (D7) can deep-link into it later without this being reopened.
         */
        detailTab: (global.location && global.location.hash === '#etkinlik') ? 'activity' : 'general',
        /*
         * The level the NEXT checklist item will be added at. Optional by default, for the reason the create
         * form gives at length: a Blocking default manufactures tasks nobody can close and nobody chose that.
         *
         * It STICKS between adds on purpose — somebody entering three blocking items should press the level
         * once, not three times — and resets with the page, so it can never leak onto another task.
         */
        checklistDraftLevel: 'Optional',
        subtaskPanelDraft: null,
        subtaskPanelRecord: null,
        subtaskPanelSaving: false,
        subtaskCreateParentId: null,
        subtaskCreateDraft: null,
        subtaskCreateSaving: false,
        assignablePeople: [],
        bulkFailedIds: new Set(),
        sortKey: 'sla',
        sortDir: 'asc',
        pageLength: 10,
        listPage: 0,
        tableColumnVisibility: [true, true, true, true, true, true, true, true],
        loadState: 'loading',
        loadError: null,
        submittingActionCode: null,
        submittingItemId: null,
        submittingTriggerId: null,
        // WC-1b — the data source is no longer synchronous mock fixtures. State starts EMPTY and is populated by
        // loadWorkItems() during boot (real API by default; Development-gated showcase fixtures per DEC-1).
        triggers: [],
        items: [],
        meetings: [],
        notes: [],
        visibleOrder: []
    };
    state.items.forEach((item) => {
        if (seenIds.has(item.id)) {
            item.isUnread = false;
            if (item.personal) { item.personal.seen = true; }
        }
    });
    state.triggers.forEach((trigger) => { trigger.isUnread = !seenIds.has(trigger.id); });

    const STATE_VALUES = {
        tab: ['inbox', 'islerim', 'havuz', 'history'],
        segment: ['aktif', 'bekleyen', 'planli'],
        view: ['list', 'table', 'focus'],
        priority: ['all', 'High', 'Medium', 'Low'],
        mode: ['all', 'direct', 'approval', 'groupQueue', 'offered'],
        panel: ['', 'agenda', 'notes']
    };

    const hydrateStateFromUrl = () => {
        const params = new URL(global.location.href).searchParams;
        const setIfAllowed = (key, param, target) => {
            const value = params.get(param);
            if (value && STATE_VALUES[key].indexOf(value) >= 0) { state[target] = value; }
        };
        setIfAllowed('tab', 'tab', 'tab');
        setIfAllowed('segment', 'segment', 'segment');
        setIfAllowed('view', 'view', 'view');
        setIfAllowed('priority', 'priority', 'priorityFilter');
        setIfAllowed('mode', 'mode', 'modeFilter');
        const modules = (params.get('module') || '').split(',').filter((module) => state.items.some((i) => i.sourceModule === module));
        state.moduleFilter = Array.from(new Set(modules));
        const scope = params.get('scope');
        // "all" only means something when there is delegated work to include, and the selector does not offer it
        // otherwise — so a hand-typed ?scope=all must not strand the surface in a state with no visible control.
        const scopeAllowed = scope === 'mine'
            // BL-023 — a hand-typed ?scope=team must not strand a user who has no reports in a view whose
            // control is disabled. Same guard the delegation scopes already use, same reason.
            || (scope === 'team' && state.team.hasTeam)
            || (data.delegators.length > 0 && (scope === 'all' || data.delegators.some((d) => d.name === scope)));
        if (scope && scopeAllowed) { state.scope = scope; }
        state.group = params.get('group') || 'all';
        state.search = params.get('q') || '';
        state.selectedId = params.get('item') || null;
        state.typeFilter = new Set((params.get('types') || '').split(',').filter((x) => TYPE_KEY[x]));
        state.signalFilter = new Set((params.get('signals') || '').split(',').filter((x) => SIGNALS.indexOf(x) >= 0));
        const panel = params.get('panel') || '';
        state.agendaOpen = panel === 'agenda';
        state.notesOpen = panel === 'notes';
        if (state.tab !== 'islerim') { state.segment = 'aktif'; }
    };

    const syncUrl = () => {
        if (!global.history || state.loadState !== 'ready') { return; }
        const url = new URL(global.location.href);
        const put = (key, value, defaultValue) => {
            if (value && value !== defaultValue) { url.searchParams.set(key, value); }
            else { url.searchParams.delete(key); }
        };
        put('tab', state.tab, 'inbox');
        put('segment', state.tab === 'islerim' ? state.segment : '', 'aktif');
        put('view', state.view, 'list');
        put('scope', state.scope, 'mine');
        put('group', state.group, 'all');
        put('module', state.moduleFilter.slice().sort().join(','), '');
        put('priority', state.priorityFilter, 'all');
        put('mode', state.modeFilter, 'all');
        put('types', Array.from(state.typeFilter).sort().join(','), '');
        put('signals', Array.from(state.signalFilter).sort().join(','), '');
        put('q', state.search, '');
        put('item', state.selectedId, '');
        put('panel', state.agendaOpen ? 'agenda' : state.notesOpen ? 'notes' : '', '');
        global.history.replaceState({ workCenterNext: true }, '', url.pathname + url.search + url.hash);
    };

    // Signal predicates (cross-cutting attention signals, spec v3 §4).
    const SIGNAL_TEST = {
        blocked: (i) => !!(i.blockedState && i.blockedState.blocked),
        'sla-risk': (i) => i.slaState === 'overdue' || i.slaState === 'due-soon',
        escalated: (i) => !!i.escalated,
        // The SAME predicate the chip, the note row and the parked banner already use — wrapped rather than
        // duplicated, because it is declared further down and a second copy would be a second answer.
        snoozed: (i) => isSnoozed(i)
    };

    // ── Helpers ───────────────────────────────────────────────────────────────
    const esc = (value) => String(value == null ? '' : value)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    /*
     * BL-050 — the ONE place a person's id is read out of an assignable-people row.
     *
     * AssignablePersonDto(Guid UserId, string? DisplayName, …) serialises `userId`. There is no `id`, and the
     * reassign picker read exactly that: every <option> got value="", the validation refused every choice the
     * user made, and no request was ever sent. The NAME rendered correctly — displayName was read right — which
     * is what kept it invisible through a whole test round.
     *
     * The repo already had the right answer written twice (this file's own create form, and Tasks/form.js) and
     * still shipped the wrong one a third time. Three spellings of one fact is the condition that produced this,
     * so there is now one. The old `person.userId || person.id` fallback is gone with it: a defensive read of a
     * field that does not exist is what made `person.id` look plausible.
     */
    const personUserId = (person) => person?.userId ?? null;

    /*
     * BL-044 — search folding that does not depend on the reader's locale.
     *
     * `'KAPANIŞ'.toLowerCase()` gives `kapanış` with a DOTTED i, because invariant lowercasing maps I→i. The text
     * on screen has the DOTLESS ı. So every Turkish word containing I/ı vanished from search the moment the user
     * typed it in capitals — and caps lock and mobile auto-capitalisation make that ordinary, not exotic. The
     * user reads it as "search is broken" and has no way to find out why.
     *
     * Two steps, both locale-INDEPENDENT:
     *   1. NFD + strip combining marks — folds ş→s, ü→u, é→e, й→и. This is also why `kapanis` (typed without
     *      any Turkish characters at all) now finds `kapanış`, which was the second half of the report.
     *   2. Map the whole I family (I İ ı i and dotted-İ's decomposed form) to plain `i`, because step 1 alone
     *      still leaves I and ı on opposite sides.
     *
     * NOT toLocaleLowerCase('tr'): that fixes Turkish by breaking everyone else — in a seven-language product it
     * would make `I` unfindable for the English, French and German readers. Folding is symmetric; a Turkish
     * lowercase is a preference imposed on six other languages.
     */
    const foldForSearch = (value) => String(value == null ? '' : value)
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')   // combining marks: ş→s, ü→u, é→e
        .replace(/[\u0130\u0131I]/g, 'i')   // İ, ı, I → i (the dotless/dotted split step 1 cannot close)
        .toLowerCase();


    /*
     * A CHIP, and the half of it that only a sighted reader was getting.
     *
     * `title` carries the RICHER sentence — the source chip's title is module · type · id where the visible text
     * is just the module name — and a `title` attribute is a mouse affordance: it needs a hover, so a screen
     * reader, a keyboard and a touch device all get the short form and never learn the long one.
     *
     * `role="img"` is not decoration here, it is what makes `aria-label` apply at all: on a bare <span> the
     * implicit role is `generic`, and ARIA does not expose a label on a generic element — the attribute would be
     * written, look right in the DOM, and be dropped by the accessibility tree. With the role, the label REPLACES
     * the visible text for AT, which is the intent: the long sentence contains the short one.
     *
     * Only when a title exists. A chip that says all it has to say stays plain text, which reads better than an
     * image role wrapped around a word.
     */
    const chip = (kind, icon, text, title) =>
        `<span class="wcn-chip wcn-chip-${kind}"${title ? ` title="${esc(title)}" role="img" aria-label="${esc(title)}"` : ''}>` +
        `<i class="bx ${icon}" aria-hidden="true"></i><span>${esc(text)}</span></span>`;

    /*
     * WHOSE seat the reader is in — and NOTHING when that is unknown.
     *
     * MEASURED LIVE: this chip rendered as an icon with an empty label on every real task. `viewerRole` is not
     * a projection field, so `t(undefined)` returned "" and the chip said nothing at all. A chip with no text
     * is worse than no chip: it looks like a label that failed to load.
     *
     * Where the answer IS knowable it is now derived rather than invented — the projection marks the caller on
     * `assignee`/`requester` (`isCurrentUser`), and mock-data turns that into a role before the person objects
     * are flattened to names. Where it is not knowable, this returns nothing.
     */
    const roleChip = (item) => {
        const key = ROLE_KEY[item.viewerRole];
        if (!key) { return ''; }
        return chip('role', 'bx-user-check', t(key));
    };

    const typeLabel = (item) => t(TYPE_KEY[item.itemType] || item.itemType);
    // normalizedStatus is already resolved by the provider/aggregation projection.
    const displayStatus = (item) => item.status;
    const statusLabel = (item) => { const s = displayStatus(item); return t(STATUS_KEY[s] || s); };
    const priorityLabel = (item) => t(PRIORITY_KEY[item.priority] || item.priority);

    /*
     * Priority is now a declared, optional projection field (BL-032, closed 2026-07-29): PRIORITIES =
     * Low|Medium|High, the engine's own spelling. It is still only rendered where it EXISTS — a provider that
     * does not rank its work omits the field, and an absent priority must stay absent rather than default to
     * Medium, which would tell the reader something nobody said.
     */
    const hasPriority = (item) => !!item && PRIORITY_KIND[item.priority] !== undefined;
    const priorityChip = (item) => (hasPriority(item)
        ? chip(PRIORITY_KIND[item.priority], 'bx-flag', priorityLabel(item))
        : '');

    /*
     * One line for the row chip's tooltip: the first blocker, in the same typed wording the detail banner uses.
     * Falls back to the generic sentence only when there is genuinely nothing more specific to say.
     */
    const blockedTooltip = (item) => {
        const first = (item.blockedState?.blockers || [])[0];
        if (!first) { return t('BlockedBanner'); }
        const key = BLOCKER_SENTENCE_KEY[first.dependencyType] || BLOCKER_CODE_SENTENCE_KEY[first.code];
        return key ? tf(key, first.labelText || '') : (first.labelText || t('BlockedBanner'));
    };

    const slaLabel = (item) => {
        const d = item.slaDiffDays;
        /*
         * BL-046 — FINISHED work is REPORTED, never counted down, and the server has the last word on it.
         *
         * Three things went wrong here in sequence, each on a live screen: the count kept climbing after the task
         * closed; then a half-fix made it read "-2 days LEFT"; then a negative guard sent every past-dated item
         * to "late" REGARDLESS OF STATE, so a task the projection called on-track — closed on time — was told it
         * was a day late. The screen contradicted the projection, which is worse than the drift it replaced.
         *
         * So the terminal branch comes first and answers on its own terms: the state is the server's (it froze it
         * at closing time), and the number comes from dueAt ↔ closedAt. The badge is KEPT — a late close is
         * exactly what reporting reads History for — it just stops moving.
         *
         * With no closing instant there is no frozen number to quote, so it states the fact without one.
         *
         * ZERO IS NOT A NUMBER WORTH QUOTING (CT live measurement, 2026-08-09). A task due 18:00 and closed 21:04
         * the SAME DAY is late — the server says overdue — but the day-granular difference floors to 0, and
         * "closed 0 days late" reads to a human as "not late". The state and the sentence disagreed again, which
         * is the exact failure this branch was written to end. So a sub-day overrun drops the number and states
         * the fact, reusing the label that already exists for the no-closing-instant case.
         */
        if (isTerminal(item)) {
            if (item.slaState === 'no-sla' || d == null) { return t('SlaNoSla'); }
            if (item.slaState !== 'overdue') { return t('SlaClosedOnTime'); }
            return item.closedAt && Math.abs(d) >= 1
                ? tf('SlaClosedLateByDays', Math.abs(d))
                : t('SlaClosedLate');
        }
        switch (item.slaState) {
            case 'overdue': return tf('SlaOverdueByDays', Math.abs(d));
            case 'due-soon':
                if (d === 0) { return t('SlaDueToday'); }
                if (d === 1) { return t('SlaDueTomorrow'); }
                return tf('SlaDueInDays', d);
            case 'on-track':
                /*
                 * The boundary the label never had, for work still in flight. `d` is derived from dueAt against
                 * TODAY, so an open task whose deadline has passed printed "-2 days left" — and d === 0 printed
                 * "0 days left". Neither is a sentence; "left" is the wrong word once the deadline is not in the
                 * future, whatever the state says.
                 *
                 * The overdue wording here does NOT contradict the server: this is LIVE work, and a deadline that
                 * has passed is a fact the reader can act on. The closed case — where overruling the projection
                 * WAS the defect — never reaches this switch; it returns above.
                 */
                if (d == null) { return t('SlaNoSla'); }
                if (d < 0) { return tf('SlaOverdueByDays', Math.abs(d)); }
                if (d === 0) { return t('SlaDueToday'); }
                return tf('SlaDueInDays', d);
            default: return t('SlaNoSla');
        }
    };

    /*
     * "How long ago", measured AT RENDER TIME from an absolute timestamp.
     *
     * It used to take a pre-computed day count. Whoever computed it — the mapper, or worse the server — froze it:
     * a tab left open overnight, or a cached projection, would still say "today". This is the same class of defect
     * as the frozen showcase date, and the cure is the same: keep the absolute instant, derive the words late.
     *
     * The clock is provenance-aware, so showcase entries are measured from the showcase's date and real ones from
     * the real today.
     */
    const agoLabel = (atMs, provenance) => {
        if (atMs === null || atMs === undefined) { return ''; }
        const reference = data.referenceDate(provenance);
        const days = Math.max(0, Math.round((reference - atMs) / 86400000));
        if (days === 0) { return t('TimeToday'); }
        if (days === 1) { return t('TimeYesterday'); }
        return tf('TimeDaysAgo', days);
    };

    /*
     * A PERSON'S MONOGRAM — the same rule the assignee picker uses (Tasks/form.js personInitials), because a
     * person has to look like the same person wherever they appear.
     *
     * Two or more words take the first and last initial; a single word takes its first two characters, which is
     * what keeps "Ayşe" from rendering as a lone "A" beside "AT". Locale-aware upper-casing, so Turkish dotted
     * and dotless i do not swap places.
     *
     * ⚠ The picker's copy still lives in Tasks/form.js — a different bundle with no export seam. Consolidating
     * them is raised as its own item rather than smuggled into this round; what matters here is that app.js has
     * ONE algorithm rather than a second improvised one.
     */
    const personInitials = (name) => {
        const words = String(name || '').trim().split(/\s+/).filter(Boolean);
        if (!words.length) { return '?'; }
        const raw = words.length > 1
            ? words[0].charAt(0) + words[words.length - 1].charAt(0)
            : words[0].slice(0, 2);
        return raw.toLocaleUpperCase();
    };

    /*
     * WHAT AN EVENT SAYS, in the reader's language.
     *
     * The server sends a CODE (`planned`, `released`, `reassigned`), never a sentence — one composed on the
     * server would be composed in one language, and this product ships seven. The code becomes a resource key
     * here, where the seven live.
     *
     * `eventKey` is the older fixture/optimistic shape and still resolves: the showcase fixtures and the local
     * "you just pressed this" entries write it, and dropping it would blank every row on those surfaces.
     *
     * A code this shell does not know falls back to a generic line rather than printing the token — a raw key or
     * a bare `submittedForReview` reaching a user is a defect this codebase has shipped before. The test for
     * "known" is the CONTRACT's vocabulary, not whether the resx happens to hold the string: a missing
     * translation is a gap to fix, while an unrecognised code is a server ahead of this shell, and the two must
     * not be diagnosed by the same check.
     */
    const KNOWN_EVENT_CODES = new Set(
        (global.WorkCenterNextContract && global.WorkCenterNextContract.enums
            && global.WorkCenterNextContract.enums.ACTIVITY_EVENT_CODES) || []);

    const eventSentence = (entry) => {
        if (entry.eventKey) {
            return entry.eventKey === 'AuditActionStamp'
                ? tf('AuditActionStamp', entry.actionLabel)
                : t(entry.eventKey);
        }

        /*
         * A FIELD EDIT SAYS WHAT CHANGED, not merely that something did (2026-08-23). An `edited` entry whose
         * sentence were only "the task was edited" would answer the question this feature exists for — "who
         * changed the due date?" — with "somebody changed something".
         *
         * Any OTHER act may carry field changes too (a reassign moves the assignee field), and those keep their
         * own verb: "Başkasına atandı" is the act, and the field row would only repeat it.
         */
        const changes = entry.event && entry.event.fieldChanges;
        if (entry.event && entry.event.code === 'edited' && changes && changes.length) {
            return fieldChangeSentence(changes);
        }

        const code = entry.event && entry.event.code;
        if (!code || !KNOWN_EVENT_CODES.has(code) || code === 'unknown') { return t('AuditEventUnknown'); }
        return t('AuditEvent' + code.charAt(0).toLocaleUpperCase('en') + code.slice(1));
    };

    /*
     * ── WHAT AN EDIT CHANGED, AS ONE SENTENCE ────────────────────────────────────────────────────────────────
     *
     * TWO SHAPES, and the split is measured against what a reader actually needs:
     *
     *   ONE field  → the field AND both values: "Son tarih: 2026-08-15 → 2026-08-20". This is the common case
     *                and the values are the whole answer.
     *   SEVERAL    → the field NAMES only: "Son tarih, öncelik ve başlık değiştirildi". Four before/after pairs
     *                on one line is a paragraph, not a row — and the reader's first question is which fields,
     *                not what each became. The values stay one click away in the record.
     *
     * ⚠ THE LIST IS BUILT BY `Intl.ListFormat`, not by joining with a comma. Turkish ends a list with "ve",
     * English with "and", Arabic with "و" and no space before it — a hard-coded separator is the fragment
     * assembly this project has banned twice. Each SENTENCE is a whole pattern per language too; only the list
     * itself is composed, and by the platform's own localized formatter.
     */
    const fieldChangeSentence = (changes) => {
        const names = changes.map(fieldChangeName);

        if (changes.length === 1) {
            const only = changes[0];
            // Values are shown only when there are values to show: a long value was never recorded, and a
            // redacted one must not be.
            if (!only.redacted && !only.valuesOmitted && (only.from || only.to)) {
                return tf('AuditFieldChangeValued', names[0], only.from || t('AuditFieldEmpty'),
                    only.to || t('AuditFieldEmpty'));
            }
            return tf('AuditFieldChangeNamed', names[0]);
        }

        return tf('AuditFieldChangeNamed', formatList(names));
    };

    /*
     * The field's NAME in the reader's language — a built-in field from the resx, a tenant field from its own
     * label, and a field the reader may not see from neither.
     *
     * ⚠ A REDACTED CHANGE CONTRIBUTES A GENERIC WORD rather than being dropped from the list. Dropping it would
     * make one reader see "two fields changed" and another "three", from the same record — and the shorter list
     * would be a quieter lie than the honest "and one more field".
     */
    const fieldChangeName = (change) => {
        if (change.redacted || !change.field) { return t('AuditFieldHidden'); }
        if (change.field === 'customField') {
            return data.resolveLabel(change.label) || t('AuditFieldHidden');
        }
        return t('AuditField' + change.field.charAt(0).toLocaleUpperCase('en') + change.field.slice(1));
    };

    /*
     * A localized LIST. `Intl.ListFormat` is what knows that Turkish wants "ve" before the last item and English
     * wants "and"; falling back to a plain join only when the runtime has no such formatter, where a comma is
     * less wrong than a crash.
     */
    const formatList = (items) => {
        try {
            return new Intl.ListFormat(global.CurrentLanguage || undefined, { style: 'long', type: 'conjunction' })
                .format(items);
        } catch (error) {
            return items.join(', ');
        }
    };

    // actions[] is the single effective command projection. The browser never
    // derives eligibility from lifecycle, permission, blockers or system state.
    const itemActions = (item) => data.getActions(item);
    /*
     * Which action LEADS a row.
     *
     * The projected primary wins even when it is DISABLED. Why a piece of work cannot move is the most important
     * thing on its row, and skipping a disabled primary hid "waiting for approval" in the ··· menu and promoted
     * `cancel` into the lead instead — so an approval-blocked task read as "the only thing I can do is call this
     * off". The disabled button carries its own reason (disabledReason), which is the message the user needs.
     *
     * A destructive action is never promoted by the fallback. It stays available in the menu, where choosing it
     * takes a deliberate second click, but it must not become the leading button merely because everything else
     * is disabled.
     */
    const rowPrimaryAction = (actions) =>
        actions.find((action) => action.primary)
        || actions.find((action) => !action.disabled && !action.destructive)
        || null;

    /// The reason the row's leading action is unusable, or null when it is usable.
    const blockedPrimaryReason = (item) => {
        const primary = rowPrimaryAction(itemActions(item));
        return primary && primary.disabled && primary.disabledReason ? primary.disabledReason : null;
    };

    const primaryAction = (item) => {
        const actions = itemActions(item);
        const primaryCode = item._fixture?.primaryActionCode || item.primaryActionCode || null;
        return actions.find((candidate) => candidate.code === primaryCode) || actions[0] || null;
    };
    const actionByKey = (item, key) => itemActions(item).find((a) => a.key === key) || null;
    const actionByRole = (item, role) => itemActions(item).find((a) => a.role === role) || null;
    const actionLabel = (action) => action?.displayLabel || (action?.labelKey ? t(action.labelKey) : '');
    const markSeen = (item) => {
        if (!item) { return; }
        item.isUnread = false;
        if (item.personal) { item.personal.seen = true; }
        seenIds.add(item.id);
        persistSeenIds(seenIds);
    };
    const markTriggerSeen = (trigger) => {
        if (!trigger) { return; }
        trigger.isUnread = false;
        seenIds.add(trigger.id);
        persistSeenIds(seenIds);
    };

    // ── Timesheet helpers (task work loop, live browser clock) ────────────────
    const foldTimer = (item) => {
        const ts = item.timesheet;
        if (ts && ts.running && ts.startedAt) {
            ts.loggedMinutes += (Date.now() - ts.startedAt) / 60000;
            ts.running = false; ts.startedAt = null;
        }
    };
    const formatMinutes = (mins) => {
        const total = Math.max(0, Math.floor(mins));
        return tf('TimeHM', Math.floor(total / 60), total % 60);
    };
    const formatSegment = (ms) => {
        const s = Math.max(0, Math.floor(ms / 1000));
        const mm = String(Math.floor(s / 60)).padStart(2, '0');
        const ss = String(s % 60).padStart(2, '0');
        return `${mm}:${ss}`;
    };

    // ── Filtering / ordering ──────────────────────────────────────────────────
    // N-way scope (spec v3 §6): 'mine' = my own · a delegator name = that person's ·
    // 'all' = combined (mine + every delegation), with per-row "X adına" badges.
    const itemInScope = (item) =>
        state.scope === 'all' ? true
            // BL-023 — the TEAM scope is not a delegation filter over the same rows: the server already
            // answered with a different set (my subordinates' work). Filtering it by `delegator` here would
            // hide every one of them, because none of them is delegated to me.
            : state.scope === 'team' ? true
                : state.scope === 'mine' ? !item.delegator
                    : item.delegator === state.scope;
    // ONE definition of "closed", owned by the data module. It used to be duplicated here, and the action filter
    // getActions now applies (BL-038) depends on this surface agreeing with it exactly — two copies would drift.
    /*
     * IS THIS TASK PARKED RIGHT NOW?
     *
     * ⚠ `>=`, NOT `>` (BL-182, measured 2026-08-23). Once the picker was allowed to accept TODAY, the four
     * places that asked this question with `>` all answered "no" for a snooze the server had just accepted and
     * stored (measured: overlay `2026-08-23T20:59:59Z` on the wire, no row, no chip, no banner on screen). A
     * snooze until today runs to 23:59 of that day — it is parked, and the screen has to say so.
     *
     * A snooze that has genuinely expired never reaches here: the provider projects `snoozedUntil` only while it
     * is still in the future, so a stale date arrives as null rather than as a date to compare.
     */
    const isSnoozed = (item) => !!item.snoozedUntil && item.snoozedUntil >= data.todayIso;

    const isTerminal = (item) => data.isTerminal(item);
    const inTab = (item, tab) => item.catalogVisible !== false && !item.dismissed && itemInScope(item)
        && (tab === 'history' ? isTerminal(item) : item.tab === tab && !isTerminal(item));
    /*
     * TAB counters ignore in-tab filters, and still should (BL-045 narrowed this claim rather than deleting it).
     * A tab badge is a claim about that tab's whole load and is read from the OTHER tabs, where the current
     * tab's chip or search box means nothing. The SEGMENT counters below no longer ignore them — that sentence
     * used to cover both, and covering both is what let the chip and the segment bar describe two different
     * populations at once.
     */
    /*
     * ⚠ A PARKED ITEM IS NOT COUNTED HERE, and the badge is where that subtraction belongs — NOT in `inTab`.
     * `inTab` feeds `tabItems()`, which is where the chips do their counting; hiding there would blind the
     * "Ertelenmiş" chip to the very rows it exists to reveal (measured: chip 0, three rows unreachable).
     *
     * A badge says "work waiting for you in this tab". Work you parked is not waiting for you — you decided
     * that. So the screen shows 16 rows, a badge of 16, and a chip that says "Ertelenmiş (3)": two different
     * populations, correctly. The chip does not claim "3 of the 16 above"; it says "3 things you put away".
     */
    const tabCount = (tab) => state.items.filter((item) => inTab(item, tab)
        && !(SNOOZE_TABS.indexOf(tab) >= 0 && isSnoozed(item))).length
        + (tab === 'inbox' ? state.triggers.length : 0);

    // Items in the current tab, before any in-tab filter — the population every in-tab counter starts from.
    const tabItems = () => state.items.filter((item) => inTab(item, state.tab));

    /*
     * BL-045 — FACETED counters. Each counter applies every OTHER in-tab filter and never its own axis, so the
     * number a reader sees and the list they can reach describe the same population.
     *
     * THE DEFECT: the "SLA riski" chip said 3, clicking it produced 2 rows, and the segment counters did not
     * move — the third at-risk item was sitting in Bekleyen with nothing on screen saying so.
     *
     * The rule is deliberately NOT symmetric, and the asymmetry is the product decision (CT):
     *   SEGMENT counters recompute under the chips — "SLA riski 3, and 1 of them is in Bekleyen".
     *   CHIP counters stay independent of the SEGMENT. Narrowing the chip to the active segment was the
     *   rejected alternative: a signal is an axis of its own, and folding it under status hides exactly the
     *   item the reader is hunting for.
     *
     * All the in-tab counters go through here, on purpose. They used to be three separate expressions, and a
     * half-faceted set would only move today's inconsistency to the chip next door.
     */
    const facetItems = (except) => tabItems().filter((item) => passesFilters(item, except));
    const segmentCount = (seg) => facetItems().filter((i) => data.segmentFor(i) === seg).length;
    const typeCount = (ty) => facetItems('type').filter((i) => i.itemType === ty).length
        + (state.tab === 'inbox' && ty === 'meetingInvite' ? state.triggers.length : 0);
    const signalCount = (sig) => facetItems('signal').filter((i) => SIGNAL_TEST[sig](i)).length;
    // The "Tümü" chip is the type axis's own zero state, so it counts the same population its siblings do —
    // left on the raw tab total it would have disagreed with the chips beside it the moment any filter was on.
    const allTypesCount = () => facetItems('type').length + state.triggers.length;

    /*
     * Advanced filters shared by list + kanban + calendar (priority, mode, group, module, search) — everything
     * except the tab-specific segment filter.
     *
     * `except` names ONE axis to skip, for the faceted counters above: a facet never applies its own filter, or
     * an active chip would count only itself and the reader could never see what turning it off would restore.
     */
    const passesFilters = (item, except) => {
        if (except !== 'type' && state.typeFilter.size && !state.typeFilter.has(item.itemType)) { return false; }
        if (except !== 'signal' && state.signalFilter.size) {
            for (const sig of state.signalFilter) { if (!SIGNAL_TEST[sig](item)) { return false; } }
        }
        /*
         * ── A PARKED ITEM IS NOT ON SCREEN (BL-181) ──────────────────────────────────────────────────────────
         *
         * Hidden HERE and not in `inTab`, on purpose: `facetItems('signal')` skips the whole signal axis, so the
         * chip counts exactly what this line hides. Put the same test in `inTab` and the chip would read 0 while
         * three items sat behind it — a door with no handle.
         *
         * The `snoozed` chip being ON is what opens them, and the loop above has already done that half: with
         * `snoozed` in the filter set, only snoozed items pass it. So this line only has to answer the OTHER
         * case — nobody asked for them, so they stay parked.
         */
        if (except !== 'signal' && !state.signalFilter.has('snoozed')
            && SNOOZE_TABS.indexOf(state.tab) >= 0 && isSnoozed(item)) {
            return false;
        }
        if (state.moduleFilter.length && !state.moduleFilter.includes(item.sourceModule)) { return false; }
        if (state.priorityFilter !== 'all' && item.priority !== state.priorityFilter) { return false; }
        if (state.modeFilter !== 'all' && item.assignmentMode !== state.modeFilter) { return false; }
        if (state.slaFilter.length && !state.slaFilter.includes(item.slaState)) { return false; }
        if (state.pinnedFilter && !item.pinned) { return false; }
        if (state.tab === 'havuz' && state.group !== 'all') {
            // The unnamed bucket matches items with NO queue name, which no plain string comparison can express.
            const matches = state.group === GROUP_UNNAMED ? !item.group : item.group === state.group;
            if (!matches) { return false; }
        }
        // BOTH sides folded the same way — folding only the needle would leave the haystack unmatchable.
        const q = foldForSearch(state.search.trim());   // ignore leading/trailing space
        if (q) {
            const hay = foldForSearch(
                item.title + ' ' + item.summary + ' ' + item.sourceModule + ' ' + item.sourceId + ' ' + item.requester);
            if (!hay.includes(q)) { return false; }
        }
        return true;
    };

    const activeItems = () => state.items.filter((item) => {
        if (!inTab(item, state.tab)) { return false; }
        if (SEGMENTS[state.tab] && data.segmentFor(item) !== state.segment) { return false; }
        return passesFilters(item);
    });

    const activeTriggers = () => {
        if (state.tab !== 'inbox' || state.signalFilter.size) { return []; }
        if (state.typeFilter.size && !state.typeFilter.has('meetingInvite')) { return []; }
        // Trigger-only invitations do not carry task priority or assignment mode.
        if (state.priorityFilter !== 'all' || state.modeFilter !== 'all') { return []; }
        // Same fold as the item search (BL-044) — a meeting invitation must not be findable by different rules
        // from the task beside it.
        const query = foldForSearch(state.search.trim());
        return state.triggers.filter((trigger) => {
            const provider = trigger.source?.providerCode || '';
            if (state.moduleFilter.length && !state.moduleFilter.includes(provider)) { return false; }
            const title = data.resolveLabel(trigger.title);
            const summary = data.resolveLabel(trigger.summary);
            return !query || foldForSearch(`${title} ${summary} ${provider}`).includes(query);
        });
    };

    const bySla = (a, b) => {
        if (a.escalated && !b.escalated) return -1;
        if (!a.escalated && b.escalated) return 1;
        return SLA_ORDER.indexOf(a.slaState) - SLA_ORDER.indexOf(b.slaState);
    };

    const moduleOptions = () => {
        const set = [];
        tabItems().forEach((item) => { if (set.indexOf(item.sourceModule) < 0) { set.push(item.sourceModule); } });
        if (state.tab === 'inbox') {
            state.triggers.forEach((trigger) => {
                const provider = trigger.source?.providerCode;
                if (provider && set.indexOf(provider) < 0) { set.push(provider); }
            });
        }
        return set.sort();
    };

    // ── Toolbar ───────────────────────────────────────────────────────────────
    // Icon-only view button (like the legacy WorkCenter view-switch): tooltip via
    // title, no text label. Active = filled primary; inactive = outline.
    const viewBtn = (view, icon, labelKey) => {
        const active = state.view === view;
        return `<button type="button" class="btn btn-icon ${active ? 'btn-primary' : 'btn-outline-secondary'} wcn-viewbtn" data-wcn-view="${view}" aria-label="${esc(t(labelKey))}" aria-pressed="${active}">` +
            `<i class="icon-base bx ${icon}"></i></button>`;
    };

    // My own items still needing action (overdue) — surfaced even while acting on
    // someone else's behalf so urgent personal work is never hidden (spec v3 §6).
    const ownUrgentCount = () => state.items.filter((i) =>
        !i.dismissed && !i.delegator && i.slaState === 'overdue'
        && i.lifecycle !== 'Done' && i.lifecycle !== 'Cancelled').length;

    const delegatorByName = (name) => data.delegators.find((d) => d.name === name) || null;

    const buildHeader = () => {
        const urgent = ownUrgentCount();
        // Current scope → the person/delegation dropdown label.
        const scopeLabel = state.scope === 'mine' ? t('ScopeMine')
            : state.scope === 'all' ? t('ScopeAll')
                : state.scope === 'team' ? t('ScopeTeam')
                    : tf('OnBehalfShort', state.scope);
        const scopeIcon = (k) => k === 'mine' ? 'bx-user'
            : k === 'all' ? 'bx-layer'
                : k === 'team' ? 'bx-group' : 'bx-user-voice';
        const ownBadge = (state.scope !== 'mine' && urgent)
            ? `<span class="wcn-own-urgent" title="${esc(t('OwnUrgentTip'))}">${urgent}</span>` : '';
        /*
         * `disabled` carries its own REASON in the subtitle. Hiding the row was rejected: a hidden control
         * cannot explain its absence, so a manager who expects a team reads the feature as missing rather than
         * their org chart as empty. Disabled + a sentence is the only variant that answers the real question.
         */
        const scopeItem = (key, label, sub, disabled) =>
            `<li><button type="button" class="dropdown-item wcn-dd-item${state.scope === key ? ' active' : ''}" data-wcn-scope="${esc(key)}"${disabled ? ' disabled aria-disabled="true"' : ''}>
                <i class="bx ${scopeIcon(key)}"></i><span>${esc(label)}</span>${sub ? `<small class="wcn-dd-sub">${esc(sub)}</small>` : ''}
            </button></li>`;
        /*
         * With no delegation data there is nothing to scope BETWEEN: "Tümü" would differ from "Kendim" only by
         * including work that cannot exist, and the delegator entries named people who are not the user's
         * colleagues. So the menu collapses to the one honest option. The branch below stays wired on purpose —
         * the day a provider fills data.delegators, the full selector returns with no further change here.
         */
        const delegations = data.delegators;
        const delegatorItems = delegations.map((d) => scopeItem(d.name, tf('OnBehalfShort', d.name), d.title)).join('');
        // "+ Yeni" create menu — WorkCenter owns task/note/meeting; module items
        // (issue/approval) are born in the source (spec v3 §5, note/meeting rule).
        const createItem = (val, icon, label) =>
            `<li><button type="button" class="dropdown-item wcn-dd-item" data-wcn-new="${val}"><i class="bx ${icon}"></i><span>${esc(label)}</span></button></li>`;

        return `<div class="d-flex flex-column flex-md-row justify-content-md-between align-items-md-center gap-3 mb-3 wcn-header">
            <div class="wcn-header-title">
                <h5 class="mb-0">${esc(t('Title'))}</h5>
                <p class="mb-0 text-muted">${esc(t('Subtitle'))}</p>
            </div>
            <div class="d-flex align-items-center gap-2 flex-shrink-0 wcn-header-actions">
                <div class="dropdown">
                    <button type="button" class="btn btn-label-secondary dropdown-toggle" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${esc(t('ScopeLabel'))}">
                        <i class="icon-base bx ${scopeIcon(state.scope === 'mine' || state.scope === 'all' ? state.scope : 'delegator')} icon-sm me-1"></i><span>${esc(scopeLabel)}</span>${ownBadge}
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end wcn-dd-menu">
                        ${scopeItem('mine', t('ScopeMine'), data.currentUser.title)}
                        ${/* BL-023 — my subordinates' OWN work, including what I never assigned. Always
                              listed: when nobody reports to the user it is disabled and SAYS SO, because a
                              missing row and a missing feature are indistinguishable to the reader. */ ''}
                        ${scopeItem(
                            'team',
                            t('ScopeTeam'),
                            state.team.hasTeam ? tf('ScopeTeamCount', state.team.memberCount) : t('ScopeTeamEmpty'),
                            !state.team.hasTeam)}
                        ${delegations.length ? `${delegatorItems}
                        <li><hr class="dropdown-divider"></li>
                        ${scopeItem('all', t('ScopeAll'), t('ScopeAllSub'))}` : ''}
                    </ul>
                </div>
                <div class="dropdown">
                    <button type="button" class="btn btn-primary shadow-none dropdown-toggle" data-bs-toggle="dropdown" aria-expanded="false">
                        <i class="icon-base bx bx-plus icon-sm me-1"></i><span>${esc(t('NewButton'))}</span>
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end wcn-dd-menu">
                        ${createItem('task', 'bx-task', t('NewSelfTask'))}
                        ${/*
                           * ⚠ "HIZLI NOT" AND "TOPLANTI PLANLA" WERE REMOVED, NOT DISABLED (2026-08-24).
                           *
                           * MEASURED: both wrote to browser memory and nowhere else — `state.notes.unshift(...)`
                           * and `state.meetings.push(...)`, no API call anywhere, and `state` starts them as `[]`
                           * and never loads them. Everything either one produced vanished on the next reload.
                           *
                           * DISABLING WAS REJECTED, deliberately: a disabled control with no reason beside it is
                           * the exact defect this session already filed (BL-208), and there is no answer to
                           * "when, then?" to put in one. A promise nobody can keep is worse than an absence.
                           *
                           * ⚠ NOT THE SAME THING, AND STILL HERE: the detail page's PERSONAL NOTE card writes
                           * through `TasksApi.addPersonalNote` (real), and the "Onay toplantısı planla" ACTION
                           * has a contract behind it (`reviewMeetingPolicy`). Neither was touched.
                           */ ''}
                        <li><hr class="dropdown-divider"></li>
                        ${createItem('source', 'bx-link-external', t('NewInSource'))}
                    </ul>
                </div>
            </div>
        </div>`;
    };

    // Prominent banner while in a delegation scope (spec v3 §6) — scoped grant, not
    // impersonation; every action is stamped "X adına". Combined view = lighter note.
    const buildDelegationBanner = () => {
        if (state.scope === 'mine') { return ''; }
        if (state.scope === 'all') {
            return `<div class="wcn-delegation wcn-delegation-all" role="note">
                <i class="bx bx-layer"></i><span class="wcn-delegation-text">${esc(t('DelegationCombined'))}</span>
            </div>`;
        }
        const d = delegatorByName(state.scope);
        if (!d) { return ''; }
        return `<div class="wcn-delegation" role="note">
            <i class="bx bx-user-voice"></i>
            <span class="wcn-delegation-text">${esc(tf('DelegationBanner', d.name, d.title))}</span>
        </div>`;
    };

    // Havuz group-queue selector (spec v3) — filter the pool by team queue.
    /*
     * WHICH queue — the Pool tab's whole question (WC-3 / BL-031). The queue names come from the projection's
     * `pool.label`; nothing is synthesized, so a tab whose items name no queue renders no selector at all.
     *
     * Collected from the items IN THIS TAB rather than from every item on the surface. A claimed pool task keeps
     * assignmentMode "groupQueue" and its pool identity — correctly, since that is how it arrived — but it lives
     * under İşlerim now, and listing its queue here would offer a filter that matches nothing in the Pool tab.
     */
    /*
     * The sentinel for "this queue has no name".
     *
     * A plain STRING, not a Symbol: `state.group` is mirrored into the query string by syncUrl, and a Symbol
     * cannot be converted to one — searchParams.set throws, the whole render dies, and the click looks like it
     * did nothing. It is also never written into `data-wcn-group` (the unnamed button carries its own
     * `data-wcn-group-unnamed` attribute instead), so attribute encoding cannot mangle it. A real position label
     * is "{position} — {unit}"; this value colliding with one would only mis-target a filter, never lose a row.
     */
    const GROUP_UNNAMED = '__wcn-unnamed';
    const buildGroupSelector = () => {
        if (state.tab !== 'havuz') { return ''; }
        const inTabItems = tabItems();
        const groups = [];
        inTabItems.forEach((i) => { if (i.group && groups.indexOf(i.group) < 0) { groups.push(i.group); } });
        // Nothing to choose between: with no NAMED queue there is no distinction a selector could offer, so the
        // tab shows none rather than a lone "all" button that filters nothing.
        if (!groups.length) { return ''; }
        const btn = (key, label, extraAttr) =>
            `<button type="button" class="wcn-seg${state.group === key ? ' active' : ''}" data-wcn-group="${esc(String(key))}"${extraAttr || ''}><span>${esc(label)}</span></button>`;
        // A pooled item whose position could not be read has an identity but no name. Beside named queues it
        // still needs to be reachable, so it gets its own bucket rather than only "all".
        const unnamedBtn = inTabItems.some((i) => !i.group)
            ? `<button type="button" class="wcn-seg${state.group === GROUP_UNNAMED ? ' active' : ''}" data-wcn-group-unnamed><span>${esc(t('GroupUnnamed'))}</span></button>`
            : '';
        return `<div class="wcn-segments" role="group" aria-label="${esc(t('GroupLabel'))}">
            ${btn('all', t('GroupAll'))}${groups.map((g) => btn(g, g)).join('')}${unnamedBtn}
        </div>`;
    };

    // Top-tabs = OWNERSHIP (spec v3): Gelen Kutusu · İşlerim · Havuz · Geçmiş.
    // Standard Sneat `nav nav-pills` (like the legacy WorkCenter), with icon +
    // count. Inbox is default so new work is seen on open. Click drives state
    // (data-wcn-tab) + re-render — not Bootstrap tab-panes.
    const TAB_ICON = { inbox: 'bx-envelope', islerim: 'bx-briefcase-alt-2', havuz: 'bx-collection', history: 'bx-history' };
    // Views are tab-appropriate, not all six everywhere: Inbox = triage (list/
    // split/table), İşlerim = full work management, Havuz = claim list, Geçmiş =
    // read-only archive. Kanban/Calendar/Bugün only make sense for active work.
    const VIEW_META = { list: 'bx-list-ul', split: 'bx-columns', table: 'bx-table', kanban: 'bx-grid-alt', calendar: 'bx-calendar', focus: 'bx-target-lock' };
    const VIEW_KEY = { list: 'ViewList', split: 'ViewSplit', table: 'ViewTable', kanban: 'ViewKanban', calendar: 'ViewCalendar', focus: 'ViewFocus' };
    // Split / Kanban / Calendar are deferred to the backlog (BL-*) — the row detail is
    // now its own page (openDetailPage → /WorkCenterNext/Details/{id}), so the in-app
    // split panel is retired. Remaining views: List · Table · (Focus in İşlerim).
    const TAB_VIEWS = {
        inbox: ['list', 'table'],
        islerim: ['list', 'table', 'focus'],
        havuz: ['list', 'table'],
        history: ['list', 'table']
    };
    const buildTabs = () => {
        const tab = (key) => {
            const active = state.tab === key;
            const cnt = tabCount(key);
            const countBadge = cnt > 0
                ? `<span class="badge rounded-pill bg-danger wcn-tab-count position-absolute top-0 start-100 translate-middle">${cnt}</span>`
                : '';
            return `<li class="nav-item" role="presentation">
                <button type="button" id="wcn-tab-${key}" class="nav-link border shadow-none wc-tab-compact d-inline-flex align-items-center${active ? ' active' : ''}" data-wcn-tab="${key}" role="tab" aria-selected="${active}" aria-controls="wcn-main-panel" tabindex="${active ? '0' : '-1'}">
                    <i class="bx ${TAB_ICON[key]} wc-tab-icon me-md-1"></i><span class="d-none d-md-inline">${esc(t(TAB_KEY[key]))}</span>
                    ${countBadge}
                </button>
            </li>`;
        };
        const allowedViews = TAB_VIEWS[state.tab] || TAB_VIEWS.islerim;
        const views = `<div class="d-flex align-items-center gap-2 ms-auto wcn-view-tools"><div class="btn-group btn-group-sm wcn-views" role="group" aria-label="${esc(t('ViewLabel'))}">${allowedViews.map((v) => viewBtn(v, VIEW_META[v], VIEW_KEY[v])).join('')}</div><div class="dropdown d-none d-lg-block"><button type="button" class="btn btn-icon btn-sm btn-label-secondary wcn-keyboard-help" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${esc(t('KeyboardHint'))}"><i class="bx bx-bxs-keyboard"></i></button><div class="dropdown-menu dropdown-menu-end wcn-keyboard-menu"><span>${esc(t('KeyboardHint'))}</span></div></div></div>`;
        return `<div class="card mb-3 wcn-tabcard">
            <div class="card-body p-3 d-flex align-items-center gap-3 flex-wrap">
                <ul class="nav nav-pills gap-2 flex-wrap mb-0 wcn-tabs" role="tablist" aria-label="${esc(t('TabsLabel'))}">
                    ${['inbox', 'islerim', 'havuz', 'history'].map(tab).join('')}
                </ul>
                ${views}
            </div>
        </div>`;
    };

    // Segment bar = STATUS (only İşlerim). Aktif / Bekleyen / Planlı — a status
    // change moves the segment, never the tab (Fable's law).
    const buildSegments = () => {
        const segs = SEGMENTS[state.tab];
        if (!segs) { return ''; }
        const btn = (seg) =>
            `<button type="button" class="wcn-seg${state.segment === seg ? ' active' : ''}" data-wcn-seg="${seg}">` +
            `<span>${esc(t(SEGMENT_KEY[seg]))}</span><span class="wcn-seg-count">${segmentCount(seg)}</span></button>`;
        return `<div class="wcn-segments" role="group" aria-label="${esc(t('SegmentsLabel'))}">${segs.map(btn).join('')}</div>`;
    };

    // ✕ affordance on a selected chip (click removes it — the chip is one button).
    const CHIP_X = '<i class="bx bx-x wcn-fchip-x" aria-hidden="true"></i>';
    // Inbox curated set (Task Center archetype): only first-decision categories.
    // task → "Kabul Bekleyen" (in the inbox every task awaits accept). Meeting
    // invitations share the triage surface but remain outside the task lifecycle:
    // accepting adds them to the agenda; declining removes the invitation.
    const INBOX_MAIN = [
        { key: 'approval', labelKey: 'TypeApproval', icon: TYPE_ICON_MAP.approval },
        { key: 'task', labelKey: 'ChipPendingAccept', icon: TYPE_ICON_MAP.task },
        { key: 'review', labelKey: 'TypeReview', icon: TYPE_ICON_MAP.review },
        { key: 'issue', labelKey: 'TypeIssue', icon: TYPE_ICON_MAP.issue },
        { key: 'exception', labelKey: 'TypeException', icon: TYPE_ICON_MAP.exception },
        { key: 'meetingInvite', labelKey: 'ChipMeetingInvite', icon: 'bx-calendar-event' }
    ];
    const INBOX_RISK = ['sla-risk', 'escalated'];   // "Bloke" is post-acceptance → not here

    // Inbox chips: single-select main types (Tümü clears), multi risk signals that
    // combine with a type ("Onay + SLA Riski"). Counts use the unfiltered inbox set.
    const buildInboxChips = () => {
        const allActive = state.typeFilter.size === 0;
        const allChip = `<button type="button" class="wcn-fchip${allActive ? ' active' : ''}" data-wcn-inbox-all aria-pressed="${allActive}">` +
            `<i class="bx bx-collection"></i><span>${esc(t('ChipAll'))}</span><span class="wcn-fchip-count">${allTypesCount()}</span></button>`;
        const mainChips = INBOX_MAIN.map((cfg) => {
            const on = state.typeFilter.has(cfg.key);
            const c = typeCount(cfg.key);
            // Inbox main chips are never dimmed at 0 (spec: no perpetual grey chips).
            return `<button type="button" class="wcn-fchip${on ? ' active' : ''}" data-wcn-inbox-type="${cfg.key}" aria-pressed="${on}">` +
                `<i class="bx ${cfg.icon}"></i><span>${esc(t(cfg.labelKey))}</span><span class="wcn-fchip-count">${c}</span>${on ? CHIP_X : ''}</button>`;
        }).join('');
        const riskChips = INBOX_RISK.map((sig) => {
            const c = signalCount(sig);
            const on = state.signalFilter.has(sig);
            if (!c && !on) { return ''; }   // secondary: hidden at 0 unless active
            return `<button type="button" class="wcn-fchip wcn-fchip-signal${on ? ' active' : ''}" data-wcn-sigchip="${sig}" aria-pressed="${on}">` +
                `<i class="bx ${SIGNAL_ICON[sig]}"></i><span>${esc(t(SIGNAL_KEY[sig]))}</span><span class="wcn-fchip-count">${c}</span>${on ? CHIP_X : ''}</button>`;
        }).join('');
        return `<div class="wcn-chips-types">${allChip}${mainChips}</div>` +
            (riskChips ? `<span class="wcn-chips-sep"></span><div class="wcn-chips-signals">${riskChips}</div>` : '');
    };

    // Default chips for İşlerim / Havuz / Geçmiş — unchanged multi-select type+signal.
    const buildDefaultChips = () => {
        const typeChip = (ty) => {
            const c = typeCount(ty);
            const on = state.typeFilter.has(ty);
            return `<button type="button" class="wcn-fchip${on ? ' active' : ''}${c ? '' : ' empty'}" data-wcn-typechip="${ty}" aria-pressed="${on}">` +
                `<i class="bx ${TYPE_ICON_MAP[ty]}"></i><span>${esc(t(TYPE_KEY[ty]))}</span><span class="wcn-fchip-count">${c}</span>${on ? CHIP_X : ''}</button>`;
        };
        const sigChip = (sig) => {
            const c = signalCount(sig);
            const on = state.signalFilter.has(sig);
            if (!c && !on) { return ''; }
            return `<button type="button" class="wcn-fchip wcn-fchip-signal${on ? ' active' : ''}" data-wcn-sigchip="${sig}" aria-pressed="${on}">` +
                `<i class="bx ${SIGNAL_ICON[sig]}"></i><span>${esc(t(SIGNAL_KEY[sig]))}</span><span class="wcn-fchip-count">${c}</span>${on ? CHIP_X : ''}</button>`;
        };
        // Only surface types actually present in this tab (or an active filter) — a row
        // of perpetual "0" type chips (İşlerim is task-dominant) is pure noise. The
        // segment bar already splits by state; chips here carry type + risk signals.
        const types = Object.keys(TYPE_KEY)
            .filter((ty) => typeCount(ty) > 0 || state.typeFilter.has(ty))
            .map(typeChip).join('');
        const signals = SIGNALS.map(sigChip).join('');
        return `<div class="wcn-chips-types">${types}</div>` +
            (signals ? `<span class="wcn-chips-sep"></span><div class="wcn-chips-signals">${signals}</div>` : '');
    };

    // Chips markup (Tümü/type/risk). Shared: List/Split render it inside the toolbar
    // band (chips left, search+filter right); Table renders it as a strip above the
    // grid (the grid owns its own toolbar), so chips never vanish on view switch.
    const chipsMarkup = () => {
        const chipsInner = state.tab === 'inbox' ? buildInboxChips() : buildDefaultChips();
        return `<div class="wcn-chips" role="group" aria-label="${esc(t('FilterType'))}">${chipsInner}</div>`;
    };
    const buildQuickFilters = () => {
        // Chips are a full-width strip above every view's toolbar — one clean line,
        // consistent across List/Split/Table, and actually shorter than folding 7
        // chips next to the search box (which wraps to 3 lines).
        return `<div class="wcn-quickfilters">${chipsMarkup()}</div>`;
    };
    // One filter row: the segmented control (status, single-select) + the type/signal
    // chips (multi-select) side by side — segment in its own white pill-box, chips
    // OUTSIDE it (a gap, not inside). Saves a header row so the list sits higher; the
    // tab row (view-switcher) and the Table's own toolbar are untouched.
    const buildFilterRow = () => {
        return `<div class="wcn-filterbar">${buildSegments()}${buildGroupSelector()}${chipsMarkup()}</div>`;
    };

    const activeAdvancedFilterCount = () => {
        return (state.moduleFilter.length ? 1 : 0)
            + (state.priorityFilter !== 'all' ? 1 : 0)
            + (state.modeFilter !== 'all' ? 1 : 0)
            + (state.slaFilter.length ? 1 : 0)
            + (state.pinnedFilter ? 1 : 0);
    };

    const toggleTableFilter = () => {
        const panel = document.getElementById('wcnFilterCollapse');
        if (!panel) { return; }
        state.filtersOpen = !panel.classList.contains('show');
        if (state.filtersOpen) {
            mountPanelSelect2();
        }
        const button = document.querySelector('.dt-filter-btn');
        if (button) { button.setAttribute('aria-expanded', String(state.filtersOpen)); }
        if (global.bootstrap?.Collapse) {
            global.bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false }).toggle();
        } else {
            panel.classList.toggle('show', state.filtersOpen);
        }
    };

    const mountTableFilterHost = () => {
        const host = document.getElementById('wcnTableFilterHost');
        const filterButton = document.querySelector('.wcn-datatable-card .dt-filter-btn');
        const toolbarRow = filterButton?.closest('.dt-layout-row') || filterButton?.closest('.row');
        if (host && toolbarRow) { toolbarRow.insertAdjacentElement('afterend', host); }
    };

    const buildChips = () => {
        if (state.view === 'table') {
            return `<div id="wcnTableFilterHost" class="px-3"><div class="collapse${state.filtersOpen ? ' show' : ''}" id="wcnFilterCollapse"><div class="pt-0 pb-3 wcn-filter-panel dt-filter-host">${filterPanel()}</div></div></div>`;
        }
        // Persistent search (page-level, not per-view) with a clear affordance.
        const searchBox = `<div class="wcn-search wcn-search-inline">
            <i class="bx bx-search"></i>
            <input type="text" class="form-control shadow-none" data-wcn-search
                value="${esc(state.search)}" placeholder="${esc(t('SearchPlaceholder'))}" aria-label="${esc(t('SearchPlaceholder'))}">
            ${state.search ? `<button type="button" class="wcn-search-clear" data-wcn-search-clear aria-label="${esc(t('SearchClear'))}"><i class="bx bx-x"></i></button>` : ''}
        </div>`;
        // Type selection is already visible in the chip strip. The advanced-filter
        // badge therefore counts only module, priority and assignment mode.
        const filterCount = activeAdvancedFilterCount();
        const filterBtn = `<button type="button" class="btn btn-icon ${filterCount > 0 ? 'btn-label-primary' : 'btn-label-secondary'} dt-filter-btn position-relative wcn-filter-toggle" data-wcn-filter-toggle aria-controls="wcnFilterCollapse" aria-expanded="${state.filtersOpen}" title="${esc(t('FiltersLabel'))}" aria-label="${esc(t('FiltersLabel'))}">` +
            `<i class="icon-base bx bx-filter-alt icon-sm"></i>` +
            `${filterCount > 0 ? `<span class="badge badge-center rounded-pill bg-primary position-absolute top-0 end-0 translate-middle">${filterCount}</span>` : ''}</button>`;
        const pageLength = state.view === 'table'
            ? `<div class="dt-length"><label><span class="visually-hidden">${esc(t('RowsPerPage'))}</span><select class="form-select ms-0" data-wcn-page-length aria-label="${esc(t('RowsPerPage'))}">${[10, 25, 50, 100].map((size) => `<option value="${size}"${state.pageLength === size ? ' selected' : ''}>${size}</option>`).join('')}</select></label></div>`
            : '';
        const colVis = state.view === 'table' ? `<div class="dropdown">
            <button type="button" class="btn btn-icon btn-label-secondary dt-colvis-btn position-relative d-none d-md-inline-flex wcn-colvis" data-bs-toggle="dropdown" aria-expanded="false" title="${esc(t('ColumnVisibility'))}" aria-label="${esc(t('ColumnVisibility'))}"><i class="icon-base bx bx-show icon-sm"></i></button>
            <div class="dropdown-menu dropdown-menu-end wcn-colvis-menu">${[{ key: 'ColType', col: 1 }, { key: 'ColTitle', col: 2 }, { key: 'ColModule', col: 3 }, { key: 'ColStatus', col: 4 }, { key: 'ColPriority', col: 5 }, { key: 'ColSla', col: 6 }, { key: 'ColRequester', col: 7 }].map((c) => `<label class="dropdown-item"><input type="checkbox" class="form-check-input me-2" data-wcn-column="${c.col}"${state.tableColumnVisibility[c.col] ? ' checked' : ''}>${esc(t(c.key))}</label>`).join('')}</div>
        </div>` : '';
        return `<div class="wcn-workspace-toolbar">
                <div class="dt-layout-row row my-0 justify-content-between wcn-toolbar-row">
                    <div class="dt-layout-start col-md-auto me-auto">${pageLength}</div>
                    <div class="dt-layout-end col-md-auto ms-auto d-flex gap-md-4 justify-content-md-between justify-content-center gap-4 flex-wrap mt-0">
                        <div class="dt-search">${searchBox}</div>
                        <div class="dt-buttons dt-buttons-actions btn-group">${colVis}${filterBtn}</div>
                    </div>
                </div>
                <div class="collapse${state.filtersOpen ? ' show' : ''}" id="wcnFilterCollapse">
                    <div class="px-3 pt-0 pb-3 wcn-filter-panel dt-filter-host">${filterPanel()}</div>
                </div>
        </div>`;
    };

    // Every view applies filters immediately. WorkCenter is a triage surface; a
    // second Apply step adds weight without protecting a destructive operation.
    const applyFilterValue = (which, value) => {
        if (which === 'module') { state.moduleFilter = Array.isArray(value) ? value.slice() : (value && value !== 'all' ? [value] : []); }
        else if (which === 'priority') { state.priorityFilter = value || 'all'; }
        else if (which === 'mode') { state.modeFilter = value || 'all'; }
        else if (which === 'worktype') { state.typeFilter = new Set(Array.isArray(value) ? value : (value && value !== 'all' ? [value] : [])); }
        else if (which === 'sla') { state.slaFilter = Array.isArray(value) ? value.slice() : (value && value !== 'all' ? [value] : []); }
        else if (which === 'pinned') { state.pinnedFilter = !!value; }
        state.listPage = 0;
    };

    // Inbox type filtering belongs to the visible single-select chips. Other tabs
    // retain the multi-select work-type filter for their broader work lists.
    const filterPanel = () => {
        const draft = {
            module: state.moduleFilter.slice(),
            priority: state.priorityFilter,
            mode: state.modeFilter,
            worktype: Array.from(state.typeFilter)
        };
        const selectedModules = Array.isArray(draft.module) ? draft.module : [];
        const modOpts = moduleOptions().map((m) =>
            `<option value="${esc(m)}"${selectedModules.includes(m) ? ' selected' : ''}>${esc(m)}</option>`).join('');
        const wtSel = Array.isArray(draft.worktype) ? draft.worktype : (draft.worktype && draft.worktype !== 'all' ? [draft.worktype] : []);
        const wtLabel = (k) => k === 'meetingInvite' ? t('ChipMeetingInvite') : t(TYPE_KEY[k]);
        const wtOpts = ['task', 'approval', 'review', 'meetingInvite', 'issue', 'exception'].map((k) =>
                `<option value="${k}"${wtSel.includes(k) ? ' selected' : ''}>${esc(wtLabel(k))}</option>`).join('');
        // İş türü duplicates the visible type chips → hidden in İşlerim (kept in Havuz/Geçmiş
        // where the chips curate differently). Atama modu is dead in İşlerim (everything is
        // already owned) → hidden. SLA-durumu (replaces the removed headings) + Sabitli added.
        const hideWorktype = state.tab === 'inbox' || state.tab === 'islerim';
        const hideMode = state.tab === 'inbox' || state.tab === 'islerim';
        const slaSel = state.slaFilter.slice();
        const slaOpts = SLA_ORDER.map((k) => `<option value="${k}"${slaSel.includes(k) ? ' selected' : ''}>${esc(t(SLA_GROUP_KEY[k]))}</option>`).join('');

        return `<div class="dt-filter-bar d-flex flex-wrap align-items-center gap-3" id="wcnFilterPanel">
            ${hideWorktype ? '' : `<div class="filter-chip">
                <select class="form-select form-select-sm select2 wcn-select" multiple="multiple" data-wcn-filter="worktype" data-placeholder="${esc(t('FilterWorkType'))}" aria-label="${esc(t('FilterWorkType'))}">${wtOpts}</select>
            </div>`}
            <div class="filter-chip">
                <select class="form-select form-select-sm select2 wcn-select" multiple="multiple" data-wcn-filter="module" data-placeholder="${esc(t('FilterAllModules'))}" aria-label="${esc(t('FilterModule'))}">${modOpts}</select>
            </div>
            ${state.items.some(hasPriority) ? `<div class="filter-chip">
                <select class="form-select form-select-sm select2 wcn-select" data-wcn-filter="priority" data-placeholder="${esc(t('FilterAllPriorities'))}" aria-label="${esc(t('FilterPriority'))}">
                    <option value=""></option>
                    ${PRIORITY_ORDER.map((p) => `<option value="${p}"${draft.priority === p ? ' selected' : ''}>${esc(t(PRIORITY_KEY[p]))}</option>`).join('')}
                </select>
            </div>` : ''}
            ${state.tab === 'inbox' ? '' : `<div class="filter-chip">
                <select class="form-select form-select-sm select2 wcn-select" multiple="multiple" data-wcn-filter="sla" data-placeholder="${esc(t('FilterSlaStatus'))}" aria-label="${esc(t('FilterSlaStatus'))}">${slaOpts}</select>
            </div>`}
            ${hideMode ? '' : `<div class="filter-chip">
                <select class="form-select form-select-sm select2 wcn-select" data-wcn-filter="mode" data-placeholder="${esc(t('FilterAllModes'))}" aria-label="${esc(t('FilterMode'))}">
                    <option value=""></option>
                    ${['direct', 'approval', 'groupQueue', 'offered'].map((m) => `<option value="${m}"${draft.mode === m ? ' selected' : ''}>${esc(t(MODE_KEY[m]))}</option>`).join('')}
                </select>
            </div>`}
            ${state.tab === 'inbox' ? '' : `<label class="filter-chip d-inline-flex align-items-center gap-2 mb-0">
                <input type="checkbox" class="form-check-input mt-0" data-wcn-filter="pinned"${state.pinnedFilter ? ' checked' : ''} aria-label="${esc(t('FilterPinned'))}">
                <span>${esc(t('FilterPinned'))}</span>
            </label>`}
        </div>`;
    };

    // ── Row (shared by List / Split / Focus) ──────────────────────────────────
    const isBlocked = (item) => !!(item.blockedState && item.blockedState.blocked);
    const sourceTitle = (item) => [item.sourceModuleId, item.sourceModuleName, item.sourceObjectType]
        .filter(Boolean).join(' · ');

    const rowChips = (item) => [
        chip('module', 'bx-cube', item.sourceModule, sourceTitle(item)),
        chip('type', item.typeIcon, typeLabel(item)),
        chip(SLA_KIND[item.slaState], 'bx-time-five', slaLabel(item)),
        priorityChip(item),
        // The row chip's tooltip comes from the FIRST blocker's own sentence — `reasonKey` was never a declared
        // field, so this used to fall through to a generic line on every blocked row.
        isBlocked(item) ? chip('danger', 'bx-lock-alt', t('BlockedLabel'), blockedTooltip(item)) : '',
        /*
         * The SAME sentence the detail note shows — one composer, three surfaces (BL: "birini düzeltip kardeşini
         * bırakmak" three times this session). The chip clips at its own max width and carries the full sentence
         * as its tooltip, so a long reason is readable without widening the row.
         */
        waitingSentence(item)
            ? chip('warning', 'bx-time-five', waitingSentence(item), waitingSentence(item))
            : '',
        // Why the leading action cannot be used, ON the row rather than only in the button's tooltip. A blocked
        // item whose reason needs a hover reads as simply broken.
        blockedPrimaryReason(item) ? chip('secondary', 'bx-lock-alt', blockedPrimaryReason(item)) : '',
        isSnoozed(item) ? chip('secondary', 'bx-moon', tf('SnoozedUntil', item.snoozedUntil)) : '',
        (item.systemState && SYSSTATE[item.systemState]) ? chip(SYSSTATE[item.systemState].kind, SYSSTATE[item.systemState].icon, t(SYSSTATE[item.systemState].key)) : '',
        item.requester ? chip('requester', 'bx-user', item.requester) : ''
    ].join('');

    const rowHtml = (item, opts) => {
        const compact = opts && opts.compact;
        const inbox = opts && opts.inbox;
        const selected = item.id === state.selectedId;
        const terminal = isTerminal(item);
        const pinBtn = inbox || terminal ? '' : `<button type="button" class="wcn-pin${item.pinned ? ' pinned' : ''}" data-wcn-pin="${item.id}" title="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-label="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-pressed="${item.pinned}"><i class="bx ${item.pinned ? 'bxs-pin' : 'bx-pin'}"></i></button>`;
        /*
         * TAKING IT BACK, ON THE ROW THAT WAS REVEALED (BL-181 §6).
         *
         * A reader who opens "Ertelenmiş" to look at what they parked is one thought away from wanting it back,
         * and sending them into the detail page to find the personal card would be a detour for a decision they
         * have already made.
         *
         * ⚠ NO NEW ROW LANGUAGE. This is the PIN, exactly: same place in `.wcn-row-actions`, same small icon
         * button, same filled-vs-outline way of saying on/off, same `aria-pressed`, same one-click toggle
         * through the handler that already exists (`data-wcn-snooze`). A menu was the rejected alternative —
         * this row has no menu, and adding one for a single item would be a second vocabulary for the same job.
         *
         * It appears ONLY on a parked row, which is to say only while the chip is open: on every other row it
         * would be a control for a state the item is not in.
         */
        const unsnoozeBtn = isSnoozed(item)
            ? `<button type="button" class="wcn-pin pinned" data-wcn-snooze="${item.id}" title="${
                esc(t('SnoozeClear'))}" aria-label="${esc(t('SnoozeClear'))}" aria-pressed="true"><i class="bx bxs-moon"></i></button>`
            : '';
        const onBehalfBadge = item.delegator
            ? `<span class="wcn-badge wcn-badge-delegation" title="${esc(tf('OnBehalfOf', item.delegator))}"><i class="bx bx-user-voice"></i>${esc(tf('OnBehalfShort', item.delegator))}</span>`
            : '';
        const summary = item.itemType === 'meetingInvite'
            ? [item.meetingStart && item.meetingEnd ? `${item.meetingStart}–${item.meetingEnd}` : '', item.meetingLocation, item.requester].filter(Boolean).join(' · ')
            : item.summary;
        return `<div class="wcn-row${selected ? ' selected' : ''}${item.isUnread ? ' unread' : ''}" data-wcn-row="${item.id}" tabindex="0">
            <span class="wcn-row-accent wcn-row-accent-${SLA_KIND[item.slaState] || 'secondary'}" aria-hidden="true"></span>
            <div class="wcn-row-body">
                <div class="wcn-row-top">
                    ${item.isUnread ? '<span class="wcn-row-unread-dot" aria-hidden="true"></span>' : ''}
                    <span class="wcn-row-title">${esc(item.title)}</span>
                    ${onBehalfBadge}
                    ${inbox ? '' : `<span class="wcn-badge wcn-badge-${STATUS_KIND[displayStatus(item)]}">${esc(statusLabel(item))}</span>`}
                </div>
                ${compact ? '' : `<p class="wcn-row-summary">${esc(summary)}</p>`}
                <div class="wcn-row-chips">${rowChips(item)}</div>
            </div>
            <div class="wcn-row-actions">${unsnoozeBtn}${pinBtn}${actionCluster(item)}</div>
        </div>`;
    };

    /*
     * ── THE ONE ACTION→ICON DICTIONARY (extended 2026-08-24) ──────────────────────────────────────────────
     *
     * ⚠ THE ROW BUTTON AND THE DIALOG IT OPENS BOTH READ THIS. A dialog that picks its own glyph gives one
     * action two pictures — measured: the rail drew `bx-user-pin` for "Yeniden ata" while the dialog it opened
     * drew a speech bubble. If an action has no entry here, ADD IT HERE; do not work around it at a call site.
     *
     * `logTime` and `requestInfo` were added for exactly that reason: both open a dialog, neither was listed,
     * and both would otherwise have fallen through to the generic arrow.
     */
    const inboxActionIcon = (action) => ({
        accept: 'bx-check', approve: 'bx-check-shield', signoff: 'bx-check-circle',
        reject: 'bx-x-circle', decline: 'bx-x-circle', return: 'bx-undo',
        inquire: 'bx-question-mark', requestInfo: 'bx-question-mark',
        reassign: 'bx-user-pin', plan: 'bx-calendar-plus', logTime: 'bx-time-five',
        reviewMeeting: 'bx-calendar-event', scheduleReviewMeeting: 'bx-calendar-event'
    }[action.key] || 'bx-right-arrow-alt');

    const actionMenuTone = (action) => {
        if (action.kind === 'danger' || action.role === 'reject' || action.semanticType === 'dispute') { return ' text-danger'; }
        if (action.kind === 'success') { return ' text-success'; }
        if (action.kind === 'warning') { return ' text-warning'; }
        return '';
    };

    // A single overflow-menu row. Destructive, constructive and attention actions
    // receive semantic text colours. Icon + label sit in a flex
    // row (see .wcn-menu-item CSS) so the glyph is vertically centred with the text,
    // not baseline-dropped like a raw Sneat dropdown-item.
    const actionMenuLi = (item, action) => {
        const interactionLocked = state.submittingItemId === item.id;
        const disabled = action.disabled || interactionLocked ? ' disabled aria-disabled="true"' : '';
        // Disabled reason sits on its own muted line under the label — never mashed onto
        // the end of the label text (e.g. "Onayla ve kapat" + why-it's-locked).
        const reason = action.disabled ? `<small class="wcn-menu-reason">${esc(action.disabledReason || t(action.disabledReasonKey))}</small>` : '';
        return `<li><button type="button" class="dropdown-item wcn-menu-item${actionMenuTone(action)}" data-wcn-action="${action.key}" data-wcn-id="${item.id}"${disabled}><i class="bx ${inboxActionIcon(action)}"></i><span class="wcn-menu-text"><span class="wcn-menu-label">${esc(actionLabel(action))}</span>${reason}</span></button></li>`;
    };

    // Grouped menu body: neutral/constructive actions first, then — separated by a
    // divider — the refuse/push-back family (Reddet/İade et/İtiraz), so accept and
    // refuse never sit adjacent. `leadHtml` lets the Table cell put Görüntüle on top.
    const isNegativeAction = (action) => action.role === 'reject' || action.semanticType === 'dispute';
    const actionMenuBody = (item, actions, leadHtml) => {
        const negative = actions.filter(isNegativeAction);
        const positive = actions.filter((action) => !isNegativeAction(action));
        const pos = (leadHtml || '') + positive.map((action) => actionMenuLi(item, action)).join('');
        const neg = negative.map((action) => actionMenuLi(item, action)).join('');
        return pos + (neg ? `<li><hr class="dropdown-divider m-0"></li>${neg}` : '');
    };

    // When the source record changed / is unreachable, itemActions locks every real
    // action. The row must not go dead: the required next step IS the refresh, so it
    // becomes the visible primary. Clearing systemState re-renders the real actions.
    const refreshSourceBtn = (item) => {
        const unreachable = item.systemState === 'sourceUnavailable';
        const label = t(unreachable ? 'RetrySource' : 'RefreshSource');
        const icon = unreachable ? 'bx-wifi-off' : 'bx-refresh';
        return `<button type="button" class="btn btn-sm btn-label-warning wcn-inbox-action-primary" data-wcn-refresh-source="${item.id}" title="${esc(label)}"><i class="bx ${icon} me-1"></i>${esc(label)}</button>`;
    };
    const needsSourceRecovery = (item) =>
        ['stale', 'sourceUnavailable', 'reconciliationRequired'].includes(item.systemState);

    // Inbox is a decision queue, not a second task-detail surface. Each row answers
    // what needs attention, why it is here and when it matters. The primary action
    // appears once; less frequent actions stay behind a compact overflow menu.
    // Stage-appropriate action cluster (primary + secondary icon + overflow ···),
    // shared by the inbox rows and the Table view's "İşlemler" column. Actions come
    // from getActions, so each row shows exactly what its lifecycle stage allows.
    const actionCluster = (item) => {
        if (needsSourceRecovery(item)) { return `<span class="wcn-inbox-actions">${refreshSourceBtn(item)}</span>`; }
        const actions = itemActions(item);
        if (!actions.length) {
            return item.deepLink
                ? `<span class="wcn-inbox-actions"><button type="button" class="btn btn-sm btn-label-secondary" data-wcn-open="${item.id}"><i class="bx bx-link-external me-1"></i>${esc(t('DetailOpenSource'))}</button></span>`
                : '<span class="wcn-inbox-actions"></span>';
        }
        // One decision, one button. The primary action stays visible (Onayla/Kabul
        // et…); every other action — including reject — lives behind the ··· overflow,
        // so a destructive choice takes a deliberate second click. Same shape in the
        // inbox rows and the Table view's İşlemler column.
        const primary = rowPrimaryAction(actions);
        const overflow = actions.filter((action) => !primary || action.key !== primary.key);
        const interactionLocked = state.submittingItemId === item.id;
        const primaryButton = primary
            ? `<button type="button" class="btn btn-sm btn-label-${primary.kind} wcn-inbox-action-primary" data-wcn-action="${primary.key}" data-wcn-id="${item.id}"${interactionLocked || primary.disabled ? ' disabled' : ''}${primary.disabled && primary.disabledReason ? ` title="${esc(primary.disabledReason)}"` : ''}><i class="bx ${inboxActionIcon(primary)} me-1"></i>${esc(actionLabel(primary))}</button>`
            : '';
        const overflowMenu = overflow.length
            ? `<div class="dropdown"><button type="button" class="btn btn-icon wcn-inbox-action-more dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-expanded="false" title="${esc(t('ActionsLabel'))}" aria-label="${esc(t('ActionsLabel'))}"><i class="bx bx-dots-vertical-rounded icon-md"></i></button><ul class="dropdown-menu dropdown-menu-end">${actionMenuBody(item, overflow)}</ul></div>`
            : '';
        return `<span class="wcn-inbox-actions">${primaryButton}${overflowMenu}</span>`;
    };

    // Table view "İşlemler" cell — same decision-queue shape as the list rows: the
    // primary action stays visible (Onayla/Kabul et…), everything else lives behind a
    // borderless vertical kebab (golden's icon style). Görüntüle leads the menu, since
    // a stray cell click does not open detail in the grid.
    const tableActionCell = (item) => {
        if (item.fixtureKind === 'triggerOnly') {
            const trigger = item.trigger;
            const surface = global.WorkCenterNextTriggerResponseResolver?.resolveTriggerResponse(trigger, {
                submittingActionCode: state.submittingTriggerId === trigger.id ? state.submittingActionCode : null
            });
            if (!surface || surface.invalid) { return ''; }
            const byCode = new Map((trigger.actions || []).map((action) => [action.code, action]));
            const primary = byCode.get(surface.primaryActionCode);
            const primaryButton = primary
                ? `<button type="button" class="btn btn-sm btn-primary wcn-inbox-action-primary" data-wcn-trigger-action="${esc(primary.code)}" data-wcn-trigger-id="${esc(trigger.id)}"${primary.enabled === false || (state.submittingTriggerId === trigger.id && state.submittingActionCode === primary.code) ? ' disabled' : ''}>${esc(data.resolveLabel(primary.label))}</button>`
                : '';
            const overflowCodes = [...surface.secondaryActionCodes, ...surface.overflowActionCodes].filter(Boolean);
            const overflowItems = overflowCodes.map((code) => {
                const action = byCode.get(code);
                return action
                    ? `<li><button type="button" class="dropdown-item wcn-menu-item${['declineMeeting', 'reject', 'cancel'].includes(code) ? ' text-danger' : ''}" data-wcn-trigger-action="${esc(code)}" data-wcn-trigger-id="${esc(trigger.id)}"${action.enabled === false || state.submittingTriggerId === trigger.id ? ' disabled' : ''}><i class="bx ${['declineMeeting', 'reject', 'cancel'].includes(code) ? 'bx-x-circle' : 'bx-right-arrow-alt'}"></i><span>${esc(data.resolveLabel(action.label))}</span></button></li>`
                    : '';
            }).join('');
            const calendarItem = trigger.source?.deepLink
                ? `<li><button type="button" class="dropdown-item wcn-menu-item" data-wcn-trigger-open="${esc(trigger.id)}"><i class="bx bx-calendar-event"></i><span>${esc(t('ViewCalendar'))}</span></button></li>`
                : '';
            const kebab = overflowItems || calendarItem
                ? `<div class="dropdown"><button type="button" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-expanded="false" title="${esc(t('ActionsLabel'))}" aria-label="${esc(t('ActionsLabel'))}"><i class="bx bx-dots-vertical-rounded icon-md"></i></button><ul class="dropdown-menu dropdown-menu-end m-0">${overflowItems}${calendarItem}</ul></div>`
                : '';
            return `<div class="d-flex align-items-center justify-content-end wcn-table-actions">${primaryButton}${kebab}</div>`;
        }
        const actions = itemActions(item);
        const primary = rowPrimaryAction(actions);
        const rest = actions.filter((action) => !primary || action.key !== primary.key);
        const interactionLocked = state.submittingItemId === item.id;
        // Stale source → refresh is the primary; real actions come back after it clears.
        const primaryButton = needsSourceRecovery(item)
            ? refreshSourceBtn(item)
            : (primary
                ? `<button type="button" class="btn btn-sm btn-label-${primary.kind} wcn-inbox-action-primary" data-wcn-action="${primary.key}" data-wcn-id="${item.id}"${interactionLocked || primary.disabled ? ' disabled' : ''}${primary.disabled && primary.disabledReason ? ` title="${esc(primary.disabledReason)}"` : ''}><i class="bx ${inboxActionIcon(primary)} me-1"></i>${esc(actionLabel(primary))}</button>`
                : '');
        const viewItem = `<li><button type="button" class="dropdown-item wcn-menu-item" data-wcn-detail="${item.id}"><i class="bx bx-show"></i><span>${esc(t('RowView'))}</span></button></li>`;
        const kebab = `<div class="dropdown"><button type="button" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-expanded="false" title="${esc(t('ActionsLabel'))}" aria-label="${esc(t('ActionsLabel'))}"><i class="bx bx-dots-vertical-rounded icon-md"></i></button><ul class="dropdown-menu dropdown-menu-end m-0">${actionMenuBody(item, needsSourceRecovery(item) ? [] : rest, viewItem)}</ul></div>`;
        return `<div class="d-flex align-items-center justify-content-end wcn-table-actions">${primaryButton}${kebab}</div>`;
    };

    const inboxRowHtml = (item) => {
        return rowHtml(item, { inbox: true });
    };

    const paginateList = (items) => {
        const pages = Math.max(1, Math.ceil(items.length / state.pageLength));
        state.listPage = Math.min(state.listPage, pages - 1);
        const start = state.listPage * state.pageLength;
        return { pageItems: items.slice(start, start + state.pageLength), pages, start };
    };

    const listPager = (total, pages, start) => {
        if (total <= state.pageLength) { return ''; }
        const end = Math.min(start + state.pageLength, total);
        return `<div class="wcn-list-pager"><span>${start + 1}–${end} / ${total}</span><div class="btn-group btn-group-sm"><button type="button" class="btn btn-label-secondary btn-icon" data-wcn-list-page="prev"${state.listPage === 0 ? ' disabled' : ''} aria-label="${esc(t('PreviousPage'))}"><i class="bx bx-chevron-left"></i></button><button type="button" class="btn btn-label-secondary btn-icon" data-wcn-list-page="next"${state.listPage >= pages - 1 ? ' disabled' : ''} aria-label="${esc(t('NextPage'))}"><i class="bx bx-chevron-right"></i></button></div></div>`;
    };

    // ── List view (grouped by SLA) ────────────────────────────────────────────
    const renderList = (items) => {
        state.visibleOrder = [];
        if (state.tab === 'inbox') {
            const sorted = items.slice().sort((a, b) => {
                if (a.itemType === 'approval' && b.itemType !== 'approval') { return -1; }
                if (a.itemType !== 'approval' && b.itemType === 'approval') { return 1; }
                return bySla(a, b);
            });
            const entries = [
                ...activeTriggers().map((trigger) => ({ kind: 'trigger', trigger })),
                ...sorted.map((item) => ({ kind: 'item', item }))
            ];
            if (!entries.length) { return emptyState(); }
            const paged = paginateList(entries);
            const rows = paged.pageItems.map((entry) => {
                if (entry.kind === 'trigger') { return renderTriggerResponses([entry.trigger]); }
                state.visibleOrder.push(entry.item.id);
                return inboxRowHtml(entry.item);
            }).join('');
            return `<div class="wcn-group-rows">${rows}</div>${listPager(entries.length, paged.pages, paged.start)}`;
        }
        if (!items.length) { return emptyState(); }
        // Flat, SLA-sorted (most urgent first) — the SLA-state group headings are
        // replaced by the per-row left colour accent (wcn-row-accent), so urgency reads
        // at a glance without the heading weight. Order preserved via bySla.
        const sortedItems = items.slice().sort(bySla);
        const paged = paginateList(sortedItems);
        const rows = paged.pageItems.map((item) => { state.visibleOrder.push(item.id); return rowHtml(item); }).join('');
        return `<div class="wcn-list wcn-group-rows">${rows}</div>${listPager(sortedItems.length, paged.pages, paged.start)}`;
    };

    // ── Split-detail view ─────────────────────────────────────────────────────
    // Compact, self-contained card for the Split master list — and the future
    // Calendar view's "unplanned work" rail (drag onto the calendar to schedule).
    // Vertical layout so it never truncates like the wide list row did in a narrow
    // column: a priority accent stripe, type, 2-line title, SLA/blocked chips, source.
    const splitCard = (item) => {
        const selected = item.id === state.selectedId;
        const terminal = item.lifecycle === 'Done' || item.lifecycle === 'Cancelled';
        const typeKind = item.itemType === 'meetingInvite' ? 'meeting' : item.itemType;
        const isMeeting = item.itemType === 'meetingInvite';
        const metaLine = isMeeting
            ? [item.meetingStart && item.meetingEnd ? `${item.meetingStart}–${item.meetingEnd}` : '', item.meetingLocation].filter(Boolean).join(' · ')
            : [item.sourceModule, item.requester].filter(Boolean).join(' · ');
        const pinBtn = terminal ? '' : `<button type="button" class="wcn-splitcard-pin${item.pinned ? ' pinned' : ''}" data-wcn-pin="${item.id}" title="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-label="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-pressed="${item.pinned}"><i class="bx ${item.pinned ? 'bxs-pin' : 'bx-pin'}"></i></button>`;
        return `<article class="card wcn-splitcard${hasPriority(item) ? ` wcn-splitcard-p-${PRIORITY_KIND[item.priority]}` : ''}${selected ? ' selected' : ''}${item.isUnread ? ' unread' : ''}" data-wcn-row="${item.id}" tabindex="0" role="button" draggable="true" aria-label="${esc(tf('TableOpenRow', item.title))}">
            <div class="wcn-splitcard-head">
                <span class="wcn-inbox-type wcn-inbox-type-${typeKind}">${esc(typeLabel(item))}</span>
                <span class="wcn-splitcard-head-end">
                    ${hasPriority(item) ? `<span class="wcn-chip wcn-chip-${PRIORITY_KIND[item.priority]} wcn-splitcard-prio"><i class="bx bx-flag"></i>${esc(priorityLabel(item))}</span>` : ''}
                    ${pinBtn}
                </span>
            </div>
            <div class="wcn-splitcard-title">${esc(item.title)}</div>
            <div class="wcn-splitcard-meta">
                <span class="wcn-chip wcn-chip-${SLA_KIND[item.slaState]}"><i class="bx bx-time-five"></i>${esc(slaLabel(item))}</span>
                ${isBlocked(item) ? `<span class="wcn-chip wcn-chip-danger"><i class="bx bx-lock-alt"></i>${esc(t('BlockedLabel'))}</span>` : ''}
                ${item.delegator ? `<span class="wcn-chip wcn-chip-delegation"><i class="bx bx-user-voice"></i>${esc(tf('OnBehalfShort', item.delegator))}</span>` : ''}
            </div>
            <div class="wcn-splitcard-foot"><i class="bx bx-cube"></i><span>${esc(metaLine)}</span></div>
        </article>`;
    };


    // Step-bar = the SOURCE-declared stages (spec v3) — only when the item carries
    // the `stages` capability, NOT a universal WorkCenter lifecycle. The active
    // stage is matched by lifecycle; earlier stages are marked done.



    // ── Golden Reference Compact primitives ────────────────────────────────────────────────────────────────
    // These are the reference's OWN classes, not a lookalike: sizes and colours come from
    // Views/DevEnablement/GoldenReferenceCompact/Details.cshtml via CSS, never from values retyped here.

    /*
     * One labelled field: icon, label, value.
     *
     * The reference prints "-" for an empty value; we print NOTHING and drop the row. A dash asserts "I looked and
     * it was empty", which is a claim about data we do not have — the rule this page has been corrected under all
     * week. That is the ONE deliberate divergence from the reference.
     */
    /*
     * BL-049 — the source record's identifier, moved off the primary surface.
     *
     * This used to render the raw GUID as an ordinary field: "Kaynak kaydı 31a44983-40cc-…". It gives the reader
     * no capability at all — the "open the source record" button directly below already does the one thing the id
     * is for — and a 36-character opaque string sitting among human-readable facts is noise that makes the real
     * ones harder to find.
     *
     * It is not deleted, because it IS what a support conversation needs. So it becomes a support affordance: a
     * shortened form the eye can skip, with the full value on the clipboard button and in the title. Anyone who
     * needs it gets it in one click; nobody else has to read past it.
     */
    /*
     * `referenceField` and `previewField` LIVED HERE and are gone with the technical card. Both built the
     * two-column golden tile the "Teknik bilgi" card used; the Kaynak card that replaced it is a one-column
     * definition list, because the rail is 337px and that grid wrapped every value in it. Measured before
     * deleting: neither had another caller. Their rules survive in `sourceRow` — an empty value prints no row,
     * and a long opaque id gets an ellipsis while a short business key is left whole.
     */

    /*
     * CALLED-OFF WORK NEVER COUNTS — written once, here.
     *
     * The server states this rule in TaskBlockingRules ("Nothing CANCELLED ever blocks"); the browser needs it
     * twice over — for the notice that names the open children holding up completion, and for the running-child
     * signal below. Two spellings of it would let the count disagree with the sentence under it.
     */
    const isCancelledSubtask = (subtask) => subtask && subtask.status === 'cancelled';

    /*
     * HOW MANY CHILDREN ARE ALREADY WORKING.
     *
     * "Running" is narrower than "open": a not-started child is not evidence that work has begun, and the
     * sentence this feeds claims exactly that. `in-progress` is the projection's own vocabulary for
     * started-and-not-finished (WorkItemSubtaskDto.Status), which the server derives from
     * TaskBlockingRules.StateOf — so this reads a decision rather than making one.
     */
    const runningSubtaskCount = (item) => ((item.subtasks && item.subtasks.items) || [])
        .filter((subtask) => !isCancelledSubtask(subtask) && subtask.status === 'in-progress')
        .length;

    /*
     * What this task needs from the viewer RIGHT NOW, in a sentence.
     *
     * The page can already show a dozen true facts without answering "so what do I do?". Keyed by state, and an
     * unmapped state prints NO banner — a guidance box that guesses is worse than none.
     */
    /*
     * ── THE WAITING SENTENCE, WRITTEN ONCE ──────────────────────────────────────────────────────────────────
     *
     * Three surfaces say this — the detail note, the list row's chip and the lifecycle strip — and until now
     * each composed it itself. Two of them also carried the SAME defect: when a person was known they printed
     * "waiting on X" and DROPPED the reason entirely, so naming somebody cost the reader the sentence that says
     * what is actually being waited for.
     *
     * Both facts are now shown together when both exist, and every language gets the WHOLE sentence rather than
     * fragments joined in JavaScript: `WaitingOnWithReason` carries both slots so a language that puts the
     * person last can do so. Never `person + ' — ' + reason` here.
     */
    const waitingSentence = (item) => {
        const person = item.waitingOn;
        const reason = item.waitingReason;
        if (person && reason) { return tf('WaitingOnWithReason', person, reason); }
        if (person) { return tf('WaitingOn', person); }
        return reason || '';
    };

    const guidanceFor = (item) => {
        if (item.admissionState === 'pendingAcceptance') { return { kind: 'primary', key: 'GuidancePendingAcceptance' }; }
        if (item.admissionState === 'pendingClaim') { return { kind: 'primary', key: 'GuidancePendingClaim' }; }
        if (item.gates?.approval?.status === 'pending') { return { kind: 'warning', key: 'GuidanceApprovalPending' }; }
        if (item.gates?.review?.status === 'pending') { return { kind: 'warning', key: 'GuidanceReviewPending' }; }
        if (item.lifecycle === 'Waiting') {
            // The holder's own sentence when they gave one — nothing here is invented on their behalf.
            return waitingSentence(item)
                ? { kind: 'warning', text: tf('GuidanceWaitingBecause', waitingSentence(item)) }
                : { kind: 'warning', key: 'GuidanceWaiting' };
        }
        return null;
    };

    /*
     * NOBODY HAS ACCEPTED THIS, AND THREE CHILDREN ARE ALREADY WORKING.
     *
     * Both facts were on the page and the combination was not: the banner said "waiting to be accepted" while
     * the list below it said "Devam ediyor" three times, and nothing joined them.
     *
     * ⚠ A SIGNAL, NOT A GATE, and the direction is deliberate. No rule ties a child's start to its parent's
     * acceptance — if one did, a single unpressed "Accept" would stop everyone below it. So this adds a
     * SENTENCE to the banner that already exists: no new banner, no new colour, nothing disabled, and no rule
     * added to TaskBlockingRules.
     *
     * It appears only when BOTH conditions hold. A sentence printed unconditionally carries no information.
     */
    const renderGuidance = (item) => {
        const guidance = guidanceFor(item);
        if (!guidance) { return ''; }
        const text = guidance.text || t(guidance.key);

        const running = item.admissionState === 'pendingAcceptance' ? runningSubtaskCount(item) : 0;
        const runningNote = running > 0
            ? `<span class="wcn-guidance-note">${esc(tf('GuidanceChildrenRunning', running))}</span>`
            : '';

        return `<div class="alert alert-${guidance.kind} wcn-guidance d-flex align-items-start gap-2" role="note">
            <i class="bx bx-info-circle"></i><span>${esc(text)}${runningNote ? ' ' : ''}${runningNote}</span>
        </div>`;
    };

    // ── Detail layout: content on the left, everything you can DO on the right ──────────────────────────────

    /*
     * What each action will DO, keyed by action code.
     *
     * A verb alone ("Return") does not say where the work goes. This is a map, not a chain of ifs, so adding an
     * action is adding one entry — and an unmapped code renders NOTHING and says so once, rather than shipping a
     * button whose consequence is a guess.
     */
    const ACTION_OUTCOME_KEY = {
        accept: 'OutcomeAccept',
        claim: 'OutcomeClaim',
        release: 'OutcomeRelease',
        plan: 'OutcomePlan',
        start: 'OutcomeStart',
        inquire: 'OutcomeInquire',
        return: 'OutcomeReturn',
        reassign: 'OutcomeReassign',
        complete: 'OutcomeComplete',
        cancel: 'OutcomeCancel'
    };

    const reportedMissingOutcomes = new Set();
    // Disabled actions the projection sent with no reason. Warned once each, then never drawn.
    const reportedUnexplainedActions = new Set();

    const actionOutcome = (action) => {
        const key = ACTION_OUTCOME_KEY[action.code];
        if (!key) {
            if (!reportedMissingOutcomes.has(action.code)) {
                reportedMissingOutcomes.add(action.code);
                console.warn(
                    `[WorkCenterNext] No outcome text for action "${action.code}" — the button will not say what `
                    + 'it does. Add it to ACTION_OUTCOME_KEY and the 7 WorkCenterNext resx files.');
            }
            return '';
        }
        return `<span class="wcn-act-outcome">${esc(t(key))}</span>`;
    };

    /*
     * ONE ACTION, IN ONE OF THREE TIERS — and the tier is STRUCTURE, not colour.
     *
     * The rail used to be four rows of button-plus-sentence at nearly equal weight, distinguished only by which
     * button was `btn-primary`. That made the page's whole purpose — "do the thing this task is asking for" —
     * a matter of noticing a hue, and it spent 275px saying four sentences of which only the first was read.
     *
     *   primary     full width, its outcome sentence KEPT. The server names it (`primaryActionCode`), so the
     *               emphasis is the projection's decision and not this file's guess.
     *   secondary   compact, wrapping, NO prose. Their sentences moved into the dialogs each one opens, where
     *               they are read at the moment they matter instead of before anyone has chosen.
     *   destructive last, under a rule, in the danger colour. NOT hidden in the kebab: hiding a destructive
     *               action is not safety, it is a surprise. The confirm dialog is where the caution belongs.
     *
     * `locked` comes from the resolver's `interactionLocked`, never recomputed here — see renderActionRail.
     */
    /*
     * THE OUTCOME SENTENCE, AT ITS DESTINATION.
     *
     * Secondary actions no longer carry prose in the card — but the sentences were not deleted, they MOVED. Each
     * one now opens the dialog its action opens, at the top, where it is read at the moment the reader is
     * actually deciding rather than while scanning four buttons none of which they had chosen yet.
     *
     * Same resource keys (`OutcomePlan`, `OutcomeInquire`, `OutcomeReassign`, `OutcomeCancel`) — nothing was
     * re-translated, so all seven languages came along unchanged.
     */
    const outcomeLead = (action) => {
        const key = ACTION_OUTCOME_KEY[action.code];
        return key ? `<p class="wcn-dialog-lead">${esc(t(key))}</p>` : '';
    };

    /*
     * WHICH OF THE THREE NATURES THIS ACTION HAS — and it is only ever three.
     *
     * The tier is already legible from STRUCTURE (full width, top, sole sentence), so the primary's colour is
     * free to say what the action IS rather than that it is the primary. Free, but not unlimited: three
     * meanings take colour and everything else stays neutral, because a fourth and fifth hue turn a signal into
     * a palette and nobody reads a palette.
     *
     *   advancing  → accent   (accept, start, plan — move it along)
     *   completing → success  (complete, approve, sign off — finish it)
     *   destructive→ danger   (cancel — end it)
     *
     * Anything whose `kind` is not one of those three falls NEUTRAL rather than being assigned a colour by
     * this file's guess.
     */
    const ACTION_NATURE = { primary: 'accent', success: 'success', danger: 'danger' };

    /*
     * ONE ACTION, IN ONE OF THREE TIERS — tier by STRUCTURE, emphasis by FILL.
     *
     * ⚠ EXACTLY ONE FILLED BUTTON PER CARD. Every button used to be a Sneat `btn-label-*` (tint) variant, and
     * in this theme a tint reads as DISABLED — measured on screen as a pale green primary with white text, two
     * pale grey pills and a pale pink cancel. A card whose most important control looks switched off is worse
     * than one with no emphasis at all, because the reader concludes they are not allowed to act.
     *
     * So the primary is the only thing with a fill, at full saturation. The others carry no fill at all — but
     * they are still real 38px buttons with a real hit area, not bare links: they DO something, and a thing
     * that does something must look pressable even when it is quiet.
     *
     * NO ICONS ON THE BUTTONS. Every one of them restated its own label — a tick on "Accept", a question mark
     * on "Ask", a calendar on "Plan" — and the actions with no mapping fell back to `bx-right-arrow-alt`, which
     * is what put an arrow in front of "Tamamla" and "Görevi iptal et" and made them read as links. The one
     * icon that survives is the LOCK on a blocked action's reason, because it states the prohibition rather
     * than repeating a word beside it.
     */
    /*
     * ── THE ID THAT CARRIES THE PAIRING ───────────────────────────────────────────────────────────────────
     *
     * DERIVED FROM THE ACTION'S OWN CODE, never generated. A counter or a random suffix would produce a new id
     * on every re-render — and this card re-renders on every poll, every filter change and every write — so an
     * `aria-describedby` written in one pass would point at nothing in the next. The action code is the thing
     * that is stable across renders, so it is the thing the id is made of. The item id joins it because the
     * list surface can hold many items at once and an id must be unique in the document.
     *
     * Empty for an action with nothing to describe: an `aria-describedby` pointing at an element that was never
     * drawn is worse than no attribute, because a screen reader announces the gap as a broken reference.
     */
    const actionReasonId = (item, action) => (action?.disabled && action?.disabledReason
        ? `wcn-actreason-${String(item?.id || '').replace(/[^a-zA-Z0-9_-]/g, '')}-${
            String(action.code || action.key || '').replace(/[^a-zA-Z0-9_-]/g, '')}`
        : '');

    /*
     * The sentence, standing on its own beneath the rail — and therefore NAMING the action it refuses.
     *
     * ⚠ THE NAME IS `actionLabel(action)`, the string on the button itself, not a second label derived for
     * prose. Two names for one action agree on the day they are written and diverge on the day one of them is
     * retranslated; the reader then has to work out whether "Devret" and "Başkasına ata" are the same control.
     */
    const actionReasonNote = (item, action) => {
        const id = actionReasonId(item, action);
        if (!id) { return ''; }
        return `<p class="alert alert-warning wcn-act-reason d-flex align-items-start gap-2" id="${esc(id)}"
                   role="note"><i class="bx bx-lock-alt" aria-hidden="true"></i><span>${
            esc(tf('ActionDisabledWithName', actionLabel(action), action.disabledReason))}</span></p>`;
    };

    /*
     * ── ONE TIER: A ROW OF BUTTONS, THEN THE SENTENCES ────────────────────────────────────────────────────
     *
     * Both non-primary tiers are drawn through here so the secondary rail and the destructive rail cannot
     * drift: the destructive tier wears the same `wcn-actrail-secondary` row and had the same defect, and the
     * previous fix touched only one of them.
     */
    const actionRail = (item, list, variant, locked) =>
        `<ul class="wcn-actrail-secondary">${
            list.map((a) => actionButton(item, a, variant, locked)).join('')}</ul>${
            list.map((a) => actionReasonNote(item, a)).join('')}`;

    const actionButton = (item, action, variant, locked) => {
        const disabled = action.disabled || locked;
        /*
         * A DISABLED ACTION MUST SAY WHY, OR NOT BE DRAWN AT ALL.
         *
         * The reason is the projection's (`disabledReason`, resolved server-side alongside `disabledReasonCode`)
         * — measured on a blocked task: `CHECKLIST_INCOMPLETE` → `WorkAggregation_ActionDisabled_ChecklistIncomplete`.
         * It is NOT re-derived from `gates` here; a second derivation is a second answer waiting to disagree.
         */
        /*
         * ⚠ ONE PROHIBITION, ONE VOICE (2026-08-24). Measured: the subtask gate — the sentence that says why
         * "Tamamla" will refuse — is drawn as `alert alert-warning` (app.js, `wcn-subtask-gate`), while THIS
         * sentence, which says the same kind of thing about the same kind of button, had no surface, no border
         * and no padding at all: bare amber text floating beside the control. Same refusal, two treatments.
         *
         * The product already answered the question, so its answer is used: the theme's own alert, plus the
         * modifier below that does exactly what `.wcn-subtask-gate` does to it (tighter padding, 13px). No new
         * colour, no new radius, no new number. The lock stays — it is the glyph that states the prohibition.
         */
        /*
         * ⚠ ONLY THE PRIMARY KEEPS ITS SENTENCE IN ITS OWN ROW (2026-08-24). The secondary and destructive
         * tiers share a wrapping flex row, and an alert inside one of those `<li>`s can only be as wide as the
         * button — or, if the `<li>` is widened to fix that, the button leaves the row and the actions stop
         * being able to stand side by side. Both were tried; each broke the other.
         *
         * So the sentence LEAVES the `<li>` (see `actionRail`): the buttons keep their natural widths on one
         * line, and the sentences stack full-width beneath them. What proximity used to say — which sentence
         * belongs to which button — the sentence now says itself, by naming the action (`ActionDisabledWithName`),
         * and `aria-describedby` says it again for a reader who never sees the layout.
         */
        const reason = variant === 'primary' && action.disabled && action.disabledReason
            ? `<p class="alert alert-warning wcn-act-reason d-flex align-items-start gap-2" role="note"><i class="bx bx-lock-alt" aria-hidden="true"></i><span>${esc(action.disabledReason)}</span></p>`
            : '';
        // The primary is the only tier that keeps its sentence in the card.
        const outcome = variant === 'primary' ? actionOutcome(action) : '';
        const busy = locked && state.submittingActionCode === action.code;
        const label = busy ? t('ActionSubmitting') : actionLabel(action);
        const nature = variant === 'destructive' ? 'danger' : (ACTION_NATURE[action.kind] || 'neutral');
        const cls = variant === 'primary'
            ? `wcn-act-btn wcn-act-fill wcn-act-fill-${nature}`
            : `wcn-act-btn wcn-act-bare wcn-act-bare-${nature}`;
        const describedBy = variant === 'primary' ? '' : actionReasonId(item, action);
        return `<li class="wcn-act wcn-act-${variant}${action.disabled ? ' wcn-act-disabled' : ''}">
            <button type="button" class="${cls}"
                    data-wcn-action="${esc(action.key)}" data-wcn-id="${esc(item.id)}"${
            describedBy ? ` aria-describedby="${esc(describedBy)}"` : ''}${
            disabled ? ' disabled aria-disabled="true"' : ''}${busy ? ' aria-busy="true"' : ''}>
                ${busy ? '<i class="bx bx-loader-alt bx-spin" aria-hidden="true"></i>' : ''}<span>${esc(label)}</span>
            </button>
            ${outcome}${reason}
        </li>`;
    };

    /*
     * ── ONE SOURCE FOR "WHAT CAN BE DONE" ─────────────────────────────────────────────────────────────────
     *
     * The card and the narrow-screen bar are two VIEWS of one answer. Computing the set twice — even from the
     * same `itemActions` — is how the two drift: a filter added on one side, a tier renamed on the other, and
     * a button appears in the bar that the card refuses. This session has produced that shape repeatedly (two
     * chip vocabularies, two lock models, three unwrappings of one envelope), so the set is derived HERE and
     * both surfaces read the result.
     *
     * The unexplained-disabled filter lives here too, so a block the projection could not justify is invisible
     * to BOTH surfaces rather than to whichever one remembered to filter.
     */
    const actionTiers = (item) => {
        const all = itemActions(item);
        const actions = all.filter((a) => {
            /*
             * ⚠ "Süre gir" IS DRAWN BY THE TIMESHEET CARD, NOT HERE (2026-08-24, Tur B). It is a personal
             * measurement, not a lifecycle move — it changes no state — so standing it beside Complete and
             * Pause misfiled it. The card owns it now; leaving it in both places would be one action with two
             * homes, which is how the two drift.
             *
             * ⚠ THE ACTION ITSELF IS UNTOUCHED: same projection entry, same key, same handler, same dialog.
             * Only where the button is painted moved.
             */
            if (a.key === 'logTime') { return false; }
            if (!a.disabled || a.disabledReason) { return true; }
            if (!reportedUnexplainedActions.has(a.code)) {
                reportedUnexplainedActions.add(a.code);
                console.warn(
                    `[WorkCenterNext] Action "${a.code}" is disabled with no disabledReason — it is not drawn. `
                    + 'The projection must send a reason with every block.');
            }
            return false;
        });
        const primary = rowPrimaryAction(actions);
        return {
            actions,
            primary,
            destructive: actions.filter((a) => a.destructive && a !== primary),
            secondary: actions.filter((a) => a !== primary && !a.destructive)
        };
    };

    /*
     * ── THE NARROW-SCREEN ACTION BAR ──────────────────────────────────────────────────────────────────────
     *
     * MEASURED at 900px: "Mevcut aksiyonlar" began at the page's 1876th pixel of 2597 — 2.08 screens of
     * scrolling to learn what you may do. At ≥992 the rail is sticky and the actions are always on screen, so
     * this exists for exactly one range and is drawn for exactly one range.
     *
     * ⚠ IT DOES NOT REPLACE THE CARD. The card stays where it is, at every width, with its sentences and its
     * tiers; this is a shortcut to the primary, not a second home for the actions.
     *
     * ⚠ WHY NOT IN THE PAGE HEADER (owner's decision, recorded because it will be asked again): a create form's
     * "Save" is a page-level constant. This page's primary is not — its identity changes with the task (Accept /
     * Complete / Complete in Mevzuat), it can be disabled with a reason, and it has four siblings. An action
     * with siblings belongs beside its siblings.
     *
     * MECHANISM, NOT INVENTION: `.sticky-bottom` is Bootstrap's own utility, already used in this product
     * (`GoalCreate.cshtml`'s readiness panel). Its `z-index: 1020` sits below the offcanvas layer (1045) and its
     * backdrop (1040), so an open panel covers the bar rather than the other way round — measured, not assumed.
     *
     * `d-lg-none` is what makes ≥992 draw nothing: `display: none` removes it from the layout, the accessibility
     * tree and the tab order alike. One render output, no width branch in JS, no resize listener — a
     * width-dependent render means redrawing on resize, and redrawing on this page has already dropped a panel.
     */
    const renderActionBar = (item, locked, surface) => {
        const { actions, primary, secondary, destructive } = actionTiers(item);
        // Nothing to do — a closed task, or one that is not yours — draws no bar at all.
        if (!actions.length || !primary) { return ''; }

        const rest = secondary.concat(destructive);
        const busy = locked && state.submittingActionCode === primary.code;
        const label = busy ? t('ActionSubmitting') : actionLabel(primary);
        const nature = ACTION_NATURE[primary.kind] || 'neutral';
        const disabled = primary.disabled || locked;
        /*
         * A disabled primary keeps its REASON here too. The bar is the only part of the page a narrow-screen
         * reader may see without scrolling, so "you cannot press this" without "because…" would be worse here
         * than in the card.
         */
        const reason = primary.disabled && primary.disabledReason
            // Its sibling in the rail was the one reported; this one carried the SAME bare treatment for the
            // SAME sentence, and fixing one while leaving the other is the mistake this session made three times.
            ? `<p class="alert alert-warning wcn-actionbar-reason d-flex align-items-start gap-2" role="note"><i class="bx bx-lock-alt" aria-hidden="true"></i><span>${esc(primary.disabledReason)}</span></p>`
            : '';

        const depthLink = surface?.surfaceMode === 'deeplink'
            ? (item.source?.deepLink || item.deepLink || item.sourceDeepLink)
            : null;
        const lead = depthLink
            ? `<a class="wcn-act-btn wcn-act-fill wcn-act-fill-accent wcn-actionbar-lead" href="${esc(depthLink)}">
                <i class="bx bx-link-external" aria-hidden="true"></i><span>${
                esc(tf('ActionCompleteInSource', item.sourceModuleName || item.sourceModule || ''))}</span>
               </a>`
            : `<button type="button" class="wcn-act-btn wcn-act-fill wcn-act-fill-${nature} wcn-actionbar-lead"
                    data-wcn-action="${esc(primary.key)}" data-wcn-id="${esc(item.id)}"${
            disabled ? ' disabled aria-disabled="true"' : ''}${busy ? ' aria-busy="true"' : ''}>
                ${busy ? '<i class="bx bx-loader-alt bx-spin" aria-hidden="true"></i>' : ''}<span>${esc(label)}</span>
               </button>`;

        // The siblings, folded — the bar is a shortcut, and a shortcut that lists everything is the card again.
        /*
         * ⚠ `dropup`, NOT a flip — and the two are different promises.
         *
         * MEASURED at 900×900: this menu opened DOWNWARD from a button already sitting on the bottom edge, so it
         * rendered 889→1063 in a 900px viewport — 163px of it below the fold, with nothing to scroll to because
         * the bar is fixed to that edge. The reader clicked, the menu opened, and the screen did not change.
         *
         * Why the automatic flip did not save it: `data-bs-display="static"` — written into this markup when the
         * bar was built — switches Bootstrap OFF Popper, and Popper is the thing that flips. Measured
         * `transform: none` on the open menu, which is the fingerprint of exactly that.
         *
         * ⚠ THAT ATTRIBUTE IS GONE NOW, deliberately: `dropup` already makes the direction deterministic, so the
         * attribute bought nothing here — and left in place it would drop the NEXT dropdown added to this page
         * into the same trap, silently. Popper positions again; `dropup` is what decides the direction.
         *
         * The cure is DETERMINISTIC rather than clever. A bar glued to the bottom of the viewport has no case in
         * which downward is right, so this does not ask to be flipped when there is no room — it opens upward,
         * always, through Bootstrap's own `dropup` (`bottom: 100%` instead of `top: 100%`). Re-enabling Popper
         * would trade one certainty for a positioning engine whose answer depends on scroll state, and the
         * failure mode of that trade is invisible until somebody is on a short screen.
         */
        const more = rest.length
            ? `<div class="dropdown dropup wcn-actionbar-more">
                <button type="button" class="wcn-act-btn wcn-act-bare wcn-act-bare-neutral dropdown-toggle"
                        data-bs-toggle="dropdown" aria-expanded="false"
                        aria-label="${esc(t('ActionsOther'))}"${locked ? ' disabled' : ''}>
                    <span>${esc(t('ActionsOther'))}</span>
                </button>
                <ul class="dropdown-menu dropdown-menu-end">
                    ${rest.map((a) => `<li><button type="button" class="dropdown-item${
                a.destructive ? ' text-danger' : ''}" data-wcn-action="${esc(a.key)}"
                        data-wcn-id="${esc(item.id)}"${a.disabled || locked ? ' disabled' : ''}>${
                esc(actionLabel(a))}</button></li>`).join('')}
                </ul>
               </div>`
            : '';

        return `<div class="wcn-actionbar sticky-bottom d-lg-none${locked ? ' wcn-actionbar-locked' : ''}"
                     role="region" aria-label="${esc(t('ActionsAvailable'))}">
            ${reason}
            <div class="wcn-actionbar-row">${lead}${more}</div>
        </div>`;
    };

    const renderActionRail = (item, locked, surface) => {
        /*
         * The set comes from `actionTiers` — the SAME call the narrow-screen bar makes. See its comment for why
         * the derivation is not repeated here.
         */
        const { actions, primary, destructive, secondary } = actionTiers(item);

        /*
         * THE CARD ALWAYS SPEAKS. It used to render nothing at all when no action applied, leaving a heading
         * over blank space — which reads as a page that failed to load rather than as a task that is finished
         * or not yours. One sentence costs 20px and answers the question the emptiness raises.
         */
        if (!actions.length) {
            return `<div class="wcn-detail-section">
                ${cardHead('bx-bolt-circle', 'ActionsAvailable')}
                <p class="wcn-block-hint wcn-act-none">${esc(t(isTerminal(item) ? 'ActionsNoneClosed' : 'ActionsNoneNotYours'))}</p>
            </div>`;
        }

        /*
         * ── WORK THAT CANNOT BE FINISHED HERE ─────────────────────────────────────────────────────────────
         *
         * The old "İşlem derinliği" row printed "Burada tamamlanır" on essentially every task — a field whose
         * value is the same everywhere carries no information. The information is in the OTHER case, and it is
         * not a technical detail at all: it is the answer to "why is there no Complete button?".
         *
         * `actionDepth` has exactly TWO values — measured, `ACTION_DEPTHS = ['inline', 'deeplink']` in
         * fixture-contract.js — so there is no third case to guess at. On `deeplink` the engine actions that
         * still apply here (ask, reassign) stay, and the LEAD becomes a link into the owning module.
         *
         * It is still exactly one filled button: this replaces the primary rather than joining it, so the card's
         * one-fill rule is untouched.
         *
         * The contract guarantees the destination — `DEEPLINK_REQUIRED` refuses a deeplink item with no
         * `source.deepLink` — so this cannot render a button that goes nowhere.
         */
        /*
         * THE RESOLVER DECIDES THIS, not this file. `surfaceMode === 'deeplink'` is the resolved answer to
         * "can this be finished here", derived once in task-detail-resolver.js from `actionDepth`. Re-deriving
         * it from `item.actionDepth` looked equivalent and was not: the presentation mapper does not carry that
         * field through, so the local test read `undefined` and the branch never fired. One model, consumed.
         */
        const depthLink = surface?.surfaceMode === 'deeplink'
            ? (item.source?.deepLink || item.deepLink || item.sourceDeepLink)
            : null;
        if (depthLink) {
            const moduleName = item.sourceModuleName || item.sourceModule || '';
            const rest = actions.filter((a) => !a.destructive);
            const destructiveOnly = actions.filter((a) => a.destructive);
            return `<div class="wcn-detail-section wcn-acts${locked ? ' wcn-acts-locked' : ''}">
                <div class="wcn-acts-main">
                ${cardHead('bx-bolt-circle', 'ActionsAvailable')}
                <ul class="wcn-actrail">
                    <li class="wcn-act wcn-act-primary">
                        <a class="wcn-act-btn wcn-act-fill wcn-act-fill-accent" href="${esc(depthLink)}"
                           data-wcn-depth-link="${esc(item.id)}">
                            <i class="bx bx-link-external" aria-hidden="true"></i><span>${
                esc(tf('ActionCompleteInSource', moduleName))}</span>
                        </a>
                        <p class="wcn-act-outcome">${esc(tf('ActionCompleteInSourceHint', moduleName))}</p>
                    </li>
                    ${rest.length
                ? `<li class="wcn-acts-row">${actionRail(item, rest, 'secondary', locked)}</li>`
                : ''}
                </ul>
                </div>
                ${destructiveOnly.length
                ? `<div class="wcn-acts-destructive">${
                    actionRail(item, destructiveOnly, 'destructive', locked)}</div>`
                : ''}
            </div>`;
        }


        /*
         * DESTRUCTIVE ACTIONS ARE VISIBLE, and the kebab is empty until something genuinely rare needs it.
         *
         * "Görevi iptal et" lived in a "Diğer aksiyonlar" menu. Folding a destructive act away is not a safety
         * measure — the reader who wants it hunts for it, and the reader who does not is no safer, because the
         * thing that actually protects them is the confirm dialog, which is where the warning sentence now
         * lives. What the menu did buy was a page that could cancel a task without ever showing the word.
         */
        return `<div class="wcn-detail-section wcn-acts${locked ? ' wcn-acts-locked' : ''}">
            <div class="wcn-acts-main">
            ${cardHead('bx-bolt-circle', 'ActionsAvailable')}
            <ul class="wcn-actrail">
                ${primary ? actionButton(item, primary, 'primary', locked) : ''}
                ${secondary.length
            ? `<li class="wcn-acts-row">${actionRail(item, secondary, 'secondary', locked)}</li>`
            : ''}
            </ul>
            </div>
            ${destructive.length
            ? `<div class="wcn-acts-destructive">${actionRail(item, destructive, 'destructive', locked)}</div>`
            : ''}
        </div>`;
    };

    /*
     * The lifecycle strip, drawn from THIS task's own route rather than a fixed set of stages.
     *
     * Steps that do not apply are not drawn at all: a task needing no review must not show a review step greyed
     * out, because a step you can see is a step you expect to reach. Approval leads the strip when it is
     * required — it gates starting, so it comes before the work.
     *
     * "Planned" is optional and marked as such; "Waiting" is NOT a step. Waiting is a pause ON the current step
     * (you are still mid-flight, blocked), so it renders as a badge over the strip rather than a station nobody
     * passes through on a healthy task.
     */
    const renderLifecycleStepper = (item) => {
        if (item.itemType !== 'task') { return ''; }
        const gates = item.gates;
        const steps = [];
        if (gates?.approval?.required) { steps.push({ key: 'StepApproval', id: 'approval' }); }
        steps.push({ key: 'StepOpen', id: 'open' });
        steps.push({ key: 'StepPlanned', id: 'planned', optional: true });
        steps.push({ key: 'StepInProgress', id: 'inProgress' });
        if (gates?.review?.required) { steps.push({ key: 'StepReview', id: 'review' }); }
        steps.push({ key: 'StepDone', id: 'done' });

        const cancelled = item.lifecycle === 'Cancelled';
        // Waiting is a pause, so the strip still shows where the work actually stands.
        const positionFor = {
            Open: 'open', Planned: 'planned', InProgress: 'inProgress',
            Waiting: 'inProgress', PendingReview: 'review', Done: 'done', Cancelled: 'open'
        };
        let activeId = positionFor[item.lifecycle] || 'open';
        // An outstanding approval is where the task really is, whatever its own lifecycle says.
        if (gates?.approval?.status === 'pending') { activeId = 'approval'; }
        // A review step only exists when required; fall back so the marker never lands on a step not drawn.
        if (!steps.some((step) => step.id === activeId)) { activeId = 'inProgress'; }

        const activeIndex = steps.findIndex((step) => step.id === activeId);
        const allDone = item.lifecycle === 'Done';

        /*
         * ONE VISIBLE NAME, FOUR ACCESSIBLE ONES.
         *
         * The four station names are the SAME on every task in the system — Açık → Planlandı → Devam → Tamam —
         * so after the second task they are furniture: 22px of label carrying a sequence the reader already
         * knows. Only the station the work is standing at gets a visible name; the rest are dots on a rail.
         *
         * ⚠ THIS IS A VISUAL ABBREVIATION AND NOT AN ACCESSIBILITY ONE. Every step keeps its name in the
         * accessibility tree via `visually-hidden`, because the reason a sighted reader can drop them — having
         * seen the same four names on the previous task — is exactly what a screen-reader user does not get for
         * free. Deleting the label instead of hiding it would take the strip's entire meaning from them.
         */
        const currentIndex = activeIndex;
        const rendered = steps.map((step, index) => {
            let cls = 'upcoming';
            if (!cancelled) {
                if (allDone || index < activeIndex) { cls = 'done'; }
                else if (index === activeIndex) { cls = 'active'; }
            }
            /*
             * `aria-current="step"` is the ONLY machine-readable statement of where the work stands. Before this,
             * position lived exclusively in a CSS class name (`wcn-step-active`) — invisible to assistive tech —
             * so a screen reader read four station names in order and learned nothing about which one the task
             * was at. A cancelled task is at no step at all, so it asserts none.
             */
            const isCurrent = index === currentIndex && !cancelled;
            /*
             * NO DOT ANY MORE. The station was a 24px circle holding a tick or an ordinal, joined by a 2px
             * connector — and the ordinal was leaking into the accessible name (measured: the second step
             * announced as "2 Planlandı"). The <li> IS the mark now: a segment of the bar, coloured by state.
             * Nothing decorative is left inside it to leak, which is a stronger guarantee than hiding one.
             *
             * State as WORDS, not only as colour — and this is now the ONLY carrier of the distinction, since
             * the bar says done/current/upcoming purely in hue. Green-means-done is unavailable to a screen
             * reader and to a reader who cannot separate those hues.
             */
            const stateKey = cls === 'done' ? 'StepStateDone'
                : cls === 'active' ? 'StepStateCurrent'
                    : 'StepStateUpcoming';
            /*
             * The step's NAME stays in the tree for all four, exactly as before. The visual shortening went one
             * step further — no station name is drawn at all now, the caption above carries the human-readable
             * statement — but the accessibility tree is unchanged: name + state, four times, one aria-current.
             */
            return `<li class="wcn-step wcn-step-${cls}${step.optional ? ' wcn-step-optional' : ''}"${
                isCurrent ? ' aria-current="step"' : ''}>
                <span class="wcn-step-label visually-hidden">${esc(t(step.key))}</span>
                <span class="visually-hidden">${esc(t(stateKey))}</span>
            </li>`;
        }).join('');

        /*
         * THE CAPTION — everything the bar cannot say in colour, said once ABOVE it.
         *
         * The bar carries POSITION and nothing else: four segments, three colours. That is enough to glance at
         * and useless to read, so the sentence that names the position sits on top of it, where it is read
         * first and the bar becomes its illustration. (It used to sit at the END of the old rail, which worked
         * while the rail had station labels of its own; with the labels gone it would have been a stray word
         * floating to the right of a graphic.)
         *
         * (1) THE STATUS NAME. Formerly a badge in the identity row — "where this work stands" filed among
         * "what this record is". No longer suppressed as a duplicate: with no station names drawn, there is
         * nothing on screen for it to repeat.
         *
         * (2) HOW FAR ALONG, as n/total. The bar shows this by construction; the caption says it, because
         * counting filled segments is a task and reading a number is not. NOT shown on a cancelled task — its
         * position marker sits at the first step by convention, and "1/4" would state a progress that called-off
         * work never made.
         *
         * (3) WHEN IT CLOSED. `closedAt` has been on the wire, normalised, and used to freeze the SLA count for
         * rounds, and was drawn on this page ZERO times. The date is printed as it ARRIVES: there is no date
         * formatter in this file to choose, because dates are normalised to `YYYY-MM-DD` at the projection seam
         * (`work-items-api.js` toDateOnly) and render sites print them through `esc()` — the same way `dueAt`
         * renders two cards down.
         */
        const closedOn = isTerminal(item) && item.closedAt
            ? `<span class="wcn-stepbar-closed">${esc(tf('StepClosedOn', item.closedAt))}</span>`
            : '';
        // Punctuation, not language — `aria-hidden` for the same reason the identity line's separator is:
        // a screen reader gains nothing from "em dash" between two facts it already reads as two.
        const captionSep = '<span class="wcn-stepbar-sep" aria-hidden="true">—</span>';
        const progress = cancelled || currentIndex < 0
            ? ''
            : `${captionSep}<span class="wcn-stepbar-count">${currentIndex + 1}/${steps.length}</span>`;
        const stepCaption = `<div class="wcn-stepbar-caption">
            <span class="wcn-stepbar-status">${esc(statusLabel(item))}</span>${progress}${
            closedOn ? captionSep + closedOn : ''}</div>`;

        const paused = item.lifecycle === 'Waiting'
            ? `<p class="wcn-step-paused" role="note"><i class="bx bx-pause-circle"></i>${
                esc(waitingSentence(item) ? tf('StepPausedBecause', waitingSentence(item)) : t('StepPaused'))}</p>`
            : '';

        /*
         * NO HEADING. It read "YAŞAM DÖNGÜSÜ" in 22px above a strip of four labelled stations — a title naming
         * what the thing below it already unmistakably is. The strip is self-describing; the heading was
         * 22px of the card's 177px spent restating it.
         *
         * The name survives where it is actually needed: as the ordered list's `aria-label`, because a screen
         * reader meeting a bare list of four items DOES need to be told what the list is. The resource key is
         * unchanged and still in all seven languages — the words moved, they were not deleted.
         */
        return `<div class="wcn-detail-section wcn-stepbar">
            ${stepCaption}
            <ol class="wcn-steps${cancelled ? ' wcn-steps-cancelled' : ''}"
                aria-label="${esc(t('StepBarLabel'))}">${rendered}</ol>
            ${paused}
        </div>`;
    };

    /*
     * `summaryFact` LIVED HERE and is gone: the icon-plus-label-plus-value tile it built was the three-column
     * grid's unit, and the grid was replaced by a definition list. It had no other caller — measured before
     * deleting — and leaving an abandoned builder in place is an invitation to rebuild the shape it made.
     *
     * Its one rule survives, restated in `renderSummary`: EMPTY FIELDS ARE NOT PRINTED. A "Son tarih: —" row is
     * a claim that the value was checked and found empty, which the reader cannot tell from a value that failed
     * to load.
     */

    /*
     * THE SUMMARY, IN THE PRODUCT'S OWN FIELD PATTERN.
     *
     * Two shapes were tried and both were this card's own invention: a three-column tile grid (which orphaned a
     * fourth fact on a second row) and a definition list (which used a 690px card as a 350px column and left the
     * right half empty).
     *
     * The third shape is not an invention. `Views/DevEnablement/GoldenReferenceCompact/Details.cshtml` — the
     * reference every Compact details page in this product is built from — reads:
     *
     *     <div class="col-12 col-md-6">
     *       <div class="backbone-preview-field">
     *         <i class="bx …"></i>
     *         <div><div class="backbone-preview-label">…</div><div class="backbone-preview-value mt-1">…</div></div>
     *       </div>
     *     </div>
     *
     * Two columns, icon at the left, label over value. This card already sits in `backbone-preview-section`; it
     * simply had never used the field pattern that section was designed around. No new class is written here.
     *
     * ONE DELIBERATE DEVIATION from the golden reference: it prints "-" for an empty value and we print NOTHING.
     * A dash claims the field was checked and found empty, which the reader cannot tell from a value that failed
     * to load. Recorded as a knowing divergence (BL-117) rather than a drift.
     */
    /*
     * WHAT THIS TASK EMAILS — one sentence, built from a WHOLE pattern per language.
     *
     * Three patterns rather than one with a switch inside, because the three answers are genuinely different
     * sentences and a language may want them ordered differently. Nothing is joined in JavaScript: the reminder
     * lead is a SLOT in the pattern, not a clause glued on the end — a language that puts "3 gün önce" first can
     * do so, and one that has no room for it can leave the slot where it belongs.
     *
     * The reminder half is folded into the SAME sentence rather than sitting beside it: "e-posta açık" and "3
     * gün önce hatırlatır" on two rows makes the reader join them, and the join is where the contradiction
     * ("kapalı" + "hatırlatır") became readable.
     */
    const notificationSentence = (notifications, reminderLeadDays) => {
        const events = notifications.events;
        const scope = events === null || events === undefined
            // NOBODY CHOSE. Everything dispatchable is sent — the entity's own rule, and the reason the field is
            // nullable at all.
            ? t('NotificationsAllEvents')
            : events.length === 0
                // They chose NONE. The master switch is still on, so the distinction is real and worth saying.
                ? t('NotificationsNoEvents')
                : tf('NotificationsSomeEvents', events.length);

        return reminderLeadDays === null || reminderLeadDays === undefined
            ? tf('NotificationsOn', scope)
            : tf('NotificationsOnWithReminder', scope, reminderLeadDays);
    };

    const renderSummary = (item) => {
        /*
         * ICONS ARE THE CREATE FORM'S — measured, field by field, from `Views/Tasks/_Form.cshtml`. One field must
         * not carry two icons across two screens: a reader who set a priority behind a flag should meet a flag
         * when they come back to read it.
         *
         *   Atanan    bx-user               (taskAssignee)
         *   Başlangıç bx-calendar           (taskStartAt)
         *   Son tarih bx-calendar           (taskDueAt — the form uses the SAME glyph for both dates and tells
         *                                    them apart by label; copied rather than "improved")
         *   Öncelik   bx-flag               (taskPriority)
         *   Tahmini   bx-time-five          (taskEstimateHours)
         *   Etiketler bx-purchase-tag-alt   (taskTags)
         *   Açıklama  bx-align-left         (taskDescription)
         *
         * `Talep eden` is the ONE field with no counterpart — the create form has no requester input, because
         * the requester is whoever is filling it in. It cannot copy an icon, and it cannot keep `bx-user` now
         * that the assignee has claimed it, so it takes `bx-user-pin`: the person who pinned this on you.
         */
        const field = (icon, labelKey, value, tone, wide) => (value === null || value === undefined || value === ''
            ? ''
            : `<div class="col-12 ${wide ? '' : 'col-md-6'}">
                <div class="backbone-preview-field${tone ? ' ' + tone : ''}">
                    <i class="bx ${icon}" aria-hidden="true"></i>
                    <div>
                        <div class="backbone-preview-label">${esc(t(labelKey))}</div>
                        <div class="backbone-preview-value mt-1">${esc(value)}</div>
                    </div>
                </div>
            </div>`);

        /*
         * THE ONE EXCEPTION TO "no value, no field": WHO HOLDS THIS.
         *
         * An unassigned task is not a missing field, it is the FACT whose consequence is that nothing happens
         * until somebody notices. It says so in a word rather than with a dash, because "—" reads as "not
         * recorded" and this is recorded: it is recorded as nobody.
         */
        const assignee = field('bx-user', 'DetailAssignee', item.assignee || t('SummaryUnassigned'),
            item.assignee ? '' : 'backbone-preview-field-muted');

        /*
         * THE LATE DUE DATE IS RED IN THE WHOLE FIELD — icon, label and value — not only in the number.
         * Colouring one of three parts reads as a typo; colouring the field reads as a state. Source unchanged:
         * `item.slaState === 'overdue'`, the projection's own verdict, which the dissolved Status card used.
         */
        const overdue = item.slaState === 'overdue' ? 'backbone-preview-field-overdue' : '';

        /*
         * THE DESCRIPTION IS A FIELD NOW, with its own label.
         *
         * MEASURED on two tasks created for the purpose: a task WITH a description projects
         * `summary = { kind:"display", text:"…" }`; a task WITHOUT one projects `summary = null`. There is no
         * generated fallback — the provider emits the description or nothing at all
         * (`TaskWorkItemProvider`: `IsNullOrWhiteSpace(task.Description) ? null : Display(task.Description)`).
         *
         * So the sentence was never ambiguous in the data, only on screen: it rendered as an unlabelled
         * paragraph that could be mistaken for a status line — and on seed data whose description literally
         * reads "Kabul bekliyor.", it was. A label settles it.
         */
        const description = field('bx-align-left', 'DetailDescription', item.summary, '', true);

        /*
         * ── THE PLANNED DATE, MOVED HERE FROM THE PERSONAL CARD (BL-141, owner decision) ─────────────────────
         *
         * It sat under a heading that said "Kişisel" and it is not personal: measured on `TaskItem` (the shared
         * task row), projected as a top-level field, read back by the requester, and a plan write moves the
         * shared lifecycle to `Planned`. So it belongs beside the other shared dates.
         *
         * IT IS CLICKABLE, and it opens the SAME editor "Planla" opens — because it carries the SAME
         * `data-wcn-action="plan"` the action button carries, and the page's one action handler routes it. Two
         * entrances to one job is fine; two MECHANISMS for one job is what drifts. Nothing new was written here:
         * no second picker, no second submit path, no second validation.
         *
         * NOT clickable when the projection does not offer `plan` (a task somebody else holds, a closed one) —
         * the row then states the date and nothing more, rather than offering a control the server would refuse.
         *
         * ⚠ NO ROW WHEN THERE IS NO PLAN. That is the Summary's own rule (a row is printed for a fact that
         * exists), and it is also the honest one here: `plan` is ALREADY offered as an action — measured live on
         * a task with no plan at all, "Planla" appears in the actions card AND in the narrow-screen bar. A third
         * invitation to the same job would be a third copy of one button, not a discovery aid.
         */
        /*
         * ── WHOSE SUBTASK THIS IS — moved out of a card of its own (2026-08-14) ──────────────────────────────
         *
         * MEASURED: it was a card with NO heading, 73px tall, whose entire content was one sentence — "'Q3 nakit
         * akış projeksiyonunu onayla' görevinin alt görevi". A card is a container for a group of facts; this was
         * one fact wearing a container. And it is a fact ABOUT THE TASK, which is what the Summary is for.
         *
         * THE LINK IS THE POINT, and it was broken: the old markup pointed at `?id=…`, a query string this page
         * does not read — the detail route is `/WorkCenterNext/Details/{id}`, so clicking it reloaded the SAME
         * task with a query nobody parses. Measured live before the move. It goes to the real route now.
         *
         * The glyph is `bx-subdirectory-left`: the mirror of the `bx-subdirectory-right` the old notice used, and
         * grep-proven unused anywhere in the product (`bx-subdirectory-right` itself is taken — the Positions
         * tree draws its descendants with it). Right branches DOWN to a child; left comes BACK to a parent, which
         * is the direction this row actually points.
         */
        const parentTask = !item.parentTaskItemId ? '' : (() => {
            const parent = itemById(item.parentTaskItemId);
            // The parent's title when this reader can see it; its own honest fallback when they cannot. Never a
            // GUID — an id is not a name, and the row is here to say WHICH task.
            const label = parent ? parent.title : t('SubtaskOfUnnamed');
            return `<div class="col-12 col-md-6">
                <div class="backbone-preview-field">
                    <i class="bx bx-subdirectory-left" aria-hidden="true"></i>
                    <div>
                        <div class="backbone-preview-label">${esc(t('DetailParentTask'))}</div>
                        <div class="backbone-preview-value mt-1">
                            <a href="/WorkCenterNext/Details/${esc(item.parentTaskItemId)}">${esc(label)}</a>
                        </div>
                    </div>
                </div>
            </div>`;
        })();

        /*
         * ── WHO ELSE IS WATCHING (2026-08-23) ───────────────────────────────────────────────────────────────
         *
         * Collected by the create form since Phase 1 and shown on no surface until now: the form could name
         * watchers and nothing could name them back.
         *
         * ⚠ THE ROLE IS NOT A CHIP. On this page a chip is a SIGNAL — overdue, priority, blocked — something that
         * changes what you do next. "Consultant" changes nothing; it qualifies a name. As a chip it would compete
         * with the two signals that actually matter on the same card, so it is quiet secondary text after the
         * name, in the same weight the rest of the field's supporting text uses.
         *
         * FULL WIDTH and BELOW the two people fields, because it is a LIST — a variable number of rows cannot sit
         * in a half-width grid cell without either clipping or stretching the field beside it. Same reasoning the
         * tag strip already carries.
         *
         * No watchers, no field — the Summary's own rule.
         */
        const watchers = Array.isArray(item.watchers) ? item.watchers : [];
        const watcherField = !watchers.length ? '' : `<div class="col-12">
                <div class="backbone-preview-field">
                    <i class="bx bx-show" aria-hidden="true"></i>
                    <div>
                        <div class="backbone-preview-label">${esc(t('DetailWatchers'))}</div>
                        <ul class="wcn-watchers mt-1">${watchers.map((watcher) => {
            // A name we could not resolve is stated as unavailable rather than printed as an id — the module's
            // standing rule, and the same label the comment feed uses for the same situation.
            const name = watcher.person?.displayName || t('PersonNameUnavailable');
            /*
             * The role is dropped for a plain Watcher: "Ayşe Yılmaz (izleyici)" in a list headed "İzleyiciler"
             * says the same word twice. Only the two roles that ADD something are spoken.
             */
            const role = watcher.role && watcher.role !== 'Watcher'
                ? `<span class="wcn-watcher-role">${esc(t('WatcherRole' + watcher.role))}</span>`
                : '';
            return `<li class="wcn-watcher"><span class="wcn-watcher-name">${esc(name)}</span>${role}</li>`;
        }).join('')}</ul>
                    </div>
                </div>
            </div>`;

        /*
         * ── WHAT THIS TASK EMAILS, AS ONE SENTENCE (2026-08-23) ─────────────────────────────────────────────
         *
         * ⚠ ONE ROW, NOT TWO. "E-posta kapalı" beside "3 gün önce hatırlatır" reads as a contradiction — the
         * reader has to work out which one wins. The master switch decides whether there is anything else to say
         * at all, so the sentence is built from it.
         *
         * ⚠ `events` NULL AND [] ARE DIFFERENT ANSWERS, and the sentence tells them apart:
         *     absent  → nobody ever chose, so everything is sent          → "…tüm olaylar için"
         *     []      → the owner chose none                              → "…seçili olay yok"
         *     [a, b]  → they chose some                                   → "…2 olay için"
         *   Collapsing them would either silence a task nobody configured or claim a choice nobody made — the
         *   distinction the projection went out of its way to preserve.
         */
        const notifications = item.notifications;
        const notificationField = !notifications ? '' : (() => {
            const value = !notifications.emailEnabled
                // Nothing is sent, so a reminder lead is not a fact about this task — it is a setting with no
                // effect, and printing it would invite the reader to believe a reminder is coming.
                ? t('NotificationsOff')
                : notificationSentence(notifications, item.reminderLeadDays);
            return `<div class="col-12 col-md-6">
                <div class="backbone-preview-field">
                    <i class="bx bx-envelope" aria-hidden="true"></i>
                    <div>
                        <div class="backbone-preview-label">${esc(t('DetailNotifications'))}</div>
                        <div class="backbone-preview-value mt-1">${esc(value)}</div>
                    </div>
                </div>
            </div>`;
        })();

        const planAction = actionByKey(item, 'plan');
        const planClickable = !!planAction && !planAction.disabled && !isTerminal(item);
        const plannedConflict = item.dueAt && item.plannedDate && item.plannedDate > item.dueAt;
        const planned = !item.plannedDate ? '' : `<div class="col-12 col-md-6">
                <div class="backbone-preview-field wcn-sumfield-plan${
            plannedConflict ? ' backbone-preview-field-overdue' : ''}"${
            planClickable
                ? ` role="button" tabindex="0" data-wcn-action="plan" data-wcn-id="${esc(item.id)}"` : ''}>
                    <i class="bx bx-calendar-check" aria-hidden="true"></i>
                    <div>
                        <div class="backbone-preview-label">${esc(t('DetailPlannedDate'))}</div>
                        <div class="backbone-preview-value mt-1">${esc(item.plannedDate)}</div>
                        ${plannedConflict
                ? `<p class="wcn-date-warn mb-0" role="note"><i class="bx bx-error-circle" aria-hidden="true"></i>${
                    esc(t('PlanConflict'))}</p>`
                : ''}
                    </div>
                </div>
            </div>`;

        // People → time → classification. A reader asks "whose is this" before "when", and "when" before "how big".
        const fields = description
            + assignee
            + field('bx-user-pin', 'DetailRequester', item.requester)
            + field('bx-calendar', 'DetailStartAt', item.startAt)
            + field('bx-calendar', 'SourceDueLabel', item.dueAt, overdue)
            + planned
            + field('bx-flag', 'DetailPriority', hasPriority(item) ? priorityLabel(item) : '')
            + parentTask
            + notificationField
            + field('bx-time-five', 'DetailEstimate', item.estimateHours === null || item.estimateHours === undefined
                ? '' : tf('EstimateHoursValue', item.estimateHours))
            // LAST among the fields and full width — see its own comment for why a list cannot share a row.
            + watcherField;

        /*
         * TAGS LAST, FULL WIDTH, UNDER A RULE. A collection of variable length cannot sit in a fixed grid cell
         * without either clipping or stretching the row beside it; the rule marks it as a different KIND of
         * fact rather than a seventh value.
         */
        const tags = Array.isArray(item.tags) && item.tags.length
            ? `<div class="wcn-sumtags">
                <div class="backbone-preview-field">
                    <i class="bx bx-purchase-tag-alt" aria-hidden="true"></i>
                    <div>
                        <div class="backbone-preview-label">${esc(t('DetailTags'))}</div>
                        <div class="wcn-sumval-tags mt-1">${
                item.tags.map((tag) => `<span class="wcn-tag">${esc(tag)}</span>`).join('')}</div>
                    </div>
                </div>
            </div>`
            : '';

        if (!fields && !tags) { return ''; }

        /*
         * TWO BLOCKS, so the tag divider can reach the card's edges without a negative margin: the card holds no
         * padding, `wcn-sum-main` pays for the fields' inset and `wcn-sumtags` pays for its own.
         */
        return `<div class="wcn-detail-section wcn-sum">
            <div class="wcn-sum-main">
                ${cardHead('bx-info-circle', 'SummaryCardLabel')}
                <div class="row g-4">${fields}</div>
            </div>
            ${tags}
        </div>`;
    };

    // ── Capability-driven depth blocks (spec v3 §5) — do-the-work in the
    // aggregator; define-the-work stays in the source (deep-link). ─────────────
    const hasCap = (item, cap) => Array.isArray(item.workItemCapabilities) && item.workItemCapabilities.indexOf(cap) >= 0;

    /*
     * ADDING an item, on the SAME row shape the subtask card uses: 38px, icon inside, Enter commits. A third
     * add pattern in one product is how a product starts reading as three.
     *
     * The one thing it adds beside the input is the LEVEL, because the level is the only part of a checklist
     * item that changes what the task DOES: a Blocking item refuses `complete`. An add row that could not
     * express it would ship the half of the feature that does nothing.
     *
     * Absent on a closed task — its checklist is history, and the server refuses the write too.
     */
    /*
     * The add row, from the SAME component that draws the item rows.
     *
     * This screen used to have an icon, an input and a level chip, and no button and no hint — so Enter was the
     * only way to commit, and the only thing that said so was the placeholder, which disappears the moment you
     * start typing. Enter still works; the button is the half of the pair that can be seen.
     */
    const checklistAddRow = (item) => global.DitenCheckItem.addRow({
        id: item.id,
        // Kept across consecutive adds, so somebody entering three blocking steps chooses the level once.
        level: state.checklistDraftLevel,
        labels: {
            optional: t('ChecklistLevelOptional'),
            required: t('ChecklistLevelRequired'),
            blocking: t('ChecklistLevelBlocking'),
            levelHint: t('ChecklistLevelHint'),
            addPlaceholder: t('ChecklistAddPlaceholder'),
            addButton: t('ChecklistAddButton'),
            addHint: t('ChecklistAddHint')
        }
    }).outerHTML;

    /*
     * OPEN "REQUIRED" ITEMS — the ones that must be done and do NOT stop the task.
     *
     * Counted WITHOUT the blocking ones, even though a blocking item is mandatory too. On the wire a Blocking
     * item carries `required: true` as well (ToChecklist derives it as "not Optional"), so counting naively
     * would report the same item twice in two different sentences — one saying the task cannot close, the other
     * saying it can. The blocking notice speaks for those; this speaks for the rest.
     */
    const openRequiredItems = (item) =>
        ((item && item.checklist && item.checklist.items) || [])
            .filter((entry) => entry.required && !entry.blocking && !entry.done);

    // Checklist — interactive (checking is "doing the work", stays here).
    const renderChecklist = (item) => {
        // Capability present but empty is a VALID state (the contract requires the container), and it is now the
        // ORDINARY state: the provider ships the container for every task so a first item can be added here.
        if (!hasCap(item, 'checklist') || !item.checklist) { return ''; }
        const items = item.checklist.items || [];
        const canAdd = !isTerminal(item);
        if (!items.length) {
            /*
             * Empty AND unaddable is the only case that gets a bare sentence. Otherwise the add row IS the empty
             * state: a line saying "there is nothing here" above a box for putting something there is noise, and
             * this card is the only place the capability can be discovered at all.
             */
            return `<div class="wcn-detail-section">
                ${cardHead('bx-list-check', 'ChecklistLabel')}
                ${canAdd ? checklistAddRow(item) : `<p class="wcn-block-hint">${esc(t('ChecklistEmpty'))}</p>`}
            </div>`;
        }
        const done = items.filter((c) => c.done).length;
        const ro = isTerminal(item);
        /*
         * THE ROW IS NO LONGER DRAWN HERE.
         *
         * It was: a `<li class="wcn-check">` with a tick box, the text, and a paperclip that was a MARK rather
         * than a control — while the create form drew the same item with a level chip, an evidence toggle, move
         * buttons and a remove. Same object, two components, two vocabularies, and a widening gap between what
         * you could decide before the task existed and what you could decide once you were doing it.
         *
         * `DitenCheckItem` draws it once. This page asks for WORKING mode, which adds the tick box the create
         * form has no use for, and gets everything else identical by construction rather than by discipline.
         * `.outerHTML` because this file builds strings — the component builds nodes so that the one field
         * carrying typed text is set with `textContent` and cannot become markup.
         */
        const rows = items.map((c, index) => global.DitenCheckItem.row(
            {
                id: `${item.id}:${c.id}`,
                text: c.text,
                requirement: c.requirement,
                evidenceRequired: c.evidenceRequired,
                done: c.done,
                // A template item's words belong to every task made from that template; the server refuses to
                // reword one, and the row says so rather than letting someone find out on reload.
                templateOwned: !!c.templateOwned,
                // Somebody else's step — or the process's. Its controls are not drawn; see the component.
                editable: c.editable !== false
            },
            {
                mode: 'working',
                // A closed task's checklist is history. The server refuses these writes too — this is the
                // courtesy, not the guard.
                readOnly: ro,
                labels: {
                    optional: t('ChecklistLevelOptional'),
                    required: t('ChecklistLevelRequired'),
                    blocking: t('ChecklistLevelBlocking'),
                    levelHint: t('ChecklistLevelHint'),
                    // The two read-only faces of the same two facts. `ChecklistLevelHint` is an instruction
                    // ("Change the level: …") and would be a lie on a chip nobody here can change; the mark's
                    // label is a statement rather than the button's verb, for the same reason.
                    levelStatic: t('ChecklistLevelReadOnly'),
                    evidenceMark: t('ChecklistEvidenceMark'),
                    moveUp: t('ChecklistMoveUp'),
                    moveDown: t('ChecklistMoveDown'),
                    evidenceToggle: t('ChecklistEvidenceToggle'),
                    remove: t('ChecklistRemove'),
                    toggle: t('ChecklistLabel')
                }
            }).outerHTML).join('');
        // The reason completion is unavailable must be READABLE on the page — a disabled button with only a
        // tooltip leaves a keyboard or touch user with no explanation at all.
        const blocked = items.some((c) => c.blocking && !c.done);
        const notice = blocked
            ? `<p class="wcn-block-hint" role="note"><i class="bx bx-error-circle"></i>${esc(t('WorkAggregation_ActionDisabled_ChecklistIncomplete'))}</p>`
            : '';
        /*
         * SIGNAL (a) FOR "REQUIRED" — the level that was stored and did nothing.
         *
         * Three levels shipped and two behaved: Blocking stopped completion, Optional was meant to do nothing,
         * and Required was indistinguishable from Optional anywhere on screen. A user chose it and the system
         * ignored it — the same "stored but inert" class this module has had to fix repeatedly.
         *
         * Quiet by construction: it states a count and nothing more. It must NOT look like the blocking notice,
         * because the whole point of the level is that the task can still close.
         *
         * That defence used to be STYLE only, and style was the wrong layer: the sentence above this one says
         * the list does not block completion, while this one said "4 REQUIRED items open", and "required" means
         * mandatory in ordinary speech — in every one of the seven languages we ship. The reader believed the
         * word and not the disclaimer sitting directly above it. The word is now EXPECTED / Beklenen, which is
         * what the level has always done: ask on completion, never stop it. The key name, the `required` wire
         * value and the enum are UNCHANGED — this was never a contract problem, only a vocabulary one.
         */
        const openRequired = openRequiredItems(item);
        const requiredNotice = openRequired.length
            ? `<p class="wcn-block-hint wcn-check-required" role="note">
                <i class="bx bx-info-circle"></i>${esc(tf('ChecklistRequiredOpen', openRequired.length))}</p>`
            : '';
        // Said once, under the list, when anything on it carries the flag — the per-row paperclip is the mark
        // and this is the sentence that explains what the mark will mean.
        const evidenceHint = items.some((c) => c.evidenceRequired)
            // An ALERT, matching the create form and the two gates on this page. It reports a condition the
            // reader did not create and cannot yet act on; a hint line reads as description instead.
            ? `<div class="alert alert-secondary dt-inline-alert diten-checkitem-evidencehint" role="note">
                <i class="bx bx-paperclip"></i><span>${esc(t('ChecklistEvidenceHint'))}</span></div>`
            : '';
        return `<div class="wcn-detail-section">
            ${cardHead('bx-list-check', 'ChecklistLabel', `<span class="wcn-count-inline">${done}/${items.length}</span>`)}
            <p class="wcn-block-hint">${esc(t(items.some((c) => c.blocking) ? 'ChecklistBlocksCompletion' : 'ChecklistDoesNotBlock'))}</p>
            <progress class="wcn-progress" value="${done}" max="${items.length}" aria-label="${esc(t('ChecklistLabel'))}"></progress>
            ${/*
               * CAPPED, like its two siblings. MEASURED: 6 items render 294px un-capped, so a 20-item checklist
               * would be ~1000px of one card — the unbounded growth the cap exists for.
               *
               * THRESHOLD: the same `cappedList` helper and the same 320px scroll box the subtask list and the
               * activity feed use. A checklist row (38px) and a subtask row (38px) are the same height, so the
               * cap shows the same number of rows on both — which is the point of reusing it rather than
               * choosing a third number. `aria-expanded` and the region label come with the helper.
               */''}
            ${items.length > CHECKLIST_CAP
            ? cappedList('checklist', `<ul class="wcn-checks">${rows}</ul>`, items.length)
            : `<ul class="wcn-checks">${rows}</ul>`}
            ${notice}
            ${requiredNotice}
            ${canAdd ? checklistAddRow(item) : ''}
            ${/*
               * THE PAPERCLIP'S PROMISE, said LAST.
               *
               * It sat above the add row, which put a sentence about what the paperclip will mean between the
               * list and the box for adding to the list — cutting the card's one continuous action in half. It
               * explains a control that lives on the rows AND on the add row, so under both is where it reads
               * as a footnote rather than as an interruption.
               */''}
            ${evidenceHint}
        </div>`;
    };


    /*
     * What must happen before this work can proceed, and where that stands.
     *
     * REPORTING ONLY. There is deliberately no approve/reject control here: the decision belongs to MOD-0023
     * (charter Binding A), and MOD-0024 has already been caught once growing a second approval engine. This card
     * answers "why is this waiting, and on whom" — nothing else.
     *
     * Absent gates object → no card. A gate that is not required is still SHOWN, because "no approval needed" is
     * an answer the holder wants; it is the difference between a gate that is satisfied and one that never
     * applied.
     */
    const GATE_STATUS_KEY = {
        notRequired: 'GateStatusNotRequired',
        required: 'GateStatusRequired',
        pending: 'GateStatusPending',
        approved: 'GateStatusApproved',
        rejected: 'GateStatusRejected'
    };

    const gateRow = (labelKey, gate) => {
        if (!gate) { return ''; }
        /*
         * ⚠ REVERSES AN EARLIER DECISION, on the owner's call (2026-08-12).
         *
         * The old rule printed a notRequired gate too, arguing that "no approval needed" is itself an answer.
         * Measured on a real task that produced a full-height card reading "Onay: Gerekmiyor / İnceleme:
         * Gerekmiyor" — two lines saying nothing, above the fold, pushing the state that DOES apply below it.
         * A gate that never applied is not part of "where does this stand"; it is the absence of a gate.
         */
        if (gate.status === 'notRequired') { return ''; }
        const statusKey = GATE_STATUS_KEY[gate.status];
        // An unknown status renders nothing rather than a raw token: the projection is the authority on the
        // vocabulary, and inventing a label for a value we do not know is how gibberish reaches the screen.
        if (!statusKey) { return ''; }
        const who = gate.decider?.displayName
            ? `<span class="wcn-gate-who">${esc(gate.decider.displayName)}</span>`
            : '';
        return `<li class="wcn-gate wcn-gate-${esc(gate.status)}">
            <span class="wcn-gate-name">${esc(t(labelKey))}</span>
            <span class="wcn-gate-status">${esc(t(statusKey))}</span>
            ${who}
        </li>`;
    };

    /*
     * WHERE DOES THIS STAND — gates and dates, one card.
     *
     * They were two, and they answer the same question: a gate says what has to happen before the work may
     * proceed, a date says by when. Split across two cards the reader assembles the answer themselves, and on a
     * task with neither an approval nor a review the gates card was a full-height box whose whole content was
     * the word "Gerekmiyor" twice.
     *
     * REPORTING ONLY, unchanged (charter Binding A): no approve/reject control lives here. MOD-0024 has been
     * caught once growing a second approval engine; this card says why work is waiting and on whom, and stops.
     */
    /*
     * THE GATES CARD — and what it stopped being.
     *
     * It was called "Durum" and, on the task measured, its ENTIRE content was two dates: the source due date
     * and the personal plan. A card named for status, containing no status.
     *
     * Worse, it disagreed with the page: the same due date rendered RED here (`slaState === 'overdue'`) and
     * grey in the Summary card two columns away — one screen saying two things about one fact.
     *
     * The dates left. The due date went to the Summary, where its row already existed, and took the RED with
     * it (the red was the correct one). The personal plan went to the Personal card, which is where the
     * reader's own decisions live.
     *
     * ⚠ WHAT DID NOT LEAVE: the approval and review GATE rows. The brief's premise was that this card held
     * nothing but dates — true of the task in front of us, and not true of the card, which renders gate state
     * whenever a task has gates. Deleting it wholesale would have deleted those. With the dates gone it now
     * disappears on its own for any task without gates, which is the outcome that was actually wanted.
     */
    const renderStatusCard = (item) => {
        const gates = item.gates || {};
        const rows = gateRow('GateApproval', gates.approval) + gateRow('GateReview', gates.review);
        if (!rows) { return ''; }

        return `<div class="wcn-detail-section">
            ${cardHead('bx-pulse', 'StatusCardLabel')}
            <ul class="wcn-gates">${rows}</ul>
        </div>`;
    };

    /*
     * THE CAP, AND THE CONTROL THAT RELEASES IT — one helper, because the same pair is rendered twice.
     *
     * ⚠ MEASURED DEAD (2026-08-12): both cards drew `<button data-wcn-showall>` with an EMPTY value and there
     * was no click handler for that attribute anywhere. Seventeen rows stayed seventeen, 320px stayed 320px. A
     * control that is drawn but wired to nothing is worse than no control: it answers the reader's question
     * with silence.
     *
     * The button now carries its LIST KEY (an empty attribute cannot address anything), toggles rather than
     * disappears — hiding it would strand an opened list with no way back — and the expansion goes through the
     * ordinary render, so it inherits this round's scroll and focus preservation.
     */
    /*
     * When a checklist earns a cap. Below this the card simply shows everything, which is the ordinary case and
     * costs nothing; above it the list would grow without bound inside a card that has four other blocks.
     */
    const CHECKLIST_CAP = 8;

    const cappedList = (key, listHtml, total) => {
        const open = !!state.expandedLists[key];
        const body = open
            ? listHtml
            /*
             * A SCROLL REGION HAS TO BE REACHABLE AND ANNOUNCED.
             *
             * This was 320px of scrollable list with no `tabindex`, no `role` and no name: the keyboard could not
             * scroll it (nothing inside receives arrow keys until a row is focused, and the capped rows below the
             * fold cannot be reached to focus them), and a screen reader stepped into it without being told it had
             * entered anything. `tabindex="0"` + `role="region"` + the card's own heading as the name is the
             * standard shape for a scrollable box.
             */
            : `<div class="wcn-scrollcap" data-wcn-scrollcap tabindex="0" role="region"
                    aria-label="${esc(t(key === 'subtasks' ? 'SubtasksLabel' : 'ChecklistLabel'))}">${listHtml}</div>`;
        /*
         * `aria-expanded` — this is a TOGGLE (its own label flips between "show all" and "show less"), and a
         * toggle that does not say which state it is in leaves a screen-reader user pressing it to find out.
         */
        return `${body}
            <button type="button" class="btn btn-sm btn-label-secondary wcn-showall"
                    data-wcn-showall="${esc(key)}" aria-expanded="${open ? 'true' : 'false'}">
                ${esc(open ? t('ShowLess') : tf('ShowAllCount', total))}
            </button>`;
    };

    /*
     * HOW MANY ROWS A CARD SHOWS BEFORE IT SCROLLS ITSELF.
     *
     * MEASURED in the browser: a subtask row is 40px and an activity entry is two lines at ~56px. The rail
     * beside them (actions + status + note) runs ~520px on a live task. Eight subtasks ≈ 320px and five
     * activity entries ≈ 280px, so the two content cards together stay near the rail's height instead of
     * running past it and leaving the right-hand column stranded against a void.
     *
     * ⚠ A SCROLL, NOT A TAB, and not a truncation. Everything stays reachable in place; "show all" simply
     * releases the cap. A tab here would hide a gate — the very thing this page must never do.
     */
    const SUBTASK_VISIBLE_LIMIT = 8;
    // How long the just-added row stays marked. Long enough to be seen after the list repaints, short enough
    // that it never reads as a status.
    const SUBTASK_FLASH_MS = 2500;
    const ACTIVITY_VISIBLE_LIMIT = 5;

    /*
     * WHEN THE FEED EARNS A FILTER — measured against what a task's history actually costs.
     *
     * A task that goes straight through records six entries: created, accepted, planned, started, submitted for
     * review, completed. Put it down once and pick it back up and it is eight. That is the ORDINARY ceiling, so a
     * threshold anywhere under it would put a permanent control on every finished task — chrome that is present
     * for everyone and useful to no one. (Seven entries is the case that prompted this measurement; the filter
     * must not appear for it.)
     *
     * Twelve is one full pass PLUS a change of hands — reassigned, re-accepted, re-planned, restarted. At that
     * point the events genuinely outnumber the conversation and scanning for "what did someone SAY" is a real
     * question. Below it, the eye does the filtering faster than a click can.
     *
     * ⚠ CHIPS, NOT TABS. A tab would claim these are two lists with two owners; they are one story told by two
     * kinds of entry, and the axis law reserves tabs for ownership.
     */
    const ACTIVITY_FILTER_MIN_EVENTS = 12;

    // Subtasks — full: complete/add here; readonly: progress + "edit in source".
    const SUBTASK_ICON = {
        done: 'bxs-check-circle', 'in-progress': 'bx-loader-circle', 'not-started': 'bx-circle',
        cancelled: 'bx-x-circle'
    };
    const SUBTASK_STATUS_KEY = {
        done: 'SubtaskStatusDone',
        'in-progress': 'SubtaskStatusInProgress',
        'not-started': 'SubtaskStatusNotStarted',
        // Called-off work is not work waiting to begin — it reads as "not started" only if nobody says otherwise.
        cancelled: 'SubtaskStatusCancelled'
    };
    /*
     * ⚠ `renderParentContext` IS GONE (2026-08-14). It drew a card with no heading whose whole content was one
     * sentence naming the parent task — one fact in a container built for a group of them — and its link pointed
     * at `?id=…`, which this page's route does not read. The fact now lives in the Summary as a field, with a
     * link to the real detail route. See `parentTask` in `renderSummary`.
     */

    /*
     * THE ADD ROW — one helper, used by the list AND by the empty line, so the two cannot drift into two
     * different ways of adding a subtask.
     *
     * The input reuses the page's OWN search pattern (`wcn-search wcn-search-inline`): same 38px box, same
     * inset icon. Inventing a second input shape for the same page is how a surface starts looking assembled
     * rather than designed.
     *
     * ENTER submits — the placeholder says so, and the keydown handler reads the parent id off this input, so
     * there is exactly one control and no button that could disagree with it. "Add in detail" is WORDS, not a
     * glyph: a funnel means "filter" everywhere, and nothing means "add with more fields" anywhere.
     */
    const subtaskAddRow = (item) => `<div class="wcn-subtask-add">
            <div class="wcn-search wcn-search-inline">
                <i class="bx bx-plus" aria-hidden="true"></i>
                <input type="text" class="form-control shadow-none" data-wcn-subtask-input
                       data-wcn-subtask-add="${item.id}"
                       placeholder="${esc(t('SubtaskAddPlaceholder'))}"
                       aria-label="${esc(t('SubtaskAddPlaceholder'))}">
            </div>
            <!--
                OUTLINE primary, not solid, and the reason is the input beside it: Enter already carries the
                primary path, so a second solid button would ask the reader to choose between two controls that
                do the same job. Outline says "this also adds" in the primary hue without competing.
            -->
            <button type="button" class="btn btn-outline-primary"
                    data-wcn-subtask-add-detailed="${item.id}">${esc(t('SubtaskAddDetailed'))}</button>
        </div>`;

    /*
     * WHAT A QUICK-ADDED SUBTASK INHERITS — measured, and previously unsaid.
     *
     * Quick-add does not skip the required fields; it takes them from the parent (its due date, its priority,
     * its assignee — and SelfAssigned when the parent is a pool with no holder). The server requires a due date
     * on EVERY task, so this has always happened silently. Saying it also explains what "add in detail" is for:
     * inheriting the assignee means you cannot hand the child to somebody else from here.
     */
    const subtaskInheritHint = () =>
        `<p class="wcn-block-hint wcn-subtask-add-hint">${esc(t('SubtaskInheritsHint'))}</p>`;

    /*
     * WHY A ROW'S BOX IS DISABLED — never just greyed out. A disabled control with no reason is reported as a
     * bug, and on touch there is not even a tooltip to hover for.
     */
    const SUBTASK_CHECK_BLOCKED_KEY = {
        done: 'SubtaskCheckDoneReason',
        cancelled: 'SubtaskCheckCancelledReason'
    };

    /*
     * THE ROW MENU — built from what the SERVER said about THAT ROW, never from a fixed list.
     *
     * ⚠ MEASURED (2026-08-12): the projection states exactly two things per subtask — `status` and `canCancel`
     * (evaluated per row, because a child's requester is its own, not the parent's). It carries NO per-row
     * action set. So the row offers: OPEN (navigation, which needs no permission) and CANCEL (only when the
     * server said so). REASSIGN is deliberately absent — nothing on the wire says this actor may reassign this
     * child, and a button that comes back 409 is a defect this project has already shipped once.
     *
     * ONE action is not a menu: a ⋯ hiding a single item is a click nobody needs, so it renders as the action
     * itself.
     */
    const subtaskRowActions = (subtask) => {
        const actions = [
            {
                code: 'open',
                labelKey: 'SubtaskOpen',
                icon: 'bx-link-external',
                attrs: `data-wcn-open-task="${esc(subtask.id)}"`
            }
        ];
        if (subtask.canCancel) {
            actions.push({
                code: 'cancel',
                labelKey: 'SubtaskCancel',
                icon: 'bx-x-circle',
                destructive: true,
                attrs: `data-wcn-subtask-cancel="${esc(subtask.id)}" data-wcn-subtask-title="${esc(subtask.title)}"`
            });
        }
        return actions;
    };

    const subtaskRowMenu = (subtask) => {
        const actions = subtaskRowActions(subtask);
        if (actions.length === 0) { return ''; }

        if (actions.length === 1) {
            const only = actions[0];
            /*
             * ⚠ NO `btn btn-icon` (2026-08-24). MEASURED: the subtask row stood 52px against the checklist
             * row's 44px, with identical padding, radius and background — the whole difference was this
             * button. `.btn` carries the theme's 38px control height, and a 38px control inside a 6px-padded
             * row sets the row's height from the inside.
             *
             * The checklist's own action (`diten-checkitem-btn`) is not a `.btn` at all; `.wcn-subtask-rowaction`
             * already declares everything this control needs. Nothing is lost — same glyph, same hit area,
             * same title and aria-label; only the imported height is gone.
             */
            return `<button type="button" class="wcn-subtask-rowaction" ${only.attrs}
                        title="${esc(t(only.labelKey))}" aria-label="${esc(t(only.labelKey))}">
                    <i class="bx ${only.icon} icon-md"></i>
                </button>`;
        }

        /*
         * THE LIST'S OWN KEBAB, measured and reused: `btn btn-icon dropdown-toggle hide-arrow` with
         * `bx-dots-vertical-rounded icon-md`, and a `dropdown-menu dropdown-menu-end m-0` of
         * `dropdown-item wcn-menu-item` rows — destructive ones in `text-danger`. Two surfaces with two
         * different row menus is how one product starts reading as two.
         */
        const items = actions.map((action) =>
            `<li><button type="button" class="dropdown-item wcn-menu-item${action.destructive ? ' text-danger' : ''}" ${action.attrs}>
                <i class="bx ${action.icon}"></i><span>${esc(t(action.labelKey))}</span>
            </button></li>`).join('');

        return `<div class="dropdown wcn-subtask-menu">
            <button type="button" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"
                    data-wcn-subtask-menu aria-expanded="false"
                    title="${esc(t('ActionsLabel'))}" aria-label="${esc(tf('SubtaskRowMenuAria', subtask.title))}">
                <i class="bx bx-dots-vertical-rounded icon-md"></i>
            </button>
            <ul class="dropdown-menu dropdown-menu-end m-0">${items}</ul>
        </div>`;
    };

    const renderSubtasks = (item) => {
        // Same capability rule as the checklist: declared-but-empty is valid and must explain itself, because a
        // parent with no children yet is exactly where "add a subtask" belongs.
        if (!hasCap(item, 'subtasks') || !item.subtasks) { return ''; }
        const subtaskItems = item.subtasks.items || [];
        const full = item.subtasks.mode === 'full' && !isTerminal(item);

        if (!subtaskItems.length) {
            /*
             * ── THE EMPTY CARD IS STILL THE CARD (2026-08-24, owner) ─────────────────────────────────────────
             *
             * MEASURED BEFORE: an empty subtask list replaced the whole card — head and all — with a single
             * `wcn-empty-line`: icon, sentence and add box on one row. So the card the reader had been looking
             * at DISAPPEARED the moment its last child was deleted, and a task that never had children never
             * showed the card at all. The same screen said two different things about the same capability.
             *
             * ⚠ THE LANGUAGE IS THE SIBLING CARD'S, not a new one. The checklist card next door already answers
             * this: it keeps `cardHead`, keeps its add row where it always is, and puts its "nothing yet"
             * sentence in a `.wcn-block-hint` — 12px secondary text, no alert, no box. That hint is also what
             * sits under THIS card's add row already (`subtaskInheritHint`), so the shape below is two existing
             * lines in their existing order rather than a design.
             *
             * ⚠ NO ALERT (owner, explicit). An empty list is not a warning; nothing is wrong.
             *
             * The sentence lands UNDER the add row, which is where a deleted row would have been — so "never had
             * any" and "just deleted the last one" look identical, which is what was asked for.
             *
             * The progress bar and the "N tamam" reading are left out on purpose: 0 of 0 is not a measurement,
             * and drawing an empty bar would say the card is tracking something it is not.
             */
            return `<div class="wcn-detail-section" id="wcn-subtasks-card" tabindex="-1">
                <div class="d-flex align-items-center justify-content-between gap-2 mb-2">
                    <h6 class="text-uppercase text-heading fw-semibold mb-0 d-flex align-items-center gap-2">
                        <i class="bx bx-sitemap dt-card-icon" aria-hidden="true"></i>${esc(t('SubtasksLabel'))}
                        <span class="badge bg-label-secondary wcn-subtask-count">0</span>
                    </h6>
                </div>
                ${full ? `${subtaskAddRow(item)}${subtaskInheritHint()}` : ''}
                <p class="wcn-block-hint">${esc(t('SubtasksEmpty'))}</p>
            </div>`;
        }

        // Cancelled subtasks sink below the live ones. They are history, and three of them at the top of a list
        // reads as work waiting to be done.
        const ordered = subtaskItems.slice().sort((a, b) =>
            (isCancelledSubtask(a) ? 1 : 0) - (isCancelledSubtask(b) ? 1 : 0));

        /*
         * A CHECKLIST ROW: a box, then two layers (title over holder·date), then the state and the row menu.
         *
         * The box is not decoration. It completes the subtask through the ordinary transition endpoint — a
         * subtask IS a task, there is no half-lifecycle for children — and nothing is ticked optimistically:
         * the refreshed projection decides what the row says next. A child has its own gates, and when the
         * server refuses, its reason is what the user reads.
         */
        const rows = ordered.map((s) => {
            const terminal = s.status === 'done' || isCancelledSubtask(s);
            const blockedKey = SUBTASK_CHECK_BLOCKED_KEY[s.status];
            const disabled = !full || terminal;
            const reason = blockedKey ? t(blockedKey) : (full ? t('SubtaskCheckAria') : t('SubtasksReadonlyHint'));
            const meta = [
                s.assignee?.displayName || '',
                s.dueAt ? String(s.dueAt).slice(0, 10) : ''
            ].filter(Boolean).join(' · ');

            return `<li class="wcn-subtask wcn-subtask-${s.status}${s.id === state.flashSubtaskId ? ' wcn-subtask-flash' : ''}">
                <button type="button" class="wcn-subtask-check"${disabled ? ' disabled' : ''}
                        ${full && !terminal ? `data-wcn-subtask="${item.id}:${s.id}"` : ''}
                        title="${esc(reason)}" aria-label="${esc(reason)}" aria-pressed="${s.status === 'done'}">
                    <i class="bx ${s.status === 'done' ? 'bxs-check-square' : isCancelledSubtask(s) ? 'bx-x-square' : 'bx-square'}"></i>
                </button>
                <div class="wcn-subtask-body">
                    <button type="button" class="wcn-subtask-title wcn-linklike" data-wcn-open-task="${esc(s.id)}"
                            aria-label="${esc(tf('SubtaskOpenAria', s.title))}">${esc(s.title)}</button>
                    ${meta ? `<span class="wcn-subtask-meta">${esc(meta)}</span>` : ''}
                </div>
                ${SUBTASK_STATUS_KEY[s.status]
                    ? `<span class="wcn-subtask-status">${esc(t(SUBTASK_STATUS_KEY[s.status]))}</span>`
                    : ''}
                ${subtaskRowMenu(s)}
            </li>`;
        }).join('');

        /*
         * TOO MANY: the list scrolls inside its own card and says how many there are. A scroll, never a tab and
         * never a truncation — everything stays reachable.
         */
        const capped = subtaskItems.length > SUBTASK_VISIBLE_LIMIT;
        const list = capped
            ? cappedList('subtasks', `<ul class="wcn-subtasks">${rows}</ul>`, subtaskItems.length)
            : `<ul class="wcn-subtasks">${rows}</ul>`;

        /*
         * Open subtasks DO block their parent's completion (BL-035, owner decision 2026-07-29). The banner above
         * names each open child; this line says how many there are.
         *
         * `cancelled` is NOT open: called-off work cannot be finished and must not hold the parent.
         */
        const openSubtasks = subtaskItems.filter((s) => s.status !== 'done' && !isCancelledSubtask(s));
        /*
         * The gate READS like a gate now. It is the sentence that says why "Complete" will refuse, and it was
         * grey body text among grey body text. The page's existing alert pattern carries it — no new colour was
         * invented, and neither the wording nor the condition changed.
         */
        const openNotice = openSubtasks.length
            ? `<div class="alert alert-warning wcn-subtask-gate d-flex align-items-start gap-2" role="note">
                <i class="bx bx-lock-alt"></i><span>${esc(tf('SubtasksBlockingNotice', openSubtasks.length))}</span>
               </div>`
            : '';

        /*
         * PROGRESS, the checklist's own shape (same <progress class="wcn-progress">, so the two lists read as
         * one family). Cancelled work counts in NEITHER half: it is not done, and it is not outstanding either
         * — counting it as remaining would make a card of called-off children look like a card of unfinished
         * ones.
         */
        const live = subtaskItems.filter((s) => !isCancelledSubtask(s));
        const done = live.filter((s) => s.status === 'done').length;

        /*
         * TWO NUMBERS, TWO JOBS — and neither repeats the other.
         *
         * "ALT GÖREVLER 5" beside "1 / 5 tamam" printed the total twice, which is exactly the confusion that was
         * reported. The badge keeps the TOTAL (how many there are); the reading on the right drops the
         * denominator and says how many are DONE. The full "1 / 5" survives as the progress bar's accessible
         * name, where a screen reader needs the whole statement rather than half of it.
         */
        return `<div class="wcn-detail-section" id="wcn-subtasks-card" tabindex="-1">
            <div class="d-flex align-items-center justify-content-between gap-2 mb-2">
                <h6 class="text-uppercase text-heading fw-semibold mb-0 d-flex align-items-center gap-2">
                    <i class="bx bx-sitemap dt-card-icon" aria-hidden="true"></i>${esc(t('SubtasksLabel'))}
                    <span class="badge bg-label-secondary wcn-subtask-count">${subtaskItems.length}</span>
                </h6>
                <span class="wcn-subtask-progress">${esc(tf('SubtaskDoneCount', done))}</span>
            </div>
            <progress class="wcn-progress" value="${done}" max="${live.length}"
                      aria-label="${esc(tf('SubtaskProgressCount', done, live.length))}"></progress>
            ${full ? `${subtaskAddRow(item)}${subtaskInheritHint()}` : `<p class="wcn-block-hint"><i class="bx bx-link-external"></i>${esc(t('SubtasksReadonlyHint'))}</p>`}
            ${list}
            ${openNotice}
        </div>`;
    };

    /*
     * The red banner: what is stopping this work, and what exactly it stops.
     *
     * Written against the CONTRACT shape { blocked, affectedActionCodes[], blockers[] }. It used to read
     * `reasonKey` and `blockedBy`, which the contract has never declared and no provider has ever sent — so a
     * correctly-shaped blockedState produced a banner with an empty sentence and no blockers in it. Nothing here
     * is invented: a blocker that does not say which edge type it is simply shows its own label, and one that
     * does not name the action it stops omits that clause.
     */
    const BLOCKER_SENTENCE_KEY = {
        FinishToStart: 'BlockerFinishToStart', FinishToFinish: 'BlockerFinishToFinish',
        StartToStart: 'BlockerStartToStart', StartToFinish: 'BlockerStartToFinish'
    };
    // A blocker that is NOT an edge gets its sentence from its code instead of a dependency type. An open subtask
    // is the first of these; the shape was designed for it.
    const BLOCKER_CODE_SENTENCE_KEY = { SUBTASK_BLOCKED: 'BlockerSubtaskOpen' };
    const BLOCKED_AFFECTS_KEY = { start: 'BlockedAffectsStart', complete: 'BlockedAffectsComplete' };
    const renderBlocked = (item) => {
        if (!isBlocked(item)) { return ''; }
        const blockers = item.blockedState.blockers || [];
        if (!blockers.length) { return ''; }
        const rows = blockers.map((b) => {
            const name = b.labelText || '';
            const sentenceKey = BLOCKER_SENTENCE_KEY[b.dependencyType] || BLOCKER_CODE_SENTENCE_KEY[b.code];
            const affectsKey = BLOCKED_AFFECTS_KEY[b.affectedActionCode];
            /*
             * ⚠ ONE TREATMENT FOR THE ABBREVIATION, WHEREVER IT APPEARS (2026-08-24).
             *
             * This row drew `FS` as a `wcn-chip wcn-chip-danger` — a loud red pill whose expansion lived ONLY
             * in a `title` tooltip: absent on touch, never sought on a desktop. The dependency card solved
             * exactly this and the banner was left behind, so one abbreviation had two treatments.
             *
             * It now wears `wcn-dep-abbr`, the card's own footnote: small, muted, AFTER the sentence, keeping
             * its `title` as a bonus rather than as the only carrier.
             *
             * ⚠ WHY THE CARD'S SENTENCES (`DepSentence*`) ARE **NOT** REUSED HERE, asked and answered:
             * they describe the RELATIONSHIP — true whether or not it currently bites ("X bitmeden
             * başlayamazsın"). This banner describes a LIVE block and pairs its sentence with the clause
             * naming WHICH act is stopped (`BlockedAffects*`). Swapping in the card's sentence would state the
             * rule twice and drop the half that says what it is stopping right now. Same abbreviation
             * treatment, different sentence — deliberately.
             */
            return `<li class="wcn-blocked-item">
                <span class="wcn-blocked-why">${esc(sentenceKey ? tf(sentenceKey, name) : name)}</span>
                ${b.dependencyType ? `<span class="wcn-dep-abbr" title="${esc(t(DEP_TYPE_KEY[b.dependencyType] || b.dependencyType))}">${esc(DEP_TYPE_ABBR[b.dependencyType] || b.dependencyType)}</span>` : ''}
                ${affectsKey ? `<span class="wcn-blocked-affects">${esc(t(affectsKey))}</span>` : ''}
            </li>`;
        }).join('');
        /*
         * WHEN EVERY BLOCKER IS A SUBTASK, SAY IT ONCE AND POINT.
         *
         * MEASURED on a live blocked task: the banner printed a title ("3 sorun ilerlemeyi engelliyor") and then
         * three rows, each ending "tamamlamayı engelliyor" — the same sentence four times — while the three
         * subtasks it was naming were already listed, by name and with their own controls, in the Subtasks card
         * further down the page. The banner was a second, worse copy of a list the page already had.
         *
         * So: one sentence, and a way to reach the real list. The link is the whole point — a count with no
         * route to the thing counted just relocates the question.
         *
         * ⚠ ONLY when every blocker is a subtask. A dependency-typed blocker (FinishToStart and friends) is NOT
         * shown anywhere else on this page, so collapsing those rows would delete information rather than
         * de-duplicate it, and the link would point at a card that does not contain them. Mixed sets keep the
         * full list.
         */
        const allSubtasks = blockers.every((b) => b.code === 'SUBTASK_BLOCKED');
        if (allSubtasks) {
            return `<div class="wcn-blocked wcn-blocked-oneline" role="alert">
                <i class="bx bx-lock-alt" aria-hidden="true"></i>
                <span class="wcn-blocked-title">${esc(tf('BlockedSubtaskOneLine', blockers.length))}</span>
                <button type="button" class="wcn-linklike wcn-blocked-goto" data-wcn-goto-subtasks>${
                    esc(t('BlockedGoToSubtasks'))}</button>
            </div>`;
        }

        return `<div class="wcn-blocked" role="alert">
            <i class="bx bx-lock-alt" aria-hidden="true"></i>
            <div class="wcn-blocked-body">
                <span class="wcn-blocked-title">${esc(tf('BlockedBannerCount', blockers.length))}</span>
                <ul class="wcn-blocked-list">${rows}</ul>
            </div>
        </div>`;
    };

    /*
     * Typed dependencies — READONLY display. MOD-0024 owns its own edges and may add or remove them from its own
     * detail surface, but the Task Center aggregates other modules' work too and hosts no dependency EDITOR: a
     * Gantt or graph editor here is on the spec's never list.
     *
     * Keys are the contract's spelling (DEPENDENCY_TYPES, the engine's TaskDependencyType). The two-letter form
     * is a DISPLAY abbreviation built here, next to the seven languages — it never crosses the wire.
     */
    const DEP_TYPE_KEY = {
        FinishToStart: 'DepTypeFS', FinishToFinish: 'DepTypeFF',
        StartToStart: 'DepTypeSS', StartToFinish: 'DepTypeSF'
    };
    const DEP_TYPE_ABBR = {
        FinishToStart: 'FS', FinishToFinish: 'FF', StartToStart: 'SS', StartToFinish: 'SF'
    };
    // A dependency's state IS the predecessor task's state, so this is the subtask vocabulary — including
    // `cancelled`, which reads differently from every other value: called-off work blocks nothing.
    const DEP_STATE_KEY = {
        done: 'DepDone', 'in-progress': 'DepInProgress', 'not-started': 'DepNotStarted', cancelled: 'DepCancelled'
    };
    const DEP_STATE_KIND = {
        done: 'success', 'in-progress': 'info', 'not-started': 'secondary', cancelled: 'secondary'
    };
    /*
     * ── EIGHT SENTENCES: FOUR EDGE TYPES × TWO DIRECTIONS (2026-08-24, owner's option C) ──────────────────
     *
     * ⚠ NOTHING HERE IS INVENTED. The meaning of each edge type is DERIVED, not guessed:
     *
     *   `DEPENDENCY_TYPES` (fixture-contract.js:76) is the engine's own `TaskDependencyType` spelling, and the
     *   product ALREADY states what each one means, in seven languages, in two places:
     *     · `DEP_TYPE_KEY` → `DepTypeFS` = "Bitince başlar (FS)" / "Finish-to-Start (FS)"
     *                       `DepTypeFF` = "Bitince biter"  · `DepTypeSS` = "Başlayınca başlar"
     *                       `DepTypeSF` = "Başlayınca biter"
     *     · `BLOCKER_SENTENCE_KEY` → `BlockerFinishToStart` = "«{0}» kapanmadan başlanamaz", and its three
     *       siblings, which are the SAME four rules already written as sentences for the red banner.
     *
     *   Read together: the first verb is what the PREDECESSOR must reach, the second is what the SUCCESSOR is
     *   then allowed to do. FinishToStart = predecessor finishes → successor may start. FinishToFinish =
     *   predecessor finishes → successor may finish. StartToStart / StartToFinish likewise from "starts".
     *
     * The eight keys below apply that one rule from BOTH ends. `pred` — the listed task is my predecessor, so
     * the sentence is about what I cannot do. `succ` — the listed task waits on me, so it is about what IT
     * cannot do. Same rule, two viewpoints; no fifth or sixth semantic was introduced.
     *
     * ⚠ These are NOT the `Blocker*` keys reused. Those are passive, quoted, and only ever describe a live
     * block ("«X» kapanmadan başlanamaz"). This row describes the RELATIONSHIP whether or not it currently
     * blocks anything — a finished predecessor still has an edge type — and it speaks in the second person,
     * which is the voice the owner chose. Two sentences, two jobs, and neither is a translation of the other.
     */
    const DEP_SENTENCE_KEY = {
        pred: {
            FinishToStart: 'DepSentencePredFS', FinishToFinish: 'DepSentencePredFF',
            StartToStart: 'DepSentencePredSS', StartToFinish: 'DepSentencePredSF'
        },
        succ: {
            FinishToStart: 'DepSentenceSuccFS', FinishToFinish: 'DepSentenceSuccFF',
            StartToStart: 'DepSentenceSuccSS', StartToFinish: 'DepSentenceSuccSF'
        }
    };
    /*
     * THE DIRECTION IS AN ARROW NOW, and the word is gone.
     *
     * "ÖNCÜL" / "ARDIL" are the vocabulary of a scheduling tool, not of the person holding the task, and they
     * sat as a fourth loose part in a row of four loose parts. The arrow points the way the constraint runs:
     * LEFT = something upstream holds me, RIGHT = I hold something downstream.
     *
     * ⚠ `aria-hidden` ON THE ARROW, deliberately. It is not a second, silent statement of the direction — the
     * SENTENCE says the direction in words ("… bitmeden başlayamazsın" vs "sen bitirmeden … başlayamaz"), so a
     * reader who never sees the icon loses nothing. An icon that repeats the sentence and announces itself
     * would make a screen reader read the direction twice.
     */
    const DEP_DIR_ICON = { pred: 'bx-left-arrow-alt', succ: 'bx-right-arrow-alt' };
    const renderDependencies = (item) => {
        if (!hasCap(item, 'dependencies') || !item.dependencies || !item.dependencies.length) { return ''; }
        const rows = item.dependencies.map((d) => {
            const dir = d.direction === 'succ' ? 'succ' : 'pred';
            const sentenceKey = (DEP_SENTENCE_KEY[dir] || {})[d.type];
            /*
             * An edge type the contract has never declared cannot be given a sentence, so it falls back to the
             * bare title rather than to an invented one. A wrong sentence about a dependency is worse than a
             * missing one.
             */
            const sentence = sentenceKey ? tf(sentenceKey, d.title) : d.title;
            /*
             * ⚠ THE ABBREVIATION SURVIVES, DEMOTED — the decision, written down.
             *
             * It was the ONLY carrier of the edge type, and its expansion lived exclusively in a `title`
             * tooltip: absent on touch, and never sought on a desktop. That is what the sentence fixes. But
             * deleting `FS` would cost the reader who DOES know the notation the fastest read on the row, so it
             * stays as a small muted marker AFTER the sentence — a footnote to a statement that is already
             * complete without it, rather than the statement itself.
             *
             * It is no longer a `wcn-chip`: a chip claims the same weight as the sentence beside it. The
             * blocked banner's chip (`renderBlocked`) is a different surface and was NOT touched this round.
             */
            return `<li class="wcn-dep wcn-dep-${dir}${d.state === 'cancelled' ? ' is-cancelled' : ''}">
                <i class="bx ${DEP_DIR_ICON[dir]} wcn-dep-arrow" aria-hidden="true"></i>
                <span class="wcn-dep-title">${esc(sentence)}</span>
                <span class="wcn-dep-abbr" title="${esc(t(DEP_TYPE_KEY[d.type] || d.type))}">${
                esc(DEP_TYPE_ABBR[d.type] || d.type)}</span>
                <span class="wcn-badge wcn-badge-${DEP_STATE_KIND[d.state] || 'secondary'}">${
                esc(t(DEP_STATE_KEY[d.state] || d.state))}</span>
            </li>`;
        }).join('');
        return `<div class="wcn-detail-section">
            ${cardHead('bx-link', 'DependenciesLabel')}
            <ul class="wcn-deps">${rows}</ul>
            <p class="wcn-block-hint"><i class="bx bx-link-external"></i>${esc(t('DepsReadonlyHint'))}</p>
        </div>`;
    };

    // Attachments — readonly references (open in source).
    const renderAttachments = (item) => {
        if (!hasCap(item, 'attachments') || !item.attachments || !item.attachments.length) { return ''; }
        const rows = item.attachments.map((a) =>
            `<li class="wcn-attach" data-wcn-attach="${esc(a.name)}"><i class="bx bx-paperclip"></i><span class="wcn-attach-name">${esc(a.name)}</span><span class="wcn-attach-size">${esc(a.size)}</span></li>`).join('');
        return `<div class="wcn-detail-section">
            ${cardHead('bx-paperclip', 'AttachmentsLabel')}
            <ul class="wcn-attachments">${rows}</ul>
        </div>`;
    };

    const renderEvidence = (item) => {
        if (!hasCap(item, 'evidence') || !item.evidence) { return ''; }
        const entries = (item.evidence.items || []).map((entry) =>
            `<li class="wcn-attach"><i class="bx bx-shield-quarter"></i><span class="wcn-attach-name">${esc(data.resolveLabel(entry.label) || entry.id)}</span></li>`
        ).join('');
        return `<div class="wcn-detail-section">
            ${cardHead('bx-file-find', 'EvidenceMissing')}
            ${entries ? `<ul class="wcn-attachments">${entries}</ul>` : `<p class="text-muted mb-0">${esc(t('ActionDisabledEvidenceIncomplete'))}</p>`}
        </div>`;
    };

    // Personal note — the thin overlay WorkCenter owns (only I see it).
    /*
     * THE PERSONAL CARD — the reader's own layer over somebody else's work.
     *
     * It was the note box alone, under the heading "Kişisel Not". The PERSONAL PLAN DATE joined it when the
     * "Durum" card was dissolved, and it belongs here for the reason that card never gave it: a plan date is
     * not the task's status, it is THIS reader's intention about the task. The source due date is the
     * organisation's claim; the plan is mine. One of them belongs beside the description, the other beside my
     * note, and they were sitting in one box labelled neither.
     *
     * `PlannedDateNone` still speaks when there is no plan — this is the one place a stated absence earns its
     * line, because "I have not planned this yet" is a thing the holder needs to notice about themselves. The
     * Summary drops empty rows; this card keeps this one. The keys, the wording and the conflict rule are all
     * unchanged — they moved house, they were not rewritten.
     */
    /*
     * ── THE PERSONAL CARD, AS THE OWNER DECIDED IT (2026-08-14) ──────────────────────────────────────────────
     *
     * It now carries ONLY what nobody else can see: this reader's snooze and this reader's notes.
     *
     * ⚠ THE PLAN DATE LEFT THIS CARD, and that is a correction rather than a rearrangement. It was measured on
     * `TaskItem` (TaskItem.cs:132) and projected as a top-level field (TaskWorkItemProvider.cs:551) — the shared
     * task row, which the requester reads back and whose lifecycle a plan write moves to `Planned`. A shared
     * field under a heading that says "Kişisel" is not a layout problem, it is a false statement about who can
     * see it. It is a field of the Summary now, where the rest of the task's shared facts live.
     */
    const renderNote = (item) => {
        if (isTerminal(item)) { return ''; }

        /*
         * (a) THE SNOOZE, as a ROW rather than a button, once it is actually set.
         *
         * A snooze that is ON is a FACT about this reader's inbox — "this is hidden from me until the 22nd" —
         * and a button labelled "Ertelemeyi kaldır" states that fact only by implication, in the negative, in a
         * verb. The row says the date; the trailing control undoes it. Not snoozed, there is no fact to state,
         * so what remains is the offer: the button, exactly as before.
         */
        const snoozed = isSnoozed(item);
        const snooze = snoozed
            ? `<div class="wcn-note-row wcn-snooze-row">
                    <i class="bx bx-moon wcn-snooze-icon" aria-hidden="true"></i>
                    <span class="wcn-snooze-label">${esc(t('SnoozedLabel'))}</span>
                    <span class="wcn-note-text wcn-snooze-date">${esc(item.snoozedUntil)}</span>
                    <button type="button" class="btn btn-sm btn-label-secondary wcn-snooze-clear"
                            data-wcn-snooze="${item.id}">${esc(t('SnoozeClear'))}</button>
                </div>`
            : `<div class="wcn-personal" role="group" aria-label="${esc(t('PersonalActionsLabel'))}">
                    <button type="button" class="wcn-personal-btn" data-wcn-snooze="${item.id}">
                        <i class="bx bx-moon" aria-hidden="true"></i><span>${esc(t('Snooze'))}</span>
                    </button>
                </div>`;

        /*
         * (b) THE NOTES.
         *
         * A LIST rather than one box, by owner decision, and the shape follows from what a note IS: a thought
         * about a moment. One box means the second thought overwrites the first, so the box either stays empty
         * or grows into a wall of text nobody dares edit.
         *
         * WHEN it was written is RELATIVE on screen ("dün", "3 gün önce") and ABSOLUTE in `title` and
         * `aria-label`. Relative is what a reader actually wants from their own note; absolute is what they need
         * the moment the relative answer stops being precise enough, and a screen-reader user has no hover to
         * fall back on. Both are derived from the stored instant at render — never a count the server froze.
         *
         * NO EDIT and NO DELETE CONFIRMATION, both by decision: a private note is cheap to lose and cheap to
         * write again, and an "are you sure" on every one of them trains the reader to dismiss dialogs.
         */
        const notes = Array.isArray(item.notes) ? item.notes : [];
        const rows = notes.map((note) => {
            const absolute = noteWhenAbsolute(note);
            const when = noteWhen(note, item);
            return `<li class="wcn-note-row">
                <span class="wcn-note-text">${esc(note.text)}</span>
                <span class="wcn-note-when"${absolute ? ` title="${esc(absolute)}"` : ''}${
                absolute ? ` aria-label="${esc(tf('NoteWrittenAt', absolute))}"` : ''}>${esc(when)}</span>
                <button type="button" class="wcn-note-remove" data-wcn-note-remove="${esc(note.id)}"
                        data-wcn-note-task="${item.id}"
                        aria-label="${esc(t('NoteRemove'))}" title="${esc(t('NoteRemove'))}">
                    <i class="bx bx-trash" aria-hidden="true"></i>
                </button>
            </li>`;
        }).join('');

        /*
         * (c) THE ADD ROW, in the grammar this round standardised on the checklist and the subtask list: an
         * inset glyph in a 38px box, Enter commits, a button that says the same thing for whoever cannot see the
         * placeholder, and one hint line beneath.
         *
         * THE HINT IS THE PRIVACY SENTENCE, not the Enter affordance, and it is NOT the placeholder. A
         * placeholder disappears the moment you start typing — which is the exact moment "only you will see
         * this" needs to be readable. Enter keeps working and the button says so visibly.
         *
         * (d) It is also the EMPTY STATE. No "there is nothing here" line above a box for putting something
         * there: the add row IS the invitation, the same rule the checklist card follows.
         */
        const addRow = `<div class="wcn-note-add">
                <div class="wcn-search wcn-search-inline">
                    <i class="bx bx-plus" aria-hidden="true"></i>
                    <input type="text" class="form-control shadow-none" data-wcn-note-input
                           data-wcn-note-add="${item.id}"
                           placeholder="${esc(t('NotePlaceholder'))}"
                           aria-label="${esc(t('NotePlaceholder'))}">
                </div>
                <button type="button" class="btn btn-outline-primary"
                        data-wcn-note-save="${item.id}">${esc(t('NoteAddButton'))}</button>
            </div>
            <p class="wcn-block-hint wcn-note-hint">${esc(t('NoteAddHint'))}</p>`;

        // (e) The count, in the card head, in the idiom the checklist head already uses — and only when there is
        // something to count. A "0" badge is a label for an absence the empty state already states better.
        const count = notes.length
            ? `<span class="wcn-count-inline">${esc(String(notes.length))}</span>`
            : '';

        /*
         * (f) TWO BLOCKS, NOT ONE — the SAME technique the actions and summary cards already use, copied rather
         * than re-invented (measured there: `.wcn-acts-destructive` and `.wcn-sumtags`, both edge to edge with
         * equal space on either side).
         *
         * MEASURED BROKEN: the divider was a `border-bottom` on the button strip inside a card with 16px of its
         * own padding, so it ran 16px short at EACH end and carried 12px above against 0 below — the note row
         * sat on the line. A divider that stops short of the edge reads as a mistake instead of a division, and
         * unequal space makes it look like it belongs to the block above.
         *
         * The card stops paying for padding and each block pays its own inset; the line then falls BETWEEN the
         * blocks, where it spans edge to edge by construction. NO NEGATIVE MARGIN anywhere — that fights the
         * padding and breaks the moment the padding changes.
         *
         * It also fixes a second thing quietly: the divider used to belong to the button strip, so SNOOZING a
         * task (which swaps the strip for a snooze row) made the divider vanish. It belongs to the card now.
         */
        return `<div class="wcn-detail-section">
            <div class="wcn-personal-main">
                ${cardHead('bx-note', 'PersonalCardLabel', count)}
                ${snooze}
            </div>
            <div class="wcn-personal-notes">
                ${rows ? `<ul class="wcn-notes">${rows}</ul>` : ''}
                ${addRow}
            </div>
        </div>`;
    };

    /*
     * WHEN a note was written, in the page's OWN time language — the same `agoLabel` the activity feed uses, from
     * the same absolute instant. Never a count the server computed: a frozen "2 days ago" is the `ago` field this
     * project already banned once.
     *
     * A note with no timestamp says nothing rather than inventing "today" — that only happens to a note written
     * by a client that predates the field, and a wrong date is worse than no date.
     */
    const noteWhen = (note, item) => {
        const at = note.createdAt ? Date.parse(note.createdAt) : NaN;
        return Number.isNaN(at) ? '' : agoLabel(at, item.provenance);
    };

    /*
     * The same instant, spelled out. This is what `title` and `aria-label` carry beside the relative words:
     * "3 gün önce" is the right answer to glance at and the wrong one to act on, and a screen-reader user has no
     * hover to reach for. Formatted in the READER'S locale rather than a fixed pattern — the page already does
     * this for the calendar's month names, from the same `CurrentLanguage`.
     */
    const noteWhenAbsolute = (note) => absoluteInstant(note.createdAt);

    /*
     * ONE absolute formatter for the whole surface. It began as the personal note's own helper and is shared now
     * that the comment feed needs the same thing for its "edited" mark — two formatters would be two date
     * dialects on one page, which is the drift this round keeps closing everywhere else.
     *
     * Takes whatever the wire or the clock hands over (an ISO string or a millisecond stamp) and answers '' for
     * anything it cannot read: a wrong date is worse than no date, and the relative words are still on screen.
     */
    const absoluteInstant = (value) => {
        if (value === null || value === undefined || value === '') { return ''; }
        const at = new Date(value);
        if (Number.isNaN(at.getTime())) { return ''; }
        try {
            return new Intl.DateTimeFormat(global.CurrentLanguage || undefined,
                { dateStyle: 'long', timeStyle: 'short' }).format(at);
        } catch (error) {
            // A bad locale must not take the row down with it.
            return at.toISOString().slice(0, 16).replace('T', ' ');
        }
    };

    // Comment composer — single stream: what I write also goes to the source.
    const renderComposer = (item) => {
        if (!hasCap(item, 'activity') || isTerminal(item)) { return ''; }
        /*
         * D2/D3 — the composer speaks the form's language now.
         *
         * It was a 30px `form-control-sm` with no icon beside a 30px bare button, while every one of the create
         * form's sixteen fields is 38px inside a `.diten-field` wrapper with a glyph. One product, two input
         * dialects.
         *
         * `.diten-field` is REUSED, not re-declared: it already lives in the shared stylesheet
         * (backbone-custom.css) — it had simply never been used on this surface. A second wrapper class here
         * would have been the fork this round exists to close.
         *
         * The glyph is `bx-message-rounded`, not the description field's `bx-align-left`: on the create form a
         * field icon names what the field MEANS, not which widget it is, and "long text block" is the wrong
         * sentence for a single-line comment box. (Owner's call, recorded.)
         *
         * Still an <input>, deliberately. Making it a textarea would change what Enter does — a behaviour
         * change wearing a styling change's clothes.
         */
        return `<div class="wcn-composer">
            <div class="diten-field wcn-composer-field">
                <i class="bx bx-message-rounded diten-field-icon" aria-hidden="true"></i>
                <input type="text" class="form-control" data-wcn-comment-input placeholder="${esc(t('CommentPlaceholder'))}">
            </div>
            <button type="button" class="btn btn-primary wcn-composer-post" data-wcn-comment-post="${item.id}">
                <i class="bx bx-send" aria-hidden="true"></i><span>${esc(t('CommentPost'))}</span>
            </button>
        </div>`;
    };

    // Lightweight timesheet (task only) — total logged + live segment when running.
    const renderTimesheet = (item) => {
        // The capability gate came first, like every other block: without it this card rendered "0h 0m" for every
        // real task, because `item.timesheet` is null when the provider does not declare timeTracking and the
        // fallback below quietly supplied zeroes. A confident zero is worse than no card — it reads as "nobody has
        // worked on this" rather than "this system does not track that".
        if (!hasCap(item, 'timeTracking')) { return ''; }
        if (item.itemType !== 'task' || item.lifecycle === 'PendingAcceptance') { return ''; }
        const ts = item.timesheet || { loggedMinutes: 0, running: false };
        const live = ts.running
            ? `<span class="wcn-ts-live"><span class="wcn-ts-dot"></span><span id="wcnTimerValue">00:00</span><span class="wcn-ts-runtxt">${esc(t('TimerRunning'))}</span></span>`
            : '';
        /*
         * ── WHAT THIS CARD MAY AND MAY NOT CARRY (2026-08-24, Tur B) ──────────────────────────────────────
         *
         * The owner's complaint was that the card states a total and then falls silent — pausing means going
         * to another card. The obvious fix is a start/pause button here, and it is the WRONG one.
         *
         * MEASURED: the timer is not an independent control. It is a SIDE EFFECT of the task's state —
         *     'start'    → the task becomes "Devam ediyor" AND the timer runs
         *     'pause'    → the task pauses            AND the timer folds
         *     'complete' → the task ends              AND the timer folds
         * Putting start/pause here would open a SECOND way to change the task's lifecycle, from inside a card
         * that reads as a readout. This session already refused exactly that for document approval — do not
         * create a second authority.
         *
         * ⚠ WHAT DOES BELONG HERE IS "Süre gir". Logging minutes by hand does NOT change the task's state; it
         * is a personal measurement, not a lifecycle move. It sits in the action rail today, beside Complete
         * and Pause, which is company it does not keep.
         *
         * The card also SAYS what the timer is doing and why, so the reader stops looking for a button that is
         * deliberately elsewhere.
         */
        const logAction = itemActions(item).find((a) => a.key === 'logTime' && !a.disabled);
        const logButton = logAction
            ? `<button type="button" class="btn btn-sm btn-label-secondary wcn-ts-log"
                       data-wcn-action="${esc(logAction.key)}" data-wcn-id="${esc(item.id)}">
                    <i class="bx ${inboxActionIcon(logAction)} me-1"></i>${esc(actionLabel(logAction))}
               </button>`
            : '';
        const stateKey = ts.running ? 'TimerStateRunning'
            : item.executionState === 'paused' ? 'TimerStatePaused'
            : null;
        const stateLine = stateKey
            ? `<p class="wcn-ts-state">${esc(t(stateKey))}</p>`
            : '';
        return `<div class="wcn-detail-section">
            ${cardHead('bx-stopwatch', 'TimesheetLabel')}
            <div class="wcn-timesheet">
                <span class="wcn-ts-icon"><i class="bx bx-time"></i></span>
                <span class="wcn-ts-total">${esc(formatMinutes(ts.loggedMinutes))}</span>
                <span class="wcn-ts-sub">${esc(t('TimeLoggedLabel'))}</span>
                ${live}
            </div>
            ${stateLine}
            <p class="wcn-block-hint"><i class="bx bx-info-circle"></i>${esc(t('TimerFollowsStatusHint'))}</p>
            ${logButton}
        </div>`;
    };

    const formatMoney = (value, currency) => {
        const amount = Number(value);
        if (!Number.isFinite(amount)) { return '—'; }
        try {
            return new Intl.NumberFormat(global.CurrentLanguage || undefined, {
                style: 'currency',
                currency: currency || 'TRY',
                maximumFractionDigits: 2
            }).format(amount);
        } catch (error) {
            return `${amount.toLocaleString()} ${currency || ''}`.trim();
        }
    };

    /*
     * A CARD'S HEADING, with the glyph that says what the card IS.
     *
     * One helper rather than a dozen copies of the same markup, and one place where the icon rule lives: the
     * glyph names the CARD ("which question does this answer"), never a field inside it ("which value goes
     * here"). The two vocabularies are kept apart deliberately — the same separation the field icons were given
     * a round earlier, applied one level up.
     *
     * `trailing` carries whatever the heading already showed beside its title (a count, a badge), so adopting
     * the icon changed no card's existing content.
     */
    const cardHead = (icon, titleKey, trailing = '') =>
        `<h6 class="text-uppercase text-heading fw-semibold mb-3 d-flex align-items-center gap-2">
            <i class="bx ${icon} dt-card-icon" aria-hidden="true"></i>${esc(t(titleKey))}${trailing}</h6>`;

    const sectionHead = (icon, titleKey) =>
        `<div class="wcn-business-head"><span class="wcn-business-icon"><i class="bx ${icon}"></i></span><h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t(titleKey))}</h6></div>`;

    /*
     * The same heading, from an already-resolved string.
     *
     * A business-context section's title is a LABEL, and a label has two forms: a system section names a
     * resource key we translate, a tenant section carries the words an administrator typed. sectionHead takes a
     * key and only a key, so a display title routed through it would be looked up, missed, and fall back to a
     * generic heading — the tenant's own section name silently replaced. resolveLabel picks the right form and
     * this renders whatever comes out.
     */
    const sectionHeadText = (icon, title) =>
        `<div class="wcn-business-head"><span class="wcn-business-icon"><i class="bx ${icon}"></i></span><h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(title)}</h6></div>`;

    /*
     * `renderApprovalContext` WAS DELETED (2026-08-24, Tur B). It drew an approval's amount and its line items
     * — description, GL account, cost centre, quantity, unit price — behind `item.amount == null`, and that gate
     * was never going to open: the approval provider that feeds this screen emits no Amount, LineItem or Currency
     * at all. Approvals do arrive here; their commercial detail does not. The field list is kept in BL-233 rather
     * than in a card nobody can render, because rewriting the card is half a day and rethinking the fields is not.
     */



    /*
     * ── THE EFFORT CARD, CONNECTED AT LAST (2026-08-24, Tur B) ────────────────────────────────────────────
     *
     * MEASURED: this card has existed since the beginning and NEVER rendered. The data was collected
     * (`FieldEstimateHours` / `FieldSpentHours` on the create form) and stored (`TaskItem.EstimateHours` /
     * `SpentHours`) — the projection simply never carried the spent half, and `taskContext` was not even in
     * the contract's capability list, so no fixture could declare it either.
     *
     * ⚠ THE ASSIGNMENT HISTORY IS NOT DRAWN, and that is a decision rather than an omission. Measured:
     * `assignmentHistory` has ZERO matches in the mapper, the contract and the entire backend. Drawing half a
     * card with data and half with a blank sub-heading is worse than a card that shows only what it knows —
     * the reader cannot tell "nobody reassigned this" from "we do not track that". Its field list is in the
     * backlog so the intent is not lost.
     */
    const renderTaskContext = (item) => {
        if (item.itemType !== 'task' || !hasCap(item, 'taskContext') || !item.effort) { return ''; }
        const estimate = Number(item.effort.estimate) || 0;
        const spent = Number(item.effort.spent) || 0;
        const progress = estimate ? Math.min(100, Math.round((spent / estimate) * 100)) : 0;
        /*
         * ⚠ NO INLINE `style="width:…"` (FG-003). The bar's width comes from the product's own
         * `.wcn-progress-{0..100}` step classes — the same ones the bulk progress bar uses — rounded to the
         * nearest ten. The exact figure is not lost: it is spoken by `aria-valuenow` and written out beside
         * the bar as "spent / estimate".
         */
        const step = Math.round(progress / 10) * 10;
        return `<section class="wcn-detail-section wcn-business-section">
            ${sectionHead('bx-timer', 'TaskContextTitle')}
            <div class="wcn-effort-head"><div><span>${esc(t('EffortSpent'))}</span><strong>${esc(String(spent))} / ${esc(String(estimate))}</strong></div></div>
            <div class="progress wcn-effort-progress" role="progressbar" aria-valuenow="${progress}" aria-valuemin="0" aria-valuemax="100" aria-label="${esc(t('EffortSpent'))}"><div class="progress-bar wcn-progress-${step}"></div></div>
        </section>`;
    };


    const renderBusinessContext = (item) => {
        if (!hasCap(item, 'businessContext')) { return ''; }
        const sections = item.businessContext?.sections || [];
        if (!sections.length) {
            return `<section class="wcn-detail-section wcn-business-section">${sectionHead('bx-grid-alt', 'BusinessContextLabel')}<p class="text-muted mb-0">${esc(t('EmptyBusinessContext'))}</p></section>`;
        }
        const renderValue = (field) => {
            if (field.restricted || field.redacted) { return `<span class="text-muted">${esc(t('RedactedValue'))}</span>`; }
            if (field.kind === 'boolean') { return esc(t(field.value ? 'Yes' : 'No')); }
            if (field.kind === 'link' && field.href) {
                return `<a href="${esc(field.href)}" target="_blank" rel="noopener noreferrer">${esc(data.resolveLabel(field.value) || t('RelatedRecordOpen'))}</a>`;
            }
            return esc(data.resolveLabel(field.value) || field.value || '—');
        };
        return sections.map((section) => {
            const rows = (section.fields || []).map((field) =>
                `<div class="wcn-fact"><span>${esc(data.resolveLabel(field.label))}</span><strong>${renderValue(field)}</strong></div>`
            ).join('');
            // The section's own name, in whichever label form it arrived as — never the generic fallback when a
            // real title exists.
            const title = data.resolveLabel(section.title) || t('BusinessContextLabel');
            return `<section class="wcn-detail-section wcn-business-section">${sectionHeadText('bx-grid-alt', title)}<div class="wcn-facts-grid">${rows}</div></section>`;
        }).join('');
    };

    const renderApprovalChain = (item) => {
        if (!hasCap(item, 'approvalChain') || !(item.approvalChain || []).length) { return ''; }
        const rows = item.approvalChain.map((step) => {
            const key = step.status === 'approved' ? 'ApprovalStatusApproved' : step.status === 'rejected' ? 'ApprovalStatusRejected' : 'ApprovalStatusPending';
            return `<li class="wcn-process-row"><span class="wcn-process-marker is-${esc(step.status)}"><i class="bx ${step.status === 'approved' ? 'bx-check' : step.status === 'rejected' ? 'bx-x' : 'bx-time-five'}"></i></span><div><strong>${esc(step.approver)}</strong><span>${esc(tf('ApprovalLevelValue', step.level))} · ${esc(t(key))}${step.at ? ` · ${esc(step.at)}` : ''}</span></div></li>`;
        }).join('');
        return `<section class="wcn-detail-section wcn-business-section">${sectionHead('bx-git-branch', 'ApprovalChainTitle')}<ol class="wcn-process-list">${rows}</ol></section>`;
    };


    const renderRelated = (item) => {
        if (!hasCap(item, 'related') || !(item.related || []).length) { return ''; }
        const typeKeys = { parent: 'RelatedTypeParent', child: 'RelatedTypeChild', transaction: 'RelatedTypeTransaction', document: 'RelatedTypeDocument' };
        const rows = item.related.map((record) => `<a class="wcn-related-row" href="${esc(record.link)}"><span class="wcn-related-type">${esc(t(typeKeys[record.type] || 'RelatedTypeDocument'))}</span><span><strong>${esc(record.title)}</strong><small>${esc(record.id)}</small></span><i class="bx bx-chevron-right"></i></a>`).join('');
        return `<section class="wcn-detail-section wcn-business-section">${sectionHead('bx-link', 'RelatedRecordsTitle')}<div class="wcn-related-list">${rows}</div></section>`;
    };

    const renderCompliance = (item) => {
        if (!hasCap(item, 'compliance') || !(item.complianceFlags || []).length) { return ''; }
        const kindKeys = { policy: 'CompliancePolicy', limit: 'ComplianceLimit', sod: 'ComplianceSoD' };
        const flags = item.complianceFlags.map((flag) => `<li class="wcn-compliance-flag is-${esc(flag.severity)}"><i class="bx ${flag.severity === 'high' ? 'bx-error-circle' : 'bx-info-circle'}"></i><div><strong>${esc(t(kindKeys[flag.kind] || 'CompliancePolicy'))}</strong><span>${esc(flag.message)}</span></div></li>`).join('');
        return `<section class="wcn-detail-section wcn-business-section">${sectionHead('bx-shield-quarter', 'ComplianceTitle')}<ul class="wcn-compliance-list">${flags}</ul></section>`;
    };

    const renderDelegation = (item) => {
        if (!item.delegator) { return ''; }
        return `<section class="wcn-detail-section wcn-business-section">
            ${sectionHead('bx-user-pin', 'DelegationTitle')}
            <div class="wcn-delegation-card"><i class="bx bx-transfer-alt"></i><div><strong>${esc(tf('DelegationOnBehalf', item.delegator))}</strong><span>${esc(t('DelegationDelegate'))}: ${esc(item.assignee || data.currentUser.name)}</span></div></div>
        </section>`;
    };

    // Source/system context block — the "where this came from" meta grid + open-source jump.
    /*
     * Source context in the Golden Reference field pattern: icon, label, value — the reference's own classes, so
     * the type sizes and colours come from one place.
     *
     * Every row is dropped when its value is empty. The reference would print "-"; here an absent field simply
     * does not appear, because a dash claims the value was checked and found empty. That is the page's standing
     * rule and the one place it diverges from the reference on purpose.
     */
    /*
     * TECHNICAL DETAILS — kept for support, folded away for everyone else.
     *
     * MEASURED: this was the biggest card on the page and every line of it was developer data — the source
     * record GUID, the canonical module, the source object type ("task"), the concurrency token rendered as
     * "version: 8", the action depth. Support genuinely needs all of it, so nothing is deleted; it starts
     * CLOSED, and what it shows when opened is written in the reader's language rather than in the wire's.
     *
     * A <details> element rather than a JS toggle: the open/closed state is the browser's to keep, it works
     * without script, and it is keyboard-reachable and announced for free.
     */
    /*
     * `technicalVersion` LIVED HERE and is gone with the "Kaynak sürümü" row it fed. The concurrency token is a
     * write-safety mechanism, not something a reader acts on: it changes on every save, means nothing on its
     * own, and was the clearest example of a field that was in the card because it EXISTED rather than because
     * anyone needed it. `TechVersionValue` stays in all seven resx files, unused, in case a diagnostics surface
     * ever wants it.
     */


    /*
     * ── WHERE THIS WORK CAME FROM ─────────────────────────────────────────────────────────────────────────
     *
     * Was "Teknik bilgi", folded inside a `<details>`, holding six fields and two buttons. Three things were
     * wrong with that and only one of them was visual:
     *
     * THE NAME. Once the genuinely technical fields go (version token, action depth), nothing technical is left
     * — what remains is "which record is this and where does it live". And "Teknik bilgi" is a sign on a door
     * saying THIS IS NOT FOR YOU: a reader who needs the source record stops before opening it.
     *
     * THE FOLD. A three-row card behind a disclosure costs a click and saves nothing. `<details>` earns its
     * keep over long or rarely-wanted content; this is neither.
     *
     * THE COLUMNS. It used the summary card's two-column golden grid inside a 337px rail, so every value wrapped
     * or truncated. One column, label and value on one line, is what fits.
     *
     * ── AND THE PART THAT IS NOT COSMETIC: A FIELD APPEARS ONLY WHEN IT DISTINGUISHES SOMETHING ─────────────
     *
     * The same rule already applied to the head card's provenance. Measured: every record on this surface today
     * carries `providerCode: "tasks"` and `objectType: "task"`, so "Görevler" and "task" were printed on every
     * task and told the reader nothing.
     *
     * THE RECORD ID is the sharper case. On our own work it is a GUID the page's own URL already carries and
     * which is clickable — nobody pastes a GUID into a support thread, they paste the link. On a FOREIGN
     * provider the same field is that system's searchable key ("REG-2026-0184"), and there it is the single
     * most useful thing on the card. Same field, opposite value, decided by whose record it is.
     */
    const renderSourceCard = (item) => {
        const foreign = (item.source?.providerCode || item.sourceProviderCode) !== 'tasks';
        const foreignType = (item.source?.objectType || item.sourceType) !== 'task';

        /*
         * "Kaynaktaki durumu", not "Kaynak durumu" — a one-word fix for a real ambiguity. The head says
         * "Beklemede" (our normalizedStatus) and this says "Planlandı" (the source's own word). Both are true,
         * they are different axes, and nothing on the page said which word belonged to whom.
         */
        const rows = sourceRow('bx-flag', 'DetailNativeStatusInSource', item.nativeStatusText)
            + (foreign ? sourceReferenceRow(item) : '')
            + (foreign ? sourceRow('bx-cube', 'DetailModuleName', item.sourceModuleName || item.sourceModule) : '')
            + (foreignType ? sourceRow('bx-category', 'DetailSourceType', item.sourceObjectType || item.sourceType) : '');

        /*
         * THE OPEN-SOURCE BUTTON, unless the actions card has already taken it.
         *
         * When the work cannot be finished here (`actionDepth === 'deeplink'`) the actions card leads with
         * "{Module}'de tamamla", which goes to the same place. Two controls for one destination is the
         * duplication this page keeps removing — so here it stands down.
         */
        const openButton = item.actionDepth === 'deeplink' ? '' : `
            <button type="button" class="btn btn-sm btn-label-primary wcn-opensource" data-wcn-open="${esc(item.id)}"
                    aria-label="${esc(tf('OpenSourceAria', item.sourceModuleName || item.sourceModule, item.sourceId))}">
                <i class="bx bx-link-external" aria-hidden="true"></i><span>${esc(t('DetailOpenSource'))}</span>
            </button>`;

        if (!rows && !openButton) { return ''; }

        return `<div class="wcn-detail-section wcn-source">
            ${cardHead('bx-git-repo-forked', 'SourceCardLabel')}
            ${rows ? `<dl class="wcn-source-list">${rows}</dl>` : ''}
            ${openButton}
        </div>`;
    };

    /* One label/value pair, on one line. A definition list because that is what these are, and because the rail
       is 337px wide — the summary card's two-column grid wraps every value in here. */
    const sourceRow = (icon, labelKey, value) => (value === null || value === undefined || value === ''
        ? ''
        : `<div class="wcn-source-row">
            <dt class="wcn-source-key"><i class="bx ${icon}" aria-hidden="true"></i>${esc(t(labelKey))}</dt>
            <dd class="wcn-source-val">${esc(value)}</dd>
        </div>`);

    /* The id keeps its copy button: this is the one value a reader takes somewhere else, and it is only drawn
       for a foreign provider, where it is that system's searchable key rather than our own GUID. */
    const sourceReferenceRow = (item) => {
        const id = item.sourceId;
        if (id === null || id === undefined || id === '') { return ''; }
        const full = String(id);
        const shown = full.length > 13 ? `${full.slice(0, 8)}…${full.slice(-4)}` : full;
        return `<div class="wcn-source-row">
            <dt class="wcn-source-key"><i class="bx bx-hash" aria-hidden="true"></i>${esc(t('DetailSourceId'))}</dt>
            <dd class="wcn-source-val wcn-source-val-id">
                <code class="wcn-reference-id" title="${esc(full)}">${esc(shown)}</code>
                <button type="button" class="btn btn-xs btn-icon btn-label-secondary wcn-copyref"
                        data-wcn-copy="${esc(full)}"
                        title="${esc(t('CopyReference'))}" aria-label="${esc(t('CopyReference'))}">
                    <i class="bx bx-copy" aria-hidden="true"></i>
                </button>
            </dd>
        </div>`;
    };

    const detailHtml = (item) => {
        if (!item) {
            return `<div class="wcn-detail-empty">
                <i class="bx bx-select-multiple"></i>
                <p>${esc(t('SplitNoSelection'))}</p>
            </div>`;
        }
        const surface = global.WorkCenterNextTaskDetailResolver?.resolveTaskDetailSurface(item._fixture || item, {
            /*
             * PER-ITEM, not global. `state.submittingActionCode` alone would lock this card while a DIFFERENT
             * item was submitting — the detail page shows one task, but the state is shared with the list.
             * Narrowing it here keeps ONE lock model (the resolver's) instead of growing a second one in the
             * rail, which is the "two parallel models, one of them dead" shape already on record this session.
             */
            submittingActionCode: state.submittingItemId === item.id ? (state.submittingActionCode || null) : null
        });
        if (!surface || surface.invalid) {
            return `<div class="wcn-detail-empty" role="alert">
                <i class="bx bx-error-circle"></i>
                <h5>${esc(t('FixtureInvalidTitle'))}</h5>
                <p>${esc(t('FixtureInvalidDesc'))}</p>
            </div>`;
        }
        // Dependency banner (spec v2 §5): source-computed block, read-only here.
        const blockedBanner = renderBlocked(item);
        // System/stale state (spec v3 §3) — "record changed", "source unreachable",
        // "your authority ended". Mock representation; the source resolves the truth.
        const bannerCode = surface.criticalBanner?.code || null;
        const sys = bannerCode && SYSSTATE[bannerCode];
        const sysAction = bannerCode === 'stale'
            ? `<button type="button" class="btn btn-sm btn-label-warning" data-wcn-refresh-source="${item.id}">${esc(t('RefreshSource'))}</button>`
            : bannerCode === 'sourceUnavailable'
                ? `<button type="button" class="btn btn-sm btn-label-danger" data-wcn-refresh-source="${item.id}">${esc(t('RetrySource'))}</button>`
                : '';
        const sysBanner = sys
            ? `<div class="wcn-sysstate wcn-sysstate-${sys.kind}" role="alert"><i class="bx ${sys.icon}"></i><span>${esc(t(sys.key))}</span>${sysAction}</div>`
            : '';
        /*
         * Parked waiting. Two independent facts and either can be absent — see `waitingSentence`, which is the
         * one place all three surfaces get this from. ⚠ It used to print the person INSTEAD of the reason when
         * both were known, so naming somebody cost the reader the sentence saying what was being waited for.
         */
        const waitingText = waitingSentence(item);
        /*
         * Its OWN class beside the shared one. The generic `wcn-parked-info` is also worn by the resolver's
         * notices, which sit in the same block — so "the waiting note" had no selector of its own, and the first
         * `.wcn-parked-info` on the page is usually a notice rather than this. Same paint, addressable.
         */
        const waitingNote = waitingText
            ? `<div class="wcn-parked wcn-parked-info wcn-parked-waiting" role="note">`
              + `<i class="bx bx-time-five"></i><span>${esc(waitingText)}</span></div>`
            : '';
        // Snoozed (personal park) note.
        const snoozeNote = isSnoozed(item)
            ? `<div class="wcn-parked wcn-parked-snooze" role="note"><i class="bx bx-moon"></i><span>${esc(tf('SnoozedUntil', item.snoozedUntil))}</span></div>`
            : '';
        const notices = surface.notices.map((notice) =>
            `<div class="wcn-parked wcn-parked-info" role="note"><i class="bx bx-info-circle"></i><span>${esc(t(notice.labelKey))}</span></div>`
        ).join('');
        /*
         * ⚠ THE SNOOZE CONTROL USED TO BE BUILT HERE and appended after the Personal card's body. It is drawn
         * inside `renderNote` now, because the card has TWO faces of it — an offer while nothing is snoozed, a
         * row stating the date once something is — and only the function that knows the notes can choose.
         *
         * FOUND BY A TEST, not by reading: leaving this block in place rendered the button twice, and the guard
         * that counts snooze controls in the rail went from 1 to 2 the moment the row was added.
         */
        /*
         * TWO KINDS, TWO SHAPES — because a sentence somebody wrote and a state that changed are not the same
         * kind of thing, and reading them at the same weight makes the conversation disappear into the log.
         *
         *   comment → an avatar, a name, and the message. It looks like somebody speaking, because it is; it can
         *             be replied to, and the person is the first thing you need.
         *   event   → no avatar, one quiet line, an arrow. Nobody replies to a state change, and giving it a face
         *             would make the machine look like a participant in the conversation.
         *
         * They shared one 46px row until now. The visible split is the point of the change, so a test asserts the
         * two are STRUCTURALLY different rather than merely differently classed.
         */
        const visibleActivity = state.activityFilter === 'comments'
            ? item.activity.filter((entry) => entry.kind === 'comment')
            : item.activity;

        const auditRows = visibleActivity.map((entry) => {
            if (entry.kind === 'comment') {
                const author = entry.actor || t('CommentAuthorUnknown');
                /*
                 * ── A COMMENT CAN NOW BE REWRITTEN AND WITHDRAWN — AND SAYS SO ──────────────────────────────
                 *
                 * Comments were immutable, deliberately: changing a sentence somebody has already replied to can
                 * make their reply nonsense. What made editing acceptable is THE TRAIL, and this is where the
                 * trail is read.
                 *
                 *   withdrawn → the words are gone (the server cleared them at rest, they are not merely hidden
                 *               here) and a TOMBSTONE stands in their place. The row survives, so the feed still
                 *               says somebody spoke here and took it back.
                 *   edited    → the sentence, plus a mark carrying WHEN. "Edited" alone cannot answer "before or
                 *               after I read it", which is the only question the mark exists to settle.
                 *
                 * The controls are drawn from `entry.editable`, which the SERVER decides. Comparing the author's
                 * NAME here would hand two people who share one name each other's buttons — and the handler
                 * would then refuse a control the screen had offered.
                 */
                const withdrawn = !!entry.withdrawnAt;
                const editedMark = entry.editedAtMs
                    ? `<span class="wcn-audit-edited" title="${esc(absoluteInstant(entry.editedAtMs))}">${
                        esc(t('CommentEdited'))}</span>`
                    : '';
                const body = withdrawn
                    ? `<span class="wcn-audit-text wcn-audit-withdrawn">${esc(t('CommentWithdrawn'))}</span>`
                    : `<span class="wcn-audit-text">${esc(entry.text)}</span>${editedMark}`;
                const controls = entry.editable
                    ? `<span class="wcn-audit-controls">
                        <button type="button" class="wcn-audit-ctl" data-wcn-comment-edit="${esc(entry.id)}"
                                data-wcn-comment-task="${item.id}"
                                aria-label="${esc(t('CommentEdit'))}" title="${esc(t('CommentEdit'))}">
                            <i class="bx bx-pencil" aria-hidden="true"></i>
                        </button>
                        <button type="button" class="wcn-audit-ctl" data-wcn-comment-withdraw="${esc(entry.id)}"
                                data-wcn-comment-task="${item.id}"
                                aria-label="${esc(t('CommentWithdraw'))}" title="${esc(t('CommentWithdraw'))}">
                            <i class="bx bx-trash" aria-hidden="true"></i>
                        </button>
                    </span>`
                    : '';
                return `<li class="wcn-audit-item wcn-audit-comment${withdrawn ? ' wcn-audit-item-withdrawn' : ''}">
                    <span class="diten-opt-avatar wcn-audit-avatar" aria-hidden="true">${esc(personInitials(author))}</span>
                    <div class="wcn-audit-body">
                        <span class="wcn-audit-author">${esc(author)}</span>
                        ${body}
                        ${entry.atMs ? `<span class="wcn-audit-meta">${esc(agoLabel(entry.atMs, item.provenance))}</span>` : ''}
                    </div>
                    ${controls}
                </li>`;
            }

            // One line: what happened · who · when. The reason, when the act carried one, follows in the actor's
            // own words — it is the half of "returned" that the code cannot carry.
            const parts = [eventSentence(entry), entry.actor || t('CommentAuthorUnknown')];
            if (entry.atMs) { parts.push(agoLabel(entry.atMs, item.provenance)); }
            return `<li class="wcn-audit-item wcn-audit-event">
                <i class="bx bx-right-arrow-alt wcn-audit-arrow" aria-hidden="true"></i>
                <span class="wcn-audit-line">${esc(parts.join(' · '))}${
                    entry.event && entry.event.reason
                        ? `<span class="wcn-audit-reason">${esc(entry.event.reason)}</span>`
                        : ''}</span>
            </li>`;
        }).join('');

        const meta = (labelKey, value) =>
            `<div class="wcn-meta-cell"><span class="wcn-meta-label">${esc(t(labelKey))}</span><span class="wcn-meta-value">${esc(value)}</span></div>`;

        // Card-grid detail (golden reference parity): a command card on top, then a
        // main column (work content, wide) beside a sidebar (source/status meta,
        // narrow), and a full-width activity feed. Widths are driven by content:
        // wide work → col-lg-8, compact meta → col-lg-4, conversation → col-12.
        /*
         * A card that holds ONLY an empty line gets the slim padding: at p-4 the padding was twice the height of
         * the sentence inside it, so "there is nothing here" still occupied a box. Measured 73px → 57px.
         */
        /*
         * THE ACTIONS CARD TAKES NO PADDING OF ITS OWN — its INNER blocks do.
         *
         * MEASURED: the destructive tier's rule ran 748→1053 inside a card spanning 732→1069, i.e. it stopped
         * 16px short at each end because the card's `p-4` pushed it inward. A rule that does not reach the edge
         * reads as a mistake rather than as a division.
         *
         * The fix is NOT a negative margin — that fights the padding and breaks the moment the padding changes.
         * The padding moves down a level: the card clips (`wcn-acts-card`), each block inside carries its own
         * inset, and the rule sits between blocks where it naturally spans edge to edge.
         */
        const card = (inner) => inner
            ? `<section class="card backbone-preview-section wcn-detail-card ${
                inner.includes('wcn-acts') ? 'wcn-acts-card'
                    : inner.includes('wcn-sum-main') ? 'wcn-sum-card'
                    : inner.includes('wcn-personal-main') ? 'wcn-personal-card'
                    /*
                     * The business-context family (context, related records, compliance, evidence, the process
                     * blocks) takes the same treatment for the same reason: `renderBusinessContext` joins N
                     * sections into ONE card, and the divider between two stacked sections is a section divider
                     * like any other. Inside a padded card it would stop 16px short at each end — the defect
                     * this round fixed twice already. The card stops paying, each section pays.
                     */
                    : inner.includes('wcn-business-section') ? 'wcn-bizctx-card'
                    : inner.includes('wcn-empty-line') ? 'wcn-detail-card--slim p-3' : 'p-4'}">${inner}</section>`
            : '';
        const reviewNote = (item.itemType === 'task' && item.lifecycle === 'PendingReview')
            ? `<div class="wcn-review-note"><i class="bx bx-hourglass"></i><span>${esc(t('AwaitingReview'))}</span></div>`
            : '';
        /*
         * These two were built as unconditional template strings, so `cell`/`card` — which drop EMPTY content —
         * never had anything to drop: the heading alone made them non-empty. A task with no description got an
         * empty "Summary" card, and one whose provider never declared `activity` still got an "Activity &
         * comments" heading over an empty list.
         *
         * They are gated the same way every other block is (hasCap / data present), so there is one mechanism
         * rather than a special case per card.
         */

        // Capability declared but empty is a VALID state and gets an explanation instead of vanishing — the same
        // distinction renderChecklist makes. Not declared at all means the provider does not offer activity, and
        // then there is nothing to head. The composer keeps its own gate because it is the WRITE half: a closed
        // task still shows its feed and no longer offers the box.
        /*
         * NOTHING WRITTEN YET is one line — but only when there is also nothing to WRITE WITH. A live task still
         * gets its composer, because "no activity" plus a comment box is a card with a purpose; on a closed task
         * the composer is gone and the full-height "Henüz etkinlik kaydı yok" box was pure announcement.
         */
        const composer = renderComposer(item);

        /*
         * THE FILTER, and the two conditions that earn it.
         *
         * It appears only when the EVENTS pass the measured threshold — not the whole feed. A busy conversation
         * needs no filter (the chip would offer to hide nothing), and a long machine log is exactly what makes
         * finding a person's sentence hard.
         */
        const eventCount = item.activity.filter((entry) => entry.kind !== 'comment').length;
        const activityFilter = eventCount < ACTIVITY_FILTER_MIN_EVENTS ? '' :
            `<div class="wcn-audit-filter" role="group" aria-label="${esc(t('ActivityFilterLabel'))}">
                ${['all', 'comments'].map((mode) => `<button type="button"
                    class="wcn-chip-filter${state.activityFilter === mode ? ' active' : ''}"
                    aria-pressed="${state.activityFilter === mode}"
                    data-wcn-activity-filter="${mode}">${
                        esc(t(mode === 'all' ? 'ActivityFilterAll' : 'ActivityFilterCommentsOnly'))}</button>`).join('')}
            </div>`;

        /*
         * A TASK OLDER THAN THE LOG SAYS SO.
         *
         * Every task written from WC-1 onwards opens its history with a `created` event, so a feed without one is
         * a task whose earlier steps were never recorded. Nothing is reconstructed for it — deriving a timeline
         * from the timestamps a task happens to carry is the precise move this feature refused — and the reader
         * is told outright, in one quiet line, rather than being shown a hole that looks like a complete story.
         *
         * Placed at the FOOT of the feed, where the history runs out, because that is where the reader meets the
         * gap.
         *
         * ⚠ THE EMPTY FEED IS THE CASE THAT MATTERS, and the first cut of this got it wrong by requiring at least
         * one recorded event before saying anything. A pre-WC-1 task typically has NO events at all, so it showed
         * "Henüz etkinlik kaydı yok" — which is true about the RECORD and false about the task: things happened
         * to it and none were written down. That is the partial-history trap in its zero case, and it is exactly
         * the surface this whole feature exists to stop shipping. The rule is therefore the plain one: no
         * `created` entry means the log did not cover this task's beginning, whatever else the feed holds.
         *
         * Gated on `provenance === 'api'` because the showcase catalogue writes `eventKey` and never `event.code`
         * — every fixture would otherwise claim its history had been cut short.
         */
        const historyGap = (item.provenance === 'api'
            && !item.activity.some((entry) => entry.event && entry.event.code === 'created'))
            /*
             * D1 — an ALERT, not a paragraph, and it moved to the TOP of the feed.
             *
             * This sentence is the only thing stopping a reader from concluding "nothing ever happened to this
             * task" from a short list. As an 11px grey <p> at 15px tall it disappeared INTO the list it was
             * warning about. It now carries the surface's existing neutral in-card alert shell
             * (`alert-secondary` + `dt-inline-alert`) — the measured idiom, not a new tone, and `dt-inline-alert`
             * already trims Bootstrap's page-banner padding, so nothing extra is needed to keep the card dense.
             *
             * ⚠ IT USED TO SIT AT THE FOOT, and that was a deliberate choice I am reversing on the owner's call:
             * the argument was "put it where the history runs out". The counter-argument wins — a warning that
             * qualifies a list has to be read BEFORE the list, and at the foot it sat below a 320px scroll cap
             * where a reader might never reach it at all.
             */
            ? `<div class="alert alert-secondary dt-inline-alert wcn-audit-gap" role="note">
                <i class="bx bx-info-circle" aria-hidden="true"></i><span>${esc(t('ActivityHistoryStartsHere'))}</span>
            </div>`
            : '';

        const activityCapped = hasCap(item, 'activity') && visibleActivity.length > ACTIVITY_VISIBLE_LIMIT;
        const activitySection = !hasCap(item, 'activity')
            ? ''
            // `!historyGap` joins the condition because a task older than the log must never take the slim
            // "nothing here" line: that sentence is true of the RECORD and false of the task, and it is the one
            // place the reader would be left believing an unrecorded past was an empty one.
            : (!item.activity.length && !composer && !historyGap)
                ? `<div class="wcn-empty-line">
                    <i class="bx bx-message-square-detail" aria-hidden="true"></i>
                    <span class="wcn-empty-text">${esc(t('ActivityEmpty'))}</span>
                </div>`
                : `<div class="wcn-detail-section">
                    ${/*
                       * D5 — the count, in the SAME shape the subtasks card beside it uses: a
                       * `badge bg-label-secondary` inside the heading. Measured and copied rather than invented,
                       * because the two cards sit side by side and a second badge style would read as a second
                       * kind of thing.
                       *
                       * ⚠ ALWAYS THE UNFILTERED TOTAL — `item.activity`, never `visibleActivity`. The badge is
                       * in the HEADING, and a heading names the card, not the current view of it. A number that
                       * dropped when "comments only" was pressed would be reporting the filter rather than the
                       * task. (Same rule the subtasks badge already follows, which was checked: it reads the
                       * unfiltered list, and that card has no filter at all.)
                       */''}
                    ${/*
                       * The count MOVED to the tab (it is not duplicated). Its whole value was "know without
                       * opening"; a tab does that better than a card heading can, and repeating it to someone
                       * who has already clicked through is noise.
                       */''}
                    ${cardHead('bx-message-square-detail', 'ActivityLabel')}
                    ${composer}
                    ${activityFilter}
                    ${visibleActivity.length
                        ? (activityCapped
                            ? cappedList('activity', `<ul class="wcn-audit">${auditRows}</ul>`, visibleActivity.length)
                            : `<ul class="wcn-audit">${auditRows}</ul>`)
                        : (state.activityFilter === 'comments'
                            ? `<p class="wcn-block-hint">${esc(t('ActivityNoComments'))}</p>`
                            // "Nothing was recorded yet" and "the record does not reach back this far" are
                            // different sentences, and printing both would contradict itself.
                            : historyGap ? '' : `<p class="wcn-block-hint">${esc(t('ActivityEmpty'))}</p>`)}
                    ${/*
                       * THE NOTICE GOES LAST — after the list AND after "show all", outside the scroll cap.
                       *
                       * It is a FOOTNOTE about what the record does not reach, so it belongs behind the record.
                       * The reason it could not simply sit at the bottom before is that the bottom was inside a
                       * 320px scrolling box, where a reader could finish reading without ever meeting it. Out
                       * here it is both always visible and in the right place. (`cappedList` emits the cap and
                       * the button together, so anything after this interpolation is outside both.)
                       */''}
                    ${historyGap}
                </div>`;

        // Command card — identity, status, actions and personal overlay. Everything
        // the viewer decides on lives here, above the read-only detail cards.
        /*
         * The two provenance facts, each shown only when it is not the surface's default. `sourceModuleId` keeps
         * its own condition — it was already conditional and is genuinely per-record.
         */
        const showModule = (item.source?.providerCode || item.sourceProviderCode) !== 'tasks';
        const showType = (item.source?.objectType || item.sourceType) !== 'task';
        const provParts = [
            showModule
                ? `<span class="wcn-detail-prov" title="${esc(sourceTitle(item))}" role="img"
                     aria-label="${esc(sourceTitle(item))}">${esc(item.sourceModule)}</span>` : '',
            showType ? `<span class="wcn-detail-prov">${esc(typeLabel(item))}</span>` : '',
            item.sourceModuleId
                ? `<span class="wcn-detail-prov"${item.sourceModuleName
                    ? ` title="${esc(item.sourceModuleName)}" role="img" aria-label="${esc(item.sourceModuleName)}"` : ''
                }>${esc(item.sourceModuleId)}</span>` : ''
        ].filter(Boolean);
        const provenance = provParts.length
            ? provParts.join('<span class="wcn-detail-prov-dot" aria-hidden="true">·</span>')
              + '<span class="wcn-detail-idsep" aria-hidden="true"></span>'
            : '';

        const commandCard = `<section class="card backbone-preview-section wcn-detail-card wcn-detail-command p-4">
            ${/*
               * ONE LINE, TWO KINDS OF THING — which is why it used to be two lines that looked identical.
               *
               * `wcn-detail-source` and `wcn-detail-chips` were both rows of chips, same shape, same weight,
               * stacked. But they answer different questions:
               *
               *   PROVENANCE  (Görevler · Görev · id) — "what record is this?" Filing information. Constant for
               *               the life of the task, true of every task from that module, and never actionable.
               *   SIGNALS     (17g gecikmiş · Yüksek · Sahip) — "what is going on with THIS work?" Volatile,
               *               specific, and the reason a chip earns colour at all.
               *
               * Giving both the chip treatment spent the same emphasis on filing metadata as on a task being
               * seventeen days late. Provenance is now quiet text; the signals keep the chips they earned; a
               * hairline separates the two so the line still reads as two thoughts rather than one list.
               *
               * The STATUS badge has left this row entirely — it belongs to the lifecycle strip, which shows
               * where the work stands and can now name it. See renderLifecycleStepper.
               */''}
            ${/*
               * PROVENANCE APPEARS ONLY WHEN IT DISTINGUISHES SOMETHING.
               *
               * MEASURED: every record on this surface today carries `providerCode: "tasks"` and
               * `objectType: "task"`, so "Görevler · Görev" was printed on every task and told the reader
               * nothing — two constants dressed as facts, taking the eye's first pass before the signals that
               * actually vary.
               *
               * NOT DELETED, CONDITIONED. The Task Center aggregates other providers by design; the day MOD-0023
               * workflow items land here, "where did this come from" becomes a real question and the field
               * appears on its own. Deleting it would mean rebuilding it then — and rebuilding it is exactly
               * when it gets forgotten.
               *
               * The separator goes with them: a hairline before a row that begins with its first chip is a rule
               * dividing nothing.
               */''}
            <div class="wcn-detail-idline">
                ${provenance}
                ${chip(SLA_KIND[item.slaState], 'bx-time-five', slaLabel(item))}
                ${priorityChip(item)}
                ${roleChip(item)}
            </div>
            ${renderLifecycleStepper(item)}
            ${/*
               * ── THE GUIDANCE MOVED HERE, UNDER THE STEPPER (2026-08-14) ─────────────────────────────────────
               *
               * MEASURED: it rendered at y=154 while the stepper began at y=255 — a hundred pixels ABOVE the
               * thing it is about, outside the head card entirely (`closest('.wcn-detail-head')` was null),
               * because it was interpolated into the page header block that sits outside the grid.
               *
               * "Bu görev kabulünü bekliyor" is the ANSWER to a question the strip asks ("Beklemede · 1/4").
               * Printing the answer above the question makes the reader hold a sentence they cannot place yet,
               * and then meet the state it referred to afterwards.
               *
               * ⚠ ORDER AGAINST THE BLOCK NOTICE, decided and measured: GUIDANCE FIRST, BLOCKERS AFTER. The
               * guidance says what to do NEXT; a blocker says why something cannot be done YET. A reader who
               * meets the obstacle first has nothing to attach it to — "this is blocked" is only meaningful once
               * you know what you were going to do. The blockers keep their existing position and their
               * existing spacing; nothing about them moved.
               */''}
            ${renderGuidance(item)}
            ${reviewNote}
            ${sysBanner}${blockedBanner}${notices}${waitingNote}${snoozeNote}
        </section>`;

        // Flowing bento — each card carries its own width, and the deck is ordered so
        // cards pair up to a full 12-column line (8+4, 6+6, 4+4+4). A wide "work" card
        // sits beside a compact "meta" card, so no single fixed column can run tall and
        // leave the other side a void. Absent capabilities simply drop out and the rest
        // compacts upward. Widths are chosen by content weight, not category.
        const cell = (inner, col) => inner
            ? `<div class="col-12 ${col}"><section class="card backbone-preview-section wcn-detail-card p-4">${inner}</section></div>`
            : '';
        /*
         * TWO COLUMNS. Left is what the work IS; right is what you can DO about it and what stands in the way.
         *
         * The split matters because reading and acting are different jobs: with actions inline among the content
         * cards, the one control the page exists for sat wherever its card happened to land. On a narrow screen
         * the columns stack and the action rail follows the content, so it is never the only thing visible.
         */
        /*
         * ── THE WORK ITSELF vs THE RECORD OF THE WORK ────────────────────────────────────────────────────────
         *
         * Two tabs over the CONTENT COLUMN ONLY. `.wcn-detail-head` (the lifecycle bar) and `.wcn-detail-rail`
         * (available actions, status, personal note, source record) are deliberately OUTSIDE and stay put.
         *
         * ⚠ THE RAIL MUST NEVER GO INSIDE A TAB. "Available actions" are GATES — the things this person may do
         * right now. A tab that hides a gate means changing tab removes what you can do, and the reader has no
         * reason to suspect a control lives behind a label that says "Activity". Non-negotiable.
         *
         * ⚠ THIS IS A DIFFERENT AXIS FROM THE LIST PAGE. There, a tab means OWNERSHIP (whose work is this) and a
         * segment means state. Here it means the work itself vs its record. A third tab added later belongs on
         * THIS axis; borrowing the list page's meanings would make one word mean two things in one product.
         */
        const generalPanel = [
            // FIRST, always: "what is this?" is the question a detail page owes its reader before "what can you
            // do about it?" — which is what the page used to open with.
            card(renderSummary(item)),
            card(renderBusinessContext(item)),
            card(renderSubtasks(item)),
            card(renderDependencies(item)),
            card(renderChecklist(item)),
            card(renderTimesheet(item)),
            // The effort card sits beside the timesheet: one says how long this was expected to take, the
            // other how much has been clocked. Both gate on their own capability, so neither draws a zero.
            card(renderTaskContext(item)),
            card(renderAttachments(item)),
            card(renderEvidence(item)),
            card(renderCompliance(item)),
            card(renderRelated(item))
        ].join('');

        const activityPanel = card(activitySection);
        const onActivity = state.detailTab === 'activity';

        /*
         * The strip is the LIST PAGE'S skeleton, class for class: `nav nav-pills gap-2 flex-wrap wcn-tabs` with
         * `nav-item[role=presentation]` wrappers and `nav-link border shadow-none wc-tab-compact` tabs. Measured
         * and copied rather than restyled — two screens of one product are written in one hand.
         *
         * It sits INSIDE `.wcn-detail-content`, so it is as wide as the column it governs (649px at 1024) and
         * not as wide as the page. A full-width strip would claim the rail too, and the reader would rightly ask
         * why the right-hand side never changes.
         *
         * The COUNT is deliberately NOT the list page's: that one is
         * `rounded-pill bg-danger position-absolute…`, a red call to action meaning "N things want you". This
         * number only means "N things happened", it never decreases, and a permanent red would go unseen within
         * days — taking the list page's real red down with it.
         *
         * AND IT IS NO LONGER A BADGE EITHER. It was `badge bg-label-secondary`, which measured 24×20px around a
         * 7px digit and painted it #8592a3 on #ebeef0 — a filled box whose contents were LIGHTER than the label
         * beside them. Both classes are gone rather than overridden: every declaration they carried is now
         * countermanded in `backbone-custom.css`, and a class list whose entries all lose is how dead styling
         * survives a rewrite. The count is a bare span; the CSS gives it the TAB's own colour, so it dims and
         * brightens with the tab and owns no colour of its own.
         *
         * "Genel" carries no badge: there is nothing to count. An invented number for symmetry would be a lie
         * with good posture.
         */
        /*
         * A TAB IS A POINTER TO A PANEL, and until now it pointed at nothing.
         *
         * `role="tab"` and `aria-selected` were already here, which is the half that describes the STRIP. The
         * half that was missing describes the RELATIONSHIP: without `aria-controls` on the tab and a matching
         * `id` + `aria-labelledby` on the panel, a screen reader is told "tab, selected" and has no way to reach
         * or name what it selected — the two halves of the widget stay strangers.
         *
         * `tabindex="-1"` on the unselected tab is the other half of the ARIA tabs pattern: a tablist is ONE tab
         * stop, and the arrow keys move within it. Leaving both tabs in the sequence makes Tab walk the strip
         * instead of leaving it, which is the behaviour of two buttons that happen to look like tabs.
         *
         * ⚠ NOT INVENTED HERE. The list page already builds its panel correctly (`#wcn-main-panel`:
         * `role` + `aria-labelledby` + `tabindex="0"`); this is that pattern brought to the detail page, so the
         * two surfaces of the same app describe themselves the same way.
         */
        const detailTab = (key, icon, labelKey, badge) => `<li class="nav-item" role="presentation">
            <button type="button" role="tab" id="wcn-detail-tab-${key}"
                class="nav-link border shadow-none wc-tab-compact d-inline-flex align-items-center${
                    (state.detailTab === key) ? ' active' : ''}"
                aria-selected="${state.detailTab === key}"
                aria-controls="wcn-detail-panel-${key}"
                tabindex="${state.detailTab === key ? '0' : '-1'}"
                data-wcn-detail-tab="${key}">
                <i class="bx ${icon} wc-tab-icon me-md-1" aria-hidden="true"></i><span>${esc(t(labelKey))}</span>${badge || ''}
            </button>
        </li>`;

        const content = [
            /*
             * ⚠ RENAMED from `wcn-detail-tabs`, which was ALREADY TAKEN.
             *
             * That class belongs to the list page's split-detail side pane (`.wcn-split-detail`) and carries
             * `position: sticky`, a border, a radius, a backdrop-filter and `margin-block-start: 1rem`. Naming
             * this strip the same thing silently inherited all of it — the stray hairlines above and below, the
             * 4px padding nobody wrote, and the 16px that pushed the strip out of line with the rail. None of it
             * was in this file; it came from a component with the same name.
             *
             * The strip sits in a plain `.card`, so its surface IS the page's card surface — same background,
             * same radius, same shadow, no new value defined anywhere.
             */
            `<div class="card wcn-detail-tabcard"><div class="card-body p-3">
            <ul class="nav nav-pills gap-2 flex-wrap mb-0 wcn-tabs wcn-detail-tabstrip" role="tablist">
                ${detailTab('general', 'bx-detail', 'DetailTabGeneral')}
                ${detailTab('activity', 'bx-message-square-detail', 'DetailTabActivity',
                    hasCap(item, 'activity')
                        ? `<span class="wcn-audit-count ms-1">${item.activity.length}</span>`
                        : '')}
            </ul></div></div>`,
            // Both panels stay in the DOM and one is hidden by CLASS (FG-003 — no inline style). Keeping both
            // mounted is what makes a half-typed comment survive a tab switch.
            `<div class="wcn-detail-panel${onActivity ? ' d-none' : ''}" role="tabpanel"
                id="wcn-detail-panel-general" aria-labelledby="wcn-detail-tab-general" tabindex="0"
                data-wcn-detail-panel="general">${generalPanel}</div>`,
            `<div class="wcn-detail-panel${onActivity ? '' : ' d-none'}" role="tabpanel"
                id="wcn-detail-panel-activity" aria-labelledby="wcn-detail-tab-activity" tabindex="0"
                data-wcn-detail-panel="activity">${activityPanel}</div>`
        ].filter(Boolean).join('');

        /*
         * THE DECISION RAIL, in the order a decision is actually made: what can I do · where does this stand ·
         * what did I note · and, folded away, what the machine knows. Gates and dates used to be two cards and
         * are one now; the source-context card is the same data behind a <details>.
         */
        const rail = [
            card(renderActionRail(item, surface.interactionLocked, surface)),
            card(renderStatusCard(item)),
            // Personal note sits UNDER the actions: it is something the viewer writes, not something the task says.
            card(renderNote(item)),
            card(`${renderDelegation(item)}${renderApprovalChain(item)}`),
            card(renderSourceCard(item))
        ].filter(Boolean).join('');

        /*
         * The page header, in the Golden Reference Compact detail shape: heading block on the left with its
         * breadcrumb underneath, inside a d-flex justify-content-between row. Same markup, same classes — this
         * page is one of the tenant's detail pages and must not look like its own species.
         *
         * Two departures from the reference, both deliberate:
         *  - The heading is the TASK's name, not the word "Details". A task is identified by what it asks for;
         *    the page type is already said by the breadcrumb's active item.
         *  - No Back button on the right. The reference's Back and its breadcrumb parent go to the same place,
         *    and the breadcrumb's Task Center link additionally restores the list AS THE USER LEFT IT (tab,
         *    segment, filters). Two controls, one destination, one of them worse — so only the breadcrumb stays.
         */
        const pageHeader = `<div class="d-flex align-items-center justify-content-between mb-3">
            <div>
                <h5 class="mb-0">${esc(item.title)}</h5>
                <nav aria-label="${esc(t('BreadcrumbLabel'))}">
                    <ol class="breadcrumb mb-0">
                        <li class="breadcrumb-item"><a href="${esc(listReturnUrl())}">${esc(t('Title'))}</a></li>
                        <li class="breadcrumb-item active text-primary" aria-current="page">${esc(t('DetailPageTitle'))}</li>
                    </ol>
                </nav>
            </div>
        </div>`;

        /*
         * THE HEADER SITS OUTSIDE THE GRID, and that placement is the whole fix for a spacing defect.
         *
         * MEASURED: breadcrumb → first card was 28px here and 12px on every other detail page in the product
         * (`/Tasks/Create`, Positions/OrgUnits Details — the Golden Reference Compact shape this page's header
         * says it copies). The markup was copied; the PLACEMENT was not.
         *
         * The reference puts the header block before the grid and lets its own `mb-3` be the whole gap. This
         * page had it as `<div class="col-12">` INSIDE `.row.g-4`, so the header collected its `mb-3` (12px)
         * AND the row's vertical gutter (16px) on the column below it — two spacing systems stacking into a
         * number neither of them chose.
         *
         * Out of the row, the gutter cancels as Bootstrap intends (row `-16px` against the first column's
         * `+16px`) and the gap is the header's margin alone. No new CSS: the defect was structural, and so is
         * the fix.
         */
        return `<div class="wcn-detail wcn-details-page">
            ${pageHeader}
            <div class="row g-4 wcn-detail-grid">
                <div class="col-12 wcn-detail-head">${commandCard}</div>
                <div class="col-12 col-lg-8 wcn-detail-content">${content}</div>
                <div class="col-12 col-lg-4 wcn-detail-rail">${rail}</div>
            </div>
            ${renderActionBar(item, surface.interactionLocked, surface)}
            ${subtaskPanel()}
            ${subtaskCreatePanel()}
        </div>`;
    };


    /*
     * Subtask quick-edit panel (Golden Reference slim offcanvas).
     *
     * SCOPE IS DELIBERATE. Title and due date are editable because they share ONE safe write path: the task
     * update endpoint, sent as a full record rebuilt from the one just fetched, under its own expected version.
     * Assignee and status are shown but NOT editable here — assignment goes through /reassign, which demands a
     * reason and enforces who may do it, and status goes through the gated transition endpoints. Wiring either
     * of those into a "quick" panel means either dropping their rules or asking for a reason in a panel whose
     * whole point is speed; both are how a surface starts lying about what it did. The full page does them
     * properly, and the link to it is always present.
     *
     * The panel holds only fields that change often. Its checklist, dependencies and activity stay on the full
     * page: two surfaces rendering the same thing eventually disagree, which is the "two lists" problem again.
     */
    const subtaskPanel = () => {
        const id = state.subtaskPanelId;
        if (!id) { return ''; }
        const draft = state.subtaskPanelDraft || {};
        const busy = state.subtaskPanelSaving;
        const statusKey = SUBTASK_STATUS_KEY[draft.status];
        return `<div class="offcanvas offcanvas-end wcn-subtask-panel" tabindex="-1" id="wcnSubtaskPanel"
                     aria-labelledby="wcnSubtaskPanelLabel">
            <div class="offcanvas-header">
                <h5 class="offcanvas-title" id="wcnSubtaskPanelLabel">${esc(t('SubtaskQuickEditTitle'))}</h5>
                <button type="button" class="btn-close" data-bs-dismiss="offcanvas"
                        aria-label="${esc(t('PanelClose'))}"></button>
            </div>
            <div class="offcanvas-body">
                <div class="mb-3">
                    <label class="form-label" for="wcnSubtaskTitle">${esc(t('SubtaskFieldTitle'))}</label>
                    <input type="text" class="form-control" id="wcnSubtaskTitle" maxlength="200"
                           data-wcn-subtask-field="title" value="${esc(draft.title || '')}">
                </div>
                <div class="mb-3">
                    <label class="form-label" for="wcnSubtaskDue">${esc(t('SubtaskFieldDue'))}</label>
                    <input type="date" class="form-control" id="wcnSubtaskDue"
                           data-wcn-subtask-field="dueAt" value="${esc((draft.dueAt || '').slice(0, 10))}">
                </div>
                <div class="mb-3">
                    <span class="form-label d-block">${esc(t('SubtaskFieldAssignee'))}</span>
                    <p class="wcn-block-hint mb-0">${draft.assigneeName
                        ? esc(draft.assigneeName)
                        : esc(t('SubtaskNoAssignee'))}</p>
                </div>
                <div class="mb-3">
                    <span class="form-label d-block">${esc(t('SubtaskFieldStatus'))}</span>
                    <p class="wcn-block-hint mb-0">${statusKey ? esc(t(statusKey)) : ''}</p>
                </div>
                <p class="wcn-block-hint">${esc(t('SubtaskQuickEditScope'))}</p>
            </div>
            <div class="offcanvas-footer p-3 border-top d-flex flex-column gap-2">
                <button type="button" class="btn btn-primary" data-wcn-subtask-save="${esc(id)}"${busy ? ' disabled' : ''}>
                    ${esc(t('SubtaskSave'))}
                </button>
                <button type="button" class="btn btn-label-secondary" data-wcn-open-task-full="${esc(id)}">
                    <i class="bx bx-link-external me-1"></i>${esc(t('SubtaskOpenFullDetail'))}
                </button>
            </div>
        </div>`;
    };

    /* Opens the panel for one subtask, reading its CURRENT record so a save cannot blank fields it never showed. */
    const openSubtaskPanel = async (parent, subtaskId) => {
        const row = (parent?.subtasks?.items || []).find((s) => s.id === subtaskId) || null;
        state.subtaskPanelId = subtaskId;
        state.subtaskPanelDraft = {
            title: row?.title || '',
            dueAt: row?.dueAt || '',
            status: row?.status || '',
            assigneeName: row?.assignee?.displayName || ''
        };
        state.subtaskPanelRecord = null;
        render();
        showSubtaskPanel();

        // The full record is what a save must send back; without it an update would drop every field the panel
        // does not render.
        const result = await global.TasksApi.get(subtaskId);
        if (result.ok && result.data) { state.subtaskPanelRecord = result.data; }
        else { toast(global.TasksApi.failureMessage(result), 'error'); }
    };

    const showSubtaskPanel = () => {
        const node = document.getElementById('wcnSubtaskPanel');
        if (!node || !global.bootstrap?.Offcanvas) { return; }
        const panel = global.bootstrap.Offcanvas.getOrCreateInstance(node);
        node.addEventListener('hidden.bs.offcanvas', () => {
            state.subtaskPanelId = null;
            state.subtaskPanelRecord = null;
            render();
        }, { once: true });
        panel.show();
    };

    const saveSubtaskPanel = async (subtaskId) => {
        const record = state.subtaskPanelRecord;
        if (!record) { toast(t('ErrorTitle'), 'error'); return; }

        const draft = state.subtaskPanelDraft || {};
        const title = String(draft.title || '').trim();
        if (!title) { toast(t('SubtaskTitleRequired'), 'error'); return; }

        /* In place — a render here would replace the open panel's node and strand the body lock. Same defect
           class as the create panel's; this one merely hid better, because the quick-edit path had no second
           render on open and so LOOKED healthy until its save was measured. */
        state.subtaskPanelSaving = true;
        setPanelBusy('[data-wcn-subtask-save]', true, t('ActionSubmitting'));

        // Every other field is carried over from the record just read: a partial payload against a full-replace
        // endpoint silently erases whatever the panel did not show.
        const payload = Object.assign({}, record, {
            title,
            dueAt: draft.dueAt || null,
            expectedVersion: record.version ?? record.expectedVersion
        });
        const result = await global.TasksApi.update(subtaskId, payload);
        state.subtaskPanelSaving = false;

        if (!result.ok) {
            // Stays open with the typed values: the reader has something to fix.
            setPanelBusy('[data-wcn-subtask-save]', false);
            toast(global.TasksApi.failureMessage(result), 'error');
            return;
        }

        toast(t('SubtaskSaved'));
        hidePanel('wcnSubtaskPanel', () => {
            state.subtaskPanelId = null;
            state.subtaskPanelRecord = null;
        });
        await loadWorkItems();
    };


    /*
     * DETAILED subtask creation.
     *
     * Quick-add stays: for most subtasks one line is the whole thought, and it inherits the parent's holder and
     * priority. But inheriting is exactly why it cannot hand work to someone ELSE — the point of this panel.
     *
     * It reuses TaskForm.buildCreatePayload and the ordinary create endpoint, because a subtask IS a task; the
     * only thing that makes it one is parentTaskItemId, which is fixed here and deliberately not editable.
     */
    /*
     * ── THE THIRD CREATE GATE (2026-08-24, owner approved) ────────────────────────────────────────
     *
     * MEASURED, three gates with different field counts:
     *     inline box + Enter      1 field   (due date / priority / assignee INHERITED from the parent)
     *     this panel              5 fields
     *     /Tasks/Create          20 fields  — and the ONLY one that renders `#taskCustomFields`
     *
     * That last clause is why this button exists rather than why the panel should grow. The custom
     * fields section fills at runtime from `TaskFieldDefinition`, which carries `IsRequired` and has
     * its own CRUD screens. The day a tenant defines a required custom field, the two shortcuts CANNOT
     * collect it and the full form can. A shortcut that silently cannot satisfy the tenant's own rule
     * needs a door, not more fields.
     *
     * ⚠ THE OTHER TWO GATES ARE UNTOUCHED. This panel stays a shortcut; it is not becoming the form.
     *
     * ⚠ THE PATTERN IS MIRRORED, NOT INVENTED: the subtask EDIT panel already carries
     * `SubtaskOpenFullDetail` — same secondary button, same external-link glyph, same place in the
     * footer. A second visual language for "leave here and continue in the full surface" would be one
     * language too many.
     */
    const subtaskCreatePanel = () => {
        if (!state.subtaskCreateParentId) { return ''; }
        const draft = state.subtaskCreateDraft || {};
        const people = state.assignablePeople || [];
        const options = people.map((person) =>
            `<option value="${esc(personUserId(person))}"${draft.assigneeUserId === personUserId(person) ? ' selected' : ''}>`
            + `${esc(person.displayName || person.name || '')}</option>`).join('');
        return `<div class="offcanvas offcanvas-end wcn-subtask-panel" tabindex="-1" id="wcnSubtaskCreatePanel"
                     aria-labelledby="wcnSubtaskCreateLabel">
            <div class="offcanvas-header">
                <h5 class="offcanvas-title" id="wcnSubtaskCreateLabel">${esc(t('SubtaskCreateTitle'))}</h5>
                <button type="button" class="btn-close" data-bs-dismiss="offcanvas"
                        aria-label="${esc(t('PanelClose'))}"></button>
            </div>
            <div class="offcanvas-body">
                <div class="mb-3">
                    <label class="form-label" for="wcnNewSubtaskTitle">
                        ${esc(t('SubtaskFieldTitle'))} <span class="text-danger">*</span>
                    </label>
                    <input type="text" class="form-control" id="wcnNewSubtaskTitle" maxlength="200"
                           data-wcn-newsubtask-field="title" value="${esc(draft.title || '')}">
                </div>
                <div class="mb-3">
                    <label class="form-label" for="wcnNewSubtaskAssignee">${esc(t('SubtaskFieldAssignee'))}</label>
                    <select class="form-select" id="wcnNewSubtaskAssignee" data-wcn-newsubtask-field="assigneeUserId">
                        <option value="">${esc(t('SubtaskAssignToMe'))}</option>
                        ${options}
                    </select>
                </div>
                <div class="mb-3">
                    ${/*
                       * The star is CORRECT here, and that was checked before adding it. The main create
                       * endpoint refuses a task with no due date exactly as the subtask endpoint does
                       * (`400 VALIDATION_REQUEST_DUE_AT_NOT_NULL`, measured on both), and `_Form.cshtml` already
                       * marks the field. The rule is the product's; this panel was the one surface not saying so.
                       */''}
                    <label class="form-label" for="wcnNewSubtaskDue">
                        ${esc(t('SubtaskFieldDue'))} <span class="text-danger">*</span>
                    </label>
                    <input type="date" class="form-control" id="wcnNewSubtaskDue"
                           data-wcn-newsubtask-field="dueAt" value="${esc(draft.dueAt || '')}">
                </div>
                <div class="mb-3">
                    <label class="form-label" for="wcnNewSubtaskPriority">${esc(t('SubtaskFieldPriority'))}</label>
                    <select class="form-select" id="wcnNewSubtaskPriority" data-wcn-newsubtask-field="priority">
                        ${['Low', 'Medium', 'High'].map((level) =>
                            `<option value="${level}"${(draft.priority || 'Medium') === level ? ' selected' : ''}>`
                            + `${esc(t('Priority' + level))}</option>`).join('')}
                    </select>
                </div>
                <div class="mb-3">
                    <label class="form-label" for="wcnNewSubtaskDesc">${esc(t('SubtaskFieldDescription'))}</label>
                    <textarea class="form-control" id="wcnNewSubtaskDesc" rows="3"
                              data-wcn-newsubtask-field="description">${esc(draft.description || '')}</textarea>
                </div>
                <p class="wcn-block-hint">${esc(t('SubtaskCreateParentFixed'))}</p>
            </div>
            <div class="offcanvas-footer p-3 border-top d-flex flex-column gap-2">
                <button type="button" class="btn btn-primary w-100"
                        data-wcn-newsubtask-save="${esc(state.subtaskCreateParentId)}"${state.subtaskCreateSaving ? ' disabled' : ''}>
                    ${esc(t('SubtaskCreateSubmit'))}
                </button>
                <button type="button" class="btn btn-label-secondary w-100"
                        data-wcn-newsubtask-full="${esc(state.subtaskCreateParentId)}">
                    <i class="bx bx-link-external me-1"></i>${esc(t('SubtaskCreateAllFields'))}
                </button>
            </div>
        </div>`;
    };

    /*
     * ⚠ THE LOOKUP IS AWAITED BEFORE THE PANEL IS DRAWN, AND THAT ORDER IS THE WHOLE FIX.
     *
     * MEASURED, with a MutationObserver over two real clicks:
     *   t=83014  node #2 created — `showPanel` bound a Bootstrap Offcanvas to it and called `.show()`
     *   t=83077  node #3 created — 63ms later, the round-trip of the people lookup
     *   final    node #3, no instance, no `show` class
     *
     * The old order was `render() → showPanel() → await lookup → render()`. That second render replaced the very
     * node the Offcanvas instance was bound to, mid-animation. The instance survived, attached to a node no
     * longer in the document; the node on screen had no instance at all and could never be shown. The panel
     * appeared to "work once" only because the opening animation was visible for those 63ms before the swap —
     * which is exactly how it was measured as working, and then never again.
     *
     * Its sibling `openSubtaskPanel` does NOT re-render after its await (it only assigns state), which is why
     * one panel worked and the other did not. Same shape now: ONE render, and it happens when there is nothing
     * left to load.
     *
     * The cost is that the panel opens ~60ms later. The benefit is that it opens COMPLETE — the old order drew
     * an assignee select with no options and then repainted it, which is a flicker nobody asked for.
     */
    const openSubtaskCreatePanel = async (parentId) => {
        state.subtaskCreateParentId = parentId;
        state.subtaskCreateDraft = { priority: 'Medium' };
        render();
        showPanel('wcnSubtaskCreatePanel', () => {
            state.subtaskCreateParentId = null;
            state.subtaskCreateDraft = null;
        });

        /*
         * THE LOOKUP LANDS IN THE SELECT, NOT THROUGH A RENDER — and that distinction is the whole fix.
         *
         * The panel opens IMMEDIATELY, as it always did: awaiting the lookup first would mean a slow or failing
         * people service leaves the reader with a button that does nothing, which is a worse failure than an
         * assignee list that fills a moment late.
         *
         * What changed is the second half. This used to end in `render()`, which rebuilt `#wcnApp` and replaced
         * the very node the Bootstrap Offcanvas instance was bound to. Measured: node #2 at t=83014 carrying the
         * instance, node #3 at t=83077 (the lookup's round-trip) carrying none — after which the panel could
         * never be opened again, and `hidden.bs.offcanvas` could never fire to release `body { overflow:hidden }`.
         *
         * So the options are written into the live `<select>`. One node, one instance, for the panel's whole life.
         */
        const people = await global.TasksApi.assignablePeople();
        state.assignablePeople = people.ok ? people.data : [];
        if (!people.ok) { console.warn('[WorkCenterNext] Assignable people could not be read; the picker is empty.'); }
        fillAssigneeSelect();
    };

    /*
     * Patches the open create panel's assignee picker in place. Silent when the panel has since been closed —
     * a late lookup must not resurrect anything.
     */
    const fillAssigneeSelect = () => {
        const select = document.getElementById('wcnNewSubtaskAssignee');
        if (!select) { return; }
        const chosen = select.value;
        select.innerHTML = `<option value="">${esc(t('SubtaskAssignToMe'))}</option>`
            + (state.assignablePeople || []).map((person) => {
                const id = personUserId(person);
                return `<option value="${esc(id)}">${esc(person.displayName || id)}</option>`;
            }).join('');
        if (chosen) { select.value = chosen; }
    };

    const saveNewSubtask = async (parentId) => {
        const draft = state.subtaskCreateDraft || {};
        const title = String(draft.title || '').trim();
        if (!title) { toast(t('SubtaskTitleRequired'), 'error'); return; }

        /* In place, not through render(): re-rendering here would replace the open panel's node — see setPanelBusy. */
        state.subtaskCreateSaving = true;
        setPanelBusy('[data-wcn-newsubtask-save]', true, t('ActionSubmitting'));

        const payload = global.TaskForm.buildCreatePayload({
            title,
            // Explicitly chosen here, unlike quick-add which inherits — that is the whole reason this panel exists.
            assignmentTarget: draft.assigneeUserId ? 'Person' : 'SelfAssigned',
            assigneeUserId: draft.assigneeUserId || null,
            dueAt: draft.dueAt || null,
            priority: draft.priority || 'Medium',
            description: draft.description || null
        });
        // What makes it a SUBTASK. Fixed, never editable: moving a task under a different parent is a different
        // operation with its own rules.
        payload.parentTaskItemId = parentId;

        const result = await global.TasksApi.create(payload);
        state.subtaskCreateSaving = false;
        if (!result.ok) {
            // The panel STAYS OPEN with the typed values intact — the reader has to fix something, and a render
            // here would both destroy their input and strand the body lock.
            setPanelBusy('[data-wcn-newsubtask-save]', false);
            toast(global.TasksApi.failureMessage(result), 'error');
            return;
        }

        toast(tf('ToastSubtaskAdded', title));
        /*
         * Close through Bootstrap FIRST. `showPanel`'s `hidden` listener clears the draft and renders once the
         * panel is actually gone; reloading before that would replace the node underneath an open offcanvas and
         * leave the page unscrollable.
         */
        hidePanel('wcnSubtaskCreatePanel', () => {
            state.subtaskCreateParentId = null;
            state.subtaskCreateDraft = null;
        });
        await loadWorkItems();
    };

    /*
     * Cancelling a subtask, from its row. NOT deleting: a subtask is a task, its history stays, and BL-035's
     * "a cancelled subtask does not gate its parent" rule needs it to still exist. Permanent deletion, if it is
     * ever wanted, belongs on the full page where the whole record is in view.
     */
    /*
     * THE SUBTASK'S OWN VERSION — the one thing every write against a child needs.
     *
     * A subtask is its own task with its own concurrency token. It is in `state.items` only when it is also
     * assigned to the reader, which is NOT the ordinary case, so the record is fetched when it is missing —
     * exactly as the quick-edit panel does. Returns null when the record cannot be read, and the caller stops.
     */
    const subtaskVersion = async (subtaskId) => {
        const known = Number(itemById(subtaskId)?.concurrency?.token ?? NaN);
        if (Number.isFinite(known)) { return known; }

        const record = await global.TasksApi.get(subtaskId);
        if (!record.ok || !record.data) {
            toast(global.TasksApi.failureMessage(record), 'error');
            return null;
        }
        return Number(record.data.version ?? 0);
    };

    const cancelSubtask = async (subtaskId, title) => {
        const confirmed = await confirmDestructive(tf('SubtaskCancelConfirm', title));
        if (!confirmed) { return; }

        /*
         * ⚠ WITH ITS VERSION. This sent `{}` — no expectedVersion — so the server compared 0 against the real
         * one and refused every cancel as a concurrency conflict. Measured live: the request went out and came
         * back 409 "somebody changed it first", about a task nobody had touched. The confirm-dialog bug hid this
         * one completely: while the dialog resolved false, the request was never sent at all.
         */
        const expectedVersion = await subtaskVersion(subtaskId);
        if (expectedVersion === null) { return; }

        const result = await global.TasksApi.transition(subtaskId, 'cancel', { expectedVersion });
        if (!result.ok) { toast(global.TasksApi.failureMessage(result), 'error'); return; }
        toast(tf('ToastSubtaskCancelled', title));
        await loadWorkItems();
        render();
    };

    /*
     * MOD-0013 is the ONE confirm implementation in the app; a page-local dialog would be a second one.
     *
     * ⚠ THE BUG THIS SHAPE CAUSED, written down because it was invisible: the previous version resolved FALSE
     * on the next tick — "showConfirm does not report dismissal" — so the promise settled a millisecond after
     * the dialog opened. The user then clicked "Yes" seconds later, the callback resolved an already-settled
     * promise, and NOTHING happened: no request, no toast, no change. A destructive action that silently does
     * nothing is worse than one that fails loudly.
     *
     * showConfirm DOES report dismissal — `options.onCancel` — so both paths resolve, and neither resolves
     * early. Nothing here settles until the person answers.
     */
    const confirmDestructive = (message) => new Promise((resolve) => {
        if (typeof global.showConfirm === 'function') {
            global.showConfirm(
                message,
                () => resolve(true),
                /*
                 * ⚠ NOT "İptal" HERE EITHER: this dialog asks whether a subtask should be CANCELLED and its
                 * confirm button says "İptal et". The dismiss button cannot wear the same word.
                 */
                { confirmButtonText: t('SubtaskCancelConfirmYes'), cancelButtonText: t('DialogDismiss'),
                    onCancel: () => resolve(false) });
            return;
        }
        console.warn('[WorkCenterNext] window.showConfirm is unavailable; the destructive action was not offered.');
        resolve(false);
    });

    /*
     * ── NEVER RE-RENDER A PANEL THAT IS OPEN ──────────────────────────────────────────────────────────────
     *
     * `render()` replaces `#wcnApp`'s subtree. If an offcanvas is open, its node goes with it — and Bootstrap's
     * instance stays bound to the detached node. From that moment the panel on screen has no instance, cannot be
     * shown or hidden, `hidden.bs.offcanvas` never fires, and **the body keeps `overflow: hidden`**: the reader
     * is left on a page that will not scroll. Measured live, twice, in this one panel.
     *
     * So a panel's busy state is applied to the BUTTON, in place, and closing goes through Bootstrap's own
     * `hide()` — whose `hidden` event is what clears the state and re-renders. One render, after the panel is
     * gone, never during.
     */
    const setPanelBusy = (saveSelector, busy, busyLabel) => {
        const btn = document.querySelector(saveSelector);
        if (!btn) { return; }
        btn.disabled = busy;
        if (busy) {
            if (!btn.dataset.wcnIdleLabel) { btn.dataset.wcnIdleLabel = btn.textContent.trim(); }
            btn.textContent = busyLabel;
        } else if (btn.dataset.wcnIdleLabel) {
            btn.textContent = btn.dataset.wcnIdleLabel;
        }
    };

    /* Closes through Bootstrap, so `hidden.bs.offcanvas` fires and the body lock is released by the library that
       applied it. Falls back to clearing state directly only when the instance is genuinely gone. */
    const hidePanel = (id, onMissing) => {
        const node = document.getElementById(id);
        const panel = node && global.bootstrap?.Offcanvas?.getInstance(node);
        if (panel) { panel.hide(); return; }
        if (typeof onMissing === 'function') { onMissing(); render(); }
    };

    /* Shared offcanvas plumbing for both subtask panels. */
    /*
     * ── THE RULE THAT WAS ONLY IN TWO PEOPLE'S HEADS ──────────────────────────────────────────────────────
     *
     * "Never render while an offcanvas is open" was applied by hand in two places. A third panel would have had
     * to rediscover it — which is exactly what happened last round, at the cost of a panel that opened once per
     * page load and a body lock that left the page unscrollable.
     *
     * So `render()` now checks, and says so loudly. A console warning alone would not be enough (this session
     * has already had one swallowed warning hide a defect), so a test asserts that this fires — the pair is the
     * guard, not either half.
     */
    const openPanelIds = () => [...document.querySelectorAll('.offcanvas.show')].map((n) => n.id || '(unnamed)');

    const warnIfPanelOpen = () => {
        const open = openPanelIds();
        if (!open.length) { return; }
        global.console?.warn?.(
            `[WorkCenterNext] render() ran while an offcanvas was open (${open.join(', ')}). `
            + 'Replacing the panel\'s node detaches its Bootstrap instance: it can never be shown or hidden '
            + 'again, `hidden.bs.offcanvas` never fires, and body scroll stays locked. '
            + 'Update the panel in place (setPanelBusy / fillAssigneeSelect) or close it first (hidePanel).');
    };

    const showPanel = (id, onHidden) => {
        const node = document.getElementById(id);
        if (!node || !global.bootstrap?.Offcanvas) { return; }
        const panel = global.bootstrap.Offcanvas.getOrCreateInstance(node);
        node.addEventListener('hidden.bs.offcanvas', () => { onHidden(); render(); }, { once: true });
        panel.show();
    };

    // ── Table view ────────────────────────────────────────────────────────────
    const SORTERS = {
        sla: (a, b) => bySla(a, b),
        title: (a, b) => a.title.localeCompare(b.title),
        module: (a, b) => a.sourceModule.localeCompare(b.sourceModule),
        type: (a, b) => a.itemType.localeCompare(b.itemType),
        status: (a, b) => a.status.localeCompare(b.status),
        priority: (a, b) => PRIORITY_ORDER.indexOf(a.priority) - PRIORITY_ORDER.indexOf(b.priority),
        requester: (a, b) => a.requester.localeCompare(b.requester)
    };

    const triggerTableRows = () => {
        return activeTriggers().map((trigger) => ({
            fixtureKind: 'triggerOnly',
            trigger,
            id: trigger.id,
            itemType: 'meetingInvite',
            typeIcon: 'bx-calendar-event',
            title: data.resolveLabel(trigger.title),
            summary: data.resolveLabel(trigger.summary),
            sourceModule: trigger.source?.providerCode || 'calendar',
            status: 'AttendancePending',
            priority: null,
            slaState: 'none',
            slaDiffDays: null,
            requester: ''
        }));
    };

    const renderTable = (items) => {
        if (!items.length) { return emptyState(); }
        const sorter = SORTERS[state.sortKey] || SORTERS.sla;
        const sorted = items.slice().sort(sorter);
        if (state.sortDir === 'desc') { sorted.reverse(); }
        state.visibleOrder = sorted.map((i) => i.id);

        return `<div class="card wcn-datatable-card">
            <div class="card-datatable table-responsive">
            <table id="wcnDataTable" data-dt-standard="v2" class="datatables-workcenter table border-top">
                <caption class="visually-hidden">${esc(t('TableCaption'))}</caption>
                <thead><tr>
                    <th class="control"></th>
                    <th>${esc(t('ColType'))}</th>
                    <th class="all">${esc(t('ColTitle'))}</th>
                    <th>${esc(t('ColModule'))}</th>
                    <th>${esc(t('ColStatus'))}</th>
                    <th>${esc(t('ColPriority'))}</th>
                    <th>${esc(t('ColSla'))}</th>
                    <th>${esc(t('ColRequester'))}</th>
                    <th class="cell-fit">${esc(t('ActionsLabel'))}</th>
                </tr></thead>
            </table>
            </div>
        </div>`;
    };

    const destroyWorkCenterDataTable = () => {
        if (!workCenterDt) { return; }
        try { workCenterDt.destroy(); } catch (error) { /* DOM is about to be replaced. */ }
        workCenterDt = null;
    };

    const mountWorkCenterDataTable = (items) => {
        const tableEl = document.getElementById('wcnDataTable');
        if (!tableEl || !global.DataTable || !global.DtDefaults?.create) { return; }
        // DtDefaults reads window.L10n for the grid's own search placeholder + the
        // export "Action" menu label; WorkCenter localises via window.WCN.t, so seed
        // them. The export menu is "Dışa Aktar" (not "İşlemler" — that's our column).
        /*
         * BL-047 — the DELIVERY half. The payload carries all six Dt* keys in seven languages, and dt-defaults
         * consumes them (dt-defaults.js:462-466) — but it reads `window.L10n`, and this seeding block only ever
         * put Search and Action there. So the strings existed at both ends and never met: the table went on
         * saying "Showing 1 to 9 of 9 entries" on a Turkish page.
         *
         * Chose (a) — seed from this module — over (b), teaching dt-defaults to read module payloads. (b) is the
         * more general answer and 61 files need it, but it changes the shared table bootstrap for every screen in
         * the product on the strength of one screen's bug. That is a platform slice with its own regression round,
         * and it is recorded as such. This is the local, reversible half.
         */
        global.L10n = global.L10n || {};
        global.L10n.Search = t('SearchPlaceholder');
        global.L10n.Action = t('ExportLabel');
        ['DtInfo', 'DtInfoEmpty', 'DtInfoFiltered', 'DtEmptyTable', 'DtNoRecords', 'DtZeroRecords']
            .forEach((key) => { const value = t(key); if (value && value !== key) { global.L10n[key] = value; } });
        const tableFilterButton = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: `btn btn-icon ${activeAdvancedFilterCount() ? 'btn-label-primary' : 'btn-label-secondary'} dt-filter-btn position-relative`,
            attr: { title: t('FiltersLabel'), 'aria-controls': 'wcnFilterCollapse', 'aria-expanded': String(state.filtersOpen) },
            action: () => toggleTableFilter()
        };
        const config = global.DtDefaults.create({
            data: items,
            processing: false,
            serverSide: false,
            stateSave: false,
            pageLength: state.pageLength,
            order: [[6, 'asc']],
            colReorder: { columns: ':gt(0):not(:last-child)' },
            search: { search: state.search },
            // WorkCenter localises via window.WCN.t, not the shared window.L10n that
            // DtDefaults reads — so set the grid's own search placeholder explicitly.
            language: { searchPlaceholder: t('SearchPlaceholder') },
            buttons: global.DtDefaults.exportButtons(
                null,
                {},
                { filterBtn: tableFilterButton },
                { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7], showAllColumns: [1, 2, 3, 4, 5, 6, 7] }
            ),
            columns: [
                { data: 'id', name: 'control' },
                { data: 'itemType', name: 'type', visible: state.tableColumnVisibility[1], render: (value, type, row) => type === 'display' ? chip('type', row.typeIcon, typeLabel(row)) : value },
                { data: 'title', name: 'title', visible: state.tableColumnVisibility[2], className: 'fw-medium text-heading', render: (value) => esc(value) },
                { data: 'sourceModule', name: 'module', visible: state.tableColumnVisibility[3], render: (value) => esc(value) },
                { data: 'status', name: 'status', visible: state.tableColumnVisibility[4], render: (value, type, row) => type === 'display' ? `<span class="wcn-badge wcn-badge-${row.fixtureKind === 'triggerOnly' ? 'info' : STATUS_KIND[displayStatus(row)]}">${esc(statusLabel(row))}</span>` : value },
                // Hidden outright when nothing on the surface has a priority — otherwise every real row shows an
                // empty flag chip under a column header promising data the projection does not carry (BL-032).
                { data: 'priority', name: 'priority', visible: state.tableColumnVisibility[5] && items.some(hasPriority), render: (value, type, row) => type === 'display' ? (row.fixtureKind === 'triggerOnly' ? '—' : priorityChip(row)) : PRIORITY_ORDER.indexOf(value) },
                { data: 'slaDiffDays', name: 'sla', visible: state.tableColumnVisibility[6], render: (value, type, row) => type === 'display' ? (row.fixtureKind === 'triggerOnly' ? '—' : chip(SLA_KIND[row.slaState], 'bx-time-five', slaLabel(row))) : (value == null ? Number.MAX_SAFE_INTEGER : value) },
                { data: 'requester', name: 'requester', visible: state.tableColumnVisibility[7], render: (value) => esc(value) },
                { data: 'id', name: 'action', orderable: false, searchable: false, className: 'cell-fit', render: (id, type, row) => type === 'display' ? tableActionCell(row) : '' }
            ],
            columnDefs: [
                // A dedicated empty column holds the Responsive collapse toggle (+),
                // matching GoldenReferenceCompact. Without it, Responsive collapses
                // but has nowhere to render the control. Title stays highest priority.
                { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 2, render: () => '' },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, responsivePriority: 2 }
            ],
            createdRow: (row, item) => {
                // Table rows no longer open on click (act via the İşlemler column /
                // + modal), so no button role/tabindex that would imply it.
                row.setAttribute('data-wcn-row', item.id);
                row.classList.toggle('selected', state.tableSelected.has(item.id));
                row.classList.toggle('wcn-tr-failed', state.bulkFailedIds.has(item.id));
            },
            drawCallback: function () {
                global.DtDefaults.updateVisualState?.(this.api(), activeAdvancedFilterCount());
            }
        });
        workCenterDt = new global.DataTable(tableEl, config);
        mountTableFilterHost();
        workCenterDt.on('search.dt', () => { state.search = workCenterDt.search() || ''; });
        workCenterDt.on('length.dt', (_event, _settings, length) => { state.pageLength = Number(length) || 10; });
        workCenterDt.on('column-visibility.dt', (_event, _settings, column, visible) => {
            if (column > 0 && column < state.tableColumnVisibility.length) { state.tableColumnVisibility[column] = !!visible; }
        });
    };


    // ── Kanban view (READ-ONLY, spec v3) — columns by status; WorkCenter doesn't
    // own status so cards don't drag between columns. Personal plan/pin only. ───
    const kanbanCard = (item) => {
        const prim = primaryAction(item);
        const quick = prim
            ? `<button type="button" class="wcn-quick btn btn-sm btn-label-${prim.kind}" data-wcn-action="${prim.key}" data-wcn-id="${item.id}">${esc(t(prim.labelKey))}</button>`
            : '';
        return `<div class="wcn-kcard${item.isUnread ? ' unread' : ''}${item.id === state.selectedId ? ' selected' : ''}" data-wcn-row="${item.id}" tabindex="0" role="button" aria-label="${esc(tf('TableOpenRow', item.title))}">
            <div class="wcn-kcard-title">${esc(item.title)}</div>
            <div class="wcn-kcard-chips">
                ${chip('module', 'bx-cube', item.sourceModule)}
                ${chip(SLA_KIND[item.slaState], 'bx-time-five', slaLabel(item))}
                ${priorityChip(item)}
            </div>
            ${quick ? `<div class="wcn-kcard-actions">${quick}</div>` : ''}
        </div>`;
    };


    // ── Calendar view (READ-ONLY deadline clustering, spec v3) — source due
    // (red) + personal plan (blue) on a month grid. No drag-reschedule. ────────
    const pad2 = (n) => String(n).padStart(2, '0');

    // ── Focus / Today view ────────────────────────────────────────────────────
    const renderFocus = (items) => {
        const overdue = items.filter((i) => i.slaState === 'overdue');
        const dueToday = items.filter((i) => i.slaState === 'due-soon' && i.slaDiffDays === 0);
        const pinned = items.filter((i) => i.pinned && i.slaState !== 'overdue' && !(i.slaState === 'due-soon' && i.slaDiffDays === 0));

        state.visibleOrder = [];
        const section = (titleKey, kind, list) => {
            if (!list.length) { return ''; }
            const rows = list.map((item) => { state.visibleOrder.push(item.id); return rowHtml(item); }).join('');
            return `<section class="wcn-group">
                <header class="wcn-group-head wcn-group-${kind}">
                    <span class="wcn-group-dot"></span>
                    <span class="wcn-group-name">${esc(t(titleKey))}</span>
                    <span class="wcn-group-count">${list.length}</span>
                </header>
                <div class="wcn-group-rows">${rows}</div>
            </section>`;
        };

        const inner = section('FocusOverdue', 'danger', overdue)
            + section('FocusDueToday', 'warning', dueToday)
            + section('FocusPinned', 'primary', pinned);

        if (!inner) {
            return `<div class="wcn-empty">
                <i class="bx bx-coffee"></i>
                <h5>${esc(t('FocusEmptyTitle'))}</h5>
                <p>${esc(t('FocusEmptyDesc'))}</p>
            </div>`;
        }
        return `<div class="wcn-focus">
            <div class="wcn-focus-intro"><i class="bx bx-target-lock"></i><span>${esc(t('FocusDesc'))}</span></div>
            <div class="wcn-list">${inner}</div>
        </div>`;
    };

    // ── Empty states ──────────────────────────────────────────────────────────
    const emptyState = () => {
        // Meeting invitations are trigger-only projections. They use a dedicated
        // empty state but never enter the task-detail resolver or task lifecycle.
        if (state.typeFilter.has('meetingInvite')) {
            return `<div class="wcn-empty">
                <i class="bx bx-calendar-event"></i>
                <h5>${esc(t('EmptyMeetingInviteTitle'))}</h5>
                <p>${esc(t('EmptyMeetingInviteDesc'))}</p>
            </div>`;
        }
        const filtered = state.moduleFilter.length || state.priorityFilter !== 'all' || state.modeFilter !== 'all'
            || state.typeFilter.size || state.signalFilter.size || state.search || (state.tab === 'havuz' && state.group !== 'all');
        if (filtered) {
            return `<div class="wcn-empty">
                <i class="bx bx-filter-alt"></i>
                <h5>${esc(t('EmptyFilterTitle'))}</h5>
                <p>${esc(t('EmptyFilterDesc'))}</p>
                <button type="button" class="btn btn-sm btn-label-primary" data-wcn-clear-filters>${esc(t('EmptyClearFilters'))}</button>
            </div>`;
        }
        const byTab = {
            inbox: ['EmptyInboxZeroTitle', 'EmptyInboxZeroDesc', 'bx-check-circle'],
            havuz: ['EmptyPoolTitle', 'EmptyPoolDesc', 'bx-time'],
            history: ['EmptyHistoryTitle', 'EmptyHistoryDesc', 'bx-history'],
            islerim: ['EmptyMineTitle', 'EmptyMineDesc', 'bx-briefcase-alt-2']
        };
        const [titleKey, descKey, icon] = byTab[state.tab] || byTab.mine;
        return `<div class="wcn-empty">
            <i class="bx ${icon}"></i>
            <h5>${esc(t(titleKey))}</h5>
            <p>${esc(t(descKey))}</p>
        </div>`;
    };

    // ── Live timer tick (updates only the running segment text node) ──────────
    let timerInterval = null;
    const stopTimerTick = () => { if (timerInterval) { global.clearInterval(timerInterval); timerInterval = null; } };
    const setupTimerTick = () => {
        stopTimerTick();
        const item = itemById(state.selectedId);
        if (!item || !item.timesheet || !item.timesheet.running) { return; }
        const paint = () => {
            const el = document.getElementById('wcnTimerValue');
            if (!el || !item.timesheet.running) { stopTimerTick(); return; }
            el.textContent = formatSegment(Date.now() - item.timesheet.startedAt);
        };
        paint();
        timerInterval = global.setInterval(paint, 1000);
    };

    // ── Render dispatcher ─────────────────────────────────────────────────────
    const itemById = (id) => state.items.find((i) => i.id === id) || null;


    // Capture what the user was focused on before an innerHTML swap so keyboard /
    // screen-reader context survives the re-render (P1 a11y fix — the full swap
    // used to drop focus to <body> on every action). We restore by stable marker:
    // the search box (with caret), or a row/control identified by data-attr.
    const captureFocus = () => {
        const el = document.activeElement;
        if (!el || el === document.body) { return null; }
        if (el.matches && el.matches('[data-wcn-search]')) {
            return { kind: 'search', caret: el.selectionStart };
        }
        /*
         * THE PAGE'S OTHER TEXT BOXES. Adding a subtask with Enter re-renders the card, and without this the
         * caret lands on <body> — so the second subtask cannot be typed without reaching for the mouse. Matched
         * by marker attribute rather than id: ids do not survive an innerHTML swap intact, and a positional
         * selector would restore focus to the wrong box on a page that has several.
         */
        const marker = ['data-wcn-subtask-input', 'data-diten-check-input', 'data-wcn-note-input', 'data-wcn-comment-input']
            .find((attribute) => el.hasAttribute && el.hasAttribute(attribute));
        if (marker) { return { kind: 'text', marker, caret: el.selectionStart, value: el.value }; }
        const row = el.closest && el.closest('[data-wcn-row]');
        if (row) { return { kind: 'row', id: row.getAttribute('data-wcn-row') }; }
        const ctl = el.closest && el.closest('[data-wcn-view],[data-wcn-tab],[data-wcn-scope],[data-wcn-sort]');
        if (ctl) {
            const attr = ['data-wcn-view', 'data-wcn-tab', 'data-wcn-scope', 'data-wcn-sort']
                .find((a) => ctl.hasAttribute(a));
            return { kind: 'ctl', attr, val: ctl.getAttribute(attr) };
        }
        return null;
    };

    const restoreFocus = (snap) => {
        // Prefer the anchor the user was on; fall back to the selected row so the
        // j/k loop keeps a live focus target after an action removes a row.
        let node = null;
        if (snap && snap.kind === 'search') {
            node = document.querySelector('#wcnApp [data-wcn-search]');
            if (node) { node.focus(); try { node.setSelectionRange(snap.caret, snap.caret); } catch (e) { /* noop */ } return; }
        } else if (snap && snap.kind === 'row') {
            node = document.querySelector(`#wcnApp .wcn-row[data-wcn-row="${snap.id}"], #wcnApp .wcn-tr[data-wcn-row="${snap.id}"]`);
        } else if (snap && snap.kind === 'text') {
            node = document.querySelector(`#wcnApp [${snap.marker}]`);
            if (node) {
                node.focus();
                // A half-typed value survives a repaint caused by something else; an ADD deliberately leaves the
                // box empty, and `!node.value` is what tells those two apart.
                if (snap.value && !node.value) { node.value = snap.value; }
                try { node.setSelectionRange(snap.caret, snap.caret); } catch (e) { /* not a text input */ }
                return;
            }
        } else if (snap && snap.kind === 'ctl') {
            node = document.querySelector(`#wcnApp [${snap.attr}="${snap.val}"]`);
        }
        if (!node && state.selectedId) {
            node = document.querySelector(`#wcnApp .wcn-row[data-wcn-row="${state.selectedId}"], #wcnApp .wcn-tr[data-wcn-row="${state.selectedId}"]`);
        }
        if (node && typeof node.focus === 'function') { node.focus(); }
    };



    const renderLoadingState = () => `<div class="wcn-system-page" role="status" aria-live="polite">
        <span class="spinner-border spinner-border-sm text-primary" aria-hidden="true"></span>
        <h5>${esc(t('LoadingTitle'))}</h5><p>${esc(t('LoadingDesc'))}</p>
        <div class="wcn-skeleton" aria-hidden="true"><span></span><span></span><span></span></div>
    </div>`;

    const renderTriggerResponses = (triggerOverride) => {
        const triggers = triggerOverride || activeTriggers();
        if (!triggers.length) { return ''; }
        const cards = triggers.map((trigger) => {
            const titleText = data.resolveLabel(trigger.title);
            const summaryText = data.resolveLabel(trigger.summary);
            const surface = global.WorkCenterNextTriggerResponseResolver?.resolveTriggerResponse(trigger, {
                submittingActionCode: state.submittingTriggerId === trigger.id ? state.submittingActionCode : null
            });
            if (!surface || surface.invalid) { return ''; }
            const byCode = new Map((trigger.actions || []).map((action) => [action.code, action]));
            const primary = byCode.get(surface.primaryActionCode);
            const primaryButton = primary
                ? `<button type="button" class="btn btn-sm btn-primary wcn-inbox-action-primary" data-wcn-trigger-action="${esc(primary.code)}" data-wcn-trigger-id="${esc(trigger.id)}"${primary.enabled === false || (state.submittingTriggerId === trigger.id && state.submittingActionCode === primary.code) ? ' disabled' : ''}>${esc(data.resolveLabel(primary.label))}</button>`
                : '';
            const overflowCodes = [...surface.secondaryActionCodes, ...surface.overflowActionCodes].filter(Boolean);
            const overflowItems = overflowCodes.map((code) => {
                const action = byCode.get(code);
                if (!action) { return ''; }
                const label = data.resolveLabel(action.label);
                const disabled = action.enabled === false || state.submittingTriggerId === trigger.id;
                const danger = ['declineMeeting', 'reject', 'cancel'].includes(code);
                return `<li><button type="button" class="dropdown-item wcn-menu-item${danger ? ' text-danger' : ''}" data-wcn-trigger-action="${esc(code)}" data-wcn-trigger-id="${esc(trigger.id)}"${disabled ? ' disabled' : ''}><i class="bx ${danger ? 'bx-x-circle' : 'bx-right-arrow-alt'}"></i><span>${esc(label)}</span></button></li>`;
            }).join('');
            const calendarItem = trigger.source?.deepLink
                ? `<li><button type="button" class="dropdown-item wcn-menu-item" data-wcn-trigger-open="${esc(trigger.id)}"><i class="bx bx-calendar-event"></i><span>${esc(t('ViewCalendar'))}</span></button></li>`
                : '';
            const overflowMenu = overflowItems || calendarItem
                ? `<div class="dropdown"><button type="button" class="btn btn-icon wcn-inbox-action-more dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-expanded="false" title="${esc(t('ActionsLabel'))}" aria-label="${esc(t('ActionsLabel'))}"><i class="bx bx-dots-vertical-rounded icon-md"></i></button><ul class="dropdown-menu dropdown-menu-end m-0">${overflowItems}${calendarItem}</ul></div>`
                : '';
            return `<article class="wcn-row wcn-row-meeting${trigger.isUnread ? ' unread' : ''}" data-wcn-trigger="${esc(trigger.id)}">
                <span class="wcn-row-unread" aria-hidden="true"></span>
                <div class="wcn-row-body">
                    <div class="wcn-row-top">
                        <i class="bx bx-calendar-event text-info" aria-hidden="true"></i>
                        <span class="wcn-row-title">${esc(titleText)}</span>
                    </div>
                    <p class="wcn-row-summary">${esc(summaryText)}</p>
                    <div class="wcn-row-chips">${chip('meeting', 'bx-calendar-event', t('ChipMeetingInvite'))}${chip('info', 'bx-reply', t('TriggerOnlyLabel'))}</div>
                </div>
                <div class="wcn-trigger-actions">${primaryButton}${overflowMenu}</div>
            </article>`;
        }).join('');
        return cards;
    };

    // WC-1b — a degraded load state must read as itself, not as a generic failure: no permission, expired
    // session and dependency-down are distinct situations with distinct guidance. Retry is only offered where
    // retrying can actually help (a permission grant will not appear by pressing a button).
    const LOAD_ERROR_STATES = {
        forbidden: { icon: 'bx-lock-alt', title: 'NoAccessTitle', desc: 'NoAccessDesc', retry: false },
        unauthorized: { icon: 'bx-log-in-circle', title: 'SessionExpiredTitle', desc: 'SessionExpiredDesc', retry: false },
        unavailable: { icon: 'bx-cloud-off', title: 'UnavailableTitle', desc: 'UnavailableDesc', retry: true }
    };
    const renderErrorState = () => {
        const conf = LOAD_ERROR_STATES[state.loadError]
            || { icon: 'bx-error-circle', title: 'ErrorTitle', desc: 'ErrorDesc', retry: true };
        const retry = conf.retry
            ? `<button type="button" class="btn btn-sm btn-primary" data-wcn-retry>${esc(t('Retry'))}</button>`
            : '';
        return `<div class="wcn-system-page wcn-system-error" role="alert">
        <i class="bx ${conf.icon}"></i><h5>${esc(t(conf.title))}</h5><p>${esc(t(conf.desc))}</p>
        ${retry}
    </div>`;
    };

    // The whole app re-renders via innerHTML, which would orphan select2's
    // document-level handlers — tear instances down before the wipe, re-init the
    // panel selects after (golden DataTable single-select config).
    const teardownPanelSelect2 = () => {
        const jq = global.jQuery;
        if (!jq || !jq.fn || !jq.fn.select2) { return; }
        jq('#wcnApp select.select2-hidden-accessible').each(function () {
            try { jq(this).select2('destroy'); } catch (e) { /* noop */ }
        });
    };
    const syncPanelMultiSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $selection = $container.find('.select2-selection--multiple');
        const $rendered = $container.find('.select2-selection__rendered');
        if (!$selection.length) { return; }
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = global.jQuery('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = global.jQuery('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = global.jQuery('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = global.jQuery('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const values = Array.isArray($select.val()) ? $select.val() : [];
        const placeholder = String($select.data('placeholder') || '');
        $summary.text(placeholder);
        $rendered.attr('title', ($select.select2('data') || []).map((entry) => entry.text).join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', values.length > 0);
        $count.toggleClass('d-none', values.length === 0).text(String(values.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (values.length) {
            const $clear = global.jQuery(`<span class="dt-multi-clear-btn" role="button" aria-label="${esc(t('Reset'))}" title="${esc(t('Reset'))}">&times;</span>`);
            $clear.on('mousedown', (event) => { event.preventDefault(); event.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clear);
        }
    };
    const mountPanelSelect2 = () => {
        if (!state.filtersOpen) { return; }
        const jq = global.jQuery;
        if (!jq || !jq.fn || !jq.fn.select2) { return; }
        jq('#wcnApp .wcn-filter-panel select.select2').each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) { return; }
            $s.select2({
                dropdownParent: jq(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                containerCssClass: $s.prop('multiple') ? 'dt-inline-filter-multi' : '',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: !$s.prop('multiple'),
                closeOnSelect: !$s.prop('multiple')
            });
            if ($s.prop('multiple')) {
                global.requestAnimationFrame(() => syncPanelMultiSummary($s));
            }
            // select2 emits its change via jQuery.trigger('change'), which does NOT
            // reach the native document 'change' listener — so wire state here.
            $s.on('change.wcn', function () {
                const which = this.getAttribute('data-wcn-filter');
                const value = this.multiple ? ($s.val() || []) : (this.value || 'all');
                if (this.multiple) {
                    global.requestAnimationFrame(() => syncPanelMultiSummary($s));
                }
                applyFilterValue(which, value);
                render();
            });
        });
    };

    const renderUnsafe = () => {
        const root = document.getElementById('wcnApp');
        if (!root) { return; }
        const snap = captureFocus();
        teardownPanelSelect2();
        destroyWorkCenterDataTable();
        if (root.dataset.wcnPage === 'detail') {
            /*
             * Loading and error are answered BEFORE "not found". This branch used to return first, so while the
             * projection was still in flight the page stated the task did not exist — an error message about
             * data that had simply not arrived, and then it flipped to the task. The list page had the skeleton
             * all along; the detail page never reached it.
             */
            if (state.loadState === 'loading') { root.innerHTML = renderLoadingState(); return; }
            if (state.loadState === 'error') { root.innerHTML = renderErrorState(); return; }

            const item = itemById(root.dataset.wcnItemId || '');
            state.selectedId = item ? item.id : null;
            if (item) { markSeen(item); }
            root.innerHTML = item
                ? detailHtml(item)
                : `<section class="card backbone-preview-section"><div class="wcn-detail-empty"><i class="bx bx-error-circle"></i><p>${esc(t('DetailItemNotFound'))}</p><a class="btn btn-label-secondary" href="${esc(listReturnUrl())}">${esc(t('DetailBackToList'))}</a></div></section>`;
            setupTimerTick();
            // Which arrow may act is derived from POSITION, after the list exists — the same rule, from the same
            // function, that the create form uses. A first row's ↑ and a last row's ↓ are disabled, and a
            // one-item list shows none at all: a control that can only ever refuse is worse than no control.
            root.querySelectorAll('.wcn-checks').forEach((list) => global.DitenCheckItem.applyMoveState(list));
            // The mouse path, attached after the list exists. The arrows above are the keyboard and
            // single-pointer path and do not depend on this succeeding.
            bindChecklistDrag(root, item);
            restoreFocus(snap);
            return;
        }
        if (state.loadState === 'loading') { root.innerHTML = renderLoadingState(); return; }
        if (state.loadState === 'error') { root.innerHTML = renderErrorState(); return; }
        const items = activeItems();
        let renderedItems = items;
        let main;
        switch (state.view) {
            case 'table':
                renderedItems = items.concat(triggerTableRows());
                main = renderTable(renderedItems);
                break;
            case 'focus': main = renderFocus(items); break;
            default: main = renderList(items);
        }
        /*
         * ⚠ THE NOTES AND AGENDA PANELS WERE REMOVED (2026-08-24, Tur B). Both were PERMANENTLY EMPTY: the only
         * code that fed them (`openQuickNote`, `openMeetingForm`) was deleted a round earlier because it wrote
         * to browser memory and nowhere else, and `state.notes` / `state.meetings` are initialised to `[]` and
         * never loaded. An empty panel is an unanswered question for whoever opens it.
         *
         * They come back together with the feature that fills them — see the backlog entry for the deferred
         * personal-note and calendar work.
         */
        const sidePanel = '';

        const mainPanel = `<div id="wcn-main-panel" class="wcn-main${state.view === 'table' ? '' : ' wcn-main-open'}" role="tabpanel" aria-labelledby="wcn-tab-${state.tab}" tabindex="0">${main}</div>`;
        const workspaceToolbar = state.view === 'table'
            ? buildChips()
            : `<section class="card wcn-workspace-card wcn-workspace-toolbar-card">${buildChips()}</section>`;
        const workspace = workspaceToolbar
            + `<div class="wcn-layout-wrap">${mainPanel}${sidePanel}</div>`;

        root.innerHTML = buildHeader() + buildDelegationBanner() + buildTabs() + buildFilterRow()
            + workspace;
        setupTimerTick();
        mountPanelSelect2();
        if (state.view === 'table') { mountWorkCenterDataTable(renderedItems); }
        restoreFocus(snap);
        syncUrl();
    };

    /*
     * WHERE THE READER WAS — the SCROLL half.
     *
     * MEASURED: scrollY 600 → add a subtask with Enter → scrollY 0. The page does not reload; `innerHTML = …`
     * collapses the document for an instant and the browser clamps the scroll to the shorter body. On a long
     * detail page that means scrolling back after every single write, and the owner read it as "the page keeps
     * refreshing". The FOCUS half already existed (captureFocus/restoreFocus) and only had to learn about the
     * text boxes this page grew.
     */
    /*
     * WHICH ELEMENT ACTUALLY SCROLLS — measured, because the obvious answer is wrong here.
     *
     * `window.scrollY` reads 0 on this theme even when the page is plainly scrolled: the shell scrolls the
     * ROOT ELEMENT (`html.layout-navbar-fixed`), and its position is `document.scrollingElement.scrollTop`. A
     * first attempt at this fix captured and restored window.scrollY, i.e. it restored 0 to 0 and changed
     * nothing on screen while looking correct in code.
     */
    const scroller = () => global.document.scrollingElement || global.document.documentElement;

    const render = () => {
        warnIfPanelOpen();
        const box = scroller();
        const scrollTop = box ? box.scrollTop : 0;
        const scrollLeft = box ? box.scrollLeft : 0;
        try {
            renderUnsafe();
            /*
             * After the paint, not before: the new DOM has to exist before it can be scrolled back to. The swap
             * shortens the document for an instant and the browser clamps the offset to the shorter body, which
             * is why an ordinary write threw the reader to the top of a long page.
             */
            const restoreScroll = () => {
                const after = scroller();
                if (!after) { return; }
                after.scrollTop = scrollTop;
                after.scrollLeft = scrollLeft;
            };
            if (scrollTop || scrollLeft) {
                restoreScroll();
                /*
                 * AND AGAIN ON THE NEXT FRAME. The first assignment lands before the replaced content has been
                 * laid out, so the browser clamps it to the height of a page that is momentarily shorter —
                 * measured live: the offset was restored to 0 and looked, in code, exactly like a working fix.
                 * The second pass runs once the new height is real.
                 */
                if (typeof global.requestAnimationFrame === 'function') {
                    global.requestAnimationFrame(restoreScroll);
                }
            }
        } catch (error) {
            state.loadState = 'error';
            state.loadError = error;
            const root = document.getElementById('wcnApp');
            if (root) { root.innerHTML = renderErrorState(); }
            console.error('WorkCenterNext render failed.', error);
        }
    };

    // WorkCenter uses the shared MOD-0013 Notyf surface. A page-local toast would
    // introduce a second position, colour system and accessibility lifecycle.
    const toast = (message, type) => global.showToast?.(message, type || 'success');

    // ── Lifecycle state machine (mock, spec v2 §4/§5) ─────────────────────────
    // Tab membership is derived from assignmentMode + claimed (data.tabFor), never
    // hard-coded from lifecycle — so a claimed pool item lands in "İşlerim".
    const setProjectionState = (item, normalizedStatus, taskLifecycle, nativeStatusText) => {
        item.normalizedStatus = normalizedStatus;
        item.status = normalizedStatus === 'InProgress' ? 'In Progress' : normalizedStatus;
        if (taskLifecycle) {
            item.taskLifecycle = taskLifecycle;
            item.lifecycle = taskLifecycle;
        }
        if (nativeStatusText) { item.nativeStatusText = nativeStatusText; }
        item.tab = data.tabFor(item);
    };

    // Returns the outcome kind so the toast can explain what happened.
    const applyTransition = (item, key) => {
        switch (key) {
            // Triage-inbox admission — take on a directly-assigned item; it moves
            // from the Inbox to İşlerim but stays at its current lifecycle stage.
            case 'accept':
                if (item.itemType === 'meetingInvite') {
                    item.dismissed = true;
                    state.meetings.push({
                        id: item.sourceId,
                        title: item.title,
                        start: item.meetingStart || '09:00',
                        end: item.meetingEnd || '10:00',
                        location: item.meetingLocation || '—',
                        owner: item.requester
                    });
                    return 'removed';
                }
                item.accepted = true;
                item.admissionState = 'admitted';
                item.ownershipState = 'owned';
                item.assignee = item.assignee || data.currentUser.name;
                item.tab = data.tabFor(item);
                return 'moved';
            // Pool admission — claim a group-queue item / accept an offered one.
            case 'claim':
            case 'acceptOffer':
                item.claimed = true;
                item.admissionState = 'admitted';
                item.ownershipState = 'owned';
                item.assignee = data.currentUser.name;
                if (item.itemType === 'task') { setProjectionState(item, 'Pending', 'Open', 'Open'); }
                else { setProjectionState(item, 'InProgress', null, item.itemType === 'review' ? 'In Review' : 'In Progress'); }
                return key === 'claim' ? 'claimed' : 'moved';
            case 'decline':
                item.dismissed = true; return 'removed';
            case 'release':
                // Drop a claimed group-queue item back to the pool for others.
                item.claimed = false; item.assignee = null;
                item.admissionState = 'pendingClaim';
                item.ownershipState = 'unowned';
                // No queue name here: the projection never says WHICH pool an item belongs to, and inventing one
                // ("Operasyon Kuyruğu") put a non-existent team's name on real work. The ungrouped state gets its
                // own translated label instead. Naming the queue is WC-3 contract work (BL-031 a/b).
                setProjectionState(item, 'Pending', item.itemType === 'task' ? 'Open' : null, t('PoolUnassigned'));
                return 'released';
            case 'approve': setProjectionState(item, 'Done', null, 'Onaylandı'); return 'resolved';
            case 'signoff': setProjectionState(item, 'Done', null, 'İmzalandı'); return 'resolved';
            case 'resolve': setProjectionState(item, 'Done', null, 'Çözüldü'); return 'resolved';
            case 'start':
            case 'resume':
                /*
                 * Resuming clears the wait AND the reader's own snooze.
                 *
                 * ⚠ The old comment here said `segmentFor` keys off `waitingOn`/`snoozedUntil`. Measured and
                 * wrong: `segmentFor` lives in the data layer and reads `normalizedStatus` and the plan date —
                 * it has never looked at either field. What clearing `waitingOn` really does is end the wait the
                 * SERVER is projecting; what clearing the snooze does is put the item back on this reader's
                 * screen, which is the whole of BL-181.
                 */
                item.waitingOn = null; item.snoozedUntil = null;
                setProjectionState(item, 'InProgress', 'InProgress', 'Devam ediyor');
                item.executionState = 'active';
                item.timerState = 'running';
                item.timesheet = item.timesheet || { running: false, startedAt: null, loggedMinutes: 0 };
                item.timesheet.running = true; item.timesheet.startedAt = Date.now();
                return 'timerStart';
            case 'pause':
                foldTimer(item);
                item.executionState = 'paused';
                item.timerState = 'paused';
                return 'timerPause';
            case 'complete':
                foldTimer(item);
                item.waitingOn = null; item.snoozedUntil = null;
                item.executionState = 'notStarted';
                item.timerState = 'inactive';
                if (item.reviewRequired) { setProjectionState(item, 'Pending', 'PendingReview', 'İnceleme bekliyor'); return 'toReview'; }
                setProjectionState(item, 'Done', 'Done', 'Kapandı'); return 'resolved';
            case 'inquire':
            case 'requestInfo':
                /*
                 * Showcase-only round-trip. It still has to write a CONTRACT-VALID waitingContext: it produced
                 * type 'information' (a value the contract now rejects) and a waitingOn with no id, which is the
                 * shape the executable contract now declares. The fixtures and the resolver were aligned when the
                 * vocabulary was settled; this runtime writer was missed, which is exactly how the divergence
                 * started in the first place.
                 */
                item.waitingOn = item.waitingOn || item.requester;
                item.waitingContext = item.waitingContext || {
                    type: 'externalInformation',
                    // A typed identity, or nothing. A name with no id is not an identity the client can act on.
                    waitingOn: item.requesterId
                        ? { id: item.requesterId, displayName: item.waitingOn || null }
                        : null,
                    since: new Date().toISOString(),
                    expectedUntil: null
                };
                setProjectionState(item, 'Waiting', item.itemType === 'task' ? 'Waiting' : null, 'Bilgi bekleniyor');
                return 'updated';
            case 'reject':
            case 'return':
            case 'delegate':
            case 'reassign':
            case 'dispute':
                item.dismissed = true; return 'removed';
            default:
                return 'updated';
        }
    };

    const toastForOutcome = (outcome, label, reason, item) => {
        switch (outcome) {
            case 'claimed': toast(tf('ToastClaimed', item.title)); break;
            case 'released': toast(tf('ToastReleased', item.title)); break;
            case 'moved': toast(tf('ToastMovedToWorkCenter', label)); break;
            case 'removed': toast(tf('ToastItemRemoved', label)); break;
            case 'toReview': toast(tf('ToastSentToReview', item.title)); break;
            case 'timerStart': toast(tf('ToastTimerStarted', item.title)); break;
            case 'timerPause': toast(tf('ToastTimerPaused', formatMinutes(item.timesheet.loggedMinutes))); break;
            case 'resolved': toast(tf('ToastAction', label)); break;
            default: toast(reason ? tf('ToastActionReason', label, reason) : tf('ToastAction', label));
        }
    };

    /*
     * A REAL work item owned by MOD-0024. Its actions must go to the engine; the browser-side transitions below
     * are a fixture-era demonstration and would otherwise change the screen while the database keeps the old
     * state — exactly what happened when "Başlat" moved a row to "Devam ediyor" while GET still returned Open.
     */
    const isRealTaskItem = (item) =>
        item && item.provenance !== 'fixture' && item.source?.providerCode === 'tasks';

    /*
     * Send the transition to the engine and re-read the projection. Nothing is applied optimistically: the server
     * decides, and the refreshed projection is the only source of the new state.
     */
    /*
     * ══ TRANSITION BODY VOCABULARY (BL-043) ═══════════════════════════════════════════════════════════════
     * The shape of the body each action sends, declared ONCE, here.
     *
     * THE DEFECT. Every transition used to post the same generic body — {expectedVersion, reasonCode, note} —
     * while three endpoints ask for something else entirely: InquireTaskItemRequest(ExpectedVersion, Reason),
     * ReturnTaskItemRequest(ExpectedVersion, Reason), ReassignTaskItemRequest(ExpectedVersion, AssigneeUserId,
     * Reason). All three answered 400 "The Reason field is required." — so `inquire`, `return` and `reassign`
     * had never worked from the UI at all, and the Waiting segment could not be filled by any means.
     *
     * THE DIRECTION IS DELIBERATE: the client was fixed, not the server. `Reason` is required on purpose —
     * TaskModels.cs says why ("a refusal the requester cannot understand only moves the problem"). Making the
     * field optional to match the client would have deleted that rule silently.
     *
     * WHY A DECLARED MAP RATHER THAN A COMMENT. This is the WC-1 lesson: a value that lives in two places and is
     * declared in neither drifts, and nothing notices. task-transition-contract.test.js reads THIS map and the
     * C# records in TaskModels.cs and asserts they agree field for field — so the next endpoint that changes its
     * DTO fails a test instead of a user's click.
     */
    const TRANSITION_BODIES = {
        // The three that were broken: a REQUIRED reason, named `reason` because that is what the DTO calls it.
        /*
         * `waitingOnUserId` is OPTIONAL and undefined when nobody was named — deliberately undefined rather
         * than null, so `JSON.stringify` omits the key entirely and the request is byte-identical to the one
         * this client sent before the field existed. A wait on a supplier or a customer has nobody here to
         * name, and the reason sentence already says what is being waited for.
         */
        inquire: ({ expectedVersion, reason, waitingOnUserId }) =>
            ({ expectedVersion, reason, waitingOnUserId: waitingOnUserId || undefined }),
        return: ({ expectedVersion, reason }) => ({ expectedVersion, reason }),
        // Plus the person being handed the work — see the picker in the reason dialog.
        reassign: ({ expectedVersion, reason, assigneeUserId }) => ({ expectedVersion, assigneeUserId, reason }),

        // Everything else takes TaskTransitionRequest(ExpectedVersion, ReasonCode, Note) — the generic body that
        // was being sent to all ten. It is correct for these; it was only ever wrong for the three above.
        __default: ({ expectedVersion, reason }) => ({ expectedVersion, reasonCode: null, note: reason || null })
    };

    /** Actions whose DTO requires a non-empty Reason — the dialog must not let them through empty. */
    const REASON_REQUIRED_ACTIONS = ['inquire', 'return', 'reassign'];

    /** Actions that must also name the person receiving the work. */
    const ASSIGNEE_REQUIRED_ACTIONS = ['reassign'];

    /*
     * Actions that may ALSO name a person, without requiring one.
     *
     * `inquire` is the first: "waiting on Ayşe" is far more actionable than "waiting", and the projection has
     * carried a `waitingOn` slot since WC-1 with nothing to put in it. Kept as a DECLARED list beside its
     * required sibling rather than an `if (action.code === 'inquire')`, because that is how the required one
     * grew a second copy last time.
     *
     * ⚠ OPTIONAL IS THE POINT. A wait is often on somebody this system has never heard of — a supplier, a
     * customer, an authority — and forcing a selection there would make the honest answer unreachable.
     */
    const WAITING_ON_ACTIONS = ['inquire'];

    const buildTransitionBody = (actionCode, parts) =>
        (TRANSITION_BODIES[actionCode] || TRANSITION_BODIES.__default)(parts);

    const submitRealTransition = async (item, action, reason, assigneeUserId, waitingOnUserId) => {
        const label = actionLabel(action);
        state.submittingItemId = item.id;
        state.submittingActionCode = action.code;
        render();

        // The concurrency token from the projection — an expected-version write, so a stale screen loses cleanly.
        const expectedVersion = Number(item.concurrency?.token ?? 0);
        // The body's shape comes from the vocabulary above, never from a guess at this call site.
        const result = await global.TasksApi.transition(
            item.id,
            action.code,
            buildTransitionBody(action.code, { expectedVersion, reason, assigneeUserId, waitingOnUserId }));

        state.submittingItemId = null;
        state.submittingActionCode = null;

        if (result.ok) {
            await loadWorkItems();
            render();
            // The task's TITLE, never its id — a GUID means nothing to the person reading the toast.
            toast(tf('ToastActionApplied', label, item.title));
            return;
        }

        // A 409 means two very different things, and they must not share a message. A CONCURRENCY conflict is
        // "someone changed it first, here is the fresh screen"; a workflow BLOCK is "the approver has not released
        // this yet" — nothing was overwritten and refreshing changes nothing. Routing every 409 to the concurrency
        // branch told the user a confident lie, which is worse than the raw server error it replaced.
        if (global.TasksApi.isConcurrencyConflict(result)) {
            await loadWorkItems();
            render();
            toast(t('ErrorConcurrencyRefreshed'), 'error');
            return;
        }

        if (global.TasksApi.isTransitionBlocked(result)) {
            // Re-read anyway: the projection's own disabled reasons come from the same state that just refused us,
            // so the row should stop offering the action it cannot honour.
            await loadWorkItems();
            render();
            toast(global.TasksApi.failureMessage(result), 'error');
            return;
        }

        render();
        toast(global.TasksApi.failureMessage(result), 'error');
    };

    /*
     * ── Phase 2 writes ────────────────────────────────────────────────────────
     * Same shape as submitRealTransition: send the concurrency token, then RE-READ the projection. The checklist
     * carries its OWN version (the run is a separate document from the task), so a tick is an expected-version
     * write against the checklist, not against the task.
     */
    /*
     * EMPTIES A REPEATED-ENTRY BOX after its contents have actually been written.
     *
     * `restoreFocus` puts a half-typed value back after a repaint, which is right for a repaint somebody else
     * caused — you do not lose a sentence because a timer ticked. It cannot tell that apart from a repaint the
     * WRITE itself caused: the re-rendered box is empty either way, and `!node.value` reads that emptiness as
     * "nothing to protect" and restores. So a comment posted with Enter went to the server, appeared in the
     * feed, and left its own text sitting in the box for the next sentence to be typed on top of.
     *
     * Only the caller knows a write succeeded and which box it consumed, so the caller says so — AFTER the
     * render, because that is when the restored value is there to clear. The button path never showed this: the
     * focus snapshot is a button then, not a text field, so nothing was restored to begin with.
     */
    /*
     * ── EDITING AND WITHDRAWING A COMMENT (2026-08-14) ───────────────────────────────────────────────────────
     *
     * Comments were immutable and both endpoints refused to exist. What opened them is the TRAIL: an edit that
     * says it was edited, and a withdrawal that leaves a marker where the sentence stood.
     *
     * Neither is applied optimistically and neither is decided here. The server owns the author rule; this only
     * offers the control the projection said was offerable (`entry.editable`) and reports whatever comes back.
     */
    const editComment = async (taskId, commentId) => {
        const item = itemById(taskId);
        const entry = (item?.activity || []).find((candidate) => String(candidate.id) === String(commentId));
        if (!item || !entry) { return; }

        if (!isRealTaskItem(item)) {
            console.warn(`[WorkCenterNext] Comment edit ignored for non-engine item ${taskId} `
                + `(provider="${item.source?.providerCode || 'unknown'}") — no backend owns it.`);
            return;
        }

        // The shared confirm's TEXTAREA, seeded with what the comment says now — an edit box that starts empty
        // asks the author to retype a sentence they only wanted to fix.
        sharedConfirm({
            title: t('CommentEdit'),
            confirmText: t('CommentEditSave'),
            input: {
                label: t('CommentEditLabel'),
                placeholder: entry.text || '',
                value: entry.text || '',
                validate: (value) => (String(value || '').trim() ? null : t('ErrorCommentTextInvalid'))
            },
            onConfirm: async (value) => {
                const text = String(value || '').trim();
                if (!text || text === entry.text) { return; }
                await afterPhase2Write(
                    await global.TasksApi.updateComment(taskId, commentId, { text }), 'ToastCommentEdited');
            }
        });
    };

    const withdrawComment = async (taskId, commentId) => {
        const item = itemById(taskId);
        if (!isRealTaskItem(item)) {
            console.warn(`[WorkCenterNext] Comment withdrawal ignored for non-engine item ${taskId} `
                + `(provider="${item?.source?.providerCode || 'unknown'}") — no backend owns it.`);
            return;
        }

        /*
         * THIS ONE ASKS, and the personal note deliberately does not. The difference is who else is affected: a
         * private note has one reader, while a withdrawn comment leaves a visible gap in a conversation other
         * people have already read and may have answered. That is worth one question.
         */
        sharedConfirm({
            title: t('CommentWithdraw'),
            subtext: `<div class="wcn-confirm-body">${esc(t('CommentWithdrawConfirm'))}</div>`,
            type: 'danger',
            confirmText: t('CommentWithdraw'),
            onConfirm: async () => {
                await afterPhase2Write(
                    await global.TasksApi.withdrawComment(taskId, commentId), 'ToastCommentWithdrawn');
            }
        });
    };

    /*
     * The tab, written into the address so the link says what the reader is looking at (BL-087).
     *
     * Guarded on `replaceState` existing: a host without the History API keeps a working page and loses only the
     * shareable address, which is the same rule every other optional capability on this surface follows.
     */
    const writeDetailTabToAddress = () => {
        if (!global.history || typeof global.history.replaceState !== 'function') { return; }
        const url = global.location.pathname + global.location.search
            + (state.detailTab === 'activity' ? '#etkinlik' : '');
        global.history.replaceState(null, '', url);
    };

    const consumeEntryBox = (marker) => {
        const box = document.querySelector(`#wcnApp [${marker}]`);
        if (box) { box.value = ''; }
    };

    const afterPhase2Write = async (result, successKey, ...successArgs) => {
        if (result.ok) {
            await loadWorkItems();
            render();
            // VARIADIC because one message needs two facts: "{title} · {date} tarihine ertelendi". Passing only
            // the first would have printed the placeholder for the second, which is the raw-key failure wearing a
            // different hat.
            toast(successArgs.length ? tf(successKey, ...successArgs) : t(successKey));
            return true;
        }

        if (global.TasksApi.isConcurrencyConflict(result)) {
            // Someone changed it first — show the truth, then say so.
            await loadWorkItems();
            render();
            toast(t('ErrorConcurrencyRefreshed'), 'error');
            return false;
        }

        // A blocked write (checklist incomplete, approval outstanding) is also a 409, but it is a RULE, not a race.
        if (global.TasksApi.isTransitionBlocked(result)) {
            await loadWorkItems();
            render();
            toast(global.TasksApi.failureMessage(result), 'error');
            return false;
        }

        render();
        toast(global.TasksApi.failureMessage(result), 'error');
        return false;
    };

    const toggleChecklistItem = async (taskId, itemCode, completed) => {
        const item = itemById(taskId);
        if (!isRealTaskItem(item)) {
            console.warn(`[WorkCenterNext] Checklist toggle ignored for non-engine item ${taskId} `
                + `(provider="${item?.source?.providerCode || 'unknown'}") — no backend owns it.`);
            return;
        }

        const result = await global.TasksApi.setChecklistItemState(taskId, {
            itemCode,
            completed,
            expectedVersion: Number(item.checklist?.version ?? 0)
        });
        await afterPhase2Write(result, 'ToastChecklistUpdated');
    };

    /*
     * ADD an item to a task's checklist — the write the detail page never had.
     *
     * `expectedVersion` comes from the projected run, and **0 is a real value**: the provider ships an empty
     * container at version 0 for a task with no run at all, and AddChecklistItemHandler reads that as "start
     * one". Coercing it to 1 would claim a document that is not there and turn a first item into a phantom
     * concurrency conflict for the only person on the page.
     */
    const addChecklistItem = async (taskId, text) => {
        const trimmed = String(text || '').trim();
        // Silence on empty, like the subtask row: the placeholder already says what the box is for, and a toast
        // for pressing Enter in an empty field is noise.
        if (!trimmed) { return; }

        const item = itemById(taskId);
        if (!isRealTaskItem(item)) {
            console.warn(`[WorkCenterNext] Checklist add ignored for non-engine item ${taskId} `
                + `(provider="${item?.source?.providerCode || 'unknown'}") — no backend owns it.`);
            return;
        }

        const result = await global.TasksApi.addChecklistItem(taskId, {
            text: trimmed,
            requirement: state.checklistDraftLevel,
            expectedVersion: Number(item.checklist?.version ?? 0)
        });
        if (await afterPhase2Write(result, 'ToastChecklistItemAdded')) {
            consumeEntryBox('data-diten-check-input');
        }
    };

    /*
     * ── The three writes the detail page never had ───────────────────────────────────────────────────────────
     *
     * `Requirement`, `EvidenceRequired` and the item's own text were stored from the moment a task was born and
     * then frozen: the create form could set all three, and the task itself could change none of them. So the
     * checklist was a decision made once, before any of the work that would teach you what it should say.
     *
     * Each one sends `expectedVersion` from the projected run. That is not ceremony — a write issued without it
     * earlier in this module reported success and changed nothing, which is indistinguishable from working right
     * up until the reload.
     */

    /** The projected checklist item behind a row, so a PUT can send the fields it is not changing. */
    const checklistItemOf = (taskId, code) =>
        (itemById(taskId)?.checklist?.items || []).find((c) => c.id === code) || null;

    const checklistWrite = async (taskId, run, toast) => {
        const item = itemById(taskId);
        if (!isRealTaskItem(item)) {
            console.warn(`[WorkCenterNext] Checklist write ignored for non-engine item ${taskId} `
                + `(provider="${item?.source?.providerCode || 'unknown'}") — no backend owns it.`);
            return;
        }
        await afterPhase2Write(await run(item), toast);
    };

    /*
     * The PUT is a REPLACE of the item's editable face, not a patch, so every call sends all three fields —
     * "clear the evidence flag" and "don't mention the evidence flag" must not be the same request.
     *
     * `labelText` is sent as null for a template-owned item. The server refuses to reword one (its own reason
     * code, CHECKLIST_ITEM_TEMPLATE_OWNED), and sending the current text back would be asking for that refusal
     * on every level change.
     */
    const updateChecklistItem = (taskId, code, changes) => checklistWrite(taskId, (item) => {
        const current = checklistItemOf(taskId, code);
        return global.TasksApi.updateChecklistItem(taskId, code, {
            labelText: current?.templateOwned ? null : (changes.labelText ?? current?.text ?? ''),
            requirement: changes.requirement ?? current?.requirement ?? 'Optional',
            evidenceRequired: changes.evidenceRequired ?? !!current?.evidenceRequired,
            expectedVersion: Number(item.checklist?.version ?? 0)
        });
    }, 'ToastChecklistUpdated');

    const removeChecklistItem = (taskId, code) => checklistWrite(taskId, (item) =>
        global.TasksApi.removeChecklistItem(taskId, code, {
            expectedVersion: Number(item.checklist?.version ?? 0)
        }), 'ToastChecklistUpdated');

    /*
     * Reordering sends the WHOLE order in one call. Per-item position writes were the alternative and they lose
     * two ways: N requests for one move, and — because each lands independently — two people reordering at once
     * interleave into an order neither of them chose.
     *
     * The order is computed from the PROJECTION, not read back out of the DOM: the DOM is a picture of the
     * projection, and deriving the payload from the picture is how a rendering bug becomes a stored fact.
     */
    const moveChecklistItem = (taskId, code, direction) => checklistWrite(taskId, (item) => {
        const codes = (item.checklist?.items || []).map((c) => c.id);
        const from = codes.indexOf(code);
        const to = direction === 'up' ? from - 1 : from + 1;
        if (from < 0 || to < 0 || to >= codes.length) { return Promise.resolve({ ok: true, status: 204 }); }
        codes.splice(to, 0, codes.splice(from, 1)[0]);
        return global.TasksApi.reorderChecklist(taskId, {
            itemCodes: codes,
            expectedVersion: Number(item.checklist?.version ?? 0)
        });
    }, 'ToastChecklistUpdated');

    /*
     * The same reorder, arrived at by DRAGGING instead of by pressing.
     *
     * One index instead of one step, and everything else identical — same whole-list payload, same version, same
     * refusal path. The order is still computed from the PROJECTION; the DOM contributes only WHERE the row was
     * dropped, which is the one fact the projection cannot know. Deriving the whole payload from the picture is
     * how a rendering bug becomes a stored fact.
     */
    const dropChecklistItem = (taskId, code, newIndex) => checklistWrite(taskId, (item) => {
        const codes = (item.checklist?.items || []).map((c) => c.id);
        const from = codes.indexOf(code);
        if (from < 0 || newIndex < 0 || newIndex >= codes.length || from === newIndex) {
            // Dropped where it started, or dropped somewhere the projection does not recognise. Not an error and
            // not a write — a no-op that still reports success, so the caller's toast logic needs no special case.
            return Promise.resolve({ ok: true, status: 204 });
        }
        codes.splice(newIndex, 0, codes.splice(from, 1)[0]);
        return global.TasksApi.reorderChecklist(taskId, {
            itemCodes: codes,
            expectedVersion: Number(item.checklist?.version ?? 0)
        });
    }, 'ToastChecklistUpdated');

    /*
     * DRAG ON THE DETAIL PAGE — the decision that was "no" in BL-094 and is "yes" now.
     *
     * It was declined when the two screens were two components: the arrows alone satisfy WCAG 2.2 §2.5.7, and
     * drag was a convenience one screen could go without. They are ONE component now, and the same row being
     * draggable on the create form and not on the detail page is a difference nobody can justify to the person
     * using it. So the grip is drawn in both modes and both lists get Sortable.
     *
     * THE ARROWS ARE NOT REMOVED. They are the single-pointer alternative §2.5.7 requires and the entire keyboard
     * path — Sortable has none — so drag is added ON TOP of them, never in place of them.
     *
     * Settings are copied from the create form deliberately: `forceFallback` because native HTML5 drag renders
     * its own drag image and ignores `ghostClass`, and because it responds only to real OS input, so the gesture
     * could never be exercised by a test; `handle` because dragging from anywhere on the row would swallow the
     * clicks meant for the three controls sharing that 38px line.
     *
     * Guarded on `global.Sortable`: a host page that does not load it, or a jsdom test, must still get a working
     * card — the arrows already reorder, so a missing library costs the mouse gesture and nothing else.
     */
    const bindChecklistDrag = (root, item) => {
        if (!global.Sortable || !root || !item || isTerminal(item)) { return; }
        root.querySelectorAll('.wcn-checks').forEach((list) => {
            // The detail body is rebuilt on every render, so this is a fresh element each time; the flag stops a
            // second Sortable binding to the SAME node if render is ever called twice without replacing it.
            if (list.dataset.wcnSortable === '1') { return; }
            list.dataset.wcnSortable = '1';
            global.Sortable.create(list, {
                animation: 150,
                forceFallback: true,
                fallbackTolerance: 3,
                handle: '[data-diten-check-grip]',
                draggable: '[data-diten-check-row]',
                ghostClass: 'diten-checkitem-ghost',
                onEnd: (event) => {
                    const [taskId, itemCode] =
                        (event.item.getAttribute('data-diten-check-row') || '').split(':');
                    if (!taskId || !itemCode) { return; }
                    dropChecklistItem(taskId, itemCode, event.newIndex);
                }
            });
        });
    };

    const completeSubtask = async (subtaskId) => {
        // The subtask is its own row in state when it is also assigned to me, and then it carries its own
        // concurrency token.
        const subtask = itemById(subtaskId);

        /*
         * A CHECKBOX MUST NOT FAIL IN SILENCE. This used to warn to the console and return whenever the child
         * was not one of MY rows — the ordinary case for a subtask somebody else holds — so the box visibly did
         * nothing. The version comes from the shared resolver instead; the SERVER still decides, and its refusal
         * is what the reader sees.
         */
        const expectedVersion = await subtaskVersion(subtaskId);
        if (expectedVersion === null) { return; }
        const title = subtask?.title || '';

        const result = await global.TasksApi.transition(subtaskId, 'complete', { expectedVersion });
        await afterPhase2Write(result, 'ToastActionApplied', title);
    };

    const postComment = async (taskId, text) => {
        const value = String(text || '').trim();
        if (!value) { toast(t('CommentTextRequired'), 'error'); return; }
        if (value.length > COMMENT_MAX_LENGTH) { toast(tf('CommentTooLong', COMMENT_MAX_LENGTH), 'error'); return; }

        const item = itemById(taskId);
        if (!isRealTaskItem(item)) {
            /*
             * Showcase items have no engine behind them, so a comment on one is a demonstration and stays local.
             * Real items go to the server and nothing is applied optimistically — the refreshed projection is the
             * only source of what the feed now says.
             */
            if (item) {
                item.activity.unshift({
                    actor: data.currentUser.name, kind: 'comment', text: value, atMs: data.referenceDate(item.provenance)
                });
                render();
                toast(t('ToastCommentPosted'));
            }
            return;
        }

        const result = await global.TasksApi.addComment(taskId, { text: value });
        // A DIFFERENT key from the fixture branch above: this comment really was posted to the engine, and
        // 'ToastCommentPosted' says "(mock)" in all seven languages — correct for the local-only path, a lie
        // here.
        if (await afterPhase2Write(result, 'ToastCommentPostedReal')) {
            consumeEntryBox('data-wcn-comment-input');
        }
    };

    const addSubtask = async (parentId, title) => {
        const text = String(title || '').trim();
        if (!text) { toast(t('SubtaskTitleRequired'), 'error'); return; }

        const parent = itemById(parentId);
        if (!isRealTaskItem(parent)) {
            console.warn(`[WorkCenterNext] Subtask add ignored for non-engine item ${parentId}.`);
            return;
        }

        /*
         * Defaults are INHERITED from the parent rather than guessed: a subtask of a High-priority task is not
         * suddenly Medium, and the person doing the parent work is a better default owner than whoever happened
         * to type the subtask. Everything stays editable afterwards — the row opens its own detail page.
         *
         * The work-item projection carries no priority, so the parent's own task record is read for it. If that
         * read fails the subtask is still created, with stated fallbacks and a warning rather than a silent guess.
         */
        let priority = 'Medium';
        let assigneeUserId = parent.assignee?.id || null;

        const parentTask = await global.TasksApi.get(parentId);
        if (parentTask.ok && parentTask.data) {
            priority = parentTask.data.priority || priority;
            assigneeUserId = parentTask.data.assigneeUserId || assigneeUserId;
        } else {
            console.warn(`[WorkCenterNext] Could not read parent ${parentId} for subtask defaults `
                + `(status ${parentTask.status}); falling back to priority=Medium and the parent's projected assignee.`);
        }

        // A subtask IS a task: the ordinary create endpoint, with a parent link. The server enforces one level.
        const payload = global.TaskForm.buildCreatePayload({
            title: text,
            // A pooled parent has no holder to inherit, so the creator takes it and reassigns from the detail.
            assignmentTarget: assigneeUserId ? 'Person' : 'SelfAssigned',
            assigneeUserId,
            dueAt: parent.dueAt || null,
            priority
        });
        payload.parentTaskItemId = parentId;

        const result = await global.TasksApi.create(payload);
        /*
         * WHICH ROW IS MINE. A write re-reads the whole list and repaints it, so a new row simply appears among
         * the others — the same paint the owner read as "the page refreshed". Marking it for a moment turns
         * "something changed" into "that one is the thing I just typed".
         */
        /*
         * ⚠ THE CREATE RESPONSE IS THE ID ITSELF, not an object around it — measured against the live endpoint
         * (`{ ok: true, status: 201, data: "f8536220-…" }`). The first version read `data.id`, got undefined,
         * and the new row was never marked; the test harness's own stub answered `{ id: "new" }` and hid it.
         * Both shapes are accepted so neither the server nor a future envelope can silence this again.
         */
        const newId = result.ok
            ? (typeof result.data === 'string' ? result.data : (result.data && result.data.id))
            : null;
        state.flashSubtaskId = newId ? String(newId) : null;
        if (await afterPhase2Write(result, 'ToastSubtaskAdded', text)) {
            // The third repeated-entry box on this page, with the same restore behind it.
            consumeEntryBox('data-wcn-subtask-input');
        }
        if (state.flashSubtaskId) {
            const flashed = state.flashSubtaskId;
            /*
             * The mark expires WITHOUT a re-render: the CSS animation runs once and is already over by the time
             * this fires, so all that is left to do is stop a LATER repaint from replaying it. Calling render()
             * here would repaint the page seconds after the user's last action — a paint nobody asked for, and
             * one that stepped on an open panel in the test suite before this comment existed.
             */
            global.setTimeout(() => {
                if (state.flashSubtaskId === flashed) { state.flashSubtaskId = null; }
            }, SUBTASK_FLASH_MS);
        }
    };

    const applyAction = (item, action, reason, assigneeUserId, waitingOnUserId) => {
        if (isRealTaskItem(item)) {
            submitRealTransition(item, action, reason, assigneeUserId, waitingOnUserId);
            return;
        }

        // Everything below only ever simulates. Real items from other providers still land here (their engines
        // are not wired yet) — say so rather than letting a fake transition look real.
        if (item && item.provenance !== 'fixture') {
            console.warn(`[WorkCenterNext] "${action.code}" on item ${item.id} `
                + `(provider="${item.source?.providerCode || 'unknown'}") is a MOCK transition — no backend call is `
                + 'made and the change will disappear on refresh.');
        }

        const label = actionLabel(action);
        state.submittingItemId = item.id;
        state.submittingActionCode = action.code;
        render();
        global.setTimeout(() => {
            const outcome = applyTransition(item, action.key);
            markSeen(item);
            item.activity = item.activity || [];
            // atMs, not a pre-computed `ago` — ACTIVITY_RELATIVE_TIME_FORBIDDEN, same reasoning as applyPlan.
            item.activity.push({
                actor: data.currentUser.name,
                kind: 'event',
                eventKey: 'AuditActionStamp',
                actionLabel: label,
                atMs: data.referenceDate(item.provenance)
            });
            state.submittingItemId = null;
            state.submittingActionCode = null;
            render();
            toastForOutcome(outcome, label, reason, item);
        }, 350);
    };

    // Plan / re-plan for a SHOWCASE item only — sets the PERSONAL planned date (spec v2 §4), which is distinct
    // from the source due date; SLA (source) is never overwritten. Local and optimistic on purpose: there is no
    // engine behind a fixture, so this IS the whole write.
    const applyPlan = (item, dateStr, label) => {
        item.plannedDate = dateStr;
        if (item.lifecycle === 'Open') { setProjectionState(item, item.normalizedStatus, 'Planned', item.nativeStatusText); }
        markSeen(item);
        // `atMs`, not a pre-computed `ago` — the contract forbids a relative count (ACTIVITY_RELATIVE_TIME_FORBIDDEN):
        // whoever computes it freezes it, and it goes stale the moment the tab stays open.
        item.activity.push({
            actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: label,
            atMs: data.referenceDate(item.provenance)
        });
        render();
        toast(tf('ToastPlanned', item.title, dateStr));
    };

    /*
     * Plan / re-plan for a REAL task — posts to the engine and applies NOTHING optimistically. The date is only
     * ever shown once the server has actually stored it and the projection has been re-read; a request that fails
     * must leave the screen exactly as it was, or a rejected write would look identical to an accepted one.
     */
    const submitPlan = async (item, dateStr) => {
        const result = await global.TasksApi.plan(item.id, {
            expectedVersion: Number(item.concurrency?.token ?? 0),
            plannedDate: dateStr
        });
        await afterPhase2Write(result, 'ToastPlanSaved', dateStr);
    };

    const openDatePicker = (item, action) => {
        const label = actionLabel(action);
        const real = isRealTaskItem(item);
        if (!global.Swal) {
            if (real) { submitPlan(item, item.dueAt || data.todayIso).catch(reportSwalFailure); return; }
            applyPlan(item, item.dueAt || data.todayIso, label);
            return;
        }
        /*
         * ── THROUGH THE SHARED COMPONENT (2026-08-24, A3) ────────────────────────────────────────────────
         *
         * This was a raw `Swal.fire` with its own `<input class="form-control">` in a `html` string, which is
         * why it rendered a 38px title, an 18px description and a RED dismiss button beside the snooze
         * dialog's 18/13/neutral. It asks for ONE value, so it is a confirmation, so it goes through the
         * component that owns what a confirmation looks like.
         *
         * ⚠ NO NEW SEAM WAS OPENED. `inputType: 'text'` + `onOpen` + `validate` is exactly the path the snooze
         * dialog already takes, flatpickr and all — see `openSnooze`.
         */
        const seed = item.plannedDate || item.dueAt;
        sharedConfirm({
            title: label,
            subtext: outcomeLead(action),
            // The rail button's own glyph — one dictionary, so the button and the dialog it opens agree.
            icon: inboxActionIcon(action),
            confirmText: t('PlanConfirm'),
            input: {
                type: 'text',
                label: t('PlanDateLabel'),
                // A REAL EXAMPLE, not the field's own name repeated: the box says what a date looks like here.
                placeholder: t('DatePlaceholder'),
                onOpen: (input) => {
                    if (!input) { return; }
                    // ⚠ NO WRAPPER — the glyph is painted ON the box. See `.wcn-date-input` and the warning at
                    // `openSnooze`: a wrapper makes `Swal.getInput()` null and takes the validator, the focus
                    // and the Enter key with it.
                    input.classList.add('wcn-date-input');
                    if (global.flatpickr) {
                        // Re-planning opens the picker seeded with the EXISTING plan, so moving a date is an
                        // edit of it rather than starting blank; falling back to the source due date only when
                        // there is no plan yet.
                        global.flatpickr(input, { dateFormat: 'Y-m-d', defaultDate: seed || undefined, disableMobile: true });
                    } else {
                        input.type = 'date';
                        if (seed) { input.value = seed; }
                    }
                },
                validate: (value) => (value ? null : t('PlanDateLabel'))
            },
            onConfirm: (value) => {
                if (!value) { return; }
                const applied = real ? submitPlan(item, value) : applyPlan(item, value, label);
                if (applied && typeof applied.catch === 'function') { applied.catch(reportSwalFailure); }
            }
        });
    };

    // Swal's own promise chain runs well after the click that opened it, outside onClick's try/catch — so a
    // failure inside submitPlan needs its OWN net, the same one onClick's own catch gives every other write.
    const reportSwalFailure = (error) => {
        console.error('WorkCenterNext date picker failed.', error);
        toast(t('ErrorTitle'), 'error');
    };

    /*
     * ══ THE SHARED CONFIRM, REACHED THROUGH ONE SEAM ══════════════════════════════════════════════════════════
     *
     * MEASURED 2026-08-14: this file held FIFTEEN direct `Swal.fire(` calls while the product has exactly one
     * confirm implementation — `window.showConfirm` (_GlobalConfirmation.cshtml), which owns the icon circle,
     * the button classes, the reversed button order, the width, and the `scrollbarPadding/heightAuto` pair that
     * stops the navbar jumping when a dialog opens. Every raw call was re-deciding all of that by hand, and the
     * ones that forgot the last pair are why that navbar rule had to be written twice.
     *
     * ⚠ ALSO MEASURED, and it changes what could be used: `window.DitenModal` is **undefined** on this page —
     * premium-modal.js is not among the scripts the WorkCenterNext views load. `DitenModal.confirm` delegates to
     * `showConfirm` anyway ("a named seam, not a second one"), so calling `showConfirm` directly reaches the same
     * implementation. Routing through a global that is not loaded here would have been a silent no-op.
     *
     * WHAT THIS SEAM DOES **NOT** DO: it does not extend the shared component. `showConfirm` supports a TEXTAREA
     * input and nothing else, so the dialogs that need a date picker, a number, a select or a multi-field form
     * stay where they are and are reported rather than bent through a shape that does not fit — see BL-146.
     */
    /*
     * ── A DIALOG THAT IS NOT A CONFIRMATION STILL LOOKS LIKE THE PRODUCT (2026-08-24, A3) ──────────────────
     *
     * Four dialogs on this page cannot go through `showConfirm`: a menu with no confirm button, a four-field
     * form, a two-field action confirm, and a progress readout that asks nothing at all. They were raw
     * `Swal.fire` calls, so they inherited NONE of the product's dialog appearance — measured against the
     * snooze dialog: 38px title against 18px, 18px body against 13px, 512px popup against 400px, and a RED
     * dismiss button, because this theme's global default for `.swal2-cancel` is `btn-label-danger`.
     *
     * ⚠ THE PACKAGE IS NOT COPIED HERE. `window.DitenDialogAppearance` is declared once, in
     * `_GlobalConfirmation.cshtml`, and this reads it. Two copies of a look drift within a fortnight; a test
     * asserts the definition exists in exactly one place.
     *
     * The fallback is deliberate and loud rather than silent: a dialog with no appearance is a visible defect,
     * so it renders (the reader still gets to answer the question) and the console says why it looks wrong.
     */
    /*
     * ── SELECT2 INSIDE A DIALOG (2026-08-24, A2 job 4) ────────────────────────────────────────────────────
     *
     * The rest of this product picks from a select2; two dialog selects were native `<select>`s. This binds
     * them with the SAME configuration `mountPanelSelect2` uses — no second wrapper, no new options object.
     *
     * ⚠⚠ `dropdownParent` IS THE POPUP, AND THAT IS THE WHOLE POINT.
     *
     * select2's default parent is `<body>`, where its list lands at the library's own z-index. That is exactly
     * how flatpickr's calendar shipped BEHIND this dialog earlier in this session — calendar 1074, SweetAlert
     * 1090 — and every click on a day reached the page behind. It passed every test and survived days.
     * Parenting the list INSIDE the popup removes the stacking question rather than answering it with a number:
     * a descendant cannot be behind its ancestor.
     *
     * ⚠ THE `<select>` STAYS A DIRECT CHILD of whatever held it. select2 hides the original in place and
     * inserts `.select2-container` as its SIBLING, so `Swal.getInput()` (which walks the popup's fixed slot
     * list) still finds `.swal2-select`. This is measured, not assumed — a wrapper around a dialog input is
     * the defect that cost this session a whole round.
     */
    const bindDialogSelect2 = (element, popup) => {
        const jq = global.jQuery;
        if (!element || !jq || !jq.fn || !jq.fn.select2) { return false; }
        const $s = jq(element);
        if ($s.hasClass('select2-hidden-accessible')) { return true; }
        /*
         * ⚠ NO `placeholder` KEY UNLESS THERE IS A PLACEHOLDER — MEASURED, and it cost a real sentence.
         *
         * Passing `placeholder: ''` still switches select2's placeholder decorator ON, and that decorator
         * treats the first option with an EMPTY VALUE as the placeholder and renders nothing for it. The
         * waiting-on picker's first option is not a placeholder at all: "Belirli bir kişi değil" is a REAL
         * CHOICE (this file's own comment says so), and select2 blanked it — the control opened showing an
         * empty box where the native select had shown the words.
         */
        /*
         * ⚠ `selectionCssClass` DOES NOTHING ON THIS SELECT2 BUILD — MEASURED, and it shipped a visible defect.
         *
         * It was passed as `'form-select'` so the control would wear the product's field styling. The class
         * never reached the element: the rendered node measured `class="select2-selection
         * select2-selection--single"` with NO `form-select`, and its text came out at **18px** beside a
         * textarea, a label and a page full of controls at **15px** — which is what the owner photographed.
         * (`selectionCssClass` is a 4.1 option; this bundle ignores unknown keys silently.)
         *
         * `containerCssClass` IS honoured here, so the hook is a real class and the styling lives in
         * `backbone-custom.css` under `.wcn-dialog-select` — which is also where it belongs (FG-003), and how
         * this product already styles select2 on its other surfaces (the filter chips do the same).
         */
        const config = {
            dropdownParent: jq(popup || element.closest('.swal2-popup') || document.body),
            containerCssClass: 'wcn-dialog-select',
            dropdownCssClass: 'wcn-dialog-select-dropdown',
            minimumResultsForSearch: 10,
            width: '100%',
            allowClear: false
        };
        const declared = String($s.data('placeholder') || '');
        if (declared) { config.placeholder = declared; }
        $s.select2(config);
        return true;
    };

    const dialogLook = (options) => {
        if (typeof global.DitenDialogAppearance !== 'function') {
            console.error('[WorkCenterNext] window.DitenDialogAppearance is unavailable (is _GlobalConfirmation loaded?).');
            return {};
        }
        return global.DitenDialogAppearance(options);
    };
    /*
     * THE ICON, for a dialog that cannot go through `showConfirm`. Read from the published builder — the same
     * one the shared confirm uses — so the circle, its tint and the glyph cannot become a second design here.
     */
    const dialogIcon = (type, glyph) => (typeof global.DitenDialogAppearance === 'function'
        && typeof global.DitenDialogAppearance.iconHtml === 'function'
        ? global.DitenDialogAppearance.iconHtml(type, glyph)
        : '');
    // The class the product's dialog DESCRIPTION wears — 13px secondary copy, read from the same one place.
    const dialogDescriptionClass = () => (typeof global.DitenDialogAppearance === 'function'
        ? global.DitenDialogAppearance.description
        : '');

    const sharedConfirm = (options) => {
        const confirm = global.showConfirm;
        if (typeof confirm !== 'function') {
            // Never silently swallow the action: a confirm that cannot be shown must not read as "cancelled".
            console.error('[WorkCenterNext] window.showConfirm is unavailable (is _GlobalConfirmation loaded?).');
            toast(t('ErrorTitle'), 'error');
            return;
        }
        confirm(options.title, options.onConfirm, {
            /*
             * HTML, deliberately: the outcome sentence in front of a confirm is markup the caller already built,
             * and the wrapper renders `subtext` as HTML for exactly this.
             *
             * ⚠ AN INPUT PROMPT GETS NO GENERIC CONFIRMATION SENTENCE (2026-08-24, owner).
             *
             * "Devam etmek istediğinize emin misiniz?" is the shared component's default, and it appeared over
             * "Kaç dakika?", "Ne zaman?" and "Hangi modül?" — three questions it does not answer. A dialog that
             * ASKS FOR A VALUE is not asking for a confirmation, so it either says something of its own or says
             * nothing, and '' is now how it says nothing (see the wrapper).
             *
             * ⚠ A REAL CONFIRMATION IS UNTOUCHED: no `input`, no override, so the default still arrives — the
             * bulk confirm and the subtask cancel in this very module still ask whether the reader is sure.
             */
            subtext: options.subtext !== undefined ? options.subtext : (options.input ? '' : undefined),
            type: options.type || 'info',
            confirmButtonText: options.confirmText,
            /*
             * ⚠ ONE DISMISS WORD FOR THIS MODULE, AND IT IS NOT "İptal" (BL-202).
             *
             * MEASURED across the product: twelve of the fifteen `showConfirm` calls live in modules whose
             * actions are Delete / Remove / Publish / Reactivate — there "İptal" can only mean "never mind", and
             * they are LEFT ALONE. This module is the exception: two of its actions are literally named "iptal"
             * (Görevi iptal et, Alt görevi iptal et), so a dismiss button saying "İptal" offers the same word
             * for both answers to one question. Every dialog here says "Vazgeç" — one word, so a reader never
             * has to work out which "iptal" a button means from where it sits.
             */
            cancelButtonText: options.cancelText || t('DialogDismiss'),
            /*
             * WHETHER TO DRAW AN ICON AT ALL. Absent for every dialog that has one today, so nothing moves; a
             * dialog that is neither destructive nor a warning nor a question says so by leaving it out.
             */
            hideIcon: !!options.hideIcon,
            // WHICH GLYPH. Absent everywhere else, so every other dialog keeps the one its `type` gives it; the
            // circle, its colour and the button's colour stay the type's business either way.
            icon: options.icon,
            /*
             * WHAT THE ACTION IS ABOUT, in the box the wrapper already draws for it.
             *
             * MEASURED (2026-08-24): the product had TWO ways to name the record a confirm is about — this
             * `entityName` badge, used by ten call sites across six files, and a title quoted INSIDE the
             * sentence, used only by this module. Never both at once, but two mechanisms for one job.
             *
             * The badge won because it already exists, it is what the rest of the product speaks, and it is
             * ALREADY the framed box the owner asked for — a surface, a radius and padding from the theme, no
             * new design. The sentence gave its quoted title up in exchange (see `ConfirmBody`).
             */
            entityName: options.entityName,
            showInput: !!options.input,
            /*
             * WHAT KIND of box. Absent for every caller that wants prose — the wrapper still answers `textarea`,
             * which is what it always did. A caller that wants a date says so, attaches its picker through the
             * `didOpen` seam that already exists, and validates through the `inputValidator` that already exists.
             */
            inputType: options.input && options.input.type,
            inputLabel: options.input && options.input.label,
            inputPlaceholder: options.input && options.input.placeholder,
            /*
             * THE CHOICES, when the box is a `select`. The shared component's seventh and final parameter — a
             * `select` with no options is a box that cannot be used, which is why the "create in source" dialog
             * could not go through this seam and therefore went through none of the product's appearance either.
             */
            inputOptions: options.input && options.input.options,
            inputValidator: options.input && options.input.validate,
            /*
             * SEEDING THE BOX, through the wrapper's OWN `didOpen` seam.
             *
             * ⚠ The shared confirm has no `inputValue` option, and it is NOT being given one: it is a shared
             * component and does not grow to suit one module (standing rule, owner). `didOpen` already exists on
             * it for exactly this kind of need, so the value is written to the textarea the wrapper created,
             * after it created it. Nothing about the component changes.
             *
             * An edit box that opened EMPTY would ask the author to retype a sentence they only wanted to fix —
             * which is how an "edit" quietly becomes a rewrite.
             */
            /*
             * The seam serves two needs with one hook, because the wrapper offers one: seeding a textarea with
             * the sentence being edited, and handing the caller the input it just created so a picker can be
             * attached to it. Neither adds anything to the shared component.
             */
            didOpen: (options.input && options.input.value !== undefined) || (options.input && options.input.onOpen)
                ? (popup) => {
                    if (!popup) { return; }
                    if (options.input.value !== undefined) {
                        const box = popup.querySelector('textarea');
                        if (box) { box.value = options.input.value; box.select(); }
                    }
                    if (typeof options.input.onOpen === 'function') {
                        /*
                         * ⚠ `Swal.getInput()`, NOT A SELECTOR LIST — MEASURED, and it was wrong for one round.
                         *
                         * SweetAlert renders ALL its slots into the popup (input, file, range, select, checkbox,
                         * textarea) and hides the ones it is not using. `querySelector('.swal2-input,
                         * .swal2-select, …')` returns the first match in DOCUMENT ORDER, not in the order the
                         * selectors are written — so a `select` dialog was handed the hidden `.swal2-input` and
                         * its picker attached to a box nobody could see. The select stayed native and the defect
                         * was invisible except by measuring `select2-hidden-accessible`.
                         *
                         * The library already answers this exact question about itself, so it is asked.
                         */
                        const current = (global.Swal && typeof global.Swal.getInput === 'function' && global.Swal.getInput())
                            || popup.querySelector('.swal2-input, .swal2-select, .swal2-textarea');
                        options.input.onOpen(current, popup);
                    }
                }
                : undefined
        });
    };

    // Review meeting is a collaboration command, not a lifecycle transition. The
    // mock applies an explicit replacement projection after Calendar returns.
    const applyReviewMeeting = (item, whenStr, label) => {
        const [date, time] = String(whenStr).split(' ');
        const startTime = time || '09:00';
        const endHour = String(Math.min(23, parseInt(startTime, 10) + 1)).padStart(2, '0');
        const endTime = `${endHour}:${startTime.slice(3) || '00'}`;
        state.meetings.push({
            id: `MTG-${item.id}`, title: item.title, start: startTime, end: endTime,
            location: 'Teams', owner: data.currentUser.name, date: date
        });
        const replacement = global.structuredClone
            ? global.structuredClone(item._fixture)
            : JSON.parse(JSON.stringify(item._fixture));
        replacement.reviewMeetingPolicy = {
            ...(replacement.reviewMeetingPolicy || { requirement: 'optional' }),
            meetingId: `MTG-${item.id}`,
            scheduledAt: `${date}T${startTime}:00+03:00`
        };
        if (replacement.reviewMeetingPolicy.requirement === 'required') {
            replacement.actions = replacement.actions.map((candidate) => candidate.code === 'signoff'
                ? { ...candidate, enabled: true, disabledReasonCode: undefined, disabledReason: undefined }
                : candidate);
            replacement.primaryActionCode = 'signoff';
            replacement.secondaryActionCodes = ['scheduleReviewMeeting'];
            replacement.overflowActionCodes = ['requestInfo', 'return'];
        }
        replacement.personal = { ...replacement.personal, seen: true };
        // Re-projecting must PRESERVE where the item came from. Letting it default silently re-stamped a showcase
        // fixture as a real item, which quietly turned off the curation that applies to fixtures alone.
        const projected = data.toPresentation(replacement, { provenance: item.provenance });
        const index = state.items.findIndex((candidate) => candidate.id === item.id);
        if (index >= 0) { state.items[index] = projected; }
        render();
        toast(tf('ToastReviewMeeting', `${date} ${startTime}`));
    };

    const openMeetingScheduler = (item, action) => {
        const label = actionLabel(action);
        if (!global.Swal) { applyReviewMeeting(item, `${item.dueAt || data.todayIso} 09:00`, label); return; }
        // Same journey as the plan dialog above, and for the same reason: one value, so one confirmation.
        const seed = item.dueAt || data.todayIso;
        sharedConfirm({
            title: label,
            // What booking it does and does NOT do — the due date is the question a reader actually has here.
            subtext: esc(t('MeetingWhenSubtext')),
            icon: inboxActionIcon(action),
            confirmText: t('PlanConfirm'),
            input: {
                type: 'text',
                label: t('MeetingWhenLabel'),
                placeholder: t('DateTimePlaceholder'),
                onOpen: (input) => {
                    if (!input) { return; }
                    input.classList.add('wcn-date-input');
                    if (global.flatpickr) {
                        global.flatpickr(input, { enableTime: true, dateFormat: 'Y-m-d H:i', defaultDate: seed, disableMobile: true });
                    } else {
                        input.type = 'datetime-local';
                    }
                },
                validate: (value) => (value ? null : t('PlanDateLabel'))
            },
            onConfirm: (value) => {
                if (value) { applyReviewMeeting(item, String(value).replace('T', ' '), label); }
            }
        });
    };

    // Log time — manual minutes entry into the timesheet (task only).
    const openLogTime = (item, action) => {
        const label = actionLabel(action);
        if (!global.Swal) { return; }
        sharedConfirm({
            title: label,
            /*
             * ITS OWN SENTENCE, saying what the box cannot: that this ADDS to what is already logged and does
             * not touch the running timer. The generic "are you sure?" it used to wear said nothing at all
             * above a field asking "how many minutes?".
             */
            subtext: esc(t('LogTimeSubtext')),
            icon: inboxActionIcon(action),
            confirmText: t('LogTimeConfirm'),
            input: {
                type: 'number',
                label: t('LogTimeLabel'),
                // Already a real example ("örn. 30"), so it was kept rather than replaced.
                placeholder: t('LogTimePlaceholder'),
                // The glyph is painted ON the box, exactly as the date field does it — no wrapper, nothing for
                // the library's slot walk to trip over.
                onOpen: (input) => { if (input) { input.classList.add('wcn-time-input'); } },
                validate: (value) => {
                    const m = parseInt(value, 10);
                    return (!m || m <= 0) ? t('LogTimeLabel') : null;
                }
            },
            onConfirm: (value) => {
                const mins = parseInt(value, 10);
                if (mins > 0) {
                    item.timesheet = item.timesheet || { running: false, startedAt: null, loggedMinutes: 0 };
                    item.timesheet.loggedMinutes += mins;
                    item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: label, atMs: data.referenceDate(item.provenance) });
                    render();
                    toast(tf('ToastTimeLogged', formatMinutes(mins)));
                }
            }
        });
    };

    /*
     * ── THE PERSONAL OVERLAY'S THREE WRITES (WC-1) ───────────────────────────────────────────────────────────
     *
     * All three were browser-only until 2026-08-14: a note was one assignment to a JavaScript object, a snooze
     * was another, and both said "kaydedildi" over the top. They now go to `/personal/*` on MOD-0024 and nothing
     * is applied optimistically — the server decides and the re-read projection is the only new state, exactly
     * like every transition on this page.
     *
     * A FIXTURE item still takes the browser-side path: no backend owns it, so posting would 404 and refusing
     * outright would break the showcase. The branch is explicit and warns, the same shape the checklist and
     * subtask writers use.
     */
    const addPersonalNote = async (taskId, text) => {
        const trimmed = String(text || '').trim();
        // Silence on empty — the placeholder already says what the box is for, and a toast for pressing Enter in
        // an empty field is noise. Same rule as the subtask and checklist add rows.
        if (!trimmed) { return; }

        const item = itemById(taskId);
        if (!isRealTaskItem(item)) {
            // The showcase keeps working, and it keeps working HONESTLY: the note is added to the fixture's own
            // list with a real instant, so it renders exactly as a stored one does.
            if (item) {
                item.notes = (item.notes || []).concat([{
                    id: `local-${item.notes ? item.notes.length : 0}-${trimmed.length}`,
                    text: trimmed,
                    createdAt: new Date(data.referenceDate(item.provenance)).toISOString()
                }]);
                render();
                toast(t('ToastNoteSaved'));
            }
            return;
        }

        const result = await global.TasksApi.addPersonalNote(taskId, { text: trimmed });
        // THE TOAST IS INSIDE the success branch. It used to fire unconditionally on a write that never happened
        // — that lie is the whole reason this round exists, and putting the message anywhere else brings it back.
        if (await afterPhase2Write(result, 'ToastNoteSaved')) {
            consumeEntryBox('data-wcn-note-input');
        }
    };

    const removePersonalNote = async (taskId, noteId) => {
        const item = itemById(taskId);
        if (!isRealTaskItem(item)) {
            if (item) {
                item.notes = (item.notes || []).filter((note) => String(note.id) !== String(noteId));
                render();
                toast(t('ToastNoteRemoved'));
            }
            return;
        }

        // No confirmation dialog, by decision: a private note is low-cost to lose and cheap to write again, and a
        // "are you sure" on every one of them would train the reader to dismiss dialogs that matter.
        await afterPhase2Write(await global.TasksApi.deletePersonalNote(taskId, noteId), 'ToastNoteRemoved');
    };

    // Snooze is a personal filter signal. It never changes the canonical
    // lifecycle, normalized status, tab or lifecycle segment — the executable contract says so outright
    // (SNOOZE_MUST_NOT_CREATE_WAITING) and the server honours it: the write touches the reader's own overlay
    // document and never the task.
    const toggleSnooze = async (item) => {
        if (!item) { return; }
        const real = isRealTaskItem(item);

        if (isSnoozed(item)) {
            if (real) {
                await afterPhase2Write(
                    await global.TasksApi.setSnooze(item.id, { snoozedUntil: null }),
                    'ToastUnsnoozed',
                    item.title);
                return;
            }
            item.snoozedUntil = null;
            item.personal.snoozedUntil = null;
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Unsnooze'), atMs: data.referenceDate(item.provenance) });
            render();
            toast(tf('ToastUnsnoozed', item.title));
            return;
        }
        const apply = async (dateStr) => {
            if (real) {
                /*
                 * The picker yields a DAY ("2026-08-20"); the server stores an instant. Sent as the END of that
                 * day in the reader's own zone, because "ertele: 20 Ağustos" means "leave me alone until the 20th
                 * is over" — sending midnight would wake the task at the start of the day the reader picked, and
                 * a same-day snooze would be refused as already past.
                 */
                const until = new Date(`${dateStr}T23:59:59`);
                await afterPhase2Write(
                    await global.TasksApi.setSnooze(item.id, { snoozedUntil: until.toISOString() }),
                    'ToastSnoozed',
                    item.title,
                    dateStr);
                return;
            }
            item.snoozedUntil = dateStr;
            item.personal.snoozedUntil = dateStr;
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Snooze'), atMs: data.referenceDate(item.provenance) });
            const prevOrder = state.visibleOrder.slice();
            const prevIdx = prevOrder.indexOf(item.id);
            if (state.view === 'split') { state.selectedId = prevOrder[prevIdx + 1] || prevOrder[prevIdx - 1] || null; }
            render();
            toast(tf('ToastSnoozed', item.title, dateStr));
        };
        if (!global.Swal) { await apply(data.todayIso); return; }
        /*
         * ── SNOOZE, THROUGH THE SHARED CONFIRM (2026-08-23) ─────────────────────────────────────────────────
         *
         * It was a raw `Swal.fire` with a bare title and no explanation — a date box that asked the reader to
         * commit to something without saying what it would do. Everything it lacked, the shared component
         * already offered; the ONE thing that kept it out was `input: 'textarea'` written as a constant, which
         * is now the parameter it should always have been.
         *
         * ⚠ WHAT THE SENTENCE MAY CLAIM WAS MEASURED, NOT ASSUMED. Three of its four clauses are enforced:
         * the lifecycle, the normalized status and the waiting context are untouched (the server's own
         * `SetTaskSnoozeHandler` writes only the reader's overlay), the due date is a different field entirely,
         * and the requester reads a projection this overlay never reaches. The fourth clause — "it disappears
         * from your inbox" — is NOT true today: nothing filters a snoozed item out of any list, on the server or
         * here. So the sentence does not say it. See the round's report and the backlog.
         *
         * ⚠ THE DISMISS BUTTON DOES NOT SAY "İptal". The wrapper's default is the shared `Cancel` string, and on
         * THIS page that word already belongs to an action — "Görevi iptal et", which calls the task off for
         * everyone. A dismiss control wearing the name of a destructive action is a misread waiting to happen.
         */
        sharedConfirm({
            title: t('SnoozeTitle'),
            subtext: esc(t('SnoozeSubtext')),
            /*
             * A MOON. The dialog's gravity is right as it stands — a plain primary confirmation — but the glyph
             * its type hands out is a question mark, which asks "are you sure?" while this dialog asks "until
             * when?". A moon says "later" in a way the title does not repeat.
             *
             * Only the picture is named here. The circle, its colour and the confirm button's colour still come
             * from `type`, untouched: measured at `rgb(105, 108, 255)` before and after.
             */
            icon: 'bx-moon',
            confirmText: t('SnoozeConfirm'),
            cancelText: t('DialogDismiss'),
            input: {
                // A TEXT box, not a native `date` one: the picker is flatpickr, the same component every other
                // date on this page uses. A native control here would be a second date language in one product.
                type: 'text',
                /*
                 * ⚠ THE LABEL IS BACK, AND THE PLACEHOLDER IS AN EXAMPLE (2026-08-24).
                 *
                 * It was the other way round: no label, and "Hangi tarihe kadar" used as the placeholder. That
                 * broke the rule this session set for every other box — a placeholder is a REAL EXAMPLE, never
                 * the field's own name repeated — and it broke it in the one dialog every other dialog was
                 * measured against. A reference that ignores its own rule is the fastest way to make the rule
                 * ignorable.
                 *
                 * Both halves are the ones the other date dialogs already use: `SnoozeUntilLabel` above the
                 * box, `DatePlaceholder` ("YYYY-AA-GG") inside it — the same pair as Planla. Nothing new was
                 * written, and `SnoozeDatePlaceholder` (an un-localised "YYYY-MM-DD" kept for this moment) is
                 * now unused; see the backlog.
                 */
                label: t('SnoozeUntilLabel'),
                placeholder: t('DatePlaceholder'),
                onOpen: (input) => {
                    if (!input) { return; }
                    /*
                     * ⚠ THE INPUT STAYS A DIRECT CHILD OF THE POPUP. Do not wrap it — measured 2026-08-23, and
                     * it cost a shipped defect: SweetAlert finds its input by walking the popup's own FIXED SLOT
                     * LIST (`.swal2-input`, `.swal2-file`, `.swal2-select`… in order), not by querying the
                     * subtree. A `.diten-field` wrapper made the box a GRANDCHILD, so `Swal.getInput()` returned
                     * null and three things broke at once: the validator read '' and answered "you cannot pick a
                     * past date" for a date in the FUTURE, the dialog never focused the field, and Enter did not
                     * confirm. `.diten-field` is right everywhere else in this product — it is wrong only INSIDE
                     * this popup, because this popup is a slot list rather than a free container.
                     *
                     * The calendar glyph is painted ON the box instead (`.wcn-date-input` in
                     * backbone-custom.css), from the very SVG the product's own `.bx-calendar` carries. No
                     * element is added, so there is nothing for the library to trip over — and the whole box
                     * opens the picker, so the glyph does not need to be a control of its own.
                     */
                    input.classList.add('wcn-date-input');

                    if (global.flatpickr) {
                        global.flatpickr(input, { dateFormat: 'Y-m-d', minDate: data.todayIso, disableMobile: true });
                    } else {
                        input.type = 'date';
                        input.min = data.todayIso;
                    }
                },
                /*
                 * TODAY IS ALLOWED (BL-182, owner decision 2026-08-23). The check used to reject it, while the
                 * calendar offered it — two halves of one field disagreeing. The server stores the snooze at
                 * 23:59:59 of the chosen day, so "snooze until today" means "leave me alone for the rest of
                 * today": a real request, and one the server accepts.
                 *
                 * The past is still refused here, and the server still refuses it too (400
                 * TASK_SNOOZE_DATE_INVALID). This does not replace that check — it arrives before it.
                 */
                validate: (value) => (!value || value < data.todayIso ? t('SnoozeFuture') : null)
            },
            onConfirm: (value) => { if (value) { apply(value).catch(reportSwalFailure); } }
        });
    };

    // ── "+ Yeni" — WorkCenter owns only self-tasks; module items are created in
    // their source (deep-link). No generic cross-module authoring here (spec v3). ─
    /*
     * ⚠ `openNew` WAS DELETED (2026-08-24, Tur B). MEASURED: the dispatch reached it only for a
     * `[data-wcn-new]` whose kind was not task/note/meeting/source, and every such element in the DOM carried
     * a known kind — the Bootstrap dropdown in the header replaced this Swal menu. No click could arrive.
     * It had just been given the product's dialog appearance, which is the most dangerous state for dead code:
     * it looked maintained.
     */

    /*
     * "+ Yeni ▸ Görev" opens the ONE quick-create surface (quick-create.js drives the offcanvas). It owns the
     * fields, validation and the POST; this module only reacts to `wcn:task-created`. Falls back to the detailed
     * form if the offcanvas is unavailable, so the action is never a dead end.
     */
    const openSelfTask = () => {
        if (global.WcnQuickCreate?.open()) { return; }

        // The fallback keeps the action usable, but it must NEVER be silent: dropping to the full page looks like
        // a deliberate design choice, so a broken offcanvas (script not loaded, JS error, markup missing) hides
        // itself and costs hours to diagnose. Say exactly which precondition failed.
        const reason = !global.WcnQuickCreate
            ? 'quick-create.js did not load (window.WcnQuickCreate is undefined)'
            : !document.getElementById('taskQuickCreate')
                ? 'the #taskQuickCreate markup is missing from the page'
                : !global.bootstrap?.Offcanvas
                    ? 'bootstrap.Offcanvas is unavailable'
                    : 'WcnQuickCreate.open() returned false';
        console.error(`[WorkCenterNext] Quick create unavailable — falling back to /Tasks/Create. Reason: ${reason}.`);

        global.location.href = '/Tasks/Create';
    };

    /*
     * MOD-0024 — a real task, created through the Task Engine; the new work item then arrives via the Task Center
     * projection (MOD-0024 is a registered work-item provider) rather than being pushed into local state.
     * Used by the agenda/notes "create a follow-up task" paths; the offcanvas posts through quick-create.js.
     * The fixture path in createSelfTask below is kept only for the Development-gated showcase catalog.
     */
    const createSelfTaskViaApi = async (v) => {
        const priorityMap = { high: 'High', medium: 'Medium', low: 'Low' };
        // Built by TaskForm so this path and the offcanvas cannot drift into two different payload shapes.
        const payload = global.TaskForm.buildCreatePayload({
            title: v.title,
            priority: priorityMap[v.priority] || 'Medium',
            assignmentTarget: 'SelfAssigned',
            dueAt: v.date || null
        });

        const result = await global.TasksApi.create(payload);
        if (!result.ok) {
            // 403 until the tasks permissions are granted; a due date is required by the engine.
            toast(result.status === 403 ? t('NoAccessTitle') : t('ErrorTitle'), 'error');
            return null;
        }

        await refreshAfterTaskCreated(v.title);
        return null;
    };

    /*
     * Re-reads the projection so a new task appears exactly as the Task Center sees it. Both callers
     * (createSelfTaskViaApi and the quick-create offcanvas's `wcn:task-created` event) are REAL engine writes —
     * neither exists for a showcase item — so this always uses the non-mock toast; 'ToastSelfTaskCreated' (which
     * says "(mock)" in all seven languages) stays reserved for createSelfTask's own fixture-only branch.
     */
    const refreshAfterTaskCreated = async (title) => {
        await loadWorkItems();
        state.tab = 'islerim';
        state.segment = 'aktif';
        state.view = 'list';
        render();
        toast(title ? tf('ToastSelfTaskCreatedReal', title) : tf('ToastSelfTaskCreatedReal', ''));
    };

    const createSelfTask = (v, origin) => {
        // Real engine unless the Development showcase catalog is driving the surface.
        if (!(data.showcaseFixturesEnabled && data.showcaseFixturesEnabled())) {
            return createSelfTaskViaApi(v);
        }

        const id = 'WC-SELF-' + (state.items.length + 1);
        const f = global.WorkCenterNextFixtureFactory;
        const fixture = f.base(id, 'task', 'TypeTask', {
            title: { kind: 'display', text: v.title, locale: global.CurrentLanguage || 'tr-TR', source: 'workcenter' },
            summary: f.resource('SelfTaskSummary'),
            source: f.source('workcenter', 'PersonalTask', id, { moduleId: 'MOD-0024', deepLink: `/WorkCenterNext/Details/${id}` }),
            nativeStatus: { code: 'OPEN', label: f.resource('LifecycleOpen') },
            taskLifecycle: 'Open',
            normalizedStatus: 'InProgress',
            workItemCapabilities: ['planning', 'execution', 'timeTracking', 'activity', 'businessContext', 'relatedRecords'],
            concurrency: { kind: 'version', token: '1' },
            actions: [f.action('plan'), f.action('start')],
            primaryActionCode: 'start',
            secondaryActionCodes: ['plan'],
            priority: v.priority,
            dueAt: v.date || null,
            requester: { id: data.currentUser.id, displayName: data.currentUser.name },
            assignee: { id: data.currentUser.id, displayName: data.currentUser.name },
            // 'fixture' directly, not item.provenance: this fixture object is built before toPresentation tags
            // it, and the branch above already fixes its provenance to 'fixture' unconditionally.
            activity: [{ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditSelfCreated', atMs: data.referenceDate('fixture') }]
        });
        // This branch only runs with the showcase catalogue on (see the guard at the top of createSelfTask), so
        // the item IS a fixture and says so rather than inheriting the 'api' default.
        const item = data.toPresentation(fixture, { provenance: 'fixture' });
        state.items.push(item);
        state.tab = 'islerim'; state.segment = 'aktif'; state.selectedId = id; state.view = 'list';
        state.agendaOpen = false; state.notesOpen = false;
        render();
        toast(tf('ToastSelfTaskCreated', v.title));
        return item;
    };

    const openCreateInSource = () => {
        const opts = {};
        moduleOptions().forEach((m) => { opts[m] = m; });
        sharedConfirm({
            title: t('NewInSource'),
            // Where the record will LIVE, which is the thing a module picker leaves unsaid.
            subtext: esc(t('NewInSourceSubtext')),
            icon: 'bx-cube',
            // The button names CREATING, not opening: nothing is opened here any more (see below), and the old
            // 'NewOpenSource' label promised an act this dialog no longer performs.
            confirmText: t('NewCreateInSource'),
            input: {
                type: 'select',
                options: opts,
                label: t('NewPickModuleLabel'),
                /*
                 * A SELECT'S PLACEHOLDER IS ITS EMPTY OPTION, not an example — there is nothing to exemplify,
                 * the choices are all listed. And its glyph is the THEME'S OWN caret, which `.swal2-select`
                 * already draws: adding a second one would put two arrows on one control.
                 */
                placeholder: t('NewPickModule'),
                // select2, from the same binder the filter panel uses — and parented INTO the popup so its
                // list cannot open behind the dialog the way flatpickr's calendar once did.
                onOpen: (box, popup) => { bindDialogSelect2(box, popup); }
            },
            onConfirm: (value) => {
                if (value) {
                /*
                 * Nothing is opened here, DELIBERATELY — do not "restore" this.
                 *
                 * It used to find an arbitrary EXISTING item from the chosen module and open that record's detail
                 * page. The user asked to CREATE something in that module; showing them an unrelated record they
                 * did not ask for is not a lesser version of creating one, it is the wrong act.
                 *
                 * Creating in another module needs that module's CREATE url, and the projection does not carry
                 * one (`deepLink` addresses an existing object). Until a provider supplies it, the honest
                 * behaviour is to say this is not wired yet and do nothing — which is exactly what the "(mock)"
                 * toast below already says.
                 */
                    toast(tf('ToastCreateInSource', value), 'info');
                }
            }
        });
    };

    /*
     * ⚠ `openMeetingForm` WAS DELETED (2026-08-24) — see the note on the "+ Yeni" menu above. It collected a
     * title, two times and a location, pushed them onto `state.meetings`, and lost all four on the next reload.
     * Its intent is recorded in the backlog as a DEFERRED feature rather than a removed one.
     */
    const createMeetingFollowup = (meeting) => {
        if (!meeting) { return; }
        createSelfTask({ title: tf('MeetingFollowupTitle', meeting.title), date: null, priority: 'Medium' }, { sourceModule: t('MeetingSource') });
    };

    const addGlobalNote = () => {
        const input = document.querySelector('#wcnApp [data-wcn-global-note-input]');
        const text = input && input.value.trim();
        if (!text) { return; }
        state.notes.unshift({ id: `NOTE-${Date.now()}`, text, ageKey: 'TimeToday', converted: false });
        render(); toast(t('NoteAdded'));
    };

    /*
     * ⚠ `openQuickNote` WAS DELETED (2026-08-24), same measurement and same reason as the meeting form: it
     * pushed onto `state.notes` and nothing else. Its `?` icon — the defect the owner photographed — is gone
     * with it rather than repainted.
     */
    const convertGlobalNote = (note) => {
        if (!note || note.converted) { return; }
        note.converted = true;
        createSelfTask({ title: note.text, date: null, priority: 'Medium' }, { sourceModule: t('NotesSource') });
    };

    const performAction = async (item, actionKey) => {
        const action = actionByKey(item, actionKey);
        if (!item || !action || action.disabled || state.submittingItemId === item.id) { return; }
        // The engine now stores the personal plan date (POST .../plan), so the picker opens for a real task too —
        // openDatePicker itself decides whether to write to the engine or, for a showcase item, only locally.
        if (action.input === 'date') { openDatePicker(item, action); return; }
        if (action.input === 'meeting') { openMeetingScheduler(item, action); return; }
        if (action.input === 'minutes') { openLogTime(item, action); return; }

        // Reason-capturing action (reject/return/inquire/dispute/delegate/reassign):
        // a mandatory-rationale textarea, which also serves as the confirm step.
        if (action.reason) {
            if (!global.Swal) { return; }

            /*
             * BL-043 — `reassign` also has to name the PERSON. The dialog used to ask only for a rationale, so
             * AssigneeUserId was never sent and the call was refused even once the field names were right.
             *
             * The picker offers the SAME list the create form uses (TasksApi.assignablePeople) because that is
             * the list the server validates against — offering anyone else would build a dialog whose confirm is
             * refused, which is the shape of defect this ticket exists to close.
             */
            const needsAssignee = ASSIGNEE_REQUIRED_ACTIONS.includes(action.code);
            /*
             * `inquire` may ALSO name a person, and must not require one — see WAITING_ON_ACTIONS. One fetch
             * serves both: the picker's list is the same list either way, because the server validates both
             * against the SAME eligibility rule (TaskAssigneeEligibility). Offering anyone else would build a
             * dialog whose confirm is refused, which is the defect shape this dialog already exists to close.
             */
            const offersWaitingOn = WAITING_ON_ACTIONS.includes(action.code);
            let people = [];
            if (needsAssignee || offersWaitingOn) {
                const res = await global.TasksApi.assignablePeople();
                // `data` IS the array — unwrapped once in TasksApi (BL-113). This line was wrong for three
                // rounds while each caller unwrapped the envelope in its own hand-written expression.
                people = res.ok ? res.data : [];
                if (!people.length && needsAssignee) {
                    // Refusing beats opening a dialog that cannot be confirmed.
                    toast(t('ReassignNoAssignableUsers'), 'error');
                    return;
                }
            }

            const options = people
                .map((person) => `<option value="${esc(personUserId(person))}">${esc(person.displayName || personUserId(person))}</option>`)
                .join('');
            const assigneeField = needsAssignee
                ? `<label class="form-label d-block text-start" for="wcnReassignAssignee">${esc(t('ReassignAssigneeLabel'))}</label>`
                  + `<select id="wcnReassignAssignee" class="form-select">`
                  + `<option value="">${esc(t('ReassignAssigneePlaceholder'))}</option>${options}</select>`
                : '';
            /*
             * The optional picker. Its empty option is not a placeholder to be replaced — it is a REAL CHOICE
             * ("nobody in particular"), so it says so in words rather than showing a greyed prompt that reads as
             * "you have not chosen yet". An empty list simply draws no field: nobody to name is not an error
             * here, unlike the required case above.
             */
            const waitingOnField = offersWaitingOn && people.length
                ? `<label class="form-label d-block text-start" for="wcnWaitingOn">${esc(t('WaitingOnLabel'))}</label>`
                  + `<select id="wcnWaitingOn" class="form-select">`
                  + `<option value="">${esc(t('WaitingOnNobody'))}</option>${options}</select>`
                : '';

            /*
             * ⚠ THIS IS THE DIALOG THE OWNER PHOTOGRAPHED. TWO fields (who is being waited on, and why), so it
             * cannot go through `showConfirm` — but every reason it looked wrong was appearance, not structure,
             * and appearance is now something it can ask for by name.
             */
            global.Swal.fire(Object.assign({
                /*
                 * ⚠ THE ICON RIDES THE TITLE HERE TOO (2026-08-24, option B). The shared confirm composes the
                 * two into one slot because the popup is a grid with one slot per row; a raw dialog that kept
                 * using the ICON SLOT would draw its circle in a row the stylesheet now collapses — measured
                 * exactly that: circle height 0 on the first pass of this change.
                 */
                title: dialogIcon('info', inboxActionIcon(action)) + '<span>' + esc(actionLabel(action)) + '</span>',
                /*
                 * ⚠ THE GLYPH COMES FROM `inboxActionIcon`, NOT FROM A HAND — CORRECTED 2026-08-24.
                 *
                 * The previous round picked `bx-conversation` by hand and gave the SAME picture to two
                 * different actions. The rail's own button already draws `bx-question-mark` for "Bilgi bekle"
                 * and `bx-user-pin` for "Yeniden ata", so one action was showing two icons: a pin on the
                 * button, a speech bubble in the dialog it opened.
                 *
                 * This product has ONE action→icon dictionary and it is `inboxActionIcon` (app.js:1324). A
                 * dialog opened BY an action asks it, exactly as the button did. An action the dictionary does
                 * not know gets added TO THE DICTIONARY, never worked around here.
                 *
                 * The CIRCLE and its colour are still `type`'s — `info`, untouched.
                 */
                html: `<div class="${dialogDescriptionClass()}">${outcomeLead(action)}</div>`
                    + assigneeField
                    + waitingOnField
                    + `<label class="form-label d-block text-start" for="wcnReasonText">${esc(t('ReasonLabel'))}</label>`
                    + `<textarea id="wcnReasonText" class="form-control" rows="3" `
                    + `placeholder="${esc(t('ReasonPlaceholder'))}"></textarea>`,
                showCancelButton: true,
                confirmButtonText: t('ReasonConfirm'),
                cancelButtonText: t('DialogDismiss'),
                // Both pickers become select2, through the same binder, parented into this popup.
                didOpen: (popup) => {
                    bindDialogSelect2(document.getElementById('wcnReassignAssignee'), popup);
                    bindDialogSelect2(document.getElementById('wcnWaitingOn'), popup);
                },
                preConfirm: () => {
                    const reason = String(document.getElementById('wcnReasonText')?.value || '').trim();
                    if (!reason) { global.Swal.showValidationMessage(t('ReasonRequired')); return false; }

                    if (!needsAssignee) {
                        // Empty is a real answer here, so it is passed through untouched and NOT validated.
                        const waitingOnUserId = String(document.getElementById('wcnWaitingOn')?.value || '').trim();
                        return { reason, waitingOnUserId };
                    }

                    const assigneeUserId = String(document.getElementById('wcnReassignAssignee')?.value || '').trim();
                    // Cannot be confirmed without a person: the server requires it and a silent 400 helps nobody.
                    if (!assigneeUserId) { global.Swal.showValidationMessage(t('ReassignAssigneeRequired')); return false; }
                    return { reason, assigneeUserId };
                }
            }, dialogLook())).then((res) => {
                if (res.isConfirmed && res.value) {
                    applyAction(item, action, res.value.reason, res.value.assigneeUserId, res.value.waitingOnUserId);
                }
            });
            return;
        }

        // High-consequence action (approve/sign-off/complete): explicit confirm so
        // an accidental click — or the `a` keyboard shortcut on a six-figure
        // approval — can't fire irreversibly (spec v2 §6, P1 fix).
        if (action.confirm) {
            if (!global.Swal) { return; }
            /*
             * The sentence no longer quotes the title: the badge below carries it, in the product's own framed
             * chip. Two places saying the same name is how one of them goes stale.
             */
            const body = item.delegator
                ? tf('ConfirmBodyOnBehalf', item.delegator)
                : t('ConfirmBody');
            /*
             * SIGNAL (b) FOR "REQUIRED" — said at the moment it matters, and at no cost to the flow.
             *
             * `complete` ALREADY opens this confirm (the projection marks it high-consequence), so naming the
             * open required items here interrupts nothing that was not already interrupted. That is what made
             * (b) worth doing alongside the card's counter rather than instead of it: the counter is for
             * somebody reading the task, this is for somebody closing it — and a person can close a task from
             * the LIST, where the card is not on screen at all.
             *
             * It is a WARNING, not a gate. The task still closes if they confirm; `Required` means "must be
             * done", not "must be done first", and turning it into a second blocker would erase the difference
             * between the two levels from the other direction.
             */
            const stillOpen = action.code === 'complete' ? openRequiredItems(item) : [];
            const requiredWarning = stillOpen.length
                ? `<div class="wcn-confirm-warning">${esc(tf('ConfirmRequiredOpen', stillOpen.length))}</div>`
                : '';
            sharedConfirm({
                title: actionLabel(action),
                /*
                 * The action's OUTCOME sentence leads the confirm — this is where `OutcomeCancel` ("cancels the
                 * task entirely; open subtasks are cancelled too") landed when destructive actions came out of
                 * the kebab and lost their card-side prose. A confirm that says only "are you sure?" asks the
                 * reader to remember what they are sure ABOUT.
                 */
                subtext: `${outcomeLead(action)}<div class="wcn-confirm-body">${esc(body)}</div>${requiredWarning}`,
                // The object of the action, in the wrapper's own badge — the one mechanism the product keeps.
                entityName: esc(item.title),
                // The wrapper picks the icon from the TYPE rather than taking one by name, so a destructive act
                // gets the danger circle and its red button from a single word instead of three settings.
                type: action.destructive ? 'danger' : 'info',
                /*
                 * ⚠ THE BUTTON NAMES THE ACT (2026-08-24, owner). It said "Evet, uygula" — a sentence that is
                 * true of every confirm in the product and therefore tells the reader nothing about the one in
                 * front of them. `ConfirmProceedNamed` is "Evet, {0}" and the argument is `actionLabel(action)`,
                 * i.e. the WORDS ON THE BUTTON THEY JUST PRESSED. No second name is derived; the dialog's
                 * title, the rail button and this button all read the same string.
                 */
                confirmText: tf('ConfirmProceedNamed', actionLabel(action).toLocaleLowerCase('tr')),
                onConfirm: () => applyAction(item, action)
            });
            return;
        }

        applyAction(item, action);
    };

    const executeTriggerAction = (trigger, action, reason) => {
        state.submittingTriggerId = trigger.id;
        state.submittingActionCode = action.code;
        render();
        global.setTimeout(() => {
            state.submittingTriggerId = null;
            state.submittingActionCode = null;
            if (trigger.responseBehavior === 'remove') {
                state.triggers = state.triggers.filter((candidate) => candidate.id !== trigger.id);
            }
            render();
            const label = data.resolveLabel(action.label);
            toast(reason ? tf('ToastActionReason', label, reason) : tf('ToastAction', label));
        }, 350);
    };

    const performTriggerAction = (trigger, action) => {
        if (!trigger || !action || action.enabled === false || state.submittingTriggerId === trigger.id) { return; }
        if (action.requiresReason) {
            sharedConfirm({
                title: data.resolveLabel(action.label),
                confirmText: t('ReasonConfirm'),
                // A TEXTAREA is the one input shape the shared wrapper offers, and it is the right one here.
                input: {
                    label: t('ReasonLabel'),
                    placeholder: t('ReasonPlaceholder'),
                    // The wrapper shows whatever string this returns and refuses; returning nothing accepts.
                    validate: (value) => (String(value || '').trim() ? null : t('ReasonRequired'))
                },
                onConfirm: (value) => {
                    const reason = String(value || '').trim();
                    if (reason) { executeTriggerAction(trigger, action, reason); }
                }
            });
            return;
        }
        executeTriggerAction(trigger, action);
    };

    // Bulk apply with a partial-failure model (spec v2 §6): some items fail (mock:
    // a stale/changed source record) — succeeded ones clear, failed ones stay
    // selected and flagged so the user can retry, never a silent all-or-nothing.

    // Brief progress pass before applying, so a large batch reads as work being
    // done rather than an instant, opaque state jump.


    // ── Keyboard (spec §4: j/k move · a accept · r reject · Enter open · Esc) ──
    const isTyping = (target) => target && /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName);

    const moveSelection = (delta) => {
        if (!state.visibleOrder.length) { return; }
        let idx = state.visibleOrder.indexOf(state.selectedId);
        idx = idx < 0 ? 0 : Math.min(state.visibleOrder.length - 1, Math.max(0, idx + delta));
        state.selectedId = state.visibleOrder[idx];
        if (state.view === 'list' || state.view === 'focus') {
            // In flat list, selection just highlights + scrolls; open with Enter.
            highlightRow();
        } else {
            render();
            focusSelectedRow();
        }
    };

    /*
     * The list's whole state — tab, segment, filters, search, page — lives in its URL (hydrateStateFromUrl reads
     * it back). Remember that URL on the way out so returning restores the list the user was actually looking at,
     * instead of dropping them on a default Inbox and making them rebuild their filters.
     */
    const LIST_RETURN_KEY = 'wcn:list-return-url';

    const rememberListUrl = () => {
        try {
            global.sessionStorage?.setItem(LIST_RETURN_KEY, global.location.pathname + global.location.search);
        } catch (error) { /* private mode / storage disabled — the default link still works */ }
    };

    const listReturnUrl = () => {
        try {
            const stored = global.sessionStorage?.getItem(LIST_RETURN_KEY);
            // Same-origin path only: a stored value is user-controlled input, not a destination to trust.
            if (stored && stored.startsWith('/WorkCenterNext')) { return stored; }
        } catch (error) { /* fall through */ }
        return '/WorkCenterNext';
    };

    const openDetailPage = (id) => {
        if (!id) { return; }
        rememberListUrl();
        global.location.assign(`/WorkCenterNext/Details/${encodeURIComponent(id)}`);
    };

    const highlightRow = () => {
        document.querySelectorAll('#wcnApp .wcn-row.selected').forEach((n) => n.classList.remove('selected'));
        const node = document.querySelector(`#wcnApp .wcn-row[data-wcn-row="${state.selectedId}"]`);
        if (node) { node.classList.add('selected'); node.scrollIntoView({ block: 'nearest' }); }
    };

    const focusSelectedRow = () => {
        const node = document.querySelector(`#wcnApp [data-wcn-row="${state.selectedId}"]`);
        if (node) { node.scrollIntoView({ block: 'nearest' }); if (typeof node.focus === 'function') { node.focus(); } }
    };

    // ASYNC: posting a comment on Enter is awaited, so a failed post cannot swallow its own rejection and look
    // like a key that was never wired — the exact shape the subtask writer shipped broken in.
    const onKeydown = async (event) => {
        /*
         * ENTER ADDS THE SUBTASK — before the typing guard, because this fires INSIDE a text field.
         *
         * The parent id rides on the input itself, so the row has exactly one control and there is no button
         * that could point at a different parent. AWAITED through the same addSubtask the button used: an
         * un-awaited async call rejects into nothing, which is precisely how this action once failed in silence.
         */
        if (event.key === 'Enter' && event.target.matches && event.target.matches('[data-wcn-subtask-input]')) {
            event.preventDefault();
            const parentId = event.target.getAttribute('data-wcn-subtask-add');
            const text = event.target.value;
            if (parentId) { addSubtask(parentId, text); }
            return;
        }
        // The checklist's add row, on the same terms as the subtask row above it: the task id rides on the
        // input, Enter commits, and the call is awaited through the same path the level chip does not touch.
        /*
         * THE COMMENT BOX takes Enter too — the one repeated-entry field on this page that never did.
         *
         * The subtask row and the checklist row both commit on Enter; this one only had its button, so the same
         * key did nothing in the box directly beneath them. It is an <input> and not a textarea precisely so
         * that Enter can mean "post" without ambiguity, and then nothing was listening for it.
         *
         * The post button stays: a visible control and a key are the pair, not alternatives.
         */
        if (event.key === 'Enter' && event.target.matches && event.target.matches('[data-wcn-comment-input]')) {
            event.preventDefault();
            const post = document.querySelector('#wcnApp [data-wcn-comment-post]');
            if (post) { await postComment(post.getAttribute('data-wcn-comment-post'), event.target.value); }
            return;
        }
        if (event.key === 'Enter' && event.target.matches && event.target.matches('[data-diten-check-input]')) {
            event.preventDefault();
            const taskId = event.target.getAttribute('data-diten-check-input');
            const text = event.target.value;
            if (taskId) { addChecklistItem(taskId, text); }
            return;
        }
        /*
         * ENTER ADDS THE PERSONAL NOTE, on the same terms as the two rows above: the task id rides on the input,
         * so the key and the button can never point at different tasks, and the call is AWAITED.
         *
         * The hint line under the box is what says so — the placeholder disappears the moment you start typing,
         * and a key nobody documented is a key nobody presses.
         */
        /*
         * THE SUMMARY'S PLAN ROW IS A `role="button"`, so it owes the keyboard what a real button gets for free:
         * Enter AND Space. Without this it is reachable by Tab, announced as a button, and does nothing when
         * pressed — which is worse than not being focusable at all.
         *
         * It routes through the SAME `performAction` the click path uses; there is one mechanism, and this is
         * the second way to reach it, not a second copy of it.
         */
        const fieldButton = event.target.closest && event.target.closest('[data-wcn-action][role="button"]');
        if (fieldButton && (event.key === 'Enter' || event.key === ' ')) {
            event.preventDefault();
            performAction(itemById(fieldButton.getAttribute('data-wcn-id')),
                fieldButton.getAttribute('data-wcn-action'));
            return;
        }
        if (event.key === 'Enter' && event.target.matches && event.target.matches('[data-wcn-note-add]')) {
            event.preventDefault();
            await addPersonalNote(event.target.getAttribute('data-wcn-note-add'), event.target.value);
            return;
        }
        // Escape in the search box clears the current query (before the typing guard).
        /*
         * THE TWO PANEL TITLE FIELDS — the only repeated-entry inputs on this page that did NOT take Enter.
         *
         * MEASURED: `#wcnSubtaskTitle` (quick edit) and `#wcnNewSubtaskTitle` (detailed create) listened for
         * `input` only, and neither panel is a `<form>`, so pressing Enter did nothing at all — no save, no
         * error, no focus move. Meanwhile the subtask add row, the comment box and the checklist add row all
         * commit on Enter. Three inputs teaching a habit and two silently refusing it is worse than none of them
         * having it: the reader has already learned that Enter works here.
         *
         * The detailed panel is the sharper case — it is the one with a REQUIRED field, i.e. the one where the
         * user is most likely to type and press Enter expecting a commit or a validation message.
         *
         * Routed through the SAME save the button uses, so validation, the busy flag and the failure path are
         * one implementation rather than a keyboard copy of them.
         */
        if (event.key === 'Enter' && event.target.matches && event.target.matches('#wcnSubtaskTitle')) {
            event.preventDefault();
            const save = document.querySelector('[data-wcn-subtask-save]');
            if (save && !save.disabled) { await saveSubtaskPanel(save.getAttribute('data-wcn-subtask-save')); }
            return;
        }
        if (event.key === 'Enter' && event.target.matches && event.target.matches('#wcnNewSubtaskTitle')) {
            event.preventDefault();
            const save = document.querySelector('[data-wcn-newsubtask-save]');
            if (save && !save.disabled) { await saveNewSubtask(save.getAttribute('data-wcn-newsubtask-save')); }
            return;
        }

        if (event.key === 'Escape' && event.target.matches && event.target.matches('[data-wcn-search]')) {
            if (state.search) { event.preventDefault(); state.search = ''; render(); }
            return;
        }
        if (isTyping(event.target) || event.metaKey || event.ctrlKey || event.altKey) { return; }
        const key = event.key.toLowerCase();
        /*
         * Arrow / Home / End across a tab strip.
         *
         * Widened from `[data-wcn-tab]` (the list page's ownership strip) to ANY `[role=tab]`, and scoped to the
         * strip the focused tab actually lives in. Two reasons: the detail page's new strip gets the same
         * keyboard behaviour for free rather than a second copy of it, and the old global query would have
         * walked between two strips as if they were one list the moment a page carried both.
         */
        const activeTab = event.target.closest && event.target.closest('[role="tab"]');
        if (activeTab && (key === 'arrowleft' || key === 'arrowright' || key === 'home' || key === 'end')) {
            const strip = activeTab.closest('[role="tablist"]') || document.getElementById('wcnApp');
            const tabs = Array.from(strip.querySelectorAll('[role="tab"]'));
            let index = tabs.indexOf(activeTab);
            index = key === 'home' ? 0 : key === 'end' ? tabs.length - 1
                : (index + (key === 'arrowright' ? 1 : -1) + tabs.length) % tabs.length;
            event.preventDefault(); tabs[index].focus(); tabs[index].click(); return;
        }
        if (key === 'j') { event.preventDefault(); moveSelection(1); return; }
        if (key === 'k') { event.preventDefault(); moveSelection(-1); return; }
        if (key === 'escape') { state.selectedId = null; render(); return; }

        const focusedRow = event.target.closest && event.target.closest('[data-wcn-row]');
        if (focusedRow && !event.target.closest('button,input,a') && (key === 'enter' || key === ' ')) {
            event.preventDefault();
            state.selectedId = focusedRow.getAttribute('data-wcn-row');
            const focusedItem = itemById(state.selectedId);
            if (focusedItem) { markSeen(focusedItem); }
            openDetailPage(state.selectedId); return;
        }

        const item = itemById(state.selectedId);
        if (!item) { return; }
        if (key === 'enter' || key === 'o') {
            event.preventDefault();
            openDetailPage(state.selectedId);
            return;
        }
        if (key === 'a') { const a = actionByRole(item, 'accept'); if (a) { event.preventDefault(); performAction(item, a.key); } return; }
        if (key === 'r') { const r = actionByRole(item, 'reject'); if (r) { event.preventDefault(); performAction(item, r.key); } return; }
    };

    // ── Event delegation ──────────────────────────────────────────────────────
    const onClick = async (event) => {
        const root = event.target.closest('#wcnApp');
        if (!root && !event.target.closest('.wcn-bulkbar')) { /* still allow bulkbar inside app */ }

        const toggleEl = event.target.closest('[data-wcn-toggle]');
        if (toggleEl) {
            const panel = toggleEl.getAttribute('data-wcn-toggle');
            if (panel === 'agenda') { state.agendaOpen = !state.agendaOpen; state.notesOpen = false; }
            if (panel === 'notes') { state.notesOpen = !state.notesOpen; state.agendaOpen = false; }
            render();
            return;
        }

        const jumpEl = event.target.closest('[data-wcn-jump]');
        if (jumpEl) {
            const jumpId = jumpEl.getAttribute('data-wcn-jump');
            const targetItem = state.items.find((i) => i.id === jumpId);
            if (targetItem) {
                state.tab = isTerminal(targetItem) ? 'history' : targetItem.tab;
                state.selectedId = jumpId;
                render();
            }
            return;
        }

        const tabEl = event.target.closest('[data-wcn-tab]');
        if (tabEl) {
            state.viewsByTab[state.tab] = state.view;
            state.tab = tabEl.getAttribute('data-wcn-tab');
            state.segment = (SEGMENTS[state.tab] || ['aktif'])[0];   // reset to first segment
            const allowed = TAB_VIEWS[state.tab] || TAB_VIEWS.islerim;
            state.view = state.viewsByTab[state.tab] || allowed[0];
            if (allowed.indexOf(state.view) === -1) { state.view = allowed[0]; }   // view not valid for this tab
            state.group = 'all';
            state.listPage = 0;
            state.selectedId = null; state.tableSelected.clear(); state.bulkFailedIds.clear();
            render(); return;
        }

        if (event.target.closest('[data-wcn-retry]')) {
            loadWorkItems();   // WC-1b — re-issue the real request instead of faking success
            return;
        }

        const meetingFollowupEl = event.target.closest('[data-wcn-meeting-followup]');
        if (meetingFollowupEl) {
            createMeetingFollowup(state.meetings.find((m) => m.id === meetingFollowupEl.getAttribute('data-wcn-meeting-followup')));
            return;
        }
        if (event.target.closest('[data-wcn-global-note-add]')) { addGlobalNote(); return; }
        const noteConvertEl = event.target.closest('[data-wcn-note-convert]');
        if (noteConvertEl) {
            convertGlobalNote(state.notes.find((note) => note.id === noteConvertEl.getAttribute('data-wcn-note-convert')));
            return;
        }

        const segEl = event.target.closest('[data-wcn-seg]');
        if (segEl) { state.segment = segEl.getAttribute('data-wcn-seg'); state.selectedId = null; render(); return; }

        if (event.target.closest('[data-wcn-group-unnamed]')) {
            state.group = GROUP_UNNAMED; state.selectedId = null; render(); return;
        }
        const groupEl = event.target.closest('[data-wcn-group]');
        if (groupEl) { state.group = groupEl.getAttribute('data-wcn-group'); state.selectedId = null; render(); return; }

        // Inbox chips — "Tümü" clears types; main type chips are single-select.
        if (event.target.closest('[data-wcn-inbox-all]')) { state.typeFilter.clear(); state.selectedId = null; render(); return; }
        const inboxTypeEl = event.target.closest('[data-wcn-inbox-type]');
        if (inboxTypeEl) {
            const ty = inboxTypeEl.getAttribute('data-wcn-inbox-type');
            const only = state.typeFilter.size === 1 && state.typeFilter.has(ty);
            state.typeFilter = only ? new Set() : new Set([ty]);   // toggle-off → Tümü
            state.selectedId = null;
            render(); return;
        }
        // Type / signal filter chips — multi-select toggle (İşlerim/Havuz/Geçmiş).
        const typeChipEl = event.target.closest('[data-wcn-typechip]');
        if (typeChipEl) {
            const ty = typeChipEl.getAttribute('data-wcn-typechip');
            if (state.typeFilter.has(ty)) { state.typeFilter.delete(ty); } else { state.typeFilter.add(ty); }
            render(); return;
        }
        const sigChipEl = event.target.closest('[data-wcn-sigchip]');
        if (sigChipEl) {
            const sig = sigChipEl.getAttribute('data-wcn-sigchip');
            if (state.signalFilter.has(sig)) { state.signalFilter.delete(sig); } else { state.signalFilter.add(sig); }
            render(); return;
        }
        if (event.target.closest('[data-wcn-chip-clear]')) { state.typeFilter.clear(); state.signalFilter.clear(); render(); return; }
        if (event.target.closest('[data-wcn-search-clear]')) { state.search = ''; render(); return; }
        const listPageEl = event.target.closest('[data-wcn-list-page]');
        if (listPageEl) {
            state.listPage = Math.max(0, state.listPage + (listPageEl.getAttribute('data-wcn-list-page') === 'next' ? 1 : -1));
            render(); return;
        }
        const filterToggle = event.target.closest('[data-wcn-filter-toggle]');
        if (filterToggle) {
            const panel = document.getElementById('wcnFilterCollapse');
            if (!panel) { return; }
            state.filtersOpen = !panel.classList.contains('show');
            filterToggle.setAttribute('aria-expanded', String(state.filtersOpen));
            if (state.filtersOpen) { mountPanelSelect2(); }
            if (global.bootstrap?.Collapse) {
                global.bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false }).toggle();
            } else {
                panel.classList.toggle('show', state.filtersOpen);
            }
            return;
        }
        if (event.target.closest('[data-wcn-filter-reset]')) {
            state.moduleFilter = []; state.priorityFilter = 'all'; state.modeFilter = 'all';
            state.typeFilter.clear(); state.signalFilter.clear(); state.search = '';
            state.sortKey = 'sla'; state.sortDir = 'asc';
            state.tableColumnVisibility = [true, true, true, true, true, true, true, true];
            state.listPage = 0;
            render();
            return;
        }
        const newEl = event.target.closest('[data-wcn-new]');
        if (newEl) {
            const kind = newEl.getAttribute('data-wcn-new');
            if (kind === 'task') { openSelfTask(); }
            else if (kind === 'source') { openCreateInSource(); }
            return;
        }

        const viewEl = event.target.closest('[data-wcn-view]');
        if (viewEl) {
            state.view = viewEl.getAttribute('data-wcn-view');
            state.viewsByTab[state.tab] = state.view;
            render(); return;
        }

        const scopeEl = event.target.closest('[data-wcn-scope]');
        if (scopeEl) {
            if (scopeEl.hasAttribute('disabled')) { return; }   // no team ⇒ the row explains itself, nothing more
            const previous = state.scope;
            state.scope = scopeEl.getAttribute('data-wcn-scope');
            state.selectedId = null;
            state.tableSelected.clear();
            state.bulkFailedIds.clear();
            /*
             * BL-023 — the delegation scopes filter rows the browser already holds; the TEAM scope is a
             * DIFFERENT QUERY (my subordinates' work is never in the personal projection). So crossing that
             * boundary in either direction has to go back to the server, or the list silently keeps showing
             * whichever set was fetched first.
             */
            if ((previous === 'team') !== (state.scope === 'team')) {
                loadWorkItems();
                return;
            }
            render();
            return;
        }

        const actionEl = event.target.closest('[data-wcn-action]');
        if (actionEl) {
            event.stopPropagation();
            performAction(itemById(actionEl.getAttribute('data-wcn-id')), actionEl.getAttribute('data-wcn-action'));
            return;
        }

        const pinEl = event.target.closest('[data-wcn-pin]');
        if (pinEl) {
            event.stopPropagation();
            const item = itemById(pinEl.getAttribute('data-wcn-pin'));
            if (item) { item.pinned = !item.pinned; render(); toast(tf(item.pinned ? 'ToastPinned' : 'ToastUnpinned', item.title)); }
            return;
        }

        const snoozeEl = event.target.closest('[data-wcn-snooze]');
        if (snoozeEl) {
            event.stopPropagation();
            // AWAITED now that the write is real: an un-awaited rejection here would make a failed snooze look
            // like a button nobody wired.
            await toggleSnooze(itemById(snoozeEl.getAttribute('data-wcn-snooze')));
            return;
        }

        // ── Depth-block interactions (Faz 2) ──────────────────────────────────
        // All three go to the ENGINE and then re-read the projection. Nothing is applied optimistically: the
        // server decides, and the refreshed projection is the only source of the new state.
        /*
         * The checklist row's controls, all five of them, on the SHARED attributes the create form also uses.
         * The row is one component now (assets/js/shared/diten-checkitem.js); a second vocabulary here would put
         * the two screens straight back on the divergent path this round exists to end.
         *
         * `id` is `taskId:itemCode` because a detail page can show a parent's list and a subtask panel at once,
         * and an item code alone would not say which task's list a click landed in.
         */
        const checkAddEl = event.target.closest('[data-diten-check-add]');
        if (checkAddEl) {
            // The button and Enter are the same call. It exists because the instruction for Enter lived in the
            // placeholder, which disappears the moment somebody starts typing — and on a touch keyboard Enter
            // is not always to hand.
            const taskId = checkAddEl.getAttribute('data-diten-check-add');
            const input = document.querySelector(`[data-diten-check-input="${taskId}"]`);
            if (input) { await addChecklistItem(taskId, input.value); }
            return;
        }
        const checkToggleEl = event.target.closest('[data-diten-check-toggle]');
        if (checkToggleEl) {
            const [taskId, itemCode] = checkToggleEl.getAttribute('data-diten-check-toggle').split(':');
            toggleChecklistItem(taskId, itemCode, checkToggleEl.getAttribute('aria-pressed') !== 'true');
            return;
        }
        const checkLevelRowEl = event.target.closest('[data-diten-check-level]');
        if (checkLevelRowEl && checkLevelRowEl.getAttribute('data-diten-check-level').includes(':')) {
            const [taskId, itemCode] = checkLevelRowEl.getAttribute('data-diten-check-level').split(':');
            const current = checklistItemOf(taskId, itemCode);
            // Weakest-first, so a reader who keeps pressing walks TOWARD the strict end rather than starting
            // there. Same order and same three values the create form cycles through.
            const order = ['Optional', 'Required', 'Blocking'];
            const now = current?.requirement || 'Optional';
            await updateChecklistItem(taskId, itemCode,
                { requirement: order[(order.indexOf(now) + 1) % order.length] });
            return;
        }
        const checkEvidenceEl = event.target.closest('[data-diten-check-evidence]');
        if (checkEvidenceEl && checkEvidenceEl.getAttribute('data-diten-check-evidence').includes(':')) {
            const [taskId, itemCode] = checkEvidenceEl.getAttribute('data-diten-check-evidence').split(':');
            await updateChecklistItem(taskId, itemCode,
                { evidenceRequired: checkEvidenceEl.getAttribute('aria-pressed') !== 'true' });
            return;
        }
        const checkRemoveEl = event.target.closest('[data-diten-check-remove]');
        if (checkRemoveEl && checkRemoveEl.getAttribute('data-diten-check-remove').includes(':')) {
            const [taskId, itemCode] = checkRemoveEl.getAttribute('data-diten-check-remove').split(':');
            await removeChecklistItem(taskId, itemCode);
            return;
        }
        const checkMoveEl = event.target.closest('[data-diten-check-move]');
        if (checkMoveEl && checkMoveEl.closest('.wcn-checks')) {
            const rowEl = checkMoveEl.closest('[data-diten-check-row]');
            const [taskId, itemCode] = rowEl.getAttribute('data-diten-check-row').split(':');
            await moveChecklistItem(taskId, itemCode, checkMoveEl.getAttribute('data-diten-check-move'));
            return;
        }
        // Opening a subtask's own detail. Checked BEFORE the toggle so the two never compete for the same click;
        // they are distinct controls inside the same row.
        // The quick panel first; "full detail" inside it is the deliberate way out to the whole page.
        const openTaskFullEl = event.target.closest('[data-wcn-open-task-full]');
        if (openTaskFullEl) {
            openDetailPage(openTaskFullEl.getAttribute('data-wcn-open-task-full'));
            return;
        }
        const newSubtaskFullEl = event.target.closest('[data-wcn-newsubtask-full]');
        if (newSubtaskFullEl) {
            /*
             * ⚠ THE RETURN URL IS THIS PAGE, and it is built here rather than taken from anywhere: the server
             * still puts it through `Url.IsLocalUrl`, so a hand-crafted link cannot turn this into an open
             * redirect — but sending a value we did not construct would be asking the gate to save us.
             */
            const parentId = newSubtaskFullEl.getAttribute('data-wcn-newsubtask-full');
            const back = `/WorkCenterNext/Details/${encodeURIComponent(parentId)}`;
            global.location.href = `/Tasks/Create?parent=${encodeURIComponent(parentId)}`
                + `&returnUrl=${encodeURIComponent(back)}`;
            return;
        }
        const subtaskSaveEl = event.target.closest('[data-wcn-subtask-save]');
        if (subtaskSaveEl) {
            await saveSubtaskPanel(subtaskSaveEl.getAttribute('data-wcn-subtask-save'));
            return;
        }
        const openTaskEl = event.target.closest('[data-wcn-open-task]');
        if (openTaskEl) {
            // A row opens the QUICK panel now; the full page is one click further, from inside it.
            await openSubtaskPanel(itemById(state.selectedId), openTaskEl.getAttribute('data-wcn-open-task'));
            return;
        }
        const subEl = event.target.closest('[data-wcn-subtask]');
        if (subEl) {
            const [, subtaskId] = subEl.getAttribute('data-wcn-subtask').split(':');
            // A subtask is a FULL task, so "tick it" means completing that task through the same endpoint any
            // other task uses — there is no separate half-lifecycle for children.
            completeSubtask(subtaskId);
            return;
        }
        /*
         * THE HANDLER THAT DID NOT EXIST. Everything else about the cap worked — the wrapper, the CSS, the
         * count in the label — and this one branch was missing, so the button was decoration.
         */
        const showAllEl = event.target.closest('[data-wcn-showall]');
        if (showAllEl) {
            const key = showAllEl.getAttribute('data-wcn-showall');
            if (key) { state.expandedLists[key] = !state.expandedLists[key]; render(); }
            return;
        }
        /*
         * THE BLOCKED BANNER'S LINK — and the reason it is a handler and not a href.
         *
         * The Subtasks card lives inside the GENERAL tab. A reader looking at the banner may be on the Activity
         * tab, where the card is in the DOM but hidden, so a plain `#wcn-subtasks-card` anchor would scroll to
         * something invisible and appear to do nothing. Switching tab first is the difference between a link
         * that works and a link that looks broken.
         *
         * ⚠ This session has twice shipped a control with NO handler at all ("Tümünü gör", in two places), which
         * is why the link's behaviour is asserted by a test rather than trusted.
         *
         * Focus moves with the scroll, not just the viewport: a keyboard reader who activates a link and keeps
         * tabbing must continue from where they were sent, otherwise the next Tab returns them to the banner.
         * The card carries `tabindex="-1"` so it can receive that focus without becoming a tab stop of its own.
         */
        const gotoSubtasksEl = event.target.closest('[data-wcn-goto-subtasks]');
        if (gotoSubtasksEl) {
            if (state.detailTab !== 'general') {
                state.detailTab = 'general';
                render();
            }
            const card = document.getElementById('wcn-subtasks-card');
            if (card) {
                /*
                 * TWO MECHANISMS, ONE OUTCOME — and deliberately not `preventScroll`.
                 *
                 * `scrollIntoView` gives the smooth motion that keeps a reader oriented. But focusing a
                 * `tabindex="-1"` element ALSO scrolls it into view, by the browser's own rules, and that path
                 * survives environments where scripted scrolling is refused — measured in this very session:
                 * `scrollingElement.scrollTop = n` reads back unchanged under the headless pane while real
                 * input scrolls fine. Suppressing the focus scroll would have staked the whole behaviour on the
                 * one call that can be ignored.
                 *
                 * They target the same element, so the redundancy costs a no-op and buys the guarantee.
                 */
                card.scrollIntoView({ block: 'start', behavior: 'smooth' });
                card.focus();
            }
            return;
        }
        const detailTabEl = event.target.closest('[data-wcn-detail-tab]');
        if (detailTabEl) {
            /*
             * Its OWN attribute, not `data-wcn-tab`. That one is the list page's ownership strip and carries a
             * click handler of its own; sharing the attribute would make a detail tab try to switch the inbox.
             */
            /*
             * ⚠ NO render() HERE, and that is the whole point.
             *
             * Switching tab changes NOTHING about the data — only which panel is visible. Re-rendering to flip a
             * class rebuilds every node in the column, and a rebuilt <input> is an empty one: measured live, a
             * half-typed comment vanished the moment the reader glanced at "Genel" and came back. (Focus restore
             * does not save it either — that only rescues the field that HAS focus, and focus is on the tab.)
             *
             * The checklist toggles taught this same lesson in an earlier round: do not rebuild the world to
             * change how it looks. Both panels stay mounted; only `d-none` and the tab's own state move.
             */
            state.detailTab = detailTabEl.getAttribute('data-wcn-detail-tab');

            document.querySelectorAll('[data-wcn-detail-panel]').forEach((panel) => {
                panel.classList.toggle('d-none',
                    panel.getAttribute('data-wcn-detail-panel') !== state.detailTab);
            });
            document.querySelectorAll('[data-wcn-detail-tab]').forEach((tab) => {
                const selected = tab.getAttribute('data-wcn-detail-tab') === state.detailTab;
                tab.classList.toggle('active', selected);
                tab.setAttribute('aria-selected', selected ? 'true' : 'false');
                // The roving tab stop moves WITH the selection. Set only at render time, the strip would keep
                // its original tab stop after a switch, and Tab would land on the tab that is no longer current.
                tab.setAttribute('tabindex', selected ? '0' : '-1');
            });

            /*
             * ── BL-087: THE CHOICE GOES INTO THE ADDRESS ────────────────────────────────────────────────────
             *
             * `#etkinlik` already worked on the way IN and not on the way OUT, so a reader sitting on Etkinlik
             * who copied the link sent the other person to Genel. The address is the thing people share; it has
             * to say what they are looking at.
             *
             * `replaceState`, NOT `pushState`, and that is a decision rather than a detail: on a two-tab page
             * the Back button's job is to return to the LIST, not to walk backwards through tab clicks. A push
             * per click would bury the way out under however many times somebody glanced at the other tab.
             *
             * NO PERSISTENCE — no storage, no cookie, nothing remembered. The address is visible and erasable;
             * hidden memory that reopens a page differently for two people following one link is not.
             */
            writeDetailTabToAddress();
            return;
        }
        const checkLevelEl = event.target.closest('[data-diten-check-draftlevel]');
        if (checkLevelEl) {
            // Weakest-first, so a reader who keeps pressing walks toward the strict end rather than starting
            // there. Same order and same three values the create form cycles through.
            const order = ['Optional', 'Required', 'Blocking'];
            state.checklistDraftLevel = order[(order.indexOf(state.checklistDraftLevel) + 1) % order.length];
            render();
            return;
        }
        const activityFilterEl = event.target.closest('[data-wcn-activity-filter]');
        if (activityFilterEl) {
            state.activityFilter = activityFilterEl.getAttribute('data-wcn-activity-filter');
            // The cap is released per list and the filter changes what that list CONTAINS, so an expansion made
            // over forty entries must not carry into a view of four.
            state.expandedLists.activity = false;
            render();
            return;
        }
        const subAddDetailedEl = event.target.closest('[data-wcn-subtask-add-detailed]');
        if (subAddDetailedEl) {
            await openSubtaskCreatePanel(subAddDetailedEl.getAttribute('data-wcn-subtask-add-detailed'));
            return;
        }
        const newSubSaveEl = event.target.closest('[data-wcn-newsubtask-save]');
        if (newSubSaveEl) {
            await saveNewSubtask(newSubSaveEl.getAttribute('data-wcn-newsubtask-save'));
            return;
        }
        const subCancelEl = event.target.closest('[data-wcn-subtask-cancel]');
        if (subCancelEl) {
            await cancelSubtask(
                subCancelEl.getAttribute('data-wcn-subtask-cancel'),
                subCancelEl.getAttribute('data-wcn-subtask-title'));
            return;
        }
        /*
         * The quick-add BUTTON is gone: `data-wcn-subtask-add` now lives on the input itself and Enter submits
         * (onKeydown). A click path is kept for anything that still carries the attribute on a button — the
         * subtask create panel does — so both surfaces reach the one write path.
         */
        const subAddEl = event.target.closest('button[data-wcn-subtask-add]');
        if (subAddEl) {
            const card = subAddEl.closest('.wcn-subtask-add') || subAddEl.closest('.wcn-detail-card');
            const input = (card || document).querySelector('[data-wcn-subtask-input]');
            // AWAITED on purpose: an un-awaited async call rejects into nothing, and that is precisely how this
            // action failed in total silence when the host page had not loaded TasksApi.
            await addSubtask(subAddEl.getAttribute('data-wcn-subtask-add'), input ? input.value : '');
            return;
        }
        const noteSaveEl = event.target.closest('[data-wcn-note-save]');
        if (noteSaveEl) {
            const inp = document.querySelector('#wcnApp [data-wcn-note-input]');
            // AWAITED, like every other write on this page: an un-awaited promise swallows its own rejection, and
            // a failed write then looks exactly like a button that was never wired.
            await addPersonalNote(noteSaveEl.getAttribute('data-wcn-note-save'), inp && inp.value);
            return;
        }
        const noteRemoveEl = event.target.closest('[data-wcn-note-remove]');
        if (noteRemoveEl) {
            await removePersonalNote(
                noteRemoveEl.getAttribute('data-wcn-note-task'),
                noteRemoveEl.getAttribute('data-wcn-note-remove'));
            return;
        }
        const commentEditEl = event.target.closest('[data-wcn-comment-edit]');
        if (commentEditEl) {
            await editComment(
                commentEditEl.getAttribute('data-wcn-comment-task'),
                commentEditEl.getAttribute('data-wcn-comment-edit'));
            return;
        }
        const commentWithdrawEl = event.target.closest('[data-wcn-comment-withdraw]');
        if (commentWithdrawEl) {
            await withdrawComment(
                commentWithdrawEl.getAttribute('data-wcn-comment-task'),
                commentWithdrawEl.getAttribute('data-wcn-comment-withdraw'));
            return;
        }
        const commentEl = event.target.closest('[data-wcn-comment-post]');
        if (commentEl) {
            const inp = document.querySelector('#wcnApp [data-wcn-comment-input]');
            // AWAITED. An un-awaited promise here would swallow its own rejection, and a failed post would look
            // exactly like a button that was never wired — which is how the subtask writer shipped broken.
            await postComment(commentEl.getAttribute('data-wcn-comment-post'), inp && inp.value);
            return;
        }
        const attachEl = event.target.closest('[data-wcn-attach]');
        if (attachEl) { toast(tf('ToastAttachment', attachEl.getAttribute('data-wcn-attach')), 'info'); return; }

        // Table quick-view (eye): a deliberate "view detail" — open the split panel
        // for this row. Unlike a stray cell click (which the grid ignores), this is an
        // explicit affordance, so leaving the grid for the detail is expected.
        const detailEl = event.target.closest('[data-wcn-detail]');
        if (detailEl) {
            state.selectedId = detailEl.getAttribute('data-wcn-detail');
            const it = itemById(state.selectedId);
            if (it) { markSeen(it); }
            openDetailPage(state.selectedId);
            return;
        }

        // BL-049 — the reference id lives on a copy button now, so it has to actually copy.
        const copyEl = event.target.closest('[data-wcn-copy]');
        if (copyEl) {
            const value = copyEl.getAttribute('data-wcn-copy') || '';
            // navigator.clipboard is absent over plain http and in older browsers; say so rather than failing
            // silently, because a copy button that does nothing is worse than no button.
            global.navigator?.clipboard?.writeText?.(value)
                .then(() => toast(t('ReferenceCopied')))
                .catch(() => toast(t('ReferenceCopyFailed'), 'error'))
                ?? toast(t('ReferenceCopyFailed'), 'error');
            return;
        }

        const openEl = event.target.closest('[data-wcn-open]');
        if (openEl) {
            const item = itemById(openEl.getAttribute('data-wcn-open'));
            if (item && item.deepLink) {
                global.open(item.deepLink, '_blank', 'noopener,noreferrer');
                toast(tf('ToastOpenSource', item.sourceModule, item.title), 'info');
            }
            return;
        }

        const refreshSourceEl = event.target.closest('[data-wcn-refresh-source]');
        if (refreshSourceEl) {
            refreshSourceEl.setAttribute('disabled', 'disabled');
            global.setTimeout(() => {
                render();
                toast(t('SourceProjectionRequested'), 'info');
            }, 350);
            return;
        }

        const triggerActionEl = event.target.closest('[data-wcn-trigger-action]');
        if (triggerActionEl) {
            const triggerId = triggerActionEl.getAttribute('data-wcn-trigger-id');
            const actionCode = triggerActionEl.getAttribute('data-wcn-trigger-action');
            const trigger = state.triggers.find((candidate) => candidate.id === triggerId);
            const action = trigger?.actions?.find((candidate) => candidate.code === actionCode);
            performTriggerAction(trigger, action);
            return;
        }

        const triggerOpenEl = event.target.closest('[data-wcn-trigger-open]');
        if (triggerOpenEl) {
            const trigger = state.triggers.find((candidate) => candidate.id === triggerOpenEl.getAttribute('data-wcn-trigger-open'));
            if (trigger?.source?.deepLink) {
                markTriggerSeen(trigger);
                render();
                global.open(trigger.source.deepLink, '_blank', 'noopener,noreferrer');
            }
            return;
        }



        const sortEl = event.target.closest('[data-wcn-sort]');
        if (sortEl) {
            const key = sortEl.getAttribute('data-wcn-sort');
            if (state.sortKey === key) { state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc'; }
            else { state.sortKey = key; state.sortDir = 'asc'; }
            render();
            return;
        }

        if (event.target.closest('[data-wcn-clear-filters]')) {
            state.moduleFilter = []; state.priorityFilter = 'all'; state.modeFilter = 'all';
            state.typeFilter.clear(); state.signalFilter.clear(); state.search = ''; render(); return;
        }

        // List cards open the dedicated Golden Compact-style detail route. Split
        // keeps master-detail selection in place. Table remains a power-grid and is
        // opened through its own actions / Responsive modal.
        // power-grid: acting happens via the "İşlemler" column / bulk, and detail via
        // the responsive + modal. Yanking the user to split on any cell click would
        // break the "stay in the grid" model.
        const rowEl = event.target.closest('[data-wcn-row]');
        // Any interactive control inside the row (action buttons, the ··· overflow
        // toggle + its menu items, pin, checkbox) handles its own click — never hijack
        // it into the split-detail navigation, which would re-render and kill the
        // dropdown before Bootstrap can open it.
        const onControl = event.target.closest('button, a, [data-bs-toggle], .dropdown-menu, [data-wcn-check], .wcn-td-check');
        if (rowEl && state.view !== 'table' && !onControl) {
            state.selectedId = rowEl.getAttribute('data-wcn-row');
            const it = itemById(state.selectedId);
            if (it) { markSeen(it); }
            openDetailPage(state.selectedId);   // detail is its own page now (no split)
        }
    };

    const onChange = (event) => {
        // A <select> emits change, not input, so the create panel's pickers would otherwise never reach the draft.
        const newFieldEl = event.target.closest('[data-wcn-newsubtask-field]');
        if (newFieldEl) {
            state.subtaskCreateDraft = Object.assign({}, state.subtaskCreateDraft, {
                [newFieldEl.getAttribute('data-wcn-newsubtask-field')]: newFieldEl.value
            });
            return;
        }
        const pageLengthEl = event.target.closest('[data-wcn-page-length]');
        if (pageLengthEl) {
            state.pageLength = [10, 25, 50, 100].includes(Number(pageLengthEl.value)) ? Number(pageLengthEl.value) : 10;
            state.listPage = 0;
            render();
            return;
        }
        const columnEl = event.target.closest('[data-wcn-column]');
        if (columnEl) {
            const index = Number(columnEl.getAttribute('data-wcn-column'));
            if (index > 0 && index < state.tableColumnVisibility.length) {
                state.tableColumnVisibility[index] = columnEl.checked;
                render();
            }
            return;
        }
        const filterEl = event.target.closest('[data-wcn-filter]');
        if (filterEl) {
            const which = filterEl.getAttribute('data-wcn-filter');
            const value = filterEl.type === 'checkbox' ? filterEl.checked
                : filterEl.multiple
                    ? Array.from(filterEl.selectedOptions).map((option) => option.value)
                    : (filterEl.value || 'all');
            applyFilterValue(which, value);
            render();
            return;
        }
        /*
         * ⚠ THE BULK SELECTION HANDLERS WENT WITH THE STRIP (2026-08-24, Tur B). MEASURED: `data-wcn-check`
         * was READ in four places and DRAWN in none — the table renders no selection column, so
         * `state.tableSelected` could never become non-empty through the UI and the bulk bar could never
         * appear. Like the "+ Yeni" menu, it had just been given the product's dialog appearance.
         */
    };

    let searchTimer = null;
    const onInput = (event) => {
        const newFieldEl = event.target.closest('[data-wcn-newsubtask-field]');
        if (newFieldEl) {
            state.subtaskCreateDraft = Object.assign({}, state.subtaskCreateDraft, {
                [newFieldEl.getAttribute('data-wcn-newsubtask-field')]: newFieldEl.value
            });
            return;
        }
        const fieldEl = event.target.closest('[data-wcn-subtask-field]');
        if (fieldEl) {
            state.subtaskPanelDraft = Object.assign({}, state.subtaskPanelDraft, {
                [fieldEl.getAttribute('data-wcn-subtask-field')]: fieldEl.value
            });
            return;
        }
        const searchEl = event.target.closest('[data-wcn-search]');
        if (!searchEl) { return; }
        const value = searchEl.value;
        global.clearTimeout(searchTimer);
        searchTimer = global.setTimeout(() => {
            state.search = value;
            state.listPage = 0;
            render();
            const again = document.querySelector('#wcnApp [data-wcn-search]');
            if (again) { again.focus(); again.setSelectionRange(value.length, value.length); }
        }, 180);
    };

    // ── Data source (WC-1b) ───────────────────────────────────────────────────
    // The REAL work-item projection is canonical. Showcase fixtures remain available for UX demos/QA but only
    // when the SERVER enabled them (Development), so production has no path to fixture data (DEC-1).
    const applySeenState = () => {
        state.items.forEach((item) => {
            if (seenIds.has(item.id)) {
                item.isUnread = false;
                if (item.personal) { item.personal.seen = true; }
            }
        });
        state.triggers.forEach((trigger) => { trigger.isUnread = !seenIds.has(trigger.id); });
    };

    /*
     * WHICH READ IS ALLOWED TO SPEAK.
     *
     * Thirteen places re-read the projection, and every write goes through one of them. Two reads that overlap
     * used to both assign `state.items` and both render, so the one that ANSWERED last won — which is not the
     * one that ASKED last. That is how a row the user had just removed came back: the read issued before the
     * removal landed after it, carrying the old snooze, and painted it.
     *
     * A counter, not a lock: a stale answer is discarded rather than delaying the fresh one. `state.loadState`
     * and `state.loadError` are held to the same rule — a stale error must not blank a page that has since
     * loaded fine.
     *
     * ⚠ THIS COVERS EVERY DIALOG, not just the snooze: plan, inquire, reassign, the checklist and the notes all
     * refresh through this one function. Fixing it in one of them and leaving the siblings is the mistake this
     * session already made three times.
     */
    let loadGeneration = 0;

    const loadWorkItems = async () => {
        const generation = ++loadGeneration;
        const isStale = () => generation !== loadGeneration;

        /*
         * A REFRESH IS NOT A LOAD, and conflating the two is what made every write feel like a page reload.
         *
         * MEASURED: each write re-reads the projection, and this used to blank the page to a spinner first. The
         * document collapsed to a few hundred pixels, the browser clamped the scroll to the shorter body, and
         * the position was already gone by the time the real content came back — so restoring it afterwards
         * restored a zero. The focus went the same way, and the "which row did I just add" mark with it.
         *
         * The spinner belongs to the FIRST load, when there is genuinely nothing on screen. A re-read over
         * existing content keeps the content, the height, the scroll and the caret; if it fails, the error
         * state still takes over below.
         */
        const firstLoad = !Array.isArray(state.items) || state.items.length === 0;
        state.loadError = null;
        if (firstLoad) {
            state.loadState = 'loading';
            render();
        }

        if (data.showcaseFixturesEnabled && data.showcaseFixturesEnabled()) {
            state.items = data.buildItems();
            state.triggers = data.buildTriggers ? data.buildTriggers() : [];
            state.meetings = data.buildMeetings ? data.buildMeetings() : [];
            state.notes = data.buildNotes ? data.buildNotes() : [];
            if (isStale()) { return; }
            applySeenState();
            state.loadState = 'ready';
            render();
            return;
        }

        const api = global.WorkCenterNextApi;
        if (!api) { state.loadState = 'error'; state.loadError = 'error'; render(); return; }

        /*
         * BL-023 — ask the ORG CHART whether this user has reports, before the control is drawn.
         *
         * Not inferred from an empty team list: "nobody reports to you" and "your team has no open work" are
         * indistinguishable in an empty array, and telling them apart is the whole empty-state decision. Asked
         * once per load and fail-closed, so an unreachable answer disables the option rather than offering one
         * that will error.
         */
        state.team = await api.fetchTeamAvailability();

        const result = await api.fetchWorkItems(
            { scope: state.scope === 'team' ? api.SCOPE.TEAM : api.SCOPE.SELF });
        // A newer read has been issued while this one was in flight: its answer is the current one, and this
        // answer describes a state that no longer exists. Drop it without touching the screen.
        if (isStale()) { return; }
        if (result.status === api.STATUS.OK) {
            state.items = result.items;
            // Triggers/meetings/notes have no provider yet — they stay empty until one lands (DEC-1).
            state.triggers = [];
            state.meetings = [];
            state.notes = [];
            applySeenState();
            state.loadState = 'ready';
        } else {
            state.items = [];
            state.loadState = 'error';
            state.loadError = result.status; // forbidden | unauthorized | unavailable | error
        }
        render();
    };

    /*
     * Every write this module performs — transitions, checklist ticks, subtask creation — goes through globals
     * that a HOST PAGE has to include. app.js cannot import them, so a page that forgets one produces a surface
     * whose buttons do nothing: the handler throws on `undefined`, the rejection is swallowed, and there is no
     * request and no message. /WorkCenterNext/Details shipped in exactly that state.
     *
     * Announced at boot rather than on first click, so the gap is visible before a user finds it.
     */
    // `bootstrap` joins the list because the subtask quick-edit panel is an offcanvas: without it the row
    // click does nothing at all, which is the same silent failure as a missing TasksApi.
    const WRITE_DEPENDENCIES = ['TasksApi', 'TaskForm', 'bootstrap'];

    const reportMissingWriteDependencies = () => {
        const missing = WRITE_DEPENDENCIES.filter((name) => !global[name]);
        if (!missing.length) { return; }
        console.error(
            `[WorkCenterNext] Missing required script(s): ${missing.join(', ')}. Every write on this page will `
            + 'fail silently. The host view must load assets/js/Tasks/api.js and assets/js/Tasks/form.js '
            + '(see Views/WorkCenterNext/Index.cshtml).');
    };

    // ── Boot ──────────────────────────────────────────────────────────────────
    let booted = false;
    const boot = async () => {
        const root = document.getElementById('wcnApp');
        if (!root || booted) { return; }   // guard: a second bundle load / hot reload
        booted = true;                     // must not double-bind document listeners
        if (root.dataset.wcnPage !== 'detail') {
            hydrateStateFromUrl();
            state.viewsByTab[state.tab] = state.view;
        }
        // The detail page used to declare itself 'ready' here, before loadWorkItems had fetched anything — so the
        // first paint had no items and announced the task did not exist. It stays 'loading' until the projection
        // answers, like every other surface.
        reportMissingWriteDependencies();
        /*
         * onClick is async, so anything it throws becomes an UNHANDLED REJECTION — which the browser swallows.
         * That is how a page missing Tasks/api.js looked exactly like a page whose buttons were never wired:
         * the handler ran, threw on an undefined global, and produced no request, no toast and no warning.
         * A failed click now says so, in the console and to the user.
         */
        document.addEventListener('click', (event) => {
            Promise.resolve()
                .then(() => onClick(event))
                .catch((error) => {
                    console.error('WorkCenterNext click handler failed.', error);
                    toast(t('ErrorTitle'), 'error');
                });
        });
        document.addEventListener('change', onChange);
        document.addEventListener('input', onInput);
        document.addEventListener('keydown', onKeydown);
        // The quick-create offcanvas announces a new task instead of touching state directly, so this module
        // stays the only owner of the work-item list.
        document.addEventListener('wcn:task-created', (event) => {
            refreshAfterTaskCreated(event.detail?.title);
        });
        // Move focus to the first menu item when a header dropdown opens (mouse or
        // keyboard) — Bootstrap only auto-focuses on keyboard-open. Delegated so it
        // survives the innerHTML re-render.
        document.addEventListener('shown.bs.dropdown', (event) => {
            const container = event.target && event.target.closest ? event.target.closest('.dropdown') : null;
            const menu = container && container.querySelector('.wcn-dd-menu');
            const first = menu && menu.querySelector('.dropdown-item:not(:disabled)');
            if (first) { first.focus(); }
        });
        render();
        // WC-1b — the detail page resolves its item out of state.items too, so it must load the projection as
        // well (previously the fixtures were loaded synchronously at state-init and both pages got them free).
        await loadWorkItems();
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(window);
