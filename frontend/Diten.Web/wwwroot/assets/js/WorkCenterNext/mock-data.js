'use strict';

/*
 * WorkCenterNext mock catalog facade.
 * Canonical truth lives in fixtures/*. This file only supplies collection/list
 * presentation fields required by the existing WorkCenterNext shell.
 */
(function (global) {
    const TODAY_ISO = '2026-07-24';
    const TODAY = new Date(TODAY_ISO + 'T09:00:00+03:00');
    const CURRENT_USER = { id: 'USR-OWN', name: 'Selin Aras', title: 'Operasyon PMO Lideri' };
    const ON_BEHALF_OF = { id: 'USR-103', name: 'Deniz Koç', title: 'Finans Kontrolörü' };
    const DELEGATORS = [
        { id: 'USR-103', name: 'Deniz Koç', title: 'Finans Kontrolörü' },
        { id: 'USR-104', name: 'Aylin Ersoy', title: 'Satınalma Lideri' }
    ];
    const MEETINGS = [
        { id: 'MTG-1001', title: 'Haftalık Operasyon Toplantısı', start: '14:00', end: '15:00', location: 'Teams', owner: 'Selin Aras' }
    ];
    const NOTES = [
        { id: 'NOTE-1001', text: 'Q3 bütçe revizyonları için departman onaylarını topla.', ageKey: 'TimeToday', converted: false }
    ];
    const TYPE_ICON = { approval: 'bx-check-shield', task: 'bx-task', review: 'bx-search-alt', issue: 'bx-error-circle', exception: 'bx-error-alt' };
    // Friendly module names — the raw provider code (finance, master-data…) must never
    // surface in the UI. The real backend supplies localized module names; the mock
    // maps here. Unknown codes fall back to the code (visible = "fix the map").
    const MODULE_LABELS = {
        finance: 'Finans', tax: 'Vergi', quality: 'Kalite', 'master-data': 'Ana Veri',
        'integration-monitoring': 'Entegrasyon İzleme', 'project-governance': 'Proje Yönetişimi',
        workflow: 'İş Akışı', incident: 'Olay Yönetimi', workcenter: 'Görev Merkezi',
        'enterprise-strategy': 'Kurumsal Strateji', documentation: 'Doküman Yönetimi',
        procurement: 'Satınalma', sales: 'Satış', treasury: 'Hazine', hr: 'İnsan Kaynakları', legal: 'Hukuk'
    };
    const moduleLabel = (code) => MODULE_LABELS[code] || code || '';
    const VISIBLE_CATALOG_IDS = new Set([
        'INBOX-TASK-01', 'INBOX-APPROVAL-01', 'INBOX-REVIEW-OPTIONAL-MEETING',
        'INBOX-REVIEW-REQUIRED-MEETING', 'INBOX-ISSUE-01', 'INBOX-EXCEPTION-01',
        // İşlerim showcase (real, varied) replaces the placeholder WC-TASK-* variants.
        'ISLERIM-WORK-ACTIVE', 'ISLERIM-WORK-ISSUE', 'ISLERIM-WORK-BLOCKED',
        'ISLERIM-WORK-DELEGATED', 'ISLERIM-WORK-SNOOZED', 'ISLERIM-WORK-WAITING',
        'ISLERIM-WORK-REVIEW-MEETING', 'ISLERIM-WORK-PLANNED',
        'WC-TASK-DONE'   // Geçmiş placeholder (Geçmiş showcase = ayrı faz)
    ]);
    const clone = (value) => (typeof global.structuredClone === 'function')
        ? global.structuredClone(value)
        : JSON.parse(JSON.stringify(value));
    const resolveLabel = (label) => {
        if (!label) { return ''; }
        if (label.kind === 'resource') { return global.WCN?.t?.(label.key) || label.key; }
        return label.text || '';
    };
    const computeSla = (dueAt) => {
        if (!dueAt) { return { state: 'no-sla', diffDays: null }; }
        const due = new Date(`${dueAt}T00:00:00`);
        const base = new Date(TODAY.getFullYear(), TODAY.getMonth(), TODAY.getDate());
        const diffDays = Math.round((due - base) / 86400000);
        return { state: diffDays < 0 ? 'overdue' : diffDays <= 2 ? 'due-soon' : 'on-track', diffDays };
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
        kind: action.riskLevel === 'danger' ? 'danger'
            : ['approve', 'complete', 'resolve', 'signoff'].includes(action.code) ? 'success'
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
        input: action.input || null,
        role: ['reject', 'return', 'declineMeeting'].includes(action.code) ? 'reject'
            : ['approve', 'accept', 'claim', 'complete', 'resolve', 'signoff', 'start', 'resume', 'acceptMeeting'].includes(action.code) ? 'accept'
                : null
    });
    const allFixtureGroups = () => {
        const fixtures = global.WorkCenterNextFixtures || {};
        const migrationAdapter = global.WorkCenterNextMigrationAdapter?.adaptLegacyFixture;
        const adaptedMigration = (fixtures.migration || []).map((fixture) => migrationAdapter?.(fixture)).filter(Boolean);
        return [
            ...(fixtures.inboxShowcase || []),
            ...(fixtures.islerimShowcase || []),
            ...(fixtures.canonical || []),
            ...(fixtures.edgeCases || []),
            ...(fixtures.enterpriseStrategy || []),
            ...(fixtures.documentation || []),
            ...adaptedMigration
        ];
    };
    const toPresentation = (fixture) => {
        const item = clone(fixture);
        const sla = computeSla(item.dueAt);
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
        item.requester = item.requester?.displayName || '';
        item.assignee = item.assignee?.displayName || '';
        item.scope = item.delegationContext ? 'onBehalf' : 'mine';
        item.delegator = item.delegationContext?.displayName || null;
        item.group = item.assignmentMode === 'groupQueue' ? 'Operasyon Kuyruğu' : null;
        item.isUnread = item.personal?.seen === false;
        item.pinned = !!item.personal?.pinned;
        item.snoozedUntil = item.personal?.snoozedUntil || null;
        item.plannedDate = item.personal?.plannedDate || null;
        item.waitingOn = item.waitingContext?.waitingOn?.displayName || null;
        item.note = item.personal?.note || null;
        item.slaState = item.slaState || sla.state;
        item.slaDiffDays = item.slaDiffDays ?? sla.diffDays;
        item.actions = item.actions.map((candidate) => {
            const mapped = actionForPresentation(candidate);
            mapped.primary = candidate.code === item.primaryActionCode;
            return mapped;
        });
        item.tab = tabFor(item);
        item.dismissed = false;
        item.catalogVisible = VISIBLE_CATALOG_IDS.has(item.id);
        item.escalated = !!item.escalated;
        item.reviewRequired = item.taskLifecycle === 'PendingReview';
        item.checklist = item.checklist ? {
            ...item.checklist,
            items: (item.checklist.items || []).map((entry) => ({
                ...entry,
                text: resolveLabel(entry.label) || entry.text || '',
                done: entry.completed === true || entry.done === true
            }))
        } : null;
        item.subtasks = item.subtasks || null;
        item.dependencies = item.dependencies ? item.dependencies.map((entry) => ({
            ...entry,
            title: resolveLabel(entry.title) || entry.title || ''
        })) : null;
        item.attachments = item.attachments ? item.attachments.map((entry) => ({
            ...entry,
            name: resolveLabel(entry.label) || entry.name || entry.id,
            size: entry.version ? `v${entry.version}` : ''
        })) : null;
        // Canonical activity carries `at` (ISO-ish timestamp); the shell renders a
        // relative "N days ago" from `ago`. Derive it once here so entries without an
        // explicit `ago` don't surface as "undefined days ago".
        item.activity = (item.activity || []).map((entry) => {
            if (entry.ago != null) { return entry; }
            const at = entry.at ? new Date(String(entry.at).replace(' ', 'T')) : null;
            const ago = (at && !isNaN(at)) ? Math.max(0, Math.round((TODAY - at) / 86400000)) : 0;
            return { ...entry, ago };
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
    const buildItems = () => allFixtureGroups().map(toPresentation);
    const getActions = (item) => clone(item?.actions || []);
    const buildTriggers = () => clone(global.WorkCenterNextFixtures?.triggerOnly || []);

    global.WorkCenterNextData = {
        todayIso: TODAY_ISO,
        currentUser: CURRENT_USER,
        onBehalfOf: ON_BEHALF_OF,
        delegators: DELEGATORS,
        status: { PENDING: 'Pending', IN_PROGRESS: 'In Progress', WAITING: 'Waiting', DONE: 'Done', CANCELLED: 'Cancelled' },
        tabFor,
        segmentFor,
        computeSla,
        computeBlocked: (dependencies) => dependencies?.some((dependency) => dependency.blocking)
            ? { blocked: true, blockedBy: dependencies.filter((dependency) => dependency.blocking), reasonKey: 'ActionDisabledDependencyBlocked' }
            : null,
        getActions,
        toPresentation,
        buildItems,
        buildTriggers,
        buildMeetings: () => clone(MEETINGS),
        buildNotes: () => clone(NOTES),
        resolveLabel
    };
})(typeof window !== 'undefined' ? window : globalThis);
