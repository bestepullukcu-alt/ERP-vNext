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
    const SIGNAL_ICON = { blocked: 'bx-lock-alt', 'sla-risk': 'bx-time-five', escalated: 'bx-up-arrow-alt' };
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
    const SIGNALS = ['blocked', 'sla-risk', 'escalated'];
    const SIGNAL_KEY = { blocked: 'SignalBlocked', 'sla-risk': 'SignalSlaRisk', escalated: 'SignalEscalated' };

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
        escalated: (i) => !!i.escalated
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


    const chip = (kind, icon, text, title) =>
        `<span class="wcn-chip wcn-chip-${kind}"${title ? ` title="${esc(title)}"` : ''}>` +
        `<i class="bx ${icon}"></i><span>${esc(text)}</span></span>`;

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
    const tabCount = (tab) => state.items.filter((item) => inTab(item, tab)).length
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
                        ${createItem('note', 'bx-note', t('NewNote'))}
                        ${createItem('meeting', 'bx-calendar-event', t('NewMeeting'))}
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
        // Same two facts as the detail note: the person if we know one, otherwise the holder's own sentence.
        item.waitingOn ? chip('warning', 'bx-time-five', tf('WaitingOn', item.waitingOn))
            : item.waitingReason ? chip('warning', 'bx-time-five', item.waitingReason) : '',
        // Why the leading action cannot be used, ON the row rather than only in the button's tooltip. A blocked
        // item whose reason needs a hover reads as simply broken.
        blockedPrimaryReason(item) ? chip('secondary', 'bx-lock-alt', blockedPrimaryReason(item)) : '',
        (item.snoozedUntil && item.snoozedUntil > data.todayIso) ? chip('secondary', 'bx-moon', tf('SnoozedUntil', item.snoozedUntil)) : '',
        (item.systemState && SYSSTATE[item.systemState]) ? chip(SYSSTATE[item.systemState].kind, SYSSTATE[item.systemState].icon, t(SYSSTATE[item.systemState].key)) : '',
        item.requester ? chip('requester', 'bx-user', item.requester) : ''
    ].join('');

    const rowHtml = (item, opts) => {
        const compact = opts && opts.compact;
        const inbox = opts && opts.inbox;
        const selected = item.id === state.selectedId;
        const terminal = isTerminal(item);
        const pinBtn = inbox || terminal ? '' : `<button type="button" class="wcn-pin${item.pinned ? ' pinned' : ''}" data-wcn-pin="${item.id}" title="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-label="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-pressed="${item.pinned}"><i class="bx ${item.pinned ? 'bxs-pin' : 'bx-pin'}"></i></button>`;
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
            <div class="wcn-row-actions">${pinBtn}${actionCluster(item)}</div>
        </div>`;
    };

    const inboxActionIcon = (action) => ({
        accept: 'bx-check', approve: 'bx-check-shield', signoff: 'bx-check-circle',
        reject: 'bx-x-circle', decline: 'bx-x-circle', return: 'bx-undo',
        inquire: 'bx-question-mark', reassign: 'bx-user-pin', plan: 'bx-calendar-plus',
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

    const renderSplit = (items) => {
        if (!items.length) { return emptyState(); }
        state.visibleOrder = [];
        const rows = items.slice().sort(bySla).map((item) => {
            state.visibleOrder.push(item.id);
            return splitCard(item);
        }).join('');
        if (!state.selectedId || state.visibleOrder.indexOf(state.selectedId) < 0) {
            state.selectedId = state.visibleOrder[0] || null;
        }
        return `<div class="wcn-split">
            <nav class="wcn-split-list" aria-label="${esc(t('ViewList'))}">${rows}</nav>
            <section class="card wcn-split-detail" aria-label="${esc(t('DetailTabsLabel'))}">${detailHtml(itemById(state.selectedId))}</section>
        </div>`;
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
    const referenceField = (item) => {
        const id = item.sourceId;
        if (id === null || id === undefined || id === '') { return ''; }

        const full = String(id);
        // Long opaque ids get an ellipsis; a short business key is left whole, because that one IS readable.
        const shown = full.length > 13 ? `${full.slice(0, 8)}…${full.slice(-4)}` : full;

        return `<div class="col-12 col-md-6">
            <div class="backbone-preview-field">
                <i class="bx bx-hash"></i>
                <div>
                    <div class="backbone-preview-label">${esc(t('DetailSourceId'))}</div>
                    <div class="backbone-preview-value mt-1 d-flex align-items-center gap-2">
                        <code class="wcn-reference-id" title="${esc(full)}">${esc(shown)}</code>
                        <button type="button" class="btn btn-xs btn-icon btn-label-secondary wcn-copyref"
                                data-wcn-copy="${esc(full)}"
                                title="${esc(t('CopyReference'))}" aria-label="${esc(t('CopyReference'))}">
                            <i class="bx bx-copy"></i>
                        </button>
                    </div>
                </div>
            </div>
        </div>`;
    };

    const previewField = (icon, labelKey, value, col) => {
        if (value === null || value === undefined || value === '') { return ''; }
        return `<div class="col-12 ${col || 'col-md-6'}">
            <div class="backbone-preview-field">
                <i class="bx ${icon}"></i>
                <div>
                    <div class="backbone-preview-label">${esc(t(labelKey))}</div>
                    <div class="backbone-preview-value mt-1">${esc(value)}</div>
                </div>
            </div>
        </div>`;
    };

    /*
     * What this task needs from the viewer RIGHT NOW, in a sentence.
     *
     * The page can already show a dozen true facts without answering "so what do I do?". Keyed by state, and an
     * unmapped state prints NO banner — a guidance box that guesses is worse than none.
     */
    const guidanceFor = (item) => {
        if (item.admissionState === 'pendingAcceptance') { return { kind: 'primary', key: 'GuidancePendingAcceptance' }; }
        if (item.admissionState === 'pendingClaim') { return { kind: 'primary', key: 'GuidancePendingClaim' }; }
        if (item.gates?.approval?.status === 'pending') { return { kind: 'warning', key: 'GuidanceApprovalPending' }; }
        if (item.gates?.review?.status === 'pending') { return { kind: 'warning', key: 'GuidanceReviewPending' }; }
        if (item.lifecycle === 'Waiting') {
            // The holder's own sentence when they gave one — nothing here is invented on their behalf.
            return item.waitingReason
                ? { kind: 'warning', text: tf('GuidanceWaitingBecause', item.waitingReason) }
                : { kind: 'warning', key: 'GuidanceWaiting' };
        }
        return null;
    };

    const renderGuidance = (item) => {
        const guidance = guidanceFor(item);
        if (!guidance) { return ''; }
        const text = guidance.text || t(guidance.key);
        return `<div class="alert alert-${guidance.kind} wcn-guidance d-flex align-items-start gap-2" role="note">
            <i class="bx bx-info-circle"></i><span>${esc(text)}</span>
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

    const actionButton = (item, action, variant) => {
        const disabled = action.disabled;
        const reason = disabled && action.disabledReason
            ? `<span class="wcn-act-reason">${esc(action.disabledReason)}</span>`
            : '';
        return `<li class="wcn-act wcn-act-${variant}${disabled ? ' wcn-act-disabled' : ''}">
            <button type="button" class="btn btn-${variant === 'primary' ? '' : 'label-'}${action.kind} wcn-act-btn"
                    data-wcn-action="${esc(action.key)}" data-wcn-id="${esc(item.id)}"${disabled ? ' disabled' : ''}>
                <i class="bx ${inboxActionIcon(action)}"></i><span>${esc(actionLabel(action))}</span>
            </button>
            ${actionOutcome(action)}${reason}
        </li>`;
    };

    /*
     * The action rail. The PRIMARY action leads it at full size — the whole point of the page is to let someone
     * act, and burying that behind a row of equal-weight buttons makes the reader hunt for it.
     *
     * A disabled primary KEEPS its place, with its reason: why work cannot move is the most important thing on
     * the page, and promoting the next enabled action instead once made `cancel` look like the intended next
     * step. Destructive actions are separated out below, never mixed in with the ordinary ones.
     */
    const renderActionRail = (item) => {
        const actions = itemActions(item);
        if (!actions.length) { return ''; }

        const primary = rowPrimaryAction(actions);
        const destructive = actions.filter((a) => a.destructive && a !== primary);
        const secondary = actions.filter((a) => a !== primary && !a.destructive);

        const available = (primary ? actionButton(item, primary, 'primary') : '')
            + secondary.map((a) => actionButton(item, a, 'secondary')).join('');

        /*
         * Primary and secondary actions stay OPEN, each with its outcome line — those sentences are why the rail
         * exists, and a kebab would hide them. Only DESTRUCTIVE actions fold into the menu: calling work off
         * should take a deliberate second click, not sit at the same weight as accepting it.
         */
        const destructiveMenu = destructive.length
            ? `<div class="dropdown wcn-actrail-menu">
                <button type="button" class="btn btn-sm btn-label-secondary dropdown-toggle" data-bs-toggle="dropdown"
                        aria-expanded="false">${esc(t('ActionsOther'))}</button>
                <ul class="dropdown-menu dropdown-menu-end">
                    ${destructive.map((a) => `<li><button type="button" class="dropdown-item text-danger"
                        data-wcn-action="${esc(a.key)}" data-wcn-id="${esc(item.id)}"${a.disabled ? ' disabled' : ''}>
                        <i class="bx ${inboxActionIcon(a)} me-1"></i>${esc(actionLabel(a))}</button></li>`).join('')}
                </ul>
            </div>`
            : '';

        return `${available
            ? `<div class="wcn-detail-section">
                <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('ActionsAvailable'))}</h6>
                <ul class="wcn-actrail">${available}</ul>
            </div>`
            : ''}
        ${destructiveMenu ? `<div class="wcn-detail-section wcn-actrail-other">${destructiveMenu}</div>` : ''}`;
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

        const rendered = steps.map((step, index) => {
            let cls = 'upcoming';
            if (!cancelled) {
                if (allDone || index < activeIndex) { cls = 'done'; }
                else if (index === activeIndex) { cls = 'active'; }
            }
            const mark = cls === 'done' ? '<i class="bx bx-check"></i>' : (index + 1);
            return `<li class="wcn-step wcn-step-${cls}${step.optional ? ' wcn-step-optional' : ''}">
                <span class="wcn-step-dot">${mark}</span>
                <span class="wcn-step-label">${esc(t(step.key))}</span>
            </li>`;
        }).join('');

        const paused = item.lifecycle === 'Waiting'
            ? `<p class="wcn-step-paused" role="note"><i class="bx bx-pause-circle"></i>${
                esc(item.waitingReason ? tf('StepPausedBecause', item.waitingReason) : t('StepPaused'))}</p>`
            : '';

        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('StepBarLabel'))}</h6>
            <ol class="wcn-steps${cancelled ? ' wcn-steps-cancelled' : ''}">${rendered}</ol>
            ${paused}
        </div>`;
    };

    /*
     * WHAT IS THIS? — the card a detail page owes its reader before anything else.
     *
     * MEASURED (2026-08-12, live): the page opened straight into "what can you do about it". The description
     * was nowhere, and neither were the due date, the start date, the estimate or the tags — the chip said
     * "15 gün gecikmiş" and the page never said what the deadline WAS. Four of those fields were missing from
     * the WC-1 projection entirely and were added to it in the same round; the other three were on the wire
     * already and simply never rendered as fields.
     *
     * EMPTY FIELDS ARE NOT PRINTED. A "Son tarih: —" row is a claim that the value was checked and found empty;
     * the reader cannot tell it from a value that failed to load. summaryFact drops the row instead.
     */
    const summaryFact = (icon, labelKey, value) => {
        if (value === null || value === undefined || value === '') { return ''; }
        return `<div class="wcn-fact">
            <i class="bx ${icon}" aria-hidden="true"></i>
            <div class="wcn-fact-body">
                <span class="wcn-fact-label">${esc(t(labelKey))}</span>
                <span class="wcn-fact-value">${esc(value)}</span>
            </div>
        </div>`;
    };

    const renderSummary = (item) => {
        // A tag strip is rendered only when there ARE tags: an empty strip is the chip-shaped version of a dash.
        const tags = Array.isArray(item.tags) && item.tags.length
            ? `<div class="wcn-fact wcn-fact-wide">
                <i class="bx bx-purchase-tag-alt" aria-hidden="true"></i>
                <div class="wcn-fact-body">
                    <span class="wcn-fact-label">${esc(t('DetailTags'))}</span>
                    <span class="wcn-fact-tags">${item.tags.map((tag) => `<span class="wcn-tag">${esc(tag)}</span>`).join('')}</span>
                </div>
            </div>`
            : '';

        const facts = summaryFact('bx-user-check', 'DetailAssignee', item.assignee)
            + summaryFact('bx-user', 'DetailRequester', item.requester)
            + summaryFact('bx-calendar-exclamation', 'SourceDueLabel', item.dueAt)
            + summaryFact('bx-calendar', 'DetailStartAt', item.startAt)
            + summaryFact('bx-flag', 'DetailPriority', hasPriority(item) ? priorityLabel(item) : '')
            + summaryFact('bx-time-five', 'DetailEstimate',
                item.estimateHours === null || item.estimateHours === undefined
                    ? '' : tf('EstimateHoursValue', item.estimateHours))
            + tags;

        // Neither a description nor a single fact: the card would be a heading over nothing.
        if (!item.summary && !facts) { return ''; }

        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('SummaryCardLabel'))}</h6>
            ${item.summary ? `<p class="wcn-detail-summary">${esc(item.summary)}</p>` : ''}
            ${facts ? `<div class="wcn-facts">${facts}</div>` : ''}
        </div>`;
    };

    // ── Capability-driven depth blocks (spec v3 §5) — do-the-work in the
    // aggregator; define-the-work stays in the source (deep-link). ─────────────
    const hasCap = (item, cap) => Array.isArray(item.workItemCapabilities) && item.workItemCapabilities.indexOf(cap) >= 0;

    // Checklist — interactive (checking is "doing the work", stays here).
    const renderChecklist = (item) => {
        // Capability present but empty is a VALID state (the contract requires the container), so an empty
        // checklist gets an explanation instead of the block silently vanishing.
        if (!hasCap(item, 'checklist') || !item.checklist) { return ''; }
        const items = item.checklist.items || [];
        if (!items.length) {
            return `<div class="wcn-detail-section">
                <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('ChecklistLabel'))}</h6>
                <p class="wcn-block-hint">${esc(t('ChecklistEmpty'))}</p>
            </div>`;
        }
        const done = items.filter((c) => c.done).length;
        const ro = isTerminal(item);
        const rows = items.map((c) =>
            `<li class="wcn-check${c.done ? ' done' : ''}">
                <button type="button" class="wcn-check-box" data-wcn-check-item="${item.id}:${c.id}"${ro ? ' disabled' : ''} aria-pressed="${c.done}">
                    <i class="bx ${c.done ? 'bxs-check-square' : 'bx-square'}"></i>
                </button>
                <span class="wcn-check-text">${esc(c.text)}</span>
            </li>`).join('');
        // The reason completion is unavailable must be READABLE on the page — a disabled button with only a
        // tooltip leaves a keyboard or touch user with no explanation at all.
        const blocked = items.some((c) => c.blocking && !c.done);
        const notice = blocked
            ? `<p class="wcn-block-hint" role="note"><i class="bx bx-error-circle"></i>${esc(t('WorkAggregation_ActionDisabled_ChecklistIncomplete'))}</p>`
            : '';
        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('ChecklistLabel'))} <span class="wcn-count-inline">${done}/${items.length}</span></h6>
            <p class="wcn-block-hint">${esc(t(items.some((c) => c.blocking) ? 'ChecklistBlocksCompletion' : 'ChecklistDoesNotBlock'))}</p>
            <progress class="wcn-progress" value="${done}" max="${items.length}" aria-label="${esc(t('ChecklistLabel'))}"></progress>
            <ul class="wcn-checks">${rows}</ul>
            ${notice}
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
    const renderStatusCard = (item) => {
        const gates = item.gates || {};
        const rows = gateRow('GateApproval', gates.approval) + gateRow('GateReview', gates.review);

        const conflict = item.dueAt && item.plannedDate && item.plannedDate > item.dueAt;
        // Per-cell empty text: "SLA yok" answers "is there a deadline?" and says nothing about whether the
        // holder has planned the work. One shared placeholder made a missing plan read as "no SLA".
        const dateCell = (labelKey, value, emptyKey, cls) => (value || emptyKey
            ? `<div class="wcn-date-cell${cls ? ' ' + cls : ''}"><span class="wcn-date-label">${esc(t(labelKey))}</span><span class="wcn-date-value">${esc(value || t(emptyKey))}</span></div>`
            : '');
        const dates = (item.dueAt || item.plannedDate)
            ? `<div class="wcn-dates">
                ${dateCell('SourceDueLabel', item.dueAt, 'SlaNoSla', item.slaState === 'overdue' ? 'wcn-date-overdue' : '')}
                ${dateCell('PlannedDateLabel', item.plannedDate, 'PlannedDateNone', conflict ? 'wcn-date-conflict' : '')}
            </div>
            ${conflict ? `<div class="wcn-date-warn" role="note"><i class="bx bx-error-circle"></i><span>${esc(t('PlanConflict'))}</span></div>` : ''}`
            : '';

        // Nothing to report is not a card. A task with no gates and no dates says nothing here rather than
        // announcing that it has nothing to say.
        if (!rows && !dates) { return ''; }

        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('StatusCardLabel'))}</h6>
            ${rows ? `<ul class="wcn-gates">${rows}</ul>` : ''}
            ${dates}
        </div>`;
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
    const ACTIVITY_VISIBLE_LIMIT = 5;

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
     * A subtask appears as its OWN row in İşlerim (it is assigned to someone and has its own lifecycle), so that
     * row has to say what it belongs to — otherwise it reads as unexplained standalone work. The parent title is
     * resolved from state when the parent is also visible to this user; otherwise the link alone is shown.
     */
    const renderParentContext = (item) => {
        if (!item.parentTaskItemId) { return ''; }
        const parent = itemById(item.parentTaskItemId);
        const label = parent ? tf('SubtaskOfNamed', parent.title) : t('SubtaskOfUnnamed');
        return `<div class="wcn-detail-section">
            <p class="wcn-block-hint" role="note"><i class="bx bx-subdirectory-right"></i>
                <a href="?id=${esc(item.parentTaskItemId)}">${esc(label)}</a></p>
        </div>`;
    };

    const renderSubtasks = (item) => {
        // Same capability rule as the checklist: declared-but-empty is valid and must explain itself, because a
        // parent with no children yet is exactly where "add a subtask" belongs.
        if (!hasCap(item, 'subtasks') || !item.subtasks) { return ''; }
        const subtaskItems = item.subtasks.items || [];
        const full = item.subtasks.mode === 'full' && !isTerminal(item);
        /*
         * A compact row, not a grid. A task has a handful of subtasks, so paging, export and column pickers cost
         * more than they carry — but "who has it and when is it due" is exactly why anyone opens the list, so
         * each row carries them WHEN THEY EXIST. An absent holder or date renders nothing at all; a dash would
         * be a claim that the field was checked and found empty.
         */
        // Cancelled subtasks sink below the live ones. They are history, and three of them at the top of a list
        // reads as work waiting to be done.
        const ordered = subtaskItems.slice().sort((a, b) =>
            (a.status === 'cancelled' ? 1 : 0) - (b.status === 'cancelled' ? 1 : 0));
        const rows = ordered.map((s) =>
            `<li class="wcn-subtask wcn-subtask-${s.status}">
                <button type="button" class="wcn-subtask-toggle" ${full ? `data-wcn-subtask="${item.id}:${s.id}"` : 'disabled'}
                        aria-label="${esc(tf('SubtaskToggleAria', s.title))}">
                    <i class="bx ${SUBTASK_ICON[s.status] || 'bx-circle'}"></i>
                </button>
                <button type="button" class="wcn-subtask-title wcn-linklike" data-wcn-open-task="${esc(s.id)}"
                        aria-label="${esc(tf('SubtaskOpenAria', s.title))}">${esc(s.title)}</button>
                ${s.assignee?.displayName
                    ? `<span class="wcn-subtask-meta wcn-subtask-assignee"><i class="bx bx-user"></i>${esc(s.assignee.displayName)}</span>`
                    : ''}
                ${s.dueAt
                    ? `<span class="wcn-subtask-meta wcn-subtask-due"><i class="bx bx-calendar"></i>${esc(String(s.dueAt).slice(0, 10))}</span>`
                    : ''}
                ${SUBTASK_STATUS_KEY[s.status]
                    ? `<span class="wcn-subtask-status">${esc(t(SUBTASK_STATUS_KEY[s.status]))}</span>`
                    : ''}
                ${s.canCancel
                    ? `<button type="button" class="wcn-subtask-cancel" data-wcn-subtask-cancel="${esc(s.id)}"
                               data-wcn-subtask-title="${esc(s.title)}"
                               title="${esc(t('SubtaskCancel'))}" aria-label="${esc(tf('SubtaskCancelAria', s.title))}">
                        <i class="bx bx-x-circle"></i>
                       </button>`
                    : ''}
            </li>`).join('');
        // The one-line add stays where it is; the header's "Add" simply focuses it, so there is still exactly one
        // quick-add control rather than two that could disagree.
        const adder = full
            ? `<div class="wcn-subtask-add">
                <input type="text" class="form-control form-control-sm" data-wcn-subtask-input placeholder="${esc(t('SubtaskAddPlaceholder'))}">
                <button type="button" class="btn btn-sm btn-label-primary" data-wcn-subtask-add="${item.id}">${esc(t('SubtaskAdd'))}</button>
               </div>`
            : `<p class="wcn-block-hint"><i class="bx bx-link-external"></i>${esc(t('SubtasksReadonlyHint'))}</p>`;
        /*
         * Open subtasks DO block their parent's completion (BL-035, owner decision 2026-07-29). This notice used to
         * say the opposite, on the reasoning that a second blocking mechanism would make "why can't I finish this?"
         * unanswerable — the banner above now answers it, naming each open child by title.
         *
         * `cancelled` is NOT open: called-off work cannot be finished and must not hold the parent.
         */
        const openSubtasks = subtaskItems.filter((s) => s.status !== 'done' && s.status !== 'cancelled');
        const openNotice = openSubtasks.length
            ? `<p class="wcn-block-hint" role="note"><i class="bx bx-lock-alt"></i>${esc(tf('SubtasksBlockingNotice', openSubtasks.length))}</p>`
            : '';
        /*
         * TOO MANY: the list scrolls inside its own card and says how many there are. The cap is released by a
         * class toggle (FG-003) rather than by re-rendering, so a half-typed quick-add is not thrown away.
         */
        const capped = subtaskItems.length > SUBTASK_VISIBLE_LIMIT;
        const list = capped
            ? `<div class="wcn-scrollcap" data-wcn-scrollcap><ul class="wcn-subtasks">${rows}</ul></div>
               <button type="button" class="btn btn-sm btn-label-secondary wcn-showall" data-wcn-showall>${esc(tf('ShowAllCount', subtaskItems.length))}</button>`
            : `<ul class="wcn-subtasks">${rows}</ul>`;
        const body = subtaskItems.length
            ? `${list}${openNotice}`
            : '';
        /*
         * A LIST card, not a checklist: heading and count on the left, its add controls on the right — the shape
         * the main page's list cards already use. Read as a checklist while the quick-add input sat under a bare
         * heading with no count.
         *
         * NO search box, deliberately. A task has three to ten subtasks; a filter earns its space at fifteen or
         * more, and that is rare. Same reasoning that kept a full DataTable out.
         */
        /*
         * NOTHING YET IS ONE LINE, not a card.
         *
         * MEASURED: on a fresh task "Henüz alt görev yok" and "Henüz etkinlik kaydı yok" were two full-height
         * boxes, so half the page announced that there was nothing on it. The line still carries the action —
         * an empty state that cannot be acted on is just an apology.
         */
        if (!subtaskItems.length) {
            /*
             * The QUICK-ADD ITSELF is the action on this line, not a button that opens something else. An
             * earlier draft of this line offered the detailed panel instead and thereby removed the one-line
             * add from the exact place it matters most — a parent with no children yet. Two tests caught it.
             */
            return `<div class="wcn-empty-line">
                <i class="bx bx-list-check" aria-hidden="true"></i>
                <span class="wcn-empty-text">${esc(t('SubtasksEmpty'))}</span>
                ${full
                    ? `<div class="wcn-subtask-add wcn-empty-action">
                        <input type="text" class="form-control form-control-sm" data-wcn-subtask-input placeholder="${esc(t('SubtaskAddPlaceholder'))}">
                        <button type="button" class="btn btn-sm btn-label-primary" data-wcn-subtask-add="${item.id}">${esc(t('SubtaskAdd'))}</button>
                        <button type="button" class="btn btn-sm btn-label-secondary" data-wcn-subtask-add-detailed="${item.id}">${esc(t('SubtaskAddDetailed'))}</button>
                       </div>`
                    : ''}
            </div>`;
        }

        return `<div class="wcn-detail-section">
            <div class="d-flex align-items-center justify-content-between gap-2 mb-3">
                <h6 class="text-uppercase text-heading fw-semibold mb-0">
                    ${esc(t('SubtasksLabel'))}
                    ${subtaskItems.length ? `<span class="wcn-count-inline">${subtaskItems.length}</span>` : ''}
                </h6>
                ${full ? `<div class="d-flex align-items-center gap-2 flex-shrink-0">
                    <button type="button" class="btn btn-sm btn-label-primary" data-wcn-subtask-add-inline="${item.id}">${esc(t('SubtaskAdd'))}</button>
                    <button type="button" class="btn btn-sm btn-label-secondary" data-wcn-subtask-add-detailed="${item.id}">${esc(t('SubtaskAddDetailed'))}</button>
                </div>` : ''}
            </div>
            ${body}
            ${adder}
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
            return `<li class="wcn-blocked-item">
                ${b.dependencyType ? `<span class="wcn-chip wcn-chip-danger wcn-dep-type" title="${esc(t(DEP_TYPE_KEY[b.dependencyType] || b.dependencyType))}">${esc(DEP_TYPE_ABBR[b.dependencyType] || b.dependencyType)}</span>` : ''}
                <span class="wcn-blocked-why">${esc(sentenceKey ? tf(sentenceKey, name) : name)}</span>
                ${affectsKey ? `<span class="wcn-blocked-affects">${esc(t(affectsKey))}</span>` : ''}
            </li>`;
        }).join('');
        return `<div class="wcn-blocked" role="alert">
            <i class="bx bx-lock-alt"></i>
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
    const renderDependencies = (item) => {
        if (!hasCap(item, 'dependencies') || !item.dependencies || !item.dependencies.length) { return ''; }
        const rows = item.dependencies.map((d) =>
            `<li class="wcn-dep${d.state === 'cancelled' ? ' is-cancelled' : ''}">
                <span class="wcn-dep-dir">${esc(t(d.direction === 'pred' ? 'DepPredecessor' : 'DepSuccessor'))}</span>
                <span class="wcn-dep-title">${esc(d.title)}</span>
                <span class="wcn-chip wcn-chip-secondary wcn-dep-type" title="${esc(t(DEP_TYPE_KEY[d.type] || d.type))}">${esc(DEP_TYPE_ABBR[d.type] || d.type)}</span>
                <span class="wcn-badge wcn-badge-${DEP_STATE_KIND[d.state] || 'secondary'}">${esc(t(DEP_STATE_KEY[d.state] || d.state))}</span>
            </li>`).join('');
        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('DependenciesLabel'))}</h6>
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
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('AttachmentsLabel'))}</h6>
            <ul class="wcn-attachments">${rows}</ul>
        </div>`;
    };

    const renderEvidence = (item) => {
        if (!hasCap(item, 'evidence') || !item.evidence) { return ''; }
        const entries = (item.evidence.items || []).map((entry) =>
            `<li class="wcn-attach"><i class="bx bx-shield-quarter"></i><span class="wcn-attach-name">${esc(data.resolveLabel(entry.label) || entry.id)}</span></li>`
        ).join('');
        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('EvidenceMissing'))}</h6>
            ${entries ? `<ul class="wcn-attachments">${entries}</ul>` : `<p class="text-muted mb-0">${esc(t('ActionDisabledEvidenceIncomplete'))}</p>`}
        </div>`;
    };

    // Personal note — the thin overlay WorkCenter owns (only I see it).
    const renderNote = (item) => {
        if (isTerminal(item)) { return ''; }
        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('NoteLabel'))}</h6>
            <div class="wcn-note">
                <textarea class="form-control form-control-sm" data-wcn-note-input rows="2" placeholder="${esc(t('NotePlaceholder'))}">${esc(item.note || '')}</textarea>
                <button type="button" class="btn btn-sm btn-label-secondary" data-wcn-note-save="${item.id}">${esc(t('NoteSave'))}</button>
            </div>
        </div>`;
    };

    // Comment composer — single stream: what I write also goes to the source.
    const renderComposer = (item) => {
        if (!hasCap(item, 'activity') || isTerminal(item)) { return ''; }
        return `<div class="wcn-composer">
            <input type="text" class="form-control form-control-sm" data-wcn-comment-input placeholder="${esc(t('CommentPlaceholder'))}">
            <button type="button" class="btn btn-sm btn-primary" data-wcn-comment-post="${item.id}">${esc(t('CommentPost'))}</button>
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
        return `<div class="wcn-detail-section">
            <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('TimesheetLabel'))}</h6>
            <div class="wcn-timesheet">
                <span class="wcn-ts-icon"><i class="bx bx-time"></i></span>
                <span class="wcn-ts-total">${esc(formatMinutes(ts.loggedMinutes))}</span>
                <span class="wcn-ts-sub">${esc(t('TimeLoggedLabel'))}</span>
                ${live}
            </div>
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

    const renderApprovalContext = (item) => {
        if (item.itemType !== 'approval' || !hasCap(item, 'approvalContext') || item.amount == null) { return ''; }
        const lines = (item.lineItems || []).map((line) => `<tr>
            <td><span class="wcn-line-desc">${esc(line.desc)}</span><span class="wcn-line-code">${esc(line.gl || '—')} · ${esc(line.costCenter || '—')}</span></td>
            <td class="text-end">${esc(String(line.qty))}</td>
            <td class="text-end">${esc(formatMoney(line.unitPrice, item.currency))}</td>
            <td class="text-end wcn-line-total">${esc(formatMoney(Number(line.qty) * Number(line.unitPrice), item.currency))}</td>
        </tr>`).join('');
        return `<section class="wcn-detail-section wcn-business-section">
            ${sectionHead('bx-wallet-alt', 'ApprovalContextTitle')}
            <div class="wcn-kpi-grid">
                <div class="wcn-kpi wcn-kpi-primary"><span>${esc(t('ApprovalAmount'))}</span><strong>${esc(formatMoney(item.amount, item.currency))}</strong></div>
                <div class="wcn-kpi"><span>${esc(t('ApprovalThreshold'))}</span><strong>${esc(formatMoney(item.threshold, item.currency))}</strong></div>
                <div class="wcn-kpi"><span>${esc(t('BudgetImpact'))}</span><strong>${esc(item.budgetImpact)}</strong></div>
            </div>
            ${lines ? `<div class="wcn-subsection-title">${esc(t('LineItemsTitle'))}</div>
                <div class="table-responsive wcn-line-table-wrap"><table class="table table-sm wcn-line-table">
                    <thead><tr><th>${esc(t('LineDescription'))}</th><th class="text-end">${esc(t('LineQuantity'))}</th><th class="text-end">${esc(t('LineUnitPrice'))}</th><th class="text-end">${esc(t('ApprovalAmount'))}</th></tr></thead>
                    <tbody>${lines}</tbody>
                </table></div>` : ''}
        </section>`;
    };

    const renderReviewContext = (item) => {
        if (item.itemType !== 'review' || !hasCap(item, 'reviewContext') || !item.artifact) { return ''; }
        const checks = (item.reviewChecklist || []).map((check) =>
            `<li class="wcn-review-check ${check.done ? 'is-done' : ''}"><i class="bx ${check.done ? 'bx-check-circle' : 'bx-circle'}"></i><span>${esc(check.label)}</span><small>${esc(t(check.done ? 'ReviewCheckComplete' : 'ReviewCheckPending'))}</small></li>`
        ).join('');
        const signatures = (item.signatureHistory || []).map((signature) =>
            `<li class="wcn-process-row"><span class="wcn-process-marker is-${esc(signature.status)}"><i class="bx bx-pen"></i></span><div><strong>${esc(signature.actor)}</strong><span>${esc(t(signature.status === 'signed' ? 'SignatureSigned' : 'SignaturePending'))}${signature.at ? ` · ${esc(signature.at)}` : ''}</span></div></li>`
        ).join('');
        return `<section class="wcn-detail-section wcn-business-section">
            ${sectionHead('bx-file-find', 'ReviewContextTitle')}
            <a class="wcn-artifact-card" href="${esc(item.artifact.url)}">
                <span class="wcn-business-icon"><i class="bx bx-file"></i></span>
                <span><strong>${esc(item.artifact.name)}</strong><small>${esc(t('ReviewVersion'))}: ${esc(item.artifact.version)}</small></span>
                <i class="bx bx-link-external"></i>
            </a>
            ${checks ? `<div class="wcn-subsection-title">${esc(t('ReviewChecklistTitle'))}</div><ul class="wcn-review-checks">${checks}</ul>` : ''}
            ${signatures ? `<div class="wcn-subsection-title">${esc(t('SignatureHistoryTitle'))}</div><ol class="wcn-process-list">${signatures}</ol>` : ''}
        </section>`;
    };

    const renderExceptionContext = (item) => {
        if (item.itemType !== 'exception' || !hasCap(item, 'exceptionContext') || !item.discrepancy) { return ''; }
        const d = item.discrepancy;
        const options = (item.resolutionOptions || []).map((option) => `<li><i class="bx bx-chevron-right"></i><span>${esc(option)}</span></li>`).join('');
        return `<section class="wcn-detail-section wcn-business-section">
            ${sectionHead('bx-error-alt', 'ExceptionContextTitle')}
            <div class="wcn-discrepancy">
                <div><span>${esc(t('DiscrepancyField'))}</span><strong>${esc(d.field)}</strong></div>
                <div><span>${esc(t('ExpectedValue'))}</span><strong>${esc(String(d.expected))}</strong></div>
                <div><span>${esc(t('ActualValue'))}</span><strong>${esc(String(d.actual))}</strong></div>
                <div class="is-alert"><span>${esc(t('DeltaPercent'))}</span><strong>${esc(String(d.deltaPct))}%</strong></div>
            </div>
            ${item.rootCause ? `<div class="wcn-callout"><i class="bx bx-search-alt"></i><div><span>${esc(t('RootCause'))}</span><strong>${esc(item.rootCause)}</strong></div></div>` : ''}
            ${options ? `<div class="wcn-subsection-title">${esc(t('ResolutionOptions'))}</div><ul class="wcn-option-list">${options}</ul>` : ''}
        </section>`;
    };

    const renderTaskContext = (item) => {
        if (item.itemType !== 'task' || !hasCap(item, 'taskContext') || !item.effort) { return ''; }
        const estimate = Number(item.effort.estimate) || 0;
        const spent = Number(item.effort.spent) || 0;
        const progress = estimate ? Math.min(100, Math.round((spent / estimate) * 100)) : 0;
        const history = (item.assignmentHistory || []).map((entry) =>
            `<li class="wcn-process-row"><span class="wcn-process-marker"><i class="bx bx-user"></i></span><div><strong>${esc(entry.assignee)}</strong><span>${esc(entry.action)} · ${esc(entry.at)}</span></div></li>`
        ).join('');
        return `<section class="wcn-detail-section wcn-business-section">
            ${sectionHead('bx-timer', 'TaskContextTitle')}
            <div class="wcn-effort-head"><div><span>${esc(t('EffortSpent'))}</span><strong>${esc(String(spent))} / ${esc(String(estimate))} ${esc(t('HoursShort'))}</strong></div><span>${progress}%</span></div>
            <div class="progress wcn-effort-progress" role="progressbar" aria-valuenow="${progress}" aria-valuemin="0" aria-valuemax="100"><div class="progress-bar wcn-progress-${Math.round(progress / 10) * 10}"></div></div>
            ${history ? `<div class="wcn-subsection-title">${esc(t('AssignmentHistoryTitle'))}</div><ol class="wcn-process-list">${history}</ol>` : ''}
        </section>`;
    };

    const renderMeetingContext = (item) => {
        if (item.itemType !== 'meetingInvite' || !hasCap(item, 'meetingContext')) { return ''; }
        const agenda = (item.agenda || []).map((entry) => `<li>${esc(entry)}</li>`).join('');
        const participants = (item.participants || []).map((person) =>
            `<li class="wcn-participant"><span class="avatar avatar-sm"><span class="avatar-initial rounded-circle bg-label-primary">${esc(person.name.charAt(0))}</span></span><div><strong>${esc(person.name)}</strong><span>${esc(t(person.role === 'organizer' ? 'ParticipantOrganizer' : person.role === 'optional' ? 'ParticipantOptional' : 'ParticipantRequired'))}</span></div><small class="is-${esc(person.status)}">${esc(t(person.status === 'accepted' ? 'AttendanceAccepted' : person.status === 'declined' ? 'AttendanceDeclined' : 'AttendancePending'))}</small></li>`
        ).join('');
        return `<section class="wcn-detail-section wcn-business-section">
            ${sectionHead('bx-calendar-event', 'MeetingContextTitle')}
            <div class="wcn-meeting-facts"><span><i class="bx bx-time"></i>${esc(item.meetingStart)}–${esc(item.meetingEnd)}</span><span><i class="bx bx-map"></i>${esc(item.meetingLocation)}</span><span><i class="bx bx-user-check"></i>${esc(t(item.attendanceStatus === 'accepted' ? 'AttendanceAccepted' : item.attendanceStatus === 'declined' ? 'AttendanceDeclined' : 'AttendancePending'))}</span></div>
            ${agenda ? `<div class="wcn-subsection-title">${esc(t('MeetingAgendaTitle'))}</div><ol class="wcn-agenda-list">${agenda}</ol>` : ''}
            ${participants ? `<div class="wcn-subsection-title">${esc(t('MeetingParticipantsTitle'))}</div><ul class="wcn-participants">${participants}</ul>` : ''}
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

    const renderThread = (item) => {
        if (!hasCap(item, 'thread') || !(item.thread || []).length) { return ''; }
        const messages = item.thread.map((message) => {
            const mine = message.actor === data.currentUser.name;
            return `<li class="wcn-thread-message${mine ? ' is-mine' : ''}"><div class="wcn-thread-bubble"><strong>${esc(message.actor)}</strong><p>${esc(message.text)}</p><time>${esc(message.at)}</time></div></li>`;
        }).join('');
        return `<section class="wcn-detail-section wcn-business-section">${sectionHead('bx-conversation', 'ConversationTitle')}<ul class="wcn-thread">${messages}</ul></section>`;
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
    const technicalVersion = (item) => {
        // "version: 8" is the wire's own spelling of the concurrency token. A number in a sentence is what a
        // person reads; the KIND stays in the title attribute for whoever actually needs to know it.
        const concurrency = item.concurrency;
        if (!concurrency || concurrency.token === null || concurrency.token === undefined) { return ''; }
        const numeric = /^\d+$/.test(String(concurrency.token));
        return numeric ? tf('TechVersionValue', concurrency.token) : String(concurrency.token);
    };

    const renderTechnicalDetails = (item) => `<details class="wcn-tech">
            <summary class="wcn-tech-summary">
                <i class="bx bx-code-alt" aria-hidden="true"></i>
                <span>${esc(t('TechnicalDetailsLabel'))}</span>
            </summary>
            <div class="row g-4 wcn-tech-body">
                ${previewField('bx-flag', 'DetailNativeStatus', item.nativeStatusText)}
                ${referenceField(item)}
                ${previewField('bx-cube', 'DetailModuleName', item.sourceModuleName || item.sourceModule)}
                ${previewField('bx-purchase-tag-alt', 'DetailModuleId', item.sourceModuleId)}
                ${previewField('bx-category', 'DetailSourceType', item.sourceObjectType || item.sourceType)}
                ${previewField('bx-link-external', 'DetailActionDepth',
                    t(item.actionDepth === 'deeplink' ? 'ActionDepthDeeplink' : 'ActionDepthInline'))}
                ${previewField('bx-git-branch', 'DetailSourceVersion', technicalVersion(item))}
                ${previewField('bx-cog', 'DetailLifecycleOwner', item.lifecycleOwner?.providerCode)}
            </div>
            <button type="button" class="btn btn-sm btn-label-primary wcn-opensource" data-wcn-open="${item.id}" aria-label="${esc(tf('OpenSourceAria', item.sourceModuleName || item.sourceModule, item.sourceId))}">
                <i class="bx bx-link-external"></i><span>${esc(t('DetailOpenSource'))}</span>
            </button>
        </details>`;

    const detailHtml = (item) => {
        if (!item) {
            return `<div class="wcn-detail-empty">
                <i class="bx bx-select-multiple"></i>
                <p>${esc(t('SplitNoSelection'))}</p>
            </div>`;
        }
        const surface = global.WorkCenterNextTaskDetailResolver?.resolveTaskDetailSurface(item._fixture || item, {
            submittingActionCode: state.submittingActionCode || null
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
         * Parked waiting. Two independent facts, and either can be absent: WHO we are waiting on (rendered as
         * "waiting on X") and WHY, which the holder typed as a whole sentence and is therefore shown as written —
         * wrapping "Muhasebeden banka ekstresi bekleniyor" in "waiting on {0}" would read as nonsense.
         */
        const waitingText = item.waitingOn ? tf('WaitingOn', item.waitingOn) : item.waitingReason;
        const waitingNote = waitingText
            ? `<div class="wcn-parked wcn-parked-info" role="note"><i class="bx bx-time-five"></i><span>${esc(waitingText)}</span></div>`
            : '';
        // Snoozed (personal park) note.
        const snoozeNote = (item.snoozedUntil && item.snoozedUntil > data.todayIso)
            ? `<div class="wcn-parked wcn-parked-snooze" role="note"><i class="bx bx-moon"></i><span>${esc(tf('SnoozedUntil', item.snoozedUntil))}</span></div>`
            : '';
        const notices = surface.notices.map((notice) =>
            `<div class="wcn-parked wcn-parked-info" role="note"><i class="bx bx-info-circle"></i><span>${esc(t(notice.labelKey))}</span></div>`
        ).join('');
        // Personal actions (pin / snooze) — the thin overlay WorkCenter owns.
        const isSnoozed = item.snoozedUntil && item.snoozedUntil > data.todayIso;
        /*
         * The personal overlay (snooze) is NOT a task action: it changes what the viewer sees, not what the task
         * is. It lives in the rail beneath the personal note — one place, with the other personal thing — rather
         * than beside the engine actions where it would read as another transition.
         */
        const personal = (item.lifecycle === 'Done' || item.lifecycle === 'Cancelled') ? '' :
            `<div class="wcn-personal" role="group" aria-label="${esc(t('PersonalActionsLabel'))}">
                <button type="button" class="wcn-personal-btn${isSnoozed ? ' active' : ''}" data-wcn-snooze="${item.id}">
                    <i class="bx bx-moon"></i><span>${esc(t(isSnoozed ? 'Unsnooze' : 'Snooze'))}</span>
                </button>
            </div>`;
        const auditRows = item.activity.map((entry) => {
            const isComment = entry.kind === 'comment';
            const text = isComment ? entry.text
                : entry.eventKey === 'AuditActionStamp' ? tf('AuditActionStamp', entry.actionLabel)
                    : t(entry.eventKey);
            return `<li class="wcn-audit-item${isComment ? ' wcn-audit-comment' : ''}">
                <span class="wcn-audit-dot"><i class="bx ${isComment ? 'bx-message-rounded' : 'bx-git-commit'}"></i></span>
                <div class="wcn-audit-body">
                    <span class="wcn-audit-text">${esc(text)}</span>
                    <span class="wcn-audit-meta">${esc(entry.actor || t('CommentAuthorUnknown'))}${entry.atMs ? ` · ${esc(agoLabel(entry.atMs, item.provenance))}` : ''}</span>
                </div>
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
        const card = (inner) => inner
            ? `<section class="card backbone-preview-section wcn-detail-card ${
                inner.includes('wcn-empty-line') ? 'wcn-detail-card--slim p-3' : 'p-4'}">${inner}</section>`
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
        const activityCapped = hasCap(item, 'activity') && item.activity.length > ACTIVITY_VISIBLE_LIMIT;
        const activitySection = !hasCap(item, 'activity')
            ? ''
            : (!item.activity.length && !composer)
                ? `<div class="wcn-empty-line">
                    <i class="bx bx-message-square-detail" aria-hidden="true"></i>
                    <span class="wcn-empty-text">${esc(t('ActivityEmpty'))}</span>
                </div>`
                : `<div class="wcn-detail-section">
                    <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('ActivityLabel'))}</h6>
                    ${composer}
                    ${item.activity.length
                        ? (activityCapped
                            ? `<div class="wcn-scrollcap" data-wcn-scrollcap><ul class="wcn-audit">${auditRows}</ul></div>
                               <button type="button" class="btn btn-sm btn-label-secondary wcn-showall" data-wcn-showall>${esc(tf('ShowAllCount', item.activity.length))}</button>`
                            : `<ul class="wcn-audit">${auditRows}</ul>`)
                        : `<p class="wcn-block-hint">${esc(t('ActivityEmpty'))}</p>`}
                </div>`;

        // Command card — identity, status, actions and personal overlay. Everything
        // the viewer decides on lives here, above the read-only detail cards.
        const commandCard = `<section class="card backbone-preview-section wcn-detail-card wcn-detail-command p-4">
            <div class="wcn-detail-source">
                ${chip('module', 'bx-cube', item.sourceModule, sourceTitle(item))}
                ${item.sourceModuleId ? chip('secondary', 'bx-hash', item.sourceModuleId, item.sourceModuleName) : ''}
                ${chip('type', item.typeIcon, typeLabel(item))}
                <span class="wcn-badge wcn-badge-${STATUS_KIND[displayStatus(item)]}">${esc(statusLabel(item))}</span>
            </div>
            <div class="wcn-detail-chips">
                ${chip(SLA_KIND[item.slaState], 'bx-time-five', slaLabel(item))}
                ${priorityChip(item)}
                ${roleChip(item)}
            </div>
            ${renderLifecycleStepper(item)}
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
        const content = [
            // FIRST, always: "what is this?" is the question a detail page owes its reader before "what can you
            // do about it?" — which is what the page used to open with.
            card(renderSummary(item)),
            card(renderBusinessContext(item)),
            card(renderParentContext(item)),
            card(renderSubtasks(item)),
            card(renderDependencies(item)),
            card(renderChecklist(item)),
            card(renderTimesheet(item)),
            card(renderAttachments(item)),
            card(renderEvidence(item)),
            card(renderCompliance(item)),
            card(renderRelated(item)),
            card(activitySection)
        ].filter(Boolean).join('');

        /*
         * THE DECISION RAIL, in the order a decision is actually made: what can I do · where does this stand ·
         * what did I note · and, folded away, what the machine knows. Gates and dates used to be two cards and
         * are one now; the source-context card is the same data behind a <details>.
         */
        const rail = [
            card(renderActionRail(item)),
            card(renderStatusCard(item)),
            // Personal note sits UNDER the actions: it is something the viewer writes, not something the task says.
            card(`${renderNote(item)}${personal}`),
            card(`${renderDelegation(item)}${renderApprovalChain(item)}`),
            card(renderTechnicalDetails(item))
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
        </div>
        ${renderGuidance(item)}`;

        return `<div class="wcn-detail wcn-details-page">
            <div class="row g-4 wcn-detail-grid">
                <div class="col-12">${pageHeader}</div>
                <div class="col-12 wcn-detail-head">${commandCard}</div>
                <div class="col-12 col-lg-8 wcn-detail-content">${content}</div>
                <div class="col-12 col-lg-4 wcn-detail-rail">${rail}</div>
            </div>
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
                        aria-label="${esc(t('ReasonCancel'))}"></button>
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

        state.subtaskPanelSaving = true;
        render();

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
            toast(global.TasksApi.failureMessage(result), 'error');
            render();
            return;
        }

        state.subtaskPanelId = null;
        state.subtaskPanelRecord = null;
        toast(t('SubtaskSaved'));
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
                        aria-label="${esc(t('ReasonCancel'))}"></button>
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
                    <label class="form-label" for="wcnNewSubtaskDue">${esc(t('SubtaskFieldDue'))}</label>
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
            <div class="offcanvas-footer p-3 border-top">
                <button type="button" class="btn btn-primary w-100"
                        data-wcn-newsubtask-save="${esc(state.subtaskCreateParentId)}"${state.subtaskCreateSaving ? ' disabled' : ''}>
                    ${esc(t('SubtaskCreateSubmit'))}
                </button>
            </div>
        </div>`;
    };

    const openSubtaskCreatePanel = async (parentId) => {
        state.subtaskCreateParentId = parentId;
        state.subtaskCreateDraft = { priority: 'Medium' };
        render();
        showPanel('wcnSubtaskCreatePanel', () => {
            state.subtaskCreateParentId = null;
            state.subtaskCreateDraft = null;
        });

        // The picker is the SAME list the server will accept — reassign validates against it, so offering anyone
        // else here would build a form whose submit is refused.
        const people = await global.TasksApi.assignablePeople();
        state.assignablePeople = (people.ok && people.data) ? people.data : [];
        if (!people.ok) { console.warn('[WorkCenterNext] Assignable people could not be read; the picker is empty.'); }
        render();
    };

    const saveNewSubtask = async (parentId) => {
        const draft = state.subtaskCreateDraft || {};
        const title = String(draft.title || '').trim();
        if (!title) { toast(t('SubtaskTitleRequired'), 'error'); return; }

        state.subtaskCreateSaving = true;
        render();

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
        if (!result.ok) { toast(global.TasksApi.failureMessage(result), 'error'); render(); return; }

        state.subtaskCreateParentId = null;
        state.subtaskCreateDraft = null;
        toast(tf('ToastSubtaskAdded', title));
        await loadWorkItems();
    };

    /*
     * Cancelling a subtask, from its row. NOT deleting: a subtask is a task, its history stays, and BL-035's
     * "a cancelled subtask does not gate its parent" rule needs it to still exist. Permanent deletion, if it is
     * ever wanted, belongs on the full page where the whole record is in view.
     */
    const cancelSubtask = async (subtaskId, title) => {
        const confirmed = await confirmDestructive(tf('SubtaskCancelConfirm', title));
        if (!confirmed) { return; }

        const result = await global.TasksApi.transition(subtaskId, 'cancel', {});
        if (!result.ok) { toast(global.TasksApi.failureMessage(result), 'error'); return; }
        toast(tf('ToastSubtaskCancelled', title));
        await loadWorkItems();
    };

    /* MOD-0013 is the ONE confirm implementation in the app; a page-local dialog would be a second one. */
    const confirmDestructive = (message) => new Promise((resolve) => {
        if (typeof global.showConfirm === 'function') {
            global.showConfirm(message, () => resolve(true), { confirmButtonText: t('SubtaskCancelConfirmYes') });
            // showConfirm does not report dismissal, so a cancelled dialog simply never resolves true; resolve
            // false on the next tick if the callback has not fired.
            global.setTimeout(() => resolve(false), 0);
            return;
        }
        console.warn('[WorkCenterNext] window.showConfirm is unavailable; the destructive action was not offered.');
        resolve(false);
    });

    /* Shared offcanvas plumbing for both subtask panels. */
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

    const bulkBar = (visible) => {
        const selected = visible.filter((i) => state.tableSelected.has(i.id));
        if (!selected.length) { return ''; }
        const candidates = itemActions(selected[0]).filter((a) => a.bulk && !a.disabled);
        const acts = candidates.filter((candidate) => selected.every((item) => {
            const match = actionByKey(item, candidate.key);
            return !!(match && match.bulk && !match.disabled);
        }));
        const inner = acts.length
            ? acts.map((a) => `<button type="button" class="btn btn-sm btn-label-${a.kind}" data-wcn-bulk="${a.key}">${esc(t(a.labelKey))}</button>`).join('')
            : `<span class="wcn-bulk-note"><i class="bx bx-info-circle"></i>${esc(t('BulkNoCommonAction'))}</span>`;
        return `<div class="wcn-bulkbar" role="region" aria-label="${esc(t('BulkActionsLabel'))}">
            <span class="wcn-bulk-count">${esc(tf('BulkSelected', selected.length))}</span>
            <div class="wcn-bulk-actions">${inner}</div>
            <button type="button" class="btn btn-sm btn-text-secondary wcn-bulk-clear" data-wcn-bulk-clear>${esc(t('BulkClear'))}</button>
        </div>`;
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

    const renderKanban = () => {
        const items = activeItems();
        if (!items.length) { return emptyState(); }
        state.visibleOrder = [];
        let cols;
        if (SEGMENTS[state.tab]) {
            cols = [{ label: t(SEGMENT_KEY[state.segment]), items }];
        } else {
            const order = ['Pending', 'In Progress', 'Waiting', 'Done', 'Cancelled'];
            cols = order.map((st) => ({ label: t(STATUS_KEY[st]), items: items.filter((i) => i.status === st) })).filter((c) => c.items.length);
        }
        const colHtml = cols.map((col) => {
            const cards = col.items.slice().sort(bySla).map((item) => { state.visibleOrder.push(item.id); return kanbanCard(item); }).join('');
            return `<div class="wcn-kcol">
                <header class="wcn-kcol-head"><span>${esc(col.label)}</span><span class="wcn-kcol-count">${col.items.length}</span></header>
                <div class="wcn-kcol-body">${cards}</div>
            </div>`;
        }).join('');
        return `<div class="wcn-kanban">
            <div class="wcn-viewnote"><i class="bx bx-info-circle"></i><span>${esc(t('KanbanReadonly'))}</span></div>
            <div class="wcn-kboard">${colHtml}</div>
        </div>`;
    };

    // ── Calendar view (READ-ONLY deadline clustering, spec v3) — source due
    // (red) + personal plan (blue) on a month grid. No drag-reschedule. ────────
    const pad2 = (n) => String(n).padStart(2, '0');
    const renderCalendar = () => {
        const items = activeItems();
        state.visibleOrder = items.map((i) => i.id);
        const lang = (document.documentElement.lang || 'tr').slice(0, 2);
        const today = new Date(data.todayIso + 'T00:00:00');
        const year = today.getFullYear();
        const month = today.getMonth();
        const first = new Date(year, month, 1);
        const startDow = (first.getDay() + 6) % 7;               // Monday = 0
        const daysInMonth = new Date(year, month + 1, 0).getDate();
        let monthTitle;
        try { monthTitle = new Intl.DateTimeFormat(lang, { month: 'long', year: 'numeric' }).format(first); }
        catch (e) { monthTitle = (month + 1) + '/' + year; }
        const wd = [];
        for (let i = 0; i < 7; i++) {
            try { wd.push(new Intl.DateTimeFormat(lang, { weekday: 'short' }).format(new Date(2024, 0, 1 + i))); }
            catch (e) { wd.push(''); }
        }
        // Cluster items by day: source due + personal plan (distinct kinds).
        const byDay = {};
        items.forEach((i) => {
            if (i.dueAt) { (byDay[i.dueAt] = byDay[i.dueAt] || []).push({ item: i, kind: 'due' }); }
            if (i.plannedDate && i.plannedDate !== i.dueAt) { (byDay[i.plannedDate] = byDay[i.plannedDate] || []).push({ item: i, kind: 'plan' }); }
        });
        const cells = [];
        for (let b = 0; b < startDow; b++) { cells.push('<div class="wcn-cal-cell wcn-cal-empty"></div>'); }
        for (let d = 1; d <= daysInMonth; d++) {
            const iso = `${year}-${pad2(month + 1)}-${pad2(d)}`;
            const isToday = iso === data.todayIso;
            const entries = (byDay[iso] || []).map((e) =>
                `<div class="wcn-cal-item wcn-cal-${e.kind}" data-wcn-row="${e.item.id}" title="${esc(e.item.title)}" tabindex="0" role="button" aria-label="${esc(tf('TableOpenRow', e.item.title))}">
                    <span class="wcn-cal-dot"></span><span class="wcn-cal-item-text">${esc(e.item.title)}</span>
                </div>`).join('');
            cells.push(`<div class="wcn-cal-cell${isToday ? ' wcn-cal-today' : ''}">
                <span class="wcn-cal-day">${d}</span>${entries}
            </div>`);
        }
        return `<div class="wcn-calendar">
            <div class="wcn-cal-head">
                <span class="wcn-cal-month">${esc(monthTitle)}</span>
                <span class="wcn-cal-legend"><span class="wcn-cal-lg wcn-cal-due"></span>${esc(t('CalLegendDue'))} <span class="wcn-cal-lg wcn-cal-plan"></span>${esc(t('CalLegendPlan'))}</span>
            </div>
            <div class="wcn-cal-weekdays">${wd.map((w) => `<span>${esc(w)}</span>`).join('')}</div>
            <div class="wcn-cal-grid">${cells.join('')}</div>
        </div>`;
    };

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
        } else if (snap && snap.kind === 'ctl') {
            node = document.querySelector(`#wcnApp [${snap.attr}="${snap.val}"]`);
        }
        if (!node && state.selectedId) {
            node = document.querySelector(`#wcnApp .wcn-row[data-wcn-row="${state.selectedId}"], #wcnApp .wcn-tr[data-wcn-row="${state.selectedId}"]`);
        }
        if (node && typeof node.focus === 'function') { node.focus(); }
    };

    const renderAgenda = () => {
        const meetings = state.meetings.slice().sort((a, b) => a.start.localeCompare(b.start));
        const rows = meetings.length ? meetings.map((meeting) => `<article class="wcn-agenda-item">
            <div class="wcn-agenda-time"><strong>${esc(meeting.start)}</strong><span>${esc(meeting.end)}</span></div>
            <div class="wcn-agenda-body">
                <h6>${esc(meeting.title)}</h6>
                <span><i class="bx bx-map"></i>${esc(meeting.location || '—')}</span>
                <span><i class="bx bx-user"></i>${esc(meeting.owner || data.currentUser.name)}</span>
            </div>
            <button type="button" class="btn btn-xs btn-label-primary" data-wcn-meeting-followup="${meeting.id}"><i class="bx bx-task"></i>${esc(t('MeetingFollowup'))}</button>
        </article>`).join('') : `<div class="wcn-panel-empty">${esc(t('AgendaEmpty'))}</div>`;
        return `<div class="wcn-panel-inner">
            <div class="wcn-panel-head">
                <h6><i class="bx bx-calendar-event text-primary"></i>${esc(t('AgendaTitle'))}</h6>
                <div class="wcn-panel-head-actions">
                    <button type="button" class="btn btn-icon btn-sm btn-outline-primary" data-wcn-meeting-add title="${esc(t('AgendaAddMeeting'))}" aria-label="${esc(t('AgendaAddMeeting'))}"><i class="bx bx-plus"></i></button>
                    <button type="button" class="btn btn-icon btn-sm btn-text-secondary" data-wcn-toggle="agenda" aria-label="${esc(t('PanelClose'))}"><i class="bx bx-x"></i></button>
                </div>
            </div>
            <div class="wcn-agenda-list">${rows}</div>
        </div>`;
    };

    const renderNotes = () => {
        const notes = state.notes.filter((note) => !note.converted);
        const rows = notes.length ? notes.map((note) => `<article class="wcn-note-card">
            <p>${esc(note.text)}</p>
            <div class="wcn-note-card-foot">
                <span>${esc(t(note.ageKey || 'TimeToday'))}</span>
                <button type="button" class="btn btn-xs btn-label-warning" data-wcn-note-convert="${note.id}"><i class="bx bx-task"></i>${esc(t('NotesConvertTask'))}</button>
            </div>
        </article>`).join('') : `<div class="wcn-panel-empty">${esc(t('NotesEmpty'))}</div>`;
        return `<div class="wcn-panel-inner">
            <div class="wcn-panel-head">
                <h6><i class="bx bx-note text-warning"></i>${esc(t('NotesPanelTitle'))}</h6>
                <button type="button" class="btn btn-icon btn-sm btn-text-secondary" data-wcn-toggle="notes" aria-label="${esc(t('PanelClose'))}"><i class="bx bx-x"></i></button>
            </div>
            <div class="wcn-notes-list">${rows}</div>
            <div class="wcn-notes-composer">
                <textarea class="form-control form-control-sm" rows="2" data-wcn-global-note-input placeholder="${esc(t('NotesAddPlaceholder'))}"></textarea>
                <button type="button" class="btn btn-sm btn-primary" data-wcn-global-note-add>${esc(t('NotesAdd'))}</button>
            </div>
        </div>`;
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
        const sidePanel = state.agendaOpen ? `<aside id="wcnSidePanel" class="wcn-sidepanel" aria-label="${esc(t('AgendaTitle'))}">${renderAgenda()}</aside>`
                        : state.notesOpen ? `<aside id="wcnSidePanel" class="wcn-sidepanel" aria-label="${esc(t('NotesPanelTitle'))}">${renderNotes()}</aside>`
                        : '';

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

    const render = () => {
        try {
            renderUnsafe();
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
                // Resuming closes any wait/snooze reason so the item leaves the
                // "Bekleyen" segment (segmentFor keys off waitingOn/snoozedUntil).
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
        inquire: ({ expectedVersion, reason }) => ({ expectedVersion, reason }),
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

    const buildTransitionBody = (actionCode, parts) =>
        (TRANSITION_BODIES[actionCode] || TRANSITION_BODIES.__default)(parts);

    const submitRealTransition = async (item, action, reason, assigneeUserId) => {
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
            buildTransitionBody(action.code, { expectedVersion, reason, assigneeUserId }));

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
    const afterPhase2Write = async (result, successKey, successArg) => {
        if (result.ok) {
            await loadWorkItems();
            render();
            toast(successArg ? tf(successKey, successArg) : t(successKey));
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

    const completeSubtask = async (subtaskId) => {
        // The subtask is its own row in state, so it carries its own concurrency token.
        const subtask = itemById(subtaskId);
        if (!isRealTaskItem(subtask)) {
            console.warn(`[WorkCenterNext] Subtask completion ignored: ${subtaskId} is not an engine task, `
                + 'or is not present as its own row (it may not be assigned to you).');
            return;
        }

        const result = await global.TasksApi.transition(subtaskId, 'complete', {
            expectedVersion: Number(subtask.concurrency?.token ?? 0)
        });
        await afterPhase2Write(result, 'ToastActionApplied', subtask.title);
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
        await afterPhase2Write(result, 'ToastCommentPostedReal');
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

        await afterPhase2Write(await global.TasksApi.create(payload), 'ToastSubtaskAdded', text);
    };

    const applyAction = (item, action, reason, assigneeUserId) => {
        if (isRealTaskItem(item)) { submitRealTransition(item, action, reason, assigneeUserId); return; }

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
        global.Swal.fire({
            title: label,
            html: '<input id="wcnPlanDate" class="form-control" autocomplete="off">',
            showCancelButton: true, confirmButtonText: t('PlanConfirm'), cancelButtonText: t('ReasonCancel'),
            didOpen: () => {
                const input = document.getElementById('wcnPlanDate');
                // Re-planning opens the picker seeded with the EXISTING plan, so moving a date is an edit of it
                // rather than starting blank; falling back to the source due date only when there is no plan yet.
                const seed = item.plannedDate || item.dueAt;
                if (global.flatpickr) {
                    global.flatpickr(input, { dateFormat: 'Y-m-d', defaultDate: seed || undefined, disableMobile: true });
                } else {
                    input.type = 'date';
                    if (seed) { input.value = seed; }
                }
            },
            preConfirm: () => {
                const v = document.getElementById('wcnPlanDate').value;
                if (!v) { global.Swal.showValidationMessage(t('PlanDateLabel')); return false; }
                return v;
            }
        }).then((res) => {
            if (!res.isConfirmed || !res.value) { return null; }
            return real ? submitPlan(item, res.value) : applyPlan(item, res.value, label);
        }).catch(reportSwalFailure);
    };

    // Swal's own promise chain runs well after the click that opened it, outside onClick's try/catch — so a
    // failure inside submitPlan needs its OWN net, the same one onClick's own catch gives every other write.
    const reportSwalFailure = (error) => {
        console.error('WorkCenterNext date picker failed.', error);
        toast(t('ErrorTitle'), 'error');
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
        global.Swal.fire({
            title: label,
            html: '<input id="wcnMeetWhen" class="form-control" autocomplete="off">',
            showCancelButton: true, confirmButtonText: t('PlanConfirm'), cancelButtonText: t('ReasonCancel'),
            didOpen: () => {
                const input = document.getElementById('wcnMeetWhen');
                const seed = item.dueAt || data.todayIso;
                if (global.flatpickr) {
                    global.flatpickr(input, { enableTime: true, dateFormat: 'Y-m-d H:i', defaultDate: seed, disableMobile: true });
                } else {
                    input.type = 'datetime-local';
                }
            },
            preConfirm: () => {
                const v = document.getElementById('wcnMeetWhen').value;
                if (!v) { global.Swal.showValidationMessage(t('PlanDateLabel')); return false; }
                return v.replace('T', ' ');
            }
        }).then((res) => { if (res.isConfirmed && res.value) { applyReviewMeeting(item, res.value, label); } });
    };

    // Log time — manual minutes entry into the timesheet (task only).
    const openLogTime = (item, action) => {
        const label = actionLabel(action);
        if (!global.Swal) { return; }
        global.Swal.fire({
            title: label, input: 'number', inputLabel: t('LogTimeLabel'),
            inputPlaceholder: t('LogTimePlaceholder'), inputAttributes: { min: '1', step: '1' },
            showCancelButton: true, confirmButtonText: t('LogTimeConfirm'), cancelButtonText: t('ReasonCancel'),
            preConfirm: (v) => { const m = parseInt(v, 10); if (!m || m <= 0) { global.Swal.showValidationMessage(t('LogTimeLabel')); return false; } return m; }
        }).then((res) => {
            if (res.isConfirmed && res.value) {
                const mins = parseInt(res.value, 10);
                item.timesheet = item.timesheet || { running: false, startedAt: null, loggedMinutes: 0 };
                item.timesheet.loggedMinutes += mins;
                item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: label, atMs: data.referenceDate(item.provenance) });
                render();
                toast(tf('ToastTimeLogged', formatMinutes(mins)));
            }
        });
    };

    // Snooze is a personal filter signal. It never changes the canonical
    // lifecycle, normalized status, tab or lifecycle segment.
    const toggleSnooze = (item) => {
        if (!item) { return; }
        if (item.snoozedUntil && item.snoozedUntil > data.todayIso) {
            item.snoozedUntil = null;
            item.personal.snoozedUntil = null;
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Unsnooze'), atMs: data.referenceDate(item.provenance) });
            render();
            toast(tf('ToastUnsnoozed', item.title));
            return;
        }
        const apply = (dateStr) => {
            item.snoozedUntil = dateStr;
            item.personal.snoozedUntil = dateStr;
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Snooze'), atMs: data.referenceDate(item.provenance) });
            const prevOrder = state.visibleOrder.slice();
            const prevIdx = prevOrder.indexOf(item.id);
            if (state.view === 'split') { state.selectedId = prevOrder[prevIdx + 1] || prevOrder[prevIdx - 1] || null; }
            render();
            toast(tf('ToastSnoozed', item.title, dateStr));
        };
        if (!global.Swal) { apply(data.todayIso); return; }
        global.Swal.fire({
            title: t('Snooze'),
            html: '<input id="wcnSnoozeDate" class="form-control" autocomplete="off">',
            showCancelButton: true, confirmButtonText: t('SnoozeConfirm'), cancelButtonText: t('ReasonCancel'),
            didOpen: () => {
                const input = document.getElementById('wcnSnoozeDate');
                if (global.flatpickr) { global.flatpickr(input, { dateFormat: 'Y-m-d', minDate: data.todayIso, disableMobile: true }); }
                else { input.type = 'date'; input.min = data.todayIso; }
            },
            preConfirm: () => {
                const v = document.getElementById('wcnSnoozeDate').value;
                if (!v || v <= data.todayIso) { global.Swal.showValidationMessage(t('SnoozeFuture')); return false; }
                return v;
            }
        }).then((res) => { if (res.isConfirmed && res.value) { apply(res.value); } });
    };

    // ── "+ Yeni" — WorkCenter owns only self-tasks; module items are created in
    // their source (deep-link). No generic cross-module authoring here (spec v3). ─
    const openNew = () => {
        if (!global.Swal) { return; }
        global.Swal.fire({
            title: t('NewButton'),
            html: `<div class="wcn-new-menu">
                <button type="button" class="wcn-new-opt" id="wcnNewSelf"><i class="bx bx-user-check"></i><div><strong>${esc(t('NewSelfTask'))}</strong><span>${esc(t('NewSelfTaskDesc'))}</span></div></button>
                <button type="button" class="wcn-new-opt" id="wcnNewSource"><i class="bx bx-cube"></i><div><strong>${esc(t('NewInSource'))}</strong><span>${esc(t('NewInSourceDesc'))}</span></div></button>
            </div>`,
            showConfirmButton: false, showCancelButton: true, cancelButtonText: t('ReasonCancel'),
            didOpen: () => {
                document.getElementById('wcnNewSelf').onclick = () => { global.Swal.close(); openSelfTask(); };
                document.getElementById('wcnNewSource').onclick = () => { global.Swal.close(); openCreateInSource(); };
            }
        });
    };

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
        global.Swal.fire({
            title: t('NewInSource'), input: 'select', inputOptions: opts, inputPlaceholder: t('NewPickModule'),
            // The button names CREATING, not opening: nothing is opened here any more (see below), and the old
            // 'NewOpenSource' label promised an act this dialog no longer performs.
            showCancelButton: true, confirmButtonText: t('NewCreateInSource'), cancelButtonText: t('ReasonCancel')
        }).then((res) => {
            if (res.isConfirmed && res.value) {
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
                toast(tf('ToastCreateInSource', res.value), 'info');
            }
        });
    };

    const openMeetingForm = () => {
        if (!global.Swal) { return; }
        global.Swal.fire({
            title: t('MeetingNewTitle'),
            html: `<label class="form-label" for="wcnMeetingTitle">${esc(t('MeetingTitleLabel'))}</label>
                <input id="wcnMeetingTitle" class="form-control mb-2">
                <div class="row g-2"><div class="col"><label class="form-label" for="wcnMeetingStart">${esc(t('MeetingStartLabel'))}</label><input id="wcnMeetingStart" type="time" class="form-control" value="09:00"></div>
                <div class="col"><label class="form-label" for="wcnMeetingEnd">${esc(t('MeetingEndLabel'))}</label><input id="wcnMeetingEnd" type="time" class="form-control" value="09:30"></div></div>
                <label class="form-label mt-2" for="wcnMeetingLocation">${esc(t('MeetingLocationLabel'))}</label><input id="wcnMeetingLocation" class="form-control">`,
            showCancelButton: true, confirmButtonText: t('MeetingAdd'), cancelButtonText: t('ReasonCancel'),
            preConfirm: () => {
                const title = document.getElementById('wcnMeetingTitle').value.trim();
                const start = document.getElementById('wcnMeetingStart').value;
                const end = document.getElementById('wcnMeetingEnd').value;
                if (!title || !start || !end || end <= start) { global.Swal.showValidationMessage(t('MeetingValidation')); return false; }
                return { title, start, end, location: document.getElementById('wcnMeetingLocation').value.trim() };
            }
        }).then((res) => {
            if (!res.isConfirmed || !res.value) { return; }
            state.meetings.push({ id: `MTG-${Date.now()}`, ...res.value, owner: data.currentUser.name });
            render(); toast(t('MeetingAdded'));
        });
    };

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

    // "+ Yeni → Hızlı not": a light capture modal, not the heavy task form. The
    // in-panel input (addGlobalNote) only exists when the notes panel is open, so
    // the menu needs its own lightweight entry point. No backend — pushes to the
    // existing personal-notes layer (state.notes).
    const openQuickNote = () => {
        if (!global.Swal) { return; }
        global.Swal.fire({
            title: t('NewNote'),
            input: 'textarea',
            inputPlaceholder: t('NotePlaceholder'),
            inputAttributes: { 'aria-label': t('NewNote') },
            showCancelButton: true, confirmButtonText: t('NewCreate'), cancelButtonText: t('ReasonCancel'),
            preConfirm: (val) => {
                const text = (val || '').trim();
                if (!text) { global.Swal.showValidationMessage(t('NotePlaceholder')); return false; }
                return text;
            }
        }).then((res) => {
            if (!res.isConfirmed || !res.value) { return; }
            state.notes.unshift({ id: `NOTE-${Date.now()}`, text: res.value, ageKey: 'TimeToday', converted: false });
            render(); toast(t('NoteAdded'));
        });
    };

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
            let people = [];
            if (needsAssignee) {
                const res = await global.TasksApi.assignablePeople();
                people = (res.ok && res.data) ? res.data : [];
                if (!people.length) {
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
                  + `<select id="wcnReassignAssignee" class="form-select mb-3">`
                  + `<option value="">${esc(t('ReassignAssigneePlaceholder'))}</option>${options}</select>`
                : '';

            global.Swal.fire({
                title: actionLabel(action),
                html: assigneeField
                    + `<label class="form-label d-block text-start" for="wcnReasonText">${esc(t('ReasonLabel'))}</label>`
                    + `<textarea id="wcnReasonText" class="form-control" rows="3" `
                    + `placeholder="${esc(t('ReasonPlaceholder'))}"></textarea>`,
                showCancelButton: true,
                confirmButtonText: t('ReasonConfirm'),
                cancelButtonText: t('ReasonCancel'),
                preConfirm: () => {
                    const reason = String(document.getElementById('wcnReasonText')?.value || '').trim();
                    if (!reason) { global.Swal.showValidationMessage(t('ReasonRequired')); return false; }

                    if (!needsAssignee) { return { reason }; }

                    const assigneeUserId = String(document.getElementById('wcnReassignAssignee')?.value || '').trim();
                    // Cannot be confirmed without a person: the server requires it and a silent 400 helps nobody.
                    if (!assigneeUserId) { global.Swal.showValidationMessage(t('ReassignAssigneeRequired')); return false; }
                    return { reason, assigneeUserId };
                }
            }).then((res) => {
                if (res.isConfirmed && res.value) {
                    applyAction(item, action, res.value.reason, res.value.assigneeUserId);
                }
            });
            return;
        }

        // High-consequence action (approve/sign-off/complete): explicit confirm so
        // an accidental click — or the `a` keyboard shortcut on a six-figure
        // approval — can't fire irreversibly (spec v2 §6, P1 fix).
        if (action.confirm) {
            if (!global.Swal) { return; }
            const body = item.delegator
                ? tf('ConfirmBodyOnBehalf', item.title, item.delegator)
                : tf('ConfirmBody', item.title);
            global.Swal.fire({
                title: actionLabel(action),
                html: `<div class="wcn-confirm-body">${esc(body)}</div>`,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: t('ConfirmProceed'),
                cancelButtonText: t('ReasonCancel')
            }).then((res) => { if (res.isConfirmed) { applyAction(item, action); } });
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
            if (!global.Swal) { return; }
            global.Swal.fire({
                title: data.resolveLabel(action.label),
                input: 'textarea',
                inputLabel: t('ReasonLabel'),
                inputPlaceholder: t('ReasonPlaceholder'),
                showCancelButton: true,
                confirmButtonText: t('ReasonConfirm'),
                cancelButtonText: t('ReasonCancel'),
                preConfirm: (value) => {
                    const reason = String(value || '').trim();
                    if (!reason) { global.Swal.showValidationMessage(t('ReasonRequired')); return false; }
                    return reason;
                }
            }).then((result) => {
                if (result.isConfirmed && result.value) { executeTriggerAction(trigger, action, result.value); }
            });
            return;
        }
        executeTriggerAction(trigger, action);
    };

    // Bulk apply with a partial-failure model (spec v2 §6): some items fail (mock:
    // a stale/changed source record) — succeeded ones clear, failed ones stay
    // selected and flagged so the user can retry, never a silent all-or-nothing.
    const runBulk = (selected, actionKey, label) => {
        const failed = [];
        let ok = 0;
        selected.forEach((item) => {
            const action = actionByKey(item, actionKey);
            if (!action || !action.bulk || action.disabled) { failed.push(item); return; }
            if (item.bulkConflict) { failed.push(item); return; }
            /*
             * Symmetry with the single-action path (applyAction): a real item NEVER goes through
             * applyTransition, which only simulates. Without this, the moment a provider ships
             * providerCode:"tasks" together with supportsBulk:true, a bulk approve would change the screen and
             * leave the database untouched — the same defect already fixed once for single actions, silently
             * reintroduced. Bulk writes against the real engine are a separate slice; until then a real item is
             * reported as failed rather than faked.
             */
            if (isRealTaskItem(item)) {
                console.warn(`[WorkCenterNext] Bulk "${actionKey}" skipped for real item ${item.id}: bulk `
                    + 'transitions are not wired to the engine, and simulating one would show a change that '
                    + 'was never persisted.');
                failed.push(item);
                return;
            }
            applyTransition(item, action.key);
            markSeen(item);
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: actionLabel(action), atMs: data.referenceDate(item.provenance) });
            ok += 1;
        });
        state.tableSelected.clear();
        state.bulkFailedIds = new Set(failed.map((i) => i.id));
        failed.forEach((i) => state.tableSelected.add(i.id));
        render();
        if (failed.length && global.Swal) {
            global.Swal.fire({
                icon: failed.length === selected.length ? 'error' : 'warning',
                title: t('BulkResultTitle'),
                html: `<div class="wcn-confirm-body">${esc(tf('BulkResult', selected.length, ok, failed.length))}</div>`,
                confirmButtonText: t('ReasonConfirm')
            });
        } else {
            toast(tf('ToastBulk', ok));
        }
    };

    // Brief progress pass before applying, so a large batch reads as work being
    // done rather than an instant, opaque state jump.
    const runBulkWithProgress = (selected, actionKey, label) => {
        if (!global.Swal) { runBulk(selected, actionKey, label); return; }
        let pct = 0;
        global.Swal.fire({
            title: label,
            html: `<div class="wcn-bulk-progress"><div class="wcn-bulk-progress-bar" id="wcnBulkBar"></div></div>` +
                  `<div class="wcn-bulk-progress-text" id="wcnBulkPct">0%</div>`,
            showConfirmButton: false, allowOutsideClick: false, allowEscapeKey: false,
            didOpen: () => {
                const bar = document.getElementById('wcnBulkBar');
                const txt = document.getElementById('wcnBulkPct');
                const step = () => {
                    pct = Math.min(100, pct + 20);
                    if (bar) { bar.className = `wcn-bulk-progress-bar wcn-progress-${pct}`; }
                    if (txt) { txt.textContent = pct + '%'; }
                    if (pct >= 100) { global.setTimeout(() => { global.Swal.close(); runBulk(selected, actionKey, label); }, 150); }
                    else { global.setTimeout(step, 90); }
                };
                step();
            }
        });
    };

    const performBulk = (actionKey) => {
        const selected = state.items.filter((i) => state.tableSelected.has(i.id) && !i.dismissed);
        if (!selected.length) { return; }
        const sample = actionByKey(selected[0], actionKey);
        if (!sample || !sample.bulk || sample.disabled || !selected.every((item) => {
            const action = actionByKey(item, actionKey);
            return !!(action && action.bulk && !action.disabled);
        })) { toast(t('BulkNoCommonAction'), 'warning'); return; }
        const label = sample ? actionLabel(sample) : '';
        if (sample.reason && global.Swal) {
            global.Swal.fire({
                title: label, input: 'textarea', inputLabel: t('ReasonLabel'), inputPlaceholder: t('ReasonPlaceholder'),
                showCancelButton: true, confirmButtonText: t('ReasonConfirm'), cancelButtonText: t('ReasonCancel'),
                preConfirm: (value) => {
                    const reason = String(value || '').trim();
                    if (!reason) { global.Swal.showValidationMessage(t('ReasonRequired')); return false; }
                    return reason;
                }
            }).then((res) => { if (res.isConfirmed && res.value) { runBulkWithProgress(selected, actionKey, label); } });
            return;
        }
        // Confirm before a high-consequence batch — approving 42 payments at once
        // is far riskier than a single click (spec v2 §6, P1 fix).
        if (sample && sample.confirm && global.Swal) {
            global.Swal.fire({
                title: label,
                html: `<div class="wcn-confirm-body">${esc(tf('ConfirmBulkBody', selected.length))}</div>`,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: t('ConfirmProceed'),
                cancelButtonText: t('ReasonCancel')
            }).then((res) => { if (res.isConfirmed) { runBulkWithProgress(selected, actionKey, label); } });
            return;
        }
        runBulkWithProgress(selected, actionKey, label);
    };

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

    const onKeydown = (event) => {
        // Escape in the search box clears the current query (before the typing guard).
        if (event.key === 'Escape' && event.target.matches && event.target.matches('[data-wcn-search]')) {
            if (state.search) { event.preventDefault(); state.search = ''; render(); }
            return;
        }
        if (isTyping(event.target) || event.metaKey || event.ctrlKey || event.altKey) { return; }
        const key = event.key.toLowerCase();
        const activeTab = event.target.closest && event.target.closest('[role="tab"][data-wcn-tab]');
        if (activeTab && (key === 'arrowleft' || key === 'arrowright' || key === 'home' || key === 'end')) {
            const tabs = Array.from(document.querySelectorAll('#wcnApp [role="tab"][data-wcn-tab]'));
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

        if (event.target.closest('[data-wcn-meeting-add]')) { openMeetingForm(); return; }
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
            else if (kind === 'note') { openQuickNote(); }
            else if (kind === 'meeting') { openMeetingForm(); }
            else if (kind === 'source') { openCreateInSource(); }
            else { openNew(); }
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
            toggleSnooze(itemById(snoozeEl.getAttribute('data-wcn-snooze')));
            return;
        }

        // ── Depth-block interactions (Faz 2) ──────────────────────────────────
        // All three go to the ENGINE and then re-read the projection. Nothing is applied optimistically: the
        // server decides, and the refreshed projection is the only source of the new state.
        const checkItemEl = event.target.closest('[data-wcn-check-item]');
        if (checkItemEl) {
            const [taskId, itemCode] = checkItemEl.getAttribute('data-wcn-check-item').split(':');
            toggleChecklistItem(taskId, itemCode, checkItemEl.getAttribute('aria-pressed') !== 'true');
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
        const subAddInlineEl = event.target.closest('[data-wcn-subtask-add-inline]');
        if (subAddInlineEl) {
            const input = document.querySelector('#wcnApp [data-wcn-subtask-input]');
            if (input) { input.focus(); }
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
        const subAddEl = event.target.closest('[data-wcn-subtask-add]');
        if (subAddEl) {
            // Read the input from the card the button belongs to, not from the document: one card per page today,
            // but a page-wide lookup silently binds to whichever card happens to come first.
            const card = subAddEl.closest('.wcn-subtask-add') || subAddEl.closest('.wcn-detail-card');
            const input = (card || document).querySelector('[data-wcn-subtask-input]');
            // AWAITED on purpose: an un-awaited async call rejects into nothing, and that is precisely how this
            // action failed in total silence when the host page had not loaded TasksApi.
            await addSubtask(subAddEl.getAttribute('data-wcn-subtask-add'), input ? input.value : '');
            return;
        }
        const noteSaveEl = event.target.closest('[data-wcn-note-save]');
        if (noteSaveEl) {
            const it = itemById(noteSaveEl.getAttribute('data-wcn-note-save'));
            const inp = document.querySelector('#wcnApp [data-wcn-note-input]');
            if (it && inp) { it.note = inp.value.trim() || null; render(); toast(t('ToastNoteSaved')); }
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

        const bulkEl = event.target.closest('[data-wcn-bulk]');
        if (bulkEl) { performBulk(bulkEl.getAttribute('data-wcn-bulk')); return; }
        if (event.target.closest('[data-wcn-bulk-clear]')) { state.tableSelected.clear(); render(); return; }

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
        const checkAll = event.target.closest('[data-wcn-check-all]');
        if (checkAll) {
            const on = checkAll.checked;
            state.visibleOrder.forEach((id) => { if (on) { state.tableSelected.add(id); } else { state.tableSelected.delete(id); } });
            render();
            return;
        }
        const checkEl = event.target.closest('[data-wcn-check]');
        if (checkEl) {
            const id = checkEl.getAttribute('data-wcn-check');
            if (checkEl.checked) { state.tableSelected.add(id); } else { state.tableSelected.delete(id); }
            render();
        }
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

    const loadWorkItems = async () => {
        state.loadState = 'loading';
        state.loadError = null;
        render();

        if (data.showcaseFixturesEnabled && data.showcaseFixturesEnabled()) {
            state.items = data.buildItems();
            state.triggers = data.buildTriggers ? data.buildTriggers() : [];
            state.meetings = data.buildMeetings ? data.buildMeetings() : [];
            state.notes = data.buildNotes ? data.buildNotes() : [];
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
