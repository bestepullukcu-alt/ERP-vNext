'use strict';

(function () {
    var root = document.getElementById('taskDetailPageV2');
    if (!root) {
        return;
    }

    var taskId = root.dataset.taskId || '';
    var returnUrl = root.dataset.returnUrl || '/WorkCenter';
    var actingAs = root.dataset.actingAs || '';

    var state = {
        loading: true,
        pending: false,
        error: '',
        notice: null,
        task: null,
        openForm: null,
        decisionNote: '',
        collapsed: {},
        forms: {
            planDate: '',
            logDate: '',
            logDescription: '',
            logDuration: '',
            requestRecipientId: '',
            requestQuestion: '',
            reassignAssigneeId: '',
            closureSummary: ''
        }
    };

    var lang = (window.L10n && window.L10n.TaskDetail) ? window.L10n.TaskDetail : {};
    function t(key, defaultVal) {
        return lang[key] !== undefined ? lang[key] : defaultVal;
    }

    var statusLabelMap = {
        PendingApproval: t('StatusPendingApproval', 'Pending Approval'),
        PendingAcceptance: t('StatusPendingAcceptance', 'Pending Acceptance'),
        Open: t('StatusOpen', 'Open'),
        Planned: t('StatusPlanned', 'Planned'),
        InProgress: t('StatusInProgress', 'In Progress'),
        WaitingForInformation: t('StatusWaitingForInformation', 'Waiting for Information'),
        PendingReview: t('StatusPendingReview', 'Pending Review'),
        Closed: t('StatusClosed', 'Closed'),
        Cancelled: t('StatusCancelled', 'Cancelled')
    };

    var dependencyTypeLabelMap = {
        FS: t('DepFS', 'Finish-to-Start'),
        SS: t('DepSS', 'Start-to-Start'),
        FF: t('DepFF', 'Finish-to-Finish'),
        SF: t('DepSF', 'Start-to-Finish')
    };

    var urgencyMap = {
        OnTrack: { label: t('UrgOnTrack', 'On Track'), cls: 'badge bg-label-success' },
        DueSoon: { label: t('UrgDueSoon', 'Due Soon'), cls: 'badge bg-label-warning' },
        Overdue: { label: t('UrgOverdue', 'Overdue'), cls: 'badge bg-label-danger' }
    };

    function escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function statusLabel(status) {
        return statusLabelMap[status] || status || '-';
    }

    function statusBadgeClass(status) {
        switch (status) {
            case 'PendingApproval': return 'badge bg-label-primary';
            case 'PendingAcceptance': return 'badge bg-label-warning';
            case 'Open': return 'badge bg-label-secondary';
            case 'Planned': return 'badge bg-label-warning';
            case 'InProgress': return 'badge bg-label-dark';
            case 'WaitingForInformation': return 'badge bg-label-warning';
            case 'PendingReview': return 'badge bg-label-info';
            case 'Closed': return 'badge bg-label-success';
            case 'Cancelled': return 'badge bg-label-danger';
            default: return 'badge bg-label-secondary';
        }
    }

    function formatDate(value) {
        if (!value) {
            return '-';
        }

        var date = new Date(value);
        if (isNaN(date.getTime())) {
            var plain = String(value);
            if (/^\d{4}-\d{2}-\d{2}$/.test(plain)) {
                return plain.split('-').reverse().join('.');
            }
            return plain;
        }

        var day = String(date.getDate()).padStart(2, '0');
        var month = String(date.getMonth() + 1).padStart(2, '0');
        var year = date.getFullYear();
        return day + '.' + month + '.' + year;
    }

    function formatDateTime(value) {
        if (!value) {
            return '-';
        }

        var date = new Date(value);
        if (isNaN(date.getTime())) {
            return String(value);
        }

        var datePart = formatDate(value);
        var hh = String(date.getHours()).padStart(2, '0');
        var mm = String(date.getMinutes()).padStart(2, '0');
        return datePart + ' ' + hh + ':' + mm;
    }

    function toInputDate(value) {
        if (!value) {
            return '';
        }

        var raw = String(value);
        if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
            return raw;
        }

        var date = new Date(value);
        if (isNaN(date.getTime())) {
            return '';
        }

        return date.toISOString().slice(0, 10);
    }

    function iconForActivity(type) {
        switch (type) {
            case 'transition': return '→';
            case 'timeLog': return '⊙';
            case 'comment': return '◎';
            case 'infoRequest': return '⊗';
            case 'cancellation': return '✕';
            default: return '•';
        }
    }

    function iconClassForActivity(type) {
        switch (type) {
            case 'transition': return 'task-v2-activity-inline-icon--transition';
            case 'timeLog': return 'task-v2-activity-inline-icon--time-log';
            case 'comment': return 'task-v2-activity-inline-icon--comment';
            case 'infoRequest': return 'task-v2-activity-inline-icon--info-request';
            case 'cancellation': return 'task-v2-activity-inline-icon--cancellation';
            default: return 'task-v2-activity-inline-icon--default';
        }
    }

    function apiUrl(path) {
        var query = actingAs ? ('?as=' + encodeURIComponent(actingAs)) : '';
        return path + query;
    }

    function clearNotice() {
        state.notice = null;
    }

    function setNotice(type, message) {
        state.notice = { type: type, message: message };
    }

    function isCollapsed(id) {
        return !!state.collapsed[id];
    }

    function toggleSection(id) {
        state.collapsed[id] = !state.collapsed[id];
        render();
    }

    function syncFormDefaults(task) {
        if (!task) {
            return;
        }

        if (!state.forms.planDate) {
            state.forms.planDate = toInputDate(task.planDate) || toInputDate(task.dueDate);
        }

        if (!state.forms.logDate) {
            state.forms.logDate = new Date().toISOString().slice(0, 10);
        }

        if (!state.forms.requestRecipientId && task.directory && task.directory.length > 0) {
            var defaultRecipient = task.directory.find(function (x) {
                return x.id !== task.assignee.id;
            });
            state.forms.requestRecipientId = defaultRecipient ? defaultRecipient.id : task.directory[0].id;
        }

        if (!state.forms.reassignAssigneeId) {
            state.forms.reassignAssigneeId = task.assignee.id;
        }

        if (!state.forms.closureSummary && task.closureSummary) {
            state.forms.closureSummary = task.closureSummary;
        }
    }

    function setCollapsedIfUnset(id, isCollapsedByDefault) {
        if (state.collapsed[id] === undefined) {
            state.collapsed[id] = !!isCollapsedByDefault;
        }
    }

    function applyPendingAcceptanceCollapseDefaults(task, initialLoad) {
        if (!initialLoad || !task || task.status !== 'PendingAcceptance') {
            return;
        }

        // Reduce low-information visual noise on first load only.
        if (!task.subtasks || task.subtasks.length === 0) {
            setCollapsedIfUnset('subtasks', true);
        }
        if (!task.dependencies || task.dependencies.length === 0) {
            setCollapsedIfUnset('dependencies', true);
        }
        if (!task.informationRequests || task.informationRequests.length === 0) {
            setCollapsedIfUnset('information-requests', true);
        }
        if (!task.timeLogged || !task.timeLogged.entries || task.timeLogged.entries.length === 0) {
            setCollapsedIfUnset('time-logged', true);
        }
    }

    function applyOpenCollapseDefaults(task, initialLoad) {
        if (!initialLoad || !task || task.status !== 'Open') {
            return;
        }

        // Reduce low-information visual noise on first load only.
        if (!task.subtasks || task.subtasks.length === 0) {
            setCollapsedIfUnset('subtasks', true);
        }
        if (!task.dependencies || task.dependencies.length === 0) {
            setCollapsedIfUnset('dependencies', true);
        }
        if (!task.informationRequests || task.informationRequests.length === 0) {
            setCollapsedIfUnset('information-requests', true);
        }
        if (!task.timeLogged || !task.timeLogged.entries || task.timeLogged.entries.length === 0) {
            setCollapsedIfUnset('time-logged', true);
        }
    }

    function applyPlannedCollapseDefaults(task, initialLoad) {
        if (!initialLoad || !task || task.status !== 'Planned') {
            return;
        }

        // Reduce low-information visual noise on first load only.
        if (!task.subtasks || task.subtasks.length === 0) {
            setCollapsedIfUnset('subtasks', true);
        }
        if (!task.dependencies || task.dependencies.length === 0) {
            setCollapsedIfUnset('dependencies', true);
        }
        if (!task.informationRequests || task.informationRequests.length === 0) {
            setCollapsedIfUnset('information-requests', true);
        }
        if (!task.timeLogged || !task.timeLogged.entries || task.timeLogged.entries.length === 0) {
            setCollapsedIfUnset('time-logged', true);
        }
    }

    async function fetchTask(initialLoad) {
        if (initialLoad) {
            state.loading = true;
            render();
        }

        state.error = '';

        try {
            var response = await fetch(apiUrl('/api/tasks/' + encodeURIComponent(taskId)), {
                method: 'GET',
                headers: {
                    Accept: 'application/json',
                    'X-WorkCenter-User': actingAs || ''
                }
            });

            if (!response.ok) {
                throw new Error('Task detail request failed (' + response.status + ').');
            }

            state.task = await response.json();
            syncFormDefaults(state.task);
            applyPendingAcceptanceCollapseDefaults(state.task, initialLoad);
            applyOpenCollapseDefaults(state.task, initialLoad);
            applyPlannedCollapseDefaults(state.task, initialLoad);
        } catch (error) {
            state.error = error && error.message ? error.message : 'Task detail could not be loaded.';
        } finally {
            state.loading = false;
            render();
        }
    }

    async function postAction(path, payload) {
        state.pending = true;
        render();

        try {
            var options = {
                method: 'POST',
                headers: {
                    Accept: 'application/json',
                    'X-WorkCenter-User': actingAs || ''
                }
            };

            if (payload !== undefined) {
                options.headers['Content-Type'] = 'application/json';
                options.body = JSON.stringify(payload);
            }

            var response = await fetch(apiUrl(path), options);
            var body = null;

            try {
                body = await response.json();
            } catch (_err) {
                body = null;
            }

            if (!response.ok) {
                throw new Error((body && body.message) ? body.message : ('Action failed (' + response.status + ').'));
            }

            setNotice('success', (body && body.message) ? body.message : 'Action completed.');
            state.openForm = null;
            clearFormDraftsAfterSubmit();
            await fetchTask(false);
        } catch (error) {
            setNotice('danger', error && error.message ? error.message : 'Action failed.');
            render();
        } finally {
            state.pending = false;
            render();
        }
    }

    function clearFormDraftsAfterSubmit() {
        state.forms.logDescription = '';
        state.forms.logDuration = '';
        state.forms.requestQuestion = '';
    }

    function findAction(actionId) {
        if (!state.task || !Array.isArray(state.task.availableActions)) {
            return null;
        }

        return state.task.availableActions.find(function (action) {
            return action.id === actionId;
        }) || null;
    }

    function actionRoute(actionId, status) {
        var id = encodeURIComponent(taskId);

        switch (actionId) {
            case 'accept': return '/api/tasks/' + id + '/accept';
            case 'plan': return '/api/tasks/' + id + '/plan';
            case 'start': return '/api/tasks/' + id + '/start';
            case 'logTime': return '/api/tasks/' + id + '/log-time';
            case 'requestInfo': return '/api/tasks/' + id + '/request-info';
            case 'resume': return '/api/tasks/' + id + '/resume';
            case 'complete': return '/api/tasks/' + id + '/complete';
            case 'reassign': return '/api/tasks/' + id + '/reassign';
            case 'cancel': return '/api/tasks/' + id + '/cancel';
            case 'approve':
                if (status === 'PendingApproval') {
                    return '/api/tasks/' + id + '/gates/approval/approve';
                }
                return '/api/tasks/' + id + '/gates/review/approve';
            case 'reject':
                if (status === 'PendingApproval') {
                    return '/api/tasks/' + id + '/gates/approval/reject';
                }
                return '/api/tasks/' + id + '/gates/review/reject';
            default:
                return '';
        }
    }

    function actionHasInlineForm(actionId) {
        return actionId === 'logTime' ||
            actionId === 'requestInfo' ||
            actionId === 'complete' ||
            actionId === 'reassign';
    }

    async function handleSwalAction(actionId) {
        var task = state.task;
        if (!task) return;

        var route = actionRoute(actionId, task.status);
        if (!route) return;

        if (actionId === 'approve' || actionId === 'reject') {
            if (task.status === 'PendingApproval' || task.status === 'PendingReview') {
                await postAction(route, { note: state.decisionNote || null });
                return;
            }

            var isApprove = actionId === 'approve';
            var res = await Swal.fire({
                title: isApprove ? 'Approve Task?' : 'Reject Task?',
                text: 'Please provide a note for this decision (optional).',
                input: 'textarea',
                inputValue: state.decisionNote || '',
                showCancelButton: true,
                confirmButtonText: isApprove ? 'Approve' : 'Reject',
                customClass: {
                    confirmButton: isApprove ? 'btn btn-success me-3' : 'btn btn-danger me-3',
                    cancelButton: 'btn btn-label-secondary'
                },
                buttonsStyling: false
            });

            if (res.isConfirmed) {
                await postAction(route, { note: res.value || null });
            }
            return;
        }

        if (actionId === 'logTime') {
            var resLog = await Swal.fire({
                title: 'Log Time',
                html: '<div class="text-start">' +
                      '<div class="mb-3"><label class="form-label">Date</label><input id="swal-log-date" class="form-control" type="date" value="' + escapeHtml(state.forms.logDate) + '"></div>' +
                      '<div class="mb-3"><label class="form-label">Description</label><input id="swal-log-desc" class="form-control" type="text" placeholder="What did you work on?"></div>' +
                      '<div class="mb-3"><label class="form-label">Duration</label><input id="swal-log-dur" class="form-control" type="text" placeholder="e.g. 2h, 1.5h, 01:30"></div>' +
                      '</div>',
                showCancelButton: true,
                confirmButtonText: 'Record Time',
                customClass: { confirmButton: 'btn btn-primary me-3', cancelButton: 'btn btn-label-secondary' },
                buttonsStyling: false,
                preConfirm: function() {
                    return {
                        date: document.getElementById('swal-log-date').value,
                        desc: document.getElementById('swal-log-desc').value,
                        dur: document.getElementById('swal-log-dur').value
                    };
                }
            });
            if (resLog.isConfirmed) {
                var v = resLog.value;
                if (!v.date || !v.desc || !v.dur) { setNotice('warning', 'Date, description and duration are required.'); render(); return; }
                await postAction(route, { date: v.date, description: v.desc, duration: v.dur });
            }
            return;
        }

        if (actionId === 'requestInfo') {
            var options = (task.directory || []).map(function (person) {
                return '<option value="' + escapeHtml(person.id) + '">' + escapeHtml(person.name) + '</option>';
            }).join('');
            var resReq = await Swal.fire({
                title: 'Request Information',
                html: '<div class="text-start">' +
                      '<div class="mb-3"><label class="form-label">Recipient</label><select id="swal-req-rec" class="form-select">' + options + '</select></div>' +
                      '<div class="mb-3"><label class="form-label">Question</label><textarea id="swal-req-q" class="form-control" rows="3" placeholder="What information is needed?"></textarea></div>' +
                      '</div>',
                showCancelButton: true,
                confirmButtonText: 'Send Request',
                customClass: { confirmButton: 'btn btn-primary me-3', cancelButton: 'btn btn-label-secondary' },
                buttonsStyling: false,
                preConfirm: function() {
                    return { rec: document.getElementById('swal-req-rec').value, q: document.getElementById('swal-req-q').value };
                }
            });
            if (resReq.isConfirmed) {
                var rv = resReq.value;
                if (!rv.rec || !rv.q) { setNotice('warning', 'Recipient and question are required.'); render(); return; }
                await postAction(route, { recipientId: rv.rec, question: rv.q });
            }
            return;
        }

        if (actionId === 'reassign') {
            var people = (task.directory || []).map(function (person) {
                return '<option value="' + escapeHtml(person.id) + '">' + escapeHtml(person.name) + '</option>';
            }).join('');
            var resReas = await Swal.fire({
                title: 'Reassign Task',
                html: '<div class="text-start mb-3"><label class="form-label">New Assignee</label><select id="swal-reas-ass" class="form-select">' + people + '</select></div>',
                showCancelButton: true,
                confirmButtonText: 'Reassign',
                customClass: { confirmButton: 'btn btn-primary me-3', cancelButton: 'btn btn-label-secondary' },
                buttonsStyling: false,
                preConfirm: function() { return document.getElementById('swal-reas-ass').value; }
            });
            if (resReas.isConfirmed) {
                if (!resReas.value) { setNotice('warning', 'Assignee is required.'); render(); return; }
                await postAction(route, { assigneeId: resReas.value });
            }
            return;
        }

        if (actionId === 'complete') {
            var resComp = await Swal.fire({
                title: 'Complete Task',
                html: '<div class="text-start mb-3"><label class="form-label">Closure Summary (optional)</label>' +
                      '<textarea id="swal-comp-sum" class="form-control" rows="4" placeholder="Describe what was done..."></textarea></div>',
                showCancelButton: true,
                confirmButtonText: 'Complete',
                customClass: { confirmButton: 'btn btn-primary me-3', cancelButton: 'btn btn-label-secondary' },
                buttonsStyling: false,
                preConfirm: function() { return document.getElementById('swal-comp-sum').value; }
            });
            if (resComp.isConfirmed) {
                await postAction(route, { closureSummary: resComp.value || null });
            }
            return;
        }

        if (actionId === 'cancel') {
            var resCancel = await Swal.fire({
                title: 'Cancel Task',
                html: '<div class="text-start mb-3"><label class="form-label">Reason (optional)</label>' +
                      '<textarea id="swal-cancel-reason" class="form-control" rows="3" placeholder="Why is this task being cancelled?"></textarea></div>',
                showCancelButton: true,
                confirmButtonText: 'Cancel Task',
                customClass: { confirmButton: 'btn btn-danger me-3', cancelButton: 'btn btn-label-secondary' },
                buttonsStyling: false,
                preConfirm: function() { return document.getElementById('swal-cancel-reason').value; }
            });
            if (resCancel.isConfirmed) {
                await postAction(route, { reason: resCancel.value || null });
            }
            return;
        }
    }

    async function executeDirectAction(actionId) {
        var task = state.task;
        if (!task) {
            return;
        }

        var action = findAction(actionId);
        if (!action) {
            setNotice('warning', 'Action is not available in current state.');
            render();
            return;
        }

        if (action.isBlocked) {
            setNotice('warning', action.blockReason || 'Action is blocked.');
            render();
            return;
        }

        if (actionId === 'plan' && task.status === 'Open') {
            state.openForm = state.openForm === 'plan' ? null : 'plan';
            if (!state.forms.planDate) {
                state.forms.planDate = toInputDate(task.planDate) || toInputDate(task.dueDate);
            }
            render();
            return;
        }

        if (task.status === 'InProgress' && (actionId === 'complete' || actionId === 'logTime' || actionId === 'requestInfo')) {
            state.openForm = state.openForm === actionId ? null : actionId;

            if (actionId === 'logTime' && !state.forms.logDate) {
                state.forms.logDate = new Date().toLocaleDateString('en-CA');
            }

            if (actionId === 'requestInfo' && !state.forms.requestRecipientId && task.directory && task.directory.length > 0) {
                var defaultRecipient = task.directory.find(function (x) {
                    return x.id !== task.assignee.id;
                });
                state.forms.requestRecipientId = defaultRecipient ? defaultRecipient.id : task.directory[0].id;
            }

            render();
            return;
        }

        if (actionHasInlineForm(actionId) || actionId === 'approve' || actionId === 'reject') {
            handleSwalAction(actionId);
            return;
        }

        var route = actionRoute(actionId, task.status);
        if (!route) {
            setNotice('warning', 'Action route is not defined.');
            render();
            return;
        }

        if (actionId === 'accept' && task.status === 'PendingAcceptance') {
            await postAction(route);
            return;
        }

        if (actionId === 'start' && (task.status === 'Open' || task.status === 'Planned')) {
            await postAction(route);
            return;
        }

        if (actionId === 'cancel') {
            handleSwalAction(actionId);
            return;
        }

        var res = await Swal.fire({
            title: 'Are you sure?',
            text: 'Do you want to proceed with this action?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, proceed',
            customClass: { confirmButton: 'btn btn-primary me-3', cancelButton: 'btn btn-label-secondary' },
            buttonsStyling: false
        });

        if (res.isConfirmed) {
            await postAction(route);
        }
    }

    function getActionDescription(task, actionId) {
        switch (task.status) {
            case 'PendingApproval':
                if (actionId === 'approve') return 'Releases task to assignee inbox.';
                if (actionId === 'reject') return 'Cancels the task permanently.';
                if (actionId === 'cancel') return 'Withdraws the task from approval and cancels it.';
                break;
            case 'PendingAcceptance':
                if (actionId === 'accept') return 'Moves task to Open.';
                if (actionId === 'reassign') return 'Assigns task to another owner.';
                break;
            case 'Open':
                if (actionId === 'plan') return 'Moves task to Planned with a target date.';
                if (actionId === 'start') return 'Moves task to In Progress.';
                if (actionId === 'reassign') return 'Assigns task to another owner.';
                break;
            case 'Planned':
                if (actionId === 'start') return 'Moves task to In Progress.';
                if (actionId === 'reassign') return 'Assigns task to another owner.';
                break;
            case 'InProgress':
                if (actionId === 'complete') {
                    return task.gates.review.required
                        ? 'Sends task to review.'
                        : 'Closes the task.';
                }
                if (actionId === 'logTime') return 'Records a time entry.';
                if (actionId === 'requestInfo') return 'Pauses task and moves it to Waiting for Information.';
                if (actionId === 'reassign') return 'Assigns task to another owner.';
                if (actionId === 'cancel') return 'Permanently cancels this task.';
                break;
            case 'WaitingForInformation':
                if (actionId === 'resume') return 'Returns task to previous state. Answer open information requests before resuming.';
                if (actionId === 'reassign') return 'Assigns task to another owner.';
                if (actionId === 'cancel') return 'Permanently cancels this task.';
                break;
            case 'PendingReview':
                if (actionId === 'approve') return 'Closes the task.';
                if (actionId === 'reject') return 'Returns task to In Progress.';
                break;
        }

        return '';
    }

    function getActionLayout(status) {
        switch (status) {
            case 'PendingApproval':
                return { primary: ['approve'], secondary: ['reject'], other: ['cancel'] };
            case 'PendingAcceptance':
                return { primary: ['accept'], secondary: [], other: ['reassign'] };
            case 'Open':
                return { primary: ['plan', 'start'], secondary: [], other: ['reassign'] };
            case 'Planned':
                return { primary: ['start'], secondary: [], other: ['reassign'] };
            case 'InProgress':
                return { primary: ['complete'], secondary: ['logTime', 'requestInfo'], other: ['reassign', 'cancel'] };
            case 'WaitingForInformation':
                return { primary: ['resume'], secondary: [], other: ['reassign', 'cancel'] };
            case 'PendingReview':
                return { primary: ['approve'], secondary: ['reject'], other: [] };
            default:
                return { primary: [], secondary: [], other: [] };
        }
    }

    function getButtonClass(actionId, lane) {
        if (actionId === 'approve') {
            return 'btn btn-success';
        }

        if (actionId === 'reject') {
            return 'btn btn-outline-danger';
        }

        if (lane === 'primary') {
            return 'btn btn-dark';
        }

        if (lane === 'secondary') {
            return 'btn btn-outline-secondary';
        }

        return 'btn btn-outline-dark';
    }

    function renderNotification() {
        if (!state.notice) {
            return '';
        }

        var cls = state.notice.type === 'success'
            ? 'alert alert-success'
            : (state.notice.type === 'warning' ? 'alert alert-warning' : 'alert alert-danger');

        return '<div class="' + cls + ' d-flex mb-4" role="alert">' +
            '<span class="alert-icon rounded-circle"><i class="bx ' + (state.notice.type === 'success' ? 'bx-check' : 'bx-error') + '"></i></span>' +
            '<div class="d-flex flex-column ps-1">' +
            '<h6 class="alert-heading d-flex align-items-center fw-bold mb-1">' + escapeHtml(state.notice.type === 'success' ? 'Success' : 'Attention') + '</h6>' +
            '<span>' + escapeHtml(state.notice.message) + '</span>' +
            '</div></div>';
    }

    function renderRoleChip(label, person) {
        if (!person) {
            return '';
        }

        return '<div class="d-flex align-items-center me-4 mb-2">' +
            '<div class="avatar avatar-sm me-2">' +
                '<span class="avatar-initial rounded-circle bg-label-primary">' + escapeHtml(person.initials || '--') + '</span>' +
            '</div>' +
            '<div class="d-flex flex-column">' +
                '<span class="fw-medium">' + escapeHtml(person.name || '-') + '</span>' +
                '<small class="text-muted">' + escapeHtml(label) + '</small>' +
            '</div>' +
        '</div>';
    }

    function renderHeader(task) {
        var assigneeLabel = task.assignee && task.assignee.isPlanned ? 'Planned Assignee' : 'Assignee';
        var localizedAssigneeLabel = t(task.assignee && task.assignee.isPlanned ? 'RolePlannedAssignee' : 'RoleAssignee', assigneeLabel);

        var urgency = task.urgency ? urgencyMap[task.urgency] : null;
        var urgencyBadge = urgency
            ? '<span class="' + urgency.cls + '"><i class="bx bx-error-circle me-1"></i>' + escapeHtml(urgency.label) + '</span>'
            : '';
        var planDateFact = task.planDate
            ? '<div class="d-flex flex-column"><span><i class="bx bx-calendar-event me-1"></i> ' + escapeHtml(t('PlanDate', 'Plan date')) + '</span><strong class="text-body">' + escapeHtml(formatDate(task.planDate)) + '</strong></div>'
            : '';

        return '' +
            '<div class="card mb-4">' +
                '<div class="card-body">' +
                    '<div class="d-flex flex-column flex-md-row justify-content-between align-items-start mb-3">' +
                        '<a class="btn btn-label-secondary btn-sm mb-3 mb-md-0" href="' + escapeHtml(returnUrl) + '"><i class="bx bx-arrow-back me-1"></i> ' + escapeHtml(t('BackToList', 'Back to list')) + '</a>' +
                        '<div class="d-flex flex-wrap justify-content-md-end">' +
                            renderRoleChip(localizedAssigneeLabel, task.assignee) +
                            renderRoleChip(t('RoleCreator', 'Creator'), task.creator) +
                            renderRoleChip(t('RoleReviewer', 'Reviewer'), task.reviewer) +
                            renderRoleChip(t('RoleApprover', 'Approver'), task.approver) +
                        '</div>' +
                    '</div>' +
                    '<div class="d-flex align-items-center gap-2 mb-2">' +
                        '<span class="text-muted small">[' + escapeHtml(task.id) + ']</span>' +
                        '<span class="' + statusBadgeClass(task.status) + '">' + escapeHtml(statusLabel(task.status)) + '</span>' +
                        urgencyBadge +
                    '</div>' +
                    '<h4 class="card-title mb-4">' + escapeHtml(task.title) + '</h4>' +
                    '<div class="d-flex flex-wrap gap-4 small text-muted">' +
                        '<div class="d-flex flex-column"><span><i class="bx bx-calendar me-1"></i> ' + escapeHtml(t('DueDate', 'Due date')) + '</span><strong class="text-body">' + escapeHtml(formatDate(task.dueDate)) + '</strong></div>' +
                        planDateFact +
                        '<div class="d-flex flex-column"><span><i class="bx bx-user me-1"></i> ' + escapeHtml(t('RoleCreator', 'Creator')) + '</span><strong class="text-body">' + escapeHtml(task.creator && task.creator.name ? task.creator.name : '-') + '</strong></div>' +
                        '<div class="d-flex flex-column"><span><i class="bx bx-show me-1"></i> ' + escapeHtml(t('RoleReviewer', 'Reviewer')) + '</span><strong class="text-body">' + escapeHtml(task.reviewer && task.reviewer.name ? task.reviewer.name : '-') + '</strong></div>' +
                    '</div>' +
                '</div>' +
            '</div>';
    }

    function renderBanner(task) {
        var role = task.userRole;
        var status = task.status;
        var pendingRequests = (task.informationRequests || []).filter(function (req) {
            return req.status === 'Waiting';
        }).length;

        var title = '';
        var lines = [];
        var cls = 'task-v2-banner task-v2-banner--muted';

        if (status === 'PendingApproval') {
            cls = role === 'approver'
                ? 'task-v2-banner task-v2-banner--pink'
                : 'task-v2-banner task-v2-banner--pink-soft';
            title = 'Task requires approval before assignment';
            if (role === 'approver') {
                lines.push('You are the approver.');
                lines.push('Awaiting your decision.');
            } else {
                lines.push('Awaiting decision from ' + (task.approver && task.approver.name ? task.approver.name : 'approver') + '.');
            }
        } else if (status === 'PendingAcceptance') {
            if (role === 'assignee') {
                cls = 'task-v2-banner task-v2-banner--yellow';
                title = 'This task is waiting for your acceptance';
                lines.push('You are the assignee.');
                lines.push('Accept to begin working, or reassign if this is not yours.');
            } else {
                cls = 'task-v2-banner task-v2-banner--muted';
                title = 'Waiting for assignee acceptance.';
            }
        } else if (status === 'WaitingForInformation') {
            cls = 'task-v2-banner task-v2-banner--orange';
            title = 'Task is paused — waiting for information';
            if (role === 'assignee') {
                lines.push('You requested information.');
                lines.push(pendingRequests + ' requests pending.');
            }
        } else if (status === 'PendingReview') {
            cls = 'task-v2-banner task-v2-banner--purple';
            title = 'Work completed — awaiting decision';
            if (role === 'reviewer') {
                lines.push('You are the reviewer.');
            } else if (task.reviewer && task.reviewer.name) {
                lines.push(task.reviewer.name + ' needs to approve or reject.');
            }
        } else if (status === 'Closed') {
            cls = 'task-v2-banner task-v2-banner--green';
            title = 'Task completed and closed';
            if (task.closureSummary) {
                lines.push('See Closure Summary section for details.');
            }
        } else if (status === 'Cancelled') {
            cls = 'task-v2-banner task-v2-banner--red';
            title = 'Task has been cancelled';
            var approval = task.gates && task.gates.approval ? task.gates.approval : null;
            var rejectedByApproval = approval && approval.status === 'rejected';
            if (!rejectedByApproval && approval && Array.isArray(approval.history)) {
                rejectedByApproval = approval.history.some(function (entry) {
                    return entry && entry.text && /reject/i.test(String(entry.text));
                });
            }
            if (rejectedByApproval) {
                lines.push('Cancelled after approval rejection.');
            }
            lines.push(task.cancellationReason || 'No cancellation reason provided.');
        }

        if (!title) {
            return '';
        }

        return '<section class="' + cls + '">' +
            '<h3>' + escapeHtml(title) + '</h3>' +
            lines.map(function (line) {
                return '<p>' + escapeHtml(line) + '</p>';
            }).join('') +
        '</section>';
    }

    function blockerBadgeText(sourceKind, dependencyType) {
        if (sourceKind === 'completion') {
            return 'Prevents completion';
        }

        if (dependencyType === 'FF') {
            return 'Prevents completion';
        }

        return 'Prevents starting work';
    }

    function renderDependencyBlocker(item, sourceKind) {
        var depType = item.dependencyType || '';
        var depTypeLabel = dependencyTypeLabelMap[depType] || depType;

        return '<div class="task-v2-blocker-item">' +
            '<div class="task-v2-blocker-item__head">' +
                '<span class="task-v2-blocker-icon">⊙</span>' +
                '<div>' +
                    '<strong>Depends on ' + escapeHtml(item.taskId || '-') + ' (' + escapeHtml(depTypeLabel) + ')</strong>' +
                    '<span class="task-v2-pill task-v2-pill--danger">' + blockerBadgeText(sourceKind, depType) + '</span>' +
                '</div>' +
            '</div>' +
            '<p>"' + escapeHtml(item.title || '-') + '" must be closed first.</p>' +
        '</div>';
    }

    function renderSubtaskBlocker(item) {
        var list = (item.items || []).map(function (subtask) {
            return escapeHtml(subtask.id) + ': ' +
                escapeHtml(subtask.title) +
                ' (' + escapeHtml(statusLabel(subtask.status)) + ')';
        }).join('; ');

        return '<div class="task-v2-blocker-item">' +
            '<div class="task-v2-blocker-item__head">' +
                '<span class="task-v2-blocker-icon">⊙</span>' +
                '<div>' +
                    '<strong>' + String(item.count || 0) + ' subtasks still open</strong>' +
                    '<span class="task-v2-pill task-v2-pill--danger">Prevents completion</span>' +
                '</div>' +
            '</div>' +
            '<p>' + escapeHtml(list || '-') + '</p>' +
        '</div>';
    }

    function renderBlockers(task) {
        var blockers = task.blockers || {};
        var starting = blockers.startingWork || [];
        var completion = blockers.completion || [];
        var total = starting.length + completion.length;

        if (!total) {
            return '';
        }

        var parts = [];

        if (starting.length) {
            parts.push('<h4 class="task-v2-blocker-group-title">Blocking starting work</h4>');
            starting.forEach(function (item) {
                parts.push(renderDependencyBlocker(item, 'starting'));
            });
        }

        if (completion.length) {
            parts.push('<h4 class="task-v2-blocker-group-title">Blocking completion</h4>');
            completion.forEach(function (item) {
                if (item.type === 'subtasks') {
                    parts.push(renderSubtaskBlocker(item));
                } else {
                    parts.push(renderDependencyBlocker(item, 'completion'));
                }
            });
        }

        return '<section class="task-v2-card task-v2-blockers">' +
            '<header>⊙ ' + total + ' blockers preventing actions</header>' +
            '<div class="task-v2-blockers__body">' + parts.join('') + '</div>' +
        '</section>';
    }

    function renderInlineForm(actionId, task) {
        if (!task || !state.openForm || state.openForm !== actionId) {
            return '';
        }

        if (task.status === 'Open' && actionId === 'plan') {
            return '' +
                '<form class="task-v2-inline-form" data-inline-form="plan">' +
                    '<label for="inline-plan-date">' + escapeHtml(t('PlanDate', 'Plan date')) + '</label>' +
                    '<input id="inline-plan-date" class="form-control" type="date" name="planDate" value="' + escapeHtml(state.forms.planDate) + '" required>' +
                    '<div class="task-v2-inline-form__actions">' +
                        '<button type="submit" class="btn btn-primary btn-sm me-2"' + (state.pending ? ' disabled' : '') + '>Save Plan</button>' +
                        '<button type="button" class="btn btn-label-secondary btn-sm" data-close-inline-form="plan"' + (state.pending ? ' disabled' : '') + '>Cancel</button>' +
                    '</div>' +
                '</form>';
        }

        if (task.status === 'InProgress' && actionId === 'logTime') {
            return '' +
                '<form class="task-v2-inline-form" data-inline-form="logTime">' +
                    '<label for="inline-log-date">Date</label>' +
                    '<input id="inline-log-date" class="form-control" type="date" name="logDate" value="' + escapeHtml(state.forms.logDate) + '" required>' +
                    '<label for="inline-log-description">Description</label>' +
                    '<input id="inline-log-description" class="form-control" type="text" name="logDescription" value="' + escapeHtml(state.forms.logDescription) + '" placeholder="What did you work on?" required>' +
                    '<label for="inline-log-duration">Duration</label>' +
                    '<input id="inline-log-duration" class="form-control" type="text" name="logDuration" value="' + escapeHtml(state.forms.logDuration) + '" placeholder="e.g. 2h, 1.5h, 01:30" required>' +
                    '<div class="task-v2-inline-form__actions">' +
                        '<button type="submit" class="btn btn-primary btn-sm me-2"' + (state.pending ? ' disabled' : '') + '>Record Time</button>' +
                        '<button type="button" class="btn btn-label-secondary btn-sm" data-close-inline-form="logTime"' + (state.pending ? ' disabled' : '') + '>Cancel</button>' +
                    '</div>' +
                '</form>';
        }

        if (task.status === 'InProgress' && actionId === 'requestInfo') {
            var options = (task.directory || []).map(function (person) {
                var selected = person.id === state.forms.requestRecipientId ? ' selected' : '';
                return '<option value="' + escapeHtml(person.id) + '"' + selected + '>' + escapeHtml(person.name) + '</option>';
            }).join('');

            return '' +
                '<form class="task-v2-inline-form" data-inline-form="requestInfo">' +
                    '<label for="inline-request-recipient">Recipient</label>' +
                    '<select id="inline-request-recipient" class="form-select" name="requestRecipientId" required>' + options + '</select>' +
                    '<label for="inline-request-question">Question</label>' +
                    '<textarea id="inline-request-question" class="form-control" rows="3" name="requestQuestion" placeholder="What information is needed?" required>' + escapeHtml(state.forms.requestQuestion) + '</textarea>' +
                    '<div class="task-v2-inline-form__actions">' +
                        '<button type="submit" class="btn btn-primary btn-sm me-2"' + (state.pending ? ' disabled' : '') + '>Send Request</button>' +
                        '<button type="button" class="btn btn-label-secondary btn-sm" data-close-inline-form="requestInfo"' + (state.pending ? ' disabled' : '') + '>Cancel</button>' +
                    '</div>' +
                '</form>';
        }

        if (task.status === 'InProgress' && actionId === 'complete') {
            return '' +
                '<form class="task-v2-inline-form" data-inline-form="complete">' +
                    '<label for="inline-complete-summary">Closure Summary (optional)</label>' +
                    '<textarea id="inline-complete-summary" class="form-control" rows="4" name="closureSummary" placeholder="Describe what was done...">' + escapeHtml(state.forms.closureSummary) + '</textarea>' +
                    '<div class="task-v2-inline-form__actions">' +
                        '<button type="submit" class="btn btn-primary btn-sm me-2"' + (state.pending ? ' disabled' : '') + '>Complete</button>' +
                        '<button type="button" class="btn btn-label-secondary btn-sm" data-close-inline-form="complete"' + (state.pending ? ' disabled' : '') + '>Cancel</button>' +
                    '</div>' +
                '</form>';
        }

        return '';
    }

    function renderActionButton(task, action, lane) {
        var description = getActionDescription(task, action.id);
        var disabled = (action.isBlocked || state.pending) ? 'disabled' : '';

        var body = '' +
            '<div class="d-flex align-items-center mb-2">' +
                '<button type="button" class="' + getButtonClass(action.id, lane) + ' me-2" data-action-id="' + escapeHtml(action.id) + '" ' + disabled + '>' +
                    escapeHtml(action.label) +
                '</button>' +
            '</div>' +
            (description ? '<p class="text-muted small mb-1">' + escapeHtml(description) + '</p>' : '');

        if (action.isBlocked && action.blockReason) {
            body += '<p class="text-danger small mb-0"><i class="bx bx-error-circle me-1"></i>' + escapeHtml(action.label) + ': ' + escapeHtml(action.blockReason) + '</p>';
        }

        return '<div class="mb-4">' + body + renderInlineForm(action.id, task) + '</div>';
    }

    function renderChecklistHint(task) {
        var checklist = task.checklist || { total: 0, completed: 0 };
        var remaining = Math.max(0, (checklist.total || 0) - (checklist.completed || 0));

        if (!remaining) {
            return '';
        }

        if (task.status === 'InProgress') {
            return '<p class="task-v2-checklist-hint task-v2-checklist-hint--warning">⚠ ' + remaining + ' of ' + checklist.total + ' checklist items incomplete (non-blocking).</p>';
        }

        if (task.status === 'PendingReview') {
            return '<p class="task-v2-checklist-hint task-v2-checklist-hint--info">ℹ ' + remaining + ' of ' + checklist.total + ' checklist items incomplete (non-blocking).</p>';
        }

        return '';
    }

    function shouldRenderDecisionNote(task) {
        if (!task || !task.availableActions) {
            return false;
        }

        var hasDecisionActions = task.availableActions.some(function (action) {
            return action.id === 'approve' || action.id === 'reject';
        });

        if (!hasDecisionActions) {
            return false;
        }

        if (task.status === 'PendingApproval') {
            return task.userRole === 'approver';
        }

        if (task.status === 'PendingReview') {
            return task.userRole === 'reviewer';
        }

        return false;
    }

    function renderActions(task) {
        if (task.status === 'Closed' || task.status === 'Cancelled') {
            return '';
        }

        var actions = task.availableActions || [];
        var layout = getActionLayout(task.status);
        var actionsById = {};
        actions.forEach(function (action) {
            actionsById[action.id] = action;
        });

        var noActionsForPendingApproval = task.status === 'PendingApproval' && task.userRole !== 'approver' && task.userRole !== 'assignee';
        var noActionsForPendingAcceptance = task.status === 'PendingAcceptance' && task.userRole !== 'assignee';
        var noActionsForPlanned = task.status === 'Planned' && task.userRole !== 'assignee';
        var noActionsForWaitingForInformation = task.status === 'WaitingForInformation' && task.userRole !== 'assignee';
        var noActionsForPendingReview = task.status === 'PendingReview' && task.userRole !== 'reviewer';

        if (noActionsForPendingApproval) {
            return '<section class="task-v2-card task-v2-actions">' +
                '<header>AVAILABLE ACTIONS</header>' +
                '<div class="task-v2-actions__body">' +
                    '<p class="task-v2-awaiting-note">Awaiting approval from ' +
                        escapeHtml(task.approver && task.approver.name ? task.approver.name : 'approver') +
                        ' before this task can be assigned.</p>' +
                '</div>' +
            '</section>';
        }

        if (noActionsForPendingAcceptance) {
            return '<section class="task-v2-card task-v2-actions">' +
                '<header>AVAILABLE ACTIONS</header>' +
                '<div class="task-v2-actions__body">' +
                    '<p class="task-v2-awaiting-note">Waiting for assignee acceptance.</p>' +
                '</div>' +
            '</section>';
        }

        if (noActionsForPlanned) {
            return '<section class="task-v2-card task-v2-actions">' +
                '<header>AVAILABLE ACTIONS</header>' +
                '<div class="task-v2-actions__body">' +
                    '<p class="task-v2-awaiting-note">Task is planned and waiting for execution.</p>' +
                '</div>' +
            '</section>';
        }

        if (noActionsForWaitingForInformation) {
            return '<section class="task-v2-card task-v2-actions">' +
                '<header>AVAILABLE ACTIONS</header>' +
                '<div class="task-v2-actions__body">' +
                    '<p class="task-v2-awaiting-note">Task is paused and waiting for information.</p>' +
                '</div>' +
            '</section>';
        }

        if (noActionsForPendingReview) {
            return '<section class="task-v2-card task-v2-actions">' +
                '<header>AVAILABLE ACTIONS</header>' +
                '<div class="task-v2-actions__body">' +
                    '<p class="task-v2-awaiting-note">Work completed — awaiting reviewer decision.</p>' +
                '</div>' +
            '</section>';
        }

        var rendered = [];

        function renderLane(laneName, ids) {
            var laneItems = ids
                .map(function (id) { return actionsById[id]; })
                .filter(function (x) { return !!x; })
                .map(function (action) { return renderActionButton(task, action, laneName); })
                .join('');

            if (!laneItems) {
                return '';
            }

            return '<div class="task-v2-action-lane task-v2-action-lane--' + laneName + '">' + laneItems + '</div>';
        }

        rendered.push(renderLane('primary', layout.primary));
        rendered.push(renderLane('secondary', layout.secondary));
        rendered.push(renderLane('other', layout.other));

        var content = rendered.join('');
        if (!content) {
            content = '<p class="task-v2-empty">No actions available for this status and role.</p>';
        }

        var decisionNote = shouldRenderDecisionNote(task)
            ? '<div class="task-v2-decision-note">' +
                '<label>Add decision note (optional)</label>' +
                '<textarea class="form-control" rows="3" name="decisionNote" placeholder="Explain your decision for future reference...">' + escapeHtml(state.decisionNote) + '</textarea>' +
                '<small>This note will be recorded in the activity log.</small>' +
              '</div>'
            : '';

        return '<section class="task-v2-card task-v2-actions">' +
            '<header>AVAILABLE ACTIONS</header>' +
            '<div class="task-v2-actions__body">' +
                content +
                renderChecklistHint(task) +
                decisionNote +
            '</div>' +
        '</section>';
    }

    function renderStepper(task) {
        var lifecycle = task.lifecycle || { steps: [] };
        var steps = lifecycle.steps || [];

        var html = steps.map(function (step, index) {
            var stateClass = 'task-v2-step task-v2-step--' + step.state;
            var marker = step.state === 'completed'
                ? '✓'
                : String(index + 1);

            var connectorClass = 'task-v2-step-connector';
            if (index > 0) {
                var prev = steps[index - 1];
                connectorClass += prev.state === 'completed' ? ' task-v2-step-connector--done' : ' task-v2-step-connector--upcoming';
            }

            return '' +
                (index > 0 ? '<div class="' + connectorClass + '"></div>' : '') +
                '<div class="' + stateClass + '">' +
                    '<span class="task-v2-step__index">' + marker + '</span>' +
                    '<span class="task-v2-step__label">' + escapeHtml(step.label) + '</span>' +
                '</div>';
        }).join('');

        var pauseLine = lifecycle.isPaused
            ? '<p class="task-v2-stepper-line task-v2-stepper-line--paused">⊙ Currently paused: Waiting for Information</p>'
            : '';

        var cancelLine = lifecycle.isCancelled
            ? '<p class="task-v2-stepper-line task-v2-stepper-line--cancelled">✕ Task cancelled</p>'
            : '';

        return '<section class="task-v2-card task-v2-stepper">' +
            '<div class="task-v2-stepper__track">' + html + '</div>' +
            pauseLine +
            cancelLine +
        '</section>';
    }

    function sectionCard(id, title, body, subtitle) {
        if (!body) {
            return '';
        }

        var collapsed = isCollapsed(id);

        return '<div class="card mb-4">' +
            '<div class="card-header d-flex justify-content-between align-items-center cursor-pointer" data-toggle-section="' + escapeHtml(id) + '">' +
                '<div class="d-flex flex-column">' +
                    '<h5 class="card-title mb-0">' + escapeHtml(title) + '</h5>' +
                    (subtitle ? '<small class="text-muted">' + escapeHtml(subtitle) + '</small>' : '') +
                '</div>' +
                '<i class="bx ' + (collapsed ? 'bx-chevron-right' : 'bx-chevron-down') + '"></i>' +
            '</div>' +
            '<div class="card-body ' + (collapsed ? 'd-none' : '') + '">' + body + '</div>' +
        '</div>';
    }

    function renderDescriptionAndContext(task) {
        if (task && task.status === 'PendingApproval') {
            return '';
        }

        var body = '<div class="task-v2-two-col">' +
            '<article>' +
                '<h4>Description</h4>' +
                '<p>' + escapeHtml(task.description || '-') + '</p>' +
            '</article>' +
            '<article>' +
                '<h4>Business Context</h4>' +
                '<p>' + escapeHtml(task.businessContext || '-') + '</p>' +
            '</article>' +
        '</div>';

        return sectionCard('description-context', 'Description + Business Context', body);
    }

    function renderBusinessContextReview(task) {
        if (task.status !== 'PendingApproval') {
            return '';
        }

        var body = '<div class="task-v2-highlight">' +
            '<h4>Review before deciding</h4>' +
            '<p>' + escapeHtml(task.businessContext || '-') + '</p>' +
        '</div>';

        return sectionCard('business-context-review', 'Business Context', body);
    }

    function renderDependencySummary(task) {
        if (task.status !== 'PendingApproval') {
            return '';
        }

        var blockedBy = (task.dependencies || []).filter(function (dep) {
            return dep.relation === 'BlockedBy';
        });

        var body = blockedBy.length
            ? '<ul class="task-v2-simple-list">' +
                blockedBy.map(function (dep) {
                    return '<li>' +
                        '<strong>' + escapeHtml(dep.id) + '</strong> · ' +
                        escapeHtml(dep.title) + ' (' + escapeHtml(dependencyTypeLabelMap[dep.type] || dep.type) + ')' +
                    '</li>';
                }).join('') +
              '</ul>'
            : '<p class="task-v2-empty">No dependencies.</p>';

        return sectionCard('dependency-summary', 'Dependency Summary', body);
    }

    function renderClosureSummary(task) {
        if (task.status !== 'PendingReview' && task.status !== 'Closed' && task.status !== 'Cancelled') {
            return '';
        }

        if (task.status === 'Cancelled' && !task.closureSummary) {
            return '';
        }

        var body = task.closureSummary
            ? '<p>' + escapeHtml(task.closureSummary) + '</p>'
            : '<p class="task-v2-empty">Closure summary is not provided.</p>';

        return sectionCard('closure-summary', 'Closure Summary', body);
    }

    function renderSubtasks(task) {
        var subtasks = task.subtasks || [];
        if (task.status === 'PendingApproval' && subtasks.length === 0) {
            return '';
        }

        var openCount = subtasks.filter(function (subtask) {
            return subtask.status !== 'Closed' && subtask.status !== 'Cancelled';
        }).length;

        var summary = subtasks.length + ' total · ' + openCount + ' open';

        var allClosedBanner = openCount === 0 && subtasks.length > 0
            ? '<div class="task-v2-good-banner">All subtasks completed — no subtask-related blockers.</div>'
            : '';

        var body = subtasks.length
            ? allClosedBanner +
                '<div class="task-v2-list">' + subtasks.map(function (subtask) {
                    return '<div class="task-v2-list-row">' +
                        '<div class="task-v2-list-row__main">' +
                            '<strong>' + escapeHtml(subtask.id) + '</strong> ' + escapeHtml(subtask.title) +
                        '</div>' +
                        '<div class="task-v2-list-row__meta">' +
                            '<span class="task-v2-avatar task-v2-avatar--sm">' + escapeHtml(subtask.assignee && subtask.assignee.initials ? subtask.assignee.initials : '--') + '</span>' +
                            '<span class="' + statusBadgeClass(subtask.status) + '">● ' + escapeHtml(statusLabel(subtask.status)) + '</span>' +
                        '</div>' +
                    '</div>';
                }).join('') + '</div>'
            : '<p class="task-v2-empty">No subtasks.</p>';

        return sectionCard('subtasks', 'Subtasks', body, summary);
    }

    function renderChecklist(task) {
        var checklist = task.checklist || { total: 0, completed: 0, items: [] };
        var total = checklist.total || 0;
        var completed = checklist.completed || 0;
        var progress = total > 0 ? Math.round((completed / total) * 100) : 0;

        var list = (checklist.items || []).map(function (item) {
            return '<li class="task-v2-check-row">' +
                '<span>' + (item.isCompleted ? '☑' : '☐') + '</span>' +
                '<span>' + escapeHtml(item.text) + '</span>' +
            '</li>';
        }).join('');

        var body = '' +
            '<p class="task-v2-muted">This checklist does not block completion. Incomplete items are kept for reference.</p>' +
            '<div class="task-v2-progress-wrap">' +
                '<div class="task-v2-progress"><span style="width:' + progress + '%"></span></div>' +
                '<small>' + progress + '%</small>' +
            '</div>' +
            '<ul class="task-v2-checklist">' + (list || '<li class="task-v2-empty">No checklist items.</li>') + '</ul>';

        return sectionCard('checklist', 'Checklist', body, completed + '/' + total + ' · Non-blocking');
    }

    function renderDependencyLine(dep) {
        var relationBadge = dep.relation === 'BlockedBy'
            ? '<span class="task-v2-pill task-v2-pill--danger">BLOCKED BY</span>'
            : '<span class="task-v2-pill task-v2-pill--info">BLOCKS</span>';

        var blockerBadge = '';
        if (dep.relation === 'BlockedBy' && dep.isActive) {
            if (dep.type === 'FF') {
                blockerBadge = '<span class="task-v2-pill task-v2-pill--danger">Prevents completion</span>';
            } else if (dep.type === 'FS' || dep.type === 'SS') {
                blockerBadge = '<span class="task-v2-pill task-v2-pill--danger">Prevents starting work</span>';
            }
        }

        return '<div class="task-v2-dep-row">' +
            '<div class="task-v2-dep-row__left">' +
                relationBadge +
                '<div><strong>' + escapeHtml(dep.title) + '</strong></div>' +
                '<small>' + escapeHtml(dep.id) + ' · ' + escapeHtml(dependencyTypeLabelMap[dep.type] || dep.type) + '</small>' +
            '</div>' +
            '<div class="task-v2-dep-row__right">' +
                blockerBadge +
                '<span class="' + statusBadgeClass(dep.status) + '">● ' + escapeHtml(statusLabel(dep.status)) + '</span>' +
            '</div>' +
        '</div>';
    }

    function renderDependencies(task) {
        var dependencies = task.dependencies || [];
        if (task.status === 'PendingApproval' && dependencies.length === 0) {
            return '';
        }

        var blockingCount = dependencies.filter(function (dep) {
            if (!(dep.relation === 'BlockedBy' && dep.isActive)) {
                return false;
            }
            return dep.type === 'FS' || dep.type === 'SS' || dep.type === 'FF' || dep.type === 'SF';
        }).length;

        var body = dependencies.length
            ? '<div class="task-v2-list">' + dependencies.map(renderDependencyLine).join('') + '</div>'
            : '<p class="task-v2-empty">No dependencies.</p>';

        return sectionCard('dependencies', 'Dependencies', body, dependencies.length + ' total · ' + blockingCount + ' blocking');
    }

    function renderInformationRequests(task) {
        var requests = task.informationRequests || [];
        if (task.status === 'PendingApproval' && requests.length === 0) {
            return '';
        }

        var waitingCount = requests.filter(function (req) { return req.status === 'Waiting'; }).length;

        var pauseWarning = task.timeLogged && task.timeLogged.isPaused
            ? '<p class="task-v2-warning">⚠ Open requests are blocking task completion. Recipients must answer before the task can be completed.</p>'
            : '';

        var currentUserId = actingAs || '';

        var body = requests.length
            ? pauseWarning +
                '<div class="task-v2-list">' + requests.map(function (req) {
                    var answerBlock = req.answer
                        ? '<blockquote>' + escapeHtml(req.answer) + '<footer>Answered ' + escapeHtml(formatDate(req.answeredAt)) + '</footer></blockquote>'
                        : '';

                    var isRecipient = currentUserId && req.to && req.to.id &&
                        req.to.id.toLowerCase() === currentUserId.toLowerCase();
                    var answerBtn = (req.status === 'Waiting' && isRecipient)
                        ? '<button class="btn btn-sm btn-outline-primary mt-2" data-ir-answer-id="' + escapeHtml(req.id) + '">Answer</button>'
                        : '';

                    return '<div class="task-v2-info-row">' +
                        '<div class="task-v2-info-row__head">' +
                            '<strong>' + escapeHtml(req.from && req.from.name ? req.from.name : '-') + ' → ' + escapeHtml(req.to && req.to.name ? req.to.name : '-') + '</strong>' +
                            '<span class="' + statusBadgeClass(req.status === 'Waiting' ? 'WaitingForInformation' : 'Closed') + '">' + escapeHtml(req.status) + '</span>' +
                        '</div>' +
                        '<p>' + escapeHtml(req.question) + '</p>' +
                        answerBlock +
                        answerBtn +
                    '</div>';
                }).join('') +
                '</div>'
            : '<p class="task-v2-empty">No information requests.</p>';

        return sectionCard('information-requests', 'Information Requests', body, waitingCount + ' waiting');
    }

    function renderGates(task) {
        var approval = task.gates && task.gates.approval ? task.gates.approval : { required: false, status: 'not_required', history: [] };
        var review = task.gates && task.gates.review ? task.gates.review : { required: false, status: 'not_required' };

        var approvalCardClass = approval.required && task.status === 'PendingApproval'
            ? 'task-v2-gate task-v2-gate--active'
            : 'task-v2-gate';

        var approvalHistory = approval.history && approval.history.length
            ? '<div class="task-v2-gate-history"><h5>APPROVAL HISTORY</h5>' +
                approval.history.map(function (entry) {
                    return '<p>' + escapeHtml(formatDate(entry.timestamp)) + ' — ' + escapeHtml(entry.text) + '</p>';
                }).join('') +
              '</div>'
            : '';

        var reviewHistory = review.history && review.history.length
            ? '<div class="task-v2-gate-history"><h5>REVIEW HISTORY</h5>' +
                review.history.map(function (entry) {
                    return '<p>' + escapeHtml(formatDate(entry.timestamp)) + ' — ' + escapeHtml(entry.text) + '</p>';
                }).join('') +
              '</div>'
            : '';

        var body = '' +
            '<div class="task-v2-gates-grid">' +
                '<article class="' + approvalCardClass + '">' +
                    '<h4>Approval Gate ' + (approval.required && task.status === 'PendingApproval' ? '<span class="task-v2-pill task-v2-pill--violet">ACTIVE</span>' : '') + '</h4>' +
                    '<p>' + (approval.required ? 'Required' : 'Not required') + '</p>' +
                    '<p>' + escapeHtml(task.approver && task.approver.name ? task.approver.initials + ' ' + task.approver.name : '-') + '</p>' +
                    '<small>Approve → Pending Acceptance · Reject → Cancelled</small>' +
                '</article>' +
                '<article class="task-v2-gate">' +
                    '<h4>Review Gate</h4>' +
                    '<p>' + (review.required ? 'Required' : 'Not required') + '</p>' +
                    '<p>' + escapeHtml(task.reviewer && task.reviewer.name ? task.reviewer.initials + ' ' + task.reviewer.name : '-') + '</p>' +
                    '<small>Complete → Pending Review · Approve → Closed · Reject → In Progress</small>' +
                '</article>' +
            '</div>' +
            approvalHistory +
            reviewHistory;

        return sectionCard('gates', 'Review & Approval Gates', body);
    }

    function renderTimeLogged(task) {
        var entries = task.timeLogged && task.timeLogged.entries ? task.timeLogged.entries : [];
        var pauseWarning = task.timeLogged && task.timeLogged.isPaused
            ? '<p class="task-v2-warning">⚠ Time logging is paused while this task is waiting for information.</p>'
            : '';

        var body = entries.length
            ? pauseWarning +
                '<div class="task-v2-list">' + entries.map(function (entry) {
                    return '<div class="task-v2-list-row">' +
                        '<div class="task-v2-list-row__main">' +
                            '<strong>' + escapeHtml(formatDate(entry.date)) + '</strong> ' + escapeHtml(entry.description) +
                        '</div>' +
                        '<div class="task-v2-list-row__meta"><strong>' + escapeHtml(entry.durationFormatted) + '</strong></div>' +
                    '</div>';
                }).join('') + '</div>'
            : '<p class="task-v2-empty">No time entries.</p>';

        return sectionCard('time-logged', 'Time Logged · ' + escapeHtml(task.timeLogged ? task.timeLogged.totalFormatted : '0h'), body);
    }

    function renderActivity(task) {
        var activity = task.activity || [];

        var body = activity.length
            ? '<ul class="timeline ms-1 mb-0">' + activity.map(function (item) {
                var statusBadge = item.statusBadge
                    ? '<span class="' + statusBadgeClass(item.statusBadge) + ' ms-2">' + escapeHtml(statusLabel(item.statusBadge)) + '</span>'
                    : '';
                var activityIcon = iconForActivity(item.type);
                var activityIconClass = iconClassForActivity(item.type);

                return '<li class="timeline-item timeline-item-transparent">' +
                    '<span class="timeline-point timeline-point-primary"></span>' +
                    '<div class="timeline-event">' +
                        '<div class="timeline-header mb-1">' +
                            '<h6 class="mb-0">' + escapeHtml(item.actor && item.actor.name ? item.actor.name : '-') + statusBadge + '</h6>' +
                            '<small class="text-muted">' + escapeHtml(formatDateTime(item.timestamp)) + '</small>' +
                        '</div>' +
                        '<p class="mb-0"><span class="task-v2-activity-inline-icon ' + activityIconClass + '">' + escapeHtml(activityIcon) + '</span>' + escapeHtml(item.text) + '</p>' +
                    '</div>' +
                '</li>';
            }).join('') + '</ul>'
            : '<p class="text-muted mb-0">No activity entries.</p>';

        return sectionCard('activity', 'Activity (' + activity.length + ')', body);
    }

    function renderMetadata(task) {
        var metadata = task.metadata || {};
        var tags = metadata.tags || [];
        var watchers = metadata.watchers || [];
        var effectivePlanDate = metadata.planDate || task.planDate;
        var planDateField = effectivePlanDate
            ? '<div><span>PLAN DATE</span><strong>' + escapeHtml(formatDate(effectivePlanDate)) + '</strong></div>'
            : '';

        var body = '' +
            '<div class="task-v2-metadata-grid">' +
                '<div><span>DOMAIN</span><strong>' + escapeHtml(metadata.domain || '-') + '</strong></div>' +
                '<div><span>SOURCE</span><strong>' + escapeHtml(metadata.source || '-') + '</strong></div>' +
                '<div><span>RELATION</span><strong>' + escapeHtml(metadata.relation || '-') + '</strong></div>' +
                planDateField +
                '<div><span>CREATED</span><strong>' + escapeHtml(formatDate(metadata.createdAt)) + '</strong></div>' +
                '<div><span>LAST UPDATED</span><strong>' + escapeHtml(formatDateTime(metadata.lastUpdatedAt)) + '</strong></div>' +
            '</div>' +
            '<div class="task-v2-meta-group">' +
                '<h5>TAGS</h5>' +
                '<div class="task-v2-chip-wrap">' +
                    (tags.length ? tags.map(function (tag) {
                        return '<span class="task-v2-pill task-v2-pill--neutral">' + escapeHtml(tag) + '</span>';
                    }).join('') : '<span class="task-v2-empty">No tags.</span>') +
                '</div>' +
            '</div>' +
            '<div class="task-v2-meta-group">' +
                '<h5>WATCHERS</h5>' +
                '<div class="task-v2-chip-wrap">' +
                    (watchers.length ? watchers.map(function (watcher) {
                        return '<span class="task-v2-avatar task-v2-avatar--sm">' + escapeHtml(watcher.initials) + '</span>';
                    }).join('') : '<span class="task-v2-empty">No watchers.</span>') +
                '</div>' +
            '</div>';

        return sectionCard('metadata', 'Metadata', body);
    }

    function renderSections(task) {
        if (task && task.status === 'InProgress') {
            return '' +
                renderBusinessContextReview(task) +
                renderDependencySummary(task) +
                renderDescriptionAndContext(task) +
                renderClosureSummary(task) +
                renderSubtasks(task) +
                renderDependencies(task) +
                renderInformationRequests(task) +
                renderTimeLogged(task) +
                renderChecklist(task) +
                renderGates(task) +
                renderActivity(task) +
                renderMetadata(task);
        }

        if (task && task.status === 'WaitingForInformation') {
            return '' +
                renderBusinessContextReview(task) +
                renderDependencySummary(task) +
                renderDescriptionAndContext(task) +
                renderClosureSummary(task) +
                renderInformationRequests(task) +
                renderTimeLogged(task) +
                renderSubtasks(task) +
                renderChecklist(task) +
                renderDependencies(task) +
                renderGates(task) +
                renderActivity(task) +
                renderMetadata(task);
        }

        if (task && task.status === 'PendingReview') {
            return '' +
                renderBusinessContextReview(task) +
                renderDependencySummary(task) +
                renderDescriptionAndContext(task) +
                renderClosureSummary(task) +
                renderTimeLogged(task) +
                renderActivity(task) +
                renderSubtasks(task) +
                renderDependencies(task) +
                renderInformationRequests(task) +
                renderChecklist(task) +
                renderGates(task) +
                renderMetadata(task);
        }

        if (task && task.status === 'Closed') {
            return '' +
                renderBusinessContextReview(task) +
                renderDependencySummary(task) +
                renderDescriptionAndContext(task) +
                renderClosureSummary(task) +
                renderSubtasks(task) +
                renderDependencies(task) +
                renderChecklist(task) +
                renderInformationRequests(task) +
                renderTimeLogged(task) +
                renderActivity(task) +
                renderGates(task) +
                renderMetadata(task);
        }

        if (task && task.status === 'Cancelled') {
            return '' +
                renderBusinessContextReview(task) +
                renderDependencySummary(task) +
                renderDescriptionAndContext(task) +
                renderClosureSummary(task) +
                renderGates(task) +
                renderActivity(task) +
                renderSubtasks(task) +
                renderChecklist(task) +
                renderDependencies(task) +
                renderInformationRequests(task) +
                renderTimeLogged(task) +
                renderMetadata(task);
        }

        return '' +
            renderBusinessContextReview(task) +
            renderDependencySummary(task) +
            renderDescriptionAndContext(task) +
            renderClosureSummary(task) +
            renderSubtasks(task) +
            renderChecklist(task) +
            renderDependencies(task) +
            renderInformationRequests(task) +
            renderGates(task) +
            renderTimeLogged(task) +
            renderActivity(task) +
            renderMetadata(task);
    }

    function renderErrorState() {
        return '<section class="task-v2-error">' +
            '<h3>Task detail could not be loaded</h3>' +
            '<p>' + escapeHtml(state.error || 'Unknown error.') + '</p>' +
            '<button class="btn btn-dark" type="button" data-refresh-task>Retry</button>' +
        '</section>';
    }

    function renderLoadingState() {
        return '<div class="task-v2-loading py-5 text-center">' +
            '<div class="spinner-border text-primary" role="status" aria-hidden="true"></div>' +
            '<p class="mt-3 mb-0 text-muted">Loading task detail...</p>' +
        '</div>';
    }

    function render() {
        if (state.loading) {
            root.innerHTML = renderLoadingState();
            return;
        }

        if (state.error) {
            root.innerHTML = renderErrorState();
            return;
        }

        var task = state.task;
        if (!task) {
            root.innerHTML = '<p class="task-v2-empty">Task not found.</p>';
            return;
        }

        root.innerHTML = '' +
            '<div class="task-v2-shell">' +
                renderNotification() +
                renderHeader(task) +
                renderBanner(task) +
                renderBlockers(task) +
                renderActions(task) +
                renderStepper(task) +
                renderSections(task) +
            '</div>';
    }

    function onClick(event) {
        var refreshBtn = event.target.closest('[data-refresh-task]');
        if (refreshBtn) {
            clearNotice();
            fetchTask(false);
            return;
        }

        var inlineClose = event.target.closest('[data-close-inline-form]');
        if (inlineClose) {
            state.openForm = null;
            render();
            return;
        }

        var sectionToggle = event.target.closest('[data-toggle-section]');
        if (sectionToggle) {
            toggleSection(sectionToggle.getAttribute('data-toggle-section'));
            return;
        }

        var actionButton = event.target.closest('[data-action-id]');
        if (actionButton) {
            var actionId = actionButton.getAttribute('data-action-id');
            clearNotice();
            executeDirectAction(actionId);
        }

        var irAnswerBtn = event.target.closest('[data-ir-answer-id]');
        if (irAnswerBtn) {
            var irId = irAnswerBtn.getAttribute('data-ir-answer-id');
            clearNotice();
            handleIrAnswer(irId);
        }
    }

    function onInput(event) {
        var name = event.target.getAttribute('name');
        if (!name) {
            return;
        }

        if (Object.prototype.hasOwnProperty.call(state.forms, name)) {
            state.forms[name] = event.target.value;
        }

        if (name === 'decisionNote') {
            state.decisionNote = event.target.value;
        }
    }

    async function handleIrAnswer(irId) {
        var res = await Swal.fire({
            title: 'Answer Information Request',
            html: '<div class="text-start mb-3"><label class="form-label">Your Answer</label>' +
                  '<textarea id="swal-ir-answer" class="form-control" rows="4" placeholder="Provide the requested information..."></textarea></div>',
            showCancelButton: true,
            confirmButtonText: 'Submit Answer',
            customClass: { confirmButton: 'btn btn-primary me-3', cancelButton: 'btn btn-label-secondary' },
            buttonsStyling: false,
            preConfirm: function() { return document.getElementById('swal-ir-answer').value; }
        });

        if (res.isConfirmed) {
            if (!res.value || !res.value.trim()) {
                setNotice('warning', 'Answer is required.');
                render();
                return;
            }
            var id = encodeURIComponent(taskId);
            var encodedIrId = encodeURIComponent(irId);
            await postAction('/api/tasks/' + id + '/info-requests/' + encodedIrId + '/answer', { answer: res.value.trim() });
        }
    }

    async function submitInlineForm(formId) {
        var task = state.task;
        if (!task) {
            return;
        }

        if (formId === 'plan') {
            var action = findAction('plan');
            if (!action) {
                setNotice('warning', 'Action is not available in current state.');
                render();
                return;
            }

            if (action.isBlocked) {
                setNotice('warning', action.blockReason || 'Action is blocked.');
                render();
                return;
            }

            if (!state.forms.planDate) {
                setNotice('warning', 'Plan date is required.');
                render();
                return;
            }

            var route = actionRoute('plan', task.status);
            if (!route) {
                setNotice('warning', 'Action route is not defined.');
                render();
                return;
            }

            await postAction(route, { planDate: state.forms.planDate });
            return;
        }

        if (formId === 'logTime') {
            var logAction = findAction('logTime');
            if (!logAction) {
                setNotice('warning', 'Action is not available in current state.');
                render();
                return;
            }

            if (logAction.isBlocked) {
                setNotice('warning', logAction.blockReason || 'Action is blocked.');
                render();
                return;
            }

            if (!state.forms.logDate || !state.forms.logDescription || !state.forms.logDuration) {
                setNotice('warning', 'Date, description and duration are required.');
                render();
                return;
            }

            var logRoute = actionRoute('logTime', task.status);
            if (!logRoute) {
                setNotice('warning', 'Action route is not defined.');
                render();
                return;
            }

            await postAction(logRoute, {
                date: state.forms.logDate,
                description: state.forms.logDescription,
                duration: state.forms.logDuration
            });
            return;
        }

        if (formId === 'requestInfo') {
            var infoAction = findAction('requestInfo');
            if (!infoAction) {
                setNotice('warning', 'Action is not available in current state.');
                render();
                return;
            }

            if (infoAction.isBlocked) {
                setNotice('warning', infoAction.blockReason || 'Action is blocked.');
                render();
                return;
            }

            if (!state.forms.requestRecipientId || !state.forms.requestQuestion) {
                setNotice('warning', 'Recipient and question are required.');
                render();
                return;
            }

            var infoRoute = actionRoute('requestInfo', task.status);
            if (!infoRoute) {
                setNotice('warning', 'Action route is not defined.');
                render();
                return;
            }

            await postAction(infoRoute, {
                recipientId: state.forms.requestRecipientId,
                question: state.forms.requestQuestion
            });
            return;
        }

        if (formId === 'complete') {
            var completeAction = findAction('complete');
            if (!completeAction) {
                setNotice('warning', 'Action is not available in current state.');
                render();
                return;
            }

            if (completeAction.isBlocked) {
                setNotice('warning', completeAction.blockReason || 'Action is blocked.');
                render();
                return;
            }

            var completeRoute = actionRoute('complete', task.status);
            if (!completeRoute) {
                setNotice('warning', 'Action route is not defined.');
                render();
                return;
            }

            await postAction(completeRoute, { closureSummary: state.forms.closureSummary || null });
            return;
        }
    }

    function onSubmit(event) {
        var form = event.target.closest('[data-inline-form]');
        if (!form) {
            return;
        }

        event.preventDefault();
        clearNotice();
        submitInlineForm(form.getAttribute('data-inline-form'));
    }

    root.addEventListener('click', onClick);
    root.addEventListener('input', onInput);
    root.addEventListener('submit', onSubmit);

    fetchTask(true);
})();
