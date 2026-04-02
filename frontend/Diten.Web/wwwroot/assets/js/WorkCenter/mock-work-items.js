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
            id: 'TASK-4102',
            title: 'Vendor merge exception approval for duplicate tax IDs',
            type: 'issue',
            status: 'Pending Approval',
            assignee: 'Selin Aras',
            creator: 'Levent Demir',
            reviewer: 'Nadia Peker',
            approver: 'Mert Aksoy',
            priority: 'medium',
            createdDate: addDays(-10),
            dueDate: addDays(5),
            blocked: false,
            blockedReason: '',
            dependencySummary: 'Awaiting MDM-221 duplicate merge validation output.',
            hasChecklist: true,
            checklistProgress: '2/5',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: true,
            source: 'Master Data',
            context: 'Supplier Cleanup'
        },
        {
            id: 'TASK-4103',
            title: 'Distributor contract waiver needs approval packet fix',
            type: 'task',
            status: 'Pending Approval',
            assignee: 'Okan Bal',
            creator: 'Aylin Ersoy',
            reviewer: '',
            approver: 'Selin Aras',
            priority: 'high',
            createdDate: addDays(-8),
            dueDate: addDays(-2),
            blocked: true,
            blockedReason: 'Finance impact note is missing from the approval packet.',
            dependencySummary: 'Depends on FIN-774 cost impact memo.',
            hasChecklist: true,
            checklistProgress: '1/3',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: true,
            source: 'Commercial',
            context: 'Azerbaijan Expansion'
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
            id: 'TASK-4202',
            title: 'Accept barcode printer outage root cause analysis',
            type: 'issue',
            status: 'Pending Acceptance',
            assignee: 'Selin Aras',
            creator: 'Bora Tunc',
            reviewer: 'Derya Akin',
            approver: '',
            priority: 'high',
            createdDate: addDays(-7),
            dueDate: addDays(-3),
            blocked: true,
            blockedReason: 'Infra logs are still missing from the spooler host.',
            dependencySummary: 'Start blocked until infra team restores print spooler logs.',
            hasChecklist: false,
            checklistProgress: '0/0',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Warehouse IT',
            context: 'WH-07 Incident'
        },
        {
            id: 'TASK-4203',
            title: 'Own steering committee prep dry run logistics',
            type: 'meeting',
            status: 'Pending Acceptance',
            assignee: 'Selin Aras',
            creator: 'Ceren Kaya',
            reviewer: '',
            approver: '',
            priority: 'medium',
            createdDate: addDays(-3),
            dueDate: addDays(6),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '3/3',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'PMO',
            context: 'Q3 Steering'
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
            id: 'TASK-4302',
            title: 'Continue vendor SLA annex redline after legal template update',
            type: 'task',
            status: 'Open',
            assignee: 'Selin Aras',
            creator: 'Nadia Peker',
            reviewer: 'Melis Yalcin',
            approver: '',
            priority: 'medium',
            createdDate: addDays(-5),
            dueDate: addDays(1),
            blocked: true,
            blockedReason: 'Legal template v4 has not been published yet.',
            dependencySummary: 'Depends on LGL-109 template publication.',
            hasChecklist: true,
            checklistProgress: '2/4',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Legal',
            context: 'Vendor Governance'
        },
        {
            id: 'TASK-4303',
            title: 'Triage warehouse slotting improvement notes',
            type: 'note',
            status: 'Open',
            assignee: 'Selin Aras',
            creator: 'Kerem Isik',
            reviewer: '',
            approver: '',
            priority: 'low',
            createdDate: addDays(-2),
            dueDate: addDays(7),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: false,
            checklistProgress: '0/0',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Warehouse Excellence',
            context: 'Slotting Initiative'
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
            id: 'TASK-4402',
            title: 'Schedule returns portal smoke test window',
            type: 'meeting',
            status: 'Planned',
            assignee: 'Selin Aras',
            creator: 'Onur Cakir',
            reviewer: 'Selin Aras',
            approver: '',
            priority: 'high',
            createdDate: addDays(-6),
            dueDate: addDays(2),
            blocked: true,
            blockedReason: 'Staging data refresh is not complete yet.',
            dependencySummary: 'Cannot start before the refreshed staging dataset is loaded.',
            hasChecklist: true,
            checklistProgress: '1/3',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Digital Commerce',
            context: 'Returns Portal'
        },
        {
            id: 'TASK-4403',
            title: 'Start CRM duplicate lead cleanup wave 2',
            type: 'task',
            status: 'Planned',
            assignee: 'Selin Aras',
            creator: 'Ece Yalcin',
            reviewer: '',
            approver: '',
            priority: 'medium',
            createdDate: addDays(-11),
            dueDate: addDays(-4),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '0/6',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'CRM',
            context: 'Lead Hygiene'
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
            id: 'TASK-4502',
            title: 'Prepare customs broker document pack for release',
            type: 'task',
            status: 'In Progress',
            assignee: 'Selin Aras',
            creator: 'Levent Demir',
            reviewer: 'Mert Aksoy',
            approver: '',
            priority: 'medium',
            createdDate: addDays(-7),
            dueDate: addDays(4),
            blocked: true,
            blockedReason: 'Signed COO attachment is still missing.',
            dependencySummary: 'Waiting on COO sign-off from external broker.',
            hasChecklist: true,
            checklistProgress: '4/7',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Logistics',
            context: 'Broker Handover'
        },
        {
            id: 'TASK-4503',
            title: 'Backfill lease contract metadata for archive migration',
            type: 'task',
            status: 'In Progress',
            assignee: 'Selin Aras',
            creator: 'Nadia Peker',
            reviewer: '',
            approver: '',
            priority: 'low',
            createdDate: addDays(-12),
            dueDate: addDays(-2),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '5/8',
            hasSubtasks: true,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Archive',
            context: 'Contract Migration'
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
            id: 'TASK-4602',
            title: 'Wait for regional promo spend confirmation before reforecast',
            type: 'issue',
            status: 'Waiting for Information',
            assignee: 'Selin Aras',
            creator: 'Burak Ozturk',
            reviewer: 'Selin Aras',
            approver: 'Mert Aksoy',
            priority: 'medium',
            createdDate: addDays(-5),
            dueDate: addDays(1),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '1/2',
            hasSubtasks: false,
            waitingInfo: 'Finance Controller - Burak Ozturk',
            reviewRequired: true,
            approvalRequired: true,
            source: 'Commercial Finance',
            context: 'Promo Reforecast'
        },
        {
            id: 'TASK-4603',
            title: 'Hold customer onboarding kickoff minutes until sales updates attendee list',
            type: 'note',
            status: 'Waiting for Information',
            assignee: 'Selin Aras',
            creator: 'Derya Kose',
            reviewer: '',
            approver: '',
            priority: 'low',
            createdDate: addDays(-2),
            dueDate: addDays(6),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '1/1',
            hasSubtasks: false,
            waitingInfo: 'Sales Ops - Derya Kose',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Sales Ops',
            context: 'Onboarding Kickoff'
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
            id: 'TASK-4702',
            title: 'Review translated quality SOP pack for plant rollout',
            type: 'task',
            status: 'Pending Review',
            assignee: 'Selin Aras',
            creator: 'Melis Yalcin',
            reviewer: 'Pinar Ozcan',
            approver: 'Selin Aras',
            priority: 'medium',
            createdDate: addDays(-6),
            dueDate: addDays(2),
            blocked: true,
            blockedReason: 'Assigned reviewer is on leave until Thursday.',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '6/6',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: true,
            source: 'Quality Systems',
            context: 'Plant Rollout'
        },
        {
            id: 'TASK-4703',
            title: 'Review QBR rehearsal notes before exec distribution',
            type: 'meeting',
            status: 'Pending Review',
            assignee: 'Bora Tunc',
            creator: 'Kerem Isik',
            reviewer: 'Selin Aras',
            approver: '',
            priority: 'low',
            createdDate: addDays(-3),
            dueDate: addDays(5),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '2/2',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Executive PMO',
            context: 'QBR Readout'
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
            id: 'TASK-4802',
            title: 'Closed security access recertification retro',
            type: 'meeting',
            status: 'Closed',
            assignee: 'Selin Aras',
            creator: 'Emre Gunes',
            reviewer: '',
            approver: '',
            priority: 'low',
            createdDate: addDays(-8),
            dueDate: addDays(1),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '3/3',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Security',
            context: 'Quarterly Retro'
        },
        {
            id: 'TASK-4803',
            title: 'Closed customer refund note archival',
            type: 'note',
            status: 'Closed',
            assignee: 'Selin Aras',
            creator: 'Ceren Kaya',
            reviewer: '',
            approver: '',
            priority: 'low',
            createdDate: addDays(-4),
            dueDate: addDays(6),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: false,
            checklistProgress: '0/0',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Customer Care',
            context: 'Refund Archive'
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
        },
        {
            id: 'TASK-4902',
            title: 'Cancelled plant visit reschedule sync',
            type: 'meeting',
            status: 'Cancelled',
            assignee: 'Selin Aras',
            creator: 'Onur Cakir',
            reviewer: '',
            approver: '',
            priority: 'low',
            createdDate: addDays(-2),
            dueDate: addDays(1),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '1/2',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: false,
            approvalRequired: false,
            source: 'Plant Operations',
            context: 'Site Visit'
        },
        {
            id: 'TASK-4903',
            title: 'Cancelled duplicate shipment alert tuning request',
            type: 'issue',
            status: 'Cancelled',
            assignee: 'Selin Aras',
            creator: 'Bora Tunc',
            reviewer: 'Selin Aras',
            approver: '',
            priority: 'high',
            createdDate: addDays(-9),
            dueDate: addDays(-5),
            blocked: false,
            blockedReason: '',
            dependencySummary: '',
            hasChecklist: true,
            checklistProgress: '3/3',
            hasSubtasks: false,
            waitingInfo: '',
            reviewRequired: true,
            approvalRequired: false,
            source: 'Logistics Control Tower',
            context: 'Alert Tuning'
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
