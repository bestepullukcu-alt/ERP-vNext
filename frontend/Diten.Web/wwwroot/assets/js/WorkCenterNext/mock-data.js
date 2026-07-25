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
        // MOD-0024 — matches the provider code, the manifest ModuleCode and platform.tasks.*
        tasks: 'Görevler',
        'enterprise-strategy': 'Kurumsal Strateji', documentation: 'Doküman Yönetimi',
        procurement: 'Satınalma', sales: 'Satış', treasury: 'Hazine', hr: 'İnsan Kaynakları', legal: 'Hukuk'
    };
    const moduleLabel = (code) => MODULE_LABELS[code] || code || '';
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
        'WC-TASK-DONE'   // Geçmiş placeholder (Geçmiş showcase = ayrı faz)
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
        // A person is { id, displayName } — fixtures carry the name, the real projection cannot yet resolve it
        // (no user-directory seam in Platform), so fall back to "Me" for the caller and to a plain
        // name-unavailable label for anyone else. Never render a raw user GUID.
        item.requester = personName(item.requester);
        item.assignee = personName(item.assignee);
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
    /*
     * WC-1b DEC-1 — FIXTURE SOURCE vs PRESENTATION MAPPER.
     * The mapper (toPresentation + tabFor/segmentFor/computeSla/computeBlocked/getActions/resolveLabel) is NOT
     * mock-specific: the real API path maps canonical work items through exactly the same code. Only the fixture
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
    const getActions = (item) => clone(item?.actions || []);
    const buildTriggers = () => (showcaseFixturesEnabled()
        ? clone(global.WorkCenterNextFixtures?.triggerOnly || [])
        : []);

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
        showcaseFixturesEnabled,
        buildMeetings: () => (showcaseFixturesEnabled() ? clone(MEETINGS) : []),
        buildNotes: () => (showcaseFixturesEnabled() ? clone(NOTES) : []),
        resolveLabel
    };
})(typeof window !== 'undefined' ? window : globalThis);
