'use strict';

(function (global) {
    const TODAY_ISO = '2026-03-30T09:00:00';
    const TODAY = new Date(TODAY_ISO);
    const CURRENT_USER = {
        name: 'Selin Aras',
        title: 'Operations PMO Lead'
    };

    const LIFECYCLE_STATUSES = [
        'Pending Approval',
        'Pending Acceptance',
        'Open',
        'Planned',
        'In Progress',
        'Waiting for Information',
        'Pending Review',
        'Closed',
        'Cancelled'
    ];

    const clone = (value) => {
        if (typeof global.structuredClone === 'function') {
            return global.structuredClone(value);
        }

        return JSON.parse(JSON.stringify(value));
    };

    const toIsoDate = (date) => date.toISOString().slice(0, 10);

    const addDays = (days) => {
        const date = new Date(TODAY);
        date.setDate(date.getDate() + days);
        return toIsoDate(date);
    };

    const humanize = (value) => {
        if (!value) {
            return '-';
        }

        const text = String(value);
        return text.charAt(0).toUpperCase() + text.slice(1);
    };

    const parseChecklistProgress = (value) => {
        const parts = String(value || '').split('/');
        const done = parseInt(parts[0], 10);
        const total = parseInt(parts[1], 10);

        return {
            done: Number.isFinite(done) ? done : 0,
            total: Number.isFinite(total) ? total : 0
        };
    };

    const resolveViewerRole = (item) => {
        if (item.approver && item.approver === CURRENT_USER.name) {
            return 'Approver';
        }

        if (item.reviewer && item.reviewer === CURRENT_USER.name) {
            return 'Reviewer';
        }

        if (item.assignee && item.assignee === CURRENT_USER.name) {
            return 'Owner';
        }

        if (item.creator && item.creator === CURRENT_USER.name) {
            return 'Creator';
        }

        return '';
    };

    const computeDueState = (value) => {
        if (!value) {
            return { kind: 'unknown', label: '-' };
        }

        const dueDate = new Date(value);
        const todayDate = new Date(TODAY.getFullYear(), TODAY.getMonth(), TODAY.getDate());
        const compareDate = new Date(dueDate.getFullYear(), dueDate.getMonth(), dueDate.getDate());
        const diffDays = Math.floor((compareDate.getTime() - todayDate.getTime()) / 86400000);

        if (diffDays < 0) {
            return { kind: 'overdue', label: 'Overdue' };
        }

        if (diffDays <= 2) {
            return { kind: 'due_soon', label: 'Due Soon' };
        }

        return { kind: 'on_track', label: 'On Track' };
    };

    const buildRequiredAction = (item) => {
        if (item.status === 'Pending Approval') {
            return item.approver === CURRENT_USER.name
                ? 'Approve or reject the request'
                : 'Await approver decision';
        }

        if (item.status === 'Pending Acceptance') {
            return 'Accept ownership or reassign';
        }

        if (item.status === 'Open') {
            return item.type === 'issue' ? 'Investigate root cause' : 'Continue execution';
        }

        if (item.status === 'Planned') {
            return item.blocked ? 'Inspect blocker before start' : 'Start execution when ready';
        }

        if (item.status === 'In Progress') {
            return item.blocked ? 'Resolve blocker and continue' : 'Continue active work';
        }

        if (item.status === 'Waiting for Information') {
            return 'Follow up with the requested owner';
        }

        if (item.status === 'Pending Review') {
            return item.reviewer === CURRENT_USER.name
                ? 'Review the submitted deliverable'
                : 'Await reviewer decision';
        }

        if (item.status === 'Closed') {
            return 'Review completion summary';
        }

        if (item.status === 'Cancelled') {
            return 'Review cancellation context';
        }

        return 'Open detail';
    };

    const buildFlags = (item) => {
        const checklist = parseChecklistProgress(item.checklistProgress);
        const flags = [];

        if (item.blocked) {
            flags.push({
                label: 'Blocked',
                kind: 'danger',
                title: item.blockedReason || 'Blocked'
            });
        }

        if (item.dependencySummary) {
            flags.push({
                label: 'Dependency',
                kind: 'warning',
                title: item.dependencySummary
            });
        }

        if (item.waitingInfo) {
            flags.push({
                label: `Waiting: ${item.waitingInfo}`,
                kind: 'warning',
                title: `Waiting for ${item.waitingInfo}`
            });
        }

        if (item.reviewRequired) {
            flags.push({
                label: 'Review',
                kind: 'info',
                title: item.reviewer ? `Reviewer: ${item.reviewer}` : 'Review required'
            });
        }

        if (item.approvalRequired) {
            flags.push({
                label: 'Approval',
                kind: 'primary',
                title: item.approver ? `Approver: ${item.approver}` : 'Approval required'
            });
        }

        if (item.hasChecklist) {
            const isComplete = checklist.total > 0 && checklist.done >= checklist.total;
            flags.push({
                label: `Checklist ${item.checklistProgress || '0/0'}`,
                kind: isComplete ? 'success' : 'secondary',
                title: `Checklist progress ${item.checklistProgress || '0/0'}`
            });
        }

        if (item.hasSubtasks) {
            flags.push({
                label: 'Subtasks',
                kind: 'secondary',
                title: 'Contains subtasks'
            });
        }

        return flags;
    };

    const isViewerApprover = (item) => item.approver && item.approver === CURRENT_USER.name;
    const isViewerAssignee = (item) => item.assignee && item.assignee === CURRENT_USER.name;
    const isViewerReviewer = (item) => item.reviewer && item.reviewer === CURRENT_USER.name;

    const getListActionConfig = (item) => {
        const blockedPrimary = {
            action: 'inspect-blocker',
            label: 'Inspect Blocker',
            icon: 'bx bx-block icon-base',
            variant: 'btn-label-warning'
        };

        if (item.status === 'Pending Approval') {
            if (isViewerApprover(item)) {
                return {
                    primary: {
                        action: 'approve',
                        label: 'Approve',
                        icon: 'bx bx-check-shield icon-base',
                        variant: 'btn-label-success'
                    },
                    secondary: [
                        {
                            action: 'reject',
                            label: 'Reject',
                            icon: 'bx bx-x-circle',
                            variant: 'btn-label-danger'
                        }
                    ],
                    bulkSelectable: false
                };
            }

            return {
                primary: {
                    action: 'await-approval',
                    label: 'Await Approver',
                    icon: 'bx bx-hourglass icon-base',
                    variant: 'btn-label-secondary',
                    disabled: true
                },
                secondary: [],
                bulkSelectable: false
            };
        }

        if (item.status === 'Pending Acceptance') {
            if (isViewerAssignee(item)) {
                return {
                    primary: {
                        action: 'accept',
                        label: 'Accept',
                        icon: 'bx bx-check icon-base',
                        variant: 'btn-label-success'
                    },
                    secondary: [
                        {
                            action: 'reassign',
                            label: 'Reassign',
                            icon: 'bx bx-user-pin',
                            variant: 'btn-label-secondary'
                        }
                    ],
                    bulkSelectable: true
                };
            }

            return {
                primary: {
                    action: 'await-assignee',
                    label: 'Await Assignee',
                    icon: 'bx bx-hourglass icon-base',
                    variant: 'btn-label-secondary',
                    disabled: true
                },
                secondary: [],
                bulkSelectable: false
            };
        }

        if (item.status === 'Open') {
            return {
                primary: item.blocked
                    ? blockedPrimary
                    : {
                        action: item.type === 'issue' ? 'investigate' : 'continue',
                        label: item.type === 'issue' ? 'Investigate' : 'Continue',
                        icon: item.type === 'issue' ? 'bx bx-search icon-base' : 'bx bx-right-arrow-alt icon-base',
                        variant: 'btn-label-primary'
                    },
                secondary: [
                    {
                        action: 'request-info',
                        label: 'Request Info',
                        icon: 'bx bx-question-mark',
                        variant: 'btn-label-warning'
                    },
                    {
                        action: 'reassign',
                        label: 'Reassign',
                        icon: 'bx bx-user-pin',
                        variant: 'btn-label-secondary'
                    }
                ],
                bulkSelectable: false
            };
        }

        if (item.status === 'Planned') {
            return {
                primary: item.blocked
                    ? blockedPrimary
                    : {
                        action: 'start-work',
                        label: 'Start Work',
                        icon: 'bx bx-play icon-base',
                        variant: 'btn-label-primary'
                    },
                secondary: [
                    {
                        action: 'reassign',
                        label: 'Reassign',
                        icon: 'bx bx-user-pin',
                        variant: 'btn-label-secondary'
                    }
                ],
                bulkSelectable: false
            };
        }

        if (item.status === 'In Progress') {
            return {
                primary: item.blocked
                    ? blockedPrimary
                    : {
                        action: item.type === 'issue' ? 'investigate' : 'continue',
                        label: item.type === 'issue' ? 'Investigate' : 'Continue',
                        icon: item.type === 'issue' ? 'bx bx-search icon-base' : 'bx bx-loader-circle icon-base',
                        variant: 'btn-label-primary'
                    },
                secondary: [
                    {
                        action: 'request-info',
                        label: 'Request Info',
                        icon: 'bx bx-question-mark',
                        variant: 'btn-label-warning'
                    },
                    {
                        action: 'reassign',
                        label: 'Reassign',
                        icon: 'bx bx-user-pin',
                        variant: 'btn-label-secondary'
                    }
                ],
                bulkSelectable: false
            };
        }

        if (item.status === 'Waiting for Information') {
            return {
                primary: {
                    action: 'follow-up',
                    label: 'Follow Up',
                    icon: 'bx bx-refresh icon-base',
                    variant: 'btn-label-warning'
                },
                secondary: [
                    {
                        action: 'reassign',
                        label: 'Reassign',
                        icon: 'bx bx-user-pin',
                        variant: 'btn-label-secondary'
                    }
                ],
                bulkSelectable: false
            };
        }

        if (item.status === 'Pending Review') {
            if (isViewerReviewer(item)) {
                return {
                    primary: {
                        action: 'review',
                        label: 'Review',
                        icon: 'bx bx-check-circle icon-base',
                        variant: 'btn-label-success'
                    },
                    secondary: [
                        {
                            action: 'reject-review',
                            label: 'Reject Review',
                            icon: 'bx bx-x-circle',
                            variant: 'btn-label-danger'
                        }
                    ],
                    bulkSelectable: false
                };
            }

            return {
                primary: {
                    action: 'await-reviewer',
                    label: 'Await Reviewer',
                    icon: 'bx bx-hourglass icon-base',
                    variant: 'btn-label-secondary',
                    disabled: true
                },
                secondary: [],
                bulkSelectable: false
            };
        }

        if (item.status === 'Closed') {
            return {
                primary: {
                    action: 'view-summary',
                    label: 'View Summary',
                    icon: 'bx bx-file icon-base',
                    variant: 'btn-label-secondary'
                },
                secondary: [
                    {
                        action: 'history',
                        label: 'History',
                        icon: 'bx bx-history',
                        variant: 'btn-label-secondary'
                    }
                ],
                bulkSelectable: false
            };
        }

        if (item.status === 'Cancelled') {
            return {
                primary: {
                    action: 'view-reason',
                    label: 'View Reason',
                    icon: 'bx bx-info-circle icon-base',
                    variant: 'btn-label-secondary'
                },
                secondary: [],
                bulkSelectable: false
            };
        }

        return {
            primary: {
                action: 'open-detail',
                label: 'Open Detail',
                icon: 'bx bx-right-arrow-alt icon-base',
                variant: 'btn-label-secondary'
            },
            secondary: [],
            bulkSelectable: false
        };
    };

    const rawItems = [
        {
            id: 'TASK-4101',
            title: 'Approve regional campaign discount release',
            type: 'task',
            status: 'Pending Approval',
            assignee: 'Can Yildiz',
            creator: 'Burcu Korkmaz',
            reviewer: '',
            approver: 'Selin Aras',
            priority: 'high',
            createdDate: addDays(-6),
            dueDate: addDays(1),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '4/4',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: true,
            source: 'Pricing Ops',
            context: 'Project Atlas'
        },
        {
            id: 'TASK-4201',
            title: 'Take ownership of quarter-end freight accrual reconciliation',
            type: 'task',
            status: 'Pending Acceptance',
            assignee: 'Selin Aras',
            creator: 'Emre Gunes',
            reviewer: 'Kerem Isik',
            approver: '',
            priority: 'high',
            createdDate: addDays(-4),
            dueDate: addDays(2),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '0/4',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Finance',
            context: 'Month End Close'
        },
        {
            id: 'TASK-4301',
            title: 'Investigate export VAT mismatch in SAP handoff',
            type: 'issue',
            status: 'Open',
            assignee: 'Selin Aras',
            creator: 'Mert Aksoy',
            reviewer: '',
            approver: '',
            priority: 'high',
            createdDate: addDays(-9),
            dueDate: addDays(-1),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '1/5',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Tax',
            context: 'Export Operations'
        },
        {
            id: 'TASK-4401',
            title: 'Plan monthly inventory cycle count pilot',
            type: 'task',
            status: 'Planned',
            assignee: 'Selin Aras',
            creator: 'Aylin Ersoy',
            reviewer: '',
            approver: '',
            priority: 'medium',
            createdDate: addDays(-4),
            dueDate: addDays(4),
            blocked: false,
            blockedReason: '',
            dependencySummary: 'Plan aligns with warehouse freeze window on 2026-04-03.',
            hasChecklist: true,
            checklistProgress: '3/5',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Warehouse Ops',
            context: 'Cycle Count Pilot'
        },
        {
            id: 'TASK-4501',
            title: 'Continue pricing approval rule hotfix',
            type: 'issue',
            status: 'In Progress',
            assignee: 'Selin Aras',
            creator: 'Burcu Korkmaz',
            reviewer: 'Kerem Isik',
            approver: '',
            priority: 'high',
            createdDate: addDays(-3),
            dueDate: addDays(1),
            blocked: false,
            blockedReason: '',
            dependencySummary: 'Linked to API rollout subtask PRC-81.',
            hasChecklist: true,
            checklistProgress: '2/5',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Pricing Engine',
            context: 'Approval Matrix'
        },
        {
            id: 'TASK-4601',
            title: 'Wait for QA evidence pack to close deviation CAPA',
            type: 'task',
            status: 'Waiting for Information',
            assignee: 'Selin Aras',
            creator: 'Melis Yalcin',
            reviewer: 'Selin Aras',
            approver: '',
            priority: 'high',
            createdDate: addDays(-18),
            dueDate: addDays(-7),
            blocked: true,
            blockedReason: 'The signed lab report has not been uploaded.',
            dependencySummary: 'Cannot close deviation until QA uploads the signed report.',
            hasChecklist: true,
            checklistProgress: '4/6',
            hasSubtasks: true,
            waitingInfo: 'QA - Aysegul Karaca',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Quality',
            context: 'Deviation CAPA'
        },
        {
            id: 'TASK-4701',
            title: 'Review payroll interface timeout fix with load evidence',
            type: 'issue',
            status: 'Pending Review',
            assignee: 'Okan Bal',
            creator: 'Mert Aksoy',
            reviewer: 'Selin Aras',
            approver: '',
            priority: 'high',
            createdDate: addDays(-9),
            dueDate: addDays(-2),
            blocked: false,
            blockedReason: '',
            dependencySummary: 'Review queued after load test evidence was uploaded.',
            hasChecklist: true,
            checklistProgress: '5/5',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Payroll Integrations',
            context: 'Timeout Fix'
        },
        {
            id: 'TASK-4801',
            title: 'Closed supplier tax code normalization follow-up',
            type: 'task',
            status: 'Closed',
            assignee: 'Selin Aras',
            creator: 'Levent Demir',
            reviewer: 'Selin Aras',
            approver: '',
            priority: 'medium',
            createdDate: addDays(-13),
            dueDate: addDays(-1),
            blocked: false,
            blockedReason: '',
            dependencySummary: 'Resolved after tax engine patch deployment.',
            hasChecklist: true,
            checklistProgress: '5/5',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Tax Master Data',
            context: 'Supplier Tax Codes'
        },
        {
            id: 'TASK-4901',
            title: 'Cancelled legacy WMS decommission checklist',
            type: 'task',
            status: 'Cancelled',
            assignee: 'Selin Aras',
            creator: 'Aylin Ersoy',
            reviewer: '',
            approver: 'Mert Aksoy',
            priority: 'medium',
            createdDate: addDays(-5),
            dueDate: addDays(8),
            blocked: false,
            blockedReason: '',
            dependencySummary: 'Superseded by WMS-Next migration workstream.',
            hasChecklist: true,
            checklistProgress: '2/5',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: true,
            source: 'Warehouse IT',
            context: 'Legacy Decommission'
        }
    ];

    const items = rawItems.map((item, index) => {
        const dueState = computeDueState(item.dueDate);

        return {
            ...item,
            assignedBy: item.creator,
            displayType: humanize(item.type),
            displayPriority: humanize(item.priority),
            dueStateKind: dueState.kind,
            dueStateLabel: dueState.label,
            viewerRole: resolveViewerRole(item),
            requiredAction: buildRequiredAction(item),
            meta: `${item.source} · ${item.context}`,
            flags: buildFlags(item),
            isUnread: index % 3 !== 0
        };
    });

    const buildMockItems = () => clone(items);

    global.WorkCenterMockData = {
        currentUser: CURRENT_USER,
        todayIso: TODAY_ISO,
        lifecycleStatuses: LIFECYCLE_STATUSES.slice(),
        computeDueState,
        buildMockItems,
        getListActionConfig
    };
})(window);
