'use strict';

/*
 * WorkCenterNext ("Görev Merkezi") — mock data + SOURCE-AGNOSTIC work-item
 * contract (spec v3). Frontend-only, zero backend.
 *
 * v3 CLEAN REBUILD — the axis law (Fable/ChatGPT/Gemini convergence):
 *   OWNERSHIP → tab · STATUS → segment (in-tab) · TYPE → filter chip.
 *
 *   tabFor() keys ONLY off ownership (accepted/claimed/done) — a status change
 *   (snooze / waiting-on / blocked) keeps the item in the SAME tab and just moves
 *   its segment. Tabs change only on ownership change (accept→İşlerim,
 *   complete→Geçmiş, release→Havuz). No cross-tab teleporting.
 *
 * Depth restored (old WorkCenter's strength) as capability-declared data:
 *   stages · planning · execution · timeTracking · checklist · subtasks(full|
 *   readonly) · dependencies(typed FS/FF/SS/SF, readonly) · activity · attachments.
 *   Rule: do-the-work in the aggregator, define-the-work in the source (deep-link).
 *   Typed dependencies are readonly AND feed blockedState (FS+unfinished →
 *   hard-block; SS+not-started → block).
 */
(function (global) {
    const TODAY_ISO = '2026-07-11';
    const TODAY = new Date(TODAY_ISO + 'T09:00:00');

    const CURRENT_USER = { name: 'Selin Aras', title: 'Operations PMO Lead' };
    const ON_BEHALF_OF = { name: 'Deniz Koç', title: 'Finance Controller' };
    // N-way scoped delegation (spec v3 §6) — Selin covers two people while they're out.
    const DELEGATORS = [
        { name: 'Deniz Koç', title: 'Finance Controller' },
        { name: 'Aylin Ersoy', title: 'Procurement Lead' }
    ];

    // Meetings are agenda context, not work items. They can produce a normal
    // follow-up task/review, keeping the WorkCenter intent model unambiguous.
    const MEETINGS = [
        { id: 'MTG-1001', title: 'Weekly Operations Sync', start: '14:00', end: '15:00', location: 'Teams', owner: 'Selin Aras' },
        { id: 'MTG-1002', title: 'Architecture Review', start: '16:30', end: '17:30', location: 'Atlas Room', owner: 'Mert Aksoy' }
    ];
    const NOTES = [
        { id: 'NOTE-1001', text: 'Collect department approvals for the Q3 budget revisions.', ageKey: 'TimeToday', converted: false },
        { id: 'NOTE-1002', text: 'Confirm the server migration calendar with the infrastructure team.', ageKey: 'TimeYesterday', converted: false }
    ];

    const STATUS = {
        PENDING: 'Pending', IN_PROGRESS: 'In Progress', WAITING: 'Waiting',
        DONE: 'Done', CANCELLED: 'Cancelled'
    };
    const LIFECYCLE_STATUS = {
        PendingApproval: STATUS.PENDING, PendingAcceptance: STATUS.PENDING,
        Open: STATUS.IN_PROGRESS, Planned: STATUS.IN_PROGRESS, InProgress: STATUS.IN_PROGRESS,
        Waiting: STATUS.WAITING, PendingReview: STATUS.IN_PROGRESS, Done: STATUS.DONE, Cancelled: STATUS.CANCELLED
    };
    const statusFor = (lifecycle) => LIFECYCLE_STATUS[lifecycle] || STATUS.IN_PROGRESS;

    const clone = (value) => (typeof global.structuredClone === 'function')
        ? global.structuredClone(value) : JSON.parse(JSON.stringify(value));

    const addDays = (days) => {
        const d = new Date(TODAY); d.setDate(d.getDate() + days);
        return d.toISOString().slice(0, 10);
    };

    const computeSla = (dueAt) => {
        if (!dueAt) { return { state: 'no-sla', diffDays: null }; }
        const due = new Date(dueAt + 'T00:00:00');
        const base = new Date(TODAY.getFullYear(), TODAY.getMonth(), TODAY.getDate());
        const diffDays = Math.round((due - base) / 86400000);
        let state = 'on-track';
        if (diffDays < 0) { state = 'overdue'; } else if (diffDays <= 2) { state = 'due-soon'; }
        return { state, diffDays };
    };

    // Typed dependencies → blockedState (spec v3). A predecessor that must finish
    // first (FS) and isn't done — or must start first (SS) and hasn't — blocks us.
    const computeBlocked = (deps) => {
        if (!Array.isArray(deps)) { return null; }
        const blockers = deps.filter((d) => d.direction === 'pred' && (
            (d.type === 'FS' && d.state !== 'done') ||
            (d.type === 'SS' && d.state === 'not-started')));
        if (!blockers.length) { return null; }
        return {
            blocked: true,
            blockedBy: blockers.map((b) => ({ title: b.title, id: b.id, type: b.type })),
            reasonKey: 'BlockedByDependency'
        };
    };

    // ── OWNERSHIP → tab (Fable's law: status never changes the tab) ────────────
    const tabFor = (item) => {
        if ((item.assignmentMode === 'groupQueue' || item.assignmentMode === 'offered') && !item.claimed) { return 'havuz'; }
        if ((item.assignmentMode === 'direct' || item.assignmentMode === 'approval') && !item.accepted) { return 'inbox'; }
        return 'islerim';
    };

    // ── STATUS → segment (only within İşlerim) ────────────────────────────────
    // Aktif = actively workable · Bekleyen = waiting-on / snoozed · Planlı = has a
    // future personal plan and not yet started. Status moves the segment, not the tab.
    const segmentFor = (item) => {
        if (item.status === STATUS.WAITING || (item.snoozedUntil && item.snoozedUntil > TODAY_ISO) || item.waitingOn) { return 'bekleyen'; }
        if (item.lifecycle === 'Planned' || (item.plannedDate && !item.startedOnce)) { return 'planli'; }
        return 'aktif';
    };

    // ── Action catalogue (spec v3 §6 — action safety metadata) ────────────────
    const ACT = {
        accept:      { key: 'accept',      labelKey: 'ActAccept',      kind: 'primary',   semanticType: 'accept',   primary: true, role: 'accept' },
        claim:       { key: 'claim',       labelKey: 'ActClaim',       kind: 'primary',   semanticType: 'claim',    primary: true, role: 'accept' },
        release:     { key: 'release',     labelKey: 'ActRelease',     kind: 'secondary', semanticType: 'release',  reason: true },
        acceptOffer: { key: 'acceptOffer', labelKey: 'ActAccept',      kind: 'primary',   semanticType: 'accept',   primary: true, role: 'accept' },
        decline:     { key: 'decline',     labelKey: 'ActReject',      kind: 'danger',    semanticType: 'decline',  role: 'reject', reason: true },
        approve:     { key: 'approve',     labelKey: 'ActApprove',     kind: 'success',   semanticType: 'approve',  primary: true, role: 'accept', confirm: true, bulk: true },
        reject:      { key: 'reject',      labelKey: 'ActReject',      kind: 'danger',    semanticType: 'reject',   role: 'reject', reason: true, bulk: true },
        inquire:     { key: 'inquire',     labelKey: 'ActRequestInfo', kind: 'warning',   semanticType: 'inquire',  reason: true },
        returnItem:  { key: 'return',      labelKey: 'ActReturn',      kind: 'warning',   semanticType: 'return',   role: 'reject', reason: true, bulk: true },
        delegate:    { key: 'delegate',    labelKey: 'ActDelegate',    kind: 'secondary', semanticType: 'delegate', reason: true },
        dispute:     { key: 'dispute',     labelKey: 'ActDispute',     kind: 'secondary', semanticType: 'dispute',  reason: true },
        reassign:    { key: 'reassign',    labelKey: 'ActReassign',    kind: 'secondary', semanticType: 'reassign', reason: true },
        signoff:     { key: 'signoff',     labelKey: 'ActSignOff',     kind: 'success',   semanticType: 'signoff',  primary: true, role: 'accept', confirm: true, bulk: true },
        resolve:     { key: 'resolve',     labelKey: 'ActResolve',     kind: 'success',   semanticType: 'resolve',  primary: true, role: 'accept', confirm: true, bulk: true },
        plan:        { key: 'plan',        labelKey: 'ActPlan',        kind: 'primary',   semanticType: 'plan',   input: 'date' },
        replan:      { key: 'replan',      labelKey: 'ActReplan',      kind: 'secondary', semanticType: 'plan',   input: 'date' },
        start:       { key: 'start',       labelKey: 'ActStart',       kind: 'primary',   semanticType: 'start',  primary: true, role: 'accept' },
        resume:      { key: 'resume',      labelKey: 'ActResume',      kind: 'primary',   semanticType: 'start',  primary: true, role: 'accept' },
        pause:       { key: 'pause',       labelKey: 'ActPause',       kind: 'warning',   semanticType: 'pause' },
        logTime:     { key: 'logTime',     labelKey: 'ActLogTime',     kind: 'info',      semanticType: 'logTime', input: 'minutes' },
        complete:    { key: 'complete',    labelKey: 'ActComplete',    kind: 'success',   semanticType: 'complete', primary: true, role: 'accept', confirm: true, bulk: true }
    };

    const has = (item, cap) => Array.isArray(item.capabilities) && item.capabilities.indexOf(cap) >= 0;

    // getActions(item) — assignmentMode + capabilities + lifecycle → action set.
    const getActions = (item) => {
        const { itemType, lifecycle, assignmentMode } = item;
        if (lifecycle === 'Done' || lifecycle === 'Cancelled') { return []; }

        if (assignmentMode === 'groupQueue' && !item.claimed) { return clone([ACT.claim]); }
        if (assignmentMode === 'offered' && !item.claimed) { return clone([ACT.acceptOffer, ACT.decline]); }

        if (itemType === 'approval') {
            return clone([ACT.approve, ACT.reject, ACT.inquire, ACT.returnItem, ACT.delegate]);
        }
        // Triage-inbox gate — a directly-assigned item I haven't taken on yet.
        if (assignmentMode === 'direct' && !item.accepted) {
            const gate = [ACT.accept];
            if (itemType === 'task' && has(item, 'planning')) { gate.push(ACT.plan); }
            gate.push(ACT.dispute, ACT.reassign);
            return clone(gate);
        }
        if (itemType === 'review') { return clone([ACT.signoff, ACT.returnItem, ACT.inquire]); }
        if (itemType === 'issue' || itemType === 'exception') { return clone([ACT.resolve, ACT.inquire, ACT.reassign]); }

        // TASK — capability-driven per lifecycle stage.
        const running = item.timesheet && item.timesheet.running;
        const blocked = item.blockedState && item.blockedState.blocked;
        let acts;
        switch (lifecycle) {
            case 'Open':
                acts = [has(item, 'planning') && ACT.plan, has(item, 'execution') && ACT.start, ACT.dispute, ACT.reassign]; break;
            case 'Planned':
                acts = [has(item, 'execution') && ACT.start, has(item, 'planning') && ACT.replan, ACT.dispute, ACT.reassign]; break;
            case 'InProgress':
                acts = [running ? ACT.pause : ACT.resume, has(item, 'timeTracking') && ACT.logTime,
                    has(item, 'execution') && ACT.complete, ACT.inquire, ACT.reassign]; break;
            case 'Waiting':
                acts = [has(item, 'execution') && ACT.resume, has(item, 'timeTracking') && ACT.logTime,
                    has(item, 'execution') && ACT.complete, ACT.inquire, ACT.reassign]; break;
            case 'PendingReview':
                acts = [ACT.signoff, ACT.returnItem]; break;
            default:
                acts = [ACT.reassign];
        }
        if (assignmentMode === 'groupQueue' && item.claimed) { acts.push(ACT.release); }
        const out = clone(acts.filter(Boolean));
        if (blocked) {
            out.forEach((a) => { if (a.semanticType === 'start') { a.disabled = true; a.disabledReasonKey = item.blockedState.reasonKey || 'BlockedBanner'; } });
        }
        return out;
    };

    const TYPE_ICON = { approval: 'bx-check-shield', task: 'bx-task', review: 'bx-search-alt', issue: 'bx-error-circle', exception: 'bx-error-alt' };

    // Reusable capability packs.
    const TASK_FULL = ['stages', 'planning', 'execution', 'timeTracking', 'checklist', 'subtasks', 'dependencies', 'activity', 'attachments', 'informationRequest'];

    // Blueprint-verified provider identities. The mock uses sourceType as its
    // adapter key so a friendly label can never silently masquerade as a
    // canonical MOD identity.
    const SOURCE_PROVIDERS = {
        MasterDataReview:  { moduleId: 'MOD-0049', moduleName: 'Master Data Management (MDM)' },
        ChecklistTask:     { moduleId: 'MOD-0181', moduleName: 'Cycle Counting' },
        ReconException:    { moduleId: 'MOD-0121', moduleName: 'Bank Reconciliation' },
        JournalApproval:   { moduleId: 'MOD-0118', moduleName: 'General Ledger (GL)' },
        PurchaseApproval:  { moduleId: 'MOD-0141', moduleName: 'Requisition & Purchase Orders' },
        DiscountApproval:  { moduleId: 'MOD-0156', moduleName: 'Price Lists & Discount Guardrails' },
        PaymentApproval:   { moduleId: 'MOD-0126', moduleName: 'Payments & Approvals' },
        CloseTask:         { moduleId: 'MOD-0122', moduleName: 'Period Close & Consolidation' },
        OnboardingTask:    { moduleId: 'MOD-0303', moduleName: 'Employee Onboarding' },
        Exception:         { moduleId: 'MOD-0128', moduleName: 'VAT/GST & Withholding' },
        DeviationTask:     { moduleId: 'MOD-0208', moduleName: 'CAPA' },
        FixReview:         { moduleId: 'MOD-0037', moduleName: 'Integration Monitoring & Reconciliation' },
        IncidentIssue:     { moduleId: 'MOD-0221', moduleName: 'Incident Management' },
        CountTask:         { moduleId: 'MOD-0181', moduleName: 'Cycle Counting' },
        SourcingTask:      { moduleId: 'MOD-0145', moduleName: 'Sourcing (RFQ/RFP)' },
        ContractReview:    { moduleId: 'MOD-0217', moduleName: 'Contract Lifecycle Management (CLM)', actionDepth: 'deeplink' },
        DecommTask:        { moduleId: 'MOD-0224', moduleName: 'Change Management (ITIL)', actionDepth: 'deeplink' },
        NormalizationTask: { moduleId: 'MOD-0049', moduleName: 'Master Data Management (MDM)' },
        PersonalTask:      { moduleId: 'MOD-0024', moduleName: 'Task & Checklist Engine' }
    };

    // Depth-data builders (keep mock items readable).
    const checklist = (arr) => ({ items: arr.map((c, i) => ({ id: 'C' + (i + 1), text: c[0], done: c[1] })) });
    const subtasks = (mode, arr) => ({ mode, items: arr.map((s, i) => ({ id: 'S' + (i + 1), title: s[0], status: s[1] })) });
    // Activity = single stream of events (l10n eventKey) + comments (literal text).
    const activity = (arr) => arr.map((a) => a[1] === 'comment'
        ? { actor: a[0], kind: 'comment', text: a[2], ago: a[3] }
        : { actor: a[0], kind: 'event', eventKey: a[2], ago: a[3] });

    // ── Mock items ────────────────────────────────────────────────────────────
    const rawItems = [
        // ── HAVUZ (claimable — groupQueue / offered) ──────────────────────────
        {
            id: 'WC-1002', sourceModule: 'MDM', sourceType: 'MasterDataReview', sourceId: 'VEND-58821',
            itemType: 'review', assignmentMode: 'offered', claimed: false, group: 'Veri Yönetimi',
            lifecycle: 'PendingAcceptance', nativeStatus: 'Offered — Data Steward',
            capabilities: ['reviewFlow', 'activity', 'attachments', 'informationRequest'],
            title: 'Review new vendor master record — Kavi Logistics',
            summary: 'Bank details and tax jurisdiction changed on a golden vendor record; accept to take the review.',
            priority: 'high', requester: 'Aylin Ersoy', assignee: null, viewerRole: 'Reviewer',
            dueAt: addDays(1), scope: 'mine', isUnread: true, pinned: true,
            attachments: [{ name: 'bank-letter.pdf', size: '210 KB' }, { name: 'tax-cert.pdf', size: '88 KB' }],
            activity: activity([['Aylin Ersoy', 'event', 'AuditReviewRequested', 2], ['System', 'event', 'AuditOffered', 2]])
        },
        {
            id: 'WC-1003', sourceModule: 'Warehouse Ops', sourceType: 'ChecklistTask', sourceId: 'CYC-3390',
            itemType: 'task', assignmentMode: 'groupQueue', claimed: false, group: 'Depo Ops',
            lifecycle: 'PendingAcceptance', nativeStatus: 'Unassigned — Ops Queue',
            capabilities: ['stages', 'planning', 'execution', 'checklist', 'activity'],
            title: 'Q3 cycle-count pilot kickoff checklist',
            summary: 'Group queue item for the Ops team: claim to own the warehouse cycle-count pilot kickoff.',
            priority: 'medium', requester: 'Levent Demir', assignee: null, viewerRole: 'Owner',
            dueAt: addDays(2), scope: 'mine', isUnread: false, pinned: false,
            checklist: checklist([['Confirm freeze window', false], ['Assign counters', false], ['Print count sheets', false]]),
            activity: activity([['Levent Demir', 'event', 'AuditSubmitted', 1], ['System', 'event', 'AuditQueued', 1]])
        },
        {
            id: 'WC-1012', sourceModule: 'Finance', sourceType: 'ReconException', sourceId: 'REC-2205',
            itemType: 'exception', assignmentMode: 'offered', claimed: false, group: 'Finans Ekibi',
            lifecycle: 'PendingAcceptance', nativeStatus: 'Offered — Open Exception',
            capabilities: ['activity', 'informationRequest', 'attachments'],
            title: 'Bank reconciliation break — main operating account',
            summary: 'Unmatched receipt of $9,140 on the operating account; accept to take ownership and action it.',
            priority: 'medium', requester: 'System', assignee: null, viewerRole: 'Owner',
            dueAt: addDays(2), scope: 'onBehalf', delegator: 'Deniz Koç', isUnread: false, pinned: false,
            activity: activity([['System', 'event', 'AuditSubmitted', 2], ['System', 'event', 'AuditOffered', 2]])
        },

        // ── GELEN KUTUSU — APPROVAL (decide in place) ─────────────────────────
        {
            id: 'WC-1001', sourceModule: 'Finance', sourceType: 'JournalApproval', sourceId: 'JE-2026-0442',
            itemType: 'approval', assignmentMode: 'approval', claimed: true,
            lifecycle: 'PendingApproval', nativeStatus: 'Awaiting Approver',
            capabilities: ['activity', 'attachments', 'informationRequest'],
            title: 'Approve month-end accrual journal (freight)',
            summary: 'Manual accrual of $184,200 for in-transit freight awaiting your sign-off before the close cut-off.',
            priority: 'high', requester: 'Emre Güneş', assignee: 'Selin Aras', viewerRole: 'Approver',
            dueAt: addDays(-1), scope: 'mine', isUnread: true, pinned: false, escalated: true,
            attachments: [{ name: 'accrual-detail.xlsx', size: '44 KB' }],
            activity: activity([['Emre Güneş', 'event', 'AuditSubmitted', 3], ['System', 'event', 'AuditRoutedTo', 3], ['System', 'event', 'AuditEscalated', 0]])
        },
        {
            id: 'WC-1005', sourceModule: 'Procurement', sourceType: 'PurchaseApproval', sourceId: 'PR-90514',
            itemType: 'approval', assignmentMode: 'approval', claimed: true, systemState: 'record-changed',
            lifecycle: 'PendingApproval', nativeStatus: 'Awaiting Approver',
            capabilities: ['activity', 'informationRequest'],
            title: 'Approve purchase requisition — IT hardware refresh',
            summary: 'Requisition of $46,900 for laptop refresh exceeds the auto-approval threshold.',
            priority: 'medium', requester: 'Burak Şahin', assignee: 'Selin Aras', viewerRole: 'Approver',
            dueAt: addDays(1), scope: 'mine', isUnread: false, pinned: false,
            activity: activity([['Burak Şahin', 'event', 'AuditSubmitted', 1], ['System', 'event', 'AuditRoutedTo', 1]])
        },
        {
            id: 'WC-1008', sourceModule: 'Sales', sourceType: 'DiscountApproval', sourceId: 'QUO-33218',
            itemType: 'approval', assignmentMode: 'approval', claimed: true,
            lifecycle: 'PendingApproval', nativeStatus: 'Awaiting Approver', bulkConflict: true,
            capabilities: ['activity', 'informationRequest'],
            title: 'Approve regional campaign discount release',
            summary: 'Discount of 18% on the EMEA campaign needs release approval before quotes go out.',
            priority: 'medium', requester: 'Can Yıldız', assignee: 'Selin Aras', viewerRole: 'Approver',
            dueAt: addDays(4), scope: 'mine', isUnread: false, pinned: false,
            activity: activity([['Can Yıldız', 'event', 'AuditSubmitted', 1]])
        },
        {
            id: 'WC-1011', sourceModule: 'Treasury', sourceType: 'PaymentApproval', sourceId: 'PAY-71190',
            itemType: 'approval', assignmentMode: 'approval', claimed: true,
            lifecycle: 'PendingApproval', nativeStatus: 'Awaiting Approver',
            capabilities: ['activity', 'attachments', 'informationRequest'],
            title: 'Approve outbound payment run — EU suppliers',
            summary: 'Batch of 42 supplier payments totalling €512,000 queued for the daily payment run.',
            priority: 'high', requester: 'Deniz Koç', assignee: 'Deniz Koç', viewerRole: 'Approver',
            dueAt: addDays(0), scope: 'onBehalf', delegator: 'Deniz Koç', isUnread: true, pinned: false,
            activity: activity([['System', 'event', 'AuditSubmitted', 1], ['System', 'event', 'AuditRoutedTo', 1]])
        },

        // ── GELEN KUTUSU — direct, freshly arrived (not yet accepted) ──────────
        {
            id: 'WC-1020', sourceModule: 'Finance', sourceType: 'CloseTask', sourceId: 'FIN-7790',
            itemType: 'task', assignmentMode: 'direct', claimed: true,
            lifecycle: 'Open', nativeStatus: 'Assigned — awaiting acceptance',
            capabilities: TASK_FULL.slice(),
            title: 'Reconcile prepaid expenses schedule',
            summary: 'Newly assigned close task; accept to take it on, then plan or start.',
            priority: 'medium', requester: 'Emre Güneş', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(2), scope: 'mine', isUnread: true, pinned: false,
            stages: [{ key: 'Assigned', label: 'Atandı' }, { key: 'InProgress', label: 'Devam' }, { key: 'Review', label: 'İnceleme' }, { key: 'Closed', label: 'Kapandı' }],
            checklist: checklist([['Pull prepaid GL balances', false], ['Match to amortization schedule', false], ['Post adjustment', false]]),
            subtasks: subtasks('full', [['Export prepaid ledger', 'not-started'], ['Reconcile variances', 'not-started']]),
            activity: activity([['Emre Güneş', 'event', 'AuditSubmitted', 0], ['System', 'event', 'AuditRoutedTo', 0]])
        },
        {
            id: 'WC-1010', sourceModule: 'HR', sourceType: 'OnboardingTask', sourceId: 'ONB-5521',
            itemType: 'task', assignmentMode: 'direct', claimed: true,
            lifecycle: 'Open', nativeStatus: 'Blocked — awaiting predecessor', reviewRequired: true,
            capabilities: ['stages', 'planning', 'execution', 'checklist', 'subtasks', 'dependencies', 'activity'],
            dependencies: [{ id: 'ONB-5519', title: 'Signed employment contract', type: 'FS', direction: 'pred', state: 'in-progress' }],
            title: 'Complete manager checklist for new joiner',
            summary: 'Provisioning, buddy assignment and first-week plan for the new operations analyst.',
            priority: 'low', requester: 'İdil Arı', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(6), scope: 'mine', isUnread: false, pinned: false,
            checklist: checklist([['Order laptop', false], ['Assign buddy', false], ['Book first-week 1:1', false]]),
            subtasks: subtasks('readonly', [['IT account provisioning', 'not-started'], ['Access badge', 'not-started']]),
            activity: activity([['İdil Arı', 'event', 'AuditSubmitted', 2], ['System', 'event', 'AuditBlocked', 2]])
        },

        // ── İŞLERİM — accepted/owned (direct) ─────────────────────────────────
        {
            id: 'WC-1004', sourceModule: 'Tax', sourceType: 'Exception', sourceId: 'VAT-7781',
            itemType: 'exception', assignmentMode: 'direct', claimed: true, accepted: true,
            lifecycle: 'InProgress', nativeStatus: 'In Progress',
            capabilities: ['execution', 'activity', 'attachments', 'informationRequest'],
            title: 'Export VAT mismatch on SAP handoff',
            summary: 'Automated reconciliation flagged a €12,430 VAT variance between the ERP and the customs filing.',
            priority: 'high', requester: 'System', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(-3), scope: 'mine', isUnread: true, pinned: false, escalated: true,
            activity: activity([['System', 'event', 'AuditSubmitted', 5], ['Selin Aras', 'comment', 'Checked customs export — variance is on line 7.', 2], ['System', 'event', 'AuditEscalated', 0]])
        },
        {
            id: 'WC-1006', sourceModule: 'Quality', sourceType: 'DeviationTask', sourceId: 'CAPA-2231',
            itemType: 'task', assignmentMode: 'direct', claimed: true, accepted: true,
            lifecycle: 'Waiting', nativeStatus: 'Waiting for Information', reviewRequired: true,
            capabilities: TASK_FULL.concat(['reviewFlow']), loggedMinutes: 30, waitingOn: 'QA Lab (Melis Yalçın)',
            title: 'Close deviation CAPA once evidence pack lands',
            summary: 'Waiting on QA to upload the signed lab report before the corrective action can be completed.',
            priority: 'high', requester: 'Melis Yalçın', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(-7), scope: 'mine', isUnread: false, pinned: false,
            stages: [{ key: 'Open', label: 'Açık' }, { key: 'InProgress', label: 'Devam' }, { key: 'Waiting', label: 'Bekliyor' }, { key: 'Review', label: 'İnceleme' }, { key: 'Closed', label: 'Kapandı' }],
            checklist: checklist([['Root-cause analysis', true], ['Corrective action defined', true], ['Evidence uploaded', false], ['Effectiveness check', false]]),
            attachments: [{ name: 'rca-report.docx', size: '120 KB' }],
            activity: activity([['Melis Yalçın', 'event', 'AuditSubmitted', 12], ['System', 'event', 'AuditReminderSent', 1]])
        },
        {
            id: 'WC-1007', sourceModule: 'Payroll Integrations', sourceType: 'FixReview', sourceId: 'INT-4471',
            itemType: 'review', assignmentMode: 'direct', claimed: true, accepted: true,
            lifecycle: 'PendingReview', nativeStatus: 'Pending Review',
            capabilities: ['reviewFlow', 'activity', 'attachments', 'informationRequest'],
            title: 'Sign off payroll interface timeout fix',
            summary: 'Load-test evidence attached; complete the review before the release window closes.',
            priority: 'high', requester: 'Mert Aksoy', assignee: 'Selin Aras', viewerRole: 'Reviewer',
            dueAt: addDays(0), scope: 'mine', isUnread: true, pinned: false,
            attachments: [{ name: 'load-test.html', size: '2.1 MB' }],
            activity: activity([['Mert Aksoy', 'event', 'AuditReviewRequested', 2], ['System', 'event', 'AuditRoutedTo', 2]])
        },
        {
            id: 'WC-1009', sourceModule: 'IT Service', sourceType: 'IncidentIssue', sourceId: 'INC-88120',
            itemType: 'issue', assignmentMode: 'direct', claimed: true, accepted: true, systemState: 'source-unreachable',
            lifecycle: 'InProgress', nativeStatus: 'In Progress',
            capabilities: ['execution', 'activity', 'informationRequest', 'attachments'],
            title: 'Investigate pricing rule hotfix regression',
            summary: 'Post-deploy monitoring flagged an approval-matrix regression on the pricing engine.',
            priority: 'high', requester: 'Burcu Korkmaz', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(1), scope: 'mine', isUnread: false, pinned: false,
            activity: activity([['Burcu Korkmaz', 'event', 'AuditSubmitted', 3], ['Selin Aras', 'comment', 'Reproduced on staging; bisecting the rule set.', 1]])
        },
        {
            id: 'WC-1016', sourceModule: 'Warehouse Ops', sourceType: 'CountTask', sourceId: 'CYC-3402',
            itemType: 'task', assignmentMode: 'direct', claimed: true, accepted: true,
            lifecycle: 'Planned', nativeStatus: 'Planned', plannedDate: addDays(3),
            capabilities: TASK_FULL.slice(),
            dependencies: [{ id: 'CYC-3402-F', title: 'Aisle D freeze window', type: 'SS', direction: 'pred', state: 'not-started' }],
            title: 'Run bin-location audit for aisle D',
            summary: 'Scheduled stock audit; can start only once the aisle freeze window opens.',
            priority: 'medium', requester: 'Levent Demir', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(3), scope: 'mine', isUnread: false, pinned: false,
            stages: [{ key: 'Planned', label: 'Planlandı' }, { key: 'InProgress', label: 'Devam' }, { key: 'Closed', label: 'Kapandı' }],
            checklist: checklist([['Confirm freeze window', false], ['Scan bins A–M', false], ['Scan bins N–Z', false]]),
            activity: activity([['Levent Demir', 'event', 'AuditSubmitted', 2], ['System', 'event', 'AuditBlocked', 1]])
        },
        {
            id: 'WC-1017', sourceModule: 'Finance', sourceType: 'CloseTask', sourceId: 'FIN-7781',
            itemType: 'task', assignmentMode: 'direct', claimed: true, accepted: true, startedOnce: true,
            lifecycle: 'InProgress', nativeStatus: 'In Progress', reviewRequired: true,
            capabilities: TASK_FULL.concat(['reviewFlow']), loggedMinutes: 45, plannedDate: addDays(3),
            dependencies: [{ id: 'FIN-7770', title: 'Sub-ledger close', type: 'FS', direction: 'pred', state: 'done' }, { id: 'FIN-7800', title: 'Consolidation run', type: 'FS', direction: 'succ', state: 'not-started' }],
            title: 'Prepare intercompany elimination schedule',
            summary: 'Build the elimination worksheet for the month-end close; partly done, timer paused.',
            priority: 'high', requester: 'Emre Güneş', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(1), scope: 'mine', isUnread: true, pinned: false,
            stages: [{ key: 'Open', label: 'Açık' }, { key: 'InProgress', label: 'Devam' }, { key: 'Review', label: 'İnceleme' }, { key: 'Closed', label: 'Kapandı' }],
            checklist: checklist([['Gather IC balances', true], ['Match pairs', false], ['Post eliminations', false]]),
            subtasks: subtasks('full', [['Collect IC confirmations', 'done'], ['Reconcile mismatches', 'in-progress']]),
            activity: activity([['Emre Güneş', 'event', 'AuditSubmitted', 2], ['Selin Aras', 'comment', 'IC confirmations in; two mismatches to chase.', 0]])
        },
        {
            id: 'WC-1018', sourceModule: 'Procurement', sourceType: 'SourcingTask', sourceId: 'SRC-2290',
            itemType: 'task', assignmentMode: 'direct', claimed: true, accepted: true, startedOnce: true,
            lifecycle: 'PendingReview', nativeStatus: 'Pending Review', reviewRequired: true,
            capabilities: TASK_FULL.concat(['reviewFlow']), loggedMinutes: 120,
            title: 'Finalize supplier scorecard refresh',
            summary: 'Work is complete and submitted; awaiting review sign-off before it closes.',
            priority: 'medium', requester: 'Burak Şahin', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(2), scope: 'mine', isUnread: false, pinned: false,
            stages: [{ key: 'Open', label: 'Açık' }, { key: 'InProgress', label: 'Devam' }, { key: 'Review', label: 'İnceleme' }, { key: 'Closed', label: 'Kapandı' }],
            checklist: checklist([['Collect KPIs', true], ['Score suppliers', true], ['Submit for review', true]]),
            activity: activity([['Burak Şahin', 'event', 'AuditSubmitted', 4], ['Selin Aras', 'comment', 'Submitted for sign-off.', 1]])
        },
        {
            id: 'WC-1013', sourceModule: 'Legal', sourceType: 'ContractReview', sourceId: 'CTR-1180',
            itemType: 'review', assignmentMode: 'direct', claimed: true, accepted: true,
            lifecycle: 'PendingReview', nativeStatus: 'Pending Review',
            capabilities: ['reviewFlow', 'activity', 'attachments', 'informationRequest'],
            title: 'Review NDA redlines — strategic partner',
            summary: 'Counterparty returned redlines on liability clauses; review before counsel finalizes.',
            priority: 'low', requester: 'Selin Aras', assignee: 'Deniz Koç', viewerRole: 'Reviewer',
            dueAt: null, scope: 'onBehalf', delegator: 'Aylin Ersoy', isUnread: false, pinned: false,
            attachments: [{ name: 'nda-redline.docx', size: '96 KB' }],
            activity: activity([['Selin Aras', 'event', 'AuditReviewRequested', 4]])
        },

        // ── GEÇMİŞ (Done / Cancelled) ─────────────────────────────────────────
        {
            id: 'WC-1014', sourceModule: 'Warehouse IT', sourceType: 'DecommTask', sourceId: 'WMS-0099',
            itemType: 'task', assignmentMode: 'direct', claimed: true, accepted: true,
            lifecycle: 'Cancelled', nativeStatus: 'Cancelled', capabilities: TASK_FULL.slice(),
            title: 'Legacy WMS decommission checklist',
            summary: 'Superseded by the WMS-Next migration workstream; retained for audit continuity.',
            priority: 'low', requester: 'Aylin Ersoy', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: null, scope: 'mine', isUnread: false, pinned: false,
            activity: activity([['Aylin Ersoy', 'event', 'AuditSubmitted', 9], ['System', 'event', 'AuditCommentAdded', 5]])
        },
        {
            id: 'WC-1015', sourceModule: 'Tax Master Data', sourceType: 'NormalizationTask', sourceId: 'TAX-5540',
            itemType: 'task', assignmentMode: 'direct', claimed: true, accepted: true,
            lifecycle: 'Done', nativeStatus: 'Closed', capabilities: TASK_FULL.slice(),
            title: 'Supplier tax-code normalization follow-up',
            summary: 'Completed after the tax-engine patch; kept visible for the closing evidence trail.',
            priority: 'low', requester: 'Levent Demir', assignee: 'Selin Aras', viewerRole: 'Owner',
            dueAt: addDays(-1), scope: 'mine', isUnread: false, pinned: false,
            activity: activity([['Levent Demir', 'event', 'AuditSubmitted', 6], ['Selin Aras', 'event', 'AuditCommentAdded', 1]])
        }
    ];

    const buildItems = () => rawItems.map((item) => {
        const sla = computeSla(item.dueAt);
        const depBlocked = computeBlocked(item.dependencies);
        const provider = SOURCE_PROVIDERS[item.sourceType] || null;
        const built = clone({
            ...item,
            sourceModuleId: provider ? provider.moduleId : null,
            sourceModuleName: provider ? provider.moduleName : item.sourceModule,
            sourceObjectType: item.sourceType,
            actionDepth: item.actionDepth || (provider && provider.actionDepth) || 'inline',
            sourceVersion: item.sourceVersion || 'v1',
            etag: item.etag || `mock-${item.id}`,
            claimed: item.claimed !== false,
            accepted: item.accepted !== undefined ? item.accepted
                : (item.assignmentMode === 'approval' ? false
                    : item.assignmentMode === 'direct' ? (item.lifecycle !== 'Open') : true),
            startedOnce: item.startedOnce || false,
            delegator: item.delegator || null,       // N-way scope: whose work (null = own)
            group: item.group || null,               // Havuz group queue
            systemState: item.systemState || null,   // stale/system signal (record-changed…)
            plannedDate: item.plannedDate || null,
            snoozedUntil: item.snoozedUntil || null,
            waitingOn: item.waitingOn || null,
            note: item.note || null,
            status: LIFECYCLE_STATUS[item.lifecycle] || STATUS.IN_PROGRESS,
            slaState: sla.state, slaDiffDays: sla.diffDays,
            typeIcon: TYPE_ICON[item.itemType] || 'bx-circle',
            deepLink: `/WorkCenterNext#source=${encodeURIComponent(item.sourceModule)}&id=${encodeURIComponent(item.sourceId)}`,
            dismissed: false,
            escalated: item.escalated || false,
            reviewRequired: item.reviewRequired || false,
            blockedState: depBlocked || item.blockedState || null,
            dependencies: item.dependencies || null,
            checklist: item.checklist || null,
            subtasks: item.subtasks || null,
            stages: item.stages || null,
            attachments: item.attachments || null,
            activity: item.activity || [],
            timesheet: item.itemType === 'task'
                ? { running: false, startedAt: null, loggedMinutes: item.loggedMinutes || 0 } : null
        });
        built.tab = tabFor(built);
        return built;
    });

    global.WorkCenterNextData = {
        todayIso: TODAY_ISO, currentUser: CURRENT_USER, onBehalfOf: ON_BEHALF_OF, delegators: DELEGATORS,
        status: STATUS, statusFor, tabFor, segmentFor, computeSla, computeBlocked, getActions, buildItems,
        buildMeetings: () => clone(MEETINGS), buildNotes: () => clone(NOTES), sourceProviders: SOURCE_PROVIDERS
    };
})(window);
