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
    const PRIORITY_KIND = { high: 'danger', medium: 'warning', low: 'secondary' };
    const PRIORITY_KEY = { high: 'PriorityHigh', medium: 'PriorityMedium', low: 'PriorityLow' };
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
        scope: 'mine',           // 'mine' | 'all' | <delegator name> (N-way delegation)
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
        priority: ['all', 'high', 'medium', 'low'],
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
        if (scope && (scope === 'mine' || scope === 'all' || data.delegators.some((d) => d.name === scope))) { state.scope = scope; }
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

    const chip = (kind, icon, text, title) =>
        `<span class="wcn-chip wcn-chip-${kind}"${title ? ` title="${esc(title)}"` : ''}>` +
        `<i class="bx ${icon}"></i><span>${esc(text)}</span></span>`;

    const typeLabel = (item) => t(TYPE_KEY[item.itemType] || item.itemType);
    // normalizedStatus is already resolved by the provider/aggregation projection.
    const displayStatus = (item) => item.status;
    const statusLabel = (item) => { const s = displayStatus(item); return t(STATUS_KEY[s] || s); };
    const priorityLabel = (item) => t(PRIORITY_KEY[item.priority] || item.priority);

    const slaLabel = (item) => {
        const d = item.slaDiffDays;
        switch (item.slaState) {
            case 'overdue': return tf('SlaOverdueByDays', Math.abs(d));
            case 'due-soon':
                if (d === 0) { return t('SlaDueToday'); }
                if (d === 1) { return t('SlaDueTomorrow'); }
                return tf('SlaDueInDays', d);
            case 'on-track': return tf('SlaDueInDays', d);
            default: return t('SlaNoSla');
        }
    };

    const agoLabel = (ago) => {
        if (ago === 0) { return t('TimeToday'); }
        if (ago === 1) { return t('TimeYesterday'); }
        return tf('TimeDaysAgo', ago);
    };

    // actions[] is the single effective command projection. The browser never
    // derives eligibility from lifecycle, permission, blockers or system state.
    const itemActions = (item) => data.getActions(item);
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
            : state.scope === 'mine' ? !item.delegator
                : item.delegator === state.scope;
    const isTerminal = (item) => ['Done', 'Cancelled'].includes(item.normalizedStatus)
        || item.lifecycle === 'Done' || item.lifecycle === 'Cancelled';
    const inTab = (item, tab) => item.catalogVisible !== false && !item.dismissed && itemInScope(item)
        && (tab === 'history' ? isTerminal(item) : item.tab === tab && !isTerminal(item));
    // Tab counters ignore in-tab filters so they reflect the true load per scope.
    const tabCount = (tab) => state.items.filter((item) => inTab(item, tab)).length
        + (tab === 'inbox' ? state.triggers.length : 0);

    // Items in the current tab (before segment/chip filters) — used for segment
    // and chip counts so those reflect what the tab really holds.
    const tabItems = () => state.items.filter((item) => inTab(item, state.tab));
    const segmentCount = (seg) => tabItems().filter((i) => data.segmentFor(i) === seg).length;
    const typeCount = (ty) => tabItems().filter((i) => i.itemType === ty).length
        + (state.tab === 'inbox' && ty === 'meetingInvite' ? state.triggers.length : 0);
    const signalCount = (sig) => tabItems().filter((i) => SIGNAL_TEST[sig](i)).length;

    // Advanced filters shared by list + kanban + calendar (priority, mode, group,
    // module, search) — everything except the tab-specific segment filter.
    const passesFilters = (item) => {
        if (state.typeFilter.size && !state.typeFilter.has(item.itemType)) { return false; }
        if (state.signalFilter.size) {
            for (const sig of state.signalFilter) { if (!SIGNAL_TEST[sig](item)) { return false; } }
        }
        if (state.moduleFilter.length && !state.moduleFilter.includes(item.sourceModule)) { return false; }
        if (state.priorityFilter !== 'all' && item.priority !== state.priorityFilter) { return false; }
        if (state.modeFilter !== 'all' && item.assignmentMode !== state.modeFilter) { return false; }
        if (state.slaFilter.length && !state.slaFilter.includes(item.slaState)) { return false; }
        if (state.pinnedFilter && !item.pinned) { return false; }
        if (state.tab === 'havuz' && state.group !== 'all' && item.group !== state.group) { return false; }
        const q = state.search.trim().toLowerCase();   // ignore leading/trailing space
        if (q) {
            const hay = (item.title + ' ' + item.summary + ' ' + item.sourceModule + ' ' + item.sourceId + ' ' + item.requester).toLowerCase();
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
        const query = state.search.trim().toLowerCase();
        return state.triggers.filter((trigger) => {
            const provider = trigger.source?.providerCode || '';
            if (state.moduleFilter.length && !state.moduleFilter.includes(provider)) { return false; }
            const title = data.resolveLabel(trigger.title);
            const summary = data.resolveLabel(trigger.summary);
            return !query || `${title} ${summary} ${provider}`.toLowerCase().includes(query);
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
                : tf('OnBehalfShort', state.scope);
        const scopeIcon = (k) => k === 'mine' ? 'bx-user' : k === 'all' ? 'bx-layer' : 'bx-user-voice';
        const ownBadge = (state.scope !== 'mine' && urgent)
            ? `<span class="wcn-own-urgent" title="${esc(t('OwnUrgentTip'))}">${urgent}</span>` : '';
        const scopeItem = (key, label, sub) =>
            `<li><button type="button" class="dropdown-item wcn-dd-item${state.scope === key ? ' active' : ''}" data-wcn-scope="${esc(key)}">
                <i class="bx ${scopeIcon(key)}"></i><span>${esc(label)}</span>${sub ? `<small class="wcn-dd-sub">${esc(sub)}</small>` : ''}
            </button></li>`;
        const delegatorItems = data.delegators.map((d) => scopeItem(d.name, tf('OnBehalfShort', d.name), d.title)).join('');
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
                        ${delegatorItems}
                        <li><hr class="dropdown-divider"></li>
                        ${scopeItem('all', t('ScopeAll'), t('ScopeAllSub'))}
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
    const buildGroupSelector = () => {
        if (state.tab !== 'havuz') { return ''; }
        const groups = [];
        state.items.forEach((i) => { if (i.group && groups.indexOf(i.group) < 0) { groups.push(i.group); } });
        if (!groups.length) { return ''; }
        const btn = (key, label) =>
            `<button type="button" class="wcn-seg${state.group === key ? ' active' : ''}" data-wcn-group="${esc(key)}"><span>${esc(label)}</span></button>`;
        return `<div class="wcn-segments" role="group" aria-label="${esc(t('GroupLabel'))}">
            ${btn('all', t('GroupAll'))}${groups.map((g) => btn(g, g)).join('')}
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
            `<i class="bx bx-collection"></i><span>${esc(t('ChipAll'))}</span><span class="wcn-fchip-count">${tabItems().length + state.triggers.length}</span></button>`;
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
            <div class="filter-chip">
                <select class="form-select form-select-sm select2 wcn-select" data-wcn-filter="priority" data-placeholder="${esc(t('FilterAllPriorities'))}" aria-label="${esc(t('FilterPriority'))}">
                    <option value=""></option>
                    ${['high', 'medium', 'low'].map((p) => `<option value="${p}"${draft.priority === p ? ' selected' : ''}>${esc(t(PRIORITY_KEY[p]))}</option>`).join('')}
                </select>
            </div>
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
        chip(PRIORITY_KIND[item.priority], 'bx-flag', priorityLabel(item)),
        isBlocked(item) ? chip('danger', 'bx-lock-alt', t('BlockedLabel'), t(item.blockedState.reasonKey || 'BlockedBanner')) : '',
        item.waitingOn ? chip('warning', 'bx-time-five', tf('WaitingOn', item.waitingOn)) : '',
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
        const primary = actions.find((action) => action.primary && !action.disabled)
            || actions.find((action) => !action.disabled)
            || null;
        const overflow = actions.filter((action) => !primary || action.key !== primary.key);
        const interactionLocked = state.submittingItemId === item.id;
        const primaryButton = primary
            ? `<button type="button" class="btn btn-sm btn-label-${primary.kind} wcn-inbox-action-primary" data-wcn-action="${primary.key}" data-wcn-id="${item.id}"${interactionLocked ? ' disabled' : ''}><i class="bx ${inboxActionIcon(primary)} me-1"></i>${esc(actionLabel(primary))}</button>`
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
        const primary = actions.find((action) => action.primary && !action.disabled)
            || actions.find((action) => !action.disabled)
            || null;
        const rest = actions.filter((action) => !primary || action.key !== primary.key);
        const interactionLocked = state.submittingItemId === item.id;
        // Stale source → refresh is the primary; real actions come back after it clears.
        const primaryButton = needsSourceRecovery(item)
            ? refreshSourceBtn(item)
            : (primary
                ? `<button type="button" class="btn btn-sm btn-label-${primary.kind} wcn-inbox-action-primary" data-wcn-action="${primary.key}" data-wcn-id="${item.id}"${interactionLocked ? ' disabled' : ''}><i class="bx ${inboxActionIcon(primary)} me-1"></i>${esc(actionLabel(primary))}</button>`
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
        return `<article class="card wcn-splitcard wcn-splitcard-p-${PRIORITY_KIND[item.priority]}${selected ? ' selected' : ''}${item.isUnread ? ' unread' : ''}" data-wcn-row="${item.id}" tabindex="0" role="button" draggable="true" aria-label="${esc(tf('TableOpenRow', item.title))}">
            <div class="wcn-splitcard-head">
                <span class="wcn-inbox-type wcn-inbox-type-${typeKind}">${esc(typeLabel(item))}</span>
                <span class="wcn-splitcard-head-end">
                    <span class="wcn-chip wcn-chip-${PRIORITY_KIND[item.priority]} wcn-splitcard-prio"><i class="bx bx-flag"></i>${esc(priorityLabel(item))}</span>
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
    const LIFECYCLE_STAGE = { PendingAcceptance: 0, PendingApproval: 0, Open: 0, Planned: 0, InProgress: 1, Waiting: 1, PendingReview: 2, Done: 99, Cancelled: -1 };
    const renderStepBar = (item) => {
        if (!Array.isArray(item.stages) || !item.stages.length) { return ''; }
        const cancelled = item.lifecycle === 'Cancelled';
        // Map lifecycle → active stage index, clamped to the source's own stages.
        let active = LIFECYCLE_STAGE[item.lifecycle];
        if (active === 99) { active = item.stages.length; }        // Done → all complete
        active = Math.min(active, item.stages.length - 1);
        const steps = item.stages.map((stage, i) => {
            let cls = 'upcoming';
            if (!cancelled) { if (i < active) { cls = 'done'; } else if (i === active) { cls = 'active'; } }
            const dot = (i < active && !cancelled) ? '<i class="bx bx-check"></i>' : (i + 1);
            return `<li class="wcn-step wcn-step-${cls}"><span class="wcn-step-dot">${dot}</span><span class="wcn-step-label">${esc(stage.label)}</span></li>`;
        }).join('');
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('StepBarLabel'))}</h6>
            <ol class="wcn-steps${cancelled ? ' wcn-steps-cancelled' : ''}">${steps}</ol>
        </div>`;
    };

    // Source due date vs personal planned date (spec v2 §3/§4) — kept visually
    // distinct; a personal plan that lands after the source deadline is flagged.
    const renderPlanDates = (item) => {
        if (!item.dueAt && !item.plannedDate) { return ''; }
        const conflict = item.dueAt && item.plannedDate && item.plannedDate > item.dueAt;
        // The empty text is PER CELL: "SLA yok" answers "is there a deadline?", which says nothing about whether
        // the user has planned the work. One shared placeholder made a missing personal plan read as "SLA yok".
        const cell = (labelKey, value, emptyKey, cls) =>
            `<div class="wcn-date-cell${cls ? ' ' + cls : ''}"><span class="wcn-date-label">${esc(t(labelKey))}</span><span class="wcn-date-value">${esc(value || t(emptyKey))}</span></div>`;
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('DatesLabel'))}</h6>
            <div class="wcn-dates">
                ${cell('SourceDueLabel', item.dueAt, 'SlaNoSla', item.slaState === 'overdue' ? 'wcn-date-overdue' : '')}
                ${cell('PlannedDateLabel', item.plannedDate, 'PlannedDateNone', conflict ? 'wcn-date-conflict' : '')}
            </div>
            ${conflict ? `<div class="wcn-date-warn" role="note"><i class="bx bx-error-circle"></i><span>${esc(t('PlanConflict'))}</span></div>` : ''}
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
                <h6 class="wcn-detail-h6">${esc(t('ChecklistLabel'))}</h6>
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
            <h6 class="wcn-detail-h6">${esc(t('ChecklistLabel'))} <span class="wcn-count-inline">${done}/${items.length}</span></h6>
            <progress class="wcn-progress" value="${done}" max="${items.length}" aria-label="${esc(t('ChecklistLabel'))}"></progress>
            <ul class="wcn-checks">${rows}</ul>
            ${notice}
        </div>`;
    };

    // Subtasks — full: complete/add here; readonly: progress + "edit in source".
    const SUBTASK_ICON = { done: 'bxs-check-circle', 'in-progress': 'bx-loader-circle', 'not-started': 'bx-circle' };
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
        const rows = subtaskItems.map((s) =>
            `<li class="wcn-subtask wcn-subtask-${s.status}">
                <button type="button" class="wcn-subtask-toggle" ${full ? `data-wcn-subtask="${item.id}:${s.id}"` : 'disabled'}
                        aria-label="${esc(tf('SubtaskToggleAria', s.title))}">
                    <i class="bx ${SUBTASK_ICON[s.status] || 'bx-circle'}"></i>
                </button>
                <button type="button" class="wcn-subtask-title wcn-linklike" data-wcn-open-task="${esc(s.id)}"
                        aria-label="${esc(tf('SubtaskOpenAria', s.title))}">${esc(s.title)}</button>
            </li>`).join('');
        const adder = full
            ? `<div class="wcn-subtask-add">
                <input type="text" class="form-control form-control-sm" data-wcn-subtask-input placeholder="${esc(t('SubtaskAddPlaceholder'))}">
                <button type="button" class="btn btn-sm btn-label-primary" data-wcn-subtask-add="${item.id}">${esc(t('SubtaskAdd'))}</button>
               </div>`
            : `<p class="wcn-block-hint"><i class="bx bx-link-external"></i>${esc(t('SubtasksReadonlyHint'))}</p>`;
        // Open subtasks NEVER block the parent — they are reported, not enforced. Blocking belongs to the
        // checklist alone; two mechanisms would make "why can't I finish this?" unanswerable.
        const openNotice = subtaskItems.some((s) => s.status !== 'done')
            ? `<p class="wcn-block-hint" role="note">${esc(t('SubtasksOpenNotice'))}</p>`
            : '';
        const body = subtaskItems.length
            ? `<ul class="wcn-subtasks">${rows}</ul>${openNotice}`
            : `<p class="wcn-block-hint">${esc(t('SubtasksEmpty'))}</p>`;
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('SubtasksLabel'))}</h6>
            ${body}
            ${adder}
        </div>`;
    };

    // Typed dependencies — READONLY display; editing the graph is the source's job.
    const DEP_TYPE_KEY = { FS: 'DepTypeFS', FF: 'DepTypeFF', SS: 'DepTypeSS', SF: 'DepTypeSF' };
    const DEP_STATE_KEY = { done: 'DepDone', 'in-progress': 'DepInProgress', 'not-started': 'DepNotStarted' };
    const DEP_STATE_KIND = { done: 'success', 'in-progress': 'info', 'not-started': 'secondary' };
    const renderDependencies = (item) => {
        if (!hasCap(item, 'dependencies') || !item.dependencies || !item.dependencies.length) { return ''; }
        const rows = item.dependencies.map((d) =>
            `<li class="wcn-dep">
                <span class="wcn-dep-dir">${esc(t(d.direction === 'pred' ? 'DepPredecessor' : 'DepSuccessor'))}</span>
                <span class="wcn-dep-title">${esc(d.title)}</span>
                <span class="wcn-chip wcn-chip-secondary wcn-dep-type" title="${esc(t(DEP_TYPE_KEY[d.type] || d.type))}">${esc(d.type)}</span>
                <span class="wcn-badge wcn-badge-${DEP_STATE_KIND[d.state]}">${esc(t(DEP_STATE_KEY[d.state] || d.state))}</span>
            </li>`).join('');
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('DependenciesLabel'))}</h6>
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
            <h6 class="wcn-detail-h6">${esc(t('AttachmentsLabel'))}</h6>
            <ul class="wcn-attachments">${rows}</ul>
        </div>`;
    };

    const renderEvidence = (item) => {
        if (!hasCap(item, 'evidence') || !item.evidence) { return ''; }
        const entries = (item.evidence.items || []).map((entry) =>
            `<li class="wcn-attach"><i class="bx bx-shield-quarter"></i><span class="wcn-attach-name">${esc(data.resolveLabel(entry.label) || entry.id)}</span></li>`
        ).join('');
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('EvidenceMissing'))}</h6>
            ${entries ? `<ul class="wcn-attachments">${entries}</ul>` : `<p class="text-muted mb-0">${esc(t('ActionDisabledEvidenceIncomplete'))}</p>`}
        </div>`;
    };

    // Personal note — the thin overlay WorkCenter owns (only I see it).
    const renderNote = (item) => {
        if (isTerminal(item)) { return ''; }
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('NoteLabel'))}</h6>
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
        if (item.itemType !== 'task' || item.lifecycle === 'PendingAcceptance') { return ''; }
        const ts = item.timesheet || { loggedMinutes: 0, running: false };
        const live = ts.running
            ? `<span class="wcn-ts-live"><span class="wcn-ts-dot"></span><span id="wcnTimerValue">00:00</span><span class="wcn-ts-runtxt">${esc(t('TimerRunning'))}</span></span>`
            : '';
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('TimesheetLabel'))}</h6>
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
        `<div class="wcn-business-head"><span class="wcn-business-icon"><i class="bx ${icon}"></i></span><h6 class="wcn-detail-h6">${esc(t(titleKey))}</h6></div>`;

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
            return `<section class="wcn-detail-section wcn-business-section">${sectionHead('bx-grid-alt', section.title?.key || 'BusinessContextLabel')}<div class="wcn-facts-grid">${rows}</div></section>`;
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
    const renderSourceContext = (item, meta) => `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('DetailContext'))}</h6>
            <div class="wcn-meta-grid">
                ${meta('DetailRequester', item.requester)}
                ${meta('DetailAssignee', item.assignee || '—')}
                ${meta('DetailNativeStatus', item.nativeStatusText)}
                ${meta('DetailSourceId', item.sourceId)}
                ${meta('DetailModuleName', item.sourceModuleName || item.sourceModule)}
                ${meta('DetailModuleId', item.sourceModuleId || t('SourceIdentityPending'))}
                ${meta('DetailSourceType', item.sourceObjectType || item.sourceType)}
                ${meta('DetailActionDepth', t(item.actionDepth === 'deeplink' ? 'ActionDepthDeeplink' : 'ActionDepthInline'))}
                ${meta('DetailSourceVersion', item.concurrency ? `${item.concurrency.kind}: ${item.concurrency.token}` : '—')}
                ${item.lifecycleOwner ? meta('DetailLifecycleOwner', item.lifecycleOwner.providerCode) : ''}
            </div>
            <button type="button" class="btn btn-sm btn-label-primary wcn-opensource" data-wcn-open="${item.id}" aria-label="${esc(tf('OpenSourceAria', item.sourceModuleName || item.sourceModule, item.sourceId))}">
                <i class="bx bx-link-external"></i><span>${esc(t('DetailOpenSource'))}</span>
            </button>
        </div>`;

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
        const byCode = new Map(itemActions(item).map((candidate) => [candidate.code, candidate]));
        const placedCodes = [surface.primaryActionCode, ...surface.secondaryActionCodes, ...surface.overflowActionCodes].filter(Boolean);
        const acts = placedCodes.map((code) => byCode.get(code)).filter(Boolean);
        const actions = acts.length
            ? acts.map((a) => {
                const dis = a.disabled ? ' disabled' : '';
                const title = a.disabled ? ` title="${esc(t(a.disabledReasonKey || 'BlockedBanner'))}"` : '';
                const reason = a.disabled
                    ? `<small class="wcn-action-disabled-reason">${esc(a.disabledReason || t(a.disabledReasonKey || 'BlockedBanner'))}</small>`
                    : '';
                return `<span class="wcn-action-wrap"><button type="button" class="btn btn-sm btn-${a.primary ? '' : 'label-'}${a.kind}"${dis}${title} data-wcn-action="${a.key}" data-wcn-id="${item.id}">` +
                    `${esc(t(a.labelKey))}</button>${reason}</span>`;
            }).join('')
            : `<span class="wcn-noactions">${esc(t('NoActionsAvailable'))}</span>`;
        // Dependency banner (spec v2 §5): source-computed block, read-only here.
        const blockedBanner = isBlocked(item)
            ? `<div class="wcn-blocked" role="note">
                <i class="bx bx-lock-alt"></i>
                <div class="wcn-blocked-body">
                    <span class="wcn-blocked-title">${esc(t(item.blockedState.reasonKey || 'BlockedBanner'))}</span>
                    ${(item.blockedState.blockedBy || []).length
                        ? `<span class="wcn-blocked-by">${esc(t('BlockedByLabel'))}: ${item.blockedState.blockedBy.map((b) => `<button type="button" class="btn btn-sm btn-link p-0 m-0 align-baseline wcn-internal-link" data-wcn-jump="${b.id}">${esc(b.title)}</button>`).join(' · ')}</span>`
                        : ''}
                </div>
            </div>`
            : '';
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
        // Information-request round-trip (spec v2 §6) — parked waiting on someone.
        const waitingNote = item.waitingOn
            ? `<div class="wcn-parked wcn-parked-info" role="note"><i class="bx bx-time-five"></i><span>${esc(tf('WaitingOn', item.waitingOn))}</span></div>`
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
        const personal = (item.lifecycle === 'Done' || item.lifecycle === 'Cancelled') ? '' :
            `<div class="wcn-personal" role="group" aria-label="${esc(t('PersonalActionsLabel'))}">
                <button type="button" class="wcn-personal-btn${item.pinned ? ' active' : ''}" data-wcn-pin="${item.id}" aria-pressed="${item.pinned}">
                    <i class="bx ${item.pinned ? 'bxs-pin' : 'bx-pin'}"></i><span>${esc(t(item.pinned ? 'Unpin' : 'Pin'))}</span>
                </button>
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
                    <span class="wcn-audit-meta">${esc(entry.actor)} · ${esc(agoLabel(entry.ago))}</span>
                </div>
            </li>`;
        }).join('');

        const meta = (labelKey, value) =>
            `<div class="wcn-meta-cell"><span class="wcn-meta-label">${esc(t(labelKey))}</span><span class="wcn-meta-value">${esc(value)}</span></div>`;

        // Card-grid detail (golden reference parity): a command card on top, then a
        // main column (work content, wide) beside a sidebar (source/status meta,
        // narrow), and a full-width activity feed. Widths are driven by content:
        // wide work → col-lg-8, compact meta → col-lg-4, conversation → col-12.
        const card = (inner) => inner
            ? `<section class="card backbone-preview-section wcn-detail-card p-4">${inner}</section>`
            : '';
        const reviewNote = (item.itemType === 'task' && item.lifecycle === 'PendingReview')
            ? `<div class="wcn-review-note"><i class="bx bx-hourglass"></i><span>${esc(t('AwaitingReview'))}</span></div>`
            : '';
        const summarySection = `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('DetailSummary'))}</h6>
            <p class="wcn-detail-summary">${esc(item.summary)}</p>
        </div>`;
        const activitySection = `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('ActivityLabel'))}</h6>
            ${renderComposer(item)}
            <ul class="wcn-audit">${auditRows}</ul>
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
            <h5 class="wcn-detail-title">${esc(item.title)}</h5>
            <div class="wcn-detail-chips">
                ${chip(SLA_KIND[item.slaState], 'bx-time-five', slaLabel(item))}
                ${chip(PRIORITY_KIND[item.priority], 'bx-flag', priorityLabel(item))}
                ${chip('role', 'bx-user-check', t(ROLE_KEY[item.viewerRole] || item.viewerRole))}
            </div>
            ${renderStepBar(item)}
            ${reviewNote}
            ${sysBanner}${blockedBanner}${notices}${waitingNote}${snoozeNote}
            <div class="wcn-detail-actions" role="group" aria-label="${esc(t('ActionsLabel'))}">${actions}</div>
            ${personal}
        </section>`;

        // Flowing bento — each card carries its own width, and the deck is ordered so
        // cards pair up to a full 12-column line (8+4, 6+6, 4+4+4). A wide "work" card
        // sits beside a compact "meta" card, so no single fixed column can run tall and
        // leave the other side a void. Absent capabilities simply drop out and the rest
        // compacts upward. Widths are chosen by content weight, not category.
        const cell = (inner, col) => inner
            ? `<div class="col-12 ${col}"><section class="card backbone-preview-section wcn-detail-card p-4">${inner}</section></div>`
            : '';
        const bento = [
            cell(summarySection, 'col-lg-8'),
            cell(renderPlanDates(item), 'col-lg-4'),
            cell(renderBusinessContext(item), 'col-lg-8'),
            cell(renderTimesheet(item), 'col-lg-4'),
            cell(renderChecklist(item), 'col-lg-6'),
            cell(renderParentContext(item), 'col-12'),
            cell(renderSubtasks(item), 'col-lg-6'),
            cell(renderDependencies(item), 'col-lg-6'),
            cell(renderSourceContext(item, meta), 'col-lg-6'),
            cell(`${renderDelegation(item)}${renderApprovalChain(item)}`, 'col-lg-6'),
            cell(renderCompliance(item), 'col-lg-6'),
            cell(renderNote(item), 'col-lg-4'),
            cell(renderAttachments(item), 'col-lg-4'),
            cell(renderEvidence(item), 'col-lg-4'),
            cell(renderRelated(item), 'col-lg-8')
        ].filter(Boolean).join('');

        return `<div class="wcn-detail wcn-details-page">
            <div class="row g-4 wcn-detail-grid">
                <div class="col-12">${commandCard}</div>
                ${bento}
                <div class="col-12">${card(activitySection)}</div>
            </div>
        </div>`;
    };

    // ── Table view ────────────────────────────────────────────────────────────
    const SORTERS = {
        sla: (a, b) => bySla(a, b),
        title: (a, b) => a.title.localeCompare(b.title),
        module: (a, b) => a.sourceModule.localeCompare(b.sourceModule),
        type: (a, b) => a.itemType.localeCompare(b.itemType),
        status: (a, b) => a.status.localeCompare(b.status),
        priority: (a, b) => ['high', 'medium', 'low'].indexOf(a.priority) - ['high', 'medium', 'low'].indexOf(b.priority),
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
        global.L10n = global.L10n || {};
        global.L10n.Search = t('SearchPlaceholder');
        global.L10n.Action = t('ExportLabel');
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
                { data: 'priority', name: 'priority', visible: state.tableColumnVisibility[5], render: (value, type, row) => type === 'display' ? (row.fixtureKind === 'triggerOnly' ? '—' : chip(PRIORITY_KIND[row.priority], 'bx-flag', priorityLabel(row))) : ['high', 'medium', 'low'].indexOf(value) },
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
                ${chip(PRIORITY_KIND[item.priority], 'bx-flag', priorityLabel(item))}
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
            const item = itemById(root.dataset.wcnItemId || '');
            state.selectedId = item ? item.id : null;
            if (item) { markSeen(item); }
            root.innerHTML = item
                ? detailHtml(item)
                : `<section class="card backbone-preview-section"><div class="wcn-detail-empty"><i class="bx bx-error-circle"></i><p>${esc(t('DetailItemNotFound'))}</p><a class="btn btn-label-secondary" href="/WorkCenterNext">${esc(t('DetailBackToList'))}</a></div></section>`;
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
                setProjectionState(item, 'Pending', item.itemType === 'task' ? 'Open' : null, 'Atanmadı — Operasyon Kuyruğu');
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
                // Information request round-trip — park in Waiting and record who we're
                // waiting on (the requester) so the "Bekleyen" note is meaningful.
                item.waitingOn = item.waitingOn || item.requester;
                item.waitingContext = item.waitingContext || {
                    type: 'information',
                    waitingOn: { displayName: item.waitingOn },
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
    const submitRealTransition = async (item, action, reason) => {
        const label = actionLabel(action);
        state.submittingItemId = item.id;
        state.submittingActionCode = action.code;
        render();

        // The concurrency token from the projection — an expected-version write, so a stale screen loses cleanly.
        const expectedVersion = Number(item.concurrency?.token ?? 0);
        const result = await global.TasksApi.transition(item.id, action.code, {
            expectedVersion,
            reasonCode: null,
            note: reason || null
        });

        state.submittingItemId = null;
        state.submittingActionCode = null;

        if (result.ok) {
            await loadWorkItems();
            render();
            // The task's TITLE, never its id — a GUID means nothing to the person reading the toast.
            toast(tf('ToastActionApplied', label, item.title));
            return;
        }

        if (result.status === 409 || result.reasonCode === 'TASK_CONCURRENCY_CONFLICT') {
            // Someone else changed it first. Refresh so the screen shows the truth, then say so.
            await loadWorkItems();
            render();
            toast(t('ErrorConcurrencyRefreshed'), 'error');
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

        if (result.status === 409 || result.reasonCode === 'TASK_CONCURRENCY_CONFLICT') {
            // Someone changed it first — show the truth, then say so.
            await loadWorkItems();
            render();
            toast(t('ErrorConcurrencyRefreshed'), 'error');
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

    const applyAction = (item, action, reason) => {
        if (isRealTaskItem(item)) { submitRealTransition(item, action, reason); return; }

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
            item.activity.push({
                actor: data.currentUser.name,
                kind: 'event',
                eventKey: 'AuditActionStamp',
                actionLabel: label,
                ago: 0
            });
            state.submittingItemId = null;
            state.submittingActionCode = null;
            render();
            toastForOutcome(outcome, label, reason, item);
        }, 350);
    };

    // Plan / re-plan — sets the PERSONAL planned date (spec v2 §4), which is
    // distinct from the source due date; SLA (source) is never overwritten.
    const applyPlan = (item, dateStr, label) => {
        item.plannedDate = dateStr;
        if (item.lifecycle === 'Open') { setProjectionState(item, item.normalizedStatus, 'Planned', item.nativeStatusText); }
        markSeen(item);
        item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: label, ago: 0 });
        render();
        toast(tf('ToastPlanned', item.title, dateStr));
    };

    const openDatePicker = (item, action) => {
        const label = actionLabel(action);
        if (!global.Swal) { applyPlan(item, item.dueAt || data.todayIso, label); return; }
        global.Swal.fire({
            title: label,
            html: '<input id="wcnPlanDate" class="form-control" autocomplete="off">',
            showCancelButton: true, confirmButtonText: t('PlanConfirm'), cancelButtonText: t('ReasonCancel'),
            didOpen: () => {
                const input = document.getElementById('wcnPlanDate');
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
        }).then((res) => { if (res.isConfirmed && res.value) { applyPlan(item, res.value, label); } });
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
        const projected = data.toPresentation(replacement);
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
                item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: label, ago: 0 });
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
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Unsnooze'), ago: 0 });
            render();
            toast(tf('ToastUnsnoozed', item.title));
            return;
        }
        const apply = (dateStr) => {
            item.snoozedUntil = dateStr;
            item.personal.snoozedUntil = dateStr;
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Snooze'), ago: 0 });
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

    /** Re-reads the projection so a new task appears exactly as the Task Center sees it. */
    const refreshAfterTaskCreated = async (title) => {
        await loadWorkItems();
        state.tab = 'islerim';
        state.segment = 'aktif';
        state.view = 'list';
        render();
        toast(title ? tf('ToastSelfTaskCreated', title) : tf('ToastSelfTaskCreated', ''));
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
            activity: [{ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditSelfCreated', ago: 0 }]
        });
        const item = data.toPresentation(fixture);
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
            showCancelButton: true, confirmButtonText: t('NewOpenSource'), cancelButtonText: t('ReasonCancel')
        }).then((res) => {
            if (res.isConfirmed && res.value) {
                const target = state.items.find((item) => item.sourceModule === res.value);
                if (target && target.deepLink) { global.open(target.deepLink, '_blank', 'noopener,noreferrer'); }
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
        createSelfTask({ title: tf('MeetingFollowupTitle', meeting.title), date: null, priority: 'medium' }, { sourceModule: t('MeetingSource') });
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
        createSelfTask({ title: note.text, date: null, priority: 'medium' }, { sourceModule: t('NotesSource') });
    };

    const performAction = (item, actionKey) => {
        const action = actionByKey(item, actionKey);
        if (!item || !action || action.disabled || state.submittingItemId === item.id) { return; }
        // The date picker feeds a PERSONAL planned date that only the fixture path stores; the engine's /plan
        // endpoint accepts no date (it moves the lifecycle Open→Planned). Asking a real user for a date we then
        // discard would be a new lie, so a real task goes straight to the transition.
        if (action.input === 'date' && !isRealTaskItem(item)) { openDatePicker(item, action); return; }
        if (action.input === 'meeting') { openMeetingScheduler(item, action); return; }
        if (action.input === 'minutes') { openLogTime(item, action); return; }

        // Reason-capturing action (reject/return/inquire/dispute/delegate/reassign):
        // a mandatory-rationale textarea, which also serves as the confirm step.
        if (action.reason) {
            if (!global.Swal) { return; }
            global.Swal.fire({
                title: actionLabel(action),
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
            }).then((res) => { if (res.isConfirmed && res.value) { applyAction(item, action, res.value); } });
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
            applyTransition(item, action.key);
            markSeen(item);
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: actionLabel(action), ago: 0 });
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

    const openDetailPage = (id) => {
        if (!id) { return; }
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
        if (scopeEl) { state.scope = scopeEl.getAttribute('data-wcn-scope'); state.selectedId = null; state.tableSelected.clear(); state.bulkFailedIds.clear(); render(); return; }

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
        const openTaskEl = event.target.closest('[data-wcn-open-task]');
        if (openTaskEl) {
            openDetailPage(openTaskEl.getAttribute('data-wcn-open-task'));
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
        const subAddEl = event.target.closest('[data-wcn-subtask-add]');
        if (subAddEl) {
            const input = document.querySelector('#wcnApp [data-wcn-subtask-input]');
            addSubtask(subAddEl.getAttribute('data-wcn-subtask-add'), input ? input.value : '');
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
            const it = itemById(commentEl.getAttribute('data-wcn-comment-post'));
            const inp = document.querySelector('#wcnApp [data-wcn-comment-input]');
            const val = inp && inp.value.trim();
            if (it && val) { it.activity.push({ actor: data.currentUser.name, kind: 'comment', text: val, ago: 0 }); render(); toast(t('ToastCommentPosted')); }
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

        const result = await api.fetchWorkItems();
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

    // ── Boot ──────────────────────────────────────────────────────────────────
    let booted = false;
    const boot = async () => {
        const root = document.getElementById('wcnApp');
        if (!root || booted) { return; }   // guard: a second bundle load / hot reload
        booted = true;                     // must not double-bind document listeners
        if (root.dataset.wcnPage !== 'detail') {
            hydrateStateFromUrl();
            state.viewsByTab[state.tab] = state.view;
        } else {
            state.loadState = 'ready';
        }
        document.addEventListener('click', onClick);
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
