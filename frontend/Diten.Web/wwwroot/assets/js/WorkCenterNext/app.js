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
    const TYPE_KEY = { approval: 'TypeApproval', task: 'TypeTask', review: 'TypeReview', issue: 'TypeIssue', exception: 'TypeException' };
    const TYPE_ICON_MAP = { approval: 'bx-check-shield', task: 'bx-task', review: 'bx-search-alt', issue: 'bx-error-circle', exception: 'bx-error-alt' };
    const SIGNAL_ICON = { blocked: 'bx-lock-alt', 'sla-risk': 'bx-time-five', escalated: 'bx-up-arrow-alt' };
    const MODE_KEY = { direct: 'ModeDirect', approval: 'ModeApproval', groupQueue: 'ModeGroupQueue', offered: 'ModeOffered' };
    const SYSSTATE = { 'record-changed': { key: 'SysRecordChanged', icon: 'bx-refresh', kind: 'warning' }, 'source-unreachable': { key: 'SysSourceUnreachable', icon: 'bx-wifi-off', kind: 'danger' }, 'authority-ended': { key: 'SysAuthorityEnded', icon: 'bx-user-x', kind: 'danger' } };
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
        scope: 'mine',           // 'mine' | 'all' | <delegator name> (N-way delegation)
        group: 'all',            // Havuz group-queue filter
        priorityFilter: 'all',
        modeFilter: 'all',
        moduleFilter: 'all',
        typeFilter: new Set(),   // multi-select type chips (empty = all)
        signalFilter: new Set(), // multi-select signal chips (empty = all)
        search: '',
        selectedId: null,
        tableSelected: new Set(),
        bulkFailedIds: new Set(),
        sortKey: 'sla',
        sortDir: 'asc',
        loadState: 'loading',
        loadError: null,
        items: data.buildItems(),
        meetings: data.buildMeetings ? data.buildMeetings() : [],
        notes: data.buildNotes ? data.buildNotes() : [],
        visibleOrder: []
    };

    const STATE_VALUES = {
        tab: ['inbox', 'islerim', 'havuz', 'history'],
        segment: ['aktif', 'bekleyen', 'planli'],
        view: ['list', 'split', 'table', 'kanban', 'calendar', 'focus'],
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
        const module = params.get('module');
        if (module && (module === 'all' || state.items.some((i) => i.sourceModule === module))) { state.moduleFilter = module; }
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
        put('module', state.moduleFilter, 'all');
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
    const statusLabel = (item) => t(STATUS_KEY[item.status] || item.status);
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

    // Actions are derived on demand from (itemType, lifecycle, timesheet) — never
    // baked onto the item — so the acceptance gate + lifecycle transitions stay
    // authoritative (spec §4 action matrix).
    const itemActions = (item) => data.getActions(item).map((action) => item.systemState
        ? { ...action, disabled: true, disabledReasonKey: 'SourceActionsLocked' }
        : action);
    // The row quick-action skips disabled actions (e.g. a blocked Start), so a
    // blocked task never offers a dead primary button.
    const primaryAction = (item) => {
        const a = itemActions(item).filter((x) => !x.disabled);
        return a.find((x) => x.role === 'accept') || a.find((x) => x.primary) || a[0] || null;
    };
    const actionByKey = (item, key) => itemActions(item).find((a) => a.key === key) || null;
    const actionByRole = (item, role) => itemActions(item).find((a) => a.role === role) || null;

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
    const isTerminal = (item) => item.lifecycle === 'Done' || item.lifecycle === 'Cancelled';
    const inTab = (item, tab) => !item.dismissed && itemInScope(item)
        && (tab === 'history' ? isTerminal(item) : item.tab === tab && !isTerminal(item));
    // Tab counters ignore in-tab filters so they reflect the true load per scope.
    const tabCount = (tab) => state.items.filter((item) => inTab(item, tab)).length;

    // Items in the current tab (before segment/chip filters) — used for segment
    // and chip counts so those reflect what the tab really holds.
    const tabItems = () => state.items.filter((item) => inTab(item, state.tab));
    const segmentCount = (seg) => tabItems().filter((i) => data.segmentFor(i) === seg).length;
    const typeCount = (ty) => tabItems().filter((i) => i.itemType === ty).length;
    const signalCount = (sig) => tabItems().filter((i) => SIGNAL_TEST[sig](i)).length;

    // Advanced filters shared by list + kanban + calendar (priority, mode, group,
    // module, search) — everything except the tab-specific segment filter.
    const passesFilters = (item) => {
        if (state.typeFilter.size && !state.typeFilter.has(item.itemType)) { return false; }
        if (state.signalFilter.size) {
            for (const sig of state.signalFilter) { if (!SIGNAL_TEST[sig](item)) { return false; } }
        }
        if (state.moduleFilter !== 'all' && item.sourceModule !== state.moduleFilter) { return false; }
        if (state.priorityFilter !== 'all' && item.priority !== state.priorityFilter) { return false; }
        if (state.modeFilter !== 'all' && item.assignmentMode !== state.modeFilter) { return false; }
        if (state.tab === 'havuz' && state.group !== 'all' && item.group !== state.group) { return false; }
        if (state.search) {
            const hay = (item.title + ' ' + item.summary + ' ' + item.sourceModule + ' ' + item.sourceId + ' ' + item.requester).toLowerCase();
            if (!hay.includes(state.search.toLowerCase())) { return false; }
        }
        return true;
    };

    const activeItems = () => state.items.filter((item) => {
        if (!inTab(item, state.tab)) { return false; }
        if (SEGMENTS[state.tab] && data.segmentFor(item) !== state.segment) { return false; }
        return passesFilters(item);
    });

    const bySla = (a, b) => {
        if (a.escalated && !b.escalated) return -1;
        if (!a.escalated && b.escalated) return 1;
        return SLA_ORDER.indexOf(a.slaState) - SLA_ORDER.indexOf(b.slaState);
    };

    const moduleOptions = () => {
        const set = [];
        state.items.forEach((item) => { if (set.indexOf(item.sourceModule) < 0) { set.push(item.sourceModule); } });
        return set.sort();
    };

    // ── Toolbar ───────────────────────────────────────────────────────────────
    const viewBtn = (view, icon, labelKey) =>
        `<button type="button" class="wcn-viewbtn${state.view === view ? ' active' : ''}" data-wcn-view="${view}">` +
        `<i class="bx ${icon}"></i><span>${esc(t(labelKey))}</span></button>`;

    // My own items still needing action (overdue) — surfaced even while acting on
    // someone else's behalf so urgent personal work is never hidden (spec v3 §6).
    const ownUrgentCount = () => state.items.filter((i) =>
        !i.dismissed && !i.delegator && i.slaState === 'overdue'
        && i.lifecycle !== 'Done' && i.lifecycle !== 'Cancelled').length;

    const delegatorByName = (name) => data.delegators.find((d) => d.name === name) || null;

    const buildHeader = () => {
        const urgent = ownUrgentCount();
        const ownBadge = (state.scope !== 'mine' && urgent)
            ? `<span class="wcn-own-urgent" title="${esc(t('OwnUrgentTip'))}">${urgent}</span>` : '';
        // Identity / delegation as a SINGLE pill selector (spec v3) — not N side-by-side
        // buttons. Neutral "Kendim" by default; a loud warning pill while acting for
        // someone else so the accountability context is never missed.
        const delegating = state.scope !== 'mine' && state.scope !== 'all';
        const pillLabel = state.scope === 'mine' ? t('ScopeMine')
            : state.scope === 'all' ? t('ScopeAll') : state.scope;
        const idItem = (key, label, covering) =>
            `<li><button type="button" class="dropdown-item${state.scope === key ? ' active' : ''}" data-wcn-scope="${esc(key)}">` +
            `${covering ? '<i class="bx bx-user-voice"></i> ' : ''}${esc(label)}${key === 'mine' ? ownBadge : ''}</button></li>`;
        const delegatorItems = data.delegators
            .map((d) => idItem(d.name, tf('ScopeCovering', d.name), true)).join('');
        const divider = data.delegators.length ? '<li><hr class="dropdown-divider"></li>' : '';
        return `<div class="wcn-header">
            <div class="wcn-header-title">
                <span class="wcn-header-icon"><i class="bx bx-briefcase-alt-2"></i></span>
                <div>
                    <h4 class="wcn-header-heading">${esc(t('Title'))}</h4>
                    <p class="wcn-header-sub">${esc(t('Subtitle'))}</p>
                </div>
            </div>
            <div class="wcn-header-actions">
                <div class="dropdown">
                    <button type="button" class="wcn-idpill${delegating ? ' wcn-idpill-active' : ''}" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${esc(t('ScopeLabel'))}">
                        ${delegating ? '<i class="bx bx-user-voice wcn-idpill-warn"></i>' : ''}<span>${esc(pillLabel)}</span>${state.scope !== 'mine' ? ownBadge : ''}<i class="bx bx-chevron-down wcn-idpill-caret"></i>
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end wcn-idmenu">
                        ${idItem('mine', t('ScopeMine'))}
                        ${divider}
                        ${delegatorItems}
                        ${divider}
                        ${idItem('all', t('ScopeAll'))}
                    </ul>
                </div>
                <button type="button" class="wcn-newbtn" data-wcn-new><i class="bx bx-plus"></i><span>${esc(t('NewButton'))}</span></button>
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

    // Top-tabs = OWNERSHIP (spec v3): primary (Gelen Kutusu · İşlerim) + secondary
    // (Havuz · Geçmiş, lighter). Havuz always shows its count — even 0 — so group
    // work is discoverable. Inbox is default so new work is seen on open.
    const buildTabs = () => {
        const tab = (key, cls) =>
            `<button type="button" id="wcn-tab-${key}" class="wcn-tab ${cls}${state.tab === key ? ' active' : ''}" data-wcn-tab="${key}" role="tab" aria-selected="${state.tab === key}" aria-controls="wcn-main-panel" tabindex="${state.tab === key ? '0' : '-1'}">` +
            `<span>${esc(t(TAB_KEY[key]))}</span><span class="wcn-tab-count">${tabCount(key)}</span></button>`;
        return `<div class="wcn-tabs" role="tablist" aria-label="${esc(t('TabsLabel'))}">
            <div class="wcn-tabs-primary">${TABS_PRIMARY.map((k) => tab(k, 'wcn-tab-primary')).join('')}</div>
            <div class="wcn-tabs-secondary">${TABS_SECONDARY.map((k) => tab(k, 'wcn-tab-secondary')).join('')}</div>
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

    // Chip bar = TYPE + SIGNAL (spec v3). Counted, multi-select; empty = all.
    const buildChips = () => {
        const typeChip = (ty) => {
            const c = typeCount(ty);
            return `<button type="button" class="wcn-fchip${state.typeFilter.has(ty) ? ' active' : ''}${c ? '' : ' empty'}" data-wcn-typechip="${ty}" aria-pressed="${state.typeFilter.has(ty)}">` +
                `<i class="bx ${TYPE_ICON_MAP[ty]}"></i><span>${esc(t(TYPE_KEY[ty]))}</span><span class="wcn-fchip-count">${c}</span></button>`;
        };
        const sigChip = (sig) => {
            const c = signalCount(sig);
            if (!c && !state.signalFilter.has(sig)) { return ''; }
            return `<button type="button" class="wcn-fchip wcn-fchip-signal${state.signalFilter.has(sig) ? ' active' : ''}" data-wcn-sigchip="${sig}" aria-pressed="${state.signalFilter.has(sig)}">` +
                `<i class="bx ${SIGNAL_ICON[sig]}"></i><span>${esc(t(SIGNAL_KEY[sig]))}</span><span class="wcn-fchip-count">${c}</span></button>`;
        };
        const types = Object.keys(TYPE_KEY).map(typeChip).join('');
        const signals = SIGNALS.map(sigChip).join('');
        const anyFilter = state.typeFilter.size || state.signalFilter.size;
        const clear = anyFilter ? `<button type="button" class="wcn-fchip-clear" data-wcn-chip-clear>${esc(t('ChipClear'))}</button>` : '';
        return `<div class="wcn-chips" role="group" aria-label="${esc(t('FilterType'))}">
            <div class="wcn-chips-types">${types}</div>
            ${signals ? `<span class="wcn-chips-sep"></span><div class="wcn-chips-signals">${signals}</div>` : ''}
            ${clear}
        </div>`;
    };

    const buildToolbar = () => {
        const modOpts = ['<option value="all">' + esc(t('FilterAllModules')) + '</option>']
            .concat(moduleOptions().map((m) =>
                `<option value="${esc(m)}"${state.moduleFilter === m ? ' selected' : ''}>${esc(m)}</option>`)).join('');

        return `<div class="wcn-toolbar">
            <div class="wcn-views" role="group" aria-label="${esc(t('ViewLabel'))}">
                ${viewBtn('list', 'bx-list-ul', 'ViewList')}
                ${viewBtn('split', 'bx-columns', 'ViewSplit')}
                ${viewBtn('table', 'bx-table', 'ViewTable')}
                ${viewBtn('kanban', 'bx-grid-alt', 'ViewKanban')}
                ${viewBtn('calendar', 'bx-calendar', 'ViewCalendar')}
                ${viewBtn('focus', 'bx-target-lock', 'ViewFocus')}
            </div>
            <div class="wcn-filters">
                <div class="wcn-search">
                    <i class="bx bx-search"></i>
                    <input type="search" class="form-control form-control-sm" data-wcn-search
                        value="${esc(state.search)}" placeholder="${esc(t('SearchPlaceholder'))}" aria-label="${esc(t('SearchPlaceholder'))}">
                </div>
                <select class="form-select form-select-sm wcn-select" data-wcn-filter="module" aria-label="${esc(t('FilterModule'))}">${modOpts}</select>
                <select class="form-select form-select-sm wcn-select" data-wcn-filter="priority" aria-label="${esc(t('FilterPriority'))}">
                    <option value="all">${esc(t('FilterAllPriorities'))}</option>
                    ${['high', 'medium', 'low'].map((p) => `<option value="${p}"${state.priorityFilter === p ? ' selected' : ''}>${esc(t(PRIORITY_KEY[p]))}</option>`).join('')}
                </select>
                <select class="form-select form-select-sm wcn-select" data-wcn-filter="mode" aria-label="${esc(t('FilterMode'))}">
                    <option value="all">${esc(t('FilterAllModes'))}</option>
                    ${['direct', 'approval', 'groupQueue', 'offered'].map((m) => `<option value="${m}"${state.modeFilter === m ? ' selected' : ''}>${esc(t(MODE_KEY[m]))}</option>`).join('')}
                </select>
            </div>
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
        (item.systemState && SYSSTATE[item.systemState]) ? chip(SYSSTATE[item.systemState].kind, SYSSTATE[item.systemState].icon, t(SYSSTATE[item.systemState].key)) : '',
        item.requester ? chip('requester', 'bx-user', item.requester) : ''
    ].join('');

    const rowHtml = (item, opts) => {
        const compact = opts && opts.compact;
        const selected = item.id === state.selectedId;
        const prim = primaryAction(item);
        const quick = prim
            ? `<button type="button" class="wcn-quick btn btn-sm btn-label-${prim.kind}" data-wcn-action="${prim.key}" data-wcn-id="${item.id}">` +
              `${esc(t(prim.labelKey))}</button>`
            : '';
        const terminal = item.lifecycle === 'Done' || item.lifecycle === 'Cancelled';
        const pinBtn = terminal ? '' : `<button type="button" class="wcn-pin${item.pinned ? ' pinned' : ''}" data-wcn-pin="${item.id}" title="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-label="${esc(t(item.pinned ? 'Unpin' : 'Pin'))}" aria-pressed="${item.pinned}"><i class="bx ${item.pinned ? 'bxs-pin' : 'bx-pin'}"></i></button>`;
        const onBehalfBadge = item.delegator
            ? `<span class="wcn-badge wcn-badge-delegation" title="${esc(tf('OnBehalfOf', item.delegator))}"><i class="bx bx-user-voice"></i>${esc(tf('OnBehalfShort', item.delegator))}</span>`
            : '';
        return `<div class="wcn-row${selected ? ' selected' : ''}${item.isUnread ? ' unread' : ''}" data-wcn-row="${item.id}" tabindex="0">
            <span class="wcn-row-unread" aria-hidden="true"></span>
            <div class="wcn-row-body">
                <div class="wcn-row-top">
                    <span class="wcn-row-title">${esc(item.title)}</span>
                    ${onBehalfBadge}
                    <span class="wcn-badge wcn-badge-${STATUS_KIND[item.status]}">${esc(statusLabel(item))}</span>
                </div>
                ${compact ? '' : `<p class="wcn-row-summary">${esc(item.summary)}</p>`}
                <div class="wcn-row-chips">${rowChips(item)}</div>
            </div>
            <div class="wcn-row-actions">${pinBtn}${quick}</div>
        </div>`;
    };

    // ── List view (grouped by SLA) ────────────────────────────────────────────
    const renderList = (items) => {
        if (!items.length) { return emptyState(); }
        state.visibleOrder = [];
        // Inbox: approvals ride a distinct top band — they need a decision in
        // place, not triage, so mixing them with accept-mode items blurs the two
        // modes (spec v3 §4, Fable). The rest keeps the SLA grouping.
        let approvalBand = '';
        let rest = items;
        if (state.tab === 'inbox') {
            const approvals = items.filter((i) => i.itemType === 'approval');
            rest = items.filter((i) => i.itemType !== 'approval');
            if (approvals.length) {
                const rows = approvals.slice().sort(bySla).map((item) => { state.visibleOrder.push(item.id); return rowHtml(item); }).join('');
                approvalBand = `<section class="wcn-group wcn-approval-band">
                    <header class="wcn-group-head wcn-group-primary">
                        <span class="wcn-group-dot"></span>
                        <span class="wcn-group-name">${esc(t('ApprovalBand'))}</span>
                        <span class="wcn-group-count">${approvals.length}</span>
                    </header>
                    <div class="wcn-group-rows">${rows}</div>
                </section>`;
            }
        }
        const groups = {};
        rest.slice().sort(bySla).forEach((item) => {
            (groups[item.slaState] = groups[item.slaState] || []).push(item);
        });
        const html = SLA_ORDER.filter((k) => groups[k] && groups[k].length).map((k) => {
            const rows = groups[k].map((item) => { state.visibleOrder.push(item.id); return rowHtml(item); }).join('');
            return `<section class="wcn-group">
                <header class="wcn-group-head wcn-group-${SLA_KIND[k]}">
                    <span class="wcn-group-dot"></span>
                    <span class="wcn-group-name">${esc(t(SLA_GROUP_KEY[k]))}</span>
                    <span class="wcn-group-count">${groups[k].length}</span>
                </header>
                <div class="wcn-group-rows">${rows}</div>
            </section>`;
        }).join('');
        return `<div class="wcn-list">${approvalBand}${html}</div>`;
    };

    // ── Split-detail view ─────────────────────────────────────────────────────
    const renderSplit = (items) => {
        if (!items.length) { return emptyState(); }
        state.visibleOrder = [];
        const rows = items.slice().sort(bySla).map((item) => {
            state.visibleOrder.push(item.id);
            return rowHtml(item, { compact: true });
        }).join('');
        if (!state.selectedId || state.visibleOrder.indexOf(state.selectedId) < 0) {
            state.selectedId = state.visibleOrder[0] || null;
        }
        return `<div class="wcn-split">
            <div class="wcn-split-list">${rows}</div>
            <div class="wcn-split-detail">${detailHtml(itemById(state.selectedId))}</div>
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
        const cell = (labelKey, value, cls) =>
            `<div class="wcn-date-cell${cls ? ' ' + cls : ''}"><span class="wcn-date-label">${esc(t(labelKey))}</span><span class="wcn-date-value">${esc(value || t('SlaNoSla'))}</span></div>`;
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('DatesLabel'))}</h6>
            <div class="wcn-dates">
                ${cell('SourceDueLabel', item.dueAt, item.slaState === 'overdue' ? 'wcn-date-overdue' : '')}
                ${cell('PlannedDateLabel', item.plannedDate, conflict ? 'wcn-date-conflict' : '')}
            </div>
            ${conflict ? `<div class="wcn-date-warn" role="note"><i class="bx bx-error-circle"></i><span>${esc(t('PlanConflict'))}</span></div>` : ''}
        </div>`;
    };

    // ── Capability-driven depth blocks (spec v3 §5) — do-the-work in the
    // aggregator; define-the-work stays in the source (deep-link). ─────────────
    const hasCap = (item, cap) => Array.isArray(item.capabilities) && item.capabilities.indexOf(cap) >= 0;

    // Checklist — interactive (checking is "doing the work", stays here).
    const renderChecklist = (item) => {
        if (!hasCap(item, 'checklist') || !item.checklist || !item.checklist.items.length) { return ''; }
        const items = item.checklist.items;
        const done = items.filter((c) => c.done).length;
        const ro = isTerminal(item);
        const rows = items.map((c) =>
            `<li class="wcn-check${c.done ? ' done' : ''}">
                <button type="button" class="wcn-check-box" data-wcn-check-item="${item.id}:${c.id}"${ro ? ' disabled' : ''} aria-pressed="${c.done}">
                    <i class="bx ${c.done ? 'bxs-check-square' : 'bx-square'}"></i>
                </button>
                <span class="wcn-check-text">${esc(c.text)}</span>
            </li>`).join('');
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('ChecklistLabel'))} <span class="wcn-count-inline">${done}/${items.length}</span></h6>
            <div class="wcn-progress"><div class="wcn-progress-bar" style="inline-size:${Math.round(done / items.length * 100)}%"></div></div>
            <ul class="wcn-checks">${rows}</ul>
        </div>`;
    };

    // Subtasks — full: complete/add here; readonly: progress + "edit in source".
    const SUBTASK_ICON = { done: 'bxs-check-circle', 'in-progress': 'bx-loader-circle', 'not-started': 'bx-circle' };
    const renderSubtasks = (item) => {
        if (!hasCap(item, 'subtasks') || !item.subtasks || !item.subtasks.items.length) { return ''; }
        const full = item.subtasks.mode === 'full' && !isTerminal(item);
        const rows = item.subtasks.items.map((s) =>
            `<li class="wcn-subtask wcn-subtask-${s.status}">
                <button type="button" class="wcn-subtask-toggle" ${full ? `data-wcn-subtask="${item.id}:${s.id}"` : 'disabled'}>
                    <i class="bx ${SUBTASK_ICON[s.status] || 'bx-circle'}"></i>
                </button>
                <span class="wcn-subtask-title">${esc(s.title)}</span>
            </li>`).join('');
        const adder = full
            ? `<div class="wcn-subtask-add">
                <input type="text" class="form-control form-control-sm" data-wcn-subtask-input placeholder="${esc(t('SubtaskAddPlaceholder'))}">
                <button type="button" class="btn btn-sm btn-label-primary" data-wcn-subtask-add="${item.id}">${esc(t('SubtaskAdd'))}</button>
               </div>`
            : `<p class="wcn-block-hint"><i class="bx bx-link-external"></i>${esc(t('SubtasksReadonlyHint'))}</p>`;
        return `<div class="wcn-detail-section">
            <h6 class="wcn-detail-h6">${esc(t('SubtasksLabel'))}</h6>
            <ul class="wcn-subtasks">${rows}</ul>
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

    const detailHtml = (item) => {
        if (!item) {
            return `<div class="wcn-detail-empty">
                <i class="bx bx-select-multiple"></i>
                <p>${esc(t('SplitNoSelection'))}</p>
            </div>`;
        }
        const acts = itemActions(item);
        const actions = acts.length
            ? acts.map((a) => {
                const dis = a.disabled ? ' disabled' : '';
                const title = a.disabled ? ` title="${esc(t(a.disabledReasonKey || 'BlockedBanner'))}"` : '';
                return `<button type="button" class="btn btn-sm btn-${a.primary ? '' : 'label-'}${a.kind}"${dis}${title} data-wcn-action="${a.key}" data-wcn-id="${item.id}">` +
                    `${esc(t(a.labelKey))}</button>`;
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
        const sys = item.systemState && SYSSTATE[item.systemState];
        const sysAction = item.systemState === 'record-changed'
            ? `<button type="button" class="btn btn-sm btn-label-warning" data-wcn-refresh-source="${item.id}">${esc(t('RefreshSource'))}</button>`
            : item.systemState === 'source-unreachable'
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

        return `<div class="wcn-detail" data-wcn-detail="${item.id}">
            <div class="wcn-detail-head">
                <div class="wcn-detail-source">
                    ${chip('module', 'bx-cube', item.sourceModule, sourceTitle(item))}
                    ${item.sourceModuleId ? chip('secondary', 'bx-hash', item.sourceModuleId, item.sourceModuleName) : ''}
                    ${chip('type', item.typeIcon, typeLabel(item))}
                    <span class="wcn-badge wcn-badge-${STATUS_KIND[item.status]}">${esc(statusLabel(item))}</span>
                </div>
                <h5 class="wcn-detail-title">${esc(item.title)}</h5>
                <div class="wcn-detail-chips">
                    ${chip(SLA_KIND[item.slaState], 'bx-time-five', slaLabel(item))}
                    ${chip(PRIORITY_KIND[item.priority], 'bx-flag', priorityLabel(item))}
                    ${chip('role', 'bx-user-check', t(ROLE_KEY[item.viewerRole] || item.viewerRole))}
                </div>
            </div>

            ${renderStepBar(item)}

            ${(item.itemType === 'task' && item.lifecycle === 'PendingReview')
                ? `<div class="wcn-review-note"><i class="bx bx-hourglass"></i><span>${esc(t('AwaitingReview'))}</span></div>`
                : ''}
            ${sysBanner}
            ${blockedBanner}
            ${waitingNote}
            ${snoozeNote}
            <div class="wcn-detail-actions" role="group" aria-label="${esc(t('ActionsLabel'))}">${actions}</div>
            ${personal}

            ${renderPlanDates(item)}

            <div class="wcn-detail-section">
                <h6 class="wcn-detail-h6">${esc(t('DetailSummary'))}</h6>
                <p class="wcn-detail-summary">${esc(item.summary)}</p>
            </div>

            ${renderChecklist(item)}
            ${renderSubtasks(item)}
            ${renderTimesheet(item)}
            ${renderDependencies(item)}
            ${renderAttachments(item)}

            <div class="wcn-detail-section">
                <h6 class="wcn-detail-h6">${esc(t('DetailContext'))}</h6>
                <div class="wcn-meta-grid">
                    ${meta('DetailRequester', item.requester)}
                    ${meta('DetailAssignee', item.assignee || '—')}
                    ${meta('DetailNativeStatus', item.nativeStatus)}
                    ${meta('DetailSourceId', item.sourceId)}
                    ${meta('DetailModuleName', item.sourceModuleName || item.sourceModule)}
                    ${meta('DetailModuleId', item.sourceModuleId || t('SourceIdentityPending'))}
                    ${meta('DetailSourceType', item.sourceObjectType || item.sourceType)}
                    ${meta('DetailActionDepth', t(item.actionDepth === 'deeplink' ? 'ActionDepthDeeplink' : 'ActionDepthInline'))}
                    ${meta('DetailSourceVersion', item.sourceVersion || '—')}
                </div>
                <button type="button" class="btn btn-sm btn-label-primary wcn-opensource" data-wcn-open="${item.id}" aria-label="${esc(tf('OpenSourceAria', item.sourceModuleName || item.sourceModule, item.sourceId))}">
                    <i class="bx bx-link-external"></i><span>${esc(t('DetailOpenSource'))}</span>
                </button>
            </div>

            ${renderNote(item)}

            <div class="wcn-detail-section">
                <h6 class="wcn-detail-h6">${esc(t('ActivityLabel'))}</h6>
                ${renderComposer(item)}
                <ul class="wcn-audit">${auditRows}</ul>
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

    const sortIndicator = (key) => state.sortKey === key
        ? `<i class="bx ${state.sortDir === 'asc' ? 'bx-chevron-up' : 'bx-chevron-down'}"></i>` : '';

    const th = (key, labelKey) => {
        const sorted = state.sortKey === key;
        const ariaSort = sorted ? (state.sortDir === 'asc' ? 'ascending' : 'descending') : 'none';
        return `<th class="wcn-th${sorted ? ' sorted' : ''}" aria-sort="${ariaSort}">
            <button type="button" class="wcn-thbtn" data-wcn-sort="${key}" aria-label="${esc(tf('SortBy', t(labelKey)))}">${esc(t(labelKey))} ${sortIndicator(key)}</button>
        </th>`;
    };

    const renderTable = (items) => {
        if (!items.length) { return emptyState(); }
        const sorter = SORTERS[state.sortKey] || SORTERS.sla;
        const sorted = items.slice().sort(sorter);
        if (state.sortDir === 'desc') { sorted.reverse(); }
        state.visibleOrder = sorted.map((i) => i.id);

        // Prune selection to what's visible.
        Array.from(state.tableSelected).forEach((id) => { if (state.visibleOrder.indexOf(id) < 0) { state.tableSelected.delete(id); } });
        const allSel = sorted.length > 0 && sorted.every((i) => state.tableSelected.has(i.id));

        const body = sorted.map((item) => `<tr class="wcn-tr${state.tableSelected.has(item.id) ? ' selected' : ''}${state.bulkFailedIds.has(item.id) ? ' wcn-tr-failed' : ''}" data-wcn-row="${item.id}" tabindex="0" role="button" aria-label="${esc(tf('TableOpenRow', item.title))}">
            <td class="wcn-td-check"><input type="checkbox" class="form-check-input" data-wcn-check="${item.id}"${state.tableSelected.has(item.id) ? ' checked' : ''} aria-label="${esc(item.title)}"></td>
            <td>${chip('type', item.typeIcon, typeLabel(item))}</td>
            <td class="wcn-td-title">${esc(item.title)}${state.bulkFailedIds.has(item.id) ? ` <span class="wcn-fail-tag"><i class="bx bx-x-circle"></i>${esc(t('BulkRowFailed'))}</span>` : ''}</td>
            <td>${esc(item.sourceModule)}</td>
            <td><span class="wcn-badge wcn-badge-${STATUS_KIND[item.status]}">${esc(statusLabel(item))}</span></td>
            <td>${chip(PRIORITY_KIND[item.priority], 'bx-flag', priorityLabel(item))}</td>
            <td>${chip(SLA_KIND[item.slaState], 'bx-time-five', slaLabel(item))}</td>
            <td>${esc(item.requester)}</td>
        </tr>`).join('');

        return `<div class="wcn-tablewrap">
            <table class="wcn-table">
                <caption class="visually-hidden">${esc(t('TableCaption'))}</caption>
                <thead><tr>
                    <th class="wcn-td-check"><input type="checkbox" class="form-check-input" data-wcn-check-all${allSel ? ' checked' : ''} aria-label="${esc(t('SelectAll'))}"></th>
                    ${th('type', 'ColType')}
                    ${th('title', 'ColTitle')}
                    ${th('module', 'ColModule')}
                    ${th('status', 'ColStatus')}
                    ${th('priority', 'ColPriority')}
                    ${th('sla', 'ColSla')}
                    ${th('requester', 'ColRequester')}
                </tr></thead>
                <tbody>${body}</tbody>
            </table>
            ${bulkBar(sorted)}
        </div>`;
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
        const filtered = state.moduleFilter !== 'all' || state.priorityFilter !== 'all' || state.modeFilter !== 'all'
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

    const renderErrorState = () => `<div class="wcn-system-page wcn-system-error" role="alert">
        <i class="bx bx-error-circle"></i><h5>${esc(t('ErrorTitle'))}</h5><p>${esc(t('ErrorDesc'))}</p>
        <button type="button" class="btn btn-sm btn-primary" data-wcn-retry>${esc(t('Retry'))}</button>
    </div>`;

    const renderUnsafe = () => {
        const root = document.getElementById('wcnApp');
        if (!root) { return; }
        const snap = captureFocus();
        if (state.loadState === 'loading') { root.innerHTML = renderLoadingState(); return; }
        if (state.loadState === 'error') { root.innerHTML = renderErrorState(); return; }
        const items = activeItems();
        let main;
        switch (state.view) {
            case 'split': main = renderSplit(items); break;
            case 'table': main = renderTable(items); break;
            case 'kanban': main = renderKanban(); break;
            case 'calendar': main = renderCalendar(); break;
            case 'focus': main = renderFocus(items); break;
            default: main = renderList(items);
        }
        const sidePanel = state.agendaOpen ? `<aside id="wcnSidePanel" class="wcn-sidepanel" aria-label="${esc(t('AgendaTitle'))}">${renderAgenda()}</aside>`
                        : state.notesOpen ? `<aside id="wcnSidePanel" class="wcn-sidepanel" aria-label="${esc(t('NotesPanelTitle'))}">${renderNotes()}</aside>`
                        : '';
                        
        root.innerHTML = buildHeader() + buildDelegationBanner() + buildTabs() + buildSegments() + buildGroupSelector() + buildChips() + buildToolbar()
            + `<div class="wcn-hint"><i class="bx bx-keyboard"></i><span>${esc(t('KeyboardHint'))}</span></div>`
            + `<div class="wcn-layout-wrap">`
            + `<div id="wcn-main-panel" class="wcn-main" role="tabpanel" aria-labelledby="wcn-tab-${state.tab}" tabindex="0">${main}</div>`
            + sidePanel
            + `</div>`;
        setupTimerTick();
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

    // ── Toast (self-contained, a11y live region) ──────────────────────────────
    const toast = (message, options) => {
        let region = document.getElementById('wcnToast');
        if (!region) {
            region = document.createElement('div');
            region.id = 'wcnToast';
            region.className = 'wcn-toast';
            region.setAttribute('role', 'status');
            region.setAttribute('aria-live', 'polite');
            document.body.appendChild(region);
        }
        const node = document.createElement('div');
        node.className = 'wcn-toast-item';
        node.innerHTML = `<i class="bx bx-check"></i><span>${esc(message)}</span>` +
            (options && options.actionLabel ? `<button type="button" class="wcn-toast-action">${esc(options.actionLabel)}</button>` : '');
        region.appendChild(node);
        const dismiss = () => { node.classList.add('leaving'); global.setTimeout(() => node.remove(), 300); };
        const actionButton = node.querySelector('.wcn-toast-action');
        if (actionButton && options && typeof options.onAction === 'function') {
            actionButton.addEventListener('click', () => { options.onAction(); dismiss(); }, { once: true });
        }
        global.setTimeout(dismiss, options && options.duration ? options.duration : 5000);
    };

    // ── Lifecycle state machine (mock, spec v2 §4/§5) ─────────────────────────
    // Tab membership is derived from assignmentMode + claimed (data.tabFor), never
    // hard-coded from lifecycle — so a claimed pool item lands in "İşlerim".
    const setLifecycle = (item, lifecycle, native) => {
        item.lifecycle = lifecycle;
        item.status = data.statusFor(lifecycle);
        if (native) { item.nativeStatus = native; }
        item.tab = data.tabFor(item);
    };

    // Returns the outcome kind so the toast can explain what happened.
    const applyTransition = (item, key) => {
        switch (key) {
            // Triage-inbox admission — take on a directly-assigned item; it moves
            // from the Inbox to İşlerim but stays at its current lifecycle stage.
            case 'accept':
                item.accepted = true;
                item.assignee = item.assignee || data.currentUser.name;
                item.tab = data.tabFor(item);
                return 'moved';
            // Pool admission — claim a group-queue item / accept an offered one.
            case 'claim':
            case 'acceptOffer':
                item.claimed = true;
                item.assignee = data.currentUser.name;
                if (item.itemType === 'review') { setLifecycle(item, 'InProgress', 'In Review'); }
                else if (item.itemType === 'task') { setLifecycle(item, 'Open', 'Open'); }
                else { setLifecycle(item, 'InProgress', 'In Progress'); }   // issue / exception
                return key === 'claim' ? 'claimed' : 'moved';
            case 'decline':
                item.dismissed = true; return 'removed';
            case 'release':
                // Drop a claimed group-queue item back to the pool for others.
                item.claimed = false; item.assignee = null;
                setLifecycle(item, 'PendingAcceptance', 'Unassigned — Ops Queue');
                return 'released';
            case 'approve': setLifecycle(item, 'Done', 'Approved'); return 'resolved';
            case 'signoff': setLifecycle(item, 'Done', 'Signed off'); return 'resolved';
            case 'resolve': setLifecycle(item, 'Done', 'Resolved'); return 'resolved';
            case 'start':
            case 'resume':
                setLifecycle(item, 'InProgress', 'In Progress');
                item.timesheet = item.timesheet || { running: false, startedAt: null, loggedMinutes: 0 };
                item.timesheet.running = true; item.timesheet.startedAt = Date.now();
                return 'timerStart';
            case 'pause':
                foldTimer(item);
                return 'timerPause';
            case 'complete':
                foldTimer(item);
                if (item.reviewRequired) { setLifecycle(item, 'PendingReview', 'Pending Review'); return 'toReview'; }
                setLifecycle(item, 'Done', 'Closed'); return 'resolved';
            case 'inquire':
                // Information request round-trip — item parks in Waiting (waiting-on).
                setLifecycle(item, 'Waiting', 'Waiting for Information'); return 'updated';
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
            case 'claimed': toast(tf('ToastClaimed', item.sourceId)); break;
            case 'released': toast(tf('ToastReleased', item.sourceId)); break;
            case 'moved': toast(tf('ToastMovedToWorkCenter', label)); break;
            case 'removed': toast(tf('ToastItemRemoved', label)); break;
            case 'toReview': toast(tf('ToastSentToReview', item.sourceId)); break;
            case 'timerStart': toast(tf('ToastTimerStarted', item.sourceId)); break;
            case 'timerPause': toast(tf('ToastTimerPaused', formatMinutes(item.timesheet.loggedMinutes))); break;
            case 'resolved': toast(tf('ToastAction', label)); break;
            default: toast(reason ? tf('ToastActionReason', label, reason) : tf('ToastAction', label));
        }
    };

    const applyAction = (item, action, reason) => {
        const label = t(action.labelKey);
        const snapshot = typeof global.structuredClone === 'function'
            ? global.structuredClone(item) : JSON.parse(JSON.stringify(item));
        const prevOrder = state.visibleOrder.slice();
        const prevIdx = prevOrder.indexOf(item.id);
        const outcome = applyTransition(item, action.key);
        const sla = data.computeSla(item.dueAt);
        item.slaState = sla.state; item.slaDiffDays = sla.diffDays;
        item.isUnread = false;
        item.activity = item.activity || [];
        item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: label, ago: 0 });
        state.tableSelected.delete(item.id);

        // a/r loop: if the acted item leaves the current tab's list, pre-select a
        // neighbour so the split view advances instead of jumping to the top.
        if (state.view === 'split' && (outcome === 'moved' || outcome === 'claimed' || outcome === 'released' || outcome === 'removed' || outcome === 'toReview' || outcome === 'updated')) {
            state.selectedId = prevOrder[prevIdx + 1] || prevOrder[prevIdx - 1] || null;
        }
        render();
        const undoable = action.confirm || action.reason || ['approve', 'signoff', 'resolve', 'complete'].indexOf(action.key) >= 0;
        if (undoable) {
            toast(reason ? tf('ToastActionReason', label, reason) : tf('ToastAction', label), {
                actionLabel: t('Undo'),
                duration: 8000,
                onAction: () => {
                    Object.keys(item).forEach((key) => { delete item[key]; });
                    Object.assign(item, snapshot);
                    state.selectedId = item.id;
                    render();
                    toast(t('UndoSuccess'));
                }
            });
        } else {
            toastForOutcome(outcome, label, reason, item);
        }
    };

    // Plan / re-plan — sets the PERSONAL planned date (spec v2 §4), which is
    // distinct from the source due date; SLA (source) is never overwritten.
    const applyPlan = (item, dateStr, label) => {
        item.plannedDate = dateStr;
        if (item.lifecycle === 'Open') { setLifecycle(item, 'Planned', item.nativeStatus); }
        item.isUnread = false;
        item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: label, ago: 0 });
        render();
        toast(tf('ToastPlanned', item.sourceId, dateStr));
    };

    const openDatePicker = (item, action) => {
        const label = t(action.labelKey);
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

    // Log time — manual minutes entry into the timesheet (task only).
    const openLogTime = (item, action) => {
        const label = t(action.labelKey);
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

    // Snooze — park an item until a personal date (moves it to "Bekleyen"); the
    // same button un-snoozes. Pure personal overlay; no source state changes.
    const toggleSnooze = (item) => {
        if (!item) { return; }
        if (item.snoozedUntil && item.snoozedUntil > data.todayIso) {
            item.snoozedUntil = null;
            item.tab = data.tabFor(item);
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Unsnooze'), ago: 0 });
            render();
            toast(tf('ToastUnsnoozed', item.sourceId));
            return;
        }
        const apply = (dateStr) => {
            item.snoozedUntil = dateStr;
            item.tab = data.tabFor(item);
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t('Snooze'), ago: 0 });
            const prevOrder = state.visibleOrder.slice();
            const prevIdx = prevOrder.indexOf(item.id);
            if (state.view === 'split') { state.selectedId = prevOrder[prevIdx + 1] || prevOrder[prevIdx - 1] || null; }
            render();
            toast(tf('ToastSnoozed', item.sourceId, dateStr));
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

    const openSelfTask = () => {
        global.Swal.fire({
            title: t('NewSelfTask'),
            html: `<input id="wcnNewTitle" class="form-control mb-2" placeholder="${esc(t('NewTaskTitlePlaceholder'))}">
                <input id="wcnNewDate" class="form-control mb-2" type="date">
                <select id="wcnNewPriority" class="form-select">
                    <option value="high">${esc(t('PriorityHigh'))}</option>
                    <option value="medium" selected>${esc(t('PriorityMedium'))}</option>
                    <option value="low">${esc(t('PriorityLow'))}</option>
                </select>`,
            showCancelButton: true, confirmButtonText: t('NewCreate'), cancelButtonText: t('ReasonCancel'),
            preConfirm: () => {
                const title = document.getElementById('wcnNewTitle').value.trim();
                if (!title) { global.Swal.showValidationMessage(t('NewTaskTitlePlaceholder')); return false; }
                return { title, date: document.getElementById('wcnNewDate').value, priority: document.getElementById('wcnNewPriority').value };
            }
        }).then((res) => { if (res.isConfirmed && res.value) { createSelfTask(res.value); } });
    };

    const createSelfTask = (v, origin) => {
        const id = 'WC-SELF-' + (state.items.length + 1);
        const sla = data.computeSla(v.date || null);
        const provider = data.sourceProviders && data.sourceProviders.PersonalTask;
        const item = {
            id, sourceModule: origin && origin.sourceModule ? origin.sourceModule : t('SelfSource'), sourceType: 'PersonalTask', sourceObjectType: 'PersonalTask', sourceId: id,
            sourceModuleId: provider ? provider.moduleId : 'MOD-0024', sourceModuleName: provider ? provider.moduleName : 'Task & Checklist Engine',
            sourceVersion: 'v1', etag: `mock-${id}`, actionDepth: 'inline',
            itemType: 'task', assignmentMode: 'direct', claimed: true, accepted: true, startedOnce: false,
            lifecycle: 'Open', nativeStatus: t('SelfSource'), status: data.status.IN_PROGRESS,
            capabilities: ['planning', 'execution', 'timeTracking', 'checklist', 'activity', 'informationRequest'],
            title: v.title, summary: t('SelfTaskSummary'),
            priority: v.priority, requester: data.currentUser.name, assignee: data.currentUser.name, viewerRole: 'Owner',
            dueAt: v.date || null, plannedDate: null, scope: 'mine', delegator: null, group: null, systemState: null,
            slaState: sla.state, slaDiffDays: sla.diffDays, typeIcon: 'bx-task',
            isUnread: false, pinned: false, escalated: false, reviewRequired: false,
            snoozedUntil: null, waitingOn: null, note: null, blockedState: null,
            dependencies: null, checklist: null, subtasks: null, stages: null, attachments: null,
            activity: [{ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditSelfCreated', ago: 0 }],
            timesheet: { running: false, startedAt: null, loggedMinutes: 0 },
            deepLink: '#', dismissed: false
        };
        item.tab = data.tabFor(item);
        state.items.push(item);
        state.tab = 'islerim'; state.segment = 'aktif'; state.selectedId = id; state.view = 'split';
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
                toast(tf('ToastCreateInSource', res.value));
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

    const convertGlobalNote = (note) => {
        if (!note || note.converted) { return; }
        note.converted = true;
        createSelfTask({ title: note.text, date: null, priority: 'medium' }, { sourceModule: t('NotesSource') });
    };

    const performAction = (item, actionKey) => {
        const action = actionByKey(item, actionKey);
        if (!item || !action || action.disabled) { return; }
        if (action.input === 'date') { openDatePicker(item, action); return; }
        if (action.input === 'minutes') { openLogTime(item, action); return; }

        // Reason-capturing action (reject/return/inquire/dispute/delegate/reassign):
        // a mandatory-rationale textarea, which also serves as the confirm step.
        if (action.reason) {
            if (!global.Swal) { return; }
            global.Swal.fire({
                title: t(action.labelKey),
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
                title: t(action.labelKey),
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
            item.isUnread = false;
            item.activity.push({ actor: data.currentUser.name, kind: 'event', eventKey: 'AuditActionStamp', actionLabel: t(action.labelKey), ago: 0 });
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
                    if (bar) { bar.style.width = pct + '%'; }
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
        })) { toast(t('BulkNoCommonAction')); return; }
        const label = sample ? t(sample.labelKey) : '';
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
            if (focusedItem) { focusedItem.isUnread = false; }
            state.view = 'split'; render(); return;
        }

        const item = itemById(state.selectedId);
        if (!item) { return; }
        if (key === 'enter' || key === 'o') {
            event.preventDefault();
            if (state.view !== 'split') { state.view = 'split'; render(); }
            return;
        }
        if (key === 'a') { const a = actionByRole(item, 'accept'); if (a) { event.preventDefault(); performAction(item, a.key); } return; }
        if (key === 'r') { const r = actionByRole(item, 'reject'); if (r) { event.preventDefault(); performAction(item, r.key); } return; }
    };

    // ── Event delegation ──────────────────────────────────────────────────────
    const onClick = (event) => {
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
            state.tab = tabEl.getAttribute('data-wcn-tab');
            state.segment = (SEGMENTS[state.tab] || ['aktif'])[0];   // reset to first segment
            state.group = 'all';
            state.selectedId = null; state.tableSelected.clear(); state.bulkFailedIds.clear();
            render(); return;
        }

        if (event.target.closest('[data-wcn-retry]')) {
            state.loadState = 'loading'; state.loadError = null; render();
            global.setTimeout(() => { state.loadState = 'ready'; render(); }, 250);
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

        // Type / signal filter chips — multi-select toggle.
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

        if (event.target.closest('[data-wcn-new]')) { openNew(); return; }

        const viewEl = event.target.closest('[data-wcn-view]');
        if (viewEl) { state.view = viewEl.getAttribute('data-wcn-view'); render(); return; }

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
            if (item) { item.pinned = !item.pinned; render(); toast(tf(item.pinned ? 'ToastPinned' : 'ToastUnpinned', item.sourceId)); }
            return;
        }

        const snoozeEl = event.target.closest('[data-wcn-snooze]');
        if (snoozeEl) {
            event.stopPropagation();
            toggleSnooze(itemById(snoozeEl.getAttribute('data-wcn-snooze')));
            return;
        }

        // ── Depth-block interactions (Faz 2) ──────────────────────────────────
        const checkItemEl = event.target.closest('[data-wcn-check-item]');
        if (checkItemEl) {
            const [id, cid] = checkItemEl.getAttribute('data-wcn-check-item').split(':');
            const it = itemById(id);
            const c = it && it.checklist && it.checklist.items.find((x) => x.id === cid);
            if (c) { c.done = !c.done; render(); }
            return;
        }
        const subEl = event.target.closest('[data-wcn-subtask]');
        if (subEl) {
            const [id, sid] = subEl.getAttribute('data-wcn-subtask').split(':');
            const it = itemById(id);
            const s = it && it.subtasks && it.subtasks.items.find((x) => x.id === sid);
            if (s) { s.status = s.status === 'done' ? 'not-started' : 'done'; render(); }
            return;
        }
        const subAddEl = event.target.closest('[data-wcn-subtask-add]');
        if (subAddEl) {
            const it = itemById(subAddEl.getAttribute('data-wcn-subtask-add'));
            const inp = document.querySelector('#wcnApp [data-wcn-subtask-input]');
            const val = inp && inp.value.trim();
            if (it && val) { it.subtasks.items.push({ id: 'S' + (it.subtasks.items.length + 1), title: val, status: 'not-started' }); render(); }
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
        if (attachEl) { toast(tf('ToastAttachment', attachEl.getAttribute('data-wcn-attach'))); return; }

        const openEl = event.target.closest('[data-wcn-open]');
        if (openEl) {
            const item = itemById(openEl.getAttribute('data-wcn-open'));
            if (item && item.deepLink) {
                global.open(item.deepLink, '_blank', 'noopener,noreferrer');
                toast(tf('ToastOpenSource', item.sourceModule, item.sourceId));
            }
            return;
        }

        const refreshSourceEl = event.target.closest('[data-wcn-refresh-source]');
        if (refreshSourceEl) {
            const item = itemById(refreshSourceEl.getAttribute('data-wcn-refresh-source'));
            if (!item) { return; }
            const was = item.systemState;
            refreshSourceEl.setAttribute('disabled', 'disabled');
            global.setTimeout(() => {
                item.systemState = null;
                item.sourceVersion = 'v' + (parseInt(String(item.sourceVersion || 'v1').replace(/\D/g, ''), 10) + 1);
                item.etag = `mock-${item.id}-${Date.now()}`;
                render(); toast(t(was === 'source-unreachable' ? 'SourceRetrySucceeded' : 'SourceRefreshed'));
            }, 350);
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
            state.moduleFilter = 'all'; state.priorityFilter = 'all'; state.modeFilter = 'all';
            state.typeFilter.clear(); state.signalFilter.clear(); state.search = ''; render(); return;
        }

        // Row selection (skip when clicking the checkbox cell in table).
        const rowEl = event.target.closest('[data-wcn-row]');
        if (rowEl && !event.target.closest('[data-wcn-check]') && !event.target.closest('.wcn-td-check')) {
            state.selectedId = rowEl.getAttribute('data-wcn-row');
            const it = itemById(state.selectedId);
            if (it) { it.isUnread = false; }
            if (state.view !== 'split') { state.view = 'split'; }
            render();
        }
    };

    const onChange = (event) => {
        const filterEl = event.target.closest('[data-wcn-filter]');
        if (filterEl) {
            const which = filterEl.getAttribute('data-wcn-filter');
            if (which === 'module') { state.moduleFilter = filterEl.value; }
            else if (which === 'priority') { state.priorityFilter = filterEl.value; }
            else if (which === 'mode') { state.modeFilter = filterEl.value; }
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
            render();
            const again = document.querySelector('#wcnApp [data-wcn-search]');
            if (again) { again.focus(); again.setSelectionRange(value.length, value.length); }
        }, 180);
    };

    // ── Boot ──────────────────────────────────────────────────────────────────
    const boot = () => {
        const root = document.getElementById('wcnApp');
        if (!root) { return; }
        hydrateStateFromUrl();
        document.addEventListener('click', onClick);
        document.addEventListener('change', onChange);
        document.addEventListener('input', onInput);
        document.addEventListener('keydown', onKeydown);
        render();
        global.setTimeout(() => {
            state.loadState = 'ready';
            render();
        }, 180);
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(window);
