'use strict';

(function () {
    var page = document.getElementById('taskDetailPage');
    if (!page) { return; }

    var TASK_ID = page.dataset.taskId || '';
    var RETURN_URL = page.dataset.returnUrl || '/WorkCenter';

    // ── L10n ──────────────────────────────────────────────────────────────────
    var l10nEl = document.getElementById('task-detail-l10n');
    var l10n = {};
    if (l10nEl) {
        try { l10n = JSON.parse(l10nEl.textContent); } catch (e) { /* ignore */ }
    }

    // ── MOCK DATA (shared with index.js) ───────────────────────────────────────
    var currentUserName = (window.WorkCenterMockData && window.WorkCenterMockData.currentUser && window.WorkCenterMockData.currentUser.name) || '';
    var baseItems = (window.WorkCenterMockData && typeof window.WorkCenterMockData.buildMockItems === 'function')
        ? window.WorkCenterMockData.buildMockItems()
        : [];

    var enrichItem = function (item) {
        var plannedDate = item.createdDate;
        if (plannedDate) {
            var p = new Date(plannedDate);
            p.setDate(p.getDate() + 1);
            plannedDate = p.toISOString().slice(0, 10);
        }

        var tagPool = ['Compliance', 'Urgent', 'Customer', 'Release', 'Audit'];
        var firstTag = tagPool[item.id.length % tagPool.length];
        var secondTag = tagPool[(item.id.length + 2) % tagPool.length];

        var blockedBy = item.dependencySummary
            ? [{ id: 'DEP-' + item.id.slice(-3), title: item.dependencySummary }]
            : [];
        var blocks = item.hasSubtasks
            ? [{ id: 'SUB-' + item.id.slice(-3), title: 'Follow-up subtasks depend on this item.' }]
            : [];
        var taskTypeByStatus = {
            'Pending Approval': 'approval',
            'Pending Acceptance': 'execution',
            'Open': 'execution',
            'Planned': 'execution',
            'In Progress': 'execution',
            'Waiting for Information': 'info',
            'Pending Review': 'review',
            'Closed': 'info',
            'Cancelled': 'info'
        };
        var sourceTypePool = ['Issue', 'SalesOrder', 'Contract', 'Shipment', 'Workflow'];
        var sourceSeed = parseInt((item.id || '').replace(/\D/g, ''), 10) || 0;
        var sourceType = sourceTypePool[sourceSeed % sourceTypePool.length];
        var sourcePrefix = sourceType === 'Issue' ? 'ISSUE-' :
            (sourceType === 'SalesOrder' ? 'SO-' :
            (sourceType === 'Contract' ? 'CTR-' :
            (sourceType === 'Shipment' ? 'SHP-' : 'WF-')));
        var estimatedMinutes = item.type === 'meeting' ? 60 : 390;
        var reassignedFrom = item.status === 'Pending Review' || item.status === 'In Progress' ? 'Mert Aksoy' : '';
        var relationPool = ['Created from', 'Triggered by', 'Child of', 'Related to'];
        var sourceRelationType = relationPool[sourceSeed % relationPool.length];
        var checklistParts = String(item.checklistProgress || '0/0').split('/');
        var checklistDone = parseInt(checklistParts[0], 10) || 0;
        var checklistTotal = parseInt(checklistParts[1], 10) || 0;
        var subtasks = [];

        if (item.hasChecklist && checklistTotal > 0) {
            for (var i = 0; i < checklistTotal; i++) {
                subtasks.push({
                    id: item.id + '-c' + (i + 1),
                    title: 'Checklist item ' + (i + 1),
                    done: i < checklistDone
                });
            }
        } else if (item.hasSubtasks) {
            subtasks = [
                { id: item.id + '-s1', title: 'Review requirements', done: true },
                { id: item.id + '-s2', title: 'Prepare working draft', done: false },
                { id: item.id + '-s3', title: 'Share final package', done: false }
            ];
        }

        var descriptionParts = [
            'This work item belongs to ' + item.context + ' and is owned by ' + (item.assignee || '-') + '.',
            item.requiredAction || '',
            item.blockedReason ? 'Blocked reason: ' + item.blockedReason : '',
            item.waitingInfo ? 'Waiting on: ' + item.waitingInfo : '',
            item.dependencySummary ? 'Dependency: ' + item.dependencySummary : ''
        ].filter(Boolean);

        return {
            id: item.id,
            type: item.type,
            taskType: taskTypeByStatus[item.status] || (item.type === 'issue' ? 'approval' : 'execution'),
            status: item.status,
            priority: item.priority,
            title: item.title,
            source: item.source,
            sourceType: sourceType,
            sourceId: sourcePrefix + item.id.replace('TASK-', ''),
            sourceTitle: item.title,
            sourceRelationType: sourceRelationType,
            context: item.context,
            assignee: item.assignee,
            createdBy: item.creator,
            createdDate: item.createdDate,
            dueDate:     item.dueDate,
            plannedAt:   plannedDate,
            meta:        item.meta,
            project:     item.context,
            reviewer:    item.reviewer || '-',
            approver:    item.approver || '-',
            blocked:     !!item.blocked,
            blockedReason: item.blockedReason || '',
            waitingInfo: item.waitingInfo || '',
            reviewRequired: !!item.reviewRequired,
            approvalRequired: !!item.approvalRequired,
            flowAnchorStatus: item.status === 'Waiting for Information' ? (item.blocked ? 'Planned' : 'Open') : '',
            tags:        [firstTag, secondTag],
            watchers:    ['PMO Bot', 'Nadia P.'],
            attachments: [
                { name: 'brief-' + item.id + '.pdf', size: '420 KB' },
                { name: 'checklist-' + item.id + '.xlsx', size: '96 KB' }
            ],
            market:      'TR',
            domain:      item.source,
            externalParty: item.type === 'issue' ? 'Vendor Team' : '-',
            estimation:  item.type === 'meeting' ? '1h 0m' : '6h 30m',
            estimatedMinutes: estimatedMinutes,
            reassignedFrom: reassignedFrom,
            description: descriptionParts.join('\n\n'),
            subtasks: subtasks,
            activity: [
                { type: 'status_change', text: 'Task created and assigned.', author: item.creator, timestamp: item.createdDate },
                { type: 'status_change', text: 'Status set to ' + item.status + '.', author: 'System', timestamp: item.createdDate }
            ],
            dependencies: { blockedBy: blockedBy, blocks: blocks },
            hasReview: !!item.reviewRequired
        };
    };

    var allItems = baseItems;
    var baseItem = null;
    for (var i = 0; i < allItems.length; i++) {
        if (allItems[i].id === TASK_ID) { baseItem = allItems[i]; break; }
    }
    if (!baseItem) { baseItem = allItems[0]; }
    if (!baseItem) { return; }

    var state = {
        item:         enrichItem(baseItem),
        timeEntries:  [],
        timerActive:  false,
        timerStartMs: null,
        timerInterval: null
    };

    // ── CONSTANTS ─────────────────────────────────────────────────────────────
    var STATUS_LIFECYCLE = ['Pending Approval', 'Pending Acceptance', 'Open', 'Planned', 'In Progress', 'Pending Review', 'Closed'];
    var OFF_FLOW = { 'Waiting for Information': true, 'Cancelled': true };

    var STATUS_LABELS = {
        'Pending Approval': 'Pending Approval',
        'Pending Acceptance': 'Pending Acceptance',
        'Open': 'Open',
        'Planned': 'Planned',
        'In Progress': 'In Progress',
        'Waiting for Information': 'Waiting for Information',
        'Pending Review': 'Pending Review',
        'Closed': 'Closed',
        'Cancelled': 'Cancelled'
    };

    var STATUS_BADGE_CLASS = {
        'Pending Approval': 'bg-label-primary',
        'Pending Acceptance': 'bg-label-info',
        'Open': 'bg-label-secondary',
        'Planned': 'bg-label-warning',
        'In Progress': 'bg-label-primary',
        'Waiting for Information': 'bg-label-warning',
        'Pending Review': 'bg-label-info',
        'Closed': 'bg-label-success',
        'Cancelled': 'bg-label-secondary'
    };

    var SOURCE_ROUTE_BUILDERS = {
        SalesOrder: function (id) { return '/sales/orders/' + encodeURIComponent(id); },
        Issue: function (id) { return '/ppm/issues/' + encodeURIComponent(id); },
        Contract: function (id) { return '/commercial/contracts/' + encodeURIComponent(id); },
        Shipment: function (id) { return '/logistics/shipments/' + encodeURIComponent(id); }
    };

    var getStatusActions = function (item) {
        if (!item) { return []; }

        switch (item.status) {
            case 'Pending Approval':
                return item.approver === currentUserName ? ['approve', 'reject'] : [];
            case 'Pending Acceptance':
                return item.assignee === currentUserName ? ['accept', 'reassign'] : [];
            case 'Open':
                if (item.blocked) { return ['inspectBlocker', 'reassign']; }
                return item.type === 'issue' ? ['investigate', 'requestInfo', 'reassign'] : ['plan', 'reassign'];
            case 'Planned':
                return item.blocked ? ['inspectBlocker', 'reassign'] : ['startWork', 'reassign'];
            case 'In Progress':
                return item.blocked ? ['inspectBlocker', 'reassign'] : ['continueWork', 'requestInfo', 'reassign'];
            case 'Waiting for Information':
                return ['followUp', 'reassign'];
            case 'Pending Review':
                return item.reviewer === currentUserName ? ['review', 'rejectReview'] : [];
            case 'Closed':
                return ['viewSummary'];
            case 'Cancelled':
                return ['viewReason'];
            default:
                return [];
        }
    };

    var ACTION_DEFS = {
        accept:        { label: l10n.Accept || 'Accept', icon: 'bx-check', cls: 'btn-success' },
        reject:        { label: l10n.Reject || 'Reject', icon: 'bx-x', cls: 'btn-danger' },
        requestInfo:   { label: l10n.RequestInfo || 'Request Info', icon: 'bx-question-mark', cls: 'btn-label-warning' },
        reassign:      { label: l10n.Reassign || 'Reassign', icon: 'bx-user-pin', cls: 'btn-label-secondary' },
        plan:          { label: l10n.Plan || 'Plan', icon: 'bx-calendar-check', cls: 'btn-label-info' },
        startWork:     { label: l10n.StartWork || 'Start Work', icon: 'bx-play', cls: 'btn-primary' },
        approve:       { label: l10n.Approve || 'Approve', icon: 'bx-check-shield', cls: 'btn-success' },
        review:        { label: 'Review', icon: 'bx-check-circle', cls: 'btn-success' },
        rejectReview:  { label: l10n.RejectReview || 'Reject Review', icon: 'bx-x-circle', cls: 'btn-outline-danger' },
        followUp:      { label: 'Follow Up', icon: 'bx-refresh', cls: 'btn-label-warning' },
        continueWork:  { label: 'Continue', icon: 'bx-right-arrow-alt', cls: 'btn-primary' },
        investigate:   { label: 'Investigate', icon: 'bx-search', cls: 'btn-primary' },
        inspectBlocker:{ label: 'Inspect Blocker', icon: 'bx-block', cls: 'btn-label-warning' },
        viewSummary:   { label: 'View Summary', icon: 'bx-file', cls: 'btn-label-secondary' },
        viewReason:    { label: 'View Reason', icon: 'bx-info-circle', cls: 'btn-label-secondary' }
    };

    // ── DOM REFS ──────────────────────────────────────────────────────────────
    var el = {
        taskIdLabel:          document.getElementById('taskIdLabel'),
        taskBreadcrumbContext: document.getElementById('taskBreadcrumbContext'),
        taskBreadcrumbItem:   document.getElementById('taskBreadcrumbItem'),
        taskTitle:            document.getElementById('taskTitle'),
        taskAssignee:         document.getElementById('taskAssignee'),
        taskCreatedBy:        document.getElementById('taskCreatedBy'),
        taskDueDate:          document.getElementById('taskDueDate'),
        taskOverdueBadge:     document.getElementById('taskOverdueBadge'),
        taskStatusBadge:      document.getElementById('taskStatusBadge'),
        taskStepBar:          document.getElementById('taskStepBar'),
        taskDescription:      document.getElementById('taskDescription'),
        taskSubtasksList:     document.getElementById('taskSubtasksList'),
        taskSubtasksProgress: document.getElementById('taskSubtasksProgress'),
        taskActivityCount:    document.getElementById('taskActivityCount'),
        taskActivityFeed:     document.getElementById('taskActivityFeed'),
        taskCommentInput:     document.getElementById('taskCommentInput'),
        taskCommentSubmit:    document.getElementById('taskCommentSubmit'),
        taskActionsPanel:     document.getElementById('taskActionsPanel'),
        taskPrimaryActions:   document.getElementById('taskPrimaryActions'),
        taskSecondaryActions: document.getElementById('taskSecondaryActions'),
        taskDestructiveDivider: document.getElementById('taskDestructiveDivider'),
        taskDestructiveActions: document.getElementById('taskDestructiveActions'),
        taskActionFeedback:   document.getElementById('taskActionFeedback'),
        taskRejectReviewForm: document.getElementById('taskRejectReviewForm'),
        taskRejectNote:       document.getElementById('taskRejectNote'),
        taskRejectReviewConfirm: document.getElementById('taskRejectReviewConfirm'),
        taskTimeHours:        document.getElementById('taskTimeHours'),
        taskTimeMinutes:      document.getElementById('taskTimeMinutes'),
        taskTimeNote:         document.getElementById('taskTimeNote'),
        taskTimeLogBtn:       document.getElementById('taskTimeLogBtn'),
        taskTimeEntryCard:    document.getElementById('taskTimeEntryCard'),
        taskTimeStateMessage: document.getElementById('taskTimeStateMessage'),
        taskTimerToggle:      document.getElementById('taskTimerToggle'),
        taskTimerIcon:        document.getElementById('taskTimerIcon'),
        taskTimerDisplay:     document.getElementById('taskTimerDisplay'),
        taskTotalTime:        document.getElementById('taskTotalTime'),
        taskLoggedSummary:    document.getElementById('taskLoggedSummary'),
        taskEstimatedSummary: document.getElementById('taskEstimatedSummary'),
        taskVarianceSummary:  document.getElementById('taskVarianceSummary'),
        taskRemainingSummary: document.getElementById('taskRemainingSummary'),
        taskTimeLogList:      document.getElementById('taskTimeLogList'),
        taskBlockedByList:    document.getElementById('taskBlockedByList'),
        taskBlocksList:       document.getElementById('taskBlocksList'),
        taskSourceRefLink:    document.getElementById('taskSourceRefLink'),
        taskSourceRefValue:   document.getElementById('taskSourceRefValue'),
        taskSourceTitleValue: document.getElementById('taskSourceTitleValue'),
        taskSourceRelationValue: document.getElementById('taskSourceRelationValue'),
        taskReviewerValue:    document.getElementById('taskReviewerValue'),
        taskReassignmentValue: document.getElementById('taskReassignmentValue'),
        taskPlannedAtValue:   document.getElementById('taskPlannedAtValue'),
        taskDependenciesStateBadge: document.getElementById('taskDependenciesStateBadge'),
        taskTagsList:         document.getElementById('taskTagsList'),
        taskWatchersList:     document.getElementById('taskWatchersList'),
        taskAttachmentsList:  document.getElementById('taskAttachmentsList'),
        taskMarketValue:      document.getElementById('taskMarketValue'),
        taskDomainValue:      document.getElementById('taskDomainValue'),
        taskExternalPartyValue: document.getElementById('taskExternalPartyValue'),
        taskEstimationValue:  document.getElementById('taskEstimationValue'),
        taskBackBtn:          document.getElementById('taskBackBtn')
    };

    // ── HELPERS ───────────────────────────────────────────────────────────────
    var notify = function (msg, type) {
        if (typeof window.showToast === 'function') {
            window.showToast(msg, type || 'info');
            return;
        }
        console.log('[TaskDetail]', type || 'info', msg);
    };

    var formatDate = function (value) {
        if (!value) { return '-'; }
        var d = new Date(value);
        if (isNaN(d.getTime())) { return value; }
        return d.toLocaleDateString();
    };

    var resolvePriorityClass = function (priority) {
        var p = (priority || '').toLowerCase();
        if (p === 'yuksek' || p === 'high')   { return 'bg-label-danger'; }
        if (p === 'orta'   || p === 'medium') { return 'bg-label-warning'; }
        if (p === 'dusuk'  || p === 'low')    { return 'bg-label-success'; }
        return 'bg-label-secondary';
    };

    var formatDuration = function (totalMs) {
        var totalSec = Math.floor(totalMs / 1000);
        var h = Math.floor(totalSec / 3600);
        var m = Math.floor((totalSec % 3600) / 60);
        var s = totalSec % 60;
        return h + ':' + String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0');
    };

    var formatMinutes = function (totalMin) {
        if (typeof totalMin !== 'number' || isNaN(totalMin) || totalMin < 0) { return '-'; }
        var h = Math.floor(totalMin / 60);
        var m = totalMin % 60;
        return h + 'h ' + m + 'm';
    };

    var computeDueState = function (dueDate) {
        if (!dueDate) { return { kind: 'unknown', label: '-', cls: 'bg-label-secondary' }; }
        var due = new Date(dueDate);
        if (isNaN(due.getTime())) { return { kind: 'unknown', label: '-', cls: 'bg-label-secondary' }; }

        var now = window.WorkCenterMockData && window.WorkCenterMockData.todayIso
            ? new Date(window.WorkCenterMockData.todayIso)
            : new Date();
        var today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        var dueDay = new Date(due.getFullYear(), due.getMonth(), due.getDate());
        var diffDays = Math.floor((dueDay.getTime() - today.getTime()) / 86400000);

        if (diffDays < 0) {
            return { kind: 'overdue', label: (l10n.Overdue || 'Overdue') + ' (' + Math.abs(diffDays) + ' ' + (l10n.DaysAgo || 'days ago') + ')', cls: 'bg-label-danger' };
        }
        if (diffDays <= 2) {
            return { kind: 'due_soon', label: (l10n.DueSoon || 'Due Soon') + ' (' + (l10n.DueInDays || 'due in') + ' ' + diffDays + 'd)', cls: 'bg-label-warning' };
        }
        return { kind: 'on_track', label: l10n.OnTrack || 'On Track', cls: 'bg-label-success' };
    };

    var totalLoggedMinutes = function () {
        return state.timeEntries.reduce(function (sum, e) { return sum + (e.hours * 60) + e.minutes; }, 0);
    };

    var isTimeEntryEnabledStatus = function () {
        return state.item && state.item.status === 'In Progress';
    };

    var esc = function (str) {
        return String(str || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    };

    // ── RENDER ────────────────────────────────────────────────────────────────
    var renderHeader = function () {
        var item = state.item;
        var dueState = computeDueState(item.dueDate);
        if (el.taskIdLabel)       { el.taskIdLabel.textContent = '#' + item.id.toUpperCase(); }
        if (el.taskBreadcrumbContext) { el.taskBreadcrumbContext.textContent = item.project || item.source || 'Task'; }
        if (el.taskBreadcrumbItem) { el.taskBreadcrumbItem.textContent = item.id ? item.id.toUpperCase() : '-'; }
        if (el.taskTitle)         { el.taskTitle.textContent = item.title || '-'; }
        if (el.taskAssignee)      { el.taskAssignee.textContent = item.assignee || '-'; }
        if (el.taskCreatedBy)     { el.taskCreatedBy.textContent = (l10n.CreatedBy || 'Created By') + ': ' + (item.createdBy || '-'); }
        if (el.taskDueDate)       { el.taskDueDate.textContent = formatDate(item.dueDate); }
        if (el.taskOverdueBadge) {
            el.taskOverdueBadge.classList.toggle('d-none', dueState.kind !== 'overdue');
            if (dueState.kind === 'overdue') { el.taskOverdueBadge.textContent = dueState.label; }
        }
        if (el.taskStatusBadge) {
            el.taskStatusBadge.textContent = STATUS_LABELS[item.status] || item.status;
            el.taskStatusBadge.className = 'badge ms-auto ' + (STATUS_BADGE_CLASS[item.status] || 'bg-label-secondary');
        }
    };

    var renderStepBar = function () {
        if (!el.taskStepBar) { return; }
        var status = state.item.status;
        var isOffFlow = !!OFF_FLOW[status];

        var effectiveIndex;
        if (!isOffFlow) {
            effectiveIndex = STATUS_LIFECYCLE.indexOf(status);
        } else if (status === 'Waiting for Information') {
            effectiveIndex = STATUS_LIFECYCLE.indexOf(state.item.flowAnchorStatus || 'Open');
        } else {
            effectiveIndex = STATUS_LIFECYCLE.length - 1;
        }

        var getStepState = function (i) {
            return {
                done: i < effectiveIndex,
                current: i === effectiveIndex && !isOffFlow,
                paused: i === effectiveIndex && isOffFlow
            };
        };

        var parts = [];
        STATUS_LIFECYCLE.forEach(function (step, i) {
            var stateFlags = getStepState(i);
            var stepCls = 'task-step';
            var connectorCls = 'task-step-connector task-step-connector--future';
            if (stateFlags.done) {
                stepCls += ' task-step--done';
                connectorCls = 'task-step-connector task-step-connector--done';
            } else if (stateFlags.current) {
                stepCls += ' task-step--current';
                connectorCls = 'task-step-connector task-step-connector--current';
            } else if (stateFlags.paused) {
                stepCls += ' task-step--paused';
                connectorCls = 'task-step-connector task-step-connector--paused';
            } else {
                stepCls += ' task-step--future';
            }

            var connector = i > 0 ? '<div class="' + connectorCls + '"></div>' : '';
            var stepIndexContent = stateFlags.done
                ? '<i class="bx bx-check task-step-index-icon" aria-hidden="true"></i>'
                : String(i + 1);
            parts.push(connector + '<div class="' + stepCls + '" role="listitem" title="' + esc(STATUS_LABELS[step] || step) + '"><span class="task-step-index">' + stepIndexContent + '</span><span class="task-step-label">' + esc(STATUS_LABELS[step] || step) + '</span></div>');
        });
        if (isOffFlow && status !== 'Cancelled') {
            var badgeCls = 'bg-label-warning';
            parts.push('<div class="task-step-offflow ms-3 flex-shrink-0"><span class="badge ' + badgeCls + '">' + esc(STATUS_LABELS[status] || status) + '</span></div>');
        }
        el.taskStepBar.innerHTML = parts.join('');
    };

    var renderDescription = function () {
        if (el.taskDescription) { el.taskDescription.textContent = state.item.description || '-'; }
    };

    var renderSubtasks = function () {
        if (!el.taskSubtasksList) { return; }
        var subtasks = state.item.subtasks || [];
        if (!subtasks.length) {
            el.taskSubtasksList.innerHTML = '<li class="text-muted small">-</li>';
            if (el.taskSubtasksProgress) { el.taskSubtasksProgress.textContent = '0/0 ' + (l10n.ChecklistCompleted || 'completed') + ' (0%)'; }
            return;
        }
        var doneCount = subtasks.filter(function (s) { return s.done; }).length;
        var percent = Math.round((doneCount / subtasks.length) * 100);
        if (el.taskSubtasksProgress) { el.taskSubtasksProgress.textContent = doneCount + '/' + subtasks.length + ' ' + (l10n.ChecklistCompleted || 'completed') + ' (' + percent + '%)'; }
        el.taskSubtasksList.innerHTML = subtasks.map(function (s) {
            var iconCls = s.done ? 'bx-check-circle text-success' : 'bx-circle text-muted';
            var textCls = s.done ? 'text-muted text-decoration-line-through' : '';
            return '<li class="d-flex align-items-center gap-2 py-1"><i class="bx ' + iconCls + ' icon-base flex-shrink-0"></i><span class="small ' + textCls + '">' + esc(s.title) + '</span></li>';
        }).join('');
    };

    var renderActivity = function () {
        if (!el.taskActivityFeed) { return; }
        var activity = state.item.activity || [];
        if (el.taskActivityCount) { el.taskActivityCount.textContent = String(activity.length); }
        if (!activity.length) {
            el.taskActivityFeed.innerHTML = '<p class="text-muted small mb-0">-</p>';
            return;
        }
        el.taskActivityFeed.innerHTML = activity.map(function (a) {
            var isSystem = a.type === 'status_change';
            var iconCls  = isSystem ? 'bx-info-circle text-info' : 'bx-message-rounded text-primary';
            return '<div class="task-activity-item d-flex gap-2 mb-2"><div class="flex-shrink-0 mt-1"><i class="bx ' + iconCls + ' icon-base"></i></div><div class="flex-grow-1"><p class="mb-0 small">' + esc(a.text) + '</p><span class="text-muted" style="font-size:0.7rem;">' + esc(a.author) + ' · ' + formatDate(a.timestamp) + '</span></div></div>';
        }).join('');
        el.taskActivityFeed.scrollTop = el.taskActivityFeed.scrollHeight;
    };

    var renderActionsPanel = function () {
        if (!el.taskActionsPanel) { return; }
        var actions = getStatusActions(state.item);
        if (el.taskRejectReviewForm) { el.taskRejectReviewForm.classList.add('d-none'); }

        if (!actions.length) {
            if (el.taskPrimaryActions) { el.taskPrimaryActions.innerHTML = ''; }
            if (el.taskSecondaryActions) { el.taskSecondaryActions.innerHTML = '<p class="text-muted small mb-0">-</p>'; }
            if (el.taskDestructiveActions) { el.taskDestructiveActions.innerHTML = ''; }
            if (el.taskDestructiveDivider) { el.taskDestructiveDivider.classList.add('d-none'); }
            return;
        }

        var primarySet = {
            accept: true,
            plan: true,
            startWork: true,
            approve: true,
            review: true,
            followUp: true,
            continueWork: true,
            investigate: true,
            inspectBlocker: true,
            viewSummary: true,
            viewReason: true
        };
        var destructiveSet = { reject: true, rejectReview: true };

        var toButton = function (action) {
            var def = ACTION_DEFS[action];
            if (!def) { return null; }
            return '<button type="button" class="btn btn-sm ' + def.cls + ' d-flex align-items-center justify-content-center gap-1 w-100" data-task-action="' + action + '"><i class="bx ' + def.icon + ' icon-base"></i>' + esc(def.label) + '</button>';
        };

        var primaryButtons = [];
        var secondaryButtons = [];
        var destructiveButtons = [];

        actions.forEach(function (action) {
            var button = toButton(action);
            if (!button) { return; }
            if (primarySet[action]) {
                primaryButtons.push(button);
                return;
            }
            if (destructiveSet[action]) {
                destructiveButtons.push(button);
                return;
            }
            secondaryButtons.push(button);
        });

        if (el.taskPrimaryActions) { el.taskPrimaryActions.innerHTML = primaryButtons.join(''); }
        if (el.taskSecondaryActions) {
            var hasRequestInfo = actions.indexOf('requestInfo') >= 0;
            var hasReassign = actions.indexOf('reassign') >= 0;
            if (hasRequestInfo && hasReassign) {
                el.taskSecondaryActions.className = 'row g-2';
                el.taskSecondaryActions.innerHTML = secondaryButtons.map(function (btn, i) {
                    var actionName = btn.indexOf('data-task-action="requestInfo"') >= 0 ? 'requestInfo'
                        : (btn.indexOf('data-task-action="reassign"') >= 0 ? 'reassign' : '');
                    if (actionName === 'requestInfo' || actionName === 'reassign') {
                        return '<div class="col-6">' + btn + '</div>';
                    }
                    return '<div class="col-12">' + btn + '</div>';
                }).join('');
            } else {
                el.taskSecondaryActions.className = 'd-grid gap-2';
                el.taskSecondaryActions.innerHTML = secondaryButtons.join('');
            }
        }
        if (el.taskDestructiveActions) { el.taskDestructiveActions.innerHTML = destructiveButtons.join(''); }
        if (el.taskDestructiveDivider) { el.taskDestructiveDivider.classList.toggle('d-none', destructiveButtons.length === 0); }
    };

    var renderTimeLog = function () {
        if (el.taskTimeEntryCard && el.taskTimeEntryCard.classList.contains('d-none')) { return; }
        var estimatedMinutes = typeof state.item.estimatedMinutes === 'number' ? state.item.estimatedMinutes : null;
        var setTimeSummary = function (totalMinutes) {
            if (el.taskLoggedSummary) { el.taskLoggedSummary.textContent = formatMinutes(totalMinutes); }
            if (el.taskEstimatedSummary) { el.taskEstimatedSummary.textContent = estimatedMinutes === null ? (l10n.NoEstimate || 'No estimate') : formatMinutes(estimatedMinutes); }
            if (el.taskVarianceSummary) {
                if (estimatedMinutes === null) {
                    el.taskVarianceSummary.textContent = '-';
                } else {
                    var diff = totalMinutes - estimatedMinutes;
                    if (diff > 0) {
                        el.taskVarianceSummary.textContent = (l10n.Overrun || 'Overrun') + ': ' + formatMinutes(diff);
                    } else if (diff < 0) {
                        el.taskVarianceSummary.textContent = (l10n.UnderEstimateBy || 'Under estimate by') + ' ' + formatMinutes(Math.abs(diff));
                    } else {
                        el.taskVarianceSummary.textContent = l10n.OnEstimate || 'On estimate';
                    }
                }
            }
            if (el.taskRemainingSummary) {
                if (estimatedMinutes === null) {
                    el.taskRemainingSummary.textContent = '-';
                } else {
                    var remaining = estimatedMinutes - totalMinutes;
                    if (remaining < 0) {
                        el.taskRemainingSummary.textContent = (l10n.Overrun || 'Overrun') + ' ' + formatMinutes(Math.abs(remaining));
                    } else {
                        el.taskRemainingSummary.textContent = formatMinutes(remaining);
                    }
                }
            }
        };

        if (!el.taskTimeLogList) { return; }
        if (!state.timeEntries.length) {
            el.taskTimeLogList.innerHTML = '<li class="text-muted small">' + esc(l10n.NoTimeLogged || 'No time logged yet.') + '</li>';
            if (el.taskTotalTime) { el.taskTotalTime.textContent = '0h 0m'; }
            setTimeSummary(0);
            return;
        }
        el.taskTimeLogList.innerHTML = state.timeEntries.map(function (e) {
            return '<li class="d-flex gap-2 align-items-start mb-1 small"><i class="bx bx-time text-muted icon-base flex-shrink-0 mt-1"></i><div><span class="fw-semibold">' + e.hours + 'h ' + e.minutes + 'm</span>' + (e.note ? '<span class="text-muted ms-1">· ' + esc(e.note) + '</span>' : '') + '<div class="text-muted" style="font-size:0.7rem;">' + esc(e.loggedAt) + '</div></div></li>';
        }).join('');
        var total = totalLoggedMinutes();
        var th = Math.floor(total / 60);
        var tm = total % 60;
        if (el.taskTotalTime) { el.taskTotalTime.textContent = th + 'h ' + tm + 'm'; }
        setTimeSummary(total);
    };

    var renderTimeEntryState = function () {
        var status = state.item ? state.item.status : '';
        var isInProgress = status === 'In Progress';
        var isInReview = status === 'Pending Review';
        var isVisible = isInProgress || isInReview;
        var isReadOnly = isInReview;

        if (el.taskTimeEntryCard) {
            el.taskTimeEntryCard.classList.toggle('d-none', !isVisible);
        }

        if (!isVisible) {
            if (state.timerActive) { stopTimer(); }
            return;
        }

        var controls = [
            el.taskTimeHours,
            el.taskTimeMinutes,
            el.taskTimeNote,
            el.taskTimeLogBtn,
            el.taskTimerToggle
        ];
        controls.forEach(function (control) {
            if (control) { control.disabled = isReadOnly; }
        });

        if (isReadOnly && state.timerActive) { stopTimer(); }

        if (el.taskTimeStateMessage) {
            if (isReadOnly) {
                el.taskTimeStateMessage.textContent = l10n.TimeEntryReadOnlyInReview || 'Time entry is read-only in review';
                el.taskTimeStateMessage.classList.remove('d-none');
            } else {
                el.taskTimeStateMessage.textContent = '';
                el.taskTimeStateMessage.classList.add('d-none');
            }
        }
    };

    var renderDependencies = function () {
        var deps = state.item.dependencies || {};
        var toDependencyLabel = function (item) {
            if (typeof item === 'string') { return esc(item); }
            if (!item) { return '-'; }
            var id = item.id ? esc(item.id) : '';
            var title = item.title ? esc(item.title) : '';
            if (id && title) { return '<span class="fw-semibold">' + id + '</span> · ' + title; }
            return id || title || '-';
        };

        if (el.taskBlockedByList) {
            if (!deps.blockedBy || !deps.blockedBy.length) {
                el.taskBlockedByList.innerHTML = '<li class="text-muted small">' + esc(l10n.NoBlockers || 'No blockers') + '</li>';
            } else {
                el.taskBlockedByList.innerHTML = deps.blockedBy.map(function (d) {
                    return '<li class="small d-flex align-items-start gap-1 mb-1"><i class="bx bx-block text-danger icon-base mt-1"></i><span>' + toDependencyLabel(d) + '</span></li>';
                }).join('');
            }
        }
        if (el.taskBlocksList) {
            if (!deps.blocks || !deps.blocks.length) {
                el.taskBlocksList.innerHTML = '<li class="text-muted small">' + esc(l10n.NoDependentTasks || 'No dependent tasks') + '</li>';
            } else {
                el.taskBlocksList.innerHTML = deps.blocks.map(function (d) {
                    return '<li class="small d-flex align-items-start gap-1 mb-1"><i class="bx bx-block text-warning icon-base mt-1"></i><span>' + toDependencyLabel(d) + '</span></li>';
                }).join('');
            }
        }
    };

    var renderMetadataSummary = function () {
        var item = state.item;
        var deps = item.dependencies || {};
        var blockedByCount = (deps.blockedBy || []).length;
        var blocksCount = (deps.blocks || []).length;
        var hasDependencies = blockedByCount + blocksCount > 0;
        var hasBlockers = blockedByCount > 0;
        var sourceType = item.sourceType || '';
        var sourceId = item.sourceId || '';
        var sourceBuilder = sourceType ? SOURCE_ROUTE_BUILDERS[sourceType] : null;
        var sourceHref = sourceBuilder && sourceId ? sourceBuilder(sourceId) : '';
        var sourceLabel = (sourceType || '-') + ': ' + (sourceId || '-');

        if (el.taskSourceRefLink && el.taskSourceRefValue) {
            if (sourceHref) {
                el.taskSourceRefLink.href = sourceHref;
                el.taskSourceRefLink.textContent = sourceLabel;
                el.taskSourceRefLink.classList.remove('d-none');
                el.taskSourceRefValue.classList.add('d-none');
            } else {
                el.taskSourceRefValue.textContent = sourceLabel;
                el.taskSourceRefValue.classList.remove('d-none');
                el.taskSourceRefLink.classList.add('d-none');
            }
        } else if (el.taskSourceRefValue) {
            el.taskSourceRefValue.textContent = sourceLabel;
        }
        if (el.taskSourceTitleValue) { el.taskSourceTitleValue.textContent = item.sourceTitle || ''; }
        if (el.taskSourceRelationValue) { el.taskSourceRelationValue.textContent = item.sourceRelationType || '-'; }
        if (el.taskReviewerValue) { el.taskReviewerValue.textContent = item.reviewer || '-'; }
        if (el.taskReassignmentValue) {
            if (item.reassignedFrom) {
                el.taskReassignmentValue.textContent = (l10n.ReassignedFrom || 'Reassigned from') + ' ' + item.reassignedFrom;
            } else {
                el.taskReassignmentValue.textContent = l10n.InitialAssignment || 'Initial assignment';
            }
        }
        if (el.taskPlannedAtValue) { el.taskPlannedAtValue.textContent = item.plannedAt ? formatDate(item.plannedAt) : (l10n.PlannedDateUnknown || 'Not planned'); }

        if (el.taskDependenciesStateBadge) {
            if (hasBlockers && !item.blocked) {
                el.taskDependenciesStateBadge.className = 'badge bg-label-warning';
                el.taskDependenciesStateBadge.textContent = l10n.BlockedStatusMismatch || 'Dependency blockers exist';
            } else if (hasDependencies) {
                el.taskDependenciesStateBadge.className = 'badge bg-label-warning';
                el.taskDependenciesStateBadge.textContent = (l10n.DependencyStateFilled || 'Has dependencies') + ' (' + blockedByCount + '/' + blocksCount + ')';
            } else {
                el.taskDependenciesStateBadge.className = 'badge bg-label-success';
                el.taskDependenciesStateBadge.textContent = l10n.NoDependencies || 'No dependencies';
            }
        }

        if (el.taskTagsList) {
            if (!item.tags || !item.tags.length) {
                el.taskTagsList.innerHTML = '<span class="text-muted small">' + esc(l10n.NoTags || 'No tags') + '</span>';
            } else {
                el.taskTagsList.innerHTML = item.tags.map(function (tag) {
                    return '<span class="badge bg-label-secondary">' + esc(tag) + '</span>';
                }).join('');
            }
        }
    };

    var renderSecondaryMeta = function () {
        var item = state.item;

        if (el.taskWatchersList) {
            if (!item.watchers || !item.watchers.length) {
                el.taskWatchersList.innerHTML = '<li class="text-muted small">' + esc(l10n.NoWatchers || 'No watchers') + '</li>';
            } else {
                el.taskWatchersList.innerHTML = item.watchers.map(function (w) {
                    return '<li class="small d-flex align-items-center gap-1 mb-1"><i class="bx bx-user-circle text-muted icon-base"></i>' + esc(w) + '</li>';
                }).join('');
            }
        }

        if (el.taskAttachmentsList) {
            if (!item.attachments || !item.attachments.length) {
                el.taskAttachmentsList.innerHTML = '<li class="text-muted small">' + esc(l10n.NoAttachments || 'No attachments') + '</li>';
            } else {
                el.taskAttachmentsList.innerHTML = item.attachments.map(function (a) {
                    return '<li class="small d-flex align-items-center gap-1 mb-1"><i class="bx bx-paperclip text-muted icon-base"></i><span>' + esc(a.name) + '</span><span class="text-muted">(' + esc(a.size) + ')</span></li>';
                }).join('');
            }
        }

        if (el.taskMarketValue) { el.taskMarketValue.textContent = item.market || '-'; }
        if (el.taskDomainValue) { el.taskDomainValue.textContent = item.domain || '-'; }
        if (el.taskExternalPartyValue) { el.taskExternalPartyValue.textContent = item.externalParty || '-'; }
        if (el.taskEstimationValue) { el.taskEstimationValue.textContent = item.estimation || '-'; }
    };

    var renderAll = function () {
        renderHeader();
        renderStepBar();
        renderDescription();
        renderSubtasks();
        renderActivity();
        renderActionsPanel();
        renderTimeEntryState();
        renderTimeLog();
        renderMetadataSummary();
        renderDependencies();
        renderSecondaryMeta();
    };

    // ── ACTION HANDLERS ───────────────────────────────────────────────────────
    var showActionFeedback = function (msg) {
        if (!el.taskActionFeedback) { return; }
        el.taskActionFeedback.textContent = msg;
        el.taskActionFeedback.classList.remove('d-none');
        setTimeout(function () { el.taskActionFeedback.classList.add('d-none'); }, 5000);
    };

    var clearActionFeedback = function () {
        if (el.taskActionFeedback) { el.taskActionFeedback.classList.add('d-none'); }
    };

    var addActivity = function (text, author, type) {
        state.item.activity.push({
            type:      type || 'status_change',
            text:      text,
            author:    author || 'You',
            timestamp: new Date().toISOString().slice(0, 10)
        });
    };

    var handleAction = function (action) {
        var item = state.item;
        clearActionFeedback();

        switch (action) {
            case 'accept':
                addActivity('Status changed to Open.', 'You', 'status_change');
                item.status = 'Open';
                item.approvalRequired = false;
                notify(l10n.ActionAcceptSuccess || 'Task accepted.', 'success');
                break;

            case 'reject':
                addActivity('Task rejected.', 'You', 'status_change');
                item.status = 'Cancelled';
                notify(l10n.ActionRejectSuccess || 'Task rejected.', 'warning');
                break;

            case 'requestInfo':
                addActivity('Additional information requested.', 'You', 'status_change');
                item.status = 'Waiting for Information';
                item.waitingInfo = item.waitingInfo || 'Business Owner';
                notify(l10n.RequestInfo || 'Request Info sent.', 'info');
                break;

            case 'reassign':
                notify(l10n.ActionReassignSuccess || 'Reassign flow started (mock).', 'info');
                return; // no status change

            case 'plan':
                addActivity('Task planned.', 'You', 'status_change');
                item.status = 'Planned';
                notify(l10n.Plan || 'Task planned.', 'success');
                break;

            case 'startWork':
                addActivity('Work started.', 'You', 'status_change');
                item.status = 'In Progress';
                item.blocked = false;
                item.blockedReason = '';
                notify(l10n.StartWork || 'Work started.', 'success');
                break;

            case 'approve':
                if (item.status === 'Pending Approval') {
                    addActivity('Approval completed. Item moved to Pending Acceptance.', 'You', 'status_change');
                    item.status = 'Pending Acceptance';
                    item.approvalRequired = false;
                    item.approver = '';
                } else {
                    addActivity('Review approved. Item closed.', 'You', 'status_change');
                    item.status = 'Closed';
                }
                notify(l10n.Approve || 'Task approved.', 'success');
                break;

            case 'rejectReview':
                if (el.taskRejectReviewForm) {
                    el.taskRejectReviewForm.classList.toggle('d-none');
                }
                return; // handled by form submit

            case 'continueWork':
            case 'investigate':
            case 'followUp':
            case 'review':
            case 'inspectBlocker':
            case 'viewSummary':
            case 'viewReason':
                notify('Mock action opened contextual details.', 'info');
                return;

            default:
                break;
        }

        renderAll();
    };

    // ── TIMER ─────────────────────────────────────────────────────────────────
    var updateTimerDisplay = function () {
        if (!state.timerActive || !state.timerStartMs) { return; }
        if (el.taskTimerDisplay) {
            el.taskTimerDisplay.textContent = formatDuration(Date.now() - state.timerStartMs);
        }
    };

    var startTimer = function () {
        state.timerActive  = true;
        state.timerStartMs = Date.now();
        if (el.taskTimerIcon)   { el.taskTimerIcon.className = 'bx bx-stop icon-base'; }
        if (el.taskTimerToggle) { el.taskTimerToggle.setAttribute('title', l10n.StopTimer || 'Stop Timer'); }
        state.timerInterval = setInterval(updateTimerDisplay, 1000);
    };

    var stopTimer = function () {
        state.timerActive = false;
        clearInterval(state.timerInterval);
        state.timerInterval = null;

        if (state.timerStartMs) {
            var elapsed  = Date.now() - state.timerStartMs;
            var totalMin = Math.floor(elapsed / 60000);
            if (el.taskTimeHours)   { el.taskTimeHours.value   = Math.floor(totalMin / 60); }
            if (el.taskTimeMinutes) { el.taskTimeMinutes.value = totalMin % 60; }
        }
        state.timerStartMs = null;
        if (el.taskTimerIcon)    { el.taskTimerIcon.className = 'bx bx-play icon-base'; }
        if (el.taskTimerToggle)  { el.taskTimerToggle.setAttribute('title', l10n.StartTimer || 'Start Timer'); }
        if (el.taskTimerDisplay) { el.taskTimerDisplay.textContent = '0:00:00'; }
    };

    // ── EVENT BINDING ─────────────────────────────────────────────────────────
    var bindEvents = function () {
        // Back navigation
        el.taskBackBtn && el.taskBackBtn.addEventListener('click', function (e) {
            e.preventDefault();
            window.location.href = RETURN_URL;
        });

        // Actions panel (event delegation)
        el.taskActionsPanel && el.taskActionsPanel.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-task-action]');
            if (!btn) { return; }
            handleAction(btn.getAttribute('data-task-action'));
        });

        // Reject review note input enables confirm button
        el.taskRejectNote && el.taskRejectNote.addEventListener('input', function () {
            if (el.taskRejectReviewConfirm) {
                el.taskRejectReviewConfirm.disabled = !el.taskRejectNote.value.trim();
            }
        });

        // Reject review confirm
        el.taskRejectReviewConfirm && el.taskRejectReviewConfirm.addEventListener('click', function () {
            var note = el.taskRejectNote ? el.taskRejectNote.value.trim() : '';
            if (!note) { return; }
            addActivity('Review rejected: ' + note, 'You', 'comment');
            state.item.status = 'In Progress';
            if (el.taskRejectReviewForm) { el.taskRejectReviewForm.classList.add('d-none'); }
            if (el.taskRejectNote)       { el.taskRejectNote.value = ''; }
            if (el.taskRejectReviewConfirm) { el.taskRejectReviewConfirm.disabled = true; }
            notify(l10n.RejectReview || 'Review rejected, task returned to in progress.', 'warning');
            renderAll();
        });

        // Comment submit
        el.taskCommentSubmit && el.taskCommentSubmit.addEventListener('click', function () {
            var text = el.taskCommentInput ? el.taskCommentInput.value.trim() : '';
            if (!text) { return; }
            addActivity(text, 'You', 'comment');
            if (el.taskCommentInput) { el.taskCommentInput.value = ''; }
            renderActivity();
            notify(l10n.CommentAdded || 'Comment added.', 'success');
        });

        // Time log
        el.taskTimeLogBtn && el.taskTimeLogBtn.addEventListener('click', function () {
            if (!isTimeEntryEnabledStatus()) { return; }
            var h = parseInt(el.taskTimeHours   ? el.taskTimeHours.value   : '0', 10) || 0;
            var m = parseInt(el.taskTimeMinutes ? el.taskTimeMinutes.value : '0', 10) || 0;
            if (h === 0 && m === 0) { return; }
            var note = el.taskTimeNote ? el.taskTimeNote.value.trim() : '';
            state.timeEntries.push({
                hours:    h,
                minutes:  m,
                note:     note,
                loggedAt: new Date().toLocaleDateString()
            });
            if (el.taskTimeHours)   { el.taskTimeHours.value   = ''; }
            if (el.taskTimeMinutes) { el.taskTimeMinutes.value = ''; }
            if (el.taskTimeNote)    { el.taskTimeNote.value    = ''; }
            renderTimeLog();
            notify(l10n.TimeLogged || 'Time logged.', 'success');
        });

        // Timer toggle
        el.taskTimerToggle && el.taskTimerToggle.addEventListener('click', function () {
            if (!isTimeEntryEnabledStatus()) { return; }
            if (state.timerActive) { stopTimer(); } else { startTimer(); }
        });

        // Collapse chevron rotation — driven by Bootstrap collapse events
        var collapses = document.querySelectorAll('#taskDetailPage [data-bs-toggle="collapse"]');
        collapses.forEach(function (trigger) {
            var targetId = (trigger.getAttribute('data-bs-target') || '').replace('#', '');
            var target   = targetId ? document.getElementById(targetId) : null;
            if (!target) { return; }
            target.addEventListener('shown.bs.collapse', function () {
                var chevron = trigger.querySelector('.task-collapse-chevron');
                if (chevron) { chevron.classList.add('task-collapse-chevron--open'); }
            });
            target.addEventListener('hidden.bs.collapse', function () {
                var chevron = trigger.querySelector('.task-collapse-chevron');
                if (chevron) { chevron.classList.remove('task-collapse-chevron--open'); }
            });
        });
    };

    // ── INIT ──────────────────────────────────────────────────────────────────
    bindEvents();
    renderAll();

})();
